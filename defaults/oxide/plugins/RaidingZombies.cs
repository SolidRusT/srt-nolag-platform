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
using System.Collections;
using Facepunch;

using Rust;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("Raiding Zombies", "Razor", "2.0.10", ResourceId = 23)]
    [Description("Make zombies toss C4")]
    public class RaidingZombies : RustPlugin
    {
        public static RaidingZombies _instance;
        private bool isInit;
        private int totalZombies;
        private int totalC4z;
        private int totalRz;
        public bool theSwitch = false;
        public List<uint> raidingZombie = new List<uint>();

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
                public float BaseScanDistance { get; set; }
                public float DamageScale { get; set; }
                public List<string> OnlyHordProfiles { get; set; }
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
                    targetPlayerOnly = true,
                    BaseScanDistance = 40f,
                    DamageScale = 10f,
                    OnlyHordProfiles = new List<string>()
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

        void OnEntitySpawned(ScientistNPC npc)
        {
            timer.Once(2f, () =>
            {
                if (npc == null) return;
                if (isInit && npc?.GetComponent<ZombieHorde.HordeMember>() != null)
                {
                    int rando = UnityEngine.Random.Range(0, 100);
                    if (rando <= configData.settings.chance)
                    {
                        ZombieHorde.HordeMember member = npc?.GetComponent<ZombieHorde.HordeMember>();

                        if (member != null && ZombieHorde.configData.Horde.UseProfiles && !string.IsNullOrEmpty(member.Manager.hordeProfile) && configData.settings.OnlyHordProfiles.Count > 0)
                        {
                            if (configData.settings.OnlyHordProfiles.Contains(member.Manager.hordeProfile))
                            {
                                 npc?.gameObject.GetComponent<BaseAIBrain<global::HumanNPC>>().AddState(new mynewclass(npc.GetComponent<ZombieHorde.HordeMember>()));
                            }
                        }
                        else if (member != null && configData.settings.OnlyHordProfiles == null || configData.settings.OnlyHordProfiles.Count <= 0 || !ZombieHorde.configData.Horde.UseProfiles)
                        {
                            npc?.gameObject.GetComponent<BaseAIBrain<global::HumanNPC>>().AddState(new mynewclass(npc.GetComponent<ZombieHorde.HordeMember>()));
                        }
                    }
                }
            });
        }

        private void OnServerInitialized()
        {
            _instance = this;
            if (configData.settings.OnlyHordProfiles == null)
            {
                configData.settings.OnlyHordProfiles = new List<string>();
                SaveConfig();
            }
            if (configData.settings.explosive == null || configData.settings.explosive == "")
            {
                configData.settings.explosive = "explosive.timed";
                SaveConfig();
            }
            timer.Once(60, () =>
            {
                for (int i = ZombieHorde.HordeManager._allHordes.Count - 1; i >= 0; i--)
                    foreach (var g in ZombieHorde.HordeManager._allHordes[i].members)
                    {
                        int rando = UnityEngine.Random.Range(0, 100);
                        if (rando <= configData.settings.chance)
                        {
                            if (ZombieHorde.configData.Horde.UseProfiles && !string.IsNullOrEmpty(g.Manager.hordeProfile) && configData.settings.OnlyHordProfiles.Count > 0)
                            {
                                if (configData.settings.OnlyHordProfiles.Contains(g.Manager.hordeProfile))
                                {
                                    g?.gameObject.GetComponent<BaseAIBrain<global::HumanNPC>>().AddState(new mynewclass(g.GetComponent<ZombieHorde.HordeMember>()));
                                    totalZombies++;
                                }
                            }
                            else if (!ZombieHorde.configData.Horde.UseProfiles || configData.settings.OnlyHordProfiles.Count <= 0)
                            {
                                g?.gameObject.GetComponent<BaseAIBrain<global::HumanNPC>>().AddState(new mynewclass(g.GetComponent<ZombieHorde.HordeMember>()));
                                totalZombies++;
                            }
                        }
                    }
                isInit = true;
                PrintWarning($"Added a total of {totalZombies} With {totalRz} rocket Zombies and {totalC4z} C4 Zombies");
            });
        }

        private class mynewclass : BaseAIBrain<global::HumanNPC>.BasicAIState
        {
            private readonly ZombieHorde.HordeMember hordeMember;
            private float nextThrowTime = Time.time;
            private float nextTargetTime = Time.time;
            private float nextPositionUpdateTime;
            private float forgetTargetTime = Time.time;
            private bool isThrowingWeapon = false;
            public int totalTossed;
            public int totalBoom;
            public bool isC4 = true;
            public ThrownWeapon _throwableWeapon = null;
            public BaseProjectile _ProectileWeapon = null;
            private Vector3 nextPosition = Vector3.zero;
            private BaseCombatEntity targetEntity;
            private Transform tr;
            public BasePlayer targetPlayer;
            private bool DoNotMove;
            private bool notActive;
			private Vector3 destination = Vector3.zero;
			private bool isSetup { get; set; }
            public mynewclass(ZombieHorde.HordeMember hordeMember) : base(AIState.Cooldown)
            {
                this.hordeMember = hordeMember;
                if (!isSetup) setup();
            }

            public void setup()
            {
				isSetup = true;
				if (_instance.theSwitch == false && _instance.configData.settings.totalExplosives > 0)
                {
                    _instance.totalC4z++;
                    totalBoom = _instance.configData.settings.totalExplosives;
					if (_instance.configData.settings.totalRockets <= 0)
						_instance.theSwitch = false;
                    else _instance.theSwitch = true;
				}
				
                else if (_instance.configData.settings.totalRockets > 0)
                {
                    isC4 = false;
                    totalBoom = _instance.configData.settings.totalRockets;
                    _instance.theSwitch = false;
                    _instance.totalRz++;
                }
            }

            public override float GetWeight()
            { 
				if (!isSetup) setup();
                if (notActive) return 0f;

                if (nextTargetTime < Time.time)
                    lookForTarget();

                if (hordeMember.Entity.currentTarget != null && hordeMember.Entity.currentTarget is BasePlayer)
                    setNewTargetPlayer();

                if (targetEntity == null || notActive)
                    return 0f;

                if (!TargetInThrowableRange())
                    return 20f;

                if (CanThrowWeapon())
                    return 20f;

                if (targetEntity != null)
                    return 20f;

                return 0f;
            }

            private void setNewTargetPlayer()
            {
                if (forgetTargetTime < Time.time)
                {
                    targetPlayer = hordeMember.Entity.currentTarget as BasePlayer;
                    forgetTargetTime = Time.time + 320;
                }
            }

			private object FindBestPointOnNavmesh(Vector3 location, float maxDistance = 4f)
			{
				AIInformationZone informationZone = hordeMember.Entity.GetInformationZone(tr.position);
				object success = informationZone.GetBestMovePointNear(hordeMember.Entity.transform.position, hordeMember.Entity.transform.position, 0f, maxDistance * 0.75f, true, hordeMember.Entity, true);
				if (success is Vector3)
					return success;
				return null;
			}

            private bool TargetInThrowableRange()
            {
                if (targetEntity == null)
                    return false;
				
                if (!isC4) return Vector3.Distance(targetEntity.transform.position, hordeMember.Entity.transform.position) <= 15.5f;
                return Vector3.Distance(targetEntity.transform.position, hordeMember.Entity.transform.position) <= 5.5f;
            }

            public override void StateEnter()
            {
                if (!_instance.raidingZombie.Contains(hordeMember.Entity.net.ID))
                    _instance.raidingZombie.Add(hordeMember.Entity.net.ID);
                hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.Sprint);
                hordeMember.Entity.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, false);
                base.StateEnter();
            }

            public override void StateLeave()
            {
                if (_instance.raidingZombie.Contains(hordeMember.Entity.net.ID))
                    _instance.raidingZombie.Remove(hordeMember.Entity.net.ID);
                hordeMember.Entity.SetDucked(false);
                base.StateLeave();
            }

            public override StateStatus StateThink(float delta)
            {
                base.StateThink(delta);
                if (hordeMember.Manager.HasInterestPoint && targetEntity != null)
                {
                    hordeMember.Manager.SetPrimaryTarget(targetEntity);
                }

                if (hordeMember.Entity.IsDormant)
                    hordeMember.Entity.IsDormant = false;

                if (targetEntity == null)
                {
                    return StateStatus.Error;
                }
                float distanceToTarget = Vector3.Distance(targetEntity.transform.position, hordeMember.Entity.transform.position);
				
				
                if (DoNotMove)
                {
					hordeMember.SetDestination(hordeMember.Entity.transform.position);
                    hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.Walk);
                    hordeMember.Entity.Stop();
                }
                else
                {
                    if (!DoNotMove)
                    {   
                        if (TargetInThrowableRange() && Time.time < nextThrowTime)
                        {
                            hordeMember.Entity.SetAimDirection((nextPosition - hordeMember.Entity.transform.position).normalized);
                            hordeMember.Entity.Stop();
                        }
                        else if (Time.time > nextPositionUpdateTime)
                        {
							if (!isC4) destination = hordeMember.Entity.GetRandomPositionAround(nextPosition, 10f, 11f);
							else destination = hordeMember.Entity.GetRandomPositionAround(nextPosition, 2f, 3f);
							
                            hordeMember.SetDestination(destination);

                            if (distanceToTarget < 9f)
                            {
                                hordeMember.Entity.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
                                hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.Walk);
                            }
                            else
                            {
                                hordeMember.Entity.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, false);
                                hordeMember.Entity.SetDesiredSpeed(global::HumanNPC.SpeedType.Sprint);
                            }
                            nextPositionUpdateTime = Time.time + 3f;
                        }
                    }
                    else
                    {
                        nextPositionUpdateTime = Time.time + 1f;
                        hordeMember.Entity.SetDucked(true);
                        hordeMember.Entity.SetAimDirection((nextPosition - hordeMember.Entity.transform.position).normalized);
                        hordeMember.Entity.Stop();
                    }
                }

                return StateStatus.Running;
            }

            private void lookForTarget()
            {
                nextTargetTime = Time.time + 15f;
                if (notActive || nextThrowTime > Time.time || targetEntity != null || isThrowingWeapon) return; //hordeMember.Entity.currentTarget != null

                ulong targetID = 0;
                if (_instance.configData.settings.targetPlayerOnly)
                {
                    if (targetPlayer == null || targetPlayer.IsSleeping()) return;
                    targetID = targetPlayer.userID;
                }

                List<BaseCombatEntity> nearby = new List<BaseCombatEntity>();
                Vis.Entities<BaseCombatEntity>(hordeMember.Entity.transform.position, _instance.configData.settings.BaseScanDistance, nearby);
                float closest = float.MaxValue;
                foreach (BaseCombatEntity entity in nearby.Distinct().ToList())
                {
                    if (entity == null || entity.OwnerID == 0 || entity is BasePlayer || entity.IsNpc || entity is ZombieHorde.HordeMember || entity == hordeMember.Entity) continue;

                    if (isBuilding((entity)))
                    {
                        if (_instance.configData.settings.targetPlayerOnly)
                            if (!isPlayersBuilding(entity, targetID)) continue;

                        float distance = Vector3.Distance(entity.transform.position, hordeMember.Entity.transform.position);
                        if (closest > distance)
                        {
                            closest = distance;
                            nextPosition = entity.transform.position;
                            targetEntity = entity;
                            tr = entity.transform;
                            hordeMember.Entity.currentTarget = entity;
                            hordeMember.Entity.SetDestination(nextPosition);
							//Vector3 dest = hordeMember.Entity.GetRandomPositionAround(nextPosition, 4f, 5f);
							destination = nextPosition;
                        }
                    }
                }
            }

            private bool CanThrowWeapon()
            {
                if (Time.time < nextThrowTime)
                    return false;

                if (targetEntity == null)
                    return false;

                TryThrowBoom();
                nextThrowTime = Time.time + 15f;
                return true;
            }

            private bool isBuilding(BaseCombatEntity entity)
            {
                if (entity == null || entity.IsDestroyed || entity.IsDead()) return false;
                if (entity is BuildingBlock || entity.name.Contains("wall") || entity.name.Contains("gates"))
                {
                    return true;
                }
                return false;
            }

            private bool isPlayersBuilding(BaseCombatEntity entity, ulong playerID)
            {
                BuildingPrivlidge theBlock = entity?.GetBuildingPrivilege();
                if (theBlock != null && theBlock.IsAuthed(playerID))
                    return true;
                else if (entity.OwnerID == playerID)
                    return true;
                return false;
            }

            private void TryThrowBoom()
            {
                if (isThrowingWeapon)
                    return;

                isThrowingWeapon = true;
                hordeMember.Entity.StartCoroutine(ThrowWeaponBoom());
            }

            private IEnumerator ThrowWeaponBoom()
            {
                EquipThrowable();
                yield return CoroutineEx.waitForSeconds(1.7f + UnityEngine.Random.value);

                if (targetEntity != null)
                {
                    hordeMember.Entity.SetAimDirection((nextPosition - hordeMember.Entity.transform.position).normalized);

                    DoNotMove = true;                   
                    hordeMember.Entity.Stop();
                    yield return CoroutineEx.waitForSeconds(1.0f);
                    if (isC4 && _throwableWeapon != null) _throwableWeapon.ServerThrow(nextPosition);
                    else if (_ProectileWeapon != null)
                    {
                        hordeMember.Entity.SetDucked(true);
                        yield return CoroutineEx.waitForSeconds(1.05f);
                        hordeMember.Entity.SetAimDirection((nextPosition - hordeMember.Entity.transform.position).normalized);
                        yield return CoroutineEx.waitForSeconds(0.8f);
                        fireRocket();
                        yield return CoroutineEx.waitForSeconds(2.0f);
                        DoNotMove = false;
                    }

                    nextThrowTime = Time.time + 15f;					
                }

                yield return CoroutineEx.waitForSeconds(1f);
                hordeMember.Entity.SetDucked(false);
                hordeMember.Entity.EquipWeapon();
                removeweapons();
                yield return CoroutineEx.waitForSeconds(0.1f);

                totalTossed++;
                if (totalTossed >= totalBoom)
				{
                    notActive = true;
				}
                isThrowingWeapon = false;
				ServerMgr.Instance.StopCoroutine(ThrowWeaponBoom());
            }

            private void EquipThrowable()
            {
                if (targetEntity == null || targetEntity.IsDestroyed || targetEntity.IsDead())
                    return;
                Item itemC4 = null;
                hordeMember.Entity.inventory.containerBelt.capacity = 6;
                Item items = hordeMember.Entity.inventory.containerBelt.GetSlot(5);
                if (items != null) hordeMember.Entity.inventory.Take(null, items.info.itemid, items.amount);
                if (isC4)
                {
                    itemC4 = ItemManager.CreateByName(_instance.configData.settings.explosive, 1);
                    if (itemC4 == null) return;
                    itemC4.MoveToContainer(hordeMember.Entity.inventory.containerBelt, 5);
                    _throwableWeapon = itemC4.GetHeldEntity() as ThrownWeapon;
                    hordeMember.Entity.UpdateActiveItem(hordeMember.Entity.inventory.containerBelt.GetSlot(5).uid);
                    if (_throwableWeapon == null) return;
                    _throwableWeapon.SetHeld(true);
                    hordeMember.Entity.inventory.UpdatedVisibleHolsteredItems();
                    hordeMember.Entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }
                else
                {
                    Item launcher = null;
                    launcher = ItemManager.CreateByName("rocket.launcher", 1);
                    launcher.MoveToContainer(hordeMember.Entity.inventory.containerBelt, 5);
                    hordeMember.Entity.UpdateActiveItem(hordeMember.Entity.inventory.containerBelt.GetSlot(5).uid);
                    _ProectileWeapon = launcher.GetHeldEntity() as BaseProjectile;
                    if (_ProectileWeapon == null) return;
                    _ProectileWeapon.SetHeld(true);
                    hordeMember.Entity.inventory.UpdatedVisibleHolsteredItems();
                    hordeMember.Entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }
            }

            private void fireRocket()
            {
                if (_ProectileWeapon != null)
                {
                    var aim = hordeMember.Entity.serverInput.current.aimAngles;
                    var rocket = GameManager.server.CreateEntity($"assets/prefabs/ammo/rocket/rocket_basic.prefab", hordeMember.Entity.eyes.position + hordeMember.Entity.eyes.HeadForward(), hordeMember.Entity.transform.rotation);
                    if (rocket == null) return;
                    var proj = rocket.GetComponent<ServerProjectile>();
                    if (proj == null) return;
					rocket.creatorEntity = hordeMember.Entity;
                    proj.InitializeVelocity(Quaternion.Euler(aim) * rocket.transform.forward * 25f);
                    rocket.Spawn();
                }
            }
            private void removeweapons()
            {
                Item itemC4 = null;
                hordeMember.Entity.inventory.containerBelt.capacity = 6;
                Item items = hordeMember.Entity.inventory.containerBelt.GetSlot(5);
                if (items != null) hordeMember.Entity.inventory.Take(null, items.info.itemid, items.amount);
            }
        }

		object OnEntityTakeDamage(ScientistNPC entity, HitInfo hitinfo)
		{
			if (hitinfo == null || hitinfo.WeaponPrefab == null || hitinfo.Initiator == null) return null;
			if (hitinfo.WeaponPrefab.ShortPrefabName.Contains("rocket") && hitinfo.Initiator is ScientistNPC)
				return false;
			if (hitinfo.WeaponPrefab.ShortPrefabName.Contains("timed") && hitinfo.Initiator is ScientistNPC)
				return false;
			return null;
		}
		
        object CanEntityTakeDamage(BuildingBlock entity, HitInfo hitinfo)
        {
            if (hitinfo?.Initiator is ScientistNPC) return true;
            return null;
        }
		object CanEntityTakeDamage(ScientistNPC entity, HitInfo hitinfo)
        {
            if (hitinfo == null || hitinfo.WeaponPrefab == null || hitinfo.Initiator == null) return null;
			if (hitinfo.WeaponPrefab.ShortPrefabName.Contains("rocket") && hitinfo.Initiator is ScientistNPC)
				return false;
			if (hitinfo.WeaponPrefab.ShortPrefabName.Contains("timed") && hitinfo.Initiator is ScientistNPC)
				return false;
			return null;
        }
		
        private void OnEntityTakeDamage(BuildingBlock baseCombatEntity, HitInfo hitInfo)
        {
            if (hitInfo != null && baseCombatEntity != null && configData.settings.DamageScale > 0f)
            {
				if (hitInfo.WeaponPrefab != null && hitInfo.WeaponPrefab.ShortPrefabName.Contains("beancan")) return;
				
				if (hitInfo.damageTypes == null || hitInfo.damageTypes.Get(DamageType.Explosion) <= 0) return;
				
                ZombieHorde.HordeMember hordeMember;

                if (hitInfo.InitiatorPlayer != null && hitInfo.InitiatorPlayer.net != null && raidingZombie.Contains(hitInfo.InitiatorPlayer.net.ID))
                {
                    hordeMember = hitInfo.InitiatorPlayer.GetComponent<ZombieHorde.HordeMember>();
                    if (hordeMember != null)
                    {
                        if (hitInfo.damageTypes.Get(DamageType.Explosion) > 0)
                        {
                            hitInfo.damageTypes.Scale(DamageType.Explosion, configData.settings.DamageScale);
                            return;
                        }
                    }
                }
            }
        }
		
    }
}