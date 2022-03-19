using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using ProtoBuf;
using Rust;
using Rust.Ai;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("ZombieHorde", "k1lly0u", "0.4.8")]
    class ZombieHorde : RustPlugin
    {
        [PluginReference] 
        private Plugin Kits, Spawns;

        private static ZombieHorde Instance { get; set; }


        public enum SpawnSystem { None, Random, SpawnsDatabase }

        public enum SpawnState { Spawn, Despawn }


        private static BaseNavigator.NavigationSpeed DefaultRoamSpeed;

        private const string ADMIN_PERMISSION = "zombiehorde.admin";

        private const string IGNORE_PERMISSION = "zombiehorde.ignore";

        #region Oxide Hooks       
        private void OnServerInitialized()
        {
            Instance = this;
            
            permission.RegisterPermission(ADMIN_PERMISSION, this);
            permission.RegisterPermission(IGNORE_PERMISSION, this);

            if (!Configuration.Member.TargetedByPeaceKeeperTurrets)
                Unsubscribe(nameof(CanEntityBeHostile));

            if (Configuration.Member.TargetedByAPC)
                Unsubscribe(nameof(CanBradleyApcTarget));

            DefaultRoamSpeed = ParseType<BaseNavigator.NavigationSpeed>(Configuration.Horde.DefaultRoamSpeed);

            ValidateLoadoutProfiles();

            ValidateSpawnSystem();

            CreateMonumentHordeOrders();
        }

        private void OnEntityTakeDamage(BaseCombatEntity baseCombatEntity, HitInfo hitInfo)
        {
            if (hitInfo != null)
            {
                if (hitInfo.InitiatorPlayer is ZombieNPC)
                {
                    if (hitInfo.damageTypes.Get(DamageType.Explosion) > 0)
                    {
                        hitInfo.damageTypes.ScaleAll(ConVar.Halloween.scarecrow_beancan_vs_player_dmg_modifier);
                        return;
                    }

                    float damageMultiplier = (hitInfo.InitiatorPlayer as ZombieNPC).Loadout.DamageMultiplier;
                    if (damageMultiplier != 1f)
                        hitInfo.damageTypes.ScaleAll(damageMultiplier);
                }
            }
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            if (player == null || hitInfo == null)
                return;

            if (player is ZombieNPC)
            {                
                (player as ZombieNPC).Horde.OnMemberKilled((player as ZombieNPC), hitInfo.Initiator);
                return;
            }

            if (Configuration.Horde.CreateOnDeath && hitInfo.InitiatorPlayer is ZombieNPC)            
                (hitInfo.InitiatorPlayer as ZombieNPC).Horde.OnPlayerKilled(player);            
        }

        private void OnEntityKill(ZombieNPC zombieNPC)
        {
            if (zombieNPC != null && zombieNPC.Horde != null && !zombieNPC.Horde.isDespawning)
                zombieNPC.Horde.OnMemberKilled(zombieNPC, null);
        }

        private object CanBeTargeted(ZombieNPC zombieNPC, GunTrap gunTrap) => Configuration.Member.TargetedByTurrets ? null : (object)false;

        private object CanBeTargeted(ZombieNPC zombieNPC, FlameTurret flameTurret) => Configuration.Member.TargetedByTurrets ? null : (object)false;

        private object CanBeTargeted(ZombieNPC zombieNPC, AutoTurret autoTurret)
        {
            if (Configuration.Member.TargetedByTurrets)
                return null;

            if ((autoTurret.PeacekeeperMode() || autoTurret is NPCAutoTurret) && Configuration.Member.TargetedByPeaceKeeperTurrets)
                return null;

            return false;
        }
        
        private object CanEntityBeHostile(ZombieNPC zombieNPC) => true;

        private object CanBradleyApcTarget(BradleyAPC bradleyAPC, ZombieNPC zombieNPC) => false;

        private object OnNpcTarget(NPCPlayer npcPlayer, ZombieNPC zombieNPC) => Configuration.Member.TargetedByNPCs ? null : (object)true;

        private object OnNpcTarget(BaseNpc baseNpc, ZombieNPC zombieNPC) => Configuration.Member.TargetedByAnimals ? null : (object)true;

        private void Unload()
        {
            Horde.SpawnOrder.OnUnload();

            for (int i = Horde.AllHordes.Count - 1; i >= 0; i--)
                Horde.AllHordes[i].Destroy(true, true);

            Horde.AllHordes.Clear();

            ZombieNPC[] zombies = UnityEngine.Object.FindObjectsOfType<ZombieNPC>();
            for (int i = 0; i < zombies?.Length; i++)            
                zombies[i].Kill(BaseNetworkable.DestroyMode.None);            

            Configuration = null;
            Instance = null;
        }
        #endregion

        #region Sensations  
        private void OnEntityKill(TimedExplosive timedExplosive)
        {
            if (!Configuration.Horde.UseSenses)
                return;

            Sense.Stimulate(new Sensation()
            {
                Type = SensationType.Gunshot,
                Position = timedExplosive.transform.position,
                Radius = 80f,
            });
        }

        private void OnEntityKill(TreeEntity treeEntity)
        {
            if (!Configuration.Horde.UseSenses)
                return;

            Sense.Stimulate(new Sensation()
            {
                Type = SensationType.Gunshot,
                Position = treeEntity.transform.position,
                Radius = 30f,
            });
        }

        private void OnEntityKill(OreResourceEntity oreResourceEntity)
        {
            if (!Configuration.Horde.UseSenses)
                return;

            Sense.Stimulate(new Sensation()
            {
                Type = SensationType.Gunshot,
                Position = oreResourceEntity.transform.position,
                Radius = 30f,
            });
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (!Configuration.Horde.UseSenses)
                return;

            Sense.Stimulate(new Sensation()
            {
                Type = SensationType.Gunshot,
                Position = dispenser.transform.position,
                Radius = 20f
            });
        }
        #endregion

        #region Functions
        private T ParseType<T>(string type)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), type, true);
            }
            catch
            {
                return default(T);
            }
        }


        #region Horde Spawning
        private List<Vector3> _spawnPoints;

        private SpawnSystem _spawnSystem = SpawnSystem.None;

        private const int SPAWN_RAYCAST_MASK = 1 << 0 | 1 << 8 | 1 << 15 | 1 << 17 | 1 << 21 | 1 << 29;

        private const TerrainTopology.Enum SPAWN_TOPOLOGY_MASK = (TerrainTopology.Enum.Ocean | TerrainTopology.Enum.River | TerrainTopology.Enum.Lake | TerrainTopology.Enum.Cliff | TerrainTopology.Enum.Cliffside | TerrainTopology.Enum.Offshore | TerrainTopology.Enum.Summit | TerrainTopology.Enum.Decor);

        private static bool ContainsTopologyAtPoint(TerrainTopology.Enum mask, Vector3 position) => (TerrainMeta.TopologyMap.GetTopology(position, 1f) & (int)mask) != 0;

        private bool ValidateSpawnSystem()
        {
            _spawnSystem = ParseType<SpawnSystem>(Configuration.Horde.SpawnType);

            if (_spawnSystem == SpawnSystem.None)
            {
                PrintError("You have set an invalid value in the config entry \"Spawn Type\". Unable to spawn hordes!");
                return false;
            }
            else if (_spawnSystem == SpawnSystem.SpawnsDatabase)
            {
                if (Spawns != null)
                {
                    if (string.IsNullOrEmpty(Configuration.Horde.SpawnFile))
                    {
                        PrintError("You have selected SpawnsDatabase as your method of spawning hordes, however you have not specified a spawn file. Unable to spawn hordes!");
                        return false;
                    }

                    object success = Spawns?.Call("LoadSpawnFile", Configuration.Horde.SpawnFile);
                    if (success is List<Vector3>)
                    {
                        _spawnPoints = success as List<Vector3>;
                        if (_spawnPoints.Count > 0)
                            return true;
                    }
                    PrintError("You have selected SpawnsDatabase as your method of spawning hordes, however the spawn file you have chosen is either invalid, or has no spawn points. Unable to spawn hordes!");
                    return false;
                }
                else PrintError("You have selected SpawnsDatabase as your method of spawning hordes, however SpawnsDatabase is not loaded on your server. Unable to spawn hordes!");
                return false;
            }
            
            return true;
        }

        private Vector3 GetSpawnPoint()
        {
            switch (_spawnSystem)
            {
                case SpawnSystem.None:
                    break;

                case SpawnSystem.SpawnsDatabase:
                {
                    if (Spawns == null)
                    {
                        PrintError("Tried getting a spawn point but SpawnsDatabase is null. Make sure SpawnsDatabase is still loaded to continue using custom spawn points");
                        break;
                    }

                    if (_spawnPoints == null || _spawnPoints.Count == 0)
                    {
                        PrintError("No spawnpoints have been loaded from the designated spawnfile. Defaulting to Rust spawns");
                        break;
                    }

                    Vector3 spawnPoint = _spawnPoints.GetRandom();
                    _spawnPoints.Remove(spawnPoint);
                    if (_spawnPoints.Count == 0)
                        _spawnPoints = (List<Vector3>)Spawns.Call("LoadSpawnFile", Configuration.Horde.SpawnFile);

                    return spawnPoint;
                }
            }
            
            float size = (World.Size / 2f) * 0.75f;

            for (int i = 0; i < 10; i++)
            {
                Vector2 randomInCircle = Random.insideUnitCircle * size;

                Vector3 position = new Vector3(randomInCircle.x, 0, randomInCircle.y);
                position.y = TerrainMeta.HeightMap.GetHeight(position);

                if (NavmeshSpawnPoint.Find(position, 25f, out position))
                {       
                    if (Physics.SphereCast(new Ray(position + (Vector3.up * 5f), Vector3.down), 10f, 10f, SPAWN_RAYCAST_MASK))
                        continue;

                    if (ContainsTopologyAtPoint(SPAWN_TOPOLOGY_MASK, position))
                        continue;

                    if (WaterLevel.GetWaterDepth(position, true, null) <= 0.01f)                    
                        return position;
                }
            }

            return ServerMgr.FindSpawnPoint().pos;
        }
        
        private void CreateMonumentHordeOrders()
        {
            int count = 0;
            GameObject[] allobjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (GameObject gobject in allobjects)
            {
                if (count >= Configuration.Horde.MaximumHordes)
                    break;

                if (gobject.name.Contains("autospawn/monument"))
                {
                    Transform tr = gobject.transform;
                    Vector3 position = tr.position;

                    if (position == Vector3.zero)
                        continue;

                    if (gobject.name.Contains("powerplant_1") && Configuration.Monument.Powerplant.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-30.8f, 0.2f, -15.8f)), Configuration.Monument.Powerplant);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("military_tunnel_1") && Configuration.Monument.Tunnels.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-7.4f, 13.4f, 53.8f)), Configuration.Monument.Tunnels);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("harbor_1") && Configuration.Monument.LargeHarbor.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(54.7f, 5.1f, -39.6f)), Configuration.Monument.LargeHarbor);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("harbor_2") && Configuration.Monument.SmallHarbor.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-66.6f, 4.9f, 16.2f)), Configuration.Monument.SmallHarbor);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("airfield_1") && Configuration.Monument.Airfield.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-12.4f, 0.2f, -28.9f)), Configuration.Monument.Airfield);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("trainyard_1") && Configuration.Monument.Trainyard.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(35.8f, 0.2f, -0.8f)), Configuration.Monument.Trainyard);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("water_treatment_plant_1") && Configuration.Monument.WaterTreatment.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(11.1f, 0.3f, -80.2f)), Configuration.Monument.WaterTreatment);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("warehouse") && Configuration.Monument.Warehouse.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(16.6f, 0.1f, -7.5f)), Configuration.Monument.Warehouse);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("satellite_dish") && Configuration.Monument.Satellite.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(18.6f, 6.0f, -7.5f)), Configuration.Monument.Satellite);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("sphere_tank") && Configuration.Monument.Dome.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-44.6f, 5.8f, -3.0f)), Configuration.Monument.Dome);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("radtown_small_3") && Configuration.Monument.Radtown.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-16.3f, -2.1f, -3.3f)), Configuration.Monument.Radtown);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("launch_site_1") && Configuration.Monument.LaunchSite.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(222.1f, 3.3f, 0.0f)), Configuration.Monument.LaunchSite);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("gas_station_1") && Configuration.Monument.GasStation.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-9.8f, 3.0f, 7.2f)), Configuration.Monument.GasStation);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("supermarket_1") && Configuration.Monument.Supermarket.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(5.5f, 0.0f, -20.5f)), Configuration.Monument.Supermarket);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_c") && Configuration.Monument.HQMQuarry.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(15.8f, 4.5f, -1.5f)), Configuration.Monument.HQMQuarry);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_a") && Configuration.Monument.SulfurQuarry.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-0.8f, 0.6f, 11.4f)), Configuration.Monument.SulfurQuarry);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_b") && Configuration.Monument.StoneQuarry.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-7.6f, 0.2f, 12.3f)), Configuration.Monument.StoneQuarry);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("junkyard_1") && Configuration.Monument.Junkyard.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-16.7f, 0.2f, 1.4f)), Configuration.Monument.Junkyard);
                        count++;
                        continue;
                    }
                }
            }

            foreach(ConfigData.MonumentSpawn.CustomSpawnPoints customSpawnPoint in Configuration.Monument.Custom)
            {
                if (customSpawnPoint.Enabled)
                {
                    Horde.SpawnOrder.Create(customSpawnPoint.Location, Configuration.Horde.InitialMemberCount, customSpawnPoint.HordeSize, customSpawnPoint.RoamDistance, customSpawnPoint.Profile);
                    count++;
                }
            }

            if (count < Configuration.Horde.MaximumHordes)
                CreateRandomHordes();
        }

        private void CreateRandomHordes()
        {
            int amountToCreate = Configuration.Horde.MaximumHordes - Horde.AllHordes.Count;
            for (int i = 0; i < amountToCreate; i++)
            {
                float roamDistance = Configuration.Horde.LocalRoam ? Configuration.Horde.RoamDistance : -1;
                string profile = Configuration.Horde.UseProfiles ? Configuration.HordeProfiles.Keys.ToArray().GetRandom() : string.Empty;

                Horde.SpawnOrder.Create(GetSpawnPoint(), Configuration.Horde.InitialMemberCount, Configuration.Horde.MaximumMemberCount, roamDistance, profile);
            }
        }
        #endregion

        #region Loadouts      
        private static LootContainer.LootSpawnSlot[] _defaultLootSpawns;

        private static PlayerInventoryProperties[] _defaultLoadouts;

        private static LootContainer.LootSpawnSlot[] DefaultLootSpawns
        {
            get
            {
                if (_defaultLootSpawns == null)
                {
                    ScarecrowNPC scarecrowNPC = GameManager.server.FindPrefab("assets/prefabs/npc/scarecrow/scarecrow.prefab").GetComponent<ScarecrowNPC>();
                    _defaultLootSpawns = scarecrowNPC.LootSpawnSlots;
                    _defaultLoadouts = scarecrowNPC.loadouts;
                }
                return _defaultLootSpawns;
            }
        }

        private static PlayerInventoryProperties[] DefaultLoadouts
        {
            get
            {
                if (_defaultLoadouts == null)
                {
                    ScarecrowNPC scarecrowNPC = GameManager.server.FindPrefab("assets/prefabs/npc/scarecrow/scarecrow.prefab").GetComponent<ScarecrowNPC>();
                    _defaultLootSpawns = scarecrowNPC.LootSpawnSlots;
                    _defaultLoadouts = scarecrowNPC.loadouts;                    
                }
                return _defaultLoadouts;
            }
        }

        private void ValidateLoadoutProfiles()
        {
            Puts("Validating horde profiles...");

            bool hasChanged = false;

            for (int i = Configuration.HordeProfiles.Count - 1; i >= 0; i--)
            {
                string key = Configuration.HordeProfiles.ElementAt(i).Key;

                for (int y = Configuration.HordeProfiles[key].Count - 1; y >= 0; y--)
                {
                    string loadoutId = Configuration.HordeProfiles[key][y];

                    if (!Configuration.Member.Loadouts.Any(x => x.LoadoutID == loadoutId))
                    {
                        Puts($"Loadout profile {loadoutId} does not exist. Removing from config");
                        Configuration.HordeProfiles[key].Remove(loadoutId);
                        hasChanged = true;
                    }
                }

                if (Configuration.HordeProfiles[key].Count <= 0)
                {
                    Puts($"Horde profile {key} does not have any valid loadouts. Removing from config");
                    Configuration.HordeProfiles.Remove(key);
                    hasChanged = true;
                }
            }

            if (hasChanged)
                SaveConfig();
        }
        #endregion

        #region Spawning
        private static ZombieNPC _reference;

        private static ZombieNPC Reference
        {
            get
            {
                if (_reference == null)
                {
                    const string SCIENTIST_PREFAB = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab";

                    GameObject gameObject = Instantiate.GameObject(GameManager.server.FindPrefab(SCIENTIST_PREFAB), Vector3.zero, Quaternion.identity);
                    gameObject.name = SCIENTIST_PREFAB;

                    gameObject.SetActive(false);

                    ScientistNPC scientistNPC = gameObject.GetComponent<ScientistNPC>();
                    ScientistBrain scientistBrain = gameObject.GetComponent<ScientistBrain>();
                    NPCPlayerNavigator scientistNavigator = gameObject.GetComponent<NPCPlayerNavigator>();

                    ZombieNPC zombieNPC = gameObject.AddComponent<ZombieNPC>();
                    ZombieNPCBrain zombieNPCBrain = gameObject.AddComponent<ZombieNPCBrain>();
                    ZombieNavigator zombieNavigator = gameObject.AddComponent<ZombieNavigator>();

                    CopySerializeableFields<NPCPlayer>(scientistNPC, zombieNPC);
                    CopySerializeableFields<NPCPlayerNavigator>(scientistNavigator, zombieNavigator);

                    zombieNPC.enableSaving = false;

                    zombieNPCBrain.UseQueuedMovementUpdates = scientistBrain.UseQueuedMovementUpdates;
                    zombieNPCBrain.AllowedToSleep = Configuration.Member.EnableDormantSystem;

                    zombieNPCBrain.DefaultDesignSO = scientistBrain.DefaultDesignSO;
                    zombieNPCBrain.Designs = new List<AIDesignSO>(scientistBrain.Designs);

                    zombieNPCBrain.InstanceSpecificDesign = scientistBrain.InstanceSpecificDesign;
                    zombieNPCBrain.CheckLOS = scientistBrain.CheckLOS;
                    zombieNPCBrain.UseAIDesign = true;
                    zombieNPCBrain.Pet = false;

                    scientistBrain._baseEntity = scientistNPC;

                    UnityEngine.Object.DestroyImmediate(scientistNavigator);
                    UnityEngine.Object.DestroyImmediate(scientistBrain, true);
                    UnityEngine.Object.DestroyImmediate(scientistNPC, true);

                    _reference = zombieNPC;
                }

                return _reference;
            }
        }
        private static ZombieNPC InstantiateEntity(Vector3 position)
        {
            ZombieNPC zombieNPC = UnityEngine.Object.Instantiate<ZombieNPC>(Reference, position, Quaternion.identity);
            zombieNPC.name = Reference.name;

            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(zombieNPC.gameObject, Rust.Server.EntityScene);
            
            zombieNPC.gameObject.SetActive(true);

            return zombieNPC;
        }

        private static void CopySerializeableFields<T>(T src, T dst)
        {
            FieldInfo[] srcFields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (FieldInfo field in srcFields)
            {
                object value = field.GetValue(src);
                field.SetValue(dst, value);
            }
        }

        private static class NavmeshSpawnPoint
        {
            private static NavMeshHit navmeshHit;

            private static RaycastHit raycastHit;

            private static readonly Collider[] _buffer = new Collider[256];

            private const int WORLD_LAYER = 65536;

            public static bool Find(Vector3 targetPosition, float maxDistance, out Vector3 position)
            {
                for (int i = 0; i < 10; i++)
                {
                    position = i == 0 ? targetPosition : targetPosition + (Random.onUnitSphere * maxDistance);
                    if (NavMesh.SamplePosition(position, out navmeshHit, maxDistance, 1))
                    {
                        if (IsInRockPrefab(navmeshHit.position))
                            continue;

                        if (IsNearWorldCollider(navmeshHit.position))
                            continue;

                        if (navmeshHit.position.y < TerrainMeta.WaterMap.GetHeight(navmeshHit.position))
                            continue;

                        position = navmeshHit.position;
                        return true;
                    }
                }
                position = default(Vector3);
                return false;
            }

            private static bool IsInRockPrefab(Vector3 position)
            {
                Physics.queriesHitBackfaces = true;

                bool isInRock = Physics.Raycast(position, Vector3.up, out raycastHit, 20f, WORLD_LAYER, QueryTriggerInteraction.Ignore) &&
                                BLOCKED_COLLIDERS.Any(s => raycastHit.collider?.gameObject?.name.Contains(s, CompareOptions.OrdinalIgnoreCase) ?? false);

                Physics.queriesHitBackfaces = false;

                return isInRock;
            }

            private static bool IsNearWorldCollider(Vector3 position)
            {
                Physics.queriesHitBackfaces = true;

                int count = Physics.OverlapSphereNonAlloc(position, 2f, _buffer, WORLD_LAYER, QueryTriggerInteraction.Ignore);
                Physics.queriesHitBackfaces = false;

                int removed = 0;
                for (int i = 0; i < count; i++)
                {
                    if (ACCEPTED_COLLIDERS.Any(s => _buffer[i].gameObject.name.Contains(s, CompareOptions.OrdinalIgnoreCase)))
                        removed++;
                }

                return count - removed > 0;
            }

            private static readonly string[] ACCEPTED_COLLIDERS = new string[] { "road", "carpark", "rocket_factory", "range", "train_track", "runway", "_grounds", "concrete_slabs", "lighthouse", "cave", "office", "walkways", "sphere", "tunnel", "industrial", "junkyard" };

            private static readonly string[] BLOCKED_COLLIDERS = new string[] { "rock", "cliff", "junk", "range", "invisible" };
        }
        #endregion
        #endregion

        public class Horde
        {
            public static List<Horde> AllHordes = new List<Horde>();

            private static readonly Spatial.Grid<Horde> HordeGrid = new Spatial.Grid<Horde>(32, 8096f);

            private static readonly Horde[] HordeGridQueryResults = new Horde[4];

            private static readonly BasePlayer[] PlayerVicinityQueryResults = new BasePlayer[32];

            public List<ZombieNPC> members;

            //public bool DebugMode = false;

            public readonly Vector3 InitialPosition;
            public readonly bool IsLocalHorde;
            public readonly float MaximumRoamDistance;

            private readonly int initialMemberCount;
            private readonly int maximumMemberCount;

            private readonly string hordeProfile;

            private float nextUpdateTime;
            private float nextSeperationCheckTime;
            private float nextGrowthTime;
            private float nextMergeTime;
            private float nextSleepTime;

            internal bool isDespawning;

            private const float HORDE_UPDATE_RATE = 1f;
            private const float SEPERATION_CHECK_RATE = 10f;
            private const float MERGE_CHECK_RATE = 10f;
            private const float SLEEP_CHECK_RATE = 5f;

            public ZombieNPC Leader { get; private set; }

            public bool IsSleeping { get; private set; }

            public Vector3 CentralLocation { get; private set; }

            public bool HordeOnAlert { get; private set; }

            public int MemberCount => members.Count;

            public static bool Create(SpawnOrder spawnOrder)
            {
                Horde horde = new Horde(spawnOrder);

                for (int i = 0; i < spawnOrder.InitialMemberCount; i++)
                    horde.SpawnMember(spawnOrder.Position);

                if (horde.members.Count == 0)
                {
                    horde.Destroy();
                    return false;
                }

                AllHordes.Add(horde);

                horde.CentralLocation = horde.CalculateCentralLocation();

                HordeGrid.Add(horde, horde.CentralLocation.x, horde.CentralLocation.z);

                return true;
            }

            public Horde(SpawnOrder spawnOrder)
            {
                members = Pool.GetList<ZombieNPC>();

                InitialPosition = CentralLocation = spawnOrder.Position;
                IsLocalHorde = spawnOrder.MaximumRoamDistance > 0;
                MaximumRoamDistance = spawnOrder.MaximumRoamDistance;
                initialMemberCount = spawnOrder.InitialMemberCount;
                maximumMemberCount = spawnOrder.MaximumMemberCount;
                hordeProfile = spawnOrder.HordeProfile;

                nextSeperationCheckTime = Time.time + SEPERATION_CHECK_RATE;
                nextGrowthTime = Time.time + Configuration.Horde.GrowthRate;
                nextMergeTime = Time.time + MERGE_CHECK_RATE;
                nextSleepTime = Time.time + SLEEP_CHECK_RATE + Random.Range(1f, 5f);
            }
                        
            public void Update()
            {
                if (members == null || members.Count == 0)
                    return;

                if (Time.time < nextUpdateTime)
                    return;

                nextUpdateTime = Time.time + HORDE_UPDATE_RATE;

                CentralLocation = CalculateCentralLocation();

                if (Configuration.Member.EnableDormantSystem)
                    DoSleepChecks();

                //if (DebugMode)
                //{
                //    Debug.Log($"Horde Update ({CentralLocation}). Sleeping? {IsSleeping}");
                //    for (int i = 0; i < members.Count; i++)
                //    {
                //        ZombieNPC zombieNPC = members[i];
                //        Debug.Log($"Member #{i} state {zombieNPC.Brain.CurrentState.StateType} (Is Leader? {zombieNPC.IsGroupLeader}");
                //    }
                //}

                if (IsSleeping)
                    return;

                HordeGrid.Move(this, CentralLocation.x, CentralLocation.z);

                TryMergeHordes();

                TryGrowHorde();

                TryCongregateHorde();

                MoveRoamersTowardsTarget();
            }

            private void MoveRoamersTowardsTarget()
            {
                BaseEntity target;

                HordeOnAlert = AnyHasTarget(out target);

                if (Leader != null && !Leader.IsDestroyed && target != null && !target.IsDestroyed)
                    SetLeaderRoamTarget(target.transform.position);
            }

            private void TryCongregateHorde()
            {
                if (Time.time > nextSeperationCheckTime)
                {
                    nextSeperationCheckTime = Time.time + SEPERATION_CHECK_RATE;

                    if (GetLargestSeperation() > 30f)
                    {
                        //if (DebugMode)
                        //    Debug.Log($"Horde is seperated, attempt re-group. Leader state {Leader.CurrentState}");

                        if (Leader.CurrentState <= AIState.Roam)
                            SetLeaderRoamTarget(CentralLocation);

                        for (int i = 0; i < members.Count; i++)
                            members[i].IsAlert = true;
                    }
                }
            }

            public void RegisterInterestInTarget(ZombieNPC interestedMember, BaseEntity baseEntity)
            {
                if (baseEntity == null || members == null)
                    return;

                //if (DebugMode)
                //    Debug.Log($"Register interest in {baseEntity.ShortPrefabName} ({baseEntity.transform.position}).");

                for (int i = 0; i < members.Count; i++)
                {
                    ZombieNPC hordeMember = members[i];
                    if (hordeMember == null || hordeMember.IsDestroyed || interestedMember == hordeMember)
                        continue;

                    hordeMember.Brain.Senses.Memory.SetKnown(baseEntity, hordeMember, null);
                }

                if (Leader != null && !Leader.IsDestroyed && !Leader.HasTarget)                                  
                    SetLeaderRoamTarget(baseEntity.transform.position);
            }

            public bool HasTarget()
            {
                if (members != null)
                {
                    for (int i = 0; i < members.Count; i++)
                    {
                        ZombieNPC hordeMember = members[i];
                        if (hordeMember.HasTarget)
                            return true;
                    }
                }
                return false;
            }

            public bool AnyHasTarget(out BaseEntity target)
            {
                if (members != null)
                {
                    for (int i = 0; i < members.Count; i++)
                    {
                        ZombieNPC hordeMember = members[i];
                        if (hordeMember.HasTarget)
                        {
                            target = hordeMember.CurrentTarget;
                            return true;
                        }
                    }
                }
                target = null;
                return false;
            }

            public void ResetRoamTarget()
            {
                if (members == null)
                    return;

                //if (DebugMode)
                //    Debug.Log("Reset roam target");

                for (int i = 0; i < members.Count; i++)
                {
                    ZombieNPC hordeMember = members[i];
                    if (hordeMember.IsGroupLeader)
                        continue;

                    hordeMember.ResetRoamState();
                }
            }

            public void SetLeaderRoamTarget(Vector3 position)
            {
                //if (DebugMode)
                //    Debug.Log($"Set leader roam target {position}");

                if (Leader != null && !Leader.IsDestroyed)
                    Leader.SetRoamTargetOverride(position);

                ResetRoamTarget();
            }

            public Vector3 GetLeaderDestination()
            {
                if (Leader == null || Leader.IsDestroyed || Leader.Brain == null || Leader.Brain.Events == null)
                    return CentralLocation;

                return Leader.Brain.Navigator.Destination;
            }
            
            public void OnMemberKilled(ZombieNPC zombieNPC, BaseEntity initiator)
            {
                if (zombieNPC == null)
                    return;

                if (members == null || !members.Contains(zombieNPC))
                    return;

                members.Remove(zombieNPC);

                if (members.Count == 0)                
                    Destroy();                
                else
                {
                    if (zombieNPC.IsGroupLeader)
                    {
                        Leader = members.GetRandom();
                        Leader.IsGroupLeader = true;
                    }

                    if ((initiator is BasePlayer && Leader.CanTargetBasePlayer(initiator as BasePlayer)) || (initiator is BaseNpc && Leader.CanTargetEntity(initiator)))
                        RegisterInterestInTarget(null, initiator);
                }
            }

            public void OnPlayerKilled(BasePlayer player)
            {
                if (Configuration.Horde.CreateOnDeath && MemberCount < maximumMemberCount)
                {
                    Vector3 position;
                    if (NavmeshSpawnPoint.Find(player.transform.position, 10f, out position))
                        SpawnMember(position);
                }
            }

            public void SpawnMember(Vector3 position)
            { 
                ZombieNPC zombieNPC = InstantiateEntity(position);
                zombieNPC.SetHordeProfile(hordeProfile);
                zombieNPC.Horde = this;

                zombieNPC.Spawn();
                members.Add(zombieNPC);

                if (members.Count == 1)
                {
                    Leader = zombieNPC;
                    Leader.IsGroupLeader = true;
                }
                else zombieNPC.Invoke(zombieNPC.OnInitialSpawn, 1f); 
            }

            public void Destroy(bool permanent = false, bool killNpcs = true)
            {
                isDespawning = true;

                if (killNpcs)
                {
                    for (int i = members.Count - 1; i >= 0; i--)
                    {
                        ZombieNPC zombieNPC = members[i];
                        if (zombieNPC != null && !zombieNPC.IsDestroyed)
                            zombieNPC.Kill();
                    }
                }

                members.Clear();
                Pool.FreeList(ref members);

                HordeGrid.Remove(this);

                AllHordes.Remove(this);

                if (!permanent && AllHordes.Count <= Configuration.Horde.MaximumHordes)
                    Instance.timer.In(Configuration.Horde.RespawnTime, () => SpawnOrder.Create(this));
            }

            private Vector3 CalculateCentralLocation()
            {
                Vector3 location = Vector3.zero;

                if (members == null || members.Count == 0)
                    return location;

                int count = 0;
                for (int i = 0; i < members.Count; i++)
                {
                    ZombieNPC zombieNPC = members[i];

                    if (zombieNPC == null || zombieNPC.IsDestroyed)
                        continue;

                    location += zombieNPC.Transform.position;
                    count++;
                }

                return location /= count;
            }

            private float GetLargestSeperation()
            {
                float distance = 0;

                if (members != null)
                {
                    for (int i = 0; i < members.Count; i++)
                    {
                        ZombieNPC zombieNPC = members[i];
                        if (zombieNPC != null && !zombieNPC.IsDestroyed)
                        {
                            float d = Vector3.Distance(zombieNPC.Transform.position, CentralLocation);
                            if (d > distance)
                                distance = d;
                        }
                    }
                }

                return distance;
            }

            private void TryGrowHorde()
            {
                if (Configuration.Horde.GrowthRate <= 0 || nextGrowthTime < Time.time)
                {
                    if (MemberCount < maximumMemberCount)                    
                        SpawnMember(members.GetRandom().Transform.position);                    

                    nextGrowthTime = Time.time + Configuration.Horde.GrowthRate;
                }
            }

            #region Horde Merging
            private static bool HordeMergeQuery(Horde horde) => horde.MemberCount < horde.maximumMemberCount;

            private void TryMergeHordes()
            {
                if (!Configuration.Horde.MergeHordes || nextMergeTime > Time.time)
                    return;

                nextMergeTime = Time.time + MERGE_CHECK_RATE;

                if (members == null || MemberCount >= maximumMemberCount)
                    return;
                
                int results = HordeGrid.Query(CentralLocation.x, CentralLocation.z, 30f, HordeGridQueryResults, HordeMergeQuery);

                if (results > 1)
                {
                    int amountToMerge = maximumMemberCount - members.Count;

                    for (int i = 0; i < results; i++)
                    {
                        Horde otherHorde = HordeGridQueryResults[i];

                        if (otherHorde == this)
                            continue;

                        if (MemberCount >= maximumMemberCount || otherHorde.members == null)
                            break;

                        if (amountToMerge >= otherHorde.members.Count)
                        {
                            for (int y = 0; y < otherHorde.members.Count; y++)
                            {
                                ZombieNPC zombieNPC = otherHorde.members[y];
                                members.Add(zombieNPC);
                                zombieNPC.Horde = this;
                                zombieNPC.OnInitialSpawn();
                            }

                            otherHorde.members.Clear();
                            otherHorde.Destroy();
                        }
                        else
                        {
                            for (int y = 0; y < amountToMerge; y++)
                            {
                                if (otherHorde.members.Count > 0)
                                {
                                    ZombieNPC zombieNPC = otherHorde.members[otherHorde.MemberCount - 1];

                                    members.Add(zombieNPC);

                                    zombieNPC.Horde = this;
                                    zombieNPC.OnInitialSpawn();

                                    otherHorde.members.Remove(zombieNPC);
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            #region Sleeping
            private void DoSleepChecks()
            {
                if (Time.time >= nextSleepTime)
                {
                    nextSleepTime = Time.time + SLEEP_CHECK_RATE + Random.Range(1f, 5f);

                    int count = BaseEntity.Query.Server.GetPlayersInSphere(CentralLocation, AiManager.ai_to_player_distance_wakeup_range, PlayerVicinityQueryResults, HordeSleepPlayerFilter);

                    if (count > 0)
                    {
                        if (IsSleeping)
                            SetSleeping(false);
                    }
                    else
                    {
                        if (!IsSleeping)
                            SetSleeping(true);
                    }
                }              
            }

            private void SetSleeping(bool sleep)
            {
                if (members == null)
                    return;

                for (int i = 0; i < members.Count; i++)
                {
                    ZombieNPC zombieNPC = members[i];
                    if (zombieNPC == null || zombieNPC.Brain == null)
                        continue;

                    if (zombieNPC.Brain.sleeping == sleep)
                        continue;

                    zombieNPC.Brain.SetSleeping(sleep);
                }

                IsSleeping = sleep;
            }

            public void ForceWakeFromSleep()
            {
                if (!IsSleeping)
                    return;

                SetSleeping(false);
            }

            private static bool HordeSleepPlayerFilter(BaseEntity entity)
            {
                BasePlayer basePlayer = entity as BasePlayer;
                if (basePlayer == null || !basePlayer.IsConnected)
                    return false;

                if (basePlayer is ZombieNPC)
                    return false;

                if (Configuration.Member.IgnoreSleepers && basePlayer.IsSleeping())
                    return false;

                return true;
            }
            #endregion

            public int GetMemberIndex(ZombieNPC zombieNPC) => members.IndexOf(zombieNPC);

            public class SpawnOrder
            {
                public Vector3 Position { get; private set; }

                public int InitialMemberCount { get; private set; }

                public int MaximumMemberCount { get; private set; }

                public float MaximumRoamDistance { get; private set; }

                public string HordeProfile { get; private set; }

                public SpawnOrder(Vector3 position, int initialMemberCount, int maximumMemberCount, float maximumRoamDistance, string hordeProfile)
                {
                    this.Position = position;
                    this.InitialMemberCount = initialMemberCount;
                    this.MaximumMemberCount = maximumMemberCount;
                    this.MaximumRoamDistance = maximumRoamDistance;
                    this.HordeProfile = hordeProfile;
                }

                #region Static
                private static Queue<SpawnOrder> _spawnOrders = new Queue<SpawnOrder>();

                private static Coroutine _spawnRoutine;

                private static Coroutine _despawnRoutine;

                private static bool _isSpawning;

                private static bool _isDespawning;

                public static SpawnState State;

                static SpawnOrder()
                {
                    State = Configuration.TimedSpawns.Enabled ? (ShouldSpawn() ? SpawnState.Spawn : SpawnState.Despawn) : SpawnState.Spawn;

                    if (Configuration.TimedSpawns.Enabled)
                        StartTimer();
                }

                internal static void Create(Vector3 position, int initialMemberCount, int maximumMemberCount, float maximumRoamDistance, string hordeProfile)
                {
                    if (NavmeshSpawnPoint.Find(position, 10f, out position))
                    {
                        _spawnOrders.Enqueue(new SpawnOrder(position, initialMemberCount, maximumMemberCount, maximumRoamDistance, hordeProfile));

                        if (!_isSpawning && State == SpawnState.Spawn)
                            DequeueAndSpawn();
                    }
                }

                internal static void Create(Horde horde)
                {
                    Vector3 position;
                    if (NavmeshSpawnPoint.Find(horde.IsLocalHorde ? horde.InitialPosition : Instance.GetSpawnPoint(), 10f, out position))
                    {
                        _spawnOrders.Enqueue(new SpawnOrder(position, horde.initialMemberCount, horde.maximumMemberCount, horde.IsLocalHorde ? horde.MaximumRoamDistance : -1, horde.hordeProfile));

                        if (!_isSpawning && State == SpawnState.Spawn)
                            DequeueAndSpawn();
                    }
                }

                internal static void Create(Vector3 position, ConfigData.MonumentSpawn.MonumentSettings settings)
                {
                    if (NavmeshSpawnPoint.Find(position, 10f, out position))
                    {
                        _spawnOrders.Enqueue(new SpawnOrder(position, Configuration.Horde.InitialMemberCount, settings.HordeSize, settings.RoamDistance, settings.Profile));

                        if (!_isSpawning && State == SpawnState.Spawn)
                            DequeueAndSpawn();
                    }
                }

                private static void DequeueAndSpawn()
                {
                    _spawnRoutine = ServerMgr.Instance.StartCoroutine(ProcessSpawnOrders());
                }

                private static void QueueAndDespawn()
                {
                    _despawnRoutine = ServerMgr.Instance.StartCoroutine(ProcessDespawn());
                }

                internal static void StopSpawning()
                {
                    if (_spawnRoutine != null)
                        ServerMgr.Instance.StopCoroutine(_spawnRoutine);

                    _isSpawning = false;
                }

                internal static void StopDespawning()
                {
                    if (_despawnRoutine != null)
                        ServerMgr.Instance.StopCoroutine(_despawnRoutine);

                    _isDespawning = false;
                }

                private static IEnumerator ProcessSpawnOrders()
                {
                    if (_spawnOrders.Count == 0)
                        yield break;

                    _isSpawning = true;

                    RESTART:
                    if (_isDespawning)
                        StopDespawning();

                    while (AllHordes.Count > Configuration.Horde.MaximumHordes)
                        yield return CoroutineEx.waitForSeconds(10f);

                    SpawnOrder spawnOrder = _spawnOrders.Dequeue();

                    if (spawnOrder != null)
                        Horde.Create(spawnOrder);

                    if (_spawnOrders.Count > 0)
                    {
                        yield return CoroutineEx.waitForSeconds(3f);
                        goto RESTART;
                    }

                    _isSpawning = false;
                }

                private static IEnumerator ProcessDespawn()
                {
                    _isDespawning = true;

                    if (_isSpawning)
                        StopSpawning();

                    while (AllHordes.Count > 0)
                    {
                        Horde horde = AllHordes.GetRandom();
                        if (!horde.HasTarget())
                        {
                            Create(horde);
                            horde.Destroy(true, true);
                        }

                        yield return CoroutineEx.waitForSeconds(3f);
                    }

                    _isDespawning = false;
                }

                internal static void OnUnload()
                {
                    if (_spawnRoutine != null)
                        ServerMgr.Instance.StopCoroutine(_spawnRoutine);

                    if (_despawnRoutine != null)
                        ServerMgr.Instance.StopCoroutine(_despawnRoutine);

                    _isDespawning = false;
                    _isSpawning = false;

                    State = SpawnState.Spawn;

                    _spawnOrders.Clear();
                }

                private static void StartTimer() => Instance.timer.In(1f, CheckTime);

                private static bool ShouldSpawn()
                {
                    float currentTime = TOD_Sky.Instance.Cycle.Hour;

                    if (Configuration.TimedSpawns.Start > Configuration.TimedSpawns.End)
                        return currentTime > Configuration.TimedSpawns.Start || currentTime < Configuration.TimedSpawns.End;
                    else return currentTime > Configuration.TimedSpawns.Start && currentTime < Configuration.TimedSpawns.End;
                }

                private static void CheckTime()
                {
                    if (ShouldSpawn())
                    {
                        if (State == SpawnState.Despawn)
                        {
                            State = SpawnState.Spawn;
                            StopDespawning();
                            DequeueAndSpawn();
                        }
                    }
                    else
                    {
                        if (State == SpawnState.Spawn)
                        {
                            State = SpawnState.Despawn;

                            if (Configuration.TimedSpawns.Despawn)
                            {
                                StopSpawning();
                                QueueAndDespawn();
                            }
                        }
                    }

                    StartTimer();
                }
                #endregion
            }
        }

        public class ZombieNPC : NPCPlayer, IAIAttack, IAISenses, IThinker
        {
            private float targetAimedDuration;

            private float lastAimSetTime;

            private float lastThinkTime;


            private bool isEquippingWeapon;

            private float nextThrowTime;

            private float abortDelta;

            private bool isThrowingExplosive;

            private bool abortThrowingExplosive;


            private bool lightsOn;

            public Transform Transform { get; private set; }

            public Horde Horde { get; internal set; }

            public ZombieNPCBrain Brain { get; private set; }

            public AIState CurrentState { get; internal set; } = AIState.Idle;

            public bool IsGroupLeader { get; internal set; }


            public BaseEntity CurrentTarget { get; private set; }

            public bool HasTarget { get { return CurrentTarget != null; } }

            public bool TargetUnreachable { get; internal set; }


            public AttackEntity CurrentWeapon { get; private set; }

            public Item ThrowableExplosive { get; private set; }



            public ConfigData.MemberOptions.Loadout Loadout { get; private set; }

            public Vector3 DestinationOverride { get; internal set; }

            public bool IsAlert { get; internal set; }


            private const int LOS_BLOCKING_LAYER = 1218519041;

            private const int AREA_MASK = 1;

            private const int AGENT_TYPE_ID = -1372625422;

            #region Horde
            public void SetHordeProfile(string hordeProfile)
            {
                if (!string.IsNullOrEmpty(hordeProfile) && Configuration.HordeProfiles.ContainsKey(hordeProfile))
                {
                    string loadoutId = Configuration.HordeProfiles[hordeProfile].GetRandom();
                    Loadout = Configuration.Member.Loadouts.FirstOrDefault(x => x.LoadoutID == loadoutId);
                }

                if (Loadout == null)
                    Loadout = Configuration.Member.Loadouts.GetRandom();
            }

            public void SetRoamTargetOverride(Vector3 position)
            {
                if (IsGroupLeader)
                {                    
                    DestinationOverride = position;
                    ResetRoamState();                    
                }
            }

            public void ResetRoamState() 
            {
                if (Brain != null && CurrentState == AIState.Roam)
                {
                    Brain.states[AIState.Roam].StateEnter();
                    //Brain.SwitchToState(AIState.Idle, IDLE_STATE_CONTAINER);
                }
            }

            public void OnInitialSpawn()
            {
                if (IsGroupLeader)
                    return;

                if (Horde.Leader.HasTarget)                
                    Brain.Senses.Memory.SetKnown(Horde.Leader.CurrentTarget, this, null);
            }
            #endregion

            #region BaseNetworkable
            public override void DestroyShared()
            {
                base.DestroyShared();
                AIThinkManager.Remove(this);
            }

            public override void ServerInit()
            {
                faction = Faction.Horror;

                loadouts = null;

                if (NavAgent == null)
                    NavAgent = GetComponent<NavMeshAgent>();

                NavAgent.areaMask = AREA_MASK;

                NavAgent.agentTypeID = AGENT_TYPE_ID;

                GetComponent<BaseNavigator>().DefaultArea = "Walkable";

                base.ServerInit();

                Invoke(DelayedSetDisplayname, 1f);

                Loadout.GiveToPlayer(this);

                FindThrowableExplosive();

                Brain = GetComponent<ZombieNPCBrain>();

                Transform = transform;

                AIThinkManager.Add(this);

                InvokeRepeating(LightCheck, 1f, 30f);
            }

            private void DelayedSetDisplayname() => displayName = Loadout.Names.GetRandom();
            #endregion

            #region BaseEntity
            public override float BoundsPadding() => (0.1f * Brain.Navigator.Agent.speed) + 0.1f;

            public override float StartHealth() => Loadout.Vitals.Health;

            public override float StartMaxHealth() => Loadout.Vitals.Health;

            public override float MaxHealth() => Loadout.Vitals.Health;

            public override void OnSensation(Sensation sensation)
            {
                if (!Configuration.Horde.UseSenses || sensation.Type == SensationType.Explosion) 
                    return; 

                if (sensation.UsedEntity is TimedExplosive && sensation.Type == SensationType.ThrownWeapon) 
                    return;

                if (sensation.Initiator != null)
                {
                    if (sensation.Initiator is ZombieNPC) 
                        return;                    

                    Brain.Senses.Memory.SetKnown(sensation.Initiator, this, null);
                }
                
                if (IsGroupLeader && CurrentState <= AIState.Roam && !HasTarget)
                    Horde.SetLeaderRoamTarget(sensation.Position);

                IsAlert = true;
            }

            public override void OnAttacked(HitInfo info)
            {
                base.OnAttacked(info);
                Horde.ForceWakeFromSleep();
            }
            #endregion

            #region BasePlayer
            public override bool IsNpc => false;

            public override BaseNpc.AiStatistics.FamilyEnum Family => BaseNpc.AiStatistics.FamilyEnum.Zombie;

            public override string Categorize() => "Zombie";

            public override bool ShouldDropActiveItem() => false;
            #endregion

            #region BaseCombatEntity
            public override bool IsHostile() => true;
            #endregion

            #region NPCPlayer
            public override bool IsLoadBalanced() => true;

            public override void EquipWeapon()
            {
                if (!isEquippingWeapon)
                    StartCoroutine(EquipItem());
            }
            
            private void FindThrowableExplosive()
            {
                for (int i = 0; i < inventory.containerBelt.itemList.Count; i++)
                {
                    Item item = inventory.containerBelt.GetSlot(i);
                    if (item != null && item.GetHeldEntity() is ThrownWeapon)
                    {
                        ThrowableExplosive = item;
                        break;
                    }
                }
            }

            private IEnumerator EquipItem(Item slot = null)
            {
                if (inventory != null && inventory.containerBelt != null)
                {
                    isEquippingWeapon = true;

                    if (slot == null)
                    {
                        for (int i = 0; i < inventory.containerBelt.itemList.Count; i++)
                        {
                            Item item = inventory.containerBelt.GetSlot(i);
                            if (item != null && item.GetHeldEntity() is AttackEntity)
                            {
                                slot = item;
                                break;
                            }
                        }
                    }

                    if (slot != null)
                    {
                        if (CurrentWeapon != null)
                        {
                            CurrentWeapon.SetHeld(false);
                            CurrentWeapon = null;

                            SendNetworkUpdate(NetworkQueue.Update);
                            inventory.UpdatedVisibleHolsteredItems();
                        }

                        yield return CoroutineEx.waitForSeconds(0.5f);

                        UpdateActiveItem(slot.uid);

                        HeldEntity heldEntity = slot.GetHeldEntity() as HeldEntity;
                        if (heldEntity != null)
                        {                                                        
                            if (heldEntity is AttackEntity)
                                (heldEntity as AttackEntity).TopUpAmmo();

                            if (heldEntity is Chainsaw)
                                ServerStartChainsaw(heldEntity as Chainsaw);                            
                        }

                        CurrentWeapon = heldEntity as AttackEntity;
                    }

                    isEquippingWeapon = false;
                }
            }

            public override float GetAimConeScale() => Loadout.AimConeScale;

            public float GetAimSwayScalar() => 1f - Mathf.InverseLerp(1f, 3f, Time.time - lastGunShotTime);
            
            public override Vector3 GetAimDirection()
            {
                if (Brain != null && Brain.Navigator != null && Brain.Navigator.IsOverridingFacingDirection)                
                    return Brain.Navigator.FacingDirectionOverride;
                return base.GetAimDirection();
            }

            public override void SetAimDirection(Vector3 newAim)
            {
                if (newAim == Vector3.zero)                
                    return;
                
                float num = Time.time - lastAimSetTime;
                lastAimSetTime = Time.time;

                AttackEntity attackEntity = CurrentWeapon;
                if (attackEntity)                
                    newAim = attackEntity.ModifyAIAim(newAim, GetAimSwayScalar());
                
                if (isMounted)
                {
                    BaseMountable baseMountable = GetMounted();
                    Vector3 eulerAngles = baseMountable.transform.eulerAngles;
                    Quaternion aimRotation = Quaternion.Euler(Quaternion.LookRotation(newAim, baseMountable.transform.up).eulerAngles);

                    Vector3 lookRotation = Quaternion.LookRotation(transform.InverseTransformDirection(aimRotation * Vector3.forward), transform.up).eulerAngles;
                    lookRotation = BaseMountable.ConvertVector(lookRotation);

                    Quaternion clampedRotation = Quaternion.Euler(Mathf.Clamp(lookRotation.x, baseMountable.pitchClamp.x, baseMountable.pitchClamp.y), 
                                                                  Mathf.Clamp(lookRotation.y, baseMountable.yawClamp.x, baseMountable.yawClamp.y), 
                                                                  eulerAngles.z);

                    newAim = BaseMountable.ConvertVector(Quaternion.LookRotation(transform.TransformDirection(clampedRotation * Vector3.forward), transform.up).eulerAngles);
                }
                else
                {
                    BaseEntity parentEntity = GetParentEntity();
                    if (parentEntity)
                    {
                        Vector3 aimDirection = parentEntity.transform.InverseTransformDirection(newAim);
                        Vector3 forward = new Vector3(newAim.x, aimDirection.y, newAim.z);

                        eyes.rotation = Quaternion.Lerp(eyes.rotation, Quaternion.LookRotation(forward, parentEntity.transform.up), num * 25f);
                        viewAngles = eyes.bodyRotation.eulerAngles;
                        ServerRotation = eyes.bodyRotation;
                        return;
                    }
                }

                eyes.rotation = (isMounted ? Quaternion.Slerp(eyes.rotation, Quaternion.Euler(newAim), num * 70f) : Quaternion.Lerp(eyes.rotation, Quaternion.LookRotation(newAim, transform.up), num * 25f));
                viewAngles = eyes.rotation.eulerAngles;
                ServerRotation = eyes.rotation;
            }

            public override void Hurt(HitInfo info)
            {
                if (info.InitiatorPlayer is ZombieNPC)
                    return;

                if (info.Initiator is ResourceEntity)
                {
                    info.damageTypes.ScaleAll(0);
                    return;
                }

                if (Configuration.Member.HeadshotKills && info.isHeadshot)                
                    info.damageTypes.ScaleAll(1000);
                
                base.Hurt(info);

                BaseEntity initiator = info.Initiator;

                if (initiator != null && !initiator.EqualNetID(this)) 
                {
                    if ((initiator is BasePlayer && CanTargetBasePlayer(initiator as BasePlayer)) || (initiator is BaseNpc && CanTargetEntity(initiator)))                    
                        Horde.RegisterInterestInTarget(this, initiator);                    
                }
            }

            public override BaseCorpse CreateCorpse()
            {
                NPCPlayerCorpse npcplayerCorpse = DropCorpse("assets/prefabs/npc/murderer/murderer_corpse.prefab") as NPCPlayerCorpse;
                if (npcplayerCorpse)
                {
                    RemoveItemsOnDeath();
                    ResetModifiedWeaponRange();

                    npcplayerCorpse.transform.position = npcplayerCorpse.transform.position + Vector3.down * NavAgent.baseOffset;
                    npcplayerCorpse.SetLootableIn(2f);

                    npcplayerCorpse.SetFlag(Flags.Reserved5, HasPlayerFlag(PlayerFlags.DisplaySash), false, true);
                    npcplayerCorpse.SetFlag(Flags.Reserved2, true, false, true);

                    npcplayerCorpse.TakeFrom(new ItemContainer[]
                    {
                        inventory.containerMain,
                        inventory.containerWear,
                        inventory.containerBelt
                    });

                    npcplayerCorpse.playerName = displayName;
                    npcplayerCorpse.playerSteamID = userID;

                    npcplayerCorpse.Spawn();
                    npcplayerCorpse.TakeChildren(this);

                    if (Configuration.Loot.DropInventory)
                    {                        
                        npcplayerCorpse.containers[1].Clear();
                    }
                    else
                    {
                        ItemContainer[] containers = npcplayerCorpse.containers;
                        for (int i = 0; i < containers.Length; i++)
                            containers[i].Clear();

                        int count = Random.Range(Configuration.Loot.Random.Minimum, Configuration.Loot.Random.Maximum);

                        int spawnedCount = 0;
                        int loopCount = 0;

                        while (true)
                        {
                            loopCount++;

                            if (loopCount > 3)
                                goto EndLootSpawn;

                            float probability = Random.Range(0f, 1f);

                            List<ConfigData.LootTable.RandomLoot.LootDefinition> definitions = new List<ConfigData.LootTable.RandomLoot.LootDefinition>(Configuration.Loot.Random.List);

                            for (int i = 0; i < Configuration.Loot.Random.List.Count; i++)
                            {
                                ConfigData.LootTable.RandomLoot.LootDefinition lootDefinition = definitions.GetRandom();

                                definitions.Remove(lootDefinition);

                                if (lootDefinition.Probability >= probability)
                                {
                                    lootDefinition.Create(containers[0]);

                                    spawnedCount++;

                                    if (spawnedCount >= count)
                                        goto EndLootSpawn;
                                }
                            }
                        }
                    }                    
                }
                EndLootSpawn:
                return npcplayerCorpse;
            }

            private void RemoveItemsOnDeath()
            {
                if (Configuration.Member.GiveGlowEyes)
                {
                    for (int i = inventory.containerWear.itemList.Count - 1; i >= 0; i--)
                    {
                        Item item = inventory.containerWear.itemList[i];
                        if (item.info == ConfigData.MemberOptions.Loadout.GlowEyes)
                        {
                            item.RemoveFromContainer();
                            item.Remove(0f);
                        }
                    }
                }

                if (Configuration.Loot.DropInventory && Configuration.Loot.DroppedBlacklist.Length > 0f)
                {
                    Action<ItemContainer> removeBlacklistedItems = new Action<ItemContainer>((ItemContainer itemContainer) =>
                    {
                        for (int i = itemContainer.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = itemContainer.itemList[i];
                            if (Configuration.Loot.DroppedBlacklist.Contains(item.info.shortname))
                            {
                                item.RemoveFromContainer();
                                item.Remove(0f);
                            }
                        }
                    });

                    removeBlacklistedItems(inventory.containerBelt);
                    removeBlacklistedItems(inventory.containerMain);
                }
            }

            private void ResetModifiedWeaponRange()
            {
                float effectiveRange;

                for (int i = 0; i < inventory.containerBelt.itemList.Count; i++)
                {
                    Item item = inventory.containerBelt.itemList[i];

                    if (item != null)
                    {
                        HeldEntity heldEntity = item.GetHeldEntity() as HeldEntity;
                        if (heldEntity != null)
                        {
                            if (heldEntity is BaseProjectile)
                            {
                                if (ConfigData.MemberOptions.Loadout.GetDefaultEffectiveRange(item.info.shortname, out effectiveRange))
                                    (heldEntity as BaseProjectile).effectiveRange = effectiveRange;
                            }

                            if (heldEntity is BaseMelee)
                            {
                                if (ConfigData.MemberOptions.Loadout.GetDefaultEffectiveRange(item.info.shortname, out effectiveRange))
                                    (heldEntity as BaseMelee).effectiveRange = effectiveRange;
                            }
                        }
                    }
                }
            }

            public override void AttackerInfo(PlayerLifeStory.DeathInfo info)
            {
                base.AttackerInfo(info);
                info.inflictorName = inventory.containerBelt.GetSlot(0)?.info?.shortname;
                info.attackerName = "zombie";
            }

            public override bool IsOnGround() => true;
            #endregion

            #region IAIAttack
            public void AttackTick(float delta, BaseEntity target, bool targetIsLOS)
            {
                if (target == null)                
                    return;

                if (Brain.Navigator.IsSwimming() && (!(CurrentWeapon is BaseMelee) || CurrentWeapon is Chainsaw))
                    return;
                                               
                Vector3 forward = eyes.BodyForward();
                Vector3 direction = target.CenterPoint() - eyes.position;
                float dot = Vector3.Dot(forward, direction.normalized);

                if (!targetIsLOS)
                {
                    if (dot < 0.5f)
                        targetAimedDuration = 0f;

                    CancelBurst();
                }
                else
                {
                    if (dot > 0.2f)
                        targetAimedDuration += delta;
                }
                
                if (targetAimedDuration >= 0.2f && targetIsLOS)
                {
                    float distanceToTarget;
                    if (IsTargetInAttackRange(target, out distanceToTarget))
                    {
                        if (CurrentWeapon is ThrownWeapon)
                        {
                            if (!abortThrowingExplosive)
                            {
                                abortDelta += delta;
                                if (abortDelta > 3f)
                                {
                                    if (isThrowingExplosive)
                                        abortThrowingExplosive = true;
                                    else
                                    {
                                        EquipWeapon();
                                        nextThrowTime = Time.time + 2f;
                                    }
                                }
                            }
                        }
                        else
                        {
                            ServerUseCurrentWeapon();
                        }
                    }
                    else
                    {
                        if (CurrentState == AIState.Chase && TargetUnreachable)
                        {
                            if (CurrentWeapon == null)
                                return;

                            if (CanThrowWeapon())
                            {                                
                                if (!(CurrentWeapon is ThrownWeapon))                                
                                    EquipThrowableExplosive();                                
                                else
                                {
                                    if (!isThrowingExplosive)                                    
                                        StartCoroutine(ServerUseThrowableExplosive());                                    
                                }
                            }
                        }
                    }
                }
                else CancelBurst();
            }

            private void ServerUseCurrentWeapon()
            {
                AttackEntity attackEntity = CurrentWeapon;
                if (attackEntity == null)                
                    return;
                
                BaseProjectile baseProjectile = attackEntity as BaseProjectile;
                if (baseProjectile)
                {
                    if (baseProjectile.primaryMagazine.contents <= 0)
                    {
                        baseProjectile.ServerReload();                        
                        return;
                    }

                    if (baseProjectile.NextAttackTime > Time.time)                    
                        return;                    
                }

                if (Mathf.Approximately(attackEntity.attackLengthMin, -1f))
                {
                    attackEntity.ServerUse(damageScale, null);
                    lastGunShotTime = Time.time;
                    return;
                }

                if (IsInvoking(TriggerDown))                
                    return;
                
                if (Time.time < nextTriggerTime)                
                    return;
                
                InvokeRepeating(TriggerDown, 0f, 0.01f);

                triggerEndTime = Time.time + Random.Range(attackEntity.attackLengthMin, attackEntity.attackLengthMax);
                TriggerDown();
            }

            #region Throw Explosives
            private bool EquipThrowableExplosive()
            {
                if (isEquippingWeapon)
                    return false;

                if (ThrowableExplosive != null && ThrowableExplosive.amount > 0)
                {
                    StartCoroutine(EquipItem(ThrowableExplosive));
                    nextThrowTime = Time.time + 1f;
                    return true;
                }

                return false;
            }

            private IEnumerator ServerUseThrowableExplosive()
            {
                isThrowingExplosive = true;

                nextThrowTime = Time.time + ConVar.Halloween.scarecrow_throw_beancan_global_delay + Random.Range(1f, 6f);

                modelState.aiming = true;
                SendNetworkUpdate(NetworkQueue.Update);

                yield return CoroutineEx.waitForSeconds(1f);

                if (!abortThrowingExplosive && HasTarget && Vector3.Distance(CurrentTarget.transform.position, transform.position) < 25f)
                {
                    Brain.Navigator.ApplyFacingDirectionOverride();

                    ThrowableExplosive.amount += 1;
                    (CurrentWeapon as ThrownWeapon).ServerThrow(CurrentTarget.transform.position);
                }
                else
                {
                    nextThrowTime = Time.time + 2f;
                }

                modelState.aiming = false;
                SendNetworkUpdate(NetworkQueue.Update);

                yield return CoroutineEx.waitForSeconds(0.5f);

                EquipWeapon();

                yield return CoroutineEx.waitForSeconds(0.5f);

                isThrowingExplosive = false;
                abortThrowingExplosive = false;
                abortDelta = 0f;
            }

            private bool CanThrowWeapon()
            {
                if (!ConVar.AI.npc_use_thrown_weapons || !ConVar.Halloween.scarecrows_throw_beancans)
                    return false;

                if (Time.time < nextThrowTime)
                    return false;

                if (isEquippingWeapon)
                    return false;

                if (CurrentWeapon is BaseProjectile)
                    return false;

                if (ThrowableExplosive == null || ThrowableExplosive.amount <= 0)
                    return false;

                if (!HasTarget)
                    return false;

                if (Brain.Navigator.IsSwimming())
                    return false;
                
                return true;
            }
            #endregion

            public bool CanAttack(BaseEntity entity) => true;

            public bool CanSeeTarget(BaseEntity entity)
            {
                if (!(entity is BaseCombatEntity))
                    return false;

                if (entity is BasePlayer)
                    return IsPlayerVisibleToUs((entity as BasePlayer), LOS_BLOCKING_LAYER);

                return (IsVisible(entity.CenterPoint(), eyes.worldStandingPosition, float.PositiveInfinity) ||
                        IsVisible(entity.transform.position, eyes.worldStandingPosition, float.PositiveInfinity));
            }

            public float CooldownDuration() => 5f;

            public float EngagementRange()
            {
                AttackEntity attackEntity = CurrentWeapon;
                if (!attackEntity)                
                    return Brain.SenseRange;
                
                return attackEntity is ThrownWeapon ? -1f : attackEntity.effectiveRange;
            }

            public float GetAmmoFraction()
            {
                if (CurrentWeapon is BaseProjectile)
                    return AmmoFractionRemaining();
                return 1f;
            }

            public BaseEntity GetBestTarget()
            {
                BaseEntity target = null;
                float delta = -1f;
                foreach (BaseEntity baseEntity in Brain.Senses.Memory.Targets)
                {
                    if (baseEntity == null || baseEntity.Health() <= 0f)
                        continue;

                    if (baseEntity is BasePlayer && !CanTargetBasePlayer(baseEntity as BasePlayer))                  
                        continue;                    

                    if (!CanTargetEntity(baseEntity))                   
                        continue;
                    
                    float distanceToTarget = Vector3.Distance(baseEntity.transform.position, transform.position);
                    if (distanceToTarget > Brain.TargetLostRange)
                        continue;

                    float rangeDelta = 1f - Mathf.InverseLerp(1f, Brain.SenseRange, distanceToTarget);                    

                    float dot = Vector3.Dot((baseEntity.transform.position - eyes.position).normalized, eyes.BodyForward());

                    if (Loadout.Sensory.IgnoreNonVisionSneakers && dot < Brain.VisionCone)
                        continue;

                    rangeDelta += Mathf.InverseLerp(Brain.VisionCone, 1f, dot) / 2f;
                    rangeDelta += (Brain.Senses.Memory.IsLOS(baseEntity) ? 2f : 0f);

                    if (rangeDelta <= delta)
                        continue;

                    target = baseEntity;
                    delta = rangeDelta;
                }

                if (target != null)
                    Horde.RegisterInterestInTarget(this, target);

                CurrentTarget = target;

                return target;
            }

            public bool CanTargetBasePlayer(BasePlayer player)
            {
                if (player.IsFlying)
                    return false;

                if (Configuration.Member.IgnoreSleepers && player.IsSleeping())
                    return false;

                if (!Configuration.Member.TargetHumanNPCs && !player.IsNpc && !player.userID.IsSteamId())
                    return false;

                if (player.userID.IsSteamId() && Instance.permission.UserHasPermission(player.UserIDString, IGNORE_PERMISSION))
                    return false;

                if (Loadout.Sensory.IgnoreSafeZonePlayers && player.InSafeZone())
                    return false;

                return true;
            }

            public bool CanTargetEntity(BaseEntity baseEntity)
            {
                if (!(baseEntity is BasePlayer) && !(baseEntity is BaseNpc))
                    return false;

                if (Configuration.Horde.RestrictLocalChaseDistance && Horde.IsLocalHorde)
                {
                    if (Vector3.Distance(baseEntity.transform.position, Horde.InitialPosition) > Horde.MaximumRoamDistance * 1.5f)
                        return false;
                }

                if (!Configuration.Member.TargetAnimals && baseEntity is BaseNpc)
                    return false;

                if (Vector3.Distance(baseEntity.transform.position, Transform.position) > Brain.TargetLostRange)
                    return false;

                return true;
            }

            public bool IsOnCooldown() => false;

            public bool IsTargetInRange(BaseEntity entity, out float distance)
            {
                if (CurrentWeapon == null || CurrentWeapon is BaseMelee)
                {
                    distance = float.MaxValue;
                    return false;
                }
                else return IsTargetInAttackRange(entity, out distance);
            }

            public bool IsTargetInAttackRange(BaseEntity entity, out float distance)
            {
                distance = Vector3.Distance(entity.transform.position, Transform.position);
                return distance <= EngagementRange();
            }

            public bool NeedsToReload() => false;

            public bool Reload() => true;

            public bool StartAttacking(BaseEntity entity) => true;

            public void StopAttacking() { }
            #endregion

            #region IAISenses
            public bool IsFriendly(BaseEntity entity) => entity is ZombieNPC;

            public bool IsTarget(BaseEntity entity) => !IsFriendly(entity);

            public bool IsThreat(BaseEntity entity) => IsTarget(entity);
            #endregion

            #region IThinker 
            public void TryThink()
            {
                if (Configuration.Member.CanSwim)
                {
                    modelState.waterLevel = Mathf.InverseLerp(0f, 1.8f, TerrainMeta.WaterMap.GetDepth(Transform.position));
                    SendNetworkUpdate(NetworkQueue.Update);

                    if (CurrentWeapon is Chainsaw)
                    {
                        if (Brain.Navigator.IsSwimming())
                            ServerStopChainsaw((CurrentWeapon as Chainsaw));
                        else ServerStartChainsaw((CurrentWeapon as Chainsaw));
                    }                   
                }

                base.ServerThink(Time.time - lastThinkTime);
                lastThinkTime = Time.time;

                if (Brain.ShouldServerThink())                
                    Brain.DoThink();
            }
            #endregion

            #region Lights
            private void LightCheck()
            {
                if ((TOD_Sky.Instance.Cycle.Hour > 18 || TOD_Sky.Instance.Cycle.Hour < 6) && !lightsOn)
                    ToggleLights(true);

                if ((TOD_Sky.Instance.Cycle.Hour < 18 && TOD_Sky.Instance.Cycle.Hour > 6) && lightsOn)
                    ToggleLights(false);
            }

            private void ToggleLights(bool lightsOn)
            {
                Item activeItem = GetActiveItem();
                if (activeItem != null)
                {
                    HeldEntity heldEntity = activeItem.GetHeldEntity() as HeldEntity;
                    if (heldEntity != null)
                        heldEntity.SendMessage("SetLightsOn", lightsOn, SendMessageOptions.DontRequireReceiver);
                }

                foreach (Item item in inventory.containerWear.itemList)
                {
                    ItemModWearable itemModWearble = item.info.GetComponent<ItemModWearable>();
                    if (itemModWearble && itemModWearble.emissive)
                    {
                        item.SetFlag(global::Item.Flag.IsOn, lightsOn);
                        item.MarkDirty();
                    }
                }

                if (isMounted)                
                    GetMounted().LightToggle(this);

                this.lightsOn = lightsOn;
            }
            #endregion

            #region Chainsaw Hackery
            private void ServerStartChainsaw(Chainsaw chainsaw)
            {
                if (chainsaw.HasFlag(Flags.On))
                    return;

                chainsaw.DoReload(default(BaseEntity.RPCMessage));
                chainsaw.SetEngineStatus(true);
                chainsaw.SendNetworkUpdateImmediate(false);

                Invoke(ChainsawRefuel, 1f);
            }

            private void ServerStopChainsaw(Chainsaw chainsaw)
            {
                if (!chainsaw.HasFlag(Flags.On))
                    return;

                chainsaw.SetEngineStatus(false);
                chainsaw.SendNetworkUpdateImmediate(false);

                CancelInvoke(ChainsawRefuel);
            }

            private void ChainsawRefuel()
            {
                if (!(CurrentWeapon is Chainsaw))
                    return;

                (CurrentWeapon as Chainsaw).ammo = (CurrentWeapon as Chainsaw).maxAmmo;

                Invoke(ChainsawRefuel, 1f);
            }
            #endregion
        }

        public class ZombieNavigator : NPCPlayerNavigator
        {
            public override void Init(BaseCombatEntity entity, NavMeshAgent agent)
            {
                TriggerStuckEvent = true;

                base.Init(entity, agent);
            }

            public override void OnStuck()
            {
                ZombieNPC zombieNPC = BaseEntity as ZombieNPC;

                if (zombieNPC.Brain != null && zombieNPC.Brain.Navigator != null && zombieNPC.Brain.Events != null)
                {
                    //if (zombieNPC.Horde.DebugMode)
                    //    Debug.Log($"Member #{zombieNPC.Horde.GetMemberIndex(zombieNPC)} stuck ({zombieNPC.Transform.position})");
                    zombieNPC.Brain.SwitchToState(AIState.Idle, IDLE_STATE_CONTAINER);
                }
            }

            public override bool IsSwimming()
            {
                if (!Configuration.Member.CanSwim)
                    return false;

                return (BaseEntity as ZombieNPC).modelState.waterLevel > 0.75f;
            }

            protected override float GetTargetSpeed()
            {
                if (IsSwimming())
                    return Speed;
                return base.GetTargetSpeed();
            }

            protected override void UpdatePositionAndRotation(Vector3 moveToPosition, float delta)
            {
                if (IsSwimming())                
                    moveToPosition.y = WaterSystem.GetHeight(moveToPosition)/* TerrainMeta.WaterMap.GetHeight(moveToPosition)*/ - 1.1f;                
                base.UpdatePositionAndRotation(moveToPosition, delta);
            }

            protected override bool CanEnableNavMeshNavigation()
            {
                if (IsSwimming())
                    return false;

                return base.CanEnableNavMeshNavigation();
            }

            public override void ApplyFacingDirectionOverride()
            {
                base.ApplyFacingDirectionOverride();

                if (overrideFacingDirectionMode == OverrideFacingDirectionMode.None)                
                    return;
                
                if (overrideFacingDirectionMode == OverrideFacingDirectionMode.Direction)
                {
                    NPCPlayerEntity.SetAimDirection(facingDirectionOverride);
                    return;
                }

                if (facingDirectionEntity != null)
                {
                    Vector3 aimDirection = GetAimDirection(NPCPlayerEntity, facingDirectionEntity);
                    facingDirectionOverride = aimDirection;
                    NPCPlayerEntity.SetAimDirection(facingDirectionOverride);
                }
            }
                       
            private static Vector3 GetAimDirection(BasePlayer aimingPlayer, BaseEntity target)
            {
                if (target == null)                
                    return Vector3Ex.Direction2D(aimingPlayer.transform.position + aimingPlayer.eyes.BodyForward() * 1000f, aimingPlayer.transform.position);
                
                if (Vector3Ex.Distance2D(aimingPlayer.transform.position, target.transform.position) <= 0.75f)                
                    return Vector3Ex.Direction2D(target.transform.position, EyesPosition(aimingPlayer));
                
                return (TargetAimPositionOffset(target) - EyesPosition(aimingPlayer)).normalized;
            }

            private static Vector3 EyesPosition(BasePlayer aimingPlayer) => aimingPlayer.eyes.position - Vector3.up * 0.15f;

            private static Vector3 TargetAimPositionOffset(BaseEntity target)
            {
                BasePlayer basePlayer = target as BasePlayer;
                if (basePlayer == null)                
                    return target.CenterPoint();
                
                if (basePlayer.IsSleeping() || basePlayer.IsWounded())                
                    return basePlayer.transform.position + Vector3.up * 0.1f;
                
                return basePlayer.eyes.position - Vector3.up * 0.15f;
            }
        }
                
        private const int IDLE_STATE_CONTAINER = 2;

        public class ZombieNPCBrain : BaseAIBrain<ZombieNPC>
        {
            public override void InitializeAI()
            {
                SenseTypes = Configuration.Member.GetSenseTypes();

                ZombieNPC zombieNPC = GetEntity();

                zombieNPC.Loadout.Sensory.ApplySettingsToBrain(this);                

                base.InitializeAI();

                ThinkMode = AIThinkMode.Interval;
                thinkRate = 0.25f;
                PathFinder = new HumanPathFinder();
                ((HumanPathFinder)PathFinder).Init(zombieNPC);

                zombieNPC.Loadout.Movement.ApplySettingsToNavigator(Navigator);

                Navigator.MaxRoamDistanceFromHome = zombieNPC.Horde.IsLocalHorde ? zombieNPC.Horde.MaximumRoamDistance : -1f;

                LoadAIDesign(ProtoBuf.AIDesign.Deserialize(Design), null, 0);                             
            }

            public override void AddStates()
            {
                states = new Dictionary<AIState, BasicAIState>();

                AddState(new RoamState());
                AddState(new ChaseState());
                AddState(new BaseIdleState());
            }

            public override void Think(float delta)
            {
                base.Think(delta);

                ZombieNPC zombieNPC = GetEntity();

                if (zombieNPC.IsGroupLeader)
                    zombieNPC.Horde.Update();

                if (sleeping)                
                    return;

                if (!Configuration.Member.CanSwim && Configuration.Member.KillUnderWater)
                {                    
                    if (zombieNPC != null && zombieNPC.WaterFactor() > 0.85f && !zombieNPC.IsDestroyed)
                    {
                        const float DROWN_TIMER = 5f;
                        zombieNPC.Hurt(delta * (zombieNPC.MaxHealth() / DROWN_TIMER), DamageType.Drowned, null, true);
                    }
                }                
            }
          
            protected override void OnStateChanged()
            {
                base.OnStateChanged();
                GetEntity().CurrentState = CurrentState.StateType;

                //if (GetEntity().Horde.DebugMode)
                //{
                //    Debug.Log($"{(GetEntity().IsGroupLeader ? "[Leader] " : "")}#{GetEntity().Horde.GetMemberIndex(GetEntity())} OnStateChanged {CurrentState.StateType}");
                //    if (GetEntity().HasTarget)
                //    {
                //        Debug.Log($"Target {GetEntity().CurrentTarget.ShortPrefabName} ({GetEntity().CurrentTarget.transform.position})");
                //    }
                //}
            }

            public override void OnDestroy()
            {
                if (Rust.Application.isQuitting)                
                    return;

                ZombieNPC zombieNPC = GetEntity();

                BaseEntity.Query.Server.RemoveBrain(zombieNPC);
                
                LeaveGroup();
            }

            internal void SetSleeping(bool sleep)
            {
                if (sleep)
                {
                    sleeping = true;

                    if (Navigator != null)
                        Navigator.Pause();

                    CancelInvoke(TickMovement);
                }
                else
                {
                    sleeping = false;

                    if (Navigator != null)
                        Navigator.Resume();

                    CancelInvoke(TickMovement);
                    InvokeRandomized(TickMovement, 1f, 0.1f, 0.010000001f);
                }
            }

            public class RoamState : BasicAIState
            {
                private StateStatus status = StateStatus.Error;

                private static readonly Vector3[] preferedTopologySamples = new Vector3[4];

                private static readonly Vector3[] topologySamples = new Vector3[4];

                private bool isAlert;

                public RoamState() : base(AIState.Roam) { }

                public override void StateEnter()
                {
                    base.StateEnter();
                    status = StateStatus.Error;

                    if (brain.PathFinder == null)                    
                        return;

                    ZombieNPC zombieNPC = GetEntity();

                    isAlert = zombieNPC.IsAlert || zombieNPC.Horde.HordeOnAlert;

                    Vector3 bestRoamPosition;
                    if (!zombieNPC.IsGroupLeader)
                    {
                        isAlert = zombieNPC.Horde.Leader.IsAlert;

                        bestRoamPosition = zombieNPC.Horde.GetLeaderDestination();
                        bestRoamPosition = BasePathFinder.GetPointOnCircle(bestRoamPosition, Random.Range(2f, 7f), Random.Range(0f, 359f));
                    }
                    else
                    {
                        if (zombieNPC.DestinationOverride != Vector3.zero)
                        {
                            bestRoamPosition = GetBestRoamPosition(brain.Navigator, zombieNPC.DestinationOverride, brain.Events.Memory.Position.Get(4), 0f, 20f);
                            zombieNPC.DestinationOverride = Vector3.zero;
                        }
                        else bestRoamPosition = zombieNPC.Horde.IsLocalHorde ? 
                                GetBestRoamPosition(brain.Navigator, zombieNPC.Horde.InitialPosition, brain.Events.Memory.Position.Get(4), 10f, zombieNPC.Horde.MaximumRoamDistance) :
                                GetBestRoamPosition(brain.Navigator, zombieNPC.Transform.position, brain.Events.Memory.Position.Get(4), 20f, 100f);
                    }

                    if (brain.Navigator.SetDestination(bestRoamPosition, isAlert ? BaseNavigator.NavigationSpeed.Fast : DefaultRoamSpeed, 0f, 0f))
                    {
                        if (zombieNPC.IsGroupLeader)                        
                            zombieNPC.Horde.ResetRoamTarget();

                        //if (zombieNPC.Horde.DebugMode)
                        //    Debug.Log($"Set member #{zombieNPC.Horde.GetMemberIndex(zombieNPC)} (leader ? {zombieNPC.IsGroupLeader}) destination to {bestRoamPosition}");

                        status = StateStatus.Running;
                        return;
                    }

                    //if (zombieNPC.Horde.DebugMode)
                    //    Debug.Log($"Failed to set member #{zombieNPC.Horde.GetMemberIndex(zombieNPC)} (leader ? {zombieNPC.IsGroupLeader}) destination to {bestRoamPosition}");

                    status = StateStatus.Error;
                }

                public override void StateLeave()
                {
                    base.StateLeave();
                    Stop();

                    if (isAlert)
                        GetEntity().IsAlert = false; 

                    isAlert = false;
                }

                public override StateStatus StateThink(float delta)
                {
                    base.StateThink(delta);
                    
                    if (status == StateStatus.Error) 
                        return status;

                    if (!isAlert && (GetEntity().Horde.HordeOnAlert || GetEntity().IsAlert))
                    {
                        StateEnter();
                        return StateStatus.Running;
                    }

                    if (brain.Navigator.Moving)                    
                        return StateStatus.Running;
                    
                    return StateStatus.Finished;
                }

                private void Stop() => brain.Navigator.Stop();
                                
                private Vector3 GetBestRoamPosition(BaseNavigator navigator, Vector3 localTo, Vector3 fallbackPos, float minRange, float maxRange)
                {
                    int topologyIndex = 0;
                    int preferredTopologyIndex = 0;

                    for (float degree = 0f; degree < 360f; degree += 90f)
                    {
                        Vector3 position;
                        Vector3 pointOnCircle = BasePathFinder.GetPointOnCircle(localTo, Random.Range(minRange, maxRange), degree + Random.Range(0f, 90f));

                        if (navigator.GetNearestNavmeshPosition(pointOnCircle, out position, 20f) && navigator.IsAcceptableWaterDepth(position))
                        {
                            topologySamples[topologyIndex] = position;
                            topologyIndex++;
                            if (navigator.IsPositionATopologyPreference(position))
                            {
                                preferedTopologySamples[preferredTopologyIndex] = position;
                                preferredTopologyIndex++;
                            }
                        }
                    }

                    Vector3 chosenPosition;

                    if (Random.Range(0f, 1f) <= 0.9f && preferredTopologyIndex > 0)                  
                        chosenPosition = preferedTopologySamples[Random.Range(0, preferredTopologyIndex)];
                    
                    else if (topologyIndex > 0)                   
                        chosenPosition = topologySamples[Random.Range(0, topologyIndex)];
                    
                    else chosenPosition = fallbackPos;                    
                    
                    return chosenPosition;
                }
            }

            public class ChaseState : BasicAIState
            {
                private StateStatus status = StateStatus.Error;

                private float nextPositionUpdateTime;

                private float originalStopDistance;

                private bool unreachableLastUpdate;

                public ChaseState() : base(AIState.Chase)
                {
                    AgrresiveState = true;
                }

                public override void StateLeave()
                {
                    base.StateLeave();

                    Stop();

                    brain.Navigator.StoppingDistance = originalStopDistance;

                    GetEntity().TargetUnreachable = false;
                }

                public override void StateEnter()
                {
                    base.StateEnter();

                    status = StateStatus.Error;
                    
                    if (brain.PathFinder == null)                    
                        return;
                    
                    status = StateStatus.Running;
                    nextPositionUpdateTime = 0f;
                    originalStopDistance = brain.Navigator.StoppingDistance;

                    AttackEntity attackEntity = GetEntity().CurrentWeapon;
                    if (attackEntity is BaseMelee)
                        brain.Navigator.StoppingDistance = 0.1f;

                    brain.Navigator.SetCurrentSpeed(BaseNavigator.NavigationSpeed.Fast);
                }

                private void Stop()
                {
                    brain.Navigator.Stop();
                    brain.Navigator.ClearFacingDirectionOverride();                    
                }

                public override StateStatus StateThink(float delta)
                {
                    base.StateThink(delta);
                    if (status == StateStatus.Error)                 
                        return status;
                                        
                    ZombieNPC zombieNPC = GetEntity();

                    BaseEntity baseEntity = brain.Events.Memory.Entity.Get(brain.Events.CurrentInputMemorySlot);
                    if (baseEntity == null || (baseEntity is BasePlayer && !zombieNPC.CanTargetBasePlayer(baseEntity as BasePlayer)) || !zombieNPC.CanTargetEntity(baseEntity))
                    {
                        brain.Events.Memory.Entity.Remove(brain.Events.CurrentInputMemorySlot);
                        Stop();
                        return StateStatus.Error;
                    }

                    FaceTarget(zombieNPC, baseEntity);

                    if (Time.time > nextPositionUpdateTime)
                    {    
                        if (!(zombieNPC.CurrentWeapon is BaseProjectile))
                        {                      
                            if (unreachableLastUpdate)
                            {
                                Vector3 position = brain.PathFinder.GetRandomPositionAround(baseEntity.transform.position, 3f, 10f);
                                brain.Navigator.SetDestination(position, BaseNavigator.NavigationSpeed.Fast, 0.1f, 0f);
                                nextPositionUpdateTime = Time.time + 3f;
                                unreachableLastUpdate = false;

                                return StateStatus.Running;
                            }

                            if (!brain.Navigator.SetDestination(baseEntity.transform.position, BaseNavigator.NavigationSpeed.Fast, 0.1f, 0f))                            
                                return StateStatus.Error;
                            
                            if (brain.Navigator.Agent.path.status > NavMeshPathStatus.PathComplete ||
                                (zombieNPC.CurrentWeapon is BaseMelee && Vector3.Distance(baseEntity.transform.position, brain.Navigator.Agent.destination) > zombieNPC.EngagementRange()))
                            {
                                zombieNPC.TargetUnreachable = true;
                                unreachableLastUpdate = true;
                            }
                            else zombieNPC.TargetUnreachable = false;

                            nextPositionUpdateTime = Time.time + 0.1f;

                            if (!brain.Navigator.Moving)                          
                                return StateStatus.Finished;                            
                        }
                        else
                        {
                            Vector3 position = brain.PathFinder.GetRandomPositionAround(baseEntity.transform.position, 10f, zombieNPC.EngagementRange() * 0.75f);

                            if (brain.Navigator.SetDestination(position, BaseNavigator.NavigationSpeed.Fast, 0f, 0f))
                                nextPositionUpdateTime = Random.Range(3f, 6f);
                            else return StateStatus.Error;
                        }
                    }

                    return StateStatus.Running;
                }

                private void FaceTarget(ZombieNPC zombieNPC, BaseEntity baseEntity)
                {
                    float distanceToTarget = Vector3.Distance(baseEntity.transform.position, zombieNPC.Transform.position);

                    if (!(zombieNPC.CurrentWeapon is BaseProjectile) && (brain.Senses.Memory.IsLOS(baseEntity) || distanceToTarget <= 10f))
                        brain.Navigator.SetFacingDirectionEntity(baseEntity);

                    else if (zombieNPC.CurrentWeapon is BaseProjectile && brain.Senses.Memory.IsLOS(baseEntity))
                        brain.Navigator.SetFacingDirectionEntity(baseEntity);

                    else brain.Navigator.ClearFacingDirectionOverride();
                }
            }

            private static readonly byte[] Design = new byte[] { 8, 2, 8, 3, 8, 1, 18, 62, 8, 0, 16, 2, 26, 12, 8, 4, 16, 2, 24, 0, 32, 0, 40, 0, 48, 0, 26, 12, 8, 2, 16, 2, 24, 0, 32, 0, 40, 0, 48, 0, 26, 12, 8, 3, 16, 1, 24, 0, 32, 4, 40, 0, 48, 0, 26, 12, 8, 14, 16, 1, 24, 0, 32, 0, 40, 0, 48, 0, 32, 0, 18, 117, 8, 1, 16, 3, 26, 12, 8, 4, 16, 2, 24, 0, 32, 0, 40, 0, 48, 0, 26, 12, 8, 2, 16, 2, 24, 0, 32, 0, 40, 0, 48, 0, 26, 21, 8, 15, 16, 255, 255, 255, 255, 255, 255, 255, 255, 255, 1, 24, 1, 32, 0, 40, 0, 48, 0, 26, 21, 8, 5, 16, 255, 255, 255, 255, 255, 255, 255, 255, 255, 1, 24, 0, 32, 0, 40, 0, 48, 0, 26, 12, 8, 20, 16, 0, 24, 0, 32, 0, 40, 0, 48, 0, 26, 21, 8, 16, 16, 255, 255, 255, 255, 255, 255, 255, 255, 255, 1, 24, 0, 32, 0, 40, 0, 48, 0, 32, 0, 18, 61, 8, 2, 16, 1, 26, 25, 8, 0, 16, 0, 24, 0, 32, 0, 40, 0, 48, 0, 162, 6, 10, 13, 0, 0, 0, 0, 21, 205, 204, 204, 61, 26, 12, 8, 3, 16, 1, 24, 0, 32, 4, 40, 0, 48, 0, 26, 12, 8, 14, 16, 1, 24, 0, 32, 0, 40, 0, 48, 0, 32, 0, 24, 0, 34, 13, 90, 111, 109, 98, 105, 101, 32, 68, 101, 115, 105, 103, 110, 40, 0, 48, 0 };            
        }

        #region Commands 
        //[ChatCommand("hordedebug")]
        //private void cmdHordeDebug(BasePlayer player, string command, string[] args)
        //{
        //    if (!player.IsAdmin)
        //        return;

        //    int number;
        //    if (args.Length != 1 || !int.TryParse(args[0], out number))
        //    {
        //        SendReply(player, "You must specify a horde number");
        //        return;
        //    }

        //    Horde horde = Horde.AllHordes[number];
        //    horde.DebugMode = !horde.DebugMode;
        //    SendReply(player, $"Debug mode for horde #{number} set to {horde.DebugMode}");
        //}

        [ChatCommand("horde")]
        private void cmdHorde(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, "/horde info - Show position and information about active zombie hordes");
                SendReply(player, "/horde tpto <number> - Teleport to the specified zombie horde");
                SendReply(player, "/horde destroy <number> - Destroy the specified zombie horde");
                SendReply(player, "/horde create <opt:distance> <opt:profile> - Create a new zombie horde on your position, optionally specifying distance they can roam and the horde profile you want to use");
                SendReply(player, "/horde createspawn <opt:membercount> <opt:distance> <opt:profile> - Save your current position as a custom horde spawn point");
                SendReply(player, "/horde createloadout - Copy your current inventory to a new zombie loadout");
                SendReply(player, "/horde hordecount <number> - Set the maximum number of hordes allowed");
                SendReply(player, "/horde membercount <number> - Set the maximum number of members allowed per horde");
                return;
            }

            switch (args[0].ToLower())
            {                
                case "info":
                    int memberCount = 0;
                    int hordeNumber = 0;
                    foreach (Horde horde in Horde.AllHordes)
                    {
                        player.SendConsoleCommand("ddraw.text", 30, Color.green, horde.CentralLocation + new Vector3(0, 1.5f, 0), $"<size=20>Zombie Horde {hordeNumber}</size>");
                        memberCount += horde.MemberCount;
                        hordeNumber++;
                    }

                    SendReply(player, $"There are {Horde.AllHordes.Count} active zombie hordes with a total of {memberCount} zombies");
                    return;
                case "destroy":
                    {
                        int number;
                        if (args.Length != 2 || !int.TryParse(args[1], out number))
                        {
                            SendReply(player, "You must specify a horde number");
                            return;
                        }

                        if (number < 0 || number >= Horde.AllHordes.Count)
                        {
                            SendReply(player, "An invalid horde number has been specified");
                            return;
                        }

                        Horde.AllHordes[number].Destroy(true, true);
                        SendReply(player, $"You have destroyed zombie horde {number}");
                        return;
                    }
                case "tpto":
                    {
                        int number;
                        if (args.Length != 2 || !int.TryParse(args[1], out number))
                        {
                            SendReply(player, "You must specify a horde number");
                            return;
                        }

                        if (number < 0 || number >= Horde.AllHordes.Count)
                        {
                            SendReply(player, "An invalid horde number has been specified");
                            return;
                        }

                        player.Teleport(Horde.AllHordes[number].CentralLocation);
                        SendReply(player, $"You have teleported to zombie horde {number}");
                        return;
                    }                
                case "create":
                    {
                        float distance = -1;
                        if (args.Length >= 2)
                        {
                            if (!float.TryParse(args[1], out distance))
                            {
                                SendReply(player, "Invalid Syntax!");
                                return;
                            }
                        }

                        string profile = string.Empty;
                        if (args.Length >= 3 && Configuration.HordeProfiles.ContainsKey(args[2]))
                            profile = args[2];

                        Vector3 position;
                        if (NavmeshSpawnPoint.Find(player.transform.position, 5f, out position))
                        {
                            if (Horde.Create(new Horde.SpawnOrder(position, Configuration.Horde.InitialMemberCount, Configuration.Horde.MaximumMemberCount, distance, profile)))
                            {
                                if (distance > 0)
                                    SendReply(player, $"You have created a zombie horde with a roam distance of {distance}");
                                else SendReply(player, "You have created a zombie horde");

                                return;
                            }
                        }

                        SendReply(player, "Invalid spawn position, move to another more open position. Unable to spawn horde");
                        return;
                    }

                case "createspawn":
                    {
                        int members = Configuration.Horde.InitialMemberCount;
                        if (args.Length >= 2)
                        {
                            if (!int.TryParse(args[1], out members))
                            {
                                SendReply(player, "Invalid Syntax!");
                                return;
                            }
                        }

                        float distance = -1;
                        if (args.Length >= 3)
                        {
                            if (!float.TryParse(args[2], out distance))
                            {
                                SendReply(player, "Invalid Syntax!");
                                return;
                            }
                        }

                        string profile = string.Empty;
                        if (args.Length >= 4 && Configuration.HordeProfiles.ContainsKey(args[3]))
                            profile = args[3];

                        Configuration.Monument.Custom.Add(new ConfigData.MonumentSpawn.CustomSpawnPoints
                        {
                            Enabled = true,
                            HordeSize = members,
                            Location = player.transform.position,
                            Profile = profile,
                            RoamDistance = distance
                        });

                        SaveConfig();

                        Vector3 position;
                        if (NavmeshSpawnPoint.Find(player.transform.position, 5f, out position))
                        {
                            if (Horde.Create(new Horde.SpawnOrder(position, Configuration.Horde.InitialMemberCount, Configuration.Horde.MaximumMemberCount, distance, profile)))
                            {
                                if (distance > 0)
                                    SendReply(player, $"You have created a custom horde spawn point with a roam distance of {distance}");
                                else SendReply(player, "You have created a custom horde spawn point");

                                return;
                            }
                        }

                        SendReply(player, "Invalid spawn position, move to another more open position");
                        return;
                    }

                case "createloadout":
                    {
                        ConfigData.MemberOptions.Loadout loadout = new ConfigData.MemberOptions.Loadout($"loadout-{Configuration.Member.Loadouts.Count}");

                        for (int i = 0; i < player.inventory.containerBelt.itemList.Count; i++)
                        {
                            Item item = player.inventory.containerBelt.itemList[i];
                            if (item == null || item.amount == 0)
                                continue;

                            loadout.BeltItems.Add(new ConfigData.LootTable.InventoryItem()
                            {
                                Amount = item.amount,
                                Shortname = item.info.shortname,
                                SkinID = item.skin
                            });
                        }

                        for (int i = 0; i < player.inventory.containerMain.itemList.Count; i++)
                        {
                            Item item = player.inventory.containerMain.itemList[i];
                            if (item == null || item.amount == 0)
                                continue;

                            loadout.MainItems.Add(new ConfigData.LootTable.InventoryItem()
                            {
                                Amount = item.amount,
                                Shortname = item.info.shortname,
                                SkinID = item.skin
                            });
                        }

                        for (int i = 0; i < player.inventory.containerWear.itemList.Count; i++)
                        {
                            Item item = player.inventory.containerWear.itemList[i];
                            if (item == null || item.amount == 0)
                                continue;

                            loadout.WearItems.Add(new ConfigData.LootTable.InventoryItem()
                            {
                                Amount = item.amount,
                                Shortname = item.info.shortname,
                                SkinID = item.skin
                            });
                        }

                        Configuration.Member.Loadouts.Add(loadout);
                        SaveConfig();

                        SendReply(player, "Saved your current inventory as a zombie loadout");
                        return;
                    }

                case "hordecount":
                    {
                        int hordes;
                        if (args.Length < 2 || !int.TryParse(args[1], out hordes))
                        {
                            SendReply(player, "You must enter a number");
                            return;
                        }

                        Configuration.Horde.MaximumHordes = hordes;

                        if (Horde.AllHordes.Count < hordes)
                            CreateRandomHordes();
                        SaveConfig();
                        SendReply(player, $"Set maximum hordes to {hordes}");
                        return;
                    }

                case "membercount":
                    {
                        int members;
                        if (args.Length < 2 || !int.TryParse(args[1], out members))
                        {
                            SendReply(player, "You must enter a number");
                            return;
                        }

                        Configuration.Horde.MaximumMemberCount = members;
                        SaveConfig();
                        SendReply(player, $"Set maximum horde members to {members}");
                        return;
                    }
                default:
                    SendReply(player, "Invalid Syntax!");
                    break;
            }
        }

        [ConsoleCommand("horde")]
        private void ccmdHorde(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                if (!permission.UserHasPermission(arg.Connection.userid.ToString(), ADMIN_PERMISSION))
                {
                    SendReply(arg, "You do not have permission to use this command");
                    return;
                }
            }

            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "horde info - Show position and information about active zombie hordes");
                SendReply(arg, "horde destroy <number> - Destroy the specified zombie horde");
                SendReply(arg, "horde create <opt:distance> <opt:profile> - Create a new zombie horde at a random position, optionally specifying distance they can roam from the initial spawn point");
                SendReply(arg, "horde addloadout <kitname> <opt:otherkitname> <opt:otherkitname> - Convert the specified kit(s) into loadout(s) (add as many as you want)");
                SendReply(arg, "horde hordecount <number> - Set the maximum number of hordes allowed");
                SendReply(arg, "horde membercount <number> - Set the maximum number of members allowed per horde");
                return;
            }

            switch (arg.Args[0].ToLower())
            {
                case "info":
                    int memberCount = 0;
                    int hordeNumber = 0;
                    foreach (Horde horde in Horde.AllHordes)
                    {
                        memberCount += horde.MemberCount;
                        hordeNumber++;
                    }

                    SendReply(arg, $"There are {Horde.AllHordes.Count} active zombie hordes with a total of {memberCount} zombies");
                    return;
                case "destroy":
                    int number;
                    if (arg.Args.Length != 2 || !int.TryParse(arg.Args[1], out number))
                    {
                        SendReply(arg, "You must specify a horde number");
                        return;
                    }

                    if (number < 1 || number > Horde.AllHordes.Count)
                    {
                        SendReply(arg, "An invalid horde number has been specified");
                        return;
                    }

                    Horde.AllHordes[number - 1].Destroy(true, true);
                    SendReply(arg, $"You have destroyed zombie horde {number}");
                    return;                
                case "create":
                    float distance = -1;
                    if (arg.Args.Length >= 2)
                    {
                        if (!float.TryParse(arg.Args[1], out distance))
                        {
                            SendReply(arg, "Invalid Syntax!");
                            return;
                        }
                    }

                    string profile = string.Empty;
                    if (arg.Args.Length >= 3 && Configuration.HordeProfiles.ContainsKey(arg.Args[2]))
                        profile = arg.Args[2];

                    Vector3 position;
                    if (NavmeshSpawnPoint.Find(GetSpawnPoint(), 20f, out position) &&
                        Horde.Create(new Horde.SpawnOrder(position, Configuration.Horde.InitialMemberCount, Configuration.Horde.MaximumMemberCount, distance, profile)))                    
                    {                        
                        if (distance > 0)
                            SendReply(arg, $"You have created a zombie horde with a roam distance of {distance}");
                        else SendReply(arg, "You have created a zombie horde");
                    }
                    else SendReply(arg, "Invalid spawn position. Unable to spawn horde. Try again for a new random position");

                    return;
                case "addloadout":
                    if (!Kits)
                    {
                        SendReply(arg, "Unable to find the kits plugin");
                        return;
                    }

                    if (arg.Args.Length < 2)
                    {
                        SendReply(arg, "horde addloadout <kitname> <opt:otherkitname> <opt:otherkitname> - Convert the specified kit(s) into loadout(s) (add as many as you want)");
                        return;
                    }

                    for (int i = 1; i < arg.Args.Length; i++)
                    {
                        string kitname = arg.Args[i];
                        object success = Kits.Call("GetKitInfo", kitname);
                        if (success == null)
                        {
                            SendReply(arg, $"Unable to find a kit with the name {kitname}");
                            continue;
                        }

                        JObject obj = success as JObject;
                        JArray items = obj["items"] as JArray;

                        ConfigData.MemberOptions.Loadout loadout = new ConfigData.MemberOptions.Loadout(kitname);

                        for (int y = 0; y < items.Count; y++)
                        {
                            JObject item = items[y] as JObject;
                            string container = (string)item["container"];

                            List<ConfigData.LootTable.InventoryItem> list = container == "belt" ? loadout.BeltItems : container == "main" ? loadout.MainItems : loadout.WearItems;
                            list.Add(new ConfigData.LootTable.InventoryItem
                            {
                                Amount = (int)item["amount"],
                                Shortname = ItemManager.FindItemDefinition((int)item["itemid"])?.shortname,
                                SkinID = (ulong)item["skinid"]
                            });
                        }

                        Configuration.Member.Loadouts.Add(loadout);

                        SendReply(arg, $"Successfully converted the kit {kitname} to a zombie loadout");
                    }
                    
                    SaveConfig();                    
                    return;

                case "hordecount":
                    int hordes;
                    if (arg.Args.Length < 2 || !int.TryParse(arg.Args[1], out hordes))
                    {
                        SendReply(arg, "You must enter a number");
                        return;
                    }

                    Configuration.Horde.MaximumHordes = hordes;

                    if (Horde.AllHordes.Count < hordes)
                        CreateRandomHordes();
                    SaveConfig();
                    SendReply(arg, $"Set maximum hordes to {hordes}");
                    return;

                case "membercount":
                    int members;
                    if (arg.Args.Length < 2 || !int.TryParse(arg.Args[1], out members))
                    {
                        SendReply(arg, "You must enter a number");
                        return;
                    }

                    Configuration.Horde.MaximumMemberCount = members;
                    SaveConfig();
                    SendReply(arg, $"Set maximum horde members to {members}");
                    return;
                default:
                    SendReply(arg, "Invalid Syntax!");
                    break;
            }
        }

        private float nextCountTime;
        private string cachedString = string.Empty;

        private string GetInfoString()
        {
            if (nextCountTime < Time.time || string.IsNullOrEmpty(cachedString))
            {
                int memberCount = 0;
                Horde.AllHordes.ForEach(x => memberCount += x.MemberCount);
                cachedString = $"There are currently <color=#ce422b>{Horde.AllHordes.Count}</color> hordes with a total of <color=#ce422b>{memberCount}</color> zombies";
                nextCountTime = Time.time + 30f;
            }

            return cachedString;
        }

        [ChatCommand("hordeinfo")]
        private void cmdHordeInfo(BasePlayer player, string command, string[] args) => player.ChatMessage(GetInfoString());
        
        [ConsoleCommand("hordeinfo")]
        private void ccmdHordeInfo(ConsoleSystem.Arg arg)
        {            
            if (arg.Connection == null)
                PrintToChat(GetInfoString());
        }

        #endregion

        #region Config      
       
        public static ConfigData Configuration;

        internal class ConfigData
        {
            [JsonProperty(PropertyName = "Horde Options")]
            public HordeOptions Horde { get; set; }

            [JsonProperty(PropertyName = "Horde Member Options")]
            public MemberOptions Member { get; set; }

            [JsonProperty(PropertyName = "Loot Table")]
            public LootTable Loot { get; set; }

            [JsonProperty(PropertyName = "Monument Spawn Options")]
            public MonumentSpawn Monument { get; set; }

            [JsonProperty(PropertyName = "Timed Spawn Options")]
            public TimedSpawnOptions TimedSpawns { get; set; }

            [JsonProperty(PropertyName = "Horde Profiles (profile name, list of applicable loadouts)")]
            public Dictionary<string, List<string>> HordeProfiles { get; set; }

            public class TimedSpawnOptions
            {
                [JsonProperty(PropertyName = "Only allows spawns during the set time period")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Despawn hordes outside of the set time period")]
                public bool Despawn { get; set; }

                [JsonProperty(PropertyName = "Start time (0.0 - 24.0)")]
                public float Start { get; set; }

                [JsonProperty(PropertyName = "End time (0.0 - 24.0)")]
                public float End { get; set; }
            }

            public class HordeOptions
            {
                [JsonProperty(PropertyName = "Amount of zombies to spawn when a new horde is created")]
                public int InitialMemberCount { get; set; }

                [JsonProperty(PropertyName = "Maximum amount of spawned zombies per horde")]
                public int MaximumMemberCount { get; set; }

                [JsonProperty(PropertyName = "Maximum amount of hordes at any given time")]
                public int MaximumHordes { get; set; }

                [JsonProperty(PropertyName = "Amount of time from when a horde is destroyed until a new horde is created (seconds)")]
                public int RespawnTime { get; set; }

                [JsonProperty(PropertyName = "Amount of time before a horde grows in size")]
                public int GrowthRate { get; set; }

                [JsonProperty(PropertyName = "Add a zombie to the horde when a horde member kills a player")]
                public bool CreateOnDeath { get; set; }

                [JsonProperty(PropertyName = "Merge hordes together if they collide")]
                public bool MergeHordes { get; set; }

                [JsonProperty(PropertyName = "Spawn system (SpawnsDatabase, Random)")]
                public string SpawnType { get; set; }

                [JsonProperty(PropertyName = "Spawn file (only required when using SpawnsDatabase)")]
                public string SpawnFile { get; set; }

                [JsonProperty(PropertyName = "Amount of time a player needs to be outside of a zombies vision before it forgets about them")]
                public float ForgetTime { get; set; }

                [JsonProperty(PropertyName = "Default roam speed (Slowest, Slow, Normal, Fast)")]
                public string DefaultRoamSpeed { get; set; }

                [JsonProperty(PropertyName = "Force all hordes to roam locally")]
                public bool LocalRoam { get; set; }

                [JsonProperty(PropertyName = "Local roam distance")]
                public float RoamDistance { get; set; }

                [JsonProperty(PropertyName = "Restrict chase distance for local hordes (1.5x the maximum roam distance for that horde)")]
                public bool RestrictLocalChaseDistance { get; set; }

                [JsonProperty(PropertyName = "Use horde profiles for randomly spawned hordes")]
                public bool UseProfiles { get; set; }

                [JsonProperty(PropertyName = "Sense nearby gunshots and explosions")]
                public bool UseSenses { get; set; } 
            }

            public class MemberOptions
            {
                [JsonProperty(PropertyName = "Can target animals")]
                public bool TargetAnimals { get; set; }

                [JsonProperty(PropertyName = "Can be targeted by turrets")]
                public bool TargetedByTurrets { get; set; }

                [JsonProperty(PropertyName = "Can be targeted by peacekeeper turrets and NPC turrets")]
                public bool TargetedByPeaceKeeperTurrets { get; set; }

                [JsonProperty(PropertyName = "Can be targeted by Bradley APC")]
                public bool TargetedByAPC { get; set; }

                [JsonProperty(PropertyName = "Can be targeted by other NPCs")]
                public bool TargetedByNPCs { get; set; }

                [JsonProperty(PropertyName = "Can be targeted by animals")]
                public bool TargetedByAnimals { get; set; }

                [JsonProperty(PropertyName = "Can target other NPCs")]
                public bool TargetNPCs { get; set; }

                [JsonProperty(PropertyName = "Can target NPCs from HumanNPC")]
                public bool TargetHumanNPCs { get; set; }

                [JsonProperty(PropertyName = "Ignore sleeping players")]
                public bool IgnoreSleepers { get; set; }

                [JsonProperty(PropertyName = "Give all zombies glowing eyes")]
                public bool GiveGlowEyes { get; set; }

                [JsonProperty(PropertyName = "Headshots instantly kill zombie")]
                public bool HeadshotKills { get; set; }
                               
                [JsonProperty(PropertyName = "Kill NPCs that are under water")]
                public bool KillUnderWater { get; set; }

                [JsonProperty(PropertyName = "Can zombies swim across water")]
                public bool CanSwim { get; set; }

                [JsonProperty(PropertyName = "Enable NPC dormant system. This will put NPCs to sleep when no players are nearby to improve performance")]
                public bool EnableDormantSystem { get; set; }

                public List<Loadout> Loadouts { get; set; }

                [JsonIgnore]
                private EntityType _senseTypes = 0;

                public EntityType GetSenseTypes()
                {
                    if (_senseTypes == 0) 
                    {
                        _senseTypes |= EntityType.Player;

                        if (TargetNPCs)
                            _senseTypes |= EntityType.BasePlayerNPC;

                        if (TargetAnimals)
                            _senseTypes |= EntityType.NPC;
                    }
                    return _senseTypes;
                }

                public class Loadout
                {
                    public string LoadoutID { get; set; }

                    [JsonProperty(PropertyName = "Potential names for zombies using this loadout (chosen at random)")]
                    public string[] Names { get; set; }

                    [JsonProperty(PropertyName = "Damage multiplier")]
                    public float DamageMultiplier { get; set; }

                    [JsonProperty(PropertyName = "Aim cone scale (for projectile weapons)")]
                    public float AimConeScale { get; set; }

                    public VitalStats Vitals { get; set; }

                    public MovementStats Movement { get; set; }

                    public SensoryStats Sensory { get; set; }

                    public List<LootTable.InventoryItem> BeltItems { get; set; }

                    public List<LootTable.InventoryItem> MainItems { get; set; }

                    public List<LootTable.InventoryItem> WearItems { get; set; }
                   
                    public class VitalStats
                    {
                        public float Health { get; set; }
                    }

                    public class MovementStats
                    {
                        public float Speed { get; set; }

                        public float Acceleration { get; set; }

                        [JsonProperty(PropertyName = "Turn speed")]
                        public float TurnSpeed { get; set; }

                        [JsonProperty(PropertyName = "Speed multiplier - Slowest")]
                        public float SlowestSpeedFraction { get; set; }

                        [JsonProperty(PropertyName = "Speed multiplier - Slow")]
                        public float SlowSpeedFraction { get; set; }

                        [JsonProperty(PropertyName = "Speed multiplier - Normal")]
                        public float NormalSpeedFraction { get; set; }

                        [JsonProperty(PropertyName = "Speed multiplier - Fast")]
                        public float FastSpeedFraction { get; set; }

                        [JsonProperty(PropertyName = "Speed multiplier - Low health")]
                        public float LowHealthMaxSpeedFraction { get; set; }

                        public void ApplySettingsToNavigator(BaseNavigator baseNavigator)
                        {
                            baseNavigator.Acceleration = Acceleration;
                            baseNavigator.FastSpeedFraction = FastSpeedFraction;
                            baseNavigator.LowHealthMaxSpeedFraction = LowHealthMaxSpeedFraction;
                            baseNavigator.NormalSpeedFraction = NormalSpeedFraction;
                            baseNavigator.SlowestSpeedFraction = SlowestSpeedFraction;
                            baseNavigator.SlowSpeedFraction = SlowSpeedFraction;
                            baseNavigator.Speed = Speed;
                            baseNavigator.TurnSpeed = TurnSpeed;

                            baseNavigator.topologyPreference = (TerrainTopology.Enum)1673010749;

                            if (Configuration.Member.CanSwim)
                            {
                                baseNavigator.MaxWaterDepth = 30f;
                                baseNavigator.SwimmingSpeedMultiplier = 0.4f;

                                baseNavigator.topologyPreference |= TerrainTopology.Enum.Ocean | TerrainTopology.Enum.Lake | TerrainTopology.Enum.River;
                            }                            
                        }
                    }

                    public class SensoryStats
                    {
                        [JsonProperty(PropertyName = "Attack range multiplier")]
                        public float AttackRangeMultiplier { get; set; }

                        [JsonProperty(PropertyName = "Sense range")]
                        public float SenseRange { get; set; } 

                        [JsonProperty(PropertyName = "Listen range")]
                        public float ListenRange { get; set; }
                       
                        [JsonProperty(PropertyName = "Target lost range")]
                        public float TargetLostRange { get; set; }

                        [JsonProperty(PropertyName = "Ignore sneaking outside of vision range")]
                        public bool IgnoreNonVisionSneakers { get; set; }

                        [JsonProperty(PropertyName = "Vision cone (0 - 180 degrees)")]
                        public float VisionCone { get; set; }

                        [JsonProperty(PropertyName = "Ignore players in safe zone")]
                        public bool IgnoreSafeZonePlayers { get; set; }

                        public void ApplySettingsToBrain(ZombieNPCBrain zombieNPCBrain)
                        {
                            zombieNPCBrain.AttackRangeMultiplier = 1f;
                            zombieNPCBrain.SenseRange = SenseRange;
                            zombieNPCBrain.ListenRange = ListenRange;
                            zombieNPCBrain.TargetLostRange = TargetLostRange;
                            zombieNPCBrain.CheckVisionCone = IgnoreNonVisionSneakers;
                            zombieNPCBrain.IgnoreNonVisionSneakers = IgnoreNonVisionSneakers;
                            zombieNPCBrain.IgnoreSafeZonePlayers = IgnoreSafeZonePlayers;                            
                           
                            zombieNPCBrain.VisionCone = Vector3.Dot(Vector3.forward, Quaternion.Euler(0f, VisionCone, 0f) * Vector3.forward);
                        }
                    }

                    [JsonIgnore]
                    private static Hash<string, float> _effectiveRangeDefaults = new Hash<string, float>();

                    [JsonIgnore]
                    private static ItemDefinition _glowEyes;

                    [JsonIgnore]
                    public static ItemDefinition GlowEyes
                    {
                        get
                        {
                            if (_glowEyes == null)
                                _glowEyes = ItemManager.FindItemDefinition("gloweyes");
                            return _glowEyes;
                        }
                    }

                    public Loadout() 
                    {
                        Names = new string[] { "Zombie" };

                        DamageMultiplier = 1f;

                        AimConeScale = 2f;

                        Vitals = new VitalStats() 
                        { 
                            Health = 200f 
                        };

                        Movement = new MovementStats()
                        {
                            Speed = 6.2f,
                            Acceleration = 12f,
                            TurnSpeed = 120f,
                            FastSpeedFraction = 1f,
                            NormalSpeedFraction = 0.5f,
                            SlowSpeedFraction = 0.3f,
                            SlowestSpeedFraction = 0.16f,
                            LowHealthMaxSpeedFraction = 0.5f,
                        };

                        Sensory = new SensoryStats()
                        {
                            AttackRangeMultiplier = 1.5f,
                            IgnoreNonVisionSneakers = true,
                            IgnoreSafeZonePlayers = true,
                            ListenRange = 20f,
                            SenseRange = 30f,
                            TargetLostRange = 40f,
                            VisionCone = 135f
                        };

                        BeltItems = new List<LootTable.InventoryItem>();
                        MainItems = new List<LootTable.InventoryItem>();
                        WearItems = new List<LootTable.InventoryItem>();
                    }

                    public Loadout(string loadoutID) : this()
                    {
                        LoadoutID = loadoutID;
                    }

                    internal void GiveToPlayer(ZombieNPC zombieNPC)
                    {
                        if (zombieNPC == null)                        
                            return;

                        zombieNPC.inventory.Strip();

                        foreach (LootTable.InventoryItem inventoryItem in BeltItems)
                        {
                            Item item = inventoryItem.Give(zombieNPC.inventory.containerBelt);

                            if (item != null)
                            {
                                HeldEntity heldEntity = item.GetHeldEntity() as HeldEntity;
                                if (heldEntity != null)
                                {
                                    if (heldEntity is BaseProjectile)
                                    {
                                        if (!_effectiveRangeDefaults.ContainsKey(item.info.shortname))
                                            _effectiveRangeDefaults[item.info.shortname] = (heldEntity as BaseProjectile).effectiveRange;

                                        float effectiveRange;
                                        if (ProjectileEffectiveRange.TryGetValue(item.info.shortname, out effectiveRange))
                                            (heldEntity as BaseProjectile).effectiveRange = effectiveRange;
                                        else (heldEntity as BaseProjectile).effectiveRange *= 1.25f;
                                    }

                                    if (heldEntity is BaseMelee)
                                    {
                                        if (!_effectiveRangeDefaults.ContainsKey(item.info.shortname))
                                            _effectiveRangeDefaults[item.info.shortname] = (heldEntity as BaseMelee).effectiveRange;

                                        (heldEntity as BaseMelee).effectiveRange *= 1.5f;
                                    }
                                }
                            }
                        }

                        foreach (LootTable.InventoryItem inventoryItem in MainItems)
                            inventoryItem.Give(zombieNPC.inventory.containerMain);

                        foreach (LootTable.InventoryItem inventoryItem in WearItems)
                            inventoryItem.Give(zombieNPC.inventory.containerWear);

                        if (Configuration.Member.GiveGlowEyes)
                        { 
                            Item item = ItemManager.Create(GlowEyes);
                            if (!item.MoveToContainer(zombieNPC.inventory.containerWear))
                                item.Remove(0f);
                        }
                    }

                    private static readonly Hash<string, float> ProjectileEffectiveRange = new Hash<string, float>
                    {
                        ["bow.compound"] = 20,
                        ["bow.hunting"] = 20,
                        ["crossbow"] = 20,
                        ["flamethrower"] = 8,                        
                        ["gun.water"] = 10,
                        ["lmg.m249"] = 150,
                        ["multiplegrenadelauncher"] = 20,
                        ["pistol.eoka"] = 5,
                        ["pistol.m92"] = 15,
                        ["pistol.nailgun"] = 10,
                        ["pistol.python"] = 15,
                        ["pistol.revolver"] = 15,
                        ["pistol.semiauto"] = 15,
                        ["pistol.water"] = 10,
                        ["rifle.ak"] = 30,
                        ["rifle.bolt"] = 80,
                        ["rifle.l96"] = 100,
                        ["rifle.lr300"] = 40,
                        ["rifle.m39"] = 30,
                        ["rifle.semiauto"] = 20,
                        ["rocket.launcher"] = 20,
                        ["shotgun.double"] = 15,
                        ["shotgun.pump"] = 15,
                        ["shotgun.spas12"] = 15,
                        ["shotgun.waterpipe"] = 10,
                        ["smg.2"] = 20,
                        ["smg.mp5"] = 20,
                        ["smg.thompson"] = 20,
                        ["snowballgun"] = 10,
                        ["speargun"] = 10,
                    };

                    public static bool GetDefaultEffectiveRange(string shortname, out float value) => _effectiveRangeDefaults.TryGetValue(shortname, out value);
                }
            }

            public class LootTable
            {
                [JsonProperty(PropertyName = "Drop inventory on death instead of random loot")]
                public bool DropInventory { get; set; }

                [JsonProperty(PropertyName = "Random loot table")]
                public RandomLoot Random { get; set; }

                [JsonProperty(PropertyName = "Dropped inventory item blacklist (shortnames)")]
                public string[] DroppedBlacklist { get; set; }

                public class InventoryItem
                {
                    public string Shortname { get; set; }
                    public ulong SkinID { get; set; }
                    public int Amount { get; set; }

                    [JsonProperty(PropertyName = "Attachments", NullValueHandling = NullValueHandling.Ignore)]
                    public InventoryItem[] SubSpawn { get; set; }

                    public Item Give(ItemContainer itemContainer)
                    {
                        Item item = ItemManager.CreateByName(Shortname, Amount, SkinID);
                        if (item == null)
                            return null;

                        if (!item.MoveToContainer(itemContainer))
                        {
                            item.Remove(0f);
                            return null;
                        }

                        if (item.contents != null && SubSpawn?.Length > 0)
                        {
                            for (int i = 0; i < SubSpawn.Length; i++)                            
                                SubSpawn[i].Give(item.contents);                            
                        }

                        return item;
                    }
                }

                public class RandomLoot
                {
                    [JsonProperty(PropertyName = "Minimum amount of items to spawn")]
                    public int Minimum { get; set; }

                    [JsonProperty(PropertyName = "Maximum amount of items to spawn")]
                    public int Maximum { get; set; }

                    public List<LootDefinition> List { get; set; }

                    public class LootDefinition
                    {
                        public string Shortname { get; set; }

                        public int Minimum { get; set; }

                        public int Maximum { get; set; }

                        public ulong SkinID { get; set; }

                        [JsonProperty(PropertyName = "Spawn as blueprint")]
                        public bool IsBlueprint { get; set; }

                        [JsonProperty(PropertyName = "Probability (0.0 - 1.0)")]
                        public float Probability { get; set; }

                        [JsonProperty(PropertyName = "Minimum condition (0.0 - 1.0)")]
                        public float MinCondition { get; set; } = 1f;

                        [JsonProperty(PropertyName = "Maximum condition (0.0 - 1.0)")]
                        public float MaxCondition { get; set; } = 1f;

                        [JsonProperty(PropertyName = "Spawn with")]
                        public LootDefinition Required { get; set; }

                        [JsonIgnore]
                        private ItemDefinition _blueprintDefinition;

                        [JsonIgnore]
                        private ItemDefinition BlueprintDefinition
                        {
                            get
                            {
                                if (_blueprintDefinition == null)
                                    _blueprintDefinition = ItemManager.FindItemDefinition("blueprintbase");
                                return _blueprintDefinition;
                            }
                        }

                        private int GetAmount()
                        {
                            if (Maximum <= 0f || Maximum <= Minimum)
                                return Minimum;

                            return UnityEngine.Random.Range(Minimum, Maximum);
                        }

                        public void Create(ItemContainer container)
                        {
                            Item item;

                            if (!IsBlueprint)                            
                                item = ItemManager.CreateByName(Shortname, GetAmount(), SkinID);
                            else
                            {
                                item = ItemManager.Create(BlueprintDefinition);
                                item.blueprintTarget = ItemManager.FindItemDefinition(Shortname).itemid;
                            }

                            if (item != null)
                            {
                                if (!IsBlueprint)                                
                                    item.conditionNormalized = UnityEngine.Random.Range(Mathf.Clamp01(MinCondition), Mathf.Clamp01(MaxCondition));
                                
                                item.OnVirginSpawn();
                                if (!item.MoveToContainer(container, -1, true))
                                    item.Remove(0f);
                            }

                            if (Required != null)
                                Required.Create(container);
                        }
                    }
                }
            }

            public class MonumentSpawn
            {
                public MonumentSettings Airfield { get; set; }
                public MonumentSettings Dome { get; set; }
                public MonumentSettings Junkyard { get; set; }
                public MonumentSettings LargeHarbor { get; set; }
                public MonumentSettings GasStation { get; set; }
                public MonumentSettings Powerplant { get; set; }
                public MonumentSettings StoneQuarry { get; set; }
                public MonumentSettings SulfurQuarry { get; set; }
                public MonumentSettings HQMQuarry { get; set; }
                public MonumentSettings Radtown { get; set; }
                public MonumentSettings LaunchSite { get; set; }
                public MonumentSettings Satellite { get; set; }
                public MonumentSettings SmallHarbor { get; set; }
                public MonumentSettings Supermarket { get; set; }
                public MonumentSettings Trainyard { get; set; }
                public MonumentSettings Tunnels { get; set; }
                public MonumentSettings Warehouse { get; set; }
                public MonumentSettings WaterTreatment { get; set; }

                public List<CustomSpawnPoints> Custom { get; set; }

                public class MonumentSettings : SpawnSettings
                {
                    [JsonProperty(PropertyName = "Enable spawns at this monument")]
                    public bool Enabled { get; set; }
                }

                public class CustomSpawnPoints : MonumentSettings
                {
                    public SerializedVector Location { get; set; }

                    public class SerializedVector
                    {
                        public float X { get; set; }
                        public float Y { get; set; }
                        public float Z { get; set; }

                        public SerializedVector() { }

                        public SerializedVector(float x, float y, float z)
                        {
                            this.X = x;
                            this.Y = y;
                            this.Z = z;
                        }

                        public static implicit operator Vector3(SerializedVector v)
                        {
                            return new Vector3(v.X, v.Y, v.Z);
                        }

                        public static implicit operator SerializedVector(Vector3 v)
                        {
                            return new SerializedVector(v.x, v.y, v.z);
                        }
                    }
                }
            }

            public class SpawnSettings
            {
                [JsonProperty(PropertyName = "Distance that this horde can roam from their initial spawn point")]
                public float RoamDistance { get; set; }
                                
                [JsonProperty(PropertyName = "Maximum amount of members in this horde")]
                public int HordeSize { get; set; }

                [JsonProperty(PropertyName = "Horde profile")]
                public string Profile { get; set; }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = Config.ReadObject<ConfigData>();

            if (Configuration.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(Configuration, true);
        }

        protected override void LoadDefaultConfig() => Configuration = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Horde = new ConfigData.HordeOptions
                {
                    InitialMemberCount = 3,
                    MaximumHordes = 5,
                    MaximumMemberCount = 10,
                    GrowthRate = 300,
                    CreateOnDeath = true,
                    ForgetTime = 10f,
                    MergeHordes = true,
                    RespawnTime = 900,
                    SpawnType = "Random",
                    SpawnFile = "",
                    DefaultRoamSpeed = BaseNavigator.NavigationSpeed.Slow.ToString(),
                    LocalRoam = false,
                    RoamDistance = 150,
                    UseProfiles = false,
                    UseSenses = true
                },
                Member = new ConfigData.MemberOptions
                {
                    IgnoreSleepers = false,
                    TargetAnimals = true,
                    TargetedByAnimals = true,
                    TargetedByNPCs = true,
                    TargetedByTurrets = false,
                    TargetedByAPC = false,
                    TargetNPCs = true,
                    TargetHumanNPCs = false,
                    GiveGlowEyes = true,
                    HeadshotKills = true,
                    Loadouts = BuildDefaultLoadouts(),
                    KillUnderWater = true,
                    TargetedByPeaceKeeperTurrets = true,
                    EnableDormantSystem = true
                },
                Loot = new ConfigData.LootTable
                {
                    DropInventory = false,
                    Random = BuildDefaultLootTable(),
                    DroppedBlacklist = new string[] { "exampleitem.shortname1", "exampleitem.shortname2" }
                },
                TimedSpawns = new ConfigData.TimedSpawnOptions
                {
                    Enabled = false,
                    Despawn = true,
                    Start = 18f,
                    End = 6f
                },
                HordeProfiles = new Dictionary<string, List<string>>
                {
                    ["Profile1"] = new List<string> { "loadout-1", "loadout-2", "loadout-3" },
                    ["Profile2"] = new List<string> { "loadout-2", "loadout-3", "loadout-4" },
                },
                Monument = new ConfigData.MonumentSpawn
                {
                    Airfield = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 85,
                        HordeSize = 10,
                        Profile = "",
                    },
                    Dome = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 50,
                        HordeSize = 10,
                    },
                    Junkyard = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 100,
                        HordeSize = 10,
                        Profile = ""
                    },
                    GasStation = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 40,
                        HordeSize = 10,
                        Profile = ""
                    },
                    LargeHarbor = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 120,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Powerplant = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 120,
                        HordeSize = 10,
                        Profile = ""
                    },
                    HQMQuarry = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 40,
                        HordeSize = 10,
                        Profile = ""
                    },
                    StoneQuarry = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 40,
                        HordeSize = 10,
                        Profile = ""
                    },
                    SulfurQuarry = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 40,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Radtown = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 85,
                        HordeSize = 10,
                        Profile = ""
                    },
                    LaunchSite = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 140,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Satellite = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 60,
                        HordeSize = 10,
                        Profile = ""
                    },
                    SmallHarbor = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 85,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Supermarket = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 20,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Trainyard = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 100,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Tunnels = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 90,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Warehouse = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 40,
                        HordeSize = 10,
                        Profile = ""
                    },
                    WaterTreatment = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 120,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Custom = new List<ConfigData.MonumentSpawn.CustomSpawnPoints>()
                    {
                        new ConfigData.MonumentSpawn.CustomSpawnPoints
                        {
                            Enabled = false,
                            HordeSize = 3,
                            Location = new ConfigData.MonumentSpawn.CustomSpawnPoints.SerializedVector
                            {
                                X = 0f,
                                Y = 0f,
                                Z = 0f
                            },
                            Profile = string.Empty,
                            RoamDistance = -1
                        },
                        new ConfigData.MonumentSpawn.CustomSpawnPoints
                        {
                            Enabled = false,
                            HordeSize = 3,
                            Location = new ConfigData.MonumentSpawn.CustomSpawnPoints.SerializedVector
                            {
                                X = 0f,
                                Y = 0f,
                                Z = 0f
                            },
                            Profile = string.Empty,
                            RoamDistance = -1
                        }
                    }
                },
                Version = Version
            };
        }

        private List<ConfigData.MemberOptions.Loadout> BuildDefaultLoadouts()
        {
            List<ConfigData.MemberOptions.Loadout> list = new List<ConfigData.MemberOptions.Loadout>();

            PlayerInventoryProperties[] loadouts = DefaultLoadouts;
            if (loadouts != null)
            {
                for (int i = 0; i < loadouts.Length; i++)
                {
                    PlayerInventoryProperties inventoryProperties = loadouts[i];

                    ConfigData.MemberOptions.Loadout loadout = new ConfigData.MemberOptions.Loadout($"loadout-{list.Count}");

                    for (int belt = 0; belt < inventoryProperties.belt.Count; belt++)
                    {
                        PlayerInventoryProperties.ItemAmountSkinned item = inventoryProperties.belt[belt];

                        loadout.BeltItems.Add(new ConfigData.LootTable.InventoryItem() { Shortname = item.itemDef.shortname, SkinID = item.skinOverride, Amount = (int)item.amount });
                    }

                    for (int main = 0; main < inventoryProperties.main.Count; main++)
                    {
                        PlayerInventoryProperties.ItemAmountSkinned item = inventoryProperties.main[main];

                        loadout.MainItems.Add(new ConfigData.LootTable.InventoryItem() { Shortname = item.itemDef.shortname, SkinID = item.skinOverride, Amount = (int)item.amount });
                    }

                    for (int wear = 0; wear < inventoryProperties.wear.Count; wear++)
                    {
                        PlayerInventoryProperties.ItemAmountSkinned item = inventoryProperties.wear[wear];

                        loadout.WearItems.Add(new ConfigData.LootTable.InventoryItem() { Shortname = item.itemDef.shortname, SkinID = item.skinOverride, Amount = (int)item.amount });
                    }

                    list.Add(loadout);
                }
            }
            return list;
        }

        private ConfigData.LootTable.RandomLoot BuildDefaultLootTable()
        {
            ConfigData.LootTable.RandomLoot randomLoot = new ConfigData.LootTable.RandomLoot();

            randomLoot.Minimum = 3;
            randomLoot.Maximum = 9;
            randomLoot.List = new List<ConfigData.LootTable.RandomLoot.LootDefinition>();

            LootContainer.LootSpawnSlot[] loot = DefaultLootSpawns;
            if (loot != null)
            {
                for (int i = 0; i < loot.Length; i++)
                {
                    LootContainer.LootSpawnSlot lootSpawn = loot[i];

                    for (int y = 0; y < lootSpawn.definition.subSpawn.Length; y++)
                    {
                        LootSpawn.Entry entry = lootSpawn.definition.subSpawn[y];                                               

                        for (int c = 0; c < entry.category.items.Length; c++)
                        {
                            ItemAmountRanged itemAmountRanged = entry.category.items[c];

                            ConfigData.LootTable.RandomLoot.LootDefinition lootDefinition = new ConfigData.LootTable.RandomLoot.LootDefinition();
                            lootDefinition.Probability = lootSpawn.probability;
                            lootDefinition.Shortname = itemAmountRanged.itemDef.shortname;
                            lootDefinition.Minimum = (int)itemAmountRanged.amount;
                            lootDefinition.Maximum = (int)itemAmountRanged.maxAmount;
                            lootDefinition.SkinID = 0;
                            lootDefinition.IsBlueprint = itemAmountRanged.itemDef.spawnAsBlueprint;
                            lootDefinition.Required = null;

                            randomLoot.List.Add(lootDefinition);
                        }
                    }
                }
            }
            return randomLoot;
        }

        protected override void SaveConfig() => Config.WriteObject(Configuration, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (Configuration.Version < new Core.VersionNumber(0, 2, 0))
                Configuration = baseConfig;

            if (Configuration.Version < new Core.VersionNumber(0, 2, 1))
                Configuration.Loot.Random = baseConfig.Loot.Random;

            if (Configuration.Version < new Core.VersionNumber(0, 2, 2))
            {
                for (int i = 0; i < Configuration.Member.Loadouts.Count; i++)                
                    Configuration.Member.Loadouts[i].LoadoutID = $"loadout-{i}";

                Configuration.Horde.LocalRoam = false;
                Configuration.Horde.RoamDistance = 150;
                Configuration.Horde.UseProfiles = false;

                Configuration.HordeProfiles = baseConfig.HordeProfiles;

                Configuration.Monument.Airfield.Profile = string.Empty;
                Configuration.Monument.Dome.Profile = string.Empty;
                Configuration.Monument.GasStation.Profile = string.Empty;
                Configuration.Monument.HQMQuarry.Profile = string.Empty;
                Configuration.Monument.Junkyard.Profile = string.Empty;
                Configuration.Monument.LargeHarbor.Profile = string.Empty;
                Configuration.Monument.LaunchSite.Profile = string.Empty;
                Configuration.Monument.Powerplant.Profile = string.Empty;
                Configuration.Monument.Radtown.Profile = string.Empty;
                Configuration.Monument.Satellite.Profile = string.Empty;
                Configuration.Monument.SmallHarbor.Profile = string.Empty;
                Configuration.Monument.StoneQuarry.Profile = string.Empty;
                Configuration.Monument.SulfurQuarry.Profile = string.Empty;
                Configuration.Monument.Supermarket.Profile = string.Empty;
                Configuration.Monument.Trainyard.Profile = string.Empty;
                Configuration.Monument.Tunnels.Profile = string.Empty;
                Configuration.Monument.Warehouse.Profile = string.Empty;
                Configuration.Monument.WaterTreatment.Profile = string.Empty;
            }

            if (Configuration.Version < new Core.VersionNumber(0, 2, 13))
                Configuration.TimedSpawns = baseConfig.TimedSpawns;

            if (Configuration.Version < new Core.VersionNumber(0, 2, 18))            
                Configuration.Member.TargetedByPeaceKeeperTurrets = Configuration.Member.TargetedByTurrets; 

            if (Configuration.Version < new Core.VersionNumber(0, 2, 30))
            {
                if (Configuration.Horde.SpawnType == "RandomSpawns" || Configuration.Horde.SpawnType == "Default")
                    Configuration.Horde.SpawnType = "Random";
            }

            if (Configuration.Version < new Core.VersionNumber(0, 2, 31))
            {
                if (string.IsNullOrEmpty(Configuration.Horde.SpawnType))
                    Configuration.Horde.SpawnType = "Random";
            }

            if (Configuration.Version < new Core.VersionNumber(0, 3, 0))
            {
                Configuration.Horde.UseSenses = true;
            }

            if (Configuration.Version < new Core.VersionNumber(0, 3, 5))
            {
                Configuration.Loot.DroppedBlacklist = baseConfig.Loot.DroppedBlacklist;
            }

            if (Configuration.Version < new Core.VersionNumber(0, 4, 0))
            {
                foreach(ConfigData.MemberOptions.Loadout loadout in Configuration.Member.Loadouts)
                {
                    loadout.AimConeScale = 2f;

                    loadout.Movement = new ConfigData.MemberOptions.Loadout.MovementStats
                    {
                        Speed = 6.2f,
                        Acceleration = 12f,
                        TurnSpeed = 120f,
                        FastSpeedFraction = 1f,
                        NormalSpeedFraction = 0.5f,
                        SlowSpeedFraction = 0.3f,
                        SlowestSpeedFraction = 0.16f,
                        LowHealthMaxSpeedFraction = 0.5f
                    };

                    loadout.Sensory = new ConfigData.MemberOptions.Loadout.SensoryStats
                    {
                        AttackRangeMultiplier = 1.5f,
                        IgnoreNonVisionSneakers = true,
                        IgnoreSafeZonePlayers = true,
                        ListenRange = 20f,
                        SenseRange = 30f,
                        TargetLostRange = 40f,
                        VisionCone = 135f
                    };
                }

                if (Configuration.Loot.DroppedBlacklist == null)
                    Configuration.Loot.DroppedBlacklist = baseConfig.Loot.DroppedBlacklist;

                Configuration.Horde.DefaultRoamSpeed = BaseNavigator.NavigationSpeed.Slow.ToString();
                Configuration.Member.EnableDormantSystem = true;
                Configuration.Member.TargetAnimals = true;
                Configuration.Member.TargetedByNPCs = true;
                Configuration.Member.TargetedByPeaceKeeperTurrets = true;
            }

            if (Configuration.Version < new Core.VersionNumber(0, 4, 2))
                Configuration.Member.TargetedByAnimals = true;

            if (Configuration.Version < new Core.VersionNumber(0, 4, 8))
            {
                if (Configuration.Monument.Custom == null)
                    Configuration.Monument.Custom = baseConfig.Monument.Custom;

                Configuration.Member.CanSwim = true;
                Configuration.Member.KillUnderWater = false;
            }
            
            Configuration.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion
    }
}
