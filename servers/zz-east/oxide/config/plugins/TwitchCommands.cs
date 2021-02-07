// Requires: Twitch

using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Twitch Commands", "Wulf", "1.0.0")]
    [Description("Runs commands when a player follows/unfollows on Twitch")]
    class TwitchCommands : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        class Configuration
        {
            [JsonProperty("Run commands on player follow")]
            public bool RunFollowCommands { get; set; } = false;

            [JsonProperty("Commands to perform on follow", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> FollowCommands = new List<string> { "twitchexample1", "twitchexample2 $player.id" };

            [JsonProperty("Run commands on player unfollow")]
            public bool RunUnfollowCommands { get; set; } = false;

            [JsonProperty("Commands to perform on unfollow", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> UnfollowCommands = new List<string> { "twitchexample3 $player.name", "twitchexample4 $player.address" };

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

        #region Initialization

        private void Init()
        {
            if (!config.RunFollowCommands)
            {
                Unsubscribe(nameof(OnTwitchFollow));
            }
            if (!config.RunUnfollowCommands)
            {
                Unsubscribe(nameof(OnTwitchUnfollow));
            }
        }

        #endregion Initialization

        #region Hook Handling

        private void OnTwitchFollow(IPlayer player)
        {
            foreach (string command in config.FollowCommands)
            {
                server.Command(ReplacePlaceholders(command, player));
            }
        }

        private void OnTwitchUnfollow(IPlayer player)
        {
            foreach (string command in config.UnfollowCommands)
            {
                server.Command(ReplacePlaceholders(command, player));
            }
        }

        #endregion Hook Handling

        #region Helpers

        private string ReplacePlaceholders(string command, IPlayer player)
        {
            return command
                .Replace("$player.id", player.Id)
                .Replace("$player.name", player.Name)
                .Replace("$player.address", player.Address)
                .Replace("$player.position", player.Position().ToString());
        }

        #endregion Helpers
    }
}
