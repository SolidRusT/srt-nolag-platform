using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;


namespace Oxide.Plugins
{
    [Info("Scraponomics Lite", "haggbart", "0.4.5")]
    [Description("Adds ATM UI with simple, intuitive functionality to vending machines and bandit vendors")]
    internal class ScraponomicsLite : RustPlugin
    {
        #region localization
        
        private const string LOC_PAID_BROKERAGE = "PaidBrokerage";
        private const string LOC_DEPOSIT = "Deposit";
        private const string LOC_WITHDRAW = "Withdraw";
        private const string LOC_AMOUNT = "Amount";
        private const string LOC_BALANCE = "Balance";
        private const string LOC_ATM = "ATM";
        private const string LOC_REWARD_INTEREST = "RewardInterst";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LOC_PAID_BROKERAGE] = "Paid the brokerage fee of {0} scrap.",
                [LOC_DEPOSIT] = "Deposit",
                [LOC_WITHDRAW] = "Withdraw",
                [LOC_BALANCE] = "Balance: {0} scrap",
                [LOC_AMOUNT] = "amount",
                [LOC_ATM] = "ATM",
                [LOC_REWARD_INTEREST] = "You've earned {0} scrap in interest."
            }, this);
        }

        #endregion localization
        

        #region data
        private void SaveData() =>
            Interface.Oxide.DataFileSystem.WriteObject(Name, playerData);

        private void ReadData() =>
            playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>(Name);

        private static Dictionary<ulong, PlayerData> playerData;
        private static readonly Dictionary<ulong, PlayerPreference> playerPrefs = new Dictionary<ulong, PlayerPreference>();

        private class PlayerData
        {
            public int scrap { get; set; }
            public DateTime lastInterest = DateTime.UtcNow;
        }
        
        private class PlayerPreference
        { 
            public int amount { get; set; }
        }
        
        #endregion data

        #region config
        
        private PluginConfig config;

        private class PluginConfig
        {
            public float feesFraction;
            public int startingBalance;
            public bool allowPlayerVendingMachines;
            public bool resetOnMapWipe;
            public float interestRate;
        }
        
        protected override void LoadDefaultConfig() => Config.WriteObject(GetDefaultConfig(), true);
        private new void SaveConfig() => Config.WriteObject(config, true);
        
        private static PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                feesFraction = 0.05f,
                startingBalance = 50,
                allowPlayerVendingMachines = false,
                resetOnMapWipe = true,
                interestRate = 0.10f
            };
        }
        
        
        #endregion config
        
        #region init

        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();

            if (!config.resetOnMapWipe)
            {
                Unsubscribe(nameof(OnNewSave));
            }

            SaveConfig();
            ReadData();
        }

        private void InitPlayerData(BasePlayer player)
        {
            var playerbalances = new PlayerData
            {
                scrap = config.startingBalance
            };
            playerData.Add(player.userID, playerbalances);
        }
        
        private static void InitPlayerPerference(BasePlayer player)
        {
            var playerPreference = new PlayerPreference
            {
                amount = 100
            };
            playerPrefs.Add(player.userID, playerPreference);
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyGuiAll(player);
            }
            
            SaveData();
        }
        
        #endregion init
        
        
        #region methods
        
        private void DoInterest(BasePlayer player)
        {
            PlayerData data = playerData[player.userID];

            if (data.scrap < 1) return;
            
            TimeSpan timeSinceLastInterest = DateTime.UtcNow - data.lastInterest;
            if (timeSinceLastInterest.Days == 0)
            {
                return;
            }
            
            int interest = (int) (data.scrap * Math.Pow(config.interestRate + 1.0f, 
                timeSinceLastInterest.TotalSeconds / 86400.0)) - data.scrap;
            
            if (interest < 1) return;
            data.scrap += interest;
            data.lastInterest = DateTime.UtcNow;

            SendReply(player, lang.GetMessage(LOC_REWARD_INTEREST, this, player.UserIDString), interest);
        }
        
        #endregion methods

        #region hooks
        
        private void OnServerSave()
        {
            SaveData();
        }

        private void OnNewSave(string filename)
        {
            playerData = new Dictionary<ulong, PlayerData>();
            SaveData();
        }

        private void OnOpenVendingShop(VendingMachine machine, BasePlayer player)
        {
            if (!(machine is NPCVendingMachine) && !config.allowPlayerVendingMachines) return;
            
            if (!playerData.ContainsKey(player.userID))
            {
                InitPlayerData(player);
            }
            
            DoInterest(player);

            NextTick(() => CreateUi(player)); 
        }
        
        private void OnLootEntityEnd(BasePlayer player, VendingMachine machine)
        {
            DestroyGuiAll(player);
        }

        private static void DestroyGuiAll(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, CUI_BANK_NAME);
        }
        
        #endregion hooks

        #region bank CUI
        
        private const int CUI_MAIN_FONTSIZE = 10;
        private const string CUI_MAIN_FONT_COLOR = "0.7 0.7 0.7 1.0";
        private const string CUI_GREEN_BUTTON_COLOR = "0.415 0.5 0.258 0.4";
        private const string CUI_GREEN_BUTTON_FONT_COLOR = "0.607 0.705 0.431";
        private const string CUI_GRAY_BUTTON_COLOR = "0.75 0.75 0.75 0.3";
        private const string CUI_BUTTON_FONT_COLOR = "0.77 0.68 0.68 1";
        private const string CUI_BANK_NAME = "BankUI";
        private const string CUI_BANK_HEADER_NAME = "header";
        private const string CUI_BANK_CONTENT_NAME = "content";
        
        private const string ANCHOR_MIN = "0.5 0.0";
        private const string ANCHOR_MAX = "0.67 0.0";
        private const string OFFSET_MIN = "193 16";
        private const string OFFSET_MAX = "200 97";

        private void CreateUi(BasePlayer player) 
        {
            if (!player.inventory.loot.IsLooting()) return;
            
            
            if (!playerPrefs.ContainsKey(player.userID))
            {
                InitPlayerPerference(player);
            }

            int amount = playerPrefs[player.userID].amount;
            

            double nextDecrement = amount / 1.5;
            double nextIncrement = amount * 1.5;
            
            CuiHelper.DestroyUi(player, CUI_BANK_NAME);
            
            var bankCui = new CuiElementContainer
            {
                {
                    new CuiPanel // main panel
                    {
                        Image = new CuiImageComponent {Color = "0 0 0 0"},
                        RectTransform =
                        {
                            AnchorMin = ANCHOR_MIN, AnchorMax = ANCHOR_MAX,
                            OffsetMin = OFFSET_MIN, OffsetMax = OFFSET_MAX
                        }
                    },
                    "Hud.Menu", CUI_BANK_NAME
                },
                {
                    new CuiPanel // header
                    {
                        Image = new CuiImageComponent {Color = "0.75 0.75 0.75 0.35"},
                        RectTransform = {AnchorMin = "0 0.775", AnchorMax = "1 1"}
                    },
                    CUI_BANK_NAME, CUI_BANK_HEADER_NAME
                },
                {
                    new CuiLabel // header label
                    {
                        RectTransform = {AnchorMin = "0.051 0", AnchorMax = "1 0.95"},
                        Text = {Text = lang.GetMessage(
                            LOC_ATM, this, player.UserIDString), 
                            Align = TextAnchor.MiddleLeft, Color = "0.77 0.7 0.7 1", FontSize = 13}
                    },
                    CUI_BANK_HEADER_NAME
                },
                {
                    new CuiPanel // content panel
                    {
                        Image = new CuiImageComponent {Color = "0.65 0.65 0.65 0.25"},
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0.74"}
                    },
                    CUI_BANK_NAME, CUI_BANK_CONTENT_NAME
                },
                {
                    new CuiLabel // balance label
                    {
                        RectTransform = {AnchorMin = "0.02 0.7", AnchorMax = "0.98 1"},
                        Text =
                        {
                            Text = string.Format(lang.GetMessage(LOC_BALANCE, this, 
                                player.UserIDString), playerData[player.userID].scrap),
                            Align = TextAnchor.MiddleLeft,
                            Color = CUI_MAIN_FONT_COLOR,
                            FontSize = CUI_MAIN_FONTSIZE
                        }
                    },
                    CUI_BANK_CONTENT_NAME
                },
                {
                    new CuiButton // deposit button
                    {
                        RectTransform = {AnchorMin = "0.02 0.4", AnchorMax = "0.25 0.7"},
                        Button = {Command = "sc.deposit " + amount, Color = CUI_GREEN_BUTTON_COLOR},
                        Text =
                        {
                            Align = TextAnchor.MiddleCenter,
                            Text = lang.GetMessage(LOC_DEPOSIT, this, player.UserIDString),
                            Color = CUI_GREEN_BUTTON_FONT_COLOR,
                            FontSize = 11
                        }
                    },
                    CUI_BANK_CONTENT_NAME
                },
                {
                    new CuiButton // withdraw button
                    {
                        RectTransform = {AnchorMin = "0.27 0.4", AnchorMax = "0.52 0.7"},
                        Button = {Command = "sc.withdraw " + amount, Color = CUI_GRAY_BUTTON_COLOR},
                        Text = {Align = TextAnchor.MiddleCenter, Text = lang.GetMessage(
                            LOC_WITHDRAW, this, player.UserIDString), Color = CUI_MAIN_FONT_COLOR, FontSize = 11}
                    },
                    CUI_BANK_CONTENT_NAME
                },
                {
                    new CuiButton // decrement button
                    {
                        RectTransform = {AnchorMin = "0.02 0.05", AnchorMax = "0.07 0.35"},
                        Button = {Command = "sc.setamount " + nextDecrement, Color = CUI_GRAY_BUTTON_COLOR},
                        Text =
                        {
                            Align = TextAnchor.MiddleCenter,
                            Text = "<",
                            Color = CUI_BUTTON_FONT_COLOR,
                            FontSize = CUI_MAIN_FONTSIZE
                        }
                    },
                    CUI_BANK_CONTENT_NAME
                },
                {
                    new CuiLabel // amount label
                    {
                        RectTransform = {AnchorMin = "0.08 0.05", AnchorMax = "0.19 0.35"},
                        Text =
                        {
                            Align = TextAnchor.MiddleCenter,
                            Text = amount.ToString(),
                            Color = CUI_MAIN_FONT_COLOR,
                            FontSize = CUI_MAIN_FONTSIZE
                        }
                    },
                    CUI_BANK_CONTENT_NAME
                },
                {
                    new CuiButton // increment button
                    {
                        RectTransform = {AnchorMin = "0.19 0.05", AnchorMax = "0.25 0.35"},
                        Button = {Command = "sc.setamount " + nextIncrement, Color = CUI_GRAY_BUTTON_COLOR},
                        Text =
                        {
                            Align = TextAnchor.MiddleCenter,
                            Text = ">",
                            Color = CUI_BUTTON_FONT_COLOR,
                            FontSize = CUI_MAIN_FONTSIZE
                        }
                    },
                    CUI_BANK_CONTENT_NAME
                },
                {
                    new CuiLabel // amount text label
                    {
                        RectTransform = {AnchorMin = "0.27 0.05", AnchorMax = "1 0.35"},
                        Text =
                        {
                            Align = TextAnchor.MiddleLeft,
                            Text = lang.GetMessage(LOC_AMOUNT, this, player.UserIDString),
                            Color = CUI_MAIN_FONT_COLOR,
                            FontSize = CUI_MAIN_FONTSIZE
                        }
                    },
                    CUI_BANK_CONTENT_NAME
                }
            };

            CuiHelper.AddUi(player, bankCui);
        }

        [ConsoleCommand("sc.setamount")]
        private void CmdSetAmount(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || arg.Args.Length != 1 || 
                !(player.inventory.loot.entitySource is VendingMachine)) return;
            
            double amount;
            if (!double.TryParse(arg.Args[0], out amount)) return;
            
            amount = Math.Round(amount / 10) * 10;
            
            if (amount < 10) amount = 10;
            else if (amount > 1000) amount = 1000;

            if (arg.Args.Length != 1) return;
            
            if (!playerPrefs.ContainsKey(player.userID))
            {
                InitPlayerPerference(player);
            }
            playerPrefs[player.userID].amount = (short) amount;
            CreateUi(player);
        }

        [ConsoleCommand("sc.deposit")]
        private void CmdDeposit(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || arg.Args.Length != 1 || 
                !(player.inventory.loot.entitySource is VendingMachine)) return;
            
            int amount;
            if (!int.TryParse(arg.Args[0], out amount)) return;
            
            if (player.inventory.GetAmount(-932201673) < amount)
            {
                amount = player.inventory.GetAmount(-932201673);
            }
            if (amount == 0) return;
            
            if (!playerData.ContainsKey(player.userID))
            {
                InitPlayerData(player);
            }
            playerData[player.userID].scrap += amount;
            player.inventory.Take(null, -932201673, amount);
            CreateUi(player);
        }

        [ConsoleCommand("sc.withdraw")]
        private void CmdWithdraw(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || arg.Args.Length != 1 || 
                !(player.inventory.loot.entitySource is VendingMachine)) return;

            int amount;
            if (!int.TryParse(arg.Args[0], out amount)) return;

            if (!playerData.ContainsKey(player.userID))
            {
                InitPlayerData(player);
            }
            int balance = playerData[player.userID].scrap;
            if (balance < amount) amount = balance;
            var tax = (int)Math.Round(amount * config.feesFraction);

            if (tax < 1) tax = 1;
            if (amount < 1) return;
            
            playerData[player.userID].scrap -= amount + tax;
            CreateUi(player);
            Item item = ItemManager.CreateByItemID(-932201673, amount);
            if (item == null) return;
            player.inventory.GiveItem(item);
            SendReply(player, string.Format(
                lang.GetMessage(LOC_PAID_BROKERAGE, this, player.UserIDString), tax));
        }
        
        #endregion bank CUI

        #region API

        private object SetBalance(ulong userId, int balance)
        {
            if (!playerData.ContainsKey(userId) && !TryInitPlayer(userId)) return null;

            playerData[userId].scrap = balance;
            return true;
        }

        private object GetBalance(ulong userId)
        {
            if (!playerData.ContainsKey(userId) && !TryInitPlayer(userId)) return null;
            return playerData[userId].scrap;
        }

        private bool TryInitPlayer(ulong userId)
        {
            BasePlayer player = BasePlayer.FindByID(userId);
            if (player == null) return false;
            InitPlayerData(player);
            return true;
        }
        
        #endregion API
    }
}
