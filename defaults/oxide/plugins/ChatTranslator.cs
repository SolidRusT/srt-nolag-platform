//#define DEBUG

// Requires: TranslationAPI

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Chat Translator", "Wulf", "2.2.0")]
    [Description("Translates chat messages to each player's language preference or server default")]
    public class ChatTranslator : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        private class Configuration
        {
            [JsonProperty("Force default server language")]
            public bool ForceServerDefault = false;

            [JsonProperty("Log translated chat messages")]
            public bool LogChatMessages = false;

            [JsonProperty("Show original and translation")]
            public bool ShowBothMessages = false;

            [JsonProperty("Translate message for sender")]
            public bool TranslateForSender = false;

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

        #region Chat Translation

        [PluginReference]
        private readonly Plugin TranslationAPI, BetterChat, BetterChatFilter, BetterChatMute, ChatFilter;

        private void Translate(string message, string targetId, string senderId, Action<string> callback)
        {
            // Get language code to translate to
            string langTo = "en";
            if (!message.Equals("Translation") && config.ForceServerDefault && CultureInfo.GetCultureInfo(lang.GetServerLanguage()) != null)
            {
                langTo = lang.GetServerLanguage();
            }
            else
            {
                langTo = lang.GetLanguage(targetId);
            }

            // Get language code to translate from
            string langFrom = lang.GetLanguage(senderId) ?? "auto";

#if DEBUG
            LogWarning($"To ({targetId}): {langTo}, From ({senderId}): {langFrom}");
#endif
            // Translate the message
            TranslationAPI.Call("Translate", message, langTo, langFrom, callback);
        }

        private void TranslateChat(IPlayer target, IPlayer sender, string message, int channel = 0)
        {
            if (sender.Equals(target) && !config.TranslateForSender)
            {
                ProcessMessage(sender, sender, message, message, channel);
                return;
            }

            Action<string> callback = translation =>
            {
                if (config.ShowBothMessages && !message.Equals(translation))
                {
                    Action<string> prefixCallback = prefixResponse =>
                    {
                        translation = $"{message}\n{prefixResponse}: {translation}";
                        ProcessMessage(target, sender, translation, message, channel);
                    };
                    Translate("Translation", sender.Id, sender.Id, prefixCallback);
                }
                else
                {
                    ProcessMessage(target, sender, translation, message, channel);
                }
            };
            Translate(message, target.Id, sender.Id, callback);
        }

        private void ProcessMessage(IPlayer target, IPlayer sender, string translation, string original, int channel = 0)
        {
            if (target == null || !target.IsConnected)
            {
                return;
            }

            if (Interface.Oxide.CallHook("OnTranslatedChat", sender, translation, original, channel) != null)
            {
                return;
            }

            // Rust colors: Admin/moderator = #aaff55, Developer = #ffaa55, Player = #55aaff
            string prefixColor = sender.IsAdmin ? "#aaff55" : "#55aaff";

            if (config.LogChatMessages)
            {
                LogToFile("log", translation, this);
#if RUST
                Log($"[{(ConVar.Chat.ChatChannel)channel}] {sender.Name}: {translation}");

                // Create RCON broadcast for Rust
                Facepunch.RCon.Broadcast(Facepunch.RCon.LogType.Chat, new ConVar.Chat.ChatEntry
                {
                    Channel = (ConVar.Chat.ChatChannel)channel,
                    Message = translation,
                    UserId = sender.Id,
                    Username = sender.Name,
                    Color = prefixColor,
                    Time = Facepunch.Math.Epoch.Current
                });
#else
                Log($"{sender.Name}: {translation}");
#endif
            }

            string formatted;
            if (BetterChat != null && BetterChat.IsLoaded)
            {
                formatted = covalence.FormatText(BetterChat.Call<string>("API_GetFormattedMessage", sender, translation));
            }
            else
            {
                formatted = $"{covalence.FormatText($"[{prefixColor}]{sender.Name}[/#]")}: {translation}";
            }

#if RUST
            switch (channel)
            {
                case 1: // Team chat
                    BasePlayer basePlayer = sender.Object as BasePlayer;
                    RelationshipManager.PlayerTeam team = basePlayer.Team;
                    if (team != null && team.members.Count != 0)
                    {
                        // Rust companion app support
                        uint current = (uint)Facepunch.Math.Epoch.Current;
                        ulong targetId = ulong.Parse(target.Id);
                        CompanionServer.Server.TeamChat.Record(team.teamID, targetId, target.Name, translation, prefixColor, current);
                        ProtoBuf.AppBroadcast appBroadcast = Facepunch.Pool.Get<ProtoBuf.AppBroadcast>();
                        appBroadcast.teamMessage = Facepunch.Pool.Get<ProtoBuf.AppTeamMessage>();
                        appBroadcast.teamMessage.message = Facepunch.Pool.Get<ProtoBuf.AppChatMessage>();
                        appBroadcast.ShouldPool = false;
                        ProtoBuf.AppChatMessage appChatMessage = appBroadcast.teamMessage.message;
                        appChatMessage.steamId = targetId;
                        appChatMessage.name = target.Name;
                        appChatMessage.message = translation;
                        appChatMessage.color = prefixColor;
                        appChatMessage.time = current;
                        CompanionServer.Server.Broadcast(new CompanionServer.PlayerTarget(targetId), appBroadcast);
                        appBroadcast.ShouldPool = true;
                        appBroadcast.Dispose();
                    }
                    break;
            }
#endif

#if RUST
            target.Command("chat.add", channel, sender.Id, formatted);
#else
            target.Message(formatted);
#endif
        }

        #endregion Chat Translation

        #region Chat Handling

        private object HandleChat(IPlayer sender, string message, int channel = 0, List<string> blockedReceivers = null)
        {
            if (sender == null || string.IsNullOrEmpty(message))
            {
                return null;
            }

            if (ChatFilter != null && ChatFilter.IsLoaded)
            {
                if (ChatFilter.Call<bool>("ContainsAdvertising"))
                {
                    return true; // TODO: Support filtering, not just blocking
                }
            }

            if (BetterChatMute != null && BetterChatMute.IsLoaded)
            {
                if (BetterChatMute.Call<bool>("API_IsMuted", sender))
                {
                    return true;
                }
            }

            if (BetterChatFilter != null && BetterChatFilter.IsLoaded)
            {
                Dictionary<string, object> filteredData = BetterChatFilter.Call<Dictionary<string, object>>("Filter", sender, message);
                if (filteredData != null && filteredData.Count > 0)
                {
                    message = filteredData["Message"].ToString();
                }
            }

            switch (channel)
            {
#if RUST
                case 1: // Team chat
                    BasePlayer basePlayer = sender.Object as BasePlayer;
                    RelationshipManager.PlayerTeam team = basePlayer.Team;
                    if (team != null && team.members.Count != 0)
                    {
                        foreach (ulong member in team.members)
                        {
                            BasePlayer targetBasePlayer = RelationshipManager.FindByID(member);
                            if (targetBasePlayer != null && targetBasePlayer.IsConnected)
                            {
                                if (!blockedReceivers.Contains(targetBasePlayer.IPlayer.Id))
                                {
                                    TranslateChat(targetBasePlayer.IPlayer, sender, message, channel);
                                }
                            }
                        }
                    }
                    break;
#endif
                default:
                    foreach (IPlayer target in players.Connected)
                    {
                        if (!blockedReceivers.Contains(target.Id))
                        {
                            TranslateChat(target, sender, message, channel);
                        }
                    }
                    break;
            }

            return true;
        }

        private object OnBetterChat(Dictionary<string, object> data)
        {
            HandleChat(data["Player"] as IPlayer, data["Message"] as string, (int)data["ChatChannel"], data["BlockedReceivers"] as List<string>);
            data["CancelOption"] = 2;
            return data;
        }

#if RUST
        private object OnPlayerChat(BasePlayer basePlayer, string message, ConVar.Chat.ChatChannel channel)
        {
            if (BetterChat == null || !BetterChat.IsLoaded)
            {
                return HandleChat(basePlayer.IPlayer, message, (int)channel);
            }

            return null;
        }
#else
        private object OnUserChat(IPlayer player, string message)
        {
            if (BetterChat == null || !BetterChat.IsLoaded)
            {
                return HandleChat(player, message);
            }

            return null;
        }
#endif

        #endregion Chat Handling
    }
}
