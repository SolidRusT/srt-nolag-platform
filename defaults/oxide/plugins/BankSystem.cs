using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Bank System", "Mevent", "1.0.12")]
    public class BankSystem : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary, Notify, StackSizeController, StackModifier;

        private const string Layer = "UI.BankSystem";

        private static BankSystem _instance;

        private readonly Dictionary<BasePlayer, ATMData> _atmByPlayer = new Dictionary<BasePlayer, ATMData>();

        private readonly List<VendingMachine> _vendingMachines = new List<VendingMachine>();

        private const string VendingMachinePrefab =
            "assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab";

        private enum Transaction
        {
            Deposit,
            Withdrawal,
            Transfer
        }

        #endregion

        #region Config

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Work with Notify?")]
            public bool UseNotify = true;

            [JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Commands = { "bank" };

            [JsonProperty(PropertyName = "Permission (example: banksystem.use)")]
            public string Permission = string.Empty;

            [JsonProperty(PropertyName = "Starting balance")]
            public int StartingBalance = 100;

            [JsonProperty(PropertyName = "Card Image")]
            public string CardImage = "https://i.imgur.com/Br9z7Ou.png";

            [JsonProperty(PropertyName = "Transit Image")]
            public string DepositImage = "https://i.imgur.com/h2bqMu4.png";

            [JsonProperty(PropertyName = "Withdraw Image")]
            public string WithdrawImage = "https://i.imgur.com/lwVwxm3.png";

            [JsonProperty(PropertyName = "Transfer Image")]
            public string TransferImage = "https://i.imgur.com/TBIxUnz.png";

            [JsonProperty(PropertyName = "Exit Image")]
            public string ExitImage = "https://i.imgur.com/OGoMu9N.png";

            [JsonProperty(PropertyName = "Currency Settings")]
            public CurrencySettings Currency = new CurrencySettings
            {
                DisplayName = "RUSTNote",
                ShortName = "sticks",
                Skin = 2536195910
            };

            [JsonProperty(PropertyName = "Card expiry date (in days)")]
            public int CardExpiryDate = 7;

            [JsonProperty(PropertyName = "ATM Settings")]
            public ATMSettings Atm = new ATMSettings
            {
                MinDepositFee = 0,
                MaxDepositFee = 10,
                StepDepositFee = 0.1f,
                MinWithdrawalFee = 0,
                MaxWithdrawalFee = 10,
                StepWithdrawalFee = 0.1f,
                DisplayName = "ATM",
                Skin = 2551771822,
                Repair = new RepairSettings
                {
                    Items = new List<RepairItemConf>
                    {
                        new RepairItemConf
                        {
                            ShortName = "scrap",
                            Amount = 2,
                            Skin = 0,
                            Title = string.Empty
                        },
                        new RepairItemConf
                        {
                            ShortName = "metalpipe",
                            Amount = 1,
                            Skin = 0,
                            Title = string.Empty
                        },
                        new RepairItemConf
                        {
                            ShortName = "metal.fragments",
                            Amount = 15,
                            Skin = 0,
                            Title = string.Empty
                        }
                    }
                },
                DefaultDepositFee = 1,
                DefaultWithdrawalFee = 1,
                DefaultBreakPercent = 1,
                BreakPercent = new Dictionary<string, float>
                {
                    ["banksystem.vip"] = 0.7f,
                    ["banksystem.premium"] = 0.5f
                },
                Spawn = new SpawnSettings
                {
                    Monuments = new Dictionary<string, ATMPosition>
                    {
                        ["compound"] = new ATMPosition
                        {
                            Enabled = true,
                            DisplayName = "ATM",
                            Position = new Vector3(-3.5f, 1.15f, 2.7f),
                            Rotation = -90,
                            DepositFee = 0,
                            WithdrawFee = 0
                        },
                        ["bandit"] = new ATMPosition
                        {
                            Enabled = true,
                            DisplayName = "ATM",
                            Position = new Vector3(34.2f, 2.35f, -24.7f),
                            Rotation = 135,
                            DepositFee = 0,
                            WithdrawFee = 0
                        }
                    }
                },
                ShopName = "ATM #{id}"
            };

            [JsonProperty(PropertyName = "Tracking Settings")]
            public TrackingSettings Tracking = new TrackingSettings
            {
                CostTable = new Dictionary<string, float>
                {
                    ["sulfur.ore"] = 0.5f,
                    ["metal.ore"] = 0.5f,
                    ["hq.metal.ore"] = 0.5f,
                    ["stones"] = 0.5f,
                    ["crate_elite"] = 10f,
                    ["crate_normal"] = 7f,
                    ["crate_normal_2"] = 4
                }
            };

            [JsonProperty(PropertyName = "Wipe Settings")]
            public WipeSettings Wipe = new WipeSettings
            {
                Players = false,
                Logs = true,
                ATMs = true
            };

            [JsonProperty(PropertyName = "NPC Settings")]
            public NPCSettings NPC = new NPCSettings
            {
                NPCs = new List<string>
                {
                    "1234567",
                    "7654321",
                    "4644687478"
                }
            };

            [JsonProperty(PropertyName = "Economy Settings")]
            public EconomySettings Economy = new EconomySettings
            {
                Self = true,
                AddHook = "Deposit",
                BalanceHook = "Balance",
                RemoveHook = "Withdraw",
                PluginName = "Economics"
            };

            [JsonProperty(PropertyName = "Drop Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<DropInfo> Drop = new List<DropInfo>
            {
                new DropInfo
                {
                    Enabled = true,
                    PrefabName = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                    DropChance = 50,
                    MinAmount = 2,
                    MaxAmount = 5
                },
                new DropInfo
                {
                    Enabled = true,
                    PrefabName = "assets/bundled/prefabs/radtown/loot_barrel_2.prefab",
                    DropChance = 5,
                    MinAmount = 2,
                    MaxAmount = 5
                },
                new DropInfo
                {
                    Enabled = true,
                    PrefabName = "assets/bundled/prefabs/radtown/loot_barrel_1.prefab",
                    DropChance = 5,
                    MinAmount = 2,
                    MaxAmount = 5
                }
            };
        }

        public class DropInfo
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = "Prefab")]
            public string PrefabName;

            [JsonProperty(PropertyName = "Chance")]
            public int DropChance;

            [JsonProperty(PropertyName = "Min Amount")]
            public int MinAmount;

            [JsonProperty(PropertyName = "Max Amount")]
            public int MaxAmount;
        }

        private class EconomySettings
        {
            [JsonProperty(PropertyName = "Use own economic system?")]
            public bool Self;

            [JsonProperty(PropertyName = "Plugin name")]
            public string PluginName;

            [JsonProperty(PropertyName = "Balance add hook")]
            public string AddHook;

            [JsonProperty(PropertyName = "Balance remove hook")]
            public string RemoveHook;

            [JsonProperty(PropertyName = "Balance show hook")]
            public string BalanceHook;

            public double ShowBalance(BasePlayer player)
            {
                return ShowBalance(player.UserIDString);
            }

            public double ShowBalance(ulong player)
            {
                return ShowBalance(player.ToString());
            }

            private double ShowBalance(string player)
            {
                if (Self) return _instance.Balance(player);

                var plugin = _instance?.plugins?.Find(PluginName);
                if (plugin == null) return 0;

                return Math.Round(Convert.ToDouble(plugin.Call(BalanceHook, player)));
            }

            public void AddBalance(BasePlayer player, int amount)
            {
                AddBalance(player.UserIDString, amount);
            }

            private void AddBalance(ulong player, int amount)
            {
                AddBalance(player.ToString(), amount);
            }

            private void AddBalance(string player, int amount)
            {
                if (Self)
                {
                    _instance.Deposit(player, amount);
                    return;
                }

                var plugin = _instance?.plugins.Find(PluginName);
                if (plugin == null) return;

                switch (PluginName)
                {
                    case "Economics":
                        plugin.Call(AddHook, player, (double)amount);
                        break;
                    default:
                        plugin.Call(AddHook, player, amount);
                        break;
                }
            }

            public bool RemoveBalance(BasePlayer player, int amount)
            {
                return RemoveBalance(player.UserIDString, amount);
            }

            private bool RemoveBalance(ulong player, int amount)
            {
                return RemoveBalance(player.ToString(), amount);
            }

            private bool RemoveBalance(string player, int amount)
            {
                if (ShowBalance(player) < amount) return false;

                if (Self) return _instance.Withdraw(player, amount);

                var plugin = _instance?.plugins.Find(PluginName);
                if (plugin == null) return false;

                switch (PluginName)
                {
                    case "Economics":
                        plugin.Call(RemoveHook, player, (double)amount);
                        break;
                    default:
                        plugin.Call(RemoveHook, player, amount);
                        break;
                }

                return true;
            }

            public bool Transfer(ulong member, ulong target, int amount)
            {
                if (!RemoveBalance(member, amount)) return false;

                AddBalance(target, amount);
                return true;
            }
        }

        private class NPCSettings
        {
            [JsonProperty(PropertyName = "NPCs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> NPCs = new List<string>();
        }

        private class WipeSettings
        {
            [JsonProperty(PropertyName = "Wipe Players?")]
            public bool Players;

            [JsonProperty(PropertyName = "Wipe Logs?")]
            public bool Logs;

            [JsonProperty(PropertyName = "Wipe ATMs?")]
            public bool ATMs;
        }

        private class SpawnSettings
        {
            [JsonProperty(PropertyName = "Monuments", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, ATMPosition> Monuments;
        }

        private class ATMPosition
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = "Display Name")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Position")]
            public Vector3 Position;

            [JsonProperty(PropertyName = "Rotation")]
            public float Rotation;

            [JsonProperty(PropertyName = "Deposit Fee")]
            public float DepositFee;

            [JsonProperty(PropertyName = "Withdraw Fee")]
            public float WithdrawFee;
        }

        private class TrackingSettings
        {
            [JsonProperty(PropertyName = "Cost Table (shortname - cost)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> CostTable;
        }

        private class CurrencySettings
        {
            [JsonProperty(PropertyName = "Display Name")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Short Name")]
            public string ShortName;

            [JsonProperty(PropertyName = "Skin")] public ulong Skin;

            public Item ToItem(int amount = 1)
            {
                var item = ItemManager.CreateByName(ShortName, amount, Skin);
                if (item == null)
                {
                    Debug.LogError($"Error creating item with ShortName: '{ShortName}'");
                    return null;
                }

                if (!string.IsNullOrEmpty(DisplayName)) item.name = DisplayName;
                return item;
            }
        }

        private class ATMSettings
        {
            [JsonProperty(PropertyName = "Minimum deposit fee")]
            public float MinDepositFee;

            [JsonProperty(PropertyName = "Maximum deposit fee")]
            public float MaxDepositFee;

            [JsonProperty(PropertyName = "Default deposit fee")]
            public float DefaultDepositFee;

            [JsonProperty(PropertyName = "Step deposit fee")]
            public float StepDepositFee;

            [JsonProperty(PropertyName = "Minimum withdrawal fee")]
            public float MinWithdrawalFee;

            [JsonProperty(PropertyName = "Maximum withdrawal fee")]
            public float MaxWithdrawalFee;

            [JsonProperty(PropertyName = "Default withdrawal fee")]
            public float DefaultWithdrawalFee;

            [JsonProperty(PropertyName = "Step withdrawal fee")]
            public float StepWithdrawalFee;

            [JsonProperty(PropertyName = "Default breakage percentage during operation")]
            public float DefaultBreakPercent;

            [JsonProperty(PropertyName = "Breakage percentage during operation",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> BreakPercent;

            [JsonProperty(PropertyName = "Repair Settings")]
            public RepairSettings Repair;

            [JsonProperty(PropertyName = "Display Name")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Skin")] public ulong Skin;

            [JsonProperty(PropertyName = "Spawn Settings")]
            public SpawnSettings Spawn;

            [JsonProperty(PropertyName = "Shop Name ({id} {owner})")]
            public string ShopName;

            public Item ToItem()
            {
                var item = ItemManager.CreateByName("vending.machine", 1, Skin);
                if (item == null)
                    return null;

                if (!string.IsNullOrEmpty(DisplayName)) item.name = DisplayName;

                return item;
            }
        }

        private class RepairSettings
        {
            [JsonProperty(PropertyName = "Items (for 1%)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<RepairItemConf> Items = new List<RepairItemConf>();
        }

        private class RepairItemConf
        {
            [JsonProperty(PropertyName = "Short Name")]
            public string ShortName;

            [JsonProperty(PropertyName = "Amount (for 1%)")]
            public float Amount;

            [JsonProperty(PropertyName = "Skin")] public ulong Skin;

            [JsonProperty(PropertyName = "Title (empty - default)")]
            public string Title;

            [JsonIgnore] private string _publicTitle;

            [JsonIgnore]
            public string PublicTitle
            {
                get
                {
                    if (string.IsNullOrEmpty(_publicTitle))
                    {
                        if (string.IsNullOrEmpty(Title))
                            _publicTitle = ItemManager.FindItemDefinition(ShortName)?.displayName.translated ??
                                           "UNKNOWN";
                        else
                            _publicTitle = Title;
                    }

                    return _publicTitle;
                }
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        #endregion

        #region Data

        private PluginData _data;
        private LogsData _logs;
        private ATMsData _atms;

        #region Save

        private void SaveData()
        {
            SavePlayers();

            SaveLogs();

            SaveATMs();
        }

        private void SavePlayers()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Players", _data);
        }

        private void SaveLogs()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Logs", _logs);
        }

        private void SaveATMs()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/ATMs", _atms);
        }

        #endregion

        #region Load

        private void LoadData()
        {
            LoadPlayers();

            LoadLogs();

            LoadATMs();
        }

        private void LoadPlayers()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>($"{Name}/Players");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }

        private void LoadLogs()
        {
            try
            {
                _logs = Interface.Oxide.DataFileSystem.ReadObject<LogsData>($"{Name}/Logs");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_logs == null) _logs = new LogsData();
        }

        private void LoadATMs()
        {
            try
            {
                _atms = Interface.Oxide.DataFileSystem.ReadObject<ATMsData>($"{Name}/ATMs");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_atms == null) _atms = new ATMsData();
        }

        #endregion

        #region Wipe

        private void WipeData()
        {
            WipePlayers();

            WipeLogs();

            WipeATMs();
        }

        private void WipePlayers()
        {
            if (_data == null)
                LoadPlayers();

            _data.Players.Clear();
            PrintWarning("Players wiped!");
        }

        private void WipeLogs()
        {
            if (_logs == null)
                LoadLogs();

            _logs.Players?.Clear();
            PrintWarning("Logs wiped!");
        }

        private void WipeATMs()
        {
            if (_atms == null)
                LoadATMs();

            _atms.ATMs?.Clear();
            _atms.LastATMID = 0;
            PrintWarning("ATMs wiped!");
        }

        #endregion

        #region Players

        private class PluginData
        {
            [JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
        }

        private class PlayerData
        {
            [JsonProperty(PropertyName = "Display Name")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Balance")]
            public int Balance;

            [JsonProperty(PropertyName = "Card Number")]
            public string Card;

            [JsonProperty(PropertyName = "Card Date")]
            public DateTime CardDate;
        }

        private PlayerData GetPlayerData(BasePlayer player)
        {
            return GetPlayerData(player.userID);
        }

        private PlayerData GetPlayerData(ulong member)
        {
            if (!_data.Players.ContainsKey(member))
                _data.Players.Add(member, new PlayerData());
            return _data.Players[member];
        }

        #endregion

        #region Logs

        private class LogsData
        {
            [JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, PlayerLogs> Players = new Dictionary<ulong, PlayerLogs>();
        }

        private class PlayerLogs
        {
            [JsonProperty(PropertyName = "Transfers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TransferData> Transfers = new List<TransferData>();

            [JsonProperty(PropertyName = "Gather Logs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<GatherLogData> GatherLogs = new List<GatherLogData>();
        }

        private class TransferData
        {
            [JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
            public Transaction Type;

            [JsonProperty(PropertyName = "Amount")]
            public int Amount;

            [JsonProperty(PropertyName = "Sender ID")]
            public ulong SenderId;

            [JsonProperty(PropertyName = "Target ID")]
            public ulong TargetId;

            public void Get(BasePlayer player, ref CuiElementContainer container, string parent)
            {
                var color = string.Empty;
                var icon = string.Empty;
                var symbol = string.Empty;
                var title = string.Empty;
                var description = string.Empty;

                #region Icon

                switch (Type)
                {
                    case Transaction.Deposit:
                    {
                        color = HexToCuiColor("#4B68FF");
                        icon = _instance.Msg(player, DepositIconTitle);
                        symbol = _instance.Msg(player, DepositSymbolTitle);
                        title = _instance.Msg(player, DepositOperationTitle);
                        description = _instance.Msg(player, DepositOperationDescription);
                        break;
                    }
                    case Transaction.Withdrawal:
                    {
                        color = HexToCuiColor("#FF6060");
                        icon = _instance.Msg(player, WithdrawalIconTitle);
                        symbol = _instance.Msg(player, WithdrawalSymbolTitle);
                        title = _instance.Msg(player, WithdrawalOperationTitle);
                        description = _instance.Msg(player, WithdrawalOperationDescription);
                        break;
                    }
                    case Transaction.Transfer:
                    {
                        var self = TargetId == 0 && SenderId != 0;
                        if (self)
                        {
                            color = HexToCuiColor("#4B68FF");
                            icon = _instance.Msg(player, SelfTransferlIconTitle);
                            symbol = _instance.Msg(player, SelfTransferSymbolTitle);
                            description = _instance.Msg(player, SelfTransferOperationDescription,
                                _instance.GetPlayerData(SenderId)?.DisplayName);
                        }
                        else
                        {
                            color = HexToCuiColor("#FF6060");
                            icon = _instance.Msg(player, TransferlIconTitle);
                            symbol = _instance.Msg(player, TransferSymbolTitle);
                            description = _instance.Msg(player, TransferOperationDescription,
                                _instance.GetPlayerData(TargetId)?.DisplayName);
                        }

                        title = _instance.Msg(player, TransferOperationTitle);
                        break;
                    }
                }

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "5 5", OffsetMax = "30 30"
                    },
                    Image =
                    {
                        Color = color
                    }
                }, parent, parent + ".Icon");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    },
                    Text =
                    {
                        Text = $"{icon}",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 20,
                        Color = "1 1 1 1"
                    }
                }, parent + ".Icon");

                #endregion

                #region Title

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0.5", AnchorMax = "1 1",
                        OffsetMin = "35 0", OffsetMax = "-37.5 0"
                    },
                    Text =
                    {
                        Text = $"{title}",
                        Align = TextAnchor.LowerLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    }
                }, parent);

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0.5",
                        OffsetMin = "35 0", OffsetMax = "-37.5 0"
                    },
                    Text =
                    {
                        Text = $"{description}",
                        Align = TextAnchor.UpperLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 0.5"
                    }
                }, parent);

                #endregion

                #region Value

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "-10 0"
                    },
                    Text =
                    {
                        Text = _instance.Msg(player, OperationsValueFormat, symbol, Amount),
                        Align = TextAnchor.MiddleRight,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = color
                    }
                }, parent);

                #endregion
            }
        }

        private enum GatherLogType
        {
            Gather,
            Loot
        }

        private class GatherLogData
        {
            [JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
            public GatherLogType Type;

            [JsonProperty(PropertyName = "Short Name")]
            public string ShortName;

            [JsonProperty(PropertyName = "Amount")]
            public int Amount;
        }

        private PlayerLogs GetPlayerLogs(BasePlayer player)
        {
            return GetPlayerLogs(player.userID);
        }

        private PlayerLogs GetPlayerLogs(ulong member)
        {
            if (!_logs.Players.ContainsKey(member))
                _logs.Players.Add(member, new PlayerLogs());

            return _logs.Players[member];
        }

        #endregion

        #region ATMs

        private class ATMsData
        {
            [JsonProperty(PropertyName = "Last ATM ID")]
            public int LastATMID;

            [JsonProperty(PropertyName = "ATMs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ATMData> ATMs = new List<ATMData>();
        }

        private class ATMData
        {
            [JsonProperty(PropertyName = "Enity ID")]
            public uint EntityID;

            [JsonProperty(PropertyName = "ATM ID")]
            public int ID;

            [JsonProperty(PropertyName = "Owner Id")]
            public ulong OwnerId;

            [JsonProperty(PropertyName = "Withdrawal Fee")]
            public float WithdrawalFee;

            [JsonProperty(PropertyName = "Deposit Fee")]
            public float DepositFee;

            [JsonProperty(PropertyName = "Condition")]
            public float Condition;

            [JsonProperty(PropertyName = "Balance")]
            public float Balance;

            [JsonProperty(PropertyName = "Is Admin")]
            public bool IsAdmin;

            public bool IsOwner(BasePlayer player)
            {
                return IsOwner(player.userID);
            }

            public bool IsOwner(ulong userId)
            {
                return OwnerId == userId;
            }

            public void LoseCondition()
            {
                if (IsAdmin)
                    return;

                Condition -= LoseConditionAmount();
            }

            private float LoseConditionAmount()
            {
                return (from check in _instance._config.Atm.BreakPercent
                    where _instance.permission.UserHasPermission(OwnerId.ToString(), check.Key)
                    select check.Value).Prepend(_instance._config.Atm.DefaultBreakPercent).Min();
            }

            public bool CanOpen()
            {
                return Condition - LoseConditionAmount() > 0;
            }
        }

        #endregion

        #endregion

        #region Hooks

        private void Init()
        {
            _instance = this;

            LoadData();

            if (_config.Drop.Count == 0)
                Unsubscribe(nameof(OnLootSpawn));
        }

        private void OnServerInitialized(bool initial)
        {
            LoadImages();

            if (StackSizeController || StackModifier)
                Unsubscribe(nameof(OnItemSplit));

            if (!string.IsNullOrEmpty(_config.Permission) &&
                !permission.PermissionExists(_config.Permission))
                permission.RegisterPermission(_config.Permission, this);

            AddCovalenceCommand(_config.Commands, nameof(CmdOpenBank));
            AddCovalenceCommand("bank.givenote", nameof(CmdGiveNotes));
            AddCovalenceCommand("bank.giveatm", nameof(CmdGiveATM));
            AddCovalenceCommand(new[] { "bank.setbalance", "bank.deposit", "bank.withdraw", "bank.transfer" },
                nameof(AdminCommands));
            AddCovalenceCommand("bank.wipe", nameof(WipeCommands));

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);

            RenameATMs();

            if (initial)
                timer.In(20, SpawnATMs);
            else
                SpawnATMs();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;

            GetAvatar(player.userID,
                avatar => ImageLibrary?.Call("AddImage", avatar, $"avatar_{player.UserIDString}"));

            var data = GetPlayerData(player);
            if (data == null) return;

            data.DisplayName = player.displayName;

            CheckCard(player.userID);
        }

        private void OnServerSave()
        {
            timer.In(Random.Range(2, 7), SavePlayers);
            timer.In(Random.Range(2, 7), SaveATMs);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, Layer);

            _vendingMachines.ForEach(entity =>
            {
                if (entity != null)
                    entity.Kill();
            });

            SaveData();

            _instance = null;
        }

        #region Wipe

        private void OnNewSave(string filename)
        {
            if (_config.Wipe.Players) WipePlayers();

            if (_config.Wipe.Logs) WipeLogs();

            if (_config.Wipe.ATMs) WipeATMs();
        }

        #endregion

        #region Loot

        private void OnLootSpawn(LootContainer container)
        {
            if (container == null) return;

            var dropInfo = _config.Drop.Find(x => x.Enabled && x.PrefabName.Contains(container.PrefabName));
            if (dropInfo == null || Random.Range(0, 100) > dropInfo.DropChance) return;

            NextTick(() =>
            {
                if (container.inventory.capacity <= container.inventory.itemList.Count)
                    container.inventory.capacity = container.inventory.itemList.Count + 1;

                _config.Currency.ToItem(Random.Range(dropInfo.MinAmount, dropInfo.MaxAmount))
                    ?.MoveToContainer(container.inventory);
            });
        }

        #endregion

        #region NPC

        private void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (player == null || npc == null || !_config.NPC.NPCs.Contains(npc.UserIDString)) return;

            if (!string.IsNullOrEmpty(_config.Permission) &&
                !permission.UserHasPermission(player.UserIDString, _config.Permission))
            {
                SendNotify(player, NoPermissions, 1);
                return;
            }

            MainUi(player, first: true);
        }

        #endregion

        #region ATM

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null || go == null) return;

            var entity = go.ToBaseEntity() as VendingMachine;
            if (entity == null) return;

            var player = plan.GetOwnerPlayer();
            if (player == null) return;

            var item = player.GetActiveItem();
            if (item == null || item.skin != _config.Atm.Skin) return;

            var data = new ATMData
            {
                ID = ++_atms.LastATMID,
                EntityID = entity.net.ID,
                OwnerId = player.userID,
                Balance = 0,
                Condition = 100,
                DepositFee = _config.Atm.DefaultDepositFee,
                WithdrawalFee = _config.Atm.DefaultWithdrawalFee
            };

            _atms.ATMs.Add(data);

            if (!string.IsNullOrEmpty(_config.Atm.ShopName))
            {
                entity.shopName = _config.Atm.ShopName
                    .Replace("{id}", data.ID.ToString())
                    .Replace("{owner}", player.displayName);
                entity.UpdateMapMarker();
            }
        }

        private void OnEntityDeath(VendingMachine entity, HitInfo hitInfo)
        {
            if (entity == null || entity.net == null)
                return;

            _atms.ATMs.RemoveAll(x => x.EntityID == entity.net.ID);
        }

        private bool? CanUseVending(BasePlayer player, VendingMachine machine)
        {
            if (machine == null || player == null || machine.skinID != _config.Atm.Skin) return null;

            var data = _atms.ATMs.Find(x => x.EntityID == machine.net.ID);
            if (data == null)
                return null;

            if (!HasCard(player))
            {
                SendNotify(player, NotBankCard, 1);
                return false;
            }

            if (!data.CanOpen())
            {
                SendNotify(player, BrokenATM, 1);
                return false;
            }

            _atmByPlayer[player] = data;

            ATMUi(player, First: true);
            return false;
        }

        #endregion

        #region Split

        private Item OnItemSplit(Item item, int amount)
        {
            if (item == null || item.info.shortname != _config.Currency.ShortName ||
                _config.Currency.Skin != item.skin) return null;

            item.amount -= amount;
            var newItem = ItemManager.CreateByItemID(item.info.itemid, 1, item.skin);
            newItem.amount = amount;
            newItem.condition = item.condition;

            if (!string.IsNullOrEmpty(item.name)) newItem.name = item.name;

            if (item.IsBlueprint()) newItem.blueprintTarget = item.blueprintTarget;
            item.MarkDirty();
            return newItem;
        }

        private object CanCombineDroppedItem(DroppedItem droppedItem, DroppedItem targetItem)
        {
            if (droppedItem == null || targetItem == null) return null;

            var item = droppedItem.GetItem();
            if (item == null) return null;

            var tItem = targetItem.GetItem();
            if (tItem == null || item.skin == tItem.skin) return null;

            return item.skin == _config.Currency.Skin || tItem.skin == _config.Currency.Skin;
        }

        private object CanStackItem(Item item, Item targetItem)
        {
            if (item == null || targetItem == null || item.skin == targetItem.skin) return null;

            return item.info.shortname == targetItem.info.shortname &&
                   (item.skin == _config.Currency.Skin || targetItem.skin == _config.Currency.Skin) &&
                   item.skin == targetItem.skin
                ? (object)(item.amount + targetItem.amount < item.info.stackable)
                : null;
        }

        #endregion

        #region Tracking

        #region Gather Tracking

        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            OnGather(player, item.info.shortname, item.amount);
        }

        private void OnCropGather(GrowableEntity plant, Item item, BasePlayer player)
        {
            OnGather(player, item.info.shortname, item.amount);
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            OnGather(player, item.info.shortname, item.amount);
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            OnGather(player, item.info.shortname, item.amount);
        }

        private void OnGather(BasePlayer player, string shortname, int amount)
        {
            if (player == null || string.IsNullOrEmpty(shortname) || amount <= 0) return;

            AddPlayerTracking(GatherLogType.Gather, player, shortname, amount);
        }

        #endregion

        #region Loot

        private readonly Dictionary<uint, List<ulong>> LootedContainers = new Dictionary<uint, List<ulong>>();

        private void OnLootEntity(BasePlayer player, LootContainer container)
        {
            if (player == null || container == null) return;

            var netID = container.net.ID;
            if (LootedContainers.ContainsKey(netID)) return;

            if (LootedContainers.ContainsKey(netID))
            {
                if (LootedContainers[netID].Contains(player.userID))
                    return;

                LootedContainers[netID].Add(player.userID);
            }
            else
            {
                LootedContainers.Add(netID, new List<ulong> { player.userID });
            }

            AddPlayerTracking(GatherLogType.Loot, player, container.ShortPrefabName);
        }

        #endregion

        #endregion

        #endregion

        #region Commands

        private void CmdOpenBank(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (!string.IsNullOrEmpty(_config.Permission) &&
                !permission.UserHasPermission(player.UserIDString, _config.Permission))
            {
                SendNotify(player, NoPermissions, 1);
                return;
            }

            MainUi(player, first: true);
        }

        [ConsoleCommand("UI_BankSystem")]
        private void CmdConsoleBank(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs()) return;

            switch (arg.Args[0])
            {
                case "close":
                {
                    _atmByPlayer.Remove(player);
                    break;
                }

                case "cardcreate":
                {
                    CreateCard(player);

                    MainUi(player);
                    break;
                }

                case "close_select":
                {
                    int amount, type;
                    ulong target;
                    if (!arg.HasArgs(4) || !int.TryParse(arg.Args[1], out type) ||
                        !int.TryParse(arg.Args[2], out amount) || !ulong.TryParse(arg.Args[3], out target)) return;

                    if (type == 0)
                        MainUi(player, amount, target, true);
                    else
                        ATMUi(player, 4, amount, target, true);
                    break;
                }

                case "setamount":
                {
                    ulong target;
                    int amount;
                    if (!arg.HasArgs(3) || !ulong.TryParse(arg.Args[1], out target) ||
                        !int.TryParse(arg.Args[2], out amount)) return;

                    MainUi(player, amount, target);
                    break;
                }

                case "select":
                {
                    int amount, type;
                    ulong target;
                    if (!arg.HasArgs(4) || !int.TryParse(arg.Args[1], out type) ||
                        !int.TryParse(arg.Args[2], out amount) || !ulong.TryParse(arg.Args[3], out target)) return;

                    if (type == 0)
                        MainUi(player, amount, target, true);
                    else
                        ATMUi(player, 4, amount, target, true);
                    break;
                }

                case "settransferinfo":
                {
                    int amount;
                    ulong target;
                    if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out amount) || amount < 0
                        || !ulong.TryParse(arg.Args[2], out target)) return;

                    MainUi(player, amount, target);
                    break;
                }

                case "ui_transfer":
                {
                    int amount;
                    ulong target;
                    if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out amount) ||
                        !ulong.TryParse(arg.Args[2], out target)) return;

                    if (_config.Economy.ShowBalance(player) < amount)
                    {
                        SendNotify(player, NotEnoughMoney, 1);
                        return;
                    }

                    _config.Economy.Transfer(player.userID, target, amount);

                    SendNotify(player, TransferedMoney, 0, amount, GetPlayerData(target)?.DisplayName);

                    AddTransactionLog(Transaction.Transfer, player.userID, target, amount);
                    _atmByPlayer.Remove(player);
                    break;
                }

                case "selectpage":
                {
                    ulong target;
                    int amount, type;
                    if (!arg.HasArgs(4) || !int.TryParse(arg.Args[1], out type) ||
                        !int.TryParse(arg.Args[2], out amount) || !ulong.TryParse(arg.Args[3], out target)) return;

                    var page = 0;
                    if (arg.HasArgs(5))
                        int.TryParse(arg.Args[4], out page);

                    SelectPlayerUi(player, type, amount, target, page);
                    break;
                }

                case "atmpage":
                {
                    int page;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

                    var amount = 0;
                    if (arg.HasArgs(3))
                        int.TryParse(arg.Args[2], out amount);

                    var targetId = 0UL;
                    if (arg.HasArgs(4))
                        ulong.TryParse(arg.Args[3], out targetId);

                    var first = false;
                    if (arg.HasArgs(5))
                        bool.TryParse(arg.Args[4], out first);

                    ATMUi(player, page, amount, targetId, first);
                    break;
                }

                case "atm_input":
                {
                    int page;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

                    var targetId = 0UL;
                    if (arg.HasArgs(3))
                        ulong.TryParse(arg.Args[2], out targetId);

                    var amount = 0;
                    if (arg.HasArgs(4))
                        int.TryParse(arg.Args[3], out amount);

                    ATMUi(player, page, amount, targetId);
                    break;
                }

                case "atm_setdepositfee":
                {
                    float amount;
                    if (!arg.HasArgs(2) || !float.TryParse(arg.Args[1], out amount)) return;

                    if (amount < _config.Atm.MinDepositFee || amount > _config.Atm.MaxDepositFee)
                        return;

                    ATMData ATM;
                    if (!_atmByPlayer.TryGetValue(player, out ATM) || ATM == null || !ATM.IsOwner(player)) return;

                    ATM.DepositFee = amount;

                    ATMUi(player, 1);
                    break;
                }

                case "atm_setwithdrawalfee":
                {
                    float amount;
                    if (!arg.HasArgs(2) || !float.TryParse(arg.Args[1], out amount)) return;

                    if (amount < _config.Atm.MinWithdrawalFee || amount > _config.Atm.MaxWithdrawalFee)
                        return;

                    ATMData ATM;
                    if (!_atmByPlayer.TryGetValue(player, out ATM) || ATM == null || !ATM.IsOwner(player)) return;

                    ATM.WithdrawalFee = amount;

                    ATMUi(player, 1);
                    break;
                }

                case "atm_tryrepair":
                {
                    RepairUi(player);
                    break;
                }

                case "atm_repair":
                {
                    ATMData ATM;
                    if (!_atmByPlayer.TryGetValue(player, out ATM) || ATM == null || !ATM.IsOwner(player)) return;

                    var needPercent = 100 - Mathf.RoundToInt(ATM.Condition);
                    if (needPercent <= 0) return;

                    var allItems = player.inventory.AllItems();

                    if (_config.Atm.Repair.Items.Any(x =>
                        !HasAmount(allItems, x.ShortName, x.Skin, Mathf.CeilToInt(x.Amount * needPercent))))
                    {
                        SendNotify(player, NotEnoughItems, 1);
                        return;
                    }

                    _config.Atm.Repair.Items.ForEach(item =>
                        Take(allItems, item.ShortName, item.Skin, Mathf.CeilToInt(item.Amount * needPercent)));

                    ATM.Condition = 100;

                    ATMUi(player, 1, First: true);
                    break;
                }

                case "atm_deposit":
                {
                    int amount;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out amount)) return;

                    ATMData ATM;
                    if (!_atmByPlayer.TryGetValue(player, out ATM) || ATM == null) return;

                    if (!ATM.CanOpen())
                    {
                        SendNotify(player, BrokenATM, 1);
                        return;
                    }

                    var commission = 0f;

                    var depositAmount = amount;
                    if (ATM.DepositFee > 0)
                    {
                        commission = amount * ATM.DepositFee / 100f;

                        amount = Mathf.RoundToInt(amount + commission);
                    }

                    var items = player.inventory.AllItems();

                    if (!HasAmount(items, _config.Currency.ShortName, _config.Currency.Skin, amount))
                    {
                        SendNotify(player, NotEnoughMoney, 1);
                        return;
                    }

                    if (commission > 0)
                        ATM.Balance += commission;

                    Take(items, _config.Currency.ShortName, _config.Currency.Skin, amount);

                    _config.Economy.AddBalance(player, depositAmount);
                    ATM.LoseCondition();

                    SendNotify(player, DepositedMoney, 0, depositAmount);

                    AddTransactionLog(Transaction.Deposit, player.userID, 0, depositAmount);
                    _atmByPlayer.Remove(player);
                    break;
                }

                case "atm_withdraw":
                {
                    int amount;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out amount)) return;

                    ATMData ATM;
                    if (!_atmByPlayer.TryGetValue(player, out ATM) || ATM == null) return;

                    if (!ATM.CanOpen())
                    {
                        SendNotify(player, BrokenATM, 1);
                        return;
                    }

                    var commission = 0f;

                    var withdrawAmount = amount;
                    if (ATM.WithdrawalFee > 0)
                    {
                        commission = amount * ATM.WithdrawalFee / 100f;

                        amount = Mathf.RoundToInt(amount + commission);
                    }

                    if (_config.Economy.ShowBalance(player) < amount)
                    {
                        SendNotify(player, NotEnoughMoney, 1);
                        return;
                    }

                    if (commission > 0)
                        ATM.Balance += commission;

                    _config.Economy.RemoveBalance(player, amount);
                    ATM.LoseCondition();

                    var item = _config.Currency.ToItem(withdrawAmount);
                    if (item != null)
                        player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);

                    SendNotify(player, WithdrawnMoney, 0, withdrawAmount);

                    AddTransactionLog(Transaction.Withdrawal, player.userID, 0, withdrawAmount);
                    _atmByPlayer.Remove(player);
                    break;
                }

                case "atm_transfer":
                {
                    int amount;
                    ulong target;
                    if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out amount) ||
                        !ulong.TryParse(arg.Args[2], out target)) return;

                    ATMData ATM;
                    if (!_atmByPlayer.TryGetValue(player, out ATM) || ATM == null) return;

                    if (!ATM.CanOpen())
                    {
                        SendNotify(player, BrokenATM, 1);
                        return;
                    }

                    if (_config.Economy.ShowBalance(player) < amount)
                    {
                        SendNotify(player, NotEnoughMoney, 1);
                        return;
                    }

                    _config.Economy.Transfer(player.userID, target, amount);

                    SendNotify(player, TransferedMoney, 0, amount, GetPlayerData(target)?.DisplayName);

                    AddTransactionLog(Transaction.Transfer, player.userID, target, amount);
                    _atmByPlayer.Remove(player);
                    break;
                }

                case "atm_admin_withdraw":
                {
                    ATMData ATM;
                    if (!_atmByPlayer.TryGetValue(player, out ATM) || ATM == null) return;

                    var amount = Mathf.CeilToInt(ATM.Balance);
                    _config.Economy.AddBalance(player, amount);
                    ATM.Balance = 0;

                    SendNotify(player, AtmOwnWithdrawnMoney, 0);

                    AddTransactionLog(Transaction.Deposit, player.userID, 0, amount);
                    _atmByPlayer.Remove(player);
                    break;
                }
            }
        }

        private void CmdGiveNotes(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            if (args.Length == 0)
            {
                player.Reply($"Error syntax! Use: /{command} [targetId] [amount]");
                return;
            }

            var target = covalence.Players.FindPlayer(args[0])?.Object as BasePlayer;
            if (target == null)
            {
                PrintError($"Player '{args[0]}' not found!");
                return;
            }

            var amount = 1;
            if (args.Length > 1)
                int.TryParse(args[1], out amount);

            var item = _config.Currency?.ToItem(amount);
            if (item == null) return;

            target.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
            Puts($"Player {target.displayName} ({target.userID}) received {amount} banknotes");
        }

        private void CmdGiveATM(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            if (args.Length == 0)
            {
                player.Reply($"Error syntax! Use: /{command} [targetId] [amount]");
                return;
            }

            var target = covalence.Players.FindPlayer(args[0])?.Object as BasePlayer;
            if (target == null)
            {
                PrintError($"Player '{args[0]}' not found!");
                return;
            }

            var item = _config.Atm?.ToItem();
            if (item == null) return;

            target.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
            Puts($"Player {target.displayName} ({target.userID}) received ATM");
        }

        private void AdminCommands(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            switch (command)
            {
                case "bank.setbalance":
                {
                    if (args.Length < 2)
                    {
                        PrintError($"Error syntax! Use: /{command} [targetId] [amount]");
                        return;
                    }

                    var target = covalence.Players.FindPlayer(args[0])?.Object as BasePlayer;
                    if (target == null)
                    {
                        PrintError($"Player '{args[0]}' not found!");
                        return;
                    }

                    int amount;
                    if (!int.TryParse(args[1], out amount)) return;

                    var data = GetPlayerData(target);
                    if (data == null) return;

                    data.Balance = amount;
                    break;
                }

                case "bank.deposit":
                {
                    if (args.Length < 2)
                    {
                        PrintError($"Error syntax! Use: /{command} [targetId] [amount]");
                        return;
                    }

                    var target = covalence.Players.FindPlayer(args[0])?.Object as BasePlayer;
                    if (target == null)
                    {
                        PrintError($"Player '{args[0]}' not found!");
                        return;
                    }

                    int amount;
                    if (!int.TryParse(args[1], out amount)) return;

                    _config.Economy.AddBalance(target, amount);
                    break;
                }

                case "bank.withdraw":
                {
                    if (args.Length < 2)
                    {
                        PrintError($"Error syntax! Use: /{command} [targetId] [amount]");
                        return;
                    }

                    var target = covalence.Players.FindPlayer(args[0])?.Object as BasePlayer;
                    if (target == null)
                    {
                        PrintError($"Player '{args[0]}' not found!");
                        return;
                    }

                    int amount;
                    if (!int.TryParse(args[1], out amount)) return;

                    _config.Economy.RemoveBalance(target, amount);
                    break;
                }

                case "bank.transfer":
                {
                    if (args.Length < 3)
                    {
                        PrintError($"Error syntax! Use: /{command} [playerId] [targetId] [amount]");
                        return;
                    }

                    var member = covalence.Players.FindPlayer(args[0])?.Object as BasePlayer;
                    if (member == null)
                    {
                        PrintError($"Player '{args[0]}' not found!");
                        return;
                    }

                    var target = covalence.Players.FindPlayer(args[1])?.Object as BasePlayer;
                    if (target == null)
                    {
                        PrintError($"Target player '{args[1]}' not found!");
                        return;
                    }

                    int amount;
                    if (!int.TryParse(args[2], out amount)) return;

                    _config.Economy.Transfer(member.userID, target.userID, amount);
                    break;
                }
            }
        }

        private void WipeCommands(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            if (args.Length == 0)
            {
                player.Reply($"Error syntax! Use: /{command} [type] (0 - all, 1 - players, 2 - logs, 3 - ATMs)");
                return;
            }

            switch (args[0])
            {
                case "0":
                {
                    WipeData();
                    break;
                }
                case "1":
                {
                    WipePlayers();
                    break;
                }
                case "2":
                {
                    WipeLogs();
                    break;
                }
                case "3":
                {
                    WipeATMs();
                    break;
                }
            }
        }

        #endregion

        #region Interface

        private void MainUi(BasePlayer player, int amount = 0, ulong targetId = 0, bool first = false)
        {
            var container = new CuiElementContainer();

            #region Background

            if (first)
            {
                CuiHelper.DestroyUi(player, Layer);

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image =
                    {
                        Color = "0 0 0 0.9",
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    CursorEnabled = true
                }, "Overlay", Layer);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = "" },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Close = Layer,
                        Command = "UI_BankSystem close"
                    }
                }, Layer);

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = "-450 -295",
                        OffsetMax = "450 300"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#0E0E10")
                    }
                }, Layer, Layer + ".Background");
            }

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1"
                },
                Image =
                {
                    Color = "0 0 0 0"
                }
            }, Layer + ".Background", Layer + ".Main");

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50",
                    OffsetMax = "0 0"
                },
                Image = { Color = HexToCuiColor("#161617") }
            }, Layer + ".Main", Layer + ".Header");


            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "30 0",
                    OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, TitleMenu),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = HexToCuiColor("#FFFFFF")
                }
            }, Layer + ".Header");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-50 -37.5",
                    OffsetMax = "-25 -12.5"
                },
                Text =
                {
                    Text = Msg(player, CloseButton),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 10,
                    Color = HexToCuiColor("#FFFFFF")
                },
                Button =
                {
                    Close = Layer,
                    Color = HexToCuiColor("#4B68FF"),
                    Command = "UI_BankSystem close"
                }
            }, Layer + ".Header");

            #endregion

            #region Second Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-425 -135",
                    OffsetMax = "425 -65"
                },
                Image =
                {
                    Color = HexToCuiColor("#161617")
                }
            }, Layer + ".Main", Layer + ".Second.Header");

            #region Logo

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0.5", AnchorMax = "1 1",
                    OffsetMin = "15 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, MainTitle),
                    Align = TextAnchor.LowerLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 16,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Second.Header");

            #endregion

            #region Welcome

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 0.5",
                    OffsetMin = "15 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, WelcomeTitle, player.displayName),
                    Align = TextAnchor.UpperLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Second.Header");

            #endregion

            #region Avatar

            container.Add(new CuiElement
            {
                Parent = Layer + ".Second.Header",
                Components =
                {
                    new CuiRawImageComponent
                        { Png = ImageLibrary.Call<string>("GetImage", $"avatar_{player.userID}") },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = "-60 -50", OffsetMax = "-25 -15"
                    }
                }
            });

            #endregion

            #region Balance Title

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0.5", AnchorMax = "1 1",
                    OffsetMin = "0 0", OffsetMax = "-75 0"
                },
                Text =
                {
                    Text = Msg(player, YourBalance),
                    Align = TextAnchor.LowerRight,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 14,
                    Color = "1 1 1 0.5"
                }
            }, Layer + ".Second.Header");

            #endregion

            #region Balance

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 0.5",
                    OffsetMin = "0 0", OffsetMax = "-75 0"
                },
                Text =
                {
                    Text = Msg(player, BalanceTitle, _config.Economy.ShowBalance(player)),
                    Align = TextAnchor.UpperRight,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 13,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Second.Header");

            #endregion

            #endregion

            #region Menu

            if (HasCard(player))
            {
                var data = GetPlayerData(player);
                var logs = GetPlayerLogs(player);

                var constXSwitch = -425f;
                var xSwitch = constXSwitch;

                #region Card

                container.Add(new CuiElement
                {
                    Name = Layer + ".Card",
                    Parent = Layer + ".Main",
                    Components =
                    {
                        new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", _config.CardImage) },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-425 -300", OffsetMax = "-180 -150"
                        }
                    }
                });

                #region Logo

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0",
                        OffsetMin = "20 110", OffsetMax = "0 130"
                    },
                    Text =
                    {
                        Text = Msg(player, CardBankTitle),
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 16,
                        Color = "1 1 1 1"
                    }
                }, Layer + ".Card");

                #endregion

                #region Number

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0",
                        OffsetMin = "20 40", OffsetMax = "0 65"
                    },
                    Text =
                    {
                        Text = $"{GetFormattedCardNumber(data.Card)}",
                        Align = TextAnchor.LowerLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 18,
                        Color = "1 1 1 0.1"
                    }
                }, Layer + ".Card");

                #endregion

                #region Name

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0",
                        OffsetMin = "20 20", OffsetMax = "0 40"
                    },
                    Text =
                    {
                        Text = $"{player.displayName.ToUpper()}",
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 12,
                        Color = "1 1 1 1"
                    }
                }, Layer + ".Card");

                #endregion

                #region Date

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0",
                        OffsetMin = "140 20", OffsetMax = "0 40"
                    },
                    Text =
                    {
                        Text = $"{CardDateFormating(player.userID)}",
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 12,
                        Color = "1 1 1 0.1"
                    }
                }, Layer + ".Card");

                #endregion

                #endregion

                #region Transactions

                #region Title

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "-425 -345",
                        OffsetMax = "-300 -325"
                    },
                    Text =
                    {
                        Text = Msg(player, TransfersTitle),
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 16,
                        Color = "1 1 1 1"
                    }
                }, Layer + ".Main");

                #endregion

                #region Frequent Transfers

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "-425 -360",
                        OffsetMax = "-300 -345"
                    },
                    Text =
                    {
                        Text = Msg(player, FrequentTransfers),
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 0.5"
                    }
                }, Layer + ".Main");

                var i = 1;
                var ySwitch = -360f;
                var Height = 60f;
                var Width = 120f;
                var amountOnString = 3;
                var Margin = 10f;
                var amountOnPage = 0;

                var topTransfers = GetTopTransfers(player);
                if (topTransfers == null || topTransfers.Count == 0)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-425 -490",
                            OffsetMax = "-45 -360"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Transfer.Not");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text =
                        {
                            Text = Msg(player, HaventTransactions),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 14,
                            Color = "1 1 1 0.1"
                        }
                    }, Layer + ".Transfer.Not");
                }
                else
                {
                    topTransfers.ForEach(member =>
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                                OffsetMin = $"{xSwitch} {ySwitch - Height}",
                                OffsetMax = $"{xSwitch + Width} {ySwitch}"
                            },
                            Image =
                            {
                                Color = HexToCuiColor("#161617")
                            }
                        }, Layer + ".Main", Layer + $".Transfer.{member}");

                        #region Avatar

                        container.Add(new CuiElement
                        {
                            Parent = Layer + $".Transfer.{member}",
                            Components =
                            {
                                new CuiRawImageComponent
                                    { Png = ImageLibrary.Call<string>("GetImage", $"avatar_{member}") },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "0 0",
                                    OffsetMin = "5 20", OffsetMax = "40 55"
                                }
                            }
                        });

                        #endregion

                        #region Name

                        container.Add(new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 0",
                                OffsetMin = "5 10", OffsetMax = "0 20"
                            },
                            Text =
                            {
                                Text = $"{GetPlayerData(member)?.DisplayName}",
                                Align = TextAnchor.MiddleLeft,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 8,
                                Color = "1 1 1 1"
                            }
                        }, Layer + $".Transfer.{member}");

                        #endregion

                        #region Card Number

                        container.Add(new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 0",
                                OffsetMin = "55 10", OffsetMax = "0 20"
                            },
                            Text =
                            {
                                Text = $"{GetLastCardNumbers(player, GetPlayerData(member)?.Card)}",
                                Align = TextAnchor.MiddleLeft,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 6,
                                Color = "1 1 1 0.3"
                            }
                        }, Layer + $".Transfer.{member}");

                        #endregion

                        #region Button

                        container.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "1 1", AnchorMax = "1 1",
                                OffsetMin = "-65 -40", OffsetMax = "-5 -10"
                            },
                            Text =
                            {
                                Text = Msg(player, TransferTitle),
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 8,
                                Color = "1 1 1 1"
                            },
                            Button =
                            {
                                Color = HexToCuiColor("#4B68FF"),
                                Command = $"UI_BankSystem ui_transfer {amount} {member}"
                            }
                        }, Layer + $".Transfer.{member}");

                        #endregion

                        if (i % amountOnString == 0)
                        {
                            ySwitch = ySwitch - Height - Margin;
                            xSwitch = constXSwitch;
                        }
                        else
                        {
                            xSwitch += Width + Margin;
                        }

                        i++;
                    });
                }

                #endregion

                #region Card Transfers

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "-425 -515",
                        OffsetMax = "-300 -500"
                    },
                    Text =
                    {
                        Text = Msg(player, TransferByCard),
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 0.5"
                    }
                }, Layer + ".Main");

                #region Number

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "-425 -550",
                        OffsetMax = "-275 -520"
                    },
                    Text =
                    {
                        Text = targetId != 0
                            ? GetLastCardNumbers(player, GetPlayerData(targetId)?.Card)
                            : Msg(player, CardNumberTitle),
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#161617"),
                        Command = $"UI_BankSystem selectpage 0 {amount} {targetId}"
                    }
                }, Layer + ".Main");

                #endregion

                #region Amount

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "-265 -550",
                        OffsetMax = "-115 -520"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#161617")
                    }
                }, Layer + ".Main", Layer + ".Enter.Amount");

                if (amount > 0)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "10 0", OffsetMax = "-10 0"
                        },
                        Text =
                        {
                            Text = $"{amount}",
                            FontSize = 10,
                            Font = "robotocondensed-regular.ttf",
                            Align = TextAnchor.MiddleLeft,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Enter.Amount");

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "1 0", AnchorMax = "1 1",
                            OffsetMin = "-20 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, RemoveAmountTitle),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 10,
                            Color = "1 1 1 "
                        },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = $"UI_BankSystem setamount {targetId} 0"
                        }
                    }, Layer + ".Enter.Amount");
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Enter.Amount",
                        Components =
                        {
                            new CuiInputFieldComponent
                            {
                                FontSize = 10,
                                Font = "robotocondensed-regular.ttf",
                                Align = TextAnchor.MiddleLeft,
                                Command = $"UI_BankSystem setamount {targetId} ",
                                Color = "1 1 1 1",
                                CharsLimit = 10
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "10 0", OffsetMax = "-10 0"
                            }
                        }
                    });
                }


                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "-105 -550",
                        OffsetMax = "-45 -520"
                    },
                    Text =
                    {
                        Text = Msg(player, TransferTitle),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 11,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#4B68FF", 50),
                        Command = targetId != 0 ? $"UI_BankSystem ui_transfer {amount} {targetId}" : ""
                    }
                }, Layer + ".Main");

                #endregion

                #endregion

                #endregion

                #region Transaction history

                if (logs.Transfers.Count > 0)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "5 -175",
                            OffsetMax = "170 -150"
                        },
                        Text =
                        {
                            Text = Msg(player, TransactionHistory),
                            Align = TextAnchor.MiddleLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 14,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".Main");

                    amountOnPage = 9;
                    Height = 35f;
                    Margin = 7.5f;

                    ySwitch = -175f;

                    foreach (var transcation in logs.Transfers.Take(amountOnPage))
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                                OffsetMin = $"5 {ySwitch - Height}",
                                OffsetMax = $"195 {ySwitch}"
                            },
                            Image =
                            {
                                Color = HexToCuiColor("#161617")
                            }
                        }, Layer + ".Main", Layer + $".Transaction.{ySwitch}");

                        transcation.Get(player, ref container, Layer + $".Transaction.{ySwitch}");

                        ySwitch = ySwitch - Margin - Height;
                    }
                }

                #endregion

                #region Gather salary

                if (logs.GatherLogs.Count > 0)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "230 -175",
                            OffsetMax = "420 -150"
                        },
                        Text =
                        {
                            Text = Msg(player, GatherHistory),
                            Align = TextAnchor.MiddleLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 14,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".Main");

                    amountOnPage = 10;
                    Height = 35f;
                    Margin = 2.5f;

                    ySwitch = -175f;

                    var gatherLogs = logs.GatherLogs.ToList();
                    gatherLogs.Reverse();
                    foreach (var gather in gatherLogs.Take(amountOnPage))
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                                OffsetMin = $"230 {ySwitch - Height}",
                                OffsetMax = $"420 {ySwitch}"
                            },
                            Image =
                            {
                                Color = HexToCuiColor("#161617")
                            }
                        }, Layer + ".Main", Layer + $".Gather.{ySwitch}");

                        container.Add(new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "7.5 0", OffsetMax = "0 0"
                            },
                            Text =
                            {
                                Text = gather.Type == GatherLogType.Gather
                                    ? Msg(player, MiningFee, GetItemTitle(gather.ShortName))
                                    : Msg(player, LootFee, Msg(player, gather.ShortName)),
                                Align = TextAnchor.MiddleLeft,
                                Font = "robotocondensed-regular.ttf",
                                FontSize = 10,
                                Color = "1 1 1 1"
                            }
                        }, Layer + $".Gather.{ySwitch}");

                        container.Add(new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "0 0", OffsetMax = "-10 0"
                            },
                            Text =
                            {
                                Text = Msg(player, MiningValue, gather.Amount),
                                Align = TextAnchor.MiddleRight,
                                Font = "robotocondensed-regular.ttf",
                                FontSize = 10,
                                Color = HexToCuiColor("#4B68FF")
                            }
                        }, Layer + $".Gather.{ySwitch}");

                        ySwitch = ySwitch - Margin - Height;
                    }
                }

                #endregion
            }
            else
            {
                #region Create Card

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "-425 -300",
                        OffsetMax = "-275 -150"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#161617")
                    }
                }, Layer + ".Main", Layer + ".Crate.Card");

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = "-2.5 -22.5", OffsetMax = "2.5 37.5"
                    },
                    Image =
                    {
                        Color = "1 1 1 0.4"
                    }
                }, Layer + ".Crate.Card");

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = "-30 5", OffsetMax = "-2.5 10"
                    },
                    Image =
                    {
                        Color = "1 1 1 0.4"
                    }
                }, Layer + ".Crate.Card");

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = "2.5 5", OffsetMax = "30 10"
                    },
                    Image =
                    {
                        Color = "1 1 1 0.4"
                    }
                }, Layer + ".Crate.Card");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0.5",
                        OffsetMin = "0 0", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = Msg(player, CreateCardTitle),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Color = "1 1 1 0.4"
                    }
                }, Layer + ".Crate.Card");

                CreateOutLine(ref container, Layer + ".Crate.Card", HexToCuiColor("#FFFFFF", 20), 1);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = "" },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = "UI_BankSystem cardcreate"
                    }
                }, Layer + ".Crate.Card");

                #endregion
            }

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Main");
            CuiHelper.AddUi(player, container);
        }

        private void SelectPlayerUi(BasePlayer player, int type, int amount, ulong target, int page = 0)
        {
            #region Fields

            var Width = 180f;
            var Height = 50f;
            var xMargin = 20f;
            var yMargin = 30f;

            var amountOnString = 4;
            var strings = 5;
            var totalAmount = amountOnString * strings;

            var constSwitch = -(amountOnString * Width + (amountOnString - 1) * xMargin) / 2f;

            var xSwitch = constSwitch;
            var ySwitch = -180f;

            var i = 1;
            var players = BasePlayer.activePlayerList.Where(x => player != x && HasCard(x)).ToList();

            #endregion

            var container = new CuiElementContainer();

            #region Background

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image =
                {
                    Color = "0.19 0.19 0.18 0.65",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                CursorEnabled = true
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "" },
                Button =
                {
                    Color = "0 0 0 0",
                    Close = Layer,
                    Command = $"UI_BankSystem close_select {type} {amount} {target}"
                }
            }, Layer);

            #endregion

            #region Title

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-200 -140",
                    OffsetMax = "200 -100"
                },
                Text =
                {
                    Text = Msg(player, SelectPlayerTitle),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 32,
                    Color = "1 1 1 1"
                }
            }, Layer);

            #endregion

            #region Players

            foreach (var member in players.Skip(page * totalAmount).Take(totalAmount))
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = $"{xSwitch} {ySwitch - Height}",
                        OffsetMax = $"{xSwitch + Width} {ySwitch}"
                    },
                    Image =
                    {
                        Color = "0 0 0 0"
                    }
                }, Layer, Layer + $".Player.{i}");

                container.Add(new CuiElement
                {
                    Parent = Layer + $".Player.{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                            { Png = ImageLibrary.Call<string>("GetImage", $"avatar_{member.userID}") },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = "0 0", OffsetMax = "50 50"
                        }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0.5", AnchorMax = "1 1",
                        OffsetMin = "55 0", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = $"{member.displayName}",
                        Align = TextAnchor.LowerLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 18,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Player.{i}");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0.5",
                        OffsetMin = "55 0", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = $"{GetLastCardNumbers(player, GetPlayerData(member)?.Card)}",
                        Align = TextAnchor.UpperLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 18,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Player.{i}");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = "" },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_BankSystem select {type} {amount} {member.userID}"
                    }
                }, Layer + $".Player.{i}");

                if (i % amountOnString == 0)
                {
                    xSwitch = constSwitch;
                    ySwitch = ySwitch - Height - yMargin;
                }
                else
                {
                    xSwitch += Width + xMargin;
                }

                i++;
            }

            #endregion

            #region Pages

            var pageSize = 25f;
            var selPageSize = 40f;
            xMargin = 5f;

            var pages = (int)Math.Ceiling((double)players.Count / totalAmount);
            if (pages > 1)
            {
                xSwitch = -((pages - 1) * pageSize + (pages - 1) * xMargin + selPageSize) / 2f;

                for (var j = 0; j < pages; j++)
                {
                    var selected = page == j;

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                            OffsetMin = $"{xSwitch} 60",
                            OffsetMax =
                                $"{xSwitch + (selected ? selPageSize : pageSize)} {60 + (selected ? selPageSize : pageSize)}"
                        },
                        Text =
                        {
                            Text = $"{j + 1}",
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = selected ? 18 : 12,
                            Color = "1 1 1 1"
                        },
                        Button =
                        {
                            Color = HexToCuiColor("#4B68FF"),
                            Command = $"UI_BankSystem selectpage {type} {amount} {target} {j}"
                        }
                    }, Layer);

                    xSwitch += (selected ? selPageSize : pageSize) + xMargin;
                }
            }

            #endregion

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void ATMUi(BasePlayer player, int page = 0, int amount = 0, ulong targetId = 0, bool First = false)
        {
            ATMData atmData;
            if (!_atmByPlayer.TryGetValue(player, out atmData) || atmData == null)
                return;

            var container = new CuiElementContainer();

            #region Background

            if (First)
            {
                CuiHelper.DestroyUi(player, Layer);

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image =
                    {
                        Color = "0 0 0 0.9",
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    CursorEnabled = true
                }, "Overlay", Layer);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = "" },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Close = Layer,
                        Command = "UI_BankSystem close"
                    }
                }, Layer);

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = "-425 -225",
                        OffsetMax = "425 225"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#0E0E10")
                    }
                }, Layer, Layer + ".Background");
            }

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1"
                },
                Image =
                {
                    Color = "0 0 0 0"
                }
            }, Layer + ".Background", Layer + ".Main");

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50",
                    OffsetMax = "0 0"
                },
                Image = { Color = HexToCuiColor("#161617") }
            }, Layer + ".Main", Layer + ".Header");


            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "30 0",
                    OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, TitleMenu),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = HexToCuiColor("#FFFFFF")
                }
            }, Layer + ".Header");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-50 -37.5",
                    OffsetMax = "-25 -12.5"
                },
                Text =
                {
                    Text = Msg(player, CloseButton),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 10,
                    Color = HexToCuiColor("#FFFFFF")
                },
                Button =
                {
                    Close = Layer,
                    Color = HexToCuiColor("#4B68FF"),
                    Command = "UI_BankSystem close"
                }
            }, Layer + ".Header");

            #endregion

            #region Second Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-400 -135",
                    OffsetMax = "400 -65"
                },
                Image =
                {
                    Color = HexToCuiColor("#161617")
                }
            }, Layer + ".Main", Layer + ".Second.Header");

            #region Logo

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0.5", AnchorMax = "1 1",
                    OffsetMin = "15 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, MainTitle),
                    Align = TextAnchor.LowerLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 16,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Second.Header");

            #endregion

            #region Welcome

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 0.5",
                    OffsetMin = "15 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, WelcomeTitle, player.displayName),
                    Align = TextAnchor.UpperLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Second.Header");

            #endregion

            #region Avatar

            container.Add(new CuiElement
            {
                Parent = Layer + ".Second.Header",
                Components =
                {
                    new CuiRawImageComponent
                        { Png = ImageLibrary.Call<string>("GetImage", $"avatar_{player.userID}") },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = "-60 -50", OffsetMax = "-25 -15"
                    }
                }
            });

            #endregion

            #region Balance Title

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0.5", AnchorMax = "1 1",
                    OffsetMin = "0 0", OffsetMax = "-75 0"
                },
                Text =
                {
                    Text = Msg(player, YourBalance),
                    Align = TextAnchor.LowerRight,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 14,
                    Color = "1 1 1 0.5"
                }
            }, Layer + ".Second.Header");

            #endregion

            #region Balance

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 0.5",
                    OffsetMin = "0 0", OffsetMax = "-75 0"
                },
                Text =
                {
                    Text = Msg(player, BalanceTitle, _config.Economy.ShowBalance(player)),
                    Align = TextAnchor.UpperRight,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 13,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Second.Header");

            #endregion

            #endregion

            switch (page)
            {
                case 0:
                {
                    #region Deposit

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-400 -255",
                            OffsetMax = "-110 -155"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Deposit");

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Deposit",
                        Components =
                        {
                            new CuiRawImageComponent
                                { Png = ImageLibrary.Call<string>("GetImage", _config.DepositImage) },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = "25 25", OffsetMax = "75 75"
                            }
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "85 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, DepositTitle),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 18,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".Deposit");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0.5",
                            OffsetMin = "85 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, DepositDescription),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Deposit");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = "" },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = "UI_BankSystem atmpage 2"
                        }
                    }, Layer + ".Deposit");

                    #endregion

                    #region Withdraw

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-90 -255",
                            OffsetMax = "200 -155"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Withdraw");

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Withdraw",
                        Components =
                        {
                            new CuiRawImageComponent
                                { Png = ImageLibrary.Call<string>("GetImage", _config.WithdrawImage) },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = "25 25", OffsetMax = "75 75"
                            }
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "85 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, WithdrawTitle),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 18,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".Withdraw");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0.5",
                            OffsetMin = "85 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, WithdrawDescription),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Withdraw");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = "" },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = "UI_BankSystem atmpage 3"
                        }
                    }, Layer + ".Withdraw");

                    #endregion

                    #region Transfer

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-400 -380",
                            OffsetMax = "-110 -280"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Transfer");

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Transfer",
                        Components =
                        {
                            new CuiRawImageComponent
                                { Png = ImageLibrary.Call<string>("GetImage", _config.TransferImage) },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = "25 25", OffsetMax = "75 75"
                            }
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "85 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, TransferTitle),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 18,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".Transfer");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0.5",
                            OffsetMin = "85 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, TransferDescription),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Transfer");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = "" },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = "UI_BankSystem atmpage 4"
                        }
                    }, Layer + ".Transfer");

                    #endregion

                    #region Exit

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-90 -380",
                            OffsetMax = "200 -280"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Exit");

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Exit",
                        Components =
                        {
                            new CuiRawImageComponent
                                { Png = ImageLibrary.Call<string>("GetImage", _config.ExitImage) },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = "25 25", OffsetMax = "75 75"
                            }
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "85 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, ExitTitle),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 18,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".Exit");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0.5",
                            OffsetMin = "85 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, ExitDescription),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Exit");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = "" },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Close = Layer,
                            Command = "UI_BankSystem close"
                        }
                    }, Layer + ".Exit");

                    #endregion

                    InfoUi(player, ref container, atmData, page);
                    break;
                }

                case 1:
                {
                    #region Profit

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-400 -255",
                            OffsetMax = "-110 -155"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Profit");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "25 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, ATMProfit),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Profit");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0.5",
                            OffsetMin = "25 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, ProfitValue, atmData.Balance),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 16,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".Profit");

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                            OffsetMin = "-155 -15", OffsetMax = "-25 20"
                        },
                        Text =
                        {
                            Text = Msg(player, WithdrawTitle),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 14,
                            Color = "1 1 1 1"
                        },
                        Button =
                        {
                            Color = HexToCuiColor("#4B68FF"),
                            Command = "UI_BankSystem atm_admin_withdraw",
                            Close = Layer
                        }
                    }, Layer + ".Profit");

                    #endregion

                    #region Condition

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-90 -255",
                            OffsetMax = "200 -155"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Condition");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "25 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, ATMCondition),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Condition");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0.5",
                            OffsetMin = "25 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, ConditionValue, atmData.Condition),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 16,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".Condition");

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                            OffsetMin = "-155 -15", OffsetMax = "-25 20"
                        },
                        Text =
                        {
                            Text = Msg(player, ATMRepair),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 14,
                            Color = "1 1 1 1"
                        },
                        Button =
                        {
                            Color = HexToCuiColor("#4B68FF"),
                            Command = atmData.Condition < 100 ? "UI_BankSystem atm_tryrepair" : ""
                        }
                    }, Layer + ".Condition");

                    #endregion

                    #region Deposit Fee

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-400 -380",
                            OffsetMax = "-110 -280"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".DepositFee");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = "25 -30",
                            OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, DepositFeeTitle),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".DepositFee");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = "25 -50", OffsetMax = "0 -30"
                        },
                        Text =
                        {
                            Text = Msg(player, FeeValue, atmData.DepositFee),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 14,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".DepositFee");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0",
                            OffsetMin = "25 20", OffsetMax = "0 35"
                        },
                        Text =
                        {
                            Text = Msg(player, FeeValue, _config.Atm.MinDepositFee),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".DepositFee");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0",
                            OffsetMin = "0 20", OffsetMax = "-15 35"
                        },
                        Text =
                        {
                            Text = Msg(player, FeeValue, _config.Atm.MaxDepositFee),
                            Align = TextAnchor.UpperRight,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".DepositFee");

                    #region Buttons

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0",
                            OffsetMin = "25 35", OffsetMax = "-15 40"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#C4C4C4", 20)
                        }
                    }, Layer + ".DepositFee", Layer + ".DepositFee.Progress");

                    var fullFee = _config.Atm.MaxDepositFee - _config.Atm.MinDepositFee;
                    var progress = atmData.DepositFee / fullFee;
                    if (progress > 0)
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = $"{progress} 0.95" },
                            Image =
                            {
                                Color = HexToCuiColor("#4B68FF")
                            }
                        }, Layer + ".DepositFee.Progress", Layer + ".DepositFee.Progress.Finish");

                        container.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                                OffsetMin = "-5 -5", OffsetMax = "5 5"
                            },
                            Image =
                            {
                                Color = "1 1 1 1"
                            }
                        }, Layer + ".DepositFee.Progress.Finish");
                    }

                    var steps = Mathf.CeilToInt(fullFee / _config.Atm.StepDepositFee);
                    var Width = 250f;
                    var stepSize = Width / steps;

                    var xSwitch = 0f;
                    for (var z = 0; z < steps; z++)
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "0 1",
                                OffsetMin = $"{xSwitch} 0",
                                OffsetMax = $"{xSwitch + stepSize} 0"
                            },
                            Text =
                            {
                                Text = ""
                            },
                            Button =
                            {
                                Color = "0 0 0 0",
                                Command =
                                    $"UI_BankSystem atm_setdepositfee {_config.Atm.StepDepositFee * (z + 1)}"
                            }
                        }, Layer + ".DepositFee.Progress");

                        xSwitch += stepSize;
                    }

                    #endregion

                    #endregion

                    #region Withdrawal Fee

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-90 -380",
                            OffsetMax = "200 -280"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".WithdrawalFee");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = "25 -30",
                            OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, WithdrawalFeeTitle),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".WithdrawalFee");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = "25 -50", OffsetMax = "0 -30"
                        },
                        Text =
                        {
                            Text = Msg(player, FeeValue, atmData.WithdrawalFee),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 14,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".WithdrawalFee");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0",
                            OffsetMin = "25 20", OffsetMax = "0 35"
                        },
                        Text =
                        {
                            Text = Msg(player, FeeValue, _config.Atm.MinWithdrawalFee),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".WithdrawalFee");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0",
                            OffsetMin = "0 20", OffsetMax = "-15 35"
                        },
                        Text =
                        {
                            Text = Msg(player, FeeValue, _config.Atm.MaxWithdrawalFee),
                            Align = TextAnchor.UpperRight,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".WithdrawalFee");

                    #region Buttons

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0",
                            OffsetMin = "25 35", OffsetMax = "-15 40"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#C4C4C4", 20)
                        }
                    }, Layer + ".WithdrawalFee", Layer + ".WithdrawalFee.Progress");

                    fullFee = _config.Atm.MaxWithdrawalFee - _config.Atm.MinWithdrawalFee;
                    progress = atmData.WithdrawalFee / fullFee;
                    if (progress > 0)
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = $"{progress} 0.95" },
                            Image =
                            {
                                Color = HexToCuiColor("#4B68FF")
                            }
                        }, Layer + ".WithdrawalFee.Progress", Layer + ".WithdrawalFee.Progress.Finish");

                        container.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                                OffsetMin = "-5 -5", OffsetMax = "5 5"
                            },
                            Image =
                            {
                                Color = "1 1 1 1"
                            }
                        }, Layer + ".WithdrawalFee.Progress.Finish");
                    }

                    steps = Mathf.CeilToInt(fullFee / _config.Atm.StepWithdrawalFee);

                    Width = 250f;

                    stepSize = Width / steps;

                    xSwitch = 0f;
                    for (var z = 0; z < steps; z++)
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "0 1",
                                OffsetMin = $"{xSwitch} 0",
                                OffsetMax = $"{xSwitch + stepSize} 0"
                            },
                            Text =
                            {
                                Text = ""
                            },
                            Button =
                            {
                                Color = "0 0 0 0",
                                Command =
                                    $"UI_BankSystem atm_setwithdrawalfee {_config.Atm.StepWithdrawalFee * (z + 1)}"
                            }
                        }, Layer + ".WithdrawalFee.Progress");

                        xSwitch += stepSize;
                    }

                    #endregion

                    #endregion

                    InfoUi(player, ref container, atmData, page);
                    break;
                }

                case 2: //Deposit
                {
                    #region Input

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-400 -255",
                            OffsetMax = "-110 -155"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Input");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "25 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, EnterAmount),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Input");

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Input",
                        Components =
                        {
                            new CuiInputFieldComponent
                            {
                                FontSize = 16,
                                Align = TextAnchor.UpperLeft,
                                Font = "robotocondensed-bold.ttf",
                                Command = $"UI_BankSystem atm_input {page} {targetId} ",
                                Color = "1 1 1 1",
                                CharsLimit = 9
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 0.5",
                                OffsetMin = "25 0", OffsetMax = "0 0"
                            }
                        }
                    });

                    #endregion

                    #region Button

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-90 -255",
                            OffsetMax = "200 -155"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Button");


                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "25 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, TotalDeposit),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Button");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0.5",
                            OffsetMin = "25 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, TotalValue,
                                atmData.DepositFee > 0
                                    ? Mathf.RoundToInt(amount + amount * atmData.DepositFee / 100f)
                                    : amount),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 16,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".Button");

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                            OffsetMin = "-110 -15", OffsetMax = "-20 20"
                        },
                        Text =
                        {
                            Text = Msg(player, DepositTitle),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Color = "1 1 1 1"
                        },
                        Button =
                        {
                            Color = HexToCuiColor("#4B68FF"),
                            Command = amount > 0 ? $"UI_BankSystem atm_deposit {amount}" : "",
                            Close = Layer
                        }
                    }, Layer + ".Button");

                    #endregion

                    #region Exit

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-90 -380",
                            OffsetMax = "200 -280"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Exit");

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Exit",
                        Components =
                        {
                            new CuiRawImageComponent
                                { Png = ImageLibrary.Call<string>("GetImage", _config.ExitImage) },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = "25 25", OffsetMax = "75 75"
                            }
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "85 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, ExitTitle),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 18,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".Exit");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0.5",
                            OffsetMin = "85 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, ExitDescription),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Exit");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = "" },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Close = Layer,
                            Command = "UI_BankSystem close"
                        }
                    }, Layer + ".Exit");

                    #endregion

                    InfoUi(player, ref container, atmData, page);
                    break;
                }

                case 3: //Withdrawal
                {
                    #region Input

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-400 -255",
                            OffsetMax = "-110 -155"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Input");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "25 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, EnterAmount),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Input");

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Input",
                        Components =
                        {
                            new CuiInputFieldComponent
                            {
                                FontSize = 16,
                                Align = TextAnchor.UpperLeft,
                                Font = "robotocondensed-bold.ttf",
                                Command = $"UI_BankSystem atm_input {page} {targetId} ",
                                Color = "1 1 1 1",
                                CharsLimit = 9
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 0.5",
                                OffsetMin = "25 0", OffsetMax = "0 0"
                            }
                        }
                    });

                    #endregion

                    #region Button

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-90 -255",
                            OffsetMax = "200 -155"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Button");


                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "25 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, TotalWithdraw),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Button");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0.5",
                            OffsetMin = "25 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, TotalValue,
                                atmData.WithdrawalFee > 0
                                    ? Mathf.RoundToInt(amount + amount * atmData.WithdrawalFee / 100f)
                                    : amount),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 16,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".Button");

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                            OffsetMin = "-110 -15", OffsetMax = "-20 20"
                        },
                        Text =
                        {
                            Text = Msg(player, WithdrawTitle),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Color = "1 1 1 1"
                        },
                        Button =
                        {
                            Color = HexToCuiColor("#4B68FF"),
                            Command = amount > 0 ? $"UI_BankSystem atm_withdraw {amount}" : "",
                            Close = Layer
                        }
                    }, Layer + ".Button");

                    #endregion

                    #region Exit

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-90 -380",
                            OffsetMax = "200 -280"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Exit");

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Exit",
                        Components =
                        {
                            new CuiRawImageComponent
                                { Png = ImageLibrary.Call<string>("GetImage", _config.ExitImage) },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = "25 25", OffsetMax = "75 75"
                            }
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "85 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, ExitTitle),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 18,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".Exit");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0.5",
                            OffsetMin = "85 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, ExitDescription),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Exit");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = "" },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Close = Layer,
                            Command = "UI_BankSystem close"
                        }
                    }, Layer + ".Exit");

                    #endregion

                    InfoUi(player, ref container, atmData, page);
                    break;
                }

                case 4: //Transfer
                {
                    #region Input

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-400 -255",
                            OffsetMax = "-110 -155"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Input");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "25 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, EnterAmount),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Input");

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Input",
                        Components =
                        {
                            new CuiInputFieldComponent
                            {
                                FontSize = 16,
                                Align = TextAnchor.UpperLeft,
                                Font = "robotocondensed-bold.ttf",
                                Command = $"UI_BankSystem atm_input {page} {targetId} ",
                                Color = "1 1 1 1",
                                CharsLimit = 9
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 0.5",
                                OffsetMin = "25 0", OffsetMax = "0 0"
                            }
                        }
                    });

                    #endregion

                    #region Button

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-90 -255",
                            OffsetMax = "200 -155"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Button");


                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "25 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, TotalTransfer),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Button");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0.5",
                            OffsetMin = "25 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, TotalValue, amount),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 16,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".Button");

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                            OffsetMin = "-110 -15", OffsetMax = "-20 20"
                        },
                        Text =
                        {
                            Text = Msg(player, TransferTitle),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Color = "1 1 1 1"
                        },
                        Button =
                        {
                            Color = HexToCuiColor("#4B68FF"),
                            Command = $"UI_BankSystem atm_transfer {amount} {targetId}",
                            Close = Layer
                        }
                    }, Layer + ".Button");

                    #endregion

                    #region Select Players

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-400 -380",
                            OffsetMax = "-110 -280"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Select");

                    if (targetId != 0)
                    {
                        container.Add(new CuiElement
                        {
                            Parent = Layer + ".Select",
                            Components =
                            {
                                new CuiRawImageComponent
                                    { Png = ImageLibrary.Call<string>("GetImage", $"avatar_{targetId}") },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "0 0",
                                    OffsetMin = "25 25", OffsetMax = "75 75"
                                }
                            }
                        });

                        container.Add(new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0.5", AnchorMax = "1 1",
                                OffsetMin = "85 0", OffsetMax = "0 0"
                            },
                            Text =
                            {
                                Text = $"{GetPlayerData(targetId)?.DisplayName}",
                                Align = TextAnchor.LowerLeft,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 18,
                                Color = "1 1 1 1"
                            }
                        }, Layer + ".Select");

                        container.Add(new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 0.5",
                                OffsetMin = "85 0", OffsetMax = "0 0"
                            },
                            Text =
                            {
                                Text = $"{GetLastCardNumbers(player, GetPlayerData(targetId)?.Card)}",
                                Align = TextAnchor.UpperLeft,
                                Font = "robotocondensed-regular.ttf",
                                FontSize = 12,
                                Color = "1 1 1 0.5"
                            }
                        }, Layer + ".Select");
                    }
                    else
                    {
                        container.Add(new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1"
                            },
                            Text =
                            {
                                Text = Msg(player, SelectPlayerSecond),
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 18,
                                Color = "1 1 1 0.1"
                            }
                        }, Layer + ".Select");
                    }

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = "" },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = $"UI_BankSystem selectpage 1 {amount} {targetId}"
                        }
                    }, Layer + ".Select");

                    #endregion

                    #region Exit

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-90 -380",
                            OffsetMax = "200 -280"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#161617")
                        }
                    }, Layer + ".Main", Layer + ".Exit");

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Exit",
                        Components =
                        {
                            new CuiRawImageComponent
                                { Png = ImageLibrary.Call<string>("GetImage", _config.ExitImage) },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = "25 25", OffsetMax = "75 75"
                            }
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1",
                            OffsetMin = "85 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, ExitTitle),
                            Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 18,
                            Color = "1 1 1 1"
                        }
                    }, Layer + ".Exit");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0.5",
                            OffsetMin = "85 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, ExitDescription),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.5"
                        }
                    }, Layer + ".Exit");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = "" },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Close = Layer,
                            Command = "UI_BankSystem close"
                        }
                    }, Layer + ".Exit");

                    #endregion

                    InfoUi(player, ref container, atmData, page);
                    break;
                }
            }

            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Main");
            CuiHelper.AddUi(player, container);
        }

        private void InfoUi(BasePlayer player, ref CuiElementContainer container, ATMData atmData, int page)
        {
            #region Info

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "220 -380",
                    OffsetMax = "400 -155"
                },
                Image =
                {
                    Color = HexToCuiColor("#161617")
                }
            }, Layer + ".Main", Layer + ".Info");

            #region Title

            var ySwitch = 0;

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = $"20 {ySwitch - 35}", OffsetMax = $"0 {ySwitch}"
                },
                Text =
                {
                    Text = atmData.IsAdmin ? Msg(player, ATMAdminTitle) : Msg(player, ATMTitle, atmData.ID),
                    Align = TextAnchor.LowerLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Info");

            #endregion

            #region Owner

            if (!atmData.IsAdmin)
            {
                ySwitch -= 50;

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1",
                        OffsetMin = $"20 {ySwitch - 15}", OffsetMax = $"0 {ySwitch}"
                    },
                    Text =
                    {
                        Text = Msg(player, ATMOwner),
                        Align = TextAnchor.LowerLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 12,
                        Color = "1 1 1 0.2"
                    }
                }, Layer + ".Info");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1",
                        OffsetMin = $"20 {ySwitch - 30}", OffsetMax = $"0 {ySwitch - 15}"
                    },
                    Text =
                    {
                        Text = $"{GetPlayerData(atmData.OwnerId)?.DisplayName}",
                        Align = TextAnchor.UpperLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 12,
                        Color = "1 1 1 1"
                    }
                }, Layer + ".Info");
            }

            #endregion

            ySwitch -= 45;

            #region Deposit Percent

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = $"20 {ySwitch - 15}", OffsetMax = $"0 {ySwitch}"
                },
                Text =
                {
                    Text = Msg(player, InfoDepositTitle),
                    Align = TextAnchor.LowerLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 0.2"
                }
            }, Layer + ".Info");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = $"20 {ySwitch - 35}", OffsetMax = $"0 {ySwitch - 15}"
                },
                Text =
                {
                    Text = Msg(player, InfoValueTitle, atmData.DepositFee),
                    Align = TextAnchor.UpperLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Info");

            #endregion

            #region Withdrawal Percent

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = $"110 {ySwitch - 15}", OffsetMax = $"0 {ySwitch}"
                },
                Text =
                {
                    Text = Msg(player, InfoWithdrawalTitle),
                    Align = TextAnchor.LowerLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 0.2"
                }
            }, Layer + ".Info");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = $"110 {ySwitch - 35}", OffsetMax = $"0 {ySwitch - 15}"
                },
                Text =
                {
                    Text = Msg(player, InfoValueTitle, atmData.WithdrawalFee),
                    Align = TextAnchor.UpperLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Info");

            #endregion

            #region Mange

            if (atmData.IsOwner(player))
            {
                if (page == 0)
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                            OffsetMin = "-55 25", OffsetMax = "55 60"
                        },
                        Text =
                        {
                            Text = Msg(player, InfoManageBtn),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Color = "1 1 1 1"
                        },
                        Button =
                        {
                            Color = HexToCuiColor("#4B68FF"),
                            Command = "UI_BankSystem atmpage 1"
                        }
                    }, Layer + ".Info");
                else
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                            OffsetMin = "-55 25", OffsetMax = "55 60"
                        },
                        Text =
                        {
                            Text = Msg(player, InfoBackBtn),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Color = "1 1 1 1"
                        },
                        Button =
                        {
                            Color = HexToCuiColor("#4B68FF"),
                            Command = "UI_BankSystem atmpage 0"
                        }
                    }, Layer + ".Info");
            }

            #endregion

            #endregion
        }

        private void RepairUi(BasePlayer player)
        {
            ATMData atmData;
            if (!_atmByPlayer.TryGetValue(player, out atmData) || atmData == null)
                return;

            var needPercent = 100 - Mathf.RoundToInt(atmData.Condition);
            var allItems = player.inventory.AllItems();

            var container = new CuiElementContainer();

            #region Background

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image =
                {
                    Color = "0.19 0.19 0.18 0.3",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                CursorEnabled = true
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "" },
                Button =
                {
                    Color = "0 0 0 0",
                    Close = Layer,
                    Command = "UI_BankSystem atmpage 0 0 0 true"
                }
            }, Layer);

            #endregion

            #region Title

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-200 -210", OffsetMax = "200 -185"
                },
                Text =
                {
                    Text = Msg(player, RepairTitle),
                    Align = TextAnchor.LowerCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 24,
                    Color = "1 1 1 0.4"
                }
            }, Layer);

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-200 -245", OffsetMax = "200 -210"
                },
                Text =
                {
                    Text = Msg(player, RepairSecondTitle),
                    Align = TextAnchor.UpperCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 24,
                    Color = "1 1 1 1"
                }
            }, Layer);

            #endregion

            #region Items

            var Width = 130f;
            var Margin = 35f;

            var notItem = false;

            var xSwitch = -(_config.Atm.Repair.Items.Count * Width + (_config.Atm.Repair.Items.Count - 1) * Margin) /
                          2f;
            _config.Atm.Repair.Items.ForEach(item =>
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = $"{xSwitch} -450",
                        OffsetMax = $"{xSwitch + Width} -285"
                    },
                    Image =
                    {
                        Color = "0 0 0 0"
                    }
                }, Layer, Layer + $".Item.{xSwitch}");

                container.Add(new CuiElement
                {
                    Parent = Layer + $".Item.{xSwitch}",
                    Components =
                    {
                        new CuiRawImageComponent
                            { Png = ImageLibrary.Call<string>("GetImage", item.ShortName, item.Skin) },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-64 -128", OffsetMax = "64 0"
                        }
                    }
                });

                var amount = Mathf.CeilToInt(item.Amount * needPercent);

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0",
                        OffsetMin = "-100 10", OffsetMax = "100 30"
                    },
                    Text =
                    {
                        Text = Msg(player, RepairItemFormat, item.PublicTitle, amount),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 16,
                        Color = "1 1 1 0.5"
                    }
                }, Layer + $".Item.{xSwitch}");

                var hasAmount = HasAmount(allItems, item.ShortName, item.Skin, amount);

                if (!hasAmount)
                    notItem = true;

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "-50 0", OffsetMax = "50 3"
                    },
                    Image =
                    {
                        Color = hasAmount ? HexToCuiColor("#74884A") : HexToCuiColor("#CD4632"),
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    }
                }, Layer + $".Item.{xSwitch}");

                xSwitch += Width + Margin;
            });

            #endregion

            #region Buttons

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-120 -530",
                    OffsetMax = "-10 -485"
                },
                Text =
                {
                    Text = Msg(player, RepairButton),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Color = notItem ? "1 1 1 0.7" : "1 1 1 1"
                },
                Button =
                {
                    Color = notItem ? HexToCuiColor("#595651") : HexToCuiColor("#74884A"),
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                    Command = notItem ? "" : "UI_BankSystem atm_repair"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "10 -530",
                    OffsetMax = "120 -485"
                },
                Text =
                {
                    Text = Msg(player, RepairCancelButton),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Color = "1 1 1 0.7"
                },
                Button =
                {
                    Color = HexToCuiColor("#595651"),
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                    Close = Layer,
                    Command = "UI_BankSystem atmpage 0 0 0 true"
                }
            }, Layer);

            #endregion

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Utils

        private static string HexToCuiColor(string hex, float alpha = 100)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

            var str = hex.Trim('#');
            if (str.Length != 6) throw new Exception(hex);
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {alpha / 100f}";
        }

        private string GetCardNumber()
        {
            var number = "4";

            for (var i = 0; i < 15; i++) number += $"{Random.Range(0, 10)}";

            return number;
        }

        private string GetFormattedCardNumber(string number)
        {
            var result = string.Empty;

            var chars = number.ToCharArray();

            for (var i = 0; i < chars.Length; i++)
            {
                result += chars[i];

                if ((i + 1) % 4 == 0)
                    result += " ";
            }

            return result;
        }

        private string GetLastCardNumbers(BasePlayer player, string number)
        {
            return player == null || string.IsNullOrEmpty(number)
                ? string.Empty
                : Msg(player, CardFormat, number.Substring(12, 4));
        }

        #region Avatar

        private readonly Regex _regex = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");

        private void GetAvatar(ulong userId, Action<string> callback)
        {
            if (callback == null) return;

            webrequest.Enqueue($"http://steamcommunity.com/profiles/{userId}?xml=1", null, (code, response) =>
            {
                if (code != 200 || response == null)
                    return;

                var avatar = _regex.Match(response).Groups[1].ToString();
                if (string.IsNullOrEmpty(avatar))
                    return;

                callback.Invoke(avatar);
            }, this);
        }

        #endregion

        private static void CreateOutLine(ref CuiElementContainer container, string parent, string color,
            float size = 2)
        {
            container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"{size} 0",
                        OffsetMax = $"-{size} {size}"
                    },
                    Image = { Color = color }
                },
                parent);
            container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{size} -{size}",
                        OffsetMax = $"-{size} 0"
                    },
                    Image = { Color = color }
                },
                parent);
            container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "0 1",
                        OffsetMin = "0 0",
                        OffsetMax = $"{size} 0"
                    },
                    Image = { Color = color }
                },
                parent);
            container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "1 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"-{size} 0",
                        OffsetMax = "0 0"
                    },
                    Image = { Color = color }
                },
                parent);
        }

        private List<ulong> GetTopTransfers(BasePlayer player)
        {
            var logs = GetPlayerLogs(player);
            if (logs == null) return null;

            var topDict = new Dictionary<ulong, int>();

            logs.Transfers
                .FindAll(x => x.Type == Transaction.Transfer && x.TargetId.IsSteamId())
                .ForEach(transfer =>
                {
                    if (topDict.ContainsKey(transfer.TargetId))
                        topDict[transfer.TargetId] += 1;
                    else
                        topDict.Add(transfer.TargetId, 1);
                });

            return topDict.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
        }

        private void CreateCard(BasePlayer player)
        {
            if (player == null || Interface.CallHook("CanPlayerCreateCard", player) != null)
                return;

            if (HasCard(player))
            {
                SendNotify(player, AlreadyHaveCard, 1);
                return;
            }

            var data = GetPlayerData(player);
            if (data == null) return;

            data.Card = GetCardNumber();
            data.CardDate = DateTime.Now;

            if (_config.StartingBalance > 0)
                data.Balance = _config.StartingBalance;

            Interface.CallHook("OnPlayerCreatedCard", player);

            NextTick(() => SendNotify(player, BecameCardOwner, 0, data.Card));
        }

        private void RemoveCard(ulong member)
        {
            var data = GetPlayerData(member);
            if (data == null) return;

            data.Card = null;
        }

        private void CheckCard(ulong member)
        {
            var data = GetPlayerData(member);
            if (data == null) return;

            if (DateTime.Now.Subtract(data.CardDate).TotalDays > _config.CardExpiryDate)
                RemoveCard(member);
        }

        private string CardDateFormating(ulong member)
        {
            var data = GetPlayerData(member);
            if (data == null || string.IsNullOrEmpty(data.Card)) return string.Empty;

            return data.CardDate.AddDays(_config.CardExpiryDate).ToString("dd/MM");
        }

        private static bool HasAmount(Item[] items, string shortname, ulong skin, int amount)
        {
            return ItemCount(items, shortname, skin) >= amount;
        }

        private static int ItemCount(Item[] items, string shortname, ulong skin)
        {
            return items.Where(item =>
                    item.info.shortname == shortname && !item.isBroken && (skin == 0 || item.skin == skin))
                .Sum(item => item.amount);
        }

        private static void Take(IEnumerable<Item> itemList, string shortname, ulong skinId, int iAmount)
        {
            var num1 = 0;
            if (iAmount == 0) return;

            var list = Pool.GetList<Item>();

            foreach (var item in itemList)
            {
                if (item.info.shortname != shortname ||
                    skinId != 0 && item.skin != skinId || item.isBroken) continue;

                var num2 = iAmount - num1;
                if (num2 <= 0) continue;
                if (item.amount > num2)
                {
                    item.MarkDirty();
                    item.amount -= num2;
                    num1 += num2;
                    break;
                }

                if (item.amount <= num2)
                {
                    num1 += item.amount;
                    list.Add(item);
                }

                if (num1 == iAmount)
                    break;
            }

            foreach (var obj in list)
                obj.RemoveFromContainer();

            Pool.FreeList(ref list);
        }

        private void AddPlayerTracking(GatherLogType type, BasePlayer member, string shortName, int amount = 1)
        {
            if (!member.userID.IsSteamId() || string.IsNullOrEmpty(shortName) || !HasCard(member)) return;

            float cost;
            if (!_config.Tracking.CostTable.TryGetValue(shortName, out cost)) return;

            var price = Mathf.CeilToInt(cost * amount);

            GetPlayerLogs(member.userID)?.GatherLogs.Add(new GatherLogData
            {
                Type = type,
                ShortName = shortName,
                Amount = price
            });

            _config.Economy.AddBalance(member, price);
        }

        private void AddTransactionLog(Transaction type, ulong member, ulong target, int amount)
        {
            if (!member.IsSteamId()) return;

            GetPlayerLogs(member)?.Transfers.Add(new TransferData
            {
                Type = type,
                SenderId = member,
                TargetId = target,
                Amount = amount
            });
        }

        private void SpawnATMs()
        {
            if (_config.Atm.Spawn.Monuments.Count > 0)
                TerrainMeta.Path.Monuments.ForEach(monument =>
                {
                    if (monument == null || !_config.Atm.Spawn.Monuments.Any(x => monument.name.Contains(x.Key)))
                        return;

                    var conf = _config.Atm.Spawn.Monuments.FirstOrDefault(x => monument.name.Contains(x.Key)).Value;
                    if (conf == null || !conf.Enabled || conf.Position == Vector3.zero) return;

                    var transform = monument.transform;
                    var rot = transform.rotation;
                    var pos = transform.position + rot * conf.Position;

                    SpawnATM(pos, transform.localEulerAngles, conf);
                });
        }

        private void SpawnATM(Vector3 pos, Vector3 rot, ATMPosition conf)
        {
            var entity =
                GameManager.server.CreateEntity(VendingMachinePrefab, pos,
                    Quaternion.Euler(rot.x, rot.y + conf.Rotation, rot.z)) as VendingMachine;
            if (entity == null) return;

            entity.enableSaving = false;

            entity.skinID = _config.Atm.Skin;

            entity.GetComponent<DestroyOnGroundMissing>().enabled = false;
            entity.GetComponent<GroundWatch>().enabled = false;

            entity.Spawn();

            entity.shopName = conf.DisplayName;
            entity.UpdateMapMarker();

            _vendingMachines.Add(entity);

            var data = new ATMData
            {
                ID = 1,
                EntityID = entity.net.ID,
                OwnerId = 0,
                Balance = 0,
                Condition = 100,
                DepositFee = conf.DepositFee,
                WithdrawalFee = conf.WithdrawFee,
                IsAdmin = true
            };

            _atms.ATMs.Add(data);
        }

        private void LoadImages()
        {
            if (!ImageLibrary)
            {
                PrintWarning("IMAGE LIBRARY IS NOT INSTALLED!");
            }
            else
            {
                var imagesList = new Dictionary<string, string>();

                var itemIcons = new List<KeyValuePair<string, ulong>>();

                imagesList.Add(_config.CardImage, _config.CardImage);
                imagesList.Add(_config.DepositImage, _config.DepositImage);
                imagesList.Add(_config.WithdrawImage, _config.WithdrawImage);
                imagesList.Add(_config.TransferImage, _config.TransferImage);
                imagesList.Add(_config.ExitImage, _config.ExitImage);

                _config.Atm.Repair.Items.ForEach(item =>
                    itemIcons.Add(new KeyValuePair<string, ulong>(item.ShortName, item.Skin)));

                if (itemIcons.Count > 0) ImageLibrary?.Call("LoadImageList", Title, itemIcons, null);

                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
            }
        }

        private readonly Dictionary<string, string> ItemsTitles = new Dictionary<string, string>();

        private string GetItemTitle(string shortName)
        {
            string result;
            if (ItemsTitles.TryGetValue(shortName, out result))
                return result;

            result = ItemManager.FindItemDefinition(shortName)?.displayName.translated;

            if (!string.IsNullOrEmpty(result))
                ItemsTitles.Add(shortName, result);

            return result;
        }

        private void RenameATMs()
        {
            foreach (var entity in BaseNetworkable.serverEntities.OfType<VendingMachine>()
                .Where(x => x.skinID == _config.Atm.Skin))
            {
                if (entity == null) continue;

                var data = _atms.ATMs.Find(z => z.EntityID == entity.net.ID);
                if (data == null) continue;

                if (!string.IsNullOrEmpty(_config.Atm.ShopName))
                {
                    entity.shopName = _config.Atm.ShopName
                        .Replace("{id}", data.ID.ToString())
                        .Replace("{owner}", GetPlayerData(data.OwnerId)?.DisplayName);
                    entity.UpdateMapMarker();
                }
            }
        }

        #endregion

        #region API

        private bool HasCard(BasePlayer player)
        {
            return HasCard(player.userID);
        }

        private bool HasCard(ulong member)
        {
            var data = GetPlayerData(member);
            return data != null && !string.IsNullOrEmpty(data.Card);
        }

        private int Balance(BasePlayer player)
        {
            return Balance(player.userID);
        }

        private int Balance(string member)
        {
            return Balance(ulong.Parse(member));
        }

        private int Balance(ulong member)
        {
            return GetPlayerData(member)?.Balance ?? 0;
        }

        private bool Deposit(BasePlayer player, int amount)
        {
            return Deposit(player.userID, amount);
        }

        private bool Deposit(string member, int amount)
        {
            return Deposit(ulong.Parse(member), amount);
        }

        private bool Deposit(ulong member, int amount)
        {
            var data = GetPlayerData(member);
            if (data == null) return false;

            data.Balance += amount;

            Interface.CallHook("OnBalanceChanged", member, amount);
            return true;
        }

        private bool Withdraw(BasePlayer player, int amount)
        {
            return Withdraw(player.userID, amount);
        }

        private bool Withdraw(string member, int amount)
        {
            return Withdraw(ulong.Parse(member), amount);
        }

        private bool Withdraw(ulong member, int amount)
        {
            var data = GetPlayerData(member);
            if (data == null || data.Balance < amount)
                return false;

            data.Balance -= amount;

            Interface.CallHook("OnBalanceChanged", member, amount);
            return true;
        }

        private bool Transfer(BasePlayer member, BasePlayer target, int amount)
        {
            return Transfer(member.userID, target.userID, amount);
        }

        private bool Transfer(string member, string target, int amount)
        {
            return Transfer(ulong.Parse(member), ulong.Parse(target), amount);
        }

        private bool Transfer(ulong member, ulong target, int amount)
        {
            if (!Withdraw(member, amount)) return false;

            Deposit(target, amount);
            return true;
        }

        #endregion

        #region Lang

        private const string
            NoPermissions = "NoPermissions",
            CloseButton = "CloseButton",
            TitleMenu = "TitleMenu",
            BalanceTitle = "BalanceTitle",
            DepositIconTitle = "DepositIconTitle",
            DepositSymbolTitle = "DepositSymbolTitle",
            WithdrawalIconTitle = "WithdrawalIconTitle",
            WithdrawalSymbolTitle = "WithdrawalSymbolTitle",
            SelfTransferlIconTitle = "SelfTransferlIconTitle",
            SelfTransferSymbolTitle = "SelfTransferSymbolTitle",
            TransferlIconTitle = "TransferlIconTitle",
            TransferSymbolTitle = "TransferSymbolTitle",
            DepositOperationTitle = "DepositOperationTitle",
            WithdrawalOperationTitle = "WithdrawalOperationTitle",
            TransferOperationTitle = "TransferOperationTitle",
            DepositOperationDescription = "DepositOperationDescription",
            WithdrawalOperationDescription = "WithdrawalOperationDescription",
            SelfTransferOperationDescription = "SelfTransferOperationDescription",
            TransferOperationDescription = "TransferOperationDescription",
            OperationsValueFormat = "OperationsValueFormat",
            NotEnoughItems = "NotEnoughItems",
            NotEnoughMoney = "NotEnoughMoney",
            BrokenATM = "BrokenATM",
            NotBankCard = "NotBankCard",
            AlreadyHaveCard = "AlreadyHaveCard",
            BecameCardOwner = "BecameCardOwner",
            WelcomeTitle = "WelcomeTitle",
            MainTitle = "MainTitle",
            YourBalance = "YourBalance",
            CardBankTitle = "CardBankTitle",
            TransfersTitle = "TransfersTitle",
            FrequentTransfers = "FrequentTransfers",
            HaventTransactions = "HaventTransactions",
            TransferTitle = "TransferTitle",
            TransferByCard = "TransferByCard",
            CardNumberTitle = "CardNumberTitle",
            RemoveAmountTitle = "RemoveAmountTitle",
            TransactionHistory = "TransactionHistory",
            GatherHistory = "GatherHistory",
            MiningFee = "MiningFee",
            LootFee = "LootFee",
            MiningValue = "MiningValue",
            CreateCardTitle = "CreateCardTitle",
            SelectPlayerTitle = "SelectPlayerTitle",
            DepositTitle = "DepositTitle",
            DepositDescription = "DepositDescription",
            WithdrawTitle = "WithdrawTitle",
            WithdrawDescription = "WithdrawDescription",
            TransferDescription = "TransferDescription",
            ExitTitle = "ExitTile",
            ExitDescription = "ExitDescription",
            ATMProfit = "ATMProfit",
            ProfitValue = "ProfitValue",
            ATMCondition = "ATMCondition",
            ConditionValue = "ConditionValue",
            ATMRepair = "ATMRepair",
            DepositFeeTitle = "DepositFee",
            FeeValue = "FeeValue",
            WithdrawalFeeTitle = "WithdrawalFee",
            EnterAmount = "EnterAmount",
            TotalDeposit = "TotalDeposit",
            TotalValue = "TotalValue",
            TotalWithdraw = "TotalWithdraw",
            TotalTransfer = "TotalTransfer",
            RepairTitle = "RepairTitle",
            RepairSecondTitle = "RepairSecondTitle",
            RepairItemFormat = "RepairItemFormat",
            RepairButton = "RepairButton",
            RepairCancelButton = "RepairCancelButton",
            CardFormat = "CardFormat",
            InfoDepositTitle = "InfoDepositTitle",
            InfoWithdrawalTitle = "InfoWithdrawalTitle",
            InfoValueTitle = "InfoValueTitle",
            InfoManageBtn = "InfoManageBtn",
            InfoBackBtn = "InfoBackBtn",
            SelectPlayerSecond = "SelectPlayerSecond",
            ATMTitle = "ATMTitle",
            ATMAdminTitle = "ATMAdminTitle",
            ATMOwner = "ATMOwner",
            TransferedMoney = "TransferedMoney",
            DepositedMoney = "DepositedMoney",
            WithdrawnMoney = "WithdrawnMoney",
            AtmOwnWithdrawnMoney = "AtmOwnWithdrawnMoney";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [NoPermissions] = "You don't have permissions!",
                [CloseButton] = "✕",
                [TitleMenu] = "Bank",
                [BalanceTitle] = "{0}$",
                [DepositIconTitle] = "+",
                [DepositSymbolTitle] = "+",
                [WithdrawalIconTitle] = "—",
                [WithdrawalSymbolTitle] = "-",
                [SelfTransferlIconTitle] = "+",
                [SelfTransferSymbolTitle] = "+",
                [TransferlIconTitle] = "—",
                [TransferSymbolTitle] = "-",
                [DepositOperationTitle] = "Deposit",
                [WithdrawalOperationTitle] = "Withdrawal",
                [TransferOperationTitle] = "Transfer",
                [DepositOperationDescription] = "You have funded your bank account",
                [WithdrawalOperationDescription] = "You have withdrawn money from your bank account",
                [SelfTransferOperationDescription] = "{0} has transferred money to you",
                [TransferOperationDescription] = "You have transferred money to player {0}",
                [OperationsValueFormat] = "{0}{1}$",
                [NotEnoughItems] = "You don't have enough items!",
                [NotEnoughMoney] = "You don't have enough money!",
                [BrokenATM] = "ATM is broken!",
                [NotBankCard] = "You do not have a credit card",
                [AlreadyHaveCard] = "You already have a credit card!",
                [BecameCardOwner] = "Congratulations! You became the owner of the card {0}!",
                [WelcomeTitle] = "Welcome <b>{0}</b>",
                [MainTitle] = "RUST<color=#4B68FF>Bank</color>",
                [YourBalance] = "Your balance:",
                [CardBankTitle] = "RUSTBank",
                [TransfersTitle] = "Transfers",
                [FrequentTransfers] = "Recent transfers:",
                [HaventTransactions] = "You have no transactions yet :(",
                [TransferTitle] = "Transfer",
                [TransferByCard] = "Transfer by card:",
                [CardNumberTitle] = "     Card number",
                [RemoveAmountTitle] = "X",
                [TransactionHistory] = "Transactions history:",
                [GatherHistory] = "Gather history:",
                [MiningFee] = "{0} mining fee",
                [LootFee] = "{0} loot fee",
                [MiningValue] = "+{0}$",
                [CreateCardTitle] = "Create Card",
                [SelectPlayerTitle] = "Select player to transfer",
                [DepositTitle] = "Deposit",
                [DepositDescription] = "Deposit cash to your bank account",
                [WithdrawTitle] = "Withdraw",
                [WithdrawDescription] = "Withdraw cash to your balance",
                [TransferDescription] = "Transfer money to another player",
                [ExitTitle] = "Exit",
                [ExitDescription] = "Exit ATM",
                [ATMProfit] = "Profit by ATM",
                [ProfitValue] = "${0}",
                [ATMCondition] = "ATM Condition",
                [ConditionValue] = "{0}%",
                [ATMRepair] = "Repair",
                [DepositFeeTitle] = "Deposit fee:",
                [FeeValue] = "{0}%",
                [WithdrawalFeeTitle] = "Withdrawal fee:",
                [EnterAmount] = "Enter the amount:",
                [RepairTitle] = "TO REPAIR THE ATM",
                [RepairSecondTitle] = "YOU NEED",
                [RepairItemFormat] = "{0} ({1} pcs)",
                [RepairButton] = "REPAIR",
                [RepairCancelButton] = "CANCEL",
                [CardFormat] = "**** **** **** {0}",
                [InfoDepositTitle] = "Deposit:",
                [InfoValueTitle] = "{0}%",
                [InfoWithdrawalTitle] = "Withdrawal:",
                [InfoManageBtn] = "MANAGE",
                [InfoBackBtn] = "BACK",
                [TotalDeposit] = "Total for deposit:",
                [TotalValue] = "${0}",
                [TotalWithdraw] = "Total for withdraw:",
                [TotalTransfer] = "Total for transfer:",
                [SelectPlayerSecond] = "Select a player",
                [ATMTitle] = "ATM #{0}",
                [ATMAdminTitle] = "ATM",
                [ATMOwner] = "Owner:",
                [TransferedMoney] = "You have successfully transferred ${0} to player '{1}'",
                [DepositedMoney] = "You have successfully replenished your balance for ${0}!",
                [WithdrawnMoney] = "You have successfully withdrawn ${0}",
                [AtmOwnWithdrawnMoney] = "You have successfully withdrawn money from your ATM!",
                ["crate_elite"] = "Crate Elite",
                ["crate_normal"] = "Crate Normal"
            }, this);
        }

        private string Msg(BasePlayer player, string key, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
        }

        private void Reply(BasePlayer player, string key, params object[] obj)
        {
            SendReply(player, Msg(player, key, obj));
        }

        private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
        {
            if (Notify && _config.UseNotify)
                Notify?.Call("SendNotify", player, type, Msg(player, key, obj));
            else
                Reply(player, key, obj);
        }

        #endregion

        #region Convert

        #region Economics

        [ConsoleCommand("bank.convert.economics")]
        private void CmdConsoleConvertEconomics(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;

            ConvertFromEconomics();
        }

        private class EconomicsData
        {
            public readonly Dictionary<string, double> Balances = new Dictionary<string, double>();
        }

        private void ConvertFromEconomics()
        {
            EconomicsData data = null;

            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<EconomicsData>("Economics");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (data == null) return;

            var amount = 0;
            foreach (var check in data.Balances)
            {
                ulong member;
                if (!ulong.TryParse(check.Key, out member)) continue;

                var newBalance = Mathf.CeilToInt((float)check.Value);

                if (_data.Players.ContainsKey(member))
                {
                    GetPlayerData(member).Balance += newBalance;
                }
                else
                {
                    var playerData = GetPlayerData(member);
                    if (playerData != null)
                    {
                        playerData.Balance = newBalance;

                        var name = covalence.Players.FindPlayer(check.Key)?.Name;
                        if (!string.IsNullOrEmpty(name))
                            playerData.DisplayName = name;
                    }
                }

                amount++;
            }

            Puts($"{amount} players was converted!");
        }

        #endregion

        #region Server Rewards

        [ConsoleCommand("bank.convert.serverrewards")]
        private void CmdConsoleConvertServerRewards(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;

            ConvertFromServerRewards();
        }

        private class ServerRewardsData
        {
            public readonly Dictionary<ulong, int> playerRP = new Dictionary<ulong, int>();
        }

        private void ConvertFromServerRewards()
        {
            ServerRewardsData data = null;

            try
            {
                data =
                    Interface.Oxide.DataFileSystem.ReadObject<ServerRewardsData>("ServerRewards/player_data");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (data == null) return;

            var amount = 0;
            foreach (var check in data.playerRP)
            {
                if (_data.Players.ContainsKey(check.Key))
                {
                    GetPlayerData(check.Key).Balance += check.Value;
                }
                else
                {
                    var playerData = GetPlayerData(check.Key);
                    if (playerData != null)
                    {
                        playerData.Balance = check.Value;

                        var name = covalence.Players.FindPlayer(check.Key.ToString())?.Name;
                        if (!string.IsNullOrEmpty(name))
                            playerData.DisplayName = name;
                    }
                }

                amount++;
            }

            Puts($"{amount} players was converted!");
        }

        #endregion

        #endregion
    }
}