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
    [Info("Blueprint Share", "c_creep", "1.2.7")]
    [Description("Allows players to share researched blueprints with their friends, clan or team")]

    class BlueprintShare : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin Clans, Friends;

        private StoredData storedData;

        #endregion

        #region Localization

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
                ["TargetEqualsPlayer"] = "You cannot share blueprints with your self!",
                ["PlayerNotFound"] = "Couldn't find a player with that name!",
                ["MultiplePlayersFound"] = "Found multiple players with a similar name: {0}",
                ["SharerSuccess"] = "You shared <color=#ffff00>{0}</color> blueprints with <color=#ffff00>{1}</color>.",
                ["ShareReceieve"] = "<color=#ffff00>{0}</color> has shared <color=#ffff00>{1}</color> blueprints with you.",
                ["NoBlueprintsToShare"] = "You don't have any new blueprints to share with {0}",
                ["SharerLearntBlueprint"] = "You have learned the <color=#ffff00>{0}</color> blueprint and have shared it with <color=#ffff00>{1}</color> players!",
                ["TargetLearntBlueprint"] = "<color=#ffff00>{0}</color> has shared the <color=#ffff00>{1}</color> blueprint with you!",
                ["BlueprintBlocked"] = "The server has blocked the <color=#ffff00>{0}</color> blueprint from being shared but you will still learn the blueprint.",
                ["ManualSharingDisabled"] = "Manual sharing of blueprints has been disabled on this server.",
                ["TargetSharingDisabled"] = "Unable to share blueprints with <color=#ffff00>{0}</color> because they have disabled their sharing"
            }, this);
        }

        private string GetLangValue(string key, string id = null, params object[] args)
        {
            var msg = lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(msg, args) : msg;
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            LoadData();

            permission.RegisterPermission("blueprintshare.use", this);
            permission.RegisterPermission("blueprintshare.toggle", this);
            permission.RegisterPermission("blueprintshare.share", this);
        }

        private void OnNewSave(string filename)
        {
            if (config.ClearDataOnWipe)
            {
                CreateData();
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            var playerUID = player.UserIDString;

            if (!PlayerDataExists(playerUID))
            {
                CreateNewPlayerData(playerUID);
            }
        }

        private void OnItemAction(Item item, string action, BasePlayer player)
        {
            if (config.PhysicalSharingEnabled)
            {
                if (player != null)
                {
                    if (action == "study")
                    {
                        if (CanShareBlueprint(item.blueprintTargetDef, player))
                        {
                            item.Remove();
                        }
                    }
                }
            }
        }

        private void OnTechTreeNodeUnlocked(Workbench workbench, TechTreeData.NodeInstance node, BasePlayer player)
        {
            if (config.TechTreeSharingEnabled)
            {
                if (workbench != null)
                {
                    if (node != null)
                    {
                        if (player != null)
                        {
                            CanShareBlueprint(node.itemDef, player);
                        }
                    }
                }
            }
        }

        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (config.ShareBlueprintsOnJoin)
            {
                NextTick(() =>
                {
                    if (team != null && player != null)
                    {
                        if (team.members.Contains(player.userID))
                        {
                            ShareWithPlayer(team.GetLeader(), player);
                        }
                    }
                });
            }
        }

        #endregion

        #region Plugin Hooks

        private void OnFriendAdded(string playerID, string friendID)
        {
            if (config.ShareBlueprintsOnJoin)
            {
                var player = RustCore.FindPlayerByIdString(playerID);
                var friend = RustCore.FindPlayerByIdString(friendID);

                if (player != null && friend != null)
                {
                    ShareWithPlayer(player, friend);
                }
            }
        }

        #endregion

        #region Core

        private bool CanShareBlueprint(ItemDefinition item, BasePlayer player)
        {
            if (item == null || player == null) return false;
            if (!permission.UserHasPermission(player.UserIDString, "blueprintshare.use")) return false;

            var playerUID = player.UserIDString;

            if (config.BlockedItems.Contains(item.shortname))
            {
                player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("BlueprintBlocked", playerUID, item.displayName.translated));

                return false;
            }

            if (SharingEnabled(playerUID))
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

            var playerUID = player.UserIDString;
            var targetUID = target.UserIDString;

            if (SharingEnabled(targetUID))
            {
                if (SameTeam(player, target) || SameClan(playerUID, targetUID) || AreFriends(playerUID, targetUID))
                {
                    var learnedBlueprints = UnlockBlueprints(target, player.PersistantPlayerInfo.unlockedItems);

                    if (learnedBlueprints > 0)
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("SharerSuccess", playerUID, learnedBlueprints, target.displayName));

                        target.ChatMessage(GetLangValue("Prefix", targetUID) + GetLangValue("ShareReceieve", targetUID, player.displayName, learnedBlueprints));
                    }
                    else
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("NoBlueprintsToShare", playerUID, target.displayName));
                    }
                }
                else
                {
                    player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("CannotShare", playerUID));
                }
            }
            else
            {
                player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("TargetSharingDisabled", playerUID, target.displayName));
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
            var playersToShareWith = new List<BasePlayer>();

            var playerUID = player.userID;

            if (config.ClansEnabled && Clans != null && InClan(playerUID))
            {
                playersToShareWith.AddRange(GetClanMembers(playerUID));
            }

            if (config.FriendsEnabled && Friends != null && HasFriends(playerUID))
            {
                playersToShareWith.AddRange(GetFriends(playerUID));
            }

            if (config.TeamsEnabled && InTeam(playerUID))
            {
                playersToShareWith.AddRange(GetTeamMembers(playerUID));
            }

            return playersToShareWith;
        }

        #endregion

        #region Configuration

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
            public bool ManualSharingEnable = true;

            [JsonProperty("Share Blueprints On Join")]
            public bool ShareBlueprintsOnJoin = false;

            [JsonProperty("Clear Data File on Wipe")]
            public bool ClearDataOnWipe = true;

            [JsonProperty("Items Blocked From Sharing")]
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

        #region Friends

        private bool HasFriends(ulong playerUID)
        {
            if (Friends == null) return false;

            var friendsList = Friends?.Call<ulong[]>("GetFriends", playerUID);

            return friendsList != null && friendsList.Length != 0;
        }

        private List<BasePlayer> GetFriends(ulong playerUID)
        {
            var friendsList = new List<BasePlayer>();

            var friends = Friends?.Call<ulong[]>("GetFriends", playerUID);

            foreach (var friendUID in friends)
            {
                var friend = RustCore.FindPlayerById(friendUID);

                if (friend != null && friendUID != playerUID)
                {
                    friendsList.Add(friend);
                }
            }

            return friendsList;
        }

        private bool AreFriends(string playerUID, string targetUID) => Friends == null ? false : Friends.Call<bool>("AreFriends", playerUID, targetUID);

        #endregion

        #region Clan

        private bool InClan(ulong playerUID)
        {
            if (Clans == null) return false;

            var clanName = Clans?.Call<string>("GetClanOf", playerUID);

            return clanName != null;
        }

        private List<BasePlayer> GetClanMembers(ulong playerUID)
        {
            var membersList = new List<BasePlayer>();

            // Clans and Clans Reborn
            var clanMembers = Clans?.Call<List<string>>("GetClanMembers", playerUID);

            if (clanMembers != null)
            {
                return GetMemberObjects(clanMembers, playerUID);
            }

            // Rust:IO Clans
            var clanName = Clans?.Call<string>("GetClanOf", playerUID);

            if (!string.IsNullOrEmpty(clanName))
            {
                var clan = Clans?.Call<JObject>("GetClan", clanName);

                if (clan != null && clan is JObject)
                {
                    var members = clan.GetValue("members") as JArray;

                    if (members != null)
                    {
                        foreach (var member in members)
                        {
                            ulong clanMemberUID;

                            if (!ulong.TryParse(member.ToString(), out clanMemberUID)) continue;

                            var clanMember = RustCore.FindPlayerById(clanMemberUID);

                            if (clanMember != null && clanMemberUID != playerUID)
                            {
                                membersList.Add(clanMember);
                            }
                        }
                    }
                }
            }

            return membersList;
        }

        private List<BasePlayer> GetMemberObjects(List<string> members, ulong playerUID)
        {
            var membersList = new List<BasePlayer>();

            foreach (var member in members)
            {
                ulong clanMemberUID;

                if (!ulong.TryParse(member, out clanMemberUID)) continue;

                var clanMember = RustCore.FindPlayerById(clanMemberUID);

                if (clanMember != null && clanMemberUID != playerUID)
                {
                    membersList.Add(clanMember);
                }
            }

            return membersList;
        }

        private bool SameClan(string playerUID, string targetUID)
        {
            if (Clans == null) return false;

            // Clans and Clans Reborn
            var isClanMember = Clans?.Call("IsClanMember", playerUID, targetUID);

            if (isClanMember != null)
            {
                return (bool)isClanMember;
            }

            // Rust:IO Clans
            var playerClan = Clans?.Call("GetClanOf", playerUID);

            if (playerClan == null) return false;

            var targetClan = Clans?.Call("GetClanOf", targetUID);

            if (targetClan == null) return false;

            return (string)targetClan == (string)playerClan;
        }

        #endregion

        #region Team

        private bool InTeam(ulong playerUID)
        {
            var player = RustCore.FindPlayerById(playerUID);

            return player.currentTeam != 0;
        }

        private List<BasePlayer> GetTeamMembers(ulong playerUID)
        {
            var membersList = new List<BasePlayer>();

            var player = RustCore.FindPlayerById(playerUID);

            var teamMembers = player.Team.members;

            foreach (var teamMemberUID in teamMembers)
            {
                var teamMember = RustCore.FindPlayerById(teamMemberUID);

                if (teamMember != null && teamMemberUID != playerUID)
                {
                    membersList.Add(teamMember);
                }
            }

            return membersList;
        }

        private bool SameTeam(BasePlayer player, BasePlayer target) => player.currentTeam == target.currentTeam;

        #endregion

        #region Utility

        private BasePlayer FindPlayer(string playerName, BasePlayer player, string playerUID)
        {
            var targets = FindPlayers(playerName);

            if (targets.Count <= 0)
            {
                player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("PlayerNotFound", playerUID));

                return null;
            }

            if (targets.Count > 1)
            {
                player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("MultiplePlayersFound", playerUID));

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
            if (player != null)
            {
                var soundEffect = new Effect("assets/prefabs/deployable/research table/effects/research-success.prefab", player.transform.position, Vector3.zero);

                if (soundEffect != null)
                {
                    EffectNetwork.Send(soundEffect, player.net.connection);
                }
            }
        }

        #endregion

        #region Data

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

        private bool PlayerDataExists(string playerUID) => storedData.Players.ContainsKey(playerUID);

        private void CreateNewPlayerData(string playerUID)
        {
            storedData.Players.Add(playerUID, new PlayerData
            {
                SharingEnabled = true
            });

            SaveData();
        }

        #endregion

        #region Chat Commands

        [ChatCommand("bs")]
        private void ToggleCommand(BasePlayer player, string command, string[] args)
        {
            var playerUID = player.UserIDString;

            if (args.Length < 1)
            {
                player.ChatMessage(GetLangValue("Help", playerUID));

                return;
            }

            switch (args[0].ToLower())
            {
                case "help":
                {
                    player.ChatMessage(GetLangValue("Help", playerUID));

                    break;
                }
                case "toggle":
                {
                    if (permission.UserHasPermission(playerUID, "blueprintshare.toggle"))
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue(SharingEnabled(playerUID) ? "ToggleOff" : "ToggleOn", playerUID));

                        if (PlayerDataExists(playerUID))
                        {
                            storedData.Players[playerUID].SharingEnabled = !storedData.Players[playerUID].SharingEnabled;

                            SaveData();
                        }
                    }
                    else
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("NoPermission", playerUID));
                    }

                    break;
                }
                case "share":
                {
                    if (config.ManualSharingEnable)
                    {
                        if (permission.UserHasPermission(playerUID, "blueprintshare.share"))
                        {
                            if (args.Length == 2)
                            {
                                var target = FindPlayer(args[1], player, playerUID);

                                if (target == null) return;

                                if (target == player)
                                {
                                    player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("TargetEqualsPlayer", playerUID));

                                    return;
                                }

                                ShareWithPlayer(player, target);
                            }
                            else
                            {
                                player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("NoTarget", playerUID));

                                return;
                            }
                        }
                        else
                        {
                            player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("NoPermission", playerUID));

                            return;
                        }
                    }
                    else
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("ManualSharingDisabled", playerUID));

                        return;
                    }

                    break;
                }
                default:
                {
                    player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("ArgumentsError", playerUID));

                    break;
                }
            }
        }

        #endregion

        #region API

        private bool SharingEnabled(string playerUID) => storedData.Players.ContainsKey(playerUID) ? storedData.Players[playerUID].SharingEnabled : true;

        #endregion
    }
}