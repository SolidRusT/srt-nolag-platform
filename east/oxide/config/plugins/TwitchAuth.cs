// Requires: Twitch

using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Twitch Auth", "Wulf", "1.0.0")]
    [Description("Allows players to authorize themselves as a Twitch follower")]
    class TwitchAuth : CovalencePlugin
    {
        #region Configuration

        /*private Configuration config;

        public class Configuration
        {
            [JsonProperty("Kick players if not following")]
            public bool KickIfNotFollowing { get; set; } = true;

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
        }*/

        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandTwitchAuth"] = "twitchauth",
                ["FollowOnTwitch"] = "Please follow @ twitch.tv/{0}",
                ["Followed"] = "Thanks for following on Twitch!",
                ["IsExcluded"] = "{0} is excluded from checks",
                ["IsFollowing"] = "{0} is a Twitch follower",
                ["NotFollowing"] = "{0} is not a Twitch follower",
                ["TryAgainLater"] = "Currently unavailable, please try again later",
                ["UsageAuth"] = "Usage: {0} <twitch user name>"
            }, this);
        }

        #endregion Localization

        #region Initialization

        [PluginReference]
        private Plugin Twitch;

        //private const string permExclude = "twitchauth.exclude";
        //private const string permNoKick = "twitchauth.nokick";

        private string TwitchChannel;

        private void Init()
        {
            AddLocalizedCommand(nameof(CommandTwitchAuth));

            //permission.RegisterPermission(permExclude, this);
            //permission.RegisterPermission(permNoKick, this);
        }

        private void OnServerInitialized()
        {
            TwitchChannel = Twitch.Call<string>("GetChannel");
        }

        #endregion Initialization

        /*private void OnUserConnected(IPlayer player)
        {
            if (player.HasPermission(permExclude))
            {
                Log(GetLang("IsExcluded", null, player.Name));
                return;
            }

            Action<bool> callback = following =>
            {
                if (!following && config.KickIfNotFollowing && !player.HasPermission(permNoKick))
                {
                    // TODO: Give a grace period to auth before kick
                    player.Kick(GetLang("FollowOnTwitch", player.Id, TwitchChannel));
                }
            };
            Twitch.Call("IsFollowing", player.Name, player, callback);
        }*/

        #region Commands

        private void CommandTwitchAuth(IPlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                Message(player, "UsageAuth", command);
                return;
            }

            string loginOrUserId = args[0];
            Match urlMatch = Regex.Match(args[0], ".*/([^/]+)$");
            if (urlMatch.Success)
            {
                loginOrUserId = urlMatch.Groups[1].ToString();
                LogWarning(loginOrUserId);
            }

            Action<bool?> callbackFollows = following =>
            {
                if (following != null && (bool)following)
                {
                    Message(player, "Followed");
                }
                else
                {
                    Message(player, "FollowOnTwitch", TwitchChannel);
                }
            };
            Twitch.Call("IsFollowing", loginOrUserId, player, callbackFollows);
        }

        #endregion Commands

        #region Helpers

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
