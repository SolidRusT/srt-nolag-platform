using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ChestStacks", "MON@H", "1.3.3")]
    [Description("Higher stack sizes in storage containers.")]

    public class ChestStacks : RustPlugin //Hobobarrel_static, item_drop
    {
        #region Class Fields

        [PluginReference] private RustPlugin WeightSystem;

        #endregion Class Fields

        #region Configuration

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Global settings")]
            public GlobalSettings globalSettings = new GlobalSettings();

            [JsonProperty(PropertyName = "Stack settings")]
            public ChatSettings stacksSettings = new ChatSettings();

            public class GlobalSettings
            {
                [JsonProperty(PropertyName = "Default Multiplier for new containers")]
                public float defaultContainerMultiplier = 1f;
            }

            public class ChatSettings
            {
                [JsonProperty(PropertyName = "Containers list (shortPrefabName: multiplier)")]
                public Dictionary<string, float> containers = new Dictionary<string, float>()
                {
                    {"autoturret_deployed", 1f},
                    {"bbq.deployed", 1f},
                    {"bigwheelbettingterminal", 1f},
                    {"box.wooden.large", 1f},
                    {"campfire", 1f},
                    {"coffinstorage", 1f},
                    {"composter", 1f},
                    {"crudeoutput", 1f},
                    {"cupboard.tool.deployed", 1f},
                    {"cursedcauldron.deployed", 1f},
                    {"engine", 1f},
                    {"excavator_output_pile", 1f},
                    {"fireplace.deployed", 1f},
                    {"fridge.deployed", 1f},
                    {"fuel_storage", 1f},
                    {"fuelstorage", 1f},
                    {"furnace", 1f},
                    {"furnace.large", 1f},
                    {"fusebox", 1f},
                    {"guntrap.deployed", 1f},
                    {"hitchtrough.deployed", 1f},
                    {"hopperoutput", 1f},
                    {"item_drop", 1f},
                    {"item_drop_backpack", 1f},
                    {"lantern.deployed", 1f},
                    {"locker.deployed", 1f},
                    {"mixingtable.deployed", 1f},
                    {"modular_car_fuel_storage", 1f},
                    {"npcvendingmachine_attire", 1f},
                    {"npcvendingmachine_components", 1f},
                    {"npcvendingmachine_extra", 1f},
                    {"npcvendingmachine_farming", 1f},
                    {"npcvendingmachine_resources", 1f},
                    {"planter.large.deployed", 1f},
                    {"recycler_static", 1f},
                    {"refinery_small_deployed", 1f},
                    {"repairbench_deployed", 1f},
                    {"repairbench_static", 1f},
                    {"researchtable_deployed", 1f},
                    {"researchtable_static", 1f},
                    {"rowboat_storage", 1f},
                    {"shopkeeper_vm_invis", 1f},
                    {"skull_fire_pit", 1f},
                    {"small_refinery_static", 1f},
                    {"supply_drop", 1f},
                    {"survivalfishtrap.deployed", 1f},
                    {"testridablehorse", 1f},
                    {"vendingmachine.deployed", 1f},
                    {"water.pump.deployed", 1f},
                    {"waterbarrel", 1f},
                    {"woodbox_deployed", 1f},
                    {"workbench1.deployed", 1f},
                    {"workbench1.static", 1f},
                    {"workbench2.deployed", 1f},
                    {"workbench3.deployed", 1f}
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                    LoadDefaultConfig();
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(configData);

        #endregion Configuration

        #region Hooks

        object OnMaxStackable(Item item)
        {
            if (WeightSystemLoaded())
            {
                return null;
            }

            if (item.info.itemType == ItemContainer.ContentsType.Liquid)
            {
                return null;
            }
            if (item.info.stackable == 1)
            {
                return null;
            }
            if (TargetContainer != null)
            {
                var entity = TargetContainer.entityOwner ?? TargetContainer.playerOwner;
                if (entity != null)
                {
                    int stacksize = Mathf.FloorToInt(GetStackSize(entity) * item.info.stackable);
                    TargetContainer = null;
                    return stacksize;
                }
            }
            if (item?.parent?.entityOwner != null)
            {
                int stacksize = Mathf.FloorToInt(GetStackSize(item.parent.entityOwner) * item.info.stackable);
                return stacksize;
            }
            return null;
        }

        private ItemContainer TargetContainer;

        object CanMoveItem(Item movedItem, PlayerInventory playerInventory, uint targetContainerID, int targetSlot, int amount)
        {
            if (WeightSystemLoaded()) return null;
            if (movedItem == null || playerInventory == null) return null;

            var container = playerInventory.FindContainer(targetContainerID);
            var player = playerInventory.GetComponent<BasePlayer>();
            var lootContainer = playerInventory.loot?.FindContainer(targetContainerID);

            TargetContainer = container;

            //Puts($"TargetSlot {targetSlot} Amount {amount} TargetContainer {targetContainerID}");

            // Right-Click Overstack into Player Inventory
            if (targetSlot == -1)  
            {
                if (lootContainer == null) 
                {
                    if (movedItem.amount > movedItem.info.stackable)
                    {
                        int loops = 1;
                        if (player != null && player.serverInput.IsDown(BUTTON.SPRINT))
                        {
                            loops = Mathf.CeilToInt((float)movedItem.amount / movedItem.info.stackable);
                        }
                        for (int i = 0; i < loops; i++)
                        {
                            if (movedItem.amount <= movedItem.info.stackable)
                            {
                                if (container != null)
                                {
                                    movedItem.MoveToContainer(container, targetSlot);
                                }
                                else
                                {
                                    playerInventory.GiveItem(movedItem);
                                }
                                break;
                            }
                            var itemToMove = movedItem.SplitItem(movedItem.info.stackable);
                            bool moved = false;
                            if (container != null)
                            {
                                moved = itemToMove.MoveToContainer(container, targetSlot);
                            }
                            else
                            {
                                moved = playerInventory.GiveItem(itemToMove);
                            }
                            if (moved == false)
                            {
                                movedItem.amount += itemToMove.amount;
                                itemToMove.Remove();
                                break;
                            }
                            if (movedItem != null)
                            {
                                movedItem.MarkDirty();
                            }
                        }
                        playerInventory.ServerUpdate(0f);
                        return false;
                    }
                }
                // Shift Right click into storage container
                else
                {
                    if (player != null && player.serverInput.IsDown(BUTTON.SPRINT))
                    {
                        foreach (var item in playerInventory.containerMain.itemList.Where(x => x.info == movedItem.info).ToList())
                        {
                            if (!item.MoveToContainer(lootContainer))
                            {
                                continue;
                            }
                        }
                        foreach (var item in playerInventory.containerBelt.itemList.Where(x => x.info == movedItem.info).ToList())
                        {
                            if (!item.MoveToContainer(lootContainer))
                            {
                                continue;
                            }
                        }
                        playerInventory.ServerUpdate(0f);
                        return false;
                    }
                }
            }
            // Moving Overstacks Around In Chest
            if (amount > movedItem.info.stackable && lootContainer != null)
            {
                var targetItem = container.GetSlot(targetSlot);
                if (targetItem == null)
                {// Split item into chest
                    if (amount < movedItem.amount)
                    {
                        ItemHelper.SplitMoveItem(movedItem, amount, container, targetSlot);
                    }
                    else
                    {// Moving items when amount > info.stacksize
                        movedItem.MoveToContainer(container, targetSlot);
                    }
                }
                else
                {
                    if (!targetItem.CanStack(movedItem) && amount == movedItem.amount)
                    {// Swapping positions of items
                        ItemHelper.SwapItems(movedItem, targetItem);
                    }
                    else
                    {
                        if (amount < movedItem.amount)
                        {
                            ItemHelper.SplitMoveItem(movedItem, amount, playerInventory);
                        }
                        else
                        {
                            movedItem.MoveToContainer(container, targetSlot);
                        }
                        // Stacking items when amount > info.stacksize
                    }
                }
                playerInventory.ServerUpdate(0f);
                return false;
            }

            // Prevent Moving Overstacks To Inventory
            if (lootContainer != null)
            {
                var targetItem = container.GetSlot(targetSlot);
                if (targetItem != null)
                {
                    if (movedItem.parent.playerOwner == player)
                    {
                        if (!movedItem.CanStack(targetItem))
                        {
                            if (targetItem.amount > targetItem.info.stackable)
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return null;
        }
        
        // Covers dropping overstacks from chests onto the ground
        void OnItemDropped(Item item, BaseEntity entity)
        {
            if (item == null || entity == null) return;
            item.RemoveFromContainer();
            int stackSize = item.MaxStackable();
            if (item.amount > stackSize)
            {
                int loops = Mathf.FloorToInt((float)item.amount / stackSize);
                if (loops > 20)
                {
                    return;
                }
                for (int i = 0; i < loops; i++)
                {
                    if (item.amount <= stackSize)
                    {
                        break;
                    }
                    var splitItem = item.SplitItem(stackSize);
                    if (splitItem != null)
                    {
                        splitItem.Drop(entity.transform.position, entity.GetComponent<Rigidbody>().velocity + Vector3Ex.Range(-1f, 1f));
                    }
                }
            }
        }
        #endregion Hooks

        #region Plugin API

        [HookMethod("GetChestSize")]
        object GetChestSize_PluginAPI(BaseEntity entity)
        {
            if (entity == null)
            {
                return 1f;
            }
            return GetStackSize(entity);
        }

        #endregion Plugin API

        #region Helpers

        private bool WeightSystemLoaded()
        {
            return WeightSystem != null && WeightSystem.IsLoaded;
        }

        public class ItemHelper
        {
            public static bool SplitMoveItem(Item item, int amount, ItemContainer targetContainer, int targetSlot)
            {
                var splitItem = item.SplitItem(amount);
                if (splitItem == null)
                {
                    return false;
                }
                if (!splitItem.MoveToContainer(targetContainer, targetSlot))
                {
                    item.amount += splitItem.amount;
                    splitItem.Remove();
                }
                return true;
            }

            public static bool SplitMoveItem(Item item, int amount, BasePlayer player)
            {
                return SplitMoveItem(item, amount, player.inventory);
            }

            public static bool SplitMoveItem(Item item, int amount, PlayerInventory inventory)
            {
                var splitItem = item.SplitItem(amount);
                if (splitItem == null)
                {
                    return false;
                }
                if (!inventory.GiveItem(splitItem))
                {
                    item.amount += splitItem.amount;
                    splitItem.Remove();
                }
                return true;
            }

            public static void SwapItems(Item item1, Item item2)
            {
                var container1 = item1.parent;
                var container2 = item2.parent;
                var slot1 = item1.position;
                var slot2 = item2.position;
                item1.RemoveFromContainer();
                item2.RemoveFromContainer();
                item1.MoveToContainer(container2, slot2);
                item2.MoveToContainer(container1, slot1);
            }
        }

        public float GetStackSize(BaseEntity entity)
        {
            if (entity is LootContainer || entity is BaseCorpse || entity is BasePlayer)
            {
                return 1f;
            }

            return GetContainerMultiplier(entity.ShortPrefabName);
        }

        private float GetContainerMultiplier(string containerName)
        {
            float multiplier;
            if (configData.stacksSettings.containers.TryGetValue(containerName, out multiplier))
            {
                return multiplier;
            }

            configData.stacksSettings.containers[containerName] = configData.globalSettings.defaultContainerMultiplier;
            configData.stacksSettings.containers = SortDictionary(configData.stacksSettings.containers);
            SaveConfig();
            return configData.globalSettings.defaultContainerMultiplier;
        }

        private Dictionary<string, float> SortDictionary(Dictionary<string, float> dic)
        {
            return dic.OrderBy(key => key.Key)
                .ToDictionary(key => key.Key, value => value.Value);
        }

        #endregion Helpers
    }
}