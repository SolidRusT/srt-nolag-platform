﻿using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NetworkPlayer = uLink.NetworkPlayer;

namespace Oxide.Plugins {
    [Info("Economy Banks", "Pho3niX90", "1.5.7")]
    [Description("Banking, cash, interest, tax, money drop on death, and more")]
    public class EconomyBanks : HurtworldPlugin {
        #region [FIELDS]

        [PluginReference]
        private Plugin Economy, HWClans;

        private double vaultBalance;
        private string MoneySym;

        #endregion [FIELDS]

        #region [DEFAULT CONFIGS]

        private new void LoadDefaultMessages() {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"moneySymbol", "$"},
                {"chat_prefix", "<color=#ce422b>[EcoBanks]</color> "},
                {"msg_CashLost","You have lost {moneySymbol}{deceasedBalance} by dying with cash in your pocket"},
                {"msg_CashWon", "Killing {deceased.Name} earned you {moneySymbol}{deceasedBalance}"},
                {"msg_BankBalance", "Your bank balance is {moneySymbol}{balance}"},
                {"msg_ClanBalance", "Your clan balance is {moneySymbol}{balance}"},
                {"msg_CashBalance", "Your cash balance is {moneySymbol}{balance}"},
                {"msg_CashDeposited", "{moneySymbol}{amount} has been taken from your wallet and deposited in your bank account."},
                {"msg_CashDepositedClan", "{moneySymbol}{amount} has been taken from your wallet and deposited in your clan bank account."},
                {"msg_TransactionError", "You are trying to {transaction} more than you have!" },
                {"msg_Withdrawn", "{moneySymbol}{amount} has been withdrawn from your account and added to your wallet."},
                {"msg_WithdrawnClan", "{moneySymbol}{amount} has been withdrawn from your clan account and added to your wallet."},
                {"msg_InterestRate", "Interest rate is now {rate}%"},
                {"msg_InterestRateError", "It seems you didn't give a interval in digits I.E 6 for 6%" },
                {"msg_IntervalMinutes", "Users will receive their interest every {interval} minutes"},
                {"msg_IntervalMinutesError", "It seems you didn't give a interval in digits I.E 720 for 720 minutes"},
                {"msg_doDeathDrop", "Drop wallet cash on death set to {doDeathDrop}"},
                {"msg_doDeathDropError", "It seems you haven't set a boolean value, value  must be true or false"},
                {"msg_InterestReceived", "You have received {moneySymbol}{interest} intrerest on your bank balance."},
                {"msg_negativeAmount", "Please insert a postive amount!"},
                {"msg_transferSuccess","Transfer successful"},
                {"msg_transferReceived","{player} has transfered {amount} to your account"},
                {"msg_feeError", "Please enter a positve integer fee amount"},
                {"msg_syntaxErrorTransfer", "Syntax Error: Use /bank transfer <amount> <player>"},
                {"toast_moneyForPlaying", "Received {moneySymbol}{amount} for playing."},
                {"msg_needToBeNearStake", "You need to be near your stake to bank"},
                {"msg_NoTopPlayers", "There are no richest players"},
                {"msg_RichestPlayersList", "{rank}: {playername} has {moneySymbol}{money}"}
            }, this);
        }
        protected override void LoadDefaultConfig() {
            PrintWarning("Creating a configuration file for " + Title);
            Config.Clear();
            Config["deathdrop"] = "true";

            Config["interest_rate"] = "3";
            Config["interest_interval"] = "90";

            Config["playtime_interval"] = "10";
            Config["playtime_cash"] = "5";

            Config["fee_deposit"] = "3";
            Config["fee_withdrawal"] = "3";
            Config["fee_transfer"] = "3";

            Config["stakeBanking"] = "false";
            Config["clanBanking"] = "true";
            SaveConfig();
        }
        private void CheckConfig() {
            if (Config["deathdrop"] == null) {
                Config["deathdrop"] = "true";
            }

            if (Config["interest_rate"] == null) {
                Config["interest_rate"] = "3";
            }

            if (Config["interest_interval"] == null) {
                Config["interest_interval"] = "720";
            }

            if (Config["playtime_interval"] == null) {
                Config["playtime_interval"] = "10";
            }

            if (Config["playtime_cash"] == null) {
                Config["playtime_cash"] = "10";
            }

            if (Config["fee_deposit"] == null) {
                Config["fee_deposit"] = "3";
            }

            if (Config["fee_withdrawal"] == null) {
                Config["fee_withdrawal"] = "3";
            }

            if (Config["fee_transfer"] == null) {
                Config["fee_transfer"] = "3";
            }

            if (Config["stakeBanking"] == null) {
                Config["stakeBanking"] = "false";
            }

            if (Config["clanBanking"] == null) {
                Config["clanBanking"] = "true";
            }

            SaveConfig();
        }

        #endregion [DEFAULT CONFIGS]

        #region [LISTS]

        private Dictionary<string, Bank> Accounts = new Dictionary<string, Bank>();

        public class Bank {
            public double money;
            public double cash;
            public double moneyClan;

            internal Bank() {
            }

            internal Bank(double amount) {
                moneyClan = Round(amount);
                money = Round(amount);
                cash = Round(amount);
            }

            internal double CashBalance() {
                return Round(cash);
            }

            internal double BankBalance() {
                return Round(money);
            }

            internal double BankBalanceClan() {
                return Round(moneyClan);
            }

            internal void _Deposit(double amount, bool doCash = true, double Fee = 0) {
                money += Round(amount - Fee);
                if (doCash) {
                    cash -= Round(amount);
                }
            }

            internal void _DepositClan(double amount, bool doCash = true, double Fee = 0) {
                moneyClan += Round(amount - Fee);
            }

            internal void _AddCash(double amount) {
                cash += Round(amount);
            }

            internal void _RemoveCash(double amount) {
                cash -= Round(amount);
            }

            internal void _Withdraw(double amount, bool doCash = true, double Fee = 0) {
                money -= Round(amount + Fee);
                if (doCash) {
                    cash += Round(amount);
                }
            }

            internal void _WithdrawClan(double amount, bool doCash = true, double Fee = 0) {
                moneyClan -= Round(amount + Fee);
            }

            internal double Round(double value) {
                return Math.Round(value, 2);
            }
        }

        #endregion [LISTS]

        #region [HOOKS]

        private void Init() {
            if (!UseEconomy()) {
                Puts(Title + " is fully functional");
            } else {
                PrintWarning("Economy plugin was found, we will use their cash system. It's recommended to disable Economy Plugin and rather use the EconomyBank cash system. To disable Economy, delete the plugin Economy.cs and reload ");
            }

            LoadData();
            MoneySym = GetMsg("moneySymbol");
        }

        private void OnServerInitialized() {
            CheckConfig();
            LoadData();
            LoadVaultMoney();

            Dictionary<NetworkPlayer, PlayerSession>.ValueCollection sessions = GameManager.Instance.GetSessions().Values;

            foreach (PlayerSession player in sessions) {
                CheckForAccount(player, UseClanBanking());
            }

            if (Config["playtime_interval"] == null) {
                Config["playtime_interval"] = 1;
            }

            // This gives cash every 10(default) minutes
            int IntervalCash = int.Parse(Config["playtime_interval"].ToString()) * 60;
            double cash;

            timer.Repeat(IntervalCash, 0, () => {
                double.TryParse(Config["playtime_cash"].ToString(), out cash);
                foreach (PlayerSession ses in sessions) {
                    AddCash(ses, cash);
                    Toast(ses, GetMsg("toast_moneyForPlaying", ses)
                        .Replace("{amount}", cash.ToString()));
                }
            });

            // This applies tax every 30 (default) minutes
            int IntervalTax = int.Parse(Config["interest_interval"].ToString()) * 60;
            timer.Repeat(IntervalTax, 0, ApplyInterest);
        }

        private void OnPlayerConnected(PlayerSession player) {
            CheckForAccount(player, UseClanBanking());
        }

        private void OnPlayerDeath(PlayerSession player, EntityEffectSourceData source) {
            if (bool.Parse(Config["deathdrop"].ToString())) {
                string tmpName = GetNameOfObject(source.EntitySource);
                if (tmpName.Length < 3) {
                    return;
                }

                string murdererName = tmpName.Remove(tmpName.Length - 3);

                bool isPlayer = GetSession(murdererName) != null;
                if (source.EntitySource.name == null || !isPlayer) {
                    return;
                }

                PlayerSession murderer = GetSession(murdererName);
                PlayerSession deceased = player;
                double deceasedBalance = Wallet(player);

                // Substract all cash from deceased
                RemoveCash(deceased, deceasedBalance);
                hurt.SendChatMessage(deceased, Color(GetMsg("msg_CashLost", player), "bad").Replace("{deceasedBalance}", deceasedBalance.ToString()));
                //PrintWarning(deceased + " have lost $" + deceasedBalance + " by dying with cash in their pocket");

                // Give all cash to murderer
                AddCash(murderer, deceasedBalance);
                hurt.SendChatMessage(murderer, Color(GetMsg("msg_CashWon", player), "good").Replace("{deceased.Name}", deceased.Identity.Name).Replace("{deceasedBalance}", deceasedBalance.ToString()));
                SaveData();
            }
        }

        #endregion [HOOKS]

        #region [CHAT COMMANDS]

        [ChatCommand("wipe")]
        private void Chat_Wipe(PlayerSession player, string command, string[] args) {
            if (!player.IsAdmin) {
                return;
            }

            if (args.Length < 1) {
                hurt.SendChatMessage(player, null, "Use /wipe all [backup] - if backup is added, it will backup the users bankaccounts before clearing.");
                return;
            }

            switch (args[0].ToLower()) {
                case "all":
                    if (args.Length > 1) {
                        if (args[1].Equals("backup", StringComparison.CurrentCultureIgnoreCase)) {
                            BackupData();
                        }
                    }
                    Accounts.Clear();
                    break;

                default:
                    hurt.SendChatMessage(player, null, "Use /wipe all [backup] - if backup is addedm it will backup the users bankaccounts before clearing.");
                    break;
            }

            SaveData();
        }

        [ChatCommand("vault")]
        private void Chat_Vault(PlayerSession player, string command, string[] args) {
            if (!player.IsAdmin) {
                return;
            }

            hurt.SendChatMessage(player, null, "Vault balance: " + MoneySym + vaultBalance);
        }

        [ChatCommand("balance")]
        private void ChatCmd_Balance(PlayerSession player, string command, string[] args) {
            hurt.SendChatMessage(player, null, GetMsg("msg_BankBalance", player).Replace("{balance}", Balance(player).ToString()));
            if (UseClanBanking() && GetClanId(player) != 0) {
                hurt.SendChatMessage(player, null, GetMsg("msg_ClanBalance", player).Replace("{balance}", BalanceClan(player).ToString()));
            }
            hurt.SendChatMessage(player, null, GetMsg("msg_CashBalance", player).Replace("{balance}", Wallet(player).ToString()));
        }

        [ChatCommand("bank")]
        private void ChatCmd_Money(PlayerSession player, string command, string[] args) {
            switch (args.Length) {
                case 0:

                    hurt.SendChatMessage(player, null, Color("Available Commands:", "header"));
                    hurt.SendChatMessage(player, null, Color("/bank balance", "highlight") + " shows cash and bank balance");
                    hurt.SendChatMessage(player, null, Color("/bank <withdraw/deposit> <amount>", "highlight") + " withdraw/deposit cash from your account");
                    if (UseClanBanking()) {
                        hurt.SendChatMessage(player, null, Color("/bank <withdraw/deposit> <amount> clan", "highlight") + " withdraw/deposit cash from your clan account");
                    }

                    hurt.SendChatMessage(player, null, Color("/bank transfer <amount> <toplayer>", "highlight") + " transfer money to player");
                    if (player.IsAdmin) {
                        hurt.SendChatMessage(player, null, Color("/bank setup interest rate <interest>", "highlight") + " Where < interest > is the digit of %, current is 6");
                        hurt.SendChatMessage(player, null, Color("/bank setup interest interval <interval>", "highlight") + " Where < interval > is the digit of minutes, current is every 30");
                        hurt.SendChatMessage(player, null, Color("/bank setup deathdrop <true/false>", "highlight") + " If set to true(default), the users cash will drop at death and be rewarded to the murderer.");
                    }
                    break;

                default:
                    double amount = 0;
                    string action = args[0].ToLower();
                    if (action != "balance" && action != "setup" && action != "top") {
                        if (args.Length < 2) {
                            hurt.SendChatMessage(player, null, "Syntax Error. Check /bank");
                            return;
                        }
                        double.TryParse(args[1], out amount);

                        if (!IsPostive(amount)) {
                            hurt.SendChatMessage(player, null, Color(GetMsg("msg_negativeAmount", player), "bad"));
                            return;
                        }
                    }
                    switch (action) {
                        case "top":
                            string actionTop = args[1];
                            switch (actionTop) {
                                case "players":
                                    List<KeyValuePair<string, Bank>> AccountsList = Accounts.ToList();
                                    IEnumerable<KeyValuePair<string, Bank>> RichestClans = AccountsList.Where(a => a.Key.StartsWith("1"));
                                    IEnumerable<KeyValuePair<string, Bank>> RichestUsers = AccountsList.Where(a => a.Key.StartsWith("7"));

                                    IEnumerable<KeyValuePair<string, Bank>> RichestClanTop5 = RichestClans.OrderByDescending(a => a.Value.money).Take(5);

                                    IEnumerable<KeyValuePair<string, Bank>> RichestUsersTop5 = RichestUsers.OrderByDescending(a => a.Value.money).Take(5);

                                    int i = 1;
                                    IEnumerable<KeyValuePair<string, Bank>> richestUsersTop5 = RichestUsersTop5.ToList();

                                    if (!richestUsersTop5.Any()) {
                                        hurt.SendChatMessage(player, null, GetMsg("msg_NoTopPlayers", player));
                                        return;
                                    }

                                    hurt.SendChatMessage(player, null, Color("Richest Players", "header"));

                                    foreach (KeyValuePair<string, Bank> richest in richestUsersTop5) {
                                        hurt.SendChatMessage(player, null, GetMsg("msg_RichestPlayersList", player)
                                                         .Replace("{rank}", i.ToString())
                                                         .Replace("{playername}", covalence.Players.FindPlayer(richest.Key).Name)
                                                         .Replace("{money}", richest.Value.money.ToString()));

                                        i++;
                                    }
                                    break;
                            }
                            break;

                        case "deposit":

                            if (amount <= Wallet(player)) {
                                if (UseClanBanking() && args.Length >= 3) {
                                    if (args[2].Equals("clan", StringComparison.CurrentCultureIgnoreCase)) {
                                        if (DepositClan(player, amount)) {
                                            SaveData();
                                            hurt.SendChatMessage(player, null, GetMsg("msg_CashDepositedClan", player).Replace("{amount}", amount.ToString()));
                                            return;
                                        }
                                    }
                                }

                                if (Deposit(player, amount)) {
                                    SaveData();
                                    hurt.SendChatMessage(player, null, GetMsg("msg_CashDeposited", player).Replace("{amount}", amount.ToString()));
                                }
                            } else {
                                hurt.SendChatMessage(player, null, GetMsg("msg_TransactionError", player).Replace("{transaction}", "deposit"));
                            }

                            break;

                        case "balance":
                            hurt.SendChatMessage(player, null, GetMsg("msg_BankBalance", player).Replace("{balance}", Balance(player).ToString()));
                            if (UseClanBanking() && GetClanId(player) != 0) {
                                hurt.SendChatMessage(player, null, GetMsg("msg_ClanBalance", player).Replace("{balance}", BalanceClan(player).ToString()));
                            }
                            hurt.SendChatMessage(player, null, GetMsg("msg_CashBalance", player).Replace("{balance}", Wallet(player).ToString()));
                            break;

                        case "withdraw":

                            if (UseClanBanking() && args.Length >= 3) {
                                hurt.SendChatMessage(player, null, BalanceClan(player) + " " + amount + " " + (amount <= BalanceClan(player)));
                                if (amount <= BalanceClan(player) && args[2].Equals("clan", StringComparison.CurrentCultureIgnoreCase) && IsPostive(BalanceClan(player))) {
                                    if (WithdrawClan(player, amount)) {
                                        SaveData();
                                        hurt.SendChatMessage(player, null, GetMsg("msg_WithdrawnClan", player).Replace("{amount}", amount.ToString()));
                                        return;
                                    }
                                } else if (args[2].Equals("clan", StringComparison.CurrentCultureIgnoreCase) && (!IsPostive(BalanceClan(player)) || amount > BalanceClan(player))) {
                                    if (amount > BalanceClan(player)) {
                                        hurt.SendChatMessage(player, null, GetMsg("msg_TransactionError", player).Replace("{transaction}", "withdraw"));
                                    } else {
                                        hurt.SendChatMessage(player, null, Color(GetMsg("msg_negativeAmount", player), "bad"));
                                    }
                                    return;
                                }
                            }

                            if (amount <= Balance(player)) {
                                if (Withdraw(player, amount)) {
                                    SaveData();
                                    hurt.SendChatMessage(player, null, GetMsg("msg_Withdrawn", player).Replace("{amount}", amount.ToString()));
                                }
                            } else {
                                hurt.SendChatMessage(player, null, GetMsg("msg_TransactionError", player).Replace("{transaction}", "withdraw"));
                            }

                            break;

                        case "transfer":
                            if (args.Length < 3) {
                                hurt.SendChatMessage(player, null, GetMsg("msg_syntaxErrorTransfer", player));
                                return;
                            }

                            if (GetSession(args[2]) == null) {
                                hurt.SendChatMessage(player, null, Color("Cannot find the specified user. Is he online?", "bad"));
                                return;
                            }

                            PlayerSession toPlayerSession = GetSession(args[2]);
                            if (amount <= Balance(player)) {
                                if (Transfer(player, toPlayerSession, amount)) {
                                    hurt.SendChatMessage(player, null, Color(GetMsg("msg_transferSuccess", player), "good"));
                                    hurt.SendChatMessage(toPlayerSession, null, Color(GetMsg("msg_transferReceived", toPlayerSession), "good")
                                        .Replace("{player}", player.Identity.Name)
                                        .Replace("{amount}", amount.ToString()));
                                }
                            }

                            break;

                        case "setup":
                            if (!player.IsAdmin) {
                                return;
                            }

                            string action_admin = args[1];
                            switch (action_admin) {
                                case "interest":
                                    if (args[2] == "rate") {
                                        int Rate;
                                        if (int.TryParse(args[3], out Rate)) {
                                            Config["interest_rate"] = Rate;
                                            hurt.SendChatMessage(player, null, GetMsg("msg_InterestRate", player).Replace("{rate}", Rate.ToString()));
                                        } else {
                                            hurt.SendChatMessage(player, null, GetMsg("msg_InterestRateError", player));
                                        }
                                    } else if (args[2] == "interval") {
                                        int Interval;
                                        if (int.TryParse(args[3], out Interval)) {
                                            Config["interest_interval"] = Interval;
                                            hurt.SendChatMessage(player, null, GetMsg("msg_IntervalMinutes", Interval).Replace("{interval}", Interval.ToString()));
                                        } else {
                                            hurt.SendChatMessage(player, null, GetMsg("msg_IntervalMinutesError", player));
                                        }
                                    }
                                    SaveConfig();
                                    break;

                                case "deathdrop":
                                    bool doDeathDrop;
                                    if (bool.TryParse(args[2], out doDeathDrop)) {
                                        Config["deathdrop"] = doDeathDrop;
                                        SaveConfig();
                                        hurt.SendChatMessage(player, null, GetMsg("msg_doDeathDrop", player).Replace("{doDeathDrop}", doDeathDrop.ToString()));
                                    } else {
                                        hurt.SendChatMessage(player, null, GetMsg("msg_doDeathDropError", player));
                                    }
                                    break;

                                case "fee":
                                    int Fee;
                                    if (!int.TryParse(args[3], out Fee)) {
                                        hurt.SendChatMessage(player, null, GetMsg("msg_feeError", player));
                                    }
                                    if (args[2] == "withdrawal") {
                                        Config["fee_withdrawal"] = Fee;
                                    } else if (args[2] == "deposit") {
                                        Config["fee_deposit"] = Fee;
                                    } else if (args[2] == "transfer") {
                                        Config["fee_transfer"] = Fee;
                                    }
                                    SaveConfig();
                                    break;

                                case "playtime":
                                    int interval;
                                    int.TryParse(args[2], out interval);
                                    int cash;
                                    int.TryParse(args[3], out cash);
                                    Config["playtime_interval"] = interval;
                                    Config["playtime_cash"] = cash;
                                    SaveConfig();
                                    break;

                                case "stakebanking":
                                    bool decision;
                                    bool.TryParse(args[2], out decision);
                                    Config["stakeBanking"] = decision;
                                    hurt.SendChatMessage(player, null, "Stake banking has been set to " + decision);
                                    SaveConfig();
                                    break;

                                case "clanbanking":
                                    bool clandecision;
                                    bool.TryParse(args[2], out clandecision);
                                    Config["clanBanking"] = clandecision;
                                    hurt.SendChatMessage(player, null, "Clan banking has been set to " + clandecision);
                                    SaveConfig();
                                    break;
                            }
                            break;
                    }
                    break;
            }
        }

        #endregion [CHAT COMMANDS]

        #region [API HOOKS]

        private bool Deposit(PlayerSession player, double Amount, bool doCash = true) {
            if (!CanBank(player)) {
                return false;
            }

            double Fee = Amount * (int.Parse(Config["fee_deposit"].ToString()) / 100D);

            if (UseEconomy()) {
                GetBankAccount(player, false)._Deposit(Amount, false, Fee);
                Economy.Call("SubstractMoney", player, Amount);
            } else {
                GetBankAccount(player, false)._Deposit(Amount, true, Fee);
            }

            AddVaultMoney(Fee);
            SaveData();
            return true;
        }

        private bool DepositClan(PlayerSession player, double Amount, bool doCash = true) {
            if (!CanBank(player)) {
                return false;
            }

            double Fee = Amount * (int.Parse(Config["fee_deposit"].ToString()) / 100D);

            if (UseEconomy()) {
                GetBankAccount(player, true)._DepositClan(Amount, false, Fee);
                Economy.Call("SubstractMoney", player, Amount);
            } else {
                GetBankAccount(player, true)._DepositClan(Amount, false, Fee);
                RemoveCash(player, Amount);
            }

            AddVaultMoney(Fee);
            SaveData();
            return true;
        }

        private bool Withdraw(PlayerSession player, double Amount, bool doCash = true) {
            if (!CanBank(player)) {
                return false;
            }

            double Fee = Amount * (int.Parse(Config["fee_deposit"].ToString()) / 100D);

            if (UseEconomy()) {
                GetBankAccount(player, false)._Withdraw(Amount, false, Fee);
                Economy.Call("AddMoney", player, Amount);
            } else {
                GetBankAccount(player, false)._Withdraw(Amount, true, Fee);
            }

            AddVaultMoney(Fee);
            SaveData();
            return true;
        }

        private bool WithdrawClan(PlayerSession player, double Amount, bool doCash = true) {
            if (!CanBank(player)) { return false; }
            double Fee = Amount * (int.Parse(Config["fee_deposit"].ToString()) / 100D);
            if (UseEconomy()) {
                GetBankAccount(player, true)._WithdrawClan(Amount, false, Fee);
                Economy.Call("AddMoney", player, Amount);
            } else {
                GetBankAccount(player, true)._WithdrawClan(Amount, false, Fee);
                AddCash(player, Amount);
            }
            AddVaultMoney(Fee);
            SaveData();
            return true;
        }

        private bool Transfer(PlayerSession fromPlayer, PlayerSession toPlayer, double Amount) {
            if (!CanBank(fromPlayer)) { return false; }
            double Fee = Amount * (int.Parse(Config["fee_transfer"].ToString()) / 100D);
            GetBankAccount(fromPlayer, false)._Withdraw(Amount, false, Fee);
            GetBankAccount(toPlayer, false)._Deposit(Amount, false);
            SaveData();
            return true;
        }

        private void RemoveCash(PlayerSession player, double Amount) {
            if (UseEconomy()) {
                Economy.Call("SubstractMoney", player, Amount);
            } else {
                GetBankAccount(player, false)._RemoveCash(Amount);
            }
            SaveData();
        }

        private void AddCash(PlayerSession player, double Amount) {
            if (UseEconomy()) {
                Economy.Call("AddMoney", player, Amount);
                SaveData();
            } else {
                GetBankAccount(player, false)._AddCash(Amount);
                SaveData();
            }
        }

        private void AddVaultMoney(double amount) {
            vaultBalance += amount;
            Interface.Oxide.DataFileSystem.WriteObject("EconomyBanksVault", vaultBalance);
        }

        private void RemoveVaultMoney(double amount) {
            vaultBalance -= amount;
            Interface.Oxide.DataFileSystem.WriteObject("EconomyBanksVault", vaultBalance);
        }

        private void LoadVaultMoney() {
            vaultBalance = Interface.Oxide.DataFileSystem.ReadObject<double>("EconomyBanksVault");
        }

        private double Balance(PlayerSession player) {
            return GetBankAccount(player, false).BankBalance();
        }

        private double BalanceClan(PlayerSession player) {
            return GetBankAccount(player, true).BankBalanceClan();
        }

        private double Wallet(PlayerSession player) {
            return UseEconomy() ? Economy.Call<double>("GetBalance", player) : GetBankAccount(player, false).CashBalance();
        }

        private string accID;

        private Bank GetBankAccount(PlayerSession player, bool forClan) {
            accID = "";
            CheckForAccount(player, forClan);
            if (forClan && GetClanId(player) != 0) {
                accID = GetClanId(player).ToString();
            } else {
                accID = player.SteamId.ToString();
            }
            return Accounts[accID];
        }

        private void CheckForAccount(PlayerSession player, bool forClan) {
            ulong accID;
            if (forClan && GetClanId(player) != 0) {
                accID = GetClanId(player);
            } else {
                accID = (ulong)player.SteamId;
            }

            if (!Accounts.ContainsKey(accID.ToString())) {
                Accounts.Add(accID.ToString(), new Bank(0));
                SaveData();
            }
        }

        private bool CanBank(PlayerSession player) {
            if (!NearStake(player) && bool.Parse(Config["stakeBanking"].ToString())) {
                hurt.SendChatMessage(player, null, Color(GetMsg("msg_needToBeNearStake", player), "bad"));
                return false;
            }

            return true;
        }

        private bool NearStake(PlayerSession player) {
            bool near = false;
            EntityReferenceCache playerEntity = player.WorldPlayerEntity;
            const float radius = 10f;

            List<OwnershipStakeServer> entities = StakesInArea(playerEntity.transform.position, radius);

            if (entities.Count == 0) {
                return false;
            }

            foreach (OwnershipStakeServer entity in entities) {
                near = entity.AuthorizedPlayers.Contains(player.Identity);
                break;
            }

            return near;
        }

        private ulong GetClanId(PlayerSession player) {
            ulong tmpId = 100000000000000000;
            return tmpId + HWClans.Call<ulong>("getClanId", player);
        }

        private List<OwnershipStakeServer> StakesInArea(Vector3 pos, float radius) {
            List<OwnershipStakeServer> entities = new List<OwnershipStakeServer>();

            foreach (OwnershipStakeServer entity in Resources.FindObjectsOfTypeAll<OwnershipStakeServer>()) {
                if (Vector3.Distance(entity.transform.position, pos) <= radius) {
                    entities.Add(entity);
                }
            }

            return entities;
        }

        #endregion [API HOOKS]

        #region [SAVE AND LOADS]

        private void LoadData() {
            Accounts = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, Bank>>("EconomyBanks");
        }

        private void SaveData() {
            Interface.Oxide.DataFileSystem.WriteObject("EconomyBanks", Accounts);
        }

        private void BackupData() {
            Interface.Oxide.DataFileSystem.WriteObject("EconomyBanks_BACKUP" + DateTime.Now.ToString(".dd_MM_yyyy"), Accounts);
        }

        #endregion [SAVE AND LOADS]

        #region [HELPERS]

        private string GetNameOfObject(GameObject obj) {
            GameManager ManagerInstance = GameManager.Instance;
            return ManagerInstance.GetDescriptionKey(obj);
        }

        private void ApplyInterest() {
            double interestAmountMultiplier = (double)int.Parse(Config["interest_rate"].ToString()) / 100;

            int playerCount = 0;
            double interestTotal = 0;
            PrintWarning("Current interest rate is " + interestAmountMultiplier);
            foreach (KeyValuePair<string, Bank> Account in Accounts) {
                double interest = Math.Round(Account.Value.money * interestAmountMultiplier, 2);
                Account.Value._Deposit(interest, false);
                if (GetSession(Account.Key) != null) {
                    PlayerSession player = GetSession(Account.Key);

                    hurt.SendChatMessage(player, null, GetMsg("msg_InterestReceived", player)
                                                .Replace("{interest}", interest.ToString()));
                    playerCount++;
                    interestTotal += interest;
                } else {
                    playerCount++;
                    interestTotal += interest;
                }
            }
            PrintWarning("A total of " + playerCount + " received a total of " + GetMsg("moneySymbol") + Math.Round(interestTotal, 2) + " interest");

            SaveData();
        }

        private void Toast(PlayerSession player, string msg) {
            AlertManager.Instance.GenericTextNotificationServer(msg, player.Player);
        }

        private PlayerSession GetSession(string identifier) {
            Dictionary<NetworkPlayer, PlayerSession> sessions = GameManager.Instance.GetSessions();
            PlayerSession session = null;

            foreach (KeyValuePair<NetworkPlayer, PlayerSession> i in sessions) {
                if (identifier.Equals(i.Value.Identity.Name, StringComparison.OrdinalIgnoreCase) || identifier.Equals(i.Value.SteamId.ToString()) || identifier.Equals(i.Key.ipAddress)) {
                    session = i.Value;
                    break;
                }
            }

            return session;
        }

        private string GetMsg(string key, object userID = null) {
            return (key.Equals("moneySymbol") ? "" : lang.GetMessage("chat_prefix", this)) + (userID != null ? lang.GetMessage(key, this, userID.ToString()).Replace("{moneySymbol}", MoneySym) : lang.GetMessage(key, this).Replace("{moneySymbol}", MoneySym));
        }

        private string Color(string text, string color) {
            switch (color) {
                case "bad": return "<color=#ff0000ff>" + text + "</color>";
                case "good": return "<color=#00ff00ff>" + text + "</color>";
                case "header": return "<color=#00ffffff>" + text + "</color>";
                case "highlight": return "<color=#d9ff00>" + text + "</color>";
                default: return "<color=#" + color + ">" + text + "</color>";
            }
        }
        private bool UseClanBanking() {
            return HWClans != null && bool.Parse(Config["stakeBanking"].ToString());
        }

        private bool UseEconomy() {
            return Economy != null;
        }

        private bool IsPostive(double amount) => amount > 0;

        #endregion [HELPERS]
    }
}