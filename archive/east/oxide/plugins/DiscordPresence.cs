using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord.DiscordObjects;

namespace Oxide.Plugins
{
    [Info("Discord Presence", "MJSU", "1.0.3")]
    [Description("Displays the discord presence text as the bots status")]
    internal class DiscordPresence : CovalencePlugin
    {
        #region Class Fields
        [PluginReference] private Plugin DiscordCore, PlaceholderAPI;

        private PluginConfig _pluginConfig;

        private Action<IPlayer, StringBuilder> _replacer;
        
        private int _index;
        #endregion

        #region Setup & Loading
        private void OnServerInitialized()
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
            if (PlaceholderAPI == null || !PlaceholderAPI.IsLoaded)
            {
                PrintError("Missing plugin dependency PlaceholderAPI: https://umod.org/plugins/placeholder-api");
                return;
            }
            
            if(PlaceholderAPI.Version < new VersionNumber(2, 0, 0))
            {
                PrintError("Placeholder API plugin must be version 2.0.0 or higher");
                return;
            }

            if (!IsDiscordCoreReady())
            {
                return;
            }

            timer.In(5f, UpdatePresence);
            timer.Every(_pluginConfig.UpdateRate, UpdatePresence);
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
            config.PresenceText = config.PresenceText ?? new List<string>
            {
                "on {server.name}",
                "{server.players}/{server.players.max} Players",
                "{server.players.sleepers} Sleepers",
                "{server.players.stored} Total Players",
#if RUST
                "{server.players.total} Lifetime Players",
                "{server.players.queued} Queued",
                "{server.players.loading} Joining",
#endif
                "Server FPS {server.fps}",
#if RUST
                "Wiped: {server.map.wipe.last!local}",
                "Size: {world.size} Seed: {world.seed}",         
#endif
                "{server.entities} Entities"
            };
            return config;
        }

        private void UpdatePresence()
        {
            if (_pluginConfig.PresenceText.Count == 0)
            {
                PrintError("Presence Text formats contains no values. Please add some to your config");
                return;
            }

            string field = ParseFields(_pluginConfig.PresenceText[_index]);

            if (!IsDiscordCoreReady())
            {
                return;
            }
            
            DiscordCore.Call("UpdatePresence", new Presence
            {
                Game = new Ext.Discord.DiscordObjects.Game
                {
                    Name = field,
                    Type = _pluginConfig.ActivityType
                },
                Status = "online",
                Since = 0,
                AFK = false
            });

            _index = ++_index % _pluginConfig.PresenceText.Count;
        }

        private bool IsDiscordCoreReady()
        {
            return DiscordCore != null && DiscordCore.IsLoaded && DiscordCore.Call<bool>("IsReady");
        }
        #endregion
        
        #region PlaceholderAPI
        private string ParseFields(string json)
        {
            StringBuilder sb = new StringBuilder(json);
            
            GetReplacer()?.Invoke(null, sb);
            
            sb.Replace("\\n", "\n");
            return sb.ToString();
        }
        
        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin?.Name == "PlaceholderAPI")
            {
                _replacer = null;
            }
        }
        
        private void RegisterPlaceholder(string key, Func<IPlayer, string, object> action, string description = null)
        {
            if (IsPlaceholderApiLoaded())
            {
                PlaceholderAPI.Call("AddPlaceholder", this, key, action, description);
            }
        }
        
        private Action<IPlayer, StringBuilder> GetReplacer()
        {
            if (!IsPlaceholderApiLoaded())
            {
                return _replacer;
            }
            
            return _replacer ?? (_replacer = PlaceholderAPI.Call<Action<IPlayer, StringBuilder>>("GetProcessPlaceholders"));
        }

        private bool IsPlaceholderApiLoaded() => PlaceholderAPI != null && PlaceholderAPI.IsLoaded;
        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue(15f)]
            [JsonProperty(PropertyName = "Update Rate (Seconds)")]
            public float UpdateRate { get; set; }
            
            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(ActivityType.Game)]
            [JsonProperty(PropertyName = "Activity Type (Game, Streaming, Listening, Watching)")]
            public ActivityType ActivityType { get; set; }

            [JsonProperty(PropertyName = "Presence Text formats")]
            public List<string> PresenceText { get; set; }
        }
        #endregion
    }
}