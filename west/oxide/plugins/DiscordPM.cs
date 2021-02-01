using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Discord PM", "MJSU", "0.11.5")]
    [Description("Allows private messaging through discord")]
    internal class DiscordPM : CovalencePlugin
    {
        #region Class Fields
        [PluginReference] private Plugin DiscordCore, EffectAnnouncements;

        private PluginConfig _pluginConfig; //Plugin Config

        private const string AccentColor = "de8732";
        private const string EffectsKey = "DiscordPm-Pm";
        private const string DefaultPmEffect = "assets/prefabs/locks/keypad/effects/lock.code.lock.prefab";

        private readonly Hash<string, string> _replys = new Hash<string, string>();

        private bool _init;
        #endregion

        #region Setup & Loading

        private void Init()
        {
            ConfigLoad();

            AddCovalenceCommand(_pluginConfig.GamePm, nameof(DiscordPrivateMessageChatCommand));
            AddCovalenceCommand(_pluginConfig.GameReply, nameof(DiscordReplyChatCommand));
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        private void ConfigLoad()
        {
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            return config;
        }

        private void Loaded()
        {
            if (DiscordCore == null)
            {
                PrintError("Missing plugin dependency DiscordCore: https://umod.org/plugins/discord-core");
                return;
            }

            OnDiscordCoreReady();
        }

        private void OnDiscordCoreReady()
        {
            if (!(DiscordCore?.Call<bool>("IsReady") ?? false))
            {
                return;
            }

            DiscordCore.Call("RegisterCommand", _pluginConfig.DiscordPm, this, new Func<IPlayer, string, string, string[], object>(HandlePrivateMessage), LangKeys.DiscordPmInfoText);
            DiscordCore.Call("RegisterCommand", _pluginConfig.DiscordReply, this, new Func<IPlayer, string, string, string[], object>(HandleReply), LangKeys.DiscordReplyInfoText);
            _init = true;
#if RUST
            if (EffectAnnouncements != null)
            {
                if (!EffectAnnouncements.Call<bool>("IsEffectRegistered", EffectsKey))
                {
                    EffectAnnouncements.Call("RegisterEffect", EffectsKey, DefaultPmEffect);
                }
            }
#endif
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Chat] = $"[#BEBEBE][[#{AccentColor}]{Title}[/#]] {{0}}[/#]",
                [LangKeys.DiscordChatPrefix] = $"[#BEBEBE][#{AccentColor}]PM {{0}} {{1}}:[/#] {{2}}[/#]",
                [LangKeys.DiscordChatNoPrefix] = $"[#BEBEBE][#{AccentColor}]{{0}}:[/#] {{1}}[/#]",
                [LangKeys.DiscordPmInfoText] = "To private message a player on the rust server / discord",
                [LangKeys.DiscordReplyInfoText] = "To reply to a previous private message",
                [LangKeys.InvalidPmSyntax] = $"Invalid Syntax. Type [#{AccentColor}]/{{0}} MJSU Hi![/#]",
                [LangKeys.InvalidReplySyntax] = $"Invalid Syntax. Ex: [#{AccentColor}]/{{0}} Hi![/#]",
                [LangKeys.UnableToFindPlayer] = "Unable to find player",
                [LangKeys.NoPreviousPm] = "You do not have any previous discord PM's. Please use /pm to be able to use this command.",
                [LangKeys.From] = "from",
                [LangKeys.To] = "to"
            }, this);
        }

        private void Unload()
        {
            DiscordCore?.Call("UnregisterCommand", _pluginConfig.DiscordPm, this);
            DiscordCore?.Call("UnregisterCommand", _pluginConfig.DiscordReply, this);
        }
        #endregion

        #region Chat Commands
        private void DiscordPrivateMessageChatCommand(IPlayer player, string cmd, string[] args)
        {
            if (args.Length < 1)
            {
                Chat(player, Lang(LangKeys.InvalidPmSyntax, player, _pluginConfig.GamePm));
                return;
            }

            IPlayer iPlayer = covalence.Players.FindPlayer(args[0]);
            if (iPlayer == null)
            {
                Chat(player, Lang(LangKeys.UnableToFindPlayer, player));
                return;
            }

            _replys[player.Id] = iPlayer.Id;
            _replys[iPlayer.Id] = player.Id;

            string message = string.Join(" ", args.Skip(1).ToArray());
            ServerPrivateMessage(player, iPlayer, message, Lang(LangKeys.To, player));
            if (iPlayer.IsConnected)
            {
#if RUST
                EffectAnnouncements?.Call("RunEffectForPlayer", iPlayer.Object, EffectsKey);
#endif
                ServerPrivateMessage(iPlayer, player, message, Lang(LangKeys.From, iPlayer));
            }

            DiscordPrivateMessage(iPlayer.Id, player, message, Lang(LangKeys.From, iPlayer));

            Puts($"{player.Name} -> {iPlayer.Name}: {message}");
        }
        private void DiscordReplyChatCommand(IPlayer player, string cmd, string[] args)
        {
            if (args.Length < 1)
            {
                Chat(player, Lang(LangKeys.InvalidReplySyntax, player, _pluginConfig.GameReply));
                return;
            }

            string replyPlayer = _replys[player.Id];
            if (replyPlayer == null)
            {
                Chat(player, Lang(LangKeys.NoPreviousPm, player));
                return;
            }

            IPlayer iPlayer = covalence.Players.FindPlayerById(replyPlayer);
            if (iPlayer == null)
            {
                Chat(player, Lang(LangKeys.NoPreviousPm, player));
                return;
            }

            string message = string.Join(" ", args);
            ServerPrivateMessage(player, iPlayer, message, Lang(LangKeys.To, player));
            if (iPlayer.IsConnected)
            {
#if RUST
                EffectAnnouncements?.Call("RunEffectForPlayer", iPlayer.Object, EffectsKey);
#endif

                ServerPrivateMessage(iPlayer, player, message, Lang(LangKeys.From, iPlayer));
            }

            DiscordPrivateMessage(iPlayer.Id, player, message, Lang(LangKeys.From, iPlayer));

            Puts($"{player.Name} -> {iPlayer.Name}: {message}");
        }
        #endregion

        #region Discord Chat Commands
        private object HandlePrivateMessage(IPlayer player, string channelId, string cmd, string[] args)
        {
            if (args.Length < 2)
            {
                SendMessage(player.Id, Lang(LangKeys.InvalidPmSyntax, player, _pluginConfig.DiscordPm));
                return null;
            }

            IPlayer receivePlayer = covalence.Players.FindPlayer(args[0]);
            if (receivePlayer == null)
            {
                SendMessage(player.Id, Lang(LangKeys.UnableToFindPlayer, player));
                return null;
            }

            _replys[player.Id] = receivePlayer.Id;
            _replys[receivePlayer.Id] = player.Id;

            string message = string.Join(" ", args.Skip(1).ToArray());
            DiscordPrivateMessage(player.Id, receivePlayer, message, Lang(LangKeys.To, player));
            if (receivePlayer.IsConnected)
            {
#if RUST
                EffectAnnouncements?.Call("RunEffectForPlayer", receivePlayer.Object, EffectsKey);
#endif
                ServerPrivateMessage(receivePlayer, player, message, Lang(LangKeys.From, receivePlayer));
            }

            DiscordPrivateMessage(receivePlayer.Id, player, message, Lang(LangKeys.From, receivePlayer));
            Puts($"{player.Name} -> {receivePlayer.Name}: {message}");
            return null;
        }

        private object HandleReply(IPlayer player, string channelId, string cmd, string[] args)
        {
            if (args.Length < 1)
            {
                SendMessage(player.Id, Lang(LangKeys.InvalidReplySyntax, player, _pluginConfig.DiscordReply));
                return null;
            }

            string replyPlayer = _replys[player.Id];
            if (replyPlayer == null)
            {
                SendMessage(player.Id, Lang(LangKeys.NoPreviousPm, player));
                return null;
            }

            IPlayer receivePlayer = covalence.Players.FindPlayerById(replyPlayer);
            if (receivePlayer == null)
            {
                SendMessage(player.Id, Lang(LangKeys.NoPreviousPm, player));
                return null;
            }

            string message = string.Join(" ", args);
            DiscordPrivateMessage(player.Id, receivePlayer, message, Lang(LangKeys.To, player));
            if (receivePlayer.IsConnected)
            {
#if RUST
                EffectAnnouncements?.Call("RunEffectForPlayer", receivePlayer.Object, EffectsKey);
#endif
                ServerPrivateMessage(receivePlayer, player, message, Lang(LangKeys.From, receivePlayer));
            }

            DiscordPrivateMessage(receivePlayer.Id, player, message, Lang(LangKeys.From, receivePlayer));
            Puts($"{player.Name} -> {receivePlayer.Name}: {message}");
            return null;
        }
        #endregion

        #region API
        private void SendPmMessage(IPlayer to, IPlayer sender, string message)
        {
            if (to == null || sender == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            _replys[to.Id] = sender.Id;
            ServerPrivateMessage(to, sender, message);
        }
        #endregion

        #region Helpers
        private void Chat(IPlayer player, string format) => player.Reply(Lang(LangKeys.Chat, player, format));
        private void ServerPrivateMessage(IPlayer player, IPlayer target, string format, string prefix) => player.Reply(Lang(LangKeys.DiscordChatPrefix, player, prefix, target.Name, format));
        private void ServerPrivateMessage(IPlayer player, IPlayer target, string format) => player.Reply(Lang(LangKeys.DiscordChatNoPrefix, player, target.Name, format));

        private void SendMessage(string id, string message)
        {
            if (!_init)
            {
                return;
            }

            DiscordCore.Call("SendMessageToUser", id, $"{Title}: {message}");
        }

        private void DiscordPrivateMessage(string id, IPlayer player, string message, string prefix)
        {
            if (!_init)
            {
                return;
            }

            DiscordCore.Call("SendMessageToUser", id, $"PM {prefix} {player.Name}: {message}");
        }

        private string Lang(string key, IPlayer player = null, params object[] args) => string.Format(lang.GetMessage(key, this, player?.Id), args);
        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue("pm")]
            [JsonProperty(PropertyName = "Game Private Message Command")]
            public string GamePm{ get; set; }

            [DefaultValue("r")]
            [JsonProperty(PropertyName = "Game Reply Command")]
            public string GameReply { get; set; }

            [DefaultValue("pm")]
            [JsonProperty(PropertyName = "Discord Private Message Command")]
            public string DiscordPm { get; set; }

            [DefaultValue("r")]
            [JsonProperty(PropertyName = "Discord Reply Command")]
            public string DiscordReply { get; set; }
        }

        private static class LangKeys
        {
            public const string Chat = "Chat";
            public const string DiscordChatPrefix = "DiscordChatPrefixV1";
            public const string DiscordChatNoPrefix = "DiscordChatNoPrefixV1";
            public const string DiscordPmInfoText = "DiscordPmInfoText";
            public const string DiscordReplyInfoText = "DiscordReplyInfoText";
            public const string InvalidPmSyntax = "InvalidPmSyntaxV1";
            public const string InvalidReplySyntax = "InvalidReplySyntaxV1";
            public const string UnableToFindPlayer = "UnableToFindPlayer";
            public const string NoPreviousPm = "NoPreviousPm";
            public const string From = "From";
            public const string To = "To";
        }

        #endregion
    }
}
