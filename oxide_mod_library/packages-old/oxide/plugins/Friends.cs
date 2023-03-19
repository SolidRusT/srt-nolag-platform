using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

// TODO: Add cooldown option for adding friends
// TODO: Add ability for varying max number of friends (based on permissions?)
// TODO: Split 'friend' command into 'addfriend', 'removefriend', and 'friends'
// TODO: Create separate plugin for friend notifications (togglable)
// TODO: Create separate plugin for private chat between friends only

namespace Oxide.Plugins
{
    [Info("Friends", "Wulf", "3.1.2")]
    [Description("Friends system and API managing friend lists")]
    internal class Friends : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Friend list cache time (0 to disable)")]
            public int CacheTime = 0;

            [JsonProperty("Maximum number of friends (0 to disable)")]
            public int MaxFriends = 30;

            [JsonProperty("Use permission system")]
            public bool UsePermissions = false;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

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

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
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
            LogWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Stored Data

        private readonly Dictionary<string, HashSet<string>> reverseData = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, PlayerData> friendsData;

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);

        private class PlayerData
        {
            public string Name { get; set; } = string.Empty;
            public HashSet<string> Friends { get; set; } = new HashSet<string>();
            public Dictionary<string, int> Cached { get; set; } = new Dictionary<string, int>();

            public bool IsCached(string playerId)
            {
                int time;
                if (!Cached.TryGetValue(playerId, out time))
                {
                    return false;
                }

                if (time >= (int)DateTime.UtcNow.Subtract(Epoch).TotalSeconds)
                {
                    return true;
                }

                Cached.Remove(playerId);
                return false;
            }
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, friendsData);
        }

        #endregion Stored Data

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AlreadyOnList"] = "{0} is already your friend",
                ["CannotAddSelf"] = "You cannot add yourself",
                ["CommandFriend"] = "friend",
                ["FriendAdded"] = "{0} is now your friend",
                ["FriendRemoved"] = "{0} was removed from your friend list",
                ["FriendList"] = "Friends {0}:\n{1}",
                ["FriendListFull"] = "Your friend list is full",
                ["NoFriends"] = "You do not have any friends",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NoPlayersFound"] = "No players found with name or ID '{0}'",
                ["NotOnFriendList"] = "{0} not found on your friend list",
                ["PlayerNotFound"] = "Player '{0}' was not found",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}",
                ["PlayersOnly"] = "Command '{0}' can only be used by players",
                ["UsageFriend"] = "Usage {0} <add|remove|list> <player name or id>"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string permUse = "friends.use";

        private void Init()
        {
            AddLocalizedCommand(nameof(CommandFriend));

            permission.RegisterPermission(permUse, this);

            try
            {
                friendsData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PlayerData>>(Name);
            }
            catch
            {
                friendsData = new Dictionary<string, PlayerData>();
            }

            foreach (KeyValuePair<string, PlayerData> data in friendsData)
            {
                foreach (string friendId in data.Value.Friends)
                {
                    AddFriendReverse(data.Key, friendId);
                }
            }
        }

        #endregion Initialization

        #region Add/Remove Friends

        private bool AddFriend(string playerId, string friendId)
        {
            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(friendId))

            {
                PlayerData playerData = GetPlayerData(playerId);
                if (playerData.Friends.Count >= config.MaxFriends || !playerData.Friends.Add(friendId))
                {
                    return false;
                }

                AddFriendReverse(playerId, friendId);
                SaveData();

                Interface.Oxide.CallHook("OnFriendAdded", playerId, friendId);
                return true;
            }

            return false;
        }

        private bool AddFriend(ulong playerId, ulong friendId)
        {
            return AddFriend(playerId.ToString(), friendId.ToString());
        }

        private void AddFriendReverse(string playerId, string friendId)
        {
            HashSet<string> friends;
            if (!reverseData.TryGetValue(friendId, out friends))
            {
                reverseData[friendId] = friends = new HashSet<string>();
            }

            friends.Add(playerId);
        }

        private bool RemoveFriend(string playerId, string friendId)
        {
            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(friendId))
            {
                PlayerData playerData = GetPlayerData(playerId);
                if (!playerData.Friends.Remove(friendId))
                {
                    return false;
                }

                HashSet<string> friends;
                if (reverseData.TryGetValue(friendId, out friends))
                {
                    friends.Remove(playerId);
                }

                if (config.CacheTime > 0)
                {
                    playerData.Cached[friendId] = (int)DateTime.UtcNow.Subtract(Epoch).TotalSeconds + config.CacheTime;
                }

                SaveData();

                Interface.Oxide.CallHook("OnFriendRemoved", playerId, friendId);
                return true;
            }

            return false;
        }

        private bool RemoveFriend(ulong playerId, ulong friendId)
        {
            return RemoveFriend(playerId.ToString(), friendId.ToString());
        }

        #endregion Add/Remove Friends

        #region Friend Checks

        private bool HasFriend(string playerId, string friendId)
        {
            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(friendId))
            {
                return GetPlayerData(playerId).Friends.Contains(friendId);
            }

            return false;
        }

        private bool HasFriend(ulong playerId, ulong friendId)
        {
            return HasFriend(playerId.ToString(), friendId.ToString());
        }

        private bool HadFriend(string playerId, string friendId)
        {
            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(friendId))
            {
                PlayerData playerData = GetPlayerData(playerId);
                return playerData.Friends.Contains(friendId) || playerData.IsCached(friendId);
            }

            return false;
        }

        private bool HadFriend(ulong playerId, ulong friendId)
        {
            return HadFriend(playerId.ToString(), friendId.ToString());
        }

        private bool AreFriends(string playerId, string friendId)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(friendId))
            {
                return false;
            }

            return GetPlayerData(playerId).Friends.Contains(friendId) && GetPlayerData(friendId).Friends.Contains(playerId);
        }

        private bool AreFriends(ulong playerId, ulong friendId)
        {
            return AreFriends(playerId.ToString(), friendId.ToString());
        }

        private bool WereFriends(string playerId, string friendId)
        {
            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(friendId))
            {
                PlayerData playerData = GetPlayerData(playerId);
                PlayerData friendData = GetPlayerData(friendId);
                return (playerData.Friends.Contains(friendId) || playerData.IsCached(friendId)) && (friendData.Friends.Contains(playerId) || friendData.IsCached(playerId));
            }

            return false;
        }

        private bool WereFriends(ulong playerId, ulong friendId)
        {
            return WereFriends(playerId.ToString(), friendId.ToString());
        }

        private bool IsFriend(string playerId, string friendId)
        {
            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(friendId))
            {
                return GetPlayerData(friendId).Friends.Contains(playerId);
            }

            return false;
        }

        private bool IsFriend(ulong playerId, ulong friendId)
        {
            return IsFriend(playerId.ToString(), friendId.ToString());
        }

        private bool WasFriend(string playerId, string friendId)
        {
            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(friendId))
            {
                PlayerData playerData = GetPlayerData(friendId);
                return playerData.Friends.Contains(playerId) || playerData.IsCached(playerId);
            }

            return false;
        }

        private bool WasFriend(ulong playerId, ulong friendId)
        {
            return WasFriend(playerId.ToString(), friendId.ToString());
        }

        private int GetMaxFriends()
        {
            return config.MaxFriends;
        }

        #endregion Friend Checks

        #region Friend Lists

        private string[] GetFriends(string playerId)
        {
            return GetPlayerData(playerId).Friends.ToArray();
        }

        private ulong[] GetFriends(ulong playerId)
        {
            return GetPlayerData(playerId.ToString()).Friends.Select(ulong.Parse).ToArray();
        }

        private string[] GetFriendList(string playerId)
        {
            PlayerData playerData = GetPlayerData(playerId);
            List<string> players = new List<string>();

            foreach (string friendId in playerData.Friends)
            {
                players.Add(GetPlayerData(friendId).Name);
            }

            return players.ToArray();
        }

        private string[] GetFriendList(ulong playerId)
        {
            return GetFriendList(playerId.ToString());
        }

        private string[] IsFriendOf(string playerId)
        {
            HashSet<string> friends;
            return reverseData.TryGetValue(playerId, out friends) ? friends.ToArray() : new string[0];
        }

        private ulong[] IsFriendOf(ulong playerId)
        {
            return IsFriendOf(playerId.ToString()).Select(ulong.Parse).ToArray();
        }

        #endregion Friend Lists

        #region Commands

        private void CommandFriend(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                Message(player, "PlayersOnly", command);
                return;
            }

            if (config.UsePermissions && player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length <= 0 || args.Length == 1 && !args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                Message(player, "UsageFriend", command);
                return;
            }

            switch (args[0].ToLower())
            {
                case "list":
                    string[] friendList = GetFriendList(player.Id);
                    if (friendList.Length > 0)
                    {
                        Message(player, "FriendList", $"{friendList.Length}/{config.MaxFriends}", string.Join(", ", friendList));
                    }
                    else
                    {
                        Message(player, "NoFriends");
                    }

                    return;

                case "+":
                case "add":
                    IPlayer target = FindPlayer(args[1], player);
                    if (target == null)
                    {
                        return;
                    }

                    if (player == target)
                    {
                        Message(player, "CannotAddSelf");
                        return;
                    }

                    PlayerData playerData = GetPlayerData(player.Id);
                    if (playerData.Friends.Count >= config.MaxFriends)
                    {
                        Message(player, "FriendListFull");
                        return;
                    }

                    if (playerData.Friends.Contains(target.Id))
                    {
                        Message(player, "AlreadyOnList", target.Name);
                        return;
                    }

                    AddFriend(player.Id, target.Id);
                    Message(player, "FriendAdded", target.Name);
                    return;

                case "-":
                case "remove":
                    string friend = FindFriend(args[1]);
                    if (string.IsNullOrEmpty(friend))
                    {
                        Message(player, "NotOnFriendList", args[1]);
                        return;
                    }

                    bool removed = RemoveFriend(player.Id, friend.ToString());
                    Message(player, removed ? "FriendRemoved" : "NotOnFriendList", args[1]);
                    return;
            }
        }

        private void SendHelpText(object obj)
        {
            IPlayer player = players.FindPlayerByObj(obj);
            if (player != null)
            {
                Message(player, "HelpText");
            }
        }

        #endregion Commands

        #region Helpers

        private string FindFriend(string nameOrId)
        {
            if (!string.IsNullOrEmpty(nameOrId))
            {
                foreach (KeyValuePair<string, PlayerData> playerData in friendsData)
                {
                    if (playerData.Key.Equals(nameOrId) || playerData.Value.Name.IndexOf(nameOrId, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return playerData.Key;
                    }
                }
            }

            return string.Empty;
        }

        private PlayerData GetPlayerData(string playerId)
        {
            PlayerData playerData;
            if (!friendsData.TryGetValue(playerId, out playerData))
            {
                friendsData[playerId] = playerData = new PlayerData();
            }

            IPlayer player = players.FindPlayerById(playerId);
            if (player != null)
            {
                playerData.Name = player.Name;
            }

            return playerData;
        }

        private IPlayer FindPlayer(string playerNameOrId, IPlayer player)
        {
            IPlayer[] foundPlayers = players.FindPlayers(playerNameOrId).ToArray();
            if (foundPlayers.Length > 1)
            {
                Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).Take(10).ToArray()).Truncate(60));
                return null;
            }

            IPlayer target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null)
            {
                Message(player, "NoPlayersFound", playerNameOrId);
                return null;
            }

            return target;
        }

        private void AddLocalizedCommand(string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages)
                {
                    if (message.Key.Equals(command))
                    {
                        if (!string.IsNullOrEmpty(message.Value))
                        {
                            AddCovalenceCommand(message.Value, command);
                        }
                    }
                }
            }
        }

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        private void Message(IPlayer player, string textOrLang, params object[] args)
        {
            if (player.IsConnected)
            {
                string message = GetLang(textOrLang, player.Id, args);
                player.Reply(message != textOrLang ? message : textOrLang);
            }
        }

        #endregion Helpers
    }
}
