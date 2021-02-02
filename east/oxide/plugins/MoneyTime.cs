// Require: Economics

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Money Time", "Wulf", "2.0.2")]
    [Description("Pays players with Economics money for playing")]
    public class MoneyTime : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            // TODO: Add option for daily/weekly login bonuses
            // TODO: Add option to disable payout for AFK players (use AFK plugin)

            [JsonProperty("Base payout amount")]
            public int BasePayout = 100;

            [JsonProperty("Payout interval (seconds)")]
            public int PayoutInterval = 600;

            [JsonProperty("Time alive bonus")]
            public bool TimeAliveBonus = false;

            [JsonProperty("Time alive multiplier")]
            public float TimeAliveMultiplier = 0f;

            [JsonProperty("New player welcome bonus")]
            public float WelcomeBonus = 500f;

            [JsonProperty("Permission-based mulitipliers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public SortedDictionary<string, float> PermissionMulitipliers = new SortedDictionary<string, float>
            {
                ["vip"] = 5f,
                ["donor"] = 2.5f
            };

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
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ReceivedForPlaying"] = "You've received $payout.amount for actively playing",
                ["ReceivedForTimeAlive"] = "You've received $payout.amount for staying alive for $time.alive",
                ["ReceivedWelcomeBonus"] = "You've received $payout.amount as a welcome bonus"
            }, this);
        }

        #endregion Localization

        #region Data Storage

        private StoredData storedData;

        private class StoredData
        {
            public Dictionary<string, PlayerInfo> Players = new Dictionary<string, PlayerInfo>();

            public StoredData()
            {
            }
        }

        private class PlayerInfo
        {
            public DateTime LastTimeAlive;
            public bool WelcomeBonus;

            public PlayerInfo()
            {
                LastTimeAlive = DateTime.Now;
                WelcomeBonus = true;
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        private void OnServerSave() => SaveData();

        #endregion Data Storage

        #region Initialization

        [PluginReference]
        private Plugin Economics;

        private readonly Dictionary<string, Timer> payTimers = new Dictionary<string, Timer>();

        private void Init()
        {
            foreach (KeyValuePair<string, float> perm in config.PermissionMulitipliers)
            {
                permission.RegisterPermission($"{Name.ToLower()}.{perm.Key}", this);
                Log($"Registered permission '{Name.ToLower()}.{perm.Key}'; multiplier {perm.Value}");
            }

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

            if (!config.TimeAliveBonus)
            {
                Unsubscribe(nameof(OnUserRespawn));
            }
        }

        private void InitializePlayer(IPlayer player)
        {
            if (!storedData.Players.ContainsKey(player.Id))
            {
                storedData.Players.Add(player.Id, new PlayerInfo());
            }
        }

        private void OnServerInitialized()
        {
            foreach (IPlayer player in players.Connected)
            {
                InitializePlayer(player);

                payTimers[player.Id] = timer.Every(config.PayoutInterval, () =>
                {
                    Payout(player, config.BasePayout, GetLang("ReceivedForPlaying", player.Id));
                });
            }
        }

        private void Unload()
        {
            foreach (IPlayer player in players.Connected)
            {
                payTimers[player.Id]?.Destroy();
            }
        }

        #endregion Initialization

        #region Payout Handling

        private void Payout(IPlayer player, double amount, string message)
        {
            foreach (KeyValuePair<string, float> perm in config.PermissionMulitipliers)
            {
                if (player.HasPermission($"{Name.ToLower()}.{perm.Key}"))
                {
                    amount *= perm.Value;
                }
            }

            if (Economics != null && Economics.IsLoaded)
            {
                Economics.Call("Deposit", player.Id, amount);
                Message(player, message.Replace("$payout.amount", amount.ToString()));
            }
        }

        private void OnUserConnected(IPlayer player)
        {
            InitializePlayer(player);

            payTimers[player.Id] = timer.Every(config.PayoutInterval, () =>
            {
                Payout(player, config.BasePayout, GetLang("ReceivedForPlaying", player.Id));
            });

            if (config.WelcomeBonus > 0f && !storedData.Players[player.Id].WelcomeBonus)
            {
                Payout(player, config.WelcomeBonus, GetLang("ReceivedWelcomeBonus", player.Id));
            }
        }

        private void OnUserDisconnected(IPlayer player) => payTimers[player.Id]?.Destroy();

        private void OnUserRespawn(IPlayer player)
        {
            if (!storedData.Players.ContainsKey(player.Id))
            {
                InitializePlayer(player);
            }

            double secondsAlive = (storedData.Players[player.Id].LastTimeAlive - DateTime.Now).TotalSeconds;
            TimeSpan timeSpan = TimeSpan.FromSeconds(secondsAlive);

            double amount = (secondsAlive / config.BasePayout) * config.TimeAliveMultiplier;
            string timeAlive = $"{timeSpan.TotalHours:00}h {timeSpan.Minutes:00}m {timeSpan.Seconds:00}s".TrimStart(' ', 'd', 'h', 'm', 's', '0');

            Payout(player, amount, GetLang("ReceivedForTimeAlive", player.Id).Replace("$time.alive", timeAlive));
        }

        #endregion Payout Handling

        #region Helpers

        private string GetLang(string langKey, string playerId = null)
        {
            return lang.GetMessage(langKey, this, playerId);
        }

        private void Message(IPlayer player, string textOrLang, params object[] args)
        {
            if (player.IsConnected)
            {
                string message = GetLang(textOrLang, player.Id);
                player.Reply(message != textOrLang ? message : textOrLang);
            }
        }

        #endregion Helpers
    }
}
