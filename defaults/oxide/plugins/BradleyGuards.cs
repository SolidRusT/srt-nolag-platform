using System.Collections.Generic;
using System.Linq;
using System;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;
using Facepunch;
using Newtonsoft.Json;
using VLB;

namespace Oxide.Plugins
{
    [Info("Bradley Guards", "Bazz3l", "1.3.7")]
    [Description("Call in armed reinforcements when bradley is destroyed at launch site.")]
    public class BradleyGuards : RustPlugin
    {
        [PluginReference] Plugin Kits;
        
        #region Fields
        
        private const string CH47_PREFAB = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab";
        private const string LANDING_NAME = "BradleyLandingZone";

        private readonly HashSet<NPCPlayerApex> _npcs = new HashSet<NPCPlayerApex>();
        private CH47HelicopterAIController _chinook;
        private CH47LandingZone _landingZone;
        private Quaternion _landingRotation;
        private Vector3 _monumentPosition;
        private Vector3 _landingPosition;
        private Vector3 _chinookPosition;
        private Vector3 _bradleyPosition;
        private bool _hasLaunch;
        private PluginConfig _config;
        
        #endregion

        #region Config
        
        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new JsonException();
                }
                
                if (_config.ToDictionary().Keys
                    .SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys)) return;
            }
            catch
            {
                PrintWarning("Loaded default config, please check your configuration file for errors.");
                
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        class PluginConfig
        {
            [JsonProperty(PropertyName = "ChatIcon (chat icon SteamID64)")]
            public ulong ChatIcon;

            [JsonProperty(PropertyName = "APCHealth (set starting health)")]
            public float APCHealth;

            [JsonProperty(PropertyName = "APCCrates (amount of crates to spawn)")]
            public int APCCrates;

            [JsonProperty(PropertyName = "NPCAmount (amount of guards to spawn max 11)")]
            public int NPCAmount;

            [JsonProperty(PropertyName = "InstantCrates (unlock crates when guards are eliminated)")]
            public bool InstantCrates;
            
            [JsonProperty(PropertyName = "DisableChinookDamage (should chinook be able to take damage)")]
            public bool DisableChinookDamage;

            [JsonProperty(PropertyName = "GuardSettings (create different types of guards must contain atleast 1)")]
            public List<GuardSetting> GuardSettings;
            
            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    ChatIcon = 0,
                    APCHealth = 1000f,
                    APCCrates = 4,
                    NPCAmount = 6,
                    InstantCrates = true,
                    GuardSettings = new List<GuardSetting> {
                        new GuardSetting
                        {
                            Name = "Heavy Gunner",
                            Health = 300f,
                            MaxRoamRadius = 80f,
                            MaxAggressionRange = 200f,
                        },
                        new GuardSetting
                        {
                            Name = "Light Gunner",
                            Health = 200f,
                            MaxRoamRadius = 80f,
                            MaxAggressionRange = 150f,
                        }
                    }
                };
            }
            
            public string ToJson() => 
                JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() =>
                JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        class GuardSetting
        {
            [JsonProperty(PropertyName = "Name (custom display name)")]
            public string Name;

            [JsonProperty(PropertyName = "Health (set starting health)")]
            public float Health = 100f;

            [JsonProperty(PropertyName = "DamageScale (higher the value more damage)")]
            public float DamageScale = 0.2f;

            [JsonProperty(PropertyName = "MaxRoamRadius (max radius guards will roam)")]
            public float MaxRoamRadius = 30f;

            [JsonProperty(PropertyName = "MaxAggressionRange (distance guards will become aggressive)")]
            public float MaxAggressionRange = 200f;

            [JsonProperty(PropertyName = "KitName (custom kit name)")]
            public string KitName = "";

            [JsonProperty(PropertyName = "KitEnabled (enable custom kit)")]
            public bool KitEnabled = false;
        }
        
        #endregion

        #region Oxide
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                {"EventStart", "<color=#DC143C>Bradley Guards</color>: Tank commander sent for reinforcements, fight for your life."},
                {"EventEnded", "<color=#DC143C>Bradley Guards</color>: Reinforcements have been eliminated, loot up fast."},
            }, this);
        }

        void OnServerInitialized() => 
            GetLandingPoint();

        void Unload() => 
            CleanUp();

        void OnEntitySpawned(BradleyAPC bradley) => 
            OnAPCSpawned(bradley);

        void OnEntityDeath(BradleyAPC bradley, HitInfo info) => 
            OnAPCDeath(bradley);

        void OnEntityDeath(NPCPlayerApex npc, HitInfo info) => 
            OnNPCDeath(npc);

        void OnEntityKill(NPCPlayerApex npc) => 
            OnNPCDeath(npc);
        
        object OnHelicopterAttacked(CH47HelicopterAIController heli, HitInfo info) => 
            OnCH47Attacked(heli, info);

        void OnFireBallDamage(FireBall fireball, NPCPlayerApex npc, HitInfo info)
        {
            if (!(_npcs.Contains(npc) && info.Initiator is FireBall)) return;
            
            info.DoHitEffects = false;
            info.damageTypes.ScaleAll(0f);
        }

        void OnEntityDismounted(BaseMountable mountable, NPCPlayerApex npc)
        {
            if (!_npcs.Contains(npc)) return;
            
            npc.Resume();
            npc.modelState.mounted = false;
            npc.modelState.onground = true;
            npc.SetFact(NPCPlayerApex.Facts.IsMounted, 0, true, true);
            npc.SetFact(NPCPlayerApex.Facts.CanNotWieldWeapon, 0, true, true);
            npc.InvokeRandomized(new Action(npc.RadioChatter), npc.RadioEffectRepeatRange.x, npc.RadioEffectRepeatRange.x, npc.RadioEffectRepeatRange.y - npc.RadioEffectRepeatRange.x);
        }
        
        #endregion

        #region Core

       void SpawnEvent()
        {
            _chinook = GameManager.server.CreateEntity(CH47_PREFAB, _chinookPosition, Quaternion.identity) as CH47HelicopterAIController;
            _chinook.Spawn();
            _chinook.SetLandingTarget(_landingPosition);
            _chinook.SetMinHoverHeight(1.5f);
            _chinook.CancelInvoke(new Action(_chinook.SpawnScientists));
            _chinook.GetOrAddComponent<CH47NavigationComponent>();

            for (int i = 0; i < _config.NPCAmount - 1; i++)
            {
                SpawnScientist(_config.GuardSettings.GetRandom(), _chinook.transform.position + _chinook.transform.forward * 10f, _bradleyPosition);
            }

            for (int j = 0; j < 1; j++)
            {
                SpawnScientist(_config.GuardSettings.GetRandom(), _chinook.transform.position - _chinook.transform.forward * 15f, _bradleyPosition);
            }

            MessageAll("EventStart");
        }

        void SpawnScientist(GuardSetting settings, Vector3 position, Vector3 eventPos)
        {
            NPCPlayerApex npc = GameManager.server.CreateEntity(_chinook.scientistPrefab.resourcePath, position, Quaternion.identity) as NPCPlayerApex;
            if (npc == null) return;
            
            npc.Spawn();
            npc.Mount(_chinook);
            npc.RadioEffect = new GameObjectRef();
            npc.DeathEffect = new GameObjectRef();
            npc.displayName = settings.Name;
            npc.damageScale = settings.DamageScale;
            npc.Stats.VisionRange = settings.MaxAggressionRange + 3f;
            npc.Stats.DeaggroRange = settings.MaxAggressionRange + 2f;
            npc.Stats.AggressionRange = settings.MaxAggressionRange + 1f;
            npc.Stats.LongRange = settings.MaxAggressionRange;
            npc.Stats.MaxRoamRange = settings.MaxRoamRadius;
            npc.Stats.Hostility = 1f;
            npc.Stats.Defensiveness = 1f;
            npc.Stats.OnlyAggroMarkedTargets = true;
            npc.startHealth = settings.Health;
            npc.InitializeHealth(settings.Health, settings.Health);
            npc.InitFacts();

            RenameNPC(npc as Scientist, settings.Name);

            npc.GetOrAddComponent<NPCNavigationComponent>()
                ?.SetDestination(GetRandomPoint(eventPos, 6f));
            
            _npcs.Add(npc);

            npc.Invoke(() => GiveKit(npc, settings.KitEnabled, settings.KitName), 2f);
        }

        void RenameNPC(Scientist npc, string name)
        {
            npc.displayName = name;
            npc.LootPanelName = name;
        }

        void GiveKit(NPCPlayerApex npc, bool kitEnabled, string kitName)
        {
            if (kitEnabled && !string.IsNullOrEmpty(kitName))
            {
                npc.inventory.Strip();
                Interface.Oxide.CallHook("GiveKit", npc, kitName);
                return;
            }

            ItemManager.CreateByName("scientistsuit_heavy", 1, 0)?.MoveToContainer(npc.inventory.containerWear);
        }

        void OnNPCDeath(NPCPlayerApex npc)
        {
            if (!_npcs.Remove(npc) || _npcs.Count > 0) return;

            if (_config.InstantCrates)
            {
                RemoveFlames();
                UnlockCrates();
            }

            MessageAll("EventEnded");
        }

        void OnAPCSpawned(BradleyAPC bradley)
        {
            Vector3 position = bradley.transform.position;
            
            if (!IsInBounds(position)) return;

            bradley.maxCratesToSpawn = _config.APCCrates;
            bradley._maxHealth = bradley._health = _config.APCHealth;
            bradley.health = bradley._maxHealth;

            ClearGuards();
        }

        void OnAPCDeath(BradleyAPC bradley)
        {
            if (bradley == null || bradley.IsDestroyed) return;

            Vector3 position = bradley.transform.position;
            
            if (!IsInBounds(position)) return;

            _bradleyPosition = position;

            SpawnEvent();
        }

        object OnCH47Attacked(CH47HelicopterAIController heli, HitInfo info)
        {
            if (heli == null || !_config.DisableChinookDamage) return null;
            if (heli == _chinook) return true;
            return null;
        }

        void RemoveFlames()
        {
            List<FireBall> entities = Pool.GetList<FireBall>();

            Vis.Entities(_bradleyPosition, 25f, entities);

            foreach (FireBall fireball in entities)
            {
                if (fireball.IsValid() && !fireball.IsDestroyed)
                {
                    fireball.Kill();
                }
            }

            Pool.FreeList(ref entities);
        }
        
        void UnlockCrates()
        {
            List<LockedByEntCrate> entities = Pool.GetList<LockedByEntCrate>();

            Vis.Entities(_bradleyPosition, 25f, entities);

            foreach (LockedByEntCrate crate in entities)
            {
                if (!(crate.IsValid() && !crate.IsDestroyed)) continue;
                
                crate.SetLocked(false);

                if (crate.lockingEnt == null) continue;

                BaseEntity entity = crate.lockingEnt.GetComponent<BaseEntity>();

                if (entity.IsValid() && !entity.IsDestroyed)
                {
                    entity.Kill();
                }
            }

            Pool.FreeList(ref entities);
        }

        void CreateLandingZone()
        {
            GameObject gameObject = new GameObject(LANDING_NAME);
            gameObject.transform.SetPositionAndRotation(_landingPosition, _landingRotation);
            _landingZone = gameObject.AddComponent<CH47LandingZone>();
        }

        void CleanUp()
        {
            ClearGuards();
            ClearZones();
        }
        
        void ClearZones()
        {
            if (_landingZone == null) return;
            
            UnityEngine.Object.Destroy(_landingZone.gameObject);
                
            _landingZone = null;
        }

        void ClearGuards()
        {
            for (int i = 0; i < _npcs.Count; i++)
            {
                NPCPlayerApex npc = _npcs.ElementAt(i);
                
                if (npc.IsValid() && !npc.IsDestroyed)
                {
                    npc.Kill();
                }
            }

            _npcs.Clear();
        }

        void GetLandingPoint()
        {
            foreach (MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (!monument.gameObject.name.Contains("launch_site_1")) continue;

                SetLandingPoint(monument);
            }
        }
        
        void SetLandingPoint(MonumentInfo monument)
        {
            _monumentPosition = monument.transform.position;
            
            _landingRotation = monument.transform.rotation;
            _landingPosition = _monumentPosition + monument.transform.right * 125f;
            _landingPosition.y = TerrainMeta.HeightMap.GetHeight(_landingPosition);
            
            _chinookPosition = _monumentPosition + -monument.transform.right * 250f;
            _chinookPosition.y += 150f;

            _hasLaunch = true;

            CreateLandingZone();
        }
        
        bool IsInBounds(Vector3 position) => _hasLaunch && Vector3.Distance(_monumentPosition, position) <= 300f;

        #endregion

        #region Component

        class NPCNavigationComponent : MonoBehaviour
        {
            NPCPlayerApex _npc;
            Vector3 _destination;

            private void Awake()
            {
                _npc = gameObject.GetComponent<NPCPlayerApex>();

                InvokeRepeating(nameof(DoMove), 5f, 5f);
            }

            private void OnDestroy()
            {
                CancelInvoke();

                if (_npc.IsValid() && !_npc.IsDestroyed)
                {
                    _npc.Kill();
                }
            }

            public void SetDestination(Vector3 destination)
            {
                _destination = destination;
            }

            private void DoMove()
            {
                if (!(_npc.IsValid() && !_npc.IsDestroyed)) return;
                
                if (_npc.isMounted) return;

                if (IsOutOfRange() && _npc.AttackTarget != null)
                {
                    _npc.AttackTarget = null;
                }

                if (_npc.AttackTarget != null)
                {
                    _npc.NeverMove = false;
                }

                if (_npc.AttackTarget == null)
                {
                    _npc.NeverMove = true;

                    if (_npc.IsStuck)
                    {
                        Warp();
                    }

                    if (_npc.GetNavAgent == null || !_npc.GetNavAgent.isOnNavMesh)
                    {
                        _npc.finalDestination = _destination;
                    }
                    else
                    {
                        _npc.GetNavAgent.SetDestination(_destination);
                    }

                    _npc.IsStopped = false;
                    _npc.Destination = _destination;
                }
            }

            private void Warp()
            {
                _npc.Pause();
                _npc.ServerPosition = _destination;
                _npc.GetNavAgent.Warp(_destination);
                _npc.stuckDuration = 0f;
                _npc.IsStuck = false;
                _npc.Resume();
            }

            private bool IsOutOfRange() =>
                Vector3.Distance(transform.position, _destination) > _npc.Stats.MaxRoamRange;
        }

        class CH47NavigationComponent : MonoBehaviour
        {
            CH47HelicopterAIController _chinook;

            void Awake()
            {
                _chinook = GetComponent<CH47HelicopterAIController>();

                InvokeRepeating(nameof(CheckDropped), 5f, 5f);
            }

            void OnDestroy()
            {
                CancelInvoke();

                if (_chinook.IsValid() && !_chinook.IsDestroyed)
                    _chinook.Invoke(_chinook.DelayedKill, 10f);
            }

            void CheckDropped()
            {
                if (_chinook.HasAnyPassengers()) return;

                Destroy(this);
            }
        }
        
        #endregion

        #region Helpers
        
        string Lang(string key, string id = null, params object[] args) => 
            string.Format(lang.GetMessage(key, this, id), args);

        void MessageAll(string key) => 
            Server.Broadcast(Lang(key, null), _config.ChatIcon);

        Vector3 GetRandomPoint(Vector3 position, float radius)
        {
            Vector3 pos = position + UnityEngine.Random.onUnitSphere * radius;
            pos.y = TerrainMeta.HeightMap.GetHeight(pos);
            return pos;
        }
        
        #endregion
    }
}