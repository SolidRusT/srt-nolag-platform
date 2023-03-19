using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Tea Modifiers", "MJSU", "2.1.2")]
    [Description("Allows the modification of tea buffs on items")]
    internal class TeaModifiers : RustPlugin
    {
        #region Class Fields
        private PluginConfig _pluginConfig; //Plugin Config
        
        private const string BasePermission = "teamodifiers.";
        private const string UsePermission = BasePermission + "use";
        
        private readonly Hash<ulong, float> _playerGlobalDurationCache = new Hash<ulong, float>();
        private readonly Hash<Modifier.ModifierType, Hash<ulong, float>> _globalTypeValueCache = new Hash<Modifier.ModifierType, Hash<ulong, float>>();
        private readonly Hash<ulong, float> _playerGlobalValueCache = new Hash<ulong, float>();
        private readonly Hash<string, Hash<ulong, float>> _playerDurationCache = new Hash<string, Hash<ulong, float>>();
        private readonly Hash<string, Hash<ulong, float>> _playerValueCache = new Hash<string, Hash<ulong, float>>();
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
            bool changed = false;
            foreach (ItemDefinition def in ItemManager.itemList)
            {
                ItemModConsumable consume = def.GetComponent<ItemModConsumable>();
                if (!consume || consume.modifiers == null|| consume.modifiers.Count == 0)
                {
                    continue;
                }

                //Load saved modifiers
                List<ModifierData> modifiers = _pluginConfig.Modifiers[def.shortname];
                
                //Currently no modifiers saved. Save them now.
                if (modifiers == null)
                {
                    modifiers = consume.modifiers.Select(m => new ModifierData
                    {
                        Duration = new Hash<string, float> {[UsePermission] = m.duration},
                        Type = m.type,
                        Amount = new Hash<string, float> {[UsePermission] = m.value},
                    }).ToList();
                    _pluginConfig.Modifiers[def.shortname] = modifiers;
                    changed = true;
                }

                foreach (ModifierData data in modifiers)
                {
                    ModifierDefintion existing = consume.modifiers.FirstOrDefault(m => m.type == data.Type);
                    if (existing != null)
                    {
                        data.DefaultAmount = existing.value;
                        data.DefaultDuration = existing.duration;
                    }
                }
            }

            //If changes occured update config
            if (changed)
            {
                Config.WriteObject(_pluginConfig);
            }

            RegisterPermissions();
        }

        private void RegisterPermissions()
        {
            HashSet<string> perms = new HashSet<string> { UsePermission };
            foreach (List<ModifierData> item in _pluginConfig.Modifiers.Values)
            {
                foreach (ModifierData modifier in item)
                {
                    foreach (string key in modifier.Amount.Keys)
                    {
                        perms.Add(key);
                    }

                    foreach (string key in modifier.Duration.Keys)
                    {
                        perms.Add(key);
                    }
                }
            }

            foreach (Hash<string, float> modifier in _pluginConfig.GlobalModifierMultiplier.Values)
            {
                foreach (string key in modifier.Keys)
                {
                    perms.Add(key);
                }
            }

            foreach (string key in _pluginConfig.GlobalAmountMultiplier.Keys)
            {
                perms.Add(key);
            }

            foreach (string key in _pluginConfig.GlobalDurationMultiplier.Keys)
            {
                perms.Add(key);
            }

            foreach (string perm in perms)
            {
                permission.RegisterPermission(perm, this);
            }
        }
        #endregion
        
        #region Permission Hooks
        private void OnUserPermissionGranted(string playerId, string permName)
        {
            ulong id = ulong.Parse(playerId);
            _playerGlobalDurationCache.Remove(id);
            _globalTypeValueCache.Clear();
            _playerGlobalValueCache.Remove(id);
            _playerDurationCache.Clear();
            _playerValueCache.Clear();
        }
        
        private void OnUserPermissionRevoked(string playerId, string permName)
        {
            ulong id = ulong.Parse(playerId);
            _playerGlobalDurationCache.Remove(id);
            _globalTypeValueCache.Clear();
            _playerGlobalValueCache.Remove(id);
            _playerDurationCache.Clear();
            _playerValueCache.Clear();
        }
        
        private void OnUserGroupAdded(string playerId, string groupName)
        {
            ulong id = ulong.Parse(playerId);
            _playerGlobalDurationCache.Remove(id);
            _globalTypeValueCache.Clear();
            _playerGlobalValueCache.Remove(id);
            _playerDurationCache.Clear();
            _playerValueCache.Clear();
        }
        
        private void OnUserGroupRemoved(string playerId, string groupName)
        {
            ulong id = ulong.Parse(playerId);
            _playerGlobalDurationCache.Remove(id);
            _globalTypeValueCache.Clear();
            _playerGlobalValueCache.Remove(id);
            _playerDurationCache.Clear();
            _playerValueCache.Clear();
        }

        private void OnGroupPermissionGranted(string groupName, string permName)
        {
            _playerGlobalValueCache.Clear();
            _globalTypeValueCache.Clear();
            _playerGlobalDurationCache.Clear();
            _playerDurationCache.Clear();
            _playerValueCache.Clear();
        }
        
        private void OnGroupPermissionRevoked(string groupName, string permName)
        {
            _playerGlobalDurationCache.Clear();
            _globalTypeValueCache.Clear();
            _playerGlobalValueCache.Clear();
            _playerDurationCache.Clear();
            _playerValueCache.Clear();
        }
        #endregion

        #region Hooks
        private object OnPlayerAddModifiers(BasePlayer player, Item item, ItemModConsumable consumable)
        {
            if (!HasPermission(player, UsePermission))
            {
                return null;
            }

            string shortname = item.info.shortname;
            List<ModifierData> modifiers = _pluginConfig.Modifiers[item.info.shortname];
            if (modifiers == null)
            {
                return null;
            }

            if (Interface.Oxide.CallHook("CanApplyTeaModifier", player, item, consumable) != null)
            {
                return null;
            }

            List<ModifierDefintion> mods = Pool.GetList<ModifierDefintion>();
            for (int index = 0; index < modifiers.Count; index++)
            {
                ModifierData modifier = modifiers[index];
                if (Interface.Oxide.CallHook("CanApplyTeaModifierType", player, item, modifier.Type) != null)
                {
                    continue;
                }
                
                mods.Add(new ModifierDefintion
                {
                    source = Modifier.ModifierSource.Tea,
                    type = modifier.Type,
                    duration = GetPlayerDuration(player, shortname, modifier),
                    value = GetPlayerValue(player, shortname, modifier)
                });
            }

            player.modifiers.Add(mods);
            
            Pool.FreeList(ref mods);
            return true;
        }
        
        #endregion

        #region API
        private bool HasModifiers(string shortName)
        {
            return _pluginConfig.Modifiers[shortName] != null;
        }
        
        private float GetTeaDuration(BasePlayer player, string shortName, Modifier.ModifierType type)
        {
            ModifierData data = GetDataForItemType(shortName, type);
            if (data == null)
            {
                return 0;
            }
            
            return GetPlayerDuration(player, shortName, data);
        }

        private float GetTeaValue(BasePlayer player, string shortName, Modifier.ModifierType type)
        {
            ModifierData data = GetDataForItemType(shortName, type);
            if (data == null)
            {
                return 0;
            }

            return GetPlayerValue(player, shortName, data);
        }
        #endregion

        #region Helper Methods
        public float GetPlayerDuration(BasePlayer player, string shortName, ModifierData modifier)
        {
            float globalDuration = GetPermissionValue(player, _pluginConfig.GlobalDurationMultiplier, 1f, _playerGlobalDurationCache);
            
            Hash<ulong, float> durationCache = _playerDurationCache[shortName];
            if (durationCache == null)
            {
                durationCache = new Hash<ulong, float>();
                _playerDurationCache[shortName] = durationCache;
            }
            
            float duration = GetPermissionValue(player, modifier.Duration, modifier.DefaultDuration, durationCache);
            return globalDuration * duration;
        }
        
        public float GetPlayerValue(BasePlayer player, string shortName, ModifierData modifier)
        {
            float globalValue = GetPermissionValue(player, _pluginConfig.GlobalAmountMultiplier, 1f, _playerGlobalValueCache);
            
            Hash<ulong, float> typeCache = _globalTypeValueCache[modifier.Type];
            if (typeCache == null)
            {
                typeCache = new Hash<ulong, float>();
                _globalTypeValueCache[modifier.Type] = typeCache;
            }
            
            float globalTypeValue = GetPermissionValue(player, _pluginConfig.GlobalModifierMultiplier[modifier.Type], 1f, typeCache);
            
            Hash<ulong, float> valueCache = _playerValueCache[shortName];
            if (valueCache == null)
            {
                valueCache = new Hash<ulong, float>();
                _playerValueCache[shortName] = valueCache;
            }

            float playerValue = GetPermissionValue(player, modifier.Amount, modifier.DefaultAmount, valueCache);
            return globalValue * globalTypeValue * playerValue;
        }
        
        public ModifierData GetDataForItemType(string shortName, Modifier.ModifierType type)
        {
            List<ModifierData> modifiers = _pluginConfig.Modifiers[shortName];
            if (modifiers == null || modifiers.Count == 0)
            {
                return null;
            }
            
            foreach (ModifierData modifier in modifiers)
            {
                if (modifier.Type == type)
                {
                    return modifier;
                }
            }

            return null;
        }
        
        public float GetPermissionValue(BasePlayer player, Hash<string, float> permissions, float defaultValue, Hash<ulong, float> cache)
        {
            if (cache != null && cache.ContainsKey(player.userID))
            {
                return cache[player.userID];
            }
            
            foreach (KeyValuePair<string,float> perm in permissions.OrderByDescending(p => p.Value))
            {
                if (HasPermission(player, perm.Key))
                {
                    if (cache != null)
                    {
                        cache[player.userID] = perm.Value;
                    }
                    return perm.Value;
                }
            }
            
            if (cache != null)
            {
                cache[player.userID] = defaultValue;
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

        public class ModifierData
        {
            [JsonProperty(PropertyName = "Modifier Duration (Seconds)")]
            public Hash<string, float> Duration { get; set; }

            [JsonProperty(PropertyName = "Modifier Amount")]
            public Hash<string, float> Amount { get; set; }

            [JsonProperty(PropertyName = "Modifer Type")]
            [JsonConverter(typeof(StringEnumConverter))]
            public Modifier.ModifierType Type { get; set; }

            [JsonIgnore]
            public float DefaultDuration { get; set; } = 1f;
            
            [JsonIgnore]
            public float DefaultAmount { get; set; } = 1f;
        }
        #endregion
    }
}
