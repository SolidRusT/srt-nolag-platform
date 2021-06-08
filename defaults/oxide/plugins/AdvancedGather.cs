using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Random = UnityEngine.Random;
using Oxide.Plugins.AdvancedGatherEx;
using UnityEngine;
using System;
using Oxide.Core.Plugins;

/*
 * This is a full re-write 2.0.2
 * More features in the works/pending.
 * Added direct inventory drop toggle request.
 * Added random item drops list for trees.
 * Added simple value updater method.
 *
 * This update 2.0.3
 * Added clone support
 * fixed a few default values not being generated.
 *
 * This update 2.0.4
 * Added PopupNotifications support for tree drops
 * Added Chat message support for tree drops
 */

namespace Oxide.Plugins
{
    [Info("AdvancedGather", "Khan", "2.0.4")]
    [Description("Custom gathering with some action's and extension drop")]
    public class AdvancedGather : CovalencePlugin
    {
        #region Refrences
        
        [PluginReference] 
        private Plugin PopupNotifications;

        #endregion
        
        #region Fields
        
        private const string UsePerm = "advancedgather.use";
        private PluginConfig _config;
        private IPlayer _player;

        #endregion

        #region Config
        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new Exception();
                }
                
                if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintError("Loaded default config.");

                LoadDefaultConfig();
            }
        }
        
        protected override void SaveConfig() => Config.WriteObject(_config, true);
        
        private class PluginConfig
        {
            #region 
            
            [JsonProperty("ChatPrefix")]
            public string ChatPrefix = "<color=#32CD32>AdvancedGather</color>: ";
            
            [JsonProperty(PropertyName = "Drop Sound Effect")]
            public string DropSoundEffect;

            [JsonProperty(PropertyName = "Enable Berry Drops from Hemp")]
            public bool EnableBerries;
            
            [JsonProperty(PropertyName = "Enable Random Tree drop Items?")]
            public bool RandomItems;
            
            [JsonProperty(PropertyName = "Switch to direct inventory Drops?")]
            public bool DirectInventory;
            
            [JsonProperty(PropertyName = "List of all chance modifiers")]
            public ChanceModifier ChanceModifiers;

            [JsonProperty(PropertyName = "Random Item drops from Trees")]
            public List<RandomConfig> RandomConfigs;
            
            [JsonProperty(PropertyName = "Get apples from Trees")]
            public AppleConfig AppleConfigs;

            [JsonProperty(PropertyName = "Berry types from Hemp")]
            public List<BerryConfig> BerryConfigs;
            
            [JsonProperty(PropertyName = "Biofuel options")]
            public BioFuelConfig BioFuelConfigs;

            [JsonProperty(PropertyName = "Set times for optimal farming")]
            public List<TimeConfig> TimeConfigs;
            
            public string ToJson() => JsonConvert.SerializeObject(this);
            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());

            #endregion
            public BerryConfig GetRandomBerry()
            {
                List<BerryConfig> berries = new List<BerryConfig>();

                foreach (BerryConfig berryConfig in BerryConfigs)
                {
                    if (Random.Range(1, 100) <= berryConfig.Rarity)
                        berries.Add(berryConfig);
                }

                return berries.Count > 0 ? berries[0] : BerryConfigs.GetRandom();
            }
            
            public RandomConfig GetRandomTreeItem()
            {
                List<RandomConfig> items = new List<RandomConfig>();

                foreach (RandomConfig randomConfig in RandomConfigs)
                {
                    if (Random.Range(1, 100) <= randomConfig.Rarity)
                        items.Add(randomConfig);
                }

                return items.Count > 0 ? items[0] : RandomConfigs.GetRandom();
            }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    DropSoundEffect = "assets/bundled/prefabs/fx/notice/loot.drag.itemdrop.fx.prefab",
                    EnableBerries = true,
                    RandomItems = false,
                    DirectInventory = false,

                    ChanceModifiers = new ChanceModifier
                    {
                        EnableDefaultDropChance = true,
                        DefaultDropChance = 70,
                        TimeModifier = false,
                        ToolModifier = false,
                        ToolModifierBonus = 10,
                        ToolItem1 = "jackhammer",
                        ToolItem2 = "axe.salvaged",
                        ToolItem3 = "rock",
                        AttireModifier = false,
                        AttireModifierBonus = 10,
                        AttireBonousID = -23994173,
                        ItemModifier = false,
                        ItemModifierBonus = 10,
                        ItemBonusID = -1651220691,
                    },
                    RandomConfigs = new List<RandomConfig>
                    {
                        new RandomConfig
                        {
                            DisplayName = "Horse Poop",
                            Shortname = "horsedung",
                            SkinID = 0,
                            RandomAmountMin = 1,
                            RandomAmountMax = 3,
                            Rarity = 50
                        },
                        new RandomConfig
                        {
                            DisplayName = "Poop Rock",
                            Shortname = "rock",
                            SkinID = 2039847176,
                            RandomAmountMin = 1,
                            RandomAmountMax = 3,
                            Rarity = 50
                        },
                    },
                    AppleConfigs = new AppleConfig
                    {
                        Enable = true,
                        GoodApple = 30,
                        BadApple = 20,
                        MinAmount = 1,
                        MaxAmount = 3
                    },
                    BerryConfigs = new List<BerryConfig>
                    {
                        new BerryConfig
                        {
                            DisplayName = "Red Berry",
                            Shortname = "red.berry",
                            SkinID = 0,
                            BerryAmountMin = 1,
                            BerryAmountMax = 3,
                            Rarity = 15
                        },
                        new BerryConfig
                        {
                            DisplayName = "Blue Berry",
                            Shortname = "blue.berry",
                            SkinID = 0,
                            BerryAmountMin = 1,
                            BerryAmountMax = 3,
                            Rarity = 15
                        },
                        new BerryConfig
                        {
                            DisplayName = "Green Berry",
                            Shortname = "green.berry",
                            SkinID = 0,
                            BerryAmountMin = 1,
                            BerryAmountMax = 3,
                            Rarity = 14
                        },
                        new BerryConfig
                        {
                            DisplayName = "Yellow Berry",
                            Shortname = "yellow.berry",
                            SkinID = 0,
                            BerryAmountMin = 1,
                            BerryAmountMax = 3,
                            Rarity = 14
                        },
                        new BerryConfig
                        {
                            DisplayName = "White Berry",
                            Shortname = "white.berry",
                            SkinID = 0,
                            BerryAmountMin = 1,
                            BerryAmountMax = 3,
                            Rarity = 14
                        },
                        new BerryConfig
                        {
                            DisplayName = "Black Berry",
                            Shortname = "black.berry",
                            SkinID = 0,
                            BerryAmountMin = 1,
                            BerryAmountMax = 3,
                            Rarity = 14
                        },
                        new BerryConfig
                        {
                            DisplayName = "Black Raspberries",
                            Shortname = "black.raspberries",
                            SkinID = 0,
                            BerryAmountMin = 1,
                            BerryAmountMax = 3,
                            Rarity = 14
                        },
                        
                    },
                    BioFuelConfigs = new BioFuelConfig
                    {
                        EnableBioFuel = true,
                        DisplayName = "Biofuel",
                        Shortname = "lowgradefuel",
                        SkinID = 2501207890,
                        Pumpkins = new Pumpkin
                        {
                            BioFuelMinAmount = 1,
                            BioFuelMaxAmount = 5,
                            Rarity = 20
                        },
                        PumpkinClones = new PumpkinClone
                        {
                            BioFuelMinAmount = 1,
                            BioFuelMaxAmount = 3,
                            Rarity = 20
                        },
                        Potatos = new Potato
                        {
                            BioFuelMinAmount = 1,
                            BioFuelMaxAmount = 8,
                            Rarity = 20
                        },
                        PotatoClones = new PotatoClone
                        {
                            BioFuelMinAmount = 1,
                            BioFuelMaxAmount = 5,
                            Rarity = 20
                        },
                        Corns = new Corn
                        {
                            BioFuelMinAmount = 1,
                            BioFuelMaxAmount = 10,
                            Rarity = 10
                        },
                        CornClones = new CornClone
                        {
                            BioFuelMinAmount = 1,
                            BioFuelMaxAmount = 8,
                            Rarity = 10
                        },
                    },
                    TimeConfigs = new List<TimeConfig>
                    {
                        new TimeConfig
                        {
                            After = 0f,
                            Before = 4f,
                            DropChanceBonous = 3,
                        },
                        new TimeConfig
                        {
                            After = 8f,
                            Before = 12f,
                            DropChanceBonous = 5,
                        },
                        new TimeConfig
                        {
                            After = 16f,
                            Before = 20f,
                            DropChanceBonous = 7,
                        },
                        new TimeConfig
                        {
                            After = 20f,
                            Before = 24f,
                            DropChanceBonous = 10,
                        },
                    }
                };
            }
        }
        private class ChanceModifier
        {
            [JsonProperty(PropertyName = "Enable Global Default Drop Chance?")]
            public bool EnableDefaultDropChance;
            
            [JsonProperty(PropertyName = "Global Default Drop Chance Value")]
            public int DefaultDropChance;
            
            [JsonProperty(PropertyName = "Enable Time Modifier feature?")]
            public bool TimeModifier;
            
            [JsonProperty(PropertyName = "Enable Tool Modifier feature?")]
            public bool ToolModifier;
            
            [JsonProperty(PropertyName = "Tool Modifier bonous chance")]
            public int ToolModifierBonus;
            
            [JsonProperty(PropertyName = "Tool Item 1 shortname")]
            public string ToolItem1;
            
            [JsonProperty(PropertyName = "Tool Item 2 shortname")]
            public string ToolItem2;
            
            [JsonProperty(PropertyName = "Tool Item 3 shortname")]
            public string ToolItem3;
            
            [JsonProperty(PropertyName = "Enable Attire Modifier feature?")]
            public bool AttireModifier;
            
            [JsonProperty(PropertyName = "Attire Modifier bonous chance")]
            public int AttireModifierBonus;
            
            [JsonProperty(PropertyName = "Attire Modifier Item ID")]
            public int AttireBonousID;
            
            [JsonProperty(PropertyName = "Enable Item Modifier feature?")]
            public bool ItemModifier;
            
            [JsonProperty(PropertyName = "Item Modifier bonous chance")]
            public int ItemModifierBonus;
            
            [JsonProperty(PropertyName = "Item Modifier Item ID")]
            public int ItemBonusID;
        }
        private class RandomConfig
        {
            [JsonProperty(PropertyName = "Set Custom DisplayName")]
            public string DisplayName;
            
            public string Shortname;
            
            [JsonProperty(PropertyName = "Set Custom Skin Id")]
            public ulong SkinID;
            
            [JsonProperty(PropertyName = "Minimum amount of random items to drop")]
            public int RandomAmountMin;
            
            [JsonProperty(PropertyName = "Max amount of random items to drop")]
            public int RandomAmountMax;
            
            public float Rarity;

            public bool GiveItem(BasePlayer player, int amount)
            {
                Item item = ItemManager.CreateByName(Shortname, amount, SkinID);
                item.name = DisplayName;
                item.skin = SkinID;
                item.MarkDirty();

                return player.inventory.GiveItem(item);
            }
        }
        private class AppleConfig
        {
            [JsonProperty(PropertyName = "Enable Apple Drops?")]
            public bool Enable;
            
            [JsonProperty(PropertyName = "Enable Messages for Tree Drops?")]
            public bool AppleMsg;
            
            [JsonProperty(PropertyName = "Use Popup Message for Apple Drops?")]
            public bool UsePopup;
            
            [JsonProperty(PropertyName = "Chance to drop any apples per hit")]
            public int GoodApple;
            
            [JsonProperty(PropertyName = "Chance it drops rotten apples")]
            public int BadApple;

            [JsonProperty(PropertyName = "Minimum amount of Apples")]
            public int MinAmount;
            
            [JsonProperty(PropertyName = "Max amount of Apples")]
            public int MaxAmount;
        }
        private class BerryConfig
        {
            [JsonProperty(PropertyName = "Set Custom DisplayName")]
            public string DisplayName;
            
            public string Shortname;
            
            [JsonProperty(PropertyName = "Set Custom Skin Id")]
            public ulong SkinID;
            
            [JsonProperty(PropertyName = "Minimum amount of Berries to drop")]
            public int BerryAmountMin;
            
            [JsonProperty(PropertyName = "Max amount of Berries to drop")]
            public int BerryAmountMax;
            
            public float Rarity;

            public bool GiveItem(BasePlayer player, int amount)
            {
                Item item = ItemManager.CreateByName(Shortname, amount, SkinID);
                item.name = DisplayName;
                item.skin = SkinID;
                item.MarkDirty();

                return player.inventory.GiveItem(item);
            }
        }
        public class BioFuelConfig
        {
            [JsonProperty(PropertyName = "Enable Biofuel")]
            public bool EnableBioFuel;
            
            [JsonProperty(PropertyName = "Biofuel Custom Name")]
            public string DisplayName;
            
            [JsonProperty(PropertyName = "Biofuel shortname")]
            public string Shortname;
            
            [JsonProperty(PropertyName = "Set Custom Skin Id")]
            public ulong SkinID;
            public Pumpkin Pumpkins { get; set; }
            
            public PumpkinClone PumpkinClones { get; set; }
            public Potato Potatos { get; set; }
            
            public PotatoClone PotatoClones { get; set; }
            public Corn Corns { get; set; }
            
            public CornClone CornClones { get; set; }

            public bool GiveItem(BasePlayer player, int amount)
            {
                Item item = ItemManager.CreateByName(Shortname, amount, SkinID);
                item.skin = SkinID;
                item.name = DisplayName;
                item.MarkDirty();

                return player.inventory.GiveItem(item);
            }
        }
        public class Pumpkin
        {
            [JsonProperty(PropertyName = "Minimum amount of biofuel to drop")]
            public int BioFuelMinAmount;
            
            [JsonProperty(PropertyName = "Max amount of biofuel to drop")]
            public int BioFuelMaxAmount;
            
            [JsonProperty(PropertyName = "Sets the chance of it dropping")]
            public float Rarity;
        }
        public class PumpkinClone
        {
            [JsonProperty(PropertyName = "Minimum amount of biofuel to drop")]
            public int BioFuelMinAmount;
            
            [JsonProperty(PropertyName = "Max amount of biofuel to drop")]
            public int BioFuelMaxAmount;
            
            [JsonProperty(PropertyName = "Sets the chance of it dropping")]
            public float Rarity;
        }
        public class Potato
        {
            [JsonProperty(PropertyName = "Minimum amount of biofuel to drop")]
            public int BioFuelMinAmount;
            
            [JsonProperty(PropertyName = "Max amount of biofuel to drop")]
            public int BioFuelMaxAmount;
            
            [JsonProperty(PropertyName = "Sets the chance of it dropping")]
            public float Rarity;
        }
        public class PotatoClone
        {
            [JsonProperty(PropertyName = "Minimum amount of biofuel to drop")]
            public int BioFuelMinAmount;
            
            [JsonProperty(PropertyName = "Max amount of biofuel to drop")]
            public int BioFuelMaxAmount;
            
            [JsonProperty(PropertyName = "Sets the chance of it dropping")]
            public float Rarity;
        }
        public class Corn
        {
            [JsonProperty(PropertyName = "Minimum amount of biofuel to drop")]
            public int BioFuelMinAmount;
            
            [JsonProperty(PropertyName = "Max amount of biofuel to drop")]
            public int BioFuelMaxAmount;
            
            [JsonProperty(PropertyName = "Sets the chance of it dropping")]
            public float Rarity;
        }
        public class CornClone
        {
            [JsonProperty(PropertyName = "Minimum amount of biofuel to drop")]
            public int BioFuelMinAmount;
            
            [JsonProperty(PropertyName = "Max amount of biofuel to drop")]
            public int BioFuelMaxAmount;
            
            [JsonProperty(PropertyName = "Sets the chance of it dropping")]
            public float Rarity;
        }
        private class TimeConfig
        {
            public float After;
            public float Before;
            public int DropChanceBonous;
        }

        #endregion

        #region Oxide

        private void OnServerInitialized()
        {
            permission.RegisterPermission(UsePerm, this);
        }

        //Get apple from Tree and nodes such as mining rocks
        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            if(player == null || dispenser == null || entity == null || item == null || !_config.AppleConfigs.Enable) return;
            if (!permission.UserHasPermission(player.UserIDString, UsePerm) || !(dispenser.GetComponent<BaseEntity>() is TreeEntity)) return;

            if (_config.DirectInventory)
            {
                player.inventory.GiveItem(AppleChanceRoll(player));
                player.RunEffect(_config.DropSoundEffect);
                return;
            }
            
            AppleChanceRoll(player)?.DropAndTossUpwards(entity.GetDropPosition());

            player.RunEffect(_config.DropSoundEffect);
        }

	    //Natural plants & random stumps 
        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (player == null || item == null || !permission.UserHasPermission(player.UserIDString, UsePerm)) return;
            
            //Get berry from hemp
            if (_config.EnableBerries && item.info.shortname.Contains("cloth"))
            {
                BerryDropChanceRoll(player);
            }

            //Get Lowgrade from planted corn
            if (item.info.shortname.Contains("corn") && !item.info.shortname.Contains("seed.corn"))
            {
                BioFuelDropChanceRoll(player, "corn");
            }
            
            if (item.info.shortname.Contains("clone.corn") && !item.info.shortname.Contains("seed.corn"))
            {
                BioFuelDropChanceRoll(player, "clone.corn");
            }
            
            //Get lowgrade from planted potato's
            if (item.info.shortname.Contains("potato") && !item.info.shortname.Contains("seed.potato"))
            {
                BioFuelDropChanceRoll(player, "potato");
            }
            
            if (item.info.shortname.Contains("clone.potato") && !item.info.shortname.Contains("seed.potato"))
            {
                BioFuelDropChanceRoll(player, "clone.potato");
            }

            //Get Lowgrade from planted pumpkin
            if (item.info.shortname.Contains("pumpkin") && !item.info.shortname.Contains("seed.pumpkin"))
            {
                BioFuelDropChanceRoll(player, "pumpkin");
            }
            
            if (item.info.shortname.Contains("clone.pumpkin") && !item.info.shortname.Contains("seed.pumpkin"))
            {
                BioFuelDropChanceRoll(player, "clone.pumpkin");
            }
        }

	    //Planted plants & cloned plants done.
        private void OnGrowableGathered(GrowableEntity growable, Item item, BasePlayer player)
        {
            if (player == null || growable == null || item == null || !permission.UserHasPermission(player.UserIDString, UsePerm)) return;
            
            //Get berry from planted hemp
            if (_config.EnableBerries && item.info.shortname.Contains("cloth"))
            {
                BerryDropChanceRoll(player);
            }

            //Get Lowgrade from planted corn
            if (item.info.shortname.Contains("corn") && !item.info.shortname.Contains("seed.corn"))
            {
                BioFuelDropChanceRoll(player, "corn");
            }
            
            if (item.info.shortname.Contains("clone.corn") && !item.info.shortname.Contains("seed.corn"))
            {
                BioFuelDropChanceRoll(player, "clone.corn");
            }
            
            //Get lowgrade from planted potato's
            if (item.info.shortname.Contains("potato") && !item.info.shortname.Contains("seed.potato"))
            {
                BioFuelDropChanceRoll(player, "potato");
            }
            
            if (item.info.shortname.Contains("clone.potato") && !item.info.shortname.Contains("seed.potato"))
            {
                BioFuelDropChanceRoll(player, "clone.potato");
            }

            //Get Lowgrade from planted pumpkin
            if (item.info.shortname.Contains("pumpkin") && !item.info.shortname.Contains("seed.pumpkin"))
            {
                BioFuelDropChanceRoll(player, "pumpkin");
            }
            
            if (item.info.shortname.Contains("clone.pumpkin") && !item.info.shortname.Contains("seed.pumpkin"))
            {
                BioFuelDropChanceRoll(player, "clone.pumpkin");
            }
        }

        #endregion
        
        #region Drop Process
        
        private bool RollRandomItemChance()
        {
            var roll = Random.Range(1, 6);
            return roll == 1;
        }
        private bool RollBadOrGood()
        {
            var roll = Random.Range(1, 6);
            return roll == 1;
        }
        public Item AppleChanceRoll(BasePlayer player)
        {
            int totalchance = DropModifier(player);
            if (totalchance > 100)
            {
                PrintError("Total Apple Roll chance cannot exceed 100");
                return null;
            }

            int roll = Random.Range(1, 100);
            
            int amount = Core.Random.Range(_config.AppleConfigs.MinAmount, _config.AppleConfigs.MaxAmount + 1);

            if (roll <= totalchance)
            {
                if (_config.AppleConfigs.Enable && RollBadOrGood())
                {
                    if (_config.AppleConfigs.AppleMsg)
                    {
                        PopupMessageArgs(player, "AppleDrops", "Rotten Apple");
                    }
                    // Bad
                    player.Command("note.inv",352130972, amount.ToString());
                    return ItemManager.CreateByName("apple.spoiled", amount);
                }
                // good
                if (_config.AppleConfigs.AppleMsg)
                {
                    PopupMessageArgs(player, "AppleDrops", "Tasty Apple");
                }
                player.Command("note.inv",1548091822, amount.ToString());
                return ItemManager.CreateByName("apple", amount);
            }
            
            if (_config.RandomItems && RollRandomItemChance())
            {
                // Chance of getting random item 
                RandomItemRoll(player);
            }

            return null;
            // nothing
        }
        private void RandomItemRoll(BasePlayer player)
        {
            RandomConfig randomConfig = _config.GetRandomTreeItem();

            int amount = Core.Random.Range(randomConfig.RandomAmountMin, randomConfig.RandomAmountMax + 1);
            randomConfig.GiveItem(player, amount);
            if (_config.AppleConfigs.AppleMsg)
            {
                PopupMessageArgs(player, "AppleDrops", randomConfig.DisplayName);
            }
            player.Command("note.inv", randomConfig.Shortname, amount.ToString(), randomConfig.DisplayName);
        }
        public void BioFuelDropChanceRoll(BasePlayer player, string shortname)
        {
            int totalchance = DropModifier(player);
            if (totalchance > 100)
            {
                return;
            }

            int roll = Random.Range(1, 100);

            if (shortname == "corn")
            {
                if (roll <= totalchance + _config.BioFuelConfigs.Corns.Rarity)
                {
                    int amount = Core.Random.Range(_config.BioFuelConfigs.Corns.BioFuelMinAmount, _config.BioFuelConfigs.Corns.BioFuelMaxAmount + 1);
                    _config.BioFuelConfigs.GiveItem(player, amount);
                    //player.RunEffect(_config.DropSoundEffect);
                    player.Command("note.inv",-946369541, amount.ToString(), _config.BioFuelConfigs.DisplayName);
                }
            }
            
            if (shortname == "clone.corn")
            {
                if (roll <= totalchance + _config.BioFuelConfigs.CornClones.Rarity)
                {
                    int amount = Core.Random.Range(_config.BioFuelConfigs.CornClones.BioFuelMinAmount, _config.BioFuelConfigs.CornClones.BioFuelMaxAmount + 1);
                    _config.BioFuelConfigs.GiveItem(player, amount);
                    //player.RunEffect(_config.DropSoundEffect);
                    player.Command("note.inv",-946369541, amount.ToString(), _config.BioFuelConfigs.DisplayName);
                }
            }
            
            if (shortname == "potato")
            {
                if (roll <= totalchance + _config.BioFuelConfigs.Potatos.Rarity)
                {
                    int amount = Core.Random.Range(_config.BioFuelConfigs.Potatos.BioFuelMinAmount, _config.BioFuelConfigs.Potatos.BioFuelMaxAmount + 1);
                    _config.BioFuelConfigs.GiveItem(player, amount);
                    //player.RunEffect(_config.DropSoundEffect);
                    player.Command("note.inv",-946369541, amount.ToString(), _config.BioFuelConfigs.DisplayName);
                }
            }
            
            if (shortname == "clone.potato")
            {
                if (roll <= totalchance + _config.BioFuelConfigs.PotatoClones.Rarity)
                {
                    int amount = Core.Random.Range(_config.BioFuelConfigs.PotatoClones.BioFuelMinAmount, _config.BioFuelConfigs.PotatoClones.BioFuelMaxAmount + 1);
                    _config.BioFuelConfigs.GiveItem(player, amount);
                    //player.RunEffect(_config.DropSoundEffect);
                    player.Command("note.inv",-946369541, amount.ToString(), _config.BioFuelConfigs.DisplayName); 
                }
            }
            
            if (shortname == "pumpkin")
            {
                if (roll <= totalchance + _config.BioFuelConfigs.Pumpkins.Rarity)
                {
                    int amount = Core.Random.Range(_config.BioFuelConfigs.Pumpkins.BioFuelMinAmount, _config.BioFuelConfigs.Pumpkins.BioFuelMaxAmount + 1);
                    _config.BioFuelConfigs.GiveItem(player, amount);
                    //player.RunEffect(_config.DropSoundEffect);
                    player.Command("note.inv",-946369541, amount.ToString(), _config.BioFuelConfigs.DisplayName);
                }
            }
            
            if (shortname == "clone.pumpkin")
            {
                if (roll <= totalchance + _config.BioFuelConfigs.PumpkinClones.Rarity)
                {
                    int amount = Core.Random.Range(_config.BioFuelConfigs.PumpkinClones.BioFuelMinAmount, _config.BioFuelConfigs.PumpkinClones.BioFuelMaxAmount + 1);
                    _config.BioFuelConfigs.GiveItem(player, amount);
                    //player.RunEffect(_config.DropSoundEffect);
                    player.Command("note.inv",-946369541, amount.ToString(), _config.BioFuelConfigs.DisplayName);
                }
            }

            // Received nothing 
        }
        public void BerryDropChanceRoll(BasePlayer player)
        {
            int totalchance = DropModifier(player);
            if (totalchance > 100)
            {
                //PopupMessageArgs(player, "Warning");
                return;
            }

            int roll = Random.Range(1, 100);

            if (roll <= totalchance)
            {
                BerryTypeRoll(player);
            }

            // Received nothing 
        }
        private void BerryTypeRoll(BasePlayer player)
        {
            BerryConfig berryConfig = _config.GetRandomBerry();

            int amount = Core.Random.Range(berryConfig.BerryAmountMin, berryConfig.BerryAmountMax + 1);
            berryConfig.GiveItem(player, amount);
            //player.RunEffect(_config.DropSoundEffect);
            player.Command("note.inv", berryConfig.Shortname, amount.ToString(), berryConfig.DisplayName);
        }
        private int DropModifier(BasePlayer player)
        {
            int chances = new int();
            if (_config.ChanceModifiers.ToolModifier)
            {
                Item activeItem = player.GetActiveItem();
                if (activeItem != null)
                {
                    if (activeItem.info.shortname == _config.ChanceModifiers.ToolItem1 || activeItem.info.shortname == _config.ChanceModifiers.ToolItem2 || activeItem.info.shortname == _config.ChanceModifiers.ToolItem3)
                    {
                        chances += _config.ChanceModifiers.ToolModifierBonus;
                    }
                }
            }

            if (_config.ChanceModifiers.AttireModifier)
            {
                int hasBoonieOn = player.inventory.containerWear.GetAmount(_config.ChanceModifiers.AttireBonousID, true);
                if (hasBoonieOn > 0) chances += _config.ChanceModifiers.AttireModifierBonus;
            }

            if (_config.ChanceModifiers.ItemModifier)
            {
                int hasPookie = player.inventory.containerMain.GetAmount(_config.ChanceModifiers.ItemBonusID, true);
                if (hasPookie > 0) chances += _config.ChanceModifiers.ItemModifierBonus;
            }

            if (_config.ChanceModifiers.TimeModifier)
            {
                var currenttime = TOD_Sky.Instance.Cycle.Hour;
                TimeConfig timeConfig = _config.TimeConfigs.FirstOrDefault(x => currenttime < x.Before && currenttime > x.After);

                if (timeConfig != null)
                {
                    chances += timeConfig.DropChanceBonous;
                }
            }

            int ddc = 0;
            if (_config.ChanceModifiers.EnableDefaultDropChance)
            {
                ddc = _config.ChanceModifiers.DefaultDropChance;
            }

            int totalchance = chances + ddc;
            return totalchance;
        }

        #endregion

        #region Lang System
        public void PopupMessageArgs(BasePlayer player, string key, params object[] args)
        {
            if (_config.AppleConfigs.UsePopup)
            {
                PopupNotifications?.Call("CreatePopupNotification", _config.ChatPrefix + Lang(key, player.UserIDString, args), player);
            }
            else
            {
                player.ChatMessage(_config.ChatPrefix + Lang(key, player.UserIDString, args));
            }
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AppleDrops"] = "A {0} has fallen from the tree",
            }, this);
        }
        
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
    #region Extension Methods

    namespace AdvancedGatherEx
    {
        public static class PlayerEx
        {
            public static void RunEffect(this BasePlayer player, string prefab)
            {
                Effect effect = new Effect();
                effect.Init(Effect.Type.Generic, player.ServerPosition, Vector3.zero);
                effect.pooledString = prefab;
                EffectNetwork.Send(effect, player.Connection);
            }
        }
    }    

    #endregion
}