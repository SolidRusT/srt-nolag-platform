using System;
using Facepunch;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using Rust;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using Oxide.Plugins.NpcSpawnExtensionMethods;

namespace Oxide.Plugins
{
    [Info("NpcSpawn", "KpucTaJl", "2.1.7")]
    internal class NpcSpawn : RustPlugin
    {
        #region Config
        internal class NpcBelt { public string ShortName; public int Amount; public ulong SkinID; public List<string> Mods; }

        internal class NpcWear { public string ShortName; public ulong SkinID; }

        internal class NpcConfig
        {
            public string Name { get; set; }
            public List<NpcWear> WearItems { get; set; }
            public List<NpcBelt> BeltItems { get; set; }
            public string Kit { get; set; }
            public float Health { get; set; }
            public float RoamRange { get; set; }
            public float ChaseRange { get; set; }
            public float DamageScale { get; set; }
            public float AimConeScale { get; set; }
            public bool DisableRadio { get; set; }
            public bool Stationary { get; set; }
            public bool CanUseWeaponMounted { get; set; }
            public bool CanRunAwayWater { get; set; }
            public float Speed { get; set; }
            public int AreaMask { get; set; }
            public int AgentTypeID { get; set; }
            public SensoryStats Sensory { get; set; }
        }

        public class SensoryStats
        {
            public float AttackRangeMultiplier { get; set; }
            public float SenseRange { get; set; }
            public float MemoryDuration { get; set; }
            public bool CheckVisionCone { get; set; }
            public float VisionCone { get; set; }

            public void ApplySettingsToBrain(BaseAIBrain<global::HumanNPC> brain)
            {
                brain.MaxGroupSize = int.MaxValue;
                brain.AttackRangeMultiplier = AttackRangeMultiplier;
                brain.SenseRange = SenseRange;
                brain.MemoryDuration = MemoryDuration;
                brain.TargetLostRange = SenseRange * 2f;
                brain.CheckVisionCone = CheckVisionCone;
                brain.VisionCone = Vector3.Dot(Vector3.forward, Quaternion.Euler(0f, VisionCone, 0f) * Vector3.forward);
            }
        }
        #endregion Config

        #region Methods
        private ScientistNPC SpawnNpc(Vector3 position, JObject configJson)
        {
            if (Rust.Ai.AiManager.nav_disable) return null;
            CustomScientistNpc npc = CreateCustomNpc(position, configJson.ToObject<NpcConfig>());
            if (npc != null) _scientists.Add(npc.net.ID, npc);
            return npc;
        }

        private static CustomScientistNpc CreateCustomNpc(Vector3 position, NpcConfig config)
        {
            ScientistNPC scientistNpc = GameManager.server.CreateEntity("assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab", position, Quaternion.identity, false) as ScientistNPC;
            if (scientistNpc == null) return null;
            ScientistBrain scientistBrain = scientistNpc.GetComponent<ScientistBrain>();
            CustomScientistNpc customScientist = scientistNpc.gameObject.AddComponent<CustomScientistNpc>();
            CustomScientistBrain customScientistBrain = scientistNpc.gameObject.AddComponent<CustomScientistBrain>();
            CopySerializableFields<ScientistNPC>(scientistNpc, customScientist);
            customScientist.enableSaving = false;
            customScientist.Config = config;
            customScientistBrain.UseQueuedMovementUpdates = scientistBrain.UseQueuedMovementUpdates;
            customScientistBrain.AllowedToSleep = false;
            customScientistBrain.DefaultDesignSO = scientistBrain.DefaultDesignSO;
            customScientistBrain.Designs = new List<AIDesignSO>(scientistBrain.Designs);
            customScientistBrain.InstanceSpecificDesign = scientistBrain.InstanceSpecificDesign;
            customScientistBrain.CheckLOS = scientistBrain.CheckLOS;
            customScientistBrain.UseAIDesign = true;
            customScientistBrain.Pet = false;
            UnityEngine.Object.DestroyImmediate(scientistBrain, true);
            UnityEngine.Object.DestroyImmediate(scientistNpc, true);
            customScientist.gameObject.AwakeFromInstantiate();
            customScientist.Spawn();
            return customScientist;
        }

        private static void CopySerializableFields<T>(T src, T dst)
        {
            FieldInfo[] srcFields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in srcFields)
            {
                object value = field.GetValue(src);
                field.SetValue(dst, value);
            }
        }

        private void AddTargetRaid(CustomScientistNpc npc, BuildingPrivlidge cupboard)
        {
            if (npc == null || cupboard == null || !_scientists.ContainsKey(npc.net.ID)) return;
            npc.AddTargetRaid(cupboard);
        }

        private void AddTargetGuard(CustomScientistNpc npc, BaseEntity target)
        {
            if (npc == null || target == null || !_scientists.ContainsKey(npc.net.ID)) return;
            npc.AddTargetGuard(target);
        }
        #endregion Methods

        #region Controller
        internal class DefaultSettings { public float EffectiveRange; public float AttackLengthMin; public float AttackLengthMax; }

        private readonly Dictionary<string, DefaultSettings> _weapons = new Dictionary<string, DefaultSettings>
        {
            ["rifle.bolt"] = new DefaultSettings { EffectiveRange = 150f, AttackLengthMin = -1f, AttackLengthMax = -1f },
            ["bow.compound"] = new DefaultSettings { EffectiveRange = 15f, AttackLengthMin = -1f, AttackLengthMax = -1f },
            ["smg.2"] = new DefaultSettings { EffectiveRange = 20f, AttackLengthMin = 0.4f, AttackLengthMax = 0.4f },
            ["shotgun.double"] = new DefaultSettings { EffectiveRange = 10f, AttackLengthMin = 0.3f, AttackLengthMax = 1f },
            ["pistol.eoka"] = new DefaultSettings { EffectiveRange = 10f, AttackLengthMin = -1f, AttackLengthMax = -1f },
            ["rifle.l96"] = new DefaultSettings { EffectiveRange = 150f, AttackLengthMin = -1f, AttackLengthMax = -1f },
            ["pistol.nailgun"] = new DefaultSettings { EffectiveRange = 15f, AttackLengthMin = 0f, AttackLengthMax = 0.46f },
            ["pistol.python"] = new DefaultSettings { EffectiveRange = 15f, AttackLengthMin = 0.175f, AttackLengthMax = 0.525f },
            ["pistol.semiauto"] = new DefaultSettings { EffectiveRange = 15f, AttackLengthMin = 0f, AttackLengthMax = 0.46f },
            ["smg.thompson"] = new DefaultSettings { EffectiveRange = 20f, AttackLengthMin = 0.4f, AttackLengthMax = 0.4f },
            ["shotgun.waterpipe"] = new DefaultSettings { EffectiveRange = 10f, AttackLengthMin = -1f, AttackLengthMax = -1f },
            ["multiplegrenadelauncher"] = new DefaultSettings { EffectiveRange = 10f, AttackLengthMin = -1f, AttackLengthMax = -1f },
            ["snowballgun"] = new DefaultSettings { EffectiveRange = 5f, AttackLengthMin = 2f, AttackLengthMax = 2f }
        };

        public class CustomScientistNpc : ScientistNPC, IAIAttack
        {
            public NpcConfig Config { get; set; }

            public Transform Transform { get; set; }

            public BasePlayer CurrentTarget { get; set; }

            public AttackEntity CurrentWeapon { get; set; }

            public Vector3 HomePosition { get; set; }

            public float DistanceFromBase => Vector3.Distance(Transform.position, HomePosition);

            public float DistanceToTarget => Vector3.Distance(Transform.position, CurrentTarget.transform.position);

            public override void ServerInit()
            {
                Transform = transform;
                HomePosition = Transform.position;
                if (NavAgent == null) NavAgent = GetComponent<NavMeshAgent>();
                if (NavAgent != null)
                {
                    if (Config.AreaMask == 0)
                    {
                        NavAgent.areaMask = 1;
                        NavAgent.agentTypeID = -1372625422;
                    }
                    else
                    {
                        NavAgent.areaMask = Config.AreaMask;
                        NavAgent.agentTypeID = Config.AgentTypeID;
                    }
                }
                startHealth = Config.Health;
                damageScale = Config.DamageScale;
                base.ServerInit();
                if (Config.DisableRadio)
                {
                    CancelInvoke(PlayRadioChatter);
                    RadioChatterEffects = Array.Empty<GameObjectRef>();
                    DeathEffects = Array.Empty<GameObjectRef>();
                }
                ClearContainer(inventory.containerWear);
                ClearContainer(inventory.containerBelt);
                if (!string.IsNullOrEmpty(Config.Kit) && _ins.Kits != null) _ins.Kits.Call("GiveKit", this, Config.Kit);
                else UpdateInventory();
                Invoke(SetDisplayName, 1f);
                InvokeRepeating(LightCheck, 1f, 30f);
                InvokeRepeating(UpdateTick, 1f, 2f);
            }

            private void UpdateInventory()
            {
                if (Config.WearItems.Count > 0)
                {
                    foreach (Item item in Config.WearItems.Select(x => ItemManager.CreateByName(x.ShortName, 1, x.SkinID)))
                    {
                        if (item == null) continue;
                        if (!item.MoveToContainer(inventory.containerWear)) item.Remove();
                    }
                }
                if (Config.BeltItems.Count > 0)
                {
                    foreach (NpcBelt npcItem in Config.BeltItems)
                    {
                        Item item = ItemManager.CreateByName(npcItem.ShortName, npcItem.Amount, npcItem.SkinID);
                        if (item == null) continue;
                        foreach (ItemDefinition itemDefinition in npcItem.Mods.Select(ItemManager.FindItemDefinition).Where(x => x != null)) item.contents.AddItem(itemDefinition, 1);
                        if (!item.MoveToContainer(inventory.containerBelt)) item.Remove();
                    }
                }
            }

            private static void ClearContainer(ItemContainer container)
            {
                List<Item> allItems = container.itemList;
                for (int i = allItems.Count - 1; i >= 0; i--)
                {
                    Item item = allItems[i];
                    item.RemoveFromContainer();
                    item.Remove();
                }
            }

            private void SetDisplayName() => displayName = Config.Name;

            private void OnDestroy()
            {
                if (_healCoroutine != null) ServerMgr.Instance.StopCoroutine(_healCoroutine);
                if (_fireC4Coroutine != null) ServerMgr.Instance.StopCoroutine(_fireC4Coroutine);
                if (_fireRocketLauncherCoroutine != null) ServerMgr.Instance.StopCoroutine(_fireRocketLauncherCoroutine);
                CancelInvoke();
            }

            private void UpdateTick()
            {
                if (CanRunAwayWater()) RunAwayWater();
                if (CanThrownGrenade()) ThrownGrenade(CurrentTarget.transform.position);
                if (CanHeal()) _healCoroutine = ServerMgr.Instance.StartCoroutine(Heal());
                if (inventory.containerBelt.itemList.Any(x => x.info.shortname == "rocket.launcher" || x.info.shortname == "explosive.timed") && Foundations.Count == 0)
                {
                    if (CurrentTarget == null)
                    {
                        FirstTarget = null;
                        CurrentRaidTarget = null;
                    }
                    else
                    {
                        BuildingBlock block = GetNearEntity<BuildingBlock>(CurrentTarget.transform.position, 0.1f, 1 << 21);
                        if (block.IsExists() && IsTeam(CurrentTarget, block.OwnerID)) FirstTarget = block;
                        else
                        {
                            FirstTarget = null;
                            CurrentRaidTarget = null;
                        }
                    }
                }
                if (_beforeGuardHomePosition != Vector3.zero)
                {
                    if (_guardTarget.IsExists()) HomePosition = _guardTarget.transform.position;
                    else
                    {
                        HomePosition = _beforeGuardHomePosition;
                        _beforeGuardHomePosition = Vector3.zero;
                        _guardTarget = null;
                    }
                }
            }

            #region Targeting
            public new BasePlayer GetBestTarget()
            {
                BasePlayer target = null;
                float delta = -1f;
                foreach (BasePlayer basePlayer in Brain.Senses.Memory.Targets.OfType<BasePlayer>())
                {
                    if (!CanTargetBasePlayer(basePlayer)) continue;
                    float rangeDelta = 1f - Mathf.InverseLerp(1f, Brain.SenseRange, Vector3.Distance(basePlayer.transform.position, Transform.position));
                    if (Config.Sensory.CheckVisionCone)
                    {
                        float dot = Vector3.Dot((basePlayer.transform.position - eyes.position).normalized, eyes.BodyForward());
                        if (dot < Brain.VisionCone) continue;
                        rangeDelta += Mathf.InverseLerp(Brain.VisionCone, 1f, dot) / 2f;
                    }
                    rangeDelta += (Brain.Senses.Memory.IsLOS(basePlayer) ? 2f : 0f);
                    if (rangeDelta <= delta) continue;
                    target = basePlayer;
                    delta = rangeDelta;
                }
                return target;
            }

            public bool CanTargetBasePlayer(BasePlayer player)
            {
                if (player == null || player.Health() <= 0f || IsRunAwayWater) return false;
                if (player.IsFlying || player.IsSleeping() || player.IsWounded() || player.IsDead() || player.InSafeZone()) return false;
                return player.userID.IsSteamId();
            }
            #endregion Targeting

            #region Equip Weapons
            private bool _isEquippingWeapon;

            public override void EquipWeapon()
            {
                if (!_isEquippingWeapon)
                {
                    Item slot = inventory.containerBelt.itemList.FirstOrDefault(x => x.info.shortname != "syringe.medical" && x.info.shortname != "grenade.f1" && x.info.shortname != "grenade.beancan" && x.info.shortname != "grenade.smoke" && x.info.shortname != "explosive.timed" && x.info.shortname != "rocket.launcher");
                    if (slot != null) StartCoroutine(EquipItem(slot));
                }
            }

            private IEnumerator EquipItem(Item slot = null)
            {
                if (inventory != null && inventory.containerBelt != null)
                {
                    _isEquippingWeapon = true;
                    if (slot == null)
                    {
                        for (int i = 0; i < inventory.containerBelt.itemList.Count; i++)
                        {
                            Item item = inventory.containerBelt.GetSlot(i);
                            if (item?.GetHeldEntity() is AttackEntity)
                            {
                                slot = item;
                                break;
                            }
                        }
                    }
                    if (slot != null)
                    {
                        if (CurrentWeapon != null)
                        {
                            CurrentWeapon.SetHeld(false);
                            CurrentWeapon = null;
                            SendNetworkUpdate();
                            inventory.UpdatedVisibleHolsteredItems();
                        }
                        yield return CoroutineEx.waitForSeconds(0.5f);
                        UpdateActiveItem(slot.uid);
                        HeldEntity heldEntity = slot.GetHeldEntity() as HeldEntity;
                        if (heldEntity != null)
                        {
                            if (heldEntity is AttackEntity) (heldEntity as AttackEntity).TopUpAmmo();
                            if (heldEntity is Chainsaw) (heldEntity as Chainsaw).ServerNPCStart();
                            if (heldEntity is BaseProjectile && _ins._weapons.ContainsKey(slot.info.shortname))
                            {
                                AttackEntity attackEntity = heldEntity as AttackEntity;
                                attackEntity.effectiveRange = _ins._weapons[slot.info.shortname].EffectiveRange;
                                attackEntity.attackLengthMin = _ins._weapons[slot.info.shortname].AttackLengthMin;
                                attackEntity.attackLengthMax = _ins._weapons[slot.info.shortname].AttackLengthMax;
                            }
                        }
                        CurrentWeapon = heldEntity as AttackEntity;
                    }
                    _isEquippingWeapon = false;
                }
            }

            internal void HolsterWeapon()
            {
                svActiveItemID = 0;
                Item activeItem = GetActiveItem();
                if (activeItem != null)
                {
                    HeldEntity heldEntity = activeItem.GetHeldEntity() as HeldEntity;
                    if (heldEntity != null) heldEntity.SetHeld(false);
                }
                SendNetworkUpdate();
                inventory.UpdatedVisibleHolsteredItems();
            }
            #endregion Equip Weapons

            protected override string OverrideCorpseName() => displayName;

            public override float GetAimConeScale() => Config.AimConeScale;

            #region Heal
            private Coroutine _healCoroutine = null;
            private bool _isReloadHeal = false;

            private bool CanHeal()
            {
                if (_isReloadHeal || health >= Config.Health || CurrentTarget != null || _isEquippingWeapon || IsFireC4 || IsFireRocketLauncher) return false;
                return inventory.containerBelt.itemList.Any(x => x.info.shortname == "syringe.medical");
            }

            private IEnumerator Heal()
            {
                _isReloadHeal = true;
                Item syringe = inventory.containerBelt.itemList.FirstOrDefault(x => x.info.shortname == "syringe.medical");
                UpdateActiveItem(syringe.uid);
                yield return CoroutineEx.waitForSeconds(1.5f);
                EquipWeapon();
                InitializeHealth(health + 15f > Config.Health ? Config.Health : health + 15f, Config.Health);
                if (syringe.amount == 1) syringe.Remove();
                else
                {
                    syringe.amount--;
                    syringe.MarkDirty();
                }
                _isReloadHeal = false;
            }
            #endregion Heal

            #region Grenades
            private readonly HashSet<string> _barricades = new HashSet<string>
            {
                "barricade.cover.wood",
                "barricade.sandbags",
                "barricade.concrete",
                "barricade.stone"
            };
            private bool _isReloadGrenade = false;
            private bool _isReloadSmoke = false;

            private void FinishReloadGrenade() => _isReloadGrenade = false;

            private void FinishReloadSmoke() => _isReloadSmoke = false;

            private bool CanThrownGrenade()
            {
                if (_isReloadGrenade || CurrentTarget == null) return false;
                if (isMounted && !Config.CanUseWeaponMounted) return false;
                return DistanceToTarget < 15f && inventory.containerBelt.itemList.Any(x => x.info.shortname == "grenade.f1" || x.info.shortname == "grenade.beancan") && (!CanSeeTarget(CurrentTarget) || IsBehindBarricade());
            }

            internal bool IsBehindBarricade() => CanSeeTarget(CurrentTarget) && IsBarricade();

            private bool IsBarricade()
            {
                SetAimDirection((CurrentTarget.transform.position - Transform.position).normalized);
                RaycastHit[] hits = Physics.RaycastAll(eyes.HeadRay());
                GamePhysics.Sort(hits);
                return hits.Select(x => x.GetEntity() as Barricade).Any(x => x != null && _barricades.Contains(x.ShortPrefabName) && Vector3.Distance(Transform.position, x.transform.position) < DistanceToTarget);
            }

            private void ThrownGrenade(Vector3 target)
            {
                Item item = inventory.containerBelt.itemList.FirstOrDefault(x => x.info.shortname == "grenade.f1" || x.info.shortname == "grenade.beancan");
                if (item != null)
                {
                    GrenadeWeapon weapon = item.GetHeldEntity() as GrenadeWeapon;
                    if (weapon != null)
                    {
                        Brain.Navigator.Stop();
                        SetAimDirection((target - Transform.position).normalized);
                        weapon.ServerThrow(target);
                        _isReloadGrenade = true;
                        Invoke(FinishReloadGrenade, 10f);
                    }
                }
            }

            internal void ThrownSmoke()
            {
                if (!_isReloadSmoke)
                {
                    Item item = inventory.containerBelt.itemList.FirstOrDefault(x => x.info.shortname == "grenade.smoke");
                    if (item != null)
                    {
                        GrenadeWeapon weapon = item.GetHeldEntity() as GrenadeWeapon;
                        if (weapon != null)
                        {
                            weapon.ServerThrow(Transform.position);
                            _isReloadSmoke = true;
                            Invoke(FinishReloadSmoke, 30f);
                        }
                    }
                }
            }
            #endregion Grenades

            #region Run Away Water
            internal bool IsRunAwayWater = false;

            private bool CanRunAwayWater()
            {
                if (!Config.CanRunAwayWater || IsRunAwayWater) return false;
                if (CurrentTarget == null)
                {
                    if (Transform.position.y < -0.25f) return true;
                    else return false;
                }
                if (Transform.position.y > -0.25f || TerrainMeta.HeightMap.GetHeight(CurrentTarget.transform.position) > -0.25f) return false;
                if (CurrentWeapon is BaseProjectile && DistanceToTarget < EngagementRange()) return false;
                if (CurrentWeapon is BaseMelee && DistanceToTarget < CurrentWeapon.effectiveRange) return false;
                return true;
            }

            private void RunAwayWater()
            {
                IsRunAwayWater = true;
                CurrentTarget = null;
                Invoke(FinishRunAwayWater, 20f);
            }

            private void FinishRunAwayWater() => IsRunAwayWater = false;
            #endregion Run Away Water

            #region Raid
            internal bool IsReloadC4 = false;
            internal bool IsReloadRocketLauncher = false;
            internal bool IsFireRocketLauncher = false;
            internal bool IsFireC4 = false;
            private Coroutine _fireC4Coroutine = null;
            private Coroutine _fireRocketLauncherCoroutine = null;
            internal BaseCombatEntity Turret = null;
            internal BaseCombatEntity FirstTarget = null;
            internal BuildingPrivlidge MainCupboard = null;
            internal HashSet<BoxStorage> Boxes = new HashSet<BoxStorage>();
            internal HashSet<BuildingPrivlidge> Cupboards = new HashSet<BuildingPrivlidge>();
            internal HashSet<BuildingBlock> Foundations = new HashSet<BuildingBlock>();
            internal BaseCombatEntity CurrentRaidTarget = null;

            internal void AddTargetRaid(BuildingPrivlidge cupboard)
            {
                Cupboards = GetCupboards(cupboard);
                Boxes = GetBoxes(Cupboards);
                HashSet<BuildingBlock> allBlocks = GetBlocks(Cupboards);
                Foundations = allBlocks.Where(x => x.ShortPrefabName.Contains("foundation"));
                Vector3 centerHome = GetCenterHomePos(allBlocks);
                MainCupboard = Cupboards.Min(x => Vector3.Distance(x.transform.position, centerHome));
                Cupboards.Remove(MainCupboard);
            }

            internal void AddTurret(BaseCombatEntity turret)
            {
                if (!Turret.IsExists() || Vector3.Distance(Transform.position, turret.transform.position) < Vector3.Distance(Transform.position, Turret.transform.position))
                {
                    Turret = turret;
                    BuildingBlock block = GetNearEntity<BuildingBlock>(Turret.transform.position, 0.1f, 1 << 21);
                    CurrentRaidTarget = block.IsExists() ? block : Turret;
                }
            }

            private static T GetNearEntity<T>(Vector3 position, float radius, int layerMask) where T : BaseCombatEntity
            {
                List<T> list = new List<T>();
                Vis.Entities(position, radius, list, layerMask);
                return list.Count == 0 ? null : list.Min(s => Vector3.Distance(position, s.transform.position));
            }

            private static List<T> GetEntities<T>(Vector3 position, float radius, int layerMask) where T : BaseCombatEntity
            {
                List<T> list = new List<T>();
                Vis.Entities(position, radius, list, layerMask);
                return list.Count == 0 ? null : list;
            }

            private static Vector3 GetCenterHomePos(HashSet<BuildingBlock> blocks)
            {
                float Xmin = blocks.Min(x => x.transform.position.x).transform.position.x;
                float Xmax = blocks.Max(x => x.transform.position.x).transform.position.x;
                float Ymin = blocks.Min(x => x.transform.position.y).transform.position.y;
                float Ymax = blocks.Max(x => x.transform.position.y).transform.position.y;
                float Zmin = blocks.Min(x => x.transform.position.z).transform.position.z;
                float Zmax = blocks.Max(x => x.transform.position.z).transform.position.z;
                return new Vector3((Xmin + Xmax) / 2, (Ymin + Ymax) / 2, (Zmin + Zmax) / 2);
            }

            private static HashSet<BuildingPrivlidge> GetCupboards(BuildingPrivlidge cupboard)
            {
                HashSet<ulong> ids = cupboard.authorizedPlayers.Select(x => x.userid).ToHashSet();
                return GetEntities<BuildingPrivlidge>(cupboard.transform.position, 100f, 1 << 8).Where(x => x.authorizedPlayers.Any(y => ids.Contains(y.userid))).ToHashSet();
            }

            private static HashSet<BuildingBlock> GetBlocks(HashSet<BuildingPrivlidge> cupboards) => cupboards.SelectMany(x => x.GetBuilding().buildingBlocks);

            private static HashSet<BoxStorage> GetBoxes(HashSet<BuildingPrivlidge> cupboards) => cupboards.SelectMany(x => x.GetBuilding().decayEntities.OfType<BoxStorage>());

            internal BaseCombatEntity GetRaidTarget()
            {
                BaseCombatEntity result = GetRaidMainTarget();
                if (result == null) return null;
                BaseCombatEntity targetPath = GetTargetPath(result);
                return targetPath != null ? targetPath : result;
            }

            private BaseCombatEntity GetRaidMainTarget()
            {
                if (Turret.IsExists())
                {
                    BuildingBlock block = GetNearEntity<BuildingBlock>(Turret.transform.position, 0.1f, 1 << 21);
                    return block.IsExists() ? block : Turret;
                }
                if (FirstTarget.IsExists()) return FirstTarget;
                if (MainCupboard.IsExists()) return MainCupboard;
                if (Boxes.Count > 0)
                {
                    foreach (BoxStorage storage in Boxes.Where(x => !x.IsExists())) Boxes.Remove(storage);
                    if (Boxes.Count > 0) return Boxes.Min(x => Vector3.Distance(Transform.position, x.transform.position));
                }
                if (Cupboards.Count > 0)
                {
                    foreach (BuildingPrivlidge cupboard in Cupboards.Where(x => !x.IsExists())) Cupboards.Remove(cupboard);
                    if (Cupboards.Count > 0) return Cupboards.Min(x => Vector3.Distance(Transform.position, x.transform.position));
                }
                if (Foundations.Count > 0)
                {
                    foreach (BuildingBlock block in Foundations.Where(x => !x.IsExists())) Foundations.Remove(block);
                    if (Foundations.Count > 0) return Foundations.Min(x => Vector3.Distance(Transform.position, x.transform.position));
                }
                return null;
            }

            private BaseCombatEntity GetTargetPath(BaseCombatEntity target)
            {
                NavMeshHit navMeshHit;
                int attempts = 0;
                while (attempts < 20)
                {
                    if (target == null) return null;

                    attempts++;

                    float targetHeight = TerrainMeta.HeightMap.GetHeight(target.transform.position);
                    if (target.transform.position.y - targetHeight > 7.5f)
                    {
                        List<BuildingBlock> blocks = GetEntities<BuildingBlock>(new Vector3(target.transform.position.x, targetHeight, target.transform.position.z), 15f, 1 << 21);
                        if (blocks != null) target = blocks.Min(s => Vector3.Distance(s.transform.position, Transform.position));
                        else return null;
                    }

                    if (NavMesh.SamplePosition(target.transform.position, out navMeshHit, 15f, NavAgent.areaMask))
                    {
                        NavMeshPath path = new NavMeshPath();
                        if (NavMesh.CalculatePath(Transform.position, navMeshHit.position, NavAgent.areaMask, path)) return path.status == NavMeshPathStatus.PathComplete ? target : GetNearEntity<BaseCombatEntity>(path.corners.Last(), 5f, 1 << 8 | 1 << 21);
                    }

                    float x1 = UnityEngine.Random.Range(target.transform.position.x - 15f - 5f, target.transform.position.x - 15f);
                    float x2 = UnityEngine.Random.Range(target.transform.position.x + 15f, target.transform.position.x + 15f + 5f);
                    float z1 = UnityEngine.Random.Range(target.transform.position.z - 15f - 5f, target.transform.position.z - 15f);
                    float z2 = UnityEngine.Random.Range(target.transform.position.z + 15f, target.transform.position.z + 15f + 5f);

                    Vector3 vector1 = new Vector3(x1, 500f, z1);
                    vector1.y = TerrainMeta.HeightMap.GetHeight(vector1);
                    Vector3 vector2 = new Vector3(x2, 500f, z1);
                    vector2.y = TerrainMeta.HeightMap.GetHeight(vector2);
                    Vector3 vector3 = new Vector3(x1, 500f, z2);
                    vector3.y = TerrainMeta.HeightMap.GetHeight(vector3);
                    Vector3 vector4 = new Vector3(x2, 500f, z2);
                    vector4.y = TerrainMeta.HeightMap.GetHeight(vector4);
                    HashSet<Vector3> list = new HashSet<Vector3> { vector1, vector2, vector3, vector4 };

                    target = GetNearEntity<BaseCombatEntity>(list.Min(x => Vector3.Distance(Transform.position, x)), 5f, 1 << 8 | 1 << 21);
                }
                return null;
            }

            internal bool StartExplosion(BaseCombatEntity target)
            {
                if (isMounted && !Config.CanUseWeaponMounted) return false;
                if (target == null) return false;
                if (CanThrownC4(target))
                {
                    _fireC4Coroutine = ServerMgr.Instance.StartCoroutine(ThrownC4(target));
                    return true;
                }
                if (CanRaidRocketLauncher(target))
                {
                    ThrownSmoke();
                    _fireRocketLauncherCoroutine = ServerMgr.Instance.StartCoroutine(ProcessFireRocketLauncher(target));
                    return true;
                }
                return false;
            }

            private bool CanRaidRocketLauncher(BaseCombatEntity target) => !IsReloadRocketLauncher && !IsFireRocketLauncher && inventory.containerBelt.itemList.Any(x => x.info.shortname == "rocket.launcher") && Vector3.Distance(Transform.position, target.transform.position) < 15f;

            private IEnumerator ProcessFireRocketLauncher(BaseCombatEntity target)
            {
                IsFireRocketLauncher = true;
                EquipRocketLauncher();
                SetDucked(true);
                Brain.Navigator.Stop();
                Brain.Navigator.SetFacingDirectionEntity(target);
                yield return CoroutineEx.waitForSeconds(1.5f);
                if (target.IsExists())
                {
                    if (target.ShortPrefabName.Contains("foundation"))
                    {
                        Brain.Navigator.ClearFacingDirectionOverride();
                        SetAimDirection((target.transform.position - new Vector3(0f, 1.5f, 0f) - Transform.position).normalized);
                    }
                    FireRocketLauncher();
                    IsReloadRocketLauncher = true;
                    Invoke(FinishReloadRocketLauncher, 6f);
                }
                IsFireRocketLauncher = false;
                EquipWeapon();
                Brain.Navigator.ClearFacingDirectionOverride();
                SetDucked(false);
            }

            private void EquipRocketLauncher()
            {
                Item item = inventory.containerBelt.itemList.FirstOrDefault(x => x.info.shortname == "rocket.launcher");
                UpdateActiveItem(item.uid);
                AttackEntity weapon = item.GetHeldEntity() as AttackEntity;
                if (weapon != null) CurrentWeapon = weapon;
            }

            private void FireRocketLauncher()
            {
                RaycastHit raycastHit;
                SignalBroadcast(Signal.Attack, string.Empty);
                Vector3 vector3 = eyes.position;
                Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(2.25f, eyes.BodyForward());
                float single = 1f;
                if (Physics.Raycast(vector3, modifiedAimConeDirection, out raycastHit, single, 1236478737)) single = raycastHit.distance - 0.1f;
                TimedExplosive rocket = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_basic.prefab", vector3 + modifiedAimConeDirection * single) as TimedExplosive;
                rocket.creatorEntity = this;
                ServerProjectile serverProjectile = rocket.GetComponent<ServerProjectile>();
                serverProjectile.InitializeVelocity(GetInheritedProjectileVelocity() + modifiedAimConeDirection * serverProjectile.speed);
                rocket.Spawn();
            }

            private void FinishReloadRocketLauncher() => IsReloadRocketLauncher = false;

            private bool CanThrownC4(BaseCombatEntity target) => !IsReloadC4 && !IsFireC4 && inventory.containerBelt.itemList.Any(x => x.info.shortname == "explosive.timed") && Vector3.Distance(Transform.position, target.transform.position) < 5f;

            private IEnumerator ThrownC4(BaseCombatEntity target)
            {
                Item item = inventory.containerBelt.itemList.FirstOrDefault(x => x.info.shortname == "explosive.timed");
                IsFireC4 = true;
                Brain.Navigator.Stop();
                Brain.Navigator.SetFacingDirectionEntity(target);
                yield return CoroutineEx.waitForSeconds(1.5f);
                if (target.IsExists())
                {
                    (item.GetHeldEntity() as ThrownWeapon).ServerThrow(target.transform.position);
                    IsReloadC4 = true;
                    Invoke(FinishReloadC4, 15f);
                }
                IsFireC4 = false;
                Brain.Navigator.ClearFacingDirectionOverride();
            }

            private void FinishReloadC4() => IsReloadC4 = false;

            private static bool IsTeam(BasePlayer player, ulong targetId)
            {
                if (player == null || targetId == 0) return false;

                if (player.userID == targetId) return true;

                if (player.currentTeam != 0)
                {
                    RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                    if (playerTeam == null) return false;
                    if (playerTeam.members.Contains(targetId)) return true;
                }

                if (_ins.plugins.Exists("Friends") && (bool)_ins.Friends.Call("AreFriends", player.userID, targetId)) return true;

                if (_ins.plugins.Exists("Clans") && (bool)_ins.Clans.Call("IsMemberOrAlly", player.UserIDString, targetId.ToString())) return true;

                return false;
            }
            #endregion Raid

            #region Guard
            private Vector3 _beforeGuardHomePosition = Vector3.zero;
            private BaseEntity _guardTarget = null;

            internal void AddTargetGuard(BaseEntity target)
            {
                _beforeGuardHomePosition = HomePosition;
                _guardTarget = target;
            }
            #endregion Guard

            #region Multiple Grenade Launcher
            internal bool IsReloadGrenadeLauncher = false;
            private int _countAmmoInGrenadeLauncher = 6;

            internal void FireGrenadeLauncher()
            {
                RaycastHit raycastHit;
                SignalBroadcast(Signal.Attack, string.Empty);
                Vector3 vector3 = eyes.position;
                Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(0.675f, eyes.BodyForward());
                float single = 1f;
                if (Physics.Raycast(vector3, modifiedAimConeDirection, out raycastHit, single, 1236478737)) single = raycastHit.distance - 0.1f;
                TimedExplosive grenade = GameManager.server.CreateEntity("assets/prefabs/ammo/40mmgrenade/40mm_grenade_he.prefab", vector3 + modifiedAimConeDirection * single) as TimedExplosive;
                grenade.creatorEntity = this;
                ServerProjectile serverProjectile = grenade.GetComponent<ServerProjectile>();
                serverProjectile.InitializeVelocity(GetInheritedProjectileVelocity() + modifiedAimConeDirection * serverProjectile.speed);
                grenade.Spawn();
                _countAmmoInGrenadeLauncher--;
                if (_countAmmoInGrenadeLauncher == 0)
                {
                    IsReloadGrenadeLauncher = true;
                    Invoke(FinishReloadGrenadeLauncher, 8f);
                }
            }

            private void FinishReloadGrenadeLauncher()
            {
                _countAmmoInGrenadeLauncher = 6;
                IsReloadGrenadeLauncher = false;
            }
            #endregion Multiple Grenade Launcher

            #region Flame Thrower
            internal bool IsReloadFlameThrower = false;

            internal void FireFlameThrower()
            {
                FlameThrower flameThrower = CurrentWeapon as FlameThrower;
                if (flameThrower == null || flameThrower.IsFlameOn()) return;
                if (flameThrower.ammo <= 0)
                {
                    IsReloadFlameThrower = true;
                    Invoke(FinishReloadFlameThrower, 4f);
                    return;
                }
                flameThrower.SetFlameState(true);
                Invoke(flameThrower.StopFlameState, 0.25f);
            }

            private void FinishReloadFlameThrower()
            {
                FlameThrower flameThrower = CurrentWeapon as FlameThrower;
                if (flameThrower == null) return;
                flameThrower.TopUpAmmo();
                IsReloadFlameThrower = false;
            }
            #endregion Flame Thrower
        }

        public class CustomScientistBrain : BaseAIBrain<global::HumanNPC>
        {
            private CustomScientistNpc _npc = null;

            public override void InitializeAI()
            {
                SenseTypes = EntityType.Player;
                _npc.Config.Sensory.ApplySettingsToBrain(this);
                base.InitializeAI();
                UseAIDesign = false;
                ThinkMode = AIThinkMode.Interval;
                thinkRate = 0.25f;
                PathFinder = new HumanPathFinder();
                ((HumanPathFinder)PathFinder).Init(_npc);
                Navigator.DefaultArea = "Walkable";
                Navigator.topologyPreference = (TerrainTopology.Enum)1673010749;
                Navigator.Speed = _npc.Config.Speed;
            }

            public override void AddStates()
            {
                _npc = GetEntity() as CustomScientistNpc;
                states = new Dictionary<AIState, BasicAIState>();
                if (_npc.Config.Stationary)
                {
                    AddState(new IdleState(_npc));
                    AddState(new CombatStationaryState(_npc));
                }
                else
                {
                    AddState(new RoamState(_npc));
                    AddState(new ChaseState(_npc));
                    AddState(new CombatState(_npc));
                    AddState(new MountedState(_npc));
                    if (_npc.Config.BeltItems.Any(x => x.ShortName == "rocket.launcher" || x.ShortName == "explosive.timed")) AddState(new RaidState(_npc));
                }
            }

            public override void Think(float delta)
            {
                if (_npc == null) return;
                Senses.Update();
                base.Think(delta);
                if (sleeping) return;
                if (!_npc.IsRunAwayWater) _npc.CurrentTarget = _npc.GetBestTarget();
            }

            public override void OnDestroy()
            {
                if (Rust.Application.isQuitting) return;
                BaseEntity.Query.Server.RemoveBrain(_npc);
                LeaveGroup();
            }

            public class RoamState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;

                public RoamState(CustomScientistNpc npc) : base(AIState.Roam) { this._npc = npc; }

                public override float GetWeight() => 25f;

                public override void StateEnter()
                {
                    base.StateEnter();
                    _npc.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
                }

                public override void StateLeave()
                {
                    base.StateLeave();
                    _npc.ThrownSmoke();
                    _npc.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, false);
                }

                public override StateStatus StateThink(float delta)
                {
                    base.StateThink(delta);
                    if (_npc.DistanceFromBase > _npc.Config.RoamRange) brain.Navigator.SetDestination(_npc.HomePosition, BaseNavigator.NavigationSpeed.Fast);
                    else if (!brain.Navigator.Moving) brain.Navigator.SetDestination(GetRoamPosition(), BaseNavigator.NavigationSpeed.Slowest);
                    return StateStatus.Running;
                }

                private Vector3 GetRoamPosition()
                {
                    Vector2 random = UnityEngine.Random.insideUnitCircle * (_npc.Config.RoamRange - 5f);
                    Vector3 result = _npc.HomePosition + new Vector3(random.x, 0f, random.y);
                    NavMeshHit navMeshHit;
                    if (NavMesh.SamplePosition(result, out navMeshHit, 5f, _npc.NavAgent.areaMask))
                    {
                        NavMeshPath path = new NavMeshPath();
                        if (NavMesh.CalculatePath(_npc.Transform.position, navMeshHit.position, _npc.NavAgent.areaMask, path)) result = path.status == NavMeshPathStatus.PathComplete ? navMeshHit.position : path.corners.Last();
                        else result = _npc.HomePosition;
                    }
                    else result = _npc.HomePosition;
                    return result;
                }
            }

            public class ChaseState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;

                public ChaseState(CustomScientistNpc npc) : base(AIState.Chase) { this._npc = npc; }

                public override float GetWeight()
                {
                    if (_npc.CurrentTarget == null) return 0f;
                    if (_npc.DistanceFromBase > _npc.Config.ChaseRange) return 0f;
                    if (_npc.IsRunAwayWater) return 0f;
                    if (_npc.isMounted) return 0f;
                    if (_npc.IsFireC4 || _npc.IsFireRocketLauncher) return 0f;
                    if (_npc.CurrentRaidTarget != null) return 0f;
                    if (!_npc.CanTargetBasePlayer(_npc.CurrentTarget)) return 0f;
                    return 50f;
                }

                public override StateStatus StateThink(float delta)
                {
                    base.StateThink(delta);
                    if (_npc.CurrentTarget == null) return StateStatus.Error;
                    if (_npc.CurrentWeapon is BaseProjectile) brain.Navigator.SetDestination(brain.PathFinder.GetRandomPositionAround(_npc.CurrentTarget.transform.position, 1f, 2f), _npc.DistanceToTarget >= 10f ? BaseNavigator.NavigationSpeed.Fast : BaseNavigator.NavigationSpeed.Normal);
                    else brain.Navigator.SetDestination(GetChasePosition(), BaseNavigator.NavigationSpeed.Fast);
                    return StateStatus.Running;
                }

                private Vector3 GetChasePosition()
                {
                    Vector2 random = UnityEngine.Random.insideUnitCircle * 2f;
                    Vector3 result = _npc.CurrentTarget.transform.position + new Vector3(random.x, 0f, random.y);
                    NavMeshHit navMeshHit;
                    if (NavMesh.SamplePosition(result, out navMeshHit, 2f, _npc.NavAgent.areaMask))
                    {
                        NavMeshPath path = new NavMeshPath();
                        if (NavMesh.CalculatePath(_npc.Transform.position, navMeshHit.position, _npc.NavAgent.areaMask, path)) result = path.status == NavMeshPathStatus.PathComplete ? navMeshHit.position : path.corners.Last();
                        else result = _npc.CurrentTarget.transform.position;
                    }
                    else result = _npc.CurrentTarget.transform.position;
                    return result;
                }
            }

            public class CombatState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;
                private float _nextStrafeTime;

                public CombatState(CustomScientistNpc npc) : base(AIState.Combat) { this._npc = npc; }

                public override float GetWeight()
                {
                    if (_npc.CurrentTarget == null || _npc.CurrentWeapon == null) return 0f;
                    if (_npc.DistanceFromBase > _npc.Config.ChaseRange) return 0f;
                    if (_npc.IsRunAwayWater) return 0f;
                    if (_npc.isMounted) return 0f;
                    if (_npc.IsFireC4 || _npc.IsFireRocketLauncher) return 0f;
                    if (!_npc.CanTargetBasePlayer(_npc.CurrentTarget)) return 0f;
                    if (_npc.DistanceToTarget > _npc.EngagementRange()) return 0f;
                    if (!_npc.CanSeeTarget(_npc.CurrentTarget) || (_npc.CanSeeTarget(_npc.CurrentTarget) && _npc.IsBehindBarricade())) return 0f;
                    if (_npc.CurrentWeapon.ShortPrefabName == "mgl.entity" && _npc.IsReloadGrenadeLauncher) return 0f;
                    if (_npc.CurrentWeapon is FlameThrower && _npc.IsReloadFlameThrower) return 0f;
                    if (_npc.CurrentWeapon.ShortPrefabName == "rocket_launcher.entity") return 0f;
                    return 75f;
                }

                public override void StateEnter()
                {
                    base.StateEnter();
                    brain.mainInterestPoint = _npc.Transform.position;
                    brain.Navigator.SetCurrentSpeed(BaseNavigator.NavigationSpeed.Normal);
                    brain.Navigator.SetFacingDirectionEntity(_npc.CurrentTarget);
                }

                public override void StateLeave()
                {
                    base.StateLeave();
                    _npc.SetDucked(false);
                    brain.Navigator.ClearFacingDirectionOverride();
                }

                public override StateStatus StateThink(float delta)
                {
                    base.StateThink(delta);
                    if (_npc.CurrentTarget == null) return StateStatus.Error;
                    brain.Navigator.SetFacingDirectionEntity(_npc.CurrentTarget);
                    if (_npc.CurrentWeapon is BaseProjectile)
                    {
                        if (Time.time > _nextStrafeTime)
                        {
                            if (UnityEngine.Random.Range(0, 3) == 1)
                            {
                                float deltaTime = _npc.CurrentWeapon is BaseLauncher ? UnityEngine.Random.Range(0.5f, 1f) : UnityEngine.Random.Range(1f, 2f);
                                _nextStrafeTime = Time.time + deltaTime;
                                _npc.SetDucked(true);
                                brain.Navigator.Stop();
                            }
                            else
                            {
                                float deltaTime = _npc.CurrentWeapon is BaseLauncher ? UnityEngine.Random.Range(1f, 1.5f) : UnityEngine.Random.Range(2f, 3f);
                                _nextStrafeTime = Time.time + deltaTime;
                                _npc.SetDucked(false);
                                brain.Navigator.SetDestination(brain.PathFinder.GetRandomPositionAround(brain.mainInterestPoint, 1f, 2f), BaseNavigator.NavigationSpeed.Normal);
                            }
                            if (_npc.CurrentWeapon is BaseLauncher) _npc.FireGrenadeLauncher();
                            else _npc.ShotTest(_npc.DistanceToTarget);
                        }
                    }
                    else if (_npc.CurrentWeapon is FlameThrower)
                    {
                        if (_npc.DistanceToTarget < _npc.CurrentWeapon.effectiveRange) _npc.FireFlameThrower();
                        else brain.Navigator.SetDestination(_npc.CurrentTarget.transform.position, BaseNavigator.NavigationSpeed.Fast);
                    }
                    else if (_npc.CurrentWeapon is BaseMelee)
                    {
                        _npc.nextTriggerTime = Time.time + 30f;
                        if (_npc.DistanceToTarget < _npc.CurrentWeapon.effectiveRange * 2f) DoMeleeAttack();
                        else brain.Navigator.SetDestination(_npc.CurrentTarget.transform.position, BaseNavigator.NavigationSpeed.Fast);
                    }
                    return StateStatus.Running;
                }

                private void DoMeleeAttack()
                {
                    if (_npc.CurrentWeapon is BaseMelee)
                    {
                        BaseMelee baseMelee = _npc.CurrentWeapon as BaseMelee;
                        if (!baseMelee.HasAttackCooldown())
                        {
                            baseMelee.StartAttackCooldown(baseMelee.repeatDelay);
                            _npc.SignalBroadcast(BaseEntity.Signal.Attack, string.Empty);
                            if (baseMelee.swingEffect.isValid) Effect.server.Run(baseMelee.swingEffect.resourcePath, baseMelee.transform.position, Vector3.forward, _npc.net.connection);
                            DoMeleeDamage(baseMelee);
                        }
                    }
                }

                private void DoMeleeDamage(BaseMelee baseMelee)
                {
                    Vector3 position = _npc.eyes.position;
                    Vector3 forward = _npc.eyes.BodyForward();
                    for (int i = 0; i < 2; i++)
                    {
                        List<RaycastHit> list = Pool.GetList<RaycastHit>();
                        GamePhysics.TraceAll(new Ray(position - (forward * (i == 0 ? 0f : 0.2f)), forward), (i == 0 ? 0f : baseMelee.attackRadius), list, baseMelee.effectiveRange + 0.2f, 1219701521);
                        bool hasHit = false;
                        foreach (RaycastHit raycastHit in list)
                        {
                            BaseEntity hitEntity = raycastHit.GetEntity();
                            if (hitEntity != null && hitEntity != _npc && !hitEntity.EqualNetID(_npc) && !(hitEntity is ScientistNPC))
                            {
                                float damageAmount = baseMelee.damageTypes.Sum(x => x.amount);
                                hitEntity.OnAttacked(new HitInfo(_npc, hitEntity, DamageType.Slash, damageAmount * baseMelee.npcDamageScale * _npc.Config.DamageScale));
                                HitInfo hitInfo = Pool.Get<HitInfo>();
                                hitInfo.HitEntity = hitEntity;
                                hitInfo.HitPositionWorld = raycastHit.point;
                                hitInfo.HitNormalWorld = -forward;
                                if (hitEntity is BaseNpc || hitEntity is BasePlayer) hitInfo.HitMaterial = StringPool.Get("Flesh");
                                else hitInfo.HitMaterial = StringPool.Get((raycastHit.GetCollider().sharedMaterial != null ? raycastHit.GetCollider().sharedMaterial.GetName() : "generic"));
                                Effect.server.ImpactEffect(hitInfo);
                                Pool.Free(ref hitInfo);
                                hasHit = true;
                                if (hitEntity.ShouldBlockProjectiles()) break;
                            }
                        }
                        Pool.FreeList(ref list);
                        if (hasHit) break;
                    }
                }
            }

            public class MountedState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;

                public MountedState(CustomScientistNpc npc) : base(AIState.Mounted) { this._npc = npc; }

                public override float GetWeight() => _npc.isMounted ? 100f : 0f;

                public override void StateEnter()
                {
                    base.StateEnter();
                    if (!_npc.Config.CanUseWeaponMounted) _npc.HolsterWeapon();
                    DisableNavAgent();
                }

                public override void StateLeave()
                {
                    base.StateLeave();
                    EnableNavAgent();
                    _npc.EquipWeapon();
                }

                private void EnableNavAgent()
                {
                    Vector3 position = _npc.Transform.position;
                    _npc.NavAgent.Warp(position);
                    _npc.Transform.position = position;
                    _npc.HomePosition = position;
                    _npc.NavAgent.enabled = true;
                    _npc.NavAgent.isStopped = false;
                    brain.Navigator.SetDestination(position, BaseNavigator.NavigationSpeed.Fast);
                }

                private void DisableNavAgent()
                {
                    if (!_npc.NavAgent.enabled) return;
                    _npc.NavAgent.destination = _npc.Transform.position;
                    _npc.NavAgent.isStopped = true;
                    _npc.NavAgent.enabled = false;
                }
            }

            public class IdleState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;

                public IdleState(CustomScientistNpc npc) : base(AIState.Idle) { this._npc = npc; }

                public override float GetWeight() => 50f;

                public override void StateEnter()
                {
                    base.StateEnter();
                    _npc.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
                }

                public override void StateLeave()
                {
                    base.StateLeave();
                    _npc.ThrownSmoke();
                    _npc.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, false);
                }
            }

            public class CombatStationaryState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;
                private float _nextStrafeTime;

                public CombatStationaryState(CustomScientistNpc npc) : base(AIState.CombatStationary) { this._npc = npc; }

                public override float GetWeight()
                {
                    if (_npc.CurrentTarget == null || _npc.CurrentWeapon == null) return 0f;
                    if (!(_npc.CurrentTarget is BasePlayer) || !_npc.CanTargetBasePlayer(_npc.CurrentTarget as BasePlayer)) return 0f;
                    if (!_npc.CanSeeTarget(_npc.CurrentTarget) || (_npc.CanSeeTarget(_npc.CurrentTarget) && _npc.IsBehindBarricade())) return 0f;
                    if (_npc.DistanceToTarget > _npc.EngagementRange()) return 0f;
                    return 100f;
                }

                public override void StateEnter()
                {
                    base.StateEnter();
                    brain.Navigator.SetFacingDirectionEntity(_npc.CurrentTarget);
                }

                public override void StateLeave()
                {
                    base.StateLeave();
                    _npc.SetDucked(false);
                    brain.Navigator.ClearFacingDirectionOverride();
                }

                public override StateStatus StateThink(float delta)
                {
                    base.StateThink(delta);
                    if (_npc.CurrentTarget == null) return StateStatus.Error;
                    brain.Navigator.SetFacingDirectionEntity(_npc.CurrentTarget);
                    if (_npc.CurrentWeapon is BaseProjectile)
                    {
                        if (Time.time > _nextStrafeTime)
                        {
                            if (UnityEngine.Random.Range(0, 3) == 1)
                            {
                                _nextStrafeTime = Time.time + UnityEngine.Random.Range(1f, 2f);
                                _npc.SetDucked(true);
                            }
                            else
                            {
                                _nextStrafeTime = Time.time + UnityEngine.Random.Range(2f, 3f);
                                _npc.SetDucked(false);
                            }
                            _npc.ShotTest(_npc.DistanceToTarget);
                        }
                    }
                    return StateStatus.Running;
                }
            }

            public class RaidState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;

                public RaidState(CustomScientistNpc npc) : base(AIState.Cooldown) { this._npc = npc; }

                public override float GetWeight()
                {
                    if (_npc.IsFireC4 || _npc.IsFireRocketLauncher) return 125f;
                    if (_npc.isMounted && !_npc.Config.CanUseWeaponMounted) return 0f;
                    if (_npc.IsRunAwayWater) return 0f;
                    if (_npc.CanTargetBasePlayer(_npc.CurrentTarget) && _npc.CanSeeTarget(_npc.CurrentTarget) && _npc.DistanceToTarget <= _npc.EngagementRange()) return 0f;
                    if (_npc.GetRaidTarget() == null) return 0f;
                    if (_npc.inventory.containerBelt.itemList.Any(x => x.info.shortname == "rocket.launcher") || _npc.inventory.containerBelt.itemList.Any(x => x.info.shortname == "explosive.timed")) return 125f;
                    return 0f;
                }

                public override void StateEnter()
                {
                    base.StateEnter();
                    _npc.CurrentRaidTarget = _npc.GetRaidTarget();
                }

                public override void StateLeave()
                {
                    base.StateLeave();
                    _npc.SetDucked(false);
                    brain.Navigator.ClearFacingDirectionOverride();
                }

                public override StateStatus StateThink(float delta)
                {
                    base.StateThink(delta);
                    if (_npc.IsFireC4 || _npc.IsFireRocketLauncher) return StateStatus.Running;
                    if (!_npc.CurrentRaidTarget.IsExists())
                    {
                        _npc.CurrentRaidTarget = _npc.GetRaidTarget();
                        if (!_npc.CurrentRaidTarget.IsExists()) return StateStatus.Error;
                    }
                    if (!_npc.StartExplosion(_npc.CurrentRaidTarget) && Vector3.Distance(_npc.Transform.position, _npc.CurrentRaidTarget.transform.position) > 5f)
                    {
                        _npc.SetDucked(false);
                        brain.Navigator.SetDestination(GetRaidPosition(), _npc.CurrentRaidTarget is AutoTurret || _npc.CurrentRaidTarget is GunTrap || _npc.CurrentRaidTarget is FlameTurret || Vector3.Distance(_npc.Transform.position, _npc.CurrentRaidTarget.transform.position) > 15f ? BaseNavigator.NavigationSpeed.Fast : Vector3.Distance(_npc.Transform.position, _npc.CurrentRaidTarget.transform.position) > 5f ? BaseNavigator.NavigationSpeed.Normal : BaseNavigator.NavigationSpeed.Slow);
                    }
                    return StateStatus.Running;
                }

                private Vector3 GetRaidPosition()
                {
                    if (_npc.CurrentRaidTarget is BuildingPrivlidge || _npc.CurrentRaidTarget is BuildingBlock || _npc.CurrentRaidTarget is BoxStorage) return brain.PathFinder.GetRandomPositionAround(_npc.CurrentRaidTarget.transform.position, 1f, 2f);
                    else
                    {
                        NavMeshHit navMeshHit;
                        if (NavMesh.SamplePosition(_npc.CurrentRaidTarget.transform.position, out navMeshHit, 5f, _npc.NavAgent.areaMask))
                        {
                            NavMeshPath path = new NavMeshPath();
                            if (NavMesh.CalculatePath(_npc.Transform.position, navMeshHit.position, _npc.NavAgent.areaMask, path)) return path.status == NavMeshPathStatus.PathComplete ? navMeshHit.position : path.corners.Last();
                        }
                    }
                    return _npc.CurrentRaidTarget.transform.position;
                }
            }
        }
        #endregion Controller

        #region Oxide Hooks
        [PluginReference] private readonly Plugin Kits, Friends, Clans;

        private static NpcSpawn _ins;

        private readonly Dictionary<uint, CustomScientistNpc> _scientists = new Dictionary<uint, CustomScientistNpc>();

        private void Init() => _ins = this;

        private void OnServerInitialized() => GenerateSpawnpoints();

        private void Unload()
        {
            foreach (CustomScientistNpc npc in _scientists.Values.Where(x => x.IsExists())) npc.Kill();
            _ins = null;
        }

        private void OnEntityKill(CustomScientistNpc npc) { if (npc != null && _scientists.ContainsKey(npc.net.ID)) _scientists.Remove(npc.net.ID); }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!entity.IsExists() || info == null) return null;
            BaseEntity attacker = info.Initiator;
            if (entity is CustomScientistNpc)
            {
                CustomScientistNpc victimNpc;
                if (_scientists.TryGetValue(entity.net.ID, out victimNpc))
                {
                    if (!attacker.IsExists()) return true;
                    if (victimNpc.CurrentTarget == null && victimNpc.CanTargetBasePlayer(attacker as BasePlayer)) victimNpc.CurrentTarget = attacker as BasePlayer;
                    if (attacker is AutoTurret || attacker is GunTrap || attacker is FlameTurret)
                    {
                        victimNpc.AddTurret(attacker as BaseCombatEntity);
                        info.damageTypes.ScaleAll(0.1f);
                        return null;
                    }
                    if (attacker is BasePlayer && (attacker as BasePlayer).userID.IsSteamId()) return null;
                    else return true;
                }
            }
            if (attacker.IsExists() && attacker is CustomScientistNpc && _scientists.ContainsKey(attacker.net.ID))
            {
                if (entity is BasePlayer)
                {
                    if ((entity as BasePlayer).userID.IsSteamId()) return null;
                    else return true;
                }
                else if (entity.OwnerID.IsSteamId()) return null;
                else return true;
            }
            return null;
        }

        private object OnEntityEnter(TriggerBase trigger, CustomScientistNpc npc)
        {
            if (npc == null || trigger == null) return null;
            if (_scientists.ContainsKey(npc.net.ID))
            {
                BaseEntity attacker = trigger.GetComponentInParent<BaseEntity>();
                if (attacker != null && attacker.OwnerID.IsSteamId() && (attacker is AutoTurret || attacker is GunTrap || attacker is FlameTurret)) return null;
                else return false;
            }
            else return null;
        }

        private object OnNpcTarget(CustomScientistNpc npc, BaseEntity entity)
        {
            if (npc != null && _scientists.ContainsKey(npc.net.ID))
            {
                if (entity is BasePlayer && (entity as BasePlayer).userID.IsSteamId()) return null;
                else return true;
            }
            else return null;
        }

        private object OnNpcTarget(BaseEntity npc, CustomScientistNpc entity)
        {
            if (entity != null && _scientists.ContainsKey(entity.net.ID)) return true;
            else return null;
        }

        private object CanBradleyApcTarget(BradleyAPC apc, CustomScientistNpc entity)
        {
            if (apc != null && entity != null && _scientists.ContainsKey(entity.net.ID)) return false;
            else return null;
        }
        #endregion Oxide Hooks

        #region Other plugins hooks
        private object OnNpcKits(CustomScientistNpc npc)
        {
            if (npc != null && _scientists.ContainsKey(npc.net.ID)) return true;
            else return null;
        }

        private object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!entity.IsExists() || info == null) return null;
            BaseEntity attacker = info.Initiator;
            if (entity is CustomScientistNpc && _scientists.ContainsKey(entity.net.ID))
            {
                if (!attacker.IsExists()) return false;
                return ((attacker is AutoTurret || attacker is GunTrap || attacker is FlameTurret) && attacker.OwnerID.IsSteamId()) || (attacker is BasePlayer && (attacker as BasePlayer).userID.IsSteamId());
            }
            if (attacker.IsExists() && attacker is CustomScientistNpc && _scientists.ContainsKey(attacker.net.ID)) return entity.OwnerID.IsSteamId() || (entity is BasePlayer && (entity as BasePlayer).userID.IsSteamId());
            return null;
        }

        private object CanEntityBeTargeted(BasePlayer victim, BaseEntity attacker)
        {
            if (victim == null || attacker == null) return null;
            if (victim is CustomScientistNpc && _scientists.ContainsKey(victim.net.ID)) return (attacker is AutoTurret || attacker is GunTrap || attacker is FlameTurret) && attacker.OwnerID.IsSteamId();
            if (attacker is CustomScientistNpc && _scientists.ContainsKey(attacker.net.ID)) return victim.userID.IsSteamId();
            return null;
        }
        #endregion Other plugins hooks

        #region Find Random Points
        private readonly Dictionary<TerrainBiome.Enum, List<Vector3>> _points = new Dictionary<TerrainBiome.Enum, List<Vector3>>();
        private const int VIS_RAYCAST_LAYERS = 1 << 8 | 1 << 17 | 1 << 21;
        private const int POINT_RAYCAST_LAYERS = 1 << 4 | 1 << 8 | 1 << 10 | 1 << 15 | 1 << 16 | 1 << 21 | 1 << 23 | 1 << 27 | 1 << 28 | 1 << 29;
        private const int BLOCKED_TOPOLOGY = (int)(TerrainTopology.Enum.Cliff | TerrainTopology.Enum.Cliffside | TerrainTopology.Enum.Lake | TerrainTopology.Enum.Ocean | TerrainTopology.Enum.Monument | TerrainTopology.Enum.Offshore | TerrainTopology.Enum.River | TerrainTopology.Enum.Swamp);

        private void GenerateSpawnpoints()
        {
            for (int i = 0; i < 3000; i++)
            {
                Vector2 random = World.Size * 0.475f * UnityEngine.Random.insideUnitCircle;
                Vector3 position = new Vector3(random.x, 500f, random.y);
                if ((TerrainMeta.TopologyMap.GetTopology(position) & BLOCKED_TOPOLOGY) != 0) continue;
                float heightAtPoint;
                if (!IsPointOnTerrain(position, out heightAtPoint)) continue;
                position.y = heightAtPoint;
                TerrainBiome.Enum majorityBiome = (TerrainBiome.Enum)TerrainMeta.BiomeMap.GetBiomeMaxType(position);
                List<Vector3> list;
                if (!_points.TryGetValue(majorityBiome, out list)) _points[majorityBiome] = list = new List<Vector3>();
                list.Add(position);
            }
        }

        private object GetSpawnPoint(string biomeName)
        {
            TerrainBiome.Enum biome = (TerrainBiome.Enum)Enum.Parse(typeof(TerrainBiome.Enum), biomeName, true);
            if (!_points.ContainsKey(biome)) return null;
            List<Vector3> spawnpoints = _points[biome];
            if (spawnpoints.Count == 0) return null;
            Vector3 position = spawnpoints.GetRandom();
            List<BaseEntity> list = Facepunch.Pool.GetList<BaseEntity>();
            Vis.Entities(position, 15f, list, VIS_RAYCAST_LAYERS);
            int count = list.Count;
            Facepunch.Pool.FreeList(ref list);
            if (count > 0)
            {
                spawnpoints.Remove(position);
                if (spawnpoints.Count == 0)
                {
                    GenerateSpawnpoints();
                    return null;
                }
                return GetSpawnPoint(biomeName);
            }
            return position;
        }

        private static bool IsPointOnTerrain(Vector3 position, out float heightAtPoint)
        {
            RaycastHit raycastHit;
            if (Physics.Raycast(position, Vector3.down, out raycastHit, 500f, POINT_RAYCAST_LAYERS))
            {
                if (raycastHit.collider is TerrainCollider)
                {
                    heightAtPoint = raycastHit.point.y;
                    return true;
                }
            }
            heightAtPoint = 500f;
            return false;
        }
        #endregion Find Random Points
    }
}

namespace Oxide.Plugins.NpcSpawnExtensionMethods
{
    public static class ExtensionMethods
    {
        #region Any
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
        }
        #endregion Any

        #region Where
        public static HashSet<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) result.Add(enumerator.Current);
            return result;
        }
        #endregion Where

        #region FirstOrDefault
        public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return enumerator.Current;
            return default(TSource);
        }
        #endregion FirstOrDefault

        #region Select
        public static List<TResult> Select<TSource, TResult>(this IList<TSource> source, Func<TSource, TResult> predicate)
        {
            List<TResult> result = new List<TResult>();
            for (int i = 0; i < source.Count; i++)
            {
                TSource element = source[i];
                result.Add(predicate(element));
            }
            return result;
        }
        #endregion Select

        #region SelectMany
        public static HashSet<TResult> SelectMany<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, IEnumerable<TResult>> predicate)
        {
            HashSet<TResult> result = new HashSet<TResult>();
            foreach (TSource elements in source) foreach (TResult element in predicate(elements)) result.Add(element);
            return result;
        }
        #endregion SelectMany

        #region OfType
        public static HashSet<T> OfType<T>(this IEnumerable<BaseEntity> source)
        {
            HashSet<T> result = new HashSet<T>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (enumerator.Current is T) result.Add((T)(object)enumerator.Current);
            return result;
        }
        #endregion OfType

        #region ToHashSet
        public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }
        #endregion ToHashSet

        #region Sum
        public static float Sum<TSource>(this IList<TSource> source, Func<TSource, float> predicate)
        {
            float result = 0;
            for (int i = 0; i < source.Count; i++) result += predicate(source[i]);
            return result;
        }
        #endregion Sum

        #region Last
        public static TSource Last<TSource>(this IList<TSource> source) => source[source.Count - 1];
        #endregion Last

        #region Min
        public static TSource Min<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue < resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }
        #endregion Min

        #region Max
        public static TSource Max<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue > resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }
        #endregion Max

        #region ElementAt
        public static TSource ElementAt<TSource>(this IEnumerable<TSource> source, int index)
        {
            int movements = 0;
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (movements == index) return enumerator.Current;
                    movements++;
                }
            }
            return default(TSource);
        }
        #endregion ElementAt

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;
    }
}