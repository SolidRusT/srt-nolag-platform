using Facepunch;
using System;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement; 
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; 
using Rust.Ai.HTN;

namespace Oxide.Plugins 
{
    [Info("BotSpawn", "Steenamaroo", "2.1.6", ResourceId = 15)] 
    [Description("Spawn tailored AI with kits at monuments, custom locations, or randomly.")]

    class BotSpawn : RustPlugin
    {
        [PluginReference] Plugin Kits, CustomLoot, ServerRewards;   
        int no_of_AI;
        bool loaded;
        Single currentTime;
        static BotSpawn botSpawn; 
        const bool True = true, False = false;   
        const object Null = null;
        const string permAllowed = "botspawn.allowed";
        static System.Random random = new System.Random();
        int GetRand(int l, int h) => random.Next(l, h);

        public Dictionary<string, PopInfo> popinfo = new Dictionary<string, PopInfo>();
        public class PopInfo
        {
            public int population;
            public int queued;
        }

        public Dictionary<ulong, Timer> weaponCheck = new Dictionary<ulong, Timer>();
        public static string Get(ulong v) => RandomUsernames.Get((int)(v % 2147483647uL));
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);
        public Dictionary<string, List<Vector3>> spawnLists = new Dictionary<string, List<Vector3>>();
        public bool IsNight => currentTime > configData.Global.NightStartHour || currentTime < configData.Global.DayStartHour;

        Dictionary<ulong, string> editing = new Dictionary<ulong, string>();
        public static Timer aridTimer, temperateTimer, tundraTimer, arcticTimer;
        public Dictionary<string, Timer> timers = new Dictionary<string, Timer>() { { "BiomeArid", aridTimer }, { "BiomeTemperate", temperateTimer }, { "BiomeTundra", tundraTimer }, { "BiomeArctic", arcticTimer } };
        bool IsAuth(BasePlayer player) => player?.net?.connection?.authLevel == 2;

        public Dictionary<ulong, NPCPlayerApex> NPCPlayers = new Dictionary<ulong, NPCPlayerApex>();
        public Dictionary<string, DataProfile> AllProfiles = new Dictionary<string, DataProfile>();

        void OnServerInitialized()
        {
            timer.Once(1f, () =>
            {
                botSpawn = this;
                currentTime = TOD_Sky.Instance.Cycle.Hour;
                timer.Repeat(2f, 0, () => currentTime = TOD_Sky.Instance.Cycle.Hour);
                CheckMonuments(False);
                LoadConfigVariables();

                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>($"BotSpawn/{configData.DataPrefix}-CustomProfiles");
                defaultData = Interface.Oxide.DataFileSystem.ReadObject<DefaultData>($"BotSpawn/{configData.DataPrefix}-DefaultProfiles");
                spawnsData = Interface.Oxide.DataFileSystem.ReadObject<SpawnsData>($"BotSpawn/{configData.DataPrefix}-SpawnsData");

                var files = Interface.Oxide.DataFileSystem.GetFiles("BotSpawn");
                StoredData storedUpdate;
                UpdateData defaultUpdate;
                string name;
                foreach (var file in files)
                {
                    name = file.Substring(file.IndexOf("BotSpawn") + 9);
                    name = name.Substring(0, name.Length - 5);
                    if (file.Contains("-CustomProfiles"))
                    {
                        storedUpdate = Interface.Oxide.DataFileSystem.ReadObject<StoredData>($"BotSpawn/{name}");
                        Interface.Oxide.DataFileSystem.WriteObject($"BotSpawn/{name}", storedUpdate);
                    }
                    if (file.Contains("-DefaultProfiles"))
                    {
                        defaultUpdate = Interface.Oxide.DataFileSystem.ReadObject<UpdateData>($"BotSpawn/{name}");
                        Interface.Oxide.DataFileSystem.WriteObject($"BotSpawn/{name}", defaultUpdate);
                    }
                }

                SaveData();
                SetupProfiles();
                timer.Once(10, () => timer.Repeat(5, 0, () => AdjustPopulation()));

                foreach (BasePlayer player in UnityEngine.Object.FindObjectsOfType<BasePlayer>())
                {
                    foreach (var comp in player?.GetComponents<Component>())
                        if (comp?.GetType()?.Name == "HumanPlayer")
                        {
                            HumanNPCs.Add(player.userID);
                            break;
                        }
                }

                loaded = True;
            });
        }

        void Init()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            };
            var filter = RustExtension.Filter.ToList();
            filter.Add("cover points");
            filter.Add("resulted in a conflict");
            RustExtension.Filter = filter.ToArray();
            no_of_AI = 0;
        }

        void Loaded()
        {
            ConVar.AI.npc_families_no_hurt = False;
            foreach (var entry in timers)
                spawnLists.Add(entry.Key, new List<Vector3>());
            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission(permAllowed, this);
        }

        void Unload()
        {
            foreach (var obj in RemoveObj.ToList())
                UnityEngine.Object.DestroyImmediate(obj);

            var filter = RustExtension.Filter.ToList();
            filter.Remove("cover points");
            filter.Remove("resulted in a conflict");
            RustExtension.Filter = filter.ToArray();
            Wipe();
        }

        void Wipe()
        {
            foreach (var bot in NPCPlayers.ToDictionary(pair => pair.Key, pair => pair.Value).Where(bot => bot.Value != null))
                NPCPlayers[bot.Key].Kill();
        }

        #region BiomeSpawnsSetup
        void GenerateSpawnPoints(string name, int number, Timer myTimer, int biomeNo)
        {
            int getBiomeAttempts = 0;
            var spawnlist = spawnLists[name];
            int halfish = Convert.ToInt16((ConVar.Server.worldsize / 2) / 1.1f);
            var rand = UnityEngine.Random.insideUnitCircle * halfish;

            if (AllProfiles[name].Kit.Count > 0 && Kits == null)
            {
                PrintWarning(lang.GetMessage("nokits", this), name);
                return;
            }

            timers[name] = timer.Repeat(0.01f, 0, () =>
            {
                bool finished = True;
                if (spawnlist.Count < number + 10)
                {
                    getBiomeAttempts++;
                    if (getBiomeAttempts > 200 && spawnlist.Count == 0)
                    {
                        PrintWarning(lang.GetMessage("noSpawn", this), name);
                        timers[name].Destroy();
                        return;
                    }
                    rand = UnityEngine.Random.insideUnitCircle * halfish;
                    Vector3 randomSpot = new Vector3(rand.x, 0, rand.y);
                    finished = False;
                    if (TerrainMeta.BiomeMap.GetBiome(randomSpot, biomeNo) > 0.5f)
                    {
                        var point = CalculateGroundPos(new Vector3(randomSpot.x, 200, randomSpot.z));
                        if (point != Vector3.zero)
                            spawnlist.Add(CalculateGroundPos(new Vector3(randomSpot.x, 200, randomSpot.z))); ;
                    }
                }
                if (finished)
                {
                    int i = 0;
                    var target = TargetAmount(AllProfiles[name]);
                    if (target > 0)
                    {
                        int amount = CanRespawn(name, target, False);
                        if (amount > 0)
                        {
                            timer.Repeat(2, amount, () =>
                            {
                                if (CanRespawn(name, 1, True) == 1)
                                {
                                    SpawnBots(name, AllProfiles[name], "biome", null, spawnlist[i], -1);
                                    i++;
                                }
                            });
                        }
                    }
                    timers[name].Destroy();
                }
            });
        }

        public bool HasNav(Vector3 pos)
        {
            NavMeshHit navMeshHit;
            return (NavMesh.SamplePosition(pos, out navMeshHit, 2, 1));
        }

        public static Vector3 CalculateGroundPos(Vector3 pos)
        {
            pos.y = TerrainMeta.HeightMap.GetHeight(pos);
            NavMeshHit navMeshHit;

            if (!NavMesh.SamplePosition(pos, out navMeshHit, 2, 1))
                pos = Vector3.zero;
            else if (WaterLevel.GetWaterDepth(pos, true) > 0)
                pos = Vector3.zero;
            else if (Physics.RaycastAll(navMeshHit.position + new Vector3(0, 100, 0), Vector3.down, 99f, 1235288065).Any())
                pos = Vector3.zero;
            else
                pos = navMeshHit.position;
            return pos;
        }

        Vector3 TryGetSpawn(Vector3 pos, int radius)
        {
            int attempts = 0;
            var spawnPoint = Vector3.zero; 
            Vector2 rand;
             
            while (attempts < 200 && spawnPoint == Vector3.zero) 
            {
                attempts++;
                rand = UnityEngine.Random.insideUnitCircle * radius;  
                spawnPoint = CalculateGroundPos(pos + new Vector3(rand.x, 0, rand.y));    
                if (spawnPoint != Vector3.zero)
                    return spawnPoint;
            }
            return spawnPoint; 
        }

        int GetNextSpawn(string name, DataProfile profile)
        {
            if (profile.UseCustomSpawns && spawnsData.CustomSpawnLocations[name].Count > 0)
            {
                if (availableStatSpawns.Count == 0) 
                    return -1;

                int spawnnum = availableStatSpawns[name][random.Next(availableStatSpawns[name].Count())];
                availableStatSpawns[name].Remove(spawnnum);
                return spawnnum;
            }  
            return -1;
        }
        #endregion

        #region population

        void AdjustPop(string profile, int num) => popinfo[profile].population += num;
        void AdjustQueue(string profile, int num) => popinfo[profile].queued += num;

        int TargetAmount(DataProfile profile) => IsNight ? profile.Night_Time_Spawn_Amount : profile.Day_Time_Spawn_Amount;

        int CanRespawn(string name, int amount, bool second)
        {
            int response = TargetAmount(AllProfiles[name]) - popinfo[name].population; 
            if (!second)
                response += popinfo[name].queued;
            if (response > 0)
            {
                if (!second)
                    AdjustQueue(name, Mathf.Min(amount, response));
                return Mathf.Min(amount, response);
            }
            else if (second)
                AdjustQueue(name, -1); 
            return 0;
        }

        Dictionary<string, List<int>> availableStatSpawns = new Dictionary<string, List<int>>();
        void AdjustPopulation()
        {
            foreach (var profile in AllProfiles.Where(x => x.Value.AutoSpawn == True && x.Key != "AirDrop" && x.Key != "HackableLockedCrate"))
            {
                int targetAmount = TargetAmount(profile.Value);
                if (targetAmount == 0)
                    continue;

                var current = popinfo[profile.Key].population + popinfo[profile.Key].queued;
                if (current < targetAmount)
                {
                    popinfo[profile.Key].queued += targetAmount - current; 
                    timer.Repeat(1f, targetAmount - current, () =>
                    {
                        if (profile.Value.UseCustomSpawns && spawnsData.CustomSpawnLocations.ContainsKey(profile.Key))
                        {
                            int spawnnum = 0, num = spawnsData.CustomSpawnLocations[profile.Key].Count;

                            if (num > 0)
                            {
                                spawnnum = GetNextSpawn(profile.Key, profile.Value);
                                if (spawnnum == -1)
                                {
                                    Puts("ADJUSTPOP FAILING");
                                    return;
                                }

                                SpawnBots(profile.Key, AllProfiles[profile.Key], null, null, new Vector3(), spawnnum);
                                if (profile.Value.Announce_Spawn && profile.Value.Announcement_Text != String.Empty)
                                    PrintToChat(profile.Value.Announcement_Text);
                            }
                        }
                        else
                        {
                            if (timers.ContainsKey(profile.Key))
                            {
                                if (spawnLists[profile.Key].Count > 0)
                                    SpawnBots(profile.Key, AllProfiles[profile.Key], "biome", null, spawnLists[profile.Key][random.Next(spawnLists[profile.Key].Count)], -1);
                            }
                            else
                            {
                                SpawnBots(profile.Key, AllProfiles[profile.Key], null, null, new Vector3(), -1);
                                if (profile.Value.Announce_Spawn && profile.Value.Announcement_Text != String.Empty)
                                    PrintToChat(profile.Value.Announcement_Text); 
                            }
                        }
                    });
                    continue;
                }
                else
                {
                    foreach (var npc in NPCPlayers.ToList())
                    {
                        var bData = npc.Value.GetComponent<BotData>();
                        if (bData.monumentName == profile.Key && bData.respawn)
                        {
                            if (popinfo[profile.Key].population > targetAmount)
                            {
                                DontRespawn.Add(npc.Key);
                                npc.Value.Kill();
                            }

                            if (configData.Global.Staggered_Despawn)
                                break;
                        }
                    }
                }
            }
        }
        #endregion

        List<ulong> DontRespawn = new List<ulong>(); 

        #region BotSetup  
        void DeployNpcs(Vector3 location, string name, DataProfile profile, string group, int num) => SpawnBots(name, profile, "Attack", group, location, num);
        void SpawnBots(string name, DataProfile zone, string type, string group, Vector3 location, int spawnNum)
        {
            bool respawn = type != "Attack" && type != "AirDrop" && type != "HackableLockedCrate"; 
            var pos = zone.Location;
            var finalPoint = Vector3.zero;
            bool stationary = zone.Stationary;
            float rot = 0;

            if (location == Vector3.zero && type != "AirDrop" && type != "HackableLockedCrate" && zone.UseCustomSpawns && spawnsData.CustomSpawnLocations[name].Count > 0)
            {
                var customLoc = spawnNum == -1 ? spawnsData.CustomSpawnLocations[name][random.Next(spawnsData.CustomSpawnLocations[name].Count)] : spawnsData.CustomSpawnLocations[name][spawnNum];
                Vector3 loc = s2v(customLoc);
                rot = s2r(customLoc); 

                var l = mons.ContainsKey(zone.Parent_Monument) ? mons[zone.Parent_Monument].transform : mons.ContainsKey(name) ? mons[name].transform : null; 
                if (l != null)
                    finalPoint = l.TransformPoint(loc);

                if (!HasNav(finalPoint) && !zone.Stationary) 
                {
                    PrintWarning(lang.GetMessage("noNav", this), name, spawnsData.CustomSpawnLocations[name].IndexOf(customLoc));
                    return;
                }
            }
            else
                stationary = False;
            if (finalPoint == Vector3.zero) 
            {
                if (location != Vector3.zero) 
                    pos = location;
                if (type != "biome")
                    finalPoint = TryGetSpawn(pos, zone.Radius);
                else
                    finalPoint = location;

                if (finalPoint == Vector3.zero)
                {
                    Puts($"Can't get spawn point at {name}. Skipping one npc.");
                    AdjustQueue(name, -1);
                    return;
                }
            }

            if (zone.Chute && !stationary)
            {
                var rand = UnityEngine.Random.insideUnitCircle * zone.Radius;
                finalPoint = type == "AirDrop" ? pos + new Vector3(rand.x, -40, rand.y) : new Vector3(finalPoint.x, 200, finalPoint.z);
            }

            NPCPlayer entity = (NPCPlayer)InstantiateSci(finalPoint, new Quaternion(), zone.Murderer);
            var npc = entity.GetComponent<NPCPlayerApex>();
            npc.Spawn();

            NextTick(() =>
            {
                if (npc == null || npc.IsDestroyed || npc.IsDead())
                {
                    if (respawn)//popinfo.ContainsKey(name))
                        AdjustQueue(name, -1);
                    if (spawnNum != -1 && respawn)
                        availableStatSpawns[name].Add(spawnNum);
                    return;
                }
                if (!NPCPlayers.ContainsKey(npc.userID))
                    NPCPlayers.Add(npc.userID, npc);
                else
                {
                    npc.Kill();
                    PrintWarning(lang.GetMessage("dupID", this));
                    if (respawn)//popinfo.ContainsKey(name))
                        AdjustQueue(name, -1);
                    if (spawnNum != -1 && respawn)
                        availableStatSpawns[name].Add(spawnNum);
                    return;
                }
                npc.EnablePlayerCollider();
                timer.Once(1f, () =>
                {
                    if (npc != null)
                    {
                        var n = mons.ContainsKey(zone.Parent_Monument) ? mons[zone.Parent_Monument].transform : mons.ContainsKey(name) ? mons[name].transform : null;
                        if (n != null)
                        {
                            npc.transform.rotation = Quaternion.Euler(0, n.transform.eulerAngles.y + rot, 0);
                            npc.SetFact(NPCPlayerApex.Facts.IsAggro, 0, False, False);
                        }
                    }
                });
                if (zone.Murderer)
                {
                    var suit = ItemManager.CreateByName("scarecrow.suit", 1, 0);
                    var eyes = ItemManager.CreateByName("gloweyes", 1, 0);
                    if (!suit.MoveToContainer(npc.inventory.containerWear))
                        suit.Remove();
                    if (!eyes.MoveToContainer(npc.inventory.containerWear))
                        eyes.Remove();
                }

                var bData = npc.gameObject.AddComponent<BotData>();
                bData.stationary = stationary;
                if (spawnNum != -1 && respawn)
                    bData.CustomSpawnNum = spawnNum;

                bData.monumentName = name;

                no_of_AI++;
                bData.respawn = True;
                bData.profile = zone.Clone();
                bData.group = group ?? null;
                bData.spawnPoint = finalPoint;
                bData.biome = type == "biome";

                npc.startHealth = zone.BotHealth;
                npc.InitializeHealth(zone.BotHealth, zone.BotHealth);

                npc.CommunicationRadius = 0;
                npc.AiContext.Human.NextToolSwitchTime = Time.realtimeSinceStartup * 10;
                npc.AiContext.Human.NextWeaponSwitchTime = Time.realtimeSinceStartup * 10;

                if (zone.Chute && !stationary)
                    AddChute(npc, finalPoint);

                int kitRnd;
                kitRnd = random.Next(zone.Kit.Count);

                if (zone.BotNames.Count == zone.Kit.Count && zone.Kit.Count != 0)
                    SetName(zone, npc, kitRnd);
                else
                    SetName(zone, npc, random.Next(zone.BotNames.Count));

                GiveKit(npc, zone, kitRnd);
                npc.clothingMoveSpeedReduction = -zone.Running_Speed_Boost;
                SortWeapons(npc);

                int suicInt = random.Next(zone.Suicide_Timer, zone.Suicide_Timer + 10);
                if (!respawn)
                {
                    bData.respawn = False;
                    RunSuicide(npc, suicInt);
                }
                else
                {
                    AdjustPop(name, 1);
                    AdjustQueue(name, -1);
                }

                if (zone.Disable_Radio)
                    npc.RadioEffect = new GameObjectRef();

                ToggleAggro(npc, Convert.ToByte(!zone.Peace_Keeper), zone.Aggro_Range);
                npc.Stats.DeaggroChaseTime = 10;
                npc.Stats.Defensiveness = 1;
            });
        }

        BaseEntity InstantiateSci(Vector3 position, Quaternion rotation, bool murd)
        {
            string type = murd ? "murderer" : "scientist";
            string prefabname = $"assets/prefabs/npc/{type}/{type}.prefab";

            GameObject gameObject = Instantiate.GameObject(GameManager.server.FindPrefab(prefabname), position, rotation);
            gameObject.name = prefabname;
            SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);
            if (gameObject.GetComponent<Spawnable>())
                UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());
            if (!gameObject.activeSelf)
                gameObject.SetActive(True);
            BaseEntity component = gameObject.GetComponent<BaseEntity>();
            return component;
        }

        void AddChute(NPCPlayerApex npc, Vector3 newPos)
        {
            float wind = random.Next(0, Mathf.Min(100, configData.Global.Max_Chute_Wind_Speed)) / 40f;
            float fall = random.Next(60, Mathf.Min(100, configData.Global.Max_Chute_Fall_Speed) + 60) / 20f;

            var rb = npc.gameObject.GetComponent<Rigidbody>();
            rb.isKinematic = False;
            rb.useGravity = False;
            rb.drag = 0f;
            npc.gameObject.layer = 0;//prevent_build layer fix
            var fwd = npc.transform.forward;
            rb.velocity = new Vector3(fwd.x * wind, 0, fwd.z * wind) - new Vector3(0, fall, 0);

            var col = npc.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(1, 1f, 1);//feet above ground

            var Chute = GameManager.server.CreateEntity("assets/prefabs/misc/parachute/parachute.prefab", newPos, Quaternion.Euler(0, 0, 0));
            Chute.gameObject.Identity();
            Chute.SetParent(npc);
            Chute.Spawn();
        }

        void SetName(DataProfile zone, NPCPlayerApex npc, int number)
        {
            if (zone.BotNames.Count == 0 || zone.BotNames.Count <= number || zone.BotNames[number] == String.Empty)
            {
                npc.displayName = Get(npc.userID);
                npc.displayName = char.ToUpper(npc.displayName[0]) + npc.displayName.Substring(1);
            }
            else
                npc.displayName = zone.BotNames[number];

            if (zone.BotNamePrefix != String.Empty)
                npc.displayName = zone.BotNamePrefix + " " + npc.displayName;
            if (npc is Scientist)
                (npc as Scientist).LootPanelName = npc.displayName;
        }

        void GiveKit(NPCPlayerApex npc, DataProfile zone, int kitRnd)
        {
            if (npc == null || npc.inventory == null)
                return;
            var bData = npc.GetComponent<BotData>();
            string type = zone.Murderer ? "Murderer" : "Scientist";

            if (zone.Kit.Count != 0 && zone.Kit[kitRnd] != null)
            {
                object checkKit = Kits?.CallHook("GetKitInfo", zone.Kit[kitRnd], True);
                if (checkKit == null)
                {
                    PrintWarning($"Kit {zone.Kit[kitRnd]} does not exist - Spawning default {type}.");
                }
                else
                {
                    bool weaponInBelt = False;
                    JObject kitContents = checkKit as JObject;
                    if (kitContents != null)
                    {
                        JArray items = kitContents["items"] as JArray;
                        foreach (var weap in items)
                        {
                            JObject item = weap as JObject;
                            if (item["container"].ToString() == "belt")
                                weaponInBelt = True;
                        }
                    }
                    if (!weaponInBelt)
                    {
                        PrintWarning($"Kit {zone.Kit[kitRnd]} has no items in belt - Spawning default {type}.");
                    }
                    else
                    {
                        if (bData.profile.Keep_Default_Loadout == False)
                            npc.inventory.Strip();
                        Kits?.Call($"GiveKit", npc, zone.Kit[kitRnd], True);
                    }
                }
            }
        }

        void SortWeapons(NPCPlayerApex npc)
        {
            if (npc == null)
                return;
            var bData = npc.GetComponent<BotData>();
            ItemDefinition fuel = ItemManager.FindItemDefinition("lowgradefuel");
            foreach (var attire in npc.inventory.containerWear.itemList)
                if (attire.info.shortname.Equals("hat.miner") || attire.info.shortname.Equals("hat.candle"))
                {
                    bData.hasHeadLamp = True;
                    Item newItem = ItemManager.Create(fuel, 1);
                    attire.contents.Clear();
                    if (!newItem.MoveToContainer(attire.contents))
                        newItem.Remove();
                    else
                    {
                        npc.SendNetworkUpdateImmediate();
                        npc.inventory.ServerUpdate(0f);
                    }
                }
            foreach (Item item in npc.inventory.containerBelt.itemList)//store organised weapons lists 
            {
                var held = item.GetHeldEntity();
                if (held != null && held as HeldEntity != null)
                {
                    if (held is FlameThrower || held.name.Contains("launcher"))
                        continue;
                    if (held as BaseMelee != null || held as TorchWeapon != null)
                    {
                        bData.Weapons[1].Add(item);
                        bData.Weapons[0].Add(item);
                    }
                    else if (held as BaseProjectile != null)
                    {
                        bData.Weapons[0].Add(item);
                        if (held.name.Contains("m92") || held.name.Contains("pistol") || held.name.Contains("python") || held.name.Contains("waterpipe"))
                            bData.Weapons[2].Add(item);
                        else if (held.name.Contains("bolt") || held.name.Contains("l96"))
                            bData.Weapons[4].Add(item);
                        else
                            bData.Weapons[3].Add(item);
                    }
                }
            }
            if ((npc is Scientist && (bData.Weapons[0].Count == 0 || (bData.Weapons[0].Count == bData.Weapons[1].Count)))
                || npc is NPCMurderer && bData.Weapons[0].Count == 0) 
            {
                PrintWarning(lang.GetMessage("noWeapon", this), bData.monumentName);
                bData.noweapon = True;
                return;
            }
            npc.CancelInvoke(npc.EquipTest);
        }

        void RunSuicide(NPCPlayerApex npc, int suicInt)
        {
            if (!NPCPlayers.ContainsKey(npc.userID))
                return;
            timer.Once(suicInt, () =>
            {
                if (npc == null)
                    return;
                if (npc.AttackTarget != null && Vector3.Distance(npc.transform.position, npc.AttackTarget.transform.position) < 10 && npc.GetNavAgent.isOnNavMesh)
                {
                    var position = npc.AttackTarget.transform.position;
                    npc.svActiveItemID = 0;
                    npc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    npc.inventory.UpdatedVisibleHolsteredItems();
                    timer.Repeat(0.05f, 100, () =>
                    {
                        if (npc == null)
                            return;
                        npc.SetDestination(position);
                    });
                }
                timer.Once(4, () =>
                {
                    if (npc == null)
                        return;
                    Effect.server.Run("assets/prefabs/weapons/rocketlauncher/effects/rocket_explosion.prefab", npc.transform.position);
                    HitInfo nullHit = new HitInfo();
                    nullHit.damageTypes.Add(Rust.DamageType.Explosion, 10000);
                    npc.IsInvinsible = False;
                    npc.Die(nullHit);
                }
                );
            });
        }
        #endregion

        static BasePlayer FindPlayerByName(string name)
        {
            BasePlayer result = null;
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                if (current.displayName.Equals(name, StringComparison.OrdinalIgnoreCase)
                    || current.UserIDString.Contains(name, CompareOptions.OrdinalIgnoreCase)
                    || current.displayName.Contains(name, CompareOptions.OrdinalIgnoreCase))
                    result = current;
            }
            return result;
        }

        #region Hooks
        object OnNpcKits(ulong userID)
        {
            return NPCPlayers.ContainsKey(userID) ? True : Null;
        }

        private object CanBeTargeted(BaseCombatEntity player, BaseEntity entity)//stops autoturrets targetting bots
        {
            NPCPlayer npcPlayer = player as NPCPlayer;
            return (npcPlayer != null && NPCPlayers.ContainsKey(npcPlayer.userID) && configData.Global.Turret_Safe) ? False : Null;
        }
         
        private object CanBradleyApcTarget(BradleyAPC bradley, BaseEntity target)//stops bradley targeting bots
        {
            NPCPlayer npcPlayer = target as NPCPlayer;
            return (npcPlayer != null && NPCPlayers.ContainsKey(npcPlayer.userID) && configData.Global.APC_Safe) ? False : Null;
        } 

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var botMelee = info?.Initiator as BaseMelee;
            bool melee = False;
            if (botMelee != null)
            {
                melee = True;
                info.Initiator = botMelee.GetOwnerPlayer();
            } 
            NPCPlayerApex bot = entity as NPCPlayerApex;
            BotData bData;

            //If victim is one of mine
            if (bot != null && NPCPlayers.ContainsKey(bot.userID))
            {
                if (configData.Global.APC_Safe && info?.Initiator is BradleyAPC)
                    return true;

                var attackPlayer = info?.Initiator as BasePlayer;
                bData = bot.GetComponent<BotData>();

                if (configData.Global.Pve_Safe)
                {
                    if (info.Initiator?.ToString() == null && info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Bullet)
                    {
                        return True; //new autoturrets
                    }
                    if (info.Initiator?.ToString() == null || info.Initiator.ToString().Contains("cactus") || info.Initiator.ToString().Contains("barricade"))
                    {
                        info.damageTypes.ScaleAll(0);
                        return True;
                    }
                }

                if (attackPlayer != null)
                {
                    if (NPCPlayers.ContainsKey(attackPlayer.userID))//and attacker is one of mine
                        if (attackPlayer.GetComponent<BotData>().monumentName == bData.monumentName)
                            return True; //wont attack their own

                    if (configData.Global.PeaceKeepers_Ignore_Melee)
                    {
                        if (bData.profile.Peace_Keeper && (info.Weapon is BaseMelee || info.Weapon is TorchWeapon))//prevent melee farming with peacekeeper on
                        {
                            info.damageTypes.ScaleAll(0);
                            return True;
                        }
                    }

                    if (info != null && bData.profile.Die_Instantly_From_Headshot && info.isHeadshot)
                    {
                        var weap = info?.Weapon?.ShortPrefabName;
                        var weaps = bData.profile.Instant_Death_From_Headshot_Allowed_Weapons;

                        if (weaps.Count == 0 || weap != null && weaps.Contains(weap))
                        {
                            info.damageTypes.Set(0, bot.health);
                            return null;
                        }
                    }
                    if (Vector3.Distance(attackPlayer.transform.position, bot.transform.position) > bot.Stats.AggressionRange)
                    {
                        if (bot.Stats.AggressionRange < 400) 
                        {
                            bot.Stats.AggressionRange += 400;
                            bot.Stats.DeaggroRange += 400;
                        }
                        ForceMemory(bot, attackPlayer);
                        timer.Repeat(1f, 20, () =>
                        {
                            if (bot != null)
                            {
                                bot.RandomMove();
                                if (bot.AttackTarget != null && bot.AttackTarget.IsVisible(bot.eyes.position, (bot.AttackTarget as BasePlayer).eyes.position, 400))
                                    Rust.Ai.HumanAttackOperator.AttackEnemy(bot.AiContext, Rust.Ai.AttackOperator.AttackType.LongRange);
                            }
                        });

                        timer.Once(20, () =>
                        {
                            if (bot == null)
                                return;
                            bot.Stats.AggressionRange = bData.profile.Aggro_Range;
                            bot.Stats.DeaggroRange += bData.profile.DeAggro_Range;
                        });
                    }

                    bot.AttackTarget = attackPlayer;
                    bot.lastAttacker = attackPlayer;
                    bData.goingHome = False;
                }
            }
            NPCPlayerApex attackNPC = info?.Initiator as NPCPlayerApex;
            //if attacker is one of mine
            if (attackNPC != null && entity is BasePlayer && NPCPlayers.ContainsKey(attackNPC.userID))
            {
                bData = attackNPC.GetComponent<BotData>();
                float rand = GetRand(1, 101);
                float distance = Vector3.Distance(info.Initiator.transform.position, entity.transform.position);

                float newAccuracy = bData.profile.Bot_Accuracy_Percent;
                float newDamage = bData.profile.Bot_Damage_Percent / 100f;
                if (distance > 100f && bData.enemyDistance != 4) //sniper exemption 
                {
                    newAccuracy = bData.profile.Bot_Accuracy_Percent / (distance / 100f);
                    newDamage = newDamage / (distance / 100f);
                }
                if (!melee && newAccuracy < rand)
                    return True;
                info.damageTypes.ScaleAll(newDamage);
            }
            return null;
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null)
                return;

            if (HumanNPCs.Contains(player.userID))
                HumanNPCs.Remove(player.userID);

            NPCPlayerApex npc = player as NPCPlayerApex;
            if (npc != null)
                OnEntityKill(npc, info);
        } 

        List<ulong> CrateIDs = new List<ulong>();
        void OnEntityKill(NPCPlayerApex npc, HitInfo info) 
        {
            if (npc?.userID != null && NPCPlayers.ContainsKey(npc.userID) && !botInventories.ContainsKey(npc.userID))
            {
                var bData = npc.GetComponent<BotData>();
                if (bData == null)
                    return;

                if (!AllProfiles.ContainsKey(bData.monumentName)) 
                    return;
                if (bData.respawn)
                    AdjustPop(bData.monumentName, -1);

                var pos = npc.transform.position;
                if (info?.InitiatorPlayer != null)
                {
                    if (bData.profile.ServerRewardsValue > 0)
                        ServerRewards?.Call("AddPoints", info.InitiatorPlayer.userID, bData.profile.ServerRewardsValue);

                    if (bData.profile.Spawn_Hackable_Death_Crate_Percent > GetRand(1, 101) && npc.WaterFactor() < 0.1f)
                    {
                        timer.Once(2f, () => 
                        {
                            var Crate = GameManager.server.CreateEntity("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", pos + new Vector3(1, 2, 0), Quaternion.Euler(0, 0, 0));
                            Crate.Spawn();
                            CrateIDs.Add(Crate.net.ID);
                            (Crate as HackableLockedCrate).hackSeconds = HackableLockedCrate.requiredHackSeconds - bData.profile.Death_Crate_LockDuration;
                            timer.Once(1.4f, () =>
                            {
                                if (Crate == null)
                                    return;
                                if (CustomLoot && bData.profile.Death_Crate_CustomLoot_Profile != string.Empty)
                                {
                                    var container = Crate?.GetComponent<StorageContainer>();
                                    if (container != null)
                                    {
                                        container.inventory.capacity = 36;
                                        container.onlyAcceptCategory = ItemCategory.All;
                                        container.SendNetworkUpdateImmediate();
                                        container.inventory.Clear();

                                        List<Item> loot = (List<Item>)CustomLoot?.Call("MakeLoot", bData.profile.Death_Crate_CustomLoot_Profile);
                                        if (loot != null)
                                            foreach (var item in loot)
                                                if (!item.MoveToContainer(container.inventory, -1, True))
                                                    item.Remove();
                                    }
                                }
                            });
                        });
                    }
                }
                Item activeItem = npc.GetActiveItem();

                if (bData.profile.Weapon_Drop_Percent >= GetRand(1, 101) && activeItem != null)
                {
                    var numb = GetRand(Mathf.Min(bData.profile.Min_Weapon_Drop_Condition_Percent, bData.profile.Max_Weapon_Drop_Condition_Percent), bData.profile.Max_Weapon_Drop_Condition_Percent);
                    numb = Convert.ToInt16((numb / 100f) * activeItem.maxCondition);
                    activeItem.condition = numb;
                    activeItem.Drop(npc.eyes.position, new Vector3(), new Quaternion());
                    npc.svActiveItemID = 0;
                    npc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }
                ItemContainer[] source = { npc.inventory.containerMain, npc.inventory.containerWear, npc.inventory.containerBelt };
                Inv botInv = new Inv() { profile = bData.profile };
                botInventories.Add(npc.userID, botInv);
                for (int i = 0; i < source.Length; i++)
                    foreach (var item in source[i].itemList)
                        botInv.inventory[i].Add(new InvContents
                        {
                            ID = item.info.itemid,
                            amount = item.amount,
                            skinID = item.skin,
                        });

                if (bData.profile.Disable_Radio == True)
                    npc.DeathEffect = new GameObjectRef();//kill radio effects 

                DeadNPCPlayerIds.Add(npc.userID);
                no_of_AI--;
                if (bData.respawn == False)
                    return;

                if (bData.biome && spawnLists.ContainsKey(bData.monumentName))
                {
                    List<Vector3> spawnList = new List<Vector3>();
                    spawnList = spawnLists[bData.monumentName];
                    int spawnPos = random.Next(spawnList.Count);
                    if (CanRespawn(bData.monumentName, 1, False) == 1)
                    {
                        timer.Once(bData.profile.Respawn_Timer, () =>
                        {
                            if (AllProfiles.ContainsKey(bData.monumentName))
                            {
                                if (CanRespawn(bData.monumentName, 1, True) == 1)
                                {
                                    SpawnBots(bData.monumentName, AllProfiles[bData.monumentName], "biome", null, spawnList[spawnPos], -1);    
                                    if (bData.profile.Announce_Spawn && bData.profile.Announcement_Text != String.Empty)  
                                        PrintToChat(bData.profile.Announcement_Text);
                                }
                            } 
                        });
                    }
                    return; 
                }
                int num = spawnsData.CustomSpawnLocations[bData.monumentName].Count;
                if (!DontRespawn.Contains(npc.userID) && CanRespawn(bData.monumentName, 1, False) == 1)
                {
                    timer.Once(bData.profile.Respawn_Timer, () => 
                    {
                        int spawnnum = 0;
                        if (AllProfiles.ContainsKey(bData.monumentName))
                        {
                            if (CanRespawn(bData.monumentName, 1, True) == 1)
                            {
                                if (num > 0 && bData.profile.UseCustomSpawns)
                                {
                                    if (!bData.profile.ChangeCustomSpawnOnDeath)
                                        spawnnum = bData.CustomSpawnNum;
                                    else
                                    {
                                        availableStatSpawns[bData.monumentName].Add(bData.CustomSpawnNum);
                                        spawnnum = GetNextSpawn(bData.monumentName, bData.profile);
                                    }
                                }
                                SpawnBots(bData.monumentName, AllProfiles[bData.monumentName], null, null, new Vector3(), spawnnum);
                                if (bData.profile.Announce_Spawn && bData.profile.Announcement_Text != String.Empty)
                                    PrintToChat(bData.profile.Announcement_Text);
                            }
                            else if (bData.profile.UseCustomSpawns)// && !bData.profile.ChangeCustomSpawnOnDeath) 
                                availableStatSpawns[bData.monumentName].Add(bData.CustomSpawnNum);
                        }
                    });
                }
                else
                {
                    if (DontRespawn.Contains(npc.userID))
                        DontRespawn.Remove(npc.userID);
                    if (bData.profile.UseCustomSpawns)// && !bData.profile.ChangeCustomSpawnOnDeath)
                        availableStatSpawns[bData.monumentName].Add(bData.CustomSpawnNum);
                }
                //UnityEngine.Object.Destroy(npc.GetComponent<BotData>());
            }
        }

        public static readonly FieldInfo AllScientists = typeof(Scientist).GetField("AllScientists", (BindingFlags.Static | BindingFlags.Public)); //NRE AskQuestion workaround
        void OnEntitySpawned(Scientist sci)
        {
            if (loaded && sci != null)
                AllScientists.SetValue(sci, new HashSet<Scientist>());//NRE AskQuestion workaround 
        }

        void OnEntitySpawned(DroppedItemContainer container)
        {
            NextTick(() =>
            {
                if (!loaded || container == null || container.IsDestroyed)
                    return;

                if (container.playerSteamID == 0) return;

                if (configData.Global.Remove_BackPacks_Percent >= GetRand(1, 101))
                    if (DeadNPCPlayerIds.Contains(container.playerSteamID))
                    {
                        container.Kill();
                        DeadNPCPlayerIds.Remove(container.playerSteamID);
                        return;
                    }
            });
        }

        void OnEntitySpawned(SupplySignal signal)
        {
            timer.Once(2.3f, () =>
            {
                if (!loaded || signal != null)
                    SmokeGrenades.Add(new Vector3(signal.transform.position.x, 0, signal.transform.position.z));
            });
        }

        void OnEntitySpawned(SupplyDrop drop)
        {
            if (!loaded || (!drop.name.Contains("supply_drop") && !drop.name.Contains("sleigh/presentdrop")))
                return;

            if (!configData.Global.Supply_Enabled) 
            {
                foreach (var location in SmokeGrenades.Where(location => Vector3.Distance(location, new Vector3(drop.transform.position.x, 0, drop.transform.position.z)) < 35f))
                {
                    SmokeGrenades.Remove(location); 
                    return;
                }
            }
            if (AllProfiles.ContainsKey("AirDrop"))
            {
                var prof = AllProfiles["AirDrop"]; 
                if (prof.AutoSpawn == True && prof.Day_Time_Spawn_Amount > 0)
                {
                    var profile = AllProfiles["AirDrop"];
                    if (profile.Announce_Spawn && profile.Announcement_Text != String.Empty)
                        PrintToChat(profile.Announcement_Text);

                    timer.Repeat(1f, profile.Day_Time_Spawn_Amount, () =>
                    {
                        if (drop == null)
                            return;
                        profile.Location = drop.transform.position;
                        SpawnBots("AirDrop", profile, "AirDrop", null, new Vector3(), -1); 
                    });
                }
            }
        }
        void OnCrateHack(HackableLockedCrate crate)
        {
            if (!HasNav(crate.transform.position))
                return;

            NextTick(() =>
            {
                if (crate == null || CrateIDs.Contains(crate.net.ID))
                    return;
                if (CrateIDs.Contains(crate.net.ID))
                    CrateIDs.Remove(crate.net.ID);
                if (AllProfiles.ContainsKey("HackableLockedCrate"))
                {
                    var prof = AllProfiles["HackableLockedCrate"];
                    if (prof.AutoSpawn == True && prof.Day_Time_Spawn_Amount > 0)
                    {
                        var profile = AllProfiles["HackableLockedCrate"];
                        if (profile.Announce_Spawn && profile.Announcement_Text != String.Empty)
                            PrintToChat(profile.Announcement_Text);

                        timer.Repeat(1f, profile.Day_Time_Spawn_Amount, () =>
                        {
                            if (crate == null)
                                return;
                            profile.Location = crate.transform.position;
                            SpawnBots("HackableLockedCrate", profile, "HackableLockedCrate", null, new Vector3(), -1);
                        });
                    }
                }
            });
        }

        void OnEntitySpawned(NPCPlayerCorpse corpse)
        {
            if (!loaded || corpse == null)
                return;

            ulong id = corpse.playerSteamID;
            timer.Once(0.1f, () =>
            {
                if (corpse == null || corpse.IsDestroyed || !botInventories.ContainsKey(id))
                    return;

                Inv botInv = botInventories[id]; 
                DataProfile profile = botInv.profile;

                timer.Once(profile.Corpse_Duration, () =>
                {
                    if (corpse != null && !corpse.IsDestroyed)  
                        corpse?.Kill();
                });
                timer.Once(2, () => corpse?.ResetRemovalTime(profile.Corpse_Duration));

                List<Item> toDestroy = new List<Item>();
                foreach (var item in corpse.containers[0].itemList)
                {
                    if (item.ToString().ToLower().Contains("keycard") && configData.Global.Remove_KeyCard)
                        toDestroy.Add(item);
                }
                foreach (var item in toDestroy)
                    item.Remove();
                if (!(profile.Allow_Rust_Loot_Percent >= GetRand(1, 101)))
                {
                    corpse.containers[0].Clear();
                    corpse.containers[1].Clear();
                    corpse.containers[2].Clear();
                }

                Item playerSkull = ItemManager.CreateByName("skull.human", 1);
                playerSkull.name = string.Concat($"Skull of {corpse.playerName}");
                ItemAmount SkullInfo = new ItemAmount() { itemDef = playerSkull.info, amount = 1, startAmount = 1 };
                var dispenser = corpse.GetComponent<ResourceDispenser>();
                if (dispenser != null)
                {
                    dispenser.containedItems.Add(SkullInfo);
                    dispenser.Initialize();
                }

                for (int i = 0; i < botInv.inventory.Length; i++)
                {
                    foreach (var item in botInv.inventory[i])
                    {
                        var giveItem = ItemManager.CreateByItemID(item.ID, item.amount, item.skinID);
                        if (!giveItem.MoveToContainer(corpse.containers[i], -1, True))
                            giveItem.Remove();
                    }
                }
                timer.Once(5f, () =>
                {
                    botInventories.Remove(id);
                });
                if (profile.Wipe_Belt_Percent >= GetRand(1, 101)) 
                    corpse.containers[2].Clear(); 
                if (profile.Wipe_Clothing_Percent >= GetRand(1, 101))
                    corpse.containers[1].Clear();
                ItemManager.DoRemoves();
            });
        }
        #endregion

        #region WeaponSwitching
        void SelectWeapon(NPCPlayerApex npcPlayer)
        {
            if (npcPlayer == null)
                return;
            Item active = npcPlayer.GetActiveItem();
            var bData = npcPlayer.GetComponent<BotData>();
            if (bData == null)
                return;
            Item targetItem = null;
            List<Item> rangeToUse = new List<Item>();

            if (active == null)
            {
                rangeToUse = bData.Weapons[0];
                targetItem = rangeToUse[random.Next(rangeToUse.Count)];
                foreach (var item in npcPlayer.inventory.containerBelt.itemList.Where(item => item.info.itemid == targetItem.info.itemid))
                    targetItem = item;
            }
            else if (npcPlayer.AttackTarget == null)
            {
                ToggleAggro(npcPlayer, Convert.ToByte(!bData.profile.Peace_Keeper), bData.profile.Aggro_Range);
                if (bData.profile.AlwaysUseLights || IsNight)
                {
                    foreach (var item in npcPlayer.inventory.containerBelt.itemList.Where(item => item.GetHeldEntity() is TorchWeapon))
                        targetItem = item;

                    if (LightEquipped(npcPlayer) != null)
                        targetItem = LightEquipped(npcPlayer);
                }
            }
            else
            {
                float distance = Vector3.Distance(npcPlayer.transform.position, npcPlayer.AttackTarget.transform.position);
                if (bData.Weapons[0].Count == 1 || bData.enemyDistance == GetRange(distance))
                    targetItem = active;
                else
                {
                    bData.enemyDistance = GetRange(distance);
                    rangeToUse = bData.Weapons[bData.enemyDistance];
                    if (!rangeToUse.Any())
                    {
                        if (active.GetHeldEntity() as BaseMelee != null && GetRange(distance) > 1)
                            foreach (var weapon in bData.Weapons[0])
                                if (weapon != active)
                                    targetItem = weapon;
                    }
                    else
                        targetItem = rangeToUse[random.Next(rangeToUse.Count)];
                }
            }
            if (targetItem != null)
                UpdateActiveItem(npcPlayer, targetItem);
            else
                UpdateActiveItem(npcPlayer, active);
        }

        int GetRange(float distance)
        {
            if (distance < 2f) return 1;
            if (distance < 10f) return 2;
            if (distance < 40f) return 3;
            return 4;
        }

        void UpdateActiveItem(NPCPlayerApex npcPlayer, Item item)
        {
            Item activeItem1 = npcPlayer.GetActiveItem();
            HeldEntity heldEntity;
            HeldEntity heldEntity1;
            if (activeItem1 != item)
            {
                npcPlayer.svActiveItemID = 0U;
                if (activeItem1 != null)
                {
                    heldEntity = activeItem1.GetHeldEntity() as HeldEntity;
                    if (heldEntity != null)
                        heldEntity.SetHeld(False);
                }
                npcPlayer.svActiveItemID = item.uid;
                npcPlayer.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                npcPlayer.inventory.UpdatedVisibleHolsteredItems();
                npcPlayer.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                SetRange(npcPlayer, item);
                heldEntity1 = npcPlayer.GetActiveItem()?.GetHeldEntity() as HeldEntity;
                if (heldEntity1 != null)
                    heldEntity1.SetHeld(True);
            }
            else
            {
                var lights = npcPlayer.GetComponent<BotData>().profile.AlwaysUseLights;
                heldEntity1 = npcPlayer.GetActiveItem()?.GetHeldEntity() as HeldEntity;
                if (heldEntity1 != null)
                    heldEntity1.SetLightsOn(lights ? True : IsNight);
                HeadLampToggle(npcPlayer, lights ? True : IsNight);
            }
        }

        Item LightEquipped(NPCPlayerApex npcPlayer)
        {
            foreach (var item in npcPlayer.inventory.containerBelt.itemList.Where(item => item.GetHeldEntity() is BaseProjectile && item.contents != null))
                foreach (var mod in (item.contents.itemList).Where(mod => mod.GetHeldEntity() as ProjectileWeaponMod != null && mod.info.name == "flashlightmod.item"))
                    return item;
            return null;
        }

        void SetRange(NPCPlayerApex npcPlayer, Item item)
        {
            var bData = npcPlayer.GetComponent<BotData>();
            var weapon = npcPlayer.GetHeldEntity() as AttackEntity;
            if (bData != null && weapon != null)
                weapon.effectiveRange = bData.Weapons[1].Contains(item) ? 2 : 350;
        }

        void HeadLampToggle(NPCPlayerApex npcPlayer, bool On)
        {
            foreach (var item in npcPlayer.inventory.containerWear.itemList)
                if (item.info.shortname.Equals("hat.miner") || item.info.shortname.Equals("hat.candle"))
                {
                    if ((On && !item.IsOn()) || (!On && item.IsOn()))
                    {
                        item.SwitchOnOff(On);
                        npcPlayer.inventory.ServerUpdate(0f);
                        break;
                    }
                }
        }
        #endregion 

        List<ulong> HumanNPCs = new List<ulong>();
        void OnEntitySpawned(BasePlayer player)
        {
            if (player?.net?.connection == null)
                foreach (var comp in player?.GetComponents<Component>())
                    if (comp.GetType().Name == "HumanPlayer")
                    {
                        HumanNPCs.Add(player.userID);
                        break;
                    }
        }

        #region onnpHooks
        object OnNpcResume(NPCPlayerApex npcPlayer) 
        {
            var bData = npcPlayer.GetComponent<BotData>();
            return (bData != null && (bData.inAir || bData.stationary)) ? True : Null;   
        }

        object OnNpcDestinationSet(NPCPlayerApex npcPlayer)
        {
            if (npcPlayer == null || !npcPlayer.GetNavAgent.isOnNavMesh) 
                return True;

            var bData = npcPlayer.GetComponent<BotData>();
            return (bData != null && bData.goingHome) ? True : Null; 
        }

        object OnNpcTarget(IHTNAgent npc, NPCPlayerApex npcPlayer) 
        {
            if (npcPlayer != null && NPCPlayers.ContainsKey(npcPlayer.userID) && !configData.Global.HTNs_Attack_BotSpawn)
                return True;
            return Null;
        }

        Dictionary<ulong, Timer> CoolDowns = new Dictionary<ulong, Timer>();
        object OnNpcTarget(NPCPlayerApex npcPlayer, BaseEntity entity)
        {
            if (npcPlayer == null || entity == null)
                return null;

            bool attackerIsMine = NPCPlayers.ContainsKey(npcPlayer.userID);

            NPCPlayer botVictim = entity as NPCPlayer;
            if (botVictim != null)
            {
                bool vicIsMine = NPCPlayers.ContainsKey(botVictim.userID);
                if (npcPlayer == botVictim)
                    return null;

                if (vicIsMine && !attackerIsMine && !configData.Global.NPCs_Attack_BotSpawn)//stop oustideNPCs attacking BotSpawn bots    
                    return True;

                if (!attackerIsMine)
                    return null;

                if (vicIsMine)
                {
                    var bData = npcPlayer.GetComponent<BotData>();
                    if (!bData.profile.Attacks_Other_Profiles || bData.monumentName == botVictim.GetComponent<BotData>().monumentName)
                        return True;

                    ForceMemory(npcPlayer, botVictim);
                }
                if (!vicIsMine && !configData.Global.BotSpawn_Attacks_NPCs)//stop BotSpawn bots attacking outsideNPCs 
                    return True;
            }

            if (!attackerIsMine)
                return null;

            BasePlayer victim = entity as BasePlayer;
            if (victim != null)
            {
                if ((victim.InSafeZone() || npcPlayer.InSafeZone()) && !victim.IsHostile())
                {
                    return True;
                }
                if (configData.Global.Ignore_HumanNPC && HumanNPCs.Contains(victim.userID))
                    return True;

                var bData = npcPlayer.GetComponent<BotData>();
                bData.goingHome = False;

                var active = npcPlayer?.GetActiveItem()?.GetHeldEntity() as HeldEntity;
                if (active == null)
                    return null;
                if (bData.profile.Peace_Keeper)
                {
                    bool attack = False;
                    var held = victim.GetHeldEntity();
                    if (victim.svActiveItemID == 0u || held == null) 
                        attack = False;

                    bool isMelee = held is BaseMelee || held is TorchWeapon;
                    bool isGun = held is BaseProjectile || held is FlameThrower;

                    if (isGun || (!configData.Global.PeaceKeepers_Ignore_Melee && isMelee))
                        attack = True;
                    
                    if (attack)
                    {
                        if (!bData.AggroPlayers.Contains(victim.userID))
                        {
                            bData.AggroPlayers.Add(victim.userID);
                            if (!CoolDowns.ContainsKey(victim.userID))
                                CoolDowns.Add(victim.userID, null);
                        }

                        bData.coolDownPlayers.Add(victim.userID);
                        CoolDowns[victim.userID]?.Destroy();
                        CoolDowns[victim.userID] = timer.Once(bData.profile.Peace_Keeper_Cool_Down, () =>
                        {
                            if (bData.AggroPlayers.Contains(victim.userID))
                            {
                                bData.AggroPlayers.Remove(victim.userID);
                                bData.coolDownPlayers.Remove(victim.userID);
                            }
                        });
                    }
                    if (!bData.AggroPlayers.Contains(victim.userID))
                        return True;
                }
                bool OnNav = npcPlayer.GetNavAgent.isOnNavMesh;
                if (OnNav)
                {
                    var distance = Vector3.Distance(npcPlayer.transform.position, victim.transform.position);
                    if (distance < 50)
                    {
                        var heightDifference = victim.transform.position.y - npcPlayer.transform.position.y;
                        if (heightDifference > 5)
                            npcPlayer.SetDestination(npcPlayer.transform.position - (Quaternion.Euler(npcPlayer.serverInput.current.aimAngles) * Vector3.forward * 2));
                    }
                }

                if (!bData.stationary && !bData.inAir && npcPlayer is NPCMurderer)
                {
                    var distance = Vector3.Distance(npcPlayer.transform.position, victim.transform.position);
                    if (npcPlayer.lastAttacker != victim && distance > npcPlayer.Stats.AggressionRange && distance > npcPlayer.Stats.DeaggroRange)
                        return True;

                    var held = npcPlayer.GetHeldEntity();
                    if (held != null)
                    {
                        if (held as BaseProjectile == null && OnNav)
                        {
                            NavMeshPath pathToEntity = new NavMeshPath();
                            npcPlayer.AiContext.AIAgent.GetNavAgent.CalculatePath(victim.ServerPosition, pathToEntity);
                            if (npcPlayer.lastAttacker != null && victim == npcPlayer.lastAttacker && pathToEntity.status == NavMeshPathStatus.PathInvalid && !bData.fleeing)
                            {
                                var heightDifference = victim.transform.position.y - npcPlayer.transform.position.y;
                                if (heightDifference > 1 && distance < 50)
                                {
                                    bData.fleeing = True;
                                    timer.Once(10f, () =>
                                    {
                                        if (npcPlayer != null)
                                            bData.fleeing = False;
                                    });
                                    WipeMemory(npcPlayer);
                                    return True;
                                }
                            }
                            if (pathToEntity.status != NavMeshPathStatus.PathInvalid && bData.fleeing)
                            {
                                ForceMemory(npcPlayer, victim);
                                return null;
                            }
                        }
                    }
                    ForceMemory(npcPlayer, victim);
                }

                bData.goingHome = False;

                if (victim.IsSleeping() && configData.Global.Ignore_Sleepers)
                    return True;
                if (npcPlayer.AttackTarget == null)
                    npcPlayer.AttackTarget = victim;

                ToggleAggro(npcPlayer, 1, npcPlayer.Stats.AggressionRange);
                npcPlayer.lastAttacker = victim;
            }

            return ((entity.name.Contains("agents/") && !(entity is BasePlayer)) || (entity is HTNPlayer && configData.Global.Ignore_HTN))
                ? True
                : Null;
        }

        object OnNpcTarget(BaseNpc npc, NPCPlayer npcPlayer)//stops animals targeting bots
        {
            return (npcPlayer != null && NPCPlayers.ContainsKey(npcPlayer.userID) && configData.Global.Animal_Safe) ? True : Null;
        }

        object OnNpcStopMoving(NPCPlayerApex npcPlayer)
        {
            if (npcPlayer == null)
                return null;
            return NPCPlayers.ContainsKey(npcPlayer.userID) ? True : Null;
        }
        #endregion

        void WipeMemory(NPCPlayerApex npc)
        {
            npc.lastDealtDamageTime = Time.time;
            npc.lastAttackedTime = Time.time;
            npc.AttackTarget = null;
            npc.lastAttacker = null;
            npc.SetFact(NPCPlayerApex.Facts.HasEnemy, 0, True, True);
            npc.SetFact(NPCPlayerApex.Facts.IsAggro, 0, True, True);
            npc.SetFact(NPCPlayerApex.Facts.IsAfraid, 0, True, True);
            npc.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
        }

        void ForceMemory(NPCPlayerApex npc, BasePlayer victim)
        {
            npc.SetFact(NPCPlayerApex.Facts.HasEnemy, 1, True, True);
            Vector3 vector3;
            float single, single1, single2;
            Rust.Ai.BestPlayerDirection.Evaluate(npc, victim.ServerPosition, out vector3, out single);
            Rust.Ai.BestPlayerDistance.Evaluate(npc, victim.ServerPosition, out single1, out single2);
            var info = new Rust.Ai.Memory.ExtendedInfo();
            npc.AiContext.Memory.Update(victim, victim.ServerPosition, 1, vector3, single, single1, 1, True, 1f, out info);
        }

        #region SetUpLocations
        public Dictionary<string, ConfigProfile> GotMonuments = new Dictionary<string, ConfigProfile>();
        public Dictionary<string, GameObject> mons = new Dictionary<string, GameObject>();
        public List<GameObject> RemoveObj = new List<GameObject>();

        void CheckMonuments(bool add)
        {
            foreach (var monumentInfo in TerrainMeta.Path.Monuments.OrderBy(x => x.displayPhrase.english))
            {
                var displayPhrase = monumentInfo.displayPhrase.english.Replace("\n", String.Empty);
                if (displayPhrase.Contains("Oil Rig") || displayPhrase.Contains("Water Well"))
                    continue;
                GameObject gobject = monumentInfo.gameObject;
                var pos = monumentInfo.gameObject.transform.position;
                var rot = monumentInfo.gameObject.transform.eulerAngles.y;
                int counter = 0;
                if (displayPhrase != String.Empty)
                {
                    if (add)
                    {
                        foreach (var entry in AllProfiles.Where(x => x.Key.Contains(displayPhrase) && x.Key.Length == displayPhrase.Length + 2))
                            counter++;
                        if (counter < 10)
                        {
                            mons.Add($"{displayPhrase} {counter}", gobject);
                            AddProfile($"{displayPhrase} {counter}", null, pos);
                        }
                    }
                    else
                    {
                        foreach (var entry in GotMonuments.Where(x => x.Key.Contains(displayPhrase) && x.Key.Length == displayPhrase.Length + 2))
                            counter++;
                        if (counter < 10)
                            GotMonuments.Add($"{displayPhrase} {counter}", new ConfigProfile());
                    }
                }
            }
        }

        private void SetupProfiles()
        {
            CheckMonuments(True);
            int BiomeCounter = 1;
            foreach (var entry in defaultData.Biomes)
            {
                ConfigProfile prof = JsonConvert.DeserializeObject<ConfigProfile>(JsonConvert.SerializeObject(entry.Value));
                AddProfile(entry.Key, prof, new Vector3());
                if (entry.Value.AutoSpawn)
                    GenerateSpawnPoints(entry.Key, Mathf.Max(prof.Night_Time_Spawn_Amount, prof.Day_Time_Spawn_Amount), timers[entry.Key], BiomeCounter);
                BiomeCounter *= 2;
            }

            DataProfile Airdrop = JsonConvert.DeserializeObject<DataProfile>(JsonConvert.SerializeObject(defaultData.Events.AirDrop));
            AllProfiles.Add("AirDrop", Airdrop);
            DataProfile HackableLockedCrate = JsonConvert.DeserializeObject<DataProfile>(JsonConvert.SerializeObject(defaultData.Events.HackableLockedCrate));
            AllProfiles.Add("HackableLockedCrate", HackableLockedCrate);
            foreach (var profile in storedData.DataProfiles)
                AddData(profile.Key, profile.Value);

            SaveData();
            SetupSpawnsFile();
            foreach (var profile in AllProfiles)
            {
                popinfo.Add(profile.Key, new PopInfo());
                if (timers.ContainsKey(profile.Key) || profile.Key.Contains("AirDrop") || profile.Key.Contains("HackableLockedCrate"))
                    continue;
                if (profile.Value.Kit.Count > 0 && Kits == null)
                    PrintWarning(lang.GetMessage("nokits", this), profile.Key);

                int num = spawnsData.CustomSpawnLocations[profile.Key].Count;
                if (profile.Value.AutoSpawn == True && (profile.Value.Day_Time_Spawn_Amount > 0 || profile.Value.Night_Time_Spawn_Amount > 0))
                {
                    for (int i = 0; i < num; i++)
                        availableStatSpawns[profile.Key].Add(i);

                    int target = TargetAmount(profile.Value);
                    if (target > 0)
                    {
                        int amount = CanRespawn(profile.Key, target, False);
                        if (amount > 0)
                            timer.Repeat(0.5f, amount, () =>
                            {
                                if (AllProfiles.Contains(profile) && CanRespawn(profile.Key, 1, True) == 1)
                                {
                                    int point = GetNextSpawn(profile.Key, profile.Value);
                                    SpawnBots(profile.Key, AllProfiles[profile.Key], null, null, new Vector3(), point);
                                }
                            });
                    }
                }
            }
        }

        void AddProfile(string name, ConfigProfile monument, Vector3 pos)//bring config data into live data   
        {
            if (monument == null && defaultData.Monuments.ContainsKey(name))
                monument = defaultData.Monuments[name];
            else if (monument == null)
            {
                monument = new ConfigProfile();
            }
             
            var toAdd = JsonConvert.SerializeObject(monument);
            DataProfile toAddDone = JsonConvert.DeserializeObject<DataProfile>(toAdd);
            if (AllProfiles.ContainsKey(name))
                return;

            AllProfiles.Add(name, toAddDone);
            AllProfiles[name].Location = pos;

            foreach (var custom in storedData.DataProfiles)  
            {
                if (!storedData.MigrationDataDoNotEdit.ContainsKey(custom.Key))
                    storedData.MigrationDataDoNotEdit[custom.Key] = new ProfileRelocation();
                if (custom.Value.Parent_Monument == name)  
                {
                    var path = storedData.MigrationDataDoNotEdit[custom.Key];  
                    if (path.ParentMonument == new Vector3())
                    {
                        Puts($"Parent_Monument added for {custom.Key}. Removing any existing custom spawn points"); 
                        spawnsData.CustomSpawnLocations[custom.Key].Clear();
                        SaveSpawns();
                        path.ParentMonument = pos;
                        path.Offset = mons[name].transform.InverseTransformPoint(custom.Value.Location);
                    }
                }
                else if (custom.Value.Parent_Monument == "" && storedData.MigrationDataDoNotEdit[custom.Key].ParentMonument != new Vector3()) 
                {
                    Puts($"Parent_Monument removed for {custom.Key}. Removing any existing custom spawn points");
                    spawnsData.CustomSpawnLocations[custom.Key].Clear();
                    storedData.MigrationDataDoNotEdit[custom.Key] = new ProfileRelocation();   
                    SaveSpawns(); 
                }
            }
            //SaveData(); 
        }

        void AddData(string name, DataProfile profile)  
        {
            if (!storedData.MigrationDataDoNotEdit.ContainsKey(name)) 
                storedData.MigrationDataDoNotEdit.Add(name, new ProfileRelocation());

            var path = storedData.MigrationDataDoNotEdit[name];

            if (profile.Parent_Monument != String.Empty)
            {
                if (AllProfiles.ContainsKey(profile.Parent_Monument) && !timers.ContainsKey(profile.Parent_Monument))    
                {
                    if (path.ParentMonument != AllProfiles[profile.Parent_Monument].Location)   
                    {
                        bool userChanged = False; 
                        foreach (var monument in AllProfiles)  
                            if (monument.Value.Location == AllProfiles[profile.Parent_Monument].Location && monument.Key != profile.Parent_Monument)  
                            {
                                userChanged = True; 
                                break; 
                            }


                        profile.Location = mons[profile.Parent_Monument].transform.TransformPoint(path.Offset);

                        if (userChanged)
                        {
                            Puts($"Parent_Monument change detected for {name}. Removing any existing custom spawn points");
                            spawnsData.CustomSpawnLocations[name].Clear();
                            SaveSpawns();
                        }

                        path.ParentMonument = AllProfiles[profile.Parent_Monument].Location; 
                        path.Offset = mons[profile.Parent_Monument].transform.InverseTransformPoint(profile.Location);
                    }
                }
                else
                {
                    if (profile.AutoSpawn == True)
                    {
                        Puts($"Parent monument {profile.Parent_Monument} does not exist for custom profile {name}"); 
                        return;
                    }
                }
            }

            SaveData();
            AllProfiles[name] = profile;
            GameObject obj = new GameObject();
            obj.transform.position = profile.Location;
            mons[name] = obj;
            RemoveObj.Add(obj);
        }

        void SetupSpawnsFile()
        {
            bool flag = False;
            foreach (var entry in AllProfiles.Where(entry => !timers.ContainsKey(entry.Key) && entry.Key != "AirDrop" && entry.Key != "HackableLockedCrate"))
            {
                if (!spawnsData.CustomSpawnLocations.ContainsKey(entry.Key))
                {
                    spawnsData.CustomSpawnLocations.Add(entry.Key, new List<string>());
                    flag = True;
                }
                if (!availableStatSpawns.ContainsKey(entry.Key))
                    availableStatSpawns.Add(entry.Key, new List<int>());

                CheckSpawnPop(entry.Key, entry.Value);
            }
            if (flag)
                SaveSpawns();
        }

        void CheckSpawnPop(string name, DataProfile profile)
        {
            if (profile.AutoSpawn && profile.UseCustomSpawns)
            {
                if (spawnsData.CustomSpawnLocations[name].Count < profile.Day_Time_Spawn_Amount)
                {
                    PrintWarning(lang.GetMessage("notenoughspawns", this), name);
                    profile.Day_Time_Spawn_Amount = spawnsData.CustomSpawnLocations[name].Count;
                }
                if (spawnsData.CustomSpawnLocations[name].Count < profile.Night_Time_Spawn_Amount)
                {
                    PrintWarning(lang.GetMessage("notenoughspawns", this), name);
                    profile.Night_Time_Spawn_Amount = spawnsData.CustomSpawnLocations[name].Count;
                }
                else if (spawnsData.CustomSpawnLocations[name].Count == 0)
                    PrintWarning(lang.GetMessage("nospawns", this), name);
            }
        }
        #endregion

        #region Commands
        [ConsoleCommand("bot.count")]
        void CmdBotCount()
        {
            string msg = (NPCPlayers.Count == 1) ? "numberOfBot" : "numberOfBots";
            PrintWarning(lang.GetMessage(msg, this), NPCPlayers.Count);
        }

        [ConsoleCommand("bots.count")]
        void CmdBotsCount()
        {
            var records = BotSpawnBots();
            if (records.Count == 0)
            {
                PrintWarning("There are no spawned npcs");
                return;
            }
            bool none = True;
            foreach (var entry in records)
                if (entry.Value.Count > 0)
                    none = False;
            if (none)
            {
                PrintWarning("There are no spawned npcs");
                return;
            }
            foreach (var entry in BotSpawnBots().Where(x => AllProfiles[x.Key].AutoSpawn == True))
                PrintWarning(entry.Key + " - " + entry.Value.Count);
        }

        [ConsoleCommand("botspawn")]
        private void CmdBotSpawn(ConsoleSystem.Arg arg)
        {
            var player = arg?.Connection?.player as BasePlayer;
            if ((player != null && player?.net?.connection.authLevel < 2) || arg?.Args?.Length != 2)
                return;
            if (arg.Args[0] == "spawn")
            {
                var profile = arg.Args[1];
                foreach (var entry in AllProfiles.Where(entry => entry.Key.ToLower() == profile.ToLower()))
                {
                    if (timers.ContainsKey(entry.Key) || entry.Key == "AirDrop" || entry.Key == "HackableLockedCrate")
                    {
                        PrintWarning(lang.GetMessage("Title", this) + lang.GetMessage("norespawn", this));
                        continue;
                    }
                    var loc = entry.Value.UseCustomSpawns && spawnsData.CustomSpawnLocations[entry.Key].Count > 0 ? Vector3.zero : entry.Value.Location;
                    if (TargetAmount(AllProfiles[entry.Key]) == 0)
                    {
                        PrintWarning(lang.GetMessage("Title", this) + lang.GetMessage("targetzero", this), entry.Key);
                        return;
                    }
                    timer.Repeat(1f, TargetAmount(entry.Value), () => DeployNpcs(loc, entry.Key, entry.Value, null, GetNextSpawn(entry.Key, entry.Value)));
                    PrintWarning(lang.GetMessage("Title", this) + lang.GetMessage("deployed", this), entry.Key, entry.Value.Location);
                    return;
                }
                PrintWarning(lang.GetMessage("Title", this) + lang.GetMessage("noprofile", this));
            }
            if (arg.Args[0] == "kill")
            {
                var profile = arg.Args[1];
                BotData bData = null;
                List<NPCPlayerApex> killList = new List<NPCPlayerApex>();
                bool found = False;
                foreach (var entry in AllProfiles.Where(entry => entry.Key.ToLower() == profile.ToLower()))
                {
                    foreach (var npc in NPCPlayers)
                    {
                        bData = npc.Value.GetComponent<BotData>();
                        if (bData.monumentName.ToLower() == entry.Key.ToLower() && !bData.respawn)
                        {
                            found = True;
                            killList.Add(npc.Value);
                        }
                    }
                    if (found)
                    {
                        NextTick(() =>
                        {
                            PrintWarning(lang.GetMessage("Title", this) + lang.GetMessage("killed", this), entry.Key);
                            foreach (var npc in killList.ToList())
                                npc.Kill();
                        });
                    }
                    else
                        PrintWarning(lang.GetMessage("Title", this) + lang.GetMessage("nonpcs", this));
                    return;
                }
                PrintWarning(lang.GetMessage("Title", this) + lang.GetMessage("noprofile", this));
            }
        }

        public NPCPlayerApex GetNPCPlayer(BasePlayer player)
        {
            Vector3 start = player.eyes.position;
            Ray ray = new Ray(start, Quaternion.Euler(player.eyes.rotation.eulerAngles) * Vector3.forward);
            var hits = Physics.RaycastAll(ray);
            foreach (var hit in hits)
            {
                var npc = hit.collider?.GetComponentInParent<NPCPlayerApex>();
                if (npc?.GetComponent<BotData>() != null && hit.distance < 2f)
                    return npc;
            }
            return null;
        }

        string TitleText => "<color=orange>" + lang.GetMessage("Title", this) + "</color>";

        [ConsoleCommand("botspawn.toplayer")]
        private void botspawnToPlayer(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || arg?.Args.Length < 2)
                return;
            Botspawn(arg.Player(), "botspawn", new string[] { "toplayer", arg.Args[0], arg.Args[1] });
        }

        [ConsoleCommand("botspawn.addspawn")] 
        private void botspawnAddSpawn(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            Botspawn(arg.Player(), "botspawn", new string[] { "addspawn" });
        }

        [ConsoleCommand("botspawn.removespawn")]
        private void botspawnRemoveSpawn(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            Botspawn(arg.Player(), "botspawn", new string[] { "removespawn" });
        }

        [ConsoleCommand("botspawn.info")]
        private void botspawnInfo(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            Botspawn(arg.Player(), "botspawn", new string[] { "info" });
        }

        [ChatCommand("botspawn")]
        void Botspawn(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permAllowed) && !IsAuth(player))
                return;
            string pn = string.Empty;
            var sp = spawnsData.CustomSpawnLocations;

            if (args != null && args.Length == 1)
            {
                if (args[0] == "info")
                {
                    var npc = GetNPCPlayer(player);
                    if (npc == null)
                        SendReply(player, TitleText + lang.GetMessage("nonpc", this));
                    else
                        SendReply(player, TitleText + "NPC from profile - " + npc.GetComponent<BotData>().monumentName);
                    return;
                }
                if (args[0] == "list")
                {
                    var outMsg = lang.GetMessage("ListTitle", this);
                    foreach (var profile in storedData.DataProfiles)
                        outMsg += $"\n{profile.Key}";
                    PrintToChat(player, outMsg);
                    return;
                }

                if (editing.ContainsKey(player.userID) && AllProfiles.ContainsKey(editing[player.userID]))
                    pn = editing[player.userID]; 
                else
                {
                    SendReply(player, TitleText + lang.GetMessage("notediting", this));
                    return;
                }
                if (args[0] == "addspawn")
                {
                    var rot = player.viewAngles.y;
                    if (!HasNav(player.transform.position) && !AllProfiles[pn].Stationary)
                        SendReply(player, TitleText + lang.GetMessage("noNavHere", this));

                    var t = mons.ContainsKey(AllProfiles[pn].Parent_Monument) ? mons[AllProfiles[pn].Parent_Monument].transform : mons.ContainsKey(pn) ? mons[pn].transform : null;
                    if (t != null)
                    {
                        Vector3 loc = t.InverseTransformPoint(player.transform.position);
                        sp[pn].Add($"{loc.x},{loc.y},{loc.z},{rot - t.eulerAngles.y}");
                        SaveSpawns();
                        ShowSpawn(player, player.transform.position, sp[pn].Count, configData.Global.AddSpawn_Show_Seconds);
                        SendReply(player, TitleText + lang.GetMessage("addedspawn", this), sp[pn].Count, pn);
                    }
                    return;
                }
                if (args[0] == "removespawn")
                {
                    if (sp[pn].Count > 0)
                    {
                        sp[pn].RemoveAt(sp[pn].Count - 1);
                        SaveSpawns();
                        CheckSpawnPop(pn, AllProfiles[pn]);
                        SendReply(player, TitleText + lang.GetMessage("removedspawn", this), pn, sp[pn].Count);
                    }
                    else
                    {
                        SendReply(player, TitleText + lang.GetMessage("nospawns", this), pn, sp[pn].Count);
                    }
                    return;
                }
                SendReply(player, TitleText + lang.GetMessage("error", this));

            }
            else if (args != null && args.Length == 2)
            {
                var name = args[1];
                if (args[0] == "reload")
                {
                    if (AllProfiles.ContainsKey(name))
                    {
                        foreach (var npc in NPCPlayers.ToList())
                        {
                            var bData = npc.Value.GetComponent<BotData>();
                            if (bData.monumentName == name)
                            {
                                bData.profile = AllProfiles[name].Clone();
                                bData.profile.Respawn_Timer = 1;
                                DontRespawn.Add(npc.Key);
                                npc.Value.Kill();
                            }
                        }
                        ReloadData(name);

                        SendReply(player, TitleText + lang.GetMessage("reloaded", this));
                        return;
                    }
                    SendReply(player, TitleText + lang.GetMessage("noprofile", this));
                }
                if (args[0] == "show")
                {
                    int amount;
                    if (int.TryParse(args[1], out amount))
                    {
                        string entry = null;
                        if (editing.ContainsKey(player.userID))
                            entry = editing[player.userID];

                        if (string.IsNullOrEmpty(entry))
                        {
                            SendReply(player, TitleText + lang.GetMessage("notediting", this));
                            return;
                        }

                        if (AllProfiles.ContainsKey(entry) && !timers.ContainsKey(entry) && name != "AirDrop" && name != "HackableLockedCrate")
                        {
                            if (editing.ContainsKey(player.userID))
                                editing[player.userID] = entry;
                            else
                                editing.Add(player.userID, entry);
                            var path = spawnsData.CustomSpawnLocations[entry];
                            for (int i = 0; i < path.Count; i++)
                            {
                                var t = mons.ContainsKey(AllProfiles[entry].Parent_Monument) ? mons[AllProfiles[entry].Parent_Monument].transform : mons.ContainsKey(entry) ? mons[entry].transform : null;
                                ShowSpawn(player, t.TransformPoint(s2v(path[i])), i + 1, amount);
                            }
                            return;
                        }
                    }
                    else
                        SendReply(player, TitleText + lang.GetMessage("showduration", this), name);
                }
                if (args[0] == "edit")
                {
                    if (!spawnsData.CustomSpawnLocations.ContainsKey(name))
                    {
                        SendReply(player, TitleText + lang.GetMessage("noprofile", this));
                        return;
                    }

                    var path = spawnsData.CustomSpawnLocations[name];
                    if (AllProfiles.ContainsKey(name) && !timers.ContainsKey(name) && name != "AirDrop" && name != "HackableLockedCrate")
                    {
                        if (editing.ContainsKey(player.userID))
                            editing[player.userID] = name; 
                        else
                            editing.Add(player.userID, name);
                        for (int i = 0; i < path.Count; i++)
                        {
                            var t = mons.ContainsKey(AllProfiles[name].Parent_Monument) ? mons[AllProfiles[name].Parent_Monument].transform : mons.ContainsKey(name) ? mons[name].transform : null;
                            ShowSpawn(player, t.TransformPoint(s2v(path[i])), i + 1, 10f);
                        }
                        SendReply(player, TitleText + lang.GetMessage("editingname", this), name); 
                    }
                    else
                        SendReply(player, TitleText + lang.GetMessage("noprofile", this));
                    return;
                }
                if (args[0] == "add")
                {
                    if (AllProfiles.ContainsKey(name))
                    {
                        SendReply(player, TitleText + lang.GetMessage("alreadyexists", this), name);
                        return; 
                    }
                    var customSettings = new DataProfile()
                    {
                        AutoSpawn = False,
                        BotNames = new List<string> { String.Empty },
                        Location = player.transform.position,
                    };
                    storedData.DataProfiles.Add(name, customSettings);
                    AddData(name, customSettings);

                    popinfo.Add(name, new PopInfo());

                    SetupSpawnsFile();
                    if (editing.ContainsKey(player.userID))
                        editing[player.userID] = name;
                    else
                        editing.Add(player.userID, name);

                    SaveData();
                    SendReply(player, TitleText + lang.GetMessage("customsaved", this), player.transform.position); 
                    return;
                }

                if (args[0] == "move") 
                {
                    if (storedData.DataProfiles.ContainsKey(name))
                    {
                        var d = storedData.DataProfiles[name];
                        d.Location = player.transform.position;
                        if (AllProfiles.ContainsKey(d.Parent_Monument))
                            storedData.MigrationDataDoNotEdit[name].Offset = mons[d.Parent_Monument].transform.InverseTransformPoint(player.transform.position);
                        SaveData();
                        SendReply(player, TitleText + lang.GetMessage("custommoved", this), name);
                    }
                    else
                        SendReply(player, TitleText + lang.GetMessage("noprofile", this));
                    return;
                }

                if (args[0] == "remove")
                {
                    if (storedData.DataProfiles.ContainsKey(name))
                    {
                        List<NPCPlayerApex> toDestroy = new List<NPCPlayerApex>();

                        foreach (var bot in NPCPlayers) 
                        {
                            if (bot.Value == null)
                                continue;
                            var bData = bot.Value.GetComponent<BotData>();
                            if (bData.monumentName == name)
                                toDestroy.Add(bot.Value);
                        }
                        NextTick(() =>
                        {
                            foreach (var killBot in toDestroy)
                                killBot.Kill();
                        });
                        spawnsData.CustomSpawnLocations[name].Clear(); 
                        SaveSpawns();
                        AllProfiles.Remove(name);
                        storedData.DataProfiles.Remove(name);
                        storedData.MigrationDataDoNotEdit.Remove(name);
                        SaveData();
                        SendReply(player, TitleText + lang.GetMessage("customremoved", this), name);
                    }
                    else
                        SendReply(player, TitleText + lang.GetMessage("noprofile", this));
                    return;
                }
                if (args[0] == "spawn")
                {
                    var profile = args[1];
                    foreach (var entry in AllProfiles.Where(entry => entry.Key.ToLower() == profile.ToLower()))
                    {
                        if (timers.ContainsKey(entry.Key) || entry.Key == "AirDrop" || entry.Key == "HackableLockedCrate")
                        {
                            SendReply(player, TitleText + lang.GetMessage("norespawn", this));
                            continue;
                        }
                        var loc = entry.Value.UseCustomSpawns && spawnsData.CustomSpawnLocations[entry.Key].Count > 0 ? Vector3.zero : entry.Value.Location;
                        if (TargetAmount(AllProfiles[entry.Key]) == 0)
                        {
                            SendReply(player, lang.GetMessage("Title", this) + lang.GetMessage("targetzero", this), entry.Key);
                            return;
                        }
                        timer.Repeat(1f, TargetAmount(AllProfiles[entry.Key]), () => DeployNpcs(loc, entry.Key, entry.Value, null, GetNextSpawn(entry.Key, entry.Value)));
                        SendReply(player, TitleText + lang.GetMessage("deployed", this), entry.Key, entry.Value.Location);
                        return;
                    }
                    SendReply(player, TitleText + lang.GetMessage("noprofile", this));
                    return;
                }
                if (args[0] == "kill")
                {
                    var profile = args[1];
                    var found = False;
                    List<NPCPlayerApex> killList = new List<NPCPlayerApex>();
                    foreach (var npc in NPCPlayers)
                    {
                        var bData = npc.Value.GetComponent<BotData>();
                        if (bData.monumentName.ToLower() == profile.ToLower() && !bData.respawn)
                        {
                            found = True;
                            killList.Add(npc.Value);
                        }
                    }

                    NextTick(() =>
                    {
                        if (found)
                        {
                            SendReply(player, TitleText + lang.GetMessage("killed", this), profile);
                            foreach (var npc in killList.ToList())
                                npc.Kill();
                        }
                        else
                            SendReply(player, TitleText + lang.GetMessage("nonpcs", this));
                    });
                    return;
                }

                int num = -1;
                int.TryParse(args[1], out num);
                if (num < 1)
                    return;

                if (args[0] == "removespawn")
                {
                    if (editing.ContainsKey(player.userID) && AllProfiles.ContainsKey(editing[player.userID]))
                        pn = editing[player.userID];
                    else
                    {
                        SendReply(player, TitleText + lang.GetMessage("notediting", this));
                        return;
                    }
                    if (sp[pn].Count() - 1 >= num)
                    {
                        sp[pn].RemoveAt(num - 1);
                        SaveSpawns();
                        SendReply(player, TitleText + lang.GetMessage("removednum", this), num, pn);
                        return;
                    }
                    SendReply(player, TitleText + lang.GetMessage("notthatmany", this), pn, Mathf.Max(1, num));
                    return;
                }
                if (args[0] == "movespawn")
                {
                    if (editing.ContainsKey(player.userID) && AllProfiles.ContainsKey(editing[player.userID]))
                        pn = editing[player.userID];
                    else
                    {
                        SendReply(player, TitleText + lang.GetMessage("notediting", this));
                        return;
                    }

                    if (sp[pn].Count() >= num)
                    {
                        var rot = player.viewAngles.y;
                        if (!HasNav(player.transform.position) && !AllProfiles[pn].Stationary)
                            SendReply(player, TitleText + lang.GetMessage("noNavHere", this));

                        var t = mons.ContainsKey(AllProfiles[pn].Parent_Monument) ? mons[AllProfiles[pn].Parent_Monument].transform : mons.ContainsKey(pn) ? mons[pn].transform : null;

                        if (t != null)
                        {
                            Vector3 loc = t.InverseTransformPoint(player.transform.position);
                            sp[pn][num - 1] = ($"{loc.x},{loc.y},{loc.z},{rot - t.transform.eulerAngles.y}");
                            SaveSpawns();
                            ShowSpawn(player, player.transform.position, num, 10f);
                            SendReply(player, TitleText + lang.GetMessage("movedspawn", this), num, pn);
                            return;
                        }
                    }
                    SendReply(player, TitleText + lang.GetMessage("notthatmany", this), pn, Mathf.Max(1, num));
                    return;
                }

                SendReply(player, TitleText + lang.GetMessage("error", this)); 
                return;
            }
            else if (args != null && args.Length == 3)
            {
                if (args[0] == "toplayer")
                {
                    var name = args[1];
                    var profile = args[2].ToLower();
                    BasePlayer target = FindPlayerByName(name);
                    Vector3 location = CalculateGroundPos(player.transform.position);
                    var found = False;
                    if (target == null)
                    {
                        SendReply(player, TitleText + lang.GetMessage("namenotfound", this), name);
                        return;
                    }
                    foreach (var entry in AllProfiles.Where(entry => entry.Key.ToLower() == profile.ToLower()))
                    {
                        if (TargetAmount(AllProfiles[entry.Key]) == 0)
                        {
                            SendReply(player, TitleText + lang.GetMessage("targetzero", this), entry.Key);
                            return;
                        }
                        timer.Repeat(1f, TargetAmount(AllProfiles[entry.Key]), () => DeployNpcs(location, entry.Key, entry.Value, null, -1));
                        SendReply(player, TitleText + lang.GetMessage("deployed", this), entry.Key, target.displayName);
                        found = True;
                        return;
                    }
                    if (!found)
                    {
                        SendReply(player, TitleText + lang.GetMessage("noprofile", this));
                        return;
                    }
                    return;
                }
                SendReply(player, TitleText + lang.GetMessage("error", this));
            }
            else
                SendReply(player, TitleText + lang.GetMessage("error", this));
        }

        void ShowSpawn(BasePlayer player, Vector3 loc, int num, float duration) => player.SendConsoleCommand("ddraw.text", duration, Color.green, loc, $"<size=80>{num}</size>");
        #endregion

        public List<ulong> DeadNPCPlayerIds = new List<ulong>(); //to tracebackpacks
        public Dictionary<ulong, string> KitRemoveList = new Dictionary<ulong, string>();
        public List<Vector3> SmokeGrenades = new List<Vector3>();
        public Dictionary<ulong, Inv> botInventories = new Dictionary<ulong, Inv>();

        public class Inv
        {
            public DataProfile profile = new DataProfile();
            public List<InvContents>[] inventory = { new List<InvContents>(), new List<InvContents>(), new List<InvContents>() };
        }

        public class InvContents
        {
            public int ID;
            public int amount;
            public ulong skinID;
        }

        #region BotMono
        public class BotData : MonoBehaviour
        {
            public NPCPlayerApex npc;
            public List<ulong> AggroPlayers = new List<ulong>();
            public List<ulong> coolDownPlayers = new List<ulong>();
            public DataProfile profile;
            public Vector3 spawnPoint;
            public List<Item>[] Weapons = { new List<Item>(), new List<Item>(), new List<Item>(), new List<Item>(), new List<Item>() };
            public int CustomSpawnNum, enemyDistance, landingAttempts;
            public string monumentName, group; //external hook identifier 
            public bool noweapon, fleeing, hasHeadLamp, stationary, inAir, goingHome, biome, respawn;
            CapsuleCollider capcol;
            Vector3 landingDirection = Vector3.zero;

            int updateCounter;

            void Start()
            {
                npc = GetComponent<NPCPlayerApex>();
                if (npc.WaterFactor() > 0.9f)
                {
                    npc.Kill();
                    return;
                }
                if (profile.Chute && !stationary)
                {
                    inAir = True;
                    capcol = npc.GetComponent<CapsuleCollider>();
                    if (capcol != null)
                    {
                        capcol.isTrigger = True;
                        npc.GetComponent<CapsuleCollider>().radius += 2f;
                    }
                    botSpawn.ToggleAggro(npc, 1, 300f);
                }
                if (stationary || inAir)
                {
                    npc.utilityAiComponent.enabled = True;
                    npc.Stats.VisionCone = -1f;
                }
                float delay = random.Next(300, 1200);
                if (respawn)
                    InvokeRepeating("Relocate", delay, delay);
                if (!noweapon)
                    InvokeRepeating("SelectWeapon", 0, 2.99f);
            }

            void SelectWeapon() => botSpawn.SelectWeapon(npc);
            public void OnDestroy()
            {
                botSpawn.NPCPlayers.Remove(npc.userID);
                if (botSpawn.weaponCheck.ContainsKey(npc.userID))
                {
                    botSpawn.weaponCheck[npc.userID].Destroy();
                    botSpawn.weaponCheck.Remove(npc.userID);
                }
                CancelInvoke("Relocate");
                CancelInvoke("SelectWeapon");
            }

            void Relocate()
            {
                if (!respawn || stationary || (profile.UseCustomSpawns == True && botSpawn.spawnsData.CustomSpawnLocations[monumentName].Count > 0))
                    return;
                if (biome)
                {
                    spawnPoint = botSpawn.spawnLists[monumentName][random.Next(botSpawn.spawnLists[monumentName].Count)];
                    return;
                }

                var randomTerrainPoint = botSpawn.TryGetSpawn(profile.Location, profile.Radius);
                if (randomTerrainPoint != new Vector3())
                    spawnPoint = randomTerrainPoint + new Vector3(0, 0.5f, 0);
            }

            private void OnCollisionEnter(Collision col)
            {
                if (!inAir)
                    return;

                var rb = npc.gameObject.GetComponent<Rigidbody>();
                if (landingAttempts == 0)
                    landingDirection = npc.transform.forward;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(npc.transform.position, out hit, 30, -1) || landingAttempts > 5) //NavMesh.AllAreas 
                {
                    if (npc.WaterFactor() > 0.9f)
                    {
                        npc.Kill();
                        return;
                    }
                    if (capcol != null)
                    {
                        capcol.isTrigger = False;
                        npc.GetComponent<CapsuleCollider>().radius -= 2f; 
                    }
                    rb.isKinematic = True;
                    rb.useGravity = False;
                    npc.gameObject.layer = 17;
                    npc.ServerPosition = hit.position;
                    npc.GetNavAgent.Warp(npc.ServerPosition);
                    botSpawn.ToggleAggro(npc, Convert.ToByte(!profile.Peace_Keeper), profile.Aggro_Range);

                    foreach (var child in npc.children.Where(child => child.name.Contains("parachute")))
                    {
                        child.SetParent(null);
                        child.Kill();
                        break;
                    }
                    SetSpawn(npc);
                    landingAttempts = 0;
                }
                else
                {
                    landingAttempts++;
                    rb.useGravity = True;
                    rb.velocity = new Vector3(landingDirection.x * 15, 11, landingDirection.z * 15);
                    rb.drag = 1f;
                }
            }
            bool done = False;
            void SetSpawn(NPCPlayerApex bot)
            {
                inAir = False;
                spawnPoint = bot.transform.position;
                bot.SpawnPosition = bot.transform.position;
                bot.Resume();
            }

            void Update()
            {
                updateCounter++;

                if (updateCounter == 50) 
                {
                    if (Time.time - npc.lastAttackedTime > 15)
                    {
                        botSpawn.ToggleAggro(npc, 0, profile.Aggro_Range);
                        botSpawn.WipeMemory(npc);
                    }

                    updateCounter = 0;
                    if (inAir || stationary) 
                    {
                        if (npc?.AttackTarget != null && npc.AttackTarget is BasePlayer)
                        {
                            if (npc.IsVisibleStanding(npc.AttackTarget.ToPlayer()) && Interface.CallHook("OnNpcTarget", npc, npc.AttackTarget) == null)
                            {
                                npc.SetAimDirection((npc.AttackTarget.transform.position - npc.GetPosition()).normalized);
                                npc.StartAttack();
                            }
                            else
                                npc.SetAimDirection(new Vector3(npc.transform.forward.x, 0, npc.transform.forward.z));
                        }
                        else
                            npc.SetAimDirection(new Vector3(npc.transform.forward.x, 0, npc.transform.forward.z));

                        goingHome = False;
                        return;
                    }

                    if (npc.GetFact(NPCPlayerApex.Facts.IsAggro) == 0 && npc.AttackTarget == null && npc.GetNavAgent.isOnNavMesh)
                    {
                        npc.CurrentBehaviour = BaseNpc.Behaviour.Wander;
                        npc.SetFact(NPCPlayerApex.Facts.Speed, (byte)NPCPlayerApex.SpeedEnum.Walk, True, True);
                        npc.TargetSpeed = 2.4f;
                        var distance = Vector3.Distance(npc.transform.position, spawnPoint);
                        if (!goingHome && distance > profile.Roam_Range || npc.WaterFactor() > 0.1f)
                            goingHome = True;
                        if (goingHome && distance > 5)
                        {
                            npc.GetNavAgent.SetDestination(spawnPoint);
                            npc.Destination = spawnPoint;
                        }
                        else
                            goingHome = False;
                    }
                }
            }
        }

        public void ToggleAggro(NPCPlayerApex npcPlayer, int hostility, float distance)
        {
            var bData = npcPlayer.GetComponent<BotData>();
            if (bData != null)
            {
                npcPlayer.Stats.VisionRange = distance + 20;
                npcPlayer.Stats.AggressionRange = distance;
                npcPlayer.Stats.DeaggroRange = npcPlayer.Stats.AggressionRange + 20;
                npcPlayer.Stats.Hostility = hostility;
            }
        }
        #endregion

        #region Config
        private ConfigData configData;

        public class Global
        {
            public int AddSpawn_Show_Seconds = 10, DayStartHour = 8, NightStartHour = 20;
            public bool PeaceKeepers_Ignore_Melee = False, NPCs_Attack_BotSpawn = True, HTNs_Attack_BotSpawn, BotSpawn_Attacks_NPCs = True, APC_Safe = True, Turret_Safe = True, Animal_Safe = True, Supply_Enabled, Staggered_Despawn = false;
            public int Remove_BackPacks_Percent = 100;
            public bool Remove_KeyCard = True, Ignore_HumanNPC = True, Ignore_HTN = True, Ignore_Sleepers = True, Pve_Safe = True;
            public int Max_Chute_Wind_Speed = 100, Max_Chute_Fall_Speed = 100;
        }

        class ConfigData
        {
            public string DataPrefix = "default";
            public Global Global = new Global();
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
            SaveConfig(configData);
        }
        protected override void LoadDefaultConfig()
        {
            LoadConfigVariables();
            Puts("Creating new config file.");
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, True);
        }
        #endregion

        #region Data  
        class StoredData
        {
            public Dictionary<string, DataProfile> DataProfiles = new Dictionary<string, DataProfile>();
            public Dictionary<string, ProfileRelocation> MigrationDataDoNotEdit = new Dictionary<string, ProfileRelocation>();
        }

        public class ProfileRelocation
        {
            public Vector3 ParentMonument = new Vector3();
            public Vector3 Offset = new Vector3();
        }

        class DefaultData
        {
            public Events Events = new Events();
            public Dictionary<string, ConfigProfile> Monuments = botSpawn.GotMonuments;
            public Dictionary<string, BiomeProfile> Biomes = new Dictionary<string, BiomeProfile>()
            {
                {"BiomeArid", new BiomeProfile() },
                {"BiomeTemperate", new BiomeProfile() },
                {"BiomeTundra", new BiomeProfile() },
                {"BiomeArctic", new BiomeProfile() },
            };
        }

        class UpdateData
        {
            public Events Events = new Events();
            public Dictionary<string, ConfigProfile> Monuments = new Dictionary<string, ConfigProfile>();
            public Dictionary<string, BiomeProfile> Biomes = new Dictionary<string, BiomeProfile>()
            {
                {"BiomeArid", new BiomeProfile() },
                {"BiomeTemperate", new BiomeProfile() },
                {"BiomeTundra", new BiomeProfile() },
                {"BiomeArctic", new BiomeProfile() },
            };
        }

        class SpawnsData
        {
            public Dictionary<string, List<string>> CustomSpawnLocations = new Dictionary<string, List<string>>();
        }

        StoredData storedData = new StoredData();
        DefaultData defaultData;
        SpawnsData spawnsData = new SpawnsData();

        void SaveSpawns() => Interface.Oxide.DataFileSystem.WriteObject($"BotSpawn/{configData.DataPrefix}-SpawnsData", spawnsData);
        void SaveData()
        {
            storedData.DataProfiles = storedData.DataProfiles.OrderBy(x => x.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
            defaultData.Monuments = defaultData.Monuments.OrderBy(x => x.Key).ToDictionary(pair => pair.Key, pair => pair.Value);

            Interface.Oxide.DataFileSystem.WriteObject($"BotSpawn/{configData.DataPrefix}-CustomProfiles", storedData);
            Interface.Oxide.DataFileSystem.WriteObject($"BotSpawn/{configData.DataPrefix}-DefaultProfiles", defaultData);
            Interface.Oxide.DataFileSystem.WriteObject($"BotSpawn/{configData.DataPrefix}-SpawnsData", spawnsData);
        }

        void ReloadData(string profile)
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>($"BotSpawn/{configData.DataPrefix}-CustomProfiles");
            defaultData = Interface.Oxide.DataFileSystem.ReadObject<DefaultData>($"BotSpawn/{configData.DataPrefix}-DefaultProfiles");

            if (storedData.DataProfiles.ContainsKey(profile))
            {
                AllProfiles.Remove(profile);
                AddData(profile, storedData.DataProfiles[profile]);
            }

            DataProfile prof = null;
            Vector3 loc = Vector3.zero;

            if (defaultData.Monuments.ContainsKey(profile))
                prof = JsonConvert.DeserializeObject<DataProfile>(JsonConvert.SerializeObject(defaultData.Monuments[profile]));
            if (defaultData.Biomes.ContainsKey(profile))
                prof = JsonConvert.DeserializeObject<DataProfile>(JsonConvert.SerializeObject(defaultData.Biomes[profile])); 

            if (prof != null)
            {
                loc = AllProfiles[profile].Location;
                AllProfiles[profile] = prof;
                AllProfiles[profile].Location = loc;
            }

            if (timers.ContainsKey(profile) || profile.Contains("AirDrop") || profile.Contains("HackableLockedCrate")) 
                return;

            prof = AllProfiles[profile];

            if (prof.Kit.Count > 0 && Kits == null)
                PrintWarning(lang.GetMessage("nokits", this), profile);

            int num = spawnsData.CustomSpawnLocations[profile].Count;

            if (availableStatSpawns.ContainsKey(profile))
            {
                availableStatSpawns.Remove(profile);
                SetupSpawnsFile();
            }

            if (prof.AutoSpawn == True && (prof.Day_Time_Spawn_Amount > 0 || prof.Night_Time_Spawn_Amount > 0))
            {
                for (int i = 0; i < num; i++)
                    availableStatSpawns[profile].Add(i);
            }

            popinfo[profile] = new PopInfo();
            CheckSpawnPop(profile, prof);
        }

        Vector3 s2v(string input)
        {
            String[] p = input.Split(',');
            return new Vector3(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2]));
        }

        float s2r(string input)=>float.Parse(input.Split(',')[3]);

        public class Events
        {
            public AirDropProfile AirDrop = new AirDropProfile { };
            public AirDropProfile HackableLockedCrate = new AirDropProfile { };
        }

        public class BaseProfile
        {
            [JsonProperty(Order = 1)] public bool AutoSpawn;
            [JsonProperty(Order = 2)] public bool Murderer;
            [JsonProperty(Order = 3)] public List<string> BotNames = new List<string>();
            [JsonProperty(Order = 4)] public string BotNamePrefix = String.Empty;
            [JsonProperty(Order = 5)] public bool Keep_Default_Loadout;
            [JsonProperty(Order = 6)] public List<string> Kit = new List<string>();
            [JsonProperty(Order = 7)] public int Day_Time_Spawn_Amount = 5;
            [JsonProperty(Order = 8)] public int Night_Time_Spawn_Amount = 0;
            [JsonProperty(Order = 10)] public int Roam_Range = 40;
            [JsonProperty(Order = 11)] public bool Chute;
            [JsonProperty(Order = 15)] public bool Announce_Spawn;
            [JsonProperty(Order = 16)] public string Announcement_Text = String.Empty;
            [JsonProperty(Order = 17)] public int BotHealth = 100;
            [JsonProperty(Order = 18)] public int Bot_Accuracy_Percent = 40;
            [JsonProperty(Order = 19)] public int Bot_Damage_Percent = 40;
            [JsonProperty(Order = 20)] public int Aggro_Range = 30;
            [JsonProperty(Order = 21)] public int DeAggro_Range = 40;
            [JsonProperty(Order = 22)] public bool Peace_Keeper = true;
            [JsonProperty(Order = 23)] public int Peace_Keeper_Cool_Down = 5;
            [JsonProperty(Order = 24)] public bool Attacks_Other_Profiles = false;
            [JsonProperty(Order = 25)] public int Suicide_Timer = 300;
            [JsonProperty(Order = 26)] public bool Die_Instantly_From_Headshot = false;
            [JsonProperty(Order = 27)] public List<string> Instant_Death_From_Headshot_Allowed_Weapons = new List<string>();
            [JsonProperty(Order = 28)] public int Weapon_Drop_Percent;
            [JsonProperty(Order = 29)] public int Min_Weapon_Drop_Condition_Percent = 50;
            [JsonProperty(Order = 30)] public int Max_Weapon_Drop_Condition_Percent = 100;
            [JsonProperty(Order = 31)] public int Wipe_Belt_Percent = 100;
            [JsonProperty(Order = 32)] public int Wipe_Clothing_Percent = 100;
            [JsonProperty(Order = 33)] public int Allow_Rust_Loot_Percent = 100;
            [JsonProperty(Order = 34)] public int Spawn_Hackable_Death_Crate_Percent;
            [JsonProperty(Order = 35)] public string Death_Crate_CustomLoot_Profile = "";
            [JsonProperty(Order = 36)] public int Death_Crate_LockDuration = 600;
            [JsonProperty(Order = 37)] public bool Disable_Radio = true;
            [JsonProperty(Order = 38)] public float Running_Speed_Boost;
            [JsonProperty(Order = 39)] public bool AlwaysUseLights;
            [JsonProperty(Order = 40)] public int Corpse_Duration = 60;
            [JsonProperty(Order = 41)] public int ServerRewardsValue = 0;
        }

        public class BiomeProfile : BaseProfile
        {
            [JsonProperty(Order = 41)] public int Respawn_Timer = 60;
        }

        public class AirDropProfile : BaseProfile
        {
            [JsonProperty(Order = 9)] public int Radius = 100;
        }

        public class ConfigProfile : AirDropProfile
        {
            [JsonProperty(Order = 41)] public int Respawn_Timer = 60;
            [JsonProperty(Order = 12)] public bool Stationary;
            [JsonProperty(Order = 13)] public bool UseCustomSpawns;
            [JsonProperty(Order = 14)] public bool ChangeCustomSpawnOnDeath;
        }

        public class DataProfile : AirDropProfile
        {
            public DataProfile Clone()
            {
                return MemberwiseClone() as DataProfile;
            }
            [JsonProperty(Order = 3)] public int Respawn_Timer = 60;
            [JsonProperty(Order = 12)] public bool Stationary;
            [JsonProperty(Order = 13)] public bool UseCustomSpawns;
            [JsonProperty(Order = 14)] public bool ChangeCustomSpawnOnDeath;
            [JsonProperty(Order = 41)] public Vector3 Location;
            [JsonProperty(Order = 42)] public string Parent_Monument = String.Empty;
        }
        #endregion

        #region Messages     
        readonly Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"Title", "BotSpawn : " },
            {"error", "\n<color=orange>Profile commands are :</color>\nlist\nshow <duration>\nadd ProfileName\nremove ProfileName\nmove ProfileName\ntoplayer ProfileName \nspawn\nkill\n<color=orange>Spawns commands are :</color>\nedit ProfileName\naddspawn\nremovespawn\nremovespawn Number\nmovespawn Number" },
            {"customsaved", "Custom Location Saved @ {0}" },
            {"custommoved", "Custom Location {0} has been moved to your current position." },

            {"nonpc", "No BotSpawn npc found directly in front of you." },
            {"noNavHere", "No navmesh was found at this location.\nConsider removing this point or using Stationary : true." },
            {"editingname", "Editing spawnpoints for {0}." },
            {"addedspawn", "Spawnpoint {0} added to {1}." },
            {"removedspawn", "Removed last spawn point from {0}. {1} points remaining." },
            {"savedspawn", "Spawnpoints saved for {0}." },
            {"notediting", "You are not editing a profile. '/botspawn edit profilename'" },
            {"imported", "Spawn points imported from {0} to {1}." },
            {"nospawns", "No custom spawn points were found for profile - {0}." },
            {"targetzero", "Target amount for time of day is zero at - {0}." },
            {"notenoughspawns", "There are not enough spawn points for population at profile - {0}. Reducing population" },
            {"removednum", "Removed point {0} from {1}." },
            {"movedspawn", "Moved point {0} in {1}." },
            {"notthatmany", "Number of spawn points in {0} is less than {1}." },
            {"alreadyexists", "Custom Location already exists with the name {0}." },
            {"customremoved", "Custom Location {0} Removed." },
            {"norespawn", "Please choose a respawning profile with set location, or associated spawns file." },
            {"deployed", "'{0}' bots deployed to {1}." },
            {"ListTitle", "Custom Locations" },
            {"killed", "Non-respawning npcs of profile {0} have been destroyed." },
            {"reloaded", "Profile was reloaded from data." },
            {"noprofile", "There is no profile by that name in default or custom profiles jsons." },
            {"showduration", "Correct formate is /botspawn show <duration>" },
            {"nonpcs", "No npcs were found belonging to a profile of that name" },
            {"namenotfound", "Player '{0}' was not found" },
            {"nokits", "Kits is not installed but you have declared custom kits at {0}." },
            {"noWeapon", "A bot at {0} has no weapon. Check your kits." },
            {"numberOfBot", "There is {0} spawned bot alive." },
            {"numberOfBots", "There are {0} spawned bots alive." },
            {"dupID", "Duplicate userID save attempted. Please notify author." },
            {"noSpawn", "Failed to find spawnpoints at {0}." },
            {"noNav", "Spawn point {1} in Spawns file {0} is too far away from navmesh." }
        };
        #endregion

        #region ExternalHooks
        private string NpcGroup(NPCPlayer npc)
        {
            if (NPCPlayers.ContainsKey(npc.userID))
                return npc.GetComponent<BotData>().group;
            return "No Group";
        }

        private string NPCProfile(NPCPlayer npc)
        {
            if (NPCPlayers.ContainsKey(npc.userID))
                return npc.GetComponent<BotData>().monumentName;
            return "No Name";
        }

        private Dictionary<string, List<ulong>> BotSpawnBots()
        {
            var BotSpawnBots = new Dictionary<string, List<ulong>>();

            foreach (var entry in AllProfiles)
                BotSpawnBots.Add(entry.Key, new List<ulong>());

            foreach (var bot in NPCPlayers)
            {
                var bData = bot.Value.GetComponent<BotData>();
                if (BotSpawnBots.ContainsKey(bData.monumentName))
                    BotSpawnBots[bData.monumentName].Add(bot.Key);
                else
                    BotSpawnBots.Add(bData.monumentName, new List<ulong> { bot.Key });
            }
            return BotSpawnBots;
        }

        private string[] AddGroupSpawn(Vector3 location, string profileName, string group)
        {
            if (location == new Vector3() || profileName == null || group == null)
                return new string[] { "error", "Null parameter" };
            string lowerProfile = profileName.ToLower();

            foreach (var entry in AllProfiles)
            {
                if (entry.Key.ToLower() == lowerProfile)
                {
                    var profile = entry.Value;
                    if (TargetAmount(AllProfiles[entry.Key]) == 0)
                        return new string[] { "false", "Target spawn amount for time of day is zero.}" };
                    timer.Repeat(1f, TargetAmount(AllProfiles[entry.Key]), () => DeployNpcs(location, entry.Key, profile, group.ToLower(), -1));
                    return new string[] { "true", "Group successfully added" };
                }
            }
            return new string[] { "false", "Group add failed - Check profile name and try again" };
        }

        private string[] RemoveGroupSpawn(string group)
        {
            if (group == null)
                return new string[] { "error", "No group specified." };

            List<NPCPlayerApex> toDestroy = new List<NPCPlayerApex>();
            bool flag = False;
            foreach (var bot in NPCPlayers.ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                if (bot.Value == null)
                    continue;
                var bData = bot.Value.GetComponent<BotData>();
                if (bData.group == group.ToLower())
                {
                    flag = True;
                    NPCPlayers[bot.Key].Kill();
                }
            }
            return flag ? new string[] { "true", $"Group {group} was destroyed." } : new string[] { "true", $"There are no bots belonging to {group}" };
        }

        private string[] CreateNewProfile(string name, string profile)
        {
            if (name == null)
                return new string[] { "error", "No name specified." };
            if (profile == null)
                return new string[] { "error", "No profile settings specified." };
            if (storedData == null)
                return new string[] { "BotSpawn was not yet initialised. Please reload the plugin which is attempting to add profiles."};

            DataProfile newProfile = JsonConvert.DeserializeObject<DataProfile>(profile);

            if (storedData.DataProfiles.ContainsKey(name))
            {
                storedData.DataProfiles[name] = newProfile;
                AllProfiles[name] = newProfile;
                foreach (var npc in NPCPlayers.ToList())
                {
                    var bData = npc.Value.GetComponent<BotData>();
                    if (bData.monumentName == name)
                    {
                        bData.profile = AllProfiles[name].Clone();
                        bData.profile.Respawn_Timer = 0;
                        npc.Value.Kill();
                    }
                }
                return new string[] { "true", $"Profile {name} was updated" };   
            }

            storedData.DataProfiles.Add(name, newProfile);
            SaveData();
            AllProfiles.Add(name, newProfile);   
            popinfo.Add(name, new PopInfo());
            return new string[] { "true", $"New Profile {name} was created." };
        }

        private string[] ProfileExists(string name)
        {
            if (name == null)
                return new string[] { "error", "No name specified." };

            if (AllProfiles.ContainsKey(name))
                return new string[] { "true", $"{name} exists." };

            return new string[] { "false", $"{name} Does not exist." };
        }

        private string[] RemoveProfile(string name)
        {
            if (name == null)
                return new string[] { "error", "No name specified." };

            if (storedData.DataProfiles.ContainsKey(name))
            {
                foreach (var bot in NPCPlayers.ToDictionary(pair => pair.Key, pair => pair.Value))
                {
                    if (bot.Value == null)
                        continue;
                    var bData = bot.Value.GetComponent<BotData>();
                    if (bData.monumentName == name)
                        NPCPlayers[bot.Key].Kill();
                }
                AllProfiles.Remove(name);
                storedData.DataProfiles.Remove(name);
                SaveData();
                return new string[] { "true", $"Profile {name} was removed." };
            }
            else
                return new string[] { "false", $"Profile {name} Does Not Exist." };
        }
        #endregion
    }
}
