using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord.Attributes;
using Oxide.Ext.Discord;
using Oxide.Ext.Discord.Constants;
using Oxide.Ext.Discord.Entities;
using Oxide.Ext.Discord.Entities.Applications;
using Oxide.Ext.Discord.Entities.Gatway;
using Oxide.Ext.Discord.Entities.Gatway.Events;
using Oxide.Ext.Discord.Entities.Guilds;
using Oxide.Ext.Discord.Entities.Messages;
using Oxide.Ext.Discord.Entities.Permissions;
using Oxide.Ext.Discord.Entities.Users;
using Oxide.Ext.Discord.Extensions;
using Oxide.Ext.Discord.Libraries.Linking;
using Oxide.Ext.Discord.Logging;

namespace Oxide.Plugins
{
    [Info("Discord Roles", "MJSU", "2.0.9")]
    [Description("Syncs players oxide group with discord roles")]
    class DiscordRoles : CovalencePlugin
    {
        #region Class Fields
        [PluginReference] private Plugin AntiSpam, Clans;
        
        [DiscordClient] private DiscordClient _client;
        
        private PluginConfig _pluginConfig; //Plugin Config

        private readonly List<PlayerSync> _processIds = new List<PlayerSync>();
        
        private Timer _playerChecker;
        private DiscordGuild _guild;
        private DiscordSettings _discordSettings;

        private const string AccentColor = "#de8732";

        private readonly List<string> _added = new List<string>();
        private readonly List<string> _removed = new List<string>();

        private readonly DiscordLink _link = GetLibrary<DiscordLink>();

        private readonly Hash<string, string> _nicknames = new Hash<string, string>();
        private readonly List<string> _userRoleList = new List<string>();

        public enum DebugEnum
        {
            Message,
            None,
            Error,
            Warning,
            Info
        }

        public enum Source
        {
            Server,
            Discord
        }

        public enum SyncEvent
        {
            None,
            PluginLoaded,
            PlayerConnected,
            ServerGroupChanged,
            DiscordRoleChanged,
            DiscordNicknameChanged,
            PlayerLinkedChanged,
            DiscordServerJoinLeave
        }
        #endregion

        #region Setup & Loading
        private void Init()
        {
            _discordSettings = new DiscordSettings
            {
                ApiToken = _pluginConfig.DiscordApiKey,
                LogLevel = _pluginConfig.ExtensionDebugging,
                Intents = GatewayIntents.Guilds | GatewayIntents.GuildMembers
            };
            
            UnsubscribeAll();
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Chat] = $"[#BEBEBE][[{AccentColor}]{Title}[/#]] {{0}}[/#]",
                [LangKeys.ClanTag] = "[{0}] {1}",
                [LangKeys.ServerMessageGroupAdded] = "{player.name} has been added to server group {group.name}",
                [LangKeys.ServerMessageGroupRemoved] = "{player.name} has been removed to server group {group.name}",
                [LangKeys.ServerMessageRoleAdded] = "{player.name} has been added to discord role {role.name}",
                [LangKeys.ServerMessageRoleRemoved] = "{player.name} has been removed to discord role {role.name}",

                [LangKeys.DiscordMessageGroupAdded] = "{discord.name} has been added to server group {group.name}",
                [LangKeys.DiscordMessageGroupRemoved] = "{discord.name} has been removed to server group {group.name}",
                [LangKeys.DiscordMessageRoleAdded] = "{discord.name} has been added to discord role {role.name}",
                [LangKeys.DiscordMessageRoleRemoved] = "{discord.name} has been removed to discord role {role.name}",
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
            config.EventSettings = new EventSettings(config.EventSettings);
            
            config.SyncData = config.SyncData ?? new List<SyncData>
            {
                new SyncData("Default", default(Snowflake), Source.Server),
                new SyncData("VIP", default(Snowflake), Source.Discord)
            };

            for (int index = 0; index < config.SyncData.Count; index++)
            {
                config.SyncData[index] = new SyncData(config.SyncData[index]);
            }

            return config;
        }

        private void OnServerInitialized()
        {
            if (string.IsNullOrEmpty(_pluginConfig.DiscordApiKey))
            {
                PrintWarning("Please enter your bot token in the config and reload the plugin.");
                return;
            }
            
            if (_pluginConfig.UseAntiSpam && AntiSpam == null)
            {
                PrintWarning("AntiSpam is enabled in the config but is not loaded. " +
                             "Please disable the setting in the config or load AntiSpam: https://umod.org/plugins/anti-spam");
                _pluginConfig.UseAntiSpam = false;
            }
            
            _client.Connect(_discordSettings);
        }
        #endregion

        #region Discord Hooks
        [HookMethod(DiscordExtHooks.OnDiscordGatewayReady)]
        private void OnDiscordGatewayReady(GatewayReadyEvent ready)
        {
            if (ready.Guilds.Count == 0)
            {
                PrintError("Your bot was not found in any discord servers. Please invite it to a server and reload the plugin.");
                return;
            }

            _guild = null;
            if (ready.Guilds.Count == 1 && !_pluginConfig.GuildId.IsValid())
            {
                _guild = ready.Guilds.Values.FirstOrDefault();
            }

            if (_guild == null)
            {
                _guild = ready.Guilds[_pluginConfig.GuildId];
                if (_guild == null)
                {
                    PrintError("Failed to find a matching guild for the Discord Server Id. " +
                               "Please make sure your guild Id is correct and the bot is in the discord server.");
                }
            }
            
            DiscordApplication app = _client.Bot.Application;
            if (!app.HasApplicationFlag(ApplicationFlags.GatewayGuildMembersLimited) && !app.HasApplicationFlag(ApplicationFlags.GatewayGuildMembers))
            {
                PrintError($"You need to enable \"Server Members Intent\" for {_client.Bot.BotUser.Username} @ https://discord.com/developers/applications\n" +
                           $"{Name} will not function correctly until that is fixed. Once updated please reload {Name}.");
                return;
            }
        }

        [HookMethod(DiscordExtHooks.OnDiscordGuildMembersLoaded)]
        private void OnDiscordGuildMembersLoaded(DiscordGuild guild)
        {
            if (guild.Id != _guild.Id)
            {
                return;
            }

            HandleMembersLoaded();
            SubscribeAll();
            Puts($"{Title} Ready");
        }

        private void HandleMembersLoaded()
        {
            if (!_pluginConfig.EventSettings.IsAnyEnabled(SyncEvent.PluginLoaded))
            {
                Debug(DebugEnum.Info, "Skipping plugin load event due to no events being enabled");
                return;
            }
            
            for (int index = _pluginConfig.SyncData.Count - 1; index >= 0; index--)
            {
                SyncData data = _pluginConfig.SyncData[index];
                bool remove = false;
                if (!permission.GroupExists(data.ServerGroup))
                {
                    PrintWarning($"Server group does not exist: '{data.ServerGroup}'. Please create the group or correct the name");
                    remove = true;
                }

                DiscordRole role = _guild.Roles[data.DiscordRole];
                if (role == null)
                {
                    PrintWarning($"Discord role ID does not exist: '{data.DiscordRole}'.\n" +
                                 "Please fix the role ID.");
                    remove = true;
                }

                if (remove)
                {
                    _pluginConfig.SyncData.RemoveAt(index);
                }
            }

            timer.In(5f, CheckAllPlayers);
        }

        private void CheckAllPlayers()
        {
            Hash<string, Snowflake> links = _link.GetSteamToDiscordIds();
            if (links == null)
            {
                PrintWarning("No Discord Link plugin registered. Please add a Discord Link plugin and reload this plugin.");
                return;
            }
            
            foreach (KeyValuePair<string, Snowflake> link in links)
            {
                IPlayer player = players.FindPlayerById(link.Key);
                if (player == null)
                {
                    continue;
                }

                _processIds.Add(new PlayerSync(player, link.Value, false, SyncEvent.PluginLoaded));
            }

            Debug(DebugEnum.Message, $"Starting sync for {_processIds.Count} linked players");

            StartChecker();
        }
        
        private void StartChecker()
        {
            if (_playerChecker == null || _playerChecker.Destroyed)
            {
                _playerChecker = timer.Every(_pluginConfig.UpdateRate, ProcessNextStartupId);
            }
        }

        private void ProcessNextStartupId()
        {
            if (_processIds.Count == 0)
            {
                _playerChecker?.Destroy();
                _playerChecker = null;
                return;
            }

            PlayerSync id = _processIds[0];
            _processIds.RemoveAt(0);

            ProcessUser(id);
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

        #region Hooks
        private void OnUserConnected(IPlayer player)
        {
            if (!_pluginConfig.EventSettings.IsAnyEnabled(SyncEvent.PlayerConnected))
            {
                Debug(DebugEnum.Info, "Skipping player connected event due to no events being enabled");
                return;
            }
            
            Debug(DebugEnum.Info, $"{nameof(OnUserConnected)} Added {player.Name}({player.Id}) to be processed");
            ProcessChange(player.Id, false, SyncEvent.PlayerConnected);
        }

        private void OnUserGroupAdded(string id, string groupName)
        {
            if (!_pluginConfig.EventSettings.IsAnyEnabled(SyncEvent.ServerGroupChanged))
            {
                Debug(DebugEnum.Info, "Skipping server group changed event due to no events being enabled");
                return;
            }
            
            Debug(DebugEnum.Info, $"{nameof(OnUserGroupAdded)} Added ({id}) to be processed because added to group {groupName}");
            ProcessChange(id, false, SyncEvent.ServerGroupChanged);
        }

        private void OnUserGroupRemoved(string id, string groupName)
        {
            if (!_pluginConfig.EventSettings.IsAnyEnabled(SyncEvent.ServerGroupChanged))
            {
                Debug(DebugEnum.Info, "Skipping server group changed event due to no events being enabled");
                return;
            }
            
            Debug(DebugEnum.Info, $"{nameof(OnUserGroupRemoved)} Added ({id}) to be processed because removed from group {groupName}");
            ProcessChange(id, false, SyncEvent.ServerGroupChanged);
        }

        [HookMethod(DiscordExtHooks.OnDiscordPlayerLinked)]
        private void OnDiscordPlayerLinked(IPlayer player, DiscordUser user)
        {
            if (!_pluginConfig.EventSettings.IsAnyEnabled(SyncEvent.PlayerLinkedChanged))
            {
                Debug(DebugEnum.Info, "Skipping player linked event due to no events being enabled");
                return;
            }
            
            Debug(DebugEnum.Info, $"{nameof(OnDiscordPlayerLinked)} Added Player {player.Name}({player.Id}) Discord: {user.Username}#{user.Discriminator}({user.Id}) to be processed");
            ProcessChange(player.Id, false, SyncEvent.PlayerLinkedChanged);
        }

        [HookMethod(DiscordExtHooks.OnDiscordPlayerUnlinked)]
        private void OnDiscordPlayerUnlinked(IPlayer player, DiscordUser user)
        {
            if (!_pluginConfig.EventSettings.IsAnyEnabled(SyncEvent.PlayerLinkedChanged))
            {
                Debug(DebugEnum.Info, "Skipping player unlinked event due to no events being enabled");
                return;
            }
            
            Debug(DebugEnum.Info, $"{nameof(OnDiscordPlayerUnlinked)} Added Player {player.Name}({player.Id}) Discord: {user.Username}#{user.Discriminator}({user.Id}) to be processed");
            ProcessChange(player.Id, true, SyncEvent.PlayerLinkedChanged);
        }

        [HookMethod(DiscordExtHooks.OnDiscordGuildMemberAdded)]
        private void OnDiscordGuildMemberAdded(GuildMemberAddedEvent member)
        {
            if (member.GuildId != _guild.Id)
            {
                return;
            }
            
            if (!_pluginConfig.EventSettings.IsAnyEnabled(SyncEvent.DiscordServerJoinLeave))
            {
                Debug(DebugEnum.Info, "Skipping player join event due to no events being enabled");
                return;
            }
            
            Debug(DebugEnum.Info, $"{nameof(OnDiscordGuildMemberAdded)} Added {member.DisplayName}({member.User.Id}) to be processed");
            HandleDiscordChange(member.User, false, SyncEvent.DiscordServerJoinLeave);
        }

        [HookMethod(DiscordExtHooks.OnDiscordGuildMemberRemoved)]
        private void OnDiscordGuildMemberRemoved(GuildMemberRemovedEvent member)
        {
            if (member.GuildId != _guild.Id)
            {
                return;
            }
            
            if (!_pluginConfig.EventSettings.IsAnyEnabled(SyncEvent.DiscordServerJoinLeave))
            {
                Debug(DebugEnum.Info, "Skipping player leave event due to no events being enabled");
                return;
            }
            
            Debug(DebugEnum.Info, $"{nameof(OnDiscordGuildMemberRemoved)} Added {member.User.GetFullUserName}({member.User.Id}) to be processed");
            HandleDiscordChange(member.User, true, SyncEvent.DiscordServerJoinLeave);
        }

        [HookMethod(DiscordExtHooks.OnDiscordGuildMemberUpdated)]
        private void OnDiscordGuildMemberUpdated(GuildMember update, GuildMember oldMember, DiscordGuild guild)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                sb.AppendLine("A");
                if (guild.Id != _guild?.Id)
                {
                    return;
                }

                sb.AppendLine("B");
                IPlayer player = update.User.Player;
                if (player == null)
                {
                    return;
                }
            
                _added.Clear();
                _removed.Clear();

                sb.AppendLine("C");
                foreach (Snowflake snowflake in update.Roles.Except(oldMember.Roles))
                {
                    _added.Add(_guild?.Roles[snowflake]?.Name ?? "Unknown Role");
                }
            
                foreach (Snowflake snowflake in oldMember.Roles.Except(update.Roles))
                {
                    _removed.Add(_guild?.Roles[snowflake]?.Name ?? "Unknown Role");
                }
            
                sb.AppendLine("D");

                bool shouldUpdate = false;
                SyncEvent syncEvent = SyncEvent.None;
                if (_added.Count != 0)
                {
                    shouldUpdate = true;
                    Debug(DebugEnum.Info, $"{nameof(OnDiscordGuildMemberUpdated)} Added {update.Nickname}({update.User.Id}) to be processed because added roles {string.Join(", ", _added)}");
                    syncEvent = SyncEvent.DiscordRoleChanged;
                }
            
                sb.AppendLine("E");
                if(_removed.Count != 0)
                {
                    shouldUpdate = true;
                    Debug(DebugEnum.Info, $"{nameof(OnDiscordGuildMemberUpdated)} Added {update.Nickname}({update.User.Id}) to be processed because removed roles {string.Join(", ", _removed)}");
                    syncEvent = SyncEvent.DiscordRoleChanged;
                }
            
                sb.AppendLine("F");
                if (update.Nickname != null && (!_nicknames.ContainsKey(update.Id) || _nicknames[update.Id] != update.Nickname) && update.Nickname != GetPlayerName(update.User.Player))
                {
                    shouldUpdate = true;
                    Debug(DebugEnum.Info, $"{nameof(OnDiscordGuildMemberUpdated)} Added {update.Nickname}({update.User.Id}) to be processed nickname changed: {oldMember.Nickname} -> {update.Nickname}");
                    syncEvent = SyncEvent.DiscordNicknameChanged;
                }

                sb.AppendLine("G");
                if (!shouldUpdate)
                {
                    return;
                }
            
                sb.AppendLine("H");
                if (!_pluginConfig.EventSettings.IsAnyEnabled(syncEvent))
                {
                    Debug(DebugEnum.Info, "Skipping guild member updated event due to no events being enabled");
                    return;
                }
            
                sb.AppendLine("I");
                HandleDiscordChange(update.User, false, syncEvent);
            }
            catch(Exception ex)
            {
                PrintError(sb.ToString());
            }
        }

        public void HandleDiscordChange(DiscordUser user, bool isLeaving, SyncEvent syncEvent)
        {
            string playerId = _link.GetSteamId(user.Id);
            if (string.IsNullOrEmpty(playerId))
            {
                return;
            }

            IPlayer player = players.FindPlayerById(playerId);
            if (player == null)
            {
                return;
            }
            
            _processIds.RemoveAll(p => p.MemberId == user.Id && !p.IsLeaving);

            PlayerSync sync = new PlayerSync(player, user.Id, isLeaving, syncEvent);
            if (isLeaving)
            {
                sync.Member = new GuildMember
                {
                    User = user,
                    Roles = new List<Snowflake>()
                };
            }

            _processIds.Insert(0, sync);
            StartChecker();
        }

        private void ProcessChange(string playerId, bool isLeaving, SyncEvent syncEvent)
        {
            _processIds.RemoveAll(p => p.Player.Id == playerId);

            IPlayer player = players.FindPlayerById(playerId);
            if (player == null)
            {
                return;
            }

            Snowflake discordId = player.GetDiscordUserId() ?? default(Snowflake);
            if (!discordId.IsValid())
            {
                return;
            }

            _processIds.Insert(0, new PlayerSync(player, discordId, isLeaving, syncEvent));

            StartChecker();
        }
        #endregion

        #region Role Handling
        public void ProcessUser(PlayerSync sync)
        {
            if (sync.Member == null)
            {
                if (!sync.MemberId.IsValid())
                {
                    return;
                }
                
                if (!_guild.Members.ContainsKey(sync.MemberId))
                {
                    return;
                }
                
                _guild.GetGuildMember(_client, sync.MemberId, member =>
                {
                    sync.Member = member;
                    ProcessUser(sync);
                });
                return;
            }
            
            try
            {
                Debug(DebugEnum.Info, $"Start processing: Player: {sync.Player.Name}({sync.Player.Id}) Discord: {sync.Member.DisplayName}({sync.Member.User?.Id}) Is Leaving: {sync.IsLeaving}");
                UnsubscribeAll();
                HandleServerGroups(sync);
                HandleDiscordRoles(sync);
                HandleUserNick(sync);
            }
            finally
            {
                SubscribeAll();
            }
        }

        public void HandleServerGroups(PlayerSync playerSync)
        {
            if (playerSync.IsLeaving)
            {
                return;
            }
            
            IPlayer player = playerSync.Player;
            GuildMember member = playerSync.Member;
            
            string playerName = $"{player.Name}({playerSync.Player.Id}) {member.DisplayName}({member.User.Id})";
            
            Debug(DebugEnum.Info, $"Processing Server for {player.Name}({player.Id}) Discord {member.DisplayName}({member.User.Id}) Is Leaving {playerSync.IsLeaving}");

            if (!_pluginConfig.EventSettings.ServerSync.IsEnabled(playerSync.Event))
            {
                Debug(DebugEnum.Info, $"Skipping server sync due to event not being enabled for {playerSync.Event}");
                return;
            }
            
            foreach (IGrouping<Snowflake, SyncData> data in _pluginConfig.SyncData.Where(s => s.Source == Source.Server).GroupBy(s => s.DiscordRole))
            {
                bool isInGroup = data.Any(d => permission.UserHasGroup(player.Id, d.ServerGroup));
                bool isInDiscord = member.Roles.Contains(data.Key);
                if (isInDiscord == isInGroup)
                {
                    Debug(DebugEnum.Info, $"{playerSync.Player.Name} skipping Server Sync: [{string.Join(", ", data.Select(d => d.ServerGroup).ToArray())}] -> {_guild.Roles[data.Key]?.Name} {(isInGroup ? "Already Synced" : "Not in group")}");
                    continue;
                }

                string roleName = _guild.Roles[data.Key]?.Name;
                
                if (isInGroup)
                {
                    Debug(DebugEnum.Message, $"Adding player {playerName} to discord role {roleName}");
                    _guild.AddGuildMemberRole(_client, member.User.Id, data.Key, () =>
                    {
                        Debug(DebugEnum.Message, $"Successfully added {playerName} to {roleName}");
                    }, error =>
                    {
                        Debug(DebugEnum.Error, $"An error has occured adding {playerName} to {roleName}. Please check above this message for the error.");
                    });
                }
                else
                {
                    _guild.RemoveGuildMemberRole(_client, playerSync.Member.User.Id, data.Key, () =>
                    {
                        Debug(DebugEnum.Message, $"Successfully removed {playerName} from {roleName}");
                    }, error =>
                    {
                        Debug(DebugEnum.Error, $"An error has occured removing {playerName} from {roleName}. Please check above this message for the error.");
                    });
                }

                SyncData sync = data.FirstOrDefault(d => permission.UserHasGroup(player.Id, d.ServerGroup)) ?? data.FirstOrDefault();
                SendSyncNotification(playerSync, sync, isInGroup);
            }
        }

        public void HandleDiscordRoles(PlayerSync playerSync)
        {
            IPlayer player = playerSync.Player;
            GuildMember member = playerSync.Member;
            
            string playerName = $"{player.Name}({playerSync.Player.Id}) {member.DisplayName}({member.User.Id})";
            
            Debug(DebugEnum.Info, $"Processing Discord for {player.Name}({player.Id}) Discord {member.DisplayName}({member.User.Id}) Is Leaving {playerSync.IsLeaving} Roles: {GetUserRoles(playerSync.Member)}");
            
            if (!_pluginConfig.EventSettings.DiscordSync.IsEnabled(playerSync.Event))
            {
                Debug(DebugEnum.Info, $"Skipping discord sync due to event not being enabled for {playerSync.Event}");
                return;
            }
            
            foreach (IGrouping<string, SyncData> data in _pluginConfig.SyncData.Where(s => s.Source == Source.Discord).GroupBy(s => s.ServerGroup))
            {
                bool isInGroup = permission.UserHasGroup(player.Id, data.Key);
                bool isInDiscord = false;
                SyncData sync = null;
                if (!playerSync.IsLeaving)
                {
                    foreach (SyncData syncData in data)
                    {
                        if (member.Roles.Contains(syncData.DiscordRole))
                        {
                            sync = syncData;
                            isInDiscord = true;
                            break;
                        }
                    }
                }

                if (isInDiscord == isInGroup)
                {
                    Debug(DebugEnum.Info, $"{player?.Name} skipping Discord Sync: [{string.Join(", ", data.Select(d => _guild.Roles[d.DiscordRole]?.Name ?? string.Empty).ToArray())}] -> {data.Key} {(isInDiscord ? "Already Synced" : "Doesn't have role")}");
                    continue;
                }

                if (isInDiscord)
                {
                    Debug(DebugEnum.Message, $"Adding player {playerName} to server group {data.Key}");
                    permission.AddUserGroup(player.Id, data.Key);
                }
                else
                {
                    Debug(DebugEnum.Message, $"Removing player {playerName} from server group {data.Key}");
                    permission.RemoveUserGroup(player.Id, data.Key);
                }
                
                sync = sync ?? data.FirstOrDefault();
                SendSyncNotification(playerSync, sync, isInDiscord);
            }
        }

        public void HandleUserNick(PlayerSync sync)
        {
            IPlayer player = sync.Player;
            if (!_pluginConfig.SyncNicknames || sync.IsLeaving)
            {
                Debug(DebugEnum.Info, $"{nameof(HandleUserNick)} don't sync nicknames or is leaving");
                return;
            }
            
            if (sync.Member.User.Id == _guild.OwnerId)
            {
                Debug(DebugEnum.Info, $"{nameof(HandleUserNick)} don't sync nickname discord server owner");
                return;
            }
            
            if (!_pluginConfig.EventSettings.NicknameSync.IsEnabled(sync.Event))
            {
                Debug(DebugEnum.Info, $"Skipping nickname sync due to event not being enabled for {sync.Event}");
                return;
            }

            string playerName = GetPlayerName(player);
            if (playerName.Equals(sync.Member.Nickname))
            {
                Debug(DebugEnum.Info, $"{nameof(HandleUserNick)} skipping nickname as it matches what we expect: {playerName}");
                return;
            }
            
            Debug(DebugEnum.Info, $"Updating {sync.Member.DisplayName}'s discord server nickname to {playerName}");
            
            _guild.ModifyUsersNick(_client, sync.Member.User.Id, playerName, member =>
            {
                Debug(DebugEnum.Info, $"Successfully updated {sync.Member.DisplayName}'s discord server nickname to {playerName}");
                _nicknames[sync.Member.User.Id] = member.Nickname;
            }, error =>
            {
                Debug(DebugEnum.Error, $"An error has occured updating {sync.Member.DisplayName}'s discord server nickname to {playerName}");
            });
        }
        
        private string GetPlayerName(IPlayer player)
        {
            string playerName = player.Name;
            if (_pluginConfig.UseAntiSpam && AntiSpam != null && AntiSpam.IsLoaded)
            {
                playerName = AntiSpam.Call<string>("GetClearName", player);
                if (string.IsNullOrEmpty(playerName))
                {
                    Debug(DebugEnum.Warning, $"AntiSpam returned an empty string for '{player.Name}'");
                    playerName = player.Name;
                }
                else if (!playerName.Equals(player.Name))
                {
                    Debug(DebugEnum.Info, $"Nickname '{player.Name}' was filtered by AntiSpam: '{playerName}'");
                }
            }
            
            if (_pluginConfig.SyncClanTag)
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
        private void SendSyncNotification(PlayerSync sync, SyncData data, bool wasAdded)
        {
            NotificationSettings settings = data.Notifications;
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
                StringBuilder message = GetServerMessage(data, wasAdded);
                ProcessMessage(message, sync, data);
                Chat(message.ToString());
            }

            if (settings.SendMessageToDiscord)
            {
                if (!settings.DiscordMessageChannelId.IsValid())
                {
                    return;
                }

                StringBuilder message = GetDiscordMessage(data, wasAdded);
                ProcessMessage(message, sync, data);
                DiscordMessage.CreateMessage(_client, settings.DiscordMessageChannelId, message.ToString());
            }
        }

        private StringBuilder GetServerMessage(SyncData sync, bool wasAdded)
        {
            StringBuilder message = new StringBuilder();
            if (wasAdded && !string.IsNullOrEmpty(sync.Notifications.ServerMessageAddedOverride))
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
                        message.Append(wasAdded ? LangNoFormat(LangKeys.ServerMessageRoleAdded) : LangNoFormat(LangKeys.ServerMessageRoleRemoved));
                        break;

                    case Source.Discord:
                        message.Append(wasAdded ? LangNoFormat(LangKeys.ServerMessageGroupAdded) : LangNoFormat(LangKeys.ServerMessageGroupRemoved));
                        break;
                }
            }

            return message;
        }

        private StringBuilder GetDiscordMessage(SyncData sync, bool wasAdded)
        {
            StringBuilder message = new StringBuilder();
            if (wasAdded && !string.IsNullOrEmpty(sync.Notifications.DiscordMessageAddedOverride))
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
                        message.Append(wasAdded ? LangNoFormat(LangKeys.DiscordMessageRoleAdded) : LangNoFormat(LangKeys.DiscordMessageRoleRemoved));
                        break;

                    case Source.Discord:
                        message.Append(wasAdded ? LangNoFormat(LangKeys.DiscordMessageGroupAdded) : LangNoFormat(LangKeys.DiscordMessageGroupRemoved));
                        break;
                }
            }

            return message;
        }

        private void ProcessMessage(StringBuilder message, PlayerSync sync, SyncData data)
        {
            IPlayer player = sync.Player;
            GuildMember member = sync.Member;

            if (player != null)
            {
                message.Replace("{player.id}", player.Id);
                message.Replace("{player.name}", player.Name);
            }

            if (member != null)
            {
                message.Replace("{discord.id}", member.User.Id.ToString());
                message.Replace("{discord.name}", member.User.Username);
                message.Replace("{discord.discriminator}", member.User.Discriminator);
                message.Replace("{discord.nickname}", member.Nickname);
            }

            DiscordRole role = _guild.Roles[data.DiscordRole];
            if (role != null)
            {
                message.Replace("{role.id}", role.Id.ToString());
                message.Replace("{role.name}", role.Name);
            }

            message.Replace("{group.name}", data.ServerGroup);
        }
        #endregion

        #region Subscription Handling
        public void UnsubscribeAll()
        {
            try
            {
                Unsubscribe(nameof(OnUserGroupAdded));
                Unsubscribe(nameof(OnUserGroupRemoved));
                Unsubscribe(nameof(OnUserConnected));
            }
            catch
            {
                
            }
        }

        public void SubscribeAll()
        {
            try
            {
                Subscribe(nameof(OnUserGroupAdded));
                Subscribe(nameof(OnUserGroupRemoved));
                Subscribe(nameof(OnUserConnected));
            }
            catch
            {
                
            }
        }
        #endregion

        #region Helper Methods
        public string GetUserRoles(GuildMember member)
        {
            _userRoleList.Clear();
            foreach (Snowflake role in member.Roles)
            {
                _userRoleList.Add($"{_guild.Roles[role].Name} ({role.ToString()})");
            }

            return string.Join(", ", _userRoleList);
        }

        public void Debug(DebugEnum level, string message)
        {
            if (level <= _pluginConfig.DebugLevel)
            {
                Puts($"{level}: {message}");
            }
        }

        public void Chat(string message)
        {
            server.Broadcast(Lang(LangKeys.Chat, null, message));
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
        
        public string LangNoFormat(string key, IPlayer player = null) => lang.GetMessage(key, this, player?.Id);
        #endregion

        #region Classes
        public class PluginConfig
        {
            [DefaultValue("")]
            [JsonProperty(PropertyName = "Discord Bot Token")]
            public string DiscordApiKey { get; set; }
            
            [JsonProperty(PropertyName = "Discord Server ID (Optional if bot only in 1 guild)")]
            public Snowflake GuildId { get; set; }
            
            [DefaultValue(false)]
            [JsonProperty(PropertyName = "Sync Nicknames")]
            public bool SyncNicknames { get; set; }
            
            [DefaultValue(false)]
            [JsonProperty(PropertyName = "Sync Clan Tag")]
            public bool SyncClanTag { get; set; }

            [DefaultValue(2f)]
            [JsonProperty(PropertyName = "Update Rate (Seconds)")]
            public float UpdateRate { get; set; }
            
            [DefaultValue(false)]
            [JsonProperty(PropertyName = "Use AntiSpam On Discord Nickname")]
            public bool UseAntiSpam { get; set; }
            
            [JsonProperty(PropertyName = "Action To Perform By Event")]
            public EventSettings EventSettings { get; set; }

            [JsonProperty(PropertyName = "Sync Data")]
            public List<SyncData> SyncData { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(DebugEnum.Warning)]
            [JsonProperty(PropertyName = "Plugin Log Level (None, Error, Warning, Info)")]
            public DebugEnum DebugLevel { get; set; }
            
            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(DiscordLogLevel.Info)]
            [JsonProperty(PropertyName = "Discord Extension Log Level (Verbose, Debug, Info, Warning, Error, Exception, Off)")]
            public DiscordLogLevel ExtensionDebugging { get; set; }
        }

        public class EventSettings
        {
            [JsonProperty("Events To Sync Server Groups -> Discord Roles")]
            public EnabledSyncEvents ServerSync { get; set; }
            
            [JsonProperty("Events To Sync Discord Roles -> Server Groups")]
            public EnabledSyncEvents DiscordSync { get; set; }
            
            [JsonProperty("Events To Sync Discord Nickname")]
            public EnabledSyncEvents NicknameSync { get; set; }

            [JsonConstructor]
            public EventSettings()
            {
                
            }
            
            public EventSettings(EventSettings settings)
            {
                ServerSync = new EnabledSyncEvents
                {
                    SyncOnPluginLoad = settings?.ServerSync?.SyncOnPluginLoad ?? true,
                    SyncOnPlayerConnected = settings?.ServerSync?.SyncOnPlayerConnected ?? true,
                    SyncOnServerGroupChanged = settings?.ServerSync?.SyncOnServerGroupChanged ?? true,
                    SyncOnDiscordRoleChanged = settings?.ServerSync?.SyncOnDiscordRoleChanged ?? true,
                    SyncOnDiscordNicknameChanged = settings?.ServerSync?.SyncOnDiscordNicknameChanged ?? false,
                    SyncOnLinkedChanged = settings?.ServerSync?.SyncOnLinkedChanged ?? true,
                    SyncOnDiscordServerJoinLeave = settings?.ServerSync?.SyncOnDiscordServerJoinLeave ?? true
                };
                
                DiscordSync = new EnabledSyncEvents
                {
                    SyncOnPluginLoad = settings?.DiscordSync?.SyncOnPluginLoad ?? true,
                    SyncOnPlayerConnected = settings?.DiscordSync?.SyncOnPlayerConnected ?? true,
                    SyncOnServerGroupChanged = settings?.DiscordSync?.SyncOnServerGroupChanged ?? true,
                    SyncOnDiscordRoleChanged = settings?.DiscordSync?.SyncOnDiscordRoleChanged ?? true,
                    SyncOnDiscordNicknameChanged = settings?.DiscordSync?.SyncOnDiscordNicknameChanged ?? false,
                    SyncOnLinkedChanged = settings?.DiscordSync?.SyncOnLinkedChanged ?? true,
                    SyncOnDiscordServerJoinLeave = settings?.DiscordSync?.SyncOnDiscordServerJoinLeave ?? true
                };
                
                NicknameSync = new EnabledSyncEvents
                {
                    SyncOnPluginLoad = settings?.NicknameSync?.SyncOnPluginLoad ?? true,
                    SyncOnPlayerConnected = settings?.NicknameSync?.SyncOnPlayerConnected ?? true,
                    SyncOnServerGroupChanged = settings?.NicknameSync?.SyncOnServerGroupChanged ?? false,
                    SyncOnDiscordRoleChanged = settings?.NicknameSync?.SyncOnDiscordRoleChanged ?? false,
                    SyncOnDiscordNicknameChanged = settings?.NicknameSync?.SyncOnDiscordNicknameChanged ?? true,
                    SyncOnLinkedChanged = settings?.NicknameSync?.SyncOnLinkedChanged ?? true,
                    SyncOnDiscordServerJoinLeave = settings?.NicknameSync?.SyncOnDiscordServerJoinLeave ?? false
                };
            }

            public bool IsAnyEnabled(SyncEvent syncEvent)
            {
                return ServerSync.IsEnabled(syncEvent) || DiscordSync.IsEnabled(syncEvent) || NicknameSync.IsEnabled(syncEvent);
            } 
        }

        public class SyncData
        {
            [JsonProperty(PropertyName = "Server Group")]
            public string ServerGroup { get; set; }

            [JsonProperty(PropertyName = "Discord Role ID")]
            public Snowflake DiscordRole { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty(PropertyName = "Sync Source (Server or Discord)")]
            public Source Source { get; set; }

            [JsonProperty(PropertyName = "Sync Notification Settings")]
            public NotificationSettings Notifications { get; set; }

            [JsonConstructor]
            public SyncData()
            {
                
            }
            
            public SyncData(string serverGroup, Snowflake discordRole, Source source)
            {
                ServerGroup = serverGroup;
                DiscordRole = discordRole;
                Source = source;
                Notifications = new NotificationSettings();
            }

            public SyncData(SyncData settings)
            {
                ServerGroup = settings?.ServerGroup ?? string.Empty;
                DiscordRole = settings?.DiscordRole ?? default(Snowflake);
                Source = settings?.Source ?? Source.Server;
                Notifications = new NotificationSettings(settings?.Notifications);
            }
        }

        public class NotificationSettings
        {
            [JsonProperty(PropertyName = "Send message to Server")]
            public bool SendMessageToServer { get; set; }

            [JsonProperty(PropertyName = "Send Message To Discord")]
            public bool SendMessageToDiscord { get; set; }

            [JsonProperty(PropertyName = "Discord Message Channel ID")]
            public Snowflake DiscordMessageChannelId { get; set; }

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

            public NotificationSettings()
            {
                SendMessageToServer = false;
                SendMessageToDiscord = false;
                DiscordMessageChannelId = default(Snowflake);
                SendMessageOnAdd = false;
                SendMessageOnRemove = false;
                ServerMessageAddedOverride = string.Empty;
                ServerMessageRemovedOverride = string.Empty;
                DiscordMessageAddedOverride = string.Empty;
                DiscordMessageRemovedOverride = string.Empty;
            }

            public NotificationSettings(NotificationSettings settings)
            {
                SendMessageToServer = settings?.SendMessageToServer ?? false;
                SendMessageToDiscord = settings?.SendMessageToDiscord ?? false;
                DiscordMessageChannelId = settings?.DiscordMessageChannelId ?? default(Snowflake);
                SendMessageOnAdd = settings?.SendMessageOnAdd ?? false;
                SendMessageOnRemove = settings?.SendMessageOnRemove ?? false;
                ServerMessageAddedOverride = settings?.ServerMessageAddedOverride ?? string.Empty;
                ServerMessageRemovedOverride = settings?.ServerMessageRemovedOverride ?? string.Empty;
                DiscordMessageAddedOverride = settings?.DiscordMessageAddedOverride ?? string.Empty;
                DiscordMessageRemovedOverride = settings?.DiscordMessageRemovedOverride ?? string.Empty;
            }
        }

        public class EnabledSyncEvents
        {
            [JsonProperty("Sync On Plugin Load")]
            public bool SyncOnPluginLoad { get; set; }
            
            [JsonProperty("Sync On Player Connected")]
            public bool SyncOnPlayerConnected { get; set; }
            
            [JsonProperty("Sync On Server Group Changed")]
            public bool SyncOnServerGroupChanged { get; set; }
            
            [JsonProperty("Sync On Discord Role Changed")]
            public bool SyncOnDiscordRoleChanged { get; set; }
            
            [JsonProperty("Sync On Discord Nickname Changed")]
            public bool SyncOnDiscordNicknameChanged { get; set; }
            
            [JsonProperty("Sync On Player Linked / Unlinked")]
            public bool SyncOnLinkedChanged { get; set; }
            
            [JsonProperty("Sync On User Join / Leave Discord Server")]
            public bool SyncOnDiscordServerJoinLeave { get; set; }

            public bool IsEnabled(SyncEvent syncEvent)
            {
                switch (syncEvent)
                {
                    case SyncEvent.None:
                        return false;
                    case SyncEvent.PluginLoaded:
                        return SyncOnPluginLoad;
                    case SyncEvent.PlayerConnected:
                        return SyncOnPlayerConnected;
                    case SyncEvent.ServerGroupChanged:
                        return SyncOnServerGroupChanged;
                    case SyncEvent.DiscordRoleChanged:
                        return SyncOnDiscordRoleChanged;
                    case SyncEvent.DiscordNicknameChanged:
                        return SyncOnDiscordNicknameChanged;
                    case SyncEvent.PlayerLinkedChanged:
                        return SyncOnLinkedChanged;
                    case SyncEvent.DiscordServerJoinLeave:
                        return SyncOnDiscordServerJoinLeave;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(syncEvent), syncEvent, null);
                }
            }
        }

        public class PlayerSync
        {
            public IPlayer Player { get; set; }
            public GuildMember Member { get; set; }
            public Snowflake MemberId { get; set; }
            public SyncEvent Event { get; set; }
            public bool IsLeaving { get; set; }

            public PlayerSync(IPlayer player, Snowflake memberId, bool isLeaving, SyncEvent syncEvent)
            {
                Player = player;
                MemberId = memberId;
                IsLeaving = isLeaving;
                Event = syncEvent;
            }
        }

        public class LangKeys
        {
            public const string Chat = nameof(Chat);
            public const string ClanTag = nameof(ClanTag);

            public const string ServerMessageGroupAdded = nameof(ServerMessageGroupAdded);
            public const string ServerMessageGroupRemoved = nameof(ServerMessageGroupRemoved);
            public const string ServerMessageRoleAdded = nameof(ServerMessageRoleAdded);
            public const string ServerMessageRoleRemoved = nameof(ServerMessageRoleRemoved);

            public const string DiscordMessageGroupAdded = nameof(DiscordMessageGroupAdded);
            public const string DiscordMessageGroupRemoved = nameof(DiscordMessageGroupRemoved);
            public const string DiscordMessageRoleAdded = nameof(DiscordMessageRoleAdded);
            public const string DiscordMessageRoleRemoved = nameof(DiscordMessageRoleRemoved);
        }
        #endregion
    }
}