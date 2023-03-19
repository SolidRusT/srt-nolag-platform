﻿/// <summary>
/// Author: S0N_0F_BISCUIT
/// Permissions:
///		securitylights.use - Allows players to use commands
///	Chat Commands:
///		/sl - Help information
///		/sl add - Converts the search light you are looking at to a security light
///		/sl remove - Converts the security light you are looking at to back to a search light
///		/sl mode <mode> - Sets the mode of the security light you are looking at
///		/sl globalmode <mode> - Sets the mode of all security lights you own
///		/sl info - Gives the owner the ability to check the status of a search light
///		/sl reloadconfig - Reloads the config file
///		<mode>
///			all - Targets players and helicopter
///			players - Targets players only
///			heli - Targets heli only
///		</mode>
/// </summary>
using System;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Rust;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Security Lights", "S0N_0F_BISCUIT", "1.1.9")]
    [Description("Search light targeting system")]
    class SecurityLights : RustPlugin
    {
        #region Variables
        /// <summary>
        /// References to other plugins
        /// </summary>
        [PluginReference]
        Plugin Clans, Friends, Vanish;
        /// <summary>
        /// Targeting mode for security lights
        /// </summary>
        public enum TargetMode { all, players, heli, lightshow };
        /// <summary>
        /// Configuration options
        /// </summary>
        class ConfigData
        {
            [JsonProperty("Detection Radius - All")]
            public int allDetectionRadius = 30;

            [JsonProperty("Tracking Radius - All")]
            public int allTrackingRadius = 30;

            [JsonProperty("Detection Radius - Player")]
            public int playerDetectionRadius = 30;

            [JsonProperty("Tracking Radius - Player")]
            public int playerTrackingRadius = 30;
            
            [JsonProperty("Detection Radius - Helicopter")]
            public int heliDetectionRadius = 100;
            
            [JsonProperty("Tracking Radius - Helicopter")]
            public int heliTrackingRadius = 100;
            
            [JsonProperty("Heli Mode - Target Minicopter and Scrap Heli")]
            public bool heliTargetVehicles = false;
            
            [JsonProperty("Auto Convert Lights When Placed")]
            public bool autoConvert = false;
            
            [JsonProperty("Require Power")]
            public bool requirePower = true;
            
            [JsonProperty("Night Only Operation")]
            public bool nightOnly = false;
            
            [JsonProperty("Target Acquired Sound")]
            public bool acquisitionSound = true;
            
            [JsonProperty("Target Friends")]
            public bool targetFriends = true;
            
            [JsonProperty("Target Team Members")]
            public bool targetTeamMembers = true;
        }
        /// <summary>
        /// Data saved by the plugin
        /// </summary>
        class StoredData
        {
            public Dictionary<uint, TargetMode> Security_Lights { get; set; } = new Dictionary<uint, TargetMode>();
        }
        /// <summary>
        /// Permissions used by the plugin
        /// </summary>
        static class Permissions
        {
            static readonly public string use = "securitylights.use";
        }
        /// <summary>
        /// Main behaviour of security lights
        /// </summary>
        class SecurityLight : MonoBehaviour
        {
            #region Variables
            private uint id;
            private SearchLight light { get; set; } = null;
            private TargetMode mode { get; set; } = TargetMode.all;
            private bool powered = false;
            public BaseCombatEntity target = null;
            #endregion

            #region Initialization
            /// <summary>
            /// Initialize security light
            /// </summary>
            private void Awake()
            {
                light = GetComponent<SearchLight>();
                id = light.net.ID;
                if (!instance.data.Security_Lights.ContainsKey(id))
                    instance.data.Security_Lights.Add(id, mode);
                mode = instance.data.Security_Lights[id];
                instance.securityLights.Add(this);
                instance.SaveData();

                gameObject.layer = (int)Layer.Reserved1;
                var collider = gameObject.GetComponent<SphereCollider>();
                if (collider != null)
                    Destroy(collider);
                collider = gameObject.AddComponent<SphereCollider>();
                collider.center = Vector3.zero;
                collider.radius = instance.config.allDetectionRadius;
                collider.isTrigger = true;
                collider.enabled = true;

                ResetTarget();
            }
            #endregion

            #region Functionality
            /// <summary>
            /// New entity in range
            /// </summary>
            /// <param name="range"></param>
            private void OnTriggerEnter(Collider range)
            {
                BaseCombatEntity entity = range.GetComponentInParent<BaseCombatEntity>();

                // Check if entity is valid
                if (!IsValid(entity))
                    return;
                // Check for current target
                if (target != null)
                    return;
                // Acquire new target
                if (ShouldTarget(entity))
                    SetTarget(entity);
            }
            /// <summary>
            /// Update entities within range
            /// </summary>
            /// <param name="range"></param>
            private void OnTriggerStay(Collider range)
            {
                BaseCombatEntity entity = range.GetComponentInParent<BaseCombatEntity>();

                // Check if entity is valid
                if (!IsValid(entity))
                    return;
                // Check for current target
                if (target != null)
                    return;
                // Acquire new target
                if (ShouldTarget(entity))
                    SetTarget(entity);
            }
            /// <summary>
            /// Entity leaving range
            /// </summary>
            /// <param name="range"></param>
            private void OnTriggerExit(Collider range)
            {
                BaseCombatEntity entity = range.GetComponentInParent<BaseCombatEntity>();

                // Check if entity is valid
                if (!IsValid(entity))
                    return;
                // Reset the target if target leaves range
                if (IsTargeting(entity))
                    ResetTarget();
            }
            /// <summary>
            /// Update the target if in lightshow mode also make sure current target is valid
            /// </summary>
            private void Update()
            {
                if (mode == TargetMode.lightshow)
                {
                    BaseCombatEntity owner = instance.GetPlayer(OwnerID()) as BaseCombatEntity;
                    if (!ShouldTarget(owner))
                        return;
                    if (!IsTargeting(owner))
                        SetTarget(owner);
                    else
                        UpdateTarget();
                }
                else if (target != null)
                {
                    if (ShouldTarget(target))
                        UpdateTarget();
                    else
                        ResetTarget();
                }

                if (light.currentEnergy >= light.DesiredPower())
                    powered = true;
                else
                    powered = false;
            }
            /// <summary>
            /// Destroy the the security light
            /// </summary>
            public void OnDestroy()
            {
                if (!instance.unloading)
                {
                    if (instance.data.Security_Lights.ContainsKey(id))
                        instance.data.Security_Lights.Remove(id);
                    if (instance.securityLights.Contains(this))
                        instance.securityLights.Remove(this);
                }

                instance.SaveData();

                Destroy(this);
            }
            #endregion

            #region Targeting
            /// <summary>
            /// Check if entity should be targeted
            /// </summary>
            /// <param name="entity"></param>
            /// <returns></returns>
            private bool ShouldTarget(BaseCombatEntity entity)
            {
                try
                {
                    // Check if light is mounted
                    if (light.IsMounted())
                        return false;
                    // If power is required, check if light is powered
                    if (instance.config.requirePower && !powered)
                        return false;
                    // Check if target is valid
                    if (!IsValid(entity))
                        return false;
                    // Check if in lightshow mode
                    if (mode == TargetMode.lightshow)
                        return true;
                    // Check if auto-lights are enabled
                    if (!instance.lightsEnabled)
                        return false;
                    if (mode != TargetMode.heli)
                    {
                        // Check if light has line of sight
                        if (!HasLoS(entity))
                            return false;
                        // Check if owner already targeting entity
                        if (!IsTargeting(entity))
                        {
                            if (instance.IsOwnerTargeting(OwnerID(), entity))
                                return false;
                            // Check if light is the closest valid light
                            else if (!instance.IsClosest(entity, id))
                                return false;
                        }
                        // Check if entity is a BasePlayer and not an NPCPlayer
                        if (entity.GetType() == typeof(BasePlayer) && !(entity is NPCPlayer))
                            return ShouldTargetPlayer(entity as BasePlayer);
                        // Check driver of vehicle
                        if (entity is BaseVehicle)
                            return ShouldTargetPlayer((entity as BaseVehicle).GetDriver());
                    }
                    else if (entity is BaseHelicopterVehicle)
                    {
                        if (instance.config.heliTargetVehicles)
                            return ShouldTargetPlayer((entity as BaseHelicopterVehicle).GetDriver());

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
            /// Check if player should be targeted
            /// </summary>
            /// <param name="player"></param>
            /// <returns></returns>
            private bool ShouldTargetPlayer(BasePlayer player)
            {
                if (player == null)
                    return false;
                try
                {
                    // Check if player is authorized on the light
                    if (instance.IsAuthorized(player, light))
                        return false;
                    // Make sure player is not NPC
                    if (instance.GetPlayer(player.userID) == null)
                        return false;
                    // Check if player has building privlege
                    if (HasBuildingPrivilege(player))
                        return false;
                    // Check if player is crouched
                    if (player.IsDucked() && player != light.lastAttacker)
                        return false;
                    // Check if player is invisible
                    if (instance.IsInvisible(player))
                        return false;
                    // Check if player is a friend
                    if (!instance.config.targetFriends)
                        if (instance.IsFriend(OwnerID(), player.userID))
                            return false;
                    // Check if player is a team member
                    if (!instance.config.targetTeamMembers)
                        if (instance.GetPlayer(OwnerID()).currentTeam == player.currentTeam && player.currentTeam != 0)
                            return false;

                    return true;
                }
                catch { return false; }
            }
            /// <summary>
            /// Set the lights target
            /// </summary>
            /// <param name="entity"></param>
            private void SetTarget(BaseCombatEntity entity)
            {
                if (entity == null)
                    return;
                target = entity;

                if (entity is BasePlayer)
                    light.SetTargetAimpoint(entity.transform.position + Vector3.up);
                else
                    light.SetTargetAimpoint(entity.transform.position);

                if (instance.config.acquisitionSound)
                    Effect.server.Run("assets/prefabs/npc/autoturret/effects/targetacquired.prefab", light.eyePoint.transform.position);

                if (!instance.config.requirePower)
                {
                    light.UpdateHasPower(light.DesiredPower(), 0);
                    light.SetFlag(BaseEntity.Flags.Reserved8, true);
                }
                else if (powered)
                {
                    light.SetFlag(BaseEntity.Flags.Reserved8, true);
                }

                light.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

                SphereCollider collider = gameObject.GetComponent<SphereCollider>();
                collider.radius = GetTrackingRadius();
            }
            /// <summary>
            /// Update the lights target
            /// </summary>
            private void UpdateTarget()
            {
                if (target is BasePlayer || target is NPCPlayer)
                    light.SetTargetAimpoint(target.transform.position + Vector3.up);
                else
                    light.SetTargetAimpoint(target.transform.position);

                if (instance.config.requirePower && !powered)
                    ResetTarget();

                light.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }
            /// <summary>
            /// Reset the lights target
            /// </summary>
            public void ResetTarget()
            {
                target = null;
                light.SetTargetAimpoint(light.eyePoint.transform.position + Vector3.down * 3);
                light.SetFlag(BaseEntity.Flags.Reserved8, false);
                light.SetFlag(BaseEntity.Flags.On, false);
                light.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                SphereCollider collider = gameObject.GetComponent<SphereCollider>();
                collider.radius = GetDetectionRadius();
            }
            #endregion

            #region Helpers
            /// <summary>
            /// Check if the light is targeting an entity
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
            /// Check if entity is a valid target
            /// </summary>
            /// <param name="entity"></param>
            /// <returns></returns>
            private bool IsValid(BaseCombatEntity entity)
            {
                if (!entity)
                    return false;

                switch (mode)
                {
                    case TargetMode.all:
                        return entity is BasePlayer ||
                               entity is BaseHelicopter ||
                               entity is NPCPlayer ||
                               entity is BaseVehicle;
                    case TargetMode.players:
                        return (entity is BasePlayer && !(entity is NPCPlayer)) ||
                               entity is BaseVehicle;
                    case TargetMode.heli:
                        return entity is BaseHelicopter ||
                               entity is CH47Helicopter ||
                               entity is BaseHelicopterVehicle;
                    case TargetMode.lightshow:
                        return entity == instance.GetPlayer(OwnerID());
                    default:
                        return false;
                }
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
                var hits = Physics.RaycastAll(ray, distance, Layers.Solid, QueryTriggerInteraction.Ignore);
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
            /// <summary>
            /// Check if light has line of sight to entity
            /// </summary>
            /// <param name="entity"></param>
            /// <returns></returns>
            public bool HasLoS(BaseCombatEntity entity)
            {
                if (!IsValid(entity))
                    return false;
                if (entity is BaseHelicopter)
                    return true;

                Ray ray = new Ray(light.eyePoint.transform.position, entity.transform.position - light.transform.position);
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
            /// Destroy collider
            /// </summary>
            public void DestroyLight()
            {
                ResetTarget();
                Destroy(this);
            }
            /// <summary>
            /// Get the detection radius for the current mode
            /// </summary>
            /// <returns></returns>
            private float GetDetectionRadius()
            {
                if (mode == TargetMode.all)
                    return instance.config.allDetectionRadius;
                if (mode == TargetMode.players)
                    return instance.config.playerDetectionRadius;
                if (mode == TargetMode.heli)
                    return instance.config.heliDetectionRadius;
                return 0;
            }
            /// <summary>
            /// Get the tracking radius for the current mode
            /// </summary>
            /// <returns></returns>
            private float GetTrackingRadius()
            {
                if (mode == TargetMode.all)
                    return instance.config.allTrackingRadius;
                if (mode == TargetMode.players)
                    return instance.config.playerTrackingRadius;
                if (mode == TargetMode.heli)
                    return instance.config.heliTrackingRadius;
                return 0;
            }
            /// <summary>
            /// Return the light's owner ID
            /// </summary>
            /// <returns></returns>
            public ulong OwnerID()
            {
                return light.OwnerID;
            }
            /// <summary>
            /// Return the light's ID
            /// </summary>
            /// <returns></returns>
            public uint ID()
            {
                return id;
            }
            /// <summary>
            /// Return the position of the light
            /// </summary>
            /// <returns></returns>
            public Vector3 Position()
            {
                return light.eyePoint.transform.position;
            }
            /// <summary>
            /// Change the operation mode
            /// </summary>
            /// <param name="newMode"></param>
            public void ChangeMode(TargetMode newMode)
            {
                if ((mode == TargetMode.lightshow && newMode != TargetMode.lightshow) || newMode == TargetMode.heli || newMode == TargetMode.lightshow)
                    ResetTarget();
                mode = newMode;
                instance.data.Security_Lights[id] = mode;
                instance.SaveData();

                UpdateRadius();
            }
            /// <summary>
            /// Return the operation mode
            /// </summary>
            /// <returns></returns>
            public TargetMode Mode()
            {
                return mode;
            }
            /// <summary>
            /// Update the detection/targeting radii
            /// </summary>
            public void UpdateRadius()
            {
                SphereCollider collider = gameObject.GetComponent<SphereCollider>();
                if (IsTargeting())
                    collider.radius = GetTrackingRadius();
                else
                    collider.radius = GetDetectionRadius();
            }
            /// <summary>
            /// Check if the player has building privledge
            /// </summary>
            /// <param name="player"></param>
            /// <returns></returns>
            private bool HasBuildingPrivilege(BasePlayer player)
            {
                BuildingPrivlidge buildingPrivlidge = player.GetBuildingPrivilege(player.WorldSpaceBounds());
                if (buildingPrivlidge)
                    if (buildingPrivlidge.IsAuthed(player))
                        return true;
                return false;
            }
            #endregion
        }

        static SecurityLights instance;
        private ConfigData config = new ConfigData();
        private StoredData data;
        private List<SecurityLight> securityLights = new List<SecurityLight>();
        private bool lightsEnabled = true;
        private bool unloading = false;
        #endregion

        #region Localization
        /// <summary>
        /// Load messages relayed to player
        /// </summary>
        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["InvalidTarget"] = "Invalid Target!",
                ["NoPermission"] = "You do not have permission to use this {0} light!",
                ["Convert"] = "Converted to a security light.",
                ["AlreadySL"] = "This is already a security light.",
                ["Revert"] = "Converted to search light.",
                ["NotSL"] = "This is not a security light.",
                ["AllMode"] = "Targeting players and helicopters.",
                ["PlayersMode"] = "Targeting only players.",
                ["HeliMode"] = "Targeting only helicopters.",
                ["LightshowMode"] = "Targeting owner for a lightshow!",
                ["ModeUsage"] = "Usage: /sl mode <all|players|heli|lightshow>",
                ["GlobalModeUsage"] = "Usage: /sl globalmode <all|players|heli|lightshow>",
                ["GlobalChange"] = "Changed {0} light(s) to {1} mode.",
                ["Unknown"] = "Unknown",
                ["SecurityLight"] = "Security Light",
                ["SearchLight"] = "Search Light",
                ["NoCommandPermission"] = "You do not have permission to use this command!",
                ["False"] = "False",
                ["True"] = "True",
                ["SecurityInfo"] = "Owner: {0}\nState: {1}\nMode: {2}\nTargeting: {3}",
                ["SearchInfo"] = "Owner: {0}\nState: {1}",
                ["DataReload"] = "Reloaded plugin data.",
                ["ConfigReload"] = "Reloaded plugin config.",
                ["ConfigInfo"] = "Configuration Info: \nRadius: (Detection,Tracking)\nRadius - All: ({0},{1})\nRadius - Players: ({2},{3})\nRadius - Helicopters: ({4},{5})\nAuto-Convert: {6}\nRequire Power: {7}\nNight Only Operation: {8}\nTarget Acquired Sound: {9}",
                ["AdminUsage"] = "Usage: /sl <add|remove|mode|globalmode|info|reloaddata|reloadconfig>",
                ["Usage"] = "Usage: /sl <add|remove|mode|globalmode|info>",
                ["Search"] = "search",
                ["Security"] = "security"
            }, this);
            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["InvalidTarget"] = "Ungueltiges VerfolgungsZiel ...",
                ["NoPermission"] = "Du hast keine Erlaubnis zum benutzen dieses {0} Scheinwerfers ...",
                ["Convert"] = "... geaendert zu SicherheitsScheinwerfer",
                ["AlreadySL"] = "Dies ist schon ein SicherheitsScheinwerfer ...",
                ["Revert"] = "... geaendert zu normalen SuchScheinwerfer",
                ["NotSL"] = "Dies ist kein SicherheitsScheinwerfer ...",
                ["AllMode"] = "Ziele sind jetzt alle normalen Spieler & Helikopter ...",
                ["PlayersMode"] = "Ziel sind jetzt nur normale Spieler ...",
                ["HeliMode"] = "Ziel sind jetzt nur Helikopter ...",
                ["LightshowMode"] = "Ziel ist jetzt nur der ServerOwner ...",
                ["ModeUsage"] = "benutze: /sl mode <all | players | heli | lightshow>",
                ["GlobalModeUsage"] = "benutze: /sl globalmode <all | players | heli | lightshow>",
                ["GlobalChange"] = "Geaendert {0} Scheinwerfer in {1}Modus ...",
                ["Unknown"] = "Unbekannt !",
                ["SecurityLight"] = "SicherheitsScheinwerfer",
                ["SearchLight"] = "SuchScheinwerfer",
                ["NoCommandPermission"] = "Du hast keine Erlaubnis zum benutzen des Befehls ...",
                ["False"] = "False",
                ["True"] = "True",
                ["SecurityInfo"] = "Owner: {0}\nState: {1}\nMode: {2}\nTargeting: {3}",
                ["SearchInfo"] = "Owner: {0}\nState: {1}",
                ["DataReload"] = "... neuladen der DATA-Datei",
                ["ConfigReload"] = "... neuladen der CONFIG-Datei",
                ["ConfigInfo"] = "Configuration Info: \nRadius: (Detection,Tracking)\nRadius - All: ({0},{1})\nRadius - Players: ({2},{3})\nRadius - Helicopters: ({4},{5})\nAuto-Convert: {6}\nRequire Power: {7}\nNight Only Operation: {8}\nTarget Acquired Sound: {9}",
                ["AdminUsage"] = "benutze: /sl <add | remove | mode | globalmode | info | reloaddata | reloadconfig>",
                ["Usage"] = "benutze: /sl <add | remove | mode | globalmode | info>",
                ["Search"] = "search",
                ["Security"] = "security"
            }, this, "de");
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Plugin initialization
        /// </summary>
        private void Init()
        {
            // Permissions
            permission.RegisterPermission(Permissions.use, this);
            // Data
            LoadData();
        }
        /// <summary>
        /// Restore plugin data when server finishes startup
        /// </summary>
        void OnServerInitialized()
        {
            // Set instance
            instance = this;
            // Restore data
            FindSecurityLights();
            // Get time of day
            if (config.nightOnly && TOD_Sky.Instance.IsDay)
                lightsEnabled = false;
        }
        /// <summary>
        /// Unloading Plugin
        /// </summary>
        void Unload()
        {
            unloading = true;
            foreach (SecurityLight light in securityLights)
                light.DestroyLight();
            SaveData();
        }
        #endregion

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

        #region Data Handling
        /// <summary>
        /// Load plugin data
        /// </summary>
        private void LoadData()
        {
            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("SecurityLights");
            }
            catch
            {
                data = new StoredData();
                SaveData();
            }
        }
        /// <summary>
        /// Save PlayerData
        /// </summary>
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("SecurityLights", data);
        }
        /// <summary>
        /// Find all security lights
        /// </summary>
        private void FindSecurityLights()
        {
            List<uint> delete = new List<uint>();
            foreach (uint id in data.Security_Lights.Keys)
            {
                BaseNetworkable networkable = BaseNetworkable.serverEntities.Find(id);

                if (networkable is SearchLight)
                {
                    SecurityLight sl = networkable.gameObject.AddComponent<SecurityLight>();
                }
                else
                    delete.Add(id);
            }
            Puts($"Implemented {securityLights.Count} saved security lights.");
            foreach (uint id in delete)
            {
                data.Security_Lights.Remove(id);
            }
        }
        /// <summary>
        /// Clear PlayerData
        /// </summary>
        private void ClearData()
        {
            data = new StoredData();
            SaveData();
        }
        #endregion

        #region Chat Commands
        /// <summary>
        /// Handle commands for plugin
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [ChatCommand("sl")]
        void ManageSecurityLight(BasePlayer player, string command, string[] args)
        {
            // Check if player has permission to use security lights
            if (!permission.UserHasPermission(player.UserIDString, Permissions.use) && !IsDeveloper(player))
            {
                PrintToChat(player, Lang("NoCommandPermission", player.UserIDString));
                return;
            }
            // Get entity player is looking at
            var target = RaycastAll<BaseEntity>(player.eyes.HeadRay());
            SearchLight light = null;
            if (target is SearchLight)
                light = target as SearchLight;

            if (args.Length == 0)
                args = new string[] { String.Empty };
            switch (args[0].ToLower())
            {
                case "add":
                    if (!(target is SearchLight))
                    {
                        PrintToChat(player, Lang("InvalidTarget", player.UserIDString));
                        return;
                    }
                    if (light.gameObject.GetComponent<SecurityLight>() == null)
                    {
                        if (!IsAuthorized(player, light) && !IsDeveloper(player))
                        {
                            PrintToChat(player, Lang("NoPermission", player.UserIDString, Lang("Search", player.UserIDString)));
                            return;
                        }
                        light.gameObject.AddComponent<SecurityLight>();
                        PrintToChat(player, Lang("Convert", player.UserIDString));
                    }
                    else
                        PrintToChat(player, Lang("AlreadySL", player.UserIDString));
                    return;
                case "remove":
                    if (!(target is SearchLight))
                    {
                        PrintToChat(player, Lang("InvalidTarget", player.UserIDString));
                        return;
                    }
                    SecurityLight removeLight;
                    if ((removeLight = light.gameObject.GetComponent<SecurityLight>()) != null)
                    {
                        if (!IsAuthorized(player, light) && !IsDeveloper(player))
                        {
                            PrintToChat(player, Lang("NoPermission", player.UserIDString, Lang("Security", player.UserIDString)));
                            return;
                        }
                        removeLight.OnDestroy();
                        PrintToChat(player, Lang("Revert", player.UserIDString));
                    }
                    else
                        PrintToChat(player, Lang("NotSL", player.UserIDString));
                    return;
                case "mode":
                    if (!(target is SearchLight))
                    {
                        PrintToChat(player, Lang("InvalidTarget", player.UserIDString));
                        return;
                    }
                    SecurityLight modeLight;
                    if ((modeLight = light.gameObject.GetComponent<SecurityLight>()) != null)
                    {
                        if (!IsAuthorized(player, light) && !IsDeveloper(player))
                        {
                            PrintToChat(player, Lang("NoPermission", player.UserIDString, Lang("Security", player.UserIDString)));
                            return;
                        }
                        string option = String.Empty;
                        if (args.Length == 2)
                            option = args[1].ToLower();
                        switch (option)
                        {
                            case "all":
                                modeLight.ChangeMode(TargetMode.all);
                                PrintToChat(player, Lang("AllMode", player.UserIDString));
                                break;
                            case "players":
                                modeLight.ChangeMode(TargetMode.players);
                                PrintToChat(player, Lang("PlayersMode", player.UserIDString));
                                break;
                            case "heli":
                                modeLight.ChangeMode(TargetMode.heli);
                                PrintToChat(player, Lang("HeliMode", player.UserIDString));
                                break;
                            case "lightshow":
                                modeLight.ChangeMode(TargetMode.lightshow);
                                PrintToChat(player, Lang("LightshowMode", player.UserIDString));
                                break;
                            default:
                                PrintToChat(player, Lang("ModeUsage", player.UserIDString));
                                return;
                        }
                    }
                    else
                        PrintToChat(player, Lang("NotSL", player.UserIDString));
                    return;
                case "globalmode":
                    TargetMode globalmode;
                    int lightsChanged = 0;
                    string option2 = String.Empty;
                    if (args.Length == 2)
                        option2 = args[1].ToLower();
                    switch (option2)
                    {
                        case "all":
                            globalmode = TargetMode.all;
                            break;
                        case "players":
                            globalmode = TargetMode.players;
                            break;
                        case "heli":
                            globalmode = TargetMode.heli;
                            break;
                        case "lightshow":
                            globalmode = TargetMode.lightshow;
                            break;
                        default:
                            PrintToChat(player, Lang("GlobalModeUsage", player.UserIDString));
                            return;
                    }
                    foreach (SecurityLight currentLight in securityLights)
                    {
                        if (currentLight.OwnerID() == player.userID)
                        {
                            currentLight.ChangeMode(globalmode);
                            lightsChanged++;
                        }
                    }
                    PrintToChat(player, Lang("GlobalChange", player.UserIDString, lightsChanged, globalmode));
                    return;
                case "info":
                    if (!(target is SearchLight))
                    {
                        PrintToChat(player, Lang("InvalidTarget", player.UserIDString));
                        return;
                    }
                    if (!IsAuthorized(player, light) && !player.IsAdmin && !IsDeveloper(player))
                    {
                        PrintToChat(player, Lang("NoCommandPermission", player.UserIDString));
                        return;
                    }
                    string ownerString = Lang("Unknown");
                    if (GetPlayer(light.OwnerID) != null)
                        ownerString = GetPlayer(light.OwnerID).displayName;
                    SecurityLight infoLight;
                    if ((infoLight = light.gameObject.GetComponent<SecurityLight>()) != null)
                    {
                        string targeting = infoLight.IsTargeting() ? Lang("True", player.UserIDString) : Lang("False", player.UserIDString);
                        PrintToChat(player, Lang("SecurityInfo", player.UserIDString, ownerString, Lang("SecurityLight", player.UserIDString), infoLight.Mode().ToString(), targeting));
                    }
                    else
                        PrintToChat(player, Lang("SearchInfo", player.UserIDString, ownerString, Lang("SearchLight", player.UserIDString)));
                    if (IsDeveloper(player))
                        PrintToChat(player, $"Total Input Power: {light.currentEnergy}");
                    return;
                case "reloadconfig":
                    if (!player.IsAdmin)
                    {
                        PrintToChat(player, Lang("NoCommandPermission", player.UserIDString));
                        return;
                    }
                    LoadConfig();
                    PrintToChat(player, Lang("ConfigReload", player.UserIDString));
                    PrintToChat(player, Lang("ConfigInfo", player.UserIDString,
                        config.allDetectionRadius, config.allTrackingRadius,
                        config.playerDetectionRadius, config.playerTrackingRadius,
                        config.heliDetectionRadius, config.heliTrackingRadius,
                        config.autoConvert,
                        config.requirePower,
                        config.nightOnly,
                        config.acquisitionSound));
                    UpdateLights();
                    return;
                default:
                    if (player.IsAdmin)
                        PrintToChat(player, Lang("AdminUsage", player.UserIDString));
                    else
                        PrintToChat(player, Lang("Usage", player.UserIDString));
                    return;
            }
        }
        #endregion

        #region Functionality
        /// <summary>
        /// Enable lights at sunset
        /// </summary>
        void OnTimeSunset()
        {
            if (config.nightOnly)
                lightsEnabled = true;
        }
        /// <summary>
        /// Disable lights at sunrise
        /// </summary>
        void OnTimeSunrise()
        {
            if (config.nightOnly)
                lightsEnabled = false;
        }
        /// <summary>
        /// Check if a search light is placed
        /// </summary>
        /// <param name="entity"></param>
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is SearchLight && config.autoConvert)
            {
                if (!permission.UserHasPermission((entity as SearchLight).OwnerID.ToString(), Permissions.use))
                    return;

                (entity as SearchLight).gameObject.AddComponent<SecurityLight>();
            }
        }
        /// <summary>
        /// Check if the entity that died is currently being targeted
        /// </summary>
        /// <param name="entity"></param>
        void OnEntityKill(BaseNetworkable entity)
        {
            if (!(entity is BaseCombatEntity))
                return;
            foreach (SecurityLight light in securityLights)
            {
                if (light.IsTargeting(entity as BaseCombatEntity))
                {
                    if (entity is BaseHelicopter)
                        light.ResetTarget();
                    else if (entity is BasePlayer)
                        if (!(entity as BasePlayer).IsAlive())
                            light.ResetTarget();
                }
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Get string and format from lang file
        /// </summary>
        /// <param name="key"></param>
        /// <param name="userId"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private string Lang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);
        /// <summary>
        /// Get player name from ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        protected BasePlayer GetPlayer(ulong id)
        {
            if (string.IsNullOrEmpty(id.ToString()))
                return null;

            IPlayer player = covalence.Players.FindPlayer(id.ToString());

            if (player.Object != null)
            {
                return (BasePlayer)player.Object;
            }
            else
            {
                foreach (BasePlayer current in BasePlayer.activePlayerList)
                {
                    if (current.userID == id)
                        return current;
                }

                foreach (BasePlayer current in BasePlayer.sleepingPlayerList)
                {
                    if (current.userID == id)
                        return current;
                }
            }
            return null;
        }
        /// <summary>
        /// Update all security lights
        /// </summary>
        void UpdateLights()
        {
            foreach (SecurityLight sl in securityLights)
                sl.UpdateRadius();
        }
        /// <summary>
        /// Check if search light from owner is already targeting entity
        /// </summary>
        /// <param name="OwnerID"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private bool IsOwnerTargeting(ulong OwnerID, BaseCombatEntity target)
        {
            foreach (SecurityLight sl in securityLights)
            {
                if (sl.OwnerID() == OwnerID && sl.IsTargeting(target))
                    return true;
            }
            return false;
        }
        /// <summary>
        /// Check if player is authorized
        /// </summary>
        /// <param name="player"></param>
        /// <param name="light"></param>
        /// <returns></returns>
        public bool IsAuthorized(BasePlayer player, SearchLight light)
        {
            if (light.OwnerID == 0)
                return false;
            if (light.OwnerID == player.userID)
                return true;
            else if (Clans)
            {
                string ownerClan = (string)(Clans.CallHook("GetClanOf", light.OwnerID));
                string playerClan = (string)(Clans.CallHook("GetClanOf", player));

                if (ownerClan == playerClan && !String.IsNullOrEmpty(ownerClan))
                    return true;
            }
            return false;
        }
        /// <summary>
        /// Find the entity the player is looking at
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ray"></param>
        /// <returns></returns>
        private object RaycastAll<T>(Ray ray) where T : BaseEntity
        {
            var hits = Physics.RaycastAll(ray);
            GamePhysics.Sort(hits);
            var distance = 100f;
            object target = false;
            foreach (var hit in hits)
            {
                var ent = hit.GetEntity();
                if (ent is T && hit.distance < distance)
                {
                    target = ent;
                    break;
                }
            }
            return target;
        }
        /// <summary>
        /// Check if light is the closest valid light
        /// </summary>
        /// <param name="target"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private bool IsClosest(BaseCombatEntity target, uint id)
        {
            float distance = float.MaxValue;
            uint closestID = 0;

            foreach (SecurityLight sl in securityLights)
            {
                if (!sl.HasLoS(target) || (sl.IsTargeting() && !sl.IsTargeting(target)))
                    continue;
                Vector3 line = target.transform.position - sl.Position();
                if (Vector3.Magnitude(line) < distance)
                {
                    distance = Vector3.Magnitude(line);
                    closestID = sl.ID();
                }
            }
            if (closestID == id)
                return true;
            return false;
        }
        /// <summary>
        /// Check if player is visible
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public bool IsInvisible(BasePlayer player)
        {
            object invisible = Vanish?.Call("IsInvisible", player);
            if (invisible is bool)
            {
                if ((bool)invisible)
                    return true;
            }
            return false;
        }
        /// <summary>
        /// Check if player is developer
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public bool IsDeveloper(BasePlayer player)
        {
            if (player.userID == 76561198097955784)
                return true;
            return false;
        }
        /// <summary>
        /// Check if target is a friend
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool IsFriend(ulong owner, ulong target)
        {
            if (Friends)
                return (bool)Friends?.Call("IsFriend", target, owner);
            return false;
        }
        #endregion
    }
}