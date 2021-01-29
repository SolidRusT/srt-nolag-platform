using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.IO;
using Rust;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;

/********************************************************
 *  Follow the status on https://trello.com/b/BCm6PUwK/guishop   __φ(．．)

 *  Credits to Nogrod and Reneb for the original plugin. <Versions up to 1.4.6
 *  Thanks! to Default for maintaining and adding in feature updates over the years.  <versions 1.4.65 to 1.5.9

 *  Current Maintainer: 8/14/2020 Khan#8615 discord ID.  1.6.0  to Present.
 *  Imported Auto Config Updater by WhiteThunder! ◝(⁰▿⁰)◜
 *  Thanks to Baz/Hockeygel/Whispers
 * -----------------------------------------------------------

 *  TODO:
 *  Finish implementing limiter function
 * Add Theme Switcher
 * Add private VIP shops
 * Add Sell All from inventory feature
 * Add BLuePrint option
 *******************************************************/

/*****
* This Update 2.0.3
* Bug fix..
*/

namespace Oxide.Plugins
{
    [Info("GUIShop", "Khan", "2.0.3")]
    [Description("GUI Shop Supports all known Currency, with NPC support - Re-Write Edition")]
    public class GUIShop : RustPlugin
    {
        #region References
        [PluginReference] Plugin Economics, Kits, ImageLibrary, ServerRewards;
        #endregion

        #region Fields
        private const string ShopOverlayName = "ShopOverlay";
        private const string ShopContentName = "ShopContent";
        private const string ShopDescOverlay = "ShopDescOverlay";
        private const string ShopColorPicker = "ShopColorPicker";
        private const string BlockAllow = "guishop.BlockByPass";
        private const string Use = "guishop.use";
        private const string Admin = "guishop.admin";
        private const string Vip = "guishop.vip";
        private const string Color = "guishop.color";

        readonly Hash<ulong, int> _shopPage = new Hash<ulong, int>();
        private Dictionary<ulong, Dictionary<string, double>> _sellCoolDownData;
        private Dictionary<ulong, Dictionary<string, double>> _buyCooldownData;
        private Dictionary<string, ulong> _boughtData;
        private Dictionary<string, ulong> _soldData;
        private Dictionary<ulong, ItemLimit> _limitsData;
        private List<MonumentInfo> _monuments => TerrainMeta.Path.Monuments;
        private bool _configChanged;
        int playersMask = LayerMask.GetMask("Player (Server)");
        private bool _isShopReady;

        private const string BackgroundImage = "Background";

        //Auto Close
        private readonly HashSet<string> _playerUIOpen = new HashSet<string>();

        private static GUIShop _instance;
        private Dictionary<string, PlayerUISetting> _playerUIData;
        private string _uiSettingChange = "Text";
        private bool _imageChanger;
        private double Transparency = 0.95;
        private PluginConfig _config;
        #endregion

        #region Config

        private readonly HashSet<string> _exclude = new HashSet<string>
        {
            "vehicle.chassis",
            "vehicle.module"
        };

        internal class PluginConfig : SerializableConfiguration
        {
            [JsonProperty("Set Default Global Shop to open")]
            public string DefaultShop = "Commands";

            [JsonProperty("Switches to Economics as default curency")]
            public bool Economics = true;
            
            [JsonProperty("Switches to ServerRewards as default curency")]
            public bool ServerRewards = false;
            
            [JsonProperty("Switches to Custom as default curency")]
            public bool CustomCurrency = false;
            
            [JsonProperty("Custom Currency Item ID")]
            public int CustomCurrencyID = -932201673;

            [JsonProperty("Was Saved Don't Touch!")]
            public bool WasSaved;

            [JsonProperty("Sets shop command")]
            public string shopcommand = "shop";

            [JsonProperty("Player UI display")]
            public bool PersonalUI = false;

            [JsonProperty("Block Monuments")]
            public bool BlockMonuments = false;

            [JsonProperty("If true = Images, If False = Text Labels")]
            public bool UIImageOption = false;
            
            [JsonProperty("Enable NPC Auto Open")]
            public bool NPCAutoOpen = false;

            [JsonProperty("Enable GUIShop NPC Msg's")]
            public bool NPCLeaveResponse = false;

            [JsonProperty("GUI Shop - Welcome MSG")]
            public string WelcomeMsg = "WELCOME TO GUISHOP ◝(⁰▿⁰)◜";

            [JsonProperty("Shop - Buy Price Label")]
            public string BuyLabel = "Buy Price";

            [JsonProperty("Shop - Amount1 Label1")]
            public string AmountLabel = "Amount";

            [JsonProperty("Shop - Sell $ Label")]
            public string SellLabel = "Sell $";

            [JsonProperty("Shop - Amount2 Label2")]
            public string AmountLabel2 = "Amount";

            [JsonProperty("Shop - Close Label")]
            public string CloseButtonlabel = "CLOSE";

            [JsonProperty("Shop - GUIShop Welcome Url")]
            public string GuiShopWelcomeUrl = "https://i.imgur.com/RcLdEly.png";

            [JsonProperty("Shop - GUIShop Background Image Url")] //setting this results in all shop items having the same Icon.
            public string BackgroundUrl = "https://i.imgur.com/Jej3cwR.png";

            [JsonProperty("Shop - Sets any shop items to this image if image link does not exist.")]
            public string IconUrl = "https://imgur.com/BPM9UR4.png";

            [JsonProperty("Shop - Shop Buy Icon Url")]
            public string BuyIconUrl = "https://imgur.com/oeVUwCy.png";

            [JsonProperty("Shop - Shop Amount Image1")]
            public string AmountUrl = "https://imgur.com/EKtvylU.png";

            [JsonProperty("Shop - Shop Amount Image2")]
            public string AmountUrl2 = "https://imgur.com/EKtvylU.png";

            [JsonProperty("Shop - Shop Sell Icon Url")]
            public string SellIconUrl = "https://imgur.com/jV3hEHy.png";

            [JsonProperty("Shop - Close Image Url")]
            public string CloseButton = "https://imgur.com/IK5yVrW.png";

            [JsonProperty("Enable Shop Buy/Sell All Button")]
            public bool AllButton = false;

            [JsonProperty("Sets the buy/Sell button amounts + how many buttons")]
            public int[] steps = { 1, 10, 100, 1000 };

            [JsonProperty("GUIShop Configurable UI colors (First 8 Colors!)")]
            public HashSet<string> ColorsUI = new HashSet<string>();

            [JsonProperty("Shop - Shop Categories")]
            public Dictionary<string, ShopCategory> ShopCategories = new Dictionary<string, ShopCategory>();

            [JsonProperty("Shop - Shop List")]
            public Dictionary<string, ShopItem> ShopItems = new Dictionary<string, ShopItem>();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        public class ShopItem
        {
            public string DisplayName;
            public string Shortname;
            public bool EnableBuy;
            public bool EnableSell;
            public string Image;
            public double SellPrice;
            public double BuyPrice;
            public int BuyCooldown;
            public int SellCooldown;
            //public int BuyLimit;
            //public int SellLimit;
            public string KitName;
            public List<string> Command;
            public ulong SkinId;
        }

        public class ShopCategory
        {
            public string DisplayName;
            public string Description;
            public bool EnabledCategory;
            //public bool BluePrints;
            public bool EnableNPC;
            public string NPCId;
            public HashSet<string> Items = new HashSet<string>();
        }

        class ItemLimit //limiter function TODO:
        {
            public readonly Dictionary<string, int> Buy = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Sell = new Dictionary<string, int>();

            public bool HasSellLimit(string item, int amount)
            {
                if (!Sell.ContainsKey(item))
                {
                    Sell[item] = 1;
                }

                return Sell[item] >= amount;
            }

            public bool HasBuyLimit(string item, int amount)
            {
                if (!Buy.ContainsKey(item))
                {
                    Buy[item] = 1;
                }

                return Buy[item] >= amount;
            }

            public void IncrementBuy(string item)
            {
                if (!Buy.ContainsKey(item))
                    Buy[item] = 1;
                else
                    Buy[item]++;
            }

            public void IncrementSell(string item)
            {
                if (!Sell.ContainsKey(item))
                    Sell[item] = 1;
                else
                    Sell[item]++;
            }
        }

        private class PlayerUISetting
        {
            public double Transparency;
            public string SellBoxColors;
            public string BuyBoxColors;
            public string UITextColor;
            public double RangeValue;
            public bool ImageOrText;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }

            }
            catch
            {
                LoadDefaultConfig();

                PrintToConsole($"Please verify your {Name}.json config at <http://pro.jsonlint.com/>.");
            }
        }

        protected override void SaveConfig()
        {
            PrintToConsole($"Configuration changes saved to {Name}.json");

            Config.WriteObject(_config);
        }

        private void CheckConfig()
        {

            if (!_config.ShopCategories.ContainsKey("Commands"))
            {
                _config.ShopCategories.Add("Commands", new ShopCategory
                {
                    DisplayName = "Commands",
                    Description = "You currently have {0} coins to spend in the commands shop",
                    EnabledCategory = true,
                    //BluePrints = false
                });
                _configChanged = true;
            }

            if (_config.ShopCategories.ContainsKey("Commands") && !_config.ShopItems.ContainsKey("Minicopter") && !_config.ShopItems.ContainsKey("Sedan") && !_config.ShopItems.ContainsKey("Airdrop Call"))
            {
                _config.ShopItems.Add("Minicopter", new ShopItem
                {
                    DisplayName = "Minicopter",
                    Shortname = "minicopter",
                    EnableBuy = true,
                    EnableSell = false,
                    Image = "https://i.imgur.com/vI6LwCZ.png",
                    BuyPrice = 1.0,
                    SellPrice = 1.0,
                    BuyCooldown = 0,
                    SellCooldown = 0,
                    KitName = null,
                    Command = new List<string> { "spawn minicopter \"$player.x $player.y $player.z\"" },
                });

                _config.ShopItems.Add("Sedan", new ShopItem
                {
                    DisplayName = "Sedan",
                    Shortname = "sedan",
                    EnableBuy = true,
                    EnableSell = false,
                    Image = "",
                    BuyPrice = 1.0,
                    SellPrice = 1.0,
                    BuyCooldown = 0,
                    SellCooldown = 0,
                    KitName = null,
                    Command = new List<string> { "spawn sedan \"$player.x $player.y $player.z\"" },
                });

                _config.ShopItems.Add("Airdrop Call", new ShopItem
                {
                    DisplayName = "Airdrop Call",
                    Shortname = "airdrop.call",
                    EnableBuy = true,
                    EnableSell = true,
                    Image = "",
                    BuyPrice = 1.0,
                    SellPrice = 1.0,
                    BuyCooldown = 0,
                    SellCooldown = 0,
                    KitName = null,
                    Command = new List<string> { "inventory.giveto $player.id supply.signal" },
                });

                _config.ShopCategories["Commands"].Items.Add("Minicopter");
                _config.ShopCategories["Commands"].Items.Add("Sedan");
                _config.ShopCategories["Commands"].Items.Add("Airdrop Call");

                _configChanged = true;
            }

            foreach (ItemDefinition item in ItemManager.itemList)
            {
                string categoryName = item.category.ToString();

                ShopCategory shopCategory;

                if (!_config.ShopCategories.TryGetValue(categoryName, out shopCategory))
                {
                    _config.ShopCategories[categoryName] = shopCategory = new ShopCategory
                    {
                        DisplayName = item.category.ToString(),
                        Description = "You currently have {0} coins to spend in the " + item.category + " shop",
                        EnabledCategory = true,
                        //BluePrints = false
                    };

                    _configChanged = true;
                }

                if (_exclude.Contains(item.shortname)) continue;

                if (!shopCategory.Items.Contains(item.displayName.english) && !_config.WasSaved)
                {
                    shopCategory.Items.Add(item.displayName.english);

                    _configChanged = true;
                }

                if (!_config.ShopItems.ContainsKey(item.displayName.english))
                {
                    _config.ShopItems.Add(item.displayName.english, new ShopItem
                    {
                        DisplayName = item.displayName.english,
                        Shortname = item.shortname,
                        EnableBuy = true,
                        EnableSell = true,
                        BuyPrice = 1.0,
                        SellPrice = 1.0,
                        Image = "https://rustlabs.com/img/items180/" + item.shortname + ".png"
                    });

                    _configChanged = true;
                }
            }

            if (_config.ColorsUI.Count <= 0)
            {
                _config.ColorsUI = new HashSet<string> { "#A569BD", "#2ECC71", "#E67E22", "#3498DB", "#E74C3C", "#F1C40F", "#F4F6F7", "#00FFFF" };
                _configChanged = true;
            }

            if (_configChanged)
            {
                _config.WasSaved = true;
                SaveConfig();
            }
        }

        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>().ToDictionary(prop => prop.Name, prop => ToObject(prop.Value));
                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        private void LoadImages()
        {
            Dictionary<string, string> imageListGUIShop = new Dictionary<string, string>();

            foreach (ShopItem shopItem in _config.ShopItems.Values)
            {
                if (string.IsNullOrEmpty(shopItem.Image) || shopItem.Shortname.Contains("hazmatsuit.spacesuit")) continue;
                
                if (imageListGUIShop.ContainsKey(shopItem.Image)) continue; //Fixed

                imageListGUIShop.Add(shopItem.Image, shopItem.Image);
            }

            ImageLibrary?.Call("AddImage", _config.BackgroundUrl, BackgroundImage);

            ImageLibrary?.Call("AddImage", _config.IconUrl, _config.IconUrl);

            if (imageListGUIShop.Count > 0)
            {
                ImageLibrary?.Call("ImportImageList", Title, imageListGUIShop);
            }
            ImageLibrary?.Call("LoadImageList", Title, _config.ShopItems.Where(y => string.IsNullOrEmpty(y.Value.Image)).Select(x => new KeyValuePair<string, ulong>(x.Value.Image, x.Value.SkinId)).ToList(), new Action(ShopReady));

            if (!ImageLibrary)
            {
                _isShopReady = true;
            }
        }

        private void ShopReady()
        {
            _isShopReady = true;
        }

        #endregion

        #region Storage
        private void LoadData()
        {
            try
            {
                _buyCooldownData = _buyCoolDowns.ReadObject<Dictionary<ulong, Dictionary<string, double>>>();
            }
            catch
            {
                _buyCooldownData = new Dictionary<ulong, Dictionary<string, double>>();
            }
            try
            {
                _sellCoolDownData = _sellCoolDowns.ReadObject<Dictionary<ulong, Dictionary<string, double>>>();
            }
            catch
            {
                _sellCoolDownData = new Dictionary<ulong, Dictionary<string, double>>();
            }
            try
            {
                _boughtData = _bought.ReadObject<Dictionary<string, ulong>>();
            }
            catch
            {
                _boughtData = new Dictionary<string, ulong>();
            }
            try
            {
                _soldData = _sold.ReadObject<Dictionary<string, ulong>>();
            }
            catch
            {
                _soldData = new Dictionary<string, ulong>();
            }
            try
            {
                _limitsData = _limits.ReadObject<Dictionary<ulong, ItemLimit>>();
            }
            catch
            {
                _limitsData = new Dictionary<ulong, ItemLimit>();
            }
            try
            {
                _playerUIData = _playerData.ReadObject<Dictionary<string, PlayerUISetting>>();
            }
            catch
            {
                _playerUIData = new Dictionary<string, PlayerUISetting>();
            }
        }

        private void SaveData()
        {
            _buyCoolDowns.WriteObject(_buyCooldownData);
            _sellCoolDowns.WriteObject(_sellCoolDownData);
            _bought.WriteObject(_boughtData);
            _sold.WriteObject(_soldData);
            _limits.WriteObject(_limitsData);
            _playerData.WriteObject(_playerUIData);
        }
        #endregion

        #region Lang File Messages

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MessageShowNoEconomics", "GUIShop did not recieve a response from the Economics plugin. Please ensure the Economics plugin is installed correctly. "},
                {"MessageShowNoServerRewards", "GUIShop did not recieve a response from the ServerRewards plugin. Please ensure the ServerRewards plugin is installed correctly. "},
                {"MessageBought", "You've successfully bought {0} {1}."},
                {"MessageSold", "You've successfully sold {0} {1}. "},
                {"MessageErrorCooldown", "You can only purchase this item every {0} seconds."},
                {"MessageErrorSellCooldownAmount", "You can only sell {0} of this item every {1} seconds."},
                {"MessageErrorBuyCooldownAmount", "You can only buy {0} of this item every {1} seconds."},
                {"MessageErrorBuyLimit", "You can only buy {0} of this item."},
                {"MessageErrorSellLimit", "You can only sell {0} of this item."},
                {"MessageErrorInventoryFull", "Your inventory is full."},
                {"MessageErrorInventorySlots", "You need at least {0} free inventory slots."},
                {"MessageErrorNoShop", "There is something wrong with this shop. Please contact an admin."},
                {"MessageErrorGlobalDisabled", "Global Shops are disabled. This server uses NPC vendors!"},
                {"MessageErrorNoActionShop", "You are not allowed to {0} in this shop"},
                {"MessageErrorNoActionItem", "You are not allowed to {0} this item here"},
                {"MessageErrorItemItem", "WARNING: It seems like this sell item you have is not a valid item! Please contact an Admin!"},
                {"MessageErrorItemNoValidbuy", "WARNING: It seems like it's not a valid item to buy, Please contact an Admin!"},
                {"MessageErrorItemNoValidsell", "WARNING: It seems like it's not a valid item to sell, Please contact an Admin!"},
                {"MessageErrorRedeemKit", "WARNING: There was an error while giving you this kit, Please contact an Admin!"},
                {"MessageErrorBuyCmd", "Can't buy multiple of this item!"},
                {"MessageErrorBuyPrice", "WARNING: No buy price was given by the admin, you can't buy this item"},
                {"MessageErrorSellPrice", "WARNING: No sell price was given by the admin, you can't sell this item"},
                {"MessageErrorNotEnoughMoney", "You need {0} coins to buy {1}x {2}"},
                {"MessageErrorNotEnoughMoneyCustom", "You need {0} currency to buy {1}x {2}"},
                {"MessageErrorNotEnoughSell", "You don't have enough of this item."},
                {"MessageErrorNotNothing", "You cannot buy Zero of this item."},
                {"MessageErrorItemNoExist", "WARNING: The item you are trying to buy doesn't seem to exist! Please contact an Admin!"},
                {"MessageErrorItemNoExistTake", "The item you are trying to sell is not sellable at this time."},
                {"MessageErrorBuildingBlocked", "You cannot shop while in a building blocked area. "},
                {"MessageErrorAdmin", "You do not have the correct permissions to use this command. (guishop.admin)"},
                {"MessageErrorWaitingOnDownloads", "GUIShop is waiting on ImageLibrary downloads to finish"},
                {"BlockedMonuments", "You may not use the shop while near a Monument!"},
                {"MessageErrorItemNotEnabled", "The shop keeper has disabled this item."},
                {"MessageErrorItemNotFound", "Item was not found"},
                {"CantSellCommands", "You can not sell Commands back to the shop."},
                {"CantSellKits", "You can not sell Kits back to the shop."},
                {"MessageErrorCannotSellWhileEquiped", "You can not sell the item if you have it Equipt."},
                {"MessageShopResponse", "GUIShop is waiting for ImageLibrary downloads to finish please wait."},
                {"MessageNPCResponseclose", "Thanks for shopping at {0} come again soon!"},
                {"MessageNPCResponseopen", "Welcome to the {0} what would you like to purchase? Press E to start shopping!"},
                {"Commands", "Commands"},
                {"Attire", "Attire"},
                {"Misc", "Misc"},
                {"Items", "Items"},
                {"Ammunition", "Ammunition"},
                {"Construction", "Construction"},
                {"Component", "Component"},
                {"Traps", "Traps"},
                {"Electrical", "Electrical"},
                {"Fun", "Fun"},
                {"Food", "Food"},
                {"Resources", "Resources"},
                {"Tool", "Tool"},
                {"Weapon", "Weapon"},
                {"Medical", "Medical"},
                {"Minicopter", "Minicopter"},
                {"Sedan", "Sedan"},
                {"Airdrop Call", "Airdrop Call"},
                {"Wolf Headdress", "Wolf Headdress"},
                {"Fogger-3000", "Fogger-3000"},
                {"Strobe Light", "Strobe Light"},
                {"Kayak", "Kayak"},
                {"MC repair", "MC repair"},
                {"ScrapTransportHeliRepair", "ScrapTransportHeliRepair"},
                {"40mm Shotgun Round", "40mm Shotgun Round"},
                {"40mm HE Grenade", "40mm HE Grenade"},
                {"40mm Smoke Grenade", "40mm Smoke Grenade"},
                {"High Velocity Arrow", "High Velocity Arrow"},
                {"Wooden Arrow", "Wooden Arrow"},
                {"Bone Arrow", "Bone Arrow"},
                {"Fire Arrow", "Fire Arrow"},
                {"Handmade Shell", "Handmade Shell"},
                {"Nailgun Nails", "Nailgun Nails"},
                {"Pistol Bullet", "Pistol Bullet"},
                {"Incendiary Pistol Bullet", "Incendiary Pistol Bullet"},
                {"HV Pistol Ammo", "HV Pistol Ammo"},
                {"5.56 Rifle Ammo", "5.56 Rifle Ammo"},
                {"Explosive 5.56 Rifle Ammo", "Explosive 5.56 Rifle Ammo"},
                {"Incendiary 5.56 Rifle Ammo", "Incendiary 5.56 Rifle Ammo"},
                {"HV 5.56 Rifle Ammo", "HV 5.56 Rifle Ammo"},
                {"Rocket", "Rocket"},
                {"Incendiary Rocket", "Incendiary Rocket"},
                {"High Velocity Rocket", "High Velocity Rocket"},
                {"Smoke Rocket WIP!!!!", "Smoke Rocket WIP!!!!"},
                {"12 Gauge Buckshot", "12 Gauge Buckshot"},
                {"12 Gauge Incendiary Shell", "12 Gauge Incendiary Shell"},
                {"12 Gauge Slug", "12 Gauge Slug"},
                {"Sheet Metal Double Door", "Sheet Metal Double Door"},
                {"Armored Double Door", "Armored Double Door"},
                {"Wood Double Door", "Wood Double Door"},
                {"Sheet Metal Door", "Sheet Metal Door"},
                {"Armored Door", "Armored Door"},
                {"Wooden Door", "Wooden Door"},
                {"Floor grill", "Floor grill"},
                {"Ladder Hatch", "Ladder Hatch"},
                {"Floor triangle grill", "Floor triangle grill"},
                {"Triangle Ladder Hatch", "Triangle Ladder Hatch"},
                {"High External Stone Gate", "High External Stone Gate"},
                {"High External Wooden Gate", "High External Wooden Gate"},
                {"Wooden Ladder", "Wooden Ladder"},
                {"High External Stone Wall", "High External Stone Wall"},
                {"High External Wooden Wall", "High External Wooden Wall"},
                {"Prison Cell Gate", "Prison Cell Gate"},
                {"Prison Cell Wall", "Prison Cell Wall"},
                {"Chainlink Fence Gate", "Chainlink Fence Gate"},
                {"Chainlink Fence", "Chainlink Fence"},
                {"Garage Door", "Garage Door"},
                {"Netting", "Netting"},
                {"Shop Front", "Shop Front"},
                {"Metal Shop Front", "Metal Shop Front"},
                {"Metal Window Bars", "Metal Window Bars"},
                {"Reinforced Window Bars", "Reinforced Window Bars"},
                {"Wooden Window Bars", "Wooden Window Bars"},
                {"Metal horizontal embrasure", "Metal horizontal embrasure"},
                {"Metal Vertical embrasure", "Metal Vertical embrasure"},
                {"Reinforced Glass Window", "Reinforced Glass Window"},
                {"Wood Shutters", "Wood Shutters"},
                {"Watch Tower", "Watch Tower"},
                {"Diving Fins", "Diving Fins"},
                {"Diving Mask", "Diving Mask"},
                {"Diving Tank", "Diving Tank"},
                {"Wetsuit", "Wetsuit"},
                {"Frog Boots", "Frog Boots"},
                {"A Barrel Costume", "A Barrel Costume"},
                {"Crate Costume", "Crate Costume"},
                {"Burlap Gloves", "Burlap Gloves"},
                {"Leather Gloves", "Leather Gloves"},
                {"Roadsign Gloves", "Roadsign Gloves"},
                {"Tactical Gloves", "Tactical Gloves"},
                {"Ghost Costume", "Ghost Costume"},
                {"Mummy Suit", "Mummy Suit"},
                {"Scarecrow Suit", "Scarecrow Suit"},
                {"Scarecrow Wrap", "Scarecrow Wrap"},
                {"Hide Halterneck", "Hide Halterneck"},
                {"Beenie Hat", "Beenie Hat"},
                {"Boonie Hat", "Boonie Hat"},
                {"Bucket Helmet", "Bucket Helmet"},
                {"Burlap Headwrap", "Burlap Headwrap"},
                {"Candle Hat", "Candle Hat"},
                {"Baseball Cap", "Baseball Cap"},
                {"Clatter Helmet", "Clatter Helmet"},
                {"Coffee Can Helmet", "Coffee Can Helmet"},
                {"Bone Helmet", "Bone Helmet"},
                {"Heavy Plate Helmet", "Heavy Plate Helmet"},
                {"Miners Hat", "Miners Hat"},
                {"Party Hat", "Party Hat"},
                {"Riot Helmet", "Riot Helmet"},
                {"Wood Armor Helmet", "Wood Armor Helmet"},
                {"Hoodie", "Hoodie"},
                {"Bone Armor", "Bone Armor"},
                {"Heavy Plate Jacket", "Heavy Plate Jacket"},
                {"Snow Jacket", "Snow Jacket"},
                {"Jacket", "Jacket"},
                {"Wood Chestplate", "Wood Chestplate"},
                {"Improvised Balaclava", "Improvised Balaclava"},
                {"Bandana Mask", "Bandana Mask"},
                {"Metal Facemask", "Metal Facemask"},
                {"Night Vision Goggles", "Night Vision Goggles"},
                {"Burlap Trousers", "Burlap Trousers"},
                {"Heavy Plate Pants", "Heavy Plate Pants"},
                {"Hide Pants", "Hide Pants"},
                {"Road Sign Kilt", "Road Sign Kilt"},
                {"Shorts", "Shorts"},
                {"Wood Armor Pants", "Wood Armor Pants"},
                {"Pants", "Pants"},
                {"Hide Poncho", "Hide Poncho"},
                {"Burlap Shirt", "Burlap Shirt"},
                {"Shirt", "Shirt"},
                {"Hide Vest", "Hide Vest"},
                {"Tank Top", "Tank Top"},
                {"Boots", "Boots"},
                {"Burlap Shoes", "Burlap Shoes"},
                {"Hide Boots", "Hide Boots"},
                {"Hide Skirt", "Hide Skirt"},
                {"Bandit Guard Gear", "Bandit Guard Gear"},
                {"Hazmat Suit", "Hazmat Suit"},
                {"Scientist Suit", "Scientist Suit"},
                {"Space Suit", "Space Suit"},
                {"Heavy Scientist Suit", "Heavy Scientist Suit"},
                {"Longsleeve T-Shirt", "Longsleeve T-Shirt"},
                {"T-Shirt", "T-Shirt"},
                {"Metal Chest Plate", "Metal Chest Plate"},
                {"Road Sign Jacket", "Road Sign Jacket"},
                {"Bleach", "Bleach"},
                {"Duct Tape", "Duct Tape"},
                {"Low Quality Carburetor", "Low Quality Carburetor"},
                {"Medium Quality Carburetor", "Medium Quality Carburetor"},
                {"High Quality Carburetor", "High Quality Carburetor"},
                {"Low Quality Crankshaft", "Low Quality Crankshaft"},
                {"Medium Quality Crankshaft", "Medium Quality Crankshaft"},
                {"High Quality Crankshaft", "High Quality Crankshaft"},
                {"Low Quality Pistons", "Low Quality Pistons"},
                {"Medium Quality Pistons", "Medium Quality Pistons"},
                {"High Quality Pistons", "High Quality Pistons"},
                {"Low Quality Spark Plugs", "Low Quality Spark Plugs"},
                {"Medium Quality Spark Plugs", "Medium Quality Spark Plugs"},
                {"High Quality Spark Plugs", "High Quality Spark Plugs"},
                {"Low Quality Valves", "Low Quality Valves"},
                {"Medium Quality Valves", "Medium Quality Valves"},
                {"High Quality Valves", "High Quality Valves"},
                {"Electric Fuse", "Electric Fuse"},
                {"Gears", "Gears"},
                {"Glue", "Glue"},
                {"Metal Blade", "Metal Blade"},
                {"Metal Pipe", "Metal Pipe"},
                {"Empty Propane Tank", "Empty Propane Tank"},
                {"Road Signs", "Road Signs"},
                {"Rope", "Rope"},
                {"Sewing Kit", "Sewing Kit"},
                {"Sheet Metal", "Sheet Metal"},
                {"Metal Spring", "Metal Spring"},
                {"Sticks", "Sticks"},
                {"Tarp", "Tarp"},
                {"Tech Trash", "Tech Trash"},
                {"Rifle Body", "Rifle Body"},
                {"Semi Automatic Body", "Semi Automatic Body"},
                {"SMG Body", "SMG Body"},
                {"Concrete Barricade", "Concrete Barricade"},
                {"Wooden Barricade Cover", "Wooden Barricade Cover"},
                {"Metal Barricade", "Metal Barricade"},
                {"Sandbag Barricade", "Sandbag Barricade"},
                {"Stone Barricade", "Stone Barricade"},
                {"Wooden Barricade", "Wooden Barricade"},
                {"Barbed Wooden Barricade", "Barbed Wooden Barricade"},
                {"Barbeque", "Barbeque"},
                {"Snap Trap", "Snap Trap"},
                {"Bed", "Bed"},
                {"Camp Fire", "Camp Fire"},
                {"Ceiling Light", "Ceiling Light"},
                {"Chair", "Chair"},
                {"Composter", "Composter"},
                {"Computer Station", "Computer Station"},
                {"Drop Box", "Drop Box"},
                {"Elevator", "Elevator"},
                {"Stone Fireplace", "Stone Fireplace"},
                {"Blue Boomer", "Blue Boomer"},
                {"Champagne Boomer", "Champagne Boomer"},
                {"Green Boomer", "Green Boomer"},
                {"Orange Boomer", "Orange Boomer"},
                {"Red Boomer", "Red Boomer"},
                {"Violet Boomer", "Violet Boomer"},
                {"Blue Roman Candle", "Blue Roman Candle"},
                {"Green Roman Candle", "Green Roman Candle"},
                {"Red Roman Candle", "Red Roman Candle"},
                {"Violet Roman Candle", "Violet Roman Candle"},
                {"White Volcano Firework", "White Volcano Firework"},
                {"Red Volcano Firework", "Red Volcano Firework"},
                {"Violet Volcano Firework", "Violet Volcano Firework"},
                {"Wooden Floor Spikes", "Wooden Floor Spikes"},
                {"Fridge", "Fridge"},
                {"Large Furnace", "Large Furnace"},
                {"Furnace", "Furnace"},
                {"Hitch & Trough", "Hitch "},
                {"Hab Repair", "Got repair"},
                {"Jack O Lantern Angry", "Jack O Lantern Angry"},
                {"Jack O Lantern Happy", "Jack O Lantern Happy"},
                {"Land Mine", "Land Mine"},
                {"Lantern", "Lantern"},
                {"Large Wood Box", "Large Wood Box"},
                {"Water Barrel", "Water Barrel"},
                {"Locker", "Locker"},
                {"Mail Box", "Mail Box"},
                {"Mixing Table", "Mixing Table"},
                {"Modular Car Lift", "Modular Car Lift"},
                {"Pump Jack", "Pump Jack"},
                {"Small Oil Refinery", "Small Oil Refinery"},
                {"Large Planter Box", "Large Planter Box"},
                {"Small Planter Box", "Small Planter Box"},
                {"Audio Alarm", "Audio Alarm"},
                {"Smart Alarm", "Smart Alarm"},
                {"Smart Switch", "Smart Switch"},
                {"Storage Monitor", "Storage Monitor"},
                {"Large Rechargable Battery", "Large Rechargable Battery"},
                {"Medium Rechargable Battery", "Medium Rechargable Battery"},
                {"Small Rechargable Battery", "Small Rechargable Battery"},
                {"Button", "Button"},
                {"Counter", "Counter"},
                {"HBHF Sensor", "HBHF Sensor"},
                {"Laser Detector", "Laser Detector"},
                {"Pressure Pad", "Pressure Pad"},
                {"Door Controller", "By Controller"},
                {"Electric Heater", "Electric Heater"},
                {"Fluid Combiner", "Fluid Combine"},
                {"Fluid Splitter", "Fluid Splitter"},
                {"Fluid Switch & Pump", "Fluid Switch "},
                {"AND Switch", "AND Switch"},
                {"Blocker", "Blocker"},
                {"Electrical Branch", "Electrical Branch"},
                {"Root Combiner", "Root Combiner"},
                {"Memory Cell", "Memory Cell"},
                {"OR Switch", "OR Switch"},
                {"RAND Switch", "RAND Switch"},
                {"RF Broadcaster", "RF Broadcaster"},
                {"RF Receiver", "RF Receiver"},
                {"XOR Switch", "XOR Switch"},
                {"Small Generator", "Small Generator"},
                {"Test Generator", "Test Generator"},
                {"Large Solar Panel", "Large Solar Panel"},
                {"Igniter", "Igniter"},
                {"Flasher Light", "Flasher Light"},
                {"Simple Light", "Simple Light"},
                {"Siren Light", "Siren Light"},
                {"Powered Water Purifier", "Powered Water Purifier"},
                {"Switch", "Switch"},
                {"Splitter", "Splitter"},
                {"Sprinkler", "Sprinkler"},
                {"Tesla Coil", "Tesla Coil"},
                {"Timer", "Timer"},
                {"Cable Tunnel", "Cable Tunnel"},
                {"Water Pump", "Water Pump"},
                {"Mining Quarry", "Mining Quarry"},
                {"Reactive Target", "Reactive Target"},
                {"Repair Bench", "Repair Bench"},
                {"Research Table", "Research Table"},
                {"Rug Bear Skin", "Rug Bear Skin"},
                {"Rug", "Rug"},
                {"Search Light", "Search Light"},
                {"Secret Lab Chair", "Secret Lab Chair"},
                {"Salvaged Shelves", "Salvaged Shelves"},
                {"Large Banner Hanging", "Large Banner Hanging"},
                {"Two Sided Hanging Sign", "Two Sided Hanging Sign"},
                {"Two Sided Ornate Hanging Sign", "Two Sided Ornate Hanging Sign"},
                {"Landscape Picture Frame", "Landscape Picture Frame"},
                {"Portrait Picture Frame", "Portrait Picture Frame"},
                {"Tall Picture Frame", "Tall Picture Frame"},
                {"XL Picture Frame", "XL Picture Frame"},
                {"XXL Picture Frame", "XXL Picture Frame"},
                {"Large Banner on pole", "Large Banner on pole"},
                {"Double Sign Post", "Double Sign Post"},
                {"Single Sign Post", "Single Sign Post"},
                {"One Sided Town Sign Post", "One Sided Town Sign Post"},
                {"Two Sided Town Sign Post", "Two Sided Town Sign Post"},
                {"Huge Wooden Sign", "Huge Wooden Sign"},
                {"Large Wooden Sign", "Large Wooden Sign"},
                {"Medium Wooden Sign", "Medium Wooden Sign"},
                {"Small Wooden Sign", "Small Wooden Sign"},
                {"Shotgun Trap", "Shotgun Trap"},
                {"Sleeping Bag", "Sleeping Bag"},
                {"Small Stash", "Small Stash"},
                {"Spinning wheel", "Spinning wheel"},
                {"Survival Fish Trap", "Survival Fish Trap"},
                {"Table", "Table"},
                {"Work Bench Level 1", "Work Bench Level 1"},
                {"Work Bench Level 2", "Work Bench Level 2"},
                {"Work Bench Level 3", "Work Bench Level 3"},
                {"Tool Cupboard", "Tool Cupboard"},
                {"Tuna Can Lamp", "Tuna Can Lamp"},
                {"Vending Machine", "Vending Machine"},
                {"Large Water Catcher", "Large Water Catcher"},
                {"Small Water Catcher", "Small Water Catcher"},
                {"Water Purifier", "Water Purifier"},
                {"Wind Turbine", "Wind Turbine"},
                {"Wood Storage Box", "Wood Storage Box"},
                {"Apple", "Apple"},
                {"Rotten Apple", "Rotten Apple"},
                {"Black Raspberries", "Black Raspberries"},
                {"Blueberries", "Blueberries"},
                {"Bota Bag", "Bota Bag"},
                {"Cactus Flesh", "Cactus Flesh"},
                {"Can of Beans", "Can of Beans"},
                {"Can of Tuna", "Can of Tuna"},
                {"Chocolate Bar", "Chocolate Bar"},
                {"Cooked Fish", "Cooked Fish"},
                {"Raw Fish", "Raw Fish"},
                {"Minnows", "Minnows"},
                {"Small Trout", "Small Trout"},
                {"Granola Bar", "Granola Bar"},
                {"Burnt Chicken", "Burnt Chicken"},
                {"Cooked Chicken", "Cooked Chicken"},
                {"Raw Chicken Breast", "Raw Chicken Breast"},
                {"Spoiled Chicken", "Spoiled Chicken"},
                {"Burnt Deer Meat", "Burnt Deer Meat"},
                {"Cooked Deer Meat", "Cooked Deer Meat"},
                {"Raw Deer Meat", "Raw Deer Meat"},
                {"Burnt Horse Meat", "Burnt Horse Meat"},
                {"Cooked Horse Meat", "Cooked Horse Meat"},
                {"Raw Horse Meat", "Raw Horse Meat"},
                {"Burnt Human Meat", "Burnt Human Meat"},
                {"Cooked Human Meat", "Cooked Human Meat"},
                {"Raw Human Meat", "Raw Human Meat"},
                {"Spoiled Human Meat", "Spoiled Human Meat"},
                {"Burnt Bear Meat", "Burnt Bear Meat"},
                {"Cooked Bear Meat", "Cooked Bear Meat"},
                {"Raw Bear Meat", "Raw Bear Meat"},
                {"Burnt Wolf Meat", "Burnt Wolf Meat"},
                {"Cooked Wolf Meat", "Cooked Wolf Meat"},
                {"Raw Wolf Meat", "Raw Wolf Meat"},
                {"Spoiled Wolf Meat", "Spoiled Wolf Meat"},
                {"Burnt Pork", "Burnt Pork"},
                {"Cooked Pork", "Cooked Pork"},
                {"Raw Pork", "Raw Pork"},
                {"Mushroom", "Mushroom"},
                {"Pickles", "Pickles"},
                {"Small Water Bottle", "Small Water Bottle"},
                {"Water Jug", "Water Jug"},
                {"Shovel Bass", "Shovel Bass"},
                {"Cowbell", "Cowbell"},
                {"Junkyard Drum Kit", "Junkyard Drum Kit"},
                {"Pan Flute", "Pan Flute"},
                {"Acoustic Guitar", "Acoustic Guitar"},
                {"Jerry Can Guitar", "Jerry Can Guitar"},
                {"Wheelbarrow Piano", "Wheelbarrow Piano"},
                {"Canbourine", "Canbourine"},
                {"Plumber's Trumpet", "Plumber's Trumpet"},
                {"Sousaphone", "Sousaphone"},
                {"Xylobone", "Xylobone"},
                {"Car Key", "Car Key"},
                {"Door Key", "By Key"},
                {"Key Lock", "Key Lock"},
                {"Code Lock", "Code Lock"},
                {"Blueprint", "Blueprint"},
                {"Chinese Lantern", "Chinese Lantern"},
                {"Dragon Door Knocker", "Dragon Door Knocker"},
                {"Dragon Mask", "Dragon Mask"},
                {"New Year Gong", "New Year Gong"},
                {"Rat Mask", "Rat Mask"},
                {"Firecracker String", "Firecracker String"},
                {"Chippy Arcade Game", "Chippy Arcade Game"},
                {"Door Closer", "Door Closer"},
                {"Bunny Ears", "Bunny Ears"},
                {"Bunny Onesie", "Bunny Onesie"},
                {"Easter Door Wreath", "Easter Door Wreath"},
                {"Egg Basket", "Egg Basket"},
                {"Rustigé Egg - Red", "Quiet Egg - Red"},
                {"Rustigé Egg - Blue", "Quiet Egg - Blue"},
                {"Rustigé Egg - Purple", "Quiet Egg - Purple"},
                {"Rustigé Egg - Ivory", "Quiet Egg - Ivory"},
                {"Nest Hat", "Nest Hat"},
                {"Bronze Egg", "Bronze Egg"},
                {"Gold Egg", "Gold Egg"},
                {"Painted Egg", "Painted Egg"},
                {"Silver Egg", "Silver Egg"},
                {"Halloween Candy", "Halloween Candy"},
                {"Large Candle Set", "Large Candle Set"},
                {"Small Candle Set", "Small Candle Set"},
                {"Coffin", "Coffin"},
                {"Cursed Cauldron", "Cursed Cauldron"},
                {"Gravestone", "Gravestone"},
                {"Wooden Cross", "Wooden Cross"},
                {"Graveyard Fence", "Graveyard Fence"},
                {"Large Loot Bag", "Large Loot Bag"},
                {"Medium Loot Bag", "Medium Loot Bag"},
                {"Small Loot Bag", "Small Loot Bag"},
                {"Pumpkin Bucket", "Pumpkin Bucket"},
                {"Scarecrow", "Scarecrow"},
                {"Skull Spikes", "Skull Spikes"},
                {"Skull Door Knocker", "Skull Door Knocker"},
                {"Skull Fire Pit", "Skull Fire Pit"},
                {"Spider Webs", "Spider Webs"},
                {"Spooky Speaker", "Spooky Speaker"},
                {"Surgeon Scrubs", "Surgeon Scrubs"},
                {"Skull Trophy", "Skull Trophy"},
                {"Card Movember Moustache", "Card Movember Moustache"},
                {"Movember Moustache", "Movember Mustache"},
                {"Note", "Note"},
                {"Human Skull", "Human Skull"},
                {"Above Ground Pool", "Above Ground Pool"},
                {"Beach Chair", "Beach Chair"},
                {"Beach Parasol", "Beach Parasol"},
                {"Beach Table", "Beach Table"},
                {"Beach Towel", "Beach Towel"},
                {"Boogie Board", "Boogie Board"},
                {"Inner Tube", "Inner Tube"},
                {"Instant Camera", "Instant Camera"},
                {"Paddling Pool", "Paddling Pool"},
                {"Photograph", "Photograph"},
                {"Landscape Photo Frame", "Landscape Photo Frame"},
                {"Large Photo Frame", "Large Photo Frame"},
                {"Portrait Photo Frame", "Portrait Photo Frame"},
                {"Sunglasses", "Sunglasses"},
                {"Water Gun", "Water Gun"},
                {"Water Pistol", "Water Pistol"},
                {"Purple Sunglasses", "Purple Sunglasses"},
                {"Headset", "Headset"},
                {"Candy Cane Club", "Candy Cane Club"},
                {"Christmas Lights", "Christmas Lights"},
                {"Festive Doorway Garland", "Festive Doorway Garland"},
                {"Candy Cane", "Candy Cane"},
                {"Giant Candy Decor", "Giant Candy Decor"},
                {"Giant Lollipop Decor", "Giant Lollipop Decor"},
                {"Pookie Bear", "Pookie Bear"},
                {"Deluxe Christmas Lights", "Deluxe Christmas Lights"},
                {"Coal ,(", "Coal ,("},
                {"Large Present", "Large Present"},
                {"Medium Present", "Medium Present"},
                {"Small Present", "Small Present"},
                {"Snow Machine", "Snow Machine"},
                {"Snowball", "Snowball"},
                {"Snowman", "Snowman"},
                {"SUPER Stocking", "SUPER Stocking"},
                {"Small Stocking", "Small Stocking"},
                {"Reindeer Antlers", "Reindeer Antlers"},
                {"Santa Beard", "Santa Beard"},
                {"Santa Hat", "Santa Hat"},
                {"Festive Window Garland", "Festive Window Garland"},
                {"Wrapped Gift", "Wrapped Gift"},
                {"Wrapping Paper", "Wrapping Paper"},
                {"Christmas Door Wreath", "Christmas Door Wreath"},
                {"Decorative Baubels", "Decorative Baubels"},
                {"Decorative Plastic Candy Canes", "Decorative Plastic Candy Canes"},
                {"Decorative Gingerbread Men", "Decorative Gingerbread Men"},
                {"Tree Lights", "Tree Lights"},
                {"Decorative Pinecones", "Decorative Pinecones"},
                {"Star Tree Topper", "Star Tree Topper"},
                {"Decorative Tinsel", "Decorative Tinsel"},
                {"Christmas Tree", "Christmas Tree"},
                {"Auto Turret", "Auto Turret"},
                {"Flame Turret", "Flame Turret"},
                {"Glowing Eyes", "Glowing Eyes"},
                {"SAM Ammo", "SAM Ammo"},
                {"SAM Site", "SAM Site"},
                {"Black Berry", "Black Berry"},
                {"Black Berry Clone", "Black Berry Clone"},
                {"Black Berry Seed", "Black Berry Seed"},
                {"Blue Berry", "Blue Berry"},
                {"Blue Berry Clone", "Blue Berry Clone"},
                {"Blue Berry Seed", "Blue Berry Seed"},
                {"Green Berry", "Green Berry"},
                {"Green Berry Clone", "Green Berry Clone"},
                {"Green Berry Seed", "Green Berry Seed"},
                {"Red Berry", "Red Berry"},
                {"Red Berry Clone", "Red Berry Clone"},
                {"Red Berry Seed", "Red Berry Seed"},
                {"White Berry", "White Berry"},
                {"White Berry Clone", "White Berry Clone"},
                {"White Berry Seed", "White Berry Seed"},
                {"Yellow Berry", "Yellow Berry"},
                {"Yellow Berry Clone", "Yellow Berry Clone"},
                {"Yellow Berry Seed", "Yellow Berry Seed"},
                {"Corn", "Corn"},
                {"Corn Clone", "Corn Clone"},
                {"Corn Seed", "Corn Seed"},
                {"Hemp Clone", "Hemp Clone"},
                {"Hemp Seed", "Hemp Seed"},
                {"Potato", "Potato"},
                {"Potato Clone", "Potato Clone"},
                {"Potato Seed", "Potato Seed"},
                {"Pumpkin", "Pumpkin"},
                {"Pumpkin Plant Clone", "Pumpkin Plant Clone"},
                {"Pumpkin Seed", "Pumpkin Seed"},
                {"Animal Fat", "Animal Fat"},
                {"Battery - Small", "Battery - Small"},
                {"Blood", "Blood"},
                {"Bone Fragments", "Bone Fragments"},
                {"CCTV Camera", "CCTV Camera"},
                {"Charcoal", "Charcoal"},
                {"Cloth", "Cloth"},
                {"Crude Oil", "Crude Oil"},
                {"Diesel Fuel", "Diesel Fuel"},
                {"Empty Can Of Beans", "Empty Can Of Beans"},
                {"Empty Tuna Can", "Empty Tuna Can"},
                {"Explosives", "Explosives"},
                {"Fertilizer", "Fertilizer"},
                {"Gun Powder", "Gun Powder"},
                {"Horse Dung", "Horse dung"},
                {"High Quality Metal Ore", "High Quality Metal Ore"},
                {"High Quality Metal", "High Quality Metal"},
                {"Leather", "Leather"},
                {"Low Grade Fuel", "Low Grade Fuel"},
                {"Metal Fragments", "Metal Fragments"},
                {"Metal Ore", "Metal Ore"},
                {"Paper", "Paper"},
                {"Plant Fiber", "Plant Fiber"},
                {"Research Paper", "Research Paper"},
                {"Salt Water", "Salt Water"},
                {"Scrap", "Scrap"},
                {"Stones", "Stones"},
                {"Sulfur Ore", "Sulfur Ore"},
                {"Sulfur", "Sulfur"},
                {"Targeting Computer", "Targeting Computer"},
                {"Water", "Water"},
                {"Wolf Skull", "Wolf Skull"},
                {"Wood", "Wood"},
                {"Advanced Healing Tea", "Advanced Healing Tea"},
                {"Basic Healing Tea", "Basic Healing Tea"},
                {"Pure Healing Tea", "Pure Healing Tea"},
                {"Advanced Max Health Tea", "Advanced Max Health Tea"},
                {"Basic Max Health Tea", "Basic Max Health Tea"},
                {"Pure Max Health Tea", "Pure Max Health Tea"},
                {"Advanced Ore Tea", "Advanced Ore Tea"},
                {"Basic Ore Tea", "Basic Ore Tea"},
                {"Pure Ore Tea", "Pure Ore Tea"},
                {"Advanced Rad. Removal Tea", "Advanced Rad. Removal Tea"},
                {"Rad. Removal Tea", "Rad. Removal Tea"},
                {"Pure Rad. Removal Tea", "Pure Rad. Removal Tea"},
                {"Adv. Anti-Rad Tea", "Adv. Anti-Rad Tea"},
                {"Anti-Rad Tea", "Anti-Rad Tea"},
                {"Pure Anti-Rad Tea", "Pure Anti-Rad Tea"},
                {"Advanced Scrap Tea", "Advanced Scrap Tea"},
                {"Basic Scrap Tea", "Basic Scrap Tea"},
                {"Pure Scrap Tea", "Pure Scrap Tea"},
                {"Advanced Wood Tea", "Advanced Wood Tea"},
                {"Basic Wood Tea", "Basic Wood Tea"},
                {"Pure Wood Tea", "Pure Wood Tea"},
                {"Anti-Radiation Pills", "Anti-Radiation Pills"},
                {"Binoculars", "Binoculars"},
                {"Timed Explosive Charge", "Timed Explosive Charge"},
                {"Camera", "Camera"},
                {"RF Transmitter", "RF Transmitter"},
                {"Handmade Fishing Rod", "Handmade Fishing Rod"},
                {"Flare", "Flare"},
                {"Flashlight", "Flashlight"},
                {"Geiger Counter", "Geiger Counter"},
                {"Hose Tool", "Hose Tool"},
                {"Jackhammer", "Jackhammer"},
                {"Blue Keycard", "Blue Keycard"},
                {"Green Keycard", "Green Keycard"},
                {"Red Keycard", "Red Keycard"},
                {"Large Medkit", "Large Medkit"},
                {"Paper Map", "Paper Map"},
                {"Medical Syringe", "Medical Syringe"},
                {"RF Pager", "RF Pager"},
                {"Building Plan", "Building Plan"},
                {"Smoke Grenade", "Smoke Grenade"},
                {"Supply Signal", "Supply Signal"},
                {"Survey Charge", "Survey Charge"},
                {"Wire Tool", "Wire Tool"},
                {"Small Chassis", "Small Chassis"},
                {"Medium Chassis", "Medium Chassis"},
                {"Large Chassis", "Large Chassis"},
                {"Cockpit Vehicle Module", "Cockpit Vehicle Module"},
                {"Armored Cockpit Vehicle Module", "Armored Cockpit Vehicle Module"},
                {"Cockpit With Engine Vehicle Module", "Cockpit With Engine Vehicle Module"},
                {"Engine Vehicle Module", "Engine Vehicle Module"},
                {"Flatbed Vehicle Module", "Flatbed Vehicle Module"},
                {"Armored Passenger Vehicle Module", "Armored Passenger Vehicle Module"},
                {"Rear Seats Vehicle Module", "Rear Seats Vehicle Module"},
                {"Storage Vehicle Module", "Storage Vehicle Module"},
                {"Taxi Vehicle Module", "Taxi Vehicle Module"},
                {"Large Flatbed Vehicle Module", "Large Flatbed Vehicle Module"},
                {"Fuel Tank Vehicle Module", "Fuel Tank Vehicle Module"},
                {"Passenger Vehicle Module", "Passenger Vehicle Module"},
                {"Generic vehicle module", "Generic vehicle module"},
                {"Telephone", "Telephone"},
                {"16x Zoom Scope", "16x Zoom Scope"},
                {"Weapon flashlight", "Weapon flashlight"},
                {"Holosight", "Holosight"},
                {"Weapon Lasersight", "Weapon Lasersight"},
                {"Muzzle Boost", "Muzzle Boost"},
                {"Muzzle Brake", "Muzzle Brake"},
                {"Simple Handmade Sight", "Simple Handmade Sight"},
                {"Silencer", "Silencer"},
                {"8x Zoom Scope", "8x Zoom Scope"},
                {"Assault Rifle", "Assault Rifle"},
                {"Bandage", "Bandage"},
                {"Beancan Grenade", "Beancan Grenada"},
                {"Bolt Action Rifle", "Bolt Action Rifle"},
                {"Bone Club", "Bone Club"},
                {"Bone Knife", "Bone Knife"},
                {"Hunting Bow", "Hunting Bow"},
                {"Birthday Cake", "Birthday Cake"},
                {"Chainsaw", "Chainsaw"},
                {"Salvaged Cleaver", "Salvaged Cleaver"},
                {"Compound Bow", "Compound Bow"},
                {"Crossbow", "Crossbow"},
                {"Double Barrel Shotgun", "Double Barrel Shotgun"},
                {"Eoka Pistol", "Eoka Pistol"},
                {"F1 Grenade", "F1 Granada"},
                {"Flame Thrower", "Flame Thrower"},
                {"Multiple Grenade Launcher", "Multiple Grenade Launcher"},
                {"Butcher Knife", "Butcher Knife"},
                {"Pitchfork", "Pitchfork"},
                {"Sickle", "Sickle"},
                {"Hammer", "Hammer"},
                {"Hatchet", "Hatchet"},
                {"Combat Knife", "Combat Knife"},
                {"L96 Rifle", "L96 Rifle"},
                {"LR-300 Assault Rifle", "LR-300 Assault Rifle"},
                {"M249", "M249"},
                {"M39 Rifle", "M39 Rifle"},
                {"M92 Pistol", "M92 Pistol"},
                {"Mace", "cat"},
                {"Machete", "Machete"},
                {"MP5A4", "MP5A4"},
                {"Nailgun", "Nailgun"},
                {"Paddle", "Paddle"},
                {"Pickaxe", "Pickaxe"},
                {"Waterpipe Shotgun", "Waterpipe Shotgun"},
                {"Python Revolver", "Python Revolver"},
                {"Revolver", "Revolver"},
                {"Rock", "Rock"},
                {"Rocket Launcher", "Rocket Launcher"},
                {"Salvaged Axe", "Salvaged Axe"},
                {"Salvaged Hammer", "Salvaged Hammer"},
                {"Salvaged Icepick", "Salvaged Icepick"},
                {"Satchel Charge", "Satchel Charge"},
                {"Pump Shotgun", "Pump Shotgun"},
                {"Semi-Automatic Pistol", "Semi-Automatic Pistol"},
                {"Semi-Automatic Rifle", "Semi-Automatic Rifle"},
                {"Custom SMG", "Custom SMG"},
                {"Spas-12 Shotgun", "Spas-12 Shotgun"},
                {"Stone Hatchet", "Stone Hatchet"},
                {"Stone Pickaxe", "Stone Pickaxe"},
                {"Stone Spear", "Stone Spear"},
                {"Longsword", "Longsword"},
                {"Salvaged Sword", "Salvaged Sword"},
                {"Thompson", "Thompson"},
                {"Garry's Mod Tool Gun", "Garry's Mod Tool Gun"},
                {"Torch", "Torch"},
                {"Water Bucket", "Water Bucket"},
                {"Wooden Spear", "Wooden Spear"},
                {"Roadsign Horse Armor", "Roadsign Horse Armor"},
                {"Wooden Horse Armor", "Wooden Horse Armor"},
                {"Horse Saddle", "Horse Saddle"},
                {"Saddle bag", "Saddle bag"},
                {"High Quality Horse Shoes", "High Quality Horse Shoes"},
                {"Basic Horse Shoes", "Basic Horse Shoes"},
                {"Generic vehicle chassis", "Generic vehicle chassis"}
            }, this); //en

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MessageShowNoEconomics", "GUIShop n'a pas reçu de réponse du plugin Economics. Veuillez vous assurer que le plugin Economics est correctement installé."},
                {"MessageShowNoServerRewards", "GUIShop n'a pas reçu de réponse du plugin ServerRewards. Veuillez vous assurer que le plugin ServerRewards est correctement installé."},
                {"MessageBought", "Vous avez acheté {0} x {1} avec succès."},
                {"MessageSold", "Vous avez vendu {0} x {1} avec succès."},
                {"MessageErrorCooldown", "Vous ne pouvez acheter cet article que toutes les {0} secondes."},
                {"MessageErrorSellCooldownAmount", "Vous ne pouvez vendre que {0} de cet article toutes les {1} secondes."},
                {"MessageErrorBuyCooldownAmount", "Vous ne pouvez acheter que {0} de cet article toutes les {1} secondes."},
                {"MessageErrorBuyLimit", "Vous ne pouvez acheter que {0} de cet article."},
                {"MessageErrorSellLimit", "Vous ne pouvez vendre que {0} de cet article."},
                {"MessageErrorInventoryFull", "Votre inventaire est plein."},
                {"MessageErrorInventorySlots", "Vous avez besoin d'au moins {0} emplacements d'inventaire gratuits."},
                {"MessageErrorNoShop", "Il y a un problème avec cette boutique. Veuillez contacter un administrateur."},
                {"MessageErrorGlobalDisabled", "Les boutiques globales sont désactivées. Ce serveur utilise des vendeurs de PNJ!"},
                {"MessageErrorNoActionShop", "Vous n'êtes pas autorisé à {0} dans cette boutique"},
                {"MessageErrorNoActionItem", "Vous n'êtes pas autorisé à {0} cet élément ici"},
                {"MessageErrorItemItem", "AVERTISSEMENT: il semble que cet article que vous possédez n'est pas un article valide! Veuillez contacter un administrateur!"},
                {"MessageErrorItemNoValidbuy", "AVERTISSEMENT: Il semble que ce ne soit pas un article valide à acheter, veuillez contacter un administrateur!"},
                {"MessageErrorItemNoValidsell", "AVERTISSEMENT: Il semble que ce ne soit pas un article valide à vendre, veuillez contacter un administrateur!"},
                {"MessageErrorRedeemKit", "AVERTISSEMENT: Une erreur s'est produite lors de la remise de ce kit, veuillez contacter un administrateur!"},
                {"MessageErrorBuyCmd", "Impossible d'acheter plusieurs exemplaires de cet article!"},
                {"MessageErrorBuyPrice", "AVERTISSEMENT: aucun prix d'achat n'a été donné par l'administrateur, vous ne pouvez pas acheter cet article"},
                {"MessageErrorSellPrice", "AVERTISSEMENT: aucun prix de vente n'a été donné par l'administrateur, vous ne pouvez pas vendre cet article"},
                {"MessageErrorNotEnoughMoney", "Vous avez besoin de {0} pièces pour acheter {1} sur {2}"},
                {"MessageErrorNotEnoughMoneyCustom", "Vous avez besoin de {0} devise pour acheter {1} x {2}"},
                {"MessageErrorNotEnoughSell", "Vous n'avez pas assez de cet article."},
                {"MessageErrorNotNothing", "Vous ne pouvez pas acheter Zero de cet article."},
                {"MessageErrorItemNoExist", "AVERTISSEMENT: l'article que vous essayez d'acheter ne semble pas exister! Veuillez contacter un administrateur!"},
                {"MessageErrorItemNoExistTake", "L'article que vous essayez de vendre n'est pas vendable pour le moment."},
                {"MessageErrorBuildingBlocked", "Vous ne pouvez pas faire vos achats dans une zone de bâtiment bloquée."},
                {"MessageErrorAdmin", "Vous ne disposez pas des autorisations appropriées pour utiliser cette commande. (guishop.admin)"},
                {"MessageErrorWaitingOnDownloads", "GUIShop attend la fin des téléchargements d'ImageLibrary"},
                {"BlockedMonuments", "Vous ne pouvez pas utiliser la boutique à proximité d'un monument!"},
                {"MessageErrorItemNotEnabled", "Le commerçant a désactivé cet article."},
                {"MessageErrorItemNotFound", "L'élément n'a pas été trouvé"},
                {"CantSellCommands", "Vous ne pouvez pas revendre les commandes à la boutique."},
                {"CantSellKits", "Vous ne pouvez pas revendre les kits à la boutique."},
                {"MessageErrorCannotSellWhileEquiped", "Vous ne pouvez pas vendre l'objet si vous l'avez équipé."},
                {"MessageShopResponse", "GUIShop attend la fin des téléchargements d'ImageLibrary, veuillez patienter."},
                {"MessageNPCResponseclose", "Merci pour vos achats chez {0} revenez bientôt!"},
                {"MessageNPCResponseopen", "Bienvenue dans le {0} que souhaitez-vous acheter? Appuyez sur E pour commencer vos achats!"},
                {"Commands", "Commandes"},
                {"Attire", "Tenue"},
                {"Misc", "Divers"},
                {"Items", "Articles"},
                {"Ammunition", "Munition"},
                {"Construction", "Construction"},
                {"Component", "Composant"},
                {"Traps", "Pièges"},
                {"Electrical", "Électrique"},
                {"Fun", "Amusement"},
                {"Food", "Nourriture"},
                {"Resources", "Ressources"},
                {"Tool", "Outil"},
                {"Weapon", "Arme"},
                {"Medical", "Médical"},
                {"Minicopter", "Minicopter"},
                {"Sedan", "Sedan"},
                {"Airdrop Call", "Fumigène de dètresse"},
                {"Wolf Headdress", "Coiffe de loup"},
                {"Fogger-3000", "Brumisateur-3000"},
                {"Strobe Light", "Lumière stroboscopique"},
                {"Kayak", "Kayak"},
                {"MC repair", "Réparation MC"},
                {"ScrapTransportHeliRepair", "FerrailleTransportHeliRepair"},
                {"40mm Shotgun Round", "Chevrotine de 40mm"},
                {"40mm HE Grenade", "Grenade HE 40 mm"},
                {"40mm Smoke Grenade", "Grenade fumigène de 40 mm"},
                {"High Velocity Arrow", "Flèche haute vitesse"},
                {"Wooden Arrow", "Flèche en bois"},
                {"Bone Arrow", "Flèche en os"},
                {"Fire Arrow", "Flèche enflammée"},
                {"Handmade Shell", "Cartouche Artisanale"},
                {"Nailgun Nails", "Clous"},
                {"Pistol Bullet", "Balle de pistolet"},
                {"Incendiary Pistol Bullet", "Balle de pistolet incendiaire"},
                {"HV Pistol Ammo", "Munitions pour pistolet HV"},
                {"5.56 Rifle Ammo", "5.56 Munitions pour fusil"},
                {"Explosive 5.56 Rifle Ammo", "Munitions pour fusil explosif 5.56"},
                {"Incendiary 5.56 Rifle Ammo", "Munitions pour fusil incendiaire 5.56"},
                {"HV 5.56 Rifle Ammo", "Munitions pour carabine HV 5.56"},
                {"Rocket", "Roquette"},
                {"Incendiary Rocket", "Roquette incendiaire"},
                {"High Velocity Rocket", "Roquette haute vitesse"},
                {"Smoke Rocket WIP!!!!", "Roquette fumigène"},
                {"12 Gauge Buckshot", "Chevrotine de calibre 12"},
                {"12 Gauge Incendiary Shell", "Cartouche incendiaire de calibre 12"},
                {"12 Gauge Slug", "Cartouche de calibre 12"},
                {"Sheet Metal Double Door", "Double porte en tôle"},
                {"Armored Double Door", "Double porte blindée"},
                {"Wood Double Door", "Double porte en bois"},
                {"Sheet Metal Door", "Porte en tôle"},
                {"Armored Door", "Porte blindée"},
                {"Wooden Door", "Porte en bois"},
                {"Floor grill", "Grille de sol"},
                {"Ladder Hatch", "Trappe à échelle"},
                {"Floor triangle grill", "Grille triangulaire au sol"},
                {"Triangle Ladder Hatch", "Trappe d'échelle triangulaire"},
                {"High External Stone Gate", "Porte en pierre externe haute"},
                {"High External Wooden Gate", "Porte en bois externe haute"},
                {"Wooden Ladder", "Échelle en bois"},
                {"High External Stone Wall", "Mur de pierre externe élevé"},
                {"High External Wooden Wall", "Mur en bois extérieur haut"},
                {"Prison Cell Gate", "Porte de cellule de prison"},
                {"Prison Cell Wall", "Mur de cellule de prison"},
                {"Chainlink Fence Gate", "Porte de clôture grillagée"},
                {"Chainlink Fence", "Grillage"},
                {"Garage Door", "Porte de garage"},
                {"Netting", "Filets"},
                {"Shop Front", "Vitrine"},
                {"Metal Shop Front", "Façade de magasin en métal"},
                {"Metal Window Bars", "Barres de fenêtre en métal"},
                {"Reinforced Window Bars", "Barres de fenêtre renforcées"},
                {"Wooden Window Bars", "Barres de fenêtre en bois"},
                {"Metal horizontal embrasure", "Embrasure horizontale en métal"},
                {"Metal Vertical embrasure", "Embrasure verticale en métal"},
                {"Reinforced Glass Window", "Fenêtre en verre renforcé"},
                {"Wood Shutters", "Volets en bois"},
                {"Watch Tower", "Tour de guet"},
                {"Diving Fins", "Palmes de plongée"},
                {"Diving Mask", "Masque de plongée"},
                {"Diving Tank", "Réservoir de plongée"},
                {"Wetsuit", "Combinaison"},
                {"Frog Boots", "Bottes de grenouille"},
                {"A Barrel Costume", "Un costume de tonneau"},
                {"Crate Costume", "Costume de caisse"},
                {"Burlap Gloves", "Gants en toile de jute"},
                {"Leather Gloves", "Gants de cuir"},
                {"Roadsign Gloves", "Gants Roadsign"},
                {"Tactical Gloves", "Gants tactiques"},
                {"Ghost Costume", "Costume de fantôme"},
                {"Mummy Suit", "Costume de momie"},
                {"Scarecrow Suit", "Costume d'épouvantail"},
                {"Scarecrow Wrap", "Enveloppement d'épouvantail"},
                {"Hide Halterneck", "Dos-nu en cuir"},
                {"Beenie Hat", "Chapeau Beenie"},
                {"Boonie Hat", "Chapeau Boonie"},
                {"Bucket Helmet", "Casque Bucket"},
                {"Burlap Headwrap", "Bandeau en toile de jute"},
                {"Candle Hat", "Bougie chapeau"},
                {"Baseball Cap", "Casquette de baseball"},
                {"Clatter Helmet", "Casque Clatter"},
                {"Coffee Can Helmet", "Casque boîte à café"},
                {"Bone Helmet", "Casque en os"},
                {"Heavy Plate Helmet", "Casque en plaques lourdes"},
                {"Miners Hat", "Chapeau de mineur"},
                {"Party Hat", "Chapeau de Fête"},
                {"Riot Helmet", "Casque anti-émeute"},
                {"Wood Armor Helmet", "Casque d'armure en bois"},
                {"Hoodie", "Sweat à capuche"},
                {"Bone Armor", "Armure d'os"},
                {"Heavy Plate Jacket", "Veste en plaques lourdes"},
                {"Snow Jacket", "Veste de neige"},
                {"Jacket", "Veste"},
                {"Wood Chestplate", "Pansière en bois"},
                {"Improvised Balaclava", "Cagoule improvisée"},
                {"Bandana Mask", "Masque de bandana"},
                {"Metal Facemask", "Masque en métal"},
                {"Night Vision Goggles", "Lunettes de vision nocturne"},
                {"Burlap Trousers", "Pantalon en toile de jute"},
                {"Heavy Plate Pants", "Pantalon en plaques lourdes"},
                {"Hide Pants", "Pantalon en peau"},
                {"Road Sign Kilt", "Panneau routier Kilt"},
                {"Shorts", "Short"},
                {"Wood Armor Pants", "Pantalon d'armure en bois"},
                {"Pants", "Pantalon"},
                {"Hide Poncho", "Poncho en peau"},
                {"Burlap Shirt", "Chemise en toile de jute"},
                {"Shirt", "Chemise"},
                {"Hide Vest", "Gilet en peau"},
                {"Tank Top", "Débardeur"},
                {"Boots", "Bottes"},
                {"Burlap Shoes", "Chaussures en toile de jute"},
                {"Hide Boots", "Bottes en peau"},
                {"Hide Skirt", "Jupe en peau"},
                {"Bandit Guard Gear", "Équipement de garde de bandit"},
                {"Hazmat Suit", "Combinaison Hazmat"},
                {"Scientist Suit", "Costume de scientifique"},
                {"Space Suit", "Combinaison spatiale"},
                {"Heavy Scientist Suit", "Costume de scientifique lourd"},
                {"Longsleeve T-Shirt", "T-shirt à manches longues"},
                {"T-Shirt", "T-shirt"},
                {"Metal Chest Plate", "Plaque de poitrine en métal"},
                {"Road Sign Jacket", "Veste de signalisation routière"},
                {"Bleach", "Eau de Javel"},
                {"Duct Tape", "Ruban adhésif"},
                {"Low Quality Carburetor", "Carburateur de basse qualité"},
                {"Medium Quality Carburetor", "Carburateur de qualité moyenne"},
                {"High Quality Carburetor", "Carburateur de haute qualité"},
                {"Low Quality Crankshaft", "Vilebrequin de basse qualité"},
                {"Medium Quality Crankshaft", "Vilebrequin de qualité moyenne"},
                {"High Quality Crankshaft", "Vilebrequin de haute qualité"},
                {"Low Quality Pistons", "Pistons de basse qualité"},
                {"Medium Quality Pistons", "Pistons de qualité moyenne"},
                {"High Quality Pistons", "Pistons de haute qualité"},
                {"Low Quality Spark Plugs", "Bougies d'allumage de basse qualité"},
                {"Medium Quality Spark Plugs", "Bougies d'allumage de qualité moyenne"},
                {"High Quality Spark Plugs", "Bougies d'allumage de haute qualité"},
                {"Low Quality Valves", "Vannes de basse qualité"},
                {"Medium Quality Valves", "Vannes de qualité moyenne"},
                {"High Quality Valves", "Vannes de haute qualité"},
                {"Electric Fuse", "Fusible électrique"},
                {"Gears", "Engrenages"},
                {"Glue", "Colle"},
                {"Metal Blade", "Lame en métal"},
                {"Metal Pipe", "Tuyau métallique"},
                {"Empty Propane Tank", "Réservoir de propane vide"},
                {"Road Signs", "Panneaux routiers"},
                {"Rope", "Corde"},
                {"Sewing Kit", "Kit de couture"},
                {"Sheet Metal", "Tôle"},
                {"Metal Spring", "Ressort en métal"},
                {"Sticks", "Bâtons"},
                {"Tarp", "Bâche"},
                {"Tech Trash", "Circuits imprimés"},
                {"Rifle Body", "Corps de fusil"},
                {"Semi Automatic Body", "Corps semi-automatique"},
                {"SMG Body", "Corps SMG"},
                {"Concrete Barricade", "Barricade en béton"},
                {"Wooden Barricade Cover", "Couverture de barricade en bois"},
                {"Metal Barricade", "Barricade en métal"},
                {"Sandbag Barricade", "Barricade de sacs de sable"},
                {"Stone Barricade", "Barricade en pierre"},
                {"Wooden Barricade", "Barricade en bois"},
                {"Barbed Wooden Barricade", "Barricade en bois barbelé"},
                {"Barbeque", "Barbecue"},
                {"Snap Trap", "Piège à ours"},
                {"Bed", "Lit"},
                {"Camp Fire", "Feu de camp"},
                {"Ceiling Light", "Plafonnier"},
                {"Chair", "chaise"},
                {"Composter", "Composteur"},
                {"Computer Station", "Station informatique"},
                {"Drop Box", "Boîte de dépôt"},
                {"Elevator", "Ascenseur"},
                {"Stone Fireplace", "Cheminée en pierre"},
                {"Blue Boomer", "Boomer bleu"},
                {"Champagne Boomer", "Champagne Boomer"},
                {"Green Boomer", "Boomer vert"},
                {"Orange Boomer", "Boumeur orange"},
                {"Red Boomer", "Boomer rouge"},
                {"Violet Boomer", "Boomer violet"},
                {"Blue Roman Candle", "Bougie romaine bleue"},
                {"Green Roman Candle", "Bougie romaine verte"},
                {"Red Roman Candle", "Bougie romaine rouge"},
                {"Violet Roman Candle", "Bougie romaine violette"},
                {"White Volcano Firework", "Feu d'artifice volcan blanc"},
                {"Red Volcano Firework", "Feu d'artifice volcan rouge"},
                {"Violet Volcano Firework", "Feu d'artifice volcan violet"},
                {"Wooden Floor Spikes", "Piques en bois au sol"},
                {"Fridge", "Réfrigérateur"},
                {"Large Furnace", "Grand fourneau"},
                {"Furnace", "fourneau"},
                {"Hitch & Trough", "Attelage et auge"},
                {"Hab Repair", "Tapis en peau d'ours"},
                {"Jack O Lantern Angry", "Jack O Lantern en colère"},
                {"Jack O Lantern Happy", "Jack O Lantern heureux"},
                {"Land Mine", "Mine terrestre"},
                {"Lantern", "Lanterne"},
                {"Large Wood Box", "Grande boîte en bois"},
                {"Water Barrel", "Baril d'eau"},
                {"Locker", "Casier"},
                {"Mail Box", "Boites aux lettres"},
                {"Mixing Table", "Table de mélange"},
                {"Modular Car Lift", "Ascenseur modulaire de voiture"},
                {"Pump Jack", "Pompe Jack"},
                {"Small Oil Refinery", "Petite raffinerie de pétrole"},
                {"Large Planter Box", "Grande jardinière"},
                {"Small Planter Box", "Petite jardinière"},
                {"Audio Alarm", "Alarme audio"},
                {"Smart Alarm", "Alarme intelligente"},
                {"Smart Switch", "Commutateur intelligent"},
                {"Storage Monitor", "Moniteur de stockage"},
                {"Large Rechargable Battery", "Grande batterie rechargeable"},
                {"Medium Rechargable Battery", "Batterie rechargeable moyenne"},
                {"Small Rechargable Battery", "Petite batterie rechargeable"},
                {"Button", "Bouton"},
                {"Counter", "Compteur"},
                {"HBHF Sensor", "Capteur HBHF"},
                {"Laser Detector", "Détecteur laser"},
                {"Pressure Pad", "bloc de pression"},
                {"Door Controller", "Contrôleur de porte"},
                {"Electric Heater", "Chauffage électrique"},
                {"Fluid Combiner", "combinateur de fluide"},
                {"Fluid Splitter", "Séparateur de fluides"},
                {"Fluid Switch & Pump", "Commutateur de fluide"},
                {"AND Switch", "Commutateur ET"},
                {"Blocker", "Bloqueur"},
                {"Electrical Branch", "Embranchement électrique"},
                {"Root Combiner", "Combinateur de source"},
                {"Memory Cell", "Cellule mémoire"},
                {"OR Switch", "Commutateur OU"},
                {"RAND Switch", "Commutateur aléatoire"},
                {"RF Broadcaster", "Diffuseur RF"},
                {"RF Receiver", "Récepteur RF"},
                {"XOR Switch", "Commutateur XOR"},
                {"Small Generator", "Petit générateur"},
                {"Test Generator", "Générateur de test"},
                {"Large Solar Panel", "Grand panneau solaire"},
                {"Igniter", "Allumeur"},
                {"Flasher Light", "Lumière clignotante"},
                {"Simple Light", "Lumière simple"},
                {"Siren Light", "Sirène Lumière"},
                {"Powered Water Purifier", "Purificateur d'eau alimenté"},
                {"Switch", "Commutateur"},
                {"Splitter", "Splitter"},
                {"Sprinkler", "Arroseur"},
                {"Tesla Coil", "Bobine Tesla"},
                {"Timer", "Minuteur"},
                {"Cable Tunnel", "Tunnel de câble"},
                {"Water Pump", "Pompe à eau"},
                {"Mining Quarry", "Carrière minière"},
                {"Reactive Target", "Cible réactive"},
                {"Repair Bench", "Banc de réparation"},
                {"Research Table", "Table de recherche"},
                {"Rug Bear Skin", "Peau d'ours de tapis"},
                {"Rug", "Couverture"},
                {"Search Light", "Lumière de recherche"},
                {"Secret Lab Chair", "Chaise de laboratoire secret"},
                {"Salvaged Shelves", "Étagères récupérées"},
                {"Large Banner Hanging", "Grande bannière suspendue"},
                {"Two Sided Hanging Sign", "Panneau suspendu à deux côtés"},
                {"Two Sided Ornate Hanging Sign", "Panneau suspendu orné à deux côtés"},
                {"Landscape Picture Frame", "Cadre photo de paysage"},
                {"Portrait Picture Frame", "Cadre photo portrait"},
                {"Tall Picture Frame", "Cadre photo haut"},
                {"XL Picture Frame", "Cadre photo XL"},
                {"XXL Picture Frame", "Cadre photo XXL"},
                {"Large Banner on pole", "Grande bannière sur poteau"},
                {"Double Sign Post", "Poteau de signalisation double"},
                {"Single Sign Post", "Poteau de signalisation unique"},
                {"One Sided Town Sign Post", "Poteau de signalisation de ville unilatéral"},
                {"Two Sided Town Sign Post", "Poteau de signalisation de ville à deux côtés"},
                {"Huge Wooden Sign", "Énorme panneau en bois"},
                {"Large Wooden Sign", "Grand panneau en bois"},
                {"Medium Wooden Sign", "Panneau en bois moyen"},
                {"Small Wooden Sign", "Petit panneau en bois"},
                {"Shotgun Trap", "Piège à fusil de chasse"},
                {"Sleeping Bag", "Sac de couchage"},
                {"Small Stash", "Petite réserve"},
                {"Spinning wheel", "Rouet"},
                {"Survival Fish Trap", "Piège à poissons de survie"},
                {"Table", "Table"},
                {"Work Bench Level 1", "Établi niveau 1"},
                {"Work Bench Level 2", "Établi niveau 2"},
                {"Work Bench Level 3", "Établi niveau 3"},
                {"Tool Cupboard", "Armoire à outils"},
                {"Tuna Can Lamp", "Lampe de boîte de thon"},
                {"Vending Machine", "Distributeur automatique"},
                {"Large Water Catcher", "Large Water Catcher"},
                {"Small Water Catcher", "Petit receveur d'eau"},
                {"Water Purifier", "Purificateur d'eau"},
                {"Wind Turbine", "Éolienne"},
                {"Wood Storage Box", "Boîte de rangement en bois"},
                {"Apple", "Pomme"},
                {"Rotten Apple", "Pomme pourrie"},
                {"Black Raspberries", "Framboises noires"},
                {"Blueberries", "Myrtilles"},
                {"Bota Bag", "Sac Bota"},
                {"Cactus Flesh", "Chair de cactus"},
                {"Can of Beans", "Boîte de haricots"},
                {"Can of Tuna", "Boîte de thon"},
                {"Chocolate Bar", "Barre de chocolat"},
                {"Cooked Fish", "Poisson cuit"},
                {"Raw Fish", "Poisson cru"},
                {"Minnows", "Minnows"},
                {"Small Trout", "Petite truite"},
                {"Granola Bar", "Barre granola"},
                {"Burnt Chicken", "Poulet brûlé"},
                {"Cooked Chicken", "Poulet cuit"},
                {"Raw Chicken Breast", "Poitrine de poulet crue"},
                {"Spoiled Chicken", "Poulet gâté"},
                {"Burnt Deer Meat", "Viande de cerf brûlé"},
                {"Cooked Deer Meat", "Viande de cerf cuit"},
                {"Raw Deer Meat", "Viande de cerf crue"},
                {"Burnt Horse Meat", "Viande de cheval brûlée"},
                {"Cooked Horse Meat", "Viande de cheval cuite"},
                {"Raw Horse Meat", "Viande de cheval crue"},
                {"Burnt Human Meat", "Viande humaine brûlée"},
                {"Cooked Human Meat", "Viande humaine cuite"},
                {"Raw Human Meat", "Viande humaine crue"},
                {"Spoiled Human Meat", "Viande humaine avariée"},
                {"Burnt Bear Meat", "Viande d'ours brûlée"},
                {"Cooked Bear Meat", "Viande d'ours cuite"},
                {"Raw Bear Meat", "Viande d'ours crue"},
                {"Burnt Wolf Meat", "Viande de loup brûlé"},
                {"Cooked Wolf Meat", "Viande de loup cuite"},
                {"Raw Wolf Meat", "Viande de loup crue"},
                {"Spoiled Wolf Meat", "Viande de loup gâtée"},
                {"Burnt Pork", "Porc brûlé"},
                {"Cooked Pork", "Porc Cuit"},
                {"Raw Pork", "Porc cru"},
                {"Mushroom", "Champignon"},
                {"Pickles", "Cornichons"},
                {"Small Water Bottle", "Petite bouteille d'eau"},
                {"Water Jug", "Cruche d'eau"},
                {"Shovel Bass", "Pelle basse"},
                {"Cowbell", "Cloche de vache"},
                {"Junkyard Drum Kit", "Kit de batterie Junkyard"},
                {"Pan Flute", "Flûte de pan"},
                {"Acoustic Guitar", "Guitare acoustique"},
                {"Jerry Can Guitar", "Jerry Can Guitar"},
                {"Wheelbarrow Piano", "Piano à brouette"},
                {"Canbourine", "Cambourin"},
                {"Plumber's Trumpet", "Trompette de plombier"},
                {"Sousaphone", "Sousaphone"},
                {"Xylobone", "Xylobone"},
                {"Car Key", "Clef de voiture"},
                {"Door Key", "Clé de porte"},
                {"Key Lock", "Serrure à clé"},
                {"Code Lock", "Digicode"},
                {"Blueprint", "Plan"},
                {"Chinese Lantern", "Lanterne chinoise"},
                {"Dragon Door Knocker", "Heurtoir de porte dragon"},
                {"Dragon Mask", "Masque de dragon"},
                {"New Year Gong", "Nouvel an Gong"},
                {"Rat Mask", "Masque de rat"},
                {"Firecracker String", "Ficelle de pétard"},
                {"Chippy Arcade Game", "Jeu d'arcade Chippy"},
                {"Door Closer", "Ferme-porte"},
                {"Bunny Ears", "Oreilles de lapin"},
                {"Bunny Onesie", "Grenouillère lapin"},
                {"Easter Door Wreath", "Couronne de porte de Pâques"},
                {"Egg Basket", "Panier d'oeufs"},
                {"Rustigé Egg - Red", "Oeuf Rustigé - Rouge"},
                {"Rustigé Egg - Blue", "Oeuf Rustigé - Bleu"},
                {"Rustigé Egg - Purple", "Œuf Rustigé - Violet"},
                {"Rustigé Egg - Ivory", "Oeuf Rustigé - Ivoire"},
                {"Nest Hat", "Chapeau de nid"},
                {"Bronze Egg", "Œuf en bronze"},
                {"Gold Egg", "Oeuf d'or"},
                {"Painted Egg", "Œuf peint"},
                {"Silver Egg", "Œuf d'argent"},
                {"Halloween Candy", "Bonbons d'Halloween"},
                {"Large Candle Set", "Grand ensemble de bougies"},
                {"Small Candle Set", "Ensemble de petites bougies"},
                {"Coffin", "Cercueil"},
                {"Cursed Cauldron", "Chaudron maudit"},
                {"Gravestone", "Pierre tombale"},
                {"Wooden Cross", "Croix en bois"},
                {"Graveyard Fence", "Clôture de cimetière"},
                {"Large Loot Bag", "Grand sac de butin"},
                {"Medium Loot Bag", "Sac de butin moyen"},
                {"Small Loot Bag", "Petit sac de butin"},
                {"Pumpkin Bucket", "Seau de citrouille"},
                {"Scarecrow", "Épouvantail"},
                {"Skull Spikes", "Pointes de crâne"},
                {"Skull Door Knocker", "Heurtoir de porte crâne"},
                {"Skull Fire Pit", "Fosse de crâne en feu"},
                {"Spider Webs", "Toiles d'araignée"},
                {"Spooky Speaker", "haut-parleur effrayant"},
                {"Surgeon Scrubs", "Blouse de chirurgien"},
                {"Skull Trophy", "Trophée de crâne"},
                {"Card Movember Moustache", "Carte moustache du Movembre"},
                {"Movember Moustache", "Moustache du Movember"},
                {"Note", "Note"},
                {"Human Skull", "Crâne humain"},
                {"Above Ground Pool", "Piscine hors terre"},
                {"Beach Chair", "Chaise de plage"},
                {"Beach Parasol", "Parasol de plage"},
                {"Beach Table", "Table de plage"},
                {"Beach Towel", "Serviette de plage"},
                {"Boogie Board", "Plance de surf"},
                {"Inner Tube", "Chambre à air"},
                {"Instant Camera", "Appareil photo instantané"},
                {"Paddling Pool", "Pataugeoire"},
                {"Photograph", "Photographie"},
                {"Landscape Photo Frame", "Cadre photo paysage"},
                {"Large Photo Frame", "Grand cadre photo"},
                {"Portrait Photo Frame", "Cadre photo portrait"},
                {"Sunglasses", "Lunettes de soleil"},
                {"Water Gun", "Pistolet à eau"},
                {"Water Pistol", "Pistolet à eau"},
                {"Purple Sunglasses", "Lunettes de soleil violettes"},
                {"Headset", "Casque"},
                {"Candy Cane Club", "Candy Cane Club"},
                {"Christmas Lights", "Lumières de Noël"},
                {"Festive Doorway Garland", "Guirlande de porte festive"},
                {"Candy Cane", "Sucre d'orge"},
                {"Giant Candy Decor", "Décor de bonbons géants"},
                {"Giant Lollipop Decor", "Décor de sucette géante"},
                {"Pookie Bear", "Ours Pookie"},
                {"Deluxe Christmas Lights", "Lumières de Noël de luxe"},
                {"Coal :(", "Charbon :("},
                {"Large Present", "Grand cadeau"},
                {"Medium Present", "Présent moyen"},
                {"Small Present", "Petit cadeau"},
                {"Snow Machine", "Machine à neige"},
                {"Snowball", "Boule de neige"},
                {"Snowman", "Bonhomme de neige"},
                {"SUPER Stocking", "SUPER bas"},
                {"Small Stocking", "Petit bas"},
                {"Reindeer Antlers", "Bois de renne"},
                {"Santa Beard", "Barbe du Père Noël"},
                {"Santa Hat", "Chapeau de père Noël"},
                {"Festive Window Garland", "Guirlande de fenêtre festive"},
                {"Wrapped Gift", "Cadeau emballé"},
                {"Wrapping Paper", "Papier cadeau"},
                {"Christmas Door Wreath", "Couronne de porte de Noël"},
                {"Decorative Baubels", "Baubels décoratifs"},
                {"Decorative Plastic Candy Canes", "Cannes de bonbon en plastique décoratives"},
                {"Decorative Gingerbread Men", "Hommes de pain d'épice décoratifs"},
                {"Tree Lights", "Lumières d'arbre"},
                {"Decorative Pinecones", "Pommes de pin décoratives"},
                {"Star Tree Topper", "Etoile de sapin"},
                {"Decorative Tinsel", "Guirlandes décoratives"},
                {"Christmas Tree", "Sapin de Noël"},
                {"Auto Turret", "Tourelle automatique"},
                {"Flame Turret", "Tourelle de flammes"},
                {"Glowing Eyes", "Yeux brillants"},
                {"SAM Ammo", "Munitions SAM"},
                {"SAM Site", "Site SAM"},
                {"Black Berry", "Baie noire"},
                {"Black Berry Clone", "Clone de baie noire"},
                {"Black Berry Seed", "Graine de baies noires"},
                {"Blue Berry", "Myrtille"},
                {"Blue Berry Clone", "Clone de baie bleue"},
                {"Blue Berry Seed", "Graine de baies bleues"},
                {"Green Berry", "Baie verte"},
                {"Green Berry Clone", "Clone de baie verte"},
                {"Green Berry Seed", "Graine de baies vertes"},
                {"Red Berry", "Fruits rouges"},
                {"Red Berry Clone", "Clone de fruits rouges"},
                {"Red Berry Seed", "Graine de fruits rouges"},
                {"White Berry", "Baies blanches"},
                {"White Berry Clone", "Clone de baies blanches"},
                {"White Berry Seed", "Graine de baies blanches"},
                {"Yellow Berry", "Baie jaune"},
                {"Yellow Berry Clone", "Clone de baies jaunes"},
                {"Yellow Berry Seed", "Graine de baies jaunes"},
                {"Corn", "Blé"},
                {"Corn Clone", "Clone de maïs"},
                {"Corn Seed", "Graine de maïs"},
                {"Hemp Clone", "Clone de chanvre"},
                {"Hemp Seed", "Graine de chanvre"},
                {"Potato", "Patate"},
                {"Potato Clone", "Clone de pomme de terre"},
                {"Potato Seed", "Graine de pomme de terre"},
                {"Pumpkin", "Citrouille"},
                {"Pumpkin Plant Clone", "Clone de plante de citrouille"},
                {"Pumpkin Seed", "Graine de citrouille"},
                {"Animal Fat", "Graisse animale"},
                {"Battery - Small", "Batterie - Petite"},
                {"Blood", "Sang"},
                {"Bone Fragments", "Fragments osseux"},
                {"CCTV Camera", "Caméra de vidéosurveillance"},
                {"Charcoal", "charbon"},
                {"Cloth", "Tissu"},
                {"Crude Oil", "Huile brute"},
                {"Diesel Fuel", "Gas-oil"},
                {"Empty Can Of Beans", "Boîte vide de haricots"},
                {"Empty Tuna Can", "Boîte de thon vide"},
                {"Explosives", "Explosifs"},
                {"Fertilizer", "Engrais"},
                {"Gun Powder", "Poudre à canon"},
                {"Horse Dung", "Bouse de cheval"},
                {"High Quality Metal Ore", "Minerai de métal de haute qualité"},
                {"High Quality Metal", "Métal de haute qualité"},
                {"Leather", "Cuir"},
                {"Low Grade Fuel", "Carburant de qualité inférieure"},
                {"Metal Fragments", "Fragments métalliques"},
                {"Metal Ore", "Minerai de métal"},
                {"Paper", "Papier"},
                {"Plant Fiber", "Fibre végétale"},
                {"Research Paper", "Document de recherche"},
                {"Salt Water", "Eau salée"},
                {"Scrap", "Ferraille"},
                {"Stones", "Des pierres"},
                {"Sulfur Ore", "Minerai de soufre"},
                {"Sulfur", "Soufre"},
                {"Targeting Computer", "Ordinateur de ciblage"},
                {"Water", "L'eau"},
                {"Wolf Skull", "Crâne de loup"},
                {"Wood", "Bois"},
                {"Advanced Healing Tea", "Thé de guérison avancé"},
                {"Basic Healing Tea", "Thé de guérison de basique"},
                {"Pure Healing Tea", "Thé de guérison pur"},
                {"Advanced Max Health Tea", "Thé avancé Max Health"},
                {"Basic Max Health Tea", "Thé de base Max Health"},
                {"Pure Max Health Tea", "Thé Pure Max Health"},
                {"Advanced Ore Tea", "Thé au minerai avancé"},
                {"Basic Ore Tea", "Thé au minerai de base"},
                {"Pure Ore Tea", "Thé minéral pur"},
                {"Advanced Rad. Removal Tea", "Rad avancé. Thé de retrait"},
                {"Rad. Removal Tea", "Rad. Thé de retrait"},
                {"Pure Rad. Removal Tea", "Pure Rad. Thé de retrait"},
                {"Adv. Anti-Rad Tea", "Adv. Thé anti-rad"},
                {"Anti-Rad Tea", "Thé anti-rad"},
                {"Pure Anti-Rad Tea", "Thé Anti-Rad pure"},
                {"Advanced Scrap Tea", "Thé de rebut avancé"},
                {"Basic Scrap Tea", "Thé de base"},
                {"Pure Scrap Tea", "Thé pur"},
                {"Advanced Wood Tea", "Thé de bois avancé"},
                {"Basic Wood Tea", "Thé au bois de base"},
                {"Pure Wood Tea", "Thé de bois pur"},
                {"Anti-Radiation Pills", "Pilules anti-radiations"},
                {"Binoculars", "Jumelles"},
                {"Timed Explosive Charge", "Charge explosive chronométrée"},
                {"Camera", "Caméra"},
                {"RF Transmitter", "Émetteur RF"},
                {"Handmade Fishing Rod", "Canne à pêche à la main"},
                {"Flare", "Éclater"},
                {"Flashlight", "Lampe de poche"},
                {"Geiger Counter", "Compteur Geiger"},
                {"Hose Tool", "Outil de tuyau"},
                {"Jackhammer", "Marteau-piqueur"},
                {"Blue Keycard", "Carte d'accès bleue"},
                {"Green Keycard", "Carte d'accès verte"},
                {"Red Keycard", "Carte d'accès rouge"},
                {"Large Medkit", "Grand Medkit"},
                {"Paper Map", "Carte papier"},
                {"Medical Syringe", "Seringue médicale"},
                {"RF Pager", "Téléavertisseur RF"},
                {"Building Plan", "Plan de construction"},
                {"Smoke Grenade", "Grenade fumigène"},
                {"Supply Signal", "Signal d'alimentation"},
                {"Survey Charge", "Frais d'enquête"},
                {"Wire Tool", "Outil cable"},
                {"Small Chassis", "Petit châssis"},
                {"Medium Chassis", "Châssis moyen"},
                {"Large Chassis", "Grand châssis"},
                {"Cockpit Vehicle Module", "Module de véhicule de cockpit"},
                {"Armored Cockpit Vehicle Module", "Module de véhicule blindé de cockpit"},
                {"Cockpit With Engine Vehicle Module", "Cockpit avec module de véhicule moteur"},
                {"Engine Vehicle Module", "Module de véhicule moteur"},
                {"Flatbed Vehicle Module", "Module de véhicule à plateau"},
                {"Armored Passenger Vehicle Module", "Module de véhicule de tourisme blindé"},
                {"Rear Seats Vehicle Module", "Module de véhicule pour sièges arrière"},
                {"Storage Vehicle Module", "Module de véhicule de stockage"},
                {"Taxi Vehicle Module", "Module de véhicule de taxi"},
                {"Large Flatbed Vehicle Module", "Grand module de véhicule à plateau"},
                {"Fuel Tank Vehicle Module", "Module de véhicule de réservoir de carburant"},
                {"Passenger Vehicle Module", "Module de véhicule de tourisme"},
                {"Generic vehicle module", "Module véhicule générique"},
                {"Telephone", "Téléphone"},
                {"16x Zoom Scope", "Lunette zoom 16x"},
                {"Weapon flashlight", "Lampe de poche d'arme"},
                {"Holosight", "Holosight"},
                {"Weapon Lasersight", "Arme de visée laser"},
                {"Muzzle Boost", "Muselière Boost"},
                {"Muzzle Brake", "Frein de bouche"},
                {"Simple Handmade Sight", "Viseur simple artisanal"},
                {"Silencer", "Silencieux"},
                {"8x Zoom Scope", "Lunette zoom 8x"},
                {"Assault Rifle", "Fusil d'assaut"},
                {"Bandage", "Bandage"},
                {"Beancan Grenade", "Grenade boite de haricots"},
                {"Bolt Action Rifle", "Fusil à verrou"},
                {"Bone Club", "Gourdin en os"},
                {"Bone Knife", "Couteau en os"},
                {"Hunting Bow", "Arc de chasse"},
                {"Birthday Cake", "Gâteau d'anniversaire"},
                {"Chainsaw", "Tronçonneuse"},
                {"Salvaged Cleaver", "Couperet de récupération"},
                {"Compound Bow", "Arc à poulies"},
                {"Crossbow", "Arbalète"},
                {"Double Barrel Shotgun", "Fusil de chasse à double canon"},
                {"Eoka Pistol", "Pistolet Eoka"},
                {"F1 Grenade", "F1 Grenade"},
                {"Flame Thrower", "Lance-flammes"},
                {"Multiple Grenade Launcher", "Lance-grenades multiples"},
                {"Butcher Knife", "Couteau de boucher"},
                {"Pitchfork", "Fourche"},
                {"Sickle", "Faucille"},
                {"Hammer", "Marteau"},
                {"Hatchet", "Hachette"},
                {"Combat Knife", "Couteau de combat"},
                {"L96 Rifle", "Fusil L96"},
                {"LR-300 Assault Rifle", "LR-300 Fusil d'assaut"},
                {"M249", "M249"},
                {"M39 Rifle", "Fusil M39"},
                {"M92 Pistol", "Pistolet M92"},
                {"Mace", "Masse"},
                {"Machete", "Machette"},
                {"MP5A4", "MP5A4"},
                {"Nailgun", "Cloueuse"},
                {"Paddle", "Pagaie"},
                {"Pickaxe", "Pioche"},
                {"Waterpipe Shotgun", "Fusil à pompe à eau"},
                {"Python Revolver", "Revolver Python"},
                {"Revolver", "Revolver"},
                {"Rock", "Roche"},
                {"Rocket Launcher", "Lance-roquettes"},
                {"Salvaged Axe", "Hache de récupération"},
                {"Salvaged Hammer", "Marteau de récupération"},
                {"Salvaged Icepick", "Piaulé de récupération"},
                {"Satchel Charge", "Charge de sacoche"},
                {"Pump Shotgun", "Fusil à pompe"},
                {"Semi-Automatic Pistol", "Pistolet semi-automatique"},
                {"Semi-Automatic Rifle", "Fusil semi-automatique"},
                {"Custom SMG", "SMG personnalisé"},
                {"Spas-12 Shotgun", "Fusil de chasse Spas-12"},
                {"Stone Hatchet", "Hachette en pierre"},
                {"Stone Pickaxe", "Pioche en pierre"},
                {"Stone Spear", "Lance en pierre"},
                {"Longsword", "Épée longue"},
                {"Salvaged Sword", "Épée récupérée"},
                {"Thompson", "Thompson"},
                {"Garry's Mod Tool Gun", "Pistolet à outils Mod de Garry"},
                {"Torch", "Torche"},
                {"Water Bucket", "Seau d'eau"},
                {"Wooden Spear", "Lance en bois"},
                {"Roadsign Horse Armor", "Armure de cheval en panneau de signalisation"},
                {"Wooden Horse Armor", "Armure de cheval en bois"},
                {"Horse Saddle", "Selle de cheval"},
                {"Saddle bag", "Sac à explosifs"},
                {"High Quality Horse Shoes", "Chaussures de cheval de haute qualité"},
                {"Basic Horse Shoes", "Fer a cheval de base"},
                {"Generic vehicle chassis", "Châssis de véhicule générique"}
            }, this, "fr"); //french

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MessageShowNoEconomics", "GUIShop fick inte svar från pluginet Economics. Se till att plugin för ekonomi är korrekt installerad."},
                {"MessageShowNoServerRewards", "GUIShop fick inte svar från ServerRewards-plugin. Se till att ServerRewards-tillägget är korrekt installerat."},
                {"MessageBought", "Du har köpt {0} x {1}."},
                {"MessageSold", "Du har sålt {0} x {1}."},
                {"MessageErrorCooldown", "Du kan bara köpa den här varan var {0} sekund."},
                {"MessageErrorSellCooldownAmount", "Du kan bara sälja {0} av denna artikel var {1} sekund."},
                {"MessageErrorBuyCooldownAmount", "Du kan bara köpa {0} av den här varan var {1} sekund."},
                {"MessageErrorBuyLimit", "Du kan bara köpa {0} av denna artikel."},
                {"MessageErrorSellLimit", "Du kan bara sälja {0} av denna artikel."},
                {"MessageErrorInventoryFull", "Ditt lager är fullt."},
                {"MessageErrorInventorySlots", "Du behöver minst {0} lediga lagerplatser."},
                {"MessageErrorNoShop", "Det är något fel med denna butik. Kontakta en administratör."},
                {"MessageErrorGlobalDisabled", "Globala butiker är inaktiverade. Denna server använder NPC-leverantörer!"},
                {"MessageErrorNoActionShop", "Du får inte {0} i den här butiken"},
                {"MessageErrorNoActionItem", "Du har inte tillåtelse att {0} det här objektet här"},
                {"MessageErrorItemItem", "VARNING: Det verkar som att detta säljföremål du har inte är ett giltigt objekt! Vänligen kontakta en administratör!"},
                {"MessageErrorItemNoValidbuy", "VARNING: Det verkar som om det inte är ett giltigt objekt att köpa. Kontakta en administratör!"},
                {"MessageErrorItemNoValidsell", "VARNING: Det verkar som om det inte är ett giltigt objekt att sälja. Kontakta en administratör!"},
                {"MessageErrorRedeemKit", "VARNING: Det uppstod ett fel när du gav dig detta kit. Kontakta en administratör!"},
                {"MessageErrorBuyCmd", "Kan inte köpa flera av denna artikel!"},
                {"MessageErrorBuyPrice", "VARNING: Inget köppris gavs av administratören, du kan inte köpa denna artikel"},
                {"MessageErrorSellPrice", "VARNING: Inget försäljningspris gavs av administratören, du kan inte sälja denna artikel"},
                {"MessageErrorNotEnoughMoney", "Du behöver {0} mynt för att köpa {1} av {2}"},
                {"MessageErrorNotEnoughMoneyCustom", "Du behöver {0} valuta för att köpa {1} x {2}"},
                {"MessageErrorNotEnoughSell", "Du har inte tillräckligt med det här objektet."},
                {"MessageErrorNotNothing", "Du kan inte köpa noll av denna artikel."},
                {"MessageErrorItemNoExist", "VARNING: Varan du försöker köpa verkar inte existera! Vänligen kontakta en administratör!"},
                {"MessageErrorItemNoExistTake", "Föremålet du försöker sälja kan inte säljas just nu."},
                {"MessageErrorBuildingBlocked", "Du kan inte handla när du är i ett byggnadsspärrat område."},
                {"MessageErrorAdmin", "Du har inte rätt behörighet att använda detta kommando. (guishop.admin)"},
                {"MessageErrorWaitingOnDownloads", "GUIShop väntar på att ImageLibrary-nedladdningar ska slutföras"},
                {"BlockMonuments", "Du får inte använda butiken i närheten av ett monument!"},
                {"MessageErrorItemNotEnabled", "Butiksinnehavaren har inaktiverat detta föremål."},
                {"MessageErrorItemNotFound", "Objektet hittades inte"},
                {"CantSellCommands", "Du kan inte sälja kommandon tillbaka till butiken."},
                {"CantSellKits", "Du kan inte sälja kit tillbaka till butiken."},
                {"MessageErrorCannotSellWhileEquiped", "Du kan inte sälja föremålet om du har det."},
                {"MessageShopResponse", "GUIShop väntar på att ImageLibrary-nedladdningar ska slutföras. Vänta."},
                {"MessageNPCResponseclose", "Tack för att du handlar på {0} kom igen snart!"},
                {"MessageNPCResponseopen", "Välkommen till {0} vad vill du köpa? Tryck på E för att börja handla!"},
                {"Commands", "Kommandon"},
                {"Attire", "Klädsel"},
                {"Misc", "Övrigt"},
                {"Items", "Objekt"},
                {"Ammunition", "Ammunition"},
                {"Construction", "Konstruktion"},
                {"Component", "Komponent"},
                {"Traps", "Fällor"},
                {"Electrical", "Elektrisk"},
                {"Fun", "Roligt"},
                {"Food", "Mat"},
                {"Resources", "Resurser"},
                {"Tool", "Verktyg"},
                {"Weapon", "Vapen"},
                {"Medical", "Medicinsk"},
                {"Minicopter", "Minikopter"},
                {"Sedan", "Sedan"},
                {"Airdrop Call", "Airdrop Call"},
                {"Wolf Headdress", "Varg huvudbonad"},
                {"Fogger-3000", "Fogger-3000"},
                {"Strobe Light", "Strobokopsljus"},
                {"Kayak", "Kajak"},
                {"MC repair", "MC-reparation"},
                {"ScrapTransportHeliRepair", "ScrapTransportHeliRepair"},
                {"40mm Shotgun Round", "40 mm hagelgevär ammunition"},
                {"40mm HE Grenade", "40 mm HE-granat"},
                {"40mm Smoke Grenade", "40 mm rökgranat"},
                {"High Velocity Arrow", "Pil med hög hastighet"},
                {"Wooden Arrow", "Träpil"},
                {"Bone Arrow", "Benpil"},
                {"Fire Arrow", "Eldpil"},
                {"Handmade Shell", "Handgjord skal"},
                {"Nailgun Nails", "Spikpistol spik"},
                {"Pistol Bullet", "Pistolkula"},
                {"Incendiary Pistol Bullet", "Brand Pistolkula"},
                {"HV Pistol Ammo", "HV Pistol Ammo"},
                {"5.56 Rifle Ammo", "5.56 Gevär Ammo"},
                {"Explosive 5.56 Rifle Ammo", "Explosivt 5,56 Gevär Ammo"},
                {"Incendiary 5.56 Rifle Ammo", "Incendiary 5.56 Gevär Ammo"},
                {"HV 5.56 Rifle Ammo", "HV 5.56 Gevär Ammo"},
                {"Rocket", "Raket"},
                {"Incendiary Rocket", "Brandraket"},
                {"High Velocity Rocket", "Snabb raket"},
                {"Smoke Rocket WIP!!!!", "Rökraket WIP !!!!"},
                {"12 Gauge Buckshot", "12 Gauge grov hagel"},
                {"12 Gauge Incendiary Shell", "12 Gauge brandkula"},
                {"12 Gauge Slug", "12 Gauge Slug"},
                {"Sheet Metal Double Door", "Dubbel dörr av plåt"},
                {"Armored Double Door", "Pansrad dubbeldörr"},
                {"Wood Double Door", "Trä dubbel dörr"},
                {"Sheet Metal Door", "Plåtdörr"},
                {"Armored Door", "Pansrad dörr"},
                {"Wooden Door", "Trädörr"},
                {"Floor grill", "Golvgrill"},
                {"Ladder Hatch", "Stege Lucka"},
                {"Floor triangle grill", "Golv triangel grill"},
                {"Triangle Ladder Hatch", "Triangel Stege Lucka"},
                {"High External Stone Gate", "Hög extern stenport"},
                {"High External Wooden Gate", "Hög extern träport"},
                {"Wooden Ladder", "Trästege"},
                {"High External Stone Wall", "Hög yttre stenmur"},
                {"High External Wooden Wall", "Hög yttre trävägg"},
                {"Prison Cell Gate", "Fängelsecellport"},
                {"Prison Cell Wall", "Fängelsecellvägg"},
                {"Chainlink Fence Gate", "Kedjelänkad Staket Port"},
                {"Chainlink Fence", "Kedjelänkad Staket"},
                {"Garage Door", "Garageport"},
                {"Netting", "Nätning"},
                {"Shop Front", "Butiksfasad"},
                {"Metal Shop Front", "Metal Butiksfasad"},
                {"Metal Window Bars", "Metallfönsterstänger"},
                {"Reinforced Window Bars", "Förstärkta fönsterstänger"},
                {"Wooden Window Bars", "Träfönsterstänger"},
                {"Metal horizontal embrasure", "Metall horisontell omfamning"},
                {"Metal Vertical embrasure", "Metall Vertikal omfamning"},
                {"Reinforced Glass Window", "Förstärkt glasfönster"},
                {"Wood Shutters", "Träluckor"},
                {"Watch Tower", "Vakttornet"},
                {"Diving Fins", "Dykfenor"},
                {"Diving Mask", "Dykmask"},
                {"Diving Tank", "Dykningstank"},
                {"Wetsuit", "Våtdräkt"},
                {"Frog Boots", "Grod stövlar"},
                {"A Barrel Costume", "En fatdräkt"},
                {"Crate Costume", "Låd kostym"},
                {"Burlap Gloves", "Säckväv-handskar"},
                {"Leather Gloves", "Läderhandskar"},
                {"Roadsign Gloves", "Vägskyltshandskar"},
                {"Tactical Gloves", "Taktiska handskar"},
                {"Ghost Costume", "Spökdräkt"},
                {"Mummy Suit", "Mummidräkt"},
                {"Scarecrow Suit", "Fågelskrämma kostym"},
                {"Scarecrow Wrap", "Fågelskrämma mask"},
                {"Hide Halterneck", "Hud Halterneck"},
                {"Beenie Hat", "mössa"},
                {"Boonie Hat", "välsignelse mössa"},
                {"Bucket Helmet", "Hink-hjälm"},
                {"Burlap Headwrap", "Säckvävd-huvudduk"},
                {"Candle Hat", "Ljushatt"},
                {"Baseball Cap", "Basebollkeps"},
                {"Clatter Helmet", "Clatter Hjälm"},
                {"Coffee Can Helmet", "Kaffe burk hjälm"},
                {"Bone Helmet", "Benhjälm"},
                {"Heavy Plate Helmet", "Tung plåthjälm"},
                {"Miners Hat", "Gruvarbetare hatt"},
                {"Party Hat", "Party hatt"},
                {"Riot Helmet", "Riot Hjälm"},
                {"Wood Armor Helmet", "Trä Rustning Hjälm"},
                {"Hoodie", "Luvtröja"},
                {"Bone Armor", "Ben Rustning"},
                {"Heavy Plate Jacket", "Tung jacka"},
                {"Snow Jacket", "Snöjacka"},
                {"Jacket", "Jacka"},
                {"Wood Chestplate", "Träbröstkorg"},
                {"Improvised Balaclava", "Improviserad balaclava"},
                {"Bandana Mask", "Bandana Mask"},
                {"Metal Facemask", "Metall ansiktsmask"},
                {"Night Vision Goggles", "Mörkerglasögon"},
                {"Burlap Trousers", "Säckvävda byxor"},
                {"Heavy Plate Pants", "Tunga plattbyxor"},
                {"Hide Pants", "Hud byxor"},
                {"Road Sign Kilt", "Vägmärke Kilt"},
                {"Shorts", "Shorts"},
                {"Wood Armor Pants", "Wood Armor Pants"},
                {"Pants", "Byxor"},
                {"Hide Poncho", "Hud Poncho"},
                {"Burlap Shirt", "Säckvävd-tröja"},
                {"Shirt", "Skjorta"},
                {"Hide Vest", "Hud väst"},
                {"Tank Top", "Linne"},
                {"Boots", "Stövlar"},
                {"Burlap Shoes", "Säckvävda skor"},
                {"Hide Boots", "Hud stövlar"},
                {"Hide Skirt", "Hud kjol"},
                {"Bandit Guard Gear", "Bandit Vakt Utrustning"},
                {"Hazmat Suit", "Hazmat dräkt"},
                {"Scientist Suit", "Forskare dräkt"},
                {"Space Suit", "Rymddräkt"},
                {"Heavy Scientist Suit", "Tung vetenskapsdräkt"},
                {"Longsleeve T-Shirt", "Långärmad T-shirt"},
                {"T-Shirt", "T-shirt"},
                {"Metal Chest Plate", "Metallbröstplatta"},
                {"Road Sign Jacket", "Vägmärkejacka"},
                {"Bleach", "Blekmedel"},
                {"Duct Tape", "Silvertejp"},
                {"Low Quality Carburetor", "Förgasare av låg kvalitet"},
                {"Medium Quality Carburetor", "Medium förgasare"},
                {"High Quality Carburetor", "Högkvalitativ förgasare"},
                {"Low Quality Crankshaft", "Vevaxel av låg kvalitet"},
                {"Medium Quality Crankshaft", "Vevaxel av medelkvalitet"},
                {"High Quality Crankshaft", "Vevaxel av hög kvalitet"},
                {"Low Quality Pistons", "Kolvar av låg kvalitet"},
                {"Medium Quality Pistons", "Kolvar av medelkvalitet"},
                {"High Quality Pistons", "Kolvar av hög kvalitet"},
                {"Low Quality Spark Plugs", "Tändstift av låg kvalitet"},
                {"Medium Quality Spark Plugs", "Tändstift av medelkvalitet"},
                {"High Quality Spark Plugs", "Tändstift av hög kvalitet"},
                {"Low Quality Valves", "Ventiler av låg kvalitet"},
                {"Medium Quality Valves", "Ventiler av medelhög kvalitet"},
                {"High Quality Valves", "Ventiler av hög kvalitet"},
                {"Electric Fuse", "Elektrisk säkring"},
                {"Gears", "Kugghjul"},
                {"Glue", "Lim"},
                {"Metal Blade", "Metallblad"},
                {"Metal Pipe", "Metallrör"},
                {"Empty Propane Tank", "Töm propanbehållaren"},
                {"Road Signs", "Vägskyltar"},
                {"Rope", "Rep"},
                {"Sewing Kit", "Sy-kit"},
                {"Sheet Metal", "Plåt"},
                {"Metal Spring", "Metallfjäder"},
                {"Sticks", "Pinnar"},
                {"Tarp", "Presenning"},
                {"Tech Trash", "Teknik skrot"},
                {"Rifle Body", "Gevärskropp"},
                {"Semi Automatic Body", "Halvautomatisk kaross"},
                {"SMG Body", "SMG-kropp"},
                {"Concrete Barricade", "Betongbarrikad"},
                {"Wooden Barricade Cover", "Träbarrikadöverdrag"},
                {"Metal Barricade", "Metallbarrikad"},
                {"Sandbag Barricade", "Sandpås Barrikad"},
                {"Stone Barricade", "Stenbarrikad"},
                {"Wooden Barricade", "Träbarrikad"},
                {"Barbed Wooden Barricade", "Taggad barrikad av trä"},
                {"Barbeque", "Grill"},
                {"Snap Trap", "Snäppfälla"},
                {"Bed", "Säng"},
                {"Camp Fire", "Lägereld"},
                {"Ceiling Light", "Takljus"},
                {"Chair", "Stol"},
                {"Composter", "Komposter"},
                {"Computer Station", "Datorstation"},
                {"Drop Box", "Dropbox"},
                {"Elevator", "Hiss"},
                {"Stone Fireplace", "Sten eldstad"},
                {"Blue Boomer", "Blå Boomer"},
                {"Champagne Boomer", "Champagne Boomer"},
                {"Green Boomer", "Grön Boomer"},
                {"Orange Boomer", "Orange Boomer"},
                {"Red Boomer", "Röd Boomer"},
                {"Violet Boomer", "Violet Boomer"},
                {"Blue Roman Candle", "Blå romerskt ljus"},
                {"Green Roman Candle", "Grönt romerskt ljus"},
                {"Red Roman Candle", "Rött romerskt ljus"},
                {"Violet Roman Candle", "Violet Roman Candle"},
                {"White Volcano Firework", "Vit vulkan fyrverkeri"},
                {"Red Volcano Firework", "Röd vulkan fyrverkeri"},
                {"Violet Volcano Firework", "Violet vulkan fyrverkeri"},
                {"Wooden Floor Spikes", "Trägolvspikar"},
                {"Fridge", "Kylskåp"},
                {"Large Furnace", "Stor ugn"},
                {"Furnace", "Ugn"},
                {"Hitch & Trough", "Lifta"},
                {"Hab Repair", "Fick reparation"},
                {"Jack O Lantern Angry", "Jack O Lantern Arg"},
                {"Jack O Lantern Happy", "Jack O Lantern Glad"},
                {"Land Mine", "Landmina"},
                {"Lantern", "Lykta"},
                {"Large Wood Box", "Stor trälåda"},
                {"Water Barrel", "Vattenfat"},
                {"Locker", "Skåp"},
                {"Mail Box", "Brevlåda"},
                {"Mixing Table", "Blandningsbord"},
                {"Modular Car Lift", "Modulär billyft"},
                {"Pump Jack", "Pumputtag"},
                {"Small Oil Refinery", "Litet oljeraffinaderi"},
                {"Large Planter Box", "Stor planteringslåda"},
                {"Small Planter Box", "Liten planteringslåda"},
                {"Audio Alarm", "Ljudlarm"},
                {"Smart Alarm", "Smart larm"},
                {"Smart Switch", "Smart Switch"},
                {"Storage Monitor", "Lagringsmonitor"},
                {"Large Rechargable Battery", "Stort uppladdningsbart batteri"},
                {"Medium Rechargable Battery", "Medium uppladdningsbart batteri"},
                {"Small Rechargable Battery", "Litet uppladdningsbart batteri"},
                {"Button", "Knapp"},
                {"Counter", "Disken"},
                {"HBHF Sensor", "HBHF-sensor"},
                {"Laser Detector", "Laserdetektor"},
                {"Pressure Pad", "Tryckplatta"},
                {"Door Controller", "Av styrenhet"},
                {"Electric Heater", "Elvärmare"},
                {"Fluid Combiner", "Vätskblandare"},
                {"Fluid Splitter", "Vätskedelare"},
                {"Fluid Switch & Pump", "Vätskekontakt & Pump"},
                {"AND Switch", "OCH växling"},
                {"Blocker", "Blockerare"},
                {"Electrical Branch", "Elektrisk gren"},
                {"Root Combiner", "Rotkombinerare"},
                {"Memory Cell", "Minnescell"},
                {"OR Switch", "ELLER växling"},
                {"RAND Switch", "RAND-omkopplare"},
                {"RF Broadcaster", "RF-sändare"},
                {"RF Receiver", "RF-mottagare"},
                {"XOR Switch", "XOR-omkopplare"},
                {"Small Generator", "Liten generator"},
                {"Test Generator", "Testgenerator"},
                {"Large Solar Panel", "Stor solpanel"},
                {"Igniter", "Tändare"},
                {"Flasher Light", "Blinkersljus"},
                {"Simple Light", "Enkelt ljus"},
                {"Siren Light", "Sirenljus"},
                {"Powered Water Purifier", "Driven vattenrenare"},
                {"Switch", "Brytare"},
                {"Splitter", "Splittare"},
                {"Sprinkler", "Sprinklare"},
                {"Tesla Coil", "Teslaspol"},
                {"Timer", "Timer"},
                {"Cable Tunnel", "Kabeltunnel"},
                {"Water Pump", "Vattenpump"},
                {"Mining Quarry", "Gruvbrott"},
                {"Reactive Target", "Reaktivt mål"},
                {"Repair Bench", "Reparationsbänk"},
                {"Research Table", "Forskningstabell"},
                {"Rug Bear Skin", "Björn Huds Matta"},
                {"Rug", "Matta"},
                {"Search Light", "Sök Light"},
                {"Secret Lab Chair", "Hemlig Labb Stol"},
                {"Salvaged Shelves", "Räddade hyllor"},
                {"Large Banner Hanging", "Stort hängande banderoll"},
                {"Two Sided Hanging Sign", "Tvåsidigt hängande skylt"},
                {"Two Sided Ornate Hanging Sign", "Tvåsidigt utsmyckat hängande skylt"},
                {"Landscape Picture Frame", "Landskap bildram"},
                {"Portrait Picture Frame", "Stående bildram"},
                {"Tall Picture Frame", "Hög bildram"},
                {"XL Picture Frame", "XL bildram"},
                {"XXL Picture Frame", "XXL-bildram"},
                {"Large Banner on pole", "Stort banner på stolpe"},
                {"Double Sign Post", "Dubbel skyltstolpe"},
                {"Single Sign Post", "Enkel skyltstolpe"},
                {"One Sided Town Sign Post", "Ensidig stadskyltstolpe"},
                {"Two Sided Town Sign Post", "Tvåsidigt stadskyltstolpe"},
                {"Huge Wooden Sign", "Stort träskylt"},
                {"Large Wooden Sign", "Stort träskylt"},
                {"Medium Wooden Sign", "Medium träskylt"},
                {"Small Wooden Sign", "Litet träskylt"},
                {"Shotgun Trap", "Hagelgevär fälla"},
                {"Sleeping Bag", "Sovsäck"},
                {"Small Stash", "Liten gömma"},
                {"Spinning wheel", "Snurrande hjul"},
                {"Survival Fish Trap", "Överlevnadsfiskfälla"},
                {"Table", "Tabell"},
                {"Work Bench Level 1", "Arbetsbänk nivå 1"},
                {"Work Bench Level 2", "Arbetsbänk nivå 2"},
                {"Work Bench Level 3", "Arbetsbänk nivå 3"},
                {"Tool Cupboard", "Verktygsskåp"},
                {"Tuna Can Lamp", "Tonfiskburklampa"},
                {"Vending Machine", "Godisautomat"},
                {"Large Water Catcher", "Stor vattenfångare"},
                {"Small Water Catcher", "Liten vattenfångare"},
                {"Water Purifier", "Vattenrenare"},
                {"Wind Turbine", "Vindturbin"},
                {"Wood Storage Box", "Träförvaringslåda"},
                {"Apple", "Äpple"},
                {"Rotten Apple", "Ruttet äpple"},
                {"Black Raspberries", "Björnbär"},
                {"Blueberries", "Blåbär"},
                {"Bota Bag", "Bota väska"},
                {"Cactus Flesh", "Kaktus kött"},
                {"Can of Beans", "Burk med bönor"},
                {"Can of Tuna", "Tonfiskburk"},
                {"Chocolate Bar", "Chokladkaka"},
                {"Cooked Fish", "Kokt fisk"},
                {"Raw Fish", "Rå fisk"},
                {"Minnows", "Minnows"},
                {"Small Trout", "Liten öring"},
                {"Granola Bar", "Granola bar"},
                {"Burnt Chicken", "Bränd kyckling"},
                {"Cooked Chicken", "Kokt kyckling"},
                {"Raw Chicken Breast", "Rått kycklingbröst"},
                {"Spoiled Chicken", "Bortskämd kyckling"},
                {"Burnt Deer Meat", "Bränt hjortkött"},
                {"Cooked Deer Meat", "Kokt hjortkött"},
                {"Raw Deer Meat", "Rått hjortkött"},
                {"Burnt Horse Meat", "Bränt hästkött"},
                {"Cooked Horse Meat", "Kokt hästkött"},
                {"Raw Horse Meat", "Rått hästkött"},
                {"Burnt Human Meat", "Bränt mänskligt kött"},
                {"Cooked Human Meat", "Kokt humant kött"},
                {"Raw Human Meat", "Rått humant kött"},
                {"Spoiled Human Meat", "Bortskämd mänskligt kött"},
                {"Burnt Bear Meat", "Bränt björnkött"},
                {"Cooked Bear Meat", "Kokt björnkött"},
                {"Raw Bear Meat", "Rått björnkött"},
                {"Burnt Wolf Meat", "Burnt vargkött"},
                {"Cooked Wolf Meat", "Kokt vargkött"},
                {"Raw Wolf Meat", "Rått vargkött"},
                {"Spoiled Wolf Meat", "Ruttet vargkött"},
                {"Burnt Pork", "Bränt fläsk"},
                {"Cooked Pork", "Kokt fläsk"},
                {"Raw Pork", "Rått fläsk"},
                {"Mushroom", "Svamp"},
                {"Pickles", "Ättiksgurka"},
                {"Small Water Bottle", "Liten vattenflaska"},
                {"Water Jug", "Vattenkanna"},
                {"Shovel Bass", "Spade bas"},
                {"Cowbell", "Koskälla"},
                {"Junkyard Drum Kit", "Skräptrumset"},
                {"Pan Flute", "Panflöjt"},
                {"Acoustic Guitar", "Akustisk gitarr"},
                {"Jerry Can Guitar", "Bensindunk gitarr"},
                {"Wheelbarrow Piano", "Skottkärrpiano"},
                {"Canbourine", "Canburin"},
                {"Plumber's Trumpet", "Rörmokarens trumpet"},
                {"Sousaphone", "Sousafon"},
                {"Xylobone", "Xylobon"},
                {"Car Key", "Bilnycklar"},
                {"Door Key", "Dörr nyckel"},
                {"Key Lock", "Nyckelhål"},
                {"Code Lock", "Kodlås"},
                {"Blueprint", "Plan"},
                {"Chinese Lantern", "Kinesisk lykta"},
                {"Dragon Door Knocker", "Drak Dörr Knackare"},
                {"Dragon Mask", "Drak Mask"},
                {"New Year Gong", "Nyår Gong"},
                {"Rat Mask", "Råttmask"},
                {"Firecracker String", "Smällarsträng"},
                {"Chippy Arcade Game", "Chippy Arcade Game"},
                {"Door Closer", "Dörrstängare"},
                {"Bunny Ears", "Kaninöron"},
                {"Bunny Onesie", "Kanin-Onesie"},
                {"Easter Door Wreath", "Påskdörrkrans"},
                {"Egg Basket", "Äggkorg"},
                {"Rustigé Egg - Red", "Tyst ägg - Rött"},
                {"Rustigé Egg - Blue", "Tyst ägg - blått"},
                {"Rustigé Egg - Purple", "Tyst ägg - Lila"},
                {"Rustigé Egg - Ivory", "Tyst ägg - Elfenben"},
                {"Nest Hat", "Näst Hatt"},
                {"Bronze Egg", "Bronsägg"},
                {"Gold Egg", "Guldägg"},
                {"Painted Egg", "Målat ägg"},
                {"Silver Egg", "Silverägg"},
                {"Halloween Candy", "Halloween godis"},
                {"Large Candle Set", "Stort ljusuppsättning"},
                {"Small Candle Set", "Liten ljusuppsättning"},
                {"Coffin", "Kista"},
                {"Cursed Cauldron", "Förbannad kittel"},
                {"Gravestone", "Gravsten"},
                {"Wooden Cross", "Träkors"},
                {"Graveyard Fence", "Kyrkogårdstaket"},
                {"Large Loot Bag", "Stor bytesväska"},
                {"Medium Loot Bag", "Medium Loot Bag"},
                {"Small Loot Bag", "Liten bytesväska"},
                {"Pumpkin Bucket", "Pumpahink"},
                {"Scarecrow", "Fågelskrämma"},
                {"Skull Spikes", "Skalle Spikar"},
                {"Skull Door Knocker", "Skalle Dörr Knackare"},
                {"Skull Fire Pit", "Skalle Eldstad"},
                {"Spider Webs", "Spindelnät"},
                {"Spooky Speaker", "Läskig Högstalare"},
                {"Surgeon Scrubs", "Kirurg skrubbar"},
                {"Skull Trophy", "Skalle Medalj"},
                {"Card Movember Moustache", "Kort Movember Mustasch"},
                {"Movember Moustache", "Movember Mustasch"},
                {"Note", "Notera"},
                {"Human Skull", "Mänsklig skalle"},
                {"Above Ground Pool", "Ovanför marken pool"},
                {"Beach Chair", "Strand stol"},
                {"Beach Parasol", "Strand parasoll"},
                {"Beach Table", "Strandbord"},
                {"Beach Towel", "Handduk"},
                {"Boogie Board", "Boogie Board"},
                {"Inner Tube", "Innerrör"},
                {"Instant Camera", "Omedelbar kamera"},
                {"Paddling Pool", "Pool fyllning"},
                {"Photograph", "Fotografera"},
                {"Landscape Photo Frame", "Landskap fotoram"},
                {"Large Photo Frame", "Stor fotoram"},
                {"Portrait Photo Frame", "Stående fotoram"},
                {"Sunglasses", "Solglasögon"},
                {"Water Gun", "Vattenpistol"},
                {"Water Pistol", "Vattenpistol"},
                {"Purple Sunglasses", "Lila solglasögon"},
                {"Headset", "Headset"},
                {"Candy Cane Club", "Sockerrör klubba"},
                {"Christmas Lights", "Julbelysning"},
                {"Festive Doorway Garland", "Festlig dörröppningsgirland"},
                {"Candy Cane", "Polkagris"},
                {"Giant Candy Decor", "Jätte godis dekor"},
                {"Giant Lollipop Decor", "Jätte Lollipop Decor"},
                {"Pookie Bear", "Pookiebjörn"},
                {"Deluxe Christmas Lights", "Deluxe julbelysning"},
                {"Coal :(", "Kol :("},
                {"Large Present", "Stor present"},
                {"Medium Present", "Medium närvarande"},
                {"Small Present", "Liten present"},
                {"Snow Machine", "Snömaskin"},
                {"Snowball", "Snöboll"},
                {"Snowman", "Snögubbe"},
                {"SUPER Stocking", "SUPER Strumpa"},
                {"Small Stocking", "Liten strumpa"},
                {"Reindeer Antlers", "Renhorn"},
                {"Santa Beard", "Santa skägg"},
                {"Santa Hat", "Santa hatt"},
                {"Festive Window Garland", "Festlig Fönster Krans"},
                {"Wrapped Gift", "Inslagen gåva"},
                {"Wrapping Paper", "Omslagspapper"},
                {"Christmas Door Wreath", "Juldörrkrans"},
                {"Decorative Baubels", "Dekorativa kulor"},
                {"Decorative Plastic Candy Canes", "Dekorativa godisrotting av plast"},
                {"Decorative Gingerbread Men", "Dekorativa pepparkakor män"},
                {"Tree Lights", "Trädlampor"},
                {"Decorative Pinecones", "Dekorativa pinecones"},
                {"Star Tree Topper", "Stjärntopp"},
                {"Decorative Tinsel", "Dekorativ glitter"},
                {"Christmas Tree", "Julgran"},
                {"Auto Turret", "Auto torn"},
                {"Flame Turret", "Eld torn"},
                {"Glowing Eyes", "Lysande ögon"},
                {"SAM Ammo", "SAM Ammo"},
                {"SAM Site", "SAM-webbplats"},
                {"Black Berry", "Svartbär"},
                {"Black Berry Clone", "Svartbär klon"},
                {"Black Berry Seed", "Svartbär frö"},
                {"Blue Berry", "Blåbär"},
                {"Blue Berry Clone", "Blåbär klon"},
                {"Blue Berry Seed", "Blåbär frö"},
                {"Green Berry", "Grön bär"},
                {"Green Berry Clone", "Grönbär klon"},
                {"Green Berry Seed", "Grönbär frö"},
                {"Red Berry", "Rött bär"},
                {"Red Berry Clone", "Rödbär Clone"},
                {"Red Berry Seed", "Rödbär frö"},
                {"White Berry", "Vit bär"},
                {"White Berry Clone", "Vitbär klon"},
                {"White Berry Seed", "Vitbär frö"},
                {"Yellow Berry", "Gul bär"},
                {"Yellow Berry Clone", "Gulbär klon"},
                {"Yellow Berry Seed", "Gulbär frö"},
                {"Corn", "Majs"},
                {"Corn Clone", "Majsklon"},
                {"Corn Seed", "Majsfrö"},
                {"Hemp Clone", "Hampklon"},
                {"Hemp Seed", "Hampfrö"},
                {"Potato", "Potatis"},
                {"Potato Clone", "Potatisklon"},
                {"Potato Seed", "Potatisfrö"},
                {"Pumpkin", "Pumpa"},
                {"Pumpkin Plant Clone", "Pumpa växt klon"},
                {"Pumpkin Seed", "Pumpafrö"},
                {"Animal Fat", "Djurfett"},
                {"Battery - Small", "Batteri - litet"},
                {"Blood", "Blod"},
                {"Bone Fragments", "Benfragment"},
                {"CCTV Camera", "Övervakningskamera"},
                {"Charcoal", "Träkol"},
                {"Cloth", "Trasa"},
                {"Crude Oil", "Råolja"},
                {"Diesel Fuel", "Dieselbränsle"},
                {"Empty Can Of Beans", "Tom burk av bönor"},
                {"Empty Tuna Can", "Tom tonfiskburk"},
                {"Explosives", "Explosiva varor"},
                {"Fertilizer", "Gödselmedel"},
                {"Gun Powder", "Pistolpulver"},
                {"Horse Dung", "Hästgödsel"},
                {"High Quality Metal Ore", "Högkvalitativ metallmalm"},
                {"High Quality Metal", "Högkvalitativ metall"},
                {"Leather", "Läder"},
                {"Low Grade Fuel", "Bränsle av låg kvalitet"},
                {"Metal Fragments", "Metallfragment"},
                {"Metal Ore", "Metallmalm"},
                {"Paper", "Papper"},
                {"Plant Fiber", "Växtfiber"},
                {"Research Paper", "Uppsats"},
                {"Salt Water", "Saltvatten"},
                {"Scrap", "Skrot"},
                {"Stones", "Stenar"},
                {"Sulfur Ore", "Svavelmalm"},
                {"Sulfur", "Svavel"},
                {"Targeting Computer", "Riktad dator"},
                {"Water", "Vatten"},
                {"Wolf Skull", "Varg Skalle"},
                {"Wood", "Trä"},
                {"Advanced Healing Tea", "Avancerat läkningste"},
                {"Basic Healing Tea", "Grundläggande läkningste"},
                {"Pure Healing Tea", "Rent helande te"},
                {"Advanced Max Health Tea", "Avancerat max hälso te"},
                {"Basic Max Health Tea", "Grundläggande max hälso te"},
                {"Pure Max Health Tea", "Rent max hälso te"},
                {"Advanced Ore Tea", "Avancerat malmte"},
                {"Basic Ore Tea", "Basalt malmte"},
                {"Pure Ore Tea", "Ren malmte"},
                {"Advanced Rad. Removal Tea", "Avancerad rad. Borttagning te"},
                {"Rad. Removal Tea", "Rad. Borttagning te"},
                {"Pure Rad. Removal Tea", "Ren Rad. Borttagning te"},
                {"Adv. Anti-Rad Tea", "Adv. Anti-Rad te"},
                {"Anti-Rad Tea", "Anti-Rad te"},
                {"Pure Anti-Rad Tea", "Rent anti-rad te"},
                {"Advanced Scrap Tea", "Avancerat skrotte"},
                {"Basic Scrap Tea", "Grundläggande skrotte"},
                {"Pure Scrap Tea", "Rent skrotte"},
                {"Advanced Wood Tea", "Avancerat träte"},
                {"Basic Wood Tea", "Grundläggande träte"},
                {"Pure Wood Tea", "Rent träte"},
                {"Anti-Radiation Pills", "Anti-strålningspiller"},
                {"Binoculars", "Kikare"},
                {"Timed Explosive Charge", "Tidsinställd explosiv laddning"},
                {"Camera", "Kamera"},
                {"RF Transmitter", "RF-sändare"},
                {"Handmade Fishing Rod", "Handgjord fiskespö"},
                {"Flare", "Blossa"},
                {"Flashlight", "Ficklampa"},
                {"Geiger Counter", "Geiger mätare"},
                {"Hose Tool", "Slangverktyg"},
                {"Jackhammer", "Jackhammer"},
                {"Blue Keycard", "Blå nyckelkort"},
                {"Green Keycard", "Grönt nyckelkort"},
                {"Red Keycard", "Rött nyckelkort"},
                {"Large Medkit", "Stort Medkit"},
                {"Paper Map", "Papperskarta"},
                {"Medical Syringe", "Medicinsk spruta"},
                {"RF Pager", "RF-personsökare"},
                {"Building Plan", "Byggplan"},
                {"Smoke Grenade", "Rökgranat"},
                {"Supply Signal", "Matningssignal"},
                {"Survey Charge", "Undersökningsavgift"},
                {"Wire Tool", "Trådverktyg"},
                {"Small Chassis", "Litet chassi"},
                {"Medium Chassis", "Medium chassi"},
                {"Large Chassis", "Stort chassi"},
                {"Cockpit Vehicle Module", "Fordonsmodul för sittbrunn"},
                {"Armored Cockpit Vehicle Module", "Armerad fordonsmodul för sittbrunn"},
                {"Cockpit With Engine Vehicle Module", "Cockpit med motorfordonsmodul"},
                {"Engine Vehicle Module", "Motorfordonsmodul"},
                {"Flatbed Vehicle Module", "Flatbilsfordonsmodul"},
                {"Armored Passenger Vehicle Module", "Armored Passenger Vehicle Module"},
                {"Rear Seats Vehicle Module", "Fordonsmodul för baksäten"},
                {"Storage Vehicle Module", "Lagringsfordonsmodul"},
                {"Taxi Vehicle Module", "Taxi Fordonsmodul"},
                {"Large Flatbed Vehicle Module", "Stor flatbäddsmodul"},
                {"Fuel Tank Vehicle Module", "Fordonsmodul för bränsletank"},
                {"Passenger Vehicle Module", "Passagerarfordonsmodul"},
                {"Generic vehicle module", "Generisk fordonsmodul"},
                {"Telephone", "Telefon"},
                {"16x Zoom Scope", "16x zoomsikte"},
                {"Weapon flashlight", "Vapenficklampa"},
                {"Holosight", "Holosight"},
                {"Weapon Lasersight", "Vapen Lasersikte"},
                {"Muzzle Boost", "Nosparti Boost"},
                {"Muzzle Brake", "Nosbroms"},
                {"Simple Handmade Sight", "Enkel handgjord sikte"},
                {"Silencer", "Ljuddämpare"},
                {"8x Zoom Scope", "8x zoomsikte"},
                {"Assault Rifle", "Automatkarbin"},
                {"Bandage", "Bandage"},
                {"Beancan Grenade", "Bönburk granat"},
                {"Bolt Action Rifle", "Bult action gevär"},
                {"Bone Club", "Benklubben"},
                {"Bone Knife", "Benkniv"},
                {"Hunting Bow", "Jaktbåge"},
                {"Birthday Cake", "Födelsedagstårta"},
                {"Chainsaw", "Motorsåg"},
                {"Salvaged Cleaver", "Räddad klyfta"},
                {"Compound Bow", "Förenad båge"},
                {"Crossbow", "Armbåge"},
                {"Double Barrel Shotgun", "Hagelgevär med dubbla tunnor"},
                {"Eoka Pistol", "Eoka Pistol"},
                {"F1 Grenade", "F1 Granat"},
                {"Flame Thrower", "Eldkastare"},
                {"Multiple Grenade Launcher", "Multigranatkastare"},
                {"Butcher Knife", "Slaktarkniv"},
                {"Pitchfork", "Högaffel"},
                {"Sickle", "Skära"},
                {"Hammer", "Hammare"},
                {"Hatchet", "Yxa"},
                {"Combat Knife", "Stridkniv"},
                {"L96 Rifle", "L96 gevär"},
                {"LR-300 Assault Rifle", "LR-300 överfallsgevär"},
                {"M249", "M249"},
                {"M39 Rifle", "M39 gevär"},
                {"M92 Pistol", "M92-pistol"},
                {"Mace", "Spikklubba"},
                {"Machete", "Machete"},
                {"MP5A4", "MP5A4"},
                {"Nailgun", "Spikpistol"},
                {"Paddle", "Paddla"},
                {"Pickaxe", "Pickaxe"},
                {"Waterpipe Shotgun", "Vattenpip-hagelgevär"},
                {"Python Revolver", "Python Revolver"},
                {"Revolver", "Revolver"},
                {"Rock", "Sten"},
                {"Rocket Launcher", "Raketgevär"},
                {"Salvaged Axe", "Räddad yxa"},
                {"Salvaged Hammer", "Räddad hammare"},
                {"Salvaged Icepick", "Räddad Ishacka"},
                {"Satchel Charge", "Ryggsäck laddning"},
                {"Pump Shotgun", "Pump hagelgevär"},
                {"Semi-Automatic Pistol", "Halvautomatisk pistol"},
                {"Semi-Automatic Rifle", "Halvautomatiskt gevär"},
                {"Custom SMG", "Modifierad SMG"},
                {"Spas-12 Shotgun", "Spas-12 hagelgevär"},
                {"Stone Hatchet", "Sten yxa"},
                {"Stone Pickaxe", "Stenhax"},
                {"Stone Spear", "Sten spjut"},
                {"Longsword", "Långt svärd"},
                {"Salvaged Sword", "Räddat svärd"},
                {"Thompson", "Thompson"},
                {"Garry's Mod Tool Gun", "Garrys modverktygspistol"},
                {"Torch", "Fackla"},
                {"Water Bucket", "Vattenhink"},
                {"Wooden Spear", "Trä spjut"},
                {"Roadsign Horse Armor", "Vägskylt Hästrustning"},
                {"Wooden Horse Armor", "Trähäst rustning"},
                {"Horse Saddle", "Hästsadel"},
                {"Saddle bag", "Sadelväska"},
                {"High Quality Horse Shoes", "Högkvalitativa hästskor"},
                {"Basic Horse Shoes", "Grundläggande hästskor"},
                {"Generic vehicle chassis", "Generiskt fordonschassi"}
            }, this, "sv-SE"); //sweedish

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MessageShowNoEconomics", "GUIShop heeft geen reactie ontvangen van de Economics-plug-in. Zorg ervoor dat de Economics-plug-in correct is geïnstalleerd."},
                {"MessageShowNoServerRewards", "GUIShop heeft geen antwoord ontvangen van de ServerRewards-plug-in. Zorg ervoor dat de ServerRewards-plug-in correct is geïnstalleerd."},
                {"MessageBought", "U heeft met succes {0} x {1} gekocht."},
                {"MessageSold", "U heeft met succes {0} x {1} verkocht."},
                {"MessageErrorCooldown", "U kunt dit item slechts elke {0} seconden kopen."},
                {"MessageErrorSellCooldownAmount", "U kunt slechts {0} van dit item elke {1} seconden verkopen."},
                {"MessageErrorBuyCooldownAmount", "U kunt slechts {0} van dit item elke {1} seconden kopen."},
                {"MessageErrorBuyLimit", "U kunt slechts {0} van dit item kopen."},
                {"MessageErrorSellLimit", "U kunt slechts {0} van dit item verkopen."},
                {"MessageErrorInventoryFull", "Uw inventaris is vol."},
                {"MessageErrorInventorySlots", "U heeft minimaal {0} gratis voorraadvakken nodig."},
                {"MessageErrorNoShop", "Er is iets mis met deze winkel. Neem contact op met een admin."},
                {"MessageErrorGlobalDisabled", "Global Shops zijn uitgeschakeld. Deze server gebruikt NPC-leveranciers!"},
                {"MessageErrorNoActionShop", "Je mag niet {0} in deze winkel komen"},
                {"MessageErrorNoActionItem", "U mag dit item hier niet {0} gebruiken"},
                {"MessageErrorItemItem", "WAARSCHUWING: het lijkt erop dat dit verkoopartikel dat u heeft geen geldig item is! Neem contact op met een beheerder!"},
                {"MessageErrorItemNoValidbuy", "WAARSCHUWING: Het lijkt erop dat het geen geldig item is om te kopen. Neem contact op met een beheerder!"},
                {"MessageErrorItemNoValidsell", "WAARSCHUWING: Het lijkt erop dat het geen geldig item is om te verkopen. Neem contact op met een beheerder!"},
                {"MessageErrorRedeemKit", "WAARSCHUWING: Er is een fout opgetreden bij het overhandigen van deze kit. Neem contact op met een beheerder!"},
                {"MessageErrorBuyCmd", "Kan niet meerdere van dit item kopen!"},
                {"MessageErrorBuyPrice", "WAARSCHUWING: Er is geen koopprijs gegeven door de admin, u kunt dit item niet kopen"},
                {"MessageErrorSellPrice", "WAARSCHUWING: Er is geen verkoopprijs opgegeven door de beheerder, u kunt dit item niet verkopen"},
                {"MessageErrorNotEnoughMoney", "U heeft {0} munten nodig om {1} van {2} te kopen"},
                {"MessageErrorNotEnoughMoneyCustom", "U heeft {0} valuta nodig om {1} x {2} te kopen"},
                {"MessageErrorNotEnoughSell", "Je hebt niet genoeg van dit item."},
                {"MessageErrorNotNothing", "U kunt geen nul van dit artikel kopen."},
                {"MessageErrorItemNoExist", "WAARSCHUWING: Het artikel dat u probeert te kopen, lijkt niet te bestaan! Neem contact op met een beheerder!"},
                {"MessageErrorItemNoExistTake", "Het item dat u probeert te verkopen, is op dit moment niet verkoopbaar."},
                {"MessageErrorBuildingBlocked", "U kunt niet winkelen in een gebied dat geblokkeerd is door gebouwen."},
                {"MessageErrorAdmin", "U heeft niet de juiste machtigingen om deze opdracht te gebruiken. (guishop.admin)"},
                {"MessageErrorWaitingOnDownloads", "GUIShop wacht totdat het downloaden van ImageLibrary is voltooid"},
                {"BlockedMonuments", "Je mag de winkel niet gebruiken in de buurt van een monument!"},
                {"MessageErrorItemNotEnabled", "De winkelier heeft dit item uitgeschakeld."},
                {"MessageErrorItemNotFound", "Item is niet gevonden"},
                {"CantSellCommands", "Je kunt geen commando&#39;s terug verkopen aan de winkel."},
                {"CantSellKits", "U kunt kits niet terug verkopen aan de winkel."},
                {"MessageErrorCannotSellWhileEquiped", "Je kunt het item niet verkopen als je het hebt Equipt."},
                {"MessageShopResponse", "GUIShop wacht tot het downloaden van ImageLibrary is voltooid, even geduld."},
                {"MessageNPCResponseclose", "Bedankt voor het winkelen bij {0}, kom snel weer!"},
                {"MessageNPCResponseopen", "Welkom bij de {0} wat wilt u kopen? Druk op E om te beginnen met winkelen!"},
                {"Commands", "Commando's"},
                {"Attire", "Kleding"},
                {"Misc", "Diversen"},
                {"Items", "Artikelen"},
                {"Ammunition", "Munitie"},
                {"Construction", "Bouw"},
                {"Component", "Component"},
                {"Traps", "Vallen"},
                {"Electrical", "Elektrisch"},
                {"Fun", "Pret"},
                {"Food", "Voedsel"},
                {"Resources", "Middelen"},
                {"Tool", "Tool"},
                {"Weapon", "Wapen"},
                {"Medical", "Medisch"},
                {"Minicopter", "Minikopter"},
                {"Sedan", "Sinds"},
                {"Airdrop Call", "Airdrop-oproep"},
                {"Wolf Headdress", "Wolf hoofdtooi"},
                {"Fogger-3000", "Fogger-3000"},
                {"Strobe Light", "Stroboscoop"},
                {"Kayak", "Leuk vinden"},
                {"MC repair", "MC reparatie"},
                {"ScrapTransportHeliRepair", "ScrapTransportHeliRepair"},
                {"40mm Shotgun Round", "40 mm shotgun rond"},
                {"40mm HE Grenade", "40 mm HE-granaat"},
                {"40mm Smoke Grenade", "40 mm rookgranaat"},
                {"High Velocity Arrow", "Hoge snelheidspijl"},
                {"Wooden Arrow", "Houten pijl"},
                {"Bone Arrow", "Bot pijl"},
                {"Fire Arrow", "Vuurpijl"},
                {"Handmade Shell", "Handgemaakte schelp"},
                {"Nailgun Nails", "Nailgun Nails"},
                {"Pistol Bullet", "Pistoolkogel"},
                {"Incendiary Pistol Bullet", "Brandgevaarlijke pistoolkogel"},
                {"HV Pistol Ammo", "HV Pistoolmunitie"},
                {"5.56 Rifle Ammo", "5.56 Geweermunitie"},
                {"Explosive 5.56 Rifle Ammo", "Explosieve 5.56 geweermunitie"},
                {"Incendiary 5.56 Rifle Ammo", "Brandgevaarlijke 5.56 geweermunitie"},
                {"HV 5.56 Rifle Ammo", "HV 5.56 geweermunitie"},
                {"Rocket", "Raket"},
                {"Incendiary Rocket", "Brandgevaarlijke raket"},
                {"High Velocity Rocket", "Raket met hoge snelheid"},
                {"Smoke Rocket WIP!!!!", "Rook Raket WIP !!!!"},
                {"12 Gauge Buckshot", "12 gauge buckshot"},
                {"12 Gauge Incendiary Shell", "12 Gauge brandbommen"},
                {"12 Gauge Slug", "12 gauge naaktslak"},
                {"Sheet Metal Double Door", "Dubbele deur van plaatstaal"},
                {"Armored Double Door", "Gepantserde dubbele deur"},
                {"Wood Double Door", "Houten dubbele deur"},
                {"Sheet Metal Door", "Plaatstalen deur"},
                {"Armored Door", "Gepantserde deur"},
                {"Wooden Door", "Wooden Door"},
                {"Floor grill", "Vloergrill"},
                {"Ladder Hatch", "Ladder luik"},
                {"Floor triangle grill", "Vloer driehoek grill"},
                {"Triangle Ladder Hatch", "Driehoek Ladderluik"},
                {"High External Stone Gate", "Hoge externe stenen poort"},
                {"High External Wooden Gate", "Hoge externe houten poort"},
                {"Wooden Ladder", "Houten ladder"},
                {"High External Stone Wall", "Hoge externe stenen muur"},
                {"High External Wooden Wall", "Hoge externe houten muur"},
                {"Prison Cell Gate", "Gevangeniscel Gate"},
                {"Prison Cell Wall", "Gevangeniscelmuur"},
                {"Chainlink Fence Gate", "Chainlink Fence Gate"},
                {"Chainlink Fence", "Chainlink Fence"},
                {"Garage Door", "Garage Door"},
                {"Netting", "Verrekening"},
                {"Shop Front", "Winkel voorkant"},
                {"Metal Shop Front", "Metalen winkel voorkant"},
                {"Metal Window Bars", "Metalen raambalken"},
                {"Reinforced Window Bars", "Versterkte raambalken"},
                {"Wooden Window Bars", "Houten raambalken"},
                {"Metal horizontal embrasure", "Metalen horizontale schietgaten"},
                {"Metal Vertical embrasure", "Metalen verticale schietgaten"},
                {"Reinforced Glass Window", "Versterkt glasvenster"},
                {"Wood Shutters", "Houten luiken"},
                {"Watch Tower", "Wachttoren"},
                {"Diving Fins", "Duikvinnen"},
                {"Diving Mask", "Duikmasker"},
                {"Diving Tank", "Duiktank"},
                {"Wetsuit", "Wetsuit"},
                {"Frog Boots", "Frog Boots"},
                {"A Barrel Costume", "Een vat-kostuum"},
                {"Crate Costume", "Krat kostuum"},
                {"Burlap Gloves", "Jute handschoenen"},
                {"Leather Gloves", "Leren handschoenen"},
                {"Roadsign Gloves", "Roadsign Handschoenen"},
                {"Tactical Gloves", "Tactische handschoenen"},
                {"Ghost Costume", "Ghost kostuum"},
                {"Mummy Suit", "Mummie-pak"},
                {"Scarecrow Suit", "Vogelverschrikker pak"},
                {"Scarecrow Wrap", "Vogelverschrikker Wrap"},
                {"Hide Halterneck", "Verberg Halterneck"},
                {"Beenie Hat", "Beenie hoed"},
                {"Boonie Hat", "Boonie hoed"},
                {"Bucket Helmet", "Emmer helm"},
                {"Burlap Headwrap", "Jute Headwrap"},
                {"Candle Hat", "Kaars hoed"},
                {"Baseball Cap", "Baseball pet"},
                {"Clatter Helmet", "Klapperende helm"},
                {"Coffee Can Helmet", "Koffie kan helm"},
                {"Bone Helmet", "Bot helm"},
                {"Heavy Plate Helmet", "Helm met zware platen"},
                {"Miners Hat", "Mijnwerkers hoed"},
                {"Party Hat", "Feesthoed"},
                {"Riot Helmet", "Oproerhelm"},
                {"Wood Armor Helmet", "Houten pantserhelm"},
                {"Hoodie", "Capuchon"},
                {"Bone Armor", "Beenpantser"},
                {"Heavy Plate Jacket", "Zware plaatmantel"},
                {"Snow Jacket", "Sneeuw jas"},
                {"Jacket", "Jas"},
                {"Wood Chestplate", "Houten borstplaat"},
                {"Improvised Balaclava", "Geïmproviseerde bivakmuts"},
                {"Bandana Mask", "Bandana-masker"},
                {"Metal Facemask", "Metalen gezichtsmasker"},
                {"Night Vision Goggles", "Nachtkijker"},
                {"Burlap Trousers", "Jute broek"},
                {"Heavy Plate Pants", "Zware plaatbroek"},
                {"Hide Pants", "Verberg broek"},
                {"Road Sign Kilt", "Verkeersbord Kilt"},
                {"Shorts", "Korte broek"},
                {"Wood Armor Pants", "Houten pantserbroek"},
                {"Pants", "Broek"},
                {"Hide Poncho", "Verberg Poncho"},
                {"Burlap Shirt", "Jute shirt"},
                {"Shirt", "Shirt"},
                {"Hide Vest", "Verberg Vest"},
                {"Tank Top", "Tanktop"},
                {"Boots", "Laarzen"},
                {"Burlap Shoes", "Jute schoenen"},
                {"Hide Boots", "Verberg Boots"},
                {"Hide Skirt", "Verberg rok"},
                {"Bandit Guard Gear", "Bandit Guard-uitrusting"},
                {"Hazmat Suit", "Hazmat-pak"},
                {"Scientist Suit", "Wetenschapper pak"},
                {"Space Suit", "Ruimtepak"},
                {"Heavy Scientist Suit", "Zwaar Wetenschapperspak"},
                {"Longsleeve T-Shirt", "Lange mouwenshirt"},
                {"T-Shirt", "T-shirt"},
                {"Metal Chest Plate", "Metalen borstplaat"},
                {"Road Sign Jacket", "Verkeersbord jas"},
                {"Bleach", "Bleken"},
                {"Duct Tape", "Duct tape"},
                {"Low Quality Carburetor", "Carburateur van lage kwaliteit"},
                {"Medium Quality Carburetor", "Carburateur van gemiddelde kwaliteit"},
                {"High Quality Carburetor", "Carburateur van hoge kwaliteit"},
                {"Low Quality Crankshaft", "Lage kwaliteit krukas"},
                {"Medium Quality Crankshaft", "Krukas van gemiddelde kwaliteit"},
                {"High Quality Crankshaft", "Hoge kwaliteit krukas"},
                {"Low Quality Pistons", "Zuigers van lage kwaliteit"},
                {"Medium Quality Pistons", "Zuigers van gemiddelde kwaliteit"},
                {"High Quality Pistons", "Zuigers van hoge kwaliteit"},
                {"Low Quality Spark Plugs", "Bougies van lage kwaliteit"},
                {"Medium Quality Spark Plugs", "Bougies van gemiddelde kwaliteit"},
                {"High Quality Spark Plugs", "Hoge kwaliteit bougies"},
                {"Low Quality Valves", "Kleppen van lage kwaliteit"},
                {"Medium Quality Valves", "Kleppen van gemiddelde kwaliteit"},
                {"High Quality Valves", "Kleppen van hoge kwaliteit"},
                {"Electric Fuse", "Elektrische zekering"},
                {"Gears", "Versnellingen"},
                {"Glue", "Lijm"},
                {"Metal Blade", "Metalen mes"},
                {"Metal Pipe", "Metalen pijp"},
                {"Empty Propane Tank", "Lege propaantank"},
                {"Road Signs", "Verkeersborden"},
                {"Rope", "Touw"},
                {"Sewing Kit", "Naaidoosje"},
                {"Sheet Metal", "Plaat metaal"},
                {"Metal Spring", "Metalen veer"},
                {"Sticks", "Stokjes"},
                {"Tarp", "Tarp"},
                {"Tech Trash", "Tech prullenbak"},
                {"Rifle Body", "Geweer lichaam"},
                {"Semi Automatic Body", "Semi-automatische carrosserie"},
                {"SMG Body", "SMG-lichaam"},
                {"Concrete Barricade", "Betonnen Barricade"},
                {"Wooden Barricade Cover", "Houten Barricade Cover"},
                {"Metal Barricade", "Metalen Barricade"},
                {"Sandbag Barricade", "Zandzak Barricade"},
                {"Stone Barricade", "Stenen Barricade"},
                {"Wooden Barricade", "Houten Barricade"},
                {"Barbed Wooden Barricade", "Houten Barricade met weerhaken"},
                {"Barbeque", "Barbecue"},
                {"Snap Trap", "Snap Trap"},
                {"Bed", "Bed"},
                {"Camp Fire", "Kampvuur"},
                {"Ceiling Light", "Plafondlicht"},
                {"Chair", "Stoel"},
                {"Composter", "Composter"},
                {"Computer Station", "Computerstation"},
                {"Drop Box", "Drop Box"},
                {"Elevator", "Lift"},
                {"Stone Fireplace", "Stenen open haard"},
                {"Blue Boomer", "Blauwe Boomer"},
                {"Champagne Boomer", "Champagne Boomer"},
                {"Green Boomer", "Groene Boomer"},
                {"Orange Boomer", "Oranje Boomer"},
                {"Red Boomer", "Rode Boomer"},
                {"Violet Boomer", "Violet Boomer"},
                {"Blue Roman Candle", "Blauwe Romeinse kaars"},
                {"Green Roman Candle", "Groene Romeinse kaars"},
                {"Red Roman Candle", "Rode Romeinse kaars"},
                {"Violet Roman Candle", "Violette Romeinse kaars"},
                {"White Volcano Firework", "Wit vulkaanvuurwerk"},
                {"Red Volcano Firework", "Rood vulkaanvuurwerk"},
                {"Violet Volcano Firework", "Violet Vulkaanvuurwerk"},
                {"Wooden Floor Spikes", "Houten vloerpennen"},
                {"Fridge", "Koelkast"},
                {"Large Furnace", "Grote oven"},
                {"Furnace", "Oven"},
                {"Hitch & Trough", "Hitch"},
                {"Hab Repair", "Ik heb reparatie"},
                {"Jack O Lantern Angry", "Jack O Lantern boos"},
                {"Jack O Lantern Happy", "Jack O Lantern Happy"},
                {"Land Mine", "Landmijn"},
                {"Lantern", "Lantaarn"},
                {"Large Wood Box", "Grote houten doos"},
                {"Water Barrel", "Watervat"},
                {"Locker", "Kluisje"},
                {"Mail Box", "Brievenbus"},
                {"Mixing Table", "Mengtafel"},
                {"Modular Car Lift", "Modulaire autolift"},
                {"Pump Jack", "Pomp Jack"},
                {"Small Oil Refinery", "Kleine olieraffinaderij"},
                {"Large Planter Box", "Grote bloembak"},
                {"Small Planter Box", "Kleine bloembak"},
                {"Audio Alarm", "Geluidsalarm"},
                {"Smart Alarm", "Slimme wekker"},
                {"Smart Switch", "Slimme schakelaar"},
                {"Storage Monitor", "Opslagmonitor"},
                {"Large Rechargable Battery", "Grote oplaadbare batterij"},
                {"Medium Rechargable Battery", "Medium oplaadbare batterij"},
                {"Small Rechargable Battery", "Kleine oplaadbare batterij"},
                {"Button", "Knop"},
                {"Counter", "Teller"},
                {"HBHF Sensor", "HBHF-sensor"},
                {"Laser Detector", "Laserdetector"},
                {"Pressure Pad", "Drukkussen"},
                {"Door Controller", "Door Controller"},
                {"Electric Heater", "Elektrische verwarming"},
                {"Fluid Combiner", "Vloeistof combineren"},
                {"Fluid Splitter", "Vloeistofsplitser"},
                {"Fluid Switch & Pump", "Vloeistofschakelaar"},
                {"AND Switch", "EN Switch"},
                {"Blocker", "Blocker"},
                {"Electrical Branch", "Elektrische tak"},
                {"Root Combiner", "Root Combiner"},
                {"Memory Cell", "Geheugencel"},
                {"OR Switch", "OF Schakelaar"},
                {"RAND Switch", "RAND-schakelaar"},
                {"RF Broadcaster", "RF-omroep"},
                {"RF Receiver", "RF-ontvanger"},
                {"XOR Switch", "XOR-schakelaar"},
                {"Small Generator", "Kleine generator"},
                {"Test Generator", "Testgenerator"},
                {"Large Solar Panel", "Groot zonnepaneel"},
                {"Igniter", "Ontsteker"},
                {"Flasher Light", "Knipperlicht"},
                {"Simple Light", "Eenvoudig licht"},
                {"Siren Light", "Sirene licht"},
                {"Powered Water Purifier", "Aangedreven waterzuiveraar"},
                {"Switch", "Schakelaar"},
                {"Splitter", "Splitser"},
                {"Sprinkler", "Sprinkler"},
                {"Tesla Coil", "Tesla-spoel"},
                {"Timer", "Timer"},
                {"Cable Tunnel", "Kabeltunnel"},
                {"Water Pump", "Waterpomp"},
                {"Mining Quarry", "Mijnbouwgroeve"},
                {"Reactive Target", "Reactief doelwit"},
                {"Repair Bench", "Bank repareren"},
                {"Research Table", "Onderzoekstafel"},
                {"Rug Bear Skin", "Tapijt Bear Skin"},
                {"Rug", "Tapijt"},
                {"Search Light", "Zoeklicht"},
                {"Secret Lab Chair", "Secret Lab-stoel"},
                {"Salvaged Shelves", "Geborgen planken"},
                {"Large Banner Hanging", "Grote banner hangen"},
                {"Two Sided Hanging Sign", "Dubbelzijdig hangend teken"},
                {"Two Sided Ornate Hanging Sign", "Dubbelzijdig Sierlijke Opknoping Teken"},
                {"Landscape Picture Frame", "Landschap fotolijst"},
                {"Portrait Picture Frame", "Portret fotolijst"},
                {"Tall Picture Frame", "Hoge afbeeldingsframe"},
                {"XL Picture Frame", "XL fotolijst"},
                {"XXL Picture Frame", "XXL fotolijst"},
                {"Large Banner on pole", "Grote banner op paal"},
                {"Double Sign Post", "Dubbel bord"},
                {"Single Sign Post", "Single Sign Post"},
                {"One Sided Town Sign Post", "Eenzijdige stadspaal"},
                {"Two Sided Town Sign Post", "Tweezijdige stadspaal"},
                {"Huge Wooden Sign", "Enorm houten bord"},
                {"Large Wooden Sign", "Groot houten bord"},
                {"Medium Wooden Sign", "Middelgroot houten bord"},
                {"Small Wooden Sign", "Klein houten bord"},
                {"Shotgun Trap", "Shotgun Trap"},
                {"Sleeping Bag", "Slaapzak"},
                {"Small Stash", "Kleine voorraad"},
                {"Spinning wheel", "Spinnewiel"},
                {"Survival Fish Trap", "Survival Fish Trap"},
                {"Table", "Tafel"},
                {"Work Bench Level 1", "Werkbank niveau 1"},
                {"Work Bench Level 2", "Werkbank niveau 2"},
                {"Work Bench Level 3", "Werkbank niveau 3"},
                {"Tool Cupboard", "Gereedschapskast"},
                {"Tuna Can Lamp", "Tonijnblik Lamp"},
                {"Vending Machine", "Automaat"},
                {"Large Water Catcher", "Grote Watervanger"},
                {"Small Water Catcher", "Kleine Watervanger"},
                {"Water Purifier", "Waterzuiveraar"},
                {"Wind Turbine", "Windturbine"},
                {"Wood Storage Box", "Houten opbergdoos"},
                {"Apple", "appel"},
                {"Rotten Apple", "Rotte appel"},
                {"Black Raspberries", "Zwarte frambozen"},
                {"Blueberries", "Bosbessen"},
                {"Bota Bag", "Bota-tas"},
                {"Cactus Flesh", "Cactus vlees"},
                {"Can of Beans", "Blik bonen"},
                {"Can of Tuna", "Blikje tonijn"},
                {"Chocolate Bar", "Chocoladereep"},
                {"Cooked Fish", "Gekookte vis"},
                {"Raw Fish", "Rauwe vis"},
                {"Minnows", "Minnows"},
                {"Small Trout", "Kleine forel"},
                {"Granola Bar", "Mueslireep"},
                {"Burnt Chicken", "Verbrande kip"},
                {"Cooked Chicken", "Gekookte kip"},
                {"Raw Chicken Breast", "Rauwe kippenborst"},
                {"Spoiled Chicken", "Verwende Kip"},
                {"Burnt Deer Meat", "Verbrand hertenvlees"},
                {"Cooked Deer Meat", "Gekookt hertenvlees"},
                {"Raw Deer Meat", "Rauw hertenvlees"},
                {"Burnt Horse Meat", "Verbrand paardenvlees"},
                {"Cooked Horse Meat", "Gekookt paardenvlees"},
                {"Raw Horse Meat", "Rauw paardenvlees"},
                {"Burnt Human Meat", "Verbrand menselijk vlees"},
                {"Cooked Human Meat", "Gekookt mensenvlees"},
                {"Raw Human Meat", "Rauw mensenvlees"},
                {"Spoiled Human Meat", "Verwend mensenvlees"},
                {"Burnt Bear Meat", "Verbrand berenvlees"},
                {"Cooked Bear Meat", "Gekookt berenvlees"},
                {"Raw Bear Meat", "Rauw berenvlees"},
                {"Burnt Wolf Meat", "Verbrand Wolfsvlees"},
                {"Cooked Wolf Meat", "Gekookt Wolfsvlees"},
                {"Raw Wolf Meat", "Rauw Wolf vlees"},
                {"Spoiled Wolf Meat", "Verwend Wolfsvlees"},
                {"Burnt Pork", "Verbrand varkensvlees"},
                {"Cooked Pork", "Gekookt Varkensvlees"},
                {"Raw Pork", "Rauw varkensvlees"},
                {"Mushroom", "Paddestoel"},
                {"Pickles", "Augurken"},
                {"Small Water Bottle", "Kleine waterfles"},
                {"Water Jug", "Water kan"},
                {"Shovel Bass", "Schop bas"},
                {"Cowbell", "Koebel"},
                {"Junkyard Drum Kit", "Junkyard Drumstel"},
                {"Pan Flute", "Panfluit"},
                {"Acoustic Guitar", "Akoestische gitaar"},
                {"Jerry Can Guitar", "Jerry Can gitaar"},
                {"Wheelbarrow Piano", "Kruiwagen Piano"},
                {"Canbourine", "Canbourine"},
                {"Plumber's Trumpet", "Trompet van de loodgieter"},
                {"Sousaphone", "Sousafoon"},
                {"Xylobone", "Xylobone"},
                {"Car Key", "Auto sleutel"},
                {"Door Key", "Door Key"},
                {"Key Lock", "Slot"},
                {"Code Lock", "Codeslot"},
                {"Blueprint", "Blauwdruk"},
                {"Chinese Lantern", "Chinese lantaarn"},
                {"Dragon Door Knocker", "Dragon Door Knocker"},
                {"Dragon Mask", "Draakmasker"},
                {"New Year Gong", "Nieuwjaarsgong"},
                {"Rat Mask", "Rattenmasker"},
                {"Firecracker String", "Voetzoeker String"},
                {"Chippy Arcade Game", "Chippy Arcade-spel"},
                {"Door Closer", "Deurdranger"},
                {"Bunny Ears", "Konijnenoren"},
                {"Bunny Onesie", "Konijn Onesie"},
                {"Easter Door Wreath", "Pasen deur krans"},
                {"Egg Basket", "Eiermand"},
                {"Rustigé Egg - Red", "Rustig  Egg - Red"},
                {"Rustigé Egg - Blue", "Rustig  Egg - Blue"},
                {"Rustigé Egg - Purple", "Rustig  Egg - Purple"},
                {"Rustigé Egg - Ivory", "Rustig  Egg - Ivory"},
                {"Nest Hat", "Nest-hoed"},
                {"Bronze Egg", "Bronzen Ei"},
                {"Gold Egg", "Gouden Ei"},
                {"Painted Egg", "Beschilderd Ei"},
                {"Silver Egg", "Zilveren Ei"},
                {"Halloween Candy", "Halloween snoep"},
                {"Large Candle Set", "Grote kaarsenset"},
                {"Small Candle Set", "Kleine kaarsenset"},
                {"Coffin", "Kist"},
                {"Cursed Cauldron", "Vervloekte ketel"},
                {"Gravestone", "Grafsteen"},
                {"Wooden Cross", "Houten kruis"},
                {"Graveyard Fence", "Kerkhofomheining"},
                {"Large Loot Bag", "Grote buitzak"},
                {"Medium Loot Bag", "Middelgrote buitzak"},
                {"Small Loot Bag", "Kleine buitzak"},
                {"Pumpkin Bucket", "Pompoen Emmer"},
                {"Scarecrow", "Vogelverschrikker"},
                {"Skull Spikes", "Schedel spikes"},
                {"Skull Door Knocker", "Skull Door Knocker"},
                {"Skull Fire Pit", "Schedel vuurplaats"},
                {"Spider Webs", "Spinnenweb"},
                {"Spooky Speaker", "Spookachtige spreker"},
                {"Surgeon Scrubs", "Chirurg Scrubs"},
                {"Skull Trophy", "Schedel Trophy"},
                {"Card Movember Moustache", "Kaart Movember Moustache"},
                {"Movember Moustache", "Movember Snor"},
                {"Note", "Notitie"},
                {"Human Skull", "Menselijke schedel"},
                {"Above Ground Pool", "Bovengronds zwembad"},
                {"Beach Chair", "Strandstoel"},
                {"Beach Parasol", "Strandparasol"},
                {"Beach Table", "Strandtafel"},
                {"Beach Towel", "Strandlaken"},
                {"Boogie Board", "Boogie Board"},
                {"Inner Tube", "Binnenste buis"},
                {"Instant Camera", "Instant camera"},
                {"Paddling Pool", "Kinderzwembad"},
                {"Photograph", "Fotograaf"},
                {"Landscape Photo Frame", "Landschap fotolijst"},
                {"Large Photo Frame", "Grote fotolijst"},
                {"Portrait Photo Frame", "Portret fotolijst"},
                {"Sunglasses", "Zonnebril"},
                {"Water Gun", "Waterpistool"},
                {"Water Pistol", "Waterpistool"},
                {"Purple Sunglasses", "Paarse zonnebril"},
                {"Headset", "Koptelefoon"},
                {"Candy Cane Club", "Candy Cane Club"},
                {"Christmas Lights", "Kerstlichten"},
                {"Festive Doorway Garland", "Feestelijke Doorway Garland"},
                {"Candy Cane", "Zuurstok"},
                {"Giant Candy Decor", "Gigantische Candy Decor"},
                {"Giant Lollipop Decor", "Gigantische Lollipop Decor"},
                {"Pookie Bear", "Pookie Beer"},
                {"Deluxe Christmas Lights", "Deluxe kerstverlichting"},
                {"Coal :(", "Steenkool :("},
                {"Large Present", "Groot cadeau"},
                {"Medium Present", "Medium aanwezig"},
                {"Small Present", "Klein cadeau"},
                {"Snow Machine", "Sneeuwmachine"},
                {"Snowball", "Sneeuwbal"},
                {"Snowman", "Sneeuwman"},
                {"SUPER Stocking", "SUPER Kous"},
                {"Small Stocking", "Kleine kous"},
                {"Reindeer Antlers", "Rendiergeweien"},
                {"Santa Beard", "Kerstman baard"},
                {"Santa Hat", "Kerstmuts"},
                {"Festive Window Garland", "Feestelijke Window Garland"},
                {"Wrapped Gift", "Verpakt cadeau"},
                {"Wrapping Paper", "Inpakpapier"},
                {"Christmas Door Wreath", "Kerst deur krans"},
                {"Decorative Baubels", "Decoratieve kerstballen"},
                {"Decorative Plastic Candy Canes", "Decoratieve plastic zuurstokken"},
                {"Decorative Gingerbread Men", "Decoratieve peperkoekmannen"},
                {"Tree Lights", "Boom lichten"},
                {"Decorative Pinecones", "Decoratieve dennenappels"},
                {"Star Tree Topper", "Star Tree Topper"},
                {"Decorative Tinsel", "Decoratief klatergoud"},
                {"Christmas Tree", "Kerstboom"},
                {"Auto Turret", "Auto Turret"},
                {"Flame Turret", "Flame Torentje"},
                {"Glowing Eyes", "Gloeiende ogen"},
                {"SAM Ammo", "SAM munitie"},
                {"SAM Site", "SAM-site"},
                {"Black Berry", "Zwarte bes"},
                {"Black Berry Clone", "Black Berry Clone"},
                {"Black Berry Seed", "Zwart bessenzaad"},
                {"Blue Berry", "Bosbes"},
                {"Blue Berry Clone", "Blue Berry Clone"},
                {"Blue Berry Seed", "Blue Berry Seed"},
                {"Green Berry", "Groene bes"},
                {"Green Berry Clone", "Groene bessenkloon"},
                {"Green Berry Seed", "Groen bessenzaad"},
                {"Red Berry", "Rode bes"},
                {"Red Berry Clone", "Rode bes kloon"},
                {"Red Berry Seed", "Rode bessenzaad"},
                {"White Berry", "Witte bes"},
                {"White Berry Clone", "Witte bessenkloon"},
                {"White Berry Seed", "Witte bessenzaad"},
                {"Yellow Berry", "Gele bes"},
                {"Yellow Berry Clone", "Gele bessenkloon"},
                {"Yellow Berry Seed", "Geel bessenzaad"},
                {"Corn", "Maïs"},
                {"Corn Clone", "Corn Clone"},
                {"Corn Seed", "Maïszaad"},
                {"Hemp Clone", "Hennep kloon"},
                {"Hemp Seed", "Hennepzaad"},
                {"Potato", "Aardappel"},
                {"Potato Clone", "Aardappelkloon"},
                {"Potato Seed", "Aardappelzaad"},
                {"Pumpkin", "Pompoen"},
                {"Pumpkin Plant Clone", "Pompoen Plant Clone"},
                {"Pumpkin Seed", "Pompoenzaad"},
                {"Animal Fat", "Dierlijk vet"},
                {"Battery - Small", "Batterij - klein"},
                {"Blood", "Bloed"},
                {"Bone Fragments", "Botfragmenten"},
                {"CCTV Camera", "Beveiligingscamera"},
                {"Charcoal", "Houtskool"},
                {"Cloth", "Kleding"},
                {"Crude Oil", "Ruwe olie"},
                {"Diesel Fuel", "Diesel brandstof"},
                {"Empty Can Of Beans", "Leeg Blikje Bonen"},
                {"Empty Tuna Can", "Leeg tonijnblik"},
                {"Explosives", "Explosieven"},
                {"Fertilizer", "Kunstmest"},
                {"Gun Powder", "Pistoolpoeder"},
                {"Horse Dung", "Paardenmest"},
                {"High Quality Metal Ore", "Metaalerts van hoge kwaliteit"},
                {"High Quality Metal", "Metaal van hoge kwaliteit"},
                {"Leather", "Leer"},
                {"Low Grade Fuel", "Laagwaardige brandstof"},
                {"Metal Fragments", "Metalen fragmenten"},
                {"Metal Ore", "Metal Ore"},
                {"Paper", "Papier"},
                {"Plant Fiber", "Plantaardige vezels"},
                {"Research Paper", "Onderzoeksdocument"},
                {"Salt Water", "Zout water"},
                {"Scrap", "Schroot"},
                {"Stones", "Stenen"},
                {"Sulfur Ore", "Zwavel erts"},
                {"Sulfur", "Zwavel"},
                {"Targeting Computer", "Gericht op computer"},
                {"Water", "Water"},
                {"Wolf Skull", "Wolf schedel"},
                {"Wood", "Hout"},
                {"Advanced Healing Tea", "Geavanceerde helende thee"},
                {"Basic Healing Tea", "Basis genezende thee"},
                {"Pure Healing Tea", "Pure Healing Tea"},
                {"Advanced Max Health Tea", "Geavanceerde Max Health Tea"},
                {"Basic Max Health Tea", "Basic Max Health Tea"},
                {"Pure Max Health Tea", "Pure Max Health Tea"},
                {"Advanced Ore Tea", "Geavanceerde Erts-thee"},
                {"Basic Ore Tea", "Basis Erts Thee"},
                {"Pure Ore Tea", "Pure Erts-thee"},
                {"Advanced Rad. Removal Tea", "Geavanceerde Rad. Verwijdering thee"},
                {"Rad. Removal Tea", "Rad. Verwijdering thee"},
                {"Pure Rad. Removal Tea", "Pure Rad. Verwijdering thee"},
                {"Adv. Anti-Rad Tea", "Adv. Anti-Rad-thee"},
                {"Anti-Rad Tea", "Anti-Rad-thee"},
                {"Pure Anti-Rad Tea", "Pure anti-rad-thee"},
                {"Advanced Scrap Tea", "Geavanceerde schrootthee"},
                {"Basic Scrap Tea", "Basic Scrap Tea"},
                {"Pure Scrap Tea", "Pure schroot thee"},
                {"Advanced Wood Tea", "Geavanceerde Wood Tea"},
                {"Basic Wood Tea", "Basic Wood Tea"},
                {"Pure Wood Tea", "Pure Wood Tea"},
                {"Anti-Radiation Pills", "Anti-stralingspillen"},
                {"Binoculars", "Verrekijker"},
                {"Timed Explosive Charge", "Getimede explosieve lading"},
                {"Camera", "Camera"},
                {"RF Transmitter", "RF-zender"},
                {"Handmade Fishing Rod", "Handgemaakte hengel"},
                {"Flare", "Gloed"},
                {"Flashlight", "Zaklamp"},
                {"Geiger Counter", "Geigerteller"},
                {"Hose Tool", "Slang Gereedschap"},
                {"Jackhammer", "Jackhammer"},
                {"Blue Keycard", "Blauwe keycard"},
                {"Green Keycard", "Groene keycard"},
                {"Red Keycard", "Rode keycard"},
                {"Large Medkit", "Grote Medkit"},
                {"Paper Map", "Papieren kaart"},
                {"Medical Syringe", "Medische spuit"},
                {"RF Pager", "RF-pager"},
                {"Building Plan", "Bouwplan"},
                {"Smoke Grenade", "Rookgranaat"},
                {"Supply Signal", "Voedingssignaal"},
                {"Survey Charge", "Onderzoekskosten"},
                {"Wire Tool", "Draadgereedschap"},
                {"Small Chassis", "Klein chassis"},
                {"Medium Chassis", "Middelgroot chassis"},
                {"Large Chassis", "Groot chassis"},
                {"Cockpit Vehicle Module", "Cockpit-voertuigmodule"},
                {"Armored Cockpit Vehicle Module", "Module voor gepantserde cockpitvoertuigen"},
                {"Cockpit With Engine Vehicle Module", "Cockpit met motorvoertuigmodule"},
                {"Engine Vehicle Module", "Motorvoertuigmodule"},
                {"Flatbed Vehicle Module", "Voertuigmodule met platte bak"},
                {"Armored Passenger Vehicle Module", "Gepantserde passagiersvoertuigmodule"},
                {"Rear Seats Vehicle Module", "Voertuigmodule achterbank"},
                {"Storage Vehicle Module", "Opslagvoertuigmodule"},
                {"Taxi Vehicle Module", "Taxi Voertuig Module"},
                {"Large Flatbed Vehicle Module", "Grote platte voertuigmodule"},
                {"Fuel Tank Vehicle Module", "Brandstoftank voertuigmodule"},
                {"Passenger Vehicle Module", "Passagiersvoertuigmodule"},
                {"Generic vehicle module", "Generieke voertuigmodule"},
                {"Telephone", "Telefoon"},
                {"16x Zoom Scope", "16x zoombereik"},
                {"Weapon flashlight", "Wapen zaklamp"},
                {"Holosight", "Holosight"},
                {"Weapon Lasersight", "Wapen Lasersight"},
                {"Muzzle Boost", "Snuitboost"},
                {"Muzzle Brake", "Mondingsrem"},
                {"Simple Handmade Sight", "Eenvoudig handgemaakt zicht"},
                {"Silencer", "Geluiddemper"},
                {"8x Zoom Scope", "8x zoombereik"},
                {"Assault Rifle", "Aanvalsgeweer"},
                {"Bandage", "Verband"},
                {"Beancan Grenade", "Bonen Grenada"},
                {"Bolt Action Rifle", "Grendelgeweer"},
                {"Bone Club", "Bot Club"},
                {"Bone Knife", "Bot mes"},
                {"Hunting Bow", "Jaag boog"},
                {"Birthday Cake", "Verjaardagstaart"},
                {"Chainsaw", "Kettingzaag"},
                {"Salvaged Cleaver", "Geborgen Cleaver"},
                {"Compound Bow", "Samengestelde boog"},
                {"Crossbow", "Kruisboog"},
                {"Double Barrel Shotgun", "Dubbel vat jachtgeweer"},
                {"Eoka Pistol", "Eoka-pistool"},
                {"F1 Grenade", "F1 Granada"},
                {"Flame Thrower", "Vlammenwerper"},
                {"Multiple Grenade Launcher", "Meerdere granaatwerper"},
                {"Butcher Knife", "Slagersmes"},
                {"Pitchfork", "Hooivork"},
                {"Sickle", "Sikkel"},
                {"Hammer", "Hamer"},
                {"Hatchet", "Bijl"},
                {"Combat Knife", "Gevechtsmes"},
                {"L96 Rifle", "L96 geweer"},
                {"LR-300 Assault Rifle", "LR-300 aanvalsgeweer"},
                {"M249", "M249"},
                {"M39 Rifle", "M39 geweer"},
                {"M92 Pistol", "M92 Pistool"},
                {"Mace", "kat"},
                {"Machete", "Machete"},
                {"MP5A4", "MP5A4"},
                {"Nailgun", "Nagel pistool"},
                {"Paddle", "Peddelen"},
                {"Pickaxe", "Pikhouweel"},
                {"Waterpipe Shotgun", "Waterpijp Shotgun"},
                {"Python Revolver", "Python Revolver"},
                {"Revolver", "Revolver"},
                {"Rock", "Rots"},
                {"Rocket Launcher", "Raketwerper"},
                {"Salvaged Axe", "Geborgen bijl"},
                {"Salvaged Hammer", "Geborgen hamer"},
                {"Salvaged Icepick", "Geborgen Icepick"},
                {"Satchel Charge", "Satchel Charge"},
                {"Pump Shotgun", "Pomp Shotgun"},
                {"Semi-Automatic Pistol", "Semi-automatisch pistool"},
                {"Semi-Automatic Rifle", "Semi-automatisch geweer"},
                {"Custom SMG", "Aangepaste SMG"},
                {"Spas-12 Shotgun", "Spas-12 Shotgun"},
                {"Stone Hatchet", "Stenen bijl"},
                {"Stone Pickaxe", "Stenen houweel"},
                {"Stone Spear", "Stenen speer"},
                {"Longsword", "Langzwaard"},
                {"Salvaged Sword", "Geborgen zwaard"},
                {"Thompson", "Thompson"},
                {"Garry's Mod Tool Gun", "Garry's Mod Tool Gun"},
                {"Torch", "Fakkel"},
                {"Water Bucket", "Wateremmer"},
                {"Wooden Spear", "Houten speer"},
                {"Roadsign Horse Armor", "Bord Horse Armor"},
                {"Wooden Horse Armor", "Houten paardenpantser"},
                {"Horse Saddle", "Paardenzadel"},
                {"Saddle bag", "Zadeltas"},
                {"High Quality Horse Shoes", "Hoge kwaliteit paardenschoenen"},
                {"Basic Horse Shoes", "Basic paardenschoenen"},
                {"Generic vehicle chassis", "Algemeen voertuigchassis"}
            }, this, "nl"); //dutch

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MessageShowNoEconomics", "GUIShop이 Economics 플러그인에서 응답을받지 못했습니다. Economics 플러그인이 올바르게 설치되었는지 확인하십시오."},
                {"MessageShowNoServerRewards", "GUIShop이 ServerRewards 플러그인에서 응답을받지 못했습니다. ServerRewards 플러그인이 올바르게 설치되었는지 확인하십시오."},
                {"MessageBought", "{0} x {1}을 (를) 성공적으로 구입했습니다."},
                {"MessageSold", "{0} x {1}을 (를) 성공적으로 판매했습니다."},
                {"MessageErrorCooldown", "이 아이템은 {0} 초마다 구매할 수 있습니다."},
                {"MessageErrorSellCooldownAmount", "{1} 초마다이 아이템의 {0} 개만 판매 할 수 있습니다."},
                {"MessageErrorBuyCooldownAmount", "{1} 초마다이 항목 중 {0} 개만 구매할 수 있습니다."},
                {"MessageErrorBuyLimit", "이 항목 중 {0} 개만 구매할 수 있습니다."},
                {"MessageErrorSellLimit", "이 항목의 {0} 만 판매 할 수 있습니다."},
                {"MessageErrorInventoryFull", "재고가 가득 찼습니다."},
                {"MessageErrorInventorySlots", "최소 {0} 개의 인벤토리 자리가 필요합니다."},
                {"MessageErrorNoShop", "이 가게에 문제가 있습니다. 관리자에게 문의하십시오."},
                {"MessageErrorGlobalDisabled", "글로벌 상점이 비활성화되었습니다. 이 서버는 NPC 상인을 사용합니다!"},
                {"MessageErrorNoActionShop", "이 상점에서 {0} 할 수 없습니다."},
                {"MessageErrorNoActionItem", "여기에서이 항목을 {0} 할 수 없습니다."},
                {"MessageErrorItemItem", "경고: 보유한 판매 항목이 유효한 항목이 아닌 것 같습니다! 관리자에게 문의하십시오!"},
                {"MessageErrorItemNoValidbuy", "경고 : 구매할 수있는 유효한 항목이 아닌 것 같습니다. 관리자에게 문의하십시오!"},
                {"MessageErrorItemNoValidsell", "경고 : 판매 할 수있는 유효한 항목이 아닌 것 같습니다. 관리자에게 문의하십시오!"},
                {"MessageErrorRedeemKit", "경고: 이 키트를 제공하는 동안 오류가 발생했습니다. 관리자에게 문의하십시오!"},
                {"MessageErrorBuyCmd", "이 항목을 여러 개 구매할 수 없습니다!"},
                {"MessageErrorBuyPrice", "경고: 관리자가 제공 한 구매 가격이 없으므로이 항목을 구매할 수 없습니다."},
                {"MessageErrorSellPrice", "경고: 관리자가 제공 한 판매 가격이 없으므로이 항목을 판매 할 수 없습니다."},
                {"MessageErrorNotEnoughMoney", "{2} 개 중 {1} 개를 구매하려면 {0} 코인이 필요합니다."},
                {"MessageErrorNotEnoughMoneyCustom", "{1} x {2}을 (를) 구매하려면 {0} 통화가 필요합니다."},
                {"MessageErrorNotEnoughSell", "이 항목이 충분하지 않습니다."},
                {"MessageErrorNotNothing", "이 항목의 0을 구매할 수 없습니다."},
                {"MessageErrorItemNoExist", "WARNING : 구매하려는 항목이 존재하지 않는 것 같습니다! 관리자에게 문의하십시오!"},
                {"MessageErrorItemNoExistTake", "판매하려는 아이템은 현재 판매 할 수 없습니다."},
                {"MessageErrorBuildingBlocked", "건물이 차단 된 구역에서는 쇼핑을 할 수 없습니다."},
                {"MessageErrorAdmin", "이 명령을 사용할 수있는 올바른 권한이 없습니다. (guishop.admin)"},
                {"MessageErrorWaitingOnDownloads", "GUIShop은 ImageLibrary 다운로드가 완료되기를 기다리고 있습니다."},
                {"BlockedMonuments", "기념비 근처에서는 상점을 사용할 수 없습니다!"},
                {"MessageErrorItemNotEnabled", "상점 주인이 항목을 비활성화했습니다."},
                {"MessageErrorItemNotFound", "항목을 찾을 수 없습니다."},
                {"CantSellCommands", "명령어는 상점에 다시 판매 할 수 없습니다."},
                {"CantSellKits", "킷은 상점에 반납 할 수 없습니다."},
                {"MessageErrorCannotSellWhileEquiped", "장비가 있으면 아이템을 판매 할 수 없습니다."},
                {"MessageShopResponse", "GUIShop은 ImageLibrary 다운로드가 완료되기를 기다리고 있습니다. 잠시 기다려주세요."},
                {"MessageNPCResponseclose", "{0}에서 쇼핑 해 주셔서 감사합니다. 곧 다시 오세요!"},
                {"MessageNPCResponseopen", "무엇을 구매 하시겠습니까? {0}에 오신 것을 환영합니다. 쇼핑을 시작하려면 E를 누르세요!"},
                {"Commands", "명령"},
                {"Attire", "복장"},
                {"Misc", "기타"},
                {"Items", "아이템"},
                {"Ammunition", "탄약"},
                {"Construction", "구성"},
                {"Component", "구성 요소"},
                {"Traps", "트랩"},
                {"Electrical", "전기 같은"},
                {"Fun", "장난"},
                {"Food", "음식"},
                {"Resources", "자원"},
                {"Tool", "수단"},
                {"Weapon", "무기"},
                {"Medical", "의료"},
                {"Minicopter", "미니 콥터"},
                {"Sedan", "이후"},
                {"Airdrop Call", "에어 드랍 콜"},
                {"Wolf Headdress", "늑대 머리 장식"},
                {"Fogger-3000", "Fogger-3000"},
                {"Strobe Light", "스트로브 라이트"},
                {"Kayak", "카약"},
                {"MC repair", "MC 수리"},
                {"ScrapTransportHeliRepair", "스크랩 수송 헬리 수리"},
                {"40mm Shotgun Round", "40mm 샷건 라운드"},
                {"40mm HE Grenade", "40mm HE 수류탄"},
                {"40mm Smoke Grenade", "40mm 연막탄"},
                {"High Velocity Arrow", "고속 화살표"},
                {"Wooden Arrow", "나무 화살"},
                {"Bone Arrow", "뼈 화살"},
                {"Fire Arrow", "파이어 애로우"},
                {"Handmade Shell", "수제 쉘"},
                {"Nailgun Nails", "네일 건 네일"},
                {"Pistol Bullet", "권총 총알"},
                {"Incendiary Pistol Bullet", "소이 권총 총알"},
                {"HV Pistol Ammo", "HV 권총 탄약"},
                {"5.56 Rifle Ammo", "5.56 소총 탄약"},
                {"Explosive 5.56 Rifle Ammo", "폭발성 5.56 소총 탄약"},
                {"Incendiary 5.56 Rifle Ammo", "소이 5.56 소총 탄약"},
                {"HV 5.56 Rifle Ammo", "HV 5.56 소총 탄약"},
                {"Rocket", "로켓"},
                {"Incendiary Rocket", "소이 로켓"},
                {"High Velocity Rocket", "고속 로켓"},
                {"Smoke Rocket WIP!!!!", "연기 로켓 WIP !!!!"},
                {"12 Gauge Buckshot", "12 게이지 벅샷"},
                {"12 Gauge Incendiary Shell", "12 게이지 소이 껍질"},
                {"12 Gauge Slug", "12 게이지 슬러그"},
                {"Sheet Metal Double Door", "판금 이중문"},
                {"Armored Double Door", "기갑 이중문"},
                {"Wood Double Door", "목재 이중문"},
                {"Sheet Metal Door", "판금 도어"},
                {"Armored Door", "기갑 문"},
                {"Wooden Door", "나무 문"},
                {"Floor grill", "바닥 그릴"},
                {"Ladder Hatch", "사다리 해치"},
                {"Floor triangle grill", "바닥 삼각형 그릴"},
                {"Triangle Ladder Hatch", "삼각형 사다리 해치"},
                {"High External Stone Gate", "높은 외부 돌 문"},
                {"High External Wooden Gate", "높은 외부 목조 게이트"},
                {"Wooden Ladder", "나무 사다리"},
                {"High External Stone Wall", "높은 외부 돌담"},
                {"High External Wooden Wall", "높은 외부 나무 벽"},
                {"Prison Cell Gate", "감옥 문"},
                {"Prison Cell Wall", "감옥 벽"},
                {"Chainlink Fence Gate", "철창 울타리 게이트"},
                {"Chainlink Fence", "철창 울타리"},
                {"Garage Door", "주차장 문"},
                {"Netting", "그물"},
                {"Shop Front", "가게 앞"},
                {"Metal Shop Front", "메탈 샵 프론트"},
                {"Metal Window Bars", "금속 창 바"},
                {"Reinforced Window Bars", "강화 된 창 바"},
                {"Wooden Window Bars", "나무 창 바"},
                {"Metal horizontal embrasure", "금속 수평 embrasure"},
                {"Metal Vertical embrasure", "금속 수직 embrasure"},
                {"Reinforced Glass Window", "강화 유리창"},
                {"Wood Shutters", "목재 셔터"},
                {"Watch Tower", "워치 타워"},
                {"Diving Fins", "다이빙 핀"},
                {"Diving Mask", "다이빙 마스크"},
                {"Diving Tank", "다이빙 탱크"},
                {"Wetsuit", "잠수복"},
                {"Frog Boots", "개구리 부츠"},
                {"A Barrel Costume", "배럴 의상"},
                {"Crate Costume", "상자 의상"},
                {"Burlap Gloves", "삼베 장갑"},
                {"Leather Gloves", "가죽 장갑"},
                {"Roadsign Gloves", "Roadsign 장갑"},
                {"Tactical Gloves", "전술 장갑"},
                {"Ghost Costume", "유령 의상"},
                {"Mummy Suit", "미라 슈트"},
                {"Scarecrow Suit", "허수아비 슈트"},
                {"Scarecrow Wrap", "허수아비 랩"},
                {"Hide Halterneck", "홀터넥 숨기기"},
                {"Beenie Hat", "비니 모자"},
                {"Boonie Hat", "Boonie 모자"},
                {"Bucket Helmet", "버킷 헬멧"},
                {"Burlap Headwrap", "삼베 머리 싸개"},
                {"Candle Hat", "캔들 모자"},
                {"Baseball Cap", "야구 모자"},
                {"Clatter Helmet", "클래 터 헬멧"},
                {"Coffee Can Helmet", "커피 캔 헬멧"},
                {"Bone Helmet", "뼈 헬멧"},
                {"Heavy Plate Helmet", "헤비 플레이트 헬멧"},
                {"Miners Hat", "광부 모자"},
                {"Party Hat", "파티 용 모자"},
                {"Riot Helmet", "폭동 헬멧"},
                {"Wood Armor Helmet", "목재 갑옷 헬멧"},
                {"Hoodie", "까마귀"},
                {"Bone Armor", "뼈 갑옷"},
                {"Heavy Plate Jacket", "헤비 플레이트 재킷"},
                {"Snow Jacket", "스노우 재킷"},
                {"Jacket", "재킷"},
                {"Wood Chestplate", "나무 흉갑"},
                {"Improvised Balaclava", "즉석 발라 클라 바"},
                {"Bandana Mask", "두건 마스크"},
                {"Metal Facemask", "메탈 페이스 마스크"},
                {"Night Vision Goggles", "나이트 비전 고글"},
                {"Burlap Trousers", "삼베 바지"},
                {"Heavy Plate Pants", "헤비 플레이트 팬츠"},
                {"Hide Pants", "바지 숨기기"},
                {"Road Sign Kilt", "도로 표지판 킬트"},
                {"Shorts", "반바지"},
                {"Wood Armor Pants", "나무 갑옷 바지"},
                {"Pants", "바지"},
                {"Hide Poncho", "Hide Poncho"},
                {"Burlap Shirt", "삼베 셔츠"},
                {"Shirt", "셔츠"},
                {"Hide Vest", "조끼 숨기기"},
                {"Tank Top", "탱크 탑"},
                {"Boots", "부츠"},
                {"Burlap Shoes", "삼베 신발"},
                {"Hide Boots", "부츠 숨기기"},
                {"Hide Skirt", "치마 숨기기"},
                {"Bandit Guard Gear", "산적 가드 장비"},
                {"Hazmat Suit", "방호복"},
                {"Scientist Suit", "과학자 슈트"},
                {"Space Suit", "우주복"},
                {"Heavy Scientist Suit", "무거운 과학자 슈트"},
                {"Longsleeve T-Shirt", "롱 슬리브 티셔츠"},
                {"T-Shirt", "티셔츠"},
                {"Metal Chest Plate", "금속 가슴 판"},
                {"Road Sign Jacket", "도로 표지 재킷"},
                {"Bleach", "표백제"},
                {"Duct Tape", "덕트 테이프"},
                {"Low Quality Carburetor", "저품질 기화기"},
                {"Medium Quality Carburetor", "중간 품질 기화기"},
                {"High Quality Carburetor", "고품질 기화기"},
                {"Low Quality Crankshaft", "저품질 크랭크 축"},
                {"Medium Quality Crankshaft", "중간 품질의 크랭크 축"},
                {"High Quality Crankshaft", "고품질 크랭크 축"},
                {"Low Quality Pistons", "저품질 피스톤"},
                {"Medium Quality Pistons", "중간 품질 피스톤"},
                {"High Quality Pistons", "고품질 피스톤"},
                {"Low Quality Spark Plugs", "저품질 점화 플러그"},
                {"Medium Quality Spark Plugs", "중간 품질의 점화 플러그"},
                {"High Quality Spark Plugs", "고품질 점화 플러그"},
                {"Low Quality Valves", "저품질 밸브"},
                {"Medium Quality Valves", "중간 품질 밸브"},
                {"High Quality Valves", "고품질 밸브"},
                {"Electric Fuse", "전기 퓨즈"},
                {"Gears", "기어"},
                {"Glue", "접착제"},
                {"Metal Blade", "금속 블레이드"},
                {"Metal Pipe", "금속 파이프"},
                {"Empty Propane Tank", "빈 프로판 탱크"},
                {"Road Signs", "도로 표지판"},
                {"Rope", "로프"},
                {"Sewing Kit", "재봉 키트"},
                {"Sheet Metal", "판금"},
                {"Metal Spring", "금속 스프링"},
                {"Sticks", "스틱"},
                {"Tarp", "타프"},
                {"Tech Trash", "기술 쓰레기"},
                {"Rifle Body", "라이플 바디"},
                {"Semi Automatic Body", "반자동 바디"},
                {"SMG Body", "SMG 바디"},
                {"Concrete Barricade", "콘크리트 바리케이드"},
                {"Wooden Barricade Cover", "나무 바리케이드 커버"},
                {"Metal Barricade", "금속 바리케이드"},
                {"Sandbag Barricade", "샌드백 바리케이드"},
                {"Stone Barricade", "돌 바리케이드"},
                {"Wooden Barricade", "나무 바리케이드"},
                {"Barbed Wooden Barricade", "가시 나무 바리케이드"},
                {"Barbeque", "바베큐"},
                {"Snap Trap", "스냅 트랩"},
                {"Bed", "침대"},
                {"Camp Fire", "캠프 파이어"},
                {"Ceiling Light", "천장 조명"},
                {"Chair", "의자"},
                {"Composter", "퇴비"},
                {"Computer Station", "컴퓨터 스테이션"},
                {"Drop Box", "드롭 박스"},
                {"Elevator", "엘리베이터"},
                {"Stone Fireplace", "돌 벽난로"},
                {"Blue Boomer", "블루 부머"},
                {"Champagne Boomer", "샴페인 부머"},
                {"Green Boomer", "그린 부머"},
                {"Orange Boomer", "오렌지 부머"},
                {"Red Boomer", "레드 부머"},
                {"Violet Boomer", "바이올렛 부머"},
                {"Blue Roman Candle", "블루 로마 캔들"},
                {"Green Roman Candle", "녹색 로마 양초"},
                {"Red Roman Candle", "붉은 로마 양초"},
                {"Violet Roman Candle", "바이올렛 로마 캔들"},
                {"White Volcano Firework", "하얀 화산 불꽃 놀이"},
                {"Red Volcano Firework", "붉은 화산 폭죽"},
                {"Violet Volcano Firework", "바이올렛 화산 불꽃 놀이"},
                {"Wooden Floor Spikes", "나무 바닥 스파이크"},
                {"Fridge", "냉장고"},
                {"Large Furnace", "대형로"},
                {"Furnace", "화로"},
                {"Hitch & Trough", "여물통"},
                {"Hab Repair", "곰 바닥"},
                {"Jack O Lantern Angry", "잭 오 랜턴 화가"},
                {"Jack O Lantern Happy", "잭 오 랜턴 해피"},
                {"Land Mine", "지뢰"},
                {"Lantern", "칸델라"},
                {"Large Wood Box", "큰 나무 상자"},
                {"Water Barrel", "물통"},
                {"Locker", "사물함"},
                {"Mail Box", "우편함"},
                {"Mixing Table", "믹싱 테이블"},
                {"Modular Car Lift", "모듈 형 자동차 리프트"},
                {"Pump Jack", "펌프 잭"},
                {"Small Oil Refinery", "소규모 정유"},
                {"Large Planter Box", "대형 화분 상자"},
                {"Small Planter Box", "작은 화분 상자"},
                {"Audio Alarm", "오디오 알람"},
                {"Smart Alarm", "스마트 알람"},
                {"Smart Switch", "스마트 스위치"},
                {"Storage Monitor", "스토리지 모니터"},
                {"Large Rechargable Battery", "대형 충전식 배터리"},
                {"Medium Rechargable Battery", "중형 충전식 배터리"},
                {"Small Rechargable Battery", "소형 충전식 배터리"},
                {"Button", "단추"},
                {"Counter", "카운터"},
                {"HBHF Sensor", "HBHF 센서"},
                {"Laser Detector", "레이저 감지기"},
                {"Pressure Pad", "압력 패드"},
                {"Door Controller", "컨트롤러 별"},
                {"Electric Heater", "전기 히터"},
                {"Fluid Combiner", "유체 결합"},
                {"Fluid Splitter", "유체 분배기"},
                {"Fluid Switch & Pump", "유체 스위치"},
                {"AND Switch", "AND 스위치"},
                {"Blocker", "차단제"},
                {"Electrical Branch", "전기 지점"},
                {"Root Combiner", "루트 결합기"},
                {"Memory Cell", "메모리 셀"},
                {"OR Switch", "OR 스위치"},
                {"RAND Switch", "RAND 스위치"},
                {"RF Broadcaster", "RF 방송사"},
                {"RF Receiver", "RF 수신기"},
                {"XOR Switch", "XOR 스위치"},
                {"Small Generator", "소형 발전기"},
                {"Test Generator", "테스트 생성기"},
                {"Large Solar Panel", "대형 태양 광 패널"},
                {"Igniter", "점화기"},
                {"Flasher Light", "점멸등"},
                {"Simple Light", "간단한 빛"},
                {"Siren Light", "사이렌 라이트"},
                {"Powered Water Purifier", "동력 정수기"},
                {"Switch", "스위치"},
                {"Splitter", "쪼개는 도구"},
                {"Sprinkler", "살포기"},
                {"Tesla Coil", "테슬라 코일"},
                {"Timer", "시간제 노동자"},
                {"Cable Tunnel", "케이블 터널"},
                {"Water Pump", "물 펌프"},
                {"Mining Quarry", "광산 채석장"},
                {"Reactive Target", "반응 대상"},
                {"Repair Bench", "수리 벤치"},
                {"Research Table", "연구 테이블"},
                {"Rug Bear Skin", "러그 베어 스킨"},
                {"Rug", "깔개"},
                {"Search Light", "검색 라이트"},
                {"Secret Lab Chair", "비밀 실험실 의자"},
                {"Salvaged Shelves", "인양 된 선반"},
                {"Large Banner Hanging", "대형 배너 걸기"},
                {"Two Sided Hanging Sign", "양면 교수형 기호"},
                {"Two Sided Ornate Hanging Sign", "양면 화려한 교수형 기호"},
                {"Landscape Picture Frame", "풍경 액자"},
                {"Portrait Picture Frame", "초상화 액자"},
                {"Tall Picture Frame", "키 큰 액자"},
                {"XL Picture Frame", "XL 액자"},
                {"XXL Picture Frame", "XXL 액자"},
                {"Large Banner on pole", "기둥에 큰 배너"},
                {"Double Sign Post", "이중 사인 포스트"},
                {"Single Sign Post", "단일 사인 포스트"},
                {"One Sided Town Sign Post", "일방적 타운 사인 포스트"},
                {"Two Sided Town Sign Post", "양면 타운 사인 포스트"},
                {"Huge Wooden Sign", "거대한 나무 기호"},
                {"Large Wooden Sign", "큰 나무 간판"},
                {"Medium Wooden Sign", "중간 나무 간판"},
                {"Small Wooden Sign", "작은 나무 기호"},
                {"Shotgun Trap", "샷건 트랩"},
                {"Sleeping Bag", "침낭"},
                {"Small Stash", "작은 보관함"},
                {"Spinning wheel", "물레"},
                {"Survival Fish Trap", "생존 물고기 함정"},
                {"Table", "표"},
                {"Work Bench Level 1", "작업대 레벨 1"},
                {"Work Bench Level 2", "작업대 레벨 2"},
                {"Work Bench Level 3", "작업대 레벨 3"},
                {"Tool Cupboard", "도구 찬장"},
                {"Tuna Can Lamp", "참치 캔 램프"},
                {"Vending Machine", "자판기"},
                {"Large Water Catcher", "대형 물 포수"},
                {"Small Water Catcher", "작은 물 포수"},
                {"Water Purifier", "정수기"},
                {"Wind Turbine", "풍력 터빈"},
                {"Wood Storage Box", "목재 보관함"},
                {"Apple", "사과"},
                {"Rotten Apple", "썩은 사과"},
                {"Black Raspberries", "블랙 라즈베리"},
                {"Blueberries", "블루 베리"},
                {"Bota Bag", "보타 백"},
                {"Cactus Flesh", "선인장 살"},
                {"Can of Beans", "콩 캔"},
                {"Can of Tuna", "참치 캔"},
                {"Chocolate Bar", "초콜릿 바"},
                {"Cooked Fish", "조리 된 생선"},
                {"Raw Fish", "생선"},
                {"Minnows", "미노우"},
                {"Small Trout", "작은 송어"},
                {"Granola Bar", "그래 놀라 바"},
                {"Burnt Chicken", "번트 치킨"},
                {"Cooked Chicken", "익힌 치킨"},
                {"Raw Chicken Breast", "생 닭 가슴살"},
                {"Spoiled Chicken", "버릇없는 치킨"},
                {"Burnt Deer Meat", "번트 사슴 고기"},
                {"Cooked Deer Meat", "요리 된 사슴 고기"},
                {"Raw Deer Meat", "날 사슴 고기"},
                {"Burnt Horse Meat", "번트 말고기"},
                {"Cooked Horse Meat", "말고기 요리"},
                {"Raw Horse Meat", "말고기 생고기"},
                {"Burnt Human Meat", "불에 탄 인간 고기"},
                {"Cooked Human Meat", "조리 된 육류"},
                {"Raw Human Meat", "생고기"},
                {"Spoiled Human Meat", "부패한 인간 고기"},
                {"Burnt Bear Meat", "탄 곰 고기"},
                {"Cooked Bear Meat", "익힌 곰 고기"},
                {"Raw Bear Meat", "곰 생고기"},
                {"Burnt Wolf Meat", "불에 탄 늑대 고기"},
                {"Cooked Wolf Meat", "요리 된 늑대 고기"},
                {"Raw Wolf Meat", "늑대 날고기"},
                {"Spoiled Wolf Meat", "버릇없는 늑대 고기"},
                {"Burnt Pork", "번트 포크"},
                {"Cooked Pork", "익힌 돼지 고기"},
                {"Raw Pork", "생 돼지"},
                {"Mushroom", "버섯"},
                {"Pickles", "절인 것"},
                {"Small Water Bottle", "작은 물병"},
                {"Water Jug", "물병"},
                {"Shovel Bass", "삽베이스"},
                {"Cowbell", "카우벨"},
                {"Junkyard Drum Kit", "폐차장 드럼 키트"},
                {"Pan Flute", "팬 플루트"},
                {"Acoustic Guitar", "어쿠스틱 기타"},
                {"Jerry Can Guitar", "제리 캔 기타"},
                {"Wheelbarrow Piano", "수레 피아노"},
                {"Canbourine", "Canbourine"},
                {"Plumber's Trumpet", "배관공의 트럼펫"},
                {"Sousaphone", "Sousaphone"},
                {"Xylobone", "자일로 본"},
                {"Car Key", "자동차 열쇠"},
                {"Door Key", "키별"},
                {"Key Lock", "키 잠금"},
                {"Code Lock", "코드 잠금"},
                {"Blueprint", "청사진"},
                {"Chinese Lantern", "중국풍 랜턴"},
                {"Dragon Door Knocker", "드래곤 도어 노커"},
                {"Dragon Mask", "드래곤 마스크"},
                {"New Year Gong", "새해 징"},
                {"Rat Mask", "쥐 마스크"},
                {"Firecracker String", "폭죽 끈"},
                {"Chippy Arcade Game", "치피 아케이드 게임"},
                {"Door Closer", "도어 클로저"},
                {"Bunny Ears", "토끼 귀"},
                {"Bunny Onesie", "토끼 Onesie"},
                {"Easter Door Wreath", "부활절 문 화환"},
                {"Egg Basket", "계란 바구니"},
                {"Rustigé Egg - Red", "러스티그? 계란 - 빨강"},
                {"Rustigé Egg - Blue", "러스티그? 계란 - 파란색"},
                {"Rustigé Egg - Purple", "러스티그? 계란 - 보라색"},
                {"Rustigé Egg - Ivory", "러스티그? 계란 - 아이보리"},
                {"Nest Hat", "둥지 모자"},
                {"Bronze Egg", "청동 달걀"},
                {"Gold Egg", "골드 에그"},
                {"Painted Egg", "페인트 달걀"},
                {"Silver Egg", "실버 에그"},
                {"Halloween Candy", "할로윈 캔디"},
                {"Large Candle Set", "라지 캔들 세트"},
                {"Small Candle Set", "작은 양초 세트"},
                {"Coffin", "관"},
                {"Cursed Cauldron", "저주받은 가마솥"},
                {"Gravestone", "묘비"},
                {"Wooden Cross", "나무 십자가"},
                {"Graveyard Fence", "묘지 울타리"},
                {"Large Loot Bag", "큰 전리품 가방"},
                {"Medium Loot Bag", "중형 전리품 가방"},
                {"Small Loot Bag", "작은 전리품 가방"},
                {"Pumpkin Bucket", "호박 통"},
                {"Scarecrow", "허수아비"},
                {"Skull Spikes", "해골 스파이크"},
                {"Skull Door Knocker", "해골 문 두 들기"},
                {"Skull Fire Pit", "해골 불 구덩이"},
                {"Spider Webs", "거미줄"},
                {"Spooky Speaker", "으스스한 스피커"},
                {"Surgeon Scrubs", "외과 의사 스크럽"},
                {"Skull Trophy", "해골 트로피"},
                {"Card Movember Moustache", "카드 Movember 콧수염"},
                {"Movember Moustache", "Movember 콧수염"},
                {"Note", "노트"},
                {"Human Skull", "인간 해골"},
                {"Above Ground Pool", "지상 수영장 위"},
                {"Beach Chair", "비치 의자"},
                {"Beach Parasol", "비치 파라솔"},
                {"Beach Table", "비치 테이블"},
                {"Beach Towel", "해변 용 수건"},
                {"Boogie Board", "부기 보드"},
                {"Inner Tube", "내부 튜브"},
                {"Instant Camera", "즉석 카메라"},
                {"Paddling Pool", "얕은 수영장"},
                {"Photograph", "사진"},
                {"Landscape Photo Frame", "풍경 사진 프레임"},
                {"Large Photo Frame", "대형 액자"},
                {"Portrait Photo Frame", "인물 사진 프레임"},
                {"Sunglasses", "색안경"},
                {"Water Gun", "물총"},
                {"Water Pistol", "물 권총"},
                {"Purple Sunglasses", "보라색 선글라스"},
                {"Headset", "헤드폰"},
                {"Candy Cane Club", "지팡이 사탕 몽둥이"},
                {"Christmas Lights", "크리스마스 조명"},
                {"Festive Doorway Garland", "축제 출입구 화환"},
                {"Candy Cane", "사탕 지팡이"},
                {"Giant Candy Decor", "거대한 사탕 장식"},
                {"Giant Lollipop Decor", "거대한 롤리팝 장식"},
                {"Pookie Bear", "푸키 베어"},
                {"Deluxe Christmas Lights", "디럭스 크리스마스 조명"},
                {"Coal :(", "석탄 :("},
                {"Large Present", "큰 선물"},
                {"Medium Present", "중간 현재"},
                {"Small Present", "작은 선물"},
                {"Snow Machine", "스노우 머신"},
                {"Snowball", "스노볼"},
                {"Snowman", "눈사람"},
                {"SUPER Stocking", "슈퍼 스타킹"},
                {"Small Stocking", "작은 스타킹"},
                {"Reindeer Antlers", "순록 뿔"},
                {"Santa Beard", "산타 수염"},
                {"Santa Hat", "산타 모자"},
                {"Festive Window Garland", "축제 창 화환"},
                {"Wrapped Gift", "포장 된 선물"},
                {"Wrapping Paper", "포장지"},
                {"Christmas Door Wreath", "크리스마스 문 화환"},
                {"Decorative Baubels", "장식용 보벨"},
                {"Decorative Plastic Candy Canes", "장식적인 플라스틱 사탕 지팡이"},
                {"Decorative Gingerbread Men", "장식용 진저 브레드 남자"},
                {"Tree Lights", "트리 조명"},
                {"Decorative Pinecones", "장식용 솔방울"},
                {"Star Tree Topper", "스타 트리 토퍼"},
                {"Decorative Tinsel", "장식용 반짝이"},
                {"Christmas Tree", "크리스마스 트리"},
                {"Auto Turret", "자동 터렛"},
                {"Flame Turret", "화염 포탑"},
                {"Glowing Eyes", "빛나는 눈"},
                {"SAM Ammo", "SAM 탄약"},
                {"SAM Site", "SAM 사이트"},
                {"Black Berry", "블랙 베리"},
                {"Black Berry Clone", "블랙 베리 클론"},
                {"Black Berry Seed", "블랙 베리 씨앗"},
                {"Blue Berry", "블루 베리"},
                {"Blue Berry Clone", "블루 베리 클론"},
                {"Blue Berry Seed", "블루 베리 씨앗"},
                {"Green Berry", "그린 베리"},
                {"Green Berry Clone", "그린 베리 클론"},
                {"Green Berry Seed", "그린 베리 씨앗"},
                {"Red Berry", "레드 베리"},
                {"Red Berry Clone", "레드 베리 클론"},
                {"Red Berry Seed", "레드 베리 씨앗"},
                {"White Berry", "화이트 베리"},
                {"White Berry Clone", "화이트 베리 클론"},
                {"White Berry Seed", "화이트 베리 씨"},
                {"Yellow Berry", "옐로우 베리"},
                {"Yellow Berry Clone", "옐로우 베리 클론"},
                {"Yellow Berry Seed", "옐로우 베리 씨앗"},
                {"Corn", "옥수수"},
                {"Corn Clone", "옥수수 클론"},
                {"Corn Seed", "옥수수 종자"},
                {"Hemp Clone", "대마 클론"},
                {"Hemp Seed", "대마 씨"},
                {"Potato", "감자"},
                {"Potato Clone", "감자 클론"},
                {"Potato Seed", "감자 씨앗"},
                {"Pumpkin", "호박"},
                {"Pumpkin Plant Clone", "호박 식물 클론"},
                {"Pumpkin Seed", "호박씨"},
                {"Animal Fat", "동물성 지방"},
                {"Battery - Small", "배터리-소형"},
                {"Blood", "피"},
                {"Bone Fragments", "뼈 조각"},
                {"CCTV Camera", "CCTV 카메라"},
                {"Charcoal", "숯"},
                {"Cloth", "천"},
                {"Crude Oil", "원유"},
                {"Diesel Fuel", "디젤 연료"},
                {"Empty Can Of Beans", "콩의 빈 캔"},
                {"Empty Tuna Can", "빈 참치 캔"},
                {"Explosives", "폭발물"},
                {"Fertilizer", "거름"},
                {"Gun Powder", "화약"},
                {"Horse Dung", "말똥"},
                {"High Quality Metal Ore", "고품질 금속 광석"},
                {"High Quality Metal", "고품질 금속"},
                {"Leather", "가죽"},
                {"Low Grade Fuel", "저급 연료"},
                {"Metal Fragments", "금속 조각"},
                {"Metal Ore", "금속 광석"},
                {"Paper", "종이"},
                {"Plant Fiber", "식물성 섬유"},
                {"Research Paper", "연구 종이"},
                {"Salt Water", "소금물"},
                {"Scrap", "폐철물"},
                {"Stones", "돌"},
                {"Sulfur Ore", "유황 광석"},
                {"Sulfur", "황"},
                {"Targeting Computer", "표적 컴퓨터"},
                {"Water", "물"},
                {"Wolf Skull", "늑대 해골"},
                {"Wood", "목재"},
                {"Advanced Healing Tea", "고급 힐링 티"},
                {"Basic Healing Tea", "기본 힐링 티"},
                {"Pure Healing Tea", "퓨어 힐링 티"},
                {"Advanced Max Health Tea", "고급 맥스 건강 차"},
                {"Basic Max Health Tea", "기본 맥스 건강 차"},
                {"Pure Max Health Tea", "퓨어 맥스 건강 차"},
                {"Advanced Ore Tea", "고급 광석 차"},
                {"Basic Ore Tea", "기본 광석 차"},
                {"Pure Ore Tea", "순수한 광석 차"},
                {"Advanced Rad. Removal Tea", "고급 방사능 제거 차"},
                {"Rad. Removal Tea", "방사능 제거 차"},
                {"Pure Rad. Removal Tea", "완전한 방사능 제거 차"},
                {"Adv. Anti-Rad Tea", "고급 향방사능 차"},
                {"Anti-Rad Tea", "향방사능 차"},
                {"Pure Anti-Rad Tea", "순수 안티 라드 차"},
                {"Advanced Scrap Tea", "고급 스크랩 티"},
                {"Basic Scrap Tea", "기본 스크랩 티"},
                {"Pure Scrap Tea", "순수 스크랩 티"},
                {"Advanced Wood Tea", "고급 목차"},
                {"Basic Wood Tea", "기본 우드 티"},
                {"Pure Wood Tea", "순수한 나무 차"},
                {"Anti-Radiation Pills", "방사능 치료 알약"},
                {"Binoculars", "쌍안경"},
                {"Timed Explosive Charge", "시간 제한 폭발물"},
                {"Camera", "카메라"},
                {"RF Transmitter", "RF 송신기"},
                {"Handmade Fishing Rod", "수제 낚싯대"},
                {"Flare", "플레어"},
                {"Flashlight", "플래시"},
                {"Geiger Counter", "가이거 계수관"},
                {"Hose Tool", "호스 도구"},
                {"Jackhammer", "잭해머"},
                {"Blue Keycard", "파란 키카드"},
                {"Green Keycard", "초록 키카드"},
                {"Red Keycard", "빨간 키카드"},
                {"Large Medkit", "대형 의료킷"},
                {"Paper Map", "종이지도"},
                {"Medical Syringe", "의료용 주사기"},
                {"RF Pager", "RF 호출기"},
                {"Building Plan", "건물 도안"},
                {"Smoke Grenade", "연막탄"},
                {"Supply Signal", "공급 신호"},
                {"Survey Charge", "조사료"},
                {"Wire Tool", "와이어 도구"},
                {"Small Chassis", "소형 섀시"},
                {"Medium Chassis", "중형 섀시"},
                {"Large Chassis", "대형 섀시"},
                {"Cockpit Vehicle Module", "조종석 차량 모듈"},
                {"Armored Cockpit Vehicle Module", "기갑 조종석 차량 모듈"},
                {"Cockpit With Engine Vehicle Module", "엔진 차량 모듈이있는 조종석"},
                {"Engine Vehicle Module", "엔진 차량 모듈"},
                {"Flatbed Vehicle Module", "평판 차량 모듈"},
                {"Armored Passenger Vehicle Module", "기갑 승용차 모듈"},
                {"Rear Seats Vehicle Module", "뒷좌석 차량 모듈"},
                {"Storage Vehicle Module", "저장 차량 모듈"},
                {"Taxi Vehicle Module", "택시 차량 모듈"},
                {"Large Flatbed Vehicle Module", "대형 평판 차량 모듈"},
                {"Fuel Tank Vehicle Module", "연료 탱크 차량 모듈"},
                {"Passenger Vehicle Module", "승용차 모듈"},
                {"Generic vehicle module", "일반 차량 모듈"},
                {"Telephone", "전화"},
                {"16x Zoom Scope", "16 배 줌 스코프"},
                {"Weapon flashlight", "무기 손전등"},
                {"Holosight", "홀로 사이트"},
                {"Weapon Lasersight", "무기 레이져사이트"},
                {"Muzzle Boost", "총구 부스트"},
                {"Muzzle Brake", "총구 브레이크"},
                {"Simple Handmade Sight", "간단한 수제 시력"},
                {"Silencer", "소음 장치"},
                {"8x Zoom Scope", "8 배 줌 스코프"},
                {"Assault Rifle", "돌격 소총"},
                {"Bandage", "붕대"},
                {"Beancan Grenade", "빈캔 수류탄"},
                {"Bolt Action Rifle", "볼트 액션 라이플"},
                {"Bone Club", "뼈 클럽"},
                {"Bone Knife", "뼈 칼"},
                {"Hunting Bow", "사냥 용 활"},
                {"Birthday Cake", "생일 케이크"},
                {"Chainsaw", "전기 톱"},
                {"Salvaged Cleaver", "인양 된 식칼"},
                {"Compound Bow", "컴파운드 보우"},
                {"Crossbow", "석궁"},
                {"Double Barrel Shotgun", "더블 배럴 샷건"},
                {"Eoka Pistol", "에오카 권총"},
                {"F1 Grenade", "F1 수류탄"},
                {"Flame Thrower", "화염 방사기"},
                {"Multiple Grenade Launcher", "다중 유탄 발사기"},
                {"Butcher Knife", "급조 식칼"},
                {"Pitchfork", "쇠스랑"},
                {"Sickle", "낫"},
                {"Hammer", "망치"},
                {"Hatchet", "철 도끼"},
                {"Combat Knife", "전투 용 칼"},
                {"L96 Rifle", "L96 소총"},
                {"LR-300 Assault Rifle", "LR-300 돌격 소총"},
                {"M249", "M249"},
                {"M39 Rifle", "M39 소총"},
                {"M92 Pistol", "M92 권총"},
                {"Mace", "철퇴"},
                {"Machete", "마체태"},
                {"MP5A4", "MP5A4"},
                {"Nailgun", "네일 건"},
                {"Paddle", "외륜"},
                {"Pickaxe", "곡괭이"},
                {"Waterpipe Shotgun", "파이프 샷건총"},
                {"Python Revolver", "파이썬 리볼버"},
                {"Revolver", "리볼버"},
                {"Rock", "돌"},
                {"Rocket Launcher", "로켓 발사기"},
                {"Salvaged Axe", "회수 된 도끼"},
                {"Salvaged Hammer", "회수 된 망치"},
                {"Salvaged Icepick", "회수 된 얼음 송이"},
                {"Satchel Charge", "사첼 차지"},
                {"Pump Shotgun", "펌프 샷건"},
                {"Semi-Automatic Pistol", "반자동 권총"},
                {"Semi-Automatic Rifle", "반자동 소총"},
                {"Custom SMG", "커스텀 SMG"},
                {"Spas-12 Shotgun", "Spas-12 산탄 총"},
                {"Stone Hatchet", "돌 손도끼"},
                {"Stone Pickaxe", "돌 곡괭이"},
                {"Stone Spear", "스톤 스피어"},
                {"Longsword", "롱소드"},
                {"Salvaged Sword", "인양 된 검"},
                {"Thompson", "톰슨"},
                {"Garry's Mod Tool Gun", "Garry의 모드 도구 총"},
                {"Torch", "토치"},
                {"Water Bucket", "물통"},
                {"Wooden Spear", "나무 창"},
                {"Roadsign Horse Armor", "도로 표지판 말 갑옷"},
                {"Wooden Horse Armor", "나무 말 갑옷"},
                {"Horse Saddle", "말 안장"},
                {"Saddle bag", "안장 가방"},
                {"High Quality Horse Shoes", "고품질 말 신발"},
                {"Basic Horse Shoes", "기본 말 신발"},
                {"Generic vehicle chassis", "일반 차량 섀시"}
            }, this, "ko"); //korean

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MessageShowNoEconomics", "GUIShop no va rebre cap resposta del connector Economics. Assegureu-vos que el connector d'Economia està instal·lat correctament."},
                {"MessageShowNoServerRewards", "GUIShop no ha rebut cap resposta del connector ServerRewards. Assegureu-vos que el connector ServerRewards està instal·lat correctament."},
                {"MessageBought", "Heu comprat {0} x {1} correctament."},
                {"Missatge venut", "Heu venut correctament {0} x {1}."},
                {"MessageErrorCooldown", "Només podeu comprar aquest article cada {0} segons."},
                {"MessageErrorSellCooldownAmount", "Només podeu vendre {0} d'aquest article cada {1} segons."},
                {"MessageErrorBuyCooldownAmount", "Només podeu comprar {0} d';aquest article cada {1} segons."},
                {"MessageErrorBuyLimit", "Només podeu comprar {0} d'aquest article."},
                {"MessageErrorSellLimit", "Només podeu vendre {0} d'aquest article."},
                {"MessageErrorInventoryFull", "El vostre inventari està ple."},
                {"MessageErrorInventorySlots", "Necessiteu almenys {0} ranures d&#39;inventari gratuïtes."},
                {"MessageErrorNoShop", "Hi ha algun problema amb aquesta botiga. Poseu-vos en contacte amb un administrador."},
                {"MessageErrorGlobalDisabled", "Les botigues globals estan desactivades. Aquest servidor utilitza proveïdors de NPC."},
                {"MessageErrorNoActionShop", "No podeu {0} en aquesta botiga"},
                {"MessageErrorNoActionItem", "No teniu permís per {0} aquest element aquí"},
                {"MessageErrorItemItem", "ADVERTÈNCIA: Sembla que aquest article de venda que teniu no és un article vàlid. Poseu-vos en contacte amb un administrador."},
                {"MessageErrorItemNoValidbuy", "ADVERTÈNCIA: Sembla que no és un article vàlid per comprar, poseu-vos en contacte amb un administrador."},
                {"MessageErrorItemNoValidsell", "ADVERTÈNCIA: Sembla que no és un article vàlid per vendre, poseu-vos en contacte amb un administrador."},
                {"MessageErrorRedeemKit", "ADVERTÈNCIA: S&#39;ha produït un error en donar-vos aquest kit. Poseu-vos en contacte amb un administrador."},
                {"MessageErrorBuyCmd", "No es poden comprar diversos elements."},
                {"MessageErrorBuyPrice", "AVÍS: l&#39;administrador no va donar cap preu de compra, no podeu comprar aquest article"},
                {"MessageErrorSellPrice", "ADVERTÈNCIA: l&#39;administrador no ha donat cap preu de venda, no es pot vendre aquest article"},
                {"MessageErrorNotEnoughMoney", "Necessiteu {0} monedes per comprar {1} de {2}"},
                {"MessageErrorNotEnoughMoneyCustom", "Necessiteu {0} moneda per comprar {1} x {2}"},
                {"MessageErrorNotEnoughSell", "No en teniu prou amb aquest element."},
                {"MessageErrorNotNothing", "No podeu comprar zero d&#39;aquest article."},
                {"MessageErrorItemNoExist", "ADVERTÈNCIA: sembla que no existeix l&#39;article que intenteu comprar. Poseu-vos en contacte amb un administrador."},
                {"MessageErrorItemNoExistTake", "L&#39;element que intenteu vendre no es pot vendre en aquest moment."},
                {"MessageErrorBuildingBlocked", "No es pot comprar mentre es troba en una zona bloquejada."},
                {"MessageErrorAdmin", "No teniu els permisos correctes per utilitzar aquesta ordre. (guishop.admin)"},
                {"MessageErrorWaitingOnDownloads", "GUIShop espera que finalitzin les baixades de ImageLibrary"},
                {"BlockMonuments", "No podeu fer servir la botiga a prop d&#39;un monument."},
                {"MessageErrorItemNotEnabled", "El botiguer ha desactivat aquest article."},
                {"MessageErrorItemNotFound", "No s&#39;ha trobat l&#39;element"},
                {"CantSellCommands", "No podeu vendre comandes a la botiga."},
                {"CantSellKits", "No es poden vendre els kits a la botiga."},
                {"MessageErrorCannotSellWhileEquiped", "No podeu vendre l&#39;article si el teniu equipat."},
                {"MessageShopResponse", "GUIShop espera que finalitzin les baixades de ImageLibrary, espereu."},
                {"MessageNPCResponseclose", "Gràcies per comprar a {0} torna aviat."},
                {"MessageNPCResponseopen", "Us donem la benvinguda a {0} què voleu comprar? Premeu E per començar a comprar."},
                {"Commands", "Ordres"},
                {"Attire", "Vestimenta"},
                {"Misc", "Misc"},
                {"Items", "Articles"},
                {"Ammunition", "Munició"},
                {"Construction", "Construcció"},
                {"Component", "Component"},
                {"Traps", "Paranys"},
                {"Electrical", "Elèctric"},
                {"Fun", "Diversió"},
                {"Food", "Menjar"},
                {"Resources", "Recursos"},
                {"Tool", "Eina"},
                {"Weapon", "Arma"},
                {"Medical", "Mèdic"},
                {"Minicopter", "Minicòpter"},
                {"Sedan", "Des de"},
                {"Airdrop Call", "Trucada Airdrop"},
                {"Wolf Headdress", "Toc de llop"},
                {"Fogger-3000", "Fogger-3000"},
                {"Strobe Light", "Llum estroboscòpica"},
                {"Kayak", "M&#39;agrada"},
                {"MC repair", "Reparació MC"},
                {"ScrapTransportHeliRepair", "ScrapTransportHeliRepair"},
                {"40mm Shotgun Round", "Escopeta rodona de 40 mm"},
                {"40mm HE Grenade", "Granada HE de 40 mm"},
                {"40mm Smoke Grenade", "Granada de fum de 40 mm"},
                {"High Velocity Arrow", "Fletxa d&#39;alta velocitat"},
                {"Wooden Arrow", "Fletxa de fusta"},
                {"Bone Arrow", "Fletxa òssia"},
                {"Fire Arrow", "Fletxa de foc"},
                {"Handmade Shell", "Petxina feta a mà"},
                {"Nailgun Nails", "Nailsgun Nails"},
                {"Pistol Bullet", "Bala de pistola"},
                {"Incendiary Pistol Bullet", "Bala de pistola incendiària"},
                {"HV Pistol Ammo", "Munició de pistola HV"},
                {"5.56 Rifle Ammo", "5,56 Munició de rifle"},
                {"Explosive 5.56 Rifle Ammo", "Munició explosiva de fusell 5.56"},
                {"Incendiary 5.56 Rifle Ammo", "Munició del rifle incendiari 5,56"},
                {"HV 5.56 Rifle Ammo", "HV 5.56 Munició per a rifles"},
                {"Rocket", "Coet"},
                {"Incendiary Rocket", "Coet Incendiari"},
                {"High Velocity Rocket", "Coet d&#39;alta velocitat"},
                {"Smoke Rocket WIP!!!!", "Smoke Rocket WIP !!!!"},
                {"12 Gauge Buckshot", "12 Calibre Buckshot"},
                {"12 Gauge Incendiary Shell", "Carcassa Incendiaria de calibre 12"},
                {"12 Gauge Slug", "12 Calibre Slug"},
                {"Sheet Metal Double Door", "Porta doble de xapa"},
                {"Armored Double Door", "Porta blindada doble"},
                {"Wood Double Door", "Porta doble de fusta"},
                {"Sheet Metal Door", "Porta de xapa"},
                {"Armored Door", "Porta Blindada"},
                {"Wooden Door", "Porta de fusta"},
                {"Floor grill", "Graella de terra"},
                {"Ladder Hatch", "Ladder Hatch"},
                {"Floor triangle grill", "Graella de triangle de terra"},
                {"Triangle Ladder Hatch", "Triangle Ladder Hatch"},
                {"High External Stone Gate", "Porta externa de pedra alta"},
                {"High External Wooden Gate", "Porta exterior alta de fusta"},
                {"Wooden Ladder", "Escala de fusta"},
                {"High External Stone Wall", "Alt mur exterior de pedra"},
                {"High External Wooden Wall", "Paret exterior de fusta alta"},
                {"Prison Cell Gate", "Porta de les cel·les de la presó"},
                {"Prison Cell Wall", "Paret cel·lular de la presó"},
                {"Chainlink Fence Gate", "Porta de la tanca Chainlink"},
                {"Chainlink Fence", "Tanca de cadena"},
                {"Garage Door", "Porta del garatge"},
                {"Netting", "Red"},
                {"Shop Front", "Botiga"},
                {"Metal Shop Front", "Metal Shop Front"},
                {"Metal Window Bars", "Barres de finestres metàl·liques"},
                {"Reinforced Window Bars", "Barres de finestres reforçades"},
                {"Wooden Window Bars", "Barres de finestres de fusta"},
                {"Metal horizontal embrasure", "Embassament horitzontal metàl·lic"},
                {"Metal Vertical embrasure", "Embassat vertical de metall"},
                {"Reinforced Glass Window", "Finestra de vidre reforçat"},
                {"Wood Shutters", "Persianes de fusta"},
                {"Watch Tower", "Torre de vigilància"},
                {"Diving Fins", "Aletes de busseig"},
                {"Diving Mask", "Màscara de busseig"},
                {"Diving Tank", "Dipòsit de busseig"},
                {"Wetsuit", "Vestit de neoprè"},
                {"Frog Boots", "Botes de granota"},
                {"A Barrel Costume", "Un vestit de barril"},
                {"Crate Costume", "Disfressa de caixa"},
                {"Burlap Gloves", "Guants de arpillera"},
                {"Leather Gloves", "Guants de pell"},
                {"Roadsign Gloves", "Guants Roadsign"},
                {"Tactical Gloves", "Guants tàctics"},
                {"Ghost Costume", "Disfressa de fantasma"},
                {"Mummy Suit", "Vestit de mòmia"},
                {"Scarecrow Suit", "Vestit d’espantaocells"},
                {"Scarecrow Wrap", "Embolcall d’espantaocells"},
                {"Hide Halterneck", "Amaga Halterneck"},
                {"Beenie Hat", "Beenie Hat"},
                {"Boonie Hat", "Barret Boonie"},
                {"Bucket Helmet", "Casc de cubell"},
                {"Burlap Headwrap", "Cinturó de arpillera"},
                {"Candle Hat", "Barret de vela"},
                {"Baseball Cap", "Baseball Cap"},
                {"Clatter Helmet", "Casc Clatter"},
                {"Coffee Can Helmet", "Casc de llauna de cafè"},
                {"Bone Helmet", "Casc d’os"},
                {"Heavy Plate Helmet", "Casc de plat pesat"},
                {"Miners Hat", "Barret de miners"},
                {"Party Hat", "Barret de festa"},
                {"Riot Helmet", "Casc antidisturbis"},
                {"Wood Armor Helmet", "Casc d&#39;armadura de fusta"},
                {"Hoodie", "Dessuadora"},
                {"Bone Armor", "Armadura òssia"},
                {"Heavy Plate Jacket", "Jaqueta de placa pesada"},
                {"Snow Jacket", "Jaqueta de neu"},
                {"Jacket", "Jaqueta"},
                {"Wood Chestplate", "Plat de fusta"},
                {"Improvised Balaclava", "Passamuntanyes improvisades"},
                {"Bandana Mask", "Màscara Bandana"},
                {"Metal Facemask", "Màscara facial de metall"},
                {"Night Vision Goggles", "Ulleres de visió nocturna"},
                {"Burlap Trousers", "Pantalons de arpillera"},
                {"Heavy Plate Pants", "Pantalons de plat pesats"},
                {"Hide Pants", "Amaga els pantalons"},
                {"Road Sign Kilt", "Senyal de trànsit Kilt"},
                {"Shorts", "Pantalons curts"},
                {"Wood Armor Pants", "Pantalons d&#39;armadura de fusta"},
                {"Pants", "Pantalons"},
                {"Hide Poncho", "Amaga el poncho"},
                {"Burlap Shirt", "Samarreta de arpillera"},
                {"Shirt", "Samarreta"},
                {"Hide Vest", "Amaga l&#39;armilla"},
                {"Tank Top", "Samarreta tancada"},
                {"Boots", "Botes"},
                {"Burlap Shoes", "Sabates de arpillera"},
                {"Hide Boots", "Amaga les botes"},
                {"Hide Skirt", "Amaga la faldilla"},
                {"Bandit Guard Gear", "Bandit Guard Gear"},
                {"Hazmat Suit", "Vestit Hazmat"},
                {"Scientist Suit", "Vestit de científic"},
                {"Space Suit", "Vestit espacial"},
                {"Heavy Scientist Suit", "Vestit de científic pesat"},
                {"Longsleeve T-Shirt", "Samarreta de màniga llarga"},
                {"T-Shirt", "Samarreta"},
                {"Metal Chest Plate", "Plat de pit de metall"},
                {"Road Sign Jacket", "Jaqueta de senyal de trànsit"},
                {"Bleach", "Lleixiu"},
                {"Duct Tape", "Cinta adhesiva"},
                {"Low Quality Carburetor", "Carburador de baixa qualitat"},
                {"Medium Quality Carburetor", "Carburador de qualitat mitjana"},
                {"High Quality Carburetor", "Carburador d&#39;alta qualitat"},
                {"Low Quality Crankshaft", "Cigonyal de baixa qualitat"},
                {"Medium Quality Crankshaft", "Cigonyal de qualitat mitjana"},
                {"High Quality Crankshaft", "Cigonyal d&#39;alta qualitat"},
                {"Low Quality Pistons", "Pistons de baixa qualitat"},
                {"Medium Quality Pistons", "Pistons de qualitat mitjana"},
                {"High Quality Pistons", "Pistons d&#39;alta qualitat"},
                {"Low Quality Spark Plugs", "Bougies de baixa qualitat"},
                {"Medium Quality Spark Plugs", "Sparks de qualitat mitjana"},
                {"High Quality Spark Plugs", "Bujies d&#39;alta qualitat"},
                {"Low Quality Valves", "Vàlvules de baixa qualitat"},
                {"Medium Quality Valves", "Vàlvules de qualitat mitjana"},
                {"High Quality Valves", "Vàlvules d&#39;alta qualitat"},
                {"Electric Fuse", "Fusible elèctric"},
                {"Gears", "Engranatges"},
                {"Glue", "Cola"},
                {"Metal Blade", "Full de metall"},
                {"Metal Pipe", "Tub de metall"},
                {"Empty Propane Tank", "Dipòsit de propà buit"},
                {"Road Signs", "Els senyals de trànsit"},
                {"Rope", "Corda"},
                {"Sewing Kit", "Kit de costura"},
                {"Sheet Metal", "Xapa de metall"},
                {"Metal Spring", "Primavera de metall"},
                {"Sticks", "Pals"},
                {"Tarp", "Lona"},
                {"Tech Trash", "Paperera tècnica"},
                {"Rifle Body", "Cos del fusell"},
                {"Semi Automatic Body", "Cos semi automàtic"},
                {"SMG Body", "Cos SMG"},
                {"Concrete Barricade", "Barricada de formigó"},
                {"Wooden Barricade Cover", "Coberta de barricada de fusta"},
                {"Metal Barricade", "Barricada de metall"},
                {"Sandbag Barricade", "Barricada de Sandbag"},
                {"Stone Barricade", "Barricada de Pedra"},
                {"Wooden Barricade", "Barricada de fusta"},
                {"Barbed Wooden Barricade", "Barricada de fusta de pues"},
                {"Barbeque", "Barbacoa"},
                {"Snap Trap", "Snap Trap"},
                {"Bed", "Llit"},
                {"Camp Fire", "Foc de camp"},
                {"Ceiling Light", "Llum de sostre"},
                {"Chair", "Cadira"},
                {"Composter", "Compostador"},
                {"Computer Station", "Estació Informàtica"},
                {"Drop Box", "Drop Box"},
                {"Elevator", "Ascensor"},
                {"Stone Fireplace", "Xemeneia de pedra"},
                {"Blue Boomer", "Blue Boomer"},
                {"Champagne Boomer", "Champagne Boomer"},
                {"Green Boomer", "Green Boomer"},
                {"Orange Boomer", "Orange Boomer"},
                {"Red Boomer", "Red Boomer"},
                {"Violet Boomer", "Violeta Boomer"},
                {"Blue Roman Candle", "Espelma blava romana"},
                {"Green Roman Candle", "Espelma romana verda"},
                {"Red Roman Candle", "Espelma romana vermella"},
                {"Violet Roman Candle", "Espelma romana violeta"},
                {"White Volcano Firework", "Focs artificials del volcà blanc"},
                {"Red Volcano Firework", "Focs artificials del volcà vermell"},
                {"Violet Volcano Firework", "Focs artificials del volcà violeta"},
                {"Wooden Floor Spikes", "Pics de terra de fusta"},
                {"Fridge", "Nevera"},
                {"Large Furnace", "Forn gran"},
                {"Furnace", "Forn"},
                {"Hitch & Trough", "Enganxi"},
                {"Hab Repair", "Tinc reparació"},
                {"Jack O Lantern Angry", "Jack O Lantern Angry"},
                {"Jack O Lantern Happy", "Jack O Lantern Happy"},
                {"Land Mine", "Terra Mina"},
                {"Lantern", "Llanterna"},
                {"Large Wood Box", "Capsa de fusta gran"},
                {"Water Barrel", "Barril d’aigua"},
                {"Locker", "Taquilla"},
                {"Mail Box", "Bústia de correu"},
                {"Mixing Table", "Taula de mescles"},
                {"Modular Car Lift", "Elevador modular de cotxes"},
                {"Pump Jack", "Pump Jack"},
                {"Small Oil Refinery", "Petita refineria de petroli"},
                {"Large Planter Box", "Caixa de jardineres gran"},
                {"Small Planter Box", "Caixa de jardiner petit"},
                {"Audio Alarm", "Alarma d&#39;àudio"},
                {"Smart Alarm", "Alarma intel·ligent"},
                {"Smart Switch", "Smart Switch"},
                {"Storage Monitor", "Monitor d&#39;emmagatzematge"},
                {"Large Rechargable Battery", "Bateria recarregable gran"},
                {"Medium Rechargable Battery", "Bateria recarregable mitjana"},
                {"Small Rechargable Battery", "Bateria recarregable petita"},
                {"Button", "Botó"},
                {"Counter", "Comptador"},
                {"HBHF Sensor", "Sensor HBHF"},
                {"Laser Detector", "Detector làser"},
                {"Pressure Pad", "Coixinet de pressió"},
                {"Door Controller", "A càrrec del controlador"},
                {"Electric Heater", "Escalfador elèctric"},
                {"Fluid Combiner", "Combina de fluids"},
                {"Fluid Splitter", "Separador de fluids"},
                {"Fluid Switch & Pump", "Interruptor de fluid"},
                {"AND Switch", "I commuta"},
                {"Blocker", "Bloquejador"},
                {"Electrical Branch", "Branca elèctrica"},
                {"Root Combiner", "Combinador d&#39;arrels"},
                {"Memory Cell", "Cèl·lula de memòria"},
                {"OR Switch", "O commutador"},
                {"RAND Switch", "Interruptor RAND"},
                {"RF Broadcaster", "Emissor de RF"},
                {"RF Receiver", "Receptor de RF"},
                {"XOR Switch", "Interruptor XOR"},
                {"Small Generator", "Petit generador"},
                {"Test Generator", "Generador de proves"},
                {"Large Solar Panel", "Gran panell solar"},
                {"Igniter", "Encenedor"},
                {"Flasher Light", "Llum intermitent"},
                {"Simple Light", "Llum senzilla"},
                {"Siren Light", "Llum de sirena"},
                {"Powered Water Purifier", "Purificador d’aigua alimentat"},
                {"Switch", "Interruptor"},
                {"Splitter", "Divisor"},
                {"Sprinkler", "Aspersor"},
                {"Tesla Coil", "Bobina Tesla"},
                {"Timer", "Temporitzador"},
                {"Cable Tunnel", "Túnel del cable"},
                {"Water Pump", "Bomba d'aigua"},
                {"Mining Quarry", "Pedrera Minera"},
                {"Reactive Target", "Objectiu reactiu"},
                {"Repair Bench", "Banc de reparació"},
                {"Research Table", "Taula d’investigació"},
                {"Rug Bear Skin", "Catifa de pell d'ós"},
                {"Rug", "Catifa"},
                {"Search Light", " "},
                {"Secret Lab Chair", "Càtedra Secret Lab"},
                {"Salvaged Shelves", "Prestatges recuperats"},
                {"Large Banner Hanging", "Penjador de pancarta gran"},
                {"Two Sided Hanging Sign", "Rètol penjat de dues cares"},
                {"Two Sided Ornate Hanging Sign", "Rètol penjat adornat a dues cares"},
                {"Landscape Picture Frame", "Marc de fotografia horitzontal"},
                {"Portrait Picture Frame", "Marc de retrat"},
                {"Tall Picture Frame", "Marc alt"},
                {"XL Picture Frame", "Marc de fotos XL"},
                {"XXL Picture Frame", "Marc XXL"},
                {"Large Banner on pole", "Gran pancarta al pal"},
                {"Double Sign Post", "Publicació de doble signe"},
                {"Single Sign Post", "Publicació amb senyal únic"},
                {"One Sided Town Sign Post", "Post de senyalització de la ciutat a una cara"},
                {"Two Sided Town Sign Post", "Post de senyalització de dues cares"},
                {"Huge Wooden Sign", "Rètol de fusta enorme"},
                {"Large Wooden Sign", "Rètol de fusta gran"},
                {"Medium Wooden Sign", "Rètol de fusta mitjà"},
                {"Small Wooden Sign", "Petit cartell de fusta"},
                {"Shotgun Trap", "Shotgun Trap"},
                {"Sleeping Bag", "Sac de dormir"},
                {"Small Stash", "Small Stash"},
                {"Spinning wheel", "Roda giratòria"},
                {"Survival Fish Trap", "Trampa de peixos de supervivència"},
                {"Table", "Taula"},
                {"Work Bench Level 1", "Banc de treball nivell 1"},
                {"Work Bench Level 2", "Banc de treball nivell 2"},
                {"Work Bench Level 3", "Banc de treball nivell 3"},
                {"Tool Cupboard", "Armari d’eines"},
                {"Tuna Can Lamp", "Llum Tuna Can"},
                {"Vending Machine", "Expenedor automàtic"},
                {"Large Water Catcher", "Gran captador d’aigua"},
                {"Small Water Catcher", "Petit captador d’aigua"},
                {"Water Purifier", "Depurador d'aigua"},
                {"Wind Turbine", "Aerogenerador"},
                {"Wood Storage Box", "Caixa de guarda de fusta"},
                {"Apple", "poma"},
                {"Rotten Apple", "Poma podrida"},
                {"Black Raspberries", "Gerds Negres"},
                {"Blueberries", "Nabius"},
                {"Bota Bag", "Bossa Bota"},
                {"Cactus Flesh", "Carn de Cactus"},
                {"Can of Beans", "Llauna de Mongetes"},
                {"Can of Tuna", "Llauna de Tonyina"},
                {"Chocolate Bar", "Barra de xocolata"},
                {"Cooked Fish", "Peix cuit"},
                {"Raw Fish", "Peix cru"},
                {"Minnows", "Minnows"},
                {"Small Trout", "Truita petita"},
                {"Granola Bar", "Barreta de cereals"},
                {"Burnt Chicken", "Pollastre Cremat"},
                {"Cooked Chicken", "Pollastre cuit"},
                {"Raw Chicken Breast", "Pit de pollastre cru"},
                {"Spoiled Chicken", "Pollastre espatllat"},
                {"Burnt Deer Meat", "Carn de cérvol cremada"},
                {"Cooked Deer Meat", "Carn de Cérvol Cuita"},
                {"Raw Deer Meat", "Carn crua de cérvol"},
                {"Burnt Horse Meat", "Carn de cavall cremada"},
                {"Cooked Horse Meat", "Carn de cavall cuita"},
                {"Raw Horse Meat", "Carn crua de cavall"},
                {"Burnt Human Meat", "Carn humana cremada"},
                {"Cooked Human Meat", "Carn humana cuita"},
                {"Raw Human Meat", "Carn humana crua"},
                {"Spoiled Human Meat", "Carn humana espatllada"},
                {"Burnt Bear Meat", "Carn d’Ós Cremat"},
                {"Cooked Bear Meat", "Carn d’Ós Cuita"},
                {"Raw Bear Meat", "Carn d’ós cru"},
                {"Burnt Wolf Meat", "Carn de llop cremada"},
                {"Cooked Wolf Meat", "Carn de llop cuita"},
                {"Raw Wolf Meat", "Carn crua de llop"},
                {"Spoiled Wolf Meat", "Carn de llop espatllada"},
                {"Burnt Pork", "Porc cremat"},
                {"Cooked Pork", "Porc cuit"},
                {"Raw Pork", "Porc cru"},
                {"Mushroom", "Bolet"},
                {"Pickles", "Escabetxos"},
                {"Small Water Bottle", "Ampolla d'aigua petita"},
                {"Water Jug", "Gerra d’aigua"},
                {"Shovel Bass", "Pala de baix"},
                {"Cowbell", "Campanar"},
                {"Junkyard Drum Kit", "Kit de bateria Junkyard"},
                {"Pan Flute", "Flauta de pa"},
                {"Acoustic Guitar", "Guitarra acústica"},
                {"Jerry Can Guitar", "Jerry Can Guitar"},
                {"Wheelbarrow Piano", "Carretó Piano"},
                {"Canbourine", "Canbourine"},
                {"Plumber's Trumpet", "Trompeta de lampista"},
                {"Sousaphone", "Sousàfon"},
                {"Xylobone", "Xilobona"},
                {"Car Key", "Clau del cotxe"},
                {"Door Key", "Per clau"},
                {"Key Lock", "Tecla de bloqueig"},
                {"Code Lock", "Bloqueig de codi"},
                {"Blueprint", "Plànol"},
                {"Chinese Lantern", "Llanterna xinesa"},
                {"Dragon Door Knocker", "Porta de drac"},
                {"Dragon Mask", "Màscara de drac"},
                {"New Year Gong", "Gong d&#39;Any Nou"},
                {"Rat Mask", "Màscara de rata"},
                {"Firecracker String", "Cadena de petards"},
                {"Chippy Arcade Game", "Joc Chippy Arcade"},
                {"Door Closer", "Tancador de porta"},
                {"Bunny Ears", "Orelles de conill"},
                {"Bunny Onesie", "Bunny Onesie"},
                {"Easter Door Wreath", "Corona de Porta de Pasqua"},
                {"Egg Basket", "Cistella d’ous"},
                {"Rustigé Egg - Red", "Ou tranquil: vermell"},
                {"Rustigé Egg - Blue", "Ou tranquil: blau"},
                {"Rustigé Egg - Purple", "Ou tranquil: porpra"},
                {"Rustigé Egg - Ivory", "Ou tranquil: marfil"},
                {"Nest Hat", "Barret de niu"},
                {"Bronze Egg", "Ou de bronze"},
                {"Gold Egg", "Ou d’or"},
                {"Painted Egg", "Ou pintat"},
                {"Silver Egg", "Ou de plata"},
                {"Halloween Candy", "Caramels de Halloween"},
                {"Large Candle Set", "Conjunt d&#39;espelmes grans"},
                {"Small Candle Set", "Conjunt d’espelmes petites"},
                {"Coffin", "Taüt"},
                {"Cursed Cauldron", "Caldero maleït"},
                {"Gravestone", "Làpida mortal"},
                {"Wooden Cross", "Creu de fusta"},
                {"Graveyard Fence", "Tanca del cementiri"},
                {"Large Loot Bag", "Bossa de botí gran"},
                {"Medium Loot Bag", "Bossa de botí mitjà"},
                {"Small Loot Bag", "Bossa petita botí"},
                {"Pumpkin Bucket", "Cub de carbassa"},
                {"Scarecrow", "Espantaocells"},
                {"Skull Spikes", "Pics del crani"},
                {"Skull Door Knocker", "Porta del crani"},
                {"Skull Fire Pit", "Fossa del Foc del Crani"},
                {"Spider Webs", "Teranyines"},
                {"Spooky Speaker", "Spooky Speaker"},
                {"Surgeon Scrubs", "Cirurgia Scrubs"},
                {"Skull Trophy", "Trofeu Crani"},
                {"Card Movember Moustache", "Targeta Movember Bigoti"},
                {"Movember Moustache", "Bigoti Movember"},
                {"Note", "Nota"},
                {"Human Skull", "Crani humà"},
                {"Above Ground Pool", "Piscina sobre terra"},
                {"Beach Chair", "Cadira de platja"},
                {"Beach Parasol", "Para-sols de platja"},
                {"Beach Table", "Taula de platja"},
                {"Beach Towel", "Tovallola de platja"},
                {"Boogie Board", "Boogie Board"},
                {"Inner Tube", "Tub interior"},
                {"Instant Camera", "Càmera instantània"},
                {"Paddling Pool", "Piscina de rem"},
                {"Photograph", "Fotografia"},
                {"Landscape Photo Frame", "Marc de fotos de paisatge"},
                {"Large Photo Frame", "Marc de fotos gran"},
                {"Portrait Photo Frame", "Marc de retrat"},
                {"Sunglasses", "Ulleres de sol"},
                {"Water Gun", "Pistola d'aigua"},
                {"Water Pistol", "Pistola d’aigua"},
                {"Purple Sunglasses", "Ulleres de sol morades"},
                {"Headset", "Auriculars"},
                {"Candy Cane Club", "Club Candy Cane"},
                {"Christmas Lights", "Llums de Nadal"},
                {"Festive Doorway Garland", "Garland de porta festiva"},
                {"Candy Cane", "Bastó de caramel"},
                {"Giant Candy Decor", "Decoració de caramels gegants"},
                {"Giant Lollipop Decor", "Decoració de Piruletes Gegants"},
                {"Pookie Bear", "Ós Pookie"},
                {"Deluxe Christmas Lights", "Llums de Nadal de luxe"},
                {"Coal :(", "Carbó :("},
                {"Large Present", "Regal gran"},
                {"Medium Present", "Present mitjà"},
                {"Small Present", "Petit present"},
                {"Snow Machine", "Màquina de neu"},
                {"Snowball", "Bola de neu"},
                {"Snowman", "Ninot de neu"},
                {"SUPER Stocking", "SUPER Mitja"},
                {"Small Stocking", "Mitja mitja"},
                {"Reindeer Antlers", "Astes de rens"},
                {"Santa Beard", "Santa Barba"},
                {"Santa Hat", "Barret de Pare Noel"},
                {"Festive Window Garland", "Garland de finestres festives"},
                {"Wrapped Gift", "Regal embolicat"},
                {"Wrapping Paper", "Paper de regal"},
                {"Christmas Door Wreath", "Corona de portes de Nadal"},
                {"Decorative Baubels", "Baubels decoratius"},
                {"Decorative Plastic Candy Canes", "Bastons de caramel de plàstic decoratius"},
                {"Decorative Gingerbread Men", "Homes de pa de pessic decoratius"},
                {"Tree Lights", "Llums dels arbres"},
                {"Decorative Pinecones", "Pinyes decoratives"},
                {"Star Tree Topper", "Star Tree Topper"},
                {"Decorative Tinsel", "Oropell decoratiu"},
                {"Christmas Tree", "Arbre de Nadal"},
                {"Auto Turret", "Torreta automàtica"},
                {"Flame Turret", "Torreta de Flama"},
                {"Glowing Eyes", "Ulls brillants"},
                {"SAM Ammo", "Munició SAM"},
                {"SAM Site", "Lloc SAM"},
                {"Black Berry", "Baia Negra"},
                {"Black Berry Clone", "Clon de baies negres"},
                {"Black Berry Seed", "Llavor de baies negres"},
                {"Blue Berry", "Blue Berry"},
                {"Blue Berry Clone", "Clon de Blue Berry"},
                {"Blue Berry Seed", "Llavor de baia blava"},
                {"Green Berry", "Baia Verda"},
                {"Green Berry Clone", "Clon de baies verdes"},
                {"Green Berry Seed", "Llavor de baya verda"},
                {"Red Berry", "Baia vermella"},
                {"Red Berry Clone", "Clon de baies vermelles"},
                {"Red Berry Seed", "Llavor de baia vermella"},
                {"White Berry", "Baia Blanca"},
                {"White Berry Clone", "Clon de la baia blanca"},
                {"White Berry Seed", "Llavor de baia blanca"},
                {"Yellow Berry", "Baia groga"},
                {"Yellow Berry Clone", "Clon de baies grogues"},
                {"Yellow Berry Seed", "Llavor de baia groga"},
                {"Corn", "Blat de moro"},
                {"Corn Clone", "Clon de blat de moro"},
                {"Corn Seed", "Llavor de blat de moro"},
                {"Hemp Clone", "Clon de cànem"},
                {"Hemp Seed", "Llavor de cànem"},
                {"Potato", "Patata"},
                {"Potato Clone", "Clon de patata"},
                {"Potato Seed", "Llavor de patata"},
                {"Pumpkin", "Carabassa"},
                {"Pumpkin Plant Clone", "Clon de plantes de carbassa"},
                {"Pumpkin Seed", "Llavor de carbassa"},
                {"Animal Fat", "Greix animal"},
                {"Battery - Small", "Bateria: petita"},
                {"Blood", "Sang"},
                {"Bone Fragments", "Fragments ossis"},
                {"CCTV Camera", "Càmera CCTV"},
                {"Charcoal", "Carbó vegetal"},
                {"Cloth", "Drap"},
                {"Crude Oil", "Cru"},
                {"Diesel Fuel", "Combustible dièsel"},
                {"Empty Can Of Beans", "Llauna de mongetes buida"},
                {"Empty Tuna Can", "Llauna de tonyina buida"},
                {"Explosives", "Explosius"},
                {"Fertilizer", "Adob"},
                {"Gun Powder", "Pols de pistola"},
                {"Horse Dung", "Fems de cavall"},
                {"High Quality Metal Ore", "Mineral de metall d'alta qualitat"},
                {"High Quality Metal", "Metall d'alta qualitat"},
                {"Leather", "Cuir"},
                {"Low Grade Fuel", "Combustible de baixa qualitat"},
                {"Metal Fragments", "Fragments de metall"},
                {"Metal Ore", "Mineral de metall"},
                {"Paper", "Paper"},
                {"Plant Fiber", "Fibra vegetal"},
                {"Research Paper", "Treball de recerca"},
                {"Salt Water", "Aigua salada"},
                {"Scrap", "Ferralla"},
                {"Stones", "Pedres"},
                {"Sulfur Ore", "Mineral de sofre"},
                {"Sulfur", "Sofre"},
                {"Targeting Computer", "ordinador"},
                {"Water", "Aigua"},
                {"Wolf Skull", "Crani de llop"},
                {"Wood", "Fusta"},
                {"Advanced Healing Tea", "Te curatiu avançat"},
                {"Basic Healing Tea", "Te curatiu bàsic"},
                {"Pure Healing Tea", "Te curatiu pur"},
                {"Advanced Max Health Tea", "Te avançat Max Health"},
                {"Basic Max Health Tea", "Te bàsic Max Health"},
                {"Pure Max Health Tea", "Te Pure Health Max"},
                {"Advanced Ore Tea", "Te avançat de mineral"},
                {"Basic Ore Tea", "Te bàsic de mineral"},
                {"Pure Ore Tea", "Te pur de mineral"},
                {"Advanced Rad. Removal Tea", "Rad. Avançat Te d’eliminació"},
                {"Rad. Removal Tea", "Rad. Te d’eliminació"},
                {"Pure Rad. Removal Tea", "Rad pur. Te d’eliminació"},
                {"Adv. Anti-Rad Tea", "Adv. Te anti-rad"},
                {"Anti-Rad Tea", "Te anti-rad"},
                {"Pure Anti-Rad Tea", "Te pur anti-rad"},
                {"Advanced Scrap Tea", "Te de ferralla avançat"},
                {"Basic Scrap Tea", "Te bàsic de ferralla"},
                {"Pure Scrap Tea", "Te pur de ferralla"},
                {"Advanced Wood Tea", "Tè avançat de fusta"},
                {"Basic Wood Tea", "Te bàsic de fusta"},
                {"Pure Wood Tea", "Te pur de fusta"},
                {"Anti-Radiation Pills", "Píndoles antiradiació"},
                {"Binoculars", "Prismàtics"},
                {"Timed Explosive Charge", "Càrrega explosiva temporitzada"},
                {"Camera", "Càmera"},
                {"RF Transmitter", "Transmissor de RF"},
                {"Handmade Fishing Rod", "Canya de pescar feta a mà"},
                {"Flare", "Flamarada"},
                {"Flashlight", "Llanterna"},
                {"Geiger Counter", "Comptador Geiger"},
                {"Hose Tool", "Eina de mànega"},
                {"Jackhammer", "Telledradora"},
                {"Blue Keycard", "targeta blava"},
                {"Green Keycard", "target verda"},
                {"Red Keycard", "targeta vermella"},
                {"Large Medkit", "Medkit gran"},
                {"Paper Map", "Mapa de paper"},
                {"Medical Syringe", "Xeringa mèdica"},
                {"RF Pager", "Cercador de RF"},
                {"Building Plan", "Pla constructiu"},
                {"Smoke Grenade", "Granada de fum"},
                {"Supply Signal", "Senyal de subministrament"},
                {"Survey Charge", "mina de cerca"},
                {"Wire Tool", "Eina de filferro"},
                {"Small Chassis", "Xassís petit"},
                {"Medium Chassis", "Xassís mitjà"},
                {"Large Chassis", "Xassís gran"},
                {"Cockpit Vehicle Module", "Mòdul de vehicle de la cabina"},
                {"Armored Cockpit Vehicle Module", "Mòdul de vehicle de la cabina blindada"},
                {"Cockpit With Engine Vehicle Module", "Cabina amb mòdul de vehicle de motor"},
                {"Engine Vehicle Module", "Mòdul de vehicles de motor"},
                {"Flatbed Vehicle Module", "Mòdul de vehicles de plataforma"},
                {"Armored Passenger Vehicle Module", "Mòdul blindat de vehicles de passatgers"},
                {"Rear Seats Vehicle Module", "Mòdul de vehicles de seients posteriors"},
                {"Storage Vehicle Module", "Mòdul d'emmagatzematge de vehicles"},
                {"Taxi Vehicle Module", "Mòdul de vehicles de taxi"},
                {"Large Flatbed Vehicle Module", "Mòdul de vehicles de gran pla"},
                {"Fuel Tank Vehicle Module", "Mòdul de vehicle de tanc de combustible"},
                {"Passenger Vehicle Module", "Mòdul de vehicles de passatgers"},
                {"Generic vehicle module", "Mòdul de vehicle genèric"},
                {"Telephone", "Telèfon"},
                {"16x Zoom Scope", "mira de zoom 16x"},
                {"Weapon flashlight", "Llanterna d'armes"},
                {"Holosight", "mira de punt vermell"},
                {"Weapon Lasersight", "laser d'arma"},
                {"Muzzle Boost", "Muzzle Boost"},
                {"Muzzle Brake", "Fre de boca"},
                {"Simple Handmade Sight", "Vista simple feta a mà"},
                {"Silencer", "Silenciador"},
                {"8x Zoom Scope", "mira de zoom 8x"},
                {"Assault Rifle", "Rifle d'assalt"},
                {"Bandage", "Embenatge"},
                {"Beancan Grenade", "Grenada Beancan"},
                {"Bolt Action Rifle", "Rifle Bolt Action"},
                {"Bone Club", "Bone Club"},
                {"Bone Knife", "Ganivet d’os"},
                {"Hunting Bow", "Arc de caça"},
                {"Birthday Cake", "Pastís d'aniversari"},
                {"Chainsaw", "Motoserra"},
                {"Salvaged Cleaver", "Cleaver salvat"},
                {"Compound Bow", "Arc compost"},
                {"Crossbow", "Ballesta"},
                {"Double Barrel Shotgun", "Escopeta de doble canó"},
                {"Eoka Pistol", "Pistola Eoka"},
                {"F1 Grenade", "F1 Granada"},
                {"Flame Thrower", "Llançaflames"},
                {"Multiple Grenade Launcher", "Llançador de granades múltiple"},
                {"Butcher Knife", "Ganivet carnisser"},
                {"Pitchfork", "Forquilla"},
                {"Sickle", "Falç"},
                {"Hammer", "Martell"},
                {"Hatchet", "Hacha"},
                {"Combat Knife", "Ganivet de combat"},
                {"L96 Rifle", "Fusil L96"},
                {"LR-300 Assault Rifle", "Fusil d'assalt LR-300"},
                {"M249", "M249"},
                {"M39 Rifle", "Fusil M39"},
                {"M92 Pistol", "Pistola M92"},
                {"Mace", "maza"},
                {"Machete", "Matxet"},
                {"MP5A4", "MP5A4"},
                {"Nailgun", "Pistola de claus"},
                {"Paddle", "Pàdel"},
                {"Pickaxe", "Pica"},
                {"Waterpipe Shotgun", "Escopeta Waterpipe"},
                {"Python Revolver", "Python Revolver"},
                {"Revolver", "Revòlver"},
                {"Rock", "Roca"},
                {"Rocket Launcher", "Llançacoets"},
                {"Salvaged Axe", "Destral salvat"},
                {"Salvaged Hammer", "Martell recuperat"},
                {"Salvaged Icepick", "Icepick salvat"},
                {"Satchel Charge", "Càrrec de maletes"},
                {"Pump Shotgun", "Escopeta de bombes"},
                {"Semi-Automatic Pistol", "Pistola semiautomàtica"},
                {"Semi-Automatic Rifle", "Rifle semiautomàtic"},
                {"Custom SMG", "SMG personalitzat"},
                {"Spas-12 Shotgun", "Escopeta Spas-12"},
                {"Stone Hatchet", "Hatchet de pedra"},
                {"Stone Pickaxe", "Pica de pedra"},
                {"Stone Spear", "Llança de pedra"},
                {"Longsword", "Espasa llarga"},
                {"Salvaged Sword", "Espasa salvada"},
                {"Thompson", "Thompson"},
                {"Garry's Mod Tool Gun", "Garry's Mod Tool Gun"},
                {"Torch", "Torxa"},
                {"Water Bucket", "Cub d’aigua"},
                {"Wooden Spear", "Llança de fusta"},
                {"Roadsign Horse Armor", "Blindage de Roadsign per caballs"},
                {"Wooden Horse Armor", "Armadura de cavall de fusta"},
                {"Horse Saddle", "Sella de cavall"},
                {"Saddle bag", "Bossa de sella"},
                {"High Quality Horse Shoes", "Sabates de cavall d&#39;alta qualitat"},
                {"Basic Horse Shoes", "Sabates bàsiques de cavall"},
                {"Generic vehicle chassis", "Xassís de vehicles genèric"},
            }, this, "ca"); // Catalan
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MessageShowNoEconomics", "GUIShop没有收到来自Economics插件的响应。请确保经济学插件已正确安装。"},
                {"MessageShowNoServerRewards", "GUIShop没有收到ServerRewards插件的响应。请确保ServerRewards插件已正确安装。"},
                {"MessageBought", "您已成功购买{0} x {1}。"},
                {"MessageSold", "您已成功售出{0} x {1}。"},
                {"MessageErrorCooldown", "您只能每{0}秒购买此商品。"},
                {"MessageErrorSellCooldownAmount", "您只能每{1}秒售出{0}项商品。"},
                {"MessageErrorBuyCooldownAmount", "您只能每{1}秒购买{0}项商品。"},
                {"MessageErrorBuyLimit", "您只能购买此商品的{0}。"},
                {"MessageErrorSellLimit", "您只能出售此商品的{0}。"},
                {"MessageErrorInventoryFull", "您的库存已满。"},
                {"MessageErrorInventorySlots", "您至少需要{0}个可用的广告位。"},
                {"MessageErrorNoShop", "这家商店有问题。请联系管理员。"},
                {"MessageErrorGlobalDisabled", "禁用全球商店。该服务器使用NPC供应商！"},
                {"MessageErrorNoActionShop", "此商店不允许您{0}"},
                {"MessageErrorNoActionItem", "您不允许在这里{0}"},
                {"MessageErrorItemItem", "警告：看来您拥有的这个销售商品不是有效商品！请联系管理员！"},
                {"MessageErrorItemNoValidbuy", "警告：看来这不是有效的商品，请与管理员联系！"},
                {"MessageErrorItemNoValidsell", "警告：看来这不是有效的商品，请与管理员联系！"},
                {"MessageErrorRedeemKit", "警告：为您提供此工具包时出错，请与管理员联系！"},
                {"MessageErrorBuyCmd", "无法购买此商品的多个！"},
                {"MessageErrorBuyPrice", "警告：管理员未给出购买价格，您不能购买此商品"},
                {"MessageErrorSellPrice", "警告：管理员未给出出售价格，您不能出售该物品"},
                {"MessageErrorNotEnoughMoney", "您需要{0}硬币才能购买{2}中的{1}"},
                {"MessageErrorNotEnoughMoneyCustom", "您需要{0}货币才能购买{1} x {2}"},
                {"MessageErrorNotEnoughSell", "您没有足够的此项。"},
                {"MessageErrorNotNothing", "您不能购买此商品的零。"},
                {"MessageErrorItemNoExist", "警告：您尝试购买的商品似乎不存在！请联系管理员！"},
                {"MessageErrorItemNoExistTake", "您要出售的商品目前无法出售。"},
                {"MessageErrorBuildingBlocked", "您不能在建筑物受阻区域购物。"},
                {"MessageErrorAdmin", "您没有使用此命令的正确权限。 （guishop.admin）"},
                {"MessageErrorWaitingOnDownloads", "GUIShop正在等待ImageLibrary下载完成"},
                {"BlockedMonuments", "您在纪念碑附近不能使用商店！"},
                {"MessageErrorItemNotEnabled", "店主已禁用此项目。"},
                {"MessageErrorItemNotFound", "找不到项目"},
                {"CantSellCommands", "您不能将Commands卖回商店。"},
                {"CantSellKits", "您不能将套件卖回商店。"},
                {"MessageErrorCannotSellWhileEquiped", "如果拥有此设备，则无法出售。"},
                {"MessageShopResponse", "GUIShop正在等待ImageLibrary下载完成，请等待。"},
                {"MessageNPCResponseclose", "感谢您在{0}购物，很快再来！"},
                {"MessageNPCResponseopen", "欢迎来到{0}，您想购买什么？按E键开始购物！"},
                {"Commands", "指令"},
                {"Attire", "服装"},
                {"Misc", "杂项"},
                {"Items", "物品"},
                {"Ammunition", "弹药"},
                {"Construction", "施工"},
                {"Component", "零件"},
                {"Traps", "陷阱"},
                {"Electrical", "电的"},
                {"Fun", "好玩"},
                {"Food", "餐饮"},
                {"Resources", "资源资源"},
                {"Tool", "工具"},
                {"Weapon", "武器"},
                {"Medical", "医疗类"},
                {"Minicopter", "微型直升机"},
                {"Sedan", "以来"},
                {"Airdrop Call", "空投电话"},
                {"Wolf Headdress", "狼头饰"},
                {"Fogger-3000", "福格3000"},
                {"Strobe Light", "频闪灯"},
                {"Kayak", "喜欢"},
                {"MC repair", "MC维修"},
                {"ScrapTransportHeliRepair", "报废运输直升机维修"},
                {"40mm Shotgun Round", "40毫米Shot弹枪弹"},
                {"40mm HE Grenade", "40mm HE手榴弹"},
                {"40mm Smoke Grenade", "40毫米烟雾弹"},
                {"High Velocity Arrow", "高速箭头"},
                {"Wooden Arrow", "木箭"},
                {"Bone Arrow", "骨箭"},
                {"Fire Arrow", "火箭"},
                {"Handmade Shell", "手工壳"},
                {"Nailgun Nails", "指甲枪指甲"},
                {"Pistol Bullet", "手枪子弹"},
                {"Incendiary Pistol Bullet", "燃烧手枪子弹"},
                {"HV Pistol Ammo", "高压手枪弹药"},
                {"5.56 Rifle Ammo", "5.56步枪弹药"},
                {"Explosive 5.56 Rifle Ammo", "炸药5.56步枪弹药"},
                {"Incendiary 5.56 Rifle Ammo", "燃烧弹5.56步枪弹药"},
                {"HV 5.56 Rifle Ammo", "HV 5.56步枪弹药"},
                {"Rocket", "火箭"},
                {"Incendiary Rocket", "燃烧火箭"},
                {"High Velocity Rocket", "高速火箭"},
                {"Smoke Rocket WIP!!!!", "烟火箭在制品！！！"},
                {"12 Gauge Buckshot", "12号铅球"},
                {"12 Gauge Incendiary Shell", "12号燃烧器外壳"},
                {"12 Gauge Slug", "12号弹头"},
                {"Sheet Metal Double Door", "钣金双门"},
                {"Armored Double Door", "装甲双门"},
                {"Wood Double Door", "木双门"},
                {"Sheet Metal Door", "钣金门"},
                {"Armored Door", "装甲门"},
                {"Wooden Door", "木质门"},
                {"Floor grill", "地板烤架"},
                {"Ladder Hatch", "阶梯舱口"},
                {"Floor triangle grill", "地板三角烤架"},
                {"Triangle Ladder Hatch", "三角阶梯舱口"},
                {"High External Stone Gate", "高外部石门"},
                {"High External Wooden Gate", "高外部木门"},
                {"Wooden Ladder", "木制梯子"},
                {"High External Stone Wall", "高外部石墙"},
                {"High External Wooden Wall", "高外部木墙"},
                {"Prison Cell Gate", "监狱牢房门"},
                {"Prison Cell Wall", "监狱牢房壁"},
                {"Chainlink Fence Gate", "铁链围栏门"},
                {"Chainlink Fence", "链栅栏"},
                {"Garage Door", "车库门"},
                {"Netting", "净额结算"},
                {"Shop Front", "铺面"},
                {"Metal Shop Front", "金属车间"},
                {"Metal Window Bars", "金属窗栏"},
                {"Reinforced Window Bars", "钢筋窗户栏"},
                {"Wooden Window Bars", "木制窗台"},
                {"Metal horizontal embrasure", "金属水平包围"},
                {"Metal Vertical embrasure", "金属垂直框"},
                {"Reinforced Glass Window", "钢化玻璃窗"},
                {"Wood Shutters", "木制百叶窗"},
                {"Watch Tower", "守望塔"},
                {"Diving Fins", "潜水脚蹼"},
                {"Diving Mask", "潜水面罩"},
                {"Diving Tank", "潜水罐"},
                {"Wetsuit", "潜水衣"},
                {"Frog Boots", "青蛙靴"},
                {"A Barrel Costume", "桶装"},
                {"Crate Costume", "板条箱服装"},
                {"Burlap Gloves", "粗麻布手套"},
                {"Leather Gloves", "皮手套"},
                {"Roadsign Gloves", "道路标志手套"},
                {"Tactical Gloves", "战术手套"},
                {"Ghost Costume", "鬼服装"},
                {"Mummy Suit", "木乃伊套装"},
                {"Scarecrow Suit", "稻草人西装"},
                {"Scarecrow Wrap", "稻草人包装"},
                {"Hide Halterneck", "隐藏Halterneck"},
                {"Beenie Hat", "比尼帽子"},
                {"Boonie Hat", "Boonie帽子"},
                {"Bucket Helmet", "水桶头盔"},
                {"Burlap Headwrap", "麻布头饰"},
                {"Candle Hat", "蜡烛帽"},
                {"Baseball Cap", "棒球帽"},
                {"Clatter Helmet", "拍手头盔"},
                {"Coffee Can Helmet", "咖啡罐头盔"},
                {"Bone Helmet", "骨盔"},
                {"Heavy Plate Helmet", "重型板甲头盔"},
                {"Miners Hat", "矿工帽"},
                {"Party Hat", "派对帽"},
                {"Riot Helmet", "防暴头盔"},
                {"Wood Armor Helmet", "木盔甲头盔"},
                {"Hoodie", "连帽衫"},
                {"Bone Armor", "骨甲"},
                {"Heavy Plate Jacket", "厚板外套"},
                {"Snow Jacket", "雪外套"},
                {"Jacket", "夹克"},
                {"Wood Chestplate", "木胸甲"},
                {"Improvised Balaclava", "即兴巴拉克拉法帽"},
                {"Bandana Mask", "头巾面具"},
                {"Metal Facemask", "金属面罩"},
                {"Night Vision Goggles", "夜视镜"},
                {"Burlap Trousers", "粗麻布长裤"},
                {"Heavy Plate Pants", "厚板甲长裤"},
                {"Hide Pants", "皮裤"},
                {"Road Sign Kilt", "道路标志苏格兰短裙"},
                {"Shorts", "短裤"},
                {"Wood Armor Pants", "木甲裤子"},
                {"Pants", "裤子"},
                {"Hide Poncho", "皮雨披"},
                {"Burlap Shirt", "粗麻布衬衫"},
                {"Shirt", "衬衫"},
                {"Hide Vest", "隐藏背心"},
                {"Tank Top", "背心"},
                {"Boots", "靴子"},
                {"Burlap Shoes", "麻布鞋"},
                {"Hide Boots", "隐藏靴子"},
                {"Hide Skirt", "隐藏裙子"},
                {"Bandit Guard Gear", "强盗护具"},
                {"Hazmat Suit", "危险品套装"},
                {"Scientist Suit", "科学家服"},
                {"Space Suit", "太空服"},
                {"Heavy Scientist Suit", "重科学家套装"},
                {"Longsleeve T-Shirt", "长袖T恤"},
                {"T-Shirt", "上衣"},
                {"Metal Chest Plate", "金属胸甲"},
                {"Road Sign Jacket", "道路标志外套"},
                {"Bleach", "漂白"},
                {"Duct Tape", "胶带"},
                {"Low Quality Carburetor", "劣质化油器"},
                {"Medium Quality Carburetor", "中型化油器"},
                {"High Quality Carburetor", "高品质化油器"},
                {"Low Quality Crankshaft", "低质量曲轴"},
                {"Medium Quality Crankshaft", "中质曲轴"},
                {"High Quality Crankshaft", "高品质曲轴"},
                {"Low Quality Pistons", "低质量活塞"},
                {"Medium Quality Pistons", "中等质量的活塞"},
                {"High Quality Pistons", "高品质活塞"},
                {"Low Quality Spark Plugs", "低质量火花塞"},
                {"Medium Quality Spark Plugs", "中等品质的火花塞"},
                {"High Quality Spark Plugs", "高品质火花塞"},
                {"Low Quality Valves", "低品质阀门"},
                {"Medium Quality Valves", "中品质阀门"},
                {"High Quality Valves", "高品质阀门"},
                {"Electric Fuse", "电熔丝"},
                {"Gears", "齿轮"},
                {"Glue", "胶"},
                {"Metal Blade", "金属刀片"},
                {"Metal Pipe", "金属管"},
                {"Empty Propane Tank", "空丙烷罐"},
                {"Road Signs", "路标"},
                {"Rope", "绳"},
                {"Sewing Kit", "针线包"},
                {"Sheet Metal", "钣金"},
                {"Metal Spring", "金属弹簧"},
                {"Sticks", "棍棒"},
                {"Tarp", "篷布"},
                {"Tech Trash", "科技垃圾"},
                {"Rifle Body", "步枪身"},
                {"Semi Automatic Body", "半自动车身"},
                {"SMG Body", "SMG车身"},
                {"Concrete Barricade", "混凝土路障"},
                {"Wooden Barricade Cover", "木制路障盖"},
                {"Metal Barricade", "金属路障"},
                {"Sandbag Barricade", "沙袋路障"},
                {"Stone Barricade", "石路障"},
                {"Wooden Barricade", "木制路障"},
                {"Barbed Wooden Barricade", "带刺的木路障"},
                {"Barbeque", "烧烤炉"},
                {"Snap Trap", "捕捉陷阱"},
                {"Bed", "床"},
                {"Camp Fire", "营火"},
                {"Ceiling Light", "天花灯"},
                {"Chair", "椅子"},
                {"Composter", "堆肥"},
                {"Computer Station", "电脑站"},
                {"Drop Box", "投递箱"},
                {"Elevator", "电梯"},
                {"Stone Fireplace", "石壁炉"},
                {"Blue Boomer", "蓝潮"},
                {"Champagne Boomer", "香槟潮"},
                {"Green Boomer", "绿潮"},
                {"Orange Boomer", "橙色婴儿潮"},
                {"Red Boomer", "红潮"},
                {"Violet Boomer", "紫潮"},
                {"Blue Roman Candle", "蓝色罗马蜡烛"},
                {"Green Roman Candle", "绿色罗马蜡烛"},
                {"Red Roman Candle", "红色罗马蜡烛"},
                {"Violet Roman Candle", "紫罗兰色罗马蜡烛"},
                {"White Volcano Firework", "白色火山烟花"},
                {"Red Volcano Firework", "红色火山烟花"},
                {"Violet Volcano Firework", "紫火山烟火"},
                {"Wooden Floor Spikes", "木地板钉"},
                {"Fridge", "冰箱"},
                {"Large Furnace", "大型炉"},
                {"Furnace", "炉"},
                {"Hitch & Trough", "拴住"},
                {"Hab Repair", "得到修理"},
                {"Jack O Lantern Angry", "杰克O灯笼生气"},
                {"Jack O Lantern Happy", "杰克O灯笼快乐"},
                {"Land Mine", "地雷"},
                {"Lantern", "灯笼"},
                {"Large Wood Box", "大木盒"},
                {"Water Barrel", "水桶"},
                {"Locker", "储物柜"},
                {"Mail Box", "信箱"},
                {"Mixing Table", "搅拌台"},
                {"Modular Car Lift", "组合式汽车升降机"},
                {"Pump Jack", "泵千斤顶"},
                {"Small Oil Refinery", "小型炼油厂"},
                {"Large Planter Box", "大花箱"},
                {"Small Planter Box", "小花箱"},
                {"Audio Alarm", "声音警报"},
                {"Smart Alarm", "智能警报"},
                {"Smart Switch", "智能开关"},
                {"Storage Monitor", "储存监控器"},
                {"Large Rechargable Battery", "大型可充电电池"},
                {"Medium Rechargable Battery", "中型可充电电池"},
                {"Small Rechargable Battery", "小型可充电电池"},
                {"Button", "纽扣"},
                {"Counter", "计数器"},
                {"HBHF Sensor", "HBHF传感器"},
                {"Laser Detector", "激光探测器"},
                {"Pressure Pad", "压垫"},
                {"Door Controller", "由控制器"},
                {"Electric Heater", "电子加热器"},
                {"Fluid Combiner", "流体联合"},
                {"Fluid Splitter", "流体分配器"},
                {"Fluid Switch & Pump", "流体开关"},
                {"AND Switch", "AND开关"},
                {"Blocker", "封锁者"},
                {"Electrical Branch", "电气科"},
                {"Root Combiner", "根组合器"},
                {"Memory Cell", "记忆单元"},
                {"OR Switch", "或开关"},
                {"RAND Switch", "兰德开关"},
                {"RF Broadcaster", "射频广播"},
                {"RF Receiver", "射频接收器"},
                {"XOR Switch", "异或开关"},
                {"Small Generator", "小型发电机"},
                {"Test Generator", "测试生成器"},
                {"Large Solar Panel", "大型太阳能电池板"},
                {"Igniter", "点火器"},
                {"Flasher Light", "闪光灯"},
                {"Simple Light", "简单的光"},
                {"Siren Light", "警笛灯"},
                {"Powered Water Purifier", "电动净水器"},
                {"Switch", "开关"},
                {"Splitter", "分离器"},
                {"Sprinkler", "洒水器"},
                {"Tesla Coil", "特斯拉线圈"},
                {"Timer", "计时器"},
                {"Cable Tunnel", "电缆隧道"},
                {"Water Pump", "水泵"},
                {"Mining Quarry", "采矿采石场"},
                {"Reactive Target", "反应目标"},
                {"Repair Bench", "维修台"},
                {"Research Table", "研究表"},
                {"Rug Bear Skin", "地毯熊皮"},
                {"Rug", "地毯"},
                {"Search Light", "探照灯"},
                {"Secret Lab Chair", "秘密实验室椅子"},
                {"Salvaged Shelves", "抢救的架子"},
                {"Large Banner Hanging", "大横幅吊"},
                {"Two Sided Hanging Sign", "两面吊牌"},
                {"Two Sided Ornate Hanging Sign", "两侧华丽的悬挂标志"},
                {"Landscape Picture Frame", "风景画框"},
                {"Portrait Picture Frame", "人像相框"},
                {"Tall Picture Frame", "高相框"},
                {"XL Picture Frame", "XL相框"},
                {"XXL Picture Frame", "XXL相框"},
                {"Large Banner on pole", "杆上的大横幅"},
                {"Double Sign Post", "双路标"},
                {"Single Sign Post", "单一路标"},
                {"One Sided Town Sign Post", "单面镇路标"},
                {"Two Sided Town Sign Post", "双面镇路标"},
                {"Huge Wooden Sign", "巨大的木牌"},
                {"Large Wooden Sign", "大型木牌"},
                {"Medium Wooden Sign", "中号木牌"},
                {"Small Wooden Sign", "小木牌"},
                {"Shotgun Trap", "弹枪陷阱"},
                {"Sleeping Bag", "睡袋"},
                {"Small Stash", "小藏匿处"},
                {"Spinning wheel", "纺车"},
                {"Survival Fish Trap", "生存鱼陷阱"},
                {"Table", "表"},
                {"Work Bench Level 1", "1级工作台"},
                {"Work Bench Level 2", "2级工作台"},
                {"Work Bench Level 3", "3级工作台"},
                {"Tool Cupboard", "工具柜"},
                {"Tuna Can Lamp", "金枪鱼罐头灯"},
                {"Vending Machine", "售货机"},
                {"Large Water Catcher", "大型捕水器"},
                {"Small Water Catcher", "小型捕水器"},
                {"Water Purifier", "净水器"},
                {"Wind Turbine", "风力发电机"},
                {"Wood Storage Box", "木制储物盒"},
                {"Apple", "苹果"},
                {"Rotten Apple", "烂苹果"},
                {"Black Raspberries", "黑树莓"},
                {"Blueberries", "蓝莓"},
                {"Bota Bag", "Bota包"},
                {"Cactus Flesh", "仙人掌肉"},
                {"Can of Beans", "一罐豌豆"},
                {"Can of Tuna", "金枪鱼罐头"},
                {"Chocolate Bar", "巧克力吧"},
                {"Cooked Fish", "煮熟的鱼"},
                {"Raw Fish", "生鱼"},
                {"Minnows", "now鱼"},
                {"Small Trout", "小鳟鱼"},
                {"Granola Bar", "燕麦棒"},
                {"Burnt Chicken", "烧鸡"},
                {"Cooked Chicken", "煮熟的鸡"},
                {"Raw Chicken Breast", "生鸡胸肉"},
                {"Spoiled Chicken", "被宠坏的鸡"},
                {"Burnt Deer Meat", "烧鹿肉"},
                {"Cooked Deer Meat", "熟鹿肉"},
                {"Raw Deer Meat", "生鹿肉"},
                {"Burnt Horse Meat", "烧马肉"},
                {"Cooked Horse Meat", "煮熟的马肉"},
                {"Raw Horse Meat", "生马肉"},
                {"Burnt Human Meat", "烧人肉"},
                {"Cooked Human Meat", "煮熟的人肉"},
                {"Raw Human Meat", "生肉"},
                {"Spoiled Human Meat", "变质的人类肉"},
                {"Burnt Bear Meat", "烧熊肉"},
                {"Cooked Bear Meat", "煮熊肉"},
                {"Raw Bear Meat", "生熊肉"},
                {"Burnt Wolf Meat", "烧狼肉"},
                {"Cooked Wolf Meat", "熟狼肉"},
                {"Raw Wolf Meat", "生狼肉"},
                {"Spoiled Wolf Meat", "被宠坏的狼肉"},
                {"Burnt Pork", "烧猪肉"},
                {"Cooked Pork", "熟猪肉"},
                {"Raw Pork", "生猪肉"},
                {"Mushroom", "蘑菇"},
                {"Pickles", "泡菜"},
                {"Small Water Bottle", "小水壶"},
                {"Water Jug", "水壶"},
                {"Shovel Bass", "铲低音"},
                {"Cowbell", "牛铃"},
                {"Junkyard Drum Kit", "垃圾场鼓套件"},
                {"Pan Flute", "排箫"},
                {"Acoustic Guitar", "原声吉他"},
                {"Jerry Can Guitar", "杰里·坎吉"},
                {"Wheelbarrow Piano", "独轮车钢琴"},
                {"Canbourine", "bour"},
                {"Plumber's Trumpet", "水管工的喇叭"},
                {"Sousaphone", "苏萨电话"},
                {"Xylobone", "木酮"},
                {"Car Key", "车钥匙"},
                {"Door Key", "按键"},
                {"Key Lock", "钥匙锁"},
                {"Code Lock", "密码锁"},
                {"Blueprint", "蓝图"},
                {"Chinese Lantern", "中国灯笼"},
                {"Dragon Door Knocker", "龙门环"},
                {"Dragon Mask", "龙面具"},
                {"New Year Gong", "新年锣"},
                {"Rat Mask", "老鼠面具"},
                {"Firecracker String", "鞭炮弦"},
                {"Chippy Arcade Game", "Chippy Arcade游戏"},
                {"Door Closer", "闭门器"},
                {"Bunny Ears", "兔子耳朵"},
                {"Bunny Onesie", "兔子奥妮"},
                {"Easter Door Wreath", "复活节门花环"},
                {"Egg Basket", "鸡蛋篮"},
                {"Rustigé Egg - Red", "安静的鸡蛋-红色"},
                {"Rustigé Egg - Blue", "安静的鸡蛋-蓝色"},
                {"Rustigé Egg - Purple", "安静的鸡蛋-紫色"},
                {"Rustigé Egg - Ivory", "安静的鸡蛋-象牙"},
                {"Nest Hat", "巢帽"},
                {"Bronze Egg", "青铜蛋"},
                {"Gold Egg", "金蛋"},
                {"Painted Egg", "彩绘的鸡蛋"},
                {"Silver Egg", "银蛋"},
                {"Halloween Candy", "万圣节糖果"},
                {"Large Candle Set", "大蜡烛套装"},
                {"Small Candle Set", "小蜡烛套装"},
                {"Coffin", "棺材"},
                {"Cursed Cauldron", "诅咒的大锅"},
                {"Gravestone", "墓碑"},
                {"Wooden Cross", "木制十字架"},
                {"Graveyard Fence", "墓地栅栏"},
                {"Large Loot Bag", "大号战利品袋"},
                {"Medium Loot Bag", "中号战利品袋"},
                {"Small Loot Bag", "小战利品袋"},
                {"Pumpkin Bucket", "南瓜桶"},
                {"Scarecrow", "稻草人"},
                {"Skull Spikes", "骷髅尖刺"},
                {"Skull Door Knocker", "骷髅门环"},
                {"Skull Fire Pit", "骷髅火坑"},
                {"Spider Webs", "蜘蛛网"},
                {"Spooky Speaker", "怪异的扬声器"},
                {"Surgeon Scrubs", "外科医生磨砂"},
                {"Skull Trophy", "骷髅奖杯"},
                {"Card Movember Moustache", "卡Movember胡子"},
                {"Movember Moustache", "Movember小胡子"},
                {"Note", "注意"},
                {"Human Skull", "人类头骨"},
                {"Above Ground Pool", "地上游泳池"},
                {"Beach Chair", "沙滩椅"},
                {"Beach Parasol", "海滩阳伞"},
                {"Beach Table", "沙滩桌"},
                {"Beach Towel", "沙滩巾"},
                {"Boogie Board", "布吉董事会"},
                {"Inner Tube", "内管"},
                {"Instant Camera", "即时相机"},
                {"Paddling Pool", "戏水池"},
                {"Photograph", "照片"},
                {"Landscape Photo Frame", "风景相框"},
                {"Large Photo Frame", "大相框"},
                {"Portrait Photo Frame", "人像相框"},
                {"Sunglasses", "墨镜"},
                {"Water Gun", "水枪"},
                {"Water Pistol", "水枪"},
                {"Purple Sunglasses", "紫色太阳镜"},
                {"Headset", "耳机"},
                {"Candy Cane Club", "糖果手杖俱乐部"},
                {"Christmas Lights", "圣诞灯饰"},
                {"Festive Doorway Garland", "节日门口花环"},
                {"Candy Cane", "糖果手杖"},
                {"Giant Candy Decor", "巨型糖果装饰"},
                {"Giant Lollipop Decor", "巨型棒棒糖装饰"},
                {"Pookie Bear", "精灵熊"},
                {"Deluxe Christmas Lights", "豪华圣诞灯"},
                {"Coal :(", "煤:("},
                {"Large Present", "大礼物"},
                {"Medium Present", "中礼物"},
                {"Small Present", "小礼物"},
                {"Snow Machine", "造雪机"},
                {"Snowball", "雪球"},
                {"Snowman", "雪人"},
                {"SUPER Stocking", "超级长袜"},
                {"Small Stocking", "小袜"},
                {"Reindeer Antlers", "驯鹿鹿角"},
                {"Santa Beard", "圣诞老人胡子"},
                {"Santa Hat", "圣诞老人帽"},
                {"Festive Window Garland", "节日窗花环"},
                {"Wrapped Gift", "包装礼物"},
                {"Wrapping Paper", "包装纸"},
                {"Christmas Door Wreath", "圣诞门花环"},
                {"Decorative Baubels", "装饰摆设"},
                {"Decorative Plastic Candy Canes", "装饰性塑料糖果手杖"},
                {"Decorative Gingerbread Men", "装饰姜饼人"},
                {"Tree Lights", "树灯"},
                {"Decorative Pinecones", "装饰松果"},
                {"Star Tree Topper", "星树礼帽"},
                {"Decorative Tinsel", "装饰金属丝"},
                {"Christmas Tree", "圣诞树"},
                {"Auto Turret", "自动炮塔"},
                {"Flame Turret", "火焰炮塔"},
                {"Glowing Eyes", "发光的眼睛"},
                {"SAM Ammo", "山姆弹药"},
                {"SAM Site", "SAM网站"},
                {"Black Berry", "黑浆果"},
                {"Black Berry Clone", "黑莓克隆"},
                {"Black Berry Seed", "黑莓种子"},
                {"Blue Berry", "蓝莓"},
                {"Blue Berry Clone", "蓝莓果克隆"},
                {"Blue Berry Seed", "蓝莓果种子"},
                {"Green Berry", "绿浆果"},
                {"Green Berry Clone", "绿色浆果克隆"},
                {"Green Berry Seed", "绿浆果种子"},
                {"Red Berry", "红莓"},
                {"Red Berry Clone", "红浆果克隆"},
                {"Red Berry Seed", "红浆果种子"},
                {"White Berry", "白浆果"},
                {"White Berry Clone", "白浆果克隆"},
                {"White Berry Seed", "白浆果种子"},
                {"Yellow Berry", "黄莓果"},
                {"Yellow Berry Clone", "黄莓果克隆"},
                {"Yellow Berry Seed", "黄莓果种子"},
                {"Corn", "玉米"},
                {"Corn Clone", "玉米克隆"},
                {"Corn Seed", "玉米种子"},
                {"Hemp Clone", "大麻克隆"},
                {"Hemp Seed", "大麻种子"},
                {"Potato", "土豆"},
                {"Potato Clone", "马铃薯克隆"},
                {"Potato Seed", "马铃薯种子"},
                {"Pumpkin", "南瓜"},
                {"Pumpkin Plant Clone", "南瓜植物克隆"},
                {"Pumpkin Seed", "南瓜种子"},
                {"Animal Fat", "动物脂肪"},
                {"Battery - Small", "电池-小"},
                {"Blood", "血液"},
                {"Bone Fragments", "骨碎片"},
                {"CCTV Camera", "闭路电视摄像机"},
                {"Charcoal", "木炭"},
                {"Cloth", "布"},
                {"Crude Oil", "原油"},
                {"Diesel Fuel", "柴油染料"},
                {"Empty Can Of Beans", "空罐豆"},
                {"Empty Tuna Can", "空的金枪鱼罐头"},
                {"Explosives", "炸药"},
                {"Fertilizer", "肥料"},
                {"Gun Powder", "枪粉"},
                {"Horse Dung", "马粪"},
                {"High Quality Metal Ore", "优质金属矿"},
                {"High Quality Metal", "高品质金属"},
                {"Leather", "皮革"},
                {"Low Grade Fuel", "低等级燃料"},
                {"Metal Fragments", "金属碎片"},
                {"Metal Ore", "金属矿"},
                {"Paper", "纸"},
                {"Plant Fiber", "植物纤维"},
                {"Research Paper", "研究论文"},
                {"Salt Water", "盐水"},
                {"Scrap", "废料"},
                {"Stones", "石头"},
                {"Sulfur Ore", "硫矿"},
                {"Sulfur", "硫"},
                {"Targeting Computer", "定位计算机"},
                {"Water", "水"},
                {"Wolf Skull", "狼头骨"},
                {"Wood", "木"},
                {"Advanced Healing Tea", "高级治疗茶"},
                {"Basic Healing Tea", "基本治疗茶"},
                {"Pure Healing Tea", "纯疗茶"},
                {"Advanced Max Health Tea", "先进的最大健康茶"},
                {"Basic Max Health Tea", "基本最大保健茶"},
                {"Pure Max Health Tea", "纯最大保健茶"},
                {"Advanced Ore Tea", "高级矿石茶"},
                {"Basic Ore Tea", "基本矿石茶"},
                {"Pure Ore Tea", "纯矿石茶"},
                {"Advanced Rad. Removal Tea", "高级Rad。去除茶"},
                {"Rad. Removal Tea", "拉德去除茶"},
                {"Pure Rad. Removal Tea", "纯Rad。去除茶"},
                {"Adv. Anti-Rad Tea", "进阶防茶"},
                {"Anti-Rad Tea", "防茶"},
                {"Pure Anti-Rad Tea", "纯抗鼠茶"},
                {"Advanced Scrap Tea", "先进的废茶"},
                {"Basic Scrap Tea", "基本废茶"},
                {"Pure Scrap Tea", "纯废茶"},
                {"Advanced Wood Tea", "高级木茶"},
                {"Basic Wood Tea", "基本木茶"},
                {"Pure Wood Tea", "纯木茶"},
                {"Anti-Radiation Pills", "防辐射药"},
                {"Binoculars", "望远镜"},
                {"Timed Explosive Charge", "定时炸药"},
                {"Camera", "相机"},
                {"RF Transmitter", "射频发射器"},
                {"Handmade Fishing Rod", "手工钓鱼竿"},
                {"Flare", "耀斑"},
                {"Flashlight", "手电筒"},
                {"Geiger Counter", "盖革计数器"},
                {"Hose Tool", "软管工具"},
                {"Jackhammer", "手提钻"},
                {"Blue Keycard", "蓝色钥匙卡"},
                {"Green Keycard", "绿色钥匙卡"},
                {"Red Keycard", "红色钥匙卡"},
                {"Large Medkit", "大型Medkit"},
                {"Paper Map", "纸质地图"},
                {"Medical Syringe", "医用注射器"},
                {"RF Pager", "射频寻呼机"},
                {"Building Plan", "建筑平面图"},
                {"Smoke Grenade", "烟雾弹"},
                {"Supply Signal", "供应信号"},
                {"Survey Charge", "调查费用"},
                {"Wire Tool", "线工具"},
                {"Small Chassis", "小机箱"},
                {"Medium Chassis", "中底盘"},
                {"Large Chassis", "大底盘"},
                {"Cockpit Vehicle Module", "驾驶舱车辆模块"},
                {"Armored Cockpit Vehicle Module", "装甲驾驶舱车辆模块"},
                {"Cockpit With Engine Vehicle Module", "带有发动机车辆模块的驾驶舱"},
                {"Engine Vehicle Module", "发动机车辆模块"},
                {"Flatbed Vehicle Module", "平板车模块"},
                {"Armored Passenger Vehicle Module", "装甲乘用车模块"},
                {"Rear Seats Vehicle Module", "后排座椅模块"},
                {"Storage Vehicle Module", "仓储车辆模块"},
                {"Taxi Vehicle Module", "出租车车辆模块"},
                {"Large Flatbed Vehicle Module", "大型平板车模块"},
                {"Fuel Tank Vehicle Module", "油箱车辆模块"},
                {"Passenger Vehicle Module", "乘用车模块"},
                {"Generic vehicle module", "通用车辆模块"},
                {"Telephone", "电话"},
                {"16x Zoom Scope", "16倍变焦范围"},
                {"Weapon flashlight", "武器手电"},
                {"Holosight", "全视线"},
                {"Weapon Lasersight", "武器激光视线"},
                {"Muzzle Boost", "枪口助推器"},
                {"Muzzle Brake", "枪口制动"},
                {"Simple Handmade Sight", "简单的手工瞄准具"},
                {"Silencer", "消音器"},
                {"8x Zoom Scope", "8倍变焦范围"},
                {"Assault Rifle", "突击步枪"},
                {"Bandage", "绷带"},
                {"Beancan Grenade", "Beancan格林纳达"},
                {"Bolt Action Rifle", "螺栓行动步枪"},
                {"Bone Club", "骨俱乐部"},
                {"Bone Knife", "骨刀"},
                {"Hunting Bow", "狩猎弓"},
                {"Birthday Cake", "生日蛋糕"},
                {"Chainsaw", "电锯"},
                {"Salvaged Cleaver", "抢救的切肉刀"},
                {"Compound Bow", "复合弓"},
                {"Crossbow", "弩"},
                {"Double Barrel Shotgun", "双管Shot弹枪"},
                {"Eoka Pistol", "冈田手枪"},
                {"F1 Grenade", "F1格拉纳达"},
                {"Flame Thrower", "火焰喷射器"},
                {"Multiple Grenade Launcher", "多榴弹发射器"},
                {"Butcher Knife", "屠夫刀"},
                {"Pitchfork", "叉"},
                {"Sickle", "镰刀"},
                {"Hammer", "锤子"},
                {"Hatchet", "斧头"},
                {"Combat Knife", "战斗刀"},
                {"L96 Rifle", "L96步枪"},
                {"LR-300 Assault Rifle", "LR-300突击步枪"},
                {"M249", "M249"},
                {"M39 Rifle", "M39步枪"},
                {"M92 Pistol", "M92手枪"},
                {"Mace", "猫"},
                {"Machete", "大砍刀"},
                {"MP5A4", "MP5A4"},
                {"Nailgun", "钉枪"},
                {"Paddle", "桨"},
                {"Pickaxe", "镐"},
                {"Waterpipe Shotgun", "水枪Shot弹枪"},
                {"Python Revolver", "Python左轮手枪"},
                {"Revolver", "左轮手枪"},
                {"Rock", "岩"},
                {"Rocket Launcher", "火箭发射器"},
                {"Salvaged Axe", "救助之斧"},
                {"Salvaged Hammer", "打捞锤"},
                {"Salvaged Icepick", "抢救的冰镐"},
                {"Satchel Charge", "公文包"},
                {"Pump Shotgun", "Shot弹枪"},
                {"Semi-Automatic Pistol", "半自动手枪"},
                {"Semi-Automatic Rifle", "半自动步枪"},
                {"Custom SMG", "定制SMG"},
                {"Spas-12 Shotgun", "Spas-12 Shot弹枪"},
                {"Stone Hatchet", "石斧"},
                {"Stone Pickaxe", "石镐"},
                {"Stone Spear", "石矛"},
                {"Longsword", "长剑"},
                {"Salvaged Sword", "救世剑"},
                {"Thompson", "汤普森"},
                {"Garry's Mod Tool Gun", "加里的Mod工具枪"},
                {"Torch", "火炬"},
                {"Water Bucket", "水桶"},
                {"Wooden Spear", "木矛"},
                {"Roadsign Horse Armor", "道路标志牌马装甲"},
                {"Wooden Horse Armor", "木马盔甲"},
                {"Horse Saddle", "马鞍"},
                {"Saddle bag", "马鞍包"},
                {"High Quality Horse Shoes", "高品质马鞋"},
                {"Basic Horse Shoes", "基本马鞋"},
                {"Generic vehicle chassis", "通用汽车底盘"}
            }, this, "zh-CN"); //Simplified Chinese 
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MessageShowNoEconomics", "O GUIShop não recebeu resposta do plugin Economics. Certifique-se de que o plugin Economics está instalado corretamente. "},
                {"MessageShowNoServerRewards", "GUIShop não recebeu uma resposta do plugin ServerRewards. Certifique-se de que o plugin ServerRewards esteja instalado corretamente. "},
                {"MessageBought", "Você comprou com sucesso {0} {1}."},
                {"MessageSold", "Você vendeu com sucesso {0} {1}. "},
                {"MessageErrorCooldown", "Você só pode comprar este item a cada {0} segundos."},
                {"MessageErrorSellCooldownAmount", "Você só pode vender {0} deste item a cada {1} segundos."},
                {"MessageErrorBuyCooldownAmount", "Você só pode comprar {0} deste item a cada {1} segundos."},
                {"MessageErrorBuyLimit", "Você só pode comprar {0} deste item."},
                {"MessageErrorSellLimit", "Você só pode vender {0} deste item."},
                {"MessageErrorInventoryFull", "Seu inventário está cheio."},
                {"MessageErrorInventorySlots", "Você precisa de pelo menos {0} slots de inventário gratuitos."},
                {"MessageErrorNoShop", "Há algo errado com esta loja. Entre em contato com um administrador."},
                {"MessageErrorGlobalDisabled", "Lojas globais estão desativadas. Este servidor usa fornecedores NPC!"},
                {"MessageErrorNoActionShop", "Você não tem permissão para {0} nesta loja"},
                {"MessageErrorNoActionItem", "Você não tem permissão para {0} este item aqui"},
                {"MessageErrorItemItem", "AVISO: Parece que este item de venda que você tem não é um item válido! Entre em contato com um administrador!"},
                {"MessageErrorItemNoValidbuy", "AVISO: parece que não é um item válido para comprar, entre em contato com um administrador!"},
                {"MessageErrorItemNoValidsell", "AVISO: parece que não é um item válido para vender, entre em contato com um administrador!"},
                {"MessageErrorRedeemKit", "AVISO: Ocorreu um erro ao fornecer este kit a você, entre em contato com um administrador!"},
                {"MessageErrorBuyCmd", "Não é possível comprar vários deste item!"},
                {"MessageErrorBuyPrice", "AVISO: Nenhum preço de compra foi fornecido pelo administrador, você não pode comprar este item"},
                {"MessageErrorSellPrice", "AVISO: Nenhum preço de venda foi fornecido pelo administrador, você não pode vender este item"},
                {"MessageErrorNotEnoughMoney", "Você precisa de {0} moedas para comprar {1}x {2}"},
                {"MessageErrorNotEnoughMoneyCustom", "Você precisa de {0} moeda para comprar {1}x {2}"},
                {"MessageErrorNotEnoughSell", "Você não tem o suficiente deste item."},
                {"MessageErrorNotNothing", "Você não pode comprar Zero deste item."},
                {"MessageErrorItemNoExist", "AVISO: o item que você está tentando comprar parece não existir! Entre em contato com um administrador!"},
                {"MessageErrorItemNoExistTake", "O item que você está tentando vender não pode ser vendido no momento."},
                {"MessageErrorBuildingBlocked", "Você não pode fazer compras enquanto estiver em uma área de construção bloqueada. "},
                {"MessageErrorAdmin", "Você não tem as permissões corretas para usar este comando. (guishop.admin)"},
                {"MessageErrorWaitingOnDownloads", "GUIShop está aguardando o término dos downloads da ImageLibrary"},
                {"BlockedMonuments", "Você não pode usar a loja perto de um Monumento!"},
                {"MessageErrorItemNotEnabled", "O lojista desativou este item."},
                {"MessageErrorItemNotFound", "Item não encontrado"},
                {"CantSellCommands", "Você não pode vender comandos de volta para a loja."},
                {"CantSellKits", "Você não pode vender Kits de volta para a loja."},
                {"MessageErrorCannotSellWhileEquiped", "Você não pode vender o item se o tiver equipado."},
                {"MessageShopResponse", "GUIShop está esperando o download da ImageLibrary terminar, por favor aguarde."},
                {"MessageNPCResponseclose", "Obrigado por comprar em {0} volte em breve!"},
                {"MessageNPCResponseopen", "Bem-vindo ao {0} o que você gostaria de comprar? Pressione E para começar a comprar!"},
                {"Commands", "Comandos"},
                {"Attire", "Vestuário"},
                {"Misc", "Misc"},
                {"Items", "Items"},
                {"Ammunition", "Munição"},
                {"Construction", "Construção"},
                {"Component", "Componente"},
                {"Traps", "Traps"},
                {"Electrical", "Elétrico"},
                {"Fun", "Diversão"},
                {"Food", "Comida"},
                {"Resources", "Resources"},
                {"Tool", "Ferramenta"},
                {"Weapon", "Arma"},
                {"Medical", "Médico"},
                {"Minicopter", "Minicóptero"},
                {"Sedan", "Desde a"},
                {"Airdrop Call", "Airdrop Call"},
                {"Wolf Headdress", "Cocar de lobo"},
                {"Fogger-3000", "Fogger-3000"},
                {"Strobe Light", "Luz estroboscópica"},
                {"Kayak", "Gostar"},
                {"MC repair", "Conserto de MC"},
                {"ScrapTransportHeliRepair", "ScrapTransportHeliRepair"},
                {"40mm Shotgun Round", "40mm Shotgun Round"},
                {"40mm HE Grenade", "Granada HE 40mm"},
                {"40mm Smoke Grenade", "Granada de fumaça 40mm"},
                {"High Velocity Arrow", "Seta de alta velocidade"},
                {"Wooden Arrow", "Flecha de madeira"},
                {"Bone Arrow", "Flecha de Osso"},
                {"Fire Arrow", "Flecha de fogo"},
                {"Handmade Shell", "Shell feito à mão"},
                {"Nailgun Nails", "Nailgun Nails"},
                {"Pistol Bullet", "Bala de pistola"},
                {"Incendiary Pistol Bullet", "Bala de pistola incendiária"},
                {"HV Pistol Ammo", "Munição de pistola HV"},
                {"5.56 Rifle Ammo", "5,56 Rifle Ammo"},
                {"Explosive 5.56 Rifle Ammo", "Munição de rifle explosivo 5,56"},
                {"Incendiary 5.56 Rifle Ammo", "Munição de rifle incendiário 5,56"},
                {"HV 5.56 Rifle Ammo", "HV 5.56 Rifle Ammo"},
                {"Rocket", "Foguete"},
                {"Incendiary Rocket", "Foguete incendiário"},
                {"High Velocity Rocket", "Foguete de alta velocidade"},
                {"Smoke Rocket WIP!!!!", "Smoke Rocket WIP !!!!"},
                {"12 Gauge Buckshot", "12 Gauge Buckshot"},
                {"12 Gauge Incendiary Shell", "Concha incendiária de calibre 12"},
                {"12 Gauge Slug", "12 Gauge Slug"},
                {"Sheet Metal Double Door", "Porta dupla de chapa metálica"},
                {"Armored Double Door", "Porta Dupla Blindada"},
                {"Wood Double Door", "Porta Dupla de Madeira"},
                {"Sheet Metal Door", "Porta de chapa metálica"},
                {"Armored Door", "Porta Blindada"},
                {"Wooden Door", "Porta de madeira"},
                {"Floor grill", "Grelha de chão"},
                {"Ladder Hatch", "Escada Escada"},
                {"Floor triangle grill", "Grelha triangular de chão"},
                {"Triangle Ladder Hatch", "Triangle Ladder Hatch"},
                {"High External Stone Gate", "Portão de pedra externa alta"},
                {"High External Wooden Gate", "Portão de madeira externo alto"},
                {"Wooden Ladder", "Escada de madeira"},
                {"High External Stone Wall", "Parede de pedra externa alta"},
                {"High External Wooden Wall", "Parede alta externa de madeira"},
                {"Prison Cell Gate", "Portão da Cela da Prisão"},
                {"Prison Cell Wall", "Parede da Cela da Prisão"},
                {"Chainlink Fence Gate", "Portão de cerca de arame"},
                {"Chainlink Fence", "Cerca de arame"},
                {"Garage Door", "Porta da garagem"},
                {"Netting", "Rede"},
                {"Shop Front", "Vitrine"},
                {"Metal Shop Front", "Frente de loja de metal"},
                {"Metal Window Bars", "Barras de janela de metal"},
                {"Reinforced Window Bars", "Barras de janela reforçadas"},
                {"Wooden Window Bars", "Barras de janela de madeira"},
                {"Metal horizontal embrasure", "Ameia horizontal de metal"},
                {"Metal Vertical embrasure", "Ameia vertical de metal"},
                {"Reinforced Glass Window", "Janela de vidro reforçado"},
                {"Wood Shutters", "Persianas de madeira"},
                {"Watch Tower", "Torre de Vigia"},
                {"Diving Fins", "Barbatanas de mergulho"},
                {"Diving Mask", "Máscara de mergulho"},
                {"Diving Tank", "Tanque de mergulho"},
                {"Wetsuit", "Wetsuit"},
                {"Frog Boots", "Sapo Botas"},
                {"A Barrel Costume", "Uma fantasia de barril"},
                {"Crate Costume", "Traje de caixote"},
                {"Burlap Gloves", "Luvas de aniagem"},
                {"Leather Gloves", "Luvas de couro"},
                {"Roadsign Gloves", "Luvas Roadsign"},
                {"Tactical Gloves", "Luvas Táticas"},
                {"Ghost Costume", "Fantasia de fantasma"},
                {"Mummy Suit", "Fato de múmia"},
                {"Scarecrow Suit", "Terno de Espantalho"},
                {"Scarecrow Wrap", "Envoltório de espantalho"},
                {"Hide Halterneck", "Esconder Halterneck"},
                {"Beenie Hat", "Beenie Hat"},
                {"Boonie Hat", "Chapéu Boonie"},
                {"Bucket Helmet", "Capacete de balde"},
                {"Burlap Headwrap", "Burlap Headwrap"},
                {"Candle Hat", "Chapéu de Vela"},
                {"Baseball Cap", "Boné de baseball"},
                {"Clatter Helmet", "Capacete de barulho"},
                {"Coffee Can Helmet", "Capacete de lata de café"},
                {"Bone Helmet", "Capacete ósseo"},
                {"Heavy Plate Helmet", "Capacete de Placa Pesada"},
                {"Miners Hat", "Chapéu de Mineiro"},
                {"Party Hat", "Chapéu de festa"},
                {"Riot Helmet", "Capacete Riot"},
                {"Wood Armor Helmet", "Capacete de armadura de madeira"},
                {"Hoodie", "Moletom com capuz"},
                {"Bone Armor", "Armadura óssea"},
                {"Heavy Plate Jacket", "Casaco Heavy Plate"},
                {"Snow Jacket", "Casaco de neve"},
                {"Jacket", "Jaqueta"},
                {"Wood Chestplate", "Peito de Madeira"},
                {"Improvised Balaclava", "Balaclava improvisada"},
                {"Bandana Mask", "Máscara Bandana"},
                {"Metal Facemask", "Máscara facial de metal"},
                {"Night Vision Goggles", "Óculos de visão noturna"},
                {"Burlap Trousers", "Calças de aniagem"},
                {"Heavy Plate Pants", "Calças grossas"},
                {"Hide Pants", "Calças de couro"},
                {"Road Sign Kilt", "Kilt de sinalização rodoviária"},
                {"Shorts", "Calção"},
                {"Wood Armor Pants", "Calças de armadura de madeira"},
                {"Pants", "calça"},
                {"Hide Poncho", "Esconder Poncho"},
                {"Burlap Shirt", "Camisa de serapilheira"},
                {"Shirt", "Camisa"},
                {"Hide Vest", "Esconder Colete"},
                {"Tank Top", "Regata"},
                {"Boots", "Chuteiras"},
                {"Burlap Shoes", "Sapatos de aniagem"},
                {"Hide Boots", "Esconder Botas"},
                {"Hide Skirt", "Esconder Saia"},
                {"Bandit Guard Gear", "Equipamento de guarda bandido"},
                {"Hazmat Suit", "Hazmat Suit"},
                {"Scientist Suit", "Roupa de Cientista"},
                {"Space Suit", "Traje espacial"},
                {"Heavy Scientist Suit", "Traje de cientista pesado"},
                {"Longsleeve T-Shirt", "Camiseta manga comprida"},
                {"T-Shirt", "Camiseta"},
                {"Metal Chest Plate", "Placa de baú de metal"},
                {"Road Sign Jacket", "Jaqueta de sinalização rodoviária"},
                {"Bleach", "Alvejante"},
                {"Duct Tape", "Fita adesiva"},
                {"Low Quality Carburetor", "Carburador de baixa qualidade"},
                {"Medium Quality Carburetor", "Carburador de média qualidade"},
                {"High Quality Carburetor", "Carburador de alta qualidade"},
                {"Low Quality Crankshaft", "Virabrequim de baixa qualidade"},
                {"Medium Quality Crankshaft", "Virabrequim de média qualidade"},
                {"High Quality Crankshaft", "Virabrequim de alta qualidade"},
                {"Low Quality Pistons", "Pistões de baixa qualidade"},
                {"Medium Quality Pistons", "Pistões de qualidade média"},
                {"High Quality Pistons", "Pistões de alta qualidade"},
                {"Low Quality Spark Plugs", "Velas de ignição de baixa qualidade"},
                {"Medium Quality Spark Plugs", "Velas de ignição de média qualidade"},
                {"High Quality Spark Plugs", "Velas de ignição de alta qualidade"},
                {"Low Quality Valves", "Válvulas de baixa qualidade"},
                {"Medium Quality Valves", "Válvulas de média qualidade"},
                {"High Quality Valves", "Válvulas de alta qualidade"},
                {"Electric Fuse", "Fusível Elétrico"},
                {"Gears", "Engrenagens"},
                {"Glue", "Cola"},
                {"Metal Blade", "Lâmina de metal"},
                {"Metal Pipe", "Tubo de metal"},
                {"Empty Propane Tank", "Tanque de Propano Vazio"},
                {"Road Signs", "Sinais de trânsito"},
                {"Rope", "Corda"},
                {"Sewing Kit", "Kit de costura"},
                {"Sheet Metal", "Chapa de Metal"},
                {"Metal Spring", "Metal Spring"},
                {"Sticks", "Gravetos"},
                {"Tarp", "Tarp"},
                {"Tech Trash", "Lixo Tecnológico"},
                {"Rifle Body", "Corpo de rifle"},
                {"Semi Automatic Body", "Corpo Semi Automático"},
                {"SMG Body", "Corpo SMG"},
                {"Concrete Barricade", "Barricada de Concreto"},
                {"Wooden Barricade Cover", "Tampa Barricada De Madeira"},
                {"Metal Barricade", "Barricada de Metal"},
                {"Sandbag Barricade", "Sandbag Barricade"},
                {"Stone Barricade", "Pedra Barricada"},
                {"Wooden Barricade", "Barricada de Madeira"},
                {"Barbed Wooden Barricade", "Barricada de Madeira Farpada"},
                {"Barbeque", "Churrasco"},
                {"Snap Trap", "Snap Trap"},
                {"Bed", "Cama"},
                {"Camp Fire", "Camp Fire"},
                {"Ceiling Light", "Luz de teto"},
                {"Chair", "Cadeira"},
                {"Composter", "Composter"},
                {"Computer Station", "Estação de computador"},
                {"Drop Box", "Dropbox"},
                {"Elevator", "Elevador"},
                {"Stone Fireplace", "Lareira de pedra"},
                {"Blue Boomer", "Blue Boomer"},
                {"Champagne Boomer", "Champagne Boomer"},
                {"Green Boomer", "Green Boomer"},
                {"Orange Boomer", "Orange Boomer"},
                {"Red Boomer", "Red Boomer"},
                {"Violet Boomer", "Violet Boomer"},
                {"Blue Roman Candle", "Vela Azul Romana"},
                {"Green Roman Candle", "Vela romana verde"},
                {"Red Roman Candle", "Vela romana vermelha"},
                {"Violet Roman Candle", "Vela Violeta Romana"},
                {"White Volcano Firework", "Fogos de artifício do vulcão branco"},
                {"Red Volcano Firework", "Fogo de artifício do vulcão vermelho"},
                {"Violet Volcano Firework", "Violet Volcano Fireworks"},
                {"Wooden Floor Spikes", "Espigões de piso de madeira"},
                {"Fridge", "Geladeira"},
                {"Large Furnace", "Forno Grande"},
                {"Furnace", "Forno"},
                {"Hitch & Trough", "Pegar"},
                {"Hab Repair", "Consertou"},
                {"Jack O Lantern Angry", "Jack O Lantern com raiva"},
                {"Jack O Lantern Happy", "Jack O Lantern Happy"},
                {"Land Mine", "Mina terrestre"},
                {"Lantern", "Lanterna"},
                {"Large Wood Box", "Grande caixa de madeira"},
                {"Water Barrel", "Barril de água"},
                {"Locker", "Armário"},
                {"Mail Box", "Caixa de correio"},
                {"Mixing Table", "Mesa de Mistura"},
                {"Modular Car Lift", "Elevador modular para carro"},
                {"Pump Jack", "Pump Jack"},
                {"Small Oil Refinery", "Pequena Refinaria de Petróleo"},
                {"Large Planter Box", "Grande Caixa Plantadora"},
                {"Small Planter Box", "Caixa Pequena Plantadeira"},
                {"Audio Alarm", "Alarme de Áudio"},
                {"Smart Alarm", "Alarme Inteligente"},
                {"Smart Switch", "Smart Switch"},
                {"Storage Monitor", "Monitor de Armazenamento"},
                {"Large Rechargable Battery", "Bateria recarregável grande"},
                {"Medium Rechargable Battery", "Bateria Média Recarregável"},
                {"Small Rechargable Battery", "Bateria recarregável pequena"},
                {"Button", "Botão"},
                {"Counter", "Contador"},
                {"HBHF Sensor", "Sensor HBHF"},
                {"Laser Detector", "Detector Laser"},
                {"Pressure Pad", "Almofada de pressão"},
                {"Door Controller", "Por Controlador"},
                {"Electric Heater", "Aquecedor elétrico"},
                {"Fluid Combiner", "Combine Fluido"},
                {"Fluid Splitter", "Divisor de fluido"},
                {"Fluid Switch & Pump", "Interruptor de fluido"},
                {"AND Switch", "E mudar"},
                {"Blocker", "Bloqueador"},
                {"Electrical Branch", "Ramo Elétrico"},
                {"Root Combiner", "Root Combiner"},
                {"Memory Cell", "Célula de Memória"},
                {"OR Switch", "OU Mudar"},
                {"RAND Switch", "Interruptor RAND"},
                {"RF Broadcaster", "RF Broadcaster"},
                {"RF Receiver", "Receptor RF"},
                {"XOR Switch", "Chave XOR"},
                {"Small Generator", "Gerador pequeno"},
                {"Test Generator", "Gerador de Teste"},
                {"Large Solar Panel", "Grande Painel Solar"},
                {"Igniter", "Igniter"},
                {"Flasher Light", "Luz intermitente"},
                {"Simple Light", "Luz simples"},
                {"Siren Light", "Siren Light"},
                {"Powered Water Purifier", "Purificador de água alimentado"},
                {"Switch", "Interruptor"},
                {"Splitter", "Divisor"},
                {"Sprinkler", "Sprinkler"},
                {"Tesla Coil", "Bobina de Tesla"},
                {"Timer", "Cronômetro"},
                {"Cable Tunnel", "Túnel de cabo"},
                {"Water Pump", "Bomba de água"},
                {"Mining Quarry", "Pedreira de mineração"},
                {"Reactive Target", "Alvo Reativo"},
                {"Repair Bench", "Banco de reparo"},
                {"Research Table", "Mesa de Pesquisa"},
                {"Rug Bear Skin", "Tapete de pele de urso"},
                {"Rug", "Tapete"},
                {"Search Light", "Search Light"},
                {"Secret Lab Chair", "Cadeira de laboratório secreta"},
                {"Salvaged Shelves", "Prateleiras Recuperadas"},
                {"Large Banner Hanging", "Banner grande pendurado"},
                {"Two Sided Hanging Sign", "Sinalização dupla-face suspensa"},
                {"Two Sided Ornate Hanging Sign", "Placa de suspensão ornamentada de dois lados"},
                {"Landscape Picture Frame", "Moldura para fotos em paisagem"},
                {"Portrait Picture Frame", "Porta-retratos"},
                {"Tall Picture Frame", "Moldura alta"},
                {"XL Picture Frame", "Porta-retratos XL"},
                {"XXL Picture Frame", "Porta-retratos XXL"},
                {"Large Banner on pole", "Banner grande no mastro"},
                {"Double Sign Post", "Postagem de duplo sinal"},
                {"Single Sign Post", "Postagem de Sinal Único"},
                {"One Sided Town Sign Post", "Poste de sinalização de cidade unilateral"},
                {"Two Sided Town Sign Post", "Poste de sinalização de dois lados da cidade"},
                {"Huge Wooden Sign", "Placa enorme de madeira"},
                {"Large Wooden Sign", "Placa grande de madeira"},
                {"Medium Wooden Sign", "Placa de madeira média"},
                {"Small Wooden Sign", "Pequena placa de madeira"},
                {"Shotgun Trap", "Armadilha de espingarda"},
                {"Sleeping Bag", "Saco de dormir"},
                {"Small Stash", "Pequeno estoque"},
                {"Spinning wheel", "Roda giratória"},
                {"Survival Fish Trap", "Survival Fish Trap"},
                {"Table", "Tabela"},
                {"Work Bench Level 1", "Bancada de trabalho nível 1"},
                {"Work Bench Level 2", "Bancada de trabalho nível 2"},
                {"Work Bench Level 3", "Bancada de trabalho nível 3"},
                {"Tool Cupboard", "Armário de ferramentas"},
                {"Tuna Can Lamp", "Lâmpada de lata de atum"},
                {"Vending Machine", "Maquina de vendas"},
                {"Large Water Catcher", "Grande Coletor de Água"},
                {"Small Water Catcher", "Pequeno coletor de água"},
                {"Water Purifier", "Purificador de água"},
                {"Wind Turbine", "Turbina de vento"},
                {"Wood Storage Box", "Caixa De Armazenamento De Madeira"},
                {"Apple", "maçã"},
                {"Rotten Apple", "Maçã podre"},
                {"Black Raspberries", "Framboesas pretas"},
                {"Blueberries", "Amoras"},
                {"Bota Bag", "Bota Bag"},
                {"Cactus Flesh", "Cactus Flesh"},
                {"Can of Beans", "Lata de feijão"},
                {"Can of Tuna", "Lata de atum"},
                {"Chocolate Bar", "Barra de chocolate"},
                {"Cooked Fish", "Peixe cozido"},
                {"Raw Fish", "Peixe cru"},
                {"Minnows", "Minnows"},
                {"Small Trout", "Truta pequena"},
                {"Granola Bar", "Barra de granola"},
                {"Burnt Chicken", "Frango Queimado"},
                {"Cooked Chicken", "Frango cozido"},
                {"Raw Chicken Breast", "Peito de frango cru"},
                {"Spoiled Chicken", "Frango Estragado"},
                {"Burnt Deer Meat", "Carne De Veado Queimada"},
                {"Cooked Deer Meat", "Carne De Veado Cozida"},
                {"Raw Deer Meat", "Carne Crua de Veado"},
                {"Burnt Horse Meat", "Carne De Cavalo Queimada"},
                {"Cooked Horse Meat", "Carne De Cavalo Cozida"},
                {"Raw Horse Meat", "Carne de Cavalo Crua"},
                {"Burnt Human Meat", "Carne Humana Queimada"},
                {"Cooked Human Meat", "Carne Humana Cozida"},
                {"Raw Human Meat", "Carne Humana Crua"},
                {"Spoiled Human Meat", "Carne Humana Estragada"},
                {"Burnt Bear Meat", "Carne De Urso Queimada"},
                {"Cooked Bear Meat", "Carne De Urso Cozida"},
                {"Raw Bear Meat", "Carne Crua de Urso"},
                {"Burnt Wolf Meat", "Carne De Lobo Queimada"},
                {"Cooked Wolf Meat", "Carne de Lobo Cozida"},
                {"Raw Wolf Meat", "Carne de lobo crua"},
                {"Spoiled Wolf Meat", "Carne De Lobo Estragada"},
                {"Burnt Pork", "Carne de porco queimada"},
                {"Cooked Pork", "Carne De Porco Cozida"},
                {"Raw Pork", "Carne de porco crua"},
                {"Mushroom", "Cogumelo"},
                {"Pickles", "Picles"},
                {"Small Water Bottle", "Garrafa de água pequena"},
                {"Water Jug", "Jarro de água"},
                {"Shovel Bass", "Shovel Bass"},
                {"Cowbell", "Cowbell"},
                {"Junkyard Drum Kit", "Junkyard Drum Kit"},
                {"Pan Flute", "Flauta pan"},
                {"Acoustic Guitar", "Violão"},
                {"Jerry Can Guitar", "Jerry Can Guitar"},
                {"Wheelbarrow Piano", "Piano carrinho de mão"},
                {"Canbourine", "Canbourino"},
                {"Plumber's Trumpet", "Trombeta de Encanador"},
                {"Sousaphone", "Sousafone"},
                {"Xylobone", "Xylobone"},
                {"Car Key", "Chave do carro"},
                {"Door Key", "Por chave"},
                {"Key Lock", "Key Lock"},
                {"Code Lock", "Code Lock"},
                {"Blueprint", "Blueprint"},
                {"Chinese Lantern", "Lanterna chinesa"},
                {"Dragon Door Knocker", "Dragon Door Knocker"},
                {"Dragon Mask", "Máscara de dragão"},
                {"New Year Gong", "Gongo de ano novo"},
                {"Rat Mask", "Máscara de Rato"},
                {"Firecracker String", "Corda de foguete"},
                {"Chippy Arcade Game", "Chippy Arcade Game"},
                {"Door Closer", "Fecho de porta"},
                {"Bunny Ears", "Orelhas de coelho"},
                {"Bunny Onesie", "Coelhinho Onesie"},
                {"Easter Door Wreath", "Coroa de Páscoa"},
                {"Egg Basket", "Cesta de ovos"},
                {"Rustigé Egg - Red", "Ovo Silencioso - Vermelho"},
                {"Rustigé Egg - Blue", "Ovo Silencioso - Azul"},
                {"Rustigé Egg - Purple", "Ovo Silencioso - Roxo"},
                {"Rustigé Egg - Ivory", "Ovo Silencioso - Marfim"},
                {"Nest Hat", "Chapéu Nest"},
                {"Bronze Egg", "Ovo de bronze"},
                {"Gold Egg", "Ovo de ouro"},
                {"Painted Egg", "Ovo pintado"},
                {"Silver Egg", "Ovo De Prata"},
                {"Halloween Candy", "Doces de Halloween"},
                {"Large Candle Set", "Conjunto de velas grandes"},
                {"Small Candle Set", "Conjunto de velas pequenas"},
                {"Coffin", "Caixão"},
                {"Cursed Cauldron", "Caldeirão Amaldiçoado"},
                {"Gravestone", "Lápide"},
                {"Wooden Cross", "Cruz de madeira"},
                {"Graveyard Fence", "Cerca do cemitério"},
                {"Large Loot Bag", "Saco de saque grande"},
                {"Medium Loot Bag", "Saco de saque médio"},
                {"Small Loot Bag", "Saco de saque pequeno"},
                {"Pumpkin Bucket", "Balde de abóbora"},
                {"Scarecrow", "Espantalho"},
                {"Skull Spikes", "Crânio Spikes"},
                {"Skull Door Knocker", "Batente de porta de caveira"},
                {"Skull Fire Pit", "Skull Fire Pit"},
                {"Spider Webs", "Teia de aranha"},
                {"Spooky Speaker", "Orador assustador"},
                {"Surgeon Scrubs", "Surgeon Scrubs"},
                {"Skull Trophy", "Troféu de Crânio"},
                {"Card Movember Moustache", "Cartão Movember bigode"},
                {"Movember Moustache", "Movember Mustache"},
                {"Note", "Nota"},
                {"Human Skull", "Crânio humano"},
                {"Above Ground Pool", "Piscina acima do solo"},
                {"Beach Chair", "Cadeira de praia"},
                {"Beach Parasol", "Guarda-sol de praia"},
                {"Beach Table", "Mesa de Praia"},
                {"Beach Towel", "Toalha de praia"},
                {"Boogie Board", "Boogie Board"},
                {"Inner Tube", "Tubo interno"},
                {"Instant Camera", "Câmera instantânea"},
                {"Paddling Pool", "Piscina para crianças"},
                {"Photograph", "Fotografia"},
                {"Landscape Photo Frame", "Moldura de foto de paisagem"},
                {"Large Photo Frame", "Moldura grande para fotos"},
                {"Portrait Photo Frame", "Porta-retratos"},
                {"Sunglasses", "Oculos de sol"},
                {"Water Gun", "arma De Agua"},
                {"Water Pistol", "Pistola de água"},
                {"Purple Sunglasses", "Óculos de sol roxos"},
                {"Headset", "Fone de ouvido"},
                {"Candy Cane Club", "Candy Cane Club"},
                {"Christmas Lights", "Luzes de Natal"},
                {"Festive Doorway Garland", "Festive Doorway Garland"},
                {"Candy Cane", "Candy Cane"},
                {"Giant Candy Decor", "Giant Candy Decor"},
                {"Giant Lollipop Decor", "Giant Lollipop Decor"},
                {"Pookie Bear", "Urso pookie"},
                {"Deluxe Christmas Lights", "Luzes de Natal Deluxe"},
                {"Coal :(", "Carvão :("},
                {"Large Present", "Grande Presente"},
                {"Medium Present", "Presente Médio"},
                {"Small Present", "Pequeno presente"},
                {"Snow Machine", "Máquina de neve"},
                {"Snowball", "Bola de neve"},
                {"Snowman", "Boneco de neve"},
                {"SUPER Stocking", "SUPER Stocking"},
                {"Small Stocking", "Meia pequena"},
                {"Reindeer Antlers", "Chifres de rena"},
                {"Santa Beard", "Santa Beard"},
                {"Santa Hat", "Gorro do Papai Noel"},
                {"Festive Window Garland", "Festive Window Garland"},
                {"Wrapped Gift", "Presente Embrulhado"},
                {"Wrapping Paper", "Papel de presente"},
                {"Christmas Door Wreath", "Coroa de Natal"},
                {"Decorative Baubels", "Baubels Decorativos"},
                {"Decorative Plastic Candy Canes", "Bastões De Doces Decorativos De Plástico"},
                {"Decorative Gingerbread Men", "Homens-biscoito decorativos"},
                {"Tree Lights", "Tree Lights"},
                {"Decorative Pinecones", "Pinhas decorativas"},
                {"Star Tree Topper", "Star Tree Topper"},
                {"Decorative Tinsel", "Tinsel Decorativo"},
                {"Christmas Tree", "Árvore de Natal"},
                {"Auto Turret", "Torre Automóvel"},
                {"Flame Turret", "Torre Flamejante"},
                {"Glowing Eyes", "Olhos brilhantes"},
                {"SAM Ammo", "Munição SAM"},
                {"SAM Site", "Site SAM"},
                {"Black Berry", "Amora"},
                {"Black Berry Clone", "Black Berry Clone"},
                {"Black Berry Seed", "Semente de Black Berry"},
                {"Blue Berry", "Mirtilo"},
                {"Blue Berry Clone", "Clone Blue Berry"},
                {"Blue Berry Seed", "Semente de Blue Berry"},
                {"Green Berry", "Green Berry"},
                {"Green Berry Clone", "Clone de Green Berry"},
                {"Green Berry Seed", "Semente Verde Berry"},
                {"Red Berry", "Baga vermelha"},
                {"Red Berry Clone", "Clone de Red Berry"},
                {"Red Berry Seed", "Semente de Baga Vermelha"},
                {"White Berry", "White Berry"},
                {"White Berry Clone", "Clone de bagas brancas"},
                {"White Berry Seed", "Semente De White Berry"},
                {"Yellow Berry", "Baga amarela"},
                {"Yellow Berry Clone", "Clone Yellow Berry"},
                {"Yellow Berry Seed", "Semente Amarela"},
                {"Corn", "Milho"},
                {"Corn Clone", "Clone de Milho"},
                {"Corn Seed", "Semente de milho"},
                {"Hemp Clone", "Clone de Cânhamo"},
                {"Hemp Seed", "Sementes de cânhamo"},
                {"Potato", "Batata"},
                {"Potato Clone", "Clone de batata"},
                {"Potato Seed", "Semente De Batata"},
                {"Pumpkin", "Abóbora"},
                {"Pumpkin Plant Clone", "Clone de Abóbora"},
                {"Pumpkin Seed", "Semente de abóbora"},
                {"Animal Fat", "Gordura animal"},
                {"Battery - Small", "Bateria - Pequena"},
                {"Blood", "Sangue"},
                {"Bone Fragments", "Fragmentos de Osso"},
                {"CCTV Camera", "Câmera CCTV"},
                {"Charcoal", "Carvão"},
                {"Cloth", "Pano"},
                {"Crude Oil", "Óleo cru"},
                {"Diesel Fuel", "Combustível diesel"},
                {"Empty Can Of Beans", "Lata de feijão vazia"},
                {"Empty Tuna Can", "Lata de atum vazia"},
                {"Explosives", "Explosivos"},
                {"Fertilizer", "Fertilizante"},
                {"Gun Powder", "Pólvora"},
                {"Horse Dung", "Esterco de cavalo"},
                {"High Quality Metal Ore", "Minério de metal de alta qualidade"},
                {"High Quality Metal", "Metal de alta qualidade"},
                {"Leather", "Couro"},
                {"Low Grade Fuel", "Combustível de baixo grau"},
                {"Metal Fragments", "Fragmentos de Metal"},
                {"Metal Ore", "Minério de metal"},
                {"Paper", "Papel"},
                {"Plant Fiber", "Fibra vegetal"},
                {"Research Paper", "Artigo de Pesquisa"},
                {"Salt Water", "Água salgada"},
                {"Scrap", "Sucatear"},
                {"Stones", "Pedras"},
                {"Sulfur Ore", "Minério de Enxofre"},
                {"Sulfur", "Enxofre"},
                {"Targeting Computer", "Computador de destino"},
                {"Water", "Água"},
                {"Wolf Skull", "Caveira de lobo"},
                {"Wood", "Madeira"},
                {"Advanced Healing Tea", "Chá de Cura Avançada"},
                {"Basic Healing Tea", "Chá de Cura Básica"},
                {"Pure Healing Tea", "Chá de cura pura"},
                {"Advanced Max Health Tea", "Advanced Max Health Tea"},
                {"Basic Max Health Tea", "Chá de saúde máximo básico"},
                {"Pure Max Health Tea", "Pure Max Health Tea"},
                {"Advanced Ore Tea", "Chá de minério avançado"},
                {"Basic Ore Tea", "Chá de minério básico"},
                {"Pure Ore Tea", "Chá de minério puro"},
                {"Advanced Rad. Removal Tea", "Rad avançado. Remoção de Chá"},
                {"Rad. Removal Tea", "Rad. Remoção de Chá"},
                {"Pure Rad. Removal Tea", "Pure Rad. Remoção de Chá"},
                {"Adv. Anti-Rad Tea", "Adv. Chá Anti-Rad"},
                {"Anti-Rad Tea", "Chá Anti-Rad"},
                {"Pure Anti-Rad Tea", "Chá Anti-Rad puro"},
                {"Advanced Scrap Tea", "Sucata de chá avançada"},
                {"Basic Scrap Tea", "Sucata básica de chá"},
                {"Pure Scrap Tea", "Sucata de Chá Puro"},
                {"Advanced Wood Tea", "Advanced Wood Tea"},
                {"Basic Wood Tea", "Chá de Madeira Básico"},
                {"Pure Wood Tea", "Chá de Madeira Pura"},
                {"Anti-Radiation Pills", "Pílulas anti-radiação"},
                {"Binoculars", "Binóculos"},
                {"Timed Explosive Charge", "Carga Explosiva Temporizada"},
                {"Camera", "Câmera"},
                {"RF Transmitter", "Transmissor RF"},
                {"Handmade Fishing Rod", "Vara de pesca artesanal"},
                {"Flare", "Flare"},
                {"Flashlight", "Lanterna"},
                {"Geiger Counter", "Contador Geiger"},
                {"Hose Tool", "Ferramenta de mangueira"},
                {"Jackhammer", "Jackhammer"},
                {"Blue Keycard", "Blue Keycard"},
                {"Green Keycard", "Green Keycard"},
                {"Red Keycard", "Keycard Vermelho"},
                {"Large Medkit", "Medkit Grande"},
                {"Paper Map", "Mapa de Papel"},
                {"Medical Syringe", "Seringa Médica"},
                {"RF Pager", "RF Pager"},
                {"Building Plan", "Plano de Construção"},
                {"Smoke Grenade", "Granada de fumaça"},
                {"Supply Signal", "Sinal de abastecimento"},
                {"Survey Charge", "Taxa de pesquisa"},
                {"Wire Tool", "Ferramenta de Arame"},
                {"Small Chassis", "Chassi Pequeno"},
                {"Medium Chassis", "Chassi Médio"},
                {"Large Chassis", "Chassi Grande"},
                {"Cockpit Vehicle Module", "Módulo de veículo de cabine"},
                {"Armored Cockpit Vehicle Module", "Módulo de veículo de cabine blindada"},
                {"Cockpit With Engine Vehicle Module", "Cabine do piloto com módulo de veículo com motor"},
                {"Engine Vehicle Module", "Módulo de Motor do Veículo"},
                {"Flatbed Vehicle Module", "Módulo de veículo plano"},
                {"Armored Passenger Vehicle Module", "Módulo de veículo blindado de passageiros"},
                {"Rear Seats Vehicle Module", "Módulo de veículos de bancos traseiros"},
                {"Storage Vehicle Module", "Módulo de veículo de armazenamento"},
                {"Taxi Vehicle Module", "Módulo de veículo táxi"},
                {"Large Flatbed Vehicle Module", "Módulo de mesa grande para veículos"},
                {"Fuel Tank Vehicle Module", "Módulo de tanque de combustível para veículo"},
                {"Passenger Vehicle Module", "Módulo de veículo de passageiros"},
                {"Generic vehicle module", "Módulo de veículo genérico"},
                {"Telephone", "Telefone"},
                {"16x Zoom Scope", "Âmbito de zoom 16x"},
                {"Weapon flashlight", "Lanterna arma"},
                {"Holosight", "Holosight"},
                {"Weapon Lasersight", "Arma Lasersight"},
                {"Muzzle Boost", "Aumento de focinho"},
                {"Muzzle Brake", "Fucinho feio"},
                {"Simple Handmade Sight", "Visão simples feita à mão"},
                {"Silencer", "Silenciador"},
                {"8x Zoom Scope", "Âmbito de zoom 8x"},
                {"Assault Rifle", "Rifle de assalto"},
                {"Bandage", "Curativo"},
                {"Beancan Grenade", "Beancan Grenada"},
                {"Bolt Action Rifle", "Rifle de Bolt Action"},
                {"Bone Club", "Bone Club"},
                {"Bone Knife", "Faca de osso"},
                {"Hunting Bow", "Arco de caça"},
                {"Birthday Cake", "Bolo de aniversário"},
                {"Chainsaw", "Motosserra"},
                {"Salvaged Cleaver", "Cutelo resgatado"},
                {"Compound Bow", "Arco composto"},
                {"Crossbow", "Besta"},
                {"Double Barrel Shotgun", "Espingarda de cano duplo"},
                {"Eoka Pistol", "Pistola Eoka"},
                {"F1 Grenade", "F1 Granada"},
                {"Flame Thrower", "Lançador de chamas"},
                {"Multiple Grenade Launcher", "Lançador de granadas múltiplas"},
                {"Butcher Knife", "Faca de açougueiro"},
                {"Pitchfork", "Pitchfork"},
                {"Sickle", "Foice"},
                {"Hammer", "Martelo"},
                {"Hatchet", "Machadinha"},
                {"Combat Knife", "Faca de combate"},
                {"L96 Rifle", "Rifle L96"},
                {"LR-300 Assault Rifle", "Rifle de assalto LR-300"},
                {"M249", "M249"},
                {"M39 Rifle", "Rifle M39"},
                {"M92 Pistol", "Pistola M92"},
                {"Mace", "gato"},
                {"Machete", "Machete"},
                {"MP5A4", "MP5A4"},
                {"Nailgun", "Pistola de pregos"},
                {"Paddle", "Remo"},
                {"Pickaxe", "Picareta"},
                {"Waterpipe Shotgun", "Waterpipe Shotgun"},
                {"Python Revolver", "Python Revolver"},
                {"Revolver", "Revólver"},
                {"Rock", "Rocha"},
                {"Rocket Launcher", "Lançador de foguetes"},
                {"Salvaged Axe", "Machado Recuperado"},
                {"Salvaged Hammer", "Martelo Resgatado"},
                {"Salvaged Icepick", "Icepick resgatado"},
                {"Satchel Charge", "Satchel Charge"},
                {"Pump Shotgun", "Pump Shotgun"},
                {"Semi-Automatic Pistol", "Pistola Semiautomática"},
                {"Semi-Automatic Rifle", "Rifle Semiautomático"},
                {"Custom SMG", "SMG personalizado"},
                {"Spas-12 Shotgun", "Spas-12 Shotgun"},
                {"Stone Hatchet", "Machadinha de Pedra"},
                {"Stone Pickaxe", "Stone Pickaxe"},
                {"Stone Spear", "Lança de pedra"},
                {"Longsword", "Espada longa"},
                {"Salvaged Sword", "Espada Resgatada"},
                {"Thompson", "Thompson"},
                {"Garry's Mod Tool Gun", "Garry's Mod Tool Gun"},
                {"Torch", "Tocha"},
                {"Water Bucket", "Balde de água"},
                {"Wooden Spear", "Lança de madeira"},
                {"Roadsign Horse Armor", "Roadsign Horse Armor"},
                {"Wooden Horse Armor", "Armadura de Cavalo de Madeira"},
                {"Horse Saddle", "Horse Saddle"},
                {"Saddle bag", "Sela"},
                {"High Quality Horse Shoes", "Sapatos de cavalo de alta qualidade"},
                {"Basic Horse Shoes", "Sapatos básicos de cavalo"},
                {"Generic vehicle chassis", "Chassi de veículo genérico"}
            }, this, "pt-BR"); //Portuguese Brazil
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MessageShowNoEconomics", "GUIShop hat keine Antwort vom Economics-Plugin erhalten. Bitte stellen Sie sicher, dass das Economics-Plugin korrekt installiert ist."},
                {"MessageShowNoServerRewards", "GUIShop hat keine Antwort vom ServerRewards-Plugin erhalten. Bitte stellen Sie sicher, dass das ServerRewards-Plugin korrekt installiert ist."},
                {"MessageBought", "Sie haben {0} x {1} erfolgreich gekauft."},
                {"MessageSold", "Sie haben {0} x {1} erfolgreich verkauft."},
                {"MessageErrorCooldown", "Sie können diesen Artikel nur alle {0} Sekunden kaufen."},
                {"MessageErrorSellCooldownAmount", "Sie können nur {0} dieses Artikels alle {1} Sekunden verkaufen."},
                {"MessageErrorBuyCooldownAmount", "Sie können nur {0} dieses Artikels alle {1} Sekunden kaufen."},
                {"MessageErrorBuyLimit", "Sie können nur {0} dieses Artikels kaufen."},
                {"MessageErrorSellLimit", "Sie können nur {0} dieses Artikels verkaufen."},
                {"MessageErrorInventoryFull", "Ihr Inventar ist voll."},
                {"MessageErrorInventorySlots", "Sie benötigen mindestens {0} freie Inventarplätze."},
                {"MessageErrorNoShop", "Mit diesem Shop stimmt etwas nicht. Bitte wenden Sie sich an einen Administrator."},
                {"MessageErrorGlobalDisabled", "Globale Shops sind deaktiviert. Dieser Server verwendet NPC-Anbieter!"},
                {"MessageErrorNoActionShop", "Sie dürfen in diesem Shop nicht {0}"},
                {"MessageErrorNoActionItem", "Sie dürfen dieses Element hier nicht {0}"},
                {"MessageErrorItemItem", "WARNUNG: Es scheint, dass dieser Verkaufsartikel, den Sie haben, kein gültiger Artikel ist! Bitte wenden Sie sich an einen Administrator!"},
                {"MessageErrorItemNoValidbuy", "WARNUNG: Es scheint, als wäre es kein gültiger Artikel zum Kaufen. Bitte wenden Sie sich an einen Administrator!"},
                {"MessageErrorItemNoValidsell", "WARNUNG: Es scheint, dass es sich nicht um einen gültigen Artikel handelt. Bitte wenden Sie sich an einen Administrator."},
                {"MessageErrorRedeemKit", "WARNUNG: Bei der Bereitstellung dieses Kits ist ein Fehler aufgetreten. Bitte wenden Sie sich an einen Administrator."},
                {"MessageErrorBuyCmd", "Mehrere Artikel können nicht gekauft werden!"},
                {"MessageErrorBuyPrice", "WARNUNG: Der Administrator hat keinen Kaufpreis angegeben. Sie können diesen Artikel nicht kaufen"},
                {"MessageErrorSellPrice", "WARNUNG: Der Administrator hat keinen Verkaufspreis angegeben. Sie können diesen Artikel nicht verkaufen"},
                {"MessageErrorNotEnoughMoney", "Sie benötigen {0} Münzen, um {1} von {2} zu kaufen."},
                {"MessageErrorNotEnoughMoneyCustom", "Sie benötigen {0} Währung, um {1} x {2} zu kaufen."},
                {"MessageErrorNotEnoughSell", "Sie haben nicht genug von diesem Artikel."},
                {"MessageErrorNotNothing", "Sie können Zero von diesem Artikel nicht kaufen."},
                {"MessageErrorItemNoExist", "WARNUNG: Der Artikel, den Sie kaufen möchten, scheint nicht zu existieren! Bitte wenden Sie sich an einen Administrator!"},
                {"MessageErrorItemNoExistTake", "Der Artikel, den Sie verkaufen möchten, ist derzeit nicht verkaufbar."},
                {"MessageErrorBuildingBlocked", "Sie können nicht in einem blockierten Bereich einkaufen."},
                {"MessageErrorAdmin", "Sie haben nicht die richtigen Berechtigungen, um diesen Befehl zu verwenden. (guishop.admin)"},
                {"MessageErrorWaitingOnDownloads", "GUIShop wartet darauf, dass die ImageLibrary-Downloads abgeschlossen sind"},
                {"BlockedMonuments", "Sie dürfen den Shop nicht in der Nähe eines Denkmals benutzen!"},
                {"MessageErrorItemNotEnabled", "Der Ladenbesitzer hat diesen Artikel deaktiviert."},
                {"MessageErrorItemNotFound", "Element wurde nicht gefunden"},
                {"CantSellCommands", "Sie können keine Befehle an den Shop zurückverkaufen."},
                {"CantSellKits", "Sie können Kits nicht an den Shop zurückverkaufen."},
                {"MessageErrorCannotSellWhileEquiped", "Sie können den Artikel nicht verkaufen, wenn Sie über Equipt verfügen."},
                {"MessageShopResponse", "GUIShop wartet auf den Abschluss der ImageLibrary-Downloads. Bitte warten Sie."},
                {"MessageNPCResponseclose", "Vielen Dank für Ihren Einkauf bei {0}. Kommen Sie bald wieder!"},
                {"MessageNPCResponseopen", "Willkommen bei der {0}, was möchten Sie kaufen? Drücken Sie E, um mit dem Einkaufen zu beginnen!"},
                {"Commands", "Befehle"},
                {"Attire", "Kleidung"},
                {"Misc", "Sonstiges"},
                {"Items", "Artikel"},
                {"Ammunition", "Munition"},
                {"Construction", "Konstruktion"},
                {"Component", "Komponente"},
                {"Traps", "Fallen"},
                {"Electrical", "Elektrisch"},
                {"Fun", "Spaß"},
                {"Food", "Essen"},
                {"Resources", "Ressourcen"},
                {"Tool", "Werkzeug"},
                {"Weapon", "Waffe"},
                {"Medical", "Medizinisch"},
                {"Minicopter", "Minikopter"},
                {"Sedan", "Schon seit"},
                {"Airdrop Call", "Airdrop Call"},
                {"Wolf Headdress", "Wolf Kopfschmuck"},
                {"Fogger-3000", "Fogger-3000"},
                {"Strobe Light", "Blitzlicht"},
                {"Kayak", "Mögen"},
                {"MC repair", "MC Reparatur"},
                {"ScrapTransportHeliRepair", "ScrapTransportHeliRepair"},
                {"40mm Shotgun Round", "40mm Schrotflinte rund"},
                {"40mm HE Grenade", "40mm HE Granate"},
                {"40mm Smoke Grenade", "40mm Rauchgranate"},
                {"High Velocity Arrow", "Hochgeschwindigkeitspfeil"},
                {"Wooden Arrow", "Holzpfeil"},
                {"Bone Arrow", "Knochenpfeil"},
                {"Fire Arrow", "Feuerpfeil"},
                {"Handmade Shell", "Handgemachte Muschel"},
                {"Nailgun Nails", "Nagelpistole Nägel"},
                {"Pistol Bullet", "Pistolengeschoss"},
                {"Incendiary Pistol Bullet", "Brandpistole Kugel"},
                {"HV Pistol Ammo", "HV Pistolenmunition"},
                {"5.56 Rifle Ammo", "5.56 Gewehrmunition"},
                {"Explosive 5.56 Rifle Ammo", "Explosive 5.56 Gewehrmunition"},
                {"Incendiary 5.56 Rifle Ammo", "Brand 5.56 Gewehrmunition"},
                {"HV 5.56 Rifle Ammo", "HV 5.56 Gewehrmunition"},
                {"Rocket", "Rakete"},
                {"Incendiary Rocket", "Brandrakete"},
                {"High Velocity Rocket", "Hochgeschwindigkeitsrakete"},
                {"Smoke Rocket WIP!!!!", "Rauchrakete WIP !!!!"},
                {"12 Gauge Buckshot", "12 Gauge Buckshot"},
                {"12 Gauge Incendiary Shell", "12 Gauge Brandschale"},
                {"12 Gauge Slug", "12 Gauge Slug"},
                {"Sheet Metal Double Door", "Blech Doppeltür"},
                {"Armored Double Door", "Gepanzerte Doppeltür"},
                {"Wood Double Door", "Holz Doppeltür"},
                {"Sheet Metal Door", "Blechtür"},
                {"Armored Door", "Gepanzerte Tür"},
                {"Wooden Door", "Hölzerne Tür"},
                {"Floor grill", "Bodengrill"},
                {"Ladder Hatch", "Leiter Luke"},
                {"Floor triangle grill", "Bodendreieckgrill"},
                {"Triangle Ladder Hatch", "Dreieck Leiter Luke"},
                {"High External Stone Gate", "Hohes externes Steintor"},
                {"High External Wooden Gate", "Hohes externes Holztor"},
                {"Wooden Ladder", "Holzleiter"},
                {"High External Stone Wall", "Hohe äußere Steinmauer"},
                {"High External Wooden Wall", "Hohe äußere Holzwand"},
                {"Prison Cell Gate", "Gefängniszellentor"},
                {"Prison Cell Wall", "Gefängniszellenwand"},
                {"Chainlink Fence Gate", "Maschendrahtzaun Tor"},
                {"Chainlink Fence", "Maschendrahtzaun"},
                {"Garage Door", "Garagentor"},
                {"Netting", "Netting"},
                {"Shop Front", "Schaufenster"},
                {"Metal Shop Front", "Metallgeschäftsfront"},
                {"Metal Window Bars", "Metallfenstergitter"},
                {"Reinforced Window Bars", "Verstärkte Fensterstangen"},
                {"Wooden Window Bars", "Fensterläden aus Holz"},
                {"Metal horizontal embrasure", "Horizontale Metallbeschichtung"},
                {"Metal Vertical embrasure", "Vertikale Metallbeschichtung"},
                {"Reinforced Glass Window", "Fenster aus verstärktem Glas"},
                {"Wood Shutters", "Holzläden"},
                {"Watch Tower", "Wachturm"},
                {"Diving Fins", "Taucherflossen"},
                {"Diving Mask", "Tauchermaske"},
                {"Diving Tank", "Tauchpanzer"},
                {"Wetsuit", "Neoprenanzug"},
                {"Frog Boots", "Froschstiefel"},
                {"A Barrel Costume", "Ein Fasskostüm"},
                {"Crate Costume", "Kistenkostüm"},
                {"Burlap Gloves", "Sackleinenhandschuhe"},
                {"Leather Gloves", "Lederhandschuhe"},
                {"Roadsign Gloves", "Roadsign Handschuhe"},
                {"Tactical Gloves", "Taktische Handschuhe"},
                {"Ghost Costume", "Geisterkostüm"},
                {"Mummy Suit", "Mumienanzug"},
                {"Scarecrow Suit", "Vogelscheuche Anzug"},
                {"Scarecrow Wrap", "Vogelscheuche Wrap"},
                {"Hide Halterneck", "Halfterneck verstecken"},
                {"Beenie Hat", "Beenie Hut"},
                {"Boonie Hat", "Boonie Hut"},
                {"Bucket Helmet", "Eimerhelm"},
                {"Burlap Headwrap", "Sackleinen Headwrap"},
                {"Candle Hat", "Kerzenhut"},
                {"Baseball Cap", "Baseball Kappe"},
                {"Clatter Helmet", "Klapphelm"},
                {"Coffee Can Helmet", "Kaffeedosenhelm"},
                {"Bone Helmet", "Knochenhelm"},
                {"Heavy Plate Helmet", "Schwerer Plattenhelm"},
                {"Miners Hat", "Bergmannshut"},
                {"Party Hat", "Partyhut"},
                {"Riot Helmet", "Bereitschaftshelm"},
                {"Wood Armor Helmet", "Holzpanzerhelm"},
                {"Hoodie", "Kapuzenpullover"},
                {"Bone Armor", "Knochenrüstung"},
                {"Heavy Plate Jacket", "Schwere Plattenjacke"},
                {"Snow Jacket", "Schneejacke"},
                {"Jacket", "Jacke"},
                {"Wood Chestplate", "Holz Brustplatte"},
                {"Improvised Balaclava", "Improvisierte Sturmhaube"},
                {"Bandana Mask", "Bandana Maske"},
                {"Metal Facemask", "Metall-Gesichtsmaske"},
                {"Night Vision Goggles", "Nachtsichtbrille"},
                {"Burlap Trousers", "Sackleinenhose"},
                {"Heavy Plate Pants", "Schwere Plattenhose"},
                {"Hide Pants", "Hosen verstecken"},
                {"Road Sign Kilt", "Verkehrszeichen Kilt"},
                {"Shorts", "Kurze Hose"},
                {"Wood Armor Pants", "Holzpanzerhose"},
                {"Pants", "Hose"},
                {"Hide Poncho", "Poncho verstecken"},
                {"Burlap Shirt", "Sackleinenhemd"},
                {"Shirt", "Hemd"},
                {"Hide Vest", "Weste verstecken"},
                {"Tank Top", "Muskelshirt"},
                {"Boots", "Stiefel"},
                {"Burlap Shoes", "Sackleinen Schuhe"},
                {"Hide Boots", "Stiefel verstecken"},
                {"Hide Skirt", "Rock verstecken"},
                {"Bandit Guard Gear", "Banditenschutzausrüstung"},
                {"Hazmat Suit", "Hazmat Anzug"},
                {"Scientist Suit", "Wissenschaftler Anzug"},
                {"Space Suit", "Raumanzug"},
                {"Heavy Scientist Suit", "Schwerer Wissenschaftleranzug"},
                {"Longsleeve T-Shirt", "Langarm T-Shirt"},
                {"T-Shirt", "T-Shirt"},
                {"Metal Chest Plate", "Metall Brustplatte"},
                {"Road Sign Jacket", "Verkehrszeichen Jacke"},
                {"Bleach", "Bleichen"},
                {"Duct Tape", "Klebeband"},
                {"Low Quality Carburetor", "Vergaser von geringer Qualität"},
                {"Medium Quality Carburetor", "Vergaser mittlerer Qualität"},
                {"High Quality Carburetor", "Hochwertiger Vergaser"},
                {"Low Quality Crankshaft", "Kurbelwelle von geringer Qualität"},
                {"Medium Quality Crankshaft", "Kurbelwelle mittlerer Qualität"},
                {"High Quality Crankshaft", "Hochwertige Kurbelwelle"},
                {"Low Quality Pistons", "Kolben von geringer Qualität"},
                {"Medium Quality Pistons", "Kolben mittlerer Qualität"},
                {"High Quality Pistons", "Hochwertige Kolben"},
                {"Low Quality Spark Plugs", "Zündkerzen von geringer Qualität"},
                {"Medium Quality Spark Plugs", "Zündkerzen mittlerer Qualität"},
                {"High Quality Spark Plugs", "Hochwertige Zündkerzen"},
                {"Low Quality Valves", "Ventile von geringer Qualität"},
                {"Medium Quality Valves", "Ventile mittlerer Qualität"},
                {"High Quality Valves", "Hochwertige Ventile"},
                {"Electric Fuse", "Elektrische Sicherung"},
                {"Gears", "Getriebe"},
                {"Glue", "Kleben"},
                {"Metal Blade", "Metall Klinge"},
                {"Metal Pipe", "Metallrohr"},
                {"Empty Propane Tank", "Propantank leeren"},
                {"Road Signs", "Straßenschilder"},
                {"Rope", "Seil"},
                {"Sewing Kit", "Flickzeug"},
                {"Sheet Metal", "Blech"},
                {"Metal Spring", "Metallfeder"},
                {"Sticks", "Sticks"},
                {"Tarp", "Plane"},
                {"Tech Trash", "Technischer Müll"},
                {"Rifle Body", "Gewehrkörper"},
                {"Semi Automatic Body", "Halbautomatischer Körper"},
                {"SMG Body", "SMG Körper"},
                {"Concrete Barricade", "Betonbarrikade"},
                {"Wooden Barricade Cover", "Holz Barrikade Abdeckung"},
                {"Metal Barricade", "Metallbarrikade"},
                {"Sandbag Barricade", "Sandsack Barrikade"},
                {"Stone Barricade", "Steinbarrikade"},
                {"Wooden Barricade", "Holzbarrikade"},
                {"Barbed Wooden Barricade", "Stacheldraht-Holzbarrikade"},
                {"Barbeque", "Grillen"},
                {"Snap Trap", "Fangfalle"},
                {"Bed", "Bett"},
                {"Camp Fire", "Lagerfeuer"},
                {"Ceiling Light", "Deckenleuchte"},
                {"Chair", "Stuhl"},
                {"Composter", "Komposter"},
                {"Computer Station", "Computerstation"},
                {"Drop Box", "Dropbox"},
                {"Elevator", "Aufzug"},
                {"Stone Fireplace", "Steinkamin"},
                {"Blue Boomer", "Blauer Boomer"},
                {"Champagne Boomer", "Champagner Boomer"},
                {"Green Boomer", "Grüner Boomer"},
                {"Orange Boomer", "Orange Boomer"},
                {"Red Boomer", "Roter Boomer"},
                {"Violet Boomer", "Violetter Boomer"},
                {"Blue Roman Candle", "Blaue römische Kerze"},
                {"Green Roman Candle", "Grüne römische Kerze"},
                {"Red Roman Candle", "Rote römische Kerze"},
                {"Violet Roman Candle", "Violette römische Kerze"},
                {"White Volcano Firework", "Weißes Vulkanfeuerwerk"},
                {"Red Volcano Firework", "Feuerwerk des roten Vulkans"},
                {"Violet Volcano Firework", "Violettes Vulkanfeuerwerk"},
                {"Wooden Floor Spikes", "Holzbodenspikes"},
                {"Fridge", "Kühlschrank"},
                {"Large Furnace", "Großer Ofen"},
                {"Furnace", "Ofen"},
                {"Hitch & Trough", "Hitch"},
                {"Hab Repair", "Hab Repair"},
                {"Jack O Lantern Angry", "Jack O Laterne wütend"},
                {"Jack O Lantern Happy", "Jack O Laterne glücklich"},
                {"Land Mine", "Landmine"},
                {"Lantern", "Laterne"},
                {"Large Wood Box", "Große Holzkiste"},
                {"Water Barrel", "Wasserfass"},
                {"Locker", "Schließfach"},
                {"Mail Box", "Briefkasten"},
                {"Mixing Table", "Mischtisch"},
                {"Modular Car Lift", "Modularer Autolift"},
                {"Pump Jack", "Pumpenheber"},
                {"Small Oil Refinery", "Kleine Ölraffinerie"},
                {"Large Planter Box", "Große Pflanzkiste"},
                {"Small Planter Box", "Kleine Pflanzkiste"},
                {"Audio Alarm", "Audio-Alarm"},
                {"Smart Alarm", "Intelligenter Alarm"},
                {"Smart Switch", "Smart Switch"},
                {"Storage Monitor", "Speichermonitor"},
                {"Large Rechargable Battery", "Großer Akku"},
                {"Medium Rechargable Battery", "Mittlerer Akku"},
                {"Small Rechargable Battery", "Kleiner Akku"},
                {"Button", "Taste"},
                {"Counter", "Zähler"},
                {"HBHF Sensor", "HBHF-Sensor"},
                {"Laser Detector", "Laserdetektor"},
                {"Pressure Pad", "Druckplatte"},
                {"Door Controller", "Vom Controller"},
                {"Electric Heater", "Elektrische Heizung"},
                {"Fluid Combiner", "Fluid Combine"},
                {"Fluid Splitter", "Flüssigkeitssplitter"},
                {"Fluid Switch & Pump", "Flüssigkeitsschalter"},
                {"AND Switch", "UND-Schalter"},
                {"Blocker", "Blocker"},
                {"Electrical Branch", "Elektrischer Zweig"},
                {"Root Combiner", "Wurzelkombinierer"},
                {"Memory Cell", "Speicherzelle"},
                {"OR Switch", "ODER Schalter"},
                {"RAND Switch", "RAND-Schalter"},
                {"RF Broadcaster", "RF Broadcaster"},
                {"RF Receiver", "RF-Empfänger"},
                {"XOR Switch", "XOR-Schalter"},
                {"Small Generator", "Kleiner Generator"},
                {"Test Generator", "Generator testen"},
                {"Large Solar Panel", "Großes Solarpanel"},
                {"Igniter", "Zünder"},
                {"Flasher Light", "Blinklicht"},
                {"Simple Light", "Einfaches Licht"},
                {"Siren Light", "Sirenenlicht"},
                {"Powered Water Purifier", "Angetriebener Wasseraufbereiter"},
                {"Switch", "Schalter"},
                {"Splitter", "Splitter"},
                {"Sprinkler", "Sprinkler"},
                {"Tesla Coil", "Tesla-Spule"},
                {"Timer", "Timer"},
                {"Cable Tunnel", "Kabeltunnel"},
                {"Water Pump", "Wasserpumpe"},
                {"Mining Quarry", "Bergbau Steinbruch"},
                {"Reactive Target", "Reaktives Ziel"},
                {"Repair Bench", "Reparaturbank"},
                {"Research Table", "Forschungstabelle"},
                {"Rug Bear Skin", "Teppich Bärenhaut"},
                {"Rug", "Teppich"},
                {"Search Light", "Suchlicht"},
                {"Secret Lab Chair", "Geheimer Laborstuhl"},
                {"Salvaged Shelves", "Geborgene Regale"},
                {"Large Banner Hanging", "Großes Banner hängen"},
                {"Two Sided Hanging Sign", "Zweiseitiges hängendes Schild"},
                {"Two Sided Ornate Hanging Sign", "Zweiseitiges verziertes hängendes Zeichen"},
                {"Landscape Picture Frame", "Landschaftsbilderrahmen"},
                {"Portrait Picture Frame", "Porträt Bilderrahmen"},
                {"Tall Picture Frame", "Hoher Bilderrahmen"},
                {"XL Picture Frame", "XL Bilderrahmen"},
                {"XXL Picture Frame", "XXL Bilderrahmen"},
                {"Large Banner on pole", "Großes Banner auf Stange"},
                {"Double Sign Post", "Doppelter Wegweiser"},
                {"Single Sign Post", "Single Sign Post"},
                {"One Sided Town Sign Post", "Einseitiger Wegweiser der Stadt"},
                {"Two Sided Town Sign Post", "Zweiseitiger Stadtschild"},
                {"Huge Wooden Sign", "Riesiges Holzschild"},
                {"Large Wooden Sign", "Großes Holzschild"},
                {"Medium Wooden Sign", "Mittleres Holzschild"},
                {"Small Wooden Sign", "Kleines Holzschild"},
                {"Shotgun Trap", "Schrotflintenfalle"},
                {"Sleeping Bag", "Schlafsack"},
                {"Small Stash", "Kleiner Vorrat"},
                {"Spinning wheel", "Drehendes Rad"},
                {"Survival Fish Trap", "Überlebensfischfalle"},
                {"Table", "Tabelle"},
                {"Work Bench Level 1", "Werkbank Level 1"},
                {"Work Bench Level 2", "Werkbank Level 2"},
                {"Work Bench Level 3", "Werkbank Level 3"},
                {"Tool Cupboard", "Werkzeugschrank"},
                {"Tuna Can Lamp", "Thunfisch kann Lampe"},
                {"Vending Machine", "Verkaufsautomat"},
                {"Large Water Catcher", "Großer Wasserfänger"},
                {"Small Water Catcher", "Kleiner Wasserfänger"},
                {"Water Purifier", "Wasserreiniger"},
                {"Wind Turbine", "Windkraftanlage"},
                {"Wood Storage Box", "Holz Aufbewahrungsbox"},
                {"Apple", "Apfel"},
                {"Rotten Apple", "Verdorbener Apfel"},
                {"Black Raspberries", "Schwarze Himbeeren"},
                {"Blueberries", "Blaubeeren"},
                {"Bota Bag", "Bota Tasche"},
                {"Cactus Flesh", "Kaktusfleisch"},
                {"Can of Beans", "Dose Bohnen"},
                {"Can of Tuna", "Thunfischdose"},
                {"Chocolate Bar", "Schokoladentafel"},
                {"Cooked Fish", "Gekochter Fisch"},
                {"Raw Fish", "Roher Fisch"},
                {"Minnows", "Minnows"},
                {"Small Trout", "Kleine Forelle"},
                {"Granola Bar", "Müsliriegel"},
                {"Burnt Chicken", "Verbranntes Huhn"},
                {"Cooked Chicken", "Gekochtes Huhn"},
                {"Raw Chicken Breast", "Rohe Hühnerbrust"},
                {"Spoiled Chicken", "Verwöhntes Huhn"},
                {"Burnt Deer Meat", "Verbranntes Hirschfleisch"},
                {"Cooked Deer Meat", "Gekochtes Hirschfleisch"},
                {"Raw Deer Meat", "Rohes Hirschfleisch"},
                {"Burnt Horse Meat", "Verbranntes Pferdefleisch"},
                {"Cooked Horse Meat", "Gekochtes Pferdefleisch"},
                {"Raw Horse Meat", "Rohes Pferdefleisch"},
                {"Burnt Human Meat", "Verbranntes menschliches Fleisch"},
                {"Cooked Human Meat", "Gekochtes menschliches Fleisch"},
                {"Raw Human Meat", "Rohes menschliches Fleisch"},
                {"Spoiled Human Meat", "Verdorbenes menschliches Fleisch"},
                {"Burnt Bear Meat", "Verbranntes Bärenfleisch"},
                {"Cooked Bear Meat", "Gekochtes Bärenfleisch"},
                {"Raw Bear Meat", "Rohes Bärenfleisch"},
                {"Burnt Wolf Meat", "Verbranntes Wolfsfleisch"},
                {"Cooked Wolf Meat", "Gekochtes Wolfsfleisch"},
                {"Raw Wolf Meat", "Rohes Wolfsfleisch"},
                {"Spoiled Wolf Meat", "Verdorbenes Wolfsfleisch"},
                {"Burnt Pork", "Verbranntes Schweinefleisch"},
                {"Cooked Pork", "Gekochtes Schweinefleisch"},
                {"Raw Pork", "Rohes Schweinefleisch"},
                {"Mushroom", "Pilz"},
                {"Pickles", "Essiggurken"},
                {"Small Water Bottle", "Kleine Wasserflasche"},
                {"Water Jug", "Wasserkrug"},
                {"Shovel Bass", "Schaufelbass"},
                {"Cowbell", "Kuhglocke"},
                {"Junkyard Drum Kit", "Junkyard Drum Kit"},
                {"Pan Flute", "Panflöte"},
                {"Acoustic Guitar", "Akustische Gitarre"},
                {"Jerry Can Guitar", "Jerry Can Gitarre"},
                {"Wheelbarrow Piano", "Schubkarre Klavier"},
                {"Canbourine", "Canbourine"},
                {"Plumber's Trumpet", "Klempner Trompete"},
                {"Sousaphone", "Sousaphon"},
                {"Xylobone", "Xylobone"},
                {"Car Key", "Autoschlüssel"},
                {"Door Key", "Nach Schlüssel"},
                {"Key Lock", "Tastensperre"},
                {"Code Lock", "Codesperre"},
                {"Blueprint", "Entwurf"},
                {"Chinese Lantern", "Chinesische Laterne"},
                {"Dragon Door Knocker", "Drachentürklopfer"},
                {"Dragon Mask", "Drachenmaske"},
                {"New Year Gong", "Neujahrsgong"},
                {"Rat Mask", "Rattenmaske"},
                {"Firecracker String", "Kracher-Schnur"},
                {"Chippy Arcade Game", "Chippy Arcade-Spiel"},
                {"Door Closer", "Türschließer"},
                {"Bunny Ears", "Hasenohren"},
                {"Bunny Onesie", "Häschen Onesie"},
                {"Easter Door Wreath", "Ostertürkranz"},
                {"Egg Basket", "Eierkorb"},
                {"Rustigé Egg - Red", "Ruhiges Ei - Rot"},
                {"Rustigé Egg - Blue", "Ruhiges Ei - Blau"},
                {"Rustigé Egg - Purple", "Ruhiges Ei - Lila"},
                {"Rustigé Egg - Ivory", "Ruhiges Ei - Elfenbein"},
                {"Nest Hat", "Nesthut"},
                {"Bronze Egg", "Bronzeei"},
                {"Gold Egg", "Gold Ei"},
                {"Painted Egg", "Gemaltes Ei"},
                {"Silver Egg", "Silber Ei"},
                {"Halloween Candy", "Halloween-Süßigkeit"},
                {"Large Candle Set", "Großes Kerzenset"},
                {"Small Candle Set", "Kleines Kerzenset"},
                {"Coffin", "Sarg"},
                {"Cursed Cauldron", "Verfluchter Kessel"},
                {"Gravestone", "Grabstein"},
                {"Wooden Cross", "Holzkreuz"},
                {"Graveyard Fence", "Friedhofszaun"},
                {"Large Loot Bag", "Große Beutetasche"},
                {"Medium Loot Bag", "Mittlere Beutetasche"},
                {"Small Loot Bag", "Kleine Beutetasche"},
                {"Pumpkin Bucket", "Kürbiseimer"},
                {"Scarecrow", "Vogelscheuche"},
                {"Skull Spikes", "Schädelspitzen"},
                {"Skull Door Knocker", "Schädeltürklopfer"},
                {"Skull Fire Pit", "Schädel Feuerstelle"},
                {"Spider Webs", "Spinnennetze"},
                {"Spooky Speaker", "Gruseliger Sprecher"},
                {"Surgeon Scrubs", "Peelings für Chirurgen"},
                {"Skull Trophy", "Schädeltrophäe"},
                {"Card Movember Moustache", "Karte Movember Schnurrbart"},
                {"Movember Moustache", "Movember Moustache"},
                {"Note", "Hinweis"},
                {"Human Skull", "Menschlicher Schädel"},
                {"Above Ground Pool", "Oberirdischer Pool"},
                {"Beach Chair", "Strandstuhl"},
                {"Beach Parasol", "Strand Sonnenschirm"},
                {"Beach Table", "Strandtisch"},
                {"Beach Towel", "Badetuch"},
                {"Boogie Board", "Boogie-Board"},
                {"Inner Tube", "Innenrohr"},
                {"Instant Camera", "Sofortbildkamera"},
                {"Paddling Pool", "Planschbecken"},
                {"Photograph", "Foto"},
                {"Landscape Photo Frame", "Landschafts-Fotorahmen"},
                {"Large Photo Frame", "Großer Fotorahmen"},
                {"Portrait Photo Frame", "Porträt-Fotorahmen"},
                {"Sunglasses", "Sonnenbrille"},
                {"Water Gun", "Wasserpistole"},
                {"Water Pistol", "Wasserpistole"},
                {"Purple Sunglasses", "Lila Sonnenbrille"},
                {"Headset", "Headset"},
                {"Candy Cane Club", "Candy Cane Club"},
                {"Christmas Lights", "Weihnachtsbeleuchtung"},
                {"Festive Doorway Garland", "Festliche Türgirlande"},
                {"Candy Cane", "Zuckerstange"},
                {"Giant Candy Decor", "Riesiges Süßigkeiten-Dekor"},
                {"Giant Lollipop Decor", "Riesiges Lutscher-Dekor"},
                {"Pookie Bear", "Pookie Bär"},
                {"Deluxe Christmas Lights", "Deluxe Weihnachtsbeleuchtung"},
                {"Coal :(", "Kohle :("},
                {"Large Present", "Großes Geschenk"},
                {"Medium Present", "Mittlere Gegenwart"},
                {"Small Present", "Kleines Geschenk"},
                {"Snow Machine", "Schneemaschine"},
                {"Snowball", "Schneeball"},
                {"Snowman", "Schneemann"},
                {"SUPER Stocking", "SUPER Strumpf"},
                {"Small Stocking", "Kleiner Strumpf"},
                {"Reindeer Antlers", "Rentiergeweih"},
                {"Santa Beard", "Santa Bart"},
                {"Santa Hat", "Weihnachtsmütze"},
                {"Festive Window Garland", "Festliche Fenstergirlande"},
                {"Wrapped Gift", "Eingepacktes Geschenk"},
                {"Wrapping Paper", "Geschenkpapier"},
                {"Christmas Door Wreath", "Weihnachtstürkranz"},
                {"Decorative Baubels", "Dekorative Baubels"},
                {"Decorative Plastic Candy Canes", "Dekorative Zuckerstangen aus Kunststoff"},
                {"Decorative Gingerbread Men", "Dekorative Lebkuchenmänner"},
                {"Tree Lights", "Baumlichter"},
                {"Decorative Pinecones", "Dekorative Tannenzapfen"},
                {"Star Tree Topper", "Star Tree Topper"},
                {"Decorative Tinsel", "Dekorative Lametta"},
                {"Christmas Tree", "Weihnachtsbaum"},
                {"Auto Turret", "Auto Turret"},
                {"Flame Turret", "Flammenturm"},
                {"Glowing Eyes", "Glühende Augen"},
                {"SAM Ammo", "SAM Munition"},
                {"SAM Site", "SAM Site"},
                {"Black Berry", "Schwarze Beere"},
                {"Black Berry Clone", "Black Berry Clone"},
                {"Black Berry Seed", "Black Berry Seed"},
                {"Blue Berry", "Blaubeere"},
                {"Blue Berry Clone", "Blue Berry Clone"},
                {"Blue Berry Seed", "Blue Berry Seed"},
                {"Green Berry", "Grüne Beere"},
                {"Green Berry Clone", "Green Berry Clone"},
                {"Green Berry Seed", "Grüner Beerensamen"},
                {"Red Berry", "Rote Beere"},
                {"Red Berry Clone", "Red Berry Clone"},
                {"Red Berry Seed", "Roter Beerensamen"},
                {"White Berry", "Weiße Beere"},
                {"White Berry Clone", "White Berry Clone"},
                {"White Berry Seed", "Weißer Beerensamen"},
                {"Yellow Berry", "Gelbe Beere"},
                {"Yellow Berry Clone", "Gelber Beerenklon"},
                {"Yellow Berry Seed", "Gelber Beerensamen"},
                {"Corn", "Mais"},
                {"Corn Clone", "Mais-Klon"},
                {"Corn Seed", "Maissamen"},
                {"Hemp Clone", "Hanfklon"},
                {"Hemp Seed", "Hanfsamen"},
                {"Potato", "Kartoffel"},
                {"Potato Clone", "Kartoffelklon"},
                {"Potato Seed", "Kartoffelsamen"},
                {"Pumpkin", "Kürbis"},
                {"Pumpkin Plant Clone", "Kürbis-Pflanzen-Klon"},
                {"Pumpkin Seed", "Kürbissamen"},
                {"Animal Fat", "Tierfett"},
                {"Battery - Small", "Batterie - klein"},
                {"Blood", "Blut"},
                {"Bone Fragments", "Knochenfragmente"},
                {"CCTV Camera", "Überwachungskamera"},
                {"Charcoal", "Holzkohle"},
                {"Cloth", "Stoff"},
                {"Crude Oil", "Rohöl"},
                {"Diesel Fuel", "Dieselkraftstoff"},
                {"Empty Can Of Beans", "Leere Dose Bohnen"},
                {"Empty Tuna Can", "Leere Thunfischdose"},
                {"Explosives", "Sprengstoff"},
                {"Fertilizer", "Dünger"},
                {"Gun Powder", "Schießpulver"},
                {"Horse Dung", "Horse Dung"},
                {"High Quality Metal Ore", "Hochwertiges Metallerz"},
                {"High Quality Metal", "Hochwertiges Metall"},
                {"Leather", "Leder"},
                {"Low Grade Fuel", "Kraftstoff mit geringer Qualität"},
                {"Metal Fragments", "Metallfragmente"},
                {"Metal Ore", "Metallerz"},
                {"Paper", "Papier"},
                {"Plant Fiber", "Pflanzenfaser"},
                {"Research Paper", "Forschungsbericht"},
                {"Salt Water", "Salzwasser"},
                {"Scrap", "Schrott"},
                {"Stones", "Steine"},
                {"Sulfur Ore", "Schwefelerz"},
                {"Sulfur", "Schwefel"},
                {"Targeting Computer", "Zielcomputer"},
                {"Water", "Wasser"},
                {"Wolf Skull", "Wolfsschädel"},
                {"Wood", "Holz"},
                {"Advanced Healing Tea", "Fortgeschrittener Heilungstee"},
                {"Basic Healing Tea", "Grundlegender Heiltee"},
                {"Pure Healing Tea", "Reiner heilender Tee"},
                {"Advanced Max Health Tea", "Advanced Max Health Tee"},
                {"Basic Max Health Tea", "Grundlegender Max Health Tee"},
                {"Pure Max Health Tea", "Pure Max Health Tee"},
                {"Advanced Ore Tea", "Fortgeschrittener Erztee"},
                {"Basic Ore Tea", "Grunderz Tee"},
                {"Pure Ore Tea", "Reiner Erztee"},
                {"Advanced Rad. Removal Tea", "Advanced Rad. Entfernung Tee"},
                {"Rad. Removal Tea", "Rad. Entfernung Tee"},
                {"Pure Rad. Removal Tea", "Pure Rad. Entfernung Tee"},
                {"Adv. Anti-Rad Tea", "Adv. Anti-Rad Tee"},
                {"Anti-Rad Tea", "Anti-Rad Tee"},
                {"Pure Anti-Rad Tea", "Reiner Anti-Rad-Tee"},
                {"Advanced Scrap Tea", "Advanced Scrap Tea"},
                {"Basic Scrap Tea", "Grundschrotttee"},
                {"Pure Scrap Tea", "Reiner Schrotttee"},
                {"Advanced Wood Tea", "Fortgeschrittener Holztee"},
                {"Basic Wood Tea", "Grundlegender Holztee"},
                {"Pure Wood Tea", "Tee aus reinem Holz"},
                {"Anti-Radiation Pills", "Anti-Strahlenpillen"},
                {"Binoculars", "Fernglas"},
                {"Timed Explosive Charge", "Zeitgesteuerte Sprengladung"},
                {"Camera", "Kamera"},
                {"RF Transmitter", "HF-Sender"},
                {"Handmade Fishing Rod", "Handgemachte Angelrute"},
                {"Flare", "Fackel"},
                {"Flashlight", "Taschenlampe"},
                {"Geiger Counter", "Geigerzähler"},
                {"Hose Tool", "Schlauchwerkzeug"},
                {"Jackhammer", "Presslufthammer"},
                {"Blue Keycard", "Blaue Schlüsselkarte"},
                {"Green Keycard", "Grüne Schlüsselkarte"},
                {"Red Keycard", "Rote Schlüsselkarte"},
                {"Large Medkit", "Großes Medkit"},
                {"Paper Map", "Papierkarte"},
                {"Medical Syringe", "Medizinische Spritze"},
                {"RF Pager", "RF Pager"},
                {"Building Plan", "Bauplan"},
                {"Smoke Grenade", "Rauchgranate"},
                {"Supply Signal", "Versorgungssignal"},
                {"Survey Charge", "Umfragegebühr"},
                {"Wire Tool", "Drahtwerkzeug"},
                {"Small Chassis", "Kleines Chassis"},
                {"Medium Chassis", "Mittleres Chassis"},
                {"Large Chassis", "Großes Chassis"},
                {"Cockpit Vehicle Module", "Cockpit Fahrzeugmodul"},
                {"Armored Cockpit Vehicle Module", "Gepanzertes Cockpit-Fahrzeugmodul"},
                {"Cockpit With Engine Vehicle Module", "Cockpit mit Motorfahrzeugmodul"},
                {"Engine Vehicle Module", "Motor Fahrzeugmodul"},
                {"Flatbed Vehicle Module", "Pritschenfahrzeugmodul"},
                {"Armored Passenger Vehicle Module", "Gepanzertes Pkw-Modul"},
                {"Rear Seats Vehicle Module", "Fahrzeugmodul für Rücksitze"},
                {"Storage Vehicle Module", "Lagerfahrzeugmodul"},
                {"Taxi Vehicle Module", "Taxi Fahrzeugmodul"},
                {"Large Flatbed Vehicle Module", "Großes Pritschenfahrzeugmodul"},
                {"Fuel Tank Vehicle Module", "Kraftstofftank Fahrzeugmodul"},
                {"Passenger Vehicle Module", "Pkw-Modul"},
                {"Generic vehicle module", "Generisches Fahrzeugmodul"},
                {"Telephone", "Telefon"},
                {"16x Zoom Scope", "16x Zoombereich"},
                {"Weapon flashlight", "Waffentaschenlampe"},
                {"Holosight", "Holosight"},
                {"Weapon Lasersight", "Waffenlaser"},
                {"Muzzle Boost", "Mündungsschub"},
                {"Muzzle Brake", "Mündungsbremse"},
                {"Simple Handmade Sight", "Einfache handgemachte Sicht"},
                {"Silencer", "Schalldämpfer"},
                {"8x Zoom Scope", "8x Zoombereich"},
                {"Assault Rifle", "Sturmgewehr"},
                {"Bandage", "Bandage"},
                {"Beancan Grenade", "Bohnenkanne Grenada"},
                {"Bolt Action Rifle", "Repetierbüchse"},
                {"Bone Club", "Knochenclub"},
                {"Bone Knife", "Knochenmesser"},
                {"Hunting Bow", "Jagdbogen"},
                {"Birthday Cake", "Geburtstagskuchen"},
                {"Chainsaw", "Kettensäge"},
                {"Salvaged Cleaver", "Geborgenes Hackmesser"},
                {"Compound Bow", "Compoundbogen"},
                {"Crossbow", "Armbrust"},
                {"Double Barrel Shotgun", "Double Barrel Shotgun"},
                {"Eoka Pistol", "Eoka Pistole"},
                {"F1 Grenade", "F1 Granada"},
                {"Flame Thrower", "Flammenwerfer"},
                {"Multiple Grenade Launcher", "Mehrfachgranatenwerfer"},
                {"Butcher Knife", "Metzgermesser"},
                {"Pitchfork", "Heugabel"},
                {"Sickle", "Sichel"},
                {"Hammer", "Hammer"},
                {"Hatchet", "Beil"},
                {"Combat Knife", "Kampfmesser"},
                {"L96 Rifle", "L96 Gewehr"},
                {"LR-300 Assault Rifle", "LR-300 Sturmgewehr"},
                {"M249", "M249"},
                {"M39 Rifle", "M39 Gewehr"},
                {"M92 Pistol", "M92 Pistole"},
                {"Mace", "Katze"},
                {"Machete", "Machete"},
                {"MP5A4", "MP5A4"},
                {"Nailgun", "Nagelpistole"},
                {"Paddle", "Paddel"},
                {"Pickaxe", "Spitzhacke"},
                {"Waterpipe Shotgun", "Wasserpfeifen-Schrotflinte"},
                {"Python Revolver", "Python Revolver"},
                {"Revolver", "Revolver"},
                {"Rock", "Felsen"},
                {"Rocket Launcher", "Raketenwerfer"},
                {"Salvaged Axe", "Geborgene Axt"},
                {"Salvaged Hammer", "Geborgener Hammer"},
                {"Salvaged Icepick", "Geborgener Eispickel"},
                {"Satchel Charge", "Schulranzenladung"},
                {"Pump Shotgun", "Pumpschrotflinte"},
                {"Semi-Automatic Pistol", "Halbautomatische Pistole"},
                {"Semi-Automatic Rifle", "Halbautomatisches Gewehr"},
                {"Custom SMG", "Benutzerdefinierte SMG"},
                {"Spas-12 Shotgun", "Spas-12 Schrotflinte"},
                {"Stone Hatchet", "Stein Beil"},
                {"Stone Pickaxe", "Steinhacke"},
                {"Stone Spear", "Steinspeer"},
                {"Longsword", "Langschwert"},
                {"Salvaged Sword", "Geborgenes Schwert"},
                {"Thompson", "Thompson"},
                {"Garry's Mod Tool Gun", "Garrys Mod Tool Gun"},
                {"Torch", "Fackel"},
                {"Water Bucket", "Wassereimer"},
                {"Wooden Spear", "Holzspeer"},
                {"Roadsign Horse Armor", "Roadsign Pferderüstung"},
                {"Wooden Horse Armor", "Hölzerne Pferderüstung"},
                {"Horse Saddle", "Pferd Sattel"},
                {"Saddle bag", "Satteltasche"},
                {"High Quality Horse Shoes", "Hochwertige Hufeisen"},
                {"Basic Horse Shoes", "Grundlegende Hufeisen"},
                {"Generic vehicle chassis", "Generisches Fahrzeugchassis"}
            }, this, "de"); // German
            
        }
        private string Lang(string key, string id = null) => lang.GetMessage(key, this, id);
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        
        #endregion

        #region Oxide

        readonly Dictionary<ulong, string> _customSpawnables = new Dictionary<ulong, string>
        {
            {
                2255658925, "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab"
            }
        };

        void OnUserConnected(IPlayer player) => GetPlayerData(player.Object as BasePlayer);

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            BaseEntity entity = go.ToBaseEntity();
            if (entity == null || entity.skinID == 0UL)
            {
                return;
            }

            if (_customSpawnables.ContainsKey(entity.skinID))
            {
                SpawnReplacementItem(entity, _customSpawnables[entity.skinID]);

                NextTick(() => entity.Kill());
            }
        }

        void SpawnReplacementItem(BaseEntity entity, string prefabPath)
        {
            BaseEntity newEntity = GameManager.server.CreateEntity(prefabPath, entity.ServerPosition, entity.ServerRotation);
            if (newEntity == null) return;
            newEntity.Spawn();
        }

        void OnEntityTakeDamage(BasePlayer player, HitInfo info) //Auto close feature
        {
            if (info == null)
            {
                return;
            }
            if (_playerUIOpen.Contains(player.UserIDString) && (info.IsProjectile() || info.damageTypes.Has(DamageType.Bite) || info.damageTypes.Has(DamageType.Blunt) || info.damageTypes.Has(DamageType.Drowned) || info.damageTypes.Has(DamageType.Explosion) || info.damageTypes.Has(DamageType.Stab) || info.damageTypes.Has(DamageType.Slash) || info.damageTypes.Has(DamageType.Fun_Water)))
            {
                DestroyUi(player, true);
            }
        }

        protected override void LoadDefaultConfig() => _config = new PluginConfig();

        private void OnServerInitialized()
        {
            CheckConfig();
            permission.RegisterPermission(BlockAllow, this);
            permission.RegisterPermission(Use, this);
            permission.RegisterPermission(Admin, this);
            permission.RegisterPermission(Vip, this);
            permission.RegisterPermission(Color, this);
            LoadImages();
        }

        private void Init()
        {
            _instance = this;
        }

        private DynamicConfigFile _buyCoolDowns;
        private DynamicConfigFile _sellCoolDowns;
        private DynamicConfigFile _bought;
        private DynamicConfigFile _sold;
        private DynamicConfigFile _limits;
        private DynamicConfigFile _playerData;

        private void Loaded()
        {
            cmd.AddChatCommand(_config.shopcommand, this, CmdShop);

            _buyCoolDowns = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/BuyCooldowns");
            _sellCoolDowns = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/SellCooldowns");
            _bought = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/Purchases");
            _sold = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/Sales");
            _limits = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/Limits"); //adding Buy Limiter Function (Limit) TODO:
            _playerData = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/GUIShopPlayerConfigs");
            LoadData();
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList) DestroyUi(player, true);
            SaveData();
            _instance = null;
            _config = null;
        }

        #endregion

        #region UI
        private static CuiElementContainer CreateShopOverlay(string shopName, BasePlayer player)
        {
            return new CuiElementContainer
            {
                {
                    new CuiPanel  //This is the background transparency slider!
                    {
                        Image = {
                            Color = $"0 0 0 {_instance.GetUITransparency(player)}"
                        },
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                        CursorEnabled = true
                    },
                    "Overlay",
                    ShopOverlayName
                },
                {
                    new CuiElement
                    {
                        Parent = ShopOverlayName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = _instance.ImageLibrary?.Call<string>("GetImage", BackgroundImage) //updated 1.8.7
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1"
                            }
                        }
                    }
                },
                {
                    new CuiElement // GUIShop Welcome MSG
                    {
                        Parent = ShopOverlayName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                //Png = _instance.ImageLibrary?.Call<string>("GetImage", WelcomeImage) 
                                Url = _instance.GetText(_instance._config.GuiShopWelcomeUrl, "image", player)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.3 0.85",
                                AnchorMax = "0.7 0.95"
                            }
                        }
                    }
                },
                {
                    new CuiLabel //Welcome Msg
                    {
                        Text = {
                            Text = _instance.GetText(_instance._config.WelcomeMsg, "label", player),  //Updated to config output. https://i.imgur.com/Y9n5KgO.png
                            FontSize = 30,
                            Color = _instance.GetUITextColor(player),
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform = {AnchorMin = "0.3 0.85", AnchorMax = "0.7 0.95"}
                    },
                    ShopOverlayName
                },
                /*{
                    new CuiElement // Limit Icon
                    {
                        Parent = ShopOverlayName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Url = _instance.GetText(config.LimitUrl, "image", player)  // // Adjust position/size
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.28 0.6",
                                AnchorMax = "0.33 0.65"
                            }
                        }
                    }
                },
                {
                    new CuiLabel
                    {
                        Text = {
                            Text = _instance.GetText(config.Limit, "label", player), //added Config output
                            FontSize = 20,
                            Color = _instance.GetUITextColor(player),
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform = {
                            AnchorMin = "0.2 0.6",
                            AnchorMax = "0.5 0.65" //"0.23 0.65" old was Item rebranded to Limit
                        }
                    },
                    ShopOverlayName
                },*/
                /*{
                    new CuiLabel  //Adding missing Lable for limit function
                    {
                        Text = {
                            Text = "Limit",
                            FontSize = 20,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform = {
                            AnchorMin = "0.2 0.6", //"0.2 0.6", Buy
                            AnchorMax = "0.5 0.65" //"0.7 0.65"  Buy
                        }
                    },
                    ShopOverlayName
                },*/
                {
                    new CuiElement // Amount Icon
                    {
                        Parent = ShopOverlayName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                //Png = _instance.ImageLibrary?.Call<string>("GetImage", AmountIcon1) //1.8.7
                                Url = _instance.GetText(_instance._config.AmountUrl, "image", player)  // // Adjust position/size
                            },
                            new CuiRectTransformComponent
                            {AnchorMin = "0.53 0.6", AnchorMax = "0.58 0.65"}
                        }
                    }
                },
                {
                    new CuiLabel // Amount Label
                    {
                        Text = {
                            Text = _instance.GetText(_instance._config.AmountLabel, "label", player),
                            FontSize = 20,
                            Color = _instance.GetUITextColor(player),
                            Align = TextAnchor.MiddleLeft
                        },
                        RectTransform = {AnchorMin = "0.535 0.6", AnchorMax = "0.7 0.65"}
                    },
                    ShopOverlayName
                },
                {
                    new CuiElement // Buy Icon
                    {
                        Parent = ShopOverlayName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                //Png = _instance.ImageLibrary?.Call<string>("GetImage", BuyIcon) //1.8.7
                                Url = _instance.GetText(_instance._config.BuyIconUrl, "image", player), //"https://i.imgur.com/3ucgFVg.png"  // Adjust position/size
                            },
                            new CuiRectTransformComponent
                            {AnchorMin = "0.435 0.6", AnchorMax = "0.465 0.65"}
                        }
                    }
                },
                {
                    new CuiLabel // Buy Price Label,
                    {
                        Text = {
                            Text = _instance.GetText(_instance._config.BuyLabel, "label", player),  //Updated
                            FontSize = 20,
                            Color = _instance.GetUITextColor(player),
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform = {AnchorMin = "0.4 0.6", AnchorMax = "0.5 0.65"}
                    },
                    ShopOverlayName
                },
                {
                    new CuiLabel // color added, added config output
                    {
                        Text = {
                            Text = _instance.GetText(_instance._config.SellLabel, "label", player),  //Sell $
                            FontSize = 20,
                            Color = _instance.GetUITextColor(player),
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform = {
                            AnchorMin = "0.55 0.6",  //Second digit = Hight Done.
                            AnchorMax = "0.9 0.65"  //Left to right size for msg
                        }
                    },
                    ShopOverlayName
                },
                {
                    new CuiElement // Sell Icon
                    {
                        Parent = ShopOverlayName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                //Png = _instance.ImageLibrary?.Call<string>("GetImage", SellIcon) //1.8.7
                                Url = _instance.GetText(_instance._config.SellIconUrl, "image", player)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.7 0.6",  //Second digit = Hight Done. First Digit = Position on screen from left to right.
                                AnchorMax = "0.76 0.65"  //Left to right size for msg
                            }
                        }
                    }
                },
                {
                    new CuiElement // Amount Icon
                    {
                        Parent = ShopOverlayName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                //Png = _instance.ImageLibrary?.Call<string>("GetImage", AmountIcon2) //1.8.7
                                Url = _instance.GetText(_instance._config.AmountUrl2, "image", player)
                            },
                            new CuiRectTransformComponent
                            {AnchorMin = "0.8 0.6", AnchorMax = "0.85 0.65"}
                        }
                    }
                },
                {
                    new CuiLabel //Amount Label
                    {
                        Text = {
                            Text = _instance.GetText(_instance._config.AmountLabel2, "label", player),
                            FontSize = 20,
                            Color = _instance.GetUITextColor(player),
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform = {AnchorMin = "0.75 0.6", AnchorMax = "0.9 0.65"}
                    },
                    ShopOverlayName
                },
                {
                    new CuiElement //close button image
                    {
                        Parent = ShopOverlayName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                //Png = _instance.ImageLibrary?.Call<string>("GetImage", CloseIcon) //1.8.7
                                Url = _instance.GetText(_instance._config.CloseButton, "image", player),  // Adjust position/size
                            },
                            new CuiRectTransformComponent
                            {AnchorMin = "0.45 0.14", AnchorMax = "0.55 0.19"}
                        }
                    }
                },
                {
                    new CuiButton //close button Label
                    {
                        Button = {
                            Close = ShopOverlayName,
                            Color = "0 0 0 0.40" //"1.4 1.4 1.4 0.14"  new
                        },
                        RectTransform = {AnchorMin = "0.45 0.14", AnchorMax = "0.55 0.19"},
                        Text = {
                            Text = _instance.GetText(_instance._config.CloseButtonlabel, "label", player),
                            FontSize = 20,
                            Color = _instance.GetUITextColor(player),
                            Align = TextAnchor.MiddleCenter
                        }
                    },
                    ShopOverlayName
                }
            };
        }

        private readonly CuiLabel shopDescription = new CuiLabel
        {
            Text = {
                Text = "{shopdescription}",
                FontSize = 15,
                Align = TextAnchor.MiddleCenter
            },
            RectTransform = {AnchorMin = "0.2 0.7", AnchorMax = "0.8 0.75"}
        };

        private CuiElementContainer CreateShopItemEntry(ShopItem shopItem, float ymax, float ymin, string shop, string color, bool sell, bool cooldown, BasePlayer player) //add _limitsData, Semi finished
        {
            var container = new CuiElementContainer
            {
                /*{
                    new CuiLabel //Test added for Limits display set amount positioning (Its in the perfect position now!)
                    {
                        Text = {
                            Text = _limitsData,  //rename for limiter config setting
                            FontSize = 15,
                            Color = _instance.GetUITextColor(player),
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform = {
                            AnchorMin = $"{(Sell ? 0.725 : 0.2)} {ymin}", //Keep location setting
                            AnchorMax = $"{(Sell ? 0.5 : 0.5)} {ymax}" //Keep location setting
                        }
                    },
                    ShopContentName
                }, */
                {
                    new CuiLabel  //Buy Price Display's Cost set amount in config
                    {
                        Text = {
                            Text = $"{(sell ? shopItem.SellPrice : shopItem.BuyPrice)}",
                            FontSize = 15,
                            Color = _instance.GetUITextColor(player),
                            Align = TextAnchor.MiddleLeft
                        },
                        RectTransform = {
                            AnchorMin = $"{(sell ? 0.725 : 0.45)} {ymin}",
                            AnchorMax = $"{(sell ? 0.755 : 0.5)} {ymax}"
                        }
                    },
                    ShopContentName
                }
            };

            bool isKitOrCommand = !shopItem.Command.IsNullOrEmpty() || !string.IsNullOrEmpty(shopItem.KitName);

            int[] maxSteps = _config.steps;

            if (isKitOrCommand)
            {
                maxSteps = new[] { 1 };
            }

            if (cooldown)
            {
                return container;
            }

            for (var i = 0; i < maxSteps.Length; i++)
            {
                container.Add(new CuiButton
                {
                    Button = {
                        Command = $"shop.{(sell ? "Sell" : "buy")} {shop.Replace(" ", "_")} {shopItem.DisplayName.Replace(" ", "_")} {maxSteps[i]}",
                        Color = color
                    },
                    RectTransform = {
                        AnchorMin = $"{(sell ? 0.775 : 0.5) + i * 0.03 + 0.001} {ymin}",
                        AnchorMax = $"{(sell ? 0.805 : 0.53) + i * 0.03 - 0.001} {ymax}"
                    },
                    Text = {
                        Text = maxSteps[i].ToString(),
                        FontSize = 15,
                        Color = _instance.GetUITextColor(player),
                        Align = TextAnchor.MiddleCenter
                    }
                }, ShopContentName);
            }

            if (_config.AllButton && !isKitOrCommand && !(!sell && shopItem.BuyCooldown > 0 || sell && shopItem.SellCooldown > 0))
            {
                container.Add(new CuiButton
                {
                    Button = {
                        Command = $"shop.{(sell ? "Sell" : "buy")} {shop.Replace(" ", "_")} {shopItem.DisplayName.Replace(" ", "_")} all",
                        Color = color
                    },
                    RectTransform = {
                        AnchorMin = $"{(sell ? 0.775 : 0.5) + maxSteps.Length * 0.03 + 0.001} {ymin}",
                        AnchorMax = $"{(sell ? 0.805 : 0.53) + maxSteps.Length * 0.03 - 0.001} {ymax}"
                    },
                    Text = {
                        Text = "All",  //All button
                        FontSize = 15,
                        Color = _instance.GetUITextColor(player),
                        Align = TextAnchor.MiddleCenter
                    }
                }, ShopContentName);
            }

            return container;
        }

        private CuiElementContainer CreateShopItemIcon(string name, float ymax, float ymin, ShopItem data, BasePlayer player)
        {

            var label = new CuiLabel
            {
                Text = {
                    Text = name,
                    FontSize = 15,
                    Color = _instance.GetUITextColor(player),
                    Align = TextAnchor.MiddleLeft
                },
                RectTransform = {AnchorMin = $"0.1 {ymin}", AnchorMax = $"0.3 {ymax}"}
            };

            var rawImage = new CuiRawImageComponent();

            if ((bool)(ImageLibrary?.Call("HasImage", data.Image) ?? false))
            {
                rawImage.Png = (string)ImageLibrary?.Call("GetImage", data.Image);
            }
            else
            {
                if (string.IsNullOrEmpty(data.Image))
                {
                    rawImage.Png = (string)ImageLibrary?.Call("GetImage", _config.IconUrl);
                }
                else
                {
                    rawImage.Url = data.Image;
                }
            }
            var container = new CuiElementContainer
            {
                {
                    label,
                    ShopContentName
                },
                new CuiElement
                {
                    Parent = ShopContentName,
                    Components =
                    {
                        rawImage,
                        new CuiRectTransformComponent {AnchorMin = $"0.05 {ymin}", AnchorMax = $"0.08 {ymax}"}
                    }
                }
            };
            return container;
        }

        private static CuiElementContainer CreateShopColorChanger(string currentshop, BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer
            {
                {
                    new CuiLabel
                    {
                        Text = {
                            Text = "Personal UI Settings",
                            FontSize = 15,
                            Color = _instance.GetUITextColor(player),
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform = {AnchorMin = "0.18 0.11", AnchorMax = "0.33 0.15"}
                    },
                    ShopOverlayName
                },
                {
                    new CuiButton //set button 1 + color
                    {
                        Button =
                        {
                            Command = $"shop.colorsetting Text {currentshop.Replace(" ", "_")}",
                            Close = ShopOverlayName,
                            Color = _instance.GetSettingTypeToChange("Text")
                        },
                        RectTransform = {AnchorMin = "0.10 0.09", AnchorMax = "0.17 0.12"},
                        Text =
                        {
                            Text = "Set Text Color",
                            FontSize = 15,
                            Color = _instance.GetUITextColor(player),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf"
                        }
                    },
                    ShopOverlayName,
                    "Set Text Color"
                },
                {
                    new CuiButton //Toggle Botton (Has enable/disable config option)
                    {
                        Button =
                        {
                            Command = $"shop.imageortext {currentshop.Replace(" ", "_")}",
                            Close = ShopOverlayName,
                            Color = "0 0 0 0"
                        },
                        RectTransform = {AnchorMin = "0.06 0.09", AnchorMax = "0.10 0.12"},
                        Text =
                        {
                            Text = "Toggle",
                            FontSize = 15,
                            Color = _instance.GetUITextColor(player),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf"
                        }
                    },
                    ShopOverlayName,
                    "Toggle"
                },
                {
                    new CuiButton //set button 3
                    {
                        Button =
                        {
                            Command = $"shop.colorsetting Sell {currentshop.Replace(" ", "_")}",
                            Close = ShopOverlayName,
                            Color = _instance.GetSettingTypeToChange("Sell")
                        },
                        RectTransform = {AnchorMin = "0.10 0.05", AnchorMax = "0.17 0.08"},
                        Text =
                        {
                            Text = "Sell Color",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter,
                            Color = _instance.GetUITextColor(player),
                            Font = "robotocondensed-regular.ttf"
                        }
                    },
                    ShopOverlayName,
                    "Sell Changer"
                },
                {
                    new CuiButton //set button 2
                    {
                        Button =
                        {
                            Command = $"shop.colorsetting Buy {currentshop.Replace(" ", "_")}",
                            Close = ShopOverlayName,
                            Color = _instance.GetSettingTypeToChange("Buy")
                        },
                        RectTransform = {AnchorMin = "0.10 0.02", AnchorMax = "0.17 0.05"},
                        Text =
                        {
                            Text = "Buy Color",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter,
                            Color = _instance.GetUITextColor(player),
                            Font = "robotocondensed-regular.ttf"
                        }
                    },
                    ShopOverlayName,
                    "Buy Changer"
                },
                {
                    new CuiLabel //Display Bar
                    {
                        Text = {
                            Text = "ⅢⅢⅢⅢⅢⅢⅢⅢ",
                            Color = _instance.GetUITextColor(player),
                            FontSize = 20,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform = {AnchorMin = "0.80 0.19", AnchorMax = $"{0.80 + _instance.AnchorBarMath(player)} 0.24"}
                    },
                    ShopOverlayName
                },
                {
                    new CuiElement
                    {
                        Parent = ShopOverlayName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Url = _instance.GetText("https://imgur.com/qx9syT5.png", "image", player)  // More transparency Arrow
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.85 0.14",
                                AnchorMax = "0.90 0.19"
                            }
                        }
                    }
                },
                {
                    new CuiButton
                    {
                        Button =
                        {
                            Command = $"shop.transparency increase  {currentshop.Replace(" ", "_")}",
                            Close = ShopOverlayName,
                            Color = "0 0 0 0.40"
                        },
                        RectTransform = {AnchorMin = "0.85 0.14", AnchorMax = "0.90 0.19"},
                        Text =
                        {
                            Text = _instance.GetText(">>", "label", player),
                            Color = _instance.GetUITextColor(player),
                            FontSize = 30, Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf"
                        }
                    },
                    ShopOverlayName,
                    "ButtonMore"
                },
                { 
                    new CuiElement
                    {
                        Parent = ShopOverlayName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Url = _instance.GetText("https://imgur.com/zNKprM1.png", "image", player) // Less transparency Arrow
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.80 0.14",
                                AnchorMax = "0.85 0.19"
                            }
                        }
                    }
                },
                {
                    new CuiButton
                    {
                        Button =
                        {
                            Command = $"shop.transparency decrease {currentshop.Replace(" ", "_")}",
                            Close = ShopOverlayName,
                            Color = "0 0 0 0.40"
                        },
                        RectTransform = {AnchorMin = "0.80 0.14", AnchorMax = "0.85 0.19"},
                        Text =
                        {
                            Text = _instance.GetText("<<", "label", player),
                            Color = _instance.GetUITextColor(player),
                            FontSize = 30, Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf"
                        }
                    },
                    ShopOverlayName,
                    "ButtonLess"
                },
                {
                    new CuiPanel
                    {
                        Image =
                        {
                            Color = "0 0 0 0"
                        },
                        RectTransform = {AnchorMin = "0.18 0.01", AnchorMax = "0.33 0.12"}
                    }, ShopOverlayName, ShopColorPicker
                }
            };
            
            
            int itemPos = 0;
            
            foreach (string color in _instance._config.ColorsUI)
            {
                int numberPerRow = 4;
            
                float padding = 0.03f; // Space between each 
                float margin = (0.01f + padding); //left to right alignment adjuster

                //_instance.Puts("{0}", padding * (numberPerRow + 1) / numberPerRow);
                float width = ((0.975f - (padding * (numberPerRow + 1))) / numberPerRow);
                //_instance.Puts("{0}", width * 1.75f);
                float height = (width * 1.975f);

                int row = (int)Math.Floor((float)itemPos / numberPerRow);
                int col = (itemPos - (row * numberPerRow));
                container.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"shop.uicolor {HexToColor(color)} {currentshop.Replace(" ", "_")}",
                            Close = ShopOverlayName,
                            Color = $"{HexToColor(color)} 0.9"
                        },
                        RectTransform = {
                            AnchorMin = $"{margin + (width * col) + (padding * col)} {(0.975f - padding) - ((row + 1) * height) - (padding * row)}", 
                            AnchorMax =  $"{margin + (width * (col + 1)) + (padding * col)} {(0.93f - padding) - (row * height) - (padding * row)}"
                            
                        },
                        Text =
                        {
                            Text = "",
                        }
                    }, ShopColorPicker, $"ColorPicker_{color}");
                itemPos++;
            }

            return container;
        }

        private static CuiElementContainer CreateShopChangePage(string currentshop, int shoppageminus, int shoppageplus, BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer
            {
                {
                    new CuiElement
                    {
                        Parent = ShopOverlayName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Url = _instance.GetText("https://imgur.com/zNKprM1.png", "image", player)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.345 0.14",
                                AnchorMax = "0.445 0.19"
                            }
                        }
                    }
                },
                {
                    new CuiButton
                    {
                        Button =
                        {
                            Command = $"shop.show {currentshop.Replace(" ", "_")} {shoppageminus}",
                            Color = "0 0 0 0.40"
                        },
                        RectTransform = {AnchorMin = "0.345 0.14", AnchorMax = "0.445 0.19"},
                        Text =
                        {
                            Text = _instance.GetText("<<", "label", player),
                            Color = _instance.GetUITextColor(player),
                            FontSize = 30,
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf"
                        }
                    },
                    ShopOverlayName,
                    "ButtonBack"
                },
                {
                    new CuiElement
                    {
                        Parent = ShopOverlayName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Url = _instance.GetText("https://imgur.com/qx9syT5.png", "image", player)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.555 0.14",
                                AnchorMax = "0.655 0.19"
                            }
                        }
                    }
                },
                {
                    new CuiButton
                    {
                        Button =
                        {
                            Command = $"shop.show {currentshop.Replace(" ", "_")} {shoppageplus}",
                            Color = "0 0 0 0.40"
                        },
                        RectTransform = {AnchorMin = "0.555 0.14", AnchorMax = "0.655 0.19"},
                        Text =
                        {
                            Text = _instance.GetText(">>", "label", player),
                            Color = _instance.GetUITextColor(player),
                            FontSize = 30,
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf"
                        }
                    },
                    ShopOverlayName,
                    "ButtonForward"
                }
            };

            return container;
        }

        private static void CreateTab(ref CuiElementContainer container, ShopCategory cat, int shoppageminus, int rowPos, BasePlayer player) //Button-Shop Tab generator
        {
            container.Add(new CuiButton
            {
                Button =
                {
                    Command = $"shop.show {cat.DisplayName.Replace(" ", "_")} {shoppageminus}",
                    Color = "0.5 0.5 0.5 0.5"  //"1.2 1.2 1.2 0.24" new
                },
                RectTransform =
                {
                    AnchorMin = $"{(0.09 + (rowPos * 0.056))} 0.78", // * 0.056 = Margin for more buttons... less is better
                    AnchorMax = $"{(0.14 + (rowPos * 0.056))} 0.82"
                },
                Text =
                {
                    Text = _instance.Lang(cat.DisplayName, player.UserIDString),
                    Align = TextAnchor.MiddleCenter,
                    Color = _instance.GetUITextColor(player),
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12
                }
            }, ShopOverlayName, cat.DisplayName);
        }

        private void DestroyUi(BasePlayer player, bool full = false)
        {
            CuiHelper.DestroyUi(player, ShopContentName);
            CuiHelper.DestroyUi(player, "ButtonForward");
            CuiHelper.DestroyUi(player, "ButtonBack");
            if (!full) return;
            CuiHelper.DestroyUi(player, ShopDescOverlay);
            CuiHelper.DestroyUi(player, ShopOverlayName);
        }
        #endregion

        #region Currency
        private int GetCurrency(BasePlayer player)
        {
            if (_config.Economics && Economics != null)
            {
                return (int)Economics.Call<double>("Balance", player.UserIDString);
            }

            if (_config.ServerRewards && ServerRewards != null)
            {
                return ServerRewards.Call<int>("CheckPoints", player.UserIDString);
            }

            if (_config.CustomCurrency)
            {
                return player.inventory.GetAmount(_config.CustomCurrencyID);
            }

            return 0;
        }

        private bool TakeCurrency(BasePlayer player, int amount)
        {
            if (_config.Economics && Economics != null)
            {
                return Economics.Call<bool>("Withdraw", player.userID, (double) amount);
            }

            if (_config.ServerRewards && ServerRewards != null)
            {
                return ServerRewards.Call<object>("TakePoints", player.userID, amount) != null;
            }

            if (_config.CustomCurrency && player.inventory.GetAmount(_config.CustomCurrencyID) >= amount)
            {
                player.inventory.Take(null, _config.CustomCurrencyID, amount);
                return true;
            }

            return false;
        }

        private void AddCurrency(BasePlayer player, int amount)
        {
            if (_config.Economics && Economics != null)
            {
                Economics?.Call("Deposit", player.UserIDString, (double) amount);
                return;
            }

            if (_config.ServerRewards && ServerRewards != null)
            {
                ServerRewards?.Call("AddPoints", player.UserIDString, amount);
                return;
            }

            if (_config.CustomCurrency && player.inventory.GetAmount(_config.CustomCurrencyID) >= amount)
            {
                Item item = ItemManager.CreateByItemID(_config.CustomCurrencyID, amount);
                player.GiveItem(item);
            }
            
        }
        
        #endregion

        #region Shop
        private void ShowShop(BasePlayer player, string shopid, int from = 0, bool fullPaint = true, bool refreshMoney = false)
        {
            _shopPage[player.userID] = from;

            ShopCategory shop;

            if (_config.ShopCategories.Where(x => !x.Value.EnableNPC).Count() <= 0) //added: When all shops are disabled for global
            {
                SendReply(player, Lang("MessageErrorGlobalDisabled"));
                return;
            }

            if (!_config.ShopCategories.TryGetValue(shopid, out shop))
            {
                SendReply(player, Lang("MessageErrorNoShop", player.UserIDString));

                return;
            }

            if (_config.BlockMonuments && !shop.EnableNPC && IsNearMonument(player))
            {
                SendReply(player, Lang("BlockedMonuments", player.UserIDString));
                return;
            }

            double playerCoins = GetCurrency(player);

            shopDescription.Text.Text = string.Format(shop.Description, playerCoins);
            shopDescription.Text.Color = _instance.GetUITextColor(player);

            if (refreshMoney)
            {
                CuiHelper.DestroyUi(player, ShopDescOverlay);

                CuiHelper.AddUi(player, new CuiElementContainer { { shopDescription, ShopOverlayName, ShopDescOverlay } });
            }

            DestroyUi(player, fullPaint);

            CuiElementContainer container;

            if (fullPaint)
            {
                container = CreateShopOverlay(shop.DisplayName, player);

                container.Add(shopDescription, ShopOverlayName, ShopDescOverlay);

                int rowPos = 0;

                if (shop.EnableNPC && !string.IsNullOrEmpty(shop.NPCId))
                {
                    foreach (ShopCategory cat in _instance._config.ShopCategories.Values.Where(i => i.EnableNPC && i.NPCId == shop.NPCId))
                    {
                        CreateTab(ref container, cat, from, rowPos, player);

                        rowPos++;
                    }
                }
                else
                {
                    foreach (ShopCategory cat in _instance._config.ShopCategories.Values.Where(i => i.EnabledCategory && !i.EnableNPC))
                    {
                        CreateTab(ref container, cat, from, rowPos, player);

                        rowPos++;
                    }
                }
            }
            else
                container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0"
                },
                RectTransform = {AnchorMin = "0 0.2", AnchorMax = "1 0.6"}
            }, ShopOverlayName, ShopContentName);

            if (from < 0)
            {
                CuiHelper.AddUi(player, container);
                return;
            }

            int current = 0;

            HashSet<ShopItem> shopItems = new HashSet<ShopItem>(); // Re-orders the items in shop based on index location.

            foreach (var shortname in shop.Items)
            {
                if (_config.ShopItems.ContainsKey(shortname))
                {
                    ShopItem shopItem = _config.ShopItems[shortname];
                    shopItems.Add(shopItem);
                }
            }

            foreach (ShopItem data in shopItems)
            {
                if (current >= from && current < from + 7)
                {
                    float pos = 0.85f - 0.125f * (current - from);

                    string name = data.DisplayName;  // TODO: Updated 12/12

                    string cooldowndescription = string.Empty;

                    double sellCooldown;
                    double buyCooldown;

                    bool hasSellCooldown = data.SellCooldown > 0 && HasSellCooldown(player.userID, data.DisplayName, out sellCooldown);
                    bool hasBuyCooldown = data.BuyCooldown > 0 && HasBuyCooldown(player.userID, data.DisplayName, out buyCooldown);

                    bool cooldown = data.BuyCooldown > 0 || data.SellCooldown > 0;

                    if (data.BuyCooldown > 0)
                    {
                        cooldowndescription += $" (Buy CoolDown: {FormatTime(data.BuyCooldown)})";
                    }

                    if (data.SellCooldown > 0)
                    {
                        cooldowndescription += $" (Sell CoolDown: {FormatTime(data.SellCooldown)})";  //TODO:  multi support
                    }

                    name = $"{Lang(name, player.UserIDString)}<size=10>{(cooldown ? "\n" + cooldowndescription : "")}</size>"; //added Updated,  Creates new line for cooldowns under the Displayed Item Names.

                    container.AddRange(CreateShopItemIcon(name, pos + 0.125f, pos, data, player));

                    bool hasBuyed = false;

                    if (hasBuyCooldown)
                    {
                        hasBuyed = true;

                        container.Add(new CuiLabel
                        {
                            Text =
                            {
                                Text = $"{data.BuyPrice}",
                                FontSize = 15,
                                Color = _instance.GetUITextColor(player),
                                Align = TextAnchor.MiddleLeft
                            },
                            RectTransform = {AnchorMin = $"0.45 {pos}", AnchorMax = $"0.5 {pos + 0.125f}"}
                        }, ShopContentName);
                    }

                    if (!hasBuyed && data.EnableBuy)
                        container.AddRange(CreateShopItemEntry(data, pos + 0.125f, pos, shopid, GetUIBuyBoxColor(player), false, hasBuyCooldown, player));

                    if (data.EnableSell)
                        container.AddRange(CreateShopItemEntry(data, pos + 0.125f, pos, shopid, GetUISellBoxColor(player), true, hasSellCooldown, player));
                }

                current++;
            }

            int minfrom = from <= 7 ? 0 : from - 7;

            int maxfrom = from + 7 >= current ? from : from + 7;

            container.AddRange(CreateShopChangePage(shopid, minfrom, maxfrom, player));

            if (permission.UserHasPermission(player.UserIDString, Vip) || _config.PersonalUI)
            {
                container.AddRange(CreateShopColorChanger(shopid, player)); //1.8.8 updating UI
            }

            CuiHelper.AddUi(player, container);
        }

        object CanDoAction(BasePlayer player, string shop, string item, string ttype)
        {
            if (!_config.ShopCategories.ContainsKey(shop))
            {
                return Lang("MessageErrorNoActionShop", player.UserIDString, ttype);
            }

            if (!_config.ShopItems.ContainsKey(item))
            {
                return Lang("MessageErrorItemNotFound", player.UserIDString);
            }

            if (!_config.ShopItems[item].EnableBuy && ttype == "buy")
            {
                return Lang("MessageErrorItemNotEnabled", player.UserIDString, ttype);
            }

            if (!_config.ShopItems[item].EnableSell && ttype == "Sell")
            {
                return Lang("MessageErrorItemNotEnabled", player.UserIDString, ttype);
            }

            return true;
        }

        object CanShop(BasePlayer player, string shopName)
        {
            if (!_config.ShopCategories.ContainsKey(shopName))
            {
                return Lang("MessageErrorNoShop", player.UserIDString);
            }

            return true;
        }

        #endregion

        #region Buy
        object TryShopBuy(BasePlayer player, string shop, string item, int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            object success = CanShop(player, shop);

            if (success is string)
            {
                return success;
            }

            success = CanDoAction(player, shop, item, "buy");

            if (success is string)
            {
                return success;
            }

            success = CanBuy(player, item, amount);

            if (success is string)
            {
                return success;
            }

            success = TryGive(player, item, amount);

            if (success is string)
            {
                return success;
            }

            ShopItem data = _config.ShopItems[item];

            if (!TakeCurrency(player, (int)(data.BuyPrice * amount)))
            {
                return Lang("MessageShowNoServerRewards", player.UserIDString);
            }

            if (data.BuyCooldown > 0) //TODO: Shorten single if statements.
            {
                Dictionary<string, double> itemCooldowns;

                if (!_buyCooldownData.TryGetValue(player.userID, out itemCooldowns))
                    _buyCooldownData[player.userID] = itemCooldowns = new Dictionary<string, double>();
                itemCooldowns[item] = CurrentTime() + data.BuyCooldown /* *amount */;
            }

            if (!string.IsNullOrEmpty(data.Shortname)) //updated //TODO: Updateded 12/12
            {
                ulong count;

                _boughtData.TryGetValue(data.Shortname, out count);

                _boughtData[data.Shortname] = count + (ulong)amount;
            }

            return false;
        }

        object TryGive(BasePlayer player, string item, int amount)
        {
            ShopItem data = _config.ShopItems[item];

            if (!data.Command.IsNullOrEmpty())  //updated 1.8.5
            {
                Vector3 pos = player.ServerPosition + player.eyes.HeadForward() * 3.5f;
                pos.y = TerrainMeta.HeightMap.GetHeight(pos);

                foreach (var command in data.Command)
                {
                    var c = command
                        .Replace("$player.id", player.UserIDString)
                        .Replace("$player.name", player.displayName)
                        .Replace("$player.x", pos.x.ToString())
                        .Replace("$player.y", pos.y.ToString())
                        .Replace("$player.z", pos.z.ToString());

                    if (c.StartsWith("shop.show close", StringComparison.OrdinalIgnoreCase))
                        NextTick(() => ConsoleSystem.Run(ConsoleSystem.Option.Server, c));
                    else
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, c);

                }
            }

            else if (!string.IsNullOrEmpty(data.KitName))
            {
                object isKit = Kits?.CallHook("isKit", data.KitName);

                if (isKit is bool && (bool)isKit)
                {
                    object successkit = Kits.CallHook("GiveKit", player, data.KitName);

                    if (successkit is bool && !(bool)successkit)
                    {
                        return Lang("MessageErrorRedeemKit", player.UserIDString);
                    }
                    return true;
                }
            }

            else if (!string.IsNullOrEmpty(data.Shortname))
            {
                if (player.inventory.containerMain.IsFull() && player.inventory.containerBelt.IsFull())
                {
                    return Lang("MessageErrorInventoryFull", player.UserIDString);
                }

                object success = GiveItem(player, data, amount);

                if (success is string)
                {
                    return success;
                }
            }

            return true;
        }

        private int FreeSlots(BasePlayer player)
        {
            var slots = player.inventory.containerMain.capacity + player.inventory.containerBelt.capacity;
            var taken = player.inventory.containerMain.itemList.Count + player.inventory.containerBelt.itemList.Count;
            return slots - taken;
        }

        private List<int> GetStacks(ItemDefinition item, int amount)
        {
            var list = new List<int>();
            var maxStack = item.stackable;

            while (amount > maxStack)
            {
                amount -= maxStack;
                list.Add(maxStack);
            }

            list.Add(amount);

            return list;
        }

        private int GetAmountBuy(BasePlayer player, string item)
        {
            if (player.inventory.containerMain.IsFull() && player.inventory.containerBelt.IsFull())
            {
                return 0;
            }

            ShopItem data = _config.ShopItems[item];
            ItemDefinition definition = ItemManager.FindItemDefinition(data.Shortname);
            if (definition == null) return 0;

            var freeSlots = FreeSlots(player);

            return freeSlots * definition.stackable;
        }

        private object GiveItem(BasePlayer player, ShopItem data, int amount)
        {
            if (amount <= 0)
            {
                return Lang("MessageErrorNotNothing", player.UserIDString);
            }

            ItemDefinition definition = ItemManager.FindItemDefinition(data.Shortname);
            if (definition == null)
            {
                return Lang("MessageErrorItemNoExist", player.UserIDString);
            }

            var stack = GetStacks(definition, amount);
            var stacks = stack.Count;

            var slots = FreeSlots(player);
            if (slots < stacks)
            {
                return Lang("MessageErrorInventorySlots", player.UserIDString, stacks);
            }

            var quantity = (int)Math.Ceiling(amount / (float)stacks);
            for (var i = 0; i < stacks; i++)
            {
                var item = ItemManager.CreateByItemID(definition.itemid, quantity, data.SkinId);
                if (!player.inventory.GiveItem(item))
                {
                    item.Remove(0);
                }
            }

            return true;
        }

        object CanBuy(BasePlayer player, string item, int amount)
        {

            if (_config.ServerRewards == true && ServerRewards == null)
            {
                return Lang("MessageShowNoServerRewards", player.UserIDString);
            }

            if (_config.Economics == false && Economics == null)
            {
                return Lang("MessageShowNoEconomics", player.UserIDString);
            }

            if (!_config.ShopItems.ContainsKey(item))
            {
                return Lang("MessageErrorItemNoValidbuy", player.UserIDString);
            }

            var data = _config.ShopItems[item];
            if (data.BuyPrice < 0)
            {
                return Lang("MessageErrorBuyPrice", player.UserIDString);
            }

            if (data.Command != null && amount > 1)
            {
                return Lang("MessageErrorBuyCmd", player.UserIDString);
            }

            double buyprice = data.BuyPrice;

            if (GetCurrency(player) < buyprice * amount)
            {
                if (_config.CustomCurrency)
                {
                    return Lang("MessageErrorNotEnoughMoneyCustom", player.UserIDString, buyprice * amount, amount, item);
                }
                return Lang("MessageErrorNotEnoughMoney", player.UserIDString, buyprice * amount, amount, item);
            }

            if (data.BuyCooldown > 0)
            {
                Dictionary<string, double> itemCooldowns;
                double itemCooldown;

                if (_buyCooldownData.TryGetValue(player.userID, out itemCooldowns) && itemCooldowns.TryGetValue(item, out itemCooldown) && itemCooldown > CurrentTime())
                {
                    return Lang("MessageErrorCooldown", player.UserIDString, FormatTime((long)(itemCooldown - CurrentTime())));
                }
            }

            return true;
        }
        #endregion

        #region Sell
        object TryShopSell(BasePlayer player, string shop, string item, int amount)
        {
            object success = CanShop(player, shop);
            if (success is string)
            {
                return success;
            }

            success = CanDoAction(player, shop, item, "Sell");
            if (success is string)
            {
                return success;
            }

            success = CanSell(player, item, amount);
            if (success is string)
            {
                return success;
            }

            success = TrySell(player, item, amount);
            if (success is string)
            {
                return success;
            }

            ShopItem data = _config.ShopItems[item];
            ShopItem itemdata = _config.ShopItems[item];
            double cooldown = Convert.ToDouble(itemdata.SellCooldown);

            if (cooldown > 0)
            {
                Dictionary<string, double> itemCooldowns;

                if (!_sellCoolDownData.TryGetValue(player.userID, out itemCooldowns))
                {
                    _sellCoolDownData[player.userID] = itemCooldowns = new Dictionary<string, double>();
                }

                itemCooldowns[item] = CurrentTime() + cooldown;
            }

            AddCurrency(player, (int)(data.SellPrice * amount));

            if (!string.IsNullOrEmpty(data.Shortname))
            {
                ulong count;

                _soldData.TryGetValue(data.Shortname, out count);

                _soldData[data.Shortname] = count + (ulong)amount;
            }

            return true;
        }

        object TrySell(BasePlayer player, string item, int amount)
        {
            ShopItem data = _config.ShopItems[item];

            if (string.IsNullOrEmpty(data.Shortname))
            {
                return Lang("MessageErrorItemItem", player.UserIDString);
            }

            if (!data.Command.IsNullOrEmpty())
            {
                return Lang("CantSellCommands", player.UserIDString);
            }

            object iskit = Kits?.CallHook("isKit", data.Shortname);

            if (iskit is bool && (bool)iskit)
            {
                return Lang("CantSellKits", player.UserIDString);
            }

            object success = TakeItem(player, data, amount);
            if (success is string)
            {
                return success;
            }

            return true;
        }

        private int GetAmountSell(BasePlayer player, string item)
        {
            ShopItem data = _config.ShopItems[item];

            ItemDefinition definition = ItemManager.FindItemDefinition(data.Shortname);

            if (definition == null)
            {
                return 0;
            }

            return player.inventory.GetAmount(definition.itemid);
        }

        private object TakeItem(BasePlayer player, ShopItem data, int amount)
        {
            if (amount <= 0)
            {
                return Lang("MessageErrorNotEnoughSell", player.UserIDString);
            }

            ItemDefinition definition = ItemManager.FindItemDefinition(data.Shortname);

            if (definition == null)
            {
                return Lang("MessageErrorItemNoExistTake", player.UserIDString);
            }

            int pamount = player.inventory.GetAmount(definition.itemid);

            if (pamount < amount)
            {
                return Lang("MessageErrorNotEnoughSell", player.UserIDString);
            }

            player.inventory.Take(null, definition.itemid, amount);

            return true;
        }

        object CanSell(BasePlayer player, string item, int amount)
        {

            if (!_config.ShopItems.ContainsKey(item))
            {
                return Lang("MessageErrorItemNoValidsell", player.UserIDString);
            }

            ShopItem itemdata = _config.ShopItems[item];

            if (player.inventory.containerMain.FindItemsByItemName(itemdata.Shortname) == null && player.inventory.containerBelt.FindItemsByItemName(itemdata.Shortname) == null) //fixed..
            {
                return Lang("MessageErrorNotEnoughSell", player.UserIDString);
            }

            if (itemdata.SellPrice < 0)
            {
                return Lang("MessageErrorSellPrice", player.UserIDString);
            }

            if (itemdata.SellCooldown > 0)
            {
                Dictionary<string, double> itemCooldowns;

                double itemCooldown;

                if (_sellCoolDownData.TryGetValue(player.userID, out itemCooldowns) && itemCooldowns.TryGetValue(item, out itemCooldown) && itemCooldown > CurrentTime())
                {
                    return Lang("MessageErrorCooldown", player.UserIDString, FormatTime((long)(itemCooldown - CurrentTime())));
                }
            }

            return true;
        }
        #endregion

        #region Chat

        private void CmdShop(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Use))
            {
                return;
            }

            if (!_isShopReady)
            {
                SendReply(player, Lang("MessageShopResponse", player.UserIDString));
                return;
            }

            ShopCategory category;

            string shopKey;

            if (GetNearestVendor(player, out category))
                shopKey = category.DisplayName;
            else
                shopKey = _config.DefaultShop;

            if (!player.CanBuild())
            {
                if (permission.UserHasPermission(player.UserIDString, BlockAllow)) //Overrides being blocked.
                    ShowShop(player, _config.DefaultShop);
                else
                    SendReply(player, Lang("MessageErrorBuildingBlocked", player.UserIDString));

                return;
            }
            _imageChanger = _config.UIImageOption;
            _playerUIOpen.Add(player.UserIDString);
            ShowShop(player, shopKey);
        }
        [ChatCommand("cleardata")]
        private void CmdClearData(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, Admin))
            {
                _playerUIData.Clear();
                Puts($"{player.userID} has cleared the data in the GUI Shop file");
            }
        }

        #endregion

        #region Console
        [ConsoleCommand("shop.show")] //updated to fix spacing issues in name again.
        private void ConsoleShopShow(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(2))
            {
                return;
            }

            BasePlayer player = arg.Player();

            string shopid = arg.GetString(0).Replace("_", " ");

            if (shopid.Equals("close", StringComparison.OrdinalIgnoreCase))
            {
                BasePlayer targetPlayer = arg.GetPlayerOrSleeper(1);

                DestroyUi(targetPlayer, true);

                return;
            }

            if (player == null || !permission.UserHasPermission(player.UserIDString, Use))
            {
                return;
            }

            ShowShop(player, shopid, arg.GetInt(1), false, true);
        }

        [ConsoleCommand("shop.buy")]
        private void ConsoleShopBuy(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(3))
            {
                return;
            }

            BasePlayer player = arg.Player();

            if (player == null || !permission.UserHasPermission(player.UserIDString, Use))
            {
                return;
            }

            object success = Interface.Oxide.CallHook("canShop", player);

            if (success != null)
            {
                SendReply(player, success as string ?? "You are not allowed to shop at the moment");
                return;
            }
            
            string shopName = arg.GetString(0).Replace("_", " ");
            string item = arg.GetString(1).Replace("_", " ");
            int amount = arg.GetString(2).Equals("all") ? GetAmountBuy(player, item) : arg.GetInt(2);

            success = TryShopBuy(player, shopName, item, amount);

            if (success is string)
            {
                SendReply(player, (string)success);
                return;
            }

            ShopItem shopitem = _config.ShopItems.Values.FirstOrDefault(x => x.DisplayName == item);

            if (shopitem == null) return;

            SendReply(player, Lang("MessageBought", player.UserIDString), amount, shopitem.DisplayName);
            ShowShop(player, shopName, _shopPage[player.userID], false, true);
        }

        [ConsoleCommand("shop.sell")]
        private void ConsoleShopSell(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(3))
            {
                return;
            }

            BasePlayer player = arg.Player();

            if (player == null || !permission.UserHasPermission(player.UserIDString, Use))
            {
                return;
            }

            object success = Interface.Oxide.CallHook("canShop", player);

            if (success != null)
            {
                string message = "You are not allowed to shop at the moment";
                if (success is string)
                {
                    message = (string)success;
                }

                SendReply(player, message);
                return;
            }
            
            string shopName = arg.GetString(0).Replace("_", " ");
            string item = arg.GetString(1).Replace("_", " ");
            int amount = arg.GetString(2).Equals("all") ? GetAmountSell(player, item) : arg.GetInt(2);

            success = TryShopSell(player, shopName, item, amount);

            if (success is string)
            {
                SendReply(player, (string)success);
                return;
            }

            ShopItem shopitem = _config.ShopItems.Values.FirstOrDefault(x => x.DisplayName == item);
            
            if (shopitem == null ) return;

            SendReply(player, Lang("MessageSold", player.UserIDString), amount, shopitem.DisplayName);
            ShowShop(player, shopName, _shopPage[player.userID], false, true);
        }

        [ConsoleCommand("shop.transparency")]
        private void ConsoleShopTransparency(ConsoleSystem.Arg arg)
        {
            PlayerTransparencyChange(arg.Player(), arg.Args[0]);

            if (!permission.UserHasPermission(arg.Player().UserIDString, Use))
            {
                return;
            }

            if (!arg.Player().CanBuild())
            {
                if (permission.UserHasPermission(arg.Player().UserIDString, BlockAllow)) //Overrides being blocked.
                    ShowShop(arg.Player(), _config.DefaultShop);
                else
                    SendReply(arg.Player(), Lang("MessageErrorBuildingBlocked", arg.Player().UserIDString));
                return;
            }
            ShowShop(arg.Player(), arg.Args[1]);
        }

        [ConsoleCommand("shop.uicolor")]
        private void ConsoleUIColor(ConsoleSystem.Arg arg)
        {
            if (arg.Args[0] == null || arg.Args[1] == null)
            {
                return;
            }

            PlayerColorTextChange(arg.Player(), arg.Args[0], arg.Args[1], arg.Args[2], _uiSettingChange);
            if (!permission.UserHasPermission(arg.Player().UserIDString, Use)) //added vip option.
            {
                return;
            }

            if (!arg.Player().CanBuild())
            {
                if (permission.UserHasPermission(arg.Player().UserIDString, BlockAllow)) //Overrides being blocked.
                    ShowShop(arg.Player(), _config.DefaultShop);
                else
                    SendReply(arg.Player(), Lang("MessageErrorBuildingBlocked", arg.Player().UserIDString));
                return;
            }
            ShowShop(arg.Player(), arg.Args[3].Replace("_", " "));
        }

        [ConsoleCommand("shop.colorsetting")]
        private void ConsoleUIColorSetting(ConsoleSystem.Arg arg)
        {
            Puts("{0}", arg.GetString(1));
            if (!arg.HasArgs(2))
            {
                return;
            }
            
            _uiSettingChange = arg.Args[0];
            if (!permission.UserHasPermission(arg.Player().UserIDString, Use)) //added vip
            {
                return;
            }

            if (!arg.Player().CanBuild())
            {
                if (permission.UserHasPermission(arg.Player().UserIDString, BlockAllow)) //Overrides being blocked.
                    ShowShop(arg.Player(), _config.DefaultShop);
                else
                    SendReply(arg.Player(), Lang("MessageErrorBuildingBlocked", arg.Player().UserIDString));
                return;
            }
            ShowShop(arg.Player(), arg.Args[1].Replace("_", " "));
            GetSettingTypeToChange(_uiSettingChange);
        }

        [ConsoleCommand("shop.imageortext")]
        private void ConsoleUIImageOrText(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs())
            {
                return;
            }

            if (_config.PersonalUI && !permission.UserHasPermission(arg.Player().UserIDString, Vip))
            {
                ShowShop(arg.Player(), arg.Args[0].Replace("_", " "));
                return;
            }
            
            SetImageOrText(arg.Player());

            if (!arg.Player().CanBuild())
            {
                if (permission.UserHasPermission(arg.Player().UserIDString, BlockAllow)) //Overrides being blocked.
                    ShowShop(arg.Player(), _config.DefaultShop);
                else
                    SendReply(arg.Player(), Lang("MessageErrorBuildingBlocked", arg.Player().UserIDString));
                return;
            }
            
            ShowShop(arg.Player(), arg.Args[0].Replace("_", " "));
        }
        #endregion

        #region CoolDowns
        private static int CurrentTime() => Facepunch.Math.Epoch.Current;

        private bool HasBuyCooldown(ulong userID, string item, out double itemCooldown)
        {
            Dictionary<string, double> itemCooldowns;

            itemCooldown = 0.0;

            return _buyCooldownData.TryGetValue(userID, out itemCooldowns) && itemCooldowns.TryGetValue(item, out itemCooldown) && itemCooldown > CurrentTime();
        }

        private bool HasSellCooldown(ulong userID, string item, out double itemCooldown)
        {
            Dictionary<string, double> itemCooldowns;

            itemCooldown = 0.0;

            return _sellCoolDownData.TryGetValue(userID, out itemCooldowns) && itemCooldowns.TryGetValue(item, out itemCooldown) && itemCooldown > CurrentTime();
        }

        private static string FormatTime(long seconds)
        {
            TimeSpan timespan = TimeSpan.FromSeconds(seconds);

            return string.Format(timespan.TotalHours >= 1 ? "{2:00}:{0:00}:{1:00}" : "{0:00}:{1:00}", timespan.Minutes, timespan.Seconds, Math.Floor(timespan.TotalHours));
        }

        #endregion

        #region UI Colors

        private string GetSettingTypeToChange(string type)
        {
            return type == _uiSettingChange ? $"{HexToColor("#FFFFFF")} 0.2" : $"{HexToColor("#CCD1D1")} 0";
        }

        private void SetImageOrText(BasePlayer player)
        {
            PlayerUISetting playerUISetting = GetPlayerData(player);
            
            if (permission.UserHasPermission(player.UserIDString, Vip))
            {
                playerUISetting.ImageOrText = !playerUISetting.ImageOrText;
                return;
            }
            
            if (_config.PersonalUI)
            {
                playerUISetting.ImageOrText = !playerUISetting.ImageOrText;
            }
        }

        private bool GetImageOrText(BasePlayer player)
        {
            PlayerUISetting playerUISetting = GetPlayerData(player);
            if (!_config.PersonalUI && !permission.UserHasPermission(player.UserIDString, Vip))
            {
                playerUISetting.ImageOrText = false;
            }

            if (_config.UIImageOption && !permission.UserHasPermission(player.UserIDString, Vip))
            {
                playerUISetting.ImageOrText = true;
            }

            if (_config.PersonalUI && _config.UIImageOption && !permission.UserHasPermission(player.UserIDString, Vip))
            {
                playerUISetting.ImageOrText = true;
            }
            
            _imageChanger = playerUISetting.ImageOrText;
            return _imageChanger;
        }

        private string GetText(string text, string type, BasePlayer player)
        {

            if (GetImageOrText(player))
            {
                switch (type)
                {
                    case "label":
                        return "";
                    case "image":
                        return text;
                }
            }
            else
            {
                switch (type)
                {
                    case "label":
                        return text;
                    case "image":
                        return "https://i.imgur.com/fL7N8Zf.png";
                }
            }
            return "";
        }

        private double AnchorBarMath(BasePlayer uiPlayer) => (GetUITransparency(uiPlayer) / 10 - (GetUITransparency(uiPlayer) / 10 - GetPlayerData(uiPlayer).RangeValue / 1000)) * 10;

        private PlayerUISetting GetPlayerData(BasePlayer player)
        {
            PlayerUISetting playerUISetting;

            if (!_playerUIData.TryGetValue(player.UserIDString, out playerUISetting))
            {
                _playerUIData[player.UserIDString] = playerUISetting = new PlayerUISetting
                {
                    Transparency = Transparency,
                    UITextColor = $"{HexToColor("#FFFFFF")} 1",
                    SellBoxColors = $"{HexToColor("#FFFFFF")} 0.15",
                    BuyBoxColors = $"{HexToColor("#FFFFFF")} 0.15",
                    RangeValue = (Transparency - 0.9) * 100,
                    ImageOrText = _config.UIImageOption
                };
            }
            return playerUISetting;
        }

        private void PlayerTransparencyChange(BasePlayer uiPlayer, string action)
        {
            PlayerUISetting playerUISetting = GetPlayerData(uiPlayer);

            switch (action)
            {
                case "increase":
                    if (Math.Abs(playerUISetting.Transparency - 1) >= 1)
                    {
                        break;
                    }
                    playerUISetting.Transparency = playerUISetting.Transparency + 0.01;
                    playerUISetting.RangeValue = playerUISetting.RangeValue + 1;
                    break;
                case "decrease":
                    if ( Math.Abs(playerUISetting.Transparency - 0.01) <= 0.9)
                    {
                        break;
                    }
                    playerUISetting.Transparency = playerUISetting.Transparency - 0.01;
                    playerUISetting.RangeValue = playerUISetting.RangeValue - 1;
                    break;
            }
        }
        
        private double GetUITransparency(BasePlayer uiPlayer) => GetPlayerData(uiPlayer).Transparency;

        private void PlayerColorTextChange(BasePlayer uiPlayer, string textColorRed, string textColorGreen, string textColorBlue, string uiSettingToChange)
        {
            PlayerUISetting playerUISetting = GetPlayerData(uiPlayer);
            
            switch (uiSettingToChange)
            {
                case "Text":
                    playerUISetting.UITextColor = $"{textColorRed} {textColorGreen} {textColorBlue} 1";
                    break;
                case "Buy":
                    playerUISetting.BuyBoxColors = $"{textColorRed} {textColorGreen} {textColorBlue} {GetUITransparency(uiPlayer) - 0.75}";
                    break;
                case "Sell":
                    playerUISetting.SellBoxColors = $"{textColorRed} {textColorGreen} {textColorBlue} {GetUITransparency(uiPlayer) - 0.75}";
                    break;
            }
        }

        private string GetUITextColor(BasePlayer uiPlayer) => GetPlayerData(uiPlayer).UITextColor;

        private string GetUISellBoxColor(BasePlayer uiPlayer) => GetPlayerData(uiPlayer).SellBoxColors;

        private string GetUIBuyBoxColor(BasePlayer uiPlayer) => GetPlayerData(uiPlayer).BuyBoxColors;

        private static string HexToColor(string hexString)
        {
            if (hexString.IndexOf('#') != -1) hexString = hexString.Replace("#", "");

            int b = 0;
            int r = 0;
            int g = 0;

            if (hexString.Length == 6)
            {
                r = int.Parse(hexString.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                g = int.Parse(hexString.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                b = int.Parse(hexString.Substring(4, 2), NumberStyles.AllowHexSpecifier);
            }
            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255}";
        }

        #endregion

        #region Limits
        private bool LimitReached(ulong userId, string item, int amount, string ttype) //Limiter function TODO:
        {
            ItemLimit itemLimit;

            if (!_limitsData.TryGetValue(userId, out itemLimit))
            {
                itemLimit = new ItemLimit();

                _limitsData.Add(userId, itemLimit);
            }

            if (ttype == "buy" && !itemLimit.HasBuyLimit(item, amount))
            {
                itemLimit.IncrementBuy(item);

                return false;
            }

            if (ttype == "Sell" && !itemLimit.HasSellLimit(item, amount))
            {
                itemLimit.IncrementSell(item);

                return false;
            }

            return true;
        }
        #endregion

        #region NPC

        bool GetNearestVendor(BasePlayer player, out ShopCategory category) //NPC helper finished. //remove request pending 
        {
            category = null;

            Collider[] colliders = Physics.OverlapSphere(player.ServerPosition, 2.5f, playersMask);

            if (!colliders.Any())
            {
                return false;
            }

            BasePlayer npc = colliders.Select(col => col.GetComponent<BasePlayer>())
                .FirstOrDefault(x => !IsPlayer(x.userID));

            if (npc == null)
            {
                return false;
            }

            category = _config.ShopCategories.Select(x => x.Value).FirstOrDefault(i => i.EnableNPC && i.NPCId == npc.UserIDString); //updated 1.8.1

            if (category == null)
            {
                return false;
            }

            return true;
        }

        bool IsPlayer(ulong userID) => userID.IsSteamId();

        private void OnUseNPC(BasePlayer npc, BasePlayer player) //added 1.8.7
        {
            ShopCategory category = _config.ShopCategories.Select(x => x.Value).FirstOrDefault(i => i.EnableNPC && i.NPCId == npc.UserIDString);

            if (category == null)
            {
                return;
            }
            ShowShop(player, category.DisplayName);
        }
        
        private void OnEnterNPC(BasePlayer npc, BasePlayer player) //added 2.0.0
        {
            ShopCategory category = _config.ShopCategories.Select(x => x.Value).FirstOrDefault(i => i.EnableNPC && i.NPCId == npc.UserIDString);

            if (category == null)
            {
                return;
            }

            if (_config.NPCAutoOpen)
            {
                OpenShop(player, category.DisplayName, category.NPCId);
                return;
            }
            
            if (_config.NPCLeaveResponse) //added 2.0.0
            {
                SendReply(player, Lang("MessageNPCResponseopen", player.UserIDString), category.DisplayName);
            }
        }

        private void OnLeaveNPC(BasePlayer npc, BasePlayer player) //added 1.8.7
        {
            ShopCategory category = _config.ShopCategories.Select(x => x.Value).FirstOrDefault(i => i.EnableNPC && i.NPCId == npc.UserIDString);

            if (category == null)
            {
                return;
            }
            CloseShop(player);

            if (_config.NPCLeaveResponse) //added 1.8.8
            {
                SendReply(player, Lang("MessageNPCResponseclose", player.UserIDString), category.DisplayName);
            }
        }

        #endregion

        #region Helpers

        private bool IsNearMonument(BasePlayer player)
        {
            foreach (var monumentInfo in _monuments)
            {
                float distance = Vector3Ex.Distance2D(monumentInfo.transform.position, player.ServerPosition);

                if (monumentInfo.name.Contains("sphere") && distance < 30f)
                {
                    return true;
                }

                if (monumentInfo.name.Contains("launch") && distance < 30f)
                {
                    return true;
                }

                if (!monumentInfo.IsInBounds(player.ServerPosition)) continue;

                return true;
            }

            return false;
        }

        #endregion

        #region API Hooks

        private void OpenShop(BasePlayer player, string shopName, string npcID)
        {
            if (player == null || string.IsNullOrEmpty(shopName) || string.IsNullOrEmpty(npcID))
            {
                return;
            }

            ShopCategory shopCategory;

            if (!_config.ShopCategories.TryGetValue(shopName, out shopCategory) || !shopCategory.EnableNPC || shopCategory.NPCId != npcID)
            {
                return;
            }

            ShowShop(player, shopName);
        }

        private void CloseShop(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }
            CuiHelper.DestroyUi(player, ShopOverlayName);
        }

        #endregion

    }
}