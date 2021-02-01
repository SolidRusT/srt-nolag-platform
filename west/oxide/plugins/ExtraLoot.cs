using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Extra Loot", "Orange", "1.0.4")]
    [Description("Add extra items (including custom) to any loot container in the game")]
    public class ExtraLoot : RustPlugin
    {
        #region Oxide Hooks
        
        private void OnLootSpawn(StorageContainer container)
        {
            NextTick(() => { SpawnLoot(container); });
        }
        
        #endregion

        #region Core

        private void SpawnLoot(StorageContainer container)
        {
            List<BaseItem> items;
            if (!config.containers.TryGetValue(container.ShortPrefabName, out items))
            {
                return;
            }

            foreach (var value in items)
            {
                if (value.chance >= UnityEngine.Random.Range(0f, 100f))
                {
                    var amount = Core.Random.Range(value.amountMin, value.amountMax + 1);
                    var shortname = value.isBlueprint ? "blueprintbase" : value.shortname;
                    var item = ItemManager.CreateByName(shortname, amount, value.skinID);
                    if (item != null)
                    {
                        item.name = value.displayName;
                        item.blueprintTarget = value.isBlueprint ? ItemManager.FindItemDefinition(value.shortname).itemid : 0;
                        container.inventory.capacity++;
                        item.MoveToContainer(container.inventory);
                    }
                }
            }
        }

        #endregion
        
        #region Configuration
        
        private static ConfigData config;
        
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Shortname -> Items")]
            public Dictionary<string, List<BaseItem>> containers;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData 
            {
                containers = new Dictionary<string, List<BaseItem>>
                {
                    ["crate_normal"] = new List<BaseItem>
                    {
                        new BaseItem
                        {
                            shortname = "pistol.revolver",
                            chance = 50,
                            amountMin = 1,
                            amountMax = 1,
                            skinID = 0,
                            displayName = "Recipe",
                            isBlueprint = true
                        },
                        new BaseItem
                        {
                            shortname = "box.repair.bench",
                            chance = 30,
                            amountMin = 1,
                            amountMax = 2,
                            skinID = 1594245394,
                            displayName = "Recycler",
                            isBlueprint = false
                        },
                        new BaseItem
                        {
                            shortname = "autoturret",
                            chance = 30,
                            amountMin = 1,
                            amountMax = 2,
                            skinID = 1587601905,
                            displayName = "Sentry Turret",
                            isBlueprint = false
                        },
                        new BaseItem
                        {
                            shortname = "paper",
                            chance = 30,
                            amountMin = 1,
                            amountMax = 1,
                            skinID = 1602864474,
                            displayName = "Recipe",
                            isBlueprint = false
                        },
                        new BaseItem
                        {
                            shortname = "paper",
                            chance = 30,
                            amountMin = 1,
                            amountMax = 1,
                            skinID = 1602955228,
                            displayName = "Recipe",
                            isBlueprint = false
                        },
                    },
                    ["crate_elite"] = new List<BaseItem>
                    {
                        new BaseItem
                        {
                            shortname = "paper",
                            chance = 15,
                            amountMin = 1,
                            amountMax = 1,
                            skinID = 1602864474,
                            displayName = "Recipe"
                        },
                        new BaseItem
                        {
                            shortname = "paper",
                            chance = 15,
                            amountMin = 1,
                            amountMax = 1,
                            skinID = 1602955228,
                            displayName = "Recipe"
                        },
                    },
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
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        
        #endregion

        #region Classes

        private class BaseItem
        {
            [JsonProperty(PropertyName = "1. Shortname")]
            public string shortname;
            
            [JsonProperty(PropertyName = "2. Chance")]
            public float chance;
            
            [JsonProperty(PropertyName = "3. Minimal amount")]
            public int amountMin;
            
            [JsonProperty(PropertyName = "4. Maximal amount")]
            public int amountMax;
            
            [JsonProperty(PropertyName = "5. Skin ID")]
            public ulong skinID;
            
            [JsonProperty(PropertyName = "6. Display name")]
            public string displayName;

            [JsonProperty(PropertyName = "7. Blueprint")]
            public bool isBlueprint;
        }

        #endregion
    }
}