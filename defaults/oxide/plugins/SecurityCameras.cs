using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Security Cameras", "Yurii", "0.0.9")]
    [Description("CCTV targeting system, cameras follow players, npcs & heli")]
    class SecurityCameras : CovalencePlugin
    {
        private const string ToggleManualControlUI = "SecurityCameras.ToggleManualControlUI";
        private readonly Dictionary<BasePlayer, SecurityCamera> PlayerUIStates = new Dictionary<BasePlayer, SecurityCamera>();
        /// <summary>
        /// Configuration options
        /// </summary>
        class ConfigData
        {
            [JsonProperty(PropertyName = "Detection Radius")]
            public int detection_radius = 30;
        }

        static SecurityCameras instance;
        private ConfigData config = new ConfigData();

        #region Config Handling
        /// <summary>
        /// Load default config file
        /// </summary>
        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }
        /// <summary>
        /// Load the config values to the config class
        /// </summary>
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("Your configuration file is invalid");
                LoadDefaultConfig();
                return;
            }
            SaveConfig();
        }
        /// <summary>
        /// Save the config file
        /// </summary>
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        private const string UsePerm = "securitycameras.use";
        private bool HasPerms(string userID, string perm) { return (string.IsNullOrEmpty(userID) || string.IsNullOrEmpty(perm)) ? false : permission.UserHasPermission(userID, perm); }
        private bool HasPerms(ulong userID, string perm) { return HasPerms(userID.ToString(), perm); }
        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
            permission.RegisterPermission(UsePerm, this);
        }

        void OnServerInitialized()
        {
            instance = this;

            this.Reload();

            Subscribe(nameof(OnEntitySpawned));
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            this.UpdateCameraUse(entity as CCTV_RC);
        }

        void OnGroupPermissionGranted(string name, string perm)
        {
            if (perm == UsePerm)
            {
                this.Reload();
            }
        }

        void OnGroupPermissionRevoked(string name, string perm)
        {
            if (perm == UsePerm)
            {
                this.Reload();
            }
        }

        void OnUserPermissionGranted(string id, string permName)
        {
            if (permName == UsePerm)
            {
                this.Reload();
            }
        }

        void OnUserPermissionRevoked(string id, string permName)
        {
            if (permName == UsePerm)
            {
                this.Reload();
            }
        }

        void OnBookmarkControlEnded(ComputerStation computerStation, BasePlayer player, BaseEntity controlledEntity)
        {
            this.DestroyButton(player);
        }

        void OnBookmarkControlStarted(ComputerStation computerStation, BasePlayer player, string bookmarkName, IRemoteControllable remoteControllable)
        {
            var camera = (remoteControllable as BaseNetworkable).gameObject.GetComponent<SecurityCamera>();
            if (camera == null) return;

            this.CreateButton(player, camera);
        }

        void Reload()
        {
            foreach (BaseNetworkable entity in BaseNetworkable.serverEntities)
            {
                this.UpdateCameraUse(entity as CCTV_RC);
            }
        }

        void UpdateCameraUse(CCTV_RC camera)
        {
            if (camera != null)
            {
                if (HasPerms(camera.OwnerID, UsePerm))
                {
                    if (camera.gameObject.GetComponent<SecurityCamera>() == null)
                    {
                        camera.gameObject.AddComponent<SecurityCamera>();
                    }
                }
                else
                {
                    SecurityCamera component = camera.gameObject.GetComponent<SecurityCamera>();
                    if (component != null)
                    {
                        component.DestroyCamera();
                    }
                }
            }
        }

        void Unload()
        {
            foreach (BaseNetworkable camera in BaseNetworkable.serverEntities)
            {
                if (camera is CCTV_RC)
                {
                    SecurityCamera component = camera.gameObject.GetComponent<SecurityCamera>();
                    if (component != null)
                    {
                        component.DestroyCamera();
                    }
                }
            }

            foreach (var item in PlayerUIStates) 
            {
                this.DestroyButton(item.Key);
            }
            instance = null;
        }

        #region Commands

        [Command("toggle")]
        private void UICommandToggle(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer) return;
            var basePlayer = player.Object as BasePlayer;
            if (PlayerUIStates.ContainsKey(basePlayer))
            {
                var camera = PlayerUIStates[basePlayer];
                camera.scanning = !camera.scanning;
                this.DestroyButton(basePlayer);
                this.CreateButton(basePlayer, camera);
            }
        }
        #endregion

        void CreateButton(BasePlayer player, SecurityCamera camera)
        {
            if (camera == null)
            {
                return;
            }
            var cuiElements = new CuiElementContainer
                {
                    {
                        new CuiButton
                        {
                            Text = {
                                Text = camera.scanning ? "Scanning" : "Manual Control",
                                Color = "0.97 0.92 0.88 1",
                                Align = TextAnchor.MiddleCenter,
                                FadeIn = 0.25f
                            },
                            Button =
                            {
                                Color = camera.scanning ? "0.44 0.54 0.26 1" : "0.7 0.3 0 1",
                                Command = "toggle"
                            },
                            RectTransform =
                            {
                                AnchorMin = "1 0",
                                AnchorMax = "1 0",
                                OffsetMin = "-150 50",
                                OffsetMax = "-50 80"
                            }
                        },
                        "Overlay",
                        ToggleManualControlUI
                    }
                };

            CuiHelper.AddUi(player, cuiElements);
            PlayerUIStates.Add(player, camera);
        }

        void DestroyButton(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, ToggleManualControlUI);
            PlayerUIStates.Remove(player);
        }

        #region SecurityCamera
        internal class SecurityCamera : MonoBehaviour
        {
            private uint id;
            private CCTV_RC camera { get; set; } = null;
            public BaseCombatEntity target = null;
            public bool scanning = true;

            private void Awake()
            {
                camera = GetComponent<CCTV_RC>();
                id = camera.net.ID;

                gameObject.layer = (int)Layer.Reserved1;
                var collider = gameObject.GetComponent<SphereCollider>();
                if (collider != null)
                    Destroy(collider);
                collider = gameObject.AddComponent<SphereCollider>();
                collider.center = Vector3.zero;
                collider.radius = instance.config.detection_radius;
                collider.isTrigger = true;
                collider.enabled = true;
                ResetTarget();
            }
            /// <summary>
            /// Reset the cameras target
            /// </summary>
            public void ResetTarget()
            {
                target = null;
                camera.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                SphereCollider collider = gameObject.GetComponent<SphereCollider>();
                collider.radius = instance.config.detection_radius;
            }
            /// <summary>
            /// New entity in range
            /// </summary>
            /// <param name="range"></param>
            private void OnTriggerEnter(Collider range)
            {
                BaseCombatEntity entity = range.GetComponentInParent<BaseCombatEntity>();
                if (target != null || !IsValid(entity))
                {
                    return;
                }
                if (ShouldTarget(entity))
                {
                    SetTarget(entity);
                }
            }
            /// <summary>
            /// Update entities within range
            /// </summary>
            /// <param name="range"></param>
            private void OnTriggerStay(Collider range)
            {
                BaseCombatEntity entity = range.GetComponentInParent<BaseCombatEntity>();
                if (!IsValid(entity))
                {
                    return;
                }
                if (ShouldTarget(entity))
                {
                    SetTarget(entity);
                }
            }
            /// <summary>
            /// Entity leaving range
            /// </summary>
            /// <param name="range"></param>
            private void OnTriggerExit(Collider range)
            {
                BaseCombatEntity entity = range.GetComponentInParent<BaseCombatEntity>();
                if (!IsValid(entity))
                {
                    return;
                }

                if (IsTargeting(entity))
                {
                    ResetTarget();
                }
            }
            /// <summary>
            /// Check if entity is a valid target
            /// </summary>
            /// <param name="entity"></param>
            /// <returns></returns>
            private bool IsValid(BaseCombatEntity entity)
            {
                if (!entity)
                {
                    return false;
                }

                if (entity is BasePlayer || entity is BaseHelicopter)
                {
                    return true;
                }
                if (entity is NPCPlayer && !camera.isStatic)
                {
                    return true;
                }
                return false;
            }

            /// <summary>
            /// Check if camera has line of sight to entity
            /// </summary>
            /// <param name="entity"></param>
            /// <returns></returns>
            public bool HasLoS(BaseCombatEntity entity)
            {
                if (!IsValid(entity))
                {
                    return false;
                }

                Ray ray = new Ray(camera.pivotOrigin.position, entity.transform.position - camera.transform.position);
                ray.origin += ray.direction / 2;
                float distance = gameObject.GetComponent<SphereCollider>().radius;

                var foundEntity = RaycastAll<BaseNetworkable>(ray, distance);

                if (foundEntity is BaseCombatEntity)
                {
                    if (entity == foundEntity as BaseCombatEntity)
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Check if entity should be targeted
            /// </summary>
            /// <param name="entity"></param>
            /// <returns></returns>
            private bool ShouldTarget(BaseCombatEntity entity)
            {
                try
                {
                    if (!scanning)
                    {
                        return false;
                    }
                    if (!IsValid(entity))
                    {
                        return false;
                    }
                    if (!HasLoS(entity))
                    {
                        return false;
                    }
                    if (HasBuildingPrivilege(entity as BasePlayer))
                    {
                        return false;
                    }
                    if (!camera.IsPowered())
                    {
                        return false;
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            /// <summary>
            /// Set the cameras target
            /// </summary>
            /// <param name="entity"></param>
            private void SetTarget(BaseCombatEntity entity)
            {
                if (entity == null)
                    return;

                target = entity;

                Vector3 vector3 = Vector3Ex.Direction((entity is BasePlayer)
                    ? (entity as BasePlayer).eyes.position
                    : entity.transform.position, camera.yaw.transform.position);
                vector3 = camera.transform.InverseTransformDirection(vector3);
                Quaternion quaternion = Quaternion.LookRotation(vector3);
                Vector3 v3 = BaseMountable.ConvertVector(quaternion.eulerAngles);
                camera.pitchAmount = v3.x;
                camera.yawAmount = v3.y;
                camera.pitchAmount = Mathf.Clamp(camera.pitchAmount, camera.pitchClamp.x, camera.pitchClamp.y);
                camera.yawAmount = Mathf.Clamp(camera.yawAmount, camera.yawClamp.x, camera.yawClamp.y);
                Quaternion quaternion1 = Quaternion.Euler(camera.pitchAmount, 0f, 0f);
                Quaternion quaternion2 = Quaternion.Euler(0f, camera.yawAmount, 0f);
                camera.pitch.transform.localRotation = quaternion1;
                camera.yaw.transform.localRotation = quaternion2;

                camera.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

                SphereCollider collider = gameObject.GetComponent<SphereCollider>();
                collider.radius = instance.config.detection_radius;
            }

            /// <summary>
            /// Destroy collider
            /// </summary>
            public void DestroyCamera()
            {
                Destroy(this);
            }
            /// <summary>
            /// Check if the camera is targeting an entity
            /// </summary>
            /// <param name="entity"></param>
            /// <returns></returns>
            public bool IsTargeting(BaseCombatEntity entity = null)
            {
                if (entity != null && target != null)
                    if (target == entity)
                        return true;
                if (target != null && entity == null)
                    return true;
                return false;
            }
            /// <summary>
            /// Check if the player has building privledge
            /// </summary>
            /// <param name="player"></param>
            /// <returns></returns>
            private bool HasBuildingPrivilege(BasePlayer player)
            {
                BuildingPrivlidge buildingPrivlidge = player.GetBuildingPrivilege(player.WorldSpaceBounds());
                if (buildingPrivlidge && buildingPrivlidge.IsAuthed(player))
                {
                    return true;
                }
                return false;
            }

            /// <summary>
            /// Find first object in line of sight
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="ray"></param>
            /// <param name="distance"></param>
            /// <returns></returns>
            private object RaycastAll<T>(Ray ray, float distance)
            {
                var hits = Physics.RaycastAll(ray, Layers.Solid);
                GamePhysics.Sort(hits);
                object target = false;
                foreach (var hit in hits)
                {
                    var ent = hit.GetEntity();
                    if (ent is T)
                    {
                        target = ent;
                        break;
                    }
                }
                return target;
            }
        }
        #endregion
    }
}