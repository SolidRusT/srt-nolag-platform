using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
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
*/

namespace Oxide.Plugins
{
    [Info("Stack Modifier", "Khan", "1.0.5")]
    [Description("Modify item stack sizes")]
    public class StackModifier : CovalencePlugin
    {
        
        #region Fields

        private IPlayer _player;
        
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
                    SaveConfig();
                }

            }

            SaveConfig();
        }
        internal class PluginConfig : SerializableConfiguration
        {
            [JsonProperty(PropertyName = "Revert to Vanilla Stacks on unload (Recommended true if removing plugin)")]
            public bool Reset { get; set; }

            [JsonProperty(PropertyName = "Disable Ammo/Fuel duplication fix (Recommended false)")]
            public bool DisableFix { get; set; }
            
            [JsonProperty(PropertyName = "Enable VendingMachine Ammo Fix (Recommended)")]
            public bool VendingMachineAmmoFix { get; set; }

            [JsonProperty(PropertyName = "Blocked Stackable Items", Order = 4)]
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

        private void OnServerInitialized()
        {
            permission.RegisterPermission(ByPass, this);
            CheckConfig();
        }
        
        private object CanStackItem(Item item, Item targetItem)
        {

            if (item.GetOwnerPlayer().IsUnityNull() || targetItem.GetOwnerPlayer().IsUnityNull())
            {
                return null;
            }

            if (_configData.Blocked.Contains(item.info.shortname) && !permission.UserHasPermission(_player.Id, ByPass))
            {
                return false;
            }

            if (
                item == targetItem ||
                item.info.stackable <= 1 ||
                targetItem.info.stackable <= 1 ||
                item.info.itemid != targetItem.info.itemid ||
                !item.IsValid() ||
                item.IsBlueprint() && item.blueprintTarget != targetItem.blueprintTarget || 
                targetItem.condition != item.maxCondition ||
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

            return true;
        }

        private Item OnItemSplit(Item item, int amount)
        {
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
            if (player == null) return;
            if (player.inventory.containerWear.uid != container.uid) return;
            if (item.amount <= 1) return;
            int amount = item.amount -=1;
            player.inventory.containerWear.Take(null, item.info.itemid, amount -1);
            Item x = ItemManager.CreateByItemID(item.info.itemid, item.amount, item.skin);
            x.name = item.name;
            x.skin = item.skin;
            x.amount = amount;
            x._condition = item._condition;
            x._maxCondition = item._maxCondition;
            x.MarkDirty();
            x.MoveToContainer(player.inventory.containerMain);
        }

        #endregion

        #region Helpers
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