using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Discord Players", "MJSU", "0.13.0")]
    [Description("Displays online players in discord")]
    internal class DiscordPlayers : CovalencePlugin
    {
        #region Class Fields
        [PluginReference] private Plugin DiscordCore, Clans;

        private PluginConfig _pluginConfig; //Plugin Config
        #endregion

        #region Setup & Loading
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.OnlineFormat] = "{0} Online: \n{1}"
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

            DiscordCore.Call("RegisterCommand", _pluginConfig.PlayersCommand, this, new Func<IPlayer, string, string, string[], object>(HandlePlayers), "To view the online players", null, true);
        }

        private void Unload()
        {
            DiscordCore?.Call("UnregisterCommand", _pluginConfig.PlayersCommand, this);
        }
        #endregion

        #region Discord Chat Command
        private object HandlePlayers(IPlayer player, string channelId, string cmd, string[] args)
        {
            List<string> onlineList = GetPlayers();
            SendMessage(channelId,   $"{Title}: {Lang(LangKeys.OnlineFormat, player, players.Connected.Count(), onlineList[0])}");

            for (int i = 1; i < onlineList.Count; i++)
            {
                SendMessage(channelId, onlineList[i]);
            }
            
            return null;
        }

        private void SendMessage(string channelId, string message)
        {
            DiscordCore.Call("SendMessageToChannel", channelId, message);
        }
        #endregion

        #region Helper Methods
        private List<string> GetPlayers()
        {
            StringBuilder sb = new StringBuilder();
            List<string> playerLists = new List<string>();
            foreach (IPlayer connectPlayer in players.Connected)
            {
                string name = GetDisplayName(connectPlayer);
                if (sb.Length + name.Length > 1850)
                {
                    playerLists.Add(sb.ToString());
                    sb.Length = 0;
                }

                sb.Append(name);
                sb.Append(_pluginConfig.Separator);
            }
            
            playerLists.Add(sb.ToString());

            return playerLists;
        }

        private string GetDisplayName(IPlayer player)
        {
            string clanTag = Clans?.Call<string>("GetClanOf", player);
            if (!string.IsNullOrEmpty(clanTag))
            {
                clanTag = $"[{clanTag}] ";
            }

            return _pluginConfig.PlayerNameFormat
                .Replace("{clan}", clanTag)
                .Replace("{name}", player.Name)
                .Replace("{steamid}", player.Id);
        }

        private string Lang(string key, IPlayer player = null, params object[] args) => string.Format(lang.GetMessage(key, this, player?.Id), args);
        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue("players")]
            [JsonProperty(PropertyName = "Players Discord Bot Command")]
            public string PlayersCommand { get; set; }
            
            [DefaultValue("{clan}{name} ({steamid})")]
            [JsonProperty(PropertyName = "Player name format")]
            public string PlayerNameFormat { get; set; }
            
            [DefaultValue("\n")]
            [JsonProperty(PropertyName = "Player name separator")]
            public string Separator { get; set; }
        }


        private static class LangKeys
        {
            public const string OnlineFormat = "OnlineFormat1";
        }

        #endregion
    }
}
