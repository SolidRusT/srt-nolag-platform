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
    [Info("Blueprint Share", "c_creep", "1.2.6")]
    [Description("Allows players to share researched blueprints with their friends, clan or team")]

    class BlueprintShare : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin Clans, ClansReborn, Friends;

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
                ["NoTarget"] = "You didn't specifiy a player to share with!",
                ["TargetEqualsPlayer"] = "You cannot share blueprints with your self!",
                ["PlayerNotFound"] = "Couldn't find a player with that name!",
                ["MultiplePlayersFound"] = "Found multiple players with a similar name: {0}",
                ["SharerSuccess"] = "You shared <color=#ffff00>{0}</color> blueprints with <color=#ffff00>{1}</color>.",
                ["ShareReceieve"] = "<color=#ffff00>{0}</color> has shared <color=#ffff00>{1}</color> blueprints with you.",
                ["NoBlueprintsToShare"] = "You don't have any new blueprints to share with {0}",
                ["NoOneLearntBlueprint"] = "The blueprint was not shared because no one learn the blueprint.",
                ["LearntBlueprintsIsNull"] = "The list of learnt blueprints is null.",
                ["SharerLearntBlueprint"] = "You have learned the <color=#ffff00>{0}</color> blueprint and have shared it with <color=#ffff00>{1}</color> players!",
                ["RecipientLearntBlueprint"] = "<color=#ffff00>{0}</color> has shared the <color=#ffff00>{1}</color> blueprint with you!",
                ["BlueprintBlocked"] = "The server has blocked the <color=#ffff00>{0}</color> blueprint from being shared but you will still learn the blueprint.",
                ["ManualSharingDisabled"] = "Manual sharing of blueprints has been disabled on this server."
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
                        if (CanShareBlueprint(item.blueprintTargetDef.shortname, item.blueprintTargetDef.displayName.translated, player))
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
                            CanShareBlueprint(node.itemDef.shortname, node.itemDef.displayName.translated, player);
                        }
                    }
                }
            }
        }

        #endregion

        #region General Methods

        private bool CanShareBlueprint(string itemShortName, string itemName, BasePlayer player)
        {
            if (player == null) return false;
            if (string.IsNullOrEmpty(itemShortName)) return false;
            if (!permission.UserHasPermission(player.UserIDString, "blueprintshare.use")) return false;

            var playerUID = player.UserIDString;

            if (config.BlockedItems.Contains(itemShortName))
            {
                player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("BlueprintBlocked", playerUID));

                return false;
            }

            if (SharingEnabled(playerUID))
            {
                if (InTeam(player.userID) || InClan(player.userID) || HasFriends(player.userID))
                {
                    if (SomeoneWillLearnBlueprint(player, itemShortName))
                    {
                        ShareWithPlayers(player, itemShortName, itemName);
                        HandleAdditionalBlueprints(player, itemShortName);

                        return true;
                    }
                    else
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("NoOneLearntBlueprint", playerUID));
                    }
                }
            }

            if (config.ManualSharingEnable)
            {
                TryInsertBlueprint(player, itemShortName);
            }

            return false;
        }

        private void HandleAdditionalBlueprints(BasePlayer player, string itemShortName)
        {
            var additionalBlueprints = GetItemDefinition(itemShortName).Blueprint.additionalUnlocks;

            if (additionalBlueprints.Count > 0)
            {
                foreach (var blueprint in additionalBlueprints)
                {
                    var additionalItemShortName = blueprint.shortname;

                    if (!string.IsNullOrEmpty(additionalItemShortName))
                    {
                        if (SomeoneWillLearnBlueprint(player, additionalItemShortName))
                        {
                            ShareWithPlayers(player, additionalItemShortName, blueprint.displayName.translated);
                        }
                    }
                }
            }
        }

        private void ShareWithPlayers(BasePlayer sharer, string itemShortName, string itemName)
        {
            if (sharer == null || string.IsNullOrEmpty(itemShortName)) return;

            var recipients = SelectSharePlayers(sharer);

            var successfulUnlocks = 0;

            foreach (var recipient in recipients)
            {
                if (recipient != null)
                {
                    if (UnlockBlueprint(recipient, itemShortName))
                    {
                        recipient.ChatMessage(GetLangValue("Prefix", sharer.UserIDString) + GetLangValue("RecipientLearntBlueprint", recipient.UserIDString, sharer.displayName, itemName));

                        successfulUnlocks++;
                    }
                }
            }

            sharer.ChatMessage(GetLangValue("Prefix", sharer.UserIDString) + GetLangValue("SharerLearntBlueprint", sharer.UserIDString, itemName, successfulUnlocks));
        }

        private void ShareWithPlayer(BasePlayer sharer, BasePlayer recipient)
        {
            if (sharer == null || recipient == null) return;

            var sharerUID = sharer.UserIDString;
            var recipientUID = recipient.UserIDString;

            if (SameTeam(sharer, recipient) || SameClan(sharerUID, recipientUID) || AreFriends(sharerUID, recipientUID))
            {
                var itemShortNames = GetLearntBlueprints(sharerUID);

                if (itemShortNames != null)
                {
                    if (itemShortNames.Count > 0)
                    {
                        var learnedBlueprints = 0;

                        foreach (var itemShortName in itemShortNames)
                        {
                            if (string.IsNullOrEmpty(itemShortName)) return;

                            if (UnlockBlueprint(recipient, itemShortName))
                            {
                                learnedBlueprints++;
                            }
                        }

                        if (learnedBlueprints > 0)
                        {
                            sharer.ChatMessage(GetLangValue("Prefix", sharerUID) + GetLangValue("SharerSuccess", sharerUID, learnedBlueprints, recipient.displayName));

                            recipient.ChatMessage(GetLangValue("Prefix", recipientUID) + GetLangValue("ShareReceieve", recipientUID, sharer.displayName, learnedBlueprints));
                        }
                        else
                        {
                            sharer.ChatMessage(GetLangValue("Prefix", sharerUID) + GetLangValue("NoBlueprintsToShare", sharerUID, recipient.displayName));
                        }
                    }
                    else
                    {
                        sharer.ChatMessage(GetLangValue("Prefix", sharerUID) + GetLangValue("NoBlueprintsToShare", sharerUID, recipient.displayName));
                    }
                }
                else
                {
                    sharer.ChatMessage(GetLangValue("Prefix", sharerUID) + GetLangValue("LearntBlueprintsIsNull", sharerUID));
                }
            }
            else
            {
                sharer.ChatMessage(GetLangValue("Prefix", sharerUID) + GetLangValue("CannotShare", sharerUID));
            }
        }

        private bool UnlockBlueprint(BasePlayer player, string itemShortName)
        {
            if (player == null || string.IsNullOrEmpty(itemShortName)) return false;

            var playerBlueprints = player.blueprints;

            if (playerBlueprints == null) return false;

            var itemDefinition = GetItemDefinition(itemShortName);

            if (itemDefinition == null) return false;

            if (playerBlueprints.HasUnlocked(itemDefinition)) return false;

            var soundEffect = new Effect("assets/prefabs/deployable/research table/effects/research-success.prefab", player.transform.position, Vector3.zero);

            if (soundEffect != null)
            {
                EffectNetwork.Send(soundEffect, player.net.connection);
            }

            playerBlueprints.Unlock(itemDefinition);

            if (config.ManualSharingEnable)
            {
                TryInsertBlueprint(player, itemShortName);
            }

            return true;
        }

        private bool SomeoneWillLearnBlueprint(BasePlayer sharer, string itemShortName)
        {
            if (sharer == null || string.IsNullOrEmpty(itemShortName)) return false;

            var players = SelectSharePlayers(sharer);

            if (players.Count > 0)
            {
                var blueprintItem = GetItemDefinition(itemShortName);

                if (blueprintItem != null)
                {
                    var counter = 0;

                    foreach (var player in players)
                    {
                        if (player != null)
                        {
                            if (!player.blueprints.HasUnlocked(blueprintItem))
                            {
                                counter++;
                            }
                        }
                    }

                    return counter > 0;
                }
            }

            return false;
        }

        private List<BasePlayer> SelectSharePlayers(BasePlayer player)
        {
            var playersToShareWith = new List<BasePlayer>();

            var playerUID = player.userID;

            if (config.ClansEnabled && (Clans != null || ClansReborn != null) && InClan(playerUID))
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

        private List<string> GetLearntBlueprints(string playerUID)
        {
            if (PlayerDataExists(playerUID))
            {
                return storedData.Players[playerUID].LearntBlueprints;
            }
            else
            {
                CreateNewPlayerData(playerUID);

                if (storedData.Players[playerUID].LearntBlueprints == null)
                {
                    return storedData.Players[playerUID].LearntBlueprints = new List<string>();
                }
                else
                {
                    return storedData.Players[playerUID].LearntBlueprints;
                }
            }
        }

        private void TryInsertBlueprint(BasePlayer player, string itemShortName)
        {
            var playerUID = player.UserIDString;

            if (!PlayerDataExists(playerUID))
            {
                CreateNewPlayerData(playerUID);
            }

            InsertBlueprint(playerUID, itemShortName);
        }

        private void InsertBlueprint(string playerUID, string itemShortName)
        {
            if (string.IsNullOrEmpty(playerUID) || string.IsNullOrEmpty(itemShortName)) return;

            if (!storedData.Players[playerUID].LearntBlueprints.Contains(itemShortName))
            {
                storedData.Players[playerUID].LearntBlueprints.Add(itemShortName);

                SaveData();
            }
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
                PrintError($"Configuration file {Name}.json is invalid; Resetting config to default values");

                LoadDefaultConfig();
            }
        }

        #endregion

        #region Friends Methods

        private bool HasFriends(ulong playerUID)
        {
            if (Friends == null) return false;

            var friendsList = Friends.Call<ulong[]>("GetFriends", playerUID);

            return friendsList != null && friendsList.Length != 0;
        }

        private List<BasePlayer> GetFriends(ulong playerUID)
        {
            var friendsList = new List<BasePlayer>();

            var friends = this.Friends.Call<ulong[]>("GetFriends", playerUID);

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

        private bool AreFriends(string sharerUID, string playerUID) => Friends == null ? false : Friends.Call<bool>("AreFriends", sharerUID, playerUID);

        #endregion

        #region Clan Methods

        private bool InClan(ulong playerUID)
        {
            if (ClansReborn == null && Clans == null) return false;

            var clanName = Clans?.Call<string>("GetClanOf", playerUID);

            return clanName != null;
        }

        private List<BasePlayer> GetClanMembers(ulong playerUID)
        {
            var membersList = new List<BasePlayer>();

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

        private bool SameClan(string sharerUID, string playerUID) => ClansReborn == null && Clans == null ? false : (bool)Clans?.Call<bool>("IsClanMember", sharerUID, playerUID);

        #endregion

        #region Team Methods

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

        private bool SameTeam(BasePlayer sharer, BasePlayer player) => sharer.currentTeam == player.currentTeam;

        #endregion

        #region Utility Methods

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

        private ItemDefinition GetItemDefinition(string itemShortName)
        {
            if (string.IsNullOrEmpty(itemShortName)) return null;

            var itemDefinition = ItemManager.FindItemDefinition(itemShortName.ToLower());

            return itemDefinition;
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

            public List<string> LearntBlueprints;
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
                SharingEnabled = true,
                LearntBlueprints = new List<string>()
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
                        }
                    }
                    else
                    {
                        player.ChatMessage(GetLangValue("Prefix", playerUID) + GetLangValue("ManualSharingDisabled", playerUID));
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