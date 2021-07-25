using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Death Chat", "Tricky", "1.1.2")]
    [Description("Provides ability to customize prefix for someone who has just died")]
    public class DeathChat : RustPlugin
    {
        #region PluginReferences
        [PluginReference]
        private Plugin BetterChat;
        #endregion

        #region Config
        Configuration config;

        class Configuration
        {
            [JsonProperty(PropertyName = "Track Player Kills Only")]
            public bool PlayerKills = true;

            [JsonProperty(PropertyName = "Time Until Prefix will show (seconds)")]
            public int Time = 60;

            [JsonProperty(PropertyName = "Prefix")]
            public string Prefix = "[RIP]";

            [JsonProperty(PropertyName = "Prefix Color")]
            public string PrefixColor = "#EE3B3B";

            [JsonProperty(PropertyName = "Prefix Size")]
            public int PrefixSize = 14;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Collection
        List<string> _players = new List<string>();
        #endregion

        #region Oxide Hooks
        private void OnServerInitialized()
        {
            if (BetterChat != null)
                Unsubscribe(nameof(OnPlayerChat));
        }
        #endregion

        #region Work with Data
        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (!config.PlayerKills && !(info.HitEntity is BasePlayer))
                return;

            _players.Add(player.UserIDString);

            timer.In(config.Time, () =>
            {
                _players.Remove(player.UserIDString);
            });
        }
        #endregion

        #region Chat Hooks
        private object OnPlayerChat(BasePlayer player, string message)
        {
            if (!_players.Contains(player.UserIDString))
                return null;

            var title = $"<color={config.PrefixColor}><size={config.PrefixSize}>{config.Prefix}</size></color> <color=#5af>{player.displayName}</color>";

            Server.Broadcast(message, title, player.userID);
            return true;
        }

        private object OnBetterChat(Dictionary<string, object> data)
        {
            var player = (IPlayer)data["Player"];

            if (!_players.Contains(player.Id))
                return null;

            var titles = (List<string>)data["Titles"];
            var deathtitle = $"<color={config.PrefixColor}><size={config.PrefixSize}>{config.Prefix}</size></color>";

            titles.Add(deathtitle);
            data["Titles"] = titles;
            return data;
        }
        #endregion
    }
}
