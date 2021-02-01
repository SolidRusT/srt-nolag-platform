//#define DEBUG

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;

// TODO: Add support for channel specification via API calls

namespace Oxide.Plugins
{
    [Info("Twitch", "Wulf", "1.0.0")]
    [Description("Provides an API for integration with Twitch")]
    class Twitch : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Default Twitch channel ID")]
            public string TwitchChannel { get; set; } = "";

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

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandTwitch"] = "twitch",
                ["IsFollowing"] = "{0} is a Twitch follower",
                ["NoUserFound"] = "No Twitch user found with the login '{0}'",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NotFollowing"] = "{0} is not a Twitch follower",
                ["SubCommandUser"] = "user",
                ["SubCommandChannel"] = "channel",
                ["TryAgainLater"] = "Currently unavailable, please try again later",
                ["TwitchChannelSet"] = "Twitch channel set to: {0} ({1})",
                ["UsageSubCommandUser"] = "Usage: {0} user <twitch user name>",
                ["UsageSubCommandChannel"] = "Usage: {0} channel <twitch channel name>"
            }, this);
        }

        #endregion Localization

        #region Stored Data

        private DynamicConfigFile data;
        private StoredData storedData;
        private bool changed;

        private class StoredData
        {
            public readonly Dictionary<string, string> Followers = new Dictionary<string, string>();
        }

        private void SaveData()
        {
            if (changed)
            {
                Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
            }
        }

        private void OnServerSave() => SaveData();

        private void Unload() => SaveData();

        #endregion Stored Data

        #region Initialization

        private static readonly Dictionary<string, string> Headers = new Dictionary<string, string>
        {
            ["Accept"] = "application/vnd.twitchtv.v5+json",
            ["Client-ID"] = clientId
        };

        private const string clientId = "5ekouzzlfd08acyaqdj3afpzi4djni";
        private const string permAdmin = "twitch.admin";

        public class TwitchUser
        {
            /*
            {
                "data": [{
                "id": "44322889",
                "login": "dallas",
                "display_name": "dallas",
                "type": "staff",
                "broadcaster_type": "",
                "description": "Just a gamer playing games and chatting. :)",
                "profile_image_url": "https://static-cdn.jtvnw.net/jtv_user_pictures/dallas-profile_image-1a2c906ee2c35f12-300x300.png",
                "offline_image_url": "https://static-cdn.jtvnw.net/jtv_user_pictures/dallas-channel_offline_image-1a2c906ee2c35f12-1920x1080.png",
                "view_count": 191836881,
                "email": "login@provider.com"
                }]
            }
            */

            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("login")]
            public string Login { get; set; }

            [JsonProperty("display_name")]
            public string DisplayName { get; set; }

            [JsonProperty("view_count")]
            public string ViewCount { get; set; }
        }

        private void Init()
        {
            AddLocalizedCommand(nameof(CommandTwitch));

            permission.RegisterPermission(permAdmin, this);

            int channel;
            if (!string.IsNullOrEmpty(GetChannel()) && !int.TryParse(GetChannel(), out channel))
            {
                Action<TwitchUser> callback = twitchUser =>
                {
                    if (twitchUser != null)
                    {
                        // TODO: Store TwitchUser for channel
                        config.TwitchChannel = twitchUser.Id.ToString();
                        SaveConfig();
                    }
                    else
                    {
                        LogWarning("Please set a valid Twitch channel"); // TODO: Localization
                    }
                };
                GetTwitchUser(GetChannel(), callback);
            }

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }

        #endregion Initialization

        #region Twitch API

        private string GetChannel() => config.TwitchChannel.Trim();

        private void GetTwitchUser(string loginOrUserId, Action<TwitchUser> callback)
        {
            Action<bool, string, int> loginCallback = (loginSuccess, loginResponse, loginCode) =>
            {
                if (loginSuccess)
                {
                    JObject loginJson = JObject.Parse(loginResponse);
                    JArray loginData = (JArray)loginJson["data"];
                    if (loginData.Count > 0)
                    {
                        callback?.Invoke(loginData[0].ToObject<TwitchUser>());
                    }
                    else
                    {
                        callback?.Invoke(null);
                    }
                }
                else
                {
                    Action<bool, string, int> userIdCallback = (userIdSuccess, userIdResponse, userIdCode) =>
                    {
                        if (userIdSuccess)
                        {
                            JObject userIdJson = JObject.Parse(userIdResponse);
                            JArray userIdData = (JArray)userIdJson["data"];
                            if (userIdData.Count > 0)
                            {
                                callback?.Invoke(userIdData[0].ToObject<TwitchUser>());
                            }
                            else
                            {
                                callback?.Invoke(null);
                            }
                        }
                        else
                        {
                            callback?.Invoke(null);
                        }
                    };
                    SendRequest($"https://api.twitch.tv/helix/users?id={loginOrUserId}", userIdCallback);
                }
            };
            SendRequest($"https://api.twitch.tv/helix/users?login={loginOrUserId}", loginCallback);
        }

        // TODO: Add IsFollowing API method for cached Followers

        private void IsFollowing(string loginOrUserId, Action<bool?> callback) => IsFollowing(loginOrUserId, null, callback);

        private void IsFollowing(string loginOrUserId, IPlayer player, Action<bool?> callback)
        {
            if (string.IsNullOrEmpty(GetChannel()))
            {
                LogWarning("Please set a valid Twitch channel"); // TODO: Localization
                callback?.Invoke(false);
                return;
            }

            Action<TwitchUser> userCallback = twitchUser =>
            {
                if (twitchUser != null)
                {
                    Action<bool, string, int> followCallback = (followSuccess, followResponse, followCode) =>
                    {
                        callback?.Invoke(followSuccess);
                        if (player != null)
                        {
                            if (storedData.Followers.ContainsKey(player.Id))
                            {
                                storedData.Followers[player.Id] = loginOrUserId;
                            }
                            else
                            {
                                storedData.Followers.Add(player.Id, loginOrUserId);
                            }
                            changed = true;

                            Interface.Oxide.CallHook("OnTwitchFollow", player); // TODO: Pass Twitch login name too?
                        }
                    };
                    SendRequest($"https://api.twitch.tv/kraken/users/{twitchUser.Id}/follows/channels/{GetChannel()}", followCallback);
                }
                else
                {
                    callback?.Invoke(null);
                }
            };
            if (player != null && storedData.Followers.ContainsKey(player.Id))
            {
                callback?.Invoke(true);
            }
            else
            {
                GetTwitchUser(loginOrUserId, userCallback);
            }
        }

        private void SendRequest(string url, Action<bool, string, int> callback = null)
        {
            webrequest.Enqueue(url, null, (code, response) =>
            {
                JObject json = JObject.Parse(response);
                string message = json["message"]?.ToString() ?? "No response message"; // TODO: Localization
#if DEBUG
                LogWarning(url);
                LogWarning(response);
                LogWarning(message);
                LogWarning($"Response code: {code}");
#endif

                if (code == 400 || code == 401 || code == 403 || code == 410 || code == 429 || code == 500 || code == 503)
                {
                    // 400  Bad Request            No client id specified
                    //                             Requests must be made over SSL
                    //                             Invalid login names, emails or IDs in request
                    //                             The parameter "user_id" was malformed: the value must match the regular expression /^[0-9]*$/
                    // 401  Unauthorized           Token invalid or missing required scope
                    // 403  Forbidden
                    // 410  Gone                   this API has been removed.
                    //                             v3 is a lie but v5 is still alive. See https://dev.twitch.tv/docs"
                    //                             War. War never changes... but APIs do. See https://dev.twitch.tv/docs
                    //                             It's time to kick ass and serve v3... and I'm all outta v3. See https://dev.twitch.tv/docs
                    // 429  Too Many Requests
                    // 500  Internal Server Error
                    // 503  Service Unavailable
                    callback?.Invoke(false, response, code);
                    LogWarning(message);
                    return;
                }

                if (code == 404)
                {
                    // 404  Not Found              Follow not found
                    callback?.Invoke(false, response, code);
                    return;
                }

                if (code == 200)
                {
                    // 200  Found                  No response message
                    //                             X is following X
                    callback?.Invoke(true, response, code);
                    return;
                }

                LogWarning($"Response from Twitch is unknown or unexpected, code: {code}"); // TODO: Localization
            }, this, RequestMethod.GET, Headers);
        }

        #endregion Twitch API

        #region Commands

        private void CommandTwitch(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 1 || !args[0].Equals(GetLang("SubCommandUser", player.Id), StringComparison.OrdinalIgnoreCase)
                && !args[0].Equals(GetLang("SubCommandChannel", player.Id), StringComparison.OrdinalIgnoreCase)) // TODO: Handle this better
            {
                Message(player, "UsageSubCommandUser", command);
                Message(player, "UsageSubCommandChannel", command);
                return;
            }

            // TODO: Add optional handling of args[2] for player ID (or potentially name with lookup for ID)

            if (args[0].Equals(GetLang("SubCommandUser", player.Id), StringComparison.OrdinalIgnoreCase))
            {
                Action<bool?> callback = following =>
                {
                    if (following != null)
                    {
                        if ((bool)following)
                        {
                            Message(player, "IsFollowing", args[1]);
                        }
                        else
                        {
                            Message(player, "NotFollowing", args[1]);
                        }
                    }
                    else
                    {
                        Message(player, "NoUserFound", args[1]);
                    }
                };
                if (storedData.Followers.ContainsKey(player.Id))
                {
                    callback?.Invoke(true);
                }
                else
                {
                    IsFollowing(args[1], callback);
                }
            }
            else if (args[0].Equals(GetLang("SubCommandChannel", player.Id), StringComparison.OrdinalIgnoreCase))
            {
                Action<TwitchUser> callback = twitchUser =>
                {
                    if (twitchUser != null)
                    {
                        config.TwitchChannel = twitchUser.Id;
                        Message(player, "TwitchChannelSet", twitchUser.Id, args[1]);
                        SaveConfig();
                    }
                    else
                    {
                        Message(player, "TryAgainLater", player.Id); // TODO: Use a different message
                    }
                };
                GetTwitchUser(args[1], callback);
            }
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
