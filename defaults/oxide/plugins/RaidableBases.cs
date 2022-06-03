/*
*  <----- End-User License Agreement ----->
*  
*  You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of This Software without the Developer’s consent
*  
*  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
*  THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS 
*  BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
*  GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT 
*  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*
*  Developer: nivex (mswenson82@yahoo.com)
*
*  Copyright © 2021 nivex
*/

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
using Oxide.Plugins.RaidableBasesExtensionMethods;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Oxide.Plugins
{
    [Info("Raidable Bases", "nivex", "2.4.5")]
    [Description("Create fully automated raidable bases with npcs.")]
    class RaidableBases : RustPlugin
    {
        [PluginReference]
        private Plugin
            DangerousTreasures, ZoneManager, IQEconomic, Economics, ServerRewards, GUIAnnouncements, CopyPaste, AdvancedAlerts,
            Friends, Clans, Kits, TruePVE, NightLantern, Wizardry, NextGenPVE, Imperium, Backpacks, BaseRepair, Notify, SkillTree;

        private new const string Name = "RaidableBases";
        private const int targetMask = Layers.Mask.World | Layers.Mask.Terrain | Layers.Mask.Default;
        private const int visibleMask = Layers.Mask.Deployed | Layers.Mask.Construction | targetMask;
        private const int ejectMask = Layers.Mask.Default | Layers.Mask.Water | Layers.Solid;
        private static RaidableBases Instance { get; set; }
        private ScheduledController Scheduled { get; set; }
        private MaintainedController Maintained { get; set; }
        private float OceanLevel { get; set; }
        private RotationCycle Cycle { get; set; } = new RotationCycle();
        public List<RaidableBase> Raids { get; } = new List<RaidableBase>();
        public Dictionary<ulong, RaidableBase> Npcs { get; set; } = new Dictionary<ulong, RaidableBase>();
        public Dictionary<ulong, DelaySettings> PvpDelay { get; } = new Dictionary<ulong, DelaySettings>();
        public Dictionary<ulong, HumanoidBrain> HumanoidBrains { get; set; } = new Dictionary<ulong, HumanoidBrain>();
        private Dictionary<string, SkinInfo> Skins { get; } = new Dictionary<string, SkinInfo>();
        private Dictionary<ulong, BuyableInfo> BuyCooldowns { get; set; } = new Dictionary<ulong, BuyableInfo>();
        private Dictionary<uint, BMGELEVATOR> _elevators { get; set; } = new Dictionary<uint, BMGELEVATOR>();
        private Dictionary<ulong, float> BlacklistTimers { get; set; } = new Dictionary<ulong, float>();
        public static StoredData data { get; set; } = new StoredData();
        private static StringBuilder _sb { get; set; }
        private static StringBuilder _sb2 { get; set; }
        private bool wiped { get; set; }
        private bool buyableEnabled { get; set; }
        private float lastSpawnRequestTime { get; set; }
        private bool IsUnloading { get; set; }
        private bool bypassRestarting { get; set; }
        private Dictionary<string, float> tryBuyCooldowns { get; set; }
        private BuildingTables Buildings { get; set; }
        private bool DebugMode { get; set; }
        private List<RandomBase> Locations { get; set; } = new List<RandomBase>();
        private Dictionary<string, ItemDefinition> _definitions { get; set; } = new Dictionary<string, ItemDefinition>();
        private Dictionary<Vector3, float> LoadingTimes { get; set; } = new Dictionary<Vector3, float>();
        private List<string> Helms { get; set; } = new List<string> { "hat.wolf", "attire.hide.helterneck", "hat.beenie", "hat.boonie", "bucket.helmet", "burlap.headwrap", "hat.candle", "hat.cap", "clatter.helmet", "coffeecan.helmet", "deer.skull.mask", "heavy.plate.helmet", "hat.miner", "partyhat", "riot.helmet", "wood.armor.helmet", "mask.balaclava", "mask.bandana", "metal.facemask", "nightvisiongoggles", "hat.dragonmask", "hat.ratmask", "attire.nesthat" };
        private List<string> Boots { get; set; } = new List<string> { "boots.frog", "shoes.boots", "burlap.shoes", "attire.hide.boots" };
        private List<string> Gloves { get; set; } = new List<string> { "burlap.gloves.new", "burlap.gloves", "roadsign.gloves", "tactical.gloves" };
        private List<string> Vests { get; set; } = new List<string> { "bone.armor.suit", "heavy.plate.jacket", "jacket.snow", "jacket", "wood.armor.jacket", "attire.hide.poncho", "metal.plate.torso", "roadsign.jacket" };
        private List<string> Legs { get; set; } = new List<string> { "burlap.trousers", "heavy.plate.pants", "attire.hide.pants", "pants.shorts", "wood.armor.pants", "pants" };
        private List<string> Shirts { get; set; } = new List<string> { "hoodie", "burlap.shirt", "shirt.collared", "attire.hide.vest", "shirt.tanktop", "tshirt.long", "tshirt" };
        private List<string> Other { get; set; } = new List<string> { "movembermoustachecard", "movembermoustache", "sunglasses02black", "sunglasses02camo", "sunglasses02red", "sunglasses03black", "sunglasses03chrome", "sunglasses03gold", "sunglasses", "twitchsunglasses", "gloweyes", "attire.bunnyears" };
        private List<string> Furnaces { get; set; } = new List<string> { "furnace.large", "furnace", "furnace_static" };
        private List<string> BBQs { get; set; } = new List<string> { "bbq", "bbq.deployed", "bbq.static" };
        private List<string> Lanterns { get; set; } = new List<string> { "chineselantern.deployed", "jackolantern.angry", "jackolantern.happy", "lantern.deployed", "tunalight.deployed" };
        private List<string> Refineries { get; set; } = new List<string> { "refinery_small_deployed", "small_refinery_static" };
        private List<uint> ExcludedMounts { get; set; } = new List<uint> { 3552983236, 4218596772, 1845856065, 1992774774, 629849447, 4267988016, 1418740895, 2493676858, 3814928951, 1980628900, 703403829, 3061223907, 113644298, 3691382632, 3858860623, 286221745, 2230162530, 51176708, 3363531184, 3224878175 };
        private List<string> Blocks { get; set; } = new List<string> { "wall.doorway", "wall", "wall.frame", "wall.half", "wall.low", "wall.window", "foundation.triangle", "foundation", "wall.external.high.wood", "wall.external.high.stone", "wall.external.high.ice", "floor.triangle.frame", "floor.triangle", "floor.frame" };
        private List<string> TrueDamage { get; set; } = new List<string> { "spikes.floor", "barricade.metal", "barricade.woodwire", "barricade.wood", "wall.external.high.wood", "wall.external.high.stone", "wall.external.high.ice" };
        private List<string> arguments { get; set; } = new List<string> { "add", "remove", "list", "clean", "easy", "med", "medium", "hard", "expert", "nightmare", "0", "1", "2", "3", "4" };
        private List<RaidableMode> RaidableModes { get; set; } = new List<RaidableMode> { RaidableMode.Easy, RaidableMode.Medium, RaidableMode.Hard, RaidableMode.Expert, RaidableMode.Nightmare };
        private SkinSettingsImportedWorkshop ImportedWorkshopSkins { get; set; }

        private const float M_RADIUS = 25f;
        private const float CELL_SIZE = M_RADIUS / 2f;

        private List<RankedRecord> RankedRecords = new List<RankedRecord>
        {
            new RankedRecord("raidablebases.ladder.easy", "raideasy", RaidableMode.Easy),
            new RankedRecord("raidablebases.ladder.medium", "raidmedium", RaidableMode.Medium),
            new RankedRecord("raidablebases.ladder.hard", "raidhard", RaidableMode.Hard),
            new RankedRecord("raidablebases.ladder.expert", "raidexpert", RaidableMode.Expert),
            new RankedRecord("raidablebases.ladder.nightmare", "raidnightmare", RaidableMode.Nightmare),
            new RankedRecord("raidablebases.th", "raidhunter", RaidableMode.Points),
        };

        public enum EjectCode { Awaken, Banned, Enter, Exceeds, FauxAdmin, Hogging, Lockout, Mounted, Protector, Scavenging, Sleeper, Spawning, Teleported, None }

        public enum RaidableType { None, Manual, Scheduled, Purchased, Maintained, Grid }

        public enum RaidableMode { Disabled = -1, Easy = 0, Medium = 1, Hard = 2, Expert = 3, Nightmare = 4, Points = 8888, Random = 9999 }

        public enum AlliedType { All, Clan, Friend, Team }

        public enum CacheType { Close, Delete, Generic, Temporary, Privilege, Submerged }

        public enum ConstructionType { Barricade, Ladder, Any }

        public enum LootType { Easy, Medium, Hard, Expert, Nightmare, Default }

        public enum LootFileType { Base, Difficulty, Default, Weekday }

        public enum SpawnResult { Failure, Transfer, Success, Skipped }

        public class StoredData
        {
            [JsonProperty(PropertyName = "PlayerLockouts")]
            public Dictionary<string, Lockout> Lockouts { get; } = new Dictionary<string, Lockout>();
            public Dictionary<string, PlayerInfo> Players { get; set; } = new Dictionary<string, PlayerInfo>();
            public Dictionary<string, UI.Info> UI { get; set; } = new Dictionary<string, UI.Info>();
            public List<uint> TemporaryUID { get; set; } = new List<uint>();
            public string RaidTime { get; set; } = DateTime.MinValue.ToString();
            public int TotalEvents { get; set; }
            public StoredData() { }
            public static void Load(PluginTimers timer)
            {
                try { data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name); } catch { }

                if (data.IsNull()) data = new StoredData();
                if (data.UI.IsNull()) data.UI = new Dictionary<string, UI.Info>();
                if (data.Players.IsNull()) data.Players = new Dictionary<string, PlayerInfo>();
                if (data.TemporaryUID.Count > 0)
                {
                    var uids = data.TemporaryUID.ToList();
                    timer.Once(30f, () => CleanupTemporaryFiles(uids));
                }
            }
            public void Save()
            {
                data.Lockouts.RemoveAll((userid, lo) => !lo.Any());
                data.UI.RemoveAll((userid, uii) => uii.All());
                data.Players.RemoveAll((userid, playerInfo) => playerInfo.TotalRaids == 0 || playerInfo.IsExpired());
                RaidableBase.TryInvokeMethod(() => Interface.Oxide.DataFileSystem.WriteObject(Name, data));
            }
            private static void CleanupTemporaryFiles(List<uint> uids)
            {
                var entities = new List<BaseEntity>();
                foreach (uint uid in uids)
                {
                    data.TemporaryUID.Remove(uid);
                    var entity = BaseNetworkable.serverEntities.Find(uid) as BaseEntity;
                    if (entity.IsKilled()) continue;
                    entities.Add(entity);
                }
                if (entities.Count == 0) return;
                var undo = new UndoSettings(entities, 3, true);
                Instance.UndoLoop(undo, 0, 1, Time.time);
            }
        }

        public class RandomBase
        {
            public string BaseName;
            public Vector3 Position;
            public BaseProfile Profile;
            public RaidableType Type;
            public RaidableSpawns spawns;
            public List<Payment> payments;
            public float Radius;
            public bool hasSpawned;

            public int BaseIndex => Position.GetHashCode();

            public RandomBase() { }

            public RandomBase(string BaseName, BaseProfile Profile, Vector3 Position, RaidableType Type, RaidableSpawns spawns, List<Payment> payments)
            {
                this.BaseName = BaseName;
                this.Position = Position;
                this.Profile = Profile;
                this.Type = Type;
                this.spawns = spawns;
                this.payments = payments;
                this.Radius = Profile.Options.ProtectionRadius(Type);
            }
            public void InitializeController()
            {
                //TODO: implement eject functionality before base spawns
            }
        }

        public class BackpackData
        {
            public DroppedItemContainer backpack;
            public BasePlayer player;
            public ulong userID;
        }

        public class BuyableInfo
        {
            public float Time;
            public Timer Timer;
        }

        public class DelaySettings
        {
            public RaidableBase RaidableBase;
            public Timer Timer;
            public bool AllowPVP;
        }

        public class MountInfo
        {
            public Vector3 position;
            public float radius;
            public BaseMountable mountable;
        }

        public class SkinInfo
        {
            public List<ulong> skins = new List<ulong>();
            public List<ulong> workshopSkins = new List<ulong>();
            public List<ulong> importedSkins = new List<ulong>();
            public List<ulong> allSkins = new List<ulong>();
        }

        public class Lockout
        {
            public double Easy, Medium, Hard, Expert, Nightmare;

            public bool Any() => Easy > 0 || Medium > 0 || Hard > 0 || Expert > 0 || Nightmare > 0;
        }

        public class RankedRecord
        {
            public string Permission;
            public string Group;
            public RaidableMode Mode;

            public RankedRecord(string permission, string group, RaidableMode mode)
            {
                Permission = permission;
                Group = group;
                Mode = mode;
            }
        }

        public class Elevation
        {
            public float Min;
            public float Max;
        }

        public class RaidableSpawnLocation
        {
            public List<Vector3> Surroundings = new List<Vector3>();
            public Elevation Elevation = new Elevation();
            public Vector3 Location = Vector3.zero;
            public float WaterHeight;
            public float TerrainHeight;
            public float SpawnHeight;
            public float Radius;
            public bool AutoHeight;

            public RaidableSpawnLocation(Vector3 Location)
            {
                this.Location = Location;
            }
        }

        public class MonumentInfoEx
        {
            public float size;
            public Bounds bounds;
            public string translated;
            public Transform transform;
            public MonumentInfo monument;

            public MonumentInfoEx(MonumentInfo monument)
            {
                this.monument = monument;
                transform = monument.transform;
                translated = Translated();
                size = Size();
                bounds = new Bounds(monument.Bounds.center, new Vector3(size, size, size));

                if (config.Settings.Management.MonumentDistance > 0f)
                {
                    bounds.Expand(config.Settings.Management.MonumentDistance);
                }
            }

            public bool IsInBounds(Vector3 target)
            {
                return monument.IsInBounds(target) || new OBB(monument.transform.position, monument.transform.rotation, bounds).Contains(target);
            }

            private float Size()
            {
                if (string.IsNullOrEmpty(monument.displayPhrase.translated.TrimEnd()))
                {
                    return monument.name.Contains("power_sub") ? 50f : monument.name.Contains("cave") ? 75f : monument.name.Contains("OilrigAI") ? 225f : Mathf.Max(monument.Bounds.size.Max(), 75f);
                }

                switch (monument.displayPhrase.translated.TrimEnd())
                {
                    case "power_sub_small_1": case "power_sub_small_2": case "power_sub_big_1": case "power_sub_big_2": case "Stone Quarry": case "Sulfur Quarry": case "Water Well": case "Wild Swamp": case "HQM Quarry": return 50f;
                    case "Abandoned Cabins": case "Abandoned Supermarket": case "Fishing Village": case "Large Fishing Village": case "Lighthouse": case "Mining Outpost": case "Ice Lake": case "The Dome": case "Oxum's Gas Station": return 100f;
                    case "Military Tunnel": case "Sewer Branch": case "Satellite Dish": case "Junkyard": case "Underwater Lab": return 125f;
                    case "Harbor": case "Train Yard": case "Power Plant": return 150f;
                    case "Barn": case "Large Barn": case "Ranch": return 200f;
                    case "Airfield": case "Oil Rig": case "Large Oil Rig": case "Water Treatment Plant": case "Giant Excavator Pit": return 225f;
                    case "Bandit Camp": case "Outpost": return 350f;
                    case "Launch Site": return 300f;
                }

                return monument.Bounds.size.Max() > 0f ? monument.Bounds.size.Max() : 100f;
            }

            private string Translated()
            {
                if (!string.IsNullOrEmpty(monument.displayPhrase.translated.TrimEnd()))
                {
                    return monument.displayPhrase.translated.TrimEnd();
                }
                else if (monument.name.Contains("Oilrig"))
                {
                    return "Oil Rig";
                }
                else if (monument.name.Contains("cave"))
                {
                    return "Cave";
                }
                else if (monument.name.Contains("power_sub"))
                {
                    return "Power Sub Station";
                }

                return "Unknown Monument";
            }
        }

        public class ZoneInfo
        {
            public Vector3 Position;
            public Vector3 Size;
            public float Distance;
            public OBB OBB;

            public ZoneInfo(object location, object radius, object size)
            {
                Position = (Vector3)location;

                if (radius is float)
                {
                    Distance = (float)radius + M_RADIUS + config.Settings.ZoneDistance;
                }

                if (size is Vector3)
                {
                    Size = (Vector3)size;
                }

                var bounds = new Bounds(Position, Size);

                if (config.Settings.ZoneDistance > 0f)
                {
                    bounds.Expand(config.Settings.ZoneDistance);
                }

                OBB = new OBB(Position, default(Quaternion), bounds);
            }
        }

        public class BaseProfile
        {
            public List<LootItem> BaseLootList { get; set; } = new List<LootItem>();

            public BuildingOptions Options { get; set; } = new BuildingOptions();

            public RaidableSpawns Spawns { get; set; } = new RaidableSpawns();

            public string Name { get; set; }

            public BaseProfile()
            {
                Options.AdditionalBases = new Dictionary<string, List<PasteOption>>();
            }

            public BaseProfile(BuildingOptions options, string name)
            {
                Options = options;
                Name = name;
            }

            public static BaseProfile Clone(BaseProfile profile)
            {
                return profile.MemberwiseClone() as BaseProfile;
            }
        }

        private class BuildingTables
        {
            public Dictionary<LootType, List<LootItem>> DifficultyLootLists { get; set; } = new Dictionary<LootType, List<LootItem>>();
            public Dictionary<DayOfWeek, List<LootItem>> WeekdayLootLists { get; set; } = new Dictionary<DayOfWeek, List<LootItem>>();
            public Dictionary<string, BaseProfile> Profiles { get; set; } = new Dictionary<string, BaseProfile>();
            public bool IsConfigured(string baseName) => Profiles.Exists(m => m.Key == baseName || m.Value.Options.AdditionalBases.ContainsKey(baseName));
        }

        public class UndoSettings
        {
            public List<BaseEntity> Entities;
            public object[] hookObjects;
            public bool Structures;
            public bool Deployables;
            public bool Mounts;
            public bool Teleport;
            public int Limit;
            public UndoSettings(List<BaseEntity> entities, int limit = 1, bool mounts = false, bool structures = false, bool deployables = false, bool teleport = false, object[] ho = null)
            {
                Entities = entities;
                Limit = limit;
                Structures = structures;
                Deployables = deployables;
                Mounts = mounts;
                Teleport = teleport;
                hookObjects = ho;
            }
        }

        public static class GridController
        {
            internal static HashSet<RaidableSpawnLocation> Seabed { get; set; }
            internal static Dictionary<RaidableType, RaidableSpawns> Spawns { get; set; }
            internal static Coroutine gridCoroutine { get; set; }
            internal static float gridTime { get; set; }

            public static void Initialize()
            {
                Seabed = new HashSet<RaidableSpawnLocation>();
                Spawns = new Dictionary<RaidableType, RaidableSpawns>();
            }

            public static void Clear()
            {
                Seabed.Clear();
                Spawns.Clear();
            }

            public static double GetRaidTime() => DateTime.Parse(data.RaidTime).Subtract(DateTime.Now).TotalSeconds;

            public static void StartAutomation()
            {
                if (Instance.Scheduled.Enabled)
                {
                    if (data.RaidTime != DateTime.MinValue.ToString() && GetRaidTime() > config.Settings.Schedule.IntervalMax)
                    {
                        data.RaidTime = DateTime.MinValue.ToString();
                    }

                    Instance.Scheduled.StartCoroutine();
                }

                if (Instance.Maintained.Enabled)
                {
                    Instance.Maintained.StartCoroutine();
                }
            }

            public static void Setup()
            {
                if (Spawns.Count >= 5)
                {
                    Instance.timer.Once(5f, StartAutomation);
                    return;
                }

                StopCoroutine();

                Interface.Oxide.NextTick(() => gridCoroutine = ServerMgr.Instance.StartCoroutine(GenerateGrid()));
            }

            public static void StopCoroutine()
            {
                if (gridCoroutine != null)
                {
                    ServerMgr.Instance.StopCoroutine(gridCoroutine);
                    gridCoroutine = null;
                }
            }

            private static IEnumerator GenerateGrid()
            {
                var gridStopwatch = new Stopwatch();

                gridStopwatch.Start();

                RaidableSpawns spawns = Spawns[RaidableType.Grid] = new RaidableSpawns();

                gridTime = Time.realtimeSinceStartup;

                float waterDepthMax = Instance.Buildings.Profiles.Exists(x => x.Value.Options.Water.AllowSubmerged) ? 3f : 0f;
                float monumentDist = M_RADIUS * 2f + config.Settings.Management.MonumentDistance;
                bool seabed = Instance.Buildings.Profiles.Exists(x => x.Value.Options.Water.Seabed > 0f);
                int minPos = (int)(World.Size / -2f);
                int maxPos = (int)(World.Size / 2f);
                float protectionRadius = 50f;
                float elevation = 0.5f;
                int checks = 0;

                foreach (var profile in Instance.Buildings.Profiles.Values)
                {
                    protectionRadius = Mathf.Max(profile.Options.ProtectionRadii.Max(), profile.Options.ArenaWalls.Radius, protectionRadius);

                    waterDepthMax = Mathf.Max(profile.Options.Water.WaterDepth, seabed ? waterDepthMax : 40f);

                    elevation = Mathf.Max(profile.Options.Elevation, elevation);
                }

                for (float x = minPos; x < maxPos; x += CELL_SIZE) // Credits to Jake_Rich for creating this for me!
                {
                    for (float z = minPos; z < maxPos; z += CELL_SIZE)
                    {
                        var position = new Vector3(x, 0f, z);

                        position.y = SpawnsController.GetSpawnHeight(position);

                        SpawnsController.ExtractLocation(spawns, position, elevation, monumentDist, protectionRadius, waterDepthMax, seabed);

                        if (++checks >= 75)
                        {
                            checks = 0;
                            yield return CoroutineEx.waitForSeconds(0.025f);
                        }
                    }
                }

                gridCoroutine = null;
                gridStopwatch.Stop();
                GridController.StartAutomation();

                Puts(mx("InitializedGrid", null, gridStopwatch.Elapsed.Seconds, gridStopwatch.Elapsed.Milliseconds, World.Size, spawns.Count));
                if (GridController.Seabed.Count > 0) Puts(mx("InitializedGridSea", null, GridController.Seabed.Count));
            }

            public static void LoadSpawns()
            {
                Spawns.Clear();
                Spawns.Add(RaidableType.Grid, new RaidableSpawns());

                if (SpawnsFileValid(config.Settings.Manual.SpawnsFile))
                {
                    var spawns = GetSpawnsLocations(config.Settings.Manual.SpawnsFile);

                    if (!spawns.IsNullOrEmpty())
                    {
                        Puts(mx("LoadedManual", null, spawns.Count));
                        Spawns[RaidableType.Manual] = new RaidableSpawns(spawns);
                    }
                }

                if (SpawnsFileValid(config.Settings.Schedule.SpawnsFile))
                {
                    var spawns = GetSpawnsLocations(config.Settings.Schedule.SpawnsFile);

                    if (!spawns.IsNullOrEmpty())
                    {
                        Puts(mx("LoadedScheduled", null, spawns.Count));
                        Spawns[RaidableType.Scheduled] = new RaidableSpawns(spawns);
                    }
                }

                if (SpawnsFileValid(config.Settings.Maintained.SpawnsFile))
                {
                    var spawns = GetSpawnsLocations(config.Settings.Maintained.SpawnsFile);

                    if (!spawns.IsNullOrEmpty())
                    {
                        Puts(mx("LoadedMaintained", null, spawns.Count));
                        Spawns[RaidableType.Maintained] = new RaidableSpawns(spawns);
                    }
                }

                if (SpawnsFileValid(config.Settings.Buyable.SpawnsFile))
                {
                    var spawns = GetSpawnsLocations(config.Settings.Buyable.SpawnsFile);

                    if (!spawns.IsNullOrEmpty())
                    {
                        Puts(mx("LoadedBuyable", null, spawns.Count));
                        Spawns[RaidableType.Purchased] = new RaidableSpawns(spawns);
                    }
                }
            }

            public static bool SpawnsFileValid(string spawnsFile)
            {
                if (string.IsNullOrEmpty(spawnsFile) || spawnsFile.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return FileExists($"SpawnsDatabase{Path.DirectorySeparatorChar}{spawnsFile}");
            }

            public static HashSet<RaidableSpawnLocation> GetSpawnsLocations(string spawnsFile)
            {
                Spawnfile data;

                try
                {
                    data = Interface.Oxide.DataFileSystem.ReadObject<Spawnfile>($"SpawnsDatabase{Path.DirectorySeparatorChar}{spawnsFile}");
                }
                catch (JsonReaderException)
                {
                    return null;
                }

                var locations = new HashSet<RaidableSpawnLocation>();

                if (data.IsNull())
                {
                    return locations;
                }

                foreach (var element in data.spawnPoints)
                {
                    if (element.Value.IsNull()) continue;

                    var value = element.Value.ToString();

                    if (string.IsNullOrEmpty(value)) continue;

                    var vector = value.ToVector3();

                    if (vector == Vector3.zero) continue;

                    locations.Add(new RaidableSpawnLocation(vector));
                }

                return locations;
            }
        }

        private class Spawnfile
        {
            public Dictionary<string, object> spawnPoints = new Dictionary<string, object>();
        }

        public class MaintainedController
        {
            internal Coroutine Coroutine { get; set; }
            internal bool Enabled { get; set; }
            internal RaidableBases Instance { get; set; }

            public MaintainedController(RaidableBases instance, bool enabled)
            {
                Instance = instance;
                Enabled = enabled;
            }

            public void StopCoroutine()
            {
                if (Coroutine != null)
                {
                    ServerMgr.Instance.StopCoroutine(Coroutine);
                    Coroutine = null;
                }
            }

            public void StartCoroutine()
            {
                if (!Enabled || config.Settings.Maintained.Max <= 0)
                {
                    return;
                }

                if (Instance.IsGridLoading)
                {
                    Instance.timer.Once(1f, () => StartCoroutine());
                    return;
                }

                StopCoroutine();

                if (!Instance.CanContinueAutomation())
                {
                    Instance.timer.Once(1f, StartCoroutine);
                    return;
                }

                Instance.timer.Once(0.2f, () => Coroutine = ServerMgr.Instance.StartCoroutine(MaintainCoroutine()));
            }

            private IEnumerator MaintainCoroutine()
            {
                string message;
                RaidableMode mode;
                RandomBase randomBase;

                while (true)
                {
                    if (Enabled && CanMaintainOpenEvent())
                    {
                        if (!Instance.IsCopyPasteLoaded(null))
                        {
                            yield return CoroutineEx.waitForSeconds(60f);
                        }
                        else if (SaveRestore.IsSaving)
                        {
                            PrintDebugMessage("Maintained: Server saving");
                            yield return CoroutineEx.waitForSeconds(15f);
                        }
                        else if (!IsModeValid(mode = Instance.GetRandomDifficulty(RaidableType.Maintained)))
                        {
                            PrintDebugMessage($"Maintained: Invalid mode {mode}");
                            yield return CoroutineEx.waitForSeconds(1f);
                        }
                        else if ((randomBase = Instance.SpawnRandomBase(out message, RaidableType.Maintained, mode)) != null)
                        {
                            RaidableBase.IsSpawning = true;

                            PrintDebugMessage($"Maintained: Waiting for base at {randomBase.Position} to be setup by the plugin.");
                            yield return new WaitWhile(() => RaidableBase.IsSpawning);
                            PrintDebugMessage($"Maintained: Base has been setup by the plugin.");

                            if (config.Settings.Maintained.Time > 0)
                            {
                                PrintDebugMessage($"Maintained: Waiting {config.Settings.Maintained.Time} seconds.");
                                yield return CoroutineEx.waitForSeconds(config.Settings.Maintained.Time);
                            }
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(message))
                            {
                                message = "Maintained: Failed to find position; trying again...";
                            }

                            PrintDebugMessage(message);
                        }
                    }

                    if (Enabled) PrintDebugMessage("Maintained coroutine is waiting for 1 second.");
                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            private bool CanMaintainOpenEvent()
            {
                if (Instance.Raids.Exists(raid => raid.IsDespawning || raid.IsLoading))
                {
                    var vector = Instance.Raids.FirstOrDefault(raid => raid.IsDespawning || raid.IsLoading).Location;
                    PrintDebugMessage($"Maintained: Paste not available; a base is currently despawning or loading at {vector}");
                    return false;
                }

                if (RaidableBase.IsSpawning)
                {
                    PrintDebugMessage($"Maintained: Paste not available; a base is currently spawning.");
                    return false;
                }

                if (Instance.IsGridLoading)
                {
                    PrintDebugMessage($"Maintained: Grid is loading.");
                    return false;
                }

                if (config.Settings.Maintained.Max > 0 && RaidableBase.Get(RaidableType.Maintained) >= config.Settings.Maintained.Max)
                {
                    PrintDebugMessage($"Maintained: The max amount of maintained events are spawned.");
                    return false;
                }

                if (BasePlayer.activePlayerList.Count < config.Settings.Maintained.PlayerLimit)
                {
                    PrintDebugMessage($"Maintained: Insufficient amount of players online {BasePlayer.activePlayerList.Count}/{config.Settings.Maintained.PlayerLimit}");
                    return false;
                }

                return true;
            }
        }

        public class ScheduledController
        {
            internal Coroutine Coroutine { get; set; }
            internal bool Enabled { get; set; }
            internal int _maxOnce { get; set; }
            internal RaidableBases Instance { get; set; }

            public ScheduledController(RaidableBases instance, bool enabled)
            {
                Instance = instance;
                Enabled = enabled;
            }

            public void StopCoroutine()
            {
                if (Coroutine != null)
                {
                    ServerMgr.Instance.StopCoroutine(Coroutine);
                    Coroutine = null;
                }
            }

            public void StartCoroutine()
            {
                if (!Enabled || config.Settings.Schedule.Max <= 0)
                {
                    return;
                }

                if (Instance.IsGridLoading)
                {
                    Instance.timer.Once(1f, () => StartCoroutine());
                    return;
                }

                StopCoroutine();

                if (!Instance.CanContinueAutomation())
                {
                    Instance.timer.Once(1f, StartCoroutine);
                    return;
                }

                if (data.RaidTime == DateTime.MinValue.ToString())
                {
                    ScheduleNextAutomatedEvent();
                }

                Instance.timer.Once(0.2f, () =>
                {
                    Coroutine = ServerMgr.Instance.StartCoroutine(ScheduleCoroutine());
                });
            }

            private IEnumerator ScheduleCoroutine()
            {
                string message;
                RaidableMode mode;
                RandomBase randomBase;

                while (true)
                {
                    if (Enabled && CanScheduleOpenEvent())
                    {
                        while (RaidableBase.Get(RaidableType.Scheduled) < config.Settings.Schedule.Max && MaxOnce())
                        {
                            if (!Instance.IsCopyPasteLoaded(null))
                            {
                                yield return CoroutineEx.waitForSeconds(60f);
                            }
                            else if (SaveRestore.IsSaving)
                            {
                                PrintDebugMessage("Scheduled: Server saving");
                                yield return CoroutineEx.waitForSeconds(15f);
                            }
                            else if (!IsModeValid(mode = Instance.GetRandomDifficulty(RaidableType.Scheduled)))
                            {
                                PrintDebugMessage($"Scheduled: Invalid mode {mode}");
                                yield return CoroutineEx.waitForSeconds(1f);
                            }
                            else if ((randomBase = Instance.SpawnRandomBase(out message, RaidableType.Scheduled, mode)) != null)
                            {
                                RaidableBase.IsSpawning = true;
                                _maxOnce++;

                                PrintDebugMessage($"Scheduled: Waiting for base at {randomBase.Position} to be setup by the plugin.");
                                yield return new WaitWhile(() => RaidableBase.IsSpawning);
                                PrintDebugMessage($"Scheduled: Base has been setup by the plugin.");

                                if (config.Settings.Schedule.Time > 0)
                                {
                                    PrintDebugMessage($"Scheduled: Waiting {config.Settings.Schedule.Time} seconds.");
                                    yield return CoroutineEx.waitForSeconds(config.Settings.Schedule.Time);
                                }
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(message))
                                {
                                    message = "Scheduled: Failed to find position; trying again...";
                                }

                                PrintDebugMessage(message);
                            }

                            PrintDebugMessage("Scheduled coroutine is waiting for 1 second.");
                            yield return CoroutineEx.waitForSeconds(1f);
                        }

                        PrintDebugMessage("Scheduling next automated event.");
                        ScheduleNextAutomatedEvent();
                    }

                    if (Enabled) PrintDebugMessage("Scheduled coroutine is waiting for 1 second.");
                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            private bool CanScheduleOpenEvent()
            {
                if (Instance.Raids.Exists(raid => raid.IsDespawning || raid.IsLoading))
                {
                    var vector = Instance.Raids.FirstOrDefault(raid => raid.IsDespawning || raid.IsLoading).Location;
                    PrintDebugMessage($"Scheduled: Paste not available; a base is currently despawning or loading at {vector}");
                    return false;
                }

                if (RaidableBase.IsSpawning)
                {
                    PrintDebugMessage($"Scheduled: Paste not available; a base is currently spawning.");
                    return false;
                }

                if (Instance.IsGridLoading)
                {
                    PrintDebugMessage($"Scheduled: Grid is loading.");
                    return false;
                }

                if (config.Settings.Schedule.Max > 0 && RaidableBase.Get(RaidableType.Scheduled) >= config.Settings.Schedule.Max)
                {
                    PrintDebugMessage($"Scheduled: The max amount of scheduled events are spawned.");
                    return false;
                }

                if (BasePlayer.activePlayerList.Count < config.Settings.Schedule.PlayerLimit)
                {
                    PrintDebugMessage($"Scheduled: Insufficient amount of players online {BasePlayer.activePlayerList.Count}/{config.Settings.Schedule.PlayerLimit}");
                    return false;
                }

                if (GridController.GetRaidTime() > 0)
                {
                    PrintDebugMessage($"Scheduled: {FormatTime(GridController.GetRaidTime())} before next event.");
                    return false;
                }

                return true;
            }

            private void ScheduleNextAutomatedEvent()
            {
                var raidInterval = Core.Random.Range(config.Settings.Schedule.IntervalMin, config.Settings.Schedule.IntervalMax + 1);

                _maxOnce = 0;
                data.RaidTime = DateTime.Now.AddSeconds(raidInterval).ToString();
                Puts(mx("Next Automated Raid", null, FormatTime(raidInterval, null), data.RaidTime));
            }

            private bool MaxOnce()
            {
                return config.Settings.Schedule.MaxOnce <= 0 || _maxOnce < config.Settings.Schedule.MaxOnce;
            }
        }

        public class BMGELEVATOR : FacepunchBehaviour // credits: bmgjet
        {
            internal const string ElevatorPanelName = "RB_UI_Elevator";

            internal Elevator _elevator;
            internal RaycastHit hit;
            internal BaseEntity hitEntity;
            internal RaidableBase raid;
            internal BuildingOptionsElevators options;
            internal Dictionary<ulong, BasePlayer> _UI = new Dictionary<ulong, BasePlayer>();
            internal bool HasButton;
            internal uint uid;
            internal int CurrentFloor;
            internal int returnDelay = 60;
            internal float Floors;
            internal float lastPermissionTime;
            internal float _LiftSpeedPerMetre = 3f;

            private void Awake()
            {
                _elevator = GetComponent<Elevator>();
                _elevator.LiftSpeedPerMetre = _LiftSpeedPerMetre;
            }

            private void OnDestroy()
            {
                _elevator.SafelyKill();
                _UI.Values.ToList().ForEach(DestroyUi);
                Instance?._elevators.Remove(uid);
                CancelInvoke();
            }

            public void DestroyMe()
            {
                Destroy(this);
            }

            public bool ValidPosition => !_elevator.IsKilled() && !_elevator.liftEntity.IsKilled();

            public Vector3 ServerPosition => _elevator.liftEntity.transform.position;

            private Vector3 GetWorldSpaceFloorPosition(int targetFloor)
            {
                int num = _elevator.Floor - targetFloor;
                Vector3 b = Vector3.up * ((float)num * _elevator.FloorHeight);
                b.y -= 1f;
                return base.transform.position - b;
            }

            public void GoToFloor(Elevator.Direction Direction = Elevator.Direction.Down, bool FullTravel = false, int forcedFloor = -1)
            {
                if (_elevator.HasFlag(BaseEntity.Flags.Busy))
                {
                    return;
                }
                int maxFloors = (int)(Floors / 3f);
                if (forcedFloor != -1)
                {
                    int targetFloor = Mathf.RoundToInt((forcedFloor - ServerPosition.y) / 3);
                    if (targetFloor == 0 && CurrentFloor == 0) targetFloor = maxFloors;
                    else if (targetFloor == 0 && CurrentFloor == maxFloors) targetFloor = -maxFloors;
                    CurrentFloor += targetFloor;
                    if (CurrentFloor > maxFloors) CurrentFloor = maxFloors;
                    if (CurrentFloor < 0) CurrentFloor = 0;
                }
                else
                {
                    if (Direction == Elevator.Direction.Up)
                    {
                        CurrentFloor++;
                        if (FullTravel) CurrentFloor = (int)(Floors / _elevator.FloorHeight);
                        if ((CurrentFloor * 3) > Floors) CurrentFloor = (int)(Floors / _elevator.FloorHeight);
                    }
                    else
                    {
                        if (GamePhysics.CheckSphere(ServerPosition - new Vector3(0, 1f, 0), 0.5f, Layers.Mask.Construction | Layers.Server.Deployed, QueryTriggerInteraction.Ignore))
                        {
                            _elevator.Invoke(Retry, returnDelay);
                            return;
                        }

                        CurrentFloor--;
                        if (CurrentFloor < 0 || FullTravel) CurrentFloor = 0;
                    }
                }
                Vector3 worldSpaceFloorPosition = GetWorldSpaceFloorPosition(CurrentFloor);
                if (!GamePhysics.LineOfSight(ServerPosition, worldSpaceFloorPosition, 2097152))
                {
                    if (Direction == Elevator.Direction.Up)
                    {
                        if (!Physics.Raycast(ServerPosition, Vector3.up, out hit, 21f) || (hitEntity = hit.GetEntity()).IsNull())
                        {
                            return;
                        }
                        CurrentFloor = (int)(Vector3.Distance(hitEntity.transform.position, _elevator.transform.position) / 3);
                        worldSpaceFloorPosition = GetWorldSpaceFloorPosition(CurrentFloor);
                    }
                    else
                    {
                        if (!Physics.Raycast(ServerPosition - new Vector3(0, 2.9f, 0), Vector3.down, out hit, 21f) || (hitEntity = hit.GetEntity()).IsNull() || hitEntity.ShortPrefabName == "foundation" || hitEntity.ShortPrefabName == "elevator.static")
                        {
                            _elevator.Invoke(Retry, returnDelay);
                            return;
                        }
                        CurrentFloor = (int)(Vector3.Distance(hitEntity.transform.position, _elevator.transform.position) / 3) + 1;
                        worldSpaceFloorPosition = GetWorldSpaceFloorPosition(CurrentFloor);
                    }
                }
                Vector3 vector = transform.InverseTransformPoint(worldSpaceFloorPosition);
                float timeToTravel = _elevator.TimeToTravelDistance(Mathf.Abs(_elevator.liftEntity.transform.localPosition.y - vector.y));
                LeanTween.moveLocalY(_elevator.liftEntity.gameObject, vector.y, timeToTravel);
                _elevator.SetFlag(BaseEntity.Flags.Busy, true, false, true);
                _elevator.liftEntity.ToggleHurtTrigger(true);
                _elevator.Invoke(_elevator.ClearBusy, timeToTravel);
                _elevator.CancelInvoke(ElevatorToGround);
                _elevator.Invoke(ElevatorToGround, timeToTravel + returnDelay);
            }

            private void Retry()
            {
                GoToFloor(Elevator.Direction.Down, true);
            }

            private void ElevatorToGround()
            {
                if (CurrentFloor != 0)
                {
                    if (_elevator.HasFlag(BaseEntity.Flags.Busy))
                    {
                        _elevator.Invoke(ElevatorToGround, 5f);
                        return;
                    }
                    GoToFloor(Elevator.Direction.Down, true);
                }
            }

            public void Init(RaidableBase raid)
            {
                this.raid = raid;
                options = raid.Options.Elevators;
                _elevator._maxHealth = options.ElevatorHealth;
                _elevator.InitializeHealth(options.ElevatorHealth, options.ElevatorHealth);

                if (options.Enabled)
                {
                    InvokeRepeating(ShowHealthUI, 10, 1);
                }

                if (HasButton)
                {
                    Instance.Subscribe(nameof(OnButtonPress));
                }
            }

            private void ShowHealthUI()
            {
                var players = raid.GetIntruders().Where(player => player.Distance(ServerPosition) <= 3f);
                foreach (var player in _UI.Values.ToList())
                {
                    if (!players.Contains(player)) // || !GamePhysics.LineOfSight(ServerPosition, player.transform.position, 2097152))
                    {
                        DestroyUi(player);
                        _UI.Remove(player.userID);
                    }
                }
                foreach (var player in players)
                {
                    if (!player.IsSleeping()) // && GamePhysics.LineOfSight(ServerPosition, player.transform.position, 2097152))
                    {
                        var translated = mx("Elevator Health", player.UserIDString);
                        var color = UI.Color(options.PanelColor, options.PanelAlpha);
                        var elements = UI.CreateElementContainer(ElevatorPanelName, color, options.Min, options.Max);
                        var text = $"{translated} {_elevator._health:#.##}/{_elevator._maxHealth}";
                        UI.CreateLabel(ref elements, ElevatorPanelName, "1 1 1 1", text, 16, "0 0", "1 1");
                        DestroyUi(player);
                        CuiHelper.AddUi(player, elements);
                        _UI[player.userID] = player;
                    }
                }
            }

            public static void DestroyUi(BasePlayer player) => CuiHelper.DestroyUi(player, ElevatorPanelName);

            public static Dictionary<Elevator, BMGELEVATOR> FixElevators(List<BaseEntity> pastedEntities)
            {
                var elevators = new List<BaseEntity>();
                var bmgs = new Dictionary<Elevator, BMGELEVATOR>();
                bool hasButton = false;

                foreach (BaseEntity entity in pastedEntities.ToList())
                {
                    if (entity is Elevator || entity is ElevatorLift)
                    {
                        elevators.Add(entity);
                        pastedEntities.Remove(entity);
                    }
                    else if (entity is PressButton)
                    {
                        hasButton = true;
                    }
                }

                foreach (var list in SplitElevators(elevators))
                {
                    BMGELEVATOR bmgELEVATOR;
                    Elevator elevator = FixElevators(list, out bmgELEVATOR);
                    if (!elevator.IsNull())
                    {
                        bmgELEVATOR.HasButton = hasButton;
                        bmgs[elevator] = bmgELEVATOR;
                    }
                }

                return bmgs;
            }

            public static Elevator FixElevators(List<BaseEntity> elevators, out BMGELEVATOR bmgELEVATOR)
            {
                bmgELEVATOR = null;
                if (!elevators.IsNullOrEmpty())
                {
                    Elevator e = FixElevator(elevators, out bmgELEVATOR);
                    if (!e.IsNull())
                    {
                        elevators.Add(e);
                        return e;
                    }
                }
                return null;
            }

            private static void CleanElevatorKill(BaseEntity entity)
            {
                if (!entity.IsDestroyed)
                {
                    entity.transform.position = new Vector3(0, -100f, 0);
                    Instance.NextFrame(entity.SafelyKill);
                }
            }

            public static Elevator FixElevator(List<BaseEntity> elevators, out BMGELEVATOR bmgELEVATOR)
            {
                bmgELEVATOR = null;
                if (elevators.Count == 1)
                {
                    CleanElevatorKill(elevators[0]);
                    return null;
                }
                Vector3 bottom = new Vector3(999f, 999f, 999f);
                Vector3 top = new Vector3(-999f, -999f, -999f);
                Quaternion rot = elevators[0].transform.rotation;
                foreach (BaseEntity entity in elevators)
                {
                    if (entity.transform.position.y < bottom.y) bottom = entity.transform.position;
                    if (entity.transform.position.y > top.y) top = entity.transform.position;
                    CleanElevatorKill(entity);
                }
                Elevator elevator = GameManager.server.CreateEntity("assets/prefabs/deployable/elevator/static/elevator.static.prefab", bottom, rot, true) as Elevator;
                elevator.transform.rotation = rot;
                elevator.transform.position = bottom;
                elevator.transform.localPosition += new Vector3(0f, 0.25f, 0f);
                bmgELEVATOR = elevator.gameObject.AddComponent<BMGELEVATOR>();
                elevator.Spawn();
                bmgELEVATOR.Floors = top.y - bottom.y;
                Interface.Oxide.NextTick(() =>
                {
                    if (elevator.IsKilled()) return;
                    RemoveImmortality(elevator.baseProtection, 0.3f, 1.0f, 1.0f, 0.5f, 0.9f, 0.9f, 0.3f, 0.9f, 0.9f, 1.0f, 0.9f, 0.9f);
                    RemoveImmortality(elevator.liftEntity.baseProtection, 0.3f, 1.0f, 1.0f, 0.5f, 0.9f, 0.9f, 0.3f, 0.9f, 0.9f, 1.0f, 0.9f, 0.9f);
                });
                elevator.SetFlag(BaseEntity.Flags.Reserved1, true, false, true);
                elevator.SetFlag(Elevator.Flag_HasPower, true);
                elevator.SendNetworkUpdateImmediate();
                bmgELEVATOR.uid = elevator.net.ID;
                Instance._elevators[elevator.net.ID] = bmgELEVATOR;
                Instance.Subscribe(nameof(OnElevatorButtonPress));
                return elevator;
            }

            private static void RemoveImmortality(ProtectionProperties baseProtection, float antivehicle, float arrow, float bite, float bullet, float blunt, float collision, float explosion, float generic, float heat, float radiation, float slash, float stab)
            {
                baseProtection.amounts[(int)DamageType.AntiVehicle] = antivehicle;
                baseProtection.amounts[(int)DamageType.Arrow] = arrow;
                baseProtection.amounts[(int)DamageType.Bite] = bite;
                baseProtection.amounts[(int)DamageType.Bullet] = bullet;
                baseProtection.amounts[(int)DamageType.Blunt] = blunt;
                baseProtection.amounts[(int)DamageType.Collision] = collision;
                baseProtection.amounts[(int)DamageType.Explosion] = explosion;
                baseProtection.amounts[(int)DamageType.Generic] = generic;
                baseProtection.amounts[(int)DamageType.Heat] = heat;
                baseProtection.amounts[(int)DamageType.Radiation] = radiation;
                baseProtection.amounts[(int)DamageType.Slash] = slash;
                baseProtection.amounts[(int)DamageType.Stab] = stab;
            }

            public static List<List<BaseEntity>> SplitElevators(List<BaseEntity> source)
            {
                var result = new List<List<BaseEntity>>();
                List<int> Elevators = new List<int>();
                foreach (BaseEntity entity in source)
                {
                    int distance = (int)(entity.transform.position.x + entity.transform.position.x);
                    if (!Elevators.Contains(distance))
                    {
                        Elevators.Add(distance);
                        result.Add(new List<BaseEntity>
                        {
                            entity
                        });
                    }
                    else
                    {
                        int index = Elevators.IndexOf(distance);
                        result[index].Add(entity);
                    }
                }
                return result;
            }

            private HashSet<ulong> _granted = new HashSet<ulong>();

            public bool HasCardPermission(BasePlayer player)
            {
                if (_granted.Contains(player.userID) || options.RequiredAccessLevel == 0 || player.HasPermission("raidablebases.elevators.bypass.card"))
                {
                    return true;
                }

                string shortname = options.RequiredAccessLevel == 1 ? "keycard_green" : options.RequiredAccessLevel == 2 ? "keycard_blue" : "keycard_red";

                Item item = player.inventory.FindItemID(shortname);

                if (item == null || item.skin != options.SkinID)
                {
                    raid.TryMessage(player, options.RequiredAccessLevel == 1 ? "Elevator Green Card" : options.RequiredAccessLevel == 2 ? "Elevator Blue Card" : options.RequiredAccessLevel == 3 ? "Elevator Red Card" : "Elevator Special Card");
                    return false;
                }

                Keycard keycard = item.GetHeldEntity() as Keycard;

                if (keycard?.accessLevel == options.RequiredAccessLevel)
                {
                    if (options.RequiredAccessLevelOnce)
                    {
                        _granted.Add(player.userID);
                    }

                    return true;
                }

                raid.TryMessage(player, options.RequiredAccessLevel == 1 ? "Elevator Green Card" : options.RequiredAccessLevel == 2 ? "Elevator Blue Card" : options.RequiredAccessLevel == 3 ? "Elevator Red Card" : "Elevator Special Card");
                return false;
            }

            public bool HasBuildingPermission(BasePlayer player)
            {
                if (!options.RequiresBuildingPermission || player.HasPermission("raidablebases.elevators.bypass.building"))
                {
                    return true;
                }

                if (Time.time < lastPermissionTime)
                {
                    return false;
                }

                lastPermissionTime = Time.time + 1f;

                if (player.IsBuildingBlocked())
                {
                    raid.TryMessage(player, "Elevator Privileges");
                    return false;
                }

                return true;
            }
        }

        public class RaidableSpawns
        {
            public HashSet<RaidableSpawnLocation> Spawns { get; set; } = new HashSet<RaidableSpawnLocation>();
            private Dictionary<CacheType, HashSet<RaidableSpawnLocation>> Cached { get; set; } = new Dictionary<CacheType, HashSet<RaidableSpawnLocation>>();
            private float lastTryTime;

            public bool IsCustomSpawn { get; set; }

            public int Count => Spawns.Count;

            public HashSet<RaidableSpawnLocation> Active => Spawns;

            public HashSet<RaidableSpawnLocation> Inactive(CacheType cacheType) => Get(cacheType);

            public RaidableSpawns(HashSet<RaidableSpawnLocation> spawns)
            {
                Spawns = spawns;
                IsCustomSpawn = true;
            }

            public RaidableSpawns() { }

            public bool Add(RaidableSpawnLocation rsl, CacheType cacheType, HashSet<RaidableSpawnLocation> cache)
            {
                switch (cacheType)
                {
                    case CacheType.Submerged:
                        rsl.WaterHeight = TerrainMeta.WaterMap.GetHeight(rsl.Location);
                        rsl.Surroundings.Clear();
                        break;
                    case CacheType.Generic:
                        if (Instance.EventTerritory(rsl.Location))
                        {
                            return false;
                        }
                        break;
                    case CacheType.Close:
                        if (RaidableBase.IsTooClose(rsl.Location, GetDistance(RaidableType.None)))
                        {
                            return false;
                        }
                        break;
                }

                return Spawns.Add(rsl);
            }

            public void Check()
            {
                if (lastTryTime == 0f || Time.time - lastTryTime > 600f)
                {
                    TryAddRange(CacheType.Temporary, true);
                    TryAddRange(CacheType.Privilege, true);
                    lastTryTime = Time.time;
                }

                if (Spawns.Count == 0)
                {
                    TryAddRange();
                }
            }

            public void TryAddRange(CacheType cacheType = CacheType.Generic, bool forced = false)
            {
                HashSet<RaidableSpawnLocation> cache = Get(cacheType);
                List<RaidableSpawnLocation> remove = new List<RaidableSpawnLocation>();

                foreach (var rsl in cache)
                {
                    if (forced && Spawns.Add(rsl) || Add(rsl, cacheType, cache))
                    {
                        remove.Add(rsl);
                    }
                }

                remove.ForEach(rsl => cache.Remove(rsl));
            }

            public RaidableSpawnLocation GetRandom(BuildingWaterOptions options)
            {
                RaidableSpawnLocation rsl;

                if (GridController.Seabed.Count > 0 && UnityEngine.Random.Range(0f, 100f) >= options.Seabed)
                {
                    rsl = GridController.Seabed.GetRandom();
                    options.SpawnOnSeabed = true;
                }
                else
                {
                    rsl = Spawns.GetRandom();
                    options.SpawnOnSeabed = false;
                }

                Remove(rsl, CacheType.Generic);

                return rsl;
            }

            public HashSet<RaidableSpawnLocation> Get(CacheType cacheType)
            {
                HashSet<RaidableSpawnLocation> cache;
                if (!Cached.TryGetValue(cacheType, out cache))
                {
                    Cached[cacheType] = cache = new HashSet<RaidableSpawnLocation>();
                }

                return cache;
            }

            public void AddNear(Vector3 target, float radius, CacheType cacheType, bool delayed)
            {
                if (delayed)
                {
                    Instance.timer.Once(1200f, () => AddNear(target, radius, cacheType, false));
                    return;
                }

                HashSet<RaidableSpawnLocation> cache = Get(cacheType);

                AddNear(cache, target, radius);
            }

            private void AddNear(HashSet<RaidableSpawnLocation> cache, Vector3 target, float radius)
            {
                List<RaidableSpawnLocation> remove = new List<RaidableSpawnLocation>();

                foreach (var rsl in cache)
                {
                    if (InRange2D(target, rsl.Location, radius) && Spawns.Add(rsl))
                    {
                        remove.Add(rsl);
                    }
                }

                remove.ForEach(rsl => cache.Remove(rsl));
            }

            public void Remove(RaidableSpawnLocation a, CacheType cacheType)
            {
                Get(cacheType).Add(a);
                Spawns.Remove(a);
            }

            public float RemoveNear(Vector3 target, float radius, CacheType cacheType, RaidableType type)
            {
                if (cacheType == CacheType.Generic)
                {
                    radius = Mathf.Max(GetDistance(type), radius);
                }

                HashSet<RaidableSpawnLocation> cache = Get(cacheType);
                List<RaidableSpawnLocation> remove = new List<RaidableSpawnLocation>();

                foreach (var rsl in Spawns)
                {
                    if (InRange2D(target, rsl.Location, radius) && cache.Add(rsl))
                    {
                        remove.Add(rsl);
                    }
                }

                remove.ForEach(rsl => Spawns.Remove(rsl));

                return radius;
            }
        }

        public class PlayerInfo
        {
            public int Raids { get; set; }
            public int Points { get; set; }
            public int Easy { get; set; }
            public int Medium { get; set; }
            public int Hard { get; set; }
            public int Expert { get; set; }
            public int Nightmare { get; set; }
            public int EasyPoints { get; set; }
            public int MediumPoints { get; set; }
            public int HardPoints { get; set; }
            public int ExpertPoints { get; set; }
            public int NightmarePoints { get; set; }
            public int TotalRaids { get; set; }
            public int TotalPoints { get; set; }
            public int TotalEasy { get; set; }
            public int TotalMedium { get; set; }
            public int TotalHard { get; set; }
            public int TotalExpert { get; set; }
            public int TotalNightmare { get; set; }
            public int TotalEasyPoints { get; set; }
            public int TotalMediumPoints { get; set; }
            public int TotalHardPoints { get; set; }
            public int TotalExpertPoints { get; set; }
            public int TotalNightmarePoints { get; set; }
            public DateTime ExpiredDate { get; set; } = DateTime.MinValue;
            public bool IsExpired()
            {
                if (ExpiredDate == DateTime.MinValue)
                {
                    ResetExpiredDate();
                }

                return ExpiredDate < DateTime.Now;
            }
            public void ResetExpiredDate() => ExpiredDate = DateTime.Now.AddDays(60);
            public static PlayerInfo Get(string userid)
            {
                PlayerInfo playerInfo;
                if (!data.Players.TryGetValue(userid, out playerInfo))
                {
                    data.Players[userid] = playerInfo = new PlayerInfo();
                }
                return playerInfo;
            }
            public void Reset()
            {
                Raids = 0;
                Points = 0;
                Easy = 0;
                Medium = 0;
                Hard = 0;
                Expert = 0;
                Nightmare = 0;
                EasyPoints = 0;
                MediumPoints = 0;
                HardPoints = 0;
                ExpertPoints = 0;
                NightmarePoints = 0;
            }
        }

        public class RotationCycle
        {
            private Dictionary<RaidableMode, List<string>> _buildings = new Dictionary<RaidableMode, List<string>>();

            public void Add(RaidableType type, RaidableMode mode, string key)
            {
                if (!config.Settings.Management.RequireAllSpawned || type == RaidableType.Grid || type == RaidableType.Manual)
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

                if (!config.Settings.Management.RequireAllSpawned || mode == RaidableMode.Random || type == RaidableType.Grid || type == RaidableType.Manual)
                {
                    return true;
                }

                List<string> keyList;

                return !_buildings.TryGetValue(mode, out keyList) || TryClear(type, mode, keyList) || !keyList.Contains(key);
            }

            public bool TryClear(RaidableType type, RaidableMode mode, List<string> keyList)
            {
                foreach (var profile in Instance.Buildings.Profiles)
                {
                    if (profile.Value.Options.Mode != mode || !CanSpawnDifficultyToday(mode) || MustExclude(type, profile.Value.Options.AllowPVP))
                    {
                        continue;
                    }

                    if (!keyList.Contains(profile.Key) && FileExists(profile.Key))
                    {
                        return false;
                    }

                    if (profile.Value.Options.AdditionalBases.Exists(kvp => !keyList.Contains(kvp.Key) && FileExists(kvp.Key)))
                    {
                        return false;
                    }
                }

                keyList.Clear();
                return true;
            }
        }

        public class PlayerInputEx : FacepunchBehaviour
        {
            public BasePlayer player { get; set; }
            private InputState input { get; set; }
            private RaidableBase raid { get; set; }
            private ulong userID { get; set; }

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                input = player.serverInput;
                userID = player.userID;
            }

            private void OnDestroy()
            {
                if (!player.IsReallyConnected())
                {
                    raid.TryInvokeResetPayLock();
                }

                CancelInvoke();
                Destroy(this);
            }

            public void Setup(RaidableBase raid)
            {
                this.raid = raid;
                raid.GetRaider(player).Input = this;

                InvokeRepeating(Repeater, 0f, 0.1f);
            }

            public void Restart()
            {
                CancelInvoke(Repeater);
                InvokeRepeating(Repeater, 0.1f, 0.1f);
            }

            private void Repeater()
            {
                if (raid == null)
                {
                    Destroy(this);
                }
                else TryPlace(ConstructionType.Any);
            }

            public bool TryPlace(ConstructionType constructionType)
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

                if (item == null)
                {
                    return false;
                }

                RaycastHit hit;
                if (!IsConstructionType(item.info.shortname, ref constructionType, out hit))
                {
                    return false;
                }

                int amount = item.amount;

                var action = new Action(() =>
                {
                    if (raid == null || item == null || item.amount != amount || IsConstructionNear(constructionType, hit.point))
                    {
                        return;
                    }

                    Quaternion rot;
                    if (constructionType == ConstructionType.Barricade)
                    {
                        rot = Quaternion.LookRotation((player.transform.position.WithY(0f) - hit.point.WithY(0f)).normalized);
                    }
                    else rot = Quaternion.LookRotation(hit.normal, Vector3.up);

                    var prefab = GetConstructionPrefab(item.info);
                    var e = GameManager.server.CreateEntity(prefab, hit.point, rot, true);

                    if (e.IsNull())
                    {
                        return;
                    }

                    e.gameObject.SendMessage("SetDeployedBy", player, SendMessageOptions.DontRequireReceiver);
                    e.OwnerID = 0;
                    e.Spawn();
                    item.UseItem(1);

                    if (constructionType == ConstructionType.Ladder)
                    {
                        e.SetParent(hit.GetEntity(), true, false);
                    }

                    raid.BuiltList[e] = hit.point;
                    raid.AddEntity(e);
                });

                player.Invoke(action, 0.1f);
                return true;
            }

            public bool IsConstructionType(string shortname, ref ConstructionType constructionType, out RaycastHit hit)
            {
                hit = default(RaycastHit);

                if (constructionType == ConstructionType.Any || constructionType == ConstructionType.Ladder)
                {
                    if (shortname == "ladder.wooden.wall")
                    {
                        constructionType = ConstructionType.Ladder;

                        if (raid.Options.RequiresCupboardAccessLadders && !player.CanBuild())
                        {
                            Message(player, "Ladders Require Building Privilege!");
                            return false;
                        }

                        if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 4f, Layers.Mask.Construction, QueryTriggerInteraction.Ignore))
                        {
                            return false;
                        }

                        var entity = hit.GetEntity();

                        if (entity.IsNull() || entity.OwnerID != 0 || !Instance.Blocks.Contains(entity.ShortPrefabName)) // walls and foundations
                        {
                            return false;
                        }

                        return true;
                    }
                }

                if (constructionType == ConstructionType.Any || constructionType == ConstructionType.Barricade)
                {
                    if (shortname.StartsWith("barricade."))
                    {
                        constructionType = ConstructionType.Barricade;

                        if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 5f, Layers.Solid, QueryTriggerInteraction.Ignore))
                        {
                            return false;
                        }

                        return hit.GetEntity().IsNull();
                    }
                }

                return false;
            }

            public bool IsConstructionNear(ConstructionType constructionType, Vector3 target)
            {
                float radius = constructionType == ConstructionType.Barricade ? 1f : 0.3f;
                int layerMask = constructionType == ConstructionType.Barricade ? -1 : Layers.Mask.Deployed;
                var entities = FindEntitiesOfType<BaseEntity>(target, radius, layerMask);
                bool result;
                if (constructionType == ConstructionType.Barricade)
                {
                    result = entities.Count > 0;
                }
                else result = entities.Exists(e => e is BaseLadder);
                return result;
            }

            public string GetConstructionPrefab(ItemDefinition def)
            {
                switch (def.shortname)
                {
                    case "ladder.wooden.wall": return StringPool.Get(2150203378);
                    case "barricade.woodwire": return StringPool.Get(1202834203);
                    case "barricade.concrete": return StringPool.Get(2057881102);
                    case "barricade.metal": return StringPool.Get(3824663394);
                    case "barricade.sandbags": return StringPool.Get(2335812770);
                    case "barricade.stone": return StringPool.Get(1206527181);
                    case "barricade.wood": return StringPool.Get(4254045167);
                    case "barricade.cover.wood": return StringPool.Get(1581233281);
                }

                return def.GetComponent<ItemModDeployable>().entityPrefab.Get().GetComponent<BaseEntity>().PrefabName;
            }
        }

        public class HumanoidBrain : BaseAIBrain<global::HumanNPC>
        {
            internal enum AttackType
            {
                BaseProjectile,
                Explosive,
                FlameThrower,
                Melee,
                Water,
                None
            }

            internal ScientistNPC npc;
            private AttackEntity _attackEntity;
            private FlameThrower flameThrower;
            private BaseProjectile launcher;
            private LiquidWeapon liquidWeapon;
            private BaseMelee baseMelee;
            private BasePlayer AttackTarget;
            internal RaidableBase raid;
            internal NpcSettings Settings;
            private List<Vector3> positions;
            internal Vector3 DestinationOverride;
            private ulong uid;
            internal bool isStationary;
            private float lastWarpTime;
            internal float softLimitSenseRange;
            private float nextAttackTime;
            private float attackRange;
            private float attackCooldown;
            internal AttackType attackType = AttackType.None;
            private BaseNavigator.NavigationSpeed CurrentSpeed = BaseNavigator.NavigationSpeed.Normal;

            internal Vector3 AttackPosition => AttackTarget.ServerPosition;

            internal Vector3 ServerPosition => npc.ServerPosition;

            internal AttackEntity AttackEntity
            {
                get
                {
                    if (_attackEntity.IsNull())
                    {
                        IdentifyWeapon();
                    }

                    return _attackEntity;
                }
            }

            internal void IdentifyWeapon()
            {
                _attackEntity = GetEntity().GetAttackEntity();

                attackRange = 0f;
                attackCooldown = 99999f;
                attackType = AttackType.None;
                baseMelee = null;
                flameThrower = null;
                launcher = null;
                liquidWeapon = null;

                if (_attackEntity.IsNull())
                {
                    return;
                }

                switch (_attackEntity.ShortPrefabName)
                {
                    case "double_shotgun.entity":
                    case "shotgun_pump.entity":
                    case "shotgun_waterpipe.entity":
                    case "spas12.entity":
                        SetAttackRestrictions(AttackType.BaseProjectile, 30f, 0f, 30f);
                        break;
                    case "ak47u.entity":
                    case "bolt_rifle.entity":
                    case "l96.entity":
                    case "lr300.entity":
                    case "m249.entity":
                    case "m39.entity":
                    case "m92.entity":
                    case "mp5.entity":
                    case "nailgun.entity":
                    case "pistol_eoka.entity":
                    case "pistol_revolver.entity":
                    case "pistol_semiauto.entity":
                    case "python.entity":
                    case "semi_auto_rifle.entity":
                    case "thompson.entity":
                    case "smg.entity":
                        SetAttackRestrictions(AttackType.BaseProjectile, 300f, 0f, 150f);
                        break;
                    case "snowballgun.entity":
                        SetAttackRestrictions(AttackType.BaseProjectile, 15f, 0.1f, 15f);
                        break;
                    case "chainsaw.entity":
                    case "jackhammer.entity":
                        baseMelee = _attackEntity as BaseMelee;
                        SetAttackRestrictions(AttackType.Melee, 2.5f, (_attackEntity.animationDelay + _attackEntity.deployDelay) * 2f);
                        break;
                    case "axe_salvaged.entity":
                    case "bone_club.entity":
                    case "butcherknife.entity":
                    case "candy_cane.entity":
                    case "hammer_salvaged.entity":
                    case "hatchet.entity":
                    case "icepick_salvaged.entity":
                    case "knife.combat.entity":
                    case "knife_bone.entity":
                    case "longsword.entity":
                    case "mace.entity":
                    case "machete.weapon":
                    case "pickaxe.entity":
                    case "pitchfork.entity":
                    case "salvaged_cleaver.entity":
                    case "salvaged_sword.entity":
                    case "sickle.entity":
                    case "spear_stone.entity":
                    case "spear_wooden.entity":
                    case "stone_pickaxe.entity":
                    case "stonehatchet.entity":
                        baseMelee = _attackEntity as BaseMelee;
                        SetAttackRestrictions(AttackType.Melee, 2.5f, _attackEntity.animationDelay + _attackEntity.deployDelay);
                        break;
                    case "explosive.satchel.entity":
                    case "explosive.timed.entity":
                        SetAttackRestrictions(AttackType.Explosive, 17.5f, 10f);
                        break;
                    case "grenade.beancan.entity":
                    case "grenade.f1.entity":
                        SetAttackRestrictions(AttackType.Explosive, 17.5f, 5f);
                        break;
                    case "mgl.entity":
                        launcher = _attackEntity as BaseProjectile;
                        SetAttackRestrictions(AttackType.Explosive, 150f, 4f, 150f);
                        break;
                    case "rocket_launcher.entity":
                        launcher = _attackEntity as BaseProjectile;
                        SetAttackRestrictions(AttackType.Explosive, 300f, 6f, 150f);
                        break;
                    case "flamethrower.entity":
                        flameThrower = _attackEntity as FlameThrower;
                        SetAttackRestrictions(AttackType.FlameThrower, 10f, (_attackEntity.animationDelay + _attackEntity.deployDelay) * 2f);
                        break;
                    case "compound_bow.entity":
                    case "crossbow.entity":
                    case "speargun.entity":
                        SetAttackRestrictions(AttackType.BaseProjectile, 200f, (_attackEntity.animationDelay + _attackEntity.deployDelay) * 1.25f, 150f);
                        break;
                    case "watergun.entity":
                    case "waterpistol.entity":
                        liquidWeapon = _attackEntity as LiquidWeapon;
                        liquidWeapon.AutoPump = true;
                        SetAttackRestrictions(AttackType.Water, 10f, 2f);
                        break;
                    default: _attackEntity = null; break;
                }
            }

            private void SetAttackRestrictions(AttackType attackType, float attackRange, float attackCooldown, float effectiveRange = 0f)
            {
                if (effectiveRange != 0f)
                {
                    _attackEntity.effectiveRange = effectiveRange;
                }

                this.attackType = attackType;
                this.attackRange = attackRange;
                this.attackCooldown = attackCooldown;
            }

            public bool ValidTarget
            {
                get
                {
                    if (AttackTarget.IsKilled() || ShouldForgetTarget(AttackTarget))
                    {
                        return false;
                    }

                    return true;
                }
            }

            public override void OnDestroy()
            {
                if (!Rust.Application.isQuitting)
                {
                    BaseEntity.Query.Server.RemoveBrain(GetEntity());
                    Instance?.HumanoidBrains?.Remove(uid);
                    LeaveGroup();
                }

                CancelInvoke();
            }

            public override void InitializeAI()
            {
                base.InitializeAI();
                base.ForceSetAge(0f);

                Pet = false;
                sleeping = false;
                UseAIDesign = true;
                AllowedToSleep = false;
                HostileTargetsOnly = false;
                AttackRangeMultiplier = 2f;
                MaxGroupSize = 0;

                Senses.Init(
                    owner: GetEntity(),
                    memoryDuration: 5f,
                    range: 50f,
                    targetLostRange: 75f,
                    visionCone: -1f,
                    checkVision: false,
                    checkLOS: true,
                    ignoreNonVisionSneakers: true,
                    listenRange: 15f,
                    hostileTargetsOnly: false,
                    senseFriendlies: false,
                    ignoreSafeZonePlayers: false,
                    senseTypes: EntityType.Player,
                    refreshKnownLOS: true
                );

                CanUseHealingItems = true;
            }

            public override void AddStates()
            {
                base.AddStates();

                states[AIState.Attack] = new AttackState(this);
            }

            public class AttackState : BaseAttackState
            {
                private new HumanoidBrain brain;

                private IAIAttack attack => brain.Senses.ownerAttack;

                public AttackState(HumanoidBrain humanoidBrain)
                {
                    base.brain = brain = humanoidBrain;
                    base.AgrresiveState = true;
                }

                public new global::HumanNPC GetEntity()
                {
                    return brain.baseEntity;
                }

                public override void StateEnter()
                {
                    if (brain.ValidTarget)
                    {
                        if (InAttackRange())
                        {
                            StartAttacking();
                        }
                        else
                        {
                            StopAttacking();
                        }
                        if (!brain.isStationary && brain.Navigator.CanUseNavMesh)
                        {
                            brain.Navigator.SetDestination(brain.DestinationOverride, BaseNavigator.NavigationSpeed.Fast, 0f, 0f);
                        }
                    }
                }

                public override void StateLeave()
                {
                    StopAttacking();
                }

                private void StopAttacking()
                {
                    if (attack != null)
                    {
                        attack.StopAttacking();
                        brain.Navigator.ClearFacingDirectionOverride();
                    }
                }

                public override StateStatus StateThink(float delta)
                {
                    if (attack == null)
                    {
                        return StateStatus.Error;
                    }
                    if (!brain.ValidTarget)
                    {
                        StopAttacking();

                        return StateStatus.Finished;
                    }
                    if (brain.Senses.ignoreSafeZonePlayers && brain.AttackTarget.InSafeZone())
                    {
                        return StateStatus.Error;
                    }
                    if (!brain.isStationary && brain.Navigator.CanUseNavMesh && !brain.Navigator.SetDestination(brain.DestinationOverride, BaseNavigator.NavigationSpeed.Fast, 0f, 0f))
                    {
                        return StateStatus.Error;
                    }
                    if (!brain.CanLeave(brain.AttackPosition) || !brain.CanShoot())
                    {
                        brain.Forget();

                        StopAttacking();

                        return StateStatus.Finished;
                    }
                    if (InAttackRange())
                    {
                        StartAttacking();
                    }
                    else
                    {
                        StopAttacking();
                    }
                    return StateStatus.Running;
                }

                private bool InAttackRange()
                {
                    return attack.CanAttack(brain.AttackTarget) && brain.IsInAttackRange() && brain.CanSeeTarget(brain.AttackTarget);
                }

                private void StartAttacking()
                {
                    brain.SetAimDirection();

                    if (!brain.CanShoot() || brain.IsAttackOnCooldown() || brain.TryThrowWeapon())
                    {
                        return;
                    }

                    if (brain.attackType == AttackType.Explosive && !brain.launcher.IsNull())
                    {
                        brain.EmulatedFire();
                    }
                    else if (brain.attackType == AttackType.BaseProjectile)
                    {
                        GetEntity().ShotTest(Vector3.Distance(brain.AttackPosition, brain.ServerPosition));
                    }
                    else if (brain.attackType == AttackType.FlameThrower)
                    {
                        brain.UseFlameThrower();
                    }
                    else if (brain.attackType == AttackType.Water)
                    {
                        brain.UseWaterGun();
                    }
                    else brain.MeleeAttack();
                }
            }

            private bool init;

            public void Init()
            {
                if (init) return;
                init = true;
                lastWarpTime = Time.time;
                npc.spawnPos = raid.Location;
                npc.AdditionalLosBlockingLayer = visibleMask;
                Instance.HumanoidBrains[uid = npc.userID] = this;

                IdentifyWeapon();

                SetupNavigator(GetEntity(), GetComponent<BaseNavigator>(), raid.ProtectionRadius, isStationary);
            }

            private void Converge()
            {
                foreach (var brain in Instance.HumanoidBrains.Values)
                {
                    if (brain != this && brain.attackType == attackType && brain.CanConverge(npc) && CanLeave(AttackPosition))
                    {
                        brain.SetTarget(AttackTarget, false);
                        brain.TryToAttack(AttackTarget);
                    }
                }
            }

            public void Forget()
            {
                Senses.Players.Clear();
                Senses.Memory.All.Clear();
                Senses.Memory.Threats.Clear();
                Senses.Memory.Targets.Clear();
                Senses.Memory.Players.Clear();
                Navigator.ClearFacingDirectionOverride();

                DestinationOverride = GetRandomRoamPosition();
                SenseRange = ListenRange = Settings.AggressionRange;
                TargetLostRange = SenseRange * 1.25f;
                AttackTarget = null;

                TryReturnHome();
            }

            private void RandomMove(float radius)
            {
                var to = AttackPosition + UnityEngine.Random.onUnitSphere * radius;

                to.y = TerrainMeta.HeightMap.GetHeight(to);

                SetDestination(to);
            }

            public void SetupNavigator(BaseCombatEntity owner, BaseNavigator navigator, float distance, bool isStationary)
            {
                navigator.CanUseNavMesh = !isStationary && !Rust.Ai.AiManager.nav_disable;

                if (isStationary)
                {
                    navigator.MaxRoamDistanceFromHome = navigator.BestMovementPointMaxDistance = navigator.BestRoamPointMaxDistance = 0f;
                    navigator.DefaultArea = "Not Walkable";
                }
                else
                {
                    navigator.MaxRoamDistanceFromHome = navigator.BestMovementPointMaxDistance = navigator.BestRoamPointMaxDistance = distance * 0.85f;
                    navigator.DefaultArea = "Walkable";
                    navigator.topologyPreference = ((TerrainTopology.Enum)TerrainTopology.EVERYTHING);
                }

                navigator.Agent.agentTypeID = -1372625422;
                navigator.MaxWaterDepth = config.Settings.Management.WaterDepth;

                if (navigator.CanUseNavMesh)
                {
                    navigator.Init(owner, navigator.Agent);
                }
            }

            private void SetAimDirection()
            {
                Navigator.SetFacingDirectionEntity(AttackTarget);
            }

            private void SetDestination()
            {
                SetDestination(GetRandomRoamPosition());
            }

            private void SetDestination(Vector3 destination)
            {
                if (!CanLeave(destination))
                {
                    if (attackType != AttackType.BaseProjectile)
                    {
                        destination = ((destination.XZ3D() - raid.Location.XZ3D()).normalized * (raid.ProtectionRadius * 0.75f)) + raid.Location;

                        destination += UnityEngine.Random.onUnitSphere * (raid.ProtectionRadius * 0.2f);
                    }
                    else
                    {
                        destination = GetRandomRoamPosition();
                    }

                    CurrentSpeed = BaseNavigator.NavigationSpeed.Normal;
                }

                if (destination != DestinationOverride)
                {
                    destination.y = TerrainMeta.HeightMap.GetHeight(destination);

                    DestinationOverride = destination;
                }

                Navigator.SetCurrentSpeed(CurrentSpeed);

                if (Navigator.CurrentNavigationType == BaseNavigator.NavigationType.None && !Rust.Ai.AiManager.ai_dormant && !Rust.Ai.AiManager.nav_disable)
                {
                    Navigator.SetCurrentNavigationType(BaseNavigator.NavigationType.NavMesh);
                }

                if (Navigator.Agent == null || !Navigator.CanUseNavMesh || !Navigator.SetDestination(destination, CurrentSpeed))
                {
                    Navigator.Destination = destination;
                    npc.finalDestination = destination;
                }
            }

            public void SetTarget(BasePlayer player, bool converge = true)
            {
                if (AttackTarget == player)
                {
                    return;
                }

                Senses.Memory.SetKnown(player, npc, null);
                npc.lastAttacker = player;
                AttackTarget = player;

                if (!IsInSenseRange(player.transform.position))
                {
                    SenseRange = ListenRange = Settings.AggressionRange + Vector3.Distance(player.transform.position, ServerPosition);
                    TargetLostRange = SenseRange + (SenseRange * 0.25f);
                }
                else
                {
                    SenseRange = ListenRange = softLimitSenseRange;
                    TargetLostRange = softLimitSenseRange * 1.25f;
                }

                if (converge)
                {
                    Converge();
                }
            }

            private void TryReturnHome()
            {
                if (!isStationary && Settings.CanLeave && !IsInHomeRange())
                {
                    CurrentSpeed = BaseNavigator.NavigationSpeed.Normal;

                    Warp();
                }
            }

            private void TryToAttack() => TryToAttack(null);

            private void TryToAttack(BasePlayer attacker)
            {
                if (attacker.IsNull())
                {
                    attacker = GetBestTarget();
                }

                if (attacker.IsNull())
                {
                    return;
                }

                if (ShouldForgetTarget(attacker))
                {
                    Forget();

                    return;
                }

                SetTarget(attacker);

                if (!CanSeeTarget(attacker))
                {
                    return;
                }

                if (isStationary)
                {
                    SetAimDirection();
                }
                else if (attackType == AttackType.BaseProjectile)
                {
                    TryScientistActions();
                }
                else
                {
                    TryMurdererActions();
                }

                SwitchToState(AIState.Attack, -1);
            }

            private void TryMurdererActions()
            {
                CurrentSpeed = BaseNavigator.NavigationSpeed.Fast;

                if (attackType == AttackType.Explosive)
                {
                    if (IsInAttackRange(20f))
                    {
                        RandomMove(15f);
                    }
                    else
                    {
                        SetDestination(AttackPosition);
                    }
                }
                else if (!IsInReachableRange())
                {
                    RandomMove(15f);
                }
                else if (!IsInAttackRange())
                {
                    if (attackType == AttackType.FlameThrower)
                    {
                        RandomMove(attackRange);
                    }
                    else
                    {
                        SetDestination(AttackPosition);
                    }
                }
            }

            private void TryScientistActions()
            {
                CurrentSpeed = BaseNavigator.NavigationSpeed.Fast;

                SetDestination();
            }

            public void SetupMovement(List<Vector3> positions)
            {
                this.positions = positions;

                if (positions.IsNullOrEmpty())
                {
                    isStationary = true;
                }

                if (!isStationary)
                {
                    InvokeRepeating(TryToRoam, 0f, 7.5f);
                }

                InvokeRepeating(TryToAttack, 1f, 1f);
            }

            private void TryToRoam()
            {
                if (Settings.KillUnderwater && npc.IsSwimming())
                {
                    npc.Kill();
                    Destroy(this);
                    return;
                }

                if (ValidTarget)
                {
                    return;
                }

                if (IsStuck())
                {
                    Warp();

                    Navigator.stuckTimer = 0f;
                }

                CurrentSpeed = BaseNavigator.NavigationSpeed.Normal;

                SetDestination();
            }

            private bool IsStuck() => false; //UnityEngine.Vector3.Distance(npc.transform.position, Navigator.stuckCheckPosition) <= Navigator.StuckDistance;

            public void Warp()
            {
                if (Time.time < lastWarpTime)
                {
                    return;
                }

                lastWarpTime = Time.time + 1f;

                DestinationOverride = GetRandomRoamPosition();

                Navigator.Warp(DestinationOverride);
            }

            private void UseFlameThrower()
            {
                if (flameThrower.ammo < flameThrower.maxAmmo * 0.25)
                {
                    flameThrower.SetFlameState(false);
                    flameThrower.ServerReload();
                    return;
                }
                npc.triggerEndTime = Time.time + attackCooldown;
                flameThrower.SetFlameState(true);
                flameThrower.Invoke(() => flameThrower.SetFlameState(false), 2f);
            }

            private void UseWaterGun()
            {
                RaycastHit hit;
                Physics.Raycast(npc.eyes.BodyRay(), out hit, 10f, 1218652417);
                List<DamageTypeEntry> damage = new List<DamageTypeEntry>();
                WaterBall.DoSplash(hit.point, 2f, ItemManager.FindItemDefinition("water"), 10);
                DamageUtil.RadiusDamage(npc, liquidWeapon.LookupPrefab(), hit.point, 0.15f, 0.15f, damage, 131072, true);
            }

            private void UseChainsaw()
            {
                AttackEntity.TopUpAmmo();
                AttackEntity.ServerUse();
                AttackTarget.Hurt(10f * AttackEntity.npcDamageScale, DamageType.Bleeding, npc);
            }

            private void EmulatedFire()
            {
                if (launcher.HasAttackCooldown()) return;
                float dist;
                string prefab;
                switch (launcher.ShortPrefabName)
                {
                    case "rocket_launcher.entity":
                        prefab = "assets/prefabs/ammo/rocket/rocket_basic.prefab";
                        dist = Vector3.Distance(ServerPosition, AttackPosition);
                        launcher.repeatDelay = 6f;
                        break;
                    case "mgl.entity":
                        prefab = "assets/prefabs/ammo/40mmgrenade/40mm_grenade_he.prefab";
                        launcher.repeatDelay = 4f;
                        dist = Vector3.Distance(ServerPosition, AttackPosition) + 5f;
                        break;
                    default: return;
                }
                Vector3 vector3 = launcher.MuzzlePoint.transform.forward + Vector3.up;
                Vector3 position = launcher.MuzzlePoint.transform.position + (Vector3.up * 1.6f);
                BaseEntity entity = GameManager.server.CreateEntity(prefab, position, GetEntity().eyes.GetLookRotation());
                if (entity.IsNull()) return;
                entity.creatorEntity = GetEntity();
                var serverProjectile = entity.GetComponent<ServerProjectile>();
                if (serverProjectile.IsNull()) return;
                serverProjectile.InitializeVelocity(Quaternion.Euler(vector3) * entity.transform.forward * dist);
                TimedExplosive explosive = entity as TimedExplosive;
                if (!explosive.IsNull())
                {
                    explosive.timerAmountMin = 1;
                    explosive.timerAmountMax = 15;
                }
                entity.Spawn();
                launcher.StartAttackCooldown(launcher.repeatDelay);
            }

            private void MeleeAttack()
            {
                if (baseMelee.IsNull())
                {
                    return;
                }

                if (AttackEntity is Chainsaw)
                {
                    UseChainsaw();
                    return;
                }

                Vector3 position = AttackPosition;
                AttackEntity.StartAttackCooldown(AttackEntity.repeatDelay * 2f);
                npc.SignalBroadcast(BaseEntity.Signal.Attack, string.Empty, null);
                if (baseMelee.swingEffect.isValid)
                {
                    Effect.server.Run(baseMelee.swingEffect.resourcePath, position, Vector3.forward, npc.Connection, false);
                }
                HitInfo hitInfo = new HitInfo
                {
                    damageTypes = new DamageTypeList(),
                    DidHit = true,
                    Initiator = npc,
                    HitEntity = AttackTarget,
                    HitPositionWorld = position,
                    HitPositionLocal = AttackTarget.transform.InverseTransformPoint(position),
                    HitNormalWorld = npc.eyes.BodyForward(),
                    HitMaterial = StringPool.Get("Flesh"),
                    PointStart = ServerPosition,
                    PointEnd = position,
                    Weapon = AttackEntity,
                    WeaponPrefab = AttackEntity
                };

                hitInfo.damageTypes.Set(DamageType.Slash, baseMelee.TotalDamage() * AttackEntity.npcDamageScale);
                Effect.server.ImpactEffect(hitInfo);
                AttackTarget.OnAttacked(hitInfo);
            }

            public bool TryThrowWeapon()
            {
                if (!IsInThrowRange())
                {
                    return false;
                }

                var thrownWeapon = AttackEntity as ThrownWeapon;

                if (thrownWeapon.IsNull())
                {
                    return false;
                }

                npc.SetAiming(true);
                SetAimDirection();

                npc.Invoke(() =>
                {
                    if (!ValidTarget)
                    {
                        CurrentSpeed = BaseNavigator.NavigationSpeed.Normal;

                        Forget();
                        SetDestination();
                        npc.SetAiming(false);

                        return;
                    }

                    if (IsInThrowRange())
                    {
                        thrownWeapon.GetItem().amount++;
                        thrownWeapon.ServerThrow(AttackPosition);
                    }
                    else nextAttackTime = Time.realtimeSinceStartup + 1f;

                    npc.SetAiming(false);
                    RandomMove(15f);
                }, 1f);

                return true;
            }

            private bool CanConverge(global::HumanNPC other)
            {
                if (ValidTarget && !ShouldForgetTarget(AttackTarget)) return false;
                if (other.IsKilled() || other.IsDead()) return false;
                return IsInTargetRange(other.transform.position);
            }

            private bool CanLeave(Vector3 destination)
            {
                return Settings.CanLeave || isStationary || IsInLeaveRange(destination);
            }

            private bool CanSeeTarget(BasePlayer target)
            {
                if (isStationary)
                {
                    return Senses.Memory.IsLOS(target);
                }

                if (attackType == AttackType.Explosive && raid.Options.NPC.CounterRaid && raid.IsInForwardOperatingBase(target.ServerPosition))
                {
                    return true;
                }

                if (Navigator.CurrentNavigationType == BaseNavigator.NavigationType.None && (attackType == AttackType.FlameThrower || attackType == AttackType.Melee))
                {
                    return true;
                }

                if (Vector3.Distance(ServerPosition, target.ServerPosition) < 10f || Senses.Memory.IsLOS(target))
                {
                    return true;
                }

                nextAttackTime = Time.realtimeSinceStartup + 1f;

                return false;
            }

            public bool CanRoam(Vector3 destination)
            {
                return destination == DestinationOverride && IsInSenseRange(destination);
            }

            private bool CanShoot()
            {
                if (attackType == AttackType.None)
                {
                    return false;
                }

                return Settings.CanShoot || attackType != AttackType.BaseProjectile && attackType != AttackType.Explosive || IsInLeaveRange(AttackPosition);
            }

            public BasePlayer GetBestTarget()
            {
                if (npc.IsWounded())
                {
                    return null;
                }
                float delta = -1f;
                BasePlayer target = null;
                foreach (var player in Senses.Memory.Targets.OfType<BasePlayer>())
                {
                    if (player.IsNull() || player.health <= 0f || player.IsDead() || player.limitNetworking) continue;
                    if (!player.IsHuman() && !config.Settings.Management.TargetNpcs) continue;
                    float dist = Vector3.Distance(player.transform.position, npc.transform.position);
                    float rangeDelta = 1f - Mathf.InverseLerp(1f, SenseRange, dist);
                    rangeDelta += (CanSeeTarget(player) ? 2f : 0f);
                    if (rangeDelta <= delta) continue;
                    target = player;
                    delta = rangeDelta;
                }
                return target;
            }

            private Vector3 GetRandomRoamPosition()
            {
                return positions.GetRandom();
            }

            private bool IsAttackOnCooldown()
            {
                if (isStationary && attackType == AttackType.BaseProjectile)
                {
                    return false;
                }

                if (attackType == AttackType.None || Time.realtimeSinceStartup < nextAttackTime)
                {
                    return true;
                }

                if (attackCooldown > 0f)
                {
                    nextAttackTime = Time.realtimeSinceStartup + attackCooldown;
                }

                return false;
            }

            private bool IsInAttackRange(float range = 0f)
            {
                return InRange(ServerPosition, AttackPosition, range == 0f ? attackRange : range);
            }

            private bool IsInHomeRange()
            {
                return InRange(ServerPosition, raid.Location, Mathf.Max(raid.ProtectionRadius, TargetLostRange));
            }

            private bool IsInLeaveRange(Vector3 destination)
            {
                return InRange(raid.Location, destination, raid.ProtectionRadius);
            }

            private bool IsInReachableRange()
            {
                if (AttackPosition.y - ServerPosition.y > attackRange)
                {
                    return false;
                }

                return attackType != AttackType.Melee || InRange(AttackPosition, ServerPosition, 15f);
            }

            private bool IsInSenseRange(Vector3 destination)
            {
                return InRange2D(raid.Location, destination, SenseRange);
            }

            private bool IsInTargetRange(Vector3 destination)
            {
                return InRange2D(raid.Location, destination, TargetLostRange);
            }

            private bool IsInThrowRange()
            {
                return InRange(ServerPosition, AttackPosition, attackRange);
            }

            private bool ShouldForgetTarget(BasePlayer target)
            {
                return target.health <= 0f || target.limitNetworking || !IsInTargetRange(target.transform.position);
            }
        }

        public class Raider
        {
            public ulong userid;
            public string id;
            public string displayName;
            public bool reward = true;
            public bool IsRaider;
            public bool IsAlly;
            public bool IsAllowed;
            public bool PreEnter = true;
            public bool LockedOnEnter;
            public BasePlayer player;
            public PlayerInputEx Input;
            public bool IsParticipant
            {
                get
                {
                    return IsRaider || LockedOnEnter;
                }
                set
                {
                    IsRaider = value;
                    LockedOnEnter = value;
                }
            }
            public Raider(BasePlayer target)
            {
                player = target;
                userid = target.userID;
                id = target.userID.ToString();
                displayName = target.displayName;
            }
            public void DestroyInput()
            {
                if (Input != null)
                {
                    UnityEngine.Object.Destroy(Input);
                }
            }
            public void Reset()
            {
                DestroyInput();
                IsAllowed = false;
                IsParticipant = false;
            }
        }

        public class DamageRecord
        {
            public BasePlayer player;
            public ulong userid;
            public DamageRecord() { }
            public DamageRecord(BasePlayer player)
            {
                this.player = player;
                userid = player.userID;
            }
        }

        public class RaidableBase : FacepunchBehaviour
        {
            private const float Radius = M_RADIUS;
            public List<ulong> intruders { get; set; } = Pool.GetList<ulong>();
            public Dictionary<ulong, Raider> raiders { get; set; } = Pool.Get<Dictionary<ulong, Raider>>();
            public Hash<uint, float> conditions { get; set; } = Pool.Get<Hash<uint, float>>();
            internal HashSet<StorageContainer> _containers { get; set; } = new HashSet<StorageContainer>();
            internal HashSet<StorageContainer> _allcontainers { get; set; } = new HashSet<StorageContainer>();
            public List<ScientistNPC> npcs { get; set; } = Pool.GetList<ScientistNPC>();
            public List<HotAirBalloon> habs { get; set; } = Pool.GetList<HotAirBalloon>();
            public List<BaseMountable> mountables { get; set; } = Pool.GetList<BaseMountable>();
            public Dictionary<uint, BackpackData> backpacks { get; set; } = Pool.Get<Dictionary<uint, BackpackData>>();
            public Dictionary<uint, DamageRecord> records { get; set; } = Pool.Get<Dictionary<uint, DamageRecord>>();
            public List<Vector3> foundations { get; set; } = new List<Vector3>(); // 2.4.3 arena wall fix (?)
            public List<BaseEntity> locks { get; set; } = Pool.GetList<BaseEntity>();
            private List<BuildingBlock> blocks { get; set; } = Pool.GetList<BuildingBlock>();
            private List<Vector3> _randomSpots { get; set; } = Pool.GetList<Vector3>();
            private List<SphereEntity> spheres { get; set; } = Pool.GetList<SphereEntity>();
            private List<IOEntity> lights { get; set; } = Pool.GetList<IOEntity>();
            private List<BaseOven> ovens { get; set; } = Pool.GetList<BaseOven>();
            public List<AutoTurret> turrets { get; set; } = Pool.GetList<AutoTurret>();
            private List<Door> doors { get; set; } = Pool.GetList<Door>();
            private List<CustomDoorManipulator> doorControllers { get; set; } = Pool.GetList<CustomDoorManipulator>();
            public Dictionary<string, float> lastActive { get; set; } = Pool.Get<Dictionary<string, float>>();
            public List<string> ids { get; set; } = Pool.GetList<string>();
            private List<Locker> lockers { get; set; } = Pool.GetList<Locker>();
            private List<BaseEntity> _decorDeployables { get; set; } = Pool.GetList<BaseEntity>();
            private Dictionary<string, ulong> skins { get; set; } = Pool.Get<Dictionary<string, ulong>>();
            private Dictionary<uint, ulong> skinIds { get; set; } = Pool.Get<Dictionary<uint, ulong>>();
            private Dictionary<TriggerBase, BaseEntity> triggers { get; set; } = Pool.Get<Dictionary<TriggerBase, BaseEntity>>();
            private List<SleepingBag> _beds { get; set; } = Pool.GetList<SleepingBag>();
            private Dictionary<SleepingBag, ulong> _bags { get; set; } = Pool.Get<Dictionary<SleepingBag, ulong>>();
            private List<BaseEntity> _rugs { get; set; } = Pool.GetList<BaseEntity>();
            public List<SamSite> samsites { get; set; } = Pool.GetList<SamSite>();
            public List<VendingMachine> vms { get; set; } = Pool.GetList<VendingMachine>();
            public Dictionary<string, ItemDefinition> Definitions { get; set; }
            public BuildingPrivlidge priv { get; set; }
            private Dictionary<string, List<string>> npcKits { get; set; }
            private MapMarkerExplosion explosionMarker { get; set; }
            private MapMarkerGenericRadius genericMarker { get; set; }
            private VendingMachineMapMarker vendingMarker { get; set; }
            private Coroutine setupRoutine { get; set; } = null;
            private GameObject go { get; set; }
            private bool IsInvokingCanFinish { get; set; }
            public bool IsDespawning { get; set; }
            public Vector3 PastedLocation { get; set; }
            public Vector3 Location { get; private set; }
            public string ProfileName { get; set; }
            public string BaseName { get; set; }
            public int BaseIndex { get; set; } = -1;
            public uint BuildingID { get; set; }
            public uint NetworkID { get; set; } = uint.MaxValue;
            public Color NoneColor { get; set; }
            public BasePlayer owner { get; set; }
            public bool ownerFlag { get; set; }
            public string ID { get; set; } = "0";
            public ulong permOwnerId { get; set; }
            public ulong ownerId { get; set; }
            public string ownerName { get; set; }
            public Raider riOwner { get; set; }
            public float loadTime { get; set; }
            public float spawnTime { get; set; }
            public float despawnTime { get; set; }
            public bool HasDroppedItems { get; set; }
            public bool AllowPVP { get; set; }
            public BuildingOptions Options { get; set; }
            public bool IsAuthed { get; set; }
            public bool IsOpened { get; set; } = true;
            public bool IsResetting { get; set; }
            public bool IsUnloading { get; set; }
            public bool IsPayLocked { get; set; }
            public int npcMaxAmount { get; set; }
            public int npcMaxAmountInside { get; set; } = -1;
            public int npcAmountInside { get; set; }
            public int npcAmountThrown { get; set; }
            public RaidableType Type { get; set; }
            public bool IsLoading { get; set; }
            private bool markerCreated { get; set; }
            private bool lightsOn { get; set; }
            private int itemAmountSpawned { get; set; }
            private int treasureAmount { get; set; }
            private bool privSpawned { get; set; }
            public string markerName { get; set; }
            public string NoMode { get; set; }
            public bool isAuthorized { get; set; }
            public bool IsEngaged { get; set; }
            private bool _undoStructures { get; set; }
            private bool _undoDeployables { get; set; }
            private bool _undoMounts { get; set; }
            private bool _undoTeleport { get; set; }
            private int _undoLimit { get; set; }
            private Dictionary<Elevator, BMGELEVATOR> Elevators { get; set; } = new Dictionary<Elevator, BMGELEVATOR>();
            private ItemDefinition lowgradefuel { get; set; } = ItemManager.FindItemDefinition("lowgradefuel");
            public HashSet<BaseEntity> Entities { get; set; } = new HashSet<BaseEntity>();
            public Dictionary<BaseEntity, Vector3> BuiltList { get; set; } = new Dictionary<BaseEntity, Vector3>();
            public RaidableSpawns spawns { get; set; }
            public float RemoveNearDistance { get; set; }
            public bool IsDamaged { get; set; }
            public bool IsCompleted { get; set; }
            public List<Payment> payments { get; set; } = Pool.GetList<Payment>();
            private static bool isSpawning { get; set; }
            private static float isSpawningTime { get; set; }
            public float ProtectionRadius => Options.ProtectionRadius(Type);
            public int foundationsDestroyed { get; set; }
            public RaidableBases MyInstance { get; set; }

            private object[] hookObjects
            {
                get
                {
                    return new object[] { Location, (int)Options.Mode, AllowPVP, ID, spawnTime, despawnTime, loadTime, ownerId, GetOwner(), GetRaiders(), GetIntruders(), Entities, BaseName };
                }
            }

            public static bool IsSpawning
            {
                get
                {
                    if (Time.time - isSpawningTime > 180f)
                    {
                        isSpawning = false;
                    }

                    return isSpawning;
                }
                set
                {
                    isSpawningTime = Time.time;
                    isSpawning = value;
                }
            }

            public Raider GetRaider(BasePlayer player)
            {
                Raider ri;
                if (!raiders.TryGetValue(player.userID, out ri))
                {
                    raiders[player.userID] = ri = new Raider(player);
                }
                return ri;
            }

            public void AddEntity(BaseEntity entity)
            {
                if (!entity.IsValid())
                {
                    return;
                }
                if (!data.TemporaryUID.Contains(entity.net.ID))
                {
                    data.TemporaryUID.Add(entity.net.ID);
                }
                Entities.Add(entity);
            }

            private void ResetToPool<T>(ICollection<T> collection)
            {
                collection.Clear();

                Pool.Free(ref collection);
            }

            public void ResetToPool()
            {
                ResetToPool(intruders);
                ResetToPool(raiders);
                ResetToPool(conditions);
                ResetToPool(records);
                ResetToPool(backpacks);
                ResetToPool(lastActive);
                ResetToPool(locks);
                ResetToPool(blocks);
                ResetToPool(npcs);
                ResetToPool(foundations);
                ResetToPool(spheres);
                ResetToPool(lights);
                ResetToPool(ovens);
                ResetToPool(turrets);
                ResetToPool(doors);
                ResetToPool(doorControllers);
                ResetToPool(ids);
                ResetToPool(lockers);
                ResetToPool(_decorDeployables);
                ResetToPool(skins);
                ResetToPool(skinIds);
                ResetToPool(triggers);
                ResetToPool(_beds);
                ResetToPool(_bags);
                ResetToPool(_rugs);
                ResetToPool(samsites);
                ResetToPool(payments);
                ResetToPool(vms);
                ResetToPool(mountables);
                ResetToPool(habs);
            }

            private void Awake()
            {
                _lastInvokeUpdate = Time.time;
                markerName = config.Settings.Markers.MarkerName;
                spawnTime = Time.time;
                go = gameObject;
            }

            private void OnDestroy()
            {
                Despawn();
                Destroy(go);
                Destroy(this);
            }



            internal List<BasePlayer> playersInSphere = new List<BasePlayer>();
            internal List<HotAirBalloon> habRemoval = new List<HotAirBalloon>();
            internal List<HotAirBalloon> habsInSphere = new List<HotAirBalloon>();
            internal List<BaseMountable> mountableRemoval = new List<BaseMountable>();
            internal List<BaseMountable> mountablesInSphere = new List<BaseMountable>();
            internal float lastMountCheckTime;

            private void CheckEntitiesInSphere()
            {
                FindEntities(Location, ProtectionRadius, 174088);
                CheckMountablesExiting();
                CheckMountablesEntering();
                CheckIntrudersExiting();
                CheckIntrudersEntering();
                habsInSphere.Clear();
                playersInSphere.Clear();
                mountablesInSphere.Clear();
            }

            public void FindEntities(Vector3 a, float n, int m)
            {
                var entities = FindEntitiesOfType<BaseCombatEntity>(a, n, m);
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];
                    if (entity.IsKilled() || entity.Health() <= 0f || entity.IsDead())
                    {
                        continue;
                    }
                    if (entity is BasePlayer)
                    {
                        playersInSphere.Add(entity as BasePlayer);
                    }
                    else if (entity is BaseMountable)
                    {
                        mountablesInSphere.Add(entity as BaseMountable);
                    }
                    else if (entity is HotAirBalloon)
                    {
                        habsInSphere.Add(entity as HotAirBalloon);
                    }
                }
            }

            private void CheckMountablesExiting()
            {
                if (habs.Count > 0 && Time.time > lastMountCheckTime)
                {
                    lastMountCheckTime = Time.time + 1f;
                    habs.ForEach(m =>
                    {
                        if (m.IsKilled() || m.health <= 0 || m.IsDead())
                        {
                            habRemoval.Add(m);
                        }
                        else if (!habsInSphere.Contains(m))
                        {
                            GetMountedPlayers(m).ForEach(player =>
                            OnPlayerExit(player, player.IsDead()));
                            habRemoval.Add(m);
                        }
                    });
                    if (habRemoval.Count > 0)
                    {
                        habRemoval.ForEach(m => habs.Remove(m));
                        habRemoval.Clear();
                    }
                }
                if (mountables.Count > 0 && Time.time > lastMountCheckTime)
                {
                    lastMountCheckTime = Time.time + 1f;
                    mountables.ForEach(m =>
                    {
                        if (m.IsKilled() || m.health <= 0 || m.IsDead())
                        {
                            mountableRemoval.Add(m);
                        }
                        else if (!mountablesInSphere.Contains(m))
                        {
                            GetMountedPlayers(m).ForEach(player =>
                            OnPlayerExit(player, player.IsDead()));
                            mountableRemoval.Add(m);
                        }
                    });
                    if (mountableRemoval.Count > 0)
                    {
                        mountableRemoval.ForEach(m => mountables.Remove(m));
                        mountableRemoval.Clear();
                    }
                }
            }

            private void CheckMountablesEntering()
            {
                if (habsInSphere.Count > 0)
                {
                    habsInSphere.ForEach(m =>
                    {
                        if (!habs.Contains(m))
                        {
                            OnPreEnterRaid(m);
                        }
                    });
                }
                if (mountablesInSphere.Count > 0)
                {
                    mountablesInSphere.ForEach(m =>
                    {
                        if (!mountables.Contains(m))
                        {
                            OnPreEnterRaid(m);
                        }
                    });
                }
            }

            private void CheckIntrudersExiting()
            {
                if (intruders.Count > 0)
                {
                    raiders.Values.ToList().ForEach(ri =>
                    {
                        if (!ri.player.IsNull() && !playersInSphere.Contains(ri.player) && intruders.Contains(ri.userid))
                        {
                            OnPlayerExit(ri.player, ri.player.IsDead());
                        }
                    });
                }
            }

            private void CheckIntrudersEntering()
            {
                if (playersInSphere.Count > 0)
                {
                    playersInSphere.ToList().ForEach(player =>
                    {
                        if (!player.IsKilled() && !intruders.Contains(player.userID))
                        {
                            OnPreEnterRaid(player);
                        }
                    });
                }
            }

            private void OnPreEnterRaid(HotAirBalloon m)
            {
                var players = GetMountedPlayers(m);

                playersInSphere.RemoveAll(players.Contains);
                players.RemoveAll(x => intruders.Contains(x.userID));

                if (TryRemoveMountable(m, players))
                {
                    return;
                }

                habs.Add(m);
                players.ForEach(player =>
                OnPreEnterRaid(player));
            }

            private void OnPreEnterRaid(BaseMountable m)
            {
                var players = GetMountedPlayers(m);

                playersInSphere.RemoveAll(players.Contains);
                players.RemoveAll(x => intruders.Contains(x.userID));

                if (TryRemoveMountable(m, players))
                {
                    return;
                }

                mountables.Add(m);
                players.ForEach(player =>
                OnPreEnterRaid(player));
            }

            private void OnPreEnterRaid(BasePlayer player)
            {
                if (!player.IsHuman() || IsUnderground(player.transform.position))
                {
                    return;
                }

                if (IsLoading && !CanBypass(player))
                {
                    RemovePlayer(player, EjectCode.Spawning, Location, ProtectionRadius, Type);
                    return;
                }

                if (!player.IsConnected && player.secondsSleeping < 2f)
                {
                    return;
                }

                if (RemoveFauxAdmin(player) || IsScavenging(player))
                {
                    return;
                }

                OnEnterRaid(player, false);
            }

            public void OnEnterRaid(BasePlayer target, bool checkUnderground = true)
            {
                if (checkUnderground && IsUnderground(target.transform.position))
                {
                    return;
                }

                if (Type != RaidableType.None && CannotEnter(target, true) && RemovePlayer(target, EjectCode.Enter, Location, ProtectionRadius, Type))
                {
                    return;
                }

                if (!intruders.Contains(target.userID))
                {
                    intruders.Add(target.userID);
                }

                Protector();

                if (!intruders.Contains(target.userID))
                {
                    return;
                }

                Raider ri = GetRaider(target);

                ri.DestroyInput();

                if (config.Settings.Management.AllowLadders)
                {
                    target.gameObject.AddComponent<PlayerInputEx>().Setup(this);
                }

                StopUsingWand(target);

                if (config.EventMessages.AnnounceEnterExit)
                {
                    QueueNotification(target, AllowPVP ? "OnPlayerEntered" : "OnPlayerEnteredPVE");
                }

                UI.UpdateStatusUI(target);

                Interface.CallHook("OnPlayerEnteredRaidableBase", new object[] { target, Location, AllowPVP, (int)Options.Mode, ID, spawnTime, despawnTime, loadTime, ownerId, BaseName });

                if (config.Settings.Management.PVPDelay > 0)
                {
                    Interface.CallHook("OnPlayerPvpDelayEntry", new object[] { target, (int)Options.Mode, Location, AllowPVP, ID, spawnTime, despawnTime, loadTime, ownerId, BaseName });
                }

                foreach (var brain in Instance.HumanoidBrains.Values)
                {
                    if (!InRange2D(brain.DestinationOverride, Location, brain.SenseRange))
                    {
                        continue;
                    }

                    //brain.states[AIState.Attack].StateEnter();
                    brain.SwitchToState(AIState.Attack, -1);
                }

                UpdateTime(target, true);
                ri.PreEnter = false;
            }

            public void OnPlayerExit(BasePlayer target, bool skipDelay = true)
            {
                if (!target.IsHuman())
                {
                    return;
                }

                MyInstance.BlacklistTimers[target.userID] = Time.realtimeSinceStartup;
                UpdateTime(target, false);
                UI.DestroyStatusUI(target);

                if (!intruders.Contains(target.userID))
                {
                    return;
                }

                Raider ri = GetRaider(target);

                ri.DestroyInput();
                intruders.Remove(target.userID);

                if (ri.PreEnter)
                {
                    return;
                }

                Interface.CallHook("OnPlayerExitedRaidableBase", new object[] { target, Location, AllowPVP, (int)Options.Mode, ID, spawnTime, despawnTime, loadTime, ownerId, BaseName });

                if (config.Settings.Management.PVPDelay > 0)
                {
                    if (skipDelay || !Instance.IsPVE() || !AllowPVP)
                    {
                        goto enterExit;
                    }

                    if (config.EventMessages.AnnounceEnterExit)
                    {
                        string arg = mx("PVPFlag", target.UserIDString).Replace("[", string.Empty).Replace("] ", string.Empty);
                        QueueNotification(target, "DoomAndGloom", arg, config.Settings.Management.PVPDelay);
                    }

                    ulong id = target.userID;
                    DelaySettings ds;
                    if (!Instance.PvpDelay.TryGetValue(id, out ds))
                    {
                        Instance.PvpDelay[id] = ds = new DelaySettings
                        {
                            Timer = Instance.timer.Once(config.Settings.Management.PVPDelay, () =>
                            {
                                Interface.CallHook("OnPlayerPvpDelayExpired", new object[] { target, (int)Options.Mode, Location, AllowPVP, ID, spawnTime, despawnTime, loadTime, ownerId, BaseName });
                                Instance.PvpDelay.Remove(id);
                            }),
                            AllowPVP = AllowPVP,
                            RaidableBase = this
                        };
                    }
                    else ds.Timer.Reset();

                    return;
                }

            enterExit:
                if (config.EventMessages.AnnounceEnterExit)
                {
                    QueueNotification(target, AllowPVP ? "OnPlayerExit" : "OnPlayerExitPVE");
                }
            }

            private bool IsScavenging(BasePlayer player)
            {
                if (IsOpened || !config.Settings.Management.EjectScavengers || !ownerId.IsSteamId() || CanBypass(player))
                {
                    return false;
                }

                return !Any(player.userID) && !IsAlly(player) && RemovePlayer(player, EjectCode.Scavenging, Location, ProtectionRadius, Type);
            }

            private bool RemoveFauxAdmin(BasePlayer player)
            {
                if (player.IsReallyValid() && player.IsDeveloper && player.HasPermission("fauxadmin.allowed") && player.HasPermission("raidablebases.block.fauxadmin") && player.IsCheating())
                {
                    RemovePlayer(player, EjectCode.FauxAdmin, Location, ProtectionRadius, Type);
                    QueueNotification(player, "NoFauxAdmin");
                    OnPlayerExit(player, false);
                    return true;
                }

                return false;
            }

            private bool IsBanned(BasePlayer player)
            {
                if (player.HasPermission("raidablebases.banned"))
                {
                    QueueNotification(player, player.IsAdmin ? "BannedAdmin" : "Banned");
                    return true;
                }

                return false;
            }

            private bool Teleported(BasePlayer p)
            {
                if (!config.Settings.Management.AllowTeleport && p.IsConnected && !CanBypass(p) && Interface.CallHook("OnBlockRaidableBasesTeleport", p, Location) == null)
                {
                    if (NearFoundation(p.transform.position))
                    {
                        QueueNotification(p, "CannotTeleport");
                        return true;
                    }
                }

                return false;
            }

            private bool IsHogging(BasePlayer player, bool shouldMessage = true)
            {
                if (!config.Settings.Management.PreventHogging || !player.IsReallyValid() || CanBypass(player) || player.HasPermission("raidablebases.hoggingbypass"))
                {
                    return false;
                }

                foreach (var raid in Instance.Raids)
                {
                    if (raid.AllowPVP && config.Settings.Management.BypassUseOwnersForPVP)
                    {
                        continue;
                    }

                    if (!raid.AllowPVP && config.Settings.Management.BypassUseOwnersForPVE)
                    {
                        continue;
                    }

                    if (!raid.IsPayLocked && raid.IsOpened && raid.BaseIndex != BaseIndex && raid.Any(player.userID, false))
                    {
                        if (shouldMessage)
                        {
                            QueueNotification(player, "HoggingFinishYourRaid", PositionToGrid(raid.Location));
                        }

                        return true;
                    }
                }

                if (!config.Settings.Management.Lockout.IsBlocking() || player.HasPermission("raidablebases.blockbypass"))
                {
                    return false;
                }

                foreach (var raid in Instance.Raids)
                {
                    if (raid.BaseIndex != BaseIndex && !raid.IsPayLocked && raid.IsOpened && raid.Type != RaidableType.None && IsAllyHogging(player, raid))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool IsAllyHogging(BasePlayer player, RaidableBase raid)
            {
                if (CanBypass(player))
                {
                    return false;
                }

                if (raid.AllowPVP && config.Settings.Management.BypassUseOwnersForPVP)
                {
                    return false;
                }

                if (!raid.AllowPVP && config.Settings.Management.BypassUseOwnersForPVE)
                {
                    return false;
                }

                foreach (var target in raid.GetIntruders().Where(x => x != player && !CanBypass(x)))
                {
                    if (config.Settings.Management.Lockout.BlockTeams && raid.IsAlly(player.userID, target.userID, AlliedType.Team))
                    {
                        TryMessage(player, "HoggingFinishYourRaidTeam", target.displayName, PositionToGrid(raid.Location));
                        return true;
                    }

                    if (config.Settings.Management.Lockout.BlockFriends && raid.IsAlly(player.userID, target.userID, AlliedType.Friend))
                    {
                        TryMessage(player, "HoggingFinishYourRaidFriend", target.displayName, PositionToGrid(raid.Location));
                        return true;
                    }

                    if (config.Settings.Management.Lockout.BlockClans && raid.IsAlly(player.userID, target.userID, AlliedType.Clan))
                    {
                        TryMessage(player, "HoggingFinishYourRaidClan", target.displayName, PositionToGrid(raid.Location));
                        return true;
                    }
                }

                return false;
            }

            private void CheckBackpacks(bool bypass = false)
            {
                foreach (var data in backpacks.ToList())
                {
                    if (EjectBackpack(data.Key, data.Value, bypass))
                    {
                        backpacks.Remove(data.Key);
                    }
                }
            }

            private void Protector()
            {
                if (backpacks.Count > 0)
                {
                    CheckBackpacks(!AllowPVP && Options.EjectBackpacksPVE);
                }

                if (Type == RaidableType.None || intruders.Count == 0)
                {
                    return;
                }

                foreach (var ri in raiders.Values.ToList())
                {
                    if (ri.player.IsNull() || RemoveFauxAdmin(ri.player) || ri.player == owner || ri.IsAllowed || CanBypass(ri.player) || !intruders.Contains(ri.userid))
                    {
                        continue;
                    }

                    if (CanEject(ri.player))
                    {
                        ri.Reset();
                        UI.DestroyStatusUI(ri.player);
                        RemovePlayer(ri.player, EjectCode.Protector, Location, ProtectionRadius, Type);
                        continue;
                    }

                    if (config.Settings.Management.LockToRaidOnEnter && !ri.LockedOnEnter)
                    {
                        QueueNotification(ri.player, "OnLockedToRaid");

                        ri.LockedOnEnter = true;
                    }

                    ri.IsAllowed = true;
                }
            }

            public void DestroyUI()
            {
                TryInvokeMethod(DestroyElevators);
                GetIntruders().ForEach(UI.DestroyStatusUI);
                raiders.ToList().ForEach(ri => TrySetLockout(ri.Value));
            }

            public static void Unload(List<RaidableBase> raids, bool isShutdown)
            {
                foreach (var raid in raids.ToList())
                {
                    if (raid.setupRoutine != null)
                    {
                        raid.StopCoroutine(raid.setupRoutine);
                    }

                    if (isShutdown)
                    {
                        raid.IsDespawning = true;
                    }

                    raid.IsUnloading = true;
                    raid.DestroyLocks();
                    raid.DestroyNpcs();
                    raid.CancelInvoke();
                }

                IsSpawning = false;
            }

            public void Despawn() => Despawn(true);

            public void Despawn(bool destroyEntities)
            {
                if (!CanDespawn())
                {
                    return;
                }

                TryInvokeMethod(RemoveAllFromEvent);
                TryInvokeMethod(StopAllCoroutines);
                TryInvokeMethod(CancelInvoke);
                TryInvokeMethod(SetNoDrops);
                TryInvokeMethod(DestroyUI);
                TryInvokeMethod(DestroyNpcs);
                TryInvokeMethod(DestroyInputs);
                TryInvokeMethod(DestroySpheres);
                TryInvokeMethod(DestroyMapMarkers);
                TryInvokeMethod(DestroyElevators);
                if (destroyEntities)
                {
                    TryInvokeMethod(DestroyEntities);
                }
                TryInvokeMethod(ResetSleepingBags);
                TryInvokeMethod(ResetToPool);
                TryInvokeMethod(CheckSubscribe);
                TryInvokeMethod(StartPurchaseCooldown);

                Interface.CallHook("OnRaidableBaseEnded", hookObjects);

                Destroy(this);
            }

            private bool CanDespawn()
            {
                if (IsDespawning)
                {
                    return false;
                }

                Interface.CallHook("OnRaidableBaseDespawn", hookObjects);

                IsDespawning = true;
                IsOpened = false;

                Instance?.Locations?.RemoveAll(e => e.BaseIndex == BaseIndex);

                return true;
            }

            private void RemoveAllFromEvent()
            {
                GetIntruders().ToList().ForEach(player => OnPlayerExit(player));
            }

            private void CheckSubscribe()
            {
                if (Instance.IsNull())
                {
                    return;
                }

                Instance.Raids.Remove(this);

                if (Instance.Raids.Count == 0)
                {
                    if (IsUnloading)
                    {
                        UnsetStatics();
                    }
                    else
                    {
                        Instance.UnsubscribeHooks();
                        spawns?.AddNear(Location, RemoveNearDistance, CacheType.Generic, HasDroppedItems);
                    }
                }
            }

            private void DestroyElevators()
            {
                if (!Elevators.IsNullOrEmpty())
                {
                    TryInvokeMethod(RemoveParentFromEntitiesOnElevators);
                    Elevators.ToList().ForEach(element => Destroy(element.Value));
                }
            }

            private void DestroyEntities()
            {
                MyInstance.UndoLoop(
                    new UndoSettings(
                    Entities.ToList(),
                    _undoLimit,
                    _undoMounts,
                    _undoStructures,
                    _undoDeployables,
                    _undoTeleport), 0, 3, spawnTime);
            }

            public void UndoInit()
            {
                _undoStructures = config.Settings.Management.DoNotDestroyStructures;
                _undoDeployables = config.Settings.Management.DoNotDestroyDeployables;
                _undoMounts = config.Settings.Management.DespawnMounts;
                _undoTeleport = Options.Setup.TeleportEntities;
                _undoLimit = Mathf.Clamp(Options.Setup.DespawnLimit, 1, 500);
            }

            public void OnBuildingPrivilegeDestroyed()
            {
                Interface.CallHook("OnRaidableBasePrivilegeDestroyed", hookObjects);
            }

            public void UpdateTime(BasePlayer player, bool state)
            {
                if (!player.IsConnected || !player.HasPermission("raidablebases.time"))
                {
                    return;
                }

                int time = state ? Options.ForcedTime : -1;

                if (player.IsAdmin)
                {
                    player.SendConsoleCommand("admintime", time);
                    player.SendNetworkUpdateImmediate();
                }
                else if (!player.IsFlying)
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    player.SendNetworkUpdateImmediate();
                    player.SendConsoleCommand("admintime", time);
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    player.SendNetworkUpdateImmediate();
                }
            }

            public BasePlayer GetOwner()
            {
                if (!owner.IsNull())
                {
                    return owner;
                }

                foreach (var player in GetRaiders())
                {
                    return player;
                }

                return null;
            }

            public List<BasePlayer> GetIntruders()
            {
                return raiders.Values.Where(x => intruders.Contains(x.userid) && x.player.IsReallyValid()).Select(x => x.player).ToList();
            }

            public List<BasePlayer> GetRaiders()
            {
                return raiders.Values.Where(x => x.player.IsReallyValid() && x.IsParticipant).Select(x => x.player).ToList();
            }

            public bool AddLooter(BasePlayer looter, HitInfo hitInfo = null)
            {
                if (!looter.IsHuman())
                {
                    return false;
                }

                if (!IsAlly(looter))
                {
                    if (hitInfo != null && CanEject())
                    {
                        TryMessage(looter, "NoDamageToEnemyBase");
                        NullifyDamage(hitInfo);
                    }
                    return false;
                }

                if (looter.IsFlying || looter.limitNetworking)
                {
                    return true;
                }

                UpdateStatus(looter);

                if (IsHogging(looter, false))
                {
                    NullifyDamage(hitInfo);
                    return false;
                }

                GetRaider(looter).IsRaider = true;

                return true;
            }

            public bool IsBlacklisted(string name)
            {
                return Options.BlacklistedPickupItems.Exists(value => !string.IsNullOrEmpty(value) && name.Contains(value, CompareOptions.OrdinalIgnoreCase));
            }

            private void FillAmmoTurret(AutoTurret turret)
            {
                if (isAuthorized || IsUnloading || turret.IsKilled())
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

                if (attachedWeapon.IsNull())
                {
                    turret.Invoke(() => FillAmmoTurret(turret), 0.2f);
                    return;
                }

                int p = Math.Max(config.Weapons.Ammo.AutoTurret, attachedWeapon.primaryMagazine.capacity);
                turret.inventory.AddItem(attachedWeapon.primaryMagazine.ammoType, p, 0uL);
                attachedWeapon.primaryMagazine.contents = attachedWeapon.primaryMagazine.capacity;
                attachedWeapon.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                turret.Invoke(turret.UpdateTotalAmmo, 0.25f);
            }

            private void FillAmmoGunTrap(GunTrap gt)
            {
                if (IsUnloading || isAuthorized || gt.IsKilled())
                {
                    return;
                }

                if (gt.ammoType == null)
                {
                    gt.ammoType = ItemManager.FindItemDefinition("ammo.handmade.shell");
                }

                var ammo = gt.inventory.GetSlot(0);

                if (ammo == null)
                {
                    gt.inventory.AddItem(gt.ammoType, config.Weapons.Ammo.GunTrap);
                }
                else ammo.amount = config.Weapons.Ammo.GunTrap;
            }

            private void FillAmmoFogMachine(FogMachine fm)
            {
                if (IsUnloading || isAuthorized || fm.IsKilled())
                {
                    return;
                }

                fm.inventory.AddItem(lowgradefuel, config.Weapons.Ammo.FogMachine);
            }

            private void FillAmmoFlameTurret(FlameTurret ft)
            {
                if (IsUnloading || isAuthorized || ft.IsKilled())
                {
                    return;
                }

                ft.inventory.AddItem(lowgradefuel, config.Weapons.Ammo.FlameTurret);
            }

            private void FillAmmoSamSite(SamSite ss)
            {
                if (IsUnloading || isAuthorized || ss.IsKilled())
                {
                    return;
                }

                if (ss.ammoItem == null || !ss.HasAmmo())
                {
                    Item item = ItemManager.Create(ss.ammoType, config.Weapons.Ammo.SamSite);

                    if (!item.MoveToContainer(ss.inventory))
                    {
                        item.Remove();
                    }
                    else ss.ammoItem = item;
                }
                else if (ss.ammoItem.amount < config.Weapons.Ammo.SamSite)
                {
                    ss.ammoItem.amount = config.Weapons.Ammo.SamSite;
                }
            }

            private void OnWeaponItemPreRemove(Item item)
            {
                if (isAuthorized || IsUnloading)
                {
                    return;
                }
                else if (!priv.IsKilled() && priv.authorizedPlayers.Exists(id => id.userid.IsSteamId()))
                {
                    isAuthorized = true;
                    return;
                }
                else if (privSpawned && priv.IsKilled())
                {
                    isAuthorized = true;
                    return;
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
                if (!IsInvokingCanFinish && !IsLoading)
                {
                    IsInvokingCanFinish = true;
                    InvokeRepeating(TryToEnd, 0f, 1f);
                }
            }

            public void TryToEnd()
            {
                if (IsOpened && !IsLoading && CanUndo())
                {
                    CancelInvoke(TryToEnd);
                    AwardRaiders();
                    Undo();
                }
            }

            private BasePlayer Record(BasePlayer attacker, BaseCombatEntity victim)
            {
                if (victim.IsReallyValid()) records[victim.net.ID] = new DamageRecord(attacker);
                attacker.lastDealtDamageTime = Time.time;
                victim.lastAttacker = attacker;

                return attacker;
            }

            public BasePlayer GetInitiatorPlayer(HitInfo hitInfo, BaseCombatEntity victim)
            {
                if (hitInfo.Initiator is BasePlayer)
                {
                    return Record(hitInfo.Initiator as BasePlayer, victim);
                }

                if (!hitInfo.damageTypes.Has(DamageType.Heat))
                {
                    return null;
                }

                foreach (var intruder in GetIntruders().Where(x => !(Time.time - x.lastDealtDamageTime > 1f || !IsUsingProjectile(x))))
                {
                    return Record(intruder, victim);
                }

                DamageRecord history;
                if (victim.IsReallyValid() && records.TryGetValue(victim.net.ID, out history) && history.player.IsReallyValid())
                {
                    history.player.lastDealtDamageTime = Time.time;
                    victim.lastAttacker = history.player;
                    return history.player;
                }

                if (victim.lastAttacker is BasePlayer)
                {
                    return Record(victim.lastAttacker as BasePlayer, victim);
                }

                return null;
            }

            private bool IsUsingProjectile(BasePlayer player)
            {
                if (player.IsNull() || player.svActiveItemID == 0)
                {
                    return false;
                }

                Item item = player.GetActiveItem();

                if (item == null)
                {
                    return false;
                }

                return item.info.shortname == "flamethrower" || item.GetHeldEntity() is BaseProjectile;
            }

            public void SetAllowPVP(RaidableType type, bool flag)
            {
                Type = type;

                if (type == RaidableType.Maintained && config.Settings.Maintained.Chance > 0)
                {
                    AllowPVP = Core.Random.Range(0, 101) <= config.Settings.Maintained.Chance;
                }
                else if (type == RaidableType.Scheduled && config.Settings.Schedule.Chance > 0)
                {
                    AllowPVP = Core.Random.Range(0, 101) <= config.Settings.Schedule.Chance;
                }
                else if (type == RaidableType.Maintained && config.Settings.Maintained.ConvertPVP)
                {
                    AllowPVP = false;
                }
                else if (type == RaidableType.Scheduled && config.Settings.Schedule.ConvertPVP)
                {
                    AllowPVP = false;
                }
                else if (type == RaidableType.Manual && config.Settings.Manual.ConvertPVP)
                {
                    AllowPVP = false;
                }
                else if (type == RaidableType.Purchased && config.Settings.Buyable.ConvertPVP)
                {
                    AllowPVP = false;
                }
                else if (type == RaidableType.Maintained && config.Settings.Maintained.ConvertPVE)
                {
                    AllowPVP = true;
                }
                else if (type == RaidableType.Scheduled && config.Settings.Schedule.ConvertPVE)
                {
                    AllowPVP = true;
                }
                else if (type == RaidableType.Manual && config.Settings.Manual.ConvertPVE)
                {
                    AllowPVP = true;
                }
                else if (type == RaidableType.Purchased && config.Settings.Buyable.ConvertPVE)
                {
                    AllowPVP = true;
                }
                else AllowPVP = flag;
            }

            public void AwardRaiders()
            {
                var sb = new StringBuilder();

                foreach (var ri in raiders.Values)
                {
                    if (ri.player.IsNull())
                    {
                        ri.reward = false;
                        continue;
                    }

                    if (ri.player.IsFlying)
                    {
                        if (config.EventMessages.Rewards.Flying) Message(ri.player, "No Reward: Flying");
                        ri.reward = false;
                        continue;
                    }

                    if (ri.player._limitedNetworking)
                    {
                        if (config.EventMessages.Rewards.Vanished) Message(ri.player, "No Reward: Vanished");
                        ri.reward = false;
                        continue;
                    }

                    if (!IsPlayerActive(ri.id))
                    {
                        if (config.EventMessages.Rewards.Inactive) Message(ri.player, "No Reward: Inactive");
                        ri.reward = false;
                        continue;
                    }

                    if (config.Settings.RemoveAdminRaiders && ri.player.IsAdmin && Type != RaidableType.None)
                    {
                        if (config.EventMessages.Rewards.RemoveAdmin) Message(ri.player, "No Reward: Admin");
                        ri.reward = false;
                        continue;
                    }

                    if (config.Settings.Management.OnlyAwardOwner && ri.player.userID != ownerId && ownerId.IsSteamId())
                    {
                        if (config.EventMessages.Rewards.NotOwner) Message(ri.player, "No Reward: Not Owner");
                        ri.reward = false;
                    }

                    if (!ri.IsParticipant)
                    {
                        if (config.EventMessages.Rewards.NotParticipant) Message(ri.player, "No Reward: Not A Participant");
                        ri.reward = false;
                        continue;
                    }

                    if (config.Settings.Management.OnlyAwardAllies && ri.player.userID != ownerId && !IsAlly(ri.userid, ownerId))
                    {
                        if (config.EventMessages.Rewards.NotAlly) Message(ri.player, "No Reward: Not Ally");
                        ri.reward = false;
                    }

                    sb.Append(ri.displayName).Append(", ");
                }

                Interface.CallHook("OnRaidableBaseCompleted", hookObjects);

                if (sb.Length == 0)
                {
                    return;
                }

                if (Options.Levels.Level2 && npcMaxAmount > 0)
                {
                    SpawnNpcs();
                }

                if (IsCompleted)
                {
                    HandleAwards();
                }

                sb.Length -= 2;
                string thieves = sb.ToString();
                string posStr = FormatGridReference(Location);

                Puts(mx("Thieves", null, mx($"Mode{Options.Mode}"), posStr, thieves));

                if (config.EventMessages.AnnounceThief)
                {
                    foreach (var target in BasePlayer.activePlayerList)
                    {
                        string mode = mx($"Mode{Options.Mode}", target.UserIDString);
                        QueueNotification(target, "Thieves", mode, posStr, thieves);
                    }
                }
            }

            private int GetRankedLadderPointsForDifficulty(string id)
            {
                if (!CanAssignTo(id, config.RankedLadder.Points.Owner))
                {
                    return 0;
                }

                switch (Options.Mode)
                {
                    case RaidableMode.Easy: return config.RankedLadder.Points.Easy;
                    case RaidableMode.Medium: return config.RankedLadder.Points.Medium;
                    case RaidableMode.Hard: return config.RankedLadder.Points.Hard;
                    case RaidableMode.Expert: return config.RankedLadder.Points.Expert;
                    default: return config.RankedLadder.Points.Nightmare;
                }
            }

            private void HandleAwards()
            {
                foreach (var ri in raiders.Values)
                {
                    TrySetLockout(ri);

                    if (!ri.IsParticipant || !ri.reward)
                    {
                        continue;
                    }

                    if (config.RankedLadder.Enabled)
                    {
                        PlayerInfo playerInfo = PlayerInfo.Get(ri.id);

                        playerInfo.ResetExpiredDate();

                        int points = GetRankedLadderPointsForDifficulty(ri.id);

                        playerInfo.TotalRaids++;
                        playerInfo.Raids++;
                        playerInfo.TotalPoints += points;
                        playerInfo.Points += points;

                        switch (Options.Mode)
                        {
                            case RaidableMode.Easy:
                                playerInfo.Easy++;
                                playerInfo.TotalEasy++;
                                playerInfo.EasyPoints += points;
                                playerInfo.TotalEasyPoints += points;

                                if (config.RankedLadder.Assign.Easy > 0 && playerInfo.Easy >= config.RankedLadder.Assign.Easy && CanAssignTo(ri.id, config.RankedLadder.Assign.Owner))
                                {
                                    AddGroupedPermission(ri.id, "raideasy", "raidablebases.ladder.easy");
                                }
                                break;
                            case RaidableMode.Medium:
                                playerInfo.Medium++;
                                playerInfo.TotalMedium++;
                                playerInfo.MediumPoints += points;
                                playerInfo.TotalMediumPoints += points;

                                if (config.RankedLadder.Assign.Medium > 0 && playerInfo.Medium >= config.RankedLadder.Assign.Medium && CanAssignTo(ri.id, config.RankedLadder.Assign.Owner))
                                {
                                    AddGroupedPermission(ri.id, "raidmedium", "raidablebases.ladder.medium");
                                }
                                break;
                            case RaidableMode.Hard:
                                playerInfo.Hard++;
                                playerInfo.TotalHard++;
                                playerInfo.HardPoints += points;
                                playerInfo.TotalHardPoints += points;

                                if (config.RankedLadder.Assign.Hard > 0 && playerInfo.Hard >= config.RankedLadder.Assign.Hard && CanAssignTo(ri.id, config.RankedLadder.Assign.Owner))
                                {
                                    AddGroupedPermission(ri.id, "raidhard", "raidablebases.ladder.hard");
                                }
                                break;
                            case RaidableMode.Expert:
                                playerInfo.Expert++;
                                playerInfo.TotalExpert++;
                                playerInfo.ExpertPoints += points;
                                playerInfo.TotalExpertPoints += points;

                                if (config.RankedLadder.Assign.Expert > 0 && playerInfo.Expert >= config.RankedLadder.Assign.Expert && CanAssignTo(ri.id, config.RankedLadder.Assign.Owner))
                                {
                                    AddGroupedPermission(ri.id, "raidexpert", "raidablebases.ladder.expert");
                                }
                                break;
                            case RaidableMode.Nightmare:
                                playerInfo.Nightmare++;
                                playerInfo.TotalNightmare++;
                                playerInfo.NightmarePoints += points;
                                playerInfo.TotalNightmarePoints += points;

                                if (config.RankedLadder.Assign.Nightmare > 0 && playerInfo.Nightmare >= config.RankedLadder.Assign.Nightmare && CanAssignTo(ri.id, config.RankedLadder.Assign.Owner))
                                {
                                    AddGroupedPermission(ri.id, "raidnightmare", "raidablebases.ladder.nightmare");
                                }
                                break;
                        }
                    }

                    if (Options.Rewards.NoBuyableRewards && payments.Count > 0)
                    {
                        return;
                    }

                    int total = raiders.Values.ToList().Sum(x => x.IsParticipant ? 1 : 0);

                    if (Options.Rewards.Custom.IsReallyValid() && !ri.player.IsNull())
                    {
                        int amount = config.Settings.Management.DivideRewards ? Options.Rewards.Custom.Amount / total : Options.Rewards.Custom.Amount;
                        Item item = ItemManager.Create(Options.Rewards.Custom.Definition, amount, Options.Rewards.Custom.Skin);
                        if (item.skin != 0 && item.GetHeldEntity()) item.GetHeldEntity().skinID = item.skin;
                        if (!string.IsNullOrEmpty(Options.Rewards.Custom.Name)) item.name = Options.Rewards.Custom.Name;
                        if (!ri.player.inventory.GiveItem(item)) item.DropAndTossUpwards(ri.player.eyes.position);
                        QueueNotification(ri.player, "CustomDeposit", string.Format("{0} {1}", item.info.displayName.english, amount));
                    }

                    if (Options.Rewards.Money > 0 && Instance.Economics.CanCall())
                    {
                        double money = config.Settings.Management.DivideRewards ? Options.Rewards.Money / total : Options.Rewards.Money;
                        if (Convert.ToBoolean(Instance.Economics?.Call("Deposit", ri.id, money)))
                        {
                            QueueNotification(ri.player, "EconomicsDeposit", money);
                        }
                    }

                    if (Options.Rewards.Money > 0 && Instance.IQEconomic.CanCall())
                    {
                        int money = Convert.ToInt32(config.Settings.Management.DivideRewards ? Options.Rewards.Money / total : Options.Rewards.Money);
                        Instance.IQEconomic?.Call("API_SET_BALANCE", ri.userid, money);
                        QueueNotification(ri.player, "EconomicsDeposit", money);
                    }

                    if (Options.Rewards.Points > 0 && Instance.ServerRewards.CanCall())
                    {
                        int points = config.Settings.Management.DivideRewards ? Options.Rewards.Points / total : Options.Rewards.Points;
                        Instance.ServerRewards?.Call("AddPoints", ri.userid, points);
                        QueueNotification(ri.player, "ServerRewardPoints", points);
                    }

                    if (Options.Rewards.XP > 0 && Instance.SkillTree.CanCall())
                    {
                        double xp = config.Settings.Management.DivideRewards ? Options.Rewards.XP / total : Options.Rewards.XP;
                        QueueNotification(ri.player, "SkillTreeXP", xp);
                        Instance.SkillTree?.Call("AwardXP", ri.player, xp);
                    }
                }
            }

            private void AddGroupedPermission(string userid, string group, string perm)
            {
                if (userid.HasPermission("raidablebases.notitle"))
                {
                    return;
                }

                if (!userid.HasPermission(perm))
                {
                    Instance.permission.GrantUserPermission(userid, perm, Instance);
                }

                if (!Instance.permission.UserHasGroup(userid, group))
                {
                    Instance.permission.AddUserGroup(userid, group);
                }
            }

            private bool CanAssignTo(string id, bool value)
            {
                return value == false || ownerId == 0uL || id == ownerId.ToString();
            }

            private List<string> messagesSent = new List<string>();

            public bool TryMessage(BasePlayer player, string key, params object[] args)
            {
                if (player.IsNull() || messagesSent.Contains(player.UserIDString))
                {
                    return false;
                }

                string userid = player.UserIDString;

                messagesSent.Add(userid);
                Instance.timer.Once(10f, () => messagesSent.Remove(userid));
                Message(player, key, args);

                return true;
            }

            public bool CanBypass(BasePlayer player)
            {
                return !player.IsHuman() || player.IsFlying || player.limitNetworking || player.HasPermission("raidablebases.canbypass");
            }

            private bool Exceeds(BasePlayer player)
            {
                if (CanBypass(player))
                {
                    return false;
                }

                int amount = config.Settings.Management.Players.Get(Options.Mode, Type);

                if (amount == 0)
                {
                    return false;
                }

                return amount == -1 || intruders.Count >= amount;
            }

            public bool HasLockout(BasePlayer player)
            {
                if (!config.Settings.Management.Lockout.Any() || player.IsNull() || CanBypass(player) || player.HasPermission("raidablebases.lockoutbypass") || Type == RaidableType.None)
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

                if (config.Settings.Buyable.AllowAlly && IsAlly(ownerId, player.userID))
                {
                    return false;
                }

                Lockout lo;
                if (data.Lockouts.TryGetValue(player.UserIDString, out lo))
                {
                    double time;

                    if (config.Settings.Management.Lockout.Global)
                    {
                        time = Math.Max(Math.Max(lo.Easy, lo.Medium), Math.Max(Math.Max(lo.Hard, lo.Expert), lo.Nightmare));
                    }
                    else time = GetLockoutTime(Options.Mode, lo, player.UserIDString);

                    if (time > 0f)
                    {
                        var difficultyMode = mx($"Mode{Options.Mode}", player.UserIDString);
                        QueueNotification(player, "LockedOut", difficultyMode, FormatTime(time, player.UserIDString));
                        return true;
                    }
                }

                if (UI.Lockouts.Contains(player))
                {
                    UI.DestroyLockoutUI(player);
                }

                return false;
            }

            private void TrySetGlobalLockout(string playerId, BasePlayer player)
            {
                Lockout lo;
                if (!data.Lockouts.TryGetValue(playerId, out lo))
                {
                    data.Lockouts[playerId] = lo = new Lockout();
                }

                if (lo.Easy <= 0) lo.Easy = GetLockoutTime(RaidableMode.Easy);
                if (lo.Medium <= 0) lo.Medium = GetLockoutTime(RaidableMode.Medium);
                if (lo.Hard <= 0) lo.Hard = GetLockoutTime(RaidableMode.Hard);
                if (lo.Expert <= 0) lo.Expert = GetLockoutTime(RaidableMode.Expert);
                if (lo.Nightmare <= 0) lo.Nightmare = GetLockoutTime(RaidableMode.Nightmare);

                if (lo.Any())
                {
                    UI.UpdateLockoutUI(player);
                }
                else data.Lockouts.Remove(playerId);
            }

            private void TrySetLockout(Raider ri)
            {
                if (IsUnloading || IsResetting || ri == null || !ri.IsParticipant || Type == RaidableType.None || ri.id.HasPermission("raidablebases.canbypass") || ri.id.HasPermission("raidablebases.lockoutbypass"))
                {
                    return;
                }

                if (ri.player.IsReallyValid() && (ri.player.IsFlying || ri.player._limitedNetworking))
                {
                    return;
                }

                if (config.Settings.Management.Lockout.Global)
                {
                    TrySetGlobalLockout(ri.id, ri.player);
                    return;
                }

                double time = GetLockoutTime(Options.Mode);

                if (time <= 0)
                {
                    return;
                }

                Lockout lo;
                if (!data.Lockouts.TryGetValue(ri.id, out lo))
                {
                    data.Lockouts[ri.id] = lo = new Lockout();
                }

                switch (Options.Mode)
                {
                    case RaidableMode.Easy: if (lo.Easy <= 0) lo.Easy = time; break;
                    case RaidableMode.Medium: if (lo.Medium <= 0) lo.Medium = time; break;
                    case RaidableMode.Hard: if (lo.Hard <= 0) lo.Hard = time; break;
                    case RaidableMode.Expert: if (lo.Expert <= 0) lo.Expert = time; break;
                    case RaidableMode.Nightmare: if (lo.Nightmare <= 0) lo.Nightmare = time; break;
                }

                if (lo.Any())
                {
                    UI.UpdateLockoutUI(ri.player);
                }
                else data.Lockouts.Remove(ri.id);
            }

            public static void SetTestLockout(BasePlayer player)
            {
                Lockout lo;
                if (!data.Lockouts.TryGetValue(player.UserIDString, out lo))
                {
                    data.Lockouts[player.UserIDString] = lo = new Lockout();
                }

                lo.Easy = 180;
                lo.Medium = 180;
                lo.Hard = 180;
                lo.Expert = 180;
                lo.Nightmare = 180;

                UI.UpdateLockoutUI(player);
            }

            private double GetLockoutTime(RaidableMode mode)
            {
                switch (mode)
                {
                    case RaidableMode.Easy: return config.Settings.Management.Lockout.Easy * 60;
                    case RaidableMode.Medium: return config.Settings.Management.Lockout.Medium * 60;
                    case RaidableMode.Hard: return config.Settings.Management.Lockout.Hard * 60;
                    case RaidableMode.Expert: return config.Settings.Management.Lockout.Expert * 60;
                    default: return config.Settings.Management.Lockout.Nightmare * 60;
                }
            }

            public static double GetLockoutTime(RaidableMode mode, Lockout lo, string playerId)
            {
                switch (mode)
                {
                    case RaidableMode.Easy: return lo.Easy;
                    case RaidableMode.Medium: return lo.Medium;
                    case RaidableMode.Hard: return lo.Hard;
                    case RaidableMode.Expert: return lo.Expert;
                    case RaidableMode.Nightmare: return lo.Nightmare;
                    default: return 0d;
                }
            }

            public string Mode(string userid = null, bool forceShowName = false)
            {
                var difficultyMode = mx($"Mode{Options.Mode}", userid);

                if (owner.IsReallyValid())
                {
                    return string.Format("{0} {1}",
                        (config.Settings.Markers.ShowOwnersName || forceShowName) ?
                        owner.displayName :
                        mx("Claimed"), difficultyMode.SentenceCase());
                }

                return difficultyMode.SentenceCase();
            }

            public void TrySetPayLock(List<Payment> payments, bool forced = false)
            {
                if (!IsOpened || payments.IsNullOrEmpty() || !payments.All(payment => Payment.IsValid(payment)))
                {
                    ResetPayLock(true);
                    return;
                }

                foreach (var payment in payments)
                {
                    if (payment.money > 0)
                    {
                        payment.TakeMoney();
                    }

                    if (payment.RP > 0)
                    {
                        payment.TakePoints();
                    }
                }

                if (config.Settings.Buyable.UsePayLock || forced)
                {
                    this.payments = payments;
                    IsPayLocked = true;
                    SetOwnerInternal(payments.FirstOrDefault().owner);
                    ClearEnemies();
                    Interface.CallHook("OnRaidableBasePurchased", new object[] { owner.displayName, Location, PhoneController.PositionToGridCoord(Location), (int)Options.Mode, AllowPVP, spawnTime, loadTime, BaseName });
                }
            }

            private void SetOwnerInternal(BasePlayer player)
            {
                UpdateStatus(player);
                riOwner = GetRaider(player);
                owner = player;
                ownerId = player.userID;
                ownerName = player.displayName;
                permOwnerId = ownerId;
                riOwner.IsAlly = true;
                riOwner.IsAllowed = true;
                UpdateMarker();
            }


            private void StartPurchaseCooldown()
            {
                if (Instance.IsNull() || !permOwnerId.IsSteamId())
                {
                    return;
                }

                float cooldown = config.Settings.Buyable.Cooldowns.Get(permOwnerId);

                if (cooldown <= 0)
                {
                    return;
                }

                Instance.BuyCooldowns[permOwnerId] = new BuyableInfo
                {
                    Time = Time.realtimeSinceStartup + cooldown,
                    Timer = Instance.timer.Once(cooldown, () => Instance.BuyCooldowns.Remove(permOwnerId))
                };
            }

            public void Refund(BasePlayer player)
            {
                if (config.Settings.Buyable.Refunds.Percentage == 0 || !config.Settings.Buyable.Refunds.Refund || IsDamaged && config.Settings.Buyable.Refunds.Damaged)
                {
                    return;
                }

                IsResetting = config.Settings.Buyable.Refunds.Reset;
                Reset(player);

                foreach (var payment in payments)
                {
                    if (!payment.Options.IsNullOrEmpty())
                    {
                        foreach (var option in payment.Options)
                        {
                            var def = ItemManager.FindItemDefinition(option.Shortname);

                            if (def == null)
                            {
                                return;
                            }

                            Item item = ItemManager.Create(def, option.Amount);

                            if (!string.IsNullOrEmpty(option.Name))
                            {
                                item.name = option.Name;
                            }

                            if (!player.inventory.GiveItem(item))
                            {
                                item.DropAndTossUpwards(player.eyes.position);
                            }
                        }
                    }
                    else if (payment.RP > 0)
                    {
                        int points = (int)(payment.RP * config.Settings.Buyable.Refunds.Percentage / 100.0);

                        Instance.ServerRewards?.Call("AddPoints", player.userID, points);
                    }
                    else if (payment.money > 0)
                    {
                        double money = payment.money * config.Settings.Buyable.Refunds.Percentage / 100.0;

                        Instance.Economics?.Call("Deposit", player.userID, money);
                        Instance.IQEconomic?.Call("API_SET_BALANCE", player.userID, (int)money);
                    }
                }
            }

            private void Reset(BasePlayer player)
            {
                if (!config.Settings.Buyable.Refunds.Reset)
                {
                    return;
                }

                BuyableInfo bi;
                if (Instance.BuyCooldowns.TryGetValue(player.userID, out bi))
                {
                    bi.Timer.Destroy();
                    Instance.BuyCooldowns.Remove(player.userID);
                }
            }

            private bool IsPlayerActive(string playerId)
            {
                if (config.Settings.Management.LockTime <= 0f)
                {
                    return true;
                }

                float time;
                if (!lastActive.TryGetValue(playerId, out time))
                {
                    return true;
                }

                float secondsInactive = Time.realtimeSinceStartup - time;
                float secondsBeforeInactive = config.Settings.Management.LockTime * 60f;

                return secondsInactive < secondsBeforeInactive;
            }

            public void TrySetOwner(BasePlayer attacker, BaseEntity entity, HitInfo hitInfo)
            {
                UpdateStatus(attacker);

                if (!config.Settings.Management.UseOwners || !IsOpened || ownerId.IsSteamId() || IsOwner(attacker)) // || !InRange(attacker.transform.position, Location, 250f))
                {
                    return;
                }

                if (config.Settings.Management.BypassUseOwnersForPVP && AllowPVP || config.Settings.Management.BypassUseOwnersForPVE && !AllowPVP)
                {
                    return;
                }

                if (HasLockout(attacker) || IsHogging(attacker))
                {
                    NullifyDamage(hitInfo);
                    return;
                }

                if (entity is ScientistNPC)
                {
                    SetOwner(attacker);
                    return;
                }

                if (!(entity is BuildingBlock) && !(entity is Door) && !(entity is SimpleBuildingBlock))
                {
                    return;
                }

                if (InRange2D(attacker.transform.position, Location, ProtectionRadius) || IsLootingWeapon(hitInfo))
                {
                    SetOwner(attacker);
                }
            }

            private void SetOwner(BasePlayer player)
            {
                TryInvokeResetOwner();
                SetOwnerInternal(player);
                ResetRaiderRelations();
                Protector();
            }

            public void ResetRaiderRelations()
            {
                foreach (var ri in raiders.Values.ToList())
                {
                    if (ri.player == owner)
                    {
                        continue;
                    }

                    ri.IsAllowed = false;
                    ri.IsAlly = false;
                }
            }

            public void ClearEnemies()
            {
                raiders.RemoveAll((uid, ri) => !ri.player.IsReallyConnected() || !IsAlly(ri.player));
            }

            public void CheckDespawn()
            {
                if (IsDespawning || !IsOpened || config.Settings.Management.DespawnMinutesInactive <= 0)
                {
                    return;
                }

                if (config.Settings.Management.Engaged && !IsEngaged)
                {
                    return;
                }

                if (IsInvoking(Despawn))
                {
                    CancelInvoke(Despawn);
                }

                if (!config.Settings.Management.DespawnMinutesInactiveReset && despawnTime != 0)
                {
                    return;
                }

                float time = config.Settings.Management.DespawnMinutesInactive * 60f;
                despawnTime = Time.time + time;
                Invoke(Despawn, time);
            }

            public bool EndWhenCupboardIsDestroyed()
            {
                if (config.Settings.Management.EndWhenCupboardIsDestroyed && privSpawned && priv.IsKilled())
                {
                    return IsCompleted = true;
                }

                return false;
            }

            public bool CanUndo()
            {
                if (EndWhenCupboardIsDestroyed())
                {
                    return true;
                }

                if (config.Settings.Management.RequireCupboardLooted && privSpawned && (ownerId == 0 || IsPlayerActive(ownerId.ToString())))
                {
                    if (!priv.IsKilled() && !priv.inventory.IsEmpty())
                    {
                        return false;
                    }
                }

                foreach (var container in _containers)
                {
                    if (!container.IsKilled() && !container.inventory.IsEmpty())
                    {
                        return false;
                    }
                }

                foreach (string value in config.Settings.Management.Inherit)
                {
                    foreach (var container in _allcontainers)
                    {
                        if (container.IsKilled() || !container.ShortPrefabName.Contains(value, CompareOptions.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!container.inventory.IsEmpty())
                        {
                            return false;
                        }
                    }
                }

                return IsCompleted = true;
            }

            private bool CanPlayerBeLooted(ulong looter, ulong target)
            {
                if (!config.Settings.Management.PlayersLootableInPVE && !AllowPVP || !config.Settings.Management.PlayersLootableInPVP && AllowPVP)
                {
                    return IsAlly(looter, target);
                }

                return true;
            }

            private bool CanBeLooted(BasePlayer player, BaseEntity e)
            {
                if (IsProtectedWeapon(e, true))
                {
                    return config.Settings.Management.LootableTraps;
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

                    return CanPlayerBeLooted(player.userID, corpse.playerSteamID);
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

                    return CanPlayerBeLooted(player.userID, container.playerSteamID);
                }

                return true;
            }

            public bool IsProtectedWeapon(BaseEntity e, bool checkBuiltList = false)
            {
                if (!e.IsReallyValid() || checkBuiltList && BuiltList.ContainsKey(e))
                {
                    return false;
                }

                return e is GunTrap || e is FlameTurret || e is FogMachine || e is SamSite || e is AutoTurret;
            }

            public void OnLootEntityInternal(BasePlayer player, BaseEntity entity)
            {
                if (!entity.OwnerID.IsSteamId() && !RaidableBase.Has(entity))
                {
                    return;
                }

                UpdateStatus(player);

                if (entity.ShortPrefabName == "coffinstorage" && Mathf.Approximately(Vector3.Distance(entity.transform.position, new Vector3(0f, -50f, 0f)), 0f))
                {
                    return;
                }

                if (entity.OwnerID == player.userID || entity is BaseMountable)
                {
                    return;
                }

                if (IsBlacklisted(entity.ShortPrefabName))
                {
                    player.Invoke(player.EndLooting, 0.01f);
                    return;
                }

                if (entity.HasParent() && entity.GetParentEntity() is BaseMountable)
                {
                    return;
                }

                if (!CanBeLooted(player, entity))
                {
                    if (!player.HasPermission("raidablebases.admin.loot") || !player.IsAdmin)
                    {
                        player.Invoke(player.EndLooting, 0.01f);
                        return;
                    }
                }

                if (entity is LootableCorpse || entity is DroppedItemContainer)
                {
                    return;
                }

                if (player.GetMounted())
                {
                    Message(player, "CannotBeMounted");
                    player.Invoke(player.EndLooting, 0.01f);
                    return;
                }

                if (Options.RequiresCupboardAccess && !player.CanBuild())
                {
                    Message(player, "MustBeAuthorized");
                    player.Invoke(player.EndLooting, 0.01f);
                    return;
                }

                if (!IsAlly(player))
                {
                    Message(player, "OwnerLocked");
                    player.Invoke(player.EndLooting, 0.01f);
                    return;
                }

                if (Type != RaidableType.None && raiders.Exists(ri => ri.Value.IsParticipant))
                {
                    CheckDespawn();
                }

                AddLooter(player);

                if (IsBox(entity, true) || entity is BuildingPrivlidge)
                {
                    StartTryToEnd();
                }
            }

            private void SetNoDrops()
            {
                foreach (var container in _allcontainers)
                {
                    if (container.IsKilled())
                    {
                        continue;
                    }
                    else if (Options.DropPrivilegeLoot && container is BuildingPrivlidge)
                    {
                        DropOrRemoveItems(container, IsProtectedWeapon(container));
                    }
                    else
                    {
                        container.dropsLoot = false;
                        container.inventory.Clear();
                    }
                }

                ItemManager.DoRemoves();
            }

            public void DestroyInputs()
            {
                raiders.Values.ToList().ForEach(ri => ri.DestroyInput());
            }

            public void Init(RandomBase x, List<BaseEntity> entities, Dictionary<Elevator, BMGELEVATOR> elevators = null)
            {
                float radius = x.Profile.Options.ProtectionRadius(x.Type);
                float distance;
                if (x.spawns == null)
                {
                    distance = radius;
                }
                else distance = x.spawns.RemoveNear(x.Position, radius, CacheType.Generic, x.Type);

                Instance.Cycle.Add(x.Type, x.Profile.Options.Mode, x.BaseName);

                if (x.Type == RaidableType.Purchased)
                {
                    TrySetPayLock(x.payments);
                }

                IsLoading = true;
                spawns = x.spawns;
                Elevators = elevators;
                BaseIndex = x.BaseIndex;
                RemoveNearDistance = distance;

                TryInvokeMethod(() => AddEntities(entities));

                Interface.Oxide.NextTick(() =>
                {
                    TryInvokeMethod(SetCenterFromMultiplePoints);
                    TryInvokeMethod(SetupElevators);
                    SetupCollider();

                    setupRoutine = ServerMgr.Instance.StartCoroutine(EntitySetup());
                });
            }

            private void SetupElevators()
            {
                if (Elevators.IsNullOrEmpty())
                {
                    return;
                }

                Elevators.ToList().ForEach(element => element.Value.Init(this));
            }

            private void AddEntities(List<BaseEntity> entities)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    var e = entities[i];
                    if (e.IsKilled())
                        continue;
                    if (e.ShortPrefabName == "foundation.triangle" || e.ShortPrefabName == "foundation")
                        foundations.Add(e.transform.position);
                    AddEntity(e);
                }
            }

            private void SetCenterFromMultiplePoints()
            {
                if (foundations.Count < 2)
                {
                    Location = PastedLocation;
                    return;
                }

                float x = 0f;
                float z = 0f;

                foreach (var position in foundations)
                {
                    z += position.z;
                    x += position.x;
                }

                var vector = new Vector3(x / foundations.Count, 0f, z / foundations.Count);

                if (Options.Setup.ForcedHeight != -1)
                {
                    vector.y = Options.Setup.ForcedHeight;
                }
                else vector.y = SpawnsController.GetSpawnHeight(vector);

                Location = vector;
            }

            private void CreateSpheres()
            {
                if (Options.SphereAmount <= 0 || Options.Silent)
                {
                    return;
                }

                for (int i = 0; i < Options.SphereAmount; i++)
                {
                    var sphere = GameManager.server.CreateEntity(StringPool.Get(3211242734), Location) as SphereEntity;

                    if (sphere.IsNull())
                    {
                        break;
                    }

                    sphere.currentRadius = 1f;
                    sphere.Spawn();
                    sphere.LerpRadiusTo(ProtectionRadius * 2f, ProtectionRadius * 0.75f);
                    spheres.Add(sphere);
                }
            }

            private void CreateZoneWalls()
            {
                if (!Options.ArenaWalls.Enabled)
                {
                    return;
                }

                int stacks = 0;
                float maxDistance = 48f;

                if (Options.Setup.ForcedHeight > 0)
                {
                    maxDistance += Options.Setup.ForcedHeight;
                    Options.ArenaWalls.Stone = true;
                    Options.ArenaWalls.Ice = false;
                    stacks = Mathf.CeilToInt(Options.Setup.ForcedHeight / 6f);
                }

                var center = new Vector3(Location.x, Location.y, Location.z);
                string prefab = Options.ArenaWalls.Ice ? StringPool.Get(921229511) : Options.ArenaWalls.Stone ? StringPool.Get(1585379529) : StringPool.Get(1745077396);
                float maxHeight = -999f;
                float minHeight = 999f;
                int next1 = Mathf.CeilToInt(360 / Options.ArenaWalls.Radius * 0.1375f);

                foreach (var position in SpawnsController.GetCircumferencePositions(center, Options.ArenaWalls.Radius, next1, false, 1f))
                {
                    float y = SpawnsController.GetSpawnHeight(position, false, true);
                    maxHeight = Mathf.Max(y, maxHeight, TerrainMeta.WaterMap.GetHeight(position));
                    minHeight = Mathf.Min(y, minHeight);
                    center.y = minHeight;
                }

                float gap = Options.ArenaWalls.Stone || Options.ArenaWalls.Ice ? 0.3f : 0.5f;
                stacks += Mathf.CeilToInt((maxHeight - minHeight) / 6f) + Options.ArenaWalls.Stacks;
                float next2 = 360 / Options.ArenaWalls.Radius - gap;
                float j = Options.ArenaWalls.Stacks * 6f + 6f;

                for (int i = 0; i < stacks; i++)
                {
                    if (Options.Setup.ForcedHeight != -1f && i < stacks * 0.75)
                    {
                        center.y += 6f;
                        continue;
                    }

                    foreach (var position in SpawnsController.GetCircumferencePositions(center, Options.ArenaWalls.Radius, next2, false, center.y))
                    {
                        if (Mathf.Abs(Location.y - position.y) > maxDistance)
                        {
                            continue;
                        }

                        var groundHeight = TerrainMeta.HeightMap.GetHeight(new Vector3(position.x, position.y + 6f, position.z));

                        if (i == 0 && position.y + 8.018669f < groundHeight)
                        {
                            continue;
                        }

                        if (groundHeight > position.y + 6.5f)
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

                        var e = GameManager.server.CreateEntity(prefab, position, default(Quaternion), false);

                        if (e.IsNull())
                        {
                            return;
                        }

                        e.OwnerID = 0;
                        e.transform.LookAt(center, Vector3.up);

                        if (Options.ArenaWalls.UseUFOWalls)
                        {
                            e.transform.Rotate(-67.5f, 0f, 0f);
                        }

                        e.Spawn();
                        e.gameObject.SetActive(true);

                        if (CanSetupEntity(e))
                        {
                            SetupEntity(e);
                            data.TemporaryUID.Add(e.net.ID);
                        }

                        if (stacks == i - 1)
                        {
                            RaycastHit hit;
                            if (Physics.Raycast(new Vector3(position.x, position.y + 6.5f, position.z), Vector3.down, out hit, 13f, targetMask))
                            {
                                if (hit.collider.ObjectName().Contains("rock") || hit.collider.ObjectName().Contains("formation", CompareOptions.OrdinalIgnoreCase))
                                {
                                    stacks++;
                                }
                            }
                        }
                    }

                    center.y += 6f;
                }
            }

            private void KillClutter()
            {
                var entities = FindEntitiesOfType<BaseEntity>(Location, ProtectionRadius);
                for (int i = 0; i < entities.Count; i++)
                {
                    var e = entities[i];
                    if (e is TreeEntity) // && NearFoundation(e.transform.position, 10f))
                    {
                        e.SafelyKill();
                    }
                    else if (e is SleepingBag)
                    {
                        SleepingBagHandler(e as SleepingBag);
                    }
                }
            }

            private void SleepingBagHandler(SleepingBag bag)
            {
                if (Entities.Contains(bag))
                {
                    return;
                }

                if (spawns?.IsCustomSpawn == true && Options.CustomSpawns.KillSleepingBags)
                {
                    bag.SafelyKill();
                }
                else
                {
                    _bags[bag] = bag.deployerUserID;

                    bag.deployerUserID = 0uL;
                    bag.unlockTime = UnityEngine.Time.realtimeSinceStartup + 99999f;
                }
            }

            private void ResetSleepingBags()
            {
                foreach (var element in _bags)
                {
                    if (element.Key.IsKilled()) continue;
                    element.Key.deployerUserID = element.Value;
                    element.Key.unlockTime = UnityEngine.Time.realtimeSinceStartup;
                }
            }

            private IEnumerator EntitySetup()
            {
                TryInvokeMethod(KillClutter);

                int checks = 0;
                float invokeTime = 0f;
                int limit = Mathf.Clamp(Options.Setup.SpawnLimit, 1, 500);

                foreach (var e in Entities.ToList())
                {
                    if (Instance.IsNull())
                    {
                        yield break;
                    }

                    TryInvokeMethod(() => TrySetupEntity(e, ref invokeTime));

                    if (++checks >= limit)
                    {
                        checks = 0;
                        yield return CoroutineEx.waitForFixedUpdate;
                    }
                }

                yield return CoroutineEx.waitForSeconds(2f);

                TryInvokeMethod(SetupLoot);
                TryInvokeMethod(Subscribe);
                TryInvokeMethod(CreateGenericMarker);
                TryInvokeMethod(UpdateMarker);
                TryInvokeMethod(EjectSleepers);
                TryInvokeMethod(CreateZoneWalls);
                TryInvokeMethod(CreateSpheres);
                TryInvokeMethod(SetupLights);
                TryInvokeMethod(SetupDoorControllers);
                TryInvokeMethod(SetupDoors);
                TryInvokeMethod(CheckDespawn);
                TryInvokeMethod(SetupContainers);
                TryInvokeMethod(MakeAnnouncements);
                TryInvokeMethod(SetupRugs);
                TryInvokeMethod(SetLoadTime);
                InvokeRepeating(Protector, 1f, 1f);

                IsLoading = false;
                IsSpawning = false;
                setupRoutine = null;
                Interface.CallHook("OnRaidableBaseStarted", hookObjects);
            }

            public static void TryInvokeMethod(Action action)
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Puts("{0} ERROR: {1}", action.Method.Name, ex);
                }
            }

            private void TrySetupEntity(BaseEntity e, ref float invokeTime)
            {
                if (!CanSetupEntity(e))
                {
                    return;
                }

                SetupEntity(e);

                var position = e.transform.position;

                NetworkID = Math.Min(NetworkID, e.net.ID);
                e.OwnerID = 0;

                if (e.skinID == 1337424001 && e is CollectibleEntity)
                {
                    (e as CollectibleEntity).itemList = null; // WaterBases compatibility
                }

                if (!Options.AllowPickup && e is BaseCombatEntity)
                {
                    SetupPickup(e as BaseCombatEntity);
                }

                if (e is DecorDeployable && !_decorDeployables.Contains(e))
                {
                    _decorDeployables.Add(e);
                }

                if (e is IOEntity)
                {
                    if (e is ContainerIOEntity)
                    {
                        SetupIO(e as ContainerIOEntity);
                    }

                    if (e is AutoTurret)
                    {
                        var turret = e as AutoTurret;

                        triggers[turret.targetTrigger] = turret;

                        Instance.timer.Once(invokeTime += 0.1f, () => SetupTurret(turret));
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
                    else if (e.name.Contains("light"))
                    {
                        SetupLight(e as IOEntity);
                    }
                    else if (e is CustomDoorManipulator)
                    {
                        doorControllers.Add(e as CustomDoorManipulator);
                    }
                    else if (e is HBHFSensor)
                    {
                        SetupHBHFSensor(e as HBHFSensor);
                    }
                    else if (e.ShortPrefabName == "generator.small" || e.ShortPrefabName == "generator.static")
                    {
                        SetupGenerator(e as ElectricGenerator);
                    }
                    else if (e is PressButton)
                    {
                        SetupButton(e as PressButton);
                    }
                }
                else if (e is StorageContainer)
                {
                    SetupContainer(e as StorageContainer);

                    if (e is BaseOven)
                    {
                        ovens.Add(e as BaseOven);
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
                    else if (e is Locker)
                    {
                        SetupLocker(e as Locker);
                    }
                    else if (e is GunTrap)
                    {
                        SetupGunTrap(e as GunTrap);
                    }
                }
                else if (e is BuildingBlock)
                {
                    SetupBuildingBlock(e as BuildingBlock);
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
                    SetupMountable(e as BaseMountable);
                }
                else if (e is CollectibleEntity)
                {
                    SetupCollectible(e as CollectibleEntity);
                }

                if (e is DecayEntity)
                {
                    SetupDecayEntity(e as DecayEntity);
                }

                if (e is Door)
                {
                    doors.Add(e as Door);
                }
                else SetupSkin(e);
            }

            private void SetLoadTime()
            {
                float time;
                if (Instance.LoadingTimes.TryGetValue(PastedLocation, out time))
                {
                    loadTime = Time.time - time;
                    Instance.LoadingTimes.Remove(PastedLocation);
                }
            }

            private void SetupLights()
            {
                if (Instance.NightLantern.CanCall())
                {
                    return;
                }

                if (config.Settings.Management.Lights || config.Settings.Management.AlwaysLights)
                {
                    ToggleLights(TOD_Sky.Instance?.IsNight == true);
                }
            }

            private void SetupCollider()
            {
                go.transform.position = Location;

                InvokeRepeating(CheckEntitiesInSphere, 0f, 0.1f);
            }

            private void SetupLoot()
            {
                _containers.RemoveWhere(x => x.IsKilled());

                treasureAmount = Options.MinTreasure > 0 ? UnityEngine.Random.Range(Options.MinTreasure, Options.MaxTreasure + 1) : Options.MaxTreasure;

                if (Options.SkipTreasureLoot || treasureAmount <= 0)
                {
                    return;
                }

                var containers = new List<StorageContainer>();

                if (!SetupLootContainers(containers))
                {
                    return;
                }

                SetTreasureLootLimit(containers);
                TakeLootFromBaseLoot();
                TakeLootFromDifficultyLoot();
                TakeLootFromDefaultLoot();
                PopulateLoot(true);
                TryAddDuplicates();
                PopulateLoot(false);

                if (Loot.Count == 0)
                {
                    Puts(mx("NoConfiguredLoot"));
                    return;
                }

                TryRemoveDuplicates();
                VerifyLootAmount();
                SpawnLoot(containers);
                SetupSellOrders();
            }
            
            private void SetupContainers()
            {
                foreach (var container in _containers)
                {
                    container.inventory.onItemAddedRemoved += new Action<Item, bool>(OnItemAddedRemoved);
                    if (container.ShortPrefabName != "box.wooden.large") continue;
                    container.SendNetworkUpdate();
                }
            }

            public void SetupEntity(BaseEntity e, bool skipCheck = true)
            {
                if (skipCheck)
                {
                    AddEntity(e);
                }

                MyInstance.RaidEntities[e] = this;
            }

            private void SetupPickup(BaseCombatEntity e)
            {
                e.pickup.enabled = false;
            }

            private void AddContainer(StorageContainer container)
            {
                if (IsBox(container, true) || container is BuildingPrivlidge)
                {
                    _containers.Add(container);
                }

                _allcontainers.Add(container);

                AddEntity(container);
            }

            private void SetupContainer(StorageContainer container)
            {
                AddContainer(container);

                if (container.inventory == null)
                {
                    container.inventory = new ItemContainer();
                    container.inventory.ServerInitialize(null, 30);
                    container.inventory.GiveUID();
                    container.inventory.entityOwner = container;
                }

                if (Options.LockBoxes && IsBox(container, false) || Options.LockLockers && container is Locker)
                {
                    CreateLock(container);
                }

                if (Options.EmptyAll && Type != RaidableType.None)
                {
                    container.inventory.Clear();
                    ItemManager.DoRemoves();
                }

                SetupBoxSkin(container);

                if (Type == RaidableType.None && container.inventory.itemList.Count > 0)
                {
                    return;
                }

                container.dropsLoot = false;
                container.dropFloats = false;

                if (container is BuildingPrivlidge)
                {
                    container.dropsLoot = config.Settings.Management.AllowCupboardLoot;
                }
                else if (!IsProtectedWeapon(container) && !(container is VendingMachine))
                {
                    container.dropsLoot = true;
                }
                //else container.isLootable = false;

                if (IsBox(container, false) || container is BuildingPrivlidge)
                {
                    container.inventory.SetFlag(ItemContainer.Flag.NoItemInput, Options.NoItemInput);
                }
            }

            private void SetupIO(ContainerIOEntity io)
            {
                io.dropFloats = false;
                io.dropsLoot = !IsProtectedWeapon(io);
                io.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
            }

            private void SetupIO(IOEntity io)
            {
                io.SetFlag(BaseEntity.Flags.Reserved8, true, false, true);
            }

            private void SetupLock(BaseEntity e, bool justCreated = false)
            {
                AddEntity(e);
                locks.Add(e);

                if (Type == RaidableType.None)
                {
                    return;
                }

                if (e is CodeLock)
                {
                    var codeLock = e as CodeLock;

                    if (config.Settings.Management.RandomCodes || justCreated)
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

                    if (config.Settings.Management.RandomCodes)
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
                vms.Add(vm);

                vm.SetFlag(BaseEntity.Flags.Reserved4, config.Settings.Management.AllowBroadcasting, false, true);
                vm.FullUpdate();
            }

            private void SetupLight(IOEntity light)
            {
                if (!config.Settings.Management.Lights && !config.Settings.Management.AlwaysLights)
                {
                    return;
                }

                lights.Add(light);
            }

            private void SetupHBHFSensor(HBHFSensor sensor)
            {
                triggers[sensor.myTrigger] = sensor;
                SetupIO(sensor);
            }

            private void SetupGenerator(ElectricGenerator generator)
            {
                generator.electricAmount = config.Weapons.TestGeneratorPower;
            }

            private void SetupButton(PressButton button)
            {
                button._maxHealth = Options.Elevators.ButtonHealth;
                button.InitializeHealth(Options.Elevators.ButtonHealth, Options.Elevators.ButtonHealth);
            }

            private void SetupBuildingBlock(BuildingBlock block)
            {
                if (block.IsKilled())
                {
                    return;
                }

                if (Options.Blocks.Any())
                {
                    ChangeTier(block);
                }

                block.StopBeingDemolishable();
                block.StopBeingRotatable();

                bool foundation = block.ShortPrefabName == "foundation.triangle" || block.ShortPrefabName == "foundation";
                bool floor = block.ShortPrefabName == "floor.triangle" || block.ShortPrefabName == "floor";
                bool roof = block.ShortPrefabName == "roof" || block.ShortPrefabName == "roof.triangle";

                if (foundation || floor || roof) blocks.Add(block);
            }

            private void ChangeTier(BuildingBlock block)
            {
                if (Options.Blocks.HQM && block.grade != BuildingGrade.Enum.TopTier)
                {
                    SetGrade(block, BuildingGrade.Enum.TopTier);
                }
                else if (Options.Blocks.Metal && block.grade != BuildingGrade.Enum.Metal)
                {
                    SetGrade(block, BuildingGrade.Enum.Metal);
                }
                else if (Options.Blocks.Stone && block.grade != BuildingGrade.Enum.Stone)
                {
                    SetGrade(block, BuildingGrade.Enum.Stone);
                }
                else if (Options.Blocks.Wooden && block.grade != BuildingGrade.Enum.Wood)
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
                if (!config.Weapons.TeslaCoil.RequiresPower)
                {
                    tc.UpdateFromInput(25, 0);
                    tc.SetFlag(IOEntity.Flag_HasPower, true, false, true);
                }

                tc.maxDischargeSelfDamageSeconds = Mathf.Clamp(config.Weapons.TeslaCoil.MaxDischargeSelfDamageSeconds, 0f, 9999f);
                tc.maxDamageOutput = Mathf.Clamp(config.Weapons.TeslaCoil.MaxDamageOutput, 0f, 9999f);
            }

            private void SetupIgniter(Igniter igniter)
            {
                igniter.SelfDamagePerIgnite = 0f;
            }

            private void SetupTurret(AutoTurret turret)
            {
                if (IsUnloading || turret.IsKilled())
                {
                    return;
                }

                SetupIO(turret as IOEntity);

                if (Type != RaidableType.None)
                {
                    turret.authorizedPlayers.Clear();
                }

                turret.InitializeHealth(Options.AutoTurret.Health, Options.AutoTurret.Health);
                turret.sightRange = Options.AutoTurret.SightRange;
                turret.aimCone = Options.AutoTurret.AimCone;
                turrets.Add(turret);

                if (Options.AutoTurret.RemoveWeapon)
                {
                    turret.AttachedWeapon = null;
                    Item slot = turret.inventory.GetSlot(0);

                    if (slot != null && (slot.info.category == ItemCategory.Weapon || slot.info.category == ItemCategory.Fun))
                    {
                        slot.RemoveFromContainer();
                        slot.Remove();
                    }
                }

                Options.AutoTurret.Shortnames.Remove("fun.trumpet");

                turret.Invoke(() =>
                {
                    if (!turret.IsDestroyed && Options.AutoTurret.Shortnames.Count > 0)
                    {
                        if (turret.AttachedWeapon.IsNull())
                        {
                            var shortname = Options.AutoTurret.Shortnames.GetRandom();
                            var itemToCreate = ItemManager.FindItemDefinition(shortname);

                            if (itemToCreate != null)
                            {
                                Item item = ItemManager.Create(itemToCreate, 1, (ulong)itemToCreate.skins.GetRandom().id);

                                if (!item.MoveToContainer(turret.inventory, 0, false))
                                {
                                    item.Remove();
                                }
                            }
                        }
                    }
                }, 0.1f);

                turret.Invoke(turret.UpdateAttachedWeapon, 1f);

                if (Instance.DebugMode)
                {
                    Instance.timer.Once(2.5f, () =>
                    {
                        if (turret.IsNull())
                        {
                            return;
                        }

                        FillAmmoTurret(turret);
                    });
                }
                else turret.Invoke(() => FillAmmoTurret(turret), 2.5f);

                if (Options.AutoTurret.Hostile)
                {
                    turret.SetPeacekeepermode(false);
                }

                if (!Options.AutoTurret.RequiresPower)
                {
                    turret.Invoke(turret.InitiateStartup, 3f);
                }

                if (config.Weapons.InfiniteAmmo.AutoTurret)
                {
                    turret.inventory.onPreItemRemove += new Action<Item>(OnWeaponItemPreRemove);
                }
            }

            private void SetInfiniteAmmo(Item item, int amount)
            {
                item.OnDirty += (Item ammo) =>
                {
                    ammo.amount = amount;
                };
            }

            private void SetupGunTrap(GunTrap gt)
            {
                if (config.Weapons.Ammo.GunTrap > 0)
                {
                    FillAmmoGunTrap(gt);
                }

                if (config.Weapons.InfiniteAmmo.GunTrap)
                {
                    gt.inventory.onPreItemRemove += new Action<Item>(OnWeaponItemPreRemove);
                }

                triggers[gt.trigger] = gt;
            }

            private void SetupFogMachine(FogMachine fm)
            {
                if (config.Weapons.Ammo.FogMachine > 0)
                {
                    FillAmmoFogMachine(fm);
                }

                if (config.Weapons.InfiniteAmmo.FogMachine)
                {
                    fm.fuelPerSec = 0f;
                }

                if (config.Weapons.FogMotion)
                {
                    fm.SetFlag(BaseEntity.Flags.Reserved7, true, false, true);
                }

                if (!config.Weapons.FogRequiresPower)
                {
                    fm.CancelInvoke(fm.CheckTrigger);
                    fm.SetFlag(BaseEntity.Flags.Reserved6, true, false, true);
                    fm.SetFlag(BaseEntity.Flags.Reserved8, true, false, true);
                    fm.SetFlag(BaseEntity.Flags.On, true, false, true);
                }
            }

            private void SetupFlameTurret(FlameTurret ft)
            {
                triggers[ft.trigger] = ft;
                ft.InitializeHealth(Options.FlameTurretHealth, Options.FlameTurretHealth);

                if (config.Weapons.Ammo.FlameTurret > 0)
                {
                    FillAmmoFlameTurret(ft);
                }

                if (config.Weapons.InfiniteAmmo.FlameTurret)
                {
                    ft.fuelPerSec = 0f;
                }
            }

            private void SetupSamSite(SamSite ss)
            {
                samsites.Add(ss);

                if (config.Weapons.SamSiteRepair > 0f)
                {
                    ss.staticRespawn = true;
                    ss.InvokeRepeating(ss.SelfHeal, config.Weapons.SamSiteRepair * 60f, config.Weapons.SamSiteRepair * 60f);
                }

                if (!config.Weapons.SamSiteRequiresPower)
                {
                    SetupIO(ss as IOEntity);
                }

                if (config.Weapons.SamSiteRange > 0f)
                {
                    ss.vehicleScanRadius = ss.missileScanRadius = config.Weapons.SamSiteRange;
                }

                if (config.Weapons.Ammo.SamSite > 0)
                {
                    FillAmmoSamSite(ss);
                }

                if (config.Weapons.InfiniteAmmo.SamSite)
                {
                    ss.inventory.onPreItemRemove += new Action<Item>(OnWeaponItemPreRemove);
                }
            }

            private bool ChangeTier(Door door)
            {
                if (door.isSecurityDoor)
                {
                    return false;
                }

                uint prefabID = 0u;

                switch (door.ShortPrefabName)
                {
                    case "door.hinged.toptier":
                        if (Options.Doors.Metal) prefabID = 202293038;
                        else if (Options.Doors.Wooden) prefabID = 1343928398;
                        break;
                    case "door.hinged.metal":
                    case "door.hinged.industrial.a":
                    case "door.hinged.industrial.d":
                        if (Options.Doors.HQM) prefabID = 170207918;
                        else if (Options.Doors.Wooden) prefabID = 1343928398;
                        break;
                    case "door.hinged.wood":
                        if (Options.Doors.HQM) prefabID = 170207918;
                        else if (Options.Doors.Metal) prefabID = 202293038;
                        break;
                    case "door.double.hinged.toptier":
                        if (Options.Doors.Metal) prefabID = 1418678061;
                        else if (Options.Doors.Wooden) prefabID = 43442943;
                        break;
                    case "wall.frame.garagedoor":
                        if (!Options.Doors.GarageDoor) return false;
                        if (Options.Doors.HQM) prefabID = 201071098;
                        else if (Options.Doors.Wooden) prefabID = 43442943;
                        break;
                    case "door.double.hinged.metal":
                        if (Options.Doors.HQM) prefabID = 201071098;
                        else if (Options.Doors.Wooden) prefabID = 43442943;
                        break;
                    case "door.double.hinged.wood":
                        if (Options.Doors.HQM) prefabID = 201071098;
                        else if (Options.Doors.Metal) prefabID = 1418678061;
                        break;
                }

                return prefabID != 0u && SetDoorType(door, prefabID);
            }

            private bool SetDoorType(Door door, uint prefabID)
            {
                var prefabName = StringPool.Get(prefabID);
                var position = door.transform.position;
                var rotation = door.transform.rotation;

                door.SafelyKill();

                var e = GameManager.server.CreateEntity(prefabName, position, rotation);

                e.Spawn();

                if (CanSetupEntity(e))
                {
                    SetupEntity(e);
                    SetupDoor(e as Door, true);
                    data.TemporaryUID.Add(e.net.ID);
                }

                return true;
            }

            private void SetupDoor(Door door, bool changed = false)
            {
                if (Options.DoorLock)
                {
                    CreateLock(door);
                }

                if (!changed && Options.Doors.Any() && ChangeTier(door))
                {
                    return;
                }

                SetupSkin(door);

                if (Options.CloseOpenDoors)
                {
                    door.SetOpen(false, true);
                }
            }

            private void SetupDoors()
            {
                doors.RemoveAll(x => x.IsKilled());

                foreach (var door in doors)
                {
                    SetupDoor(door);
                }
            }

            private void SetupDoorControllers()
            {
                doorControllers.RemoveAll(x => x.IsKilled());

                foreach (var cdm in doorControllers)
                {
                    SetupIO(cdm);

                    if (cdm.IsPaired())
                    {
                        doors.Remove(cdm.targetDoor);
                        continue;
                    }

                    var door = cdm.FindDoor(true);

                    if (door.IsReallyValid())
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

            private void CreateLock(BaseEntity entity)
            {
                if (Type == RaidableType.None || entity.IsKilled())
                {
                    return;
                }

                var slot = entity.GetSlot(BaseEntity.Slot.Lock) as BaseLock;

                if (slot.IsNull())
                {
                    CreateCodeLock(entity);
                    return;
                }

                KeyLock keyLock;
                if (slot.TryGetComponent(out keyLock))
                {
                    keyLock.SetParent(null);
                    keyLock.SafelyKill();
                }

                CreateCodeLock(entity);
            }

            private void CreateCodeLock(BaseEntity entity)
            {
                var codeLock = GameManager.server.CreateEntity(StringPool.Get(3518824735)) as CodeLock;

                if (codeLock.IsNull())
                {
                    return;
                }

                codeLock.gameObject.Identity();
                codeLock.SetParent(entity, entity.GetSlotAnchorName(BaseEntity.Slot.Lock));
                codeLock.Spawn();
                entity.SetSlot(BaseEntity.Slot.Lock, codeLock);

                SetupLock(codeLock, true);
            }

            private void SetupBuildingPriviledge(BuildingPrivlidge priv)
            {
                if (Type != RaidableType.None)
                {
                    priv.authorizedPlayers.Clear();
                    priv.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }

                if (Options.LockPrivilege)
                {
                    CreateLock(priv);
                }

                if (this.priv.IsKilled())
                {
                    this.priv = priv;
                }
                else if (priv.Distance(Location) < this.priv.Distance(Location))
                {
                    this.priv = priv;
                }

                privSpawned = true;
            }

            private void SetupLocker(Locker locker)
            {
                if (config.Settings.Management.Lockers)
                {
                    lockers.Add(locker);
                }
            }

            private void SetupRugs()
            {
                _rugs.RemoveAll(rug => rug.IsKilled());
                _decorDeployables.RemoveAll(x => x.IsKilled());

                foreach (var deployable in _decorDeployables)
                {
                    _rugs.RemoveAll(rug => rug != deployable && deployable.transform.position.y >= rug.transform.position.y && InRange(rug.transform.position, deployable.transform.position, 1f));
                }
            }

            private void SetupSleepingBag(SleepingBag bag)
            {
                if (Options.NPC.Inside.SpawnOnBeds)
                {
                    _beds.Add(bag);
                }

                if (Type != RaidableType.None)
                {
                    bag.deployerUserID = 0uL;
                }
            }

            private void SetupMountable(BaseMountable mountable)
            {
                if (!config.Settings.Management.DespawnMounts)
                {
                    Instance.MountEntities[mountable] = new MountInfo
                    {
                        position = Location,
                        radius = ProtectionRadius,
                        mountable = mountable
                    };
                }
            }

            private void SetupCollectible(CollectibleEntity ce)
            {
                if (IsBlacklisted(ce.ShortPrefabName))
                {
                    ce.itemList = null;
                }
            }

            private void SetupDecayEntity(DecayEntity decayEntity)
            {
                if (BuildingID == 0)
                {
                    BuildingID = BuildingManager.server.NewBuildingID();
                }

                decayEntity.AttachToBuilding(BuildingID);
                decayEntity.decay = null;
                decayEntity.upkeepTimer = float.MinValue;

                if (Options.NPC.Inside.SpawnOnRugs && decayEntity.ShortPrefabName.StartsWith("rug.") && Mathf.Approximately(decayEntity.transform.up.y, 1f))
                {
                    _rugs.RemoveAll(rug => rug.IsKilled());
                    _rugs.Add(decayEntity);

                    if (Options.NPC.Inside.SpawnOnRugsSkin != 1 && Options.NPC.Inside.SpawnOnRugsSkin >= 0)
                    {
                        _rugs.RemoveAll(rug => rug.skinID != Options.NPC.Inside.SpawnOnRugsSkin);
                    }
                }
            }

            private void SetupBoxSkin(StorageContainer container)
            {
                if (!IsBox(container, false))
                {
                    return;
                }

                ItemDefinition def;
                if (!Definitions.TryGetValue(container.gameObject.name, out def))
                {
                    return;
                }

                var si = GetItemSkins(def);

                if (si.allSkins.Count == 0)
                {
                    return;
                }

                if (!skinIds.ContainsKey(container.prefabID))
                {
                    if (config.Skins.Boxes.PresetSkin == 0uL)
                    {
                        var random = new List<ulong>();

                        if (config.Skins.Boxes.RandomWorkshopSkins)
                        {
                            random.Add(si.workshopSkins.GetRandom());
                        }

                        if (config.Skins.Boxes.RandomSkins)
                        {
                            random.Add(si.skins.GetRandom());
                        }

                        if (random.Count == 0)
                        {
                            skinIds[container.prefabID] = container.skinID;
                        }
                        else skinIds[container.prefabID] = random.GetRandom();
                    }
                    else skinIds[container.prefabID] = config.Skins.Boxes.PresetSkin;
                }

                if (config.Skins.Boxes.PresetSkin != 0uL || Options.SetSkins)
                {
                    container.skinID = skinIds[container.prefabID];
                }
                else if (config.Skins.Boxes.RandomWorkshopSkins)
                {
                    container.skinID = si.workshopSkins.GetRandom();
                }
                else if (config.Skins.Boxes.RandomSkins)
                {
                    container.skinID = si.skins.GetRandom();
                }
            }

            private void SetupSkin(BaseEntity entity)
            {
                if (IsBox(entity, false) || config.Skins.IgnoreSkinned && entity.skinID != 0uL)
                {
                    return;
                }

                if (!config.Skins.Deployables.Everything && !config.Skins.Deployables.Names.Exists(entity.name.Contains))
                {
                    return;
                }

                ItemDefinition def;
                if (!Definitions.TryGetValue(entity.gameObject.name, out def))
                {
                    return;
                }

                var si = GetItemSkins(def);
                var random = new List<ulong>();

                if (config.Skins.Deployables.RandomWorkshopSkins && si.workshopSkins.Count > 0)
                {
                    random.Add(si.workshopSkins.GetRandom());
                }

                if (config.Skins.Loot.Imported && si.importedSkins.Count > 0)
                {
                    random.Add(si.importedSkins.GetRandom());
                }

                if (config.Skins.Deployables.RandomSkins && si.skins.Count > 0)
                {
                    random.Add(si.skins.GetRandom());
                }

                if (random.Count > 0)
                {
                    entity.skinID = random.GetRandom();
                    entity.SendNetworkUpdate();
                }
            }

            private void Subscribe()
            {
                if (IsUnloading)
                {
                    return;
                }

                if (Instance.BaseRepair.CanCall())
                {
                    Subscribe(nameof(OnBaseRepair));
                }

                if (Options.EnforceDurability)
                {
                    Subscribe(nameof(OnLoseCondition));
                }

                if (config.Weapons.SamSiteRange > 0f)
                {
                    Subscribe(nameof(CanSamSiteShoot));
                }

                if (config.Settings.Management.NoLifeSupport)
                {
                    Subscribe(nameof(OnLifeSupportSavingLife));
                }

                if (Options.NPC.SpawnAmount > 0 && Options.NPC.Enabled)
                {
                    Options.NPC.SpawnAmount = Mathf.Clamp(Options.NPC.SpawnAmount, 0, 25);
                    Options.NPC.SpawnMinAmount = Mathf.Clamp(Options.NPC.SpawnMinAmount, 0, Options.NPC.SpawnAmount);
                    Options.NPC.ScientistHealth = Mathf.Clamp(Options.NPC.ScientistHealth, 1, 5000);
                    Options.NPC.MurdererHealth = Mathf.Clamp(Options.NPC.MurdererHealth, 1, 5000);
                    npcMaxAmount = Options.NPC.SpawnRandomAmount && Options.NPC.SpawnAmount > 1 ? UnityEngine.Random.Range(Options.NPC.SpawnMinAmount, Options.NPC.SpawnAmount + 1) : Options.NPC.SpawnAmount;

                    if (Options.NPC.Inside.Max > 0)
                    {
                        npcMaxAmountInside = UnityEngine.Random.Range(Options.NPC.Inside.Min, Options.NPC.Inside.Max + 1);
                        npcMaxAmountInside = Mathf.Clamp(npcMaxAmountInside, -1, npcMaxAmount);
                    }

                    if (npcMaxAmount > 0)
                    {
                        if (config.Settings.Management.BlockNpcKits)
                        {
                            Subscribe(nameof(OnNpcKits));
                        }

                        if (Options.NPC.PlayCatch)
                        {
                            Subscribe(nameof(OnExplosiveFuseSet));
                        }

                        Subscribe(nameof(OnNpcDuck));
                        Subscribe(nameof(OnNpcDestinationSet));
                        SetupNpcKits();
                        Invoke(SpawnNpcs, 1f);
                    }
                }

                if (config.Settings.Management.PreventFallDamage)
                {
                    Subscribe(nameof(OnPlayerLand));
                }

                if (!config.Settings.Management.AllowTeleport)
                {
                    Subscribe(nameof(CanTeleport));
                    Subscribe(nameof(canTeleport));
                }

                if (config.Settings.Management.BlockRestorePVP && AllowPVP || config.Settings.Management.BlockRestorePVE && !AllowPVP)
                {
                    Subscribe(nameof(OnRestoreUponDeath));
                }

                if (Options.DropTimeAfterLooting > 0 || config.UI.Containers)
                {
                    Subscribe(nameof(OnLootEntityEnd));
                }

                if (!config.Settings.Management.BackpacksOpenPVP || !config.Settings.Management.BackpacksOpenPVE)
                {
                    Subscribe(nameof(CanOpenBackpack));
                }

                if (config.Settings.Management.PreventFireFromSpreading)
                {
                    Subscribe(nameof(OnFireBallSpread));
                }

                if (Instance.IsPVE())
                {
                    Subscribe(nameof(CanEntityBeTargeted));
                    Subscribe(nameof(CanEntityTrapTrigger));
                }
                else
                {
                    Subscribe(nameof(CanBeTargeted));
                    Subscribe(nameof(OnTrapTrigger));
                }

                if (privSpawned)
                {
                    Subscribe(nameof(OnCupboardProtectionCalculated));
                }

                if (Options.BuildingRestrictions.Any() || !config.Settings.Management.AllowUpgrade)
                {
                    Subscribe(nameof(OnStructureUpgrade));
                }

                if (config.Settings.BlacklistedCommands.Exists(x => x.Equals("remove", StringComparison.OrdinalIgnoreCase)))
                {
                    Subscribe(nameof(canRemove));
                }

                Subscribe(nameof(CanPickupEntity));
                Subscribe(nameof(OnPlayerDropActiveItem));
                Subscribe(nameof(OnPlayerDeath));
                Subscribe(nameof(OnEntityDeath));
                Subscribe(nameof(OnEntityKill));
                Subscribe(nameof(CanBGrade));
                Subscribe(nameof(CanBePenalized));
                Subscribe(nameof(OnEntityGroundMissing));
                Subscribe(nameof(OnLootEntity));
                Subscribe(nameof(OnEntityBuilt));
                Subscribe(nameof(OnCupboardAuthorize));
            }

            private void Subscribe(string hook) => Instance.Subscribe(hook);

            private void MakeAnnouncements()
            {
                if (Type == RaidableType.None)
                {
                    _allcontainers.RemoveWhere(x => x.IsKilled());

                    itemAmountSpawned = _allcontainers.ToList().Sum(x => x.inventory.itemList.Count);
                }

                var posStr = FormatGridReference(Location);

                Puts("{0} @ {1} : {2} items", BaseName, posStr, itemAmountSpawned);

                if (Options.Silent)
                {
                    return;
                }

                foreach (var target in BasePlayer.activePlayerList)
                {
                    var difficultyMode = mx($"Mode{Options.Mode}", target.UserIDString);
                    float distance = Mathf.Floor((target.transform.position - Location).magnitude);
                    string flag = mx(AllowPVP ? "PVPFlag" : "PVEFlag", target.UserIDString).Replace("[", string.Empty).Replace("] ", string.Empty);
                    string api = mx("RaidOpenMessage", target.UserIDString, difficultyMode, posStr, distance, flag);
                    string api2 = mx("Owner", target.UserIDString);
                    if (Type == RaidableType.None) api = api.Replace(difficultyMode, NoMode);
                    string message = owner.IsReallyValid() ? string.Format("{0}[{1} {2}]", api, api2, owner.displayName) : api;

                    if ((!IsPayLocked && config.EventMessages.Opened) || (IsPayLocked && config.EventMessages.OpenedAndPaid))
                    {
                        QueueNotification(target, message);
                    }
                    else if (distance <= config.GUIAnnouncement.Distance)
                    {
                        QueueNotification(target, message);
                    }
                }
            }

            private float _lastInvokeUpdate;

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
                if (config.Settings.Management.LockTime > 0f)
                {
                    if (IsInvoking(ResetOwner)) CancelInvoke(ResetOwner);
                    Invoke(ResetOwner, config.Settings.Management.LockTime * 60f);
                }
            }

            public void ResetOwner()
            {
                if (!IsOpened || IsPayLocked || !ownerId.IsSteamId() || IsPlayerActive(ownerId.ToString()))
                {
                    TryInvokeResetOwner();
                    return;
                }

                if (config.Settings.Management.SetLockout)
                {
                    TrySetLockout(riOwner);
                    CheckBackpacks(true);
                }

                ResetLock();
            }

            public void TryInvokeResetPayLock()
            {
                if (!IsUnloading && config.Settings.Buyable.ResetDuration > 0 && IsPayLocked && IsOpened)
                {
                    CancelInvoke(ResetPayLock);
                    Invoke(ResetPayLock, config.Settings.Buyable.ResetDuration * 60f);
                }
            }

            private void ResetPayLock() => ResetPayLock(false);

            private void ResetPayLock(bool forced)
            {
                if (!forced && (!IsOpened || IsPlayerActive(ownerId.ToString()) || owner.IsReallyValid()))
                {
                    return;
                }

                ResetLock();
                IsEngaged = true;
                IsPayLocked = false;
                CheckBackpacks(true);
                raiders.Values.ToList().ForEach(ri => ri.IsParticipant = false);
            }

            private void ResetLock()
            {
                riOwner = null;
                owner = null;
                ownerId = 0;
                ownerName = string.Empty;
                UpdateMarker();
            }

            public void DropItems(ItemContainer[] containers, bool isMurderer)
            {
                Invoke(() =>
                {
                    if (containers.IsNullOrEmpty())
                    {
                        return;
                    }

                    if (isMurderer && Options.NPC.MurdererDrops.Count > 0)
                    {
                        Options.NPC.MurdererDrops.ForEach(ti =>
                        {
                            if (UnityEngine.Random.value <= ti.probability)
                            {
                                SpawnItem(ti, containers);
                            }
                        });
                    }
                    else if (!isMurderer && Options.NPC.ScientistDrops.Count > 0)
                    {
                        Options.NPC.ScientistDrops.ForEach(ti =>
                        {
                            if (UnityEngine.Random.value <= ti.probability)
                            {
                                SpawnItem(ti, containers);
                            }
                        });
                    }
                }, 1f);
            }

            private void ClearLoot()
            {
                BaseLoot.Clear();
                BaseLootPermanent.Clear();
                DifficultyLoot.Clear();
                DefaultLoot.Clear();
                Collective.Clear();
                Loot.Clear();
                LootNames.Clear();
            }

            private void SpawnLoot(List<StorageContainer> containers)
            {
                if (Options.DivideLoot)
                {
                    DivideLoot(containers);
                }
                else SpawnLoot(containers, treasureAmount);

                if (itemAmountSpawned == 0)
                {
                    Puts(mx("NoLootSpawned"));
                }

                Interface.Oxide.NextTick(ClearLoot);
            }

            private void SpawnLoot(List<StorageContainer> containers, int amount)
            {
                StorageContainer container = containers.FirstOrDefault(x => x.inventory.itemList.Count + amount <= x.inventory.capacity);

                if (container == null)
                {
                    container = containers.GetRandom();
                    container.inventory.Clear();
                    ItemManager.DoRemoves();
                }

                SpawnLoot(container, amount);
            }

            private void SpawnLoot(StorageContainer container, int amount)
            {
                if (amount > container.inventory.capacity)
                {
                    amount = container.inventory.capacity;
                }

                for (int j = 0; j < amount; j++)
                {
                    if (Loot.Count == 0)
                    {
                        break;
                    }

                    var lootItem = GetRandomLootItem();

                    SpawnItem(lootItem, new List<StorageContainer> { container });
                }
            }

            private LootItem GetRandomLootItem()
            {
                for (int i = 0; i < Loot.Count; i++)
                {
                    if (IsPriority(Loot[i]) || Loot[i].isModified)
                    {
                        return Loot[i];
                    }
                }

                return Loot.GetRandom();
            }

            private void DivideLoot(List<StorageContainer> containers)
            {
                while (Loot.Count > 0 && containers.Count > 0 && itemAmountSpawned < treasureAmount)
                {
                    var lootItem = GetRandomLootItem();

                    if (containers.Count > 1)
                    {
                        var lastContainer = containers[0];

                        containers.Remove(lastContainer);

                        SpawnItem(lootItem, containers);

                        containers.Insert(containers.Count, lastContainer);
                    }
                    else SpawnItem(lootItem, containers);

                    containers.RemoveAll(container => container.inventory.IsFull());
                }
            }

            private void AddToLoot(List<LootItem> source)
            {
                foreach (var ti in source)
                {
                    AddToLoot(ti);
                }

                source.Clear();
            }

            private void AddToLoot(LootItem lootItem)
            {
                LootItem ti = lootItem.Clone();
                bool isBlueprint = ti.shortname.EndsWith(".bp");
                string shortname = isBlueprint ? ti.shortname.Replace(".bp", string.Empty) : ti.shortname;
                bool isModified = false;

                if (shortname.Contains("_") && ItemManager.FindItemDefinition(shortname) == null)
                {
                    isModified = true;
                    shortname = shortname.Substring(shortname.IndexOf("_") + 1);
                }

                if (ti.definition == null)
                {
                    Puts("Invalid shortname in config: {0} -> {1}", ti.shortname, shortname);
                    return;
                }

                ti.isBlueprint = isBlueprint;

                int amount = ti.amount;

                if (ti.amountMin < ti.amount)
                {
                    amount = Core.Random.Range(ti.amountMin, ti.amount + 1);
                }

                if (amount <= 0)
                {
                    Loot.RemoveAll(x => lootItem.Equals(x));
                    Collective.RemoveAll(x => lootItem.Equals(x));
                    return;
                }

                if (config.Treasure.UseStackSizeLimit || ti.stacksize != -1)
                {
                    int stackable = ti.stacksize <= 0 ? ti.definition.stackable : ti.stacksize;
                    if (!isModified) isModified = amount > stackable;

                    foreach (int stack in GetStacks(amount, stackable))
                    {
                        Loot.Add(new LootItem(shortname, stack, stack, ti.skin, isBlueprint, ti.probability, ti.stacksize, ti.name, isModified));
                    }
                }
                else Loot.Add(new LootItem(shortname, amount, amount, ti.skin, isBlueprint, ti.probability, ti.stacksize, ti.name, isModified));
            }

            private List<int> GetStacks(int amount, int maxStack)
            {
                var stacks = new List<int>();

                while (amount > maxStack && stacks.Count < 100)
                {
                    amount -= maxStack;
                    stacks.Add(maxStack);
                }

                if (amount > 0)
                {
                    stacks.Add(amount);
                }

                return stacks;
            }

            private void TakeLootFrom(LootType type)
            {
                List<LootItem> lootList;
                if (Instance.Buildings.DifficultyLootLists.TryGetValue(type, out lootList))
                {
                    TakeLootFrom(lootList, DifficultyLoot, LootFileType.Difficulty, config.Treasure.UniqueDifficultyLoot);
                }
            }

            private void TakeLootFrom(List<LootItem> source, List<LootItem> to, LootFileType lootFileType, bool isUnique)
            {
                if (source.Count == 0)
                {
                    return;
                }

                var from = new List<LootItem>();

                foreach (var ti in source)
                {
                    if (ti == null || ti.amount <= 0 || ti.probability <= 0f)
                    {
                        continue;
                    }

                    Collective.Add(ti.Clone());
                    from.Add(ti.Clone());
                }

                if (from.Count == 0)
                {
                    return;
                }

                Shuffle(from);

                if (lootFileType != LootFileType.Base || !Options.AlwaysSpawn)
                {
                    foreach (var ti in from)
                    {
                        if (UnityEngine.Random.value <= ti.probability)
                        {
                            to.Add(ti);
                        }
                    }
                }
                else to.AddRange(from);

                if (Options.Multiplier == 1f)
                {
                    return;
                }

                var m = Mathf.Clamp(Options.Multiplier, 0f, 999f);

                foreach (var ti in to)
                {
                    if (ti.amount > 1)
                    {
                        ti.amount = Mathf.CeilToInt(ti.amount * m);
                        ti.amountMin = Mathf.CeilToInt(ti.amountMin * m);
                    }
                }
            }

            private bool SetupLootContainers(List<StorageContainer> containers)
            {
                if (_containers.Count == 0)
                {
                    Puts(mx(Entities.Exists() ? "NoContainersFound" : "NoEntitiesFound", null, BaseName, PositionToGrid(Location)));
                    return false;
                }

                CheckExpansionSettings();

                foreach (var container in _containers)
                {
                    if (!IsBox(container, true) || Options.IgnoreContainedLoot && !container.inventory.IsEmpty())
                    {
                        continue;
                    }

                    containers.Add(container);
                }

                if (Options.IgnoreContainedLoot)
                {
                    lockers.RemoveAll(x => !x.inventory.IsEmpty());
                }

                if (containers.Count == 0)
                {
                    Puts(mx("NoBoxesFound", null, BaseName, PositionToGrid(Location)));
                    return false;
                }

                return true;
            }

            private void SetTreasureLootLimit(List<StorageContainer> containers)
            {
                if (treasureAmount <= 400)
                {
                    return;
                }

                int availableSpace = privSpawned && config.Settings.Management.Cupboard ? 24 : 0;

                foreach (var container in containers)
                {
                    availableSpace += container.InventorySlots();
                }

                if (config.Settings.Management.Lockers)
                {
                    foreach (var container in lockers)
                    {
                        availableSpace += container.InventorySlots();
                    }
                }

                if (config.Settings.Management.Cook)
                {
                    foreach (var container in ovens)
                    {
                        availableSpace += container.InventorySlots();
                    }
                }

                if (config.Settings.Management.Food)
                {
                    foreach (var container in _allcontainers)
                    {
                        if (container?.ShortPrefabName != "fridge.deployed") continue;
                        availableSpace += container.InventorySlots();
                    }

                    foreach (var container in ovens)
                    {
                        if (!Instance.BBQs.Contains(container.ShortPrefabName)) continue;
                        availableSpace += container.InventorySlots();
                    }
                }

                if (treasureAmount > availableSpace)
                {
                    treasureAmount = availableSpace;
                }
            }

            private void TakeLootFromBaseLoot()
            {
                var baseLoot = new List<LootItem>();

                foreach (var profile in Instance.Buildings.Profiles)
                {
                    if (profile.Key == BaseName || profile.Value.Options.AdditionalBases.ContainsKey(BaseName))
                    {
                        baseLoot.AddRange(profile.Value.BaseLootList.ToList());
                        break;
                    }
                }

                TakeLootFrom(baseLoot, BaseLoot, LootFileType.Base, config.Treasure.UniqueBaseLoot);

                BaseLootPermanent = BaseLoot.ToList();
            }

            private void TakeLootFromDifficultyLoot()
            {
                if (BaseLoot.Count < treasureAmount)
                {
                    switch (Options.Mode)
                    {
                        case RaidableMode.Easy: TakeLootFrom(LootType.Easy); break;
                        case RaidableMode.Medium: TakeLootFrom(LootType.Medium); break;
                        case RaidableMode.Hard: TakeLootFrom(LootType.Hard); break;
                        case RaidableMode.Expert: TakeLootFrom(LootType.Expert); break;
                        case RaidableMode.Nightmare: TakeLootFrom(LootType.Nightmare); break;
                    }
                }
            }

            private void TakeLootFromDefaultLoot()
            {
                if (BaseLoot.Count + DifficultyLoot.Count < treasureAmount)
                {
                    TakeLootFrom(TreasureLoot, DefaultLoot, LootFileType.Default, config.Treasure.UniqueDefaultLoot);
                }

                if (BaseLoot.Count + DifficultyLoot.Count + DefaultLoot.Count < treasureAmount)
                {
                    TakeLootFrom(WeekdayLoot, DefaultLoot, LootFileType.Weekday, config.Treasure.UniqueDefaultLoot);
                }
            }

            private void PopulateLoot(bool unique)
            {
                if (!unique || !config.Treasure.UniqueBaseLoot)
                {
                    AddToLoot(BaseLoot);
                }

                if (!unique || !config.Treasure.UniqueDifficultyLoot)
                {
                    AddToLoot(DifficultyLoot);
                }

                if (!unique || !config.Treasure.UniqueDefaultLoot)
                {
                    AddToLoot(DefaultLoot);
                }
            }

            private void TryAddDuplicates()
            {
                if (Options.AllowDuplicates && Loot.Count > 0 && Loot.Count < treasureAmount)
                {
                    int attempts = treasureAmount;

                    while (Loot.Count < treasureAmount && Collective.Count > 0 && --attempts > 0)
                    {
                        var ti = Collective.GetRandom();

                        if (IsUnique(ti))
                        {
                            Collective.Remove(ti);
                            continue;
                        }

                        AddToLoot(ti);
                    }
                }
            }

            private void TryRemoveDuplicates()
            {
                if (!Options.AllowDuplicates)
                {
                    var newLoot = new List<LootItem>();

                    foreach (var ti in Loot)
                    {
                        if (ti.isModified || !LootNames.Contains(ti.shortname) || IsPriority(ti))
                        {
                            LootNames.Add(ti.shortname);
                            newLoot.Add(ti);
                        }
                    }

                    Loot = newLoot;
                }

                foreach (var ti in Loot)
                {
                    LootNames.Add(ti.shortname);
                }
            }

            private bool IsPriority(LootItem a)
            {
                return Options.AlwaysSpawn && BaseLootPermanent.Exists(b => a.shortname == b.shortname);
            }

            private bool IsUnique(LootItem ti)
            {
                if (!Options.AllowDuplicates && Loot.Exists(x => x.shortname == ti.shortname))
                {
                    return true;
                }

                if (config.Treasure.UniqueBaseLoot && BaseLootPermanent.Exists(x => x.Equals(ti)))
                {
                    return true;
                }

                if (config.Treasure.UniqueDifficultyLoot && DifficultyLoot.Exists(x => x.Equals(ti)))
                {
                    return true;
                }

                if (config.Treasure.UniqueDefaultLoot && DefaultLoot.Exists(x => x.Equals(ti)))
                {
                    return true;
                }

                return false;
            }

            private void VerifyLootAmount()
            {
                if (Loot.Count > treasureAmount)
                {
                    Shuffle(Loot);

                    int index = Loot.Count;

                    while (Loot.Count > treasureAmount && --index >= 0)
                    {
                        if (!IsPriority(Loot[index]))
                        {
                            Loot.RemoveAt(index);
                        }
                    }
                }
                else
                {
                    int retries = treasureAmount - Loot.Count;

                    while (Loot.Count < treasureAmount && Collective.Count > 0 && --retries > 0)
                    {
                        var ti = Collective.GetRandom();

                        if (IsUnique(ti))
                        {
                            Collective.Remove(ti);
                            continue;
                        }

                        LootNames.Add(ti.shortname);
                        AddToLoot(ti);
                        retries++;
                    }
                }
            }

            private void SetupSellOrders()
            {
                if (!config.Settings.Management.Inherit.Exists("vendingmachine".Contains))
                {
                    return;
                }

                vms.RemoveAll(vm => vm.IsKilled());

                foreach (var vm in vms)
                {
                    vm.InstallDefaultSellOrders();
                    vm.SetFlag(BaseEntity.Flags.Reserved4, config.Settings.Management.AllowBroadcasting, false, true);

                    foreach (Item item in vm.inventory.itemList)
                    {
                        if (vm.sellOrders.sellOrders.Count < 8)
                        {
                            ItemDefinition itemToSellDef = ItemManager.FindItemDefinition(item.info.itemid);
                            ItemDefinition currencyDef = ItemManager.FindItemDefinition(-932201673);

                            if (!(itemToSellDef == null) && !(currencyDef == null))
                            {
                                int itemToSellAmount = Mathf.Clamp(item.amount, 1, itemToSellDef.stackable);

                                vm.sellOrders.sellOrders.Add(new ProtoBuf.VendingMachine.SellOrder
                                {
                                    ShouldPool = false,
                                    itemToSellID = item.info.itemid,
                                    itemToSellAmount = itemToSellAmount,
                                    currencyID = -932201673,
                                    currencyAmountPerItem = 999999,
                                    currencyIsBP = true,
                                    itemToSellIsBP = item.IsBlueprint()
                                });

                                vm.RefreshSellOrderStockLevel(itemToSellDef);
                            }
                        }
                    }

                    vm.FullUpdate();
                }
            }

            private List<LootItem> BaseLoot { get; set; } = new List<LootItem>();
            private List<LootItem> BaseLootPermanent { get; set; } = new List<LootItem>();
            private List<LootItem> Collective { get; set; } = new List<LootItem>();
            private List<LootItem> DifficultyLoot { get; set; } = new List<LootItem>();
            private List<LootItem> DefaultLoot { get; set; } = new List<LootItem>();
            private List<LootItem> Loot { get; set; } = new List<LootItem>();
            private HashSet<string> LootNames { get; set; } = new HashSet<string>();

            private void SpawnItem(LootItem ti, ItemContainer[] containers)
            {
                Item item = CreateItem(ti);

                if (item == null || item.MoveToContainer(containers[0]) || item.MoveToContainer(containers[1]) || item.MoveToContainer(containers[2]))
                {
                    return;
                }

                item.Remove();
            }

            private SpawnResult SpawnItem(LootItem ti, List<StorageContainer> containers)
            {
                Item item = CreateItem(ti);

                if (item == null)
                {
                    return SpawnResult.Skipped;
                }

                foreach (var container in containers)
                {
                    if (MoveToCupboard(item) || MoveToBBQ(item) || MoveToOven(item) || MoveToFridge(item) || MoveToLocker(item))
                    {
                        itemAmountSpawned++;
                        return SpawnResult.Transfer;
                    }
                    else if (container is BaseOven && !IsCookable(item.info))
                    {
                        continue;
                    }
                    else if (item.MoveToContainer(container.inventory, -1, false))
                    {
                        itemAmountSpawned++;
                        return SpawnResult.Success;
                    }
                }

                item.Remove();
                return SpawnResult.Failure;
            }

            private Item CreateItem(LootItem ti)
            {
                if (ti.amount <= 0)
                {
                    return null;
                }

                if (ti.definition == null)
                {
                    Puts("Invalid shortname in config: {0}", ti.shortname);
                    return null;
                }

                var def = ti.definition;
                ulong skin = GetItemSkin(def, ti.skin, true);

                Item item;
                if (ti.isBlueprint)
                {
                    item = ItemManager.Create(Workbench.GetBlueprintTemplate());
                    item.blueprintTarget = def.itemid;
                    item.amount = ti.amount;
                }
                else item = ItemManager.Create(def, ti.amount, skin);

                if (!string.IsNullOrEmpty(ti.name))
                {
                    item.name = ti.name;
                }

                var e = item.GetHeldEntity();

                if (e.IsReallyValid())
                {
                    e.skinID = skin;
                    e.SendNetworkUpdate();
                }

                return item;
            }

            private bool MoveToFridge(Item item)
            {
                if (!config.Settings.Management.Food || _allcontainers.Count == 0 || item.info.category != ItemCategory.Food)
                {
                    return false;
                }

                if (config.Settings.Management.Foods.Exists(item.info.shortname.Contains))
                {
                    return false;
                }

                if (_allcontainers.Count <= 1)
                {
                    return false;
                }

                var fridges = _allcontainers.Where(x => !x.IsKilled() && x.ShortPrefabName == "fridge.deployed");

                if (fridges.Count > 1)
                {
                    Shuffle(fridges);
                }

                foreach (var x in fridges)
                {
                    if (item.MoveToContainer(x.inventory, -1, true))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool MoveToBBQ(Item item)
            {
                if (!config.Settings.Management.Food || ovens.Count == 0 || item.info.category != ItemCategory.Food || !IsCookable(item.info))
                {
                    return false;
                }

                if (config.Settings.Management.Foods.Exists(item.info.shortname.Contains))
                {
                    return false;
                }

                if (ovens.Count > 1)
                {
                    Shuffle(ovens);
                }

                foreach (var oven in ovens)
                {
                    if (oven.IsKilled())
                    {
                        continue;
                    }

                    if (Instance.BBQs.Contains(oven.ShortPrefabName) && item.MoveToContainer(oven.inventory, -1, true))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool MoveToCupboard(Item item)
            {
                if (!config.Settings.Management.Cupboard || !privSpawned || item.info.category != ItemCategory.Resources || config.Treasure.ExcludeFromCupboard.Contains(item.info.shortname))
                {
                    return false;
                }

                if (config.Settings.Management.Cook && item.info.shortname.EndsWith(".ore") && MoveToOven(item))
                {
                    return true;
                }

                if (!priv.IsKilled() && item.MoveToContainer(priv.inventory, -1, true))
                {
                    return true;
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
                if (!config.Settings.Management.Cook || ovens.Count == 0 || !IsCookable(item.info))
                {
                    return false;
                }

                if (ovens.Count > 1)
                {
                    Shuffle(ovens);
                }

                foreach (var oven in ovens)
                {
                    if (oven.IsKilled() || Instance.BBQs.Contains(oven.ShortPrefabName))
                    {
                        continue;
                    }

                    if (item.info.shortname.EndsWith(".ore") && !Instance.Furnaces.Contains(oven.ShortPrefabName))
                    {
                        continue;
                    }

                    if (item.info.shortname == "lowgradefuel" && !Instance.Lanterns.Contains(oven.ShortPrefabName))
                    {
                        continue;
                    }

                    if (item.info.shortname == "crude.oil" && !Instance.Refineries.Contains(oven.ShortPrefabName))
                    {
                        continue;
                    }

                    if (item.MoveToContainer(oven.inventory, -1, true))
                    {
                        if (!oven.IsOn() && oven.FindBurnable() != null)
                        {
                            oven.SetFlag(BaseEntity.Flags.On, true, false, true);
                        }

                        if (oven.IsOn() && !item.HasFlag(global::Item.Flag.OnFire))
                        {
                            item.SetFlag(global::Item.Flag.OnFire, true);
                            item.MarkDirty();
                        }

                        return true;
                    }
                }

                return false;
            }

            private bool IsHealthy(Item item)
            {
                if (item.info.category == ItemCategory.Food || item.info.category == ItemCategory.Medical)
                {
                    if (item.info.shortname.Contains(".spoiled") || item.info.shortname.Contains(".raw") || item.info.shortname.Contains(".burned"))
                    {
                        return false;
                    }

                    return item.info.GetComponent<ItemModConsumable>();
                }

                return false;
            }

            private bool IsRangedWeapon(Item item)
            {
                return item.info.category == ItemCategory.Weapon && item.info.GetComponent<ItemModProjectile>() != null;
            }

            private bool MoveToLocker(Item item)
            {
                if (!config.Settings.Management.Lockers || lockers.Count == 0)
                {
                    return false;
                }

                foreach (var locker in lockers)
                {
                    if (locker.IsKilled())
                    {
                        continue;
                    }

                    if (Instance.Helms.Contains(item.info.shortname) && MoveToContainer(locker.inventory, item, 0, 13, 26))
                    {
                        return true;
                    }
                    else if (Instance.Boots.Contains(item.info.shortname) && MoveToContainer(locker.inventory, item, 1, 14, 27))
                    {
                        return true;
                    }
                    else if (Instance.Gloves.Contains(item.info.shortname) && MoveToContainer(locker.inventory, item, 2, 15, 28))
                    {
                        return true;
                    }
                    else if (Instance.Vests.Contains(item.info.shortname) && MoveToContainer(locker.inventory, item, 3, 16, 29))
                    {
                        return true;
                    }
                    else if (Instance.Legs.Contains(item.info.shortname) && MoveToContainer(locker.inventory, item, 4, 17, 30))
                    {
                        return true;
                    }
                    else if (Instance.Shirts.Contains(item.info.shortname) && MoveToContainer(locker.inventory, item, 5, 18, 31))
                    {
                        return true;
                    }
                    else if (Instance.Other.Contains(item.info.shortname) && MoveToContainer(locker.inventory, item, 6, 19, 32))
                    {
                        return true;
                    }
                    else if (IsRangedWeapon(item) && MoveToContainer(locker.inventory, item, 7, 8, 20, 21, 33, 34))
                    {
                        return true;
                    }
                    else if (item.info.category == ItemCategory.Ammunition && MoveToContainer(locker.inventory, item, 9, 10, 22, 23, 35, 36))
                    {
                        return true;
                    }
                    else if (IsHealthy(item) && MoveToContainer(locker.inventory, item, 11, 12, 24, 25, 37, 38))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool MoveToContainer(ItemContainer container, Item item, params int[] positions)
            {
                foreach (int position in positions)
                {
                    if (item.MoveToContainer(container, position, false))
                    {
                        return true;
                    }
                }

                return false;
            }

            private void CheckExpansionSettings()
            {
                if (!config.Settings.ExpansionMode || !Instance.DangerousTreasures.CanCall())
                {
                    return;
                }

                var boxes = Pool.GetList<StorageContainer>();

                foreach (var x in _containers)
                {
                    if (x.ShortPrefabName == "box.wooden.large")
                    {
                        boxes.Add(x);
                    }
                }

                if (boxes.Count > 0)
                {
                    Instance.DangerousTreasures?.Call("API_SetContainer", boxes.GetRandom(), Radius, !Options.NPC.Enabled || Options.NPC.UseExpansionNpcs);
                }

                Pool.FreeList(ref boxes);
            }

            private bool ToggleNpcMinerHat(ScientistNPC npc, bool state)
            {
                if (npc.IsNull() || npc.inventory == null || npc.IsDead())
                {
                    return false;
                }

                var slot = npc.inventory.FindItemID("hat.miner");

                if (slot == null)
                {
                    return false;
                }

                if (state && slot.contents != null)
                {
                    slot.contents.AddItem(ItemManager.FindItemDefinition("lowgradefuel"), 50);
                }

                slot.SwitchOnOff(state);
                npc.inventory.ServerUpdate(0f);
                return true;
            }

            public void ToggleLights(bool state)
            {
                if (config.Settings.Management.AlwaysLights && lightsOn && !state)
                {
                    return;
                }

                if (!lights.IsNullOrEmpty())
                {
                    foreach (var io in lights)
                    {
                        if (io.IsKilled()) continue;
                        io.UpdateHasPower(state ? 25 : 0, 1);
                        io.SetFlag(BaseEntity.Flags.On, state);
                        io.SendNetworkUpdateImmediate();
                    }
                }

                if (!ovens.IsNullOrEmpty())
                {
                    foreach (var oven in ovens.Where(oven => !oven.IsKilled()))
                    {
                        if (state && (Instance.Furnaces.Contains(oven.ShortPrefabName) && (oven as BaseOven).inventory.IsEmpty())) continue;
                        else if (!state && (oven.ShortPrefabName == "campfire" || Instance.Furnaces.Contains(oven.ShortPrefabName) || Instance.BBQs.Contains(oven.ShortPrefabName))) continue;
                        oven.SetFlag(BaseEntity.Flags.On, state);
                    }
                }

                if (!npcs.IsNullOrEmpty())
                {
                    foreach (var npc in npcs.Where(npc => !npc.IsKilled()))
                    {
                        ToggleNpcMinerHat(npc, state);
                    }
                }

                lightsOn = state;
            }

            public void Undo()
            {
                if (IsOpened)
                {
                    float time = config.Settings.Management.DespawnMinutes * 60f;

                    IsOpened = false;
                    CancelInvoke(ResetOwner);

                    if (time > 0f)
                    {
                        despawnTime = Time.time + time;

                        if (config.EventMessages.ShowWarning)
                        {
                            var grid = FormatGridReference(Location);

                            foreach (var target in BasePlayer.activePlayerList)
                            {
                                QueueNotification(target, "DestroyingBaseAt", grid, config.Settings.Management.DespawnMinutes);
                            }
                        }

                        Instance.timer.Once(time, () => Destroy(go));
                    }
                    else Destroy(go);
                }
            }

            public bool Any(ulong userid, bool checkAllies = true)
            {
                if (ownerId == userid) return true;
                Raider ri;
                if (!raiders.TryGetValue(userid, out ri)) return false;
                return ri.IsParticipant || checkAllies && ri.IsAlly;
            }

            public static bool IsOwner(BasePlayer player)
            {
                return Instance.Raids.Exists(raid => raid.ownerId == player.userID && (raid.IsOpened || raid.IsDespawning));
            }

            public static bool Has(ulong userID)
            {
                return Instance.Npcs.ContainsKey(userID);
            }

            public static bool Has(TriggerBase triggerBase)
            {
                return Instance.Raids.Exists(raid => raid.triggers.ContainsKey(triggerBase));
            }

            public static bool Has(BaseEntity entity)
            {
                return Instance.RaidEntities.ContainsKey(entity);
            }

            public static int Get(RaidableType type)
            {
                return Instance.Locations.Sum(value => value.Type == type ? 1 : 0);
            }

            public static int Get(RaidableMode mode)
            {
                return Instance.Raids.Sum(raid => raid.Options.Mode == mode && !raid.IsDespawning ? 1 : 0);
            }

            public static RaidableBase Get(ulong userID)
            {
                RaidableBase raid;

                return Instance.Npcs.TryGetValue(userID, out raid) ? raid : null;
            }

            public static RaidableBase Get(Vector3 target, float f = 0f)
            {
                return Instance.Raids.FirstOrDefault(raid => InRange2D(raid.Location, target, raid.ProtectionRadius + f));
            }

            public static RaidableBase Get(BasePlayer victim, HitInfo hitInfo = null)
            {
                if (Has(victim.userID))
                {
                    return Get(victim.userID);
                }

                DelaySettings ds;
                if (Instance.PvpDelay.TryGetValue(victim.userID, out ds) && ds.RaidableBase != null)
                {
                    return ds.RaidableBase;
                }

                return Get(victim.ServerPosition) ?? Get(hitInfo);
            }

            public static RaidableBase Get(HitInfo hitInfo)
            {
                if (hitInfo?.Initiator?.IsDestroyed == false)
                {
                    var raid = Get(hitInfo.Initiator.ServerPosition);
                    if (raid != null) return raid;
                }

                if (hitInfo?.WeaponPrefab?.IsDestroyed == false)
                {
                    var raid = Get(hitInfo.WeaponPrefab.ServerPosition);
                    if (raid != null) return raid;
                }

                if (hitInfo?.Weapon?.IsDestroyed == false)
                {
                    return Get(hitInfo.Weapon.ServerPosition);
                }

                return null;
            }

            public static RaidableBase Get(PlayerCorpse corpse)
            {
                if (!corpse.playerSteamID.IsSteamId())
                {
                    return Get(corpse.playerSteamID);
                }

                DelaySettings ds;

                return Instance.PvpDelay.TryGetValue(corpse.playerSteamID, out ds) ? ds.RaidableBase : Get(corpse.transform.position);
            }

            public static RaidableBase Get(BaseEntity entity)
            {
                RaidableBase raid;
                return Instance.RaidEntities.TryGetValue(entity, out raid) ? raid : null;
            }

            public static bool IsTooClose(Vector3 target, float radius)
            {
                for (int i = 0; i < Instance.Locations.Count; i++)
                {
                    if (InRange2D(Instance.Locations[i].Position, target, radius))
                    {
                        return true;
                    }
                }
                return false;
            }

            private static bool IsBlacklistedSkin(ItemDefinition def, int num)
            {
                var skinId = ItemDefinition.FindSkin(def.isRedirectOf?.itemid ?? def.itemid, num);
                var dirSkin = def.isRedirectOf == null ? def.skins.FirstOrDefault(x => (ulong)x.id == skinId) : def.isRedirectOf.skins.FirstOrDefault(x => (ulong)x.id == skinId);
                var itemSkin = (dirSkin.id == 0) ? null : (dirSkin.invItem as ItemSkin);

                return itemSkin?.Redirect != null || def.isRedirectOf != null;
            }

            public ulong GetItemSkin(ItemDefinition def, ulong defaultSkin, bool unique)
            {
                ulong skin = defaultSkin;

                if (def.shortname != "explosive.satchel" && def.shortname != "grenade.f1")
                {
                    if (!skins.TryGetValue(def.shortname, out skin)) // apply same skin once randomly chosen so items with skins can stack properly
                    {
                        skin = defaultSkin;
                    }

                    if (!unique || skin == 0)
                    {
                        var si = GetItemSkins(def);
                        var random = new List<ulong>();

                        if (config.Skins.Loot.RandomWorkshopSkins && si.workshopSkins.Count > 0)
                        {
                            random.Add(si.workshopSkins.GetRandom());
                        }

                        if (config.Skins.Loot.Imported && si.importedSkins.Count > 0)
                        {
                            random.Add(si.importedSkins.GetRandom());
                        }

                        if (config.Skins.Loot.RandomSkins && si.skins.Count > 0)
                        {
                            random.Add(si.skins.GetRandom());
                        }

                        if (random.Count != 0)
                        {
                            skins[def.shortname] = skin = random.GetRandom();
                        }
                    }
                }

                return skin;
            }

            public static SkinInfo GetItemSkins(ItemDefinition def)
            {
                SkinInfo si;
                if (!Instance.Skins.TryGetValue(def.shortname, out si))
                {
                    Instance.Skins[def.shortname] = si = new SkinInfo();

                    foreach (var skin in def.skins)
                    {
                        if (IsBlacklistedSkin(def, skin.id))
                        {
                            continue;
                        }

                        var id = Convert.ToUInt64(skin.id);

                        si.skins.Add(id);
                        si.allSkins.Add(id);
                    }

                    if (config.Skins.Loot.Imported)
                    {
                        List<ulong> value;
                        if (Instance.ImportedWorkshopSkins.SkinList.TryGetValue(def.shortname, out value))
                        {
                            foreach (var skin in value)
                            {
                                if (IsBlacklistedSkin(def, (int)skin))
                                {
                                    continue;
                                }

                                si.allSkins.Add(skin);
                                si.importedSkins.Add(skin);
                            }
                        }
                    }

                    if (def.skins2 == null)
                    {
                        return si;
                    }

                    foreach (var skin in def.skins2)
                    {
                        if (IsBlacklistedSkin(def, (int)skin.WorkshopId))
                        {
                            continue;
                        }

                        if (!si.workshopSkins.Contains(skin.WorkshopId))
                        {
                            si.workshopSkins.Add(skin.WorkshopId);
                            si.allSkins.Add(skin.WorkshopId);
                        }
                    }
                }

                return si;
            }

            public bool IsAlly(ulong playerId, ulong targetId, AlliedType type = AlliedType.All)
            {
                if (type == AlliedType.All || type == AlliedType.Team)
                {
                    RelationshipManager.PlayerTeam team;
                    if (RelationshipManager.ServerInstance.playerToTeam.TryGetValue(playerId, out team) && team.members.Contains(targetId))
                    {
                        return true;
                    }
                }

                if ((type == AlliedType.All || type == AlliedType.Clan) && Convert.ToBoolean(Instance.Clans?.Call("IsMemberOrAlly", playerId.ToString(), targetId.ToString())))
                {
                    return true;
                }

                if ((type == AlliedType.All || type == AlliedType.Friend) && Convert.ToBoolean(Instance.Friends?.Call("AreFriends", playerId.ToString(), targetId.ToString())))
                {
                    return true;
                }

                return false;
            }

            public bool IsAlly(BasePlayer player)
            {
                if (!ownerId.IsSteamId() || CanBypass(player) || player.userID == ownerId)
                {
                    return true;
                }

                Raider ri = GetRaider(player);

                if (ri.IsAlly || IsAlly(player.userID, ownerId))
                {
                    ri.IsAlly = true;
                    return true;
                }

                return false;
            }

            public static void StopUsingWand(BasePlayer player)
            {
                if (!config.Settings.NoWizardry || !Instance.Wizardry.CanCall())
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
                    Message(player, "TooPowerfulDrop");
                }
                else Message(player, "TooPowerful");
            }

            public BackpackData AddBackpack(DroppedItemContainer backpack, BasePlayer player)
            {
                BackpackData data;
                if (!backpacks.TryGetValue(backpack.net.ID, out data))
                {
                    backpacks[backpack.net.ID] = data = new BackpackData
                    {
                        backpack = backpack,
                        player = player,
                        userID = backpack.playerSteamID
                    };
                }

                return data;
            }

            private void RemoveParentFromEntitiesOnElevators()
            {
                var entities = FindEntitiesOfType<BaseEntity>(Location, ProtectionRadius);
                for (int i = 0; i < entities.Count; i++)
                {
                    var e = entities[i];
                    if ((e is PlayerCorpse || e is DroppedItemContainer) && e.HasParent())
                    {
                        e.SetParent(null, false, true);
                    }
                }
            }

            public bool EjectBackpack(uint key, BackpackData data, bool bypass)
            {
                if (data.backpack.IsKilled())
                {
                    return true;
                }

                if (!bypass && (!ownerId.IsSteamId() || Any(data.userID) || data.player.IsReallyValid() && IsAlly(data.player)))
                {
                    return false;
                }

                var position = GetEjectLocation(data.backpack.transform.position, 5f, Location, ProtectionRadius);

                position.y = Mathf.Max(position.y, TerrainMeta.WaterMap.GetHeight(position));
                data.backpack.transform.position = position;
                data.backpack.TransformChanged();

                var player = data.player;

                if (player.IsReallyConnected())
                {
                    if (config.Settings.Management.DrawTime <= 0)
                    {
                        Message(player, "YourCorpse");
                        return true;
                    }

                    bool isAdmin = player.IsAdmin;
                    string message = mx("YourCorpse", player.UserIDString);

                    EjectionNotice(player);

                    try
                    {
                        ToggleAdminFlag(player, isAdmin, true);

                        player.SendConsoleCommand("ddraw.text", config.Settings.Management.DrawTime, Color.red, data.backpack.transform.position, message);
                    }
                    catch (Exception ex)
                    {
                        Puts("{0}", ex);
                    }
                    finally
                    {
                        ToggleAdminFlag(player, isAdmin, false);
                    }
                }

                Interface.CallHook("OnRaidableBaseBackpackEjected", new object[] { data.player, data.userID, data.backpack, Location, AllowPVP, (int)Options.Mode, GetOwner(), GetRaiders(), BaseName });

                return true;
            }

            private static void EjectionNotice(BasePlayer player)
            {
                if (!player.IsReallyConnected())
                {
                    return;
                }
                if (player.IsDead() || player.IsSleeping())
                {
                    player.Invoke(() => EjectionNotice(player), 1f);
                    return;
                }
                QueueNotification(player, "EjectedYourCorpse");
            }

            private void EjectSleepers()
            {
                if (!config.Settings.Management.EjectSleepers || Type == RaidableType.None)
                {
                    return;
                }

                foreach (var player in FindEntitiesOfType<BasePlayer>(Location, ProtectionRadius, Layers.Mask.Player_Server))
                {
                    if (player.IsSleeping() && !player.IsBuildingAuthed())
                    {
                        RemovePlayer(player, EjectCode.Sleeper, Location, ProtectionRadius, Type);
                    }
                }
            }

            public static Vector3 GetEjectLocation(Vector3 a, float distance, Vector3 target, float radius)
            {
                var position = ((a.XZ3D() - target.XZ3D()).normalized * (radius + distance)) + target; // credits ZoneManager
                float y = TerrainMeta.HighestPoint.y + 250f;

                RaycastHit hit;
                if (Physics.Raycast(position + new Vector3(0f, y, 0f), Vector3.down, out hit, Mathf.Infinity, ejectMask, QueryTriggerInteraction.Ignore))
                {
                    position.y = hit.point.y + 0.75f;
                }
                else position.y = Mathf.Max(TerrainMeta.HeightMap.GetHeight(position), TerrainMeta.WaterMap.GetHeight(position)) + 0.75f;

                return position;
            }

            public static bool RemovePlayer(BasePlayer player, EjectCode code, Vector3 a, float radius, RaidableType type)
            {
                if (!player.IsHuman() || type == RaidableType.None && !player.IsSleeping())
                {
                    return false;
                }

                var m = player.GetMounted();

                if (m.IsReallyValid())
                {
                    var players = GetMountedPlayers(m);

                    players.RemoveAll(x => x.IsNull() || !x.IsHuman());

                    return EjectMountable(m, code, players, a, radius);
                }

                var position = GetEjectLocation(player.transform.position, 10f, a, radius);

                if (player.IsFlying)
                {
                    position.y = player.transform.position.y;
                }

                player.Teleport(position);
                player.SendNetworkUpdateImmediate();

                PrintDebugMessage($"Ejected {player.displayName} ({player.userID}) with code {code} at {a} in {PositionToGrid(a)}");

                return true;
            }

            public void DismountAllPlayers(BaseMountable m)
            {
                foreach (var target in GetMountedPlayers(m))
                {
                    if (target.IsNull()) continue;

                    m.DismountPlayer(target, false);

                    target.EnsureDismounted();
                }
            }

            public static List<BasePlayer> GetMountedPlayers(HotAirBalloon m)
            {
                var players = FindEntitiesOfType<BasePlayer>(m.CenterPoint(), 1.75f, Layers.Mask.Player_Server);
                players.RemoveAll(player => !player.IsHuman() || !(player.GetParentEntity() == m));
                return players;
            }

            public static List<BasePlayer> GetMountedPlayers(BaseMountable m)
            {
                BaseVehicle vehicle = m.HasParent() ? m.VehicleParent() : m as BaseVehicle;

                if (vehicle.IsReallyValid())
                {
                    return GetMountedPlayers(vehicle);
                }

                var players = new List<BasePlayer>();
                var player = m.GetMounted();

                if (player.IsReallyValid() && player.IsHuman())
                {
                    players.Add(player);
                }

                return players;
            }

            private static List<BasePlayer> GetMountedPlayers(BaseVehicle vehicle)
            {
                var players = new List<BasePlayer>();

                if (!vehicle.HasMountPoints())
                {
                    var player = vehicle.GetMounted();

                    if (player?.IsHuman() == true && !players.Contains(player))
                    {
                        players.Add(player);
                    }

                    return players;
                }

                foreach (var mountPoint in vehicle.allMountPoints)
                {
                    var player = mountPoint.mountable?.GetMounted();

                    if (player?.IsHuman() == true && !players.Contains(player))
                    {
                        players.Add(player);
                    }
                }

                return players;
            }

            private bool CanEject(List<BasePlayer> players)
            {
                return players.Exists(player => !intruders.Contains(player.userID) && CanEject(player));
            }

            private bool CanEject(BasePlayer target)
            {
                if (target.IsNull() || target == owner)
                {
                    return false;
                }

                if (CannotEnter(target, false))
                {
                    return true;
                }
                else if (CanEject() && !IsAlly(target))
                {
                    TryMessage(target, "OnPlayerEntryRejected");
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

            private bool CannotEnter(BasePlayer target, bool justEntered)
            {
                if (GetRaider(target).IsAllowed)
                {
                    return IsBanned(target);
                }

                EjectCode code = EjectCode.None;

                if (Exceeds(target)) code = EjectCode.Exceeds;
                else if (HasLockout(target)) code = EjectCode.Lockout;
                else if (IsBanned(target)) code = EjectCode.Banned;
                else if (IsHogging(target)) code = EjectCode.Hogging;
                else if (justEntered && Teleported(target)) code = EjectCode.Teleported;

                if (code != EjectCode.None)
                {
                    return RemovePlayer(target, code, Location, ProtectionRadius, Type);
                }

                return false;
            }

            public bool IsControlledMount(BaseEntity m)
            {
                if (!config.Settings.Management.Mounts.ControlledMounts)
                {
                    return false;
                }

                if (m is BaseChair)
                {
                    DismountAllPlayers(m as BaseMountable);

                    return true;
                }

                var parentEntity = m.GetParentEntity();

                if (parentEntity.IsNull() || parentEntity is RidableHorse)
                {
                    return false;
                }

                if (parentEntity.GetType().Name.Contains("Controller"))
                {
                    DismountAllPlayers(m as BaseMountable);

                    return true;
                }

                return false;
            }

            private bool TryRemoveMountable(BaseEntity m, List<BasePlayer> players)
            {
                if (m.IsNull() || Type == RaidableType.None || m.GetParentEntity() is TrainCar || IsControlledMount(m) || Entities.Contains(m))
                {
                    return false;
                }

                bool shouldEject = config.Settings.Management.Mounts.Other;

                if (CanEject(players))
                {
                    shouldEject = true;
                }
                else if (m is BaseBoat)
                {
                    shouldEject = config.Settings.Management.Mounts.Boats;
                }
                else if (m is BasicCar)
                {
                    shouldEject = config.Settings.Management.Mounts.BasicCars;
                }
                else if (m is ModularCar)
                {
                    shouldEject = config.Settings.Management.Mounts.ModularCars;
                }
                else if (m is CH47Helicopter)
                {
                    shouldEject = config.Settings.Management.Mounts.CH47;
                }
                else if (m is RidableHorse)
                {
                    shouldEject = config.Settings.Management.Mounts.Horses;
                }
                else if (m is ScrapTransportHelicopter)
                {
                    shouldEject = config.Settings.Management.Mounts.Scrap;
                }
                else if (m is MiniCopter && !(m is ScrapTransportHelicopter))
                {
                    shouldEject = config.Settings.Management.Mounts.MiniCopters;
                }
                else if (m is Snowmobile)
                {
                    shouldEject = config.Settings.Management.Mounts.Snowmobile;
                }
                else if (m is StaticInstrument)
                {
                    shouldEject = config.Settings.Management.Mounts.Pianos;
                }
                else if (m is HotAirBalloon)
                {
                    if (config.Settings.Management.Mounts.HotAirBalloon)
                    {
                        return EjectMountable(m as HotAirBalloon, EjectCode.Mounted, players, Location, ProtectionRadius);
                    }
                }
                else if (IsJetpack(m))
                {
                    shouldEject = config.Settings.Management.Mounts.Jetpacks;
                }

                if (shouldEject && m is BaseMountable)
                {
                    return EjectMountable(m as BaseMountable, EjectCode.Mounted, players, Location, ProtectionRadius);
                }

                return false;
            }

            private bool IsJetpack(BaseEntity m)
            {
                return m.ShortPrefabName == "standingdriver" && m.HasParent() && m.GetParentEntity() is DroppedItemContainer;
            }

            private static void TryPushMountable(BaseVehicle vehicle, Vector3 target)
            {
                if (vehicle is MiniCopter || vehicle is CH47Helicopter || vehicle.name.Contains("modularcar"))
                {
                    var body = vehicle.rigidBody ?? vehicle.GetComponent<Rigidbody>();
                    Vector3 normalized = Vector3.ProjectOnPlane(vehicle.transform.position - target, vehicle.transform.up).normalized;
                    float d2 = body.mass * 4f;
                    body.AddForce(-normalized * d2, ForceMode.Impulse);
                }
                else
                {
                    var e = vehicle.transform.eulerAngles; // credits k1lly0u

                    vehicle.transform.rotation = Quaternion.Euler(e.x, e.y - 180f, e.z);
                    var body = vehicle.rigidBody ?? vehicle.GetComponent<Rigidbody>();
                    if (body != null)
                    {
                        body.velocity *= -1f;
                    }
                }
            }

            private static bool IsFlying(BasePlayer player)
            {
                return !player.IsKilled() && !player.modelState.onground && TerrainMeta.HeightMap.GetHeight(player.transform.position) < player.transform.position.y - 1f;
            }

            public static bool EjectMountable(HotAirBalloon m, EjectCode code, List<BasePlayer> players, Vector3 position, float radius)
            {
                var j = TerrainMeta.HeightMap.GetHeight(m.transform.position) - m.transform.position.y;
                var target = ((m.transform.position.XZ3D() - position.XZ3D()).normalized * (radius + 10f)) + position;

                target.y = Mathf.Max(m.transform.position.y, SpawnsController.GetSpawnHeight(target)) + 5f;

                m.transform.position = target;

                return true;
            }

            public static bool EjectMountable(BaseMountable m, EjectCode code, List<BasePlayer> players, Vector3 position, float radius)
            {
                m = GetParentMountableEntity(m);

                var j = TerrainMeta.HeightMap.GetHeight(m.transform.position) - m.transform.position.y;
                float distance = m is RidableHorse ? 2f : 10f;

                if (j > 5f)
                {
                    distance += j;
                }

                var target = ((m.transform.position.XZ3D() - position.XZ3D()).normalized * (radius + distance)) + position;

                if (m is MiniCopter || m is CH47Helicopter || players.Exists(player => IsFlying(player)))
                {
                    target.y = Mathf.Max(m.transform.position.y, SpawnsController.GetSpawnHeight(target)) + 5f;
                }
                else target.y = SpawnsController.GetSpawnHeight(target) + 0.1f;

                BaseVehicle vehicle = m.HasParent() ? m.VehicleParent() : m as BaseVehicle;

                if (m is RidableHorse || InRange(m.transform.position, position, radius + 1f))
                {
                    m.transform.position = target;
                }
                else if (!vehicle.IsNull())
                {
                    TryPushMountable(vehicle, target);
                }
                else m.transform.position = target;

                return true;
            }

            private static BaseMountable GetParentMountableEntity(BaseMountable m)
            {
                while (m != null && m.HasParent())
                {
                    var parent = m.GetParentEntity() as BaseMountable;
                    if (parent == null) break;
                    m = parent;
                }

                return m;
            }

            public bool CanSetupEntity(BaseEntity e)
            {
                if (e.IsKilled())
                {
                    Entities.Remove(e);
                    return false;
                }

                if (e.net == null)
                {
                    e.net = Net.sv.CreateNetworkable();
                }

                if (e is StaticInstrument)
                {
                    data.TemporaryUID.Remove(e.net.ID);
                    Entities.Remove(e);
                    e.Invoke(e.SafelyKill, 0.01f);
                    return false;
                }

                return true;
            }

            public void TryRespawnNpc()
            {
                if ((!IsOpened && !Options.Levels.Level2) || IsInvoking(RespawnNpcNow))
                {
                    return;
                }

                if (Options.RespawnRateMin > 0)
                {
                    Invoke(RespawnNpcNow, UnityEngine.Random.Range(Options.RespawnRateMin, Options.RespawnRateMax));
                }
                else Invoke(RespawnNpcNow, Options.RespawnRateMax);
            }

            private void RespawnNpcNow()
            {
                if (npcs.Count >= npcMaxAmount)
                {
                    return;
                }

                var npc = SpawnNpc(!Options.NPC.SpawnScientistsOnly && (Options.NPC.SpawnBoth ? UnityEngine.Random.value > 0.5f : Options.NPC.SpawnMurderers));

                if (npc.IsNull() || npcs.Count >= npcMaxAmount)
                {
                    return;
                }

                TryRespawnNpc();
            }

            public void SpawnNpcs()
            {
                if (!Options.NPC.Enabled || (Options.NPC.UseExpansionNpcs && config.Settings.ExpansionMode && Instance.DangerousTreasures.CanCall()))
                {
                    return;
                }

                for (int i = 0; i < npcMaxAmount; i++)
                {
                    if (npcs.Count >= npcMaxAmount)
                    {
                        break;
                    }

                    SpawnNpc(!Options.NPC.SpawnScientistsOnly && (Options.NPC.SpawnBoth ? UnityEngine.Random.value >= 0.5f : Options.NPC.SpawnMurderers));
                }
            }

            public bool IsInForwardOperatingBase(Vector3 from)
            {
                return BuiltList.Count > 0 && BuiltList.Values.Exists(to => InRange2D(from, to, 3f));
            }

            public bool NearFoundation(Vector3 from, float range = 5f)
            {
                return foundations.Exists(to => InRange2D(from, to, range));
            }

            private NavMeshHit _navHit;

            public Vector3 FindPointOnNavmesh(Vector3 target, float radius)
            {
                int tries = 25;

                while (--tries > 0)
                {
                    if (NavMesh.SamplePosition(target, out _navHit, radius, NavMesh.AllAreas))
                    {
                        if (NearFoundation(_navHit.position) || !IsAcceptableWaterDepth(_navHit.position))
                        {
                            continue;
                        }

                        if (TestInsideRock(_navHit.position) || TestInsideObject(_navHit.position) || IsNpcNearSpot(_navHit.position))
                        {
                            continue;
                        }

                        return _navHit.position;
                    }
                }

                return Vector3.zero;
            }

            private bool IsAcceptableWaterDepth(Vector3 position)
            {
                return WaterLevel.GetOverallWaterDepth(position, true, null, false) <= config.Settings.Management.WaterDepth;
            }

            private bool TestInsideObject(Vector3 position)
            {
                return GamePhysics.CheckSphere(position, 0.5f, Layers.Mask.Player_Server | Layers.Server.Deployed, QueryTriggerInteraction.Ignore);
            }

            private bool TestInsideRock(Vector3 a)
            {
                Physics.queriesHitBackfaces = true;

                bool flag = IsRockFaceUpwards(a);

                Physics.queriesHitBackfaces = false;

                return flag || IsRockFaceDownwards(a);
            }

            private RaycastHit _hit;
            private RaycastHit[] hits;

            private bool IsRockFaceDownwards(Vector3 a)
            {
                Vector3 b = a + new Vector3(0f, 20f, 0f);
                Vector3 d = a - b;
                hits = Physics.RaycastAll(b, d, d.magnitude, Layers.World);
                return hits.Exists(hit => IsRock(hit.collider.ObjectName()));
            }

            private bool IsRockFaceUpwards(Vector3 point)
            {
                if (!Physics.Raycast(point, Vector3.up, out _hit, 20f, Layers.World)) return false;
                return IsRock(_hit.collider.ObjectName());
            }

            private bool IsRock(string name) => _prefabs.Exists(value => name.Contains(value, CompareOptions.OrdinalIgnoreCase));

            private List<string> _prefabs = new List<string> { "rock", "formation", "cliff" };

            private ScientistNPC InstantiateEntity(Vector3 position, bool isStationary, out HumanoidBrain humanoidBrain)
            {
                var prefabName = StringPool.Get(1536035819);
                var prefab = GameManager.server.FindPrefab(prefabName);
                var go = Facepunch.Instantiate.GameObject(prefab, position, Quaternion.identity);

                go.SetActive(false);

                go.name = prefabName;

                ScientistBrain scientistBrain = go.GetComponent<ScientistBrain>();
                ScientistNPC npc = go.GetComponent<ScientistNPC>();

                humanoidBrain = go.AddComponent<HumanoidBrain>();
                humanoidBrain.DestinationOverride = position;
                humanoidBrain.CheckLOS = humanoidBrain.RefreshKnownLOS = true;
                humanoidBrain.SenseRange = Options.NPC.AggressionRange;
                humanoidBrain.softLimitSenseRange = humanoidBrain.SenseRange + (humanoidBrain.SenseRange * 0.25f);
                humanoidBrain.TargetLostRange = humanoidBrain.SenseRange * 1.25f;
                humanoidBrain.Settings = Options.NPC;
                humanoidBrain.isStationary = isStationary;
                humanoidBrain.UseAIDesign = false;
                humanoidBrain._baseEntity = npc;
                humanoidBrain.raid = this;
                humanoidBrain.npc = npc;

                DestroyImmediate(scientistBrain, true);

                SceneManager.MoveGameObjectToScene(go, Rust.Server.EntityScene);

                go.SetActive(true); //go.AwakeFromInstantiate();

                return npc;
            }

            private Vector3 RandomPosition(float radius)
            {
                return RandomWanderPositions(Options.ArenaWalls.Radius * 0.9f).FirstOrDefault();
            }

            private List<Vector3> RandomWanderPositions(float radius)
            {
                var list = new List<Vector3>();

                for (int i = 0; i < 10; i++)
                {
                    var target = GetRandomPoint(radius);
                    var vector = FindPointOnNavmesh(target, radius);

                    if (vector != Vector3.zero)
                    {
                        list.Add(vector);
                    }
                }

                return list;
            }

            private Vector3 GetRandomPoint(float radius)
            {
                var vector = Location + UnityEngine.Random.onUnitSphere * radius;

                if (Options.Setup.ForcedHeight == -1)
                {
                    vector.y = TerrainMeta.HeightMap.GetHeight(vector);
                }

                return vector;
            }

            private ScientistNPC SpawnNpc(bool isMurderer)
            {
                bool isStationary = false;
                Vector3 position = Vector3.zero;

                if (isMurderer)
                {
                    if (!Options.NPC.Inside.SpawnMurderersOutside)
                    {
                        return null;
                    }
                }
                else
                {
                    if (Options.NPC.Inside.SpawnOnRugs || Options.NPC.Inside.SpawnOnBeds || Options.NPC.Inside.SpawnOnFloors)
                    {
                        position = SpawnInsideBase();
                    }

                    if (!Options.NPC.Inside.SpawnScientistsOutside && position == Vector3.zero)
                    {
                        return null;
                    }
                }

                var positions = RandomWanderPositions(ProtectionRadius * 0.9f);

                if (positions.Count == 0)
                {
                    return null;
                }

                if (position == Vector3.zero)
                {
                    position = RandomPosition(Options.ArenaWalls.Radius * 0.9f);
                }
                else isStationary = true;

                if (position == Vector3.zero)
                {
                    return null;
                }

                HumanoidBrain brain;
                ScientistNPC npc = InstantiateEntity(position, isStationary, out brain);

                if (npc.IsNull())
                {
                    return null;
                }

                if (isStationary)
                {
                    npcAmountInside++;
                    isMurderer = false;
                }

                npc.userID = (ulong)UnityEngine.Random.Range(0, 10000000);
                npc.UserIDString = npc.userID.ToString();
                npc.displayName = Options.NPC.RandomNames.Count > 0 ? Options.NPC.RandomNames.GetRandom() : RandomUsernames.Get(npc.userID);

                var loadout = GetLoadout(npc, brain, isMurderer);

                if (loadout.belt.Count > 0 || loadout.main.Count > 0 || loadout.wear.Count > 0)
                {
                    npc.loadouts = new PlayerInventoryProperties[1];
                    npc.loadouts[0] = loadout;
                }
                else npc.loadouts = new PlayerInventoryProperties[0];

                BasePlayer.bots.Add(npc);

                Instance.Npcs[npc.userID] = this;

                npc.Spawn();
                npc.CancelInvoke(npc.EquipTest);

                npcs.Add(npc);

                SetupNpc(npc, brain, isMurderer, positions);

                return npc;
            }

            public class Loadout
            {
                public List<PlayerInventoryProperties.ItemAmountSkinned> belt = new List<PlayerInventoryProperties.ItemAmountSkinned>();
                public List<PlayerInventoryProperties.ItemAmountSkinned> main = new List<PlayerInventoryProperties.ItemAmountSkinned>();
                public List<PlayerInventoryProperties.ItemAmountSkinned> wear = new List<PlayerInventoryProperties.ItemAmountSkinned>();
            }

            private PlayerInventoryProperties GetLoadout(ScientistNPC npc, HumanoidBrain brain, bool isMurderer)
            {
                var loadout = CreateLoadout(npc, brain, isMurderer);
                var pip = ScriptableObject.CreateInstance<PlayerInventoryProperties>();

                pip.belt = loadout.belt;
                pip.main = loadout.main;
                pip.wear = loadout.wear;

                return pip;
            }

            private Loadout CreateLoadout(ScientistNPC npc, HumanoidBrain brain, bool isMurderer)
            {
                var loadout = new Loadout();

                switch (isMurderer)
                {
                    case true:
                        AddItemAmountSkinned(loadout.wear, Options.NPC.MurdererItems.Boots);
                        AddItemAmountSkinned(loadout.wear, Options.NPC.MurdererItems.Gloves);
                        AddItemAmountSkinned(loadout.wear, Options.NPC.MurdererItems.Helm);
                        AddItemAmountSkinned(loadout.wear, Options.NPC.MurdererItems.Pants);
                        AddItemAmountSkinned(loadout.wear, Options.NPC.MurdererItems.Shirt);
                        AddItemAmountSkinned(loadout.wear, Options.NPC.MurdererItems.Torso);
                        if (!Options.NPC.MurdererItems.Torso.Exists(v => v.Contains("suit")))
                        {
                            AddItemAmountSkinned(loadout.wear, Options.NPC.MurdererItems.Kilts);
                        }
                        AddItemAmountSkinned(loadout.belt, Options.NPC.MurdererItems.Weapon);
                        break;
                    case false:
                        AddItemAmountSkinned(loadout.wear, Options.NPC.ScientistItems.Boots);
                        AddItemAmountSkinned(loadout.wear, Options.NPC.ScientistItems.Gloves);
                        AddItemAmountSkinned(loadout.wear, Options.NPC.ScientistItems.Helm);
                        AddItemAmountSkinned(loadout.wear, Options.NPC.ScientistItems.Pants);
                        AddItemAmountSkinned(loadout.wear, Options.NPC.ScientistItems.Shirt);
                        AddItemAmountSkinned(loadout.wear, Options.NPC.ScientistItems.Torso);
                        if (!Options.NPC.ScientistItems.Torso.Exists(v => v.Contains("suit")))
                        {
                            AddItemAmountSkinned(loadout.wear, Options.NPC.ScientistItems.Kilts);
                        }
                        AddItemAmountSkinned(loadout.belt, Options.NPC.ScientistItems.Weapon);
                        break;
                }

                return loadout;
            }

            private void AddItemAmountSkinned(List<PlayerInventoryProperties.ItemAmountSkinned> source, List<string> shortnames)
            {
                if (shortnames.Count == 0)
                {
                    return;
                }

                string shortname = shortnames.GetRandom();

                if (shortname == "bow.hunting")
                {
                    shortname = "bow.compound";
                }

                ItemDefinition def = ItemManager.FindItemDefinition(shortname);

                if (def == null)
                {
                    Puts("Invalid shortname for npc item in {0}/{1} profile: {2}", ProfileName, BaseName, shortname);
                    return;
                }

                bool isThrownWeapon = def.GetComponent<ItemModEntity>()?.entityPrefab.Get().GetComponent<ThrownWeapon>().IsNull() == false;

                if (isThrownWeapon)
                {
                    if (npcAmountThrown >= Options.NPC.Thrown)
                    {
                        shortnames.Remove(shortname);
                        AddItemAmountSkinned(source, shortnames);
                        return;
                    }
                    else npcAmountThrown++;
                }

                ulong skin = 0uL;
                if (config.Skins.Npcs)
                {
                    skin = GetItemSkin(def, 0uL, config.Skins.UniqueNpcs);
                }

                source.Add(new PlayerInventoryProperties.ItemAmountSkinned
                {
                    amount = 1,
                    itemDef = def,
                    skinOverride = skin,
                    startAmount = 1
                });
            }

            private void SetupNpc(ScientistNPC npc, HumanoidBrain brain, bool isMurderer, List<Vector3> positions)
            {
                if (Options.NPC.DespawnInventory)
                {
                    npc.LootSpawnSlots = new LootContainer.LootSpawnSlot[0];
                }

                if (Options.NPC.AlternateScientistLoot.Enabled && Options.NPC.AlternateScientistLoot.IDs.Count > 0)
                {
                    var id = Options.NPC.AlternateScientistLoot.GetRandom();
                    var lootSpawnSlots = GameManager.server.FindPrefab(StringPool.Get(id))?.GetComponent<ScientistNPC>()?.LootSpawnSlots;

                    if (lootSpawnSlots != null)
                    {
                        npc.LootSpawnSlots = lootSpawnSlots;
                    }
                }

                npc.CancelInvoke(npc.PlayRadioChatter);
                npc.DeathEffects = new GameObjectRef[0];
                npc.RadioChatterEffects = new GameObjectRef[0];
                npc.radioChatterType = ScientistNPC.RadioChatterType.NONE;
                npc.startHealth = isMurderer ? Options.NPC.MurdererHealth : Options.NPC.ScientistHealth;
                npc.InitializeHealth(npc.startHealth, npc.startHealth);
                npc.Invoke(() => UpdateItems(npc, brain, isMurderer), 0.2f);
                npc.Invoke(() => brain.SetupMovement(positions), 0.3f);
            }

            private void UpdateItems(ScientistNPC npc, HumanoidBrain brain, bool isMurderer)
            {
                List<string> kits;
                if (npcKits.TryGetValue(isMurderer ? "murderer" : "scientist", out kits) && kits.Count > 0)
                {
                    npc.inventory.Strip();

                    Instance.Kits?.Call("GiveKit", npc, kits.GetRandom());
                }

                EquipWeapon(npc, brain);

                if (!ToggleNpcMinerHat(npc, TOD_Sky.Instance?.IsNight == true))
                {
                    npc.inventory.ServerUpdate(0f);
                }
            }

            public void EquipWeapon(ScientistNPC npc, HumanoidBrain brain)
            {
                foreach (Item item in npc.inventory.AllItems())
                {
                    var e = item.GetHeldEntity() as HeldEntity;

                    if (e.IsReallyValid())
                    {
                        if (item.skin != 0)
                        {
                            e.skinID = item.skin;
                            e.SendNetworkUpdate();
                        }

                        if (!brain.AttackEntity.IsNull() || item.info.shortname == "syringe.medical")
                        {
                            continue;
                        }

                        var weapon = e as BaseProjectile;

                        if (weapon.IsReallyValid())
                        {
                            weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
                            weapon.SendNetworkUpdateImmediate();
                        }

                        if (e is AttackEntity && item.GetRootContainer() == npc.inventory.containerBelt)
                        {
                            var attackEntity = e as AttackEntity;

                            if (attackEntity.hostile || item.info.shortname == "syringe.medical")
                            {
                                UpdateWeapon(npc, brain, attackEntity, item);
                            }
                        }
                    }

                    item.MarkDirty();
                }
            }

            private void UpdateWeapon(ScientistNPC npc, HumanoidBrain brain, AttackEntity attackEntity, Item item)
            {
                npc.UpdateActiveItem(item.uid);

                if (attackEntity is Chainsaw)
                {
                    (attackEntity as Chainsaw).ServerNPCStart();
                }

                npc.damageScale = 1f;

                attackEntity.TopUpAmmo();
                attackEntity.SetHeld(true);
                brain.Init();
            }

            private void SortRandomSpots()
            {
                int layers = Layers.Mask.Construction | Layers.Mask.Deployed | Layers.Mask.Player_Server;

                blocks.RemoveAll(block => block.IsKilled() || (!Options.NPC.Roofcampers && IsOutside(block) && (block.ShortPrefabName == "floor.triangle" || block.ShortPrefabName == "floor" || block.ShortPrefabName.Contains("roof"))));

                List<BuildingBlock> remove = new List<BuildingBlock>();

                for (int j = 0; j < blocks.Count; j++)
                {
                    for (int k = blocks.Count - 1; k >= 0; k--)
                    {
                        Vector3 a = blocks[j].transform.position;
                        Vector3 b = blocks[k].transform.position;

                        if (a.y > b.y && Vector3.Distance(a, b) < 3f)
                        {
                            remove.Add(blocks[k]);
                        }
                    }
                }

                blocks.RemoveAll(remove.Contains);

                for (int i = 0; i < blocks.Count; i++)
                {
                    int walls = 0;
                    var block = blocks[i];
                    var destination = block.transform.position + Vector3.up * 1.25f;
                    var entities = FindEntitiesOfType<BaseEntity>(destination, 1.5f, layers);

                    for (int n = 0; n < entities.Count; n++)
                    {
                        var e = entities[n];
                        if (e.IsDestroyed || e == block || e is Door || e.ShortPrefabName.StartsWith("rug"))
                        {
                            continue;
                        }
                        else if (e.ShortPrefabName == "wall")
                        {
                            walls++;
                        }
                        else if (!(e is BuildingBlock) || e.ShortPrefabName.Contains("roof"))
                        {
                            walls = int.MaxValue;
                            break;
                        }
                    }

                    if (walls < 3 && block.ShortPrefabName == "foundation.triangle" || walls < 4 && block.ShortPrefabName == "foundation")
                    {
                        _randomSpots.Add(block.CenterPoint() + new Vector3(0f, (block.bounds.size.Max() / 2f) + 0.1125f, 0f));
                    }
                    else if (walls < 3 && block.ShortPrefabName == "floor.triangle" || walls < 4 && block.ShortPrefabName == "floor")
                    {
                        _randomSpots.Add(block.CenterPoint() + new Vector3(0f, 0.155f, 0f));
                    }
                }

                blocks.Clear();
            }

            private bool IsOutside(BaseEntity entity)
            {
                OBB oBB = entity.WorldSpaceBounds();

                return entity.IsOutside(oBB.position.WithY(entity.transform.position.y));
            }

            private Vector3 FindRandomRug()
            {
                if (Options.NPC.Inside.SpawnOnRugs)
                {
                    _rugs.RemoveAll(x => x.IsKilled());

                    foreach (var rug in _rugs)
                    {
                        if (!IsNpcNearSpot(rug.transform.position))
                        {
                            return rug.transform.position;
                        }
                    }
                }

                return Vector3.zero;
            }

            private Vector3 FindRandomBed()
            {
                if (Options.NPC.Inside.SpawnOnBeds)
                {
                    _beds.RemoveAll(x => x.IsKilled());

                    foreach (var bed in _beds)
                    {
                        if (!IsNpcNearSpot(bed.transform.position))
                        {
                            return bed.transform.position;
                        }
                    }
                }

                return Vector3.zero;
            }

            private Vector3 FindRandomFloor()
            {
                if (Options.NPC.Inside.SpawnOnFloors)
                {
                    if (blocks.Count > 0)
                    {
                        SortRandomSpots();
                    }

                    Shuffle(_randomSpots);
                    _beds.RemoveAll(x => x.IsKilled());
                    _decorDeployables.RemoveAll(x => x.IsKilled());

                    foreach (var position in _randomSpots)
                    {
                        if (Options.NPC.Inside.SpawnOnRugs && _decorDeployables.Exists(x => x.ShortPrefabName.StartsWith("rug") && InRange(x.transform.position, position, 1f)))
                        {
                            continue;
                        }

                        if (Options.NPC.Inside.SpawnOnBeds && _beds.Exists(x => InRange(x.transform.position, position, 1f)))
                        {
                            continue;
                        }

                        if (IsNpcNearSpot(position))
                        {
                            continue;
                        }

                        return position;
                    }
                }

                return Vector3.zero;
            }

            private bool IsNpcNearSpot(Vector3 position)
            {
                return npcs.Exists(npc => !npc.IsKilled() && InRange2D(npc.transform.position, position, 1f));
            }

            public Vector3 SpawnInsideBase()
            {
                if (npcMaxAmountInside == -1)
                {
                    npcMaxAmountInside = npcMaxAmount;
                }

                if (npcAmountInside >= npcMaxAmountInside)
                {
                    return Vector3.zero;
                }

                var position = FindRandomRug();

                if (position == Vector3.zero)
                {
                    position = FindRandomBed();
                }

                if (position == Vector3.zero)
                {
                    position = FindRandomFloor();
                }

                return position;
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
                return Convert.ToBoolean(Instance.Kits?.Call("isKit", kit));
            }

            public static void UpdateAllMarkers()
            {
                foreach (var raid in Instance.Raids)
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

                if (!genericMarker.IsKilled())
                {
                    genericMarker.SendUpdate();
                }

                if (!explosionMarker.IsKilled())
                {
                    explosionMarker.transform.position = Location;
                    explosionMarker.SendNetworkUpdate();
                }

                if (!vendingMarker.IsKilled())
                {
                    vendingMarker.transform.position = Location;
                    float seconds = despawnTime - Time.time;
                    string despawnText = config.Settings.Management.DespawnMinutesInactive > 0 && seconds > 0 ? string.Format(" [{0}m]", Math.Floor(TimeSpan.FromSeconds(seconds).TotalMinutes)) : null;
                    string flag = mx(AllowPVP ? "PVPFlag" : "PVEFlag");

                    vendingMarker.markerShopName = (markerName == config.Settings.Markers.MarkerName ? mx("MapMarkerOrderWithMode", null, flag, Mode(), markerName, despawnText) : string.Format("{0} {1}", flag, markerName)).Trim();
                    vendingMarker.SendNetworkUpdate();
                }

                if (markerCreated || !IsMarkerAllowed())
                {
                    return;
                }

                if (config.Settings.Markers.UseExplosionMarker)
                {
                    explosionMarker = GameManager.server.CreateEntity(StringPool.Get(4060989661), Location) as MapMarkerExplosion;

                    if (!explosionMarker.IsNull())
                    {
                        explosionMarker.Spawn();
                        explosionMarker.Invoke(() => explosionMarker.CancelInvoke(explosionMarker.DelayedDestroy), 1f);
                    }
                }
                else if (config.Settings.Markers.UseVendingMarker)
                {
                    vendingMarker = GameManager.server.CreateEntity(StringPool.Get(3459945130), Location) as VendingMachineMapMarker;

                    if (!vendingMarker.IsNull())
                    {
                        string flag = mx(AllowPVP ? "PVPFlag" : "PVEFlag");
                        string despawnText = config.Settings.Management.DespawnMinutesInactive > 0 ? string.Format(" [{0}m]", config.Settings.Management.DespawnMinutesInactive.ToString()) : null;

                        if (markerName == config.Settings.Markers.MarkerName)
                        {
                            vendingMarker.markerShopName = mx("MapMarkerOrderWithMode", null, flag, Mode(), markerName, despawnText);
                        }
                        else vendingMarker.markerShopName = mx("MapMarkerOrderWithoutMode", null, flag, markerName, despawnText);

                        vendingMarker.enabled = false;
                        vendingMarker.Spawn();
                    }
                }

                markerCreated = true;
            }

            private void CreateGenericMarker()
            {
                if (IsMarkerAllowed() && (config.Settings.Markers.UseExplosionMarker || config.Settings.Markers.UseVendingMarker))
                {
                    genericMarker = GameManager.server.CreateEntity(StringPool.Get(2849728229), Location) as MapMarkerGenericRadius;

                    if (!genericMarker.IsNull())
                    {
                        genericMarker.alpha = 0.75f;
                        genericMarker.color1 = GetMarkerColor1();
                        genericMarker.color2 = GetMarkerColor2();
                        genericMarker.radius = Mathf.Min(2.5f, config.Settings.Markers.Radius);
                        genericMarker.Spawn();
                        genericMarker.SendUpdate();
                    }
                }
            }

            private bool TryParseHtmlString(string value, out Color color)
            {
                if (!value.StartsWith("#"))
                {
                    value = $"#{value}";
                }

                return ColorUtility.TryParseHtmlString(value, out color);
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
                        if (TryParseHtmlString(config.Settings.Management.Colors1.Easy, out color))
                        {
                            return color;
                        }
                        return Color.green;
                    case RaidableMode.Medium:
                        if (TryParseHtmlString(config.Settings.Management.Colors1.Medium, out color))
                        {
                            return color;
                        }
                        return Color.yellow;
                    case RaidableMode.Hard:
                        if (TryParseHtmlString(config.Settings.Management.Colors1.Hard, out color))
                        {
                            return color;
                        }
                        return Color.red;
                    case RaidableMode.Expert:
                        if (TryParseHtmlString(config.Settings.Management.Colors1.Expert, out color))
                        {
                            return color;
                        }
                        return Color.blue;
                    case RaidableMode.Nightmare:
                    default:
                        if (TryParseHtmlString(config.Settings.Management.Colors1.Nightmare, out color))
                        {
                            return color;
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
                        if (TryParseHtmlString(config.Settings.Management.Colors2.Easy, out color))
                        {
                            return color;
                        }
                        return Color.green;
                    case RaidableMode.Medium:
                        if (TryParseHtmlString(config.Settings.Management.Colors2.Medium, out color))
                        {
                            return color;
                        }
                        return Color.yellow;
                    case RaidableMode.Hard:
                        if (TryParseHtmlString(config.Settings.Management.Colors2.Hard, out color))
                        {
                            return color;
                        }
                        return Color.red;
                    case RaidableMode.Expert:
                        if (TryParseHtmlString(config.Settings.Management.Colors2.Expert, out color))
                        {
                            return color;
                        }
                        return Color.blue;
                    case RaidableMode.Nightmare:
                    default:
                        if (TryParseHtmlString(config.Settings.Management.Colors2.Nightmare, out color))
                        {
                            return color;
                        }
                        return Color.black;
                }
            }

            private bool IsMarkerAllowed()
            {
                if (Options.Silent)
                {
                    return false;
                }

                switch (Type)
                {
                    case RaidableType.Grid: case RaidableType.Manual: case RaidableType.None: return config.Settings.Markers.Manual;
                    case RaidableType.Maintained: return config.Settings.Markers.Maintained;
                    case RaidableType.Purchased: return config.Settings.Markers.Buyables;
                    case RaidableType.Scheduled: return config.Settings.Markers.Scheduled;
                }

                return true;
            }

            private void DestroyLocks()
            {
                locks.ForEach(SafelyKill);
            }

            private void DestroyNpcs()
            {
                npcs.ForEach(SafelyKill);
            }

            private void DestroySpheres()
            {
                spheres.ForEach(SafelyKill);
            }

            public void DestroyMapMarkers()
            {
                if (!explosionMarker.IsKilled())
                {
                    explosionMarker.CancelInvoke(explosionMarker.DelayedDestroy);
                    explosionMarker.Kill();
                }

                genericMarker.SafelyKill();
                vendingMarker.SafelyKill();
            }
        }

        public static class SpawnsController
        {
            public static List<MonumentInfoEx> Monuments;
            private static List<ZoneInfo> managedZones;
            private static List<string> assets;
            private static List<string> blockedcolliders;

            public static void Initialize()
            {
                Monuments = new List<MonumentInfoEx>();
                managedZones = new List<ZoneInfo>();
                assets = new List<string> { "/props/", "/structures/", "/building/", "train_", "powerline_", "dune", "candy-cane", "assets/content/nature/", "walkway", "invisible_collider", "train_track" };
                blockedcolliders = new List<string> { "powerline", "invisible", "TopCol" };

                SetupMonuments();
            }

            public static void Clear()
            {
                assets.Clear();
                Monuments.Clear();
                managedZones.Clear();
                blockedcolliders.Clear();
            }

            public static List<Vector3> GetCircumferencePositions(Vector3 center, float radius, float next, bool spawnHeight = true, float y = 0f)
            {
                float degree = 0f;
                float angleInRadians = 2f * Mathf.PI;
                List<Vector3> positions = new List<Vector3>();

                while (degree < 360)
                {
                    float radian = (angleInRadians / 360) * degree;
                    float x = center.x + radius * Mathf.Cos(radian);
                    float z = center.z + radius * Mathf.Sin(radian);
                    Vector3 a = new Vector3(x, y, z);

                    positions.Add(y == 0f ? a.WithY(spawnHeight ? GetSpawnHeight(a) : TerrainMeta.HeightMap.GetHeight(a)) : a);

                    degree += next;
                }

                return positions;
            }

            public static float GetSpawnHeight(Vector3 target, bool flag = true, bool draw = false)
            {
                float y = TerrainMeta.HeightMap.GetHeight(target);
                float w = TerrainMeta.WaterMap.GetHeight(target);
                float p = TerrainMeta.HighestPoint.y + 250f;
                RaycastHit hit;

                if (Physics.Raycast(target.WithY(p), Vector3.down, out hit, ++p, targetMask, QueryTriggerInteraction.Ignore) && !blockedcolliders.Exists(hit.collider.ObjectName().Contains))
                {
                    y = Mathf.Max(y, hit.point.y);
                }

                return flag ? Mathf.Max(y, w) : y;
            }

            public static Elevation GetTerrainElevation(Vector3 center, float radius)
            {
                float maxY = -1000;
                float minY = 1000;

                foreach (var position in GetCircumferencePositions(center, radius, 30f, true, 0f)) // 70 to 30 in 1.5.1
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

            public static bool ContainsTopology(TerrainTopology.Enum mask, Vector3 position, float radius)
            {
                return (TerrainMeta.TopologyMap.GetTopology(position, radius) & (int)mask) != 0;
            }

            private static bool IsValidLocation(Vector3 vector, float radius, float md, bool seabed)
            {
                CacheType cacheType;
                string message;
                SafeAreaCheck sac = new SafeAreaCheck(vector, radius, radius, Layers.Mask.World | Layers.Mask.Deployed | Layers.Mask.Trigger);
                if (!IsAreaSafe(sac, out cacheType, out message))
                {
                    return false;
                }

                foreach (var zone in managedZones)
                {
                    if (zone.Size != Vector3.zero)
                    {
                        if (zone.OBB.IsInBounds(vector))
                        {
                            return false;
                        }
                    }
                    else if (InRange2D(zone.Position, vector, zone.Distance))
                    {
                        return false;
                    }
                }

                if (!seabed && InDeepWater(vector, 5f))
                {
                    return false;
                }

                if (IsMonumentPosition(vector) || ContainsTopology(TerrainTopology.Enum.Monument, vector, md))
                {
                    return false;
                }

                if (!config.Settings.Management.AllowOnBuildingTopology && ContainsTopology(TerrainTopology.Enum.Building, vector, M_RADIUS))
                {
                    return false;
                }

                if (!config.Settings.Management.AllowOnRivers && ContainsTopology(TerrainTopology.Enum.River | TerrainTopology.Enum.Riverside, vector, M_RADIUS))
                {
                    return false;
                }

                if (!config.Settings.Management.AllowOnRoads && ContainsTopology(TerrainTopology.Enum.Road | TerrainTopology.Enum.Roadside, vector, M_RADIUS))
                {
                    return false;
                }

                return true;
            }

            public static void ExtractLocation(RaidableSpawns spawns, Vector3 position, float e, float m, float p, float w, bool s)
            {
                if (IsValidLocation(position, CELL_SIZE, m, s))
                {
                    var elevation = GetTerrainElevation(position, 20f);

                    if (IsFlatTerrain(position, elevation, e))
                    {
                        var rsl = new RaidableSpawnLocation(position)
                        {
                            Elevation = elevation,
                            WaterHeight = TerrainMeta.WaterMap.GetHeight(position),
                            TerrainHeight = TerrainMeta.HeightMap.GetHeight(position),
                            SpawnHeight = GetSpawnHeight(position, false),
                            Radius = p,
                            AutoHeight = true
                        };

                        if (s)
                        {
                            if (InDeepWater(position, w))
                            {
                                GridController.Seabed.Add(rsl);
                            }
                            else spawns.Spawns.Add(rsl);
                        }
                        else spawns.Spawns.Add(rsl);
                    }
                }
            }

            public static bool IsSubmerged(BuildingWaterOptions options, RaidableSpawnLocation rsl)
            {
                if (rsl.WaterHeight - rsl.TerrainHeight > options.WaterDepth)
                {
                    if (!options.AllowSubmerged)
                    {
                        return true;
                    }

                    rsl.Location.y = rsl.WaterHeight;
                }

                return !options.AllowSubmerged && options.SubmergedAreaCheck && IsSubmerged(options, rsl, rsl.Radius);
            }

            private static bool IsSubmerged(BuildingWaterOptions options, RaidableSpawnLocation rsl, float radius)
            {
                if (rsl.Surroundings.Count == 0)
                {
                    rsl.Surroundings = GetCircumferencePositions(rsl.Location, radius, 90f, false, 1f);
                }

                foreach (var vector in rsl.Surroundings)
                {
                    float w = TerrainMeta.WaterMap.GetHeight(vector);
                    float h = TerrainMeta.HeightMap.GetHeight(vector);

                    if (w - h > options.WaterDepth)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static float GetRockHeight(Vector3 a)
            {
                RaycastHit hit;
                if (Physics.Raycast(a + new Vector3(0f, 50f, 0f), Vector3.down, out hit, a.y + 51f, Layers.Mask.World, QueryTriggerInteraction.Ignore))
                {
                    return Mathf.Abs(hit.point.y - a.y);
                }

                return 0f;
            }

            private static bool IsSafeZone(Vector3 a)
            {
                return TriggerSafeZone.allSafeZones.Exists(triggerSafeZone => InRange2D(triggerSafeZone.transform.position, a, triggerSafeZone.triggerCollider.GetRadius(triggerSafeZone.transform.localScale) + config.Settings.Management.MonumentDistance));
            }

            public class SafeAreaCheck
            {
                public Vector3 position;
                public float safeRadius;
                public float buildRadius;
                public int layers;
                public RaidableType type;
                public BuildingOptionsDifficultySpawns spawns;
                public SafeAreaCheck(Vector3 position, float safeRadius, float buildRadius, int layers, RaidableType type = RaidableType.None, BuildingOptionsDifficultySpawns spawns = null)
                {
                    this.position = position;
                    this.safeRadius = safeRadius;
                    this.buildRadius = buildRadius;
                    this.layers = layers;
                    this.type = type;
                    this.spawns = spawns;
                }
            }

            public static bool IsAreaSafe(SafeAreaCheck sac, out CacheType cacheType, out string message)
            {
                if (IsSafeZone(sac.position))
                {
                    message = $"Safe Zone at {sac.position}";
                    cacheType = CacheType.Delete;
                    return false;
                }

                cacheType = CacheType.Generic;
                message = string.Empty;

                int hits = Physics.OverlapSphereNonAlloc(sac.position, Mathf.Max(sac.safeRadius, sac.buildRadius), Vis.colBuffer, sac.layers, QueryTriggerInteraction.Collide);

                for (int i = 0; i < hits; i++)
                {
                    if (cacheType != CacheType.Generic)
                    {
                        goto next;
                    }

                    var collider = Vis.colBuffer[i];
                    var colName = collider.ObjectName();

                    if (colName == "ZoneManager" || colName.Contains("xmas"))
                    {
                        goto next;
                    }

                    var e = collider.ToBaseEntity();

                    if (e is BuildingPrivlidge)
                    {
                        if (!RaidableBase.Has(e))
                        {
                            message = $"Blocked by a building privilege {collider.transform.position} {colName}";
                            cacheType = CacheType.Privilege;
                        }

                        goto next;
                    }

                    float dist = (collider.transform.position - sac.position).magnitude;

                    if (dist > sac.safeRadius && dist <= sac.buildRadius)
                    {
                        goto next;
                    }

                    if (assets.Exists(colName.Contains) && (e.IsNull() || e.name.Contains("/treessource/")))
                    {
                        message = $"Blocked by a map prefab {collider.transform.position} {colName}";
                        cacheType = CacheType.Delete;
                        goto next;
                    }

                    if (e.IsReallyValid())
                    {
                        if (e.PrefabName.Contains("xmas") || e.name.StartsWith("assets/prefabs/plants"))
                        {
                            goto next;
                        }

                        bool isSteamId = e.OwnerID.IsSteamId();

                        if (e is BasePlayer)
                        {
                            var player = e as BasePlayer;

                            if (!(!player.IsHuman() || player.IsFlying || config.Settings.Management.EjectSleepers && player.IsSleeping()))
                            {
                                message = $"{player.displayName} ({player.userID}) is too close {e.transform.position}";
                                cacheType = CacheType.Temporary;
                                goto next;
                            }
                        }
                        else if (isSteamId && e is SleepingBag)
                        {
                            goto next;
                        }
                        else if (isSteamId && sac.spawns?.Skip == true)
                        {
                            goto next;
                        }
                        else if (isSteamId && config.Settings.Schedule.Skip && sac.type == RaidableType.Scheduled)
                        {
                            goto next;
                        }
                        else if (isSteamId && config.Settings.Maintained.Skip && sac.type == RaidableType.Maintained)
                        {
                            goto next;
                        }
                        else if (isSteamId && config.Settings.Buyable.Skip && sac.type == RaidableType.Purchased)
                        {
                            goto next;
                        }
                        else if (RaidableBase.Has(e))
                        {
                            message = $"Already occupied by a raidable base {e.transform.position}";
                            cacheType = CacheType.Temporary;
                            goto next;
                        }
                        else if (e.IsNpc || e is SleepingBag)
                        {
                            goto next;
                        }
                        else if (e is BaseOven)
                        {
                            if (e.bounds.size.Max() > 1.6f)
                            {
                                message = $"An oven is too close {e.transform.position}";
                                cacheType = CacheType.Temporary;
                                goto next;
                            }
                        }
                        else if (e is PlayerCorpse)
                        {
                            var corpse = e as PlayerCorpse;

                            if (corpse.playerSteamID == 0 || corpse.playerSteamID.IsSteamId())
                            {
                                message = $"A player's corpse is too close {e.transform.position}";
                                cacheType = CacheType.Temporary;
                                goto next;
                            }
                        }
                        else if (e is DroppedItemContainer && e.ShortPrefabName != "item_drop")
                        {
                            var backpack = e as DroppedItemContainer;

                            if (backpack.playerSteamID == 0 || backpack.playerSteamID.IsSteamId())
                            {
                                message = $"A player's backpack is too close {e.transform.position}";
                                cacheType = CacheType.Temporary;
                                goto next;
                            }
                        }
                        else if (e.OwnerID == 0)
                        {
                            if (e is BuildingBlock)
                            {
                                message = $"{e.ShortPrefabName} is too close {e.transform.position}";
                                cacheType = CacheType.Temporary;
                                goto next;
                            }
                            else if (e is MiningQuarry)
                            {
                                message = $"{e.ShortPrefabName} is too close {e.transform.position}";
                                cacheType = CacheType.Delete;
                                goto next;
                            }
                        }
                        else
                        {
                            message = $"Blocked by {e.ShortPrefabName} {e.transform.position}";
                            cacheType = CacheType.Temporary;
                            goto next;
                        }
                    }
                    else if (collider.gameObject.layer == (int)Layer.World)
                    {
                        if (colName.Contains("rock_") || colName.Contains("formation_", CompareOptions.OrdinalIgnoreCase))
                        {
                            float height = GetRockHeight(collider.transform.position);

                            if (height > 2f)
                            {
                                message = $"Rock is too large {collider.transform.position}";
                                cacheType = CacheType.Delete;
                                goto next;
                            }
                        }
                        else if (!config.Settings.Management.AllowOnRoads && colName.StartsWith("road_"))
                        {
                            message = $"Not allowed on roads {collider.transform.position}";
                            cacheType = CacheType.Delete;
                            goto next;
                        }
                        else if (colName.StartsWith("ice_sheet"))
                        {
                            message = $"Not allowed on ice sheets {collider.transform.position}";
                            cacheType = CacheType.Delete;
                            goto next;
                        }
                    }
                    else if (collider.gameObject.layer == (int)Layer.Water)
                    {
                        if (!config.Settings.Management.AllowOnRivers && colName.StartsWith("River Mesh"))
                        {
                            message = $"Not allowed on rivers {collider.transform.position}";
                            cacheType = CacheType.Delete;
                            goto next;
                        }
                    }

                next:
                    Vis.colBuffer[i] = null;
                }

                return string.IsNullOrEmpty(message);
            }

            public static bool IsFlatTerrain(Vector3 center, Elevation elevation, float value)
            {
                return elevation.Max - elevation.Min <= value && elevation.Max - center.y <= value;
            }

            private static bool InDeepWater(Vector3 vector, float depth)
            {
                vector.y = TerrainMeta.HeightMap.GetHeight(vector);

                return WaterLevel.GetWaterDepth(vector, true, null) >= depth;
            }

            public static bool IsMonumentPosition(Vector3 target)
            {
                return Monuments.Exists(monument => monument.IsInBounds(target));
            }

            private static void SetupMonuments()
            {
                List<MonumentInfo> monuments;
                if (TerrainMeta.Path == null)
                {
                    monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>().ToList();
                }
                else monuments = TerrainMeta.Path.Monuments;

                foreach (var monument in monuments)
                {
                    MonumentInfoEx mi = new MonumentInfoEx(monument);

                    Monuments.Add(mi);
                }
            }

            public static void BlockZoneManagerZones(Plugin ZoneManager)
            {
                if (!ZoneManager.CanCall())
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

                    var zoneName = Convert.ToString(ZoneManager.Call("GetZoneName", zoneId));

                    if (config.Settings.Inclusions.Exists(zone => zone == "*" || zone == zoneId || !string.IsNullOrEmpty(zoneName) && zoneName.Contains(zone, CompareOptions.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var radius = ZoneManager.Call("GetZoneRadius", zoneId);
                    var size = ZoneManager.Call("GetZoneSize", zoneId);

                    managedZones.Add(new ZoneInfo(zoneLoc, radius, size));
                }

                if (managedZones.Count > 0)
                {
                    Puts(mx("BlockedZones", null, managedZones.Count));
                }
            }

            public static bool IsObstructed(Vector3 target, float radius, float elevation, float forcedHeight, BasePlayer player = null)
            {
                float f = radius * 0.2f;
                int n = 5;
                bool flag = false;
                bool isValid = player.IsReallyValid();

                if (forcedHeight != -1)
                {
                    elevation += forcedHeight;
                }

                while (n-- > 0)
                {
                    float step = f * n;
                    float next = 360f / step;

                    foreach (var a in GetCircumferencePositions(target, step, next, true, 0f))
                    {
                        if (Mathf.Abs(a.y - target.y) > elevation)
                        {
                            if (isValid) player.SendConsoleCommand("ddraw.text", 15f, Color.red, a, "X");
                            flag = true;
                        }
                    }
                }

                return flag;
            }
        }

        #region Hooks

        private void UnsubscribeHooks()
        {
            if (IsUnloading)
            {
                return;
            }

            Unsubscribe(nameof(CanBGrade));
            Unsubscribe(nameof(OnLifeSupportSavingLife));
            Unsubscribe(nameof(OnRestoreUponDeath));
            Unsubscribe(nameof(OnNpcKits));
            Unsubscribe(nameof(CanTeleport));
            Unsubscribe(nameof(canTeleport));
            Unsubscribe(nameof(canRemove));
            Unsubscribe(nameof(CanEntityBeTargeted));
            Unsubscribe(nameof(CanEntityTrapTrigger));
            Unsubscribe(nameof(CanEntityTakeDamage));
            Unsubscribe(nameof(CanOpenBackpack));
            Unsubscribe(nameof(CanBePenalized));
            Unsubscribe(nameof(OnBaseRepair));

            Unsubscribe(nameof(CanBeTargeted));
            Unsubscribe(nameof(OnButtonPress));
            Unsubscribe(nameof(OnElevatorButtonPress));
            Unsubscribe(nameof(CanSamSiteShoot));
            Unsubscribe(nameof(OnPlayerCommand));
            Unsubscribe(nameof(OnServerCommand));
            Unsubscribe(nameof(OnTrapTrigger));
            Unsubscribe(nameof(OnEntityBuilt));
            Unsubscribe(nameof(OnStructureUpgrade));
            Unsubscribe(nameof(OnEntityGroundMissing));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnLootEntity));
            Unsubscribe(nameof(OnLootEntityEnd));
            Unsubscribe(nameof(OnExplosiveFuseSet));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(CanPickupEntity));
            Unsubscribe(nameof(OnPlayerLand));
            Unsubscribe(nameof(OnPlayerDeath));
            Unsubscribe(nameof(OnPlayerDropActiveItem));
            Unsubscribe(nameof(OnEntityEnter));
            Unsubscribe(nameof(OnNpcDuck));
            Unsubscribe(nameof(OnNpcResume));
            Unsubscribe(nameof(OnNpcDestinationSet));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnCupboardAuthorize));
            Unsubscribe(nameof(OnActiveItemChanged));
            Unsubscribe(nameof(OnLoseCondition));
            Unsubscribe(nameof(OnFireBallSpread));
            Unsubscribe(nameof(CanBuild));
            Unsubscribe(nameof(OnCupboardProtectionCalculated));
        }

        private void OnMapMarkerAdded(BasePlayer player, ProtoBuf.MapNote note)
        {
            if (player.IsAlive() && player.HasPermission("raidablebases.mapteleport"))
            {
                float y = SpawnsController.GetSpawnHeight(note.worldPosition);
                if (player.IsFlying) y = Mathf.Max(y, player.transform.position.y);
                player.Teleport(new Vector3(note.worldPosition.x, y, note.worldPosition.z));
            }
        }

        private void OnNewSave(string filename) => wiped = true;

        private void Init()
        {
            tryBuyCooldowns = new Dictionary<string, float>();
            Instance = this;
            IsUnloading = false;
            _sb = new StringBuilder();
            _sb2 = new StringBuilder();
            Buildings = new BuildingTables();

            if (config.RankedLadder.Enabled)
            {
                foreach (var record in RankedRecords)
                {
                    permission.RegisterPermission(record.Permission, this);
                    permission.CreateGroup(record.Group, record.Group, 0);
                    permission.GrantGroupPermission(record.Group, record.Permission, this);
                }
            }

            permission.RegisterPermission("raidablebases.allow", this);
            permission.RegisterPermission("raidablebases.allow.commands", this);
            permission.RegisterPermission("raidablebases.setowner", this);
            permission.RegisterPermission("raidablebases.ladder.exclude", this);
            permission.RegisterPermission("raidablebases.durabilitybypass", this);
            permission.RegisterPermission("raidablebases.ddraw", this);
            permission.RegisterPermission("raidablebases.mapteleport", this);
            permission.RegisterPermission("raidablebases.canbypass", this);
            permission.RegisterPermission("raidablebases.lockoutbypass", this);
            permission.RegisterPermission("raidablebases.blockbypass", this);
            permission.RegisterPermission("raidablebases.banned", this);
            permission.RegisterPermission("raidablebases.vipcooldown", this);
            permission.RegisterPermission("raidablebases.despawn.buyraid", this);
            permission.RegisterPermission("raidablebases.notitle", this);
            permission.RegisterPermission("raidablebases.block.fauxadmin", this);
            permission.RegisterPermission("raidablebases.admin.loot", this);
            permission.RegisterPermission("raidablebases.elevators.bypass.building", this);
            permission.RegisterPermission("raidablebases.elevators.bypass.card", this);
            permission.RegisterPermission("raidablebases.time", this);
            permission.RegisterPermission("raidablebases.buyraid", this);
            permission.RegisterPermission("raidablebases.hoggingbypass", this);
            lastSpawnRequestTime = Time.realtimeSinceStartup;
            buyableEnabled = config.Settings.Buyable.Max > 0;
            Unsubscribe(nameof(OnMapMarkerAdded));
            Unsubscribe(nameof(OnPlayerSleepEnded));
            UnsubscribeHooks();
            GridController.Initialize();
            SpawnsController.Initialize();
            UI.Init();
        }

        private void OnServerInitialized(bool isStartup)
        {
            Scheduled = new ScheduledController(this, config.Settings.Schedule.Enabled);
            Maintained = new MaintainedController(this, config.Settings.Maintained.Enabled);
            AddCovalenceCommand(config.Settings.BuyCommand, nameof(CommandBuyRaid));
            AddCovalenceCommand(config.Settings.EventCommand, nameof(CommandRaidBase));
            AddCovalenceCommand(config.Settings.HunterCommand, nameof(CommandRaidHunter));
            AddCovalenceCommand(config.Settings.ConsoleCommand, nameof(CommandRaidBase));
            AddCovalenceCommand("rb.reloadconfig", nameof(CommandReloadConfig));
            AddCovalenceCommand("rb.config", nameof(CommandConfig), "raidablebases.config");
            AddCovalenceCommand("rb.populate", nameof(CommandPopulate), "raidablebases.config");
            AddCovalenceCommand("rb.toggle", nameof(CommandToggle), "raidablebases.config");
            StoredData.Load(timer);
            CheckForWipe();
            Initialize();
            OceanLevel = WaterSystem.OceanLevel;
            if (config.Settings.Management.Lockout.Any()) timer.Repeat(1f, 0, CheckLockouts);
            timer.Repeat(Mathf.Clamp(config.EventMessages.Interval, 1f, 60f), 0, CheckNotifications);
            timer.Repeat(30f, 0, RaidableBase.UpdateAllMarkers);
            timer.Repeat(60f, 0, CheckOceanLevel);
            timer.Repeat(300f, 0, SaveData);
        }

        private void OnSunrise()
        {
            Raids.ToList().ForEach(raid => raid.ToggleLights(false));
        }

        private void OnSunset()
        {
            Raids.ToList().ForEach(raid => raid.ToggleLights(true));
        }

        private void SaveData() => RaidableBase.TryInvokeMethod(data.Save);

        private void OnServerShutdown()
        {
            IsUnloading = true;
            RaidableBase.Unload(Raids.ToList(), true);
            Scheduled.StopCoroutine();
            Maintained.StopCoroutine();
            GridController.StopCoroutine();
            DestroyAll();
        }

        private void Unload()
        {
            if (IsUnloading)
            {
                return;
            }

            SetOnSun(false);
            IsUnloading = true;
            SaveData();
            RaidableBase.Unload(Raids.ToList(), false);
            Scheduled.StopCoroutine();
            Maintained.StopCoroutine();
            GridController.StopCoroutine();
            DestroyComponents();

            if (Raids.Count > 0)
            {
                DespawnAllBasesNow(false);
                return;
            }

            UnsetStatics();
        }

        private static void UnsetStatics()
        {
            SpawnsController.Clear();
            GridController.Clear();
            UI.DestroyAllLockoutUI();
            UI.DestroyAllBuyableUI();
            UI.DestroyAllStatusUI();
            UI.Clear();
            Instance = null;
            config = null;
            data = null;
            _sb = null;
            _sb2 = null;
            RaidableBasesExtensionMethods.ExtensionMethods.p = null;
        }

        private void CheckForWipe()
        {
            if (!wiped && BuildingManager.server.buildingDictionary.Count == 0)
            {
                wiped = data.Players.Values.Exists(pi => pi.Raids > 0);
            }

            if (wiped)
            {
                var raids = new List<int>();

                if (data.Players.Count > 0)
                {
                    if (AssignTreasureHunters())
                    {
                        foreach (var entry in data.Players.ToList())
                        {
                            if (entry.Value.Raids > 0)
                            {
                                raids.Add(entry.Value.Raids);
                            }

                            data.Players[entry.Key].Reset();
                        }
                    }

                    if (raids.Count > 0)
                    {
                        var average = raids.Average();

                        data.Players.RemoveAll((userid, playerInfo) => playerInfo.TotalRaids < average);
                    }
                }

                data.Lockouts.Clear();
                wiped = false;
                NextTick(SaveData);
            }
        }

        private void Reinitialize()
        {
            Instance.Skins.Clear();

            if (config.Settings.TeleportMarker)
            {
                Subscribe(nameof(OnMapMarkerAdded));
            }
            else Unsubscribe(nameof(OnMapMarkerAdded));

            Subscribe(nameof(OnPlayerSleepEnded));
        }

        private object OnLifeSupportSavingLife(BasePlayer player)
        {
            return EventTerritory(player.transform.position) || HasPVPDelay(player.userID) ? true : (object)null;
        }

        private object OnRestoreUponDeath(BasePlayer player)
        {
            var raid = RaidableBase.Get(player.transform.position);

            if (raid == null)
            {
                return null;
            }

            return config.Settings.Management.BlockRestorePVE && !raid.AllowPVP || config.Settings.Management.BlockRestorePVP && raid.AllowPVP ? true : (object)null;
        }

        private object OnNpcKits(ulong targetId)
        {
            return RaidableBase.Get(targetId) == null ? (object)null : true;
        }

        private object CanBGrade(BasePlayer player, int playerGrade, BuildingBlock block, Planner planner)
        {
            if (player.IsReallyValid() && (EventTerritory(player.transform.position) || PvpDelay.ContainsKey(player.userID)))
            {
                return 0;
            }

            return null;
        }

        private object canRemove(BasePlayer player)
        {
            return !player.IsFlying && EventTerritory(player.transform.position) ? m("CannotRemove", player.UserIDString) : null;
        }

        private object canTeleport(BasePlayer player)
        {
            return !player.IsFlying && (EventTerritory(player.transform.position) || PvpDelay.ContainsKey(player.userID)) ? m("CannotTeleport", player.UserIDString) : null;
        }

        private object CanTeleport(BasePlayer player, Vector3 to)
        {
            return !player.IsFlying && (EventTerritory(to) || EventTerritory(player.transform.position) || PvpDelay.ContainsKey(player.userID)) ? m("CannotTeleport", player.UserIDString) : null;
        }

        private BasePlayer GetOwnerPlayer(Item item)
        {
            if (item.parentItem == null)
            {
                return item.GetOwnerPlayer();
            }

            return item.parentItem.GetOwnerPlayer();
        }

        private object OnBaseRepair(BuildingManager.Building building, BasePlayer player)
        {
            return EventTerritory(player.transform.position) ? false : (object)null;
        }

        private object OnLoseCondition(Item item, float amount)
        {
            if (item == null) // || !item.info.GetComponent<ItemModWearable>().IsReallyNull())
            {
                return null;
            }

            var player = GetOwnerPlayer(item);

            if (player.IsNull() || !player.userID.IsSteamId() || player.HasPermission("raidablebases.durabilitybypass"))
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

                if (item == null || !item.IsValid() || item.isBroken)
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

        private object OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            var raid = RaidableBase.Get(block.transform.position);

            if (raid == null || !raid.Options.BuildingRestrictions.Any())
            {
                return null;
            }

            if (!config.Settings.Management.AllowUpgrade && RaidableBase.Has(block))
            {
                return true;
            }

            switch (grade)
            {
                case BuildingGrade.Enum.Metal: return raid.Options.BuildingRestrictions.Metal ? true : (object)null;
                case BuildingGrade.Enum.Stone: return raid.Options.BuildingRestrictions.Stone ? true : (object)null;
                case BuildingGrade.Enum.TopTier: return raid.Options.BuildingRestrictions.HQM ? true : (object)null;
                case BuildingGrade.Enum.Wood: return raid.Options.BuildingRestrictions.Wooden ? true : (object)null;
            }

            return null;
        }

        private void OnEntityBuilt(Planner planner, GameObject go)
        {
            var e = go.ToBaseEntity();

            if (e.IsNull())
            {
                return;
            }

            var raid = RaidableBase.Get(e.transform.position);

            if (raid == null)
            {
                return;
            }

            var player = planner.GetOwnerPlayer();

            if (player.IsNull())
            {
                return;
            }

            if (raid.Options.BuildingRestrictions.Any() && e is BuildingBlock)
            {
                var block = e as BuildingBlock;
                var grade = block.grade;

                block.Invoke(() =>
                {
                    if (block.IsDestroyed || block.grade == grade || OnStructureUpgrade(block, player, block.grade) == null)
                    {
                        return;
                    }

                    foreach (var ia in block.BuildCost())
                    {
                        player.GiveItem(ItemManager.Create(ia.itemDef, (int)ia.amount));
                    }

                    block.SafelyKill();
                }, 0.1f);
            }

            if (!raid.intruders.Contains(player.userID))
            {
                e.Invoke(e.SafelyKill, 0.1f);
                return;
            }

            if (e.ShortPrefabName == "foundation.triangle" || e.ShortPrefabName == "foundation")
            {
                var decayEntity = e as DecayEntity;

                if (decayEntity.buildingID == raid.BuildingID)
                {
                    raid.TryMessage(player, "TooCloseToABuilding");
                    e.Invoke(e.SafelyKill, 0.1f);
                    return;
                }
            }

            AddEntity(e, raid);
        }

        private void AddEntity(BaseEntity e, RaidableBase raid)
        {
            raid.BuiltList[e] = e.transform.position;
            raid.SetupEntity(e, false);

            if (e.name.Contains("assets/prefabs/deployable/"))
            {
                if (config.Settings.Management.DoNotDestroyDeployables)
                {
                    UnityEngine.Object.Destroy(e.GetComponent<DestroyOnGroundMissing>());
                    UnityEngine.Object.Destroy(e.GetComponent<GroundWatch>());
                }
                else
                {
                    raid.AddEntity(e);
                }
            }
            else if (!config.Settings.Management.DoNotDestroyStructures)
            {
                raid.AddEntity(e);
            }
        }

        private void OnElevatorButtonPress(ElevatorLift e, BasePlayer player, Elevator.Direction Direction, bool FullTravel)
        {
            BMGELEVATOR bmgELEVATOR;
            if (_elevators.TryGetValue(e.GetParentEntity().net.ID, out bmgELEVATOR))
            {
                if (bmgELEVATOR.HasCardPermission(player) && bmgELEVATOR.HasBuildingPermission(player))
                {
                    bmgELEVATOR.GoToFloor(Direction, FullTravel);
                }
            }
        }

        private void OnButtonPress(PressButton button, BasePlayer player)
        {
            if (button.OwnerID == 0 && RaidableBase.Has(button))
            {
                foreach (var e in _elevators)
                {
                    if (e.Value.ValidPosition && Vector3Ex.Distance2D(button.ServerPosition, e.Value.ServerPosition) <= 3f)
                    {
                        e.Value.GoToFloor(Elevator.Direction.Up, false, Mathf.CeilToInt(button.transform.position.y));
                    }
                }
            }
        }

        private object CanSamSiteShoot(SamSite ss)
        {
            if (EventTerritory(ss.transform.position) && RaidableBase.Has(ss))
            {
                if (ss.HasValidTarget() && InRange(ss.currentTarget.CenterPoint(), ss.transform.position, config.Weapons.SamSiteRange))
                {
                    return null;
                }

                return true;
            }

            return null;
        }

        private object OnEntityEnter(TriggerBase trigger, BaseEntity entity)
        {
            if (entity.IsKilled())
            {
                return null;
            }

            if (entity is Drone && RaidableBase.Has(trigger))
            {
                return true;
            }

            var player = entity as BasePlayer;

            if (player.IsNull()) // TargetTrigger, PlayerDetectionTrigger // Prevent npcs from triggering HBHFSensor, GunTrap, FlameTurret, AutoTurret
            {
                return null;
            }

            if (RaidableBase.Has(player.userID) && (RaidableBase.Has(trigger) || RaidableBase.Get(player.userID).Options.NPC.IgnoreTrapsTurrets))
            {
                return true;
            }

            return config.Settings.Management.IgnoreFlying && player.IsFlying && EventTerritory(player.transform.position) ? true : (object)null;
        }

        private object OnNpcDuck(ScientistNPC npc) => RaidableBase.Has(npc.userID) ? true : (object)null;

        private object OnNpcDestinationSet(ScientistNPC npc, Vector3 newDestination)
        {
            if (npc.IsNull() || npc.NavAgent == null || !npc.NavAgent.enabled || !npc.NavAgent.isOnNavMesh)
            {
                return true;
            }

            HumanoidBrain brain;
            if (!HumanoidBrains.TryGetValue(npc.userID, out brain) || brain.CanRoam(newDestination))
            {
                return null;
            }

            return true;
        }

        private object OnNpcResume(ScientistNPC npc)
        {
            if (npc.IsNull())
            {
                return null;
            }

            HumanoidBrain brain;
            if (!HumanoidBrains.TryGetValue(npc.userID, out brain) || !brain.isStationary)
            {
                return null;
            }

            return true;
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (!player.IsHuman() || !EventTerritory(player.transform.position))
            {
                return;
            }

            RaidableBase.StopUsingWand(player);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            player.Invoke(() =>
            {
                if (player.IsDestroyed || !player.IsHuman())
                {
                    return;
                }

                DelaySettings ds;
                if (PvpDelay.TryGetValue(player.userID, out ds))
                {
                    if (ds.Timer != null && !ds.Timer.Destroyed)
                    {
                        ds.Timer.Callback.Invoke();
                        ds.Timer.Destroy();
                    }

                    PvpDelay.Remove(player.userID);
                }

                if (config.UI.Enabled)
                {
                    UI.UpdateLockoutUI(player);
                }

                if (config.Settings.Management.AllowTeleport)
                {
                    return;
                }

                var raid = RaidableBase.Get(player.transform.position, 5f);

                if (raid == null)
                {
                    return;
                }

                if (InRange2D(player.transform.position, raid.Location, raid.ProtectionRadius))
                {
                    raid.OnEnterRaid(player);
                }
                else RaidableBase.RemovePlayer(player, EjectCode.Awaken, raid.Location, raid.ProtectionRadius, raid.Type);
            }, 0.015f);
        }

        private object OnPlayerLand(BasePlayer player, float amount)
        {
            var raid = RaidableBase.Get(player.transform.position);

            return raid == null || !raid.IsDespawning ? (object)null : true;
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            var raid = RaidableBase.Get(player, hitInfo);

            if (raid == null)
            {
                return;
            }

            if (!player.IsHuman())
            {
                if (!RaidableBase.Has(player.userID))
                {
                    return;
                }

                if (config.Settings.Management.UseOwners && hitInfo != null && hitInfo.Initiator is BasePlayer)
                {
                    var attacker = hitInfo.Initiator as BasePlayer;

                    if (raid.AddLooter(attacker))
                    {
                        raid.TrySetOwner(attacker, player, hitInfo);
                    }
                }

                if (raid.Options.NPC.DespawnInventory)
                {
                    player.inventory.Strip();
                }

                raid.CheckDespawn();
            }
            else
            {
                if (CanDropPlayerBackpack(player, raid))
                {
                    Backpacks?.Call("API_DropBackpack", player);
                }

                raid.OnPlayerExit(player);
            }
        }

        private object OnPlayerDropActiveItem(BasePlayer player, Item item)
        {
            return EventTerritory(player.transform.position) ? true : (object)null;
        }

        private object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (player.IsReallyValid() && !player.HasPermission("raidablebases.allow.commands") && EventTerritory(player.transform.position))
            {
                foreach (var value in config.Settings.BlacklistedCommands)
                {
                    if (command.EndsWith(value, StringComparison.OrdinalIgnoreCase))
                    {
                        Message(player, "CommandNotAllowed");
                        return true;
                    }
                }
            }

            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (player.IsReallyValid() && !player.HasPermission("raidablebases.allow.commands") && EventTerritory(player.transform.position))
            {
                foreach (var value in config.Settings.BlacklistedCommands)
                {
                    if (arg.cmd.FullName.EndsWith(value, StringComparison.OrdinalIgnoreCase))
                    {
                        Message(player, "CommandNotAllowed");
                        return true;
                    }
                }
            }

            return null;
        }

        private object OnExplosiveFuseSet(TimedExplosive explosive, float fuseLength)
        {
            var npc = explosive.creatorEntity as ScientistNPC;

            if (npc.IsNull())
            {
                return null;
            }

            HumanoidBrain brain;
            if (!HumanoidBrains.TryGetValue(npc.userID, out brain) || !brain.Settings.PlayCatch || !brain.ValidTarget)
            {
                return null;
            }

            return Vector3.Distance(brain.ServerPosition, brain.AttackPosition) * 0.1275f;
        }

        private void OnEntityDeath(BuildingPrivlidge priv, HitInfo hitInfo)
        {
            var raid = RaidableBase.Get(priv);

            if (raid == null)
            {
                return;
            }

            if (!raid.IsDespawning && config.Settings.Management.AllowCupboardLoot)
            {
                DropOrRemoveItems(priv, false);
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

            raid.OnBuildingPrivilegeDestroyed();
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

        //private void OnEntityKill(BuildingBlock block) => OnEntityDeath(block, null); // ent kill testing

        private void OnEntityDeath(StabilityEntity entity, HitInfo hitInfo)
        {
            var raid = RaidableBase.Get(entity.transform.position);

            if (raid == null || raid.IsDespawning)
            {
                return;
            }

            var player = hitInfo?.Initiator as BasePlayer;

            if (!player.IsReallyValid())
            {
                return;
            }

            if (raid.AddLooter(player))
            {
                raid.TrySetOwner(player, entity, hitInfo);
            }

            raid.CheckDespawn();

            if (raid.IsDamaged || entity is SimpleBuildingBlock)
            {
                return;
            }

            raid.IsDamaged = true;
        }

        private object OnEntityGroundMissing(StorageContainer container)
        {
            if (IsBox(container, false))
            {
                var raid = RaidableBase.Get(container);

                if (raid != null && raid.Options.Invulnerable)
                {
                    return true;
                }
            }

            EntityHandler(container, null);
            return null;
        }

        private void OnEntityDeath(IOEntity io, HitInfo hitInfo)
        {
            if (config.Settings.Management.DropLoot.AutoTurret && io is AutoTurret && io.OwnerID == 0uL)
            {
                DropIOLoot(io, true);
            }
            else if (config.Settings.Management.DropLoot.SamSite && io is SamSite && io.OwnerID == 0uL)
            {
                DropIOLoot(io, false);
            }
        }

        private void DropIOLoot(IOEntity io, bool isAutoTurret)
        {
            var raid = RaidableBase.Get(io);

            if (raid == null || !raid.IsOpened || raid.IsDespawning)
            {
                return;
            }

            IItemContainerEntity ice = io as IItemContainerEntity;

            if (ice.inventory.itemList.Count > 0)
            {
                float y = io.transform.position.y + (io.bounds.size.y - 0.015f);

                ice.inventory.Drop(StringPool.Get(545786656), io.transform.position.WithY(y), io.transform.rotation);
            }

            if (isAutoTurret) raid.turrets.RemoveAll(turret => io == turret);
            else raid.samsites.RemoveAll(ss => io == ss);
        }

        private void EntityHandler(StorageContainer container, HitInfo hitInfo)
        {
            var raid = RaidableBase.Get(container);

            if (raid == null || raid.IsDespawning)
            {
                return;
            }

            DropOrRemoveItems(container, raid.IsProtectedWeapon(container, true));

            raid._containers.Remove(container);

            UI.UpdateStatusUI(raid);

            if (IsLootingWeapon(hitInfo))
            {
                var player = raid.GetInitiatorPlayer(hitInfo, container);

                if (player.IsReallyValid())
                {
                    raid.AddLooter(player);
                }
            }

            if (raid.IsOpened && (IsBox(container, true) || container is BuildingPrivlidge))
            {
                raid.StartTryToEnd();
            }

            if (!Raids.Exists(x => x._containers.Count > 0))
            {
                Unsubscribe(nameof(OnEntityKill));
                Unsubscribe(nameof(OnEntityGroundMissing));
            }
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
            foreach (var raid in Raids)
            {
                if (raid.priv == priv && raid.Options.RequiresCupboardAccess && !raid.IsAuthed)
                {
                    raid.IsAuthed = true;

                    if (raid.Options.RequiresCupboardAccess && config.EventMessages.AnnounceRaidUnlock)
                    {
                        foreach (var p in BasePlayer.activePlayerList)
                        {
                            QueueNotification(p, "OnRaidFinished", FormatGridReference(raid.Location));
                        }
                    }

                    break;
                }
            }

            if (Raids.Exists(raid => !raid.IsAuthed))
            {
                return;
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

            if (player.IsReallyValid() && !raid.AddLooter(player))
            {
                return false;
            }

            if (raid.IsBlacklisted(entity.ShortPrefabName))
            {
                return false;
            }

            return !raid.Options.AllowPickup && entity.OwnerID == 0 ? false : (object)null;
        }

        private void OnEntitySpawned(MLRSRocket rocket)
        {
            if (rocket.IsNull()) return;
            var systems = FindEntitiesOfType<MLRS>(rocket.transform.position, 15f);
            if (systems.Count == 0) return;
            var raid = RaidableBase.Get(systems[0].TrueHitPos);
            if (raid == null) return;
            var owner = systems[0].rocketOwnerRef.Get(true) as BasePlayer;
            if (owner.IsNull()) return;
            rocket.creatorEntity = raid.Options.MLRS ? owner : null;
            rocket.OwnerID = raid.Options.MLRS ? owner.userID : 0uL;
        }

        private void OnEntitySpawned(FireBall fire)
        {
            if (config.Settings.Management.PreventFireFromSpreading && fire.ShortPrefabName == "flamethrower_fireball")
            {
                var ownerPlayer = fire.creatorEntity as BasePlayer;

                if (ownerPlayer?.userID.IsSteamId() == false)
                {
                    fire.Invoke(fire.Extinguish, 0.01f);
                }
            }
        }

        private void OnFireBallSpread(FireBall entity, BaseEntity fire)
        {
            if (EventTerritory(fire.transform.position))
            {
                NextTick(fire.SafelyKill);
            }
        }

        private void OnEntitySpawned(DroppedItemContainer backpack)
        {
            NextTick(() =>
            {
                if (backpack.IsKilled())
                {
                    return;
                }

                var raid = RaidableBase.Get(backpack.transform.position);

                if (raid == null)
                {
                    return;
                }

                if (backpack.ShortPrefabName == "item_drop")
                {
                    raid.HasDroppedItems = true;
                }

                if (!backpack.playerSteamID.IsSteamId())
                {
                    return;
                }

                DelaySettings ds;
                if (PvpDelay.TryGetValue(backpack.playerSteamID, out ds) && (ds.AllowPVP && config.Settings.Management.BackpacksPVP || !ds.AllowPVP && config.Settings.Management.BackpacksPVE))
                {
                    backpack.playerSteamID = 0;
                }
                else if (raid.AllowPVP && config.Settings.Management.BackpacksPVP || !raid.AllowPVP && config.Settings.Management.BackpacksPVE)
                {
                    backpack.playerSteamID = 0;
                }
            });
        }

        private void OnEntitySpawned(BaseLock entity)
        {
            var parent = entity.GetParentEntity();

            foreach (var raid in Raids)
            {
                if (raid.IsLoading)
                {
                    continue;
                }

                foreach (var container in raid._containers)
                {
                    if (parent == container)
                    {
                        entity.Invoke(entity.SafelyKill, 0.01f);
                        break;
                    }
                }
            }
        }

        private void OnEntitySpawned(PlayerCorpse corpse)
        {
            if (corpse.IsNull())
            {
                return;
            }

            var raid = RaidableBase.Get(corpse);

            if (raid == null)
            {
                return;
            }

            if (corpse.playerSteamID.IsSteamId())
            {
                var playerSteamID = corpse.playerSteamID;

                if (raid.Options.EjectBackpacks && !playerSteamID.HasPermission("reviveplayer.use"))
                {
                    if (corpse.containers.IsNullOrEmpty())
                    {
                        goto done;
                    }

                    var container = GameManager.server.CreateEntity(StringPool.Get(1519640547), corpse.transform.position, Quaternion.identity) as DroppedItemContainer;

                    container.maxItemCount = 42;
                    container.lootPanelName = "generic_resizable";
                    container.playerName = corpse.playerName;
                    container.playerSteamID = corpse.playerSteamID;
                    container.TakeFrom(corpse.containers);
                    container.Spawn();
                    corpse.SafelyKill();

                    var player = RustCore.FindPlayerById(playerSteamID);
                    var data = raid.AddBackpack(container, player);

                    if (raid.EjectBackpack(container.net.ID, data, false))
                    {
                        raid.backpacks.Remove(container.net.ID);
                    }
                    else
                    {
                        Interface.CallHook("OnRaidablePlayerCorpse", new object[] { player, playerSteamID, container, raid.Location, raid.AllowPVP, (int)raid.Options.Mode, raid.GetOwner(), raid.GetRaiders(), raid.BaseName });
                    }

                    if (config.Settings.Management.PlayersLootableInPVE && !raid.AllowPVP || config.Settings.Management.PlayersLootableInPVP && raid.AllowPVP)
                    {
                        container.playerSteamID = 0;
                    }

                    return;
                }

            done:

                if (config.Settings.Management.PlayersLootableInPVE && !raid.AllowPVP || config.Settings.Management.PlayersLootableInPVP && raid.AllowPVP)
                {
                    corpse.playerSteamID = 0;
                }
            }
            else if (raid.npcs.RemoveAll(npc => npc.IsNull() || npc.userID == corpse.playerSteamID) > 0)
            {
                if (raid.Options.NPC.DespawnInventory)
                {
                    corpse.Invoke(corpse.SafelyKill, 30f);
                }

                Npcs.Remove(corpse.playerSteamID);

                HumanoidBrain brain;
                if (HumanoidBrains.TryGetValue(corpse.playerSteamID, out brain))
                {
                    raid.DropItems(corpse.containers, brain.attackType != HumanoidBrain.AttackType.BaseProjectile);
                }

                if (raid.Options.RespawnRateMax > 0f)
                {
                    raid.TryRespawnNpc();
                }
                else if (!AnyNpcs())
                {
                    Unsubscribe(nameof(OnNpcResume));
                    Unsubscribe(nameof(OnNpcDestinationSet));
                }
            }
        }

        private object CanBuild(Planner planner, Construction construction, Construction.Target target)
        {
            var buildPos = target.entity && target.entity.transform && target.socket ? target.GetWorldPosition() : target.position;
            var raid = RaidableBase.Get(buildPos);

            if (raid == null)
            {
                return null;
            }

            if (!raid.Options.AllowBuildingPriviledges && construction.prefabID == 2476970476) // TC
            {
                Message(target.player, "Cupboards are blocked!");
                return false;
            }
            else if (construction.prefabID == 2150203378) // LADDER
            {
                if (CanBuildLadders(target.player, raid))
                {
                    Raider ri;
                    if (raid.raiders.TryGetValue(target.player.userID, out ri) && ri.Input != null)
                    {
                        ri.Input.Restart();
                        ri.Input.TryPlace(ConstructionType.Ladder);
                    }
                }
                else
                {
                    Message(target.player, "Ladders are blocked!");
                    return false;
                }
            }
            else if (construction.fullName.Contains("/barricades/barricade."))
            {
                if (raid.Options.Barricades)
                {
                    Raider ri;
                    if (raid.raiders.TryGetValue(target.player.userID, out ri) && ri.Input != null)
                    {
                        ri.Input.Restart();
                        ri.Input.TryPlace(ConstructionType.Barricade);
                    }
                }
                else
                {
                    Message(target.player, "Barricades are blocked!");
                    return false;
                }
            }
            else if (!config.Settings.Management.AllowBuilding)
            {
                Message(target.player, "Building is blocked!");
                return false;
            }

            return null;
        }

        private bool CanBuildLadders(BasePlayer player, RaidableBase raid)
        {
            if (!config.Settings.Management.AllowLadders) return false;
            if (raid.Options.RequiresCupboardAccessLadders) return player.CanBuild();
            return true;
        }

        private void OnLootEntityEnd(BasePlayer player, StorageContainer container)
        {
            if (player.limitNetworking || container?.inventory == null || container.OwnerID.IsSteamId())
            {
                return;
            }

            var raid = RaidableBase.Get(container);

            if (raid == null)
            {
                return;
            }

            if (IsBox(container, true) || container is BuildingPrivlidge)
            {
                UI.UpdateStatusUI(raid);
            }

            if (raid.Options.DropTimeAfterLooting <= 0 || (raid.Options.DropOnlyBoxesAndPrivileges && !IsBox(container, true) && !(container is BuildingPrivlidge)))
            {
                return;
            }

            if (container.inventory.IsEmpty() && (container.ShortPrefabName == "box.wooden.large" || container.ShortPrefabName == "woodbox_deployed" || container.ShortPrefabName == "coffinstorage"))
            {
                container.Invoke(container.SafelyKill, 0.1f);
            }
            else container.Invoke(() => DropOrRemoveItems(container, raid.IsProtectedWeapon(container, true)), raid.Options.DropTimeAfterLooting);
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            RaidableBase.Get(entity.transform.position)?.OnLootEntityInternal(player, entity);
        }

        private object CanBePenalized(BasePlayer player)
        {
            var raid = RaidableBase.Get(player);

            if (raid != null &&
                (raid.Type == RaidableType.None ||
                raid.AllowPVP && !raid.Options.PenalizePVP ||
                !raid.AllowPVP && !raid.Options.PenalizePVE))
            {
                return false;
            }

            return null;
        }

        private void CanOpenBackpack(BasePlayer looter, ulong backpackOwnerID)
        {
            var raid = RaidableBase.Get(looter.transform.position);

            if (raid == null)
            {
                return;
            }

            if (!raid.AllowPVP && !config.Settings.Management.BackpacksOpenPVE || raid.AllowPVP && !config.Settings.Management.BackpacksOpenPVP)
            {
                looter.Invoke(looter.EndLooting, 0.01f);
                QueueNotification(looter, "NotAllowed");
            }
        }

        private bool CanDropPlayerBackpack(BasePlayer player, RaidableBase raid)
        {
            DelaySettings ds;
            if (PvpDelay.TryGetValue(player.userID, out ds) && (ds.AllowPVP && config.Settings.Management.BackpacksPVP || !ds.AllowPVP && config.Settings.Management.BackpacksPVE))
            {
                return true;
            }

            return raid != null && (raid.AllowPVP && config.Settings.Management.BackpacksPVP || !raid.AllowPVP && config.Settings.Management.BackpacksPVE);
        }

        private object CanBeTargeted(BasePlayer target, BaseEntity entity)
        {
            var success = CanEntityBeTargeted(target, entity);

            if (success is bool && (bool)success)
            {
                return null;
            }

            return success;
        }

        private object CanEntityBeTargeted(BasePlayer player, BaseEntity entity)
        {
            if (player.IsNull() || entity.IsNull() || player.limitNetworking)
            {
                return null;
            }

            if (entity is NPCAutoTurret && !player.userID.IsSteamId())
            {
                return null;
            }

            if (PvpDelay.ContainsKey(player.userID))
            {
                return true;
            }

            var raid = RaidableBase.Get(player.transform.position) ?? RaidableBase.Get(entity.transform.position);

            if (raid == null)
            {
                return null;
            }

            if (RaidableBase.Has(player.userID))
            {
                return !raid.Options.NPC.IgnoreTrapsTurrets && (entity.OwnerID.IsSteamId() || !raid.Entities.Contains(entity));
            }
            else if (player is ScientistNPC)
            {
                return InRange2D((player as ScientistNPC).spawnPos, raid.Location, raid.ProtectionRadius) ? !raid.Options.NPC.IgnoreTrapsTurrets : (object)null;
            }

            if (!raid.AllowPVP)
            {
                return raid.Entities.Contains(entity) && !raid.BuiltList.ContainsKey(entity);
            }

            return raid.Entities.Contains(entity) || raid.BuiltList.ContainsKey(entity) ? true : (object)null;
        }

        private object OnTrapTrigger(BaseTrap trap, GameObject go)
        {
            var player = go.GetComponent<BasePlayer>();
            var success = CanEntityTrapTrigger(trap, player);

            return success is bool && !(bool)success ? true : (object)null;
        }

        private object CanEntityTrapTrigger(BaseTrap trap, BasePlayer player)
        {
            if (player.IsNull() || player.limitNetworking)
            {
                return null;
            }

            if (RaidableBase.Has(player.userID))
            {
                return false;
            }

            return EventTerritory(player.transform.position) ? true : (object)null;
        }

        private void OnCupboardProtectionCalculated(BuildingPrivlidge priv, float cachedProtectedMinutes)
        {
            if (priv.OwnerID == 0 && RaidableBase.Has(priv))
            {
                priv.cachedProtectedMinutes = 0;
            }
        }

        private object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null || hitInfo.damageTypes == null || entity.IsNull() || entity.OwnerID == 1337422)
            {
                return null;
            }

            object success = entity is BasePlayer ? HandlePlayerDamage(entity as BasePlayer, hitInfo) : HandleEntityDamage(entity, hitInfo);

            if (success is bool && !(bool)success)
            {
                NullifyDamage(hitInfo);
                return false;
            }

            return success;
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo) => CanEntityTakeDamage(entity, hitInfo);

        private object HandlePlayerDamage(BasePlayer victim, HitInfo hitInfo)
        {
            if (RaidableBase.Has(victim.userID) && hitInfo.Initiator?.ShortPrefabName.Contains("external.high") == true)
            {
                return false;
            }

            var raid = RaidableBase.Get(victim, hitInfo);

            if (raid == null || raid.IsDespawning)
            {
                return null;
            }

            if (!raid.Options.MLRS && hitInfo.WeaponPrefab is MLRSRocket)
            {
                hitInfo.damageTypes = new DamageTypeList();
                hitInfo.DoHitEffects = false;
                hitInfo.DidHit = false;
                return null;
            }

            var weapon = hitInfo.Initiator;

            if (IsTrueDamage(weapon, raid.IsProtectedWeapon(weapon)))
            {
                return HandleTrueDamage(raid, hitInfo, weapon, victim);
            }

            var attacker = raid.GetInitiatorPlayer(hitInfo, victim);

            if (attacker.IsReallyValid())
            {
                return HandleAttacker(attacker, victim, hitInfo, raid);
            }
            else if (RaidableBase.Has(victim.userID))
            {
                return false;
            }

            return null;
        }

        private object HandleTrueDamage(RaidableBase raid, HitInfo hitInfo, BaseEntity weapon, BasePlayer victim)
        {
            if (victim is ScientistNPC && !RaidableBase.Has(victim.userID))
            {
                return null;
            }

            if (weapon is AutoTurret)
            {
                hitInfo.damageTypes.Scale(DamageType.Bullet, UnityEngine.Random.Range(raid.Options.AutoTurret.Min, raid.Options.AutoTurret.Max));

                if (RaidableBase.Has(victim.userID) && (raid.Options.NPC.IgnoreTrapsTurrets || RaidableBase.Has(weapon) && !raid.BuiltList.ContainsKey(weapon)))
                {
                    return false;
                }

                if (weapon.OwnerID.IsSteamId())
                {
                    if (InRange2D(weapon.transform.position, raid.Location, raid.ProtectionRadius))
                    {
                        return victim.IsHuman() ? raid.AllowPVP : true;
                    }

                    return raid.AllowPVP || !victim.IsHuman();
                }
            }

            return true;
        }

        private object HandleAttacker(BasePlayer attacker, BasePlayer victim, HitInfo hitInfo, RaidableBase raid)
        {
            if (raid.HasLockout(attacker) || RaidableBase.Has(attacker.userID) && RaidableBase.Has(victim.userID))
            {
                return false;
            }

            if (attacker.userID == victim.userID)
            {
                return true;
            }

            if (PvpDelay.ContainsKey(victim.userID))
            {
                if (EventTerritory(attacker.transform.position))
                {
                    return true;
                }

                if (config.Settings.Management.PVPDelayAnywhere && PvpDelay.ContainsKey(attacker.userID))
                {
                    return true;
                }
            }

            if (config.Settings.Management.PVPDelayDamageInside && PvpDelay.ContainsKey(attacker.userID) && InRange2D(raid.Location, victim.transform.position, raid.ProtectionRadius))
            {
                return true;
            }

            if (!victim.IsHuman() && attacker.IsHuman())
            {
                return HandleNpcVictim(raid, victim, attacker);
            }
            else if (victim.IsHuman() && attacker.IsHuman())
            {
                return HandlePVPDamage(raid, victim, attacker, hitInfo);
            }
            else if (RaidableBase.Has(attacker.userID))
            {
                return HandleNpcAttacker(raid, victim, attacker, hitInfo);
            }

            return null;
        }

        private object HandleNpcVictim(RaidableBase raid, BasePlayer victim, BasePlayer attacker)
        {
            HumanoidBrain brain;
            if (!HumanoidBrains.TryGetValue(victim.userID, out brain))
            {
                return true;
            }

            if (config.Settings.Management.BlockMounts && attacker.GetMounted() || raid.ownerId.IsSteamId() && raid.CanEject() && !raid.IsAlly(attacker))
            {
                return false;
            }

            if (CanBlockOutsideDamage(raid, attacker, raid.Options.NPC.BlockOutsideDamageToNpcsInside))
            {
                return false;
            }

            var e = attacker.HasParent() ? attacker.GetParentEntity() : null;

            if (!e.IsNull() && (e is ScrapTransportHelicopter || e is HotAirBalloon || e is CH47Helicopter))
            {
                return false;
            }

            if (!raid.Options.NPC.CanLeave && raid.Options.NPC.BlockOutsideDamageOnLeave && !InRange(attacker.transform.position, raid.Location, raid.ProtectionRadius))
            {
                brain.Forget();

                return false;
            }

            brain.SetTarget(attacker);

            return true;
        }

        private object HandlePVPDamage(RaidableBase raid, BasePlayer victim, BasePlayer attacker, HitInfo hitInfo)
        {
            if (!raid.AllowPVP || (!raid.Options.AllowFriendlyFire && raid.IsAlly(victim.userID, attacker.userID)))
            {
                return false;
            }

            if (CanBlockOutsideDamage(raid, attacker, raid.Options.BlockOutsideDamageToPlayersInside) && !(hitInfo.WeaponPrefab is MLRSRocket))
            {
                return false;
            }

            if (IsPVE())
            {
                if (!InRange(attacker.transform.position, raid.Location, raid.ProtectionRadius))
                {
                    return false;
                }

                return InRange(victim.transform.position, raid.Location, raid.ProtectionRadius);
            }

            return true;
        }

        private object HandleNpcAttacker(RaidableBase raid, BasePlayer victim, BasePlayer attacker, HitInfo hitInfo)
        {
            HumanoidBrain brain;
            if (!Instance.HumanoidBrains.TryGetValue(attacker.userID, out brain))
            {
                return true;
            }

            if (RaidableBase.Has(victim.userID) || (InRange2D(attacker.transform.position, raid.Location, raid.ProtectionRadius) && CanBlockOutsideDamage(raid, victim, raid.Options.BlockNpcDamageToPlayersOutside)))
            {
                return false;
            }

            if (brain.SenseRange <= brain.softLimitSenseRange && hitInfo.IsProjectile() && UnityEngine.Random.Range(0f, 100f) > raid.Options.NPC.Accuracy)
            {
                return false;
            }

            if (hitInfo.damageTypes.GetMajorityDamageType() == DamageType.Explosion)
            {
                hitInfo.UseProtection = false;
            }

            if (brain.attackType == HumanoidBrain.AttackType.BaseProjectile)
            {
                hitInfo.damageTypes.ScaleAll(raid.Options.NPC.Multipliers.ProjectileDamageMultiplier);
            }
            else if (brain.attackType == HumanoidBrain.AttackType.Explosive)
            {
                hitInfo.damageTypes.ScaleAll(raid.Options.NPC.Multipliers.ExplosiveDamageMultiplier);
            }
            else if (brain.attackType == HumanoidBrain.AttackType.Melee)
            {
                hitInfo.damageTypes.ScaleAll(raid.Options.NPC.Multipliers.MeleeDamageMultiplier);
            }

            return true;
        }

        private object HandleEntityDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo.damageTypes.GetMajorityDamageType() == DamageType.Decay && RaidableBase.Has(entity))
            {
                return false;
            }

            var raid = RaidableBase.Get(entity.transform.position);

            if (raid == null || raid.IsDespawning)
            {
                return null;
            }

            if (!raid.Options.MLRS && hitInfo.WeaponPrefab is MLRSRocket)
            {
                hitInfo.damageTypes = new DamageTypeList();
                hitInfo.DoHitEffects = false;
                hitInfo.DidHit = false;
                return null;
            }

            if (raid.Options.Setup.FoundationsImmune && raid.Options.Setup.ForcedHeight != -1f)
            {
                if (raid.foundations.Count > 0 && entity.ShortPrefabName.StartsWith("foundation"))
                {
                    return false;
                }

                if (raid.foundations.Count == 0 && entity.ShortPrefabName.StartsWith("floor") && entity.transform.position.y - raid.Location.y <= 3f)
                {
                    return false;
                }
            }

            if (raid.Options.BlocksImmune && entity is BuildingBlock)
            {
                return false;
            }

            if (raid.Options.TwigImmune && entity is BuildingBlock && (entity as BuildingBlock).grade == BuildingGrade.Enum.Twigs)
            {
                return false;
            }

            if (entity.IsNpc || entity is PlayerCorpse)
            {
                return true;
            }

            if (entity is BaseMountable || entity.name.Contains("modularcar"))
            {
                if (hitInfo.Initiator is SamSite)
                {
                    return config.Settings.Management.MountDamageFromSamSites;
                }

                if (!config.Settings.Management.MountDamageFromPlayers && !ExcludedMounts.Contains(entity.prefabID) && hitInfo.Initiator is BasePlayer)
                {
                    raid.TryMessage(hitInfo.Initiator as BasePlayer, "NoMountedDamageTo");
                    return false;
                }
            }

            bool hasEntity = RaidableBase.Has(entity);

            if (!hasEntity && entity is DecayEntity && (entity as DecayEntity).buildingID != raid.BuildingID)
            {
                return null;
            }

            if (config.Settings.Management.BlockHelicopterDamage && hasEntity && !hitInfo.Initiator.IsNull() && IsHelicopter(hitInfo))
            {
                return false;
            }

            if (hitInfo.Initiator == entity && entity is AutoTurret)
            {
                return false;
            }

            var attacker = raid.GetInitiatorPlayer(hitInfo, entity);

            if (!attacker.IsReallyValid())
            {
                return null;
            }

            if (!attacker.IsHuman())
            {
                if (hitInfo.damageTypes.Has(DamageType.Explosion) && RaidableBase.Has(attacker.userID))
                {
                    if (entity.OwnerID == 0uL && !(entity is BasePlayer))
                    {
                        return false;
                    }

                    return raid.BuiltList.ContainsKey(entity);
                }

                return true;
            }

            entity.lastAttacker = attacker;
            attacker.lastDealtDamageTime = Time.time;

            if (config.Settings.Management.BlockMounts && (attacker.GetMounted() || attacker.GetParentEntity() is BaseMountable))
            {
                raid.TryMessage(attacker, "NoMountedDamageFrom");
                return false;
            }

            if (CanBlockOutsideDamage(raid, attacker, raid.Options.BlockOutsideDamageToBaseInside) && !(hitInfo.WeaponPrefab is MLRSRocket))
            {
                raid.TryMessage(attacker, "NoDamageFromOutsideToBaseInside");
                return false;
            }

            if (raid.ID.IsSteamId() && IsBox(entity, false) && (attacker.UserIDString == raid.ID || raid.IsAlly(attacker.userID, Convert.ToUInt64(raid.ID))))
            {
                return false;
            }

            if (raid.ownerId.IsSteamId() && raid.CanEject() && !raid.IsAlly(attacker))
            {
                raid.TryMessage(attacker, "NoDamageToEnemyBase");
                return false;
            }

            if (raid.HasLockout(attacker))
            {
                return false;
            }

            if (raid.Options.AutoTurret.AutoAdjust && raid.turrets.Count > 0 && entity is AutoTurret)
            {
                var turret = entity as AutoTurret;

                if (raid.turrets.Contains(turret) && turret.sightRange <= raid.Options.AutoTurret.SightRange)
                {
                    turret.sightRange = raid.Options.AutoTurret.SightRange * 2f;
                }
            }

            if (!raid.Options.ExplosionModifier.Equals(100) && hitInfo.damageTypes.Has(DamageType.Explosion))
            {
                float m = Mathf.Clamp(raid.Options.ExplosionModifier, 0f, 999f);

                hitInfo.damageTypes.Scale(DamageType.Explosion, m.Equals(0f) ? 0f : m / 100f);
            }

            if (raid.BuiltList.ContainsKey(entity))
            {
                return true;
            }

            if (raid.Type != RaidableType.None)
            {
                raid.IsEngaged = true;
                raid.CheckDespawn();
            }

            if (raid.IsOpened && IsLootingWeapon(hitInfo) && raid.AddLooter(attacker, hitInfo))
            {
                raid.TrySetOwner(attacker, entity, hitInfo);
            }

            if (raid.Options.Invulnerable && IsBox(entity, true))
            {
                return false;
            }

            return true;
        }

        #endregion Hooks

        #region Spawn

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

        private static Vector3 GetBuildingPrivilege(Vector3 target, float radius)
        {
            foreach (var tc in FindEntitiesOfType<BuildingPrivlidge>(target, radius))
            {
                if (!tc.IsKilled() && !RaidableBase.Has(tc))
                {
                    return tc.transform.position;
                }
            }
            return Vector3.zero;
        }

        public RaidableBase OpenEvent(RandomBase x)
        {
            var raid = new GameObject().AddComponent<RaidableBase>();

            raid.MyInstance = this;
            raid.name = Name;
            raid.Definitions = _definitions;
            raid.SetAllowPVP(x.Type, x.Profile.Options.AllowPVP);
            raid.PastedLocation = x.Position;
            raid.Options = x.Profile.Options;
            raid.BaseName = x.BaseName;
            raid.BaseIndex = x.BaseIndex;
            raid.ProfileName = x.Profile.Name;

            if (config.Settings.NoWizardry && Wizardry.CanCall())
            {
                Subscribe(nameof(OnActiveItemChanged));
            }

            if (config.Settings.BlacklistedCommands.Count > 0)
            {
                Subscribe(nameof(OnPlayerCommand));
                Subscribe(nameof(OnServerCommand));
            }

            if (!IsPVE())
            {
                Subscribe(nameof(OnEntityTakeDamage));
            }

            Subscribe(nameof(CanEntityTakeDamage));
            Subscribe(nameof(OnEntityEnter));
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(CanBuild));

            data.TotalEvents++;
            raid.UndoInit();

            Raids.Add(raid);

            return raid;
        }

        #endregion

        #region Paste

        protected bool IsGridLoading
        {
            get
            {
                return GridController.gridCoroutine != null;
            }
        }

        protected bool IsPasteAvailable
        {
            get
            {
                return !Raids.Exists(raid => raid.IsLoading);
            }
        }

        private Payment TryBuyRaidServerRewards(BasePlayer buyer, BasePlayer player, RaidableMode mode, out bool isValid)
        {
            isValid = false;

            if (config.Settings.ServerRewards.Any && ServerRewards.CanCall())
            {
                int cost = mode == RaidableMode.Easy ? config.Settings.ServerRewards.Easy : mode == RaidableMode.Medium ? config.Settings.ServerRewards.Medium : mode == RaidableMode.Hard ? config.Settings.ServerRewards.Hard : mode == RaidableMode.Expert ? config.Settings.ServerRewards.Expert : config.Settings.ServerRewards.Nightmare;

                if (cost > 0)
                {
                    isValid = true;

                    var success = ServerRewards?.Call("CheckPoints", buyer.userID);
                    int points = success is int ? Convert.ToInt32(success) : 0;

                    if (points > 0 && points - cost >= 0)
                    {
                        return new Payment(cost, 0, buyer, player, player.transform.position);
                    }
                    else Message(buyer, "ServerRewardPointsFailed", cost);
                }
            }

            return null;
        }

        private Payment TryBuyRaidEconomics(BasePlayer buyer, BasePlayer player, RaidableMode mode, out bool isValid)
        {
            isValid = false;

            if (config.Settings.Economics.Any && Economics.CanCall() || config.Settings.Economics.Any && IQEconomic.CanCall())
            {
                var cost = mode == RaidableMode.Easy ? config.Settings.Economics.Easy : mode == RaidableMode.Medium ? config.Settings.Economics.Medium : mode == RaidableMode.Hard ? config.Settings.Economics.Hard : mode == RaidableMode.Expert ? config.Settings.Economics.Expert : config.Settings.Economics.Nightmare;

                if (cost > 0)
                {
                    isValid = true;

                    var points = Convert.ToDouble(Economics?.Call("Balance", buyer.UserIDString));

                    if (points > 0 && points - cost >= 0)
                    {
                        return new Payment(0, cost, buyer, player, player.transform.position);
                    }

                    var money = Convert.ToInt32(IQEconomic?.Call("API_GET_BALANCE", buyer.userID));

                    if (money > 0 && money - cost >= 0)
                    {
                        return new Payment(0, cost, buyer, player, player.transform.position);
                    }

                    Message(buyer, "EconomicsWithdrawFailed", cost);
                }
            }

            return null;
        }

        private Payment TryBuyRaidCustom(BasePlayer buyer, BasePlayer player, RaidableMode mode, out bool isValid)
        {
            isValid = false;

            List<RaidableBaseCustomCostOptions> options;
            if (config.Settings.Custom.TryGetValue(mode, out options) && options.All(o => o.IsReallyValid()))
            {
                isValid = true;

                foreach (var option in options)
                {
                    var slots = buyer.inventory.FindItemIDs(option.Definition.itemid);
                    int amount = 0;

                    foreach (var slot in slots)
                    {
                        if (option.Skin != 0 && slot.skin != option.Skin)
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(option.Name) && slot.name != option.Name)
                        {
                            continue;
                        }

                        amount += slot.amount;

                        if (amount >= option.Amount)
                        {
                            break;
                        }
                    }

                    if (amount < option.Amount)
                    {
                        string name = string.IsNullOrEmpty(option.Name) ? option.Shortname : option.Name;
                        Message(buyer, "CustomWithdrawFailed", string.Format("{0} ({1})", name, option.Amount));
                        return null;
                    }
                }

                return new Payment(options, buyer, player, player.transform.position);
            }

            return null;
        }

        public class Payment
        {
            public Payment(List<RaidableBaseCustomCostOptions> options, BasePlayer buyer, BasePlayer owner, Vector3 position)
            {
                userId = buyer?.userID ?? owner?.userID ?? 0;
                self = buyer?.userID == owner?.userID;
                buyerName = buyer?.displayName;
                Options = options;
                this.buyer = buyer;
                this.owner = owner;
                this.position = position;
            }

            public Payment(int RP, double money, BasePlayer buyer, BasePlayer owner, Vector3 position)
            {
                userId = buyer?.userID ?? owner?.userID ?? 0;
                self = buyer?.userID == owner?.userID;
                buyerName = buyer?.displayName;
                this.RP = RP;
                this.money = money;
                this.buyer = buyer;
                this.owner = owner;
                this.position = position;
            }

            public List<RaidableBaseCustomCostOptions> Options { get; set; }
            public int RP { get; set; }
            public double money { get; set; }
            public ulong userId { get; set; }
            public BasePlayer buyer { get; set; }
            public BasePlayer owner { get; set; }
            public bool self { get; set; }
            public string buyerName { get; set; }
            public Vector3 position { get; set; }

            public static bool IsValid(Payment payment)
            {
                if (payment.IsNull() || payment.owner.IsNull() || payment.buyer.IsNull())
                {
                    return false;
                }

                return true;
            }

            public static Vector3 GetPosition(List<Payment> payments)
            {
                if (payments.IsNullOrEmpty() || !payments.Exists(x => x.userId.IsSteamId()))
                {
                    return Vector3.zero;
                }

                foreach (var payment in payments)
                {
                    if (payment.position == Vector3.zero)
                    {
                        continue;
                    }

                    return payment.position;
                }

                return Vector3.zero;
            }

            public void RefundItems()
            {
                _sb.Clear();

                foreach (var option in Options)
                {
                    Item item = ItemManager.CreateByItemID(option.Definition.itemid, option.Amount, option.Skin);

                    string name;
                    if (!string.IsNullOrEmpty(option.Name))
                    {
                        name = item.name = option.Name;
                    }
                    else name = item.info.displayName.english;

                    buyer.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);

                    _sb.Append(string.Format("{0} {1}", option.Amount, name)).Append(", ");
                }

                if (_sb.Length > 2)
                {
                    _sb.Length -= 2;

                    Message(buyer, "Refunded", _sb.ToString());
                }
            }

            public void TakeItems()
            {
                var sb = new StringBuilder();

                foreach (var option in Options)
                {
                    var slots = buyer.inventory.FindItemIDs(option.Definition.itemid);
                    var amountLeft = option.Amount;

                    foreach (var slot in slots)
                    {
                        if (slot == null || option.Skin != 0 && slot.skin != option.Skin)
                        {
                            continue;
                        }

                        var taken = slot.amount > amountLeft ? slot.SplitItem(amountLeft) : slot;
                        taken.Drop(Vector3.zero, Vector3.zero);
                        amountLeft -= taken.amount;

                        if (amountLeft <= 0)
                        {
                            string name = string.IsNullOrEmpty(option.Name) ? slot.info.displayName.english : option.Name;
                            sb.Append(string.Format("{0} {1}", option.Amount, name)).Append(", ");
                            break;
                        }
                    }
                }

                if (sb.Length > 2)
                {
                    sb.Length -= 2;

                    if (!self)
                    {
                        Message(owner, "CustomWithdrawGift", buyerName, sb.ToString());
                    }

                    Message(buyer, "CustomWithdraw", sb.ToString());
                }
            }

            public void TakeMoney()
            {
                Instance.Economics?.Call("Withdraw", userId.ToString(), money);

                Instance.IQEconomic?.Call("API_REMOVE_BALANCE", userId, (int)money);

                if (!self)
                {
                    Message(owner, "EconomicsWithdrawGift", buyerName, money);
                }

                Message(buyer, "EconomicsWithdraw", money);
            }

            public void TakePoints()
            {
                Instance.ServerRewards?.Call("TakePoints", userId, RP);

                if (!self)
                {
                    Message(owner, "ServerRewardPointsGift", buyerName, RP);
                }

                Message(buyer, "ServerRewardPointsTaken", RP);
            }
        }

        private bool BuyRaid(RaidableMode mode, List<Payment> payments, BasePlayer owner)
        {
            string message;
            var randomBase = SpawnRandomBase(out message, RaidableType.Purchased, mode, null, false, payments, owner.UserIDString);
            if (randomBase != null)
            {
                var grid = FormatGridReference(randomBase.Position);

                Message(owner, "BuyBaseSpawnedAt", randomBase.Position, grid);

                if (config.EventMessages.AnnounceBuy)
                {
                    foreach (var target in BasePlayer.activePlayerList)
                    {
                        QueueNotification(target, "BuyBaseAnnouncement", owner.displayName, randomBase.Position, grid);
                    }
                }

                Puts(mx("BuyBaseAnnouncementConsole", null, owner.displayName, mode, randomBase.BaseName, randomBase.Position, grid));

                return true;
            }

            QueueNotification(owner, "CannotFindPosition");

            return false;
        }

        private bool IsDifficultyAvailable(RaidableMode mode, bool checkAllowPVP)
        {
            if (!CanSpawnDifficultyToday(mode))
            {
                return false;
            }

            foreach (var profile in Buildings.Profiles.Values)
            {
                if (profile.Options.Mode != mode || (checkAllowPVP && !config.Settings.Buyable.BuyPVP && profile.Options.AllowPVP))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool PasteBuilding(RandomBase x, out string message)
        {
            if (x.Profile.Options.Water.Seabed > 0f && x.Profile.Options.Water.SpawnOnSeabed)
            {
                var h = TerrainMeta.HeightMap.GetHeight(x.Position);

                if (TerrainMeta.WaterMap.GetHeight(x.Position) > h)
                {
                    x.Position.y = h;
                }
            }
            else if (x.Profile.Options.Setup.ForcedHeight != -1)
            {
                x.Position.y = x.Profile.Options.Setup.ForcedHeight;
            }
            else if (x.Profile.Options.Setup.Recalculate)
            {
                int pasteLayer = LayerMask.GetMask("Construction", "Deployed", "Tree", "Terrain", "World", "Water", "Prevent Building");
                RaycastHit hit;
                if (Physics.Raycast(x.Position + new Vector3(0f, 200f, 0f), Vector3.down, out hit, 500f, pasteLayer))
                {
                    x.Position = hit.point;
                }
            }

            x.Position.y += x.Profile.Options.Setup.PasteHeightAdjustment;

            if (Locations.Exists(y => y.Position == x.Position))
            {
                message = $"{x.Position} has a base already.";
                return false;
            }

            LoadingTimes[x.Position] = Time.time;

            List<PasteOption> options = x.Profile.Options.PasteOptions;

            foreach (var kvp in x.Profile.Options.AdditionalBases)
            {
                if (kvp.Key.Equals(x.BaseName, StringComparison.OrdinalIgnoreCase))
                {
                    options = kvp.Value;
                    break;
                }
            }

            var list = GetListedOptions(options, x.Profile.Options.Stability);

            Locations.Add(x);

            x.InitializeController();

            Subscribe(nameof(OnEntitySpawned));

            CopyPaste.Call("TryPasteFromVector3", x.Position, 0f, x.BaseName, list.ToArray());

            message = $"{x.BaseName} trying to paste at {x.Position}";

            return true;
        }

        private List<string> GetListedOptions(List<PasteOption> options, bool stability)
        {
            var list = new List<string>();
            bool flag1 = false, flag2 = false, flag3 = false, flag4 = false, flag5 = false;

            for (int i = 0; i < options.Count; i++)
            {
                string key = options[i].Key.ToLower();
                string value = options[i].Value.ToLower();

                if (key == "stability")
                {
                    flag1 = true;
                    value = stability ? "true" : "false";
                }
                if (key == "autoheight")
                {
                    flag2 = true;
                }
                if (key == "height")
                {
                    flag3 = true;
                }
                if (key == "entityowner")
                {
                    flag4 = true;
                    value = "false";
                }
                if (key == "auth")
                {
                    flag5 = true;
                    value = "false";
                }

                list.Add(key);
                list.Add(value);
            }

            if (!flag1)
            {
                list.Add("stability");
                list.Add(stability ? "true" : "false");
            }

            if (!flag2)
            {
                list.Add("autoheight");
                list.Add("false");
            }

            if (!flag3)
            {
                list.Add("height");
                list.Add("1.0");
            }

            if (!flag4)
            {
                list.Add("entityowner");
                list.Add("false");
            }

            if (!flag5)
            {
                list.Add("auth");
                list.Add("false");
            }

            return list;
        }

        private List<string> unityEngineScripts { get; set; } = new List<string> { "saddletest", "fuel_storage" };

        private void OnPasteFinished(List<BaseEntity> pastedEntities, string fileName)
        {
            if (pastedEntities.IsNullOrEmpty() || !Locations.Exists(x => x.BaseName == fileName && !x.hasSpawned))
            {
                return;
            }

            var randomBase = Locations.FirstOrDefault(x => x.BaseName == fileName && !x.hasSpawned);

            if (IsUnloading)
            {
                RaidableBase.IsSpawning = false;
                return;
            }

            pastedEntities.RemoveAll(e => e.IsKilled() || unityEngineScripts.Exists(e.ShortPrefabName.Contains));

            var bmgs = BMGELEVATOR.FixElevators(pastedEntities);
            var raid = OpenEvent(randomBase);

            raid.Init(randomBase, pastedEntities, bmgs);

            randomBase.hasSpawned = true;
        }

        private Vector3 GetEventPosition(BuildingOptions options, List<Payment> payments, float distanceFrom, bool checkTerrain, RaidableSpawns spawns, RaidableType type, out string message, float scanDistance = -1f, Vector3 scanVector = default(Vector3))
        {
            //if (type == RaidableType.Purchased) distanceFrom = 5000f;
            spawns.Check();

            message = null;
            _totalattempts = 0;

            int attempts = 1000; //spawns.Count;
            Vector3 paymentPos = Payment.GetPosition(payments);
            float typeDistance = GetDistance(type);
            float protectionRadius = options.ProtectionRadius(type);
            float safeRadius = Mathf.Max(options.ArenaWalls.Radius, protectionRadius);
            float buildRadius = Mathf.Max(config.Settings.Management.CupboardDetectionRadius, options.ArenaWalls.Radius, protectionRadius) + 5f;
            int layers = Layers.Mask.Player_Server | Layers.Mask.Construction | Layers.Mask.Deployed | Layers.Mask.Ragdoll;
            CacheType cacheType;

            while (spawns.Count > 0 && --attempts > 0)
            {
                _totalattempts++;
                
                var rsl = spawns.GetRandom(options.Water);
                var vector = rsl.Location;
                
                if (options.Setup.ForcedHeight != -1)
                {
                    vector.y = options.Setup.ForcedHeight;
                }
                else vector.y += options.Setup.PasteHeightAdjustment;

                if (scanDistance > 0 && !InRange2D(vector, scanVector, scanDistance))
                {
                    message = "Not close enough to specified parameter";
                    continue;
                }

                if (typeDistance > 0 && RaidableBase.IsTooClose(vector, typeDistance))
                {
                    spawns.RemoveNear(vector, options.ProtectionRadius(type), CacheType.Close, type);
                    message = "Too close to another raidable base";
                    continue;
                }
                
                if (spawns.IsCustomSpawn && (options.CustomSpawns.Ignore || options.CustomSpawns.SafeRadius > 0f))
                {
                    if (options.CustomSpawns.SafeRadius <= 0f)
                    {
                        message = $"Ignoring safe checks enabled for {options.Mode} events; returning {vector}";
                        return vector;
                    }
                    else safeRadius = options.CustomSpawns.SafeRadius;
                }
                
                if (type == RaidableType.Maintained && spawns.IsCustomSpawn && (config.Settings.Maintained.Ignore || config.Settings.Maintained.SafeRadius > 0f))
                {
                    if (config.Settings.Maintained.SafeRadius <= 0f)
                    {
                        message = $"Ignoring safe checks enabled for maintained events; returning {vector}";
                        return vector;
                    }
                    else safeRadius = config.Settings.Maintained.SafeRadius;
                }
                
                if (type == RaidableType.Scheduled && spawns.IsCustomSpawn && (config.Settings.Schedule.Ignore || config.Settings.Schedule.SafeRadius > 0f))
                {
                    if (config.Settings.Schedule.SafeRadius <= 0f)
                    {
                        message = $"Ignoring safe checks enabled for scheduled events; returning {vector}";
                        return vector;
                    }
                    else safeRadius = config.Settings.Schedule.SafeRadius;
                }
                
                if (type == RaidableType.Purchased && spawns.IsCustomSpawn && (config.Settings.Buyable.Ignore || config.Settings.Buyable.SafeRadius > 0f))
                {
                    if (config.Settings.Buyable.SafeRadius <= 0f)
                    {
                        message = $"Ignoring safe checks enabled for buyable events; returning {vector}";
                        return vector;
                    }
                    else safeRadius = config.Settings.Buyable.SafeRadius;
                }
                
                if (!spawns.IsCustomSpawn && options.Setup.ForcedHeight == -1f && options.Water.Seabed <= 0f && SpawnsController.IsSubmerged(options.Water, rsl))
                {
                    continue;
                }
                
                if (paymentPos != Vector3.zero && distanceFrom > 0 && !InRange2D(paymentPos, vector, distanceFrom))
                {
                    continue;
                }
                
                var sac = new SpawnsController.SafeAreaCheck(vector, safeRadius, buildRadius, layers, type, options.CustomSpawns);
                
                if (!SpawnsController.IsAreaSafe(sac, out cacheType, out message))
                {
                    if (cacheType == CacheType.Delete)
                    {
                        spawns.Remove(rsl, cacheType);
                    }
                    else if (cacheType == CacheType.Privilege)
                    {
                        spawns.RemoveNear(vector, buildRadius, cacheType, type);
                    }
                    else spawns.RemoveNear(vector, safeRadius / 2f, cacheType, type);

                    if (string.IsNullOrEmpty(message))
                    {
                        message = "Failed safe check; trying again...";
                    }
                    continue;
                }
                
                if (!spawns.IsCustomSpawn && SpawnsController.IsObstructed(vector, protectionRadius, options.Elevation, options.Setup.ForcedHeight))
                {
                    spawns.RemoveNear(vector, protectionRadius / 2f, CacheType.Temporary, type);
                    continue;
                }
                
                PrintDebugMessage($"Attempts before finding position to paste base: {_totalattempts}");

                return vector;
            }
            
            spawns.TryAddRange();
            
            if (paymentPos != Vector3.zero && spawns.Count > 0 && distanceFrom < 5000f)
            {
                return GetEventPosition(options, payments, 5000f, checkTerrain, spawns, type, out message);
            }
            
            if (message == null)
            {
                message = mx("CannotFindPosition");
            }
            
            return Vector3.zero;
        }

        private int _totalattempts = 0;

        private object SpawnRandomBase(string baseName, Vector3 a, int type = 1)
        {
            lastSpawnRequestTime = Time.realtimeSinceStartup;
            if (RaidableBase.IsSpawning)
            {
                return 1;
            }

            var profile = Buildings.Profiles.FirstOrDefault(x => x.Key.Equals(baseName, StringComparison.OrdinalIgnoreCase));
            if (!IsProfileValid(profile))
            {
                return 2;
            }

            bool checkTerrain;
            var spawns = GetSpawns((RaidableType)type, profile.Value, out checkTerrain);
            if (spawns == null)
            {
                return 3;
            }

            string message;
            if (PasteBuilding(new RandomBase(profile.Key, profile.Value, a, (RaidableType)type, spawns, null), out message))
            {
                return RaidableBase.IsSpawning = true;
            }

            return message;
        }

        private RandomBase SpawnRandomBase(out string message, RaidableType type, RaidableMode mode, string baseName = null, bool isAdmin = false, List<Payment> payments = null, string userid = null, float distance = -1, Vector3 scanVector = default(Vector3))
        {
            lastSpawnRequestTime = Time.realtimeSinceStartup;

            if (RaidableBase.IsSpawning)
            {
                message = "Base is spawning already.";
                return null;
            }

            message = string.Empty;

            var profile = GetBuilding(type, mode, baseName);
            bool checkTerrain, validProfile = IsProfileValid(profile);
            var spawns = GetSpawns(type, profile.Value, out checkTerrain);

            if (validProfile && spawns != null)
            {
                var eventPos = GetEventPosition(profile.Value.Options, payments, config.Settings.Buyable.DistanceToSpawnFrom, checkTerrain, spawns, type, out message, distance, scanVector);

                if (eventPos != Vector3.zero)
                {
                    var x = new RandomBase(profile.Key, profile.Value, eventPos, type, spawns, payments);

                    if (PasteBuilding(x, out message))
                    {
                        RaidableBase.IsSpawning = true;
                        message = $"Pasting building {profile.Key} at {eventPos}";

                        return x;
                    }
                }
            }

            if (type == RaidableType.Maintained || type == RaidableType.Scheduled)
            {
                PrintDebugMessage(message);

                return null;
            }

            message = GetDebugMessage(mode, validProfile, isAdmin, userid, baseName, profile.Value?.Options, message);

            return null;
        }

        private List<string> _messages = new List<string>();

        private static void PrintDebugMessage(string message)
        {
            if (!string.IsNullOrEmpty(message) && Instance != null)
            {
                Instance._messages.Add(message);

                if (Instance._messages.Count > 10)
                {
                    Instance._messages.RemoveAt(0);
                }

                if (Instance.DebugMode)
                {
                    Puts("DEBUG: {0}", message);
                    Instance.LogToFile("debug", message, Instance, true);
                }
            }
        }

        private string GetDebugMessage(RaidableMode mode, bool validProfile, bool isAdmin, string id, string baseName, BuildingOptions options, string message)
        {
            if (options != null)
            {
                if (!options.Enabled)
                {
                    return mx("Profile Not Enabled", id, baseName);
                }
                else if (options.Mode == RaidableMode.Disabled)
                {
                    return mx("Difficulty Disabled", id, baseName);
                }
            }

            if (!validProfile)
            {
                return message;
            }

            if (!string.IsNullOrEmpty(baseName))
            {
                if (!FileExists(baseName))
                {
                    return mx("FileDoesNotExist", id);
                }
                else if (!Buildings.IsConfigured(baseName))
                {
                    return mx("BuildingNotConfigured", id);
                }
            }

            if (!IsDifficultyAvailable(mode, options?.AllowPVP ?? false) && mode != RaidableMode.Random)
            {
                return mx(isAdmin ? "Difficulty Not Available Admin" : "Difficulty Not Available", id, (int)mode);
            }
            else if (Buildings.Profiles.Count == 0)
            {
                return mx("NoBuildingsConfigured", id);
            }

            return message;
        }

        private RaidableSpawns GetSpawns(RaidableType type, BaseProfile profile, out bool checkTerrain)
        {
            RaidableSpawns spawns;
            checkTerrain = false;

            if (profile?.Spawns.IsCustomSpawn == true)
            {
                return profile.Spawns;
            }

            switch (type)
            {
                case RaidableType.Maintained: if (GridController.Spawns.TryGetValue(RaidableType.Maintained, out spawns)) return spawns; break;
                case RaidableType.Manual: if (GridController.Spawns.TryGetValue(RaidableType.Manual, out spawns)) return spawns; break;
                case RaidableType.Purchased: if (GridController.Spawns.TryGetValue(RaidableType.Purchased, out spawns)) return spawns; break;
                case RaidableType.Scheduled: if (GridController.Spawns.TryGetValue(RaidableType.Scheduled, out spawns)) return spawns; break;
            }

            checkTerrain = true;
            return GridController.Spawns.TryGetValue(RaidableType.Grid, out spawns) ? spawns : null;
        }

        private KeyValuePair<string, BaseProfile> GetBuilding(RaidableType type, RaidableMode mode, string baseName)
        {
            var list = new List<KeyValuePair<string, BaseProfile>>();
            bool isBaseNull = string.IsNullOrEmpty(baseName);
            string last = "Start of selection";

            foreach (var profile in Buildings.Profiles)
            {
                if (MustExclude(type, profile.Value.Options.AllowPVP) || !IsBuildingAllowed(type, mode, profile.Value.Options.Mode, profile.Value.Options.AllowPVP))
                {
                    last = "Profile excluded or building not allowed";
                    continue;
                }

                if (FileExists(profile.Key) && Cycle.CanSpawn(type, mode, profile.Key))
                {
                    if (isBaseNull)
                    {
                        list.Add(profile);
                    }
                    else if (profile.Key.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                    {
                        return profile;
                    }
                }
                else last = $"Profile {profile.Key} either does not exist, or cannot be spawned again yet.";

                foreach (var extra in profile.Value.Options.AdditionalBases)
                {
                    if (!FileExists(extra.Key) || !Cycle.CanSpawn(type, mode, extra.Key))
                    {
                        last = $"Additional Base {extra.Key} of {profile.Key} profile either does not exist, or cannot be spawned again yet.";
                        continue;
                    }

                    var clone = BaseProfile.Clone(profile.Value);
                    var kvp = new KeyValuePair<string, BaseProfile>(extra.Key, clone);

                    kvp.Value.Options.PasteOptions = new List<PasteOption>(extra.Value);

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

            if (list.Count == 0)
            {
                if (!AnyFileExists)
                {
                    PrintDebugMessage("No copypaste file in any profile exists?");
                }
                else PrintDebugMessage($"No building was available for random selection of {mode} difficulty for {type} event.");

                PrintDebugMessage($"Last message: {last}");

                return default(KeyValuePair<string, BaseProfile>);
            }

            return list.GetRandom();
        }

        private static bool IsProfileValid(KeyValuePair<string, BaseProfile> profile)
        {
            if (string.IsNullOrEmpty(profile.Key) || profile.Value == null || profile.Value.Options == null)
            {
                return false;
            }

            return profile.Value.Options.Mode != RaidableMode.Disabled && profile.Value.Options.Enabled;
        }

        private RaidableMode GetRandomDifficulty(RaidableType type)
        {
            var modes = new List<RaidableMode>();

            foreach (RaidableMode mode in Enum.GetValues(typeof(RaidableMode)))
            {
                if (!CanSpawnDifficultyToday(mode))
                {
                    continue;
                }

                int max = config.Settings.Management.Amounts.Get(mode);

                if (max < 0 || max > 0 && RaidableBase.Get(mode) >= max)
                {
                    continue;
                }

                foreach (var profile in Buildings.Profiles.Values)
                {
                    if (profile.Options.Mode == mode && !MustExclude(type, profile.Options.AllowPVP))
                    {
                        modes.Add(mode);
                        break;
                    }
                }
            }

            if (modes.Count > 0)
            {
                if (config.Settings.Management.Chances.Cumulative)
                {
                    return GetRandomDifficulty(modes);
                }

                decimal chance = Convert.ToDecimal(Core.Random.Range(0.0, 100.0));

                if (chance <= config.Settings.Management.Chances.Easy && modes.Contains(RaidableMode.Easy))
                {
                    return RaidableMode.Easy;
                }
                else if (chance <= config.Settings.Management.Chances.Medium && modes.Contains(RaidableMode.Medium))
                {
                    return RaidableMode.Medium;
                }
                else if (chance <= config.Settings.Management.Chances.Hard && modes.Contains(RaidableMode.Hard))
                {
                    return RaidableMode.Hard;
                }
                else if (chance <= config.Settings.Management.Chances.Expert && modes.Contains(RaidableMode.Expert))
                {
                    return RaidableMode.Expert;
                }
                else if (chance <= config.Settings.Management.Chances.Nightmare && modes.Contains(RaidableMode.Nightmare))
                {
                    return RaidableMode.Nightmare;
                }

                return modes.GetRandom();
            }

            return RaidableMode.Random;
        }

        private RaidableMode GetRandomDifficulty(List<RaidableMode> modes)
        {
            var elements = modes.ToDictionary(mode => mode, mode => config.Settings.Management.Chances.Get(mode));
            decimal chance = Convert.ToDecimal(Core.Random.Range(0.0, 100.0));
            decimal cumulative = 0.0m;

            foreach (var element in elements)
            {
                cumulative += element.Value;

                if (chance < cumulative)
                {
                    return element.Key;
                }
            }

            return modes.GetRandom();
        }

        private bool AnyFileExists;

        private static bool FileExists(string file)
        {
            if (!file.Contains(Path.DirectorySeparatorChar.ToString()))
            {
                bool exists = Interface.Oxide.DataFileSystem.ExistsDatafile($"copypaste{Path.DirectorySeparatorChar}{file}");

                if (exists)
                {
                    Instance.AnyFileExists = true;
                }

                return exists;
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
                        if (!CanSpawnDifficultyToday(buildingMode) || !config.Settings.Buyable.BuyPVP && allowPVP)
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
                case DayOfWeek.Monday: return mode == RaidableMode.Easy ? config.Settings.Management.Easy.Monday : mode == RaidableMode.Medium ? config.Settings.Management.Medium.Monday : mode == RaidableMode.Hard ? config.Settings.Management.Hard.Monday : mode == RaidableMode.Expert ? config.Settings.Management.Expert.Monday : mode == RaidableMode.Nightmare && config.Settings.Management.Nightmare.Monday;
                case DayOfWeek.Tuesday: return mode == RaidableMode.Easy ? config.Settings.Management.Easy.Tuesday : mode == RaidableMode.Medium ? config.Settings.Management.Medium.Tuesday : mode == RaidableMode.Hard ? config.Settings.Management.Hard.Tuesday : mode == RaidableMode.Expert ? config.Settings.Management.Expert.Tuesday : mode == RaidableMode.Nightmare && config.Settings.Management.Nightmare.Tuesday;
                case DayOfWeek.Wednesday: return mode == RaidableMode.Easy ? config.Settings.Management.Easy.Wednesday : mode == RaidableMode.Medium ? config.Settings.Management.Medium.Wednesday : mode == RaidableMode.Hard ? config.Settings.Management.Hard.Wednesday : mode == RaidableMode.Expert ? config.Settings.Management.Expert.Wednesday : mode == RaidableMode.Nightmare && config.Settings.Management.Nightmare.Wednesday;
                case DayOfWeek.Thursday: return mode == RaidableMode.Easy ? config.Settings.Management.Easy.Thursday : mode == RaidableMode.Medium ? config.Settings.Management.Medium.Thursday : mode == RaidableMode.Hard ? config.Settings.Management.Hard.Thursday : mode == RaidableMode.Expert ? config.Settings.Management.Expert.Thursday : mode == RaidableMode.Nightmare && config.Settings.Management.Nightmare.Thursday;
                case DayOfWeek.Friday: return mode == RaidableMode.Easy ? config.Settings.Management.Easy.Friday : mode == RaidableMode.Medium ? config.Settings.Management.Medium.Friday : mode == RaidableMode.Hard ? config.Settings.Management.Hard.Friday : mode == RaidableMode.Expert ? config.Settings.Management.Expert.Friday : mode == RaidableMode.Nightmare && config.Settings.Management.Nightmare.Friday;
                case DayOfWeek.Saturday: return mode == RaidableMode.Easy ? config.Settings.Management.Easy.Saturday : mode == RaidableMode.Medium ? config.Settings.Management.Medium.Saturday : mode == RaidableMode.Hard ? config.Settings.Management.Hard.Saturday : mode == RaidableMode.Expert ? config.Settings.Management.Expert.Saturday : mode == RaidableMode.Nightmare && config.Settings.Management.Nightmare.Saturday;
                default: return mode == RaidableMode.Easy ? config.Settings.Management.Easy.Sunday : mode == RaidableMode.Medium ? config.Settings.Management.Medium.Sunday : mode == RaidableMode.Hard ? config.Settings.Management.Hard.Sunday : mode == RaidableMode.Expert ? config.Settings.Management.Expert.Sunday : mode == RaidableMode.Nightmare && config.Settings.Management.Nightmare.Sunday;
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

            if (player.IsNull() || player.IPlayer == null)
            {
                return;
            }

            if (arg.Args[0] == "closeui")
            {
                CuiHelper.DestroyUi(player, UI.BuyablePanelName);
                return;
            }

            CommandBuyRaid(player.IPlayer, config.Settings.BuyCommand, arg.Args);
        }

        private void CommandReloadConfig(IPlayer user, string command, string[] args)
        {
            if (user.IsServer || user.ToPlayer().IsAdmin)
            {
                if (!IsPasteAvailable)
                {
                    user.Reply(mx("PasteOnCooldown", user.Id));
                    return;
                }

                SetOnSun(false);

                user.Reply(mx("ReloadConfig", user.Id));
                LoadConfig();
                Maintained.Enabled = config.Settings.Maintained.Enabled;
                Scheduled.Enabled = config.Settings.Schedule.Enabled;
                buyableEnabled = config.Settings.Buyable.Max > 0;

                if (Maintained.Coroutine != null)
                {
                    Maintained.StopCoroutine();
                    user.Reply(mx("ReloadMaintainCo", user.Id));
                }

                if (Scheduled.Coroutine != null)
                {
                    Scheduled.StopCoroutine();
                    user.Reply(mx("ReloadScheduleCo", user.Id));
                }

                user.Reply(mx("ReloadInit", user.Id));
                Initialize();
            }
        }

        private void Initialize()
        {
            Instance = this;
            GridController.Initialize();
            GridController.LoadSpawns();
            GridController.Setup();
            InitializeSkins();
            SpawnsController.BlockZoneManagerZones(ZoneManager);
            Reinitialize();
            UpdateUI();
            CreateDefaultFiles();
            LoadTables();
            LoadProfiles();
            SetOnSun(true);
        }

        private void CommandBuyRaid(IPlayer user, string command, string[] args)
        {
            if (config.Settings.Buyable.UsePermission && !user.HasPermission("raidablebases.buyraid"))
            {
                user.Reply(m("No Permission", user.Id));
                return;
            }

            var player = user.ToPlayer();

            if (user.IsServer && args.Length >= 1 && args[0].IsSteamId())
            {
                player = BasePlayer.FindByID(ulong.Parse(args[0]));
                args = Array.Empty<string>();
            }
            else if (args.Length > 1 && args[1].IsSteamId())
            {
                player = BasePlayer.FindByID(ulong.Parse(args[1]));
            }

            if (!player.IsReallyValid())
            {
                user.Reply(args.Length > 1 ? m("TargetNotFoundId", user.Id, args[1]) : m("TargetNotFoundNoId", user.Id));
                return;
            }

            var buyer = user.ToPlayer() ?? player;
            var members = GetMembers(buyer);

            foreach (string memberId in members)
            {
                float time;
                if (tryBuyCooldowns.TryGetValue(memberId, out time) && time > Time.time)
                {
                    Message(buyer, "BuyableAlreadyRequestedTime", $"{time - Time.time:N02}");
                    return;
                }
            }

            members.ForEach(memberId =>
            {
                tryBuyCooldowns[memberId] = Time.time + 5f;
                timer.Once(5f, () => tryBuyCooldowns.Remove(memberId));
            });

            if (args.Length == 0)
            {
                if (config.UI.Buyable.Enabled)
                {
                    UI.CreateBuyableUI(player);
                }
                else
                {
                    Message(buyer, "BuySyntax", config.Settings.BuyCommand, user.IsServer ? "ID" : user.Id);
                }
                return;
            }

            if (!buyableEnabled && !buyer.HasPermission("raidablebases.canbypass"))
            {
                Message(buyer, "BuyRaidsDisabled");
                return;
            }

            if (!IsCopyPasteLoaded(buyer))
            {
                return;
            }

            if (IsGridLoading && !buyer.IsAdmin)
            {
                Message(buyer, "GridIsLoading");
                return;
            }

            if (RaidableBase.Get(RaidableType.Purchased) >= config.Settings.Buyable.Max)
            {
                Message(buyer, "Max Manual Events", config.Settings.Buyable.Max);
                return;
            }

            string value = args[0].ToLower();
            RaidableMode mode = GetRaidableMode(value);

            if (!CanSpawnDifficultyToday(mode))
            {
                Message(buyer, "BuyDifficultyNotAvailableToday", value);
                return;
            }

            if (mode == RaidableMode.Random || !IsDifficultyAvailable(mode, false))
            {
                Message(buyer, "BuyAnotherDifficulty", value);
                return;
            }

            if (!IsDifficultyAvailable(mode, true))
            {
                Message(buyer, "BuyPVPRaidsDisabled");
                return;
            }

            if (!IsPasteAvailable)
            {
                Message(buyer, "PasteOnCooldown");
                return;
            }

            if (!bypassRestarting && ServerMgr.Instance.Restarting)
            {
                Message(buyer, buyer.IsAdmin ? "BuyableServerRestartingAdmin" : "BuyableServerRestarting");
                return;
            }

            if (SaveRestore.IsSaving)
            {
                Message(buyer, "BuyableServerSaving");
                return;
            }

            if (RaidableBase.IsOwner(player))
            {
                Message(buyer, "BuyableAlreadyOwner");
                return;
            }

            BuyableInfo bi;
            if (BuyCooldowns.TryGetValue(buyer.userID, out bi))
            {
                Message(buyer, "BuyCooldown", bi.Time - Time.realtimeSinceStartup);
                return;
            }

            CuiHelper.DestroyUi(player, UI.BuyablePanelName);

            bool isValid;
            Payment customPayment = null;
            var payments = new List<Payment>();
            
            if (config.Settings.Costs.IncludeCustom)
            {
                customPayment = TryBuyRaidCustom(buyer, player, mode, out isValid);

                if (customPayment == null && isValid)
                {
                    return;
                }
                
                if (customPayment != null)
                {
                    payments.Add(customPayment);
                }
            }

            if (config.Settings.Costs.IncludeEconomics)
            {
                var economicsPayment = TryBuyRaidEconomics(buyer, player, mode, out isValid);

                if (economicsPayment == null && isValid)
                {
                    return;
                }

                if (economicsPayment != null)
                {
                    payments.Add(economicsPayment);
                }
            }

            if (config.Settings.Costs.IncludeServerRewards)
            {
                var serverRewardsPayment = TryBuyRaidServerRewards(buyer, player, mode, out isValid);

                if (serverRewardsPayment == null && isValid)
                {
                    return;
                }

                if (serverRewardsPayment != null)
                {
                    payments.Add(serverRewardsPayment);
                }
            }

            if (payments.Count > 0)
            {
                customPayment?.TakeItems();

                if (!BuyRaid(mode, payments, player))
                {
                    customPayment?.RefundItems();
                }
            }
        }

        private void CommandRaidHunter(IPlayer user, string command, string[] args)
        {
            var player = user.ToPlayer();
            bool isAdmin = user.IsServer || player.IsAdmin;
            string arg = args.Length >= 1 ? args[0].ToLower() : string.Empty;

            switch (arg)
            {
                case "version":
                    {
                        user.Reply($"Version: {Version}");
                        return;
                    }
                case "invite":
                    {
                        CommandInvite(player, args);
                        return;
                    }
                case "resettime":
                    {
                        if (isAdmin)
                        {
                            data.RaidTime = DateTime.MinValue.ToString();
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
                case "ignore_restart":
                    {
                        if (isAdmin)
                        {
                            bypassRestarting = !bypassRestarting;
                            user.Reply($"Bypassing restart check: {bypassRestarting}");
                        }

                        return;
                    }
                case "savefix":
                    {
                        if (user.IsAdmin || user.HasPermission("raidablebases.allow"))
                        {
                            int removed = BaseEntity.saveList.RemoveWhere(e => e.IsKilled());

                            if (removed > 0)
                            {
                                user.Reply($"Removed {removed} invalid entities from the save list.");
                            }

                            if (SaveRestore.IsSaving)
                            {
                                SaveRestore.IsSaving = false;
                                user.Reply("Server save has been canceled. You must type server.save again, and then restart your server.");
                            }
                            else user.Reply("Server save is operating normally.");
                        }

                        return;
                    }
                case "tp":
                    {
                        if (player.IsReallyValid() && (isAdmin || user.HasPermission("raidablebases.allow")))
                        {
                            RaidableBase raid = null;
                            float num = 9999f;

                            foreach (var other in Raids)
                            {
                                float num2 = player.Distance(other.Location);

                                if (num2 > other.ProtectionRadius * 2f && num2 < num)
                                {
                                    num = num2;
                                    raid = other;
                                }
                            }

                            if (raid != null)
                            {
                                player.Teleport(raid.Location);
                            }
                        }

                        return;
                    }
                case "grid":
                    {
                        if (player.IsReallyValid() && (isAdmin || user.HasPermission("raidablebases.ddraw")))
                        {
                            ShowGrid(player);
                        }

                        return;
                    }
                case "monuments":
                    {
                        if (player.IsReallyValid() && (isAdmin || user.HasPermission("raidablebases.ddraw")))
                        {
                            ShowMonuments(player);
                        }

                        return;
                    }
                case "ui":
                    {
                        CommandUI(user, command, args.Skip(1));
                        return;
                    }
                case "ladder":
                case "lifetime":
                    {
                        ShowLadder(user, args);
                        return;
                    }
                case "prod":
                    {
                        Prodigy(player, args);
                        return;
                    }
            }

            if (config.UI.Enabled)
            {
                user.Reply(m(config.UI.Lockout.Enabled ? "UIHelpTextAll" : "UIHelpText", user.Id, command));
            }

            if (config.RankedLadder.Enabled)
            {
                PlayerInfo playerInfo = PlayerInfo.Get(user.Id);

                user.Reply(m("RankedWins", user.Id, playerInfo.Raids, playerInfo.Points, config.Settings.HunterCommand));
                user.Reply(m("RankedWins2", user.Id, config.Settings.HunterCommand));
            }

            if (Raids.Count == 0 && Scheduled.Enabled)
            {
                ShowNextScheduledEvent(user);
                return;
            }

            if (player.IsReallyValid())
            {
                DrawRaidLocations(player, isAdmin || player.HasPermission("raidablebases.ddraw"));
            }
        }

        private void CommandInvite(BasePlayer player, string[] args)
        {
            if (player == null) return;
            if (args.Length < 1) { Message(player, "Invite Usage", config.Settings.HunterCommand); return; }
            var target = RustCore.FindPlayer(args[0]);
            if (target == null) { Message(player, "TargetNotFoundId", args[0]); return; }
            var raid = Raids.FirstOrDefault(x => x.ownerId == player.userID);
            if (raid == null) { Message(player, "Invite Ownership Error"); return; }
            if (raid.HasLockout(target)) { Message(player, "Invite Lockout Error"); return; }
            Raider raider;
            if (!raid.raiders.TryGetValue(target.userID, out raider)) raid.raiders[target.userID] = raider = new Raider(target);
            raider.IsAlly = true;
            Message(player, "Invite Success", target.displayName);
            Message(target, "Invite Allowed", player.displayName);
        }

        protected void DrawRaidLocations(BasePlayer player, bool hasPerm)
        {
            foreach (var raid in Raids)
            {
                if (InRange2D(raid.Location, player.transform.position, 100f))
                {
                    Player.Message(player, string.Format("{0} @ {1} ({2})", raid.BaseName, raid.Location, PositionToGrid(raid.Location)));
                }
            }

            if (!hasPerm)
            {
                return;
            }

            bool isAdmin = player.IsAdmin;

            try
            {
                ToggleAdminFlag(player, isAdmin, true);

                foreach (var raid in Raids)
                {
                    int num = BasePlayer.activePlayerList.Sum(x => x.IsReallyValid() && x.Distance(raid.Location) <= raid.ProtectionRadius * 3f ? 1 : 0);
                    int distance = Mathf.CeilToInt(Vector3.Distance(player.transform.position, raid.Location));
                    string message = mx("RaidMessage", player.UserIDString, distance, num);
                    string flag = mx(raid.AllowPVP ? "PVPFlag" : "PVEFlag", player.UserIDString);

                    player.SendConsoleCommand("ddraw.text", 15f, Color.yellow, raid.Location, string.Format("{0} : {1}{2} {3}", raid.BaseName, flag, raid.Mode(player.UserIDString, true), message));

                    foreach (var ri in raid.raiders.Values.Where(x => x.IsAlly && x.player.IsReallyValid()))
                    {
                        player.SendConsoleCommand("ddraw.text", 15f, Color.yellow, ri.player.transform.position, mx("Ally", player.UserIDString).Replace(":", string.Empty));
                    }

                    if (raid.owner.IsReallyValid())
                    {
                        player.SendConsoleCommand("ddraw.text", 15f, Color.yellow, raid.owner.transform.position, mx("Owner", player.UserIDString).Replace(":", string.Empty));
                    }
                }
            }
            catch (Exception ex)
            {
                Puts("{0}", ex);
            }
            finally
            {
                ToggleAdminFlag(player, isAdmin, false);
            }
        }

        protected void ShowNextScheduledEvent(IPlayer user)
        {
            string message;
            double time = GridController.GetRaidTime();

            if (BasePlayer.activePlayerList.Count < config.Settings.Schedule.PlayerLimit)
            {
                message = mx("Not Enough Online", user.Id, config.Settings.Schedule.PlayerLimit);
            }
            else message = FormatTime(time, user.Id);

            user.Reply(m("Next", user.Id, message));
        }

        protected void Prodigy(BasePlayer player, string[] args)
        {
            if (!player.IsReallyValid() || !(player.IsAdmin || player.HasPermission("raidablebases.allow")))
            {
                Message(player, "No Permission");
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 5f, -1, QueryTriggerInteraction.Ignore))
            {
                Message(player, "LookElsewhere");
                return;
            }

            var entity = hit.GetEntity();

            if (entity.IsNull())
            {
                Message(player, "LookElsewhere");
                return;
            }

            _sb.Clear();
            _sb.AppendLine(string.Format("Owner ID: {0}", entity.OwnerID));
            _sb.AppendLine(string.Format("RaidEntities: {0}", RaidEntities.ContainsKey(entity)));
            _sb.AppendLine(string.Format("EventTerritory: {0}", EventTerritory(entity.transform.position)));

            var raid = RaidableBase.Get(entity.transform.position);

            if (raid == null)
            {
                _sb.AppendLine("Entity is not part of a raidable base.");
            }
            else
            {
                if (raid.BuiltList.ContainsKey(entity))
                {
                    _sb.AppendLine("Entity was built by a player after the base spawned.");
                }
                else _sb.AppendLine("Entity was built by the profile.");

                _sb.AppendLine(string.Format("Distance From Raid: {0}m", (raid.Location - entity.transform.position).magnitude));
            }

            QueueNotification(player, _sb.ToString());
            _sb.Clear();
        }

        protected void ShowLadder(IPlayer user, string[] args)
        {
            if (!config.RankedLadder.Enabled || config.RankedLadder.Top < 1)
            {
                return;
            }

            if (args.Length == 2 && args[1].ToLower() == "resetme")
            {
                data.Players[user.Id] = new PlayerInfo();
                user.Reply("Your ranked stats have been reset.");
                return;
            }

            string key = args[0].ToLower();
            var mode = args.Length == 2 ? GetRaidableMode(args[1]) : RaidableMode.Points;

            if (data.Players.Count == 0)
            {
                user.Reply(m("Ladder Insufficient Players", user.Id));
                return;
            }

            int rank = 0;
            bool isByWipe = key == "ladder";
            var ladder = GetLadder(key, mode);

            ladder.Sort((x, y) => y.Value.CompareTo(x.Value));

            user.Reply(m(isByWipe ? "RankedLadder" : "RankedTotal", user.Id, config.RankedLadder.Top, mode));

            foreach (var kvp in ladder.Take(config.RankedLadder.Top))
            {
                NotifyPlayer(user, ++rank, kvp.Key, isByWipe, mode);
            }

            ladder.Clear();
        }

        private static RaidableMode GetRaidableMode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return RaidableMode.Random;
            }

            if (IsEasy(value))
            {
                return RaidableMode.Easy;
            }
            else if (IsMedium(value))
            {
                return RaidableMode.Medium;
            }
            else if (IsHard(value))
            {
                return RaidableMode.Hard;
            }
            else if (IsExpert(value))
            {
                return RaidableMode.Expert;
            }
            else if (IsNightmare(value))
            {
                return RaidableMode.Nightmare;
            }

            return RaidableMode.Random;
        }

        protected void ShowGrid(BasePlayer player)
        {
            bool isAdmin = player.IsAdmin;

            try
            {
                RaidableSpawns spawns;
                if (!GridController.Spawns.TryGetValue(RaidableType.Grid, out spawns))
                {
                    return;
                }

                ToggleAdminFlag(player, isAdmin, true);

                foreach (var rsl in spawns.Active)
                {
                    if (InRange2D(rsl.Location, player.transform.position, 1000f))
                    {
                        player.SendConsoleCommand("ddraw.text", 30f, Color.green, rsl.Location, "X");
                    }
                }

                foreach (CacheType cacheType in Enum.GetValues(typeof(CacheType)))
                {
                    var color = cacheType == CacheType.Generic ? Color.red : cacheType == CacheType.Temporary ? Color.cyan : cacheType == CacheType.Privilege ? Color.yellow : Color.blue;
                    var text = cacheType == CacheType.Generic ? "X" : cacheType == CacheType.Temporary ? "C" : cacheType == CacheType.Privilege ? "TC" : "W";

                    foreach (var rsl in spawns.Inactive(cacheType))
                    {
                        if (InRange2D(rsl.Location, player.transform.position, 1000f))
                        {
                            player.SendConsoleCommand("ddraw.text", 30f, color, rsl.Location, text);
                        }
                    }
                }

                foreach (var monument in SpawnsController.Monuments)
                {
                    player.SendConsoleCommand("ddraw.sphere", 30f, Color.blue, monument.transform.position, monument.bounds.size.Max());
                    player.SendConsoleCommand("ddraw.text", 30f, Color.cyan, monument.transform.position, monument.translated);
                }
            }
            catch (Exception ex)
            {
                Puts("{0}", ex);
            }
            finally
            {
                ToggleAdminFlag(player, isAdmin, false);
            }
        }

        protected void ShowMonuments(BasePlayer player)
        {
            bool isAdmin = player.IsAdmin;

            try
            {
                ToggleAdminFlag(player, isAdmin, true);

                foreach (var monument in SpawnsController.Monuments)
                {
                    player.SendConsoleCommand("ddraw.sphere", 30f, Color.blue, monument.transform.position, monument.bounds.size.Max());
                    player.SendConsoleCommand("ddraw.text", 30f, Color.cyan, monument.transform.position, monument.translated);
                }
            }
            catch (Exception ex)
            {
                Puts("{0}", ex);
            }
            finally
            {
                ToggleAdminFlag(player, isAdmin, false);
            }
        }


        protected List<KeyValuePair<string, int>> GetLadder(string arg, RaidableMode mode)
        {
            var ladder = new List<KeyValuePair<string, int>>();
            bool isLadder = arg.ToLower() == "ladder";

            foreach (var entry in data.Players)
            {
                int value = 0;

                switch (mode)
                {
                    case RaidableMode.Points:
                        value = isLadder ? entry.Value.Points : entry.Value.TotalPoints;
                        break;
                    case RaidableMode.Easy:
                        value = isLadder ? entry.Value.Easy : entry.Value.TotalEasy;
                        break;
                    case RaidableMode.Medium:
                        value = isLadder ? entry.Value.Medium : entry.Value.TotalMedium;
                        break;
                    case RaidableMode.Hard:
                        value = isLadder ? entry.Value.Hard : entry.Value.TotalHard;
                        break;
                    case RaidableMode.Expert:
                        value = isLadder ? entry.Value.Expert : entry.Value.TotalExpert;
                        break;
                    case RaidableMode.Nightmare:
                        value = isLadder ? entry.Value.Nightmare : entry.Value.TotalNightmare;
                        break;
                }

                if (value > 0)
                {
                    ladder.Add(new KeyValuePair<string, int>(entry.Key, value));
                }
            }

            return ladder;
        }

        private void NotifyPlayer(IPlayer user, int rank, string key, bool isByWipe, RaidableMode mode)
        {
            int value;
            int points;
            PlayerInfo playerInfo = PlayerInfo.Get(key);
            playerInfo.ResetExpiredDate();
            string name = covalence.Players.FindPlayerById(key)?.Name ?? key;
            string message = lang.GetMessage("NotifyPlayerFormat", this, user.Id);

            switch (mode)
            {
                case RaidableMode.Easy:
                    value = isByWipe ? playerInfo.Easy : playerInfo.TotalEasy;
                    points = isByWipe ? playerInfo.EasyPoints : playerInfo.TotalEasyPoints;
                    break;
                case RaidableMode.Medium:
                    value = isByWipe ? playerInfo.Medium : playerInfo.TotalMedium;
                    points = isByWipe ? playerInfo.MediumPoints : playerInfo.TotalMediumPoints;
                    break;
                case RaidableMode.Hard:
                    value = isByWipe ? playerInfo.Hard : playerInfo.TotalHard;
                    points = isByWipe ? playerInfo.HardPoints : playerInfo.TotalHardPoints;
                    break;
                case RaidableMode.Expert:
                    value = isByWipe ? playerInfo.Expert : playerInfo.TotalExpert;
                    points = isByWipe ? playerInfo.ExpertPoints : playerInfo.TotalExpertPoints;
                    break;
                case RaidableMode.Nightmare:
                    value = isByWipe ? playerInfo.Nightmare : playerInfo.TotalNightmare;
                    points = isByWipe ? playerInfo.NightmarePoints : playerInfo.TotalNightmarePoints;
                    break;
                case RaidableMode.Points:
                default:
                    value = isByWipe ? playerInfo.Raids : playerInfo.TotalRaids;
                    points = isByWipe ? playerInfo.Points : playerInfo.TotalPoints;
                    break;
            }

            message = message.Replace("{rank}", rank.ToString());
            message = message.Replace("{name}", name);
            message = message.Replace("{value}", value.ToString());
            message = message.Replace("{points}", points.ToString());

            user.Reply(message);
        }

        private void CommandRaidBase(IPlayer user, string command, string[] args)
        {
            var player = user.ToPlayer();
            bool isAllowed = user.IsServer || player.IsAdmin || user.HasPermission("raidablebases.allow");

            if (!CanCommandContinue(player, user, isAllowed, args))
            {
                return;
            }

            if (command == config.Settings.EventCommand) // rbe
            {
                ProcessEventCommand(user, player, isAllowed, args);
            }
            else if (command == config.Settings.ConsoleCommand) // rbevent
            {
                ProcessConsoleCommand(user, player, isAllowed, args);
            }
        }

        protected void ProcessEventCommand(IPlayer user, BasePlayer player, bool isAllowed, string[] args) // rbe
        {
            if (!isAllowed || !player.IsReallyValid())
            {
                return;
            }

            string message = null;
            string baseName = args.FirstOrDefault(value => FileExists(value));
            RaidableMode mode = GetRaidableMode(args.FirstOrDefault(value => GetRaidableMode(value) != RaidableMode.Random));
            var profile = GetBuilding(RaidableType.Manual, mode, baseName);

            if (IsProfileValid(profile))
            {
                RaycastHit hit;
                int layers = Layers.Solid | Layers.Mask.Default | Layers.Mask.Water | ~Layers.Mask.Invisible;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, isAllowed ? Mathf.Infinity : 100f, layers, QueryTriggerInteraction.Ignore))
                {
                    CacheType cacheType;
                    int layers2 = Layers.Mask.Player_Server | Layers.Mask.Construction | Layers.Mask.Deployed;
                    var radius = Mathf.Max(M_RADIUS * 2f, profile.Value.Options.ArenaWalls.Radius);
                    var sac = new SpawnsController.SafeAreaCheck(hit.point, radius, radius, layers2, RaidableType.Manual, profile.Value.Options.CustomSpawns);
                    var safe = player.IsAdmin || SpawnsController.IsAreaSafe(sac, out cacheType, out message);

                    if (!safe && !player.IsFlying && InRange(player.transform.position, hit.point, 50f))
                    {
                        user.Reply(m("PasteIsBlockedStandAway", user.Id));
                        return;
                    }

                    if (safe && (isAllowed || !SpawnsController.IsMonumentPosition(hit.point)))
                    {
                        var spawns = GridController.Spawns.FirstOrDefault(s => s.Value.Spawns.Exists(t => InRange2D(t.Location, hit.point, M_RADIUS))).Value;
                        var x = new RandomBase(profile.Key, profile.Value, hit.point, RaidableType.Manual, spawns, null);
                        if (PasteBuilding(x, out message) && player.IsAdmin)
                        {
                            player.SendConsoleCommand("ddraw.text", 10f, Color.red, hit.point, "XXX");
                        }
                    }
                    else user.Reply(m("PasteIsBlocked", user.Id));

                    if (!string.IsNullOrEmpty(message))
                    {
                        user.Reply(message);
                    }
                }
                else user.Reply(m("LookElsewhere", user.Id));
            }
            else
            {
                if (profile.Value == null)
                {
                    user.Reply(m("BuildingNotConfigured", user.Id));
                }
                else user.Reply(GetDebugMessage(mode, false, true, user.Id, profile.Key, profile.Value.Options, message));
            }
        }

        protected void ProcessConsoleCommand(IPlayer user, BasePlayer player, bool isAllowed, string[] args) // rbevent
        {
            if (IsGridLoading && !user.IsAdmin)
            {
                int count = GridController.Spawns.ContainsKey(RaidableType.Grid) ? GridController.Spawns[RaidableType.Grid].Count : 0;
                user.Reply(m("GridIsLoadingFormatted", user.Id, (Time.realtimeSinceStartup - GridController.gridTime).ToString("N02"), count));
                return;
            }

            float scanDistance = -1f;
            Vector3 scanVector = Vector3.zero;

            if (player.IsReallyValid())
            {
                foreach (string arg in args)
                {
                    float num;
                    if (float.TryParse(arg, out num) && num >= 5f)
                    {
                        scanVector = player.transform.position;
                        scanDistance = num;
                        break;
                    }
                }
            }

            string message;
            string baseName = args.FirstOrDefault(value => FileExists(value));
            RaidableMode mode = GetRaidableMode(args.FirstOrDefault(value => GetRaidableMode(value) != RaidableMode.Random));
            RandomBase x = SpawnRandomBase(out message, RaidableType.Manual, mode, baseName, isAllowed, null, null, scanDistance, scanVector);

            if (x == null)
            {
                user.Reply(message);
            }
            else if (isAllowed && user.IsConnected)
            {
                user.Teleport(x.Position.x, x.Position.y, x.Position.z);
            }
        }

        private bool CanCommandContinue(BasePlayer player, IPlayer user, bool isAllowed, string[] args)
        {
            if (HandledCommandArguments(player, user, isAllowed, args))
            {
                return false;
            }

            if (!IsCopyPasteLoaded(player))
            {
                return false;
            }

            if (!isAllowed && RaidableBase.Get(RaidableType.Manual) >= config.Settings.Manual.Max)
            {
                user.Reply(m("Max Manual Events", user.Id, config.Settings.Manual.Max));
                return false;
            }

            if (!IsPasteAvailable)
            {
                user.Reply(m("PasteOnCooldown", user.Id));
                return false;
            }

            if (IsSpawnOnCooldown() && !user.IsAdmin)
            {
                user.Reply(m("SpawnOnCooldown", user.Id));
                return false;
            }

            if (!isAllowed && BaseNetworkable.serverEntities.Count > 300000)
            {
                user.Reply(m("EntityCountMax", user.Id));
                return false;
            }

            return true;
        }

        private bool HandledCommandArguments(BasePlayer player, IPlayer user, bool isAllowed, string[] args)
        {
            if (args.Length == 0)
            {
                return false;
            }

            if (HandledPlayerArguments(player, isAllowed, args))
            {
                return true;
            }

            switch (args[0].ToLower())
            {
                case "flat":
                    {
                        if (!isAllowed) return false;
                        if (SpawnsController.IsObstructed(player.transform.position, 50f, 2.5f, -1f, player))
                        {
                            user.Reply("Test failed");
                        }
                        else user.Reply("Test passed");
                        return true;
                    }
                case "debug":
                    {
                        if (!isAllowed) return false;
                        DebugMode = !DebugMode;
                        user.Reply(string.Format("Debug mode: {0}", DebugMode));
                        user.Reply(string.Format("Scheduled Events Running: {0}", Scheduled.Coroutine != null));
                        user.Reply(string.Format("Maintained Events Running: {0}", Maintained.Coroutine != null));
                        if (_messages.Count > 0)
                        {
                            user.Reply($"DEBUG: Last {_messages.Count} messages:");
                            _messages.ForEach(message => user.Reply($"PREVIOUS DEBUG: {message}"));
                        }
                        return true;
                    }
                case "lockout":
                    {
                        if (!isAllowed) return false;
                        RaidableBase.SetTestLockout(player);
                        return true;
                    }
                case "kill_cleanup":
                    {
                        if (!isAllowed) return false;
                        BaseNetworkable.serverEntities.OfType<BaseEntity>().ToList().ForEach(entity =>
                        {
                            if (entity.IsDestroyed || entity.OwnerID != 0 || entity.Distance(player) > 100f) return;
                            if (entity.name.Contains("building") || entity.name.Contains("deploy")) entity.SafelyKill();
                        });

                        return true;
                    }
                case "despawnall":
                case "despawn_inactive":
                    {
                        if (!isAllowed) return false;
                        if (Raids.Count > 0)
                        {
                            DespawnAllBasesNow(args[0].ToLower() == "despawn_inactive");
                            Puts(mx("DespawnedAll", null, user.Name));
                        }

                        return true;
                    }
                case "active":
                    {
                        if (!isAllowed) return false;
                        int count = 0;

                        foreach (var raid in Raids)
                        {
                            if (raid.intruders.Count > 0 || raid.ownerId.IsSteamId())
                            {
                                user.Reply($"Active raid at {raid.Location} in {FormatGridReference(raid.Location)}");
                                count++;
                            }
                        }

                        user.Reply($"{count} active raids");
                        return true;
                    }
                case "expire":
                case "resetcooldown":
                    {
                        if (!isAllowed) return false;
                        if (args.Length >= 2)
                        {
                            var target = RustCore.FindPlayer(args[1]);

                            if (!target.IsNull())
                            {
                                BuyableInfo bi;
                                if (BuyCooldowns.TryGetValue(target.userID, out bi))
                                {
                                    bi.Timer.Destroy();
                                    BuyCooldowns.Remove(target.userID);
                                }

                                data.Lockouts.Remove(target.UserIDString);

                                user.Reply(m("RemovedLockFor", user.Id, target.displayName, target.UserIDString));
                            }
                        }

                        return true;
                    }
                case "expireall":
                case "resetall":
                case "resetallcooldowns":
                    {
                        if (!isAllowed) return false;
                        BuyCooldowns.Clear();
                        data.Lockouts.Clear();
                        Puts($"All cooldowns and lockouts have been reset by {user.Name} ({user.Id})");
                        return true;
                    }
                case "setowner":
                case "lockraid":
                    {
                        if (args.Length >= 2 && (isAllowed || user.HasPermission("raidablebases.setowner")))
                        {
                            var target = RustCore.FindPlayer(args[1]);

                            if (target.IsReallyValid())
                            {
                                var raid = GetNearestBase(target.transform.position);

                                if (raid == null)
                                {
                                    user.Reply(m("TargetTooFar", user.Id));
                                }
                                else
                                {
                                    var payments = new List<Payment> { new Payment(0, 0, target, target, target.transform.position) };
                                    raid.TrySetPayLock(payments, true);
                                    user.Reply(m("RaidLockedTo", user.Id, target.displayName));
                                }
                            }
                            else user.Reply(m("TargetNotFoundId", user.Id, args[1]));
                        }

                        return true;
                    }
                case "clearowner":
                    {
                        if (player.IsReallyValid() && (isAllowed || user.HasPermission("raidablebases.setowner")))
                        {
                            var raid = GetNearestBase(player.transform.position);

                            if (raid == null)
                            {
                                user.Reply(m("TooFar", user.Id));
                            }
                            else
                            {
                                raid.TrySetPayLock(null);
                                user.Reply(m("RaidOwnerCleared", user.Id));
                            }
                        }

                        return true;
                    }
            }

            return false;
        }

        private bool HandledPlayerArguments(BasePlayer player, bool isAllowed, string[] args)
        {
            if (player.IsReallyValid())
            {
                switch (args[0].ToLower())
                {
                    case "despawn":
                        if (isAllowed || player.HasPermission("raidablebases.despawn.buyraid"))
                        {
                            bool success = DespawnBase(player, isAllowed);
                            Message(player, success ? "DespawnBaseSuccess" : isAllowed ? "DespawnBaseNoneAvailable" : "DespawnBaseNoneOwned");
                            if (success) Puts(mx("DespawnedAt", null, player.displayName, FormatGridReference(player.transform.position)));
                        }

                        return true;
                    case "draw":
                        DrawSpheres(player, isAllowed);
                        return true;
                }
            }

            return false;
        }

        private void DrawSpheres(BasePlayer player, bool isAllowed)
        {
            if (!isAllowed && !player.HasPermission("raidablebases.ddraw"))
            {
                return;
            }

            bool isAdmin = player.IsAdmin;

            try
            {
                ToggleAdminFlag(player, isAdmin, true);

                foreach (var raid in Raids)
                {
                    player.SendConsoleCommand("ddraw.sphere", 30f, Color.blue, raid.Location, raid.ProtectionRadius);
                }
            }
            catch (Exception ex)
            {
                Puts("{0}", ex);
            }
            finally
            {
                ToggleAdminFlag(player, isAdmin, false);
            }
        }

        private void CommandToggle(IPlayer user, string command, string[] args)
        {
            if (config.Settings.Maintained.Enabled)
            {
                Maintained.Enabled = !Maintained.Enabled;
                user.Reply($"Toggled maintained events {(Maintained.Enabled ? "on" : "off")}");
            }

            if (config.Settings.Schedule.Enabled)
            {
                Scheduled.Enabled = !Scheduled.Enabled;
                user.Reply($"Toggled scheduled events {(Scheduled.Enabled ? "on" : "off")}");
            }

            if (config.Settings.Buyable.Max > 0)
            {
                buyableEnabled = !buyableEnabled;
                user.Reply($"Toggled buyable events {(buyableEnabled ? "on" : "off")}");
            }
        }

        private void CommandPopulate(IPlayer user, string command, string[] args)
        {
            if (args.Length == 0)
            {
                user.Reply("Valid arguments: 0 1 2 3 4 default all");
                return;
            }

            var list = new List<LootItem>();

            ItemManager.GetItemDefinitions().ForEach(def => list.Add(new LootItem(def.shortname)));

            list.Sort((x, y) => x.shortname.CompareTo(y.shortname));

            foreach (var str in args)
            {
                string arg = str.ToLower();

                if (IsEasy(arg) || arg == "all")
                {
                    AddToList(LootType.Easy, list);
                    user.Reply("Created Editable_Lists/Easy.json");
                }

                if (IsMedium(arg) || arg == "all")
                {
                    AddToList(LootType.Medium, list);
                    user.Reply("Created Editable_Lists/Medium.json`");
                }

                if (IsHard(arg) || arg == "all")
                {
                    AddToList(LootType.Hard, list);
                    user.Reply("Created Editable_Lists/Hard.json`");
                }

                if (IsExpert(arg) || arg == "all")
                {
                    AddToList(LootType.Expert, list);
                    user.Reply("Created Editable_Lists/Expert.json");
                }

                if (IsNightmare(arg) || arg == "all")
                {
                    AddToList(LootType.Nightmare, list);
                    user.Reply("Created Editable_Lists/Nightmare.json");
                }

                if (arg == "loot" || arg == "default" || arg == "all")
                {
                    AddToList(LootType.Default, list);
                    user.Reply("Created Editable_Lists/Default.json");
                }
            }

            SaveConfig();
        }

        private void CommandConfig(IPlayer user, string command, string[] args)
        {
            if (args.Length == 0 || !arguments.Contains(args[0]))
            {
                user.Reply(mx("ConfigUseFormat", user.Id, string.Join("|", arguments.ToArray())));
                return;
            }

            string arg = args[0].ToLower();

            switch (arg)
            {
                case "add": ConfigAddBase(user, args); return;
                case "remove": case "clean": ConfigRemoveBase(user, args); return;
                case "list": ConfigListBases(user); return;
            }

            if (args.Length == 3 && IsModeValid(GetRaidableMode(arg)))
            {
                ConfigSetEnabledWeekday(user, GetRaidableMode(arg), args[1].ToLower(), args[2].ToLower());
            }
        }

        #endregion Commands

        #region Garbage

        internal Dictionary<BaseEntity, MountInfo> MountEntities { get; set; } = new Dictionary<BaseEntity, MountInfo>();
        internal Dictionary<BaseEntity, RaidableBase> RaidEntities { get; set; } = new Dictionary<BaseEntity, RaidableBase>();

        private bool KeepEntity(BaseEntity entity, UndoSettings undo)
        {
            if (!undo.Mounts && KeepMountable(entity) || entity.OwnerID.IsSteamId() && KeepPlayerEntity(entity, undo))
            {
                return true;
            }

            RaidEntities.Remove(entity);

            return false;
        }

        private bool KeepPlayerEntity(BaseEntity entity, UndoSettings undo)
        {
            if (entity.PrefabName.Contains("assets/prefabs/deployable/"))
            {
                if (!undo.Deployables)
                {
                    if (entity is IItemContainerEntity)
                    {
                        var ice = entity as IItemContainerEntity;

                        if (ice != null)
                        {
                            DropUtil.DropItems(ice.inventory, entity.transform.position + Vector3.up);
                        }
                    }

                    return false;
                }
            }
            else if (!undo.Structures)
            {
                return false;
            }

            RaidEntities.Remove(entity);

            return true;
        }

        private bool KeepMountable(BaseEntity entity)
        {
            MountInfo mi;
            if (!MountEntities.TryGetValue(entity, out mi) || !MountEntities.Remove(entity))
            {
                return false;
            }

            return !mi.mountable.GetMounted().IsNull() || !InRange2D(entity.transform.position, mi.position, mi.radius);
        }

        public void RemoveHeldEntities()
        {
            foreach (var element in RaidEntities.ToList())
            {
                if (element.Key is IItemContainerEntity)
                {
                    var ice = element.Key as IItemContainerEntity;

                    if (ice.IsNull() || ice.inventory.IsNull())
                    {
                        continue;
                    }

                    for (int i = 0; i < ice.inventory.itemList.Count; i++)
                    {
                        var e = ice.inventory.itemList[i].GetHeldEntity();

                        if (e.IsReallyValid())
                        {
                            e.enableSaving = false;
                            BaseEntity.saveList.Remove(e);
                        }
                    }

                    ice.inventory.Clear();
                }
            }

            ItemManager.DoRemoves();
        }

        public void DespawnAll(List<RaidableBase> raids, bool inactiveOnly)
        {
            var entities = new List<BaseEntity>();
            bool teleportEntities = false;
            int limit = 1;

            for (int i = 0; i < raids.Count; i++)
            {
                var raid = raids[i];

                if (raid.IsNull() || inactiveOnly && (raid.intruders.Count > 0 || raid.ownerId.IsSteamId()))
                {
                    continue;
                }

                entities.AddRange(raid.Entities);

                if (raid.Options.Setup.TeleportEntities)
                {
                    teleportEntities = true;
                }

                limit = Math.Max(limit, raid.Options.Setup.DespawnLimit);

                raid.Despawn(false);
            }

            UndoLoop(new UndoSettings(
                entities,
                limit,
                config.Settings.Management.DespawnMounts,
                config.Settings.Management.DoNotDestroyStructures,
                config.Settings.Management.DoNotDestroyDeployables,
                teleportEntities), 0, 2, Time.realtimeSinceStartup + 5.33333f);
        }

        public static int Evaluate(BaseEntity entity)
        {
            if (entity is BuildingBlock)
            {
                return 2;
            }
            if (!entity.HasParent())
            {
                return 1;
            }
            return 0;
        }

        public void UndoLoop(UndoSettings undo, int count, int index, float spawnTime)
        {
            undo.Entities.RemoveAll(e => e.IsKilled() || KeepEntity(e, undo));

            undo.Entities.Sort((x, y) => Evaluate(x).CompareTo(Evaluate(y)));

            undo.Entities.Take(undo.Limit).ToList().ForEach(entity =>
            {
                undo.Entities.Remove(entity);

                if (entity.IsKilled())
                {
                    return;
                }

                if (entity is IOEntity)
                {
                    var io = entity as IOEntity;

                    io.ClearConnections();

                    if (entity is SamSite)
                    {
                        var ss = entity as SamSite;

                        ss.staticRespawn = false;
                    }
                }

                if (data != null && entity.net != null)
                {
                    data.TemporaryUID.Remove(entity.net.ID);
                }

                if (undo.Teleport)
                {
                    entity.transform.position = new Vector3(0f, -500f, 0f);
                    Interface.Oxide.NextTick(entity.SafelyKill);
                }
                else entity.Kill(BaseNetworkable.DestroyMode.None);
            });

            if (count != 0 && undo.Entities.Count != 0 && undo.Entities.Count == count)
            {
                goto done;
            }

            if (undo.Entities.Count > 0)
            {
                var action = new Action(() => UndoLoop(undo, undo.Entities.Count, index, spawnTime));
                Interface.Oxide.NextTick(action);
                return;
            }

        done:
            RaidEntities.RemoveAll((e, raid) => e.IsKilled());

            if (RaidEntities.Count == 0)
            {
                MountEntities.Clear();
            }

            if (undo.hookObjects != null)
            {
                Interface.CallHook("OnRaidableBaseDespawned", undo.hookObjects);
            }
        }

        #endregion Garbage

        #region Helpers

        private List<string> _lockoutKeys = new List<string>();

        private void CheckLockouts()
        {
            if (data.Lockouts.Count == 0) return;
            foreach (var element in data.Lockouts)
            {
                var lo = element.Value;
                if (--lo.Easy <= 0) lo.Easy = 0;
                if (--lo.Medium <= 0) lo.Medium = 0;
                if (--lo.Hard <= 0) lo.Hard = 0;
                if (--lo.Expert <= 0) lo.Expert = 0;
                if (--lo.Nightmare <= 0) lo.Nightmare = 0;
                if (!lo.Any()) _lockoutKeys.Add(element.Key);
            }
            if (_lockoutKeys.Count == 0) return;
            _lockoutKeys.ForEach(key => data.Lockouts.Remove(key));
            _lockoutKeys.Clear();
        }

        private static List<T> FindEntitiesOfType<T>(Vector3 a, float n, int m = -1) where T : BaseEntity
        {
            int hits = Physics.OverlapSphereNonAlloc(a, n, Vis.colBuffer, m, QueryTriggerInteraction.Collide);
            List<T> entities = new List<T>();
            for (int i = 0; i < hits; i++)
            {
                var entity = Vis.colBuffer[i]?.ToBaseEntity();
                if (entity is T) entities.Add(entity as T);
                Vis.colBuffer[i] = null;
            }

            return entities;
        }

        private void CheckOceanLevel()
        {
            if (OceanLevel != WaterSystem.OceanLevel)
            {
                OceanLevel = WaterSystem.OceanLevel;

                RaidableSpawns spawns;
                if (GridController.Spawns.TryGetValue(RaidableType.Grid, out spawns))
                {
                    spawns.TryAddRange(CacheType.Submerged);
                }
            }
        }

        private void SetOnSun(bool state)
        {
            if (!config.Settings.Management.Lights)
            {
                return;
            }

            if (state)
            {
                TOD_Sky.Instance.Components.Time.OnSunrise += OnSunrise;
                TOD_Sky.Instance.Components.Time.OnSunset += OnSunset;
            }
            else
            {
                TOD_Sky.Instance.Components.Time.OnSunrise -= OnSunrise;
                TOD_Sky.Instance.Components.Time.OnSunset -= OnSunset;
            }
        }

        public static bool IsUnderground(Vector3 a) => GamePhysics.CheckSphere<TerrainCollisionTrigger>(a, 5f, 262144, QueryTriggerInteraction.Collide);

        public void InitializeSkins()
        {
            foreach (var def in ItemManager.GetItemDefinitions())
            {
                ItemModDeployable imd;
                if (def.TryGetComponent(out imd))
                {
                    _definitions[imd.entityPrefab.resourcePath] = def;
                }
            }
        }

        private static void ToggleAdminFlag(BasePlayer player, bool isAdmin, bool state)
        {
            if (!isAdmin)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, state);
                player.SendNetworkUpdateImmediate();
            }
        }

        private static void SafelyKill(BaseEntity entity) => entity.SafelyKill();

        private List<string> GetMembers(BasePlayer buyer)
        {
            var members = new HashSet<string>();

            if (buyer.currentTeam == 0uL)
            {
                members.Add(buyer.UserIDString);
            }
            else buyer.Team.members.ForEach(member => members.Add(member.ToString()));

            var clan = Clans?.Call("GetClanMembers", buyer.userID) as List<string>;

            if (clan != null)
            {
                clan.ForEach(member => members.Add(member));
            }

            return members.ToList();
        }

        private bool IsHelicopter(HitInfo hitInfo)
        {
            if (hitInfo.Initiator is BaseHelicopter || hitInfo.Initiator?.ShortPrefabName == "oilfireballsmall" || hitInfo.Initiator?.ShortPrefabName == "napalm")
            {
                return true;
            }
            else if (hitInfo.WeaponPrefab?.ShortPrefabName == "rocket_heli" || hitInfo.WeaponPrefab?.ShortPrefabName == "rocket_heli_napalm")
            {
                return true;
            }
            return false;
        }

        private bool IsCopyPasteLoaded(BasePlayer player)
        {
            if (!CopyPaste.CanCall())
            {
                if (player.IsReallyValid())
                {
                    QueueNotification(player, "InstallPluginCopyPaste");
                }
                else Puts(mx("InstallPluginCopyPaste"));

                return false;
            }

            if (CopyPaste.Version < new VersionNumber(4, 1, 31))
            {
                if (player.IsReallyValid())
                {
                    QueueNotification(player, "LoadSupportedCopyPasteVersion");
                }
                else Puts(mx("LoadSupportedCopyPasteVersion"));

                return false;
            }

            return true;
        }

        private bool HasPVPDelay(ulong playerId)
        {
            return PvpDelay.ContainsKey(playerId);
        }

        private static bool IsBox(BaseEntity entity, bool inherit)
        {
            if (entity.ShortPrefabName == "box.wooden.large" || entity.ShortPrefabName == "woodbox_deployed" || entity.ShortPrefabName == "coffinstorage")
            {
                return true;
            }

            return inherit && config.Settings.Management.Inherit.Exists(entity.ShortPrefabName.Contains);
        }

        public static float GetDistance(RaidableType type)
        {
            switch (type)
            {
                case RaidableType.Maintained: return config.Settings.Maintained.Distance;
                case RaidableType.Purchased: return config.Settings.Buyable.Distance;
                case RaidableType.Scheduled: return config.Settings.Schedule.Distance;
                case RaidableType.None: return Mathf.Max(config.Settings.Maintained.Distance, config.Settings.Buyable.Distance, config.Settings.Schedule.Distance);
                default: return 100f;
            }
        }

        private void AddToList(LootType lootType, List<LootItem> source)
        {
            List<LootItem> lootList;
            if (!Buildings.DifficultyLootLists.TryGetValue(lootType, out lootList))
            {
                Buildings.DifficultyLootLists[lootType] = lootList = new List<LootItem>();
            }

            foreach (var ti in source)
            {
                if (!lootList.Exists(x => x.shortname == ti.shortname))
                {
                    lootList.Add(ti);
                }
            }

            string file = $"{Name}{Path.DirectorySeparatorChar}Editable_Lists{Path.DirectorySeparatorChar}{lootType}";
            Interface.Oxide.DataFileSystem.WriteObject(file, lootList);
        }

        private bool IsPVE() => TruePVE != null || NextGenPVE != null || Imperium != null;

        private static bool IsEasy(string value) => value == "0" || value.Equals("easy", StringComparison.OrdinalIgnoreCase) || value.Equals("easy bases", StringComparison.OrdinalIgnoreCase);

        private static bool IsMedium(string value) => value == "1" || value.Equals("med", StringComparison.OrdinalIgnoreCase) || value.Equals("medium", StringComparison.OrdinalIgnoreCase) || value.Equals("medium bases", StringComparison.OrdinalIgnoreCase);

        private static bool IsHard(string value) => value == "2" || value.Equals("hard", StringComparison.OrdinalIgnoreCase) || value.Equals("hard bases", StringComparison.OrdinalIgnoreCase);

        private static bool IsExpert(string value) => value == "3" || value.Equals("expert", StringComparison.OrdinalIgnoreCase) || value.Equals("expert bases", StringComparison.OrdinalIgnoreCase);

        private static bool IsNightmare(string value) => value == "4" || value.Equals("nightmare", StringComparison.OrdinalIgnoreCase) || value.Equals("nightmare bases", StringComparison.OrdinalIgnoreCase);

        [HookMethod("IsPremium")]
        public bool IsPremium() => true;

        private void UpdateUI()
        {
            if (config.UI.Enabled)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player.IsReallyValid())
                    {
                        UI.UpdateLockoutUI(player);
                    }
                }
            }
        }

        private static void NullifyDamage(HitInfo hitInfo)
        {
            if (hitInfo != null)
            {
                hitInfo.damageTypes = new DamageTypeList();
                hitInfo.DidHit = false;
                hitInfo.DoHitEffects = false;
                hitInfo.HitEntity = null;
            }
        }

        public static bool MustExclude(RaidableType type, bool allowPVP)
        {
            if (!config.Settings.Maintained.IncludePVE && type == RaidableType.Maintained && !allowPVP)
            {
                return true;
            }

            if (!config.Settings.Maintained.IncludePVP && type == RaidableType.Maintained && allowPVP)
            {
                return true;
            }

            if (!config.Settings.Schedule.IncludePVE && type == RaidableType.Scheduled && !allowPVP)
            {
                return true;
            }

            if (!config.Settings.Schedule.IncludePVP && type == RaidableType.Scheduled && allowPVP)
            {
                return true;
            }

            return false;
        }

        private bool AnyNpcs()
        {
            return Raids.Exists(x => x.npcs.Exists(npc => !npc.IsNull()));
        }

        private void DestroyComponents()
        {
            foreach (var raid in Raids)
            {
                raid.DestroyInputs();
            }
        }

        private void ConfigAddBase(IPlayer user, string[] args)
        {
            if (args.Length < 2)
            {
                user.Reply(mx("ConfigAddBaseSyntax", user.Id));
                return;
            }

            _sb.Clear();
            var values = new List<string>(args);
            values.RemoveAt(0);
            string profileName = values[0];
            RaidableMode mode = RaidableMode.Random;

            foreach (string value in values)
            {
                var m = GetRaidableMode(value);

                if (m != RaidableMode.Random)
                {
                    values.Remove(value);
                    mode = m;
                    break;
                }
            }

            user.Reply(mx("Adding", user.Id, string.Join(" ", values.ToArray())));

            BaseProfile profile;
            if (!Buildings.Profiles.TryGetValue(profileName, out profile))
            {
                Buildings.Profiles[profileName] = profile = new BaseProfile();
                _sb.AppendLine(mx("AddedPrimaryBase", user.Id, profileName));
            }

            foreach (string value in values)
            {
                if (!profile.Options.AdditionalBases.ContainsKey(value))
                {
                    profile.Options.AdditionalBases.Add(value, DefaultPasteOptions);
                    _sb.AppendLine(mx("AddedAdditionalBase", user.Id, value));
                }
            }

            if (IsModeValid(mode))
            {
                _sb.AppendLine(mx("DifficultySetTo", user.Id, profile.Options.Mode = mode));
            }

            if (_sb.Length > 0)
            {
                user.Reply(_sb.ToString());
                profile.Options.Enabled = true;
                SaveProfile(profileName, profile.Options);
                Buildings.Profiles[profileName] = profile;

                if (mode == RaidableMode.Disabled)
                {
                    user.Reply(mx("DifficultyNotSet", user.Id));
                }

                _sb.Clear();
            }
            else user.Reply(mx("EntryAlreadyExists", user.Id));

            values.Clear();
        }

        private void ConfigRemoveBase(IPlayer user, string[] args)
        {
            if (args.Length < 2)
            {
                user.Reply(mx("RemoveSyntax", user.Id));
                return;
            }

            int num = 0;
            var profiles = new Dictionary<string, BaseProfile>(Buildings.Profiles);
            var files = (string.Join(" ", args[0].ToLower() == "remove" ? args.Skip(1) : args)).Replace(", ", " ");
            var split = files.Split(' ');

            _sb.Clear();
            _sb.AppendLine(mx("RemovingAllBasesFor", user.Id, string.Join(" ", files)));

            foreach (var profile in profiles)
            {
                foreach (var element in profile.Value.Options.AdditionalBases.ToList())
                {
                    if (split.Contains(element.Key))
                    {
                        _sb.AppendLine(mx("RemovedAdditionalBase", user.Id, element.Key, profile.Key));
                        if (profile.Value.Options.AdditionalBases.Remove(element.Key)) num++;
                        SaveProfile(profile.Key, profile.Value.Options);
                    }
                }

                if (split.Contains(profile.Key))
                {
                    _sb.AppendLine(mx("RemovedPrimaryBase", user.Id, profile.Key));
                    if (Buildings.Profiles.Remove(profile.Key)) num++;
                    profile.Value.Options.Enabled = false;
                    SaveProfile(profile.Key, profile.Value.Options);
                }
            }

            _sb.AppendLine(mx("RemovedEntries", user.Id, num));
            user.Reply(_sb.ToString());
            _sb.Clear();
        }

        private void ConfigSetEnabledWeekday(IPlayer user, RaidableMode mode, string day, string flag)
        {
            DayOfWeek dayOfWeek;
            if (!Enum.TryParse(day.SentenceCase(), out dayOfWeek))
            {
                user.Reply($"Invalid weekday: {day}");
                return;
            }

            bool value;
            if (!bool.TryParse(flag, out value))
            {
                user.Reply($"Invalid flag (true/false): {flag}");
                return;
            }

            user.Reply($"{mode} is now {(value ? "enabled" : "disabled")} on {dayOfWeek}");

            if (mode == RaidableMode.Easy)
            {
                switch (dayOfWeek)
                {
                    case DayOfWeek.Monday: config.Settings.Management.Easy.Monday = value; break;
                    case DayOfWeek.Tuesday: config.Settings.Management.Easy.Tuesday = value; break;
                    case DayOfWeek.Wednesday: config.Settings.Management.Easy.Wednesday = value; break;
                    case DayOfWeek.Thursday: config.Settings.Management.Easy.Thursday = value; break;
                    case DayOfWeek.Friday: config.Settings.Management.Easy.Friday = value; break;
                    case DayOfWeek.Saturday: config.Settings.Management.Easy.Saturday = value; break;
                    case DayOfWeek.Sunday: config.Settings.Management.Easy.Sunday = value; break;
                }
            }
            else if (mode == RaidableMode.Medium)
            {
                switch (dayOfWeek)
                {
                    case DayOfWeek.Monday: config.Settings.Management.Medium.Monday = value; break;
                    case DayOfWeek.Tuesday: config.Settings.Management.Medium.Tuesday = value; break;
                    case DayOfWeek.Wednesday: config.Settings.Management.Medium.Wednesday = value; break;
                    case DayOfWeek.Thursday: config.Settings.Management.Medium.Thursday = value; break;
                    case DayOfWeek.Friday: config.Settings.Management.Medium.Friday = value; break;
                    case DayOfWeek.Saturday: config.Settings.Management.Medium.Saturday = value; break;
                    case DayOfWeek.Sunday: config.Settings.Management.Medium.Sunday = value; break;
                }
            }
            else if (mode == RaidableMode.Hard)
            {
                switch (dayOfWeek)
                {
                    case DayOfWeek.Monday: config.Settings.Management.Hard.Monday = value; break;
                    case DayOfWeek.Tuesday: config.Settings.Management.Hard.Tuesday = value; break;
                    case DayOfWeek.Wednesday: config.Settings.Management.Hard.Wednesday = value; break;
                    case DayOfWeek.Thursday: config.Settings.Management.Hard.Thursday = value; break;
                    case DayOfWeek.Friday: config.Settings.Management.Hard.Friday = value; break;
                    case DayOfWeek.Saturday: config.Settings.Management.Hard.Saturday = value; break;
                    case DayOfWeek.Sunday: config.Settings.Management.Hard.Sunday = value; break;
                }
            }
            else if (mode == RaidableMode.Expert)
            {
                switch (dayOfWeek)
                {
                    case DayOfWeek.Monday: config.Settings.Management.Expert.Monday = value; break;
                    case DayOfWeek.Tuesday: config.Settings.Management.Expert.Tuesday = value; break;
                    case DayOfWeek.Wednesday: config.Settings.Management.Expert.Wednesday = value; break;
                    case DayOfWeek.Thursday: config.Settings.Management.Expert.Thursday = value; break;
                    case DayOfWeek.Friday: config.Settings.Management.Expert.Friday = value; break;
                    case DayOfWeek.Saturday: config.Settings.Management.Expert.Saturday = value; break;
                    case DayOfWeek.Sunday: config.Settings.Management.Expert.Sunday = value; break;
                }
            }
            else if (mode == RaidableMode.Nightmare)
            {
                switch (dayOfWeek)
                {
                    case DayOfWeek.Monday: config.Settings.Management.Nightmare.Monday = value; break;
                    case DayOfWeek.Tuesday: config.Settings.Management.Nightmare.Tuesday = value; break;
                    case DayOfWeek.Wednesday: config.Settings.Management.Nightmare.Wednesday = value; break;
                    case DayOfWeek.Thursday: config.Settings.Management.Nightmare.Thursday = value; break;
                    case DayOfWeek.Friday: config.Settings.Management.Nightmare.Friday = value; break;
                    case DayOfWeek.Saturday: config.Settings.Management.Nightmare.Saturday = value; break;
                    case DayOfWeek.Sunday: config.Settings.Management.Nightmare.Sunday = value; break;
                }
            }

            _saveConfigTimer = timer.Once(1f, SaveConfig);
        }

        private Timer _saveConfigTimer;

        private void ConfigListBases(IPlayer user)
        {
            _sb.Clear();
            _sb.Append(mx("ListingAll", user.Id));
            _sb.AppendLine();

            bool buyable = false;
            bool validBase = false;

            foreach (var entry in Buildings.Profiles)
            {
                if (!entry.Value.Options.AllowPVP)
                {
                    buyable = true;
                }

                _sb.AppendLine(mx("PrimaryBase", user.Id));

                if (FileExists(entry.Key))
                {
                    _sb.AppendLine(entry.Key);
                    validBase = true;
                }
                else _sb.Append(entry.Key).Append(mx("IsProfile", user.Id));

                if (entry.Value.Options.AdditionalBases.Count > 0)
                {
                    _sb.AppendLine(mx("AdditionalBase", user.Id));

                    foreach (var ab in entry.Value.Options.AdditionalBases)
                    {
                        if (FileExists(ab.Key))
                        {
                            _sb.AppendLine(ab.Key);
                            validBase = true;
                        }
                        else _sb.Append(ab.Key).Append((mx("FileDoesNotExist", user.Id)));
                    }
                }
            }

            if (!buyable && !config.Settings.Buyable.BuyPVP)
            {
                _sb.AppendLine(mx("NoBuyableEventsPVP", user.Id));
            }

            if (!validBase)
            {
                _sb.AppendLine(mx("NoBuildingsConfigured", user.Id));
            }

            user.Reply(_sb.ToString());
            _sb.Clear();
        }

        private static void DropOrRemoveItems(StorageContainer container, bool isProtectedWeapon)
        {
            if (!config.Settings.Management.DropLoot.FlameTurret && container.OwnerID == 0 && container is FlameTurret)
            {
                container.inventory.Clear();
            }
            else if (!config.Settings.Management.DropLoot.FogMachine && container.OwnerID == 0 && container is FogMachine)
            {
                container.inventory.Clear();
            }
            else if (!config.Settings.Management.DropLoot.GunTrap && container.OwnerID == 0 && container is GunTrap)
            {
                container.inventory.Clear();
            }
            else if (!config.Settings.Management.AllowCupboardLoot && container.OwnerID == 0 && container is BuildingPrivlidge)
            {
                container.inventory.Clear();
            }
            else if (container.inventory.itemList.Count > 0)
            {
                float y = container.transform.position.y + Mathf.Max(0.81158f, container.bounds.size.y);

                container.inventory.Drop(StringPool.Get(545786656), container.transform.position.WithY(y), container.transform.rotation);
            }

            container.Invoke(container.SafelyKill, 0.1f);
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

        protected bool DespawnBase(BasePlayer player, bool isAllowed)
        {
            var raid = isAllowed ? GetNearestBase(player.transform.position) : GetPurchasedBase(player);

            if (raid == null || raid.IsLoading)
            {
                return false;
            }

            if (raid.IsPayLocked)
            {
                raid.Refund(player);
            }

            raid.Despawn();

            return true;
        }

        private RaidableBase GetPurchasedBase(BasePlayer player)
        {
            return Raids.FirstOrDefault(raid => raid.IsPayLocked && raid.ownerId == player.userID);
        }

        private RaidableBase GetNearestBase(Vector3 target, float radius = 100f)
        {
            var values = new List<RaidableBase>();

            foreach (var x in Raids)
            {
                if (InRange2D(x.Location, target, radius))
                {
                    values.Add(x);
                }
            }

            if (values.Count == 0)
            {
                return null;
            }

            if (values.Count > 1)
            {
                values.Sort((a, b) => (a.Location - target).sqrMagnitude.CompareTo((b.Location - target).sqrMagnitude));
            }

            return values[0];
        }

        private void DespawnAllBasesNow(bool inactiveOnly)
        {
            RaidableBase.IsSpawning = false;

            if (!IsUnloading)
            {
                DespawnAll(Raids.ToList(), inactiveOnly);
                return;
            }

            if (Interface.Oxide.IsShuttingDown)
            {
                RemoveHeldEntities();
                return;
            }

            DestroyAll();
        }

        private void DestroyAll()
        {
            foreach (var raid in Raids.ToList())
            {
                Puts(lang.GetMessage("Destroyed Raid", this), $"{PositionToGrid(raid.Location)} {raid.Location}");
                if (raid.IsOpened) raid.AwardRaiders();
                raid.Despawn();
            }
        }

        private bool IsTrueDamage(BaseEntity entity, bool isProtectedWeapon)
        {
            if (entity.IsNull())
            {
                return false;
            }

            return isProtectedWeapon || entity.skinID == 1587601905 || TrueDamage.Contains(entity.ShortPrefabName) || entity is TeslaCoil || entity is BaseTrap;
        }

        private Vector3 GetCenterLocation(Vector3 position)
        {
            return Raids.FirstOrDefault(raid => InRange2D(raid.Location, position, raid.ProtectionRadius))?.Location ?? Vector3.zero;
        }

        private bool HasEventEntity(BaseEntity entity)
        {
            if (entity.IsKilled()) return false;
            if (RaidableBase.Has(entity)) return true;
            if (entity is BasePlayer) return RaidableBase.Has(((BasePlayer)entity).userID);
            return false;
        }

        private bool EventTerritory(Vector3 position)
        {
            for (int i = 0; i < Raids.Count; i++)
            {
                if (InRange2D(Raids[i].Location, position, Raids[i].ProtectionRadius))
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
                float radius = Mathf.Max(raid.ProtectionRadius, raid.Options.ArenaWalls.Radius, M_RADIUS);

                return !InRange(attacker.transform.position, raid.Location, radius);
            }

            return false;
        }

        private static bool InRange2D(Vector3 a, Vector3 b, float distance)
        {
            return (new Vector3(a.x, 0f, a.z) - new Vector3(b.x, 0f, b.z)).sqrMagnitude <= distance * distance;
        }

        private static bool InRange(Vector3 a, Vector3 b, float distance)
        {
            return (a - b).sqrMagnitude <= distance * distance;
        }

        private bool AssignTreasureHunters()
        {
            foreach (var target in covalence.Players.All)
            {
                foreach (var record in RankedRecords)
                {
                    if (target.Id.HasPermission(record.Permission))
                    {
                        permission.RevokeUserPermission(target.Id, record.Permission);
                    }

                    if (permission.UserHasGroup(target.Id, record.Group))
                    {
                        permission.RemoveUserGroup(target.Id, record.Group);
                    }
                }
            }

            if (!config.RankedLadder.Enabled || config.RankedLadder.Amount <= 0)
            {
                return true;
            }

            var players = data.Players.ToList().Where(x => x.Key.IsSteamId() && IsNormalUser(x.Key));

            RankedRecords.ForEach(record => AssignTreasureHunters(record, players));

            Puts(mx("Log Saved", null, "treasurehunters"));

            return true;
        }

        private bool IsNormalUser(string userid)
        {
            if (userid.HasPermission("raidablebases.notitle"))
            {
                return false;
            }

            var player = covalence.Players.FindPlayerById(userid);

            return !(player == null || player.IsBanned);
        }

        private void AssignTreasureHunters(RankedRecord record, List<KeyValuePair<string, PlayerInfo>> players)
        {
            var ladder = new List<KeyValuePair<string, int>>();

            foreach (var entry in players)
            {
                switch (record.Mode)
                {
                    case RaidableMode.Points:
                        if (entry.Value.Points > 0) ladder.Add(new KeyValuePair<string, int>(entry.Key, entry.Value.Points));
                        break;
                    case RaidableMode.Easy:
                        if (entry.Value.Easy > 0) ladder.Add(new KeyValuePair<string, int>(entry.Key, entry.Value.Easy));
                        break;
                    case RaidableMode.Medium:
                        if (entry.Value.Medium > 0) ladder.Add(new KeyValuePair<string, int>(entry.Key, entry.Value.Medium));
                        break;
                    case RaidableMode.Hard:
                        if (entry.Value.Hard > 0) ladder.Add(new KeyValuePair<string, int>(entry.Key, entry.Value.Hard));
                        break;
                    case RaidableMode.Expert:
                        if (entry.Value.Expert > 0) ladder.Add(new KeyValuePair<string, int>(entry.Key, entry.Value.Expert));
                        break;
                    case RaidableMode.Nightmare:
                        if (entry.Value.Nightmare > 0) ladder.Add(new KeyValuePair<string, int>(entry.Key, entry.Value.Nightmare));
                        break;
                }
            }

            if (ladder.Count == 0)
            {
                return;
            }

            ladder.Sort((x, y) => y.Value.CompareTo(x.Value));

            foreach (var kvp in ladder.Take(config.RankedLadder.Amount))
            {
                var p = covalence.Players.FindPlayerById(kvp.Key);

                if (p == null || p.HasPermission("raidablebases.ladder.exclude"))
                {
                    continue;
                }

                permission.GrantUserPermission(p.Id, record.Permission, this);
                permission.AddUserGroup(p.Id, record.Group);

                string message = mx("Log Stolen", null, p.Name, p.Id, kvp.Value);

                LogToFile("treasurehunters", $"{DateTime.Now} : {message}", this, true);
                Puts(mx("Log Granted", null, p.Name, p.Id, record.Permission, record.Group));
            }
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

        private static bool IsModeValid(RaidableMode mode) => mode != RaidableMode.Disabled && mode != RaidableMode.Random && mode != RaidableMode.Points;

        public static string FormatGridReference(Vector3 position)
        {
            if (config.Settings.ShowXZ)
            {
                return string.Format("{0} ({1} {2})", PositionToGrid(position), position.x.ToString("N2"), position.z.ToString("N2"));
            }

            return PositionToGrid(position);
        }

        private static string PositionToGrid(Vector3 position) => PhoneController.PositionToGridCoord(position);

        private static string FormatTime(double seconds, string id = null)
        {
            if (seconds < 0)
            {
                return "0s";
            }

            var ts = TimeSpan.FromSeconds(seconds);
            string format = mx("TimeFormat", id);

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

            foreach (var building in DefaultBuildingOptions)
            {
                string filename = $"{Name}{Path.DirectorySeparatorChar}Profiles{Path.DirectorySeparatorChar}{building.Key}";

                if (!Interface.Oxide.DataFileSystem.ExistsDatafile(filename))
                {
                    SaveProfile(building.Key, building.Value);
                }
            }

            string lootFile = $"{Name}{Path.DirectorySeparatorChar}Default_Loot";

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(lootFile))
            {
                Interface.Oxide.DataFileSystem.WriteObject(lootFile, DefaultLoot);
            }
        }

        protected void VerifyProfiles()
        {
            bool allowPVP = Buildings.Profiles.Values.Exists(profile => profile.Options.AllowPVP);
            bool allowPVE = Buildings.Profiles.Values.Exists(profile => !profile.Options.AllowPVP);

            if (config.Settings.Maintained.Enabled)
            {
                if (allowPVP && !config.Settings.Maintained.IncludePVP && !allowPVE)
                {
                    Puts("Invalid configuration detected: Maintained Events -> Include PVP Bases is set false, and all profiles have Allow PVP enabled. Therefore no bases can spawn for Maintained Events. The ideal configuration is for Include PVP Bases to be set true, and Convert PVP To PVE to be set true.");
                }

                if (allowPVE && !config.Settings.Maintained.IncludePVE && !allowPVP)
                {
                    Puts("Invalid configuration detected: Maintained Events -> Include PVE Bases is set false, and all profiles have Allow PVP disabled. Therefore no bases can spawn for Maintained Events. The ideal configuration is for Include PVE Bases to be set true, and Convert PVE To PVP to be set true.");
                }
            }

            if (config.Settings.Schedule.Enabled)
            {
                if (allowPVP && !config.Settings.Schedule.IncludePVP && !allowPVE)
                {
                    Puts("Invalid configuration detected: Scheduled Events -> Include PVP Bases is set false, and all profiles have Allow PVP enabled. Therefore no bases can spawn for Scheduled Events. The ideal configuration is for Include PVP Bases to be set true, and Convert PVP To PVE to be set true.");
                }

                if (allowPVE && !config.Settings.Schedule.IncludePVE && !allowPVP)
                {
                    Puts("Invalid configuration detected: Scheduled Events -> Include PVE Bases is set false, and all profiles have Allow PVP disabled. Therefore no bases can spawn for Scheduled Events. The ideal configuration is for Include PVE Bases to be set true, and Convert PVE To PVP to be set true.");
                }
            }
        }

        protected void LoadProfiles()
        {
            string folder = $"{Name}{Path.DirectorySeparatorChar}Profiles";
            string[] files;

            try
            {
                files = Interface.Oxide.DataFileSystem.GetFiles(folder);
            }
            catch (UnauthorizedAccessException ex)
            {
                Puts("{0}", ex);
                return;
            }

            foreach (string file in files)
            {
                string profileName = file;

                try
                {
                    if (file.EndsWith("_emptyfile.json") || file.EndsWith("_empty_file.json"))
                    {
                        continue;
                    }

                    int index = file.LastIndexOf(Path.DirectorySeparatorChar) + 1;

                    profileName = file.Substring(index, file.Length - index - 5);

                    string fullName = $"{folder}{Path.DirectorySeparatorChar}{profileName}";

                    var options = Interface.Oxide.DataFileSystem.ReadObject<BuildingOptions>(fullName);

                    if (options == null || !options.Enabled)
                    {
                        continue;
                    }

                    if (options.AdditionalBases == null)
                    {
                        options.AdditionalBases = new Dictionary<string, List<PasteOption>>();
                    }

                    Buildings.Profiles[profileName] = new BaseProfile(options, profileName);
                }
                catch (Exception ex)
                {
                    Puts("{0}\n{1}", file, ex);
                }
            }

            foreach (var profile in Buildings.Profiles)
            {
                SaveProfile(profile.Key, profile.Value.Options);
            }

            LoadBaseTables();
            VerifyProfiles();
            LoadImportedSkins();

            foreach (var profile in Buildings.Profiles.ToList())
            {
                if (GridController.SpawnsFileValid(profile.Value.Options.CustomSpawns.SpawnsFile))
                {
                    var spawns = GridController.GetSpawnsLocations(profile.Value.Options.CustomSpawns.SpawnsFile);

                    if (spawns?.Count > 0)
                    {
                        Puts(mx("LoadedDifficulty", null, spawns.Count, profile.Value.Options.Mode));
                        Buildings.Profiles[profile.Key].Spawns = new RaidableSpawns(spawns);
                    }
                }
            }
        }

        private void LoadImportedSkins()
        {
            string skinsFilename = $"{Name}{Path.DirectorySeparatorChar}ImportedWorkshopSkins";

            try
            {
                ImportedWorkshopSkins = Interface.Oxide.DataFileSystem.ReadObject<SkinSettingsImportedWorkshop>(skinsFilename);
            }
            catch (JsonException ex)
            {
                UnityEngine.Debug.LogException(ex);
                ImportedWorkshopSkins = new SkinSettingsImportedWorkshop();
            }
            finally
            {
                if (ImportedWorkshopSkins == null)
                {
                    Interface.Oxide.DataFileSystem.WriteObject(skinsFilename, ImportedWorkshopSkins = new SkinSettingsImportedWorkshop());
                }
            }
        }

        protected void SaveProfile(string key, BuildingOptions options)
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}{Path.DirectorySeparatorChar}Profiles{Path.DirectorySeparatorChar}{key}", options);
        }

        protected void LoadTables()
        {
            Buildings = new BuildingTables();
            _sb.Clear();
            _sb.AppendLine("-");

            foreach (LootType lootType in Enum.GetValues(typeof(LootType)))
            {
                string file = lootType == LootType.Default ? $"{Name}{Path.DirectorySeparatorChar}Default_Loot" : $"{Name}{Path.DirectorySeparatorChar}Difficulty_Loot{Path.DirectorySeparatorChar}{lootType}";
                var lootTable = Buildings.DifficultyLootLists[lootType] = GetTable(file);
                LoadTable(file, lootTable);
            }

            foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
            {
                string file = $"{Name}{Path.DirectorySeparatorChar}Weekday_Loot{Path.DirectorySeparatorChar}{day}";
                var lootTable = Buildings.WeekdayLootLists[day] = GetTable(file);
                LoadTable(file, lootTable);
            }
        }

        private void LoadBaseTables()
        {
            foreach (var entry in Buildings.Profiles)
            {
                string file = $"{Name}{Path.DirectorySeparatorChar}Base_Loot{Path.DirectorySeparatorChar}{entry.Key}";
                var lootTable = entry.Value.BaseLootList = GetTable(file);
                LoadTable(file, lootTable);
            }

            Interface.Oxide.LogInfo("{0}", _sb.ToString());
            _sb.Clear();
        }

        private void LoadTable(string file, List<LootItem> lootList)
        {
            if (lootList.Count == 0)
            {
                return;
            }

            if (lootList.All(ti => ti.probability == 0f))
            {
                lootList.ForEach(ti => ti.probability = 1f);
            }

            if (lootList.All(ti => ti.stacksize == 0))
            {
                lootList.ForEach(ti => ti.stacksize = -1);
            }

            Interface.Oxide.DataFileSystem.WriteObject(file, lootList);

            lootList.RemoveAll(ti => ti == null || ti.amount == 0 && ti.amountMin == 0);

            if (lootList.Count == 0)
            {
                return;
            }

            _sb.AppendLine($"Loaded {lootList.Count} items from {file}");

            lootList.ForEach(ti =>
            {
                if (ti != null && ti.amount < ti.amountMin)
                {
                    int min = ti.amountMin;

                    ti.amountMin = ti.amount;
                    ti.amount = min;
                }
            });
        }

        private List<LootItem> GetTable(string file)
        {
            var lootList = new List<LootItem>();

            try
            {
                lootList = Interface.Oxide.DataFileSystem.ReadObject<List<LootItem>>(file);
            }
            catch (JsonReaderException ex)
            {
                Puts("{0}", ex);
            }

            if (lootList == null)
            {
                return new List<LootItem>();
            }

            return lootList;
        }

        #endregion

        #region Configuration

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You do not have permission to use this command.",
                ["Building is blocked!"] = "<color=#FF0000>Building is blocked near raidable bases!</color>",
                ["Ladders are blocked!"] = "<color=#FF0000>Ladders are blocked in raidable bases!</color>",
                ["Barricades are blocked!"] = "<color=#FF0000>Barricades are blocked in raidable bases!</color>",
                ["Cupboards are blocked!"] = "<color=#FF0000>Tool cupboards are blocked in raidable bases!</color>",
                ["Ladders Require Building Privilege!"] = "<color=#FF0000>You need building privilege to place ladders!</color>",
                ["Profile Not Enabled"] = "This profile is not enabled: <color=#FF0000>{0}</color>.",
                ["Difficulty Disabled"] = "Difficulty is disabled for the profile <color=#FF0000>{0}</color>.",
                ["Difficulty Not Available"] = "Difficulty <color=#FF0000>{0}</color> is not available on any of your buildings.",
                ["Difficulty Not Available Admin"] = "Difficulty <color=#FF0000>{0}</color> is not available on any of your buildings. This could indicate that your CopyPaste files are not on this server in the oxide/data/copypaste folder.",
                ["Max Manual Events"] = "Maximum number of manual events <color=#FF0000>{0}</color> has been reached!",
                ["Manual Event Failed"] = "Event failed to start! Unable to obtain a valid position. Please try again.",
                ["Help"] = "/{0} <tp> - start a manual event, and teleport to the position if TP argument is specified and you are an admin.",
                ["RaidOpenMessage"] = "<color=#C0C0C0>A {0} raidable base event has opened at <color=#FFFF00>{1}</color>! You are <color=#FFA500>{2}m</color> away. [{3}]</color>",
                ["Next"] = "<color=#C0C0C0>No events are open. Next event in <color=#FFFF00>{0}</color></color>",
                ["RankedWins"] = "<color=#C0C0C0>You have looted <color=#FFFF00>{0}</color> raid bases for a total of <color=#FFFF00>{1}</color> points! View the ladder using <color=#FFA500>/{2} ladder</color> or <color=#FFA500>/{2} lifetime</color></color>",
                ["RankedWins2"] = "<color=#C0C0C0>View individual difficulty rankings by using <color=#FFA500>/{0} ladder 0|1|2|3|4</color> or <color=#FFA500>/{0} lifetime 0|1|2|3|4</color></color>",
                ["RaidMessage"] = "Raidable Base {0}m [{1} players]",
                ["RankedLadder"] = "<color=#FFFF00>[ Top {0} {1} (This Wipe) ]</color>:",
                ["RankedTotal"] = "<color=#FFFF00>[ Top {0} {1} (Lifetime) ]</color>:",
                ["Ladder Insufficient Players"] = "<color=#FFFF00>No players are on the ladder yet!</color>",
                ["Next Automated Raid"] = "Next automated raid in {0} at {1}",
                ["Not Enough Online"] = "Not enough players online ({0} minimum)",
                ["Raid Base Distance"] = "<color=#C0C0C0>Raidable Base <color=#FFA500>{0}m</color>",
                ["Destroyed Raid"] = "Destroyed a left over raid base at {0}",
                ["Indestructible"] = "<color=#FF0000>Treasure chests are indestructible!</color>",
                ["Log Stolen"] = "{0} ({1}) Raids {2}",
                ["Log Granted"] = "Granted {0} ({1}) permission {2} for group {3}",
                ["Log Saved"] = "Raid Hunters have been logged to: {0}",
                ["Prefix"] = "[ <color=#406B35>Raidable Bases</color> ] ",
                ["RestartDetected"] = "Restart detected. Next event in {0} minutes.",
                ["EconomicsDeposit"] = "You have received <color=#FFFF00>${0}</color> for stealing the treasure!",
                ["EconomicsWithdraw"] = "You have paid <color=#FFFF00>${0}</color> for a raidable base!",
                ["EconomicsWithdrawGift"] = "{0} has paid <color=#FFFF00>${1}</color> for your raidable base!",
                ["EconomicsWithdrawFailed"] = "You do not have <color=#FFFF00>${0}</color> for a raidable base!",
                ["ServerRewardPoints"] = "You have received <color=#FFFF00>{0} RP</color> for stealing the treasure!",
                ["ServerRewardPointsTaken"] = "You have paid <color=#FFFF00>{0} RP</color> for a raidable base!",
                ["ServerRewardPointsGift"] = "{0} has paid <color=#FFFF00>{1} RP</color> for your raidable base!",
                ["ServerRewardPointsFailed"] = "You do not have <color=#FFFF00>{0} RP</color> for a raidable base!",
                ["SkillTreeXP"] = "You have received <color=#FFFF00>{0} XP</color> for stealing the treasure!",
                ["CustomDeposit"] = "You have received <color=#FFFF00>{0}</color> for stealing the treasure!",
                ["CustomWithdraw"] = "You have paid <color=#FFFF00>{0}</color> for a raidable base!",
                ["CustomWithdrawGift"] = "{0} has paid <color=#FFFF00>{1}</color> for your raidable base!",
                ["CustomWithdrawFailed"] = "You do not have <color=#FFFF00>{0}</color> for a raidable base!",
                ["InvalidItem"] = "Invalid item shortname: {0}. Use /{1} additem <shortname> <amount> [skin]",
                ["AddedItem"] = "Added item: {0} amount: {1}, skin: {2}",
                ["CustomPositionSet"] = "Custom event spawn location set to: {0}",
                ["CustomPositionRemoved"] = "Custom event spawn location removed.",
                ["OpenedEvents"] = "Opened {0}/{1} events.",
                ["OnPlayerEntered"] = "<color=#FF0000>You have entered a raidable PVP base!</color>",
                ["OnPlayerEnteredPVE"] = "<color=#FF0000>You have entered a raidable PVE base!</color>",
                ["OnPlayerEntryRejected"] = "<color=#FF0000>You cannot enter an event that does not belong to you!</color>",
                ["OnLockedToRaid"] = "<color=#FF0000>You are now locked to this base.</color>",
                ["OnFirstPlayerEntered"] = "<color=#FFFF00>{0}</color> is the first to enter the raidable base at <color=#FFFF00>{1}</color>",
                ["OnChestOpened"] = "<color=#FFFF00>{0}</color> is the first to see the loot at <color=#FFFF00>{1}</color>!</color>",
                ["OnRaidFinished"] = "The raid at <color=#FFFF00>{0}</color> has been unlocked!",
                ["CannotBeMounted"] = "You cannot loot the treasure while mounted!",
                ["CannotTeleport"] = "You are not allowed to teleport from this event.",
                ["CannotRemove"] = "You are not allowed to remove entities from this event.",
                ["MustBeAuthorized"] = "You must have building privilege to access this treasure!",
                ["OwnerLocked"] = "This treasure belongs to someone else!",
                ["CannotFindPosition"] = "Could not find a random position!",
                ["PasteOnCooldown"] = "Paste is on cooldown!",
                ["SpawnOnCooldown"] = "Try again, a manual spawn was already requested.",
                ["Thieves"] = "<color=#FFFF00>The {0} base at <color=#FFFF00>{1}</color> has been raided by <color=#FFFF00>{2}</color>!</color>",
                ["BuySyntax"] = "<color=#FFFF00>Syntax: {0} easy|medium|hard {1}</color>",
                ["TargetNotFoundId"] = "<color=#FFFF00>Target {0} not found, or not online.</color>",
                ["TargetNotFoundNoId"] = "<color=#FFFF00>No steamid provided.</color>",
                ["BuyAnotherDifficulty"] = "Difficulty '<color=#FFFF00>{0}</color>' is not available, please try another difficulty.",
                ["BuyDifficultyNotAvailableToday"] = "Difficulty '<color=#FFFF00>{0}</color>' is not available today, please try another difficulty.",
                ["BuyPVPRaidsDisabled"] = "<color=#FFFF00>No PVE raids can be bought for this difficulty as buying raids that allow PVP is not allowed.</color>",
                ["BuyRaidsDisabled"] = "<color=#FFFF00>No raids can be bought at this time.</color>",
                ["BuyBaseSpawnedAt"] = "<color=#FFFF00>Your base has been spawned at {0} in {1} !</color>",
                ["BuyBaseAnnouncement"] = "<color=#FFFF00>{0} has paid for a base at {1} in {2}!</color>",
                ["BuyBaseAnnouncementConsole"] = "<color=#FFFF00>{0} has paid for the {1} base ({2}) at {3} in {4}!</color>",
                ["DestroyingBaseAt"] = "<color=#C0C0C0>Destroying raid base at <color=#FFFF00>{0}</color> in <color=#FFFF00>{1}</color> minutes!</color>",
                ["PasteIsBlocked"] = "You cannot start a raid base event there!",
                ["LookElsewhere"] = "Unable to find a position; look elsewhere.",
                ["BuildingNotConfigured"] = "You cannot spawn a base that is not configured.",
                ["NoBuildingsConfigured"] = "No valid buildings have been configured.",
                ["DespawnBaseSuccess"] = "<color=#C0C0C0>Despawning the nearest raid base to you!</color>",
                ["DespawnedAt"] = "{0} despawned a base manually at {1}",
                ["DespawnedAll"] = "{0} despawned all bases manually",
                ["ModeLevel"] = "level",
                ["ModeEasy"] = "easy",
                ["ModeMedium"] = "medium",
                ["ModeHard"] = "hard",
                ["ModeExpert"] = "expert",
                ["ModeNightmare"] = "nightmare",
                ["DespawnBaseNoneAvailable"] = "<color=#C0C0C0>You must be within 100m of a raid base to despawn it.</color>",
                ["DespawnBaseNoneOwned"] = "<color=#C0C0C0>You may only despawn a base you have purchased.</color>",
                ["GridIsLoading"] = "The grid is loading; please wait until it has finished.",
                ["GridIsLoadingFormatted"] = "Grid is loading. The process has taken {0} seconds so far with {1} locations added on the grid.",
                ["TooPowerful"] = "<color=#FF0000>This place is guarded by a powerful spirit. You sheath your wand in fear!</color>",
                ["TooPowerfulDrop"] = "<color=#FF0000>This place is guarded by a powerful spirit. You drop your wand in fear!</color>",
                ["BuyCooldown"] = "<color=#FF0000>You must wait {0} seconds to use this command!</color>",
                ["InstallPluginCopyPaste"] = "CopyPaste is a required plugin and is not installed. Download from https://umod.org/plugins/copy-paste",
                ["LoadSupportedCopyPasteVersion"] = "You must update your version of CopyPaste to 4.1.31 or higher!",
                ["DoomAndGloom"] = "<color=#FF0000>You have left a {0} zone and can be attacked for another {1} seconds!</color>",
                ["NoConfiguredLoot"] = "Error: No loot found in the config!",
                ["NoContainersFound"] = "Error: No usable containers found for {0} @ {1}!",
                ["NoEntitiesFound"] = "Error: No entities found at {0} @ {1}!",
                ["NoBoxesFound"] = "Error: No usable boxes found for {0} @ {1}!",
                ["NoLootSpawned"] = "Error: No loot was spawned!",
                ["LoadedManual"] = "Loaded {0} manual spawns.",
                ["LoadedBuyable"] = "Loaded {0} buyable spawns.",
                ["LoadedMaintained"] = "Loaded {0} maintained spawns.",
                ["LoadedScheduled"] = "Loaded {0} scheduled spawns.",
                ["LoadedDifficulty"] = "Loaded {0} {1} spawns.",
                ["InitializedGrid"] = "Grid initialization completed in {0} seconds and {1} milliseconds on a {2} size map. {3} locations are on the grid.",
                ["InitializedGridSea"] = "{0} locations are on the seabed grid.",
                ["EntityCountMax"] = "Command disabled due to entity count being greater than 300k",
                ["NotifyPlayerFormat"] = "<color=#ADD8E6>{rank}</color>. <color=#C0C0C0>{name}</color> (raided <color=#FFFF00>{value}</color> bases for <color=#FFFF00>{points}</color> points)",
                ["ConfigUseFormat"] = "Use: rb.config <{0}> [base] [subset]",
                ["ConfigAddBaseSyntax"] = "Use: rb.config add nivex1 nivex4 nivex5 nivex6",
                ["FileDoesNotExist"] = " > This file does not exist\n",
                ["IsProfile"] = " > Profile\n",
                ["ListingAll"] = "Listing all primary bases and their subsets:",
                ["PrimaryBase"] = "Primary Base: ",
                ["AdditionalBase"] = "Additional Base: ",
                ["NoValidBuilingsWarning"] = "No valid buildings are configured with a valid file that exists. Did you configure valid files and reload the plugin?",
                ["Adding"] = "Adding: {0}",
                ["AddedPrimaryBase"] = "Added Primary Base: {0}",
                ["AddedAdditionalBase"] = "Added Additional Base: {0}",
                ["DifficultyNotSet"] = "Difficulty has not been configured for this profile! This profile will not be available for use until this has been configured.",
                ["DifficultySetTo"] = "Difficulty set to: {0}",
                ["EntryAlreadyExists"] = "That entry already exists.",
                ["RemoveSyntax"] = "Use: rb.config remove nivex1",
                ["RemovingAllBasesFor"] = "\nRemoving all bases for: {0}",
                ["RemovedPrimaryBase"] = "Removed primary base: {0}",
                ["RemovedAdditionalBase"] = "Removed additional base {0} from primary base {1}",
                ["RemovedEntries"] = "Removed {0} entries",
                ["LockedOut"] = "You are locked out from {0} raids for {1}",
                ["PVPFlag"] = "[<color=#FF0000>PVP</color>] ",
                ["PVEFlag"] = "[<color=#008000>PVE</color>] ",
                ["PVP ZONE"] = "PVP ZONE",
                ["PVE ZONE"] = "PVE ZONE",
                ["OnPlayerExit"] = "<color=#FF0000>You have left a raidable PVP base!</color>",
                ["OnPlayerExitPVE"] = "<color=#FF0000>You have left a raidable PVE base!</color>",
                ["PasteIsBlockedStandAway"] = "You cannot start a raid base event there because you are too close to the spawn. Either move or use noclip.",
                ["ReloadConfig"] = "Reloading config...",
                ["ReloadMaintainCo"] = "Stopped maintain coroutine.",
                ["ReloadScheduleCo"] = "Stopped schedule coroutine.",
                ["ReloadInit"] = "Initializing...",
                ["YourCorpse"] = "Your Corpse",
                ["EjectedYourCorpse"] = "Your corpse has been ejected from your raid.",
                ["NotAllowed"] = "<color=#FF0000>That action is not allowed in this zone.</color>",
                ["BlockedZones"] = "Blocked spawn points in {0} zones.",
                ["UI Format"] = "{0} - Loot Remaining: {1} [Despawn in {2} mins]",
                ["UI FormatContainers"] = "{0} - Loot Remaining: {1}",
                ["UI FormatMinutes"] = "{0} [Despawn in {1} mins]",
                ["UIFormatLockoutMinutes"] = "{0}m",
                ["UIHelpTextAll"] = "<color=#C0C0C0>You can toggle the UI by using <color=#FFA500>/{0} ui [lockouts]</color></color>",
                ["UIHelpText"] = "<color=#C0C0C0>You can toggle the UI by using <color=#FFA500>/{0} ui</color></color>",
                ["HoggingFinishYourRaid"] = "<color=#FF0000>You must finish your last raid at {0} before joining another.</color>",
                ["HoggingFinishYourRaidClan"] = "<color=#FF0000>Your clan mate `{0}` must finish their last raid at {1}.</color>",
                ["HoggingFinishYourRaidTeam"] = "<color=#FF0000>Your team mate `{0}` must finish their last raid at {1}.</color>",
                ["HoggingFinishYourRaidFriend"] = "<color=#FF0000>Your friend `{0}` must finish their last raid at {1}.</color>",
                ["TimeFormat"] = "{0:D2}h {1:D2}m {2:D2}s",
                ["BuyableAlreadyRequestedTime"] = "You must wait {0} seconds to try buying again.",
                ["BuyableServerRestarting"] = "You cannot buy a raid when a server restart is pending.",
                ["BuyableServerRestartingAdmin"] = "You cannot buy a raid when a server restart is pending. You can bypass this typing: /rb ignore_restart",
                ["BuyableServerSaving"] = "You cannot buy a raid while the server is saving.",
                ["BuyableAlreadyOwner"] = "You cannot buy multiple raids.",
                ["TargetTooFar"] = "Your target is not close enough to a raid.",
                ["TooFar"] = "You are not close enough to a raid.",
                ["RaidLockedTo"] = "Raid has been locked to: {0}",
                ["RemovedLockFor"] = "Removed lockout for {0} ({1})",
                ["RaidOwnerCleared"] = "Raid owner has been cleared.",
                ["TooCloseToABuilding"] = "Too close to another building",
                ["Buy Raids"] = "Buy Raids",
                ["CommandNotAllowed"] = "You are not allowed to use this command right now.",
                ["NoBuyableEventsCostsConfigured"] = "No difficulty has a purchase price configured.",
                ["NoBuyableEventsToday"] = "No difficulty is enabled in the configuration today.",
                ["NoBuyableEventsEnabled"] = "All difficulties are disabled in the configuration.",
                ["NoBuyableEventsPVP"] = "Buyable Events is configured to not allow PVP purchases, and no PVE profiles exist. Therefore players cannot purchase anything until a PVE profile is created, or by setting Allow PVP to false in a profile.",
                ["MapMarkerOrderWithMode"] = "{0}{1} {2}{3}",
                ["MapMarkerOrderWithoutMode"] = "{0}{1}{2}",
                ["BannedAdmin"] = "You have the raidablebases.banned permission and as a result are banned from these events.",
                ["Banned"] = "You are banned from these events.",
                ["NoMountedDamageTo"] = "You cannot damage mounts!",
                ["NoMountedDamageFrom"] = "You cannot do damage while mounted to this!",
                ["NoDamageFromOutsideToBaseInside"] = "You must be inside of the event to damage the base!",
                ["NoDamageToEnemyBase"] = "You are not allowed to damage another players event!",
                ["None"] = "None",
                ["You"] = "You",
                ["Enemy"] = "Enemy",
                ["RP"] = "RP",
                ["Ally"] = "Ally",
                ["Owner"] = "Owner:",
                ["Owner:"] = "OWNER: <color={0}>{1}</color>  ",
                ["Active"] = "Active",
                ["Inactive"] = "Inactive",
                ["InactiveTimeLeft"] = " [Inactive in {0} mins]",
                ["Status:"] = "YOUR STATUS: <color={0}>{1}</color>{2}",
                ["Claimed"] = "(Claimed)",
                ["Refunded"] = "You have been refunded: {0}",
                ["TryAgain"] = "Try again at a different location.",
                ["Elevator Health"] = "Elevator Health:",
                ["Elevator Green Card"] = "Elevator access requires a green access card!",
                ["Elevator Blue Card"] = "Elevator access requires a blue access card!",
                ["Elevator Red Card"] = "Elevator access requires a red access card!",
                ["Elevator Special Card"] = "Elevator access requires a special access card!",
                ["Elevator Privileges"] = "Elevator access requires building privileges!",
                ["Invite Usage"] = "/{0} invite <name>",
                ["Invite Ownership Error"] = "You must have ownership of a raid to invite someone.",
                ["Invite Lockout Error"] = "You cannot invite this person until their lockout expires.",
                ["Invite Success"] = "You have invited {0} to join your raid.",
                ["Invite Allowed"] = "You have been allowed to join the raid owned by {0}.",
                ["No Reward: Flying"] = "You cannot earn a reward while flying.",
                ["No Reward: Vanished"] = "You cannot earn a reward while vanished.",
                ["No Reward: Inactive"] = "You cannot earn a reward while inactive.",
                ["No Reward: Admin"] = "Administrators cannot earn rewards.",
                ["No Reward: Not Owner"] = "You must be the owner of the raid to be eligible.",
                ["No Reward: Not Ally"] = "You must be the owner or an ally of the raid to be eligible.",
                ["No Reward: Not A Participant"] = "You must be a participant of the raid to be eligible."
            }, this, "en");
        }

        public static void Message(BasePlayer player, string key, params object[] args)
        {
            if (player.IsReallyValid())
            {
                QueueNotification(player, key, args);
            }
        }

        public static string m(string key, string id = null, params object[] args)
        {
            _sb2.Length = 0;

            if (config.EventMessages.Prefix)
            {
                _sb2.Append(Instance.lang.GetMessage("Prefix", Instance, id));
            }

            string message = Instance.lang.GetMessage(key, Instance, id);

            _sb2.Append(id == null || id == "server_console" ? rf(message) : message);

            return args.Length > 0 ? string.Format(_sb2.ToString(), args) : _sb2.ToString();
        }

        public static string mx(string key, string id = null, params object[] args)
        {
            _sb2.Length = 0;

            string message = Instance.lang.GetMessage(key, Instance, id);

            _sb2.Append(id == null || id == "server_console" ? rf(message) : message);

            return args.Length > 0 ? string.Format(_sb2.ToString(), args) : _sb2.ToString();
        }

        public static string rf(string source) => source.Contains(">") ? Regex.Replace(source, "<.*?>", string.Empty) : source;

        public class Notification
        {
            public BasePlayer player;
            public string messageEx;
        }

        private Dictionary<ulong, List<Notification>> _notifications = new Dictionary<ulong, List<Notification>>();

        private void CheckNotifications()
        {
            if (_notifications.Count > 0)
            {
                foreach (var entry in _notifications.ToList())
                {
                    var notification = entry.Value.ElementAt(0);

                    SendNotification(notification);

                    entry.Value.Remove(notification);

                    if (entry.Value.Count == 0)
                    {
                        _notifications.Remove(entry.Key);
                    }
                }
            }
        }

        private static void QueueNotification(BasePlayer player, string key, params object[] args)
        {
            if (!player.IsReallyConnected())
            {
                return;
            }

            string message = m(key, player.UserIDString, args);

            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (config.EventMessages.Message)
            {
                Instance.Player.Message(player, message, config.Settings.ChatID);
            }

            if (config.GUIAnnouncement.Enabled || config.UI.AA.Enabled || config.EventMessages.NotifyType != -1)
            {
                string messageEx = mx(key, player.UserIDString, args);

                List<Notification> notifications;
                if (!Instance._notifications.TryGetValue(player.userID, out notifications))
                {
                    Instance._notifications[player.userID] = notifications = new List<Notification>();
                }

                notifications.Add(new Notification
                {
                    player = player,
                    messageEx = messageEx
                });
            }
        }

        private static void SendNotification(Notification notification)
        {
            if (!notification.player.IsReallyConnected())
            {
                return;
            }

            if (config.GUIAnnouncement.Enabled && Instance.GUIAnnouncements.CanCall())
            {
                Instance.GUIAnnouncements?.Call("CreateAnnouncement", notification.messageEx, config.GUIAnnouncement.TintColor, config.GUIAnnouncement.TextColor, notification.player);
            }

            if (config.UI.AA.Enabled && Instance.AdvancedAlerts.CanCall())
            {
                Instance.AdvancedAlerts?.Call("SpawnAlert", notification.player, "hook", notification.messageEx, config.UI.AA.AnchorMin, config.UI.AA.AnchorMax, config.UI.AA.Time);
            }

            if (config.EventMessages.NotifyType != -1 && Instance.Notify.CanCall())
            {
                Instance.Notify?.Call("SendNotify", notification.player, config.EventMessages.NotifyType, notification.messageEx);
            }
        }

        protected new static void Puts(string format, params object[] args)
        {
            Interface.Oxide.LogInfo("[{0}] {1}", "Raidable Bases", (args.Length != 0) ? string.Format(format, args) : format);
        }

        private static Configuration config;

        private static Dictionary<string, List<ulong>> DefaultImportedSkins
        {
            get
            {
                return new Dictionary<string, List<ulong>>
                {
                    ["jacket.snow"] = new List<ulong> { 785868744, 939797621 },
                    ["knife.bone"] = new List<ulong> { 1228176194, 2038837066 }
                };
            }
        }

        private static Dictionary<RaidableMode, List<RaidableBaseCustomCostOptions>> DefaultCustomCosts
        {
            get
            {
                return new Dictionary<RaidableMode, List<RaidableBaseCustomCostOptions>>
                {
                    [RaidableMode.Easy] = new List<RaidableBaseCustomCostOptions> { new RaidableBaseCustomCostOptions(50) },
                    [RaidableMode.Medium] = new List<RaidableBaseCustomCostOptions> { new RaidableBaseCustomCostOptions(100) },
                    [RaidableMode.Hard] = new List<RaidableBaseCustomCostOptions> { new RaidableBaseCustomCostOptions(150) },
                    [RaidableMode.Expert] = new List<RaidableBaseCustomCostOptions> { new RaidableBaseCustomCostOptions(200) },
                    [RaidableMode.Nightmare] = new List<RaidableBaseCustomCostOptions> { new RaidableBaseCustomCostOptions(250) }
                };
            }
        }

        private static List<PasteOption> DefaultPasteOptions
        {
            get
            {
                return new List<PasteOption>
                {
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
                    ["Easy Bases"] = new BuildingOptions(RaidableMode.Easy, "EasyBase1", "EasyBase2", "EasyBase3", "EasyBase4", "EasyBase5")
                    {
                        NPC = new NpcSettings(15f, 3)
                    },
                    ["Medium Bases"] = new BuildingOptions(RaidableMode.Medium, "MediumBase1", "MediumBase2", "MediumBase3", "MediumBase4", "MediumBase5")
                    {
                        NPC = new NpcSettings(20f, 6)
                    },
                    ["Hard Bases"] = new BuildingOptions(RaidableMode.Hard, "HardBase1", "HardBase2", "HardBase3", "HardBase4", "HardBase5")
                    {
                        NPC = new NpcSettings(20f, 9)
                    },
                    ["Expert Bases"] = new BuildingOptions(RaidableMode.Expert, "ExpertBase1", "ExpertBase2", "ExpertBase3", "ExpertBase4", "ExpertBase5")
                    {
                        NPC = new NpcSettings(25f, 12)
                    },
                    ["Nightmare Bases"] = new BuildingOptions(RaidableMode.Nightmare, "NightmareBase1", "NightmareBase2", "NightmareBase3", "NightmareBase4", "NightmareBase5")
                    {
                        NPC = new NpcSettings(25f, 12)
                    }
                };
            }
        }

        private static List<LootItem> DefaultLoot
        {
            get
            {
                return new List<LootItem>
                {
                    new LootItem("ammo.pistol", 40, 40),
                    new LootItem("ammo.pistol.fire", 40, 40),
                    new LootItem("ammo.pistol.hv", 40, 40),
                    new LootItem("ammo.rifle", 60, 60),
                    new LootItem("ammo.rifle.explosive", 60, 60),
                    new LootItem("ammo.rifle.hv", 60, 60),
                    new LootItem("ammo.rifle.incendiary", 60, 60),
                    new LootItem("ammo.shotgun", 24, 24),
                    new LootItem("ammo.shotgun.slug", 40, 40),
                    new LootItem("surveycharge", 20, 20),
                    new LootItem("bucket.helmet", 1, 1),
                    new LootItem("cctv.camera", 1, 1),
                    new LootItem("coffeecan.helmet", 1, 1),
                    new LootItem("explosive.timed", 1, 1),
                    new LootItem("metal.facemask", 1, 1),
                    new LootItem("metal.plate.torso", 1, 1),
                    new LootItem("mining.quarry", 1, 1),
                    new LootItem("pistol.m92", 1, 1),
                    new LootItem("rifle.ak", 1, 1),
                    new LootItem("rifle.bolt", 1, 1),
                    new LootItem("rifle.lr300", 1, 1),
                    new LootItem("shotgun.pump", 1, 1),
                    new LootItem("shotgun.spas12", 1, 1),
                    new LootItem("smg.2", 1, 1),
                    new LootItem("smg.mp5", 1, 1),
                    new LootItem("smg.thompson", 1, 1),
                    new LootItem("supply.signal", 1, 1),
                    new LootItem("targeting.computer", 1, 1),
                    new LootItem("metal.refined", 150, 150),
                    new LootItem("stones", 7500, 15000),
                    new LootItem("sulfur", 2500, 7500),
                    new LootItem("metal.fragments", 2500, 7500),
                    new LootItem("charcoal", 1000, 5000),
                    new LootItem("gunpowder", 1000, 3500),
                    new LootItem("scrap", 100, 150)
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
            [JsonProperty(PropertyName = "Apply All Lockouts Everytime")]
            public bool Global { get; set; }

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
            [JsonProperty(PropertyName = "Allow Max Amount Increase From Difficulties Disabled On A Specific Day Of The Week")]
            public bool Merge { get; set; }

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

            public bool Any() => Easy > -1 || Medium > -1 || Hard > -1 || Expert > -1 || Nightmare > -1;

            public int Get(RaidableMode a)
            {
                int t = GetInternal(a);

                if (t <= 0 || !Merge)
                {
                    return t;
                }

                foreach (RaidableMode b in Enum.GetValues(typeof(RaidableMode)))
                {
                    if (a == b || CanSpawnDifficultyToday(b))
                    {
                        continue;
                    }

                    int m = GetInternal(b);

                    if (m <= 0)
                    {
                        continue;
                    }

                    t += m;
                }

                return t;
            }

            private int GetInternal(RaidableMode mode)
            {
                switch (mode)
                {
                    case RaidableMode.Easy: return Easy;
                    case RaidableMode.Medium: return Medium;
                    case RaidableMode.Hard: return Hard;
                    case RaidableMode.Expert: return Expert;
                    case RaidableMode.Nightmare: return Nightmare;
                    default: return 0;
                }
            }
        }

        public class PluginSettingsBaseChances
        {
            [JsonProperty(PropertyName = "Easy")]
            public decimal Easy { get; set; } = -1m;

            [JsonProperty(PropertyName = "Medium")]
            public decimal Medium { get; set; } = -1m;

            [JsonProperty(PropertyName = "Hard")]
            public decimal Hard { get; set; } = -1m;

            [JsonProperty(PropertyName = "Expert")]
            public decimal Expert { get; set; } = -1m;

            [JsonProperty(PropertyName = "Nightmare")]
            public decimal Nightmare { get; set; } = -1m;

            [JsonProperty(PropertyName = "Use Cumulative Probability")]
            public bool Cumulative { get; set; } = true;

            public decimal Get(RaidableMode mode)
            {
                switch (mode)
                {
                    case RaidableMode.Easy: return Easy;
                    case RaidableMode.Medium: return Medium;
                    case RaidableMode.Hard: return Hard;
                    case RaidableMode.Expert: return Expert;
                    case RaidableMode.Nightmare: default: return Nightmare;
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

            public string Get(RaidableMode mode)
            {
                string hex;

                switch (mode)
                {
                    case RaidableMode.Easy: hex = Easy; break;
                    case RaidableMode.Medium: hex = Medium; break;
                    case RaidableMode.Hard: hex = Hard; break;
                    case RaidableMode.Expert: hex = Expert; break;
                    case RaidableMode.Nightmare: default: hex = Nightmare; break;
                }

                return hex.StartsWith("#") ? hex : $"#{hex}";
            }
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

            public string Get(RaidableMode mode)
            {
                string hex;

                switch (mode)
                {
                    case RaidableMode.Easy: hex = Easy; break;
                    case RaidableMode.Medium: hex = Medium; break;
                    case RaidableMode.Hard: hex = Hard; break;
                    case RaidableMode.Expert: hex = Expert; break;
                    case RaidableMode.Nightmare:
                    default: hex = Nightmare; break;
                }

                if (!hex.StartsWith("#"))
                {
                    hex = $"#{hex}";
                }

                return hex;
            }
        }

        public class PluginSettingsBaseManagementMountables
        {
            [JsonProperty(PropertyName = "All Controlled Mounts")]
            public bool ControlledMounts { get; set; }

            [JsonProperty(PropertyName = "All Other Mounts")]
            public bool Other { get; set; }

            [JsonProperty(PropertyName = "Boats")]
            public bool Boats { get; set; }

            [JsonProperty(PropertyName = "Cars (Basic)")]
            public bool BasicCars { get; set; }

            [JsonProperty(PropertyName = "Cars (Modular)")]
            public bool ModularCars { get; set; }

            [JsonProperty(PropertyName = "Chinook")]
            public bool CH47 { get; set; }

            [JsonProperty(PropertyName = "Horses")]
            public bool Horses { get; set; }

            [JsonProperty(PropertyName = "HotAirBalloon")]
            public bool HotAirBalloon { get; set; } = true;

            [JsonProperty(PropertyName = "Jetpacks")]
            public bool Jetpacks { get; set; }

            [JsonProperty(PropertyName = "MiniCopters")]
            public bool MiniCopters { get; set; }

            [JsonProperty(PropertyName = "Pianos")]
            public bool Pianos { get; set; } = true;

            [JsonProperty(PropertyName = "Scrap Transport Helicopters")]
            public bool Scrap { get; set; }

            [JsonProperty(PropertyName = "Snowmobiles")]
            public bool Snowmobile { get; set; }
        }

        public class BuildingOptionsSetupSettings
        {
            [JsonProperty(PropertyName = "Amount Of Entities To Spawn Per Batch")]
            public int SpawnLimit { get; set; } = 1;

            [JsonProperty(PropertyName = "Amount Of Entities To Despawn Per Batch")]
            public int DespawnLimit { get; set; } = 10;

            [JsonProperty(PropertyName = "Height Adjustment Applied To This Paste")]
            public float PasteHeightAdjustment { get; set; }

            [JsonProperty(PropertyName = "Force All Bases To Spawn At Height Level (0 = Water)")]
            public float ForcedHeight { get; set; } = -1f;

            [JsonProperty(PropertyName = "Foundations Immune To Damage When Forced Height Is Applied")]
            public bool FoundationsImmune { get; set; }

            [JsonProperty(PropertyName = "Recalculate Spawn Position Before Paste")]
            public bool Recalculate { get; set; }

            [JsonProperty(PropertyName = "Teleport Entities Underworld Before Despawning")]
            public bool TeleportEntities { get; set; }
        }

        public class PluginSettingsBaseManagePlayerAmountsEventTypes
        {
            [JsonProperty(PropertyName = "Buyable Events")]
            public int Buyable { get; set; }

            [JsonProperty(PropertyName = "Maintained Events")]
            public int Maintained { get; set; }

            [JsonProperty(PropertyName = "Manual Events")]
            public int Manual { get; set; }

            [JsonProperty(PropertyName = "Scheduled Events")]
            public int Scheduled { get; set; }
        }

        public class PluginSettingsBaseManagementPlayerAmounts
        {
            [JsonProperty(PropertyName = "Easy")]
            public PluginSettingsBaseManagePlayerAmountsEventTypes Easy { get; set; } = new PluginSettingsBaseManagePlayerAmountsEventTypes();

            [JsonProperty(PropertyName = "Medium")]
            public PluginSettingsBaseManagePlayerAmountsEventTypes Medium { get; set; } = new PluginSettingsBaseManagePlayerAmountsEventTypes();

            [JsonProperty(PropertyName = "Hard")]
            public PluginSettingsBaseManagePlayerAmountsEventTypes Hard { get; set; } = new PluginSettingsBaseManagePlayerAmountsEventTypes();

            [JsonProperty(PropertyName = "Expert")]
            public PluginSettingsBaseManagePlayerAmountsEventTypes Expert { get; set; } = new PluginSettingsBaseManagePlayerAmountsEventTypes();

            [JsonProperty(PropertyName = "Nightmare")]
            public PluginSettingsBaseManagePlayerAmountsEventTypes Nightmare { get; set; } = new PluginSettingsBaseManagePlayerAmountsEventTypes();

            public int Get(RaidableMode mode, RaidableType type)
            {
                switch (mode)
                {
                    case RaidableMode.Easy:
                        switch (type)
                        {
                            case RaidableType.Maintained: return Easy.Maintained;
                            case RaidableType.Scheduled: return Easy.Scheduled;
                            case RaidableType.Purchased: return Easy.Buyable;
                            default: return Easy.Manual;
                        }
                    case RaidableMode.Medium:
                        switch (type)
                        {
                            case RaidableType.Maintained: return Medium.Maintained;
                            case RaidableType.Scheduled: return Medium.Scheduled;
                            case RaidableType.Purchased: return Medium.Buyable;
                            default: return Medium.Manual;
                        }
                    case RaidableMode.Hard:
                        switch (type)
                        {
                            case RaidableType.Maintained: return Hard.Maintained;
                            case RaidableType.Scheduled: return Hard.Scheduled;
                            case RaidableType.Purchased: return Hard.Buyable;
                            default: return Hard.Manual;
                        }
                    case RaidableMode.Expert:
                        switch (type)
                        {
                            case RaidableType.Maintained: return Expert.Maintained;
                            case RaidableType.Scheduled: return Expert.Scheduled;
                            case RaidableType.Purchased: return Expert.Buyable;
                            default: return Expert.Manual;
                        }
                    case RaidableMode.Nightmare:
                        switch (type)
                        {
                            case RaidableType.Maintained: return Nightmare.Maintained;
                            case RaidableType.Scheduled: return Nightmare.Scheduled;
                            case RaidableType.Purchased: return Nightmare.Buyable;
                            default: return Nightmare.Manual;
                        }
                }

                return 0;
            }
        }

        public class PluginSettingsBaseManagementDrops
        {
            [JsonProperty(PropertyName = "Auto Turrets")]
            public bool AutoTurret { get; set; }

            [JsonProperty(PropertyName = "Flame Turret")]
            public bool FlameTurret { get; set; }

            [JsonProperty(PropertyName = "Fog Machine")]
            public bool FogMachine { get; set; }

            [JsonProperty(PropertyName = "Gun Trap")]
            public bool GunTrap { get; set; }

            [JsonProperty(PropertyName = "SAM Site")]
            public bool SamSite { get; set; }
        }

        public class PluginSettingsBaseManagement
        {
            [JsonProperty(PropertyName = "Eject Mounts")]
            public PluginSettingsBaseManagementMountables Mounts { get; set; } = new PluginSettingsBaseManagementMountables();

            [JsonProperty(PropertyName = "Max Amount Of Players Allowed To Enter Each Difficulty (0 = infinite, -1 = none)")]
            public PluginSettingsBaseManagementPlayerAmounts Players { get; set; } = new PluginSettingsBaseManagementPlayerAmounts();

            [JsonProperty(PropertyName = "Max Amount Allowed To Automatically Spawn Per Difficulty (0 = infinite, -1 = disabled)")]
            public PluginSettingsBaseAmounts Amounts { get; set; } = new PluginSettingsBaseAmounts();

            [JsonProperty(PropertyName = "Chance To Automatically Spawn Each Difficulty (-1 = ignore)")]
            public PluginSettingsBaseChances Chances { get; set; } = new PluginSettingsBaseChances();

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

            [JsonProperty(PropertyName = "Additional Containers To Include As Boxes", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Inherit { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Difficulty Colors (Border)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public PluginSettingsColors1 Colors1 { get; set; } = new PluginSettingsColors1();

            [JsonProperty(PropertyName = "Difficulty Colors (Inner)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public PluginSettingsColors2 Colors2 { get; set; } = new PluginSettingsColors2();

            [JsonProperty(PropertyName = "Entities Allowed To Drop Loot")]
            public PluginSettingsBaseManagementDrops DropLoot { get; set; } = new PluginSettingsBaseManagementDrops();

            [JsonProperty(PropertyName = "Allow Teleport")]
            public bool AllowTeleport { get; set; }

            [JsonProperty(PropertyName = "Allow Cupboard Loot To Drop")]
            public bool AllowCupboardLoot { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Players To Build")]
            public bool AllowBuilding { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Players To Use Ladders")]
            public bool AllowLadders { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Players To Upgrade Event Buildings")]
            public bool AllowUpgrade { get; set; }

            [JsonProperty(PropertyName = "Allow Player Bags To Be Lootable At PVP Bases")]
            public bool PlayersLootableInPVP { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Player Bags To Be Lootable At PVE Bases")]
            public bool PlayersLootableInPVE { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Players To Loot Traps")]
            public bool LootableTraps { get; set; }

            [JsonProperty(PropertyName = "Allow Npcs To Target Other Npcs")]
            public bool TargetNpcs { get; set; }

            [JsonProperty(PropertyName = "Allow Raid Bases On Roads")]
            public bool AllowOnRoads { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Raid Bases On Rivers")]
            public bool AllowOnRivers { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Raid Bases On Building Topology")]
            public bool AllowOnBuildingTopology { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Vending Machines To Broadcast")]
            public bool AllowBroadcasting { get; set; }

            [JsonProperty(PropertyName = "Backpacks Can Be Opened At PVE Bases")]
            public bool BackpacksOpenPVE { get; set; } = true;

            [JsonProperty(PropertyName = "Backpacks Can Be Opened At PVP Bases")]
            public bool BackpacksOpenPVP { get; set; } = true;

            [JsonProperty(PropertyName = "Backpacks Drop At PVE Bases")]
            public bool BackpacksPVE { get; set; }

            [JsonProperty(PropertyName = "Backpacks Drop At PVP Bases")]
            public bool BackpacksPVP { get; set; }

            [JsonProperty(PropertyName = "Block Npc Kits Plugin")]
            public bool BlockNpcKits { get; set; }

            [JsonProperty(PropertyName = "Block Helicopter Damage To Bases")]
            public bool BlockHelicopterDamage { get; set; }

            [JsonProperty(PropertyName = "Block Mounted Damage To Bases And Players")]
            public bool BlockMounts { get; set; }

            [JsonProperty(PropertyName = "Block RestoreUponDeath Plugin For PVP Bases")]
            public bool BlockRestorePVP { get; set; }

            [JsonProperty(PropertyName = "Block RestoreUponDeath Plugin For PVE Bases")]
            public bool BlockRestorePVE { get; set; }

            [JsonProperty(PropertyName = "Block LifeSupport Plugin")]
            public bool NoLifeSupport { get; set; } = true;

            [JsonProperty(PropertyName = "Bypass Lock Treasure To First Attacker For PVE Bases")]
            public bool BypassUseOwnersForPVE { get; set; }

            [JsonProperty(PropertyName = "Bypass Lock Treasure To First Attacker For PVP Bases")]
            public bool BypassUseOwnersForPVP { get; set; }

            [JsonProperty(PropertyName = "Despawn Spawned Mounts")]
            public bool DespawnMounts { get; set; } = true;

            [JsonProperty(PropertyName = "Do Not Destroy Player Built Deployables")]
            public bool DoNotDestroyDeployables { get; set; } = true;

            [JsonProperty(PropertyName = "Do Not Destroy Player Built Structures")]
            public bool DoNotDestroyStructures { get; set; } = true;

            [JsonProperty(PropertyName = "Divide Rewards Among All Raiders")]
            public bool DivideRewards { get; set; } = true;

            [JsonProperty(PropertyName = "Draw Corpse Time (Seconds)")]
            public float DrawTime { get; set; } = 300f;

            [JsonProperty(PropertyName = "Eject Sleepers Before Spawning Base")]
            public bool EjectSleepers { get; set; } = true;

            [JsonProperty(PropertyName = "Eject Scavengers When Raid Is Completed")]
            public bool EjectScavengers { get; set; } = true;

            [JsonProperty(PropertyName = "Extra Distance To Spawn From Monuments")]
            public float MonumentDistance { get; set; }

            [JsonProperty(PropertyName = "Move Cookables Into Ovens")]
            public bool Cook { get; set; } = true;

            [JsonProperty(PropertyName = "Move Food Into BBQ Or Fridge")]
            public bool Food { get; set; } = true;

            [JsonProperty(PropertyName = "Blacklist For BBQ And Fridge", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public HashSet<string> Foods { get; set; } = new HashSet<string> { "syrup", "pancakes" };

            [JsonProperty(PropertyName = "Move Resources Into Tool Cupboard")]
            public bool Cupboard { get; set; } = true;

            [JsonProperty(PropertyName = "Move Items Into Lockers")]
            public bool Lockers { get; set; } = true;

            [JsonProperty(PropertyName = "Lock Treasure To First Attacker")]
            public bool UseOwners { get; set; } = true;

            [JsonProperty(PropertyName = "Lock Treasure Max Inactive Time (Minutes)")]
            public float LockTime { get; set; } = 20f;

            [JsonProperty(PropertyName = "Assign Lockout When Lock Treasure Max Inactive Time Expires")]
            public bool SetLockout { get; set; }

            [JsonProperty(PropertyName = "Lock Players To Raid Base After Entering Zone")]
            public bool LockToRaidOnEnter { get; set; }

            [JsonProperty(PropertyName = "Only Award First Attacker and Allies")]
            public bool OnlyAwardAllies { get; set; }

            [JsonProperty(PropertyName = "Only Award Owner Of Raid")]
            public bool OnlyAwardOwner { get; set; }

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

            [JsonProperty(PropertyName = "Players With PVP Delay Can Damage Anything Inside Zone")]
            public bool PVPDelayDamageInside { get; set; }

            [JsonProperty(PropertyName = "Players With PVP Delay Can Damage Other Players With PVP Delay Anywhere")]
            public bool PVPDelayAnywhere { get; set; }

            [JsonProperty(PropertyName = "PVP Delay Between Zone Hopping")]
            public float PVPDelay { get; set; } = 10f;

            [JsonProperty(PropertyName = "Prevent Fire From Spreading")]
            public bool PreventFireFromSpreading { get; set; } = true;

            [JsonProperty(PropertyName = "Prevent Players From Hogging Raids")]
            public bool PreventHogging { get; set; } = true;

            [JsonProperty(PropertyName = "Prevent Fall Damage When Base Despawns")]
            public bool PreventFallDamage { get; set; }

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

            [JsonProperty(PropertyName = "Traps And Turrets Ignore Users Using NOCLIP")]
            public bool IgnoreFlying { get; set; }

            [JsonProperty(PropertyName = "Use Random Codes On Code Locks")]
            public bool RandomCodes { get; set; } = true;

            [JsonProperty(PropertyName = "Wait To Start Despawn Timer When Base Takes Damage From Player")]
            public bool Engaged { get; set; }

            [JsonProperty(PropertyName = "Maximum Water Depth For All Npcs")]
            public float WaterDepth { get; set; } = 3f;
        }

        public class PluginSettingsMapMarkers
        {
            [JsonProperty(PropertyName = "Marker Name")]
            public string MarkerName { get; set; } = "Raidable Base Event";

            [JsonProperty(PropertyName = "Radius")]
            public float Radius { get; set; } = 0.25f;

            [JsonProperty(PropertyName = "Use Vending Map Marker")]
            public bool UseVendingMarker { get; set; } = true;

            [JsonProperty(PropertyName = "Show Owners Name on Map Marker")]
            public bool ShowOwnersName { get; set; } = true;

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

            [JsonProperty(PropertyName = "Allowed Zone Manager Zones", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Inclusions { get; set; } = new List<string> { "pvp", "99999999" };

            [JsonProperty(PropertyName = "Extended Distance To Spawn Away From Zone Manager Zones")]
            public float ZoneDistance { get; set; } = 25f;

            [JsonProperty(PropertyName = "Blacklisted Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> BlacklistedCommands { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Buyable Event Costs")]
            public RaidableBaseCostOptions Costs { get; set; } = new RaidableBaseCostOptions();

            [JsonProperty(PropertyName = "Economics Buy Raid Costs (0 = disabled)")]
            public RaidableBaseEconomicsOptions Economics { get; set; } = new RaidableBaseEconomicsOptions();

            [JsonProperty(PropertyName = "ServerRewards Buy Raid Costs (0 = disabled)")]
            public RaidableBaseServerRewardsOptions ServerRewards { get; set; } = new RaidableBaseServerRewardsOptions();

            [JsonProperty(PropertyName = "Custom Buy Raid Cost", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<RaidableMode, List<RaidableBaseCustomCostOptions>> Custom { get; set; } = DefaultCustomCosts;

            [JsonProperty(PropertyName = "Automatically Teleport Admins To Their Map Marker Positions")]
            public bool TeleportMarker { get; set; } = true;

            [JsonProperty(PropertyName = "Block Wizardry Plugin At Events")]
            public bool NoWizardry { get; set; }

            [JsonProperty(PropertyName = "Chat Steam64ID")]
            public ulong ChatID { get; set; }

            [JsonProperty(PropertyName = "Expansion Mode (Dangerous Treasures)")]
            public bool ExpansionMode { get; set; }

            [JsonProperty(PropertyName = "Remove Admins From Raiders List")]
            public bool RemoveAdminRaiders { get; set; }

            [JsonProperty(PropertyName = "Show X Z Coordinates")]
            public bool ShowXZ { get; set; }

            [JsonProperty(PropertyName = "Buy Raid Command")]
            public string BuyCommand { get; set; } = "buyraid";

            [JsonProperty(PropertyName = "Event Command")]
            public string EventCommand { get; set; } = "rbe";

            [JsonProperty(PropertyName = "Hunter Command")]
            public string HunterCommand { get; set; } = "rb";

            [JsonProperty(PropertyName = "Server Console Command")]
            public string ConsoleCommand { get; set; } = "rbevent";
        }

        public class EventMessageRewardSettings
        {
            [JsonProperty(PropertyName = "Flying")]
            public bool Flying { get; set; }

            [JsonProperty(PropertyName = "Vanished")]
            public bool Vanished { get; set; }

            [JsonProperty(PropertyName = "Inactive")]
            public bool Inactive { get; set; } = true;

            [JsonProperty(PropertyName = "Not An Ally")]
            public bool NotAlly { get; set; } = true;

            [JsonProperty(PropertyName = "Not The Owner")]
            public bool NotOwner { get; set; } = true;

            [JsonProperty(PropertyName = "Not A Participant")]
            public bool NotParticipant { get; set; } = true;

            [JsonProperty(PropertyName = "Remove Admins From Raiders List")]
            public bool RemoveAdmin { get; set; }
        }

        public class EventMessageSettings
        {
            [JsonProperty(PropertyName = "Ineligible For Rewards")]
            public EventMessageRewardSettings Rewards { get; set; } = new EventMessageRewardSettings();

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

            [JsonProperty(PropertyName = "Notify Plugin - Type (-1 = disabled)")]
            public int NotifyType { get; set; } = -1;

            [JsonProperty(PropertyName = "Notification Interval")]
            public float Interval { get; set; } = 1f;

            [JsonProperty(PropertyName = "Send Messages To Player")]
            public bool Message { get; set; } = true;
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

        public class NpcSettingsInsideBase
        {
            [JsonProperty(PropertyName = "Spawn On Floors")]
            public bool SpawnOnFloors { get; set; }

            [JsonProperty(PropertyName = "Spawn On Beds")]
            public bool SpawnOnBeds { get; set; }

            [JsonProperty(PropertyName = "Spawn On Rugs")]
            public bool SpawnOnRugs { get; set; }

            [JsonProperty(PropertyName = "Spawn On Rugs With Skin Only")]
            public ulong SpawnOnRugsSkin { get; set; } = 1;

            [JsonProperty(PropertyName = "Spawn Murderers Outside")]
            public bool SpawnMurderersOutside { get; set; } = true;

            [JsonProperty(PropertyName = "Spawn Scientists Outside")]
            public bool SpawnScientistsOutside { get; set; } = true;

            [JsonProperty(PropertyName = "Minimum Inside (-1 = ignore)")]
            public int Min { get; set; } = -1;

            [JsonProperty(PropertyName = "Maximum Inside (-1 = ignore)")]
            public int Max { get; set; } = -1;
        }

        public class NpcKitSettings
        {
            [JsonProperty(PropertyName = "Helm", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Helm { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Torso", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Torso { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Pants", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Pants { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Gloves", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Gloves { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Boots", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Boots { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Shirt", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Shirt { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Kilts", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Kilts { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Weapon", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Weapon { get; set; } = new List<string>();

            public NpcKitSettings(bool isMurderer)
            {
                if (isMurderer)
                {
                    Helm.Add("metal.facemask");
                    Torso.Add("metal.plate.torso");
                    Pants.Add("pants");
                    Gloves.Add("tactical.gloves");
                    Boots.Add("boots.frog");
                    Shirt.Add("tshirt");
                    Weapon.Add("machete");
                }
                else
                {
                    Torso.Add("hazmatsuit_scientist_peacekeeper");
                    Weapon.Add("rifle.ak");
                }
            }
        }

        public class ScientistLootSettings
        {
            [JsonProperty(PropertyName = "Prefab ID List", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> IDs { get; set; } = new List<string> { "cargo", "turret_any", "ch47_gunner", "excavator", "full_any", "heavy", "junkpile_pistol", "oilrig", "patrol", "peacekeeper", "roam", "roamtethered" };

            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; }

            public uint GetRandom()
            {
                if (IDs.Count > 0)
                {
                    switch (IDs.GetRandom())
                    {
                        case "cargo": return 3623670799;
                        case "turret_any": return 1639447304;
                        case "ch47_gunner": return 1017671955;
                        case "excavator": return 4293908444;
                        case "full_any": return 1539172658;
                        case "heavy": return 1536035819;
                        case "junkpile_pistol": return 2066159302;
                        case "oilrig": return 548379897;
                        case "patrol": return 4272904018;
                        case "peacekeeper": return 2390854225;
                        case "roam": return 4199494415;
                        case "roamtethered": return 529928930;
                    }
                }

                return 1536035819;
            }
        }

        public class NpcMultiplierSettings
        {
            [JsonProperty(PropertyName = "Explosive Damage Multiplier")]
            public float ExplosiveDamageMultiplier { get; set; } = 1f;

            [JsonProperty(PropertyName = "Gun Damage Multiplier")]
            public float ProjectileDamageMultiplier { get; set; } = 1f;

            [JsonProperty(PropertyName = "Melee Damage Multiplier")]
            public float MeleeDamageMultiplier { get; set; } = 1f;
        }

        public class NpcSettings
        {
            public NpcSettings() { }

            public NpcSettings(float accuracy, int spawnAmount)
            {
                Accuracy = accuracy;
                SpawnAmount = SpawnMinAmount = spawnAmount;
            }

            [JsonProperty(PropertyName = "Damage Multipliers")]
            public NpcMultiplierSettings Multipliers { get; set; } = new NpcMultiplierSettings();

            [JsonProperty(PropertyName = "Spawn Inside Bases")]
            public NpcSettingsInsideBase Inside { get; set; } = new NpcSettingsInsideBase();

            [JsonProperty(PropertyName = "Murderer (Items)")]
            public NpcKitSettings MurdererItems { get; set; } = new NpcKitSettings(true);

            [JsonProperty(PropertyName = "Scientist (Items)")]
            public NpcKitSettings ScientistItems { get; set; } = new NpcKitSettings(false);

            [JsonProperty(PropertyName = "Murderer Kits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> MurdererKits { get; set; } = new List<string> { "murderer_kit_1", "murderer_kit_2" };

            [JsonProperty(PropertyName = "Scientist Kits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> ScientistKits { get; set; } = new List<string> { "scientist_kit_1", "scientist_kit_2" };

            [JsonProperty(PropertyName = "Murderer Items Dropped On Death", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<LootItem> MurdererDrops { get; set; } = new List<LootItem> { new LootItem("ammo.pistol", 1, 30) };

            [JsonProperty(PropertyName = "Scientist Items Dropped On Death", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<LootItem> ScientistDrops { get; set; } = new List<LootItem> { new LootItem("ammo.rifle", 1, 30) };

            [JsonProperty(PropertyName = "Spawn Alternate Default Scientist Loot")]
            public ScientistLootSettings AlternateScientistLoot { get; set; } = new ScientistLootSettings();

            [JsonProperty(PropertyName = "Random Names", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> RandomNames { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty(PropertyName = "Amount That Can Throw Weapons")]
            public int Thrown { get; set; } = 2;

            [JsonProperty(PropertyName = "Amount To Spawn")]
            public int SpawnAmount { get; set; } = 3;

            [JsonProperty(PropertyName = "Minimum Amount To Spawn")]
            public int SpawnMinAmount { get; set; } = 1;

            [JsonProperty(PropertyName = "Spawn Random Amount")]
            public bool SpawnRandomAmount { get; set; }

            [JsonProperty(PropertyName = "Allow Npcs To Roofcamp")]
            public bool Roofcampers { get; set; }

            [JsonProperty(PropertyName = "Allow Npcs To Counter Raid")]
            public bool CounterRaid { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Npcs To Leave Dome When Attacking")]
            public bool CanLeave { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Npcs To Shoot Players Outside Of The Dome")]
            public bool CanShoot { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Npcs To Play Catch When Equipped With Explosives")]
            public bool PlayCatch { get; set; }

            [JsonProperty(PropertyName = "Aggression Range")]
            public float AggressionRange { get; set; } = 70f;

            [JsonProperty(PropertyName = "Block Damage Outside To Npcs When Not Allowed To Leave Dome")]
            public bool BlockOutsideDamageOnLeave { get; set; } = true;

            [JsonProperty(PropertyName = "Block Damage Outside Of The Dome To Npcs Inside")]
            public bool BlockOutsideDamageToNpcsInside { get; set; }

            [JsonProperty(PropertyName = "Despawn Inventory On Death")]
            public bool DespawnInventory { get; set; } = true;

            [JsonProperty(PropertyName = "Health For Murderers (100 min, 5000 max)")]
            public float MurdererHealth { get; set; } = 150f;

            [JsonProperty(PropertyName = "Health For Scientists (100 min, 5000 max)")]
            public float ScientistHealth { get; set; } = 150f;

            [JsonProperty(PropertyName = "Kill Underwater Npcs")]
            public bool KillUnderwater { get; set; } = true;

            [JsonProperty(PropertyName = "Player Traps And Turrets Ignore Npcs")]
            public bool IgnoreTrapsTurrets { get; set; }

            [JsonProperty(PropertyName = "Scientist Weapon Accuracy (0 - 100)")]
            public float Accuracy { get; set; } = 30f;

            [JsonProperty(PropertyName = "Spawn Murderers And Scientists")]
            public bool SpawnBoth { get; set; } = true;

            [JsonProperty(PropertyName = "Spawn Murderers")]
            public bool SpawnMurderers { get; set; }

            [JsonProperty(PropertyName = "Spawn Scientists Only")]
            public bool SpawnScientistsOnly { get; set; }

            [JsonProperty(PropertyName = "Use Dangerous Treasures NPCs")]
            public bool UseExpansionNpcs { get; set; }
        }

        public class PasteOption
        {
            [JsonProperty(PropertyName = "Option")]
            public string Key { get; set; }

            [JsonProperty(PropertyName = "Value")]
            public string Value { get; set; }
        }

        public class BuildingLevels
        {
            [JsonProperty(PropertyName = "Level 2 - Final Death")]
            public bool Level2 { get; set; }
        }

        public class DoorTypes
        {
            [JsonProperty(PropertyName = "Wooden")]
            public bool Wooden { get; set; }

            [JsonProperty(PropertyName = "Metal")]
            public bool Metal { get; set; }

            [JsonProperty(PropertyName = "HQM")]
            public bool HQM { get; set; }

            [JsonProperty(PropertyName = "Include Garage Doors")]
            public bool GarageDoor { get; set; }

            public bool Any() => Wooden || Metal || HQM;
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

        public class BuildingOptionsAutoTurrets
        {
            [JsonProperty(PropertyName = "Aim Cone")]
            public float AimCone { get; set; } = 5f;

            [JsonProperty(PropertyName = "Minimum Damage Modifier")]
            public float Min { get; set; } = 1f;

            [JsonProperty(PropertyName = "Maximum Damage Modifier")]
            public float Max { get; set; } = 1f;

            [JsonProperty(PropertyName = "Start Health")]
            public float Health { get; set; } = 1000f;

            [JsonProperty(PropertyName = "Sight Range")]
            public float SightRange { get; set; } = 30f;

            [JsonProperty(PropertyName = "Double Sight Range When Shot")]
            public bool AutoAdjust { get; set; }

            [JsonProperty(PropertyName = "Set Hostile (False = Do Not Set Any Mode)")]
            public bool Hostile { get; set; } = true;

            [JsonProperty(PropertyName = "Requires Power Source")]
            public bool RequiresPower { get; set; }

            [JsonProperty(PropertyName = "Remove Equipped Weapon")]
            public bool RemoveWeapon { get; set; }

            [JsonProperty(PropertyName = "Random Weapons To Equip When Unequipped", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Shortnames { get; set; } = new List<string> { "rifle.ak" };
        }

        public class BuildingOptionsProtectionRadius
        {
            [JsonProperty(PropertyName = "Buyable Events")]
            public float Buyable { get; set; } = 50f;

            [JsonProperty(PropertyName = "Maintained Events")]
            public float Maintained { get; set; } = 50f;

            [JsonProperty(PropertyName = "Manual Events")]
            public float Manual { get; set; } = 50f;

            [JsonProperty(PropertyName = "Scheduled Events")]
            public float Scheduled { get; set; } = 50f;

            public void Set(float value)
            {
                Buyable = value;
                Maintained = value;
                Manual = value;
                Scheduled = value;
            }

            public float Get(RaidableType type)
            {
                switch (type)
                {
                    case RaidableType.Purchased: return Buyable;
                    case RaidableType.Maintained: return Maintained;
                    case RaidableType.Scheduled: return Scheduled;
                    case RaidableType.Manual: return Manual;
                    default: return Max();
                }
            }

            public float Max() => Mathf.Max(Buyable, Maintained, Manual, Scheduled);
        }

        public class BuildingOptionsBradleySettings
        {
            [JsonProperty(PropertyName = "Spawn Bradley When Base Spawns")]
            public bool SpawnImmediately { get; set; }

            [JsonProperty(PropertyName = "Spawn Bradley When Base Is Completed")]
            public bool SpawnCompleted { get; set; }

            [JsonProperty(PropertyName = "Chance To Spawn (Min)")]
            public float Min { get; set; } = 0.05f;

            [JsonProperty(PropertyName = "Chance To Spawn (Max)")]
            public float Max { get; set; } = 0.1f;

            [JsonProperty(PropertyName = "Health")]
            public float Health { get; set; } = 1000f;

            [JsonProperty(PropertyName = "Bullet Damage")]
            public float BulletDamage { get; set; } = 15f;

            [JsonProperty(PropertyName = "Crates")]
            public int Crates { get; set; } = 3;

            [JsonProperty(PropertyName = "Sight Range")]
            public float SightRange { get; set; } = 100f;

            [JsonProperty(PropertyName = "Double Sight Range When Shot")]
            public bool Vision { get; set; } = true;

            [JsonProperty(PropertyName = "Splash Radius")]
            public float Splash { get; set; } = 15f;
        }

        public class BuildingWaterOptions
        {
            [JsonProperty(PropertyName = "Allow Bases To Float Above Water")]
            public bool AllowSubmerged { get; set; }

            [JsonProperty(PropertyName = "Chance For Underwater Bases To Spawn (0-100) (BETA - WORK IN PROGRESS)")]
            public float Seabed { get; set; }

            [JsonProperty(PropertyName = "Prevent Bases From Floating Above Water By Also Checking Surrounding Area")]
            public bool SubmergedAreaCheck { get; set; }

            [JsonProperty(PropertyName = "Maximum Water Depth Level Used For Float Above Water Option")]
            public float WaterDepth { get; set; } = 1f;

            [JsonIgnore]
            public bool SpawnOnSeabed;
        }

        public class BuildingOptionsDifficultySpawns
        {
            [JsonProperty(PropertyName = "Spawns Database File (Optional)")]
            public string SpawnsFile { get; set; } = "none";

            [JsonProperty(PropertyName = "Ignore Safe Checks")]
            public bool Ignore { get; set; }

            [JsonProperty(PropertyName = "Ignore Safe Checks In X Radius Only")]
            public float SafeRadius { get; set; }

            [JsonProperty(PropertyName = "Ignore Player Entities At Custom Spawn Locations")]
            public bool Skip { get; set; }

            [JsonProperty(PropertyName = "Kill Sleeping Bags")]
            public bool KillSleepingBags { get; set; } = true;
        }

        public class BuildingOptionsElevators
        {
            [JsonProperty(PropertyName = "UI Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty(PropertyName = "Anchor Min")]
            public string Min { get; set; } = "0.406 0.915";

            [JsonProperty(PropertyName = "Anchor Max")]
            public string Max { get; set; } = "0.59 0.949";

            [JsonProperty(PropertyName = "Panel Color")]
            public string PanelColor { get; set; } = "#000000";

            [JsonProperty(PropertyName = "Panel Alpha")]
            public float PanelAlpha { get; set; } = 0f;

            [JsonProperty(PropertyName = "Required Access Level")]
            public int RequiredAccessLevel { get; set; }

            [JsonProperty(PropertyName = "Required Access Level Grants Permanent Use")]
            public bool RequiredAccessLevelOnce { get; set; }

            [JsonProperty(PropertyName = "Required Keycard Skin ID")]
            public ulong SkinID { get; set; } = 2690554489;

            [JsonProperty(PropertyName = "Requires Building Permission")]
            public bool RequiresBuildingPermission { get; set; }

            [JsonProperty(PropertyName = "Button Health")]
            public float ButtonHealth { get; set; } = 1000f;

            [JsonProperty(PropertyName = "Elevator Health")]
            public float ElevatorHealth { get; set; } = 600f;
        }

        public class BuildingOptions
        {
            public BuildingOptions() { }

            public BuildingOptions(RaidableMode mode, params string[] bases)
            {
                Mode = mode;
                PasteOptions = DefaultPasteOptions;
                AdditionalBases = new Dictionary<string, List<PasteOption>>();

                if (bases?.Length > 0)
                {
                    foreach (string value in bases)
                    {
                        AdditionalBases[value] = DefaultPasteOptions;
                    }
                }
            }

            [JsonProperty(PropertyName = "Difficulty (0 = easy, 1 = medium, 2 = hard, 3 = expert, 4 = nightmare)")]
            public RaidableMode Mode { get; set; } = RaidableMode.Easy;

            [JsonProperty(PropertyName = "Advanced Protection Radius")]
            public BuildingOptionsProtectionRadius ProtectionRadii { get; set; } = new BuildingOptionsProtectionRadius();

            [JsonProperty(PropertyName = "Advanced Setup Settings")]
            public BuildingOptionsSetupSettings Setup { get; set; } = new BuildingOptionsSetupSettings();

            [JsonProperty(PropertyName = "Elevators")]
            public BuildingOptionsElevators Elevators { get; set; } = new BuildingOptionsElevators();

            [JsonProperty(PropertyName = "Entities Not Allowed To Be Picked Up", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> BlacklistedPickupItems { get; set; } = new List<string> { "generator.small", "generator.static", "autoturret_deployed" };

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
            public BuildingGradeLevels Blocks { get; set; } = new BuildingGradeLevels();

            [JsonProperty(PropertyName = "Change Door Type To")]
            public DoorTypes Doors { get; set; } = new DoorTypes();

            [JsonProperty(PropertyName = "Auto Turrets")]
            public BuildingOptionsAutoTurrets AutoTurret { get; set; } = new BuildingOptionsAutoTurrets();

            [JsonProperty(PropertyName = "Player Building Restrictions")]
            public BuildingGradeLevels BuildingRestrictions { get; set; } = new BuildingGradeLevels();

            [JsonProperty(PropertyName = "Water Settings")]
            public BuildingWaterOptions Water { get; set; } = new BuildingWaterOptions();

            [JsonProperty(PropertyName = "Spawns Database")]
            public BuildingOptionsDifficultySpawns CustomSpawns { get; set; } = new BuildingOptionsDifficultySpawns();

            [JsonProperty(PropertyName = "Profile Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty(PropertyName = "Maximum Elevation Level")]
            public float Elevation { get; set; } = 2.5f;

            [JsonProperty(PropertyName = "Allow Players To Use MLRS")]
            public bool MLRS { get; set; } = true;

            [JsonProperty(PropertyName = "Add Code Lock To Unlocked Or KeyLocked Doors")]
            public bool DoorLock { get; set; } = true;

            [JsonProperty(PropertyName = "Add Code Lock To Tool Cupboards")]
            public bool LockPrivilege { get; set; }

            [JsonProperty(PropertyName = "Add Code Lock To Boxes")]
            public bool LockBoxes { get; set; }

            [JsonProperty(PropertyName = "Add Code Lock To Lockers")]
            public bool LockLockers { get; set; } = true;

            [JsonProperty(PropertyName = "Close Open Doors With No Door Controller Installed")]
            public bool CloseOpenDoors { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Duplicate Items")]
            public bool AllowDuplicates { get; set; }

            [JsonProperty(PropertyName = "Allow Players To Pickup Deployables")]
            public bool AllowPickup { get; set; }

            [JsonProperty(PropertyName = "Allow Players To Deploy A Cupboard")]
            public bool AllowBuildingPriviledges { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Players To Deploy Barricades")]
            public bool Barricades { get; set; } = true;

            [JsonProperty(PropertyName = "Allow PVP")]
            public bool AllowPVP { get; set; } = true;

            [JsonProperty(PropertyName = "Allow Friendly Fire (Teams)")]
            public bool AllowFriendlyFire { get; set; } = true;

            [JsonProperty(PropertyName = "Minimum Amount Of Items To Spawn (0 = Use Max Value)")]
            public int MinTreasure { get; set; }

            [JsonProperty(PropertyName = "Amount Of Items To Spawn")]
            public int MaxTreasure { get; set; } = 30;

            [JsonProperty(PropertyName = "Flame Turret Health")]
            public float FlameTurretHealth { get; set; } = 300f;

            [JsonProperty(PropertyName = "Block Plugins Which Prevent Item Durability Loss")]
            public bool EnforceDurability { get; set; }

            [JsonProperty(PropertyName = "Block Damage Outside Of The Dome To Players Inside")]
            public bool BlockOutsideDamageToPlayersInside { get; set; }

            [JsonProperty(PropertyName = "Block Damage Outside Of The Dome To Bases Inside")]
            public bool BlockOutsideDamageToBaseInside { get; set; }

            [JsonProperty(PropertyName = "Block Damage Inside From Npcs To Players Outside")]
            public bool BlockNpcDamageToPlayersOutside { get; set; }

            [JsonProperty(PropertyName = "Building Blocks Are Immune To Damage")]
            public bool BlocksImmune { get; set; }

            [JsonProperty(PropertyName = "Building Blocks Are Immune To Damage (Twig Only)")]
            public bool TwigImmune { get; set; }

            [JsonProperty(PropertyName = "Boxes Are Invulnerable")]
            public bool Invulnerable { get; set; }

            [JsonProperty(PropertyName = "Spawn Silently (No Notifcation, No Dome, No Map Marker)")]
            public bool Silent { get; set; }

            [JsonProperty(PropertyName = "Divide Loot Into All Containers")]
            public bool DivideLoot { get; set; } = true;

            [JsonProperty(PropertyName = "Drop Tool Cupboard Loot After Raid Is Completed")]
            public bool DropPrivilegeLoot { get; set; }

            [JsonProperty(PropertyName = "Drop Container Loot X Seconds After It Is Looted")]
            public float DropTimeAfterLooting { get; set; }

            [JsonProperty(PropertyName = "Drop Container Loot Applies Only To Boxes And Cupboards")]
            public bool DropOnlyBoxesAndPrivileges { get; set; } = true;

            [JsonProperty(PropertyName = "Create Dome Around Event Using Spheres (0 = disabled, recommended = 5)")]
            public int SphereAmount { get; set; } = 5;

            [JsonProperty(PropertyName = "Enable CopyPaste Stability Option (USE AT YOUR OWN RISK - POTENTIAL SERVER CRASH)")]
            public bool Stability { get; set; }

            [JsonProperty(PropertyName = "Empty All Containers Before Spawning Loot")]
            public bool EmptyAll { get; set; } = true;

            [JsonProperty(PropertyName = "Eject Corpses From Enemy Raids (Advanced Users Only)")]
            public bool EjectBackpacks { get; set; } = true;

            [JsonProperty(PropertyName = "Eject Corpses From PVE Instantly (Advanced Users Only)")]
            public bool EjectBackpacksPVE { get; set; }

            [JsonProperty(PropertyName = "Eject Enemies From Purchased PVE Raids")]
            public bool EjectPurchasedPVE { get; set; } = true;

            [JsonProperty(PropertyName = "Eject Enemies From Purchased PVP Raids")]
            public bool EjectPurchasedPVP { get; set; }

            [JsonProperty(PropertyName = "Eject Enemies From Locked PVE Raids")]
            public bool EjectLockedPVE { get; set; } = true;

            [JsonProperty(PropertyName = "Eject Enemies From Locked PVP Raids")]
            public bool EjectLockedPVP { get; set; }

            [JsonProperty(PropertyName = "Explosion Damage Modifier (0-999)")]
            public float ExplosionModifier { get; set; } = 100f;

            [JsonProperty(PropertyName = "Force All Boxes To Have Same Skin")]
            public bool SetSkins { get; set; } = true;

            [JsonProperty(PropertyName = "Ignore Containers That Spawn With Loot Already")]
            public bool IgnoreContainedLoot { get; set; }

            [JsonProperty(PropertyName = "Loot Amount Multiplier")]
            public float Multiplier { get; set; } = 1f;

            [JsonProperty(PropertyName = "Maximum Respawn Npc X Seconds After Death")]
            public float RespawnRateMax { get; set; }

            [JsonProperty(PropertyName = "Minimum Respawn Npc X Seconds After Death")]
            public float RespawnRateMin { get; set; }

            [JsonProperty(PropertyName = "No Item Input For Boxes And TC")]
            public bool NoItemInput { get; set; } = true;

            [JsonProperty(PropertyName = "Penalize Players On Death In PVE (ZLevels)")]
            public bool PenalizePVE { get; set; } = true;

            [JsonProperty(PropertyName = "Penalize Players On Death In PVP (ZLevels)")]
            public bool PenalizePVP { get; set; } = true;

            [JsonProperty(PropertyName = "Require Cupboard Access To Loot")]
            public bool RequiresCupboardAccess { get; set; }

            [JsonProperty(PropertyName = "Require Cupboard Access To Place Ladders")]
            public bool RequiresCupboardAccessLadders { get; set; }

            [JsonProperty(PropertyName = "Skip Treasure Loot And Use Loot In Base Only")]
            public bool SkipTreasureLoot { get; set; }

            [JsonProperty(PropertyName = "Force Time In Dome To (requires raidablebases.time)")]
            public int ForcedTime { get; set; } = -1;

            [JsonProperty(PropertyName = "Always Spawn Base Loot Table")]
            public bool AlwaysSpawn { get; set; }

            public static BuildingOptions Clone(BuildingOptions options)
            {
                return options.MemberwiseClone() as BuildingOptions;
            }

            public float ProtectionRadius(RaidableType type)
            {
                float radius = ProtectionRadii.Get(type);

                return radius < CELL_SIZE ? 50f : radius;
            }
        }

        public class RaidableBaseSettingsScheduled
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; }

            [JsonProperty(PropertyName = "Chance To Randomly Spawn PVP Bases (0 = Ignore Setting)")]
            public double Chance { get; set; }

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

            [JsonProperty(PropertyName = "Ignore Safe Checks")]
            public bool Ignore { get; set; }

            [JsonProperty(PropertyName = "Ignore Safe Checks In X Radius Only")]
            public float SafeRadius { get; set; }

            [JsonProperty(PropertyName = "Ignore Player Entities At Custom Spawn Locations")]
            public bool Skip { get; set; }

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

            [JsonProperty(PropertyName = "Time To Wait Between Spawns")]
            public float Time { get; set; } = 15f;
        }

        public class RaidableBaseSettingsMaintained
        {
            [JsonProperty(PropertyName = "Always Maintain Max Events")]
            public bool Enabled { get; set; }

            [JsonProperty(PropertyName = "Chance To Randomly Spawn PVP Bases (0 = Ignore Setting)")]
            public double Chance { get; set; }

            [JsonProperty(PropertyName = "Convert PVE To PVP")]
            public bool ConvertPVE { get; set; }

            [JsonProperty(PropertyName = "Convert PVP To PVE")]
            public bool ConvertPVP { get; set; }

            [JsonProperty(PropertyName = "Include PVE Bases")]
            public bool IncludePVE { get; set; } = true;

            [JsonProperty(PropertyName = "Include PVP Bases")]
            public bool IncludePVP { get; set; } = true;

            [JsonProperty(PropertyName = "Ignore Safe Checks")]
            public bool Ignore { get; set; }

            [JsonProperty(PropertyName = "Ignore Safe Checks In X Radius Only")]
            public float SafeRadius { get; set; }

            [JsonProperty(PropertyName = "Ignore Player Entities At Custom Spawn Locations")]
            public bool Skip { get; set; }

            [JsonProperty(PropertyName = "Minimum Required Players Online")]
            public int PlayerLimit { get; set; } = 1;

            [JsonProperty(PropertyName = "Max Maintained Events")]
            public int Max { get; set; } = 1;

            [JsonProperty(PropertyName = "Spawn Bases X Distance Apart")]
            public float Distance { get; set; } = 100f;

            [JsonProperty(PropertyName = "Spawns Database File (Optional)")]
            public string SpawnsFile { get; set; } = "none";

            [JsonProperty(PropertyName = "Time To Wait Between Spawns")]
            public float Time { get; set; } = 15f;
        }

        public class RaidableBaseSettingsBuyableCooldowns
        {
            [JsonProperty(PropertyName = "VIP Permission: raidablebases.vipcooldown")]
            public float VIP { get; set; } = 600f;

            [JsonProperty(PropertyName = "Admin Permission: raidablebases.allow")]
            public float Allow { get; set; }

            [JsonProperty(PropertyName = "Server Admins")]
            public float Admin { get; set; }

            [JsonProperty(PropertyName = "Normal Users")]
            public float Cooldown { get; set; } = 1200f;

            public float Get(ulong userid)
            {
                var cooldowns = new List<float>() { Cooldown };
                var player = BasePlayer.FindByID(userid);

                if (player.IsReallyValid())
                {
                    if (player.IsFlying)
                    {
                        return 0f;
                    }

                    if (player.IsAdmin || player.IsDeveloper)
                    {
                        cooldowns.Add(Admin);
                    }
                }

                if (userid.HasPermission("raidablebases.vipcooldown"))
                {
                    cooldowns.Add(VIP);
                }

                if (userid.HasPermission("raidablebases.allow"))
                {
                    cooldowns.Add(Allow);
                }

                return Mathf.Min(cooldowns.ToArray());
            }
        }

        public class RaidableBaseSettingsBuyableRefunds
        {
            [JsonProperty(PropertyName = "Refund Despawned Bases")]
            public bool Refund { get; set; }

            [JsonProperty(PropertyName = "Block Refund If Base Is Damaged")]
            public bool Damaged { get; set; } = true;

            [JsonProperty(PropertyName = "Refund Percentage")]
            public double Percentage { get; set; } = 100.0;

            [JsonProperty(PropertyName = "Refund Resets Cooldown Timer")]
            public bool Reset { get; set; }
        }

        public class RaidableBaseSettingsBuyable
        {
            [JsonProperty(PropertyName = "Cooldowns (0 = No Cooldown)")]
            public RaidableBaseSettingsBuyableCooldowns Cooldowns { get; set; } = new RaidableBaseSettingsBuyableCooldowns();

            [JsonProperty(PropertyName = "Refunds")]
            public RaidableBaseSettingsBuyableRefunds Refunds { get; set; } = new RaidableBaseSettingsBuyableRefunds();

            [JsonProperty(PropertyName = "Allow Players To Buy PVP Raids")]
            public bool BuyPVP { get; set; }

            [JsonProperty(PropertyName = "Allow Ally With Lockouts To Enter")]
            public bool AllowAlly { get; set; } = true;

            [JsonProperty(PropertyName = "Convert PVE To PVP")]
            public bool ConvertPVE { get; set; }

            [JsonProperty(PropertyName = "Convert PVP To PVE")]
            public bool ConvertPVP { get; set; }

            [JsonProperty(PropertyName = "Distance To Spawn Bought Raids From Player")]
            public float DistanceToSpawnFrom { get; set; } = 500f;

            [JsonProperty(PropertyName = "Ignore Safe Checks")]
            public bool Ignore { get; set; }

            [JsonProperty(PropertyName = "Ignore Safe Checks In X Radius Only")]
            public float SafeRadius { get; set; }

            [JsonProperty(PropertyName = "Ignore Player Entities At Custom Spawn Locations")]
            public bool Skip { get; set; }

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

            [JsonProperty(PropertyName = "Use Permission (raidablebases.buyraid)")]
            public bool UsePermission { get; set; }
        }

        public class RaidableBaseSettingsManual
        {
            [JsonProperty(PropertyName = "Convert PVE To PVP")]
            public bool ConvertPVE { get; set; }

            [JsonProperty(PropertyName = "Convert PVP To PVE")]
            public bool ConvertPVP { get; set; }

            [JsonProperty(PropertyName = "Max Manual Events")]
            public int Max { get; set; } = 1;

            [JsonProperty(PropertyName = "Spawns Database File (Optional)")]
            public string SpawnsFile { get; set; } = "none";
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

        public class RaidableBaseCostOptions
        {
            [JsonProperty(PropertyName = "Require Custom Costs")]
            public bool IncludeCustom { get; set; } = true;

            [JsonProperty(PropertyName = "Require Economics Costs")]
            public bool IncludeEconomics { get; set; } = true;

            [JsonProperty(PropertyName = "Require Server Rewards Costs")]
            public bool IncludeServerRewards { get; set; } = true;
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
            public bool Any => Easy > 0 || Medium > 0 || Hard > 0 || Expert > 0 || Nightmare > 0;
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

        public class RaidableBaseCustomCostOptions
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; }

            [JsonProperty(PropertyName = "Item Shortname")]
            public string Shortname { get; set; } = "scrap";

            [JsonProperty(PropertyName = "Item Name")]
            public string Name { get; set; } = null;

            [JsonProperty(PropertyName = "Amount")]
            public int Amount { get; set; }

            [JsonProperty(PropertyName = "Skin")]
            public ulong Skin { get; set; }

            [JsonIgnore]
            public ItemDefinition Definition { get; set; }

            public bool IsReallyValid()
            {
                if (Enabled && !string.IsNullOrEmpty(Shortname) && Amount > 0)
                {
                    if (Definition == null)
                    {
                        Definition = ItemManager.FindItemDefinition(Shortname);
                    }

                    return Definition != null;
                }

                return false;
            }

            public RaidableBaseCustomCostOptions(int amount)
            {
                Amount = amount;
            }
        }

        public class RaidableBaseSettingsRankedLadderAssignOptions
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

            [JsonProperty(PropertyName = "Assign To Owner Of Raid Only")]
            public bool Owner { get; set; }
        }

        public class RankedLadderSettings
        {
            [JsonProperty(PropertyName = "Award Top X Players On Wipe")]
            public int Amount { get; set; } = 3;

            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty(PropertyName = "Show Top X Ladder")]
            public int Top { get; set; } = 10;

            [JsonProperty(PropertyName = "Assign Rank After X Completions")]
            public RaidableBaseSettingsRankedLadderAssignOptions Assign { get; set; } = new RaidableBaseSettingsRankedLadderAssignOptions();

            [JsonProperty(PropertyName = "Difficulty Points")]
            public RaidableBaseSettingsRankedLadderPointOptions Points { get; set; } = new RaidableBaseSettingsRankedLadderPointOptions();
        }

        public class RaidableBaseSettingsRankedLadderPointOptions
        {
            [JsonProperty(PropertyName = "Easy")]
            public int Easy { get; set; } = 1;

            [JsonProperty(PropertyName = "Medium")]
            public int Medium { get; set; } = 2;

            [JsonProperty(PropertyName = "Hard")]
            public int Hard { get; set; } = 3;

            [JsonProperty(PropertyName = "Expert")]
            public int Expert { get; set; } = 4;

            [JsonProperty(PropertyName = "Nightmare")]
            public int Nightmare { get; set; } = 5;

            [JsonProperty(PropertyName = "Assign To Owner Of Raid Only")]
            public bool Owner { get; set; }
        }

        public class RewardSettings
        {
            [JsonProperty(PropertyName = "Custom Currency")]
            public RaidableBaseCustomCostOptions Custom { get; set; } = new RaidableBaseCustomCostOptions(0);

            [JsonProperty(PropertyName = "Economics Money")]
            public double Money { get; set; }

            [JsonProperty(PropertyName = "ServerRewards Points")]
            public int Points { get; set; }

            [JsonProperty(PropertyName = "SkillTree XP")]
            public double XP { get; set; }

            [JsonProperty(PropertyName = "Do Not Reward Buyable Events")]
            public bool NoBuyableRewards { get; set; }
        }

        public class SkinSettingsDefault
        {
            [JsonProperty(PropertyName = "Include Workshop Skins")]
            public bool RandomWorkshopSkins { get; set; } = true;

            [JsonProperty(PropertyName = "Preset Skin")]
            public ulong PresetSkin { get; set; }

            [JsonProperty(PropertyName = "Use Random Skin")]
            public bool RandomSkins { get; set; } = true;
        }

        public class SkinSettingsLoot
        {
            [JsonProperty(PropertyName = "Include Workshop Skins")]
            public bool RandomWorkshopSkins { get; set; } = true;

            [JsonProperty(PropertyName = "Use Random Skin")]
            public bool RandomSkins { get; set; } = true;

            [JsonProperty(PropertyName = "Use Imported Workshop Skins File")]
            public bool Imported { get; set; }
        }

        public class SkinSettingsDeployables
        {
            [JsonProperty(PropertyName = "Partial Names", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Names { get; set; } = new List<string>
            {
                "door", "barricade", "chair", "fridge", "furnace", "locker", "reactivetarget", "rug", "sleepingbag", "table", "vendingmachine", "waterpurifier", "skullspikes", "skulltrophy", "summer_dlc", "sled"
            };

            [JsonProperty(PropertyName = "Include Workshop Skins")]
            public bool RandomWorkshopSkins { get; set; } = true;

            [JsonProperty(PropertyName = "Use Random Skin")]
            public bool RandomSkins { get; set; } = true;

            [JsonProperty(PropertyName = "Skin Everything")]
            public bool Everything { get; set; } = true;
        }

        public class SkinSettings
        {
            [JsonProperty(PropertyName = "Boxes")]
            public SkinSettingsDefault Boxes { get; set; } = new SkinSettingsDefault();

            [JsonProperty(PropertyName = "Loot Items")]
            public SkinSettingsLoot Loot { get; set; } = new SkinSettingsLoot();

            [JsonProperty(PropertyName = "Deployables")]
            public SkinSettingsDeployables Deployables { get; set; } = new SkinSettingsDeployables();

            [JsonProperty(PropertyName = "Randomize Npc Item Skins")]
            public bool Npcs { get; set; } = true;

            [JsonProperty(PropertyName = "Use Identical Skins For All Npcs")]
            public bool UniqueNpcs { get; set; } = true;

            [JsonProperty(PropertyName = "Ignore If Skinned Already")]
            public bool IgnoreSkinned { get; set; } = true;
        }

        public class SkinSettingsImportedWorkshop
        {
            [JsonProperty(PropertyName = "Imported Workshop Skins", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, List<ulong>> SkinList { get; set; } = DefaultImportedSkins;
        }

        public class LootItem : IEquatable<LootItem>
        {
            [JsonProperty(PropertyName = "shortname")]
            public string shortname { get; set; }

            [JsonProperty(PropertyName = "name")]
            public string name { get; set; } = null;

            [JsonProperty(PropertyName = "amount")]
            public int amount { get; set; }

            [JsonProperty(PropertyName = "skin")]
            public ulong skin { get; set; }

            [JsonProperty(PropertyName = "amountMin")]
            public int amountMin { get; set; }

            [JsonProperty(PropertyName = "probability")]
            public float probability { get; set; } = 1f;

            [JsonProperty(PropertyName = "stacksize")]
            public int stacksize { get; set; } = -1;

            [JsonIgnore]
            private ItemDefinition _def { get; set; }

            [JsonIgnore]
            public ItemDefinition definition
            {
                get
                {
                    if (_def == null)
                    {
                        string _shortname = shortname.EndsWith(".bp") ? shortname.Replace(".bp", string.Empty) : shortname;

                        if (shortname.Contains("_") && ItemManager.FindItemDefinition(_shortname) == null)
                        {
                            _shortname = _shortname.Substring(_shortname.IndexOf("_") + 1);
                        }

                        _def = ItemManager.FindItemDefinition(_shortname);
                    }

                    return _def;
                }
            }

            [JsonIgnore]
            public bool isBlueprint { get; set; }

            [JsonIgnore]
            public bool isModified { get; set; }

            public LootItem() { }

            public LootItem(string shortname, int amountMin = 1, int amount = 1, ulong skin = 0, bool isBlueprint = false, float probability = 1.0f, int stacksize = -1, string name = null, bool isModified = false)
            {
                this.shortname = shortname;
                this.amountMin = amountMin;
                this.amount = amount;
                this.skin = skin;
                this.isBlueprint = isBlueprint;
                this.probability = probability;
                this.stacksize = stacksize;
                this.name = name;
                this.isModified = isModified;
            }

            public LootItem Clone()
            {
                return new LootItem(shortname, amountMin, amount, skin, isBlueprint, probability, stacksize, name, isModified);
            }

            public bool Equals(LootItem other)
            {
                return shortname == other.shortname && amount == other.amount && skin == other.skin && amountMin == other.amountMin;
            }
        }

        public class TreasureSettings
        {
            [JsonProperty(PropertyName = "Resources Not Moved To Cupboards", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> ExcludeFromCupboard { get; set; } = new List<string>
            {
                "skull.human", "battery.small", "bone.fragments", "can.beans.empty", "can.tuna.empty", "water.salt", "water", "skull.wolf"
            };

            [JsonProperty(PropertyName = "Use Day Of Week Loot")]
            public bool UseDOWL { get; set; }

            [JsonProperty(PropertyName = "Do Not Duplicate Base Loot")]
            public bool UniqueBaseLoot { get; set; }

            [JsonProperty(PropertyName = "Do Not Duplicate Difficulty Loot")]
            public bool UniqueDifficultyLoot { get; set; }

            [JsonProperty(PropertyName = "Do Not Duplicate Default Loot")]
            public bool UniqueDefaultLoot { get; set; }

            [JsonProperty(PropertyName = "Use Stack Size Limit For Spawning Items")]
            public bool UseStackSizeLimit { get; set; }
        }

        public class UIRaidDetailsSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = false;

            [JsonProperty(PropertyName = "Anchor Min")]
            public string AnchorMin { get; set; } = "0.748 0.228";

            [JsonProperty(PropertyName = "Anchor Max")]
            public string AnchorMax { get; set; } = "0.986 0.248";

            [JsonProperty(PropertyName = "Details Font Size")]
            public int FontSize { get; set; } = 10;

            [JsonProperty(PropertyName = "Panel Alpha")]
            public float Alpha { get; set; } = 0.98f;

            [JsonProperty(PropertyName = "Panel Color")]
            public string PanelColor { get; set; } = "#000000";

            [JsonProperty(PropertyName = "Label Color")]
            public string LabelColor { get; set; } = "#EAEAEA";

            [JsonProperty(PropertyName = "Negative Color")]
            public string NegativeColor { get; set; } = "#FF0000";

            [JsonProperty(PropertyName = "Positive Color")]
            public string PositiveColor { get; set; } = "#008000";
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

            [JsonProperty(PropertyName = "Expert Anchor Min")]
            public string ExpertMin { get; set; } = "0.838 0.325";

            [JsonProperty(PropertyName = "Expert Anchor Max")]
            public string ExpertMax { get; set; } = "0.883 0.365";

            [JsonProperty(PropertyName = "Nightmare Anchor Min")]
            public string NightmareMin { get; set; } = "0.893 0.325";

            [JsonProperty(PropertyName = "Nightmare Anchor Max")]
            public string NightmareMax { get; set; } = "0.936 0.365";

            [JsonProperty(PropertyName = "Panel Alpha")]
            public float Alpha { get; set; } = 1f;

            public string Min(RaidableMode mode)
            {
                switch (mode)
                {
                    case RaidableMode.Easy: return EasyMin;
                    case RaidableMode.Medium: return MediumMin;
                    case RaidableMode.Hard: return HardMin;
                    case RaidableMode.Expert: return ExpertMin;
                    default: return NightmareMin;
                }
            }

            public string Max(RaidableMode mode)
            {
                switch (mode)
                {
                    case RaidableMode.Easy: return EasyMax;
                    case RaidableMode.Medium: return MediumMax;
                    case RaidableMode.Hard: return HardMax;
                    case RaidableMode.Expert: return ExpertMax;
                    default: return NightmareMax;
                }
            }
        }

        public class UIBuyableSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty(PropertyName = "Cursor Enabled")]
            public bool CursorEnabled { get; set; }

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

            [JsonProperty(PropertyName = "Font Size")]
            public int FontSize { get; set; } = 14;

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

            public string Get(RaidableMode mode)
            {
                switch (mode)
                {
                    case RaidableMode.Easy: return EasyColor;
                    case RaidableMode.Medium: return MediumColor;
                    case RaidableMode.Hard: return HardColor;
                    case RaidableMode.Expert: return ExpertColor;
                    default: return NightmareColor;
                }
            }
        }

        public class UIAdvancedAlertSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty(PropertyName = "Anchor Min")]
            public string AnchorMin { get; set; } = "0.35 0.85";

            [JsonProperty(PropertyName = "Anchor Max")]
            public string AnchorMax { get; set; } = "0.65 0.95";

            [JsonProperty(PropertyName = "Time Shown")]
            public float Time { get; set; } = 5f;
        }

        public class UISettings
        {
            [JsonProperty(PropertyName = "Advanced Alerts UI")]
            public UIAdvancedAlertSettings AA { get; set; } = new UIAdvancedAlertSettings();

            [JsonProperty(PropertyName = "Buyable UI")]
            public UIBuyableSettings Buyable { get; set; } = new UIBuyableSettings();

            [JsonProperty(PropertyName = "Details")]
            public UIRaidDetailsSettings Details { get; set; } = new UIRaidDetailsSettings();

            [JsonProperty(PropertyName = "Lockouts")]
            public UILockoutSettings Lockout { get; set; } = new UILockoutSettings();

            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty(PropertyName = "Status Anchor Min")]
            public string AnchorMin { get; set; } = "0.748 0.249";

            [JsonProperty(PropertyName = "Status Anchor Max")]
            public string AnchorMax { get; set; } = "0.986 0.279";

            [JsonProperty(PropertyName = "Status Font Size")]
            public int FontSize { get; set; } = 12;

            [JsonProperty(PropertyName = "Panel Alpha")]
            public float Alpha { get; set; } = 0.98f;

            [JsonProperty(PropertyName = "Panel Color")]
            public string PanelColor { get; set; } = "#000000";

            [JsonProperty(PropertyName = "PVP Color")]
            public string ColorPVP { get; set; } = "#FF0000";

            [JsonProperty(PropertyName = "PVE Color")]
            public string ColorPVE { get; set; } = "#008000";

            [JsonProperty(PropertyName = "Show Loot Left")]
            public bool Containers { get; set; } = true;

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

            [JsonProperty(PropertyName = "SamSite Requires Power Source")]
            public bool SamSiteRequiresPower { get; set; }

            [JsonProperty(PropertyName = "Test Generator Power")]
            public float TestGeneratorPower { get; set; } = 100f;
        }

        public class Configuration
        {
            [JsonProperty(PropertyName = "Settings")]
            public PluginSettings Settings = new PluginSettings();

            [JsonProperty(PropertyName = "Event Messages")]
            public EventMessageSettings EventMessages = new EventMessageSettings();

            [JsonProperty(PropertyName = "GUIAnnouncements")]
            public GUIAnnouncementSettings GUIAnnouncement = new GUIAnnouncementSettings();

            [JsonProperty(PropertyName = "Ranked Ladder")]
            public RankedLadderSettings RankedLadder = new RankedLadderSettings();

            [JsonProperty(PropertyName = "Skins")]
            public SkinSettings Skins = new SkinSettings();

            [JsonProperty(PropertyName = "Treasure")]
            public TreasureSettings Treasure = new TreasureSettings();

            [JsonProperty(PropertyName = "UI")]
            public UISettings UI = new UISettings();

            [JsonProperty(PropertyName = "Weapons")]
            public WeaponSettings Weapons = new WeaponSettings();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
                CheckConfig();
                SaveConfig();
            }
            catch (Exception ex)
            {
                Puts("{0}", ex);
                LoadDefaultConfig();
            }
        }

        private void CheckConfig()
        {
            if (config.GUIAnnouncement.TintColor.ToLower() == "black")
            {
                config.GUIAnnouncement.TintColor = "grey";
            }
        }

        public static List<LootItem> TreasureLoot
        {
            get
            {
                List<LootItem> lootList;
                if (!Instance.Buildings.DifficultyLootLists.TryGetValue(LootType.Default, out lootList))
                {
                    Instance.Buildings.DifficultyLootLists[LootType.Default] = lootList = new List<LootItem>();
                }

                return lootList.ToList();
            }
        }

        public static List<LootItem> WeekdayLoot
        {
            get
            {
                List<LootItem> lootList;
                if (!config.Treasure.UseDOWL || !Instance.Buildings.WeekdayLootLists.TryGetValue(DateTime.Now.DayOfWeek, out lootList))
                {
                    Instance.Buildings.WeekdayLootLists[DateTime.Now.DayOfWeek] = lootList = new List<LootItem>();
                }

                return lootList.ToList();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            Puts("Loaded default configuration file");
        }

        #endregion

        #region UI

        public class UI // Credits: Absolut & k1lly0u
        {
            public static CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = false, string parent = "Overlay")
            {
                return new CuiElementContainer
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
                }, panel);
            }

            public static void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text =
                    {
                        Color = color,
                        FontSize = size,
                        Align = align,
                        FadeIn = 0f,
                        Text = text
                    },
                    RectTransform =
                    {
                        AnchorMin = aMin,
                        AnchorMax = aMax,
                        //OffsetMin = "0.5 0.5",
                        //OffsetMax = "0.5 0.5"
                    }
                }, panel);
            }

            private static string GetContrast(string hexColor, bool useConstrast)
            {
                if (!useConstrast)
                {
                    return Color(config.UI.Buyable.TextColor);
                }

                hexColor = hexColor.TrimStart('#');
                int r = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int g = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int b = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                var color = ((r * 299) + (g * 587) + (b * 114)) / 1000 >= 128 ? "0 0 0 1" : "1 1 1 1";
                return color;

            }

            public static string Color(string hexColor, float a = 1.0f)
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
                if (player.IsReallyConnected() && Players.Contains(player))
                {
                    CuiHelper.DestroyUi(player, StatusPanelName);
                    CuiHelper.DestroyUi(player, DetailsPanelName);
                    Players.Remove(player);
                    DestroyStatusUpdate(player);
                }
            }

            public static void DestroyLockoutUI(BasePlayer player)
            {
                if (player.IsReallyConnected() && Lockouts.Contains(player))
                {
                    Instance.RaidableModes.ForEach(mode => CuiHelper.DestroyUi(player, $"RB_UI_{mode}"));
                    Lockouts.Remove(player);
                    DestroyLockoutUpdate(player);
                }
            }

            public static void DestroyAllLockoutUI()
            {
                foreach (var player in Lockouts)
                {
                    if (player.IsReallyConnected() && Lockouts.Contains(player))
                    {
                        Instance.RaidableModes.ForEach(mode => CuiHelper.DestroyUi(player, $"RB_UI_{mode}"));
                        DestroyLockoutUpdate(player);
                    }
                }

                Lockouts.Clear();
            }

            public static void DestroyAllBuyableUI()
            {
                Buyables.RemoveAll(x => !x.IsReallyConnected());

                foreach (var player in Buyables)
                {
                    CuiHelper.DestroyUi(player, BuyablePanelName);
                }

                Buyables.Clear();
            }

            public static void DestroyAllStatusUI()
            {
                Players.RemoveAll(x => !x.IsReallyConnected());

                foreach (var player in Players)
                {
                    CuiHelper.DestroyUi(player, StatusPanelName);
                    CuiHelper.DestroyUi(player, DetailsPanelName);
                }

                Players.Clear();
            }

            public static void CreateBuyableUI(BasePlayer player)
            {
                if (Buyables.Contains(player))
                {
                    CuiHelper.DestroyUi(player, BuyablePanelName);
                    Buyables.Remove(player);
                }

                if (!config.UI.Buyable.Enabled)
                {
                    return;
                }

                var element = CreateElementContainer(BuyablePanelName, Color(config.UI.Buyable.PanelColor, config.UI.Buyable.PanelAlpha), config.UI.Buyable.Min, config.UI.Buyable.Max, config.UI.Buyable.CursorEnabled, "Hud");
                var created = false;

                CreateLabel(ref element, BuyablePanelName, "1 1 1 1", mx("Buy Raids", player.UserIDString), 14, "0.02 0.865", "0.447 0.959");
                CreateButton(ref element, BuyablePanelName, Color(config.UI.Buyable.CloseColor, config.UI.Buyable.ButtonAlpha), "ⓧ", 14, "0.833 0.835", "1 0.982", "ui_buyraid closeui", TextAnchor.MiddleCenter, config.UI.Buyable.TextColor);

                double min = 0.665;
                double max = 0.812;

                foreach (var mode in Instance.RaidableModes)
                {
                    if (!IsEnabled(mode))
                    {
                        continue;
                    }

                    var lapi = mx($"Mode{mode}", player.UserIDString);

                    if (GetRaidableMode(lapi) != RaidableMode.Random)
                    {
                        lapi = lapi.SentenceCase();
                    }

                    var text = GetButtonText(mode, lapi, player.UserIDString);
                    var command = text == null ? "ui_buyraid closeui" : $"ui_buyraid {(int)mode}";
                    var labelColor = GetContrast(text == null ? "#808080" : config.UI.Buyable.Difficulty ? config.Settings.Management.Colors2.Get(mode) : config.UI.Buyable.Get(mode), config.UI.Buyable.Contrast);
                    var color = Color(text == null ? "#808080" : config.UI.Buyable.Difficulty ? config.Settings.Management.Colors2.Get(mode) : config.UI.Buyable.Get(mode), config.UI.Buyable.ButtonAlpha);

                    CreateButton(ref element, BuyablePanelName, color, text ?? lapi, config.UI.Buyable.FontSize, $"0 {min}", $"1 {max}", command, TextAnchor.MiddleCenter, labelColor);
                    created = true;
                    min -= 0.165;
                    max -= 0.165;
                }

                if (created)
                {
                    CuiHelper.AddUi(player, element);
                    Buyables.Add(player);
                }
                else
                {
                    if (!config.Settings.Custom.Exists(kvp => kvp.Value.All(o => o.IsReallyValid())) && !config.Settings.ServerRewards.Any && !config.Settings.Economics.Any)
                    {
                        Message(player, "NoBuyableEventsCostsConfigured");
                    }

                    if (!config.Settings.Buyable.BuyPVP && Instance.Buildings.Profiles.ToList().All(profile => !profile.Value.Options.AllowPVP))
                    {
                        Message(player, "NoBuyableEventsPVP");
                    }

                    if (!config.Settings.Management.Amounts.Any())
                    {
                        Message(player, "NoBuyableEventsEnabled");
                    }

                    if (!Instance.RaidableModes.Exists(mode => CanSpawnDifficultyToday(mode)))
                    {
                        Message(player, "NoBuyableEventsToday");
                    }
                }
            }

            private static bool IsEnabled(RaidableMode mode)
            {
                if (!CanSpawnDifficultyToday(mode))
                {
                    return false;
                }

                List<RaidableBaseCustomCostOptions> options;
                if (config.Settings.Custom.TryGetValue(mode, out options) && options.All(o => o.IsReallyValid()))
                {
                    return true;
                }

                if ((mode == RaidableMode.Easy ? config.Settings.Economics.Easy : mode == RaidableMode.Medium ? config.Settings.Economics.Medium : mode == RaidableMode.Hard ? config.Settings.Economics.Hard : mode == RaidableMode.Expert ? config.Settings.Economics.Expert : mode == RaidableMode.Nightmare ? config.Settings.Economics.Nightmare : 0) > 0)
                {
                    return true;
                }

                return (mode == RaidableMode.Easy ? config.Settings.ServerRewards.Easy : mode == RaidableMode.Medium ? config.Settings.ServerRewards.Medium : mode == RaidableMode.Hard ? config.Settings.ServerRewards.Hard : mode == RaidableMode.Expert ? config.Settings.ServerRewards.Expert : mode == RaidableMode.Nightmare ? config.Settings.ServerRewards.Nightmare : 0) > 0;
            }

            private static string GetButtonText(RaidableMode mode, string langMode, string userid)
            {
                List<RaidableBaseCustomCostOptions> costs;
                if (config.Settings.Custom.TryGetValue(mode, out costs) && costs.All(o => o.IsReallyValid()))
                {
                    return costs.Count == 1 ? string.Format("{0} ({1} {2})", langMode, costs[0].Amount, string.IsNullOrEmpty(costs[0].Name) ? costs[0].Shortname : costs[0].Name) : langMode;
                }

                return GetPurchasePrice(mode, langMode, userid);
            }

            private static string GetPurchasePrice(RaidableMode mode, string langMode, string userid)
            {
                switch (mode)
                {
                    case RaidableMode.Easy:
                        {
                            if (config.Settings.ServerRewards.Easy > 0)
                            {
                                return string.Format("{0} ({1} {2})", langMode, config.Settings.ServerRewards.Easy, mx("RP", userid));
                            }
                            else if (config.Settings.Economics.Easy > 0)
                            {
                                return string.Format("{0} (${1})", langMode, config.Settings.Economics.Easy);
                            }
                            return GetCustomName(langMode, RaidableMode.Easy);
                        }
                    case RaidableMode.Medium:
                        {
                            if (config.Settings.ServerRewards.Medium > 0)
                            {
                                return string.Format("{0} ({1} {2})", langMode, config.Settings.ServerRewards.Medium, mx("RP", userid));
                            }
                            else if (config.Settings.Economics.Medium > 0)
                            {
                                return string.Format("{0} (${1})", langMode, config.Settings.Economics.Medium);
                            }
                            return GetCustomName(langMode, RaidableMode.Medium);
                        }
                    case RaidableMode.Hard:
                        {
                            if (config.Settings.ServerRewards.Hard > 0)
                            {
                                return string.Format("{0} ({1} {2})", langMode, config.Settings.ServerRewards.Hard, mx("RP", userid));
                            }
                            else if (config.Settings.Economics.Hard > 0)
                            {
                                return string.Format("{0} (${1})", langMode, config.Settings.Economics.Hard);
                            }
                            return GetCustomName(langMode, RaidableMode.Hard);
                        }
                    case RaidableMode.Expert:
                        {
                            if (config.Settings.ServerRewards.Expert > 0)
                            {
                                return string.Format("{0} ({1} {2})", langMode, config.Settings.ServerRewards.Expert, mx("RP", userid));
                            }
                            else if (config.Settings.Economics.Expert > 0)
                            {
                                return string.Format("{0} (${1})", langMode, config.Settings.Economics.Expert);
                            }
                            return GetCustomName(langMode, RaidableMode.Expert);
                        }
                    case RaidableMode.Nightmare:
                        {
                            if (config.Settings.ServerRewards.Nightmare > 0)
                            {
                                return string.Format("{0} ({1} {2})", langMode, config.Settings.ServerRewards.Nightmare, mx("RP", userid));
                            }
                            else if (config.Settings.Economics.Nightmare > 0)
                            {
                                return string.Format("{0} (${1})", langMode, config.Settings.Economics.Nightmare);
                            }
                            return GetCustomName(langMode, RaidableMode.Nightmare);
                        }
                }

                return null;
            }

            private static string GetCustomName(string langMode, RaidableMode mode)
            {
                List<RaidableBaseCustomCostOptions> options;
                if (config.Settings.Custom.TryGetValue(mode, out options) && options.All(o => o.IsReallyValid()))
                {
                    return string.Format("{0} ({1})", langMode, options[0].Definition.displayName.english);
                }
                return null;
            }

            private static void CreateStatus(BasePlayer player, RaidableBase raid, string panelName, string text, string color, string panelColor, string aMin, string aMax, int fontSize = 0)
            {
                var element = CreateElementContainer(panelName, panelColor, aMin, aMax, false, "Hud");

                CreateLabel(ref element, panelName, Color(color), text, fontSize == 0 ? config.UI.FontSize : fontSize, "0 0", "1 1");
                Players.Remove(player);
                CuiHelper.DestroyUi(player, panelName);
                CuiHelper.AddUi(player, element);
                Players.Add(player);
            }

            private static void CreateLockout(BasePlayer player, string panelName, string text, string color, string panelColor, string aMin, string aMax)
            {
                var element = CreateElementContainer(panelName, panelColor, aMin, aMax, false, "Hud");

                CreateLabel(ref element, panelName, Color(color), text, config.UI.FontSize, "0 0", "1 1");
                CuiHelper.AddUi(player, element);
            }

            private static void ShowStatus(BasePlayer player)
            {
                var raid = RaidableBase.Get(player.transform.position);

                if (raid == null)
                {
                    return;
                }

                string zone = raid.AllowPVP ? mx("PVP ZONE", player.UserIDString) : mx("PVE ZONE", player.UserIDString);
                float seconds = raid.despawnTime - Time.time;
                string despawnText = config.Settings.Management.DespawnMinutesInactive > 0 && seconds > 0 ? Math.Floor(TimeSpan.FromSeconds(seconds).TotalMinutes).ToString() : null;
                int lootAmount = raid._containers.Where(x => !x.IsKilled() && !raid.IsProtectedWeapon(x)).Sum(x => x.inventory.itemList.Count);
                string text;

                if (config.UI.Containers && config.UI.Time && !string.IsNullOrEmpty(despawnText))
                {
                    text = mx("UI Format", player.UserIDString, zone, lootAmount, despawnText);
                }
                else if (config.UI.Containers)
                {
                    text = mx("UI FormatContainers", player.UserIDString, zone, lootAmount);
                }
                else if (config.UI.Time && !string.IsNullOrEmpty(despawnText))
                {
                    text = mx("UI FormatMinutes", player.UserIDString, zone, despawnText);
                }
                else text = zone;

                CreateStatus(player, raid, StatusPanelName, text, raid.AllowPVP ? config.UI.ColorPVP : config.UI.ColorPVE, Color(config.UI.PanelColor, config.UI.Alpha), config.UI.AnchorMin, config.UI.AnchorMax);
                ShowDetails(raid, player);
            }

            private static void ShowDetails(RaidableBase raid, BasePlayer player)
            {
                if (!config.UI.Details.Enabled)
                {
                    return;
                }

                _sb.Clear();

                if (config.Settings.Management.UseOwners)
                {
                    string ownerColor = config.UI.Details.NegativeColor;
                    string ownerLabel = mx("None", player.UserIDString);

                    if (raid.owner.IsReallyValid() && raid.ownerId.IsSteamId())
                    {
                        if (raid.ownerId == player.userID)
                        {
                            ownerColor = config.UI.Details.PositiveColor;
                            ownerLabel = mx("You", player.UserIDString);
                        }
                        else if (raid.IsAlly(raid.ownerId, player.userID))
                        {
                            ownerColor = config.UI.Details.PositiveColor;
                            ownerLabel = mx("Ally", player.UserIDString);
                        }
                        else
                        {
                            ownerLabel = mx("Enemy", player.UserIDString);
                        }
                    }

                    _sb.Append(mx("Owner:", player.UserIDString, ownerColor, ownerLabel));
                }


                if (config.Settings.Management.LockTime > 0f)
                {
                    string statusColor = config.UI.Details.PositiveColor;
                    string status = mx("Active", player.UserIDString);
                    string inactiveTimeLeft = string.Empty;

                    float time;
                    if (raid.lastActive.TryGetValue(player.userID.ToString(), out time))
                    {
                        float secondsLeft = (config.Settings.Management.LockTime * 60f) - (Time.realtimeSinceStartup - time);
                        if (secondsLeft > 0f)
                        {
                            inactiveTimeLeft = mx("InactiveTimeLeft", player.UserIDString, Math.Floor(TimeSpan.FromSeconds(secondsLeft).TotalMinutes).ToString());
                        }
                    }

                    if (string.IsNullOrEmpty(inactiveTimeLeft))
                    {
                        statusColor = config.UI.Details.NegativeColor;
                        status = mx("Inactive", player.UserIDString);
                    }

                    _sb.Append(mx("Status:", player.UserIDString, statusColor, status, inactiveTimeLeft));
                }

                if (_sb.Length != 0)
                {
                    CreateStatus(player, raid, DetailsPanelName, _sb.ToString(), config.UI.Details.LabelColor, Color(config.UI.Details.PanelColor, config.UI.Details.Alpha), config.UI.Details.AnchorMin, config.UI.Details.AnchorMax, config.UI.Details.FontSize);
                    _sb.Clear();
                }
            }

            private static void ShowLockouts(BasePlayer player)
            {
                Lockout lo;
                if (!data.Lockouts.TryGetValue(player.UserIDString, out lo) || !lo.Any())
                {
                    return;
                }

                foreach (var mode in Instance.RaidableModes)
                {
                    var amin = config.UI.Lockout.Min(mode);
                    var amax = config.UI.Lockout.Max(mode);
                    var time = RaidableBase.GetLockoutTime(mode, lo, player.UserIDString);
                    var key = Math.Floor(TimeSpan.FromSeconds(Math.Max(0, time)).TotalMinutes).ToString();
                    var text = mx("UIFormatLockoutMinutes", player.UserIDString, key);
                    var color2H = config.Settings.Management.Colors2.Get(mode);
                    var color2 = Color(color2H, config.UI.Lockout.Alpha);
                    var color1 = RGBToHex(GetContrast(color2H, true));

                    CreateLockout(player, $"RB_UI_{mode}", text, color1, color2, amin, amax);
                }

                Lockouts.Add(player);
            }

            private static string RGBToHex(string hex)
            {
                var split = hex.Split(' ');

                if (split.Length != 4 || !split.All(x => x.IsNumeric()))
                {
                    return "FFFFFFFF";
                }

                float r = float.Parse(split[0]), g = float.Parse(split[1]), b = float.Parse(split[2]), a = float.Parse(split[3]);

                return ColorUtility.ToHtmlStringRGBA(new Color(r, g, b, a));
            }

            public static void UpdateStatusUI(RaidableBase raid)
            {
                foreach (var x in raid.raiders) UpdateStatusUI(x.Value.player);
            }

            public static void UpdateStatusUI(BasePlayer player)
            {
                Players.RemoveAll(x => !x.IsReallyConnected());

                if (!player.IsReallyConnected())
                {
                    return;
                }

                DestroyStatusUI(player);

                if (config == null || !config.UI.Enabled)
                {
                    return;
                }

                var uii = GetSettings(player.UserIDString);

                if (!uii.Enabled || !uii.Status)
                {
                    return;
                }

                ShowStatus(player);
                SetStatusUpdate(player);
            }

            private static void SetStatusUpdate(BasePlayer player)
            {
                var raid = RaidableBase.Get(player.transform.position);

                if (raid == null || raid.IsDespawning)
                {
                    return;
                }

                Timers timers;
                if (!InvokeTimers.TryGetValue(player.userID, out timers))
                {
                    InvokeTimers[player.userID] = timers = new Timers();
                }

                if (timers.Status == null || timers.Status.Destroyed)
                {
                    timers.Status = Instance.timer.Once(60f, () => UpdateStatusUI(player));
                }
                else timers.Status.Reset();
            }

            public static void DestroyStatusUpdate(BasePlayer player)
            {
                Timers timers;
                if (!InvokeTimers.TryGetValue(player.userID, out timers))
                {
                    return;
                }

                if (timers.Status == null || timers.Status.Destroyed)
                {
                    return;
                }

                timers.Status.Destroy();
            }

            public static void UpdateLockoutUI(BasePlayer player)
            {
                Lockouts.RemoveAll(p => !p.IsReallyConnected());

                if (!player.IsReallyConnected())
                {
                    return;
                }

                DestroyLockoutUI(player);

                if (!config.UI.Lockout.Enabled)
                {
                    return;
                }

                var uii = GetSettings(player.UserIDString);

                if (!uii.Enabled || !uii.Lockouts)
                {
                    return;
                }

                ShowLockouts(player);
                SetLockoutUpdate(player);
            }

            private static void SetLockoutUpdate(BasePlayer player)
            {
                Timers timers;
                if (!InvokeTimers.TryGetValue(player.userID, out timers))
                {
                    InvokeTimers[player.userID] = timers = new Timers();
                }

                if (timers.Lockout == null || timers.Lockout.Destroyed)
                {
                    timers.Lockout = Instance.timer.Once(60f, () => UpdateLockoutUI(player));
                }
                else
                {
                    timers.Lockout.Reset();
                }
            }

            public static void DestroyLockoutUpdate(BasePlayer player)
            {
                Timers timers;
                if (!InvokeTimers.TryGetValue(player.userID, out timers))
                {
                    return;
                }

                if (timers.Lockout == null || timers.Lockout.Destroyed)
                {
                    return;
                }

                timers.Lockout.Destroy();
                InvokeTimers.Remove(player.userID);
            }

            public static Info GetSettings(string playerId)
            {
                Info uii;
                if (!data.UI.TryGetValue(playerId, out uii))
                {
                    data.UI[playerId] = uii = new Info();
                }

                return uii;
            }

            public const string StatusPanelName = "RB_UI_Status";
            public const string BuyablePanelName = "RB_UI_Buyable";
            public const string DetailsPanelName = "RB_UI_Details";

            public static List<BasePlayer> Players { get; set; }
            public static List<BasePlayer> Lockouts { get; set; }
            public static List<BasePlayer> Buyables { get; set; }
            public static Dictionary<ulong, Timers> InvokeTimers { get; set; }

            public static void Clear()
            {
                Players = null;
                Lockouts = null;
                Buyables = null;
                InvokeTimers = null;
            }

            public static void Init()
            {
                Players = new List<BasePlayer>();
                Lockouts = new List<BasePlayer>();
                Buyables = new List<BasePlayer>();
                InvokeTimers = new Dictionary<ulong, Timers>();
            }

            public class Timers
            {
                public Timer Status;
                public Timer Lockout;
            }

            public class Info
            {
                public bool Enabled { get; set; } = true;
                public bool Lockouts { get; set; } = true;
                public bool Status { get; set; } = true;
                public bool All() => Enabled && Lockouts && Status;
            }
        }

        private void CommandUI(IPlayer user, string command, string[] args)
        {
            if (user.IsServer)
            {
                return;
            }

            var uii = UI.GetSettings(user.Id);
            var player = user.ToPlayer();

            if (args.Length == 0)
            {
                uii.Enabled = !uii.Enabled;

                if (uii.Enabled)
                {
                    UI.UpdateStatusUI(player);
                    UI.UpdateLockoutUI(player);
                }
                else
                {
                    UI.DestroyStatusUI(player);
                    UI.DestroyLockoutUI(player);
                }

                return;
            }

            switch (args[0].ToLower())
            {
                case "lockouts":
                    {
                        uii.Lockouts = !uii.Lockouts;
                        UI.UpdateLockoutUI(player);
                        return;
                    }
                case "status":
                    {
                        uii.Status = !uii.Status;
                        UI.UpdateStatusUI(player);
                        return;
                    }
            }
        }

        #endregion UI
    }
}

namespace Oxide.Plugins.RaidableBasesExtensionMethods
{
    public static class ExtensionMethods
    {
        internal static Core.Libraries.Permission p;
        public static bool All<T>(this IList<T> a, Func<T, bool> b) { for (int i = 0; i < a.Count; i++) { if (!b(a[i])) { return false; } } return true; }
        public static int Average(this IList<int> a) { if (a.Count == 0) { return 0; } int b = 0; for (int i = 0; i < a.Count; i++) { b += a[i]; } return b / a.Count; }
        public static T ElementAt<T>(this IEnumerable<T> a, int b) { using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (b == 0) { return c.Current; } b--; } } return default(T); }
        public static bool Exists<T>(this HashSet<T> a) where T : BaseEntity { foreach (var b in a) { if (!b.IsKilled()) { return true; } } return false; }
        public static bool Exists<T>(this IEnumerable<T> a, Func<T, bool> b = null) { using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (b == null || b(c.Current)) { return true; } } } return false; }
        public static T FirstOrDefault<T>(this IEnumerable<T> a, Func<T, bool> b = null) { using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (b == null || b(c.Current)) { return c.Current; } } } return default(T); }
        public static void RemoveAll<K, V>(this IDictionary<K, V> a, Func<K, V, bool> b) { var c = new List<K>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { if (b(d.Current.Key, a[d.Current.Key])) { c.Add(d.Current.Key); } } } for (int e = 0; e < c.Count; e++) { a.Remove(c[e]); } }
        public static IEnumerable<V> Select<T, V>(this IEnumerable<T> a, Func<T, V> b) { var c = new List<V>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { c.Add(b(d.Current)); } } return c; }
        public static string[] Skip(this string[] a, int b) { if (a.Length == 0) { return Array.Empty<string>(); } string[] c = new string[a.Length - b]; int n = 0; for (int i = 0; i < a.Length; i++) { if (i < b) continue; c[n] = a[i]; n++; } return c; }
        public static List<T> Take<T>(this IList<T> a, int b) { var c = new List<T>(); for (int i = 0; i < a.Count; i++) { if (c.Count == b) { break; } c.Add(a[i]); } return c; }
        public static Dictionary<T, V> ToDictionary<S, T, V>(this IEnumerable<S> a, Func<S, T> b, Func<S, V> c) { var d = new Dictionary<T, V>(); using (var e = a.GetEnumerator()) { while (e.MoveNext()) { d[b(e.Current)] = c(e.Current); } } return d; }
        public static List<T> ToList<T>(this IEnumerable<T> a) { var b = new List<T>(); using (var c = a.GetEnumerator()) { while (c.MoveNext()) { b.Add(c.Current); } } return b; }
        public static List<T> Where<T>(this IEnumerable<T> a, Func<T, bool> b) { var c = new List<T>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { if (b(d.Current)) { c.Add(d.Current); } } } return c; }
        public static List<T> OfType<T>(this IEnumerable<BaseNetworkable> a) where T : BaseEntity { var b = new List<T>(); using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (c.Current is T) { b.Add(c.Current as T); } } } return b; }
        public static int Sum<T>(this IList<T> a, Func<T, int> b) { int c = 0; for (int i = 0; i < a.Count; i++) { var d = b(a[i]); if (!float.IsNaN(d)) { c += d; } } return c; }
        public static bool HasPermission(this string a, string b) { if (p == null) { p = Interface.Oxide.GetLibrary<Core.Libraries.Permission>(null); } return !string.IsNullOrEmpty(a) && p.UserHasPermission(a, b); }
        public static bool HasPermission(this BasePlayer a, string b) { return a.UserIDString.HasPermission(b); }
        public static bool HasPermission(this ulong a, string b) { return a.ToString().HasPermission(b); }
        public static bool IsReallyConnected(this BasePlayer a) { return a.IsReallyValid() && a.net.connection != null; }
        public static bool IsKilled(this BaseNetworkable a) { return (object)a == null || a.IsDestroyed; }
        public static bool IsNull<T>(this T a) where T : class { return a == null; }
        public static bool IsNull(this BasePlayer a) { return (object)a == null; }
        public static bool IsReallyValid(this BaseNetworkable a) { return !((object)a == null || a.IsDestroyed || (object)a.net == null); }
        public static void SafelyKill(this BaseNetworkable a) { if (a.IsKilled()) { return; } a.Kill(BaseNetworkable.DestroyMode.None); }
        public static bool CanCall(this Plugin o) { return (object)o != null && o.IsLoaded; }
        public static bool IsInBounds(this OBB o, Vector3 a) { return o.ClosestPoint(a) == a; }
        public static bool IsHuman(this BasePlayer a) { return !(a.IsNpc || !a.userID.IsSteamId()); }
        public static bool IsCheating(this BasePlayer a) { return a._limitedNetworking || a.IsFlying || a.UsedAdminCheat(30f) || a.IsGod() || a.metabolism?.calories?.min == 500; }
        public static void SetAiming(this BasePlayer a, bool f) { a.modelState.aiming = f; a.SendNetworkUpdate(); }
        public static BasePlayer ToPlayer(this IPlayer user) { return user.Object as BasePlayer; }
        public static string ObjectName(this Collider collider) { try { return collider.gameObject.name ?? string.Empty; } catch { return string.Empty; } }
        public static T GetRandom<T>(this HashSet<T> h) { if (h == null || h.Count == 0) { return default(T); } return h.ElementAt(UnityEngine.Random.Range(0, h.Count)); }
        public static int InventorySlots(this StorageContainer a) { if (a.IsKilled() || a.inventory == null) return 0; return a.inventorySlots; } }
}
