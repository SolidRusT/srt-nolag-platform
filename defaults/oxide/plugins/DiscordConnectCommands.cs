using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Discord Connect Commands", "Iv Misticos", "1.0.4")]
    [Description("Execute commands on Discord Connect events")]
    class DiscordConnectCommands : CovalencePlugin
    {
        #region Variables

        private StringBuilder _builder = new StringBuilder();
        
        #endregion
        
        #region Configuration

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands On Connect",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> CommandsConnect = new List<string>
            {
                "exampleCommand {gameId} {discordId}"
            };

            [JsonProperty(PropertyName = "Commands On Overwrite",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> CommandsOverwrite = new List<string>
            {
                "exampleCommand {oldGameId} {newGameId} {oldDiscordId} {newDiscordId}"
            };

            [JsonProperty(PropertyName = "Commands On Server Leave",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> CommandsLeave = new List<string>
            {
                "exampleCommand {gameId} {discordId}"
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
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

        #region Methods

        private string FormatCommand(string command, string gameId, string discordId)
        {
            _builder.Length = 0;
            return _builder.Append(command).Replace("{gameId}", gameId).Replace("{discordId}", discordId).ToString();
        }
        private string FormatCommand(string command, string oldGameId, string newGameId, string oldDiscordId,
            string newDiscordId)
        {
            _builder.Length = 0;
            return _builder.Append(command).Replace("{oldGameId}", oldGameId)
                    .Replace("{newGameId}", newGameId).Replace("{oldDiscordId}", oldDiscordId)
                    .Replace("{newDiscordId}", newDiscordId).ToString();
        }
        private void ExecuteCommand(string command) => server.Command(command);

        #endregion

        #region Hooks

        private void OnDiscordAuthenticate(string gameId, string discordId)
        {
            foreach (var command in _config.CommandsConnect)
                ExecuteCommand(FormatCommand(command, gameId, discordId));
        }

        private void OnDiscordAuthOverwrite(string oldGameId, string newGameId, string oldDiscordId,
            string newDiscordId)
        {
            foreach (var command in _config.CommandsOverwrite)
                ExecuteCommand(FormatCommand(command, oldGameId, newGameId, oldDiscordId, newDiscordId));
        }

        private void OnDiscordAuthLeave(string gameId, string discordId)
        {
            foreach (var command in _config.CommandsLeave)
                ExecuteCommand(FormatCommand(command, gameId, discordId));
        }
        
        #endregion
    }
}