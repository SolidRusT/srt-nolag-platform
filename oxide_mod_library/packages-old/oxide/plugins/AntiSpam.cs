using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System;

namespace Oxide.Plugins
{
    [Info("Anti Spam", "MON@H", "2.0.2")]
    [Description("Filters spam and impersonation in player names and chat messages.")]

    class AntiSpam : CovalencePlugin
    {
        #region Variables

        [PluginReference] private Plugin BetterChat;

        private const string PermissionImmunity = "antispam.immunity";
        private const string ColorAdmin = "#AAFF55";
        private const string ColorDeveloper = "#FFAA55";
        private const string ColorPlayer = "#55AAFF";

        private readonly List<Regex> _listRegexImpersonation = new List<Regex>();
        private readonly List<Regex> _listRegexSpam = new List<Regex>();
        private readonly StringBuilder _sb = new StringBuilder();

        private IPlayer _iPlayer;
        private string _newName;
        private string _text;
        private string _newText;

        #endregion Variables

        #region Initialization
        private void Init()
        {
            UnsubscribeHooks();
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermissionImmunity, this);

            CreateRegexCache();

            foreach (IPlayer player in players.Connected)
            {
                HandleName(player);
            }

            SubscribeHooks();
        }

        #endregion Initialization

        #region Configuration

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Enable logging")]
            public bool LoggingEnabled = false;

            [JsonProperty(PropertyName = "Filter player names")]
            public bool FilterPlayerNames = false;

            [JsonProperty(PropertyName = "Filter chat messages")]
            public bool FilterChatMessages = false;

            [JsonProperty(PropertyName = "Use regex")]
            public bool UseRegex = false;

            [JsonProperty(PropertyName = "Regex spam list", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> RegexSpamList = new List<string>()
            {
                "(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)",
                "(:\\d{3,5})",
                "(https|http|ftp|):\\/\\/",
                "((\\p{L}|[0-9]|-)+\\.)+(com|org|net|int|edu|gov|mil|ch|cn|co|de|eu|fr|in|nz|ru|tk|tr|uk|us)",
                "((\\p{L}|[0-9]|-)+\\.)+(ua|pro|io|dev|me|ml|tk|ml|ga|cf|gq|tf|money|pl|gg|net|info|cz|sk|nl)",
                "((\\p{L}|[0-9]|-)+\\.)+(store|shop)",
                "(\\#+(.+)?rust(.+)?)",
                "((.+)?rust(.+)?\\#+)"
            };

            [JsonProperty(PropertyName = "Regex impersonation list", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> RegexImpersonationList = new List<string>()
            {
                "([Ааa4][Ддd][Ммm][Ииi1][Ннn])",
                "([Ммm][Ооo0][Ддd][Ееe3][Ррr])"
            };

            [JsonProperty(PropertyName = "Use impersonation blacklist")]
            public bool UseBlacklistImpersonation = false;

            [JsonProperty(PropertyName = "Impersonation blacklist", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> BlacklistImpersonation = new List<string>()
            {
                "Admin",
                "Administrator",
                "Moder",
                "Moderator"
            };

            [JsonProperty(PropertyName = "Use spam blacklist")]
            public bool UseBlacklistSpam = false;

            [JsonProperty(PropertyName = "Spam blacklist", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> BlacklistSpam = new List<string>()
            {
                "#SPAMRUST",
                "#BESTRUST"
            };

            [JsonProperty(PropertyName = "Replacement for impersonation")]
            public string ReplacementImpersonation = "";

            [JsonProperty(PropertyName = "Replacement for spam")]
            public string ReplacementSpam = "";

            [JsonProperty(PropertyName = "Replacement for empty name")]
            public string ReplacementEmptyName = "Player-";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(_configData);

        #endregion Configuration

        #region Oxide Hooks

        private void OnUserConnected(IPlayer player) => HandleName(player);

        private void OnUserNameUpdated(string id, string oldName, string newName)
        {
            if (newName != oldName)
            {
                HandleName(players.FindPlayerById(id));
            }
        }

        private object OnBetterChat(Dictionary<string, object> data)
        {
            _iPlayer = data["Player"] as IPlayer;
            _text = data["Message"] as string;

            if (string.IsNullOrEmpty(_text) || permission.UserHasPermission(_iPlayer.Id, PermissionImmunity))
            {
                return null;
            }

            _newText = GetSpamFreeMessage(_iPlayer, _text);

            if (string.IsNullOrEmpty(_newText))
            {
                data["CancelOption"] = 2;
                return data;
            }

            if (_newText != _text)
            {
                data["Message"] = _newText;
                return data;
            }

            return null;
        }

#if RUST
        private object OnPlayerChat(BasePlayer basePlayer, string message, ConVar.Chat.ChatChannel channel)
        {
            _iPlayer = basePlayer.IPlayer;

            return HandleChatMessage(_iPlayer, message, (int)channel);
        }
#else
        private object OnUserChat(IPlayer player, string message) => HandleChatMessage(player, message);
#endif

        #endregion Oxide Hooks

        #region Core Methods

        private void CreateRegexCache()
        {
            if (!_configData.UseRegex)
            {
                return;          
            }

            foreach (string spamRegex in _configData.RegexSpamList)
            {
                _listRegexSpam.Add(new Regex(spamRegex, RegexOptions.IgnoreCase | RegexOptions.Compiled));
            }

            foreach (string adminRegex in _configData.RegexImpersonationList)
            {
                _listRegexImpersonation.Add(new Regex(adminRegex, RegexOptions.IgnoreCase | RegexOptions.Compiled));
            }
        }

        private void HandleName(IPlayer player)
        {
            if (player == null || !player.IsConnected || permission.UserHasPermission(player.Id, PermissionImmunity))
            {
                return;
            }

            _newName = GetClearName(player);

            if (_newName != player.Name)
            {
                Log($"{player.Id} renaming '{player.Name}' to '{_newName}'");

                player.Rename(_newName);
            }
        }

        private string GetClearName(IPlayer player)
        {
            if (permission.UserHasPermission(player.Id, PermissionImmunity))
            {
                return player.Name;
            }

            _newName = GetSpamFreeText(player.Name);

            _newName = GetImpersonationFreeText(_newName);

            if (string.IsNullOrEmpty(_newName))
            {
                _newName = $"{_configData.ReplacementEmptyName}{player.Id.Substring(11, 6)}";
            }

            return _newName.Trim();
        }

        private object HandleChatMessage(IPlayer player, string message, int channel = 0)
        {
            if (string.IsNullOrEmpty(message)
            || IsPluginLoaded(BetterChat)
            || permission.UserHasPermission(player.Id, PermissionImmunity))
            {
                return null;
            }

            _newText = GetSpamFreeMessage(player, message);

            if (string.IsNullOrEmpty(_newText))
            {
                return true;
            }

            if (_newText != message)
            {
                Broadcast(player, covalence.FormatText($"[{(_iPlayer.IsAdmin ? ColorAdmin : ColorPlayer)}]{_iPlayer.Name}[/#]: {_newText}"), channel);

                return true;
            }

            return null;
        }

        private string GetSpamFreeMessage(IPlayer player, string message)
        {
            if (player == null || string.IsNullOrEmpty(message))
            {
                return null;
            }

            _newText = GetSpamFreeText(message);

            if (_newText != message)
            {
                Log($"{player.Id} spam detected in message: {message}");

                return _newText;
            }

            return message;
        }

        private string GetSpamFreeText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (_configData.UseBlacklistSpam)
            {
                _sb.Length = 0;
                _sb.Append(text);

                foreach (string spamKeyword in _configData.BlacklistSpam)
                {
                    _sb.Replace(spamKeyword, _configData.ReplacementSpam);
                }

                text = _sb.ToString().Trim();
            }

            if (_configData.UseRegex)
            {
                foreach (Regex regex in _listRegexSpam)
                {
                    text = regex.Replace(text, _configData.ReplacementSpam).Trim();
                }
            }

            return text;
        }

        private string GetImpersonationFreeText(string text)
        {
            if (_configData.UseBlacklistImpersonation)
            {
                _sb.Length = 0;
                _sb.Append(text);

                foreach (string impersonation in _configData.BlacklistImpersonation)
                {
                    _sb.Replace(impersonation, _configData.ReplacementImpersonation);
                }

                text = _sb.ToString().Trim();
            }

            if (_configData.UseRegex)
            {
                foreach (Regex regexImpersonation in _listRegexImpersonation)
                {
                    text = regexImpersonation.Replace(text, _configData.ReplacementImpersonation).Trim();
                }
            }

            return text;
        }

        #endregion Core Methods

        #region Helpers

        private void UnsubscribeHooks()
        {
            Unsubscribe(nameof(OnBetterChat));
#if RUST
            Unsubscribe(nameof(OnPlayerChat));
#else
            Unsubscribe(nameof(OnUserChat));
#endif
            Unsubscribe(nameof(OnUserConnected));
            Unsubscribe(nameof(OnUserNameUpdated));
        }

        private void SubscribeHooks()
        {
            if (_configData.FilterPlayerNames)
            {
                Subscribe(nameof(OnUserConnected));
                Subscribe(nameof(OnUserNameUpdated));
            }

            if (_configData.FilterChatMessages)
            {
                Subscribe(nameof(OnBetterChat));
#if RUST
                Subscribe(nameof(OnPlayerChat));
#else
                Subscribe(nameof(OnUserChat));
#endif
            }
        }

        private bool IsPluginLoaded(Plugin plugin) => plugin != null && plugin.IsLoaded;

        private void Log(string text)
        {
            if (_configData.LoggingEnabled)
            {
                LogToFile("log", $"{DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss")} {text}", this);
            }
        }

        private void Broadcast(IPlayer sender, string text, int channel = 0)
        {
#if RUST
            foreach (IPlayer target in players.Connected)
            {
                target.Command("chat.add", channel, sender.Id, text);
            }
#else
            server.Broadcast(text);
#endif
        }

        #endregion Helpers
    }
}