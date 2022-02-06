using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord;
using Oxide.Ext.Discord.Attributes;
using Oxide.Ext.Discord.Constants;
using Oxide.Ext.Discord.Entities;
using Oxide.Ext.Discord.Entities.Applications;
using Oxide.Ext.Discord.Entities.Channels;
using Oxide.Ext.Discord.Entities.Gatway;
using Oxide.Ext.Discord.Entities.Gatway.Events;
using Oxide.Ext.Discord.Entities.Guilds;
using Oxide.Ext.Discord.Entities.Messages;
using Oxide.Ext.Discord.Entities.Messages.AllowedMentions;
using Oxide.Ext.Discord.Entities.Permissions;
using Oxide.Ext.Discord.Entities.Users;
using Oxide.Ext.Discord.Libraries.Subscription;
using Oxide.Ext.Discord.Logging;
using Oxide.Ext.Discord.Rest;
#if RUST
using ConVar;
#endif

// ReSharper disable MemberCanBePrivate.Global
namespace Oxide.Plugins
{
    [Info("Discord Chat", "MJSU", "2.1.2")]
    [Description("Allows chatting through discord")]
    internal class DiscordChat : CovalencePlugin
    {
        #region Class Fields
        [PluginReference]
        private Plugin AdminChat, AdminDeepCover, AntiSpam, BetterChat, BetterChatMute, Clans, ChatTranslator, TranslationAPI, UFilter;

        [DiscordClient]
        private DiscordClient _client;

        private readonly DiscordSettings _discordSettings = new DiscordSettings
        {
            Intents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages
        };

        private readonly AllowedMention _allowedMention = new AllowedMention
        {
            AllowedTypes = new List<AllowedMentionTypes>(),
            Roles = new List<Snowflake>(),
            Users = new List<Snowflake>()
        };

        private DiscordGuild _guild;
        private Snowflake _guildId;

        private readonly DiscordSubscriptions _subscriptions = GetLibrary<DiscordSubscriptions>();

        private PluginConfig _pluginConfig;

        private readonly Hash<MessageType, DiscordTimedSend> _sends = new Hash<MessageType, DiscordTimedSend>();

        private static DiscordChat _ins;

        public enum MessageType
        {
            Discord,
            Server,
            Team,
            Cards,
            JoinLeave,
            AdminChat,
            OnlineOffline
        }

        private readonly StringBuilder _name = new StringBuilder();
        private readonly StringBuilder _message = new StringBuilder();
        private readonly StringBuilder _mentions = new StringBuilder();
        
        private readonly Regex _channelMention = new Regex("(<#\\d+>)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private bool _isServerStartup;
        #endregion

        #region Setup & Loading
        private void Init()
        {
            _ins = this;

            _discordSettings.ApiToken = _pluginConfig.DiscordApiKey;
            _discordSettings.LogLevel = _pluginConfig.ExtensionDebugging;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Discord.NotLinkedError] = "You're not allowed to chat with the server unless you are linked.",
                [LangKeys.Discord.JoinLeave.ConnectedMessage] = ":white_check_mark: ({0:HH:mm}) **{1}** has joined.",
                [LangKeys.Discord.JoinLeave.DisconnectedMessage] = ":x: ({0:HH:mm}) **{1}** has disconnected. ({2})",
                [LangKeys.Discord.OnlineOffline.OnlineMessage] = ":green_circle: The server is now online",
                [LangKeys.Discord.OnlineOffline.OfflineMessage] = ":red_circle: The server has shutdown",
                [LangKeys.Discord.ChatChannel.Message] = ":desktop: ({0:HH:mm}) **{1}**: {2}",
                [LangKeys.Discord.ChatChannel.LinkedMessage] = ":speech_left: ({0:HH:mm}) **{1}**: {2}",
                [LangKeys.Discord.ChatChannel.UnlinkedMessage] = ":chains: ({0:HH:mm}) **{1}#{2}**: {3}",
                [LangKeys.Discord.TeamChannel.Message] = ":busts_in_silhouette: ({0:HH:mm}) **{1}**: {2}",
                [LangKeys.Discord.CardsChannel.Message] = ":black_joker: ({0:HH:mm}) **{1}**: {2}",
                [LangKeys.Discord.AdminChat.Message] = ":mechanic: ({0:HH:mm}) **{1}**: {2}",
                [LangKeys.Discord.AdminChat.Permission] = ":no_entry: You're not allowed to use admin chat channel because you do not have permission.",
                [LangKeys.Server.DiscordTag] = "[#5f79d6][Discord][/#]",
                [LangKeys.Server.UnlinkedMessage] = "{0} [#5f79d6]{1}#{2}[/#]: {3}",
                [LangKeys.Server.LinkedMessage] = "{0} [#5f79d6]{1}[/#]: {2}",
                [LangKeys.Server.ClanTag] = "[{0}] "
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Discord.NotLinkedError] = "Вам не разрешено писать в чат сервера, если ваши аккаунты не связаны.",
                [LangKeys.Discord.JoinLeave.ConnectedMessage] = ":white_check_mark: {0:HH:mm} **{1}** подключился",
                [LangKeys.Discord.JoinLeave.DisconnectedMessage] = ":x: {0:HH:mm} **{1}** отключился. Причина: {2}",
                [LangKeys.Discord.OnlineOffline.OnlineMessage] = ":green_circle: Выключение сервера",
                [LangKeys.Discord.OnlineOffline.OfflineMessage] = ":red_circle: Сервер снова онлайн",
                [LangKeys.Discord.ChatChannel.Message] = ":desktop: ({0:HH:mm}) **{1}**: {2}",
                [LangKeys.Discord.ChatChannel.LinkedMessage] = ":speech_left: ({0:HH:mm}) **{1}**: {2}",
                [LangKeys.Discord.ChatChannel.UnlinkedMessage] = ":chains: ({0:HH:mm}) **{1}#{2}**: {3}",
                [LangKeys.Discord.TeamChannel.Message] = ":busts_in_silhouette: ({0:HH:mm}) **{1}**: {2}",
                [LangKeys.Discord.CardsChannel.Message] = ":black_joker: ({0:HH:mm}) **{1}**: {2}",
                [LangKeys.Discord.AdminChat.Message] = ":mechanic: ({0:HH:mm}) **{1}** {2}",
                [LangKeys.Discord.AdminChat.Permission] = "Вам не разрешено использовать канал чата администратора, потому что у вас нет разрешения.",
                [LangKeys.Server.DiscordTag] = "[#5f79d6][Discord][/#]",
                [LangKeys.Server.UnlinkedMessage] = "{0} [#5f79d6]{1}#{2}[/#]: {3}",
                [LangKeys.Server.LinkedMessage] = "{0} [#5f79d6]{1}[/#]: {2}",
                [LangKeys.Server.ClanTag] = "[{0}] "
            }, this, "ru");
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            config.ChannelSettings = new ChannelSettings(config.ChannelSettings);
            config.MessageSettings = new MessageSettings(config.MessageSettings);
            config.PluginSupport = new PluginSupport(config.PluginSupport);
            return config;
        }

        private void OnServerInitialized(bool startup)
        {
            _isServerStartup = startup;
            if (IsPluginLoaded(BetterChat))
            {
                if (BetterChat.Version < new VersionNumber(5, 2, 7))
                {
                    PrintWarning("Please update your version of BetterChat to version >= 5.2.7");
                }

                if (_pluginConfig.EnableServerChatTag)
                {
                    BetterChat.Call("API_RegisterThirdPartyTitle", this, new Func<IPlayer, string>(GetBetterChatTag));
                }
            }

            if (_pluginConfig.PluginSupport.AntiSpam.ValidateNicknames
                || _pluginConfig.PluginSupport.AntiSpam.ServerMessage
                || _pluginConfig.PluginSupport.AntiSpam.DiscordMessage
#if RUST
                || _pluginConfig.PluginSupport.AntiSpam.TeamMessage
#endif
            )
            {
                if (AntiSpam == null)
                {
                    PrintWarning("AntiSpam is enabled in the config but is not loaded. " +
                                 "Please disable the setting in the config or load AntiSpam: https://umod.org/plugins/anti-spam");
                    return;
                }

                if (AntiSpam.Version < new VersionNumber(2, 0, 0))
                {
                    PrintError("AntiSpam plugin must be version 2.0.0 or higher");
                }
            }

            if (string.IsNullOrEmpty(_pluginConfig.DiscordApiKey))
            {
                PrintWarning("Please set the Discord Bot Token and reload the plugin");
                return;
            }

            _client.Connect(_discordSettings);

            if (!_pluginConfig.ChannelSettings.ChatChannel.IsValid()
#if RUST
                && !_pluginConfig.ChannelSettings.TeamChannel.IsValid()
                && !_pluginConfig.ChannelSettings.CardsChannel.IsValid()
#endif
            )
            {
#if RUST
                Unsubscribe(nameof(OnPlayerChat));
#else
                Unsubscribe(nameof(OnUserChat));
#endif
            }

            if (!_pluginConfig.ChannelSettings.JoinLeaveChannel.IsValid())
            {
                Unsubscribe(nameof(OnUserConnected));
                Unsubscribe(nameof(OnUserDisconnected));
            }

            if (!_pluginConfig.ChannelSettings.OnlineOfflineChannel.IsValid())
            {
                Unsubscribe(nameof(OnServerShutdown));
            }
        }

        private void OnServerShutdown()
        {
            string message = JsonConvert.SerializeObject(new MessageCreate { Content = Lang(LangKeys.Discord.OnlineOffline.OfflineMessage) }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            webrequest.Enqueue( $"{Request.UrlBase}/{Request.ApiVersion}/channels/{_pluginConfig.ChannelSettings.OnlineOfflineChannel.ToString()}/messages",
                message,
                (i, s) => { },
                null,
                RequestMethod.POST,
                new Dictionary<string, string> { ["Authorization"] = $"Bot {_pluginConfig.DiscordApiKey}", ["Content-Type"] = "application/json" });
        }

        private void Unload()
        {
            _ins = null;
        }
        #endregion

        #region Discord Setup
        [HookMethod(DiscordExtHooks.OnDiscordGatewayReady)]
        private void OnDiscordGatewayReady(GatewayReadyEvent ready)
        {
            if (ready.Guilds.Count == 0)
            {
                PrintError("Your bot was not found in any discord servers. Please invite it to a server and reload the plugin.");
                return;
            }

            DiscordGuild guild = null;
            if (ready.Guilds.Count == 1 && !_pluginConfig.GuildId.IsValid())
            {
                guild = ready.Guilds.Values.FirstOrDefault();
            }

            if (guild == null)
            {
                guild = ready.Guilds[_pluginConfig.GuildId];
                if (guild == null)
                {
                    PrintError("Failed to find a matching guild for the Discord Server Id. " +
                               "Please make sure your guild Id is correct and the bot is in the discord server.");
                    return;
                }
            }

            DiscordApplication app = _client.Bot.Application;
            if (!app.HasApplicationFlag(ApplicationFlags.GatewayMessageContentLimited) && !app.HasApplicationFlag(ApplicationFlags.GatewayMessageContent))
            {
                PrintWarning($"You will need to enable \"Message Content Intent\" for {_client.Bot.BotUser.Username} @ https://discord.com/developers/applications\n by April 2022" +
                             $"{Name} will stop function correctly after that date until that is fixed. Once updated please reload {Name}.");
            }

            _guildId = guild.Id;
            Puts($"{Title} Ready");
        }

        [HookMethod(DiscordExtHooks.OnDiscordGuildCreated)]
        private void OnDiscordGuildCreated(DiscordGuild guild)
        {
            if (guild.Id != _guildId)
            {
                return;
            }

            _guild = guild;
            if (_pluginConfig.MessageSettings.DiscordToServer)
            {
                SetupChannel(MessageType.Server, _pluginConfig.ChannelSettings.ChatChannel, _pluginConfig.MessageSettings.UseBotMessageDisplay, HandleDiscordChatMessage);
            }
            else
            {
                SetupChannel(MessageType.Server, _pluginConfig.ChannelSettings.ChatChannel, _pluginConfig.MessageSettings.UseBotMessageDisplay);
            }

            SetupChannel(MessageType.JoinLeave, _pluginConfig.ChannelSettings.JoinLeaveChannel, false);
            SetupChannel(MessageType.OnlineOffline, _pluginConfig.ChannelSettings.OnlineOfflineChannel, false);
            SetupChannel(MessageType.AdminChat, _pluginConfig.PluginSupport.AdminChat.ChatChannel, _pluginConfig.MessageSettings.UseBotMessageDisplay, HandleAdminChatDiscordMessage);

#if RUST
            SetupChannel(MessageType.Team, _pluginConfig.ChannelSettings.TeamChannel, false);
            SetupChannel(MessageType.Cards, _pluginConfig.ChannelSettings.CardsChannel, false);
#endif

            if (_isServerStartup)
            {
                _sends[MessageType.OnlineOffline]?.QueueMessage(Lang(LangKeys.Discord.OnlineOffline.OnlineMessage));
            }
        }

        public void SetupChannel(MessageType messageChannel, Snowflake id, bool wipeNonBotMessages, Action<DiscordMessage> message = null)
        {
            if (!id.IsValid())
            {
                return;
            }

            DiscordChannel channel = _guild.Channels[id];
            if (channel == null)
            {
                PrintWarning($"Channel with ID: '{id}' not found in guild");
                return;
            }

            if (message != null)
            {
                _subscriptions.AddChannelSubscription(this, id, message);
            }

            if (wipeNonBotMessages)
            {
                channel.GetChannelMessages(_client, new ChannelMessagesRequest{Limit = 100}, messages => OnGetChannelMessages(messages, channel));
            }

            _sends[messageChannel] = new DiscordTimedSend(id);
        }

        private void OnGetChannelMessages(List<DiscordMessage> messages, DiscordChannel channel)
        {
            if (messages.Count == 0)
            {
                return;
            }

            DiscordMessage[] messagesToDelete = messages
                                                .Where(m => !ShouldIgnoreDiscordUser(m.Author))
                                                .ToArray();

            if (messagesToDelete.Length == 0)
            {
                return;
            }

            if (messagesToDelete.Length == 1)
            {
                messagesToDelete[0]?.DeleteMessage(_client);
                return;
            }

            channel.BulkDeleteMessages(_client, messagesToDelete.Take(100).Select(m => m.Id).ToArray());
        }
        #endregion

        #region Oxide Chat Hooks
#if RUST
        private void OnPlayerChat(BasePlayer rustPlayer, string message, Chat.ChatChannel chatChannel)
        {
            IPlayer player = rustPlayer.IPlayer;
            int channel = (int)chatChannel;

#else
        private void OnUserChat(IPlayer player, string message)
        {
            int channel = 0;
#endif

            if (channel == 0 && !_pluginConfig.MessageSettings.ServerToDiscord)
            {
                return;
            }

            //Ignore Admin Chat Messages
            //Processed by OnAdminChat Hook
            if (IsAdminChatMessage(player, message))
            {
                return;
            }

            MessageType type = GetMessageType(message, channel, player);
            //Check if type is enabled
            if (!_sends.ContainsKey(type))
            {
                return;
            }

            if (IsInAdminDeepCover(player, type))
            {
                return;
            }

            GetMessage(message, type, newMessage => { _sends[type]?.QueueMessage(FormatServerMessage(player, type, newMessage.ToString())); });
        }

        private MessageType GetMessageType(string message, int channel, IPlayer player)
        {
            switch (channel)
            {
                case 1:
                    return MessageType.Team;
                case 3:
                    return MessageType.Cards;
                default:
                    return IsAdminChatMessage(player, message) ? MessageType.AdminChat : MessageType.Server;
            }
        }
        #endregion

        #region Discord Hooks
        public void HandleDiscordChatMessage(DiscordMessage message)
        {
            if (ShouldIgnoreDiscordUser(message.Author))
            {
                return;
            }

            if (ShouldIgnorePrefix(message.Content))
            {
                return;
            }

            IPlayer player = message.Author.Player;
            if (Interface.Oxide.CallHook("OnDiscordChatMessage", player, message.Content, message.Author) != null)
            {
                return;
            }

            if (player != null && IsBetterChatMuted(player))
            {
                if (_pluginConfig.MessageSettings.UseBotMessageDisplay)
                {
                    timer.In(.25f, () =>
                    {
                        message.DeleteMessage(_client);
                    });
                }
                return;
            }

            ProcessMentions(message);
            GetMessage(message.Content, MessageType.Discord, sb =>
            {
                if (player == null)
                {
                    if (!IsUnlinkedBlocked(message))
                    {
                        HandleUnlinkedMessage(message, sb);
                    }
                }
                else
                {
                    HandleLinkedMessage(player, sb, message);
                }
            });
        }

        public void HandleLinkedMessage(IPlayer player, StringBuilder sb, DiscordMessage discordMessage)
        {
            string message = sb.ToString();
            if (string.IsNullOrEmpty(message) || message.Trim().Length == 0)
            {
                discordMessage.DeleteMessage(_client);
                return;
            }

            if (_pluginConfig.MessageSettings.UseBotMessageDisplay)
            {
                _sends[MessageType.Server].QueueMessage(FormatServerMessage(player, MessageType.Discord, message));
                discordMessage.DeleteMessage(_client);
            }

            if (_pluginConfig.MessageSettings.AllowPluginProcessing)
            {
                bool playerReturn = false;
#if RUST
                //Let other chat plugins process first
                if (player.Object != null)
                {
                    Unsubscribe(nameof(OnPlayerChat));
                    playerReturn = Interface.Call(nameof(OnPlayerChat), player.Object, message, Chat.ChatChannel.Global) != null;
                    Subscribe(nameof(OnPlayerChat));
                }
#endif

                //Let other chat plugins process first
                Unsubscribe("OnUserChat");
                bool userReturn = Interface.Call("OnUserChat", player, message) != null;
                Subscribe("OnUserChat");

                if (playerReturn || userReturn)
                {
                    return;
                }

                if (SendBetterChatMessage(player, message))
                {
                    return;
                }
            }

            string discordTag = string.Empty;
            if (_pluginConfig.EnableServerChatTag)
            {
                discordTag = Lang(LangKeys.Server.DiscordTag, player);
            }

            message = Lang(LangKeys.Server.LinkedMessage, player, discordTag, player.Name, message);
            server.Broadcast(message);
            Puts(Formatter.ToPlaintext(message));
        }

        public void HandleUnlinkedMessage(DiscordMessage discordMessage, StringBuilder sb)
        {
            string message = sb.ToString();
            if (string.IsNullOrEmpty(message) || message.Trim().Length == 0)
            {
                discordMessage.DeleteMessage(_client);
                return;
            }

            if (_pluginConfig.MessageSettings.UseBotMessageDisplay)
            {
                _sends[MessageType.Server].QueueMessage(Lang(LangKeys.Discord.ChatChannel.UnlinkedMessage, null, GetServerTime(), discordMessage.Author.Username, discordMessage.Author.Discriminator, message));
                discordMessage.DeleteMessage(_client);
            }

            string name = _guild.Members[discordMessage.Author.Id]?.DisplayName ?? discordMessage.Author.Username;

            string serverMessage = Lang(LangKeys.Server.UnlinkedMessage, null, Lang(LangKeys.Server.DiscordTag), name, discordMessage.Author.Discriminator, message);
            server.Broadcast(serverMessage);
            Puts(Formatter.ToPlaintext(serverMessage));
        }
        #endregion

        #region Join Leave Handling
        private void OnUserConnected(IPlayer player)
        {
            _sends[MessageType.JoinLeave].QueueMessage(Lang(LangKeys.Discord.JoinLeave.ConnectedMessage, player, GetServerTime(), GetPlayerName(player), player.Id));
        }

        private void OnUserDisconnected(IPlayer player, string reason)
        {
            _sends[MessageType.JoinLeave].QueueMessage(Lang(LangKeys.Discord.JoinLeave.DisconnectedMessage, player, GetServerTime(), GetPlayerName(player), reason, player.Id));
        }
        #endregion

        #region Plugin Support
        #region AdminDeepCover
        public bool IsInAdminDeepCover(IPlayer player, MessageType type)
        {
            return IsPluginLoaded(AdminDeepCover)
                   && player.Object != null
                   && (type == MessageType.Server || type == MessageType.Discord)
                   && AdminDeepCover.Call<bool>("API_IsDeepCovered", player.Object);
        }
        #endregion

        #region AdminChat
        private const string AdminChatPermission = "adminchat.use";

        public bool IsAdminChatEnabled() => IsPluginLoaded(AdminChat) && _pluginConfig.PluginSupport.AdminChat.Enabled && _sends.ContainsKey(MessageType.AdminChat);
        public bool CanPlayerAdminChat(IPlayer player) => IsAdminChatEnabled() && player.HasPermission(AdminChatPermission);
        public bool IsAdminChatMessage(IPlayer player, string message) => CanPlayerAdminChat(player) && (message.StartsWith(_pluginConfig.PluginSupport.AdminChat.AdminChatPrefix) || AdminChat.Call<bool>("HasAdminChatEnabled", player));

        //Hook called from AdminChat Plugin
        [HookMethod(nameof(OnAdminChat))]
        public void OnAdminChat(IPlayer player, string message)
        {
            if (IsAdminChatEnabled())
            {
                _sends[MessageType.AdminChat].QueueMessage(Lang(LangKeys.Discord.AdminChat.Message, player, GetServerTime(), player.Name, message));
            }
        }

        //If this is an admin chat message from the server ignore it as we use hook to process.
        public bool HandledAdminChatServerMessage(IPlayer player, string message)
        {
            return IsAdminChatMessage(player, message);
        }

        //Message sent in admin chat channel. Process bot replace and sending to server
        public void HandleAdminChatDiscordMessage(DiscordMessage message)
        {
            AdminChatSettings settings = _pluginConfig.PluginSupport.AdminChat;

            IPlayer player = message.Author.Player;
            if (player == null)
            {
                message.Reply(_client, Lang(LangKeys.Discord.NotLinkedError));
                return;
            }

            if (!CanPlayerAdminChat(player))
            {
                message.Reply(_client, Lang(LangKeys.Discord.AdminChat.Permission, player));
                return;
            }

            if (settings.ReplaceWithBot)
            {
                timer.In(.25f, () => { message.DeleteMessage(_client); });

                _sends[MessageType.AdminChat].QueueMessage(Lang(LangKeys.Discord.AdminChat.Message, player, GetServerTime(), player.Name, message.Content));
            }

            AdminChat.Call("SendAdminMessage", player, message.Content);
        }
        #endregion

        #region AntiSpam
        public bool AntiSpamLoaded() => AntiSpam != null && AntiSpam.IsLoaded;
        public bool CanAntiSpamPlayerNames() => AntiSpamLoaded() && _pluginConfig.PluginSupport.AntiSpam.ValidateNicknames;
        public bool CanAntiSpamGlobalMessage() => AntiSpamLoaded() && _pluginConfig.PluginSupport.AntiSpam.ServerMessage;
        public bool CanAntiSpamDiscordMessage() => AntiSpamLoaded() && _pluginConfig.PluginSupport.AntiSpam.DiscordMessage;
#if RUST
        public bool CanAntiSpamTeamMessage() => AntiSpamLoaded() && _pluginConfig.PluginSupport.AntiSpam.TeamMessage;
        public bool CanAntiSpamCardsMessage() => AntiSpamLoaded() && _pluginConfig.PluginSupport.AntiSpam.CardMessages;
#endif
        public bool CanAntiSpam(MessageType type)
        {
            switch (type)
            {
                case MessageType.Server:
                    return CanAntiSpamGlobalMessage();

                case MessageType.Discord:
                    return CanAntiSpamDiscordMessage();

#if RUST
                case MessageType.Team:
                    return CanAntiSpamTeamMessage();

                case MessageType.Cards:
                    return CanAntiSpamCardsMessage();
#endif
            }

            return false;
        }

        public void ProcessMessageAntiSpam(StringBuilder message, MessageType type)
        {
            if (CanAntiSpam(type))
            {
                string clearMessage = AntiSpam.Call<string>("GetSpamFreeText", message.ToString());
                message.Length = 0;
                message.Append(clearMessage);
            }
        }

        public void ProcessNameAntiSpam(IPlayer player, StringBuilder sb)
        {
            if (CanAntiSpamPlayerNames())
            {
                sb.Length = 0;
                sb.Append(AntiSpam.Call<string>("GetClearName", player));
            }
        }
        #endregion

        #region BetterChat
        public bool CanBetterChat() => IsPluginLoaded(BetterChat);

        public bool SendBetterChatMessage(IPlayer player, string message)
        {
            if (CanBetterChat())
            {
                Dictionary<string, object> data = BetterChat.Call<Dictionary<string, object>>("API_GetMessageData", player, message);
                BetterChat.Call("API_SendMessage", data);
                return true;
            }

            return false;
        }

        public void GetBetterChatMessage(IPlayer player, string message, out string playerInfo, out string messageInfo)
        {
            string bcMessage = GetBetterChatConsoleMessage(player, message);
            int index = bcMessage.IndexOf(':');

            if (index != -1)
            {
                playerInfo = bcMessage.Substring(0, index);
                messageInfo = bcMessage.Substring(index + 2);
            }
            else
            {
                playerInfo = string.Empty;
                messageInfo = bcMessage;
            }
        }

        public string GetBetterChatConsoleMessage(IPlayer player, string message)
        {
            return BetterChat.Call<string>("API_GetFormattedMessage", player, message, true);
        }

        public string GetBetterChatTag(IPlayer player)
        {
            return player.IsConnected ? null : Lang(LangKeys.Server.DiscordTag, player);
        }
        #endregion

        #region BetterChatMute
        public bool IsBetterChatMuted(IPlayer player) => IsPluginLoaded(BetterChatMute) && _pluginConfig.PluginSupport.BetterChatMuteSettings.IgnoreMuted && BetterChatMute.Call<bool>("API_IsMuted", player);
        #endregion

        #region Clans
        public void ProcessClanName(StringBuilder name, IPlayer player)
        {
            if (!_pluginConfig.PluginSupport.Clans.ShowClanTag || !IsPluginLoaded(Clans))
            {
                return;
            }

            string clanTag = Clans.Call<string>("GetClanOf", player.Id);
            if (!string.IsNullOrEmpty(clanTag))
            {
                name.Insert(0, Lang(LangKeys.Server.ClanTag, player, clanTag));
            }
        }
        #endregion

        #region Translation API
        public bool CanChatTranslator() => IsPluginLoaded(ChatTranslator) && _pluginConfig.PluginSupport.ChatTranslator.Enabled;

        public bool CanChatTranslatorGlobalMessage() => CanChatTranslator() && _pluginConfig.PluginSupport.ChatTranslator.ServerMessage;
        public bool CanChatTranslatorDiscordMessage() => CanChatTranslator() && _pluginConfig.PluginSupport.ChatTranslator.DiscordMessage;
#if RUST
        public bool CanChatTranslatorTeamMessage() => CanChatTranslator() && _pluginConfig.PluginSupport.ChatTranslator.TeamMessage;
        public bool CanChatTranslatorCardsMessage() => CanChatTranslator() && _pluginConfig.PluginSupport.ChatTranslator.CardMessages;
#endif

        public bool CanChatTranslatorSource(MessageType type)
        {
            switch (type)
            {
                case MessageType.Server:
                    return CanChatTranslatorGlobalMessage();

                case MessageType.Discord:
                    return CanChatTranslatorDiscordMessage();

#if RUST
                case MessageType.Team:
                    return CanChatTranslatorTeamMessage();

                case MessageType.Cards:
                    return CanChatTranslatorCardsMessage();
#endif
            }

            return false;
        }

        public void TranslateMessage(string message, MessageType type, Action<StringBuilder> callback)
        {
            if (CanChatTranslatorSource(type))
            {
                TranslationAPI.Call("Translate", message, _pluginConfig.PluginSupport.ChatTranslator.DiscordServerLanguage, "auto", new Action<string>(translatedText =>
                {
                    _message.Length = 0;
                    _message.Append(message);
                    callback.Invoke(_message);
                }));
            }

            _message.Length = 0;
            _message.Append(message);
            callback.Invoke(_message);
        }
        #endregion

        #region UFilter
        public bool IsUFilterLoaded() => UFilter != null && UFilter.IsLoaded;
        public bool CanUFilterPlayerNames() => IsUFilterLoaded() && _pluginConfig.PluginSupport.UFilter.ValidateNicknames;
        public bool CanUFilterGlobalMessage() => IsUFilterLoaded() && _pluginConfig.PluginSupport.UFilter.GlobalMessage;
        public bool CanUFilterDiscordMessage() => IsUFilterLoaded() && _pluginConfig.PluginSupport.UFilter.DiscordMessages;
#if RUST
        public bool CanUFilterTeamMessage() => IsUFilterLoaded() && _pluginConfig.PluginSupport.UFilter.TeamMessage;
        public bool CanUFilterCardMessage() => IsUFilterLoaded() && _pluginConfig.PluginSupport.UFilter.CardMessage;
#endif

        public bool CanUFilter(MessageType type)
        {
            switch (type)
            {
                case MessageType.Server:
                    return CanUFilterGlobalMessage();

                case MessageType.Discord:
                    return CanUFilterDiscordMessage();

#if RUST
                case MessageType.Team:
                    return CanUFilterTeamMessage();

                case MessageType.Cards:
                    return CanUFilterCardMessage();
#endif
            }

            return false;
        }

        public void ProcessMessageUFilter(StringBuilder text, MessageType type)
        {
            if (CanUFilter(type))
            {
                UFilterText(text);
            }
        }

        public void ProcessNameUFilter(StringBuilder name)
        {
            if (CanUFilterPlayerNames())
            {
                UFilterText(name);
            }
        }

        public void UFilterText(StringBuilder text)
        {
            string[] profanities = UFilter.Call<string[]>("Profanities", text.ToString());
            if (profanities.Length == 0)
            {
                return;
            }

            foreach (string profanity in profanities)
            {
                text.Replace(profanity, new string('＊', profanity.Length));
            }
        }
        #endregion
        #endregion

        #region Helpers
        private DateTime GetServerTime()
        {
            return DateTime.Now + TimeSpan.FromHours(_pluginConfig.MessageSettings.ServerTimeOffset);
        }

        private void ProcessTextReplacements(StringBuilder message)
        {
            foreach (KeyValuePair<string, string> replacement in _pluginConfig.MessageSettings.TextReplacements)
            {
                message.Replace(replacement.Key, replacement.Value);
            }
        }

        private void SendMessageToChannel(Snowflake channelId, StringBuilder message)
        {
            DiscordMessage.CreateMessage(_client, channelId, new MessageCreate
            {
                Content = message.ToString(),
                AllowedMention = _allowedMention
            });
        }

        public string GetPlayerName(IPlayer player)
        {
            _name.Length = 0;
            _name.Append(player.Name);
            ProcessNameAntiSpam(player, _name);
            ProcessNameUFilter(_name);
            ProcessClanName(_name, player);

            return _name.ToString();
        }
        
        private void ProcessMentions(DiscordMessage message)
        {
            _mentions.Length = 0;
            _mentions.Append(message.Content);

            bool changed = false;
            if (message.Mentions != null)
            {
                foreach (KeyValuePair<Snowflake, DiscordUser> mention in message.Mentions)
                {
                    _mentions.Replace($"<@{mention.Key.ToString()}>", $"@{mention.Value.Username}");
                    changed = true;
                }
            
                foreach (KeyValuePair<Snowflake, DiscordUser> mention in message.Mentions)
                {
                    GuildMember member = _guild.Members[mention.Key];
                    _mentions.Replace($"<@!{mention.Key.ToString()}>", $"@{member?.Nickname ?? mention.Value.Username}");
                    changed = true;
                }
            }
            
            if (message.MentionsChannels != null)
            {
                foreach (KeyValuePair<Snowflake, ChannelMention> mention in message.MentionsChannels)
                {
                    _mentions.Replace($"<#{mention.Key.ToString()}>", $"#{mention.Value.Name}");
                    changed = true;
                }
            }

            foreach (Match match in _channelMention.Matches(message.Content))
            {
                string value = match.Value;
                Snowflake id = new Snowflake(value.Substring(2, value.Length - 3));
                DiscordChannel channel = _guild.Channels[id];
                if (channel != null)
                {
                    _mentions.Replace(value, $"#{channel.Name}");
                }

                changed = true;
            }

            if (message.MentionRoles != null)
            {
                foreach (Snowflake roleId in message.MentionRoles)
                {
                    DiscordRole role = _guild.Roles[roleId];
                    _mentions.Replace($"<@&{roleId.ToString()}>", $"@{role.Name ?? roleId}");
                    changed = true;
                }
            }

            if (changed)
            {
                message.Content = _mentions.ToString();
            }
        }

        public void GetMessage(string message, MessageType type, Action<StringBuilder> callback)
        {
            TranslateMessage(message, type, sb =>
            {
                ProcessTextReplacements(sb);
                ProcessMessageUFilter(sb, type);
                ProcessMessageAntiSpam(sb, type);

                callback.Invoke(sb);
            });
        }

        private bool IsUnlinkedBlocked(DiscordMessage message)
        {
            if (!_pluginConfig.MessageSettings.UnlinkedSettings.AllowedUnlinked)
            {
                message.Reply(_client, Lang(LangKeys.Discord.NotLinkedError), newMessage => { timer.In(5f, () => { newMessage.DeleteMessage(_client); }); });

                return true;
            }

            return false;
        }

        public string FormatServerMessage(IPlayer player, MessageType type, string message)
        {
            string format;
            switch (type)
            {
                case MessageType.Server:
                    format = LangKeys.Discord.ChatChannel.Message;
                    break;

                case MessageType.Team:
                    format = LangKeys.Discord.TeamChannel.Message;
                    break;

                case MessageType.Cards:
                    format = LangKeys.Discord.CardsChannel.Message;
                    break;

                case MessageType.AdminChat:
                    format = LangKeys.Discord.AdminChat.Message;
                    break;

                case MessageType.Discord:
                    format = LangKeys.Discord.ChatChannel.LinkedMessage;
                    break;

                default:
                    return message;
            }

            string playerName;

            if (_pluginConfig.MessageSettings.AllowPluginProcessing && type != MessageType.AdminChat && CanBetterChat())
            {
                GetBetterChatMessage(player, message, out playerName, out message);
            }
            else
            {
                playerName = GetPlayerName(player);
            }

            return Lang(format, player, GetServerTime(), playerName, message);
        }

        public bool ShouldIgnoreDiscordUser(DiscordUser user)
        {
            if (user.Bot ?? false)
            {
                return true;
            }

            MessageFilterSettings filter = _pluginConfig.MessageSettings.Filter;
            if (filter.IgnoreUsers.Contains(user.Id))
            {
                return true;
            }

            GuildMember member = _guild.Members[user.Id];
            if (member != null)
            {
                foreach (Snowflake role in filter.IgnoreRoles)
                {
                    if (member.Roles.Contains(role))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool ShouldIgnorePrefix(string message)
        {
            for (int index = 0; index < _pluginConfig.MessageSettings.Filter.IgnoredPrefixes.Count; index++)
            {
                string prefix = _pluginConfig.MessageSettings.Filter.IgnoredPrefixes[index];
                if (message.StartsWith(prefix))
                {
                    return true;
                }
            }

            return false;
        }
        
        public bool IsPluginLoaded(Plugin plugin) => plugin != null && plugin.IsLoaded;

        public string Lang(string key, IPlayer player = null)
        {
            return lang.GetMessage(key, this, player?.Id);
        }
        
        public string Lang(string key, IPlayer player = null, params object[] args)
        {
            string lang = Lang(key, player);
            try
            {
                return string.Format(lang, args);
            }
            catch (Exception ex)
            {
                PrintError($"Lang Key '{key}'\nMessage:{lang}\nException:{ex}");
                throw;
            }
        }
        #endregion

        #region Classes
        public class PluginConfig
        {
            [JsonProperty(PropertyName = "Discord Bot Token")]
            public string DiscordApiKey { get; set; } = string.Empty;

            [JsonProperty(PropertyName = "Discord Server ID (Optional if bot only in 1 guild)")]
            public Snowflake GuildId { get; set; }

            [DefaultValue(true)]
            [JsonProperty("Enable Adding Discord Tag To In Game Messages When Sent From Discord")]
            public bool EnableServerChatTag { get; set; } = true;

            [JsonProperty("Channel Settings")]
            public ChannelSettings ChannelSettings { get; set; }

            [JsonProperty("Message Settings")]
            public MessageSettings MessageSettings { get; set; }

            [JsonProperty("Plugin Support")]
            public PluginSupport PluginSupport { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(DiscordLogLevel.Info)]
            [JsonProperty(PropertyName = "Discord Extension Log Level (Verbose, Debug, Info, Warning, Error, Exception, Off)")]
            public DiscordLogLevel ExtensionDebugging { get; set; } = DiscordLogLevel.Info;
        }

        public class ChannelSettings
        {
            [JsonProperty("Chat Channel ID")]
            public Snowflake ChatChannel { get; set; }

#if RUST
            [JsonProperty("Team Channel ID")]
            public Snowflake TeamChannel { get; set; }

            [JsonProperty("Cards Channel ID")]
            public Snowflake CardsChannel { get; set; }
#endif

            [JsonProperty("Join / Leave Channel ID")]
            public Snowflake JoinLeaveChannel { get; set; }

            [JsonProperty("Server Online / Offline Channel ID")]
            public Snowflake OnlineOfflineChannel { get; set; }

            public ChannelSettings(ChannelSettings settings)
            {
                ChatChannel = settings?.ChatChannel ?? default(Snowflake);
                JoinLeaveChannel = settings?.JoinLeaveChannel ?? default(Snowflake);
                OnlineOfflineChannel = settings?.OnlineOfflineChannel ?? default(Snowflake);
#if RUST
                TeamChannel = settings?.TeamChannel ?? default(Snowflake);
                CardsChannel = settings?.CardsChannel ?? default(Snowflake);
#endif
            }
        }

        public class MessageSettings
        {
            [JsonProperty("Replace Discord User Message With Bot Message")]
            public bool UseBotMessageDisplay { get; set; }

            [JsonProperty("Send Messages From Server Chat To Discord Channel")]
            public bool ServerToDiscord { get; set; }

            [JsonProperty("Send Messages From Discord Channel To Server Chat")]
            public bool DiscordToServer { get; set; }

            [JsonProperty("Allow plugins to process Discord to Server Chat Messages")]
            public bool AllowPluginProcessing { get; set; }

            [JsonProperty("Discord Message Server Time Offset (Hours)")]
            public float ServerTimeOffset { get; set; }

            [JsonProperty("Text Replacements")]
            public Hash<string, string> TextReplacements { get; set; }

            [JsonProperty("Unlinked Settings")]
            public UnlinkedSettings UnlinkedSettings { get; set; }

            [JsonProperty("Message Filter Settings")]
            public MessageFilterSettings Filter { get; set; }

            public MessageSettings(MessageSettings settings)
            {
                UseBotMessageDisplay = settings?.UseBotMessageDisplay ?? true;
                ServerToDiscord = settings?.ServerToDiscord ?? true;
                DiscordToServer = settings?.DiscordToServer ?? true;
                AllowPluginProcessing = settings?.AllowPluginProcessing ?? true;
                ServerTimeOffset = settings?.ServerTimeOffset ?? 0f;
                TextReplacements = settings?.TextReplacements ?? new Hash<string, string> { ["TextToBeReplaced"] = "ReplacedText" };
                UnlinkedSettings = new UnlinkedSettings(settings?.UnlinkedSettings);
                Filter = new MessageFilterSettings(settings?.Filter);
            }
        }

        public class UnlinkedSettings
        {
            [JsonProperty("Allow Unlinked Players To Chat With Server")]
            public bool AllowedUnlinked { get; set; }

#if RUST
            [JsonProperty("Steam Icon ID")]
            public ulong SteamIcon { get; set; }
#endif

            public UnlinkedSettings(UnlinkedSettings settings)
            {
                AllowedUnlinked = settings?.AllowedUnlinked ?? true;
#if RUST
                SteamIcon = settings?.SteamIcon ?? 76561199144296099;
#endif
            }
        }

        public class MessageFilterSettings
        {
            [JsonProperty("Ignore messages from users in this list (Discord ID)")]
            public List<Snowflake> IgnoreUsers { get; set; }

            [JsonProperty("Ignore messages from users in this role (Role ID)")]
            public List<Snowflake> IgnoreRoles { get; set; }

            [JsonProperty("Ignored Prefixes")]
            public List<string> IgnoredPrefixes { get; set; }

            public MessageFilterSettings(MessageFilterSettings settings)
            {
                IgnoreUsers = settings?.IgnoreUsers ?? new List<Snowflake>();
                IgnoreRoles = settings?.IgnoreRoles ?? new List<Snowflake>();
                IgnoredPrefixes = settings?.IgnoredPrefixes ?? new List<string>();
            }
        }

        public class PluginSupport
        {
            [JsonProperty("AdminChat Settings")]
            public AdminChatSettings AdminChat { get; set; }

            [JsonProperty("AntiSpam Settings")]
            public AntiSpamSettings AntiSpam { get; set; }

            [JsonProperty("BetterChatMute Settings")]
            public BetterChatMuteSettings BetterChatMuteSettings { get; set; }

            [JsonProperty("ChatTranslator Settings")]
            public ChatTranslatorSettings ChatTranslator { get; set; }

            [JsonProperty("Clan Settings")]
            public ClanSettings Clans { get; set; }

            [JsonProperty("UFilter Settings")]
            public UFilterSettings UFilter { get; set; }

            public PluginSupport(PluginSupport settings)
            {
                AdminChat = new AdminChatSettings(settings?.AdminChat);
                BetterChatMuteSettings = new BetterChatMuteSettings(settings?.BetterChatMuteSettings);
                ChatTranslator = new ChatTranslatorSettings(settings?.ChatTranslator);
                Clans = new ClanSettings(settings?.Clans);
                AntiSpam = new AntiSpamSettings(settings?.AntiSpam);
                UFilter = new UFilterSettings(settings?.UFilter);
            }
        }

        public class AdminChatSettings
        {
            [JsonProperty("Enable AdminChat Plugin Support")]
            public bool Enabled { get; set; }

            [JsonProperty("Chat Channel ID")]
            public Snowflake ChatChannel { get; set; }

            [JsonProperty("Chat Prefix")]
            public string AdminChatPrefix { get; set; }

            [JsonProperty("Replace Discord Message With Bot")]
            public bool ReplaceWithBot { get; set; }

            public AdminChatSettings(AdminChatSettings settings)
            {
                Enabled = settings?.Enabled ?? false;
                ChatChannel = settings?.ChatChannel ?? default(Snowflake);
                AdminChatPrefix = settings?.AdminChatPrefix ?? "@";
                ReplaceWithBot = settings?.ReplaceWithBot ?? true;
            }
        }

        public class BetterChatMuteSettings
        {
            [JsonProperty("Ignore Muted Players")]
            public bool IgnoreMuted { get; set; }

            public BetterChatMuteSettings(BetterChatMuteSettings settings)
            {
                IgnoreMuted = settings?.IgnoreMuted ?? true;
            }
        }

        public class ChatTranslatorSettings
        {
            [JsonProperty("Enable Chat Translator")]
            public bool Enabled { get; set; }

            [JsonProperty("Use ChatTranslator On Server Messages")]
            public bool ServerMessage { get; set; }

            [JsonProperty("Use ChatTranslator On Chat Messages")]
            public bool DiscordMessage { get; set; }

#if RUST
            [JsonProperty("Use ChatTranslator On Team Messages")]
            public bool TeamMessage { get; set; }

            [JsonProperty("Use ChatTranslator On Card Messages")]
            public bool CardMessages { get; set; }
#endif

            [JsonProperty("Discord Server Chat Language")]
            public string DiscordServerLanguage { get; set; }

            public ChatTranslatorSettings(ChatTranslatorSettings settings)
            {
                Enabled = settings?.Enabled ?? false;
                ServerMessage = settings?.ServerMessage ?? false;
                DiscordMessage = settings?.DiscordMessage ?? false;
#if RUST
                TeamMessage = settings?.TeamMessage ?? false;
                CardMessages = settings?.CardMessages ?? false;
#endif
                DiscordServerLanguage = settings?.DiscordServerLanguage ?? Interface.Oxide.GetLibrary<Lang>().GetServerLanguage();
            }
        }

        public class ClanSettings
        {
            [JsonProperty("Display Clan Tag")]
            public bool ShowClanTag { get; set; }

            public ClanSettings(ClanSettings settings)
            {
                ShowClanTag = settings?.ShowClanTag ?? true;
            }
        }

        public class AntiSpamSettings
        {
            [JsonProperty("Use AntiSpam On Player Names")]
            public bool ValidateNicknames { get; set; }

            [JsonProperty("Use AntiSpam On Server Messages")]
            public bool ServerMessage { get; set; }

            [JsonProperty("Use AntiSpam On Chat Messages")]
            public bool DiscordMessage { get; set; }

#if RUST
            [JsonProperty("Use AntiSpam On Team Messages")]
            public bool TeamMessage { get; set; }

            [JsonProperty("Use AntiSpam On Card Messages")]
            public bool CardMessages { get; set; }
#endif

            public AntiSpamSettings(AntiSpamSettings settings)
            {
                ValidateNicknames = settings?.ValidateNicknames ?? false;
                ServerMessage = settings?.ServerMessage ?? false;
                DiscordMessage = settings?.DiscordMessage ?? false;
#if RUST
                TeamMessage = settings?.TeamMessage ?? false;
                CardMessages = settings?.CardMessages ?? false;
#endif
            }
        }

        public class UFilterSettings
        {
            [JsonProperty("Use UFilter On Player Names")]
            public bool ValidateNicknames { get; set; }

            [JsonProperty("Use UFilter On Server Messages")]
            public bool GlobalMessage { get; set; }

            [JsonProperty("Use UFilter On Discord Messages")]
            public bool DiscordMessages { get; set; }

#if RUST
            [JsonProperty("Use UFilter On Team Messages")]
            public bool TeamMessage { get; set; }

            [JsonProperty("Use UFilter On Card Messages")]
            public bool CardMessage { get; set; }
#endif

            public UFilterSettings(UFilterSettings settings)
            {
                ValidateNicknames = settings?.ValidateNicknames ?? false;
                GlobalMessage = settings?.GlobalMessage ?? false;
                DiscordMessages = settings?.DiscordMessages ?? false;
#if RUST
                TeamMessage = settings?.TeamMessage ?? false;
                CardMessage = settings?.CardMessage ?? false;
#endif
            }
        }

        public class DiscordTimedSend
        {
            private readonly StringBuilder _message = new StringBuilder();
            private Timer _sendTimer;
            private readonly Snowflake _channelId;

            public DiscordTimedSend(Snowflake channelId)
            {
                _channelId = channelId;
            }

            public void QueueMessage(string message)
            {
                if (string.IsNullOrEmpty(message) || message.Trim().Length == 0)
                {
                    return;
                }
                
                if (_message.Length + message.Length > 2000)
                {
                    Send();
                }

                if (_sendTimer == null)
                {
                    _sendTimer = _ins.timer.In(1f, Send);
                }

                _message.AppendLine(message);
            }

            private void Send()
            {
                _ins.SendMessageToChannel(_channelId, _message);
                _message.Length = 0;
                _sendTimer = null;
            }
        }
        #endregion

        #region Lang
        private static class LangKeys
        {
            public const string Root = "V1.";

            public static class Discord
            {
                private const string Base = Root + nameof(Discord) + ".";

                public const string NotLinkedError = Base + nameof(NotLinkedError);

                public static class ChatChannel
                {
                    private const string Base = Discord.Base + nameof(ChatChannel) + ".";

                    public const string Message = Base + nameof(Message);
                    public const string LinkedMessage = Base + nameof(LinkedMessage);
                    public const string UnlinkedMessage = Base + nameof(UnlinkedMessage);
                }

                public static class TeamChannel
                {
                    private const string Base = Discord.Base + nameof(TeamChannel) + ".";

                    public const string Message = Base + nameof(Message);
                }

                public static class CardsChannel
                {
                    private const string Base = Discord.Base + nameof(CardsChannel) + ".";

                    public const string Message = Base + nameof(Message);
                }

                public static class JoinLeave
                {
                    private const string Base = Discord.Base + nameof(JoinLeave) + ".";

                    public const string ConnectedMessage = Base + nameof(ConnectedMessage);
                    public const string DisconnectedMessage = Base + nameof(DisconnectedMessage);
                }

                public static class OnlineOffline
                {
                    private const string Base = Discord.Base + nameof(OnlineOffline) + ".";

                    public const string OnlineMessage = Base + nameof(OnlineMessage);
                    public const string OfflineMessage = Base + nameof(OfflineMessage);
                }

                public static class AdminChat
                {
                    private const string Base = Discord.Base + nameof(AdminChat) + ".";

                    public const string Message = Base + nameof(Message);
                    public const string Permission = Base + nameof(Permission);
                }
            }

            public static class Server
            {
                private const string Base = Root + nameof(Server) + ".";

                public const string LinkedMessage = Base + nameof(LinkedMessage);
                public const string UnlinkedMessage = Base + nameof(UnlinkedMessage);
                public const string DiscordTag = Base + nameof(DiscordTag);
                public const string ClanTag = Base + nameof(ClanTag);
            }
        }
        #endregion
    }
}