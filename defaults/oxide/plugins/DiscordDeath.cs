using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord.DiscordObjects;

namespace Oxide.Plugins
{
    [Info("Discord Death", "MJSU", "0.10.5")]
    [Description("Displays deaths to a discord channel")]
    internal class DiscordDeath : CovalencePlugin
    {
        #region Class Fields
        [PluginReference] private readonly Plugin DiscordCore;

        private PluginConfig _pluginConfig;

        private bool _init;
        #endregion

        #region Setup & Loading
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Death] = "({0:HH:mm}) {1}"
            }, this);
        }
        
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
            if (!(DiscordCore?.Call<bool>("IsReady") ?? false))
            {
                return;
            }

            Channel channel = DiscordCore.Call<Channel>("GetChannel", _pluginConfig.DeathChannel);
            if (channel == null)
            {
                PrintError($"Failed to find a channel with the name or id {_pluginConfig.DeathChannel} in the discord");
            }

            _init = true;
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
            return config;
        }
        #endregion

        #region Death Hook
        private void OnDeathNotice(Dictionary<string, object> data, string message)
        {
            if (!_init)
            {
                return;
            }

            SendChatToChannel(Lang(LangKeys.Death, null, DateTime.Now, message));
        }
        #endregion

        #region Helpers
        private void SendChatToChannel(string message)
        {
            DiscordCore.Call("SendMessageToChannel", _pluginConfig.DeathChannel, message);
        }
        
        private string Lang(string key, BasePlayer player = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, player?.UserIDString), args);
            }
            catch (Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception\n:{ex}");
                throw;
            }
        }
        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue("Deaths")]
            [JsonProperty("Deaths Channel Name or Id")]
            public string DeathChannel { get; set; }
        }
        
        private static class LangKeys
        {
            public const string Death = "Death";
        }
        #endregion

    }
}
