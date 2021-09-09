using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/*
 * Has WearContainer Anti Stacking Duplication features/bug fixes
 * Fixes Custom skin splitting issues + Custom Item Names. Making oranges skinsfix plugin not required/needed. 
 * Has vending machine no ammo patch toggle (so it won't affect default map vending machine not giving out stock ammo.
 * Doesn't have ammo duplication with repair bench by skin manipulation issue.
 * Doesn't have condition reset issues when re-stacking used weapons.
 * Has not being able to stack a used gun onto a new gun, only.
 * Doesn't have the weapon attachments issues
 *
 * Fixed config spelling errors
 * Fixed Visual bug on item splits ( where the players inventory UI wasn't updating properly )
 * Slight performance tweak
 * Added Updater methods.
 *
 * Updater code was derived from whitethunder's plugin.
 *
 * Fixed new NRE issues 6/8/2021
 *
 * Update 1.0.5 6/12/2021
 * Changed check config value to >= because fuck it
 *
 * updated 1.0.6 7/15/2021
 * Added feature to stop player abusing higher stack sizes when moving items from other storage containers to player inventory set from other plugins
 * Fixed Clone stack issues
 * Updated OnItemAddedToContainer code logic to fix StackItemStorage Bug Credits to Clearshot.
 *
 * Update 1.0.8
 * Fixed High hook time warnings significantly reduced
 * Fixed Condition loss comparison between float values
 * Added Ignore Admin check for F1 Spawns
 * Adjusted/fixed item moving issues from other plugins
 *
 * Update 1.0.9
 * Patched Skins auto merging into 1 stack bug
 *
 * Update 1.1.0
 * Added Liquid stacking support
 * Fixed On ItemAdded issue with stacks when using StackItemStorage
 *
 * Update 1.1.1
 * Added support for stacking Miner Hats with fuel + Candle Hats
 *
 * Update 1.1.2
 * Fixed Stacking issues with float values not matching due to unity float comparison bug
 *
 * Update 1.1.3
 * Fixed Vendor bug..
 *
 * Update 1.1.4
 * Added OnCardSwipe to fix stack loss when it hits broken stage.
 *
 * Update 1.1.5
 * Fixed High hook time hangs sometimes resulted in server crashes..
 *
 * TODO
 * Fix? Possible vending machine purchase issues using Vending Machine Plugin with economics/blood??
*/

namespace Oxide.Plugins
{
    [Info("Stack Modifier", "Khan", "1.1.5")]
    [Description("Modify item stack sizes")]
    public class StackModifier : CovalencePlugin
    {
        
        #region Fields

        private const string ByPass = "stackmodifier.bypass";
        
        private readonly HashSet<string> _exclude = new HashSet<string>
        {
            "water",
            "water.salt",
            "cardtable",
        };

        #endregion
        
        #region Config

        private PluginConfig _configData;
        
        protected override void LoadDefaultConfig() => _configData = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _configData = Config.ReadObject<PluginConfig>();

                if (_configData == null)
                {
                    PrintWarning($"Generating Config File for GUIShop");
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_configData))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                PrintWarning("Failed to load config file (is the config file corrupt?) (" + ex.Message + ")");
            }
        }
        protected override void SaveConfig() => Config.WriteObject(_configData, true);
        private void CheckConfig()
        {
            foreach (ItemDefinition item in ItemManager.itemList)
            {
                string categoryName = item.category.ToString();

                Dictionary<string, _Items> stackCategory;

                if (!_configData.StackCategories.TryGetValue(categoryName, out stackCategory))
                {
                    _configData.StackCategories[categoryName] = stackCategory = new Dictionary<string, _Items>();
                }

                if (_exclude.Contains(item.shortname)) continue;

                if (!stackCategory.ContainsKey(item.shortname))
                {
                    stackCategory.Add(item.shortname, new _Items
                    {
                        DisplayName = item.displayName.english,
                        Vanilla = item.stackable,
                        Modified = item.stackable,
                    });
                }
                
                if (_configData.StackCategories[categoryName][item.shortname].Modified >= _configData.StackCategories[categoryName][item.shortname].Vanilla )
                {
                    item.stackable = _configData.StackCategories[categoryName][item.shortname].Modified;
                }
            }
            
            foreach (var entity in BaseNetworkable.serverEntities.OfType<NPCVendingMachine>())
            {
                if (entity == null || entity.vendingOrders == null) continue;
                foreach (var order in entity.vendingOrders.orders)
                {
                    if (order == null) continue;
                    var cat = order.sellItem.category.ToString();
                    var sname = order.sellItem.shortname;
                    if (!_configData.StackCategories.ContainsKey(cat) || !_configData.StackCategories[cat].ContainsKey(sname)) continue;
                    order.sellItemAmount = _configData.StackCategories[cat][order.sellItem.shortname].Vanilla;
                }
            }

            SaveConfig();
        }
        internal class PluginConfig : SerializableConfiguration
        {
            [JsonProperty("Ignore Admins")] 
            public bool IsAdmin = true;
            
            [JsonProperty("Revert to Vanilla Stacks on unload (Recommended true if removing plugin)")]
            public bool Reset { get; set; }

            [JsonProperty("Disable Ammo/Fuel duplication fix (Recommended false)")]
            public bool DisableFix { get; set; }
            
            [JsonProperty("Enable VendingMachine Ammo Fix (Recommended)")]
            public bool VendingMachineAmmoFix { get; set; }

            [JsonProperty("Blocked Stackable Items", Order = 4)]
            public List<string> Blocked { get; set; }

            [JsonProperty("Stack Categories", Order = 5)]
            public Dictionary<string, Dictionary<string, _Items>> StackCategories = new Dictionary<string, Dictionary<string, _Items>>();
            
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    Reset = false,
                    DisableFix = false,
                    VendingMachineAmmoFix = true,
                    Blocked = new List<string>
                    {
                    "shortname"
                    },
                    
                };
            }
        }
        public class _Items
        {
            public string DisplayName;
            public int Vanilla;
            public int Modified;
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
                        return ((JValue) token).Value;
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

        #endregion

        #region Oxide

        private void Unload()
        {
            if (_configData.Reset)
            {
                ResetStacks();
            }
        }

        private void Init()
        {
            Unsubscribe(nameof(OnItemAddedToContainer));
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(ByPass, this);
            CheckConfig();
            Subscribe(nameof(OnItemAddedToContainer));
        }

        private object CanStackItem(Item item, Item targetItem)
        {

            if (item.GetOwnerPlayer().IsUnityNull() || targetItem.GetOwnerPlayer().IsUnityNull())
            {
                return null;
            }
            
            /*if (_configData.VendingMachineAmmoFix && item.GetRootContainer()?.entityOwner is VendingMachine)
            {
                return true;
            }*/

            if (_configData.Blocked.Contains(item.info.shortname) && item.GetOwnerPlayer() != null && !permission.UserHasPermission(item.GetOwnerPlayer().UserIDString, ByPass))
            {
                return false;
            }

            if (item.info.itemid == targetItem.info.itemid && !CanWaterItemsStack(item, targetItem))
            {
                return false;
            }

            if (
                item.info.stackable <= 1 ||
                targetItem.info.stackable <= 1 ||
                item.info.itemid != targetItem.info.itemid ||
                !item.IsValid() ||
                item.IsBlueprint() && item.blueprintTarget != targetItem.blueprintTarget ||
                Math.Ceiling(targetItem.condition) != item.maxCondition ||
                item.skin != targetItem.skin
            )
            {
                return false;
            }

            if (item.info.amountType == ItemDefinition.AmountType.Genetics || targetItem.info.amountType == ItemDefinition.AmountType.Genetics)
            {
                if ((item.instanceData?.dataInt ?? -1) != (targetItem.instanceData?.dataInt ?? -1))
                {
                    return false;
                }
            }
            
            if (targetItem.contents?.itemList.Count > 0)
            {
                foreach (Item containedItem in targetItem.contents.itemList)
                {
                    item.parent.playerOwner.GiveItem(ItemManager.CreateByItemID(containedItem.info.itemid, containedItem.amount));
                }
            }

            if (_configData.DisableFix)
            {
                return null;
            }
                
            BaseProjectile.Magazine itemMag = targetItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
            
            if (itemMag != null)
            {
                if (itemMag.contents > 0)
                {
                    item.GetOwnerPlayer().GiveItem(ItemManager.CreateByItemID(itemMag.ammoType.itemid,itemMag.contents));

                    itemMag.contents = 0;
                }
            }
            
            if (targetItem.GetHeldEntity() is FlameThrower)
            {
                FlameThrower flameThrower = targetItem.GetHeldEntity().GetComponent<FlameThrower>();

                if (flameThrower.ammo > 0)
                {
                    item.GetOwnerPlayer().GiveItem(ItemManager.CreateByItemID(flameThrower.fuelType.itemid,flameThrower.ammo));

                    flameThrower.ammo = 0;
                }
            }
            
            if (targetItem.GetHeldEntity() is Chainsaw)
            {
                Chainsaw chainsaw = targetItem.GetHeldEntity().GetComponent<Chainsaw>();

                if (chainsaw.ammo > 0)
                {
                    item.GetOwnerPlayer().GiveItem(ItemManager.CreateByItemID(chainsaw.fuelType.itemid,chainsaw.ammo));

                    chainsaw.ammo = 0;
                }
            }
            
            if (targetItem.info.shortname == "hat.miner" || targetItem.info.shortname == "hat.candle")
            {
                if (targetItem.contents != null && !targetItem.contents.IsEmpty())
                {
                    var content = targetItem.contents.itemList.First();
                    Item newItem = ItemManager.CreateByItemID(content.info.itemid, content.amount);
                    newItem.amount = content.amount;
                    newItem.amount = 0;
                    item.GetOwnerPlayer().GiveItem(newItem);
                }
            }

            return true;
        }
        
        private object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.skinID == 0 && targetItem.skinID == 0)
            {
                return null;
            }
            
            if (item.skinID != targetItem.skinID)
            {
                return false;
            }

            if (item.item.contents != null || targetItem.item.contents != null)
            {
                return false;
            }

            if (Math.Abs(item.item._condition - targetItem.item._condition) > 0f)
            {
                return false;
            }

            if (Math.Abs(item.item._maxCondition - targetItem.item._maxCondition) > 0f)
            {
                return false;
            }

            return null;
        }

        private Item OnItemSplit(Item item, int amount)
        {
            
            if (item.GetHeldEntity()?.GetComponentInChildren<BaseLiquidVessel>() != null)
            {
                Item LiquidContainer = ItemManager.CreateByName(item.info.shortname);
                LiquidContainer.amount = amount;
    
                item.amount -= amount;
                item.MarkDirty();

                Item water = item.contents.FindItemByItemID(-1779180711);

                if (water != null) LiquidContainer.contents.AddItem(ItemManager.FindItemDefinition(-1779180711), water.amount);

                return LiquidContainer;
            }

            if (item.skin != 0)
            {
                Item x = ItemManager.CreateByItemID(item.info.itemid);
                BaseProjectile.Magazine itemMag = x.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
                if (itemMag != null && itemMag.contents > 0)
                {
                    itemMag.contents = 0;
                }
                item.amount -= amount;
                x.name = item.name;
                x.skin = item.skin;
                x.amount = amount;
                x.MarkDirty();
                
                return x;
            }

            Item newItem = ItemManager.CreateByItemID(item.info.itemid);

            BaseProjectile.Magazine newItemMag = newItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;

            if (newItem.contents?.itemList.Count == 0 && (_configData.DisableFix || newItem.contents?.itemList.Count == 0 && newItemMag?.contents == 0))
            {
                return null;
            }
            
            item.amount -= amount;
            newItem.name = item.name;
            newItem.amount = amount;

            item.MarkDirty();

            if (item.IsBlueprint())
            {
                newItem.blueprintTarget = item.blueprintTarget;
            }
            
            if (item.info.amountType == ItemDefinition.AmountType.Genetics && item.instanceData != null && item.instanceData.dataInt != 0)
            {
                newItem.instanceData = new ProtoBuf.Item.InstanceData()
                {
                    dataInt = item.instanceData.dataInt,
                    ShouldPool = false
                };
            }

            if (newItem.contents?.itemList.Count > 0)
            {
                item.contents.Clear();
            }
            
            newItem.MarkDirty();

            if (_configData.VendingMachineAmmoFix && item.GetRootContainer()?.entityOwner is VendingMachine)
            {
                return newItem;
            }

            if (_configData.DisableFix)
            {
                return newItem;
            }

            if (newItem.GetHeldEntity() is FlameThrower)
            {
                newItem.GetHeldEntity().GetComponent<FlameThrower>().ammo = 0;
            }
            
            if (newItem.GetHeldEntity() is Chainsaw)
            {
                newItem.GetHeldEntity().GetComponent<Chainsaw>().ammo = 0;
            }
            
            BaseProjectile.Magazine itemMagDefault = newItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
            if (itemMagDefault != null && itemMagDefault.contents > 0)
            {
                itemMagDefault.contents = 0;
            }

            return newItem;
           
        }
        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            BasePlayer player = container.GetOwnerPlayer();

            if (player == null || _configData.IsAdmin && player.IsAdmin) return;
            
            if ((player.inventory.containerMain.uid == container.uid || player.inventory.containerBelt.uid == container.uid) && item.amount > item.MaxStackable())
            {
                int division = item.amount / item.MaxStackable();

                for (int i = 0; i < division; i++)
                {
                    Item y = item.SplitItem(item.MaxStackable());
                    if (y != null && !y.MoveToContainer(player.inventory.containerMain, -1, false) && (item.parent == null || !y.MoveToContainer(item.parent)))
                    {
                        y.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                    }
                }
            }

            if (player.inventory.containerWear.uid != container.uid) return;
            if (item.amount <= 1) return;
            int amount = item.amount -= 1;
            player.inventory.containerWear.Take(null, item.info.itemid, amount - 1);
            Item x = ItemManager.CreateByItemID(item.info.itemid, item.amount, item.skin);
            x.name = item.name;
            x.skin = item.skin;
            x.amount = amount;
            x._condition = item._condition;
            x._maxCondition = item._maxCondition;
            x.MarkDirty();
            x.MoveToContainer(player.inventory.containerMain);
            }
        
        private object OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            var item = card.GetItem();
            if (item.amount <= 1) return null;

            int division = item.amount / 1;
            
            for (int i = 0; i < division; i++)
            {
                Item x = item.SplitItem(1);
                if (x != null && !x.MoveToContainer(player.inventory.containerMain, -1, false) && (item.parent == null || !x.MoveToContainer(item.parent)))
                {
                    x.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                }
            }

            return null;
        }

        #endregion
        
        #region Helpers
        private bool CanWaterItemsStack(Item item, Item targetItem)
        {
            if (item.GetHeldEntity()?.GetComponentInChildren<BaseLiquidVessel>() == null)
            {
                return true;
            }
            
            if (targetItem.contents.IsEmpty() || item.contents.IsEmpty()) 
                return (!targetItem.contents.IsEmpty() || !item.contents.IsFull()) && (!item.contents.IsEmpty() || !targetItem.contents.IsFull());

            var first = item.contents.itemList.First();
            var second = targetItem.contents.itemList.First();
            if (first.info.itemid != second.info.itemid || first.amount != second.amount) return false;

            return (!targetItem.contents.IsEmpty() || !item.contents.IsFull()) && (!item.contents.IsEmpty() || !targetItem.contents.IsFull());
        }
        private void ResetStacks()
        {
            foreach (ItemDefinition itemDefinition in ItemManager.GetItemDefinitions())
            {
                if (!_configData.StackCategories.ContainsKey(itemDefinition.category.ToString())) continue;
                Dictionary<string, _Items> stackCategory = _configData.StackCategories[itemDefinition.category.ToString()];
                if (!stackCategory.ContainsKey(itemDefinition.shortname)) continue;
                itemDefinition.stackable = stackCategory[itemDefinition.shortname].Vanilla;
            }
        }

        #endregion
    }
}