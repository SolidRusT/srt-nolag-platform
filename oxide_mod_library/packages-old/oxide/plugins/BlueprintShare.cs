using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Blueprint Share", "c_creep", "1.2.10")]
    [Description("Allows players to share researched blueprints with their friends, clan or team")]
    class BlueprintShare : RustPlugin
    {
        #region Fields

        [PluginReference]
        private Plugin Clans, Friends;

        private StoredData storedData;
        
        private const string UsePermission = "blueprintshare.use";
        private const string TogglePermission = "blueprintshare.toggle";
        private const string SharePermission = "blueprintshare.share";

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            LoadData();

            permission.RegisterPermission(UsePermission, this);
            permission.RegisterPermission(TogglePermission, this);
            permission.RegisterPermission(SharePermission, this);
        }

        private void OnNewSave(string filename)
        {
            if (!config.ClearDataOnWipe) return;

            CreateData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            var playerID = player.UserIDString;

            if (PlayerDataExists(playerID)) return;

            CreatePlayerData(playerID);
        }

        private void OnItemAction(Item item, string action, BasePlayer player)
        {
            if (!config.PhysicalSharingEnabled) return;
            if (player == null || item == null) return;
            if (action != "study") return;
            
            if (CanShareBlueprint(item.blueprintTargetDef, player))
            {
                item.Remove();
            }
        }

        private void OnTechTreeNodeUnlocked(Workbench workbench, TechTreeData.NodeInstance node, BasePlayer player)
        {
            if (!config.TechTreeSharingEnabled) return;
            if (workbench == null || node == null || player == null) return;

            CanShareBlueprint(node.itemDef, player);
        }

        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (!config.ShareBlueprintsOnJoin) return;
            
            NextTick(() =>
            {
                if (team == null || player == null) return;

                if (team.members.Contains(player.userID))
                {
                    ShareWithPlayer(team.GetLeader(), player);
                }
            });
        }

        #endregion

        #region External Plugins

        #region Friends

        private bool HasFriends(ulong playerID)
        {
            if (Friends == null || !Friends.IsLoaded) return false;

            var friendsList = Friends.Call<ulong[]>("GetFriends", playerID);

            return friendsList != null && friendsList.Length != 0;
        }

        private List<ulong> GetFriends(ulong playerID)
        {
            if (Friends == null || !Friends.IsLoaded) return new List<ulong>();
            
            var friends = Friends.Call<ulong[]>("GetFriends", playerID);

            return friends.ToList();
        }

        private bool AreFriends(string playerID, string targetID)
        {
            return Friends != null && Friends.IsLoaded && Friends.Call<bool>("AreFriends", playerID, targetID);
        }

        #endregion

        #region Clan

        private bool InClan(ulong playerID)
        {
            if (Clans == null) return false;

            var clanName = Clans?.Call<string>("GetClanOf", playerID);

            return clanName != null;
        }

        private List<ulong> GetClanMembers(ulong playerID)
        {
            // Clans and Clans Reborn
            var clanMembers = Clans?.Call<List<string>>("GetClanMembers", playerID);

            if (clanMembers != null)
            {
                return clanMembers.Select(x => ulong.Parse(x)).ToList();
            }

            // Rust:IO Clans
            var clanName = Clans?.Call<string>("GetClanOf", playerID);

            return !string.IsNullOrEmpty(clanName) ? GetClanMembers(clanName) : null;
        }

        private List<ulong> GetClanMembers(string clanName)
        {
            var clan = Clans?.Call<JObject>("GetClan", clanName);

            var members = clan?.GetValue("members") as JArray;

            return members?.Select(Convert.ToUInt64).ToList();
        }

        private bool SameClan(string playerID, string targetID)
        {
            if (Clans == null) return false;

            // Clans and Clans Reborn
            var isClanMember = Clans?.Call("IsClanMember", playerID, targetID);

            if (isClanMember != null)
            {
                return (bool)isClanMember;
            }

            // Rust:IO Clans
            var playerClan = Clans?.Call("GetClanOf", playerID);

            if (playerClan == null) return false;

            var targetClan = Clans?.Call("GetClanOf", targetID);

            if (targetClan == null) return false;

            return (string)playerClan == (string)targetClan;
        }

        #endregion

        #region Team

        private bool InTeam(ulong playerID)
        {
            var player = RustCore.FindPlayerById(playerID);

            return player.currentTeam != 0;
        }

        private List<ulong> GetTeamMembers(ulong playerID)
        {
            var player = RustCore.FindPlayerById(playerID);

            return player?.Team.members;
        }

        private bool SameTeam(BasePlayer player, BasePlayer target)
        {
            if (player.currentTeam == 0 || target.currentTeam == 0) return false;

            var playerTeam = player.currentTeam;
            var targetTeam = target.currentTeam;

            return playerTeam == targetTeam;
        }

        #endregion

        #endregion

        #region External Plugin Hooks

        private void OnFriendAdded(string playerID, string friendID)
        {
            if (!config.ShareBlueprintsOnJoin) return;

            var player = RustCore.FindPlayerByIdString(playerID);

            if (player == null) return;

            var friend = RustCore.FindPlayerByIdString(friendID);

            if (friend == null) return;

            ShareWithPlayer(player, friend);
        }

        private void OnClanMemberJoined(string playerID, List<string> memberIDs)
        {
            if (!config.ShareBlueprintsOnJoin) return;

            var player = RustCore.FindPlayerByIdString(playerID);

            var ids = memberIDs.Select(x => ulong.Parse(x)).ToList();

            var clanMembers = FindPlayers(ids, player.userID);

            if (clanMembers.Count > 0)
            {
                foreach (var clanMember in clanMembers)
                {
                    if (player == null || clanMember == null) continue;
                            
                    ShareWithPlayer(clanMember, player);
                }
            }
        }

        #endregion

        #region Core

        private bool CanShareBlueprint(ItemDefinition item, BasePlayer player)
        {
            if (item == null || player == null) return false;
            if (!permission.UserHasPermission(player.UserIDString, UsePermission)) return false;

            var playerID = player.UserIDString;

            if (BlueprintBlocked(item))
            {
                player.ChatMessage(GetLangValue("Prefix", playerID) + GetLangValue("BlueprintBlocked", playerID, item.displayName.translated));

                return false;
            }

            if (SharingEnabled(playerID))
            {
                if (InTeam(player.userID) || InClan(player.userID) || HasFriends(player.userID))
                {
                    if (SomeoneWillLearnBlueprint(player, item))
                    {
                        ShareWithPlayers(player, item);
                        HandleAdditionalBlueprints(player, item);

                        return true;
                    }
                }
            }

            return false;
        }

        private void HandleAdditionalBlueprints(BasePlayer player, ItemDefinition item)
        {
            var additionalBlueprints = item.Blueprint.additionalUnlocks;

            if (additionalBlueprints.Count > 0)
            {
                foreach (var blueprint in additionalBlueprints)
                {
                    UnlockBlueprint(player, blueprint.itemid);

                    ShareWithPlayers(player, blueprint);
                }
            }
        }

        private bool UnlockBlueprint(BasePlayer player, int blueprint)
        {
            if (player == null) return false;

            var playerInfo = player.PersistantPlayerInfo;

            var unlockedBlueprints = playerInfo.unlockedItems;

            if (!unlockedBlueprints.Contains(blueprint))
            {
                unlockedBlueprints.Add(blueprint);

                player.PersistantPlayerInfo = playerInfo;
                player.SendNetworkUpdateImmediate();
                player.ClientRPCPlayer(null, player, "UnlockedBlueprint", 0);

                PlaySoundEffect(player);

                return true;
            }

            return false;
        }

        private void ShareWithPlayers(BasePlayer player, ItemDefinition item)
        {
            if (player == null || item == null) return;

            var targets = SelectSharePlayers(player);

            var successfulUnlocks = 0;

            foreach (var target in targets)
            {
                if (target != null)
                {
                    if (SharingEnabled(target.UserIDString))
                    {
                        if (UnlockBlueprint(target, item.itemid))
                        {
                            target.ChatMessage(GetLangValue("Prefix", player.UserIDString) + GetLangValue("TargetLearntBlueprint", target.UserIDString, player.displayName, item.displayName.translated));

                            successfulUnlocks++;
                        }
                    }
                }
            }

            if (successfulUnlocks > 0)
            {
                player.ChatMessage(GetLangValue("Prefix", player.UserIDString) + GetLangValue("SharerLearntBlueprint", player.UserIDString, item.displayName.translated, successfulUnlocks));
            }
        }

        private void ShareWithPlayer(BasePlayer player, BasePlayer target)
        {
            if (player == null || target == null) return;

            var playerID = player.UserIDString;
            var targetID = target.UserIDString;

            if (!SharingEnabled(targetID))
            {
                player.ChatMessage(GetLangValue("Prefix", playerID) + GetLangValue("TargetSharingDisabled", playerID, target.displayName));

                return;
            }

            if (!SameTeam(player, target) && !SameClan(playerID, targetID) && !AreFriends(playerID, targetID))
            {
                player.ChatMessage(GetLangValue("Prefix", playerID) + GetLangValue("CannotShare", playerID));

                return;
            }

            var playerBlueprints = GetPlayerBlueprintItems(player.PersistantPlayerInfo.unlockedItems);

            foreach (var blueprint in playerBlueprints.ToList())
            {
                if (BlueprintBlocked(blueprint))
                {
                    playerBlueprints.Remove(blueprint);
                }
            }

            var unlockedItems = ConvertItemsToIds(playerBlueprints);
            var learnedBlueprints = UnlockBlueprints(target, unlockedItems);

            if (learnedBlueprints > 0)
            {
                player.ChatMessage(GetLangValue("Prefix", playerID) + GetLangValue("SharerSuccess", playerID, learnedBlueprints, target.displayName));

                target.ChatMessage(GetLangValue("Prefix", targetID) + GetLangValue("ShareReceieve", targetID, player.displayName, learnedBlueprints));
            }
            else
            {
                player.ChatMessage(GetLangValue("Prefix", playerID) + GetLangValue("NoBlueprintsToShare", playerID, target.displayName));
            }
        }

        private int UnlockBlueprints(BasePlayer player, List<int> blueprints)
        {
            if (player != null)
            {
                var playerInfo = player.PersistantPlayerInfo;

                var unlockedBlueprints = playerInfo.unlockedItems;

                var successfulUnlocks = 0;

                foreach (var blueprint in blueprints)
                {
                    if (!unlockedBlueprints.Contains(blueprint))
                    {
                        unlockedBlueprints.Add(blueprint);

                        successfulUnlocks++;
                    }
                }

                player.PersistantPlayerInfo = playerInfo;
                player.SendNetworkUpdateImmediate();
                player.ClientRPCPlayer(null, player, "UnlockedBlueprint", 0);

                PlaySoundEffect(player);

                return successfulUnlocks;
            }

            return 0;
        }

        private bool SomeoneWillLearnBlueprint(BasePlayer player, ItemDefinition item)
        {
            if (player == null || item == null) return false;

            var targets = SelectSharePlayers(player);

            if (targets.Count > 0)
            {
                var counter = 0;

                foreach (var target in targets)
                {
                    if (target != null)
                    {
                        if (!target.blueprints.HasUnlocked(item))
                        {
                            counter++;
                        }
                    }
                }

                return counter > 0;
            }

            return false;
        }

        private List<BasePlayer> SelectSharePlayers(BasePlayer player)
        {
            var ids = new List<ulong>();

            var playerID = player.userID;

            if (config.ClansEnabled && Clans != null && InClan(playerID))
            {
                ids.AddRange(GetClanMembers(playerID));
            }

            if (config.FriendsEnabled && HasFriends(playerID))
            {
                ids.AddRange(GetFriends(playerID));
            }

            if (config.TeamsEnabled && InTeam(playerID))
            {
                ids.AddRange(GetTeamMembers(playerID));
            }

            return FindPlayers(ids, playerID);
        }

        #endregion

        #region Utility

        private BasePlayer FindPlayer(string playerName, BasePlayer player, string playerID)
        {
            var targets = FindPlayers(playerName);

            if (targets.Count <= 0)
            {
                player.ChatMessage(GetLangValue("Prefix", playerID) + GetLangValue("PlayerNotFound", playerID));

                return null;
            }

            if (targets.Count > 1)
            {
                player.ChatMessage(GetLangValue("Prefix", playerID) + GetLangValue("MultiplePlayersFound", playerID));

                return null;
            }

            return targets.First();
        }

        private List<BasePlayer> FindPlayers(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return null;

            return BasePlayer.allPlayerList.Where(p => p && p.UserIDString == playerName || p.displayName.Contains(playerName, CompareOptions.OrdinalIgnoreCase)).ToList();
        }

        private void PlaySoundEffect(BasePlayer player)
        {
            if (player == null) return;

            var soundEffect = new Effect("assets/prefabs/deployable/research table/effects/research-success.prefab", player.transform.position, Vector3.zero);

            if (soundEffect == null) return;

            EffectNetwork.Send(soundEffect, player.net.connection);
        }

        private List<BasePlayer> FindPlayers(List<ulong> ids, ulong playerId)
        {
            var targets = new List<BasePlayer>();

            foreach (var id in ids)
            {
                var target = RustCore.FindPlayerById(id);

                if (target != null && id != playerId && !targets.Contains(target))
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        private List<ItemDefinition> GetPlayerBlueprintItems(List<int> blueprintIds)
        {
            var blueprintItems = new List<ItemDefinition>();

            foreach (var id in blueprintIds)
            {
                var item = ItemManager.FindItemDefinition(id);

                if (item == null) continue;

                blueprintItems.Add(item);
            }

            return blueprintItems;
        }

        private List<int> ConvertItemsToIds(List<ItemDefinition> items)
        {
            var ids = new List<int>();

            foreach (var item in items)
            {
                if (item == null) continue;

                ids.Add(item.itemid);
            }

            return ids;
        }

        private bool BlueprintBlocked(ItemDefinition item) => config.BlockedItems.Contains(item.shortname);

        #endregion

        #region Chat Commands

        [ChatCommand("bs")]
        private void ToggleCommand(BasePlayer player, string command, string[] args)
        {
            var playerID = player.UserIDString;

            if (args.Length < 1)
            {
                player.ChatMessage(GetLangValue("Help", playerID));

                return;
            }

            switch (args[0].ToLower())
            {
                case "help":
                {
                    player.ChatMessage(GetLangValue("Help", playerID));

                    break;
                }

                case "toggle":
                {
                    if (!permission.UserHasPermission(playerID, TogglePermission))
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerID) + GetLangValue("NoPermission", playerID));

                        return;
                    }

                    if (!PlayerDataExists(playerID))
                    {
                        CreatePlayerData(playerID);
                    }

                    player.ChatMessage(GetLangValue("Prefix", playerID) + GetLangValue(SharingEnabled(playerID) ? "ToggleOff" : "ToggleOn", playerID));

                    storedData.Players[playerID].SharingEnabled = !storedData.Players[playerID].SharingEnabled;

                    SaveData();

                    break;
                }

                case "share":
                {
                    if (!config.ManualSharingEnabled)
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerID) + GetLangValue("ManualSharingDisabled", playerID));

                        return;
                    }

                    if (!permission.UserHasPermission(playerID, SharePermission))
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerID) + GetLangValue("NoPermission", playerID));

                        return;
                    }

                    if (args.Length != 2)
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerID) + GetLangValue("NoTarget", playerID));

                        return;
                    }

                    var target = FindPlayer(args[1], player, playerID);

                    if (target == null) return;

                    if (target == player)
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerID) + GetLangValue("TargetEqualsPlayer", playerID));

                        return;
                    }

                    ShareWithPlayer(player, target);

                    break;
                }

                default:
                {
                    player.ChatMessage(GetLangValue("Prefix", playerID) + GetLangValue("ArgumentsError", playerID));

                    break;
                }
            }
        }

        #endregion

        #region Configuration File

        private Configuration config;

        private class Configuration
        {
            [JsonProperty("Teams Sharing Enabled")]
            public bool TeamsEnabled = true;

            [JsonProperty("Clans Sharing Enabled")]
            public bool ClansEnabled = true;

            [JsonProperty("Friends Sharing Enabled")]
            public bool FriendsEnabled = true;

            [JsonProperty("Share Physical Blueprints")]
            public bool PhysicalSharingEnabled = true;

            [JsonProperty("Share Tech Tree Blueprints")]
            public bool TechTreeSharingEnabled = true;

            [JsonProperty("Allow Manual Sharing of Blueprints")]
            public bool ManualSharingEnabled = true;

            [JsonProperty("Share Blueprints on Join")]
            public bool ShareBlueprintsOnJoin = false;

            [JsonProperty("Clear Data File on Wipe")]
            public bool ClearDataOnWipe = true;

            [JsonProperty("Items Blocked from Sharing")]
            public List<string> BlockedItems = new List<string>();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating new configuration file");

            config = new Configuration();
        }

        protected override void SaveConfig()
        {
            PrintWarning("Configuration file has been saved");

            Config.WriteObject(config, true);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();

                if (config == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                PrintError($"Configuration file {Name}.json is invalid; Resetting configuration to default values");

                LoadDefaultConfig();
            }
        }

        #endregion

        #region Data File

        private class StoredData
        {
            public Dictionary<string, PlayerData> Players = new Dictionary<string, PlayerData>();
        }

        private class PlayerData
        {
            public bool SharingEnabled;
        }

        private void CreateData()
        {
            storedData = new StoredData();

            SaveData();
        }

        private void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            else
            {
                CreateData();
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        private bool PlayerDataExists(string playerID) => storedData.Players.ContainsKey(playerID);

        private void CreatePlayerData(string playerID)
        {
            storedData.Players.Add(playerID, new PlayerData
            {
                SharingEnabled = true
            });

            SaveData();
        }

        #endregion

        #region Localization File

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Prefix"] = "<color=#D85540>[Blueprint Share] </color>",
                ["ArgumentsError"] = "Error, incorrect arguments. Try /bs help.",
                ["Help"] = "<color=#D85540>Blueprint Share Help:</color>\n\n<color=#D85540>/bs toggle</color> - Toggles the sharing of blueprints.\n<color=#D85540>/bs share <player></color> - Shares your blueprints with other player.",
                ["ToggleOn"] = "You have <color=#00ff00>enabled</color> sharing blueprints.",
                ["ToggleOff"] = "You have <color=#ff0000>disabled</color> sharing blueprints.",
                ["NoPermission"] = "You don't have permission to use this command!",
                ["CannotShare"] = "You cannot share blueprints with this player because they aren't a friend or in the same clan or team!",
                ["NoTarget"] = "You didn't specify a player to share with!",
                ["TargetEqualsPlayer"] = "You cannot share blueprints with yourself!",
                ["PlayerNotFound"] = "Could not find a player with that name!",
                ["MultiplePlayersFound"] = "Found multiple players with a similar name: {0}",
                ["SharerSuccess"] = "You shared <color=#ffff00>{0}</color> blueprint(s) with <color=#ffff00>{1}</color>.",
                ["ShareReceieve"] = "<color=#ffff00>{0}</color> has shared <color=#ffff00>{1}</color> blueprint(s) with you.",
                ["NoBlueprintsToShare"] = "You don't have any new blueprints to share with <color=#ffff00>{0}</color>.",
                ["SharerLearntBlueprint"] = "You have learned the <color=#ffff00>{0}</color> blueprint and have shared it with <color=#ffff00>{1}</color> player(s)!",
                ["TargetLearntBlueprint"] = "<color=#ffff00>{0}</color> has shared the <color=#ffff00>{1}</color> blueprint with you!",
                ["BlueprintBlocked"] = "The server has blocked the <color=#ffff00>{0}</color> blueprint from being shared but you will still learn the blueprint.",
                ["ManualSharingDisabled"] = "Manual sharing of blueprints is disabled on this server.",
                ["TargetSharingDisabled"] = "Unable to share blueprints with <color=#ffff00>{0}</color> because they have disabled their sharing."
            }, this);
        }

        private string GetLangValue(string key, string id = null, params object[] args)
        {
            var msg = lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(msg, args) : msg;
        }

        #endregion

        #region API

        private bool SharingEnabled(string playerID) => !storedData.Players.ContainsKey(playerID) || storedData.Players[playerID].SharingEnabled;

        #endregion
    }
}