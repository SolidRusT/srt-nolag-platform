using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using Rust.Ai.HTN.Murderer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Rust;
using System.Reflection;
using Rust.Ai;

namespace Oxide.Plugins
{
    [Info("ZombieHorde", "k1lly0u", "0.3.4")]
    class ZombieHorde : RustPlugin
    {
        [PluginReference] 
        private Plugin Kits, Spawns;

        private static ZombieHorde Instance { get; set; } 

        #region Oxide Hooks       
        private void OnServerInitialized()
        {
            Instance = this;

            HordeThinkManager.Create();

            permission.RegisterPermission("zombiehorde.admin", this);
            permission.RegisterPermission("zombiehorde.ignore", this);

            _blueprintBase = ItemManager.FindItemDefinition("blueprintbase");
            _glowEyes = ItemManager.FindItemDefinition("gloweyes");

            if (!configData.Member.TargetedByPeaceKeeperTurrets)
                Unsubscribe(nameof(CanEntityBeHostile));

            ValidateLoadoutProfiles();

            ValidateSpawnSystem();

            CreateMonumentHordeOrders();
        }
                
        private void OnEntityTakeDamage(BaseCombatEntity baseCombatEntity, HitInfo hitInfo)
        {
            if (hitInfo != null)
            {
                HordeMember hordeMember;

                if (hitInfo.InitiatorPlayer != null)
                {
                    hordeMember = hitInfo.InitiatorPlayer.GetComponent<HordeMember>();
                    if (hordeMember != null)
                    {
                        if (hitInfo.damageTypes.Get(DamageType.Explosion) > 0)
                        {
                            hitInfo.damageTypes.ScaleAll(ConVar.Halloween.scarecrow_beancan_vs_player_dmg_modifier);
                            return;
                        }

                        if (hordeMember.DamageScale != 1f)
                            hitInfo.damageTypes.ScaleAll(hordeMember.DamageScale);
                        
                        return;
                    }
                }

                hordeMember = baseCombatEntity.GetComponent<HordeMember>();
                if (hordeMember != null)
                {
                    if (configData.Member.HeadshotKills && hitInfo.isHeadshot)
                        hitInfo.damageTypes.ScaleAll(1000);
                }
            }
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            if (player == null || hitInfo == null)
                return;

            HordeMember hordeMember = player.GetComponent<HordeMember>();
            if (hordeMember != null)
            {
                for (int i = 0; i < player.inventory.containerWear.itemList.Count; i++)
                {
                    Item item = player.inventory.containerWear.itemList[i];
                    if (item != null && item.info == _glowEyes)
                    {
                        player.inventory.containerWear.Remove(item);
                        break;
                    }                   
                }
               
                if (configData.Loot.DropInventory)
                    hordeMember.PrepareInventory();

                hordeMember.Manager.OnMemberDeath(hordeMember, hitInfo.Initiator as BaseCombatEntity);
                return;
            }

            if (configData.Horde.CreateOnDeath && hitInfo.InitiatorPlayer != null)
            {
                HordeMember attacker = hitInfo.InitiatorPlayer.GetComponent<HordeMember>();

                if (attacker != null && attacker.Manager != null)                
                    attacker.Manager.OnPlayerDeath(player, attacker);
            }
        }

        private void OnEntityKill(ScientistNPC scientistNPC)
        {
            HordeMember hordeMember = scientistNPC.GetComponent<HordeMember>();
            if (hordeMember != null && hordeMember.Manager != null)            
                hordeMember.Manager.OnMemberDeath(hordeMember, null);            
        }

        private object CanBeTargeted(ScientistNPC scientistNPC, MonoBehaviour behaviour)
        {
            HordeMember hordeMember = scientistNPC.GetComponent<HordeMember>();
            if (hordeMember != null)
            {
                if (((behaviour is AutoTurret) || (behaviour is GunTrap) || (behaviour is FlameTurret)) && configData.Member.TargetedByTurrets)
                    return null;
                return false;
            }

            return null;
        }

        private object CanEntityBeHostile(ScientistNPC scientistNPC) => scientistNPC != null && scientistNPC.GetComponent<HordeMember>() != null ? (object)true : null;
        
        private object CanBradleyApcTarget(BradleyAPC bradleyAPC, ScientistNPC scientistNPC)
        {
            if (scientistNPC != null)
            {
                HordeMember hordeMember = scientistNPC.GetComponent<HordeMember>();
                if (hordeMember != null && !configData.Member.TargetedByAPC)
                    return false;
            }
            return null;
        }

        private object OnCorpsePopulate(ScientistNPC scientistNPC, NPCPlayerCorpse npcPlayerCorpse)
        {
            if (scientistNPC != null && npcPlayerCorpse != null)
            {
                HordeMember hordeMember = scientistNPC.GetComponent<HordeMember>();
                if (hordeMember == null)
                    return null;

                npcPlayerCorpse.playerName = scientistNPC.displayName;

                if (configData.Loot.DropInventory)
                {
                    hordeMember.MoveInventoryTo(npcPlayerCorpse);
                    return npcPlayerCorpse;
                }

                SpawnIntoContainer(npcPlayerCorpse);
                return npcPlayerCorpse;
            }
            return null;
        }

        private object CanPopulateLoot(ScientistNPC scientistNPC, NPCPlayerCorpse corpse) => scientistNPC != null && scientistNPC.GetComponent<HordeMember>() != null ? (object)false : null;

        private void Unload()
        {
            HordeManager.Order.OnUnload();

            _hordeThinkManager.Destroy();

            for (int i = HordeManager._allHordes.Count - 1; i >= 0; i--)
                HordeManager._allHordes[i].Destroy(true, true);

            HordeManager._allHordes.Clear();

            _spawnState = SpawnState.Spawn;

            configData = null;
            Instance = null;
        }
        #endregion

        #region Sensations
        private void OnEntityKill(TimedExplosive timedExplosive)
        {
            if (!configData.Horde.UseSenses)
                return;

            HordeManager.Stimulate(new Sensation()
            {
                Type = SensationType.Explosion,
                Position = timedExplosive.transform.position,
                Radius = timedExplosive.explosionRadius * 17f,
            });
        }

        private void OnEntityKill(Landmine landmine)
        {
            if (!configData.Horde.UseSenses)
                return;

            HordeManager.Stimulate(new Sensation()
            {
                Type = SensationType.Explosion,
                Position = landmine.transform.position,
                Radius = landmine.explosionRadius * 17f,
            });
        }

        private void OnEntityKill(TreeEntity treeEntity)
        {
            if (!configData.Horde.UseSenses)
                return;

            HordeManager.Stimulate(new Sensation()
            {
                Type = SensationType.Gunshot,
                Position = treeEntity.transform.position,
                Radius = 30f,
            });
        }

        private void OnEntityKill(OreResourceEntity oreResourceEntity)
        {
            if (!configData.Horde.UseSenses)
                return;

            HordeManager.Stimulate(new Sensation()
            {
                Type = SensationType.Gunshot,
                Position = oreResourceEntity.transform.position,
                Radius = 30f,
            });
        }

        private void OnWeaponFired(BaseProjectile baseProjectile, BasePlayer player, ItemModProjectile itemModProjectile, ProtoBuf.ProjectileShoot projectileShoot)
        {
            if (!configData.Horde.UseSenses)
                return;

            HordeManager.Stimulate(new Sensation()
            {
                Type = SensationType.Gunshot,
                Position = player.transform.position,
                Radius = baseProjectile.NoiseRadius,
                Initiator = player
            });
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (!configData.Horde.UseSenses)
                return;

            HordeManager.Stimulate(new Sensation()
            {
                Type = SensationType.Gunshot,
                Position = dispenser.transform.position,
                Radius = 10f
            });
        }
        #endregion

        #region Functions
        #region Horde Spawning
        private List<Vector3> _spawnPoints;

        private SpawnSystem _spawnSystem = SpawnSystem.None; 

        private bool ValidateSpawnSystem()
        {
            _spawnSystem = ParseType<SpawnSystem>(configData.Horde.SpawnType);

            if (_spawnSystem == SpawnSystem.None)
            {
                PrintError("You have set an invalid value in the config entry \"Spawn Type\". Unable to spawn hordes!");
                return false;
            }
            else if (_spawnSystem == SpawnSystem.SpawnsDatabase)
            {
                if (Spawns != null)
                {
                    if (string.IsNullOrEmpty(configData.Horde.SpawnFile))
                    {
                        PrintError("You have selected SpawnsDatabase as your method of spawning hordes, however you have not specified a spawn file. Unable to spawn hordes!");
                        return false;
                    }

                    object success = Spawns?.Call("LoadSpawnFile", configData.Horde.SpawnFile);
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

        private const int SPAWN_RAYCAST_MASK = 1 << 0 | 1 << 8 | 1 << 15 | 1 << 17 | 1 << 21 | 1 << 29;

        private const TerrainTopology.Enum SPAWN_TOPOLOGY_MASK = (TerrainTopology.Enum.Ocean | TerrainTopology.Enum.River | TerrainTopology.Enum.Lake | TerrainTopology.Enum.Cliff | TerrainTopology.Enum.Cliffside | TerrainTopology.Enum.Offshore | TerrainTopology.Enum.Summit);

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
                        _spawnPoints = (List<Vector3>)Spawns.Call("LoadSpawnFile", configData.Horde.SpawnFile);

                    return spawnPoint;
                }
            }
            
            float size = (World.Size / 2f) * 0.75f;
            NavMeshHit navMeshHit;

            for (int i = 0; i < 10; i++)
            {
                Vector2 randomInCircle = UnityEngine.Random.insideUnitCircle * size;

                Vector3 position = new Vector3(randomInCircle.x, 0, randomInCircle.y);
                position.y = TerrainMeta.HeightMap.GetHeight(position);

                if (NavMesh.SamplePosition(position, out navMeshHit, 25f, 1))
                {                    
                    position = navMeshHit.position;

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
                if (count >= configData.Horde.MaximumHordes)
                    break;

                if (gobject.name.Contains("autospawn/monument"))
                {
                    Transform tr = gobject.transform;
                    Vector3 position = tr.position;

                    if (position == Vector3.zero)
                        continue;

                    if (gobject.name.Contains("powerplant_1") && configData.Monument.Powerplant.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(-30.8f, 0.2f, -15.8f)), configData.Monument.Powerplant);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("military_tunnel_1") && configData.Monument.Tunnels.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(-7.4f, 13.4f, 53.8f)), configData.Monument.Tunnels);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("harbor_1") && configData.Monument.LargeHarbor.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(54.7f, 5.1f, -39.6f)), configData.Monument.LargeHarbor);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("harbor_2") && configData.Monument.SmallHarbor.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(-66.6f, 4.9f, 16.2f)), configData.Monument.SmallHarbor);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("airfield_1") && configData.Monument.Airfield.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(-12.4f, 0.2f, -28.9f)), configData.Monument.Airfield);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("trainyard_1") && configData.Monument.Trainyard.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(35.8f, 0.2f, -0.8f)), configData.Monument.Trainyard);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("water_treatment_plant_1") && configData.Monument.WaterTreatment.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(11.1f, 0.3f, -80.2f)), configData.Monument.WaterTreatment);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("warehouse") && configData.Monument.Warehouse.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(16.6f, 0.1f, -7.5f)), configData.Monument.Warehouse);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("satellite_dish") && configData.Monument.Satellite.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(18.6f, 6.0f, -7.5f)), configData.Monument.Satellite);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("sphere_tank") && configData.Monument.Dome.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(-44.6f, 5.8f, -3.0f)), configData.Monument.Dome);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("radtown_small_3") && configData.Monument.Radtown.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(-16.3f, -2.1f, -3.3f)), configData.Monument.Radtown);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("launch_site_1") && configData.Monument.LaunchSite.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(222.1f, 3.3f, 0.0f)), configData.Monument.LaunchSite);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("gas_station_1") && configData.Monument.GasStation.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(-9.8f, 3.0f, 7.2f)), configData.Monument.GasStation);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("supermarket_1") && configData.Monument.Supermarket.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(5.5f, 0.0f, -20.5f)), configData.Monument.Supermarket);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_c") && configData.Monument.HQMQuarry.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(15.8f, 4.5f, -1.5f)), configData.Monument.HQMQuarry);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_a") && configData.Monument.SulfurQuarry.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(-0.8f, 0.6f, 11.4f)), configData.Monument.SulfurQuarry);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_b") && configData.Monument.StoneQuarry.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(-7.6f, 0.2f, 12.3f)), configData.Monument.StoneQuarry);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("junkyard_1") && configData.Monument.Junkyard.Enabled)
                    {
                        HordeManager.Order.CreateOrder(tr.TransformPoint(new Vector3(-16.7f, 0.2f, 1.4f)), configData.Monument.Junkyard);
                        count++;
                        continue;
                    }
                }
            }

            if (count < configData.Horde.MaximumHordes)
                CreateRandomHordes();
        }

        private void CreateRandomHordes()
        {
            int amountToCreate = configData.Horde.MaximumHordes - HordeManager._allHordes.Count;
            for (int i = 0; i < amountToCreate; i++)
            {
                float roamDistance = configData.Horde.LocalRoam ? configData.Horde.RoamDistance : -1;
                string profile = configData.Horde.UseProfiles ? configData.HordeProfiles.Keys.ToArray().GetRandom() : string.Empty;

                HordeManager.Order.CreateOrder(GetSpawnPoint(), configData.Horde.InitialMemberCount, configData.Horde.MaximumMemberCount, roamDistance, profile);
            }
        }
        #endregion

        #region Inventory and Loot
        private ItemDefinition _blueprintBase;

        private ItemDefinition _glowEyes;

        private static MurdererDefinition _defaultDefinition;
        private static MurdererDefinition DefaultDefinition
        {
            get
            {
                if (_defaultDefinition == null)
                    _defaultDefinition = GameManager.server.FindPrefab("assets/prefabs/npc/scarecrow/scarecrow.prefab").GetComponent<HTNPlayer>().AiDefinition as MurdererDefinition;
                return _defaultDefinition;
            }
        }

        private void ValidateLoadoutProfiles()
        {
            Puts("Validating horde profiles...");

            bool hasChanged = false;

            for (int i = configData.HordeProfiles.Count - 1; i >= 0; i--)
            {
                string key = configData.HordeProfiles.ElementAt(i).Key;

                for (int y = configData.HordeProfiles[key].Count - 1; y >= 0; y--)
                {
                    string loadoutId = configData.HordeProfiles[key][y];

                    if (!configData.Member.Loadouts.Any(x => x.LoadoutID == loadoutId))
                    {
                        Puts($"Loadout profile {loadoutId} does not exist. Removing from config");
                        configData.HordeProfiles[key].Remove(loadoutId);
                        hasChanged = true;
                    }
                }

                if (configData.HordeProfiles[key].Count <= 0)
                {
                    Puts($"Horde profile {key} does not have any valid loadouts. Removing from config");
                    configData.HordeProfiles.Remove(key);
                    hasChanged = true;
                }
            }

            foreach (ConfigData.MemberOptions.Loadout loadout in configData.Member.Loadouts)
            {
                if (loadout.Vitals == null)
                {
                    loadout.Vitals = new ConfigData.MemberOptions.Loadout.VitalStats() { Health = DefaultDefinition.Vitals.HP };
                    hasChanged = true;
                }

                if (loadout.Movement == null)
                {
                    loadout.Movement = new ConfigData.MemberOptions.Loadout.MovementStats()
                    {
                        Acceleration = DefaultDefinition.Movement.Acceleration,
                        DuckSpeed = DefaultDefinition.Movement.DuckSpeed,
                        RunSpeed = DefaultDefinition.Movement.RunSpeed,
                        WalkSpeed = DefaultDefinition.Movement.WalkSpeed
                    };
                    hasChanged = true;
                }

                if (loadout.Sensory == null)
                {
                    loadout.Sensory = new ConfigData.MemberOptions.Loadout.SensoryStats()
                    {
                        VisionRange = DefaultDefinition.Sensory.VisionRange
                    };
                    hasChanged = true;
                }
            }

            if (hasChanged)
                SaveConfig();
        }

        private void SpawnIntoContainer(LootableCorpse lootableCorpse)
        {
            int count = UnityEngine.Random.Range(configData.Loot.Random.Minimum, configData.Loot.Random.Maximum);

            int spawnedCount = 0;
            int loopCount = 0;

            while (true)
            {
                loopCount++;

                if (loopCount > 3)
                    return;

                float probability = UnityEngine.Random.Range(0f, 1f);

                List<ConfigData.LootTable.RandomLoot.LootDefinition> definitions = new List<ConfigData.LootTable.RandomLoot.LootDefinition>(configData.Loot.Random.List);

                for (int i = 0; i < configData.Loot.Random.List.Count; i++)
                {
                    ConfigData.LootTable.RandomLoot.LootDefinition lootDefinition = definitions.GetRandom();

                    definitions.Remove(lootDefinition);

                    if (lootDefinition.Probability >= probability)
                    {
                        CreateItem(lootDefinition, lootableCorpse.containers[0]);

                        spawnedCount++;

                        if (spawnedCount >= count)
                            return;
                    }
                }
            }
        }

        private void CreateItem(ConfigData.LootTable.RandomLoot.LootDefinition lootDefinition, ItemContainer container)
        {
            Item item;

            if (!lootDefinition.IsBlueprint)
                item = ItemManager.CreateByName(lootDefinition.Shortname, lootDefinition.GetAmount(), lootDefinition.SkinID);
            else
            {
                item = ItemManager.Create(_blueprintBase);
                item.blueprintTarget = ItemManager.FindItemDefinition(lootDefinition.Shortname).itemid;
            }

            if (item != null)
            {
                item.OnVirginSpawn();
                if (!item.MoveToContainer(container, -1, true))
                    item.Remove(0f);
            }

            if (lootDefinition.Required != null)
                CreateItem(lootDefinition.Required, container);
        }

        private static void StripInventory(BasePlayer player, bool skipWear = false)
        {
            List<Item> list = Pool.GetList<Item>();

            player.inventory.AllItemsNoAlloc(ref list);

            for (int i = list.Count - 1; i >= 0; i--)
            {
                Item item = list[i];

                if (skipWear && item?.parent == player.inventory.containerWear)
                    continue;

                item.RemoveFromContainer();
                item.Remove();
            }

            Pool.FreeList(ref list);
        }

        private static void ClearContainer(ItemContainer container, bool skipWear = false)
        {
            if (container == null || container.itemList == null)
                return;

            while (container.itemList.Count > 0)
            {
                Item item = container.itemList[0];
                item.RemoveFromContainer();
                item.Remove(0f);
            }
        }        
        #endregion

        #region Spawning
        private static ScientistNPC InstantiateEntity(Vector3 position)
        {
            const string SCIENTIST_PREFAB = "assets/rust.ai/agents/npcplayer/humannpc/heavyscientist/heavyscientist.prefab";

            GameObject gameObject = Instantiate.GameObject(GameManager.server.FindPrefab(SCIENTIST_PREFAB), position, Quaternion.identity);
            gameObject.name = SCIENTIST_PREFAB;

            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);

            UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            ScientistNPC component = gameObject.GetComponent<ScientistNPC>();
            return component;
        }

        private static NavMeshHit navmeshHit;

        private static RaycastHit raycastHit;

        private static Collider[] _buffer = new Collider[256];

        private const int WORLD_LAYER = 65536;

        private static object FindPointOnNavmesh(Vector3 targetPosition, float maxDistance = 4f)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector3 position = i == 0 ? targetPosition : targetPosition + (UnityEngine.Random.onUnitSphere * maxDistance);
                if (NavMesh.SamplePosition(position, out navmeshHit, maxDistance, 1))
                {
                    if (IsInRockPrefab(navmeshHit.position))                    
                        continue;                    

                    if (IsNearWorldCollider(navmeshHit.position))                    
                        continue;  

                    return navmeshHit.position;
                }
            }
            return null;
        } 

        private static bool IsInRockPrefab(Vector3 position)
        {
            Physics.queriesHitBackfaces = true;

            bool isInRock = Physics.Raycast(position, Vector3.up, out raycastHit, 20f, WORLD_LAYER, QueryTriggerInteraction.Ignore) && 
                            blockedColliders.Any(s => raycastHit.collider?.gameObject?.name.Contains(s) ?? false);

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
                if (acceptedColliders.Any(s => _buffer[i].gameObject.name.Contains(s)))
                  removed++;
            }


            return count - removed > 0;
        }

        private static readonly string[] acceptedColliders = new string[] { "road", "carpark", "rocket_factory", "range", "train_track", "runway", "_grounds", "concrete_slabs", "lighthouse", "cave", "office", "walkways", "sphere", "tunnel", "industrial", "junkyard" };

        private static readonly string[] blockedColliders = new string[] { "rock", "junk", "range", "invisible" };
        #endregion

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

        private static bool ContainsTopologyAtPoint(TerrainTopology.Enum mask, Vector3 position) => (TerrainMeta.TopologyMap.GetTopology(position, 1f) & (int)mask) != 0;
        #endregion

        #region Think Manager
        private HordeThinkManager _hordeThinkManager;

        private static SpawnState _spawnState = SpawnState.Spawn;

        private class HordeThinkManager : MonoBehaviour
        {
            internal static void Create() => Instance._hordeThinkManager = new GameObject("ZombieHorde-ThinkManager").AddComponent<HordeThinkManager>();

            private void Awake()
            {
                _spawnState = configData.TimedSpawns.Enabled ? (ShouldSpawn() ? SpawnState.Spawn : SpawnState.Despawn) : SpawnState.Spawn;

                if (configData.TimedSpawns.Enabled)
                    InvokeHandler.InvokeRepeating(this, CheckTimeTick, 0f, 1f);
            }

            internal void Update()
            {
                HordeManager._hordeTickQueue.RunQueue(0.5);
            }

            internal void Destroy()
            {
                if (configData.TimedSpawns.Enabled)
                    InvokeHandler.CancelInvoke(this, CheckTimeTick);

                Destroy(gameObject);
            }

            private bool ShouldSpawn()
            {
                float currentTime = TOD_Sky.Instance.Cycle.Hour;

                if (configData.TimedSpawns.Start > configData.TimedSpawns.End)
                    return currentTime > configData.TimedSpawns.Start || currentTime < configData.TimedSpawns.End;
                else return currentTime > configData.TimedSpawns.Start && currentTime < configData.TimedSpawns.End;
            }
                         
            private void CheckTimeTick()
            {
                if (ShouldSpawn())
                {
                    if (_spawnState == SpawnState.Despawn)
                    {
                        _spawnState = SpawnState.Spawn;
                        HordeManager.Order.StopDespawning();
                        HordeManager.Order.BeginSpawning();
                    }
                }
                else
                {
                    if (_spawnState == SpawnState.Spawn)
                    {
                        _spawnState = SpawnState.Despawn;

                        if (configData.TimedSpawns.Despawn)
                        {
                            HordeManager.Order.StopSpawning();
                            HordeManager.Order.BeginDespawning();
                        }
                    }
                }
            }
        }
                
        internal class HordeTickQueue : ObjectWorkQueue<HordeManager>
        {
            protected override void RunJob(HordeManager hordeManager)
            {
                if (!ShouldAdd(hordeManager))
                    return;

                hordeManager.HordeTick();
                
                Instance.timer.In(3f, ()=> HordeManager._hordeTickQueue.Add(hordeManager));
            }

            protected override bool ShouldAdd(HordeManager hordeManager)
            {
                if (!base.ShouldAdd(hordeManager))
                    return false;

                return hordeManager != null && !hordeManager.isDestroyed && hordeManager.members?.Count > 0;
            }
        }
        #endregion

        #region Horde Manager
        internal class HordeManager
        {
            internal static List<HordeManager> _allHordes = new List<HordeManager>();

            internal static HordeTickQueue _hordeTickQueue = new HordeTickQueue();

            internal List<HordeMember> members;

            private Vector3 destination;

            private Vector3 interestPoint;
            
            internal Vector3 AverageLocation { get; private set; }

            internal BaseCombatEntity PrimaryTarget;


            private bool isRegrouping = false;

            internal bool isDestroyed = false;


            private Vector3 initialSpawnPosition;

            private int initialMemberCount;

            private bool isLocalHorde = false;

            private float maximumRoamDistance;

            internal string hordeProfile;


            private float nextGrowthTime = Time.time + configData.Horde.GrowthRate;

            private int maximumMemberCount;

            private float nextMergeTime = Time.time + MERGE_COOLDOWN;

            private float refreshRoamTime;


            private const float MERGE_COOLDOWN = 180f;

            private const float ROAM_REFRESH_RATE = 1f;

            internal bool HasInterestPoint => interestPoint != destination;

            internal Vector3 Destination => HasInterestPoint ? interestPoint : destination;

            private static readonly BasePlayer[] playersInVicinityQuery = new BasePlayer[1];

            private static readonly Func<BasePlayer, bool> filter = new Func<BasePlayer, bool>(IsHumanPlayer);

            internal static bool Create(Order order)
            {
                HordeManager manager = new HordeManager
                {
                    members = Pool.GetList<HordeMember>(),
                    initialSpawnPosition = order.position,
                    isLocalHorde = order.maximumRoamDistance > 0,
                    maximumRoamDistance = order.maximumRoamDistance,
                    initialMemberCount = order.initialMemberCount,
                    maximumMemberCount = order.maximumMemberCount,
                    hordeProfile = order.hordeProfile
                };                  

                for (int i = 0; i < order.initialMemberCount; i++)                
                    manager.SpawnMember(order.position, false);

                if (manager.members.Count == 0)
                {
                    manager.Destroy();
                    return false;
                }

                _allHordes.Add(manager);

                _hordeTickQueue.Add(manager);

                return true;
            }
            
            internal void Destroy(bool permanent = false, bool killNpcs = false)
            {
                isDestroyed = true;

                if (killNpcs)
                {
                    for (int i = members.Count - 1; i >= 0; i--)
                    {
                        HordeMember hordeMember = members[i];
                        if (hordeMember != null && hordeMember.Entity != null && !hordeMember.Entity.IsDestroyed)                        
                            hordeMember.Despawn();                        
                    }
                }

                members.Clear();
                Pool.FreeList(ref members);

                _allHordes.Remove(this);

                if (!permanent && _allHordes.Count <= configData.Horde.MaximumHordes)                
                    InvokeHandler.Invoke(Instance._hordeThinkManager, () => 
                    Order.CreateOrder(isLocalHorde ? initialSpawnPosition : Instance.GetSpawnPoint(), initialMemberCount, maximumMemberCount, isLocalHorde ? maximumRoamDistance : -1f, hordeProfile), configData.Horde.RespawnTime);                
            }

            internal void HordeTick()
            {
                if (members.Count == 0 || isDestroyed)                
                    return;

                AverageLocation = GetAverageVector();

                UpdateDormancy();
                if (_isDormant)
                    return;

                TryMergeHordes();

                TryGrowHorde();

                bool hasValidTarget = HasTarget();
                if (PrimaryTarget is BasePlayer && ShouldIgnorePlayer(PrimaryTarget as BasePlayer))
                {
                    PrimaryTarget = null;
                    hasValidTarget = false;
                }

                if (hasValidTarget)
                {
                    interestPoint = destination = PrimaryTarget.transform.position;

                    for (int i = 0; i < members.Count; i++)                    
                        members[i].SetTarget();                    
                }
                else
                {
                    if (Time.time > refreshRoamTime)
                    {
                        refreshRoamTime = Time.time + ROAM_REFRESH_RATE;

                        if ((!isRegrouping && GetMaximumSeperation() > 15f) || (isRegrouping && GetMaximumSeperation() > 5))
                        {
                            isRegrouping = true;
                            interestPoint = destination = members.GetRandom().Transform.position;                            
                        }

                        if (Destination == Vector3.zero || Vector3.Distance(Destination, AverageLocation) < 10f)
                        {
                            isRegrouping = false;
                            interestPoint = destination = GetRandomLocation(isLocalHorde ? initialSpawnPosition : AverageLocation);                            
                        }                        
                    }
                }                
            }

            internal bool HasTarget() => PrimaryTarget != null && PrimaryTarget.transform != null && !PrimaryTarget.IsDead();

            internal void SetPrimaryTarget(BaseCombatEntity baseCombatEntity)
            {
                if (baseCombatEntity == null || baseCombatEntity.transform == null)
                {
                    PrimaryTarget = null;
                    interestPoint = destination = AverageLocation;
                    return;
                }

                PrimaryTarget = baseCombatEntity;

                interestPoint = destination = PrimaryTarget.transform.position;
               
                for (int i = 0; i < members.Count; i++)
                {
                    members[i].SetTarget();
                }
            }

            internal Vector3 GetAverageVector()
            {
                Vector3 location = Vector3.zero;

                if (members.Count == 0)
                    return location;

                int count = 0;
                for (int i = 0; i < members.Count; i++)
                {
                    HordeMember hordeMember = members[i];

                    if (hordeMember == null || hordeMember.Entity == null)
                        continue;

                    location += hordeMember.Transform.position;
                    count++;
                }

                return location /= count;
            }

            
            private const TerrainTopology.Enum DESTINATION_TOPOLOGY_MASK = (TerrainTopology.Enum.Ocean | TerrainTopology.Enum.River | TerrainTopology.Enum.Lake | TerrainTopology.Enum.Offshore | TerrainTopology.Enum.Cliff);

            private Vector3 GetRandomLocation(Vector3 from)
            {
                for (int i = 0; i < 10; i++)
                {
                    Vector2 vector2 = UnityEngine.Random.insideUnitCircle * (isLocalHorde ? maximumRoamDistance : 100f);
                    
                    Vector3 destination = from + new Vector3(vector2.x, 0f, vector2.y);
                    if (TerrainMeta.HeightMap != null)                    
                        destination.y = TerrainMeta.HeightMap.GetHeight(destination);

                    NavMeshHit navMeshHit;
                    if (NavMesh.FindClosestEdge(destination, out navMeshHit, 1))
                    {
                        destination = navMeshHit.position;
                        if (WaterLevel.GetWaterDepth(destination, true, null) <= 0.01f && !ContainsTopologyAtPoint(DESTINATION_TOPOLOGY_MASK, destination))                                                    
                            return destination;                        
                    }
                    else if (NavMesh.SamplePosition(destination, out navMeshHit, 5f, 1) && !ContainsTopologyAtPoint(DESTINATION_TOPOLOGY_MASK, destination))
                    {                        
                        destination = navMeshHit.position;
                        if (WaterLevel.GetWaterDepth(destination, true, null) <= 0.01f)
                            return destination;                        
                    }
                }
                return AverageLocation;
            }

            private float GetMaximumSeperation()
            {
                float distance = 0;

                for (int i = 0; i < members.Count; i++)
                {
                    HordeMember hordeMember = members[i];
                    if (hordeMember != null && hordeMember.Entity != null)
                    {
                        float d = Vector3.Distance(hordeMember.Transform.position, AverageLocation);
                        if (d > distance)
                            distance = d;
                    }
                }

                return distance;
            }

            #region Dormancy  
            private float _nextDormancyCheck;

            private bool _isDormant = false;

            private void UpdateDormancy()
            {
                if (configData.Member.DisableDormantSystem)
                    return;

                if (Time.time < _nextDormancyCheck)
                    return;

                _nextDormancyCheck = Time.time + UnityEngine.Random.Range(0.5f, 1.5f);

                if (IsHordeCloseToPlayers())
                {
                    members.ForEach((HordeMember hordeMember) => hordeMember.Entity.IsDormant = false);
                    _isDormant = false;
                }
                else
                {
                    members.ForEach((HordeMember hordeMember) => hordeMember.Entity.IsDormant = true);
                    _isDormant = true;
                }
            }

            private bool IsHordeCloseToPlayers() => BaseEntity.Query.Server.GetPlayersInSphere(AverageLocation, AiManager.ai_to_player_distance_wakeup_range, HordeManager.playersInVicinityQuery, HordeManager.filter) > 0;

            private static bool IsHumanPlayer(BaseEntity entity)
            {
                BasePlayer basePlayer = entity as BasePlayer;
                if (basePlayer == null)
                    return false;

                if (basePlayer is IAIAgent)
                    return false;

                if (basePlayer.IsNpc || basePlayer is NPCPlayer || basePlayer is global::HumanNPC || basePlayer is HTNPlayer)
                    return false;

                if (!basePlayer.IsSleeping() && basePlayer.IsConnected)
                    return true;

                return false;
            }
            #endregion

            internal bool SpawnMember(Vector3 position, bool alreadyInitialized = true)
            {
                ScientistNPC scientistNPC = InstantiateEntity(position);                
                scientistNPC.enableSaving = false;

                HordeMember._allHordeScientists.Add(scientistNPC);

                BaseAIBrain<global::HumanNPC> defaultBrain = scientistNPC.GetComponent<BaseAIBrain<global::HumanNPC>>();
                defaultBrain._baseEntity = scientistNPC;
                UnityEngine.Object.DestroyImmediate(defaultBrain);

                HordeMember member = scientistNPC.gameObject.AddComponent<HordeMember>();
                member.Manager = this;
                scientistNPC._brain = scientistNPC.gameObject.AddComponent<ZombieBrain>();

                scientistNPC.Spawn();
                
                member.Setup();
                members.Add(member);

                if (alreadyInitialized)
                {
                    if (PrimaryTarget != null)
                        member.SetTarget();                    
                }

                return true;
            }

            internal void OnPlayerDeath(BasePlayer player, HordeMember hordeMember)
            {
                if (hordeMember == null || !members.Contains(hordeMember))
                    return;

                if (members.Count < maximumMemberCount)
                    SpawnMember(hordeMember.Transform.position);
            }

            internal void OnMemberDeath(HordeMember hordeMember, BaseCombatEntity initiator)
            {
                if (isDestroyed || members == null)
                    return;

                members.Remove(hordeMember);

                if (members.Count == 0)
                    Destroy();
                else
                {
                    if (PrimaryTarget == null && initiator is BasePlayer)                    
                        SetPrimaryTarget(initiator);                    
                }
            }

            private void TryGrowHorde()
            {
                if (nextGrowthTime < Time.time)
                {
                    if (members.Count < maximumMemberCount)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            if (SpawnMember(members.GetRandom().Transform.position))
                                break;
                        }
                    }

                    nextGrowthTime = Time.time + configData.Horde.GrowthRate;
                }
            }

            private void TryMergeHordes()
            {
                if (!configData.Horde.MergeHordes || nextMergeTime > Time.time)
                    return;

                for (int y = _allHordes.Count - 1; y >= 0; y--)
                {
                    HordeManager manager = _allHordes[y];

                    if (manager == this)
                        continue;

                    if (members.Count >= maximumMemberCount)
                        return;

                    if (Vector3.Distance(AverageLocation, manager.AverageLocation) < 20)
                    {
                        int amountToMerge = maximumMemberCount - members.Count;
                        if (amountToMerge >= manager.members.Count)
                        {
                            for (int i = 0; i < manager.members.Count; i++)
                            {
                                HordeMember member = manager.members[i];
                                members.Add(member);
                                member.Manager = this;
                            }

                            manager.members.Clear();
                            manager.Destroy();

                            nextMergeTime = Time.time + MERGE_COOLDOWN;
                        }
                        else
                        {
                            bool hasMerged = false;
                            for (int i = 0; i < amountToMerge; i++)
                            {
                                if (manager.members.Count > 0)
                                {
                                    HordeMember member = manager.members[0];

                                    members.Add(member);

                                    member.Manager = this;

                                    manager.members.Remove(member);

                                    hasMerged = true;
                                }
                            }

                            if (hasMerged)                            
                                nextMergeTime = Time.time + MERGE_COOLDOWN;                            
                        }
                    }
                }
            }

            internal static bool ShouldIgnorePlayer(BasePlayer player)
            {
                if (player.IsDead())
                    return true;

                if (player._limitedNetworking)
                    return true;

                if (player.IsFlying)
                    return true;

                if (player.HasPlayerFlag(BasePlayer.PlayerFlags.SafeZone))
                    return true;

                if (configData.Member.IgnoreSleepers && player.IsSleeping())
                    return true;

                if (player.userID.IsSteamId() && Instance.permission.UserHasPermission(player.UserIDString, "zombiehorde.ignore"))
                    return true;

                if (player is ScientistNPC && HordeMember._allHordeScientists.Contains(player as ScientistNPC))
                    return true;

                return false;
            }

            internal static void Stimulate(Sensation sensation)
            {
                float radius = sensation.Radius * sensation.Radius;

                BasePlayer target = sensation.Initiator as BasePlayer;

                for (int i = 0; i < _allHordes.Count; i++)
                {
                    HordeManager hordeManager = _allHordes[i];
                    if ((hordeManager.AverageLocation - sensation.Position).sqrMagnitude <= radius)
                    {
                        if (target != null) 
                            hordeManager.SetKnown(target);

                        hordeManager.SetInterestPoint(sensation.Position, sensation.Type == SensationType.Explosion);
                    }
                }
            }

            private void SetKnown(BasePlayer target)
            {
                foreach (HordeMember hordeMember in members)
                    hordeMember.Entity.myMemory.SetKnown(target, hordeMember.Entity, null);
            }

            internal void SetInterestPoint(Vector3 position, bool forced = false)
            {
                if (PrimaryTarget == null && (forced || !HasInterestPoint))
                {
                    interestPoint = position;

                    foreach (HordeMember hordeMember in members)
                    {
                        hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.Sprint);
                        hordeMember.Entity.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, false);
                        hordeMember.Entity.SetDestination(interestPoint);
                    }
                }
            }

            public class Order
            {
                public Vector3 position;
                public int initialMemberCount;
                public int maximumMemberCount;
                public float maximumRoamDistance;
                public string hordeProfile;

                public Order(Vector3 position, int initialMemberCount, string hordeProfile)
                {
                    this.position = position;
                    this.initialMemberCount = initialMemberCount;
                    maximumMemberCount = configData.Horde.MaximumMemberCount;
                    maximumRoamDistance = -1f;
                    this.hordeProfile = hordeProfile;
                }

                public Order(Vector3 position, int initialMemberCount, int maximumMemberCount, float maximumRoamDistance, string hordeProfile)
                {
                    this.position = position;
                    this.initialMemberCount = initialMemberCount;
                    this.maximumMemberCount = maximumMemberCount;
                    this.maximumRoamDistance = maximumRoamDistance;
                    this.hordeProfile = hordeProfile;
                }

                private static Queue<Order> _queue = new Queue<Order>();

                private static bool IsSpawning { get; set; }

                private static bool IsDespawning { get; set; }

                private static Coroutine SpawnRoutine { get; set; }

                private static Coroutine DespawnRoutine { get; set; }

                internal static void CreateOrder(Vector3 position, int initialMemberCount, int maximumMemberCount, float maximumRoamDistance, string hordeProfile)
                {
                    object success = FindPointOnNavmesh(position, 10f);
                    if (success == null)
                        return;

                    _queue.Enqueue(new Order((Vector3)success, initialMemberCount, maximumMemberCount, maximumRoamDistance, hordeProfile));

                    if (!IsSpawning && _spawnState == SpawnState.Spawn)                    
                        BeginSpawning();                    
                }

                internal static void CreateOrder(Vector3 position, ConfigData.MonumentSpawn.MonumentSettings settings)
                {
                    object success = FindPointOnNavmesh(position, 10f);
                    if (success == null)                    
                        return;
                    
                    _queue.Enqueue(new Order((Vector3)success, configData.Horde.InitialMemberCount, settings.HordeSize, settings.RoamDistance, settings.Profile));

                    if (!IsSpawning && _spawnState == SpawnState.Spawn)
                        BeginSpawning();
                }

                internal static Coroutine BeginSpawning() => SpawnRoutine = ServerMgr.Instance.StartCoroutine(ProcessSpawnOrders());

                internal static Coroutine BeginDespawning() => DespawnRoutine = ServerMgr.Instance.StartCoroutine(ProcessDespawn());   
                
                internal static void StopSpawning()
                {
                    if (SpawnRoutine != null)
                        ServerMgr.Instance.StopCoroutine(SpawnRoutine);

                    IsSpawning = false;
                }

                internal static void StopDespawning()
                {
                    if (DespawnRoutine != null)
                        ServerMgr.Instance.StopCoroutine(DespawnRoutine);

                    IsDespawning = false;
                }

                private static IEnumerator ProcessSpawnOrders()
                {
                    if (_queue.Count == 0)
                        yield break;

                    IsSpawning = true;

                    RESTART:
                    if (IsDespawning)
                        StopDespawning();

                    while (_allHordes?.Count > configData.Horde.MaximumHordes)                    
                        yield return CoroutineEx.waitForSeconds(10f);
                    
                    Order order = _queue.Dequeue();

                    if (order != null)
                        Create(order);

                    if (_queue.Count > 0)
                    {
                        yield return CoroutineEx.waitForSeconds(3f);
                        goto RESTART;
                    }

                    IsSpawning = false;
                }

                private static IEnumerator ProcessDespawn()
                {
                    IsDespawning = true;

                    if (IsSpawning)
                        StopSpawning();

                    while (_allHordes?.Count > 0)
                    {
                        HordeManager manager = HordeManager._allHordes.GetRandom();
                        if (manager.PrimaryTarget == null)
                        {
                            Order.CreateOrder(manager.isLocalHorde ? manager.initialSpawnPosition : Instance.GetSpawnPoint(), manager.initialMemberCount,
                                              manager.maximumMemberCount, manager.isLocalHorde ? manager.maximumRoamDistance : -1f, manager.hordeProfile);

                            manager.Destroy(true, true);
                        }

                        yield return CoroutineEx.waitForSeconds(3f);
                    }

                    IsDespawning = false;
                }

                internal static void OnUnload()
                {
                    if (SpawnRoutine != null)
                        ServerMgr.Instance.StopCoroutine(SpawnRoutine);

                    if (DespawnRoutine != null)
                        ServerMgr.Instance.StopCoroutine(DespawnRoutine);

                    IsDespawning = false;
                    IsSpawning = false;

                   _queue.Clear();
                }
            }
        }
        #endregion

        #region Horde Member        
        public class HordeMember : MonoBehaviour, IThinker
        {
            internal ScientistNPC Entity { get; private set; }

            internal Transform Transform { get; private set; }

            internal HordeManager Manager { get; set; }


            private bool lightsOn;

            private ConfigData.MemberOptions.Loadout loadout;

            private ItemContainer[] containers;

            internal AttackEntity _attackEntity;

            internal ThrownWeapon _throwableWeapon;


            internal float DamageScale { get; private set; }
           
            internal bool _NavMeshEnabled
            {
                get
                {
                    return (bool)typeof(global::HumanNPC).GetField("navmeshEnabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).GetValue(Entity);
                }
                set
                {
                    typeof(global::HumanNPC).GetField("navmeshEnabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).SetValue(Entity, value);
                }
            }

            internal bool NavMeshEnabled { get; set; }



            private const int AREA_MASK = 1;

            private const int AGENT_TYPE_ID = -1372625422;

            internal static HashSet<ScientistNPC> _allHordeScientists = new HashSet<ScientistNPC>();

            #region Initialization
            private void Awake()
            {
                Entity = GetComponent<ScientistNPC>();
                Transform = Entity.transform;

                Entity.DeathEffects = new GameObjectRef[0];
                Entity.RadioChatterEffects = new GameObjectRef[0];

                AIThinkManager.Remove(Entity);
                AIThinkManager.Add(this);
            }
                        
            internal void Setup()
            {
                Entity.CancelInvoke(Entity.IdleCheck);
                Entity.CancelInvoke(Entity.EnableNavAgent);
                Entity.CancelInvoke(Entity.EquipTest);
                
                Entity.NavAgent.areaMask = AREA_MASK;
                Entity.NavAgent.agentTypeID = AGENT_TYPE_ID;

                if (!string.IsNullOrEmpty(Manager.hordeProfile) && configData.HordeProfiles.ContainsKey(Manager.hordeProfile))
                {
                    string loadoutId = configData.HordeProfiles[Manager.hordeProfile].GetRandom();
                    loadout = configData.Member.Loadouts.Find(x => x.LoadoutID == loadoutId);
                }
                else loadout = configData.Member.Loadouts.GetRandom();

                Entity.displayName = loadout.Names.Length > 0 ? loadout.Names.GetRandom() : "Zombie";

                Entity.sightRange = loadout.Sensory.VisionRange;

                Entity.InitializeHealth(loadout.Vitals.Health, loadout.Vitals.Health);

                DamageScale = loadout.DamageMultiplier;

                UpdateGear();

                Entity.Invoke(EnableNavAgent, 0.25f);

                Entity.Invoke(EquipWeapon, 0.25f);
                
                Entity.InvokeRandomized(TickSpeed, 1f, Entity.PositionTickRate, Entity.PositionTickRate * 0.1f);

                Entity.InvokeRandomized(UpdateTick, 1f, 4f, 1f);

                RunZombieEffects();
            }

            protected void UpdateGear()
            {
                StripInventory(Entity);

                for (int i = 0; i < loadout.BeltItems.Count; i++)
                {
                    ConfigData.LootTable.InventoryItem loadoutItem = loadout.BeltItems[i];

                    Item item = ItemManager.CreateByName(loadoutItem.Shortname, loadoutItem.Amount, loadoutItem.SkinID);
                    item.MoveToContainer(Entity.inventory.containerBelt);

                    if (_throwableWeapon == null && item.GetHeldEntity() is ThrownWeapon)                    
                        _throwableWeapon = item.GetHeldEntity() as ThrownWeapon;
                    
                    if (loadoutItem.SubSpawn != null && item.contents != null)
                    {
                        for (int y = 0; y < loadoutItem.SubSpawn.Length; y++)
                        {
                            ConfigData.LootTable.InventoryItem subspawnItem = loadoutItem.SubSpawn[y];

                            Item subItem = ItemManager.CreateByName(subspawnItem.Shortname, subspawnItem.Amount, subspawnItem.SkinID);
                            subItem.MoveToContainer(item.contents);
                        }
                    }
                }

                for (int i = 0; i < loadout.MainItems.Count; i++)
                {
                    ConfigData.LootTable.InventoryItem loadoutItem = loadout.MainItems[i];

                    Item item = ItemManager.CreateByName(loadoutItem.Shortname, loadoutItem.Amount, loadoutItem.SkinID);
                    item.MoveToContainer(Entity.inventory.containerMain);

                    if (loadoutItem.SubSpawn != null && item.contents != null)
                    {
                        for (int y = 0; y < loadoutItem.SubSpawn.Length; y++)
                        {
                            ConfigData.LootTable.InventoryItem subspawnItem = loadoutItem.SubSpawn[y];

                            Item subItem = ItemManager.CreateByName(subspawnItem.Shortname, subspawnItem.Amount, subspawnItem.SkinID);
                            subItem.MoveToContainer(item.contents);
                        }
                    }
                }

                for (int i = 0; i < loadout.WearItems.Count; i++)
                {
                    ConfigData.LootTable.InventoryItem loadoutItem = loadout.WearItems[i];

                    Item item = ItemManager.CreateByName(loadoutItem.Shortname, loadoutItem.Amount, loadoutItem.SkinID);
                    item.MoveToContainer(Entity.inventory.containerWear);

                    if (loadoutItem.SubSpawn != null && item.contents != null)
                    {
                        for (int y = 0; y < loadoutItem.SubSpawn.Length; y++)
                        {
                            ConfigData.LootTable.InventoryItem subspawnItem = loadoutItem.SubSpawn[y];

                            Item subItem = ItemManager.CreateByName(subspawnItem.Shortname, subspawnItem.Amount, subspawnItem.SkinID);
                            subItem.MoveToContainer(item.contents);
                        }
                    }
                }
                                
                Entity.InvokeRandomized(LightCheck, 5f, 30f, 5f);

                if (configData.Member.GiveGlowEyes)
                    ItemManager.Create(Instance._glowEyes).MoveToContainer(Entity.inventory.containerWear);
            }
            #endregion

            #region Lights
            private void LightCheck()
            {
                if ((TOD_Sky.Instance.Cycle.Hour > 18 || TOD_Sky.Instance.Cycle.Hour < 6) && !lightsOn)
                    LightToggle(true);
                else if ((TOD_Sky.Instance.Cycle.Hour < 18 && TOD_Sky.Instance.Cycle.Hour > 6) && lightsOn)
                    LightToggle(false);
            }

            private void LightToggle(bool on)
            {
                Item activeItem = Entity.GetActiveItem();
                if (activeItem != null)
                {
                    BaseEntity heldEntity = activeItem.GetHeldEntity();
                    if (heldEntity != null)
                    {
                        HeldEntity component = heldEntity.GetComponent<HeldEntity>();
                        if (component)
                        {
                            component.SendMessage("SetLightsOn", on, SendMessageOptions.DontRequireReceiver);
                        }
                    }
                }
                foreach (Item item in Entity.inventory.containerWear.itemList)
                {
                    ItemModWearable itemModWearable = item.info.GetComponent<ItemModWearable>();
                    if (!itemModWearable || !itemModWearable.emissive)
                        continue;

                    item.SetFlag(global::Item.Flag.IsOn, on);
                    item.MarkDirty();
                }

                lightsOn = on;
            }
            #endregion

            #region Loot
            internal void PrepareInventory()
            {
                ItemContainer[] source = new ItemContainer[] { Entity.inventory.containerMain, Entity.inventory.containerWear, Entity.inventory.containerBelt };

                containers = new ItemContainer[3];

                for (int i = 0; i < containers.Length; i++)
                {
                    containers[i] = new ItemContainer();
                    containers[i].ServerInitialize(null, source[i].capacity);
                    containers[i].GiveUID();
                    Item[] array = source[i].itemList.ToArray();
                    for (int j = 0; j < array.Length; j++)
                    {
                        Item item = array[j];
                        if (i == 1)
                        {
                            Item newItem = ItemManager.CreateByItemID(item.info.itemid, item.amount, item.skin);
                            if (!newItem.MoveToContainer(containers[i], -1, true))
                                newItem.Remove(0f);
                        }
                        else
                        {                            
                            if (!item.MoveToContainer(containers[i], -1, true))
                                item.Remove(0f);
                        }
                    }
                }
            }

            internal void MoveInventoryTo(LootableCorpse corpse)
            {
                for (int i = 0; i < containers.Length; i++)
                {
                    Item[] array = containers[i].itemList.ToArray();
                    corpse.containers[i].capacity = array.Length;

                    for (int j = 0; j < array.Length; j++)
                    {
                        Item item = array[j];
                        if (item != null && !item.MoveToContainer(corpse.containers[i], -1, true))                        
                            item.Remove(0f);                        
                    }
                }

                corpse.ResetRemovalTime();
            }
            #endregion

            #region Navigation
            private float lastThinkTime = Time.time;

            private float lastMovementTickTime = Time.time;

            internal void EnableNavAgent()
            {
                NavMeshHit navMeshHit;

                if (!NavMesh.SamplePosition(Transform.position + (Vector3.up * 1f), out navMeshHit, 20f, -1))
                {
                    Debug.Log("Failed to sample navmesh");
                    return;
                }

                Entity.NavAgent.Warp(navMeshHit.position);
                Transform.position = navMeshHit.position;

                _NavMeshEnabled = NavMeshEnabled = true;

                Entity.NavAgent.enabled = true;
                Entity.NavAgent.isStopped = false;
                SetDestination(Transform.position);
            }

            internal void SetDestination(Vector3 destination)
            {
                if (!NavMeshEnabled)
                {
                    if (!_NavMeshEnabled)
                        _NavMeshEnabled = true;

                    Entity.NavAgent.enabled = true;
                    Entity.NavAgent.isStopped = false;
                    Entity.SetDestination(destination);
                }
            }

            private void TickSpeed()
            {
                float d = Time.time - lastMovementTickTime;
                lastMovementTickTime = Time.time;
                UpdateSpeed(d);
            }

            private void UpdateSpeed(float delta)
            {
                float speed = SpeedFromEnum(Entity.desiredSpeed);
                Entity.NavAgent.speed = Mathf.Lerp(Entity.NavAgent.speed, speed, delta * 8f);
            }

            public float SpeedFromEnum(global::HumanNPC.SpeedType newSpeed)
            {
                switch (newSpeed)
                {
                    case global::HumanNPC.SpeedType.Crouch:
                        return loadout.Movement.DuckSpeed;
                    case global::HumanNPC.SpeedType.SlowWalk:
                        return loadout.Movement.WalkSpeed * 0.5f;
                    case global::HumanNPC.SpeedType.Walk:
                        return loadout.Movement.WalkSpeed;
                    case global::HumanNPC.SpeedType.Sprint:
                        return loadout.Movement.RunSpeed;
                }
                return 0f;
            }
            #endregion

            #region Think
            private void UpdateTick()
            {
                if (Entity == null || Entity.IsDestroyed)
                {
                    Destroy(this);
                    return;
                }

                if (_attackEntity == null)
                    _attackEntity = Entity.GetAttackEntity();
            }

            public void TryThink()
            {
                ServerThink(Time.time - lastThinkTime);
                lastThinkTime = Time.time;
            }

            private void ServerThink(float delta)
            {
                if (!configData.Member.DisableDormantSystem)
                {
                    if (Entity.IsDormant)
                        return;
                }
                else Entity.IsDormant = false;

                if (Entity == null || Entity.IsDestroyed)
                    return;

                if (configData.Member.KillUnderWater && Entity.WaterFactor() > 0.8f)
                {
                    Die();
                    return;
                }

                Entity.TickAi(delta);
                if (Entity._brain.ShouldServerThink())
                {
                    Entity._brain.DoThink();
                }

                Entity.timeSinceItemTick += delta;
                Entity.timeSinceTargetUpdate += delta;
                
                if (Entity.timeSinceItemTick > 0.1f)
                {
                    Entity.TickItems(Entity.timeSinceItemTick);
                    Entity.timeSinceItemTick = 0f;
                }

                if (Entity.timeSinceTargetUpdate > 0.5f)
                {
                    UpdateTargets();
                    Entity.timeSinceTargetUpdate = 0f;
                }
            }

            internal void SetTarget()
            {
                Entity.myMemory.SetKnown(Manager.PrimaryTarget, Entity, null);

                Entity.currentTarget = Manager.PrimaryTarget;

                Entity.currentTargetLOS = TargetVisible(Manager.PrimaryTarget);

                Entity.SetDestination(Manager.PrimaryTarget.transform.position);
            }

            internal bool TargetVisible(BaseCombatEntity baseCombatEntity)
            {
                return baseCombatEntity is BasePlayer ? Entity.IsPlayerVisibleToUs(baseCombatEntity as BasePlayer) :
                    (Entity.IsVisible(baseCombatEntity.CenterPoint(), Entity.eyes.worldStandingPosition, float.PositiveInfinity) ||
                    !Entity.IsVisible(baseCombatEntity.transform.position, Entity.eyes.worldStandingPosition, float.PositiveInfinity));
            }
            #endregion

            #region Targeting            
            private void UpdateTargets()
            {
                UpdateMemory();

                int targetIndex = -1;
                float targetDelta = -1f;
                Vector3 currentPosition = Transform.position;

                for (int i = 0; i < Entity.myMemory.All.Count; i++)
                {
                    Rust.AI.SimpleAIMemory.SeenInfo seenInfo = Entity.myMemory.All[i];
                    if (seenInfo.Entity != null)
                    {
                        float distToTarget = Vector3.Distance(seenInfo.Entity.transform.position, currentPosition);

                        if (seenInfo.Entity is BasePlayer && HordeManager.ShouldIgnorePlayer(seenInfo.Entity as BasePlayer))
                            continue;

                        if (seenInfo.Entity.Health() > 0f)
                        {
                            float sightRangeDelta = (1f - Mathf.InverseLerp(10f, Entity.sightRange, distToTarget));
                            Vector3 dirToTarget = seenInfo.Entity.transform.position - Entity.eyes.position;

                            float d = Vector3.Dot(dirToTarget.normalized, Entity.eyes.BodyForward());
                            sightRangeDelta += Mathf.InverseLerp(Entity.visionCone, 1f, d);
                            
                            float timestamp = seenInfo.Timestamp - Time.realtimeSinceStartup;
                            sightRangeDelta += (1f - Mathf.InverseLerp(0f, 3f, timestamp));
                            
                            if (sightRangeDelta > targetDelta)
                            {
                                targetIndex = i;
                                targetDelta = sightRangeDelta;
                            }
                        }
                    }
                }

                if (targetIndex == -1)
                {
                    Entity.currentTarget = null;
                    Entity.currentTargetLOS = false;
                }
                else
                {
                    Rust.AI.SimpleAIMemory.SeenInfo seenInfo = Entity.myMemory.All[targetIndex];
                    if (seenInfo.Entity != null && seenInfo.Entity is BaseCombatEntity)
                    {                        
                        Entity.currentTarget = (seenInfo.Entity as BaseCombatEntity);
                        Entity.currentTargetLOS = IsVisibleToUs(Entity.currentTarget);

                        if (Manager.PrimaryTarget == null)
                            Manager.SetPrimaryTarget(Entity.currentTarget);
                    }
                }
            }

            private void UpdateMemory()
            {
                int inSphere = BaseEntity.Query.Server.GetInSphere(Transform.position, Entity.sightRange, Entity.QueryResults, new Func<BaseEntity, bool>(AiCaresAbout));
                for (int i = 0; i < inSphere; i++)
                {
                    BaseCombatEntity baseCombatEntity = Entity.QueryResults[i] as BaseCombatEntity;
                    if (baseCombatEntity != null && !baseCombatEntity.EqualNetID(Entity) && Entity.WithinVisionCone(baseCombatEntity) && IsVisibleToUs(baseCombatEntity))
                    {
                        Entity.myMemory.SetKnown(baseCombatEntity, Entity, null);
                    }
                }

                Forget(Entity.memoryDuration);
            }

            private void Forget(float secondsOld)
            {
                for (int i = 0; i < Entity.myMemory.All.Count; i++)
                {
                    if (Time.realtimeSinceStartup - Entity.myMemory.All[i].Timestamp > secondsOld)
                    {
                        BaseEntity entity = Entity.myMemory.All[i].Entity;
                        if (entity != null)
                        {
                            if (entity is BasePlayer)
                            {
                                Entity.myMemory.Players.Remove(entity);
                            }
                            Entity.myMemory.Targets.Remove(entity);
                            Entity.myMemory.Threats.Remove(entity);
                            Entity.myMemory.Friendlies.Remove(entity);
                            Entity.myMemory.LOS.Remove(entity);
                        }
                        Entity.myMemory.All.RemoveAt(i);
                        i--;
                    }
                }
            }

            private bool IsVisibleToUs(BaseCombatEntity baseCombatEntity)
            {
                if (baseCombatEntity is BasePlayer)
                    return Entity.IsPlayerVisibleToUs(baseCombatEntity as BasePlayer);
                else return Entity.IsVisible(baseCombatEntity.CenterPoint(), Entity.eyes.worldStandingPosition, float.PositiveInfinity);                
            }

            private static bool AiCaresAbout(BaseEntity entity)
            {
                BaseCombatEntity baseCombatEntity = entity as BaseCombatEntity;

                if (baseCombatEntity == null || baseCombatEntity.IsDestroyed || baseCombatEntity.transform == null)
                    return false;

                if (baseCombatEntity.Health() <= 0f)
                    return false;

                if (baseCombatEntity is BasePlayer)
                {
                    BasePlayer player = baseCombatEntity as BasePlayer;

                    if (HordeManager.ShouldIgnorePlayer(player))
                        return false;
                    
                    if (!configData.Member.TargetNPCs)
                    {
                        if (player.IsNpc)
                            return false;

                        if (player is NPCPlayer || player is HTNPlayer || player is global::HumanNPC)
                            return false;
                    }
                    
                    if (!configData.Member.TargetHumanNPCs && !player.userID.IsSteamId() && !player.IsNpc)
                        return false;

                    return true;
                }
                                
                if (baseCombatEntity is BaseNpc)                
                    return configData.Member.TargetAnimals;
                
                return false;
            }
            #endregion

            #region Equip/Holster
            internal void EquipWeapon()
            {
                if (Entity == null || Entity.IsDestroyed || Entity.IsDead())
                    return;

                for (int i = 0; i < Entity.inventory.containerBelt.itemList.Count; i++)
                {
                    Item slot = Entity.inventory.containerBelt.GetSlot(i);
                    if (slot != null)
                    {
                        Entity.UpdateActiveItem(Entity.inventory.containerBelt.GetSlot(i).uid);

                        BaseEntity heldEntity = slot.GetHeldEntity();
                        if (heldEntity != null)
                        {
                            _attackEntity = heldEntity.GetComponent<AttackEntity>();

                            if (_attackEntity is ThrownWeapon)
                                continue;

                            if (_attackEntity != null)
                                _attackEntity.TopUpAmmo();

                            if (_attackEntity is BaseProjectile)
                                _attackEntity.effectiveRange *= 2f;

                            if (_attackEntity is Chainsaw)
                                (_attackEntity as Chainsaw).ServerNPCStart();

                            return;
                        }                        
                    }
                }
            }

            internal void EquipThrowable()
            {
                if (Entity == null || Entity.IsDestroyed || Entity.IsDead())
                    return;

                if (_throwableWeapon == null)
                    return;

                for (int i = 0; i < Entity.inventory.containerBelt.itemList.Count; i++)
                {
                    Item slot = Entity.inventory.containerBelt.GetSlot(i);
                    if (slot != null)
                    {
                        if (slot.GetHeldEntity() == _throwableWeapon)
                        {
                            Entity.UpdateActiveItem(Entity.inventory.containerBelt.GetSlot(i).uid);
                            _attackEntity = _throwableWeapon;
                            return;
                        }  
                    }
                }
            }

            internal void HolsterWeapon()
            {
                Entity.svActiveItemID = 0;

                Item activeItem = Entity.GetActiveItem();
                if (activeItem != null)
                {
                    HeldEntity heldEntity = activeItem.GetHeldEntity() as HeldEntity;
                    if (heldEntity != null)
                    {
                        heldEntity.SetHeld(false);
                    }
                }

                Entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                Entity.inventory.UpdatedVisibleHolsteredItems();
            }
            #endregion

            #region Weapon Usage
            private readonly NavMeshPath navMeshPath = new NavMeshPath();

            private bool isThrowingWeapon = false;

            private float nextThrowTime = Time.time + ConVar.Halloween.scarecrow_throw_beancan_global_delay + UnityEngine.Random.Range(0, 10);

            internal float GetIdealDistanceFromTarget() => Mathf.Max(1f, EngagementRange() * 0.75f);
            
            internal float EngagementRange()
            {
                if (_attackEntity != null)
                {
                    if (_attackEntity is ThrownWeapon)
                        return (_attackEntity as ThrownWeapon).maxThrowVelocity;

                    return _attackEntity.effectiveRange;
                }
                
                return 1f;
            }

            internal float ThrownEngagementRange()
            {
                if (_throwableWeapon != null)
                    return _throwableWeapon.maxThrowVelocity;

                return 0f;
            }

            internal bool TargetInRange()
            {
                if (!Entity.HasTarget())                
                    return false;
                
                return Entity.DistanceToTarget() <= EngagementRange();
            }

            internal bool TargetInThrowableRange()
            {
                if (!Entity.HasTarget())
                    return false;

                return Entity.DistanceToTarget() <= ThrownEngagementRange();
            }

            internal bool CanThrowWeapon()
            {
                if (!ConVar.AI.npc_use_thrown_weapons)
                    return false;

                if (isThrowingWeapon)
                    return true;

                if (Time.time < nextThrowTime)
                    return false;

                if (Entity.currentTarget == null)
                    return false;

                if (CanNavigateToTarget())
                    return false;
                                
                if (!IsVisibleToUs(Entity.currentTarget))               
                    return false;                

                return true;
            }

            internal void TryThrowWeapon()
            {
                if (isThrowingWeapon)
                    return;

                isThrowingWeapon = true;
                Entity.StartCoroutine(ThrowWeapon());
            }

            private IEnumerator ThrowWeapon()
            {
                EquipThrowable();
                yield return CoroutineEx.waitForSeconds(1f + UnityEngine.Random.value);

                if (Entity.currentTarget != null)
                {
                    Entity.SetAimDirection((Entity.currentTarget.transform.position - Transform.position).normalized);

                    _throwableWeapon.GetItem().amount += 1;

                    _throwableWeapon.ServerThrow(Entity.currentTarget.transform.position);

                    nextThrowTime = Time.time + ConVar.Halloween.scarecrow_throw_beancan_global_delay + UnityEngine.Random.Range(0, 8);
                }

                yield return CoroutineEx.waitForSeconds(1f);

                EquipWeapon();
                yield return CoroutineEx.waitForSeconds(1f);
                isThrowingWeapon = false;
            }

            internal bool CanNavigateToTarget()
            {
                if (Entity.NavAgent.CalculatePath(Entity.currentTarget.transform.position, navMeshPath) && navMeshPath.status == NavMeshPathStatus.PathComplete)
                    return true;
                
                return false;
            }

            internal bool OutOfThrowingRange()
            {
                if ((Transform.position - Entity.currentTarget.transform.position).magnitude > ThrownEngagementRange())
                    return true;

                return false;
            }

            internal void DoMeleeAttack() // Hackery to make ScientistNPC's do melee damage
            {
                if (_attackEntity == null || !(_attackEntity is BaseMelee))
                    return;

                BaseMelee baseMelee = _attackEntity as BaseMelee;
                if (baseMelee.HasAttackCooldown())
                    return;

                baseMelee.StartAttackCooldown(baseMelee.repeatDelay * 2f);
                Entity.SignalBroadcast(BaseEntity.Signal.Attack, string.Empty, null);

                if (baseMelee.swingEffect.isValid)
                    Effect.server.Run(baseMelee.swingEffect.resourcePath, baseMelee.transform.position, Vector3.forward, Entity.net.connection, false);

                DoMeleeDamage(_attackEntity as BaseMelee);
            }

            private void DoMeleeDamage(BaseMelee baseMelee)
            {
                Vector3 position = Entity.eyes.position;
                Vector3 forward = Entity.eyes.BodyForward();

                for (int i = 0; i < 2; i++)
                {
                    List<RaycastHit> list = Pool.GetList<RaycastHit>();

                    GamePhysics.TraceAll(new Ray(position - (forward * (i == 0 ? 0f : 0.2f)), forward), (i == 0 ? 0f : baseMelee.attackRadius), list, baseMelee.effectiveRange + 0.2f, 1219701521, QueryTriggerInteraction.UseGlobal);

                    bool hasHit = false;
                    for (int j = 0; j < list.Count; j++)
                    {
                        RaycastHit raycastHit = list[j];
                        BaseEntity hitEntity = raycastHit.GetEntity();

                        if (hitEntity != null && hitEntity != Entity && !hitEntity.EqualNetID(Entity) && !(hitEntity is ScientistNPC))
                        {
                            float damageAmount = 0f;
                            foreach (DamageTypeEntry damageType in baseMelee.damageTypes)
                                damageAmount += damageType.amount;

                            hitEntity.OnAttacked(new HitInfo(Entity, hitEntity, DamageType.Slash, damageAmount * baseMelee.npcDamageScale));

                            HitInfo hitInfo = Pool.Get<HitInfo>();
                            hitInfo.HitEntity = hitEntity;
                            hitInfo.HitPositionWorld = raycastHit.point;
                            hitInfo.HitNormalWorld = -forward;

                            if (hitEntity is BaseNpc || hitEntity is BasePlayer)
                                hitInfo.HitMaterial = StringPool.Get("Flesh");
                            else hitInfo.HitMaterial = StringPool.Get((raycastHit.GetCollider().sharedMaterial != null ? raycastHit.GetCollider().sharedMaterial.GetName() : "generic"));

                            Effect.server.ImpactEffect(hitInfo);
                            Pool.Free(ref hitInfo);

                            hasHit = true;

                            if (hitEntity.ShouldBlockProjectiles())
                                break;
                        }
                    }

                    Pool.FreeList(ref list);
                    if (hasHit)
                        break;
                }
            }
            #endregion

            #region Death
            internal void Die()
            {
                if (Entity == null || Entity.IsDestroyed || Entity.IsDead())
                    return;

                _allHordeScientists.Remove(Entity);

                Entity.Die(new HitInfo(Entity, Entity, DamageType.Explosion, 1000f));
            }
                       
            internal void Despawn()
            {                
                if (Entity == null || Entity.IsDestroyed || Entity.IsDead())
                    return;

                _allHordeScientists.Remove(Entity);

                StripInventory(Entity);
                Entity.Kill();
            }

            private void OnDestroy()
            {
                RunDeathEffect();

                AIThinkManager._processQueue.Remove(this);

                if (Entity != null)
                {
                    _allHordeScientists.Remove(Entity);

                    Entity.CancelInvoke(EnableNavAgent);
                    Entity.CancelInvoke(EquipWeapon);
                    Entity.CancelInvoke(UpdateTick);
                    Entity.CancelInvoke(TickSpeed);
                    Entity.CancelInvoke(LightCheck);
                }
            }
            #endregion

            #region Effects
            private void RunZombieEffects()
            {
                if (!Entity.IsAlive() || Entity.IsDestroyed)
                    return;

                const string EFFECT = "assets/prefabs/npc/murderer/sound/breathing.prefab";

                Effect.server.Run(EFFECT, Entity, StringPool.Get("head"), Vector3.zero, Vector3.zero, null, false);

                Entity.Invoke(RunZombieEffects, UnityEngine.Random.Range(10f, 15f));
            }

            private void RunDeathEffect()
            {
                const string EFFECT = "assets/prefabs/npc/murderer/sound/death.prefab";
                Effect.server.Run(EFFECT, Entity.ServerPosition, Vector3.up, null, false);
            }
            #endregion
        }
        #endregion

        #region States
        public class ZombieBrain : BaseAIBrain<global::HumanNPC> 
        {
            private HordeMember hordeMember;

            private void Awake()
            {
                hordeMember = GetComponent<HordeMember>();
            }
            public override void AddStates()
            {
                base.AddStates();

                AddState(new HordeRoamState(hordeMember));
                AddState(new MemberChaseState(hordeMember));
                AddState(new MemberCombatState(hordeMember));
                AddState(new MemberCombatThrowState(hordeMember));
            }

            public override void InitializeAI()
            {
                global::HumanNPC humanNpc = GetEntity();

                UseAIDesign = false;

                base.InitializeAI();

                ThinkMode = AIThinkMode.Interval;
                thinkRate = 0.25f;
                PathFinder = new HumanPathFinder();
                ((HumanPathFinder)PathFinder).Init(humanNpc);
            }

            public override void Think(float delta)
            {
                if (!ConVar.AI.think || states == null)
                    return;

                lastThinkTime = Time.time;
                
                if (!configData.Member.DisableDormantSystem && hordeMember.Entity.IsDormant)
                    return;

                if (CurrentState != null)
                    CurrentState.StateThink(delta);

                if (CurrentState == null || CurrentState.CanLeave())
                {
                    float highest = 0f;
                    BasicAIState state = null;

                    foreach (BasicAIState value in states.Values)
                    {
                        if (value == null || !value.CanEnter())
                            continue;

                        float weight = value.GetWeight();

                        if (weight <= highest)
                            continue;

                        highest = weight;
                        state = value;
                    }

                    if (state != CurrentState)
                        SwitchToState(state, -1);
                }
            }

            public class HordeRoamState : BasicAIState
            {
                private readonly HordeMember hordeMember;

                private float nextSetDestinationTime;

                public HordeRoamState(HordeMember hordeMember) : base(AIState.Roam)
                {
                    this.hordeMember = hordeMember;
                }

                public override float GetWeight()
                {                    
                    if (hordeMember.Entity.HasTarget())
                        return 0f;

                    return 1f;
                }

                public override void StateEnter()
                {
                    hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.SlowWalk);
                    hordeMember.Entity.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);

                    base.StateEnter();
                }

                public override StateStatus StateThink(float delta)
                {
                    base.StateThink(delta);

                    if (hordeMember.Entity.IsDormant)
                        return StateStatus.Running;

                    float distanceFromAverage = Vector3.Distance(hordeMember.Manager.AverageLocation, hordeMember.Entity.transform.position);
                    if (hordeMember.Manager.HasInterestPoint || distanceFromAverage > 15f)
                    {
                        hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.Sprint);
                        hordeMember.Entity.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, false);
                    }
                    else
                    {
                        hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.SlowWalk);
                        hordeMember.Entity.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
                    }

                    if (Vector3.Distance(hordeMember.Manager.Destination, hordeMember.Transform.position) < 3f)                    
                        return StateStatus.Running;
                    
                    if (Time.time < nextSetDestinationTime)                  
                        return StateStatus.Running;
                    
                    if (distanceFromAverage > 15f)                                          
                        hordeMember.Entity.SetDestination(hordeMember.Manager.AverageLocation);                    
                    else hordeMember.Entity.SetDestination(hordeMember.Manager.Destination);

                    nextSetDestinationTime = Time.time + 3f;

                    return StateStatus.Running;
                }
            }

            public class MemberChaseState : BasicAIState
            {
                private readonly HordeMember hordeMember;

                private float nextPositionUpdateTime;

                public MemberChaseState(HordeMember hordeMember) : base(AIState.Chase)
                {
                    this.hordeMember = hordeMember;
                }

                public override float GetWeight()
                {
                    float delta = 0.5f;

                    if (!hordeMember.Entity.HasTarget())                    
                        return 0f;

                    if (Vector3.Distance(hordeMember.Transform.position, hordeMember.Manager.AverageLocation) > 30f)
                        return 0f;

                    if (hordeMember.Entity.AmmoFractionRemaining() < 0.3f || hordeMember.Entity.IsReloading())                    
                        delta -= 1f;
                             
                    if (!hordeMember.Entity.CanSeeTarget())                    
                        delta -= 0.5f;                    
                    else delta += 1f;
                    
                    if (hordeMember.Entity.DistanceToTarget() > hordeMember.GetIdealDistanceFromTarget())                    
                        delta += 1f;
                    
                    return delta;
                }

                public override void StateEnter()
                {
                    base.StateEnter();
                    hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.Sprint);
                }

                public override StateStatus StateThink(float delta)
                {
                    base.StateThink(delta);

                    if (!hordeMember.Entity.HasTarget())
                        return StateStatus.Error;

                    float distanceToTarget = Vector3.Distance(hordeMember.Entity.currentTarget.transform.position, hordeMember.Entity.transform.position);

                    if (distanceToTarget < hordeMember.EngagementRange())
                        hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.SlowWalk);
                    else hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.Sprint);

                    if (Time.time > nextPositionUpdateTime)
                    {
                        Vector3 position;

                        if (hordeMember._attackEntity is BaseProjectile)
                        {
                            AIInformationZone informationZone = hordeMember.Entity.GetInformationZone(hordeMember.Entity.currentTarget.transform.position);
                            AIMovePoint bestMovePointNear = informationZone.GetBestMovePointNear(hordeMember.Entity.currentTarget.transform.position, hordeMember.Entity.transform.position, 0f, hordeMember.EngagementRange() * 0.75f, true, hordeMember.Entity, true);
                            if (!bestMovePointNear)
                            {
                                position = hordeMember.Entity.GetRandomPositionAround(hordeMember.Entity.currentTarget.transform.position, 0.5f, hordeMember.EngagementRange() * 0.75f);
                            }
                            else
                            {
                                bestMovePointNear.SetUsedBy(hordeMember.Entity, 5f);
                                position = bestMovePointNear.transform.position;
                                position = hordeMember.Entity.GetRandomPositionAround(position, 0f, bestMovePointNear.radius - 0.3f);
                            }
                        }
                        else position = hordeMember.Entity.currentTarget.transform.position;

                        hordeMember.Entity.SetDestination(position);

                        nextPositionUpdateTime = Time.time + 1f;
                    }
                    return StateStatus.Running;
                }
            }

            public class MemberCombatState : BasicAIState
            {
                private readonly HordeMember hordeMember;

                private float nextStrafeTime;

                public MemberCombatState(HordeMember hordeMember) : base(AIState.Combat)
                {
                    this.hordeMember = hordeMember;
                }

                public override float GetWeight()
                {
                    if (!hordeMember.Entity.HasTarget())                    
                        return 0f;

                    if (!hordeMember.TargetInRange())                    
                        return 0f;
                                        
                    float delta = (1f - Mathf.InverseLerp(hordeMember.GetIdealDistanceFromTarget(), hordeMember.EngagementRange(), hordeMember.Entity.DistanceToTarget())) * 0.5f;
                    
                    if (hordeMember.Entity.CanSeeTarget())
                        delta += 1f;
                              
                    return delta;
                }

                public override void StateEnter()
                {
                    base.StateEnter();
                    brain.mainInterestPoint = hordeMember.Entity.transform.position;
                    hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.Sprint);
                }

                public override void StateLeave()
                {
                    hordeMember.Entity.SetDucked(false);
                    base.StateLeave();
                }

                public override StateStatus StateThink(float delta)
                {
                    base.StateThink(delta);

                    if (!hordeMember.Entity.HasTarget())
                        return StateStatus.Error;

                    if (hordeMember._attackEntity is BaseProjectile)
                    {
                        if (Time.time > nextStrafeTime)
                        {
                            if (UnityEngine.Random.Range(0, 3) != 1)
                            {
                                nextStrafeTime = Time.time + UnityEngine.Random.Range(3f, 4f);
                                hordeMember.Entity.SetDucked(false);
                                hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.Walk);
                                hordeMember.Entity.SetDestination(hordeMember.Entity.GetRandomPositionAround(brain.mainInterestPoint, 1f, 2f));
                            }
                            else
                            {
                                nextStrafeTime = Time.time + UnityEngine.Random.Range(2f, 3f);
                                hordeMember.Entity.SetDucked(true);
                                hordeMember.Entity.Stop();
                            }
                        }
                    }
                    else if (hordeMember._attackEntity is BaseMelee)
                    {
                        hordeMember.Entity.nextTriggerTime = Time.time + 30f;

                        if (Vector3.Distance(hordeMember.Transform.position, hordeMember.Entity.currentTarget.transform.position) < hordeMember._attackEntity.effectiveRange)
                            hordeMember.DoMeleeAttack();
                        else
                        {
                            hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.Sprint);
                            hordeMember.Entity.SetDestination(hordeMember.Entity.currentTarget.transform.position);
                        }
                    }

                    return StateStatus.Running;
                }                
            }
            public class MemberCombatThrowState : BasicAIState
            {
                private readonly HordeMember hordeMember;

                public MemberCombatThrowState(HordeMember hordeMember) : base(AIState.CombatStationary)
                {
                    this.hordeMember = hordeMember;
                }

                public override float GetWeight()
                {
                    if (!hordeMember.Entity.HasTarget())
                        return 0f;

                    if (!hordeMember.TargetInThrowableRange() || hordeMember.TargetInRange())                    
                        return 0f;
                    
                    if (hordeMember.CanThrowWeapon() && UnityEngine.Random.value > 0.33f)
                        return 2f;

                    return 0f;
                }

                public override void StateEnter()
                {
                    base.StateEnter();
                    hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.SlowWalk);
                }

                public override StateStatus StateThink(float delta)
                {
                    base.StateThink(delta);

                    if (!hordeMember.Entity.HasTarget())
                        return StateStatus.Error;

                    if (hordeMember.TargetInThrowableRange() && hordeMember.CanThrowWeapon())
                    {
                        hordeMember.TryThrowWeapon();
                        return StateStatus.Running;
                    }

                    return StateStatus.Running;
                }
            }
        }
        #endregion

        #region Commands        
        [ChatCommand("horde")]
        private void cmdHorde(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "zombiehorde.admin"))
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
                    foreach (HordeManager hordeManager in HordeManager._allHordes)
                    {
                        player.SendConsoleCommand("ddraw.text", 30, Color.green, hordeManager.AverageLocation + new Vector3(0, 1.5f, 0), $"<size=20>Zombie Horde {hordeNumber}</size>");
                        memberCount += hordeManager.members.Count;
                        hordeNumber++;
                    }

                    SendReply(player, $"There are {HordeManager._allHordes.Count} active zombie hordes with a total of {memberCount} zombies");
                    return;
                case "destroy":
                    {
                        int number;
                        if (args.Length != 2 || !int.TryParse(args[1], out number))
                        {
                            SendReply(player, "You must specify a horde number");
                            return;
                        }

                        if (number < 0 || number >= HordeManager._allHordes.Count)
                        {
                            SendReply(player, "An invalid horde number has been specified");
                            return;
                        }

                        HordeManager._allHordes[number].Destroy(true, true);
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

                        if (number < 0 || number >= HordeManager._allHordes.Count)
                        {
                            SendReply(player, "An invalid horde number has been specified");
                            return;
                        }

                        player.Teleport(HordeManager._allHordes[number].AverageLocation);
                        SendReply(player, $"You have teleported to zombie horde {number}");
                        return;
                    }                
                case "create":
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
                    if (args.Length >= 3 && configData.HordeProfiles.ContainsKey(args[2]))
                        profile = args[2];

                    object success = FindPointOnNavmesh(player.transform.position, 5f);
                    if (success != null)
                    {
                        if (HordeManager.Create(new HordeManager.Order((Vector3)success, configData.Horde.InitialMemberCount, configData.Horde.MaximumMemberCount, distance, profile)))
                        {
                            if (distance > 0)
                                SendReply(player, $"You have created a zombie horde with a roam distance of {distance}");
                            else SendReply(player, "You have created a zombie horde");

                            return;
                        }
                    }

                    SendReply(player, "Invalid spawn position, move to another more open position. Unable to spawn horde");
                    return;

                case "createloadout":
                    ConfigData.MemberOptions.Loadout loadout = new ConfigData.MemberOptions.Loadout($"loadout-{configData.Member.Loadouts.Count}", DefaultDefinition);
                    
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

                    configData.Member.Loadouts.Add(loadout);
                    SaveConfig();

                    SendReply(player, "Saved your current inventory as a zombie loadout");
                    return;

                case "hordecount":
                    int hordes;
                    if (args.Length < 2 || !int.TryParse(args[1], out hordes))
                    {
                        SendReply(player, "You must enter a number");
                        return;
                    }

                    configData.Horde.MaximumHordes = hordes;

                    if (HordeManager._allHordes.Count < hordes)
                        CreateRandomHordes();
                    SaveConfig();
                    SendReply(player, $"Set maximum hordes to {hordes}");
                    return;

                case "membercount":
                    int members;
                    if (args.Length < 2 || !int.TryParse(args[1], out members))
                    {
                        SendReply(player, "You must enter a number");
                        return;
                    }

                    configData.Horde.MaximumMemberCount = members;
                    SaveConfig();
                    SendReply(player, $"Set maximum horde members to {members}");
                    return;
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
                if (!permission.UserHasPermission(arg.Connection.userid.ToString(), "zombiehorde.admin"))
                {
                    SendReply(arg, "You do not have permission to use this command");
                    return;
                }
            }

            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "horde info - Show position and information about active zombie hordes");
                SendReply(arg, "horde destroy <number> - Destroy the specified zombie horde");
                SendReply(arg, "horde create <opt:distance> - Create a new zombie horde at a random position, optionally specifying distance they can roam from the initial spawn point");
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
                    foreach (HordeManager hordeManager in HordeManager._allHordes)
                    {
                        memberCount += hordeManager.members.Count;
                        hordeNumber++;
                    }

                    SendReply(arg, $"There are {HordeManager._allHordes.Count} active zombie hordes with a total of {memberCount} zombies");
                    return;
                case "destroy":
                    int number;
                    if (arg.Args.Length != 2 || !int.TryParse(arg.Args[1], out number))
                    {
                        SendReply(arg, "You must specify a horde number");
                        return;
                    }

                    if (number < 1 || number > HordeManager._allHordes.Count)
                    {
                        SendReply(arg, "An invalid horde number has been specified");
                        return;
                    }

                    HordeManager._allHordes[number - 1].Destroy(true, true);
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
                    if (arg.Args.Length >= 3 && configData.HordeProfiles.ContainsKey(arg.Args[2]))
                        profile = arg.Args[2];

                    if (HordeManager.Create(new HordeManager.Order(GetSpawnPoint(), configData.Horde.InitialMemberCount, configData.Horde.MaximumMemberCount, distance, profile)))
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

                        ConfigData.MemberOptions.Loadout loadout = new ConfigData.MemberOptions.Loadout(kitname, DefaultDefinition);

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

                        configData.Member.Loadouts.Add(loadout);

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

                    configData.Horde.MaximumHordes = hordes;

                    if (HordeManager._allHordes.Count < hordes)
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

                    configData.Horde.MaximumMemberCount = members;
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
                HordeManager._allHordes.ForEach(x => memberCount += x.members.Count);
                cachedString = $"There are currently <color=#ce422b>{HordeManager._allHordes.Count}</color> hordes with a total of <color=#ce422b>{memberCount}</color> zombies";
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
        public enum SpawnSystem { None, Random, SpawnsDatabase }

        public enum SpawnState { Spawn, Despawn }


        internal static ConfigData configData;

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

                [JsonProperty(PropertyName = "Force all hordes to roam locally")]
                public bool LocalRoam { get; set; }

                [JsonProperty(PropertyName = "Local roam distance")]
                public float RoamDistance { get; set; }

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

                [JsonProperty(PropertyName = "Can be targeted by turrets set to peacekeeper mode")]
                public bool TargetedByPeaceKeeperTurrets { get; set; }

                [JsonProperty(PropertyName = "Can be targeted by Bradley APC")]
                public bool TargetedByAPC { get; set; }

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

                [JsonProperty(PropertyName = "Projectile weapon aimcone override (0 = disabled)")]
                public float AimconeOverride { get; set; }

                [JsonProperty(PropertyName = "Kill NPCs that are under water")]
                public bool KillUnderWater { get; set; }

                [JsonProperty(PropertyName = "Disable NPC dormant system. This will allow NPCs to move all the time, but at a cost to performance")]
                public bool DisableDormantSystem { get; set; }

                public List<Loadout> Loadouts { get; set; }

                public class Loadout
                {
                    public string LoadoutID { get; set; }

                    [JsonProperty(PropertyName = "Potential names for zombies using this loadout (chosen at random)")]
                    public string[] Names { get; set; }

                    [JsonProperty(PropertyName = "Damage multiplier")]
                    public float DamageMultiplier { get; set; }

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
                        [JsonProperty(PropertyName = "Movement speed (running)")]
                        public float RunSpeed { get; set; }

                        [JsonProperty(PropertyName = "Movement speed (walking)")]
                        public float WalkSpeed { get; set; }
                        
                        [JsonProperty(PropertyName = "Duck speed")]
                        public float DuckSpeed { get; set; }

                        public float Acceleration { get; set; }
                    }

                    public class SensoryStats
                    {
                        [JsonProperty(PropertyName = "Vision range")]
                        public float VisionRange { get; set; }        
                    }
                    
                    public Loadout() { }

                    public Loadout(string loadoutID, MurdererDefinition definition)
                    {
                        LoadoutID = loadoutID;

                        Names = new string[] { "Zombie" };

                        DamageMultiplier = 1f;

                        Vitals = new VitalStats() { Health = definition.Vitals.HP };

                        Movement = new MovementStats()
                        {
                            Acceleration = definition.Movement.Acceleration,
                            DuckSpeed = definition.Movement.DuckSpeed,
                            RunSpeed = definition.Movement.RunSpeed,
                            WalkSpeed = definition.Movement.WalkSpeed
                        };

                        Sensory = new SensoryStats()
                        {
                            VisionRange = definition.Sensory.VisionRange
                        };

                        BeltItems = new List<LootTable.InventoryItem>();
                        MainItems = new List<LootTable.InventoryItem>();
                        WearItems = new List<LootTable.InventoryItem>();
                    }
                }
            }

            public class LootTable
            {
                [JsonProperty(PropertyName = "Drop inventory on death instead of random loot")]
                public bool DropInventory { get; set; }

                [JsonProperty(PropertyName = "Random loot table")]
                public RandomLoot Random { get; set; }

                public class InventoryItem
                {
                    public string Shortname { get; set; }
                    public ulong SkinID { get; set; }
                    public int Amount { get; set; }

                    [JsonProperty(PropertyName = "Attachments", NullValueHandling = NullValueHandling.Ignore)]
                    public InventoryItem[] SubSpawn { get; set; }
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

                        [JsonProperty(PropertyName = "Spawn with")]
                        public LootDefinition Required { get; set; }

                        public int GetAmount()
                        {
                            if (Maximum <= 0f || Maximum <= Minimum)
                                return Minimum;

                            return UnityEngine.Random.Range(Minimum, Maximum);
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

                public class MonumentSettings : SpawnSettings
                {
                    [JsonProperty(PropertyName = "Enable spawns at this monument")]
                    public bool Enabled { get; set; }
                }
            }

            public class CustomSpawnPoints : SpawnSettings
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
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

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
                    LocalRoam = false,
                    RoamDistance = 150,
                    UseProfiles = false,
                    UseSenses = true
                },
                Member = new ConfigData.MemberOptions
                {
                    IgnoreSleepers = false,
                    TargetAnimals = false,
                    TargetedByTurrets = false,
                    TargetedByAPC = false,                    
                    TargetNPCs = true,
                    TargetHumanNPCs = false,
                    GiveGlowEyes = true,
                    HeadshotKills = true,
                    Loadouts = BuildDefaultLoadouts(),
                    AimconeOverride = 0,
                    KillUnderWater = true,
                    TargetedByPeaceKeeperTurrets = true,
                    DisableDormantSystem = true
                },
                Loot = new ConfigData.LootTable
                {
                    DropInventory = false,
                    Random = BuildDefaultLootTable(),
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
                },
                Version = Version
            };
        }

        private List<ConfigData.MemberOptions.Loadout> BuildDefaultLoadouts()
        {
            List<ConfigData.MemberOptions.Loadout> list = new List<ConfigData.MemberOptions.Loadout>();

            MurdererDefinition definition = DefaultDefinition;
            if (definition != null)
            {
                for (int i = 0; i < definition.loadouts.Length; i++)
                {
                    PlayerInventoryProperties inventoryProperties = definition.loadouts[i];

                    ConfigData.MemberOptions.Loadout loadout = new ConfigData.MemberOptions.Loadout($"loadout-{list.Count}", definition);

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

            MurdererDefinition definition = DefaultDefinition;
            if (definition != null)
            {
                for (int i = 0; i < definition.Loot.Length; i++)
                {
                    LootContainer.LootSpawnSlot lootSpawn = definition.Loot[i];

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

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Core.VersionNumber(0, 2, 0))
                configData = baseConfig;

            if (configData.Version < new Core.VersionNumber(0, 2, 1))
                configData.Loot.Random = baseConfig.Loot.Random;

            if (configData.Version < new Core.VersionNumber(0, 2, 2))
            {
                for (int i = 0; i < configData.Member.Loadouts.Count; i++)                
                    configData.Member.Loadouts[i].LoadoutID = $"loadout-{i}";

                configData.Horde.LocalRoam = false;
                configData.Horde.RoamDistance = 150;
                configData.Horde.UseProfiles = false;

                configData.HordeProfiles = baseConfig.HordeProfiles;

                configData.Monument.Airfield.Profile = string.Empty;
                configData.Monument.Dome.Profile = string.Empty;
                configData.Monument.GasStation.Profile = string.Empty;
                configData.Monument.HQMQuarry.Profile = string.Empty;
                configData.Monument.Junkyard.Profile = string.Empty;
                configData.Monument.LargeHarbor.Profile = string.Empty;
                configData.Monument.LaunchSite.Profile = string.Empty;
                configData.Monument.Powerplant.Profile = string.Empty;
                configData.Monument.Radtown.Profile = string.Empty;
                configData.Monument.Satellite.Profile = string.Empty;
                configData.Monument.SmallHarbor.Profile = string.Empty;
                configData.Monument.StoneQuarry.Profile = string.Empty;
                configData.Monument.SulfurQuarry.Profile = string.Empty;
                configData.Monument.Supermarket.Profile = string.Empty;
                configData.Monument.Trainyard.Profile = string.Empty;
                configData.Monument.Tunnels.Profile = string.Empty;
                configData.Monument.Warehouse.Profile = string.Empty;
                configData.Monument.WaterTreatment.Profile = string.Empty;
            }

            if (configData.Version < new Core.VersionNumber(0, 2, 5))
                configData.Member.AimconeOverride = 0f;

            if (configData.Version < new Core.VersionNumber(0, 2, 13))
                configData.TimedSpawns = baseConfig.TimedSpawns;

            if (configData.Version < new Core.VersionNumber(0, 2, 18))            
                configData.Member.TargetedByPeaceKeeperTurrets = configData.Member.TargetedByTurrets; 

            if (configData.Version < new Core.VersionNumber(0, 2, 30))
            {
                if (configData.Horde.SpawnType == "RandomSpawns" || configData.Horde.SpawnType == "Default")
                    configData.Horde.SpawnType = "Random";
            }

            if (configData.Version < new Core.VersionNumber(0, 2, 31))
            {
                if (string.IsNullOrEmpty(configData.Horde.SpawnType))
                    configData.Horde.SpawnType = "Random";

                configData.Member.DisableDormantSystem = true;
            }

            if (configData.Version < new Core.VersionNumber(0, 3, 0))
            {
                configData.Horde.UseSenses = true;
            }
            
            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion
    }
}
