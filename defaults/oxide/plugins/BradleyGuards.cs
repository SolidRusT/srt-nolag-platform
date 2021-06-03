using System.Collections.Generic;
using System.Linq;
using System;
using Rust;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;
using Facepunch;
using Newtonsoft.Json;
using VLB;

namespace Oxide.Plugins
{
    [Info("Bradley Guards", "Bazz3l", "1.3.6")]
    [Description("Calls reinforcements when bradley is destroyed at launch site.")]
    public class BradleyGuards : RustPlugin
    {
        [PluginReference] Plugin Kits;

        #region Fields
        
        private const string Ch47Prefab = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab";
        private const string LandingName = "BradleyLandingZone";

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
        
        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

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
            }
            catch
            {
                LoadDefaultConfig();
                
                SaveConfig();
                
                PrintWarning("Loaded default configuration file.");
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private PluginConfig GetDefaultConfig()
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

        private class PluginConfig
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

            [JsonProperty(PropertyName = "GuardSettings (create different types of guards must contain atleast 1)")]
            public List<GuardSetting> GuardSettings;
        }

        private class GuardSetting
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
                {"EventStart", "<color=#DC143C>Bradley Gaurds</color>: Tank commander sent for reinforcements, fight for your life."},
                {"EventEnded", "<color=#DC143C>Bradley Gaurds</color>: Reinforcements have been eliminated, loot up fast."},
            }, this);
        }

        private void OnServerInitialized() => GetLandingPoint();

        private void Unload() => CleanUp();

        private void OnEntitySpawned(BradleyAPC bradley) => OnAPCSpawned(bradley);

        private void OnEntityDeath(BradleyAPC bradley, HitInfo info) => OnAPCDeath(bradley);

        private void OnEntityDeath(NPCPlayerApex npc, HitInfo info) => OnNPCDeath(npc);

        private void OnEntityKill(NPCPlayerApex npc) => OnNPCDeath(npc);

        private void OnFireBallDamage(FireBall fire, NPCPlayerApex npc, HitInfo info)
        {
            if (!(_npcs.Contains(npc) && info.Initiator is FireBall))
            {
                return;
            }

            info.damageTypes = new DamageTypeList();
            info.DoHitEffects = false;
            info.damageTypes.ScaleAll(0f);
        }

        private void OnEntityDismounted(BaseMountable mountable, NPCPlayerApex npc)
        {
            if (!_npcs.Contains(npc))
            {
                return;
            }

            npc.SetFact(NPCPlayerApex.Facts.IsMounted, (byte) 0, true, true);
            npc.SetFact(NPCPlayerApex.Facts.WantsToDismount, (byte) 0, true, true);
            npc.SetFact(NPCPlayerApex.Facts.CanNotWieldWeapon, (byte) 0, true, true);
            npc.Resume();
        }
        
        #endregion

        #region Core

        private void SpawnEvent()
        {
            _chinook = GameManager.server.CreateEntity(Ch47Prefab, _chinookPosition, Quaternion.identity) as CH47HelicopterAIController;
            _chinook.Spawn();
            _chinook.SetLandingTarget(_landingPosition);
            _chinook.SetMinHoverHeight(1.5f);
            _chinook.CancelInvoke(new Action(_chinook.SpawnScientists));
            _chinook.GetOrAddComponent<CustomCh47>();

            for (int i = 0; i < _config.NPCAmount - 1; i++)
            {
                SpawnScientist(_chinook, _config.GuardSettings.GetRandom(), _chinook.transform.position + _chinook.transform.forward * 10f, _bradleyPosition);
            }

            for (int j = 0; j < 1; j++)
            {
                SpawnScientist(_chinook, _config.GuardSettings.GetRandom(), _chinook.transform.position - _chinook.transform.forward * 15f, _bradleyPosition);
            }

            MessageAll("EventStart");
        }

        private void SpawnScientist(CH47HelicopterAIController chinook, GuardSetting settings, Vector3 position, Vector3 eventPos)
        {
            NPCPlayerApex npc = GameManager.server.CreateEntity(chinook.scientistPrefab.resourcePath, position, Quaternion.identity) as NPCPlayerApex;
            
            if (npc == null)
            {
                return;
            }
            
            npc.Spawn();
            npc.Mount((BaseMountable) chinook);
            
            npc.RadioEffect = new GameObjectRef();
            npc.DeathEffect = new GameObjectRef();
            npc.displayName = settings.Name;
            npc.startHealth = settings.Health;
            npc.damageScale = settings.DamageScale;
            npc.Stats.VisionRange = settings.MaxAggressionRange + 3f;
            npc.Stats.DeaggroRange = settings.MaxAggressionRange + 2f;
            npc.Stats.AggressionRange = settings.MaxAggressionRange + 1f;
            npc.Stats.LongRange = settings.MaxAggressionRange;
            npc.Stats.MaxRoamRange = settings.MaxRoamRadius;
            npc.Stats.Hostility = 1f;
            npc.Stats.Defensiveness = 1f;
            npc.Stats.OnlyAggroMarkedTargets = true;
            npc.InitializeHealth(settings.Health, settings.Health);
            npc.InitFacts();

            (npc as Scientist).LootPanelName = settings.Name;
            
            _npcs.Add(npc);

            npc.Invoke(() => {
                GiveKit(npc, settings.KitEnabled, settings.KitName);

                npc.gameObject.AddComponent<CustomNavigation>().SetDestination(GetRandomPoint(eventPos, 6f));
            }, 2f);
        }

        private void GiveKit(NPCPlayerApex npc, bool kitEnabled, string kitName)
        {
            if (kitEnabled && !string.IsNullOrEmpty(kitName))
            {
                npc.inventory.Strip();
                
                Interface.Oxide.CallHook("GiveKit", npc, kitName);
                
                return;
            }

            ItemManager.CreateByName("scientistsuit_heavy", 1, 0)?.MoveToContainer(npc.inventory.containerWear);
        }

        private void OnNPCDeath(NPCPlayerApex npc)
        {
            if (!_npcs.Remove(npc))
            {
                return;
            }

            if (_npcs.Count > 0)
            {
                return;
            }

            if (_config.InstantCrates)
            {
                RemoveFlames();
                UnlockCrates();
            }

            MessageAll("EventEnded");
        }

        private void OnAPCSpawned(BradleyAPC bradley)
        {
            Vector3 position = bradley.transform.position;
            
            if (!IsInBounds(position))
            {
                return;
            }
            
            bradley.maxCratesToSpawn = _config.APCCrates;
            bradley._maxHealth = bradley._health = _config.APCHealth;
            bradley.health = bradley._maxHealth;

            ClearGuards();
        }

        private void OnAPCDeath(BradleyAPC bradley)
        {
            if (bradley == null || bradley.IsDestroyed)
            {
                return;
            }

            Vector3 position = bradley.transform.position;
            
            if (!IsInBounds(position))
            {
                return;
            }

            _bradleyPosition = position;

            SpawnEvent();
        }

        private void RemoveFlames()
        {
            List<FireBall> entities = Pool.GetList<FireBall>();

            Vis.Entities(_bradleyPosition, 25f, entities);

            foreach (FireBall fireball in entities)
            {
                if (!(fireball.IsValid() && !fireball.IsDestroyed)) continue;

                NextFrame(() => fireball.Kill());
            }

            Pool.FreeList(ref entities);
        }

        private void UnlockCrates()
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

        private void CreateLandingZone()
        {
            _landingZone = new GameObject(LandingName) {
                layer = 16,
                transform = {
                    position = _landingPosition,
                    rotation = _landingRotation
                }
            }.AddComponent<CH47LandingZone>();
        }

        private void CleanUp()
        {
            ClearGuards();
            ClearZones();
        }

        private void ClearZones()
        {
            if (_landingZone != null)
            {
                UnityEngine.Object.Destroy(_landingZone.gameObject);
            }

            _landingZone = null;
        }

        private void ClearGuards()
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

        private void GetLandingPoint()
        {
            foreach (MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (!monument.gameObject.name.Contains("launch_site_1")) continue;

                SetLandingPoint(monument);
            }
        }

        private void SetLandingPoint(MonumentInfo monument)
        {
            _monumentPosition = monument.transform.position;

            _landingRotation = monument.transform.rotation;
            _landingPosition = monument.transform.position + monument.transform.right * 125f;
            _landingPosition.y = TerrainMeta.HeightMap.GetHeight(_landingPosition);
            
            _chinookPosition = monument.transform.position + -monument.transform.right * 250f;
            _chinookPosition.y += 150f;

            _hasLaunch = true;

            CreateLandingZone();
        }

        private bool IsInBounds(Vector3 position)
        {
            return _hasLaunch && Vector3.Distance(_monumentPosition, position) <= 300f;
        }
        
        #endregion

        #region Component
        
        private class CustomNavigation : MonoBehaviour
        {
            private NPCPlayerApex _npc;
            private Vector3 _targetPoint;

            private void Awake()
            {
                _npc = gameObject.GetComponent<NPCPlayerApex>();

                InvokeRepeating(nameof(Relocate), 0f, 5f);
            }

            private void OnDestroy()
            {
                CancelInvoke();
                
                if (_npc.IsValid() && !_npc.IsDestroyed)
                {
                    _npc.Kill();
                }
            }

            public void SetDestination(Vector3 position)
            {
                _targetPoint = position;
            }

            private void Relocate()
            {
                if (_npc == null || _npc.IsDestroyed)
                {
                    return;
                }

                if (_npc.isMounted)
                {
                    return;
                }

                if (!(_npc.AttackTarget == null || IsOutOfBounds()))
                {
                    return;
                }
                
                if (_npc.IsStuck)
                {
                    DoWarp();
                }

                if (_npc.GetNavAgent == null || !_npc.GetNavAgent.isOnNavMesh)
                {    
                    _npc.finalDestination = _targetPoint;
                }
                else
                {
                    _npc.GetNavAgent.SetDestination(_targetPoint);
                    _npc.IsDormant = false;
                }

                _npc.IsStopped = false;
                _npc.Destination = _targetPoint;
            }

            private bool IsOutOfBounds()
            {
                return _npc.AttackTarget != null && Vector3.Distance(transform.position, _targetPoint) > _npc.Stats.MaxRoamRange;
            }

            private void DoWarp()
            {
                _npc.Pause();
                _npc.ServerPosition = _targetPoint;
                _npc.GetNavAgent.Warp(_targetPoint);
                _npc.stuckDuration = 0f;
                _npc.IsStuck = false;
                _npc.Resume();
            }
        }

        private class CustomCh47 : MonoBehaviour
        {
            private CH47HelicopterAIController _chinook;

            private void Awake()
            {
                _chinook = GetComponent<CH47HelicopterAIController>();

                InvokeRepeating(nameof(CheckDropped), 5f, 5f);
            }

            private void OnDestroy()
            {
                CancelInvoke();
                
                _chinook.Invoke(new Action(_chinook.DelayedKill), 10f);
            }

            private void CheckDropped()
            {
                if (_chinook == null || _chinook.IsDestroyed || _chinook.HasAnyPassengers())
                {
                    return;
                }

                Destroy(this);
            }
        }
        
        #endregion

        #region Helpers
        
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void MessageAll(string key) => Server.Broadcast(Lang(key, null), _config.ChatIcon);

        private Vector3 GetRandomPoint(Vector3 position, float radius)
        {
            Vector3 pos = position + UnityEngine.Random.onUnitSphere * radius;
            
            pos.y = TerrainMeta.HeightMap.GetHeight(pos);
            
            return pos;
        }
        
        #endregion
    }
}