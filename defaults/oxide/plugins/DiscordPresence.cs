using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord;
using Oxide.Ext.Discord.Attributes;
using Oxide.Ext.Discord.Constants;
using Oxide.Ext.Discord.Entities.Activities;
using Oxide.Ext.Discord.Entities.Gatway;
using Oxide.Ext.Discord.Entities.Gatway.Commands;
using Oxide.Ext.Discord.Entities.Gatway.Events;
using Oxide.Ext.Discord.Entities.Users;
using Oxide.Ext.Discord.Logging;

namespace Oxide.Plugins
{
    [Info("Discord Presence", "MJSU", "2.0.4")]
    [Description("Displays the discord presence text as the bots status")]
    internal class DiscordPresence : CovalencePlugin
    {
        #region Class Fields
        [DiscordClient] private DiscordClient _client;
        
        [PluginReference] private Plugin PlaceholderAPI;

        private PluginConfig _pluginConfig;

        private Action<IPlayer, StringBuilder, bool> _replacer;
        
        private int _index;
        private readonly StringBuilder _sb = new StringBuilder();

        private DiscordSettings _discordSettings;

        private readonly UpdatePresenceCommand _status = new UpdatePresenceCommand
        {
            Afk = false,
            Since = 0,
            Status = UserStatusType.Online
        };

        private readonly DiscordActivity _activity = new DiscordActivity();

        private Timer _timer;
        #endregion

        #region Setup & Loading
        private void Init()
        {
            _discordSettings = new DiscordSettings
            {
                ApiToken = _pluginConfig.Token,
                LogLevel = _pluginConfig.ExtensionDebugging,
                Intents = GatewayIntents.None
            };

            _status.Activities = new List<DiscordActivity> {_activity};
            if (_pluginConfig.UpdateRate < 0.5f)
            {
                _pluginConfig.UpdateRate = 0.5f;
            }
        }
        
        private void OnServerInitialized()
        {
            if(IsPlaceholderApiLoaded() && PlaceholderAPI.Version < new VersionNumber(2, 2, 0))
            {
                PrintError("Placeholder API plugin must be version 2.2.0 or higher");
                return;
            }

            if (string.IsNullOrEmpty(_pluginConfig.Token))
            {
                PrintWarning("Please enter your bot token in the config and reload the plugin.");
                return;
            }
            
            _client.Connect(_discordSettings);
        }

        [HookMethod(DiscordExtHooks.OnDiscordGatewayReady)]
        private void OnDiscordGatewayReady(GatewayReadyEvent ready)
        {
            Puts($"{Title} Ready");
            timer.In(1f, UpdatePresence);
            _timer = timer.Every(_pluginConfig.UpdateRate, UpdatePresence);
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
            config.StatusMessages = config.StatusMessages ?? new List<MessageSettings>
            {
                new MessageSettings("on {server.name}", ActivityType.Game),
                new MessageSettings("{server.players}/{server.players.max} Players", ActivityType.Game),
                new MessageSettings("{server.players.sleepers} Sleepers", ActivityType.Game),
                new MessageSettings("{server.players.stored} Total Players", ActivityType.Game),
                new MessageSettings("Server FPS {server.fps}", ActivityType.Game),
                new MessageSettings("{server.entities} Entities", ActivityType.Game),
                new MessageSettings("{server.players.total} Lifetime Players", ActivityType.Game),
                #if RUST
                new MessageSettings("{server.entities} Entities", ActivityType.Game),
                new MessageSettings("{server.players.queued} Queued", ActivityType.Game),
                new MessageSettings("{server.players.loading} Joining", ActivityType.Game),
                new MessageSettings("Wiped: {server.map.wipe.last!local}", ActivityType.Game),
                new MessageSettings("Size: {world.size} Seed: {world.seed}", ActivityType.Game)
                #endif
            };

            for (int index = 0; index < config.StatusMessages.Count; index++)
            {
                config.StatusMessages[index] = new MessageSettings(config.StatusMessages[index]);
            }

            return config;
        }

        private void Unload()
        {
            _timer?.Destroy();
        }

        private void UpdatePresence()
        {
            if (_pluginConfig.StatusMessages.Count == 0)
            {
                PrintError("Presence Text formats contains no values. Please add some to your config");
                return;
            }

            MessageSettings message = _pluginConfig.StatusMessages[_index];

            _sb.Length = 0;
            _sb.Append(message.Message);

            GetReplacer()?.Invoke(null, _sb, false);

            _activity.Name = _sb.ToString();
            _activity.Type = message.Type;

            _client?.Bot?.UpdateStatus(_status);

            _index = ++_index % _pluginConfig.StatusMessages.Count;
        }

        #endregion
        
        #region PlaceholderAPI
        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin?.Name == "PlaceholderAPI")
            {
                _replacer = null;
            }
        }

        private Action<IPlayer, StringBuilder, bool> GetReplacer()
        {
            if (!IsPlaceholderApiLoaded())
            {
                return _replacer;
            }

            if (_replacer == null)
            {
                _replacer = PlaceholderAPI.Call<Action<IPlayer, StringBuilder, bool>>("GetProcessPlaceholders", 1);
            }
            
            return _replacer;
        }

        private bool IsPlaceholderApiLoaded() => PlaceholderAPI != null && PlaceholderAPI.IsLoaded;
        #endregion

        #region Classes
        public class PluginConfig
        {
            [DefaultValue("")]
            [JsonProperty(PropertyName = "Discord Application Bot Token")]
            public string Token { get; set; }
            
            [DefaultValue(15f)]
            [JsonProperty(PropertyName = "Update Rate (Seconds)")]
            public float UpdateRate { get; set; }

            [JsonProperty(PropertyName = "Presence Messages")]
            public List<MessageSettings> StatusMessages { get; set; }
            
            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(DiscordLogLevel.Info)]
            [JsonProperty(PropertyName = "Discord Extension Log Level (Verbose, Debug, Info, Warning, Error, Exception, Off)")]
            public DiscordLogLevel ExtensionDebugging { get; set; }
        }

        public class MessageSettings
        {
            public string Message { get; set; }
            
            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(ActivityType.Game)]
            public ActivityType Type { get; set; }

            [JsonConstructor]
            public MessageSettings()
            {
                
            }
            
            public MessageSettings(string message, ActivityType type)
            {
                Message = message;
                Type = type;
            }
            
            public MessageSettings(MessageSettings settings)
            {
                Message = settings?.Message ?? string.Empty;
                Type = settings?.Type ?? ActivityType.Game;
            }
        }
        #endregion
    }
}