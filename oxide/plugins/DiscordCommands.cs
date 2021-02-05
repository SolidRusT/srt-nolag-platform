using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Discord Commands", "MJSU", "0.13.1")]
    [Description("Allows using discord to execute commands")]
    internal class DiscordCommands : CovalencePlugin
    {
        #region Class Fields
        [PluginReference] private Plugin DiscordCore;

        private PluginConfig _pluginConfig; //Plugin Config

        private const string ExecutePermission = "discordcommands.execute";
        #endregion

        #region Setup & Loading

        private void Init()
        {
            permission.RegisterPermission(ExecutePermission, this);
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

            DiscordCore.Call("RegisterCommand", _pluginConfig.DiscordCommand, this, new Func<IPlayer, string, string, string[], object>(HandleCommand), LangKeys.CommandInfoText, ExecutePermission, _pluginConfig.AllowInBotChannel);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.CommandInfoText] = "To execute a command on the server",
                [LangKeys.RanCommand] = "Ran Command",
                [LangKeys.CommandLogging] = "{0} ran command '{1}'",
                [LangKeys.CommandHelpText] = "Send commands to the rust server:\n" +
                                             "Type /{0} {{command}} - to execute that command on the server\n" +
                                             "Example: /{0} o.reload DiscordCommand"
            }, this);
        }

        private void Unload()
        {
            DiscordCore.Call("UnregisterCommand", _pluginConfig.DiscordCommand, this);
        }
        #endregion

        #region Discord Chat Command
        private object HandleCommand(IPlayer player, string channelId, string cmd, string[] args)
        {
            string log = Lang(LangKeys.CommandLogging, player, player.Name, string.Join(" ", args));
            if (!string.IsNullOrEmpty(_pluginConfig.CommandLogging))
            {
                SendMessage(_pluginConfig.CommandLogging, log);
            }

            if (_pluginConfig.LogToConsole)
            {
                Puts(log);
            }
            
            if (args.Length == 0)
            {
                SendMessage(channelId, Lang(LangKeys.CommandHelpText, player, _pluginConfig.DiscordCommand));
                return null;
            }

            server.Command(args[0], args.Skip(1).ToArray());
            SendMessage(channelId, $"{Lang(LangKeys.RanCommand, player)}: {string.Join(" ", args)}");
            return null;
        }

        private void SendMessage(string channelId, string message)
        {
            DiscordCore.Call("SendMessageToChannel", channelId, $"{Title}: {message}");
        }
        #endregion

        #region Helper Methods
        private string Lang(string key, IPlayer player = null, params object[] args) => string.Format(lang.GetMessage(key, this, player?.Id), args);
        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue("command")]
            [JsonProperty(PropertyName = "Discord Command Command")]
            public string DiscordCommand { get; set; }
            
            [DefaultValue("")]
            [JsonProperty(PropertyName = "Command Usage Logging Channel Id")]
            public string CommandLogging { get; set; }
            
            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Log command usage in server console")]
            public bool LogToConsole { get; set; }
            
            [DefaultValue(false)]
            [JsonProperty(PropertyName = "Allow command usage in bot channel")]
            public bool AllowInBotChannel { get; set; }
        }

        private static class LangKeys
        {
            public const string CommandInfoText = "CommandInfoText";
            public const string CommandHelpText = "CommandHelpTextV2";
            public const string RanCommand = "RanCommand";
            public const string CommandLogging = "CommandLogging";
        }
        #endregion
    }
}
