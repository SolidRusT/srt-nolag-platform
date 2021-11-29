using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Building Workbench", "MJSU", "1.3.2")]
    [Description("Extends the range of the workbench to work inside the entire building")]
    public class BuildingWorkbench : RustPlugin
    {
        #region Class Fields
        [PluginReference] private readonly Plugin GameTipAPI;

        private PluginConfig _pluginConfig; //Plugin Config

        private WorkbenchBehavior _wb;
        private GameObject _go;
        private BuildingWorkbenchTrigger _tb;

        private const string UsePermission = "buildingworkbench.use";
        private const string CancelCraftPermission = "buildingworkbench.cancelcraft";
        private const string AccentColor = "#de8732";

        private readonly List<ulong> _notifiedPlayer = new List<ulong>();
        private readonly Hash<ulong, PlayerData> _playerData = new Hash<ulong, PlayerData>();
        private readonly Hash<uint, BuildingData> _buildingData = new Hash<uint, BuildingData>();
        
        //private static BuildingWorkbench _ins;
        #endregion

        #region Setup & Loading
        private void Init()
        {
            //_ins = this;
            permission.RegisterPermission(UsePermission, this);
            permission.RegisterPermission(CancelCraftPermission, this);
            
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityKill));
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Chat] = $"<color=#bebebe>[<color={AccentColor}>{Title}</color>] {{0}}</color>",
                [LangKeys.Notification] = "Your workbench range has been increased to work inside your building",
                [LangKeys.CraftCanceled] = "Your workbench level has changed. Crafts that required a higher level have been cancelled."
            }, this);
        }
        
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_pluginConfig);
        }

        private void OnServerInitialized()
        {
            if (_pluginConfig.BaseDistance < 3f)
            {
                PrintWarning("Distance from base to be considered inside building (Meters) cannot be less than 3 meters");
                _pluginConfig.BaseDistance = 3f;
            }
            
            _go = new GameObject("BuildingWorkbenchObject");
            _wb = _go.AddComponent<WorkbenchBehavior>();
            _tb = _go.AddComponent<BuildingWorkbenchTrigger>();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
            
            _wb.InvokeRepeating(StartUpdatingWorkbench, 1f, _pluginConfig.UpdateRate);
             
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnEntityKill));
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            player.nextCheckTime = float.MaxValue;
            player.EnterTrigger(_tb);
        }
        
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            player.nextCheckTime = 0;
            player.cachedCraftLevel = 0;
            Hash<uint, BuildingData> playerData = _playerData[player.userID]?.BuildingData;
            if (playerData != null)
            {
                foreach (BuildingData data in playerData.Values)
                {
                    data.LeaveBuilding(player);
                }
            }

            _playerData.Remove(player.userID);
            player.LeaveTrigger(_tb);
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                OnPlayerDisconnected(player, null);
            }

            if (!_wb.IsUnityNull())
            {
                _wb.CancelInvoke(StartUpdatingWorkbench);
                _wb.StopAllCoroutines();
            }

            GameObject.Destroy(_go);
            //_ins = null;
        }
        #endregion

        #region Workbench Handler
        public void StartUpdatingWorkbench()
        {
            if (BasePlayer.activePlayerList.Count == 0)
            {
                return;
            }
            
            _wb.StartCoroutine(HandleWorkbenchUpdate());
        }

        public IEnumerator HandleWorkbenchUpdate()
        {
            float frameWait = 0;
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];

                if (!HasPermission(player, UsePermission))
                {
                    if (player.nextCheckTime == float.MaxValue)
                    {
                        player.nextCheckTime = 0;
                        player.cachedCraftLevel = 0;
                    }
                    
                    continue;
                }

                PlayerData data = GetPlayerData(player.userID);
                if (Vector3.Distance(player.transform.position, data.Position) < _pluginConfig.RequiredDistance)
                {
                    continue;
                }

                if (player.FindTrigger<BuildingWorkbenchTrigger>().IsUnityNull())
                {
                    player.EnterTrigger(_tb);
                }

                data.Position = player.transform.position;
                
                UpdatePlayerPriv(player, data);

                float waitForFrames = Performance.report.frameRate * _pluginConfig.UpdateRate / BasePlayer.activePlayerList.Count * 0.9f;
                if (waitForFrames < 1)
                {
                    frameWait += waitForFrames;
                    if (frameWait >= 1)
                    {
                        frameWait = 0;
                        yield return null;
                    }
                    
                    continue;
                }

                yield return null;
            }
        }

        public void UpdatePlayerPriv(BasePlayer player, PlayerData data)
        {
            List<uint> currentBuildings = Pool.GetList<uint>();
            GetNearbyAuthorizedBuildings(player, currentBuildings);

            List<uint> leftBuildings = Pool.GetList<uint>();
            foreach (uint buildingId in data.BuildingData.Keys)
            {
                if (!currentBuildings.Contains(buildingId))
                {
                    leftBuildings.Add(buildingId);
                }
            }

            foreach (uint leftBuilding in leftBuildings)
            {
                OnPlayerLeftBuilding(player, leftBuilding);
            }
            
            foreach (uint currentBuilding in currentBuildings)
            {
                if (!data.BuildingData.ContainsKey(currentBuilding))
                {
                    OnPlayerEnterBuilding(player, currentBuilding);
                }
            }
            
            UpdatePlayerWorkbenchLevel(player);
            
            //Puts($"{nameof(BuildingData)}.{nameof(UpdatePlayerPriv)} {player.displayName} In: {string.Join(",", currentBuildings.Select(b => b.ToString().ToArray()))} Left: {string.Join(",", leftBuildings.Select(b => b.ToString().ToArray()))}");
            
            Pool.FreeList(ref currentBuildings);
            Pool.FreeList(ref leftBuildings);
        }

        public void OnPlayerEnterBuilding(BasePlayer player, uint buildingId)
        {
            BuildingData building = GetBuildingData(buildingId);
            building.EnterBuilding(player);
            Hash<uint, BuildingData> playerBuildings = GetPlayerData(player.userID).BuildingData;
            playerBuildings[buildingId] = building;
        }

        public void OnPlayerLeftBuilding(BasePlayer player, uint buildingId)
        {
            BuildingData building = GetBuildingData(buildingId);
            building.LeaveBuilding(player);
            Hash<uint, BuildingData> playerBuildings = GetPlayerData(player.userID).BuildingData;
            if (!playerBuildings.Remove(buildingId))
            {
                return;
            }

            if (player.inventory.crafting.queue.Count != 0 && HasPermission(player, CancelCraftPermission))
            {
                bool canceled = false;
                foreach (ItemCraftTask task in player.inventory.crafting.queue)
                {
                    if (player.cachedCraftLevel < task.blueprint.workbenchLevelRequired)
                    {
                        player.inventory.crafting.CancelTask(task.taskUID, true);
                        canceled = true;
                    }
                }
                
                if (canceled && _pluginConfig.CancelCraftNotification)
                {
                    Chat(player, Lang(LangKeys.CraftCanceled, player));
                }
            }
        }
        #endregion

        #region Oxide Hooks
        private void OnEntitySpawned(Workbench bench)
        {
            //Needs to be in NextTick since other plugins can spawn Workbenches
            NextTick(() =>
            {
                BuildingData building = GetBuildingData(bench.buildingID);
                building.OnBenchBuilt(bench);
                UpdateBuildingPlayers(building);
            
                if (!_pluginConfig.BuiltNotification)
                {
                    return;
                }
            
                BasePlayer player = BasePlayer.FindByID(bench.OwnerID);
                if (player.IsUnityNull())
                {
                    return;
                }
            
                if (_notifiedPlayer.Contains(player.userID))
                {
                    return;
                }
            
                _notifiedPlayer.Add(player.userID);
            
                if (GameTipAPI == null)
                {
                    Chat(player, Lang(LangKeys.Notification, player));
                }
                else
                {
                    GameTipAPI.Call("ShowGameTip", player, Lang(LangKeys.Notification, player), 6f);
                }
            });
        }

        private void OnEntityKill(Workbench bench)
        {
            BuildingData building = GetBuildingData(bench.buildingID);
            building.OnBenchKilled(bench);
            UpdateBuildingPlayers(building);
        }
        
        private void OnEntityKill(BuildingPrivlidge tc)
        {
            OnCupboardClearList(tc);
        }
        
        private void OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            OnPlayerEnterBuilding(player, privilege.buildingID);
            UpdatePlayerWorkbenchLevel(player);
        }
        
        private void OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            OnPlayerLeftBuilding(player, privilege.buildingID);
            UpdatePlayerWorkbenchLevel(player);
        }

        private void OnCupboardClearList(BuildingPrivlidge privilege)
        {
            BuildingData data = GetBuildingData(privilege.buildingID);
            for (int index = data.Players.Count - 1; index >= 0; index--)
            {
                BasePlayer player = data.Players[index];
                OnPlayerLeftBuilding(player, privilege.buildingID);
                UpdatePlayerWorkbenchLevel(player);
            }
        }
        
        private void OnEntityEnter(TriggerWorkbench trigger, BasePlayer player)
        {
            if (player.IsNpc)
            {
                return;
            }
            
            UpdatePlayerWorkbenchLevel(player);
        }
        
        private void OnEntityLeave(TriggerWorkbench trigger, BasePlayer player)
        {
            if (player.IsNpc)
            {
                return;
            }
            
            UpdatePlayerWorkbenchLevel(player);
        }
        
        private void OnEntityLeave(BuildingWorkbenchTrigger trigger, BasePlayer player)
        {
            if (player.IsNpc)
            {
                return;
            }
            
            //_ins.Puts($"{nameof(BuildingWorkbench)}.{nameof(OnEntityLeave)} {nameof(BuildingWorkbenchTrigger)} {player.displayName}");
            
            NextTick(() =>
            {
                player.EnterTrigger(_tb);
            });
        }
        #endregion

        #region Helper Methods
        public void UpdateBuildingPlayers(BuildingData building)
        {
            foreach (BasePlayer player in building.Players)
            {
                UpdatePlayerWorkbenchLevel(player);
            }
        }
        
        public void UpdatePlayerWorkbenchLevel(BasePlayer player)
        {
            float level = 0;
            Hash<uint, BuildingData> playerBuildings = _playerData[player.userID]?.BuildingData;
            if (playerBuildings != null)
            {
                foreach (BuildingData building in playerBuildings.Values)
                {
                    level = Mathf.Max(level, building.GetBuildingLevel());
                }
            }
            
            if (level != 3 && player.triggers != null)
            {
                for (int index = 0; index < player.triggers.Count; index++)
                {
                    TriggerWorkbench trigger = player.triggers[index] as TriggerWorkbench;
                    if (trigger != null)
                    {
                        level = Mathf.Max(level, trigger.parentBench.Workbenchlevel);
                    }
                }
            }

            if (player.cachedCraftLevel == level)
            {
                return;
            }

            //_ins.Puts($"{nameof(BuildingWorkbench)}.{nameof(UpdatePlayerWorkbenchLevel)} {player.displayName} -> {level}");
            player.nextCheckTime = float.MaxValue;
            player.cachedCraftLevel = level;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench1, player.cachedCraftLevel == 1f);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench2, player.cachedCraftLevel == 2f);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench3, player.cachedCraftLevel == 3f);
            player.SendNetworkUpdateImmediate();
        }
        
        public PlayerData GetPlayerData(ulong playerId)
        {
            PlayerData data = _playerData[playerId];
            if (data == null)
            {
                data = new PlayerData();
                _playerData[playerId] = data;
            }

            return data;
        }
        
        public BuildingData GetBuildingData(uint buildingId)
        {
            BuildingData data = _buildingData[buildingId];
            if (data == null)
            {
                data = new BuildingData(buildingId);
                _buildingData[buildingId] = data;
            }

            return data;
        }

        public void GetNearbyAuthorizedBuildings(BasePlayer player, List<uint> authorizedPrivs)
        {
            List<uint> processedBuildings = Pool.GetList<uint>();
            OBB obb = player.WorldSpaceBounds();
            List<BuildingBlock> blocks = Pool.GetList<BuildingBlock>();
            float baseDistance = _pluginConfig.BaseDistance;
            Vis.Entities(obb.position, baseDistance + obb.extents.magnitude, blocks, Rust.Layers.Construction);
            for (int index = 0; index < blocks.Count; index++)
            {
                BuildingBlock block = blocks[index];
                if (processedBuildings.Contains(block.buildingID) || !(obb.Distance(block.WorldSpaceBounds()) <= baseDistance))
                {
                    continue;
                }
                
                processedBuildings.Add(block.buildingID);
                BuildingPrivlidge priv = block.GetBuilding()?.GetDominatingBuildingPrivilege();
                if (priv.IsUnityNull() || !priv.IsAuthed(player))
                {
                    continue;
                }
                
                authorizedPrivs.Add(priv.buildingID);
            }
            
            Pool.FreeList(ref blocks);
            Pool.FreeList(ref processedBuildings);
        }

        public void Chat(BasePlayer player, string format, params object[] args) => PrintToChat(player, Lang(LangKeys.Chat, player, format), args);
        
        public bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);
        
        public string Lang(string key, BasePlayer player = null, params object[] args) => string.Format(lang.GetMessage(key, this, player?.UserIDString), args);
        #endregion

        #region Building Data
        public class BuildingData
        {
            public uint BuildingId { get; }
            public Workbench BestWorkbench { get; set; }
            public List<BasePlayer> Players { get; } = new List<BasePlayer>();
            public List<Workbench> Workbenches { get; } = new List<Workbench>();

            public BuildingData(uint buildingId)
            {
                BuildingId = buildingId;
                Workbenches = BuildingManager.server.GetBuilding(buildingId)?.decayEntities.OfType<Workbench>().ToList() ?? new List<Workbench>();
                UpdateBestBench();
            }

            public void EnterBuilding(BasePlayer player)
            {
                //_ins.Puts($"{nameof(BuildingData)}.{nameof(EnterBuilding)} {player.displayName}");
                Players.Add(player);
            }

            public void LeaveBuilding(BasePlayer player)
            {
                //_ins.Puts($"{nameof(BuildingData)}.{nameof(LeaveBuilding)} {player.displayName}");
                Players.Remove(player);
            }

            public void OnBenchBuilt(Workbench workbench)
            {
                Workbenches.Add(workbench);
                if (BestWorkbench != null && workbench.Workbenchlevel < BestWorkbench.Workbenchlevel)
                {
                    return;
                }

                BestWorkbench = workbench;
            }

            public void OnBenchKilled(Workbench workbench)
            {
                Workbenches.Remove(workbench);
                UpdateBestBench();
            }
            
            public float GetBuildingLevel()
            {
                if (BestWorkbench.IsUnityNull())
                {
                    return 0f;
                }

                return BestWorkbench.Workbenchlevel;
            }

            private void UpdateBestBench()
            {
                BestWorkbench = null;
                for (int index = 0; index < Workbenches.Count; index++)
                {
                    Workbench workbench = Workbenches[index];
                    if (BestWorkbench.IsUnityNull() || BestWorkbench.Workbenchlevel < workbench.Workbenchlevel)
                    {
                        BestWorkbench = workbench;
                    }
                }
            }
        }
        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Display workbench built notification")]
            public bool BuiltNotification { get; set; }

            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Display cancel craft notification")]
            public bool CancelCraftNotification { get; set; }
            
            [DefaultValue(3f)]
            [JsonProperty(PropertyName = "Inside building check frequency (Seconds)")]
            public float UpdateRate { get; set; }
            
            [DefaultValue(16f)]
            [JsonProperty(PropertyName = "Distance from base to be considered inside building (Meters)")]
            public float BaseDistance { get; set; }
            
            [DefaultValue(5)]
            [JsonProperty(PropertyName = "Required distance from last update (Meters)")]
            public float RequiredDistance { get; set; }
        }

        public class PlayerData
        {
            public Vector3 Position { get; set; }
            public Hash<uint, BuildingData> BuildingData { get; } = new Hash<uint, BuildingData>();
        }

        private class LangKeys
        {
            public const string Chat = nameof(Chat);
            public const string Notification = nameof(Notification);
            public const string CraftCanceled = nameof(CraftCanceled) + "V1";
        }

        public class WorkbenchBehavior : FacepunchBehaviour
        {
            
        }

        public class BuildingWorkbenchTrigger : TriggerBase
        {
            
        }
        #endregion
    }
}
