using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Global Gamble", "Trey", "1.2.2")]
    [Description("Gives every player currently online to enter X amount of scrap in to a gambling pot at the chance of winning everything.")]
    public class GlobalGamble : RustPlugin
    {
        #region Fields

        const string depositPerm = "globalgamble.deposit";
        const string gambleStartPerm = "globalgamble.start";

        private bool gambleActive;
        Timer remindActiveGamble;
        Timer initGlobalGamble;
        int currentScrapAmount;

        #endregion

        #region Data

        WinnerData _winnerCache;
        public class WinnerData
        {
            public List<string> WinnerList = new List<string>();
        }

        StandingDeposits standingDeposits;
        public class StandingDeposits
        {
            public Hash<string, double> Deposits = new Hash<string, double>();
        }

        #endregion

        #region Configuration

        Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Global Gamble Options")]
            public PluginOptions POptions = new PluginOptions();
        }

        public class PluginOptions
        {
            [JsonProperty(PropertyName = "Chat Command")]
            public string Ggccmd = "globalgamble";

            [JsonProperty(PropertyName = "Frequency of GlobalGamble(minutes)")]
            public float globalGambleTimer = 60;

            [JsonProperty(PropertyName = "Duration of Deposit Window(minutes)")]
            public float durationOfGamble = 10;

            [JsonProperty(PropertyName = "Remind Players of Active Gamble Every X Seconds")]
            public float remindEvery = 120;

            [JsonProperty(PropertyName = "Minimum Online Players")]
            public int minPlayer = 10;

            [JsonProperty(PropertyName = "Minimum Scrap Deposit")]
            public int minDeposit = 100;

            [JsonProperty(PropertyName = "Commission Permissions", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Hash<string, double> commissionPerms = new Hash<string, double>
            {
                {"globalgamble.default", 0.30 },
                {"globalgamble.vip1com", 0.25 },
                {"globalgamble.vip2com", 0.20 },
                {"globalgamble.vip3com", 0.15 },
                {"globalgamble.vip4com", 0.10 },
                {"globalgamble.vip5com", 0.05 },
            };

            [JsonProperty(PropertyName = "Wipe Total Earnings List")]
            public bool wipeEarnings = true;

            [JsonProperty(PropertyName = "Chat Icon (Steam64ID)")]
            public ulong chatIcon = 0;
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
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminHelp"] = "<color=#C0C0C0><color=#66B2FF>/globalgamble deposit <amount></color> - This will deposit X amount of scrap in to the global pot.\n\n<color=#66B2FF>/globalgamble start</color> - This will begin a new global gamble.</color>",
                ["GambleAlreadyActive"] = "<color=#C0C0C0>There is already a gamble currently running, wait for it to end before starting another one.</color>!",
                ["GambleBegin"] = "<color=#C0C0C0>A new scrap gamble has started, type <color=#66B2FF>/globalgamble</color> to enter.</color>",
                ["GambleReminder"] = "<color=#C0C0C0>There is currently a scrap gamble occuring!, type <color=#66B2FF>/globalgamble</color> to read more info.</color>",
                ["IncorrectFormat"] = "<color=#C0C0C0>That wasn't a correct deposit statement. Example: <color=#66B2FF>/globalgamble deposit 150</color></color>",
                ["InsufficientFunds"] = "<color=#C0C0C0>You don't have <color=#66B2FF>{0} scrap</color> to deposit!</color>",
                ["MinimumDeposit"] = "<color=#C0C0C0>The minimum deposit to enter is <color=#66B2FF>{0} scrap</color>!",
                ["NewDeposit"] = "<color=#C0C0C0><color=#FF6666>{0}</color> has just deposited <color=#66B2FF>{1} scrap</color>, bringing the total pot to <color=#66B2FF>{2}</color>!</color>",
                ["NewWinner"] = "<color=#C0C0C0><color=#FF6666>{0}</color> is the winner of the scrap gamble, with a commission of <color=#66B2FF>{1}</color>, receiving a total of <color=#66B2FF>{2} scrap</color>!",
                ["NobodyPlayed"] = "<color=#C0C0C0>Nobody played in the gamble, so nobody won!</color>",
                ["NoPermission"] = "<color=#C0C0C0>You do not have permission to use this.</color>",
                ["NormalHelp"] = "<color=#C0C0C0><color=#66B2FF>/globalgamble deposit <amount></color> - This will deposit X amount of scrap in to the global pot.</color>",
                ["NotGambleTime"] = "<color=#C0C0C0>There are currently no scrap gambles occuring, try again later.</color>",
                ["Prefix"] = "<color=#FF6666>[Global Gamble]</color>",

            }, this);
        }

        #endregion

        #region Lottery Function

        public class GambleSystem
        {
            public class Ticket
            {
                public string Key;
                public double Weight;
                public Ticket(string key, double weight)
                {
                    Key = key;
                    Weight = weight;
                }
            }

            List<Ticket> tickets = new List<Ticket>();
            Hash<int, string> playerEntries = new Hash<int, string>();

            public void Add(string key, double weight)
            {
                tickets.Add(new Ticket(key, weight));
            }

            public string Draw()
            {
                int place = 1;
                foreach (Ticket playerEntry in tickets)
                {
                    for (var i = 0; i < playerEntry.Weight; i++)
                    {
                        playerEntries.Add(place++, playerEntry.Key);
                    }
                }

                int winnerPlace = UnityEngine.Random.Range(1, playerEntries.Count);
                string winnerId = playerEntries[winnerPlace];

                return winnerId;
            }
        }

        #endregion

        #region Core Methods

        private void OnServerInitialized()
        {
            permission.RegisterPermission(depositPerm, this);
            permission.RegisterPermission(gambleStartPerm, this);

            cmd.AddChatCommand(config.POptions.Ggccmd, this, "GlobalGambleChatCommand");

            foreach (KeyValuePair<string, double> commissionPerms in config.POptions.commissionPerms)
            {
                permission.RegisterPermission(commissionPerms.Key, this);
            }

            InitGambleTimer();

            gambleActive = false;
            currentScrapAmount = 0;

            standingDeposits = new StandingDeposits();
            _winnerCache = new WinnerData();
        }

        private void InitGambleTimer()
        {
            initGlobalGamble = timer.Repeat(config.POptions.globalGambleTimer * 60, 0, () => { BeginGamble(null); });
        }

        private void BeginGamble(BasePlayer player)
        {
            if (gambleActive)
            {
                if (player != null)
                {
                    PrintMsg(player, Lang("GambleAlreadyActive", player.UserIDString));
                    return;
                }
                return;
            }

            if (BasePlayer.activePlayerList.Count < config.POptions.minPlayer) return;

            foreach (BasePlayer players in BasePlayer.activePlayerList)
            {
                if (players == null) continue;

                PrintMsg(players, Lang("GambleBegin", players.UserIDString));
            }

            initGlobalGamble.Destroy();

            remindActiveGamble = timer.Repeat(config.POptions.remindEvery, 0, () => {

                foreach (BasePlayer players in BasePlayer.activePlayerList)
                {
                    if (players == null) continue;

                    PrintMsg(players, Lang("GambleReminder", players.UserIDString));
                }
            });

            timer.In(config.POptions.durationOfGamble * 60, () => { GetWinner(); });

            gambleActive = true;
        }

        private void ResetFields()
        {
            gambleActive = false;
            _winnerCache.WinnerList.Clear();
            standingDeposits.Deposits.Clear();
            currentScrapAmount = 0;
            remindActiveGamble.Destroy();
            InitGambleTimer();
        }

        private void GetWinner()
        {
            var lottery = new GambleSystem();

            if (standingDeposits.Deposits.Count == 0)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (player == null) continue;

                    PrintMsg(player, Lang("NobodyPlayed", player.UserIDString));
                    ResetFields();
                    return;
                }
            }

            foreach (KeyValuePair<string, double> playerEntries in standingDeposits.Deposits)
            {
                lottery.Add(playerEntries.Key, playerEntries.Value);
            }

            var winner = lottery.Draw();

            BasePlayer winnerPlayer = BasePlayer.FindAwakeOrSleeping(winner);

            AwardWinnerFunds(winnerPlayer, currentScrapAmount);
        }

        private void AwardWinnerFunds(BasePlayer winnerPlayer, double amt)
        {
            double commissionedAmt = amt * GetCommissionAmt(winnerPlayer.UserIDString);
            double finalAmt = amt - commissionedAmt;

            foreach (BasePlayer players in BasePlayer.activePlayerList)
            {
                if (players == null) continue;

                PrintMsg(players, Lang("NewWinner", players.UserIDString, winnerPlayer.displayName, GetCommissionAmt(winnerPlayer.UserIDString).ToString("P"), $"{finalAmt:###,###,###,###,###}"));
            }

            Item item = ItemManager.CreateByName("scrap", (int)finalAmt);

            item.MoveToContainer(winnerPlayer.inventory.containerMain);

            ResetFields();
        }

        private void AddDeposit(BasePlayer player, int amountDeposited)
        {
            standingDeposits.Deposits[player.UserIDString] += amountDeposited;

            foreach (BasePlayer players in BasePlayer.activePlayerList)
            {
                if (player == null) continue;

                PrintMsg(players, Lang("NewDeposit", players.UserIDString, player.displayName, $"{amountDeposited:###,###,###,###,###}", $"{GetCurrentDeposits():###,###,###,###,###}"));
            }

            currentScrapAmount += amountDeposited;
        }

        private double GetCurrentDeposits()
        {
            double currentDeposit = 0;

            foreach (KeyValuePair<string, double> deposits in standingDeposits.Deposits)
            {
                currentDeposit += deposits.Value;
            }

            return currentDeposit;
        }

        private double GetCommissionAmt(string playerID)
        {
            foreach (KeyValuePair<string, double> commisionamt in config.POptions.commissionPerms.OrderBy(cp => cp.Value))
            {
                if (permission.UserHasPermission(playerID, commisionamt.Key))
                {
                    return commisionamt.Value;
                }
            }

            return 0;
        }

        #endregion

        #region ChatCommands
        private void GlobalGambleChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, depositPerm))
            {
                PrintMsg(player, Lang("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                if (permission.UserHasPermission(player.UserIDString, gambleStartPerm))
                {
                    PrintMsg(player, Lang("AdminHelp", player.UserIDString));
                    return;
                }

                PrintMsg(player, Lang("NormalHelp", player.UserIDString));
            }

            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    default:
                        {
                            if (permission.UserHasPermission(player.UserIDString, gambleStartPerm))
                            {
                                PrintMsg(player, Lang("AdminHelp", player.UserIDString));
                                return;
                            }

                            PrintMsg(player, Lang("NormalHelp", player.UserIDString));
                            return;
                        }

                    case "deposit":
                        {
                            var item = ItemManager.FindItemDefinition("scrap");
                            int amount;
                            bool success = int.TryParse(args[1], out amount);

                            if (!gambleActive)
                            {
                                PrintMsg(player, Lang("NotGambleTime", player.UserIDString));
                                return;
                            }

                            if (success)
                            {
                                if (amount < config.POptions.minDeposit)
                                {
                                    PrintMsg(player, Lang("MinimumDeposit", player.UserIDString, $"{config.POptions.minDeposit:###,###,###,###,###}"));
                                    return;
                                }

                                if (player.inventory.GetAmount(item.itemid) >= amount)
                                {
                                    player.inventory.Take(null, item.itemid, amount);
                                    AddDeposit(player, amount);
                                    return;
                                }

                                PrintMsg(player, Lang("InsufficientFunds", player.UserIDString, $"{amount:###,###,###,###,###}"));
                                return;
                            }

                            else
                            {
                                PrintMsg(player, Lang("IncorrectFormat", player.UserIDString));
                                return;
                            }
                        }

                    case "start":
                        {
                            if (!permission.UserHasPermission(player.UserIDString, gambleStartPerm))
                            {
                                PrintMsg(player, Lang("NoPermission", player.UserIDString));
                                return;
                            }

                            BeginGamble(null);
                            return;
                        }
                }
            }
        }

        #endregion

        #region Helpers

        private void PrintMsg(BasePlayer player, string message) => Player.Message(player, $"{Lang("Prefix", player.UserIDString)}\n\n{message}", config.POptions.chatIcon);

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}

