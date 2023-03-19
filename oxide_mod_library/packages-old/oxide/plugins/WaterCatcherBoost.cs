using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Water Catcher Boost", "Substrata", "1.0.2")]
    [Description("Boosts the collection rate of water catchers & pumps")]

    class WaterCatcherBoost : RustPlugin
    {
        private HashSet<WaterCatcher> wCatcherList = new HashSet<WaterCatcher>();
        private HashSet<WaterPump> wPumpList = new HashSet<WaterPump>();

        System.Random rnd = new System.Random();

        int lMin; int lMax; int sMin; int sMax; int wpMin; int wpMax;

        private void OnServerInitialized()
        {
            lMin = configData.largeCatchers.MinBoost;
            lMax = configData.largeCatchers.MaxBoost;
            sMin = configData.smallCatchers.MinBoost;
            sMax = configData.smallCatchers.MaxBoost;
            wpMin = configData.waterPumps.MinBoost;
            wpMax = configData.waterPumps.MaxBoost;

            if (lMin > lMax || sMin > sMax || wpMin > wpMax)
            {
                if (lMin > lMax) lMin = lMax;
                if (sMin > sMax) sMin = sMax;
                if (wpMin > wpMax) wpMin = wpMax;
                PrintWarning("Warning! Maximum values must be greater than or equal to minimum values.\nSee the documentation for more info.");
            }

            foreach (WaterCatcher wCatcher in BaseNetworkable.serverEntities.Where(x => x is WaterCatcher))
            {
                wCatcherList.Add(wCatcher);
            }

            foreach (WaterPump wPump in BaseNetworkable.serverEntities.Where(x => x is WaterPump))
            {
                wPumpList.Add(wPump);
            }

            timer.Every(60f, () =>
            {
                AddToCatchers();
                AddToPumps();
            });
        }

        void AddToCatchers()
        {
            if (wCatcherList == null || wCatcherList.Count == 0) return;
            if (lMax < 1 && sMax < 1) return;

            ItemDefinition water = ItemManager.FindItemDefinition("water");
            if (water == null) return;

            foreach (WaterCatcher wCatcher in wCatcherList)
            {
                if (wCatcher != null && !wCatcher.IsFull())
                {
                    if (wCatcher.ShortPrefabName == "water_catcher_large" && lMax >= 1)
                    {
                        int amount;
                        if (lMin == lMax) amount = lMax;
                        else amount = rnd.Next(lMin, lMax + 1);

                        if (amount >= 1) wCatcher.inventory?.AddItem(water, amount);
                    }
                    if (wCatcher.ShortPrefabName.Contains("water_catcher_small") && sMax >= 1)
                    {
                        int amount;
                        if (sMin == sMax) amount = sMax;
                        else amount = rnd.Next(sMin, sMax + 1);

                        if (amount >= 1) wCatcher.inventory?.AddItem(water, amount);
                    }
                }
            }
        }

        void AddToPumps()
        {
            if (wPumpList == null || wPumpList.Count == 0) return;
            if (wpMax < 1) return;

            foreach (WaterPump wPump in wPumpList)
            {
                if (wPump != null && !wPump.IsFull() && wPump.HasFlag(BaseEntity.Flags.Reserved8))
                {
                    int amount;
                    if (wpMin == wpMax) amount = wpMax;
                    else amount = rnd.Next(wpMin, wpMax + 1);

                    if (amount >= 1)
                    {
                        ItemDefinition water = WaterResource.GetAtPoint(wPump.WaterResourceLocation.position);
                        if (water != null) wPump.inventory?.AddItem(water, amount);
                    }
                }
            }
        }

        void OnEntitySpawned(WaterCatcher wCatcher)
        {
            if (!wCatcherList.Contains(wCatcher)) wCatcherList.Add(wCatcher);
        }

        void OnEntityKill(WaterCatcher wCatcher)
        {
            if (wCatcherList.Contains(wCatcher)) wCatcherList.Remove(wCatcher);
        }

        void OnEntitySpawned(WaterPump wPump)
        {
            if (!wPumpList.Contains(wPump)) wPumpList.Add(wPump);
        }

        void OnEntityKill(WaterPump wPump)
        {
            if (wPumpList.Contains(wPump)) wPumpList.Remove(wPump);
        }

        #region Config
        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Large Water Catchers")]
            public LargeCatchers largeCatchers = new LargeCatchers();
            [JsonProperty(PropertyName = "Small Water Catchers")]
            public SmallCatchers smallCatchers = new SmallCatchers();
            [JsonProperty(PropertyName = "Water Pumps")]
            public WaterPumps waterPumps = new WaterPumps();
        }

        private class LargeCatchers
        {
            [JsonProperty(PropertyName = "Minimum Boost (per minute)")]
            public int MinBoost = 0;
            [JsonProperty(PropertyName = "Maximum Boost (per minute)")]
            public int MaxBoost = 60;
        }

        private class SmallCatchers
        {
            [JsonProperty(PropertyName = "Minimum Boost (per minute)")]
            public int MinBoost = 0;
            [JsonProperty(PropertyName = "Maximum Boost (per minute)")]
            public int MaxBoost = 20;
        }

        private class WaterPumps
        {
            [JsonProperty(PropertyName = "Minimum Boost (per minute)")]
            public int MinBoost = 0;
            [JsonProperty(PropertyName = "Maximum Boost (per minute)")]
            public int MaxBoost = 510;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => configData = new ConfigData();
        protected override void SaveConfig() => Config.WriteObject(configData);
        #endregion
    }
}