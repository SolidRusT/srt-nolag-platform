using Oxide.Core;
using Oxide.Core.Configuration;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DeepWater", "Colon Blow", "1.0.25")]
    class DeepWater : RustPlugin
    {
        private static Vector3 Vector3_90 = new Vector3(0f, 90f, 0f);
        private static Vector3 Vector3_180 = new Vector3(0f, 180f, 0f);
        private static Vector3 Vector3_270 = new Vector3(0f, 270f, 0f);

        private const string prefabfoundation = "assets/prefabs/building core/foundation/foundation.prefab";
        private const string prefabfloor = "assets/prefabs/building core/floor/floor.prefab";
        private const string prefabfloorframe = "assets/prefabs/building core/floor/floor.prefab";
        private const string prefabwall = "assets/prefabs/building core/wall/wall.prefab";
        private const string prefabwallframe = "assets/prefabs/building core/wall.frame/wall.frame.prefab";
        private const string prefablowwall = "assets/prefabs/building core/wall.low/wall.low.prefab";
        private const string prefabstairsl = "assets/prefabs/building core/stairs.l/block.stair.lshape.prefab";
        private const string prefabpumpjack = "assets/prefabs/deployable/oil jack/mining.pumpjack.prefab";
        private const string prefabladder = "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab";
        private const string prefabrefinerysmall = "assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab";
        private const string prefabfurnacelarge = "assets/prefabs/deployable/furnace.large/furnace.large.prefab";
        private const string prefabwindmill = "assets/prefabs/deployable/windmill/generator.wind.scrap.prefab";
        private const string prefabceilinglight = "assets/prefabs/deployable/ceiling light/ceilinglight.deployed.prefab";
        private const string prefabrug = "assets/prefabs/deployable/rug/rug.deployed.prefab";         //1371746398
        private const string prefabfence = "assets/prefabs/building/wall.frame.fence/wall.frame.fence.prefab";
        private const string prefabheli = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab";
        private const string prefabbarrel = "assets/bundled/prefabs/radtown/oil_barrel.prefab";
        private const string prefabattirevending = "assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_attire.prefab";
        private const string prefabbuildingvending = "assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_building.prefab";
        private const string prefabcomponentsvending = "assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_components.prefab";
        private const string prefabresourcesvending = "assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_resources.prefab";
        private const string prefabtoolsvending = "assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_tools.prefab";
        private const string prefabweaponsvending = "assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_weapons.prefab";
        private const string prefabpeacekeeper = "assets/content/props/sentry_scientists/sentry.scientist.static.prefab";
        private const string prefabrecycler = "assets/bundled/prefabs/static/recycler_static.prefab";
        private const string prefabworkbench = "assets/prefabs/deployable/tier 3 workbench/workbench3.deployed.prefab";
        private const string prefabresearchtable = "assets/prefabs/deployable/research table/researchtable_deployed.prefab";
        private const string prefabwaterwell = "assets/prefabs/deployable/water well/waterwellstatic.prefab";
        #region Load

        private bool initialized;

        private void Loaded()
        {
            LoadVariables();
            lang.RegisterMessages(messages, this);
            permission.RegisterPermission("deepwater.spawn", this);
        }

        private void OnServerInitialized()
        {
            LoadDataFile();
            initialized = true;
            timer.In(5, RestoreSavedRigs);
            if (SpawnRandomOnLoad) timer.In(30, SetAutoSpawnEnabled);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("No configuration file found, generating...");
            Config.Clear();
            LoadVariables();
        }

        private void RestoreSavedRigs()
        {
            if (storedData.saveRigData.Count > 0)
            {
                BaseEntity[] objects = BaseEntity.saveList.Where(x => x is BaseEntity).ToArray();
                if (objects != null)
                {
                    foreach (var obj in objects)
                    {
                        if (storedData.saveRigData.ContainsKey(obj.net.ID))
                        {
                            var getent = obj.GetComponent<BaseEntity>();
                            if (getent) getent.Invoke("KillMessage", 0.1f);

                            var signownerid = storedData.saveRigData[obj.net.ID].ownerid;
                            var signpos = StringToVector3(storedData.saveRigData[obj.net.ID].pos);
                            var signrot = StringToQuaternion(storedData.saveRigData[obj.net.ID].rot);
                            var signangle = StringToVector3(storedData.saveRigData[obj.net.ID].eangles);
                            var rigcompound = storedData.saveRigData[obj.net.ID].iscompound;
                            timer.Once(2f, () => SpawnRig(signownerid, signpos, signrot, signangle, rigcompound));

                            storedData.saveRigData.Remove(obj.net.ID);
                            SaveData();
                        }
                    }
                }
            }
        }

        #endregion

        #region Configuration

        private string gridLocation;

        static bool SpawnRandomOnLoad = false;

        static bool EnableAutoZoneOnRandom = false;
        static bool EnableAutoZoneOnLocal = false;
        static bool EnableAutoZoneOnCompound = false;
        static bool EnableAutoZoneOnRigEvent = true;

        static bool useStaticRandomID = false;
        static bool useStaticLocalID = false;
        static bool useStaticComoundID = false;
        static bool useStaticEventID = false;

        static string randomRigZoneID = "123456";
        static string localRigZoneID = "223344";
        static string compoundRigZoneID = "555555";
        static string eventRigZoneID = "999999";

        static string randomzoneargs = @"radius,50,enter_message,You have entered a Random Rig Area";
        static string localzoneargs = @"radius,50,enter_message,You have entered a Rig Area";
        static string compoundzoneargs = @"radius,50,enter_message,You have entered a Compound Rig Area";
        static string eventzoneargs = @"radius,50,enter_message,You have entered a Event Rig Area";

        static int PumpStartingFuel = 100;

        static uint HeliPadRugLogo1 = 1371746398;
        static uint HeliPadRugLogo2 = 1371746398;

        static bool UseSentryOnCompound = false;

        bool CanDamageRig = false;

        bool DestroyRandomOnReload = false;
        bool DestroyLocalOnReload = false;

        float RigSpawnHeight = 16f;

        static bool UseDespawnOnRandom = true;
        static bool UseDespawnOnLocal = false;

        static bool AutoMoveLiftBucket = true;
        static bool StandardRigHasLiftBucket = true;
        static bool StandardRigHasBarrels = false;
        static bool StandardRigHasLadders = true;
        static bool StandardRigHasLootSpawn = true;

        static float DespawnTime = 60000f;
        static float LootRespawnTime = 1000f;

        static int EventMinRestartTime = 10000;
        static int EventMaxRestartTime = 30000;
        static int MinPlayersForEventSpawn = 5;
        static bool EventRigHasLiftBucket = true;
        static bool EventRigHasBarrels = true;
        static bool EventRigHasLadders = true;
        static bool EventRigHasHackCrate = true;
        static bool EventRigHasLootSpawn = true;
        static bool AddFullDivingKit = true;
        static int Stage1Duration = 6000;
        static int Stage2Duration = 4000;
        static float Stage2HackDuration = 200f;
        static int Stage3Duration = 1000;
        static int Stage3TimeBetweenBarrels = 100;
        static float Stage3BarrelDamage = 50f;
        static int Stage4Duration = 2000;
        static float Stage4HackDefaultReset = 900f;


        static int MaxLootCratesToSpawn = 2;
        static string LootPrefab1 = "assets/bundled/prefabs/radtown/crate_basic.prefab";
        static string LootPrefab2 = "assets/bundled/prefabs/radtown/crate_elite.prefab";
        static string LootPrefab3 = "assets/bundled/prefabs/radtown/crate_mine.prefab";
        static string LootPrefab4 = "assets/bundled/prefabs/radtown/crate_normal.prefab";
        static string LootPrefab5 = "assets/bundled/prefabs/radtown/crate_normal_2.prefab";

        static bool HasOilDeposit = true;
        static bool HasHQMetalDeposit = true;
        static bool HasSulfurDeposit = true;
        static bool HasMetalDeposit = true;

        static int OilDepositTickRate = 1;
        static int HQMetalTickRate = 1;
        static int SulfurOreTickRate = 1;
        static int MetalOreTickRate = 1;

        static bool BroadcastSpawn = true;
        static bool BroadcastEventSpawn = true;
        static bool BroadcastEventEnd = true;
        static bool BroadCastEventStages = true;
        int eventcounter = 0;
        int eventrespawntime = EventMaxRestartTime;

        bool Changed;
        static bool DoRandomRespawn = false;
        bool EventCountDown = false;

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        private void LoadConfigVariables()
        {
            CheckCfg("Spawn - Starting Low Grade Fuel amount for engine when Deepwater spawns : ", ref PumpStartingFuel);
            CheckCfg("Spawn - Broadcast to chat when Deepwater spawns and its coords ? ", ref BroadcastSpawn);

            CheckCfg("ZoneManager - Automatic Zone Options - Local Zone Creation Arguments (except zone id, no space after commas) ", ref localzoneargs);
            CheckCfg("ZoneManager - Automatic Zone Options - Random Zone Creation Arguments (except zone id, no space after commas) ", ref randomzoneargs);
            CheckCfg("ZoneManager - Automatic Zone Options - Compound Zone Creation Arguments (except zone id, no space after commas) ", ref compoundzoneargs);
            CheckCfg("ZoneManager - Automatic Zone Options - Event Rig Zone Creation Arguments (except zone id, no space after commas) ", ref eventzoneargs);

            CheckCfg("ZoneManager - Automatic Zone - Autmatically spawn a zone when Random Rig Spawns ? ", ref EnableAutoZoneOnRandom);
            CheckCfg("ZoneManager - Automatic Zone - Autmatically spawn a zone when Local Rig Spawns ? ", ref EnableAutoZoneOnLocal);
            CheckCfg("ZoneManager - Automatic Zone - Autmatically spawn a zone when Compound Rig Spawns ? ", ref EnableAutoZoneOnCompound);
            CheckCfg("ZoneManager - Automatic Zone - Autmatically spawn a zone when Event Rig Spawns ? ", ref EnableAutoZoneOnRigEvent);

            CheckCfg("ZoneManager - Enable Predefined Zone ID - Turn this on for Event Rigs (otherwise, it autogenerates one from entity ID) ? ", ref useStaticEventID);
            CheckCfg("ZoneManager - Enable Predefined Zone ID - Turn this on for Compound Rigs (otherwise, it autogenerates one from entity ID) ? ", ref useStaticComoundID);
            CheckCfg("ZoneManager - Enable Predefined Zone ID - Turn this on for Random Rigs (otherwise, it autogenerates one from entity ID) ? ", ref useStaticRandomID);
            CheckCfg("ZoneManager - Enable Predefined Zone ID - Turn this on for Local Rigs (otherwise, it autogenerates one from entity ID) ? ", ref useStaticLocalID);

            CheckCfg("ZoneManager - Zone ID - Use This Predefined Event Rig Zone ID (if enabled) ", ref eventRigZoneID);
            CheckCfg("ZoneManager - Zone ID - Use This Predefined Compound Rig Zone ID (if enabled) ", ref compoundRigZoneID);
            CheckCfg("ZoneManager - Zone ID - Use This Predefined Random Rig Zone ID (if enabled) ", ref randomRigZoneID);
            CheckCfg("ZoneManager - Zone ID - Use This Predefined Local Rig Zone ID (if enabled) ", ref localRigZoneID);


            CheckCfg("Spawn - Helipad H logo Half 1 (H) rug skind ID ? ", ref HeliPadRugLogo1);
            CheckCfg("Spawn - Helipad H logo Half 2 (H) rug skind ID ? ", ref HeliPadRugLogo2);

            CheckCfg("Compound - Spawn the Sentry Guns if rig is a compound ? ", ref UseSentryOnCompound);

            CheckCfg("_Event - AutoSpawn - Spawn Event at random Intervals between Min and Max Respawn time ? ", ref SpawnRandomOnLoad);
            CheckCfg("_Event - Minimum Player Count needed to AutoSpawn Event Rig ? ", ref MinPlayersForEventSpawn);
            CheckCfg("_Event - Spawn Bucket Lift for Event Rigs ? ", ref EventRigHasLiftBucket);
            CheckCfg("_Event - Spawn Barrels (and explosions) for Event Rigs ? ", ref EventRigHasBarrels);
            CheckCfg("_Event - Spawn Ladders for Event Rigs ? ", ref EventRigHasLadders);
            CheckCfg("_Event - Spawn Hack Crate for Stage 2 ? ", ref EventRigHasHackCrate);
            CheckCfg("_Event - Hack Crate will always have a full Diving Kit (wetsuit, tank, fins and mask) ? ", ref AddFullDivingKit);
            CheckCfg("_Event - Spawn Loots crates for Event Rigs ? ", ref EventRigHasLootSpawn);
            CheckCfg("_Event - Broadcast Event stages to all players ? ", ref BroadCastEventStages);
            CheckCfg("_Event - Broadcast Event spawn to all players ?  ", ref BroadcastEventSpawn);
            CheckCfg("_Event - Broadcast Event ending to all players ?  ", ref BroadcastEventEnd);
            CheckCfg("_Event - AutoSpawn - Minimum Respawn Time for event  (Every 1000 is approx 1 min) : ", ref EventMinRestartTime);
            CheckCfg("_Event - AutoSpawn - Maximum Respawn Time for event  (Every 1000 is approx 1 min) : ", ref EventMaxRestartTime);
            CheckCfg("_Event - Duration - Rig Spawn to Stage 1 Wait Time : ", ref Stage1Duration);
            CheckCfg("_Event - Duration - Stage 1 to Stage 2 Wait Time (MAX is 4500) : ", ref Stage2Duration);
            CheckCfg("_Event - Duration - Stage 2 Duration to Stage 3 Wait Time : ", ref Stage3Duration);
            CheckCfg("_Event - Duration - Stage 3 Time between Barrel Explosions : ", ref Stage3TimeBetweenBarrels);
            CheckCfgFloat("_Event - Damage - Stage 3 Barrel Explosion Damage : ", ref Stage3BarrelDamage);
            CheckCfg("_Event - Duration - Stage 4 Countdown Time (Every 1000 is approx 1 min) : ", ref Stage4Duration);

            CheckCfgFloat("_Event - Hack time for Hack crate during event : ", ref Stage2HackDuration);
            CheckCfgFloat("_Event - Reset global Hack Crate time back to (900 is default) after event : ", ref Stage4HackDefaultReset);


            CheckCfgFloat("Spawn - Height of rig off water. 13 to 19 works best : ", ref RigSpawnHeight);
            CheckCfg("Spawn - Spawn Bucket Lift for Standard Rigs (non Event) ? ", ref StandardRigHasLiftBucket);
            CheckCfg("Spawn - Spawn Barrels for Standard Rigs (non Event) ? ", ref StandardRigHasBarrels);
            CheckCfg("Spawn - Spawn Ladders for Standard Rigs (non Event) ? ", ref StandardRigHasLadders);
            CheckCfg("Spawn - Spawn Loots Crates for Standard Rigs (non Event) ? ", ref StandardRigHasLootSpawn);

            CheckCfg("Allow Damage to Deepwater Rig ? ", ref CanDamageRig);

            CheckCfg("Destroy all Random Spawned Deepwater Rigs when plugin reloads or server restarts ? ", ref DestroyRandomOnReload);
            CheckCfg("Destroy all Manually Spawned Deepwater Rigs when plugin reloads or server restarts ? ", ref DestroyLocalOnReload);

            CheckCfg("Automatically have lift bucket go up and down periodically ? ", ref AutoMoveLiftBucket);

            CheckCfg("Despawn - Enable despawn timer on Randomly Spawned Deepwater Rigs ? ", ref UseDespawnOnRandom);
            CheckCfg("Despawn - Enable despawn timer on Manually placed Deepwater Rigs ? ", ref UseDespawnOnLocal);
            CheckCfgFloat("Despawn - Time before Deepwater Rig will despawn itself : ", ref DespawnTime);

            CheckCfgFloat("Loot Spawn - amount of time to respawn loot spawns on rig : ", ref LootRespawnTime);
            CheckCfg("Loot Spawn - maximum number of loot crates spawn locations (max 8) : ", ref MaxLootCratesToSpawn);
            CheckCfg("Loot Spawn - Random Loot prefab 1 : ", ref LootPrefab1);
            CheckCfg("Loot Spawn - Random Loot prefab 2 : ", ref LootPrefab2);
            CheckCfg("Loot Spawn - Random Loot prefab 3 : ", ref LootPrefab3);
            CheckCfg("Loot Spawn - Random Loot prefab 4 : ", ref LootPrefab4);
            CheckCfg("Loot Spawn - Random Loot prefab 5 : ", ref LootPrefab5);

            CheckCfg("Resource Deposit - Enable Crude Oil as a resource : ", ref HasOilDeposit);
            CheckCfg("Resource Deposit - Enable High Quality Metal Ore as a resource : ", ref HasHQMetalDeposit);
            CheckCfg("Resource Deposit - Enable Sulfur Ore as a resource : ", ref HasSulfurDeposit);
            CheckCfg("Resource Deposit - Enable Metal Ore as a resource : ", ref HasMetalDeposit);

            CheckCfg("Resource Deposit - Tick Rate - Get 1 Crude Oil every : (if enabled) : ", ref OilDepositTickRate);
            CheckCfg("Resource Deposit - Tick Rate - Get 1 HQ Metal every (if enabled) : ", ref HQMetalTickRate);
            CheckCfg("Resource Deposit - Tick Rate - Get 1 Sulfur Ore every (if enabled) : ", ref SulfurOreTickRate);
            CheckCfg("Resource Deposit - Tick Rate - Get 1 Metal Ore every (if enabled) : ", ref MetalOreTickRate);

        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        private void CheckCfgFloat(string Key, ref float var)
        {

            if (Config[Key] != null)
                var = System.Convert.ToSingle(Config[Key]);
            else
                Config[Key] = var;
        }

        #endregion

        #region Localization

        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            ["notauthorized"] = "You are not authorized to use that command!",
            ["rigspawn"] = "DeepWater Rig Event at grid : ",
            ["rigdespawn"] = "Deepwater Rig Event has Ended !!"
        };

        #endregion

        #region Commands

        // Spawns a Random Deepwater Rig

        [ChatCommand("spawnrandomrig")]
        void cmdSpawnRandomRig(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, "deepwater.spawn")) return;
            BuildRandomDeepWater(false);
        }

        [ConsoleCommand("spawnrandomrig")]
        void cmdConsoleSpawnRandomRig(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !IsAllowed(player, "deepwater.spawn")) return;
            BuildRandomDeepWater(false);
        }

        // Spawns Random Deepwater Rig with Event started

        [ChatCommand("spawnrandomrigevent")]
        void cmdSpawnRandomRigEvent(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, "deepwater.spawn")) return;
            BuildRandomDeepWater(true);
        }

        [ConsoleCommand("spawnrandomrigevent")]
        void cmdConsoleSpawnRandomRigEvent(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !IsAllowed(player, "deepwater.spawn")) return;
            BuildRandomDeepWater(true);
        }


        // Spawns a Deepwater rig on players location

        [ChatCommand("spawnrig")]
        void cmdSpawnRig(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, "deepwater.spawn")) return;
            BuildLocalDeepWater(player, false, 1);
        }

        // Spawns a Deepwater rig with Event Started on players location

        [ChatCommand("spawnrigevent")]
        void cmdSpawnRigEvent(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, "deepwater.spawn")) return;
            BuildLocalDeepWater(player, true);
        }

        [ChatCommand("destroyrig")]
        void cmdDestroyRig(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, "deepwater.spawn")) return;
            RaycastHit hit;
            BaseEntity hitentity = new BaseEntity();
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 50f)) hitentity = hit.GetTransform().gameObject.ToBaseEntity();
            if (hitentity == null) 
                return;
            
            var isdeepwater = hitentity.GetComponentInParent<DeepWaterEntity>() ?? null;

            if (isdeepwater) 
                UnityEngine.Object.Destroy(isdeepwater);
        }

        [ChatCommand("clearrigdatabase")]
        void cmdChatClearRigDataBase(BasePlayer player, string command, string[] args)
        {
            if (player.net?.connection?.authLevel > 1)
            {
                storedData.saveRigData.Clear();
                SaveData();
            }
            return;
        }

        #endregion

        #region RigAntihack check

        static List<BasePlayer> rigantihack = new List<BasePlayer>();

        object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (player == null) return null;
            if (rigantihack.Contains(player)) return false;
            return null;
        }

        #endregion

        #region Data 

        static Dictionary<ulong, PlayerRigCount> loadplayer = new Dictionary<ulong, PlayerRigCount>();

        public class PlayerRigCount
        {
            public BasePlayer player;
            public int rigcount;
        }

        static StoredData storedData = new StoredData();
        DynamicConfigFile dataFile;

        public class StoredData
        {
            public Dictionary<uint, StoredRigData> saveRigData = new Dictionary<uint, StoredRigData>();
            public StoredData() { }
        }

        public class StoredRigData
        {
            public ulong ownerid;
            public string pos;
            public string eangles;
            public string rot;
            public int iscompound;
        }

        void LoadDataFile()
        {
            dataFile = Interface.Oxide.DataFileSystem.GetFile(Title);

            try
            {
                storedData = dataFile.ReadObject<StoredData>();
            }
            catch { }

            if (storedData == null)
                storedData = new StoredData();
        }

        void AddData(uint entnetid, ulong sownerid, string spos, string seangles, string srot, int compound = 0)
        {
            if (storedData.saveRigData.ContainsKey(entnetid)) storedData.saveRigData.Remove(entnetid);

            storedData.saveRigData.Add(entnetid, new StoredRigData
            {
                ownerid = sownerid,
                pos = spos,
                eangles = seangles,
                rot = srot,
                iscompound = compound,
            });
            SaveData();
        }

        void RemoveData(uint entnetid)
        {
            if (storedData.saveRigData.ContainsKey(entnetid)) storedData.saveRigData.Remove(entnetid);
        }

        void SaveData()
        {
            if (dataFile != null && storedData != null)
            {
                dataFile.WriteObject(storedData);
            }
        }

        public static Vector3 StringToVector3(string sVector)
        {
            if (sVector.StartsWith("(") && sVector.EndsWith(")"))            
                sVector = sVector.Substring(1, sVector.Length - 2);
            
            string[] sArray = sVector.Split(',');

            return new Vector3(float.Parse(sArray[0]), float.Parse(sArray[1]), float.Parse(sArray[2]));
        }

        public static Quaternion StringToQuaternion(string sVector)
        {
            if (sVector.StartsWith("(") && sVector.EndsWith(")"))            
                sVector = sVector.Substring(1, sVector.Length - 2);
            
            string[] sArray = sVector.Split(',');

            return new Quaternion(float.Parse(sArray[0]), float.Parse(sArray[1]), float.Parse(sArray[2]), float.Parse(sArray[3]));
        }


        #endregion

        #region Hooks

        public void CreateZone(BaseEntity entity, string zoneidstr, string[] zoneargs)
        {
            var spawnpos = entity.transform.position;
            var ZoneManager = plugins.Find("ZoneManager");
            if (!Convert.ToBoolean(ZoneManager?.Call("CreateOrUpdateZone", zoneidstr, zoneargs, spawnpos)))
            {
                return;
            }
        }

        public void EraseZone(string zoneid)
        {
            var ZoneManager = plugins.Find("ZoneManager");

            if (!Convert.ToBoolean(ZoneManager?.Call("EraseZone", zoneid)))
            {
                return;
            }
        }

        private int GetRandomTime()
        {
            var respawnroll = EventMaxRestartTime;
            var randomroll = UnityEngine.Random.Range(EventMinRestartTime, EventMaxRestartTime);
            respawnroll = randomroll;
            EventCountDown = true;
            return respawnroll;
        }

        void OnTick()
        {
            if (DoRandomRespawn)
            {
                DoRandomRespawn = false;
                eventrespawntime = GetRandomTime();
                return;
            }
            if (!DoRandomRespawn && EventCountDown)
            {
                if (eventcounter == eventrespawntime)
                { SpawnRandom(); EventCountDown = false; eventcounter = 0; return; }
                eventcounter = eventcounter + 1;
                return;
            }
        }

        void SetAutoSpawnEnabled()
        {
            DoRandomRespawn = true;
        }

        void SpawnRandom()
        {
            var onlineplayers = BasePlayer.activePlayerList.Count;
            if (onlineplayers < MinPlayersForEventSpawn)
            {
                PrintWarning("Minimum Player count not reached, Deepwater Event did not spawn !!!");
                DoRandomRespawn = true;
                return;
            }
            BuildRandomDeepWater(true, true);
        }

        bool IsAllowed(BasePlayer player, string perm)
        {
            if (permission.UserHasPermission(player.userID.ToString(), perm)) return true;
            return false;
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (CanDamageRig) return;
            if (entity == null || hitInfo == null) return;
            var isdeepwater = entity.GetComponentInParent<DeepWaterEntity>();
            if (isdeepwater) hitInfo.damageTypes.ScaleAll(0);
            return;
        }

        void OnEntityDeath(BaseCombatEntity target, HitInfo info)
        {
            if (target == null || target.net == null || info == null) return;
            if (storedData.saveRigData.ContainsKey(target.net.ID))
            {
                storedData.saveRigData.Remove(target.net.ID);
                SaveData();
            }
        }

        private object OnEntityGroundMissing(BaseEntity entity)
        {
            var onship = entity.GetComponentInParent<DeepWaterEntity>();
            if (onship != null) return false;
            return null;
        }

        private object CanBuild(Planner plan, Construction prefab, object obj)
        {
            if (plan == null || prefab == null || obj == null) return null;
            if (obj is Construction.Target)
            {
                var target = (Construction.Target)obj;
                var targetent = target.entity as BaseEntity;
                var isdeepwater = targetent?.GetComponentInParent<DeepWaterEntity>();
                if (isdeepwater) return false;
                return null;
            }
            else return null;
        }

        private object CanPickupEntity(BasePlayer player, BaseCombatEntity entity)
        {
            if (entity == null || player == null) return null;
            if (entity.GetComponentInParent<DeepWaterEntity>()) return false;
            return null;
        }

        Vector3 FindSpawnPoint()
        {
            Vector3 spawnpoint = new Vector3();
            float mapoffset = 75f;
            float spawnline = ((ConVar.Server.worldsize) / 2) - 75f;

            float sidepicked = UnityEngine.Random.Range(1, 5);
            if (sidepicked == 1) spawnpoint = new Vector3(UnityEngine.Random.Range(-spawnline, spawnline), RigSpawnHeight, -spawnline);
            else if (sidepicked == 2) spawnpoint = new Vector3(UnityEngine.Random.Range(-spawnline, spawnline), RigSpawnHeight, spawnline);
            else if (sidepicked == 3) spawnpoint = new Vector3(spawnline, RigSpawnHeight, UnityEngine.Random.Range(-spawnline, spawnline));
            else if (sidepicked == 4) spawnpoint = new Vector3(-spawnline, RigSpawnHeight, UnityEngine.Random.Range(-spawnline, spawnline));
            return spawnpoint;
        }

        string GetGridLocation(Vector3 position)
        {
            Vector2 offsetPos = new Vector2((World.Size / 2 - 6) + position.x, (World.Size / 2 - 56) - position.z);
            string gridstring = $"{Convert.ToChar(65 + (int)offsetPos.x / 146)}{(int)(offsetPos.y / 146)}";
            return gridstring;
        }

        public void BuildRandomDeepWater(bool eventstarted, bool autospawned = false)
        {
            string prefabpumpjack = "assets/prefabs/visualization/sphere.prefab";
            var spawnpos = FindSpawnPoint();
            BaseEntity newrig = GameManager.server.CreateEntity(prefabpumpjack, new Vector3(spawnpos.x, spawnpos.y, spawnpos.z), Quaternion.identity, true);
            newrig.OwnerID = 0;
            newrig?.Spawn();

            var addrigstructure = newrig.gameObject.AddComponent<DeepWaterEntity>();
            newrig.transform.hasChanged = true;
            newrig.SendNetworkUpdateImmediate();
            newrig.UpdateNetworkGroup();
            if (!eventstarted) AddData(newrig.net.ID, newrig.OwnerID, newrig.transform.position.ToString(), newrig.transform.eulerAngles.ToString(), newrig.transform.rotation.ToString());

            addrigstructure.israndom = true;
            if (eventstarted == true) { addrigstructure.stage1 = true; addrigstructure.isevent = true; }
            if (autospawned == true) { addrigstructure.isautospawn = true; }
            string getgrid = GetGridLocation(newrig.transform.position);
            if ((!eventstarted && BroadcastSpawn) || (eventstarted && BroadcastEventSpawn))
            {
                if (eventstarted) PrintWarning(TOD_Sky.Instance.Cycle.Hour + " : DeepWater Event Started at Grid : " + getgrid);
                if (eventstarted) { ConVar.Chat.Broadcast("Rig Event has Spawned at Grid : " + getgrid, "DeepWater", "#4286f4"); return; }
                ConVar.Chat.Broadcast("Rig has Spawned at Grid : " + getgrid, "DeepWater", "#4286f4");
            }
        }

        public void BuildLocalDeepWater(BasePlayer player, bool eventstarted, int compound = 0)
        {
            string prefabpumpjack = "assets/prefabs/visualization/sphere.prefab";
            var spawnpos = new Vector3(player.transform.position.x, RigSpawnHeight, player.transform.position.z);
            BaseEntity newrig = GameManager.server.CreateEntity(prefabpumpjack, spawnpos, new Quaternion(), true);
            newrig.OwnerID = 0;
            newrig?.Spawn();
            var addrigstructure = newrig.gameObject.AddComponent<DeepWaterEntity>();
            if (!eventstarted) AddData(newrig.net.ID, newrig.OwnerID, newrig.transform.position.ToString(), newrig.transform.eulerAngles.ToString(), newrig.transform.rotation.ToString(), compound);
            addrigstructure.israndom = false;
            if (compound == 1) addrigstructure.iscompound = true;
            if (eventstarted == true) { addrigstructure.stage1 = true; addrigstructure.isevent = true; }
            string getgrid = GetGridLocation(newrig.transform.position);
            if ((!eventstarted && BroadcastSpawn) || (eventstarted && BroadcastEventSpawn))
            {
                if (eventstarted) PrintWarning(TOD_Sky.Instance.Cycle.Hour + " : DeepWater Event Started at Grid : " + getgrid);

                if (compound == 1) { ConVar.Chat.Broadcast("Rig Compound has Spawned at Grid : " + getgrid, "DeepWater", "#4286f4"); return; }
                if (eventstarted) { ConVar.Chat.Broadcast("Rig Event has Spawned at Grid : " + getgrid, "DeepWater", "#4286f4"); return; }
                ConVar.Chat.Broadcast("Rig has Spawned at Grid : " + getgrid, "DeepWater", "#4286f4");
            }
        }

        void SpawnRig(ulong ownerid, Vector3 pos, Quaternion rot, Vector3 angle, int compound = 0)
        {
            string prefabpumpjack = "assets/prefabs/visualization/sphere.prefab";
            BaseEntity newrig = GameManager.server.CreateEntity(prefabpumpjack, pos, rot, true);
            newrig.OwnerID = ownerid;
            newrig?.Spawn();
            var addrigstructure = newrig.gameObject.AddComponent<DeepWaterEntity>();
            if (compound == 1) addrigstructure.iscompound = true;
            AddData(newrig.net.ID, ownerid, pos.ToString(), angle.ToString(), rot.ToString(), compound);
        }

        void DestroyAll()
        {
            DeepWaterEntity[] objects = UnityEngine.Object.FindObjectsOfType<DeepWaterEntity>();
            if (objects != null)
            {
                foreach (DeepWaterEntity deepWaterEntity in objects)
                {
                    if (deepWaterEntity.isevent)
                        UnityEngine.Object.Destroy(deepWaterEntity);

                    if (DestroyRandomOnReload && deepWaterEntity.israndom)
                        UnityEngine.Object.Destroy(deepWaterEntity);

                    if (DestroyLocalOnReload && !deepWaterEntity.israndom)
                        UnityEngine.Object.Destroy(deepWaterEntity);                    
                }
            }
        }

        void Unload()
        {
            SaveData();
            DestroyAll();
        }

        #endregion

        #region DeepWater Entity

        public class DeepWaterEntity : MonoBehaviour
        {
            DeepWater deepwater;
            public MiningQuarry pumpjackentity;
            BaseEntity entity;
            Vector3 entitypos;
            Quaternion entityrot;

            BaseEntity spawnEntity;

            private List<BaseEntity> spawnedParts = Facepunch.Pool.GetList<BaseEntity>();

            BaseEntity refinerysmall;
            BaseEntity furnacelarge;
            BaseEntity windmill;

            BaseEntity lootspawn1; BaseEntity lootspawn2; BaseEntity lootspawn3; BaseEntity lootspawn4;
            BaseEntity lootspawn5; BaseEntity lootspawn6; BaseEntity lootspawn7; BaseEntity lootspawn8;

            BaseEntity leg1floorbase;
            BaseEntity leg1floor5wall1; BaseEntity leg1floor5wall2; BaseEntity leg1floor5wall3; BaseEntity leg1floor5wall4;
            BaseEntity leg1floor4wall1; BaseEntity leg1floor4wall2; BaseEntity leg1floor4wall3; BaseEntity leg1floor4wall4;
            BaseEntity leg1floor1wall1; BaseEntity leg1floor1wall2; BaseEntity leg1floor1wall3; BaseEntity leg1floor1wall4;
            BaseEntity leg1floor2wall1; BaseEntity leg1floor2wall2; BaseEntity leg1floor2wall3; BaseEntity leg1floor2wall4;
            BaseEntity leg1floor3wall1; BaseEntity leg1floor3wall2; BaseEntity leg1floor3wall3; BaseEntity leg1floor3wall4;

            BaseEntity leg2floorbase;
            BaseEntity leg2floor5wall1; BaseEntity leg2floor5wall2; BaseEntity leg2floor5wall3; BaseEntity leg2floor5wall4;
            BaseEntity leg2floor4wall1; BaseEntity leg2floor4wall2; BaseEntity leg2floor4wall3; BaseEntity leg2floor4wall4;
            BaseEntity leg2floor1wall1; BaseEntity leg2floor1wall2; BaseEntity leg2floor1wall3; BaseEntity leg2floor1wall4;
            BaseEntity leg2floor2wall1; BaseEntity leg2floor2wall2; BaseEntity leg2floor2wall3; BaseEntity leg2floor2wall4;
            BaseEntity leg2floor3wall1; BaseEntity leg2floor3wall2; BaseEntity leg2floor3wall3; BaseEntity leg2floor3wall4;

            BaseEntity leg3floorbase;
            BaseEntity leg3floor5wall1; BaseEntity leg3floor5wall2; BaseEntity leg3floor5wall3; BaseEntity leg3floor5wall4;
            BaseEntity leg3floor4wall1; BaseEntity leg3floor4wall2; BaseEntity leg3floor4wall3; BaseEntity leg3floor4wall4;
            BaseEntity leg3floor1wall1; BaseEntity leg3floor1wall2; BaseEntity leg3floor1wall3; BaseEntity leg3floor1wall4;
            BaseEntity leg3floor2wall1; BaseEntity leg3floor2wall2; BaseEntity leg3floor2wall3; BaseEntity leg3floor2wall4;
            BaseEntity leg3floor3wall1; BaseEntity leg3floor3wall2; BaseEntity leg3floor3wall3; BaseEntity leg3floor3wall4;

            BaseEntity leg4floorbase;
            BaseEntity leg4floor5wall1; BaseEntity leg4floor5wall2; BaseEntity leg4floor5wall3; BaseEntity leg4floor5wall4;
            BaseEntity leg4floor4wall1; BaseEntity leg4floor4wall2; BaseEntity leg4floor4wall3; BaseEntity leg4floor4wall4;
            BaseEntity leg4floor1wall1; BaseEntity leg4floor1wall2; BaseEntity leg4floor1wall3; BaseEntity leg4floor1wall4;
            BaseEntity leg4floor2wall1; BaseEntity leg4floor2wall2; BaseEntity leg4floor2wall3; BaseEntity leg4floor2wall4;
            BaseEntity leg4floor3wall1; BaseEntity leg4floor3wall2; BaseEntity leg4floor3wall3; BaseEntity leg4floor3wall4;

            BaseEntity deck1row1floor1; BaseEntity deck1row1floor2; BaseEntity deck1row1floor3; BaseEntity deck1row1floor4; BaseEntity deck1row1floor5; BaseEntity deck1row1floor6; BaseEntity deck1row1floor7;
            BaseEntity deck1row2floor1; BaseEntity deck1row2floor2; BaseEntity deck1row2floor3; BaseEntity deck1row2floor4; BaseEntity deck1row2floor5; BaseEntity deck1row2floor6; BaseEntity deck1row2floor7;
            BaseEntity deck1row3floor1; BaseEntity deck1row3floor2; BaseEntity deck1row3floor3; BaseEntity deck1row3floor4; BaseEntity deck1row3floor5;
            BaseEntity deck1row4floor1; BaseEntity deck1row4floor2; BaseEntity deck1row4floor3; BaseEntity deck1row4floor4; BaseEntity deck1row4floor5;
            BaseEntity helipadsupport1; BaseEntity helipadsupport2; BaseEntity helipadsupport3; BaseEntity helipadsupport4; BaseEntity helipadsupport5;
            BaseEntity deck1row2lowwall1; BaseEntity deck1row2lowwall2; BaseEntity deck1row2lowwall3; BaseEntity deck1row2lowwall4; BaseEntity deck1row2lowwall5;
            BaseEntity deck1row3lowwall1; BaseEntity deck1row3lowwall2; BaseEntity deck1row3lowwall3; BaseEntity deck1row3lowwall4; BaseEntity deck1row3lowwall5;
            BaseEntity deck1row4lowwall1; BaseEntity deck1row4lowwall2; BaseEntity deck1row4lowwall3; BaseEntity deck1row4lowwall4; BaseEntity deck1row4lowwall5;
            BaseEntity deck1row1wallframe1; BaseEntity deck1row1wallframe2; BaseEntity deck1row1wallframe3; BaseEntity deck1row1wallframe4; BaseEntity deck1row1wallframe5;
            BaseEntity deck1row2wallframe1; BaseEntity deck1row2wallframe2; BaseEntity deck1row2wallframe3; BaseEntity deck1row2wallframe4; BaseEntity deck1row2wallframe5;
            BaseEntity deck1row3wallframe1; BaseEntity deck1row3wallframe2; BaseEntity deck1row3wallframe3; BaseEntity deck1row3wallframe4; BaseEntity deck1row3wallframe5;
            BaseEntity deck1row4wallframe1; BaseEntity deck1row4wallframe2; BaseEntity deck1row4wallframe3; BaseEntity deck1row4wallframe4; BaseEntity deck1row4wallframe5;
            BaseEntity deck1row1floorframe1; BaseEntity deck1row1floorframe2; BaseEntity deck1row1floorframe3; BaseEntity deck1row1floorframe4; BaseEntity deck1row1floorframe5;
            BaseEntity deck1row1floorgrill1; BaseEntity deck1row1floorgrill2; BaseEntity deck1row1floorgrill3; BaseEntity deck1row1floorgrill4; BaseEntity deck1row1floorgrill5;
            BaseEntity deck1row2floorframe1; BaseEntity deck1row2floorframe2; BaseEntity deck1row2floorframe3; BaseEntity deck1row2floorframe4; BaseEntity deck1row2floorframe5;
            BaseEntity deck1row2floorgrill1; BaseEntity deck1row2floorgrill2; BaseEntity BucketLift; BaseEntity deck1row2floorgrill4; BaseEntity deck1row2floorgrill5;
            BaseEntity deck1row3floorframe1; BaseEntity deck1row3floorframe2; BaseEntity deck1row3floorframe3;
            BaseEntity deck1row3floorgrill1; BaseEntity deck1row3floorgrill2; BaseEntity deck1row3floorgrill3;
            BaseEntity deck1row4floorframe1; BaseEntity deck1row4floorframe2; BaseEntity deck1row4floorframe3;
            BaseEntity deck1row4floorgrill1; BaseEntity deck1row4floorgrill2; BaseEntity deck1row4floorgrill3;

            BaseEntity deck2frontrowfloor1; BaseEntity deck2frontrowfloor2; BaseEntity deck2frontrowfloor3; BaseEntity deck2frontrowfloor4; BaseEntity deck2frontrowfloor5;
            BaseEntity deck2row1floor1; BaseEntity deck2row1floor2; BaseEntity deck2row1floor3; BaseEntity deck2row1floor4; BaseEntity deck2row1floor5;
            BaseEntity deck2row2floor1; BaseEntity deck2row2floor2; BaseEntity deck2row2floor3; BaseEntity deck2row2floor4; BaseEntity deck2row2floor5;
            BaseEntity deck2row3floor1; BaseEntity deck2row3floor2; BaseEntity deck2row3floor3;
            BaseEntity deck2row4floor1; BaseEntity deck2row4floor2; BaseEntity deck2row4floor3;
            BaseEntity deck2center1; BaseEntity deck2center2; BaseEntity deck2center3; BaseEntity deck2center4; BaseEntity deck2center5; BaseEntity deck2center6; BaseEntity deck2center7; BaseEntity deck2center8; BaseEntity deck2center9;
            BaseEntity deck2backrowfloor1; BaseEntity deck2backrowfloor2; BaseEntity deck2backrowfloor3; BaseEntity deck2backrowfloor4; BaseEntity deck2backrowfloor5;
            BaseEntity helipadsupportframe1; BaseEntity helipadsupportframe2; BaseEntity helipadsupportframe3; BaseEntity helipadsupportframe4; BaseEntity helipadsupportframe5;
            BaseEntity deck2row2lowwall1; BaseEntity deck2row2lowwall2; BaseEntity deck2row2lowwall3; BaseEntity deck2row2lowwall4; BaseEntity deck2row2lowwall5;
            BaseEntity deck2row3lowwall1; BaseEntity deck2row3lowwall2; BaseEntity deck2row3lowwall3; BaseEntity deck2row3lowwall3U; BaseEntity deck2row3lowwall4; BaseEntity deck2row3lowwall5;
            BaseEntity deck2row4lowwall1; BaseEntity deck2row4lowwall2; BaseEntity deck2row4lowwall3; BaseEntity deck2row4lowwall3U; BaseEntity deck2row4lowwall4; BaseEntity deck2row4lowwall5;
            BaseEntity ladder1leg1; BaseEntity ladder2leg1; BaseEntity ladder3leg1; BaseEntity ladder4leg1; BaseEntity ladder5leg1; BaseEntity ladder6leg1;
            BaseEntity ladder1leg2; BaseEntity ladder2leg2; BaseEntity ladder3leg2; BaseEntity ladder4leg2; BaseEntity ladder5leg2; BaseEntity ladder6leg2;
            BaseEntity ladder1leg3; BaseEntity ladder2leg3; BaseEntity ladder3leg3; BaseEntity ladder4leg3; BaseEntity ladder5leg3; BaseEntity ladder6leg3;
            BaseEntity ladder1leg4; BaseEntity ladder2leg4; BaseEntity ladder3leg4; BaseEntity ladder4leg4; BaseEntity ladder5leg4; BaseEntity ladder6leg4;

            BaseEntity lowerlight1; BaseEntity lowerlight2; BaseEntity lowerlight3; BaseEntity lowerlight4;
            BaseEntity lowerlight5; BaseEntity lowerlight6; BaseEntity lowerlight7; BaseEntity lowerlight8;

            BaseEntity upperlight1; BaseEntity upperlight2; BaseEntity upperlight3; BaseEntity upperlight4; BaseEntity upperlight5; BaseEntity upperlight6; BaseEntity upperlight7;

            BaseEntity helipadfloor1; BaseEntity helipadfloor2; BaseEntity helipadfloor3; BaseEntity helipadfloor4; BaseEntity helipadfloor5;
            BaseEntity helipadfloor6; BaseEntity helipadfloor7; BaseEntity helipadfloor8; BaseEntity helipadfloor9; BaseEntity helipadfloor10;
            BaseEntity helipadfence1; BaseEntity helipadfence2; BaseEntity helipadfence3; BaseEntity helipadfence4; BaseEntity helipadfence5;
            BaseEntity helilogo1; BaseEntity helilogo2;

            BaseEntity helicopter; BaseEntity helicrate;

            BaseEntity explosivecrate1; BaseEntity explosivecrate2; BaseEntity explosivecrate3; BaseEntity explosivecrate4; BaseEntity explosivecrate5;
            BaseEntity explosivecrate6; BaseEntity explosivecrate7; BaseEntity explosivecrate8; BaseEntity explosivecrate9; BaseEntity explosivecrate10;

            BaseEntity vendattire; BaseEntity vendbuilding; BaseEntity vendcomponents; BaseEntity vendresources; BaseEntity vendtools; BaseEntity vendweapons;
            BaseEntity peacekeeper1; BaseEntity peacekeeper2; BaseEntity peacekeeper3; BaseEntity peacekeeper4; BaseEntity peacekeeper5;
            BaseEntity recycler1; BaseEntity recycler2; BaseEntity workbench; BaseEntity researchtable; BaseEntity waterwell;

            BoxCollider boxcollider;
            bool didspawnvariables;
            bool isrepairing;
            public bool iscompound;
            public bool israndom;
            public bool isevent;
            public bool isautospawn;
            public bool stage1;
            public int stage1time;
            int stage1counter;
            public bool stage2;
            public int stage2time;
            int stage2counter;
            public bool stage3;
            bool stage3startfire;
            public int stage3time;
            public int stage3barreltime;
            int stage3barrelcounter;
            int stage3counter;
            public bool stage4;
            public int stage4time;
            int stage4counter;
            float F1HeightOffset;
            float F2HeightOffset;
            float F3HeightOffset;
            float F4HeightOffset;
            float F5HeightOffset;
            float ShiftOffset;
            float UDHeightOffset;
            float LDHeightOffSet;
            int count;
            int despawncount;
            int liftcount;
            int maxlootcrates;
            public int currentlootcrates;
            BaseEntity mapmarker;
            uint entitynetid;
            string zoneid;
            bool dorigdestroy;

            void Awake()
            {
                entity = GetComponentInParent<BaseEntity>();
                pumpjackentity = GetComponent<MiningQuarry>();
                entitypos = entity.transform.position;
                entityrot = Quaternion.identity;
                gameObject.name = "DeepWater";
                deepwater = new DeepWater();
                entitynetid = entity.net.ID;
                dorigdestroy = false;
                didspawnvariables = false;
                isrepairing = false;
                iscompound = false;
                israndom = false;
                isevent = false;
                isautospawn = false;
                stage1 = false;
                stage1time = Stage1Duration;
                stage1counter = 0;
                stage2 = false;
                stage2time = Stage2Duration;
                stage2counter = 0;
                stage3 = false;
                stage3startfire = false;
                stage3time = Stage3Duration;
                stage3barreltime = Stage3TimeBetweenBarrels;
                stage3barrelcounter = 0;
                stage3counter = 0;
                stage4 = false;
                stage4time = Stage4Duration;
                stage4counter = 0;
                F1HeightOffset = -13f;
                F2HeightOffset = -10f;
                F3HeightOffset = -7f;
                F4HeightOffset = -16f;
                F5HeightOffset = -19f;
                ShiftOffset = 0f;
                UDHeightOffset = -1f;
                LDHeightOffSet = -4f;
                count = 0;
                despawncount = 0;
                liftcount = 0;
                currentlootcrates = 0;
                maxlootcrates = MaxLootCratesToSpawn;
                zoneid = entitynetid.ToString();

                boxcollider = entity.gameObject.AddComponent<BoxCollider>();
                boxcollider.gameObject.layer = (int)Layer.Reserved1;
                boxcollider.isTrigger = true;
                boxcollider.center = new Vector3(0.5f, 5f, 0f);
                // left/right   up/down   front/back
                boxcollider.size = new Vector3(35, 20, 35);

                SpawnPumpJack();
                AddOilDeposit();
                SpawnRefinery();
                SpawnLeg1();
                SpawnLeg2();
                SpawnLeg3();
                SpawnLeg4();
                SpawnLowerDeck();
                SpawnLowerDeckGrid();
                SpawnUpperDeck();
                SpawnHelipad();
                SpawnLowerLights();
                SpawnUpperLights();
            }

            string GetZoneID()
            {
                string zoneidstr = entitynetid.ToString();
                if (isevent && useStaticEventID) { zoneidstr = eventRigZoneID; return zoneidstr; }
                if (iscompound && useStaticComoundID) { zoneidstr = compoundRigZoneID; return zoneidstr; }
                if (israndom && useStaticRandomID) { zoneidstr = randomRigZoneID; return zoneidstr; }
                if (useStaticLocalID) { zoneidstr = localRigZoneID; return zoneidstr; }
                return zoneidstr;
            }

            void AddZone()
            {
                if (entity == null) return;
                zoneid = GetZoneID();
                string[] randomArray = randomzoneargs.Split(',');
                string[] localArray = localzoneargs.Split(',');
                string[] compoundArray = compoundzoneargs.Split(',');
                string[] eventArray = eventzoneargs.Split(',');
                if (isevent && EnableAutoZoneOnRigEvent) { deepwater.CreateZone(entity, zoneid, eventArray); return; }
                if (iscompound && EnableAutoZoneOnCompound) { deepwater.CreateZone(entity, zoneid, compoundArray); return; }
                if (israndom && EnableAutoZoneOnRandom) { deepwater.CreateZone(entity, zoneid, randomArray); return; }
                if (!iscompound && !israndom && EnableAutoZoneOnLocal) { deepwater.CreateZone(entity, zoneid, localArray); return; }
            }

            void DespawnRig()
            {
                dorigdestroy = true;
                Destroy(this);
            }

            private void OnTriggerEnter(Collider col)
            {
                if (col.name.Contains("/player/player.prefab"))
                {
                    var player = col.GetComponentInParent<BasePlayer>() ?? null;
                    if (player != null && player.isMounted)
                    {
                        return;
                    }
                    if (player != null && player.IsSleeping())
                    {
                        return;
                    }
                    if (player.GetParentEntity() == base.gameObject.ToBaseEntity())
                    {
                        return;
                    }
                    if (player != null)
                    {
                        BaseEntity getpar = player.GetParentEntity() ?? null;
                        if (getpar == null)
                        {
                            //player.SetParent(entity, true, true);
                            player.PauseFlyHackDetection(99999f);
                            player.PauseSpeedHackDetection(99999f);
                            player.PauseVehicleNoClipDetection(99999f);
                        }
                    }
                }
            }

            private void OnTriggerExit(Collider col)
            {
                if (col.name.Contains("/player/player.prefab"))
                {
                    var player = col.GetComponentInParent<BasePlayer>() ?? null;
                    if (player != null)
                    {
                        if (player != null && player.IsSleeping())
                        {
                            return;
                        }
                        if (player.GetParentEntity() != base.gameObject.ToBaseEntity())
                        {
                            return;
                        }
                        //player.SetParent(null, true, true);
                        player.PauseFlyHackDetection(5f);
                        player.PauseSpeedHackDetection(5f);
                        player.PauseVehicleNoClipDetection(5f);
                    }
                }
            }

            void CheckVariables()
            {
                SpawnRadBarrels();
                SpawnLadders();
                SpawnLift();
                SpawnRigLoot();
                AddMarker();
                if (iscompound) SpawnCompound();
                AddZone();
                RefreshAll();
                didspawnvariables = true;
            }

            void AddMarker()
            {
                string prefabmarker = "assets/prefabs/tools/map/explosionmarker.prefab";
                mapmarker = GameManager.server.CreateEntity(prefabmarker, entity.transform.position, Quaternion.identity, true);
                mapmarker.Spawn();
            }

            void AddOilDeposit()
            {
                if (HasOilDeposit)
                {
                    ResourceDepositManager.ResourceDeposit newresourece1 = ResourceDepositManager.GetOrCreate(entity.transform.position);
                    newresourece1.Add(ItemManager.FindItemDefinition("crude.oil"), 1f, 999999, OilDepositTickRate, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, true);
                    Interface.CallHook("OnResourceDepositCreated", newresourece1);
                }

                if (HasHQMetalDeposit)
                {
                    ResourceDepositManager.ResourceDeposit newresourece2 = ResourceDepositManager.GetOrCreate(entity.transform.position);
                    newresourece2.Add(ItemManager.FindItemDefinition("hq.metal.ore"), 1f, 999999, HQMetalTickRate, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, true);
                    Interface.CallHook("OnResourceDepositCreated", newresourece2);
                }

                if (HasSulfurDeposit)
                {
                    ResourceDepositManager.ResourceDeposit newresourece4 = ResourceDepositManager.GetOrCreate(entity.transform.position);
                    newresourece4.Add(ItemManager.FindItemDefinition("sulfur.ore"), 1f, 999999, SulfurOreTickRate, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, true);
                    Interface.CallHook("OnResourceDepositCreated", newresourece4);
                }

                if (HasMetalDeposit)
                {
                    ResourceDepositManager.ResourceDeposit newresourece5 = ResourceDepositManager.GetOrCreate(entity.transform.position);
                    newresourece5.Add(ItemManager.FindItemDefinition("metal.ore"), 1f, 999999, MetalOreTickRate, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, true);
                    Interface.CallHook("OnResourceDepositCreated", newresourece5);
                }

            }

            private BaseEntity SpawnPart(string prefab, BaseEntity entitypart, Vector3 localEulerAngle, Vector3 localPosition, ulong skin = 0UL)
            {
                if (entitypart == null)
                {
                    Vector3 position = entity.transform.TransformPoint(localPosition);
                    Quaternion rotation = entity.transform.rotation * Quaternion.Euler(localEulerAngle);
                    
                    entitypart = GameManager.server.CreateEntity(prefab, position, rotation);
                    entitypart.skinID = skin;
                    entitypart.Spawn();

                    if (entitypart != null)
                    {
                        spawnedParts.Add(entitypart);
                        SpawnRefresh(entitypart);
                    }
                }

                return entitypart;                
            }

            void SpawnRefresh(BaseNetworkable entity1)
            {
                var hasstab = entity1.GetComponent<StabilityEntity>() ?? null;
                if (hasstab != null)
                {
                    hasstab.grounded = true;
                }
                var isladder = entity1.GetComponent<BaseLadder>() ?? null;
                if (isladder != null)
                {
                    isladder.SetFlag(BaseEntity.Flags.Busy, true, true);
                }
                var hasdecay = entity1.GetComponent<DecayEntity>() ?? null;
                if (hasdecay != null)
                {
                    hasdecay.decay = null;
                }
                var hasblock = entity1.GetComponent<BuildingBlock>() ?? null;
                if (hasblock != null)
                {
                    hasblock.SetGrade(BuildingGrade.Enum.TopTier);
                    hasblock.SetHealthToMax();
                    hasblock.UpdateSkin();
                    hasblock.ClientRPC(null, "RefreshSkin");
                }
                
                NPCVendingMachine npcVendingMachine = entity1.GetComponent<NPCVendingMachine>();
                if (npcVendingMachine != null)                
                    InvokeHandler.Invoke(this, npcVendingMachine.InstallFromVendingOrders, 1f);                
            }

            public void RefreshAll()
            {
                if (entity == null) return;
                entity.transform.hasChanged = true;
                entity.SendNetworkUpdateImmediate();
                entity.UpdateNetworkGroup();

                if (entity.children != null)
                    for (int i = 0; i < entity.children.Count; i++)
                    {
                        entity.children[i].transform.hasChanged = true;
                        var isblock = entity.children[i].GetComponent<BuildingBlock>() ?? null;
                        if (isblock != null)
                        {
                            isblock.UpdateSkin();
                            isblock.ClientRPC(null, "RefreshSkin");
                        }
                        entity.children[i].SendNetworkUpdateImmediate(false);
                        entity.children[i].UpdateNetworkGroup();
                    }
            }

            void SpawnPumpJack()
            {
                pumpjackentity = GameManager.server.CreateEntity(prefabpumpjack, entity.transform.position, entity.transform.rotation, true) as MiningQuarry;               
                pumpjackentity.Spawn();

                if (pumpjackentity != null)
                {
                    ItemContainer component1 = pumpjackentity.GetComponent<MiningQuarry>().fuelStoragePrefab.instance.GetComponent<StorageContainer>().inventory;
                    Item addfuel = ItemManager.CreateByItemID(-946369541, PumpStartingFuel);
                    component1.itemList.Add(addfuel);
                    addfuel.parent = component1;
                    addfuel.MarkDirty();
                    pumpjackentity.EngineSwitch(true);
                }
            }

            void SpawnRefinery()
            {
                refinerysmall = SpawnPart(prefabrefinerysmall, refinerysmall, Vector3.zero, new Vector3(4.3f, 1.2f, 4.5f));
                furnacelarge = SpawnPart(prefabfurnacelarge, furnacelarge, Vector3.zero, new Vector3(-7f, 2f, -4f));
                if (furnacelarge != null) furnacelarge.SetFlag(BaseEntity.Flags.On, true, true);
            }

            void SpawnLeg1()
            {
                leg1floorbase = SpawnPart(prefabfoundation, leg1floorbase, Vector3.zero, new Vector3(9f + ShiftOffset, F5HeightOffset, 9f));

                leg1floor5wall1 = SpawnPart(prefabwall, leg1floor5wall1, new Vector3(0, 90, 0), new Vector3(9f + ShiftOffset, F5HeightOffset, 7.5f));
                leg1floor5wall2 = SpawnPart(prefabwall, leg1floor5wall2, Vector3_270, new Vector3(9f + ShiftOffset, F5HeightOffset, 10.5f));
                leg1floor5wall3 = SpawnPart(prefabwall, leg1floor5wall3, Vector3_180, new Vector3(7.5f + ShiftOffset, F5HeightOffset, 9f));
                leg1floor5wall4 = SpawnPart(prefabwall, leg1floor5wall4, Vector3.zero, new Vector3(10.5f + ShiftOffset, F5HeightOffset, 9f));

                leg1floor4wall1 = SpawnPart(prefabwall, leg1floor4wall1, Vector3_90, new Vector3(9f + ShiftOffset, F4HeightOffset, 7.5f));
                leg1floor4wall2 = SpawnPart(prefabwall, leg1floor4wall2, Vector3_270, new Vector3(9f + ShiftOffset, F4HeightOffset, 10.5f));
                leg1floor4wall3 = SpawnPart(prefabwall, leg1floor4wall3, Vector3_180, new Vector3(7.5f + ShiftOffset, F4HeightOffset, 9f));
                leg1floor4wall4 = SpawnPart(prefabwall, leg1floor4wall4, Vector3.zero, new Vector3(10.5f + ShiftOffset, F4HeightOffset, 9f));

                leg1floor1wall1 = SpawnPart(prefabwall, leg1floor1wall1, Vector3_90, new Vector3(9f + ShiftOffset, F1HeightOffset, 7.5f));
                leg1floor1wall2 = SpawnPart(prefabwall, leg1floor1wall2, Vector3_270, new Vector3(9f + ShiftOffset, F1HeightOffset, 10.5f));
                leg1floor1wall3 = SpawnPart(prefabwall, leg1floor1wall3, Vector3_180, new Vector3(7.5f + ShiftOffset, F1HeightOffset, 9f));
                leg1floor1wall4 = SpawnPart(prefabwall, leg1floor1wall4, Vector3.zero, new Vector3(10.5f + ShiftOffset, F1HeightOffset, 9f));

                leg1floor2wall1 = SpawnPart(prefabwall, leg1floor2wall1, Vector3_90, new Vector3(9f + ShiftOffset, F2HeightOffset, 7.5f));
                leg1floor2wall2 = SpawnPart(prefabwall, leg1floor2wall2, Vector3_270, new Vector3(9f + ShiftOffset, F2HeightOffset, 10.5f));
                leg1floor2wall3 = SpawnPart(prefabwall, leg1floor2wall3, Vector3_180, new Vector3(7.5f + ShiftOffset, F2HeightOffset, 9f));
                leg1floor2wall4 = SpawnPart(prefabwall, leg1floor2wall4, Vector3.zero, new Vector3(10.5f + ShiftOffset, F2HeightOffset, 9f));

                leg1floor3wall1 = SpawnPart(prefabwall, leg1floor3wall1, Vector3_90, new Vector3(9f + ShiftOffset, F3HeightOffset, 7.5f));
                leg1floor3wall2 = SpawnPart(prefabwall, leg1floor3wall2, Vector3_270, new Vector3(9f + ShiftOffset, F3HeightOffset, 10.5f));
                leg1floor3wall3 = SpawnPart(prefabwall, leg1floor3wall3, Vector3_180, new Vector3(7.5f + ShiftOffset, F3HeightOffset, 9f));
                leg1floor3wall4 = SpawnPart(prefabwall, leg1floor3wall4, Vector3.zero, new Vector3(10.5f + ShiftOffset, F3HeightOffset, 9f));
            }

            void SpawnLeg2()
            {
                leg2floorbase = SpawnPart(prefabfoundation, leg2floorbase, Vector3.zero, new Vector3(-9f + ShiftOffset, F5HeightOffset, -9f));

                leg2floor5wall1 = SpawnPart(prefabwall, leg2floor5wall1, Vector3_90, new Vector3(-9f + ShiftOffset, F5HeightOffset, -10.5f));
                leg2floor5wall2 = SpawnPart(prefabwall, leg2floor5wall2, Vector3_270, new Vector3(-9f + ShiftOffset, F5HeightOffset, -7.5f));
                leg2floor5wall3 = SpawnPart(prefabwall, leg2floor5wall3, Vector3_180, new Vector3(-10.5f + ShiftOffset, F5HeightOffset, -9f));
                leg2floor5wall4 = SpawnPart(prefabwall, leg2floor5wall4, Vector3.zero, new Vector3(-7.5f + ShiftOffset, F5HeightOffset, -9f));

                leg2floor4wall1 = SpawnPart(prefabwall, leg2floor4wall1, Vector3_90, new Vector3(-9f + ShiftOffset, F4HeightOffset, -10.5f));
                leg2floor4wall2 = SpawnPart(prefabwall, leg2floor4wall2, Vector3_270, new Vector3(-9f + ShiftOffset, F4HeightOffset, -7.5f));
                leg2floor4wall3 = SpawnPart(prefabwall, leg2floor4wall3, Vector3_180, new Vector3(-10.5f + ShiftOffset, F4HeightOffset, -9f));
                leg2floor4wall4 = SpawnPart(prefabwall, leg2floor4wall4, Vector3.zero, new Vector3(-7.5f + ShiftOffset, F4HeightOffset, -9f));

                leg2floor1wall1 = SpawnPart(prefabwall, leg2floor1wall1, Vector3_90, new Vector3(-9f + ShiftOffset, F1HeightOffset, -10.5f));
                leg2floor1wall2 = SpawnPart(prefabwall, leg2floor1wall2, Vector3_270, new Vector3(-9f + ShiftOffset, F1HeightOffset, -7.5f));
                leg2floor1wall3 = SpawnPart(prefabwall, leg2floor1wall3, Vector3_180, new Vector3(-10.5f + ShiftOffset, F1HeightOffset, -9f));
                leg2floor1wall4 = SpawnPart(prefabwall, leg2floor1wall4, Vector3.zero, new Vector3(-7.5f + ShiftOffset, F1HeightOffset, -9f));

                leg2floor2wall1 = SpawnPart(prefabwall, leg2floor2wall1, Vector3_90, new Vector3(-9f + ShiftOffset, F2HeightOffset, -10.5f));
                leg2floor2wall2 = SpawnPart(prefabwall, leg2floor2wall2, Vector3_270, new Vector3(-9f + ShiftOffset, F2HeightOffset, -7.5f));
                leg2floor2wall3 = SpawnPart(prefabwall, leg2floor2wall3, Vector3_180, new Vector3(-10.5f + ShiftOffset, F2HeightOffset, -9f));
                leg2floor2wall4 = SpawnPart(prefabwall, leg2floor2wall4, Vector3.zero, new Vector3(-7.5f + ShiftOffset, F2HeightOffset, -9f));

                leg2floor3wall1 = SpawnPart(prefabwall, leg2floor3wall1, Vector3_90, new Vector3(-9f + ShiftOffset, F3HeightOffset, -10.5f));
                leg2floor3wall2 = SpawnPart(prefabwall, leg2floor3wall2, Vector3_270, new Vector3(-9f + ShiftOffset, F3HeightOffset, -7.5f));
                leg2floor3wall3 = SpawnPart(prefabwall, leg2floor3wall3, Vector3_180, new Vector3(-10.5f + ShiftOffset, F3HeightOffset, -9f));
                leg2floor3wall4 = SpawnPart(prefabwall, leg2floor3wall4, Vector3.zero, new Vector3(-7.5f + ShiftOffset, F3HeightOffset, -9f));
            }

            void SpawnLeg3()
            {
                leg3floorbase = SpawnPart(prefabfoundation, leg3floorbase, Vector3.zero, new Vector3(9f + ShiftOffset, F5HeightOffset, -9f));

                leg3floor5wall1 = SpawnPart(prefabwall, leg3floor5wall1, Vector3_90, new Vector3(9f + ShiftOffset, F5HeightOffset, -10.5f));
                leg3floor5wall2 = SpawnPart(prefabwall, leg3floor5wall2, Vector3_270, new Vector3(9f + ShiftOffset, F5HeightOffset, -7.5f));
                leg3floor5wall3 = SpawnPart(prefabwall, leg3floor5wall3, Vector3_180, new Vector3(7.5f + ShiftOffset, F5HeightOffset, -9f));
                leg3floor5wall4 = SpawnPart(prefabwall, leg3floor5wall4, Vector3.zero, new Vector3(10.5f + ShiftOffset, F5HeightOffset, -9f));

                leg3floor4wall1 = SpawnPart(prefabwall, leg3floor4wall1, Vector3_90, new Vector3(9f + ShiftOffset, F4HeightOffset, -10.5f));
                leg3floor4wall2 = SpawnPart(prefabwall, leg3floor4wall2, Vector3_270, new Vector3(9f + ShiftOffset, F4HeightOffset, -7.5f));
                leg3floor4wall3 = SpawnPart(prefabwall, leg3floor4wall3, Vector3_180, new Vector3(7.5f + ShiftOffset, F4HeightOffset, -9f));
                leg3floor4wall4 = SpawnPart(prefabwall, leg3floor4wall4, Vector3.zero, new Vector3(10.5f + ShiftOffset, F4HeightOffset, -9f));

                leg3floor1wall1 = SpawnPart(prefabwall, leg3floor1wall1, Vector3_90, new Vector3(9f + ShiftOffset, F1HeightOffset, -10.5f));
                leg3floor1wall2 = SpawnPart(prefabwall, leg3floor1wall2, Vector3_270, new Vector3(9f + ShiftOffset, F1HeightOffset, -7.5f));
                leg3floor1wall3 = SpawnPart(prefabwall, leg3floor1wall3, Vector3_180, new Vector3(7.5f + ShiftOffset, F1HeightOffset, -9f));
                leg3floor1wall4 = SpawnPart(prefabwall, leg3floor1wall4, Vector3.zero, new Vector3(10.5f + ShiftOffset, F1HeightOffset, -9f));

                leg3floor2wall1 = SpawnPart(prefabwall, leg3floor2wall1, Vector3_90, new Vector3(9f + ShiftOffset, F2HeightOffset, -10.5f));
                leg3floor2wall2 = SpawnPart(prefabwall, leg3floor2wall2, Vector3_270, new Vector3(9f + ShiftOffset, F2HeightOffset, -7.5f));
                leg3floor2wall3 = SpawnPart(prefabwall, leg3floor2wall3, Vector3_180, new Vector3(7.5f + ShiftOffset, F2HeightOffset, -9f));
                leg3floor2wall4 = SpawnPart(prefabwall, leg3floor2wall4, Vector3.zero, new Vector3(10.5f + ShiftOffset, F2HeightOffset, -9f));

                leg3floor3wall1 = SpawnPart(prefabwall, leg3floor3wall1, Vector3_90, new Vector3(9f + ShiftOffset, F3HeightOffset, -10.5f));
                leg3floor3wall2 = SpawnPart(prefabwall, leg3floor3wall2, Vector3_270, new Vector3(9f + ShiftOffset, F3HeightOffset, -7.5f));
                leg3floor3wall3 = SpawnPart(prefabwall, leg3floor3wall3, Vector3_180, new Vector3(7.5f + ShiftOffset, F3HeightOffset, -9f));
                leg3floor3wall4 = SpawnPart(prefabwall, leg3floor3wall4, Vector3.zero, new Vector3(10.5f + ShiftOffset, F3HeightOffset, -9f));
            }

            void SpawnLeg4()
            {
                leg4floorbase = SpawnPart(prefabfoundation, leg4floorbase, Vector3.zero, new Vector3(-9f + ShiftOffset, F5HeightOffset, 9f));

                leg4floor5wall1 = SpawnPart(prefabwall, leg4floor5wall1, Vector3_90, new Vector3(-9f + ShiftOffset, F5HeightOffset, 7.5f));
                leg4floor5wall2 = SpawnPart(prefabwall, leg4floor5wall2, Vector3_270, new Vector3(-9f + ShiftOffset, F5HeightOffset, 10.5f));
                leg4floor5wall3 = SpawnPart(prefabwall, leg4floor5wall3, Vector3_180, new Vector3(-10.5f + ShiftOffset, F5HeightOffset, 9f));
                leg4floor5wall4 = SpawnPart(prefabwall, leg4floor5wall4, Vector3.zero, new Vector3(-7.5f + ShiftOffset, F5HeightOffset, 9f));

                leg4floor4wall1 = SpawnPart(prefabwall, leg4floor4wall1, Vector3_90, new Vector3(-9f + ShiftOffset, F4HeightOffset, 7.5f));
                leg4floor4wall2 = SpawnPart(prefabwall, leg4floor4wall2, Vector3_270, new Vector3(-9f + ShiftOffset, F4HeightOffset, 10.5f));
                leg4floor4wall3 = SpawnPart(prefabwall, leg4floor4wall3, Vector3_180, new Vector3(-10.5f + ShiftOffset, F4HeightOffset, 9f));
                leg4floor4wall4 = SpawnPart(prefabwall, leg4floor4wall4, Vector3.zero, new Vector3(-7.5f + ShiftOffset, F4HeightOffset, 9f));

                leg4floor1wall1 = SpawnPart(prefabwall, leg4floor1wall1, Vector3_90, new Vector3(-9f + ShiftOffset, F1HeightOffset, 7.5f));
                leg4floor1wall2 = SpawnPart(prefabwall, leg4floor1wall2, Vector3_270, new Vector3(-9f + ShiftOffset, F1HeightOffset, 10.5f));
                leg4floor1wall3 = SpawnPart(prefabwall, leg4floor1wall3, Vector3_180, new Vector3(-10.5f + ShiftOffset, F1HeightOffset, 9f));
                leg4floor1wall4 = SpawnPart(prefabwall, leg4floor1wall4, Vector3.zero, new Vector3(-7.5f + ShiftOffset, F1HeightOffset, 9f));

                leg4floor2wall1 = SpawnPart(prefabwall, leg4floor2wall1, Vector3_90, new Vector3(-9f + ShiftOffset, F2HeightOffset, 7.5f));
                leg4floor2wall2 = SpawnPart(prefabwall, leg4floor2wall2, Vector3_270, new Vector3(-9f + ShiftOffset, F2HeightOffset, 10.5f));
                leg4floor2wall3 = SpawnPart(prefabwall, leg4floor2wall3, Vector3_180, new Vector3(-10.5f + ShiftOffset, F2HeightOffset, 9f));
                leg4floor2wall4 = SpawnPart(prefabwall, leg4floor2wall4, Vector3.zero, new Vector3(-7.5f + ShiftOffset, F2HeightOffset, 9f));

                leg4floor3wall1 = SpawnPart(prefabwall, leg4floor3wall1, Vector3_90, new Vector3(-9f + ShiftOffset, F3HeightOffset, 7.5f));
                leg4floor3wall2 = SpawnPart(prefabwall, leg4floor3wall2, Vector3_270, new Vector3(-9f + ShiftOffset, F3HeightOffset, 10.5f));
                leg4floor3wall3 = SpawnPart(prefabwall, leg4floor3wall3, Vector3_180, new Vector3(-10.5f + ShiftOffset, F3HeightOffset, 9f));
                leg4floor3wall4 = SpawnPart(prefabwall, leg4floor3wall4, Vector3.zero, new Vector3(-7.5f + ShiftOffset, F3HeightOffset, 9f));
            }

            void SpawnLowerDeck()
            {
                deck1row1floor1 = SpawnPart(prefabfloor, deck1row1floor1, Vector3.zero, new Vector3(9f + ShiftOffset, LDHeightOffSet, 9f));
                deck1row1floor2 = SpawnPart(prefabfloor, deck1row1floor2, Vector3.zero, new Vector3(9f + ShiftOffset, LDHeightOffSet, 6f));
                deck1row1floor3 = SpawnPart(prefabfloor, deck1row1floor3, Vector3.zero, new Vector3(9f + ShiftOffset, LDHeightOffSet, 3f));
                deck1row1floor4 = SpawnPart(prefabfloor, deck1row1floor4, Vector3.zero, new Vector3(9f + ShiftOffset, LDHeightOffSet, 0f));
                deck1row1floor5 = SpawnPart(prefabfloor, deck1row1floor5, Vector3.zero, new Vector3(9f + ShiftOffset, LDHeightOffSet, -3f));
                deck1row1floor6 = SpawnPart(prefabfloor, deck1row1floor6, Vector3.zero, new Vector3(9f + ShiftOffset, LDHeightOffSet, -6f));
                deck1row1floor7 = SpawnPart(prefabfloor, deck1row1floor7, Vector3.zero, new Vector3(9f + ShiftOffset, LDHeightOffSet, -9f));

                deck1row2floor1 = SpawnPart(prefabfloor, deck1row2floor1, Vector3.zero, new Vector3(-9f + ShiftOffset, LDHeightOffSet, 9f));
                deck1row2floor2 = SpawnPart(prefabfloor, deck1row2floor2, Vector3.zero, new Vector3(-9f + ShiftOffset, LDHeightOffSet, 6f));
                deck1row2floor3 = SpawnPart(prefabfloor, deck1row2floor3, Vector3.zero, new Vector3(-9f + ShiftOffset, LDHeightOffSet, 3f));
                deck1row2floor4 = SpawnPart(prefabfloor, deck1row2floor4, Vector3.zero, new Vector3(-9f + ShiftOffset, LDHeightOffSet, 0f));
                deck1row2floor5 = SpawnPart(prefabfloor, deck1row2floor5, Vector3.zero, new Vector3(-9f + ShiftOffset, LDHeightOffSet, -3f));
                deck1row2floor6 = SpawnPart(prefabfloor, deck1row2floor6, Vector3.zero, new Vector3(-9f + ShiftOffset, LDHeightOffSet, -6f));
                deck1row2floor7 = SpawnPart(prefabfloor, deck1row2floor7, Vector3.zero, new Vector3(-9f + ShiftOffset, LDHeightOffSet, -9f));

                deck1row3floor1 = SpawnPart(prefabfloor, deck1row3floor1, Vector3.zero, new Vector3(6f + ShiftOffset, LDHeightOffSet, -9f));
                deck1row3floor2 = SpawnPart(prefabfloor, deck1row3floor2, Vector3.zero, new Vector3(3f + ShiftOffset, LDHeightOffSet, -9f));
                deck1row3floor3 = SpawnPart(prefabfloor, deck1row3floor3, Vector3.zero, new Vector3(0f + ShiftOffset, LDHeightOffSet, -9f));
                deck1row3floor4 = SpawnPart(prefabfloor, deck1row3floor4, Vector3.zero, new Vector3(-3f + ShiftOffset, LDHeightOffSet, -9f));
                deck1row3floor5 = SpawnPart(prefabfloor, deck1row3floor5, Vector3.zero, new Vector3(-6f + ShiftOffset, LDHeightOffSet, -9f));

                deck1row4floor1 = SpawnPart(prefabfloor, deck1row4floor1, Vector3.zero, new Vector3(6f + ShiftOffset, LDHeightOffSet, 9f));
                deck1row4floor2 = SpawnPart(prefabfloor, deck1row4floor2, Vector3.zero, new Vector3(3f + ShiftOffset, LDHeightOffSet, 9f));
                deck1row4floor3 = SpawnPart(prefabfloor, deck1row4floor3, Vector3.zero, new Vector3(0f + ShiftOffset, LDHeightOffSet, 9f));
                deck1row4floor4 = SpawnPart(prefabfloor, deck1row4floor4, Vector3.zero, new Vector3(-3f + ShiftOffset, LDHeightOffSet, 9f));
                deck1row4floor5 = SpawnPart(prefabfloor, deck1row4floor5, Vector3.zero, new Vector3(-6f + ShiftOffset, LDHeightOffSet, 9f));

                deck1row1wallframe1 = SpawnPart(prefabwallframe, deck1row1wallframe1, Vector3_180, new Vector3(7.5f + ShiftOffset, LDHeightOffSet, 6f));
                deck1row1wallframe2 = SpawnPart(prefabwall, deck1row1wallframe2, Vector3_180, new Vector3(4.5f + ShiftOffset, LDHeightOffSet, 3f));
                deck1row1wallframe3 = SpawnPart(prefabwall, deck1row1wallframe3, Vector3_180, new Vector3(4.5f + ShiftOffset, LDHeightOffSet, 0f));
                deck1row1wallframe4 = SpawnPart(prefabwall, deck1row1wallframe4, Vector3_180, new Vector3(4.5f + ShiftOffset, LDHeightOffSet, -3f));
                deck1row1wallframe5 = SpawnPart(prefabwallframe, deck1row1wallframe5, Vector3_180, new Vector3(7.5f + ShiftOffset, LDHeightOffSet, -6f));

                deck1row2wallframe1 = SpawnPart(prefabwallframe, deck1row2wallframe1, Vector3.zero, new Vector3(-7.5f + ShiftOffset, LDHeightOffSet, 6f));
                deck1row2wallframe2 = SpawnPart(prefabwall, deck1row2wallframe2, Vector3.zero, new Vector3(-4.5f + ShiftOffset, LDHeightOffSet, 3f));
                deck1row2wallframe3 = SpawnPart(prefabwall, deck1row2wallframe3, Vector3.zero, new Vector3(-4.5f + ShiftOffset, LDHeightOffSet, 0f));
                deck1row2wallframe4 = SpawnPart(prefabwall, deck1row2wallframe4, Vector3.zero, new Vector3(-4.5f + ShiftOffset, LDHeightOffSet, -3f));
                deck1row2wallframe5 = SpawnPart(prefabwallframe, deck1row2wallframe5, Vector3.zero, new Vector3(-7.5f + ShiftOffset, LDHeightOffSet, -6f));

                deck1row3wallframe1 = SpawnPart(prefabwallframe, deck1row3wallframe1, Vector3_90, new Vector3(6f + ShiftOffset, LDHeightOffSet, -7.5f));
                deck1row3wallframe2 = SpawnPart(prefabwall, deck1row3wallframe2, Vector3_90, new Vector3(3f + ShiftOffset, LDHeightOffSet, -4.5f));
                deck1row3wallframe3 = SpawnPart(prefabwall, deck1row3wallframe3, Vector3_90, new Vector3(0f + ShiftOffset, LDHeightOffSet, -4.5f));
                deck1row3wallframe4 = SpawnPart(prefabwall, deck1row3wallframe4, Vector3_90, new Vector3(-3f + ShiftOffset, LDHeightOffSet, -4.5f));
                deck1row3wallframe5 = SpawnPart(prefabwallframe, deck1row3wallframe5, Vector3_90, new Vector3(-6f + ShiftOffset, LDHeightOffSet, -7.5f));

                deck1row4wallframe1 = SpawnPart(prefabwallframe, deck1row4wallframe1, Vector3_270, new Vector3(6f + ShiftOffset, LDHeightOffSet, 7.5f));
                deck1row4wallframe2 = SpawnPart(prefabwall, deck1row4wallframe2, Vector3_270, new Vector3(3f + ShiftOffset, LDHeightOffSet, 4.5f));
                deck1row4wallframe3 = SpawnPart(prefabwall, deck1row4wallframe3, Vector3_270, new Vector3(0f + ShiftOffset, LDHeightOffSet, 4.5f));
                deck1row4wallframe4 = SpawnPart(prefabwall, deck1row4wallframe4, Vector3_270, new Vector3(-3f + ShiftOffset, LDHeightOffSet, 4.5f));
                deck1row4wallframe5 = SpawnPart(prefabwallframe, deck1row4wallframe5, Vector3_270, new Vector3(-6f + ShiftOffset, LDHeightOffSet, 7.5f));

                deck1row2lowwall1 = SpawnPart(prefablowwall, deck1row2lowwall1, Vector3.zero, new Vector3(-10.5f + ShiftOffset, LDHeightOffSet, 6f));
                deck1row2lowwall2 = SpawnPart(prefablowwall, deck1row2lowwall2, Vector3.zero, new Vector3(-10.5f + ShiftOffset, LDHeightOffSet, 3f));
                deck1row2lowwall3 = SpawnPart(prefablowwall, deck1row2lowwall3, Vector3.zero, new Vector3(-10.5f + ShiftOffset, LDHeightOffSet, 0f));
                deck1row2lowwall4 = SpawnPart(prefablowwall, deck1row2lowwall4, Vector3.zero, new Vector3(-10.5f + ShiftOffset, LDHeightOffSet, -3f));
                deck1row2lowwall5 = SpawnPart(prefablowwall, deck1row2lowwall5, Vector3.zero, new Vector3(-10.5f + ShiftOffset, LDHeightOffSet, -6f));

                deck1row3lowwall1 = SpawnPart(prefablowwall, deck1row3lowwall1, Vector3_90, new Vector3(6f + ShiftOffset, LDHeightOffSet, -10.5f));
                deck1row3lowwall2 = SpawnPart(prefablowwall, deck1row3lowwall2, Vector3_90, new Vector3(3f + ShiftOffset, LDHeightOffSet, -10.5f));
                deck1row3lowwall3 = SpawnPart(prefablowwall, deck1row3lowwall3, Vector3_90, new Vector3(0f + ShiftOffset, LDHeightOffSet, -10.5f));
                deck1row3lowwall4 = SpawnPart(prefablowwall, deck1row3lowwall4, Vector3_90, new Vector3(-3f + ShiftOffset, LDHeightOffSet, -10.5f));
                deck1row3lowwall5 = SpawnPart(prefablowwall, deck1row3lowwall5, Vector3_90, new Vector3(-6f + ShiftOffset, LDHeightOffSet, -10.5f));

                deck1row4lowwall1 = SpawnPart(prefablowwall, deck1row4lowwall1, Vector3_270, new Vector3(6f + ShiftOffset, LDHeightOffSet, 10.5f));
                deck1row4lowwall2 = SpawnPart(prefablowwall, deck1row4lowwall2, Vector3_270, new Vector3(3f + ShiftOffset, LDHeightOffSet, 10.5f));
                deck1row4lowwall3 = SpawnPart(prefablowwall, deck1row4lowwall3, Vector3_270, new Vector3(0f + ShiftOffset, LDHeightOffSet, 10.5f));
                deck1row4lowwall4 = SpawnPart(prefablowwall, deck1row4lowwall4, Vector3_270, new Vector3(-3f + ShiftOffset, LDHeightOffSet, 10.5f));
                deck1row4lowwall5 = SpawnPart(prefablowwall, deck1row4lowwall5, Vector3_270, new Vector3(-6f + ShiftOffset, LDHeightOffSet, 10.5f));
            }

            void SpawnLowerDeckGrid()
            {
                deck1row1floorframe1 = SpawnPart(prefabfloorframe, deck1row1floorframe1, Vector3.zero, new Vector3(6f + ShiftOffset, LDHeightOffSet, 6f));
                deck1row1floorframe2 = SpawnPart(prefabfloorframe, deck1row1floorframe2, Vector3.zero, new Vector3(6f + ShiftOffset, LDHeightOffSet, 3f));
                deck1row1floorframe3 = SpawnPart(prefabfloorframe, deck1row1floorframe3, Vector3.zero, new Vector3(6f + ShiftOffset, LDHeightOffSet, 0f));
                deck1row1floorframe4 = SpawnPart(prefabfloorframe, deck1row1floorframe4, Vector3.zero, new Vector3(6f + ShiftOffset, LDHeightOffSet, -3f));
                deck1row1floorframe5 = SpawnPart(prefabfloorframe, deck1row1floorframe5, Vector3.zero, new Vector3(6f + ShiftOffset, LDHeightOffSet, -6f));

                deck1row2floorframe1 = SpawnPart(prefabfloorframe, deck1row2floorframe1, Vector3.zero, new Vector3(-6f + ShiftOffset, LDHeightOffSet, 6f));
                deck1row2floorframe2 = SpawnPart(prefabfloorframe, deck1row2floorframe2, Vector3.zero, new Vector3(-6f + ShiftOffset, LDHeightOffSet, 3f));
                //deck1row2floorframe3 = SpawnPart(prefabfloorframe, deck1row2floorframe3, Vector3.zero, new Vector3(-6f + ShiftOffset, LDHeightOffSet, 0f));
                deck1row2floorframe4 = SpawnPart(prefabfloorframe, deck1row2floorframe4, Vector3.zero, new Vector3(-6f + ShiftOffset, LDHeightOffSet, -3f));
                deck1row2floorframe5 = SpawnPart(prefabfloorframe, deck1row2floorframe5, Vector3.zero, new Vector3(-6f + ShiftOffset, LDHeightOffSet, -6f));

                deck1row3floorframe1 = SpawnPart(prefabfloorframe, deck1row3floorframe1, Vector3.zero, new Vector3(3f + ShiftOffset, LDHeightOffSet, -6f));
                deck1row3floorframe2 = SpawnPart(prefabfloorframe, deck1row3floorframe2, Vector3.zero, new Vector3(0f + ShiftOffset, LDHeightOffSet, -6f));
                deck1row3floorframe3 = SpawnPart(prefabfloorframe, deck1row3floorframe3, Vector3.zero, new Vector3(-3f + ShiftOffset, LDHeightOffSet, -6f));

                deck1row4floorframe1 = SpawnPart(prefabfloorframe, deck1row4floorframe1, Vector3.zero, new Vector3(3f + ShiftOffset, LDHeightOffSet, 6f));
                deck1row4floorframe2 = SpawnPart(prefabfloorframe, deck1row4floorframe2, Vector3.zero, new Vector3(0f + ShiftOffset, LDHeightOffSet, 6f));
                deck1row4floorframe3 = SpawnPart(prefabfloorframe, deck1row4floorframe3, Vector3.zero, new Vector3(-3f + ShiftOffset, LDHeightOffSet, 6f));
            }


            void SpawnUpperDeck()
            {

                deck2frontrowfloor1 = SpawnPart(prefabfloor, deck2frontrowfloor1, Vector3.zero, new Vector3(9f + ShiftOffset, UDHeightOffset, 6f));
                deck2frontrowfloor2 = SpawnPart(prefabfloor, deck2frontrowfloor2, Vector3.zero, new Vector3(9f + ShiftOffset, UDHeightOffset, 3f));
                deck2frontrowfloor3 = SpawnPart(prefabfloor, deck2frontrowfloor3, Vector3.zero, new Vector3(9f + ShiftOffset, UDHeightOffset, 0f));
                deck2frontrowfloor4 = SpawnPart(prefabfloor, deck2frontrowfloor4, Vector3.zero, new Vector3(9f + ShiftOffset, UDHeightOffset, -3f));
                deck2frontrowfloor5 = SpawnPart(prefabfloor, deck2frontrowfloor5, Vector3.zero, new Vector3(9f + ShiftOffset, UDHeightOffset, -6f));

                deck2row1floor1 = SpawnPart(prefabfloor, deck2row1floor1, Vector3.zero, new Vector3(6f + ShiftOffset, UDHeightOffset, 6f));
                deck2row1floor2 = SpawnPart(prefabfloor, deck2row1floor2, Vector3.zero, new Vector3(6f + ShiftOffset, UDHeightOffset, 3f));
                deck2row1floor3 = SpawnPart(prefabfloor, deck2row1floor3, Vector3.zero, new Vector3(6f + ShiftOffset, UDHeightOffset, 0f));
                deck2row1floor4 = SpawnPart(prefabfloor, deck2row1floor4, Vector3.zero, new Vector3(6f + ShiftOffset, UDHeightOffset, -3f));
                deck2row1floor5 = SpawnPart(prefabfloor, deck2row1floor5, Vector3.zero, new Vector3(6f + ShiftOffset, UDHeightOffset, -6f));

                deck2row2floor1 = SpawnPart(prefabfloor, deck2row2floor1, Vector3.zero, new Vector3(-6f + ShiftOffset, UDHeightOffset, 6f));
                deck2row2floor2 = SpawnPart(prefabfloor, deck2row2floor2, Vector3.zero, new Vector3(-6f + ShiftOffset, UDHeightOffset, 3f));
                deck2row2floor3 = SpawnPart(prefabfloor, deck2row2floor3, Vector3.zero, new Vector3(-6f + ShiftOffset, UDHeightOffset, 0f));
                deck2row2floor4 = SpawnPart(prefabfloor, deck2row2floor4, Vector3.zero, new Vector3(-6f + ShiftOffset, UDHeightOffset, -3f));
                deck2row2floor5 = SpawnPart(prefabfloor, deck2row2floor5, Vector3.zero, new Vector3(-6f + ShiftOffset, UDHeightOffset, -6f));

                deck2row3floor1 = SpawnPart(prefabfloor, deck2row3floor1, Vector3.zero, new Vector3(3f + ShiftOffset, UDHeightOffset, -6f));
                deck2row3floor2 = SpawnPart(prefabfloor, deck2row3floor2, Vector3.zero, new Vector3(0f + ShiftOffset, UDHeightOffset, -6f));
                deck2row3floor3 = SpawnPart(prefabfloor, deck2row3floor3, Vector3.zero, new Vector3(-3f + ShiftOffset, UDHeightOffset, -6f));

                deck2row4floor1 = SpawnPart(prefabfloor, deck2row4floor1, Vector3.zero, new Vector3(3f + ShiftOffset, UDHeightOffset, 6f));
                deck2row4floor2 = SpawnPart(prefabfloor, deck2row4floor2, Vector3.zero, new Vector3(0f + ShiftOffset, UDHeightOffset, 6f));
                deck2row4floor3 = SpawnPart(prefabfloor, deck2row4floor3, Vector3.zero, new Vector3(-3f + ShiftOffset, UDHeightOffset, 6f));

                deck2center1 = SpawnPart(prefabfloor, deck2center1, Vector3.zero, new Vector3(3f + ShiftOffset, UDHeightOffset, 3f));
                deck2center2 = SpawnPart(prefabfloor, deck2center2, Vector3.zero, new Vector3(0f + ShiftOffset, UDHeightOffset, 3f));
                deck2center3 = SpawnPart(prefabfloor, deck2center3, Vector3.zero, new Vector3(-3f + ShiftOffset, UDHeightOffset, 3f));
                deck2center4 = SpawnPart(prefabfloor, deck2center4, Vector3.zero, new Vector3(3f + ShiftOffset, UDHeightOffset, 0f));
                deck2center5 = SpawnPart(prefabfloor, deck2center5, Vector3.zero, new Vector3(0f + ShiftOffset, UDHeightOffset, 0f));
                deck2center6 = SpawnPart(prefabfloor, deck2center6, Vector3.zero, new Vector3(-3f + ShiftOffset, UDHeightOffset, 0f));
                deck2center7 = SpawnPart(prefabfloor, deck2center7, Vector3.zero, new Vector3(3f + ShiftOffset, UDHeightOffset, -3f));
                deck2center8 = SpawnPart(prefabfloor, deck2center8, Vector3.zero, new Vector3(0f + ShiftOffset, UDHeightOffset, -3f));
                deck2center9 = SpawnPart(prefabfloor, deck2center9, Vector3.zero, new Vector3(-3f + ShiftOffset, UDHeightOffset, -3f));

                deck2backrowfloor1 = SpawnPart(prefabfloor, deck2backrowfloor1, Vector3.zero, new Vector3(-9f + ShiftOffset, UDHeightOffset, 6f));
                deck2backrowfloor2 = SpawnPart(prefabfloor, deck2backrowfloor2, Vector3.zero, new Vector3(-9f + ShiftOffset, UDHeightOffset, 3f));
                deck2backrowfloor3 = SpawnPart(prefabfloor, deck2backrowfloor3, Vector3.zero, new Vector3(-9f + ShiftOffset, UDHeightOffset, 0f));
                deck2backrowfloor4 = SpawnPart(prefabfloor, deck2backrowfloor4, Vector3.zero, new Vector3(-9f + ShiftOffset, UDHeightOffset, -3f));
                deck2backrowfloor5 = SpawnPart(prefabfloor, deck2backrowfloor5, Vector3.zero, new Vector3(-9f + ShiftOffset, UDHeightOffset, -6f)); ;

                deck2row2lowwall1 = SpawnPart(prefablowwall, deck2row2lowwall1, Vector3.zero, new Vector3(-10.5f + ShiftOffset, UDHeightOffset, 6f));
                deck2row2lowwall2 = SpawnPart(prefablowwall, deck2row2lowwall2, Vector3.zero, new Vector3(-10.5f + ShiftOffset, UDHeightOffset, 3f));
                deck2row2lowwall3 = SpawnPart(prefablowwall, deck2row2lowwall3, Vector3.zero, new Vector3(-10.5f + ShiftOffset, UDHeightOffset, 0f));
                deck2row2lowwall4 = SpawnPart(prefablowwall, deck2row2lowwall4, Vector3.zero, new Vector3(-10.5f + ShiftOffset, UDHeightOffset, -3f));
                deck2row2lowwall5 = SpawnPart(prefablowwall, deck2row2lowwall5, Vector3.zero, new Vector3(-10.5f + ShiftOffset, UDHeightOffset, -6f));

                deck2row3lowwall1 = SpawnPart(prefablowwall, deck2row3lowwall1, Vector3_90, new Vector3(6f + ShiftOffset, UDHeightOffset, -7.5f));
                deck2row3lowwall2 = SpawnPart(prefablowwall, deck2row3lowwall2, Vector3_90, new Vector3(3f + ShiftOffset, UDHeightOffset, -7.5f));
                deck2row3lowwall3 = SpawnPart(prefabstairsl, deck2row3lowwall3, Vector3_90, new Vector3(0f + ShiftOffset, UDHeightOffset - 3f, -9f));
                deck2row3lowwall3U = SpawnPart(prefabstairsl, deck2row3lowwall3U, Vector3_90, new Vector3(-3.5f + ShiftOffset, UDHeightOffset, -6f));
                deck2row3lowwall4 = SpawnPart(prefablowwall, deck2row3lowwall4, Vector3_90, new Vector3(-3f + ShiftOffset, UDHeightOffset, -7.5f));
                deck2row3lowwall5 = SpawnPart(prefablowwall, deck2row3lowwall5, Vector3_90, new Vector3(-6f + ShiftOffset, UDHeightOffset, -7.5f));

                deck2row4lowwall1 = SpawnPart(prefablowwall, deck2row4lowwall1, Vector3_270, new Vector3(6f + ShiftOffset, UDHeightOffset, 7.5f));
                deck2row4lowwall2 = SpawnPart(prefablowwall, deck2row4lowwall2, Vector3_270, new Vector3(3f + ShiftOffset, UDHeightOffset, 7.5f));
                deck2row4lowwall3 = SpawnPart(prefabstairsl, deck2row4lowwall3, Vector3_270, new Vector3(0f + ShiftOffset, UDHeightOffset - 3f, 9f));
                deck2row4lowwall3U = SpawnPart(prefabstairsl, deck2row4lowwall3U, Vector3.zero, new Vector3(0f + ShiftOffset, UDHeightOffset, 4.7f));
                deck2row4lowwall4 = SpawnPart(prefablowwall, deck2row4lowwall4, Vector3_270, new Vector3(-3f + ShiftOffset, UDHeightOffset, 7.5f));
                deck2row4lowwall5 = SpawnPart(prefablowwall, deck2row4lowwall5, Vector3_270, new Vector3(-6f + ShiftOffset, UDHeightOffset, 7.5f));
            }

            void SpawnHelipad()
            {
                helipadfloor1 = SpawnPart(prefabfloor, helipadfloor1, Vector3.zero, new Vector3(12f + ShiftOffset, UDHeightOffset, 6f));
                helipadfloor2 = SpawnPart(prefabfloor, helipadfloor2, Vector3.zero, new Vector3(12f + ShiftOffset, UDHeightOffset, 3f));
                helipadfloor3 = SpawnPart(prefabfloor, helipadfloor3, Vector3.zero, new Vector3(12f + ShiftOffset, UDHeightOffset, 0f));
                helipadfloor4 = SpawnPart(prefabfloor, helipadfloor4, Vector3.zero, new Vector3(12f + ShiftOffset, UDHeightOffset, -3f));
                helipadfloor5 = SpawnPart(prefabfloor, helipadfloor5, Vector3.zero, new Vector3(12f + ShiftOffset, UDHeightOffset, -6f));

                helipadfloor6 = SpawnPart(prefabfloor, helipadfloor6, Vector3.zero, new Vector3(15f + ShiftOffset, UDHeightOffset, 6f));
                helipadfloor7 = SpawnPart(prefabfloor, helipadfloor7, Vector3.zero, new Vector3(15f + ShiftOffset, UDHeightOffset, 3f));
                helipadfloor8 = SpawnPart(prefabfloor, helipadfloor8, Vector3.zero, new Vector3(15f + ShiftOffset, UDHeightOffset, 0f));
                helipadfloor9 = SpawnPart(prefabfloor, helipadfloor9, Vector3.zero, new Vector3(15f + ShiftOffset, UDHeightOffset, -3f));
                helipadfloor10 = SpawnPart(prefabfloor, helipadfloor10, Vector3.zero, new Vector3(15f + ShiftOffset, UDHeightOffset, -6f));

                helipadsupportframe1 = SpawnPart(prefabwallframe, helipadsupportframe1, new Vector3(0, 0, 315), new Vector3(11.1f + ShiftOffset, UDHeightOffset - 2.15f, 6f));
                helipadsupportframe2 = SpawnPart(prefabwallframe, helipadsupportframe2, new Vector3(0, 0, 315), new Vector3(11.1f + ShiftOffset, UDHeightOffset - 2.15f, 3f));
                helipadsupportframe3 = SpawnPart(prefabwallframe, helipadsupportframe3, new Vector3(0, 0, 315), new Vector3(11.1f + ShiftOffset, UDHeightOffset - 2.15f, 0f));
                helipadsupportframe4 = SpawnPart(prefabwallframe, helipadsupportframe4, new Vector3(0, 0, 315), new Vector3(11.1f + ShiftOffset, UDHeightOffset - 2.15f, -3f));
                helipadsupportframe5 = SpawnPart(prefabwallframe, helipadsupportframe5, new Vector3(0, 0, 315), new Vector3(11.1f + ShiftOffset, UDHeightOffset - 2.15f, -6f));

                helipadsupport1 = SpawnPart(prefablowwall, helipadsupport1, new Vector3(0, 180, 45), new Vector3(10.4f + ShiftOffset, LDHeightOffSet + 0.1f, 6f));
                helipadsupport2 = SpawnPart(prefablowwall, helipadsupport2, new Vector3(0, 180, 45), new Vector3(10.4f + ShiftOffset, LDHeightOffSet + 0.1f, 3f));
                helipadsupport3 = SpawnPart(prefablowwall, helipadsupport3, new Vector3(0, 180, 45), new Vector3(10.4f + ShiftOffset, LDHeightOffSet + 0.1f, 0f));
                helipadsupport4 = SpawnPart(prefablowwall, helipadsupport4, new Vector3(0, 180, 45), new Vector3(10.4f + ShiftOffset, LDHeightOffSet + 0.1f, -3f));
                helipadsupport5 = SpawnPart(prefablowwall, helipadsupport5, new Vector3(0, 180, 45), new Vector3(10.4f + ShiftOffset, LDHeightOffSet + 0.1f, -6f));

                helipadfence1 = SpawnPart(prefabfence, helipadfence1, new Vector3(0, 0, 315), new Vector3(11.1f + ShiftOffset, UDHeightOffset - 2.15f, 6f));
                helipadfence2 = SpawnPart(prefabfence, helipadfence2, new Vector3(0, 0, 315), new Vector3(11.1f + ShiftOffset, UDHeightOffset - 2.15f, 3f));
                helipadfence3 = SpawnPart(prefabfence, helipadfence3, new Vector3(0, 0, 315), new Vector3(11.1f + ShiftOffset, UDHeightOffset - 2.15f, 0f));
                helipadfence4 = SpawnPart(prefabfence, helipadfence4, new Vector3(0, 0, 315), new Vector3(11.1f + ShiftOffset, UDHeightOffset - 2.15f, -3f));
                helipadfence5 = SpawnPart(prefabfence, helipadfence5, new Vector3(0, 0, 315), new Vector3(11.1f + ShiftOffset, UDHeightOffset - 2.15f, -6f));

                helilogo1 = SpawnPart(prefabrug, helilogo1, new Vector3(0, 0, 180), new Vector3(12.65f + ShiftOffset, UDHeightOffset + 0.2f, 0f), HeliPadRugLogo1);
                helilogo2 = SpawnPart(prefabrug, helilogo2, Vector3.zero, new Vector3(14.35f + ShiftOffset, UDHeightOffset + 0.1f, 0f), HeliPadRugLogo1);

            }

            void SpawnLowerLights()
            {
                lowerlight1 = SpawnPart(prefabceilinglight, lowerlight1, Vector3.zero, new Vector3(9f + ShiftOffset, LDHeightOffSet, 6f));
                lowerlight2 = SpawnPart(prefabceilinglight, lowerlight2, Vector3.zero, new Vector3(9f + ShiftOffset, LDHeightOffSet, -6f));
                lowerlight3 = SpawnPart(prefabceilinglight, lowerlight3, Vector3.zero, new Vector3(-9f + ShiftOffset, LDHeightOffSet, 6f));
                lowerlight4 = SpawnPart(prefabceilinglight, lowerlight4, Vector3.zero, new Vector3(-9f + ShiftOffset, LDHeightOffSet, -6f));
                lowerlight5 = SpawnPart(prefabceilinglight, lowerlight5, Vector3.zero, new Vector3(6f + ShiftOffset, LDHeightOffSet, 9f));
                lowerlight6 = SpawnPart(prefabceilinglight, lowerlight6, Vector3.zero, new Vector3(6f + ShiftOffset, LDHeightOffSet, -9f));
                lowerlight7 = SpawnPart(prefabceilinglight, lowerlight7, Vector3.zero, new Vector3(-6f + ShiftOffset, LDHeightOffSet, 9f));
                lowerlight8 = SpawnPart(prefabceilinglight, lowerlight8, Vector3.zero, new Vector3(-6f + ShiftOffset, LDHeightOffSet, -9f));
                lowerlight1.SetFlag(BaseEntity.Flags.On, true, true);
                lowerlight2.SetFlag(BaseEntity.Flags.On, true, true);
                lowerlight3.SetFlag(BaseEntity.Flags.On, true, true);
                lowerlight4.SetFlag(BaseEntity.Flags.On, true, true);
                lowerlight5.SetFlag(BaseEntity.Flags.On, true, true);
                lowerlight6.SetFlag(BaseEntity.Flags.On, true, true);
                lowerlight7.SetFlag(BaseEntity.Flags.On, true, true);
                lowerlight8.SetFlag(BaseEntity.Flags.On, true, true);
                lowerlight1.SetFlag(BaseEntity.Flags.Busy, true, true);
                lowerlight2.SetFlag(BaseEntity.Flags.Busy, true, true);
                lowerlight3.SetFlag(BaseEntity.Flags.Busy, true, true);
                lowerlight4.SetFlag(BaseEntity.Flags.Busy, true, true);
                lowerlight5.SetFlag(BaseEntity.Flags.Busy, true, true);
                lowerlight6.SetFlag(BaseEntity.Flags.Busy, true, true);
                lowerlight7.SetFlag(BaseEntity.Flags.Busy, true, true);
                lowerlight8.SetFlag(BaseEntity.Flags.Busy, true, true);
            }

            void SpawnUpperLights()
            {
                upperlight1 = SpawnPart(prefabceilinglight, upperlight1, Vector3.zero, new Vector3(-6f + ShiftOffset, UDHeightOffset, 6f));
                upperlight2 = SpawnPart(prefabceilinglight, upperlight2, Vector3.zero, new Vector3(-6f + ShiftOffset, UDHeightOffset, -6f));
                upperlight3 = SpawnPart(prefabceilinglight, upperlight3, Vector3.zero, new Vector3(6f + ShiftOffset, UDHeightOffset, -6f));
                upperlight4 = SpawnPart(prefabceilinglight, upperlight4, Vector3.zero, new Vector3(6f + ShiftOffset, UDHeightOffset, 6f));

                upperlight5 = SpawnPart(prefabceilinglight, upperlight5, Vector3.zero, new Vector3(0f + ShiftOffset, UDHeightOffset, -6f));
                upperlight6 = SpawnPart(prefabceilinglight, upperlight6, Vector3.zero, new Vector3(0f + ShiftOffset, UDHeightOffset, 6f));
                upperlight7 = SpawnPart(prefabceilinglight, upperlight7, Vector3.zero, new Vector3(6f + ShiftOffset, UDHeightOffset, 0f));
                upperlight1.SetFlag(BaseEntity.Flags.On, true, true);
                upperlight2.SetFlag(BaseEntity.Flags.On, true, true);
                upperlight3.SetFlag(BaseEntity.Flags.On, true, true);
                upperlight4.SetFlag(BaseEntity.Flags.On, true, true);
                upperlight5.SetFlag(BaseEntity.Flags.On, true, true);
                upperlight6.SetFlag(BaseEntity.Flags.On, true, true);
                upperlight7.SetFlag(BaseEntity.Flags.On, true, true);
                upperlight1.SetFlag(BaseEntity.Flags.Busy, true, true);
                upperlight2.SetFlag(BaseEntity.Flags.Busy, true, true);
                upperlight3.SetFlag(BaseEntity.Flags.Busy, true, true);
                upperlight4.SetFlag(BaseEntity.Flags.Busy, true, true);
                upperlight5.SetFlag(BaseEntity.Flags.Busy, true, true);
                upperlight6.SetFlag(BaseEntity.Flags.Busy, true, true);
                upperlight7.SetFlag(BaseEntity.Flags.Busy, true, true);
            }

            void SpawnLift()
            {
                if (BucketLift == null)
                {
                    if (isevent && !EventRigHasLiftBucket) return;
                    if (!isevent && !StandardRigHasLiftBucket) return;
                    BucketLift = SpawnPart("assets/content/structures/lift_shaft/cave_lift.prefab", BucketLift, Vector3.zero, new Vector3(-6f + ShiftOffset, -16f, 0f));
                }
            }

            void SpawnLadders()
            {
                if (isevent && !EventRigHasLadders) return;
                if (!isevent && !StandardRigHasLadders) return;

                ladder6leg1 = SpawnPart(prefabladder, ladder6leg1, Vector3.zero, new Vector3(9f + ShiftOffset, F5HeightOffset, 10.7f));
                ladder5leg1 = SpawnPart(prefabladder, ladder5leg1, Vector3.zero, new Vector3(9f + ShiftOffset, F4HeightOffset, 10.7f));
                ladder1leg1 = SpawnPart(prefabladder, ladder1leg1, Vector3.zero, new Vector3(9f + ShiftOffset, F1HeightOffset, 10.7f));
                ladder2leg1 = SpawnPart(prefabladder, ladder2leg1, Vector3.zero, new Vector3(9f + ShiftOffset, F2HeightOffset, 10.7f));
                ladder3leg1 = SpawnPart(prefabladder, ladder3leg1, Vector3.zero, new Vector3(9f + ShiftOffset, F3HeightOffset, 10.7f));
                ladder4leg1 = SpawnPart(prefabladder, ladder4leg1, Vector3.zero, new Vector3(9f + ShiftOffset, F3HeightOffset + 1.5f, 10.7f));

                ladder6leg2 = SpawnPart(prefabladder, ladder6leg2, Vector3.zero, new Vector3(-9f + ShiftOffset, F5HeightOffset, 10.7f));
                ladder5leg2 = SpawnPart(prefabladder, ladder5leg2, Vector3.zero, new Vector3(-9f + ShiftOffset, F4HeightOffset, 10.7f));
                ladder1leg2 = SpawnPart(prefabladder, ladder1leg2, Vector3.zero, new Vector3(-9f + ShiftOffset, F1HeightOffset, 10.7f));
                ladder2leg2 = SpawnPart(prefabladder, ladder2leg2, Vector3.zero, new Vector3(-9f + ShiftOffset, F2HeightOffset, 10.7f));
                ladder3leg2 = SpawnPart(prefabladder, ladder3leg2, Vector3.zero, new Vector3(-9f + ShiftOffset, F3HeightOffset, 10.7f));
                ladder4leg2 = SpawnPart(prefabladder, ladder4leg2, Vector3.zero, new Vector3(-9f + ShiftOffset, F3HeightOffset + 1.5f, 10.7f));

                ladder6leg3 = SpawnPart(prefabladder, ladder6leg3, Vector3.zero, new Vector3(9f + ShiftOffset, F5HeightOffset, -10.7f));
                ladder5leg3 = SpawnPart(prefabladder, ladder5leg3, Vector3.zero, new Vector3(9f + ShiftOffset, F4HeightOffset, -10.7f));
                ladder1leg3 = SpawnPart(prefabladder, ladder1leg3, Vector3.zero, new Vector3(9f + ShiftOffset, F1HeightOffset, -10.7f));
                ladder2leg3 = SpawnPart(prefabladder, ladder2leg3, Vector3.zero, new Vector3(9f + ShiftOffset, F2HeightOffset, -10.7f));
                ladder3leg3 = SpawnPart(prefabladder, ladder3leg3, Vector3.zero, new Vector3(9f + ShiftOffset, F3HeightOffset, -10.7f));
                ladder4leg3 = SpawnPart(prefabladder, ladder4leg3, Vector3.zero, new Vector3(9f + ShiftOffset, F3HeightOffset + 1.5f, -10.7f));

                ladder6leg4 = SpawnPart(prefabladder, ladder6leg4, Vector3.zero, new Vector3(-9f + ShiftOffset, F5HeightOffset, -10.7f));
                ladder5leg4 = SpawnPart(prefabladder, ladder5leg4, Vector3.zero, new Vector3(-9f + ShiftOffset, F4HeightOffset, -10.7f));
                ladder1leg4 = SpawnPart(prefabladder, ladder1leg4, Vector3.zero, new Vector3(-9f + ShiftOffset, F1HeightOffset, -10.7f));
                ladder2leg4 = SpawnPart(prefabladder, ladder2leg4, Vector3.zero, new Vector3(-9f + ShiftOffset, F2HeightOffset, -10.7f));
                ladder3leg4 = SpawnPart(prefabladder, ladder3leg4, Vector3.zero, new Vector3(-9f + ShiftOffset, F3HeightOffset, -10.7f));
                ladder4leg4 = SpawnPart(prefabladder, ladder4leg4, Vector3.zero, new Vector3(-9f + ShiftOffset, F3HeightOffset + 1.5f, -10.7f));
            }

            void SpawnCompound()
            {
                iscompound = false;

                windmill = SpawnPart(prefabwindmill, windmill, Vector3_270, new Vector3(-7f, 1f, 0f));
                vendattire = SpawnPart(prefabattirevending, vendattire, Vector3.zero, new Vector3(3f + ShiftOffset, LDHeightOffSet, 5f));
                vendbuilding = SpawnPart(prefabbuildingvending, vendbuilding, Vector3.zero, new Vector3(0f + ShiftOffset, LDHeightOffSet, 5f));
                vendcomponents = SpawnPart(prefabcomponentsvending, vendcomponents, Vector3.zero, new Vector3(-3f + ShiftOffset, LDHeightOffSet, 5f));
                vendresources = SpawnPart(prefabresourcesvending, vendresources, Vector3_180, new Vector3(3f + ShiftOffset, LDHeightOffSet, -5f));
                vendtools = SpawnPart(prefabtoolsvending, vendtools, Vector3_180, new Vector3(0f + ShiftOffset, LDHeightOffSet, -5f));
                vendweapons = SpawnPart(prefabweaponsvending, vendweapons, Vector3_180, new Vector3(-3f + ShiftOffset, LDHeightOffSet, -5f));

                recycler1 = SpawnPart(prefabrecycler, recycler1, Vector3_90, new Vector3(6f + ShiftOffset, LDHeightOffSet, -3f));
                recycler2 = SpawnPart(prefabrecycler, recycler2, Vector3_270, new Vector3(-6f + ShiftOffset, LDHeightOffSet, 3f));
                waterwell = SpawnPart(prefabwaterwell, waterwell, Vector3_90, new Vector3(6f + ShiftOffset, LDHeightOffSet, 0f));
                researchtable = SpawnPart(prefabresearchtable, researchtable, Vector3_270, new Vector3(-6f + ShiftOffset, LDHeightOffSet, -3f));
                workbench = SpawnPart(prefabworkbench, workbench, Vector3_90, new Vector3(6f + ShiftOffset, LDHeightOffSet, 3f));

                if (UseSentryOnCompound)
                {
                    peacekeeper1 = SpawnPart(prefabpeacekeeper, peacekeeper1, Vector3.zero, new Vector3(15f + ShiftOffset, UDHeightOffset, 6f));
                    peacekeeper2 = SpawnPart(prefabpeacekeeper, peacekeeper2, Vector3_180, new Vector3(-9f + ShiftOffset, UDHeightOffset, -6f));
                    peacekeeper3 = SpawnPart(prefabpeacekeeper, peacekeeper3, Vector3.zero, new Vector3(-9f + ShiftOffset, LDHeightOffSet, 9f));
                    peacekeeper4 = SpawnPart(prefabpeacekeeper, peacekeeper4, Vector3.zero, new Vector3(9f + ShiftOffset, LDHeightOffSet, -9f));
                }

                if (waterwell != null)                
                    ItemManager.CreateByItemID(-1779180711, 6000).MoveToContainer((waterwell as WaterWell).inventory);
            }

            void DeepWaterStage1()
            {
                helicopter = GameManager.server.CreateEntity(prefabheli, entity.transform.position + new Vector3(12f + ShiftOffset, UDHeightOffset + 20f, 2f), new Quaternion(), true);
                var getai = helicopter.GetComponent<CH47HelicopterAIController>() ?? null;
                if (getai != null)
                {
                    getai.enabled = false;
                }
                helicopter?.Spawn();
                
                RefreshAll();
            }

            void DeepWaterStage2()
            {
                if (helicopter == null) { stage3 = true; return; }
                var getai = helicopter.GetComponent<CH47HelicopterAIController>() ?? null;
                if (getai == null) return;
                getai.numCrates = 0;
                HitInfo info = new HitInfo();
                getai.OnKilled(info);
                if (helipadfloor3 != null) helipadfloor3.Invoke("KillMessage", 0.1f);
                stage3 = true;

                if (EventRigHasHackCrate)
                {
                    helicrate = GameManager.server.CreateEntity(getai.lockedCratePrefab.resourcePath, deck1row1floor4.transform.position + Vector3.up, Quaternion.identity, true);
                    if (helicrate)
                    {
                        Interface.CallHook("OnHelicopterDropCrate", getai);
                        helicrate.SendMessage("SetWasDropped");
                        HackableLockedCrate.requiredHackSeconds = Stage2HackDuration;
                        helicrate.Spawn();
                        if (AddFullDivingKit) AddWetSuitKit(helicrate);
                    }
                }
                RefreshAll();
            }

            void AddWetSuitKit(BaseEntity entity)
            {
                ItemContainer hackcrateinv = entity.GetComponent<StorageContainer>().inventory;
                if (hackcrateinv == null) return;

                Item addsuit = ItemManager.CreateByItemID(-1101924344, 1);
                hackcrateinv.itemList.Add(addsuit);
                addsuit.parent = hackcrateinv;
                addsuit.MarkDirty();

                Item addmask = ItemManager.CreateByItemID(-113413047, 1);
                hackcrateinv.itemList.Add(addmask);
                addmask.parent = hackcrateinv;
                addmask.MarkDirty();

                Item addfins = ItemManager.CreateByItemID(296519935, 1);
                hackcrateinv.itemList.Add(addfins);
                addfins.parent = hackcrateinv;
                addfins.MarkDirty();

                Item addtank = ItemManager.CreateByItemID(-2022172587, 1);
                hackcrateinv.itemList.Add(addtank);
                addtank.parent = hackcrateinv;
                addtank.MarkDirty();
            }

            void SpawnRadBarrels()
            {
                if (isevent && !EventRigHasBarrels) 
                    return;

                if (!isevent && !StandardRigHasBarrels) 
                    return;

                if (iscompound) return;

                explosivecrate1 = SpawnPart(prefabbarrel, explosivecrate1, Vector3.zero, new Vector3(6f + ShiftOffset, UDHeightOffset + 0.1f, -7f), 2297672356);
                explosivecrate2 = SpawnPart(prefabbarrel, explosivecrate2, Vector3.zero, new Vector3(3f + ShiftOffset, LDHeightOffSet + 0.1f, -9f), 2297672356);
                explosivecrate3 = SpawnPart(prefabbarrel, explosivecrate3, Vector3.zero, new Vector3(-1f + ShiftOffset, UDHeightOffset + 0.1f, -5.5f), 2297672356);
                explosivecrate4 = SpawnPart(prefabbarrel, explosivecrate4, Vector3.zero, new Vector3(-3f + ShiftOffset, LDHeightOffSet + 0.1f, -9f), 2297672356);
                explosivecrate5 = SpawnPart(prefabbarrel, explosivecrate5, Vector3.zero, new Vector3(-9f + ShiftOffset, LDHeightOffSet + 0.1f, -6f), 2297672356);
                explosivecrate6 = SpawnPart(prefabbarrel, explosivecrate6, Vector3.zero, new Vector3(6f + ShiftOffset, UDHeightOffset + 0.1f, 7f), 2297672356);
                explosivecrate7 = SpawnPart(prefabbarrel, explosivecrate7, Vector3.zero, new Vector3(3f + ShiftOffset, LDHeightOffSet + 0.1f, 9f), 2297672356);
                explosivecrate8 = SpawnPart(prefabbarrel, explosivecrate8, Vector3.zero, new Vector3(2f + ShiftOffset, UDHeightOffset + 0.1f, 4.5f), 2297672356);
                explosivecrate9 = SpawnPart(prefabbarrel, explosivecrate9, Vector3.zero, new Vector3(-3f + ShiftOffset, LDHeightOffSet + 0.1f, 9f), 2297672356);
                explosivecrate10 = SpawnPart(prefabbarrel, explosivecrate10, Vector3.zero, new Vector3(-9f + ShiftOffset, LDHeightOffSet + 0.1f, 6f), 2297672356);

                explosivecrate1.SetFlag(BaseEntity.Flags.Busy, true, true);
                explosivecrate2.SetFlag(BaseEntity.Flags.Busy, true, true);
                explosivecrate3.SetFlag(BaseEntity.Flags.Busy, true, true);
                explosivecrate4.SetFlag(BaseEntity.Flags.Busy, true, true);
                explosivecrate5.SetFlag(BaseEntity.Flags.Busy, true, true);
                explosivecrate6.SetFlag(BaseEntity.Flags.Busy, true, true);
                explosivecrate7.SetFlag(BaseEntity.Flags.Busy, true, true);
                explosivecrate8.SetFlag(BaseEntity.Flags.Busy, true, true);
                explosivecrate9.SetFlag(BaseEntity.Flags.Busy, true, true);
                explosivecrate10.SetFlag(BaseEntity.Flags.Busy, true, true);
            }

            bool HasBarrels()
            {
                var barrelcount = 0;
                if (explosivecrate1) barrelcount = barrelcount + 1;
                if (explosivecrate2) barrelcount = barrelcount + 1;
                if (explosivecrate3) barrelcount = barrelcount + 1;
                if (explosivecrate4) barrelcount = barrelcount + 1;
                if (explosivecrate5) barrelcount = barrelcount + 1;
                if (explosivecrate6) barrelcount = barrelcount + 1;
                if (explosivecrate7) barrelcount = barrelcount + 1;
                if (explosivecrate8) barrelcount = barrelcount + 1;
                if (explosivecrate9) barrelcount = barrelcount + 1;
                if (explosivecrate10) barrelcount = barrelcount + 1;
                if (barrelcount > 0) return true;
                else return false;
            }

            void DeepWaterStage3()
            {
                if (HasBarrels())
                {
                    int choosebarrel = UnityEngine.Random.Range(1, 11);
                    if ((choosebarrel == 1) && (explosivecrate1 != null)) { DoBarrelExplosion(explosivecrate1); return; }
                    if ((choosebarrel == 2) && (explosivecrate2 != null)) { DoBarrelExplosion(explosivecrate2); return; }
                    if ((choosebarrel == 3) && (explosivecrate3 != null)) { DoBarrelExplosion(explosivecrate3); return; }
                    if ((choosebarrel == 4) && (explosivecrate4 != null)) { DoBarrelExplosion(explosivecrate4); return; }
                    if ((choosebarrel == 5) && (explosivecrate5 != null)) { DoBarrelExplosion(explosivecrate5); return; }
                    if ((choosebarrel == 6) && (explosivecrate6 != null)) { DoBarrelExplosion(explosivecrate6); return; }
                    if ((choosebarrel == 7) && (explosivecrate7 != null)) { DoBarrelExplosion(explosivecrate7); return; }
                    if ((choosebarrel == 8) && (explosivecrate8 != null)) { DoBarrelExplosion(explosivecrate8); refinerysmall.Invoke("KillMessage", 0.1f); return; }
                    if ((choosebarrel == 9) && (explosivecrate9 != null)) { DoBarrelExplosion(explosivecrate9); return; }
                    if ((choosebarrel == 10) && (explosivecrate10 != null)) { DoBarrelExplosion(explosivecrate10); return; }
                }
                else
                {
                    stage3 = false;
                    stage4 = true;
                }
            }

            void DoBarrelExplosion(BaseEntity barrel)
            {
                if (barrel == null) return;
                string prefabexplosion = "assets/prefabs/npc/m2bradley/effects/bradley_explosion.prefab";
                BaseEntity fireentity = GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/oilfireball2.prefab", barrel.transform.position + new Vector3(0f, 1f, 0f), new Quaternion(), true);
                fireentity?.Spawn();
                BarrelExplosionDamage(barrel.transform.position);
                Effect.server.Run(prefabexplosion, barrel.transform.position);
                barrel.Invoke("KillMessage", 0.1f);
            }

            void BarrelExplosionDamage(Vector3 location)
            {
                List<BaseCombatEntity> playerlist = new List<BaseCombatEntity>();
                Vis.Entities<BaseCombatEntity>(location, 5f, playerlist);
                foreach (BaseCombatEntity p in playerlist)
                {
                    if (!(p is BuildingPrivlidge))
                    {
                        p.Hurt(Stage3BarrelDamage, Rust.DamageType.Explosion, null, false);
                    }
                }
            }

            void DeepWaterStage4()
            {
                HackableLockedCrate.requiredHackSeconds = Stage4HackDefaultReset;
                if (helicrate != null) helicrate.Invoke("KillMessage", 0.1f);
                SendDespawnMessage();
                string prefabexplosion = "assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab";
                Effect.server.Run(prefabexplosion, entity.transform.position);
                Effect.server.Run(prefabexplosion, entity.transform.position + (Vector3.right * 5f));
                Effect.server.Run(prefabexplosion, entity.transform.position - (Vector3.right * 5f));
                Effect.server.Run(prefabexplosion, entity.transform.position + (Vector3.forward * 5f));
                Effect.server.Run(prefabexplosion, entity.transform.position - (Vector3.forward * 5f));
                entity.Invoke("KillMessage", 0.1f);
            }

            void SpawnRigLoot()
            {
                count = 0;
                if (iscompound) return;
                if (isevent && !EventRigHasLootSpawn) return;
                if (!isevent && !StandardRigHasLootSpawn) return;
                if (DoLootCrateCount() < maxlootcrates) DoLootSpawn();
                else return;
            }

            int DoLootCrateCount()
            {
                currentlootcrates = 0;
                if (lootspawn1 != null) currentlootcrates = currentlootcrates + 1;
                if (lootspawn2 != null) currentlootcrates = currentlootcrates + 1;
                if (lootspawn3 != null) currentlootcrates = currentlootcrates + 1;
                if (lootspawn4 != null) currentlootcrates = currentlootcrates + 1;
                if (lootspawn5 != null) currentlootcrates = currentlootcrates + 1;
                if (lootspawn6 != null) currentlootcrates = currentlootcrates + 1;
                if (lootspawn7 != null) currentlootcrates = currentlootcrates + 1;
                if (lootspawn8 != null) currentlootcrates = currentlootcrates + 1;
                return currentlootcrates;
            }

            void DoLootSpawn()
            {
                int cratesroll = UnityEngine.Random.Range(1, 9);
                if (cratesroll == 1 && lootspawn1 == null && deck1row3floorframe2 != null) { lootspawn1 = SpawnLootBox(lootspawn1, deck1row3floorframe2.transform.position); return; }
                if (cratesroll == 2 && lootspawn2 == null && deck1row1floorframe3 != null) { lootspawn2 = SpawnLootBox(lootspawn2, deck1row1floorframe3.transform.position); return; }
                if (cratesroll == 3 && lootspawn3 == null && deck2frontrowfloor1 != null) { lootspawn3 = SpawnLootBox(lootspawn3, deck2frontrowfloor1.transform.position); return; }
                if (cratesroll == 4 && lootspawn4 == null && deck2frontrowfloor5 != null) { lootspawn4 = SpawnLootBox(lootspawn4, deck2frontrowfloor5.transform.position); return; }
                if (cratesroll == 5 && lootspawn5 == null && deck2backrowfloor1 != null) { lootspawn5 = SpawnLootBox(lootspawn5, deck2backrowfloor1.transform.position); return; }
                if (cratesroll == 6 && lootspawn6 == null && deck1row4floorframe2 != null) { lootspawn6 = SpawnLootBox(lootspawn6, deck1row4floorframe2.transform.position); return; }
                if (cratesroll == 7 && lootspawn7 == null && entity != null) { lootspawn7 = SpawnLootBox(lootspawn7, entity.transform.position + new Vector3(-6f, 3f, 4f)); return; }
                if (cratesroll == 8 && lootspawn8 == null & entity != null) { lootspawn8 = SpawnLootBox(lootspawn8, entity.transform.position + new Vector3(6.5f, 3f, -2f)); return; }
                else SpawnRigLoot();
            }

            BaseEntity SpawnLootBox(BaseEntity treasurebox, Vector3 spawnloc)
            {
                var randomlootprefab = "assets/bundled/prefabs/radtown/crate_basic.prefab";
                int rlroll = UnityEngine.Random.Range(1, 6);
                if (rlroll == 1) randomlootprefab = "assets/bundled/prefabs/radtown/crate_basic.prefab";
                if (rlroll == 2) randomlootprefab = "assets/bundled/prefabs/radtown/crate_elite.prefab";
                if (rlroll == 3) randomlootprefab = "assets/bundled/prefabs/radtown/crate_mine.prefab";
                if (rlroll == 4) randomlootprefab = "assets/bundled/prefabs/radtown/crate_normal.prefab";
                if (rlroll == 5) randomlootprefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab";
                var createdPrefab = GameManager.server.CreateEntity(randomlootprefab, spawnloc);
                treasurebox = createdPrefab?.GetComponent<BaseEntity>();
                treasurebox?.Spawn();
                return treasurebox;
            }

            public void MoveBucket()
            {
                if (BucketLift == null) return;
                if (!BucketLift.IsOpen() && !BucketLift.IsBusy())
                {
                    BucketLift.SetFlag(BaseEntity.Flags.Open, true, true);
                    BucketLift.SendNetworkUpdateImmediate();
                }
            }

            void BroadcastMessage(string str)
            {
                if (!BroadCastEventStages) return;
                string getgrid = deepwater.GetGridLocation(entity.transform.position);
                ConVar.Chat.Broadcast(str + getgrid, "DeepWater", "#4286f4");
            }



            void FixedUpdate()
            {
                if (dorigdestroy) return;
                if (!didspawnvariables)
                {
                    count++;
                    if (count >= 25) { CheckVariables(); count = 0; didspawnvariables = true; }
                    return;
                }
                if ((isevent && EventRigHasLiftBucket) || (!isevent && StandardRigHasLiftBucket))
                {
                    if (AutoMoveLiftBucket)
                    {
                        if (liftcount == 200) { MoveBucket(); liftcount = 0; }
                        liftcount++;
                    }
                }
                if ((isevent && EventRigHasLootSpawn) || (!isevent && StandardRigHasLootSpawn))
                {
                    if (count == LootRespawnTime) { SpawnRigLoot(); count = 0; }
                    count++;
                }
                if (stage1)
                {
                    if (stage1counter == stage1time) { DeepWaterStage1(); stage2 = true; stage1 = false; stage3 = false; stage4 = false; BroadcastMessage("Rig Event Stage 1 (CH47 Landing) started at grid : "); }
                    stage1counter++;
                    despawncount = 0;

                }
                if (stage2)
                {
                    if (stage2counter == stage2time) { DeepWaterStage2(); stage3 = true; stage1 = false; stage2 = false; stage4 = false; BroadcastMessage("Rig Event Stage 2 (CH47 Crash) started at grid : "); }
                    stage2counter = stage2counter + 1;
                    despawncount = 0;
                }
                if (stage3)
                {
                    if (stage3counter == stage3time) { BroadcastMessage("Rig Event Stage 3 (Rig Fire Started) started at grid : "); stage3startfire = true; }
                    if (stage3startfire)
                    {
                        if (stage3barrelcounter == stage3barreltime) { DeepWaterStage3(); stage3barrelcounter = 0; }
                        stage3barrelcounter = stage3barrelcounter + 1;
                    }
                    stage3counter = stage3counter + 1;
                    despawncount = 0;
                }
                if (stage4)
                {
                    stage1 = false; stage2 = false; stage3 = false;
                    if (stage4counter == 0) BroadcastMessage("Rig Event Stage 4 (Final Countdown) started at grid : ");
                    if (stage4counter >= stage4time) { DeepWaterStage4(); stage1 = false; stage2 = false; stage3 = false; stage4 = false; return; }
                    stage4counter = stage4counter + 1;
                    despawncount = 0;
                }
                if ((UseDespawnOnLocal && !israndom) || (UseDespawnOnRandom && israndom))
                {
                    despawncount = despawncount + 1;
                    if (despawncount == DespawnTime) DespawnRig();
                }
            }

            void DestroyLootBoxs()
            {
                if (lootspawn1 != null) lootspawn1.Invoke("KillMessage", 0.1f);
                if (lootspawn2 != null) lootspawn2.Invoke("KillMessage", 0.1f);
                if (lootspawn3 != null) lootspawn3.Invoke("KillMessage", 0.1f);
                if (lootspawn4 != null) lootspawn4.Invoke("KillMessage", 0.1f);
                if (lootspawn5 != null) lootspawn5.Invoke("KillMessage", 0.1f);
                if (lootspawn6 != null) lootspawn6.Invoke("KillMessage", 0.1f);
                if (lootspawn7 != null) lootspawn7.Invoke("KillMessage", 0.1f);
                if (lootspawn8 != null) lootspawn8.Invoke("KillMessage", 0.1f);
            }

            void SendDespawnMessage()
            {
                if (isevent && BroadcastEventEnd)
                {
                    ConVar.Chat.Broadcast("Rig Event has Ended at Grid", "DeepWater", "#4286f4");
                }
                else return;
            }

            public void OnDestroy()
            {
                for (int i = 0; i < spawnedParts.Count; i++)
                {
                    BaseEntity baseEntity = spawnedParts[i];
                    if (baseEntity != null && !baseEntity.IsDestroyed)
                        baseEntity.Kill(BaseNetworkable.DestroyMode.None);
                }

                Facepunch.Pool.FreeList(ref spawnedParts);

                DestroyLootBoxs();

                if (helicopter != null && !helicopter.IsDestroyed)
                    helicopter.Kill(BaseNetworkable.DestroyMode.None);

                if (helicrate != null && !helicrate.IsDestroyed)
                    helicrate.Kill(BaseNetworkable.DestroyMode.None);

                if (boxcollider != null) 
                    GameObject.Destroy(boxcollider);

                if (isautospawn && SpawnRandomOnLoad) 
                    DoRandomRespawn = true;

                if (mapmarker != null && !mapmarker.IsDestroyed)
                    mapmarker.Kill(BaseNetworkable.DestroyMode.None);

                deepwater.EraseZone(zoneid);

                if (pumpjackentity != null && !pumpjackentity.IsDestroyed)
                    pumpjackentity.Kill(BaseNetworkable.DestroyMode.None);

                if (entity != null && !entity.IsDestroyed)
                    entity.Kill(BaseNetworkable.DestroyMode.None);
            }
        }

        #endregion

    }
}