#define USE_HTN_HOOK
//#define DEBUG

using Facepunch;
using Facepunch.Math;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using Oxide.Game.Rust.Cui;
using Oxide.Game.Rust.Libraries;
using Rust;
using Rust.Ai;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using static NPCPlayerApex;

namespace Oxide.Plugins
{
    [Info("Raidable Bases", "nivex", "1.6.6")]
    [Description("Create fully automated raidable bases with npcs.")]
    class RaidableBases : RustPlugin
    {
        [PluginReference]
        private Plugin DangerousTreasures, Vanish, LustyMap, ZoneManager, Economics, ServerRewards, Map, GUIAnnouncements, CopyPaste, Friends, Clans, Kits, TruePVE, Spawns, NightLantern, Wizardry, NextGenPVE, Imperium;

        protected static SingletonBackbone Backbone { get; set; }
        protected RotationCycle Cycle { get; set; } = new RotationCycle();
        public Dictionary<int, List<BaseEntity>> Bases { get; } = new Dictionary<int, List<BaseEntity>>();
        public Dictionary<int, RaidableBase> Raids { get; } = new Dictionary<int, RaidableBase>();
        public Dictionary<BaseEntity, RaidableBase> RaidEntities { get; } = new Dictionary<BaseEntity, RaidableBase>();
        public Dictionary<int, RaidableBase> Indices { get; set; } = new Dictionary<int, RaidableBase>();
        public Dictionary<ulong, RaidableBase> Npcs { get; set; } = new Dictionary<ulong, RaidableBase>();
        protected Dictionary<ulong, DelaySettings> PvpDelay { get; } = new Dictionary<ulong, DelaySettings>();
        private Dictionary<string, List<ulong>> Skins { get; } = new Dictionary<string, List<ulong>>();
        private Dictionary<string, HashSet<ulong>> WorkshopSkins { get; } = new Dictionary<string, HashSet<ulong>>();
        private Dictionary<MonumentInfo, float> monuments { get; set; } = new Dictionary<MonumentInfo, float>();
        private Dictionary<Vector3, ZoneInfo> managedZones { get; set; } = new Dictionary<Vector3, ZoneInfo>();
        private Dictionary<int, MapInfo> mapMarkers { get; set; } = new Dictionary<int, MapInfo>();
        private Dictionary<int, string> lustyMarkers { get; set; } = new Dictionary<int, string>();
        protected Dictionary<RaidableType, RaidableSpawns> raidSpawns { get; set; } = new Dictionary<RaidableType, RaidableSpawns>();
        protected Dictionary<string, float> buyCooldowns { get; set; } = new Dictionary<string, float>();
        protected Dictionary<uint, AutoTurret> ElectricalConnections { get; set; } = new Dictionary<uint, AutoTurret>();
        public StoredData storedData { get; set; } = new StoredData();
        protected Coroutine despawnCoroutine { get; set; }
        protected Coroutine maintainCoroutine { get; set; }
        protected Coroutine scheduleCoroutine { get; set; }
        protected Coroutine gridCoroutine { get; set; }
        private Stopwatch gridStopwatch { get; } = new Stopwatch();
        private StringBuilder _sb { get; } = new StringBuilder();
        protected const float Radius = 25f;
        private bool wiped { get; set; }
        private float lastSpawnRequestTime { get; set; }
        private float gridTime { get; set; }
        private bool IsUnloading { get; set; }
        private int _maxOnce { get; set; }
        private List<string> tryBuyCooldowns { get; set; } = new List<string>();
        private static BuildingTables Buildings { get; set; } = new BuildingTables();
        private const uint LARGE_WOODEN_BOX = 2206646561;
        private const uint SMALL_WOODEN_BOX = 1560881570;
        private const uint COFFIN_STORAGE = 4080262419;
        private const float INSTRUCTION_TIME = 0.01f;
        private bool maintainedEnabled { get; set; }
        private bool scheduledEnabled { get; set; }
        private bool buyableEnabled { get; set; }
        private static List<Vector3> Locations = new List<Vector3>();

        private static bool IsBox(uint prefabID) => prefabID == LARGE_WOODEN_BOX || prefabID == SMALL_WOODEN_BOX || prefabID == COFFIN_STORAGE;

        private enum SpawnResult
        {
            Failure,
            Transfer,
            Success,
            Skipped
        }

        private enum LootType
        {
            Easy,
            Medium,
            Hard,
            Expert,
            Nightmare,
            Default
        }

        public class ZoneInfo
        {
            public Vector3 Position;
            public Vector3 Size;
            public float Distance;
            public OBB OBB;
        }

        private class BuildingTables
        {
            public Dictionary<string, List<TreasureItem>> BaseLoot { get; set; } = new Dictionary<string, List<TreasureItem>>();
            public Dictionary<LootType, List<TreasureItem>> DifficultyLoot { get; set; } = new Dictionary<LootType, List<TreasureItem>>();
            public Dictionary<DayOfWeek, List<TreasureItem>> WeekdayLoot { get; set; } = new Dictionary<DayOfWeek, List<TreasureItem>>();
            public Dictionary<string, BuildingOptions> Profiles { get; set; } = new Dictionary<string, BuildingOptions>();
        }

        private enum TurretState
        {
            Online,
            Offline,
            Unknown
        }

        public class DelaySettings
        {
            public Timer Timer;
            public bool AllowPVP;
            public RaidableBase RaidableBase;
        }

        private enum HandledResult
        {
            Allowed,
            Blocked,
            None
        }

        public enum RaidableType
        {
            None,
            Manual,
            Scheduled,
            Purchased,
            Maintained,
            Grid
        }

        public enum RaidableMode
        {
            Disabled = -1,
            Easy = 0,
            Medium = 1,
            Hard = 2,
            Expert = 3,
            Nightmare = 4,
            Random = 9999
        }

        public class ResourcePath
        {
            public List<uint> Blocks { get; }
            public List<uint> TrueDamage { get; }
            public ItemDefinition BoxDefinition { get; }
            public string ExplosionMarker { get; }
            public string CodeLock { get; }
            public string FireballSmall { get; }
            public string Fireball { get; }
            public string HighExternalWoodenWall { get; }
            public string HighExternalStoneWall { get; }
            public string Ladder { get; }
            public string Murderer { get; }
            public string RadiusMarker { get; }
            public string Sphere { get; }
            public string Scientist { get; }
            public string VendingMarker { get; }

            public ResourcePath()
            {
                Blocks = new List<uint> { 803699375, 2194854973, 919059809, 3531096400, 310235277, 2326657495, 3234260181, 72949757, 1745077396, 1585379529 };
                TrueDamage = new List<uint> { 976279966, 3824663394, 1202834203, 4254045167, 1745077396, 1585379529 };
                BoxDefinition = ItemManager.FindItemDefinition(StringPool.Get(2735448871));
                CodeLock = StringPool.Get(3518824735);
                ExplosionMarker = StringPool.Get(4060989661);
                FireballSmall = StringPool.Get(2086405370);
                Fireball = StringPool.Get(3369311876);
                HighExternalWoodenWall = StringPool.Get(1745077396);
                HighExternalStoneWall = StringPool.Get(1585379529);
                Ladder = StringPool.Get(2150203378);
                Murderer = StringPool.Get(3879041546);
                RadiusMarker = StringPool.Get(2849728229);
                Sphere = StringPool.Get(3211242734);
                Scientist = StringPool.Get(4223875851);
                VendingMarker = StringPool.Get(3459945130);
            }
        }

        public class SingletonBackbone : SingletonComponent<SingletonBackbone>
        {
            public RaidableBases Plugin { get; private set; }
            public ResourcePath Path { get; private set; }
            public Oxide.Core.Libraries.Lang lang => Plugin.lang;
            private StringBuilder sb => Plugin._sb;
            public StoredData Data => Plugin.storedData;
            public Dictionary<ulong, FinalDestination> Destinations { get; set; }
            public string Easy { get; set; }
            public string Medium { get; set; }
            public string Hard { get; set; }
            public string Expert { get; set; }
            public string Nightmare { get; set; }

            public SingletonBackbone(RaidableBases plugin)
            {
                Plugin = plugin;
                Path = new ResourcePath();
                Destinations = new Dictionary<ulong, FinalDestination>();
                Easy = RemoveFormatting(GetMessage("ModeEasy")).ToLower();
                Medium = RemoveFormatting(GetMessage("ModeMedium")).ToLower();
                Hard = RemoveFormatting(GetMessage("ModeHard")).ToLower();
                Expert = RemoveFormatting(GetMessage("ModeExpert")).ToLower();
                Nightmare = RemoveFormatting(GetMessage("ModeNightmare")).ToLower();
            }

            public void Destroy()
            {
                Path = null;
                Plugin = null;
                DestroyImmediate(Instance);
            }

            public void Message(BasePlayer player, string key, params object[] args)
            {
                if (player.IsValid())
                {
                    Plugin.Player.Message(player, GetMessage(key, player.UserIDString, args), _config.Settings.ChatID);
                }
            }

            public string GetMessage(string key, string id = null, params object[] args)
            {
                sb.Length = 0;

                if (_config.EventMessages.Prefix && id != null && id != "server_console" && !key.EndsWith("Flag"))
                {
                    sb.Append(lang.GetMessage("Prefix", Plugin, id));
                }

                sb.Append(id == "server_console" || id == null ? RemoveFormatting(lang.GetMessage(key, Plugin, id)) : lang.GetMessage(key, Plugin, id));

                return args.Length > 0 ? string.Format(sb.ToString(), args) : sb.ToString();
            }

            public string RemoveFormatting(string source) => source.Contains(">") ? Regex.Replace(source, "<.*?>", string.Empty) : source;

            public Timer Timer(float seconds, Action action) => Plugin.timer.Once(seconds, action);

            public bool HasPermission(string id, string perm) => Plugin.permission.UserHasPermission(id, perm);
        }

        public class Elevation
        {
            public float Min { get; set; }
            public float Max { get; set; }
        }

        public class RaidableSpawnLocation
        {
            public Elevation Elevation = new Elevation();
            public Vector3 Location = Vector3.zero;
        }

        public class RaidableSpawns
        {
            private readonly List<RaidableSpawnLocation> Spawns = new List<RaidableSpawnLocation>();
            private readonly List<RaidableSpawnLocation> Cache = new List<RaidableSpawnLocation>();

            public void Add(RaidableSpawnLocation rsl)
            {
                Spawns.Add(rsl);
            }

            public void TryAddRange()
            {
                if (Cache.Count > 0)
                {
                    Spawns.AddRange(new List<RaidableSpawnLocation>(Cache));
                    Cache.Clear();
                }
            }

            public IEnumerable<RaidableSpawnLocation> Active => Spawns;

            public IEnumerable<RaidableSpawnLocation> Inactive => Cache;

            public void Check()
            {
                if (Spawns.Count == 0)
                {
                    TryAddRange();
                }
            }

            public void Clear()
            {
                Spawns.Clear();
                Cache.Clear();
            }

            public int Count
            {
                get
                {
                    return Spawns.Count;
                }
            }

            public RaidableSpawnLocation GetRandom()
            {
                var rsl = Spawns.GetRandom();

                Remove(rsl);

                return rsl;
            }

            private void Remove(RaidableSpawnLocation a)
            {
                Spawns.Remove(a);
                Cache.Add(a);
            }

            public void RemoveNear(RaidableSpawnLocation a, float radius)
            {
                var list = new List<RaidableSpawnLocation>(Spawns);

                foreach (var b in list)
                {
                    if (InRange(a.Location, b.Location, radius))
                    {
                        Remove(b);
                    }
                }

                list.Clear();
            }

            public RaidableSpawns(List<RaidableSpawnLocation> spawns)
            {
                Spawns = spawns;
            }

            public RaidableSpawns()
            {

            }
        }

        private class MapInfo
        {
            public string Url;
            public string IconName;
            public Vector3 Position;
        }

        public class PlayerInfo
        {
            public int TotalRaids { get; set; }
            public int Raids { get; set; }
            public PlayerInfo() { }
        }

        public class Lockout
        {
            public double Easy { get; set; }
            public double Medium { get; set; }
            public double Hard { get; set; }
            public double Expert { get; set; }
            public double Nightmare { get; set; }

            public bool Any() => Easy > 0 || Medium > 0 || Hard > 0;
        }

        public class RotationCycle
        {
            private Dictionary<RaidableMode, List<string>> _buildings = new Dictionary<RaidableMode, List<string>>();

            public void Add(RaidableType type, RaidableMode mode, string key)
            {
                if (!_config.Settings.Management.RequireAllSpawned || type == RaidableType.Grid || type == RaidableType.Manual)
                {
                    return;
                }

                List<string> keyList;
                if (!_buildings.TryGetValue(mode, out keyList))
                {
                    _buildings[mode] = keyList = new List<string>();
                }

                if (!keyList.Contains(key))
                {
                    keyList.Add(key);
                }
            }

            public bool CanSpawn(RaidableType type, RaidableMode mode, string key)
            {
                if (mode == RaidableMode.Disabled)
                {
                    return false;
                }

                if (!_config.Settings.Management.RequireAllSpawned || mode == RaidableMode.Random || type == RaidableType.Grid || type == RaidableType.Manual)
                {
                    return true;
                }

                List<string> keyList;
                if (!_buildings.TryGetValue(mode, out keyList))
                {
                    return true;
                }

                return TryClear(type, mode, keyList) || !keyList.Contains(key);
            }

            public bool TryClear(RaidableType type, RaidableMode mode, List<string> keyList)
            {
                foreach (var building in Buildings.Profiles)
                {
                    if (building.Value.Mode != mode || !CanSpawnDifficultyToday(mode) || MustExclude(type, building.Value.AllowPVP))
                    {
                        continue;
                    }

                    if (!keyList.Contains(building.Key) && FileExists(building.Key))
                    {
                        return false;
                    }

                    foreach (var kvp in building.Value.AdditionalBases)
                    {
                        if (!keyList.Contains(kvp.Key) && FileExists(kvp.Key))
                        {
                            return false;
                        }
                    }
                }

                keyList.Clear();
                return true;
            }
        }

        public class StoredData
        {
            public Dictionary<string, Lockout> Lockouts { get; } = new Dictionary<string, Lockout>();
            public Dictionary<string, PlayerInfo> Players { get; set; } = new Dictionary<string, PlayerInfo>();
            public Dictionary<string, UI.Info> UI { get; set; } = new Dictionary<string, UI.Info>();
            public string RaidTime { get; set; } = DateTime.MinValue.ToString();
            public int TotalEvents { get; set; }
            public StoredData() { }
        }

        private class PlayWithFire : FacepunchBehaviour
        {
            private FireBall fireball { get; set; }
            private BaseEntity target { get; set; }
            private bool fireFlung { get; set; }
            private Coroutine mcCoroutine { get; set; }

            public BaseEntity Target
            {
                get
                {
                    return target;
                }
                set
                {
                    target = value;
                    enabled = true;
                }
            }

            private void Awake()
            {
                fireball = GetComponent<FireBall>();
                enabled = false;
            }

            private void FixedUpdate()
            {
                if (!IsValid(target) || target.Health() <= 0)
                {
                    fireball.Extinguish();
                    Destroy(this);
                    return;
                }

                fireball.transform.RotateAround(target.transform.position, Vector3.up, 5f);
                fireball.transform.hasChanged = true;
            }

            public void FlingFire(BaseEntity attacker)
            {
                if (fireFlung) return;
                fireFlung = true;
                mcCoroutine = StartCoroutine(MakeContact(attacker));
            }

            private IEnumerator MakeContact(BaseEntity attacker)
            {
                float distance = Vector3.Distance(fireball.ServerPosition, attacker.transform.position);

                while (!Backbone.Plugin.IsUnloading && attacker != null && fireball != null && !fireball.IsDestroyed && !InRange(fireball.ServerPosition, attacker.transform.position, 2.5f))
                {
                    fireball.ServerPosition = Vector3.MoveTowards(fireball.ServerPosition, attacker.transform.position, distance * 0.1f);
                    yield return CoroutineEx.waitForSeconds(0.3f);
                }
            }

            private void OnDestroy()
            {
                if (mcCoroutine != null)
                {
                    StopCoroutine(mcCoroutine);
                    mcCoroutine = null;
                }

                Destroy(this);
            }
        }

        public class PlayerInputEx : FacepunchBehaviour
        {
            public BasePlayer player { get; set; }
            private InputState input { get; set; }
            private RaidableBase raid { get; set; }

            public void Setup(BasePlayer player, RaidableBase raid)
            {
                this.player = player;
                this.raid = raid;
                raid.Inputs[player] = this;
                input = player.serverInput;
                InvokeRepeating(Repeater, 0f, 0.1f);
            }

            public void Restart()
            {
                CancelInvoke(Repeater);
                InvokeRepeating(Repeater, 0f, 0.1f);
            }

            private void Repeater()
            {
                if (raid == null)
                {
                    Destroy(this);
                    return;
                }

                if (!player || !player.IsConnected)
                {
                    raid.TryInvokeResetPayLock();
                    Destroy(this);
                    return;
                }

                TryPlaceLadder(player, raid);
            }

            public static bool TryPlaceLadder(BasePlayer player, RaidableBase raid)
            {
                if (player.svActiveItemID == 0)
                {
                    return false;
                }

                var input = player.serverInput;

                if (!input.WasJustReleased(BUTTON.FIRE_PRIMARY) && !input.IsDown(BUTTON.FIRE_PRIMARY))
                {
                    return false;
                }

                Item item = player.GetActiveItem();

                if (item?.info.shortname != "ladder.wooden.wall")
                {
                    return false;
                }

                RaycastHit hit;
                if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 4f, Layers.Mask.Construction, QueryTriggerInteraction.Ignore))
                {
                    return false;
                }

                var block = hit.GetEntity();

                if (!block.IsValid() || block.OwnerID != 0 || !Backbone.Path.Blocks.Contains(block.prefabID)) // walls and foundations
                {
                    return false;
                }

                int amount = item.amount;
                var action = new Action(() =>
                {
                    if (raid == null || item == null || item.amount != amount || IsLadderNear(hit.point))
                    {
                        return;
                    }

                    var rot = Quaternion.LookRotation(hit.normal, Vector3.up);
                    var e = GameManager.server.CreateEntity(Backbone.Path.Ladder, hit.point, rot, true);

                    if (e == null)
                    {
                        return;
                    }

                    e.gameObject.SendMessage("SetDeployedBy", player, SendMessageOptions.DontRequireReceiver);
                    e.OwnerID = 0;
                    e.Spawn();
                    item.UseItem(1);

                    var planner = item.GetHeldEntity() as Planner;

                    if (planner != null)
                    {
                        var deployable = planner?.GetDeployable();

                        if (deployable != null && deployable.setSocketParent && block.SupportsChildDeployables())
                        {
                            e.SetParent(block, true, false);
                        }
                    }

                    raid.BuiltList.Add(e.net.ID);
                });

                player.Invoke(action, 0.1f);
                return true;
            }

            public static bool IsLadderNear(Vector3 target)
            {
                var list = Pool.GetList<BaseLadder>();

                Vis.Entities(target, 0.3f, list, Layers.Mask.Deployed, QueryTriggerInteraction.Ignore);

                bool result = list.Count > 0;

                Pool.FreeList(ref list);

                return result;
            }

            private void OnDestroy()
            {
                CancelInvoke();
                UI.DestroyStatusUI(player);
                raid?.Inputs?.Remove(player);
                Destroy(this);
            }
        }

        public class FinalDestination : FacepunchBehaviour
        {
            public NPCPlayerApex npc;
            private List<Vector3> positions;
            public AttackOperator.AttackType attackType;
            private NpcSettings settings;
            private bool isRanged;
            private BasePlayer target;
            private ulong userID;

            private void OnDestroy()
            {
                Backbone?.Destinations?.Remove(userID);
                CancelInvoke();
                Destroy(this);
            }

            public void Set(NPCPlayerApex npc, List<Vector3> positions, NpcSettings settings)
            {
                this.npc = npc;
                this.positions = positions;
                this.settings = settings;
                attackType = IsMelee(npc) ? AttackOperator.AttackType.CloseRange : AttackOperator.AttackType.LongRange;
                isRanged = attackType != AttackOperator.AttackType.CloseRange;
                Backbone.Destinations[userID = npc.userID] = this;
                InvokeRepeating(Go, 0f, 7.5f);
            }

            public void Attack(BasePlayer player, bool converge = true)
            {
                if (target == player)
                {
                    return;
                }

                if (!IsInvoking(Attack))
                {
                    InvokeRepeating(Attack, 0f, 1f);
                }

                if (npc.Stats.AggressionRange < 150f)
                {
                    npc.Stats.AggressionRange += 150f;
                    npc.Stats.DeaggroRange += 100f;
                }

                npc.AttackTarget = player;
                npc.lastAttacker = player;
                npc.AiContext.LastAttacker = player;
                target = player;

                if (converge)
                {
                    Converge(player);
                }
            }

            private void Attack()
            {
                if (npc.AttackTarget == null || !(npc.AttackTarget is BasePlayer))
                {
                    return;
                }

                var attacker = npc.AttackTarget as BasePlayer;

                if (attacker.IsDead())
                {
                    Forget();
                    CancelInvoke(Attack);
                    InvokeRepeating(Go, 0f, 7.5f);
                    return;
                }

                npc.NeverMove = false;
                npc.IsStopped = false;
                npc.RandomMove();

                if (attacker.IsVisible(npc.eyes.position, attacker.eyes.position))
                {
                    HumanAttackOperator.AttackEnemy(npc.AiContext, attackType);
                }
            }

            private void Forget()
            {
                npc.Stats.AggressionRange = settings.AggressionRange;
                npc.Stats.DeaggroRange = settings.AggressionRange * 1.125f;
                npc.lastDealtDamageTime = Time.time - 21f;
                npc.SetFact(Facts.HasEnemy, 0, true, true);
                npc.SetFact(Facts.EnemyRange, 3, true, true);
                npc.SetFact(Facts.AfraidRange, 1, true, true);
                npc.AttackTarget = null;
                npc.lastAttacker = null;
                npc.lastAttackedTime = Time.time - 31f;
                npc.LastAttackedDir = Vector3.zero;
            }

            public void Warp()
            {
                var position = positions.GetRandom();

                npc.Pause();
                npc.ServerPosition = position;
                npc.GetNavAgent.Warp(position);
                npc.stuckDuration = 0f;
                npc.IsStuck = false;
                npc.Resume();
            }

            private void Go()
            {
                if (npc.IsHeadUnderwater())
                {
                    npc.Kill();
                    Destroy(this);
                    return;
                }

                if (npc.AttackTarget == null)
                {
                    npc.NeverMove = true;

                    if (npc.IsStuck)
                    {
                        Warp();
                    }

                    var position = positions.GetRandom();

                    if (npc.GetNavAgent == null || !npc.GetNavAgent.isOnNavMesh)
                    {
                        npc.finalDestination = position;
                    }
                    else npc.GetNavAgent.SetDestination(position);

                    npc.IsStopped = false;
                    npc.Destination = position;
                }
            }

            private void Converge(BasePlayer player)
            {
                foreach (var fd in Backbone.Destinations.Values)
                {
                    if (fd != this && fd.npc.IsValid() && fd.npc.AttackTarget == null && fd.npc.IsAlive() && fd.npc.Distance(npc) < 25f)
                    {
                        if (isRanged && fd.attackType == AttackOperator.AttackType.CloseRange) continue;
                        fd.npc.SetFact(Facts.AllyAttackedRecently, 1, true, true);
                        fd.npc.SetFact(Facts.AttackedRecently, 1, true, true);
                        fd.Attack(player, false);
                        fd.Attack();
                    }
                }
            }

            private bool IsMelee(BasePlayer player)
            {
                var attackEntity = player.GetHeldEntity() as AttackEntity;

                if (attackEntity == null)
                {
                    return false;
                }

                return attackEntity is BaseMelee;
            }
        }

        public class CorpseData
        {
            public PlayerCorpse corpse;
            public BasePlayer player;
            public string displayName;
            public ulong userID;
        }

        public class RaidableBase : FacepunchBehaviour
        {
            private List<BaseMountable> mountables;
            public Hash<uint, float> conditions;
            private Dictionary<string, List<string>> _clans;
            private Dictionary<string, List<string>> _friends;
            public List<StorageContainer> _containers;
            public List<StorageContainer> _allcontainers;
            private List<ulong> BoxSkins;
            public Dictionary<BasePlayer, PlayerInputEx> Inputs;
            public List<NPCPlayerApex> npcs;
            public Dictionary<uint, BasePlayer> records;
            public Dictionary<ulong, BasePlayer> raiders;
            public List<BasePlayer> friends;
            public List<BasePlayer> intruders;
            public Dictionary<uint, CorpseData> corpses;
            private Dictionary<FireBall, PlayWithFire> fireballs;
            private List<Vector3> foundations;
            private List<SphereEntity> spheres;
            private List<BaseEntity> lights;
            private List<BaseOven> ovens;
            public List<AutoTurret> turrets;
            private List<Door> doors;
            private List<CustomDoorManipulator> doorControllers;
            public List<uint> blocks;
            public Dictionary<string, float> lastActive;
            public List<string> ids;
            public BuildingPrivlidge priv { get; set; }
            private Dictionary<string, List<string>> npcKits { get; set; }
            private MapMarkerExplosion explosionMarker { get; set; }
            private MapMarkerGenericRadius genericMarker { get; set; }
            private VendingMachineMapMarker vendingMarker { get; set; }
            private Coroutine setupRoutine { get; set; } = null;
            private bool IsInvokingCanFinish { get; set; }
            public bool IsDespawning { get; set; }
            public Vector3 PastedLocation { get; set; }
            public Vector3 Location { get; set; }
            public string BaseName { get; set; }
            public int BaseIndex { get; set; } = -1;
            public uint BuildingID { get; set; }
            public uint NetworkID { get; set; } = uint.MaxValue;
            public Color NoneColor { get; set; }
            public BasePlayer owner { get; set; }
            public bool ownerFlag { get; set; }
            public string ID { get; set; } = "0";
            public ulong ownerId { get; set; }
            public float spawnTime { get; set; }
            public float despawnTime { get; set; }
            private ulong skinId { get; set; }
            public bool AllowPVP { get; set; }
            public BuildingOptions Options { get; set; }
            public bool IsAuthed { get; set; }
            public bool IsOpened { get; set; } = true;
            public bool IsUnloading { get; set; }
            public int uid { get; set; }
            public bool IsPayLocked { get; set; }
            public int npcMaxAmount { get; set; }
            public RaidableType Type { get; set; }
            public string DifficultyMode { get; set; }
            public bool IsLooted => CanUndo();
            public bool IsLoading => setupRoutine != null;
            private bool markerCreated { get; set; }
            private bool lightsOn { get; set; }
            private bool killed { get; set; }
            private int itemAmountSpawned { get; set; }
            private bool privSpawned { get; set; }
            public string markerName { get; set; }
            public string NoMode { get; set; }
            public bool isAuthorized { get; set; }
            public bool IsEngaged { get; set; }
            private TurretState _turretsState { get; set; } = TurretState.Unknown;
            private ItemDefinition lowgradefuel { get; set; } = ItemManager.FindItemDefinition("lowgradefuel");
            private List<BaseEntity> Entities { get; set; } = new List<BaseEntity>();
            public List<uint> BuiltList { get; set; } = new List<uint>();

            private void CreatePool()
            {
                mountables = Pool.GetList<BaseMountable>();
                conditions = Pool.Get<Hash<uint, float>>();
                _clans = Pool.Get<Dictionary<string, List<string>>>();
                _friends = Pool.Get<Dictionary<string, List<string>>>();
                _containers = Pool.GetList<StorageContainer>();
                _allcontainers = Pool.GetList<StorageContainer>();
                BoxSkins = Pool.GetList<ulong>();
                Inputs = Pool.Get<Dictionary<BasePlayer, PlayerInputEx>>();
                npcs = Pool.GetList<NPCPlayerApex>();
                records = Pool.Get<Dictionary<uint, BasePlayer>>();
                raiders = Pool.Get<Dictionary<ulong, BasePlayer>>();
                friends = Pool.GetList<BasePlayer>();
                intruders = Pool.GetList<BasePlayer>();
                corpses = Pool.Get<Dictionary<uint, CorpseData>>();
                fireballs = Pool.Get<Dictionary<FireBall, PlayWithFire>>();
                foundations = Pool.GetList<Vector3>();
                spheres = Pool.GetList<SphereEntity>();
                lights = Pool.GetList<BaseEntity>();
                ovens = Pool.GetList<BaseOven>();
                turrets = Pool.GetList<AutoTurret>();
                doors = Pool.GetList<Door>();
                doorControllers = Pool.GetList<CustomDoorManipulator>();
                blocks = Pool.GetList<uint>();
                lastActive = Pool.Get<Dictionary<string, float>>();
                ids = Pool.Get<List<string>>();
            }

            public void FreePool()
            {
                mountables.Clear();
                conditions.Clear();
                _clans.Clear();
                _friends.Clear();
                _containers.Clear();
                _allcontainers.Clear();
                BoxSkins.Clear();
                Inputs.Clear();
                npcs.Clear();
                records.Clear();
                raiders.Clear();
                friends.Clear();
                intruders.Clear();
                corpses.Clear();
                fireballs.Clear();
                foundations.Clear();
                spheres.Clear();
                lights.Clear();
                ovens.Clear();
                turrets.Clear();
                doors.Clear();
                doorControllers.Clear();
                blocks.Clear();
                lastActive.Clear();
                ids.Clear();

                Pool.Free(ref mountables);
                Pool.Free(ref conditions);
                Pool.Free(ref _clans);
                Pool.Free(ref _friends);
                Pool.Free(ref _containers);
                Pool.Free(ref _allcontainers);
                Pool.Free(ref BoxSkins);
                Pool.Free(ref Inputs);
                Pool.Free(ref npcs);
                Pool.Free(ref records);
                Pool.Free(ref raiders);
                Pool.Free(ref friends);
                Pool.Free(ref intruders);
                Pool.Free(ref corpses);
                Pool.Free(ref fireballs);
                Pool.Free(ref foundations);
                Pool.Free(ref spheres);
                Pool.Free(ref lights);
                Pool.Free(ref ovens);
                Pool.Free(ref turrets);
                Pool.Free(ref doors);
                Pool.Free(ref doorControllers);
                Pool.Free(ref blocks);
                Pool.Free(ref lastActive);
                Pool.Free(ref ids);
            }

            private void Awake()
            {
                CreatePool();
                markerName = _config.Settings.Markers.MarkerName;
                spawnTime = Time.realtimeSinceStartup;
            }

            private void OnDestroy()
            {
                Interface.CallHook("OnRaidableBaseEnded", Location, (int)Options.Mode);
                Despawn();
                FreePool();
                Destroy(this);
            }

            private void OnTriggerEnter(Collider col)
            {
                var player = col?.ToBaseEntity() as BasePlayer;
                bool isPlayerValid = player.IsValid();
                var m = col?.ToBaseEntity() as BaseMountable;
                bool isMountValid = m.IsValid();
                var players = new List<BasePlayer>();

                if (isMountValid)
                {
                    players = GetMountedPlayers(m);

                    if (Type != RaidableType.None && TryRemoveMountable(m, players))
                    {
                        return;
                    }
                }
                else if (isPlayerValid)
                {
                    players.Add(player);
                }

                players.RemoveAll(p => p.IsNpc || intruders.Contains(p));

                foreach (var p in players)
                {
                    if (!p.IsConnected && Time.time - p.sleepStartTime < 2f)
                    {
                        continue;
                    }

                    OnEnterRaid(p);
                }
            }

            private bool JustRespawned(BasePlayer player)
            {
                return player.lifeStory?.timeBorn - (uint)Epoch.Current < 1f;
            }

            private bool NearFoundation(Vector3 position)
            {
                foreach (var a in foundations)
                {
                    if (InRange(a, position, 3f))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void OnEnterRaid(BasePlayer p)
            {
                if (p.IsNpc)
                {
                    return;
                }

                if (Type != RaidableType.None)
                {
                    if (IsBanned(p) || Teleported(p) || HasLockout(p))
                    {
                        RemovePlayer(p);
                        return;
                    }
                }

                if (Type != RaidableType.None)
                {
                    if (!_config.Settings.Management.AllowTeleport && p.IsConnected && !CanBypass(p) && (InRange(p.transform.position, Location, Radius, false) || NearFoundation(p.transform.position)))
                    {
                        if (CanMessage(p))
                        {
                            Backbone.Message(p, "CannotTeleport");
                        }

                        RemovePlayer(p);
                        return;
                    }

                    if (HasLockout(p) || JustRespawned(p) || IsBanned(p))
                    {
                        RemovePlayer(p);
                        return;
                    }
                }

                if (!intruders.Contains(p))
                {
                    intruders.Add(p);
                }

                Protector();

                if (!intruders.Contains(p))
                {
                    return;
                }

                PlayerInputEx component;
                if (Inputs.TryGetValue(p, out component))
                {
                    Destroy(component);
                }

                p.gameObject.AddComponent<PlayerInputEx>().Setup(p, this);
                StopUsingWand(p);

                if (_config.EventMessages.AnnounceEnterExit)
                {
                    Backbone.Message(p, AllowPVP ? "OnPlayerEntered" : "OnPlayerEnteredPVE");
                }

                if (_config.Settings.Management.AutoTurretPowerOnOff && intruders.Count > 0 && _turretsState != TurretState.Online)
                {
                    CancelInvoke(SetTurretsState);
                    SetTurretsState();
                }

                UI.Update(this, p);
                Interface.CallHook("OnPlayerEnteredRaidableBase", p, Location, AllowPVP);

                if (_config.Settings.Management.PVPDelay > 0 && !Backbone.Plugin.PvpDelay.ContainsKey(p.userID))
                {
                    Interface.CallHook("OnPlayerPvpDelayEntry", p);
                }
            }

            private void OnTriggerExit(Collider col)
            {
                var player = col?.ToBaseEntity() as BasePlayer;

                /*if (player.IsValid() && IsUnderTerrain(player.transform.position))
                {
                    return;
                }*/

var m = col?.ToBaseEntity() as BaseMountable;
                var players = new List<BasePlayer>();

                if (m.IsValid())
                {
                    players = GetMountedPlayers(m);
                }
                else if (player.IsValid() && !player.IsNpc)
                {
                    players.Add(player);
                }

                if (players.Count == 0)
                {
                    return;
                }

                foreach (var p in players)
                {
                    OnPlayerExit(p, p.IsDead());
                }
            }

            public void OnPlayerExit(BasePlayer p, bool skipDelay = true)
            {
                UI.DestroyStatusUI(p);

                PlayerInputEx component;
                if (Inputs.TryGetValue(p, out component))
                {
                    Destroy(component);
                }

                if (!intruders.Contains(p))
                {
                    return;
                }

                intruders.Remove(p);
                Interface.CallHook("OnPlayerExitedRaidableBase", p, Location, AllowPVP);

                if (_config.Settings.Management.PVPDelay > 0)
                {
                    if (skipDelay || !IsPVE() || !AllowPVP)
                    {
                        goto enterExit;
                    }

                    if (_config.EventMessages.AnnounceEnterExit)
                    {
                        string arg = Backbone.GetMessage("PVPFlag", p.UserIDString).Replace("[", string.Empty).Replace("] ", string.Empty);
                        Backbone.Message(p, "DoomAndGloom", arg, _config.Settings.Management.PVPDelay);
                    }

                    ulong id = p.userID;
                    DelaySettings ds;
                    if (!Backbone.Plugin.PvpDelay.TryGetValue(id, out ds))
                    {
                        Backbone.Plugin.PvpDelay[id] = ds = new DelaySettings
                        {
                            Timer = Backbone.Timer(_config.Settings.Management.PVPDelay, () =>
                            {
                                Interface.CallHook("OnPlayerPvpDelayExpired", p);
                                Backbone.Plugin.PvpDelay.Remove(id);
                            }),
                            AllowPVP = AllowPVP,
                            RaidableBase = this
                        };

                        goto exit;
                    }

                    ds.Timer.Reset();
                    goto exit;
                }

                enterExit:
                if (_config.EventMessages.AnnounceEnterExit)
                {
                    Backbone.Message(p, AllowPVP ? "OnPlayerExit" : "OnPlayerExitPVE");
                }

                exit:
                if (_config.Settings.Management.AutoTurretPowerOnOff && intruders.Count == 0)
                {
                    Invoke(SetTurretsState, 10f);
                }
            }

            private bool IsBanned(BasePlayer p)
            {
                if (Backbone.HasPermission(p.UserIDString, banPermission))
                {
                    if (CanMessage(p))
                    {
                        Backbone.Message(p, "Banned");
                    }

                    return true;
                }

                return false;
            }

            private bool Teleported(BasePlayer p)
            {
                if (!_config.Settings.Management.AllowTeleport && p.IsConnected && !CanBypass(p))
                {
                    if (NearFoundation(p.transform.position))
                    {
                        if (CanMessage(p))
                        {
                            Backbone.Message(p, "CannotTeleport");
                        }

                        return true;
                    }
                }

                return false;
            }

            private bool IsHogging(BasePlayer player)
            {
                if (!_config.Settings.Management.PreventHogging || CanBypass(player))
                {
                    return false;
                }

                foreach (var raid in Backbone.Plugin.Raids.Values)
                {
                    if (!raid.IsPayLocked && raid.IsOpened && raid.BaseIndex != BaseIndex && raid.Any(player.userID, false))
                    {
                        if (CanMessage(player))
                        {
                            Backbone.Message(player, "HoggingFinishYourRaid", PositionToGrid(raid.Location));
                        }

                        return true;
                    }
                }

                if (!_config.Settings.Management.Lockout.IsBlocking() || Backbone.HasPermission(player.UserIDString, bypassBlockPermission))
                {
                    return false;
                }

                foreach (var raid in Backbone.Plugin.Raids.Values)
                {
                    if (raid.BaseIndex != BaseIndex && !raid.IsPayLocked && raid.IsOpened && IsHogging(player, raid))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool IsHogging(BasePlayer player, RaidableBase raid)
            {
                foreach (var intruder in raid.intruders)
                {
                    if (!intruder.IsValid())
                    {
                        continue;
                    }

                    if (_config.Settings.Management.Lockout.BlockTeams && raid.IsOnSameTeam(player.userID, intruder.userID))
                    {
                        if (CanMessage(player))
                        {
                            Backbone.Message(player, "HoggingFinishYourRaidTeam", intruder.displayName, PositionToGrid(raid.Location));
                        }

                        return true;
                    }
                    else if (_config.Settings.Management.Lockout.BlockFriends && raid.IsFriends(player.UserIDString, intruder.UserIDString))
                    {
                        if (CanMessage(player))
                        {
                            Backbone.Message(player, "HoggingFinishYourRaidFriend", intruder.displayName, PositionToGrid(raid.Location));
                        }

                        return true;
                    }
                    else if (_config.Settings.Management.Lockout.BlockClans && raid.IsInSameClan(player.UserIDString, intruder.UserIDString))
                    {
                        if (CanMessage(player))
                        {
                            Backbone.Message(player, "HoggingFinishYourRaidClan", intruder.displayName, PositionToGrid(raid.Location));
                        }

                        return true;
                    }
                }

                return false;
            }

            private void SetTurretsState()
            {
                bool online = intruders.Count > 0;

                foreach (var turret in turrets)
                {
                    if (turret == null || turret.IsDestroyed)
                    {
                        continue;
                    }

                    if (!online)
                    {
                        if (turret.target.IsValid())
                        {
                            turret.MarkDirtyForceUpdateOutputs();
                            turret.nextShotTime += 0.1f;
                            turret.target = null;
                        }

                        turret.CancelInvoke(turret.SetOnline);
                    }

                    Effect.server.Run(online ? turret.onlineSound.resourcePath : turret.offlineSound.resourcePath, turret, 0u, Vector3.zero, Vector3.zero, null, false);
                    turret.SetFlag(BaseEntity.Flags.On, online, false, true);
                    //turret.booting = online;
                    turret.isLootable = false;
                    turret.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

                    _turretsState = online ? TurretState.Online : TurretState.Offline;
                }
            }

            private void CheckCorpses()
            {
                var keys = Pool.GetList<uint>();

                foreach (var data in corpses)
                {
                    if (EjectCorpse(data.Key, data.Value))
                    {
                        keys.Add(data.Key);
                    }
                }

                foreach (uint key in keys)
                {
                    corpses.Remove(key);
                }

                Pool.FreeList(ref keys);
            }

            private void Protector()
            {
                if (corpses.Count > 0)
                {
                    CheckCorpses();
                }

                if (Type == RaidableType.None || intruders.Count == 0)
                {
                    return;
                }

                var targets = new List<BasePlayer>(intruders);

                foreach (var target in targets)
                {
                    if (target == null || target == owner || friends.Contains(target) || CanBypass(target))
                    {
                        continue;
                    }

                    if (CanEject(target) || _config.Settings.Management.EjectSleepers && Type != RaidableType.None && target.IsSleeping())
                    {
                        intruders.Remove(target);
                        RemovePlayer(target);
                    }
                    else if (ownerId.IsSteamId())
                    {
                        friends.Add(target);
                    }
                }

                targets.Clear();
            }

            public void DestroyUI()
            {
                var list = Pool.GetList<BasePlayer>();

                foreach (var kvp in UI.Players)
                {
                    if (kvp.Value == this)
                    {
                        list.Add(kvp.Key);
                    }
                }

                foreach (var player in list)
                {
                    UI.DestroyStatusUI(player);
                }

                list.Clear();
                Pool.Free(ref list);
            }

            public bool IsUnderTerrain(Vector3 pos)
            {
                float height = TerrainMeta.HeightMap.GetHeight(pos);

                if (pos.y - 1f > height || pos.y + 1f < height)
                {
                    return false;
                }

                return true;
            }

            public static void Unload()
            {
                foreach (var raid in Backbone.Plugin.Raids.Values)
                {
                    if (raid.setupRoutine != null)
                    {
                        raid.StopCoroutine(raid.setupRoutine);
                    }

                    raid.IsUnloading = true;
                }
            }

            public void Despawn()
            {
                IsOpened = false;

                if (killed)
                {
                    return;
                }

                killed = true;

                Interface.CallHook("OnRaidableBaseDespawn", Location, spawnTime, ID);

                SetNoDrops();
                CancelInvoke();
                DestroyFire();
                DestroyInputs();
                RemoveSpheres();
                KillNpc();
                StopAllCoroutines();
                RemoveMapMarkers();
                DestroyUI();

                if (!IsUnloading)
                {
                    ServerMgr.Instance.StartCoroutine(Backbone.Plugin.UndoRoutine(BaseIndex, BuiltList.ToList(), Location));
                }

                foreach (var raider in raiders)
                {
                    TrySetLockout(raider.Key.ToString(), raider.Value);
                }

                Backbone.Plugin.Raids.Remove(uid);

                if (Backbone.Plugin.Raids.Count == 0)
                {
                    if (IsUnloading)
                    {
                        UnsetStatics();
                    }
                    else Backbone.Plugin.UnsubscribeHooks();
                }

                Locations.Remove(PastedLocation);
                Destroy(this);
            }

            public bool AddLooter(BasePlayer looter)
            {
                if (!looter.IsValid() || !IsAlly(looter) || looter.IsFlying || Backbone.Plugin.IsInvisible(looter))
                {
                    return false;
                }

                UpdateStatus(looter);

                if (!raiders.ContainsKey(looter.userID))
                {
                    raiders.Add(looter.userID, looter);
                    return true;
                }

                return false;
            }

            private void FillAmmoTurret(AutoTurret turret)
            {
                if (isAuthorized)
                {
                    return;
                }

                foreach (var id in turret.authorizedPlayers)
                {
                    if (id.userid.IsSteamId())
                    {
                        isAuthorized = true;
                        return;
                    }
                }

                var attachedWeapon = turret.GetAttachedWeapon();

                if (!attachedWeapon.IsValid())
                {
                    return;
                }

                turret.inventory.AddItem(attachedWeapon.primaryMagazine.ammoType, _config.Weapons.Ammo.AutoTurret);
                attachedWeapon.primaryMagazine.contents = attachedWeapon.primaryMagazine.capacity;
                attachedWeapon.SendNetworkUpdateImmediate();
                turret.Invoke(turret.UpdateTotalAmmo, 0.1f);
            }

            private void FillAmmoGunTrap(GunTrap gt)
            {
                if (gt.ammoType == null)
                {
                    return;
                }

                gt.inventory.AddItem(gt.ammoType, _config.Weapons.Ammo.GunTrap);
            }

            private void FillAmmoFogMachine(FogMachine fm)
            {
                if (lowgradefuel == null)
                {
                    return;
                }

                fm.inventory.AddItem(lowgradefuel, _config.Weapons.Ammo.FogMachine);
            }

            private void FillAmmoFlameTurret(FlameTurret ft)
            {
                if (lowgradefuel == null)
                {
                    return;
                }

                ft.inventory.AddItem(lowgradefuel, _config.Weapons.Ammo.FlameTurret);
            }

            private void FillAmmoSamSite(SamSite ss)
            {
                if (!ss.HasAmmo())
                {
                    Item item = ItemManager.Create(ss.ammoType, _config.Weapons.Ammo.SamSite);

                    if (!item.MoveToContainer(ss.inventory))
                    {
                        item.Remove();
                    }
                    else ss.ammoItem = item;
                }
                else if (ss.ammoItem != null && ss.ammoItem.amount < _config.Weapons.Ammo.SamSite)
                {
                    ss.ammoItem.amount = _config.Weapons.Ammo.SamSite;
                }
            }

            private void OnWeaponItemPreRemove(Item item)
            {
                if (isAuthorized)
                {
                    return;
                }

                if (priv != null && !priv.IsDestroyed)
                {
                    foreach (var id in priv.authorizedPlayers)
                    {
                        if (id.userid.IsSteamId())
                        {
                            isAuthorized = true;
                            return;
                        }
                    }
                }

                var weapon = item.parent?.entityOwner;

                if (weapon is AutoTurret)
                {
                    weapon.Invoke(() => FillAmmoTurret(weapon as AutoTurret), 0.1f);
                }
                else if (weapon is GunTrap)
                {
                    weapon.Invoke(() => FillAmmoGunTrap(weapon as GunTrap), 0.1f);
                }
                else if (weapon is SamSite)
                {
                    weapon.Invoke(() => FillAmmoSamSite(weapon as SamSite), 0.1f);
                }
            }

            private void OnItemAddedRemoved(Item item, bool bAdded)
            {
                if (!bAdded)
                {
                    StartTryToEnd();
                }
            }

            public void StartTryToEnd()
            {
                if (!IsInvokingCanFinish)
                {
                    IsInvokingCanFinish = true;
                    InvokeRepeating(TryToEnd, 0f, 1f);
                }
            }

            public void TryToEnd()
            {
                if (IsOpened && IsLooted)
                {
                    CancelInvoke(TryToEnd);
                    AwardRaiders();
                    Undo();
                }
            }

            public void AwardRaiders()
            {
                var players = new List<BasePlayer>();
                var sb = new StringBuilder();

                foreach (var raider in raiders)
                {
                    TrySetLockout(raider.Key.ToString(), raider.Value);

                    var player = raider.Value;

                    if (player == null || player.IsFlying || !IsPlayerActive(player.userID))
                    {
                        continue;
                    }

                    if (_config.Settings.RemoveAdminRaiders && player.IsAdmin && Type != RaidableType.None)
                    {
                        continue;
                    }

                    sb.Append(player.displayName).Append(", ");
                    players.Add(player);
                }

                if (players.Count == 0)
                {
                    return;
                }

                if (Options.Levels.Level2)
                {
                    SpawnNpcs();
                }

                HandleAwards(players);

                sb.Length -= 2;
                string thieves = sb.ToString();
                string posStr = FormatGridReference(Location);

                Puts(Backbone.GetMessage("Thief", null, posStr, thieves));

                if (_config.EventMessages.AnnounceThief)
                {
                    foreach (var target in BasePlayer.activePlayerList)
                    {
                        Backbone.Message(target, "Thief", posStr, thieves);
                    }
                }

                Backbone.Plugin.SaveData();
            }

            private void HandleAwards(List<BasePlayer> players)
            {
                foreach (var raider in players)
                {
                    if (_config.RankedLadder.Enabled)
                    {
                        PlayerInfo playerInfo;
                        if (!Backbone.Data.Players.TryGetValue(raider.UserIDString, out playerInfo))
                        {
                            Backbone.Data.Players[raider.UserIDString] = playerInfo = new PlayerInfo();
                        }

                        playerInfo.TotalRaids++;
                        playerInfo.Raids++;
                    }

                    if (Options.Rewards.Money > 0 && Backbone.Plugin.Economics != null && Backbone.Plugin.Economics.IsLoaded)
                    {
                        double money = _config.Settings.Management.DivideRewards ? Options.Rewards.Money / players.Count : Options.Rewards.Money;
                        Backbone.Plugin.Economics?.Call("Deposit", raider.UserIDString, money);
                        Backbone.Message(raider, "EconomicsDeposit", money);
                    }

                    if (Options.Rewards.Points > 0 && Backbone.Plugin.ServerRewards != null && Backbone.Plugin.ServerRewards.IsLoaded)
                    {
                        int points = _config.Settings.Management.DivideRewards ? Options.Rewards.Points / players.Count : Options.Rewards.Points;
                        Backbone.Plugin.ServerRewards?.Call("AddPoints", raider.userID, points);
                        Backbone.Message(raider, "ServerRewardPoints", points);
                    }
                }
            }

            private List<string> messagesSent = new List<string>();

            public bool CanMessage(BasePlayer player)
            {
                if (player == null || messagesSent.Contains(player.UserIDString))
                {
                    return false;
                }

                string uid = player.UserIDString;

                messagesSent.Add(uid);
                Backbone.Timer(10f, () => messagesSent.Remove(uid));

                return true;
            }

            private bool CanBypass(BasePlayer player)
            {
                return Backbone.HasPermission(player.UserIDString, canBypassPermission) || player.IsFlying;
            }

            public bool HasLockout(BasePlayer player)
            {
                if (!_config.Settings.Management.Lockout.Any() || !player.IsValid() || CanBypass(player))
                {
                    return false;
                }

                if (!IsOpened && Any(player.userID))
                {
                    return false;
                }

                if (player.userID == ownerId)
                {
                    return false;
                }

                Lockout lo;
                if (Backbone.Data.Lockouts.TryGetValue(player.UserIDString, out lo))
                {
                    double time = GetLockoutTime(Options.Mode, lo, player.UserIDString);

                    if (time > 0f)
                    {
                        if (CanMessage(player))
                        {
                            Backbone.Message(player, "LockedOut", DifficultyMode, FormatTime(time));
                        }

                        return true;
                    }
                }

                return false;
            }

            private void TrySetLockout(string playerId, BasePlayer player)
            {
                if (IsUnloading || Type == RaidableType.None || Backbone.HasPermission(playerId, canBypassPermission))
                {
                    return;
                }

                if (player.IsValid() && player.IsFlying)
                {
                    return;
                }

                double time = GetLockoutTime();

                if (time <= 0)
                {
                    return;
                }

                Lockout lo;
                if (!Backbone.Data.Lockouts.TryGetValue(playerId, out lo))
                {
                    Backbone.Data.Lockouts[playerId] = lo = new Lockout();
                }

                switch (Options.Mode)
                {
                    case RaidableMode.Easy:
                        {
                            if (lo.Easy <= 0)
                            {
                                lo.Easy = Epoch.Current + time;
                            }
                            return;
                        }
                    case RaidableMode.Medium:
                        {
                            if (lo.Medium <= 0)
                            {
                                lo.Medium = Epoch.Current + time;
                            }
                            return;
                        }
                    case RaidableMode.Hard:
                        {
                            if (lo.Hard <= 0)
                            {
                                lo.Hard = Epoch.Current + time;
                            }
                            return;
                        }
                    case RaidableMode.Expert:
                        {
                            if (lo.Expert <= 0)
                            {
                                lo.Expert = Epoch.Current + time;
                            }
                            return;
                        }
                    default:
                        {
                            if (lo.Nightmare <= 0)
                            {
                                lo.Nightmare = Epoch.Current + time;
                            }
                            return;
                        }
                }
            }

            private double GetLockoutTime()
            {
                switch (Options.Mode)
                {
                    case RaidableMode.Easy:
                        {
                            return _config.Settings.Management.Lockout.Easy * 60;
                        }
                    case RaidableMode.Medium:
                        {
                            return _config.Settings.Management.Lockout.Medium * 60;
                        }
                    case RaidableMode.Hard:
                        {
                            return _config.Settings.Management.Lockout.Hard * 60;
                        }
                    case RaidableMode.Expert:
                        {
                            return _config.Settings.Management.Lockout.Expert * 60;
                        }
                    default:
                        {
                            return _config.Settings.Management.Lockout.Nightmare * 60;
                        }
                }
            }

            public static double GetLockoutTime(RaidableMode mode, Lockout lo, string playerId)
            {
                double time;

                switch (mode)
                {
                    case RaidableMode.Easy:
                        {
                            time = lo.Easy;

                            if (time <= 0 || (time -= Epoch.Current) <= 0)
                            {
                                lo.Easy = 0;
                            }

                            break;
                        }
                    case RaidableMode.Medium:
                        {
                            time = lo.Medium;

                            if (time <= 0 || (time -= Epoch.Current) <= 0)
                            {
                                lo.Medium = 0;
                            }

                            break;
                        }
                    case RaidableMode.Hard:
                        {
                            time = lo.Hard;

                            if (time <= 0 || (time -= Epoch.Current) <= 0)
                            {
                                lo.Hard = 0;
                            }

                            break;
                        }
                    case RaidableMode.Expert:
                        {
                            time = lo.Expert;

                            if (time <= 0 || (time -= Epoch.Current) <= 0)
                            {
                                lo.Expert = 0;
                            }

                            break;
                        }
                    default:
                        {
                            time = lo.Nightmare;

                            if (time <= 0 || (time -= Epoch.Current) <= 0)
                            {
                                lo.Nightmare = 0;
                            }

                            break;
                        }
                }

                if (!lo.Any())
                {
                    Backbone.Data.Lockouts.Remove(playerId);
                }

                return time < 0 ? 0 : time;
            }

            public string Mode()
            {
                if (owner.IsValid())
                {
                    return string.Format("{0} {1}", owner.displayName, DifficultyMode.SentenceCase());
                }

                return DifficultyMode.SentenceCase();
            }

            public void TrySetPayLock(BasePlayer player)
            {
                if (!IsOpened)
                {
                    return;
                }

                if (player != null)
                {
                    IsPayLocked = true;
                    owner = player;
                    ownerId = player.userID;
                    friends.Add(player);
                    ClearEnemies();
                }
                else if (!IsPlayerActive(ownerId))
                {
                    IsPayLocked = false;
                    owner = null;
                    ownerId = 0;
                    friends.Clear();
                    raiders.Clear();
                }

                UpdateMarker();
            }

            private bool IsPlayerActive(ulong playerId)
            {
                if (_config.Settings.Management.LockTime <= 0f)
                {
                    return true;
                }

                float time;
                if (!lastActive.TryGetValue(playerId.ToString(), out time))
                {
                    return true;
                }

                return Time.realtimeSinceStartup - time <= _config.Settings.Management.LockTime * 60f;
            }

            public void TrySetOwner(BasePlayer attacker, BaseEntity entity, HitInfo hitInfo)
            {
                UpdateStatus(attacker);

                if (!_config.Settings.Management.UseOwners || !IsOpened || ownerId.IsSteamId())
                {
                    return;
                }

                if (_config.Settings.Management.BypassUseOwnersForPVP && AllowPVP)
                {
                    return;
                }

                if (_config.Settings.Management.BypassUseOwnersForPVE && !AllowPVP)
                {
                    return;
                }

                if (HasLockout(attacker) || IsHogging(attacker))
                {
                    NullifyDamage(hitInfo);
                    return;
                }

                if (IsOwner(attacker))
                {
                    return;
                }

                if (entity is NPCPlayerApex)
                {
                    SetOwner(attacker);
                    return;
                }

                if (!(entity is BuildingBlock) && !(entity is Door) && !(entity is SimpleBuildingBlock))
                {
                    return;
                }

                if (InRange(attacker.transform.position, Location, Options.ProtectionRadius) || IsLootingWeapon(hitInfo))
                {
                    SetOwner(attacker);
                }
            }

            private void SetOwner(BasePlayer player)
            {
                if (_config.Settings.Management.LockTime > 0f)
                {
                    if (IsInvoking(ResetOwner)) CancelInvoke(ResetOwner);
                    Invoke(ResetOwner, _config.Settings.Management.LockTime * 60f);
                }

                UpdateStatus(player);
                owner = player;
                ownerId = player.userID;
                UpdateMarker();
                ClearEnemies();
            }

            private void ClearEnemies()
            {
                var list = new List<ulong>();

                foreach (var raider in raiders)
                {
                    var target = raider.Value;

                    if (target == null || !IsAlly(target))
                    {
                        list.Add(raider.Key);
                    }
                }

                foreach (ulong targetId in list)
                {
                    raiders.Remove(targetId);
                }
            }

            public void CheckDespawn()
            {
                if (IsDespawning || _config.Settings.Management.DespawnMinutesInactive <= 0 || !_config.Settings.Management.DespawnMinutesInactiveReset)
                {
                    return;
                }

                if (_config.Settings.Management.Engaged && !IsEngaged)
                {
                    return;
                }

                if (IsInvoking(Despawn))
                {
                    CancelInvoke(Despawn);
                }

                float time = _config.Settings.Management.DespawnMinutesInactive * 60f;
                despawnTime = Time.realtimeSinceStartup + time;
                Invoke(Despawn, time);
            }

            public bool EndWhenCupboardIsDestroyed()
            {
                if (_config.Settings.Management.EndWhenCupboardIsDestroyed && privSpawned)
                {
                    return priv == null || priv.IsDestroyed;
                }

                return false;
            }

            public bool CanUndo()
            {
                if (EndWhenCupboardIsDestroyed())
                {
                    return true;
                }

                if (_config.Settings.Management.RequireCupboardLooted && privSpawned)
                {
                    if (priv.IsValid() && !priv.IsDestroyed && !priv.inventory.IsEmpty())
                    {
                        return false;
                    }
                }

                foreach (var container in _containers)
                {
                    if (container.IsValid() && !container.IsDestroyed && !container.inventory.IsEmpty() && IsBox(container.prefabID))
                    {
                        return false;
                    }
                }

                return true;
            }

            private bool CanPlayerBeLooted()
            {
                if (!_config.Settings.Management.PlayersLootableInPVE && !AllowPVP || !_config.Settings.Management.PlayersLootableInPVP && AllowPVP)
                {
                    return false;
                }

                return true;
            }

            private bool CanBeLooted(BasePlayer player, BaseEntity e)
            {
                if (IsProtectedWeapon(e))
                {
                    return false;
                }

                if (e is NPCPlayerCorpse)
                {
                    return true;
                }

                if (e is LootableCorpse)
                {
                    if (CanBypass(player))
                    {
                        return true;
                    }

                    var corpse = e as LootableCorpse;

                    if (!corpse.playerSteamID.IsSteamId() || corpse.playerSteamID == player.userID || corpse.playerName == player.displayName)
                    {
                        return true;
                    }

                    return CanPlayerBeLooted();
                }
                else if (e is DroppedItemContainer)
                {
                    if (CanBypass(player))
                    {
                        return true;
                    }

                    var container = e as DroppedItemContainer;

                    if (!container.playerSteamID.IsSteamId() || container.playerSteamID == player.userID || container.playerName == player.displayName)
                    {
                        return true;
                    }

                    return CanPlayerBeLooted();
                }

                return true;
            }

            public static bool IsProtectedWeapon(BaseEntity e)
            {
                return e is GunTrap || e is FlameTurret || e is FogMachine || e is SamSite || e is AutoTurret;
            }

            public void OnLootEntityInternal(BasePlayer player, BaseEntity e)
            {
                UpdateStatus(player);

                if (e.OwnerID == player.userID || e is BaseMountable)
                {
                    return;
                }

                if (_config.Settings.Management.BlacklistedPickupItems.Contains(e.ShortPrefabName))
                {
                    player.Invoke(player.EndLooting, 0.1f);
                    return;
                }

                if (e.HasParent() && e.GetParentEntity() is BaseMountable)
                {
                    return;
                }

                if (!CanBeLooted(player, e))
                {
                    player.Invoke(player.EndLooting, 0.1f);
                    return;
                }

                if (e is LootableCorpse || e is DroppedItemContainer)
                {
                    return;
                }

                if (player.isMounted)
                {
                    Backbone.Message(player, "CannotBeMounted");
                    player.Invoke(player.EndLooting, 0.1f);
                    return;
                }

                if (Options.RequiresCupboardAccess && !player.CanBuild()) //player.IsBuildingBlocked())
                {
                    Backbone.Message(player, "MustBeAuthorized");
                    player.Invoke(player.EndLooting, 0.1f);
                    return;
                }

                if (!IsAlly(player))
                {
                    Backbone.Message(player, "OwnerLocked");
                    player.Invoke(player.EndLooting, 0.1f);
                    return;
                }

                if (raiders.Count > 0 && Type != RaidableType.None)
                {
                    CheckDespawn();
                }

                AddLooter(player);

                if (IsBox(e.prefabID) || e is BuildingPrivlidge)
                {
                    StartTryToEnd();
                }
            }

            private void TryStartPlayingWithFire()
            {
                if (Options.Levels.Level1.Amount > 0)
                {
                    InvokeRepeating(StartPlayingWithFire, 2f, 2f);
                }
            }

            private void StartPlayingWithFire()
            {
                if (npcs.Count == 0)
                {
                    return;
                }

                var dict = Pool.Get<Dictionary<FireBall, PlayWithFire>>();

                foreach (var entry in fireballs)
                {
                    dict.Add(entry.Key, entry.Value);
                }

                foreach (var entry in dict)
                {
                    if (entry.Key == null || entry.Key.IsDestroyed)
                    {
                        Destroy(entry.Value);
                        fireballs.Remove(entry.Key);
                    }
                }

                dict.Clear();
                Pool.Free(ref dict);

                if (fireballs.Count >= Options.Levels.Level1.Amount || UnityEngine.Random.value > Options.Levels.Level1.Chance)
                {
                    return;
                }

                var npc = npcs.GetRandom();

                if (!IsValid(npc))
                {
                    return;
                }

                var fireball = GameManager.server.CreateEntity(Backbone.Path.FireballSmall, npc.transform.position + new Vector3(0f, 3f, 0f), Quaternion.identity, true) as FireBall;

                if (fireball == null)
                {
                    return;
                }

                var rb = fireball.GetComponent<Rigidbody>();

                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = false;
                    rb.drag = 0f;
                }

                fireball.lifeTimeMax = 15f;
                fireball.lifeTimeMin = 15f;
                fireball.canMerge = false;
                fireball.Spawn();
                fireball.CancelInvoke(fireball.TryToSpread);

                var component = fireball.gameObject.AddComponent<PlayWithFire>();

                component.Target = npc;

                fireballs.Add(fireball, component);
            }

            private void SetNoDrops()
            {
                foreach (var container in _allcontainers)
                {
                    if (!container.IsValid()) continue;
                    container.dropChance = 0f;
                }
            }

            public void DestroyInputs()
            {
                if (Inputs.Count > 0)
                {
                    foreach (var input in Inputs.ToList())
                    {
                        Destroy(input.Value);
                    }

                    Inputs.Clear();
                }
            }

            public void DestroyFire()
            {
                if (fireballs.Count == 0)
                {
                    return;
                }

                foreach (var entry in fireballs)
                {
                    Destroy(entry.Value);

                    if (entry.Key == null)
                    {
                        continue;
                    }

                    entry.Key.Extinguish();
                }

                fireballs.Clear();
            }

            public void SetEntities(int baseIndex, List<BaseEntity> entities)
            {
                if (!IsLoading)
                {
                    Entities = entities;
                    BaseIndex = baseIndex;
                    setupRoutine = StartCoroutine(EntitySetup());
                }
            }

            private Vector3 GetCenterFromMultiplePoints()
            {
                if (foundations.Count <= 1)
                {
                    return PastedLocation;
                }

                float x = 0f;
                float z = 0f;

                foreach (var position in foundations)
                {
                    x += position.x;
                    z += position.z;
                }

                var vector = new Vector3(x / foundations.Count, 0f, z / foundations.Count);

                vector.y = GetSpawnHeight(vector);

                return vector;
            }

            private void CreateSpheres()
            {
                if (Options.SphereAmount <= 0)
                {
                    return;
                }

                for (int i = 0; i < Options.SphereAmount; i++)
                {
                    var sphere = GameManager.server.CreateEntity(Backbone.Path.Sphere, Location, default(Quaternion), true) as SphereEntity;

                    if (sphere == null)
                    {
                        break;
                    }

                    sphere.currentRadius = 1f;
                    sphere.Spawn();
                    sphere.LerpRadiusTo(Options.ProtectionRadius * 2f, Options.ProtectionRadius * 0.75f);
                    spheres.Add(sphere);
                }
            }

            private void CreateZoneWalls()
            {
                if (!Options.ArenaWalls.Enabled)
                {
                    return;
                }

                var center = new Vector3(Location.x, Location.y, Location.z);
                string prefab = Options.ArenaWalls.Ice ? StringPool.Get(921229511) : Options.ArenaWalls.Stone ? Backbone.Path.HighExternalStoneWall : Backbone.Path.HighExternalWoodenWall;
                float maxHeight = -200f;
                float minHeight = 200f;
                int raycasts = Mathf.CeilToInt(360 / Options.ArenaWalls.Radius * 0.1375f);

                foreach (var position in GetCircumferencePositions(center, Options.ArenaWalls.Radius, raycasts, false))
                {
                    maxHeight = Mathf.Max(position.y, maxHeight, TerrainMeta.WaterMap.GetHeight(position));
                    minHeight = Mathf.Min(position.y, minHeight);
                    center.y = minHeight;
                }

                float gap = prefab == Backbone.Path.HighExternalStoneWall ? 0.3f : 0.5f;
                int stacks = Mathf.CeilToInt((maxHeight - minHeight) / 6f) + Options.ArenaWalls.Stacks;
                float next = 360 / Options.ArenaWalls.Radius - gap;
                float j = Options.ArenaWalls.Stacks * 6f + 6f;
                float groundHeight = 0f;
                BaseEntity e;

                for (int i = 0; i < stacks; i++)
                {
                    foreach (var position in GetCircumferencePositions(center, Options.ArenaWalls.Radius, next, false, center.y))
                    {
                        if (Location.y - position.y > 48f)
                        {
                            continue;
                        }

                        groundHeight = TerrainMeta.HeightMap.GetHeight(new Vector3(position.x, position.y + 6f, position.z));

                        if (groundHeight > position.y + 9f)
                        {
                            continue;
                        }

                        if (Options.ArenaWalls.LeastAmount)
                        {
                            float h = TerrainMeta.HeightMap.GetHeight(position);

                            if (position.y - groundHeight > j && position.y < h)
                            {
                                continue;
                            }
                        }

                        e = GameManager.server.CreateEntity(prefab, position, default(Quaternion), false);

                        if (e == null)
                        {
                            return;
                        }

                        e.OwnerID = 0;
                        e.transform.LookAt(center, Vector3.up);

                        if (Options.ArenaWalls.UseUFOWalls)
                        {
                            e.transform.Rotate(-67.5f, 0f, 0f);
                        }

                        e.enableSaving = false;
                        e.Spawn();
                        e.gameObject.SetActive(true);

                        if (CanSetupEntity(e))
                        {
                            Entities.Add(e);
                            Backbone.Plugin.RaidEntities[e] = this;
                        }

                        if (stacks == i - 1)
                        {
                            RaycastHit hit;
                            if (Physics.Raycast(new Vector3(position.x, position.y + 6f, position.z), Vector3.down, out hit, 12f, Layers.Mask.World))
                            {
                                stacks++;
                            }
                        }
                    }

                    center.y += 6f;
                }
            }

            private void KillTrees()
            {
                BaseEntity e;
                int hits = Physics.OverlapSphereNonAlloc(Location, Radius, Vis.colBuffer, Layers.Mask.Tree, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits; i++)
                {
                    e = Vis.colBuffer[i].ToBaseEntity();

                    if (e != null && !e.IsDestroyed)
                    {
                        e.Kill();
                    }

                    Vis.colBuffer[i] = null;
                }
            }

            private IEnumerator EntitySetup()
            {
                var list = new List<BaseEntity>(Entities);
                var _instruction = ConVar.FPS.limit > 80 ? CoroutineEx.waitForSeconds(INSTRUCTION_TIME) : null;

                foreach (var e in list)
                {
                    if (!CanSetupEntity(e))
                    {
                        yield return _instruction;
                        continue;
                    }

                    Backbone.Plugin.RaidEntities[e] = this;

                    if (e.net.ID < NetworkID)
                    {
                        NetworkID = e.net.ID;
                    }

                    e.OwnerID = 0;

                    if (!Options.AllowPickup && e is BaseCombatEntity)
                    {
                        SetupPickup(e as BaseCombatEntity);
                    }

                    if (e is IOEntity)
                    {
                        if (e is ContainerIOEntity)
                        {
                            SetupIO(e as ContainerIOEntity);
                        }

                        if (e is AutoTurret)
                        {
                            SetupTurret(e as AutoTurret);
                        }
                        else if (e is Igniter)
                        {
                            SetupIgniter(e as Igniter);
                        }
                        else if (e is SamSite)
                        {
                            SetupSamSite(e as SamSite);
                        }
                        else if (e is TeslaCoil)
                        {
                            SetupTeslaCoil(e as TeslaCoil);
                        }
                        else if (e is SearchLight)
                        {
                            SetupSearchLight(e as SearchLight);
                        }
                        else if (e is CustomDoorManipulator)
                        {
                            doorControllers.Add(e as CustomDoorManipulator);
                        }
                    }
                    else if (e is StorageContainer)
                    {
                        if (e is BaseOven)
                        {
                            ovens.Add(e as BaseOven);
                        }
                        else if (e is GunTrap)
                        {
                            SetupGunTrap(e as GunTrap);
                        }
                        else if (e is FogMachine)
                        {
                            SetupFogMachine(e as FogMachine);
                        }
                        else if (e is FlameTurret)
                        {
                            SetupFlameTurret(e as FlameTurret);
                        }
                        else if (e is VendingMachine)
                        {
                            SetupVendingMachine(e as VendingMachine);
                        }
                        else if (e is BuildingPrivlidge)
                        {
                            SetupBuildingPriviledge(e as BuildingPrivlidge);
                        }

                        SetupContainer(e as StorageContainer);
                    }
                    else if (e is BuildingBlock)
                    {
                        SetupBuildingBlock(e as BuildingBlock);
                    }
                    else if (e is Door)
                    {
                        doors.Add(e as Door);
                    }
                    else if (e is BaseLock)
                    {
                        SetupLock(e);
                    }
                    else if (e is SleepingBag)
                    {
                        SetupSleepingBag(e as SleepingBag);
                    }
                    else if (e is BaseMountable)
                    {
                        mountables.Add(e as BaseMountable);
                    }

                    if (e is DecayEntity)
                    {
                        SetupDecayEntity(e as DecayEntity);
                    }

                    yield return _instruction;
                }

                yield return CoroutineEx.waitForSeconds(2f);

                SetupCollider();
                SetupLoot();
                Subscribe();
                CreateZoneWalls();
                KillTrees();
                CreateGenericMarker();
                CreateSpheres();
                EjectSleepers();
                UpdateMarker();
                TryStartPlayingWithFire();
                SetupLights();
                SetupDoorControllers();
                SetupDoors();
                CheckDespawn();
                SetupContainers();
                MakeAnnouncements();
                InvokeRepeating(Protector, 1f, 1f);

                setupRoutine = null;
                Interface.CallHook("OnRaidableBaseStarted", Location, (int)Options.Mode);
            }

            private void SetupLights()
            {
                if (Backbone.Plugin.NightLantern == null)
                {
                    if (_config.Settings.Management.Lights)
                    {
                        InvokeRepeating(Lights, 1f, 1f);
                    }
                    else if (_config.Settings.Management.AlwaysLights)
                    {
                        Lights();
                    }
                }
            }

            private void SetupCollider()
            {
                transform.position = Location = GetCenterFromMultiplePoints();

                var collider = gameObject.GetComponent<SphereCollider>() ?? gameObject.AddComponent<SphereCollider>();
                collider.radius = Options.ProtectionRadius;
                collider.isTrigger = true;
                collider.center = Vector3.zero;
                gameObject.layer = (int)Layer.Trigger;
            }

            private void PopulateLoot(bool unique)
            {
                if (unique)
                {
                    if (!_config.Treasure.UniqueBaseLoot && BaseLoot.Count > 0)
                    {
                        AddToLoot(BaseLoot);
                    }

                    if (!_config.Treasure.UniqueDifficultyLoot && DifficultyLoot.Count > 0)
                    {
                        AddToLoot(DifficultyLoot);
                    }

                    if (!_config.Treasure.UniqueDefaultLoot && DefaultLoot.Count > 0)
                    {
                        AddToLoot(DefaultLoot);
                    }
                }
                else
                {
                    if (BaseLoot.Count > 0)
                    {
                        AddToLoot(BaseLoot);
                    }

                    if (DifficultyLoot.Count > 0)
                    {
                        AddToLoot(DifficultyLoot);
                    }

                    if (DefaultLoot.Count > 0)
                    {
                        AddToLoot(DefaultLoot);
                    }
                }
            }

            private void SetupLoot()
            {
                _containers.RemoveAll(x => !IsValid(x));

                if (_containers.Count == 0)
                {
                    Puts(Backbone.GetMessage("NoContainersFound", null, BaseName, PositionToGrid(Location)));
                    return;
                }

                CheckExpansionSettings();

                if (Options.SkipTreasureLoot || Options.TreasureAmount <= 0)
                {
                    return;
                }

                if (Options.EmptyAll)
                {
                    foreach (var container in _containers)
                    {
                        container.inventory.Clear();
                    }

                    ItemManager.DoRemoves();
                }

                if (_config.Settings.Management.IgnoreContainedLoot)
                {
                    _containers.RemoveAll(x => !x.inventory.IsEmpty());
                }

                var containers = Pool.GetList<StorageContainer>();

                foreach (var container in _containers)
                {
                    if (IsBox(container.prefabID))
                    {
                        containers.Add(container);
                    }
                }

                if (containers.Count == 0)
                {
                    Pool.FreeList(ref containers);
                    Puts(Backbone.GetMessage("NoBoxesFound", null, BaseName, PositionToGrid(Location)));
                    return;
                }

                List<TreasureItem> baseLoot;
                if (Buildings.BaseLoot.TryGetValue(BaseName, out baseLoot))
                {
                    TakeLootFrom(baseLoot, BaseLoot);
                }
                else baseLoot = new List<TreasureItem>();

                if (BaseLoot.Count < Options.TreasureAmount)
                {
                    switch (Options.Mode)
                    {
                        case RaidableMode.Easy:
                            {
                                TakeLootFrom(LootType.Easy);
                                break;
                            }
                        case RaidableMode.Medium:
                            {
                                TakeLootFrom(LootType.Medium);
                                break;
                            }
                        case RaidableMode.Hard:
                            {
                                TakeLootFrom(LootType.Hard);
                                break;
                            }
                        case RaidableMode.Expert:
                            {
                                TakeLootFrom(LootType.Expert);
                                break;
                            }
                        case RaidableMode.Nightmare:
                            {
                                TakeLootFrom(LootType.Nightmare);
                                break;
                            }
                    }
                }

                if (BaseLoot.Count + DifficultyLoot.Count < Options.TreasureAmount)
                {
                    TakeLootFrom(TreasureLoot, DefaultLoot);
                }

                PopulateLoot(true);

                if (Options.AllowDuplicates)
                {
                    if (Loot.Count > 0 && Loot.Count < Options.TreasureAmount)
                    {
                        do
                        {
                            Loot.Add(Loot.GetRandom());
                        } while (Loot.Count < Options.TreasureAmount);
                    }
                }

                PopulateLoot(false);

                if (Loot.Count == 0)
                {
                    Pool.FreeList(ref containers);
                    Puts(Backbone.GetMessage("NoConfiguredLoot"));
                    return;
                }

                if (!Options.AllowDuplicates)
                {
                    var newLoot = new List<TreasureItem>();

                    foreach (var ti in Loot)
                    {
                        if (ti.modified || !newLoot.Any(x => x.shortname == ti.shortname))
                        {
                            newLoot.Add(ti);
                        }
                    }

                    Loot = newLoot;
                }

                Shuffle(Loot);

                if (Loot.Count > Options.TreasureAmount)
                {
                    int index = Loot.Count;

                    while (Loot.Count > Options.TreasureAmount && --index >= 0)
                    {
                        var ti = Loot[index];

                        if (Options.Prioritize && baseLoot.Contains(ti))
                        {
                            continue;
                        }

                        Loot.RemoveAt(index);
                    }
                }

                if (Options.DivideLoot)
                {
                    DivideLoot(containers);
                }
                else
                {
                    SpawnLoot(containers);
                }

                if (itemAmountSpawned == 0)
                {
                    Puts(Backbone.GetMessage("NoLootSpawned"));
                }

                Pool.FreeList(ref containers);
            }

            private void SetupContainers()
            {
                foreach (var container in _containers)
                {
                    container.inventory.onItemAddedRemoved += new Action<Item, bool>(OnItemAddedRemoved);
                    if (container.prefabID != LARGE_WOODEN_BOX) continue;
                    container.SendNetworkUpdate();
                }
            }

            private void SetupPickup(BaseCombatEntity e)
            {
                e.pickup.enabled = false;
            }

            private void CreateInventory(StorageContainer container)
            {
                container.inventory = new ItemContainer();
                container.inventory.ServerInitialize(null, 30);
                container.inventory.GiveUID();
                container.inventory.entityOwner = container;
            }

            private void AddContainer(StorageContainer container)
            {
                if (!Entities.Contains(container))
                {
                    Entities.Add(container);
                }

                if (!_allcontainers.Contains(container))
                {
                    _allcontainers.Add(container);
                }

                if (!_containers.Contains(container) && (IsBox(container.prefabID) || container is BuildingPrivlidge))
                {
                    _containers.Add(container);
                }
            }

            private void SetupContainer(StorageContainer container)
            {
                if (container.inventory == null)
                {
                    CreateInventory(container);
                }

                AddContainer(container);

                if (IsBox(container.prefabID))
                {
                    if (skinId == 0uL)
                    {
                        if (_config.Skins.PresetSkin == 0uL)
                        {
                            skinId = BoxSkins.GetRandom();
                        }
                        else skinId = _config.Skins.PresetSkin;
                    }

                    if (_config.Skins.PresetSkin != 0uL || Options.SetSkins)
                    {
                        container.skinID = skinId;
                    }
                    else if (_config.Skins.RandomSkins && BoxSkins.Count > 0)
                    {
                        container.skinID = BoxSkins.GetRandom();
                    }
                }

                if (Type == RaidableType.None && container.inventory.itemList.Count > 0)
                {
                    return;
                }

                container.dropChance = 0f;

                if (container is BuildingPrivlidge)
                {
                    container.dropChance = _config.Settings.Management.AllowCupboardLoot ? 1f : 0f;
                }
                else if (!IsProtectedWeapon(container) && !(container is VendingMachine))
                {
                    container.dropChance = 1f;
                }

                container.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
            }

            private void SetupIO(ContainerIOEntity io)
            {
                io.dropChance = IsProtectedWeapon(io) ? 0f : 1f;
                io.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
            }

            private void SetupIO(IOEntity io)
            {
                io.SetFlag(BaseEntity.Flags.Reserved8, true, false, true);
            }

            private void SetupLock(BaseEntity e, bool justCreated = false)
            {
                if (!Entities.Contains(e))
                {
                    Entities.Add(e);
                }

                if (Type == RaidableType.None)
                {
                    return;
                }

                if (e is CodeLock)
                {
                    var codeLock = e as CodeLock;

                    if (_config.Settings.Management.RandomCodes || justCreated)
                    {
                        codeLock.code = UnityEngine.Random.Range(1000, 9999).ToString();
                        codeLock.hasCode = true;
                    }

                    codeLock.OwnerID = 0;
                    codeLock.guestCode = string.Empty;
                    codeLock.hasGuestCode = false;
                    codeLock.guestPlayers.Clear();
                    codeLock.whitelistPlayers.Clear();
                    codeLock.SetFlag(BaseEntity.Flags.Locked, true);
                }
                else if (e is KeyLock)
                {
                    var keyLock = e as KeyLock;

                    if (_config.Settings.Management.RandomCodes)
                    {
                        keyLock.keyCode = UnityEngine.Random.Range(1, 100000);
                    }

                    keyLock.OwnerID = 0;
                    keyLock.firstKeyCreated = true;
                    keyLock.SetFlag(BaseEntity.Flags.Locked, true);
                }
            }

            private void SetupVendingMachine(VendingMachine vm)
            {
                if (_config.Settings.Management.AllowBroadcasting)
                {
                    return;
                }

                vm.SetFlag(BaseEntity.Flags.Reserved4, false, false, true);
                vm.UpdateMapMarker();
            }

            private void SetupSearchLight(SearchLight light)
            {
                if (!_config.Settings.Management.Lights && !_config.Settings.Management.AlwaysLights)
                {
                    return;
                }

                lights.Add(light);

                light.enabled = false;
            }

            private void SetupBuildingBlock(BuildingBlock block)
            {
                if (Options.Tiers.Any())
                {
                    ChangeTier(block);
                }

                block.StopBeingDemolishable();
                block.StopBeingRotatable();

                if (block.transform == null)
                {
                    return;
                }

                if (block.prefabID == 3234260181 || block.prefabID == 72949757) // triangle and square foundations
                {
                    foundations.Add(block.transform.position);
                }
            }

            private void ChangeTier(BuildingBlock block)
            {
                if (Options.Tiers.HQM && block.grade != BuildingGrade.Enum.TopTier)
                {
                    SetGrade(block, BuildingGrade.Enum.TopTier);
                }
                else if (Options.Tiers.Metal && block.grade != BuildingGrade.Enum.Metal)
                {
                    SetGrade(block, BuildingGrade.Enum.Metal);
                }
                else if (Options.Tiers.Stone && block.grade != BuildingGrade.Enum.Stone)
                {
                    SetGrade(block, BuildingGrade.Enum.Stone);
                }
                else if (Options.Tiers.Wooden && block.grade != BuildingGrade.Enum.Wood)
                {
                    SetGrade(block, BuildingGrade.Enum.Wood);
                }
            }

            private void SetGrade(BuildingBlock block, BuildingGrade.Enum grade)
            {
                block.SetGrade(grade);
                block.SetHealthToMax();
                block.SendNetworkUpdate();
                block.UpdateSkin();
            }

            private void SetupTeslaCoil(TeslaCoil tc)
            {
                if (!_config.Weapons.TeslaCoil.RequiresPower)
                {
                    tc.UpdateFromInput(25, 0);
                    tc.SetFlag(IOEntity.Flag_HasPower, true, false, true);
                }

                tc.maxDischargeSelfDamageSeconds = Mathf.Clamp(_config.Weapons.TeslaCoil.MaxDischargeSelfDamageSeconds, 0f, 9999f);
                tc.maxDamageOutput = Mathf.Clamp(_config.Weapons.TeslaCoil.MaxDamageOutput, 0f, 9999f);
            }

            private void SetupIgniter(Igniter igniter)
            {
                igniter.SelfDamagePerIgnite = 0f;
            }

            private void SetupTurret(AutoTurret turret)
            {
                SetupIO(turret as IOEntity);

                if (Type != RaidableType.None)
                {
                    turret.authorizedPlayers.Clear();
                }

                turret.InitializeHealth(Options.AutoTurretHealth, Options.AutoTurretHealth);
                turret.sightRange = Options.AutoTurretSightRange;
                turret.aimCone = Options.AutoTurretAimCone;
                turrets.Add(turret);

                if (Options.RemoveTurretWeapon)
                {
                    turret.AttachedWeapon = null;
                    Item slot = turret.inventory.GetSlot(0);

                    if (slot != null && (slot.info.category == ItemCategory.Weapon || slot.info.category == ItemCategory.Fun))
                    {
                        slot.RemoveFromContainer();
                        slot.Remove();
                    }
                }

                if (turret.AttachedWeapon == null)
                {
                    var itemToCreate = ItemManager.FindItemDefinition(Options.AutoTurretShortname);

                    if (itemToCreate != null)
                    {
                        Item item = ItemManager.Create(itemToCreate, 1, (ulong)itemToCreate.skins.GetRandom().id);

                        if (!item.MoveToContainer(turret.inventory, 0, false))
                        {
                            item.Remove();
                        }
                    }
                }

                turret.Invoke(turret.UpdateAttachedWeapon, 0.1f);
                turret.Invoke(() => FillAmmoTurret(turret), 0.2f);
                turret.SetPeacekeepermode(false);

                if (_config.Settings.Management.AutoTurretPowerOnOff)
                {
                    SetElectricalSources(turret);
                }
                else turret.Invoke(turret.InitiateStartup, 0.3f);

                if (_config.Weapons.InfiniteAmmo.AutoTurret)
                {
                    turret.inventory.onPreItemRemove += new Action<Item>(OnWeaponItemPreRemove);
                }
            }

            private void SetElectricalSources(AutoTurret turret)
            {
                IOEntity source = turret;

                while ((source = GetConnectedInput(source)).IsValid())
                {
                    Backbone.Plugin.ElectricalConnections[source.net.ID] = turret;

                    if (IsElectricalSource(source))
                    {
                        return;
                    }
                }
            }

            private IOEntity GetConnectedInput(IOEntity io)
            {
                if (io == null || io.inputs == null)
                {
                    return null;
                }

                foreach (var input in io.inputs)
                {
                    var e = input?.connectedTo?.Get(true);

                    if (e.IsValid())
                    {
                        return e;
                    }
                }

                return null;
            }

            public bool IsElectricalSource(IOEntity io)
            {
                return io is ElectricBattery || io is SolarPanel || io is ElectricWindmill || io is ElectricGenerator || io is FuelElectricGenerator || io is FuelGenerator;
            }

            private void SetupGunTrap(GunTrap gt)
            {
                if (_config.Weapons.Ammo.GunTrap > 0)
                {
                    FillAmmoGunTrap(gt);
                }

                if (_config.Weapons.InfiniteAmmo.GunTrap)
                {
                    gt.inventory.onPreItemRemove += new Action<Item>(OnWeaponItemPreRemove);
                }
            }

            private void SetupFogMachine(FogMachine fm)
            {
                if (_config.Weapons.Ammo.FogMachine > 0)
                {
                    FillAmmoFogMachine(fm);
                }

                if (_config.Weapons.InfiniteAmmo.FogMachine)
                {
                    fm.fuelPerSec = 0f;
                }

                if (_config.Weapons.FogMotion)
                {
                    fm.SetFlag(BaseEntity.Flags.Reserved7, true, false, true);
                }

                if (!_config.Weapons.FogRequiresPower)
                {
                    fm.CancelInvoke(fm.CheckTrigger);
                    fm.SetFlag(BaseEntity.Flags.Reserved6, true, false, true);
                    fm.SetFlag(BaseEntity.Flags.Reserved8, true, false, true);
                    fm.SetFlag(BaseEntity.Flags.On, true, false, true);
                }
            }

            private void SetupFlameTurret(FlameTurret ft)
            {
                ft.InitializeHealth(Options.FlameTurretHealth, Options.FlameTurretHealth);

                if (_config.Weapons.Ammo.FlameTurret > 0)
                {
                    FillAmmoFlameTurret(ft);
                }

                if (_config.Weapons.InfiniteAmmo.FlameTurret)
                {
                    ft.fuelPerSec = 0f;
                }
            }

            private void SetupSamSite(SamSite ss)
            {
                SetupIO(ss as IOEntity);

                if (_config.Weapons.SamSiteRepair > 0f)
                {
                    ss.staticRespawn = true;
                    ss.InvokeRepeating(ss.SelfHeal, _config.Weapons.SamSiteRepair * 60f, _config.Weapons.SamSiteRepair * 60f);
                }

                if (_config.Weapons.SamSiteRange > 0f)
                {
                    ss.scanRadius = _config.Weapons.SamSiteRange;
                }

                if (_config.Weapons.Ammo.SamSite > 0)
                {
                    FillAmmoSamSite(ss);
                }

                if (_config.Weapons.InfiniteAmmo.SamSite)
                {
                    ss.inventory.onPreItemRemove += new Action<Item>(OnWeaponItemPreRemove);
                }
            }

            private void SetupDoor(Door door)
            {
                if (Options.DoorLock)
                {
                    CreateLock(door);
                }

                if (!Options.CloseOpenDoors)
                {
                    return;
                }

                door.SetOpen(false, true);
            }

            private void SetupDoors()
            {
                doors.RemoveAll(x => x == null || x.IsDestroyed);

                foreach (var door in doors)
                {
                    if (door == null || door.IsDestroyed)
                    {
                        continue;
                    }

                    SetupDoor(door);
                }

                doors.Clear();
            }

            private void SetupDoorControllers()
            {
                doorControllers.RemoveAll(x => x == null || x.IsDestroyed);

                foreach (var cdm in doorControllers)
                {
                    SetupIO(cdm);

                    if (cdm.IsPaired())
                    {
                        doors.Remove(cdm.targetDoor);
                        continue;
                    }

                    var door = cdm.FindDoor(true);

                    if (door.IsValid())
                    {
                        cdm.SetTargetDoor(door);
                        doors.Remove(door);

                        if (Options.DoorLock)
                        {
                            CreateLock(door);
                        }
                    }
                }

                doorControllers.Clear();
            }

            private void CreateLock(Door door)
            {
                if (door == null || door.IsDestroyed)
                {
                    return;
                }

                var slot = door.GetSlot(BaseEntity.Slot.Lock) as BaseLock;

                if (slot == null)
                {
                    CreateCodeLock(door);
                    return;
                }

                var keyLock = slot.GetComponent<KeyLock>();

                if (keyLock.IsValid() && !keyLock.IsDestroyed)
                {
                    keyLock.SetParent(null);
                    keyLock.Kill();
                }

                CreateCodeLock(door);
            }

            private void CreateCodeLock(Door door)
            {
                var codeLock = GameManager.server.CreateEntity(Backbone.Path.CodeLock, default(Vector3), default(Quaternion), true) as CodeLock;

                if (codeLock == null)
                {
                    return;
                }

                codeLock.gameObject.Identity();
                codeLock.SetParent(door, BaseEntity.Slot.Lock.ToString().ToLower());
                codeLock.enableSaving = false;
                codeLock.OwnerID = 0;
                codeLock.Spawn();
                door.SetSlot(BaseEntity.Slot.Lock, codeLock);
                Backbone.Plugin.AddEntity(codeLock, this);

                SetupLock(codeLock, true);
            }

            private void SetupBuildingPriviledge(BuildingPrivlidge priv)
            {
                if (Type != RaidableType.None)
                {
                    priv.authorizedPlayers.Clear();
                    priv.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }

                this.priv = priv;
                privSpawned = true;
            }

            private void SetupSleepingBag(SleepingBag bag)
            {
                if (Type == RaidableType.None)
                {
                    return;
                }

                bag.deployerUserID = 0uL;
            }

            private void SetupDecayEntity(DecayEntity decayEntity)
            {
                if (BuildingID == 0)
                {
                    BuildingID = BuildingManager.server.NewBuildingID();
                }

                decayEntity.AttachToBuilding(BuildingID);
                decayEntity.decay = null;
            }

            private void Subscribe()
            {
                if (IsUnloading)
                {
                    return;
                }

                if (Options.EnforceDurability)
                {
                    Subscribe(nameof(OnLoseCondition));
                }

                Subscribe(nameof(CanPickupEntity));

                if (Options.NPC.Enabled)
                {
                    if (Options.NPC.SpawnAmount < 1)
                    {
                        Options.NPC.Enabled = false;
                    }

                    if (Options.NPC.SpawnAmount > 25)
                    {
                        Options.NPC.SpawnAmount = 25;
                    }

                    if (Options.NPC.SpawnMinAmount < 1 || Options.NPC.SpawnMinAmount > Options.NPC.SpawnAmount)
                    {
                        Options.NPC.SpawnMinAmount = 1;
                    }

                    if (Options.NPC.ScientistHealth < 100)
                    {
                        Options.NPC.ScientistHealth = 100f;
                    }

                    if (Options.NPC.ScientistHealth > 5000)
                    {
                        Options.NPC.ScientistHealth = 5000f;
                    }

                    if (Options.NPC.MurdererHealth < 100)
                    {
                        Options.NPC.MurdererHealth = 100f;
                    }

                    if (Options.NPC.MurdererHealth > 5000)
                    {
                        Options.NPC.MurdererHealth = 5000f;
                    }

                    if (_config.Settings.Management.FlameTurrets)
                    {
                        Subscribe(nameof(CanBeTargeted));
                    }

                    Subscribe(nameof(OnNpcKits));
                    Subscribe(nameof(OnNpcTarget));
                    SetupNpcKits();
                    Invoke(SpawnNpcs, 1f);
                }

                Subscribe(nameof(OnPlayerDeath));
                Subscribe(nameof(OnPlayerDropActiveItem));
                Subscribe(nameof(OnEntityDeath));
                Subscribe(nameof(OnEntityKill));

                if (!_config.Settings.Management.AllowTeleport)
                {
                    Subscribe(nameof(CanTeleport));
                    Subscribe(nameof(canTeleport));
                }

                if (_config.Settings.Management.BlockRestorePVP && AllowPVP || _config.Settings.Management.BlockRestorePVE && !AllowPVP)
                {
                    Subscribe(nameof(OnRestoreUponDeath));
                }

                if (_config.Settings.Management.UseOwners || _config.Settings.Buyable.UsePayLock)
                {
                    Subscribe(nameof(OnFriendAdded));
                    Subscribe(nameof(OnFriendRemoved));
                    Subscribe(nameof(OnClanUpdate));
                    Subscribe(nameof(OnClanDestroy));
                }

                if (Options.DropTimeAfterLooting > 0)
                {
                    Subscribe(nameof(OnLootEntityEnd));
                }

                if (!_config.Settings.Management.BackpacksOpenPVP || !_config.Settings.Management.BackpacksOpenPVE)
                {
                    Subscribe(nameof(CanOpenBackpack));
                }

                if (_config.Settings.Management.PreventFireFromSpreading)
                {
                    Subscribe(nameof(OnFireBallSpread));
                }

                Subscribe(nameof(CanBuild));
                Subscribe(nameof(CanDropBackpack));
                Subscribe(nameof(OnEntityGroundMissing));
                Subscribe(nameof(CanEntityBeTargeted));
                Subscribe(nameof(CanEntityTrapTrigger));
                Subscribe(nameof(OnLootEntity));
                Subscribe(nameof(OnEntityBuilt));
                Subscribe(nameof(OnCupboardAuthorize));
                Subscribe(nameof(OnEntityMounted));
            }

            private void Subscribe(string hook) => Backbone.Plugin.Subscribe(hook);

            private void MakeAnnouncements()
            {
                if (Type == RaidableType.None)
                {
                    itemAmountSpawned = 0;

                    foreach (var x in _allcontainers)
                    {
                        if (x == null || x.IsDestroyed)
                        {
                            continue;
                        }

                        itemAmountSpawned += x.inventory.itemList.Count;
                    }
                }

                var posStr = FormatGridReference(Location);

                Puts("{0} @ {1} : {2} items", BaseName, posStr, itemAmountSpawned);

                foreach (var target in BasePlayer.activePlayerList)
                {
                    float distance = Mathf.Floor((target.transform.position - Location).magnitude);
                    string flag = Backbone.GetMessage(AllowPVP ? "PVPFlag" : "PVEFlag", target.UserIDString).Replace("[", string.Empty).Replace("] ", string.Empty);
                    string api = Backbone.GetMessage("RaidOpenMessage", target.UserIDString, DifficultyMode, posStr, distance, flag);
                    if (Type == RaidableType.None) api = api.Replace(DifficultyMode, NoMode);
                    string message = owner.IsValid() ? string.Format("{0}[Owner: {1}]", api, owner.displayName) : api;

                    if ((!IsPayLocked && _config.EventMessages.Opened) || (IsPayLocked && _config.EventMessages.OpenedAndPaid))
                    {
                        target.SendConsoleCommand("chat.add", 2, _config.Settings.ChatID, message);
                    }

                    if (_config.GUIAnnouncement.Enabled && Backbone.Plugin.GUIAnnouncements != null && Backbone.Plugin.GUIAnnouncements.IsLoaded && distance <= _config.GUIAnnouncement.Distance)
                    {
                        Backbone.Plugin.GUIAnnouncements?.Call("CreateAnnouncement", message, _config.GUIAnnouncement.TintColor, _config.GUIAnnouncement.TextColor, target);
                    }
                }
            }

            private float _lastInvokeUpdate = Time.time;

            private void UpdateStatus(BasePlayer player)
            {
                if (IsOpened)
                {
                    lastActive[player.UserIDString] = Time.realtimeSinceStartup;
                }

                if (ownerId == player.userID && Time.time - _lastInvokeUpdate > 1f)
                {
                    _lastInvokeUpdate = Time.time;
                    TryInvokeResetOwner();
                }
            }

            private void TryInvokeResetOwner()
            {
                if (_config.Settings.Management.LockTime > 0f)
                {
                    if (IsInvoking(ResetOwner)) CancelInvoke(ResetOwner);
                    Invoke(ResetOwner, _config.Settings.Management.LockTime * 60f);
                }
            }

            public void ResetOwner()
            {
                if (!IsOpened || IsPayLocked || IsPlayerActive(ownerId))
                {
                    TryInvokeResetOwner();
                    return;
                }

                owner = null;
                ownerId = 0;
                friends.Clear();
                UpdateMarker();
            }

            public void TryInvokeResetPayLock()
            {
                if (_config.Settings.Buyable.ResetDuration > 0 && IsPayLocked && IsOpened)
                {
                    CancelInvoke(ResetPayLock);
                    Invoke(ResetPayLock, _config.Settings.Buyable.ResetDuration * 60f);
                }
            }

            private void ResetPayLock()
            {
                if (!IsOpened || IsPlayerActive(ownerId) || owner.IsValid())
                {
                    return;
                }

                IsPayLocked = false;
                owner = null;
                ownerId = 0;
                friends.Clear();
                UpdateMarker();
            }

            private void Puts(string format, params object[] args)
            {
                Backbone.Plugin.Puts(format, args);
            }

            private void TakeLootFrom(LootType type)
            {
                List<TreasureItem> lootList;
                if (Buildings.DifficultyLoot.TryGetValue(type, out lootList))
                {
                    TakeLootFrom(lootList, DifficultyLoot);
                }
            }

            private void TakeLootFrom(List<TreasureItem> source, List<TreasureItem> to)
            {
                if (source.Count == 0)
                {
                    return;
                }

                var from = new List<TreasureItem>(source);

                from.RemoveAll(ti => ti == null || ti.amount <= 0);

                if (from.Count == 0)
                {
                    return;
                }

                Shuffle(from);

                if (Options.Prioritize)
                {
                    int difference = Math.Abs(Options.TreasureAmount - Loot.Count);
                    int amount = Math.Min(difference, from.Count);
                    to.AddRange(from.Take(amount));
                }
                else to.AddRange(from);
            }

            private List<TreasureItem> Loot { get; set; } = new List<TreasureItem>();
            private List<TreasureItem> BaseLoot { get; set; } = new List<TreasureItem>();
            private List<TreasureItem> DifficultyLoot { get; set; } = new List<TreasureItem>();
            private List<TreasureItem> DefaultLoot { get; set; } = new List<TreasureItem>();

            private bool HasSpace(StorageContainer container, int amount)
            {
                return container.inventory.itemList.Count + amount < container.inventory.capacity;
            }

            private void SpawnLoot(List<StorageContainer> containers)
            {
                StorageContainer container = null;

                foreach (var x in containers)
                {
                    if (HasSpace(x, Options.TreasureAmount))
                    {
                        container = x;
                        break;
                    }
                }

                if (container == null)
                {
                    container = containers.GetRandom();
                    container.inventory.Clear();
                    ItemManager.DoRemoves();
                }

                SpawnLoot(container, Loot, Options.TreasureAmount);
            }

            private void SpawnLoot(StorageContainer container, List<TreasureItem> loot, int total)
            {
                if (total > container.inventory.capacity)
                {
                    total = container.inventory.capacity;
                }

                for (int j = 0; j < total; j++)
                {
                    if (loot.Count == 0)
                    {
                        break;
                    }

                    var lootItem = loot.GetRandom();

                    loot.Remove(lootItem);

                    SpawnItem(lootItem, container);
                }
            }

            private void DivideLoot(List<StorageContainer> containers)
            {
                int index = 0;

                while (Loot.Count > 0 && containers.Count > 0)
                {
                    var container = containers[index];

                    if (!container.inventory.IsFull())
                    {
                        var lootItem = Loot.GetRandom();
                        var result = SpawnItem(lootItem, container);

                        if (result == SpawnResult.Transfer || result == SpawnResult.Failure)
                        {
                            index--;
                        }

                        Loot.Remove(lootItem);
                    }
                    else containers.Remove(container);

                    if (++index >= containers.Count)
                    {
                        index = 0;
                    }
                }
            }

            private void AddToLoot(List<TreasureItem> source)
            {
                foreach (var ti in source)
                {
                    bool isBlueprint = ti.shortname.EndsWith(".bp");
                    string shortname = isBlueprint ? ti.shortname.Replace(".bp", string.Empty) : ti.shortname;

                    ti.def = ItemManager.FindItemDefinition(shortname);
                    ti.isBlueprint = isBlueprint;

                    if (ti.def == null)
                    {
                        Puts("Invalid shortname in config: {0}", ti.shortname);
                        continue;
                    }

                    if (ti.amountMin < ti.amount)
                    {
                        ti.total = UnityEngine.Random.Range(ti.amountMin, ti.amount + 1);
                    }
                    else ti.total = ti.amount;

                    //ti.total = GetPercentAmount(ti.total);

                    if (_config.Treasure.UseStackSizeLimit)
                    {
                        var stacks = GetStacks(ti.total, ti.def.stackable);
                        bool isModified = ti.total > ti.def.stackable;

                        foreach (int stack in stacks)
                        {
                            Loot.Add(new TreasureItem
                            {
                                amount = stack,
                                amountMin = stack,
                                def = ti.def,
                                shortname = ti.shortname,
                                skin = ti.skin,
                                total = stack,
                                modified = isModified,
                                isBlueprint = isBlueprint
                            });
                        }
                    }
                    else Loot.Add(ti);
                }

                source.Clear();
            }

            private List<int> GetStacks(int amount, int maxStack)
            {
                var list = new List<int>();

                while (amount > maxStack)
                {
                    amount -= maxStack;
                    list.Add(maxStack);
                }

                list.Add(amount);

                return list;
            }

            private SpawnResult SpawnItem(TreasureItem lootItem, StorageContainer container)
            {
                int amount = lootItem.total;

                if (amount <= 0)
                {
                    return SpawnResult.Skipped;
                }

                var def = lootItem.def;
                ulong skin = lootItem.skin;

                if (_config.Treasure.RandomSkins && skin == 0 && def.stackable <= 1)
                {
                    var skins = GetItemSkins(def);

                    if (skins.Count > 0)
                    {
                        skin = skins.GetRandom();
                    }
                }

                Item item;

                if (lootItem.isBlueprint)
                {
                    item = ItemManager.CreateByItemID(-996920608, 1, 0);

                    if (item == null)
                    {
                        Puts("-996920608 invalid blueprint ID. Contact author.");
                        return SpawnResult.Skipped;
                    }

                    item.blueprintTarget = def.itemid;
                    item.amount = amount;
                }
                else item = ItemManager.Create(def, amount, skin);

                if (MoveToCupboard(item) || MoveToBBQ(item) || MoveToOven(item) || MoveToFridge(item))
                {
                    itemAmountSpawned++;
                    return SpawnResult.Transfer;
                }
                else if (item.MoveToContainer(container.inventory, -1, false))
                {
                    itemAmountSpawned++;
                    return SpawnResult.Success;
                }

                item.Remove();
                return SpawnResult.Failure;
            }

            private bool MoveToFridge(Item item)
            {
                if (!_config.Settings.Management.Food || _allcontainers.Count == 0 || item.info.category != ItemCategory.Food)
                {
                    return false;
                }

                if (_allcontainers.Count > 1)
                {
                    Shuffle(_allcontainers);
                }

                foreach (var x in _allcontainers)
                {
                    if (x == null || x.IsDestroyed)
                    {
                        continue;
                    }

                    if ((x.prefabID == 378293714 || x.prefabID == 1844023509) && item.MoveToContainer(x.inventory, -1, true))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool MoveToBBQ(Item item)
            {
                if (!_config.Settings.Management.Food || ovens.Count == 0 || item.info.category != ItemCategory.Food || !IsCookable(item.info))
                {
                    return false;
                }

                if (ovens.Count > 1)
                {
                    Shuffle(ovens);
                }

                foreach (var oven in ovens)
                {
                    if (oven == null || oven.IsDestroyed)
                    {
                        continue;
                    }

                    if ((oven.prefabID == 2409469892 || oven.prefabID == 2409469892) && item.MoveToContainer(oven.inventory, -1, true))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool MoveToCupboard(Item item)
            {
                if (!_config.Settings.Management.Cupboard || !privSpawned || item.info.category != ItemCategory.Resources || _config.Treasure.ExcludeFromCupboard.Contains(item.info.shortname))
                {
                    return false;
                }

                if (_config.Settings.Management.Cook && item.info.shortname.EndsWith(".ore") && MoveToOven(item))
                {
                    return true;
                }

                if (priv.IsValid() && !priv.IsDestroyed)
                {
                    return item.MoveToContainer(priv.inventory, -1, true);
                }

                return false;
            }

            private bool IsCookable(ItemDefinition def)
            {
                if (def.shortname.EndsWith(".cooked") || def.shortname.EndsWith(".burned") || def.shortname.EndsWith(".spoiled") || def.shortname == "lowgradefuel")
                {
                    return false;
                }

                return def.GetComponent<ItemModCookable>() || def.shortname == "wood";
            }

            private bool MoveToOven(Item item)
            {
                if (!_config.Settings.Management.Cook || ovens.Count == 0 || !IsCookable(item.info))
                {
                    return false;
                }

                if (ovens.Count > 1)
                {
                    Shuffle(ovens);
                }

                foreach (var oven in ovens)
                {
                    if (oven == null || oven.IsDestroyed)
                    {
                        continue;
                    }

                    if (item.MoveToContainer(oven.inventory, -1, true))
                    {
                        if (!oven.IsOn())
                        {
                            oven.SetFlag(BaseEntity.Flags.On, true, false, true);
                        }

                        if (!item.HasFlag(global::Item.Flag.OnFire))
                        {
                            item.SetFlag(global::Item.Flag.OnFire, true);
                            item.MarkDirty();
                        }

                        return true;
                    }
                }

                return false;
            }

            private void CheckExpansionSettings()
            {
                if (!_config.Settings.ExpansionMode || Backbone.Plugin.DangerousTreasures == null)
                {
                    return;
                }

                var boxes = Pool.GetList<StorageContainer>();

                foreach (var x in _containers)
                {
                    if (IsBox(x.prefabID))
                    {
                        boxes.Add(x);
                    }
                }

                if (boxes.Count > 0)
                {
                    Backbone.Plugin.DangerousTreasures?.Call("API_SetContainer", boxes.GetRandom(), Radius, !Options.NPC.Enabled || Options.NPC.UseExpansionNpcs);
                }

                Pool.FreeList(ref boxes);
            }

            private void ToggleNpcMinerHat(NPCPlayerApex npc, bool state)
            {
                if (npc == null || npc.inventory == null || npc.IsDead())
                {
                    return;
                }

                var slot = npc.inventory.FindItemID("hat.miner");

                if (slot == null)
                {
                    return;
                }

                if (state && slot.contents != null)
                {
                    slot.contents.AddItem(ItemManager.FindItemDefinition("lowgradefuel"), 50);
                }

                slot.SwitchOnOff(state);
                npc.inventory.ServerUpdate(0f);
            }

            private void Lights()
            {
                if (lights.Count == 0 && ovens.Count == 0 && npcs.Count == 0)
                {
                    CancelInvoke(Lights);
                    return;
                }

                if (_config.Settings.Management.AlwaysLights || (!lightsOn && !IsDayTime()))
                {
                    lights.RemoveAll(e => e == null || e.IsDestroyed);
                    ovens.RemoveAll(e => e == null || e.IsDestroyed);

                    var list = new List<BaseEntity>(lights);

                    list.AddRange(ovens);

                    foreach (var e in list)
                    {
                        if (!e.IsOn())
                        {
                            if (e.prefabID == 2931042549)
                            {
                                if ((e as BaseOven).inventory.IsEmpty())
                                {
                                    continue;
                                }
                            }

                            e.SetFlag(BaseEntity.Flags.On, true, false, true);
                        }
                    }

                    foreach (var npc in npcs)
                    {
                        ToggleNpcMinerHat(npc, true);
                    }

                    lightsOn = true;
                }
                else if (lightsOn && IsDayTime())
                {
                    lights.RemoveAll(e => e == null || e.IsDestroyed);
                    ovens.RemoveAll(e => e == null || e.IsDestroyed);

                    var list = new List<BaseEntity>(lights);

                    list.AddRange(ovens);

                    foreach (var e in list)
                    {
                        if (e.prefabID == 2931042549 || e.prefabID == 4160694184 || e.prefabID == 1374462671 || e.prefabID == 2162666837 || e.prefabID == 2409469892)
                        {
                            continue;
                        }

                        if (e.IsOn())
                        {
                            e.SetFlag(BaseEntity.Flags.On, false);
                        }
                    }

                    foreach (var npc in npcs)
                    {
                        ToggleNpcMinerHat(npc, false);
                    }

                    lightsOn = false;
                }
            }

            public bool IsDayTime() => TOD_Sky.Instance?.Cycle.DateTime.Hour >= 8 && TOD_Sky.Instance?.Cycle.DateTime.Hour < 20;

            public void Undo()
            {
                if (IsOpened)
                {
                    IsOpened = false;
                    CancelInvoke(ResetOwner);
                    Backbone.Plugin.UndoPaste(Location, this, BaseIndex, BuiltList.ToList());
                }
            }

            public bool Any(ulong targetId, bool checkFriends = true)
            {
                if (ownerId == targetId)
                {
                    return true;
                }

                foreach (var x in raiders)
                {
                    if (x.Key == targetId)
                    {
                        return true;
                    }
                }

                if (checkFriends)
                {
                    foreach (var x in friends)
                    {
                        if (x?.userID == targetId)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            public static bool IsOwner(BasePlayer player)
            {
                foreach (var raid in Backbone.Plugin.Raids.Values)
                {
                    if (raid.ownerId.IsSteamId() && raid.ownerId == player.userID && raid.IsOpened)
                    {
                        return true;
                    }
                }

                return false;
            }

            public static bool Has(ulong userID)
            {
                return Backbone.Plugin.Npcs.ContainsKey(userID);
            }

            public static bool Has(BaseEntity entity)
            {
                return Backbone.Plugin.RaidEntities.ContainsKey(entity);
            }

            public static int Get(RaidableType type)
            {
                int amount = 0;

                foreach (var raid in Backbone.Plugin.Raids.Values)
                {
                    if (raid.Type == type)
                    {
                        amount++;
                    }
                }

                return amount;
            }

            public static int Get(RaidableMode mode)
            {
                int amount = 0;

                foreach (var raid in Backbone.Plugin.Raids.Values)
                {
                    if (raid.Options.Mode == mode)
                    {
                        amount++;
                    }
                }

                return amount;
            }

            public static RaidableBase Get(ulong userID)
            {
                if (Backbone.Plugin.Npcs.ContainsKey(userID))
                {
                    return Backbone.Plugin.Npcs[userID];
                }

                return null;
            }

            public static RaidableBase Get(Vector3 target, float f = 0f)
            {
                foreach (var raid in Backbone.Plugin.Raids.Values)
                {
                    if (InRange(raid.Location, target, raid.Options.ProtectionRadius + f))
                    {
                        return raid;
                    }
                }

                return null;
            }

            public static RaidableBase Get(int baseIndex)
            {
                if (Backbone.Plugin.Indices.ContainsKey(baseIndex))
                {
                    return Backbone.Plugin.Indices[baseIndex];
                }

                return null;
            }

            public static RaidableBase Get(BaseEntity entity)
            {
                if (Backbone.Plugin.RaidEntities.ContainsKey(entity))
                {
                    return Backbone.Plugin.RaidEntities[entity];
                }

                return null;
            }

            public static RaidableBase Get(List<BaseEntity> entities)
            {
                foreach (var raid in Backbone.Plugin.Raids.Values)
                {
                    foreach (var e in entities)
                    {
                        if (InRange(raid.PastedLocation, e.transform.position, Radius))
                        {
                            return raid;
                        }
                    }
                }

                return null;
            }

            public static bool IsTooClose(Vector3 target, float radius)
            {
                foreach (var raid in Backbone.Plugin.Raids.Values)
                {
                    if (InRange(raid.Location, target, radius))
                    {
                        return true;
                    }
                }

                foreach (var position in Locations)
                {
                    if (InRange(position, target, radius))
                    {
                        return true;
                    }
                }

                return false;
            }

            private List<ulong> GetItemSkins(ItemDefinition def)
            {
                if (def == null)
                {
                    return new List<ulong> { 0 };
                }

                List<ulong> skins;
                if (!Backbone.Plugin.Skins.TryGetValue(def.shortname, out skins))
                {
                    Backbone.Plugin.Skins[def.shortname] = skins = ExtractItemSkins(def, skins);
                }

                return skins;
            }

            private List<ulong> ExtractItemSkins(ItemDefinition def, List<ulong> skins)
            {
                skins = new List<ulong>();

                foreach (var skin in def.skins)
                {
                    skins.Add(Convert.ToUInt64(skin.id));
                }

                if (Backbone.Plugin.WorkshopSkins.ContainsKey(def.shortname))
                {
                    skins.AddRange(Backbone.Plugin.WorkshopSkins[def.shortname]);
                    Backbone.Plugin.WorkshopSkins.Remove(def.shortname);
                }

                return skins;
            }

            private void AuthorizePlayer(NPCPlayerApex npc)
            {
                turrets.RemoveAll(x => !x.IsValid() || x.IsDestroyed);

                foreach (var turret in turrets)
                {
                    turret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                    {
                        userid = npc.userID,
                        username = npc.displayName
                    });

                    turret.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }

                if (priv == null || priv.IsDestroyed)
                {
                    return;
                }

                priv.authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                {
                    userid = npc.userID,
                    username = npc.displayName
                });

                priv.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }

            public bool IsInSameClan(string playerId, string targetId)
            {
                if (playerId == targetId)
                {
                    return false;
                }

                if (Backbone.Plugin.Clans == null || !Backbone.Plugin.Clans.IsLoaded)
                {
                    return false;
                }

                var clan = new List<string>();

                foreach (var x in _clans.Values)
                {
                    if (x.Contains(playerId))
                    {
                        if (x.Contains(targetId))
                        {
                            return true;
                        }

                        clan = x;
                        break;
                    }
                }

                string playerClan = Backbone.Plugin.Clans?.Call("GetClanOf", playerId) as string;

                if (string.IsNullOrEmpty(playerClan))
                {
                    return false;
                }

                string targetClan = Backbone.Plugin.Clans?.Call("GetClanOf", targetId) as string;

                if (string.IsNullOrEmpty(targetClan))
                {
                    return false;
                }

                if (playerClan == targetClan)
                {
                    if (!_clans.ContainsKey(playerClan))
                    {
                        _clans[playerClan] = clan;
                    }

                    clan.Add(playerId);
                    clan.Add(targetId);
                    return true;
                }

                return false;
            }

            public void UpdateClans(string clan)
            {
                _clans.Remove(clan);
            }

            public void UpdateFriends(string playerId, string targetId, bool added)
            {
                List<string> playerList;
                if (_friends.TryGetValue(playerId, out playerList))
                {
                    if (added)
                    {
                        playerList.Add(targetId);
                    }
                    else playerList.Remove(targetId);
                }
            }

            public bool IsFriends(string playerId, string targetId)
            {
                if (playerId == targetId)
                {
                    return false;
                }

                if (Backbone.Plugin.Friends == null || !Backbone.Plugin.Friends.IsLoaded)
                {
                    return false;
                }

                List<string> targetList;
                if (!_friends.TryGetValue(targetId, out targetList))
                {
                    _friends[targetId] = targetList = new List<string>();
                }

                if (targetList.Contains(playerId))
                {
                    return true;
                }

                var success = Backbone.Plugin.Friends?.Call("AreFriends", playerId, targetId);

                if (success is bool && (bool)success)
                {
                    targetList.Add(playerId);
                    return true;
                }

                return false;
            }

            public bool IsOnSameTeam(ulong playerId, ulong targetId)
            {
                if (playerId == targetId)
                {
                    return false;
                }

                RelationshipManager.PlayerTeam team1;
                if (!RelationshipManager.Instance.playerToTeam.TryGetValue(playerId, out team1))
                {
                    return false;
                }

                RelationshipManager.PlayerTeam team2;
                if (!RelationshipManager.Instance.playerToTeam.TryGetValue(targetId, out team2))
                {
                    return false;
                }

                return team1.teamID == team2.teamID;
            }

            public bool IsAlly(ulong playerId, ulong targetId)
            {
                return playerId == targetId || IsOnSameTeam(playerId, targetId) || IsInSameClan(playerId.ToString(), targetId.ToString()) || IsFriends(playerId.ToString(), targetId.ToString());
            }

            public bool IsAlly(BasePlayer player)
            {
                if (!player.IsValid())
                {
                    return false;
                }

                if (!ownerId.IsSteamId() || CanBypass(player) || player.userID == ownerId || friends.Contains(player))
                {
                    return true;
                }

                if (IsOnSameTeam(player.userID, ownerId) || IsInSameClan(player.UserIDString, ownerId.ToString()) || IsFriends(player.UserIDString, ownerId.ToString()))
                {
                    friends.Add(player);
                    return true;
                }

                return false;
            }

            public static void StopUsingWand(BasePlayer player)
            {
                if (!_config.Settings.NoWizardry || Backbone.Plugin.Wizardry == null || !Backbone.Plugin.Wizardry.IsLoaded)
                {
                    return;
                }

                if (player.svActiveItemID == 0)
                {
                    return;
                }

                Item item = player.GetActiveItem();

                if (item?.info.shortname != "knife.bone")
                {
                    return;
                }

                if (!item.MoveToContainer(player.inventory.containerMain))
                {
                    item.DropAndTossUpwards(player.GetDropPosition() + player.transform.forward, 2f);
                    Backbone.Message(player, "TooPowerfulDrop");
                }
                else Backbone.Message(player, "TooPowerful");
            }

            private int targetLayer { get; set; } = ~(Layers.Mask.Invisible | Layers.Mask.Trigger | Layers.Mask.Prevent_Movement | Layers.Mask.Prevent_Building); // credits ZoneManager

            public Vector3 GetEjectLocation(Vector3 a, float distance)
            {
                var position = ((a.XZ3D() - Location.XZ3D()).normalized * (Options.ProtectionRadius + distance)) + Location; // credits ZoneManager
                float y = TerrainMeta.HighestPoint.y + 250f;

                RaycastHit hit;
                if (Physics.Raycast(position + new Vector3(0f, y, 0f), Vector3.down, out hit, Mathf.Infinity, targetLayer, QueryTriggerInteraction.Ignore))
                {
                    position.y = hit.point.y + 0.75f;
                }
                else position.y = Mathf.Max(TerrainMeta.HeightMap.GetHeight(position), TerrainMeta.WaterMap.GetHeight(position)) + 0.75f;

                return position;
            }

            public CorpseData AddCorpse(PlayerCorpse corpse, BasePlayer player)
            {
                CorpseData data;
                if (!corpses.TryGetValue(corpse.net.ID, out data))
                {
                    corpses[corpse.net.ID] = data = new CorpseData
                    {
                        corpse = corpse,
                        player = player,
                        displayName = corpse.playerName,
                        userID = corpse.playerSteamID
                    };
                }

                return data;
            }

            public bool EjectCorpse(uint key, CorpseData data)
            {
                if (!IsValid(data.corpse))
                {
                    return true;
                }

                if (!ownerId.IsSteamId() || Any(data.userID) || IsAlly(data.player))
                {
                    return false;
                }

                var position = GetEjectLocation(data.corpse.transform.position, 5f);
                float w = TerrainMeta.WaterMap.GetHeight(position);

                if (position.y < w)
                {
                    position.y = w;
                }

                var container = ItemContainer.Drop(StringPool.Get(1519640547), position, Quaternion.identity, data.corpse.containers);

                if (container == null)
                {
                    return false;
                }

                container.playerName = data.displayName;
                container.playerSteamID = data.userID;

                if (!data.corpse.IsDestroyed)
                {
                    data.corpse.Kill();
                }

                var player = data.player;

                if (player.IsValid() && player.IsConnected)
                {
                    if (_config.Settings.Management.DrawTime <= 0)
                    {
                        Backbone.Message(player, "YourCorpse");
                        return true;
                    }

                    bool isAdmin = player.IsAdmin;
                    string message = Backbone.GetMessage("YourCorpse", player.UserIDString);

                    try
                    {
                        if (!isAdmin)
                        {
                            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                            player.SendNetworkUpdateImmediate();
                        }

                        player.SendConsoleCommand("ddraw.text", _config.Settings.Management.DrawTime, Color.red, container.transform.position, message);
                    }
                    catch (Exception ex)
                    {
                        Puts(ex.StackTrace);
                        Puts(ex.Message);
                    }
                    finally
                    {
                        if (!isAdmin)
                        {
                            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                            player.SendNetworkUpdateImmediate();
                        }
                    }
                }

                return true;
            }

            private void EjectSleepers()
            {
                if (!_config.Settings.Management.EjectSleepers || Type == RaidableType.None)
                {
                    return;
                }

                var players = Pool.GetList<BasePlayer>();

                Vis.Entities(Location, Options.ProtectionRadius, players, Layers.Mask.Player_Server, QueryTriggerInteraction.Ignore);

                foreach (var player in players)
                {
                    if (player.IsSleeping() && !player.CanBuild())
                    {
                        RemovePlayer(player);
                    }
                }

                Pool.FreeList(ref players);
            }

            public bool RemovePlayer(BasePlayer player)
            {
                if (player.IsNpc || Type == RaidableType.None && !player.IsSleeping())
                {
                    return false;
                }

                if (player.isMounted)
                {
                    return RemoveMountable(player.GetMounted());

                }

                var position = GetEjectLocation(player.transform.position, 10f);

                if (player.IsFlying)
                {
                    position.y = player.transform.position.y;
                }

                player.EnsureDismounted();
                player.Teleport(position);
                player.SendNetworkUpdateImmediate();

                return true;
            }

            private bool JustDied(BasePlayer target)
            {
                return target?.previousLifeStory != null && target.lifeStory != null && (uint)Epoch.Current - target.lifeStory.timeBorn <= 6;
            }

            private bool CanEject(List<BasePlayer> players)
            {
                foreach (var player in players)
                {
                    if (CanEject(player))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool CanEject(BasePlayer target)
            {
                if (target == null || target == owner)
                {
                    return false;
                }

                if (IsBanned(target) || HasLockout(target) || IsHogging(target))
                {
                    return true;
                }
                else if (CanEject() && !IsAlly(target))
                {
                    return true;
                }
                else if (_config.Settings.Management.EjectSleepers && target.IsSleeping() && !target.IsConnected && Type != RaidableType.None)
                {
                    return true;
                }

                return false;
            }

            public bool CanEject()
            {
                if (IsPayLocked && AllowPVP && Options.EjectPurchasedPVP)
                {
                    return true;
                }

                if (IsPayLocked && !AllowPVP && Options.EjectPurchasedPVE)
                {
                    return true;
                }

                if (AllowPVP && Options.EjectLockedPVP && ownerId.IsSteamId())
                {
                    return true;
                }

                if (!AllowPVP && Options.EjectLockedPVE && ownerId.IsSteamId())
                {
                    return true;
                }

                return false;
            }

            private bool TryRemoveMountable(BaseMountable m, List<BasePlayer> players)
            {
                if (CanEject(players))
                {
                    return RemoveMountable(m);
                }

                if (_config.Settings.Management.Mounts.Boats && m is BaseBoat)
                {
                    return RemoveMountable(m);
                }
                else if (_config.Settings.Management.Mounts.BasicCars && m is BasicCar)
                {
                    return RemoveMountable(m);
                }
                else if (_config.Settings.Management.Mounts.ModularCars && m is ModularCar)
                {
                    return RemoveMountable(m);
                }
                else if (_config.Settings.Management.Mounts.CH47 && m is CH47Helicopter)
                {
                    return RemoveMountable(m);
                }
                else if (_config.Settings.Management.Mounts.Horses && m is RidableHorse)
                {
                    return RemoveMountable(m);
                }
                else if (_config.Settings.Management.Mounts.Scrap && m is ScrapTransportHelicopter)
                {
                    return RemoveMountable(m);
                }
                else if (_config.Settings.Management.Mounts.MiniCopters && m is MiniCopter && !(m is ScrapTransportHelicopter))
                {
                    return RemoveMountable(m);
                }
                else if (_config.Settings.Management.Mounts.Pianos && m is StaticInstrument)
                {
                    return RemoveMountable(m);
                }

                return false;
            }

            private bool RemoveMountable(BaseMountable m)
            {
                bool flag = m is MiniCopter || m is CH47Helicopter;

                if (InRange(m.transform.position, Location, Radius, false))
                {
                    EjectMountable(m, flag);
                    return true;
                }

                var e = m.transform.eulerAngles; // credits k1lly0u

                if (m is RidableHorse)
                {
                    m.transform.rotation = m.mountAnchor.transform.rotation = Quaternion.Euler(e.x, e.y - 180f, e.z);
                }
                else m.transform.rotation = Quaternion.Euler(e.x, e.y - 180f, e.z);

                var rigidBody = m.GetComponent<Rigidbody>();

                if (rigidBody)
                {
                    rigidBody.velocity *= -1f;
                }

                if (flag || m is BaseBoat || m is StaticInstrument)
                {
                    EjectMountable(m, flag);
                }

                return true;
            }

            private void EjectMountable(BaseMountable m, bool flag)
            {
                var position = ((m.transform.position.XZ3D() - Location.XZ3D()).normalized * (Options.ProtectionRadius + 10f)) + Location; // credits k1lly0u
                float y = TerrainMeta.HighestPoint.y + 250f;

                if (!flag)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(position + new Vector3(0f, y, 0f), Vector3.down, out hit, position.y + y + 1f, targetLayer, QueryTriggerInteraction.Ignore))
                    {
                        position.y = hit.point.y;
                    }
                    else position.y = GetSpawnHeight(position);
                }

                if (m.transform.position.y > position.y)
                {
                    position.y = m.transform.position.y;
                }

                if (flag)
                {
                    m.transform.position = position;
                }
                else m.transform.position = m.mountAnchor.transform.position = position;

                m.TransformChanged();
            }

            public bool CanSetupEntity(BaseEntity e)
            {
                BaseEntity.saveList.Remove(e);

                if (e == null || e.IsDestroyed)
                {
                    if (e != null)
                    {
                        e.enableSaving = false;
                    }

                    Entities.Remove(e);
                    return false;
                }

                if (e.net == null)
                {
                    e.net = Net.sv.CreateNetworkable();
                }

                e.enableSaving = false;
                return true;
            }

            public void TryRespawnNpc()
            {
                if ((!IsOpened && !Options.Levels.Level2) || IsInvoking(RespawnNpcNow))
                {
                    return;
                }

                Invoke(RespawnNpcNow, Options.RespawnRate);
            }

            public void RespawnNpcNow()
            {
                if (npcs.Count >= npcMaxAmount)
                {
                    return;
                }

                var npc = SpawnNPC(Options.NPC.SpawnScientistsOnly ? false : Options.NPC.SpawnBoth ? UnityEngine.Random.value > 0.5f : Options.NPC.SpawnMurderers);

                if (npc == null || npcs.Count >= npcMaxAmount)
                {
                    return;
                }

                TryRespawnNpc();
            }

            public void SpawnNpcs()
            {
                if (!Options.NPC.Enabled || (Options.NPC.UseExpansionNpcs && _config.Settings.ExpansionMode && Backbone.Plugin.DangerousTreasures != null && Backbone.Plugin.DangerousTreasures.IsLoaded))
                {
                    return;
                }

                if (npcMaxAmount == 0)
                {
                    npcMaxAmount = Options.NPC.SpawnRandomAmount && Options.NPC.SpawnAmount > 1 ? UnityEngine.Random.Range(Options.NPC.SpawnMinAmount, Options.NPC.SpawnAmount) : Options.NPC.SpawnAmount;
                }

                for (int i = 0; i < npcMaxAmount; i++)
                {
                    if (npcs.Count >= npcMaxAmount)
                    {
                        break;
                    }

                    SpawnNPC(Options.NPC.SpawnScientistsOnly ? false : Options.NPC.SpawnBoth ? UnityEngine.Random.value > 0.5f : Options.NPC.SpawnMurderers);
                }
            }

            private Vector3 FindPointOnNavmesh(Vector3 target, float radius)
            {
                int tries = 0;
                NavMeshHit navHit;

                while (++tries < 100)
                {
                    if (NavMesh.SamplePosition(target, out navHit, radius, 1))
                    {
                        if (NearFoundation(navHit.position))
                        {
                            continue;
                        }

                        float y = TerrainMeta.HeightMap.GetHeight(navHit.position);

                        if (IsInOrOnRock(navHit.position, "rock_") || navHit.position.y < y)
                        {
                            continue;
                        }

                        if (TerrainMeta.WaterMap.GetHeight(navHit.position) - y > 1f)
                        {
                            continue;
                        }

                        if ((navHit.position - Location).magnitude > Mathf.Max(radius * 2f, Options.ProtectionRadius) - 2.5f)
                        {
                            continue;
                        }

                        return navHit.position;
                    }
                }

                return Vector3.zero;
            }

            private bool IsRockTooLarge(Bounds bounds, float extents = 1.5f)
            {
                return bounds.extents.Max() > extents;
            }

            private bool IsInOrOnRock(Vector3 position, string meshName, float radius = 2f)
            {
                bool flag = false;
                int hits = Physics.OverlapSphereNonAlloc(position, radius, Vis.colBuffer, Layers.Mask.World, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits; i++)
                {
                    if (Vis.colBuffer[i].name.StartsWith(meshName) && IsRockTooLarge(Vis.colBuffer[i].bounds))
                    {
                        flag = true;
                    }

                    Vis.colBuffer[i] = null;
                }
                if (!flag)
                {
                    float y = TerrainMeta.HighestPoint.y + 250f;
                    RaycastHit hit;
                    if (Physics.Raycast(position, Vector3.up, out hit, y, Layers.Mask.World, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.name.StartsWith(meshName) && IsRockTooLarge(hit.collider.bounds)) flag = true;
                    }
                    if (!flag && Physics.Raycast(position, Vector3.down, out hit, y, Layers.Mask.World, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.name.StartsWith(meshName) && IsRockTooLarge(hit.collider.bounds)) flag = true;
                    }
                    if (!flag && Physics.Raycast(position + new Vector3(0f, y, 0f), Vector3.down, out hit, y + 1f, Layers.Mask.World, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.name.StartsWith(meshName) && IsRockTooLarge(hit.collider.bounds)) flag = true;
                    }
                }
                return flag;
            }

            private static NPCPlayerApex InstantiateEntity(Vector3 position, bool murd)
            {
                var prefabName = murd ? Backbone.Path.Murderer : Backbone.Path.Scientist;
                var prefab = GameManager.server.FindPrefab(prefabName);
                var go = Facepunch.Instantiate.GameObject(prefab, position, default(Quaternion));

                go.name = prefabName;
                SceneManager.MoveGameObjectToScene(go, Rust.Server.EntityScene);

                if (go.GetComponent<Spawnable>())
                {
                    Destroy(go.GetComponent<Spawnable>());
                }

                if (!go.activeSelf)
                {
                    go.SetActive(true);
                }

                return go.GetComponent<NPCPlayerApex>();
            }

            private List<Vector3> RandomWanderPositions
            {
                get
                {
                    var list = new List<Vector3>();
                    float maxRoamRange = Options.ArenaWalls.Enabled ? 20f : Mathf.Max(Radius * 2f, Options.ProtectionRadius);

                    for (int i = 0; i < 10; i++)
                    {
                        var vector = FindPointOnNavmesh(GetRandomPoint(maxRoamRange), 15f);

                        if (vector != Vector3.zero)
                        {
                            list.Add(vector);
                        }
                    }

                    return list;
                }
            }

            private Vector3 GetRandomPoint(float radius)
            {
                var vector = Location + UnityEngine.Random.onUnitSphere * radius;

                vector.y = TerrainMeta.HeightMap.GetHeight(vector);

                return vector;
            }

            private NPCPlayerApex SpawnNPC(bool murd)
            {
                var list = RandomWanderPositions;

                if (list.Count == 0)
                    return null;

                var npc = InstantiateEntity(GetRandomPoint(Radius * 0.85f), murd); //var npc = InstantiateEntity(GetRandomPoint(Mathf.Max(Options.ArenaWalls.Radius, Options.ProtectionRadius) * 0.85f), murd);

                if (npc == null)
                    return null;

                npc.Spawn();

                npcs.Add(npc);

                npc.IsInvinsible = false;
                npc.startHealth = murd ? Options.NPC.MurdererHealth : Options.NPC.ScientistHealth;
                npc.InitializeHealth(npc.startHealth, npc.startHealth);
                npc.CommunicationRadius = 0;
                npc.RadioEffect.guid = null;
                npc.displayName = Options.NPC.RandomNames.Count > 0 ? Options.NPC.RandomNames.GetRandom() : RandomUsernames.Get(npc.userID);
                npc.Stats.AggressionRange = Options.NPC.AggressionRange;
                npc.Stats.DeaggroRange = Options.NPC.AggressionRange * 1.125f;
                npc.NeverMove = true;

                npc.Invoke(() =>
                {
                    if (npc.IsDestroyed)
                    {
                        return;
                    }

                    EquipNpc(npc, murd);
                }, 1f);

                npc.Invoke(() =>
                {
                    if (npc.IsDestroyed)
                    {
                        return;
                    }

                    Item projectileItem = null;

                    foreach (var item in npc.inventory.containerBelt.itemList)
                    {
                        if (item.GetHeldEntity() is BaseProjectile)
                        {
                            projectileItem = item;
                            break;
                        }
                    }

                    if (projectileItem != null)
                    {
                        npc.UpdateActiveItem(projectileItem.uid);
                    }
                    else npc.EquipWeapon();
                }, 2f);

                if (Options.NPC.DespawnInventory)
                {
                    if (murd)
                    {
                        (npc as NPCMurderer).LootSpawnSlots = new LootContainer.LootSpawnSlot[0];
                    }
                    else (npc as Scientist).LootSpawnSlots = new LootContainer.LootSpawnSlot[0];
                }

                if (!murd)
                {
                    (npc as Scientist).LootPanelName = npc.displayName;
                }

                AuthorizePlayer(npc);
                npc.Invoke(() => UpdateDestination(npc, list), 0.25f);
                Backbone.Plugin.Npcs[npc.userID] = this;

                return npc;
            }

            private void SetupNpcKits()
            {
                var murdererKits = new List<string>();
                var scientistKits = new List<string>();

                foreach (string kit in Options.NPC.MurdererKits)
                {
                    if (IsKit(kit))
                    {
                        murdererKits.Add(kit);
                    }
                }

                foreach (string kit in Options.NPC.ScientistKits)
                {
                    if (IsKit(kit))
                    {
                        scientistKits.Add(kit);
                    }
                }

                npcKits = new Dictionary<string, List<string>>
                {
                    { "murderer", murdererKits },
                    { "scientist", scientistKits }
                };
            }

            private bool IsKit(string kit)
            {
                var success = Backbone.Plugin.Kits?.Call("isKit", kit);

                if (success == null || !(success is bool))
                {
                    return false;
                }

                return (bool)success;
            }

            private void EquipNpc(NPCPlayerApex npc, bool murd)
            {
                List<string> kits;
                if (npcKits.TryGetValue(murd ? "murderer" : "scientist", out kits) && kits.Count > 0)
                {
                    npc.inventory.Strip();

                    object success = Backbone.Plugin.Kits?.Call("GiveKit", npc, kits.GetRandom());

                    if (success is bool && (bool)success)
                    {
                        goto done;
                    }
                }

                var items = murd ? Options.NPC.MurdererItems : Options.NPC.ScientistItems;

                if (items.Count == 0)
                {
                    goto done;
                }

                npc.inventory.Strip();
                List<ulong> skins;
                BaseProjectile weapon;

                foreach (string shortname in items)
                {
                    Item item = ItemManager.CreateByName(shortname, 1, 0);

                    if (item == null)
                    {
                        Backbone.Plugin.PrintError("Invalid shortname in config: {0}", shortname);
                        continue;
                    }

                    skins = GetItemSkins(item.info);

                    if (skins.Count > 0 && item.info.stackable <= 1)
                    {
                        ulong skin = skins.GetRandom();
                        item.skin = skin;
                    }

                    if (item.skin != 0 && item.GetHeldEntity())
                    {
                        item.GetHeldEntity().skinID = item.skin;
                    }

                    weapon = item.GetHeldEntity() as BaseProjectile;

                    if (weapon != null)
                    {
                        weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
                        weapon.SendNetworkUpdateImmediate();
                    }

                    item.MarkDirty();

                    if (!item.MoveToContainer(npc.inventory.containerWear, -1, false) && !item.MoveToContainer(npc.inventory.containerBelt, -1, false) && !item.MoveToContainer(npc.inventory.containerMain, -1, true))
                    {
                        item.Remove();
                    }
                }

                done:
                ToggleNpcMinerHat(npc, !IsDayTime());
            }

            private void UpdateDestination(NPCPlayerApex npc, List<Vector3> list)
            {
                npc.gameObject.AddComponent<FinalDestination>().Set(npc, list, Options.NPC);
            }

            public static void UpdateAllMarkers()
            {
                foreach (var raid in Backbone.Plugin.Raids.Values)
                {
                    raid.UpdateMarker();
                }
            }

            public void UpdateMarker()
            {
                if (IsLoading)
                {
                    Invoke(UpdateMarker, 1f);
                    return;
                }

                if (genericMarker != null && !genericMarker.IsDestroyed)
                {
                    genericMarker.SendUpdate();
                }

                if (vendingMarker != null && !vendingMarker.IsDestroyed)
                {
                    vendingMarker.transform.position = Location;
                    float seconds = despawnTime - Time.realtimeSinceStartup;
                    string despawnText = _config.Settings.Management.DespawnMinutesInactive > 0 && seconds > 0 ? Math.Floor(TimeSpan.FromSeconds(seconds).TotalMinutes).ToString() : null;
                    string flag = Backbone.GetMessage(AllowPVP ? "PVPFlag" : "PVEFlag");
                    string markerShopName = markerName == _config.Settings.Markers.MarkerName ? string.Format("{0}{1} {2}", flag, Mode(), markerName) : string.Format("{0} {1}", flag, markerName).TrimStart();
                    vendingMarker.markerShopName = string.IsNullOrEmpty(despawnText) ? markerShopName : string.Format("{0} [{1}m]", markerShopName, despawnText);
                    vendingMarker.SendNetworkUpdate();
                }

                if (markerCreated || !IsMarkerAllowed())
                {
                    return;
                }

                if (_config.Settings.Markers.UseExplosionMarker)
                {
                    explosionMarker = GameManager.server.CreateEntity(Backbone.Path.ExplosionMarker, Location) as MapMarkerExplosion;

                    if (explosionMarker != null)
                    {

                        explosionMarker.Spawn();
                        explosionMarker.SendMessage("SetDuration", 60, SendMessageOptions.DontRequireReceiver);

                    }
                }
                else if (_config.Settings.Markers.UseVendingMarker)
                {
                    vendingMarker = GameManager.server.CreateEntity(Backbone.Path.VendingMarker, Location) as VendingMachineMapMarker;

                    if (vendingMarker != null)
                    {
                        string flag = Backbone.GetMessage(AllowPVP ? "PVPFlag" : "PVEFlag");
                        string markerShopName = markerName == _config.Settings.Markers.MarkerName ? string.Format("{0}{1} {2}", flag, Mode(), markerName) : string.Format("{0}{1}", flag, markerName);

                        vendingMarker.enabled = false;
                        vendingMarker.markerShopName = markerShopName;
                        vendingMarker.Spawn();
                    }
                }

                markerCreated = true;
            }

            private void CreateGenericMarker()
            {
                if (_config.Settings.Markers.UseExplosionMarker || _config.Settings.Markers.UseVendingMarker)
                {
                    if (!IsMarkerAllowed())
                    {
                        return;
                    }

                    genericMarker = GameManager.server.CreateEntity(Backbone.Path.RadiusMarker, Location) as MapMarkerGenericRadius;

                    if (genericMarker != null)
                    {
                        genericMarker.alpha = 0.75f;
                        genericMarker.color1 = GetMarkerColor1();
                        genericMarker.color2 = GetMarkerColor2();
                        genericMarker.radius = Mathf.Min(2.5f, _config.Settings.Markers.Radius);
                        genericMarker.Spawn();
                        genericMarker.SendUpdate();
                    }
                }
            }

            private Color GetMarkerColor1()
            {
                if (Type == RaidableType.None)
                {
                    return Color.clear;
                }

                Color color;

                switch (Options.Mode)
                {
                    case RaidableMode.Easy:
                        {
                            if (ColorUtility.TryParseHtmlString(_config.Settings.Management.Colors1.Easy, out color))
                            {
                                return color;
                            }
                        }

                        return Color.green;
                    case RaidableMode.Medium:
                        {
                            if (ColorUtility.TryParseHtmlString(_config.Settings.Management.Colors1.Medium, out color))
                            {
                                return color;
                            }
                        }

                        return Color.yellow;
                    case RaidableMode.Hard:
                        {
                            if (ColorUtility.TryParseHtmlString(_config.Settings.Management.Colors1.Hard, out color))
                            {
                                return color;
                            }
                        }

                        return Color.red;
                    case RaidableMode.Expert:
                        {
                            if (ColorUtility.TryParseHtmlString(_config.Settings.Management.Colors1.Expert, out color))
                            {
                                return color;
                            }
                        }

                        return Color.blue;
                    case RaidableMode.Nightmare:
                    default:
                        {
                            if (ColorUtility.TryParseHtmlString(_config.Settings.Management.Colors1.Nightmare, out color))
                            {
                                return color;
                            }
                        }

                        return Color.black;
                }
            }

            private Color GetMarkerColor2()
            {
                if (Type == RaidableType.None)
                {
                    return NoneColor;
                }

                Color color;

                switch (Options.Mode)
                {
                    case RaidableMode.Easy:
                        {
                            if (ColorUtility.TryParseHtmlString(_config.Settings.Management.Colors2.Easy, out color))
                            {
                                return color;
                            }
                        }

                        return Color.green;
                    case RaidableMode.Medium:
                        {
                            if (ColorUtility.TryParseHtmlString(_config.Settings.Management.Colors2.Medium, out color))
                            {
                                return color;
                            }
                        }

                        return Color.yellow;
                    case RaidableMode.Hard:
                        {
                            if (ColorUtility.TryParseHtmlString(_config.Settings.Management.Colors2.Hard, out color))
                            {
                                return color;
                            }
                        }

                        return Color.red;
                    case RaidableMode.Expert:
                        {
                            if (ColorUtility.TryParseHtmlString(_config.Settings.Management.Colors2.Expert, out color))
                            {
                                return color;
                            }
                        }

                        return Color.blue;
                    case RaidableMode.Nightmare:
                    default:
                        {
                            if (ColorUtility.TryParseHtmlString(_config.Settings.Management.Colors2.Nightmare, out color))
                            {
                                return color;
                            }
                        }

                        return Color.black;
                }
            }

            private bool IsMarkerAllowed()
            {
                switch (Type)
                {
                    case RaidableType.Grid:
                    case RaidableType.Manual:
                    case RaidableType.None:
                        {
                            return _config.Settings.Markers.Manual;
                        }
                    case RaidableType.Maintained:
                        {
                            return _config.Settings.Markers.Maintained;
                        }
                    case RaidableType.Purchased:
                        {
                            return _config.Settings.Markers.Buyables;
                        }
                    case RaidableType.Scheduled:
                        {
                            return _config.Settings.Markers.Scheduled;
                        }
                }

                return true;
            }

            private void KillNpc()
            {
                var list = new List<BaseEntity>(npcs);

                foreach (var npc in list)
                {
                    if (npc != null && !npc.IsDestroyed)
                    {
                        npc.Kill();
                    }
                }

                npcs.Clear();
                list.Clear();
            }

            private void RemoveSpheres()
            {
                if (spheres.Count > 0)
                {
                    foreach (var sphere in spheres)
                    {
                        if (sphere != null && !sphere.IsDestroyed)
                        {
                            sphere.Kill();
                        }
                    }

                    spheres.Clear();
                }
            }

            public void RemoveMapMarkers()
            {
                Interface.CallHook("RemoveTemporaryLustyMarker", uid);
                Interface.CallHook("RemoveMapPrivatePluginMarker", uid);

                if (explosionMarker != null && !explosionMarker.IsDestroyed)
                {
                    explosionMarker.CancelInvoke(explosionMarker.DelayedDestroy);
                    explosionMarker.Kill();
                }

                if (genericMarker != null && !genericMarker.IsDestroyed)
                {
                    genericMarker.Kill();
                }

                if (vendingMarker != null && !vendingMarker.IsDestroyed)
                {
                    vendingMarker.Kill();
                }
            }
        }

        #region Hooks

        private void UnsubscribeHooks()
        {
            if (IsUnloading)
            {
                return;
            }

            Unsubscribe(nameof(OnRestoreUponDeath));
            Unsubscribe(nameof(OnNpcKits));
            Unsubscribe(nameof(CanTeleport));
            Unsubscribe(nameof(canTeleport));
            Unsubscribe(nameof(CanEntityBeTargeted));
            Unsubscribe(nameof(CanEntityTrapTrigger));
            Unsubscribe(nameof(CanEntityTakeDamage));
            Unsubscribe(nameof(OnFriendAdded));
            Unsubscribe(nameof(OnFriendRemoved));
            Unsubscribe(nameof(OnClanUpdate));
            Unsubscribe(nameof(OnClanDestroy));
            Unsubscribe(nameof(CanDropBackpack));
            Unsubscribe(nameof(CanOpenBackpack));

            Unsubscribe(nameof(CanBeTargeted));
            Unsubscribe(nameof(OnEntityMounted));
            Unsubscribe(nameof(OnEntityBuilt));
            Unsubscribe(nameof(OnEntityGroundMissing));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnLootEntity));
            Unsubscribe(nameof(OnLootEntityEnd));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(CanPickupEntity));
            Unsubscribe(nameof(OnPlayerDeath));
            Unsubscribe(nameof(OnPlayerDropActiveItem));
            Unsubscribe(nameof(OnNpcTarget));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnCupboardAuthorize));
            Unsubscribe(nameof(OnActiveItemChanged));
            Unsubscribe(nameof(OnLoseCondition));
            Unsubscribe(nameof(OnFireBallSpread));
            Unsubscribe(nameof(CanBuild));
        }

        private void OnMapMarkerAdded(BasePlayer player, ProtoBuf.MapNote note)
        {
            if (player.IsValid() && player.IsConnected && note != null && permission.UserHasPermission(player.UserIDString, mapPermission))
            {
                player.Teleport(new Vector3(note.worldPosition.x, GetSpawnHeight(note.worldPosition), note.worldPosition.z));
            }
        }

        private void OnNewSave(string filename) => wiped = true;

        private void Init()
        {
            permission.CreateGroup(rankLadderGroup, rankLadderGroup, 0);
            permission.GrantGroupPermission(rankLadderGroup, rankLadderPermission, this);
            permission.RegisterPermission(adminPermission, this);
            permission.RegisterPermission(rankLadderPermission, this);
            permission.RegisterPermission(drawPermission, this);
            permission.RegisterPermission(mapPermission, this);
            permission.RegisterPermission(canBypassPermission, this);
            permission.RegisterPermission(bypassBlockPermission, this);
            permission.RegisterPermission(banPermission, this);
            permission.RegisterPermission(vipPermission, this);
            lastSpawnRequestTime = Time.realtimeSinceStartup;
            Backbone = new SingletonBackbone(this);
            Unsubscribe(nameof(OnMapMarkerAdded));
            Unsubscribe(nameof(OnPlayerSleepEnded));
            UnsubscribeHooks();
            maintainedEnabled = _config.Settings.Maintained.Enabled;
            scheduledEnabled = _config.Settings.Schedule.Enabled;
            buyableEnabled = _config.Settings.Buyable.Max > 0;
        }

        private void OnServerInitialized(bool isStartup)
        {
            if (!configLoaded)
            {
                return;
            }

            timer.Repeat(30f, 0, () => RaidableBase.UpdateAllMarkers());

            LoadData();
            Reinitialize();
            BlockZoneManagerZones();
            SetupMonuments();
            LoadSpawns();
            SetupGrid();
            RegisterCommands();
            RemoveAllThirdPartyMarkers();
            CheckForWipe();
            UpdateUI();
            CreateDefaultFiles();
            LoadTables();
            LoadProfiles();
        }

        private void Unload()
        {
            IsUnloading = true;

            if (!configLoaded)
            {
                UnsetStatics();
                return;
            }

            SaveData();
            RaidableBase.Unload();
            StopScheduleCoroutine();
            StopMaintainCoroutine();
            StopGridCoroutine();
            StopDespawnCoroutine();
            DestroyComponents();
            RemoveAllThirdPartyMarkers();

            if (Raids.Count > 0 || Bases.Count > 0)
            {
                DespawnAllBasesNow();
                return;
            }

            UnsetStatics();
        }

        private static void UnsetStatics()
        {
            Buildings = new BuildingTables();
            UI.DestroyAllLockoutUI();
            UI.DestroyAllBuyableUI();
            UI.Players.Clear();
            UI.Lockouts.Clear();
            UI.Buyables.Clear();
            Backbone.Destroy();
            Backbone = null;
            _config = null;
            Locations.Clear();
        }

        private void RegisterCommands()
        {
            AddCovalenceCommand(_config.Settings.BuyCommand, nameof(CommandBuyRaid));
            AddCovalenceCommand(_config.Settings.EventCommand, nameof(CommandRaidBase));
            AddCovalenceCommand(_config.Settings.HunterCommand, nameof(CommandRaidHunter));
            AddCovalenceCommand(_config.Settings.ConsoleCommand, nameof(CommandRaidBase));
            AddCovalenceCommand("rb.reloadconfig", nameof(CommandReloadConfig));
            AddCovalenceCommand("rb.config", nameof(CommandConfig), "raidablebases.config");
            AddCovalenceCommand("rb.populate", nameof(CommandPopulate), "raidablebases.config");
            AddCovalenceCommand("rb.toggle", nameof(CommandToggle), "raidablebases.config");
        }

        private void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
            }

            if (storedData?.Players == null)
            {
                storedData = new StoredData();
                SaveData();
            }
        }

        private void CheckForWipe()
        {
            if (!wiped && storedData.Players.Count >= _config.RankedLadder.Amount && BuildingManager.server.buildingDictionary.Count == 0)
            {
                foreach (var pi in storedData.Players.Values)
                {
                    if (pi.Raids > 0)
                    {
                        wiped = true;
                        break;
                    }
                }
            }

            if (wiped)
            {
                var raids = new List<int>();
                var dict = new Dictionary<string, PlayerInfo>(storedData.Players);

                if (storedData.Players.Count > 0)
                {
                    if (AssignTreasureHunters())
                    {
                        foreach (var entry in dict)
                        {
                            if (entry.Value.Raids > 0)
                            {
                                raids.Add(entry.Value.Raids);
                            }

                            storedData.Players[entry.Key].Raids = 0;
                        }
                    }
                }

                if (raids.Count > 0)
                {
                    var average = raids.Average();

                    foreach (var entry in dict)
                    {
                        if (entry.Value.TotalRaids < average)
                        {
                            storedData.Players.Remove(entry.Key);
                        }
                    }
                }

                storedData.Lockouts.Clear();
                wiped = false;
                SaveData();
            }
        }

        private void SetupMonuments()
        {
            foreach (var monument in TerrainMeta.Path?.Monuments?.ToArray() ?? UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (string.IsNullOrEmpty(monument.displayPhrase.translated))
                {
                    float size = monument.name.Contains("power_sub") ? 35f : Mathf.Max(monument.Bounds.size.Max(), 75f);
                    monuments[monument] = monument.name.Contains("cave") ? 75f : monument.name.Contains("OilrigAI") ? 150f : size;
                }
                else
                {
                    monuments[monument] = GetMonumentFloat(monument.displayPhrase.translated.TrimEnd());
                }

                monuments[monument] += _config.Settings.Management.MonumentDistance;
            }
        }

        private void BlockZoneManagerZones()
        {
            if (ZoneManager == null || !ZoneManager.IsLoaded)
            {
                return;
            }

            var zoneIds = ZoneManager?.Call("GetZoneIDs") as string[];

            if (zoneIds == null)
            {
                return;
            }

            managedZones.Clear();

            foreach (string zoneId in zoneIds)
            {
                var zoneLoc = ZoneManager.Call("GetZoneLocation", zoneId);

                if (!(zoneLoc is Vector3))
                {
                    continue;
                }

                var position = (Vector3)zoneLoc;

                if (position == Vector3.zero)
                {
                    continue;
                }

                var zoneName = Convert.ToString(ZoneManager.Call("GetZoneName", zoneId));

                if (_config.Settings.Inclusions.Any(zone => zone == zoneId || !string.IsNullOrEmpty(zoneName) && zoneName.Contains(zone, CompareOptions.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var zoneInfo = new ZoneInfo();
                var radius = ZoneManager.Call("GetZoneRadius", zoneId);

                if (radius is float)
                {
                    zoneInfo.Distance = (float)radius + Radius + 5f;
                }

                var size = ZoneManager.Call("GetZoneSize", zoneId);

                if (size is Vector3)
                {
                    zoneInfo.Size = (Vector3)size;
                }

                zoneInfo.Position = position;
                zoneInfo.OBB = new OBB(zoneInfo.Position, zoneInfo.Size, Quaternion.identity);
                managedZones[position] = zoneInfo;
            }

            if (managedZones.Count > 0)
            {
                Puts(Backbone.GetMessage("BlockedZones", null, managedZones.Count));
            }
        }

        private void Reinitialize()
        {
            Backbone.Plugin.Skins.Clear();

            if (_config.Skins.RandomWorkshopSkins || _config.Treasure.RandomWorkshopSkins)
            {
                SetWorkshopIDs(); // webrequest.Enqueue("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", null, GetWorkshopIDs, this, Core.Libraries.RequestMethod.GET);
            }

            if (_config.Settings.TeleportMarker)
            {
                Subscribe(nameof(OnMapMarkerAdded));
            }

            if (_config.UI.Enabled)
            {
                Subscribe(nameof(OnPlayerSleepEnded));
            }
        }

        private void OnClanUpdate(string tag)
        {
            foreach (var raid in Raids.Values)
            {
                raid.UpdateClans(tag);
            }
        }

        private void OnClanDestroy(string tag)
        {
            OnClanUpdate(tag);
        }

        private void OnFriendAdded(string playerId, string targetId)
        {
            foreach (var raid in Raids.Values)
            {
                raid.UpdateFriends(playerId, targetId, true);
            }
        }

        private void OnFriendRemoved(string playerId, string targetId)
        {
            foreach (var raid in Raids.Values)
            {
                raid.UpdateFriends(playerId, targetId, false);
            }
        }

        private object OnRestoreUponDeath(BasePlayer player)
        {
            var raid = RaidableBase.Get(player.transform.position);

            if (raid == null)
            {
                return null;
            }

            return _config.Settings.Management.BlockRestorePVE && !raid.AllowPVP || _config.Settings.Management.BlockRestorePVP && raid.AllowPVP ? true : (object)null;
        }

        private object OnNpcKits(ulong targetId)
        {
            var raid = RaidableBase.Get(targetId);

            if (raid == null || !raid.Options.NPC.DespawnInventory)
            {
                return null;
            }

            return true;
        }

        private object canTeleport(BasePlayer player)
        {
            return CanTeleport(player);
        }

        private object CanTeleport(BasePlayer player)
        {
            return !player.IsFlying && (EventTerritory(player.transform.position) || PvpDelay.ContainsKey(player.userID)) ? Backbone.GetMessage("CannotTeleport", player.UserIDString) : null;
        }

        private void OnEntityMounted(BaseMountable m, BasePlayer player)
        {
            if (player.IsNpc)
            {
                return;
            }

            var raid = RaidableBase.Get(player.transform.position);

            if (raid == null || raid.intruders.Contains(player))
            {
                return;
            }

            player.EnsureDismounted();
            raid.RemovePlayer(player);
        }

        private object OnLoseCondition(Item item, float amount)
        {
            var player = item.GetOwnerPlayer();

            if (!IsValid(player))
            {
                return null;
            }

            var raid = RaidableBase.Get(player.transform.position);

            if (raid == null || !raid.Options.EnforceDurability)
            {
                return null;
            }

            uint uid = item.uid;
            float condition;
            if (!raid.conditions.TryGetValue(uid, out condition))
            {
                raid.conditions[uid] = condition = item.condition;
            }

            NextTick(() =>
            {
                if (raid == null)
                {
                    return;
                }

                if (!IsValid(item))
                {
                    raid.conditions.Remove(uid);
                    return;
                }

                item.condition = condition - amount;

                if (item.condition <= 0f && item.condition < condition)
                {
                    item.OnBroken();
                    raid.conditions.Remove(uid);
                }
                else raid.conditions[uid] = item.condition;
            });

            return true;
        }

        private void OnEntityBuilt(Planner planner, GameObject go)
        {
            var e = go.ToBaseEntity();

            if (e == null)
            {
                return;
            }

            var raid = RaidableBase.Get(e.transform.position);

            if (raid == null)
            {
                return;
            }

            if (!raid.Options.AllowBuildingPriviledges && e is BuildingPrivlidge)
            {
                var player = planner.GetOwnerPlayer();
                var item = player.inventory.FindItemID("cupboard.tool");

                if (item != null)
                {
                    item.amount++;
                    item.MarkDirty();
                }
                else player.GiveItem(ItemManager.CreateByName("cupboard.tool"));

                e.Invoke(e.KillMessage, 0.1f);
                return;
            }

            var priv = e.GetBuildingPrivilege();

            if (priv.IsValid())
            {
                if (priv.net.ID < raid.NetworkID)
                {
                    return;
                }

                var decayEntity = e as DecayEntity;

                if (decayEntity.IsValid())
                {
                    if (e.prefabID == 3234260181 || e.prefabID == 72949757)
                    {
                        if (decayEntity.buildingID == raid.BuildingID)
                        {
                            var player = planner.GetOwnerPlayer();

                            if (raid.CanMessage(player))
                            {
                                Backbone.Message(player, "TooCloseToABuilding");
                            }

                            e.Invoke(e.KillMessage, 0.1f);
                            return;
                        }
                    }
                }

                var building = priv.GetBuilding();

                if (building != null && building.ID != raid.BuildingID)
                {
                    return;
                }
            }

            AddEntity(e, raid);
        }

        private void AddEntity(BaseEntity e, RaidableBase raid)
        {
            if (!raid.BuiltList.Contains(e.net.ID))
            {
                raid.BuiltList.Add(e.net.ID);
            }

            RaidEntities[e] = raid;

            if (_config.Settings.Management.DoNotDestroyDeployables && e.name.Contains("assets/prefabs/deployable/"))
            {
                UnityEngine.Object.Destroy(e.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.Destroy(e.GetComponent<GroundWatch>());
                return;
            }

            if (Bases.ContainsKey(raid.BaseIndex) && !Bases[raid.BaseIndex].Contains(e))
            {
                Bases[raid.BaseIndex].Add(e);
            }
        }

        private object CanBeTargeted(NPCPlayerApex npc, StorageContainer container) // guntrap and flameturret. containerioentity for autoturrets which is already covered by priv
        {
            return npc != null && RaidableBase.Has(npc.userID) ? false : (object)null;
        }

#if USE_HTN_HOOK
        private object OnNpcTarget(NPCPlayerApex npc, BaseEntity entity)
        {
            return entity != null && npc != null && entity.IsNpc && RaidableBase.Has(npc.userID) ? true : (object)null;
        }

        private object OnNpcTarget(BaseEntity entity, NPCPlayerApex npc)
        {
            return entity != null && npc != null && entity.IsNpc && RaidableBase.Has(npc.userID) ? true : (object)null;
        }
#else
        private object OnNpcTarget(NPCPlayerApex npc, NPCPlayerApex npc2)
        {
            return npc != null && RaidableBase.Has(npc.userID) ? true : (object)null;
        }

        private object OnNpcTarget(BaseNpc entity, NPCPlayerApex npc)
        {
            return npc != null && RaidableBase.Has(npc.userID) ? true : (object)null;
        }

        private object OnNpcTarget(NPCPlayerApex npc, BaseNpc entity)
        {
            return npc != null && RaidableBase.Has(npc.userID) ? true : (object)null;
        }
#endif

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (player.IsNpc || !EventTerritory(player.transform.position))
            {
                return;
            }

            RaidableBase.StopUsingWand(player);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            UI.Update(player);

            NextTick(() =>
            {
                if (player == null || player.transform == null)
                {
                    return;
                }

                var raid = RaidableBase.Get(player.transform.position, 5f);

                if (raid == null || raid.intruders.Contains(player))
                {
                    return;
                }

                if (InRange(player.transform.position, raid.Location, raid.Options.ProtectionRadius))
                {
                    raid.OnEnterRaid(player);
                }
                else raid.RemovePlayer(player); // 1.5.1 sleeping bag exploit fix
            });
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            var raid = player.IsNpc ? RaidableBase.Get(player.userID) : RaidableBase.Get(player.transform.position);

            if (raid == null)
            {
                return;
            }

            if (player.IsNpc)
            {
                raid.CheckDespawn();

                if (_config.Settings.Management.UseOwners)
                {
                    var attacker = hitInfo?.Initiator as BasePlayer ?? player.lastAttacker as BasePlayer;

                    if (attacker.IsValid() && !attacker.IsNpc)
                    {
                        raid.TrySetOwner(attacker, player, hitInfo);
                    }
                }

                if (raid.Options.NPC.DespawnInventory)
                {
                    player.inventory.Strip();
                }
            }
            else raid.OnPlayerExit(player);
        }

        private object OnPlayerDropActiveItem(BasePlayer player, Item item)
        {
            if (EventTerritory(player.transform.position))
            {
                return true;
            }

            return null;
        }

        private void OnEntityKill(IOEntity io) => OnEntityDeath(io, null);

        private void OnEntityDeath(IOEntity io, HitInfo hitInfo)
        {
            if (!_config.Settings.Management.AutoTurretPowerOnOff)
            {
                return;
            }

            var raid = RaidableBase.Get(io);

            if (raid == null)
            {
                ElectricalConnections.Remove(io.net.ID);
                return;
            }

            if (io is AutoTurret)
            {
                RemoveElectricalConnectionReferences(io);
                return;
            }

            AutoTurret turret;
            if (!ElectricalConnections.TryGetValue(io.net.ID, out turret))
            {
                return;
            }

            raid.turrets.RemoveAll(e => e == null || e.IsDestroyed || e == turret);
            RemoveElectricalConnectionReferences(turret);
            ElectricalConnections.Remove(io.net.ID);
        }

        private void OnEntityDeath(BuildingPrivlidge priv, HitInfo hitInfo)
        {
            var raid = RaidableBase.Get(priv);

            if (raid == null)
            {
                return;
            }

            if (hitInfo?.Initiator == null && !raid.IsOpened)
            {
                priv.inventory.Clear();
            }

            if (raid.Options.RequiresCupboardAccess)
            {
                OnCupboardAuthorize(priv, null);
            }

            if (raid.IsOpened && raid.EndWhenCupboardIsDestroyed())
            {
                raid.CancelInvoke(raid.TryToEnd);
                raid.AwardRaiders();
                raid.Undo();
            }
        }

        private void OnEntityKill(StorageContainer container)
        {
            if (container is BuildingPrivlidge)
            {
                OnEntityDeath(container as BuildingPrivlidge, null);
            }

            EntityHandler(container, null);
        }

        private void OnEntityDeath(StorageContainer container, HitInfo hitInfo) => EntityHandler(container, hitInfo);

        private void OnEntityDeath(Door door, HitInfo hitInfo) => BlockHandler(door, hitInfo);

        private void OnEntityDeath(BuildingBlock block, HitInfo hitInfo) => BlockHandler(block, hitInfo);

        private void OnEntityDeath(SimpleBuildingBlock block, HitInfo hitInfo) => BlockHandler(block, hitInfo);

        private void BlockHandler(BaseEntity entity, HitInfo hitInfo)
        {
            var raid = RaidableBase.Get(entity.transform.position);

            if (raid == null)
            {
                return;
            }

            var player = hitInfo?.Initiator as BasePlayer;

            if (!player.IsValid())
            {
                return;
            }

            raid.TrySetOwner(player, entity, hitInfo);
            raid.CheckDespawn();
        }

        private object OnEntityGroundMissing(StorageContainer container)
        {
            if (_config.Settings.Management.Invulnerable && RaidableBase.Has(container))
            {
                return true;
            }

            EntityHandler(container, null);
            return null;
        }

        private void EntityHandler(StorageContainer container, HitInfo hitInfo)
        {
            var raid = RaidableBase.Get(container);

            if (raid == null || !raid.IsOpened)
            {
                return;
            }

            if (hitInfo?.Initiator == null)
            {
                DropOrRemoveItems(container);
            }
            else if (IsLootingWeapon(hitInfo))
            {
                var player = hitInfo.Initiator as BasePlayer ?? GetInitiatorFromHitInfo(hitInfo, raid, container.transform.position);

                if (player.IsValid())
                {
                    raid.AddLooter(player);
                }
            }

            raid._containers.Remove(container);
            raid.StartTryToEnd();
            UI.Update(raid);

            foreach (var x in Raids.Values)
            {
                if (x._containers.Count > 0)
                {
                    return;
                }
            }

            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnEntityGroundMissing));
        }

        private static bool IsLootingWeapon(HitInfo hitInfo)
        {
            if (hitInfo == null || hitInfo.damageTypes == null)
            {
                return false;
            }

            return hitInfo.damageTypes.Has(DamageType.Explosion) || hitInfo.damageTypes.Has(DamageType.Heat) || hitInfo.damageTypes.IsMeleeType();
        }

        private void OnCupboardAuthorize(BuildingPrivlidge priv, BasePlayer player)
        {
            foreach (var raid in Raids.Values)
            {
                if (raid.priv == priv && raid.Options.RequiresCupboardAccess && !raid.IsAuthed)
                {
                    raid.IsAuthed = true;

                    if (raid.Options.RequiresCupboardAccess && _config.EventMessages.AnnounceRaidUnlock)
                    {
                        foreach (var p in BasePlayer.activePlayerList)
                        {
                            Backbone.Message(p, "OnRaidFinished", FormatGridReference(raid.Location));
                        }
                    }

                    break;
                }
            }

            foreach (var raid in Raids.Values)
            {
                if (!raid.IsAuthed)
                {
                    return;
                }
            }

            Unsubscribe(nameof(OnCupboardAuthorize));
        }

        private object CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            var raid = RaidableBase.Get(entity);

            if (raid == null)
            {
                return null;
            }

            raid.AddLooter(player);

            if (_config.Settings.Management.BlacklistedPickupItems.Contains(entity.ShortPrefabName))
            {
                return false;
            }

            return !raid.Options.AllowPickup && entity.OwnerID == 0 ? false : (object)null;
        }

        private void OnFireBallSpread(FireBall ball, BaseEntity fire)
        {
            if (EventTerritory(fire.transform.position))
            {
                NextTick(() =>
                {
                    if (fire == null || fire.IsDestroyed)
                    {
                        return;
                    }

                    fire.Kill();
                });
            }
        }

        private void OnEntitySpawned(DroppedItemContainer backpack)
        {
            if (!backpack.playerSteamID.IsSteamId())
            {
                return;
            }

            var raid = RaidableBase.Get(backpack.transform.position);

            if (raid == null)
            {
                return;
            }

            if (raid.AllowPVP && _config.Settings.Management.BackpacksPVP || !raid.AllowPVP && _config.Settings.Management.BackpacksPVE)
            {
                backpack.playerSteamID = 0;
            }
        }

        private void OnEntitySpawned(BaseLock entity)
        {
            var parent = entity.GetParentEntity();

            foreach (var raid in Raids.Values)
            {
                foreach (var container in raid._containers)
                {
                    if (parent == container)
                    {
                        entity.Invoke(entity.KillMessage, 0.1f);
                        break;
                    }
                }
            }
        }

        private void OnEntitySpawned(PlayerCorpse corpse)
        {
            if (!IsValid(corpse))
            {
                return;
            }

            RaidableBase raid;
            DelaySettings delaySettings;
            if (PvpDelay.TryGetValue(corpse.playerSteamID, out delaySettings))
            {
                raid = delaySettings.RaidableBase;
            }
            else raid = corpse.playerSteamID.IsSteamId() ? RaidableBase.Get(corpse.transform.position) : RaidableBase.Get(corpse.playerSteamID);

            if (raid == null)
            {
                return;
            }

            if (corpse.playerSteamID.IsSteamId())
            {
                if (raid.Options.EjectCorpses)
                {
                    var player = RustCore.FindPlayerById(corpse.playerSteamID);
                    var data = raid.AddCorpse(corpse, player);

                    if (raid.EjectCorpse(corpse.net.ID, data))
                    {
                        raid.corpses.Remove(corpse.net.ID);
                    }
                    else Interface.CallHook("OnRaidablePlayerCorpse", player, corpse);
                }

                if (_config.Settings.Management.PlayersLootableInPVE && !raid.AllowPVP || _config.Settings.Management.PlayersLootableInPVP && raid.AllowPVP)
                {
                    corpse.playerSteamID = 0;
                }

                return;
            }

            if (raid.Options.NPC.DespawnInventory)
            {
                corpse.Invoke(corpse.KillMessage, 30f);
            }

            raid.npcs.RemoveAll(npc => npc == null || npc.userID == corpse.playerSteamID);
            Npcs.Remove(corpse.playerSteamID);

            if (raid.Options.RespawnRate > 0f)
            {
                raid.TryRespawnNpc();
                return;
            }

            if (!AnyNpcs())
            {
                Unsubscribe(nameof(OnNpcTarget));
            }
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            var buildPos = target.entity && target.entity.transform && target.socket ? target.GetWorldPosition() : target.position;
            var raid = RaidableBase.Get(buildPos);

            if (raid == null)
            {
                return null;
            }

            PlayerInputEx input;
            if (raid.Inputs.TryGetValue(target.player, out input))
            {
                input.Restart();
            }

            if (PlayerInputEx.TryPlaceLadder(target.player, raid))
            {
                return null;
            }

            if (!_config.Settings.Management.AllowBuilding)
            {
                target.player.ChatMessage(lang.GetMessage("Building is blocked!", this, target.player.UserIDString));
                return false;
            }

            return null;
        }

        private void OnLootEntityEnd(BasePlayer player, StorageContainer container)
        {
            if (container?.inventory == null || container.OwnerID.IsSteamId() || IsInvisible(player))
            {
                return;
            }

            var raid = RaidableBase.Get(container);

            if (raid == null || raid.Options.DropTimeAfterLooting <= 0 || (raid.Options.DropOnlyBoxesAndPrivileges && !IsBox(container.prefabID) && !(container is BuildingPrivlidge)))
            {
                return;
            }

            if (container.inventory.IsEmpty() && (container.prefabID == LARGE_WOODEN_BOX || container.prefabID == SMALL_WOODEN_BOX || container.prefabID == COFFIN_STORAGE))
            {
                container.Invoke(container.KillMessage, 0.1f);
            }
            else container.Invoke(() => DropOrRemoveItems(container), raid.Options.DropTimeAfterLooting);
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            var raid = RaidableBase.Get(entity.transform.position);

            if (raid == null)
            {
                return;
            }

            raid.OnLootEntityInternal(player, entity);
        }

        private void CanOpenBackpack(BasePlayer looter, ulong backpackOwnerID)
        {
            var raid = RaidableBase.Get(looter.transform.position);

            if (raid == null)
            {
                return;
            }

            if (!raid.AllowPVP && !_config.Settings.Management.BackpacksOpenPVE || raid.AllowPVP && !_config.Settings.Management.BackpacksOpenPVP)
            {
                looter.Invoke(looter.EndLooting, 0.1f);
                Player.Message(looter, lang.GetMessage("NotAllowed", this, looter.UserIDString));
            }
        }

        private object CanDropBackpack(ulong backpackOwnerID, Vector3 position)
        {
            var player = RustCore.FindPlayerById(backpackOwnerID);

            if (IsValid(player))
            {
                DelaySettings ds;
                if (PvpDelay.TryGetValue(player.userID, out ds) && (ds.AllowPVP && _config.Settings.Management.BackpacksPVP || !ds.AllowPVP && _config.Settings.Management.BackpacksPVE))
                {
                    return true;
                }

                var nearest = GetNearestBase(player.transform.position);

                if (nearest != null && (nearest.AllowPVP && _config.Settings.Management.BackpacksPVP || !nearest.AllowPVP && _config.Settings.Management.BackpacksPVE))
                {
                    return true;
                }
            }

            var raid = RaidableBase.Get(position);

            if (raid == null)
            {
                return null;
            }

            return raid.AllowPVP && _config.Settings.Management.BackpacksPVP || !raid.AllowPVP && _config.Settings.Management.BackpacksPVE;
        }

        private object CanEntityBeTargeted(BaseMountable m, BaseEntity turret)
        {
            if (!turret.IsValid() || turret.OwnerID.IsSteamId())
            {
                return null;
            }

            if (turret is AutoTurret && EventTerritory(m.transform.position) && EventTerritory(turret.transform.position))
            {
                return true;
            }

            var players = GetMountedPlayers(m);

            if (players.Count == 0)
            {
                return null;
            }

            var player = players[0];

            return IsValid(player) && EventTerritory(player.transform.position) && IsTrueDamage(turret) && !IsInvisible(player) ? true : (object)null;
        }

        private object CanEntityBeTargeted(BasePlayer player, BaseEntity turret)
        {
            if (!IsValid(player) || !turret.IsValid() || turret.OwnerID.IsSteamId() || IsInvisible(player))
            {
                return null;
            }

            if (turret is AutoTurret && RaidableBase.Has(player.userID))
            {
                return !EventTerritory(turret.transform.position) ? true : (object)null;
            }

            return !RaidableBase.Has(player.userID) && EventTerritory(player.transform.position) && IsTrueDamage(turret) ? true : (object)null;
        }

        private object CanEntityTrapTrigger(BaseTrap trap, BasePlayer player)
        {
            return IsValid(player) && !RaidableBase.Has(player.userID) && trap.IsValid() && EventTerritory(player.transform.position) && !IsInvisible(player) ? true : (object)null;
        }

        private object CanEntityTakeDamage(BaseEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null || !IsValid(entity))
            {
                return null;
            }

            if (RaidableBase.Has(entity) && hitInfo.damageTypes.GetMajorityDamageType() == DamageType.Decay)
            {
                NullifyDamage(hitInfo);
                return false;
            }

            if (entity is BasePlayer && HandlePlayerDamage(entity as BasePlayer, hitInfo) == HandledResult.Blocked)
            {
                return false;
            }
            else if (ShouldHandleEntity(entity) && HandleEntityDamage(entity, hitInfo) == HandledResult.Blocked)
            {
                return false;
            }

            if (entity is NPCPlayerApex && RaidableBase.Has(entity as NPCPlayerApex))
            {
                return true;
            }

            var raid = RaidableBase.Get(entity.transform.position);
            bool isRaidValid = raid != null;

            if (isRaidValid)
            {
                if (entity.IsNpc)
                {
                    return true;
                }

                if (entity is BaseMountable || entity.name.Contains("modularcar"))
                {
                    if (!_config.Settings.Management.MountDamageFromSamSites && hitInfo.Initiator is SamSite || !_config.Settings.Management.MountDamageFromPlayers && hitInfo.Initiator is BasePlayer)
                    {
                        NullifyDamage(hitInfo);
                        return false;
                    }
                }
            }

            var attacker = hitInfo.Initiator as BasePlayer ?? GetInitiatorFromHitInfo(hitInfo, raid, entity.transform.position);
            bool isAttackerValid = IsValid(attacker);

            if (RaidEntities.ContainsKey(entity))
            {
                if (isAttackerValid && isRaidValid)
                {
                    if (CanBlockOutsideDamage(raid, attacker, raid.Options.BlockOutsideDamageToBaseDistance))
                    {
                        return false;
                    }

                    if (raid.ID.IsSteamId() && IsBox(entity.prefabID) && raid.IsAlly(attacker.userID, Convert.ToUInt64(raid.ID)))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (isRaidValid)
            {
                if (!entity.OwnerID.IsSteamId() && (entity is Door || (isAttackerValid && IsTrueDamage(entity))))
                {
                    return true;
                }
                else if (IsTrueDamage(hitInfo.Initiator))
                {
                    return !(entity is BuildingBlock);
                }
                else if (entity is PlayerCorpse)
                {
                    return true;
                }
            }

            if (!isAttackerValid || !(entity is BasePlayer))
            {
                return null;
            }

            var victim = entity as BasePlayer;

            if (attacker.userID == victim.userID)
            {
                return true;
            }

            if (_config.TruePVE.ServerWidePVP)
            {
                return true;
            }

            if (!EventTerritory(attacker.transform.position))
            {
                return null;
            }

            if (PvpDelay.ContainsKey(victim.userID))
            {
                return true;
            }

            if (!isRaidValid || !raid.AllowPVP)
            {
                return null;
            }

            return true;
        }

        private void OnEntityTakeDamage(BaseEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null || hitInfo.damageTypes == null)
            {
                return;
            }

            if (RaidableBase.Has(entity) && hitInfo.damageTypes.GetMajorityDamageType() == DamageType.Decay)
            {
                NullifyDamage(hitInfo);
                return;
            }

            if (entity is BasePlayer)
            {
                HandlePlayerDamage(entity as BasePlayer, hitInfo);
            }
            else if (ShouldHandleEntity(entity))
            {
                HandleEntityDamage(entity, hitInfo);
            }
        }

        private HandledResult HandlePlayerDamage(BasePlayer victim, HitInfo hitInfo)
        {
            var raid = victim.IsNpc ? RaidableBase.Get(victim.userID) : RaidableBase.Get(victim.transform.position);

            if (raid == null)
            {
                return HandledResult.None;
            }

            var attacker = hitInfo.Initiator as BasePlayer ?? GetInitiatorFromHitInfo(hitInfo, raid, victim.transform.position);

            if (IsValid(attacker))
            {
                if (!attacker.IsNpc && CanBlockOutsideDamage(raid, attacker, raid.Options.BlockOutsideDamageToPlayersDistance))
                {
                    if (victim.IsNpc && !InRange(raid.Location, victim.transform.position, raid.Options.ProtectionRadius))
                    {
                        goto skip;
                    }

                    NullifyDamage(hitInfo);
                    return HandledResult.Blocked;
                }

                skip:
                if (_config.Settings.Management.BlockMounts && attacker.isMounted)
                {
                    NullifyDamage(hitInfo);
                    return HandledResult.Blocked;
                }

                if (victim.IsNpc && !attacker.IsNpc)
                {
                    if (raid.ownerId.IsSteamId() && !raid.IsAlly(attacker))
                    {
                        NullifyDamage(hitInfo);
                        return HandledResult.Blocked;
                    }

                    if (raid.HasLockout(attacker))
                    {
                        NullifyDamage(hitInfo);
                        return HandledResult.Blocked;
                    }

                    FinalDestination fd;
                    if (Backbone.Destinations.TryGetValue(victim.userID, out fd))
                    {
                        var e = attacker.HasParent() ? attacker.GetParentEntity() : null;

                        if (e is ScrapTransportHelicopter || e is HotAirBalloon)
                        {
                            NullifyDamage(hitInfo);
                            return HandledResult.Blocked;
                        }

                        fd.Attack(attacker);
                    }
                }
                else if (!victim.IsNpc && !attacker.IsNpc && victim.userID != attacker.userID)
                {
                    if (!raid.Options.AllowFriendlyFire && raid.IsOnSameTeam(victim.userID, attacker.userID))
                    {
                        NullifyDamage(hitInfo);
                        return HandledResult.Blocked;
                    }

                    if (PvpDelay.ContainsKey(victim.userID))
                    {
                        return HandledResult.Allowed;
                    }

                    if (!raid.AllowPVP)
                    {
                        NullifyDamage(hitInfo);
                        return HandledResult.Blocked;
                    }
                }
                else if (RaidableBase.Has(attacker.userID))
                {
                    if (RaidableBase.Has(victim.userID))
                    {
                        NullifyDamage(hitInfo);
                        return HandledResult.Blocked;
                    }

                    if (raid.Options.NPC.Accuracy < UnityEngine.Random.Range(0f, 100f))
                    {
                        if (victim.GetMounted() is MiniCopter || victim.GetMounted() is ScrapTransportHelicopter)
                        {
                            return HandledResult.Allowed;
                        }

                        NullifyDamage(hitInfo);
                        return HandledResult.Blocked;
                    }
                }
            }
            else if (RaidableBase.Has(victim.userID))
            {
                var entity = hitInfo.Initiator;

                if (entity.IsValid() && entity is AutoTurret)
                {
                    return HandledResult.Allowed;
                }

                NullifyDamage(hitInfo); // make npc's immune to all damage which isn't from a player or turret
                return HandledResult.Blocked;
            }

            return HandledResult.None;
        }

        private HandledResult HandleEntityDamage(BaseEntity entity, HitInfo hitInfo)
        {
            var raid = RaidableBase.Get(entity);

            if (raid == null)
            {
                return HandledResult.None;
            }

            if ((_config.Settings.Management.BlocksImmune && entity is BuildingBlock) || hitInfo.damageTypes.Has(DamageType.Decay))
            {
                NullifyDamage(hitInfo, entity is BuildingBlock ? entity as BaseCombatEntity : null);
                return HandledResult.Blocked;
            }

            if (raid.Type != RaidableType.None)
            {
                raid.CheckDespawn();
            }

            if (_config.Settings.Management.Invulnerable && IsBox(entity.prefabID) && !raid.BuiltList.Contains(entity.net.ID))
            {
                NullifyDamage(hitInfo);
            }

            var attacker = hitInfo.Initiator as BasePlayer ?? GetInitiatorFromHitInfo(hitInfo, raid, entity.transform.position);

            if (!IsValid(attacker) || attacker.IsNpc)
            {
                if (hitInfo.damageTypes.GetMajorityDamageType() == DamageType.Heat && entity is BuildingPrivlidge)
                {
                    NullifyDamage(hitInfo);
                    return HandledResult.Blocked;
                }

                return HandledResult.None;
            }

            if (raid.ownerId.IsSteamId() && !raid.IsAlly(attacker))
            {
                NullifyDamage(hitInfo);
                return HandledResult.Blocked;
            }

            if ((_config.Settings.Management.BlockMounts && attacker.isMounted) || CanBlockOutsideDamage(raid, attacker, raid.Options.BlockOutsideDamageToBaseDistance))
            {
                NullifyDamage(hitInfo);
                return HandledResult.Blocked;
            }

            raid.TrySetOwner(attacker, entity, hitInfo);

            if (raid.IsOpened && IsLootingWeapon(hitInfo))
            {
                raid.AddLooter(attacker);
            }

            if (!raid.Options.ExplosionModifier.Equals(100) && hitInfo.damageTypes.Has(DamageType.Explosion))
            {
                float m = Mathf.Clamp(raid.Options.ExplosionModifier, 0f, 999f);

                hitInfo.damageTypes.Scale(DamageType.Explosion, m.Equals(0f) ? 0f : m / 100f);
            }

            if (!raid.IsEngaged && raid.Type != RaidableType.None)
            {
                raid.IsEngaged = true;
                raid.CheckDespawn();
            }

            return HandledResult.Allowed;
        }

        private bool ShouldHandleEntity(BaseEntity entity)
        {
            return entity is BuildingBlock || entity is SimpleBuildingBlock || entity.name.Contains("assets/prefabs/deployable/");
        }

        private BasePlayer GetInitiatorFromHitInfo(HitInfo hitInfo, RaidableBase raid, Vector3 a)
        {
            if (hitInfo.Initiator.IsValid() && hitInfo.Initiator is FireBall && raid != null)
            {
                uint id = hitInfo.Initiator.net.ID;

                foreach (var intruder in raid.intruders)
                {
                    if (intruder.IsValid() && Time.time - intruder.lastDealtDamageTime < 1f && IsUsingProjectile(intruder))
                    {
                        intruder.lastDealtDamageTime = Time.time;
                        raid.records[id] = intruder;
                        return intruder;
                    }
                }

                BasePlayer player;
                if (raid.records.TryGetValue(id, out player) && player.IsValid())
                {
                    player.lastDealtDamageTime = Time.time;
                    return player;
                }
            }

            return null;
        }

        private bool IsUsingProjectile(BasePlayer player)
        {
            if (player == null || player.svActiveItemID == 0)
            {
                return false;
            }

            Item item = player.GetActiveItem();

            if (item == null)
            {
                return false;
            }

            if (item.info.shortname == "flamethrower" || item.info.shortname == "rocket.launcher")
            {
                return true;
            }

            return item.GetHeldEntity() is BaseProjectile;
        }

        #endregion Hooks

        #region Spawn

        private static float GetSpawnHeight(Vector3 target)
        {
            float y = TerrainMeta.HeightMap.GetHeight(target);
            float w = TerrainMeta.WaterMap.GetHeight(target);
            float p = TerrainMeta.HighestPoint.y + 250f;
            RaycastHit hit;

            if (Physics.Raycast(new Vector3(target.x, w, target.z), Vector3.up, out hit, p, Layers.Mask.World))
            {
                y = Mathf.Max(y, hit.point.y);

                if (Physics.Raycast(new Vector3(target.x, hit.point.y + 0.5f, target.z), Vector3.up, out hit, p, Layers.Mask.World))
                {
                    y = Mathf.Max(y, hit.point.y);
                }
            }

            return Mathf.Max(y, w);
        }

        private bool OnIceSheetOrInDeepWater(Vector3 vector)
        {
            if (TerrainMeta.WaterMap.GetHeight(vector) - TerrainMeta.HeightMap.GetHeight(vector) > 5f)
            {
                return true;
            }

            vector.y += TerrainMeta.HighestPoint.y;

            RaycastHit hit;
            if (Physics.Raycast(vector, Vector3.down, out hit, vector.y + 1f, Layers.Mask.World, QueryTriggerInteraction.Ignore) && hit.collider.name.StartsWith("ice_sheet"))
            {
                return true;
            }

            return false;
        }

        protected void LoadSpawns()
        {
            raidSpawns.Clear();
            raidSpawns.Add(RaidableType.Grid, new RaidableSpawns());

            if (SpawnsFileValid(_config.Settings.Manual.SpawnsFile))
            {
                var spawns = GetSpawnsLocations(_config.Settings.Manual.SpawnsFile);

                if (spawns?.Count > 0)
                {
                    Puts(Backbone.GetMessage("LoadedManual", null, spawns.Count));
                    raidSpawns[RaidableType.Manual] = new RaidableSpawns(spawns);
                }
            }

            if (SpawnsFileValid(_config.Settings.Schedule.SpawnsFile))
            {
                var spawns = GetSpawnsLocations(_config.Settings.Schedule.SpawnsFile);

                if (spawns?.Count > 0)
                {
                    Puts(Backbone.GetMessage("LoadedScheduled", null, spawns.Count));
                    raidSpawns[RaidableType.Scheduled] = new RaidableSpawns(spawns);
                }
            }

            if (SpawnsFileValid(_config.Settings.Maintained.SpawnsFile))
            {
                var spawns = GetSpawnsLocations(_config.Settings.Maintained.SpawnsFile);

                if (spawns?.Count > 0)
                {
                    Puts(Backbone.GetMessage("LoadedMaintained", null, spawns.Count));
                    raidSpawns[RaidableType.Maintained] = new RaidableSpawns(spawns);
                }
            }

            if (SpawnsFileValid(_config.Settings.Buyable.SpawnsFile))
            {
                var spawns = GetSpawnsLocations(_config.Settings.Buyable.SpawnsFile);

                if (spawns?.Count > 0)
                {
                    Puts(Backbone.GetMessage("LoadedBuyable", null, spawns.Count));
                    raidSpawns[RaidableType.Purchased] = new RaidableSpawns(spawns);
                }
            }
        }

        protected void SetupGrid()
        {
            if (raidSpawns.Count >= 5)
            {
                StartAutomation();
                return;
            }

            StopGridCoroutine();

            NextTick(() =>
            {
                gridStopwatch.Start();
                gridTime = Time.realtimeSinceStartup;
                gridCoroutine = ServerMgr.Instance.StartCoroutine(GenerateGrid());
            });
        }

        private static bool ContainsTopology(TerrainTopology.Enum mask, Vector3 position)
        {
            return (TerrainMeta.TopologyMap.GetTopology(position) & (int)mask) != 0;
        }

        private static bool ContainsTopology(TerrainTopology.Enum mask, Vector3 position, float radius)
        {
            return (TerrainMeta.TopologyMap.GetTopology(position, radius) & (int)mask) != 0;
        }

        private bool IsInsideBounds(OBB obb, Vector3 worldPos)
        {
            return obb.ClosestPoint(worldPos) == worldPos;
        }

        private bool IsValidLocation(Vector3 vector, float radius, float md)
        {
            foreach (var zone in managedZones)
            {
                if (zone.Value.Size != Vector3.zero)
                {
                    if (IsInsideBounds(zone.Value.OBB, vector))
                    {
                        draw(vector, "Z");
                        return false;
                    }
                }
                else if (InRange(zone.Key, vector, zone.Value.Distance))
                {
                    draw(vector, "Z");
                    return false;
                }
            }

            if (IsMonumentPosition(vector) || ContainsTopology(TerrainTopology.Enum.Monument, vector, md))
            {
                draw(vector, "M");
                return false;
            }

            if (!_config.Settings.Management.AllowOnRivers && ContainsTopology(TerrainTopology.Enum.River | TerrainTopology.Enum.Riverside, vector, Radius))
            {
                draw(vector, "W");
                return false;
            }

            if (!_config.Settings.Management.AllowOnRoads && ContainsTopology(TerrainTopology.Enum.Road | TerrainTopology.Enum.Roadside, vector, Radius))
            {
                draw(vector, "R");
                return false;
            }

            if (OnIceSheetOrInDeepWater(vector))
            {
                draw(vector, "D");
                return false;
            }

            if (!IsAreaSafe(ref vector, radius, Layers.Mask.World))
            {
                return false;
            }

            return true;
        }

        private void StopGridCoroutine()
        {
            if (gridCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(gridCoroutine);
                gridCoroutine = null;
            }
        }

        private IEnumerator GenerateGrid() // Credits to Jake_Rich for creating this for me!
        {
            RaidableSpawns rs = raidSpawns[RaidableType.Grid] = new RaidableSpawns();
            int minPos = (int)(World.Size / -2f);
            int maxPos = (int)(World.Size / 2f);
            int checks = 0;
            float max = GetMaxElevation();
            var _instruction = ConVar.FPS.limit > 80 ? CoroutineEx.waitForSeconds(INSTRUCTION_TIME) : null;
            float md = Radius * 2f + _config.Settings.Management.MonumentDistance;

            for (float x = minPos; x < maxPos; x += 12.5f)
            {
                for (float z = minPos; z < maxPos; z += 12.5f)
                {
                    var pos = new Vector3(x, 0f, z);
                    pos.y = GetSpawnHeight(pos);

                    draw(pos, "O");
                    ExtractLocation(rs, max, pos, md);

                    if (++checks >= 75)
                    {
                        checks = 0;
                        yield return _instruction;
                    }
                }
            }

            Puts(Backbone.GetMessage("InitializedGrid", null, gridStopwatch.Elapsed.Seconds, gridStopwatch.Elapsed.Milliseconds, World.Size, rs.Count));
            gridCoroutine = null;
            gridStopwatch.Stop();
            gridStopwatch.Reset();
            StartAutomation();
        }

        private void ExtractLocation(RaidableSpawns rs, float max, Vector3 pos, float md)
        {
            if (IsValidLocation(pos, Radius, md))
            {
                var elevation = GetTerrainElevation(pos);

                if (IsFlatTerrain(pos, elevation, max))
                {
                    rs.Add(new RaidableSpawnLocation
                    {
                        Location = pos,
                        Elevation = elevation
                    });
                }
                else draw(pos, "Elevation: " + (elevation.Max - elevation.Min).ToString());
            }
        }

        private void draw(Vector3 pos, string text)
        {
#if DEBUG
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!player.IsAdmin || !InRange(player.transform.position, pos, 100f))
                    continue;

                player.SendConsoleCommand("ddraw.text", 60f, Color.yellow, pos, text);
            }
#endif
        }

        private float GetMaxElevation()
        {
            float max = 2.5f;

            foreach (var x in Buildings.Profiles.Values)
            {
                if (x.Elevation > max)
                {
                    max = x.Elevation;
                }
            }

            return ++max;
        }

        private bool SpawnsFileValid(string spawnsFile)
        {
            if (Spawns == null || !Spawns.IsLoaded)
            {
                return false;
            }

            if (!FileExists($"SpawnsDatabase{Path.DirectorySeparatorChar}{spawnsFile}"))
            {
                return false;
            }

            return Spawns?.Call("GetSpawnsCount", spawnsFile) is int;
        }

        private List<RaidableSpawnLocation> GetSpawnsLocations(string spawnsFile)
        {
            object success = Spawns?.Call("LoadSpawnFile", spawnsFile);

            if (success == null)
            {
                return null;
            }

            var list = (List<Vector3>)success;
            var locations = new List<RaidableSpawnLocation>();

            foreach (var pos in list)
            {
                locations.Add(new RaidableSpawnLocation
                {
                    Location = pos
                });
            }

            list.Clear();

            return locations;
        }

        private void StartAutomation()
        {
            if (scheduledEnabled)
            {
                if (storedData.RaidTime != DateTime.MinValue.ToString() && GetRaidTime() > _config.Settings.Schedule.IntervalMax) // Allows users to lower max event time
                {
                    storedData.RaidTime = DateTime.MinValue.ToString();
                    SaveData();
                }

                StartScheduleCoroutine();
            }

            StartMaintainCoroutine();
        }

        private static void Shuffle<T>(IList<T> list) // Fisher-Yates shuffle
        {
            int count = list.Count;
            int n = count;
            while (n-- > 0)
            {
                int k = UnityEngine.Random.Range(0, count);
                int j = UnityEngine.Random.Range(0, count);
                T value = list[k];
                list[k] = list[j];
                list[j] = value;
            }
        }

        private Vector3 GetEventPosition(BuildingOptions options, BasePlayer owner, float distanceFrom, bool checkTerrain, RaidableSpawns rs, RaidableType type)
        {
            rs.Check();

            int num1 = 0;
            float distance;

            switch (type)
            {
                case RaidableType.Maintained:
                    {
                        distance = _config.Settings.Maintained.Distance;
                        break;
                    }
                case RaidableType.Purchased:
                    {
                        distance = _config.Settings.Buyable.Distance;
                        break;
                    }
                case RaidableType.Scheduled:
                    {
                        distance = _config.Settings.Schedule.Distance;
                        break;
                    }
                case RaidableType.Manual:
                    {
                        distance = _config.Settings.Manual.Distance;
                        break;
                    }
                default:
                    {
                        distance = 100f;
                        break;
                    }
            }

            bool isOwner = IsValid(owner);
            float safeRadius = Mathf.Max(options.ArenaWalls.Radius, Radius * 2f);
            int attempts = isOwner ? 1000 : 2000;
            int layers = Layers.Mask.Player_Server | Layers.Mask.Construction | Layers.Mask.Deployed;
            float buildRadius = Mathf.Max(_config.Settings.Management.CupboardDetectionRadius, options.ArenaWalls.Radius, options.ProtectionRadius) + 5f;
            float submergedRadius = Mathf.Max(options.ArenaWalls.Radius, options.ProtectionRadius);

            while (rs.Count > 0 && attempts > 0)
            {
                var rsl = rs.GetRandom();

                if (isOwner && distanceFrom > 0 && !InRange(owner.transform.position, rsl.Location, distanceFrom))
                {
                    num1++;
                    continue;
                }

                if (RaidableBase.IsTooClose(rsl.Location, distance))
                {
                    continue;
                }

                attempts--;

                if (!IsAreaSafe(ref rsl.Location, safeRadius, layers, type))
                {
                    continue;
                }

                if (checkTerrain && !IsFlatTerrain(rsl.Location, rsl.Elevation, options.Elevation))
                {
                    continue;
                }

                if (HasActiveBuildingPrivilege(rsl.Location, buildRadius))
                {
                    continue;
                }

                var position = new Vector3(rsl.Location.x, rsl.Location.y, rsl.Location.z);
                float w = TerrainMeta.WaterMap.GetHeight(position);
                float h = TerrainMeta.HeightMap.GetHeight(position);

                if (w - h > 1f)
                {
                    if (!options.Submerged)
                    {
                        continue;
                    }

                    position.y = w;
                }

                if (!options.Submerged && options.SubmergedAreaCheck && !raidSpawns.ContainsKey(type) && IsSubmerged(position, submergedRadius))
                {
                    continue;
                }

                if (!checkTerrain)
                {
                    rs.RemoveNear(rsl, options.ProtectionRadius);
                }

                return position;
            }

            rs.TryAddRange();

            if (rs.Count > 0 && rs.Count < 500 && num1 >= rs.Count / 2 && (distanceFrom += 50f) < World.Size)
            {
                return GetEventPosition(options, owner, distanceFrom, checkTerrain, rs, type);
            }

            return Vector3.zero;
        }

        private bool IsSubmerged(Vector3 position, float radius)
        {
            foreach (var vector in GetCircumferencePositions(position, radius, 90f, false, 1f)) // 90 to reduce lag as this is called thousands of times
            {
                float w = TerrainMeta.WaterMap.GetHeight(vector);
                float h = TerrainMeta.HeightMap.GetHeight(vector);

                if (w - h > 1f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasActiveBuildingPrivilege(Vector3 target, float radius)
        {
            bool flag = false;
            var list = Pool.GetList<BuildingPrivlidge>();
            Vis.Entities(target, radius, list);
            foreach (var tc in list)
            {
                if (tc.IsValid())
                {
                    flag = true;
                    break;
                }
            }

            Pool.FreeList(ref list);
            return flag;
        }

        private float GetRockHeight(Vector3 a)
        {
            RaycastHit hit;
            if (Physics.Raycast(a + new Vector3(0f, 50f, 0f), Vector3.down, out hit, a.y + 51f, Layers.Mask.World, QueryTriggerInteraction.Ignore))
            {
                return Mathf.Abs(hit.point.y - a.y);
            }

            return 0f;
        }

        private readonly List<string> assets = new List<string>
        {
            "/props/", "/structures/", "/building/", "train_", "powerline_", "dune", "candy-cane", "assets/content/nature/"
        };

        private bool IsAreaSafe(ref Vector3 position, float radius, int layers, RaidableType type = RaidableType.None)
        {
            var colliders = Pool.GetList<Collider>();

            Vis.Colliders(position, radius, colliders, layers, QueryTriggerInteraction.Ignore);

            int count = colliders.Count;

            foreach (var collider in colliders)
            {
                if (collider == null || collider.transform == null)
                {
                    count--;
                    continue;
                }

                if (IsAsset(collider.name))
                {
                    count = int.MaxValue;
                    break;
                }

                var e = collider.ToBaseEntity();

                if (e.IsValid())
                {
                    if (RaidableBase.Has(e))
                    {
                        count = int.MaxValue;
                        break;
                    }
                    else if (e.IsNpc || e is SleepingBag || e is BaseOven)
                    {
                        count--;
                    }
                    else if (e is BasePlayer)
                    {
                        var player = e as BasePlayer;

                        if (!(player.IsFlying || _config.Settings.Management.EjectSleepers && player.IsSleeping()))
                        {
                            count = int.MaxValue;
                            break;
                        }
                        else count--;
                    }
                    else if (e.OwnerID == 0)
                    {
                        if (e is BuildingBlock)
                        {
                            count = int.MaxValue;
                            break;
                        }
                        else count--;
                    }
                }

                if (collider.gameObject.layer == (int)Layer.World)
                {
                    if (collider.name.Contains("rock_"))
                    {
                        float height = GetRockHeight(collider.transform.position);

                        if (height > 2f)
                        {
                            //if (!_rocks.ContainsKey(position)) _rocks.Add(position, $"{collider.bounds.size}: {height}");
                            draw(position, $"{collider.name}> {height}");
                            count = int.MaxValue;
                            break;
                        }
                        else count--;
                    }
                    else if (!_config.Settings.Management.AllowOnRoads && collider.name.StartsWith("road_"))
                    {
                        draw(position, "road");
                        count = int.MaxValue;
                        break;
                    }
                    else if (collider.name.StartsWith("ice_sheet"))
                    {
                        draw(position, "ice_sheet");
                        count = int.MaxValue;
                        break;
                    }
                    else count--;
                }
                else if (collider.gameObject.layer == (int)Layer.Water)
                {
                    if (!_config.Settings.Management.AllowOnRivers && collider.name.StartsWith("River Mesh"))
                    {
                        count = int.MaxValue;
                        draw(position, "river");
                        break;
                    }

                    count--;
                }
            }

            Pool.FreeList(ref colliders);

            return count == 0;
        }

        private bool IsAsset(string value)
        {
            foreach (var asset in assets)
            {
                if (value.Contains(asset))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsMonumentPosition(Vector3 target)
        {
            foreach (var monument in monuments)
            {
                if (InRange(monument.Key.transform.position, target, monument.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetAllowPVP(RaidableType type, RaidableBase raid, bool flag)
        {
            if (type == RaidableType.Maintained && _config.Settings.Maintained.ConvertPVP)
            {
                raid.AllowPVP = false;
            }
            else if (type == RaidableType.Scheduled && _config.Settings.Schedule.ConvertPVP)
            {
                raid.AllowPVP = false;
            }
            else if (type == RaidableType.Manual && _config.Settings.Manual.ConvertPVP)
            {
                raid.AllowPVP = false;
            }
            else if (type == RaidableType.Purchased && _config.Settings.Buyable.ConvertPVP)
            {
                raid.AllowPVP = false;
            }
            else if (type == RaidableType.Maintained && _config.Settings.Maintained.ConvertPVE)
            {
                raid.AllowPVP = true;
            }
            else if (type == RaidableType.Scheduled && _config.Settings.Schedule.ConvertPVE)
            {
                raid.AllowPVP = true;
            }
            else if (type == RaidableType.Manual && _config.Settings.Manual.ConvertPVE)
            {
                raid.AllowPVP = true;
            }
            else if (type == RaidableType.Purchased && _config.Settings.Buyable.ConvertPVE)
            {
                raid.AllowPVP = true;
            }
            else raid.AllowPVP = flag;
        }

        public bool TryOpenEvent(RaidableType type, Vector3 position, int uid, string BaseName, KeyValuePair<string, BuildingOptions> building, out RaidableBase raid)
        {
            if (IsUnloading)
            {
                raid = null;
                return false;
            }

            raid = new GameObject().AddComponent<RaidableBase>();

            SetAllowPVP(type, raid, building.Value.AllowPVP);

            raid.DifficultyMode = building.Value.Mode == RaidableMode.Easy ? Backbone.Easy : building.Value.Mode == RaidableMode.Medium ? Backbone.Medium : building.Value.Mode == RaidableMode.Hard ? Backbone.Hard : building.Value.Mode == RaidableMode.Expert ? Backbone.Expert : Backbone.Nightmare;

            /*if (Options.ExplosionModifier != 100f)
            {
                if (Options.ExplosionModifier <= 75)
                {
                    raid.DifficultyMode = Backbone.GetMessage("VeryHard");
                }
                else if (Options.ExplosionModifier >= 150)
                {
                    raid.DifficultyMode = Backbone.GetMessage("VeryEasy");
                }
            }*/

            raid.PastedLocation = position;
            raid.Location = position;
            raid.Options = building.Value;
            raid.BaseName = string.IsNullOrEmpty(BaseName) ? building.Key : BaseName;
            raid.Type = type;
            raid.uid = uid;

            Cycle.Add(type, building.Value.Mode, BaseName);

            if (_config.Settings.NoWizardry && Wizardry != null && Wizardry.IsLoaded)
            {
                Subscribe(nameof(OnActiveItemChanged));
            }

            Subscribe(nameof(OnEntitySpawned));

            if (IsPVE())
            {
                Subscribe(nameof(CanEntityTakeDamage));
            }
            else Subscribe(nameof(OnEntityTakeDamage));

            storedData.TotalEvents++;
            SaveData();

            if (_config.LustyMap.Enabled && LustyMap != null && LustyMap.IsLoaded)
            {
                AddTemporaryLustyMarker(position, uid);
            }

            if (Map)
            {
                AddMapPrivatePluginMarker(position, uid);
            }

            Raids[uid] = raid;
            return true;
        }

        #endregion

        #region Paste

        protected bool IsGridLoading
        {
            get
            {
                return gridCoroutine != null;
            }
        }

        protected bool IsPasteAvailable
        {
            get
            {
                foreach (var raid in Raids.Values)
                {
                    if (raid.IsLoading)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private bool TryBuyRaidServerRewards(BasePlayer buyer, BasePlayer player, RaidableMode mode)
        {
            if (_config.Settings.ServerRewards.Any && ServerRewards != null && ServerRewards.IsLoaded)
            {
                int cost = mode == RaidableMode.Easy ? _config.Settings.ServerRewards.Easy : mode == RaidableMode.Medium ? _config.Settings.ServerRewards.Medium : mode == RaidableMode.Hard ? _config.Settings.ServerRewards.Hard : mode == RaidableMode.Expert ? _config.Settings.ServerRewards.Expert : _config.Settings.ServerRewards.Nightmare;
                var success = ServerRewards?.Call("CheckPoints", buyer.userID);
                int points = success is int ? Convert.ToInt32(success) : 0;

                if (points > 0 && points - cost >= 0)
                {
                    if (BuyRaid(player, mode))
                    {
                        ServerRewards.Call("TakePoints", buyer.userID, cost);
                        Backbone.Message(buyer, "ServerRewardPointsTaken", cost);
                        if (buyer != player) Backbone.Message(player, "ServerRewardPointsGift", buyer.displayName, cost);
                        return true;
                    }
                }
                else Backbone.Message(buyer, "ServerRewardPointsFailed", cost);
            }

            return false;
        }

        private bool TryBuyRaidEconomics(BasePlayer buyer, BasePlayer player, RaidableMode mode)
        {
            if (_config.Settings.Economics.Any && Economics != null && Economics.IsLoaded)
            {
                var cost = mode == RaidableMode.Easy ? _config.Settings.Economics.Easy : mode == RaidableMode.Medium ? _config.Settings.Economics.Medium : mode == RaidableMode.Hard ? _config.Settings.Economics.Hard : mode == RaidableMode.Expert ? _config.Settings.Economics.Expert : _config.Settings.Economics.Nightmare;
                var success = Economics?.Call("Balance", buyer.UserIDString);
                var points = success is double ? Convert.ToDouble(success) : 0;

                if (points > 0 && points - cost >= 0)
                {
                    if (BuyRaid(player, mode))
                    {
                        Economics.Call("Withdraw", buyer.UserIDString, cost);
                        Backbone.Message(buyer, "EconomicsWithdraw", cost);
                        if (buyer != player) Backbone.Message(player, "EconomicsWithdrawGift", buyer.displayName, cost);
                        return true;
                    }
                }
                else Backbone.Message(buyer, "EconomicsWithdrawFailed", cost);
            }

            return false;
        }

        private bool BuyRaid(BasePlayer owner, RaidableMode mode)
        {
            string message;
            var position = SpawnRandomBase(out message, RaidableType.Purchased, mode, null, false, owner);

            if (position != Vector3.zero)
            {
                var grid = FormatGridReference(position);
                Backbone.Message(owner, "BuyBaseSpawnedAt", position, grid);

                if (_config.EventMessages.AnnounceBuy)
                {
                    foreach (var target in BasePlayer.activePlayerList)
                    {
                        var announcement = Backbone.GetMessage("BuyBaseAnnouncement", target.UserIDString, owner.displayName, position, grid);
                        target.SendConsoleCommand("chat.add", 2, _config.Settings.ChatID, announcement);
                    }
                }

                Puts(Backbone.RemoveFormatting(Backbone.GetMessage("BuyBaseAnnouncement", null, owner.displayName, position, grid)));
                return true;
            }

            Backbone.Message(owner, message);
            return false;
        }

        private static bool IsDifficultyAvailable(RaidableMode mode, bool checkAllowPVP)
        {
            if (!CanSpawnDifficultyToday(mode))
            {
                return false;
            }

            foreach (var option in Buildings.Profiles.Values)
            {
                if (option.Mode != mode || (checkAllowPVP && !_config.Settings.Buyable.BuyPVP && option.AllowPVP))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void PasteBuilding(RaidableType type, Vector3 position, KeyValuePair<string, BuildingOptions> building, BasePlayer owner)
        {
            if (Locations.Contains(position)) return;

            int uid;

            do
            {
                uid = UnityEngine.Random.Range(1000, 100000);
            } while (Raids.ContainsKey(uid));

            var callback = new Action(() =>
            {
                RaidableBase raid;
                if (TryOpenEvent(type, position, uid, building.Key, building, out raid))
                {
                    Cycle.Add(type, building.Value.Mode, building.Key);

                    if (type == RaidableType.Purchased && _config.Settings.Buyable.UsePayLock)
                    {
                        raid.TrySetPayLock(owner);
                    }
                }
                else Locations.Remove(position);
            });

            var list = GetListedOptions(building.Value.PasteOptions);
            float rotationCorrection = IsValid(owner) ? DegreeToRadian(owner.GetNetworkRotation().eulerAngles.y) : 0f;

            Locations.Add(position);
            CopyPaste.Call("TryPasteFromVector3", position, rotationCorrection, building.Key, list.ToArray(), callback);
        }

        private List<string> GetListedOptions(List<PasteOption> options)
        {
            var list = new List<string>();
            bool flag1 = false, flag2 = false, flag3 = false, flag4 = false;

            for (int i = 0; i < options.Count; i++)
            {
                string key = options[i].Key.ToLower();
                string value = options[i].Value.ToLower();

                if (key == "stability") flag1 = true;
                if (key == "autoheight") flag2 = true;
                if (key == "height") flag3 = true;
                if (key == "entityowner") flag4 = true;

                list.Add(key);
                list.Add(value);
            }

            if (!flag1)
            {
                list.Add("stability");
                list.Add("false");
            }

            if (!flag2)
            {
                list.Add("autoheight");
                list.Add("false");
            }

            if (!flag3)
            {
                list.Add("height");
                list.Add("2.5");
            }

            if (!flag4)
            {
                list.Add("entityowner");
                list.Add("false");
            }

            return list;
        }

        private float DegreeToRadian(float angle)
        {
            return angle.Equals(0f) ? 0f : (float)(Math.PI * angle / 180.0f);
        }

        private void OnPasteFinished(List<BaseEntity> pastedEntities)
        {
            if (pastedEntities == null || pastedEntities.Count == 0)
            {
                return;
            }

            Timer t = null;

            t = timer.Repeat(1f, 120, () =>
            {
                if (IsUnloading)
                {
                    if (t != null)
                    {
                        t.Destroy();
                    }

                    return;
                }

                pastedEntities.RemoveAll(e => e == null || e.IsDestroyed);

                var raid = RaidableBase.Get(pastedEntities);

                if (raid == null)
                {
                    return;
                }

                if (t != null)
                {
                    t.Destroy();
                }

                int baseIndex = 0;

                while (Bases.ContainsKey(baseIndex) || Indices.ContainsKey(baseIndex))
                {
                    baseIndex = UnityEngine.Random.Range(1, 9999999);
                }

                Indices[baseIndex] = raid;
                Bases[baseIndex] = pastedEntities;

                raid.SetEntities(baseIndex, pastedEntities);
            });
        }

        private IEnumerator UndoRoutine(int baseIndex, List<uint> builtList, Vector3 position)
        {
            var raid = RaidableBase.Get(baseIndex);

            if (raid != null)
            {
                UnityEngine.Object.Destroy(raid.gameObject);
            }

            List<BaseEntity> entities;
            if (!Bases.TryGetValue(baseIndex, out entities))
            {
                yield break;
            }

            int total = 0;
            int batchLimit = Mathf.Clamp(_config.Settings.BatchLimit, 1, 15);
            var _instruction = ConVar.FPS.limit > 80 ? CoroutineEx.waitForSeconds(INSTRUCTION_TIME) : null;

            while (entities.Count > 0)
            {
                var e = entities[0];

                if (e == null)
                {
                    entities.Remove(e);
                    continue;
                }

                if (!e.IsDestroyed)
                {
                    e.Kill();
                }

                if (++total % batchLimit == 0)
                {
                    yield return _instruction;
                }

                if (e == null || e.IsDestroyed)
                {
                    entities.Remove(e);
                    RaidEntities.Remove(e);
                }
            }

            Bases.Remove(baseIndex);
            Indices.Remove(baseIndex);

            if (Bases.Count == 0)
            {
                UnsubscribeHooks();
            }

            Interface.CallHook("OnRaidableBaseDespawned", position);
        }

        private void UndoPaste(Vector3 position, RaidableBase raid, int baseIndex, List<uint> builtList)
        {
            if (IsUnloading || !Bases.ContainsKey(baseIndex))
            {
                return;
            }

            if (_config.Settings.Management.DespawnMinutes > 0)
            {
                if (_config.EventMessages.ShowWarning)
                {
                    var grid = FormatGridReference(position);

                    foreach (var target in BasePlayer.activePlayerList)
                    {
                        var message = Backbone.GetMessage("DestroyingBaseAt", target.UserIDString, grid, _config.Settings.Management.DespawnMinutes);
                        target.SendConsoleCommand("chat.add", 2, _config.Settings.ChatID, message);
                    }
                }

                float time = _config.Settings.Management.DespawnMinutes * 60f;

                if (raid != null)
                {
                    raid.IsDespawning = true;
                    raid.despawnTime = Time.realtimeSinceStartup + time;
                }

                timer.Once(time, () =>
                {
                    if (!IsUnloading)
                    {
                        ServerMgr.Instance.StartCoroutine(UndoRoutine(baseIndex, builtList, position));
                    }
                });
            }
            else ServerMgr.Instance.StartCoroutine(UndoRoutine(baseIndex, builtList, position));
        }

        private static List<Vector3> GetCircumferencePositions(Vector3 center, float radius, float next, bool spawnHeight, float y = 0f)
        {
            var positions = new List<Vector3>();

            if (next < 1f)
            {
                next = 1f;
            }

            float angle = 0f;
            float angleInRadians = 2 * (float)Math.PI;

            while (angle < 360)
            {
                float radian = (angleInRadians / 360) * angle;
                float x = center.x + radius * (float)Math.Cos(radian);
                float z = center.z + radius * (float)Math.Sin(radian);
                var a = new Vector3(x, 0f, z);

                a.y = y == 0f ? spawnHeight ? GetSpawnHeight(a) : TerrainMeta.HeightMap.GetHeight(a) : y;

                if (a.y < -48f)
                {
                    a.y = -48f;
                }

                positions.Add(a);
                angle += next;
            }

            return positions;
        }

        private Elevation GetTerrainElevation(Vector3 center)
        {
            float maxY = -1000;
            float minY = 1000;

            foreach (var position in GetCircumferencePositions(center, 20f, 30f, true)) // 70 to 30 in 1.5.1
            {
                if (position.y > maxY) maxY = position.y;
                if (position.y < minY) minY = position.y;
            }

            return new Elevation
            {
                Min = minY,
                Max = maxY
            };
        }

        private bool IsFlatTerrain(Vector3 center, Elevation elevation, float value)
        {
            return elevation.Max - elevation.Min <= value && elevation.Max - center.y <= value;
        }

        private float GetMonumentFloat(string monumentName)
        {
            switch (monumentName)
            {
                case "Abandoned Cabins":
                    return 54f;
                case "Abandoned Supermarket":
                    return 50f;
                case "Airfield":
                    return 200f;
                case "Bandit Camp":
                    return 125f;
                case "Giant Excavator Pit":
                    return 225f;
                case "Harbor":
                    return 150f;
                case "HQM Quarry":
                    return 37.5f;
                case "Large Oil Rig":
                    return 200f;
                case "Launch Site":
                    return 300f;
                case "Lighthouse":
                    return 48f;
                case "Military Tunnel":
                    return 100f;
                case "Mining Outpost":
                    return 45f;
                case "Oil Rig":
                    return 100f;
                case "Outpost":
                    return 250f;
                case "Oxum's Gas Station":
                    return 65f;
                case "Power Plant":
                    return 140f;
                case "Satellite Dish":
                    return 90f;
                case "Sewer Branch":
                    return 100f;
                case "Stone Quarry":
                    return 27.5f;
                case "Sulfur Quarry":
                    return 27.5f;
                case "The Dome":
                    return 70f;
                case "Train Yard":
                    return 150f;
                case "Water Treatment Plant":
                    return 185f;
                case "Water Well":
                    return 24f;
                case "Wild Swamp":
                    return 24f;
            }

            return 300f;
        }

        private Vector3 SpawnRandomBase(out string message, RaidableType type, RaidableMode mode, string baseName = null, bool isAdmin = false, BasePlayer owner = null)
        {
            lastSpawnRequestTime = Time.realtimeSinceStartup;

            var building = GetBuilding(type, mode, baseName);
            bool flag = IsBuildingValid(building);

            if (flag)
            {
                bool checkTerrain;
                var spawns = GetSpawns(type, out checkTerrain);

                if (spawns != null)
                {
                    var eventPos = GetEventPosition(building.Value, owner, _config.Settings.Buyable.DistanceToSpawnFrom, checkTerrain, spawns, type);

                    if (eventPos != Vector3.zero)
                    {
                        PasteBuilding(type, eventPos, building, owner);
                        message = "Success";
                        return eventPos;
                    }
                }
            }

            message = GetDebugMessage(mode, flag, isAdmin, owner?.UserIDString, building.Key, building.Value);
            return Vector3.zero;
        }

        private string GetDebugMessage(RaidableMode mode, bool flag, bool isAdmin, string id, string baseName, BuildingOptions options)
        {
            if (options != null)
            {
                if (!options.Enabled)
                {
                    return Backbone.GetMessage("Profile Not Enabled", id, baseName);
                }
                else if (options.Mode == RaidableMode.Disabled)
                {
                    return Backbone.GetMessage("Difficulty Not Configured", id, baseName);
                }
            }

            if (!flag)
            {
                if (!string.IsNullOrEmpty(baseName))
                {
                    if (!FileExists(baseName))
                    {
                        return Backbone.GetMessage("FileDoesNotExist", id);
                    }
                    else if (!Buildings.Profiles.ContainsKey(baseName))
                    {
                        return Backbone.GetMessage("BuildingNotConfigured", id);
                    }
                }

                if (mode == RaidableMode.Random)
                {
                    return Backbone.GetMessage("NoValidBuildingsConfigured", id);
                }
                else if (!IsDifficultyAvailable(mode, options?.AllowPVP ?? false))
                {
                    return Backbone.GetMessage(isAdmin ? "Difficulty Not Available Admin" : "Difficulty Not Available", id, (int)mode);
                }
                else return Backbone.GetMessage("NoValidBuildingsConfigured", id);
            }

            return Backbone.GetMessage("CannotFindPosition", id);
        }

        private RaidableSpawns GetSpawns(RaidableType type, out bool checkTerrain)
        {
            RaidableSpawns spawns;

            switch (type)
            {
                case RaidableType.Maintained:
                    {
                        if (raidSpawns.TryGetValue(RaidableType.Maintained, out spawns))
                        {
                            checkTerrain = false;
                            return spawns;
                        }
                        break;
                    }
                case RaidableType.Manual:
                    {
                        if (raidSpawns.TryGetValue(RaidableType.Manual, out spawns))
                        {
                            checkTerrain = false;
                            return spawns;
                        }
                        break;
                    }
                case RaidableType.Purchased:
                    {
                        if (raidSpawns.TryGetValue(RaidableType.Purchased, out spawns))
                        {
                            checkTerrain = false;
                            return spawns;
                        }
                        break;
                    }
                case RaidableType.Scheduled:
                    {
                        if (raidSpawns.TryGetValue(RaidableType.Scheduled, out spawns))
                        {
                            checkTerrain = false;
                            return spawns;
                        }
                        break;
                    }
            }

            checkTerrain = true;
            return raidSpawns.TryGetValue(RaidableType.Grid, out spawns) ? spawns : null;
        }

        private KeyValuePair<string, BuildingOptions> GetBuilding(RaidableType type, RaidableMode mode, string baseName)
        {
            var list = new List<KeyValuePair<string, BuildingOptions>>();
            bool isBaseNull = string.IsNullOrEmpty(baseName);

            foreach (var building in Buildings.Profiles)
            {
                if (MustExclude(type, building.Value.AllowPVP) || !IsBuildingAllowed(type, mode, building.Value.Mode, building.Value.AllowPVP))
                {
                    continue;
                }

                if (FileExists(building.Key) && Cycle.CanSpawn(type, mode, building.Key))
                {
                    if (isBaseNull)
                    {
                        list.Add(building);
                    }
                    else if (building.Key.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                    {
                        return building;
                    }
                }

                foreach (var extra in building.Value.AdditionalBases)
                {
                    if (!FileExists(extra.Key) || !Cycle.CanSpawn(type, mode, extra.Key))
                    {
                        continue;
                    }

                    var kvp = new KeyValuePair<string, BuildingOptions>(extra.Key, BuildingOptions.Clone(building.Value));
                    kvp.Value.PasteOptions = new List<PasteOption>(extra.Value);

                    if (isBaseNull)
                    {
                        list.Add(kvp);
                    }
                    else if (extra.Key.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                    {
                        return kvp;
                    }
                }
            }

            if (list.Count > 0)
            {
                return list.GetRandom();
            }

            return default(KeyValuePair<string, BuildingOptions>);
        }

        private static bool IsBuildingValid(KeyValuePair<string, BuildingOptions> building)
        {
            if (string.IsNullOrEmpty(building.Key) || building.Value == null)
            {
                return false;
            }

            return building.Value.Mode != RaidableMode.Disabled;
        }

        private RaidableMode GetRandomDifficulty(RaidableType type)
        {
            var list = new List<RaidableMode>();

            foreach (RaidableMode mode in Enum.GetValues(typeof(RaidableMode)))
            {
                if (!CanSpawnDifficultyToday(mode))
                {
                    continue;
                }

                int max = _config.Settings.Management.Amounts.Get(mode);

                if (max < 0 || max > 0 && RaidableBase.Get(mode) >= max)
                {
                    continue;
                }

                foreach (var options in Buildings.Profiles.Values)
                {
                    if (options.Mode == mode && !MustExclude(type, options.AllowPVP))
                    {
                        list.Add(mode);
                        break;
                    }
                }
            }

            if (list.Count > 0)
            {
                return list.GetRandom();
            }

            return RaidableMode.Random;
        }

        private static bool FileExists(string file)
        {
            if (!file.Contains(Path.DirectorySeparatorChar))
            {
                return Interface.Oxide.DataFileSystem.ExistsDatafile($"copypaste{Path.DirectorySeparatorChar}{file}");
            }

            return Interface.Oxide.DataFileSystem.ExistsDatafile(file);
        }

        private static bool IsBuildingAllowed(RaidableType type, RaidableMode requestedMode, RaidableMode buildingMode, bool allowPVP)
        {
            if (requestedMode != RaidableMode.Random && buildingMode != requestedMode)
            {
                return false;
            }

            switch (type)
            {
                case RaidableType.Purchased:
                    {
                        if (!CanSpawnDifficultyToday(buildingMode) || !_config.Settings.Buyable.BuyPVP && allowPVP)
                        {
                            return false;
                        }
                        break;
                    }
                case RaidableType.Maintained:
                case RaidableType.Scheduled:
                    {
                        if (!CanSpawnDifficultyToday(buildingMode))
                        {
                            return false;
                        }
                        break;
                    }
            }

            return true;
        }

        private static bool CanSpawnDifficultyToday(RaidableMode mode)
        {
            switch (DateTime.Now.DayOfWeek)
            {
                case DayOfWeek.Monday:
                    {
                        return mode == RaidableMode.Easy ? _config.Settings.Management.Easy.Monday : mode == RaidableMode.Medium ? _config.Settings.Management.Medium.Monday : mode == RaidableMode.Hard ? _config.Settings.Management.Hard.Monday : mode == RaidableMode.Expert ? _config.Settings.Management.Expert.Monday : mode == RaidableMode.Nightmare ? _config.Settings.Management.Nightmare.Monday : false;
                    }
                case DayOfWeek.Tuesday:
                    {
                        return mode == RaidableMode.Easy ? _config.Settings.Management.Easy.Tuesday : mode == RaidableMode.Medium ? _config.Settings.Management.Medium.Tuesday : mode == RaidableMode.Hard ? _config.Settings.Management.Hard.Tuesday : mode == RaidableMode.Expert ? _config.Settings.Management.Expert.Tuesday : mode == RaidableMode.Nightmare ? _config.Settings.Management.Nightmare.Tuesday : false;
                    }
                case DayOfWeek.Wednesday:
                    {
                        return mode == RaidableMode.Easy ? _config.Settings.Management.Easy.Wednesday : mode == RaidableMode.Medium ? _config.Settings.Management.Medium.Wednesday : mode == RaidableMode.Hard ? _config.Settings.Management.Hard.Wednesday : mode == RaidableMode.Expert ? _config.Settings.Management.Expert.Wednesday : mode == RaidableMode.Nightmare ? _config.Settings.Management.Nightmare.Wednesday : false;
                    }
                case DayOfWeek.Thursday:
                    {
                        return mode == RaidableMode.Easy ? _config.Settings.Management.Easy.Thursday : mode == RaidableMode.Medium ? _config.Settings.Management.Medium.Thursday : mode == RaidableMode.Hard ? _config.Settings.Management.Hard.Thursday : mode == RaidableMode.Expert ? _config.Settings.Management.Expert.Thursday : mode == RaidableMode.Nightmare ? _config.Settings.Management.Nightmare.Thursday : false;
                    }
                case DayOfWeek.Friday:
                    {
                        return mode == RaidableMode.Easy ? _config.Settings.Management.Easy.Friday : mode == RaidableMode.Medium ? _config.Settings.Management.Medium.Friday : mode == RaidableMode.Hard ? _config.Settings.Management.Hard.Friday : mode == RaidableMode.Expert ? _config.Settings.Management.Expert.Friday : mode == RaidableMode.Nightmare ? _config.Settings.Management.Nightmare.Friday : false;
                    }
                case DayOfWeek.Saturday:
                    {
                        return mode == RaidableMode.Easy ? _config.Settings.Management.Easy.Saturday : mode == RaidableMode.Medium ? _config.Settings.Management.Medium.Saturday : mode == RaidableMode.Hard ? _config.Settings.Management.Hard.Saturday : mode == RaidableMode.Expert ? _config.Settings.Management.Expert.Saturday : mode == RaidableMode.Nightmare ? _config.Settings.Management.Nightmare.Saturday : false;
                    }
                default:
                    {
                        return mode == RaidableMode.Easy ? _config.Settings.Management.Easy.Sunday : mode == RaidableMode.Medium ? _config.Settings.Management.Medium.Sunday : mode == RaidableMode.Hard ? _config.Settings.Management.Hard.Sunday : mode == RaidableMode.Expert ? _config.Settings.Management.Expert.Sunday : mode == RaidableMode.Nightmare ? _config.Settings.Management.Nightmare.Sunday : false;
                    }
            }
        }

        #endregion

        #region Commands


        [ConsoleCommand("ui_buyraid")]
        private void ccmdBuyRaid(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs())
            {
                return;
            }

            var player = arg.Player();

            if (player == null || player.IPlayer == null)
            {
                return;
            }

            if (arg.Args[0] == "closeui")
            {
                CuiHelper.DestroyUi(player, "Buyable_UI");
                return;
            }

            CommandBuyRaid(player.IPlayer, _config.Settings.BuyCommand, arg.Args);
        }

        private void CommandReloadConfig(IPlayer p, string command, string[] args)
        {
            if (p.IsServer || (p.Object as BasePlayer).IsAdmin)
            {
                p.Reply(Backbone.GetMessage("ReloadConfig", p.Id));
                LoadConfig();
                maintainedEnabled = _config.Settings.Maintained.Enabled;
                scheduledEnabled = _config.Settings.Schedule.Enabled;
                buyableEnabled = _config.Settings.Buyable.Max > 0;

                if (maintainCoroutine != null)
                {
                    StopMaintainCoroutine();
                    p.Reply(Backbone.GetMessage("ReloadMaintainCo", p.Id));
                }

                if (scheduleCoroutine != null)
                {
                    StopScheduleCoroutine();
                    p.Reply(Backbone.GetMessage("ReloadScheduleCo", p.Id));
                }

                p.Reply(Backbone.GetMessage("ReloadInit", p.Id));

                LoadData();
                Reinitialize();
                BlockZoneManagerZones();
                LoadSpawns();
                SetupGrid();
                LoadTables();
                LoadProfiles();
            }
        }

        private void CommandBuyRaid(IPlayer p, string command, string[] args)
        {
            var player = p.Object as BasePlayer;

            if (args.Length > 1 && args[1].IsSteamId())
            {
                ulong playerId;
                if (ulong.TryParse(args[1], out playerId))
                {
                    player = BasePlayer.FindByID(playerId);
                }
            }

            if (!IsValid(player))
            {
                p.Reply(args.Length > 1 ? Backbone.GetMessage("TargetNotFoundId", p.Id, args[1]) : Backbone.GetMessage("TargetNotFoundNoId", p.Id));
                return;
            }

            var buyer = p.Object as BasePlayer ?? player;

            if (args.Length == 0)
            {
                if (_config.UI.Buyable.Enabled) UI.CreateBuyableUI(player);
                else Backbone.Message(buyer, "BuySyntax", _config.Settings.BuyCommand);
                return;
            }

            if (!buyableEnabled)
            {
                Backbone.Message(buyer, "BuyRaidsDisabled");
                return;
            }

            if (CopyPaste == null || !CopyPaste.IsLoaded)
            {
                Backbone.Message(buyer, "LoadCopyPaste");
                return;
            }

            if (IsGridLoading)
            {
                Backbone.Message(buyer, "GridIsLoading");
                return;
            }

            if (RaidableBase.Get(RaidableType.Purchased) >= _config.Settings.Buyable.Max)
            {
                Backbone.Message(buyer, "Max Manual Events", _config.Settings.Buyable.Max);
                return;
            }

            string value = args[0].ToLower();
            RaidableMode mode = IsEasy(value) ? RaidableMode.Easy : IsMedium(value) ? RaidableMode.Medium : IsHard(value) ? RaidableMode.Hard : IsExpert(value) ? RaidableMode.Expert : IsNightmare(value) ? RaidableMode.Nightmare : RaidableMode.Random;

            if (!CanSpawnDifficultyToday(mode))
            {
                Backbone.Message(buyer, "BuyDifficultyNotAvailableToday", value);
                return;
            }

            if (!IsDifficultyAvailable(mode, false))
            {
                Backbone.Message(buyer, "BuyAnotherDifficulty", value);
                return;
            }

            if (!IsDifficultyAvailable(mode, true))
            {
                Backbone.Message(buyer, "BuyPVPRaidsDisabled");
                return;
            }

            if (!IsPasteAvailable)
            {
                Backbone.Message(buyer, "PasteOnCooldown");
                return;
            }

            string id = buyer.UserIDString;

            if (tryBuyCooldowns.Contains(id))
            {
                Backbone.Message(buyer, "BuyableAlreadyRequested");
                return;
            }

            if (ServerMgr.Instance.Restarting)
            {
                Backbone.Message(buyer, "BuyableServerRestarting");
                return;
            }

            if (SaveRestore.IsSaving)
            {
                Backbone.Message(buyer, "BuyableServerSaving");
                return;
            }

            if (RaidableBase.IsOwner(player))
            {
                Backbone.Message(buyer, "BuyableAlreadyOwner");
                return;
            }

            tryBuyCooldowns.Add(id);
            timer.Once(2f, () => tryBuyCooldowns.Remove(id));

            float cooldown;
            if (buyCooldowns.TryGetValue(id, out cooldown))
            {
                Backbone.Message(buyer, "BuyCooldown", cooldown - Time.realtimeSinceStartup);
                return;
            }

            bool flag = TryBuyRaidServerRewards(buyer, player, mode) || TryBuyRaidEconomics(buyer, player, mode);

            if (flag && (cooldown = _config.Settings.Buyable.Cooldowns.Get(player)) > 0)
            {
                buyCooldowns.Add(id, Time.realtimeSinceStartup + cooldown);
                timer.Once(cooldown, () => buyCooldowns.Remove(id));
            }

            CuiHelper.DestroyUi(player, "Buyable_UI");
        }

        private void CommandRaidHunter(IPlayer p, string command, string[] args)
        {
            var player = p.Object as BasePlayer;
            bool isAdmin = p.IsServer || player.IsAdmin;
            string arg = args.Length >= 1 ? args[0].ToLower() : string.Empty;

            switch (arg)
            {
                case "resettotal":
                    {
                        if (isAdmin)
                        {
                            foreach (var entry in storedData.Players)
                            {
                                entry.Value.TotalRaids = 0;
                            }

                            SaveData();
                        }

                        return;
                    }
                case "resettime":
                    {
                        if (isAdmin)
                        {
                            storedData.RaidTime = DateTime.MinValue.ToString();
                            SaveData();
                        }

                        return;
                    }
                case "wipe":
                    {
                        if (isAdmin)
                        {
                            wiped = true;
                            CheckForWipe();
                        }

                        return;
                    }
                case "grid":
                    {
                        if (player.IsValid() && (isAdmin || permission.UserHasPermission(player.UserIDString, drawPermission)))
                        {
                            ShowGrid(player);
                        }

                        return;
                    }
                case "ui":
                    {
                        CommandUI(p, command, args.Skip(1).ToArray());
                        return;
                    }
                case "ladder":
                case "lifetime":
                    {
                        ShowLadder(p, args);
                        return;
                    }
                case "count":
                    {
                        CountLoot(player);
                        return;
                    }
            }

            if (_config.UI.Enabled)
            {
                p.Reply(Backbone.GetMessage(_config.UI.Lockout.Enabled ? "UIHelpTextAll" : "UIHelpText", p.Id, command));
            }

            if (_config.RankedLadder.Enabled)
            {
                p.Reply(Backbone.GetMessage("Wins", p.Id, storedData.Players.ContainsKey(p.Id) ? storedData.Players[p.Id].Raids : 0, _config.Settings.HunterCommand));
            }

            if (Raids.Count == 0 && scheduledEnabled)
            {
                ShowNextScheduledEvent(p);
                return;
            }

            if (!player.IsValid())
            {
                return;
            }

            DrawRaidLocations(player, isAdmin || permission.UserHasPermission(player.UserIDString, drawPermission));
        }

        protected void CountLoot(BasePlayer player)
        {
            if (player.IsValid() && player.IsAdmin)
            {
                var raid = GetNearestBase(player.transform.position);

                if (raid == null)
                {
                    return;
                }

                int amount = 0;

                foreach (var x in raid._containers)
                {
                    amount = +x.inventory.itemList.Count;
                    player.SendConsoleCommand("ddraw.text", 15f, Color.yellow, x.transform.position, string.Format("{0} : {1}", x.inventory.itemList.Count, x.ShortPrefabName.Substring(0, 6)));
                }

                Player.Message(player, amount.ToString());
            }
        }

        protected void DrawRaidLocations(BasePlayer player, bool hasPerm)
        {
            if (!hasPerm)
            {
                foreach (var raid in Raids.Values)
                {
                    if (InRange(raid.Location, player.transform.position, 100f))
                    {
                        Player.Message(player, string.Format("{0} @ {1} ({2})", raid.BaseName, raid.Location, PositionToGrid(raid.Location)));
                    }
                }

                return;
            }

            bool isAdmin = player.IsAdmin;

            try
            {
                if (!isAdmin)
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    player.SendNetworkUpdateImmediate();
                }

                foreach (var raid in Raids.Values)
                {
                    int num = 0;

                    foreach (var t in BasePlayer.activePlayerList)
                    {
                        if (t.Distance(raid.Location) <= raid.Options.ProtectionRadius * 3f)
                        {
                            num++;
                        }
                    }

                    int distance = Mathf.CeilToInt(Vector3.Distance(player.transform.position, raid.Location));
                    string message = string.Format(lang.GetMessage("RaidMessage", this, player.UserIDString), distance, num);
                    string flag = Backbone.GetMessage(raid.AllowPVP ? "PVPFlag" : "PVEFlag", player.UserIDString);

                    player.SendConsoleCommand("ddraw.text", 15f, Color.yellow, raid.Location, string.Format("{0} : {1}{2} {3}", raid.BaseName, flag, raid.Mode(), message));

                    foreach (var target in raid.friends)
                    {
                        if (!target.IsValid())
                        {
                            continue;
                        }

                        player.SendConsoleCommand("ddraw.text", 15f, Color.yellow, target.transform.position, "Ally");
                    }

                    if (raid.owner.IsValid())
                    {
                        player.SendConsoleCommand("ddraw.text", 15f, Color.yellow, raid.owner.transform.position, "Owner");
                    }
                }
            }
            catch (Exception ex)
            {
                Puts(ex.StackTrace);
                Puts(ex.Message);
            }
            finally
            {
                if (!isAdmin)
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    player.SendNetworkUpdateImmediate();
                }
            }
        }

        protected void ShowNextScheduledEvent(IPlayer p)
        {
            string message;
            double time = GetRaidTime();

            if (BasePlayer.activePlayerList.Count < _config.Settings.Schedule.PlayerLimit)
            {
                message = Backbone.GetMessage("Not Enough Online", p.Id, _config.Settings.Schedule.PlayerLimit);
            }
            else message = FormatTime(time);

            p.Reply(Backbone.GetMessage("Next", p.Id, message));
        }

        protected void ShowLadder(IPlayer p, string[] args)
        {
            if (!_config.RankedLadder.Enabled)
            {
                return;
            }

            if (storedData.Players.Count == 0)
            {
                p.Reply(Backbone.GetMessage("Ladder Insufficient Players", p.Id));
                return;
            }

            if (args.Length == 2 && args[1].ToLower() == "resetme" && storedData.Players.ContainsKey(p.Id))
            {
                storedData.Players[p.Id].Raids = 0;
                return;
            }

            string key = args[0].ToLower();
            var ladder = GetLadder(key);
            int rank = 0;

            ladder.Sort((x, y) => y.Value.CompareTo(x.Value));

            p.Reply(Backbone.GetMessage(key == "ladder" ? "Ladder" : "Ladder Total", p.Id));

            foreach (var kvp in ladder)
            {
                if (++rank >= 10)
                {
                    break;
                }

                NotifyPlayer(p, rank, kvp);
            }

            ladder.Clear();
        }

        protected void ShowGrid(BasePlayer player)
        {
            bool isAdmin = player.IsAdmin;

            try
            {
                if (!isAdmin)
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    player.SendNetworkUpdateImmediate();
                }

                foreach (var rsl in raidSpawns[RaidableType.Grid].Active)
                {
                    if (InRange(rsl.Location, player.transform.position, 1000f))
                    {
                        player.SendConsoleCommand("ddraw.text", 30f, Color.green, rsl.Location, "X");
                    }
                }

                foreach (var rsl in raidSpawns[RaidableType.Grid].Inactive)
                {
                    if (InRange(rsl.Location, player.transform.position, 1000f))
                    {
                        player.SendConsoleCommand("ddraw.text", 30f, Color.red, rsl.Location, "X");
                    }
                }

                foreach (var monument in monuments)
                {
                    string text = monument.Key.displayPhrase.translated;

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        text = GetMonumentName(monument);
                    }

                    player.SendConsoleCommand("ddraw.sphere", 30f, Color.blue, monument.Key.transform.position, monument.Value);
                    player.SendConsoleCommand("ddraw.text", 30f, Color.cyan, monument.Key.transform.position, text);
                }
            }
            catch (Exception ex)
            {
                Puts(ex.StackTrace);
                Puts(ex.Message);
            }
            finally
            {
                if (!isAdmin)
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    player.SendNetworkUpdateImmediate();
                }
            }
        }

        protected List<KeyValuePair<string, int>> GetLadder(string arg)
        {
            var ladder = new List<KeyValuePair<string, int>>();
            bool isLadder = arg.ToLower() == "ladder";

            foreach (var entry in storedData.Players)
            {
                int value = isLadder ? entry.Value.Raids : entry.Value.TotalRaids;

                if (value > 0)
                {
                    ladder.Add(new KeyValuePair<string, int>(entry.Key, value));
                }
            }

            return ladder;
        }

        private void NotifyPlayer(IPlayer p, int rank, KeyValuePair<string, int> kvp)
        {
            string name = covalence.Players.FindPlayerById(kvp.Key)?.Name ?? kvp.Key;
            string value = kvp.Value.ToString("N0");
            string message = lang.GetMessage("NotifyPlayerMessageFormat", this, p.Id);

            message = message.Replace("{rank}", rank.ToString());
            message = message.Replace("{name}", name);
            message = message.Replace("{value}", value);

            p.Reply(message);
        }

        protected string GetMonumentName(KeyValuePair<MonumentInfo, float> monument)
        {
            string text;
            if (monument.Key.name.Contains("Oilrig")) text = "Oil Rig";
            else if (monument.Key.name.Contains("cave")) text = "Cave";
            else if (monument.Key.name.Contains("power_sub")) text = "Power Sub Station";
            else text = "Unknown Monument";
            return text;
        }

        private void CommandRaidBase(IPlayer p, string command, string[] args)
        {
            var player = p.Object as BasePlayer;
            bool isAllowed = p.IsServer || player.IsAdmin || p.HasPermission(adminPermission);

            if (!CanCommandContinue(player, p, args, isAllowed))
            {
                return;
            }

            if (command == _config.Settings.EventCommand) // rbe
            {
                ProcessEventCommand(player, p, args, isAllowed);
            }
            else if (command == _config.Settings.ConsoleCommand) // rbevent
            {
                ProcessConsoleCommand(p, args, isAllowed);
            }
        }

        protected void ProcessEventCommand(BasePlayer player, IPlayer p, string[] args, bool isAllowed)
        {
            if (!isAllowed || !player.IsValid())
            {
                return;
            }

            RaidableMode mode = RaidableMode.Random;
            string baseName = null;

            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string value = args[i].ToLower();

                    if (IsEasy(value)) mode = RaidableMode.Easy;
                    else if (IsMedium(value)) mode = RaidableMode.Medium;
                    else if (IsHard(value)) mode = RaidableMode.Hard;
                    else if (IsExpert(value)) mode = RaidableMode.Expert;
                    else if (IsNightmare(value)) mode = RaidableMode.Nightmare;
                    else if (string.IsNullOrEmpty(baseName) && FileExists(args[i])) baseName = args[i];
                }
            }

            var building = GetBuilding(RaidableType.Manual, mode, baseName);

            if (IsBuildingValid(building))
            {
                RaycastHit hit;
                int layers = Layers.Mask.Construction | Layers.Mask.Default | Layers.Mask.Deployed | Layers.Mask.Tree | Layers.Mask.Terrain | Layers.Mask.Water | Layers.Mask.World;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, isAllowed ? Mathf.Infinity : 100f, layers, QueryTriggerInteraction.Ignore))
                {
                    var position = hit.point;
                    var safeRadius = Mathf.Max(building.Value.ArenaWalls.Radius, Radius * 2f);
                    var safe = IsAreaSafe(ref position, safeRadius, Layers.Mask.Player_Server | Layers.Mask.Construction | Layers.Mask.Deployed, RaidableType.Manual);

                    if (!safe && !player.IsFlying && InRange(player.transform.position, position, 50f, false))
                    {
                        p.Reply(Backbone.GetMessage("PasteIsBlockedStandAway", p.Id));
                        return;
                    }

                    if (safe && (isAllowed || !IsMonumentPosition(hit.point)))
                    {
                        PasteBuilding(RaidableType.Manual, hit.point, building, null);
                        if (player.IsAdmin) player.SendConsoleCommand("ddraw.text", 10f, Color.red, hit.point, "XXX");
                    }
                    else p.Reply(Backbone.GetMessage("PasteIsBlocked", p.Id));
                }
                else p.Reply(Backbone.GetMessage("LookElsewhere", p.Id));
            }
            else p.Reply(GetDebugMessage(mode, false, true, p.Id, building.Key, building.Value));
        }

        protected void ProcessConsoleCommand(IPlayer p, string[] args, bool isAdmin)
        {
            if (IsGridLoading)
            {
                p.Reply(GridIsLoadingMessage);
                return;
            }

            RaidableMode mode = RaidableMode.Random;
            string baseName = null;

            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string value = args[i].ToLower();

                    if (IsEasy(value)) mode = RaidableMode.Easy;
                    else if (IsMedium(value)) mode = RaidableMode.Medium;
                    else if (IsHard(value)) mode = RaidableMode.Hard;
                    else if (IsExpert(value)) mode = RaidableMode.Expert;
                    else if (IsNightmare(value)) mode = RaidableMode.Nightmare;
                    else if (string.IsNullOrEmpty(baseName) && FileExists(args[i])) baseName = args[i];
                }
            }

            string message;
            var position = SpawnRandomBase(out message, RaidableType.Manual, mode, baseName, isAdmin);

            if (position == Vector3.zero)
            {
                p.Reply(message);
            }
            else if (isAdmin && p.IsConnected)
            {
                p.Teleport(position.x, position.y, position.z);
            }
        }

        private bool CanCommandContinue(BasePlayer player, IPlayer p, string[] args, bool isAllowed)
        {
            if (HandledCommandArguments(player, p, isAllowed, args))
            {
                return false;
            }

            if (CopyPaste == null || !CopyPaste.IsLoaded)
            {
                p.Reply(Backbone.GetMessage("LoadCopyPaste", p.Id));
                return false;
            }

            if (!isAllowed && RaidableBase.Get(RaidableType.Manual) >= _config.Settings.Manual.Max)
            {
                p.Reply(Backbone.GetMessage("Max Manual Events", p.Id, _config.Settings.Manual.Max));
                return false;
            }

            if (!IsPasteAvailable)
            {
                p.Reply(Backbone.GetMessage("PasteOnCooldown", p.Id));
                return false;
            }

            if (IsSpawnOnCooldown())
            {
                p.Reply(Backbone.GetMessage("SpawnOnCooldown", p.Id));
                return false;
            }

            if (!isAllowed && BaseNetworkable.serverEntities.Count > 300000)
            {
                p.Reply(lang.GetMessage("EntityCountMax", this, p.Id));
                return false;
            }

            return true;
        }

        private bool HandledCommandArguments(BasePlayer player, IPlayer p, bool isAllowed, string[] args)
        {
            if (args.Length == 0)
            {
                return false;
            }

            if (player.IsValid())
            {
                if (!permission.UserHasPermission(player.UserIDString, drawPermission) && !isAllowed)
                {
                    return false;
                }

                if (args[0].ToLower() == "draw")
                {
                    bool isAdmin = player.IsAdmin;

                    try
                    {
                        if (!isAdmin)
                        {
                            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                            player.SendNetworkUpdateImmediate();
                        }

                        foreach (var raid in Raids.Values)
                        {
                            player.SendConsoleCommand("ddraw.sphere", 30f, Color.blue, raid.Location, raid.Options.ProtectionRadius);
                        }
                    }
                    catch (Exception ex)
                    {
                        Puts(ex.StackTrace);
                        Puts(ex.Message);
                    }
                    finally
                    {
                        if (!isAdmin)
                        {
                            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                            player.SendNetworkUpdateImmediate();
                        }
                    }

                    return true;
                }
            }

            if (!isAllowed)
            {
                return false;
            }

            switch (args[0].ToLower())
            {
                case "type":
                    {
                        List<string> list;

                        foreach (RaidableType type in Enum.GetValues(typeof(RaidableType)))
                        {
                            list = new List<string>();

                            foreach (var raid in Raids.Values)
                            {
                                if (raid.Type != type)
                                {
                                    continue;
                                }

                                list.Add(PositionToGrid(raid.Location));
                            }

                            if (list.Count == 0) continue;
                            p.Reply(string.Format("{0} : {1} @ {2}", type.ToString(), RaidableBase.Get(type), string.Join(", ", list.ToArray())));
                        }

                        return true;
                    }
                case "mode":
                    {
                        List<string> list;

                        foreach (RaidableMode mode in Enum.GetValues(typeof(RaidableMode)))
                        {
                            if (mode == RaidableMode.Disabled) continue;
                            list = new List<string>();

                            foreach (var raid in Raids.Values)
                            {
                                if (raid.Options.Mode != mode)
                                {
                                    continue;
                                }

                                list.Add(PositionToGrid(raid.Location));
                            }

                            if (list.Count == 0) continue;
                            p.Reply(string.Format("{0} : {1} @ {2}", mode.ToString(), RaidableBase.Get(mode), string.Join(", ", list.ToArray())));
                        }

                        return true;
                    }
                case "resetmarkers":
                    {
                        RemoveAllThirdPartyMarkers();

                        foreach (var raid in Raids.Values)
                        {
                            raid.RemoveMapMarkers();
                            raid.UpdateMarker();
                        }

                        return true;
                    }
                case "despawn":
                    {
                        if (IsValid(player))
                        {
                            bool success = DespawnBase(player.transform.position);
                            Backbone.Message(player, success ? "DespawnBaseSuccess" : "DespawnBaseNoneAvailable");
                            if (success) Puts(Backbone.GetMessage("DespawnedAt", null, player.displayName, FormatGridReference(player.transform.position)));
                            return true;
                        }

                        break;
                    }
                case "despawnall":
                    {
                        if (Raids.Count > 0)
                        {
                            DespawnAllBasesNow();
                            Puts(Backbone.GetMessage("DespawnedAll", null, player?.displayName ?? p.Id));
                        }

                        return true;
                    }
                case "expire":
                case "resetcooldown":
                    {
                        if (args.Length >= 2)
                        {
                            var target = RustCore.FindPlayer(args[1]);

                            if (target.IsValid())
                            {
                                buyCooldowns.Remove(target.UserIDString);
                                storedData.Lockouts.Remove(target.UserIDString);
                                SaveData();
                                p.Reply(Backbone.GetMessage("RemovedLockFor", p.Id, target.displayName, target.UserIDString));
                            }
                        }

                        return true;
                    }
                case "expireall":
                case "resetall":
                case "resetallcooldowns":
                    {
                        buyCooldowns.Clear();
                        storedData.Lockouts.Clear();
                        SaveData();
                        Puts($"All cooldowns and lockouts have been reset by {p.Name} ({p.Id})");
                        return true;
                    }
                case "setowner":
                case "lockraid":
                    {
                        if (args.Length >= 2)
                        {
                            var target = RustCore.FindPlayer(args[1]);

                            if (IsValid(target))
                            {
                                var raid = GetNearestBase(target.transform.position);

                                if (raid == null)
                                {
                                    p.Reply(Backbone.GetMessage("TargetTooFar", p.Id));
                                }
                                else
                                {
                                    raid.TrySetPayLock(target);
                                    p.Reply(Backbone.GetMessage("RaidLockedTo", p.Id, target.displayName));
                                }
                            }
                            else p.Reply(Backbone.GetMessage("TargetNotFoundId", p.Id, args[1]));
                        }

                        return true;
                    }
                case "clearowner":
                    {
                        if (!player.IsValid()) return true;

                        var raid = GetNearestBase(player.transform.position);

                        if (raid == null)
                        {
                            p.Reply(Backbone.GetMessage("TooFar", p.Id));
                        }
                        else
                        {
                            raid.TrySetPayLock(null);
                            p.Reply(Backbone.GetMessage("RaidOwnerCleared", p.Id));
                        }

                        return true;
                    }
            }

            return false;
        }

        private void CommandToggle(IPlayer p, string command, string[] args)
        {
            if (_config.Settings.Maintained.Enabled)
            {
                maintainedEnabled = !maintainedEnabled;
                p.Reply($"Toggled maintained events {(maintainedEnabled ? "on" : "off")}");
            }

            if (_config.Settings.Schedule.Enabled)
            {
                scheduledEnabled = !scheduledEnabled;
                p.Reply($"Toggled scheduled events {(scheduledEnabled ? "on" : "off")}");
            }

            if (_config.Settings.Buyable.Max > 0)
            {
                buyableEnabled = !buyableEnabled;
                p.Reply($"Toggled buyable events {(buyableEnabled ? "on" : "off")}");
            }
        }

        private void CommandPopulate(IPlayer p, string command, string[] args)
        {
            if (args.Length == 0)
            {
                p.Reply("Valid arguments: easy medium hard expert nightmare loot all");
                p.Reply("Valid arguments: 0 1 2 3 4 loot all");
                return;
            }

            var list = new List<TreasureItem>();

            foreach (var def in ItemManager.GetItemDefinitions())
            {
                list.Add(new TreasureItem
                {
                    shortname = def.shortname,
                });
            }

            list.Sort((x, y) => x.shortname.CompareTo(y.shortname));

            foreach (var str in args)
            {
                string arg = str.ToLower();

                if (IsEasy(arg) || arg == "all")
                {
                    AddToList(LootType.Easy, list);
                    p.Reply("Saved to `Loot (Easy Difficulty)`");
                }

                if (IsMedium(arg) || arg == "all")
                {
                    AddToList(LootType.Medium, list);
                    p.Reply("Saved to `Loot (Medium Difficulty)`");
                }

                if (IsHard(arg) || arg == "all")
                {
                    AddToList(LootType.Hard, list);
                    p.Reply("Saved to `Loot (Hard Difficulty)`");
                }

                if (IsExpert(arg) || arg == "all")
                {
                    AddToList(LootType.Expert, list);
                    p.Reply("Saved to `Loot (Expert Difficulty)`");
                }

                if (IsNightmare(arg) || arg == "all")
                {
                    AddToList(LootType.Nightmare, list);
                    p.Reply("Saved to `Loot (Nightmare Difficulty)`");
                }

                if (arg == "loot" || arg == "default" || arg == "all")
                {
                    AddToList(LootType.Default, list);
                    p.Reply("Saved to `Default`");
                }
            }

            SaveConfig();
        }

        private void CommandConfig(IPlayer p, string command, string[] args)
        {
            if (!IsValid(args))
            {
                p.Reply(string.Format(lang.GetMessage("ConfigUseFormat", this, p.Id), string.Join("|", arguments.ToArray())));
                return;
            }

            switch (args[0].ToLower())
            {
                case "add":
                    {
                        ConfigAddBase(p, args);
                        return;
                    }
                case "remove":
                    {
                        ConfigRemoveBase(p, args);
                        return;
                    }
                case "list":
                    {
                        ConfigListBases(p);
                        return;
                    }
            }
        }

        #endregion Commands

        #region Helpers

        private void RemoveElectricalConnectionReferences(IOEntity io)
        {
            var ios = Pool.GetList<uint>();

            foreach (var connection in ElectricalConnections)
            {
                if (connection.Value == null || connection.Value == io)
                {
                    ios.Add(connection.Key);
                }
            }

            foreach (uint key in ios)
            {
                ElectricalConnections.Remove(key);
            }

            ios.Clear();
            Pool.Free(ref ios);
        }

        private void AddToList(LootType lootType, List<TreasureItem> source)
        {
            List<TreasureItem> lootList;
            if (!Buildings.DifficultyLoot.TryGetValue(lootType, out lootList))
            {
                Buildings.DifficultyLoot[lootType] = lootList = new List<TreasureItem>();
            }

            foreach (var ti in source)
            {
                if (!lootList.Any(x => x.shortname == ti.shortname))
                {
                    lootList.Add(ti);
                }
            }

            string file = $"{Name}{Path.DirectorySeparatorChar}Editable_Lists{Path.DirectorySeparatorChar}{lootType}";
            Interface.Oxide.DataFileSystem.WriteObject(file, lootList);
        }

        private static bool IsPVE() => Backbone.Plugin.TruePVE != null || Backbone.Plugin.NextGenPVE != null || Backbone.Plugin.Imperium != null;

        private static bool IsEasy(string value) => value == "0" || value == "easy" || value == Backbone.Easy;

        private static bool IsMedium(string value) => value == "1" || value == "med" || value == "medium" || value == Backbone.Medium;

        private static bool IsHard(string value) => value == "2" || value == "hard" || value == Backbone.Hard;

        private static bool IsExpert(string value) => value == "3" || value == "expert" || value == Backbone.Expert;

        private static bool IsNightmare(string value) => value == "4" || value == "nm" || value == "nightmare" || value == Backbone.Nightmare;

        private void UpdateUI()
        {
            if (!_config.UI.Enabled)
            {
                return;
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                UI.Update(player);
            }
        }

        private bool IsInvisible(BasePlayer player)
        {
            if (!player || Vanish == null || !Vanish.IsLoaded)
            {
                return false;
            }

            var success = Vanish?.Call("IsInvisible", player);

            return success is bool ? (bool)success : false;
        }

        private static void NullifyDamage(HitInfo hitInfo, BaseCombatEntity entity = null)
        {
            if (hitInfo == null)
            {
                return;
            }

            if (entity.IsValid())
            {
                var total = hitInfo.damageTypes?.Total() ?? entity.MaxHealth();

                entity.Invoke(() =>
                {
                    if (!entity.IsDestroyed)
                    {
                        entity.Heal(total);
                    }
                }, 1f);
            }

            hitInfo.damageTypes = new DamageTypeList();
            hitInfo.DidHit = false;
            hitInfo.DoHitEffects = false;
            hitInfo.HitEntity = null;
        }

        public static bool MustExclude(RaidableType type, bool allowPVP)
        {
            if (!_config.Settings.Maintained.IncludePVE && type == RaidableType.Maintained && !allowPVP)
            {
                return true;
            }

            if (!_config.Settings.Maintained.IncludePVP && type == RaidableType.Maintained && allowPVP)
            {
                return true;
            }

            if (!_config.Settings.Schedule.IncludePVE && type == RaidableType.Scheduled && !allowPVP)
            {
                return true;
            }

            if (!_config.Settings.Schedule.IncludePVP && type == RaidableType.Scheduled && allowPVP)
            {
                return true;
            }

            return false;
        }

        private static List<BasePlayer> GetMountedPlayers(BaseMountable m)
        {
            var players = new List<BasePlayer>();

            if (m is BaseVehicle)
            {
                var vehicle = m as BaseVehicle;

                foreach (var mp in vehicle.mountPoints)
                {
                    if (mp.mountable.IsValid() && mp.mountable.GetMounted().IsValid())
                    {
                        players.Add(mp.mountable.GetMounted());
                    }
                }
            }
            else if (m.GetMounted().IsValid())
            {
                players.Add(m.GetMounted());
            }

            return players;
        }

        private bool AnyNpcs()
        {
            foreach (var x in Raids.Values)
            {
                if (x.npcs.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void DestroyComponents()
        {
            foreach (var raid in Raids.Values)
            {
                raid.DestroyFire();
                raid.DestroyInputs();
            }
        }

        private string GridIsLoadingMessage
        {
            get
            {
                int count = raidSpawns.ContainsKey(RaidableType.Grid) ? raidSpawns[RaidableType.Grid].Count : 0;
                return Backbone.GetMessage("GridIsLoadingFormatted", null, (Time.realtimeSinceStartup - gridTime).ToString("N02"), count);
            }
        }

        private void ConfigAddBase(IPlayer p, string[] args)
        {
            if (args.Length < 2)
            {
                p.Reply(lang.GetMessage("ConfigAddBaseSyntax", this, p.Id));
                return;
            }

            _sb.Length = 0;
            var values = new List<string>(args);
            values.RemoveAt(0);
            string value = values[0];
            RaidableMode mode = RaidableMode.Random;

            if (args.Length > 2)
            {
                foreach (string s in values)
                {
                    string str = s.ToLower();

                    if (IsEasy(str))
                    {
                        mode = RaidableMode.Easy;
                        values.Remove(s);
                        break;
                    }
                    else if (IsMedium(str))
                    {
                        mode = RaidableMode.Medium;
                        values.Remove(s);
                        break;
                    }
                    else if (IsHard(str))
                    {
                        mode = RaidableMode.Hard;
                        values.Remove(s);
                        break;
                    }
                    else if (IsExpert(str))
                    {
                        mode = RaidableMode.Expert;
                        values.Remove(s);
                        break;
                    }
                    else if (IsNightmare(str))
                    {
                        mode = RaidableMode.Nightmare;
                        values.Remove(s);
                        break;
                    }
                }
            }

            p.Reply(string.Format(lang.GetMessage("Adding", this, p.Id), string.Join(" ", values.ToArray())));

            BuildingOptions options;
            if (!Buildings.Profiles.TryGetValue(value, out options))
            {
                Buildings.Profiles[value] = options = new BuildingOptions();
                _sb.AppendLine(string.Format(lang.GetMessage("AddedPrimaryBase", this, p.Id), value));
                options.AdditionalBases = new Dictionary<string, List<PasteOption>>();
            }

            if (IsModeValid(mode) && options.Mode != mode)
            {
                options.Mode = mode;
                _sb.AppendLine(string.Format(lang.GetMessage("DifficultySetTo", this, p.Id), (int)mode));
            }

            if (args.Length >= 3)
            {
                values.RemoveAt(0);

                foreach (string ab in values)
                {
                    if (!options.AdditionalBases.ContainsKey(ab))
                    {
                        options.AdditionalBases.Add(ab, DefaultPasteOptions);
                        _sb.AppendLine(string.Format(lang.GetMessage("AddedAdditionalBase", this, p.Id), ab));
                    }
                }
            }

            if (_sb.Length > 0)
            {
                p.Reply(_sb.ToString());
                _sb.Length = 0;
                options.Enabled = true;
                SaveProfile(new KeyValuePair<string, BuildingOptions>(value, options));
                Buildings.Profiles[value] = options;
            }
            else p.Reply(lang.GetMessage("EntryAlreadyExists", this, p.Id));

            values.Clear();
        }

        private void ConfigRemoveBase(IPlayer p, string[] args)
        {
            if (args.Length < 2)
            {
                p.Reply(lang.GetMessage("RemoveSyntax", this, p.Id));
                return;
            }

            int num = 0;
            var dict = new Dictionary<string, BuildingOptions>(Buildings.Profiles);
            var array = args.Skip(1).ToArray();

            _sb.Length = 0;
            _sb.AppendLine(string.Format(lang.GetMessage("RemovingAllBasesFor", this, p.Id), string.Join(" ", array)));

            foreach (var entry in dict)
            {
                var list = new List<KeyValuePair<string, List<PasteOption>>>(entry.Value.AdditionalBases);

                foreach (string value in array)
                {
                    foreach (var ab in list)
                    {
                        if (ab.Key == value || entry.Key == value)
                        {
                            _sb.AppendLine(string.Format(lang.GetMessage("RemovedAdditionalBase", this, p.Id), ab.Key, entry.Key));
                            entry.Value.AdditionalBases.Remove(ab.Key);
                            num++;
                            SaveProfile(entry);
                        }
                    }

                    if (entry.Key == value)
                    {
                        _sb.AppendLine(string.Format(lang.GetMessage("RemovedPrimaryBase", this, p.Id), value));
                        Buildings.Profiles.Remove(entry.Key);
                        entry.Value.Enabled = false;
                        num++;
                        SaveProfile(entry);
                    }
                }

                list.Clear();
            }

            _sb.AppendLine(string.Format(lang.GetMessage("RemovedEntries", this, p.Id), num));
            p.Reply(_sb.ToString());
            _sb.Length = 0;
        }

        private void ConfigListBases(IPlayer p)
        {
            _sb.Length = 0;
            _sb.Append(lang.GetMessage("ListingAll", this, p.Id));
            _sb.AppendLine();

            bool buyable = false;
            bool validBase = false;

            foreach (var entry in Buildings.Profiles)
            {
                if (!entry.Value.AllowPVP)
                {
                    buyable = true;
                }

                _sb.AppendLine(lang.GetMessage("PrimaryBase", this, p.Id));

                if (FileExists(entry.Key))
                {
                    _sb.AppendLine(entry.Key);
                    validBase = true;
                }
                else _sb.Append(entry.Key).Append(lang.GetMessage("FileDoesNotExist", this, p.Id));

                if (entry.Value.AdditionalBases.Count > 0)
                {
                    _sb.AppendLine(lang.GetMessage("AdditionalBase", this, p.Id));

                    foreach (var ab in entry.Value.AdditionalBases)
                    {
                        if (FileExists(ab.Key))
                        {
                            _sb.AppendLine(ab.Key);
                            validBase = true;
                        }
                        else _sb.Append(ab.Key).Append((lang.GetMessage("FileDoesNotExist", this, p.Id)));
                    }
                }
            }

            if (!buyable && !_config.Settings.Buyable.BuyPVP)
            {
                _sb.AppendLine(lang.GetMessage("RaidPVEWarning", this, p.Id));
            }

            if (!validBase)
            {
                _sb.AppendLine(lang.GetMessage("NoValidBuildingsConfigured", this, p.Id));
            }

            p.Reply(_sb.ToString());
            _sb.Length = 0;
        }

        private readonly List<string> arguments = new List<string>
        {
            "add", "remove", "list"
        };

        private static bool IsValid(BaseEntity e)
        {
            if (e == null || e.net == null || e.IsDestroyed || e.transform == null)
            {
                return false;
            }

            return true;
        }

        private bool IsValid(Item item)
        {
            if (item == null || !item.IsValid() || item.isBroken)
            {
                return false;
            }

            return true;
        }

        private bool IsValid(string[] args)
        {
            return args.Length > 0 && arguments.Contains(args[0]);
        }

        private void DropOrRemoveItems(StorageContainer container, bool isOpened = true)
        {
            if (!isOpened || RaidableBase.IsProtectedWeapon(container) || (container is BuildingPrivlidge && !_config.Settings.Management.AllowCupboardLoot))
            {
                container.inventory.Clear();
            }
            else
            {
                var dropPos = container.WorldSpaceBounds().ToBounds().center;

                RaycastHit hit;
                if (Physics.Raycast(container.transform.position, Vector3.up, out hit, 5f, Layers.Mask.World | Layers.Mask.Construction, QueryTriggerInteraction.Ignore))
                {
                    dropPos.y = hit.point.y - 0.3f;
                }

                container.inventory.Drop(StringPool.Get(545786656), dropPos, container.transform.rotation);
            }

            container.Invoke(container.KillMessage, 0.1f);
        }

        private bool IsSpawnOnCooldown()
        {
            if (Time.realtimeSinceStartup - lastSpawnRequestTime < 2f)
            {
                return true;
            }

            lastSpawnRequestTime = Time.realtimeSinceStartup;
            return false;
        }

        private bool DespawnBase(Vector3 target)
        {
            var raid = GetNearestBase(target);

            if (raid == null)
            {
                return false;
            }

            raid.Despawn();

            return true;
        }

        private static RaidableBase GetNearestBase(Vector3 target)
        {
            var values = new List<RaidableBase>();

            foreach (var x in Backbone.Plugin.Raids.Values)
            {
                if (InRange(x.Location, target, 100f))
                {
                    values.Add(x);
                }
            }

            int count = values.Count;

            if (count == 0)
            {
                return null;
            }

            if (count > 1)
            {
                values.Sort((a, b) => (a.Location - target).sqrMagnitude.CompareTo((b.Location - target).sqrMagnitude));
            }

            return values[0];
        }

        private void DespawnAllBasesNow()
        {
            if (!IsUnloading)
            {
                StartDespawnRoutine();
                return;
            }

            if (Interface.Oxide.IsShuttingDown)
            {
                return;
            }

            StartDespawnInvokes();
            DestroyAll();
        }

        private void DestroyAll()
        {
            foreach (var raid in Raids.Values.ToList())
            {
                Interface.CallHook("OnRaidableBaseDespawn", raid.Location, raid.spawnTime, raid.ID);
                Puts(Backbone.GetMessage("Destroyed Raid", null, raid.Location));
                if (raid.IsOpened) raid.AwardRaiders();
                raid.Despawn();
                UnityEngine.Object.Destroy(raid.gameObject);
            }
        }

        private void StartDespawnInvokes()
        {
            if (Bases.Count == 0)
            {
                return;
            }

            float num = 0f;

            foreach (var entry in Bases)
            {
                if (entry.Value == null || entry.Value.Count == 0)
                {
                    continue;
                }

                foreach (var e in entry.Value)
                {
                    if (e != null && !e.IsDestroyed)
                    {
                        if (e is StorageContainer)
                        {
                            (e as StorageContainer).dropChance = 0f;
                        }
                        else if (e is ContainerIOEntity)
                        {
                            (e as ContainerIOEntity).dropChance = 0f;
                        }

                        e.Invoke(() =>
                        {
                            if (!e.IsDestroyed)
                            {
                                e.KillMessage();
                            }
                        }, num += 0.002f);
                    }
                }
            }
        }

        private void StopDespawnCoroutine()
        {
            if (despawnCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(despawnCoroutine);
                despawnCoroutine = null;
            }
        }

        private void StartDespawnRoutine()
        {
            if (Raids.Count == 0)
            {
                return;
            }

            if (despawnCoroutine != null)
            {
                timer.Once(0.1f, () => StartDespawnRoutine());
                return;
            }

            despawnCoroutine = ServerMgr.Instance.StartCoroutine(DespawnCoroutine());
        }

        private IEnumerator DespawnCoroutine()
        {
            while (Raids.Count > 0)
            {
                var raid = Raids.ElementAt(0).Value;
                var baseIndex = raid.BaseIndex;
                var uid = raid.uid;
                var position = raid.Location;

                raid.Despawn();

                do
                {
                    yield return CoroutineEx.waitForSeconds(0.1f);
                } while (Bases.ContainsKey(baseIndex));

                Raids.Remove(uid);
                yield return CoroutineEx.waitForSeconds(0.1f);
                Interface.CallHook("OnRaidableBaseDespawned", position);
            }

            despawnCoroutine = null;
        }

        private bool IsTrueDamage(BaseEntity entity)
        {
            if (!entity.IsValid())
            {
                return false;
            }

            return entity.skinID == 1587601905 || Backbone.Path.TrueDamage.Contains(entity.prefabID) || RaidableBase.IsProtectedWeapon(entity) || entity is TeslaCoil || entity is FireBall || entity is BaseTrap;
        }

        private Vector3 GetCenterLocation(Vector3 position)
        {
            foreach (var raid in Raids.Values)
            {
                if (InRange(raid.Location, position, raid.Options.ProtectionRadius))
                {
                    return raid.Location;
                }
            }

            return Vector3.zero;
        }

        private bool EventTerritory(Vector3 position)
        {
            foreach (var raid in Raids.Values)
            {
                if (InRange(raid.Location, position, raid.Options.ProtectionRadius))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanBlockOutsideDamage(RaidableBase raid, BasePlayer attacker, bool isEnabled)
        {
            if (isEnabled)
            {
                float radius = Mathf.Max(raid.Options.ProtectionRadius, raid.Options.ArenaWalls.Radius, Radius);

                return !InRange(attacker.transform.position, raid.Location, radius, false);
            }

            return false;
        }

        private static bool InRange(Vector3 a, Vector3 b, float distance, bool ex = true)
        {
            if (!ex)
            {
                return (a - b).sqrMagnitude <= distance * distance;
            }

            return (new Vector3(a.x, 0f, a.z) - new Vector3(b.x, 0f, b.z)).sqrMagnitude <= distance * distance;
        }

        private void SetWorkshopIDs()
        {
            if (Rust.Workshop.Approved.All == null || Rust.Workshop.Approved.All.Count == 0)
            {
                timer.Once(1f, () => SetWorkshopIDs());
                return;
            }

            List<ulong> skins;
            HashSet<ulong> workshopSkins;

            foreach (var def in ItemManager.GetItemDefinitions())
            {
                skins = new List<ulong>();

                foreach (var asi in Rust.Workshop.Approved.All)
                {
                    if (asi.Value?.Name == def.shortname)
                    {
                        skins.Add(Convert.ToUInt64(asi.Value.WorkshopdId));
                    }
                }

                if (skins.Count == 0)
                {
                    continue;
                }

                if (!WorkshopSkins.TryGetValue(def.shortname, out workshopSkins))
                {
                    WorkshopSkins[def.shortname] = workshopSkins = new HashSet<ulong>();
                }

                foreach (ulong skin in skins)
                {
                    workshopSkins.Add(skin);
                }

                skins.Clear();
            }
        }

        private bool AssignTreasureHunters()
        {
            foreach (var target in covalence.Players.All)
            {
                if (target == null || string.IsNullOrEmpty(target.Id))
                    continue;

                if (permission.UserHasPermission(target.Id, rankLadderPermission))
                    permission.RevokeUserPermission(target.Id, rankLadderPermission);

                if (permission.UserHasGroup(target.Id, rankLadderGroup))
                    permission.RemoveUserGroup(target.Id, rankLadderGroup);
            }

            if (!_config.RankedLadder.Enabled)
                return true;

            var ladder = new List<KeyValuePair<string, int>>();

            foreach (var entry in storedData.Players)
            {
                if (entry.Value.Raids > 0)
                {
                    ladder.Add(new KeyValuePair<string, int>(entry.Key, entry.Value.Raids));
                }
            }

            ladder.Sort((x, y) => y.Value.CompareTo(x.Value));

            int permsGiven = 0;
            IPlayer p;

            for (int i = 0; i < ladder.Count; i++)
            {
                p = covalence.Players.FindPlayerById(ladder[i].Key);

                if (p == null || p.IsBanned || p.IsAdmin)
                    continue;

                permission.GrantUserPermission(p.Id, rankLadderPermission, this);
                permission.AddUserGroup(p.Id, rankLadderGroup);

                LogToFile("treasurehunters", DateTime.Now.ToString() + " : " + Backbone.GetMessage("Log Stolen", null, p.Name, p.Id, ladder[i].Value), this, true);
                Puts(Backbone.GetMessage("Log Granted", null, p.Name, p.Id, rankLadderPermission, rankLadderGroup));

                if (++permsGiven >= _config.RankedLadder.Amount)
                    break;
            }

            if (permsGiven > 0)
            {
                Puts(Backbone.GetMessage("Log Saved", null, "treasurehunters"));
            }

            return true;
        }

        private void AddMapPrivatePluginMarker(Vector3 position, int uid)
        {
            if (Map == null || !Map.IsLoaded)
            {
                return;
            }

            mapMarkers[uid] = new MapInfo { IconName = _config.LustyMap.IconName, Position = position, Url = _config.LustyMap.IconFile };
            Map?.Call("ApiAddPointUrl", _config.LustyMap.IconFile, _config.LustyMap.IconName, position);
        }

        private void RemoveMapPrivatePluginMarker(int uid)
        {
            if (Map == null || !Map.IsLoaded || !mapMarkers.ContainsKey(uid))
            {
                return;
            }

            var mapInfo = mapMarkers[uid];
            Map?.Call("ApiRemovePointUrl", mapInfo.Url, mapInfo.IconName, mapInfo.Position);
            mapMarkers.Remove(uid);
        }

        private void AddTemporaryLustyMarker(Vector3 pos, int uid)
        {
            if (LustyMap == null || !LustyMap.IsLoaded)
            {
                return;
            }

            string name = string.Format("{0}_{1}", _config.LustyMap.IconName, storedData.TotalEvents).ToLower();
            LustyMap?.Call("AddTemporaryMarker", pos.x, pos.z, name, _config.LustyMap.IconFile, _config.LustyMap.IconRotation);
            lustyMarkers[uid] = name;
        }

        private void RemoveTemporaryLustyMarker(int uid)
        {
            if (LustyMap == null || !LustyMap.IsLoaded || !lustyMarkers.ContainsKey(uid))
            {
                return;
            }

            LustyMap?.Call("RemoveTemporaryMarker", lustyMarkers[uid]);
            lustyMarkers.Remove(uid);
        }

        private void RemoveAllThirdPartyMarkers()
        {
            if (lustyMarkers.Count > 0)
            {
                var lusty = new Dictionary<int, string>(lustyMarkers);

                foreach (var entry in lusty)
                {
                    RemoveTemporaryLustyMarker(entry.Key);
                }

                lusty.Clear();
            }

            if (mapMarkers.Count > 0)
            {
                var maps = new Dictionary<int, MapInfo>(mapMarkers);

                foreach (var entry in maps)
                {
                    RemoveMapPrivatePluginMarker(entry.Key);
                }

                maps.Clear();
            }
        }

        private void StopMaintainCoroutine()
        {
            if (maintainCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(maintainCoroutine);
                maintainCoroutine = null;
            }
        }

        private void StartMaintainCoroutine()
        {
            if (!maintainedEnabled || _config.Settings.Maintained.Max <= 0)
            {
                return;
            }

            if (IsGridLoading)
            {
                timer.Once(1f, () => StartMaintainCoroutine());
                return;
            }

            StopMaintainCoroutine();

            timer.Once(0.2f, () =>
            {
                maintainCoroutine = ServerMgr.Instance.StartCoroutine(MaintainCoroutine());
            });
        }

        private IEnumerator MaintainCoroutine()
        {
            string message;
            RaidableMode mode;

            if (!CanContinueAutomation())
            {
                Puts(Backbone.GetMessage("MaintainCoroutineFailedToday"));
                yield break;
            }

            while (!IsUnloading)
            {
                if (!maintainedEnabled || SaveRestore.IsSaving)
                {
                    yield return CoroutineEx.waitForSeconds(15f);
                    continue;
                }

                if (!IsModeValid(mode = GetRandomDifficulty(RaidableType.Maintained)) || !CanMaintainOpenEvent() || CopyPaste == null || !CopyPaste.IsLoaded)
                {
                    yield return CoroutineEx.waitForSeconds(1f);
                    continue;
                }

                SpawnRandomBase(out message, RaidableType.Maintained, mode);
                yield return CoroutineEx.waitForSeconds(60f);
            }
        }

        private bool CanMaintainOpenEvent() => IsPasteAvailable && !IsGridLoading && _config.Settings.Maintained.Max > 0 && RaidableBase.Get(RaidableType.Maintained) < _config.Settings.Maintained.Max && BasePlayer.activePlayerList.Count >= _config.Settings.Maintained.PlayerLimit;

        private void StopScheduleCoroutine()
        {
            if (scheduleCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(scheduleCoroutine);
                scheduleCoroutine = null;
            }
        }

        private void StartScheduleCoroutine()
        {
            if (!scheduledEnabled || _config.Settings.Schedule.Max <= 0)
            {
                return;
            }

            if (IsGridLoading)
            {
                timer.Once(1f, () => StartScheduleCoroutine());
                return;
            }

            StopScheduleCoroutine();

            timer.Once(0.2f, () =>
            {
                scheduleCoroutine = ServerMgr.Instance.StartCoroutine(ScheduleCoroutine());
            });
        }

        private IEnumerator ScheduleCoroutine()
        {
            if (!CanContinueAutomation())
            {
                Puts(Backbone.GetMessage("ScheduleCoroutineFailedToday"));
                yield break;
            }

            double raidInterval = Core.Random.Range(_config.Settings.Schedule.IntervalMin, _config.Settings.Schedule.IntervalMax + 1);
            string message;
            RaidableMode mode;

            if (storedData.RaidTime == DateTime.MinValue.ToString()) // first time users
            {
                storedData.RaidTime = DateTime.Now.AddSeconds(raidInterval).ToString();
                Puts(Backbone.GetMessage("Next Automated Raid", null, FormatTime(raidInterval), DateTime.UtcNow.AddSeconds(raidInterval).ToString()));
                SaveData();
            }

            while (!IsUnloading)
            {
                if (CanScheduleOpenEvent() && CopyPaste != null && CopyPaste.IsLoaded)
                {
                    while (RaidableBase.Get(RaidableType.Scheduled) < _config.Settings.Schedule.Max && MaxOnce())
                    {
                        if (!scheduledEnabled || SaveRestore.IsSaving)
                        {
                            yield return CoroutineEx.waitForSeconds(15f);
                            continue;
                        }

                        if (IsModeValid(mode = GetRandomDifficulty(RaidableType.Scheduled)) && SpawnRandomBase(out message, RaidableType.Scheduled, mode) != Vector3.zero)
                        {
                            _maxOnce++;
                            yield return CoroutineEx.waitForSeconds(60f);
                            continue;
                        }

                        yield return CoroutineEx.waitForSeconds(1f);
                    }

                    _maxOnce = 0;
                    raidInterval = Core.Random.Range(_config.Settings.Schedule.IntervalMin, _config.Settings.Schedule.IntervalMax + 1);
                    storedData.RaidTime = DateTime.Now.AddSeconds(raidInterval).ToString();
                    Puts(Backbone.GetMessage("Next Automated Raid", null, FormatTime(raidInterval), DateTime.Now.AddSeconds(raidInterval)));
                    SaveData();
                }

                yield return CoroutineEx.waitForSeconds(1f);
            }
        }

        private bool MaxOnce()
        {
            return _config.Settings.Schedule.MaxOnce <= 0 || _maxOnce < _config.Settings.Schedule.MaxOnce;
        }

        private bool CanContinueAutomation()
        {
            foreach (RaidableMode mode in Enum.GetValues(typeof(RaidableMode)))
            {
                if (CanSpawnDifficultyToday(mode))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsModeValid(RaidableMode mode) => mode != RaidableMode.Disabled && mode != RaidableMode.Random;

        private double GetRaidTime() => DateTime.Parse(storedData.RaidTime).Subtract(DateTime.Now).TotalSeconds;

        private bool CanScheduleOpenEvent() => GetRaidTime() <= 0 && _config.Settings.Schedule.Max > 0 && RaidableBase.Get(RaidableType.Scheduled) < _config.Settings.Schedule.Max && IsPasteAvailable && !IsGridLoading && BasePlayer.activePlayerList.Count >= _config.Settings.Schedule.PlayerLimit;

        private void DoLockoutRemoves()
        {
            var keys = new List<string>();

            foreach (var lockout in storedData.Lockouts)
            {
                if (lockout.Value.Easy - Epoch.Current <= 0)
                {
                    lockout.Value.Easy = 0;
                }

                if (lockout.Value.Medium - Epoch.Current <= 0)
                {
                    lockout.Value.Medium = 0;
                }

                if (lockout.Value.Hard - Epoch.Current <= 0)
                {
                    lockout.Value.Hard = 0;
                }

                if (lockout.Value.Expert - Epoch.Current <= 0)
                {
                    lockout.Value.Expert = 0;
                }

                if (lockout.Value.Nightmare - Epoch.Current <= 0)
                {
                    lockout.Value.Nightmare = 0;
                }

                if (!lockout.Value.Any())
                {
                    keys.Add(lockout.Key);
                }
            }

            foreach (string key in keys)
            {
                storedData.Lockouts.Remove(key);
            }
        }

        private void SaveData()
        {
            DoLockoutRemoves();
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        public static string FormatGridReference(Vector3 position)
        {
            if (_config.Settings.ShowXZ)
            {
                return string.Format("{0} ({1} {2})", PositionToGrid(position), position.x.ToString("N2"), position.z.ToString("N2"));
            }

            return PositionToGrid(position);
        }

        /*public static string PositionToGrid(Vector3 position) // Credit: yetzt/Dana/nivex
        {
            var r = new Vector2(World.Size / 2 + position.x, World.Size / 2 + position.z);
            var f = Mathf.Floor(r.x / 148.4f);
            var x = f % 26;
            var c = Mathf.Floor(f / 26);
            var z = Mathf.Floor(World.Size / 148.4f) - Mathf.Floor(r.y / 148.4f);
            var s = c <= 0 ? string.Empty : ((char)('A' + (c - 1))).ToString();
            
            return $"{s}{(char)('A' + x)}{z}";
        }*/

        private static string PositionToGrid(Vector3 position) // Credit: MagicGridPanel
        {
            int maxGridSize = Mathf.FloorToInt(World.Size / 146.3f) - 1;
            int xGrid = Mathf.Clamp(Mathf.FloorToInt((position.x + (World.Size / 2f)) / 146.3f), 0, maxGridSize);
            string extraA = string.Empty;
            if (xGrid > 26) extraA = $"{(char)('A' + (xGrid / 26 - 1))}";
            return $"{extraA}{(char)('A' + xGrid % 26)}{Mathf.Clamp(maxGridSize - Mathf.FloorToInt((position.z + (World.Size / 2f)) / 146.3f), 0, maxGridSize)}";
        }

        private static string FormatTime(double seconds)
        {
            if (seconds < 0)
            {
                return "0s";
            }

            var ts = TimeSpan.FromSeconds(seconds);
            string format = Backbone.GetMessage("TimeFormat");

            if (format == "TimeFormat")
            {
                format = "{0:D2}h {1:D2}m {2:D2}s";
            }

            return string.Format(format, ts.Hours, ts.Minutes, ts.Seconds);
        }

        #endregion

        #region Data files

        private void CreateDefaultFiles()
        {
            string folder = $"{Name}{Path.DirectorySeparatorChar}Profiles";
            string empty = $"{folder}{Path.DirectorySeparatorChar}_emptyfile";

            if (Interface.Oxide.DataFileSystem.ExistsDatafile(empty))
            {
                return;
            }

            Interface.Oxide.DataFileSystem.GetDatafile(empty);

            foreach (var profile in DefaultBuildingOptions)
            {
                string filename = $"{Name}{Path.DirectorySeparatorChar}Profiles{Path.DirectorySeparatorChar}{profile.Key}";

                if (!Interface.Oxide.DataFileSystem.ExistsDatafile(filename))
                {
                    SaveProfile(profile);
                }
            }

            string lootFile = $"{Name}{Path.DirectorySeparatorChar}Default_Loot";

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(lootFile))
            {
                Interface.Oxide.DataFileSystem.WriteObject(lootFile, DefaultLoot);
            }
        }

        protected void LoadProfiles()
        {
            string folder = $"{Name}{Path.DirectorySeparatorChar}Profiles";
            string empty = $"{folder}{Path.DirectorySeparatorChar}_emptyfile";

            Interface.Oxide.DataFileSystem.GetDatafile(empty); // required to create the directory if it doesn't exist, otherwise GetFiles will throw an exception

            ConvertProfilesFromConfig();

            var files = Interface.Oxide.DataFileSystem.GetFiles(folder);

            foreach (string file in files)
            {
                //Puts(file);
                try
                {
                    if (file.EndsWith("_emptyfile.json"))
                    {
                        continue;
                    }

                    int index = file.LastIndexOf(Path.DirectorySeparatorChar) + 1;
                    string baseName = file.Substring(index, file.Length - index - 5);
                    string fullName = $"{folder}{Path.DirectorySeparatorChar}{baseName}";
                    var options = Interface.Oxide.DataFileSystem.ReadObject<BuildingOptions>(fullName);

                    if (options == null || !options.Enabled)
                    {
                        continue;
                    }

                    if (options.AdditionalBases == null)
                    {
                        options.AdditionalBases = new Dictionary<string, List<PasteOption>>();
                    }

                    Buildings.Profiles[baseName] = options;
                }
                catch (Exception ex)
                {
                    Puts("Profile {0} is corrupted!\n{1}", file, ex.Message);
                }
            }

            foreach (var profile in Buildings.Profiles)
            {
                SaveProfile(profile);
            }

            LoadBaseTables();
        }

        protected void SaveProfile(KeyValuePair<string, BuildingOptions> profile)
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}{Path.DirectorySeparatorChar}Profiles{Path.DirectorySeparatorChar}{profile.Key}", profile.Value);
        }

        protected void ConvertProfilesFromConfig()
        {
            if (_config.RaidableBases.Buildings.Count > 0)
            {
                CreateBackup();

                foreach (var profile in _config.RaidableBases.Buildings)
                {
                    SaveProfile(profile);
                }

                _config.RaidableBases.Buildings.Clear();
                SaveConfig();
            }
        }

        protected void LoadTables()
        {
            ConvertTablesFromConfig();
            Buildings = new BuildingTables();

            foreach (LootType lootType in Enum.GetValues(typeof(LootType)))
            {
                string file = lootType == LootType.Default ? $"{Name}{Path.DirectorySeparatorChar}Default_Loot" : $"{Name}{Path.DirectorySeparatorChar}Difficulty_Loot{Path.DirectorySeparatorChar}{lootType}";
                List<TreasureItem> lootList;

                Buildings.DifficultyLoot[lootType] = lootList = GetTable(file);

                if (lootList.Count > 0)
                {
                    Puts($"Loaded {lootList.Count} items from {file}");
                }
            }

            foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
            {
                string file = $"{Name}{Path.DirectorySeparatorChar}Weekday_Loot{Path.DirectorySeparatorChar}{day}";
                List<TreasureItem> lootList;

                Buildings.WeekdayLoot[day] = lootList = GetTable(file);

                if (lootList.Count > 0)
                {
                    Puts($"Loaded {lootList.Count} items from {file}");
                }
            }
        }

        private void LoadBaseTables()
        {
            foreach (var entry in Buildings.Profiles)
            {
                if (entry.Value == null || entry.Value.AdditionalBases == null)
                {
                    continue;
                }

                string file = $"{Name}{Path.DirectorySeparatorChar}Base_Loot{Path.DirectorySeparatorChar}{entry.Key}";
                var lootList = GetTable(file);

                Buildings.BaseLoot[entry.Key] = lootList;

                foreach (var extra in entry.Value.AdditionalBases)
                {
                    Buildings.BaseLoot[extra.Key] = lootList;
                }

                if (lootList.Count > 0)
                {
                    Puts($"Loaded {lootList.Count} items from {file}");
                }
            }
        }

        private List<TreasureItem> GetTable(string file)
        {
            var lootList = new List<TreasureItem>();

            try
            {
                //Puts(file);
                lootList = Interface.Oxide.DataFileSystem.ReadObject<List<TreasureItem>>(file);
            }
            catch
            {

            }

            if (lootList == null)
            {
                Interface.Oxide.DataFileSystem.WriteObject(file, lootList = new List<TreasureItem>());
            }

            return lootList;
        }

        protected void ConvertTablesFromConfig()
        {
            foreach (var building in _config.RaidableBases.Buildings)
            {
                ConvertFromConfig(building.Value.Loot, $"Base_Loot{Path.DirectorySeparatorChar}{building.Key}");
            }

            ConvertFromConfig(_config.Treasure.Loot, "Default_Loot");
            ConvertFromConfig(_config.Treasure.LootEasy, $"Difficulty_Loot{Path.DirectorySeparatorChar}Easy");
            ConvertFromConfig(_config.Treasure.LootMedium, $"Difficulty_Loot{Path.DirectorySeparatorChar}Medium");
            ConvertFromConfig(_config.Treasure.LootHard, $"Difficulty_Loot{Path.DirectorySeparatorChar}Hard");
            ConvertFromConfig(_config.Treasure.LootExpert, $"Difficulty_Loot{Path.DirectorySeparatorChar}Expert");
            ConvertFromConfig(_config.Treasure.LootNightmare, $"Difficulty_Loot{Path.DirectorySeparatorChar}Nightmare");
            ConvertFromConfig(_config.Treasure.DOWL_Monday, $"Weekday_Loot{Path.DirectorySeparatorChar}Monday");
            ConvertFromConfig(_config.Treasure.DOWL_Tuesday, $"Weekday_Loot{Path.DirectorySeparatorChar}Tuesday");
            ConvertFromConfig(_config.Treasure.DOWL_Wednesday, $"Weekday_Loot{Path.DirectorySeparatorChar}Wednesday");
            ConvertFromConfig(_config.Treasure.DOWL_Thursday, $"Weekday_Loot{Path.DirectorySeparatorChar}Thursday");
            ConvertFromConfig(_config.Treasure.DOWL_Friday, $"Weekday_Loot{Path.DirectorySeparatorChar}Friday");
            ConvertFromConfig(_config.Treasure.DOWL_Saturday, $"Weekday_Loot{Path.DirectorySeparatorChar}Saturday");
            ConvertFromConfig(_config.Treasure.DOWL_Sunday, $"Weekday_Loot{Path.DirectorySeparatorChar}Sunday");
            SaveConfig();
        }

        protected void ConvertFromConfig(List<TreasureItem> lootList, string key)
        {
            if (lootList.Count > 0)
            {
                CreateBackup();
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}{Path.DirectorySeparatorChar}{key}", lootList);
                lootList.Clear();
            }
        }

        private void CreateBackup()
        {
            if (!_createdBackup)
            {
                string file = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.backup_old_system.{DateTime.Now:yyyy-MM-dd hh-mm-ss}.json";
                Config.WriteObject(_config, false, file);
                Puts("Created config backup of old system: {0}", file);
                _createdBackup = true;
            }
        }

        private bool _createdBackup;

        #endregion

        #region Configuration

        private Dictionary<string, Dictionary<string, string>> GetMessages()
        {
            return new Dictionary<string, Dictionary<string, string>>
            {
                {"No Permission", new Dictionary<string, string>() {
                    {"en", "You do not have permission to use this command."},
                }},
                {"Building is blocked!", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>Building is blocked near raidable bases!</color>"},
                }},
                {"Profile Not Enabled", new Dictionary<string, string>() {
                    {"en", "This profile is not enabled: <color=#FF0000>{0}</color>."},
                }},
                {"Difficulty Not Configured", new Dictionary<string, string>() {
                    {"en", "Difficulty is not configured for the profile <color=#FF0000>{0}</color>."},
                }},
                {"Difficulty Not Available", new Dictionary<string, string>() {
                    {"en", "Difficulty <color=#FF0000>{0}</color> is not available on any of your buildings."},
                }},
                {"Difficulty Not Available Admin", new Dictionary<string, string>() {
                    {"en", "Difficulty <color=#FF0000>{0}</color> is not available on any of your buildings. This could indicate that your CopyPaste files are not on this server in the oxide/data/copypaste folder."},
                }},
                {"Max Manual Events", new Dictionary<string, string>() {
                    {"en", "Maximum number of manual events <color=#FF0000>{0}</color> has been reached!"},
                }},
                {"Manual Event Failed", new Dictionary<string, string>() {
                    {"en", "Event failed to start! Unable to obtain a valid position. Please try again."},
                }},
                {"Help", new Dictionary<string, string>() {
                    {"en", "/{0} <tp> - start a manual event, and teleport to the position if TP argument is specified and you are an admin."},
                }},
                {"RaidOpenMessage", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>A {0} raidable base event has opened at <color=#FFFF00>{1}</color>! You are <color=#FFA500>{2}m</color> away. [{3}]</color>"},
                }},
                {"Next", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>No events are open. Next event in <color=#FFFF00>{0}</color></color>"},
                }},
                {"Wins", new Dictionary<string, string>()
                {
                    {"en", "<color=#C0C0C0>You have looted <color=#FFFF00>{0}</color> raid bases! View the ladder using <color=#FFA500>/{1} ladder</color> or <color=#FFA500>/{1} lifetime</color></color>"},
                }},
                {"RaidMessage", new Dictionary<string, string>() {
                    {"en", "Raidable Base {0}m [{1} players]"},
                }},
                {"Ladder", new Dictionary<string, string>()
                {
                    {"en", "<color=#FFFF00>[ Top 10 Raid Hunters (This Wipe) ]</color>:"},
                }},
                {"Ladder Total", new Dictionary<string, string>()
                {
                    {"en", "<color=#FFFF00>[ Top 10 Raid Hunters (Lifetime) ]</color>:"},
                }},
                {"Ladder Insufficient Players", new Dictionary<string, string>()
                {
                    {"en", "<color=#FFFF00>No players are on the ladder yet!</color>"},
                }},
                {"Next Automated Raid", new Dictionary<string, string>() {
                    {"en", "Next automated raid in {0} at {1}"},
                }},
                {"Not Enough Online", new Dictionary<string, string>() {
                    {"en", "Not enough players online ({0} minimum)"},
                }},
                {"Raid Base Distance", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>Raidable Base <color=#FFA500>{0}m</color>"},
                }},
                {"Destroyed Raid", new Dictionary<string, string>() {
                    {"en", "Destroyed a left over raid base at {0}"},
                }},
                {"Indestructible", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>Treasure chests are indestructible!</color>"},
                }},
                {"View Config", new Dictionary<string, string>() {
                    {"en", "Please view the config if you haven't already."},
                }},
                {"Log Stolen", new Dictionary<string, string>() {
                    {"en", "{0} ({1}) Raids {2}"},
                }},
                {"Log Granted", new Dictionary<string, string>() {
                    {"en", "Granted {0} ({1}) permission {2} for group {3}"},
                }},
                {"Log Saved", new Dictionary<string, string>() {
                    {"en", "Raid Hunters have been logged to: {0}"},
                }},
                {"Prefix", new Dictionary<string, string>() {
                    {"en", "[ <color=#406B35>Raidable Bases</color> ] "},
                }},
                {"RestartDetected", new Dictionary<string, string>()
                {
                    {"en", "Restart detected. Next event in {0} minutes."},
                }},
                {"EconomicsDeposit", new Dictionary<string, string>()
                {
                    {"en", "You have received <color=#FFFF00>${0}</color> for stealing the treasure!"},
                }},
                {"EconomicsWithdraw", new Dictionary<string, string>()
                {
                    {"en", "You have paid <color=#FFFF00>${0}</color> for a raidable base!"},
                }},
                {"EconomicsWithdrawGift", new Dictionary<string, string>()
                {
                    {"en", "{0} has paid <color=#FFFF00>${1}</color> for your raidable base!"},
                }},
                {"EconomicsWithdrawFailed", new Dictionary<string, string>()
                {
                    {"en", "You do not have <color=#FFFF00>${0}</color> for a raidable base!"},
                }},
                {"ServerRewardPoints", new Dictionary<string, string>()
                {
                    {"en", "You have received <color=#FFFF00>{0} RP</color> for stealing the treasure!"},
                }},
                {"ServerRewardPointsTaken", new Dictionary<string, string>()
                {
                    {"en", "You have paid <color=#FFFF00>{0} RP</color> for a raidable base!"},
                }},
                {"ServerRewardPointsGift", new Dictionary<string, string>()
                {
                    {"en", "{0} has paid <color=#FFFF00>{1} RP</color> for your raidable base!"},
                }},
                {"ServerRewardPointsFailed", new Dictionary<string, string>()
                {
                    {"en", "You do not have <color=#FFFF00>{0} RP</color> for a raidable base!"},
                }},
                {"InvalidItem", new Dictionary<string, string>()
                {
                    {"en", "Invalid item shortname: {0}. Use /{1} additem <shortname> <amount> [skin]"},
                }},
                {"AddedItem", new Dictionary<string, string>()
                {
                    {"en", "Added item: {0} amount: {1}, skin: {2}"},
                }},
                {"CustomPositionSet", new Dictionary<string, string>()
                {
                    {"en", "Custom event spawn location set to: {0}"},
                }},
                {"CustomPositionRemoved", new Dictionary<string, string>()
                {
                    {"en", "Custom event spawn location removed."},
                }},
                {"OpenedEvents", new Dictionary<string, string>()
                {
                    {"en", "Opened {0}/{1} events."},
                }},
                {"OnPlayerEntered", new Dictionary<string, string>()
                {
                    {"en", "<color=#FF0000>You have entered a raidable PVP base!</color>"},
                }},
                {"OnPlayerEnteredPVE", new Dictionary<string, string>()
                {
                    {"en", "<color=#FF0000>You have entered a raidable PVE base!</color>"},
                }},
                {"OnFirstPlayerEntered", new Dictionary<string, string>()
                {
                    {"en", "<color=#FFFF00>{0}</color> is the first to enter the raidable base at <color=#FFFF00>{1}</color>"},
                }},
                {"OnChestOpened", new Dictionary<string, string>() {
                    {"en", "<color=#FFFF00>{0}</color> is the first to see the treasures at <color=#FFFF00>{1}</color>!</color>"},
                }},
                {"OnRaidFinished", new Dictionary<string, string>() {
                    {"en", "The raid at <color=#FFFF00>{0}</color> has been unlocked!"},
                }},
                {"CannotBeMounted", new Dictionary<string, string>() {
                    {"en", "You cannot loot the treasure while mounted!"},
                }},
                {"CannotTeleport", new Dictionary<string, string>() {
                    {"en", "You are not allowed to teleport from this event."},
                }},
                {"MustBeAuthorized", new Dictionary<string, string>() {
                    {"en", "You must have building privilege to access this treasure!"},
                }},
                {"OwnerLocked", new Dictionary<string, string>() {
                    {"en", "This treasure belongs to someone else!"},
                }},
                {"CannotFindPosition", new Dictionary<string, string>() {
                    {"en", "Could not find a random position!"},
                }},
                {"PasteOnCooldown", new Dictionary<string, string>() {
                    {"en", "Paste is on cooldown!"},
                }},
                {"SpawnOnCooldown", new Dictionary<string, string>() {
                    {"en", "Try again, a manual spawn was already requested."},
                }},
                {"Thief", new Dictionary<string, string>() {
                    {"en", "<color=#FFFF00>The base at <color=#FFFF00>{0}</color> has been raided by <color=#FFFF00>{1}</color>!</color>"},
                }},
                {"BuySyntax", new Dictionary<string, string>() {
                    {"en", "<color=#FFFF00>Syntax: {0} easy|medium|hard {1}</color>"},
                }},
                {"TargetNotFoundId", new Dictionary<string, string>() {
                    {"en", "<color=#FFFF00>Target {0} not found, or not online.</color>"},
                }},
                {"TargetNotFoundNoId", new Dictionary<string, string>() {
                    {"en", "<color=#FFFF00>No steamid provided.</color>"},
                }},
                {"BuyAnotherDifficulty", new Dictionary<string, string>() {
                    {"en", "Difficulty '<color=#FFFF00>{0}</color>' is not available, please try another difficulty."},
                }},
                {"BuyDifficultyNotAvailableToday", new Dictionary<string, string>() {
                    {"en", "Difficulty '<color=#FFFF00>{0}</color>' is not available today, please try another difficulty."},
                }},
                {"BuyPVPRaidsDisabled", new Dictionary<string, string>() {
                    {"en", "<color=#FFFF00>No PVE raids can be bought for this difficulty as buying raids that allow PVP is not allowed.</color>"},
                }},
                {"BuyRaidsDisabled", new Dictionary<string, string>() {
                    {"en", "<color=#FFFF00>No raids can be bought at this time.</color>"},
                }},
                {"BuyBaseSpawnedAt", new Dictionary<string, string>() {
                    {"en", "<color=#FFFF00>Your base has been spawned at {0} in {1} !</color>"},
                }},
                {"BuyBaseAnnouncement", new Dictionary<string, string>() {
                    {"en", "<color=#FFFF00>{0} has paid for a base at {1} in {2}!</color>"},
                }},
                {"DestroyingBaseAt", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>Destroying raid base at <color=#FFFF00>{0}</color> in <color=#FFFF00>{1}</color> minutes!</color>"},
                }},
                {"PasteIsBlocked", new Dictionary<string, string>() {
                    {"en", "You cannot start a raid base event there!"},
                }},
                {"LookElsewhere", new Dictionary<string, string>() {
                    {"en", "Unable to find a position; look elsewhere."},
                }},
                {"BuildingNotConfigured", new Dictionary<string, string>() {
                    {"en", "You cannot spawn a base that is not configured."},
                }},
                {"NoValidBuildingsConfigured", new Dictionary<string, string>() {
                    {"en", "No valid buildings have been configured. Raidable Bases > Building Names in config."},
                }},
                {"DespawnBaseSuccess", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>Despawning the nearest raid base to you!</color>"},
                }},
                {"DespawnedAt", new Dictionary<string, string>() {
                    {"en", "{0} despawned a base manually at {1}"},
                }},
                {"DespawnedAll", new Dictionary<string, string>() {
                    {"en", "{0} despawned all bases manually"},
                }},
                {"ModeLevel", new Dictionary<string, string>() {
                    {"en", "level"},
                }},
                {"ModeEasy", new Dictionary<string, string>() {
                    {"en", "easy"},
                }},
                {"ModeMedium", new Dictionary<string, string>() {
                    {"en", "medium"},
                }},
                {"ModeHard", new Dictionary<string, string>() {
                    {"en", "hard"},
                }},
                {"ModeExpert", new Dictionary<string, string>() {
                    {"en", "expert"},
                }},
                {"ModeNightmare", new Dictionary<string, string>() {
                    {"en", "nightmare"},
                }},
                {"DespawnBaseNoneAvailable", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>You must be within 100m of a raid base to despawn it.</color>"},
                }},
                {"GridIsLoading", new Dictionary<string, string>() {
                    {"en", "The grid is loading; please wait until it has finished."},
                }},
                {"GridIsLoadingFormatted", new Dictionary<string, string>() {
                    {"en", "Grid is loading. The process has taken {0} seconds so far with {1} locations added on the grid."},
                }},
                {"TooPowerful", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>This place is guarded by a powerful spirit. You sheath your wand in fear!</color>"},
                }},
                {"TooPowerfulDrop", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>This place is guarded by a powerful spirit. You drop your wand in fear!</color>"},
                }},
                {"BuyCooldown", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>You must wait {0} seconds to use this command!</color>"},
                }},
                {"LoadCopyPaste", new Dictionary<string, string>() {
                    {"en", "CopyPaste is not loaded."},
                }},
                {"DoomAndGloom", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>You have left a {0} zone and can be attacked for another {1} seconds!</color>"},
                }},
                {"MaintainCoroutineFailedToday", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>Failed to start maintain coroutine; no difficulties are available today.</color>"},
                }},
                {"ScheduleCoroutineFailedToday", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>Failed to start scheduled coroutine; no difficulties are available today.</color>"},
                }},
                {"NoConfiguredLoot", new Dictionary<string, string>() {
                    {"en", "Error: No loot found in the config!"},
                }},
                {"NoContainersFound", new Dictionary<string, string>() {
                    {"en", "Error: No usable containers found for {0} @ {1}!"},
                }},
                {"NoBoxesFound", new Dictionary<string, string>() {
                    {"en", "Error: No usable boxes found for {0} @ {1}!"},
                }},
                {"NoLootSpawned", new Dictionary<string, string>() {
                    {"en", "Error: No loot was spawned!"},
                }},
                {"LoadedManual", new Dictionary<string, string>() {
                    {"en", "Loaded {0} manual spawns."},
                }},
                {"LoadedBuyable", new Dictionary<string, string>() {
                    {"en", "Loaded {0} buyable spawns."},
                }},
                {"LoadedMaintained", new Dictionary<string, string>() {
                    {"en", "Loaded {0} maintained spawns."},
                }},
                {"LoadedScheduled", new Dictionary<string, string>() {
                    {"en", "Loaded {0} scheduled spawns."},
                }},
                {"InitializedGrid", new Dictionary<string, string>() {
                    {"en", "Grid initialization completed in {0} seconds and {1} milliseconds on a {2} size map. {3} locations are on the grid."},
                }},
                {"EntityCountMax", new Dictionary<string, string>() {
                    {"en", "Command disabled due to entity count being greater than 300k"},
                }},
                {"NotifyPlayerMessageFormat", new Dictionary<string, string>() {
                    {"en", "<color=#ADD8E6>{rank}</color>. <color=#C0C0C0>{name}</color> (<color=#FFFF00>{value}</color>)"},
                }},
                {"ConfigUseFormat", new Dictionary<string, string>() {
                    {"en", "Use: rb.config <{0}> [base] [subset]"},
                }},
                {"ConfigAddBaseSyntax", new Dictionary<string, string>() {
                    {"en", "Use: rb.config add nivex1 nivex4 nivex5 nivex6"},
                }},
                {"FileDoesNotExist", new Dictionary<string, string>() {
                    {"en", " > This file does not exist\n"},
                }},
                {"ListingAll", new Dictionary<string, string>() {
                    {"en", "Listing all primary bases and their subsets:"},
                }},
                {"PrimaryBase", new Dictionary<string, string>() {
                    {"en", "Primary Base: "},
                }},
                {"AdditionalBase", new Dictionary<string, string>() {
                    {"en", "Additional Base: "},
                }},
                {"RaidPVEWarning", new Dictionary<string, string>() {
                    {"en", "Configuration is set to block PVP raids from being bought, and no PVE raids are configured. Therefore players cannot buy raids until you add a PVE raid."},
                }},
                {"NoValidBuilingsWarning", new Dictionary<string, string>() {
                    {"en", "No valid buildings are configured with a valid file that exists. Did you configure valid files and reload the plugin?"},
                }},
                {"Adding", new Dictionary<string, string>() {
                    {"en", "Adding: {0}"},
                }},
                {"AddedPrimaryBase", new Dictionary<string, string>() {
                    {"en", "Added Primary Base: {0}"},
                }},
                {"AddedAdditionalBase", new Dictionary<string, string>() {
                    {"en", "Added Additional Base: {0}"},
                }},
                {"DifficultySetTo", new Dictionary<string, string>() {
                    {"en", "Difficulty set to: {0}"},
                }},
                {"EntryAlreadyExists", new Dictionary<string, string>() {
                    {"en", "That entry already exists."},
                }},
                {"RemoveSyntax", new Dictionary<string, string>() {
                    {"en", "Use: rb.config remove nivex1"},
                }},
                {"RemovingAllBasesFor", new Dictionary<string, string>() {
                    {"en", "\nRemoving all bases for: {0}"},
                }},
                {"RemovedPrimaryBase", new Dictionary<string, string>() {
                    {"en", "Removed primary base: {0}"},
                }},
                {"RemovedAdditionalBase", new Dictionary<string, string>() {
                    {"en", "Removed additional base {0} from primary base {1}"},
                }},
                {"RemovedEntries", new Dictionary<string, string>() {
                    {"en", "Removed {0} entries"},
                }},
                {"LockedOut", new Dictionary<string, string>() {
                    {"en", "You are locked out from {0} raids for {1}"},
                }},
                {"PVPFlag", new Dictionary<string, string>() {
                    {"en", "[<color=#FF0000>PVP</color>] "},
                }},
                {"PVEFlag", new Dictionary<string, string>() {
                    {"en", "[<color=#008000>PVE</color>] "},
                }},
                {"PVP ZONE", new Dictionary<string, string>() {
                    {"en", "PVP ZONE"},
                }},
                {"PVE ZONE", new Dictionary<string, string>() {
                    {"en", "PVE ZONE"},
                }},
                {"OnPlayerExit", new Dictionary<string, string>()
                {
                    {"en", "<color=#FF0000>You have left a raidable PVP base!</color>"},
                }},
                {"OnPlayerExitPVE", new Dictionary<string, string>()
                {
                    {"en", "<color=#FF0000>You have left a raidable PVE base!</color>"},
                }},
                {"PasteIsBlockedStandAway", new Dictionary<string, string>() {
                    {"en", "You cannot start a raid base event there because you are too close to the spawn. Either move or use noclip."},
                }},
                {"ReloadConfig", new Dictionary<string, string>() {
                    {"en", "Reloading config..."},
                }},
                {"ReloadMaintainCo", new Dictionary<string, string>() {
                    {"en", "Stopped maintain coroutine."},
                }},
                {"ReloadScheduleCo", new Dictionary<string, string>() {
                    {"en", "Stopped schedule coroutine."},
                }},
                {"ReloadInit", new Dictionary<string, string>() {
                    {"en", "Initializing..."},
                }},
                {"YourCorpse", new Dictionary<string, string>() {
                    {"en", "Your Corpse"},
                }},
                {"NotAllowed", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>That action is not allowed in this zone.</color>"},
                }},
                {"BlockedZones", new Dictionary<string, string>() {
                    {"en", "Blocked spawn points in {0} zones."},
                }},
                {"UIFormat", new Dictionary<string, string>() {
                    {"en", "{0} C:{1} [{2}m]"},
                }},
                {"UIFormatContainers", new Dictionary<string, string>() {
                    {"en", "{0} C:{1}"},
                }},
                {"UIFormatMinutes", new Dictionary<string, string>() {
                    {"en", "{0} [{1}m]"},
                }},
                {"UIFormatLockoutMinutes", new Dictionary<string, string>() {
                    {"en", "{0}m"},
                }},
                {"UIHelpTextAll", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>You can toggle the UI by using <color=#FFA500>/{0} ui [lockouts]</color></color>"},
                }},
                {"UIHelpText", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>You can toggle the UI by using <color=#FFA500>/{0} ui</color></color>"},
                }},
                {"HoggingFinishYourRaid", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>You must finish your last raid at {0} before joining another.</color>"},
                }},
                {"HoggingFinishYourRaidClan", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>Your clan mate `{0}` must finish their last raid at {1}.</color>"},
                }},
                {"HoggingFinishYourRaidTeam", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>Your team mate `{0}` must finish their last raid at {1}.</color>"},
                }},
                {"HoggingFinishYourRaidFriend", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>Your friend `{0}` must finish their last raid at {1}.</color>"},
                }},
                {"TimeFormat", new Dictionary<string, string>() {
                    {"en", "{0:D2}h {1:D2}m {2:D2}s"},
                }},
                {"BuyableAlreadyRequested", new Dictionary<string, string>() {
                    {"en", "You must wait 2 seconds to try buying again."},
                }},
                {"BuyableServerRestarting", new Dictionary<string, string>() {
                    {"en", "You cannot buy a raid when a server restart is pending."},
                }},
                {"BuyableServerSaving", new Dictionary<string, string>() {
                    {"en", "You cannot buy a raid while the server is saving."},
                }},
                {"BuyableAlreadyOwner", new Dictionary<string, string>() {
                    {"en", "You cannot buy multiple raids."},
                }},
                {"TargetTooFar", new Dictionary<string, string>() {
                    {"en", "Your target is not close enough to a raid."},
                }},
                {"TooFar", new Dictionary<string, string>() {
                    {"en", "You are not close enough to a raid."},
                }},
                {"RaidLockedTo", new Dictionary<string, string>() {
                    {"en", "Raid has been locked to: {0}"},
                }},
                {"RemovedLockFor", new Dictionary<string, string>() {
                    {"en", "Removed lockout for {0} ({1})"},
                }},
                {"RaidOwnerCleared", new Dictionary<string, string>() {
                    {"en", "Raid owner has been cleared."},
                }},
                {"TooCloseToABuilding", new Dictionary<string, string>() {
                    {"en", "Too close to another building"},
                }},
                {"Buy Raids", new Dictionary<string, string>() {
                    {"en", "Buy Raids"},
                }},
            };
        }

        protected override void LoadDefaultMessages()
        {
            var compiledLangs = new Dictionary<string, Dictionary<string, string>>();

            foreach (var line in GetMessages())
            {
                foreach (var translate in line.Value)
                {
                    if (!compiledLangs.ContainsKey(translate.Key))
                        compiledLangs[translate.Key] = new Dictionary<string, string>();

                    compiledLangs[translate.Key][line.Key] = translate.Value;
                }
            }

            foreach (var cLangs in compiledLangs)
                lang.RegisterMessages(cLangs.Value, this, cLangs.Key);
        }

        private static int GetPercentAmount(int amount)
        {
            decimal percentLoss = _config.Treasure.PercentLoss;

            if (percentLoss > 0m && (percentLoss /= 100m) > 0m)
            {
                amount = Convert.ToInt32(amount - (amount * percentLoss));

                if (_config.Treasure.UseDOWL && !_config.Treasure.Increased)
                {
                    return amount;
                }
            }

            decimal percentIncrease = GetDayPercentIncrease();

            if (percentIncrease > 0m && (percentIncrease /= 100m) > 0m)
            {
                return Convert.ToInt32(amount + (amount * percentIncrease));
            }

            return amount;
        }

        private static decimal GetDayPercentIncrease()
        {
            decimal percentIncrease;

            switch (DateTime.Now.DayOfWeek)
            {
                case DayOfWeek.Monday:
                    {
                        percentIncrease = _config.Treasure.PercentIncreaseOnMonday;
                        break;
                    }
                case DayOfWeek.Tuesday:
                    {
                        percentIncrease = _config.Treasure.PercentIncreaseOnTuesday;
                        break;
                    }
                case DayOfWeek.Wednesday:
                    {
                        percentIncrease = _config.Treasure.PercentIncreaseOnWednesday;
                        break;
                    }
                case DayOfWeek.Thursday:
                    {
                        percentIncrease = _config.Treasure.PercentIncreaseOnThursday;
                        break;
                    }
                case DayOfWeek.Friday:
                    {
                        percentIncrease = _config.Treasure.PercentIncreaseOnFriday;
                        break;
                    }
                case DayOfWeek.Saturday:
                    {
                        percentIncrease = _config.Treasure.PercentIncreaseOnSaturday;
                        break;
                    }
                default:
                    {
                        percentIncrease = _config.Treasure.PercentIncreaseOnSunday;
                        break;
                    }
            }

            return percentIncrease;
        }

        private static Configuration _config;

        private static List<PasteOption> DefaultPasteOptions
        {
            get
            {
                return new List<PasteOption>
                {
                    new PasteOption() { Key = "stability", Value = "false" },
                    new PasteOption() { Key = "autoheight", Value = "false" },
                    new PasteOption() { Key = "height", Value = "1.0" }
                };
            }
        }

        private static Dictionary<string, BuildingOptions> DefaultBuildingOptions
        {
            get
            {
                return new Dictionary<string, BuildingOptions>()
                {
                    ["Easy Bases"] = new BuildingOptions
                    {
                        AdditionalBases = new Dictionary<string, List<PasteOption>>
                        {
                            ["EasyBase1"] = DefaultPasteOptions,
                            ["EasyBase2"] = DefaultPasteOptions,
                            ["EasyBase3"] = DefaultPasteOptions,
                            ["EasyBase4"] = DefaultPasteOptions,
                            ["EasyBase5"] = DefaultPasteOptions
                        },
                        Mode = RaidableMode.Easy,
                        PasteOptions = DefaultPasteOptions
                    },
                    ["Medium Bases"] = new BuildingOptions
                    {
                        AdditionalBases = new Dictionary<string, List<PasteOption>>
                        {
                            ["MediumBase1"] = DefaultPasteOptions,
                            ["MediumBase2"] = DefaultPasteOptions,
                            ["MediumBase3"] = DefaultPasteOptions,
                            ["MediumBase4"] = DefaultPasteOptions,
                            ["MediumBase5"] = DefaultPasteOptions
                        },
                        Mode = RaidableMode.Medium,
                        PasteOptions = DefaultPasteOptions
                    },
                    ["Hard Bases"] = new BuildingOptions
                    {
                        AdditionalBases = new Dictionary<string, List<PasteOption>>
                        {
                            ["HardBase1"] = DefaultPasteOptions,
                            ["HardBase2"] = DefaultPasteOptions,
                            ["HardBase3"] = DefaultPasteOptions,
                            ["HardBase4"] = DefaultPasteOptions,
                            ["HardBase5"] = DefaultPasteOptions
                        },
                        Mode = RaidableMode.Hard,
                        PasteOptions = DefaultPasteOptions
                    },
                    ["Expert Bases"] = new BuildingOptions
                    {
                        AdditionalBases = new Dictionary<string, List<PasteOption>>
                        {
                            ["ExpertBase1"] = DefaultPasteOptions,
                            ["ExpertBase2"] = DefaultPasteOptions,
                            ["ExpertBase3"] = DefaultPasteOptions,
                            ["ExpertBase4"] = DefaultPasteOptions,
                            ["ExpertBase5"] = DefaultPasteOptions
                        },
                        Mode = RaidableMode.Expert,
                        PasteOptions = DefaultPasteOptions
                    },
                    ["Nightmare Bases"] = new BuildingOptions
                    {
                        AdditionalBases = new Dictionary<string, List<PasteOption>>
                        {
                            ["NightmareBase1"] = DefaultPasteOptions,
                            ["NightmareBase2"] = DefaultPasteOptions,
                            ["NightmareBase3"] = DefaultPasteOptions,
                            ["NightmareBase4"] = DefaultPasteOptions,
                            ["NightmareBase5"] = DefaultPasteOptions
                        },
                        Mode = RaidableMode.Nightmare,
                        PasteOptions = DefaultPasteOptions
                    }
                };
            }
        }

        private static List<TreasureItem> DefaultLoot
        {
            get
            {
                return new List<TreasureItem>
                {
                    new TreasureItem { shortname = "ammo.pistol", amount = 40, skin = 0, amountMin = 40 },
                    new TreasureItem { shortname = "ammo.pistol.fire", amount = 40, skin = 0, amountMin = 40 },
                    new TreasureItem { shortname = "ammo.pistol.hv", amount = 40, skin = 0, amountMin = 40 },
                    new TreasureItem { shortname = "ammo.rifle", amount = 60, skin = 0, amountMin = 60 },
                    new TreasureItem { shortname = "ammo.rifle.explosive", amount = 60, skin = 0, amountMin = 60 },
                    new TreasureItem { shortname = "ammo.rifle.hv", amount = 60, skin = 0, amountMin = 60 },
                    new TreasureItem { shortname = "ammo.rifle.incendiary", amount = 60, skin = 0, amountMin = 60 },
                    new TreasureItem { shortname = "ammo.shotgun", amount = 24, skin = 0, amountMin = 24 },
                    new TreasureItem { shortname = "ammo.shotgun.slug", amount = 40, skin = 0, amountMin = 40 },
                    new TreasureItem { shortname = "surveycharge", amount = 20, skin = 0, amountMin = 20 },
                    new TreasureItem { shortname = "metal.refined", amount = 150, skin = 0, amountMin = 150 },
                    new TreasureItem { shortname = "bucket.helmet", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "cctv.camera", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "coffeecan.helmet", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "explosive.timed", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "metal.facemask", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "metal.plate.torso", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "mining.quarry", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "pistol.m92", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "rifle.ak", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "rifle.bolt", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "rifle.lr300", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "smg.2", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "smg.mp5", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "smg.thompson", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "supply.signal", amount = 1, skin = 0, amountMin = 1 },
                    new TreasureItem { shortname = "targeting.computer", amount = 1, skin = 0, amountMin = 1 },
                };
            }
        }

        public class PluginSettingsLimitsDays
        {
            [JsonProperty(PropertyName = "Monday")]
            public bool Monday { get; set; } = true;

            [JsonProperty(PropertyName = "Tuesday")]
            public bool Tuesday { get; set; } = true;

            [JsonProperty(PropertyName = "Wednesday")]
            public bool Wednesday { get; set; } = true;

            [JsonProperty(PropertyName = "Thursday")]
            public bool Thursday { get; set; } = true;

            [JsonProperty(PropertyName = "Friday")]
            public bool Friday { get; set; } = true;

            [JsonProperty(PropertyName = "Saturday")]
            public bool Saturday { get; set; } = true;

            [JsonProperty(PropertyName = "Sunday")]
            public bool Sunday { get; set; } = true;
        }

        public class PluginSettingsBaseLockout
        {
            [JsonProperty(PropertyName = "Time Between Raids In Minutes (Easy)")]
            public double Easy { get; set; }

            [JsonProperty(PropertyName = "Time Between Raids In Minutes (Medium)")]
            public double Medium { get; set; }

            [JsonProperty(PropertyName = "Time Between Raids In Minutes (Hard)")]
            public double Hard { get; set; }

            [JsonProperty(PropertyName = "Time Between Raids In Minutes (Expert)")]
            public double Expert { get; set; }

            [JsonProperty(PropertyName = "Time Between Raids In Minutes (Nightmare)")]
            public double Nightmare { get; set; }

            [JsonProperty(PropertyName = "Block Clans From Owning More Than One Raid")]
            public bool BlockClans { get; set; }

            [JsonProperty(PropertyName = "Block Friends From Owning More Than One Raid")]
            public bool BlockFriends { get; set; }

            [JsonProperty(PropertyName = "Block Teams From Owning More Than One Raid")]
            public bool BlockTeams { get; set; }

            public bool Any() => Easy > 0 || Medium > 0 || Hard > 0 || Expert > 0 || Nightmare > 0;

            public bool IsBlocking() => BlockClans || BlockFriends || BlockTeams;
        }

        public class PluginSettingsBaseAmounts
        {
            [JsonProperty(PropertyName = "Easy")]
            public int Easy { get; set; }

            [JsonProperty(PropertyName = "Medium")]
            public int Medium { get; set; }

            [JsonProperty(PropertyName = "Hard")]
            public int Hard { get; set; }

            [JsonProperty(PropertyName = "Expert")]
            public int Expert { get; set; }

            [JsonProperty(PropertyName = "Nightmare")]
            public int Nightmare { get; set; }

            public int Get(RaidableMode mode)
            {
                switch (mode)
                {
                    case RaidableMode.Easy:
                        {
                            return Easy;
                        }
                    case RaidableMode.Medium:
                        {
                            return Medium;
                        }
                    case RaidableMode.Hard:
                        {
                            return Hard;
                        }
                    case RaidableMode.Expert:
                        {
                            return Expert;
                        }
                    case RaidableMode.Nightmare:
                        {
                            return Nightmare;
                        }
                    case RaidableMode.Random:
                        {
                            return 0;
                        }
                    default:
                        {
                            return -1;
                        }
                }
            }
        }

        public class PluginSettingsColors1
        {
            [JsonProperty(PropertyName = "Easy")]
            public string Easy { get; set; } = "000000";

            [JsonProperty(PropertyName = "Medium")]
            public string Medium { get; set; } = "000000";

            [JsonProperty(PropertyName = "Hard")]
            public string Hard { get; set; } = "000000";

            [JsonProperty(PropertyName = "Expert")]
            public string Expert { get; set; } = "000000";

            [JsonProperty(PropertyName = "Nightmare")]
            public string Nightmare { get; set; } = "000000";
        }

        public class PluginSettingsColors2
        {
            [JsonProperty(PropertyName = "Easy")]
            public string Easy { get; set; } = "00FF00";

            [JsonProperty(PropertyName = "Medium")]
            public string Medium { get; set; } = "FFEB04";

            [JsonProperty(PropertyName = "Hard")]
            public string Hard { get; set; } = "FF0000";

            [JsonProperty(PropertyName = "Expert")]
            public string Expert { get; set; } = "0000FF";

            [JsonProperty(PropertyName = "Nightmare")]
            public string Nightmare { get; set; } = "000000";
        }

        public class PluginSettingsBaseManagementMountables
        {
            [JsonProperty(PropertyName = "Boats")]
            public bool Boats { get; set; }

            [JsonProperty(PropertyName = "Cars (Basic)")]
            public bool BasicCars { get; set; }

            [JsonProperty(PropertyName = "Cars (Modular)")]
            public bool ModularCars { get; set; } = true;

            [JsonProperty(PropertyName = "Chinook")]
            public bool CH47 { get; set; } = true;

            [JsonProperty(PropertyName = "Horses")]
            public bool Horses { get; set; }

            [JsonProperty(PropertyName = "MiniCopters")]
            public bool MiniCopters { get; set; } = true;

            [JsonProperty(PropertyName = "Pianos")]
            public bool Pianos { get; set; } = true;

            [JsonProperty(PropertyName = "Scrap Transport Helicopters")]
            public bool Scrap { get; set; } = true;
        }

        public class PluginSettingsBaseManagement
        {
            [JsonProperty(PropertyName = "Eject Mounts")]
            public PluginSettingsBaseManagementMountables Mounts { get; set; } = new PluginSettingsBaseManagementMountables();

            [JsonProperty(PropertyName = "Max Amount Allowed To Automatically Spawn Per Difficulty (0 = infinite, -1 = disabled)")]
            public PluginSettingsBaseAmounts Amounts { get; set; } = new PluginSettingsBaseAmounts();

            [JsonProperty(PropertyName = "Player Lockouts (0 = ignore)")]
            public PluginSettingsBaseLockout Lockout { get; set; } = new PluginSettingsBaseLockout();

            [JsonProperty(PropertyName = "Easy Raids Can Spawn On")]
            public PluginSettingsLimitsDays Easy { get; set; } = new PluginSettingsLimitsDays();

            [JsonProperty(PropertyName = "Medium Raids Can Spawn On")]
            public PluginSettingsLimitsDays Medium { get; set; } = new PluginSettingsLimitsDays();

            [JsonProperty(PropertyName = "Hard Raids Can Spawn On")]
            public PluginSettingsLimitsDays Hard { get; set; } = new PluginSettingsLimitsDays();

            [JsonProperty(PropertyName = "Expert Raids Can Spawn On")]
            public PluginSettingsLimitsDays Expert { get; set; } = new PluginSettingsLimitsDays();

            [JsonProperty(PropertyName = "Nightmare Raids Can Spawn On")]
            public PluginSettingsLimitsDays Nightmare { get; set; } = new PluginSettingsLimitsDays();

            [JsonProperty(PropertyName = "Entities Not Allowed To Be Picked Up", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> BlacklistedPickupItems { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Difficulty Colors (Border)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public PluginSettingsColors1 Colors1 { get; set; } = new PluginSettingsColors1();

            [JsonProperty(PropertyName = "Difficulty Colors (Inner)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public PluginSettingsColors2 Colors2 { get; set; } = new PluginSettingsColors2();

            [JsonProperty(PropertyName = "Allow Teleport")]
            public bool AllowTeleport { get; set; }

            [JsonProperty(PropertyName = "Allow Cupboard Loot To Drop")]
            public bool AllowCupboardLoot { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Players To Build")]
            public bool AllowBuilding { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Player Bags To Be Lootable At PVP Bases")]
            public bool PlayersLootableInPVP { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Player Bags To Be Lootable At PVE Bases")]
            public bool PlayersLootableInPVE { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Raid Bases On Roads")]
            public bool AllowOnRoads { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Raid Bases On Rivers")]
            public bool AllowOnRivers { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Vending Machines To Broadcast")]
            public bool AllowBroadcasting { get; set; }

            [JsonProperty(PropertyName = "Auto Turrets Power On/Off Automatically")]
            public bool AutoTurretPowerOnOff { get; set; } = true;

            [JsonProperty(PropertyName = "Backpacks Can Be Opened At PVE Bases")]
            public bool BackpacksOpenPVE { get; set; } = true;

            [JsonProperty(PropertyName = "Backpacks Can Be Opened At PVP Bases")]
            public bool BackpacksOpenPVP { get; set; } = true;

            [JsonProperty(PropertyName = "Backpacks Can Drop At PVE Bases")]
            public bool BackpacksPVE { get; set; } = true;

            [JsonProperty(PropertyName = "Backpacks Can Drop In PVP Bases")]
            public bool BackpacksPVP { get; set; } = true;

            [JsonProperty(PropertyName = "Block Mounted Damage To Bases And Players")]
            public bool BlockMounts { get; set; }

            [JsonProperty(PropertyName = "Block RestoreUponDeath Plugin For PVP Bases")]
            public bool BlockRestorePVP { get; set; }

            [JsonProperty(PropertyName = "Block RestoreUponDeath Plugin For PVE Bases")]
            public bool BlockRestorePVE { get; set; }

            [JsonProperty(PropertyName = "Boxes Are Invulnerable")]
            public bool Invulnerable { get; set; }

            [JsonProperty(PropertyName = "Bypass Lock Treasure To First Attacker For PVE Bases")]
            public bool BypassUseOwnersForPVE { get; set; }

            [JsonProperty(PropertyName = "Bypass Lock Treasure To First Attacker For PVP Bases")]
            public bool BypassUseOwnersForPVP { get; set; }

            [JsonProperty(PropertyName = "Building Blocks Are Immune To Damage")]
            public bool BlocksImmune { get; set; }

            //[JsonProperty(PropertyName = "Destroy Dropped Container Loot On Despawn")]
            //public bool DestroyLoot { get; set; }

            [JsonProperty(PropertyName = "Do Not Destroy Player Built Deployables")]
            public bool DoNotDestroyDeployables { get; set; }

            [JsonProperty(PropertyName = "Divide Rewards Among All Raiders")]
            public bool DivideRewards { get; set; } = true;

            [JsonProperty(PropertyName = "Draw Corpse Time (Seconds)")]
            public float DrawTime { get; set; } = 300f;

            [JsonProperty(PropertyName = "Eject Sleepers Before Spawning Base")]
            public bool EjectSleepers { get; set; } = true;

            [JsonProperty(PropertyName = "Extra Distance To Spawn From Monuments")]
            public float MonumentDistance { get; set; }

            [JsonProperty(PropertyName = "Flame Turrets Ignore NPCs")]
            public bool FlameTurrets { get; set; }

            [JsonProperty(PropertyName = "Ignore Containers That Spawn With Loot Already")]
            public bool IgnoreContainedLoot { get; set; }

            [JsonProperty(PropertyName = "Move Cookables Into Ovens")]
            public bool Cook { get; set; } = true;

            [JsonProperty(PropertyName = "Move Food Into BBQ Or Fridge")]
            public bool Food { get; set; } = true;

            [JsonProperty(PropertyName = "Move Resources Into Tool Cupboard")]
            public bool Cupboard { get; set; }

            [JsonProperty(PropertyName = "Lock Treasure To First Attacker")]
            public bool UseOwners { get; set; }

            [JsonProperty(PropertyName = "Lock Treasure Max Inactive Time (Minutes)")]
            public float LockTime { get; set; } = 10f;

            [JsonProperty(PropertyName = "Minutes Until Despawn After Looting (min: 1)")]
            public int DespawnMinutes { get; set; } = 15;

            [JsonProperty(PropertyName = "Minutes Until Despawn After Inactive (0 = disabled)")]
            public int DespawnMinutesInactive { get; set; } = 45;

            [JsonProperty(PropertyName = "Minutes Until Despawn After Inactive Resets When Damaged")]
            public bool DespawnMinutesInactiveReset { get; set; } = true;

            [JsonProperty(PropertyName = "Mounts Can Take Damage From Players")]
            public bool MountDamageFromPlayers { get; set; }

            [JsonProperty(PropertyName = "Mounts Can Take Damage From SamSites")]
            public bool MountDamageFromSamSites { get; set; } = true;

            [JsonProperty(PropertyName = "Player Cupboard Detection Radius")]
            public float CupboardDetectionRadius { get; set; } = 75f;

            [JsonProperty(PropertyName = "PVP Delay Between Zone Hopping")]
            public float PVPDelay { get; set; } = 10f;

            [JsonProperty(PropertyName = "Prevent Fire From Spreading")]
            public bool PreventFireFromSpreading { get; set; } = true;

            [JsonProperty(PropertyName = "Prevent Players From Hogging Raids")]
            public bool PreventHogging { get; set; } = true;

            [JsonProperty(PropertyName = "Require Cupboard To Be Looted Before Despawning")]
            public bool RequireCupboardLooted { get; set; }

            [JsonProperty(PropertyName = "Destroying The Cupboard Completes The Raid")]
            public bool EndWhenCupboardIsDestroyed { get; set; }

            [JsonProperty(PropertyName = "Require All Bases To Spawn Before Respawning An Existing Base")]
            public bool RequireAllSpawned { get; set; }

            [JsonProperty(PropertyName = "Turn Lights On At Night")]
            public bool Lights { get; set; } = true;

            [JsonProperty(PropertyName = "Turn Lights On Indefinitely")]
            public bool AlwaysLights { get; set; }

            [JsonProperty(PropertyName = "Use Random Codes On Code Locks")]
            public bool RandomCodes { get; set; } = true;

            [JsonProperty(PropertyName = "Wait To Start Despawn Timer When Base Takes Damage From Player")]
            public bool Engaged { get; set; }
        }

        public class PluginSettingsMapMarkers
        {
            [JsonProperty(PropertyName = "Marker Name")]
            public string MarkerName { get; set; } = "Raidable Base Event";

            [JsonProperty(PropertyName = "Radius")]
            public float Radius { get; set; } = 0.25f;

            [JsonProperty(PropertyName = "Use Vending Map Marker")]
            public bool UseVendingMarker { get; set; } = true;

            [JsonProperty(PropertyName = "Use Explosion Map Marker")]
            public bool UseExplosionMarker { get; set; }

            [JsonProperty(PropertyName = "Create Markers For Buyable Events")]
            public bool Buyables { get; set; } = true;

            [JsonProperty(PropertyName = "Create Markers For Maintained Events")]
            public bool Maintained { get; set; } = true;

            [JsonProperty(PropertyName = "Create Markers For Scheduled Events")]
            public bool Scheduled { get; set; } = true;

            [JsonProperty(PropertyName = "Create Markers For Manual Events")]
            public bool Manual { get; set; } = true;
        }

        public class PluginSettings
        {
            [JsonProperty(PropertyName = "Raid Management")]
            public PluginSettingsBaseManagement Management { get; set; } = new PluginSettingsBaseManagement();

            [JsonProperty(PropertyName = "Map Markers")]
            public PluginSettingsMapMarkers Markers { get; set; } = new PluginSettingsMapMarkers();

            [JsonProperty(PropertyName = "Buyable Events")]
            public RaidableBaseSettingsBuyable Buyable { get; set; } = new RaidableBaseSettingsBuyable();

            [JsonProperty(PropertyName = "Maintained Events")]
            public RaidableBaseSettingsMaintained Maintained { get; set; } = new RaidableBaseSettingsMaintained();

            [JsonProperty(PropertyName = "Manual Events")]
            public RaidableBaseSettingsManual Manual { get; set; } = new RaidableBaseSettingsManual();

            [JsonProperty(PropertyName = "Scheduled Events")]
            public RaidableBaseSettingsScheduled Schedule { get; set; } = new RaidableBaseSettingsScheduled();

            [JsonProperty(PropertyName = "Economics Buy Raid Costs (0 = disabled)")]
            public RaidableBaseEconomicsOptions Economics { get; set; } = new RaidableBaseEconomicsOptions();

            [JsonProperty(PropertyName = "ServerRewards Buy Raid Costs (0 = disabled)")]
            public RaidableBaseServerRewardsOptions ServerRewards { get; set; } = new RaidableBaseServerRewardsOptions();

            [JsonProperty(PropertyName = "Allowed Zone Manager Zones", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Inclusions { get; set; } = new List<string> { "pvp", "99999999" };

            [JsonProperty(PropertyName = "Amount Of Entities To Undo Per Batch (1 = Slowest But Better Performance)")]
            public int BatchLimit { get; set; } = 5;

            [JsonProperty(PropertyName = "Automatically Teleport Admins To Their Map Marker Positions")]
            public bool TeleportMarker { get; set; }

            [JsonProperty(PropertyName = "Block Wizardry Plugin At Events")]
            public bool NoWizardry { get; set; }

            [JsonProperty(PropertyName = "Chat Steam64ID")]
            public ulong ChatID { get; set; }

            [JsonProperty(PropertyName = "Expansion Mode (Dangerous Treasures)")]
            public bool ExpansionMode { get; set; }

            [JsonProperty(PropertyName = "Remove Admins From Raiders List")]
            public bool RemoveAdminRaiders { get; set; }

            [JsonProperty(PropertyName = "Show X Z Coordinates")]
            public bool ShowXZ { get; set; } = false;

            [JsonProperty(PropertyName = "Buy Raid Command")]
            public string BuyCommand { get; set; } = "buyraid";

            [JsonProperty(PropertyName = "Event Command")]
            public string EventCommand { get; set; } = "rbe";

            [JsonProperty(PropertyName = "Hunter Command")]
            public string HunterCommand { get; set; } = "rb";

            [JsonProperty(PropertyName = "Server Console Command")]
            public string ConsoleCommand { get; set; } = "rbevent";
        }

        public class EventMessageSettings
        {
            [JsonProperty(PropertyName = "Announce Raid Unlocked")]
            public bool AnnounceRaidUnlock { get; set; }

            [JsonProperty(PropertyName = "Announce Buy Base Messages")]
            public bool AnnounceBuy { get; set; }

            [JsonProperty(PropertyName = "Announce Thief Message")]
            public bool AnnounceThief { get; set; } = true;

            [JsonProperty(PropertyName = "Announce PVE/PVP Enter/Exit Messages")]
            public bool AnnounceEnterExit { get; set; } = true;

            [JsonProperty(PropertyName = "Show Destroy Warning")]
            public bool ShowWarning { get; set; } = true;

            [JsonProperty(PropertyName = "Show Opened Message")]
            public bool Opened { get; set; } = true;

            [JsonProperty(PropertyName = "Show Opened Message For Paid Bases")]
            public bool OpenedAndPaid { get; set; } = true;

            [JsonProperty(PropertyName = "Show Prefix")]
            public bool Prefix { get; set; } = true;
        }

        public class GUIAnnouncementSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; }

            [JsonProperty(PropertyName = "Banner Tint Color")]
            public string TintColor { get; set; } = "Grey";

            [JsonProperty(PropertyName = "Maximum Distance")]
            public float Distance { get; set; } = 300f;

            [JsonProperty(PropertyName = "Text Color")]
            public string TextColor { get; set; } = "White";
        }

        public class LustyMapSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; }

            [JsonProperty(PropertyName = "Icon File")]
            public string IconFile { get; set; } = "http://i.imgur.com/XoEMTJj.png";

            [JsonProperty(PropertyName = "Icon Name")]
            public string IconName { get; set; } = "rbevent";

            [JsonProperty(PropertyName = "Icon Rotation")]
            public float IconRotation { get; set; }
        }

        public class NpcSettings
        {
            [JsonProperty(PropertyName = "Murderer Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> MurdererItems { get; set; } = new List<string> { "metal.facemask", "metal.plate.torso", "pants", "tactical.gloves", "boots.frog", "tshirt", "machete" };

            [JsonProperty(PropertyName = "Scientist Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> ScientistItems { get; set; } = new List<string> { "hazmatsuit_scientist", "rifle.ak" };

            [JsonProperty(PropertyName = "Murderer Kits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> MurdererKits { get; set; } = new List<string> { "murderer_kit_1", "murderer_kit_2" };

            [JsonProperty(PropertyName = "Scientist Kits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> ScientistKits { get; set; } = new List<string> { "scientist_kit_1", "scientist_kit_2" };

            [JsonProperty(PropertyName = "Random Names", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> RandomNames { get; set; } = new List<string>();

            //[JsonProperty(PropertyName = "Attempt To Mount Objects Inside Base")]
            //public bool Mount { get; set; }

            [JsonProperty(PropertyName = "Amount To Spawn")]
            public int SpawnAmount { get; set; } = 3;

            [JsonProperty(PropertyName = "Aggression Range")]
            public float AggressionRange { get; set; } = 70f;

            [JsonProperty(PropertyName = "Despawn Inventory On Death")]
            public bool DespawnInventory { get; set; } = true;

            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty(PropertyName = "Health For Murderers (100 min, 5000 max)")]
            public float MurdererHealth { get; set; } = 150f;

            [JsonProperty(PropertyName = "Health For Scientists (100 min, 5000 max)")]
            public float ScientistHealth { get; set; } = 150f;

            [JsonProperty(PropertyName = "Minimum Amount To Spawn")]
            public int SpawnMinAmount { get; set; } = 1;

            [JsonProperty(PropertyName = "Use Dangerous Treasures NPCs")]
            public bool UseExpansionNpcs { get; set; }

            [JsonProperty(PropertyName = "Spawn Murderers And Scientists")]
            public bool SpawnBoth { get; set; } = true;

            [JsonProperty(PropertyName = "Scientist Weapon Accuracy (0 - 100)")]
            public float Accuracy { get; set; } = 75f;

            [JsonProperty(PropertyName = "Spawn Murderers")]
            public bool SpawnMurderers { get; set; }

            [JsonProperty(PropertyName = "Spawn Random Amount")]
            public bool SpawnRandomAmount { get; set; }

            [JsonProperty(PropertyName = "Spawn Scientists Only")]
            public bool SpawnScientistsOnly { get; set; }
        }

        public class PasteOption
        {
            [JsonProperty(PropertyName = "Option")]
            public string Key { get; set; }

            [JsonProperty(PropertyName = "Value")]
            public string Value { get; set; }
        }

        public class BuildingLevelOne
        {
            [JsonProperty(PropertyName = "Amount (0 = disabled)")]
            public int Amount { get; set; }

            [JsonProperty(PropertyName = "Chance To Play")]
            public float Chance { get; set; } = 0.5f;
        }

        public class BuildingLevels
        {
            [JsonProperty(PropertyName = "Level 1 - Play With Fire")]
            public BuildingLevelOne Level1 { get; set; } = new BuildingLevelOne();

            [JsonProperty(PropertyName = "Level 2 - Final Death")]
            public bool Level2 { get; set; }
        }

        public class BuildingGradeLevels
        {
            [JsonProperty(PropertyName = "Wooden")]
            public bool Wooden { get; set; }

            [JsonProperty(PropertyName = "Stone")]
            public bool Stone { get; set; }

            [JsonProperty(PropertyName = "Metal")]
            public bool Metal { get; set; }

            [JsonProperty(PropertyName = "HQM")]
            public bool HQM { get; set; }

            public bool Any() => Wooden || Stone || Metal || HQM;
        }

        public class BuildingOptions
        {
            [JsonProperty(PropertyName = "Difficulty (0 = easy, 1 = medium, 2 = hard, 3 = expert, 4 = nightmare)")]
            public RaidableMode Mode { get; set; } = RaidableMode.Disabled;

            [JsonProperty(PropertyName = "Additional Bases For This Difficulty", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, List<PasteOption>> AdditionalBases { get; set; } = new Dictionary<string, List<PasteOption>>();

            [JsonProperty(PropertyName = "Paste Options", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<PasteOption> PasteOptions { get; set; } = new List<PasteOption>();

            [JsonProperty(PropertyName = "Arena Walls")]
            public RaidableBaseWallOptions ArenaWalls { get; set; } = new RaidableBaseWallOptions();

            [JsonProperty(PropertyName = "NPC Levels")]
            public BuildingLevels Levels { get; set; } = new BuildingLevels();

            [JsonProperty(PropertyName = "NPCs")]
            public NpcSettings NPC { get; set; } = new NpcSettings();

            [JsonProperty(PropertyName = "Rewards")]
            public RewardSettings Rewards { get; set; } = new RewardSettings();

            [JsonProperty(PropertyName = "Change Building Material Tier To")]
            public BuildingGradeLevels Tiers { get; set; } = new BuildingGradeLevels();

            [JsonProperty(PropertyName = "Loot (Empty List = Use Treasure Loot)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> Loot { get; set; } = new List<TreasureItem>();

            [JsonProperty(PropertyName = "Profile Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty(PropertyName = "Add Code Lock To Unlocked Or KeyLocked Doors")]
            public bool DoorLock { get; set; } = true;

            [JsonProperty(PropertyName = "Close Open Doors With No Door Controller Installed")]
            public bool CloseOpenDoors { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Base To Float Above Water")]
            public bool Submerged { get; set; }

            [JsonProperty(PropertyName = "Prevent Base To Float Above Water By Also Checking Surrounding Area (Do Not Use On Lower End Machines)")]
            public bool SubmergedAreaCheck { get; set; }

            [JsonProperty(PropertyName = "Allow Duplicate Items")]
            public bool AllowDuplicates { get; set; }

            [JsonProperty(PropertyName = "Allow Players To Pickup Deployables")]
            public bool AllowPickup { get; set; }

            [JsonProperty(PropertyName = "Allow Players To Deploy A Cupboard")]
            public bool AllowBuildingPriviledges { get; set; } = true;

            [JsonProperty(PropertyName = "Allow PVP")]
            public bool AllowPVP { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Friendly Fire (Teams)")]
            public bool AllowFriendlyFire { get; set; } = true;

            [JsonProperty(PropertyName = "Amount Of Items To Spawn")]
            public int TreasureAmount { get; set; } = 6;

            [JsonProperty(PropertyName = "Auto Turret Health")]
            public float AutoTurretHealth { get; set; } = 1000f;

            [JsonProperty(PropertyName = "Auto Turret Aim Cone")]
            public float AutoTurretAimCone { get; set; } = 5f;

            [JsonProperty(PropertyName = "Auto Turret Sight Range")]
            public float AutoTurretSightRange { get; set; } = 30f;

            [JsonProperty(PropertyName = "Flame Turret Health")]
            public float FlameTurretHealth { get; set; } = 300f;

            [JsonProperty(PropertyName = "Block Damage Outside Of The Dome To Players Inside")]
            public bool BlockOutsideDamageToPlayersDistance { get; set; }

            [JsonProperty(PropertyName = "Block Damage Outside Of The Dome To Bases Inside")]
            public bool BlockOutsideDamageToBaseDistance { get; set; }

            [JsonProperty(PropertyName = "Divide Loot Into All Containers")]
            public bool DivideLoot { get; set; } = true;

            [JsonProperty(PropertyName = "Drop Container Loot X Seconds After It Is Looted")]
            public float DropTimeAfterLooting { get; set; }

            [JsonProperty(PropertyName = "Drop Container Loot Applies Only To Boxes And Cupboards")]
            public bool DropOnlyBoxesAndPrivileges { get; set; } = true;

            [JsonProperty(PropertyName = "Create Dome Around Event Using Spheres (0 = disabled, recommended = 5)")]
            public int SphereAmount { get; set; } = 5;

            [JsonProperty(PropertyName = "Empty All Containers Before Spawning Loot")]
            public bool EmptyAll { get; set; }

            [JsonProperty(PropertyName = "Eject Corpses From Enemy Raids (Advanced Users Only)")]
            public bool EjectCorpses { get; set; } = true;

            [JsonProperty(PropertyName = "Eject Enemies From Purchased PVE Raids")]
            public bool EjectPurchasedPVE { get; set; }

            [JsonProperty(PropertyName = "Eject Enemies From Purchased PVP Raids")]
            public bool EjectPurchasedPVP { get; set; }

            [JsonProperty(PropertyName = "Eject Enemies From Locked PVE Raids")]
            public bool EjectLockedPVE { get; set; }

            [JsonProperty(PropertyName = "Eject Enemies From Locked PVP Raids")]
            public bool EjectLockedPVP { get; set; }

            [JsonProperty(PropertyName = "Equip Unequipped AutoTurret With")]
            public string AutoTurretShortname { get; set; } = "rifle.ak";

            [JsonProperty(PropertyName = "Explosion Damage Modifier (0-999)")]
            public float ExplosionModifier { get; set; } = 100f;

            [JsonProperty(PropertyName = "Force All Boxes To Have Same Skin")]
            public bool SetSkins { get; set; } = true;

            [JsonProperty(PropertyName = "Maximum Elevation Level")]
            public float Elevation { get; set; } = 2.5f;

            [JsonProperty(PropertyName = "Protection Radius")]
            public float ProtectionRadius { get; set; } = 50f;

            [JsonProperty(PropertyName = "Block Plugins Which Prevent Item Durability Loss")]
            public bool EnforceDurability { get; set; } = false;

            [JsonProperty(PropertyName = "Remove Equipped AutoTurret Weapon")]
            public bool RemoveTurretWeapon { get; set; }

            [JsonProperty(PropertyName = "Require Cupboard Access To Loot")]
            public bool RequiresCupboardAccess { get; set; }

            [JsonProperty(PropertyName = "Respawn Npc X Seconds After Death")]
            public float RespawnRate { get; set; }

            [JsonProperty(PropertyName = "Skip Treasure Loot And Use Loot In Base Only")]
            public bool SkipTreasureLoot { get; set; }

            [JsonProperty(PropertyName = "Use Loot Table Priority")]
            public bool Prioritize { get; set; }

            public static BuildingOptions Clone(BuildingOptions options)
            {
                return options.MemberwiseClone() as BuildingOptions;
            }
        }

        public class RaidableBaseSettingsScheduled
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; }

            [JsonProperty(PropertyName = "Convert PVE To PVP")]
            public bool ConvertPVE { get; set; }

            [JsonProperty(PropertyName = "Convert PVP To PVE")]
            public bool ConvertPVP { get; set; }

            [JsonProperty(PropertyName = "Every Min Seconds")]
            public double IntervalMin { get; set; } = 3600f;

            [JsonProperty(PropertyName = "Every Max Seconds")]
            public double IntervalMax { get; set; } = 7200f;

            [JsonProperty(PropertyName = "Include PVE Bases")]
            public bool IncludePVE { get; set; } = true;

            [JsonProperty(PropertyName = "Include PVP Bases")]
            public bool IncludePVP { get; set; } = true;

            [JsonProperty(PropertyName = "Max Scheduled Events")]
            public int Max { get; set; } = 1;

            [JsonProperty(PropertyName = "Max To Spawn At Once (0 = Use Max Scheduled Events Amount)")]
            public int MaxOnce { get; set; }

            [JsonProperty(PropertyName = "Minimum Required Players Online")]
            public int PlayerLimit { get; set; } = 1;

            [JsonProperty(PropertyName = "Spawn Bases X Distance Apart")]
            public float Distance { get; set; } = 100f;

            [JsonProperty(PropertyName = "Spawns Database File (Optional)")]
            public string SpawnsFile { get; set; } = "none";
        }

        public class RaidableBaseSettingsMaintained
        {
            [JsonProperty(PropertyName = "Always Maintain Max Events")]
            public bool Enabled { get; set; }

            [JsonProperty(PropertyName = "Convert PVE To PVP")]
            public bool ConvertPVE { get; set; }

            [JsonProperty(PropertyName = "Convert PVP To PVE")]
            public bool ConvertPVP { get; set; }

            [JsonProperty(PropertyName = "Include PVE Bases")]
            public bool IncludePVE { get; set; } = true;

            [JsonProperty(PropertyName = "Include PVP Bases")]
            public bool IncludePVP { get; set; } = true;

            [JsonProperty(PropertyName = "Minimum Required Players Online")]
            public int PlayerLimit { get; set; } = 1;

            [JsonProperty(PropertyName = "Max Maintained Events")]
            public int Max { get; set; } = 1;

            [JsonProperty(PropertyName = "Spawn Bases X Distance Apart")]
            public float Distance { get; set; } = 100f;

            [JsonProperty(PropertyName = "Spawns Database File (Optional)")]
            public string SpawnsFile { get; set; } = "none";
        }

        public class RaidableBaseSettingsBuyableCooldowns
        {
            [JsonProperty(PropertyName = "VIP Permission: raidablebases.vipcooldown")]
            public float VIP { get; set; } = 300f;

            [JsonProperty(PropertyName = "Admin Permission: raidablebases.allow")]
            public float Allow { get; set; }

            [JsonProperty(PropertyName = "Server Admins")]
            public float Admin { get; set; }

            [JsonProperty(PropertyName = "Normal Users")]
            public float Cooldown { get; set; } = 600f;

            public float Get(BasePlayer player)
            {
                var cooldowns = new List<float>() { Cooldown };

                if (!cooldowns.Contains(VIP) && Backbone.HasPermission(player.UserIDString, vipPermission))
                {
                    cooldowns.Add(VIP);
                }

                if (!cooldowns.Contains(Allow) && Backbone.HasPermission(player.UserIDString, adminPermission))
                {
                    cooldowns.Add(Allow);
                }

                if (!cooldowns.Contains(Admin) && (player.IsAdmin || player.IsDeveloper || Backbone.HasPermission(player.UserIDString, "fauxadmin.allowed")))
                {
                    cooldowns.Add(Admin);
                }

                if (!cooldowns.Contains(Cooldown))
                {
                    cooldowns.Add(Cooldown);
                }

                return Mathf.Min(cooldowns.ToArray());
            }
        }

        public class RaidableBaseSettingsBuyable
        {
            [JsonProperty(PropertyName = "Cooldowns (0 = No Cooldown)")]
            public RaidableBaseSettingsBuyableCooldowns Cooldowns { get; set; } = new RaidableBaseSettingsBuyableCooldowns();

            [JsonProperty(PropertyName = "Allow Players To Buy PVP Raids")]
            public bool BuyPVP { get; set; }

            [JsonProperty(PropertyName = "Convert PVE To PVP")]
            public bool ConvertPVE { get; set; }

            [JsonProperty(PropertyName = "Convert PVP To PVE")]
            public bool ConvertPVP { get; set; }

            [JsonProperty(PropertyName = "Distance To Spawn Bought Raids From Player")]
            public float DistanceToSpawnFrom { get; set; } = 150f;

            [JsonProperty(PropertyName = "Lock Raid To Buyer And Friends")]
            public bool UsePayLock { get; set; } = true;

            [JsonProperty(PropertyName = "Max Buyable Events")]
            public int Max { get; set; } = 1;

            [JsonProperty(PropertyName = "Reset Purchased Owner After X Minutes Offline")]
            public float ResetDuration { get; set; } = 10f;

            [JsonProperty(PropertyName = "Spawn Bases X Distance Apart")]
            public float Distance { get; set; } = 100f;

            [JsonProperty(PropertyName = "Spawns Database File (Optional)")]
            public string SpawnsFile { get; set; } = "none";
        }

        public class RaidableBaseSettingsManual
        {
            [JsonProperty(PropertyName = "Convert PVE To PVP")]
            public bool ConvertPVE { get; set; }

            [JsonProperty(PropertyName = "Convert PVP To PVE")]
            public bool ConvertPVP { get; set; }

            [JsonProperty(PropertyName = "Max Manual Events")]
            public int Max { get; set; } = 1;

            [JsonProperty(PropertyName = "Spawn Bases X Distance Apart")]
            public float Distance { get; set; } = 100f;

            [JsonProperty(PropertyName = "Spawns Database File (Optional)")]
            public string SpawnsFile { get; set; } = "none";
        }

        public class RaidableBaseSettings
        {
            [JsonProperty(PropertyName = "Buildings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, BuildingOptions> Buildings { get; set; } = new Dictionary<string, BuildingOptions>();
        }

        public class RaidableBaseWallOptions
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty(PropertyName = "Extra Stacks")]
            public int Stacks { get; set; } = 1;

            [JsonProperty(PropertyName = "Use Stone Walls")]
            public bool Stone { get; set; } = true;

            [JsonProperty(PropertyName = "Use Iced Walls")]
            public bool Ice { get; set; }

            [JsonProperty(PropertyName = "Use Least Amount Of Walls")]
            public bool LeastAmount { get; set; } = true;

            [JsonProperty(PropertyName = "Use UFO Walls")]
            public bool UseUFOWalls { get; set; }

            [JsonProperty(PropertyName = "Radius")]
            public float Radius { get; set; } = 25f;
        }

        public class RaidableBaseEconomicsOptions
        {
            [JsonProperty(PropertyName = "Easy")]
            public double Easy { get; set; }

            [JsonProperty(PropertyName = "Medium")]
            public double Medium { get; set; }

            [JsonProperty(PropertyName = "Hard")]
            public double Hard { get; set; }

            [JsonProperty(PropertyName = "Expert")]
            public double Expert { get; set; }

            [JsonProperty(PropertyName = "Nightmare")]
            public double Nightmare { get; set; }

            [JsonIgnore]
            public bool Any
            {
                get
                {
                    return Easy > 0 || Medium > 0 || Hard > 0 || Expert > 0 || Nightmare > 0;
                }
            }
        }

        public class RaidableBaseServerRewardsOptions
        {
            [JsonProperty(PropertyName = "Easy")]
            public int Easy { get; set; }

            [JsonProperty(PropertyName = "Medium")]
            public int Medium { get; set; }

            [JsonProperty(PropertyName = "Hard")]
            public int Hard { get; set; }

            [JsonProperty(PropertyName = "Expert")]
            public int Expert { get; set; }

            [JsonProperty(PropertyName = "Nightmare")]
            public int Nightmare { get; set; }

            [JsonIgnore]
            public bool Any
            {
                get
                {
                    return Easy > 0 || Medium > 0 || Hard > 0 || Expert > 0 || Nightmare > 0;
                }
            }
        }

        public class RankedLadderSettings
        {
            [JsonProperty(PropertyName = "Award Top X Players On Wipe")]
            public int Amount { get; set; } = 3;

            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = true;
        }

        public class RewardSettings
        {
            [JsonProperty(PropertyName = "Economics Money")]
            public double Money { get; set; }

            [JsonProperty(PropertyName = "ServerRewards Points")]
            public int Points { get; set; }
        }

        public class SkinSettings
        {
            [JsonProperty(PropertyName = "Include Workshop Skins")]
            public bool RandomWorkshopSkins { get; set; } = true;

            [JsonProperty(PropertyName = "Preset Skin")]
            public ulong PresetSkin { get; set; }

            [JsonProperty(PropertyName = "Use Random Skin")]
            public bool RandomSkins { get; set; } = true;
        }

        public class TreasureItem
        {
            public string shortname { get; set; }
            public int amount { get; set; }
            public ulong skin { get; set; }
            public int amountMin { get; set; }

            [JsonIgnore]
            public ItemDefinition def { get; set; }

            [JsonIgnore]
            public bool isBlueprint { get; set; }

            [JsonIgnore]
            public int total { get; set; }

            [JsonIgnore]
            public bool modified { get; set; }
        }

        public class TreasureSettings
        {
            [JsonProperty(PropertyName = "Resources Not Moved To Cupboards", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> ExcludeFromCupboard { get; set; } = new List<string>
            {
                "skull.human", "battery.small", "bone.fragments", "can.beans.empty", "can.tuna.empty", "water.salt", "water", "skull.wolf"
            };

            [JsonProperty(PropertyName = "Use Random Skins")]
            public bool RandomSkins { get; set; }

            [JsonProperty(PropertyName = "Include Workshop Skins")]
            public bool RandomWorkshopSkins { get; set; }

            [JsonProperty(PropertyName = "Use Day Of Week Loot")]
            public bool UseDOWL { get; set; }

            [JsonProperty(PropertyName = "Percent Minimum Loss")]
            public decimal PercentLoss { get; set; }

            [JsonProperty(PropertyName = "Percent Increase When Using Day Of Week Loot")]
            public bool Increased { get; set; }

            [JsonProperty(PropertyName = "Day Of Week Loot Monday", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> DOWL_Monday { get; set; } = new List<TreasureItem>();

            [JsonProperty(PropertyName = "Day Of Week Loot Tuesday", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> DOWL_Tuesday { get; set; } = new List<TreasureItem>();

            [JsonProperty(PropertyName = "Day Of Week Loot Wednesday", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> DOWL_Wednesday { get; set; } = new List<TreasureItem>();

            [JsonProperty(PropertyName = "Day Of Week Loot Thursday", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> DOWL_Thursday { get; set; } = new List<TreasureItem>();

            [JsonProperty(PropertyName = "Day Of Week Loot Friday", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> DOWL_Friday { get; set; } = new List<TreasureItem>();

            [JsonProperty(PropertyName = "Day Of Week Loot Saturday", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> DOWL_Saturday { get; set; } = new List<TreasureItem>();

            [JsonProperty(PropertyName = "Day Of Week Loot Sunday", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> DOWL_Sunday { get; set; } = new List<TreasureItem>();

            [JsonProperty(PropertyName = "Percent Increase On Monday")]
            public decimal PercentIncreaseOnMonday { get; set; }

            [JsonProperty(PropertyName = "Percent Increase On Tuesday")]
            public decimal PercentIncreaseOnTuesday { get; set; }

            [JsonProperty(PropertyName = "Percent Increase On Wednesday")]
            public decimal PercentIncreaseOnWednesday { get; set; }

            [JsonProperty(PropertyName = "Percent Increase On Thursday")]
            public decimal PercentIncreaseOnThursday { get; set; }

            [JsonProperty(PropertyName = "Percent Increase On Friday")]
            public decimal PercentIncreaseOnFriday { get; set; }

            [JsonProperty(PropertyName = "Percent Increase On Saturday")]
            public decimal PercentIncreaseOnSaturday { get; set; }

            [JsonProperty(PropertyName = "Percent Increase On Sunday")]
            public decimal PercentIncreaseOnSunday { get; set; }

            [JsonProperty(PropertyName = "Loot (Easy Difficulty)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> LootEasy { get; set; } = new List<TreasureItem>();

            [JsonProperty(PropertyName = "Loot (Medium Difficulty)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> LootMedium { get; set; } = new List<TreasureItem>();

            [JsonProperty(PropertyName = "Loot (Hard Difficulty)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> LootHard { get; set; } = new List<TreasureItem>();

            [JsonProperty(PropertyName = "Loot (Expert Difficulty)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> LootExpert { get; set; } = new List<TreasureItem>();

            [JsonProperty(PropertyName = "Loot (Nightmare Difficulty)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> LootNightmare { get; set; } = new List<TreasureItem>();

            [JsonProperty(PropertyName = "Loot", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> Loot { get; set; } = new List<TreasureItem>();

            [JsonProperty(PropertyName = "Do Not Duplicate Base Loot")]
            public bool UniqueBaseLoot { get; set; }

            [JsonProperty(PropertyName = "Do Not Duplicate Difficulty Loot")]
            public bool UniqueDifficultyLoot { get; set; }

            [JsonProperty(PropertyName = "Do Not Duplicate Default Loot")]
            public bool UniqueDefaultLoot { get; set; }

            [JsonProperty(PropertyName = "Use Stack Size Limit For Spawning Items")]
            public bool UseStackSizeLimit { get; set; }
        }

        public class TruePVESettings
        {
            [JsonProperty(PropertyName = "Allow PVP Server-Wide During Events")]
            public bool ServerWidePVP { get; set; }
        }

        public class UILockoutSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty(PropertyName = "Easy Anchor Min")]
            public string EasyMin { get; set; } = "0.838 0.285";

            [JsonProperty(PropertyName = "Easy Anchor Max")]
            public string EasyMax { get; set; } = "0.883 0.320";

            [JsonProperty(PropertyName = "Medium Anchor Min")]
            public string MediumMin { get; set; } = "0.893 0.285";

            [JsonProperty(PropertyName = "Medium Anchor Max")]
            public string MediumMax { get; set; } = "0.936 0.320";

            [JsonProperty(PropertyName = "Hard Anchor Min")]
            public string HardMin { get; set; } = "0.946 0.285";

            [JsonProperty(PropertyName = "Hard Anchor Max")]
            public string HardMax { get; set; } = "0.986 0.320";

            [JsonProperty(PropertyName = "Panel Alpha")]
            public float Alpha { get; set; } = 1f;
        }

        public class UIBuyableSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty(PropertyName = "Anchor Min")]
            public string Min { get; set; } = "0.522 0.136";

            [JsonProperty(PropertyName = "Anchor Max")]
            public string Max { get; set; } = "0.639 0.372";

            [JsonProperty(PropertyName = "Panel Color")]
            public string PanelColor { get; set; } = "#000000";

            [JsonProperty(PropertyName = "Panel Alpha")]
            public float PanelAlpha { get; set; } = 0f;

            [JsonProperty(PropertyName = "Button Alpha")]
            public float ButtonAlpha { get; set; } = 1f;

            [JsonProperty(PropertyName = "Text Color")]
            public string TextColor { get; set; } = "#FFFFFF";

            [JsonProperty(PropertyName = "Use Contrast Colors For Text Color")]
            public bool Contrast { get; set; }

            [JsonProperty(PropertyName = "Use Difficulty Colors For Buttons")]
            public bool Difficulty { get; set; }

            [JsonProperty(PropertyName = "X Button Color")]
            public string CloseColor { get; set; } = "#497CAF";

            [JsonProperty(PropertyName = "Easy Button Color")]
            public string EasyColor { get; set; } = "#497CAF";

            [JsonProperty(PropertyName = "Medium Button Color")]
            public string MediumColor { get; set; } = "#497CAF";

            [JsonProperty(PropertyName = "Hard Button Color")]
            public string HardColor { get; set; } = "#497CAF";

            [JsonProperty(PropertyName = "Expert Button Color")]
            public string ExpertColor { get; set; } = "#497CAF";

            [JsonProperty(PropertyName = "Nightmare Button Color")]
            public string NightmareColor { get; set; } = "#497CAF";
        }

        public class UISettings
        {
            [JsonProperty(PropertyName = "Buyable UI")]
            public UIBuyableSettings Buyable { get; set; } = new UIBuyableSettings();

            [JsonProperty(PropertyName = "Lockouts")]
            public UILockoutSettings Lockout { get; set; } = new UILockoutSettings();

            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty(PropertyName = "Anchor Min")]
            public string AnchorMin { get; set; } = "0.838 0.249";

            [JsonProperty(PropertyName = "Anchor Max")]
            public string AnchorMax { get; set; } = "0.986 0.284";

            [JsonProperty(PropertyName = "Font Size")]
            public int FontSize { get; set; } = 18;

            [JsonProperty(PropertyName = "Panel Alpha")]
            public float Alpha { get; set; } = 1f;

            [JsonProperty(PropertyName = "Panel Color")]
            public string PanelColor { get; set; } = "#000000";

            [JsonProperty(PropertyName = "PVP Color")]
            public string ColorPVP { get; set; } = "#FF0000";

            [JsonProperty(PropertyName = "PVE Color")]
            public string ColorPVE { get; set; } = "#008000";

            [JsonProperty(PropertyName = "Show Containers Left")]
            public bool Containers { get; set; }

            [JsonProperty(PropertyName = "Show Time Left")]
            public bool Time { get; set; } = true;
        }

        public class WeaponTypeStateSettings
        {
            [JsonProperty(PropertyName = "AutoTurret")]
            public bool AutoTurret { get; set; } = true;

            [JsonProperty(PropertyName = "FlameTurret")]
            public bool FlameTurret { get; set; } = true;

            [JsonProperty(PropertyName = "FogMachine")]
            public bool FogMachine { get; set; } = true;

            [JsonProperty(PropertyName = "GunTrap")]
            public bool GunTrap { get; set; } = true;

            [JsonProperty(PropertyName = "SamSite")]
            public bool SamSite { get; set; } = true;
        }

        public class WeaponTypeAmountSettings
        {
            [JsonProperty(PropertyName = "AutoTurret")]
            public int AutoTurret { get; set; } = 256;

            [JsonProperty(PropertyName = "FlameTurret")]
            public int FlameTurret { get; set; } = 256;

            [JsonProperty(PropertyName = "FogMachine")]
            public int FogMachine { get; set; } = 5;

            [JsonProperty(PropertyName = "GunTrap")]
            public int GunTrap { get; set; } = 128;

            [JsonProperty(PropertyName = "SamSite")]
            public int SamSite { get; set; } = 24;
        }

        public class WeaponSettingsTeslaCoil
        {
            [JsonProperty(PropertyName = "Requires A Power Source")]
            public bool RequiresPower { get; set; } = true;

            [JsonProperty(PropertyName = "Max Discharge Self Damage Seconds (0 = None, 120 = Rust default)")]
            public float MaxDischargeSelfDamageSeconds { get; set; }

            [JsonProperty(PropertyName = "Max Damage Output")]
            public float MaxDamageOutput { get; set; } = 35f;
        }

        public class WeaponSettings
        {
            [JsonProperty(PropertyName = "Infinite Ammo")]
            public WeaponTypeStateSettings InfiniteAmmo { get; set; } = new WeaponTypeStateSettings();

            [JsonProperty(PropertyName = "Ammo")]
            public WeaponTypeAmountSettings Ammo { get; set; } = new WeaponTypeAmountSettings();

            [JsonProperty(PropertyName = "Tesla Coil")]
            public WeaponSettingsTeslaCoil TeslaCoil { get; set; } = new WeaponSettingsTeslaCoil();

            [JsonProperty(PropertyName = "Fog Machine Allows Motion Toggle")]
            public bool FogMotion { get; set; } = true;

            [JsonProperty(PropertyName = "Fog Machine Requires A Power Source")]
            public bool FogRequiresPower { get; set; } = true;

            [JsonProperty(PropertyName = "SamSite Repairs Every X Minutes (0.0 = disabled)")]
            public float SamSiteRepair { get; set; } = 5f;

            [JsonProperty(PropertyName = "SamSite Range (350.0 = Rust default)")]
            public float SamSiteRange { get; set; } = 75f;
        }

        public class Configuration
        {
            [JsonProperty(PropertyName = "Settings")]
            public PluginSettings Settings = new PluginSettings();

            [JsonProperty(PropertyName = "Event Messages")]
            public EventMessageSettings EventMessages = new EventMessageSettings();

            [JsonProperty(PropertyName = "GUIAnnouncements")]
            public GUIAnnouncementSettings GUIAnnouncement = new GUIAnnouncementSettings();

            [JsonProperty(PropertyName = "Lusty Map")]
            public LustyMapSettings LustyMap = new LustyMapSettings();

            [JsonProperty(PropertyName = "Raidable Bases")]
            public RaidableBaseSettings RaidableBases = new RaidableBaseSettings();

            [JsonProperty(PropertyName = "Ranked Ladder")]
            public RankedLadderSettings RankedLadder = new RankedLadderSettings();

            [JsonProperty(PropertyName = "Skins")]
            public SkinSettings Skins = new SkinSettings();

            [JsonProperty(PropertyName = "Treasure")]
            public TreasureSettings Treasure = new TreasureSettings();

            [JsonProperty(PropertyName = "TruePVE")]
            public TruePVESettings TruePVE = new TruePVESettings();

            [JsonProperty(PropertyName = "UI")]
            public UISettings UI = new UISettings();

            [JsonProperty(PropertyName = "Weapons")]
            public WeaponSettings Weapons = new WeaponSettings();
        }

        private bool configLoaded = false;

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<Configuration>();
            }
            catch (JsonException ex)
            {
                Puts(ex.Message);
                PrintError("Your configuration file contains a json error, shown above. Please fix this.");
                return;
            }
            catch (Exception ex)
            {
                Puts(ex.Message);
                LoadDefaultConfig();
            }

            if (_config == null)
            {
                Puts("Config is null");
                LoadDefaultConfig();
            }

            configLoaded = true;

            if (string.IsNullOrEmpty(_config.LustyMap.IconFile) || string.IsNullOrEmpty(_config.LustyMap.IconName))
            {
                _config.LustyMap.Enabled = false;
            }

            if (_config.GUIAnnouncement.TintColor.ToLower() == "black")
            {
                _config.GUIAnnouncement.TintColor = "grey";
            }

            SaveConfig();
            Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.new_backup_1.json");
        }

        private const string rankLadderPermission = "raidablebases.th";
        private const string rankLadderGroup = "raidhunter";
        private const string adminPermission = "raidablebases.allow";
        private const string drawPermission = "raidablebases.ddraw";
        private const string mapPermission = "raidablebases.mapteleport";
        private const string canBypassPermission = "raidablebases.canbypass";
        private const string bypassBlockPermission = "raidablebases.blockbypass";
        private const string banPermission = "raidablebases.banned";
        private const string vipPermission = "raidablebases.vipcooldown";

        public static List<TreasureItem> TreasureLoot
        {
            get
            {
                List<TreasureItem> lootList;

                if (_config.Treasure.UseDOWL && Buildings.WeekdayLoot.TryGetValue(DateTime.Now.DayOfWeek, out lootList) && lootList.Count > 0)
                {
                    return new List<TreasureItem>(lootList);
                }

                if (!Buildings.DifficultyLoot.TryGetValue(LootType.Default, out lootList))
                {
                    Buildings.DifficultyLoot[LootType.Default] = lootList = new List<TreasureItem>();
                }

                return lootList;
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
            Puts("Loaded default configuration file");
        }

        #endregion

        #region UI

        public class UI // Credits: Absolut & k1lly0u
        {
            private static CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = false, string parent = "Overlay")
            {
                var NewElement = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image =
                            {
                                Color = color
                            },
                            RectTransform =
                            {
                                AnchorMin = aMin,
                                AnchorMax = aMax
                            },
                            CursorEnabled = cursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return NewElement;
            }

            public static void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter, string labelColor = "")
            {
                container.Add(new CuiButton
                {
                    Button =
                        {
                            Color = color,
                            Command = command,
                            FadeIn = 1.0f
                        },
                    RectTransform =
                        {
                            AnchorMin = aMin,
                            AnchorMax = aMax
                        },
                    Text =
                        {
                            Text = text,
                            FontSize = size,
                            Align = align,
                            Color = labelColor
                        }
                },
                    panel);
            }

            private static void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text =
                    {
                        Color = color,
                        FontSize = size,
                        Align = align,
                        FadeIn = 1.0f,
                        Text = text
                    },
                    RectTransform =
                    {
                        AnchorMin = aMin,
                        AnchorMax = aMax
                    }
                },
                panel);
            }

            private static string GetContrast(string hexColor)
            {
                if (!_config.UI.Buyable.Contrast)
                {
                    return Color(_config.UI.Buyable.TextColor);
                }

                hexColor = hexColor.TrimStart('#');
                int r = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int g = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int b = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                var color = ((r * 299) + (g * 587) + (b * 114)) / 1000 >= 128 ? "0 0 0 1" : "1 1 1 1";
                return color;

            }

            private static string Color(string hexColor, float a = 1.0f)
            {
                a = Mathf.Clamp(a, 0f, 1f);
                hexColor = hexColor.TrimStart('#');
                int r = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int g = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int b = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {a}";
            }

            public static void DestroyStatusUI(BasePlayer player)
            {
                if (player.IsValid() && player.IsConnected && Players.ContainsKey(player))
                {
                    CuiHelper.DestroyUi(player, StatusPanelName);
                    Players.Remove(player);
                }
            }

            public static void DestroyLockoutUI(BasePlayer player)
            {
                if (player.IsValid() && player.IsConnected && Lockouts.Contains(player))
                {
                    CuiHelper.DestroyUi(player, EasyPanelName);
                    CuiHelper.DestroyUi(player, MediumPanelName);
                    CuiHelper.DestroyUi(player, HardPanelName);
                    Lockouts.Remove(player);
                }
            }

            public static void DestroyAllLockoutUI()
            {
                foreach (var player in new List<BasePlayer>(Lockouts))
                {
                    DestroyLockoutUI(player);
                }

                Lockouts.Clear();
            }

            public static void DestroyAllBuyableUI()
            {
                foreach (var player in Buyables)
                {
                    if (player == null || !player.IsConnected) continue;

                    CuiHelper.DestroyUi(player, "Buyable_UI");
                }
            }

            public static void CreateBuyableUI(BasePlayer player)
            {
                if (Buyables.Contains(player))
                {
                    CuiHelper.DestroyUi(player, "Buyable_UI");
                    Buyables.Remove(player);
                }

                if (!_config.UI.Buyable.Enabled)
                {
                    return;
                }

                var disabled = "#808080";
                var element = CreateElementContainer("Buyable_UI", Color(_config.UI.Buyable.PanelColor, _config.UI.Buyable.PanelAlpha), _config.UI.Buyable.Min, _config.UI.Buyable.Max, false, "Hud");
                var buyRaids = Backbone.Plugin.lang.GetMessage("Buy Raids", Backbone.Plugin, player.UserIDString);
                var easy = _config.Settings.ServerRewards.Easy > 0 ? string.Format("{0} ({1} RP)", Backbone.Easy.SentenceCase(), _config.Settings.ServerRewards.Easy) : _config.Settings.Economics.Easy > 0 ? string.Format("{0} (${1})", Backbone.Easy.SentenceCase(), _config.Settings.Economics.Easy) : null;
                var medium = _config.Settings.ServerRewards.Medium > 0 ? string.Format("{0} ({1} RP)", Backbone.Medium.SentenceCase(), _config.Settings.ServerRewards.Medium) : _config.Settings.Economics.Medium > 0 ? string.Format("{0} (${1})", Backbone.Medium.SentenceCase(), _config.Settings.Economics.Medium) : null;
                var hard = _config.Settings.ServerRewards.Hard > 0 ? string.Format("{0} ({1} RP)", Backbone.Hard.SentenceCase(), _config.Settings.ServerRewards.Hard) : _config.Settings.Economics.Hard > 0 ? string.Format("{0} (${1})", Backbone.Hard.SentenceCase(), _config.Settings.Economics.Hard) : null;
                var expert = _config.Settings.ServerRewards.Expert > 0 ? string.Format("{0} ({1} RP)", Backbone.Expert.SentenceCase(), _config.Settings.ServerRewards.Expert) : _config.Settings.Economics.Expert > 0 ? string.Format("{0} (${1})", Backbone.Expert.SentenceCase(), _config.Settings.Economics.Expert) : null;
                var nightmare = _config.Settings.ServerRewards.Nightmare > 0 ? string.Format("{0} ({1} RP)", Backbone.Nightmare.SentenceCase(), _config.Settings.ServerRewards.Nightmare) : _config.Settings.Economics.Nightmare > 0 ? string.Format("{0} (${1})", Backbone.Nightmare.SentenceCase(), _config.Settings.Economics.Nightmare) : null;

                CreateLabel(ref element, "Buyable_UI", "1 1 1 1", buyRaids, 14, "0.02 0.865", "0.447 0.959");
                CreateButton(ref element, "Buyable_UI", Color(_config.UI.Buyable.CloseColor, _config.UI.Buyable.ButtonAlpha), "X", 14, "0.833 0.835", "1 0.982", "ui_buyraid closeui", TextAnchor.MiddleCenter, _config.UI.Buyable.TextColor);
                CreateButton(ref element, "Buyable_UI", Color(easy == null ? disabled : _config.UI.Buyable.Difficulty ? "#008000" : _config.UI.Buyable.EasyColor, _config.UI.Buyable.ButtonAlpha), easy ?? Backbone.Easy.SentenceCase(), 14, "0 0.665", "1 0.812", easy == null ? "ui_buyraid closeui" : "ui_buyraid 0", TextAnchor.MiddleCenter, GetContrast(easy == null ? disabled : _config.UI.Buyable.Difficulty ? "#008000" : _config.UI.Buyable.EasyColor));
                CreateButton(ref element, "Buyable_UI", Color(medium == null ? disabled : _config.UI.Buyable.Difficulty ? "#FFFF00" : _config.UI.Buyable.MediumColor, _config.UI.Buyable.ButtonAlpha), medium ?? Backbone.Medium.SentenceCase(), 14, "0 0.5", "1 0.647", medium == null ? "ui_buyraid closeui" : "ui_buyraid 1", TextAnchor.MiddleCenter, GetContrast(medium == null ? disabled : _config.UI.Buyable.Difficulty ? "#FFFF00" : _config.UI.Buyable.MediumColor));
                CreateButton(ref element, "Buyable_UI", Color(hard == null ? disabled : _config.UI.Buyable.Difficulty ? "#FF0000" : _config.UI.Buyable.HardColor, _config.UI.Buyable.ButtonAlpha), hard ?? Backbone.Hard.SentenceCase(), 14, "0 0.335", "1 0.482", hard == null ? "ui_buyraid closeui" : "ui_buyraid 2", TextAnchor.MiddleCenter, GetContrast(hard == null ? disabled : _config.UI.Buyable.Difficulty ? "#FF0000" : _config.UI.Buyable.HardColor));
                CreateButton(ref element, "Buyable_UI", Color(expert == null ? disabled : _config.UI.Buyable.Difficulty ? "#0000FF" : _config.UI.Buyable.ExpertColor, _config.UI.Buyable.ButtonAlpha), expert ?? Backbone.Expert.SentenceCase(), 14, "0 0.171", "1 0.318", expert == null ? "ui_buyraid closeui" : "ui_buyraid 3", TextAnchor.MiddleCenter, GetContrast(expert == null ? disabled : _config.UI.Buyable.Difficulty ? "#0000FF" : _config.UI.Buyable.ExpertColor));
                CreateButton(ref element, "Buyable_UI", Color(nightmare == null ? disabled : _config.UI.Buyable.Difficulty ? "#000000" : _config.UI.Buyable.NightmareColor, _config.UI.Buyable.ButtonAlpha), nightmare ?? Backbone.Nightmare.SentenceCase(), 14, "0 0.006", "1 0.153", nightmare == null ? "ui_buyraid closeui" : "ui_buyraid 4", TextAnchor.MiddleCenter, GetContrast(nightmare == null ? disabled : _config.UI.Buyable.Difficulty ? "#000000" : _config.UI.Buyable.NightmareColor));

                CuiHelper.AddUi(player, element);
                Buyables.Add(player);
            }

            private static void Create(BasePlayer player, RaidableBase raid, string panelName, string text, string color, string panelColor, string aMin, string aMax)
            {
                var element = CreateElementContainer(panelName, panelColor, aMin, aMax, false, "Hud");

                CreateLabel(ref element, panelName, Color(color), text, _config.UI.FontSize, "0 0", "1 1");
                CuiHelper.AddUi(player, element);

                if (!Players.ContainsKey(player))
                {
                    Players.Add(player, raid);
                }
            }

            private static void Create(BasePlayer player, string panelName, string text, string color, string panelColor, string aMin, string aMax)
            {
                var element = CreateElementContainer(panelName, panelColor, aMin, aMax, false, "Hud");

                CreateLabel(ref element, panelName, Color(color), text, _config.UI.FontSize, "0 0", "1 1");
                CuiHelper.AddUi(player, element);

                if (!Lockouts.Contains(player))
                {
                    Lockouts.Add(player);
                }
            }

            private static void ShowStatus(RaidableBase raid, BasePlayer player)
            {
                string zone = raid.AllowPVP ? Backbone.GetMessage("PVP ZONE") : Backbone.GetMessage("PVE ZONE");
                int lootAmount = 0;
                float seconds = raid.despawnTime - Time.realtimeSinceStartup;
                string despawnText = _config.Settings.Management.DespawnMinutesInactive > 0 && seconds > 0 ? Math.Floor(TimeSpan.FromSeconds(seconds).TotalMinutes).ToString() : null;
                string text;

                foreach (var x in raid._containers)
                {
                    if (x == null || x.IsDestroyed)
                    {
                        continue;
                    }

                    if (IsBox(x.prefabID) || _config.Settings.Management.RequireCupboardLooted && x is BuildingPrivlidge)
                    {
                        lootAmount += x.inventory.itemList.Count;
                    }
                }

                if (_config.UI.Containers && _config.UI.Time && !string.IsNullOrEmpty(despawnText))
                {
                    text = Backbone.GetMessage("UIFormat", null, zone, lootAmount, despawnText);
                }
                else if (_config.UI.Containers)
                {
                    text = Backbone.GetMessage("UIFormatContainers", null, zone, lootAmount);
                }
                else if (_config.UI.Time && !string.IsNullOrEmpty(despawnText))
                {
                    text = Backbone.GetMessage("UIFormatMinutes", null, zone, despawnText);
                }
                else text = zone;

                Create(player, raid, StatusPanelName, text, raid.AllowPVP ? _config.UI.ColorPVP : _config.UI.ColorPVE, Color(_config.UI.PanelColor, _config.UI.Alpha), _config.UI.AnchorMin, _config.UI.AnchorMax);
            }

            private static void ShowLockouts(BasePlayer player)
            {
                Lockout lo;
                if (!Backbone.Data.Lockouts.TryGetValue(player.UserIDString, out lo))
                {
                    Backbone.Data.Lockouts[player.UserIDString] = lo = new Lockout();
                }

                double easyTime = RaidableBase.GetLockoutTime(RaidableMode.Easy, lo, player.UserIDString);
                double mediumTime = RaidableBase.GetLockoutTime(RaidableMode.Medium, lo, player.UserIDString);
                double hardTime = RaidableBase.GetLockoutTime(RaidableMode.Hard, lo, player.UserIDString);

                if (easyTime <= 0 && mediumTime <= 0 && hardTime <= 0)
                {
                    return;
                }

                string easy = Math.Floor(TimeSpan.FromSeconds(easyTime).TotalMinutes).ToString();
                string medium = Math.Floor(TimeSpan.FromSeconds(mediumTime).TotalMinutes).ToString();
                string hard = Math.Floor(TimeSpan.FromSeconds(hardTime).TotalMinutes).ToString();
                string green = Color("#008000", _config.UI.Lockout.Alpha);
                string yellow = Color("#FFFF00", _config.UI.Lockout.Alpha);
                string red = Color("#FF0000", _config.UI.Lockout.Alpha);

                Create(player, EasyPanelName, Backbone.GetMessage("UIFormatLockoutMinutes", null, easy), "#000000", green, _config.UI.Lockout.EasyMin, _config.UI.Lockout.EasyMax);
                Create(player, MediumPanelName, Backbone.GetMessage("UIFormatLockoutMinutes", null, medium), "#000000", yellow, _config.UI.Lockout.MediumMin, _config.UI.Lockout.MediumMax);
                Create(player, HardPanelName, Backbone.GetMessage("UIFormatLockoutMinutes", null, hard), "#000000", red, _config.UI.Lockout.HardMin, _config.UI.Lockout.HardMax);
            }

            public static void Update(RaidableBase raid, BasePlayer player)
            {
                if (_config == null || raid == null || player == null)
                {
                    return;
                }

                if (!player.IsConnected)
                {
                    Players.Remove(player);
                    return;
                }

                if (!_config.UI.Enabled || !raid.intruders.Contains(player))
                {
                    DestroyStatusUI(player);
                    DestroyLockoutUI(player);
                    return;
                }

                var uii = Get(player.UserIDString);

                if (!uii.Enabled || (!uii.Status && !uii.Lockouts))
                {
                    return;
                }

                DestroyStatusUI(player);

                if (uii.Status)
                {
                    ShowStatus(raid, player);
                }

                if (uii.Lockouts)
                {
                    Update(player, false);
                }

                player.Invoke(() => Update(raid, player), 60f);
            }

            public static void Update(BasePlayer player, bool invoke = true)
            {
                if (_config == null || (!_config.UI.Lockout.Enabled && !Lockouts.Contains(player)))
                {
                    return;
                }

                if (!IsValid(player) || !player.IsConnected)
                {
                    Lockouts.Remove(player);
                    return;
                }

                if (!_config.UI.Enabled)
                {
                    DestroyLockoutUI(player);
                    return;
                }

                var uii = Get(player.UserIDString);

                if (!uii.Enabled || !uii.Lockouts || !_config.UI.Lockout.Enabled)
                {
                    DestroyLockoutUI(player);
                    return;
                }

                DestroyLockoutUI(player);
                ShowLockouts(player);

                if (invoke) player.Invoke(() => Update(player), 60f);
            }

            public static void Update(RaidableBase raid)
            {
                var _players = new Dictionary<BasePlayer, RaidableBase>(Players);

                foreach (var player in _players)
                {
                    if (!player.Key.IsValid() || !player.Key.IsConnected || player.Value == null)
                    {
                        Players.Remove(player.Key);
                    }
                    else if (player.Value == raid)
                    {
                        Update(raid, player.Key);
                    }
                }

                _players.Clear();
            }

            public static Info Get(string playerId)
            {
                Info uii;
                if (!Backbone.Data.UI.TryGetValue(playerId, out uii))
                {
                    Backbone.Data.UI[playerId] = uii = new UI.Info();
                }

                return uii;
            }

            private const string StatusPanelName = "RB_UI_Status";
            private const string EasyPanelName = "RB_UI_Easy";
            private const string MediumPanelName = "RB_UI_Medium";
            private const string HardPanelName = "RB_UI_Hard";

            public static Dictionary<BasePlayer, RaidableBase> Players { get; set; } = new Dictionary<BasePlayer, RaidableBase>();
            public static List<BasePlayer> Lockouts { get; set; } = new List<BasePlayer>();
            public static List<BasePlayer> Buyables { get; set; } = new List<BasePlayer>();

            public class Info
            {
                public bool Enabled { get; set; } = true;
                public bool Lockouts { get; set; } = true;
                public bool Status { get; set; } = true;
            }
        }

        private void CommandUI(IPlayer p, string command, string[] args)
        {
            if (p.IsServer)
            {
                return;
            }

            var uii = UI.Get(p.Id);
            var player = p.Object as BasePlayer;
            var raid = RaidableBase.Get(player.transform.position);

            if (args.Length == 0)
            {
                uii.Enabled = !uii.Enabled;
                if (!uii.Enabled)
                {
                    UI.DestroyStatusUI(player);
                    UI.DestroyLockoutUI(player);
                }
                else
                {
                    UI.Update(raid, player);
                    UI.Update(player, false);
                }
                return;
            }

            switch (args[0].ToLower())
            {
                case "lockouts":
                    {
                        uii.Lockouts = !uii.Lockouts;
                        UI.Update(player, uii.Lockouts);
                        return;
                    }
                case "status":
                    {
                        uii.Status = !uii.Status;
                        UI.Update(raid, player);
                        return;
                    }
            }
        }

        #endregion UI
    }
}
