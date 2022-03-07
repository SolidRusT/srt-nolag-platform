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
    [Info("Crafts", "Mevent", "2.6.1")]
    public class Crafts : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary, SpawnModularCar, Notify, UINotify, LangAPI;

        private const string Layer = "UI.Crafts";

        private const string EditLayer = "UI.Crafts.Edit";

        private const string PermEdit = "crafts.edit";

        private static Crafts _instance;

        private List<CraftConf> _crafts = new List<CraftConf>();

        private readonly List<RecyclerComponent> _recyclers = new List<RecyclerComponent>();

        private readonly List<CarController> _cars = new List<CarController>();

        private readonly Dictionary<string, List<string>> _itemsCategories =
            new Dictionary<string, List<string>>();

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

        private readonly Dictionary<int, ItemForCraft> _itemsById = new Dictionary<int, ItemForCraft>();

        private readonly Dictionary<int, CraftConf> _craftsById = new Dictionary<int, CraftConf>();

        private readonly Dictionary<BasePlayer, Dictionary<string, object>> _craftEditing =
            new Dictionary<BasePlayer, Dictionary<string, object>>();

        private readonly Dictionary<BasePlayer, Dictionary<string, object>> _itemEditing =
            new Dictionary<BasePlayer, Dictionary<string, object>>();

        #endregion

        #region Config

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly string[] Commands = {"craft", "crafts"};

            [JsonProperty(PropertyName = "Work with Notify?")]
            public readonly bool UseNotify = true;

            [JsonProperty(PropertyName = "Work with LangAPI?")]
            public readonly bool UseLangAPI = true;

            [JsonProperty(PropertyName = "Permission (ex: crafts.use)")]
            public readonly string Permission = string.Empty;

            [JsonProperty(PropertyName = "Categories", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<Category> Categories = new List<Category>
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
                        },
                        new CraftConf
                        {
                            Enabled = true,
                            Image = "https://i.imgur.com/xj0N3lI.png",
                            Title = "Snowmobile",
                            Description = "Conquers snow biomes",
                            CmdToGive = "givesnowmobile",
                            Permission = "crafts.all",
                            DisplayName = "Snowmobile",
                            ShortName = "electric.flasherlight",
                            Amount = 1,
                            SkinID = 2747934628,
                            Type = CraftType.Vehicle,
                            GiveCommand = string.Empty,
                            Prefab = "assets/content/vehicles/snowmobiles/snowmobile.prefab",
                            Level = WorkbenchLevel.Two,
                            UseDistance = true,
                            Distance = 1.5f,
                            Ground = true,
                            Structure = false,
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
            public readonly Dictionary<WorkbenchLevel, IColor> Workbenches =
                new Dictionary<WorkbenchLevel, IColor>
                {
                    [WorkbenchLevel.None] = new IColor("#FFFFFF", 00),
                    [WorkbenchLevel.One] = new IColor("#74884A", 100),
                    [WorkbenchLevel.Two] = new IColor("#B19F56", 100),
                    [WorkbenchLevel.Three] = new IColor("#B43D3D", 100)
                };

            [JsonProperty(PropertyName = "Recycler Settings")]
            public readonly RecyclerConfig Recycler = new RecyclerConfig
            {
                Speed = 5f,
                Radius = 7.5f,
                Text = "<size=19>RECYCLER</size>\n<size=15>{0}/{1}</size>",
                Color = "#C5D0E6",
                Delay = 0.75f,
                Available = true,
                Owner = true,
                Amounts = new[]
                    {0.9f, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0.9f, 0.5f, 0.5f, 0, 1, 1, 0.5f, 0, 0, 0, 0, 0, 1, 1},
                Scale = 0.5f,
                DDraw = true,
                Building = true
            };

            [JsonProperty(PropertyName = "Car Settings")]
            public readonly CarConfig Car = new CarConfig
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
            public readonly UserInterface UI = new UserInterface
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
                Color8 = new IColor("#4B68FF", 70),
                Color9 = new IColor("#0E0E10", 98),
                Color10 = new IColor("#4B68FF", 50),
                Color11 = new IColor("#FF4B4B", 100),
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

            [JsonProperty(PropertyName = "Color 8")]
            public IColor Color8;

            [JsonProperty(PropertyName = "Color 9")]
            public IColor Color9;

            [JsonProperty(PropertyName = "Color 10")]
            public IColor Color10;

            [JsonProperty(PropertyName = "Color 11")]
            public IColor Color11;

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

            [JsonProperty(PropertyName = "ShortName")]
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

            [JsonIgnore] public bool Active = false;

            [JsonIgnore] private int _id = -1;

            [JsonIgnore]
            public int ID
            {
                get
                {
                    while (_id == -1)
                    {
                        var val = Random.Range(int.MinValue, int.MaxValue);
                        if (_instance._craftsById.ContainsKey(val)) continue;

                        _id = val;
                        _instance._craftsById[_id] = this;
                    }

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

            public Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    ["Generated"] = false,
                    ["Enabled"] = Enabled,
                    ["Image"] = Image,
                    ["Title"] = Title,
                    ["Description"] = Description,
                    ["CmdToGive"] = CmdToGive,
                    ["Permission"] = Permission,
                    ["DisplayName"] = DisplayName,
                    ["ShortName"] = ShortName,
                    ["Amount"] = Amount,
                    ["SkinID"] = SkinID,
                    ["Type"] = Type,
                    ["Prefab"] = Prefab,
                    ["GiveCommand"] = GiveCommand,
                    ["Level"] = Level,
                    ["UseDistance"] = UseDistance,
                    ["Distance"] = Distance,
                    ["Ground"] = Ground,
                    ["Structure"] = Structure,
                    ["Items"] = Items
                };
            }
        }

        private class IColor
        {
            [JsonProperty(PropertyName = "HEX")] public string Hex;

            [JsonProperty(PropertyName = "Opacity (0 - 100)")]
            public readonly float Alpha;

            public string Get()
            {
                if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

                var str = Hex.Trim('#');
                if (str.Length != 6) throw new Exception(Hex);
                var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

                return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
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

            [JsonProperty(PropertyName = "ShortName")]
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

            [JsonIgnore] private int _id = -1;

            [JsonIgnore]
            public int ID
            {
                get
                {
                    while (_id == -1)
                    {
                        var val = Random.Range(int.MinValue, int.MaxValue);
                        if (_instance._itemsById.ContainsKey(val)) continue;

                        _id = val;
                        _instance._itemsById[_id] = this;
                    }

                    return _id;
                }
            }

            public Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    ["Generated"] = false,
                    ["ID"] = ID,
                    ["Image"] = Image,
                    ["ShortName"] = ShortName,
                    ["Amount"] = Amount,
                    ["SkinID"] = SkinID,
                    ["Title"] = Title
                };
            }

            public string GetItemDisplayName(BasePlayer player)
            {
                return _config.UseLangAPI && _instance.LangAPI != null &&
                       _instance.LangAPI.Call<bool>("IsDefaultDisplayName", PublicTitle)
                    ? _instance.LangAPI.Call<string>("GetItemDisplayName", ShortName, PublicTitle,
                        player.UserIDString) ?? PublicTitle
                    : PublicTitle;
            }

            #region Constructor

            public ItemForCraft()
            {
            }

            public ItemForCraft(string image, string shortname, int amount, ulong skin)
            {
                Image = image;
                ShortName = shortname;
                Amount = amount;
                SkinID = skin;
            }

            #endregion
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

        private void Init()
        {
            _instance = this;

            _crafts = _config.Categories.SelectMany(x => x.Crafts).ToList();

            RegisterPermissions();
        }

        private void OnServerInitialized(bool initial)
        {
            LoadItems();
            
            LoadImages();

            if (!SpawnModularCar && _crafts.Exists(x => x.Enabled && x.Type == CraftType.ModularCar))
                PrintError("SpawnModularCar IS NOT INSTALLED.");

            if (!initial)
                foreach (var ent in BaseNetworkable.serverEntities.OfType<BaseCombatEntity>())
                    OnEntitySpawned(ent);

            RegisterCommands();
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, EditLayer + ".Background");
            }

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
            if (entity == null || entity.OwnerID == 0) return;

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

                    _craftEditing.Remove(player);
                    _itemEditing.Remove(player);

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

                case "start_edit":
                {
                    int category, page, craftId;
                    if (!arg.HasArgs(4) ||
                        !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[3], out craftId))
                        return;

                    EditUi(player, category, page, craftId);
                    break;
                }

                case "edit":
                {
                    int category, page, craftId, itemsPage;
                    if (!arg.HasArgs(7) ||
                        !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[3], out craftId) ||
                        !int.TryParse(arg.Args[4], out itemsPage) ||
                        string.IsNullOrEmpty(arg.Args[5]) || string.IsNullOrEmpty(arg.Args[6])) return;

                    var key = arg.Args[5];
                    var value = arg.Args[6];

                    if (_craftEditing.ContainsKey(player) && _craftEditing[player].ContainsKey(key))
                    {
                        object newValue;

                        switch (key)
                        {
                            case "Amount":
                            {
                                int result;
                                if (int.TryParse(value, out result))
                                    newValue = result;
                                else
                                    return;
                                break;
                            }

                            case "SkinID":
                            {
                                ulong result;
                                if (ulong.TryParse(value, out result))
                                    newValue = result;
                                else
                                    return;
                                break;
                            }

                            case "Type":
                            {
                                CraftType result;
                                if (Enum.TryParse(value, out result))
                                    newValue = result;
                                else
                                    return;
                                break;
                            }

                            case "Level":
                            {
                                WorkbenchLevel result;
                                if (Enum.TryParse(value, out result))
                                    newValue = result;
                                else
                                    return;
                                break;
                            }

                            case "Enabled":
                            case "UseDistance":
                            case "Ground":
                            case "Structure":
                            {
                                bool result;
                                if (bool.TryParse(value, out result))
                                    newValue = result;
                                else
                                    return;
                                break;
                            }

                            case "Description":
                            {
                                newValue = string.Join(" ", arg.Args.Skip(6));
                                break;
                            }

                            default:
                            {
                                newValue = value;
                                break;
                            }
                        }

                        _craftEditing[player][key] = newValue;
                    }

                    EditUi(player, category, page, craftId);
                    break;
                }

                case "save_edit":
                {
                    int category, page, craftId;
                    if (!arg.HasArgs(4) ||
                        !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[3], out craftId))
                        return;

                    Dictionary<string, object> values;
                    if (!_craftEditing.TryGetValue(player, out values) || values == null) return;

                    var generated = Convert.ToBoolean(values["Generated"]);
                    var craft = generated ? new CraftConf() : FindCraftById(craftId);
                    if (craft == null) return;

                    craft.Enabled = Convert.ToBoolean(values["Enabled"]);
                    craft.Image = (string) values["Image"];
                    craft.Title = (string) values["Title"];
                    craft.Description = (string) values["Description"];
                    craft.CmdToGive = (string) values["CmdToGive"];
                    craft.Permission = (string) values["Permission"];
                    craft.DisplayName = (string) values["DisplayName"];
                    craft.ShortName = (string) values["ShortName"];
                    craft.Prefab = (string) values["Prefab"];
                    craft.GiveCommand = (string) values["GiveCommand"];
                    craft.Amount = Convert.ToInt32(values["Amount"]);
                    craft.SkinID = Convert.ToUInt64(values["SkinID"]);
                    craft.Type = (CraftType) values["Type"];
                    craft.Level = (WorkbenchLevel) values["Level"];
                    craft.UseDistance = Convert.ToBoolean(values["UseDistance"]);
                    craft.Distance = Convert.ToSingle(values["Distance"]);
                    craft.Structure = Convert.ToBoolean(values["Structure"]);
                    craft.Items = values["Items"] as List<ItemForCraft>;

                    if (generated)
                        GetPlayerCategories(player)[category].Crafts.Add(craft);

                    _craftEditing.Remove(player);
                    _itemEditing.Remove(player);

                    SaveConfig();

                    MainUi(player, category, page, true);
                    break;
                }

                case "delete_edit":
                {
                    int category, page, craftId;
                    if (!arg.HasArgs(4) ||
                        !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[3], out craftId))
                        return;

                    var craft = FindCraftById(craftId);
                    if (craft == null) return;

                    GetPlayerCategories(player)[category].Crafts.Remove(craft);

                    _craftEditing.Remove(player);
                    _itemEditing.Remove(player);

                    SaveConfig();

                    MainUi(player, category, page, true);
                    break;
                }

                case "edit_page":
                {
                    int category, page, craftId, itemsPage;
                    if (!arg.HasArgs(5) ||
                        !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[3], out craftId) ||
                        !int.TryParse(arg.Args[4], out itemsPage))
                        return;

                    EditUi(player, category, page, craftId, itemsPage);
                    break;
                }

                case "stopedit":
                {
                    _craftEditing.Remove(player);
                    _itemEditing.Remove(player);
                    break;
                }

                case "start_edititem":
                {
                    int category, page, craftId, itemsPage, itemId;
                    if (!arg.HasArgs(6) ||
                        !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[3], out craftId) ||
                        !int.TryParse(arg.Args[4], out itemsPage) ||
                        !int.TryParse(arg.Args[5], out itemId))
                        return;

                    _itemEditing.Remove(player);

                    EditItemUi(player, category, page, craftId, itemsPage, itemId);
                    break;
                }

                case "edititem":
                {
                    int category, page, craftId, itemsPage, itemId;
                    if (!arg.HasArgs(8) ||
                        !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[3], out craftId) ||
                        !int.TryParse(arg.Args[4], out itemsPage) ||
                        !int.TryParse(arg.Args[5], out itemId) ||
                        string.IsNullOrEmpty(arg.Args[6]) || string.IsNullOrEmpty(arg.Args[7]))
                        return;

                    var key = arg.Args[6];
                    var value = arg.Args[7];

                    if (_itemEditing.ContainsKey(player) && _itemEditing[player].ContainsKey(key))
                    {
                        object newValue = null;

                        switch (key)
                        {
                            case "Amount":
                            {
                                int result;
                                if (value == "delete")
                                    newValue = 1;
                                else if (int.TryParse(value, out result))
                                    newValue = result;
                                break;
                            }
                            case "SkinID":
                            {
                                ulong result;
                                if (value == "delete")
                                    newValue = 0UL;
                                else if (ulong.TryParse(value, out result))
                                    newValue = result;
                                break;
                            }
                            default:
                            {
                                newValue = value == "delete" ? string.Empty : value;
                                break;
                            }
                        }

                        _itemEditing[player][key] = newValue;
                    }

                    EditItemUi(player, category, page, craftId, itemsPage, itemId);
                    break;
                }

                case "saveitem":
                {
                    int category, page, craftId, itemsPage, itemId;
                    if (!arg.HasArgs(6) ||
                        !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[3], out craftId) ||
                        !int.TryParse(arg.Args[4], out itemsPage) ||
                        !int.TryParse(arg.Args[5], out itemId))
                        return;

                    Dictionary<string, object> values;
                    if (!_itemEditing.TryGetValue(player, out values) || values == null) return;

                    var generated = Convert.ToBoolean(values["Generated"]);
                    var item = generated ? new ItemForCraft() : FindItemById(itemId);
                    if (item == null) return;

                    item.Image = values["Image"].ToString();
                    item.ShortName = values["ShortName"].ToString();
                    item.Amount = Convert.ToInt32(values["Amount"]);
                    item.SkinID = Convert.ToUInt64(values["SkinID"]);

                    if (generated)
                        ((List<ItemForCraft>) _craftEditing[player]["Items"]).Add(item);
                    else
                        _craftEditing.Remove(player);

                    _itemEditing.Remove(player);

                    SaveConfig();

                    EditUi(player, category, page, craftId, itemsPage);
                    break;
                }

                case "removeitem":
                {
                    int category, page, craftId, itemsPage, itemId;
                    if (!arg.HasArgs(6) ||
                        !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[3], out craftId) ||
                        !int.TryParse(arg.Args[4], out itemsPage) ||
                        !int.TryParse(arg.Args[5], out itemId))
                        return;

                    var craft = FindCraftById(craftId);
                    if (craft == null) return;

                    var item = FindItemById(itemId);
                    if (item == null) return;

                    craft.Items.Remove(item);

                    _craftEditing.Remove(player);
                    _itemEditing.Remove(player);

                    SaveConfig();

                    EditUi(player, category, page, craftId, itemsPage);
                    break;
                }

                case "selectitem":
                {
                    int category, page, craftId, itemsPage, itemId;
                    if (!arg.HasArgs(6) ||
                        !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[3], out craftId) ||
                        !int.TryParse(arg.Args[4], out itemsPage) ||
                        !int.TryParse(arg.Args[5], out itemId))
                        return;

                    var selectedCategory = string.Empty;
                    if (arg.HasArgs(7))
                        selectedCategory = arg.Args[6];

                    var localPage = 0;
                    if (arg.HasArgs(8))
                        int.TryParse(arg.Args[7], out localPage);

                    var input = string.Empty;
                    if (arg.HasArgs(9))
                        input = string.Join(" ", arg.Args.Skip(8));

                    SelectItemUi(player, category, page, craftId, itemsPage, itemId, selectedCategory, localPage,
                        input);
                    break;
                }

                case "takeitem":
                {
                    int category, page, craftId, itemsPage, itemId;
                    if (!arg.HasArgs(7) ||
                        !int.TryParse(arg.Args[1], out category) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[3], out craftId) ||
                        !int.TryParse(arg.Args[4], out itemsPage) ||
                        !int.TryParse(arg.Args[5], out itemId))
                        return;

                    var shortName = arg.Args[6];
                    if (string.IsNullOrEmpty(shortName)) return;

                    _itemEditing[player]["ShortName"] = shortName;

                    EditItemUi(player, category, page, craftId, itemsPage, itemId);
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
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Image =
                    {
                        Color = "0 0 0 0.9",
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    CursorEnabled = true
                }, "Overlay", Layer);

                if (!string.IsNullOrEmpty(_config.UI.BackgroundImage))
                    container.Add(new CuiElement
                    {
                        Parent = Layer,
                        Components =
                        {
                            new CuiRawImageComponent
                                {Png = ImageLibrary.Call<string>("GetImage", _config.UI.BackgroundImage)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1"
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
                            Color = _config.Workbenches[(WorkbenchLevel) wb].Get()
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
                Image = {Color = _config.UI.Color2.Get()}
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

            if (CanEdit(player))
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = "-145 -37.5",
                        OffsetMax = "-55 -12.5"
                    },
                    Text =
                    {
                        Text = Msg(player, CraftCreate),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 10,
                        Color = _config.UI.Color3.Get()
                    },
                    Button =
                    {
                        Color = _config.UI.Color1.Get(),
                        Command = $"UI_Crafts start_edit {category} {page} -1"
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
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                        Text = {Text = ""},
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = $"UI_Crafts trycraft {category} {page} {craft.ID}"
                        }
                    }, Layer + $".Craft.{i}");

                    if (CanEdit(player))
                    {
                        if (!craft.Enabled)
                            container.Add(new CuiPanel
                            {
                                RectTransform =
                                {
                                    AnchorMin = "0 1", AnchorMax = "0 1",
                                    OffsetMin = "5 -15", OffsetMax = "15 -5"
                                },
                                Image =
                                {
                                    Color = _config.UI.Color4.Get()
                                }
                            }, Layer + $".Craft.{i}");

                        container.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "1 1", AnchorMax = "1 1",
                                OffsetMin = "-15 -15", OffsetMax = "-5 -5"
                            },
                            Text =
                            {
                                Text = ""
                            },
                            Button =
                            {
                                Color = "1 1 1 1",
                                Sprite = "assets/icons/gear.png",
                                Command = $"UI_Crafts start_edit {category} {page} {craft.ID}"
                            }
                        }, Layer + $".Craft.{i}");
                    }

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

            var pages = Mathf.CeilToInt((float) playerCrafts.Count / totalAmount);
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
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = "0.19 0.19 0.18 0.3",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                CursorEnabled = true
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text = {Text = ""},
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
                            {Png = ImageLibrary.Call<string>("GetImage", _config.UI.BackgroundImage)},
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
                        Text = item.GetItemDisplayName(player),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 16,
                        Color = "1 1 1 0.5"
                    }
                }, Layer + $".Item.{xSwitch}");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0",
                        OffsetMin = "-100 -25", OffsetMax = "100 0"
                    },
                    Text =
                    {
                        Text = Msg(player, CraftItemAmount, item.Amount),
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

        private void EditUi(BasePlayer player, int category, int page, int craftId, int itemsPage = 0)
        {
            #region Dictionary

            if (!_craftEditing.ContainsKey(player))
            {
                var craft = FindCraftById(craftId);
                if (craft != null)
                    _craftEditing[player] = craft.ToDictionary();
                else
                    _craftEditing[player] = new Dictionary<string, object>
                    {
                        ["Generated"] = true,
                        ["Enabled"] = false,
                        ["Image"] = string.Empty,
                        ["Title"] = string.Empty,
                        ["Description"] = string.Empty,
                        ["CmdToGive"] = string.Empty,
                        ["Permission"] = string.Empty,
                        ["DisplayName"] = string.Empty,
                        ["ShortName"] = string.Empty,
                        ["Amount"] = 1,
                        ["SkinID"] = 0UL,
                        ["Type"] = CraftType.Command,
                        ["Prefab"] = string.Empty,
                        ["GiveCommand"] = string.Empty,
                        ["Level"] = WorkbenchLevel.None,
                        ["UseDistance"] = false,
                        ["Distance"] = 0f,
                        ["Ground"] = false,
                        ["Structure"] = false,
                        ["Items"] = new List<ItemForCraft>()
                    };
            }

            #endregion

            var edit = _craftEditing[player];

            var container = new CuiElementContainer();

            #region Background

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = _config.UI.Color9.Get()
                }
            }, Layer, EditLayer + ".Background");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text = {Text = ""},
                Button =
                {
                    Color = "0 0 0 0",
                    Close = EditLayer + ".Background",
                    Command = "UI_Crafts stopedit"
                }
            }, EditLayer + ".Background");

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-240 -275",
                    OffsetMax = "240 275"
                },
                Image =
                {
                    Color = _config.UI.Color1.Get()
                }
            }, EditLayer + ".Background", EditLayer);

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50",
                    OffsetMax = "0 0"
                },
                Image = {Color = _config.UI.Color2.Get()}
            }, EditLayer, Layer + ".Header");

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
                    Text = Msg(player, CraftEditingTitle),
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
                    Close = EditLayer + ".Background",
                    Color = _config.UI.Color4.Get(),
                    Command = "UI_Crafts stopedit"
                }
            }, Layer + ".Header");

            #endregion

            #region Image

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = "10 -200",
                    OffsetMax = "145 -65"
                },
                Image = {Color = _config.UI.Color2.Get()}
            }, EditLayer, Layer + ".Image");

            if (!string.IsNullOrEmpty(edit["Image"].ToString()) || !string.IsNullOrEmpty(edit["ShortName"].ToString()))
                container.Add(new CuiElement
                {
                    Parent = Layer + ".Image",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = !string.IsNullOrEmpty(edit["Image"].ToString())
                                ? ImageLibrary.Call<string>("GetImage", edit["Image"].ToString())
                                : ImageLibrary.Call<string>("GetImage", edit["ShortName"].ToString(),
                                    Convert.ToUInt64(edit["SkinID"]))
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "5 5", OffsetMax = "-5 -5"
                        }
                    }
                });

            #region Input

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 0",
                    OffsetMin = "0 -20", OffsetMax = "0 0"
                },
                Image =
                {
                    Color = _config.UI.Color8.Get()
                }
            }, Layer + ".Image", Layer + ".Image.Input");

            if (!string.IsNullOrEmpty(edit["Image"].ToString()))
                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "5 0", OffsetMax = "-5 0"
                    },
                    Text =
                    {
                        Text = $"{edit["Image"]}",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 0.45"
                    }
                }, Layer + ".Image.Input");

            container.Add(new CuiElement
            {
                Parent = Layer + ".Image.Input",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        FontSize = 10,
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-bold.ttf",
                        Command = $"UI_Crafts edit {category} {page} {craftId} {itemsPage} Image ",
                        Color = "1 1 1 0.95",
                        CharsLimit = 9
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"
                    }
                }
            });

            #endregion

            #endregion

            #region Types

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = "155 -85",
                    OffsetMax = "205 -65"
                },
                Text =
                {
                    Text = Msg(player, CraftTypeTitle),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, EditLayer);

            var xSwitch = 155f;
            var width = 60f;
            var margin = 5f;

            var type = edit["Type"] as CraftType? ?? CraftType.Item;
            foreach (var craftType in Enum.GetValues(typeof(CraftType)).Cast<CraftType>())
            {
                var nowStatus = type == craftType;
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{xSwitch} -105",
                        OffsetMax = $"{xSwitch + width} -85"
                    },
                    Text =
                    {
                        Text = $"{craftType}",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = nowStatus ? _config.UI.Color10.Get() : _config.UI.Color4.Get(),
                        Command = $"UI_Crafts edit {category} {page} {craftId} {itemsPage} Type {craftType}"
                    }
                }, EditLayer);

                xSwitch += width + margin;
            }

            #endregion

            #region Work Bench

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = "155 -135",
                    OffsetMax = "300 -115"
                },
                Text =
                {
                    Text = Msg(player, CraftWorkbenchTitle),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, EditLayer);

            xSwitch = 155f;
            width = 76.25f;
            margin = 5f;

            foreach (var wbLevel in Enum.GetValues(typeof(WorkbenchLevel)).Cast<WorkbenchLevel>())
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{xSwitch} -155",
                        OffsetMax = $"{xSwitch + width} -135"
                    },
                    Text =
                    {
                        Text = $"{wbLevel}",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _config.Workbenches[wbLevel].Get(),
                        Command = $"UI_Crafts edit {category} {page} {craftId} {itemsPage} Level {wbLevel}"
                    }
                }, EditLayer, Layer + $".WorkBench.{wbLevel}");

                var lvl = (WorkbenchLevel) edit["Level"];
                if (lvl == wbLevel)
                    CreateOutLine(ref container, Layer + $".WorkBench.{wbLevel}", _config.UI.Color2.Get());

                xSwitch += width + margin;
            }

            #endregion

            #region Prefab

            EditFieldUi(player, ref container, EditLayer, CuiHelper.GetGuid(),
                "-85 -215",
                "235 -165",
                $"UI_Crafts edit {category} {page} {craftId} {itemsPage} Prefab ",
                new KeyValuePair<string, object>("Prefab", edit["Prefab"]));

            #endregion

            #region Fields

            width = 150f;
            margin = 5;
            var yMargin = 5f;
            var height = 45f;
            var ySwitch = -225f;
            var fieldsOnString = 3;

            var constSwitch = -(fieldsOnString * width + (fieldsOnString - 1) * margin) / 2f;
            xSwitch = constSwitch;

            var i = 1;
            foreach (var obj in _craftEditing[player]
                         .Where(x => x.Key != "Generated"
                                     && x.Key != "ID"
                                     && x.Key != "Prefab"
                                     && x.Key != "Enabled"
                                     && x.Key != "Type"
                                     && x.Key != "Level"
                                     && x.Key != "UseDistance"
                                     && x.Key != "Ground"
                                     && x.Key != "Structure"
                                     && x.Key != "Items"))
            {
                EditFieldUi(player, ref container, EditLayer, Layer + $".Editing.{i}",
                    $"{xSwitch} {ySwitch - height}",
                    $"{xSwitch + width} {ySwitch}",
                    $"UI_Crafts edit {category} {page} {craftId} {itemsPage} {obj.Key} ",
                    obj);

                if (i % fieldsOnString == 0)
                {
                    ySwitch = ySwitch - height - yMargin;
                    xSwitch = constSwitch;
                }
                else
                {
                    xSwitch += width + margin;
                }

                i++;
            }

            #endregion

            #region Items

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = "10 -445",
                    OffsetMax = "100 -425"
                },
                Text =
                {
                    Text = Msg(player, CraftItemsTitle),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, EditLayer);

            var amountOnString = 7;
            width = 60f;
            height = 60f;
            margin = 5f;

            ySwitch = -450f;

            xSwitch = 10f;

            var items = (List<ItemForCraft>) edit["Items"];
            if (items != null)
            {
                foreach (var craftItem in items.Skip(amountOnString * itemsPage).Take(amountOnString))
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{xSwitch} {ySwitch - height}",
                            OffsetMax = $"{xSwitch + width} {ySwitch}"
                        },
                        Image =
                        {
                            Color = _config.UI.Color2.Get()
                        }
                    }, EditLayer, Layer + $".Craft.Item.{xSwitch}");

                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".Craft.Item.{xSwitch}",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = !string.IsNullOrEmpty(craftItem.Image)
                                    ? ImageLibrary.Call<string>("GetImage", craftItem.Image)
                                    : ImageLibrary.Call<string>("GetImage", craftItem.ShortName, craftItem.SkinID)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "5 5", OffsetMax = "-5 -5"
                            }
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "0 2",
                            OffsetMax = "-2 0"
                        },
                        Text =
                        {
                            Text = $"{craftItem.Amount}",
                            Align = TextAnchor.LowerRight,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 10,
                            Color = "1 1 1 0.9"
                        }
                    }, Layer + $".Craft.Item.{xSwitch}");

                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                        Text = {Text = ""},
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = $"UI_Crafts start_edititem {category} {page} {craftId} {itemsPage} {craftItem.ID}"
                        }
                    }, Layer + $".Craft.Item.{xSwitch}");

                    xSwitch += margin + width;
                }

                #region Buttons

                #region Add

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "45 -445",
                        OffsetMax = "65 -425"
                    },
                    Text =
                    {
                        Text = Msg(player, CraftItemsAddTitle),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _config.UI.Color4.Get(),
                        Command = $"UI_Crafts start_edititem {category} {page} {craftId} {itemsPage} -1"
                    }
                }, EditLayer);

                #endregion

                #region Back

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "70 -445",
                        OffsetMax = "90 -425"
                    },
                    Text =
                    {
                        Text = Msg(player, BtnBack),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _config.UI.Color4.Get(),
                        Command = itemsPage != 0
                            ? $"UI_Crafts edit_page {category} {page} {craftId} {itemsPage - 1}"
                            : ""
                    }
                }, EditLayer);

                #endregion

                #region Next

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "95 -445",
                        OffsetMax = "115 -425"
                    },
                    Text =
                    {
                        Text = Msg(player, BtnNext),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _config.UI.Color4.Get(),
                        Command = items.Count > (itemsPage + 1) * amountOnString
                            ? $"UI_Crafts edit_page {category} {page} {craftId} {itemsPage + 1}"
                            : ""
                    }
                }, EditLayer);

                #endregion

                #endregion
            }

            #endregion

            #endregion

            #region Params

            xSwitch = constSwitch;

            ySwitch = ySwitch - height - 10f;

            #region Enabled

            var enabled = Convert.ToBoolean(_craftEditing[player]["Enabled"]);

            var text = Msg(player, EnableCraft);

            CheckBoxUi(ref container, EditLayer, Layer + ".Editing.Enabled", "0.5 1", "0.5 1",
                $"{xSwitch} {ySwitch - 10}",
                $"{xSwitch + 10} {ySwitch}",
                enabled,
                $"UI_Crafts edit {category} {page} {craftId} {itemsPage} Enabled {!enabled}",
                text
            );

            xSwitch += (text.Length + 1) * 10 * 0.5f + 10;

            #endregion

            #region UseDistance

            var useDistance = Convert.ToBoolean(_craftEditing[player]["UseDistance"]);

            text = Msg(player, EditUseDistance);

            CheckBoxUi(ref container, EditLayer, Layer + ".Editing.UseDistance", "0.5 1", "0.5 1",
                $"{xSwitch} {ySwitch - 10}",
                $"{xSwitch + 10} {ySwitch}",
                useDistance,
                $"UI_Crafts edit {category} {page} {craftId} {itemsPage} UseDistance {!useDistance}",
                text
            );

            xSwitch += (text.Length + 1) * 10 * 0.5f + 10;

            #endregion

            #region Ground

            var ground = Convert.ToBoolean(_craftEditing[player]["Ground"]);

            text = Msg(player, EditGround);

            CheckBoxUi(ref container, EditLayer, Layer + ".Editing.Ground", "0.5 1", "0.5 1",
                $"{xSwitch} {ySwitch - 10}",
                $"{xSwitch + 10} {ySwitch}",
                ground,
                $"UI_Crafts edit {category} {page} {craftId} {itemsPage} Ground {!ground}",
                text
            );

            xSwitch += (text.Length + 1) * 10 * 0.5f + 10;

            #endregion

            #region Structure

            var structure = Convert.ToBoolean(_craftEditing[player]["Structure"]);

            text = Msg(player, EnableStructure);

            CheckBoxUi(ref container, EditLayer, Layer + ".Editing.Structure", "0.5 1", "0.5 1",
                $"{xSwitch} {ySwitch - 10}",
                $"{xSwitch + 10} {ySwitch}",
                structure,
                $"UI_Crafts edit {category} {page} {craftId} {itemsPage} Structure {!structure}",
                text
            );

            #endregion

            #endregion

            #region Buttons

            var generated = Convert.ToBoolean(_craftEditing[player]["Generated"]);

            #region Save

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                    OffsetMin = $"{(generated ? -90 : -105)} -12",
                    OffsetMax = $"{(generated ? 90 : 75)} 12"
                },
                Text =
                {
                    Text = Msg(player, CraftSaveTitle),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 14,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _config.UI.Color4.Get(),
                    Command = $"UI_Crafts save_edit {category} {page} {craftId}"
                }
            }, EditLayer);

            #endregion

            #region Delete

            if (!generated)
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "80 -12",
                        OffsetMax = "110 12"
                    },
                    Text =
                    {
                        Text = Msg(player, CraftRemoveTitle),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _config.UI.Color11.Get(),
                        Command = $"UI_Crafts delete_edit {category} {page} {craftId}"
                    }
                }, EditLayer);

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, EditLayer + ".Background");
            CuiHelper.AddUi(player, container);
        }

        private void EditFieldUi(BasePlayer player, ref CuiElementContainer container,
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
                    OffsetMin = "0 -15", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = $"{Msg(player, obj.Key)}".Replace("_", " "),
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

            CreateOutLine(ref container, $"{name}.Value", _config.UI.Color2.Get());

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

        private void CheckBoxUi(ref CuiElementContainer container, string parent, string name, string aMin, string aMax,
            string oMin, string oMax, bool enabled,
            string command, string text)
        {
            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = aMin, AnchorMax = aMax,
                    OffsetMin = oMin,
                    OffsetMax = oMax
                },
                Image = {Color = "0 0 0 0"}
            }, parent, name);

            CreateOutLine(ref container, name, _config.UI.Color4.Get(), 1);

            if (enabled)
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    },
                    Image = {Color = _config.UI.Color4.Get()}
                }, name);


            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text = {Text = ""},
                Button =
                {
                    Color = "0 0 0 0",
                    Command = $"{command}"
                }
            }, name);

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                    OffsetMin = "5 -10",
                    OffsetMax = "100 10"
                },
                Text =
                {
                    Text = $"{text}",
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                }
            }, name);
        }

        private void EditItemUi(BasePlayer player, int category, int page, int craftId, int itemsPage, int itemId)
        {
            #region Dictionary

            if (!_itemEditing.ContainsKey(player))
            {
                var itemById = FindItemById(itemId);
                if (itemById != null)
                    _itemEditing[player] = itemById.ToDictionary();
                else
                    _itemEditing[player] = new Dictionary<string, object>
                    {
                        ["Generated"] = true,
                        ["ID"] = 0,
                        ["Image"] = string.Empty,
                        ["ShortName"] = string.Empty,
                        ["Amount"] = 1,
                        ["SkinID"] = 0UL,
                        ["Title"] = string.Empty
                    };
            }

            #endregion

            var edit = _itemEditing[player];

            var container = new CuiElementContainer();

            #region Background

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = _config.UI.Color9.Get()
                }
            }, Layer, EditLayer + ".Background");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text = {Text = ""},
                Button =
                {
                    Color = "0 0 0 0",
                    Command = $"UI_Crafts edit_page {category} {page} {craftId} {itemsPage}"
                }
            }, EditLayer + ".Background");

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-240 -120",
                    OffsetMax = "240 120"
                },
                Image =
                {
                    Color = _config.UI.Color1.Get()
                }
            }, EditLayer + ".Background", EditLayer);

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50",
                    OffsetMax = "0 0"
                },
                Image = {Color = _config.UI.Color2.Get()}
            }, EditLayer, Layer + ".Header");

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
                    Text = Msg(player, ItemEditingTitle),
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
                    Color = _config.UI.Color4.Get(),
                    Command = $"UI_Crafts edit_page {category} {page} {craftId} {itemsPage}"
                }
            }, Layer + ".Header");

            #endregion

            #region Image

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = "10 -200",
                    OffsetMax = "145 -65"
                },
                Image = {Color = _config.UI.Color2.Get()}
            }, EditLayer, Layer + ".Image");

            if (!string.IsNullOrEmpty(edit["Image"].ToString()) || !string.IsNullOrEmpty(edit["ShortName"].ToString()))
                container.Add(new CuiElement
                {
                    Parent = Layer + ".Image",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = !string.IsNullOrEmpty(edit["Image"].ToString())
                                ? ImageLibrary.Call<string>("GetImage", edit["Image"].ToString())
                                : ImageLibrary.Call<string>("GetImage", edit["ShortName"].ToString(),
                                    Convert.ToUInt64(edit["SkinID"]))
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "5 5", OffsetMax = "-5 -5"
                        }
                    }
                });

            #region Input

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 0",
                    OffsetMin = "0 -20", OffsetMax = "0 0"
                },
                Image =
                {
                    Color = _config.UI.Color8.Get()
                }
            }, Layer + ".Image", Layer + ".Image.Input");

            if (!string.IsNullOrEmpty(edit["Image"].ToString()))
                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "5 0", OffsetMax = "-5 0"
                    },
                    Text =
                    {
                        Text = $"{edit["Image"]}",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 0.45"
                    }
                }, Layer + ".Image.Input");

            container.Add(new CuiElement
            {
                Parent = Layer + ".Image.Input",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        FontSize = 10,
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-bold.ttf",
                        Command = $"UI_Crafts edit {category} {page} {craftId} {itemsPage} Image ",
                        Color = "1 1 1 0.95",
                        CharsLimit = 9
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"
                    }
                }
            });

            #endregion

            #endregion

            #region Title

            EditFieldUi(player, ref container, EditLayer, CuiHelper.GetGuid(),
                "-85 -105",
                "235 -65",
                $"UI_Crafts edititem {category} {page} {craftId} {itemsPage} {itemId} Title ",
                new KeyValuePair<string, object>("Title", edit["Title"]));

            #endregion

            #region Amount

            EditFieldUi(player, ref container, EditLayer, CuiHelper.GetGuid(),
                "-85 -155",
                "70 -115",
                $"UI_Crafts edititem {category} {page} {craftId} {itemsPage} {itemId} Amount ",
                new KeyValuePair<string, object>("Amount", edit["Amount"]));

            #endregion

            #region Skin

            EditFieldUi(player, ref container, EditLayer, CuiHelper.GetGuid(),
                "80 -155",
                "235 -115",
                $"UI_Crafts edititem {category} {page} {craftId} {itemsPage} {itemId} SkinID ",
                new KeyValuePair<string, object>("SkinID", edit["SkinID"]));

            #endregion

            #region ShortName

            EditFieldUi(player, ref container, EditLayer, CuiHelper.GetGuid(),
                "-85 -205",
                "70 -165",
                $"UI_Crafts edititem {category} {page} {craftId} {itemsPage} {itemId} Shortname ",
                new KeyValuePair<string, object>("ShortName", edit["ShortName"]));

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "80 -205", OffsetMax = "180 -185"
                },
                Text =
                {
                    Text = Msg(player, CraftSelect),
                    Align = TextAnchor.MiddleCenter,
                    FontSize = 10,
                    Font = "robotocondensed-regular.ttf",
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _config.UI.Color4.Get(),
                    Command = $"UI_Crafts selectitem {category} {page} {craftId} {itemsPage} {itemId}"
                }
            }, EditLayer);

            #endregion

            #region Buttons

            var creating = Convert.ToBoolean(_itemEditing[player]["Generated"]);

            #region Save

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                    OffsetMin = $"{(creating ? -90 : -105)} -12",
                    OffsetMax = $"{(creating ? 90 : 75)} 12"
                },
                Text =
                {
                    Text = Msg(player, CraftSaveTitle),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 14,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _config.UI.Color4.Get(),
                    Command = $"UI_Crafts saveitem {category} {page} {craftId} {itemsPage} {itemId}"
                }
            }, EditLayer);

            #endregion

            #region Delete

            if (!creating)
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "80 -12",
                        OffsetMax = "110 12"
                    },
                    Text =
                    {
                        Text = Msg(player, CraftRemoveTitle),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _config.UI.Color11.Get(),
                        Command = $"UI_Crafts removeitem {category} {page} {craftId} {itemsPage} {itemId}"
                    }
                }, EditLayer);

            #endregion

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, EditLayer + ".Background");
            CuiHelper.AddUi(player, container);
        }

        private void SelectItemUi(BasePlayer player, int category, int page, int craftId, int itemsPage, int itemId,
            string selectedCategory = "",
            int localPage = 0,
            string input = "")
        {
            if (string.IsNullOrEmpty(selectedCategory)) selectedCategory = _itemsCategories.FirstOrDefault().Key;

            var container = new CuiElementContainer();

            #region Background

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = _config.UI.Color9.Get()
                }
            }, Layer, EditLayer + ".Background");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text = {Text = ""},
                Button =
                {
                    Color = "0 0 0 0",
                    Command = $"UI_Crafts start_edititem {category} {page} {craftId} {itemsPage} {itemId}"
                }
            }, EditLayer + ".Background");

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
                    Color = _config.UI.Color1.Get()
                }
            }, EditLayer + ".Background", EditLayer);

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
                            ? _config.UI.Color4.Get()
                            : _config.UI.Color2.Get(),
                        Command = $"UI_Crafts selectitem {category} {page} {craftId} {itemsPage} {itemId} {cat.Key}"
                    }
                }, EditLayer);

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
            var Items = temp.Skip(localPage * totalAmount).Take(totalAmount).ToList();

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
                    Image = {Color = _config.UI.Color2.Get()}
                }, EditLayer, EditLayer + $".Item.{item}");

                if (ImageLibrary)
                    container.Add(new CuiElement
                    {
                        Parent = EditLayer + $".Item.{item}",
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
                        Command =
                            $"UI_Crafts takeitem {category} {page} {craftId} {itemsPage} {itemId} {item}"
                    }
                }, EditLayer + $".Item.{item}");

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
                Image = {Color = _config.UI.Color4.Get()}
            }, EditLayer, EditLayer + ".Search");

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
            }, EditLayer + ".Search");

            container.Add(new CuiElement
            {
                Parent = EditLayer + ".Search",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        FontSize = 10,
                        Align = TextAnchor.MiddleLeft,
                        Command =
                            $"UI_Crafts selectitem selectitem {category} {page} {craftId} {itemsPage} {itemId} {selectedCategory} 0 ",
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
                    Color = _config.UI.Color2.Get(),
                    Command = localPage != 0
                        ? $"UI_Crafts selectitem {category} {page} {craftId} {itemsPage} {itemId} {selectedCategory} {localPage - 1} {input}"
                        : ""
                }
            }, EditLayer);

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
                    Color = _config.UI.Color4.Get(),
                    Command = itemsAmount > (localPage + 1) * totalAmount
                        ? $"UI_Crafts selectitem {category} {page} {craftId} {itemsPage} {itemId} {selectedCategory} {localPage + 1} {input}"
                        : ""
                }
            }, EditLayer);

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, EditLayer + ".Background");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Utils

        private bool CanEdit(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, PermEdit);
        }

        private void LoadItems()
        {
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
        }

        private ItemForCraft FindItemById(int id)
        {
            ItemForCraft item;
            return _itemsById.TryGetValue(id, out item) ? item : null;
        }

        private CraftConf FindCraftById(int id)
        {
            CraftConf craft;
            return _craftsById.TryGetValue(id, out craft) ? craft : null;
        }

        private int GetId()
        {
            var result = -1;

            do
            {
                var val = Random.Range(int.MinValue, int.MaxValue);

                if (!_crafts.Exists(craft => craft.ID == val))
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
            permission.RegisterPermission(PermEdit, this);

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
            return category.Crafts.FindAll(craft => (craft.Enabled || CanEdit(player)) &&
                                                    (string.IsNullOrEmpty(craft.Permission) ||
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

                    _recycler.gameObject.AddComponent<GroundWatch>();
                    _recycler.gameObject.AddComponent<DestroyOnGroundMissing>();
                }
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
                                                if (Core.Random.Range(0.0f, 1f) <= num3 * (double) num1)
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
                                            var num5 = Mathf.CeilToInt(num4 / (float) current.itemDef.stackable);
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
                            return (bool) can;

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
            CraftItemAmount = "CraftItemAmount",
            CraftSelect = "CraftSelect",
            CraftCreate = "CraftCreate",
            Back = "Back",
            Next = "Next",
            ItemSearch = "ItemSearch",
            CraftRemoveTitle = "CraftRemoveTitle",
            BtnNext = "BtnNext",
            BtnBack = "BtnBack",
            CraftItemsAddTitle = "CraftItemsAddTitle",
            CraftSaveTitle = "CraftSaveTitle",
            CraftItemsTitle = "CraftItemsTitle",
            CraftWorkbenchTitle = "CraftWorkbenchTitle",
            CraftTypeTitle = "CraftTypeTitle",
            ItemEditingTitle = "ItemEditingTitle",
            CraftEditingTitle = "CraftEditingTitle",
            EnableStructure = "EnableStructure",
            EditGround = "EditGround",
            EditUseDistance = "EditUseDistance",
            EnableCraft = "EnableCraft",
            NotWorkbench = "NotWorkbench",
            CraftsDescription = "CraftsDescription",
            WorkbenchLvl = "WorkbenchLvl",
            GotCraft = "GotCraft",
            NoPermission = "NoPermission",
            SuccessfulCraft = "SuccessfulCraft",
            NotEnoughResources = "NotEnoughResources",
            CraftCancelButton = "CraftCancelButton",
            CraftButton = "CraftButton",
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
                [NotWorkbench] = "Not enough workbench level for craft!",
                [EnableCraft] = "Enabled",
                [EditUseDistance] = "Distance Check",
                [EditGround] = "Place the ground",
                [EnableStructure] = "Place the structure",
                [CraftEditingTitle] = "Creating/editing craft",
                [ItemEditingTitle] = "Creating/editing item",
                [CraftTypeTitle] = "Type",
                [CraftWorkbenchTitle] = "WorkBench",
                [CraftItemsTitle] = "Items",
                [CraftSaveTitle] = "Save",
                [CraftItemsAddTitle] = "+",
                [BtnBack] = "◀",
                [BtnNext] = "▶",
                [CraftRemoveTitle] = "✕",
                [ItemSearch] = "Item search",
                [Back] = "Back",
                [Next] = "Next",
                [CraftCreate] = "Create Craft",
                [CraftSelect] = "Select",
                [CraftItemAmount] = "{0} pcs",
                ["Enabled"] = "Enabled",
                ["Image"] = "Image",
                ["Title"] = "Title",
                ["Description"] = "Description",
                ["CmdToGive"] = "Command (to give an item)",
                ["Permission"] = "Permission (ex: crafts.vip)",
                ["DisplayName"] = "Display Name",
                ["ShortName"] = "ShortName",
                ["Amount"] = "Amount",
                ["SkinID"] = "Skin",
                ["Prefab"] = "Prefab",
                ["GiveCommand"] = "Command on give",
                ["Distance"] = "Distance"
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
            if (_config.UseNotify && (Notify != null || UINotify != null))
                Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
            else
                Reply(player, key, obj);
        }

        #endregion
    }
}