using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Extra Gather Bonuses", "Orange", "1.0.4")]
    [Description("Get extra items on gathering resources")]
    public class ExtraGatherBonuses : RustPlugin
    {
        #region Vars

        private Dictionary<string, GatherInfo> bonuses = new Dictionary<string, GatherInfo>();

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            foreach (var value in config.list)
            {
                bonuses.Add(value.resource, value);
                if (!permission.PermissionExists(value.perm,this))
                {
                    permission.RegisterPermission(value.perm, this);
                }
            }
        }

        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            OnGather(player, item.info.shortname);
        }

        private void OnCropGather(GrowableEntity  plant, Item item, BasePlayer player)
        {
            OnGather(player, item.info.shortname);
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            OnGather(player, item.info.shortname);
        }

        #endregion

        #region Core

        private void OnGather(BasePlayer player, string name)
        {
            var list = bonuses.Where(x => x.Key == name).ToList();
            if (list.Count == 0)
            {
                return;
            }

            foreach (var value in list)
            {
                CheckBonus(player, value.Value);
            }
        }

        private void CheckBonus(BasePlayer player, GatherInfo info)
        {
            if (permission.UserHasPermission(player.UserIDString, info.perm) == false)
            {
                return;
            }

            var amount = 0;
            var max = info.maxItems;
            foreach (var def in info.extra)
            {
                if (max != 0 && amount >= max)
                {
                    break;
                }

                var random = Core.Random.Range(0, 100);
                if (random > def.chance)
                {
                    continue;
                }

                var item = CreateItem(def);
                if (item != null)
                {
                    player.GiveItem(item);
                    Message(player, "Received", def.displayName);
                }

                amount++;
            }
        }

        private Item CreateItem(BaseItem def)
        {
            var amount = Core.Random.Range(def.amountMin, def.amountMax + 1);
            var item = ItemManager.CreateByName(def.shortname, amount, def.skinId);
            if (item == null)
            {
                PrintWarning($"Can't create item ({def.shortname})");
                return null;
            }

            item.name = def.displayName;
            return item;
        }

        #endregion

        #region Configuration 1.1.0

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "A. Bonus list")]
            public List<GatherInfo> list;
        }

        private class GatherInfo
        {
            [JsonProperty(PropertyName = "Item gathered to get bonus")]
            public string resource;

            [JsonProperty(PropertyName = "Permission")]
            public string perm;

            [JsonProperty(PropertyName = "Maximal items that player can get by once")]
            public int maxItems;

            [JsonProperty(PropertyName = "Bonus list")]
            public List<BaseItem> extra;
        }

        private class BaseItem
        {
            [JsonProperty(PropertyName = "Shortname")]
            public string shortname;

            [JsonProperty(PropertyName = "Amount min")]
            public int amountMin = 1;

            [JsonProperty(PropertyName = "Amount max")]
            public int amountMax = 1;

            [JsonProperty(PropertyName = "Skin")]
            public ulong skinId;

            [JsonProperty(PropertyName = "Display name")]
            public string displayName;

            [JsonProperty(PropertyName = "Chance")]
            public int chance;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                list = new List<GatherInfo>
                {
                    new GatherInfo
                    {
                        resource = "cloth",
                        maxItems = 0,
                        perm = "extragatherbonuses.default",
                        extra = new List<BaseItem>
                        {
                            new BaseItem
                            {
                                shortname = "bandage",
                                amountMin = 1,
                                amountMax = 5,
                                skinId = 0,
                                displayName = "Apple",
                                chance = 50
                            }
                        }
                    },
                    new GatherInfo
                    {
                        resource = "wood",
                        maxItems = 0,
                        perm = "extragatherbonuses.default",
                        extra = new List<BaseItem>
                        {
                            new BaseItem
                            {
                                shortname = "apple",
                                amountMin = 1,
                                amountMax = 5,
                                skinId = 0,
                                displayName = "Apple",
                                chance = 50
                            },
                            new BaseItem
                            {
                                shortname = "apple.spoiled",
                                amountMin = 1,
                                amountMax = 5,
                                skinId = 0,
                                displayName = "Spoiled Apple",
                                chance = 50
                            }
                        }
                    },
                    new GatherInfo
                    {
                        resource = "sulfur.ore",
                        maxItems = 1,
                        perm = "extragatherbonuses.vip",
                        extra = new List<BaseItem>
                        {
                            new BaseItem
                            {
                                shortname = "ammo.rifle",
                                amountMin = 10,
                                amountMax = 50,
                                skinId = 0,
                                displayName = "556 Ammo",
                                chance = 10
                            },
                            new BaseItem
                            {
                                shortname = "ammo.pistol",
                                amountMin = 10,
                                amountMax = 50,
                                skinId = 0,
                                displayName = "9mm Ammo",
                                chance = 10
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("Configuration file is corrupt! Unloading plugin...");
                Interface.Oxide.RootPluginManager.RemovePlugin(this);
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Localization 1.1.1

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Received", "You received {0} for gathering!"},
            }, this);
        }

        private void Message(BasePlayer player, string messageKey, params object[] args)
        {
            if (player == null)
            {
                return;
            }

            var message = GetMessage(messageKey, player.UserIDString, args);
            player.ChatMessage(message);
        }

        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
        }

        #endregion
    }
}