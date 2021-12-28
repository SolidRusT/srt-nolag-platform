using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Crafts", "Mevent", "2.5.0")]
    public class Crafts : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary, SpawnModularCar, Notify;

        private const string Layer = "UI.Crafts";

        private static Crafts _instance;

        private List<CraftConf> _crafts = new List<CraftConf>();

        private readonly List<RecyclerComponent> _recyclers = new List<RecyclerComponent>();

        private readonly List<CarController> _cars = new List<CarController>();

        private enum WorkbenchLevel
        {
            None = 0,
            One = 1,
            Two = 2,
            Three = 3
        }

        private enum CraftType
        {
            Command,
            Vehicle,
            Item,
            Recycler,
            ModularCar
        }

        #endregion

        #region Config

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Commands = { "craft", "crafts" };

            [JsonProperty(PropertyName = "Work with Notify?")]
            public bool UseNotify = true;

            [JsonProperty(PropertyName = "Permission (ex: crafts.use)")]
            public string Permission = string.Empty;

            [JsonProperty(PropertyName = "Categories", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Category> Categories = new List<Category>
            {
                new Category
                {
                    Enabled = true,
                    Permission = string.Empty,
                    Title = "Vehicles",
                    Color = new IColor("#161617", 100),
                    Crafts = new List<CraftConf>
                    {
                        new CraftConf
                        {
                            Enabled = true,
                            Image = "https://i.imgur.com/YXjADeE.png",
                            Title = "Minicopter",
                            Description = "Fast air transport",
                            CmdToGive = "givecopter",
                            Permission = "crafts.all",
                            DisplayName = "Minicopter",
                            ShortName = "electric.flasherlight",
                            Amount = 1,
                            SkinID = 2080145158,
                            Type = CraftType.Vehicle,
                            Prefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                            Level = WorkbenchLevel.One,
                            UseDistance = true,
                            Distance = 1.5f,
                            GiveCommand = string.Empty,
                            Ground = true,
                            Structure = true,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft(string.Empty, "gears", 5, 0),
                                new ItemForCraft(string.Empty, "roadsigns", 5, 0),
                                new ItemForCraft(string.Empty, "metal.fragments", 2000, 0)
                            }
                        },
                        new CraftConf
                        {
                            Enabled = true,
                            Image = "https://i.imgur.com/dmWQOm6.png",
                            Description = "Slow water transport",
                            Title = "Row Boat",
                            CmdToGive = "giverowboat",
                            Permission = "crafts.all",
                            DisplayName = "Row Boat",
                            ShortName = "coffin.storage",
                            Amount = 1,
                            SkinID = 2080150023,
                            Type = CraftType.Vehicle,
                            Prefab = "assets/content/vehicles/boats/rowboat/rowboat.prefab",
                            Level = WorkbenchLevel.Two,
                            UseDistance = true,
                            Distance = 1.5f,
                            GiveCommand = string.Empty,
                            Ground = true,
                            Structure = true,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft(string.Empty, "gears", 5, 0),
                                new ItemForCraft(string.Empty, "roadsigns", 5, 0),
                                new ItemForCraft(string.Empty, "metal.fragments", 2000, 0)
                            }
                        },
                        new CraftConf
                        {
                            Enabled = true,
                            Image = "https://i.imgur.com/CgpVw2j.png",
                            Description = "Slow water transport",
                            Title = "RHIB",
                            CmdToGive = "giverhibboat",
                            Permission = "crafts.all",
                            DisplayName = "RHIB",
                            ShortName = "electric.sirenlight",
                            Amount = 1,
                            SkinID = 2080150770,
                            Type = CraftType.Vehicle,
                            GiveCommand = string.Empty,
                            Prefab = "assets/content/vehicles/boats/rhib/rhib.prefab",
                            Level = WorkbenchLevel.Three,
                            UseDistance = true,
                            Distance = 1.5f,
                            Ground = true,
                            Structure = true,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft(string.Empty, "gears", 5, 0),
                                new ItemForCraft(string.Empty, "roadsigns", 5, 0),
                                new ItemForCraft(string.Empty, "metal.fragments", 2000, 0)
                            }
                        },
                        new CraftConf
                        {
                            Enabled = true,
                            Image = "https://i.imgur.com/cp2Xx2A.png",
                            Title = "Hot Air Balloon",
                            Description = "Slow air transport",
                            CmdToGive = "givehotair",
                            Permission = "crafts.all",
                            DisplayName = "Hot Air Balloon",
                            ShortName = "box.repair.bench",
                            Amount = 1,
                            SkinID = 2080152635,
                            Type = CraftType.Vehicle,
                            GiveCommand = string.Empty,
                            Prefab = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab",
                            Level = WorkbenchLevel.Three,
                            UseDistance = true,
                            Distance = 1.5f,
                            Ground = true,
                            Structure = true,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft(string.Empty, "gears", 5, 0),
                                new ItemForCraft(string.Empty, "roadsigns", 5, 0),
                                new ItemForCraft(string.Empty, "metal.fragments", 2000, 0)
                            }
                        },
                        new CraftConf
                        {
                            Enabled = true,
                            Image = "https://i.imgur.com/7JZE0Lr.png",
                            Title = "Transport Helicopter",
                            Description = "Fast air transport",
                            CmdToGive = "givescrapheli",
                            Permission = "crafts.all",
                            DisplayName = "Transport Helicopter",
                            ShortName = "electric.flasherlight",
                            Amount = 1,
                            SkinID = 2080154394,
                            Type = CraftType.Vehicle,
                            GiveCommand = string.Empty,
                            Prefab = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
                            Level = WorkbenchLevel.Three,
                            UseDistance = true,
                            Distance = 1.5f,
                            Ground = true,
                            Structure = true,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft(string.Empty, "gears", 5, 0),
                                new ItemForCraft(string.Empty, "roadsigns", 5, 0),
                                new ItemForCraft(string.Empty, "metal.fragments", 2000, 0)
                            }
                        }
                    }
                },
                new Category
                {
                    Enabled = true,
                    Permission = string.Empty,
                    Title = "Cars",
                    Color = new IColor("#161617", 100),
                    Crafts = new List<CraftConf>
                    {
                        new CraftConf
                        {
                            Enabled = true,
                            Image = "https://i.imgur.com/eioxlvK.png",
                            Title = "Sedan",
                            Description = "5KM/H",
                            CmdToGive = "givesedan",
                            Permission = "crafts.all",
                            DisplayName = "Car",
                            ShortName = "woodcross",
                            Amount = 1,
                            SkinID = 2080151780,
                            Type = CraftType.Vehicle,
                            GiveCommand = string.Empty,
                            Prefab = "assets/content/vehicles/sedan_a/sedantest.entity.prefab",
                            Level = WorkbenchLevel.Two,
                            UseDistance = true,
                            Distance = 1.5f,
                            Ground = true,
                            Structure = true,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft(string.Empty, "gears", 5, 0),
                                new ItemForCraft(string.Empty, "roadsigns", 5, 0),
                                new ItemForCraft(string.Empty, "metal.fragments", 2000, 0)
                            }
                        },
                        new CraftConf
                        {
                            Enabled = true,
                            Image = "https://i.imgur.com/z7X5D5V.png",
                            Title = "Ferrari",
                            Description = "25KM/H",
                            CmdToGive = "givemod1",
                            Permission = "crafts.all",
                            DisplayName = "Car",
                            ShortName = "electric.flasherlight",
                            Amount = 1,
                            SkinID = 2244308598,
                            Type = CraftType.ModularCar,
                            Prefab = string.Empty,
                            GiveCommand = string.Empty,
                            Level = WorkbenchLevel.Two,
                            UseDistance = true,
                            Distance = 1.5f,
                            Ground = true,
                            Structure = true,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft(string.Empty, "gears", 5, 0),
                                new ItemForCraft(string.Empty, "roadsigns", 5, 0),
                                new ItemForCraft(string.Empty, "metal.fragments", 2000, 0)
                            },
                            Modular = new ModularCarConf
                            {
                                CodeLock = true,
                                KeyLock = false,
                                EnginePartsTier = 2,
                                FreshWaterAmount = 0,
                                FuelAmount = 140,
                                Modules = new[]
                                {
                                    "vehicle.1mod.engine",
                                    "vehicle.1mod.cockpit.armored",
                                    "vehicle.1mod.cockpit.armored"
                                }
                            }
                        }
                    }
                },
                new Category
                {
                    Enabled = true,
                    Permission = string.Empty,
                    Title = "Misc",
                    Color = new IColor("#161617", 100),
                    Crafts = new List<CraftConf>
                    {
                        new CraftConf
                        {
                            Enabled = true,
                            Image = "https://i.imgur.com/LLB2AVi.png",
                            Title = "Home Recycler",
                            Description = string.Empty,
                            CmdToGive = "giverecycler",
                            Permission = "crafts.all",
                            DisplayName = "Home Recycler",
                            ShortName = "research.table",
                            Amount = 1,
                            SkinID = 2186833264,
                            Type = CraftType.Recycler,
                            Prefab = "assets/bundled/prefabs/static/recycler_static.prefab",
                            GiveCommand = string.Empty,
                            Level = WorkbenchLevel.Two,
                            UseDistance = true,
                            Distance = 1.5f,
                            Ground = true,
                            Structure = true,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft(string.Empty, "gears", 5, 0),
                                new ItemForCraft(string.Empty, "roadsigns", 5, 0),
                                new ItemForCraft(string.Empty, "metal.fragments", 2000, 0)
                            }
                        },
                        new CraftConf
                        {
                            Enabled = true,
                            Image = "https://i.imgur.com/mw1T17x.png",
                            Title = string.Empty,
                            Description = string.Empty,
                            CmdToGive = "givelr300",
                            Permission = "crafts.all",
                            DisplayName = string.Empty,
                            ShortName = "rifle.lr300",
                            Amount = 1,
                            SkinID = 0,
                            Type = CraftType.Item,
                            Prefab = string.Empty,
                            GiveCommand = string.Empty,
                            Level = WorkbenchLevel.None,
                            UseDistance = true,
                            Distance = 1.5f,
                            Ground = true,
                            Structure = true,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft(string.Empty, "gears", 5, 0),
                                new ItemForCraft(string.Empty, "roadsigns", 5, 0),
                                new ItemForCraft(string.Empty, "metal.fragments", 2000, 0)
                            }
                        }
                    }
                }
            };

            [JsonProperty(PropertyName = "Workbenches Setting",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<WorkbenchLevel, IColor> Workbenches =
                new Dictionary<WorkbenchLevel, IColor>
                {
                    [WorkbenchLevel.None] = new IColor("#FFFFFF", 00),
                    [WorkbenchLevel.One] = new IColor("#74884A", 100),
                    [WorkbenchLevel.Two] = new IColor("#B19F56", 100),
                    [WorkbenchLevel.Three] = new IColor("#B43D3D", 100)
                };

            [JsonProperty(PropertyName = "Recycler Settings")]
            public RecyclerConfig Recycler = new RecyclerConfig
            {
                Speed = 5f,
                Radius = 7.5f,
                Text = "<size=19>RECYCLER</size>\n<size=15>{0}/{1}</size>",
                Color = "#C5D0E6",
                Delay = 0.75f,
                Available = true,
                Owner = true,
                Amounts = new[]
                    { 0.9f, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0.9f, 0.5f, 0.5f, 0, 1, 1, 0.5f, 0, 0, 0, 0, 0, 1, 1 },
                Scale = 0.5f,
                DDraw = true,
                Building = true
            };

            [JsonProperty(PropertyName = "Car Settings")]
            public CarConfig Car = new CarConfig
            {
                ActiveItems = new ActiveItemOptions
                {
                    Disable = true,
                    BlackList = new[]
                    {
                        "explosive.timed", "rocket.launcher", "surveycharge", "explosive.satchel"
                    }
                },
                Radius = 7.5f,
                Text = "<size=15>{0}/{1}</size>",
                Color = "#C5D0E6",
                Delay = 0.75f
            };

            [JsonProperty(PropertyName = "UI Settings")]
            public UserInterface UI = new UserInterface
            {
                CatWidth = 90,
                CatMargin = 5,
                CatHeight = 25,
                CraftWidth = 125,
                CraftHeight = 125,
                CraftMargin = 10,
                CraftYIndent = -115,
                CraftStrings = 2,
                CraftAmountOnString = 5,
                PageSize = 25,
                PageSelectedSize = 40,
                PagesMargin = 5,
                ItemMargin = 40,
                ItemWidth = 130,
                Color1 = new IColor("#0E0E10", 100),
                Color2 = new IColor("#161617", 100),
                Color3 = new IColor("#FFFFFF", 100),
                Color4 = new IColor("#4B68FF", 100),
                Color5 = new IColor("#74884A", 100),
                Color6 = new IColor("#CD4632", 100),
                Color7 = new IColor("#595651", 100),
                BackgroundImage = string.Empty
            };

            public VersionNumber Version;
        }

        private class UserInterface
        {
            [JsonProperty(PropertyName = "Category Width")]
            public float CatWidth;

            [JsonProperty(PropertyName = "Category Height")]
            public float CatHeight;

            [JsonProperty(PropertyName = "Category Margin")]
            public float CatMargin;

            [JsonProperty(PropertyName = "Craft Width")]
            public float CraftWidth;

            [JsonProperty(PropertyName = "Craft Height")]
            public float CraftHeight;

            [JsonProperty(PropertyName = "Craft Margin")]
            public float CraftMargin;

            [JsonProperty(PropertyName = "Craft Y Indent")]
            public float CraftYIndent;

            [JsonProperty(PropertyName = "Craft Amount On String")]
            public int CraftAmountOnString;

            [JsonProperty(PropertyName = "Craft Strings")]
            public int CraftStrings;

            [JsonProperty(PropertyName = "Page Size")]
            public float PageSize;

            [JsonProperty(PropertyName = "Page Selected Size")]
            public float PageSelectedSize;

            [JsonProperty(PropertyName = "Pages Margin")]
            public float PagesMargin;

            [JsonProperty(PropertyName = "Item Width")]
            public float ItemWidth;

            [JsonProperty(PropertyName = "Item Margin")]
            public float ItemMargin;

            [JsonProperty(PropertyName = "Color 1")]
            public IColor Color1;

            [JsonProperty(PropertyName = "Color 2")]
            public IColor Color2;

            [JsonProperty(PropertyName = "Color 3")]
            public IColor Color3;

            [JsonProperty(PropertyName = "Color 4")]
            public IColor Color4;

            [JsonProperty(PropertyName = "Color 5")]
            public IColor Color5;

            [JsonProperty(PropertyName = "Color 6")]
            public IColor Color6;

            [JsonProperty(PropertyName = "Color 7")]
            public IColor Color7;

            [JsonProperty(PropertyName = "Background Image")]
            public string BackgroundImage;
        }

        private class Category
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = "Permission (ex: crafts.vip)")]
            public string Permission;

            [JsonProperty(PropertyName = "Title")] public string Title;

            [JsonProperty(PropertyName = "Background color")]
            public IColor Color;

            [JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<CraftConf> Crafts;
        }

        private class CraftConf
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = "Image")] public string Image;

            [JsonProperty(PropertyName = "Title")] public string Title;

            [JsonProperty(PropertyName = "Description")]
            public string Description;

            [JsonProperty(PropertyName = "Command (to give an item)")]
            public string CmdToGive;

            [JsonProperty(PropertyName = "Permission (ex: crafts.vip)")]
            public string Permission;

            [JsonProperty(PropertyName = "Display Name")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Shortname")]
            public string ShortName;

            [JsonProperty(PropertyName = "Amount")]
            public int Amount;

            [JsonProperty(PropertyName = "Skin")] public ulong SkinID;

            [JsonProperty(PropertyName = "Type (Item/Command/Vehicle/Recycler)")]
            [JsonConverter(typeof(StringEnumConverter))]
            public CraftType Type;

            [JsonProperty(PropertyName = "Prefab")]
            public string Prefab;

            [JsonProperty(PropertyName = "Command on give")]
            public string GiveCommand;

            [JsonProperty(PropertyName = "Workbench Level")]
            public WorkbenchLevel Level;

            [JsonProperty(PropertyName = "Distance Check")]
            public bool UseDistance;

            [JsonProperty(PropertyName = "Distance")]
            public float Distance;

            [JsonProperty(PropertyName = "Place the ground")]
            public bool Ground;

            [JsonProperty(PropertyName = "Place the structure")]
            public bool Structure;

            [JsonProperty(PropertyName = "Items For Craft",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ItemForCraft> Items;

            [JsonProperty(PropertyName = "For Modular Car")]
            public ModularCarConf Modular;

            [JsonIgnore] private int _id = -1;

            [JsonIgnore]
            public int ID
            {
                get
                {
                    if (_id == -1)
                        _id = Random.Range(int.MinValue, int.MaxValue);

                    return _id;
                }
            }

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

            public Item ToItem()
            {
                var newItem = ItemManager.CreateByName(ShortName, Amount, SkinID);
                if (newItem == null)
                {
                    Debug.LogError($"Error creating item with ShortName: '{ShortName}'");
                    return null;
                }

                if (!string.IsNullOrEmpty(DisplayName)) newItem.name = DisplayName;

                return newItem;
            }

            public void Give(BasePlayer player)
            {
                if (player == null) return;

                var item = ToItem();
                if (item == null) return;

                player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
            }

            public void Spawn(BasePlayer player, Vector3 pos, Quaternion rot)
            {
                switch (Type)
                {
                    case CraftType.ModularCar:
                    {
                        _instance?.SpawnModularCar?.Call("API_SpawnPresetCar", player, Modular.Get());
                        break;
                    }
                    case CraftType.Vehicle:
                    {
                        var entity = GameManager.server.CreateEntity(Prefab, pos,
                            Quaternion.Euler(0, player.GetNetworkRotation().eulerAngles.y - 90, 0));
                        if (entity == null) return;

                        entity.skinID = SkinID;
                        entity.OwnerID = player.userID;
                        entity.Spawn();
                        break;
                    }
                    default:
                    {
                        var entity = GameManager.server.CreateEntity(Prefab, pos, rot);
                        if (entity == null) return;

                        entity.skinID = SkinID;
                        entity.OwnerID = player.userID;
                        entity.Spawn();
                        break;
                    }
                }
            }
        }

        private class IColor
        {
            [JsonProperty(PropertyName = "HEX")] public string Hex;

            [JsonProperty(PropertyName = "Opacity (0 - 100)")]
            public float Alpha;

            public string Get()
            {
                if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

                var str = Hex.Trim('#');
                if (str.Length != 6) throw new Exception(Hex);
                var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

                return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
            }

            public IColor(string hex, float alpha)
            {
                Hex = hex;
                Alpha = alpha;
            }
        }

        private class ItemForCraft
        {
            [JsonProperty(PropertyName = "Image")] public string Image;

            [JsonProperty(PropertyName = "Shortname")]
            public string ShortName;

            [JsonProperty(PropertyName = "Amount")]
            public int Amount;

            [JsonProperty(PropertyName = "Skin")] public ulong SkinID;

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

            public ItemForCraft(string image, string shortname, int amount, ulong skin)
            {
                Image = image;
                ShortName = shortname;
                Amount = amount;
                SkinID = skin;
            }
        }

        private class ModularCarConf
        {
            [JsonProperty(PropertyName = "CodeLock")]
            public bool CodeLock;

            [JsonProperty(PropertyName = "KeyLock")]
            public bool KeyLock;

            [JsonProperty(PropertyName = "Engine Parts Tier")]
            public int EnginePartsTier;

            [JsonProperty(PropertyName = "Fresh Water Amount")]
            public int FreshWaterAmount;

            [JsonProperty(PropertyName = "Fuel Amount")]
            public int FuelAmount;

            [JsonProperty(PropertyName = "Modules", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Modules;

            public Dictionary<string, object> Get()
            {
                return new Dictionary<string, object>
                {
                    ["CodeLock"] = CodeLock,
                    ["KeyLock"] = KeyLock,
                    ["EnginePartsTier"] = EnginePartsTier,
                    ["FreshWaterAmount"] = FreshWaterAmount,
                    ["FuelAmount"] = FuelAmount,
                    ["Modules"] = Modules
                };
            }
        }

        private class CarConfig
        {
            [JsonProperty(PropertyName = "Active Items (in hand)")]
            public ActiveItemOptions ActiveItems;

            [JsonProperty(PropertyName = "DDraw Radius")]
            public float Radius;

            [JsonProperty(PropertyName = "DDraw Text")]
            public string Text;

            [JsonProperty(PropertyName = "DDraw Color")]
            public string Color;

            [JsonProperty(PropertyName = "DDraw Delay (sec)")]
            public float Delay;
        }

        public class ActiveItemOptions
        {
            [JsonProperty(PropertyName = "Forbid to hold all items")]
            public bool Disable;

            [JsonProperty(PropertyName = "List of blocked items (shortname)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] BlackList;
        }

        private class RecyclerConfig
        {
            [JsonProperty(PropertyName = "Recycling speed")]
            public float Speed;

            [JsonProperty(PropertyName = "Use DDraw? (showing damage on the recycler)")]
            public bool DDraw;

            [JsonProperty(PropertyName = "DDraw Radius")]
            public float Radius;

            [JsonProperty(PropertyName = "DDraw Text")]
            public string Text;

            [JsonProperty(PropertyName = "DDraw Color")]
            public string Color;

            [JsonProperty(PropertyName = "DDraw Delay (sec)")]
            public float Delay;

            [JsonProperty(PropertyName = "Enabled pickup?")]
            public bool Available;

            [JsonProperty(PropertyName = "Only owner can pickup")]
            public bool Owner;

            [JsonProperty(PropertyName = "Check ability to build for pickup")]
            public bool Building;

            [JsonProperty(PropertyName = "BaseProtection Settings",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public float[] Amounts;

            [JsonProperty(PropertyName = "Damage Scale")]
            public float Scale;
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

            if (_config.Version < new VersionNumber(2, 4, 0))
                _config.Categories.ForEach(cat => { cat.Color = new IColor("#161617", 100); });

            _config.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Hooks

        private void OnServerInitialized(bool initial)
        {
            _instance = this;

            _crafts = _config.Categories.SelectMany(x => x.Crafts).ToList();

            LoadImages();

            RegisterPermissions();

            RegisterCommands();

            if (!SpawnModularCar && _crafts.Exists(x => x.Enabled && x.Type == CraftType.ModularCar))
                PrintError("SpawnModularCar IS NOT INSTALLED.");

            if (!initial)
                foreach (var ent in BaseNetworkable.serverEntities.OfType<BaseCombatEntity>())
                    OnEntitySpawned(ent);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);

            Array.ForEach(_recyclers.ToArray(), recycler =>
            {
                if (recycler != null)
                    recycler.Kill();
            });

            Array.ForEach(_cars.ToArray(), car =>
            {
                if (car != null)
                    car.Kill();
            });

            _instance = null;
            _config = null;
        }

        private void OnEntityBuilt(Planner held, GameObject go)
        {
            if (held == null || go == null) return;

            var player = held.GetOwnerPlayer();
            if (player == null) return;

            var entity = go.ToBaseEntity();
            if (entity == null || entity.skinID == 0) return;

            var craft = _crafts.Find(x =>
                (x.Type == CraftType.Vehicle || x.Type == CraftType.Recycler || x.Type == CraftType.ModularCar) &&
                x.SkinID == entity.skinID);
            if (craft == null) return;

            var transform = entity.transform;

            var itemName = craft.PublicTitle;

            NextTick(() =>
            {
                if (entity != null)
                    entity.Kill();
            });

            RaycastHit rHit;
            if (Physics.Raycast(transform.position + new Vector3(0, 0.1f, 0), Vector3.down, out rHit, 4f,
                LayerMask.GetMask("Construction")) && rHit.GetEntity() != null)
            {
                if (!craft.Structure)
                {
                    Reply(player, OnStruct, itemName);
                    GiveCraft(player, craft);
                    return;
                }
            }
            else
            {
                if (!craft.Ground)
                {
                    Reply(player, OnGround, itemName);
                    GiveCraft(player, craft);
                    return;
                }
            }

            if (craft.UseDistance && Vector3.Distance(player.ServerPosition, transform.position) < craft.Distance)
            {
                Reply(player, BuildDistance, craft.Distance);
                GiveCraft(player, craft);
                return;
            }

            craft.Spawn(player, transform.position, transform.rotation);
        }

        private object CanResearchItem(BasePlayer player, Item item)
        {
            if (player == null || item == null ||
                !_crafts.Exists(x => x.Type == CraftType.Vehicle && x.SkinID == item.skin)) return null;
            return false;
        }

        private void OnEntitySpawned(BaseCombatEntity entity)
        {
            if (entity == null) return;

            if (entity is Recycler)
                entity.gameObject.AddComponent<RecyclerComponent>();

            if (entity is BasicCar)
                entity.gameObject.AddComponent<CarController>();
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || entity.OwnerID == 0) return;

            var recycler = entity.GetComponent<RecyclerComponent>();
            if (recycler != null)
            {
                info.damageTypes.ScaleAll(_config.Recycler.Scale);
                recycler.DDraw();
            }

            var car = entity.GetComponent<CarController>();
            if (car != null)
            {
                car.ManageDamage(info);
                car.DDraw();
            }
        }

        private object OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            if (recycler == null || player == null) return null;

            var component = recycler.GetComponent<RecyclerComponent>();
            if (component == null) return null;

            if (!recycler.IsOn())
            {
                foreach (var obj in recycler.inventory.itemList)
                    obj.CollectedForCrafting(player);

                component.StartRecycling();
            }
            else
            {
                component.StopRecycling();
            }

            return false;
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null) return;

            var entity = info.HitEntity;
            if (entity == null) return;

            var component = entity.GetComponent<RecyclerComponent>();
            if (component == null) return;

            if (!_config.Recycler.Available)
            {
                Reply(player, NotTake);
                return;
            }

            component.TryPickup(player);
        }

        #endregion

        #region Commands

        private void CmdOpenCrafts(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (!string.IsNullOrEmpty(_config.Permission) &&
                !permission.UserHasPermission(player.UserIDString, _config.Permission))
            {
                SendNotify(player, NoPermission, 1);
                return;
            }

            MainUi(player, first: true);
        }

        private void CmdGiveCrafts(IPlayer cov, string command, string[] args)
        {
            if (!cov.IsAdmin) return;

            if (args.Length == 0)
            {
                cov?.Reply($"Error syntax! Use: /{command} [name/steamId]");
                return;
            }

            var craft = _crafts.Find(x => x.CmdToGive == command);
            if (craft == null) return;

            var target = covalence.Players.FindPlayer(args[0])?.Object as BasePlayer;
            if (target == null)
            {
                PrintError($"Player '{args[0]}' not found!");
                return;
            }

            GiveCraft(target, craft);
            SendNotify(target, GotCraft, 0, craft.PublicTitle);
        }

        [ConsoleCommand("UI_Crafts")]
        private void CmdConsoleCrafts(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs()) return;

            switch (arg.Args[0])
            {
                case "page":
                {
                    int category, page = 0;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out category)) return;

                    if (arg.HasArgs(3))
                        int.TryParse(arg.Args[2], out page);

                    MainUi(player, category, page);
                    break;
                }

                case "back":
                {
                    int category, page;
                    if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page)) return;

                    MainUi(player, category, page, true);
                    break;
                }

                case "trycraft":
                {
                    int category, page, itemId;
                    if (!arg.HasArgs(4) || !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[3], out itemId)) return;

                    CraftUi(player, category, page, itemId);
                    break;
                }

                case "craft":
                {
                    int category, page, itemId;
                    if (!arg.HasArgs(4) || !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[3], out itemId)) return;

                    var craft = GetPlayerCategories(player)[category].Crafts.Find(x => x.ID == itemId);
                    if (craft == null) return;

                    if (!HasWorkbench(player, craft.Level))
                    {
                        Reply(player, NotWorkbench);
                        return;
                    }

                    var allItems = player.inventory.AllItems();

                    if (craft.Items.Exists(item => !HasAmount(allItems, item.ShortName, item.SkinID, item.Amount)))
                    {
                        SendNotify(player, NotEnoughResources, 1);
                        return;
                    }

                    craft.Items.ForEach(item => Take(allItems, item.ShortName, item.SkinID, item.Amount));

                    GiveCraft(player, craft);
                    SendNotify(player, SuccessfulCraft, 0, craft.PublicTitle);
                    break;
                }
            }
        }

        #endregion

        #region Interface

        private void MainUi(BasePlayer player, int category = 0, int page = 0, bool first = false)
        {
            var categories = GetPlayerCategories(player);
            if (categories == null) return;

            var container = new CuiElementContainer();

            #region Background

            int totalAmount;
            float margin;
            float ySwitch;
            float height;

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
                        Close = Layer
                    }
                }, Layer);

                #region Workbenches

                totalAmount = 3;
                height = 15f;
                margin = 5f;

                ySwitch = (totalAmount * height + (totalAmount - 1) * margin) / 2f;

                for (var wb = 1; wb <= totalAmount; wb++)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                            OffsetMin = $"-235 {ySwitch - height}",
                            OffsetMax = $"0 {ySwitch}"
                        },
                        Image =
                        {
                            Color = "0 0 0 0"
                        }
                    }, Layer, Layer + $".Workbench.{wb}");

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0.5", AnchorMax = "0 0.5",
                            OffsetMin = "0 -1.5", OffsetMax = "40 1.5"
                        },
                        Image =
                        {
                            Color = _config.Workbenches[(WorkbenchLevel)wb].Get()
                        }
                    }, Layer + $".Workbench.{wb}");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "45 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, WorkbenchLvl, wb),
                            Align = TextAnchor.MiddleLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = "1 1 1 1"
                        }
                    }, Layer + $".Workbench.{wb}");

                    ySwitch = ySwitch - height - margin;
                }

                #endregion

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "-200 0", OffsetMax = "200 130"
                    },
                    Text =
                    {
                        Text = Msg(player, CraftsDescription),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Color = "1 1 1 0.5"
                    }
                }, Layer);

                if (!string.IsNullOrEmpty(_config.UI.BackgroundImage))
                    container.Add(new CuiElement
                    {
                        Parent = Layer,
                        Components =
                        {
                            new CuiRawImageComponent
                                { Png = ImageLibrary.Call<string>("GetImage", _config.UI.BackgroundImage) },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1"
                            }
                        }
                    });
            }

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-350 -225",
                    OffsetMax = "350 225"
                },
                Image =
                {
                    Color = _config.UI.Color1.Get()
                }
            }, Layer, Layer + ".Main");

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50",
                    OffsetMax = "0 0"
                },
                Image = { Color = _config.UI.Color2.Get() }
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
                    Color = _config.UI.Color3.Get()
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
                    Color = _config.UI.Color3.Get()
                },
                Button =
                {
                    Close = Layer,
                    Color = _config.UI.Color4.Get()
                }
            }, Layer + ".Header");

            #endregion

            #region Categories

            var width = _config.UI.CatWidth;
            margin = _config.UI.CatMargin;

            var xSwitch = 25f;

            for (var i = 0; i < categories.Count; i++)
            {
                var cat = categories[i];

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{xSwitch} {-70 - _config.UI.CatHeight}",
                        OffsetMax = $"{xSwitch + width} -70"
                    },
                    Text =
                    {
                        Text = $"{cat.Title}",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 12,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = i == category ? _config.UI.Color4.Get() : cat.Color.Get(),
                        Command = $"UI_Crafts page {i} 0"
                    }
                }, Layer + ".Main");

                xSwitch += width + margin;
            }

            #endregion

            #region Crafts

            width = _config.UI.CraftWidth;
            height = _config.UI.CraftHeight;
            margin = _config.UI.CraftMargin;

            var amountOnString = _config.UI.CraftAmountOnString;
            var lines = _config.UI.CraftStrings;
            totalAmount = amountOnString * lines;

            xSwitch = -(amountOnString * width + (amountOnString - 1) * margin) / 2f;
            ySwitch = _config.UI.CraftYIndent;

            var playerCrafts = GetPlayerCrafts(player, categories[category]);
            var crafts = playerCrafts.Skip(page * totalAmount).Take(totalAmount)
                .ToList();
            if (crafts.Count > 0)
                for (var i = 0; i < crafts.Count; i++)
                {
                    var craft = crafts[i];

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = $"{xSwitch} {ySwitch - height}",
                            OffsetMax = $"{xSwitch + width} {ySwitch}"
                        },
                        Image =
                        {
                            Color = _config.UI.Color2.Get()
                        }
                    }, Layer + ".Main", Layer + $".Craft.{i}");

                    if (ImageLibrary)
                        container.Add(new CuiElement
                        {
                            Parent = Layer + $".Craft.{i}",
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = !string.IsNullOrEmpty(craft.Image)
                                        ? ImageLibrary.Call<string>("GetImage", craft.Image)
                                        : ImageLibrary.Call<string>("GetImage", craft.ShortName, craft.SkinID)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                                    OffsetMin = "-36 -84", OffsetMax = "36 -12"
                                }
                            }
                        });

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = "0 -100", OffsetMax = "0 -84"
                        },
                        Text =
                        {
                            Text = $"{craft.PublicTitle}",
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 14,
                            Color = "1 1 1 1"
                        }
                    }, Layer + $".Craft.{i}");

                    if (!string.IsNullOrEmpty(craft.Description))
                        container.Add(new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "0 0", OffsetMax = "0 -100"
                            },
                            Text =
                            {
                                Text = $"{craft.Description}",
                                Align = TextAnchor.UpperCenter,
                                Font = "robotocondensed-regular.ttf",
                                FontSize = 10,
                                Color = "1 1 1 0.5"
                            }
                        }, Layer + $".Craft.{i}");

                    if (craft.Level > 0)
                        container.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 0",
                                OffsetMin = "0 0", OffsetMax = "0 2"
                            },
                            Image =
                            {
                                Color = _config.Workbenches[craft.Level].Get()
                            }
                        }, Layer + $".Craft.{i}");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = "" },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = $"UI_Crafts trycraft {category} {page} {craft.ID}"
                        }
                    }, Layer + $".Craft.{i}");

                    if ((i + 1) % amountOnString == 0)
                    {
                        ySwitch = ySwitch - height - margin;
                        xSwitch = -(amountOnString * width + (amountOnString - 1) * margin) / 2f;
                    }
                    else
                    {
                        xSwitch += width + margin;
                    }
                }

            #endregion

            #region Pages

            var pageSize = _config.UI.PageSize;
            var selPageSize = _config.UI.PageSelectedSize;
            margin = _config.UI.PagesMargin;

            var pages = Mathf.CeilToInt((float)playerCrafts.Count / totalAmount);
            if (pages > 1)
            {
                xSwitch = -((pages - 1) * pageSize + (pages - 1) * margin + selPageSize) / 2f;

                for (var j = 0; j < pages; j++)
                {
                    var selected = page == j;

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                            OffsetMin = $"{xSwitch} 10",
                            OffsetMax =
                                $"{xSwitch + (selected ? selPageSize : pageSize)} {10 + (selected ? selPageSize : pageSize)}"
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
                            Color = _config.UI.Color4.Get(),
                            Command = $"UI_Crafts page {category} {j}"
                        }
                    }, Layer + ".Main");

                    xSwitch += (selected ? selPageSize : pageSize) + margin;
                }
            }

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Main");
            CuiHelper.AddUi(player, container);
        }

        private void CraftUi(BasePlayer player, int category, int page, int itemId)
        {
            var craft = GetPlayerCategories(player)[category].Crafts.Find(x => x.ID == itemId);
            if (craft == null) return;

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
                    Command = $"UI_Crafts back {category} {page}"
                }
            }, Layer);

            if (!string.IsNullOrEmpty(_config.UI.BackgroundImage))
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiRawImageComponent
                            { Png = ImageLibrary.Call<string>("GetImage", _config.UI.BackgroundImage) },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });

            #endregion

            #region Title

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-250 -230", OffsetMax = "250 -165"
                },
                Text =
                {
                    Text = Msg(player, CraftTitle, craft.PublicTitle.ToUpper()),
                    Align = TextAnchor.LowerCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 24,
                    Color = "1 1 1 0.4"
                }
            }, Layer);

            #endregion

            #region Items

            var width = _config.UI.ItemWidth;
            var margin = _config.UI.ItemMargin;

            var notItem = false;

            var xSwitch = -(craft.Items.Count * width + (craft.Items.Count - 1) * margin) /
                          2f;
            craft.Items.ForEach(item =>
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = $"{xSwitch} -450",
                        OffsetMax = $"{xSwitch + width} -285"
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
                        {
                            Png =
                                !string.IsNullOrEmpty(item.Image)
                                    ? ImageLibrary.Call<string>("GetImage", item.Image)
                                    : ImageLibrary.Call<string>("GetImage", item.ShortName, item.SkinID)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-64 -128", OffsetMax = "64 0"
                        }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0",
                        OffsetMin = "-100 10", OffsetMax = "100 30"
                    },
                    Text =
                    {
                        Text = Msg(player, CraftItemFormat, item.PublicTitle, item.Amount),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 16,
                        Color = "1 1 1 0.5"
                    }
                }, Layer + $".Item.{xSwitch}");

                var hasAmount = HasAmount(allItems, item.ShortName, item.SkinID, item.Amount);

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
                        Color = hasAmount ? _config.UI.Color5.Get() : _config.UI.Color6.Get(),
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    }
                }, Layer + $".Item.{xSwitch}");

                xSwitch += width + margin;
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
                    Text = Msg(player, CraftButton),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Color = notItem ? "1 1 1 0.7" : "1 1 1 1"
                },
                Button =
                {
                    Color = notItem ? _config.UI.Color7.Get() : _config.UI.Color4.Get(),
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                    Command = notItem ? "" : $"UI_Crafts craft {category} {page} {itemId}",
                    Close = Layer
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
                    Text = Msg(player, CraftCancelButton),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Color = "1 1 1 0.7"
                },
                Button =
                {
                    Color = _config.UI.Color7.Get(),
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                    Close = Layer,
                    Command = $"UI_Crafts back {category} {page}"
                }
            }, Layer);

            #endregion

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Utils

        private void LoadImages()
        {
            if (!ImageLibrary)
            {
                PrintError("IMAGE LIBRARY IS NOT INSTALLED!");
            }
            else
            {
                var imagesList = new Dictionary<string, string>();

                var itemIcons = new List<KeyValuePair<string, ulong>>();

                if (!string.IsNullOrEmpty(_config.UI.BackgroundImage) &&
                    !imagesList.ContainsKey(_config.UI.BackgroundImage))
                    imagesList.Add(_config.UI.BackgroundImage, _config.UI.BackgroundImage);

                _crafts.ForEach(craft =>
                {
                    if (!string.IsNullOrEmpty(craft.Image)
                        && !imagesList.ContainsKey(craft.Image))
                        imagesList.Add(craft.Image, craft.Image);

                    craft.Items.ForEach(item =>
                    {
                        if (!string.IsNullOrEmpty(item.Image)
                            && !imagesList.ContainsKey(item.Image))
                            imagesList.Add(item.Image, item.Image);

                        itemIcons.Add(new KeyValuePair<string, ulong>(item.ShortName, item.SkinID));
                    });

                    itemIcons.Add(new KeyValuePair<string, ulong>(craft.ShortName, craft.SkinID));
                });

                if (itemIcons.Count > 0) ImageLibrary?.Call("LoadImageList", Title, itemIcons, null);

                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
            }
        }

        private void RegisterPermissions()
        {
            if (!string.IsNullOrEmpty(_config.Permission) && !permission.PermissionExists(_config.Permission))
                permission.RegisterPermission(_config.Permission, this);

            _config.Categories.ForEach(category =>
            {
                if (!string.IsNullOrEmpty(category.Permission) && !permission.PermissionExists(category.Permission))
                    permission.RegisterPermission(category.Permission, this);

                category.Crafts.ForEach(item =>
                {
                    if (!string.IsNullOrEmpty(item.Permission) && !permission.PermissionExists(item.Permission))
                        permission.RegisterPermission(item.Permission, this);
                });
            });
        }

        private void RegisterCommands()
        {
            AddCovalenceCommand(_config.Commands, nameof(CmdOpenCrafts));

            AddCovalenceCommand(
                _crafts.FindAll(x => !string.IsNullOrEmpty(x.CmdToGive)).Select(x => x.CmdToGive).ToArray(),
                nameof(CmdGiveCrafts));
        }

        private List<Category> GetPlayerCategories(BasePlayer player)
        {
            return _config.Categories.FindAll(cat =>
                cat.Enabled && (string.IsNullOrEmpty(cat.Permission) ||
                                permission.UserHasPermission(player.UserIDString, cat.Permission)));
        }

        private List<CraftConf> GetPlayerCrafts(BasePlayer player, Category category)
        {
            return category.Crafts.FindAll(craft => craft.Enabled && (string.IsNullOrEmpty(craft.Permission) ||
                                                                      permission.UserHasPermission(player.UserIDString,
                                                                          craft.Permission)));
        }

        private static Color HexToUnityColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8) throw new Exception(hex);

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return color;
        }

        private static void SetPlayerFlag(BasePlayer player, BasePlayer.PlayerFlags f, bool b)
        {
            if (b)
            {
                if (player.HasPlayerFlag(f)) return;
                player.playerFlags |= f;
            }
            else
            {
                if (!player.HasPlayerFlag(f)) return;
                player.playerFlags &= ~f;
            }

            player.SendNetworkUpdateImmediate();
        }

        private void GiveCraft(BasePlayer player, CraftConf cfg)
        {
            switch (cfg.Type)
            {
                case CraftType.Command:
                {
                    var command = cfg.GiveCommand.Replace("\n", "|")
                        .Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace(
                            "%username%",
                            player.displayName, StringComparison.OrdinalIgnoreCase);

                    foreach (var check in command.Split('|')) Server.Command(check);
                    break;
                }
                default:
                {
                    cfg.Give(player);
                    break;
                }
            }
        }

        private static bool HasAmount(Item[] items, string shortname, ulong skin, int amount)
        {
            return ItemCount(items, shortname, skin) >= amount;
        }

        private static int ItemCount(Item[] items, string shortname, ulong skin)
        {
            return Array.FindAll(items, item =>
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

        private static bool HasWorkbench(BasePlayer player, WorkbenchLevel level)
        {
            return level == WorkbenchLevel.Three ? player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench3)
                : level == WorkbenchLevel.Two ? player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench3) ||
                                                player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench2)
                : level == WorkbenchLevel.One ? player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench3) ||
                                                player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench2) ||
                                                player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench1)
                : level == WorkbenchLevel.None;
        }

        #endregion

        #region Components

        #region Recycler Component

        private class RecyclerComponent : FacepunchBehaviour
        {
            private Recycler _recycler;

            private readonly BaseEntity[] _sensesResults = new BaseEntity[64];

            private void Awake()
            {
                _recycler = GetComponent<Recycler>();

                _instance?._recyclers.Add(this);

                if (_recycler.OwnerID != 0)
                {
                    _recycler.baseProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
                    _recycler.baseProtection.amounts = _config.Recycler.Amounts;
                }

                _recycler.gameObject.AddComponent<GroundWatch>();
                _recycler.gameObject.AddComponent<DestroyOnGroundMissing>();
            }

            public void DDraw()
            {
                if (_recycler == null)
                {
                    Kill();
                    return;
                }

                if (_recycler.OwnerID == 0 || !_config.Recycler.DDraw)
                    return;

                var inSphere = BaseEntity.Query.Server.GetInSphere(_recycler.transform.position,
                    _config.Recycler.Radius,
                    _sensesResults, entity => entity is BasePlayer);
                if (inSphere == 0)
                    return;

                for (var i = 0; i < inSphere; i++)
                {
                    var user = _sensesResults[i] as BasePlayer;
                    if (user == null || user.IsDestroyed || !user.IsConnected || user.IsNpc ||
                        !user.userID.IsSteamId()) continue;

                    if (user.Connection.authLevel < 2) SetPlayerFlag(user, BasePlayer.PlayerFlags.IsAdmin, true);

                    user.SendConsoleCommand("ddraw.text", _config.Recycler.Delay,
                        HexToUnityColor(_config.Recycler.Color),
                        _recycler.transform.position + Vector3.up,
                        string.Format(_config.Recycler.Text, _recycler.health, _recycler._maxHealth));

                    if (user.Connection.authLevel < 2) SetPlayerFlag(user, BasePlayer.PlayerFlags.IsAdmin, false);
                }
            }

            #region Methods

            public void StartRecycling()
            {
                if (_recycler.IsOn())
                    return;

                InvokeRepeating(RecycleThink, _config.Recycler.Speed, _config.Recycler.Speed);
                Effect.server.Run(_recycler.startSound.resourcePath, _recycler, 0U, Vector3.zero, Vector3.zero);
                _recycler.SetFlag(BaseEntity.Flags.On, true);

                _recycler.SendNetworkUpdateImmediate();
            }

            public void StopRecycling()
            {
                CancelInvoke(RecycleThink);

                if (!_recycler.IsOn())
                    return;

                Effect.server.Run(_recycler.stopSound.resourcePath, _recycler, 0U, Vector3.zero, Vector3.zero);
                _recycler.SetFlag(BaseEntity.Flags.On, false);
                _recycler.SendNetworkUpdateImmediate();
            }

            public void RecycleThink()
            {
                var flag = false;
                var num1 = _recycler.recycleEfficiency;
                for (var slot1 = 0; slot1 < 6; ++slot1)
                {
                    var slot2 = _recycler.inventory.GetSlot(slot1);
                    if (slot2 != null)
                    {
                        if (Interface.CallHook("OnRecycleItem", _recycler, slot2) != null)
                        {
                            if (HasRecyclable())
                                return;
                            StopRecycling();
                            return;
                        }

                        if (slot2.info.Blueprint != null)
                        {
                            if (slot2.hasCondition)
                                num1 = Mathf.Clamp01(
                                    num1 * Mathf.Clamp(slot2.conditionNormalized * slot2.maxConditionNormalized, 0.1f,
                                        1f));
                            var num2 = 1;
                            if (slot2.amount > 1)
                                num2 = Mathf.CeilToInt(Mathf.Min(slot2.amount, slot2.info.stackable * 0.1f));
                            if (slot2.info.Blueprint.scrapFromRecycle > 0)
                            {
                                var iAmount = slot2.info.Blueprint.scrapFromRecycle * num2;
                                if (slot2.info.stackable == 1 && slot2.hasCondition)
                                    iAmount = Mathf.CeilToInt(iAmount * slot2.conditionNormalized);
                                if (iAmount >= 1)
                                    _recycler.MoveItemToOutput(ItemManager.CreateByName("scrap", iAmount));
                            }

                            if (!string.IsNullOrEmpty(slot2.info.Blueprint.RecycleStat))
                            {
                                var list = Pool.GetList<BasePlayer>();
                                Vis.Entities(transform.position, 3f, list, 131072);
                                foreach (var basePlayer in list)
                                    if (basePlayer.IsAlive() && !basePlayer.IsSleeping() &&
                                        basePlayer.inventory.loot.entitySource == _recycler)
                                    {
                                        basePlayer.stats.Add(slot2.info.Blueprint.RecycleStat, num2,
                                            Stats.Steam | Stats.Life);
                                        basePlayer.stats.Save();
                                    }

                                Pool.FreeList(ref list);
                            }

                            slot2.UseItem(num2);
                            using (var enumerator = slot2.info.Blueprint.ingredients.GetEnumerator())
                            {
                                while (enumerator.MoveNext())
                                {
                                    var current = enumerator.Current;
                                    if (current != null && current.itemDef.shortname != "scrap")
                                    {
                                        var num3 = current.amount / slot2.info.Blueprint.amountToCreate;
                                        var num4 = 0;
                                        if (num3 <= 1.0)
                                        {
                                            for (var index = 0; index < num2; ++index)
                                                if (Core.Random.Range(0.0f, 1f) <= num3 * (double)num1)
                                                    ++num4;
                                        }
                                        else
                                        {
                                            num4 = Mathf.CeilToInt(
                                                Mathf.Clamp(num3 * num1 * Core.Random.Range(1f, 1f), 0.0f,
                                                    current.amount) *
                                                num2);
                                        }

                                        if (num4 > 0)
                                        {
                                            var num5 = Mathf.CeilToInt(num4 / (float)current.itemDef.stackable);
                                            for (var index = 0; index < num5; ++index)
                                            {
                                                var iAmount = num4 > current.itemDef.stackable
                                                    ? current.itemDef.stackable
                                                    : num4;
                                                if (!_recycler.MoveItemToOutput(ItemManager.Create(current.itemDef,
                                                    iAmount)))
                                                    flag = true;
                                                num4 -= iAmount;
                                                if (num4 <= 0)
                                                    break;
                                            }
                                        }
                                    }
                                }

                                break;
                            }
                        }
                    }
                }

                if (!flag && HasRecyclable())
                    return;
                StopRecycling();
            }

            private bool HasRecyclable()
            {
                for (var slot1 = 0; slot1 < 6; ++slot1)
                {
                    var slot2 = _recycler.inventory.GetSlot(slot1);
                    if (slot2 != null)
                    {
                        var can = Interface.CallHook("CanRecycle", _recycler, slot2);
                        if (can is bool)
                            return (bool)can;

                        if (slot2.info.Blueprint != null)
                            return true;
                    }
                }

                return false;
            }

            #endregion

            #region Destroy

            public void TryPickup(BasePlayer player)
            {
                if (_config.Recycler.Building && !player.CanBuild())
                {
                    _instance.Reply(player, CantBuild);
                    return;
                }

                if (_config.Recycler.Owner && _recycler.OwnerID != player.userID)
                {
                    _instance.Reply(player, OnlyOwner);
                    return;
                }

                if (_recycler.SecondsSinceDealtDamage < 30f)
                {
                    _instance.Reply(player, RecentlyDamaged);
                    return;
                }

                _recycler.Kill();

                var craft = _instance._crafts.Find(x => x.Type == CraftType.Recycler);
                if (craft == null)
                {
                    _instance.Reply(player, CannotGive);
                    return;
                }

                _instance?.GiveCraft(player, craft);
            }

            private void OnDestroy()
            {
                CancelInvoke();

                _instance?._recyclers.Remove(this);

                Destroy(this);
            }

            public void Kill()
            {
                Destroy(this);
            }

            #endregion
        }

        #endregion

        #region Car Component

        public class CarController : FacepunchBehaviour
        {
            public BasicCar entity;
            public BasePlayer player;
            public bool isDiving;

            private bool _allowHeldItems;
            private string[] _disallowedItems;

            private readonly BaseEntity[] _sensesResults = new BaseEntity[64];

            private void Awake()
            {
                entity = GetComponent<BasicCar>();

                _allowHeldItems = !_config.Car.ActiveItems.Disable;
                _disallowedItems = _config.Car.ActiveItems.BlackList;

                _instance?._cars.Add(this);
            }

            private void Update()
            {
                UpdateHeldItems();
                CheckWaterLevel();
            }

            public void ManageDamage(HitInfo info)
            {
                if (isDiving)
                {
                    NullifyDamage(info);
                    return;
                }

                if (info.damageTypes.GetMajorityDamageType() == DamageType.Bullet)
                    info.damageTypes.ScaleAll(200);

                if (info.damageTypes.Total() >= entity.health)
                {
                    isDiving = true;
                    NullifyDamage(info);
                    OnDeath();
                }
            }

            public void DDraw()
            {
                if (entity == null)
                {
                    Kill();
                    return;
                }

                if (entity.OwnerID == 0)
                    return;

                var inSphere = BaseEntity.Query.Server.GetInSphere(entity.transform.position, _config.Car.Radius,
                    _sensesResults, ent => ent is BasePlayer);
                if (inSphere == 0)
                    return;

                for (var i = 0; i < inSphere; i++)
                {
                    var user = _sensesResults[i] as BasePlayer;
                    if (user == null || user.IsDestroyed || !user.IsConnected || user.IsNpc ||
                        !user.userID.IsSteamId()) continue;

                    if (user.Connection.authLevel < 2) SetPlayerFlag(user, BasePlayer.PlayerFlags.IsAdmin, true);

                    user.SendConsoleCommand("ddraw.text", _config.Car.Delay, HexToUnityColor(_config.Car.Color),
                        entity.transform.position + new Vector3(0.25f, 1, 0),
                        string.Format(_config.Car.Text, entity.health, entity._maxHealth));

                    if (user.Connection.authLevel < 2) SetPlayerFlag(user, BasePlayer.PlayerFlags.IsAdmin, false);
                }
            }

            private void NullifyDamage(HitInfo info)
            {
                info.damageTypes = new DamageTypeList();
                info.HitEntity = null;
                info.HitMaterial = 0;
                info.PointStart = Vector3.zero;
            }

            public void UpdateHeldItems()
            {
                if (player == null)
                    return;

                var item = player.GetActiveItem();
                if (item == null || item.GetHeldEntity() == null)
                    return;

                if (_disallowedItems.Contains(item.info.shortname) || !_allowHeldItems)
                {
                    _instance?.Reply(player, ItemNotAllowed);

                    var slot = item.position;
                    item.SetParent(null);
                    item.MarkDirty();

                    Invoke(() =>
                    {
                        if (player == null) return;
                        item.SetParent(player.inventory.containerBelt);
                        item.position = slot;
                        item.MarkDirty();
                    }, 0.15f);
                }
            }

            public void CheckWaterLevel()
            {
                if (WaterLevel.Factor(entity.WorldSpaceBounds().ToBounds()) > 0.7f)
                    StopToDie();
            }

            public void StopToDie(bool death = true)
            {
                if (entity != null)
                {
                    entity.SetFlag(BaseEntity.Flags.Reserved1, false);

                    foreach (var wheel in entity.wheels)
                    {
                        wheel.wheelCollider.motorTorque = 0;
                        wheel.wheelCollider.brakeTorque = float.MaxValue;
                    }

                    entity.GetComponent<Rigidbody>().velocity = Vector3.zero;

                    if (player != null)
                        entity.DismountPlayer(player);
                }

                if (death) OnDeath();
            }

            private void OnDeath()
            {
                isDiving = true;

                if (player != null)
                    player.EnsureDismounted();

                Invoke(() =>
                {
                    Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab",
                        transform.position);
                    _instance.NextTick(() =>
                    {
                        if (entity != null && !entity.IsDestroyed)
                            entity.DieInstantly();
                        Destroy(this);
                    });
                }, 5f);
            }

            private void OnDestroy()
            {
                _instance?._cars.Remove(this);
            }

            public void Kill()
            {
                StopToDie(false);
                Destroy(this);
            }
        }

        #endregion

        #endregion

        #region Lang

        private const string
            NotWorkbench = "NotWorkbench",
            CraftsDescription = "CraftsDescription",
            WorkbenchLvl = "WorkbenchLvl",
            GotCraft = "GotCraft",
            NoPermission = "NoPermission",
            SuccessfulCraft = "SuccessfulCraft",
            NotEnoughResources = "NotEnoughResources",
            CraftCancelButton = "CraftCancelButton",
            CraftButton = "CraftButton",
            CraftItemFormat = "CraftItemFormat",
            CraftTitle = "CraftTitle",
            TitleMenu = "TitleMenu",
            CloseButton = "CloseButton",
            OnGround = "OnGround",
            BuildDistance = "BuildDistance",
            OnStruct = "OnStruct",
            NotTake = "NotTake",
            ItemNotAllowed = "ItemNotAllowed",
            CantBuild = "CantBuild",
            OnlyOwner = "OnlyOwner",
            RecentlyDamaged = "RecentlyDamaged",
            CannotGive = "CannotGive";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [NoPermission] = "You don't have the required permission",
                [TitleMenu] = "Crafts Menu",
                [CloseButton] = "✕",
                [CraftTitle] = "TO CRAFT {0} YOU NEED",
                [CraftItemFormat] = "{0} ({1} pcs)",
                [CraftButton] = "CRAFT",
                [CraftCancelButton] = "CANCEL",
                [NotEnoughResources] = "Not enough resources",
                [SuccessfulCraft] = "You have successfully crafted the '{0}'",
                [OnGround] = "{0} can't put it on the ground!",
                [BuildDistance] = "Built closer than {0}m is blocked!",
                [OnStruct] = "{0} can't put on the buildings!",
                [NotTake] = "Pickup disabled!",
                [ItemNotAllowed] = "Item blocked!",
                [CantBuild] = "You must have the permission to build.",
                [OnlyOwner] = "Only the owner can pick up the recycler!",
                [RecentlyDamaged] = "The recycler has recently been damaged, you can take it in 30 seconds!",
                [CannotGive] = "Call the administrator. The recycler cannot be give",
                [GotCraft] = "You got a '{0}'",
                [WorkbenchLvl] = "Workbench LVL {0}",
                [CraftsDescription] =
                    "Select the desired item from the list of all items, sort them by category, after which you can find out the cost of manufacturing and the most efficient way to create an item.",
                [NotWorkbench] = "Not enough workbench level for craft!"
            }, this);
        }

        private string Msg(string key, string userid = null, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, userid), obj);
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