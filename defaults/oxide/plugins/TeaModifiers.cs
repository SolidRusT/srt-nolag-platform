using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Oxide.Plugins
{
    [Info("Tea Modifiers", "MJSU", "2.0.0")]
    [Description("Allows the modification of tea buffs on items")]
    internal class TeaModifiers : RustPlugin
    {
        #region Class Fields
        private PluginConfig _pluginConfig; //Plugin Config
        
        private const string BasePermission = "teamodifiers.";
        private const string UsePermission = BasePermission + "use";
        #endregion

        #region Setup & Loading
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            config.Modifiers = config.Modifiers ?? new Hash<string, List<ModifierData>>();
            config.GlobalDurationMultiplier = config.GlobalDurationMultiplier ?? new Hash<string, float>
            {
                [UsePermission] = 1f
            };
            
            config.GlobalAmountMultiplier = config.GlobalAmountMultiplier ?? new Hash<string, float>
            {
                [UsePermission] = 1f
            };
            
            config.GlobalModifierMultiplier = config.GlobalModifierMultiplier ?? new Hash<Modifier.ModifierType, Hash<string, float>>();
            
            foreach (Modifier.ModifierType type in Enum.GetValues(typeof(Modifier.ModifierType)).Cast<Modifier.ModifierType>())
            {
                if (!config.GlobalModifierMultiplier.ContainsKey(type))
                {
                    config.GlobalModifierMultiplier[type] = new Hash<string, float>
                    {
                        [UsePermission] = 1f
                    };
                }
            }

            return config;
        }
        
        private void OnServerInitialized()
        {
            permission.RegisterPermission(UsePermission, this);
            
            bool changed = false;
            foreach (ItemDefinition def in ItemManager.itemList)
            {
                ItemModConsumable consume = def.GetComponent<ItemModConsumable>();
                if (consume == null || consume.modifiers == null || consume.modifiers.Count == 0)
                {
                    continue;
                }

                //Load saved modifiers
                List<ModifierData> modifiers = _pluginConfig.Modifiers[def.shortname];
                
                //Currently no modifiers saved. Save them now.
                if (modifiers == null)
                {
                    _pluginConfig.Modifiers[def.shortname] = consume.modifiers.Select(m => new ModifierData
                    {
                        Duration = new Hash<string, float> {[UsePermission] = m.duration},
                        Type = m.type,
                        Amount = new Hash<string, float> {[UsePermission] = m.value},
                    }).ToList();
                    changed = true;
                }
            }

            //If changes occured update config
            if (changed)
            {
                Config.WriteObject(_pluginConfig);
            }

            List<string> perms = Pool.GetList<string>();
            foreach (List<ModifierData> item in _pluginConfig.Modifiers.Values)
            {
                foreach (ModifierData modifier in item)
                {
                    perms.AddRange(modifier.Amount.Keys);
                    perms.AddRange(modifier.Duration.Keys);
                }
            }
            
            foreach (Hash<string, float> modifier in _pluginConfig.GlobalModifierMultiplier.Values)
            {
                perms.AddRange(modifier.Keys);
            }
            
            perms.AddRange(_pluginConfig.GlobalAmountMultiplier.Keys);
            perms.AddRange(_pluginConfig.GlobalDurationMultiplier.Keys);

            foreach (string perm in perms.Distinct().Where(p => !p.Equals(UsePermission, StringComparison.OrdinalIgnoreCase)))
            {
                permission.RegisterPermission(perm, this);  
            }
            
            Pool.FreeList(ref perms);
        }
        #endregion

        #region Hooks
        private object OnPlayerAddModifiers(BasePlayer player, Item item, ItemModConsumable consumable)
        {
            if (!HasPermission(player, UsePermission))
            {
                return null;
            }
            
            List<ModifierData> modifiers = _pluginConfig.Modifiers[item.info.shortname];
            if (modifiers == null)
            {
                return null;
            }

            float globalDuration = GetPermissionValue(player, _pluginConfig.GlobalDurationMultiplier, 1f);
            float globalAmount = GetPermissionValue(player, _pluginConfig.GlobalAmountMultiplier, 1f);
            
            List<ModifierDefintion> mods = Pool.GetList<ModifierDefintion>();

            for (int index = 0; index < modifiers.Count; index++)
            {
                ModifierData modifier = modifiers[index];
                mods.Add(new ModifierDefintion
                {
                    source = Modifier.ModifierSource.Tea,
                    type = modifier.Type,
                    duration = globalDuration * GetPermissionValue(player, modifier.Duration, 1f),
                    value = globalAmount * GetPermissionValue(player, modifier.Amount, 1f) * GetPermissionValue(player, _pluginConfig.GlobalModifierMultiplier[modifier.Type], 1f)
                });
            }

            player.modifiers.Add(mods);
            
            Pool.FreeList(ref mods);
            return true;
        }
        
        #endregion

        #region Helper Methods
        public float GetPermissionValue(BasePlayer player, Hash<string, float> permissions, float defaultValue)
        {
            foreach (KeyValuePair<string,float> perm in permissions.OrderByDescending(p => p.Value))
            {
                if (HasPermission(player, perm.Key))
                {
                    return perm.Value;
                }
            }

            return defaultValue;
        }

        public bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);
        #endregion

        #region Classes
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Global Duration Multiplier")]
            public Hash<string, float> GlobalDurationMultiplier { get; set; }
            
            [JsonProperty(PropertyName = "Global Amount Multiplier")]
            public Hash<string, float> GlobalAmountMultiplier { get; set; }
            
            [JsonProperty(PropertyName = "Global Modifier Type Amount Multiplier")]
            public Hash<Modifier.ModifierType, Hash<string, float>> GlobalModifierMultiplier { get; set; }
            
            [JsonProperty(PropertyName = "Item List Modifiers")]
            public Hash<string, List<ModifierData>> Modifiers { get; set; }
        }

        private class ModifierData
        {
            [JsonProperty(PropertyName = "Modifier Duration (Seconds)")]
            public Hash<string, float> Duration { get; set; }

            [JsonProperty(PropertyName = "Modifier Amount")]
            public Hash<string, float> Amount { get; set; }

            [JsonProperty(PropertyName = "Modifer Type")]
            [JsonConverter(typeof(StringEnumConverter))]
            public Modifier.ModifierType Type { get; set; }
        }
        #endregion
    }
}
