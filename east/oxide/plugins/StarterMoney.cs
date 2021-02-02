using Oxide.Core;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;

// Cash System support implemented by misticos

namespace Oxide.Plugins
{
    [Info("Starter Money", "Tricky", "1.1.0")]
    [Description("Customizes the starting balance of players with perms")]
    public class StarterMoney : CovalencePlugin
    {
        #region Plugin References
        [PluginReference]
        private Plugin Economics, ServerRewards, CashSystem;
        #endregion

        #region Config
        Configuration config;

        class Configuration
        {
            [JsonProperty(PropertyName = "Clear Data On New Save (Rust)")]
            public bool ClearData = false;

            [JsonProperty(PropertyName = "Use Economics")]
            public bool Economics = true;

            [JsonProperty(PropertyName = "Use Server Rewards")]
            public bool ServerRewards = false;

            [JsonProperty(PropertyName = "Use Cash System")]
            public bool CashSystem = false;

            [JsonProperty(PropertyName = "Cash System Currency")]
            public string CashSystemCurrency = "$";

            [JsonProperty(PropertyName = "Permissions", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, double> Permissions = new Dictionary<string, double>
            {
                {"startermoney.default", 10 },
                {"startermoney.vip", 20 },
                {"startermoney.god", 30 }
            };
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

        #region Data
        private Data data;

        private class Data
        {
            public List<string> Players = new List<string>();
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, data);
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);

            config.Permissions.Keys.ToList().ForEach(perm =>
            {
                if (!permission.PermissionExists(perm, this))
                    permission.RegisterPermission(perm, this);
            });
        }

        private void Loaded()
        {
            if (config.Economics && Economics == null)
                PrintWarning("Economics is enabled but not loaded!");

            if (config.ServerRewards && ServerRewards == null)
                PrintWarning("Server Rewards is enabled but not loaded!");

            if (config.CashSystem && CashSystem == null)
                PrintWarning("Cash System is enabled but not loaded!");
        }

        private void OnServerSave() => SaveData();

        private void Unload() => SaveData();

        private void OnUserConnected(IPlayer player)
        {
            if (data.Players.Contains(player.Id))
                return;

            config.Permissions.ToList().ForEach(perm =>
            {
                if (!player.HasPermission(perm.Key))
                    return;

                if (config.Economics && Economics)
                    TryGiveEconomics(player, perm.Value);

                if (config.ServerRewards && ServerRewards)
                    TryGiveRP(player, Convert.ToInt32(perm.Value));

                if (config.CashSystem && CashSystem)
                    TryGiveCS(player, perm.Value);
            });
        }

        private void OnNewSave()
        {
            if (!config.ClearData)
                return;

            data.Players.Clear();
        }
        #endregion

        #region Core
        private void TryGiveEconomics(IPlayer player, double amount)
        {
            if (Economics.Call<bool>("Deposit", player.Id, amount))
                data.Players.Add(player.Id);
        }

        private void TryGiveRP(IPlayer player, int amount)
        {
            if (ServerRewards.Call<bool>("AddPoints", player.Id, amount))
                data.Players.Add(player.Id);
        }

        private void TryGiveCS(IPlayer player, double amount)
        {
            if (CashSystem.Call<bool>("AddTransaction", ulong.Parse(player.Id), config.CashSystemCurrency, amount, "Starter Money"))
                data.Players.Add(player.Id);
        }
        #endregion
    }
}