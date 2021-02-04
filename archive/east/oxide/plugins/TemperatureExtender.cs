using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Temperature Extender", "MJSU", "1.0.0")]
    [Description("Extends the range of temperature and comfort")]
    public class TemperatureExtender : RustPlugin
    {
        #region Class Fields
        private PluginConfig _pluginConfig; //Plugin Config
        private readonly Hash<string, EntitySettings> _defaultSettings = new Hash<string, EntitySettings>();

        private bool _init;
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
            config.EntitySettings = config.EntitySettings ?? new Hash<string, EntitySettings>();
            return config;
        }
        
        private void OnServerInitialized()
        {
            foreach (string prefab in FileSystemBackend.cache.Keys)
            {
                GameObject go = GameManager.server.FindPrefab(prefab);
                BaseCombatEntity entity = go?.GetComponent<BaseCombatEntity>();
                if (entity == null)
                {
                    continue;
                }
                
                AddSetting(_pluginConfig.EntitySettings, entity, false);
                AddSetting(_defaultSettings, entity, true);
            }
            
            Config.WriteObject(_pluginConfig);
            
            UpdateAllEntities(_pluginConfig.EntitySettings);

            _init = true;
        }

        private void AddSetting(Hash<string, EntitySettings> settings, BaseCombatEntity entity, bool enabled)
        {
            TriggerTemperature temp = entity.GetComponentInChildren<TriggerTemperature>();
            TriggerComfort comfort = entity.GetComponentInChildren<TriggerComfort>();

            if (temp == null && comfort == null)
            {
                return;
            }
            
            if (settings.ContainsKey(entity.ShortPrefabName))
            {
                return;
            }

            EntitySettings setting = new EntitySettings
            {
                Enabled = enabled
            };
            
            settings[entity.ShortPrefabName] = setting;

            if (temp != null)
            {
                setting.Temperature = GetTemperatureSettings(temp);
            }

            if (comfort != null)
            {
                setting.Comfort = GetComfortSettings(comfort);
            }
        }

        private void Unload()
        {
            UpdateAllEntities(_defaultSettings);
        }
        #endregion

        #region uMod Hooks

        private void OnEntitySpawned(BaseCombatEntity entity)
        {
            if (!_init)
            {
                return;
            }
            
            UpdateEntity(entity, _pluginConfig.EntitySettings);
        }

        #endregion

        #region Entity Methods

        private void UpdateAllEntities(Hash<string, EntitySettings> settings)
        {
            foreach (BaseCombatEntity entity in BaseNetworkable.serverEntities.OfType<BaseCombatEntity>())
            {
                UpdateEntity(entity, settings);
            }
        }

        private void UpdateEntity(BaseCombatEntity entity, Hash<string, EntitySettings> settings)
        {
            EntitySettings setting = settings[entity.ShortPrefabName];
            if (setting == null || !setting.Enabled)
            {
                return;
            }

            TriggerTemperature temp = entity.GetComponentInChildren<TriggerTemperature>(true);
            if (temp != null && setting.Temperature != null)
            {
                SphereCollider sphere = temp.GetComponent<SphereCollider>();
                temp.Temperature = setting.Temperature.Temperature;
                temp.minSize = setting.Temperature.MinRange;
                sphere.radius = setting.Temperature.TriggerRadius;
                temp.triggerSize = sphere.radius * entity.transform.localScale.y * setting.Temperature.FalloffRate;
            }
            
            TriggerComfort comfort = entity.GetComponentInChildren<TriggerComfort>(true);
            if (comfort != null)
            {
                SphereCollider sphere = comfort.GetComponent<SphereCollider>();
                comfort.baseComfort = setting.Comfort.BaseComfort;
                comfort.minComfortRange = setting.Comfort.MinRange;
                sphere.radius = setting.Comfort.TriggerRadius;
                comfort.triggerSize = sphere.radius * entity.transform.localScale.y * setting.Comfort.FalloffRate;
            }
        }

        private TemperatureSettings GetTemperatureSettings(TriggerTemperature temp)
        {
            SphereCollider sphere = temp.GetComponent<SphereCollider>();
            return new TemperatureSettings
            {
                Temperature = temp.Temperature,
                MinRange = temp.minSize,
                TriggerRadius = sphere.radius,
                FalloffRate = temp.triggerSize / sphere.radius
            };
        }

        private ComfortSettings GetComfortSettings(TriggerComfort comfort)
        {
            SphereCollider sphere = comfort.GetComponent<SphereCollider>();
            return new ComfortSettings
            {
                BaseComfort = comfort.baseComfort,
                MinRange = comfort.minComfortRange,
                TriggerRadius = sphere.radius,
                FalloffRate = comfort.triggerSize / sphere.radius
            };
        }
        #endregion

        #region Classes
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Entity Settings")]
            public Hash<string, EntitySettings> EntitySettings { get; set; }
        }

        private class EntitySettings
        {
            public TemperatureSettings Temperature { get; set; }
            public ComfortSettings Comfort { get; set; }
            public bool Enabled { get; set; }
        }
        
        private class TemperatureSettings
        {
            [JsonProperty(PropertyName = "Max temperature")]
            public float Temperature { get; set; }
            
            [JsonProperty(PropertyName = "Temperature range (Meters)")]
            public float TriggerRadius { get; set; }
            
            [JsonProperty(PropertyName = "Temperature falloff rate multiplier ( > 1 falls off slower < 1 falls off quicker)")]
            public float FalloffRate { get; set; }
            
            [JsonProperty(PropertyName = "Minimum range from entity before temperature takes effect")]
            public float MinRange { get; set; }
        }
        
        private class ComfortSettings
        {
            [JsonProperty(PropertyName = "Max comfort")]
            public float BaseComfort { get; set; }
            
            [JsonProperty(PropertyName = "Comfort range (Meters)")]
            public float TriggerRadius { get; set; }
            
            [JsonProperty(PropertyName = "Comfort falloff rate multiplier ( > 1 falls off slower < 1 falls off quicker)")]
            public float FalloffRate { get; set; }
            
            [JsonProperty(PropertyName = "Minimum range from entity before temperature takes effect")]
            public float MinRange { get; set; }
        }
        #endregion
    }
}
