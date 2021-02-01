using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Barrel Event", "Orange", "1.1.0")]
    [Description("Special actions on barrel destroying")]
    public class BarrelEvent : RustPlugin
    {
        #region Oxide Hooks

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info?.InitiatorPlayer != null)
            {
                CheckDeath(entity);
            }
        }

        #endregion

        #region Core

        private void CheckDeath(BaseEntity entity)
        {
            var name = entity.ShortPrefabName;
            
            if (!name.Contains("barrel") || name.Contains("oil"))
            {
                return;  
            }

            foreach (var value in config.events)
            {
                var random = Core.Random.Range(0, 101);
                if (random > value.chance) {continue;}

                switch (value.type)
                {
                        case "Spawn":
                            Spawn(value.range, value.param, entity.transform.position);
                            break;
                        
                        
                        case "Damage":
                            Damage(value.range, Convert.ToInt32(value.param), entity.transform.position);
                            break;
                        
                        default:
                            return;
                }
                
                if (config.single) {break;}
            }
        }
        
        private void Spawn(float range, string prefab, Vector3 position)
        {
            var entity = GameManager.server.CreateEntity(prefab, position + new Vector3(Core.Random.Range(-range, range), 0, Core.Random.Range(-range, range)));
            if (entity == null) {return;}

            var plane = entity.GetComponent<CargoPlane>();
            if (plane != null)
            {
                plane.InitDropPosition(position);
                plane.secondsToTake = config.cargoSpeed;
                plane.transform.position = new Vector3(plane.transform.position.x, config.cargoHeight, plane.transform.position.z);
                plane.TransformChanged();
            }
            
            entity.Spawn();
        }

        private void Damage(float radius, int damage, Vector3 position)
        {
            var list = new List<BaseCombatEntity>();
            Vis.Entities(position, radius, list);
            
            foreach (var entity in list)
            {
                entity.Hurt(damage);
            }
        }

        #endregion
        
        #region Configuration
        
        private static ConfigData config;
        
        private class ConfigData
        {
            [JsonProperty(PropertyName = "1. Run only 1 event at action")]
            public bool single;

            [JsonProperty(PropertyName = "2.Cargo plane speed")]
            public float cargoSpeed;

            [JsonProperty(PropertyName = "3.Cargo plane height")]
            public float cargoHeight;
            
            [JsonProperty(PropertyName = "Event list:")]
            public List<Event> events = new List<Event>();

            public class Event
            {
                [JsonProperty(PropertyName = "1. Chance")]
                public int chance;

                [JsonProperty(PropertyName = "2. Type")]
                public string type;
                
                [JsonProperty(PropertyName = "3. Parameter")]
                public string param;

                [JsonProperty(PropertyName = "4. Range")]
                public float range;
            }
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData 
            {
                single = false,
                cargoSpeed = 10,
                cargoHeight = 700f,
                events = new List<ConfigData.Event>
                {
                    new ConfigData.Event
                    {
                        type = "Spawn",
                        param = "assets/rust.ai/agents/bear/bear.prefab",
                        chance = 10,
                        range = 0f
                    },
                    new ConfigData.Event
                    {
                        type = "Spawn",
                        param = "assets/prefabs/npc/scientist/scientist.prefab",
                        chance = 10,
                        range = 0f
                    },
                    new ConfigData.Event
                    {
                        type = "Damage",
                        param = "30",
                        chance = 10,
                        range = 5f
                    }
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
    }
}