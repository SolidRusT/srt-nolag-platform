using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using System.Text;
using System.Globalization;
using Rust.Ai;

namespace Oxide.Plugins
{
    [Info("Dangerous Treasures", "nivex", "2.1.9")]
    [Description("Event with treasure chests.")]
    class DangerousTreasures : RustPlugin
    {
        [PluginReference] Plugin LustyMap, ZoneManager, Economics, ServerRewards, Map, GUIAnnouncements, MarkerManager, CopyPaste, Clans, Friends, Kits, NPCKits, Duelist, RaidableBases;

        static bool useRockets;
        static bool useRandomSkins;
        static bool useSpheres;
        static bool useMissileLauncher;
        static bool useFireballs;
        const bool True = true;
        const bool False = false;
        static DangerousTreasures ins;
        static bool unloading = false;
        bool init = false;
        bool wipeChestsSeed = false;
        SpawnFilter filter = new SpawnFilter();
        ItemDefinition boxDef;
        static ItemDefinition rocketDef;
        string boxShortname;
        const string fireRocketShortname = "ammo.rocket.fire";
        const string basicRocketShortname = "ammo.rocket.basic";
        string boxPrefab;
        const string spherePrefab = "assets/prefabs/visualization/sphere.prefab";
        const string fireballPrefab = "assets/bundled/prefabs/oilfireballsmall.prefab";
        static string radiusMarkerPrefab;
        static string scientistPrefab;
        static string murdererPrefab;
        static string vendingPrefab;
        static string explosionPrefab;
        static string rocketResourcePath;
        DynamicConfigFile dataFile;
        static StoredData storedData = new StoredData();
        Vector3 sd_customPos;
        Timer eventTimer;

        static int treeLayer = LayerMask.GetMask("Tree");
        static int worldLayer = LayerMask.GetMask("World");
        static int waterLayer = LayerMask.GetMask("Water");
        int deployableLayer = LayerMask.GetMask("Deployed");
        static int playerLayer = LayerMask.GetMask("Player (Server)");
        int obstructionLayer = LayerMask.GetMask("Player (Server)", "Construction", "Deployed", "Clutter");
        static int heightLayer = LayerMask.GetMask("Terrain", "World", "Default", "Construction", "Deployed", "Clutter");
        List<int> BlockedLayers = new List<int> { (int)Layer.Water, (int)Layer.Construction, (int)Layer.Trigger, (int)Layer.Prevent_Building, (int)Layer.Deployed, (int)Layer.Tree, (int)Layer.Clutter };
        //int terrainLayer = LayerMask.GetMask("Terrain", "World", "Default", "Construction", "Deployed");
        int terrainLayer = LayerMask.GetMask("Terrain", "World", "Default"); //already checked

        static Dictionary<string, MonInfo> allowedMonuments = new Dictionary<string, MonInfo>();
        static Dictionary<string, MonInfo> monuments = new Dictionary<string, MonInfo>();  // positions of monuments on the server

        class MonInfo
        {
            public Vector3 Position;
            public float Radius;
        }

        static List<uint> newmanProtections = new List<uint>();
        List<ulong> indestructibleWarnings = new List<ulong>(); // indestructible messages limited to once every 10 seconds
        List<ulong> drawGrants = new List<ulong>(); // limit draw to once every 15 seconds by default
        Dictionary<Vector3, float> managedZones = new Dictionary<Vector3, float>();
        static Dictionary<uint, MapInfo> mapMarkers = new Dictionary<uint, MapInfo>();
        static Dictionary<uint, string> lustyMarkers = new Dictionary<uint, string>();
        Dictionary<string, List<ulong>> skinsCache = new Dictionary<string, List<ulong>>();
        Dictionary<string, List<ulong>> workshopskinsCache = new Dictionary<string, List<ulong>>();
        static Dictionary<uint, TreasureChest> treasureChests = new Dictionary<uint, TreasureChest>();
        static Dictionary<uint, string> looters = new Dictionary<uint, string>();

        class MapInfo
        {
            public string Url;
            public string IconName;
            public Vector3 Position;

            public MapInfo() { }
        }

        class PlayerInfo
        {
            public int StolenChestsTotal { get; set; } = 0;
            public int StolenChestsSeed { get; set; } = 0;
            public PlayerInfo() { }
        }

        class StoredData
        {
            public double SecondsUntilEvent = double.MinValue;
            public int TotalEvents = 0;
            public readonly Dictionary<string, PlayerInfo> Players = new Dictionary<string, PlayerInfo>();
            public List<uint> Markers = new List<uint>();
            public string CustomPosition;
            public StoredData() { }
        }

        class GuidanceSystem : FacepunchBehaviour
        {
            TimedExplosive missile;
            ServerProjectile projectile;
            BaseEntity target;
            float rocketSpeedMulti = 2f;
            Timer launch;
            Vector3 launchPos;
            List<ulong> exclude = new List<ulong>();
            public bool targetChest;

            void Awake()
            {
                missile = GetComponent<TimedExplosive>();
                projectile = missile.GetComponent<ServerProjectile>();

                launchPos = missile.transform.position;
                launchPos.y = TerrainMeta.HeightMap.GetHeight(launchPos);

                projectile.gravityModifier = 0f;
                projectile.speed = 0.1f;
                projectile.InitializeVelocity(Vector3.up);

                missile.explosionRadius = 0f;
                missile.timerAmountMin = _config.MissileLauncher.Lifetime;
                missile.timerAmountMax = _config.MissileLauncher.Lifetime;

                missile.damageTypes = new List<DamageTypeEntry>(); // no damage
            }

            public void SetTarget(BaseEntity target, bool targetChest)
            {
                this.target = target;
                this.targetChest = targetChest;
            }

            public void Launch(float targettingTime)
            {
                missile.Spawn();

                launch = ins.timer.Once(targettingTime, () =>
                {
                    if (!missile || missile.IsDestroyed)
                        return;

                    var list = new List<BasePlayer>();
                    var entities = new List<BaseEntity>();
                    Vis.Entities<BaseEntity>(launchPos, _config.Event.Radius + _config.MissileLauncher.Distance, entities, playerLayer, QueryTriggerInteraction.Ignore);

                    foreach (var entity in entities)
                    {
                        var player = entity as BasePlayer;

                        if (!player || player is NPCPlayer || !player.CanInteract())
                            continue;

                        if (_config.MissileLauncher.IgnoreFlying && player.IsFlying)
                            continue;

                        if (exclude.Contains(player.userID) || newmanProtections.Contains(player.net.ID))
                            continue;

                        list.Add(player); // acquire a player target 
                    }

                    entities.Clear();
                    entities = null;

                    if (list.Count > 0)
                    {
                        target = list.GetRandom(); // pick a random player
                        list.Clear();
                        list = null;
                    }
                    else if (!_config.MissileLauncher.TargetChest)
                    {
                        missile.Kill();
                        return;
                    }

                    projectile.speed = _config.Rocket.Speed * rocketSpeedMulti;
                    InvokeRepeating(GuideMissile, 0.1f, 0.1f);
                });
            }

            public void Exclude(List<ulong> list)
            {
                if (list != null && list.Count > 0)
                {
                    exclude.Clear();
                    exclude.AddRange(list);
                }
            }

            void GuideMissile()
            {
                if (target == null)
                    return;

                if (target.IsDestroyed)
                {
                    if (missile != null && !missile.IsDestroyed)
                    {
                        missile.Kill();
                    }

                    return;
                }

                if (missile == null || missile.IsDestroyed || projectile == null)
                {
                    GameObject.Destroy(this);
                    return;
                }

                if (Vector3.Distance(target.transform.position, missile.transform.position) <= 1f)
                {
                    missile.Explode();
                    return;
                }

                var direction = (target.transform.position - missile.transform.position) + Vector3.down; // direction to guide the missile
                projectile.InitializeVelocity(direction); // guide the missile to the target's position
            }

            void OnDestroy()
            {
                exclude.Clear();
                launch?.Destroy();
                CancelInvoke(GuideMissile);
                Destroy(this);
            }
        }

        public class TreasureChest : FacepunchBehaviour
        {
            public StorageContainer container;
            public bool started;
            public bool opened;
            long _unlockTime;
            public Vector3 containerPos;
            public uint uid;
            public int countdownTime;
            float posMulti = 3f;
            float sphereMulti = 2f;
            float claimTime;
            Vector3 lastFirePos;
            bool firstEntered;
            bool markerCreated;
            string zoneName;

            Dictionary<ulong, float> fireticks = new Dictionary<ulong, float>();
            List<FireBall> fireballs = new List<FireBall>();
            List<ulong> newmans = new List<ulong>();
            List<ulong> traitors = new List<ulong>();
            List<uint> protects = new List<uint>();
            List<ulong> players = new List<ulong>();
            List<TimedExplosive> missiles = new List<TimedExplosive>();
            List<int> times = new List<int>();
            List<SphereEntity> spheres = new List<SphereEntity>();
            List<Vector3> missilePositions = new List<Vector3>();
            List<Vector3> firePositions = new List<Vector3>();
            Timer destruct, unlock, countdown, announcement;
            public List<NPCPlayerApex> npcs = new List<NPCPlayerApex>();
            MapMarkerExplosion explosionMarker;
            MapMarkerGenericRadius genericMarker;
            VendingMachineMapMarker vendingMarker;
            int npcSpawnedAmount;
            bool npcsSpawned;
            bool killed = False;
            float _radius;
            Dictionary<string, List<string>> npcKits = new Dictionary<string, List<string>>();

            public float Radius
            {
                get
                {
                    return _radius;
                }
                set
                {
                    _radius = value;
                    Awaken();
                }
            }

            class NewmanTracker : FacepunchBehaviour
            {
                BasePlayer player;
                TreasureChest chest;

                void Awake()
                {
                    player = GetComponent<BasePlayer>();
                }

                public void Assign(TreasureChest chest)
                {
                    this.chest = chest;
                    InvokeRepeating(Track, 1f, 0.1f);
                }

                void Track()
                {
                    if (!chest || chest.started || !player || !player.IsConnected || !chest.players.Contains(player.userID))
                    {
                        Destroy(this);
                        return;
                    }

                    if ((player.transform.position - chest.containerPos).magnitude > chest.Radius)
                    {
                        return;
                    }

                    if (_config.NewmanMode.Aura || _config.NewmanMode.Harm)
                    {
                        int count = player.inventory.AllItems().Where(item => item.info.shortname != "torch" && item.info.shortname != "rock" && player.IsHostileItem(item))?.Count() ?? 0;

                        if (count == 0)
                        {
                            if (_config.NewmanMode.Aura && !chest.newmans.Contains(player.userID) && !chest.traitors.Contains(player.userID))
                            {
                                player.ChatMessage(ins.msg("Newman Enter", player.UserIDString));
                                chest.newmans.Add(player.userID);
                            }

                            if (_config.NewmanMode.Harm && !newmanProtections.Contains(player.net.ID) && !chest.protects.Contains(player.net.ID) && !chest.traitors.Contains(player.userID))
                            {
                                player.ChatMessage(ins.msg("Newman Protect", player.UserIDString));
                                newmanProtections.Add(player.net.ID);
                                chest.protects.Add(player.net.ID);
                            }

                            if (!chest.traitors.Contains(player.userID))
                            {
                                return;
                            }
                        }

                        if (chest.newmans.Contains(player.userID))
                        {
                            player.ChatMessage(ins.msg(useFireballs ? "Newman Traitor Burn" : "Newman Traitor", player.UserIDString));
                            chest.newmans.Remove(player.userID);

                            if (!chest.traitors.Contains(player.userID))
                                chest.traitors.Add(player.userID);

                            if (newmanProtections.Contains(player.net.ID))
                                newmanProtections.Remove(player.net.ID);

                            if (chest.protects.Contains(player.net.ID))
                                chest.protects.Remove(player.net.ID);
                        }
                    }

                    if (!useFireballs || player.IsFlying)
                    {
                        return;
                    }

                    var stamp = Time.realtimeSinceStartup;

                    if (!chest.fireticks.ContainsKey(player.userID))
                    {
                        chest.fireticks[player.userID] = stamp + _config.Fireballs.SecondsBeforeTick;
                    }

                    if (chest.fireticks[player.userID] - stamp <= 0)
                    {
                        chest.fireticks[player.userID] = stamp + _config.Fireballs.SecondsBeforeTick;
                        chest.SpawnFire(player.transform.position);
                    }
                }

                void OnDestroy()
                {
                    CancelInvoke(Track);
                    Destroy(this);
                }
            }

            void Free()
            {
                fireticks.Clear();
                fireticks = null;
                fireballs.Clear();
                fireballs = null;
                newmans.Clear();
                newmans = null;
                traitors.Clear();
                traitors = null;
                protects.Clear();
                protects = null;
                missiles.Clear();
                missiles = null;
                times.Clear();
                times = null;
                spheres.Clear();
                spheres = null;
                missilePositions.Clear();
                missilePositions = null;
                firePositions.Clear();
                firePositions = null;
                npcKits.Clear();
                npcKits = null;

                if (destruct != null)
                {
                    destruct.Destroy();
                }

                if (unlock != null)
                {
                    unlock.Destroy();
                }

                if (countdown != null)
                {
                    countdown.Destroy();
                }

                if (announcement != null)
                {
                    announcement.Destroy();
                }
            }

            public void Kill()
            {
                if (killed) return;

                RemoveMapMarkers();
                KillNpc();
                CancelInvoke();
                DestroyLauncher();
                DestroySphere();
                DestroyFire();
                killed = true;
                Interface.CallHook("OnDangerousEventEnded", containerPos);
            }

            public bool HasRustMarker
            {
                get
                {
                    return explosionMarker != null || vendingMarker != null;
                }
            }

            public static bool HasNPC(NPCPlayerApex player)
            {
                return player != null && HasNPC(player.userID);
            }

            public static bool HasNPC(ulong userID)
            {
                foreach (var x in treasureChests.Values)
                {
                    foreach (var npc in x.npcs)
                    {
                        if (npc.userID == userID)
                        {
                            return True;
                        }
                    }
                }

                return False;
            }

            public static bool IsTooFar(ulong userID)
            {
                foreach (var x in treasureChests.Values)
                {
                    foreach (var npc in x.npcs)
                    {
                        if (npc.userID == userID)
                        {
                            return (x.containerPos - npc.transform.position).magnitude > x.Radius * 2f;
                        }
                    }
                }

                return False;
            }

            public static TreasureChest GetNPC(ulong userID)
            {
                foreach (var x in treasureChests.Values)
                {
                    foreach (var npc in x.npcs)
                    {
                        if (npc.userID == userID)
                        {
                            return x;
                        }
                    }
                }

                return null;
            }

            public static TreasureChest Get(Vector3 target, float f = 15f)
            {
                foreach (var x in treasureChests.Values)
                {
                    if (Vector3Ex.Distance2D(x.containerPos, target) <= x.Radius + f)
                    {
                        return x;
                    }
                }

                return null;
            }

            public static bool HasNPC(ulong userID1, ulong userID2)
            {
                bool flag1 = False;
                bool flag2 = False;
                
                foreach (var chest in treasureChests.Values)
                {
                    foreach (var npc in chest.npcs)
                    {
                        if (npc.userID == userID1)
                        {
                            flag1 = True;
                        }
                        else if (npc.userID == userID2)
                        {
                            flag2 = True;
                        }
                    }
                }

                return flag1 && flag2;
            }

            public void Awaken()
            {
                var collider = gameObject.GetComponent<SphereCollider>() ?? gameObject.AddComponent<SphereCollider>();
                collider.center = Vector3.zero;
                collider.radius = Radius;
                collider.isTrigger = true;
                collider.enabled = true;

                if (_config.Event.Spheres && _config.Event.SphereAmount > 0)
                {
                    for (int i = 0; i < _config.Event.SphereAmount; i++)
                    {
                        var sphere = GameManager.server.CreateEntity(spherePrefab, containerPos, new Quaternion(), true) as SphereEntity;

                        if (sphere != null)
                        {
                            sphere.currentRadius = 1f;
                            sphere.Spawn();
                            sphere.LerpRadiusTo(Radius * sphereMulti, 5f);
                            spheres.Add(sphere);
                        }
                        else
                        {
                            ins.Puts(_("Invalid Constant", null, spherePrefab));
                            useSpheres = false;
                            break;
                        }
                    }
                }

                if (useRockets)
                {
                    var positions = GetRandomPositions(containerPos, Radius * posMulti, _config.Rocket.Amount, 0f);

                    foreach (var position in positions)
                    {
                        var missile = GameManager.server.CreateEntity(rocketResourcePath, position, new Quaternion(), true) as TimedExplosive;
                        var gs = missile.gameObject.AddComponent<GuidanceSystem>();

                        gs.SetTarget(container, true);
                        gs.Launch(0.1f);
                    }

                    positions.Clear();
                }

                if (useFireballs)
                {
                    firePositions = GetRandomPositions(containerPos, Radius, 25, containerPos.y + 25f);

                    if (firePositions.Count > 0)
                        InvokeRepeating(SpawnFire, 0.1f, _config.Fireballs.SecondsBeforeTick);
                }

                if (useMissileLauncher)
                {
                    missilePositions = GetRandomPositions(containerPos, Radius, 25, 1);

                    if (missilePositions.Count > 0)
                    {
                        InvokeRepeating(LaunchMissile, 0.1f, _config.MissileLauncher.Frequency);
                        LaunchMissile();
                    }
                }

                InvokeRepeating(UpdateMarker, 5f, 30f);
                Interface.CallHook("OnDangerousEventStarted", containerPos);
            }

            void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                container = GetComponent<StorageContainer>();
                container.OwnerID = 0;
                containerPos = container.transform.position;
                uid = container.net.ID;
                container.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
                SetupNpcKits();
            }

            public static void SetLoot(StorageContainer container, List<TreasureItem> treasure)
            {
                if (container == null || container.IsDestroyed || treasure == null || treasure.Count == 0)
                {
                    return;
                }

                var loot = treasure.ToList();

                container.inventory.Clear();
                container.inventory.capacity = Math.Min(_config.Event.TreasureAmount, loot.Count);

                for (int j = 0; j < container.inventory.capacity; j++)
                {
                    if (loot.Count == 0)
                    {
                        break;
                    }

                    var lootItem = loot.GetRandom();
                    var itemDef = ItemManager.FindItemDefinition(lootItem.shortname);

                    loot.Remove(lootItem);

                    if (itemDef == null)
                    {
                        ins.PrintError("Invalid shortname in config: {0}", lootItem.shortname);
                        continue;
                    }

                    int amount = Convert.ToInt32(lootItem.amount);

                    if (amount <= 0)
                    {
                        j--;
                        continue;
                    }

                    int amountMin = Convert.ToInt32(lootItem.amountMin);

                    if (amountMin > 0 && amountMin < amount)
                    {
                        amount = UnityEngine.Random.Range(amountMin, amount);
                    }

                    if (itemDef.stackable == 1 || (itemDef.condition.enabled && itemDef.condition.max > 0f))
                        amount = 1;

                    ulong skin = lootItem.skin;
                    Item item = ItemManager.CreateByName(itemDef.shortname, amount, skin);

                    if (item.info.stackable > 1 && !item.hasCondition)
                    {
                        item.amount = GetPercentIncreasedAmount(amount);
                    }

                    if (_config.Treasure.RandomSkins && skin == 0)
                    {
                        var skins = ins.GetItemSkins(item.info);

                        if (skins.Count > 0)
                        {
                            skin = skins.GetRandom();
                            item.skin = skin;
                        }
                    }

                    if (skin != 0 && item.GetHeldEntity())
                        item.GetHeldEntity().skinID = skin;

                    item.MarkDirty();

                    if (!item.MoveToContainer(container.inventory, -1, true))
                    {
                        item.Remove(0.1f);
                    }
                }

                loot.Clear();
                loot = null;
            }

            void OnTriggerEnter(Collider col)
            {
                if (started)
                    return;

                var player = col.ToBaseEntity() as BasePlayer;

                if (!player || player.IsNpc || players.Contains(player.userID))
                    return;

                if (_config.EventMessages.FirstEntered && !firstEntered)
                {
                    firstEntered = true;
                    ins.PrintToChat(ins.msg("OnFirstPlayerEntered", null, player.displayName, FormatGridReference(containerPos)));
                }

                string key;
                if (_config.EventMessages.NoobWarning)
                {
                    key = _config.Unlock.WhenNpcsDie && npcsSpawned ? "Npc Event" : _config.Unlock.RequireAllNpcsDie && npcsSpawned ? "Timed Npc Event" : "Timed Event";
                }
                else key = useFireballs ? "Dangerous Zone Protected" : "Dangerous Zone Unprotected";

                Message(player, ins.msg(key, player.UserIDString));

                var tracker = player.gameObject.GetComponent<NewmanTracker>() ?? player.gameObject.AddComponent<NewmanTracker>();

                tracker.Assign(this);

                players.Add(player.userID);
            }

            void OnTriggerExit(Collider col)
            {
                var player = col.ToBaseEntity() as BasePlayer;

                if (!player)
                    return;

                if (player.IsNpc && npcs.Contains(player))
                {
                    var apex = player as NPCPlayerApex;

                    if (apex.GetNavAgent == null || !apex.GetNavAgent.isOnNavMesh)
                        apex.finalDestination = containerPos;
                    else apex.GetNavAgent.SetDestination(containerPos);

                    apex.Destination = containerPos;
                }

                if (!_config.NewmanMode.Harm)
                    return;

                if (protects.Contains(player.net.ID))
                {
                    newmanProtections.Remove(player.net.ID);
                    protects.Remove(player.net.ID);
                    Message(player, ins.msg("Newman Protect Fade", player.UserIDString));
                }

                newmans.Remove(player.userID);
            }

            public void SpawnNpcs()
            {
                container.SendNetworkUpdate();

                if (_config.NPC.SpawnAmount < 1 || !_config.NPC.Enabled)
                    return;

                var rot = new Quaternion(1f, 0f, 0f, 1f);
                int amount = _config.NPC.SpawnRandomAmount && _config.NPC.SpawnAmount > 1 ? UnityEngine.Random.Range(_config.NPC.SpawnMinAmount, _config.NPC.SpawnAmount) : _config.NPC.SpawnAmount;

                for (int i = 0; i < amount; i++)
                {
                    var npc = SpawnNPC(rot, _config.NPC.SpawnScientistsOnly ? false : _config.NPC.SpawnBoth ? UnityEngine.Random.Range(0.1f, 1.0f) > 0.5f : _config.NPC.SpawnMurderers);

                    if (npc == null)
                    {
                        continue;
                    }

                    npcs.Add(npc);
                }

                npcSpawnedAmount = npcs.Count;
                npcsSpawned = npcSpawnedAmount > 0;
            }

            Vector3 FindPointOnNavmesh()
            {
                int tries = 0;
                NavMeshHit navHit;

                while (++tries < 100)
                {
                    var r = containerPos + (UnityEngine.Random.insideUnitSphere * (Radius - 2.5f));

                    if (NavMesh.SamplePosition(r, out navHit, Radius, NavMesh.AllAreas))
                    {
                        if (ins.IsInOrOnRock(navHit.position, "rock_"))
                        {
                            continue;
                        }
                        if (navHit.position.y < TerrainMeta.HeightMap.GetHeight(navHit.position) + 1f)
                        {
                            return navHit.position;
                        }
                    }
                }

                return Vector3.zero;
            }

            void ResetNpc(NPCPlayerApex apex)
            {
                var position = FindPointOnNavmesh();

                if (position == Vector3.zero)
                {
                    return;
                }

                apex.ServerPosition = position;
                apex.GetNavAgent.Warp(position);
            }

            NPCPlayerApex InstantiateEntity(Vector3 position, bool murd)
            {
                var name = murd ? murdererPrefab : scientistPrefab;
                var prefab = GameManager.server.FindPrefab(name);
                var gameObject = Facepunch.Instantiate.GameObject(prefab, position, default(Quaternion));

                gameObject.name = name;
                SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);

                if (gameObject.GetComponent<Spawnable>())
                {
                    Destroy(gameObject.GetComponent<Spawnable>());
                }

                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }

                return gameObject.GetComponent<NPCPlayerApex>();
            }

            Vector3 GetRandomPoint
            {
                get
                {
                    return containerPos + UnityEngine.Random.insideUnitSphere * (Radius * 0.5f);
                }
            }

            NPCPlayerApex SpawnNPC(Quaternion rot, bool murd)
            {
                var spawnPoint = FindPointOnNavmesh();

                if (spawnPoint == Vector3.zero)
                    return null;

                var apex = InstantiateEntity(spawnPoint, murd);

                if (apex == null)
                    return null;

                
                apex.Spawn();
                apex.CancelInvoke(apex.EquipTest);
                apex.CancelInvoke(apex.RadioChatter);

                apex.startHealth = murd ? _config.NPC.MurdererHealth : _config.NPC.ScientistHealth;
                apex.InitializeHealth(apex.startHealth, apex.startHealth);
                apex.CommunicationRadius = 0;
                apex.RadioEffect = new GameObjectRef();
                apex.displayName = _config.NPC.RandomNames.Count > 0 ? _config.NPC.RandomNames.GetRandom() : Facepunch.RandomUsernames.Get(apex.userID);
                if (!murd) apex.GetComponent<Scientist>().LootPanelName = apex.displayName;

                apex.Invoke(() => EquipNpc(apex, murd), 1f);

                apex.Invoke(() =>
                {
                    var heldEntity = apex.GetHeldEntity();

                    if (heldEntity != null)
                    {
                        heldEntity.SetHeld(true);
                    }

                    apex.EquipWeapon();
                }, 2f);

                if (_config.NPC.DespawnInventory)
                {
                    if (murd)
                    {
                        apex.GetComponent<NPCMurderer>().LootSpawnSlots = new LootContainer.LootSpawnSlot[0];
                    }
                    else apex.GetComponent<Scientist>().LootSpawnSlots = new LootContainer.LootSpawnSlot[0];
                }

                apex.SpawnPosition = containerPos;
                apex.Stats.MaxRoamRange = 75f;

                ins.timer.Once(0.25f, () => UpdateDestination(apex, containerPos, murd));
                return apex;
            }

            void SetupNpcKits()
            {
                npcKits = new Dictionary<string, List<string>>
                {
                    { "murderer", _config.NPC.MurdererKits.Where(kit => IsKit(kit)).ToList() },
                    { "scientist", _config.NPC.ScientistKits.Where(kit => IsKit(kit)).ToList() }
                };
            }

            bool IsKit(string kit)
            {
                return ins.Kits != null && Convert.ToBoolean(ins.Kits?.Call("isKit", kit));
            }

            void EquipNpc(NPCPlayerApex apex, bool murd)
            {
                if (apex.IsDestroyed)
                {
                    return;
                }

                List<string> kits;
                if (npcKits.TryGetValue(murd ? "murderer" : "scientist", out kits) && kits.Count > 0)
                {
                    apex.inventory.Strip();

                    object success = ins.Kits.Call("GiveKit", apex, kits.GetRandom());

                    if (success is bool && (bool)success)
                    {
                        return;
                    }
                }

                var items = new List<string>();

                if (murd)
                {
                    if (_config.NPC.MurdererItems.Boots.Count > 0) items.Add(_config.NPC.MurdererItems.Boots.GetRandom());
                    if (_config.NPC.MurdererItems.Gloves.Count > 0) items.Add(_config.NPC.MurdererItems.Gloves.GetRandom());
                    if (_config.NPC.MurdererItems.Helm.Count > 0) items.Add(_config.NPC.MurdererItems.Helm.GetRandom());
                    if (_config.NPC.MurdererItems.Pants.Count > 0) items.Add(_config.NPC.MurdererItems.Pants.GetRandom());
                    if (_config.NPC.MurdererItems.Shirt.Count > 0) items.Add(_config.NPC.MurdererItems.Shirt.GetRandom());
                    if (_config.NPC.MurdererItems.Torso.Count > 0) items.Add(_config.NPC.MurdererItems.Torso.GetRandom());
                    if (_config.NPC.MurdererItems.Weapon.Count > 0) items.Add(_config.NPC.MurdererItems.Weapon.GetRandom());
                }
                else
                {
                    if (_config.NPC.ScientistItems.Boots.Count > 0) items.Add(_config.NPC.ScientistItems.Boots.GetRandom());
                    if (_config.NPC.ScientistItems.Gloves.Count > 0) items.Add(_config.NPC.ScientistItems.Gloves.GetRandom());
                    if (_config.NPC.ScientistItems.Helm.Count > 0) items.Add(_config.NPC.ScientistItems.Helm.GetRandom());
                    if (_config.NPC.ScientistItems.Pants.Count > 0) items.Add(_config.NPC.ScientistItems.Pants.GetRandom());
                    if (_config.NPC.ScientistItems.Shirt.Count > 0) items.Add(_config.NPC.ScientistItems.Shirt.GetRandom());
                    if (_config.NPC.ScientistItems.Torso.Count > 0) items.Add(_config.NPC.ScientistItems.Torso.GetRandom());
                    if (_config.NPC.ScientistItems.Weapon.Count > 0) items.Add(_config.NPC.ScientistItems.Weapon.GetRandom());
                }

                if (items.Count == 0)
                {
                    return;
                }

                apex.inventory.Strip();

                SpawnItems(apex, items);
            }

            void SpawnItems(BasePlayer player, List<string> items)
            {
                foreach (string shortname in items)
                {
                    var def = ItemManager.FindItemDefinition(shortname);

                    if (def == null)
                    {
                        continue;
                    }

                    Item item = ItemManager.Create(def, 1, 0);
                    var skins = ins.GetItemSkins(item.info);

                    if (skins.Count > 0)
                    {
                        ulong skin = skins.GetRandom();
                        item.skin = skin;
                    }

                    if (item.skin != 0 && item.GetHeldEntity())
                    {
                        item.GetHeldEntity().skinID = item.skin;
                    }

                    item.MarkDirty();

                    if (!item.MoveToContainer(player.inventory.containerWear, -1, False) && !item.MoveToContainer(player.inventory.containerBelt, 0, False))
                    {
                        item.Remove(0f);
                    }
                }
            }

            void UpdateDestination(NPCPlayerApex apex, Vector3 pos, bool murd)
            {
                if (!apex.IsValid() || apex.IsDestroyed)
                {
                    return;
                }

                if (apex.AttackTarget == null)
                {
                    apex.NeverMove = true;
                    float distance = (apex.transform.position - pos).magnitude;
                    bool tooFar = distance > Radius; // * 1.25f;

                    if (apex.GetNavAgent == null || !apex.GetNavAgent.isOnNavMesh)
                        apex.finalDestination = pos;
                    else apex.GetNavAgent.SetDestination(pos);

                    apex.Destination = pos;
                    apex.SetFact(NPCPlayerApex.Facts.Speed, tooFar ? (byte)NPCPlayerApex.SpeedEnum.Run : (byte)NPCPlayerApex.SpeedEnum.Walk, true, true);
                }
                else apex.NeverMove = false;

                ins.timer.Once(7.5f, () => UpdateDestination(apex, pos, murd));
            }

            public void UpdateMarker()
            {
                if (!_config.Event.MarkerVending && !_config.Event.MarkerExplosion)
                {
                    CancelInvoke(UpdateMarker);
                }

                if (explosionMarker != null && !explosionMarker.IsDestroyed)
                {
                    explosionMarker.SendNetworkUpdate();
                }

                if (genericMarker != null && !genericMarker.IsDestroyed)
                {
                    genericMarker.SendUpdate();
                }

                if (vendingMarker != null && !vendingMarker.IsDestroyed)
                {
                    vendingMarker.SendNetworkUpdate();
                }

                if (markerCreated)
                {
                    return;
                }

                if (ins.MarkerManager != null)
                {
                    Interface.CallHook("API_CreateMarker", container as BaseEntity, "DangerousTreasures", 0, 10f, 0.25f, _config.Event.MarkerName, "FF0000", "00FFFFFF");
                    markerCreated = true;
                    return;
                }

                int markerCount = 0;

                foreach (var e in treasureChests.Values)
                {
                    if (e.HasRustMarker && ++markerCount > 10)
                    {
                        return;
                    }
                }

                //explosionmarker cargomarker ch47marker cratemarker
                if (_config.Event.MarkerExplosion)
                {
                    explosionMarker = GameManager.server.CreateEntity(explosionPrefab, containerPos, Quaternion.identity, true) as MapMarkerExplosion;

                    if (explosionMarker != null)
                    {
                        explosionMarker.Spawn();
                        explosionMarker.SendMessage("SetDuration", 60, SendMessageOptions.DontRequireReceiver);
                    }

                    genericMarker = GameManager.server.CreateEntity(radiusMarkerPrefab, containerPos) as MapMarkerGenericRadius;

                    if (genericMarker != null)
                    {
                        genericMarker.alpha = 1f;
                        genericMarker.color2 = __(_config.Event.MarkerColor);
                        genericMarker.radius = Mathf.Clamp(_config.Event.MarkerRadius, 0.1f, 1f);
                        genericMarker.Spawn();
                        genericMarker.SendUpdate();
                    }
                }
                else if (_config.Event.MarkerVending)
                {
                    vendingMarker = GameManager.server.CreateEntity(vendingPrefab, containerPos) as VendingMachineMapMarker;

                    if (vendingMarker != null)
                    {
                        vendingMarker.enabled = false;
                        vendingMarker.markerShopName = _config.Event.MarkerName;
                        vendingMarker.Spawn();
                    }

                    genericMarker = GameManager.server.CreateEntity(radiusMarkerPrefab, containerPos) as MapMarkerGenericRadius;

                    if (genericMarker != null)
                    {
                        genericMarker.alpha = 0.75f;
                        genericMarker.color2 = __(_config.Event.MarkerColor);
                        genericMarker.radius = Mathf.Clamp(_config.Event.MarkerRadius, 0.1f, 1f);
                        genericMarker.Spawn();
                        genericMarker.SendUpdate();
                    }
                }

                markerCreated = true;
            }

            void KillNpc()
            {
                npcs.ForEach(npc =>
                {
                    if (npc == null || npc.IsDestroyed)
                        return;

                    npc.Kill();
                });
            }

            public void RemoveMapMarkers()
            {
                ins.RemoveLustyMarker(uid);
                ins.RemoveMapMarker(uid);

                if (explosionMarker != null && !explosionMarker.IsDestroyed)
                {
                    explosionMarker.CancelInvoke(explosionMarker.DelayedDestroy);
                    explosionMarker.Kill(BaseNetworkable.DestroyMode.None);
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

            void OnDestroy()
            {
                Kill();

                if (treasureChests.ContainsKey(uid))
                {
                    treasureChests.Remove(uid);

                    if (!unloading && treasureChests.Count == 0) // 0.1.21 - nre fix
                    {
                        ins.SubscribeHooks(false);
                    }
                }

                Free();
            }

            public void LaunchMissile()
            {
                if (string.IsNullOrEmpty(rocketResourcePath))
                    useMissileLauncher = false;

                if (!useMissileLauncher)
                {
                    DestroyLauncher();
                    return;
                }

                var missilePos = missilePositions.GetRandom();
                float y = TerrainMeta.HeightMap.GetHeight(missilePos) + 15f;
                missilePos.y = 200f;

                RaycastHit hit;
                if (Physics.Raycast(missilePos, Vector3.down, out hit, heightLayer)) // don't want the missile to explode before it leaves its spawn location
                    missilePos.y = Mathf.Max(hit.point.y, y);

                var missile = GameManager.server.CreateEntity(rocketResourcePath, missilePos, new Quaternion(), true) as TimedExplosive;

                if (!missile)
                {
                    useMissileLauncher = false;
                    DestroyLauncher();
                    return;
                }

                missiles.Add(missile);
                missiles.RemoveAll(x => x == null || x.IsDestroyed);

                var gs = missile.gameObject.AddComponent<GuidanceSystem>();

                gs.Exclude(newmans);
                gs.SetTarget(container, _config.MissileLauncher.TargetChest);
                gs.Launch(_config.MissileLauncher.TargettingTime);
            }

            void SpawnFire()
            {
                var firePos = firePositions.GetRandom();
                int retries = firePositions.Count;

                while (Vector3.Distance(firePos, lastFirePos) < Radius * 0.35f && --retries > 0)
                {
                    firePos = firePositions.GetRandom();
                }

                SpawnFire(firePos);
                lastFirePos = firePos;
            }

            void SpawnFire(Vector3 firePos)
            {
                if (!useFireballs)
                    return;

                if (fireballs.Count >= 6) // limit fireballs
                {
                    foreach (var entry in fireballs)
                    {
                        if (entry != null && !entry.IsDestroyed)
                            entry.Kill();

                        fireballs.Remove(entry);
                        break;
                    }
                }

                var fireball = GameManager.server.CreateEntity(fireballPrefab, firePos, new Quaternion(), true) as FireBall;

                if (fireball == null)
                {
                    ins.Puts(_("Invalid Constant", null, fireballPrefab));
                    useFireballs = false;
                    CancelInvoke(SpawnFire);
                    firePositions.Clear();
                    return;
                }

                fireball.Spawn();
                fireball.damagePerSecond = _config.Fireballs.DamagePerSecond;
                fireball.generation = _config.Fireballs.Generation;
                fireball.lifeTimeMax = _config.Fireballs.LifeTimeMax;
                fireball.lifeTimeMin = _config.Fireballs.LifeTimeMin;
                fireball.radius = _config.Fireballs.Radius;
                fireball.tickRate = _config.Fireballs.TickRate;
                fireball.waterToExtinguish = _config.Fireballs.WaterToExtinguish;
                fireball.SendNetworkUpdate();
                fireball.Think();

                float lifeTime = UnityEngine.Random.Range(_config.Fireballs.LifeTimeMin, _config.Fireballs.LifeTimeMax);
                ins.timer.Once(lifeTime, () => fireball?.Extinguish());

                fireballs.Add(fireball);
            }

            public void Destruct()
            {
                if (_config.EventMessages.Destruct)
                {
                    var posStr = FormatGridReference(containerPos);

                    foreach (var target in BasePlayer.activePlayerList)
                        Message(target, ins.msg("OnChestDespawned", target.UserIDString, posStr));
                }

                if (container != null && !container.IsDestroyed)
                {
                    container.inventory.Clear();
                    container.Kill();
                }
            }

            void Unclaimed()
            {
                if (!started)
                    return;

                float time = claimTime - Time.realtimeSinceStartup;

                if (time < 60f)
                    return;

                string eventPos = FormatGridReference(containerPos);

                foreach (var target in BasePlayer.activePlayerList)
                    Message(target, ins.msg("DestroyingTreasure", target.UserIDString, eventPos, ins.FormatTime(time, target.UserIDString), _config.Settings.DistanceChatCommand));
            }

            public string GetUnlockTime(string userID = null)
            {
                return started ? null : ins.FormatTime(_unlockTime - Time.realtimeSinceStartup, userID);
            }

            public void Unlock()
            {
                if (unlock != null && !unlock.Destroyed)
                {
                    unlock.Destroy();
                }

                if (!started)
                {
                    DestroyFire();
                    DestroySphere();
                    DestroyLauncher();

                    if (_config.Event.DestructTime > 0f && destruct == null)
                        destruct = ins.timer.Once(_config.Event.DestructTime, () => Destruct());

                    if (_config.EventMessages.Started)
                        ins.PrintToChat(ins.msg(_config.Unlock.RequireAllNpcsDie && npcsSpawned ? "StartedNpcs" : "Started", null, FormatGridReference(containerPos)));

                    if (_config.UnlootedAnnouncements.Enabled)
                    {
                        claimTime = Time.realtimeSinceStartup + _config.Event.DestructTime;
                        announcement = ins.timer.Repeat(_config.UnlootedAnnouncements.Interval * 60f, 0, () => Unclaimed());
                    }

                    started = true;
                }

                if (_config.Unlock.RequireAllNpcsDie && npcsSpawned)
                {
                    if (npcs != null && npcs.Count > 0)
                    {
                        npcs.RemoveAll(npc => npc == null || npc.IsDestroyed || npc.IsDead());

                        if (npcs.Count > 0)
                        {
                            Invoke(Unlock, 1f);
                            return;
                        }
                    }
                }

                if (container.HasFlag(BaseEntity.Flags.Locked))
                    container.SetFlag(BaseEntity.Flags.Locked, false);
                if (container.HasFlag(BaseEntity.Flags.OnFire))
                    container.SetFlag(BaseEntity.Flags.OnFire, false);
            }

            public void SetUnlockTime(float time)
            {
                countdownTime = Convert.ToInt32(time);
                _unlockTime = Convert.ToInt64(Time.realtimeSinceStartup + time);

                if (_config.Unlock.RequireAllNpcsDie && !npcsSpawned || _config.Unlock.WhenNpcsDie && !npcsSpawned)
                {
                    ins.Puts("Npcs failed to spawn on navmesh, restarting event elsewhere...");
                    ins.TryOpenEvent();
                    Kill();
                    return;
                }
                
                unlock = ins.timer.Once(time, () => Unlock());

                if (_config.Countdown.Enabled && _config.Countdown.Times?.Count > 0 && countdownTime > 0)
                {
                    if (times.Count == 0)
                        times.AddRange(_config.Countdown.Times);

                    countdown = ins.timer.Repeat(1f, 0, () =>
                    {
                        countdownTime--;

                        if (started || times.Count == 0)
                        {
                            countdown.Destroy();
                            return;
                        }

                        if (times.Contains(countdownTime))
                        {
                            string eventPos = FormatGridReference(containerPos);

                            foreach (var target in BasePlayer.activePlayerList)
                                Message(target, ins.msg("Countdown", target.UserIDString, eventPos, ins.FormatTime(countdownTime, target.UserIDString)));

                            times.Remove(countdownTime);
                        }
                    });
                }
            }

            public void DestroyLauncher()
            {
                if (missilePositions.Count > 0)
                {
                    CancelInvoke(LaunchMissile);
                    missilePositions.Clear();
                }

                if (missiles.Count > 0)
                {
                    foreach (var entry in missiles)
                        if (entry != null && !entry.IsDestroyed)
                            entry.Kill();

                    missiles.Clear();
                }
            }

            public void DestroySphere()
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

            public void DestroyFire()
            {
                CancelInvoke(SpawnFire);
                firePositions.Clear();

                if (fireballs.Count > 0)
                {
                    foreach (var fireball in fireballs)
                    {
                        if (fireball != null && !fireball.IsDestroyed)
                        {
                            fireball.Kill();
                        }
                    }

                    fireballs.Clear();
                }

                newmanProtections.RemoveAll(x => protects.Contains(x));
                traitors.Clear();
                newmans.Clear();
                protects.Clear();
            }
        }

        void OnNewSave(string filename) => wipeChestsSeed = true;

        void Init()
        {
            SubscribeHooks(false);
        }

        void OnServerInitialized()
        {
            if (!configLoaded)
            {
                Puts("Failed to load config; exiting OnServerInitialized hook");
                return;
            }

            unloading = False;
            ins = this;
            dataFile = Interface.Oxide.DataFileSystem.GetFile(Name);

            boxShortname = StringPool.Get(2735448871);
            boxPrefab = StringPool.Get(2206646561);
            radiusMarkerPrefab = StringPool.Get(2849728229);
            scientistPrefab = StringPool.Get(4223875851);
            murdererPrefab = StringPool.Get(3879041546);
            vendingPrefab = StringPool.Get(3459945130);
            explosionPrefab = StringPool.Get(4060989661);

            try
            {
                storedData = dataFile.ReadObject<StoredData>();
            }
            catch { }

            if (storedData?.Players == null)
                storedData = new StoredData();

            if (_config.Event.Automated)
            {
                if (storedData.SecondsUntilEvent != double.MinValue)
                    if (storedData.SecondsUntilEvent - Facepunch.Math.Epoch.Current > _config.Event.IntervalMax) // Allows users to lower max event time
                        storedData.SecondsUntilEvent = double.MinValue;

                eventTimer = timer.Once(1f, () => CheckSecondsUntilEvent());
            }

            if (!string.IsNullOrEmpty(storedData.CustomPosition))
                sd_customPos = storedData.CustomPosition.ToVector3();
            else
                sd_customPos = Vector3.zero;

            if (!wipeChestsSeed && BuildingManager.server.buildingDictionary.Count == 0)
            {
                if (storedData.Players.Count > 0 && storedData.Players.Values.Any(pi => pi.StolenChestsSeed > 0))
                {
                    wipeChestsSeed = true;
                }
            }

            if (wipeChestsSeed)
            {
                if (storedData.Players.Count > 0)
                {
                    var ladder = storedData.Players.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.StolenChestsSeed).ToList<KeyValuePair<string, int>>();

                    if (AssignTreasureHunters(ladder))
                    {
                        foreach (string key in storedData.Players.Keys)
                        {
                            storedData.Players[key].StolenChestsSeed = 0;
                        }

                        sd_customPos = Vector3.zero;
                        storedData.CustomPosition = "";
                        wipeChestsSeed = false;
                        SaveData();
                    }
                }
                else
                {
                    sd_customPos = Vector3.zero;
                    storedData.CustomPosition = "";
                    wipeChestsSeed = false;
                    SaveData();
                }
            }

            useMissileLauncher = _config.MissileLauncher.Enabled;
            useSpheres = _config.Event.Spheres;
            useFireballs = _config.Fireballs.Enabled;
            useRockets = _config.Rocket.Enabled;
            rocketDef = ItemManager.FindItemDefinition(_config.Rocket.FireRockets ? fireRocketShortname : basicRocketShortname);

            if (!rocketDef)
            {
                ins.Puts(_("Invalid Constant", null, _config.Rocket.FireRockets ? fireRocketShortname : basicRocketShortname));
                useRockets = false;
            }
            else
                rocketResourcePath = rocketDef.GetComponent<ItemModProjectile>().projectileObject.resourcePath;

            boxDef = ItemManager.FindItemDefinition(boxShortname);

            if (!boxDef)
            {
                Puts(_("Invalid Constant", null, boxShortname));
                useRandomSkins = false;
            }

            if (ZoneManager != null)
            {
                var zoneIds = ZoneManager?.Call("GetZoneIDs");

                if (zoneIds != null && zoneIds is string[])
                {
                    foreach (var zoneId in (string[])zoneIds)
                    {
                        var zoneLoc = ZoneManager?.Call("GetZoneLocation", zoneId);

                        if (zoneLoc is Vector3 && (Vector3)zoneLoc != Vector3.zero)
                        {
                            var position = (Vector3)zoneLoc;
                            var zoneRadius = ZoneManager?.Call("GetZoneRadius", zoneId);
                            float distance = 0f;

                            if (zoneRadius is float && (float)zoneRadius > 0f)
                            {
                                distance = (float)zoneRadius;
                            }
                            else
                            {
                                var zoneSize = ZoneManager?.Call("GetZoneSize", zoneId);
                                if (zoneSize is Vector3 && (Vector3)zoneSize != Vector3.zero)
                                {
                                    var size = (Vector3)zoneSize;
                                    distance = Mathf.Max(size.x, size.y);
                                }
                            }

                            if (distance > 0f)
                            {
                                distance += _config.Event.Radius + 5f;
                                managedZones[position] = distance;
                            }
                        }
                    }
                }

                if (managedZones.Count > 0)
                    Puts("Blocking events at zones: {0}", string.Join(", ", managedZones.Select(zone => string.Format("{0} ({1}: {2}m)", zone.Key.ToString().Replace("(", "").Replace(")", ""), FormatGridReference(zone.Key), zone.Value)).ToArray()));
            }

            monuments.Clear();
            allowedMonuments.Clear();

            string name = null;
            foreach (var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                name = monument.displayPhrase.english;
                if (monument.name.Contains("cave")) name = "Cave:" + RandomString(5, 5);
                else if (monument.name.Contains("power_sub")) name = monument.name.Substring(monument.name.LastIndexOf("/") + 1).Replace(".prefab", "") + ":" + RandomString(5, 5);
                else if (monuments.ContainsKey(name)) name += ":" + RandomString(5, 5);
                float radius = GetMonumentFloat(name);
                monuments[name] = new MonInfo() { Position = monument.transform.position, Radius = radius };
            }

            if (monuments.Count > 0) allowedMonuments = monuments.ToDictionary(k => k.Key, k => k.Value);

            if (!_config.Monuments.Underground)
            {
                allowedMonuments.Remove("Sewer Branch");

                foreach (var mon in allowedMonuments.ToList())
                {
                    if (mon.Key.Contains("Cave"))
                    {
                        allowedMonuments.Remove(mon.Key);
                    }
                }
            }

            foreach (var mon in allowedMonuments.ToList())
            {
                name = mon.Key.Contains(":") ? mon.Key.Substring(0, mon.Key.LastIndexOf(":")) : mon.Key.TrimEnd();

                if (name.Contains("Oil Rig") || _config.Monuments.Blacklist.Any(str => name.ToLower().Trim() == str.ToLower().Trim()))
                {
                    allowedMonuments.Remove(mon.Key);
                }
            }

            init = true;
            RemoveAllTemporaryMarkers();

            if (_config.Skins.RandomWorkshopSkins || _config.Treasure.RandomWorkshopSkins) SetWorkshopIDs(); // webrequest.Enqueue("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", null, GetWorkshopIDs, this, Core.Libraries.RequestMethod.GET);
        }

        void Unload()
        {
            if (!init)
                return;

            unloading = True;

            foreach (var chest in treasureChests.Values)
            {
                if (chest != null)
                {
                    Puts(_("Destroyed Treasure Chest", null, chest.containerPos));
                    chest.Kill();
                    if (chest.container.IsValid())
                    {
                        chest.container.dropChance = 0f;

                        if (!chest.container.IsDestroyed)
                        {
                            chest.container.Kill();
                        }
                    }
                }
            }

            if (lustyMarkers.Count > 0)
                foreach (var entry in lustyMarkers.ToList())
                    RemoveLustyMarker(entry.Key);

            if (mapMarkers.Count > 0)
                foreach (var entry in mapMarkers.ToList())
                    RemoveMapMarker(entry.Key);

            BlockedLayers.Clear();
            eventTimer?.Destroy();
            indestructibleWarnings.Clear();
            skinsCache.Clear();
            workshopskinsCache.Clear();
            RemoveAllTemporaryMarkers();
        }

        object canTeleport(BasePlayer player)
        {
            return init && EventTerritory(player.transform.position) ? msg("CannotTeleport", player.UserIDString) : null;
        }

        object CanTeleport(BasePlayer player)
        {
            return init && EventTerritory(player.transform.position) ? msg("CannotTeleport", player.UserIDString) : null;
        }

        object CanBradleyApcTarget(BradleyAPC apc, NPCPlayerApex npc)
        {
            return npc != null && TreasureChest.HasNPC(npc.userID) ? (object)False : null;
        }

        object CanBeTargeted(BasePlayer player, MonoBehaviour behaviour)
        {
            if (player.IsValid())
            {
                if (newmanProtections.Contains(player.net.ID) || TreasureChest.HasNPC(player.userID))
                {
                    return False;
                }
            }

            return null;
        }

        object OnNpcDestinationSet(NPCPlayerApex npc, Vector3 newDestination)
        {
            if (npc == null || !npc.GetNavAgent.isOnNavMesh)
            {
                return true;
            }

            if (TreasureChest.IsTooFar(npc.userID))
            {
                return true;
            }

            return null;
        }

        object OnNpcTarget(BasePlayer player, BasePlayer target)
        {
            if (player == null || target == null)
            {
                return null;
            }

            if (TreasureChest.HasNPC(player.userID) && target.IsNpc)
            {
                return True;
            }
            else if (TreasureChest.HasNPC(target.userID) && player.IsNpc)
            {
                return True;
            }
            else if (newmanProtections.Contains(target.net.ID))
            {
                return True;
            }

            return null;
        }

        object OnNpcTarget(BaseNpc npc, BasePlayer target)
        {
            if (npc == null || target == null)
            {
                return null;
            }

            if (TreasureChest.HasNPC(target.userID) || newmanProtections.Contains(target.net.ID))
            {
                return True;
            }

            return null;
        }

        void OnPlayerDeath(NPCPlayerApex player)
        {
            if (!init || !player.IsValid())
                return;

            var chest = TreasureChest.GetNPC(player.userID);

            if (chest != null)
            {
                player.svActiveItemID = 0;
                player.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                if (_config.NPC.DespawnInventory) player.inventory.Strip();

                if (_config.Unlock.WhenNpcsDie && chest.npcs.Count <= 1)
                {
                    NextTick(() =>
                    {
                        if (chest != null)
                        {
                            chest.Unlock();
                        }
                    });
                }
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!init || entity == null || entity.transform == null)
                return;

            if (entity is BaseLock)
            {
                NextTick(() =>
                {
                    if (entity != null && !entity.IsDestroyed)
                    {
                        foreach (var x in treasureChests.Values)
                        {
                            if (entity.HasParent() && entity.GetParentEntity() == x.container)
                            {
                                entity.KillMessage();
                                break;
                            }
                        }
                    }
                });
            }

            if (!_config.NPC.Enabled)
            {
                return;
            }

            var corpse = entity as NPCPlayerCorpse;

            if (corpse == null)
            {
                return;
            }

            var chest = TreasureChest.GetNPC(corpse.playerSteamID);

            if (chest == null)
            {
                return;
            }

            if (_config.NPC.DespawnInventory) corpse.Invoke(corpse.KillMessage, 30f);
            chest.npcs.RemoveAll(npc => npc.userID == corpse.playerSteamID);

            foreach (var x in treasureChests.Values)
            {
                if (x.npcs.Count > 0)
                {
                    return;
                }
            }

            Unsubscribe(nameof(CanBeTargeted));
            Unsubscribe(nameof(OnNpcTarget));
            Unsubscribe(nameof(OnNpcDestinationSet));
            Unsubscribe(nameof(CanBradleyApcTarget));
            Unsubscribe(nameof(OnPlayerDeath));
        }

        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            var player = planner?.GetOwnerPlayer();

            if (!player || player.IsAdmin) return null;

            var chest = TreasureChest.Get(player.transform.position);

            if (chest != null)
            {
                Message(player, msg("Building is blocked!", player.UserIDString));
                return False;
            }

            return null;
        }

        void OnLootEntity(BasePlayer player, BoxStorage container)
        {
            if (!init || !player || !container.IsValid() || !treasureChests.ContainsKey(container.net.ID))
                return;

            if (player.isMounted)
            {
                Message(player, msg("CannotBeMounted", player.UserIDString));
                NextTick(player.EndLooting);
                return;
            }
            else looters[container.net.ID] = player.UserIDString;

            if (_config.EventMessages.FirstOpened)
            {
                var chest = treasureChests[container.net.ID];

                if (!chest.opened)
                {
                    chest.opened = true;
                    var posStr = FormatGridReference(container.transform.position);

                    foreach (var target in BasePlayer.activePlayerList)
                        Message(target, msg("OnChestOpened", target.UserIDString, player.displayName, posStr));
                }
            }
        }

        bool IsOnSameTeam(ulong playerId, ulong targetId)
        {
            RelationshipManager.PlayerTeam team1;
            if (!RelationshipManager.ServerInstance.playerToTeam.TryGetValue(playerId, out team1))
            {
                return false;
            }

            RelationshipManager.PlayerTeam team2;
            if (!RelationshipManager.ServerInstance.playerToTeam.TryGetValue(targetId, out team2))
            {
                return false;
            }

            return team1.teamID == team2.teamID;
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (container?.entityOwner == null || !(container.entityOwner is StorageContainer))
                return;

            NextTick(() =>
            {
                var box = container?.entityOwner as StorageContainer;

                if (!box.IsValid() || !treasureChests.ContainsKey(box.net.ID))
                    return;

                var looter = item.GetOwnerPlayer();

                if (looter != null)
                {
                    if (looters.ContainsKey(box.net.ID))
                        looters.Remove(box.net.ID);

                    looters.Add(box.net.ID, looter.UserIDString);
                }

                if (box.inventory.itemList.Count == 0)
                {
                    if (looter == null && looters.ContainsKey(box.net.ID))
                        looter = BasePlayer.Find(looters[box.net.ID]);

                    if (looter != null)
                    {
                        if (_config.RankedLadder.Enabled)
                        {
                            if (!storedData.Players.ContainsKey(looter.UserIDString))
                                storedData.Players.Add(looter.UserIDString, new PlayerInfo());

                            storedData.Players[looter.UserIDString].StolenChestsTotal++;
                            storedData.Players[looter.UserIDString].StolenChestsSeed++;
                            SaveData();
                        }

                        var posStr = FormatGridReference(looter.transform.position);

                        Puts(_("Thief", null, posStr, looter.displayName));

                        if (_config.EventMessages.Thief)
                        {
                            foreach (var target in BasePlayer.activePlayerList)
                                Message(target, msg("Thief", target.UserIDString, posStr, looter.displayName));
                        }

                        looter.EndLooting();

                        if (_config.Rewards.Economics && _config.Rewards.Money > 0)
                        {
                            if (Economics != null)
                            {
                                Economics?.Call("Deposit", looter.UserIDString, _config.Rewards.Money);
                                Message(looter, msg("EconomicsDeposit", looter.UserIDString, _config.Rewards.Money));
                            }
                        }

                        if (_config.Rewards.ServerRewards && _config.Rewards.Points > 0)
                        {
                            if (ServerRewards != null)
                            {
                                var success = ServerRewards?.Call("AddPoints", looter.userID, (int)_config.Rewards.Points);

                                if (success != null && success is bool && (bool)success)
                                    Message(looter, msg("ServerRewardPoints", looter.UserIDString, (int)_config.Rewards.Points));
                            }
                        }
                    }

                    RemoveLustyMarker(box.net.ID);
                    RemoveMapMarker(box.net.ID);

                    if (!box.IsDestroyed)
                        box.Kill();

                    if (treasureChests.Count == 0)
                        SubscribeHooks(false);
                }
            });
        }

        object CanEntityBeTargeted(BasePlayer player, BaseEntity target)
        {
            return player.IsValid() && !player.IsNpc && target.IsValid() && EventTerritory(player.transform.position) && IsTrueDamage(target) ? (object)True : null;
        }

        object CanEntityTrapTrigger(BaseTrap trap, BasePlayer player)
        {
            return player.IsValid() && !player.IsNpc && trap.IsValid() && EventTerritory(player.transform.position) ? (object)True : null;
        }

        object CanEntityTakeDamage(BaseEntity entity, HitInfo hitInfo) // TruePVE!!!! <3 @ignignokt84
        {
            if (!entity.IsValid() || hitInfo == null || hitInfo.Initiator == null)
            {
                return null;
            }

            if (hitInfo.Initiator.IsNpc || hitInfo.Initiator is AutoTurret)
            {
                var npc = entity as NPCPlayerApex;

                if (npc.IsValid() && TreasureChest.HasNPC(npc.userID))
                {
                    return true;
                }
            }

            var attacker = hitInfo.Initiator as BasePlayer;

            if (_config.TruePVE.ServerWidePVP && attacker.IsValid() && entity is BasePlayer && treasureChests.Count > 0) // 1.2.9 & 1.3.3 & 1.6.4
            {
                return true;
            }

            if (EventTerritory(entity.transform.position)) // 1.5.8 & 1.6.4
            {
                if (entity is NPCPlayerCorpse)
                {
                    return true;
                }

                if (attacker.IsValid())
                {
                    if (EventTerritory(attacker.transform.position) && _config.TruePVE.AllowPVPAtEvents && entity is BasePlayer) // 1.2.9
                    {
                        return True;
                    }

                    if (EventTerritory(attacker.transform.position) && _config.TruePVE.AllowBuildingDamageAtEvents) // 1.3.3
                    {
                        return True;
                    }
                }

                if (IsTrueDamage(hitInfo.Initiator))
                {
                    return True;
                }
            }

            return null; // 1.6.4 rewrite
        }

        bool IsTrueDamage(BaseEntity entity)
        {
            if (!entity.IsValid())
            {
                return False;
            }

            return entity is AutoTurret || entity is BearTrap || entity is FlameTurret || entity is Landmine || entity is GunTrap || entity is ReactiveTarget || entity.name.Contains("spikes.floor") || entity is FireBall;
        }

        bool EventTerritory(Vector3 target, float f = 10f)
        {
            foreach (var x in treasureChests.Values)
            {
                if (Vector3Ex.Distance2D(x.containerPos, target) <= x.Radius + f)
                {
                    return true;
                }
            }

            return false;
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (!init || !entity.IsValid() || hitInfo == null)
                return;

            bool isNewman = newmanProtections.Contains(entity.net.ID);
            bool isChest = treasureChests.ContainsKey(entity.net.ID);
            var attacker = hitInfo.InitiatorPlayer;

            if (isNewman || isChest)
            {
                if (attacker.IsValid())
                {
                    if (hitInfo.hasDamage)
                    {
                        hitInfo.damageTypes.ScaleAll(0f);
                    }

                    if (!attacker.IsNpc)
                    {
                        ulong uid = attacker.userID;

                        if (!indestructibleWarnings.Contains(uid))
                        {
                            indestructibleWarnings.Add(uid);
                            timer.Once(10f, () => indestructibleWarnings.Remove(uid));
                            Message(attacker, msg(isChest ? "Indestructible" : "Newman Protected", attacker.UserIDString));
                        }

                        return;
                    }
                }
            }

            if (attacker.IsValid() && attacker is Scientist && TreasureChest.HasNPC(attacker as NPCPlayerApex) && _config.NPC.Accuracy < UnityEngine.Random.Range(0f, 100f))
            {
                hitInfo.damageTypes = new DamageTypeList(); // don't hurt a player when the shot misses
                hitInfo.DidHit = False;
                hitInfo.DoHitEffects = False;
                hitInfo.HitEntity = null;
                return;
            }

            var npc = entity as NPCPlayerApex;

            if (!TreasureChest.HasNPC(npc))
            {
                return;
            }

            if (hitInfo.hasDamage && !attacker.IsValid() && !(hitInfo.Initiator is AutoTurret)) // make npc's immune to fire/explosions/other
            {
                hitInfo.damageTypes = new DamageTypeList();
            }
            else
            {
                HumanAttackOperator.AttackEnemy(npc.AiContext, IsMelee(npc) ? AttackOperator.AttackType.CloseRange : AttackOperator.AttackType.LongRange);
                
                if (npc.Stats.AggressionRange < 150f)
                {
                    npc.Stats.AggressionRange += 150f;
                    npc.Stats.DeaggroRange += 100f;
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

        void SaveData() => dataFile.WriteObject(storedData);

        static void Message(BasePlayer target, string message)
        {
            if (target == null)
                return;

            ins.Player.Message(target, message, 0uL);
        }

        void SubscribeHooks(bool flag)
        {
            if (flag && init)
            {
                if (_config.NPC.Enabled)
                {
                    Subscribe(nameof(OnPlayerDeath));
                }

                if (_config.TruePVE.AllowPVPAtEvents || _config.TruePVE.ServerWidePVP)
                {
                    Subscribe(nameof(CanEntityTakeDamage));
                }

                Subscribe(nameof(OnNpcTarget));
                Subscribe(nameof(OnNpcDestinationSet));
                Subscribe(nameof(OnEntitySpawned));
                Subscribe(nameof(CanBradleyApcTarget));
                Subscribe(nameof(OnEntityTakeDamage));
                Subscribe(nameof(OnItemRemovedFromContainer));
                Subscribe(nameof(OnLootEntity));
                Subscribe(nameof(CanBuild));
                Subscribe(nameof(CanTeleport));
                Subscribe(nameof(canTeleport));
                Subscribe(nameof(CanBeTargeted));
            }
            else
            {
                Unsubscribe(nameof(CanTeleport));
                Unsubscribe(nameof(canTeleport));
                Unsubscribe(nameof(CanBeTargeted));
                Unsubscribe(nameof(CanEntityTakeDamage));
                Unsubscribe(nameof(CanBradleyApcTarget));
                Unsubscribe(nameof(OnNpcTarget));
                Unsubscribe(nameof(OnEntitySpawned));
                Unsubscribe(nameof(OnEntityTakeDamage));
                Unsubscribe(nameof(OnItemRemovedFromContainer));
                Unsubscribe(nameof(OnLootEntity));
                Unsubscribe(nameof(CanBuild));
                Unsubscribe(nameof(OnPlayerDeath));
            }
        }

        void SetWorkshopIDs()
        {
            try
            {
                foreach (var def in ItemManager.GetItemDefinitions())
                {
                    var skins = Rust.Workshop.Approved.All.Values.Where(skin => !string.IsNullOrEmpty(skin.Skinnable.ItemName) && skin.Skinnable.ItemName == def.shortname).Select(skin => System.Convert.ToUInt64(skin.WorkshopdId)).ToList();

                    if (skins != null && skins.Count > 0)
                    {
                        if (!workshopskinsCache.ContainsKey(def.shortname))
                        {
                            workshopskinsCache.Add(def.shortname, new List<ulong>());
                        }

                        foreach (ulong skin in skins)
                        {
                            if (!workshopskinsCache[def.shortname].Contains(skin))
                            {
                                workshopskinsCache[def.shortname].Add(skin);
                            }
                        }

                        skins.Clear();
                        skins = null;
                    }
                }
            }
            catch { }
        }

        static List<Vector3> GetRandomPositions(Vector3 destination, float radius, int amount, float y)
        {
            var positions = new List<Vector3>();

            if (amount <= 0)
                return positions;

            int retries = 100;
            float space = (radius / amount); // space each rocket out from one another

            for (int i = 0; i < amount; i++)
            {
                var position = destination + UnityEngine.Random.insideUnitSphere * radius;

                position.y = y != 0f ? y : UnityEngine.Random.Range(100f, 200f);

                var match = Vector3.zero;

                foreach (var p in positions)
                {
                    if (Vector3.Distance(p, position) < space)
                    {
                        match = p;
                        break;
                    }
                }

                if (match != Vector3.zero)
                {
                    if (--retries < 0)
                        break;

                    i--;
                    continue;
                }

                retries = 100;
                positions.Add(position);
            }

            return positions;
        }

        List<Vector3> _cachedPos = new List<Vector3>();

        public Vector3 GetEventPosition()
        {
            if (sd_customPos != Vector3.zero)
                return sd_customPos;

            float radius = _config.Event.Radius / 2f;

            if (_config.Monuments.Chance > 0f)
            {
                var value = UnityEngine.Random.value;

                if (value <= _config.Monuments.Chance && allowedMonuments.Count > 0)
                {
                    return GetMonumentDropPosition();
                }
            }

            if (_config.Monuments.Only && allowedMonuments.Count > 0)
            {
                return GetMonumentDropPosition();
            }

            int maxRetries = 500;
            Vector3 eventPos;

            do
            {
                eventPos = GetSafeDropPosition(RandomDropPosition());

                if (eventPos == Vector3.zero) continue;
                if (_cachedPos.Count > maxRetries) _cachedPos.Clear();

                if (!_cachedPos.Contains(eventPos) && radius <= 50)
                {
                    foreach (var position in _cachedPos)
                    {
                        if ((position - eventPos).magnitude <= radius)
                        {
                            maxRetries++;
                            continue;
                        }
                    }

                    _cachedPos.Add(eventPos);
                }

                foreach (var x in treasureChests.Values)
                {
                    if (Vector3.Distance(x.containerPos, eventPos) <= x.Radius * 2)
                    {
                        eventPos = Vector3.zero;
                        continue;
                    }
                }

                if (managedZones.Count > 0)
                {
                    foreach (var zone in managedZones)
                    {
                        if (Vector3.Distance(zone.Key, eventPos) <= zone.Value)
                        {
                            eventPos = Vector3.zero; // blocked by zone manager
                            continue;
                        }
                    }
                }

                if (IsMonumentPosition(eventPos)) // don't put the treasure chest near a monument
                {
                    eventPos = Vector3.zero;
                    continue;
                }

                if (Convert.ToBoolean(Duelist?.Call("DuelistTerritory", eventPos)))
                {
                    eventPos = Vector3.zero;
                    continue;
                }

                if (Convert.ToBoolean(RaidableBases?.Call("EventTerritory", eventPos)))
                {
                    eventPos = Vector3.zero;
                    continue;
                }
            } while (eventPos == Vector3.zero && --maxRetries > 0);

            return eventPos;
        }

        public Vector3 GetSafeDropPosition(Vector3 position)
        {
            RaycastHit hit;
            position.y += 200f;

            if (Physics.Raycast(position, Vector3.down, out hit, position.y, heightLayer, QueryTriggerInteraction.Ignore))
            {
                if (!BlockedLayers.Contains(hit.collider.gameObject.layer))
                {
                    position.y = Mathf.Max(hit.point.y, TerrainMeta.HeightMap.GetHeight(position));

                    if (!IsLayerBlocked(position, _config.Event.Radius + 25f, obstructionLayer))
                    {
                        return position;
                    }
                }
            }

            return Vector3.zero;
        }

        public bool IsLayerBlocked(Vector3 position, float radius, int mask)
        {
            var colliders = new List<Collider>();
            Vis.Colliders<Collider>(position, radius, colliders, mask, QueryTriggerInteraction.Ignore);

            colliders.RemoveAll(collider => (collider.ToBaseEntity()?.IsNpc ?? false) || !(collider.ToBaseEntity()?.OwnerID.IsSteamId() ?? true));

            bool blocked = colliders.Count > 0;

            colliders.Clear();
            colliders = null;

            return blocked;
        }

        private bool IsInOrOnRock(Vector3 position, string meshName)
        {
            bool flag = False;
            int hits = Physics.OverlapSphereNonAlloc(position, 2f, Vis.colBuffer, worldLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                if (Vis.colBuffer[i].name.StartsWith(meshName)) flag = True;
                Vis.colBuffer[i] = null;
            }
            if (!flag)
            {
                float y = TerrainMeta.HighestPoint.y + 250f;
                RaycastHit hit;
                if (Physics.Raycast(position, Vector3.up, out hit, y, worldLayer, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.name.StartsWith(meshName)) flag = True;
                }
                if (!flag && Physics.Raycast(position, Vector3.down, out hit, y, worldLayer, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.name.StartsWith(meshName)) flag = True;
                }
                if (!flag && Physics.Raycast(position + new Vector3(0f, y, 0f), Vector3.down, out hit, y + 1f, worldLayer, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.name.StartsWith(meshName)) flag = True;
                }
            }
            return flag;
        }

        public Vector3 GetRandomMonumentDropPosition(Vector3 position)
        {
            foreach (var monument in allowedMonuments)
            {
                if (Vector3.Distance(monument.Value.Position, position) < 75f)
                {
                    int attempts = 100;

                    while (--attempts > 0)
                    {
                        var randomPoint = monument.Value.Position + UnityEngine.Random.insideUnitSphere * 75f;
                        randomPoint.y = 100f;

                        RaycastHit hit;
                        if (Physics.Raycast(randomPoint, Vector3.down, out hit, 100.5f, heightLayer, QueryTriggerInteraction.Ignore))
                        {
                            if (hit.point.y - TerrainMeta.HeightMap.GetHeight(hit.point) < 3f)
                            {
                                if (!IsLayerBlocked(hit.point, _config.Event.Radius + 25f, obstructionLayer))
                                {
                                    return hit.point;
                                }
                            }
                        }
                    }
                }
            }

            return Vector3.zero;
        }

        public bool IsMonumentPosition(Vector3 target)
        {
            foreach (var monument in monuments)
            {
                var position = new Vector3(monument.Value.Position.x, target.y, monument.Value.Position.z);

                if ((position - target).magnitude < monument.Value.Radius)
                {
                    return True;
                }
            }

            return False;
        }

        public Vector3 GetMonumentDropPosition()
        {
            var list = allowedMonuments.ToList();
            var monPosition = Vector3.zero;

            while (monPosition == Vector3.zero && list.Count > 0)
            {
                var mon = list.GetRandom();

                foreach (var x in treasureChests.Values) // 1.3.1
                {
                    if (Vector3.Distance(x.containerPos, mon.Value.Position) <= _config.Event.Radius) //* 2)
                    {
                        list.Remove(mon);
                        break;
                    }
                }

                if (!list.Contains(mon))
                {
                    continue;
                }

                if (!IsLayerBlocked(mon.Value.Position, _config.Event.Radius + 25f, obstructionLayer))
                {
                    monPosition = mon.Value.Position;
                    break;
                }

                list.Remove(mon);
            }

            if (monPosition == Vector3.zero)
            {
                return Vector3.zero;
            }

            var entities = new List<BaseNetworkable>();

            foreach (var e in BaseNetworkable.serverEntities) // TODO: cache this
            {
                if (e != null && e.transform != null && !entities.Contains(e) && (e.transform.position - monPosition).magnitude < _config.Event.Radius && IsValidSpawn(e))
                {
                    entities.Add(e);
                }
            }

            if (!_config.Monuments.Underground)
            {
                entities.RemoveAll(e => e.transform.position.y < monPosition.y);
            }

            if (entities.Count < 2)
            {
                var pos = GetRandomMonumentDropPosition(monPosition);
                return pos == Vector3.zero ? GetMonumentDropPosition() : pos;
            }

            var entity = entities.GetRandom();
            var position = entity.transform.position;

            if (entity is LootContainer) entity.Kill();

            return position;
        }

        public Vector3 RandomDropPosition() // CargoPlane.RandomDropPosition()
        {
            var vector = Vector3.zero;
            float num = 100f, x = TerrainMeta.Size.x / 3f;
            do
            {
                vector = Vector3Ex.Range(-x, x);
            }
            while (filter.GetFactor(vector) == 0f && (num -= 1f) > 0f);
            vector.y = 0f;
            return vector;
        }

        List<ulong> GetItemSkins(ItemDefinition def)
        {
            if (!skinsCache.ContainsKey(def.shortname))
            {
                var skins = def.skins.Select(skin => Convert.ToUInt64(skin.id)).ToList();

                if ((def.shortname == boxShortname && _config.Skins.RandomWorkshopSkins) || (def.shortname != boxShortname && _config.Treasure.RandomWorkshopSkins))
                {
                    if (workshopskinsCache.ContainsKey(def.shortname))
                    {
                        foreach (ulong skin in workshopskinsCache[def.shortname])
                        {
                            if (!skins.Contains(skin))
                            {
                                skins.Add(skin);
                            }
                        }

                        workshopskinsCache.Remove(def.shortname);
                    }
                }

                skinsCache.Add(def.shortname, new List<ulong>());

                foreach (ulong skin in skins)
                {
                    if (!skinsCache[def.shortname].Contains(skin))
                    {
                        skinsCache[def.shortname].Add(skin);
                    }
                }

                skins.Clear();
                skins = null;
            }

            return skinsCache[def.shortname];
        }

        TreasureChest TryOpenEvent(BasePlayer player = null)
        {
            var eventPos = Vector3.zero;

            if (player.IsValid())
            {
                RaycastHit hit;

                if (!Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity))
                {
                    return null;
                }

                eventPos = hit.point;
            }
            else
            {
                var randomPos = GetEventPosition();

                if (randomPos == Vector3.zero)
                {
                    return null;
                }

                eventPos = randomPos;
            }

            var container = GameManager.server.CreateEntity(boxPrefab, eventPos, new Quaternion(), true) as StorageContainer;

            if (container == null)
            {
                return null;
            }

            if (_config.Skins.PresetSkin != 0uL)
                container.skinID = _config.Skins.PresetSkin;
            else if (useRandomSkins && boxDef != null)
            {
                var skins = GetItemSkins(boxDef);

                if (skins.Count > 0)
                {
                    container.skinID = skins.GetRandom();
                    container.SendNetworkUpdate();
                }
            }

            container.SetFlag(BaseEntity.Flags.Locked, true);
            container.SetFlag(BaseEntity.Flags.OnFire, true);
            if (!container.isSpawned) container.Spawn();

            TreasureChest.SetLoot(container, ChestLoot);

            var chest = container.gameObject.AddComponent<TreasureChest>();
            chest.Radius = _config.Event.Radius;

            if (container.enableSaving)
            {
                container.enableSaving = false;
                BaseEntity.saveList.Remove(container);
            }

            uint uid = container.net.ID;
            float unlockTime = UnityEngine.Random.Range(_config.Unlock.MinTime, _config.Unlock.MaxTime);

            SubscribeHooks(true);
            treasureChests.Add(uid, chest);
            
            var posStr = FormatGridReference(container.transform.position);
            ins.Puts("{0}: {1}", posStr, string.Join(", ", container.inventory.itemList.Select(item => string.Format("{0} ({1})", item.info.displayName.translated, item.amount)).ToArray()));

            //if (!_config.Event.SpawnMax && treasureChests.Count > 1)
            //{
            //    AnnounceEventSpawn(container, unlockTime, posStr);
            //}

            foreach (var target in BasePlayer.activePlayerList)
            {
                double distance = Math.Round(Vector3.Distance(target.transform.position, container.transform.position), 2);
                string unlockStr = FormatTime(unlockTime, target.UserIDString);
                string message = msg("Opened", target.UserIDString, posStr, unlockStr, distance, _config.Settings.DistanceChatCommand);

                if (_config.EventMessages.Opened)
                {
                    Message(target, message);
                }

                if (_config.GUIAnnouncement.Enabled && GUIAnnouncements != null && distance <= _config.GUIAnnouncement.Distance)
                {
                    GUIAnnouncements?.Call("CreateAnnouncement", message, _config.GUIAnnouncement.TintColor, _config.GUIAnnouncement.TextColor, target);
                }

                if (useRockets && _config.EventMessages.Barrage)
                    Message(target, msg("Barrage", target.UserIDString, _config.Rocket.Amount));

                if (_config.Event.DrawTreasureIfNearby && _config.Event.AutoDrawDistance > 0f && distance <= _config.Event.AutoDrawDistance)
                    DrawText(target, container.transform.position, msg("Treasure Chest", target.UserIDString, distance));
            }

            var position = container.transform.position;
            storedData.TotalEvents++;
            SaveData();

            if (_config.LustyMap.Enabled)
                AddLustyMarker(position, uid);

            if (Map)
                AddMapMarker(position, uid);

            string monumentName = null;

            foreach (var x in monuments)
            {
                if (Vector3.Distance(x.Value.Position, position) <= 75f)
                {
                    monumentName = x.Key;
                    break;
                }
            }

            chest.Invoke(chest.SpawnNpcs, 1f);
            chest.Invoke(() => chest.SetUnlockTime(unlockTime), 2f);

            if (!string.IsNullOrEmpty(monumentName))
            {
                foreach (var value in _config.NPC.BlacklistedMonuments)
                {
                    if (monumentName.ToLower().Trim() == value.ToLower().Trim())
                    {
                        return chest;
                    }
                }
            }

            return chest;
        }

        private void AnnounceEventSpawn()
        {
            foreach (var target in BasePlayer.activePlayerList)
            {
                string message = msg("OpenedX", target.UserIDString, _config.Settings.DistanceChatCommand);

                if (_config.EventMessages.Opened)
                {
                    Message(target, message);
                }

                if (_config.GUIAnnouncement.Enabled && GUIAnnouncements != null)
                {
                    foreach (var chest in treasureChests)
                    {
                        double distance = Math.Round(Vector3.Distance(target.transform.position, chest.Value.containerPos), 2);
                        string unlockStr = FormatTime(chest.Value.countdownTime, target.UserIDString);
                        var posStr = FormatGridReference(chest.Value.containerPos);
                        string text = msg2("Opened", target.UserIDString, posStr, unlockStr, distance, _config.Settings.DistanceChatCommand);

                        if (distance <= _config.GUIAnnouncement.Distance)
                        {
                            GUIAnnouncements?.Call("CreateAnnouncement", text, _config.GUIAnnouncement.TintColor, _config.GUIAnnouncement.TextColor, target);
                        }

                        if (_config.Event.DrawTreasureIfNearby && _config.Event.AutoDrawDistance > 0f && distance <= _config.Event.AutoDrawDistance)
                        {
                            DrawText(target, chest.Value.containerPos, msg2("Treasure Chest", target.UserIDString, distance));
                        }
                    }
                }

                if (useRockets && _config.EventMessages.Barrage)
                    Message(target, msg("Barrage", target.UserIDString, _config.Rocket.Amount));
            }
        }

        private void AnnounceEventSpawn(StorageContainer container, float unlockTime, string posStr)
        {
            foreach (var target in BasePlayer.activePlayerList)
            {
                double distance = Math.Round(Vector3.Distance(target.transform.position, container.transform.position), 2);
                string unlockStr = FormatTime(unlockTime, target.UserIDString);
                string message = msg("Opened", target.UserIDString, posStr, unlockStr, distance, _config.Settings.DistanceChatCommand);

                if (_config.EventMessages.Opened)
                {
                    Message(target, message);
                }

                if (_config.GUIAnnouncement.Enabled && GUIAnnouncements != null && distance <= _config.GUIAnnouncement.Distance)
                {
                    GUIAnnouncements?.Call("CreateAnnouncement", message, _config.GUIAnnouncement.TintColor, _config.GUIAnnouncement.TextColor, target);
                }

                if (useRockets && _config.EventMessages.Barrage)
                {
                    Message(target, msg("Barrage", target.UserIDString, _config.Rocket.Amount));
                }

                if (_config.Event.DrawTreasureIfNearby && _config.Event.AutoDrawDistance > 0f && distance <= _config.Event.AutoDrawDistance)
                {
                    DrawText(target, container.transform.position, msg2("Treasure Chest", target.UserIDString, distance));
                }
            }
        }

        private void API_SetContainer(StorageContainer container, float radius, bool spawnNpcs) // Expansion Mode for Raidable Bases plugin
        {
            if (!container.IsValid())
            {
                return;
            }

            container.SetFlag(BaseEntity.Flags.Locked, true);
            container.SetFlag(BaseEntity.Flags.OnFire, true);

            var chest = container.gameObject.AddComponent<TreasureChest>();
            float unlockTime = UnityEngine.Random.Range(_config.Unlock.MinTime, _config.Unlock.MaxTime);

            chest.Radius = radius;
            treasureChests.Add(container.net.ID, chest);
            chest.Invoke(() => chest.SetUnlockTime(unlockTime), 2f);
            storedData.TotalEvents++;
            SaveData();

            Subscribe(nameof(OnEntityTakeDamage));
            Subscribe(nameof(OnItemRemovedFromContainer));
            Subscribe(nameof(OnLootEntity));

            if (spawnNpcs)
            {
                Subscribe(nameof(OnNpcTarget));
                Subscribe(nameof(OnNpcDestinationSet));
                Subscribe(nameof(CanBeTargeted));
                Subscribe(nameof(OnEntitySpawned));
                Subscribe(nameof(OnPlayerDeath));
                Subscribe(nameof(CanBradleyApcTarget));
                chest.Invoke(chest.SpawnNpcs, 1f);
            }
        }

        char[] chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();
        readonly StringBuilder _sb = new StringBuilder();

        string RandomString(int min = 5, int max = 10)
        {
            _sb.Length = 0;

            for (int i = 0; i <= UnityEngine.Random.Range(min, max); i++)
                _sb.Append(chars[UnityEngine.Random.Range(0, chars.Length)]);

            return _sb.ToString();
        }

        float GetMonumentFloat(string monumentName)
        {
            string name = (monumentName.Contains(":") ? monumentName.Substring(0, monumentName.LastIndexOf(":")) : monumentName).TrimEnd();

            switch (name)
            {
                case "Abandoned Cabins":
                    return 24f + 30f;
                case "Abandoned Supermarket":
                    return 50f;
                case "Airfield":
                    return 200f;
                case "Bandit Camp":
                    return 100f + 25f;
                case "Cave":
                    return 75f;
                case "Giant Excavator Pit":
                    return 200f + 25f;
                case "Harbor":
                    return 100f + 50f;
                case "HQM Quarry":
                    return 27.5f + 10f;
                case "Large Oil Rig":
                    return 200f;
                case "Launch Site":
                    return 200f + 100f;
                case "Lighthouse":
                    return 24f + 24f;
                case "Military Tunnel":
                    return 100f;
                case "Mining Outpost":
                    return 25f + 15f;
                case "Oil Rig":
                    return 100f;
                case "Outpost":
                    return 100f + 25f;
                case "Oxum's Gas Station":
                    return 50f + 15f;
                case "Power Plant":
                    return 100f + 40f;
                case "power_sub_small_1":
                case "power_sub_small_2":
                case "power_sub_big_1":
                case "power_sub_big_2":
                    return 30f;
                case "Satellite Dish":
                    return 75f + 15f;
                case "Sewer Branch":
                    return 75f + 25f;
                case "Stone Quarry":
                    return 27.5f;
                case "Sulfur Quarry":
                    return 27.5f;
                case "The Dome":
                    return 50f + 20f;
                case "Train Yard":
                    return 100 + 50f;
                case "Water Treatment Plant":
                    return 100f + 85f;
                case "Water Well":
                    return 24f;
                case "Wild Swamp":
                    return 24f;
            }

            return 200f;
        }

        void CheckSecondsUntilEvent()
        {
            var eventInterval = UnityEngine.Random.Range(_config.Event.IntervalMin, _config.Event.IntervalMax);
            float stamp = Facepunch.Math.Epoch.Current;

            if (storedData.SecondsUntilEvent == double.MinValue) // first time users
            {
                storedData.SecondsUntilEvent = stamp + eventInterval;
                Puts(_("Next Automated Event", null, FormatTime(eventInterval), DateTime.Now.AddSeconds(eventInterval).ToString()));
                //Puts(_("View Config"));
                SaveData();
            }

            if (_config.Event.Automated && storedData.SecondsUntilEvent - stamp <= 0 && treasureChests.Count < _config.Event.Max && BasePlayer.activePlayerList.Count >= _config.Event.PlayerLimit)
            {
                bool save = false;

                if (_config.Event.SpawnMax)
                {
                    save = TryOpenEvent() != null && treasureChests.Count >= _config.Event.Max;
                }
                else save = TryOpenEvent() != null;

                if (save)
                {
                    if (_config.Event.SpawnMax && treasureChests.Count > 1)
                    {
                        AnnounceEventSpawn();
                    }

                    storedData.SecondsUntilEvent = stamp + eventInterval;
                    Puts(_("Next Automated Event", null, FormatTime(eventInterval), DateTime.Now.AddSeconds(eventInterval).ToString()));
                    SaveData();
                }
            }

            eventTimer = timer.Once(1f, () => CheckSecondsUntilEvent());
        }

        public static string FormatGridReference(Vector3 position) // Credit: Jake_Rich. Fix by trixxxi (y,x -> x,y switch)
        {
            string monumentName = null;

            foreach (var x in monuments)  // request MrSmallZzy
            {
                if ((x.Value.Position - position).magnitude <= x.Value.Radius)
                {
                    monumentName = (x.Key.Contains(":") ? x.Key.Substring(0, x.Key.LastIndexOf(":")) : x.Key).TrimEnd();
                    break;
                }
            }

            if (_config.Settings.ShowXZ)
            {
                string pos = string.Format("{0} {1}", position.x.ToString("N2"), position.z.ToString("N2"));
                return string.IsNullOrEmpty(monumentName) ? pos : $"{monumentName} ({pos})";
            }

            return string.IsNullOrEmpty(monumentName) ? PositionToGrid(position) : $"{monumentName} ({PositionToGrid(position)})";
        }

        private static string PositionToGrid(Vector3 position) // Credit: Whispers88/redBGDR/Dana
        {
            var r = new Vector2(World.Size / 2 + position.x, World.Size / 2 + position.z);
            var c = Mathf.Floor(r.x / 146.3f) / 26;
            var x = Mathf.Floor(r.x / 146.3f) % 26;
            var z = Mathf.Floor(World.Size / 146.3f) - Mathf.Floor(r.y / 146.3f);
            var s = c >= 1 ? (char)('A' + (c - 1)) : ' ';
            return $"{s}{(char)('A' + x)}{z - 1}";
        }

        string FormatTime(double seconds, string id = null)
        {
            if (seconds == 0)
            {
                if (BasePlayer.activePlayerList.Count < _config.Event.PlayerLimit)
                    return msg("Not Enough Online", null, _config.Event.PlayerLimit);
                else
                    return "0s";
            }

            var ts = TimeSpan.FromSeconds(seconds);

            return string.Format("{0:D2}h {1:D2}m {2:D2}s", ts.Hours, ts.Minutes, ts.Seconds);
        }

        bool AssignTreasureHunters(List<KeyValuePair<string, int>> ladder)
        {
            foreach (var target in covalence.Players.All)
            {
                if (target == null || string.IsNullOrEmpty(target.Id))
                    continue;

                if (permission.UserHasPermission(target.Id, _config.RankedLadder.Permission))
                    permission.RevokeUserPermission(target.Id, _config.RankedLadder.Permission);

                if (permission.UserHasGroup(target.Id, _config.RankedLadder.Group))
                    permission.RemoveUserGroup(target.Id, _config.RankedLadder.Group);
            }

            if (!_config.RankedLadder.Enabled)
                return true;

            foreach (var entry in ladder.ToList())
                if (entry.Value < 1)
                    ladder.Remove(entry);

            if (ladder.Count == 0)
                return true;

            ladder.Sort((x, y) => y.Value.CompareTo(x.Value));

            int permsGiven = 0;

            for (int i = 0; i < ladder.Count; i++)
            {
                var target = covalence.Players.FindPlayerById(ladder[i].Key);

                if (target == null || target.IsBanned || target.IsAdmin)
                    continue;

                permission.GrantUserPermission(target.Id, _config.RankedLadder.Permission, this);
                permission.AddUserGroup(target.Id, _config.RankedLadder.Group);

                LogToFile("treasurehunters", DateTime.Now.ToString() + " : " + msg("Log Stolen", null, target.Name, target.Id, ladder[i].Value), this, true);
                Puts(_("Log Granted", null, target.Name, target.Id, _config.RankedLadder.Permission, _config.RankedLadder.Group));

                if (++permsGiven >= _config.RankedLadder.Amount)
                    break;
            }

            if (permsGiven > 0)
            {
                string file = string.Format("{0}{1}{2}_{3}-{4}.txt", Interface.Oxide.LogDirectory, System.IO.Path.DirectorySeparatorChar, Name.Replace(" ", "").ToLower(), "treasurehunters", DateTime.Now.ToString("yyyy-MM-dd"));
                Puts(_("Log Saved", null, file));
            }

            return true;
        }

        void AddMapMarker(Vector3 position, uint uid)
        {
            mapMarkers[uid] = new MapInfo { IconName = _config.LustyMap.IconName, Position = position, Url = _config.LustyMap.IconFile };
            Map?.Call("ApiAddPointUrl", _config.LustyMap.IconFile, _config.LustyMap.IconName, position);
            storedData.Markers.Add(uid);
        }

        void RemoveMapMarker(uint uid)
        {
            if (!mapMarkers.ContainsKey(uid))
                return;

            var mapInfo = mapMarkers[uid];

            Map?.Call("ApiRemovePointUrl", mapInfo.Url, mapInfo.IconName, mapInfo.Position);
            mapMarkers.Remove(uid);
            storedData.Markers.Remove(uid);
        }

        void AddLustyMarker(Vector3 pos, uint uid)
        {
            string name = string.Format("{0}_{1}", _config.LustyMap.IconName, storedData.TotalEvents).ToLower();

            LustyMap?.Call("AddTemporaryMarker", pos.x, pos.z, name, _config.LustyMap.IconFile, _config.LustyMap.IconRotation);
            lustyMarkers[uid] = name;
            storedData.Markers.Add(uid);
        }

        void RemoveLustyMarker(uint uid)
        {
            if (!lustyMarkers.ContainsKey(uid))
                return;

            LustyMap?.Call("RemoveTemporaryMarker", lustyMarkers[uid]);
            lustyMarkers.Remove(uid);
            storedData.Markers.Remove(uid);
        }

        void RemoveAllTemporaryMarkers()
        {
            if (storedData.Markers.Count == 0)
                return;

            if (LustyMap)
            {
                foreach (uint marker in storedData.Markers)
                {
                    LustyMap.Call("RemoveMarker", marker.ToString());
                }
            }

            if (Map)
            {
                foreach (uint marker in storedData.Markers.ToList())
                {
                    RemoveMapMarker(marker);
                }
            }

            storedData.Markers.Clear();
            SaveData();
        }

        void RemoveAllMarkers()
        {
            int removed = 0;

            for (int i = 0; i < storedData.TotalEvents + 1; i++)
            {
                string name = string.Format("{0}_{1}", _config.LustyMap.IconName, i).ToLower();

                if ((bool)(LustyMap?.Call("RemoveMarker", name) ?? false))
                {
                    removed++;
                }
            }

            storedData.Markers.Clear();

            if (removed > 0)
            {
                Puts("Removed {0} existing markers", removed);
            }
            else
                Puts("No markers found");
        }

        void DrawText(BasePlayer player, Vector3 drawPos, string text)
        {
            if (!player || !player.IsConnected || drawPos == Vector3.zero || string.IsNullOrEmpty(text) || _config.Event.DrawTime < 1f)
                return;

            bool isAdmin = player.IsAdmin;

            try
            {
                if (_config.Event.GrantDraw && !player.IsAdmin)
                {
                    var uid = player.userID;

                    if (!drawGrants.Contains(uid))
                    {
                        drawGrants.Add(uid);
                        timer.Once(_config.Event.DrawTime, () => drawGrants.Remove(uid));
                    }

                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    player.SendNetworkUpdateImmediate();
                }

                if (player.IsAdmin || drawGrants.Contains(player.userID))
                    player.SendConsoleCommand("ddraw.text", _config.Event.DrawTime, Color.yellow, drawPos, text);
            }
            catch (Exception ex)
            {
                _config.Event.GrantDraw = false;
                Puts("DrawText Exception: {0} --- {1}", ex.Message, ex.StackTrace);
                Puts("Disabled drawing for players!");
            }

            if (!isAdmin)
            {
                if (player.HasPlayerFlag(BasePlayer.PlayerFlags.IsAdmin))
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    player.SendNetworkUpdateImmediate();
                }
            }
        }

        public static bool IsValidSpawn(BaseNetworkable e)
        {
            var entity = e as BaseEntity;

            if (!entity.IsValid() || entity.OwnerID.IsSteamId())
                return false;

            if (e.GetComponentInParent<BasePlayer>() && !entity.IsNpc)
                return false;

            return !(e is AutoTurret) && !(e is Door) && !(e is ResourceEntity);
        }

        void AddItem(BasePlayer player, string[] args)
        {
            if (args.Length >= 2)
            {
                string shortname = args[0];
                var itemDef = ItemManager.FindItemDefinition(shortname);

                if (itemDef == null)
                {
                    Message(player, msg("InvalidItem", player.UserIDString, shortname, _config.Settings.DistanceChatCommand));
                    return;
                }

                int amount;
                if (int.TryParse(args[1], out amount))
                {
                    if (itemDef.stackable == 1 || (itemDef.condition.enabled && itemDef.condition.max > 0f) || amount < 1)
                        amount = 1;

                    ulong skin = 0uL;

                    if (args.Length >= 3)
                    {
                        ulong num;
                        if (ulong.TryParse(args[2], out num))
                            skin = num;
                        else
                            Message(player, msg("InvalidValue", player.UserIDString, args[2]));
                    }

                    int minAmount = amount;
                    if (args.Length >= 4)
                    {
                        int num;
                        if (int.TryParse(args[3], out num))
                            minAmount = num;
                        else
                            Message(player, msg("InvalidValue", player.UserIDString, args[3]));
                    }

                    foreach (var loot in ChestLoot)
                    {
                        if (loot.shortname == shortname)
                        {
                            loot.amount = amount;
                            loot.skin = skin;
                            loot.amountMin = minAmount;
                        }
                    }

                    SaveConfig();
                    Message(player, msg("AddedItem", player.UserIDString, shortname, amount, skin));
                }
                else
                    Message(player, msg("InvalidValue", player.UserIDString, args[2]));

                return;
            }

            Message(player, msg("InvalidItem", player.UserIDString, args.Length >= 1 ? args[0] : "?", _config.Settings.DistanceChatCommand));
        }

        void cmdTreasureHunter(BasePlayer player, string command, string[] args)
        {
            if (drawGrants.Contains(player.userID))
                return;

            if (args.Length >= 1 && (args[0].ToLower() == "ladder" || args[0].ToLower() == "lifetime") && _config.RankedLadder.Enabled)
            {
                if (storedData.Players.Count == 0)
                {
                    Message(player, msg("Ladder Insufficient Players", player.UserIDString));
                    return;
                }

                if (args.Length == 2 && args[1] == "resetme")
                    if (storedData.Players.ContainsKey(player.UserIDString))
                        storedData.Players[player.UserIDString].StolenChestsSeed = 0;

                int rank = 0;
                var ladder = storedData.Players.ToDictionary(k => k.Key, v => args[0].ToLower() == "ladder" ? v.Value.StolenChestsSeed : v.Value.StolenChestsTotal).Where(kvp => kvp.Value > 0).ToList();
                ladder.Sort((x, y) => y.Value.CompareTo(x.Value));

                Message(player, msg(args[0].ToLower() == "ladder" ? "Ladder" : "Ladder Total", player.UserIDString));

                foreach (var kvp in ladder)
                {
                    string name = covalence.Players.FindPlayerById(kvp.Key)?.Name ?? kvp.Key;
                    string value = kvp.Value.ToString("N0");

                    Message(player, msg("TreasureHunter", player.UserIDString, ++rank, name, value));

                    if (rank >= 10)
                        break;
                }

                return;
            }

            if (_config.RankedLadder.Enabled)
                Message(player, msg("Wins", player.UserIDString, storedData.Players.ContainsKey(player.UserIDString) ? storedData.Players[player.UserIDString].StolenChestsSeed : 0, _config.Settings.DistanceChatCommand));

            if (args.Length >= 1 && player.IsAdmin)
            {
                if (args[0] == "markers")
                {
                    RemoveAllMarkers();
                    return;
                }
                if (args[0] == "resettime")
                {
                    storedData.SecondsUntilEvent = double.MinValue;
                    return;
                }
                if (args[0] == "now")
                {
                    storedData.SecondsUntilEvent = Facepunch.Math.Epoch.Current;
                    return;
                }
                if (args[0] == "tp" && treasureChests.Count > 0)
                {
                    float dist = float.MaxValue;
                    var dest = Vector3.zero;

                    foreach (var entry in treasureChests)
                    {
                        var v3 = Vector3.Distance(entry.Value.containerPos, player.transform.position);

                        if (treasureChests.Count > 1 && v3 < 25f) // 0.2.0 fix - move admin to the next nearest chest
                            continue;

                        if (v3 < dist)
                        {
                            dist = v3;
                            dest = entry.Value.containerPos;
                        }
                    }

                    if (dest != Vector3.zero)
                        player.Teleport(dest);
                }

                if (args[0].ToLower() == "additem")
                {
                    AddItem(player, args.Skip(1).ToArray());
                    return;
                }
            }

            if (treasureChests.Count == 0)
            {
                double time = storedData.SecondsUntilEvent - Facepunch.Math.Epoch.Current;

                if (time < 0)
                    time = 0;

                Message(player, msg("Next", player.UserIDString, FormatTime(time, player.UserIDString)));
                return;
            }

            foreach (var chest in treasureChests)
            {
                double distance = Math.Round(Vector3.Distance(player.transform.position, chest.Value.containerPos), 2);
                string posStr = FormatGridReference(chest.Value.containerPos);

                Message(player, chest.Value.GetUnlockTime() != null ? msg("Info", player.UserIDString, chest.Value.GetUnlockTime(player.UserIDString), posStr, distance, _config.Settings.DistanceChatCommand) : msg("Already", player.UserIDString, posStr, distance, _config.Settings.DistanceChatCommand));
                if (_config.Settings.AllowDrawText) DrawText(player, chest.Value.containerPos, msg("Treasure Chest", player.UserIDString, distance));
            }
        }

        void ccmdDangerousTreasures(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (!(player?.IPlayer?.HasPermission(_config.Settings.PermName) ?? arg.IsAdmin))
            {
                arg.ReplyWith(msg("No Permission", player?.UserIDString ?? null));
                return;
            }

            if (arg.HasArgs() && arg.Args.Length == 1)
            {
                if (arg.Args[0].ToLower() == "help")
                {
                    if (arg.IsRcon || arg.IsServerside)
                    {
                        Puts("Monuments:");
                        foreach (var m in monuments) Puts(m.Key);
                    }

                    arg.ReplyWith(msg("Help", player?.UserIDString ?? null, _config.Settings.EventChatCommand));
                }
                else if (arg.Args[0].ToLower() == "5") storedData.SecondsUntilEvent = Facepunch.Math.Epoch.Current + 5f;

                return;
            }

            var position = Vector3.zero;
            bool isDigit = arg.HasArgs() && arg.Args.Any(x => x.All(char.IsDigit));
            bool isTeleport = arg.HasArgs() && arg.Args.Any(x => x.ToLower() == "tp");

            int num = 0, amount = isDigit ? Convert.ToInt32(arg.Args.First(x => x.All(char.IsDigit))) : 1;

            if (amount < 1)
                amount = 1;

            if (treasureChests.Count >= _config.Event.Max && !arg.IsServerside)
            {
                arg.ReplyWith(RemoveFormatting(msg("Max Manual Events", player?.UserIDString, _config.Event.Max)));
                return;
            }

            for (int i = 0; i < amount; i++)
            {
                if (treasureChests.Count >= _config.Event.Max)
                {
                    break;
                }

                var chest = TryOpenEvent();

                if (chest != null)
                {
                    position = chest.containerPos;
                    num++;
                }
            }


            if (position != Vector3.zero)
            {
                if (arg.HasArgs() && isTeleport && (player?.IsAdmin ?? false))
                {
                    player.Teleport(position);
                }
            }
            else
            {
                if (position == Vector3.zero)
                    arg.ReplyWith(msg("Manual Event Failed", player?.UserIDString ?? null));
                else
                    ins.Puts(_("Invalid Constant", null, boxPrefab + " : Command()"));
            }

            if (num > 1)
            {
                arg.ReplyWith(msg("OpenedEvents", player?.UserIDString ?? null, num, amount));
            }
        }

        void cmdDangerousTreasures(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _config.Settings.PermName) && !player.IsAdmin)
            {
                Message(player, msg("No Permission", player.UserIDString));
                return;
            }

            if (args.Length == 1)
            {
                var arg = args[0].ToLower();

                if (arg == "help")
                {
                    Message(player, "Monuments: " + string.Join(", ", monuments.Select(m => m.Key)));
                    Message(player, msg("Help", player.UserIDString, _config.Settings.EventChatCommand));
                    return;
                }
                else if (player.IsAdmin)
                {
                    if (arg == "custom")
                    {
                        if (string.IsNullOrEmpty(storedData.CustomPosition))
                        {
                            storedData.CustomPosition = player.transform.position.ToString();
                            sd_customPos = player.transform.position;
                            Message(player, msg("CustomPositionSet", player.UserIDString, storedData.CustomPosition));
                        }
                        else
                        {
                            storedData.CustomPosition = "";
                            sd_customPos = Vector3.zero;
                            Message(player, msg("CustomPositionRemoved", player.UserIDString));
                        }
                        SaveData();
                        return;
                    }
                }
            }

            if (treasureChests.Count >= _config.Event.Max && player.net.connection.authLevel < 2)
            {
                Message(player, msg("Max Manual Events", player.UserIDString, _config.Event.Max));
                return;
            }

            var chest = TryOpenEvent(args.Length == 1 && args[0] == "me" && player.IsAdmin ? player : null);
            if (chest != null)
            {
                if (args.Length == 1 && args[0].ToLower() == "tp" && player.IsAdmin)
                {
                    player.Teleport(chest.containerPos);
                }
            }
            else
            {
                Message(player, msg("Manual Event Failed", player.UserIDString));
            }
        }

        #region Config

        Dictionary<string, Dictionary<string, string>> GetMessages()
        {
            return new Dictionary<string, Dictionary<string, string>>
            {
                {"No Permission", new Dictionary<string, string>() {
                    {"en", "You do not have permission to use this command."},
                }},
                {"Building is blocked!", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>Building is blocked near treasure chests!</color>"},
                }},
                {"Max Manual Events", new Dictionary<string, string>() {
                    {"en", "Maximum number of manual events <color=#FF0000>{0}</color> has been reached!"},
                }},
                {"Dangerous Zone Protected", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>You have entered a dangerous zone protected by a fire aura! You must leave before you die!</color>"},
                }},
                {"Dangerous Zone Unprotected", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>You have entered a dangerous zone!</color>"},
                }},
                {"Manual Event Failed", new Dictionary<string, string>() {
                    {"en", "Event failed to start! Unable to obtain a valid position. Please try again."},
                }},
                {"Help", new Dictionary<string, string>() {
                    {"en", "/{0} <tp> - start a manual event, and teleport to the position if TP argument is specified and you are an admin."},
                }},
                {"Started", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>The event has started at <color=#FFFF00>{0}</color>! The protective fire aura has been obliterated!</color>"},
                }},
                {"StartedNpcs", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>The event has started at <color=#FFFF00>{0}</color>! The protective fire aura has been obliterated! Npcs must be killed before the treasure will become lootable.</color>"},
                }},
                {"Opened", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>An event has opened at <color=#FFFF00>{0}</color>! Event will start in <color=#FFFF00>{1}</color>. You are <color=#FFA500>{2}m</color> away. Use <color=#FFA500>/{3}</color> for help.</color>"},
                }},
                {"OpenedX", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0><color=#FFFF00>Multiple events have opened! Use <color=#FFA500>/{0}</color> for help.</color>"},
                }},
                {"Barrage", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>A barrage of <color=#FFFF00>{0}</color> rockets can be heard at the location of the event!</color>"},
                }},
                {"Info", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>Event will start in <color=#FFFF00>{0}</color> at <color=#FFFF00>{1}</color>. You are <color=#FFA500>{2}m</color> away.</color>"},
                }},
                {"Already", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>The event has already started at <color=#FFFF00>{0}</color>! You are <color=#FFA500>{1}m</color> away.</color>"},
                }},
                {"Next", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>No events are open. Next event in <color=#FFFF00>{0}</color></color>"},
                }},
                {"Thief", new Dictionary<string, string>() {
                    {"en", "<color=#C0C0C0>The treasures at <color=#FFFF00>{0}</color> have been stolen by <color=#FFFF00>{1}</color>!</color>"},
                }},
                {"Wins", new Dictionary<string, string>()
                {
                    {"en", "<color=#C0C0C0>You have stolen <color=#FFFF00>{0}</color> treasure chests! View the ladder using <color=#FFA500>/{1} ladder</color> or <color=#FFA500>/{1} lifetime</color></color>"},
                }},
                {"Ladder", new Dictionary<string, string>()
                {
                    {"en", "<color=#FFFF00>[ Top 10 Treasure Hunters (This Wipe) ]</color>:"},
                }},
                {"Ladder Total", new Dictionary<string, string>()
                {
                    {"en", "<color=#FFFF00>[ Top 10 Treasure Hunters (Lifetime) ]</color>:"},
                }},
                {"Ladder Insufficient Players", new Dictionary<string, string>()
                {
                    {"en", "<color=#FFFF00>No players are on the ladder yet!</color>"},
                }},
                {"Event At", new Dictionary<string, string>() {
                    {"en", "Event at {0}"},
                }},
                {"Next Automated Event", new Dictionary<string, string>() {
                    {"en", "Next automated event in {0} at {1}"},
                }},
                {"Not Enough Online", new Dictionary<string, string>() {
                    {"en", "Not enough players online ({0} minimum)"},
                }},
                {"Treasure Chest", new Dictionary<string, string>() {
                    {"en", "Treasure Chest <color=#FFA500>{0}m</color>"},
                }},
                {"Invalid Constant", new Dictionary<string, string>() {
                    {"en", "Invalid constant {0} - please notify the author!"},
                }},
                {"Destroyed Treasure Chest", new Dictionary<string, string>() {
                    {"en", "Destroyed a left over treasure chest at {0}"},
                }},
                {"Indestructible", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>Treasure chests are indestructible!</color>"},
                }},
                {"View Config", new Dictionary<string, string>() {
                    {"en", "Please view the config if you haven't already."},
                }},
                {"Newman Enter", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>To walk with clothes is to set one-self on fire. Tread lightly.</color>"},
                }},
                {"Newman Traitor Burn", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>Tempted by the riches you have defiled these grounds. Vanish from these lands or PERISH!</color>"},
                }},
                {"Newman Traitor", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>Tempted by the riches you have defiled these grounds. Vanish from these lands!</color>"},
                }},
                {"Newman Protected", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>This newman is temporarily protected on these grounds!</color>"},
                }},
                {"Newman Protect", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>You are protected on these grounds. Do not defile them.</color>"},
                }},
                {"Newman Protect Fade", new Dictionary<string, string>() {
                    {"en", "<color=#FF0000>Your protection has faded.</color>"},
                }},
                {"Log Stolen", new Dictionary<string, string>() {
                    {"en", "{0} ({1}) chests stolen {2}"},
                }},
                {"Log Granted", new Dictionary<string, string>() {
                    {"en", "Granted {0} ({1}) permission {2} for group {3}"},
                }},
                {"Log Saved", new Dictionary<string, string>() {
                    {"en", "Treasure Hunters have been logged to: {0}"},
                }},
                {"MessagePrefix", new Dictionary<string, string>() {
                    {"en", "[ <color=#406B35>Dangerous Treasures</color> ] "},
                }},
                {"Countdown", new Dictionary<string, string>()
                {
                    {"en", "<color=#C0C0C0>Event at <color=#FFFF00>{0}</color> will start in <color=#FFFF00>{1}</color>!</color>"},
                }},
                {"RestartDetected", new Dictionary<string, string>()
                {
                    {"en", "Restart detected. Next event in {0} minutes."},
                }},
                {"DestroyingTreasure", new Dictionary<string, string>()
                {
                    {"en", "<color=#C0C0C0>The treasure at <color=#FFFF00>{0}</color> will be destroyed by fire in <color=#FFFF00>{1}</color> if not looted! Use <color=#FFA500>/{2}</color> to find this chest.</color>"},
                }},
                {"EconomicsDeposit", new Dictionary<string, string>()
                {
                    {"en", "You have received <color=#FFFF00>${0}</color> for stealing the treasure!"},
                }},
                {"ServerRewardPoints", new Dictionary<string, string>()
                {
                    {"en", "You have received <color=#FFFF00>{0} RP</color> for stealing the treasure!"},
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
                {"OnFirstPlayerEntered", new Dictionary<string, string>()
                {
                    {"en", "<color=#FFFF00>{0}</color> is the first to enter the dangerous treasure event at <color=#FFFF00>{1}</color>"},
                }},
                {"OnChestOpened", new Dictionary<string, string>() {
                    {"en", "<color=#FFFF00>{0}</color> is the first to see the treasures at <color=#FFFF00>{1}</color>!</color>"},
                }},
                {"OnChestDespawned", new Dictionary<string, string>() {
                    {"en", "The treasures at <color=#FFFF00>{0}</color> have been lost forever! Better luck next time."},
                }},
                {"CannotBeMounted", new Dictionary<string, string>() {
                    {"en", "You cannot loot the treasure while mounted!"},
                }},
                {"CannotTeleport", new Dictionary<string, string>() {
                    {"en", "You are not allowed to teleport from this event."},
                }},
                {"TreasureHunter", new Dictionary<string, string>() {
                    {"en", "<color=#ADD8E6>{0}</color>. <color=#C0C0C0>{1}</color> (<color=#FFFF00>{2}</color>)"},
                }},
                {"Timed Event", new Dictionary<string, string>() {
                    {"en", "<color=#FFFF00>You cannot loot until the fire aura expires! Tread lightly, the fire aura is very deadly!</color>)"},
                }},
                {"Timed Npc Event", new Dictionary<string, string>() {
                    {"en", "<color=#FFFF00>You cannot loot until you kill all of the npcs and wait for the fire aura to expire! Tread lightly, the fire aura is very deadly!</color>)"},
                }},
                {"Npc Event", new Dictionary<string, string>() {
                    {"en", "<color=#FFFF00>You cannot loot until you kill all of the npcs surrounding the fire aura! Tread lightly, the fire aura is very deadly!</color>)"},
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

        static int GetPercentIncreasedAmount(int amount)
        {
            if (_config.Treasure.UseDOWL && !_config.Treasure.Increased && _config.Treasure.PercentLoss > 0m)
            {
                return UnityEngine.Random.Range(Convert.ToInt32(amount - (amount * _config.Treasure.PercentLoss)), amount);
            }

            decimal percentIncrease = 0m;

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
                case DayOfWeek.Sunday:
                    {
                        percentIncrease = _config.Treasure.PercentIncreaseOnSunday;
                        break;
                    }
            }

            if (percentIncrease > 1m)
            {
                percentIncrease /= 100;
            }

            if (percentIncrease > 0m)
            {
                amount = Convert.ToInt32(amount + (amount * percentIncrease));

                if (_config.Treasure.PercentLoss > 0m)
                {
                    amount = UnityEngine.Random.Range(Convert.ToInt32(amount - (amount * _config.Treasure.PercentLoss)), amount);
                }
            }

            return amount;
        }

        public static Color __(string hex)
        {
            var c = hex.TrimStart('#');                         // strip #
            var rgb = int.Parse(c, NumberStyles.HexNumber);     // convert rrggbb to decimal
            var r = (rgb >> 16) & 0xff;                         // extract red
            var g = (rgb >> 8) & 0xff;                          // extract green
            var b = (rgb >> 0) & 0xff;                          // extract blue

            return new Color(r, g, b);
        }

        static string _(string key, string id = null, params object[] args)
        {
            return ins.RemoveFormatting(ins.msg(key, id, args));
        }

        string msg(string key, string id = null, params object[] args)
        {
            string message = _config.EventMessages.Prefix && id != null && id != "server_console" ? lang.GetMessage("MessagePrefix", this, null) + lang.GetMessage(key, this, id) : lang.GetMessage(key, this, id);

            return message.Contains("{0}") ? string.Format(message, args) : message;
        }

        string msg2(string key, string id, params object[] args)
        {
            string message = lang.GetMessage(key, this, id);

            return message.Contains("{0}") ? string.Format(message, args) : message;
        }

        string RemoveFormatting(string source) => source.Contains(">") ? System.Text.RegularExpressions.Regex.Replace(source, "<.*?>", string.Empty) : source;
        #endregion

        #region Configuration

        private static Configuration _config;

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

        public class PluginSettings
        {
            [JsonProperty(PropertyName = "Permission Name")]
            public string PermName { get; set; } = "dangeroustreasures.use";

            [JsonProperty(PropertyName = "Event Chat Command")]
            public string EventChatCommand { get; set; } = "dtevent";

            [JsonProperty(PropertyName = "Distance Chat Command")]
            public string DistanceChatCommand { get; set; } = "dtd";

            [JsonProperty(PropertyName = "Draw Location On Screen With Distance Command")]
            public bool AllowDrawText { get; set; } = True;

            [JsonProperty(PropertyName = "Event Console Command")]
            public string EventConsoleCommand { get; set; } = "dtevent";

            [JsonProperty(PropertyName = "Show X Z Coordinates")]
            public bool ShowXZ { get; set; } = False;
        }

        public class CountdownSettings
        {
            [JsonProperty(PropertyName = "Use Countdown Before Event Starts")]
            public bool Enabled { get; set; } = False;

            [JsonProperty(PropertyName = "Time In Seconds", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<int> Times { get; set; } = new List<int> { 120, 60, 30, 15 };
        }

        public class EventSettings
        {
            [JsonProperty(PropertyName = "Automated")]
            public bool Automated { get; set; } = False;

            [JsonProperty(PropertyName = "Every Min Seconds")]
            public float IntervalMin { get; set; } = 3600f;

            [JsonProperty(PropertyName = "Every Max Seconds")]
            public float IntervalMax { get; set; } = 7200f;

            [JsonProperty(PropertyName = "Use Vending Map Marker")]
            public bool MarkerVending { get; set; } = True;

            [JsonProperty(PropertyName = "Use Explosion Map Marker")]
            public bool MarkerExplosion { get; set; } = False;

            [JsonProperty(PropertyName = "Marker Color")]
            public string MarkerColor { get; set; } = "#FF0000";

            [JsonProperty(PropertyName = "Marker Radius")]
            public float MarkerRadius { get; set; } = 0.25f;

            [JsonProperty(PropertyName = "Marker Event Name")]
            public string MarkerName { get; set; } = "Dangerous Treasures Event";

            [JsonProperty(PropertyName = "Max Manual Events")]
            public int Max { get; set; } = 1;

            [JsonProperty(PropertyName = "Always Spawn Max Manual Events")]
            public bool SpawnMax { get; set; }

            [JsonProperty(PropertyName = "Amount Of Items To Spawn")]
            public int TreasureAmount { get; set; } = 6;

            [JsonProperty(PropertyName = "Use Spheres")]
            public bool Spheres { get; set; } = True;

            [JsonProperty(PropertyName = "Amount Of Spheres")]
            public int SphereAmount { get; set; } = 5;

            [JsonProperty(PropertyName = "Player Limit For Event")]
            public int PlayerLimit { get; set; } = 1;

            [JsonProperty(PropertyName = "Fire Aura Radius (Advanced Users Only)")]
            public float Radius { get; set; } = 25f;

            [JsonProperty(PropertyName = "Auto Draw On New Event For Nearby Players")]
            public bool DrawTreasureIfNearby { get; set; } = False;

            [JsonProperty(PropertyName = "Auto Draw Minimum Distance")]
            public float AutoDrawDistance { get; set; } = 300f;

            [JsonProperty(PropertyName = "Grant DDRAW temporarily to players")]
            public bool GrantDraw { get; set; } = True;

            [JsonProperty(PropertyName = "Grant Draw Time")]
            public float DrawTime { get; set; } = 15f;

            [JsonProperty(PropertyName = "Time To Loot")]
            public float DestructTime { get; set; } = 900f;
        }

        public class EventMessageSettings
        {
            [JsonProperty(PropertyName = "Show Noob Warning Message")]
            public bool NoobWarning { get; set; }

            [JsonProperty(PropertyName = "Show Barrage Message")]
            public bool Barrage { get; set; } = True;

            [JsonProperty(PropertyName = "Show Despawn Message")]
            public bool Destruct { get; set; } = True;

            [JsonProperty(PropertyName = "Show First Player Entered")]
            public bool FirstEntered { get; set; } = False;

            [JsonProperty(PropertyName = "Show First Player Opened")]
            public bool FirstOpened { get; set; } = False;

            [JsonProperty(PropertyName = "Show Opened Message")]
            public bool Opened { get; set; } = True;

            [JsonProperty(PropertyName = "Show Prefix")]
            public bool Prefix { get; set; } = True;

            [JsonProperty(PropertyName = "Show Started Message")]
            public bool Started { get; set; } = True;

            [JsonProperty(PropertyName = "Show Thief Message")]
            public bool Thief { get; set; } = True;
        }

        public class FireballSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = True;

            [JsonProperty(PropertyName = "Damage Per Second")]
            public float DamagePerSecond { get; set; } = 10f;

            [JsonProperty(PropertyName = "Lifetime Min")]
            public float LifeTimeMin { get; set; } = 7.5f;

            [JsonProperty(PropertyName = "Lifetime Max")]
            public float LifeTimeMax { get; set; } = 10f;

            [JsonProperty(PropertyName = "Radius")]
            public float Radius { get; set; } = 1f;

            [JsonProperty(PropertyName = "Tick Rate")]
            public float TickRate { get; set; } = 1f;

            [JsonProperty(PropertyName = "Generation")]
            public float Generation { get; set; } = 5f;

            [JsonProperty(PropertyName = "Water To Extinguish")]
            public int WaterToExtinguish { get; set; } = 25;

            [JsonProperty(PropertyName = "Spawn Every X Seconds")]
            public int SecondsBeforeTick { get; set; } = 5;
        }

        public class GUIAnnouncementSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = False;

            [JsonProperty(PropertyName = "Text Color")]
            public string TextColor { get; set; } = "White";

            [JsonProperty(PropertyName = "Banner Tint Color")]
            public string TintColor { get; set; } = "Grey";

            [JsonProperty(PropertyName = "Maximum Distance")]
            public float Distance { get; set; } = 300f;
        }

        public class LustyMapSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = True;

            [JsonProperty(PropertyName = "Icon Name")]
            public string IconName { get; set; } = "dtchest";

            [JsonProperty(PropertyName = "Icon File")]
            public string IconFile { get; set; } = "http://i.imgur.com/XoEMTJj.png";

            [JsonProperty(PropertyName = "Icon Rotation")]
            public float IconRotation { get; set; } = 0f;
        }

        public class MissileLauncherSettings
        {
            [JsonProperty(PropertyName = "Acquire Time In Seconds")]
            public float TargettingTime { get; set; } = 10f;

            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = False;

            [JsonProperty(PropertyName = "Damage Per Missile")]
            public float Damage { get; set; } = 0.0f;

            [JsonProperty(PropertyName = "Detection Distance")]
            public float Distance { get; set; } = 15f;

            [JsonProperty(PropertyName = "Life Time In Seconds")]
            public float Lifetime { get; set; } = 60f;

            [JsonProperty(PropertyName = "Ignore Flying Players")]
            public bool IgnoreFlying { get; set; } = True;

            [JsonProperty(PropertyName = "Spawn Every X Seconds")]
            public float Frequency { get; set; } = 15f;

            [JsonProperty(PropertyName = "Target Chest If No Player Target")]
            public bool TargetChest { get; set; } = False;
        }

        public class MonumentSettings
        {
            [JsonProperty(PropertyName = "Blacklisted", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Blacklist { get; set; } = new List<string> { "Bandit Camp", "Outpost", "Junkyard" };

            [JsonProperty(PropertyName = "Auto Spawn At Monuments Only")]
            public bool Only { get; set; } = False;

            [JsonProperty(PropertyName = "Chance To Spawn At Monuments Instead")]
            public float Chance { get; set; } = 0.0f;

            [JsonProperty(PropertyName = "Allow Treasure Loot Underground")]
            public bool Underground { get; set; } = False;
        }

        public class NewmanModeSettings
        {
            [JsonProperty(PropertyName = "Protect Nakeds From Fire Aura")]
            public bool Aura { get; set; } = False;

            [JsonProperty(PropertyName = "Protect Nakeds From Other Harm")]
            public bool Harm { get; set; } = False;
        }


        public class MurdererKitSettings
        {
            [JsonProperty(PropertyName = "Helm", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Helm { get; set; } = new List<string> { "metal.facemask" };

            [JsonProperty(PropertyName = "Torso", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Torso { get; set; } = new List<string> { "metal.plate.torso" };

            [JsonProperty(PropertyName = "Pants", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Pants { get; set; } = new List<string> { "pants" };

            [JsonProperty(PropertyName = "Gloves", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Gloves { get; set; } = new List<string> { "tactical.gloves" };

            [JsonProperty(PropertyName = "Boots", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Boots { get; set; } = new List<string> { "boots.frog" };

            [JsonProperty(PropertyName = "Shirt", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Shirt { get; set; } = new List<string> { "tshirt" };

            [JsonProperty(PropertyName = "Weapon", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Weapon { get; set; } = new List<string> { "machete" };
        }

        public class ScientistKitSettings
        {
            [JsonProperty(PropertyName = "Helm", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Helm { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Torso", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Torso { get; set; } = new List<string> { "hazmatsuit_scientist_peacekeeper" };

            [JsonProperty(PropertyName = "Pants", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Pants { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Gloves", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Gloves { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Boots", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Boots { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Shirt", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Shirt { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Weapon", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Weapon { get; set; } = new List<string> { "rifle.ak" };
        }

        public class NpcSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = True;

            [JsonProperty(PropertyName = "Scientist Weapon Accuracy (0 - 100)")]
            public float Accuracy { get; set; } = 75f;

            [JsonProperty(PropertyName = "Spawn Murderers")]
            public bool SpawnMurderers { get; set; } = False;

            [JsonProperty(PropertyName = "Spawn Scientists Only")]
            public bool SpawnScientistsOnly { get; set; } = False;

            [JsonProperty(PropertyName = "Spawn Murderers And Scientists")]
            public bool SpawnBoth { get; set; } = True;

            [JsonProperty(PropertyName = "Amount To Spawn")]
            public int SpawnAmount { get; set; } = 3;

            [JsonProperty(PropertyName = "Minimum Amount To Spawn")]
            public int SpawnMinAmount { get; set; } = 1;

            [JsonProperty(PropertyName = "Despawn Inventory On Death")]
            public bool DespawnInventory { get; set; } = True;

            [JsonProperty(PropertyName = "Spawn Random Amount")]
            public bool SpawnRandomAmount { get; set; } = False;

            [JsonProperty(PropertyName = "Health For Murderers (100 min, 5000 max)")]
            public float MurdererHealth { get; set; } = 150f;

            [JsonProperty(PropertyName = "Health For Scientists (100 min, 5000 max)")]
            public float ScientistHealth { get; set; } = 150f;

            /*[JsonProperty(PropertyName = "Murderer Sprinting Speed (0 min, 2.5 max)")]
            public float MurdererSpeedSprinting { get; set; } = 2.5f;

            [JsonProperty(PropertyName = "Murderer Walking Speed (0 min, 2.5 max)")]
            public float MurdererSpeedWalking { get; set; } = 1.5f;

            [JsonProperty(PropertyName = "Scientist Sprinting Speed (0 min, 2.5 max)")]
            public float ScientistSpeedSprinting { get; set; } = 1.0f;

            [JsonProperty(PropertyName = "Scientist Walking Speed (0 min, 2.5 max)")]
            public float ScientistSpeedWalking { get; set; } = 0.0f;*/

            [JsonProperty(PropertyName = "Murderer (Items)")]
            public MurdererKitSettings MurdererItems { get; set; } = new MurdererKitSettings();

            [JsonProperty(PropertyName = "Scientist (Items)")]
            public ScientistKitSettings ScientistItems { get; set; } = new ScientistKitSettings();

            [JsonProperty(PropertyName = "Murderer Kits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> MurdererKits { get; set; } = new List<string> { "murderer_kit_1", "murderer_kit_2" };

            [JsonProperty(PropertyName = "Scientist Kits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> ScientistKits { get; set; } = new List<string> { "scientist_kit_1", "scientist_kit_2" };

            [JsonProperty(PropertyName = "Blacklisted", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> BlacklistedMonuments { get; set; } = new List<string> { "Bandit Camp", "Outpost", "Junkyard" };

            [JsonProperty(PropertyName = "Random Names", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> RandomNames { get; set; } = new List<string>();
        }

        public class PasteOption
        {
            [JsonProperty(PropertyName = "Option")]
            public string Key { get; set; }

            [JsonProperty(PropertyName = "Value")]
            public string Value { get; set; }
        }

        public class RankedLadderSettings
        {
            [JsonProperty(PropertyName = "Award Top X Players On Wipe")]
            public int Amount { get; set; } = 3;

            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = True;

            [JsonProperty(PropertyName = "Group Name")]
            public string Group { get; set; } = "treasurehunter";

            [JsonProperty(PropertyName = "Permission Name")]
            public string Permission { get; set; } = "dangeroustreasures.th";
        }

        public class RewardSettings
        {
            [JsonProperty(PropertyName = "Economics Money")]
            public double Money { get; set; } = 0;

            [JsonProperty(PropertyName = "ServerRewards Points")]
            public double Points { get; set; } = 0;

            [JsonProperty(PropertyName = "Use Economics")]
            public bool Economics { get; set; } = False;

            [JsonProperty(PropertyName = "Use ServerRewards")]
            public bool ServerRewards { get; set; } = False;
        }

        public class RocketOpenerSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = True;

            [JsonProperty(PropertyName = "Rockets")]
            public int Amount { get; set; } = 8;

            [JsonProperty(PropertyName = "Speed")]
            public float Speed { get; set; } = 5f;

            [JsonProperty(PropertyName = "Use Fire Rockets")]
            public bool FireRockets { get; set; } = False;
        }

        public class SkinSettings
        {
            [JsonProperty(PropertyName = "Use Random Skin")]
            public bool RandomSkins { get; set; } = True;

            [JsonProperty(PropertyName = "Preset Skin")]
            public ulong PresetSkin { get; set; } = 0;

            [JsonProperty(PropertyName = "Include Workshop Skins")]
            public bool RandomWorkshopSkins { get; set; } = True;
        }

        public class TreasureItem
        {
            public string shortname { get; set; }
            public int amount { get; set; }
            public ulong skin { get; set; }
            public int amountMin { get; set; }
        }

        public class TreasureSettings
        {
            [JsonProperty(PropertyName = "Loot", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TreasureItem> Loot { get; set; } = DefaultLoot;

            [JsonProperty(PropertyName = "Minimum Percent Loss")]
            public decimal PercentLoss { get; set; } = 0;

            [JsonProperty(PropertyName = "Percent Increase When Using Day Of Week Loot")]
            public bool Increased { get; set; } = False;

            [JsonProperty(PropertyName = "Use Random Skins")]
            public bool RandomSkins { get; set; } = False;

            [JsonProperty(PropertyName = "Include Workshop Skins")]
            public bool RandomWorkshopSkins { get; set; } = False;

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

            [JsonProperty(PropertyName = "Use Day Of Week Loot")]
            public bool UseDOWL { get; set; } = False;

            [JsonProperty(PropertyName = "Percent Increase On Monday")]
            public decimal PercentIncreaseOnMonday { get; set; } = 0;

            [JsonProperty(PropertyName = "Percent Increase On Tuesday")]
            public decimal PercentIncreaseOnTuesday { get; set; } = 0;

            [JsonProperty(PropertyName = "Percent Increase On Wednesday")]
            public decimal PercentIncreaseOnWednesday { get; set; } = 0;

            [JsonProperty(PropertyName = "Percent Increase On Thursday")]
            public decimal PercentIncreaseOnThursday { get; set; } = 0;

            [JsonProperty(PropertyName = "Percent Increase On Friday")]
            public decimal PercentIncreaseOnFriday { get; set; } = 0;

            [JsonProperty(PropertyName = "Percent Increase On Saturday")]
            public decimal PercentIncreaseOnSaturday { get; set; } = 0;

            [JsonProperty(PropertyName = "Percent Increase On Sunday")]
            public decimal PercentIncreaseOnSunday { get; set; } = 0;
        }

        public class TruePVESettings
        {
            [JsonProperty(PropertyName = "Allow Building Damage At Events")]
            public bool AllowBuildingDamageAtEvents { get; set; } = False;

            [JsonProperty(PropertyName = "Allow PVP At Events")]
            public bool AllowPVPAtEvents { get; set; } = True;

            [JsonProperty(PropertyName = "Allow PVP Server-Wide During Events")]
            public bool ServerWidePVP { get; set; } = False;
        }

        public class UnlockSettings
        {
            [JsonProperty(PropertyName = "Min Seconds")]
            public float MinTime { get; set; } = 300f;

            [JsonProperty(PropertyName = "Max Seconds")]
            public float MaxTime { get; set; } = 480f;

            [JsonProperty(PropertyName = "Unlock When Npcs Die")]
            public bool WhenNpcsDie { get; set; } = False;

            [JsonProperty(PropertyName = "Require All Npcs Die Before Unlocking")]
            public bool RequireAllNpcsDie { get; set; } = False;
        }

        public class UnlootedAnnouncementSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; } = False;

            [JsonProperty(PropertyName = "Notify Every X Minutes (Minimum 1)")]
            public float Interval { get; set; } = 3f;
        }

        public class Configuration
        {
            [JsonProperty(PropertyName = "Settings")]
            public PluginSettings Settings = new PluginSettings();

            [JsonProperty(PropertyName = "Countdown")]
            public CountdownSettings Countdown = new CountdownSettings();

            [JsonProperty(PropertyName = "Events")]
            public EventSettings Event = new EventSettings();

            [JsonProperty(PropertyName = "Event Messages")]
            public EventMessageSettings EventMessages = new EventMessageSettings();

            [JsonProperty(PropertyName = "Fireballs")]
            public FireballSettings Fireballs = new FireballSettings();

            [JsonProperty(PropertyName = "GUIAnnouncements")]
            public GUIAnnouncementSettings GUIAnnouncement = new GUIAnnouncementSettings();

            [JsonProperty(PropertyName = "Lusty Map")]
            public LustyMapSettings LustyMap = new LustyMapSettings();

            [JsonProperty(PropertyName = "Monuments")]
            public MonumentSettings Monuments = new MonumentSettings();

            [JsonProperty(PropertyName = "Newman Mode")]
            public NewmanModeSettings NewmanMode = new NewmanModeSettings();

            [JsonProperty(PropertyName = "NPCs")]
            public NpcSettings NPC = new NpcSettings();

            [JsonProperty(PropertyName = "Missile Launcher")]
            public MissileLauncherSettings MissileLauncher = new MissileLauncherSettings();

            [JsonProperty(PropertyName = "Ranked Ladder")]
            public RankedLadderSettings RankedLadder = new RankedLadderSettings();

            [JsonProperty(PropertyName = "Rewards")]
            public RewardSettings Rewards = new RewardSettings();

            [JsonProperty(PropertyName = "Rocket Opener")]
            public RocketOpenerSettings Rocket = new RocketOpenerSettings();

            [JsonProperty(PropertyName = "Skins")]
            public SkinSettings Skins = new SkinSettings();

            [JsonProperty(PropertyName = "Treasure")]
            public TreasureSettings Treasure = new TreasureSettings();

            [JsonProperty(PropertyName = "TruePVE")]
            public TruePVESettings TruePVE = new TruePVESettings();

            [JsonProperty(PropertyName = "Unlock Time")]
            public UnlockSettings Unlock = new UnlockSettings();

            [JsonProperty(PropertyName = "Unlooted Announcements")]
            public UnlootedAnnouncementSettings UnlootedAnnouncements = new UnlootedAnnouncementSettings();
        }

        bool configLoaded = False;

        protected override void LoadConfig()
        {
            bool baseFailed = False;
            base.LoadConfig();
            try
            {
                Config.Load(null);
            }
            catch (Exception ex)
            {
                Puts(ex.ToString());
                baseFailed = True;
            }
            try
            {
                _config = Config.ReadObject<Configuration>();
            }
            catch (Exception ex)
            {
                if (!baseFailed)
                {
                    Puts("Loading default configuration");
                    Puts(ex.ToString());
                    return;
                    LoadDefaultConfig();
                }
                else
                {
                    PrintError("Your configuration file contains an error. Either delete it, or correct the error.");
                    return;
                }
            }

            configLoaded = True;

            if (_config.Rocket.Speed > 0.1f) _config.Rocket.Speed = 0.1f;
            if (_config.Treasure.PercentLoss > 0) _config.Treasure.PercentLoss /= 100m;
            if (_config.Monuments.Chance < 0) _config.Monuments.Chance = 0f;
            if (_config.Monuments.Chance > 1f) _config.Monuments.Chance /= 100f;
            if (_config.Event.Radius < 10f) _config.Event.Radius = 10f;
            if (_config.Event.Radius > 150f) _config.Event.Radius = 150f;
            if (_config.MissileLauncher.Distance < 1f) _config.MissileLauncher.Distance = 15f;
            if (_config.MissileLauncher.Distance > _config.Event.Radius * 15) _config.MissileLauncher.Distance = _config.Event.Radius * 2;
            if (_config.LustyMap.IconFile == "special") _config.LustyMap.IconFile = "http://i.imgur.com/XoEMTJj.png";

            if (!string.IsNullOrEmpty(_config.Settings.PermName) && !permission.PermissionExists(_config.Settings.PermName)) permission.RegisterPermission(_config.Settings.PermName, this);
            if (!string.IsNullOrEmpty(_config.Settings.EventChatCommand)) cmd.AddChatCommand(_config.Settings.EventChatCommand, this, cmdDangerousTreasures);
            if (!string.IsNullOrEmpty(_config.Settings.DistanceChatCommand)) cmd.AddChatCommand(_config.Settings.DistanceChatCommand, this, cmdTreasureHunter);
            if (!string.IsNullOrEmpty(_config.Settings.EventConsoleCommand)) cmd.AddConsoleCommand(_config.Settings.EventConsoleCommand, this, nameof(ccmdDangerousTreasures));
            if (string.IsNullOrEmpty(_config.RankedLadder.Permission)) _config.RankedLadder.Permission = "dangeroustreasures.th";
            if (string.IsNullOrEmpty(_config.RankedLadder.Group)) _config.RankedLadder.Group = "treasurehunter";
            if (string.IsNullOrEmpty(_config.LustyMap.IconFile) || string.IsNullOrEmpty(_config.LustyMap.IconName)) _config.LustyMap.Enabled = false;

            if (!string.IsNullOrEmpty(_config.RankedLadder.Permission))
            {
                if (!permission.PermissionExists(_config.RankedLadder.Permission))
                    permission.RegisterPermission(_config.RankedLadder.Permission, this);

                if (!string.IsNullOrEmpty(_config.RankedLadder.Group))
                {
                    permission.CreateGroup(_config.RankedLadder.Group, _config.RankedLadder.Group, 0);
                    permission.GrantGroupPermission(_config.RankedLadder.Group, _config.RankedLadder.Permission, this);
                }
            }

            if (_config.UnlootedAnnouncements.Interval < 1f) _config.UnlootedAnnouncements.Interval = 1f;
            if (_config.Event.AutoDrawDistance < 0f) _config.Event.AutoDrawDistance = 0f;
            if (_config.Event.AutoDrawDistance > ConVar.Server.worldsize) _config.Event.AutoDrawDistance = ConVar.Server.worldsize;
            if (_config.GUIAnnouncement.TintColor.ToLower() == "black") _config.GUIAnnouncement.TintColor = "grey";
            if (_config.NPC.SpawnAmount < 1) _config.NPC.Enabled = false;
            if (_config.NPC.SpawnAmount > 25) _config.NPC.SpawnAmount = 25;
            if (_config.NPC.SpawnMinAmount < 1 || _config.NPC.SpawnMinAmount > _config.NPC.SpawnAmount) _config.NPC.SpawnMinAmount = 1;
            if (_config.NPC.ScientistHealth < 100) _config.NPC.ScientistHealth = 100f;
            if (_config.NPC.ScientistHealth > 5000) _config.NPC.ScientistHealth = 5000f;
            if (_config.NPC.MurdererHealth < 100) _config.NPC.MurdererHealth = 100f;
            if (_config.NPC.MurdererHealth > 5000) _config.NPC.MurdererHealth = 5000f;
            /*if (_config.NPC.MurdererSpeedSprinting > 2.5f) _config.NPC.MurdererSpeedSprinting = 2.5f;
            if (_config.NPC.MurdererSpeedWalking > 2.5) _config.NPC.MurdererSpeedWalking = 2.5f;
            if (_config.NPC.MurdererSpeedWalking < 0) _config.NPC.MurdererSpeedWalking = 0f;
            if (_config.NPC.ScientistSpeedSprinting > 2.5f) _config.NPC.ScientistSpeedSprinting = 2.5f;
            if (_config.NPC.ScientistSpeedWalking < 0) _config.NPC.ScientistSpeedWalking = 0f;
            if (_config.NPC.ScientistSpeedWalking > 2.5) _config.NPC.ScientistSpeedWalking = 2.5f;*/

            SaveConfig();
        }

        List<TreasureItem> ChestLoot
        {
            get
            {
                if (unloading || !init)
                {
                    return new List<TreasureItem>();
                }

                if (_config.Treasure.UseDOWL)
                {
                    switch (DateTime.Now.DayOfWeek)
                    {
                        case DayOfWeek.Monday:
                            {
                                return _config.Treasure.DOWL_Monday;
                            }
                        case DayOfWeek.Tuesday:
                            {
                                return _config.Treasure.DOWL_Tuesday;
                            }
                        case DayOfWeek.Wednesday:
                            {
                                return _config.Treasure.DOWL_Wednesday;
                            }
                        case DayOfWeek.Thursday:
                            {
                                return _config.Treasure.DOWL_Thursday;
                            }
                        case DayOfWeek.Friday:
                            {
                                return _config.Treasure.DOWL_Friday;
                            }
                        case DayOfWeek.Saturday:
                            {
                                return _config.Treasure.DOWL_Saturday;
                            }
                        case DayOfWeek.Sunday:
                            {
                                return _config.Treasure.DOWL_Sunday;
                            }
                    }
                }

                return _config.Treasure.Loot;
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion
    }
}