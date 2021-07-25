using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Auto Fuel", "redBDGR", "1.2.0")]
    [Description("Automatically fuels lights using fuel from the tool cupboard's inventory")]
    class AutoFuel : RustPlugin
    {
        private bool Changed;

        private bool dontRequireFuel;
        private List<object> activeShortNames = new List<object>();

        private static AutoFuel plugin;

        #region Configuration

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            dontRequireFuel = Convert.ToBoolean(GetConfig("Settings", "Don't use fuel", false));
            activeShortNames = (List<object>)GetConfig("Settings", "Allowed objects", GenDefaultList());

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        private List<object> GenDefaultList()
        {
            return new List<object>
            {
                "bbq.deployed",
                "campfire",
                "ceilinglight.deployed",
                "fireplace.deployed",
                "furnace",
                "furnace.large",
                "jackolantern.angry",
                "jackolantern.happy",
                "lantern.deployed",
                "refinery_small_deployed",
                "searchlight.deployed",
                "skull_fire_pit",
                "tunalight.deployed",
                "fogmachine",
                "snowmachine",
                "chineselantern.deployed",
                "small_fuel_generator.deployed"
            };
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            plugin = this;
            LoadVariables();
        }

        private void OnServerInitialized()
        {
            ServerMgr.Instance.StartCoroutine(FindOvens());
        }

        private void Unload()
        {
            foreach (AutomaticRefuel refuel in UnityEngine.Object.FindObjectsOfType<AutomaticRefuel>())
                UnityEngine.Object.Destroy(refuel);

            foreach (AutomaticRefuelGenerator refuel in UnityEngine.Object.FindObjectsOfType<AutomaticRefuelGenerator>()) 
                UnityEngine.Object.Destroy(refuel);
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity.GetComponent<BaseEntity>()?.ShortPrefabName == "jackolantern.angry" || entity.GetComponent<BaseEntity>()?.ShortPrefabName == "jackolantern.happy")
                entity.GetComponent<BaseOven>().fuelType = ItemManager.FindItemDefinition("wood");

            BaseOven oven = entity.GetComponent<BaseOven>();
            if (oven && activeShortNames.Contains(oven.ShortPrefabName) && !oven.GetComponent<AutomaticRefuel>())
                oven.gameObject.AddComponent<AutomaticRefuel>();

            else if (entity is FuelGenerator && activeShortNames.Contains(entity.ShortPrefabName) && !entity.GetComponent<AutomaticRefuelGenerator>())
                entity.gameObject.AddComponent<AutomaticRefuelGenerator>();
        }

        private Item OnFindBurnable(BaseOven oven)
        {
            if (oven.fuelType == null || !activeShortNames.Contains(oven.ShortPrefabName) || HasFuel(oven))
                return null;

            DecayEntity decayEnt = oven.GetComponent<DecayEntity>();
            if (decayEnt == null)
                return null;

            AutomaticRefuel refuel = decayEnt.GetComponent<AutomaticRefuel>();
            if (!refuel)
                refuel = decayEnt.gameObject.AddComponent<AutomaticRefuel>();

            if (refuel.cupboard == null)
            {
                refuel.SearchForCupboard();
                if (refuel.cupboard == null)
                    return null;
            }

            if (dontRequireFuel)
                return ItemManager.CreateByName(oven.fuelType.shortname, 1);

            Item fuelItem = refuel.GetFuel();
            if (fuelItem == null)
                return null;

            RemoveItemThink(fuelItem);

            ItemManager.CreateByName(oven.fuelType.shortname, 1)?.MoveToContainer(oven.inventory);
            return null;
        }

        void OnItemUse(Item item, int amountToUse)
        {
            if (item.GetRootContainer() != null && item.GetRootContainer().entityOwner != null && item.GetRootContainer().entityOwner is FuelGenerator)
            {
                FuelGenerator gen = item.GetRootContainer().entityOwner as FuelGenerator;

                timer.Once(0.5f, () =>
                {
                    AutomaticRefuelGenerator gencomp;

                    if (gen != null && gen.inventory.itemList.IsEmpty() && gen.TryGetComponent(out gencomp)) gencomp.OutOfFuel();
                });
            }
        }

        #endregion

        #region Custom Components

        private class AutomaticRefuel : FacepunchBehaviour
        {
            public BuildingPrivlidge cupboard;
            public Item cachedFuelItem;
            private BaseOven oven;

            private void Awake()
            {
                oven = GetComponent<BaseOven>();
                SearchForCupboard();
                InvokeRepeating(() => { FuelStillRemains(); }, 5f, 5f);
            }

            public Item FuelStillRemains()
            {
                if (cachedFuelItem == null)
                    return cachedFuelItem;
                if (cachedFuelItem.GetRootContainer() == cupboard.inventory)
                    return cachedFuelItem;
                cachedFuelItem = null;
                return cachedFuelItem;
            }

            public BuildingPrivlidge SearchForCupboard()
            {
                cupboard = oven.GetBuildingPrivilege();
                return cupboard;
            }

            private Item SearchForFuel()
            {
                cachedFuelItem = cupboard.inventory?.itemList?.FirstOrDefault(item => item.info == oven.fuelType);
                return cachedFuelItem;
            }

            public Item GetFuel()
            {
                if (cachedFuelItem != null)
                {
                    Item item = FuelStillRemains();
                    if (item != null)
                        return item;
                }

                return SearchForFuel();
            }
        }

        private class AutomaticRefuelGenerator : MonoBehaviour
        {
            public BuildingPrivlidge tc;
            public Item cachedFuelItem;
            protected FuelGenerator generator;
            protected ItemContainer inventory;

            private void Awake()
            {
                generator = GetComponent<FuelGenerator>();
                inventory = generator.inventory;
                tc = generator.GetBuildingPrivilege();
            }

            private Item SearchForFuel()
            {
                return tc.inventory?.itemList?.FirstOrDefault(item => item.info == ItemManager.FindItemDefinition(-946369541));
            }

            public void OutOfFuel()
            {
                Item fuel = SearchForFuel();

                if (fuel != null)
                {
                    if (!plugin.dontRequireFuel) RemoveItemThink(fuel);

                    ItemManager.CreateByItemID(fuel.info.itemid, 1).MoveToContainer(inventory);
                }
            }
        }

        #endregion

        #region Methods

        private IEnumerator FindOvens()
        {
            BaseOven[] ovens = UnityEngine.Object.FindObjectsOfType<BaseOven>();
            foreach (BaseOven oven in ovens)
            {
                yield return new WaitForSeconds(0.05f);
                if (oven.fuelType == null)
                    continue;
                if (!activeShortNames.Contains(oven.ShortPrefabName))
                    continue;
                AutomaticRefuel refuel = oven.GetComponent<AutomaticRefuel>();
                if (!refuel)
                    oven.gameObject.AddComponent<AutomaticRefuel>();
            }

            FuelGenerator[] generators = UnityEngine.Object.FindObjectsOfType<FuelGenerator>();
            foreach (FuelGenerator gen in generators)
            {
                if (!activeShortNames.Contains(gen.ShortPrefabName)) break;
                yield return new WaitForSeconds(0.5f);

                AutomaticRefuelGenerator refuel;
                if (!gen.TryGetComponent(out refuel)) gen.gameObject.AddComponent<AutomaticRefuelGenerator>();
            }
        }

        private bool HasFuel(BaseOven oven)
        {
            return oven.inventory.itemList.Any(item => item.info == oven.fuelType);
        }

        private static void RemoveItemThink(Item item)
        {
            if (item == null)
                return;
            if (item.amount == 1)
            {
                item.RemoveFromContainer();
                item.RemoveFromWorld();
            }
            else
            {
                item.amount = item.amount - 1;
                item.MarkDirty();
            }
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (data.TryGetValue(datavalue, out value)) return value;
            value = defaultValue;
            data[datavalue] = value;
            Changed = true;
            return value;
        }

        #endregion
    }
}
