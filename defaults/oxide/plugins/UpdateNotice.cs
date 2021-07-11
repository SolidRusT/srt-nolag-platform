using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Update Notice", "Psystec", "1.3.0", ResourceId = 2837)]
    [Description("Notifies you when new Rust updates are released.")]

    public class UpdateNotice : RustPlugin
    {
        #region Fields

        private const string AdminPermission = "updatenotice.admin";
        private const string ApiUrl = "https://rust-updater.chroma-gaming.xyz/api/notifier/";
        private Configuration _configuration;
        private int _devBlogId = 0, _port = 0, _serverBuildId = 0, _clientBuildId = 0, _stagingBuildId = 0, _oxideBuildId = 0;
        private Timer checkTimer;

        #endregion Fields

        #region Classes

        private class Commands
        {
            public string Command { get; set; }
            public string Desciption { get; set; }
        }

        private class UpdateInfo
        {
            public int client { get; set; }
            public int oxide { get; set; }
            public int server { get; set; }
            public int staging { get; set; }
        }

        private class DevBlog
        {
            public Appnews appnews { get; set; }
        }

        private class Appnews
        {
            public int appid { get; set; }
            public Newsitem[] newsitems { get; set; }
            public int count { get; set; }
        }

        private class Newsitem
        {
            public string gid { get; set; }
            public string title { get; set; }
            public string url { get; set; }
            public bool is_external_url { get; set; }
            public string author { get; set; }
            public string contents { get; set; }
            public string feedlabel { get; set; }
            public int date { get; set; }
            public string feedname { get; set; }
            public int feed_type { get; set; }
            public int appid { get; set; }
        }

        #region Discord Message
        private class DiscordMessageEmbeds
        {
            /// <summary>
            /// if used, it overrides the default username of the webhook
            /// </summary>
            public string username { get; set; }
            /// <summary>
            /// if used, it overrides the default avatar of the webhook
            /// </summary>
            public string avatar_url { get; set; }
            /// <summary>
            /// simple message, the message contains (up to 2000 characters)
            /// </summary>
            public string content { get; set; }
            /// <summary>
            /// array of embed objects. That means, you can use more than one in the same body
            /// </summary>
            public Embed[] embeds { get; set; }
        }
        private class Embed
        {
            /// <summary>
            /// embed author object
            /// </summary>
            public Author author { get; set; }
            /// <summary>
            /// title of embed
            /// </summary>
            public string title { get; set; }
            /// <summary>
            /// url of embed. If title was used, it becomes hyperlink
            /// </summary>
            public string url { get; set; }
            /// <summary>
            /// description text
            /// </summary>
            public string description { get; set; }
            /// <summary>
            /// color code of the embed. You have to use Decimal numeral system, not Hexadecimal. Use color picker and converter: https://htmlcolorcodes.com/color-picker/ and https://www.binaryhexconverter.com/hex-to-decimal-converter
            /// </summary>
            public int color { get; set; }
            /// <summary>
            /// rray of embed field objects
            /// </summary>
            public Field[] fields { get; set; }
            /// <summary>
            /// embed thumbnail object
            /// </summary>
            public Thumbnail thumbnail { get; set; }
            /// <summary>
            /// embed image object
            /// </summary>
            public Image image { get; set; }
            /// <summary>
            /// embed footer object
            /// </summary>
            public Footer footer { get; set; }
        }
        private class Author
        {
            /// <summary>
            /// name of author
            /// </summary>
            public string name { get; set; }
            /// <summary>
            /// url of author. If name was used, it becomes a hyperlink
            /// </summary>
            public string url { get; set; }
            /// <summary>
            /// url of author icon
            /// </summary>
            public string icon_url { get; set; }
        }
        private class Thumbnail
        {
            /// <summary>
            /// url of thumbnail
            /// </summary>
            public string url { get; set; }
        }
        private class Image
        {
            /// <summary>
            /// url of image
            /// </summary>
            public string url { get; set; }
        }
        private class Footer
        {
            /// <summary>
            /// footer text, doesn't support Markdown
            /// </summary>
            public string text { get; set; }
            /// <summary>
            /// url of footer icon
            /// </summary>
            public string icon_url { get; set; }
        }
        private class Field
        {
            /// <summary>
            /// name of the field
            /// </summary>
            public string name { get; set; }
            /// <summary>
            /// alue of the field
            /// </summary>
            public string value { get; set; }
            /// <summary>
            /// if true, fields will be displayed in same line, but there can only be 3 max in same line or 2 max if you used thumbnail
            /// </summary>
            public bool inline { get; set; }
        }
        #endregion Discord Message

        #endregion Classes

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Only Notify Admin")]
            public bool OnlyNotifyAdmins { get; set; } = false;

            [JsonProperty("Enable Discord Notifications")]
            public bool EnableDiscordNotify { get; set; } = false;

            [JsonProperty("Discord Webhook URL")]
            public string DiscordWebhookURL { get; set; } = "https://support.discordapp.com/hc/en-us/articles/228383668";

            [JsonProperty("Enable Gui Notifications")]
            public bool EnableGuiNotifications { get; set; } = true;

            [JsonProperty("Enable Chat Notifications")]
            public bool EnableChatNotifications { get; set; } = true;

            [JsonProperty("GUI Notifications Tint Color")]
            public string GUINotificationsTintColor { get; set; } = "Purple";

            [JsonProperty("GUI Notifications Text Color")]
            public string GUINotificationsTextColor { get; set; } = "Yellow";

            [JsonProperty("Enable Server Version Notifications")]
            public bool EnableServer { get; set; } = true;

            [JsonProperty("Enable DevBlog Notifications")]
            public bool EnableDevBlog { get; set; } = true;

            [JsonProperty("Enable Client Version Notifications")]
            public bool EnableClient { get; set; } = true;

            [JsonProperty("Enable Staging Version Notifications")]
            public bool EnableStaging { get; set; } = false;

            [JsonProperty("Enable Oxide Version Notifications")]
            public bool EnableOxide { get; set; } = false;

            [JsonProperty("Checking Interval (in Seconds)")]
            public int CheckingInterval { get; set; } = 300;
        }

        protected override void LoadDefaultConfig()
        {
            _configuration = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _configuration = Config.ReadObject<Configuration>();

            if (_configuration.CheckingInterval < 180)
            {
                PrintWarning("Checking interval must be 180 seconds or greater! Setting this lower may get your server banned. Auto adjusted to 300.");
                _configuration.CheckingInterval = 300;
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_configuration);

        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ServerUpdated"] = "Server Update Released!",
                ["DevBlogUpdated"] = "DevBlog Update Released!",
                ["ClientUpdated"] = "Client Update Released!",
                ["StagingUpdated"] = "Staging Update Released!",
                ["OxideUpdated"] = "Oxide Update Released!",
                ["FailedToCheckUpdates"] = "Failed to check for RUST updates, if this keeps happening please contact the developer.",
                ["PluginNotFoundGuiAnnouncements"] = "GUIAnnouncements plugin was not found. Disabled by defaut.",
                ["NoPermission"] = "You do not have permission to use this command."
            }, this);
        }

        #endregion Localization

        #region Hooks

        private void Init()
        {
            _port = ConVar.Server.port;
            LoadConfig();
            permission.RegisterPermission(AdminPermission, this);
        }

        private void Loaded()
        {
            if (GUIAnnouncements == null)
            {
                PrintWarning(Lang("PluginNotFoundGuiAnnouncements"));
                _configuration.EnableGuiNotifications = false;
            }

            CompareBuilds();
            checkTimer = timer.Every(_configuration.CheckingInterval, CompareBuilds);
        }

        #endregion Hooks

        #region Testing

        [ConsoleCommand("updatenotice")]
        private void AdvertCommands(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!HasPermission(player, AdminPermission))
            {
                SendReply(arg, Lang("NoPermission"));
                return;
            }
            var args = arg?.Args ?? null;

            if (args == null)
            {
                SendReply(arg, ("COMMAND").PadRight(50) + "DESCRIPTION");
                SendReply(arg, ("updatenotice gui").PadRight(50) + "Tests GUI notification");
                SendReply(arg, ("updatenotice discord").PadRight(50) + "Tests Discord notification");
                SendReply(arg, ("updatenotice current").PadRight(50) + "Display's current update versions");
                SendReply(arg, ("updatenotice server").PadRight(50) + "Simulate Server update release");
                SendReply(arg, ("updatenotice devblog").PadRight(50) + "Simulate DevBlog update release");
                SendReply(arg, ("updatenotice client").PadRight(50) + "Simulate Client update release");
                SendReply(arg, ("updatenotice staging").PadRight(50) + "Simulate Staging update release");
                SendReply(arg, ("updatenotice oxide").PadRight(50) + "Simulate Oxide update release");
                SendReply(arg, ("updatenotice all").PadRight(50) + "Simulate all updates released");
                SendReply(arg, ("updatenotice check").PadRight(50) + "Forces a version check");
                return;
            }

            if (args[0] == "gui")
            {
                SendReply(arg, "Testing GUI Messages: Test message from Update Notice by Psystec");
                Puts("Testing GUI Messages: Test message from Update Notice by Psystec");
                SendtoGui("Test message from Update Notice by Psystec");

            }

            if (args[0] == "discord")
            {
                SendReply(arg, "Testing Discord Messages: Test message from Update Notice by Psystec");
                Puts("Testing Discord Messages: Test message from Update Notice by Psystec");
                SendToDiscord("Test message from Update Notice by Psystec");
            }

            if (args[0] == "current")
            {
                SendReply(arg, "Update Notice by Psystec\nServer: " + _serverBuildId.ToString() + "\n" +
                    "DevBlog: " + _devBlogId.ToString() + "\n" +
                    "Client: " + _clientBuildId.ToString() + "\n" +
                    "Staging: " + _stagingBuildId.ToString() + "\n" +
                    "Oxide: " + _oxideBuildId.ToString());

                Puts("Update Notice by Psystec\nServer: " + _serverBuildId.ToString() + "\n" +
                    "DevBlog: " + _devBlogId.ToString() + "\n" +
                    "Client: " + _clientBuildId.ToString() + "\n" +
                    "Staging: " + _stagingBuildId.ToString() + "\n" +
                    "Oxide: " + _oxideBuildId.ToString());
            }

            if (args[0] == "server")
            {
                SendReply(arg, "Testing Server Update");
                Puts("Testing Server Update");
                _serverBuildId = 1;
            }

            if (args[0] == "devblog")
            {
                SendReply(arg, "Testing Devblog Update");
                Puts("Testing Devblog Update");
                _devBlogId = 1;
            }

            if (args[0] == "client")
            {
                SendReply(arg, "Testing Client Update");
                Puts("Testing Client Update");
                _clientBuildId = 1;
            }

            if (args[0] == "staging")
            {
                SendReply(arg, "Testing Stagting Update");
                Puts("Testing Stagting Update");
                _stagingBuildId = 1;
            }

            if (args[0] == "oxide")
            {
                SendReply(arg, "Testing Oxide Update");
                Puts("Testing Oxide Update");
                _oxideBuildId = 1;
            }

            if (args[0] == "all")
            {
                SendReply(arg, "Testing All Updates");
                Puts("Testing All Updates");
                _serverBuildId = 1;
                _devBlogId = 1;
                _clientBuildId = 1;
                _stagingBuildId = 1;
                _oxideBuildId = 1;
            }

            if (args[0] == "check")
            {
                SendReply(arg, "Forcing Version Check");
                Puts("Forcing Version Check");
                CompareBuilds();
            }
        }

        #endregion Testing

        #region Build Comparison

        private void CompareBuilds()
        {
            webrequest.Enqueue(ApiUrl, null, (code, response) =>
            {
                if (code != 200)
                {
                    if (code == 0)
                    {
                        return;
                    }

                    PrintWarning("API: " + Lang("FailedToCheckUpdates") + "\nError Code: " + code + " | Message: " + response);
                    return;
                }

                var updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(response);

                if (_serverBuildId == 0 || _clientBuildId == 0 || _stagingBuildId == 0 || _oxideBuildId == 0)
                {
                    _serverBuildId = updateInfo.server;
                    _clientBuildId = updateInfo.client;
                    _stagingBuildId = updateInfo.staging;
                    _oxideBuildId = updateInfo.oxide;
                }
                else
                {
                    bool serverUpdated = _serverBuildId != updateInfo.server;
                    bool clientUpdated = _clientBuildId != updateInfo.client;
                    bool stagingUpdated = _stagingBuildId != updateInfo.staging;
                    bool oxideUpdated = _oxideBuildId != updateInfo.oxide;

                    if (!serverUpdated && !clientUpdated && !stagingUpdated && !oxideUpdated)
                    {
                        return;
                    }

                    if (serverUpdated)
                    {
                        _serverBuildId = updateInfo.server;
                        Puts(Lang("ServerUpdated"));

                        if (_configuration.EnableServer)
                        {
                            if (_configuration.EnableChatNotifications)
                                SendToChat(Lang("ServerUpdated"));
                            if (_configuration.EnableGuiNotifications)
                                SendtoGui(Lang("ServerUpdated"));
                            if (_configuration.EnableDiscordNotify)
                                SendToDiscord(Lang("ServerUpdated"));
                        }
                    }
                    if (clientUpdated)
                    {
                        _clientBuildId = updateInfo.client;
                        Puts(Lang("ClientUpdated"));

                        if (_configuration.EnableClient)
                        {
                            if (_configuration.EnableChatNotifications)
                                SendToChat(Lang("ClientUpdated"));
                            if (_configuration.EnableGuiNotifications)
                                SendtoGui(Lang("ClientUpdated"));
                            if (_configuration.EnableDiscordNotify)
                                SendToDiscord(Lang("ClientUpdated"));
                        }
                    }
                    if (stagingUpdated)
                    {
                        _stagingBuildId = updateInfo.staging;
                        Puts(Lang("StagingUpdated"));

                        if (_configuration.EnableStaging)
                        {
                            if (_configuration.EnableChatNotifications)
                                SendToChat(Lang("StagingUpdated"));
                            if (_configuration.EnableGuiNotifications)
                                SendtoGui(Lang("StagingUpdated"));
                            if (_configuration.EnableDiscordNotify)
                                SendToDiscord(Lang("StagingUpdated"));
                        }
                    }
                    if (oxideUpdated)
                    {
                        _oxideBuildId = updateInfo.oxide;
                        Puts(Lang("OxideUpdated"));

                        if (_configuration.EnableOxide)
                        {
                            if (_configuration.EnableChatNotifications)
                                SendToChat(Lang("OxideUpdated"));
                            if (_configuration.EnableGuiNotifications)
                                SendtoGui(Lang("OxideUpdated"));
                            if (_configuration.EnableDiscordNotify)
                                SendToDiscord(Lang("OxideUpdated"));
                        }
                    }
                }
            }, this);

            webrequest.Enqueue("http://api.steampowered.com/ISteamNews/GetNewsForApp/v0002/?appid=252490&count=1&maxlength=300&format=json", null, (code, response) =>
            {
                if (code != 200)
                {
                    if (code == 0) return;

                    PrintWarning("Steam: " + Lang("FailedToCheckUpdates") + "\nError Code: " + code + " | Message: " + response);
                    return;
                }

                var DevBlogInfo = JsonConvert.DeserializeObject<DevBlog>(response);

                if (_devBlogId == 0)
                {
                    _devBlogId = DevBlogInfo.appnews.newsitems[0].date;
                }
                else
                {
                    bool devBlogUpdated = _devBlogId != DevBlogInfo.appnews.newsitems[0].date;

                    if (devBlogUpdated)
                    {
                        _devBlogId = DevBlogInfo.appnews.newsitems[0].date;
                        Puts(Lang("DevBlogUpdated"));

                        if (_configuration.EnableDevBlog)
                        {
                            if (_configuration.EnableChatNotifications)
                                SendToChat(Lang("DevBlogUpdated"));
                            if (_configuration.EnableGuiNotifications)
                                SendtoGui(Lang("DevBlogUpdated"));
                            if (_configuration.EnableDiscordNotify)
                                SendToDiscord(Lang("DevBlogUpdated"));
                        }
                    }
                }

            }, this, Core.Libraries.RequestMethod.GET);
        }

        #endregion Build Comparison

        #region Helpers

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        //private bool HasPermission(BasePlayer player, string perm) => Permission.UserHasPermission(player.userID.ToString(), perm);
        private bool HasPermission(BasePlayer player, string perm)
        {
            if(!permission.UserHasPermission(player.userID.ToString(), perm))
                {
                PrintWarning("UserID: " + player.UserIDString + " | UserName: " + player.displayName + " | " + Lang("NoPermission"));
                return false;
            }
            return true;
        }

        private void SendtoGui(string message)
        {
            if (GUIAnnouncements == null)
            {
                PrintWarning(Lang("PluginNotFoundGuiAnnouncements"));
                return;
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (_configuration.OnlyNotifyAdmins)
                {
                    if (_configuration.OnlyNotifyAdmins && HasPermission(player, AdminPermission))
                    {
                        GUIAnnouncements?.Call("CreateAnnouncement", message, _configuration.GUINotificationsTintColor, _configuration.GUINotificationsTextColor, player);
                        Puts($"Announcement created for: {player.displayName}: {message}");
                    }
                }
                else
                {
                    GUIAnnouncements?.Call("CreateAnnouncement", message, _configuration.GUINotificationsTintColor, _configuration.GUINotificationsTextColor, player);
                    Puts($"Announcement created for: {player.displayName}: {message}");
                }
            }
        }

        private void SendToChat(string message)
        {
            rust.BroadcastChat(null, $"<size=20><color=#ff0000>Update Notice</color></size>\n{message}");
        }

        private void SendToDiscord(string message)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("Content-Type", "application/json");

            DiscordMessageEmbeds dc = new DiscordMessageEmbeds();
            dc.content = message;
            dc.embeds = new[]
            {
                new Embed
                {
                    title = "Update Notice",
                    description = $"Alert Time: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}\nVisit [Umod](https://umod.org/plugins/update-notice) for more information or [Discord](https://discord.gg/eZcFanf) for any assistance.",
                    url = "https://discord.gg/eZcFanf",
                    color = 16711686,
                    thumbnail = new Thumbnail { url = "https://assets.umod.org/images/icons/plugin/5b676d0909599.jpg" },
                    footer = new Footer { icon_url = "https://i.imgur.com/OjM8mr4.png", text = "Created by Psystec" }
                }
                
            };

            string payload = JsonConvert.SerializeObject(dc);
            webrequest.Enqueue(_configuration.DiscordWebhookURL, payload, (code, response) => 
            {
                if (code != 200 && code != 204)
                {
                    if (response == null)
                    {
                        PrintWarning($"Discord didn't respond. Error Code: {code}");
                    }
                    else
                    {
                        Puts($"Discord respond with: {response} Payload: {payload}");
                    }
                }
            }, this, Core.Libraries.RequestMethod.POST, headers);
        }        

        #endregion Helpers

        #region Internal API

        private int GetServerVersion() => _serverBuildId;  // Returns 0 if version could not be determined
        private int GetDevBlogVersion() => _devBlogId;   // Returns 0 if version could not be determined (date)
        private int GetClientVersion() => _clientBuildId;  // Returns 0 if version could not be determined
        private int GetStagingVersion() => _stagingBuildId;  // Returns 0 if version could not be determined
        private int GetOxideVersion() => _oxideBuildId;  // Returns 0 if version could not be determined

        #endregion Internal API

        #region External API
        [PluginReference]
        Plugin GUIAnnouncements;
        #endregion External API
    }
}