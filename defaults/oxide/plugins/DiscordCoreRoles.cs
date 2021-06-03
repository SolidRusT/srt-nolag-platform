using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord;
using Oxide.Ext.Discord.DiscordEvents;
using Oxide.Ext.Discord.DiscordObjects;

namespace Oxide.Plugins
{
    [Info("Discord Core Roles", "MJSU", "1.3.7")]
    [Description("Syncs players oxide group with discord roles")]
    class DiscordCoreRoles : CovalencePlugin
    {
        #region Class Fields

        [PluginReference] private Plugin AntiSpamNames, Clans, DiscordCore;

        private PluginConfig _pluginConfig; //Plugin Config

        private readonly List<PlayerSync> _processIds = new List<PlayerSync>();
        private readonly Hash<string, string> _discordRoleLookup = new Hash<string, string>();

        private bool _init;

        private DiscordClient _client;
        private Guild _guild;
        
        private const string AccentColor = "#de8732";

        private enum DebugEnum
        {
            Message,
            None,
            Error,
            Warning,
            Info
        }

        private Timer _playerChecker;

        private enum Source
        {
            Server,
            [Obsolete]
            Umod, //Replaced by Server enum value
            Discord
        }

        #endregion

        #region Setup & Loading
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Chat] = $"[#BEBEBE][[{AccentColor}]{Title}[/#]] {{0}}[/#]",
                [LangKeys.ClanTag] = "[{0}] {1}",
                [LangKeys.ServerMessageOxideGroupAdded] = "{{player.name}} has been added to oxide group {{group.name}}",
                [LangKeys.ServerMessageOxideGroupRemoved] = "{{player.name}} has been removed to oxide group {{group.name}}",
                [LangKeys.ServerMessageDiscordRoleAdded] = "{{player.name}} has been added to discord role {{role.name}}",
                [LangKeys.ServerMessageDiscordRoleRemoved] = "{{player.name}} has been removed to discord role {{role.name}}",
                
                [LangKeys.DiscordMessageOxideGroupAdded] = "{{discord.name}} has been added to oxide group {{group.name}}",
                [LangKeys.DiscordMessageOxideGroupRemoved] = "{{discord.name}} has been removed to oxide group {{group.name}}",
                [LangKeys.DiscordMessageDiscordRoleAdded] = "{{discord.name}} has been added to discord role {{role.name}}",
                [LangKeys.DiscordMessageDiscordRoleRemoved] = "{{discord.name}} has been removed to discord role {{role.name}}",
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
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            config.SyncData = config.SyncData ?? new List<SyncData>
            {
                new SyncData
                {
                    Oxide = "Default",
                    Discord = "DiscordRoleNameOrId",
                    Source = Source.Server
                },
                new SyncData
                {
                    Oxide = "VIP",
                    Discord = "VIP",
                    Source = Source.Discord
                }
            };
            
            foreach (SyncData data in config.SyncData)
            {
                //Migrate from Umod enum value to Server enum value
                if (data.SourceOld.HasValue)
                {
                    Source old = data.SourceOld.Value;
                    data.Source = old == Source.Umod ? Source.Server : Source.Discord;
                    data.SourceOld = null;
                }
                
                //Add new field to old data
                if (data.Notifications == null)
                {
                    data.Notifications = new NotificationSettings();
                }

                data.Notifications.DiscordMessageChannelId = data.Notifications.DiscordMessageChannelId ?? string.Empty;
                data.Notifications.ServerMessageAddedOverride = data.Notifications.ServerMessageAddedOverride ?? string.Empty;
                data.Notifications.ServerMessageRemovedOverride = data.Notifications.ServerMessageRemovedOverride ?? string.Empty;
                data.Notifications.DiscordMessageAddedOverride = data.Notifications.DiscordMessageAddedOverride ?? string.Empty;
                data.Notifications.DiscordMessageRemovedOverride = data.Notifications.DiscordMessageRemovedOverride ?? string.Empty;
            }
            return config;
        }

        private void OnServerInitialized()
        {
            if (DiscordCore == null)
            {
                PrintError("Missing plugin dependency DiscordCore: https://umod.org/plugins/discord-core");
                return;
            }

            if(DiscordCore.Version < new VersionNumber(0, 17, 0))
            {
                PrintError("DiscordCore plugin must be version 0.17.0 or higher");
                return;
            }

            if (_pluginConfig.UseAntiSpamNames && AntiSpamNames == null)
            {
                PrintWarning("AntiSpamNames is enabled in the config but is not loaded. " +
                             "Please disable the setting in the config or load AntiSpamNames: https://umod.org/plugins/anti-spam-names");
            }
            
            OnDiscordCoreReady();
        }

        private void OnDiscordCoreReady()
        {
            if (!(DiscordCore?.Call<bool>("IsReady") ?? false))
            {
                return;
            }

            DiscordCore.Call("RegisterPluginForExtensionHooks", this);

            foreach (var data in _pluginConfig.SyncData.ToList())
            {
                bool remove = false;
                if (!permission.GroupExists(data.Oxide))
                {
                    PrintWarning($"Oxide group does not exist: '{data.Oxide}'. Please create the group or correct the name");
                    remove = true;
                }

                Role role = GetRole(data.Discord);
                if (role == null)
                {
                    PrintWarning($"Discord role name or id does not exist: '{data.Discord}'.\n" +
                                 "Please add the discord role or fix the role name/id.");
                    remove = true;
                }
                
                if (remove)
                {
                    _pluginConfig.SyncData.Remove(data);
                    continue;
                }
                
                data.Discord = role.id;

                _discordRoleLookup[role.id] = role.name;
                _discordRoleLookup[role.name] = role.id;
            }

            _client = GetDiscordClient();
            _guild = GetDiscordGuild();
            
            timer.In(5f, CheckAllPlayers);
        }

        private void CheckAllPlayers()
        {
            if (_init)
            {
                return;
            }
            
            List<string> users = DiscordCore.Call<List<string>>("GetAllUsers");
            foreach (string user in users)
            {
                _processIds.Add(new PlayerSync(user));
            }

            Debug(DebugEnum.Message, $"Starting sync for {_processIds.Count} linked players");

            if (_playerChecker == null)
            {
                _playerChecker = timer.Every(_pluginConfig.UpdateRate, ProcessNextStartupId);
            }

            _init = true;
        }

        private void ProcessNextStartupId()
        {
            if (_client == null || _guild == null)
            {
                Debug(DebugEnum.Info, "Waiting for discord core to come back online");
                return;
            }
            
            if (_processIds.Count == 0)
            {
                _playerChecker?.Destroy();
                _playerChecker = null;
                return;
            }

            PlayerSync id = _processIds[0];
            _processIds.RemoveAt(0);

            if (string.IsNullOrEmpty(id.DiscordId))
            {
                id.DiscordId = GetDiscordId(id.PlayerId);
            }
            
            Debug(DebugEnum.Info, $"Start processing: Player Id: {id.PlayerId} Discord Id: {id.DiscordId} Is Leaving: {id.IsLeaving}");

            IPlayer player = players.FindPlayerById(id.PlayerId);
            if (player != null)
            {
                if (id.IsLeaving)
                {
                    HandleUser(id.PlayerId, id.DiscordId, id.IsLeaving, null);
                }
                else
                {
                    if (string.IsNullOrEmpty(id.DiscordId))
                    {
                        return;
                    }

                    _guild.GetGuildMember(_client, id.DiscordId, member =>
                    {
                        HandleUser(id.PlayerId, id.DiscordId, id.IsLeaving, member);
                    });
                }
            }
        }
        #endregion

        #region Commands
        [Command("dcr.forcecheck")]
        private void HandleCommand(IPlayer player, string cmd, string[] args)
        {
            Debug(DebugEnum.Message, "Begin checking all players");
            CheckAllPlayers();
        }
        #endregion

        #region Oxide Hooks

        private void OnUserConnected(IPlayer player)
        {
            Debug(DebugEnum.Info, $"{nameof(OnUserConnected)} Added {player.Name}({player.Id}) to be processed");
            ProcessChange(player.Id);
        }

        private void OnUserGroupAdded(string id, string groupName)
        {
            Debug(DebugEnum.Info, $"{nameof(OnUserGroupAdded)} Added ({id}) to be processed because added to group {groupName}");
            ProcessChange(id);
        }

        private void OnUserGroupRemoved(string id, string groupName)
        {
            Debug(DebugEnum.Info, $"{nameof(OnUserGroupRemoved)} Added ({id}) to be processed because removed from group {groupName}");
            ProcessChange(id);
        }

        private void OnDiscordCoreJoin(IPlayer player)
        {
            Debug(DebugEnum.Info, $"{nameof(OnDiscordCoreJoin)} Added {player.Name}({player.Id}) to be processed");
            ProcessChange(player.Id);
        }

        private void OnDiscordCoreLeave(IPlayer player, string discordId)
        {
            Debug(DebugEnum.Info, $"{nameof(OnDiscordCoreLeave)} Added {player.Name}({player.Id})[{discordId}] to be processed");
            ProcessChange(player.Id, discordId, true);
        }

        private void Discord_MemberAdded(GuildMember member)
        {
            Debug(DebugEnum.Info, $"{nameof(Discord_MemberAdded)} Added {member.nick}({member.user.username}) to be processed");
            HandleDiscordChange(member.user.id, false);
        }

        private void Discord_MemberRemoved(GuildMember member)
        {
            if (member?.user == null)
            {
                return;
            }
        
            Debug(DebugEnum.Info, $"{nameof(Discord_MemberRemoved)} Added {member.nick}({member.user.username}) to be processed");
            HandleDiscordChange(member.user.id, true);
        }

        private void Discord_GuildMemberUpdate(GuildMemberUpdate update, GuildMember oldMember)
        {
            //Don't update if the nick and roles haven't changed
            if ((update.nick == null || update.nick == oldMember.nick)
                && update.roles.All(r => oldMember.roles.Contains(r))
                && oldMember.roles.All(r => update.roles.Contains(r)))
            {
                return;
            }
            
            Debug(DebugEnum.Info, $"{nameof(Discord_GuildMemberUpdate)} Added {update.nick}({update.user.username}) to be processed");
            HandleDiscordChange(oldMember.user.id, false);
        }

        public void HandleDiscordChange(string discordId, bool isLeaving)
        {
            string playerId = GetPlayerId(discordId);
            if (string.IsNullOrEmpty(playerId))
            {
                return;
            }

            ProcessChange(playerId, discordId, isLeaving);
        }

        private void ProcessChange(string playerId, string discordId = null, bool isLeaving = false)
        {
            PlayerSync sync = _processIds.FirstOrDefault(p => p.PlayerId == playerId);
            if (sync != null)
            {
                _processIds.Remove(sync);
                _processIds.Insert(0, sync);
            }
            else
            {
                _processIds.Insert(0, new PlayerSync(playerId, discordId, isLeaving));
            }

            if (_playerChecker == null)
            {
                _playerChecker = timer.Every(_pluginConfig.UpdateRate, ProcessNextStartupId);
            }
        }
        #endregion

        #region Role Handling
        public void HandleUser(string playerId, string discordId, bool isLeaving, GuildMember member)
        {
            try
            {
                UnsubscribeAll();
                if (string.IsNullOrEmpty(discordId))
                {
                    return;
                }

                IPlayer player = covalence.Players.FindPlayerById(playerId);

                Debug(DebugEnum.Info, $"Checking player {player?.Name} ({playerId}) Discord: {discordId}");
                HandleOxideGroups(playerId, discordId, isLeaving, member);
                HandleDiscordRoles(playerId, discordId, isLeaving, member);
                HandleUserNick(playerId, isLeaving);
            }
            finally
            {
                SubscribeAll();
            }
        }

        public void HandleOxideGroups(string playerId, string discordId, bool isLeaving, GuildMember member)
        {
            if (DiscordCore == null)
            {
                PrintError("Missing plugin dependency DiscordCore: https://umod.org/plugins/discord-core");
                return;
            }

            Debug(DebugEnum.Info, $"Processing Oxide for {players.FindPlayerById(playerId)?.Name}({playerId}) Discord ID: {discordId} Is Leaving {isLeaving}");

            IPlayer player = covalence.Players.FindPlayerById(playerId);
            foreach (IGrouping<string, SyncData> data in _pluginConfig.SyncData.Where(s => s.Source == Source.Server).GroupBy(s => s.Discord))
            {
                bool isInGroup = !isLeaving && data.Any(d => permission.UserHasGroup(playerId, d.Oxide));
                bool isInDiscord = DiscordUserHasRole(playerId, data.Key, member);
                if (isInDiscord == isInGroup)
                {
                    Debug(DebugEnum.Info, $"{player?.Name} skipping Server Sync: [{string.Join(", ", data.Select(d => d.Oxide).ToArray())}] -> {GetRoleDisplayName(data.Key)} {(isInGroup ? "Already Synced" : "Not in group")}");
                    continue;
                }

                string hook = isInGroup ? "AddRoleToUser" : "RemoveRoleFromUser";
                DiscordCore.Call(hook, discordId, data.Key);

                SyncData sync = data.FirstOrDefault(d => permission.UserHasGroup(playerId, d.Oxide)) ?? data.FirstOrDefault();
                SendSyncNotification(playerId, member, sync, isInGroup);
                if (isInGroup)
                {
                    Debug(DebugEnum.Message, $"Adding player {player?.Name}({playerId}) to discord role {GetRoleDisplayName(data.Key)}");
                }
                else
                {
                    Debug(DebugEnum.Message, $"Removing player {player?.Name}({playerId}) from discord role {GetRoleDisplayName(data.Key)}");
                }
            }
        }

        public void HandleDiscordRoles(string playerId, string discordId, bool isLeaving, GuildMember member)
        {
            if (DiscordCore == null)
            {
                PrintError("Missing plugin dependency DiscordCore: https://umod.org/plugins/discord-core");
                return;
            }

            if (string.IsNullOrEmpty(playerId))
            {
                return;
            }

            Debug(DebugEnum.Info, $"Processing Discord for {players.FindPlayerById(playerId)?.Name} ({playerId}) Is Leaving {isLeaving}");

            IPlayer player = covalence.Players.FindPlayerById(playerId);
            foreach (IGrouping<string, SyncData> data in _pluginConfig.SyncData.Where(s => s.Source == Source.Discord).GroupBy(s => s.Oxide))
            {
                bool isInGroup = permission.UserHasGroup(playerId, data.Key);
                bool isInDiscord = false;
                SyncData sync = null;
                if (!isLeaving)
                {
                    foreach (SyncData syncData in data)
                    {
                        if (DiscordUserHasRole(playerId, syncData.Discord, member))
                        {
                            sync = syncData;
                            isInDiscord = true;
                            break;
                        }
                    }
                }

                if (isInDiscord == isInGroup)
                {
                    Debug(DebugEnum.Info, $"{player?.Name} skipping Discord Sync: [{string.Join(", ", data.Select(d => GetRoleDisplayName(d.Discord)).ToArray())}] -> {data.Key} {(isInDiscord ? "Already Synced" : "Doesn't have role")}");
                    continue;
                }

                sync = sync ?? data.FirstOrDefault();
                SendSyncNotification(playerId, member, sync, isInDiscord);
                if (isInDiscord)
                {
                    Debug(DebugEnum.Message, $"Adding player {player?.Name}({playerId}) to oxide group {data.Key}");
                    permission.AddUserGroup(playerId, data.Key);
                }
                else
                {
                    Debug(DebugEnum.Message, $"Removing player {player?.Name}({playerId}) from oxide group {data.Key}");
                    permission.RemoveUserGroup(playerId, data.Key);
                }
            }
        }

        public void HandleUserNick(string playerId, bool isLeaving)
        {
            if (!_pluginConfig.SyncNicknames || isLeaving)
            {
                return;
            }
            
            IPlayer player = covalence.Players.FindPlayerById(playerId);
            if (player == null)
            {
                Debug(DebugEnum.Warning, $"Failed to sync player id '{playerId}' as they don't have an IPlayer");
                return;
            }

            Debug(DebugEnum.Info, $"Setting {player.Name} as their discord nickname");

            string playerName = GetPlayerName(player);
            Debug(DebugEnum.Info, $"Setting {playerName} as their discord nickname");
            UpdateUserNick(player.Id, playerName);
        }

        private string GetPlayerName(IPlayer player)
        {
            string playerName = player.Name;
            if (_pluginConfig.UseAntiSpamNames && AntiSpamNames != null && AntiSpamNames.IsLoaded)
            {
                playerName = AntiSpamNames.Call<string>("GetClearName", player);
                if (string.IsNullOrEmpty(playerName))
                {
                    Debug(DebugEnum.Warning, $"AntiSpamNames returned an empty string for '{player.Name}'");
                    playerName = player.Name;
                }
                else if (!playerName.Equals(player.Name))
                {
                    Debug(DebugEnum.Info, $"Nickname '{player.Name}' was filtered by AntiSpamNames: '{playerName}'");
                }
            }

            if (_pluginConfig.AddClanTag)
            {
                string tag = Clans?.Call<string>("GetClanOf", player.Id);
                if (!string.IsNullOrEmpty(tag))
                {
                    playerName = Lang(LangKeys.ClanTag, player, tag, playerName);
                }
            }

            if (playerName.Length > 32)
            {
                playerName = playerName.Substring(0, 32);
            }
            
            return playerName;
        }
        #endregion

        #region Message Handling
        private void SendSyncNotification(string playerId, GuildMember member, SyncData sync, bool wasAdded)
        {
            NotificationSettings settings = sync.Notifications;
            if (!settings.SendMessageToServer && !settings.SendMessageToDiscord)
            {
                return;
            }

            if (wasAdded && !settings.SendMessageOnAdd)
            {
                return;
            }

            if (!wasAdded && !settings.SendMessageOnRemove)
            {
                return;
            }

            if (settings.SendMessageToServer)
            {
                StringBuilder message = GetServerMessage(sync, wasAdded);
                ProcessMessage(message, playerId, member, sync);
                Chat(message.ToString());
            }

            if (settings.SendMessageToDiscord)
            {
                if (string.IsNullOrEmpty(settings.DiscordMessageChannelId))
                {
                    string direction;
                    if (sync.Source == Source.Server)
                    {
                        direction = $"{sync.Oxide} -> {sync.Discord}";
                    }
                    else
                    {
                        direction = $"{sync.Discord} -> {sync.Oxide}";
                    }
                    
                    Debug(DebugEnum.Warning,$"Send message to discord is enabled for {direction} but no channel ID is specified");
                    return;
                }
                
                StringBuilder message = GetDiscordMessage(sync, wasAdded);
                ProcessMessage(message, playerId, member, sync);
                SendChannelMessage(settings.DiscordMessageChannelId, message.ToString());
                Debug(DebugEnum.Info, $"Sent Discord Message - Channel ID:{settings.DiscordMessageChannelId} Message: {message}");
            }
        }

        private StringBuilder GetServerMessage(SyncData sync, bool wasAdded)
        {
            StringBuilder message = new StringBuilder();
            if(wasAdded && !string.IsNullOrEmpty(sync.Notifications.ServerMessageAddedOverride))
            {
                message.Append(sync.Notifications.ServerMessageAddedOverride);
            }
            else if (!wasAdded && !string.IsNullOrEmpty(sync.Notifications.ServerMessageRemovedOverride))
            {
                message.Append(sync.Notifications.ServerMessageRemovedOverride);
            }
            else
            {
                switch (sync.Source)
                {
                    case Source.Server:
                        message.Append(wasAdded ? Lang(LangKeys.ServerMessageDiscordRoleAdded) : Lang(LangKeys.ServerMessageDiscordRoleRemoved));
                        break;

                    case Source.Discord:
                        message.Append(wasAdded ? Lang(LangKeys.ServerMessageOxideGroupAdded) : Lang(LangKeys.ServerMessageOxideGroupRemoved));
                        break;
                }
            }

            return message;
        }
        
        private StringBuilder GetDiscordMessage(SyncData sync, bool wasAdded)
        {
            StringBuilder message = new StringBuilder();
            if(wasAdded && !string.IsNullOrEmpty(sync.Notifications.DiscordMessageAddedOverride))
            {
                message.Append(sync.Notifications.DiscordMessageAddedOverride);
            }
            else if (!wasAdded && !string.IsNullOrEmpty(sync.Notifications.DiscordMessageRemovedOverride))
            {
                message.Append(sync.Notifications.DiscordMessageRemovedOverride);
            }
            else
            {
                switch (sync.Source)
                {
                    case Source.Server:
                        message.Append(wasAdded ? Lang(LangKeys.DiscordMessageDiscordRoleAdded) : Lang(LangKeys.DiscordMessageDiscordRoleRemoved));
                        break;

                    case Source.Discord:
                        message.Append(wasAdded ? Lang(LangKeys.DiscordMessageOxideGroupAdded) : Lang(LangKeys.DiscordMessageOxideGroupRemoved));
                        break;
                }
            }

            return message;
        }

        private void ProcessMessage(StringBuilder message, string playerId, GuildMember member, SyncData sync)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerId);

            if (player != null)
            {
                message.Replace("{player.id}", player.Id);
                message.Replace("{player.name}", player.Name);
            }
            
            if (member != null)
            {
                message.Replace("{discord.id}", member.user.id);
                message.Replace("{discord.name}", member.user.username);
                message.Replace("{discord.discriminator}", member.user.discriminator);
                message.Replace("{discord.nickname}", member.nick);
            }

            Role role = GetRole(sync.Discord);
            if (role != null)
            {
                message.Replace("{role.id}", role.id);
                message.Replace("{role.name}", role.name);
            }

            message.Replace("{group.name}", sync.Oxide);
        }
        #endregion

        #region Subscription Handling
        public void UnsubscribeAll()
        {
            Unsubscribe(nameof(OnUserGroupAdded));
            Unsubscribe(nameof(OnUserGroupRemoved));
            Unsubscribe(nameof(Discord_GuildMemberUpdate));
        }

        public void SubscribeAll()
        {
            Subscribe(nameof(OnUserGroupAdded));
            Subscribe(nameof(OnUserGroupRemoved));
            Subscribe(nameof(Discord_GuildMemberUpdate));
        }
        #endregion

        #region Discord Core Hooks
        private void OnDiscordCoreClose()
        {
            _client = null;
            _guild = null;
        }

        private DiscordClient GetDiscordClient()
        {
            return DiscordCore?.Call<DiscordClient>("GetClient");
        }
        
        private Guild GetDiscordGuild()
        {
            return DiscordCore?.Call<Guild>("GetServer");
        }
        
        private string GetDiscordId(string playerId)
        {
            return DiscordCore?.Call<string>("GetDiscordIdFromSteamId", playerId);
        }

        private string GetPlayerId(string discordId)
        {
            return DiscordCore?.Call<string>("GetSteamIdFromDiscordId", discordId);
        }

        private Role GetRole(string role)
        {
            return DiscordCore.Call<Role>("GetRole", role);
        }

        public bool DiscordUserHasRole(string playerId, string role, GuildMember member)
        {
            if (member != null)
            {
                return member.roles.Any(r => r == role);
            }
            
            return DiscordCore.Call<bool>("UserHasRole", playerId, role);
        }

        private void UpdateUserNick(string id, string newNick)
        {
            DiscordCore.Call("UpdateUserNick", id, newNick);
        }
        
        private void SendChannelMessage(string channel, string message)
        {
            if (string.IsNullOrEmpty(channel))
            {
                return;
            }
            
            DiscordCore.Call("SendMessageToChannel", channel, message);
        }
        #endregion

        #region Clan Hooks
        private void OnClanCreate(string tag)
        {
            HandleClan(tag);
        }

        private void OnClanUpdate(string tag)
        {
            HandleClan(tag);
        }

        private void HandleClan(string tag)
        {
            JObject clan = Clans?.Call<JObject>("GetClan", tag);
            if (clan == null)
            {
                return;
            }
            
            JArray membersObject = clan["members"] as JArray;
            if (membersObject == null)
            {
                return;
            }
            
            foreach (JToken token in membersObject)
            {
                string id = token.ToString();
                _processIds.RemoveAll(p => p.PlayerId == id);
                _processIds.Insert(0, new PlayerSync(id));
                Debug(DebugEnum.Info, $"User clan has changed for clan {tag}. Adding {id} to be processed.");
            }
        }

        #endregion

        #region Helper Methods
        private string GetRoleDisplayName(string role)
        {
            ulong val;
            if (ulong.TryParse(role, out val))
            {
                return $"{_discordRoleLookup[role]}({role})";
            }

            return $"{role}({_discordRoleLookup[role]})";
        }

        private void Debug(DebugEnum level, string message)
        {
            if (level <= _pluginConfig.DebugLevel)
            {
                Puts($"{level}: {message}");
            }
        }

        public void Chat(string message)
        {
            server.Broadcast(Lang(LangKeys.Chat, null, message));
            Debug(DebugEnum.Message, $"Chat Message: {Formatter.ToPlaintext(message)}");
        }
        
        public string Lang(string key, IPlayer player = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, player?.Id), args);
            }
            catch (Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception:\n{ex}");
                throw;
            }
        }
        #endregion

        #region Classes

        private class PluginConfig
        {
            [DefaultValue(false)]
            [JsonProperty(PropertyName = "Sync Nicknames")]
            public bool SyncNicknames { get; set; }
            
            [DefaultValue(false)]
            [JsonProperty(PropertyName = "Add Clan Tag To Nicknames")]
            public bool AddClanTag { get; set; }

            [DefaultValue(2f)]
            [JsonProperty(PropertyName = "Update Rate (Seconds)")]
            public float UpdateRate { get; set; }
            
            [DefaultValue(false)]
            [JsonProperty(PropertyName = "Use AntiSpamNames On Discord Nickname")]
            public bool UseAntiSpamNames { get; set; }

            [JsonProperty(PropertyName = "Sync Data")]
            public List<SyncData> SyncData { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(DebugEnum.None)]
            [JsonProperty(PropertyName = "Debug Level (None, Error, Warning, Info)")]
            public DebugEnum DebugLevel { get; set; }
        }

        private class SyncData
        {
            [JsonProperty(PropertyName = "Oxide Group")]
            public string Oxide { get; set; }

            [JsonProperty(PropertyName = "Discord Role (Name or Id)")]
            public string Discord { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty(PropertyName = "Sync Source (Umod or Discord)")]
            public Source? SourceOld { get; set; }
            
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty(PropertyName = "Sync Source (Server or Discord)")]
            public Source Source { get; set; }
            
            [JsonProperty(PropertyName = "Sync Notification Settings")]
            public NotificationSettings Notifications { get; set; }
            
            public bool ShouldSerializeSourceOld()
            {
                return SourceOld != null;
            }
        }

        private class NotificationSettings
        {
            [JsonProperty(PropertyName = "Send message to Server")]
            public bool SendMessageToServer { get; set; }
            
            [JsonProperty(PropertyName = "Send Message To Discord")]
            public bool SendMessageToDiscord { get; set; }
            
            [JsonProperty(PropertyName = "Discord Message Channel (Name or ID)")]
            public string DiscordMessageChannelId { get; set; }
            
            [JsonProperty(PropertyName = "Send Message When Added")]
            public bool SendMessageOnAdd { get; set; }
            
            [JsonProperty(PropertyName = "Send Message When Removed")]
            public bool SendMessageOnRemove { get; set; }
            
            [JsonProperty(PropertyName = "Server Message Added Override Message")]
            public string ServerMessageAddedOverride { get; set; }
            
            [JsonProperty(PropertyName = "Server Message Removed Override Message")]
            public string ServerMessageRemovedOverride { get; set; }

            [JsonProperty(PropertyName = "Discord Message Added Override Message")]
            public string DiscordMessageAddedOverride { get; set; }
            
            [JsonProperty(PropertyName = "Discord Message Removed Override Message")]
            public string DiscordMessageRemovedOverride { get; set; }
        }

        private class PlayerSync
        {
            public string PlayerId;
            public string DiscordId;
            public bool IsLeaving;

            public PlayerSync(string playerId)
            {
                PlayerId = playerId;
            }

            public PlayerSync(string playerId, string discordId, bool isLeaving)
            {
                PlayerId = playerId;
                DiscordId = discordId;
                IsLeaving = isLeaving;
            }
        }
        
        public class LangKeys
        {
            public const string Chat = nameof(Chat);
            public const string ClanTag = nameof(ClanTag);
            
            public const string ServerMessageOxideGroupAdded = nameof(ServerMessageOxideGroupAdded) + "V1";
            public const string ServerMessageOxideGroupRemoved = nameof(ServerMessageOxideGroupRemoved) + "V1";
            public const string ServerMessageDiscordRoleAdded = nameof(ServerMessageDiscordRoleAdded) + "V1";
            public const string ServerMessageDiscordRoleRemoved = nameof(ServerMessageDiscordRoleRemoved) + "V1";
            
            public const string DiscordMessageOxideGroupAdded = nameof(DiscordMessageOxideGroupAdded) + "V1";
            public const string DiscordMessageOxideGroupRemoved = nameof(DiscordMessageOxideGroupRemoved) + "V1";
            public const string DiscordMessageDiscordRoleAdded = nameof(DiscordMessageDiscordRoleAdded) + "V1";
            public const string DiscordMessageDiscordRoleRemoved = nameof(DiscordMessageDiscordRoleRemoved) + "V1";
        }
        #endregion
    }
}