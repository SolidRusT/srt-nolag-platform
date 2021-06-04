using System.Collections.Generic;
using System.Collections;
using System;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Core;
using UnityEngine;
using VLB;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Guarded Crate", "Bazz3l", "1.4.8")]
    [Description("Spawns hackable crate events at random locations guarded by scientists.")]
    public class GuardedCrate : RustPlugin
    {
        #region Fields

        private const string CRATE_PREFAB = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";
        private const string MARKER_PREFAB = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        private const string CHUTE_PREFAB = "assets/prefabs/misc/parachute/parachute.prefab";
        private const string NPC_PREFAB = "assets/prefabs/npc/scientist/scientist.prefab";
        private const string PLANE_PREFAB = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
        private const string USE_PERM = "guardedcrate.use";
        
        private static readonly LayerMask CollisionLayer = LayerMask.GetMask("Terrain", "World", "Water", "Tree", "Debris", "Clutter", "Default", "Resource", "Construction", "Deployed");
        private static readonly int WorldLayer = LayerMask.GetMask("World");
        
        private readonly Dictionary<BaseEntity, CrateEvent> _entities = new Dictionary<BaseEntity, CrateEvent>();
        private readonly HashSet<CrateEvent> _events = new HashSet<CrateEvent>();
        private PluginConfig _config;
        private PluginData _stored;
        private static GuardedCrate _plugin;

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
                    throw new Exception();
                }
            }
            catch
            {
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" + "The error configuration file was saved in the .jsonError extension");
                
                LoadDefaultConfig();
            }
        }
        
        protected override void SaveConfig() => Config.WriteObject(_config);    
        
        private class PluginConfig
        {
            [JsonProperty("AutoEvent (enables auto event spawns)")]
            public bool EnableAutoEvent;
            
            [JsonProperty("AutoEventDuration (time until new event spawns)")]
            public float AutoEventDuration;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    EnableAutoEvent = true,
                    AutoEventDuration = 1800f
                };
            }
        }

        #endregion

        #region Storage

        private class PluginData
        {
            public readonly List<EventSetting> Events = new List<EventSetting>();

            public static PluginData LoadData()
            {
                PluginData data;
                
                try
                {
                    data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>("GuardedCrate");

                    if (data == null)
                    {
                        throw new JsonException();
                    }
                }
                catch (Exception e)
                {
                    data = new PluginData();
                }
                
                return data;
            }

            public void Save()
            {
                Interface.Oxide.DataFileSystem.WriteObject("GuardedCrate", this);
            }
        }
        
        private class EventSetting
        {
            [JsonProperty("EventDuration (duration the event will be active for)")]
            public float EventDuration;
            
            [JsonProperty("EventName (event name)")]
            public string EventName;

            [JsonProperty("AutoHack (enables auto hacking of crates when an event is finished)")]
            public bool AutoHack = true;
            
            [JsonProperty("AutoHackSeconds (countdown for crate to unlock in seconds)")]
            public float AutoHackSeconds = 60f;

            [JsonProperty("UseKits (use custom kits plugin)")]
            public bool UseKits = false;
            
            [JsonProperty("Kits (custom kits)")]
            public List<string> Kits = new List<string>();
            
            [JsonProperty("NpcName (custom name)")]
            public string NpcName;
            
            [JsonProperty("NpcCount (number of guards to spawn)")]
            public int NpcCount;
            
            [JsonProperty("NpcHealth (health guards spawn with)")]
            public float NpcHealth;
            
            [JsonProperty("NpcRadius (max distance guards will roam)")]
            public float NpcRadius;
            
            [JsonProperty("NpcAggression (max aggression distance guards will target)")]
            public float NpcAggression;

            [JsonProperty("MarkerColor (marker color)")]
            public string MarkerColor;
            
            [JsonProperty("MarkerBorderColor (marker border color)")]
            public string MarkerBorderColor;
            
            [JsonProperty("MarkerOpacity (marker opacity)")]
            public float MarkerOpacity = 1f;

            [JsonProperty("UseLoot (use custom loot table)")]
            public bool UseLoot = false;
            
            [JsonProperty("MaxLootItems (max items to spawn in crate)")]
            public int MaxLootItems = 6;
            
            [JsonProperty("CustomLoot (items to spawn in crate)")]
            public List<LootItem> CustomLoot = new List<LootItem>();
        }

        private class LootItem
        {
            public string Shortname;
            public int MinAmount;
            public int MaxAmount;

            public Item CreateItem() => ItemManager.CreateByName(Shortname, Random.Range(MinAmount, MaxAmount));
        }

        #endregion
        
        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages (new Dictionary<string, string>
            {
                { "InvalidSyntax", "gc start|stop" },
                { "Permission", "No permission" },
                { "CreateEvent", "<color=#DC143C>Guarded Crate</color>: New event starting stand by." },
                { "CleanEvents", "<color=#DC143C>Guarded Crate</color>: Cleaning up events." },
                { "EventStarted", "<color=#DC143C>Guarded Crate</color>: <color=#EDDf45>{0}</color>, event started at <color=#EDDf45>{1}</color>, eliminate the guards before they leave in <color=#EDDf45>{2}</color>." },
                { "EventEnded", "<color=#DC143C>Guarded Crate</color>: The event ended at the location <color=#EDDf45>{0}</color>, <color=#EDDf45>{1}</color> cleared the event!" },
                { "EventClear", "<color=#DC143C>Guarded Crate</color>: The event ended at <color=#EDDf45>{0}</color>; You were not fast enough; better luck next time!" },
            }, this);
        }

        #endregion
        
        #region Oxide

        private void OnServerInitialized()
        {
            permission.RegisterPermission(USE_PERM, this);

            RegisterCommands();
            RegisterDefaults();

            if (_config.EnableAutoEvent)
            {
                timer.Every(_config.AutoEventDuration, () => StartEvent(null, new string[] {}));
            }
            
            timer.Every(30f, RefreshEvents);
        }

        private void Init()
        {
            _plugin = this;
            _stored = PluginData.LoadData();
        }

        private void Unload()
        {
            StopEvents(null);

            _plugin = null;
        }

        private void OnEntityDeath(NPCPlayerApex npc, HitInfo hitInfo) => 
            FindEntityEvent(npc)
                ?.OnNPCDeath(npc, hitInfo?.InitiatorPlayer);

        private void OnEntityKill(NPCPlayerApex npc) => 
            FindEntityEvent(npc)
                ?.OnNPCDeath(npc, null);

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target) => 
            OnCanBuild(planner.GetOwnerPlayer());

        #endregion
        
        #region Core

        private void RegisterDefaults()
        {
            if (_stored.Events == null || _stored.Events.Count != 0) return;

            _stored.Events.Add(new EventSetting
            {
                EventDuration = 800f,
                EventName = "Low Level",
                NpcAggression = 120f,
                NpcRadius = 15f,
                NpcCount = 6,
                NpcHealth = 100,
                NpcName = "Easy Guard",
                MarkerColor = "#32a844",
                MarkerBorderColor = "#000000",
                MarkerOpacity = 0.9f
            });
            
            _stored.Events.Add(new EventSetting
            {
                EventDuration = 800f,
                EventName = "Medium Level",
                NpcAggression = 120f,
                NpcRadius = 15f,
                NpcCount = 8,
                NpcHealth = 150,
                NpcName = "Medium Guard",
                MarkerColor = "#eddf45",
                MarkerBorderColor = "#000000",
                MarkerOpacity = 0.9f
            });
            
            _stored.Events.Add(new EventSetting {
                EventDuration = 1800f,
                EventName = "Hard Level",
                NpcAggression = 150f,
                NpcRadius = 50f,
                NpcCount = 10,
                NpcHealth = 200, 
                NpcName = "Hard Guard",
                MarkerColor = "#3060d9",
                MarkerBorderColor = "#000000",
                MarkerOpacity = 0.9f
            });
            
            _stored.Events.Add(new EventSetting {
                EventDuration = 1800f,
                EventName = "Elite Level",
                NpcAggression = 180f,
                NpcRadius = 50f,
                NpcCount = 12,
                NpcHealth = 350, 
                NpcName = "Elite Guard",
                MarkerColor = "#e81728",
                MarkerBorderColor = "#000000",
                MarkerOpacity = 0.9f
            });

            _stored.Save();
        }

        private void RegisterCommands()
        {
            cmd.AddChatCommand("gc", this, GCChatCommand);
            cmd.AddConsoleCommand("gc", this, nameof(GCConsoleCommand));
        }

        #region Event Management

        private void StartEvent(BasePlayer player, string[] args)
        {
            EventSetting settings = _stored.Events.FirstOrDefault(x => x.EventName == string.Join(" ", args)) ?? _stored.Events.GetRandom();

            if (settings == null) return;
            
            new CrateEvent().PreStartEvent(settings);

            if (player == null) return;

            player.ChatMessage(Lang("CreateEvent", player.UserIDString));
        }

        private void StopEvents(BasePlayer player)
        {
            CommunityEntity.ServerInstance.StartCoroutine(DespawnRoutine());

            if (player == null) return;

            player.ChatMessage(Lang("CleanEvents", player.UserIDString));
        }

        private void RefreshEvents()
        {
            for (int i = 0; i < _events.Count; i++)
            {
                CrateEvent crateEvent = _events.ElementAt(i);
                crateEvent?.RefreshEvent();
            }
        }        

        #endregion

        #region Cleanup

        private IEnumerator DespawnRoutine()
        {
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                CrateEvent crateEvent = _events.ElementAt(i);
                crateEvent?.StopEvent();

                yield return CoroutineEx.waitForSeconds(0.25f);
            }
            
            yield return null;
        }        

        #endregion

        #region Cache
        
        private CrateEvent FindEntityEvent(BaseEntity entity)
        {
            CrateEvent crateEvent;

            return _entities.TryGetValue(entity, out crateEvent) ? crateEvent : null;
        }

        private void AddEntity(BaseEntity entity, CrateEvent crateEvent)
        {
            _entities.Add(entity, crateEvent);
        }
        
        private void DelEntity(BaseEntity entity)
        {
            _entities.Remove(entity);
        }

        private void AddEvent(CrateEvent crateEvent)
        {
            _events.Add(crateEvent);
        }

        private void DelEvent(CrateEvent crateEvent)
        {
            _events.Remove(crateEvent);
        }        

        #endregion

        private object OnCanBuild(BasePlayer player)
        {
            if (player != null && _events.FirstOrDefault(x => x.Distance(player.ServerPosition)) != null)
            {
                return false;
            }

            return null;
        }

        private class CrateEvent
        {
            #region Fields

            private readonly List<NPCPlayerApex> _npcPlayers = new List<NPCPlayerApex>();
            private MapMarkerGenericRadius _marker;
            private HackableLockedCrate _crate;
            private Coroutine _coroutine;
            private CargoPlane _plane;
            private Vector3 _position;
            private Timer _eventTimer;
            private EventSetting _settings;            

            #endregion

            #region State Management

            public void PreStartEvent(EventSetting settings)
            {
                _settings = settings;
                
                SpawnPlane();
                
                _plugin?.AddEvent(this);
            }

            public void StartEvent(Vector3 position)
            {
                _position = position;

                SpawnCrate();
                RefillCrate();
                StartSpawnRoutine();
                StartDespawnTimer();

                MessageAll("EventStarted", _settings.EventName, GetGrid(_position), GetTime((int)_settings.EventDuration));
            }

            public void StopEvent(bool completed = false)
            {
                _eventTimer?.Destroy();

                StopSpawnRoutine();
                DespawnPlane();
                DespawnMarker();
                DespawnCrate(completed);
                DespawnAI();

                _plugin?.DelEvent(this);
            }

            public void RefreshEvent()
            {
                if (!IsValid(_marker)) return;

                _marker.SendUpdate();
            }            

            #endregion

            #region Cache

            void CacheAdd(NPCPlayerApex player)
            {
                _npcPlayers.Add(player);

                _plugin.AddEntity(player, this);
            }

            void CacheDel(NPCPlayerApex player)
            {
                if (player == null) return;

                _npcPlayers.Remove(player);

                _plugin.DelEntity(player);
            }

            #endregion

            #region Coroutine

            private void StartSpawnRoutine()
            {
                _coroutine = CommunityEntity.ServerInstance.StartCoroutine(SpawnAI());
            }
            
            private void StopSpawnRoutine()
            {
                if (_coroutine == null) return;
                
                CommunityEntity.ServerInstance.StopCoroutine(_coroutine);
                    
                _coroutine = null;
            }            

            #endregion

            #region Timer

            private void StartDespawnTimer()
            {
                _eventTimer = _plugin.timer.Once(_settings.EventDuration, () => StopEvent());
            }
            
            private void ResetDespawnTimer()
            {
                _eventTimer?.Destroy();
                
                StartDespawnTimer();
            }            

            #endregion

            #region Cargo Plane

            private void SpawnPlane()
            {
                _plane = GameManager.server.CreateEntity(PLANE_PREFAB) as CargoPlane;
                if (_plane == null) return;
                
                _plane.Spawn();
                _plane.gameObject.GetOrAddComponent<CargoComponent>().SetEvent(this);
            }            

            #endregion

            #region AI

            private IEnumerator SpawnAI()
            {
                for (int i = 0; i < _settings.NpcCount; i++)
                {
                    Vector3 position = GetPointNavmesh(_position, 8f, (360 / _settings.NpcCount * i));
                    
                    if (position == Vector3.zero) continue;

                    SpawnNpc(position, Quaternion.LookRotation(position - _position));
                    
                    yield return CoroutineEx.waitForSeconds(0.25f);
                }
            }
            
            private void SpawnNpc(Vector3 position, Quaternion rotation)
            {
                NPCPlayerApex npc = (NPCPlayerApex) GameManager.server.CreateEntity(NPC_PREFAB, position, rotation);
                if (npc == null) return;

                npc.enableSaving = false;
                npc.RadioEffect = new GameObjectRef();
                npc.DeathEffect = new GameObjectRef();
                npc.displayName = _settings.NpcName;  
                npc.startHealth = _settings.NpcHealth;
                npc.InitializeHealth(_settings.NpcHealth, _settings.NpcHealth);
                npc.Spawn();
                
                npc.Stats.VisionRange = _settings.NpcAggression + 3f;
                npc.Stats.DeaggroRange = _settings.NpcAggression + 2f;
                npc.Stats.AggressionRange = _settings.NpcAggression + 1f;
                npc.Stats.LongRange = _settings.NpcAggression;
                npc.Stats.MaxRoamRange = _settings.NpcRadius;
                npc.Stats.Hostility = 1f;
                npc.Stats.Defensiveness = 1f;
                npc.Stats.OnlyAggroMarkedTargets = true;
                npc.InitFacts();
                
                npc.gameObject.AddComponent<NavigationComponent>()
                    ?.SetDestination(position);

                CacheAdd(npc);

                npc.Invoke(() => GiveKit(npc, _settings.Kits.GetRandom(), _settings.UseKits), 2f);
            }            

            #endregion
            
            #region Crate

            private void SpawnCrate()
            {
                _marker = GameManager.server.CreateEntity(MARKER_PREFAB, _position) as MapMarkerGenericRadius;
                if (_marker == null) return;

                _marker.enableSaving = false;
                _marker.color1 = GetColor(_settings.MarkerColor);
                _marker.color2 = GetColor(_settings.MarkerBorderColor);
                _marker.alpha  = _settings.MarkerOpacity;
                _marker.radius = 0.5f;
                _marker.Spawn();

                _crate = GameManager.server.CreateEntity(CRATE_PREFAB, _position, Quaternion.identity) as HackableLockedCrate;
                if (_crate == null) return;

                _crate.enableSaving = false;
                _crate.shouldDecay = false;
                _crate.SetWasDropped();
                _crate.Spawn();
                _crate.gameObject.GetOrAddComponent<DropComponent>();

                _marker.SetParent(_crate);
                _marker.transform.localPosition = Vector3.zero;
                _marker.SendUpdate();
            }

            private void RefillCrate()
            {
                if (!_settings.UseLoot || _settings.CustomLoot.Count <= 0) return;

                List<LootItem> lootItems = GenerateLoot();
                
                _crate.inventory.Clear();
                ItemManager.DoRemoves();
                
                _crate.inventory.capacity = lootItems.Count;

                foreach (LootItem lootItem in lootItems)
                {
                    lootItem.CreateItem()
                        ?.MoveToContainer(_crate.inventory);
                }
                
                lootItems.Clear();
            }     
            
            private List<LootItem> GenerateLoot()
            {
                int maxLoopLimit = 1000;

                List<LootItem> lootItems = new List<LootItem>();

                do
                {
                    LootItem lootItem = _settings.CustomLoot.GetRandom();

                    if (lootItems.Contains(lootItem)) continue;
                        
                    lootItems.Add(lootItem);                    
                } 
                while (lootItems.Count < _settings.MaxLootItems && maxLoopLimit-- > 0);

                return lootItems;
            }

            #endregion

            #region Cleanup

            private void DespawnCrate(bool completed = false)
            {
                if (!IsValid(_crate)) return;

                if (!completed)
                {
                    _crate.Kill();
                    return;
                }

                if (_settings.AutoHack)
                {
                    _crate.hackSeconds = HackableLockedCrate.requiredHackSeconds - _settings.AutoHackSeconds;
                    _crate.StartHacking();                    
                }
                
                _crate.shouldDecay = true;
                _crate.RefreshDecay();                
            }
            
            private void DespawnMarker()
            {
                if (!IsValid(_marker)) return;

                _marker.SetParent(null);
                _marker.Kill();
            }
            
            private void DespawnPlane()
            {
                if (!IsValid(_plane)) return;

                _plane.Kill();
            }

            private void DespawnAI()
            {
                List<BaseEntity> npcList = new List<BaseEntity>(_npcPlayers);

                foreach (BaseEntity npc in npcList)
                {
                    if (!IsValid(npc)) continue;

                    npc.Kill();
                }

                _npcPlayers.Clear();
                npcList.Clear();
            }            

            #endregion

            #region Oxide Hooks

            public void OnNPCDeath(NPCPlayerApex npc, BasePlayer player)
            {
                CacheDel(npc);

                if (_npcPlayers.Count > 0)
                {
                    ResetDespawnTimer();
                    return;
                }

                if (player != null)
                {
                    MessageAll("EventEnded", GetGrid(_position), player.displayName);
                    
                    StopEvent(true);
                }
                else
                {
                    MessageAll("EventClear", GetGrid(_position));
                    
                    StopEvent();
                }
            }            

            #endregion
            
            public bool Distance(Vector3 position) => Vector3Ex.Distance2D(position, _position) <= 20f;
        }

        #endregion

        #region Component
        
        private class NavigationComponent : MonoBehaviour
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

        private class CargoComponent : MonoBehaviour
        {
            private CrateEvent _crateEvent;
            private CargoPlane _plane;
            private bool _hasDropped;
            
            private void Awake()
            {
                _plane = GetComponent<CargoPlane>();
                _plane.dropped = true;
            }

            private void Update()
            {
                float time = Mathf.InverseLerp(0.0f, _plane.secondsToTake, _plane.secondsTaken);

                if (!_hasDropped && (double) time >= 0.5)
                {
                    _hasDropped = true;
                    
                    _crateEvent.StartEvent(transform.position);
                    
                    Destroy(this);
                }
            }

            public void SetEvent(CrateEvent crateEvent)
            {
                _crateEvent = crateEvent;
            }
        }

        private class DropComponent : MonoBehaviour
        {
            private BaseEntity _chute;
            private BaseEntity _crate;
            private bool _hasLanded;

            private void Awake()
            {
                _crate = gameObject.GetComponent<BaseEntity>();
                
                _crate.GetComponent<Rigidbody>().drag = 0.7f;

                SpawnChute();
            }

            private void FixedUpdate()
            {
                int size = Physics.OverlapSphereNonAlloc(transform.position, 1f, Vis.colBuffer, CollisionLayer);
                if (size <= 0 || _hasLanded) return;

                _hasLanded = true;

                RemoveChute();
                    
                Destroy(this);
            }

            private void SpawnChute()
            {
                _chute = GameManager.server.CreateEntity(CHUTE_PREFAB, transform.position, Quaternion.identity);
                if (_chute == null) return;

                _chute.enableSaving = false;
                _chute.Spawn();
                _chute.SetParent(_crate);
                _chute.transform.localPosition = Vector3.zero;
                _chute.SendNetworkUpdate();
            }

            private void RemoveChute()
            {
                if (!IsValid(_chute)) return;

                _chute.Kill();
            }
        }

        #endregion
        
        #region Helpers

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        
        private static Vector3 GetPointNavmesh(Vector3 position, float radius, float angle)
        {
            position.x += radius * Mathf.Sin(angle * Mathf.Deg2Rad);
            position.z += radius * Mathf.Cos(angle * Mathf.Deg2Rad);

            RaycastHit hit;

            if (!Physics.Raycast(position, Vector3.down, out hit, float.PositiveInfinity, CollisionLayer))
            {
                return Vector3.zero;
            }

            return IsInOrOnRock(hit.point, "rock_") ? Vector3.zero : hit.point;
        }

        private static bool IsInOrOnRock(Vector3 position, string meshName)
        {
            bool flag = false;
            
            int hits = Physics.OverlapSphereNonAlloc(position, 2f, Vis.colBuffer, WorldLayer, QueryTriggerInteraction.Ignore);
            
            for (int i = 0; i < hits; i++)
            {
                if (Vis.colBuffer[i].name.StartsWith(meshName))
                {
                    flag = true;
                }
                
                Vis.colBuffer[i] = null;
            }

            if (flag)
            {
                return flag;
            }
            
            float y = TerrainMeta.HighestPoint.y + 250f;
            
            RaycastHit hit;
            
            if (Physics.Raycast(position, Vector3.up, out hit, y, WorldLayer, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.name.StartsWith(meshName)) flag = true;
            }
            
            if (!flag && Physics.Raycast(position, Vector3.down, out hit, y, WorldLayer, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.name.StartsWith(meshName)) flag = true;
            }
            
            if (!flag && Physics.Raycast(position + new Vector3(0f, y, 0f), Vector3.down, out hit, y + 1f, WorldLayer, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.name.StartsWith(meshName)) flag = true;
            }
            
            return flag;
        }
        
        private static string GetGrid(Vector3 position)
        {
            Vector2 r = new Vector2(World.Size / 2 + position.x, World.Size / 2 + position.z);
            float x = Mathf.Floor(r.x / 146.3f) % 26;
            float z = Mathf.Floor(World.Size / 146.3f) - Mathf.Floor(r.y / 146.3f);

            return $"{(char)('A' + x)}{z - 1}";
        }
        
        private static Color GetColor(string hex)
        {
            Color color = Color.black;

            ColorUtility.TryParseHtmlString(hex, out color);
            
            return color;
        }

        private static string GetTime(int secs)
        {
            TimeSpan t = TimeSpan.FromSeconds(secs);
            
            return $"{t.Hours:D2}h:{t.Minutes:D2}m:{t.Seconds:D2}s";
        }
        
        private static void GiveKit(NPCPlayerApex npc, string kit, bool giveKit)
        {
            if (!giveKit) return;

            npc.inventory.Strip();
            
            Interface.Oxide.CallHook("GiveKit", npc, kit);

            Item item = npc.inventory.containerBelt.GetSlot(0);
            if (item == null) return;

            npc.UpdateActiveItem(item.uid);
        }

        private static bool IsValid(BaseEntity entity)
        {
            return entity != null && !entity.IsDestroyed;
        }

        private static void MessageAll(string key, params object[] args) => _plugin?.PrintToChat(_plugin.Lang(key, null, args));

        #endregion

        #region Command
        
        private void GCChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, USE_PERM))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
                return;
            }
            
            if (args.Length < 1)
            {
                player.ChatMessage(Lang("InvalidSyntax", player.UserIDString));
                return;
            }
            
            switch (args[0].ToLower())
            {
                case "start":
                    StartEvent(player, args.Skip(1).ToArray());
                    break;
                case "stop":
                    StopEvents(player);
                    break;
                default:
                    player.ChatMessage(Lang("InvalidSyntax", player.UserIDString));
                    break;
            }
        }
        
        private void GCConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon)
            {
                arg.ReplyWith(Lang("NoPermission"));
                return;
            }
            
            if (!arg.HasArgs())
            {
                arg.ReplyWith(Lang("InvalidSyntax"));
                return;
            }
            
            switch (arg.GetString(0).ToLower())
            {
                case "start":
                    StartEvent(null, arg.Args.Skip(1).ToArray());
                    break;
                case "stop":
                    StopEvents(null);
                    break;
                default:
                    arg.ReplyWith(Lang("InvalidSyntax"));
                    break;
            }
        }
        
        #endregion
    }
}