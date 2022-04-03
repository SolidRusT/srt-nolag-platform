using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Shop", "Mevent", "1.0.28")]
    public class Shop : RustPlugin
    {
        #region Fields

        [PluginReference]
        private Plugin ImageLibrary, ItemCostCalculator, HumanNPC, Notify, UINotify, NoEscape, Duel, Duelist;

        private static Shop _instance;

        private readonly Dictionary<int, ShopItem> _shopItems = new Dictionary<int, ShopItem>();

        private readonly Dictionary<BasePlayer, Coroutine> _coroutines = new Dictionary<BasePlayer, Coroutine>();

        private readonly Dictionary<string, List<string>> _itemsCategories =
            new Dictionary<string, List<string>>();

        private readonly List<string> _images = new List<string>();

        private const string Layer = "UI.Shop";

        private const string ModalLayer = "UI.Shop.Modal";

        private const string EditingLayer = "UI.Shop.Editing";

        private const string PermAdmin = "Shop.admin";

        private const string PermFreeBypass = "Shop.free";

        private readonly Dictionary<ulong, DataCart> _carts = new Dictionary<ulong, DataCart>();

        private class DataCart
        {
            public readonly Dictionary<ShopItem, int> Items = new Dictionary<ShopItem, int>();

            public void AddItem(ShopItem item, BasePlayer player)
            {
                int result;
                int amount;
                if (Items.TryGetValue(item, out amount))
                {
                    if (item.BuyMaxAmount > 0 && amount >= item.BuyMaxAmount) return;

                    if (!CanAdd(player, item, amount + 1, out result))
                    {
                        _instance.SendNotify(player, result == 1 ? BuyLimitReached : DailyBuyLimitReached, 1,
                            item.PublicTitle);
                        return;
                    }

                    Items[item]++;
                }
                else
                {
                    if (!CanAdd(player, item, 1, out result))
                    {
                        _instance.SendNotify(player, result == 1 ? BuyLimitReached : DailyBuyLimitReached, 1,
                            item.PublicTitle);
                        return;
                    }

                    Items.Add(item, 1);
                }
            }

            private bool CanAdd(BasePlayer player, ShopItem item, int amount, out int result)
            {
                int leftLimit;
                if (HasLimit(player, item, true, out leftLimit) && amount >= leftLimit) //total Limit
                {
                    result = 1;
                    return false;
                }

                if (HasLimit(player, item, true, out leftLimit, true) && amount > leftLimit) //daily Limit
                {
                    result = 2;
                    return false;
                }

                result = 0;
                return true;
            }

            public void RemoveItem(ShopItem item)
            {
                Items.Remove(item);
            }

            public void ChangeAmountItem(BasePlayer player, ShopItem item, int amount)
            {
                if (amount > 0)
                {
                    int totalLimit;
                    if (HasLimit(player, item, true, out totalLimit) && amount >= totalLimit)
                        amount = Math.Min(totalLimit, amount);

                    int dailyLimit;
                    if (HasLimit(player, item, true, out dailyLimit, true) && amount >= dailyLimit)
                        amount = Math.Min(dailyLimit, amount);

                    if (amount <= 0) return;

                    Items[item] = amount;
                }
                else
                {
                    Items.Remove(item);
                }
            }

            public int GetAmount()
            {
                return Items.Sum(x => x.Key.Amount * x.Value);
            }

            public double GetPrice(BasePlayer player)
            {
                return Items.Sum(x => x.Key.GetPrice(player) * x.Value);
            }

            public int GetTotalAmount()
            {
                return (from check in Items
                    let shopItem = check.Key
                    where shopItem.Type == ItemType.Item && shopItem.Definition != null
                    select shopItem.GetStacks(shopItem.Definition, shopItem.Amount * check.Value).Count).Sum();
            }

            #region Data

            public PlayerCart ToPlayerCart()
            {
                return new PlayerCart
                {
                    Items = Items.ToDictionary(x => x.Key.ID, y => y.Value)
                };
            }

            public DataCart()
            {
            }

            public DataCart(PlayerCart cart)
            {
                Items = cart.Items.ToDictionary(x => _instance.FindItemById(x.Key), y => y.Value);
            }

            #endregion
        }

        private readonly Dictionary<BasePlayer, NPCShop> _openedShops = new Dictionary<BasePlayer, NPCShop>();

        private NPCShop GetShopByPlayer(BasePlayer player)
        {
            NPCShop shop;
            return _openedShops.TryGetValue(player, out shop) ? shop : null;
        }

        private readonly Dictionary<BasePlayer, Dictionary<string, object>> _itemEditing =
            new Dictionary<BasePlayer, Dictionary<string, object>>();

        #endregion

        #region Colors

        private string _firstColor;
        private string _secondColor;
        private string _thirdColor;
        private string _fourthColor;
        private string _fifthColor;
        private string _sixthColor;
        private string _seventhColor;

        #endregion

        #region Config

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly string[] Commands = {"shop", "shops"};

            [JsonProperty(PropertyName = "Enable logging to the console?")]
            public readonly bool LogToConsole = true;

            [JsonProperty(PropertyName = "Enable logging to the file?")]
            public readonly bool LogToFile = true;

            [JsonProperty(PropertyName = "Load images when logging into the server?")]
            public readonly bool LoginImages = true;

            [JsonProperty(PropertyName = "Work with Notify?")]
            public readonly bool UseNotify = true;

            [JsonProperty(PropertyName = "Can admins edit? (by flag)")]
            public readonly bool FlagAdmin = true;

            [JsonProperty(PropertyName = "Block (NoEscape)")]
            public bool BlockNoEscape;

            [JsonProperty(PropertyName = "Blocking the opening in duels?")]
            public bool UseDuels;

            [JsonProperty(PropertyName = "Delay between loading images")]
            public readonly float ImagesDelay = 1f;

            [JsonProperty(PropertyName = "Economy")]
            public readonly EconomyConf Economy = new EconomyConf
            {
                Type = EconomyType.Plugin,
                AddHook = "Deposit",
                BalanceHook = "Balance",
                RemoveHook = "Withdraw",
                Plug = "Economics",
                ShortName = "scrap",
                DisplayName = string.Empty,
                Skin = 0
            };

            [JsonProperty(PropertyName = "Shop", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<ShopCategory> Shop = new List<ShopCategory>();

            [JsonProperty(PropertyName = "First Color")]
            public readonly string FirstColor = "#161617";

            [JsonProperty(PropertyName = "Second Color")]
            public readonly string SecondColor = "#4B68FF";

            [JsonProperty(PropertyName = "Third Color")]
            public readonly string ThirdColor = "#0E0E10";

            [JsonProperty(PropertyName = "Fourth Color")]
            public readonly string FourthColor = "#A0A935";

            [JsonProperty(PropertyName = "Fifth Color")]
            public readonly string FifthColor = "#FF4B4B";

            [JsonProperty(PropertyName = "Sixth Color")]
            public readonly string SixthColor = "#324192";

            [JsonProperty(PropertyName = "Seventh Color")]
            public readonly string SeventhColor = "#CD3838";

            [JsonProperty(PropertyName = "NPC Shops (NPC ID - shop categories)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly Dictionary<string, NPCShop> NPCs = new Dictionary<string, NPCShop>
            {
                ["1234567"] = new NPCShop
                {
                    Shops = new List<string>
                    {
                        "Tool",
                        "Food"
                    }
                },
                ["7654321"] = new NPCShop
                {
                    Shops = new List<string>
                    {
                        "Weapon",
                        "Ammunition"
                    }
                },
                ["4644687478"] = new NPCShop
                {
                    Shops = new List<string>
                    {
                        "*"
                    }
                }
            };

            [JsonProperty(PropertyName = "Interface")]
            public readonly UserInterface UI = new UserInterface
            {
                DisplayType = "Overlay",
                Width = 770,
                Height = 500,
                CategoriesOnString = 9,
                CategoriesMargin = 7.5f,
                CategoriesHeight = 40,
                ItemsOnString = 4,
                Strings = 2,
                ItemWidth = 150,
                ItemHeight = 165,
                Margin = 35,
                UseScrollCategories = true,
                EnableSearch = true,
                RoundDigits = 5
            };

            [JsonProperty(PropertyName = "Blocked skins for sell",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly Dictionary<string, List<ulong>> BlockedSkins = new Dictionary<string, List<ulong>>
            {
                ["short name"] = new List<ulong>
                {
                    52,
                    25
                },

                ["short name 2"] = new List<ulong>
                {
                    52,
                    25
                }
            };

            [JsonProperty(PropertyName = "Auto-Wipe Settings")]
            public WipeSettings Wipe = new WipeSettings
            {
                Cooldowns = true,
                Players = true,
                Limits = true
            };

            public VersionNumber Version;
        }

        private class WipeSettings
        {
            [JsonProperty(PropertyName = "Wipe Cooldowns?")]
            public bool Cooldowns;

            [JsonProperty(PropertyName = "Wipe Players?")]
            public bool Players;

            [JsonProperty(PropertyName = "Wipe Limits?")]
            public bool Limits;
        }

        private class UserInterface
        {
            [JsonProperty(PropertyName = "Display type (Overlay/Hud)")]
            public string DisplayType;

            [JsonProperty(PropertyName = "Height")]
            public float Height;

            [JsonProperty(PropertyName = "Width")] public float Width;

            [JsonProperty(PropertyName = "Categories On Page")]
            public int CategoriesOnString;

            [JsonProperty(PropertyName = "Categories Margin")]
            public float CategoriesMargin;

            [JsonProperty(PropertyName = "Categories Height")]
            public float CategoriesHeight;

            [JsonProperty(PropertyName = "Items On String")]
            public int ItemsOnString;

            [JsonProperty(PropertyName = "Strings")]
            public int Strings;

            [JsonProperty(PropertyName = "Item Height")]
            public float ItemHeight;

            [JsonProperty(PropertyName = "Item Width")]
            public float ItemWidth;

            [JsonProperty(PropertyName = "Margin")]
            public float Margin;

            [JsonProperty(PropertyName = "Use scroll in categories?")]
            public bool UseScrollCategories;

            [JsonProperty(PropertyName = "Enable search?")]
            public bool EnableSearch;

            [JsonProperty(PropertyName = "Number of digits after decimal point for rounding prices")]
            public int RoundDigits;
        }

        private class NPCShop
        {
            [JsonProperty(PropertyName = "Categories (Titles) [* - all]",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Shops;
        }

        private class ShopCategory
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = "Title")] public string Title;

            [JsonProperty(PropertyName = "Permission")]
            public string Permission;

            [JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ShopItem> Items;
        }

        private enum ItemType
        {
            Item,
            Command,
            Plugin,
            Kit
        }

        private class ShopItem
        {
            [JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
            public ItemType Type;

            [JsonProperty(PropertyName = "ID")] public int ID;

            [JsonProperty(PropertyName = "Image")] public string Image;

            [JsonProperty(PropertyName = "Title")] public string Title;

            [JsonProperty(PropertyName = "Description")]
            public string Description;

            [JsonProperty(PropertyName = "Command (%steamid%)")]
            public string Command;

            [JsonProperty(PropertyName = "Kit")] public string Kit;

            [JsonProperty(PropertyName = "Plugin")]
            public PluginItem Plugin;

            [JsonProperty(PropertyName = "DisplayName (empty - default)")]
            public string DisplayName;

            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName;

            [JsonProperty(PropertyName = "Skin")] public ulong Skin;

            [JsonProperty(PropertyName = "Amount")]
            public int Amount;

            [JsonProperty(PropertyName = "Price")] public double Price;

            [JsonProperty(PropertyName = "Sell Price")]
            public double SellPrice;

            [JsonProperty(PropertyName = "Buy Cooldown (0 - disable)")]
            public float BuyCooldown;

            [JsonProperty(PropertyName = "Sell Cooldown (0 - disable)")]
            public float SellCooldown;

            [JsonProperty(PropertyName = "Discount (%)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> Discount = new Dictionary<string, int>();

            [JsonProperty(PropertyName = "Sell Limits (0 - no limit)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> SellLimits = new Dictionary<string, int>();

            [JsonProperty(PropertyName = "Buy Limits (0 - no limit)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> BuyLimits = new Dictionary<string, int>();

            [JsonProperty(PropertyName = "Daily Buy Limits (0 - no limit)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> DailyBuyLimits = new Dictionary<string, int>();

            [JsonProperty(PropertyName = "Daily Sell Limits (0 - no limit)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> DailySellLimits = new Dictionary<string, int>();

            [JsonProperty(PropertyName = "Max Buy Amount (0 - disable)")]
            public int BuyMaxAmount;

            [JsonProperty(PropertyName = "Max Sell Amount (0 - disable)")]
            public int SellMaxAmount;

            [JsonIgnore] private string _publicTitle;

            [JsonIgnore]
            public string PublicTitle
            {
                get
                {
                    if (string.IsNullOrEmpty(_publicTitle))
                        _publicTitle = GetName();

                    return _publicTitle;
                }
            }

            private string GetName()
            {
                if (!string.IsNullOrEmpty(Title))
                    return Title;

                if (!string.IsNullOrEmpty(DisplayName))
                    return DisplayName;

                var def = ItemManager.FindItemDefinition(ShortName);
                if (!string.IsNullOrEmpty(ShortName) && def != null)
                    return def.displayName.translated;

                return string.Empty;
            }

            [JsonIgnore] public ItemDefinition Definition;

            public Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    ["Generated"] = false,
                    ["ID"] = ID,
                    ["Type"] = Type,
                    ["Image"] = Image,
                    ["Title"] = Title,
                    ["Command"] = Command,
                    ["DisplayName"] = DisplayName,
                    ["ShortName"] = ShortName,
                    ["Skin"] = Skin,
                    ["Amount"] = Amount,
                    ["Price"] = Price,
                    ["SellPrice"] = SellPrice,
                    ["Plugin_Hook"] = Plugin.Hook,
                    ["Plugin_Name"] = Plugin.Plugin,
                    ["Plugin_Amount"] = Plugin.Amount
                };
            }

            public ShopItem()
            {
            }

            public ShopItem(Dictionary<string, object> dictionary)
            {
                ID = (int) dictionary["ID"];
                Type = (ItemType) dictionary["Type"];
                Image = (string) dictionary["Image"];
                Title = (string) dictionary["Title"];
                Command = (string) dictionary["Command"];
                DisplayName = (string) dictionary["DisplayName"];
                ShortName = (string) dictionary["ShortName"];
                Skin = (ulong) dictionary["Skin"];
                Amount = (int) dictionary["Amount"];
                Price = (double) dictionary["Price"];
                SellPrice = (double) dictionary["SellPrice"];
                Plugin = new PluginItem
                {
                    Hook = (string) dictionary["Plugin_Hook"],
                    Plugin = (string) dictionary["Plugin_Name"],
                    Amount = (int) dictionary["Plugin_Amount"]
                };
                Discount = new Dictionary<string, int>();
            }

            public double GetPrice(BasePlayer player)
            {
                var discount = GetDiscount(player);

                var price = Price;

                return Math.Round(discount != 0 ? price * (1f - discount / 100f) : price,
                    _instance._config.UI.RoundDigits);
            }

            public int GetDiscount(BasePlayer player)
            {
                return (from check in Discount
                    where _instance.permission.UserHasPermission(player.UserIDString, check.Key)
                    select check.Value).Prepend(0).Max();
            }

            public int GetLimit(BasePlayer player, bool buy = true, bool daily = false)
            {
                var dict = daily ? buy ? DailyBuyLimits : DailySellLimits
                    : buy ? BuyLimits : SellLimits;

                if (dict.Count == 0)
                    return 0;

                return (from check in dict
                    where _instance.permission.UserHasPermission(player.UserIDString, check.Key)
                    select check.Value).Prepend(0).Max();
            }

            public void Get(BasePlayer player, int count = 1)
            {
                switch (Type)
                {
                    case ItemType.Item:
                        ToItem(player, count);
                        break;
                    case ItemType.Command:
                        ToCommand(player, count);
                        break;
                    case ItemType.Plugin:
                        Plugin.Get(player, count);
                        break;
                    case ItemType.Kit:
                        ToKit(player, count);
                        break;
                }
            }

            private void ToKit(BasePlayer player, int count)
            {
                if (string.IsNullOrEmpty(Kit)) return;

                for (var i = 0; i < count; i++)
                    Interface.Oxide.CallHook("GiveKit", player, Kit);
            }

            private void ToItem(BasePlayer player, int count)
            {
                var def = ItemManager.FindItemDefinition(ShortName);
                if (def == null)
                {
                    Debug.LogError($"Error creating item with ShortName '{ShortName}'");
                    return;
                }

                GetStacks(def, Amount * count)?.ForEach(stack =>
                {
                    var newItem = ItemManager.Create(def, stack, Skin);
                    if (newItem == null)
                    {
                        _instance?.PrintError($"Error creating item with ShortName '{ShortName}'");
                        return;
                    }

                    if (!string.IsNullOrEmpty(DisplayName)) newItem.name = DisplayName;

                    player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
                });
            }

            private void ToCommand(BasePlayer player, int count)
            {
                for (var i = 0; i < count; i++)
                {
                    var command = Command.Replace("\n", "|")
                        .Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace(
                            "%username%",
                            player.displayName, StringComparison.OrdinalIgnoreCase);

                    foreach (var check in command.Split('|')) _instance?.Server.Command(check);
                }
            }

            public List<int> GetStacks(ItemDefinition item, int amount)
            {
                var list = new List<int>();
                var maxStack = item.stackable;

                if (maxStack == 0) maxStack = 1;

                while (amount > maxStack)
                {
                    amount -= maxStack;
                    list.Add(maxStack);
                }

                list.Add(amount);

                return list;
            }

            public override string ToString()
            {
                switch (Type)
                {
                    case ItemType.Item:
                        return $"[ITEM-{ID}] {ShortName}x{Amount}(DN: {DisplayName}, SKIN: {Skin})";
                    case ItemType.Command:
                        return $"[COMMAND-{ID}] {Command}";
                    case ItemType.Plugin:
                        return
                            $"[PLUGIN-{ID}] Name: {Plugin?.Plugin}, Hook: {Plugin?.Hook}, Amount: {Plugin?.Amount ?? 0}";
                    case ItemType.Kit:
                        return $"[KIT-{ID}] {Kit}";
                    default:
                        return base.ToString();
                }
            }
        }

        private class PluginItem
        {
            [JsonProperty(PropertyName = "Hook")] public string Hook;

            [JsonProperty(PropertyName = "Plugin Name")]
            public string Plugin;

            [JsonProperty(PropertyName = "Amount")]
            public int Amount;

            public void Get(BasePlayer player, int count = 1)
            {
                var plug = _instance?.plugins.Find(Plugin);
                if (plug == null)
                {
                    _instance?.PrintError($"Plugin '{Plugin}' not found !!! ");
                    return;
                }

                switch (Plugin)
                {
                    case "Economics":
                    {
                        plug.Call(Hook, player.userID, (double) Amount * count);
                        break;
                    }
                    default:
                    {
                        plug.Call(Hook, player.userID, Amount * count);
                        break;
                    }
                }
            }
        }

        private enum EconomyType
        {
            Plugin,
            Item
        }

        private class EconomyConf
        {
            [JsonProperty(PropertyName = "Type (Plugin/Item)")] [JsonConverter(typeof(StringEnumConverter))]
            public EconomyType Type;

            [JsonProperty(PropertyName = "Plugin name")]
            public string Plug;

            [JsonProperty(PropertyName = "Balance add hook")]
            public string AddHook;

            [JsonProperty(PropertyName = "Balance remove hook")]
            public string RemoveHook;

            [JsonProperty(PropertyName = "Balance show hook")]
            public string BalanceHook;

            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName;

            [JsonProperty(PropertyName = "Display Name (empty - default)")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Skin")] public ulong Skin;

            public double ShowBalance(BasePlayer player)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        var plugin = _instance?.plugins?.Find(Plug);
                        if (plugin == null) return 0;

                        return Math.Round(Convert.ToDouble(plugin.Call(BalanceHook, player.userID)), 2);
                    }
                    case EconomyType.Item:
                    {
                        return ItemCount(player.inventory.AllItems(), ShortName, Skin);
                    }
                    default:
                        return 0;
                }
            }

            public void AddBalance(BasePlayer player, double amount)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        var plugin = _instance?.plugins?.Find(Plug);
                        if (plugin == null) return;

                        switch (Plug)
                        {
                            case "BankSystem":
                            case "ServerRewards":
                                plugin.Call(AddHook, player.userID, (int) amount);
                                break;
                            default:
                                plugin.Call(AddHook, player.userID, amount);
                                break;
                        }

                        break;
                    }
                    case EconomyType.Item:
                    {
                        var am = (int) amount;

                        var item = ToItem(am);
                        if (item == null) return;

                        player.GiveItem(item);
                        break;
                    }
                }
            }

            public bool RemoveBalance(BasePlayer player, double amount)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        if (ShowBalance(player) < amount) return false;

                        var plugin = _instance?.plugins.Find(Plug);
                        if (plugin == null) return false;

                        switch (Plug)
                        {
                            case "BankSystem":
                            case "ServerRewards":
                                plugin.Call(RemoveHook, player.userID, (int) amount);
                                break;
                            default:
                                plugin.Call(RemoveHook, player.userID, amount);
                                break;
                        }

                        return true;
                    }
                    case EconomyType.Item:
                    {
                        var playerItems = player.inventory.AllItems();
                        var am = (int) amount;

                        if (ItemCount(playerItems, ShortName, Skin) < am) return false;

                        Take(playerItems, ShortName, Skin, am);
                        return true;
                    }
                    default:
                        return false;
                }
            }

            private Item ToItem(int amount)
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

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();

                if (_config.Version < Version)
                    UpdateConfigValues();

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

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            var baseConfig = new Configuration();

            if (_config.Version == default(VersionNumber) && _config.Version < new VersionNumber(1, 0, 21))
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
                if (_data != null)
                    Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Players", _data);
            }

            if (_config.Version < new VersionNumber(1, 0, 21))
                _config.Shop.ForEach(shop =>
                {
                    shop.Items.ForEach(item =>
                    {
                        item.SellLimits = new Dictionary<string, int>
                        {
                            ["shop.default"] = 0,
                            ["shop.vip"] = 0
                        };
                        item.BuyLimits = new Dictionary<string, int>
                        {
                            ["shop.default"] = 0,
                            ["shop.vip"] = 0
                        };
                        item.DailyBuyLimits = new Dictionary<string, int>
                        {
                            ["shop.default"] = 0,
                            ["shop.vip"] = 0
                        };
                        item.DailySellLimits = new Dictionary<string, int>
                        {
                            ["shop.default"] = 0,
                            ["shop.vip"] = 0
                        };
                    });
                });

            if (_config.Version < new VersionNumber(1, 0, 24)) _config.UI.DisplayType = baseConfig.UI.DisplayType;

            _config.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data

        private PluginData _data;

        private void SaveData()
        {
            SavePlayers();

            SaveCooldown();

            SaveLimits();
        }

        private void SavePlayers()
        {
            _data.PlayerCarts.Clear();

            foreach (var check in _carts)
                _data.PlayerCarts.Add(check.Key, check.Value.ToPlayerCart());

            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Players", _data);
        }

        private void SaveCooldown()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Cooldown", _cooldown);
        }

        private void SaveLimits()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Limits", _limits);
        }

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>($"{Name}/Players");
                _cooldown = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Cooldown>>($"{Name}/Cooldown");
                _limits = Interface.Oxide.DataFileSystem.ReadObject<PlayerLimits>($"{Name}/Limits");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
            if (_cooldown == null) _cooldown = new Dictionary<ulong, Cooldown>();
            if (_limits == null) _limits = new PlayerLimits();
        }

        private class PluginData
        {
            [JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly Dictionary<ulong, PlayerCart> PlayerCarts = new Dictionary<ulong, PlayerCart>();
        }

        private class PlayerCart
        {
            [JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<int, int> Items = new Dictionary<int, int>();
        }

        #endregion

        #region Hooks

        private void Init()
        {
            _instance = this;

            LoadData();

            if (!_config.LoginImages)
                Unsubscribe(nameof(OnPlayerConnected));

            _firstColor = HexToCuiColor(_config.FirstColor);
            _secondColor = HexToCuiColor(_config.SecondColor);
            _thirdColor = HexToCuiColor(_config.ThirdColor);
            _fourthColor = HexToCuiColor(_config.FourthColor);
            _fifthColor = HexToCuiColor(_config.FifthColor);
            _sixthColor = HexToCuiColor(_config.SixthColor);
            _seventhColor = HexToCuiColor(_config.SeventhColor);
        }

        private void OnServerInitialized()
        {
            if (_config.Shop.Count == 0)
                FillCategories();

            LoadImages();

            _config.Shop.ForEach(category =>
            {
                if (!string.IsNullOrEmpty(category.Permission) && !permission.PermissionExists(category.Permission))
                    permission.RegisterPermission(category.Permission, this);
            });

            permission.RegisterPermission(PermAdmin, this);
            permission.RegisterPermission(PermFreeBypass, this);

            CheckOnDuplicates();

            ItemsToDict();

            foreach (var image in _shopItems.Values
                         .Select(shopItem =>
                             !string.IsNullOrEmpty(shopItem.Image) ? shopItem.Image : shopItem.ShortName)
                         .Where(image => !_images.Contains(image))) _images.Add(image);

            foreach (var check in _data.PlayerCarts)
                _carts.Add(check.Key, new DataCart(check.Value));

            if (_config.LoginImages)
                foreach (var player in BasePlayer.activePlayerList)
                    OnPlayerConnected(player);

            AddCovalenceCommand(_config.Commands, nameof(CmdShopOpen));

            if (_shopItems.Any(x => x.Value.BuyCooldown > 0 || x.Value.SellCooldown > 0))
                timer.Every(1, ItemsUpdateController);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, ModalLayer);
                CuiHelper.DestroyUi(player, EditingLayer);

                OnPlayerDisconnected(player);
            }

            SaveData();

            _instance = null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            var coroutine = ServerMgr.Instance.StartCoroutine(LoadImages(player));

            if (_coroutines.ContainsKey(player))
                _coroutines[player] = coroutine;
            else
                _coroutines.Add(player, coroutine);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;

            _itemsToUpdate.Remove(player);

            if (_itemEditing.ContainsKey(player)) _itemEditing.Remove(player);

            if (_coroutines.ContainsKey(player))
            {
                var coroutine = _coroutines[player];
                if (coroutine != null)
                    ServerMgr.Instance.StopCoroutine(coroutine);
            }
        }

        private void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (npc == null || player == null) return;

            NPCShop npcShop;
            if (!_config.NPCs.TryGetValue(npc.UserIDString, out npcShop) || npcShop == null) return;

            _openedShops[player] = npcShop;

            MainUi(player, npcShop: npcShop, first: true);
        }

        private void OnNewSave(string filename)
        {
            if (_config.Wipe.Players)
                _data.PlayerCarts.Clear();

            if (_config.Wipe.Cooldowns)
                _cooldown.Clear();

            if (_config.Wipe.Limits)
                _limits.Players.Clear();

            SaveData();
        }

        #endregion

        #region Commands

        private void CmdShopOpen(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (_config.UseDuels && InDuel(player))
            {
                SendNotify(player, NoUseDuel, 1);
                return;
            }

            MainUi(player, first: true);
        }

        [ConsoleCommand("UI_Shop")]
        private void CmdConsoleShop(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs()) return;

            switch (arg.Args[0])
            {
                case "closeui":
                {
                    _itemsToUpdate.Remove(player);
                    _openedShops.Remove(player);
                    break;
                }

                case "main_page":
                {
                    int catPage, page;
                    if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out catPage) ||
                        !int.TryParse(arg.Args[2], out page)) return;

                    var search = string.Empty;
                    if (arg.HasArgs(4)) search = string.Join(" ", arg.Args.Skip(3));

                    if (string.IsNullOrEmpty(search) && catPage == -1)
                        catPage = 0;

                    MainUi(player, catPage, page, GetShopByPlayer(player), search);
                    break;
                }

                case "buyitem":
                {
                    int id;
                    if (!arg.HasArgs(2) ||
                        !int.TryParse(arg.Args[1], out id)) return;

                    var shopItem = FindItemById(id);
                    if (shopItem == null) return;

                    var playerCart = GetPlayerCart(player);
                    if (playerCart == null) return;

                    playerCart.AddItem(shopItem, player);

                    var cooldownTime = GetCooldownTime(player.userID, shopItem, true);
                    if (cooldownTime > 0)
                    {
                        SendNotify(player, BuyCooldownMessage, 1, shopItem.PublicTitle,
                            FormatShortTime(player, TimeSpan.FromSeconds(cooldownTime)));
                        return;
                    }

                    var container = new CuiElementContainer();
                    RefreshCart(ref container, player, playerCart);
                    CuiHelper.AddUi(player, container);
                    break;
                }

                case "changeicat":
                {
                    int catPage, iCategory;
                    if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out catPage) ||
                        !int.TryParse(arg.Args[2], out iCategory)) return;

                    var container = new CuiElementContainer();
                    RefreshCategories(ref container, player, GetCategories(player, GetShopByPlayer(player)), catPage,
                        iCategory);
                    CuiHelper.AddUi(player, container);
                    break;
                }

                case "gocat":
                {
                    int catPage, iCategory;
                    if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out catPage) ||
                        !int.TryParse(arg.Args[2], out iCategory)) return;

                    MainUi(player, catPage, npcShop: GetShopByPlayer(player));

                    var container = new CuiElementContainer();
                    RefreshCategories(ref container, player, GetCategories(player, GetShopByPlayer(player)), catPage,
                        iCategory);
                    CuiHelper.AddUi(player, container);
                    break;
                }

                case "cart_page":
                {
                    int page;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

                    var playerCart = GetPlayerCart(player);
                    if (playerCart == null) return;

                    var container = new CuiElementContainer();
                    RefreshCart(ref container, player, playerCart, page);
                    CuiHelper.AddUi(player, container);
                    break;
                }

                case "cart_item_remove":
                {
                    int id;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out id)) return;

                    var shopItem = FindItemById(id);
                    if (shopItem == null) return;

                    var playerCart = GetPlayerCart(player);
                    if (playerCart == null) return;

                    playerCart.RemoveItem(shopItem);

                    var container = new CuiElementContainer();
                    RefreshCart(ref container, player, playerCart);
                    CuiHelper.AddUi(player, container);
                    break;
                }

                case "cart_item_change":
                {
                    int id, amount;
                    if (!arg.HasArgs(3) ||
                        !int.TryParse(arg.Args[1], out id) ||
                        !int.TryParse(arg.Args[2], out amount)) return;

                    var shopItem = FindItemById(id);
                    if (shopItem == null) return;

                    var playerCart = GetPlayerCart(player);
                    if (playerCart == null) return;

                    if (shopItem.BuyMaxAmount > 0 && amount > shopItem.BuyMaxAmount)
                        amount = shopItem.BuyMaxAmount;


                    /*
                    var limit = Mathf.Min(GetLimit(player, shopItem, true),
                        GetLimit(player, shopItem, true, true));
                    
                    amount = Mathf.Min(limit, amount);
                    
                    if (amount <= 0) return;
                    */

                    playerCart.ChangeAmountItem(player, shopItem, amount);

                    var container = new CuiElementContainer();
                    RefreshCart(ref container, player, playerCart);
                    CuiHelper.AddUi(player, container);
                    break;
                }

                case "cart_try_buyitems":
                {
                    AcceptBuy(player);
                    break;
                }

                case "cart_buyitems":
                {
                    var playerCart = GetPlayerCart(player);
                    if (playerCart == null) return;

                    var price = playerCart.GetPrice(player);
                    if (price <= 0) return;

                    if (_config.BlockNoEscape && NoEscape != null)
                    {
                        var success = NoEscape?.Call("IsBlocked", player);
                        if (success is bool && (bool) success)
                        {
                            ErrorUi(player, Msg(player, BuyRaidBlocked));
                            return;
                        }
                    }

                    var totalcount = playerCart.GetTotalAmount();

                    var slots = player.inventory.containerBelt.capacity -
                                player.inventory.containerBelt.itemList.Count +
                                (player.inventory.containerMain.capacity -
                                 player.inventory.containerMain.itemList.Count);
                    if (slots < totalcount)
                    {
                        ErrorUi(player, Msg(player, NotEnoughtSpace));
                        return;
                    }

                    if (!permission.UserHasPermission(player.UserIDString, PermFreeBypass) &&
                        !_config.Economy.RemoveBalance(player, price))
                    {
                        ErrorUi(player, Msg(player, NotMoney));
                        return;
                    }

                    if (playerCart.Items.Any(x =>
                        {
                            var limit = GetLimit(player, x.Key, true);
                            if (limit <= 0)
                            {
                                ErrorUi(player, Msg(player, BuyLimitReached, x.Key.PublicTitle));
                                return true;
                            }

                            limit = GetLimit(player, x.Key, true, true);
                            if (limit <= 0)
                            {
                                ErrorUi(player, Msg(player, DailyBuyLimitReached, x.Key.PublicTitle));
                                return true;
                            }

                            return false;
                        }))
                        return;

                    var logItems = Pool.GetList<string>();
                    foreach (var cartItem in playerCart.Items)
                    {
                        logItems.Add(cartItem.Key.ToString());

                        cartItem.Key?.Get(player, cartItem.Value);

                        SetCooldown(player, cartItem.Key, true);
                        UseLimit(player, cartItem.Key, true, cartItem.Value);
                        UseLimit(player, cartItem.Key, true, cartItem.Value, true);
                    }

                    Log("Buy", LogBuyItems, player.displayName, player.UserIDString,
                        price, string.Join(", ", logItems));

                    CuiHelper.DestroyUi(player, Layer);
                    _carts.Remove(player.userID);
                    _itemsToUpdate.Remove(player);

                    SendNotify(player, ReceivedItems, 0);
                    break;
                }

                case "trysellitem":
                {
                    int itemId;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out itemId)) return;

                    var item = FindItemById(itemId);
                    if (item == null) return;

                    var amount = 1;
                    if (arg.HasArgs(3) && !int.TryParse(arg.Args[2], out amount) || amount < 1) return;

                    if (item.SellMaxAmount > 0 && amount > item.SellMaxAmount) amount = item.SellMaxAmount;

                    SellUi(player, item, amount);
                    break;
                }

                case "sellitem":
                {
                    int itemId, amount;
                    if (!arg.HasArgs(3) ||
                        !int.TryParse(arg.Args[1], out itemId) ||
                        !int.TryParse(arg.Args[2], out amount)) return;

                    var item = FindItemById(itemId);
                    if (item == null) return;

                    var cooldownTime = GetCooldownTime(player.userID, item, false);
                    if (cooldownTime > 0)
                    {
                        ErrorUi(player, Msg(player, SellCooldownMessage));
                        return;
                    }

                    if (_config.BlockNoEscape && NoEscape != null)
                    {
                        var success = NoEscape?.Call("IsBlocked", player);
                        if (success is bool && (bool) success)
                        {
                            ErrorUi(player, Msg(player, SellRaidBlocked));
                            return;
                        }
                    }

                    var limit = GetLimit(player, item, false);
                    if (limit <= 0)
                    {
                        ErrorUi(player, Msg(player, SellLimitReached, item.PublicTitle));
                        return;
                    }

                    limit = GetLimit(player, item, false, true);
                    if (limit <= 0)
                    {
                        ErrorUi(player, Msg(player, DailySellLimitReached, item.PublicTitle));
                        return;
                    }

                    List<ulong> blockedSkins;
                    if (_config.BlockedSkins.TryGetValue(item.ShortName, out blockedSkins))
                        if (blockedSkins.Contains(item.Skin))
                        {
                            ErrorUi(player, Msg(player, SkinBlocked));
                            return;
                        }

                    var totalAmount = item.Amount * amount;
                    var playerItems = player.inventory.AllItems();

                    if (ItemCount(playerItems, item.ShortName, item.Skin) < totalAmount)
                    {
                        ErrorUi(player, Msg(player, NotEnough));
                        return;
                    }

                    Log("Sell", LogSellItem, player.displayName, player.UserIDString,
                        item.SellPrice * amount, item.ToString());

                    Take(playerItems, item.ShortName, item.Skin, totalAmount);

                    _config.Economy.AddBalance(player, item.SellPrice * amount);

                    SetCooldown(player, item, false, true);
                    UseLimit(player, item, false, amount);
                    UseLimit(player, item, false, amount, true);

                    if (_itemsToUpdate.ContainsKey(player))
                    {
                        if (!_itemsToUpdate[player].Contains(item))
                            _itemsToUpdate[player].Add(item);
                    }
                    else
                    {
                        _itemsToUpdate.Add(player, new List<ShopItem> {item});
                    }


                    var container = new CuiElementContainer();
                    SellButtonUi(player, ref container, item);
                    BalanceUi(ref container, player);
                    CuiHelper.AddUi(player, container);

                    SendNotify(player, SellNotify, 0, totalAmount, item.PublicTitle);
                    break;
                }

                case "startedititem":
                {
                    int id, category, page;
                    if (!IsAdmin(player) || !arg.HasArgs(4) ||
                        !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[3], out id)) return;

                    _itemEditing.Remove(player);

                    EditUi(player, category, page, id, true);
                    break;
                }

                case "edititem":
                {
                    int category, page;
                    if (!IsAdmin(player) || !arg.HasArgs(5) || !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page)) return;

                    var key = arg.Args[3];
                    var value = arg.Args[4];

                    if (_itemEditing.ContainsKey(player) && _itemEditing[player].ContainsKey(key))
                    {
                        object newValue = null;

                        switch (key)
                        {
                            case "Type":
                            {
                                ItemType type;
                                if (Enum.TryParse(value, out type))
                                    newValue = type;
                                break;
                            }

                            case "Plugin_Hook":
                            case "Plugin_Name":
                            case "Image":
                            case "Command":
                            case "Title":
                            case "DisplayName":
                            {
                                newValue = string.Join(" ", arg.Args.Skip(4));
                                break;
                            }

                            case "ShortName":
                            {
                                newValue = value;
                                break;
                            }

                            case "Plugin_Amount":
                            case "Amount":
                            {
                                int Value;

                                if (int.TryParse(value, out Value))
                                    newValue = Value;
                                break;
                            }

                            case "SellPrice":
                            case "Price":
                            {
                                double Value;

                                if (double.TryParse(value, out Value))
                                    newValue = Value;
                                break;
                            }

                            case "Skin":
                            {
                                ulong Value;
                                if (ulong.TryParse(value, out Value))
                                    newValue = Value;
                                break;
                            }
                        }

                        _itemEditing[player][key] = newValue;
                    }

                    EditUi(player, category, page);
                    break;
                }

                case "closeediting":
                {
                    _itemEditing.Remove(player);
                    break;
                }

                case "saveitem":
                {
                    int category, page;
                    if (!IsAdmin(player) || !arg.HasArgs(3) || !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page)) return;

                    var edit = _itemEditing[player];
                    if (edit == null) return;

                    var npcShop = GetShopByPlayer(player);

                    var newItem = new ShopItem(edit);

                    var generated = (bool) edit["Generated"];
                    if (generated)
                    {
                        GetCategories(player, npcShop)[category].Items.Add(newItem);
                    }
                    else
                    {
                        var shopItem = FindItemById((int) edit["ID"]);
                        if (shopItem != null)
                        {
                            shopItem.Type = newItem.Type;
                            shopItem.ID = newItem.ID;
                            shopItem.Image = newItem.Image;
                            shopItem.Title = newItem.Title;
                            shopItem.Command = newItem.Command;
                            shopItem.Plugin = newItem.Plugin;
                            shopItem.DisplayName = newItem.DisplayName;
                            shopItem.Skin = newItem.Skin;
                            shopItem.Amount = newItem.Amount;
                            shopItem.Price = newItem.Price;
                            shopItem.SellPrice = newItem.SellPrice;
                        }

                        if (!string.IsNullOrEmpty(newItem.Image))
                            ImageLibrary.Call("AddImage", newItem.Image, newItem.Image);
                    }

                    _itemEditing.Remove(player);

                    SaveConfig();

                    ItemsToDict();

                    if (category == -1)
                        category = 0;

                    MainUi(player, category, page, npcShop, string.Empty, true);
                    break;
                }

                case "removeitem":
                {
                    int category;
                    if (!IsAdmin(player) || !arg.HasArgs(2) || !int.TryParse(arg.Args[1], out category)) return;


                    var editing = _itemEditing[player];
                    if (editing == null) return;

                    var shopItem = FindItemById((int) editing["ID"]);
                    if (shopItem == null) return;

                    _config.Shop.ForEach(x => x.Items.Remove(shopItem));

                    _itemEditing.Remove(player);

                    SaveConfig();

                    if (category == -1)
                        category = 0;

                    MainUi(player, category, npcShop: GetShopByPlayer(player), first: true);
                    break;
                }

                case "selectitem":
                {
                    int category;
                    if (!IsAdmin(player) || !arg.HasArgs(2) || !int.TryParse(arg.Args[1], out category)) return;

                    var cat = string.Empty;
                    if (arg.HasArgs(3))
                        cat = arg.Args[2];

                    var page = 0;
                    if (arg.HasArgs(4))
                        int.TryParse(arg.Args[3], out page);

                    var input = string.Empty;
                    if (arg.HasArgs(5))
                        input = string.Join(" ", arg.Args.Skip(4));

                    SelectItem(player, category, cat, page, input);
                    break;
                }

                case "takeitem":
                {
                    int category, page;
                    if (!IsAdmin(player) || !arg.HasArgs(4) || !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page)) return;

                    _itemEditing[player]["ShortName"] = arg.Args[3];

                    EditUi(player, category, page);
                    break;
                }

                case "item_info":
                {
                    int itemId, catPage, shopPage;
                    bool status;
                    if (!arg.HasArgs(5) ||
                        !int.TryParse(arg.Args[1], out itemId) ||
                        !int.TryParse(arg.Args[2], out catPage) ||
                        !int.TryParse(arg.Args[3], out shopPage) ||
                        !bool.TryParse(arg.Args[4], out status)) return;

                    var item = FindItemById(itemId);
                    if (item == null) return;

                    var container = new CuiElementContainer();
                    ItemUi(player, item, ref container, catPage, shopPage, !status);
                    CuiHelper.DestroyUi(player, Layer + $".Item.{item.ID}");
                    CuiHelper.AddUi(player, container);
                    break;
                }
            }
        }

        [ConsoleCommand("shop.refill")]
        private void CmdConsoleRefill(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;

            FillCategories();

            ItemsToDict();
        }

        [ConsoleCommand("shop.wipe")]
        private void CmdConsoleWipe(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;

            if (_config.Wipe.Players)
                _data.PlayerCarts.Clear();

            if (_config.Wipe.Cooldowns)
                _cooldown.Clear();

            if (_config.Wipe.Limits)
                _limits.Players.Clear();

            PrintWarning($"{Name} wiped!");

            SaveData();
        }

        #endregion

        #region Interface

        private int MainTotalAmount => _config.UI.ItemsOnString * _config.UI.Strings;

        private float MainSwitch => -(_config.UI.ItemsOnString * _config.UI.ItemWidth +
                                      (_config.UI.ItemsOnString - 1) * _config.UI.Margin) / 2f;

        private void MainUi(BasePlayer player, int catPage = 0, int shopPage = 0, NPCShop npcShop = null,
            string search = "",
            bool first = false)
        {
            var shopCategories = GetCategories(player, npcShop);
            var playerCart = GetPlayerCart(player);

            var container = new CuiElementContainer();

            #region Background

            if (first)
            {
                CuiHelper.DestroyUi(player, Layer);

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Image =
                    {
                        Color = "0 0 0 0.9",
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    CursorEnabled = true
                }, _config.UI.DisplayType, Layer);

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text = {Text = ""},
                    Button =
                    {
                        Color = "0 0 0 0",
                        Close = Layer,
                        Command = "UI_Shop closeui"
                    }
                }, Layer);

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = $"-{_config.UI.Width / 2f} -{_config.UI.Height / 2f}",
                        OffsetMax = $"{_config.UI.Width / 2f} {_config.UI.Height / 2f}"
                    },
                    Image =
                    {
                        Color = _thirdColor
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
                    OffsetMin = "0 -50", OffsetMax = "0 0"
                },
                Image =
                {
                    Color = _firstColor
                }
            }, Layer + ".Main", Layer + ".Header");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "20 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, MainTitle),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Header");

            #region Balance

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "0 0", OffsetMax = $"{(IsAdmin(player) ? -225 : -150)} 0"
                },
                Text =
                {
                    Text = Msg(player, YourBalance),
                    Align = TextAnchor.MiddleRight,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Header");

            BalanceUi(ref container, player);

            if (IsAdmin(player))
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = "-110 -37.5",
                        OffsetMax = "-40 -12.5"
                    },
                    Text =
                    {
                        Text = Msg(player, AddItem),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _secondColor,
                        Command = $"UI_Shop startedititem {catPage} {shopPage} {GetId()}"
                    }
                }, Layer + ".Header");

            #endregion

            #region Close

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-35 -37.5",
                    OffsetMax = "-10 -12.5"
                },
                Text =
                {
                    Text = Msg(player, CloseButton),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Close = Layer,
                    Color = _secondColor,
                    Command = "UI_Shop closeui"
                }
            }, Layer + ".Header");

            #endregion

            #endregion

            if (shopCategories.Count > 0)
            {
                #region Items

                var xSwitch = MainSwitch;
                var ySwitch = -70f;

                var shopItems = string.IsNullOrEmpty(search)
                    ? shopCategories[catPage].Items
                    : shopCategories.SelectMany(x => x.Items).Where(
                        item => item.PublicTitle.StartsWith(search) || item.PublicTitle.Contains(search) ||
                                item.ShortName.StartsWith(search) || item.ShortName.Contains(search)).ToList();


                var inPageItems = shopItems.Skip(shopPage * MainTotalAmount).Take(MainTotalAmount).ToList();

                var cdItems = inPageItems.FindAll(x =>
                    GetCooldownTime(player.userID, x, true) > 0 || GetCooldownTime(player.userID, x, false) > 0);
                if (cdItems.Count > 0)
                    _itemsToUpdate[player] = cdItems;
                else
                    _itemsToUpdate.Remove(player);

                for (var i = 0; i < inPageItems.Count; i++)
                {
                    var shopItem = inPageItems[i];
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = $"{xSwitch} {ySwitch - _config.UI.ItemHeight}",
                            OffsetMax = $"{xSwitch + _config.UI.ItemWidth} {ySwitch}"
                        },
                        Image =
                        {
                            Color = "0 0 0 0"
                        }
                    }, Layer + ".Main", Layer + $".Item.{shopItem.ID}.Background");

                    ItemUi(player, shopItem, ref container, catPage, shopPage);

                    if ((i + 1) % _config.UI.ItemsOnString == 0)
                    {
                        xSwitch = MainSwitch;
                        ySwitch = ySwitch - _config.UI.Margin - _config.UI.ItemHeight;
                    }
                    else
                    {
                        xSwitch += _config.UI.Margin + _config.UI.ItemWidth;
                    }
                }

                #endregion

                #region Search

                var enableSearch = _config.UI.EnableSearch;
                if (enableSearch)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                            OffsetMin = "-140 20",
                            OffsetMax = "60 55"
                        },
                        Image =
                        {
                            Color = _sixthColor
                        }
                    }, Layer + ".Main", Layer + ".Search");

                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                        Text =
                        {
                            Text = string.IsNullOrEmpty(search) ? Msg(player, SearchTitle) : $"{search}",
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.65"
                        }
                    }, Layer + ".Search");

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Search",
                        Components =
                        {
                            new CuiInputFieldComponent
                            {
                                FontSize = 12,
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-regular.ttf",
                                Command = catPage != -1 && shopPage != 0
                                    ? "UI_Shop main_page -1 0 "
                                    : $"UI_Shop main_page -1 {shopPage} ",
                                Color = "1 1 1 0.95",
                                CharsLimit = 32
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1"
                            }
                        }
                    });
                }

                #endregion

                #region Pages

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = $"{(enableSearch ? 65 : -37.5f)} 20",
                        OffsetMax = $"{(enableSearch ? 100 : -2.5f)} 55"
                    },
                    Text =
                    {
                        Text = Msg(player, BackPage),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 12,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _sixthColor,
                        Command = shopPage != 0 ? $"UI_Shop main_page {catPage} {shopPage - 1} {search}" : ""
                    }
                }, Layer + ".Main");

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = $"{(enableSearch ? 105 : 2.5f)} 20",
                        OffsetMax = $"{(enableSearch ? 140 : 37.5f)} 55"
                    },
                    Text =
                    {
                        Text = Msg(player, NextPage),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 12,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _secondColor,
                        Command = shopItems.Count > (shopPage + 1) * MainTotalAmount
                            ? $"UI_Shop main_page {catPage} {shopPage + 1} {search}"
                            : ""
                    }
                }, Layer + ".Main");

                #endregion
            }

            #endregion

            #region Categories

            if (first) RefreshCategories(ref container, player, shopCategories, catPage);

            #endregion

            #region Cart

            if (first) RefreshCart(ref container, player, playerCart);

            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Main");
            CuiHelper.AddUi(player, container);
        }

        private void RefreshCategories(ref CuiElementContainer container, BasePlayer player,
            List<ShopCategory> shopCategories, int catPage, int iCategory = 0)
        {
            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "0 1",
                    OffsetMin = "-240 0", OffsetMax = "-10 0"
                },
                Image =
                {
                    Color = _thirdColor
                }
            }, Layer + ".Background", Layer + ".Categories");

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50", OffsetMax = "0 0"
                },
                Image =
                {
                    Color = _firstColor
                }
            }, Layer + ".Categories", Layer + ".Categories.Header");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "20 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, CategoriesTitle),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Categories.Header");

            #endregion

            #region Loop

            var ySwitch = -65f;

            var i = iCategory * _config.UI.CategoriesOnString;
            foreach (var category in shopCategories.Skip(iCategory * _config.UI.CategoriesOnString)
                         .Take(_config.UI.CategoriesOnString))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"10 {ySwitch - _config.UI.CategoriesHeight}",
                        OffsetMax = $"200 {ySwitch}"
                    },
                    Text =
                    {
                        Text = $"{category.Title}",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = i == catPage ? _secondColor : _firstColor,
                        Command = i != catPage ? $"UI_Shop gocat {i} {iCategory}" : ""
                    }
                }, Layer + ".Categories");

                ySwitch = ySwitch - _config.UI.CategoriesMargin - _config.UI.CategoriesHeight;

                i++;
            }

            #endregion

            #region Pages

            if (_config.UI.UseScrollCategories)
            {
                var pages = (int) Math.Ceiling((double) shopCategories.Count / _config.UI.CategoriesOnString);

                if (pages > 1)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-20 15", OffsetMax = "-10 -65"
                        },
                        Image = {Color = "0 0 0 0"}
                    }, Layer + ".Categories", Layer + ".Pages");

                    var size = 1.0 / pages;

                    var pSwitch = 0.0;

                    for (var z = pages - 1; z >= 0; z--)
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = $"0 {pSwitch}", AnchorMax = $"1 {pSwitch + size}"},
                            Button =
                            {
                                Command = $"UI_Shop changeicat {catPage} {z}",
                                Color = z == iCategory ? _secondColor : _firstColor
                            },
                            Text = {Text = ""}
                        }, Layer + ".Pages");

                        pSwitch += size;
                    }
                }
            }
            else
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 0",
                        AnchorMax = "1 0",
                        OffsetMin = "-25 37",
                        OffsetMax = "-5 57"
                    },
                    Text =
                    {
                        Text = Msg(player, BtnBack),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 9,
                        Color = "1 1 1 0.95"
                    },
                    Button =
                    {
                        Color = HexToCuiColor(_config.SecondColor, 33),
                        Command = iCategory != 0 ? $"UI_Shop changeicat {catPage} {iCategory - 1}" : ""
                    }
                }, Layer + ".Categories");

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 0",
                        AnchorMax = "1 0",
                        OffsetMin = "-25 15",
                        OffsetMax = "-5 35"
                    },
                    Text =
                    {
                        Text = Msg(player, BtnNext),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 0.95"
                    },
                    Button =
                    {
                        Color = _secondColor,
                        Command = shopCategories.Count > (iCategory + 1) * _config.UI.CategoriesOnString
                            ? $"UI_Shop changeicat {catPage} {iCategory + 1}"
                            : ""
                    }
                }, Layer + ".Categories");
            }

            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Categories");
        }

        private const int CartTotalAmount = 7;
        private const float CartHeight = 45f;
        private const float CartMargin = 5f;

        private void RefreshCart(ref CuiElementContainer container, BasePlayer player, DataCart playerCart,
            int cartPage = 0)
        {
            var ySwitch = -60f;

            var i = 0;

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "1 0", AnchorMax = "1 1",
                    OffsetMin = "10 0", OffsetMax = "240 0"
                },
                Image =
                {
                    Color = _thirdColor
                }
            }, Layer + ".Background", Layer + ".PlayerCart");

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50", OffsetMax = "0 0"
                },
                Image =
                {
                    Color = _firstColor
                }
            }, Layer + ".PlayerCart", Layer + ".PlayerCart.Header");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "20 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, ShoppingBag),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = "1 1 1 1"
                }
            }, Layer + ".PlayerCart.Header");

            #region Pages

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-128 -35", OffsetMax = "-73 -15"
                },
                Text =
                {
                    Text = Msg(player, BackTitle),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _thirdColor,
                    Command = cartPage != 0 ? $"UI_Shop cart_page {cartPage - 1}" : ""
                }
            }, Layer + ".PlayerCart.Header");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-70 -35", OffsetMax = "-15 -15"
                },
                Text =
                {
                    Text = Msg(player, NextTitle),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _secondColor,
                    Command = playerCart.Items.Count > (cartPage + 1) * CartTotalAmount
                        ? $"UI_Shop cart_page {cartPage + 1}"
                        : ""
                }
            }, Layer + ".PlayerCart.Header");

            #endregion

            #endregion

            #region Loop

            foreach (var check in playerCart.Items.Skip(cartPage * CartTotalAmount).Take(CartTotalAmount))
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = $"-105 {ySwitch - CartHeight}",
                        OffsetMax = $"105 {ySwitch}"
                    },
                    Image =
                    {
                        Color = _firstColor
                    }
                }, Layer + ".PlayerCart", Layer + $".PlayerCart.Item.{i}");

                container.Add(new CuiElement
                {
                    Parent = Layer + $".PlayerCart.Item.{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png =
                                !string.IsNullOrEmpty(check.Key.Image)
                                    ? ImageLibrary.Call<string>("GetImage", check.Key.Image)
                                    : ImageLibrary.Call<string>("GetImage", check.Key.ShortName, check.Key.Skin)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = "10 5", OffsetMax = "45 40"
                        }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0.5", AnchorMax = "1 1",
                        OffsetMin = "50 0", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = $"{check.Key.PublicTitle}",
                        Align = TextAnchor.LowerLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 12,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".PlayerCart.Item.{i}");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0.5",
                        OffsetMin = "50 0", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = Msg(player, AmountTitle, check.Key.Amount * check.Value),
                        Align = TextAnchor.UpperLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".PlayerCart.Item.{i}");

                #region Amount

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                        OffsetMin = "-60 -17.5", OffsetMax = "-10 -2.5"
                    },
                    Text =
                    {
                        Text = Msg(player, RemoveTitle),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _secondColor,
                        Command = $"UI_Shop cart_item_remove {check.Key.ID}"
                    }
                }, Layer + $".PlayerCart.Item.{i}");

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                        OffsetMin = "-60 2.5", OffsetMax = "-45 17.5"
                    },
                    Text =
                    {
                        Text = Msg(player, MinusTitle),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _secondColor,
                        Command = $"UI_Shop cart_item_change {check.Key.ID} {check.Value - 1}"
                    }
                }, Layer + $".PlayerCart.Item.{i}");

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                        OffsetMin = "-25 2.5", OffsetMax = "-10 17.5"
                    },
                    Text =
                    {
                        Text = Msg(player, PlusTitle),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _secondColor,
                        Command = $"UI_Shop cart_item_change {check.Key.ID} {check.Value + 1}"
                    }
                }, Layer + $".PlayerCart.Item.{i}");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                        OffsetMin = "-45 2.5", OffsetMax = "-25 17.5"
                    },
                    Text =
                    {
                        Text = $"{check.Value}",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 0.75"
                    }
                }, Layer + $".PlayerCart.Item.{i}", Layer + $".PlayerCart.Item.{i}.Amount");

                container.Add(new CuiElement
                {
                    Parent = Layer + $".PlayerCart.Item.{i}.Amount",
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Command = $"UI_Shop cart_item_change {check.Key.ID} ",
                            Color = "1 1 1 0.95",
                            CharsLimit = 5
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });

                #endregion

                ySwitch = ySwitch - CartMargin - CartHeight;

                i++;
            }

            #endregion

            #region Footer

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 0",
                    OffsetMin = "0 0", OffsetMax = "0 80"
                },
                Image =
                {
                    Color = _firstColor
                }
            }, Layer + ".PlayerCart", Layer + ".PlayerCart.Footer");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                    OffsetMin = "-90 10", OffsetMax = "90 40"
                },
                Text =
                {
                    Text = Msg(player, BuyTitle),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _secondColor,
                    Command = "UI_Shop cart_try_buyitems"
                }
            }, Layer + ".PlayerCart.Footer");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-90 -40", OffsetMax = "90 0"
                },
                Text =
                {
                    Text = Msg(player, ItemsTitle, playerCart.GetAmount()),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                }
            }, Layer + ".PlayerCart.Footer");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-90 -40", OffsetMax = "90 0"
                },
                Text =
                {
                    Text = Msg(player, CostTitle, playerCart.GetPrice(player)),
                    Align = TextAnchor.MiddleRight,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                }
            }, Layer + ".PlayerCart.Footer");

            #endregion

            CuiHelper.DestroyUi(player, Layer + ".PlayerCart");
        }

        private void AcceptBuy(BasePlayer player)
        {
            var container = new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                        Image = {Color = HexToCuiColor(_config.ThirdColor, 99)}
                    },
                    _config.UI.DisplayType, ModalLayer
                },
                {
                    new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-70 40",
                            OffsetMax = "70 60"
                        },
                        Text =
                        {
                            Text = Msg(player, PurchaseConfirmation),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Color = "1 1 1 1"
                        }
                    },
                    ModalLayer
                },
                {
                    new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-70 10",
                            OffsetMax = "70 40"
                        },
                        Text =
                        {
                            Text = Msg(player, BuyTitle),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 10,
                            Color = "1 1 1 1"
                        },
                        Button =
                        {
                            Color = _secondColor,
                            Command = "UI_Shop cart_buyitems",
                            Close = ModalLayer
                        }
                    },
                    ModalLayer
                },
                {
                    new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-70 -22.5",
                            OffsetMax = "70 7.5"
                        },
                        Text =
                        {
                            Text = Msg(player, CancelTitle),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 10,
                            Color = "1 1 1 1"
                        },
                        Button = {Color = HexToCuiColor(_config.SecondColor, 33), Close = ModalLayer}
                    },
                    ModalLayer
                }
            };


            CuiHelper.DestroyUi(player, ModalLayer);
            CuiHelper.AddUi(player, container);
        }

        private void ErrorUi(BasePlayer player, string msg)
        {
            var container = new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                        Image = {Color = HexToCuiColor(_config.ThirdColor, 98)},
                        CursorEnabled = true
                    },
                    _config.UI.DisplayType, ModalLayer
                },
                {
                    new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-127.5 -75",
                            OffsetMax = "127.5 140"
                        },
                        Image = {Color = _fifthColor}
                    },
                    ModalLayer, ModalLayer + ".Main"
                },
                {
                    new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -165", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, ErrorMsg),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 120,
                            Color = "1 1 1 1"
                        }
                    },
                    ModalLayer + ".Main"
                },
                {
                    new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -175", OffsetMax = "0 -135"
                        },
                        Text =
                        {
                            Text = $"{msg}",
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 1"
                        }
                    },
                    ModalLayer + ".Main"
                },
                {
                    new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 30"
                        },
                        Text =
                        {
                            Text = Msg(player, ErrorClose),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 1"
                        },
                        Button = {Color = _seventhColor, Close = ModalLayer}
                    },
                    ModalLayer + ".Main"
                }
            };

            CuiHelper.DestroyUi(player, ModalLayer);
            CuiHelper.AddUi(player, container);
        }

        private void SellUi(BasePlayer player, ShopItem item, int amount = 1)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image = {Color = HexToCuiColor(_config.ThirdColor, 98)},
                CursorEnabled = true
            }, _config.UI.DisplayType, ModalLayer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text = {Text = ""},
                Button = {Color = "0 0 0 0", Close = ModalLayer}
            }, ModalLayer);

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-95 -100", OffsetMax = "95 110"
                },
                Image =
                {
                    Color = _firstColor
                }
            }, ModalLayer, ModalLayer + ".Main");

            container.Add(new CuiElement
            {
                Parent = ModalLayer + ".Main",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = !string.IsNullOrEmpty(item.Image)
                            ? ImageLibrary.Call<string>("GetImage", item.Image)
                            : ImageLibrary.Call<string>("GetImage", item.ShortName, item.Skin)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "-45 -100", OffsetMax = "45 -10"
                    }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -135", OffsetMax = "0 -95"
                },
                Text =
                {
                    Text = $"{item.PublicTitle}",
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 16,
                    Color = "1 1 1 1"
                }
            }, ModalLayer + ".Main");

            #region Sell Btn

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-60 -190", OffsetMax = "60 -170"
                },
                Text =
                {
                    Text = Msg(player, SellBtn, item.SellPrice * amount),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _secondColor,
                    Command = $"UI_Shop sellitem {item.ID} {amount}",
                    Close = ModalLayer
                }
            }, ModalLayer + ".Main");

            #endregion

            #region Input

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-60 -165", OffsetMax = "-45 -150"
                },
                Text =
                {
                    Text = Msg(player, MinusTitle),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _secondColor,
                    Command = $"UI_Shop trysellitem {item.ID} {amount - 1}"
                }
            }, ModalLayer + ".Main");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "45 -165", OffsetMax = "60 -150"
                },
                Text =
                {
                    Text = Msg(player, PlusTitle),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _secondColor,
                    Command = $"UI_Shop trysellitem {item.ID} {amount + 1}"
                }
            }, ModalLayer + ".Main");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-45 -165", OffsetMax = "45 -150"
                },
                Text =
                {
                    Text = $"{amount}",
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 0.75"
                }
            }, ModalLayer + ".Main", ModalLayer + ".Amount");

            container.Add(new CuiElement
            {
                Parent = ModalLayer + ".Amount",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        FontSize = 10,
                        Align = TextAnchor.MiddleCenter,
                        Command = $"UI_Shop trysellitem {item.ID} ",
                        Color = "1 1 1 0.95",
                        CharsLimit = 5
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    }
                }
            });

            #endregion

            CuiHelper.DestroyUi(player, ModalLayer);
            CuiHelper.AddUi(player, container);
        }

        private void BalanceUi(ref CuiElementContainer container, BasePlayer player)
        {
            var admin = IsAdmin(player);

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = $"{(admin ? -220 : -145)} -37.5",
                    OffsetMax = $"{(admin ? -115 : -40)} -12.5"
                },
                Image =
                {
                    Color = _secondColor
                }
            }, Layer + ".Header", Layer + ".Balance");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text =
                {
                    Text = Msg(player, BalanceTitle, _config.Economy.ShowBalance(player)),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Balance");

            CuiHelper.DestroyUi(player, Layer + ".Balance");
        }

        private void EditUi(BasePlayer player, int category, int page, int itemId = 0, bool First = false)
        {
            var container = new CuiElementContainer();

            #region Dictionary

            if (!_itemEditing.ContainsKey(player))
            {
                var shopItem = FindItemById(itemId);
                if (shopItem != null)
                    _itemEditing[player] = shopItem.ToDictionary();
                else
                    _itemEditing[player] = new Dictionary<string, object>
                    {
                        ["Generated"] = true,
                        ["ID"] = GetId(),
                        ["Type"] = ItemType.Item,
                        ["Image"] = string.Empty,
                        ["Title"] = string.Empty,
                        ["Command"] = string.Empty,
                        ["DisplayName"] = string.Empty,
                        ["ShortName"] = string.Empty,
                        ["Skin"] = 0UL,
                        ["Amount"] = 1,
                        ["Price"] = 100.0,
                        ["SellPrice"] = 100.0,
                        ["Plugin_Hook"] = string.Empty,
                        ["Plugin_Name"] = string.Empty,
                        ["Plugin_Amount"] = 1
                    };
            }

            #endregion

            var edit = _itemEditing[player];

            #region Background

            if (First)
            {
                CuiHelper.DestroyUi(player, EditingLayer);

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Image = {Color = HexToCuiColor(_config.FirstColor, 95)},
                    CursorEnabled = true
                }, _config.UI.DisplayType, EditingLayer);
            }

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-260 -240",
                    OffsetMax = "260 260"
                },
                Image =
                {
                    Color = _thirdColor
                }
            }, EditingLayer, EditingLayer + ".Main");

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50",
                    OffsetMax = "0 0"
                },
                Image = {Color = _firstColor}
            }, EditingLayer + ".Main", EditingLayer + ".Header");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "10 0",
                    OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, EditingTitle),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = "1 1 1 1"
                }
            }, EditingLayer + ".Header");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-35 -37.5",
                    OffsetMax = "-10 -12.5"
                },
                Text =
                {
                    Text = Msg(player, CloseButton),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Close = EditingLayer,
                    Color = _secondColor,
                    Command = "UI_Shop closeediting"
                }
            }, EditingLayer + ".Header");

            #endregion

            #region Type

            var type = edit["Type"] as ItemType? ?? ItemType.Item;

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-70 -110",
                    OffsetMax = "30 -80"
                },
                Text =
                {
                    Text = Msg(player, ItemName),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = HexToCuiColor(_config.SecondColor, type == ItemType.Item ? 100 : 50),
                    Command = $"UI_Shop edititem {category} {page} Type {ItemType.Item}"
                }
            }, EditingLayer + ".Main");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "35 -110",
                    OffsetMax = "135 -80"
                },
                Text =
                {
                    Text = Msg(player, CmdName),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = HexToCuiColor(_config.SecondColor, type == ItemType.Command ? 100 : 50),
                    Command = $"UI_Shop edititem {category} {page} Type {ItemType.Command}"
                }
            }, EditingLayer + ".Main");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "140 -110",
                    OffsetMax = "240 -80"
                },
                Text =
                {
                    Text = Msg(player, PluginName),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = HexToCuiColor(_config.SecondColor, type == ItemType.Plugin ? 100 : 50),
                    Command = $"UI_Shop edititem {category} {page} Type {ItemType.Plugin}"
                }
            }, EditingLayer + ".Main");

            #endregion

            #region Command

            EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "-240 -110",
                "-75 -60",
                $"UI_Shop edititem {category} {page} Command ",
                new KeyValuePair<string, object>("Command", edit["Command"]));

            #endregion

            #region Item

            var shortName = (string) edit["ShortName"];

            #region Image

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-240 -290", OffsetMax = "-100 -150"
                },
                Image = {Color = _firstColor}
            }, EditingLayer + ".Main", EditingLayer + ".Image");

            if (!string.IsNullOrEmpty(shortName) && ImageLibrary)
                container.Add(new CuiElement
                {
                    Parent = EditingLayer + ".Image",
                    Components =
                    {
                        new CuiRawImageComponent
                            {Png = ImageLibrary.Call<string>("GetImage", shortName, edit["Skin"])},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "10 10", OffsetMax = "-10 -10"
                        }
                    }
                });

            #endregion

            #region Select Item

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-240 -325",
                    OffsetMax = "-100 -295"
                },
                Text =
                {
                    Text = Msg(player, BtnSelect),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _secondColor,
                    Command = $"UI_Shop selectitem {category}"
                }
            }, EditingLayer + ".Main");

            #endregion

            #region ShortName

            EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "-85 -190",
                "75 -130",
                $"UI_Shop edititem {category} {page} ShortName ",
                new KeyValuePair<string, object>("ShortName", edit["ShortName"]));

            #endregion

            #region Skin

            EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "80 -190",
                "240 -130",
                $"UI_Shop edititem {category} {page} Skin ",
                new KeyValuePair<string, object>("Skin", edit["Skin"]));

            #endregion

            #region DisplayName

            EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "-85 -260",
                "75 -200",
                $"UI_Shop edititem {category} {page} DisplayName ",
                new KeyValuePair<string, object>("DisplayName", edit["DisplayName"]));

            #endregion

            #region Amount

            EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "80 -260",
                "240 -200",
                $"UI_Shop edititem {category} {page} Amount ",
                new KeyValuePair<string, object>("Amount", edit["Amount"]));

            #endregion

            #region SellPrice

            EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "-85 -330",
                "75 -270",
                $"UI_Shop edititem {category} {page} SellPrice ",
                new KeyValuePair<string, object>("SellPrice", edit["SellPrice"]));

            #endregion

            #region Price

            EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "80 -330",
                "240 -270",
                $"UI_Shop edititem {category} {page} Price ",
                new KeyValuePair<string, object>("Price", edit["Price"]));

            #endregion

            #region Title

            EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "-240 -395",
                "-5 -335",
                $"UI_Shop edititem {category} {page} Title ",
                new KeyValuePair<string, object>("Title", edit["Title"]));

            #endregion

            #region Image

            EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "5 -395",
                "240 -335",
                $"UI_Shop edititem {category} {page} Image ",
                new KeyValuePair<string, object>("Image", edit["Image"]));

            #endregion

            #region Plugin Hook

            EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "-240 -460",
                "-85 -400",
                $"UI_Shop edititem {category} {page} Plugin_Hook ",
                new KeyValuePair<string, object>("Plugin_Hook", edit["Plugin_Hook"]));

            #endregion

            #region Plugin Name

            EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "-80 -460",
                "80 -400",
                $"UI_Shop edititem {category} {page} Plugin_Name ",
                new KeyValuePair<string, object>("Plugin_Name", edit["Plugin_Name"]));

            #endregion

            #region Plugin Amount

            EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "85 -460",
                "240 -400",
                $"UI_Shop edititem {category} {page} Plugin_Amount ",
                new KeyValuePair<string, object>("Plugin_Amount", edit["Plugin_Amount"]));

            #endregion

            #endregion

            var generated = (bool) edit["Generated"];


            #region Save Button

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                    OffsetMin = "-90 -5",
                    OffsetMax = $"{(generated ? 90 : 55)} 25"
                },
                Text =
                {
                    Text = Msg(player, BtnSave),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _secondColor,
                    Command = $"UI_Shop saveitem {category} {page}",
                    Close = EditingLayer
                }
            }, EditingLayer + ".Main");

            if (!generated)
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "60 -5",
                        OffsetMax = "90 25"
                    },
                    Text =
                    {
                        Text = Msg(player, RemoveItem),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _fifthColor,
                        Command = $"UI_Shop removeitem {category}",
                        Close = EditingLayer
                    }
                }, EditingLayer + ".Main");

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, EditingLayer + ".Main");
            CuiHelper.AddUi(player, container);
        }

        private void EditFieldUi(ref CuiElementContainer container,
            string parent,
            string name,
            string oMin,
            string oMax,
            string command,
            KeyValuePair<string, object> obj)
        {
            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = $"{oMin}",
                    OffsetMax = $"{oMax}"
                },
                Image =
                {
                    Color = "0 0 0 0"
                }
            }, parent, name);

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -20", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = $"{obj.Key}".Replace("_", " "),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                }
            }, name);

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "0 0", OffsetMax = "0 -20"
                },
                Image = {Color = "0 0 0 0"}
            }, name, $"{name}.Value");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "10 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = $"{obj.Value}",
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 0.15"
                }
            }, $"{name}.Value");

            CreateOutLine(ref container, $"{name}.Value", _firstColor);

            container.Add(new CuiElement
            {
                Parent = $"{name}.Value",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Command = $"{command}",
                        Color = "1 1 1 0.99",
                        CharsLimit = 150
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "10 0", OffsetMax = "0 0"
                    }
                }
            });
        }


        private void SelectItem(BasePlayer player, int category, string selectedCategory = "", int page = 0,
            string input = "")
        {
            if (string.IsNullOrEmpty(selectedCategory)) selectedCategory = _itemsCategories.FirstOrDefault().Key;

            var container = new CuiElementContainer();

            #region Background

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text = {Text = ""},
                Button =
                {
                    Close = ModalLayer,
                    Color = HexToCuiColor(_config.FirstColor, 80)
                }
            }, _config.UI.DisplayType, ModalLayer);

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-260 -270",
                    OffsetMax = "260 280"
                },
                Image =
                {
                    Color = _thirdColor
                }
            }, ModalLayer, ModalLayer + ".Main");

            #region Categories

            var amountOnString = 4;
            var Width = 120f;
            var Height = 25f;
            var xMargin = 5f;
            var yMargin = 5f;

            var constSwitch = -(amountOnString * Width + (amountOnString - 1) * xMargin) / 2f;
            var xSwitch = constSwitch;
            var ySwitch = -15f;

            var i = 1;
            foreach (var cat in _itemsCategories)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = $"{xSwitch} {ySwitch - Height}",
                        OffsetMax = $"{xSwitch + Width} {ySwitch}"
                    },
                    Text =
                    {
                        Text = $"{cat.Key}",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = selectedCategory == cat.Key
                            ? _secondColor
                            : _firstColor,
                        Command = $"UI_Shop selectitem {category} {cat.Key}"
                    }
                }, ModalLayer + ".Main");

                if (i % amountOnString == 0)
                {
                    ySwitch = ySwitch - Height - yMargin;
                    xSwitch = constSwitch;
                }
                else
                {
                    xSwitch += xMargin + Width;
                }

                i++;
            }

            #endregion

            #region Items

            amountOnString = 5;

            var strings = 4;
            var totalAmount = amountOnString * strings;

            ySwitch = ySwitch - yMargin - Height - 10f;

            Width = 85f;
            Height = 85f;
            xMargin = 15f;
            yMargin = 5f;

            constSwitch = -(amountOnString * Width + (amountOnString - 1) * xMargin) / 2f;
            xSwitch = constSwitch;

            i = 1;

            var canSearch = !string.IsNullOrEmpty(input) && input.Length > 2;

            var temp = canSearch
                ? _itemsCategories
                    .SelectMany(x => x.Value)
                    .Where(x => x.StartsWith(input) || x.Contains(input) || x.EndsWith(input)).ToList()
                : _itemsCategories[selectedCategory];

            var itemsAmount = temp.Count;
            var Items = temp.Skip(page * totalAmount).Take(totalAmount).ToList();

            Items.ForEach(item =>
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = $"{xSwitch} {ySwitch - Height}",
                        OffsetMax = $"{xSwitch + Width} {ySwitch}"
                    },
                    Image = {Color = _firstColor}
                }, ModalLayer + ".Main", ModalLayer + $".Item.{item}");

                if (ImageLibrary)
                    container.Add(new CuiElement
                    {
                        Parent = ModalLayer + $".Item.{item}",
                        Components =
                        {
                            new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", item)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "5 5", OffsetMax = "-5 -5"
                            }
                        }
                    });

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text = {Text = ""},
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_Shop takeitem {category} {page} {item}",
                        Close = ModalLayer
                    }
                }, ModalLayer + $".Item.{item}");

                if (i % amountOnString == 0)
                {
                    xSwitch = constSwitch;
                    ySwitch = ySwitch - yMargin - Height;
                }
                else
                {
                    xSwitch += xMargin + Width;
                }

                i++;
            });

            #endregion

            #region Search

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                    OffsetMin = "-90 10", OffsetMax = "90 35"
                },
                Image = {Color = _secondColor}
            }, ModalLayer + ".Main", ModalLayer + ".Search");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "10 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = canSearch ? $"{input}" : Msg(player, ItemSearch),
                    Align = canSearch ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = canSearch ? "1 1 1 0.8" : "1 1 1 1"
                }
            }, ModalLayer + ".Search");

            container.Add(new CuiElement
            {
                Parent = ModalLayer + ".Search",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        FontSize = 10,
                        Align = TextAnchor.MiddleLeft,
                        Command = $"UI_Shop selectitem {category} {selectedCategory} 0 ",
                        Color = "1 1 1 0.95",
                        CharsLimit = 150
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "10 0", OffsetMax = "0 0"
                    }
                }
            });

            #endregion

            #region Pages

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "0 0",
                    OffsetMin = "10 10",
                    OffsetMax = "80 35"
                },
                Text =
                {
                    Text = Msg(player, Back),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _firstColor,
                    Command = page != 0
                        ? $"UI_Shop selectitem {category} {selectedCategory} {page - 1} {input}"
                        : ""
                }
            }, ModalLayer + ".Main");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 0", AnchorMax = "1 0",
                    OffsetMin = "-80 10",
                    OffsetMax = "-10 35"
                },
                Text =
                {
                    Text = Msg(player, Next),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _secondColor,
                    Command = itemsAmount > (page + 1) * totalAmount
                        ? $"UI_Shop selectitem {category} {selectedCategory} {page + 1} {input}"
                        : ""
                }
            }, ModalLayer + ".Main");

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, ModalLayer);
            CuiHelper.AddUi(player, container);
        }

        private void BuyButtonUi(BasePlayer player, ref CuiElementContainer container, ShopItem shopItem)
        {
            var cooldownTime = GetCooldownTime(player.userID, shopItem, true);
            if (cooldownTime > 0)
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "-65 -165", OffsetMax = "65 -135"
                    },
                    Image =
                    {
                        Color = HexToCuiColor(_config.SecondColor, 33)
                    }
                }, Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Buy");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "5 0", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = Msg(player, BuyCooldownTitle),
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 12,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Item.{shopItem.ID}.Buy");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "0 0", OffsetMax = "-10 0"
                    },
                    Text =
                    {
                        Text = $"{FormatShortTime(player, TimeSpan.FromSeconds(cooldownTime))}",
                        Align = TextAnchor.MiddleRight,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Item.{shopItem.ID}.Buy");
            }
            else
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "-65 -165", OffsetMax = "65 -135"
                    },
                    Image =
                    {
                        Color = _secondColor
                    }
                }, Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Buy");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "5 0", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = Msg(player, BuyTitle),
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 12,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Item.{shopItem.ID}.Buy");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "0 0", OffsetMax = "-10 0"
                    },
                    Text =
                    {
                        Text = Msg(player, ItemPrice, shopItem.GetPrice(player)),
                        Align = TextAnchor.MiddleRight,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Item.{shopItem.ID}.Buy");

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text = {Text = ""},
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_Shop buyitem {shopItem.ID}"
                    }
                }, Layer + $".Item.{shopItem.ID}.Buy");
            }

            CuiHelper.DestroyUi(player, Layer + $".Item.{shopItem.ID}.Buy");
        }

        private void SellButtonUi(BasePlayer player, ref CuiElementContainer container, ShopItem shopItem)
        {
            var cooldownTime = GetCooldownTime(player.userID, shopItem, false);
            if (cooldownTime > 0)
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "-50 -15", OffsetMax = "50 5"
                    },
                    Image =
                    {
                        Color = HexToCuiColor(_config.FifthColor, 65)
                    }
                }, Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Sell");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "5 0", OffsetMax = "-5 0"
                    },
                    Text =
                    {
                        Text = Msg(player, SellCooldownTitle),
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 12,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Item.{shopItem.ID}.Sell");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "0 0", OffsetMax = "-10 0"
                    },
                    Text =
                    {
                        Text = $"{FormatShortTime(player, TimeSpan.FromSeconds(cooldownTime))}",
                        Align = TextAnchor.MiddleRight,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Item.{shopItem.ID}.Sell");
            }
            else
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "-50 -15", OffsetMax = "50 5"
                    },
                    Image =
                    {
                        Color = _fifthColor
                    }
                }, Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Sell");


                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "5 0", OffsetMax = "-5 0"
                    },
                    Text =
                    {
                        Text = Msg(player, SellTitle),
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 12,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Item.{shopItem.ID}.Sell");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "0 0", OffsetMax = "-10 0"
                    },
                    Text =
                    {
                        Text = Msg(player, ItemPrice, shopItem.SellPrice),
                        Align = TextAnchor.MiddleRight,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Item.{shopItem.ID}.Sell");

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text = {Text = ""},
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_Shop trysellitem {shopItem.ID}"
                    }
                }, Layer + $".Item.{shopItem.ID}.Sell");
            }

            CuiHelper.DestroyUi(player, Layer + $".Item.{shopItem.ID}.Sell");
        }

        private void ItemUi(BasePlayer player, ShopItem shopItem, ref CuiElementContainer container, int catPage,
            int shopPage, bool status = false)
        {
            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -150", OffsetMax = "0 0"
                },
                Image =
                {
                    Color = _firstColor
                }
            }, Layer + $".Item.{shopItem.ID}.Background", Layer + $".Item.{shopItem.ID}");

            if (status)
            {
                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1",
                        OffsetMin = "0 -135", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = $"{shopItem.Description}",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Item.{shopItem.ID}");
            }
            else
            {
                container.Add(new CuiElement
                {
                    Name = Layer + $".Item.{shopItem.ID}.Image",
                    Parent = Layer + $".Item.{shopItem.ID}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = !string.IsNullOrEmpty(shopItem.Image)
                                ? ImageLibrary.Call<string>("GetImage", shopItem.Image)
                                : ImageLibrary.Call<string>("GetImage", shopItem.ShortName, shopItem.Skin)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-35 -85", OffsetMax = "35 -15"
                        }
                    }
                });

                #region Name

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1",
                        OffsetMin = "0 -100", OffsetMax = "0 -85"
                    },
                    Text =
                    {
                        Text = $"{shopItem.PublicTitle}",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 12,
                        Color = "1 1 1 0.95"
                    }
                }, Layer + $".Item.{shopItem.ID}");

                #endregion
            }

            #region Amount

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-65 -130", OffsetMax = "-5 -110"
                },
                Text =
                {
                    Text = Msg(player, ItemAmount),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, Layer + $".Item.{shopItem.ID}");

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-10 -130", OffsetMax = "30 -110"
                },
                Image =
                {
                    Color = HexToCuiColor(_config.SecondColor, 33)
                }
            }, Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Amount");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text =
                {
                    Text = $"{shopItem.Amount}",
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, Layer + $".Item.{shopItem.ID}.Amount");

            #endregion

            #region Info

            if (!string.IsNullOrEmpty(shopItem.Description))
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "35 -130", OffsetMax = "55 -110"
                    },
                    Text =
                    {
                        Text = Msg(player, InfoTitle),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#000000"),
                        Command = $"UI_Shop item_info {shopItem.ID} {catPage} {shopPage} {status}"
                    }
                }, Layer + $".Item.{shopItem.ID}");

            #endregion

            #region Discount

            var discount = shopItem.GetDiscount(player);
            if (discount > 0)
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = "-30 -45", OffsetMax = "10 -25"
                    },
                    Image =
                    {
                        Color = _fourthColor
                    }
                }, Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Discount");

                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text =
                    {
                        Text = $"-{discount}%",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 12,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Item.{shopItem.ID}.Discount");
            }

            #endregion

            #region Button

            if (shopItem.Price > 0.0) BuyButtonUi(player, ref container, shopItem);

            #endregion

            #region Sell

            if (shopItem.SellPrice > 0.0) SellButtonUi(player, ref container, shopItem);

            #endregion

            #region Edit

            if (IsAdmin(player))
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    },
                    Text = {Text = ""},
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_Shop startedititem {catPage} {shopPage} {shopItem.ID}"
                    }
                }, Layer + $".Item.{shopItem.ID}.Image");

            #endregion
        }

        #endregion

        #region Utils

        private bool InDuel(BasePlayer player)
        {
            return Convert.ToBoolean(Duel?.Call("IsPlayerOnActiveDuel", player)) ||
                   Convert.ToBoolean(Duelist?.Call("inEvent", player));
        }

        private bool IsAdmin(BasePlayer player)
        {
            return player != null && (player.IsAdmin && _config.FlagAdmin ||
                                      permission.UserHasPermission(player.UserIDString, PermAdmin));
        }

        private int GetId()
        {
            var result = -1;

            do
            {
                var val = Random.Range(int.MinValue, int.MaxValue);

                if (!_shopItems.ContainsKey(val))
                    result = val;
            } while (result == -1);

            return result;
        }

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
                    Image = {Color = color}
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
                    Image = {Color = color}
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
                    Image = {Color = color}
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
                    Image = {Color = color}
                },
                parent);
        }

        private IEnumerator LoadImages(BasePlayer player)
        {
            foreach (var image in _images)
            {
                if (player == null || !player.IsConnected) continue;

                ImageLibrary?.Call("SendImage", player, image);

                yield return CoroutineEx.waitForSeconds(_config.ImagesDelay);
            }
        }

        private ShopItem FindItemById(int id)
        {
            ShopItem item;
            return _shopItems.TryGetValue(id, out item) ? item : null;
        }

        private void FillCategories()
        {
            _config.Shop.Clear();

            var sw = Stopwatch.StartNew();

            var dict = new Dictionary<string, List<ItemDefinition>>();

            ItemManager.itemList.ForEach(item =>
            {
                var itemCategory = item.category.ToString();

                if (dict.ContainsKey(itemCategory))
                    dict[itemCategory].Add(item);
                else
                    dict.Add(itemCategory, new List<ItemDefinition> {item});
            });

            var id = 0;
            foreach (var check in dict)
            {
                var category = new ShopCategory
                {
                    Enabled = true,
                    Title = check.Key,
                    Permission = string.Empty,
                    Items = new List<ShopItem>()
                };

                check.Value
                    .FindAll(itemDefinition => itemDefinition.shortname != "blueprintbase" &&
                                               ImageLibrary.Call<bool>("HasImage", itemDefinition.shortname))
                    .ForEach(
                        itemDefinition =>
                        {
                            var itemCost = Math.Round(GetItemCost(itemDefinition));

                            category.Items.Add(new ShopItem
                            {
                                Type = ItemType.Item,
                                ID = id++,
                                Price = itemCost,
                                SellPrice = itemCost,
                                Image = string.Empty,
                                Title = string.Empty,
                                Command = string.Empty,
                                Plugin = new PluginItem(),
                                DisplayName = string.Empty,
                                ShortName = itemDefinition.shortname,
                                Skin = 0,
                                Amount = 1,
                                Discount = new Dictionary<string, int>
                                {
                                    ["shop.default"] = 0,
                                    ["shop.vip"] = 10
                                },
                                SellLimits = new Dictionary<string, int>
                                {
                                    ["shop.default"] = 0,
                                    ["shop.vip"] = 0
                                },
                                BuyLimits = new Dictionary<string, int>
                                {
                                    ["shop.default"] = 0,
                                    ["shop.vip"] = 0
                                },
                                DailyBuyLimits = new Dictionary<string, int>
                                {
                                    ["shop.default"] = 0,
                                    ["shop.vip"] = 0
                                },
                                DailySellLimits = new Dictionary<string, int>
                                {
                                    ["shop.default"] = 0,
                                    ["shop.vip"] = 0
                                }
                            });
                        });

                _config.Shop.Add(category);
            }

            SaveConfig();

            sw.Stop();
            PrintWarning($"The store was filled with items in {sw.ElapsedMilliseconds} ms!");
        }

        private double GetItemCost(ItemDefinition itemDefinition)
        {
            return ItemCostCalculator?.Call<double>("GetItemCost", itemDefinition) ?? 100;
        }

        private void CheckOnDuplicates()
        {
            var duplicates = _config.Shop.SelectMany(x => x.Items)
                .GroupBy(x => x.ID)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            var shopItems = duplicates as int[] ?? duplicates.ToArray();
            if (shopItems.Length > 0)
                PrintError(
                    $"Matching item IDs found (Shop): {string.Join(", ", shopItems.Select(x => x.ToString()))}");
        }

        private void ItemsToDict()
        {
            _shopItems.Clear();

            ItemManager.itemList.ForEach(item =>
            {
                var itemCategory = item.category.ToString();

                if (_itemsCategories.ContainsKey(itemCategory))
                {
                    if (!_itemsCategories[itemCategory].Contains(item.shortname))
                        _itemsCategories[itemCategory].Add(item.shortname);
                }
                else
                {
                    _itemsCategories.Add(itemCategory, new List<string> {item.shortname});
                }
            });

            _config.Shop.ForEach(category => category.Items.ForEach(item =>
            {
                var id = item.ID;
                if (_shopItems.ContainsKey(item.ID))
                    id = GetId();
                _shopItems.Add(id, item);

                if (item.Discount != null)
                    foreach (var check in item.Discount.Where(check =>
                                 !string.IsNullOrEmpty(check.Key) && !permission.PermissionExists(check.Key)))
                        permission.RegisterPermission(check.Key, this);

                if (item.Type == ItemType.Item && !string.IsNullOrEmpty(item.ShortName))
                    item.Definition = !string.IsNullOrEmpty(item.ShortName)
                        ? ItemManager.FindItemDefinition(item.ShortName)
                        : null;
            }));
        }

        private List<ShopCategory> GetCategories(BasePlayer player, NPCShop npcShop = null)
        {
            return _config.Shop.FindAll(cat => cat != null && cat.Enabled && (string.IsNullOrEmpty(cat.Permission) ||
                                                                              permission.UserHasPermission(
                                                                                  player.UserIDString,
                                                                                  cat.Permission)) &&
                                               (npcShop == null || npcShop.Shops.Contains(cat.Title) ||
                                                npcShop.Shops.Contains("*")));
        }

        private static string HexToCuiColor(string hex, float alpha = 100)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

            var str = hex.Trim('#');
            if (str.Length != 6) throw new Exception(hex);
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {alpha / 100f}";
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

        private DataCart GetPlayerCart(BasePlayer player)
        {
            if (!_carts.ContainsKey(player.userID))
                _carts.Add(player.userID, new DataCart());

            return _carts[player.userID];
        }

        private string FormatShortTime(BasePlayer player, TimeSpan time)
        {
            if (time.Days != 0)
                return Msg(player, DaysFormat, time.Days);

            if (time.Hours != 0)
                return Msg(player, HoursFormat, time.Hours);

            if (time.Minutes != 0)
                return Msg(player, MinutesFormat, time.Minutes);

            if (time.Seconds != 0)
                return Msg(player, SecondsFormat, time.Seconds);

            return string.Empty;
        }

        private void LoadImages()
        {
            timer.In(5, () =>
            {
                if (!ImageLibrary)
                {
                    PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
                }
                else
                {
                    var imagesList = new Dictionary<string, string>();

                    var itemIcons = new List<KeyValuePair<string, ulong>>();

                    _config.Shop.ForEach(category =>
                    {
                        category.Items.ForEach(item =>
                        {
                            if (!string.IsNullOrEmpty(item.Image) && !imagesList.ContainsKey(item.Image))
                                imagesList.Add(item.Image, item.Image);

                            itemIcons.Add(new KeyValuePair<string, ulong>(item.ShortName, item.Skin));
                        });
                    });

                    if (itemIcons.Count > 0) ImageLibrary?.Call("LoadImageList", Title, itemIcons, null);

                    ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
                }
            });
        }

        #endregion

        #region Cooldown

        private readonly Dictionary<BasePlayer, List<ShopItem>> _itemsToUpdate =
            new Dictionary<BasePlayer, List<ShopItem>>();

        private Dictionary<ulong, Cooldown> _cooldown = new Dictionary<ulong, Cooldown>();

        private void ItemsUpdateController()
        {
            var toRemove = Pool.GetList<BasePlayer>();

            var toRemove2 = Pool.GetList<KeyValuePair<BasePlayer, ShopItem>>();

            foreach (var check in _itemsToUpdate)
            {
                if (check.Key == null)
                {
                    toRemove.Add(check.Key);
                    continue;
                }

                var container = new CuiElementContainer();

                check.Value.ToList().ForEach(shopItem =>
                {
                    if (shopItem.Price > 0.0)
                        BuyButtonUi(check.Key, ref container, shopItem);

                    if (shopItem.SellPrice > 0.0)
                        SellButtonUi(check.Key, ref container, shopItem);

                    if (GetCooldownTime(check.Key.userID, shopItem, true) <= 0 &&
                        GetCooldownTime(check.Key.userID, shopItem, false) <= 0)
                        toRemove2.Add(new KeyValuePair<BasePlayer, ShopItem>(check.Key, shopItem));
                });

                CuiHelper.AddUi(check.Key, container);
            }

            toRemove2.ForEach(x => RemoveCooldown(x.Key, x.Value));

            toRemove.ForEach(x => _itemsToUpdate.Remove(x));

            Pool.FreeList(ref toRemove);
            Pool.FreeList(ref toRemove2);
        }

        private Cooldown GetCooldown(ulong player)
        {
            Cooldown cooldown;
            return _cooldown.TryGetValue(player, out cooldown) ? cooldown : null;
        }

        private CooldownData GetCooldown(ulong player, ShopItem item)
        {
            return GetCooldown(player)?.GetCooldown(item);
        }

        private int GetCooldownTime(ulong player, ShopItem item, bool buy)
        {
            return GetCooldown(player)?.GetCooldownTime(item, buy) ?? -1;
        }

        private void SetCooldown(BasePlayer player, ShopItem item, bool buy, bool needUpdate = false)
        {
            if ((buy ? item.BuyCooldown : item.SellCooldown) <= 0) return;

            if (_cooldown.ContainsKey(player.userID))
                _cooldown[player.userID].SetCooldown(item, buy);
            else
                _cooldown.Add(player.userID, new Cooldown().SetCooldown(item, buy));

            if (needUpdate)
            {
                if (_itemsToUpdate.ContainsKey(player))
                {
                    if (!_itemsToUpdate[player].Contains(item))
                        _itemsToUpdate[player].Add(item);
                }
                else
                {
                    _itemsToUpdate.Add(player, new List<ShopItem> {item});
                }
            }
        }

        private void RemoveCooldown(BasePlayer player, ShopItem item)
        {
            if (!_cooldown.ContainsKey(player.userID)) return;

            _itemsToUpdate[player].Remove(item);

            _cooldown[player.userID].RemoveCooldown(item);

            if (_cooldown[player.userID].Data.Count == 0)
            {
                _cooldown.Remove(player.userID);

                _itemsToUpdate.Remove(player);
            }
        }

        private class Cooldown
        {
            #region Fields

            [JsonProperty(PropertyName = "Cooldowns", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly Dictionary<int, CooldownData> Data = new Dictionary<int, CooldownData>();

            #endregion

            #region Utils

            public CooldownData GetCooldown(ShopItem item)
            {
                CooldownData data;
                return Data.TryGetValue(item.ID, out data) ? data : null;
            }

            public int GetCooldownTime(ShopItem item, bool buy)
            {
                var data = GetCooldown(item);
                if (data == null) return -1;

                return (int) ((buy ? data.LastBuyTime : data.LastSellTime).AddSeconds(
                    buy ? item.BuyCooldown : item.SellCooldown) - DateTime.Now).TotalSeconds;
            }

            public void RemoveCooldown(ShopItem item)
            {
                Data.Remove(item.ID);
            }

            public Cooldown SetCooldown(ShopItem item, bool buy)
            {
                if (!Data.ContainsKey(item.ID))
                    Data.Add(item.ID, new CooldownData());

                if (buy)
                    Data[item.ID].LastBuyTime = DateTime.Now;
                else
                    Data[item.ID].LastSellTime = DateTime.Now;

                return this;
            }

            #endregion
        }

        private class CooldownData
        {
            public DateTime LastBuyTime = new DateTime(1970, 1, 1, 0, 0, 0);

            public DateTime LastSellTime = new DateTime(1970, 1, 1, 0, 0, 0);
        }

        #endregion

        #region Limits

        private PlayerLimits _limits;

        private class PlayerLimits
        {
            [JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly Dictionary<ulong, PlayerLimitData> Players = new Dictionary<ulong, PlayerLimitData>();

            public static PlayerLimitData GetOrAdd(ulong member)
            {
                if (!_instance._limits.Players.ContainsKey(member))
                    _instance._limits.Players.Add(member, new PlayerLimitData());

                return _instance._limits.Players[member];
            }
        }

        private class PlayerLimitData
        {
            [JsonProperty(PropertyName = "Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly Dictionary<int, ItemLimitData> ItemsLimits = new Dictionary<int, ItemLimitData>();

            [JsonProperty(PropertyName = "Last Update Time")]
            public DateTime LastUpdate;

            [JsonProperty(PropertyName = "Daily Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly Dictionary<int, ItemLimitData> DailyItemsLimits = new Dictionary<int, ItemLimitData>();

            public void AddItem(ShopItem item, bool buy, int amount, bool daily = false)
            {
                var totalAmount = item.Amount * amount;

                var dict = daily ? DailyItemsLimits : ItemsLimits;

                if (!dict.ContainsKey(item.ID)) dict.Add(item.ID, new ItemLimitData());

                if (buy)
                    dict[item.ID].Buy += totalAmount;
                else
                    dict[item.ID].Sell += totalAmount;
            }

            public int GetLimit(ShopItem item, bool buy, bool daily = false)
            {
                if (daily && DateTime.Now.Date != LastUpdate.Date) // auto wipe
                {
                    LastUpdate = DateTime.Now;
                    DailyItemsLimits.Clear();
                }

                ItemLimitData data;
                return (daily ? DailyItemsLimits : ItemsLimits).TryGetValue(item.ID, out data)
                    ? buy ? data.Buy : data.Sell
                    : 0;
            }
        }

        private class ItemLimitData
        {
            public int Sell;

            public int Buy;
        }

        private void UseLimit(BasePlayer player, ShopItem item, bool buy, int amount, bool daily = false)
        {
            PlayerLimits.GetOrAdd(player.userID).AddItem(item, buy, amount, daily);
        }

        private int GetLimit(BasePlayer player, ShopItem item, bool buy, bool daily = false)
        {
            var hasLimit = item.GetLimit(player, buy, daily);
            if (hasLimit == 0)
                return 1;

            var used = PlayerLimits.GetOrAdd(player.userID).GetLimit(item, buy, daily);
            return hasLimit - used;
        }

        private static bool HasLimit(BasePlayer player, ShopItem item, bool buy, out int leftAmount, bool daily = false)
        {
            var hasLimit = item.GetLimit(player, buy, daily);
            if (hasLimit == 0)
            {
                leftAmount = 0;
                return false;
            }

            var used = PlayerLimits.GetOrAdd(player.userID).GetLimit(item, buy, daily);
            leftAmount = hasLimit - used;
            return true;
        }

        #endregion

        #region Log

        private void Log(string filename, string key, params object[] obj)
        {
            var text = string.Format(lang.GetMessage(key, this), obj);
            if (_config.LogToConsole) Puts(text);

            if (_config.LogToFile) LogToFile(filename, $"[{DateTime.Now}] {text}", this);
        }

        #endregion

        #region Lang

        private const string
            LogSellItem = "LogSellItem",
            LogBuyItems = "LogBuyItems",
            SkinBlocked = "SkinBlocked",
            NoUseDuel = "NoUseDuel",
            DailySellLimitReached = "DailySellLimitReached",
            DailyBuyLimitReached = "DailyBuyLimitReached",
            SellLimitReached = "SellLimitReached",
            BuyLimitReached = "BuyLimitReached",
            InfoTitle = "InfoTitle",
            BuyRaidBlocked = "BuyRaidBlocked",
            SellRaidBlocked = "SellRaidBlocked",
            DaysFormat = "DaysFormat",
            HoursFormat = "HoursFormat",
            MinutesFormat = "MinutesFormat",
            SecondsFormat = "SecondsFormat",
            NotEnoughtSpace = "NotEnoughtSpace",
            NotMoney = "NotMoney",
            ReceivedItems = "GiveItem",
            BalanceTitle = "BalanceTitle",
            BuyTitle = "BuyTitle",
            SellTitle = "SellTitle",
            ItemsTitle = "ItemsTitle",
            CostTitle = "CostTitle",
            PlusTitle = "PlusTitle",
            MinusTitle = "MinusTitle",
            RemoveTitle = "RemoveTitle",
            AmountTitle = "AmountTitle",
            NextTitle = "NextTitle",
            BackTitle = "BackTitle",
            ItemPrice = "ItemPrice",
            ItemAmount = "ItemAmount",
            CloseButton = "CloseButton",
            YourBalance = "YourBalance",
            MainTitle = "MainTitle",
            CategoriesTitle = "CategoriesTitle",
            ShoppingBag = "ShoppingBag",
            PurchaseConfirmation = "PurchaseConfirmation",
            CancelTitle = "CancelTitle",
            ErrorClose = "ErrorClose",
            BtnSave = "BtnSave",
            ErrorMsg = "ErrorMsg",
            NotEnough = "NotEnough",
            Back = "Back",
            Next = "Next",
            SellBtn = "SellBtn",
            ItemName = "ItemName",
            CmdName = "CmdName",
            RemoveItem = "RemoveItem",
            ItemSearch = "ItemSearch",
            PluginName = "PluginName",
            BtnSelect = "BtnSelect",
            AddItem = "AddItem",
            EditingTitle = "EditingTitle",
            SearchTitle = "SearchTitle",
            BackPage = "BackPage",
            NextPage = "NextPage",
            SellCooldownTitle = "SellCooldownTitle",
            BuyCooldownTitle = "BuyCooldownTitle",
            BuyCooldownMessage = "BuyCooldownMessage",
            SellCooldownMessage = "SellCooldownMessage",
            BtnNext = "BtnNext",
            BtnBack = "BtnBack",
            SellNotify = "SellNotify";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [DaysFormat] = " {0} d. ",
                [HoursFormat] = " {0} h. ",
                [MinutesFormat] = " {0} m. ",
                [SecondsFormat] = " {0} s. ",
                [NotEnoughtSpace] = "Not enought space",
                [NotMoney] = "You don't have enough money!",
                [ReceivedItems] = "All items received!",
                [BalanceTitle] = "{0} RP",
                [BuyTitle] = "Buy",
                [SellTitle] = "Sell",
                [ItemsTitle] = "Items\n<b>{0} pcs</b>",
                [CostTitle] = "Cost\n<b>{0} RP</b>",
                [PlusTitle] = "+",
                [MinusTitle] = "-",
                [RemoveTitle] = "Remove",
                [AmountTitle] = "Amount {0} pcs",
                [BackTitle] = "Back",
                [NextTitle] = "Next",
                [ItemPrice] = "{0} RP",
                [ItemAmount] = "Amt.",
                [CloseButton] = "✕",
                [YourBalance] = "Your Balance",
                [MainTitle] = "Shop",
                [CategoriesTitle] = "Categories",
                [ShoppingBag] = "Shopping Bag",
                [PurchaseConfirmation] = "Purchase confirmation",
                [CancelTitle] = "Cancel",
                [ErrorClose] = "CLOSE",
                [ErrorMsg] = "XXX",
                [NotEnough] = "You don't have enough item!",
                [BtnSelect] = "Select",
                [EditingTitle] = "Item editing",
                [ItemSearch] = "Item search",
                [Back] = "Back",
                [Next] = "Next",
                [RemoveItem] = "✕",
                [BtnSave] = "Save",
                [ItemName] = "Item",
                [CmdName] = "Command",
                [PluginName] = "Plugin",
                [AddItem] = "Add Item",
                [SellBtn] = "SELL FOR {0} RP",
                [SearchTitle] = "Search...",
                [BackPage] = "<",
                [NextPage] = ">",
                [SellCooldownTitle] = "Cooldown",
                [BuyCooldownTitle] = "Cooldown",
                [BuyCooldownMessage] = "You cannot buy the '{0}' item! Wait {1}",
                [SellCooldownMessage] = "You cannot sell the '{0}' item! Wait {1}",
                [BtnBack] = "▲",
                [BtnNext] = "▼",
                [SellNotify] = "You have successfully sold {0} pcs of {1}",
                [BuyRaidBlocked] = "You can't buy while blocked!",
                [SellRaidBlocked] = "You can't sell while blocked!",
                [InfoTitle] = "i",
                [DailyBuyLimitReached] =
                    "You cannot buy the '{0}'. You have reached the daily limit. Come back tomorrow!",
                [DailySellLimitReached] =
                    "You cannot buy the '{0}'. You have reached the daily limit. Come back tomorrow!",
                [BuyLimitReached] = "You cannot buy the '{0}'. You have reached the limit",
                [SellLimitReached] = "You cannot sell the '{0}'. You have reached the limit",
                [NoUseDuel] = "You are in a duel. The use of the shop is blocked.",
                [SkinBlocked] = "Skin is blocked for sale",
                [LogBuyItems] = "Player {0} ({1}) bought items for {2}$: {3}.",
                [LogSellItem] = "Player {0} ({1}) sold item for {2}$: {3}."
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
    }
}