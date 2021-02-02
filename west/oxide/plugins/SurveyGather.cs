using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Survey Gather", "MJSU", "1.0.3")]
    [Description("Spawns configurable entities where a player throws a survey charge")]
    internal class SurveyGather : RustPlugin
    {
        #region Class Fields
        [PluginReference] private Plugin GameTipAPI;
        
        private PluginConfig _pluginConfig; //Plugin Config

        private const string AccentColor = "#de8732";
        private const string UsePermission = "surveygather.use";
        private const string AdminPermission = "surveygather.admin";

        private ItemDefinition _surveyCharge;
        #endregion

        #region Setup & Loading
        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
            permission.RegisterPermission(AdminPermission, this);
            
            cmd.AddChatCommand(_pluginConfig.ChatCommand, this, SurveyGatherChatCommand);
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.NoPermission] = "You do not have permission to use this command",
                [LangKeys.Chat] = $"<color=#bebebe>[<color={AccentColor}>{Title}</color>] {{0}}</color>",
                [LangKeys.Notification] = "Throw this survey charge on the ground to spawn a random item",
                [LangKeys.NoEntity] = "You're not looking at an entity. Please try to get closer or from a different angle.",
                [LangKeys.Add] = $"You have successfully added <color={AccentColor}>{{0}}</color> prefab to the config.",
                [LangKeys.HelpText] = "Allows admins to configure which entities are spawned with the survey charge.\n" +
                                      $"<color={AccentColor}>/{{0}} add</color> - to add the entity you're looking at\n" +
                                      $"<color={AccentColor}>/{{0}}</color> - to view this help text again\n"
            }, this);
        }
        
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
            config.GatherItems = config.GatherItems ?? new List<GatherConfig>
            {
                new GatherConfig
                {
                    Prefab = "assets/bundled/prefabs/radtown/ore_stone.prefab",
                    Chance = 45f,
                    Distance = 1.5f,
                    Duration = 3f,
                    MinHealth = .3f,
                    MaxHealth = .9f
                },
                new GatherConfig
                {
                    Prefab = "assets/bundled/prefabs/radtown/ore_metal.prefab",
                    Chance = 40f,
                    Distance = 1.5f,
                    Duration = 3f,
                    MinHealth = .3f,
                    MaxHealth = .9f
                },
                new GatherConfig
                {
                    Prefab = "assets/bundled/prefabs/radtown/ore_sulfur.prefab",
                    Chance = 35f,
                    Distance = 1.5f,
                    Duration = 3f,
                    MinHealth = .3f,
                    MaxHealth = .9f
                },
                new GatherConfig
                {
                    Prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab",
                    Chance = 2.5f,
                    Distance = 1.5f,
                    Duration = 5f,
                    MinHealth = 1f,
                    MaxHealth = 1f
                },
                new GatherConfig
                {
                    Prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                    Chance = 5,
                    Distance = 1.0f,
                    Duration = 5f,
                    MinHealth = 1f,
                    MaxHealth = 1f
                }
            };
            return config;
        }

        private void OnServerInitialized()
        {
            _surveyCharge = ItemManager.FindItemDefinition("surveycharge");
            if (!_pluginConfig.AllowCrafting)
            {
                _surveyCharge.Blueprint.userCraftable = false;
            }

            if (!_pluginConfig.AllowResearching)
            {
                _surveyCharge.Blueprint.isResearchable = false;
            }

            if (!_pluginConfig.EnableNotifications)
            {
                Unsubscribe(nameof(OnActiveItemChanged));
            }
        }

        private void Unload()
        {
            if (!_pluginConfig.AllowCrafting)
            {
                _surveyCharge.Blueprint.userCraftable = true;
            }

            if (!_pluginConfig.AllowResearching)
            {
                _surveyCharge.Blueprint.isResearchable = true;
            }

            foreach (RiserBehavior riser in GameObject.FindObjectsOfType<RiserBehavior>())
            {
                riser.DoDestroy();
            }
        }
        #endregion

        #region Chat Command

        private void SurveyGatherChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!HasPermission(player, AdminPermission))
            {
                Chat(player, LangKeys.NoPermission);
                return;
            }

            if (args.Length == 0)
            {
                Chat(player, LangKeys.HelpText, _pluginConfig.ChatCommand);
                return;
            }

            switch (args[0].ToLower())
            {
                case "add":
                    HandleAdd(player);
                    break;
                
                default:
                    Chat(player, LangKeys.HelpText, _pluginConfig.ChatCommand);
                    break;
            }
        }

        private void HandleAdd(BasePlayer player)
        {
            BaseEntity entity = Raycast<BaseEntity>(player.eyes.HeadRay(), 5f);
            if (entity == null)
            {
                Chat(player, LangKeys.NoEntity);
                return;
            }
            
            _pluginConfig.GatherItems.Add(new GatherConfig
            {
                Chance = 0.05f,
                Distance = entity.bounds.size.y,
                Duration = 3f,
                MinHealth = 1f,
                MaxHealth = 1f,
                Prefab = entity.PrefabName
            });
            
            Config.WriteObject(_pluginConfig);
            
            Chat(player, LangKeys.Add, entity.PrefabName);
        }
        #endregion

        #region Oxide Hook
        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem == null || newItem.info.itemid != _surveyCharge?.itemid || !HasPermission(player, UsePermission))
            {
                return;
            }

            Chat(player, LangKeys.Notification);
            GameTipAPI?.Call("ShowGameTip", player, Lang(LangKeys.Notification, player), 6f);
        }

        private void OnExplosiveDropped(BasePlayer player, SurveyCharge entity)
        {
            HandleSurveyCharge(player, entity);
        }

        private void OnExplosiveThrown(BasePlayer player, SurveyCharge entity)
        {
            HandleSurveyCharge(player, entity);
        }

        private void HandleSurveyCharge(BasePlayer player, SurveyCharge charge)
        {
            if (charge == null || !HasPermission(player, UsePermission) || player.IsBuildingBlocked())
            {
                return;
            }

            charge.CancelInvoke(charge.Explode);
            charge.Invoke(() =>
            {
                RaycastHit raycastHit;
                if (!WaterLevel.Test(charge.transform.position) 
                    && TransformUtil.GetGroundInfo(charge.transform.position, out raycastHit, 0.3f, Layers.Terrain) 
                    && !RaycastAny(new Ray(charge.transform.position, Vector3.down), .5f))
                {
                    
                    SpawnEntity(charge.transform.position);
                }
                
                if (charge.explosionEffect.isValid)
                {
                    Effect.server.Run(charge.explosionEffect.resourcePath, charge.PivotPoint(), (!charge.explosionUsesForward ? Vector3.up : charge.transform.forward), null, true);
                }
                
                if (!charge.IsDestroyed)
                {
                    charge.Kill(BaseNetworkable.DestroyMode.Gib);
                }
            }, charge.GetRandomTimerTime());
        }

        private void SpawnEntity(Vector3 pos)
        {
            float random = Core.Random.Range(0, _pluginConfig.GatherItems.Sum(gi => gi.Chance));
            float total = 0;
            GatherConfig config = null;
            foreach (GatherConfig item in _pluginConfig.GatherItems)
            {
                if (random <= item.Chance + total)
                {
                    config = item;
                    break;
                }

                total += item.Chance;
            }

            if (config == null)
            {
                config = _pluginConfig.GatherItems.Last();
            }

            BaseEntity entity = GameManager.server.CreateEntity(config.Prefab, pos + Vector3.down * config.Distance);
            if (entity == null)
            {
                return;
            }

            ResourceEntity resource = entity as ResourceEntity;
            if (resource != null)
            {
                resource.health = Core.Random.Range(resource.startHealth * config.MinHealth, resource.startHealth * config.MaxHealth);;
            }
            
            OreResourceEntity ore = entity as OreResourceEntity;
            if (ore != null)
            {
                ore.UpdateNetworkStage();
            }
            
            BaseCombatEntity combat = entity as BaseCombatEntity;
            if (combat != null)
            {
                combat.health = Core.Random.Range(combat.startHealth * config.MinHealth, combat.startHealth * config.MaxHealth);;
            }
            
            RiserBehavior riser = entity.gameObject.AddComponent<RiserBehavior>();
            riser.StartRise(config);
            entity.Spawn();
            entity.SendNetworkUpdate();
        }
        #endregion

        #region Helper Methods
        private bool RaycastAny(Ray ray, float distance)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, distance);
            return hits.Any(h => h.GetEntity() != null);
        }
        
        private T Raycast<T>(Ray ray, float distance) where T : BaseEntity
        {
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, distance))
            {
                return null;
            }

            return hit.GetEntity() as T;
        }
        
        private void Chat(BasePlayer player, string key, params object[] args) => PrintToChat(player, Lang(LangKeys.Chat, player, Lang(key, player, args)));
        
        private string Lang(string key, BasePlayer player = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, player?.UserIDString), args);
            }
            catch(Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception\n:{ex}");
                throw;
            }
        }

        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);
        #endregion

        #region Classes
        private class RiserBehavior : FacepunchBehaviour
        {
            private BaseEntity Entity { get; set; }
            private GatherConfig Config { get; set; }

            private float _timeTaken;

            private void Awake()
            {
                Entity = GetComponent<BaseEntity>();
                enabled = false;
            }

            public void StartRise(GatherConfig config)
            {
                Config = config;
                enabled = true;
            }

            private void FixedUpdate()
            {
                if (_timeTaken > Config.Duration)
                {
                    OreResourceEntity ore = Entity as OreResourceEntity;
                    if (ore != null) 
                    {
                        ore.CleanupBonus();
                        ore._hotSpot = ore.SpawnBonusSpot(Vector3.zero);
                    }
                    
                    enabled = false;
                    Destroy(this);
                    return;
                }

                Entity.transform.position += Vector3.up * Config.Distance * (Time.deltaTime / Config.Duration);
                Entity.SendNetworkUpdate();
                _timeTaken += Time.deltaTime;
            }

            public void DoDestroy()
            {
                Destroy(this);
            }
        }

        private class PluginConfig
        {
            [DefaultValue("sg")]
            [JsonProperty(PropertyName = "Survey Gather Chat Command")]
            public string ChatCommand { get; set; }
            
            [DefaultValue(false)]
            [JsonProperty(PropertyName = "Allow survey charge researching")]
            public bool AllowResearching { get; set; }
            
            [DefaultValue(false)]
            [JsonProperty(PropertyName = "Allow survey charge crafting")]
            public bool AllowCrafting { get; set; }
            
            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Enabled notifications")]
            public bool EnableNotifications { get; set; }
            
            [JsonProperty(PropertyName = "Gather Items")]
            public List<GatherConfig> GatherItems { get; set; }
        }

        private class GatherConfig
        {
            [JsonProperty(PropertyName = "Prefab to spawn")]
            public string Prefab { get; set; }
            
            [JsonProperty(PropertyName = "Chance to spawn")]
            public float Chance { get; set; }
            
            [JsonProperty(PropertyName = "Distance to spawn underground")]
            public float Distance { get; set; }
            
            [JsonProperty(PropertyName = "Min health Percentage")]
            public float MinHealth { get; set; }
            
            [JsonProperty(PropertyName = "Max health Percentage")]
            public float MaxHealth { get; set; }
            
            [JsonProperty(PropertyName = "Rise duration (Seconds)")]
            public float Duration { get; set; }
        }
        
        private class LangKeys
        {
            public const string NoPermission = "NoPermission";
            public const string Chat = "Chat";
            public const string Notification = "Notification";
            public const string HelpText = "HelpText";
            public const string Add = "Add";
            public const string NoEntity = "NoEntity";
        }
        #endregion
    }
}
