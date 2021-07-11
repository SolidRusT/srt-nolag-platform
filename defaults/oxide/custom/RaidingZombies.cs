// Requires: ZombieHorde
using System.Collections.Generic;
using Oxide.Core.Configuration;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;
using VLB;
using Rust.Ai;
using Random = UnityEngine.Random;
using UnityEngine.AI;
using Rust.Ai.HTN;
using Rust.Ai.HTN.Sensors;
using Rust.Ai.HTN.Reasoning;
using Rust.Ai.HTN.Murderer;

namespace Oxide.Plugins
{
    [Info("Raiding Zombies", "Razor", "1.0.9", ResourceId = 23)]
    [Description("Make zombies toss C4")]
    public class RaidingZombies : RustPlugin
    {
        public static RaidingZombies _instance;
        private bool isInit;
        private int totalZombies;
		private int totalC4z;
		private int totalRz;
        private static int groundLayer;
        private const string SCARECROW_PREFAB = "assets/prefabs/npc/scarecrow/scarecrow.prefab";
		public bool theSwitchBoom = false;

        #region Config

        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public Settings settings { get; set; }

            public class Settings
            {
                public int chance { get; set; }
                public int totalExplosives { get; set; }
                public int totalRockets { get; set; }
				public string explosive { get; set; } 
                public bool targetPlayerOnly { get; set; }
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
                settings = new ConfigData.Settings
                {
                    chance = 50,
                    totalExplosives = 2,
                    totalRockets = 2,
					explosive = "explosive.timed",
                    targetPlayerOnly = true
                },

                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(1, 0, 1))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion Config

        void OnEntitySpawned(HTNPlayer npc)
        {
			bool theSwitch = false;
			timer.Once(2f, () => {
			if (npc == null) return;
            if (isInit && npc?.name == SCARECROW_PREFAB && npc?.GetComponent<ZombieHorde.HordeMember>() != null)
            {
				if (npc.GetComponent<SuicideController>() != null) return;
                int rando = UnityEngine.Random.Range(0, 100);
                if (rando <= configData.settings.chance)
                {
                    SuicideController controler = npc?.GetOrAddComponent<SuicideController>();
                    if (controler != null && configData.settings.totalExplosives < 1) { theSwitch = true; controler.NotC4NPC = true; }
							else if (controler != null && configData.settings.totalRockets < 1) { theSwitch = false; controler.NotC4NPC = false; }
                            else if (controler != null) controler.NotC4NPC = theSwitch;
                            if (theSwitch) { totalRz++; theSwitch = false; }
						    else { totalC4z++; theSwitch = true; }
                    controler.manager = npc?.GetComponent<ZombieHorde.HordeMember>().Manager;
                }
            }});
        }
		
        private void OnServerInitialized()
        {
            _instance = this;
			if (configData.settings.explosive == null || configData.settings.explosive == "")
			{
				configData.settings.explosive = "explosive.timed";
				SaveConfig();
			} 
            bool theSwitch = false;
            groundLayer = LayerMask.GetMask("Construction", "Terrain", "World");
            timer.Once(60, () =>
            {
                for (int i = ZombieHorde.HordeManager._allHordes.Count - 1; i >= 0; i--)
                    foreach (var g in ZombieHorde.HordeManager._allHordes[i].members)
                    {
                        int rando = UnityEngine.Random.Range(0, 100);
                        if (rando <= configData.settings.chance)
                        {
                            SuicideController controler = g?.GetOrAddComponent<SuicideController>();
							if (controler != null && configData.settings.totalExplosives < 1) { theSwitch = true; controler.NotC4NPC = true; }
							else if (controler != null && configData.settings.totalRockets < 1) { theSwitch = false; controler.NotC4NPC = false; }
                            else if (controler != null) controler.NotC4NPC = theSwitch;
                            if (theSwitch) { totalRz++; theSwitch = false; }
						    else { totalC4z++; theSwitch = true; }
                            totalZombies++;
                            controler.manager = g?.GetComponent<ZombieHorde.HordeMember>().Manager;
                        }
                    }
                isInit = true;
                PrintWarning($"Added a total of {totalZombies} With {totalRz} rocket Zombies and {totalC4z} C4 Zombies");
            });
        }

        private class SuicideController : MonoBehaviour
        {
            private ScientistNPC npc;
            public ZombieHorde.HordeManager manager = null;
            private ZombieHorde.HordeMember hordeMember = null;
            public BasePlayer targetPlayer;
            private DateTime newtargetTime = DateTime.Now;
            private TimedExplosive c4;
            private Vector3 nextPosition = Vector3.zero;
            private BaseEntity targetEntity;
            private bool target;
            private float num1 = 1f;
            public Vector3 StartPos = new Vector3(0f, 0f, 0f);
            public Vector3 EndPos = new Vector3(0f, 0f, 0f);
            public Vector3 LastPos = new Vector3(0f, 0f, 0f);
            public Dictionary<Vector3, float> NoMoveInfo = new Dictionary<Vector3, float>();
            private Vector3 nextPos = new Vector3(0f, 0f, 0f);
            private float waypointDone = 0f;
            public float secondsTaken = 0f;
            private float secondsToTake = 0f;
            private bool shouldMove;
            private Vector3 Vector3Down = new Vector3(0f, -1f, 0f);
            private bool c4Deployed;
            public int totalExplosives;
            public int allowedC4 = 1;
            private DateTime nextDeploy = DateTime.Now;
            public bool NotC4NPC = false;

            private void Awake()
            {
                npc = GetComponent<ScientistNPC>();
                hordeMember = npc.GetComponent<ZombieHorde.HordeMember>();
                InvokeRepeating("lookForTarget", 10f, 30f);
                _instance.NextTick(() =>
                {
                    allowedC4 = _instance.configData.settings.totalExplosives;
                    if (NotC4NPC) allowedC4 = _instance.configData.settings.totalRockets;
                });
            }

            void Update()
            {
                if (shouldMove && StartPos != EndPos) Execute_Move();
                if (manager == null) return;
                if (newtargetTime < DateTime.Now && manager.PrimaryTarget != null && manager.PrimaryTarget is BasePlayer)
                {
                    targetPlayer = manager.PrimaryTarget as BasePlayer;
                    if (targetPlayer.IsSleeping()) targetPlayer = null;
                    newtargetTime = DateTime.Now.AddSeconds(15);
                }
            }

            private void lookForTarget()
            {
                bool isPlayers = false;
                float currentTime = Time.time;
                if (target || totalExplosives >= allowedC4 || nextDeploy > DateTime.Now) return;
                ulong targetID = 0;
                if (_instance.configData.settings.targetPlayerOnly)
                {
                    if (targetPlayer == null || targetPlayer.IsSleeping()) return; //hordeMember.Context.GetFact(Facts.IsRoaming) != 1 REMOVED
                    targetID = targetPlayer.userID;
                }
                List<BaseEntity> nearby = new List<BaseEntity>();
                Vis.Entities<BaseEntity>(npc.transform.position, 40, nearby);
                float closest = float.MaxValue;
                foreach (BaseEntity entity in nearby.Distinct().ToList())
                {
                    isPlayers = false;
                    if (entity == null || entity.OwnerID == 0) continue;
                    if (entity is BuildingBlock || entity.name.Contains("wall") || entity.name.Contains("gates"))
                    {
                        if (_instance.configData.settings.targetPlayerOnly)
                        {
                            BuildingPrivlidge theBlock = entity?.GetBuildingPrivilege();
                            if (theBlock != null)
                            {
                                if (theBlock.IsAuthed(targetID))
                                {
                                    isPlayers = true;
                                }
                            }
                            else if (entity.OwnerID != targetID)
                                continue;
                        }
                        if (isPlayers)
                        {
                            float distance = Vector3.Distance(entity.transform.position, npc.transform.position);
                            if (closest > distance)
                            {
                                closest = distance;
                                nextPosition = entity.transform.position;
                                targetEntity = entity;
                            }
                        }
                        else
                        {
                            float distance = Vector3.Distance(entity.transform.position, npc.transform.position);
                            if (closest > distance)
                            {
                                closest = distance;
                                nextPosition = entity.transform.position;
                                targetEntity = entity;
                            }
                        }
                    }
                }
                if (nextPosition != Vector3.zero)
                {
                    //npc.Pause(); REMOVED
                    target = true;
                    SetMovementPoint(npc.transform.position + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f)), nextPosition, 3.2f);
                }
            }

            public void LookTowards(Vector3 vector3_1)
            {
                if (targetEntity != null)
                {
                    BaseEntity parentEntity = npc.GetParentEntity();
                    if (parentEntity != null)
                    {
                        parentEntity.transform.LookAt(targetEntity.transform);
                    }
                    else
                    {
                        npc.transform.LookAt(targetEntity.transform);
                    }
                }
            }

            public void SetMovementPoint(Vector3 startpos, Vector3 endpos, float s)
            {
                StartPos = startpos;

                if (endpos != Vector3.zero)
                {
                    EndPos = endpos;
                    EndPos.y = Math.Max(EndPos.y, TerrainMeta.HeightMap.GetHeight(EndPos));
                    if (StartPos != EndPos)
                        secondsToTake = Vector3.Distance(EndPos, StartPos) / s;

                    LookTowards(nextPosition);
                    shouldMove = true;
                    // npc.ToPlayer().LookTowards(EndPos);
                }
                secondsTaken = 0f;
                waypointDone = 0f;
            }

            private void Execute_Move()
            {
                if (!shouldMove) return;
                float distance = Vector3.Distance(nextPosition, npc.transform.position);
                if (!NotC4NPC && distance <= 6.0f)
                {
                    shouldMove = false;
                    if (nextDeploy < DateTime.Now)
                    {
                        nextDeploy = DateTime.Now.AddSeconds(20);
                        LookTowards(nextPosition);
                        if (!c4Deployed && targetEntity != null) GetFirststC4();
                        c4Deployed = true;
                    }
                    _instance.timer.Once(4, () => { if (npc == null) return; npc.Resume(); GetFirststC4(false); });
                    _instance.timer.Once(10, () => { target = false; });

                    return;
                }
                else if (NotC4NPC && distance <= 9.0f)
                {
                    shouldMove = false;
                    if (nextDeploy < DateTime.Now)
                    {
                        nextDeploy = DateTime.Now.AddSeconds(20);
                        LookTowards(nextPosition);
                        if (!c4Deployed && targetEntity != null) GetFirststLauncher();
                        c4Deployed = true;
                    }
                    _instance.timer.Once(6, () => { if (npc == null) return; npc.Resume(); GetFirststLauncher(false); });
                    _instance.timer.Once(10, () => { target = false; });

                    return;
                }
                secondsTaken += Time.deltaTime;
                waypointDone = Mathf.InverseLerp(0f, secondsToTake, secondsTaken);
                nextPos = Vector3.Lerp(StartPos, EndPos, waypointDone);
                nextPos.y = GetMoveY(nextPos);
                npc.ToPlayer().MovePosition(nextPos);
                var newEyesPos = nextPos + new Vector3(0, 1.6f, 0);
                npc.ToPlayer().eyes.position.Set(newEyesPos.x, newEyesPos.y, newEyesPos.z);
                //npc.ToPlayer().UpdatePlayerCollider(true);
                npc.ToPlayer().EnablePlayerCollider();
                LookTowards(nextPosition);
            }

            public float GetMoveY(Vector3 position)
            {
                return GetGroundY(position);
            }

            public float GetGroundY(Vector3 position)
            {
                position = position + Vector3.up;
                RaycastHit hitinfo;
                if (Physics.Raycast(position, Vector3Down, out hitinfo, 100f, groundLayer))
                {
                    return hitinfo.point.y;
                }
                return position.y - .5f;
            }

            public void GetFirststLauncher(bool shot = true)
            {
                Item launcher = null;
                Item items = npc.inventory.containerBelt.GetSlot(5);
                if (items != null) npc.inventory.Take(null, items.info.itemid, items.amount);
                if (shot)
                {
                    launcher = ItemManager.CreateByName("rocket.launcher", 1);
                    launcher.MoveToContainer(npc.inventory.containerBelt, 5);
                }
                ChangeWeapon(launcher, shot);
                totalExplosives++;
            }

            public void GetFirststC4(bool c4Toss = true)
            {
                Item itemC4 = null;
                Item items = npc.inventory.containerBelt.GetSlot(5);
                if (items != null) npc.inventory.Take(null, items.info.itemid, items.amount);
                if (c4Toss)
                {
                    itemC4 = ItemManager.CreateByName(_instance.configData.settings.explosive, 1);
					if (itemC4 == null) return;
                    itemC4.MoveToContainer(npc.inventory.containerBelt, 5);
                }
                ChangeWeapon(itemC4, c4Toss);
                totalExplosives++;
            }

            void ChangeWeapon(Item item, bool c4Toss)
            {
                HeldEntity heldEntity = null;

                if (!c4Toss || item == null)
                {
                    foreach (Item item1 in npc.inventory.containerBelt.itemList)
                    {
                        if (item1.GetHeldEntity() as BaseMelee != null)
                        {
                            npc.svActiveItemID = item1.uid;
                            npc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                            heldEntity = item1.GetHeldEntity() as HeldEntity;
                            if (heldEntity != null)
                            {
                                heldEntity.SetHeld(true);
                                npc.inventory.UpdatedVisibleHolsteredItems();
                                npc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                            }
                            break;
                        }
                    }
                    return;
                }

                npc.svActiveItemID = item.uid;
                npc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

                heldEntity = item.GetHeldEntity() as HeldEntity;
                if (heldEntity != null)
                    heldEntity.SetHeld(true);

                npc.inventory.UpdatedVisibleHolsteredItems();
                npc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                npc.transform.LookAt(targetEntity.transform.position);

                if (heldEntity == null) return;
                ThrownWeapon thrown = heldEntity as ThrownWeapon;
                BaseProjectile launcher = heldEntity as BaseProjectile;
                if (thrown == null && launcher == null) return;
                BasePlayer ownerPlayer = thrown?.GetOwnerPlayer();
                if (ownerPlayer == null) ownerPlayer = launcher?.GetOwnerPlayer();
                if (ownerPlayer == null) heldEntity.SetOwnerPlayer(npc.ToPlayer());
                _instance.timer.Once(3, () =>
                {
                    if (targetEntity == null || npc == null) return;
                    LookTowards(targetEntity.transform.position);
                    Vector3 targetPosition = targetEntity.transform.position;
                    Vector3 position = npc.eyes.position;
                    Vector3 vector3 = npc.eyes.BodyForward();
                    npc.transform.LookAt(targetEntity.transform.position);

                    if (item.info.shortname.Contains("launcher") && launcher != null)
                    {
                        var pos = npc.eyes.position;
                        var forward = npc.eyes.HeadForward();
                        var rot = npc.transform.rotation;
                        var aim = npc.serverInput.current.aimAngles;
                        var rocket = GameManager.server.CreateEntity($"assets/prefabs/ammo/rocket/rocket_basic.prefab", npc.eyes.position + npc.eyes.HeadForward(), npc.transform.rotation);
                        if (rocket == null) return;
                        var proj = rocket.GetComponent<ServerProjectile>();
                        if (proj == null) return;
                        proj.InitializeVelocity(Quaternion.Euler(aim) * rocket.transform.forward * 25f);

                        rocket.Spawn();
                    }
                    else if (item.info.shortname.Contains(_instance.configData.settings.explosive))
                    {
                        BaseEntity entity = GameManager.server.CreateEntity(thrown.prefabToThrow.resourcePath, position, Quaternion.LookRotation(thrown.overrideAngle == Vector3.zero ? -vector3 : thrown.overrideAngle), true);
                        entity.creatorEntity = (BaseEntity)npc;
                        Vector3 aimDir = vector3 + Quaternion.AngleAxis(10f, Vector3.right) * Vector3.up;
                        float f = GetThrowVelocity(position, targetPosition, aimDir);
                        if (float.IsNaN(f))
                        {
                            aimDir = vector3 + Vector3.up * 2;
                            f = GetThrowVelocity(position, targetPosition, aimDir);
                            if (float.IsNaN(f))
                                f = 10f;
                        }
                        entity.SetVelocity(aimDir * f * num1);
                        if ((double)thrown.tumbleVelocity > 0.0)
                            entity.SetAngularVelocity(new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * thrown.tumbleVelocity);
                        npc.SignalBroadcast(BaseEntity.Signal.Throw, string.Empty, (Network.Connection)null);
                        entity.Spawn();

                        if (item != null) item.RemoveFromContainer();
                        if (item != null) item.MarkDirty();
                        thrown.StartAttackCooldown(thrown.repeatDelay);
                    }
                    c4Deployed = false;
                    nextPosition = Vector3.zero;
                    targetEntity = null;
                    _instance.timer.Once(5, () =>
                    {
                        if (npc == null) return;
                        ChangeWeapon(null, false);
                        Item items = npc.inventory.containerBelt.GetSlot(5);
                        if (items != null) npc.inventory.Take(null, items.info.itemid, items.amount);
                        npc.inventory.UpdatedVisibleHolsteredItems();
                        npc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    });
                });
            }

            private float GetThrowVelocity(Vector3 throwPos, Vector3 targetPos, Vector3 aimDir)
            {
                Vector3 vector3 = targetPos - throwPos;
                float magnitude1 = new Vector2(vector3.x, vector3.z).magnitude;
                float y1 = vector3.y;
                float magnitude2 = new Vector2(aimDir.x, aimDir.z).magnitude;
                float y2 = aimDir.y;
                return Mathf.Sqrt((float)(0.5 * (double)UnityEngine.Physics.gravity.y * (double)magnitude1 * (double)magnitude1 / ((double)magnitude2 * ((double)magnitude2 * (double)y1 - (double)y2 * (double)magnitude1))));
            }
        }
        object CanEntityTakeDamage(BuildingBlock entity, HitInfo hitinfo)
        {
            if (hitinfo?.Initiator is HTNPlayer) return true;
            return null;
        }
    }
}