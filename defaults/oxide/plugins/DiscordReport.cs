using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Discord Report", "misticos", "1.1.0")]
    [Description("Send reports from players ingame to a Discord channel")]
    class DiscordReport : CovalencePlugin
    {
        #region Variables

        [PluginReference("PlaceholderAPI")]
        private Plugin _placeholders = null;

        private Action<IPlayer, StringBuilder, bool> _placeholderProcessor = null;

        private static DiscordReport _ins;

        private const string SteamProfileXML = "https://steamcommunity.com/profiles/{0}?xml=1";
        private const string SteamProfile = "https://steamcommunity.com/profiles/{0}";

        private readonly Regex _steamProfileIconRegex =
            new Regex(@"(?<=<avatarIcon>[\w\W]+)https://.+\.jpg(?=[\w\W]+<\/avatarIcon>)", RegexOptions.Compiled);
        
        private Dictionary<string, uint> _cooldownData = new Dictionary<string, uint>();
        private Dictionary<string, HashSet<string>> _reportLog = new Dictionary<string, HashSet<string>>();

        private Time _time = GetLibrary<Time>();

        private const string PermissionIgnoreCooldown = "discordreport.ignorecooldown";
        private const string PermissionUse = "discordreport.use";
        private const string PermissionAdmin = "discordreport.admin";
        
        #endregion
        
        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Webhook URL")]
            public string Webhook = "YOUR WEBHOOK LINK HERE";
            
            [JsonProperty(PropertyName = "Embed Title")]
            public string EmbedTitle = "My Server Report";
            
            [JsonProperty(PropertyName = "Embed Description")]
            public string EmbedDescription = "Report sent by a player from your server";
            
            [JsonProperty(PropertyName = "Embed Color")]
            public int EmbedColor = 1484265;

            [JsonProperty(PropertyName = "Set Author Icon From Player Profile")]
            public bool AuthorIcon = true;

            [JsonProperty(PropertyName = "Use Reporter (True) Or Suspect (False) As Author")]
            public bool IsReporterIcon = true;

            [JsonProperty(PropertyName = "Allow Reporting Admins")]
            public bool ReportAdmins = false;

            [JsonProperty(PropertyName = "Report Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> ReportCommands = new List<string> {"report"};

            [JsonProperty(PropertyName = "Allow Only Online Suspects Reports")]
            public bool OnlyOnlineSuspects = true;

            [JsonProperty(PropertyName = "Threshold Before Sending Reports")]
            public int Threshold = 2;

            #if RUST
            [JsonProperty(PropertyName = "Show Recent Suspect Combatlog")]
            public bool ShowCombatlog = false;
            
            [JsonProperty(PropertyName = "Recent Combatlog Entries")]
            public int CombatlogEntries = 3;
            #endif

            [JsonProperty(PropertyName = "Cooldown In Seconds")]
            public uint Cooldown = 300;

            [JsonProperty(PropertyName = "Old User Cache Purge In Seconds")]
            public uint UserCachePurge = 86400;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion
        
        #region Work with Data

        private PluginData _data;

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }

        private class PluginData
        {
            public List<UserCache> Cache = new List<UserCache>();

            public class UserCache
            {
                public string ID = string.Empty;
                public string ImageURL = string.Empty;
                public string LastKnownAddress = string.Empty;
                public uint LastUpdate = 0;

                public static int FindIndex(string id)
                {
                    for (var i = 0; i < _ins._data.Cache.Count; i++)
                    {
                        var cache = _ins._data.Cache[i];
                        if (cache.ID == id)
                            return i;
                    }

                    return -1;
                }

                public static UserCache Find(string id)
                {
                    var index = FindIndex(id);
                    return index == -1 ? null : _ins._data.Cache[index];
                }

                public static UserCache TryFind(string id)
                {
                    var cache = Find(id);
                    if (cache != null)
                        return cache;
                    
                    cache = new UserCache {ID = id};
                    _ins._data.Cache.Add(cache);

                    return cache;
                }
            }
        }

        #endregion
        
        #region Discord Classes
        // ReSharper disable NotAccessedField.Local

        private class WebhookBody
        {
            [JsonProperty(PropertyName = "embeds")]
            public EmbedBody[] Embeds;
        }

        private class EmbedBody
        {
            [JsonProperty(PropertyName = "title")]
            public string Title;

            [JsonProperty(PropertyName = "type")]
            public string Type;
            
            [JsonProperty(PropertyName = "description")]
            public string Description;

            [JsonProperty(PropertyName = "color")]
            public int Color;

            [JsonProperty(PropertyName = "author")]
            public AuthorBody Author;

            [JsonProperty(PropertyName = "fields")]
            public FieldBody[] Fields;

            public class AuthorBody
            {
                [JsonProperty(PropertyName = "name")]
                public string Name;
                
                [JsonProperty(PropertyName = "url")]
                public string AuthorURL;
                
                [JsonProperty(PropertyName = "icon_url")]
                public string AuthorIconURL;
            }

            public class FieldBody
            {
                [JsonProperty(PropertyName = "name")]
                public string Name;
                
                [JsonProperty(PropertyName = "value")]
                public string Value;
                
                [JsonProperty(PropertyName = "inline")]
                public bool Inline;
            }
        }
        
        // ReSharper restore NotAccessedField.Local
        #endregion
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Webhook: Reporter Data Title", "Reporter" },
                { "Webhook: Suspect Data Title", "Suspect" },
                #if RUST
                { "Webhook: Combatlog Title", "Suspect's Combatlog #{n}" },
                { "Webhook: Combatlog Attacker Title", "Attacker" },
                { "Webhook: Combatlog Target Title", "Target" },
                { "Webhook: Combatlog Time Title", "Time" },
                { "Webhook: Combatlog Weapon Title", "Weapon" },
                { "Webhook: Combatlog Ammo Title", "Ammo" },
                { "Webhook: Combatlog Distance Title", "Distance" },
                { "Webhook: Combatlog Old HP Title", "Old HP" },
                { "Webhook: Combatlog New HP Title", "New HP" },
                { "Webhook: Combatlog Info Title", "Info" },
                #endif
                { "Webhook: Reporter Data", "{name} ({id}). IP: {ip}.\n" +
                                            "Ping: {ping}ms. Connected: {connected}" },
                { "Webhook: Suspect Data", "{name} ({id}). IP: {ip}.\n" +
                                           "Ping: {ping}ms. Connected: {connected}" },
                { "Webhook: Report Subject", "Report Subject" },
                { "Webhook: Report Message", "Report Message" },
                { "Webhook: Report Message If Empty", "No message specified" },
                { "Command: Syntax", "Syntax:\n" +
                                     "report (ID / Name) \"(Subject)\" \"<Message>\"\n" +
                                     "WARNING! Use quotes for names, subject and message." },
                { "Command: User Not Found", "We were unable to find this user or multiple were found." },
                { "Command: Report Sent", "Thank you for your report, it was sent to our administration." },
                { "Command: Exceeded Cooldown", "You have exceeded your cooldown on reports." },
                { "Command: Cannot Report Admins", "You cannot report admins." },
                { "Command: Cannot Use", "You cannot use this command since you do not have enough permissions." }
            }, this);
        }

        private void Init()
        {
            _ins = this;
            
            permission.RegisterPermission(PermissionIgnoreCooldown, this);
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);

            LoadData();

            foreach (var command in _config.ReportCommands)
                AddCovalenceCommand(command, nameof(CommandReport));
        }

        private void OnServerSave()
        {
            var currentTime = _time.GetUnixTimestamp();
            for (var i = _data.Cache.Count - 1; i >= 0; i--)
            {
                var user = _data.Cache[i];
                if (user.LastUpdate + _config.UserCachePurge < currentTime)
                    _data.Cache.RemoveAt(i);
            }
            
            SaveData();
        }

        private void Loaded()
        {
            foreach (var player in players.Connected)
                OnUserConnected(player);
            
            SaveData();
        }

        private void Unload()
        {
            _ins = null;
        }
        
        private void OnUserConnected(IPlayer player)
        {
            if (_config.AuthorIcon)
                UpdateCachedImage(player);

            PluginData.UserCache.TryFind(player.Id).LastKnownAddress = player.Address;
            PluginData.UserCache.TryFind(player.Id).LastUpdate = _time.GetUnixTimestamp();
        }

        private void OnPlaceholderAPIReady()
        {
            _placeholderProcessor = _placeholders.Call<Action<IPlayer, StringBuilder, bool>>("GetProcessPlaceholders", 1);
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin?.Name != "PlaceholderAPI")
                return;

            _placeholderProcessor = null;
        }

        #if RUST

        private void OnPlayerReported(BasePlayer player, string targetName, string targetId, string subject,
            string message, string type)
        {
            if (player == null)
                return;

            var suspect = players.FindPlayer(targetId);
            if (suspect == null || player.IPlayer == null)
                return;

            if (!CanUse(player.IPlayer))
                return;

            if (!_config.ReportAdmins && suspect.HasPermission(PermissionAdmin))
                return;

            if (_config.OnlyOnlineSuspects && !suspect.IsConnected)
                return;

            if (ExceedsCooldown(player.IPlayer))
                return;

            SendReport(player.IPlayer, suspect, subject, message);
        }

        #endif

        #endregion
        
        #region Commands

        private void CommandReport(IPlayer player, string command, string[] args)
        {
            if (!CanUse(player))
            {
                player.Reply(GetMsg("Command: Cannot Use", player.Id));
                return;
            }
            
            if (args.Length < 2)
                goto syntax;

            var suspect = players.FindPlayer(args[0]);
            if (suspect == null || !suspect.IsConnected && _config.OnlyOnlineSuspects)
            {
                player.Reply(GetMsg("Command: User Not Found", player.Id));
                return;
            }

            if (!_config.ReportAdmins && suspect.HasPermission(PermissionAdmin))
            {
                player.Reply(GetMsg("Command: Cannot Report Admins", player.Id));
                return;
            }

            if (ExceedsCooldown(player))
            {
                player.Reply(GetMsg("Command: Exceeded Cooldown", player.Id));
                return;
            }

            SendReport(player, suspect, args[1], args.Length > 2 ? args[2] : string.Empty);
            player.Reply(GetMsg("Command: Report Sent", player.Id));
            return;
            
            syntax:
            player.Reply(GetMsg("Command: Syntax", player.Id));
        }
        
        #endregion
        
        #region Helpers

        private void UpdateCachedImage(IPlayer player)
        {
            webrequest.Enqueue(string.Format(SteamProfileXML, player.Id), string.Empty,
                (code, result) =>
                {
                    var cache = PluginData.UserCache.TryFind(player.Id);
                    cache.ImageURL = _steamProfileIconRegex.Match(result).Value;
                    cache.LastUpdate =  _time.GetUnixTimestamp();
                },
                this);
        }
        
        #region Webhook

        private void SendReport(IPlayer reporter, IPlayer suspect, string subject, string message)
        {
            // Threshold

            HashSet<string> reporters;
            if (_reportLog.TryGetValue(suspect.Id, out reporters))
                reporters.Add(reporter.Id);
            else
                _reportLog.Add(suspect.Id, reporters = new HashSet<string> {reporter.Id});

            if (reporters.Count < _config.Threshold)
                return;
            
            // Author Icon
            
            var authorIconURL = string.Empty;
            var author = _config.IsReporterIcon ? reporter : suspect;
            if (_config.AuthorIcon)
            {
                authorIconURL = PluginData.UserCache.Find(author.Id)?.ImageURL ?? string.Empty;
                UpdateCachedImage(author); // Won't get update now but will be for any other reports for this user
            }
            
            // Embed data

            const string type = "rich";
            var body = new WebhookBody
            {
                Embeds = new[]
                {
                    new EmbedBody
                    {
                        Title = _config.EmbedTitle,
                        Description = _config.EmbedDescription,
                        Type = type,
                        Color = _config.EmbedColor,
                        Author = new EmbedBody.AuthorBody
                        {
                            AuthorIconURL = authorIconURL,
                            AuthorURL = string.Format(SteamProfile, author.Id),
                            Name = author.Name
                        },
                        Fields = new[]
                        {
                            new EmbedBody.FieldBody
                            {
                                Name = GetMsg("Webhook: Report Subject"),
                                Value = subject,
                                Inline = false
                            },
                            new EmbedBody.FieldBody
                            {
                                Name = GetMsg("Webhook: Report Message"),
                                Value = string.IsNullOrEmpty(message)
                                    ? GetMsg("Webhook: Report Message If Empty")
                                    : message,
                                Inline = false
                            },
                            new EmbedBody.FieldBody
                            {
                                Name = GetMsg("Webhook: Reporter Data Title"),
                                Value =
                                    FormatUserDetails(new StringBuilder(GetMsg("Webhook: Reporter Data")), reporter),
                                Inline = false
                            },
                            new EmbedBody.FieldBody
                            {
                                Name = GetMsg("Webhook: Suspect Data Title"),
                                Value = FormatUserDetails(new StringBuilder(GetMsg("Webhook: Suspect Data")), suspect),
                                Inline = false
                            }
                        }
                    }
                }
            };

            // Rust-specific embed data
            
            #if RUST
            if (_config.ShowCombatlog && suspect.Object is BasePlayer)
            {
                var events = CombatLog.Get(((BasePlayer) suspect.Object).userID).ToArray();
                for (var i = 1; i <= _config.CombatlogEntries && i <= events.Length; i++)
                {
                    var combat = events[events.Length - i];
                    
                    Array.Resize(ref body.Embeds, 1 + i);
                    body.Embeds[i] = new EmbedBody
                    {
                        Title = GetMsg("Webhook: Combatlog Title").Replace("{n}", $"{i}"),
                        Type = type,
                        Color = _config.EmbedColor,
                        Fields = new[]
                        {
                            new EmbedBody.FieldBody
                            {
                                Name = GetMsg("Webhook: Combatlog Attacker Title"),
                                Value = combat.attacker,
                                Inline = true
                            },
                            new EmbedBody.FieldBody
                            {
                                Name = GetMsg("Webhook: Combatlog Target Title"),
                                Value = combat.target,
                                Inline = true
                            },
                            new EmbedBody.FieldBody
                            {
                                Name = GetMsg("Webhook: Combatlog Time Title"),
                                Value = (UnityEngine.Time.realtimeSinceStartup - combat.time).ToString("0.0s"),
                                Inline = true
                            },
                            new EmbedBody.FieldBody
                            {
                                Name = GetMsg("Webhook: Combatlog Weapon Title"),
                                Value = combat.weapon,
                                Inline = true
                            },
                            new EmbedBody.FieldBody
                            {
                                Name = GetMsg("Webhook: Combatlog Ammo Title"),
                                Value = combat.ammo,
                                Inline = true
                            },
                            new EmbedBody.FieldBody
                            {
                                Name = GetMsg("Webhook: Combatlog Distance Title"),
                                Value = combat.distance.ToString("0.0m"),
                                Inline = true
                            },
                            new EmbedBody.FieldBody
                            {
                                Name = GetMsg("Webhook: Combatlog Old HP Title"),
                                Value = combat.health_old.ToString("0.0"),
                                Inline = true
                            },
                            new EmbedBody.FieldBody
                            {
                                Name = GetMsg("Webhook: Combatlog New HP Title"),
                                Value = combat.health_new.ToString("0.0"),
                                Inline = true
                            },
                            new EmbedBody.FieldBody
                            {
                                Name = GetMsg("Webhook: Combatlog Info Title"),
                                Value = string.IsNullOrEmpty(combat.info) ? "none" : combat.info,
                                Inline = true
                            }
                        }
                    };
                }
            }
            #endif
            
            // Send a web request

            webrequest.Enqueue(_config.Webhook, JObject.FromObject(body).ToString(),
                (code, result) => SetCooldown(reporter), this, RequestMethod.POST,
                new Dictionary<string, string> {{"Content-Type", "application/json"}});
        }

        private string FormatUserDetails(StringBuilder builder, IPlayer player)
        {
            // Apply placeholders if possible
            _placeholderProcessor?.Invoke(player, builder, false);
            
            return builder
                .Replace("{name}", player.Name).Replace("{id}", player.Id).Replace("{ip}",
                    PluginData.UserCache.Find(player.Id)?.LastKnownAddress ?? "Unknown")
                .Replace("{ping}", player.IsConnected ? player.Ping.ToString() : "0")
                .Replace("{connected}", player.IsConnected.ToString()).ToString();
        }

        #endregion
        
        #region Cooldown

        private bool ExceedsCooldown(IPlayer player)
        {
            if (player.HasPermission(PermissionIgnoreCooldown))
                return false;
            
            var currentTime = _time.GetUnixTimestamp();
            if (_cooldownData.ContainsKey(player.Id))
                return _cooldownData[player.Id] - currentTime < _config.Cooldown;

            return false;
        }

        private void SetCooldown(IPlayer player) => _cooldownData[player.Id] = _time.GetUnixTimestamp();
        
        #endregion

        private bool CanUse(IPlayer player) => player.HasPermission(PermissionUse);

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}