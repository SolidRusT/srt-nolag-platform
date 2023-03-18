using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Plugins;
using IEnumerator = System.Collections.IEnumerator;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("FancyDrop", "FastBurst", "3.2.2")]
    [Description("The Next Level of a fancy airdrop-toolset")]
    class FancyDrop : RustPlugin
    {
        [PluginReference]
        Plugin AlphaLoot, GUIAnnouncements, MagicLoot;

        #region Vars
        private static FancyDrop Instance = null;
        bool Changed = false;
        bool initialized = false;
        Vector3 lastDropPos;
        float lastDropRadius = 0;
        Vector3 lastLootPos;
        int lastMinute;
        double lastHour;

        string msgConsoleDropSpawn;
        static string msgConsoleDropLanded;
        Timer _aidropTimer;
        Timer _massDropTimer;

        private const string SMOKE_EFFECT = "assets/bundled/prefabs/fx/smoke_signal_full.prefab";
        private const string SIRENLIGHT_EFFECT = "assets/prefabs/io/electric/lights/sirenlightorange.prefab";
        private const string SIRENALARM_EFFECT = "assets/prefabs/deployable/playerioents/alarms/audioalarm.prefab";
        private const string PLANE_PREFAB = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
        private const string SUPPLY_PREFAB = "assets/prefabs/misc/supply drop/supply_drop.prefab";
        private const string HELIBURST_PREFAB = "assets/prefabs/npc/patrol helicopter/rocket_heli_airburst.prefab";
        private const string ROCKETSMOKE_PREFAB = "assets/prefabs/ammo/rocket/rocket_smoke.prefab";
        private const string EXPLOSION_PREFAB = "assets/bundled/prefabs/fx/survey_explosion.prefab";
        private const string HELIEXPLOSION_EFFECT = "assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab";
        private const string BRADLEY_CANNON_EFFECT = "assets/prefabs/npc/m2bradley/effects/maincannonattack.prefab";
        private const string PARACHUTE_PREFAB = "assets/prefabs/misc/parachute/parachute.prefab";

        private static RaycastHit rayCastHit;
        private const int CAST_LAYERS = 1 << 0 | 1 << 4 | 1 << 8 | 1 << 10 | 1 << 16 | 1 << 21 | 1 << 23 | 1 << 25 | 1 << 26;

        List<CargoPlane> CargoPlanes = new List<CargoPlane>();
        List<SupplyDrop> SupplyDrops = new List<SupplyDrop>();
        private bool IsFancySupplyDrop(SupplyDrop drop) => SupplyDrops.Contains(drop);
        List<SupplyDrop> LootedDrops = new List<SupplyDrop>();
        List<BaseEntity> activeSignals = new List<BaseEntity>();
        Dictionary<BasePlayer, Timer> timers = new Dictionary<BasePlayer, Timer>();
        private Dictionary<string, int> itemNameToId = new Dictionary<string, int>();
        private ConfigData.StaticOptions.LootOptions lootTable;
        #endregion        

        #region Oxide Hooks
        private void Init()
        {
            Instance = this;
            initialized = false;
            msgConsoleDropSpawn = msg("msgConsoleDropSpawn");
            msgConsoleDropLanded = msg("msgConsoleDropLanded");

            Interface.CallHook("OnFancyDropTypes", configData.DropSettings.setupDropTypes);
        }

        private void Unload()
        {
            airdropTimerStop();

            foreach (var obj in UnityEngine.Object.FindObjectsOfType<ColliderCheck>().ToList())
                GameObject.Destroy(obj);

            foreach (var obj in UnityEngine.Object.FindObjectsOfType<DropTiming>().ToList())
                GameObject.Destroy(obj);

            if (_massDropTimer != null && !_massDropTimer.Destroyed)
                _massDropTimer.Destroy();

            var drops = UnityEngine.Object.FindObjectsOfType<SupplyDrop>().ToList();
            foreach (var drop in drops)
                drop.Kill();

            CargoPlanes.Clear();
            SupplyDrops.Clear();
            LootedDrops.Clear();
            ItemManager.DoRemoves();

            Instance = null;
        }

        private void OnServerInitialized()
        {
            Puts($"Map Highest Point: ({TerrainMeta.HighestPoint.y}m) | Plane flying height: (~{TerrainMeta.HighestPoint.y * configData.AirdropSettings.planeOffSetYMultiply}m)");
            if (configData.TimerSettings.airdropTimerEnabled)
                Puts($"Timed Airdrop activated with '{configData.TimerSettings.airdropTimerMinPlayers}' players between '{configData.TimerSettings.airdropTimerWaitMinutesMin}' and '{configData.TimerSettings.airdropTimerWaitMinutesMax}' minutes");
            if ((configData.TimerSettings.airdropCleanupAtStart && UnityEngine.Time.realtimeSinceStartup < 60) || BasePlayer.activePlayerList.Count == 1)
                airdropCleanUp();
            if (configData.TimerSettings.airdropRemoveInBuilt)
                removeBuiltInAirdrop();
            if (configData.TimerSettings.airdropTimerEnabled)
                airdropTimerNext();

            object value;
            var checkdefaults = defaultDrop();
            foreach (var pair in checkdefaults)
                if (!setupDropDefault.TryGetValue(pair.Key, out value))
                    setupDropDefault.Add(pair.Key, checkdefaults[pair.Key]);

            lang.RegisterMessages(Messages, this);
            initialized = true;
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            NextTick(() =>
            {
                if (!initialized || entity == null || !(entity is SupplySignal))
                    return;

                if (entity.net == null)
                    entity.net = Network.Net.sv.CreateNetworkable();

                if (Interface.CallHook("ShouldFancyDrop", entity.net.ID) != null)
                    return;

                if (activeSignals.Contains(entity))
                    return;

                activeSignals.Add(entity);
                SupplyThrown(player, entity);
            });
        }

        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity, ThrownWeapon thrown)
        {
            NextTick(() =>
            {
                if (!initialized || entity == null || !(entity is SupplySignal))
                    return;

                if (Interface.CallHook("ShouldFancyDrop", entity.net.ID) != null)
                    return;

                if (activeSignals.Contains(entity))
                    return;

                activeSignals.Add(entity);
                SupplyThrown(player, entity);
            });
        }

        private void SupplyThrown(BasePlayer player, BaseEntity entity)
        {
            Vector3 playerposition = player.transform.position;

            timer.Once(3.0f, () => {
                if (entity == null)
                {
                    activeSignals.Remove(entity);
                    return;
                }
                InvokeHandler.CancelInvoke(entity.GetComponent<MonoBehaviour>(), new Action((entity as SupplySignal).Explode));
            });
            timer.Once(3.3f, () => {
                if (entity == null) return;
                activeSignals.Remove(entity);

                Vector3 position = new Vector3();
                string gridPos = GetGridString(position);

                if (!configData.AirdropSettings.disableRandomSupplyPos)
                    position = entity.transform.position + new Vector3(UnityEngine.Random.Range(-20f, 20f), 0f, UnityEngine.Random.Range(-20f, 20f));
                else
                    position = entity.transform.position;
                InvokeHandler.Invoke(entity.GetComponent<MonoBehaviour>(), new Action((entity as SupplySignal).FinishUp), configData.GenericSettings.supplySignalSmokeTime);
                entity.SetFlag(BaseEntity.Flags.On, true, false);
                entity.SendNetworkUpdateImmediate(false);

                if (configData.GenericSettings.lockSignalDrop)
                    startCargoPlane(position, false, null, "supplysignal", "", true, player.userID);
                else
                    startCargoPlane(position, false, null, "supplysignal");

                if (configData.Notifications.notifyDropConsoleSignal)
                    Puts($"SupplySignal thrown by '{player.displayName}' at: {playerposition}");

                if (configData.Notifications.notifyDropAdminSignal)
                {
                    foreach (var admin in BasePlayer.activePlayerList.Where(p => p.IsAdmin).ToList())
                        SendReply(admin, $"<color={configData.GenericSettings.colorAdmMsg}>" + string.Format(msg("msgDropSignalAdmin", player.UserIDString), player.displayName, playerposition) + "</color>");
                }

                if (configData.Notifications.notifyDropSignalByPlayer)
                    if (configData.Notifications.notifyDropSignalByPlayerCoords)
                        PrintToChat(string.Format(configData.GenericSettings.Format, configData.GenericSettings.Color, configData.GenericSettings.Prefix) + $"<color={configData.GenericSettings.colorTextMsg}>" + string.Format(msg("msgDropSignalByPlayerCoords", player.UserIDString), player.displayName, position.x.ToString("0"), position.z.ToString("0")) + "</color>");
                    else if (configData.Notifications.notifyDropSignalByPlayerGrid)
                        PrintToChat(string.Format(configData.GenericSettings.Format, configData.GenericSettings.Color, configData.GenericSettings.Prefix) + $"<color={configData.GenericSettings.colorTextMsg}>" + string.Format(msg("msgDropSignalByPlayerGrid", player.UserIDString), player.displayName, gridPos) + "</color>");
                    else
                        PrintToChat(string.Format(configData.GenericSettings.Format, configData.GenericSettings.Color, configData.GenericSettings.Prefix) + $"<color={configData.GenericSettings.colorTextMsg}>" + string.Format(msg("msgDropSignalByPlayer", player.UserIDString), player.displayName) + "</color>");
            });
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!initialized || entity == null || !(entity is SupplyDrop) || LootedDrops.Contains(entity as SupplyDrop))
                return;

            if ((configData.GenericSettings.lockSignalDrop || configData.GenericSettings.lockDirectDrop) && entity.OwnerID != 0uL && entity.OwnerID != player.userID)
            {
                NextTick(() => player.EndLooting());
                MessageToPlayer(player, msg("msgCrateLocked", player.UserIDString));
                if (configData.DebugSettings.useDebug)
                    Puts($"Drop is locked to {entity.OwnerID.ToString()}");
                return;
            }

            if (entity.OwnerID == player.userID)
            {
                string gridPos = GetGridString(entity.transform.position);
                if (configData.Notifications.notifyDropConsoleLooted)
                    Puts($"{player.displayName} ({player.UserIDString}) looted his Drop at: {entity.transform.position.ToString("0")} grid area: {gridPos}");

                if (!configData.GenericSettings.unlockDropAfterLoot)
                {
                    entity.OwnerID = 0uL;
                    LootedDrops.Add(entity as SupplyDrop);
                }
                return;
            }

            if (Vector3.Distance(lastLootPos, entity.transform.position) > ((lastDropRadius * 2) * 1.2))
            {
                string gridPos = GetGridString(entity.transform.position);

                if (configData.Notifications.notifyDropServerLooted)
                    NotifyOnDropLooted(entity, player);
                if (configData.Notifications.notifyDropConsoleLooted)
                    Puts($"{player.displayName} ({player.UserIDString}) looted the Drop at: {entity.transform.position.ToString("0")} grid area: {gridPos}");
                LootedDrops.Add(entity as SupplyDrop);
                lastLootPos = entity.transform.position;
                return;
            }
        }

        private object getFancyDropTypes()
        {
            if (setupDropTypes != null)
                return setupDropTypes;
            else
                return null;
        }

        private void OnTick()
        {
            if (configData.TimersSettings.useRealtimeTimers)
                OnTickReal();
            if (configData.TimersSettings.useGametimeTimers)
                OnTickServer();
        }

        private void OnTickReal()
        {
            if (lastMinute == DateTime.UtcNow.Minute)
                return;

            lastMinute = DateTime.UtcNow.Minute;
            if (BasePlayer.activePlayerList.Count >= configData.TimersSettings.timersMinPlayers && configData.TimersSettings.realTimers.ContainsKey(DateTime.Now.ToString("HH:mm")))
            {
                string runCmd = (string)configData.TimersSettings.realTimers[DateTime.Now.ToString("HH:mm")];
                if (configData.TimersSettings.logTimersToConsole)
                    Puts($"Run real timer: ({DateTime.Now.ToString("HH:mm")}) {runCmd}");
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "ad." + runCmd);
            }
        }

        private void OnTickServer()
        {
            if (lastHour == Math.Floor(TOD_Sky.Instance.Cycle.Hour))
                return;

            lastHour = Math.Floor(TOD_Sky.Instance.Cycle.Hour);
            if (BasePlayer.activePlayerList.Count >= configData.TimersSettings.timersMinPlayers && configData.TimersSettings.serverTimers.ContainsKey(lastHour.ToString()))
            {
                string runCmd = (string)configData.TimersSettings.serverTimers[lastHour.ToString()];
                if (configData.TimersSettings.logTimersToConsole)
                    Puts($"Run server timer: ({lastHour}) {runCmd}");
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "ad." + runCmd);
            }
        }
        #endregion Oxide Hooks

        #region Plugin Hooks
        private bool IsFancyDrop(CargoPlane plane) => plane == null ? false : plane.GetComponent<DropTiming>();

        private object IsFancyDropType(CargoPlane plane)
        {
            if (plane == null || !plane.GetComponent<DropTiming>())
                return false;
            return (string)plane.GetComponent<DropTiming>().dropsettings["droptype"];
        }

        private void OverrideDropTime(CargoPlane plane, float seconds)
        {
            var dropTiming = plane.GetComponent<DropTiming>();
            if (dropTiming != null)
            {
                dropTiming.TimeOverride(seconds);
            }
        }
        #endregion

        #region ColliderCheck
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!configData.DamageSettings.shootDownDrops || info == null)
                return;

            if (entity is SupplyDrop)
            {
                var drop = entity as SupplyDrop;
                if (drop == null || drop.IsDestroyed)
                    return;

                BaseEntity parachute = _parachute.GetValue(drop) as BaseEntity;
                if (parachute == null || parachute.IsDestroyed)
                    return;

                var col = drop.GetComponent<ColliderCheck>();
                if (col == null)
                    return;

                if (col.hitCounter < configData.DamageSettings.shootDownCount)
                {
                    col.hitCounter++;
                    return;
                }
                parachute.Kill();
                parachute = null;
                drop.GetComponent<Rigidbody>().drag = configData.DamageSettings.dropDragsetting;
                drop.GetComponent<Rigidbody>().mass = configData.DamageSettings.dropMassSetting;
                drop.GetComponent<Rigidbody>().useGravity = true;
                drop.GetComponent<Rigidbody>().isKinematic = false;
                drop.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.Interpolate;
                drop.GetComponent<Rigidbody>().angularDrag = configData.DamageSettings.dropAngularDragSetting;
                drop.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.Continuous;
                col.wasHit = true;
            }
        }

        sealed class ColliderCheck : FacepunchBehaviour
        {
            public bool notifyEnabled = true;
            public bool notifyConsole;
            public Dictionary<string, object> cratesettings;
            public bool landed = false;
            public int hitCounter = 0;
            public bool wasHit;
            public string dropType;
            private BaseEntity parachute;

            private void Awake()
            {
                Instance.NextTick(() =>
                {
                    if (dropType != null)
                    {
                        if (Convert.ToBoolean(cratesettings["useCustomLootTable"]))
                            Instance.SetupContainer(GetComponent<StorageContainer>(), dropType, true);
                        else
                            Instance.SetupContainer(GetComponent<StorageContainer>(), dropType);
                    }
                    else
                        Awake();
                });
            }

            private void PlayerStoppedLooting(BasePlayer player)
            {
                if (!configData.GenericSettings.unlockDropAfterLoot || GetComponent<BaseEntity>().OwnerID == 0uL || player.userID != GetComponent<BaseEntity>().OwnerID)
                    return;

                GetComponent<BaseEntity>().OwnerID = 0uL;
                Instance.LootedDrops.Add(GetComponent<BaseEntity>() as SupplyDrop);
            }

            private void OnCollisionEnter(Collision col)
            {
                if (!landed)
                {
                    landed = true;
                    if (wasHit)
                    {
                        if (configData.AirdropSettings.EffectsSettings.useSupplyDropEffectLanded)
                            Effect.server.Run(EXPLOSION_PREFAB, GetComponent<BaseEntity>().transform.position);

                        if (configData.DamageSettings.explodeImpact)
                        {
                            if (UnityEngine.Random.Range(0, 100) <= configData.DamageSettings.explodeChance)
                            {
                                StartCoroutine(HitRemove(true));
                                return;
                            }
                        }
                        else
                        {
                            StartCoroutine(DeSpawn());
                            return;
                        }
                    }

                    if (configData.AirdropSettings.EffectsSettings.useSupplyDropRocket)
                        Instance.CreateRocket(GetComponent<BaseEntity>().transform.position);

                    if (notifyEnabled && configData.Notifications.notifyDropPlayersOnLanded && dropType != "dropdirect" && (((Instance.lastDropRadius * 2) * 1.2) - Vector3.Distance(Instance.lastDropPos, GetComponent<BaseEntity>().transform.position) <= 0 && !(UnityEngine.CollisionEx.GetEntity(col) is SupplyDrop)))
                        Instance.NotifyOnDropLanded(GetComponent<BaseEntity>());

                    Instance.lastDropPos = GetComponent<BaseEntity>().transform.position;

                    StartCoroutine(DeSpawn());

                    if (configData.DebugSettings.useDebug)
                        Instance.Puts("Debug Info: Drop has landed on the ground.");

                    if (configData.AirdropSettings.EffectsSettings.useSupplyDropEffectLanded)
                        Effect.server.Run(EXPLOSION_PREFAB, GetComponent<BaseEntity>().transform.position);

                    if (notifyConsole && configData.Notifications.notifyDropConsoleOnLanded)
                        Instance.Puts(string.Format(Instance.lang.GetMessage(msgConsoleDropLanded, Instance), GetComponent<BaseEntity>().transform.position.x.ToString("0"), GetComponent<BaseEntity>().transform.position.y.ToString("0"), GetComponent<BaseEntity>().transform.position.z.ToString("0")));
                }
            }

            private IEnumerator HitRemove(bool explode)
            {
                if (explode)
                {
                    yield return new WaitForEndOfFrame();
                    Effect.server.Run("assets/bundled/prefabs/fx/item_break.prefab", GetComponent<BaseEntity>().transform.position);
                    (GetComponent<BaseEntity>() as StorageContainer).DropItems();
                    Instance.SupplyDrops.Remove(GetComponent<SupplyDrop>());
                    cratesettings.Clear();
                    GetComponent<BaseEntity>().Kill();
                }
                else
                {
                    yield return new WaitForEndOfFrame();
                    Instance.SupplyDrops.Remove(GetComponent<SupplyDrop>());
                }
            }

            private IEnumerator DeSpawn()
            {
                yield return new WaitForSeconds(Convert.ToSingle(cratesettings["despawnMinutes"]) * 60.0f);
                yield return new WaitWhile(() => GetComponent<BaseEntity>().IsOpen());
                cratesettings.Clear();
                GetComponent<BaseEntity>().Kill();
            }

            private void OnDestroy()
            {
                cratesettings.Clear();
                //Instance.SupplyDrops.Remove(GetComponent<SupplyDrop>());
                //Instance.LootedDrops.Remove(GetComponent<SupplyDrop>());
            }

            internal void OnGroundCollision()
            {
                parachute.Kill(BaseNetworkable.DestroyMode.None);
            }
            public ColliderCheck() { }
        }
        #endregion ColliderCheck

        #region Drop Timing
        class DropTiming : FacepunchBehaviour
        {
            private int dropCount;
            private float updatedTime;
            public Vector3 startPos;
            public Vector3 endPos;
            public bool notify = true;
            private bool notifyConsole = true;
            private int cratesToDrop;
            public string dropType;
            public Dictionary<string, object> dropsettings;
            public ulong userID;

            private float gapTimeToTake = 0f;
            private float halfTimeToTake = 0f;
            private float offsetTimeToTake = 0f;

            private void Awake()
            {
                dropCount = 0;
                notifyConsole = true;
            }

            public void GetSettings(Dictionary<string, object> drop, Vector3 start, Vector3 end, float seconds)
            {
                dropsettings = new Dictionary<string, object>(drop);
                startPos = start;
                endPos = end;
                gapTimeToTake = Convert.ToSingle(dropsettings["cratesGap"]) / Convert.ToSingle(dropsettings["planeSpeed"]);
                halfTimeToTake = seconds / 2;
                offsetTimeToTake = gapTimeToTake / 2;
                cratesToDrop = Mathf.RoundToInt(UnityEngine.Random.Range(Convert.ToSingle(dropsettings["minCrates"]) * 100f, Convert.ToSingle(dropsettings["maxCrates"]) * 100f) / 100f);

                if ((cratesToDrop % 2) == 0)
                    updatedTime = halfTimeToTake - offsetTimeToTake - ((cratesToDrop - 1) / 2 * gapTimeToTake);
                else
                    updatedTime = halfTimeToTake - ((cratesToDrop - 1) / 2 * gapTimeToTake);

                if (userID != 0ul)
                    userID = Convert.ToUInt64(dropsettings["userID"]);
            }
            public void TimeOverride(float seconds)
            {
                halfTimeToTake = seconds / 2;
                offsetTimeToTake = gapTimeToTake / 2;
                cratesToDrop = Mathf.RoundToInt(UnityEngine.Random.Range(Convert.ToSingle(dropsettings["minCrates"]) * 100f, Convert.ToSingle(dropsettings["maxCrates"]) * 100f) / 100f);

                if ((cratesToDrop % 2) == 0)
                    updatedTime = halfTimeToTake - offsetTimeToTake - ((cratesToDrop - 1) / 2 * gapTimeToTake);
                else
                    updatedTime = halfTimeToTake - ((cratesToDrop - 1) / 2 * gapTimeToTake);
            }
            private void Update()
            {
                if ((float)Instance.dropPlanesecondsTaken.GetValue(GetComponent<CargoPlane>()) > updatedTime && dropCount < cratesToDrop)
                {
                    dropsettings = new Dictionary<string, object>();
                    dropsettings = getObjects(dropType);
                    updatedTime += gapTimeToTake;
                    dropCount++;
                    //Instance.Puts("We Got a UserID? " + dropsettings["userID"].ToString());
                    Instance.createSupplyDrop(GetComponent<CargoPlane>().transform.position, dropsettings, notify, notifyConsole, startPos, endPos, dropType, userID);
                    if (configData.Notifications.notifyDropConsoleSpawned && notifyConsole)
                        Instance.Puts(string.Format(Instance.lang.GetMessage(Instance.msgConsoleDropSpawn, Instance), GetComponent<BaseEntity>().transform.position.x.ToString("0"), GetComponent<BaseEntity>().transform.position.y.ToString("0"), GetComponent<BaseEntity>().transform.position.z.ToString("0")));
                }

                if (dropCount == 1 && notify)
                    notify = false;

                if (dropCount == 1 && configData.Notifications.notifyDropConsoleFirstOnly)
                    notifyConsole = false;
            }

            private void OnDestroy()
            {
                dropsettings.Clear();
                Instance.CargoPlanes.Remove(GetComponent<CargoPlane>());
                Destroy(this);
            }

            public DropTiming() { }
        }
        #endregion Drop Timing

        #region Objects Creation
        CargoPlane createCargoPlane(Vector3 pos = new Vector3()) => (CargoPlane)GameManager.server.CreateEntity(PLANE_PREFAB, new Vector3(), new Quaternion(), true);

        private static void RunEffect(string name, BaseEntity entity = null, Vector3 position = new Vector3(), Vector3 offset = new Vector3())
        {
            if (entity != null)
                Effect.server.Run(name, entity, 0, offset, position, null, true);
            else
                Effect.server.Run(name, position, Vector3.up, null, true);
        }

        private static void CreateSirenLights(BaseEntity entity)
        {
            var SirenLight = GameManager.server.CreateEntity(SIRENLIGHT_EFFECT, default(Vector3), default(Quaternion), true);

            SirenLight.gameObject.Identity();
            SirenLight.SetParent(entity as LootContainer, "parachute_attach");
            SirenLight.Spawn();
            SirenLight.SetFlag(BaseEntity.Flags.Reserved8, true);
        }

        private static void CreateSirenAlarms(BaseEntity entity)
        {
            var SirenAlarm = GameManager.server.CreateEntity(SIRENALARM_EFFECT, new Vector3(0f, 0f, 0f), default(Quaternion), true);

            SirenAlarm.gameObject.Identity();
            SirenAlarm.SetParent(entity);
            SirenAlarm.Spawn();
            SirenAlarm.SetFlag(BaseEntity.Flags.Reserved8, true);
        }

        private void CreateDropEffects(BaseEntity entity)
        {
            if (entity == null)
                return;

            if (configData.AirdropSettings.EffectsSettings.useSirenAlarmOnDrop && configData.AirdropSettings.EffectsSettings.useSirenAlarmAtNightOnly && TOD_Sky.Instance.IsNight)
                CreateSirenAlarms(entity);
            else if (configData.AirdropSettings.EffectsSettings.useSirenAlarmAtNightOnly && TOD_Sky.Instance.IsNight)
                CreateSirenAlarms(entity);
            else if (configData.AirdropSettings.EffectsSettings.useSirenAlarmOnDrop)
                CreateSirenAlarms(entity);
            else
                return;

            if (configData.AirdropSettings.EffectsSettings.useSirenLightOnDrop && configData.AirdropSettings.EffectsSettings.useSirenLightAtNightOnly && TOD_Sky.Instance.IsNight)
                CreateSirenLights(entity);
            else if (configData.AirdropSettings.EffectsSettings.useSirenLightAtNightOnly && TOD_Sky.Instance.IsNight)
                CreateSirenLights(entity);
            else if (configData.AirdropSettings.EffectsSettings.useSirenLightOnDrop)
                CreateSirenLights(entity);
            else
                return;
        }

        private void createSupplyDrop(Vector3 pos, Dictionary<string, object> cratesettings, bool notify = true, bool notifyConsole = true, Vector3 start = new Vector3(), Vector3 end = new Vector3(), string dropType = "regular", ulong userID = 0uL)
        {
            SupplyDrop newDrop;
            object value;
            if (configData.DropSettings.setupDropTypes.ContainsKey(dropType))
            {
                cratesettings = getObjects(dropType);

                if (configData.DebugSettings.useDebug)
                    Puts("Debug Info: Yes we have current settings");
            }
            else
            {
                cratesettings = new Dictionary<string, object>((Dictionary<string, object>)setupDropDefault);

                if (configData.DebugSettings.useDebug)
                    Puts("Debug Info: We are using new settings");
            }

            if (userID != 0uL)
            {
                if (configData.DebugSettings.useDebug)
                    Puts("Debug Info: We got userID and set and locked to " + userID.ToString());

                newDrop = GameManager.server.CreateEntity(SUPPLY_PREFAB, pos, new Quaternion(), true) as SupplyDrop;
                (newDrop as BaseEntity).OwnerID = userID;
            }
            else
            {
                newDrop = GameManager.server.CreateEntity(SUPPLY_PREFAB, pos, Quaternion.LookRotation(end - start), true) as SupplyDrop;
                if (configData.DebugSettings.useDebug)
                    Puts("Debug Info: Drop is currently not locked to any players");
            }

            (newDrop as BaseNetworkable).gameObject.AddComponent<ColliderCheck>();
            newDrop.GetComponent<ColliderCheck>().cratesettings = cratesettings;
            newDrop.GetComponent<ColliderCheck>().notifyEnabled = notify;
            newDrop.GetComponent<ColliderCheck>().notifyConsole = notifyConsole;
            newDrop.GetComponent<ColliderCheck>().dropType = dropType;
            newDrop.GetComponent<Rigidbody>().drag = Convert.ToSingle(cratesettings["crateAirResistance"]);

            if (configData.DebugSettings.useDebug)
            {
                Puts("Debug Info: Supply Drop (after spawning) Resistance Now " + Convert.ToDouble(cratesettings["crateAirResistance"]).ToString());
                Puts("Debug Info: Supply Drop (after spawning) Despawn Time " + cratesettings["despawnMinutes"].ToString());
            }

            newDrop.Spawn();

            int slots = 36;
            newDrop.inventory.capacity = 36;
            newDrop.panelName = "generic";
            newDrop.inventory.ServerInitialize(null, slots);
            newDrop.inventory.MarkDirty();

            Interface.CallHook("OnLootSpawn", new object[] { newDrop.GetComponent<LootContainer>() });
            SupplyDrops.Add(newDrop);

            if (configData.AirdropSettings.EffectsSettings.useSmokeEffectOnDrop)
                RunEffect(SMOKE_EFFECT, newDrop);

            if (configData.AirdropSettings.EffectsSettings.useSirenAlarmOnDrop || configData.AirdropSettings.EffectsSettings.useSirenLightOnDrop && newDrop != null)
            {
                if (newDrop == null)
                    return;

                CreateDropEffects(newDrop);
            }
        }

        private void createSupplyDropSpace(Vector3 pos, Dictionary<string, object> cratesettings, bool notify = true, bool notifyConsole = true, Vector3 start = new Vector3(), Vector3 end = new Vector3(), string dropType = "regular", ulong userID = 0uL)
        {
            SupplyDrop newDrop;
            object value;
            pos.y = 350f;

            if (configData.DropSettings.setupDropTypes.ContainsKey(dropType))
            {
                cratesettings = getObjects(dropType);
                if (configData.DebugSettings.useDebug)
                    Puts("Debug Info: Yes we have current settings");
            }
            else
            {
                cratesettings = new Dictionary<string, object>((Dictionary<string, object>)setupDropDefault);
                if (configData.DebugSettings.useDebug)
                    Puts("Debug Info: We are using new settings");
            }

            if (userID != 0uL)
            {
                if (configData.DebugSettings.useDebug)
                    Puts("Debug Info: We got userID and set and locked to " + userID.ToString());

                newDrop = GameManager.server.CreateEntity(SUPPLY_PREFAB, pos, new Quaternion(), true) as SupplyDrop;
                (newDrop as BaseEntity).OwnerID = userID;
            }
            else
            {
                newDrop = GameManager.server.CreateEntity(SUPPLY_PREFAB, pos, Quaternion.LookRotation(end - start), true) as SupplyDrop;
                if (configData.DebugSettings.useDebug)
                    Puts("Debug Info: Drop is currently not locked to any players");
            }

            (newDrop as BaseNetworkable).gameObject.AddComponent<ColliderCheck>();
            newDrop.GetComponent<ColliderCheck>().cratesettings = cratesettings;
            newDrop.GetComponent<ColliderCheck>().notifyEnabled = notify;
            newDrop.GetComponent<ColliderCheck>().notifyConsole = notifyConsole;
            newDrop.GetComponent<ColliderCheck>().dropType = dropType;
            (newDrop as BaseNetworkable).gameObject.AddComponent<DropBehaviour>();

            Effect.server.Run(BRADLEY_CANNON_EFFECT, newDrop.transform.position, Vector3.zero, null, true);
            newDrop.Invoke(() => Effect.server.Run(HELIEXPLOSION_EFFECT, newDrop.transform.position, Vector3.zero, null, true), 0.5f);
            newDrop.Invoke(() => Effect.server.Run(HELIEXPLOSION_EFFECT, newDrop.transform.position, Vector3.zero, null, true), 0.6f);

            if (configData.DebugSettings.useDebug)
            {
                Puts("Debug Info: Supply Drop (after spawning) Resistance Now " + Convert.ToDouble(cratesettings["crateAirResistance"]).ToString());
                Puts("Debug Info: Supply Drop (after spawning) Despawn Time " + cratesettings["despawnMinutes"].ToString());
            }

            newDrop.Spawn();

            int slots = 36;
            newDrop.inventory.capacity = 36;
            newDrop.panelName = "generic";
            newDrop.inventory.ServerInitialize(null, slots);
            newDrop.inventory.MarkDirty();

            Interface.CallHook("OnLootSpawn", new object[] { newDrop.GetComponent<LootContainer>() });
            SupplyDrops.Add(newDrop);

            if (configData.AirdropSettings.EffectsSettings.useSmokeEffectOnDrop)
                RunEffect(SMOKE_EFFECT, newDrop);

            if (configData.AirdropSettings.EffectsSettings.useSirenAlarmOnDrop || configData.AirdropSettings.EffectsSettings.useSirenLightOnDrop && newDrop != null)
            {
                if (newDrop == null)
                    return;

                CreateDropEffects(newDrop);
            }
        }

        private void SpawnNetworkable(BaseNetworkable ent)
        {
            if (ent.GetComponent<UnityEngine.Component>().transform.root != ent.GetComponent<UnityEngine.Component>().transform)
                ent.GetComponent<UnityEngine.Component>().transform.parent = null;

            Rust.Registry.Entity.Register(ent.GetComponent<UnityEngine.Component>().gameObject, ent);

            if (ent.net == null)
                ent.net = Network.Net.sv.CreateNetworkable();

            ent.net.handler = ent;
            _creationFrame.SetValue(ent, Time.frameCount);
            ent.PreInitShared();
            ent.InitShared();
            ent.ServerInit();
            ent.PostInitShared();
            ent.UpdateNetworkGroup();
            _isSpawned.SetValue(ent, true);
            Interface.CallHook("OnEntitySpawned", ent);
            ent.SendNetworkUpdateImmediate(true);
        }

        private void CreateRocket(Vector3 startPoint)
        {
            BaseEntity entity = null;
            if (TOD_Sky.Instance.IsNight)
                entity = GameManager.server.CreateEntity(HELIBURST_PREFAB, startPoint + new Vector3(0, 10, 0), new Quaternion(), true);
            else
                entity = GameManager.server.CreateEntity(ROCKETSMOKE_PREFAB, startPoint + new Vector3(0, 10, 0), new Quaternion(), true);

            entity.GetComponent<TimedExplosive>().timerAmountMin = configData.AirdropSettings.EffectsSettings.signalRocketExplosionTime;
            entity.GetComponent<TimedExplosive>().timerAmountMax = configData.AirdropSettings.EffectsSettings.signalRocketExplosionTime;
            entity.GetComponent<ServerProjectile>().gravityModifier = 0f;
            entity.GetComponent<ServerProjectile>().speed = configData.AirdropSettings.EffectsSettings.signalRocketSpeed;
            for (int i = 0; i < entity.GetComponent<TimedExplosive>().damageTypes.Count; i++)
            {
                entity.GetComponent<TimedExplosive>().damageTypes[i].amount *= 0f;
            }
            entity.SendMessage("InitializeVelocity", Vector3.up * 2f);
            entity.Spawn();
        }

        private static Dictionary<string, object> getObjects(string name)
        {
            Dictionary<string, object> dropsettings = new Dictionary<string, object>();
            if (configData.DropSettings.setupDropTypes.ContainsKey(name))
            {
                var json = JsonConvert.SerializeObject(configData.DropSettings.setupDropTypes[name]);
                dropsettings = JsonConvert.DeserializeObject<Dictionary<string, object>>(json) as Dictionary<string, object>;
            }
            return dropsettings;
        }

        public static Dictionary<string, TValue> ToDictionary<TValue>(object obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            var dictionary = JsonConvert.DeserializeObject<Dictionary<string, TValue>>(json);
            return dictionary;
        }

        private void startCargoPlane(Vector3 dropToPos = new Vector3(), bool randomDrop = true, CargoPlane plane = null, string dropType = "regular", string staticList = "", bool showinfo = true, ulong userID = 0uL)
        {
            Dictionary<string, object> dropsettings = new Dictionary<string, object>();
            int speed = 35;
            int additionalheight = 0;
            if (configData.DropSettings.setupDropTypes.ContainsKey(dropType))
            {
                dropsettings = getObjects(dropType);
            }
            else
                dropsettings = new Dictionary<string, object>((Dictionary<string, object>)setupDropDefault);

            if (userID != 0uL)
            {
                if (configData.DebugSettings.useDebug)
                    Puts("Debug Info: UserID received and set as " + userID.ToString());
                dropsettings.Add("userID", userID);
            }

            if (!dropsettings.ContainsKey("droptype"))
                dropsettings.Add("droptype", dropType);

            if (staticList != "")
                dropsettings["CustomLootListName"] = staticList;
            if (Convert.ToInt32(dropsettings["planeSpeed"]) < 20)
                dropsettings["planeSpeed"] = 20;
            if (Convert.ToSingle(dropsettings["crateAirResistance"]) < 0.6)
                dropsettings["crateAirResistance"] = 0.6;
            if (Convert.ToInt32(dropsettings["despawnMinutes"]) < 1)
                dropsettings["despawnMinutes"] = 1;
            if (Convert.ToInt32(dropsettings["cratesGap"]) < 5)
                dropsettings["cratesGap"] = 5;
            if (Convert.ToInt32(dropsettings["minCrates"]) < 1)
                dropsettings["minCrates"] = 1;
            if (Convert.ToInt32(dropsettings["maxCrates"]) < 1)
                dropsettings["maxCrates"] = 1;

            speed = Convert.ToInt32(dropsettings["planeSpeed"]);
            additionalheight = Convert.ToInt32(dropsettings["additionalheight"]);

            object isSupplyDropActive = Interface.Oxide.CallHook("isSupplyDropActive");
            if (dropsettings.ContainsKey("betterloot"))
                dropsettings["betterloot"] = isSupplyDropActive != null && (bool)isSupplyDropActive ? true : false;
            string notificationInfo = "";

            if (dropsettings.ContainsKey("notificationInfo"))
                notificationInfo = (string)dropsettings["notificationInfo"];
            if (plane == null)
                plane = createCargoPlane();
            if (randomDrop)
                dropToPos = plane.RandomDropPosition();

            float x = TerrainMeta.Size.x;
            float y;
            y = (TerrainMeta.HighestPoint.y * configData.AirdropSettings.planeOffSetYMultiply) + additionalheight;
            Vector3 startPos = Vector3Ex.Range(-1f, 1f);
            startPos.y = 0f;
            startPos.Normalize();
            startPos *= x * configData.AirdropSettings.planeOffSetXMultiply;
            startPos.y = y;
            Vector3 endPos = startPos * -1f;
            endPos.y = startPos.y;
            startPos += dropToPos;
            endPos += dropToPos;
            float secondsToTake = Vector3.Distance(startPos, endPos) / speed;
            plane.gameObject.AddComponent<DropTiming>();
            plane.GetComponent<DropTiming>().dropType = dropType;
            plane.GetComponent<DropTiming>().userID = userID;
            plane.GetComponent<DropTiming>().GetSettings(dropsettings, startPos, endPos, secondsToTake);

            if (configData.DebugSettings.useDebug)
            {
                Puts("Debug Info: Plane Height Setting " + dropsettings["additionalheight"].ToString());
                Puts("Debug Info: Plane Speed Setting " + dropsettings["planeSpeed"].ToString());
                Puts("Debug Info: Supply Drop Despawn Setting " + dropsettings["despawnMinutes"].ToString());
                Puts("Debug Info: Supply Drop Resistance Setting " + dropsettings["crateAirResistance"].ToString());
                Puts("Debug Info: Notification Info " + notificationInfo);
                if (userID != 0uL)
                    Puts("Debug Info: UserID Set to " + dropsettings["userID"].ToString());
            }

            dropsettings.Clear();
            dropPlanedropped.SetValue(plane, true);
            plane.InitDropPosition(dropToPos);

            if (!CargoPlanes.Contains(plane))
            {
                if ((plane as BaseNetworkable).net == null)
                    (plane as BaseNetworkable).net = Network.Net.sv.CreateNetworkable();
                CargoPlanes.Add(plane);
            }

            (plane as BaseNetworkable).limitNetworking = true;

            if ((int)_creationFrame.GetValue(plane) == 0)
                plane.Spawn();

            plane.transform.position = startPos;
            plane.transform.rotation = Quaternion.LookRotation(endPos - startPos);
            dropPlanestartPos.SetValue(plane, startPos);
            dropPlaneendPos.SetValue(plane, endPos);
            dropPlanesecondsToTake.SetValue(plane, secondsToTake);
            (plane as BaseNetworkable).limitNetworking = false;

            if (showinfo)
                DropNotifier(dropToPos, dropType, staticList, notificationInfo);
        }

        private static string GetGridString(Vector3 position) => PhoneController.PositionToGridCoord(position);

        private void DropNotifier(Vector3 dropToPos, string dropType, string staticList, string notificationInfo)
        {
            string gridPos = GetGridString(dropToPos);

            if (dropType == "dropdirect")
                return;

            else if (dropType == "supplysignal")
            {
                if (configData.Notifications.notifyDropServerSignal)
                    if (configData.Notifications.notifyDropGUI && configData.UISettings.SimpleUI_Enable)
                        if (configData.Notifications.notifyDropServerSignalCoords)
                            foreach (var player in BasePlayer.activePlayerList)
                                MessageToPlayerUI(player, string.Format(msg("msgDropSignalCoords", player.UserIDString), dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
                        else if (configData.Notifications.notifyDropServerSignalGrid)
                            foreach (var player in BasePlayer.activePlayerList)
                                MessageToPlayerUI(player, string.Format(msg("msgDropSignalGrid", player.UserIDString), gridPos));
                        else
                            foreach (var player in BasePlayer.activePlayerList)
                                MessageToPlayerUI(player, string.Format(msg("msgDropSignal", player.UserIDString)));
                    else if (configData.Notifications.notifyDropGUI && GUIAnnouncements)
                        if (configData.Notifications.notifyDropServerSignalCoords)
                            MessageToAllGui(string.Format(msg("msgDropSignalCoords"), dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
                        else if (configData.Notifications.notifyDropServerSignalGrid)
                            MessageToAllGui(string.Format(msg("msgDropSignalGrid"), gridPos));
                        else
                            MessageToAllGui(msg("msgDropSignal"));
                    else
                        if (configData.Notifications.notifyDropServerSignalCoords)
                        foreach (var player in BasePlayer.activePlayerList)
                            MessageToPlayer(player, string.Format(msg("msgDropSignalCoords", player.UserIDString), dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
                    else if (configData.Notifications.notifyDropServerSignalGrid)
                        foreach (var player in BasePlayer.activePlayerList)
                            MessageToPlayer(player, string.Format(msg("msgDropSignalGrid", player.UserIDString), gridPos));
                    else
                        foreach (var player in BasePlayer.activePlayerList)
                            MessageToPlayer(player, msg("msgDropSignal", player.UserIDString));
                return;
            }
            else if (dropType == "regular")
            {
                if (configData.Notifications.notifyDropServerRegular)
                    if (configData.Notifications.notifyDropGUI && configData.UISettings.SimpleUI_Enable)
                        if (configData.Notifications.notifyDropServerRegularCoords)
                            foreach (var player in BasePlayer.activePlayerList)
                                MessageToPlayerUI(player, string.Format(msg("msgDropRegularCoords", player.UserIDString), dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
                        else if (configData.Notifications.notifyDropServerRegularGrid)
                            foreach (var player in BasePlayer.activePlayerList)
                                MessageToPlayerUI(player, string.Format(msg("msgDropRegularGrid", player.UserIDString), gridPos));
                        else
                            foreach (var player in BasePlayer.activePlayerList)
                                MessageToPlayerUI(player, msg("msgDropRegular", player.UserIDString));
                    else if (configData.Notifications.notifyDropGUI && GUIAnnouncements)
                        if (configData.Notifications.notifyDropServerRegularCoords)
                            MessageToAllGui(string.Format(msg("msgDropRegularCoords"), dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
                        else if (configData.Notifications.notifyDropServerRegularGrid)
                            MessageToAllGui(string.Format(msg("msgDropRegularGrid"), gridPos));
                        else
                            MessageToAllGui(msg("msgDropRegular"));
                    else
                        if (configData.Notifications.notifyDropServerRegularCoords)
                        foreach (var player in BasePlayer.activePlayerList)
                            MessageToPlayer(player, string.Format(msg("msgDropRegularCoords", player.UserIDString), dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
                    else if (configData.Notifications.notifyDropServerRegularGrid)
                        foreach (var player in BasePlayer.activePlayerList)
                            MessageToPlayer(player, string.Format(msg("msgDropRegularGrid", player.UserIDString), gridPos));
                    else
                        foreach (var player in BasePlayer.activePlayerList)
                            MessageToPlayer(player, msg("msgDropRegular", player.UserIDString));
                return;
            }
            else if (dropType == "massdrop")
            {
                if (configData.Notifications.notifyDropServerMass)
                    if (configData.Notifications.notifyDropGUI && configData.UISettings.SimpleUI_Enable)
                        foreach (var player in BasePlayer.activePlayerList)
                            MessageToPlayerUI(player, msg("msgDropMass", player.UserIDString));
                    else if (configData.Notifications.notifyDropGUI && GUIAnnouncements)
                        MessageToAllGui(msg("msgDropMass"));
                    else
                        foreach (var player in BasePlayer.activePlayerList)
                            MessageToPlayer(player, msg("msgDropMass", player.UserIDString));
                return;
            }
            else
            {
                if (configData.Notifications.notifyDropServerCustom)
                    if (configData.Notifications.notifyDropGUI && configData.UISettings.SimpleUI_Enable)
                        if (configData.Notifications.notifyDropServerCustomCoords && _massDropTimer != null && _massDropTimer.Repetitions == 0)
                            foreach (var player in BasePlayer.activePlayerList)
                                MessageToPlayerUI(player, string.Format(msg("msgDropCustomCoords", player.UserIDString), notificationInfo, dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
                        else if (configData.Notifications.notifyDropServerCustomGrid && _massDropTimer != null && _massDropTimer.Repetitions == 0)
                            foreach (var player in BasePlayer.activePlayerList)
                                MessageToPlayerUI(player, string.Format(msg("msgDropCustomGrid", player.UserIDString), notificationInfo, gridPos));
                        else
                            foreach (var player in BasePlayer.activePlayerList)
                                MessageToPlayerUI(player, string.Format(msg("msgDropCustom", player.UserIDString), notificationInfo));
                    else if (configData.Notifications.notifyDropGUI && GUIAnnouncements)
                        if (configData.Notifications.notifyDropServerCustomCoords && _massDropTimer != null && _massDropTimer.Repetitions == 0)
                            MessageToAllGui(string.Format(msg("msgDropCustomCoords"), notificationInfo, dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
                        else if (configData.Notifications.notifyDropServerCustomGrid && _massDropTimer != null && _massDropTimer.Repetitions == 0)
                            MessageToAllGui(string.Format(msg("msgDropCustoGrid"), notificationInfo, gridPos));
                        else
                            MessageToAllGui(string.Format(msg("msgDropCustom"), notificationInfo));
                    else
                        if (configData.Notifications.notifyDropServerCustomCoords && _massDropTimer != null && _massDropTimer.Repetitions == 0)
                        foreach (var player in BasePlayer.activePlayerList)
                            MessageToPlayer(player, string.Format(msg("msgDropCustomCoords", player.UserIDString), notificationInfo, dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
                    else if (configData.Notifications.notifyDropServerCustomGrid && _massDropTimer != null && _massDropTimer.Repetitions == 0)
                        foreach (var player in BasePlayer.activePlayerList)
                            MessageToPlayer(player, string.Format(msg("msgDropCustomGrid", player.UserIDString), notificationInfo, gridPos));
                    else
                        foreach (var player in BasePlayer.activePlayerList)
                            MessageToPlayer(player, string.Format(msg("msgDropCustom", player.UserIDString), notificationInfo));
            }
        }
        #endregion ObjectsCreation        

        #region Airdrop Timers
        private void airdropTimerNext(int custom = 0)
        {
            if (configData.TimerSettings.airdropTimerEnabled)
            {
                int delay;
                airdropTimerStop();
                if (custom == 0)
                    delay = UnityEngine.Random.Range(configData.TimerSettings.airdropTimerWaitMinutesMin, configData.TimerSettings.airdropTimerWaitMinutesMax);
                else
                    delay = custom;
                _aidropTimer = timer.Once(delay * 60, airdropTimerRun);
                if (configData.Notifications.notifyDropConsoleRegular)
                    Puts($"Next timed Airdrop in {delay.ToString()} minutes");
            }
        }

        private void airdropTimerRun()
        {
            var playerCount = BasePlayer.activePlayerList.Count;
            if (playerCount >= configData.TimerSettings.airdropTimerMinPlayers)
            {
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "ad." + configData.TimerSettings.airdropTimerCmd.Replace("ad.", ""));
                if (configData.Notifications.notifyDropConsoleRegular)
                    Puts($"Timed Airdrop initiated with command '{"ad." + configData.TimerSettings.airdropTimerCmd.Replace("ad.", "")}'");
            }
            else
            {
                if (configData.Notifications.notifyDropConsoleRegular)
                    Puts("Timed Airdrop skipped, not enough Players");
            }
            airdropTimerNext();
        }

        private void airdropTimerStop()
        {
            if (_aidropTimer == null || _aidropTimer.Destroyed)
                return;

            _aidropTimer.Destroy();
            _aidropTimer = null;
        }

        private void removeBuiltInAirdrop()
        {
            var triggeredEvents = UnityEngine.Object.FindObjectsOfType<TriggeredEventPrefab>();
            var planePrefab = triggeredEvents.Where(e => e.targetPrefab != null && e.targetPrefab.guid.Equals("8429b072581d64747bfe17eab7852b42")).ToList();
            foreach (var prefab in planePrefab)
            {
                Puts("Builtin Airdrop removed");
                UnityEngine.Object.Destroy(prefab);
            }
        }
        #endregion Airdrop Timers

        #region FindPlayer
        private static BasePlayer FindPlayerByName(string name)
        {
            BasePlayer result = null;
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                if (current.displayName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    BasePlayer result2 = current;
                    return result2;
                }

                if (current.UserIDString.Contains(name, CompareOptions.OrdinalIgnoreCase))
                {
                    BasePlayer result2 = current;
                    return result2;
                }

                if (current.displayName.Contains(name, CompareOptions.OrdinalIgnoreCase))
                {
                    result = current;
                }
            }
            return result;
        }
        #endregion FindPlayer        

        #region Setup Loot
        private void ClearContainer(ItemContainer itemContainer)
        {
            if (itemContainer == null || itemContainer.itemList == null)
                return;

            while (itemContainer.itemList.Count > 0)
            {
                var item = itemContainer.itemList[0];
                item.RemoveFromContainer();
                item.Remove(0f);
                item.DoRemove();
            }
            Puts("Cleared Container");
        }

        private void FillLootContainer(BaseEntity Container, string dropType)
        {
            if (Container == null)
                return;

            ItemContainer itemContainer = Container.GetComponent<StorageContainer>()?.inventory;
            if (itemContainer == null)
                return;

            bool exitloop = false;
            foreach (var dtn in configData.StaticItems.LootSettings)
            {
                switch (dtn.DropTypeName)
                {
                    case "regular":
                    case "massdrop":
                    case "dropdirect":
                    case "supplysignal":
                    case "custom_event":
                        if (dtn.DropTypeName == dropType)
                        {

                            int count = UnityEngine.Random.Range(dtn.MinimumItems, dtn.MaximumItems);

                            if (itemContainer.capacity < count)
                                itemContainer.capacity = count;

                            List<ConfigData.StaticOptions.LootOptions.LootItem> Items = new List<ConfigData.StaticOptions.LootOptions.LootItem>(dtn.Items);
                            for (int i = 0; i < count; i++)
                            {
                                if (Items.Count == 0)
                                    Items = new List<ConfigData.StaticOptions.LootOptions.LootItem>(dtn.Items);

                                ConfigData.StaticOptions.LootOptions.LootItem lootItem = Items.GetRandom();
                                if (lootItem == null)
                                {
                                    count--;
                                    continue;
                                }

                                Item item = null;
                                item = ItemManager.CreateByName(lootItem.Name, 1, lootItem.Skin);

                                if (item != null)
                                {
                                    item.amount = UnityEngine.Random.Range(lootItem.Minimum, lootItem.Maximum);
                                    item.MarkDirty();
                                    if (lootItem.DisplayName != null)
                                        item.name = lootItem.DisplayName;

                                    item.MoveToContainer(itemContainer, -1, false);
                                }
                                Items.Remove(lootItem);
                            }
                            exitloop = true;
                        }
                        break;
                    default:
                        break;
                }
                if (exitloop) break;
            }
        }

        //private void SetupContainer(StorageContainer drop, Dictionary<string, object> setup)
        private void SetupContainer(StorageContainer drop, string dropType, bool CustomLoot = false)
        {
            ItemContainer itemContainer = drop.GetComponent<StorageContainer>()?.inventory;

            if (Instance.AlphaLoot != null && Instance.AlphaLoot.Call("WantsToHandleFancyDropLoot") != null)
                return;

            if (Instance.MagicLoot != null && Instance.MagicLoot.CallHook("OnLootSpawn", new object[] { drop.GetComponent<LootContainer>() }) != null)
                return;

            if (dropType != null && CustomLoot)
            {
                ClearContainer(itemContainer);
                FillLootContainer(drop, dropType);
                return;
            }
        }
        #endregion Setup Loot

        #region SimpleUI
        private class UIColor
        {
            string color;

            public UIColor(double red, double green, double blue, double alpha)
            {
                color = $"{red} {green} {blue} {alpha}";
            }

            public override string ToString() => color;
        }

        private class UIObject
        {
            List<object> ui = new List<object>();
            List<string> objectList = new List<string>();

            public UIObject()
            {
            }

            public void Draw(BasePlayer player)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", JsonConvert.SerializeObject(ui).Replace("{NEWLINE}", Environment.NewLine));
            }

            public void Destroy(BasePlayer player)
            {
                foreach (string uiName in objectList)
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", uiName);
            }

            public string AddText(string name, double left, double top, double width, double height, string color, string text, int textsize = 15, string parent = "Hud", int alignmode = 0, float fadeIn = 0f, float fadeOut = 0f)
            {
                text = text.Replace("\n", "{NEWLINE}");
                string align = "";

                switch (alignmode)
                {
                    case 0: { align = "LowerCenter"; break; };
                    case 1: { align = "LowerLeft"; break; };
                    case 2: { align = "LowerRight"; break; };
                    case 3: { align = "MiddleCenter"; break; };
                    case 4: { align = "MiddleLeft"; break; };
                    case 5: { align = "MiddleRight"; break; };
                    case 6: { align = "UpperCenter"; break; };
                    case 7: { align = "UpperLeft"; break; };
                    case 8: { align = "UpperRight"; break; };
                }

                ui.Add(new Dictionary<string, object> {
                    {"name", name},
                    {"parent", parent},
                    {"fadeOut", fadeOut.ToString()},
                    {"components",
                        new List<object> {
                            new Dictionary<string, string> {
                                {"type", "UnityEngine.UI.Text"},
                                {"text", text},
                                {"fontSize", textsize.ToString()},
                                {"color", color.ToString()},
                                {"align", align},
                                {"fadeIn", fadeIn.ToString()}
                            },
                            new Dictionary<string, string> {
                                {"type", "RectTransform"},
                                {"anchormin", $"{left} {((1 - top) - height)}"},
                                {"anchormax", $"{(left + width)} {(1 - top)}"}
                            }
                        }
                    }
                });

                objectList.Add(name);
                return name;
            }
        }

        private void UIMessage(BasePlayer player, string message)
        {
            bool replaced = false;
            float fadeIn = 0.2f;
            Timer playerTimer;

            timers.TryGetValue(player, out playerTimer);
            if (playerTimer != null && !playerTimer.Destroyed)
            {
                playerTimer.Destroy();
                fadeIn = 0.1f;
                replaced = true;
            }

            UIObject ui = new UIObject();

            ui.AddText("Notice_DropShadow", configData.UISettings.SimpleUI_Left + 0.002, configData.UISettings.SimpleUI_Top + 0.002, configData.UISettings.SimpleUI_MaxWidth, configData.UISettings.SimpleUI_MaxHeight, configData.UISettings.SimpleUI_ShadowColor, StripTags(message), configData.UISettings.SimpleUI_FontSize, "Hud", 3, fadeIn, 0.2f);
            ui.AddText("Notice", configData.UISettings.SimpleUI_Left, configData.UISettings.SimpleUI_Top, configData.UISettings.SimpleUI_MaxWidth, configData.UISettings.SimpleUI_MaxHeight, configData.UISettings.SimpleUI_NoticeColor, message, configData.UISettings.SimpleUI_FontSize, "Hud", 3, fadeIn, 0.2f);

            ui.Destroy(player);

            if (replaced)
            {
                timer.Once(0.1f, () =>
                {
                    ui.Draw(player);
                    timers[player] = timer.Once(configData.UISettings.SimpleUI_HideTimer, () => ui.Destroy(player));
                });
            }
            else
            {
                ui.Draw(player);
                timers[player] = timer.Once(configData.UISettings.SimpleUI_HideTimer, () => ui.Destroy(player));
            }
        }

        private string StripTags(string original)
        {
            foreach (string tag in tags)
                original = original.Replace(tag, "");

            foreach (Regex regexTag in regexTags)
                original = regexTag.Replace(original, "");

            return original;
        }
        #endregion SimpleUI

        #region Space Drop Component
        private class DropBehaviour : MonoBehaviour
        {
            private SupplyDrop sDrop;
            private Rigidbody rBody;
            private Transform transForm;

            private float heightAtPos;
            private float distToTarget;
            private bool DeployedChute = false;

            private BaseEntity parachute;
            private Vector3 velocityDrop;

            private void Awake()
            {
                sDrop = GetComponent<SupplyDrop>();
                rBody = GetComponent<Rigidbody>();
                transForm = sDrop.transform;
            }

            private void Start()
            {
                sDrop.RemoveParachute();

                rBody.isKinematic = false;
                rBody.useGravity = true;
                rBody.mass = 1.25f;
                rBody.interpolation = RigidbodyInterpolation.Interpolate;
                rBody.drag = 0.1f;
                rBody.angularDrag = 0.1f;
                rBody.AddForce(Vector3.down * configData.AirdropSettings.SpaceSettings.dropVelocity, ForceMode.Impulse);

                if (Physics.Raycast(transForm.position + Vector3.down, Vector3.down, out rayCastHit, 500f, CAST_LAYERS, QueryTriggerInteraction.Collide))
                    heightAtPos = rayCastHit.point.y;
                else
                    heightAtPos = TerrainMeta.HeightMap.GetHeight(transForm.position);
            }

            private void Update()
            {
                distToTarget = transForm.position.y - heightAtPos;

                if (distToTarget < configData.AirdropSettings.SpaceSettings.dropGroundDistance && distToTarget > 2f)
                {
                    if (!DeployedChute)
                    {
                        parachute = GameManager.server.CreateEntity(PARACHUTE_PREFAB, transForm.position, Quaternion.identity);
                        parachute.SetParent(sDrop, "parachute_attach", false, false);
                        parachute.transform.localPosition = Vector3.zero;
                        parachute.transform.localRotation = Quaternion.identity;
                        parachute.enableSaving = false;
                        parachute.Spawn();

                        velocityDrop = rBody.velocity;

                        DeployedChute = true;
                    }

                    rBody.velocity = Vector3.Lerp(velocityDrop, Vector3.zero, 1f - Mathf.InverseLerp(0f, configData.AirdropSettings.SpaceSettings.dropGroundDistance, distToTarget));
                }

                if (distToTarget < 1f)
                {
                    if (DeployedChute)
                    {
                        sDrop.RemoveParachute();
                        parachute.Kill(BaseNetworkable.DestroyMode.None);
                    }
                    DeployedChute = false;
                    return;
                }
            }

            private void OnGroundCollision()
            {
                sDrop.RemoveParachute();
                parachute.Kill(BaseNetworkable.DestroyMode.None);
                Destroy(this);
            }
        }
        #endregion

        #region Commands

        #region Console Commands
        [ConsoleCommand("ad.random")]
        private void dropRandom(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < configData.GenericSettings.neededAuthLvl) return;
            var plane = createCargoPlane();
            Vector3 newpos = plane.RandomDropPosition();
            newpos.y += TerrainMeta.HeightMap.GetHeight(newpos);
            string gridPos = GetGridString(newpos);
            string type = "regular";
            if (arg.Args != null && arg.Args.Length > 0)
                if (configData.DropSettings.setupDropTypes.ContainsKey(arg.Args[0]))
                    type = arg.Args[0];
                else
                {
                    SendReply(arg, "Droptype not found");
                    return;
                }
            string list = "";
            if (arg.Args != null && arg.Args.Length > 1)
            {
                bool exitloop = false;
                foreach (var dtn in configData.StaticItems.LootSettings)
                {
                    switch (dtn.DropTypeName)
                    {
                        case "regular":
                        case "massdrop":
                        case "dropdirect":
                        case "supplysignal":
                        case "custom_event":
                            if (dtn.DropTypeName == arg.Args[1])
                            {
                                list = arg.Args[1];
                                if (configData.DebugSettings.useDebug) Puts("List " + arg.Args[1].ToString());
                                exitloop = true;
                            }
                            else
                            {
                                SendReply(arg, string.Format("Static itemlist not found"));
                                exitloop = true;
                                return;
                            }
                            break;
                        default:
                            break;
                    }
                    if (exitloop) break;
                }
            }
            startCargoPlane(newpos, false, plane, type, list);
            if (list == "")
                SendReply(arg, $"Random Airdrop of type '{type}' incoming at: {newpos.ToString("0")} grid area: {gridPos}");
            else
                SendReply(arg, $"Random Airdrop of type '{type}|{list}' incoming at: {newpos.ToString("0")} grid area: {gridPos}");
            if (configData.TimerSettings.airdropTimerEnabled && configData.TimerSettings.airdropTimerResetAfterRandom)
                airdropTimerNext();
        }

        [ConsoleCommand("ad.topos")]
        private void dropToPos(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < configData.GenericSettings.neededAuthLvl) return;
            if (arg.Args == null || arg.Args.Length < 2)
            {
                SendReply(arg, "Please specity location with X and Z coordinates as integer");
                return;
            }
            string type = "regular";
            if (arg.Args != null && arg.Args.Length > 2)
                if (configData.DropSettings.setupDropTypes.ContainsKey(arg.Args[2]))
                    type = arg.Args[2];
                else
                {
                    SendReply(arg, "Droptype not found");
                    return;
                }
            string list = "";
            if (arg.Args != null && arg.Args.Length > 3)
            {
                bool exitloop = false;
                foreach (var dtn in configData.StaticItems.LootSettings)
                {
                    switch (dtn.DropTypeName)
                    {
                        case "regular":
                        case "massdrop":
                        case "dropdirect":
                        case "supplysignal":
                        case "custom_event":
                            if (dtn.DropTypeName == arg.Args[3])
                            {
                                list = arg.Args[3];
                                if (configData.DebugSettings.useDebug) Puts("List " + arg.Args[3].ToString());
                                exitloop = true;
                            }
                            else
                            {
                                SendReply(arg, string.Format("Cusatom Loot list not found"));
                                exitloop = true;
                                return;
                            }
                            break;
                        default:
                            break;
                    }
                    if (exitloop) break;
                }
            }
            Vector3 newpos = new Vector3(Convert.ToInt32(arg.Args[0]), 0, Convert.ToInt32(arg.Args[1]));
            newpos.y += TerrainMeta.HeightMap.GetHeight(newpos);
            string gridPos = GetGridString(newpos);
            startCargoPlane(newpos, false, null, type, list);
            if (list == "")
                SendReply(arg, $"Airdrop of type '{type}' started to: {newpos.ToString("0")} grid area: {gridPos}");
            else
                SendReply(arg, $"Airdrop of type '{type}|{list}' started to: {newpos.ToString("0")} grid area: {gridPos}");
        }

        [ConsoleCommand("ad.massdrop")]
        private void dropMass(ConsoleSystem.Arg arg)
        {
            int drops = 0;
            if (arg.Connection != null && arg.Connection.authLevel < configData.GenericSettings.neededAuthLvl) return;
            if (arg.Args == null)
                drops = configData.AirdropSettings.airdropMassdropDefault;
            else if (arg.Args != null && arg.Args.Length >= 1)
            {
                int.TryParse(arg.Args[0], out drops);
                if (drops == 0)
                {
                    SendReply(arg, "Massdrop value has to be an integer number");
                    return;
                }
            }
            string type = "massdrop";
            if (arg.Args != null && arg.Args.Length > 1)
                if (configData.DropSettings.setupDropTypes.ContainsKey(arg.Args[1]))
                    type = arg.Args[1];
                else
                {
                    SendReply(arg, string.Format("Droptype not found"));
                    return;
                }
            string list = "";
            if (arg.Args != null && arg.Args.Length > 2)
            {
                bool exitloop = false;
                foreach (var dtn in configData.StaticItems.LootSettings)
                {
                    switch (dtn.DropTypeName)
                    {
                        case "regular":
                        case "massdrop":
                        case "dropdirect":
                        case "supplysignal":
                        case "custom_event":
                            if (dtn.DropTypeName == arg.Args[2])
                            {
                                list = arg.Args[2];
                                if (configData.DebugSettings.useDebug) Puts("List " + arg.Args[2].ToString());
                                exitloop = true;
                            }
                            else
                            {
                                SendReply(arg, string.Format("Custom Loot list not found"));
                                exitloop = true;
                                return;
                            }
                            break;
                        default:
                            break;
                    }
                    if (exitloop) break;
                }
            }
            if (list == "")
                SendReply(arg, $"Massdrop started with {drops.ToString()} Drops of type '{type}'");
            else
                SendReply(arg, $"Massdrop started with {drops.ToString()} Drops of type '{type}|{list}'");
            if (_massDropTimer != null && !_massDropTimer.Destroyed)
                _massDropTimer.Destroy();
            bool showinfo = true;
            _massDropTimer = timer.Repeat(configData.AirdropSettings.airdropMassdropDelay, drops + 1, () => {
                if (_massDropTimer == null || _massDropTimer.Destroyed) return;
                startCargoPlane(Vector3.zero, true, null, type, list, showinfo);
                if (_massDropTimer.Repetitions == drops) showinfo = false;
            });
        }

        [ConsoleCommand("ad.massdropto")]
        private void dropMassTo(ConsoleSystem.Arg arg)
        {
            int drops = configData.AirdropSettings.airdropMassdropDefault;
            float x = -99999;
            float z = -99999;
            float radius = configData.AirdropSettings.airdropMassdropRadiusDefault;
            if ((arg.Connection != null && arg.Connection.authLevel < configData.GenericSettings.neededAuthLvl) || arg.Args == null) return;
            if (arg.Args.Length < 2)
            {
                SendReply(arg, "Specify at minumum (X) and (Z)");
                return;
            }
            if (arg.Args.Length >= 3) int.TryParse(arg.Args[2], out drops);

            if (!float.TryParse(arg.Args[0], out x) || !float.TryParse(arg.Args[1], out z))
            {
                SendReply(arg, "Specify at minumum (X) (Z) or with '0 0' for random position | and opt:drop-count and opt:radius )");
                return;
            }
            Vector3 newpos = new Vector3();
            if (x == 0 || z == 0)
            {
                newpos = referencePlane.RandomDropPosition();
                x = newpos.x;
                z = newpos.z;
            }
            if (arg.Args.Length > 3) float.TryParse(arg.Args[3], out radius);
            lastDropRadius = radius;
            string type = "massdrop";
            if (arg.Args != null && arg.Args.Length > 4)
                if (configData.DropSettings.setupDropTypes.ContainsKey(arg.Args[4]))
                    type = arg.Args[4];
                else
                {
                    SendReply(arg, string.Format("Droptype not found"));
                    return;
                }
            string list = "";
            if (arg.Args != null && arg.Args.Length > 5)
            {
                bool exitloop = false;
                foreach (var dtn in configData.StaticItems.LootSettings)
                {
                    switch (dtn.DropTypeName)
                    {
                        case "regular":
                        case "massdrop":
                        case "dropdirect":
                        case "supplysignal":
                        case "custom_event":
                            if (dtn.DropTypeName == arg.Args[5])
                            {
                                list = arg.Args[5];
                                if (configData.DebugSettings.useDebug) Puts("List " + arg.Args[5].ToString());
                                exitloop = true;
                            }
                            else
                            {
                                SendReply(arg, string.Format("Custom Loot list not found"));
                                exitloop = true;
                                return;
                            }
                            break;
                    }
                    if (exitloop) break;
                }
            }
            if (list == "")
                SendReply(arg, string.Format($"Massdrop  of type '{type}' to (X:{x.ToString("0")} Z:{z.ToString("0")}) started with {drops.ToString()} Drops( {radius}m Radius)"));
            else
                SendReply(arg, string.Format($"Massdrop  of type '{type}|{list}' to (X:{x.ToString("0")} Z:{z.ToString("0")}) started with {drops.ToString()} Drops( {radius}m Radius)"));
            if (_massDropTimer != null && !_massDropTimer.Destroyed)
                _massDropTimer.Destroy();
            bool showinfo = true;
            _massDropTimer = timer.Repeat(configData.AirdropSettings.airdropMassdropDelay, drops + 1, () => {
                if (_massDropTimer == null || _massDropTimer.Destroyed) return;
                newpos.x = UnityEngine.Random.Range(x - radius, x + radius);
                newpos.z = UnityEngine.Random.Range(z - radius, z + radius);
                //newpos.y -= TerrainMeta.HeightMap.GetHeight(newpos);
                startCargoPlane(newpos, false, null, type, list, showinfo);
                if (_massDropTimer.Repetitions == drops) showinfo = false;
            });
        }

        [ConsoleCommand("ad.toplayer")]
        private void dropToPlayer(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < configData.GenericSettings.neededAuthLvl) return;
            if (arg.Args == null)
            {
                SendReply(arg, string.Format("Please specify a target playername"));
                return;
            }
            if (arg.Args[0] == "*")
            {
                foreach (BasePlayer target in BasePlayer.activePlayerList)
                {
                    if (target.IsAdmin) continue;
                    NextTick(() => {
                        var newpos = new Vector3();
                        newpos = target.transform.position;
                        startCargoPlane(newpos, false, null, "dropdirect");
                        if (configData.Notifications.notifyDropPlayer)
                            MessageToPlayer(target, msg("msgDropPlayer", target.UserIDString));
                    });
                }
                SendReply(arg, string.Format($"Started Airdrop to each active player"));
            }
            else
            {
                BasePlayer target = FindPlayerByName(arg.Args[0]);
                if (target == null)
                {
                    SendReply(arg, string.Format($"Player '{arg.Args[0]}' not found"));
                    return;
                }
                var newpos = new Vector3();
                newpos = target.transform.position;
                string gridPos = GetGridString(newpos);
                startCargoPlane(newpos, false, null, "dropdirect");
                SendReply(arg, string.Format($"Starting Airdrop to Player '{target.displayName}' at: {newpos.ToString("0")} grid area: {gridPos}"));
                if (configData.Notifications.notifyDropPlayer)
                    MessageToPlayer(target, msg("msgDropPlayer", target.UserIDString));
            }
        }

        [ConsoleCommand("ad.dropplayer")]
        private void dropDropOnly(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < configData.GenericSettings.neededAuthLvl) return;
            if (arg.Args == null)
            {
                SendReply(arg, string.Format("Please specify a target playername"));
                return;
            }
            BasePlayer target = FindPlayerByName(arg.Args[0]);
            if (target == null)
            {
                SendReply(arg, string.Format($"Player '{arg.Args[0]}' not found"));
                return;
            }
            var newpos = new Vector3();
            newpos = target.transform.position;
            newpos.y += 100;
            string gridPos = GetGridString(newpos);
            Dictionary<string, object> setting;
            if (setupDropTypes.ContainsKey("dropdirect"))
            {
                setting = new Dictionary<string, object>((Dictionary<string, object>)setupDropTypes["dropdirect"]);
                object value;
                foreach (var pair in setupDropDefault)
                    if (!setting.TryGetValue(pair.Key, out value))
                        setting.Add(pair.Key, setupDropDefault[pair.Key]);
            }
            else
                setting = new Dictionary<string, object>((Dictionary<string, object>)setupDropDefault);
            setting.Add("userID", target.userID);
            object isSupplyDropActive = Interface.Oxide.CallHook("isSupplyDropActive");
            setting.Add("betterloot", isSupplyDropActive != null && (bool)isSupplyDropActive ? true : false);
            setting["droptype"] = "dropdirect";
            createSupplyDrop(newpos, new Dictionary<string, object>(setting), false, false, new Vector3(), new Vector3(), "dropdirect", target.userID);
            setting.Clear();
            SendReply(arg, string.Format($"Direct Drop to Player '{target.displayName}' at: {target.transform.position.ToString("0")} grid area: {gridPos}"));
            if (configData.Notifications.notifyDropDirect)
                MessageToPlayer(target, msg("msgDropDirect", target.UserIDString));

        }

        [ConsoleCommand("ad.dropspace")]
        private void dropDropSpaceOnly(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < configData.GenericSettings.neededAuthLvl) return;
            if (arg.Args == null)
            {
                SendReply(arg, string.Format("Please specify a target playername"));
                return;
            }
            BasePlayer target = FindPlayerByName(arg.Args[0]);
            if (target == null)
            {
                SendReply(arg, string.Format($"Player '{arg.Args[0]}' not found"));
                return;
            }
            var newpos = new Vector3();
            newpos = target.transform.position;
            newpos.y += 100;
            string gridPos = GetGridString(newpos);
            Dictionary<string, object> setting;
            if (setupDropTypes.ContainsKey("dropdirect"))
            {
                setting = new Dictionary<string, object>((Dictionary<string, object>)setupDropTypes["dropdirect"]);
                object value;
                foreach (var pair in setupDropDefault)
                    if (!setting.TryGetValue(pair.Key, out value))
                        setting.Add(pair.Key, setupDropDefault[pair.Key]);
            }
            else
                setting = new Dictionary<string, object>((Dictionary<string, object>)setupDropDefault);
            setting.Add("userID", target.userID);
            object isSupplyDropActive = Interface.Oxide.CallHook("isSupplyDropActive");
            setting.Add("betterloot", isSupplyDropActive != null && (bool)isSupplyDropActive ? true : false);
            setting["droptype"] = "dropdirect";
            createSupplyDropSpace(newpos, new Dictionary<string, object>(setting), false, false, new Vector3(), new Vector3(), "dropdirect", target.userID);
            setting.Clear();
            SendReply(arg, string.Format($"Space Drop to Player '{target.displayName}' at: {target.transform.position.ToString("0")} grid area: {gridPos}"));
            if (configData.Notifications.notifyDropDirect)
                MessageToPlayer(target, msg("msgDropDirectSpace", target.UserIDString));

        }

        [ConsoleCommand("ad.timer")]
        private void dropReloadTimer(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < configData.GenericSettings.neededAuthLvl) return;
            if (arg.Args != null && arg.Args.Length > 0)
            {
                try
                {
                    airdropTimerNext(Convert.ToInt32(arg.Args[0]));
                    return;
                }
                catch
                {
                    SendReply(arg, string.Format("Custom Timer value has to be an integer number."));
                    return;
                }
            }
            airdropTimerNext();
        }

        [ConsoleCommand("ad.cleanup")]
        private void dropCleanUp(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < configData.GenericSettings.neededAuthLvl) return;
            if (_massDropTimer != null && !_massDropTimer.Destroyed)
                _massDropTimer.Destroy();
            var planes = UnityEngine.Object.FindObjectsOfType<CargoPlane>().ToList();
            SendReply(arg, $"...killing {planes.Count} Planes");
            foreach (var plane in planes)
                plane.Kill();
            var drops = UnityEngine.Object.FindObjectsOfType<SupplyDrop>().ToList();
            SendReply(arg, $"...killing {drops.Count} SupplyDrops");
            foreach (var drop in drops)
                drop.Kill();
            CargoPlanes.Clear();
            SupplyDrops.Clear();
            LootedDrops.Clear();
            ItemManager.DoRemoves();
        }

        private void airdropCleanUp()
        {
            var drops = UnityEngine.Object.FindObjectsOfType<SupplyDrop>().ToList();
            Puts($"...killing {drops.Count} SupplyDrops");
            foreach (var drop in drops)
                drop.KillMessage();
        }

        [ConsoleCommand("ad.lootreload")]
        private void dropLootReload(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < configData.GenericSettings.neededAuthLvl) return;
            SendReply(arg, "Custom loot reloading...");
            //SetupLoot();
        }
        #endregion Console Commands

        #region Chat Commands
        [ChatCommand("droprandom")]
        private void cdropRandom(BasePlayer player, string command)
        {
            if (player.net.connection.authLevel < configData.GenericSettings.neededAuthLvl)
            {
                SendReply(player, string.Format(configData.GenericSettings.Format, configData.GenericSettings.Color, configData.GenericSettings.Prefix) + "You are not allowed to use this command");
                return;
            }
            var plane = createCargoPlane();
            Vector3 newpos = plane.RandomDropPosition();
            string gridPos = GetGridString(newpos);
            newpos.y += TerrainMeta.HeightMap.GetHeight(newpos);
            startCargoPlane(newpos, false, plane, "regular");
            SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Random Airdrop incoming at: {newpos.ToString("0")} grid area: {gridPos}</color>");
            if (configData.TimerSettings.airdropTimerEnabled && configData.TimerSettings.airdropTimerResetAfterRandom)
                airdropTimerNext();
        }

        [ChatCommand("droptopos")]
        private void cdropToPos(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < configData.GenericSettings.neededAuthLvl)
            {
                SendReply(player, string.Format(configData.GenericSettings.Format, configData.GenericSettings.Color, configData.GenericSettings.Prefix) + "You are not allowed to use this command");
                return;
            }
            if (args == null || args.Length != 2)
            {
                SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Please specity location with X and Z coordinates only</color>");
                return;
            }
            Vector3 newpos = new Vector3(Convert.ToInt32(args[0]), 0, Convert.ToInt32(args[1]));
            string gridPos = GetGridString(newpos);
            newpos.y += TerrainMeta.HeightMap.GetHeight(newpos);
            startCargoPlane(newpos, false, null, "regular");
            if (configData.GenericSettings.notifyByChatAdminCalls)
                SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Airdrop called to position: {newpos.ToString("0")} grid area: {gridPos}</color>");
        }

        [ChatCommand("droptoplayer")]
        private void cdropToPlayer(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < configData.GenericSettings.neededAuthLvl)
            {
                SendReply(player, string.Format(configData.GenericSettings.Format, configData.GenericSettings.Color, configData.GenericSettings.Prefix) + msg("msgNoAccess"));
                return;
            }
            if (args.Length < 1)
            {
                SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Please specify a target playername</color>");
                return;
            }
            if (args[0] == "*")
            {
                foreach (BasePlayer target in BasePlayer.activePlayerList)
                {
                    if (target.IsAdmin) continue;
                    NextTick(() => {
                        var newpos = new Vector3();
                        newpos = target.transform.position;
                        startCargoPlane(newpos, false, null, "dropdirect");
                        if (configData.Notifications.notifyDropPlayer)
                            MessageToPlayer(target, msg("msgDropPlayer", target.UserIDString));
                    });
                }
                if (configData.GenericSettings.notifyByChatAdminCalls)
                    SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Started Airdrop to each active player</color>");
            }
            else
            {
                BasePlayer target = FindPlayerByName(args[0]);
                if (target == null)
                {
                    SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Player '{args[0]}' not found</color>");
                    return;
                }
                var newpos = new Vector3();
                newpos = target.transform.position;
                string gridPos = GetGridString(newpos);
                startCargoPlane(newpos, false, null, "dropdirect");
                if (configData.GenericSettings.notifyByChatAdminCalls)
                    SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Airdrop called to player '{target.displayName}' at: {newpos.ToString("0")} grid area: {gridPos}</color>");
                if (configData.Notifications.notifyDropPlayer)
                    MessageToPlayer(target, msg("msgDropPlayer", target.UserIDString));
            }
        }

        [ChatCommand("dropdirect")]
        private void cdropDirect(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < configData.GenericSettings.neededAuthLvl)
            {
                SendReply(player, string.Format(configData.GenericSettings.Format, configData.GenericSettings.Color, configData.GenericSettings.Prefix) + msg("msgNoAccess"));
                return;
            }
            if (args.Length < 1)
            {
                SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Please specify a target playername</color>");
                return;
            }
            BasePlayer target = FindPlayerByName(args[0]);
            if (target == null)
            {
                SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Player '{args[0]}' not found</color>");
                return;
            }
            var newpos = new Vector3();
            newpos = target.transform.position;
            newpos.y += 100;

            Dictionary<string, object> setting;
            if (setupDropTypes.ContainsKey("dropdirect"))
            {
                setting = new Dictionary<string, object>((Dictionary<string, object>)setupDropTypes["dropdirect"]);
                object value;
                foreach (var pair in setupDropDefault)
                    if (!setting.TryGetValue(pair.Key, out value))
                        setting.Add(pair.Key, setupDropDefault[pair.Key]);
            }
            else
                setting = new Dictionary<string, object>((Dictionary<string, object>)setupDropDefault);
            setting.Add("userID", target.userID);
            object isSupplyDropActive = Interface.Oxide.CallHook("isSupplyDropActive");
            setting.Add("betterloot", isSupplyDropActive != null && (bool)isSupplyDropActive ? true : false);
            setting["droptype"] = "dropdirect";
            createSupplyDrop(newpos, new Dictionary<string, object>(setting), false, false, new Vector3(), new Vector3(), "dropdirect", target.userID);
            setting.Clear();
            if (configData.GenericSettings.notifyByChatAdminCalls)
                SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Direct Drop to Player '{target.displayName}' at: {target.transform.position.ToString("0")}</color>");
            if (configData.Notifications.notifyDropDirect)
                MessageToPlayer(target, msg("msgDropDirect", target.UserIDString));
        }

        [ChatCommand("dropspace")]
        private void cdropDirectSpace(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < configData.GenericSettings.neededAuthLvl)
            {
                SendReply(player, string.Format(configData.GenericSettings.Format, configData.GenericSettings.Color, configData.GenericSettings.Prefix) + msg("msgNoAccess"));
                return;
            }
            if (args.Length < 1)
            {
                SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Please specify a target playername</color>");
                return;
            }
            BasePlayer target = FindPlayerByName(args[0]);
            if (target == null)
            {
                SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Player '{args[0]}' not found</color>");
                return;
            }
            var newpos = new Vector3();
            newpos = target.transform.position;
            newpos.y += 100;

            Dictionary<string, object> setting;
            if (setupDropTypes.ContainsKey("dropdirect"))
            {
                setting = new Dictionary<string, object>((Dictionary<string, object>)setupDropTypes["dropdirect"]);
                object value;
                foreach (var pair in setupDropDefault)
                    if (!setting.TryGetValue(pair.Key, out value))
                        setting.Add(pair.Key, setupDropDefault[pair.Key]);
            }
            else
                setting = new Dictionary<string, object>((Dictionary<string, object>)setupDropDefault);
            setting.Add("userID", target.userID);
            object isSupplyDropActive = Interface.Oxide.CallHook("isSupplyDropActive");
            setting.Add("betterloot", isSupplyDropActive != null && (bool)isSupplyDropActive ? true : false);
            setting["droptype"] = "dropdirect";

            createSupplyDropSpace(newpos, new Dictionary<string, object>(setting), false, false, new Vector3(), new Vector3(), "dropdirect", target.userID);

            setting.Clear();
            if (configData.GenericSettings.notifyByChatAdminCalls)
                SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Space Drop to Player '{target.displayName}' at: {target.transform.position.ToString("0")}</color>");
            if (configData.Notifications.notifyDropDirect)
                MessageToPlayer(target, msg("msgDropDirectSpace", target.UserIDString));
        }

        [ChatCommand("dropmass")]
        private void cdropMass(BasePlayer player, string command, string[] args)
        {
            int drops = 0;
            if (player.net.connection.authLevel < configData.GenericSettings.neededAuthLvl)
            {
                SendReply(player, string.Format(configData.GenericSettings.Format, configData.GenericSettings.Color, configData.GenericSettings.Prefix) + msg("msgNoAccess"));
                return;
            }
            if (args.Length < 1)
                drops = configData.AirdropSettings.airdropMassdropDefault;
            else
                try { drops = Convert.ToInt32(args[0]); }
                catch
                {
                    SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Massdrop value has to be an integer number</color>");
                    return;
                }
            if (configData.GenericSettings.notifyByChatAdminCalls)
                SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Massdrop started with {drops.ToString()} Drops</color>");

            if (_massDropTimer != null && !_massDropTimer.Destroyed)
                _massDropTimer.Destroy();

            bool showinfo = true;
            _massDropTimer = timer.Repeat(configData.AirdropSettings.airdropMassdropDelay, drops + 1, () => {
                if (_massDropTimer == null || _massDropTimer.Destroyed) return;
                startCargoPlane(Vector3.zero, true, null, "massdrop", "", showinfo);
                if (_massDropTimer.Repetitions == drops) showinfo = false;
            });
        }

        [ChatCommand("droptomass")]
        private void cdropToMass(BasePlayer player, string command, string[] args)
        {
            int drops = configData.AirdropSettings.airdropMassdropDefault;
            float x = -99999;
            float z = -99999;
            float radius = configData.AirdropSettings.airdropMassdropRadiusDefault;
            if (player.net.connection.authLevel < configData.GenericSettings.neededAuthLvl)
            {
                SendReply(player, string.Format(configData.GenericSettings.Format, configData.GenericSettings.Color, configData.GenericSettings.Prefix) + msg("msgNoAccess"));
                return;
            }
            if (args.Length < 2)
            {
                SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Specify at minumum (X) and (Z)</color>");
                return;
            }
            if (args.Length >= 3) int.TryParse(args[2], out drops);

            if (!float.TryParse(args[0], out x) || !float.TryParse(args[1], out z))
            {
                SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Specify at minumum (X) (Z) or with '0 0' for random position | and opt:drop-count and opt:radius )</color>");
                return;
            }
            Vector3 newpos = new Vector3();
            if (x == 0 || z == 0)
            {
                var plane = createCargoPlane();
                newpos = plane.RandomDropPosition();
                x = newpos.x;
                z = newpos.z;
                plane.Kill();
            }

            if (args.Length > 3) float.TryParse(args[3], out radius);
            lastDropRadius = radius;
            if (configData.GenericSettings.notifyByChatAdminCalls)
                SendReply(player, $"<color={configData.GenericSettings.colorAdmMsg}>Massdrop to (X:{x.ToString("0")} Z:{z.ToString("0")}) started with {drops.ToString()} Drops( {radius}m Radius)</color>");

            if (_massDropTimer != null && !_massDropTimer.Destroyed)
                _massDropTimer.Destroy();

            bool showinfo = true;
            _massDropTimer = timer.Repeat(configData.AirdropSettings.airdropMassdropDelay, drops + 1, () => {
                if (_massDropTimer == null || _massDropTimer.Destroyed) return;
                newpos.x = UnityEngine.Random.Range(x - radius, x + radius);
                newpos.z = UnityEngine.Random.Range(z - radius, z + radius);
                newpos.y += TerrainMeta.HeightMap.GetHeight(newpos);
                startCargoPlane(newpos, false, null, "massdrop", "", showinfo);
                if (_massDropTimer.Repetitions == drops) showinfo = false;
            });
        }
        #endregion Chat Commands

        #endregion Commands

        #region Dictionary & Lists
        static Dictionary<string, object> defaultRealTimers()
        {
            var dp = new Dictionary<string, object>();
            dp.Add("16:00", "ad.massdrop 3");
            dp.Add("18:00", "ad.toplayer *");
            return dp;
        }

        static Dictionary<string, object> defaultServerTimers()
        {
            var dp = new Dictionary<string, object>();
            dp.Add("6", "ad.massdrop 3");
            dp.Add("18", "ad.massdropto 0 0 5 100");
            return dp;
        }

        static Dictionary<string, object> defaultDrop()
        {
            var dp = new Dictionary<string, object>();
            dp.Add("minCrates", 1);
            dp.Add("maxCrates", 1);
            dp.Add("cratesGap", 50);
            dp.Add("despawnMinutes", 15);
            dp.Add("crateAirResistance", 2.0); ;
            dp.Add("useCustomLootTable", false);
            dp.Add("CustomLootListName", "regular");
            dp.Add("planeSpeed", 35);
            dp.Add("additionalheight", 0);
            return dp;
        }

        static Dictionary<string, object> defaultDropTypes()
        {
            var dp = new Dictionary<string, object>();

            var dp0 = new Dictionary<string, object>();
            dp0.Add("minCrates", 1);
            dp0.Add("maxCrates", 1);
            dp0.Add("cratesGap", 50);
            dp0.Add("despawnMinutes", 15);
            dp0.Add("crateAirResistance", 2.0);
            dp0.Add("useCustomLootTable", false);
            dp0.Add("CustomLootListName", "regular");
            dp0.Add("planeSpeed", 35);
            dp0.Add("additionalheight", 0);
            dp.Add("regular", dp0);

            var dp1 = new Dictionary<string, object>();
            dp1.Add("minCrates", 1);
            dp1.Add("maxCrates", 1);
            dp1.Add("cratesGap", 50);
            dp1.Add("despawnMinutes", 15);
            dp1.Add("crateAirResistance", 2.0);
            dp1.Add("useCustomLootTable", false);
            dp1.Add("CustomLootListName", "supplysignal");
            dp1.Add("planeSpeed", 35);
            dp1.Add("additionalheight", 0);
            dp.Add("supplysignal", dp1);

            var dp2 = new Dictionary<string, object>();
            dp2.Add("minCrates", 1);
            dp2.Add("maxCrates", 1);
            dp2.Add("cratesGap", 50);
            dp2.Add("despawnMinutes", 15);
            dp2.Add("crateAirResistance", 2.0);
            dp2.Add("useCustomLootTable", false);
            dp2.Add("CustomLootListName", "massdrop");
            dp2.Add("planeSpeed", 45);
            dp2.Add("additionalheight", 0);
            dp.Add("massdrop", dp2);

            var dp3 = new Dictionary<string, object>();
            dp3.Add("minCrates", 1);
            dp3.Add("maxCrates", 1);
            dp3.Add("cratesGap", 50);
            dp3.Add("despawnMinutes", 15);
            dp3.Add("crateAirResistance", 2.0);
            dp3.Add("useCustomLootTable", false);
            dp3.Add("CustomLootListName", "dropdirect");
            dp3.Add("planeSpeed", 65);
            dp3.Add("additionalheight", 0);
            dp.Add("dropdirect", dp3);

            var dp4 = new Dictionary<string, object>();
            dp4.Add("minCrates", 1);
            dp4.Add("maxCrates", 1);
            dp4.Add("cratesGap", 50);
            dp4.Add("despawnMinutes", 15);
            dp4.Add("crateAirResistance", 2.0);
            dp4.Add("useCustomLootTable", false);
            dp4.Add("CustomLootListName", "custom_event");
            dp4.Add("planeSpeed", 95);
            dp4.Add("additionalheight", 0);
            dp4.Add("notificationInfo", "Custom Stuff");
            dp.Add("custom_event", dp4);

            return dp;
        }

        static Dictionary<string, object> defaultItemList()
        {
            var dp0_0 = new Dictionary<string, object>();
            dp0_0.Add("targeting.computer", 2);
            dp0_0.Add("cctv.camera", 2);
            var dp0 = new Dictionary<string, object>();
            dp0.Add("itemList", dp0_0);
            dp0.Add("itemDivider", 2);

            var dp1_0 = new Dictionary<string, object>();
            dp1_0.Add("explosive.timed", 4);
            dp1_0.Add("metal.refined", 100);
            var dp1 = new Dictionary<string, object>();
            dp1.Add("itemList", dp1_0);
            dp1.Add("itemDivider", 2);

            var dp2_0 = new Dictionary<string, object>();
            dp2_0.Add("explosive.timed", 4);
            dp2_0.Add("grenade.f1", 10);
            var dp2 = new Dictionary<string, object>();
            dp2.Add("itemList", dp2_0);
            dp2.Add("itemDivider", 2);

            var dp3_0 = new Dictionary<string, object>();
            dp3_0.Add("explosive.timed", 4);
            dp3_0.Add("surveycharge", 10);
            var dp3 = new Dictionary<string, object>();
            dp3.Add("itemList", dp3_0);
            dp3.Add("itemDivider", 2);

            var dp4_0 = new Dictionary<string, object>();
            dp4_0.Add("explosive.timed", 10);
            dp4_0.Add("grenade.f1", 10);
            var dp4 = new Dictionary<string, object>();
            dp4.Add("itemList", dp4_0);
            dp4.Add("itemDivider", 2);

            var dp = new Dictionary<string, object>();
            dp.Add("regular", dp0);
            dp.Add("supplysignal", dp1);
            dp.Add("massdrop", dp2);
            dp.Add("dropdirect", dp3);
            dp.Add("custom_event", dp4);

            return dp;
        }

        FieldInfo dropPlanestartPos = typeof(CargoPlane).GetField("startPos", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
        FieldInfo dropPlaneendPos = typeof(CargoPlane).GetField("endPos", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
        FieldInfo dropPlanesecondsToTake = typeof(CargoPlane).GetField("secondsToTake", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
        FieldInfo dropPlanesecondsTaken = typeof(CargoPlane).GetField("secondsTaken", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
        FieldInfo dropPlanedropped = typeof(CargoPlane).GetField("dropped", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
        FieldInfo _isSpawned = typeof(BaseNetworkable).GetField("isSpawned", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
        FieldInfo _creationFrame = typeof(BaseNetworkable).GetField("creationFrame", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
        static FieldInfo _parachute = typeof(SupplyDrop).GetField("parachute", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

        CargoPlane referencePlane = GameManager.server.FindPrefab(PLANE_PREFAB).GetComponent<CargoPlane>();
        SpawnFilter spawnFilter = GameManager.server.FindPrefab(PLANE_PREFAB).GetComponent<CargoPlane>().filter;

        List<Regex> regexTags = new List<Regex>
        {
            new Regex(@"<color=.+?>", RegexOptions.Compiled),
            new Regex(@"<size=.+?>", RegexOptions.Compiled)
        };

        List<string> tags = new List<string>
        {
            "</color>",
            "</size>",
            "<i>",
            "</i>",
            "<b>",
            "</b>"
        };
        #endregion

        #region Configuration
        Dictionary<string, object> setupDropTypes = new Dictionary<string, object>();
        Dictionary<string, object> setupDropDefault = new Dictionary<string, object>();
        //Dictionary<string, object> setupItemList = new Dictionary<string, object>();
        Dictionary<string, object> realTimers = new Dictionary<string, object>();
        Dictionary<string, object> serverTimers = new Dictionary<string, object>();

        private static ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Debug")]
            public DebugOptions DebugSettings { get; set; }
            [JsonProperty(PropertyName = "General Settings")]
            public GenericOptions GenericSettings { get; set; }
            [JsonProperty(PropertyName = "Airdrop & General Plane Settings")]
            public AirdropOptions AirdropSettings { get; set; }
            [JsonProperty(PropertyName = "Allow Damage Airdrop Settings")]
            public DamageOptions DamageSettings { get; set; }
            [JsonProperty(PropertyName = "Notification Settings")]
            public NotificationOptions Notifications { get; set; }
            [JsonProperty(PropertyName = "SimpleUI")]
            public UIOptions UISettings { get; set; }
            [JsonProperty(PropertyName = "Timer - Random Event")]
            public TimerOptions TimerSettings { get; set; }
            [JsonProperty(PropertyName = "Timers- System Timers")]
            public TimersOptions TimersSettings { get; set; }
            [JsonProperty(PropertyName = "Drop Settings")]
            public DropOptions DropSettings { get; set; }
            [JsonProperty(PropertyName = "Custom Loot Items")]
            public StaticOptions StaticItems { get; set; }

            public class DebugOptions
            {
                [JsonProperty(PropertyName = "Enable Debugging to Console")]
                public bool useDebug { get; set; }
            }

            public class GenericOptions
            {
                [JsonProperty(PropertyName = "Admin messages color")]
                public string colorAdmMsg { get; set; }
                [JsonProperty(PropertyName = "AuthLevel needed for console commands")]
                public int neededAuthLvl { get; set; }
                [JsonProperty(PropertyName = "Broadcast messages color")]
                public string colorTextMsg { get; set; }
                [JsonProperty(PropertyName = "Chat/Message prefix")]
                public string Prefix { get; set; }
                [JsonProperty(PropertyName = "Prefix color")]
                public string Color { get; set; }
                [JsonProperty(PropertyName = "Prefix format")]
                public string Format { get; set; }
                [JsonProperty(PropertyName = "GUI Announce command")]
                public string guiCommand { get; set; }
                [JsonProperty(PropertyName = "Show message to admin after command usage")]
                public bool notifyByChatAdminCalls { get; set; }
                [JsonProperty(PropertyName = "Time for active smoke of SupplySignal (default is 210)")]
                public float supplySignalSmokeTime { get; set; }
                [JsonProperty(PropertyName = "Lock DirectDrop to be looted only by target player")]
                public bool lockDirectDrop { get; set; }
                [JsonProperty(PropertyName = "Lock SignalDrop to be looted only by target player")]
                public bool lockSignalDrop { get; set; }
                [JsonProperty(PropertyName = "Unlock crates only after player stopped looting")]
                public bool unlockDropAfterLoot { get; set; }
            }

            public class AirdropOptions
            {
                [JsonProperty(PropertyName = "Default radius for location based massdrop")]
                public float airdropMassdropRadiusDefault { get; set; }
                [JsonProperty(PropertyName = "Delay between Massdrop plane spawns")]
                public float airdropMassdropDelay { get; set; }
                [JsonProperty(PropertyName = "Massdrop default plane amount")]
                public int airdropMassdropDefault { get; set; }
                [JsonProperty(PropertyName = "Multiplier for (plane height * highest point on Map); Default 1.0")]
                public float planeOffSetYMultiply { get; set; }
                [JsonProperty(PropertyName = "Multiplier for overall flight distance; lower means faster at map")]
                public float planeOffSetXMultiply { get; set; }
                [JsonProperty(PropertyName = "Disable SupplySignal randomization")]
                public bool disableRandomSupplyPos { get; set; }
                [JsonProperty(PropertyName = "Visual & Sound Effects Settings")]
                public EffectsOptions EffectsSettings { get; set; }
                [JsonProperty(PropertyName = "Space Delivery Drop Settings")]
                public SpaceOptions SpaceSettings { get; set; }

                public class EffectsOptions
                {
                    [JsonProperty(PropertyName = "Use Explosion Sound Effect when hits ground position")]
                    public bool useSupplyDropEffectLanded { get; set; }
                    [JsonProperty(PropertyName = "Deploy Smoke on drop as it falls")]
                    public bool useSmokeEffectOnDrop { get; set; }
                    [JsonProperty(PropertyName = "Deploy with Audio Alarm on drop")]
                    public bool useSirenAlarmOnDrop { get; set; }
                    [JsonProperty(PropertyName = "Deploy with Audio Alarms on drop only during the night")]
                    public bool useSirenAlarmAtNightOnly { get; set; }
                    [JsonProperty(PropertyName = "Deploy with Spinning Siren Light on drop")]
                    public bool useSirenLightOnDrop { get; set; }
                    [JsonProperty(PropertyName = "Deploy with Spinning Siren Light on drop only during the night")]
                    public bool useSirenLightAtNightOnly { get; set; }
                    [JsonProperty(PropertyName = "Enable Rocket Signal upon Supply Drop Landing")]
                    public bool useSupplyDropRocket { get; set; }
                    [JsonProperty(PropertyName = "Signal rocket speed")]
                    public float signalRocketSpeed { get; set; }
                    [JsonProperty(PropertyName = "Signal rocket explosion timer")]
                    public float signalRocketExplosionTime { get; set; }
                }

                public class SpaceOptions
                {
                    [JsonProperty(PropertyName = "Incoming Space Delivery Supply Drop velocity")]
                    public float dropVelocity { get; set; }
                    [JsonProperty(PropertyName = "Parachute deploy distance from ground")]
                    public float dropGroundDistance { get; set; }
                }

            }

            public class DamageOptions
            {
                [JsonProperty(PropertyName = "Players can shoot down the drop")]
                public bool shootDownDrops { get; set; }
                [JsonProperty(PropertyName = "Players can shoot down the drop - needed hits")]
                public int shootDownCount { get; set; }
                [JsonProperty(PropertyName = "Set Angular Drag for drop")]
                public float dropAngularDragSetting { get; set; }
                [JsonProperty(PropertyName = "Set Drag for drop (drop resistance)")]
                public float dropDragsetting { get; set; }
                [JsonProperty(PropertyName = "Set drop allow to explode on impact")]
                public bool explodeImpact { get; set; }
                [JsonProperty(PropertyName = "Set drop chance exploding on impact (x out of 100)")]
                public int explodeChance { get; set; }
                [JsonProperty(PropertyName = "Set Mass weight for drop")]
                public float dropMassSetting { get; set; }
            }

            public class NotificationOptions
            {
                [JsonProperty(PropertyName = "Maximum distance in meters to get notified about landed Drop")]
                public float supplyDropNotifyDistance { get; set; }
                [JsonProperty(PropertyName = "Maximum distance in meters to get notified about looted Drop")]
                public float supplyLootNotifyDistance { get; set; }
                [JsonProperty(PropertyName = "Notify a player about incoming Drop to his location")]
                public bool notifyDropPlayer { get; set; }
                [JsonProperty(PropertyName = "Notify a player about spawned Drop at his location")]
                public bool notifyDropDirect { get; set; }
                [JsonProperty(PropertyName = "Notify a player about Space Delivery Drop at his location")]
                public bool notifyDropDirectSpace { get; set; }
                [JsonProperty(PropertyName = "Notify admins per chat about player who has thrown SupplySignal")]
                public bool notifyDropAdminSignal { get; set; }
                [JsonProperty(PropertyName = "Notify console at Drop by SupplySignal")]
                public bool notifyDropConsoleSignal { get; set; }
                [JsonProperty(PropertyName = "Notify console at timed-regular Drop")]
                public bool notifyDropConsoleRegular { get; set; }
                [JsonProperty(PropertyName = "Notify console when a Drop is being looted")]
                public bool notifyDropConsoleLooted { get; set; }
                [JsonProperty(PropertyName = "Notify console when Drop is landed")]
                public bool notifyDropConsoleOnLanded { get; set; }
                [JsonProperty(PropertyName = "Notify console when Drop is spawned")]
                public bool notifyDropConsoleSpawned { get; set; }
                [JsonProperty(PropertyName = "Notify console when Drop landed/spawned only at the first")]
                public bool notifyDropConsoleFirstOnly { get; set; }
                [JsonProperty(PropertyName = "Notify players at custom/event Drop")]
                public bool notifyDropServerCustom { get; set; }
                [JsonProperty(PropertyName = "Notify players at custom/event Drop including Coords")]
                public bool notifyDropServerCustomCoords { get; set; }
                [JsonProperty(PropertyName = "Notify players at custom/event Drop including Grid Area")]
                public bool notifyDropServerCustomGrid { get; set; }
                [JsonProperty(PropertyName = "Notify players at Drop by SupplySignal")]
                public bool notifyDropServerSignal { get; set; }
                [JsonProperty(PropertyName = "Notify players at Drop by SupplySignal including Coords")]
                public bool notifyDropServerSignalCoords { get; set; }
                [JsonProperty(PropertyName = "Notify players at Drop by SupplySignal including Grid Area")]
                public bool notifyDropServerSignalGrid { get; set; }
                [JsonProperty(PropertyName = "Notify players at Massdrop")]
                public bool notifyDropServerMass { get; set; }
                [JsonProperty(PropertyName = "Notify players at Random/Timed Drop")]
                public bool notifyDropServerRegular { get; set; }
                [JsonProperty(PropertyName = "Notify players at Random/Timed Drop including Coords")]
                public bool notifyDropServerRegularCoords { get; set; }
                [JsonProperty(PropertyName = "Notify players at Random/Timed Drop including Grid Area")]
                public bool notifyDropServerRegularGrid { get; set; }
                [JsonProperty(PropertyName = "Notify players when a Drop is being looted")]
                public bool notifyDropServerLooted { get; set; }
                [JsonProperty(PropertyName = "Notify players when a Drop is being looted including coords")]
                public bool notifyDropServerLootedCoords { get; set; }
                [JsonProperty(PropertyName = "Notify players when a Drop is being looted including Grid Area")]
                public bool notifyDropServerLootedGrid { get; set; }
                [JsonProperty(PropertyName = "Notify players when Drop is landed about distance")]
                public bool notifyDropPlayersOnLanded { get; set; }
                [JsonProperty(PropertyName = "Notify Players who has thrown a SupplySignal")]
                public bool notifyDropSignalByPlayer { get; set; }
                [JsonProperty(PropertyName = "Notify Players who has thrown a SupplySignal including coords")]
                public bool notifyDropSignalByPlayerCoords { get; set; }
                [JsonProperty(PropertyName = "Notify Players who has thrown a SupplySignal including Grid Area")]
                public bool notifyDropSignalByPlayerGrid { get; set; }
                [JsonProperty(PropertyName = "Use GUI Announcements for any Drop notification")]
                public bool notifyDropGUI { get; set; }
            }

            public class UIOptions
            {
                [JsonProperty(PropertyName = "SimpleUI_Enable")]
                public bool SimpleUI_Enable { get; set; }
                [JsonProperty(PropertyName = "SimpleUI_FontSize")]
                public int SimpleUI_FontSize { get; set; }
                [JsonProperty(PropertyName = "SimpleUI_HideTimer")]
                public float SimpleUI_HideTimer { get; set; }
                [JsonProperty(PropertyName = "SimpleUI_NoticeColor")]
                public string SimpleUI_NoticeColor { get; set; }
                [JsonProperty(PropertyName = "SimpleUI_ShadowColor")]
                public string SimpleUI_ShadowColor { get; set; }
                [JsonProperty(PropertyName = "SimpleUI_Top")]
                public float SimpleUI_Top { get; set; }
                [JsonProperty(PropertyName = "SimpleUI_Left")]
                public float SimpleUI_Left { get; set; }
                [JsonProperty(PropertyName = "SimpleUI_MaxWidth")]
                public float SimpleUI_MaxWidth { get; set; }
                [JsonProperty(PropertyName = "SimpleUI_MaxHeight")]
                public float SimpleUI_MaxHeight { get; set; }
            }

            public class TimerOptions
            {
                [JsonProperty(PropertyName = "Use Airdrop timer")]
                public bool airdropTimerEnabled { get; set; }
                [JsonProperty(PropertyName = "Used Airdrop timer command")]
                public string airdropTimerCmd { get; set; }
                [JsonProperty(PropertyName = "Minimum players for timed Drop")]
                public int airdropTimerMinPlayers { get; set; }
                [JsonProperty(PropertyName = "Minimum minutes for random timer delay")]
                public int airdropTimerWaitMinutesMin { get; set; }
                [JsonProperty(PropertyName = "Maximum minutes for random timer delay")]
                public int airdropTimerWaitMinutesMax { get; set; }
                [JsonProperty(PropertyName = "Reset Timer after manual random drop")]
                public bool airdropTimerResetAfterRandom { get; set; }
                [JsonProperty(PropertyName = "Remove builtIn Airdrop")]
                public bool airdropRemoveInBuilt { get; set; }
                [JsonProperty(PropertyName = "Cleanup old Drops at serverstart")]
                public bool airdropCleanupAtStart { get; set; }
            }

            public class TimersOptions
            {
                [JsonProperty(PropertyName = "Log to console")]
                public bool logTimersToConsole { get; set; }
                [JsonProperty(PropertyName = "Minimum players for running Timers")]
                public int timersMinPlayers { get; set; }
                [JsonProperty(PropertyName = "Use RealTime")]
                public bool useRealtimeTimers { get; set; }
                [JsonProperty(PropertyName = "RealTime")]
                public Dictionary<string, object> realTimers { get; set; }
                [JsonProperty(PropertyName = "Use ServerTime")]
                public bool useGametimeTimers { get; set; }
                [JsonProperty(PropertyName = "ServerTime")]
                public Dictionary<string, object> serverTimers { get; set; }
            }

            public class DropOptions
            {
                [JsonProperty(PropertyName = "DropDefault")]
                public Dictionary<string, object> setupDropDefault { get; set; }
                [JsonProperty(PropertyName = "DropTypes")]
                public Dictionary<string, object> setupDropTypes { get; set; }
            }

            public class StaticOptions
            {
                [JsonProperty(PropertyName = "DropTypes")]
                public List<LootOptions> LootSettings { get; set; }

                public class LootOptions
                {
                    [JsonProperty(PropertyName = "DropType Name")]
                    public string DropTypeName { get; set; }
                    [JsonProperty(PropertyName = "Minimum amount of items to spawn")]
                    public int MinimumItems { get; set; }
                    [JsonProperty(PropertyName = "Maximum amount of items to spawn")]
                    public int MaximumItems { get; set; }
                    [JsonProperty(PropertyName = "Custom Loot Contents")]
                    public List<LootItem> Items { get; set; }

                    public class LootItem
                    {
                        [JsonProperty(PropertyName = "Shortname")]
                        public string Name { get; set; }
                        [JsonProperty(PropertyName = "Minimum amount of item")]
                        public int Minimum { get; set; }
                        [JsonProperty(PropertyName = "Maximum amount of item")]
                        public int Maximum { get; set; }
                        [JsonProperty(PropertyName = "Skin ID")]
                        public ulong Skin { get; set; }
                        [JsonProperty(PropertyName = "Display Name")]
                        public string DisplayName { get; set; }
                    }
                }
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
                DebugSettings = new ConfigData.DebugOptions
                {
                    useDebug = false
                },
                GenericSettings = new ConfigData.GenericOptions
                {
                    Prefix = "[ZTL Drop Services]",
                    Color = "#00ffff",
                    Format = "<size=12><color={0}>{1}</color>: ",
                    guiCommand = "announce",
                    neededAuthLvl = 1,
                    supplySignalSmokeTime = 210.0f,
                    colorAdmMsg = "#c0c0c0",
                    notifyByChatAdminCalls = true,
                    colorTextMsg = "#ffffff",
                    lockDirectDrop = true,
                    lockSignalDrop = false,
                    unlockDropAfterLoot = false,
                },
                AirdropSettings = new ConfigData.AirdropOptions
                {
                    airdropMassdropDefault = 5,
                    airdropMassdropDelay = 0.66f,
                    airdropMassdropRadiusDefault = 100,
                    planeOffSetXMultiply = 1.25f,
                    planeOffSetYMultiply = 1.0f,
                    disableRandomSupplyPos = false,
                    EffectsSettings = new ConfigData.AirdropOptions.EffectsOptions
                    {
                        useSupplyDropEffectLanded = false,
                        signalRocketSpeed = 15,
                        signalRocketExplosionTime = 15,
                        useSmokeEffectOnDrop = false,
                        useSirenAlarmOnDrop = false,
                        useSirenAlarmAtNightOnly = false,
                        useSirenLightOnDrop = false,
                        useSirenLightAtNightOnly = false,
                        useSupplyDropRocket = false
                    },
                    SpaceSettings = new ConfigData.AirdropOptions.SpaceOptions
                    {
                        dropVelocity = 120.0f,
                        dropGroundDistance = 50.0f
                    }
                },
                DamageSettings = new ConfigData.DamageOptions
                {
                    shootDownDrops = true,
                    shootDownCount = 3,
                    dropDragsetting = 0.3f,
                    dropMassSetting = 0.75f,
                    dropAngularDragSetting = 0.1f,
                    explodeImpact = true,
                    explodeChance = 25
                },
                Notifications = new ConfigData.NotificationOptions
                {
                    supplyDropNotifyDistance = 1000,
                    supplyLootNotifyDistance = 1000,
                    notifyDropGUI = false,
                    notifyDropServerSignal = false,
                    notifyDropServerSignalCoords = false,
                    notifyDropServerSignalGrid = false,
                    notifyDropConsoleSignal = true,
                    notifyDropConsoleRegular = true,
                    notifyDropConsoleOnLanded = false,
                    notifyDropConsoleSpawned = false,
                    notifyDropConsoleFirstOnly = true,
                    notifyDropConsoleLooted = true,
                    notifyDropServerRegular = true,
                    notifyDropServerRegularCoords = false,
                    notifyDropServerRegularGrid = false,
                    notifyDropServerCustom = true,
                    notifyDropServerCustomCoords = false,
                    notifyDropServerCustomGrid = false,
                    notifyDropServerMass = true,
                    notifyDropPlayer = true,
                    notifyDropDirect = true,
                    notifyDropDirectSpace = true,
                    notifyDropPlayersOnLanded = false,
                    notifyDropServerLooted = false,
                    notifyDropServerLootedCoords = false,
                    notifyDropServerLootedGrid = false,
                    notifyDropSignalByPlayer = false,
                    notifyDropSignalByPlayerCoords = false,
                    notifyDropSignalByPlayerGrid = false,
                    notifyDropAdminSignal = false
                },
                UISettings = new ConfigData.UIOptions
                {
                    SimpleUI_Enable = false,
                    SimpleUI_FontSize = 25,
                    SimpleUI_Top = 0.05f,
                    SimpleUI_Left = 0.1f,
                    SimpleUI_MaxWidth = 0.8f,
                    SimpleUI_MaxHeight = 0.1f,
                    SimpleUI_HideTimer = 10,
                    SimpleUI_NoticeColor = "1 1 1 0.9",
                    SimpleUI_ShadowColor = "0.1 0.1 0.1 0.5"
                },
                TimerSettings = new ConfigData.TimerOptions
                {
                    airdropTimerEnabled = true,
                    airdropTimerCmd = "random",
                    airdropRemoveInBuilt = true,
                    airdropCleanupAtStart = true,
                    airdropTimerMinPlayers = 1,
                    airdropTimerWaitMinutesMin = 30,
                    airdropTimerWaitMinutesMax = 60,
                    airdropTimerResetAfterRandom = false
                },
                TimersSettings = new ConfigData.TimersOptions
                {
                    useRealtimeTimers = false,
                    useGametimeTimers = false,
                    logTimersToConsole = true,
                    realTimers = defaultRealTimers(),
                    serverTimers = defaultServerTimers(),
                    timersMinPlayers = 0
                },
                DropSettings = new ConfigData.DropOptions
                {
                    setupDropTypes = defaultDropTypes(),
                    setupDropDefault = defaultDrop()
                },
                StaticItems = new ConfigData.StaticOptions
                {
                    LootSettings = new List<ConfigData.StaticOptions.LootOptions>
                    {
                        new ConfigData.StaticOptions.LootOptions
                        {
                            DropTypeName = "regular",
                            MaximumItems = 6,
                            MinimumItems = 2,
                            Items = new List<ConfigData.StaticOptions.LootOptions.LootItem>
                            {
                                new ConfigData.StaticOptions.LootOptions.LootItem {Name = "metalspring", Maximum = 6, Minimum = 2, Skin =0, DisplayName = null },
                                new ConfigData.StaticOptions.LootOptions.LootItem {Name = "mining.quarry", Maximum = 2, Minimum = 1, Skin =0, DisplayName = null }
                            }
                        },
                        new ConfigData.StaticOptions.LootOptions
                        {
                            DropTypeName = "massdrop",
                            MaximumItems = 6,
                            MinimumItems = 2,
                            Items = new List<ConfigData.StaticOptions.LootOptions.LootItem>
                            {
                                new ConfigData.StaticOptions.LootOptions.LootItem {Name = "metalpipe", Maximum = 2, Minimum = 1, Skin =0, DisplayName = null },
                                new ConfigData.StaticOptions.LootOptions.LootItem {Name = "pistol.revolver", Maximum = 1, Minimum = 1, Skin =0, DisplayName = null }
                            }
                        },
                        new ConfigData.StaticOptions.LootOptions
                        {
                            DropTypeName = "dropdirect",
                            MaximumItems = 6,
                            MinimumItems = 2,
                            Items = new List<ConfigData.StaticOptions.LootOptions.LootItem>
                            {
                                new ConfigData.StaticOptions.LootOptions.LootItem {Name = "syringe.medical", Maximum = 6, Minimum = 2, Skin =0, DisplayName = null },
                                new ConfigData.StaticOptions.LootOptions.LootItem {Name = "largemedkit", Maximum = 2, Minimum = 1, Skin =0, DisplayName = null }
                            }
                        },
                        new ConfigData.StaticOptions.LootOptions
                        {
                            DropTypeName = "supplysignal",
                            MaximumItems = 6,
                            MinimumItems = 2,
                            Items = new List<ConfigData.StaticOptions.LootOptions.LootItem>
                            {
                                new ConfigData.StaticOptions.LootOptions.LootItem {Name = "metal.fragments", Maximum = 4, Minimum = 1, Skin =0, DisplayName = null },
                                new ConfigData.StaticOptions.LootOptions.LootItem {Name = "metal.facemask", Maximum = 1, Minimum = 1, Skin =0, DisplayName = null }
                            }
                        },
                        new ConfigData.StaticOptions.LootOptions
                        {
                            DropTypeName = "custom_event",
                            MaximumItems = 6,
                            MinimumItems = 2,
                            Items = new List<ConfigData.StaticOptions.LootOptions.LootItem>
                            {
                                new ConfigData.StaticOptions.LootOptions.LootItem {Name = "fish.cooked", Maximum = 6, Minimum = 2, Skin =0, DisplayName = null },
                                new ConfigData.StaticOptions.LootOptions.LootItem {Name = "metal.plate.torso", Maximum = 1, Minimum = 1, Skin =0, DisplayName = null }
                            }
                        }
                    }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Core.VersionNumber(3, 1, 5))
                configData = baseConfig;

            if (configData.Version < new Core.VersionNumber(3, 1, 6))
            {
                configData.DropSettings.setupDropTypes = defaultDropTypes();
                configData.DropSettings.setupDropDefault = defaultDrop();
            }

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion Config

        #region Messaging
        private void MessageToAllGui(string message)
        {
            var msg = string.Format(configData.GenericSettings.Format, configData.GenericSettings.Color, configData.GenericSettings.Prefix) + message;
            rust.RunServerCommand(configData.GenericSettings.guiCommand + " " + msg.Quote());
        }

        private void MessageToPlayerUI(BasePlayer player, string message)
        {
            UIMessage(player, string.Format(configData.GenericSettings.Format, configData.GenericSettings.Color, configData.GenericSettings.Prefix) + message);
        }

        private void MessageToPlayer(BasePlayer player, string message)
        {
            PrintToChat(player, string.Format(configData.GenericSettings.Format, configData.GenericSettings.Color, configData.GenericSettings.Prefix) + $"<color={configData.GenericSettings.colorTextMsg}>" + message + "</color>");
        }

        private string GetDirectionAngle(float angle, string UserIDString)
        {
            if (angle > 337.5 || angle < 22.5)
                return msg("msgNorth", UserIDString);
            else if (angle > 22.5 && angle < 67.5)
                return msg("msgNorthEast", UserIDString);
            else if (angle > 67.5 && angle < 112.5)
                return msg("msgEast", UserIDString);
            else if (angle > 112.5 && angle < 157.5)
                return msg("msgSouthEast", UserIDString);
            else if (angle > 157.5 && angle < 202.5)
                return msg("msgSouth", UserIDString);
            else if (angle > 202.5 && angle < 247.5)
                return msg("msgSouthWest", UserIDString);
            else if (angle > 247.5 && angle < 292.5)
                return msg("msgWest", UserIDString);
            else if (angle > 292.5 && angle < 337.5)
                return msg("msgNorthWest", UserIDString);
            return "";
        }

        private void NotifyOnDropLanded(BaseEntity drop)
        {
            foreach (var player in BasePlayer.activePlayerList.Where(p => Vector3.Distance(p.transform.position, drop.transform.position) < configData.Notifications.supplyDropNotifyDistance).ToList())
            {
                var message = string.Format(msg("msgDropLanded", player.UserIDString), Vector3.Distance(player.transform.position, drop.transform.position), GetDirectionAngle(Quaternion.LookRotation((drop.transform.position - player.eyes.position).normalized).eulerAngles.y, player.UserIDString));
                MessageToPlayer(player, message);
            }
        }

        private void NotifyOnDropLooted(BaseEntity drop, BasePlayer looter)
        {
            string gridPos = GetGridString(drop.transform.position);

            foreach (var player in BasePlayer.activePlayerList.Where(p => Vector3.Distance(p.transform.position, drop.transform.position) < configData.Notifications.supplyLootNotifyDistance).ToList())
                if (configData.Notifications.notifyDropServerLootedCoords)
                    MessageToPlayer(player, string.Format(msg("msgDropLootetCoords", player.UserIDString), looter.displayName, drop.transform.position.x.ToString("0"), drop.transform.position.z.ToString("0")));
                else if (configData.Notifications.notifyDropServerLootedGrid)
                    MessageToPlayer(player, string.Format(msg("msgDropLootetGrid", player.UserIDString), looter.displayName, gridPos));
                else
                    MessageToPlayer(player, string.Format(msg("msgDropLootet", player.UserIDString), looter.displayName));
        }
        #endregion Messaging

        #region Localization
        private static void SendChatMessage(string key, params object[] args)
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                player.ChatMessage(args != null ? string.Format(msg(key, player.UserIDString), args) : msg(key, player.UserIDString));
            }
        }

        private static string msg(string key, string playerId = "") => Instance.lang.GetMessage(key, Instance, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["msgDropSignal"] = "<size=12>Someone ordered an Air Drop</size>",
            ["msgDropSignalCoords"] = "<size=12>Someone ordered an Air Drop to position <color=#ffff00>X:{0} Z:{1}</color></size>",
            ["msgDropSignalAdmin"] = "<size=12>Supply Signal thrown by '{0}' at: {1}</size>",
            ["msgDropSignalByPlayer"] = "<size=12>Supply Signal thrown by <color=#ffff00>{0}</color></size>",
            ["msgDropSignalByPlayerCoords"] = "<size=12>Supply Signal thrown by <color=#ffff00>{0}</color> at position <color=#ffff00>X:{1} Z:{2}</color></size>",
            ["msgDropRegular"] = "<size=12>A Cargo Plane will deliver the daily Air Drop in a few moments</size>",
            ["msgDropRegularCoords"] = "<size=12>A Cargo Plane will deliver the daily Air Drop at <color=#ffff00>X:{0} | Z:{1}</color> in a few moments</size>",
            ["msgDropMass"] = "<size=12>Mass Air Drop incoming</size>",
            ["msgDropCustom"] = "<size=12>Eventdrop <color=#ffa500>{0}</color> is on the way</size>",
            ["msgDropCustomCoords"] = "<size=12>Eventdrop <color=#ffa500>{0}</color> is on the way to <color=#ffff00>X:{1} | Z:{2}</color></size>",
            ["msgDropPlayer"] = "<size=12><color=#ffff00>Incoming Supply Drop</color> to your current location</size>",
            ["msgDropDirect"] = "<size=12><color=#ffff00>A Supply Drop</color> spawned above your <color=#ffff00>current</color> location</size>",
            ["msgDropLanded"] = "<size=12>A Supply Drop has landed <color=#ffff00>{0:F0}m</color> away from you at direction <color=#ffff00>{1}</color></size>",
            ["msgDropLootet"] = "<size=12><color=#0099CC>Someone is looting the Supply Drop</color></size>",
            ["msgDropLootetCoords"] = "<size=12><color=#ffff00>{0}</color> was looting the Supply Drop at (<color=#ffff00>X:{1} | Z:{2}</color>)</size>",
            ["msgNoAccess"] = "<size=12>You are not allowed to use this command</size>",
            ["msgConsoleDropSpawn"] = "<size=12>Supply Drop spawned at (X:{0} Y:{1} Z:{2})</size>",
            ["msgConsoleDropLanded"] = "<size=12>Supply Drop landed at (X:{0} Y:{1} Z:{2})</size>",
            ["msgCrateLocked"] = "<size=12>This crate is locked until it is being looted by the owner</size>",
            ["msgNorth"] = "<size=12>North</size>",
            ["msgNorthEast"] = "<size=12>NorthEast</size>",
            ["msgEast"] = "<size=12>East</size>",
            ["msgSouthEast"] = "<size=12>SouthEast</size>",
            ["msgSouth"] = "<size=12>South</size>",
            ["msgSouthWest"] = "<size=12>SouthWest</size>",
            ["msgWest"] = "<size=12>West</size>",
            ["msgNorthWest"] = "<size=12>NorthWest</size>",
            ["msgDropSignalGrid"] = "Someone ordered an Airdrop to grid area <color=#ffff00>{0}</color></size>",
            ["msgDropSignalByPlayerGrid"] = "Signal thrown by <color=#ffff00>{0}</color> at grid area <color=#ffff00>{1}</color></size>",
            ["msgDropRegularGrid"] = "Cargoplane will deliver the daily AirDrop in grid area <color=#ffff00>{0}</color> in a few moments</size>",
            ["msgDropCustomGrid"] = "Eventdrop <color=#ffa500>{0}</color> is on his way to grid area <color=#ffff00>{1}</color></size>",
            ["msgDropLootetGrid"] = "<color=#ffff00>{0}</color> was looting the AirDrop located grid area (<color=#ffff00>{1}</color>)</size>",
            ["msgDropDirectSpace"] = "A <color=#ffff00>Space Supply Drop</color> is being deployed to your <color=#ffff00>current</color> location</size>"
        };
        #endregion
    }
}
