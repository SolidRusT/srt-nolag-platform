using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Simple Loot", "MisterPixie", "1.0.91")]
    [Description("Sets multipliers for any item of your choosing")]
    public class SimpleLoot : RustPlugin
    {
        private static SimpleLoot _instance;

        private Configuration _configuration;

        private void Init()
        {
            Unsubscribe("OnLootSpawn");
            _instance = this;
        }

        private void OnServerInitialized()
        {
            _configuration = new Configuration();
            var containers = UnityEngine.Object.FindObjectsOfType<LootContainer>();
            for (var i = 0; i < containers.Length; i++)
            {
                OnLootSpawn(containers[i]);
                if (i == containers.Length - 1)
                    PrintWarning($"Repopulating {i} loot containers.");
            }

            Subscribe("OnLootSpawn");
        }

        private object OnLootSpawn(LootContainer lootContainer)
        {
            if (lootContainer?.inventory?.itemList == null)
                return null;

            foreach (var item in lootContainer.inventory.itemList.ToList())
            {
                item.RemoveFromWorld();
                item.RemoveFromContainer();
            }

            lootContainer.PopulateLoot();

            foreach (var item in lootContainer.inventory.itemList.ToList())
            {
                var itemBlueprint = ItemManager.FindItemDefinition(item.info.shortname).Blueprint;
                if (_configuration.ReplaceItems && itemBlueprint != null && itemBlueprint.isResearchable)
                {
                    var slot = item.position;
                    item.RemoveFromWorld();
                    item.RemoveFromContainer();
                    var blueprint = ItemManager.CreateByName("blueprintbase");
                    blueprint.blueprintTarget = item.info.itemid;
                    blueprint.MoveToContainer(lootContainer.inventory, slot);
                }
                else
                {
                    object multiplier;
                    if (_configuration.Multipliers.TryGetValue(item.info.shortname, out multiplier))
                        item.amount *= Convert.ToInt32(multiplier);
                }
            }

            if (lootContainer.shouldRefreshContents)
            {
                lootContainer.Invoke(new Action(lootContainer.SpawnLoot), UnityEngine.Random.Range(lootContainer.minSecondsBetweenRefresh, lootContainer.maxSecondsBetweenRefresh));
            }

            return true;

        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new config file...");

        private class Configuration
        {
            public readonly Dictionary<string, object> Multipliers = new Dictionary<string, object>
            {
                {"scrap", 1}
            };

            public readonly bool ReplaceItems;

            public Configuration()
            {
                GetConfig(ref Multipliers, "Settings", "Multipliers");

                GetConfig(ref ReplaceItems, "Settings", "Replace items with blueprints");

                foreach (var itemDefinition in ItemManager.itemList.Where(x => x?.category == ItemCategory.Component))
                {
                    if (itemDefinition.shortname == "bleach" || itemDefinition.shortname == "ducttape" ||
                        itemDefinition.shortname == "glue" || itemDefinition.shortname == "sticks")
                        continue;

                    if (Multipliers.ContainsKey(itemDefinition.shortname))
                        continue;

                    Multipliers.Add(itemDefinition.shortname, 1);
                }

                _instance.SaveConfig();
            }

            private void GetConfig<T>(ref T variable, params string[] path)
            {
                if (path.Length == 0) return;

                if (_instance.Config.Get(path) == null)
                {
                    SetConfig(ref variable, path);
                    _instance.PrintWarning($"Added field to config: {string.Join("/", path)}");
                }

                variable = (T)Convert.ChangeType(_instance.Config.Get(path), typeof(T));
            }

            private void SetConfig<T>(ref T variable, params string[] path) => _instance.Config.Set(path.Concat(new object[] { variable }).ToArray());
        }
    }
}