using System.Collections.Generic;
using Newtonsoft.Json;
using CompanionServer.Handlers;
using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using Network;
using Rust;
using Rust.Modular;
using System.Collections;
using Newtonsoft.Json.Linq;
using Oxide.Game.Rust.Cui;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Convoy", "Adem", "1.1.5")]
    class Convoy : RustPlugin
    {
        [PluginReference] Plugin NpcSpawn, GUIAnnouncements, DiscordMessages;

        #region Variables
        const bool en = true;
        static Convoy ins;
        int pathCount = 0;

        bool active;
        bool round = true;
        int stopTime = 0;
        int destroyTime = 0;
        int eventTime = 0;
        bool destroying = false;
        bool failed = false;
        bool hackedCrate = false;

        List<Vector3> currentPath = new List<Vector3>();
        List<ConvoyVehicle> convoyVehicles = new List<ConvoyVehicle>();
        List<ScientistNPC> freeConvoyNpc = new List<ScientistNPC>();

        HashSet<BasePlayer> players = new HashSet<BasePlayer>();
        HashSet<BaseEntity> convoySummons = new HashSet<BaseEntity>();

        DoorCloser doorCloser;
        ConvoyModular convoyModular;
        ConvoyHeli convoyHeli;
        RootCar rootCar;

        HeliConfig heliConfig;
        BradleyConfig bradleyConfig;
        ModularConfig modularConfig;
        SedanConfig sedanConfig;

        Vector3 deathBradleyCoord = Vector3.zero;
        Vector3 deathHeliCoord = Vector3.zero;
        Vector3 deathModularCoord = Vector3.zero;

        Coroutine stopCoroutine;
        Coroutine eventCoroutine;
        Coroutine destroyCoroutine;
        #endregion Variables

        #region Hooks
        void Init()
        {
            ins = this;
            Unsubscribes();
            LoadData();
            Unsubscribe("OnLootSpawn");
        }

        void OnServerInitialized()
        {
            Subscribe("OnLootSpawn");
            LoadDefaultMessages();
            int vehicleCount = 0;

            foreach (ConvoySetting convoySetting in _config.convoys)
            {
                int count = convoySetting.firstBradleyCount + convoySetting.firstBradleyCount + 1 + convoySetting.endSedanCount + convoySetting.endSedanCount;
                if (count > vehicleCount) vehicleCount = count;
            }

            int rootCount = vehicleCount * _config.carDistance * 2 + vehicleCount + 20;

            int roadCount = _config.roadCount <= rootCount ? rootCount : _config.roadCount;

            if (_config.customRootName != "" && roots.ContainsKey(_config.customRootName))
            {
                List<List<string>> rootsPrefab = roots[_config.customRootName];
                List<List<string>> goodRoads = rootsPrefab.Where(x => x.Count() > roadCount).ToList();

                if (goodRoads.Count > 0)
                {
                    List<string> currentpathString = goodRoads.GetRandom();
                    foreach (string vectorString in currentpathString) currentPath.Add(vectorString.ToVector3());
                }
            }

            if (_config.rounRoadPriority && currentPath.Count == 0)
            {
                PathList pathList = TerrainMeta.Path.Roads.Where(x => x.Path.Points.Count() > roadCount && Vector3.Distance(x.Path.Points[0], x.Path.Points[x.Path.Points.Count() - 1]) < 10f && Vector3.Distance(x.Path.Points[0], x.Path.Points[x.Path.Points.Count() / 2]) > 50f).FirstOrDefault();
                if (pathList != null && pathList.Path != null && pathList.Path.Points != null) currentPath = pathList.Path.Points.ToList();
            }

            if (currentPath.Count == 0)
            {
                List<PathList> goodRoads = TerrainMeta.Path.Roads.Where(x => !_config.blockRoads.Contains(TerrainMeta.Path.Roads.IndexOf(x)) && x.Path.Points.Count() > roadCount && Vector3.Distance(x.Path.Points[0], x.Path.Points[x.Path.Points.Count() - 1]) > 100 && UnityEngine.Physics.RaycastAll(new Ray(x.Path.Points[10] + new Vector3(0, 1, 0), Vector3.down), 4f).Any(y => y.collider.name.Contains("Road Mesh")) && UnityEngine.Physics.RaycastAll(new Ray(x.Path.Points[x.Path.Points.Length / 2] + new Vector3(0, 1, 0), Vector3.down), 4f).Any(y => y.collider.name.Contains("Road Mesh"))).ToList();
                if (goodRoads.Count > 0)
                {
                    PathList path = goodRoads.GetRandom();
                    currentPath = path.Path.Points.ToList();
                    pathCount = currentPath.Count();
                }
            }

            pathCount = currentPath.Count();

            if (pathCount == 0)
            {
                PrintError("No road detected");
                NextTick(() => Server.Command($"o.unload {Name}"));
                return;
            }

            if (Vector3.Distance(currentPath[0], currentPath[currentPath.Count() - 1]) > 10f) round = false;

            timer.In(UnityEngine.Random.Range(_config.minStartTime, _config.minStartTime), () =>
            {
                if (!active) CreateConvoy();
                else Puts("This event is active now");
            });
        }

        void Unload()
        {
            if(active) DeleteConvoy(false);
            RootStop();
            if (rootCar != null) rootCar.basicCar.Kill();
            ins = null;
        }

        object OnEntityTakeDamage(BaseVehicleModule baseVehicleModule, HitInfo info)
        {
            if (baseVehicleModule == null || info == null) return null;
            BaseModularVehicle modularVehicle = baseVehicleModule.Vehicle;
            if (modularVehicle != null && convoyModular != null && !convoyModular.baseEntity.IsDestroyed && convoyModular.baseEntity == modularVehicle)
            {
                if (info.InitiatorPlayer != null)
                {
                    if (info.damageTypes.Has(DamageType.Explosion)) modularVehicle.health -= modularConfig.damageMultiplier * info.damageTypes.Total() / 10;
                    else modularVehicle.health -= modularConfig.damageMultiplier * info.damageTypes.Total() / 5;
                    if (!modularVehicle.IsDestroyed && modularVehicle.health <= 0) modularVehicle.Kill();
                    else ConvoyTakeDamage(modularVehicle, info);
                }
                return true;
            }
            return null;
        }

        object OnEntityTakeDamage(ModularCar entity, HitInfo info)
        {
            if (convoyModular != null && entity == convoyModular.baseEntity) info.damageTypes.ScaleAll(modularConfig.damageMultiplier);
            return ConvoyTakeDamage(entity, info);
        }

        object OnEntityTakeDamage(BasicCar entity, HitInfo info)
        {
            if (info.InitiatorPlayer != null && convoySummons.Contains(entity))
            {
                if (!info.damageTypes.Has(DamageType.Explosion) && info.damageTypes.GetMajorityDamageType() == DamageType.Bullet) info.damageTypes.ScaleAll(250f);
                else if (info.damageTypes.GetMajorityDamageType() == DamageType.Explosion && info.WeaponPrefab != null && info.WeaponPrefab.name == "rocket_hv") info.damageTypes.ScaleAll(10f);
                return ConvoyTakeDamage(entity, info);
            }
            return null;
        }

        object OnEntityTakeDamage(BradleyAPC entity, HitInfo info) { return ConvoyTakeDamage(entity, info); }

        object OnEntityTakeDamage(ScientistNPC entity, HitInfo info)
        {
            if (info == null || entity == null) return null;
            if ((info.InitiatorPlayer == null || !info.InitiatorPlayer.userID.IsSteamId()) && convoyVehicles.Count > 0 && convoyVehicles.Any(x => x.roamNpc.Contains(entity))) return true;
            ConvoyVehicle convoyVehicle = convoyVehicles.Where(x => x.scientists.Contains(entity)).FirstOrDefault();
            if (convoyVehicle != null) StopConvoy();
            if (convoyVehicles.Any(x => x.driver == entity)) return true;
            return null;
        }

        void OnEntityTakeDamage(BaseHelicopter entity, HitInfo info)
        {
            if (convoyHeli == null || info == null || entity == null || convoyHeli.baseHelicopter != entity) return;
            if (info.InitiatorPlayer != null) StopConvoy();
        }

        void OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || info.Initiator == null) return;
            BradleyAPC bradleyAPC = info.Initiator as BradleyAPC;
            if (bradleyAPC != null && convoyVehicles.Any(x => x.baseEntity == bradleyAPC))
            {
                if (!player.userID.IsSteamId()) info.damageTypes.ScaleAll(0);
                else info.damageTypes.ScaleAll(bradleyConfig.scaleDamage);
            }
        }

        void OnEntityKill(ModularCar entity)
        {
            if (convoyModular == null || entity != convoyModular.baseEntity) return;
            failed = true;
            if (convoyVehicles.Contains(convoyModular)) convoyVehicles.Remove(convoyModular);
            StopConvoy();
            deathModularCoord = entity.transform.position;
            if (!destroying && destroyCoroutine == null)
            {
                destroying = true;
                AlertToAllPlayers("Failed", _config.prefics);
                AlertToAllPlayers("PreFinish", _config.prefics, _config.preFinishTime);
                destroyCoroutine = ServerMgr.Instance.StartCoroutine(DestroyCounter());
            }
        }

        void OnEntityKill(BradleyAPC entity) => ConvoyVehicleDie(entity);

        void OnEntityKill(BasicCar entity) => ConvoyVehicleDie(entity);

        void OnEntityKill(BaseHelicopter entity)
        {
            if (entity == null || convoyHeli == null || convoyHeli.baseHelicopter != entity) return;
            deathHeliCoord = entity.transform.position;
            ConvoyVehicleDie(entity);
        }

        void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (player == null) return;

            if (player.userID.IsSteamId() && players.Contains(player))
            {
                players.Remove(player);
                if (_config.GUI.IsGUI) CuiHelper.DestroyUi(player, "TextMain");
            }

            else if (_config.blockSpawnDieNpc && info != null && info.Initiator != null && player is ScientistNPC)
            {
                ConvoyVehicle convoyVehicle = convoyVehicles.FirstOrDefault(x => x.roamNpc.Contains(player));
                if (convoyVehicle != null) convoyVehicle.NpcDie(player as ScientistNPC);
            }
        }

        object OnVehicleModulesAssign(ModularCar car)
        {
            if (car.GetComponent<ConvoyModular>() == null) return null;
            return false;
        }

        object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (crate == null || convoyModular == null || crate != convoyModular.crate) return null;
            else if (convoySummons.Count == 0 || modularConfig.innidiatlyOpen)
            {
                Unsubscribe("CanHackCrate");
                AlertToAllPlayers("StartHackCrate", _config.prefics, player.displayName);
                if (destroying) destroyTime += (int)modularConfig.crateUnlockTime + 10;
                hackedCrate = true;
                timer.In(modularConfig.crateUnlockTime, () =>
                {
                    if (destroyCoroutine == null && !destroying)
                    {
                        destroying = true;
                        destroyCoroutine = ServerMgr.Instance.StartCoroutine(DestroyCounter());
                        AlertToAllPlayers("PreFinish", _config.prefics, _config.preFinishTime);
                    }
                });

                return null;
            }
            else
            {
                Alert(player, GetMessage("CantHackCrate", player.UserIDString, _config.prefics));
                return true;
            }
        }

        void OnEntitySpawned(HelicopterDebris entity) => NextTick(() => { if (entity != null && !entity.IsDestroyed && deathBradleyCoord != null && (Vector3.Distance(entity.transform.position, deathBradleyCoord) < 20f || Vector3.Distance(entity.transform.position, deathHeliCoord) < 20f)) entity.Kill(); });

        void OnEntitySpawned(DroppedItemContainer entity)
        {
            if (entity != null && entity.PrefabName == "assets/prefabs/misc/item drop/item_drop.prefab" && deathModularCoord != Vector3.zero) NextTick(() => { if (entity.Distance(deathModularCoord) < 5f) entity.Kill(); });
        }

        void OnLootSpawn(LootContainer entity)
        {
            if (entity != null && barrels.Contains(entity.ShortPrefabName) && !entity.IsDestroyed && UnityEngine.Physics.RaycastAll(new Ray(entity.transform.position + new Vector3(0, 1, 0), Vector3.down), 4f).Any(x => x.collider.name.Contains("Road Mesh"))) entity.Kill();
        }

        object CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            BaseEntity parrent = entity.VehicleParent();
            if (parrent != null && player.userID.IsSteamId() && convoyVehicles.Any(x => x.baseEntity == parrent)) return true;
            return null;
        }

        object OnHelicopterRetire(PatrolHelicopterAI ai)
        {
            if (convoyHeli != null && convoyHeli.patrolHelicopterAI == ai) return true;
            return null;
        }

        object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player)
        {
            if (convoyHeli != null && heli != null && heli == convoyHeli.patrolHelicopterAI && ((_config.blockFirstAttack && stopTime == 0 && !failed && !hackedCrate) || player == null || !player.userID.IsSteamId())) return false;
            return null;
        }

        object CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            if (apc != null && convoySummons.Count > 0 && convoySummons.Contains(apc))
            {
                if (_config.blockFirstAttack && stopTime == 0 && !failed && !hackedCrate) return false;
                BasePlayer player = entity as BasePlayer;
                if (player == null || !player.userID.IsSteamId()) return false;
            }
            return null;
        }
        #endregion Hooks

        #region Commands
        [ChatCommand("convoystart")]
        void StartCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;
            if (active)
            {
                Alert(player, GetMessage("EventActive", player.UserIDString, _config.prefics));
                return;
            }
            if (arg != null && arg.Length >= 1) CreateConvoy(arg[0]);
            else CreateConvoy();
        }

        [ChatCommand("convoystop")]
        void StopCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;
            if (active) DeleteConvoy(true);
        }

        [ConsoleCommand("convoystart")]
        void ConsoleStartCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) return;
            if (arg.Args != null && arg.Args.Count() > 0) CreateConvoy(arg.Args[0]);
            CreateConvoy();
        }

        [ConsoleCommand("convoystop")]
        void ConsoleStopCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null && active) DeleteConvoy(true);
        }

        [ChatCommand("convoyrootstart")]
        void RootStartCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin || player.isInAir) return;
            CreateRootCar(player);
        }

        [ChatCommand("convoyrootstop")]
        void RootStopCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin || rootCar == null) return;
            RootStop();
        }

        [ChatCommand("convoyrootsave")]
        void RootSaveCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin || rootCar == null) return;

            if (arg == null || arg.Count() == 0)
            {
                Alert(player, $"{_config.prefics} To save the route, use the command: <color=#738d43>convoyrootsave [rootpresetname]</color>");
                return;
            }

            if (rootCar.root.Count() < 50) Alert(player, $"{_config.prefics} The route is too short!");
            else
            {
                List<string> root = new List<string>();
                foreach (Vector3 vector in rootCar.root) root.Add(vector.ToString());
                if (!roots.ContainsKey(arg[0])) roots.Add(arg[0], new List<List<string>>());
                roots[arg[0]].Add(root);
                SaveData();
                RootStop();
                Alert(player, $"{_config.prefics} Route added to group <color=#738d43>{arg[0]}</color>");
            }
        }

        [ChatCommand("convoyroadblock")]
        void RoadBlockCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin || player.isInAir) return;

            PathList blockRoad = TerrainMeta.Path.Roads.FirstOrDefault(x => x.Path.Points.Any(y => Vector3.Distance(player.transform.position, y) < 10));
            if (blockRoad == null) Alert(player, $"{_config.prefics} Road not found <color=#ce3f27>not found</color>"); 
            int index = TerrainMeta.Path.Roads.IndexOf(blockRoad);
            if (_config.blockRoads.Contains(index)) Alert(player, $"{_config.prefics} The road is already <color=#ce3f27>blocked</color>");
            else if (blockRoad != null)
            {
                _config.blockRoads.Add(index);
                SaveConfig();
                Alert(player, $"{_config.prefics} The road with the index <color=#738d43>{index}</color> is <color=#ce3f27>blocked</color>");
            }
        }
        #endregion Commands

        #region Method
        void CreateConvoy(string presetName = "")
        {
            if (active)
            {
                Puts("This event is active now. To finish this event (convoystop), then to start the next one");
                return;
            }

            if (!plugins.Exists("NpcSpawn"))
            {
                PrintError("NpcSpawn plugin doesn`t exist! Please read the file ReadMe.txt");
                Server.Command($"o.unload {Name}");
                return;
            }

            destroyTime = ins._config.preFinishTime;
            ConvoySetting convoySetting = null;

            if (presetName != "") convoySetting = _config.convoys.Where(x => x.name == presetName).FirstOrDefault();
            else if (_config.convoys.Any(x => x.chance > 0))
            {
                while (convoySetting == null)
                {
                    foreach (ConvoySetting setting in _config.convoys)
                    {
                        if (UnityEngine.Random.Range(0.0f, 100.0f) <= setting.chance) convoySetting = setting;
                    }
                }
            }
            
            if (convoySetting == null)
            {
                PrintError("Event configuration not found!");
                Server.Command($"o.reload {Name}");
                return;
            }

            AlertToAllPlayers("PreStart", _config.prefics, _config.preStartTime);

            active = true;

            timer.In(_config.preStartTime, () =>
            {
                if (!active) return;

                Subscribes();
                bradleyConfig = _config.bradleyConfiguration.Where(x => x.presetName == convoySetting.bradleyConfigurationName).FirstOrDefault();
                sedanConfig = _config.sedanConfiguration.Where(x => x.presetName == convoySetting.sedanConfigurationName).FirstOrDefault();
                modularConfig = _config.modularConfiguration.Where(x => x.presetName == convoySetting.modularConfigurationName).FirstOrDefault();
                heliConfig = _config.heliesConfiguration.Where(x => x.presetName == convoySetting.heliConfigurationName).FirstOrDefault();

                int totalVehicleCount = convoySetting.firstBradleyCount + convoySetting.firstSedanCount + convoySetting.endSedanCount + convoySetting.endBradleyCount;

                int startPoint = round ? UnityEngine.Random.Range(0, pathCount) : 10;

                int count = 0;
                int totalCount = convoySetting.endBradleyCount;

                int delataPoint = _config.carDistance;

                int cycleCount = 0;
                while (cycleCount < 25 && Vector3.Distance(currentPath[0], currentPath[delataPoint]) < 10)
                {
                    delataPoint += 2;
                    cycleCount++;
                }

                for (; count < totalCount; count++)
                {
                    int firstpoint = 0, secondpoint = 0;
                    DefineNextPathPoint(startPoint, pathCount, out firstpoint, out secondpoint);
                    CreateBradley(firstpoint, secondpoint);
                    startPoint += delataPoint;
                }

                totalCount += convoySetting.endSedanCount;

                for (; count < totalCount; count++)
                {
                    int firstpoint = 0, secondpoint = 0;
                    DefineNextPathPoint(startPoint, pathCount, out firstpoint, out secondpoint);
                    CreateSedan(firstpoint, secondpoint);
                    startPoint += delataPoint;
                }

                totalCount++;

                for (; count < totalCount; count++)
                {
                    int firstpoint = 0, secondpoint = 0;
                    DefineNextPathPoint(startPoint, pathCount, out firstpoint, out secondpoint);
                    CreateModular(firstpoint, secondpoint);
                    startPoint += delataPoint;
                }

                totalCount += convoySetting.firstSedanCount;

                for (; count < totalCount; count++)
                {
                    int firstpoint = 0, secondpoint = 0;
                    DefineNextPathPoint(startPoint, pathCount, out firstpoint, out secondpoint);
                    CreateSedan(firstpoint, secondpoint);
                    startPoint += delataPoint;
                }

                totalCount += convoySetting.firstBradleyCount;

                for (; count < totalCount; count++)
                {
                    int firstpoint = 0, secondpoint = 0;
                    DefineNextPathPoint(startPoint, pathCount, out firstpoint, out secondpoint);
                    CreateBradley(firstpoint, secondpoint);
                    startPoint += delataPoint;
                }

                if (convoySetting.heliOn && convoyVehicles.Count > 0) CreateHelicopter();

                convoyVehicles.Reverse();
                AlertToAllPlayers("EventStart", _config.prefics);

                if (eventCoroutine != null) ServerMgr.Instance.StopCoroutine(eventCoroutine);
                eventCoroutine = ServerMgr.Instance.StartCoroutine(EventCounter());

                Puts("The event has begun");
            });
        }

        void DefineNextPathPoint(int point, int pathCount, out int firstPoint, out int endPoint)
        {
            if (point > pathCount - 1)
            {
                if(round) firstPoint = point - pathCount;
                else
                {
                    PrintError("Insufficient route length!");
                    DeleteConvoy(true);
                    firstPoint = endPoint = 0;
                }
            }
            else firstPoint = point;

            int endpointClone = firstPoint++;
            if (endpointClone > pathCount - 1)
            {
                if(round) endPoint = endpointClone - pathCount;
                else
                {
                    PrintError("Insufficient route length!");
                    DeleteConvoy(true);
                    firstPoint = endPoint = 0;
                }
            }
            else endPoint = endpointClone;
        }

        void ReverseConvoy()
        {
            StopConvoy(null, 10);
            currentPath.Reverse();
            convoyVehicles.Reverse();
            foreach (ConvoyVehicle convoyVehicle in convoyVehicles)
            {
                if (convoyVehicle == null) continue;
                Transform transform = convoyVehicle.baseEntity.transform;
                transform.RotateAround(transform.position, transform.up, 180);
                convoyVehicle.Rotate();
                convoyVehicle.DefineFollowEntity();
            }

        }

        void DeleteConvoy(bool unload = false)
        {
            Unsubscribes();
            active = false;
            if (doorCloser != null && !doorCloser.IsDestroyed) doorCloser.Kill();
            foreach (BasePlayer player in ins.players) CuiHelper.DestroyUi(player, "TextMain");
            players.Clear();
            if (active) AlertToAllPlayers("Finish", _config.prefics);
            destroying = false;
            foreach (ScientistNPC scientist in freeConvoyNpc)
            {
                if (scientist != null && !scientist.IsDestroyed) scientist.Kill();
            }
            
            if (convoyVehicles.Count > 0) foreach (ConvoyVehicle convoyVehicle in convoyVehicles) { if (convoyVehicle != null && convoyVehicle.baseEntity != null && !convoyVehicle.baseEntity.IsDestroyed && convoyVehicle is ConvoyModular == false) convoyVehicle.baseEntity.Kill(); }
            if (convoyHeli != null && convoyHeli.baseHelicopter != null && !convoyHeli.baseHelicopter.IsDestroyed) convoyHeli.baseHelicopter.Kill();
            convoyVehicles.Clear();
            convoySummons.Clear();
            Puts("The event is over");

            if (stopCoroutine != null) ServerMgr.Instance.StopCoroutine(stopCoroutine);
            if (eventCoroutine != null) ServerMgr.Instance.StopCoroutine(eventCoroutine);
            if (destroyCoroutine != null) ServerMgr.Instance.StopCoroutine(destroyCoroutine);

            if (convoyModular != null && convoyModular.baseEntity != null && !convoyModular.baseEntity.IsDestroyed) convoyModular.baseEntity.Kill();
            if (unload) Server.Command($"o.reload {Name}");
        }

        void ConvoyVehicleDie(BaseEntity entity)
        {
            if (entity == null) return;
            ConvoyVehicle convoyVehicle = convoyVehicles.Where(x => x.baseEntity == entity).FirstOrDefault();
            if (convoyVehicle != null)
            {
                if (convoyVehicle is ConvoyBradley) deathBradleyCoord = entity.transform.position;
                DefineFollow(entity);
            }
            if (convoySummons.Count > 0 && convoySummons.Contains(entity))
            {
                convoySummons.Remove(entity);
                if (convoySummons.Count == 0 && convoyModular != null)
                {
                    convoyModular.StopMoving(false, true);
                    AlertToAllPlayers("SecurityKill", _config.prefics);
                }
            }
        }

        object ConvoyTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.InitiatorPlayer == null || !convoyVehicles.Any(x => x.baseEntity == entity)) return null;
            BasePlayer initiator = info.InitiatorPlayer;
            if (!initiator.userID.IsSteamId()) return true;
            if (info.ProjectileDistance <= 100f) StopConvoy(initiator);
            return null;
        }

        void StopConvoy(BasePlayer initiator = null, int time = 0)
        {
            stopTime = time > 0 ? time : _config.damamageStopTime;
            if (stopCoroutine != null && stopTime != 0) return;
            stopCoroutine = ServerMgr.Instance.StartCoroutine(StopCounter());
            foreach (ConvoyVehicle convoyVehicle in convoyVehicles) if (convoyVehicle != null) convoyVehicle.StopMoving(true, true);
            if (initiator != null && convoyHeli != null) convoyHeli.SetTarget(initiator);
            if (convoyModular != null) CreateEventZone(convoyModular.baseEntity.transform.position - new Vector3(0f, 0.5f, 0f));
        }

        void StartConvoy()
        {
            if (failed || convoyModular == null || convoyModular.crate == null || convoyModular.crate.IsFullyHacked() || convoyModular.crate.IsBeingHacked()) return;
            if (stopCoroutine != null) ServerMgr.Instance.StopCoroutine(stopCoroutine);
            foreach (ScientistNPC scientist in freeConvoyNpc) if (scientist != null && !scientist.IsDestroyed) scientist.Kill();
            freeConvoyNpc.Clear();
            stopCoroutine = null;
            stopTime = 0;
            foreach (ConvoyVehicle convoyVehicle in convoyVehicles) convoyVehicle.StartMoving();
            if (doorCloser != null && !doorCloser.IsDestroyed) doorCloser.Kill();
        }

        void CreateEventZone(Vector3 position)
        {
            if (doorCloser != null && !doorCloser.IsDestroyed) doorCloser.Kill();
            if (convoyModular == null || convoyModular.baseEntity.IsDestroyed) return;
            doorCloser = GameManager.server.CreateEntity("assets/prefabs/misc/doorcloser/doorcloser.prefab", position) as DoorCloser;
            doorCloser.gameObject.AddComponent<ZoneController>();
        }

        void CreateBradley(int firstPoint, int secondPoint)
        {
            Vector3 vector3 = currentPath[firstPoint];
            ChechTrash(vector3);
            BradleyAPC bradley = GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", vector3, Quaternion.LookRotation(currentPath[firstPoint] - currentPath[secondPoint])) as BradleyAPC;
            bradley.Spawn();
            bradley.ClearPath();
            bradley.currentPath = currentPath;
            bradley.currentPathIndex = firstPoint;
            ConvoyBradley convoyBradley = bradley.gameObject.AddComponent<ConvoyBradley>();
            convoyVehicles.Add(convoyBradley);
            convoyBradley.InitBradley();
            convoySummons.Add(bradley);
        }

        void CreateSedan(int firstPoint, int secondPoint)
        {
            Vector3 vector3 = currentPath[firstPoint];
            ChechTrash(vector3);
            BasicCar car = GameManager.server.CreateEntity("assets/content/vehicles/sedan_a/sedantest.entity.prefab", vector3, Quaternion.LookRotation(currentPath[firstPoint] - currentPath[secondPoint])) as BasicCar;
            car.enableSaving = false;
            car.Spawn();
            ConvoyCar convoyCar = car.gameObject.AddComponent<ConvoyCar>();
            convoyCar.currentPoint = firstPoint;
            convoyCar.InitSedan();
            convoyVehicles.Add(convoyCar);
            convoySummons.Add(car);
        }

        void CreateModular(int firstPoint, int secondPoint)
        {
            Vector3 vector3 = currentPath[firstPoint];
            ChechTrash(vector3);
            ModularCar car = GameManager.server.CreateEntity("assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab", vector3, Quaternion.LookRotation(currentPath[firstPoint] - currentPath[secondPoint])) as ModularCar;
            car.enableSaving = false;
            ConvoyModular modular = car.gameObject.AddComponent<ConvoyModular>();
            car.Spawn();
            modular.currentPoint = firstPoint;
            modular.InitModular();
            convoyVehicles.Add(modular);
            convoyModular = modular;
        }

        void CreateHelicopter()
        {
            Vector3 position = convoyModular.baseEntity.transform.position + new Vector3(0, heliConfig.height, 0);
            Quaternion rotation = convoyModular.baseEntity.transform.rotation;
            BaseHelicopter heli = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", position, rotation) as BaseHelicopter;
            heli.enableSaving = false;
            heli.Spawn();
            heli.transform.position = position;
            convoyHeli = heli.gameObject.AddComponent<ConvoyHeli>();
            heli._maxHealth = heliConfig.hp;
            heli.startHealth = heliConfig.hp;
            convoyHeli.InitHelicopter(heliConfig.hp);
            convoySummons.Add(heli);
        }

        void DefineFollow(BaseEntity entity)
        {
            ConvoyVehicle convoyVehicle = convoyVehicles.Where(x => x.baseEntity.net.ID == entity.net.ID).FirstOrDefault();
            if (convoyVehicle == null) return;
            int index = convoyVehicles.IndexOf(convoyVehicle);
            index++;
            if (index >= convoyVehicles.Count()) return;
            ConvoyVehicle nextVehicle = convoyVehicles[index];
            if (nextVehicle == null) return;
            BaseEntity baseEntity = nextVehicle.baseEntity;
            if (baseEntity == null || baseEntity.IsDestroyed) return;
            convoyVehicles.Remove(convoyVehicle);
            nextVehicle.DefineFollowEntity();
        }

        void RootStop()
        {
            if (rootCar != null && !rootCar.basicCar.IsDestroyed) rootCar.basicCar.Kill();
        }

        void CreateRootCar(BasePlayer player)
        {
            if (rootCar != null)
            {
                Alert(player, $"{_config.prefics} The route is <color=#738d43>already</color> being recorded!");
                return;
            }
            Alert(player, $"{_config.prefics} To build a route, drive a car along it and write to the chat: <color=#738d43>convoyrootsave [rootgroupname]</color>\nTo reset the route, print to the chat: <color=#738d43>convoyrootstop</color>");
            BasicCar car = GameManager.server.CreateEntity("assets/content/vehicles/sedan_a/sedantest.entity.prefab", player.transform.position + new Vector3(0, 0.3f, 0), player.eyes.GetLookRotation()) as BasicCar;
            car.enableSaving = false;
            car.Spawn();
            rootCar = car.gameObject.AddComponent<RootCar>();
            rootCar.InitSedan(player);

            BaseVehicle.MountPointInfo mountPointInfo = car.mountPoints[0];
            player.MountObject(mountPointInfo.mountable);
            mountPointInfo.mountable.MountPlayer(player);
        }

        IEnumerator EventCounter()
        {
            while (eventTime < _config.eventTime && !destroying && active)
            {
                eventTime++;
                yield return CoroutineEx.waitForSeconds(1f);
            }
            if (!destroying && destroyCoroutine == null)
            {
                destroying = true;
                AlertToAllPlayers("PreFinish", _config.prefics, _config.preFinishTime);
                destroyCoroutine = ServerMgr.Instance.StartCoroutine(DestroyCounter());
            }
        }

        IEnumerator StopCounter()
        {
            while (stopTime > 0)
            {
                stopTime--;
                yield return CoroutineEx.waitForSeconds(1f);
            }
            stopTime = 0;
            if(active) StartConvoy();
        }

        IEnumerator DestroyCounter()
        {
            while (destroyTime > 0)
            {
                destroyTime--;
                yield return CoroutineEx.waitForSeconds(1f);
            }
            destroyTime = 0;
            if(active) DeleteConvoy(true);
        }
        #endregion Method

        #region Classes 
        class ZoneController : FacepunchBehaviour
        {
            DoorCloser mainCloser;
            SphereCollider sphereCollider;
            HashSet<BaseEntity> spheres = new HashSet<BaseEntity>();

            void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = 70f;
                mainCloser = GetComponent<DoorCloser>();
                if (ins._config.GUI.IsGUI) InvokeRepeating(UpdateGui, 1f, 1f);
                if (ins._config.eventZone.isDome) CreateSphere();
            }

            void CreateSphere()
            {
                for (int i = 0; i < ins._config.eventZone.darkening; i++)
                {
                    BaseEntity sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", mainCloser.transform.position);
                    SphereEntity entity = sphere.GetComponent<SphereEntity>();
                    entity.currentRadius = 70f * 2;
                    entity.lerpSpeed = 0f;
                    sphere.enableSaving = false;
                    sphere.Spawn();
                    spheres.Add(sphere);
                }
            }

            void UpdateGui()
            {
                float time = ins._config.eventTime - ins.eventTime;
                if (ins.destroying) time = ins.destroyTime;
                foreach (BasePlayer player in ins.players) ins.MessageGUI(player, ins.GetMessage("GUI", player.UserIDString, time));
            }

            void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player != null && player.userID.IsSteamId())
                {
                    ins.players.Add(player);
                    if (ins._config.GUI.IsGUI) ins.MessageGUI(player, ins.GetMessage("GUI", player.UserIDString, ins._config.eventTime - ins.eventTime + ins.destroyTime));
                    if (ins._config.eventZone.isCreateZonePVP) ins.Alert(player, ins.GetMessage("EnterPVP", player.UserIDString, ins._config.prefics));
                }
            }

            void OnTriggerExit(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player != null && player.userID.IsSteamId())
                {
                    ins.players.Remove(player);
                    if (ins._config.GUI.IsGUI) CuiHelper.DestroyUi(player, "TextMain");
                    if (ins._config.eventZone.isCreateZonePVP) ins.Alert(player, ins.GetMessage("ExitPVP", player.UserIDString, ins._config.prefics));
                }
            }

            void OnDestroy()
            {
                foreach (BaseEntity sphere in spheres) if (sphere != null && !sphere.IsDestroyed) sphere.Kill();
                if (ins._config.GUI.IsGUI)
                {
                    CancelInvoke(UpdateGui);
                    foreach (BasePlayer player in ins.players) CuiHelper.DestroyUi(player, "TextMain");
                }
                ins.players.Clear();
            }
        }

        class ConvoyVehicle : FacepunchBehaviour
        {
            List<CoordConfig> coordNPC = new List<CoordConfig>();
            List<BaseVehicle.MountPointInfo> baseMountables = new List<BaseVehicle.MountPointInfo>();

            internal ConvoyVehicle followVehicle;
            internal Rigidbody rigidbody;
            internal BaseEntity baseEntity;
            internal ScientistNPC driver;
            internal bool stop = true;
            internal bool allConvoyStop = true;
            int countDieNpc = 0;
            internal List<ScientistNPC> scientists = new List<ScientistNPC>();
            internal List<ScientistNPC> roamNpc = new List<ScientistNPC>();
            void Awake()
            {
                baseEntity = GetComponent<BaseEntity>();
                if (baseEntity is BradleyAPC) coordNPC = ins.bradleyConfig.coordinates;
                else if (baseEntity is ModularCar) coordNPC = ins.modularConfig.coordinates;
                else if (baseEntity is BasicCar) coordNPC = ins.sedanConfig.coordinates;
                Invoke(InitVehicle, 0.5f);
            }

            void InitVehicle()
            {
                rigidbody = baseEntity.gameObject.GetComponent<Rigidbody>();
                rigidbody.mass = 3500;
                rigidbody.centerOfMass = new Vector3(0, -0.2f, 0);
                rigidbody.isKinematic = true;
                DefineMountPoints();
                StartMoving();
                DefineFollowEntity();
                if (!ins.round) InvokeRepeating(CheckRotate, 0.5f, 0.1f);
                if (ins._config.deleteBarriers) InvokeRepeating(CheckBarriers, 3f, 3f);
            }

            internal virtual void Rotate() { }

            internal virtual void CheckRotate() { }

            internal virtual int GetCurrentPointIndex() { return 0; }

            internal virtual void CheckBarriers()
            {
                if (followVehicle != null) return;

                foreach (Collider collider in UnityEngine.Physics.OverlapSphere(baseEntity.transform.position + baseEntity.transform.forward * 4f, 3f))
                {
                    BaseEntity entity = collider.ToBaseEntity();
                    if (entity != null && !entity.IsDestroyed && ins._config.barriers.Contains(entity.ShortPrefabName) && !ins.convoyVehicles.Any(x => x.baseEntity == entity))
                    {
                        if (entity is BaseVehicle)
                        {
                            BaseVehicle vehicle = entity as BaseVehicle;
                            if (!vehicle.mountPoints.Any(x => x.mountable.GetMounted() != null)) entity.Kill();
                        }
                        else entity.Kill();
                    }
                }
            }

            internal virtual void OnDestroy()
            {
                CancelInvoke(CheckRotate);
                KillScientists(true);
                Destroy(this);
            }

            #region Moving
            internal void DefineFollowEntity()
            {
                int index = ins.convoyVehicles.IndexOf(this);
                if (index == 0) followVehicle = null;
                else followVehicle = ins.convoyVehicles[--index];
            }

            internal void StopMoving(bool NPC = true, bool allConvoyStop = false)
            {
                this.allConvoyStop = allConvoyStop;
                CancelInvoke(BreakOff);
                stop = true;
                BreakOn();
                if (NPC && ins.active) Invoke(CreateRoamNpc, 0.1f);
            }

            internal void StartMoving(bool delay = true)
            {
                foreach (ScientistNPC scientist in roamNpc) if (scientist != null && !scientist.IsDestroyed) scientist.Kill();
                roamNpc.Clear();

                if (allConvoyStop && ins.active) CreatePassengers();
                if (delay && ins.active) Invoke(BreakOff, 7f);
                else BreakOff();
            }

            internal void BreakOff()
            {
                allConvoyStop = false;
                rigidbody.isKinematic = false;
                stop = false;
            }

            void BreakOn() => rigidbody.isKinematic = true;
            #endregion Moving

            #region NPC
            internal void NpcDie(ScientistNPC scientistNPC)
            {
                if (!ins.active) return;
                roamNpc.Remove(scientistNPC);
                countDieNpc++;
            }

            void DefineMountPoints()
            {
                BaseVehicle baseVehicle = baseEntity.gameObject.GetComponent<BaseVehicle>();
                if (baseVehicle != null) baseMountables = baseVehicle.allMountPoints.ToList();
            }

            void CreatePassengers()
            {
                int count = baseMountables.Count();
                if (count == 0) return;
                count -= countDieNpc;
                if (count == 0) count = 1;
                driver = null;
                scientists.Clear();

                for (int i = 0; i < count; i++)
                {
                    BaseVehicle.MountPointInfo mountPointInfo = baseMountables[i];
                    BaseMountable baseMountable = mountPointInfo.mountable;
                    if (baseMountable == null) continue;
                    ScientistNPC scientist = CreateNpc(mountPointInfo.isDriver, Vector3.zero, Vector3.forward, true);
                    Invoke(() =>
                    {
                        scientist.MountObject(baseMountable);
                        baseMountable.MountPlayer(scientist);
                        if (mountPointInfo.isDriver) driver = scientist;
                        scientists.Add(scientist);
                    }, 1f);
                }
            }

            void CreateRoamNpc()
            {
                KillScientists();
                int count = coordNPC.Count() - countDieNpc;
                if (count <= 0) return;

                for (int i = 0; i < count; i++)
                {
                    CoordConfig location = coordNPC[i];
                    ScientistNPC scientist = CreateNpc(false, location.position.ToVector3(), location.rotation.ToVector3(), false);
                    if (scientist != null) roamNpc.Add(scientist);
                }
            }

            ScientistNPC CreateNpc(bool driver, Vector3 position, Vector3 rotation, bool passenger)
            {
                Vector3 pos; Vector3 rot;
                ins.GetGlobal(baseEntity, position, rotation, out pos, out rot);
                return (ScientistNPC)ins.NpcSpawn.Call("SpawnNpc", pos, ins.GetNpcConfig(!driver, ins._config.NPC.health, passenger));
            }

            internal void KillScientists(bool die = false)
            {
                foreach (ScientistNPC scientist in scientists)
                {
                    if (scientist != null && !scientist.IsDestroyed) scientist.Kill();
                }
                if (die && ins.active)
                {
                    foreach (ScientistNPC freeScientist in roamNpc) if (freeScientist != null && !freeScientist.IsDestroyed) ins.freeConvoyNpc.Add(freeScientist);
                }
                else
                {
                    foreach (ScientistNPC freeScientist in roamNpc) if (freeScientist != null && !freeScientist.IsDestroyed) freeScientist.Kill();
                }
                roamNpc.Clear();
                scientists.Clear();
            }
            #endregion NPC
        }

        class ConvoyCar : ConvoyVehicle
        {
            FlasherLight flasherLight;

            internal BasicCar basicCar;
            internal int currentPoint = 0;
            float lastDistance = 0;

            internal void InitSedan()
            {
                basicCar = GetComponent<BasicCar>();
                basicCar.motorForceConstant = 1000;
                basicCar._maxHealth = ins.sedanConfig.hp;
                basicCar.health = ins.sedanConfig.hp;
                foreach (BasicCar.VehicleWheel vehicleWheel in basicCar.wheels) vehicleWheel.powerWheel = true;

                flasherLight = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab") as FlasherLight;
                flasherLight.enableSaving = false;
                flasherLight.SetParent(basicCar);
                flasherLight.transform.localPosition = new Vector3(0.45f, 1.64f, 0.4f);
                flasherLight.Spawn();
                flasherLight.UpdateFromInput(1, 0);
                InvokeRepeating(UpdateFlasher, 10, 10);
            }

            internal override void CheckRotate()
            {
                if (followVehicle != null) return;
                if (currentPoint >= ins.pathCount - 3) ins.ReverseConvoy();
                if (Physics.RaycastAll(transform.position, basicCar.transform.forward, 7.5f, 1 << 16).Any(x => x.collider != null && x.collider.name.Contains("cliff"))) ins.ReverseConvoy();
            }

            internal override int GetCurrentPointIndex()
            {
                return currentPoint;
            }

            internal override void Rotate() => currentPoint = ins.pathCount - currentPoint;

            void FixedUpdate()
            {
                if (allConvoyStop || ins.failed) return;

                if (currentPoint >= ins.pathCount - 2) currentPoint = 0;
                float destanationDistance = Vector3.Distance(basicCar.transform.position, ins.currentPath[currentPoint + 1]);

                if (destanationDistance < 6f)
                {
                    lastDistance = 0;
                    currentPoint++;
                }

                if (rigidbody.velocity.magnitude < 0.5f)
                {
                    if (lastDistance > 0 && lastDistance - destanationDistance < -0.0f)
                    {
                        rigidbody.isKinematic = false;
                        rigidbody.AddForce(new Vector3(basicCar.transform.forward.x, 0, basicCar.transform.forward.z) * (rigidbody.velocity.magnitude + 0.1f), ForceMode.VelocityChange);
                        lastDistance = 0;
                    }
                }
                lastDistance = destanationDistance;

                ControlTurn();
                ControlTrottle();
            }

            void UpdateFlasher()
            {
                flasherLight.limitNetworking = true;
                flasherLight.limitNetworking = false;
            }

            void SetSpeed(float gasP)
            {
                if (allConvoyStop) return;
                float maxSpeed = followVehicle == null ? 4 : 6;

                if (gasP < 0 && !stop)
                {
                    StopMoving(false);
                    basicCar.brakePedal = 100;
                    return;
                }

                else if (gasP > 0 && stop)
                {
                    StartMoving(false);
                    basicCar.brakePedal = 0;
                }

                else if (rigidbody.velocity.magnitude > maxSpeed)
                {
                    if (rigidbody.velocity.magnitude > ++maxSpeed) basicCar.brakePedal = 50;
                    basicCar.gasPedal = 0;
                }

                else
                {
                    basicCar.gasPedal = gasP;
                    basicCar.brakePedal = 0;
                }

                basicCar.motorForceConstant = gasP;
                rigidbody.isKinematic = false;
            }

            void ControlTrottle()
            {
                if (followVehicle == null || followVehicle.baseEntity == null || followVehicle.baseEntity.IsDestroyed) SetSpeed(80);
                else
                {
                    float distance = Vector3.Distance(basicCar.transform.position, followVehicle.baseEntity.transform.position);
                    SetSpeed(ins.GetSpeed(10, distance, 100, 1.1f));
                }
            }

            void ControlTurn()
            {
                float turning = 0;

                Vector3 lhs = global::BradleyAPC.Direction2D(ins.currentPath[currentPoint + 1], basicCar.transform.position);
                float num2 = Vector3.Dot(lhs, basicCar.transform.right);
                float num3 = Vector3.Dot(lhs, basicCar.transform.right);
                float num4 = Vector3.Dot(lhs, -basicCar.transform.right);

                if (Vector3.Dot(lhs, -basicCar.transform.forward) > num2)
                {
                    if (num3 >= num4) turning = 1f;
                    else turning = -1f;
                }
                else turning = Mathf.Clamp(num2 * 3f, -1f, 1f);
                if (rigidbody.velocity.magnitude < 0.6f) turning = 0;

                basicCar.steering = turning * 70;
                basicCar.DoSteering();
            }
        }

        class ConvoyModular : ConvoyVehicle
        {
            MapMarkerGenericRadius mapmarker;
            VendingMachineMapMarker vendingMarker;

            internal int currentPoint = 0;
            internal ModularCar modularCar;
            internal HackableLockedCrate crate;
            float lastDistance = 0;

            internal override void OnDestroy()
            {
                CancelInvoke(CheckRotate);
                if (ins._config.marker != null && ins._config.marker.IsMarker)
                {
                    CancelInvoke(UpdateMapMarker);
                    if (mapmarker != null && !mapmarker.IsDestroyed) mapmarker.Kill();
                    if (vendingMarker != null && !vendingMarker.IsDestroyed) vendingMarker.Kill();
                }
                else CancelInvoke(UpdateCrateMarker);
                CancelInvoke(CheckRotate);
                CancelInvoke(UpdateCrate);
                KillScientists(true);
                Destroy(this);
            }

            internal override int GetCurrentPointIndex()
            {
                return currentPoint;
            }

            void UpdateMapMarker()
            {
                mapmarker.transform.position = modularCar.transform.position;
                mapmarker.SendUpdate();
                mapmarker.SendNetworkUpdate();

                vendingMarker.transform.position = modularCar.transform.position;
                vendingMarker.markerShopName = $"Convoy ({ins._config.eventTime - ins.eventTime + ins.destroyTime} s)";
                vendingMarker.SendNetworkUpdate();
            }

            void SpawnMapMarker()
            {
                mapmarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", modularCar.transform.position) as MapMarkerGenericRadius;
                mapmarker.Spawn();
                mapmarker.radius = ins._config.marker.Radius;
                mapmarker.alpha = ins._config.marker.Alpha;
                mapmarker.color1 = new Color(ins._config.marker.Color1.r, ins._config.marker.Color1.g, ins._config.marker.Color1.b);
                mapmarker.color2 = new Color(ins._config.marker.Color2.r, ins._config.marker.Color2.g, ins._config.marker.Color2.b);

                vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", modularCar.transform.position) as VendingMachineMapMarker;
                vendingMarker.Spawn();
                vendingMarker.markerShopName = $"Convoy ({ins._config.eventTime - ins.eventTime + ins.destroyTime} s)";

                InvokeRepeating(UpdateMapMarker, 0, 1f);
            }

            internal override void CheckRotate()
            {
                if (followVehicle != null) return;
                if (currentPoint >= ins.pathCount - 3) ins.ReverseConvoy();

                if (Physics.RaycastAll(transform.position, modularCar.transform.forward, 7.5f, 1 << 16).Any(x => x.collider != null && x.collider.name.Contains("cliff"))) ins.ReverseConvoy();
            }

            internal override void Rotate() => currentPoint = ins.pathCount - currentPoint;

            internal void InitModular()
            {
                modularCar = GetComponent<ModularCar>();
                Invoke(Build, 0.1f);
            }

            #region Builder
            void Build()
            {
                AddCarModules();
                AddFuel();
                modularCar.SetMaxHealth(ins.modularConfig.hp);
                modularCar.health = ins.modularConfig.hp;
                SpawnCrate();
                InvokeRepeating(UpdateCrate, 10f, 10f);
                if (ins._config.marker != null && ins._config.marker.IsMarker) SpawnMapMarker();
                else InvokeRepeating(UpdateCrateMarker, 10f, 2f);
            }

            void AddFuel()
            {
                StorageContainer fuelContainer = modularCar.GetFuelSystem().GetFuelContainer();
                fuelContainer.inventory.AddItem(fuelContainer.allowedItem, 1000);
                fuelContainer.isLootable = false;
            }

            void AddCarModules()
            {
                List<string> modules = ins.modularConfig.modules;
                for (int socketIndex = 0; socketIndex < modularCar.TotalSockets && socketIndex < modules.Count; socketIndex++)
                {
                    string shortName = modules[socketIndex];
                    Item existingItem = modularCar.Inventory.ModuleContainer.GetSlot(socketIndex);
                    if (existingItem != null) continue;
                    Item moduleItem = ItemManager.CreateByName(shortName);
                    if (moduleItem == null) continue;
                    moduleItem.conditionNormalized = 100;

                    if (!modularCar.TryAddModule(moduleItem, socketIndex)) moduleItem.Remove();
                }
                Invoke(AddEngineParts, 0.01f);
            }

            void AddEngineParts()
            {
                foreach (BaseVehicleModule module in modularCar.AttachedModuleEntities)
                {
                    VehicleModuleEngine engineModule = module as VehicleModuleEngine;
                    if (engineModule == null) continue;
                    engineModule.engine.maxFuelPerSec = 0;
                    engineModule.engine.idleFuelPerSec = 0;
                    EngineStorage engineStorage = engineModule.GetContainer() as EngineStorage;
                    if (engineStorage == null) continue;
                    ItemContainer inventory = engineStorage.inventory;

                    for (var i = 0; i < inventory.capacity; i++)
                    {
                        ItemModEngineItem output;
                        if (!engineStorage.allEngineItems.TryGetItem(1, engineStorage.slotTypes[i], out output)) continue;
                        ItemDefinition component = output.GetComponent<ItemDefinition>();
                        Item item = ItemManager.Create(component);
                        if (item == null) continue;
                        item.conditionNormalized = 100;
                        item.MoveToContainer(engineStorage.inventory, i, allowStack: false);
                    }
                    engineModule.RefreshPerformanceStats(engineStorage);
                    return;
                }
            }

            void UpdateCrateMarker()
            {
                if (crate != null || !crate.IsDestroyed) crate.CreateMapMarker(120);
            }

            void UpdateCrate()
            {
                if (crate == null || crate.IsDestroyed) return;
                crate.RefreshDecay();
                crate.limitNetworking = true;
                crate.limitNetworking = false;
            }

            internal void SpawnCrate()
            {
                crate = GameManager.server.CreateEntity("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", modularCar.transform.position) as HackableLockedCrate;
                crate.SetParent(modularCar, true, true);
                crate.transform.localPosition = ins.modularConfig.crateLocation.position.ToVector3();
                crate.transform.localEulerAngles = ins.modularConfig.crateLocation.rotation.ToVector3();
                crate.Spawn();
                Rigidbody crateRigidbody = crate.GetComponent<Rigidbody>();
                Destroy(crateRigidbody);

                crate.hackSeconds = HackableLockedCrate.requiredHackSeconds - ins.modularConfig.crateUnlockTime;
                if (ins.modularConfig.typeLootTable == 1)
                {
                    Invoke(() =>
                    {
                        for (int i = crate.inventory.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = crate.inventory.itemList[i];
                            item.RemoveFromContainer();
                            item.Remove();
                        }
                        ins.AddToContainer(crate.inventory, ins.modularConfig.lootTable.Items, UnityEngine.Random.Range(ins.modularConfig.lootTable.Min, ins.modularConfig.lootTable.Max + 1));
                    }, 0.01f);
                }
                crate.EnableGlobalBroadcast(true);
                crate.syncPosition = true;
                crate.SendNetworkUpdate();
            }
            #endregion Builder

            #region Moving
            void FixedUpdate()
            {
                if (allConvoyStop || ins.failed) return;
                if (modularCar.engineController.IsOff && !modularCar.engineController.IsStarting && driver != null) modularCar.engineController.TryStartEngine(driver);

                if (currentPoint >= ins.pathCount - 2) currentPoint = 0;
                float destanationDistance = Vector3.Distance(modularCar.transform.position, ins.currentPath[currentPoint + 1]);

                if (destanationDistance < 6f)
                {
                    currentPoint++;
                    lastDistance = 0;
                }

                if (rigidbody.velocity.magnitude < 1f)
                {
                    if (lastDistance > 0 && lastDistance - destanationDistance < -0.0f)
                    {
                        rigidbody.isKinematic = false;
                        rigidbody.AddForce(new Vector3(modularCar.transform.forward.x, 0, modularCar.transform.forward.z) * (rigidbody.velocity.magnitude + 0.5f), ForceMode.VelocityChange);
                        lastDistance = 0;
                    }
                    lastDistance = destanationDistance;
                }

                ControlTrottle();
                ControlTurn();
            }

            InputState CreateInput()
            {
                InputState inputState = new InputState();
                inputState.previous.mouseDelta = new Vector3(0, 0, 0);
                inputState.current.aimAngles = new Vector3(0, 0, 0);
                inputState.current.mouseDelta = new Vector3(0, 0, 0);
                return inputState;
            }

            void SetSpeed(float gasP)
            {
                if (allConvoyStop) return;

                float maxSpeed = followVehicle == null ? 4 : 6;

                if (gasP < 0 && !stop)
                {
                    StopMoving(false);
                    return;
                }

                else if (gasP > 0 && stop) StartMoving(false);

                else if (rigidbody.velocity.magnitude > maxSpeed)
                {
                    if (rigidbody.velocity.magnitude > ++maxSpeed) gasP = -0.3f;
                    else gasP = 0;
                }

                rigidbody.AddForce(new Vector3(modularCar.transform.forward.x, 0, modularCar.transform.forward.z) * gasP, ForceMode.VelocityChange);
            }

            void ControlTrottle()
            {
                if (followVehicle == null || followVehicle.baseEntity == null || followVehicle.baseEntity.IsDestroyed) SetSpeed(0.5f);
                else
                {
                    float distance = Vector3.Distance(modularCar.transform.position, followVehicle.transform.position);
                    SetSpeed(ins.GetSpeed(0.3f, distance, 1.5f, 1.05f));
                }
            }

            void ControlTurn()
            {
                float turning;

                Vector3 lhs = global::BradleyAPC.Direction2D(ins.currentPath[currentPoint + 1], modularCar.transform.position);
                float num2 = Vector3.Dot(lhs, modularCar.transform.right);
                float num3 = Vector3.Dot(lhs, modularCar.transform.right);
                float num4 = Vector3.Dot(lhs, -modularCar.transform.right);

                if (Vector3.Dot(lhs, -modularCar.transform.forward) > num2)
                {
                    if (num3 >= num4) turning = 1f;
                    else turning = -1f;
                }
                else turning = Mathf.Clamp(num2 * 3f, -1f, 1f);

                InputState inputState = CreateInput();
                if (turning < -0.5f) inputState.current.buttons = 8;

                else if (turning > 0.5f) inputState.current.buttons = 16;
                else inputState.current.buttons = 0;

                if (rigidbody.velocity.magnitude < 0.3f) inputState.current.buttons = 0;

                if (driver != null && inputState != null) modularCar.PlayerServerInput(inputState, driver);
            }
            #endregion Moving
        }

        class ConvoyBradley : ConvoyVehicle
        {
            internal BradleyAPC bradley;
            bool init = false;

            internal void InitBradley()
            {
                bradley = GetComponent<BradleyAPC>();
                bradley.pathLooping = true;
                bradley._maxHealth = ins.bradleyConfig.hp;
                bradley.health = ins.bradleyConfig.hp;
                bradley.maxCratesToSpawn = ins.bradleyConfig.countCrates;
                bradley.viewDistance = ins.bradleyConfig.viewDistance;
                bradley.searchRange = ins.bradleyConfig.searchDistance;
                bradley.coaxAimCone *= ins.bradleyConfig.coaxAimCone;
                bradley.coaxFireRate *= ins.bradleyConfig.coaxFireRate;
                bradley.coaxBurstLength = ins.bradleyConfig.coaxBurstLength;
                bradley.nextFireTime = ins.bradleyConfig.nextFireTime;
                bradley.topTurretFireRate = ins.bradleyConfig.topTurretFireRate;
                bradley.currentPath = ins.currentPath;
                bradley.enableSaving = false;
            }

            internal override int GetCurrentPointIndex()
            {
                return bradley.currentPathIndex;
            }

            internal override void CheckRotate()
            {
                if (followVehicle != null) return;
                if (bradley.currentPathIndex >= ins.pathCount - 3) ins.ReverseConvoy();
                if (Physics.RaycastAll(transform.position, bradley.transform.forward, 7.5f, 1 << 16).Any(x => x.collider != null && x.collider.name.Contains("cliff"))) ins.ReverseConvoy();
            }

            internal override void Rotate()
            {
                bradley.currentPath = ins.currentPath;
                bradley.currentPathIndex = ins.pathCount - bradley.currentPathIndex;
            }

            #region Moving
            void FixedUpdate()
            {
                if (ins.round && bradley.currentPathIndex >= ins.pathCount - 3) bradley.currentPathIndex = 0;

                if (!init && rigidbody != null && !rigidbody.isKinematic)
                {
                    rigidbody.AddForce(bradley.transform.forward * 5000, ForceMode.Force);
                    if (rigidbody.velocity.magnitude > 2) init = true;
                }

                if (followVehicle == null || followVehicle.baseEntity == null || followVehicle.baseEntity.IsDestroyed) bradley.moveForceMax = 800;
                else
                {
                    float distance = Vector3.Distance(bradley.transform.position, followVehicle.baseEntity.transform.position);
                    SetSpeed(ins.GetSpeed(200, distance, 2000, 1f));
                }
            }

            void SetSpeed(float gasP)
            {
                if (allConvoyStop) return;

                float maxSpeed = followVehicle == null ? 4 : 7.5f;

                if (gasP < 0 && !stop)
                {
                    StopMoving(false);
                    return;
                }

                else if (gasP > 0 && stop) StartMoving(false);

                else if (rigidbody.velocity.magnitude > maxSpeed)
                {
                    bradley.leftThrottle = 0;
                    bradley.rightThrottle = 0;
                    bradley.moveForceMax = 0;
                }
                else bradley.moveForceMax = gasP;
            }
            #endregion Moving
        }

        class ConvoyHeli : FacepunchBehaviour
        {
            BaseEntity targetEntity;

            internal PatrolHelicopterAI patrolHelicopterAI;
            internal BaseHelicopter baseHelicopter;

            internal void InitHelicopter(float hp)
            {
                baseHelicopter = GetComponent<BaseHelicopter>();
                patrolHelicopterAI = baseHelicopter.GetComponent<PatrolHelicopterAI>();
                baseHelicopter.startHealth = hp;
                baseHelicopter.InitializeHealth(hp, hp);
                baseHelicopter.maxCratesToSpawn = ins.heliConfig.cratesAmount;
                baseHelicopter.bulletDamage = ins.heliConfig.bulletDamage;
                baseHelicopter.bulletSpeed = ins.heliConfig.bulletSpeed;
                var weakspots = baseHelicopter.weakspots;
                if (weakspots != null && weakspots.Length > 1)
                {
                    weakspots[0].maxHealth = ins.heliConfig.mainRotorHealth;
                    weakspots[1].maxHealth = ins.heliConfig.rearRotorHealth;
                }
                targetEntity = ins.convoyModular.baseEntity;
                patrolHelicopterAI.isRetiring = true;
            }

            internal void SetTarget(BasePlayer player)
            {
                patrolHelicopterAI.SetTargetDestination(player.transform.position);
                patrolHelicopterAI._targetList.Add(new PatrolHelicopterAI.targetinfo(player, player));
                patrolHelicopterAI.State_Strafe_Enter(player.transform.position);
            }

            void FixedUpdate()
            {
                if (targetEntity == null || targetEntity.IsDestroyed) return;
                if (ins.stopTime <= 0)
                {
                    patrolHelicopterAI.SetTargetDestination(targetEntity.transform.position + new Vector3(0, ins.heliConfig.height, 0), 0, 0);
                    patrolHelicopterAI.SetIdealRotation(targetEntity.transform.rotation, 100);
                }
                else if (targetEntity.Distance(baseHelicopter.transform.position) > 350f) patrolHelicopterAI.SetTargetDestination(targetEntity.transform.position + new Vector3(0, ins.heliConfig.height, 0));
            }

            public void OnDestroy()
            {
                if (baseHelicopter != null && !baseHelicopter.IsDestroyed) baseHelicopter.Kill();
            }
        }

        class RootCar : FacepunchBehaviour
        {
            internal BasicCar basicCar;
            internal List<Vector3> root = new List<Vector3>();
            BasePlayer player;

            internal void InitSedan(BasePlayer player)
            {
                basicCar = GetComponent<BasicCar>();
                root.Add(basicCar.transform.position);
            }

            void FixedUpdate()
            {
                if (Vector3.Distance(basicCar.transform.position, root[root.Count() - 1]) > 3) root.Add(basicCar.transform.position);
            }
        }
        #endregion Classes 

        #region Helper  

        #region Loot  
        void OnCorpsePopulate(BasePlayer entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || corpse == null) return;
            if (entity is ScientistNPC && (convoyVehicles.Any(x => x.scientists.Contains(entity) || x.roamNpc.Contains(entity) || freeConvoyNpc.Contains(entity))))
            {
                NextTick(() =>
                {
                    if (_config.NPC.typeLootTable == 2 || _config.NPC.typeLootTable == 3)
                    {
                        if (corpse != null && !corpse.IsDestroyed) corpse.Kill();
                        return;
                    }
                    List<ItemConfig> items = _config.NPC.typeLootTable == 0 ? new List<ItemConfig>() : _config.NPC.lootTable.Items;

                    if (_config.NPC.typeLootTable == 0) foreach (Item item in corpse.containers[0].itemList) if (!_config.NPC.wearItems.Any(x => x.shortName == item.info.shortname)) items.Add(new ItemConfig { shortName = item.info.shortname, minAmount = item.amount, maxAmount = item.amount, chance = 100f, isBluePrint = false, skinID = item.skin, name = "" });
                    ItemContainer contaier = corpse.containers[0];
                    for (int i = corpse.containers[0].itemList.Count - 1; i >= 0; i--)
                    {
                        Item item = corpse.containers[0].itemList[i];
                        item.RemoveFromContainer();
                        item.Remove();
                    }

                    AddToContainer(contaier, items, _config.NPC.typeLootTable == 0 ? items.Count : UnityEngine.Random.Range(_config.NPC.lootTable.Min, _config.NPC.lootTable.Max + 1));

                    if (_config.NPC.lootTable.Max == 0 && _config.NPC.typeLootTable == 1)
                    {
                        for (int i = contaier.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = corpse.containers[0].itemList[i];
                            item.RemoveFromContainer();
                            item.Remove();
                        }
                    }
                    if (corpse != null && !corpse.IsDestroyed) corpse.Kill();
                });
            }
        }

        object CanPopulateLoot(LootContainer container)
        {
            if (container == null) return null;

            else if (container is HackableLockedCrate && convoyModular != null && convoyModular.crate != null && convoyModular.crate == container)
            {
                if (modularConfig.typeLootTable == 2) return null;
                else return true;
            }

            else if (container.ShortPrefabName == "bradley_crate" && Vector3.Distance(deathBradleyCoord, container.transform.position) < 15f)
            {
                if (bradleyConfig.typeLootTable == 2) return null;
                else return true;
            }

            else if (container.ShortPrefabName == "heli_crate" && Vector3.Distance(deathHeliCoord, container.transform.position) < 25f)
            {
                if (heliConfig.typeLootTable == 2) return null;
                else return true;
            }

            else return null;
        }

        object CanPopulateLoot(BasePlayer entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || corpse == null || convoyVehicles.Count == 0) return null;
            if (entity is ScientistNPC && convoyVehicles.Any(x => x.scientists.Contains(entity)))
            {
                if (_config.NPC.typeLootTable == 2) return null;
                else return true;
            }
            return null;
        }

        private object OnCustomLootContainer(uint netID)
        {
            if (!active) return null;
            if (convoyModular != null && convoyModular.baseEntity != null && !convoyModular.baseEntity.IsDestroyed && convoyModular.crate != null && convoyModular.crate.net.ID == netID)
            {
                if (modularConfig.typeLootTable == 3) return null;
                else return true;
            }
            return null;
        }

        void OnEntitySpawned(LockedByEntCrate crate)
        {
            if (crate == null) return;

            NextTick(() =>
            {
                if (crate.ShortPrefabName == "bradley_crate" && Vector3.Distance(deathBradleyCoord, crate.transform.position) < 15f)
                {

                    if (bradleyConfig.offDelay)
                    {
                        crate.CancelInvoke(crate.Think);
                        crate.SetLocked(false);
                        crate.lockingEnt = null;
                    }
                    if (bradleyConfig.typeLootTable == 1)
                    {
                        for (int i = crate.inventory.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = crate.inventory.itemList[i];
                            item.RemoveFromContainer();
                            item.Remove();
                        }
                        AddToContainer(crate.inventory, bradleyConfig.lootTable.Items, UnityEngine.Random.Range(bradleyConfig.lootTable.Min, bradleyConfig.lootTable.Max + 1));
                    }
                }

                else if (crate.ShortPrefabName == "heli_crate" && Vector3.Distance(deathHeliCoord, crate.transform.position) < 15f)
                {
                    if (heliConfig.offDelay)
                    {
                        crate.CancelInvoke(crate.Think);
                        crate.SetLocked(false);
                        crate.lockingEnt = null;
                    }
                    if (heliConfig.typeLootTable == 1)
                    {
                        for (int i = crate.inventory.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = crate.inventory.itemList[i];
                            item.RemoveFromContainer();
                            item.Remove();
                        }
                        AddToContainer(crate.inventory, heliConfig.lootTable.Items, UnityEngine.Random.Range(heliConfig.lootTable.Min, heliConfig.lootTable.Max + 1));
                    }
                }
            });
        }

        void AddToContainer(ItemContainer Container, List<ItemConfig> Items, int CountLoot)
        {
            int CountLootInContainer = 0;
            for (; CountLootInContainer <= CountLoot;)
            {
                foreach (ItemConfig item in Items)
                {
                    if (UnityEngine.Random.Range(0.0f, 100.0f) <= item.chance)
                    {
                        int amount = UnityEngine.Random.Range(item.minAmount, item.maxAmount + 1);
                        Item newItem = item.isBluePrint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.shortName, amount, item.skinID);
                        if (item.isBluePrint) newItem.blueprintTarget = ItemManager.FindItemDefinition(item.shortName).itemid;
                        if (item.name != "") newItem.name = item.name;
                        if (!newItem.MoveToContainer(Container)) newItem.Remove();
                        CountLootInContainer++;
                        if (CountLootInContainer == CountLoot) return;
                    }
                }
            }
        }
        #endregion Loot 

        #region TruePVE
        object CanEntityTakeDamage(BasePlayer victim, HitInfo hitinfo)
        {
            if (!_config.eventZone.isCreateZonePVP || victim == null || !victim.userID.IsSteamId() || hitinfo == null || !active || doorCloser == null || doorCloser.IsDestroyed) return null;
            BasePlayer attacker = hitinfo.InitiatorPlayer;
            if (players.Contains(victim) && (attacker == null || (attacker != null && players.Contains(attacker)))) return true;
            else return null;
        }

        object CanEntityTakeDamage(BasicCar victim, HitInfo hitinfo)
        {
            if (victim == null || hitinfo == null || active == false || hitinfo.InitiatorPlayer == null || !hitinfo.InitiatorPlayer.userID.IsSteamId()) return null;
            if (convoySummons.Contains(victim)) return true;
            else return null;
        }
        #endregion TruePVE

        #region Alerts
        private bool CanSendDiscordMessage() => _config.discord.isDiscord && !string.IsNullOrEmpty(_config.discord.webhookUrl) && _config.discord.webhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

        private void AlertToAllPlayers(string langKey, params object[] args)
        {
            if (CanSendDiscordMessage() && _config.discord.keys.Contains(langKey))
            {
                object fields = new[] { new { name = Title, value = ClearColorAndSize(GetMessage(langKey, null, args)), inline = false } };
                DiscordMessages?.Call("API_SendFancyMessage", _config.discord.webhookUrl, "", _config.discord.embedColor, JsonConvert.SerializeObject(fields), null, this);
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList) Alert(player, GetMessage(langKey, player.UserIDString, args));
        }

        string ClearColorAndSize(string message)
        {
            message = message.Replace("</color>", string.Empty);
            message = message.Replace("</size>", string.Empty);
            while (message.Contains("<color="))
            {
                int index = message.IndexOf("<color=");
                message = message.Remove(index, message.IndexOf(">", index) - index + 1);
            }
            while (message.Contains("<size="))
            {
                int index = message.IndexOf("<size=");
                message = message.Remove(index, message.IndexOf(">", index) - index + 1);
            }
            return message;
        }

        void Alert(BasePlayer player, string message)
        {
            if (_config.IsChat) PrintToChat(player, message);
            if (_config.GUIAnnouncements.isGUIAnnouncements) GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(message), _config.GUIAnnouncements.bannerColor, _config.GUIAnnouncements.textColor, player, _config.GUIAnnouncements.apiAdjustVPosition);
            if (_config.Notify.IsNotify) player.SendConsoleCommand($"notify.show {_config.Notify.Type} {ClearColorAndSize(message)}");
        }
        #endregion Alerts

        #region GUI
        void MessageGUI(BasePlayer player, string text)
        {
            CuiHelper.DestroyUi(player, "TextMain");

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = _config.GUI.AnchorMin, AnchorMax = _config.GUI.AnchorMax },
                CursorEnabled = false,
            }, "Hud", "TextMain");
            container.Add(new CuiElement
            {
                Parent = "TextMain",
                Components =
                {
                    new CuiTextComponent() { Color = "1 1 1 1", FadeIn = 0f, Text = text, FontSize = 24, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
                    new CuiOutlineComponent { Distance = "1 1", Color = "0 0 0 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            CuiHelper.AddUi(player, container);
        }
        #endregion GUI

        float GetSpeed(float step, float distance, float maxSpeed, float multiplicator)
        {
            float speed;
            float stopDistance = 10;
            if (distance <= stopDistance) return -1;
            speed = step * (distance - 5 - stopDistance) * multiplicator;
            if (speed < 0) return 0;
            if (speed > maxSpeed) return maxSpeed;
            return speed;
        }

        void GetGlobal(BaseEntity parrent, Vector3 localPosition, Vector3 localRotation, out Vector3 globalPosition, out Vector3 globalRotation)
        {
            globalPosition = parrent.transform.TransformPoint(localPosition);
            globalRotation = parrent.transform.rotation.eulerAngles + localRotation;
        }

        JObject GetNpcConfig(bool driver, float hp, bool passenger)
        {
            bool passive = !driver || (passenger && _config.blockFirstAttack);

            JObject sensory = new JObject()
            {
                ["AttackRangeMultiplier"] = _config.NPC.attackRangeMultiplier,
                ["SenseRange"] = _config.NPC.senseRange,
                ["MemoryDuration"] = _config.NPC.memoryDuration,
                ["CheckVisionCone"] = _config.NPC.checkVisionCone,
                ["VisionCone"] = _config.NPC.visionCone
            };
            JObject config = new JObject()
            {
                ["Name"] = _config.NPC.name,
                ["WearItems"] = new JArray { _config.NPC.wearItems.Select(x => new JObject { ["ShortName"] = x.shortName, ["SkinID"] = x.skinID }) },
                ["BeltItems"] = new JArray { _config.NPC.beltItems.Select(x => new JObject { ["ShortName"] = x.shortName, ["Amount"] = x.amount, ["SkinID"] = x.skinID, ["Mods"] = new JArray { x.Mods.Select(y => y) } }) },
                ["Kit"] = _config.NPC.Kits.GetRandom(),
                ["Health"] = _config.NPC.health,
                ["RoamRange"] = _config.NPC.roamRange,
                ["ChaseRange"] = _config.NPC.chaseRange,
                ["DamageScale"] = _config.NPC.damageScale,
                ["AimConeScale"] = _config.NPC.aimConeScale,
                ["DisableRadio"] = true,
                ["Stationary"] = false,
                ["CanUseWeaponMounted"] = !passive,
                ["CanRunAwayWater"] = Name != "Underwater Lab" && Name != "Train Tunnel",
                ["Speed"] = _config.NPC.speed,
                ["Sensory"] = sensory
            };

            return config;
        }

        void ChechTrash(Vector3 pos)
        {
            foreach (Collider collider in UnityEngine.Physics.OverlapSphere(pos, 10f))
            {
                BaseEntity entity = collider.ToBaseEntity();
                if (entity != null && !entity.IsDestroyed && (trashList.Contains(entity.ShortPrefabName) || barrels.Contains(entity.ShortPrefabName))) entity.Kill();
            }
        }

        void Unsubscribes() { foreach (string hook in subscribeMetods) Unsubscribe(hook); }

        void Subscribes() { foreach (string hook in subscribeMetods) Subscribe(hook); }

        HashSet<string> barrels = new HashSet<string>
        {
            "loot_barrel_1",
            "loot_barrel_2",
            "loot-barrel-2",
            "loot-barrel-1",
            "oil_barrel"
        };

        HashSet<string> trashList = new HashSet<string>
        {
            "minicopter.entity",
            "scraptransporthelicopter",
            "rowboat",
            "rhib",
            "3module_car_spawned.entity",
            "1module_passengers_armored",
            "hotairballoon",
            "wolf",
            "2module_car_spawned.entity",
            "chicken",
            "boar",
            "stag",
            "bear",
            "saddletest",
            "testridablehorse",
            "servergibs_bradley",
            "loot_barrel_1",
            "loot_barrel_2",
            "loot-barrel-2",
            "loot-barrel-1",
            "oil_barrel"
        };

        HashSet<string> subscribeMetods = new HashSet<string>
        {
            "OnEntityKill",
            "OnEntityTakeDamage",
            "OnVehicleModulesAssign",
            "CanHackCrate",
            "OnEntitySpawned",
            "OnHelicopterRetire",
            "OnEntityDeath",
            "CanHelicopterTarget",
            "CanBradleyApcTarget"
        };
        #endregion Helper  

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PreStart"] = "{0} Через <color=#738d43>{1}c</color>. начнется перевозка груза по автодороге!",
                ["ConvoyAttacked"] = "{0} {1} <color=#ce3f27>напал</color> на конвой",
                ["EventStart"] = "{0} Конвой <color=#738d43>начал</color> движение!",
                ["SecurityKill"] = "{0} Охрана конвоя была <color=#738d43>успешно</color> уничтожена!",
                ["Failed"] = "{0} Грузовик с грузом <color=#ce3f27>уничтожен</color>! Добыча <color=#ce3f27>потеряна</color>!",
                ["StartHackCrate"] = "{0} {1} <color=#738d43>начал</color> взлом заблокированного ящика!",
                ["PreFinish"] = "{0} Ивент будет окончен через <color=#ce3f27>{1}c</color>",
                ["Finish"] = "{0} Перевозка груза <color=#ce3f27>окончена</color>!",
                ["CantHackCrate"] = "{0} Для того чтобы открыть ящик убейте все <color=#ce3f27>сопровождающие</color> транспортные средства!",
                ["EventActive"] = "{0} Ивент в данный момент активен, сначала завершите текущий ивент (<color=#ce3f27>/convoystop</color>)!",
                ["EnterPVP"] = "{0} Вы <color=#ce3f27>вошли</color> в PVP зону, теперь другие игроки <color=#ce3f27>могут</color> наносить вам урон!",
                ["ExitPVP"] = "{0} Вы <color=#738d43>вышли</color> из PVP зоны, теперь другие игроки <color=#738d43>не могут</color> наносить вам урон!",
                ["GUI"] = "Груз будет уничтожен через {0} сек."
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PreStart"] = "{0} In <color=#738d43>{1}s.</color> the cargo will be transported along the road!",
                ["ConvoyAttacked"] = "{0} {1} <color=#ce3f27>attacked</color> a convoy",
                ["EventStart"] = "{0} The convoy <color=#738d43>started</color> moving",
                ["SecurityKill"] = "{0} The guard of the convoy was <color=#738d43>destroyed</color>!",
                ["Failed"] = "{0} The cargo truck has been <color=#ce3f27>destroyed</color>! The loot is <color=#ce3f27>lost</color>!",
                ["StartHackCrate"] = "{0} {1} started <color=#738d43>hacking</color> the locked crate!",
                ["PreFinish"] = "{0} The event will be over in <color=#ce3f27>{1}s</color>",
                ["Finish"] = "{0} The event is <color=#ce3f27>over</color>!",
                ["CantHackCrate"] = "{0} To open the crate, kill all the <color=#ce3f27>accompanying</color> vehicles!",
                ["EventActive"] = "{0} This event is active now. To finish this event (<color=#ce3f27/convoystop</color>)!",
                ["EnterPVP"] = "{0} You <color=#ce3f27>have entered</color> the PVP zone, now other players <color=#ce3f27>can damage</color> you!",
                ["ExitPVP"] = "{0} You <color=#738d43>have gone out</color> the PVP zone, now other players <color=#738d43>can’t damage</color> you!",
                ["GUI"] = "The cargo will be destroyed in {0} sec."
            }, this);
        }

        string GetMessage(string langKey, string userID) => lang.GetMessage(langKey, this, userID);

        string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
        #endregion Lang

        #region Data 

        Dictionary<string, List<List<string>>> roots = new Dictionary<string, List<List<string>>>();

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Title, roots);

        private void LoadData() => roots = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, List<List<string>>>>(Title);

        #endregion Data 

        #region Config  

        PluginConfig _config;

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        public class ConvoySetting
        {
            [JsonProperty(en ? "Name" : "Название пресета")] public string name;
            [JsonProperty(en ? "Automatic startup" : "Автоматический запуск")] public bool on;
            [JsonProperty(en ? "Probability of a preset [0.0-100.0]" : "Вероятность пресета [0.0-100.0]")] public float chance;
            [JsonProperty(en ? "Enable the helicopter" : "Включить вертолет")] public bool heliOn;
            [JsonProperty(en ? "The number of Bradleys ahead the truck" : "Количество бредли впереди грузовика")] public int firstBradleyCount;
            [JsonProperty(en ? "Number of Sedans ahead the truck" : "Количество седанов впереди грузовика")] public int firstSedanCount;
            [JsonProperty(en ? "Number of Sedans behind the truck" : "Количество седанов позади грузовика")] public int endSedanCount;
            [JsonProperty(en ? "The number of Bradleys behind the truck" : "Количество бредли позади грузовика")] public int endBradleyCount;
            [JsonProperty(en ? "Bradley preset" : "Пресет бредли")] public string bradleyConfigurationName;
            [JsonProperty(en ? "Sedan preset" : "Пресет седана")] public string sedanConfigurationName;
            [JsonProperty(en ? "Truck preset" : "Пресет грузовика")] public string modularConfigurationName;
            [JsonProperty(en ? "Heli preset" : "Пресет вертолета")] public string heliConfigurationName;
        }

        public class ModularConfig
        {
            [JsonProperty(en ? "Name" : "Название пресета")] public string presetName;
            [JsonProperty("HP")] public float hp;
            [JsonProperty(en ? "Scale damage" : "Множитель урона")] public float damageMultiplier;
            [JsonProperty(en ? "Modules" : "Модули")] public List<string> modules;
            [JsonProperty(en ? "Location of NPCs" : "Расположение NPC")] public List<CoordConfig> coordinates;
            [JsonProperty(en ? "The crate can be opened if other vehicles are not destroyed" : "Ящик можно открыть, если другие транспортные средства не разрушены")] public bool innidiatlyOpen;
            [JsonProperty(en ? "Time to unlock the crates [sec.]" : "Время до открытия заблокированного ящика [sec.]")] public float crateUnlockTime;
            [JsonProperty(en ? "Location of the locked crate" : "Расположение заблокированного ящика")] public CoordConfig crateLocation;
            [JsonProperty(en ? "Which loot table should the plugin use? (0 - default, BetterLoot, MagicLoot; 1 - own; 2 - AlphaLoot)" : "Какую таблицу лута необходимо использовать? (0 - стандартную, BetterLoot, MagicLoot; 1 - собственную; 2 - AlphaLoot)")] public int typeLootTable;
            [JsonProperty(en ? "Own loot table" : "Собственная таблица лута")] public LootTableConfig lootTable;
        }

        public class SedanConfig
        {
            [JsonProperty(en ? "Name" : "Название пресета")] public string presetName;
            [JsonProperty("HP")] public float hp;
            [JsonProperty(en ? "Location of all NPCs" : "Расположение NPC")] public List<CoordConfig> coordinates;
        }

        public class BradleyConfig
        {
            [JsonProperty(en ? "Name" : "Название пресета")] public string presetName;
            [JsonProperty("HP")] public float hp;
            [JsonProperty(en ? "Scale damage" : "Множитель урона")] public float scaleDamage;
            [JsonProperty(en ? "The viewing distance" : "Дальность обзора")] public float viewDistance;
            [JsonProperty(en ? "Radius of search" : "Радиус поиска")] public float searchDistance;
            [JsonProperty(en ? "The multiplier of Machine-gun aim cone" : "Множитель разброса пулемёта")] public float coaxAimCone;
            [JsonProperty(en ? "The multiplier of Machine-gun fire rate" : "Множитель скорострельности пулемёта")] public float coaxFireRate;
            [JsonProperty(en ? "Amount of Machine-gun burst shots" : "Кол-во выстрелов очереди пулемёта")] public int coaxBurstLength;
            [JsonProperty(en ? "The time between shots of the main gun [sec.]" : "Время между залпами основного орудия [sec.]")] public float nextFireTime;
            [JsonProperty(en ? "The time between shots of the main gun in a fire rate [sec.]" : "Время между выстрелами основного орудия в залпе [sec.]")] public float topTurretFireRate;
            [JsonProperty(en ? "Numbers of crates" : "Кол-во ящиков после уничтожения")] public int countCrates;
            [JsonProperty(en ? "Location of all NPCs" : "Расположение NPC")] public List<CoordConfig> coordinates;
            [JsonProperty(en ? "Open crates after spawn [true/false]" : "Open crates after spawn [true/false]")] public bool offDelay;
            [JsonProperty(en ? "Which loot table should the plugin use? (0 - default, BetterLoot, MagicLoot; 1 - own; 2 - AlphaLoot)" : "Какую таблицу лута необходимо использовать? (0 - стандартную, BetterLoot, MagicLoot; 1 - собственную; 2 - AlphaLoot)")] public int typeLootTable;
            [JsonProperty(en ? "Own loot table" : "Собственная таблица лута")] public LootTableConfig lootTable;
        }

        public class HeliConfig
        {
            [JsonProperty(en ? "Name" : "Название пресета")] public string presetName;
            [JsonProperty("HP")] public float hp;
            [JsonProperty(en ? "HP of the main rotor" : "HP главного винта")] public float mainRotorHealth;
            [JsonProperty(en ? "HP of tail rotor" : "HP хвостового винта")] public float rearRotorHealth;
            [JsonProperty(en ? "Numbers of crates" : "Количество ящиков")] public int cratesAmount;
            [JsonProperty(en ? "Flying height" : "Высота полета")] public float height;
            [JsonProperty(en ? "Bullet speed" : "Скорость пуль")] public float bulletSpeed;
            [JsonProperty(en ? "Bullet Damage" : "Урон пуль")] public float bulletDamage;
            [JsonProperty(en ? "Speed" : "Скорость")] public float speed;
            [JsonProperty(en ? "Open crates after spawn [true/false]" : "Открывать ящики после спавна [true/false]")] public bool offDelay;
            [JsonProperty(en ? "Which loot table should the plugin use? (0 - default, BetterLoot, MagicLoot; 1 - own; 2 - AlphaLoot)" : "Какую таблицу лута необходимо использовать? (0 - стандартную, BetterLoot, MagicLoot; 1 - собственную; 2 - AlphaLoot)")] public int typeLootTable;
            [JsonProperty(en ? "Own loot table" : "Собственная таблица лута")] public LootTableConfig lootTable;
        }

        public class NpcConfig
        {
            [JsonProperty(en ? "Name" : "Название")] public string name;
            [JsonProperty(en ? "Health" : "Кол-во ХП")] public float health;
            [JsonProperty(en ? "Roam Range" : "Дальность патрулирования местности")] public float roamRange;
            [JsonProperty(en ? "Chase Range" : "Дальность погони за целью")] public float chaseRange;
            [JsonProperty(en ? "Attack Range Multiplier" : "Множитель радиуса атаки")] public float attackRangeMultiplier;
            [JsonProperty(en ? "Sense Range" : "Радиус обнаружения цели")] public float senseRange;
            [JsonProperty(en ? "Memory duration [sec.]" : "Длительность памяти цели [sec.]")] public float memoryDuration;
            [JsonProperty(en ? "Scale damage" : "Множитель урона")] public float damageScale;
            [JsonProperty(en ? "Aim Cone Scale" : "Множитель разброса")] public float aimConeScale;
            [JsonProperty(en ? "Detect the target only in the NPC's viewing vision cone?" : "Обнаруживать цель только в углу обзора NPC? [true/false]")] public bool checkVisionCone;
            [JsonProperty(en ? "Vision Cone" : "Угол обзора")] public float visionCone;
            [JsonProperty(en ? "Speed" : "Скорость")] public float speed;
            [JsonProperty(en ? "Which loot table should the plugin use? (0 - default, BetterLoot, MagicLoot; 1 - own; 2 - AlphaLoot; 3 - CustomLoot)" : "Какую таблицу лута необходимо использовать? (0 - стандартную, BetterLoot, MagicLoot; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot)")] public int typeLootTable;
            [JsonProperty(en ? "Wear items" : "Одежда")] public List<NpcWear> wearItems;
            [JsonProperty(en ? "Belt items" : "Быстрые слоты")] public List<NpcBelt> beltItems;
            [JsonProperty(en ? "Kits" : "Kits")] public List<string> Kits;
            [JsonProperty(en ? "Own loot table" : "Собственная таблица лута")] public LootTableConfig lootTable;
        }

        public class CoordConfig
        {
            [JsonProperty(en ? "Position" : "Позиция")] public string position;
            [JsonProperty(en ? "Rotation" : "Вращение")] public string rotation;
        }

        public class NpcWear
        {
            [JsonProperty(en ? "ShortName" : "ShortName")] public string shortName;
            [JsonProperty(en ? "skinID (0 - default)" : "SkinID (0 - default)")] public ulong skinID;
        }

        public class NpcBelt
        {
            [JsonProperty(en ? "ShortName" : "ShortName")] public string shortName;
            [JsonProperty(en ? "Amount" : "Кол-во")] public int amount;
            [JsonProperty(en ? "skinID (0 - default)" : "SkinID (0 - default)")] public ulong skinID;
            [JsonProperty(en ? "Mods" : "Модификации на оружие")] public List<string> Mods;
        }

        public class ItemConfig
        {
            [JsonProperty("ShortName")] public string shortName;
            [JsonProperty(en ? "Minimum" : "Минимальное кол-во")] public int minAmount;
            [JsonProperty(en ? "Maximum" : "Максимальное кол-во")] public int maxAmount;
            [JsonProperty(en ? "Chance [0.0-100.0]" : "Шанс выпадения предмета [0.0-100.0]")] public float chance;
            [JsonProperty(en ? "Is this a blueprint? [true/false]" : "Это чертеж? [true/false]")] public bool isBluePrint;
            [JsonProperty(en ? "SkinID (0 - default)" : "SkinID (0 - default)")] public ulong skinID;
            [JsonProperty(en ? "Name (empty - default)" : "Название (empty - default)")] public string name;
        }

        public class LootTableConfig
        {
            [JsonProperty(en ? "Minimum numbers of items" : "Минимальное кол-во элементов")] public int Min;
            [JsonProperty(en ? "Maximum numbers of items" : "Максимальное кол-во элементов")] public int Max;
            [JsonProperty(en ? "List of items" : "Список предметов")] public List<ItemConfig> Items;
        }

        public class DomeConfig
        {
            [JsonProperty(en ? "Create a PVP zone in the convoy stop zone? (only for those who use the TruePVE plugin)[true/false]" : "Создавать зону PVP в зоне проведения ивента? (только для тех, кто использует плагин TruePVE) [true/false]")] public bool isCreateZonePVP;
            [JsonProperty(en ? "Use the dome? [true/false]" : "Использовать ли купол? [true/false]")] public bool isDome;
            [JsonProperty(en ? "Darkening the dome" : "Затемнение купола")] public int darkening;
        }

        public class GUIConfig
        {
            [JsonProperty(en ? "Use the Countdown GUI? [true/false]" : "Использовать ли GUI обратного отсчета? [true/false]")] public bool IsGUI;
            [JsonProperty("AnchorMin")] public string AnchorMin;
            [JsonProperty("AnchorMax")] public string AnchorMax;
        }

        public class ColorConfig
        {
            [JsonProperty("r")] public float r;
            [JsonProperty("g")] public float g;
            [JsonProperty("b")] public float b;
        }

        public class MarkerConfig
        {
            [JsonProperty(en ? "Do you use the Marker? [true/false]" : "Использовать ли маркер? [true/false]")] public bool IsMarker;
            [JsonProperty(en ? "Radius" : "Радиус")] public float Radius;
            [JsonProperty(en ? "Alpha" : "Прозрачность")] public float Alpha;
            [JsonProperty(en ? "Marker color" : "Цвет маркера")] public ColorConfig Color1;
            [JsonProperty(en ? "Outline color" : "Цвет контура")] public ColorConfig Color2;
        }

        public class GUIAnnouncementsConfig
        {
            [JsonProperty(en ? "Do you use the GUI Announcements? [true/false]" : "Использовать ли GUI Announcements? [true/false]")] public bool isGUIAnnouncements;
            [JsonProperty(en ? "Banner color" : "Цвет баннера")] public string bannerColor;
            [JsonProperty(en ? "Text color" : "Цвет текста")] public string textColor;
            [JsonProperty(en ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float apiAdjustVPosition;
        }

        public class NotifyConfig
        {
            [JsonProperty(en ? "Do you use the Notify? [true/false]" : "Использовать ли Notify? [true/false]")] public bool IsNotify;
            [JsonProperty(en ? "Type" : "Тип")] public string Type;
        }

        public class DiscordConfig
        {
            [JsonProperty(en ? "Do you use the Discord? [true/false]" : "Использовать ли Discord? [true/false]")] public bool isDiscord;
            [JsonProperty("Webhook URL")] public string webhookUrl;
            [JsonProperty(en ? "Embed Color (DECIMAL)" : "Цвет полосы (DECIMAL)")] public int embedColor;
            [JsonProperty(en ? "Keys of required messages" : "Ключи необходимых сообщений")] public HashSet<string> keys;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        class PluginConfig
        {
            [JsonProperty(en ? "Version" : "Версия плагина")] public string version;
            [JsonProperty(en ? "Prefix of chat messages" : "Префикс в чате")] public string prefics;
            [JsonProperty(en ? "Use a chat? [true/false]" : "Использовать ли чат? [true/false]")] public bool IsChat;
            [JsonProperty(en ? "GUI Announcements setting" : "Настройка GUI Announcements")] public GUIAnnouncementsConfig GUIAnnouncements;
            [JsonProperty(en ? "Notify setting" : "Настройка Notify")] public NotifyConfig Notify;
            [JsonProperty(en ? "Discord setting (only for DiscordMessages)" : "Настройка оповещений в Discord (только для DiscordMessages)")] public DiscordConfig discord;
            [JsonProperty(en ? "Custom route name" : "Пресет кастомного маршрута")] public string customRootName;
            [JsonProperty(en ? "If there is a ring road on the map, then the event will be held on it" : "Если на карте есть кольцевая дорога, то ивент будет проводиться на ней")] public bool rounRoadPriority;
            [JsonProperty(en ? "The minimum length of the road on which the event can be held (Recommended values: standard map - 100, custom - 300)" : "Минимальное длина дороги, на которой может проводиться ивент (Рекомендуемые значения: стандартная карта - 100, кастомная - 300)")] public int roadCount;
            [JsonProperty(en ? "The distance between the machines during spawn (Recommended values: standard map - 3, custom - 10)" : "Расстояние между машинами при спавне (Рекомендуемые значения: стандартная карта - 3, кастомная - 10)")] public int carDistance;
            [JsonProperty(en ? "Minimum time between events [sec.]" : "Минимальное время между ивентами [sec.]")] public int minStartTime;
            [JsonProperty(en ? "Maximum time between events [sec.]" : "Максимальное время между ивентами [sec.]")] public int maxStartTime;
            [JsonProperty(en ? "Time before the starting of the event after receiving a chat message [sec.]" : "Время до начала ивента после сообщения в чате [sec.]")] public int preStartTime;
            [JsonProperty(en ? "Notification time until the end of the event [sec.] " : "Время оповещения до окончания ивента [sec.]")] public int preFinishTime;
            [JsonProperty(en ? "Duration of the event [sec.]" : "Длительность ивента [sec.]")] public int eventTime;
            [JsonProperty(en ? "The time for which the convoy stops moving after receiving damage [sec.]" : "Время, на которое останавливается конвой, после получения урона [sec.]")] public int damamageStopTime;
            [JsonProperty(en ? "The convoy will not attack first [true/false]" : "Бредли и вертолет не будут атаковать первыми [true/false]")] public bool blockFirstAttack;
            [JsonProperty(en ? "Remove obstacles in front of the convoy [true/false]" : "Удалять преграды перед конвоем [true/false]")] public bool deleteBarriers;
            [JsonProperty(en ? "List of obstacles" : "Список преград")] public List<string> barriers;
            [JsonProperty(en ? "If an NPC has been killed, it will not spawn at the next stop of the convoy [true/false]" : "Если NPC был убит, то он не будет поялвляться при следующей остановке конвоя [true/false]")] public bool blockSpawnDieNpc;
            [JsonProperty(en ? "Blocked roads (command /convoyroadblock)" : "Заблокированные дороги (команда /convoyroadblock)")] public List<int> blockRoads;
            [JsonProperty(en ? "Convoy Presets" : "Пресеты конвоя")] public List<ConvoySetting> convoys;
            [JsonProperty(en ? "Marker Setting" : "Настройки маркера")] public MarkerConfig marker;
            [JsonProperty(en ? "Event zone" : "Настройка зоны ивента")] public DomeConfig eventZone;
            [JsonProperty("GUI")] public GUIConfig GUI;
            [JsonProperty(en ? "Bradley Configurations" : "Кофигурации бредли")] public List<BradleyConfig> bradleyConfiguration;
            [JsonProperty(en ? "Sedan Configurations" : "Кофигурации седанов")] public List<SedanConfig> sedanConfiguration;
            [JsonProperty(en ? "Truck Configurations" : "Кофигурации грузовиков")] public List<ModularConfig> modularConfiguration;
            [JsonProperty(en ? "Heli Configurations" : "Кофигурации вертолетов")] public List<HeliConfig> heliesConfiguration;
            [JsonProperty(en ? "NPC Configurations" : "Кофигурации NPC")] public NpcConfig NPC;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    version = "1.1.5",
                    prefics = "[Convoy]",
                    IsChat = true,
                    GUIAnnouncements = new GUIAnnouncementsConfig
                    {
                        isGUIAnnouncements = false,
                        bannerColor = "Grey",
                        textColor = "White",
                        apiAdjustVPosition = 0.03f
                    },
                    Notify = new NotifyConfig
                    {
                        IsNotify = false,
                        Type = "0"
                    },
                    discord = new DiscordConfig
                    {
                        isDiscord = false,
                        webhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                        embedColor = 13516583,
                        keys = new HashSet<string>
                        {
                            "PreStart",
                            "EventStart",
                            "PreFinish",
                            "Finish",
                            "StartHackCrate"
                        }
                    },
                    customRootName = "",
                    rounRoadPriority = true,
                    roadCount = 100,
                    carDistance = 3,
                    minStartTime = 3600,
                    maxStartTime = 3600,
                    preStartTime = 60,
                    eventTime = 1700,
                    damamageStopTime = 120,
                    blockFirstAttack = false,
                    blockSpawnDieNpc = false,
                    deleteBarriers = true,
                    barriers = new List<string>
                    {
                        "minicopter.entity",
                        "scraptransporthelicopter",
                        "rowboat",
                        "rhib",
                        "1module_passengers_armored",
                        "2module_car_spawned.entity",
                        "3module_car_spawned.entity",
                        "4module_car_spawned.entity",
                        "hotairballoon",
                        "saddletest",
                        "testridablehorse",
                        "servergibs_bradley",
                        "loot_barrel_1",
                        "loot_barrel_2",
                        "loot-barrel-2",
                        "loot-barrel-1",
                        "oil_barrel"
                    },
                    preFinishTime = 60,
                    blockRoads = new List<int>(),
                    convoys = new List<ConvoySetting>
                    {
                        new ConvoySetting
                        {
                            name = "standart",
                            chance = 100,
                            on = true,
                            firstBradleyCount = 1,
                            firstSedanCount = 1,
                            endSedanCount = 1,
                            endBradleyCount = 1,
                            bradleyConfigurationName = "bradley_1",
                            modularConfigurationName = "truck_1",
                            sedanConfigurationName = "sedan_1",
                            heliOn = false,
                            heliConfigurationName = "heli_1"
                        }
                    },
                    marker = new MarkerConfig
                    {
                        IsMarker = false,
                        Radius = 0.2f,
                        Alpha = 0.6f,
                        Color1 = new ColorConfig { r = 0.81f, g = 0.25f, b = 0.15f },
                        Color2 = new ColorConfig { r = 0f, g = 0f, b = 0f }
                    },
                    eventZone = new DomeConfig
                    {
                        isCreateZonePVP = false,
                        isDome = false,
                        darkening = 5
                    },
                    GUI = new GUIConfig
                    {
                        IsGUI = true,
                        AnchorMin = "0 0.9",
                        AnchorMax = "1 0.95"
                    },
                    bradleyConfiguration = new List<BradleyConfig>
                    {
                        new BradleyConfig
                        {
                            presetName = "bradley_1",
                            hp = 1000f,
                            scaleDamage = 1f,
                            viewDistance = 100.0f,
                            searchDistance = 100.0f,
                            coaxAimCone = 1.1f,
                            coaxFireRate = 1.0f,
                            coaxBurstLength = 10,
                            nextFireTime = 10f,
                            topTurretFireRate = 0.25f,
                            countCrates = 3,
                            coordinates = new List<CoordConfig>
                            {
                                new CoordConfig { position = "(3, 0, 3)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-3, 0, 3)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(3, 0, -3)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(3, 0, 0)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-3, 0, 0)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-3, 0, -3)", rotation = "(0, 0, 0)" }
                            },
                            offDelay = false,
                            typeLootTable = 0,
                            lootTable = new LootTableConfig
                            {
                                Min = 1,
                                Max = 2,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig
                                    {
                                        shortName = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 50.0f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    }
                                }
                            }
                        }
                    },

                    sedanConfiguration = new List<SedanConfig>
                    {
                        new SedanConfig
                        {
                            presetName = "sedan_1",
                            hp = 500f,
                            coordinates = new List<CoordConfig>
                            {
                                new CoordConfig { position = "(2, 0, 2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-2, 0, 2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(2, 0, -2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-2, 0, -2)", rotation = "(0, 0, 0)" }
                            },
                        }
                    },

                    modularConfiguration = new List<ModularConfig>
                    {
                        new ModularConfig
                        {
                            presetName = "truck_1",
                            damageMultiplier = 0.5f,
                            modules = new List<string> { "vehicle.1mod.engine", "vehicle.1mod.cockpit.armored", "vehicle.1mod.passengers.armored", "vehicle.1mod.flatbed" },
                            crateLocation = new CoordConfig { position = "(0, 0.65, -2.35)", rotation = "(0, 180, 0)" },
                            coordinates = new List<CoordConfig>
                            {
                                new CoordConfig { position = "(2, 0, 2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-2, 0, 2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(2, 0, -2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-2, 0, -2)", rotation = "(0, 0, 0)" }
                            },
                            innidiatlyOpen = false,
                            crateUnlockTime = 10,
                            typeLootTable = 0,
                            lootTable = new LootTableConfig
                            {
                                Min = 1,
                                Max = 2,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig
                                    {
                                        shortName = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 50.0f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    }
                                }
                            }
                        }
                    },

                    heliesConfiguration = new List<HeliConfig>
                    {
                        new HeliConfig
                        {
                            presetName = "heli_1",
                            hp = 10000f,
                            cratesAmount = 3,
                            mainRotorHealth = 750f,
                            rearRotorHealth = 375f,
                            height = 50f,
                            bulletDamage = 20f,
                            bulletSpeed = 250f,
                            speed = 25f,
                            offDelay = false,
                            typeLootTable = 0,
                            lootTable = new LootTableConfig
                            {
                                Min = 1,
                                Max = 2,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig
                                    {
                                        shortName = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 50.0f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    }
                                }
                            }
                        }
                    },

                    NPC = new NpcConfig
                    {
                        wearItems = new List<NpcWear>
                        {
                            new NpcWear
                            {
                                shortName = "metal.plate.torso",
                                skinID = 1988476232
                            },
                            new NpcWear
                            {
                                shortName = "riot.helmet",
                                skinID = 1988478091
                            },
                            new NpcWear
                            {
                                shortName = "pants",
                                skinID = 1582399729
                            },
                            new NpcWear
                            {
                                shortName = "tshirt",
                                skinID = 1582403431
                            },
                            new NpcWear
                            {
                                shortName = "shoes.boots",
                                skinID = 0
                            }
                        },
                        beltItems = new List<NpcBelt>
                        {
                            new NpcBelt
                            {
                                shortName = "rifle.lr300",
                                amount = 1,
                                skinID = 0,
                                Mods = new List<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }
                            },
                            new NpcBelt
                            {
                                shortName = "syringe.medical",
                                amount = 10,
                                skinID = 0,
                                Mods = new List<string> ()
                            },
                            new NpcBelt
                            {
                                shortName = "grenade.f1",
                                amount = 10,
                                skinID = 0,
                                Mods = new List<string> ()
                            }
                        },
                        Kits = new List<string>(),
                        name = "ConvoyNPC",
                        health = 200f,
                        roamRange = 5f,
                        chaseRange = 110f,
                        attackRangeMultiplier = 1f,
                        senseRange = 60f,
                        memoryDuration = 60f,
                        damageScale = 1f,
                        aimConeScale = 1f,
                        checkVisionCone = false,
                        visionCone = 135f,
                        speed = 7.5f,

                        lootTable = new LootTableConfig
                        {
                            Min = 2,
                            Max = 4,
                            Items = new List<ItemConfig>
                            {
                                new ItemConfig
                                {
                                    shortName = "rifle.semiauto",
                                    minAmount = 1,
                                    maxAmount = 1,
                                    chance = 0.09f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "shotgun.pump",
                                    minAmount = 1,
                                    maxAmount = 1,
                                    chance = 0.09f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "pistol.semiauto",
                                    minAmount = 1,
                                    maxAmount = 1,
                                    chance = 0.09f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "largemedkit",
                                    minAmount = 1,
                                    maxAmount = 1,
                                    chance = 0.1f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "smg.2",
                                    minAmount = 1,
                                    maxAmount = 1,
                                    chance = 0.1f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "pistol.python",
                                    minAmount = 1,
                                    maxAmount = 1,
                                    chance = 0.1f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "smg.thompson",
                                    minAmount = 1,
                                    maxAmount = 1,
                                    chance = 0.1f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "shotgun.waterpipe",
                                    minAmount = 1,
                                    maxAmount = 1,
                                    chance = 0.2f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "shotgun.double",
                                    minAmount = 1,
                                    maxAmount = 1,
                                    chance = 0.2f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "ammo.rifle.explosive",
                                    minAmount = 8,
                                    maxAmount = 8,
                                    chance = 0.2f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "pistol.revolver",
                                    minAmount = 1,
                                    maxAmount = 1,
                                    chance = 0.2f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "ammo.rifle.hv",
                                    minAmount = 10,
                                    maxAmount = 10,
                                    chance = 0.2f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "ammo.rifle.incendiary",
                                    minAmount = 8,
                                    maxAmount = 8,
                                    chance = 0.5f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "ammo.pistol.hv",
                                    minAmount = 10,
                                    maxAmount = 10,
                                    chance = 0.5f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "ammo.pistol.fire",
                                    minAmount = 10,
                                    maxAmount = 10,
                                    chance = 1f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "ammo.shotgun.slug",
                                    minAmount = 4,
                                    maxAmount = 8,
                                    chance = 5f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "ammo.shotgun.fire",
                                    minAmount = 4,
                                    maxAmount = 14,
                                    chance = 5f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "ammo.shotgun",
                                    minAmount = 6,
                                    maxAmount = 12,
                                    chance = 8f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "bandage",
                                    minAmount = 1,
                                    maxAmount = 1,
                                    chance = 17f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "syringe.medical",
                                    minAmount = 1,
                                    maxAmount = 2,
                                    chance = 34f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "ammo.rifle",
                                    minAmount = 12,
                                    maxAmount = 36,
                                    chance = 51f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                },
                                new ItemConfig
                                {
                                    shortName = "ammo.pistol",
                                    minAmount = 15,
                                    maxAmount = 45,
                                    chance = 52f,
                                    isBluePrint = false,
                                    skinID = 0,
                                    name = ""
                                }
                            }
                        }
                    }
                };
            }
        }
        #endregion Config
    }
}