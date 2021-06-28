using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Drone Lights", "WhiteThunder", "1.0.2")]
    [Description("Adds controllable search lights to RC drones.")]
    internal class DroneLights : CovalencePlugin
    {
        #region Fields

        private static DroneLights _pluginInstance;
        private static Configuration _pluginConfig;

        private const string PermissionAutoDeploy = "dronelights.searchlight.autodeploy";
        private const string PermissionMoveLight = "dronelights.searchlight.move";

        private const string SpherePrefab = "assets/prefabs/visualization/sphere.prefab";
        private const string SearchLightPrefab = "assets/prefabs/deployable/search light/searchlight.deployed.prefab";

        private const float SearchLightYAxisRotation = 180;

        private const float SearchLightScale = 0.1f;

        private static readonly Vector3 SphereEntityInitialLocalPosition = new Vector3(0, -100, 0);
        private static readonly Vector3 SphereEntityLocalPosition = new Vector3(0, -0.075f, 0.25f);
        private static readonly Vector3 SearchLightLocalPosition = new Vector3(0, -1.25f, -0.25f);

        private ProtectionProperties ImmortalProtection;

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginInstance = this;
            permission.RegisterPermission(PermissionAutoDeploy, this);
            permission.RegisterPermission(PermissionMoveLight, this);
            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void Unload()
        {
            SearchLightUpdater.DestroyAll();
            UnityEngine.Object.Destroy(ImmortalProtection);
            _pluginInstance = null;
            _pluginConfig = null;
        }

        private void OnServerInitialized()
        {
            ImmortalProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
            ImmortalProtection.name = "DroneLightsProtection";
            ImmortalProtection.Add(1);

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var drone = entity as Drone;
                if (drone == null || !IsDroneEligible(drone))
                    continue;

                AddOrUpdateSearchLight(drone);
            }

            Subscribe(nameof(OnEntitySpawned));
        }

        private void OnEntitySpawned(Drone drone)
        {
            if (!IsDroneEligible(drone))
                return;

            MaybeAutoDeploySearchLight(drone);
        }

        private bool? OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null || arg.cmd.FullName != "inventory.lighttoggle")
                return null;

            var basePlayer = arg.Player();
            if (basePlayer == null)
                return null;

            var drone = GetControlledDrone(basePlayer);
            if (drone == null)
                return null;

            var searchLight = GetDroneSearchLight(drone);
            if (searchLight == null)
                return null;

            searchLight.SetFlag(IOEntity.Flag_HasPower, !searchLight.HasFlag(IOEntity.Flag_HasPower));

            // Prevent other lights from toggling since they are not useful while using the computer station.
            return false;
        }

        private void OnBookmarkControlStarted(ComputerStation station, BasePlayer player, string bookmarkName, Drone drone)
        {
            var searchLight = GetDroneSearchLight(drone);
            if (searchLight == null)
                return;

            if (permission.UserHasPermission(player.UserIDString, PermissionMoveLight))
                drone.GetOrAddComponent<SearchLightUpdater>().Controller = player;
        }

        private void OnBookmarkControlEnded(ComputerStation station, BasePlayer player, Drone drone)
        {
            if (drone == null)
                return;

            var searchLightUpdater = drone.GetOrAddComponent<SearchLightUpdater>();
            if (searchLightUpdater != null)
                UnityEngine.Object.DestroyImmediate(searchLightUpdater);
        }

        #endregion

        #region Helper Methods

        private static bool DeployLightWasBlocked(Drone drone)
        {
            object hookResult = Interface.CallHook("OnDroneSearchLightDeploy", drone);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool IsDroneEligible(Drone drone) =>
            !(drone is DeliveryDrone);

        private static Drone GetControlledDrone(BasePlayer player)
        {
            var computerStation = player.GetMounted() as ComputerStation;
            if (computerStation == null)
                return null;

            return GetControlledDrone(computerStation);
        }

        private static Drone GetControlledDrone(ComputerStation station) =>
            station.currentlyControllingEnt.Get(serverside: true) as Drone;

        private static SearchLight GetDroneSearchLight(Drone drone, out SphereEntity parentSphere) =>
            GetGrandChildOfType<SphereEntity, SearchLight>(drone, out parentSphere);

        private static SearchLight GetDroneSearchLight(Drone drone)
        {
            SphereEntity parentSphere;
            return GetGrandChildOfType<SphereEntity, SearchLight>(drone, out parentSphere);
        }

        private static T2 GetGrandChildOfType<T1, T2>(BaseEntity entity, out T1 childOfType) where T1 : BaseEntity where T2 : BaseEntity
        {
            foreach (var child in entity.children)
            {
                childOfType = child as T1;
                if (childOfType == null)
                    continue;

                foreach (var grandChild in childOfType.children)
                {
                    var grandChildOfType = grandChild as T2;
                    if (grandChildOfType != null)
                        return grandChildOfType;
                }
            }

            childOfType = null;
            return null;
        }

        private static void RemoveProblemComponents(BaseEntity entity)
        {
            foreach (var collider in entity.GetComponentsInChildren<Collider>())
                UnityEngine.Object.DestroyImmediate(collider);

            UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());
        }

        private static void HideInputsAndOutputs(IOEntity ioEntity)
        {
            // Trick to hide the inputs and outputs on the client.
            foreach (var input in ioEntity.inputs)
                input.type = IOEntity.IOType.Generic;

            foreach (var output in ioEntity.outputs)
                output.type = IOEntity.IOType.Generic;
        }

        private static float Clamp(float x, float min, float max) => Math.Max(min, Math.Min(x, max));

        private SearchLight TryDeploySearchLight(Drone drone)
        {
            if (DeployLightWasBlocked(drone))
                return null;

            var sphereLocalPosition = SphereEntityInitialLocalPosition;
            var scale = drone.transform.lossyScale.y;
            if (scale != 0)
                sphereLocalPosition = sphereLocalPosition / scale;

            // Spawn the search light below the map initially while the resize is performed.
            SphereEntity sphereEntity = GameManager.server.CreateEntity(SpherePrefab, sphereLocalPosition) as SphereEntity;
            if (sphereEntity == null)
                return null;

            // Fix the issue where leaving the area and returning would not recreate the sphere and its children on clients.
            sphereEntity.globalBroadcast = false;

            sphereEntity.currentRadius = SearchLightScale;
            sphereEntity.lerpRadius = SearchLightScale;

            sphereEntity.SetParent(drone);
            sphereEntity.Spawn();

            var localRotation = Quaternion.Euler(_pluginConfig.SearchLight.DefaultAngle - 90 % 350, SearchLightYAxisRotation, 0);
            SearchLight searchLight = GameManager.server.CreateEntity(SearchLightPrefab, SearchLightLocalPosition, localRotation) as SearchLight;
            if (searchLight == null)
                return null;

            SetupSearchLight(searchLight);

            searchLight.SetParent(sphereEntity);
            searchLight.Spawn();
            Interface.CallHook("OnDroneSearchLightDeployed", drone, searchLight);

            timer.Once(3, () =>
            {
                if (sphereEntity != null)
                    sphereEntity.transform.localPosition = SphereEntityLocalPosition;
            });

            return searchLight;
        }

        private void SetupSearchLight(SearchLight searchLight)
        {
            RemoveProblemComponents(searchLight);
            HideInputsAndOutputs(searchLight);
            searchLight.SetFlag(BaseEntity.Flags.Busy, true);
            searchLight.baseProtection = ImmortalProtection;
            searchLight.pickup.enabled = false;
        }

        private void AddOrUpdateSearchLight(Drone drone)
        {
            var searchLight = GetDroneSearchLight(drone);
            if (searchLight == null)
            {
                MaybeAutoDeploySearchLight(drone);
                return;
            }

            SetupSearchLight(searchLight);
        }

        private void MaybeAutoDeploySearchLight(Drone drone)
        {
            if (!permission.UserHasPermission(drone.OwnerID.ToString(), PermissionAutoDeploy))
                return;

            TryDeploySearchLight(drone);
        }

        private void OnDroneScaleBegin(Drone drone, BaseEntity rootEntity, float scale, float previousScale)
        {
            if (scale == 0)
                return;

            SphereEntity parentSphere;
            var searchLight = GetDroneSearchLight(drone, out parentSphere);
            if (searchLight == null)
                return;

            var sphereTransform = parentSphere.transform;
            if (sphereTransform.localPosition == SphereEntityLocalPosition)
                return;

            sphereTransform.localPosition = SphereEntityInitialLocalPosition / scale;
        }

        #endregion

        #region Classes

        private class SearchLightUpdater : MonoBehaviour
        {
            public static void DestroyAll()
            {
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    var drone = entity as Drone;
                    if (drone == null)
                        continue;

                    var searchLightUpdater = drone.GetComponent<SearchLightUpdater>();
                    if (searchLightUpdater == null)
                        continue;

                    Destroy(searchLightUpdater);
                }
            }

            public BasePlayer Controller;

            private Drone _drone;
            private SearchLight _searchLight;

            private void Awake()
            {
                _drone = GetComponent<Drone>();
                _searchLight = GetDroneSearchLight(_drone);
            }

            private void Update()
            {
                if (Controller == null)
                {
                    Destroy(this);
                    return;
                }

                if (!_searchLight.HasFlag(IOEntity.Flag_HasPower))
                    return;

                var mouseVerticalDelta = Controller.serverInput.current.mouseDelta.y;
                if (mouseVerticalDelta == 0)
                    return;

                // Track the performance cost with Oxide so that server owners can be informed.
                _pluginInstance.TrackStart();
                UpdateAim(mouseVerticalDelta);
                _pluginInstance.TrackEnd();
            }

            private void UpdateAim(float mouseVerticalDelta)
            {
                var localX = _searchLight.transform.localRotation.eulerAngles.x;
                var searchLightSettings = _pluginConfig.SearchLight;

                // Temporarily translate the angle by 90 degrees so it can be clamped based on a configured 0-180 range.
                var newLocalX = (localX + 90) % 360;
                newLocalX += mouseVerticalDelta * searchLightSettings.AimSensitivity;
                newLocalX = Clamp(newLocalX, searchLightSettings.MinAngle, searchLightSettings.MaxAngle);
                newLocalX = (newLocalX - 90) % 360;

                _searchLight.transform.localRotation = Quaternion.Euler(newLocalX, SearchLightYAxisRotation, 0);

                _searchLight.InvalidateNetworkCache();
                // This is the most expensive line in terms of performance.
                _searchLight.SendNetworkUpdate_Position();
            }
        }

        #endregion

        #region Configuration

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("SearchLight")]
            public SearchLightSettings SearchLight = new SearchLightSettings();
        }

        private class SearchLightSettings
        {
            [JsonProperty("DefaultAngle")]
            public int DefaultAngle = 75;

            [JsonProperty("MinAngle")]
            public int MinAngle = 60;

            [JsonProperty("MaxAngle")]
            public int MaxAngle = 120;

            [JsonProperty("AimSensitivity")]
            public float AimSensitivity = 0.25f;
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #endregion

        #region Configuration Boilerplate

        private class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _pluginConfig = Config.ReadObject<Configuration>();
                if (_pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_pluginConfig))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_pluginConfig, true);
        }

        #endregion
    }
}
