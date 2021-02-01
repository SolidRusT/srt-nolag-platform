using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("Plugin Update Notifications", "Whispers88", "1.1.0")]
    [Description("Checks Umod plugins for updates")]
    public class PluginUpdateNotifications : CovalencePlugin
    {
        private const string vurl = "https://umod.org/plugins/{0}/versions.json";
        private IEnumerator coroutine;
        private List<string> mismatchedplugins = new List<string>();

        #region Configuration
        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Frequency to check for updates (hours)")]
            public float CheckFrequency = 12f;

            [JsonProperty("Ignore lower versions (ignores when the official version is a lower version number)")]
            public bool IgnoreLowVer = true;

            [JsonProperty("Check Unloaded Plugins")]
            public bool CheckUnloadedPlugins = false;

            [JsonProperty("Enable Discord Notifications")]
            public bool DiscordNotifications = false;

            [JsonProperty("Discord Webhook URL")]
            public string DiscordWebhookURL = "https://discordapp.com/api/webhooks";

            [JsonProperty("Avatar URL")]
            public string AvatarUrl = "https://i.imgur.com/poRMpyf.png";

            [JsonProperty("Discord Username")]
            public string DiscordUsername = "Plugin Update Notifications";

            [JsonProperty("Automatic Blacklist")]
            public bool AutomaticBacklist = true;

            [JsonProperty("Blacklist of plugins names not to check", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> BlacklistedPlugins = new List<string>() { "RustIO" };

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
                    Puts("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                Puts($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ErrorLocation"] = "Cannot locate {0} plugin on uMod",
                ["VersionMismatch"] = "{0} version mismatch current {1} uMod Version {2}",
                ["AlreadyRunning"] = "Already Checking for updates",
                //Commands
                ["CheckForUpdatesCommand"] = "CheckforUpdates"

            }, this);
        }

        #endregion Localization

        #region Commands

        private void CheckForUpdatesCommand(IPlayer iplayer, string command, string[] args)
        {
            if (!iplayer.IsServer) return;
            if (coroutine != null)
            {
                PrintError(GetLang("AlreadyRunning", null));
                return;
            }
            coroutine = CheckForUpdates();
            ServerMgr.Instance.StartCoroutine(coroutine);

        }
        #endregion

        #region Core

        void OnServerInitialized()
        {
            AddLocalizedCommand(nameof(CheckForUpdatesCommand));
            coroutine = CheckForUpdates();
            ServerMgr.Instance.StartCoroutine(coroutine);
            timer.Every(config.CheckFrequency * 3600f, () => {
                if (coroutine != null)
                {
                    PrintError(GetLang("AlreadyRunning", null));
                    return;
                }
                coroutine = CheckForUpdates();
                ServerMgr.Instance.StartCoroutine(coroutine);
            });
        }

        private void Unload()
        {
            if (coroutine != null)
                ServerMgr.Instance.StopCoroutine(coroutine);
        }

        private IEnumerator CheckForUpdates()
        {
            var pluginslist = plugins.GetAll().ToList();
            for (int i = 0; i < pluginslist.Count; i++)
            {
                var plugin = pluginslist[i];
                if (!config.CheckUnloadedPlugins && !plugin.IsLoaded || config.BlacklistedPlugins.Contains(plugin.Name) || plugin.IsCorePlugin) continue;

                string downloadHandler;
                UnityWebRequest www = UnityWebRequest.Get(string.Format(vurl, plugin.Name));
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                // Verify that the webrequest was succesful.
                if (www.isNetworkError || www.isHttpError)
                {
                    // The webrequest wasn't succesful, print error
                    if (!www.error.ToString().Contains("Too Many Requests"))
                    {
                        PrintError(GetLang("ErrorLocation", null, plugin.Name));
                        if (config.AutomaticBacklist)
                            config.BlacklistedPlugins.Add(plugin.Name);
                        www.Dispose();
                        continue;
                    }
                    www.Dispose();
                    PrintError("waiting 30 seconds for rate limit");
                    i--;
                    yield return new WaitForSeconds(30f);
                    continue;
                }

                downloadHandler = www.downloadHandler.text;

                var json = JsonConvert.DeserializeObject<Root>(downloadHandler);
                string umodversion = json.data[0].version;
                string pluginversion = plugin.Version.ToString();
                //Have to substring here to remove quotations
                umodversion.Substring(1, umodversion.Length - 1);
                if (umodversion.Length < pluginversion.Length)
                    pluginversion = pluginversion.Substring(0, umodversion.Length);

                if (umodversion.Length > pluginversion.Length)
                {
                    string[] umodnums = umodversion.Split('.');
                    for (int j = 0; j < umodnums.Length; j++)
                    {
                        if (umodnums[j].Length > 1 && umodnums[j].First().ToString() == "0")
                        {
                            umodnums[j] = umodnums[j].TrimStart('0');
                        }
                    }
                    umodversion = string.Join(".", umodnums);
                }
                var uModV = new Version();
                var pluginV = new Version();
                if (config.IgnoreLowVer)
                {
                    if (System.Version.TryParse(umodversion, out uModV) && System.Version.TryParse(pluginversion, out pluginV))
                    {
                        if (pluginV.CompareTo(uModV) >= 0)
                            continue;
                    }
                }

                if (umodversion != pluginversion && !mismatchedplugins.Contains(plugin.Name))
                {
                    mismatchedplugins.Add(plugin.Name);
                    PrintWarning(GetLang("VersionMismatch", null, plugin.Name, plugin.Version.ToString(), umodversion));
                    if (config.DiscordNotifications)
                    {
                        var msg = DiscordMessage(ConVar.Server.hostname, plugin.Name, plugin.Version.ToString(), umodversion, StripMarkUp(json.data[0].description), DateTime.UtcNow.ToString());
                        string jsonmsg = JsonConvert.SerializeObject(msg);
                        UnityWebRequest wwwpost = new UnityWebRequest(config.DiscordWebhookURL, "POST");
                        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonmsg.ToString());
                        wwwpost.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
                        wwwpost.SetRequestHeader("Content-Type", "application/json");
                        yield return wwwpost.SendWebRequest();

                        if (wwwpost.isNetworkError || wwwpost.isHttpError)
                        {
                            PrintError(wwwpost.error);
                        }
                        wwwpost.Dispose();
                    }
                }
                else if (mismatchedplugins.Contains(plugin.Name))
                {
                    mismatchedplugins.Remove(plugin.Name);
                }
                yield return new WaitForSeconds(0.5f);
            }
            if (config.AutomaticBacklist)
                SaveConfig();

            coroutine = null;
        }

        #endregion Core

        #region Helpers
        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        private Message DiscordMessage(string servername, string pluginname, string curversion, string umodversion, string umodchanges, string time)
        {
            var fields = new List<Message.Fields>()
            {
                new Message.Fields("Current Version", curversion, false),
                new Message.Fields("Umod Version", umodversion, false),
                new Message.Fields("Changes ", umodchanges, false),
                new Message.Fields("Umod Link", "https://umod.org/plugins/" + pluginname, false)
            };
            var footer = new Message.Footer($"Logged @{DateTime.UtcNow:dd/MM/yy HH:mm:ss}");
            var embeds = new List<Message.Embeds>()
            {
                new Message.Embeds("Server - " + servername, "Found an update for " + pluginname, fields, footer)
            };
            Message msg = new Message(config.DiscordUsername, config.AvatarUrl, embeds);
            return msg;
        }

        private static string StripMarkUp(string value)
        {
            value.Replace("/", string.Empty);
            value = Regex.Replace(value, "<.*?>", String.Empty);
            if (value.Length > 1024)
            {
                value = value.Substring(0, 1020) + "..";
            }
            return value;
        }

        #endregion Helpers

        #region Umod Class
        public class Datum
        {
            public string version { get; set; }
            public string description { get; set; }
            public int visible { get; set; }
            public string checksum { get; set; }
            public object tags { get; set; }
            public int downloads { get; set; }
            public string created_at { get; set; }
            public string description_md { get; set; }
            public string download_url { get; set; }
            public string revert_url { get; set; }
            public string toggle_url { get; set; }
            public string delete_url { get; set; }
            public string edit_url { get; set; }
            public bool is_latest { get; set; }
            public string text_class { get; set; }
            public bool revertable { get; set; }
            public string toggle_icon { get; set; }
            public string version_formatted { get; set; }
            public string downloads_shortened { get; set; }
            public string downloads_lang { get; set; }
            public DateTime created_at_atom { get; set; }
            public DateTime updated_at_atom { get; set; }

        }

        public class Root
        {
            public int current_page { get; set; }
            public List<Datum> data { get; set; }
            public string first_page_url { get; set; }
            public int from { get; set; }
            public int last_page { get; set; }
            public string last_page_url { get; set; }
            public object next_page_url { get; set; }
            public string path { get; set; }
            public int per_page { get; set; }
            public object prev_page_url { get; set; }
            public int to { get; set; }
            public int total { get; set; }

        }

        #endregion Umod Class

        #region Discord Class
        public class Message
        {
            public string username { get; set; }
            public string avatar_url { get; set; }
            public List<Embeds> embeds { get; set; }

            public class Fields
            {
                public string name { get; set; }
                public string value { get; set; }
                public bool inline { get; set; }
                public Fields(string name, string value, bool inline)
                {
                    this.name = name;
                    this.value = value;
                    this.inline = inline;
                }
            }

            public class Footer
            {
                public string text { get; set; }
                public Footer(string text)
                {
                    this.text = text;
                }
            }

            public class Embeds
            {
                public string title { get; set; }
                public string description { get; set; }
                public List<Fields> fields { get; set; }
                public Footer footer { get; set; }
                public Embeds(string title, string description, List<Fields> fields, Footer footer)
                {
                    this.title = title;
                    this.description = description;
                    this.fields = fields;
                    this.footer = footer;
                }
            }

            public Message(string username, string avatar_url, List<Embeds> embeds)
            {
                this.username = username;
                this.avatar_url = avatar_url;
                this.embeds = embeds;
            }
        }

        #endregion

        #region Helpers

        private void AddLocalizedCommand(string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages)
                {
                    if (!message.Key.Equals(command)) continue;

                    if (string.IsNullOrEmpty(message.Value)) continue;

                    AddCovalenceCommand(message.Value, command);
                }
            }
        }

        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);

        #endregion Helpers
    }
}