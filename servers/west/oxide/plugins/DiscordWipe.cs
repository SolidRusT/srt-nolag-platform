using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;

#if RUST
using UnityEngine.Networking;
using System.Collections;
#endif

namespace Oxide.Plugins
{
    [Info("Discord Wipe", "MJSU", "2.2.1")]
    [Description("Sends a notification to a discord channel when the server wipes or protocol changes")]
    internal class DiscordWipe : CovalencePlugin
    {
        #region Class Fields

        [PluginReference] private Plugin RustMapApi, PlaceholderAPI;
        
        private PluginConfig _pluginConfig; //Plugin Config
        private StoredData _storedData; //Plugin Data

        private const string DefaultUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        private const string AdminPermission = "discordwipe.admin";
        private const string AttachmentBase = "attachment://";
        private const string MapAttachment = AttachmentBase + MapFilename;
        private const string MapFilename = "map.jpg";
        private const int MaxImageSize = 8 * 1024 * 1024;
        
        private string _protocol;
        private string _previousProtocol;

        private Action<IPlayer, StringBuilder, bool> _replacer;
        
        private enum DebugEnum {Message, None, Error, Warning, Info}
        public enum SendMode {Always, Random}
        private enum EncodingMode {Jpg = 1, Png = 2}
        #endregion

        #region Setup & Loading
        private void Init()
        {
            AddCovalenceCommand(_pluginConfig.Command, nameof(SendWipeCommand));

            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            _previousProtocol = _storedData.Protocol;
            
            permission.RegisterPermission(AdminPermission, this);
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.NoPermission] = "You do not have permission to use this command",
                [LangKeys.SentWipe] = "You have sent a test wipe message",
                [LangKeys.SentProtocol] = "You have sent a test protocol message",
                [LangKeys.Help] = "Sends test message for plugin\n" +
                                  "{0}{1} wipe - sends a wipe test message\n" +
                                  "{0}{1} protocol - sends a protocol test message\n" +
                                  "{0}{1} - displays this help text again" ,
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
            config.WipeEmbeds = config.WipeEmbeds ?? new List<DiscordMessageConfig> { 
                new DiscordMessageConfig
                {
                    Content = "@everyone",
                    SendMode = SendMode.Always,
                    Embed = new EmbedConfig
                    {
                        Title = "{server.name}",
                        Description = "The server has wiped!",
                        Url = string.Empty,
                        Color = "#de8732",
                        Image = MapAttachment,
                        Thumbnail = string.Empty,
                        Fields = new List<FieldConfig>
                        {
#if RUST
                            new FieldConfig
                            {
                                Title = "Seed",
                                Value = "[{world.seed}](https://rustmaps.com/map/{world.size}_{world.seed})",
                                Inline = true,
                                Enabled = true
                            },
                            new FieldConfig
                            {
                                Title = "Size",
                                Value = "{world.size}M ({world.size!km^2}km^2)",
                                Inline = true,
                                Enabled = true
                            },
                            new FieldConfig
                            {
                                Title = "Protocol",
                                Value = "{server.protocol.network}",
                                Inline = true,
                                Enabled = true
                            },
#endif
                            new FieldConfig
                            {
                                Title = "Click & Connect",
                                Value = "steam://connect/{server.address}:{server.port}",
                                Inline = false,
                                Enabled = true
                            }
                        },
                        Footer = new FooterConfig
                        {
                            IconUrl = string.Empty,
                            Text = string.Empty,
                            Enabled = true
                        },
                        Enabled = true
                    }
                }};
            
            config.ProtocolEmbeds = config.ProtocolEmbeds ?? new List<DiscordMessageConfig> { 
                new DiscordMessageConfig
                {
                    Content = "@everyone",
                    SendMode = SendMode.Always,
                    Embed = new EmbedConfig
                    {
                        Title = "{server.name}",
                        Description = "The server protocol has changed!",
                        Url = string.Empty,
                        Color = "#de8732",
                        Image = string.Empty,
                        Thumbnail = string.Empty,
                        Fields = new List<FieldConfig>
                        {
                            new FieldConfig
                            {
                                Title = "Protocol",
                                Value = "{server.protocol.network}",
                                Inline = true,
                                Enabled = true
                            },
                            new FieldConfig
                            {
                                Title = "Previous Protocol",
                                Value = "{server.protocol.previous}",
                                Inline = true,
                                Enabled = true
                            },
                            new FieldConfig
                            {
                                Title = "Mandatory Client Update",
                                Value = "This update requires a mandatory client update in order to be able to play on the server",
                                Inline = false,
                                Enabled = true
                            },
                            new FieldConfig
                            {
                                Title = "Click & Connect",
                                Value = "steam://connect/{server.address}:{server.port}",
                                Inline = false,
                                Enabled = true
                            }
                        },
                        Footer = new FooterConfig
                        {
                            IconUrl = string.Empty,
                            Text = string.Empty,
                            Enabled = true
                        },
                        Enabled = true
                    }
                }};

#if RUST
            config.ImageSettings = new RustMapImageSettings
            {
                Name = config.ImageSettings?.Name ?? "Icons",
                Scale = config.ImageSettings?.Scale ?? 0.5f,
                FileType = config.ImageSettings?.FileType ?? EncodingMode.Jpg
            };        
#endif
            
            return config;
        }
        
        private void OnServerInitialized()
        {
            bool changed = false;
            if (_pluginConfig.WipeEmbed != null)
            {
                _pluginConfig.WipeEmbeds.Clear();
                _pluginConfig.WipeEmbed.SendMode = SendMode.Always;
                _pluginConfig.WipeEmbeds.Add(_pluginConfig.WipeEmbed);
                _pluginConfig.WipeEmbed = null;
                changed = true;
            }
            
            if (_pluginConfig.ProtocolEmbed != null)
            {
                _pluginConfig.ProtocolEmbeds.Clear();
                _pluginConfig.ProtocolEmbed.SendMode = SendMode.Always;
                _pluginConfig.ProtocolEmbeds.Add(_pluginConfig.ProtocolEmbed);
                _pluginConfig.ProtocolEmbed = null;
                changed = true;
            }

            if (changed)
            {
                Config.WriteObject(_pluginConfig);
            }
            
            _protocol = GetProtocol();
            if (PlaceholderAPI == null || !PlaceholderAPI.IsLoaded)
            {
                PrintError("Missing plugin dependency PlaceholderAPI: https://umod.org/plugins/placeholder-api");
                return;
            }
            
            if(PlaceholderAPI.Version < new VersionNumber(2, 2, 0))
            {
                PrintError("Placeholder API plugin must be version 2.2.0 or higher");
                return;
            }

            if (IsRustMapApiLoaded())
            {
                if(RustMapApi.Version < new VersionNumber(1,3,2))
                {
                    PrintError("RustMapApi plugin must be version 1.3.2 or higher");
                    return;
                }
                
                if (!IsRustMapApiReady())
                {
                    return;
                }
            }

            //Delayed so PlaceholderAPI can be ready before we call
            timer.In(1f, HandleStartup);
        }

        private void OnRustMapApiReady()
        {
            HandleStartup();
        }
        
        private void HandleStartup()
        {
            if (string.IsNullOrEmpty(_storedData.Protocol))
            {
                Debug(DebugEnum.Info, $"HandleStartup - Protocol is not set setting protocol to: {_protocol}");
                _storedData.Protocol = _protocol;
                SaveData();
            }
            else if (_storedData.Protocol != _protocol)
            {
                Debug(DebugEnum.Info, $"HandleStartup - Protocol has changed {_storedData.Protocol} -> {_protocol}");
                if (_pluginConfig.SendProtocolAutomatically)
                {
                    SendProtocol();
                }
                _storedData.Protocol = _protocol;
                SaveData();
                Puts("Protocol notification sent");
            }
            
            if (_storedData.IsWipe)
            {
                if (_pluginConfig.SendWipeAutomatically)
                {
                    Debug(DebugEnum.Info, "HandleStartup - IsWipe is set. Sending wipe message.");
                    SendWipe();
                    Puts("Wipe notification sent");
                }
                _storedData.IsWipe = false;
                SaveData();
            }
        }

        private void OnNewSave(string filename)
        {
            _storedData.IsWipe = true;
            Debug(DebugEnum.Info, "OnNewSave - Wipe Detected");
            SaveData();
        }

        private void Unload()
        {
            SaveData();
        }
        
        private string GetProtocol()
        {
#if RUST
            return Rust.Protocol.network.ToString();
#else 
            return covalence.Server.Protocol;
#endif
        }
        #endregion

        #region Command
        private bool SendWipeCommand(IPlayer player, string cmd, string[] args)
        {
            if (!HasPermission(player, AdminPermission))
            {
                player.Message(Lang(LangKeys.NoPermission, player));
                return true;
            }

            string commandPrefix = player.IsServer ? "" : "/";
            if (args.Length == 0)
            {
                player.Message(Lang(LangKeys.Help, player, commandPrefix, _pluginConfig.Command));
                return true;
            }
            
            switch (args[0].ToLower())
            {
                case "wipe":
                    SendWipe();
                    player.Message(Lang(LangKeys.SentWipe, player));
                    break;
                
                case "protocol":
                    SendProtocol();
                    player.Message(Lang(LangKeys.SentProtocol, player));
                    break;
                
                default:
                    player.Message(Lang(LangKeys.Help, player,commandPrefix, _pluginConfig.Command));
                    break;
            }
            
            return true;
        }
        #endregion

        #region Message Handling
        private void SendWipe()
        {
            Debug(DebugEnum.Info, "SendWipe - Sending wipe message");
            if (string.IsNullOrEmpty(_pluginConfig.WipeWebhook) || _pluginConfig.WipeWebhook == DefaultUrl)
            {
                Debug(DebugEnum.Info, "SendWipe - Wipe message not sent due to Wipe Webhook being blank or matching the default url");
                return;
            }

            List<DiscordMessageConfig> messages = _pluginConfig.WipeEmbeds
                .Where(w => w.SendMode == SendMode.Always)
                .ToList();
            
            DiscordMessageConfig random = _pluginConfig.WipeEmbeds
                .Where(w => w.SendMode == SendMode.Random)
                .OrderBy(w => Guid.NewGuid())
                .FirstOrDefault();

            if (random != null)
            {
                messages.Add(random);
            }

            SendMessage(_pluginConfig.WipeWebhook, messages);
            Debug(DebugEnum.Message, "");
        }

        private void SendProtocol()
        {
            Debug(DebugEnum.Info, "SendProtocol - Sending protocol message");
            if (string.IsNullOrEmpty(_pluginConfig.ProtocolWebhook) || _pluginConfig.ProtocolWebhook == DefaultUrl)
            {
                Debug(DebugEnum.Info, "SendProtocol - Protocol message not sent due to Wipe Webhook being blank or matching the default url");
                return;
            }
            
            List<DiscordMessageConfig> messages = _pluginConfig.ProtocolEmbeds
                .Where(w => w.SendMode == SendMode.Always)
                .ToList();
            
            DiscordMessageConfig random = _pluginConfig.ProtocolEmbeds
                .Where(w => w.SendMode == SendMode.Random)
                .OrderBy(w => Guid.NewGuid())
                .FirstOrDefault();

            if (random != null)
            {
                messages.Add(random);
            }
            
            SendMessage(_pluginConfig.ProtocolWebhook, messages);
        }

        private void SendMessage(string url, List<DiscordMessageConfig> messageConfigs)
        {
            for (int index = 0; index < messageConfigs.Count; index++)
            {
                DiscordMessageConfig messageConfig = messageConfigs[index];
                DiscordMessage message = ParseMessage(messageConfig);

                timer.In(index + 1, () =>
                {
#if RUST
                    List<Attachment> attachments = new List<Attachment>();

                    AttachMap(attachments, messageConfig);

                    SendDiscordAttachmentMessage(url, message, attachments);
#else
                SendDiscordMessage(url, message);
#endif
                });
            }
        }
        #endregion
        
        #if RUST
        private void AttachMap(List<Attachment> attachments, DiscordMessageConfig messageConfig)
        {
            if (IsRustMapApiLoaded() && IsRustMapApiReady() && messageConfig.Embed.Image.StartsWith(AttachmentBase))
            {
                Debug(DebugEnum.Info, "AttachMap - RustMapApi is ready, attaching map");
                List<string> maps = RustMapApi.Call<List<string>>("GetSavedMaps");
                string mapName = _pluginConfig.ImageSettings.Name;
                if (maps != null)
                {
                    mapName = maps.FirstOrDefault(m => m.Equals(mapName, StringComparison.InvariantCultureIgnoreCase));
                    if (string.IsNullOrEmpty(mapName))
                    {
                        PrintWarning($"Map name not found {_pluginConfig.ImageSettings.Name}. Valid names are {string.Join(", ", maps.ToArray())}");
                        mapName = "Icons";
                    }
                }
                
                Debug(DebugEnum.Info, $"AttachMap - RustMapApi map name set to: {mapName}");
                int resolution = (int) (World.Size * _pluginConfig.ImageSettings.Scale);
                AttachmentContentType contentType = _pluginConfig.ImageSettings.FileType == EncodingMode.Jpg ? AttachmentContentType.Jpg : AttachmentContentType.Png; 
                object response = RustMapApi.Call("CreatePluginImage", this, mapName, resolution, (int)_pluginConfig.ImageSettings.FileType);
                if (response is string)
                {
                    PrintError($"An error occurred creating the plugin image: {response}");
                    return;
                }

                Hash<string, object> map = response as Hash<string, object>;
                byte[] mapData = map?["image"] as byte[];
                if (mapData != null)
                {
                    if (mapData.Length >= 8 * 1024 * 1024)
                    {
                        PrintError( "Map Image too large. " +
                                               $"Image size is {mapData.Length / 1024.0 / 1024.0:0.00}MB. " +
                                               $"Max size is {MaxImageSize / 1024 / 1024}MB. " +
                                               "Please reduce the \"Image Resolution Scale\"");
                    }
                    else
                    {
                        attachments.Add(new Attachment(mapData, MapFilename, contentType));
                        Debug(DebugEnum.Info, "AttachMap - Successfully attached map");
                    }
                    
                }
                else
                {
                    Debug(DebugEnum.Warning, "AttachMap - MapData was null!!!");
                }
            }
        }
        #endif
        
        #region PlaceholderAPI
        private string ParseFields(string json)
        {
            StringBuilder sb = new StringBuilder(json);

            GetReplacer()?.Invoke(null, sb, false);

            return sb.ToString();
        }
        
        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin?.Name == "PlaceholderAPI")
            {
                _replacer = null;
            }
        }
        
        private void OnPlaceholderAPIReady()
        {
            RegisterPlaceholder("server.protocol.previous", (player, s) => _previousProtocol, "Displays the previous protocol version if it changed during the last restart", double.MaxValue);
        }

        private void RegisterPlaceholder(string key, Func<IPlayer, string, object> action, string description = null, double ttl = double.NaN)
        {
            if (IsPlaceholderApiLoaded())
            {
                PlaceholderAPI.Call("AddPlaceholder", this, key, action, description, ttl);
            }
        }
        
        private Action<IPlayer, StringBuilder, bool> GetReplacer()
        {
            if (!IsPlaceholderApiLoaded())
            {
                return _replacer;
            }
            
            return _replacer ?? (_replacer = PlaceholderAPI.Call<Action<IPlayer, StringBuilder, bool>>("GetProcessPlaceholders", 1));
        }

        private bool IsPlaceholderApiLoaded() => PlaceholderAPI != null && PlaceholderAPI.IsLoaded;
        #endregion

        #region Helpers
        private void Debug(DebugEnum level, string message)
        {
            if (level > _pluginConfig.DebugLevel)
            {
                return;
            }

            switch (level)
            {
                case DebugEnum.Error:
                    PrintError(message);
                    break;
                case DebugEnum.Warning:
                    PrintWarning(message);
                    break;
                default:
                    Puts($"{level}: {message}");
                    break;
            }
        }

        private string Lang(string key, IPlayer player = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, player?.Id), args);
            }
            catch(Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception\n:{ex.Message}");
                throw;
            }
        }

        private bool IsRustMapApiLoaded() => RustMapApi != null && RustMapApi.IsLoaded;
        private bool IsRustMapApiReady() => RustMapApi.Call<bool>("IsReady");
        
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);

        private bool HasPermission(IPlayer player, string perm) => permission.UserHasPermission(player.Id, perm);
        #endregion

        #region Classes
        private class PluginConfig
        {
            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(DebugEnum.Warning)]
            [JsonProperty(PropertyName = "Debug Level (None, Error, Warning, Info)")]
            public DebugEnum DebugLevel { get; set; }
            
            [DefaultValue("dw")]
            [JsonProperty(PropertyName = "Command")]
            public string Command { get; set; }
            
#if RUST
            [JsonProperty(PropertyName = "Rust Map Image Settings")]
            public RustMapImageSettings ImageSettings { get; set; }
#endif
            
            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Send wipe message when server wipes")]
            public bool SendWipeAutomatically { get; set; }
            
            [DefaultValue(DefaultUrl)]
            [JsonProperty(PropertyName = "Wipe Webhook url")]
            public string WipeWebhook { get; set; }
            
            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Send protocol message when server protocol changes")]
            public bool SendProtocolAutomatically { get; set; }
            
            [DefaultValue(DefaultUrl)]
            [JsonProperty(PropertyName = "Protocol Webhook url")]
            public string ProtocolWebhook { get; set; }
            
            [JsonProperty(PropertyName = "Wipe messages")]
            public List<DiscordMessageConfig> WipeEmbeds { get; set; }
            
            [JsonProperty(PropertyName = "Protocol messages")]
            public List<DiscordMessageConfig> ProtocolEmbeds { get; set; }

            [JsonProperty(PropertyName = "Wipe message")]
            public DiscordMessageConfig WipeEmbed { get; set; }
            
            [JsonProperty(PropertyName = "Protocol message")]
            public DiscordMessageConfig ProtocolEmbed { get; set; }

            public bool ShouldSerializeWipeEmbed()
            {
                return WipeEmbed != null;
            }

            public bool ShouldSerializeProtocolEmbed()
            {
                return ProtocolEmbed != null;
            }
        }

#if RUST
        private class RustMapImageSettings
        {
            [DefaultValue("Icons")]
            [JsonProperty(PropertyName = "Render Name")]
            public string Name { get; set; }
            
            [DefaultValue(0.5f)]
            [JsonProperty(PropertyName = "Image Resolution Scale")]
            public float Scale { get; set; }            
            
            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(EncodingMode.Jpg)]
            [JsonProperty(PropertyName = "File Type (Jpg, Png")]
            public EncodingMode FileType { get; set; }
        }
#endif
        
        private class StoredData
        {
            public bool IsWipe { get; set; }
            public string Protocol { get; set; }
        }

        private class LangKeys
        {
            public const string NoPermission = "NoPermission";
            public const string SentWipe = "SentWipe";
            public const string SentProtocol = "SentProtocol";
            public const string Help = "Help";
        }
        #endregion
        
        #region Discord Embed
        #region Send Embed Methods
        /// <summary>
        /// Headers when sending an embeded message
        /// </summary>
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>()
        {
            {"Content-Type", "application/json"}
        };

        /// <summary>
        /// Sends the DiscordMessage to the specified webhook url
        /// </summary>
        /// <param name="url">Webhook url</param>
        /// <param name="message">Message being sent</param>
        private void SendDiscordMessage(string url, DiscordMessage message)
        {
            string messageJson = message.ToJson();
            Debug(DebugEnum.Info, $"SendDiscordMessage - ToJson \n{messageJson}");
            string json = ParseFields( messageJson);
            Debug(DebugEnum.Info, $"SendDiscordMessage - ParseFields \n{json}");
            webrequest.Enqueue(url, json, SendDiscordMessageCallback, this, RequestMethod.POST, _headers);
        }

        /// <summary>
        /// Callback when sending the embed if any errors occured
        /// </summary>
        /// <param name="code">HTTP response code</param>
        /// <param name="message">Response message</param>
        private void SendDiscordMessageCallback(int code, string message)
        {
            if (code != 204)
            {
                PrintError(message);
            }
        }

#if RUST
        /// <summary>
        /// Sends the DiscordMessage to the specified webhook url with attachments
        /// </summary>
        /// <param name="url">Webhook url</param>
        /// <param name="message">Message being sent</param>
        /// <param name="files">Attachments to be added to the DiscordMessage</param>
        private void SendDiscordAttachmentMessage(string url, DiscordMessage message, List<Attachment> files)
        {
            string messageJson = message.ToJson();
            Debug(DebugEnum.Info, $"SendDiscordMessage - ToJson \n{messageJson}");
            string json = ParseFields( messageJson);
            Debug(DebugEnum.Info, $"SendDiscordMessage - ParseFields \n{json}");
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("payload_json", json)
            };

            for (int i = 0; i < files.Count; i++)
            {
                Attachment attachment = files[i];
                formData.Add(new MultipartFormFileSection($"file{i + 1}", attachment.Data, attachment.Filename, attachment.ContentType));
            }

            InvokeHandler.Instance.StartCoroutine(SendDiscordAttachmentMessageHandler(url, formData));
        }

        private IEnumerator SendDiscordAttachmentMessageHandler(string url, List<IMultipartFormSection> data)
        {
            UnityWebRequest www = UnityWebRequest.Post(url, data);
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                PrintError($"{www.error} {www.downloadHandler.text}");
            }
        }
#endif
        #endregion
        
        #region Helper Methods

        private const string OwnerIcon = "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/47/47db946f27bc76d930ac82f1656f7a10707bb67d_full.jpg";

        private void AddPluginInfoFooter(Embed embed)
        {
            embed.AddFooter($"{Title} V{Version} by {Author}", OwnerIcon);
        }

        private string GetPositionField(Vector3 pos)
        {
            return $"{pos.x:0.00} {pos.y:0.00} {pos.z:0.00}";
        }
        #endregion
        
        #region Embed Classes

        private class DiscordMessage
        {
            /// <summary>
            /// The name of the user sending the message changing this will change the webhook bots name
            /// </summary>
            [JsonProperty("username")]
            private string Username { get; set; }

            /// <summary>
            /// The avatar url of the user sending the message changing this will change the webhook bots avatar
            /// </summary>
            [JsonProperty("avatar_url")]
            private string AvatarUrl { get; set; }

            /// <summary>
            /// String only content to be sent
            /// </summary>
            [JsonProperty("content")]
            private string Content { get; set; }

            /// <summary>
            /// Embeds to be sent
            /// </summary>
            [JsonProperty("embeds")]
            private List<Embed> Embeds { get; }

            public DiscordMessage(string username = null, string avatarUrl = null)
            {
                Username = username;
                AvatarUrl = avatarUrl;
                Embeds = new List<Embed>();
            }

            public DiscordMessage(string content, string username = null, string avatarUrl = null)
            {
                Content = content;
                Username = username;
                AvatarUrl = avatarUrl;
                Embeds = new List<Embed>();
            }

            public DiscordMessage(Embed embed, string username = null, string avatarUrl = null)
            {
                Embeds = new List<Embed> {embed};
                Username = username;
                AvatarUrl = avatarUrl;
            }

            /// <summary>
            /// Adds a new embed to the list of embed to send
            /// </summary>
            /// <param name="embed">Embed to add</param>
            /// <returns>This</returns>
            /// <exception cref="IndexOutOfRangeException">Thrown if more than 10 embeds are added in a send as that is the discord limit</exception>
            public DiscordMessage AddEmbed(Embed embed)
            {
                if (Embeds.Count >= 10)
                {
                    throw new IndexOutOfRangeException("Only 10 embed are allowed per message");
                }

                Embeds.Add(embed);
                return this;
            }

            /// <summary>
            /// Adds string content to the message
            /// </summary>
            /// <param name="content"></param>
            /// <returns></returns>
            public DiscordMessage AddContent(string content)
            {
                Content = content;
                return this;
            }

            /// <summary>
            /// Changes the username and avatar image for the bot sending the message
            /// </summary>
            /// <param name="username">username to change</param>
            /// <param name="avatarUrl">avatar img url to change</param>
            /// <returns>This</returns>
            public DiscordMessage AddSender(string username, string avatarUrl)
            {
                Username = username;
                AvatarUrl = avatarUrl;
                return this;
            }

            /// <summary>
            /// Returns message as JSON to be sent in the web request
            /// </summary>
            /// <returns></returns>
            public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None,
                new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore});
        }

        private class Embed
        {
            /// <summary>
            /// Color of the left side bar of the embed message
            /// </summary>
            [JsonProperty("color")]
            private int Color { get; set; }

            /// <summary>
            /// Fields to be added to the embed message
            /// </summary>
            [JsonProperty("fields")]
            private List<Field> Fields { get; } = new List<Field>();

            /// <summary>
            /// Title of the embed message
            /// </summary>
            [JsonProperty("title")]
            private string Title { get; set; }

            /// <summary>
            /// Description of the embed message
            /// </summary>
            [JsonProperty("description")]
            private string Description { get; set; }
            
            /// <summary>
            /// Description of the embed message
            /// </summary>
            [JsonProperty("url")]
            private string Url { get; set; }

            /// <summary>
            /// Image to added to the embed message. Appears at the bottom of the message above the footer
            /// </summary>
            [JsonProperty("image")]
            private Image Image { get; set; }

            /// <summary>
            /// Thumbnail image added to the embed message. Appears in the top right corner
            /// </summary>
            [JsonProperty("thumbnail")]
            private Image Thumbnail { get; set; }

            /// <summary>
            /// Video to add to the embed message
            /// </summary>
            [JsonProperty("video")]
            private Video Video { get; set; }

            /// <summary>
            /// Author to add to the embed message. Appears above the title.
            /// </summary>
            [JsonProperty("author")]
            private AuthorInfo Author { get; set; }

            /// <summary>
            /// Footer to add to the embed message. Appears below all content.
            /// </summary>
            [JsonProperty("footer")]
            private Footer Footer { get; set; }

            /// <summary>
            /// Adds a title to the embed message
            /// </summary>
            /// <param name="title">Title to add</param>
            /// <returns>This</returns>
            public Embed AddTitle(string title)
            {
                Title = title;
                return this;
            }

            /// <summary>
            /// Adds a description to the embed message
            /// </summary>
            /// <param name="description">description to add</param>
            /// <returns>This</returns>
            public Embed AddDescription(string description)
            {
                Description = description;
                return this;
            }

            /// <summary>
            /// Adds a url to the embed message
            /// </summary>
            /// <param name="url"></param>
            /// <returns>This</returns>
            public Embed AddUrl(string url)
            {
                Url = url;
                return this;
            }

            /// <summary>
            /// Adds an author to the embed message. The author will appear above the title
            /// </summary>
            /// <param name="name">Name of the author</param>
            /// <param name="iconUrl">Icon Url to use for the author</param>
            /// <param name="url">Url to go to when the authors name is clicked on</param>
            /// <param name="proxyIconUrl">Backup icon url. Can be left null if you only have one icon url</param>
            /// <returns>This</returns>
            public Embed AddAuthor(string name, string iconUrl = null, string url = null, string proxyIconUrl = null)
            {
                Author = new AuthorInfo(name, iconUrl, url, proxyIconUrl);
                return this;
            }

            /// <summary>
            /// Adds a footer to the embed message
            /// </summary>
            /// <param name="text">Text to be added to the footer</param>
            /// <param name="iconUrl">Icon url to add in the footer. Appears to the left of the text</param>
            /// <param name="proxyIconUrl">Backup icon url. Can be left null if you only have one icon url</param>
            /// <returns>This</returns>
            public Embed AddFooter(string text, string iconUrl = null, string proxyIconUrl = null)
            {
                Footer = new Footer(text, iconUrl, proxyIconUrl);

                return this;
            }

            /// <summary>
            /// Adds an int based color to the embed. Color appears as a bar on the left side of the message
            /// </summary>
            /// <param name="color"></param>
            /// <returns></returns>
            public Embed AddColor(int color)
            {
                if (color < 0x0 || color > 0xFFFFFF)
                {
                    throw new Exception($"Color '{color}' is outside the valid color range");
                }
                
                Color = color;
                return this;
            }

            /// <summary>
            /// Adds a hex based color. Color appears as a bar on the left side of the message
            /// </summary>
            /// <param name="color">Color in string hex format</param>
            /// <returns>This</returns>
            /// <exception cref="Exception">Exception thrown if color is outside of range</exception>
            public Embed AddColor(string color)
            {
                int parsedColor = int.Parse(color.TrimStart('#'), NumberStyles.AllowHexSpecifier);
                if (parsedColor < 0x0 || parsedColor > 0xFFFFFF)
                {
                    throw new Exception($"Color '{color}' is outside the valid color range");
                }

                Color = parsedColor;
                return this;
            }

            /// <summary>
            /// Adds a RGB based color. Color appears as a bar on the left side of the message
            /// </summary>
            /// <param name="red">Red value between 0 - 255</param>
            /// <param name="green">Green value between 0 - 255</param>
            /// <param name="blue">Blue value between 0 - 255</param>
            /// <returns>This</returns>
            /// <exception cref="Exception">Thrown if red, green, or blue is outside of range</exception>
            public Embed AddColor(int red, int green, int blue)
            {
                if (red < 0 || red > 255 || green < 0 || green > 255 || green < 0 || green > 255)
                {
                    throw new Exception($"Color Red:{red} Green:{green} Blue:{blue} is outside the valid color range. Must be between 0 - 255");
                }

                Color = red * 65536 + green * 256 + blue;;
                return this;
            }

            /// <summary>
            /// Adds a blank field.
            /// If inline it will add a blank column.
            /// If not inline will add a blank row
            /// </summary>
            /// <param name="inline">If the field is inline</param>
            /// <returns>This</returns>
            public Embed AddBlankField(bool inline)
            {
                Fields.Add(new Field("\u200b", "\u200b", inline));
                return this;
            }

            /// <summary>
            /// Adds a new field with the name as the title and value as the value.
            /// If inline will add a new column. If row will add in a new row.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="value"></param>
            /// <param name="inline"></param>
            /// <returns></returns>
            public Embed AddField(string name, string value, bool inline)
            {
                Fields.Add(new Field(name, value, inline));
                return this;
            }

            /// <summary>
            /// Adds an image to the embed. The url should point to the url of the image.
            /// If using attachment image you can make the url: "attachment://{image name}.{image extension}
            /// </summary>
            /// <param name="url">Url for the image</param>
            /// <param name="width">width of the image</param>
            /// <param name="height">height of the image</param>
            /// <param name="proxyUrl">Backup url for the image</param>
            /// <returns></returns>
            public Embed AddImage(string url, int? width = null, int? height = null, string proxyUrl = null)
            {
                Image = new Image(url, width, height, proxyUrl);
                return this;
            }

            /// <summary>
            /// Adds a thumbnail in the top right corner of the embed
            /// If using attachment image you can make the url: "attachment://{image name}.{image extension}
            /// </summary>
            /// <param name="url">Url for the image</param>
            /// <param name="width">width of the image</param>
            /// <param name="height">height of the image</param>
            /// <param name="proxyUrl">Backup url for the image</param>
            /// <returns></returns>
            public Embed AddThumbnail(string url, int? width = null, int? height = null, string proxyUrl = null)
            {
                Thumbnail = new Image(url, width, height, proxyUrl);
                return this;
            }

            /// <summary>
            /// Adds a video to the embed
            /// </summary>
            /// <param name="url">Url for the video</param>
            /// <param name="width">Width of the video</param>
            /// <param name="height">Height of the video</param>
            /// <returns></returns>
            public Embed AddVideo(string url, int? width = null, int? height = null)
            {
                Video = new Video(url, width, height);
                return this;
            }
        }

        /// <summary>
        /// Field for and embed message
        /// </summary>
        private class Field
        {
            /// <summary>
            /// Name of the field
            /// </summary>
            [JsonProperty("name")]
            private string Name { get; }

            /// <summary>
            /// Value for the field
            /// </summary>
            [JsonProperty("value")]
            private string Value { get; }

            /// <summary>
            /// If the field should be in the same row or a new row
            /// </summary>
            [JsonProperty("inline")]
            private bool Inline { get; }

            public Field(string name, string value, bool inline)
            {
                Name = name;
                Value = value;
                Inline = inline;
            }
        }

        /// <summary>
        /// Image for an embed message
        /// </summary>
        private class Image
        {
            /// <summary>
            /// Url for the image
            /// </summary>
            [JsonProperty("url")]
            private string Url { get; }

            /// <summary>
            /// Width for the image
            /// </summary>
            [JsonProperty("width")]
            private int? Width { get; }

            /// <summary>
            /// Height for the image
            /// </summary>
            [JsonProperty("height")]
            private int? Height { get; }

            /// <summary>
            /// Proxy url for the image
            /// </summary>
            [JsonProperty("proxyURL")]
            private string ProxyUrl { get; }

            public Image(string url, int? width, int? height, string proxyUrl)
            {
                Url = url;
                Width = width;
                Height = height;
                ProxyUrl = proxyUrl;
            }
        }

        /// <summary>
        /// Video for an embed message
        /// </summary>
        private class Video
        {
            /// <summary>
            /// Url to the video
            /// </summary>
            [JsonProperty("url")]
            private string Url { get; }

            /// <summary>
            /// Width of the video
            /// </summary>
            [JsonProperty("width")]
            private int? Width { get; }

            /// <summary>
            /// Height of the video
            /// </summary>
            [JsonProperty("height")]
            private int? Height { get; }

            public Video(string url, int? width, int? height)
            {
                Url = url;
                Width = width;
                Height = height;
            }
        }

        /// <summary>
        /// Author of an embed message
        /// </summary>
        private class AuthorInfo
        {
            /// <summary>
            /// Name of the author
            /// </summary>
            [JsonProperty("name")]
            private string Name { get; }

            /// <summary>
            /// Url to go to when clicking on the authors name
            /// </summary>
            [JsonProperty("url")]
            private string Url { get; }

            /// <summary>
            /// Icon url for the author
            /// </summary>
            [JsonProperty("icon_url")]
            private string IconUrl { get; }

            /// <summary>
            /// Proxy icon url for the author
            /// </summary>
            [JsonProperty("proxy_icon_url")]
            private string ProxyIconUrl { get; }

            public AuthorInfo(string name, string iconUrl, string url, string proxyIconUrl)
            {
                Name = name;
                Url = url;
                IconUrl = iconUrl;
                ProxyIconUrl = proxyIconUrl;
            }
        }

        /// <summary>
        /// Footer for an embed message
        /// </summary>
        private class Footer
        {
            /// <summary>
            /// Text for the footer
            /// </summary>
            [JsonProperty("text")]
            private string Text { get; }

            /// <summary>
            /// Icon url for the footer
            /// </summary>
            [JsonProperty("icon_url")]
            private string IconUrl { get; }

            /// <summary>
            /// Proxy icon url for the footer
            /// </summary>
            [JsonProperty("proxy_icon_url")]
            private string ProxyIconUrl { get; }

            public Footer(string text, string iconUrl, string proxyIconUrl)
            {
                Text = text;
                IconUrl = iconUrl;
                ProxyIconUrl = proxyIconUrl;
            }
        }

        #endregion

        #region Attachment Classes
        /// <summary>
        /// Enum for attachment content type
        /// </summary>
        private enum AttachmentContentType
        {
            Png,
            Jpg
        }

        private class Attachment
        {
            /// <summary>
            /// Attachment data
            /// </summary>
            public byte[] Data { get; }
            
            /// <summary>
            /// File name for the attachment.
            /// Used in the url field of an image
            /// </summary>
            public string Filename { get; }
            
            /// <summary>
            /// Content type for the attachment
            /// https://developer.mozilla.org/en-US/docs/Web/HTTP/Basics_of_HTTP/MIME_types
            /// </summary>
            public string ContentType { get; }

            public Attachment(byte[] data, string filename, AttachmentContentType contentType)
            {
                Data = data;
                Filename = filename;

                switch (contentType)
                {
                    case AttachmentContentType.Jpg:
                        ContentType = "image/jpeg";
                        break;
                    
                    case AttachmentContentType.Png:
                        ContentType = "image/png";
                        break;
                }
            }

            public Attachment(byte[] data, string filename, string contentType)
            {
                Data = data;
                Filename = filename;
                ContentType = contentType;
            }
        }
        
        #endregion

        #region Config Classes

        public class DiscordMessageConfig
        {
            public string Content { get; set; }
            
            [JsonProperty("Send Mode (Always, Random)")]
            [JsonConverter(typeof(StringEnumConverter))]
            public SendMode SendMode { get; set; }
            
            public EmbedConfig Embed { get; set; }
        }
        
        public class EmbedConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled { get; set; }
            
            [JsonProperty("Title")]
            public string Title { get; set; }
            
            [JsonProperty("Description")]
            public string Description { get; set; }
            
            [JsonProperty("Url")]
            public string Url { get; set; }
            
            [JsonProperty("Embed Color")]
            public string Color { get; set; }
            
            [JsonProperty("Image Url")]
            public string Image { get; set; }
            
            [JsonProperty("Thumbnail Url")]
            public string Thumbnail { get; set; }
            
            [JsonProperty("Fields")]
            public List<FieldConfig> Fields { get; set; }
            
            [JsonProperty("Footer")]
            public FooterConfig Footer { get; set; }
        }
        
        public class FieldConfig
        {
            [JsonProperty("Title")]
            public string Title { get; set; }
            
            [JsonProperty("Value")]
            public string Value { get; set; }
            
            [JsonProperty("Inline")]
            public bool Inline { get; set; }

            [JsonProperty("Enabled")]
            public bool Enabled { get; set; }
        }

        public class FooterConfig
        {
            [JsonProperty("Icon Url")]
            public string IconUrl { get; set; }
            
            [JsonProperty("Text")]
            public string Text { get; set; }
            
            [JsonProperty("Enabled")]
            public bool Enabled { get; set; }
        }
        #endregion
        
        #region Config Methods
        private DiscordMessage ParseMessage(DiscordMessageConfig config)
        {
            DiscordMessage message = new DiscordMessage();

            if (!string.IsNullOrEmpty(config.Content))
            {
                message.AddContent(config.Content);
            }

            if (config.Embed != null && config.Embed.Enabled)
            {
                Embed embed = new Embed();
                if (!string.IsNullOrEmpty(config.Embed.Title))
                {
                    embed.AddTitle(config.Embed.Title);
                }

                if (!string.IsNullOrEmpty(config.Embed.Description))
                {
                    embed.AddDescription(config.Embed.Description);
                }
                
                if (!string.IsNullOrEmpty(config.Embed.Url))
                {
                    embed.AddUrl(config.Embed.Url);
                }

                if (!string.IsNullOrEmpty(config.Embed.Color))
                {
                    embed.AddColor(config.Embed.Color);
                }

                if (!string.IsNullOrEmpty(config.Embed.Image))
                {
                    embed.AddImage(config.Embed.Image);
                }

                if (!string.IsNullOrEmpty(config.Embed.Thumbnail))
                {
                    embed.AddThumbnail(config.Embed.Thumbnail);
                }

                foreach (FieldConfig field in config.Embed.Fields.Where(f => f.Enabled))
                {
                    string value = field.Value;
                    if (string.IsNullOrEmpty(value))
                    {
                        PrintWarning($"Field: {field.Title} was skipped because the value was null or empty.");
                        continue;
                    }
                    
                    embed.AddField(field.Title, value, field.Inline);
                }

                if (config.Embed.Footer != null && config.Embed.Footer.Enabled)
                {
                    if (string.IsNullOrEmpty(config.Embed.Footer.Text) &&
                        string.IsNullOrEmpty(config.Embed.Footer.IconUrl))
                    {
                        AddPluginInfoFooter(embed);
                    }
                    else
                    {
                        embed.AddFooter(config.Embed.Footer.Text, config.Embed.Footer.IconUrl);
                    }
                }
                
                message.AddEmbed(embed);
            }
            
            return message;
        }
        #endregion
        #endregion
    }
}
