using System;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Raidblock Building Health", "Orange", "1.1.4")]
    [Description("Changing health of created/upgraded building blocks in raidblock")]
    public class RaidblockBuildingHealth : RustPlugin
    {
        #region Oxide Hooks

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan.GetOwnerPlayer();
            if (player == null)
            {
                return;
            }

            var entity = go?.ToBaseEntity();
            if (InRaidBlock(player))
            {
                CheckEntity(entity);
            }
        }
        
        private void OnStructureUpgrade(BaseEntity entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (InRaidBlock(player))
            {
                CheckEntity(entity);
            }
        }

        private void CheckEntity(BaseEntity entity)
        {
            NextTick(() =>
            {
                if (entity.IsValid() == false)
                {
                    return;
                }

                if (config.buildingBlocks && (entity is SimpleBuildingBlock || entity is BuildingBlock))
                {
                    var obj = entity.GetOrAddComponent<HealComponent>();
                    obj.OnChangedState();
                    return;
                }

                if (config.doors && entity is Door)
                {
                    var obj = entity.GetOrAddComponent<HealComponent>();
                    obj.OnChangedState();
                    return;
                }
            });
        }

        #endregion

        #region Configuration | 24.05.2020

        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Health on spawn in percents (0-100)")]
            public int spawnHealthPercent = 0;

            [JsonProperty(PropertyName = "Health on spawn in amount (0 to disable)")]
            public int spawnHealthAmount = 10;

            [JsonProperty(PropertyName = "Structure should be not attacked for X seconds to start healing")]
            public float secondsAfterAttacked = 60;

            [JsonProperty(PropertyName = "Interrupt healing at all after getting any damage")]
            public bool interruptAfterAnyDamage = false;
            
            [JsonProperty(PropertyName = "Heal rate (seconds)")]
            public float healRateSeconds = 1f;

            [JsonProperty(PropertyName = "Heal in amount (0 to disable)")]
            public float healAmount = 1f;

            [JsonProperty(PropertyName = "Heal in percents (0-100)")]
            public float healPercent = 1f;

            [JsonProperty(PropertyName = "Work for Building Blocks")]
            public bool buildingBlocks = true;

            [JsonProperty(PropertyName = "Work for Doors")]
            public bool doors = true;
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
                for (var i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }
                
                LoadDefaultConfig();
                return;
            }

            ValidateConfig();
            SaveConfig();
        }

        private static void ValidateConfig()
        {
            if (ConVar.Server.hostname.Contains("[DEBUG]") == true)
            {
                config = new ConfigData();
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Scripts

        private class HealComponent : MonoBehaviour
        {
            private BaseCombatEntity entity;
            private bool MaxHealth => Math.Abs(entity.MaxHealth() - entity.Health()) < 0.1f;

            private void Awake()
            {
                entity = GetComponent<BaseCombatEntity>();
            }

            public void OnChangedState()
            {
                var health = 0f;
                if (config.spawnHealthPercent > 0)
                {
                    health += entity.MaxHealth() / 100f * config.spawnHealthPercent;
                }

                if (config.spawnHealthAmount > 0)
                {
                    health += config.spawnHealthAmount;
                }

                if (health > 0f)
                {
                    entity.health = health;
                }

                if (config.healRateSeconds > 0)
                {
                    CancelInvoke(nameof(Regen));
                    InvokeRepeating(nameof(Regen), config.healRateSeconds, config.healRateSeconds);
                }
            }

            private void Regen()
            {
                if (MaxHealth == true)
                {
                    Destroy(this);
                    return;
                }

                if (entity.lastAttacker != null && config.interruptAfterAnyDamage == true)
                {
                    Destroy(this);
                    return;
                }

                if (entity.SecondsSinceAttacked < config.secondsAfterAttacked)
                {
                    return;
                }

                var healAmount = 0f;
                if (config.healPercent > 0)
                {
                    healAmount += entity.MaxHealth() / 100f * config.healPercent;
                }

                if (config.healAmount > 0)
                {
                    healAmount += config.healAmount;
                }

                if (healAmount > 0f)
                {
                    entity.Heal(healAmount);
                }
            }
        }

        #endregion
        
        #region NoEscape 01.06.2020

        [PluginReference] private Plugin NoEscape;

        private bool InRaidBlock(BasePlayer player)
        {
            return NoEscape?.Call<bool>("IsRaidBlocked", player) ?? false;
        }

        #endregion
    }
}