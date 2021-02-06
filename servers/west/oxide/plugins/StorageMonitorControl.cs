using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Storage Monitor Control", "WhiteThunder", "1.0.0")]
    [Description("Allows storage monitors to be deployed to more container types.")]
    internal class StorageMonitorControl : CovalencePlugin
    {
        #region Fields

        private const string PermissionAll = "storagemonitorcontrol.owner.all";
        private const string PermissionEntityFormat = "storagemonitorcontrol.owner.{0}";

        private const string StorageMonitorBoneName = "storagemonitor";

        private Configuration _pluginConfig;

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginConfig.GeneratePermissionNames();

            permission.RegisterPermission(PermissionAll, this);
            foreach (var entry in _pluginConfig.Containers)
                permission.RegisterPermission(entry.Value.PermissionName, this);

            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var container = entity as StorageContainer;
                if (container != null)
                    OnEntitySpawned(container);
            }

            Subscribe(nameof(OnEntitySpawned));
        }

        private void OnEntitySpawned(StorageContainer container)
        {
            if (container == null || NativelySupportsStorageMonitor(container))
                return;

            container.isMonitorable = ShouldEnableMonitoring(container);
        }

        private void OnEntitySpawned(StorageMonitor storageMonitor)
        {
            var parentContainer = storageMonitor.GetParentEntity() as StorageContainer;
            if (parentContainer == null || NativelySupportsStorageMonitor(parentContainer))
                return;

            var containerConfig = GetContainerConfig(parentContainer);
            if (containerConfig == null || !containerConfig.Enabled)
                return;

            storageMonitor.transform.localPosition = containerConfig.Position;
            storageMonitor.transform.localRotation = containerConfig.Rotation;
            storageMonitor.SendNetworkUpdateImmediate();
        }

        #endregion

        #region Helper Methods

        private static T GetChildEntity<T>(BaseEntity entity) where T : BaseEntity
        {
            foreach (BaseEntity child in entity.children)
            {
                var childOfType = child as T;
                if (childOfType != null)
                    return childOfType;
            }
            return null;
        }

        private static bool NativelySupportsStorageMonitor(BaseEntity entity) =>
            entity.model != null
            && entity.FindBone(StorageMonitorBoneName) != entity.model.rootBone;

        private bool ShouldEnableMonitoring(StorageContainer container)
        {
            var containerConfig = GetContainerConfig(container);
            if (containerConfig == null || !containerConfig.Enabled)
                return false;

            if (!ContainerOwnerHasPermission(container, containerConfig))
                return false;

            return true;
        }

        private bool ContainerOwnerHasPermission(BaseEntity entity, ContainerConfig containerConfig)
        {
            if (!containerConfig.RequirePermission)
                return true;

            if (entity.OwnerID == 0)
                return false;

            var ownerIdString = entity.OwnerID.ToString();

            return permission.UserHasPermission(ownerIdString, PermissionAll) ||
                permission.UserHasPermission(ownerIdString, containerConfig.PermissionName);
        }

        #endregion

        #region Configuration

        private ContainerConfig GetContainerConfig(StorageContainer container)
        {
            ContainerConfig containerConfig;
            return _pluginConfig.Containers.TryGetValue(container.ShortPrefabName, out containerConfig)
                ? containerConfig
                : null;
        }

        private class Configuration : SerializableConfiguration
        {
            public void GeneratePermissionNames()
            {
                foreach (var entry in Containers)
                {
                    // Make the permission name less redundant
                    entry.Value.PermissionName = string.Format(PermissionEntityFormat, entry.Key)
                        .Replace(".deployed", string.Empty)
                        .Replace("_deployed", string.Empty)
                        .Replace(".entity", string.Empty);
                }
            }

            [JsonProperty("Containers")]
            public Dictionary<string, ContainerConfig> Containers = new Dictionary<string, ContainerConfig>()
            {
                ["composter"] = new ContainerConfig { Position = new Vector3(0, 1.54f, 0.4f) },
                ["dropbox.deployed"] = new ContainerConfig { Position = new Vector3(0.3f, 0.545f, -0.155f), RotationAngle = 184 },
                ["furnace"] = new ContainerConfig { Position = new Vector3(0, 1.53f, 0.05f) },
                ["furnace.large"] = new ContainerConfig { Position = new Vector3(0.31f, 0.748f, -1.9f), RotationAngle = 190 },
                ["hitchtrough.deployed"] = new ContainerConfig { Position = new Vector3(-0.82f, 0.65f, 0.215f) },
                ["locker.deployed"] = new ContainerConfig { Position = new Vector3(-0.67f, 2.238f, 0.04f), RotationAngle = 10 },
                ["mailbox.deployed"] = new ContainerConfig { Position = new Vector3(0f, 1.327f, 0.21f) },
                ["planter.small.deployed"] = new ContainerConfig { Position = new Vector3(-1.22f, 0.482f, 0.3f) },
                ["planter.large.deployed"] = new ContainerConfig { Position = new Vector3(-1.22f, 0.482f, 1.22f) },
                ["refinery_small_deployed"] = new ContainerConfig { Position = new Vector3(0, 2.477f, 0), RotationAngle = 180 },
                ["survivalfishtrap.deployed"] = new ContainerConfig { Position = new Vector3(0, 0.4f, -0.6f) },
                ["woodbox_deployed"] = new ContainerConfig { Position = new Vector3(-0.24f, 0.55f, 0.14f), RotationAngle = 10 },
            };
        }

        internal class ContainerConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled = false;

            [JsonProperty("RequirePermission")]
            public bool RequirePermission = false;

            [JsonProperty("Position")]
            public Vector3 Position;

            [JsonProperty("RotationAngle", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public float RotationAngle;

            private bool _rotationCached = false;
            private Quaternion _rotation;

            [JsonIgnore]
            public Quaternion Rotation
            {
                get
                {
                    if (_rotationCached)
                        return _rotation;

                    _rotation = RotationAngle == 0
                        ? Quaternion.identity
                        : Quaternion.Euler(0, RotationAngle, 0);
                    _rotationCached = true;

                    return _rotation;
                }
            }

            [JsonIgnore]
            public string PermissionName;
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
