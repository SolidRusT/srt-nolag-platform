/*
    NPCVendingMapMarker - A Rust umod plugin to add in-game vending map markers to NPC's.
    Copyright (C) 2020 by Pinguin

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("NPC Vending Map Marker", "PinguinNordpol", "0.1.0")]
    [Description("Adds in-game vending map markers to NPC's")]
    class NPCVendingMapMarker : CovalencePlugin
    {
        #region Fields
        [PluginReference]
        private Plugin HumanNPC;

        // Configuration data
        private NPCVendingMapMarkerConfig config_data;
        // List of currently spawned vending map markers
        private Dictionary<ulong, VendingMachineMapMarker> map_markers = new Dictionary<ulong, VendingMachineMapMarker>();
        // Structure to hold temporary config values while adding a new vending map marker
        private struct TempVmm
        {
            public ulong request_uid;
            public string shop_name;
        }
        // List of current temporary vending map markers values
        private List<TempVmm> TempVmms = new List<TempVmm>();
        #endregion

        #region Plugin Config
        /*
         * Classes & functions to load / store plugin configuration
         */
        private class MarkerData
        {
            public string shop_name;
            public UnityEngine.Vector3 position;

            /*
             * Constructor
             */
            public MarkerData(string _shop_name, UnityEngine.Vector3 _position)
            {
                this.shop_name = _shop_name;
                this.position = _position;
            }
        }
        private class NPCVendingMapMarkerConfig
        {
            public Dictionary<ulong, MarkerData> configured_markers = new Dictionary<ulong, MarkerData>();
            public string currency_sign = "RP";
            public bool debug = false;
        }
        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }
        private NPCVendingMapMarkerConfig GetDefaultConfig()
        {
            return new NPCVendingMapMarkerConfig();
        }
        #endregion

        #region ServerRewards structures / functions
        private class SR_NPCData
        {
            public Dictionary<string, SR_NPCInfo> npcInfo = new Dictionary<string, SR_NPCInfo>();

            public class SR_NPCInfo
            {
                public string name;
                public float x, z;
                public bool useCustom, sellItems, sellKits, sellCommands, canTransfer, canSell, canExchange;
                public List<string> items = new List<string>();
                public List<string> kits = new List<string>();
                public List<string> commands = new List<string>();
            }
        }
        private class SR_RewardData
        {
            public Dictionary<string, SR_RewardItem> items = new Dictionary<string, SR_RewardItem>();
            public SortedDictionary<string, SR_RewardKit> kits = new SortedDictionary<string, SR_RewardKit>();
            public SortedDictionary<string, SR_RewardCommand> commands = new SortedDictionary<string, SR_RewardCommand>();

            public class SR_RewardItem : SR_Reward
            {
                public string shortname, customIcon;
                public int amount;
                public ulong skinId;
                public bool isBp;
                public SR_Category category;
            }

            public enum SR_Category { None, Weapon, Construction, Items, Resources, Attire, Tool, Medical, Food, Ammunition, Traps, Misc, Component, Electrical, Fun }

            public class SR_RewardKit : SR_Reward
            {
                public string kitName, description, iconName;
            }

            public class SR_RewardCommand : SR_Reward
            {
                public string description, iconName;
                public List<string> commands = new List<string>();
            }

            public class SR_Reward
            {
                public string displayName;
                public int cost;
                public int cooldown;
            }
        }

        // ServerRewards data
        private SR_NPCData sr_npcdata;
        private SR_RewardData sr_rewarddata;
        private bool sr_enabled = false;

        /*
         * Try to read ServerRewards data files
         */
        private void ReadServerRewardsData()
        {
            bool flag = true;
            DynamicConfigFile npc_data, reward_data;

            // Load data files
            this.LogDebug("Trying to read ServerRewards plugin data files");
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("ServerRewards/npc_data") || !Interface.Oxide.DataFileSystem.ExistsDatafile("ServerRewards/reward_data"))
            {
                // If data files do not exist, do not try to load them to prevent creating empty files
                this.sr_enabled = false;
                return;
            }
            npc_data = Interface.Oxide.DataFileSystem.GetFile("ServerRewards/npc_data");
            reward_data = Interface.Oxide.DataFileSystem.GetFile("ServerRewards/reward_data");

            // Try parsing the data files
            try
            {
                this.sr_rewarddata = reward_data.ReadObject<SR_RewardData>();
                this.LogDebug(string.Format("Successfully read {0} ServerRewards reward entrie(s)", this.sr_rewarddata.items.Count));
            }
            catch
            {
                this.LogDebug("No ServerRewards reward data found");
                this.sr_rewarddata = new SR_RewardData();
                flag = false;
            }
            try
            {
                this.sr_npcdata = npc_data.ReadObject<SR_NPCData>();
                this.LogDebug(string.Format("Successfully read {0} ServerRewards NPC entrie(s)", this.sr_npcdata.npcInfo.Count));
            }
            catch
            {
                this.LogDebug("No ServerRewards npc data found");
                this.sr_npcdata = new SR_NPCData();
                flag = false;
            }

            this.sr_enabled = flag;
        }

        /*
         * 
         */
        private string GetServerRewardsData(string _npc_id)
        {
            string ret = "";

            // Make sure we ServerRewards data files were loaded
            if (!this.sr_enabled) return "";
            
            // Check if the given NPC has a shop configured
            this.LogDebug("Checking ServerRewards data for shop items to display");
            if (!this.sr_npcdata.npcInfo.ContainsKey(_npc_id)) return "";

            // Check if shop has items to sell
            if (this.sr_npcdata.npcInfo[_npc_id].items.Count == 0) return "";

            // Parse and add items
            this.LogDebug("ServerRewards has shop items to display for current NPC");
            foreach (string reward_item in this.sr_npcdata.npcInfo[_npc_id].items)
            {
                if (this.sr_rewarddata.items.ContainsKey(reward_item))
                {
                    this.LogDebug(string.Format("Found values for item '{0}'. Adding", reward_item));
                    ret += string.Format("\r\n{0} {1} | {2} {3}", this.sr_rewarddata.items[reward_item].amount, this.sr_rewarddata.items[reward_item].displayName, this.sr_rewarddata.items[reward_item].cost, this.config_data.currency_sign);
                }
                else
                {
                    this.LogDebug(string.Format("Did not find values for item '{0}'", reward_item));
                }
            }

            if (ret != "") ret = "\r\n" + ret;
            return ret;
        }
        #endregion

        #region Umod Hooks
        /*
         * Initialize plugin once loaded
         */
        void Loaded()
        {
            if (this.HumanNPC == null)
            {
                // Can't do much without HumanNPC
                LogWarning("This plugin requires the HumanNPC plugin in order to be of any use!");
            }

            // Unsubscribe from OnUseNPC until we actually need it
            Unsubscribe("OnUseNPC");

            // Try reading ServerRewards plugin data
            this.ReadServerRewardsData();
        }

        /*
         * Remove all vending map markers on unload
         */
        void Unload()
        {
            this.ClearVendingMapMarkers();
        }

        /*
         * Load plugin config as soon as possible
         */
        void Init()
        {
            // Load plugin config
            this.config_data = Config.ReadObject<NPCVendingMapMarkerConfig>();
        }
        #endregion

        #region HumanNPC Hooks
        /*
         * Add vending map marker when configured NPCs respawn
         */
        void OnNPCRespawn(BasePlayer npc)
        {
            // If new NPC has a vending map marker configured, display it
            foreach(KeyValuePair<ulong, MarkerData> cm in this.config_data.configured_markers)
            {
                if(cm.Key == npc.userID)
                {
                    this.AddMapMarker(cm.Key, cm.Value);
                    break;
                }
            }
        }

        /*
         * Remove vending map marker when configured NPCs die
         */
        void OnKillNPC(BasePlayer npc, HitInfo hitinfo)
        {
            // If NPC had a vending map marker, remove it
            Dictionary<ulong, MarkerData>.KeyCollection keys = this.config_data.configured_markers.Keys;
            foreach (ulong key in keys.ToList())
            {
                if(key == npc.userID)
                {
                    this.RemoveMapMarker(key);
                    break;
                }
            }
        }

        /*
         * Add vending map marker to NPC
         */
        void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            // Check if we have a temporary configuration from current player
            this.LogDebug(string.Format("Looping over {0} temp_vmms", this.TempVmms.Count));
            for(int i = 0; i < this.TempVmms.Count; i++)
            {
                if(this.TempVmms[i].request_uid == player.userID) {
                    // Found a temporary configuration, let's finish it
                    this.LogDebug(string.Format("Found temp_vmm for user id {0}", player.userID));

                    // Make sure selected NPC is unconfigured
                    if (this.map_markers.ContainsKey(npc.userID)) {
                        this.ReplyToPlayer(player.IPlayer, string.Format(lang.GetMessage("NpcAlreadyHasMapMarker", this, player.IPlayer.Id), npc.userID));
                        return;
                    }

                    // Try to re-read ServerRewards data files
                    this.ReadServerRewardsData();

                    // Create new vending map marker for NPC and display it
                    this.LogDebug(string.Format("Creating new npcvmm for npc id {0} with name '{1}' at position {2}", npc.userID, this.TempVmms[i].shop_name, npc.transform.position.ToString()));
                    MarkerData marker_data = new MarkerData(this.TempVmms[i].shop_name, npc.transform.position);
                    if(!this.AddMapMarker(npc.userID, marker_data))
                    {
                        this.ReplyToPlayer(player.IPlayer, string.Format(lang.GetMessage("FailedSpawningMarker", this, player.IPlayer.Id)));
                        return;
                    }

                    // Add marker permanently to config
                    this.config_data.configured_markers.Add(npc.userID, marker_data);
                    Config.WriteObject<NPCVendingMapMarkerConfig>(this.config_data, true);

                    // Remove temporary config values
                    this.TempVmms.RemoveAt(i);
                    this.ReplyToPlayer(player.IPlayer, string.Format(lang.GetMessage("AddingMapMarkerSuccess", this, player.IPlayer.Id), npc.userID));
                }
            }

            // Unsubscribe from OnUseNPC hook if it is not needed anymore
            if(this.TempVmms.Count == 0) Unsubscribe("OnUseNPC");
        }
        #endregion

        #region Localization
        /*
         * Load default messages
         */
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UsageHelp"] = "NPCVendingMapMarker v{0} - A Rust umod plugin to add in-game vending map markers to NPC's.\r\n" +
                                      "Copyright(C) 2020 by Pinguin and released under GPLv3\r\n" +
                                      "\r\n" +
                                      "The following commands are available:\r\n" +
                                      "  npcvmm.add : Add a new NPC vending map marker.\r\n" +
                                      "  npcvmm.reset : Reset temporary NPC vending map marker data for current player.\r\n" +
                                      "  npcvmm.list : List all configured NPC vending map markers.\r\n" +
                                      "  npcvmm.del : Delete a single NPC vending map marker.\r\n" +
                                      "  npcvmm.clear : Delete all configured NPC vending map markers.\r\n" +
                                      "  npcvmm.refresh : Refresh all configured NPC vending map markers.\r\n" +
                                      "  npcvmm.debug : Toggle internal debugging on/off.\r\n" +
                                      "\r\n" +
                                      "For commands that take arguments, more help is available by executing them without any arguments.\r\n" +
                                      "\r\n" +
                                      "To be able to execute any NPCVendingMapMarker commands, you need to have the umod 'npcvendingmapmarker.admin' right assigned to your user.",
                ["DebugOn"] = "Switched debugging ON",
                ["DebugOff"] = "Switched debugging OFF",
                ["MissingShopName"] = "Usage: npcvmm.add <shop name>",
                ["UseNpcToAdd"] = "The next NPC you use will get a vending map marker with the name '{0}'",
                ["NpcAlreadyHasMapMarker"] = "The selected NPC '{0}' already has a vending map marker configured!",
                ["FailedSpawningMarker"] = "Unable to spawn vending map marker!",
                ["AddingMapMarkerSuccess"] = "Successfully added vending map marker to NPC '{0}'",
                ["AlreadyHaveTempVmm"] = "You can't add another marker until you finished adding the previous one! Use an NPC to add your previously configured map marker or do npcvmm.reset",
                ["ResetSuccessful"] = "Successfully removed temporary config values",
                ["ResetFailed"] = "No temporary config values available",
                ["TotalConfiguredMarkers"] = "You have a total of {0} NPC vending map marker(s) configured",
                ["MarkerDetails"] = "{0} @ {1} : '{2}'",
                ["MissingNpcId"] = "Usage: npcvmm.del <npc_id>",
                ["InvalidNpcId"] = "The given NPC ID is not valid!",
                ["NpcvmmRemoved"] = "The vending map marker for NPC id {0} was removed",
                ["UnknownNpcId"] = "No vending map marker for NPC id {0} configured",
                ["AllCleared"] = "All vending map markers cleared",
            }, this);
        }
        #endregion

        #region Console Commands
        /*
         * Print Usage info
         */
        [Command("npcvmm")]
        void CmdInfo(IPlayer player, string command, string[] args)
        {
            this.ReplyToPlayer(player, string.Format(lang.GetMessage("UsageHelp", this, player.Id), Version));
        }

        /*
         * Toggle debug on/off
         */
        [Command("npcvmm.debug"), Permission("npcvendingmapmarker.admin")]
        void CmdDebug(IPlayer player, string command, string[] args)
        {
            this.config_data.debug = !this.config_data.debug;
            if(this.config_data.debug) this.ReplyToPlayer(player, string.Format(lang.GetMessage("DebugOn", this, player.Id)));
            else this.ReplyToPlayer(player, string.Format(lang.GetMessage("DebugOff", this, player.Id)));
            Config.WriteObject<NPCVendingMapMarkerConfig>(this.config_data, true);
        }

        /*
         * Add a new NPC vending map marker
         */
        [Command("npcvmm.add"), Permission("npcvendingmapmarker.admin")]
        void CmdAdd(IPlayer player, string command, string[] args)
        {
            BasePlayer base_player = player.Object as BasePlayer;

            // Check command line args
            if (args.Length != 1)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("MissingShopName", this, player.Id)));
                return;
            }

            // Make sure we don't already have temporary config values
            bool flag = false;
            foreach(TempVmm temp_vmm in this.TempVmms)
            {
                if(temp_vmm.request_uid == base_player.userID)
                {
                    this.ReplyToPlayer(player, string.Format(lang.GetMessage("AlreadyHaveTempVmm", this, player.Id), args[0]));
                    flag = true;
                    break;
                }
            }
            if(flag) return;

            // Create temporary config values
            TempVmm temp_vmm2;
            temp_vmm2.request_uid = base_player.userID;
            temp_vmm2.shop_name = args[0];
            this.TempVmms.Add(temp_vmm2);

            // Subscribe to OnUseNPC to receive npc id & position and tell player to use NPC now
            Subscribe("OnUseNPC");
            this.ReplyToPlayer(player, string.Format(lang.GetMessage("UseNpcToAdd", this, player.Id), args[0]));
        }

        /*
         * Remove temporary config values
         */
        [Command("npcvmm.reset"), Permission("npcvendingmapmarker.admin")]
        void CmdReset(IPlayer player, string command, string[] args)
        {
            BasePlayer base_player = player.Object as BasePlayer;
            bool flag = false;
            for(int i = 0; i < this.TempVmms.Count; i++)
            {
                if(this.TempVmms[i].request_uid == base_player.userID)
                {
                    this.TempVmms.RemoveAt(i);
                    flag = true;
                    break;
                }
            }
            if(flag) this.ReplyToPlayer(player, string.Format(lang.GetMessage("ResetSuccessful", this, player.Id)));
            else this.ReplyToPlayer(player, string.Format(lang.GetMessage("ResetFailed", this, player.Id)));
        }

        /*
         * List all configured vending map markers
         */
        [Command("npcvmm.list"), Permission("npcvendingmapmarker.admin")]
        void CmdList(IPlayer player, string command, string[] args)
        {
            this.ReplyToPlayer(player, string.Format(lang.GetMessage("TotalConfiguredMarkers", this, player.Id), this.config_data.configured_markers.Count));
            foreach (KeyValuePair<ulong, MarkerData> cm in this.config_data.configured_markers)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("MarkerDetails", this, player.Id), cm.Key, cm.Value.position.ToString(), cm.Value.shop_name));
            }
        }

        /*
         * Delete a configured vending map markers
         */
        [Command("npcvmm.del"), Permission("npcvendingmapmarker.admin")]
        void CmdDel(IPlayer player, string command, string[] args)
        {
            BasePlayer base_player = player.Object as BasePlayer;
            ulong npc_id = 0;

            // Check command line args
            if (args.Length != 1)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("MissingNpcId", this, player.Id)));
                return;
            }
            if (!ulong.TryParse(args[0], out npc_id))
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("InvalidNpcId", this, player.Id)));
                return;
            }

            // Check if we have a configuration for the given NPC id
            if(this.config_data.configured_markers.ContainsKey(npc_id))
            {
                // Remove npcvmm from config and despawn it
                this.config_data.configured_markers.Remove(npc_id);
                Config.WriteObject<NPCVendingMapMarkerConfig>(this.config_data, true);
                this.RemoveMapMarker(npc_id);
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("NpcvmmRemoved", this, player.Id), npc_id));
            }
            else
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("UnknownNpcId", this, player.Id), npc_id));
            }
        }

        /*
         * Delete all configured vending map markers
         */
        [Command("npcvmm.clear"), Permission("npcvendingmapmarker.admin")]
        void CmdClear(IPlayer player, string command, string[] args)
        {
            this.ClearVendingMapMarkers();
            this.config_data.configured_markers.Clear();
            Config.WriteObject<NPCVendingMapMarkerConfig>(this.config_data, true);
            this.ReplyToPlayer(player, string.Format(lang.GetMessage("AllCleared", this, player.Id)));
        }

        /*
         * Refresh all configured vending map markers
         */
        [Command("npcvmm.refresh"), Permission("npcvendingmapmarker.admin")]
        void CmdRefresh(IPlayer player, string command, string[] args)
        {
            // Try to re-read ServerRewards data files
            this.ReadServerRewardsData();

            // Iterate over all spawned vmms and re-add them
            Dictionary<ulong, VendingMachineMapMarker>.KeyCollection keys = this.map_markers.Keys;
            this.LogDebug(string.Format("Refreshing {0} npcvmm(s)", keys.Count));
            foreach (ulong key in keys.ToList())
            {
                // Update / remove npcvmms
                if (this.config_data.configured_markers.ContainsKey(key))
                {
                    this.LogDebug(string.Format("Refreshing npcvmm for npc {0}", key));
                    this.AddMapMarker(key, this.config_data.configured_markers[key]);
                }
                else
                {
                    this.LogDebug(string.Format("Found unconfigured npcvmm for npc {0}. Removing", key));
                    this.RemoveMapMarker(key);
                }
            }
        }
        #endregion

        #region Helper Functions
        /*
         * Add a vending map marker to the map
         */
        private bool AddMapMarker(ulong _npc_id, MarkerData _marker_data)
        {
            this.LogDebug(string.Format("Spawning npcvmm for npc id {0} with name '{1}' at position {2}", _npc_id, _marker_data.shop_name, _marker_data.position.ToString()));

            // Make sure to remove an already existing npcvmm first
            this.RemoveMapMarker(_npc_id);

            // Create and configure new marker
            VendingMachineMapMarker vmmm = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", _marker_data.position, UnityEngine.Quaternion.identity, true) as VendingMachineMapMarker;
            if (vmmm == null)
            {
                this.LogDebug("CreateEntity failed!");
                return false;
            }
            vmmm.server_vendingMachine = null;

            // Set busy flag to have a green symbol
            vmmm.SetFlag(BaseEntity.Flags.Busy, true, false, true);

            // Set shop name and add shop info if available
            vmmm.markerShopName = _marker_data.shop_name;
            vmmm.markerShopName += this.GetServerRewardsData(_npc_id.ToString());

            // Spawn marker on map
            vmmm.Spawn();
            vmmm.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            // Add marker to our marker list
            this.map_markers.Add(_npc_id, vmmm);

            this.LogDebug(string.Format("Successfully spawned npcvmm for npc id {0} with name '{1}' at position {2}", _npc_id, _marker_data.shop_name, _marker_data.position.ToString()));
            return true;
        }

        /*
         * Remove a vending map marker from the map
         */
        private bool RemoveMapMarker(ulong _npc_id)
        {
            // Search map marker, kill it and remove it from list
            if(this.map_markers.ContainsKey(_npc_id))
            {
                this.LogDebug(string.Format("npcvmm for npc id {0} exists. Removing", _npc_id));
                this.map_markers[_npc_id].Kill(BaseNetworkable.DestroyMode.None);
                this.map_markers.Remove(_npc_id);
                return true;
            }
            return false;
        }

        /*
         * Check if a given NPC already has a configured vending map marker
         */
        private bool HasMapMarker(ulong _npc_id)
        {
            return this.map_markers.ContainsKey(_npc_id);
        }

        /*
         * Remove all added vending map markers
         */
        private void ClearVendingMapMarkers()
        {
            Dictionary<ulong, VendingMachineMapMarker>.ValueCollection markers = this.map_markers.Values;
            this.LogDebug(string.Format("Removing {0} npcvmm(s)", markers.Count));
            foreach (VendingMachineMapMarker map_marker in markers)
            {
                // Remove map marker from map and our marker list
                map_marker.Kill(BaseNetworkable.DestroyMode.None);
            }
            this.map_markers.Clear();
        }

        /*
         * Helper functions to send messages to players / console
         */
        private void ReplyToPlayer(IPlayer player, string msg)
        {
            player.Reply(msg);
        }
        private void Log(string msg)
        {
            Puts(msg);
        }
        private void LogDebug(string msg)
        {
            if(this.config_data.debug)
            {
                this.Log("DEBUG :: " + msg);
            }
        }
        #endregion
    }
}
