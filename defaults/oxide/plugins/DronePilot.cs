using Network;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DronePilot", "k1lly0u", "1.0.0"), Description("Allow players with permission to fly drones")]
    class DronePilot : RustPlugin
    {
        #region Fields  
        private const string USE_PERMISSION = "dronepilot.use";

        private const string CREATE_PERMISSION = "dronepilot.create";

        private const float DRONE_DISABLE_HEALTH_FRACTION = 0.1f;
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            permission.RegisterPermission(USE_PERMISSION, this);
            permission.RegisterPermission(CREATE_PERMISSION, this);

            GetMessage = Message;
        }

        protected override void LoadDefaultMessages() => lang.RegisterMessages(Messages, this);

        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            BasePlayer player = planner?.GetOwnerPlayer();
            if (player == null)
                return;

            Drone drone = gameObject.ToBaseEntity() as Drone;
            if (drone != null)
            {
                if (permission.UserHasPermission(player.UserIDString, USE_PERMISSION))
                {
                    if (drone.healthFraction < DRONE_DISABLE_HEALTH_FRACTION)
                    {
                        player.ChatMessage(Message("Notification.TooMuchDamage", player.userID));
                        return;
                    }

                    DroneController droneController = gameObject.AddComponent<DroneController>();
                    droneController.StartPiloting(player);

                    player.ChatMessage(Message("Notification.Controls", player.userID));
                }
                else player.ChatMessage(Message("Error.NoFlyPermissions", player.userID));
            }
        }

        private void OnEntityTakeDamage(Drone drone, HitInfo hitInfo)
        {
            DroneController droneController = drone.GetComponent<DroneController>();
            if (droneController != null)
            {
                Rust.DamageType damageType = hitInfo.damageTypes.GetMajorityDamageType();
                if (damageType == Rust.DamageType.Generic) // Disable damage from Drone.OnCollisionEnter because its a stupid amount
                {
                    hitInfo.damageTypes.Clear();
                    hitInfo.HitEntity = null;
                    hitInfo.HitMaterial = 0;
                    hitInfo.PointStart = Vector3.zero;
                    return;
                }

                if (damageType != Rust.DamageType.Collision) // Dont do anything is its collision damage because we are managing that in DroneController.OnCollisionEnter
                {
                    BasePlayer player = droneController.Controller;

                    float healthFraction = (drone.Health() - hitInfo.damageTypes.Total()) / drone.MaxHealth();
                    if (healthFraction < 0)
                    {
                        player.ChatMessage(Message("Notification.Destroyed", player.userID));

                        droneController._killOnDestroy = true;

                        UnityEngine.Object.Destroy(droneController);
                    }
                    if (healthFraction < DRONE_DISABLE_HEALTH_FRACTION)
                    {
                        player.ChatMessage(Message("Notification.TooMuchDamage", player.userID));

                        UnityEngine.Object.Destroy(droneController);
                    }
                }
            }
        }

        private void OnPlayerWound(BasePlayer player, HitInfo hitInfo)
        {
            for (int i = 0; i < DroneController._allDroneControllers.Count; i++)
            {
                DroneController droneController = DroneController._allDroneControllers[i];

                if (droneController.Dummy == player)
                {
                    BasePlayer actualPlayer = droneController.Controller;
                    UnityEngine.Object.Destroy(droneController);

                    actualPlayer.StartWounded(hitInfo.InitiatorPlayer, hitInfo);
                    return;
                }
            }
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            for (int i = 0; i < DroneController._allDroneControllers.Count; i++)
            {
                DroneController droneController = DroneController._allDroneControllers[i];

                if (droneController.Dummy == player)
                {
                    droneController.OnPlayerDeath(hitInfo);
                    return;
                }
            }
        }

        private void OnEntityDeath(Drone drone, HitInfo hitInfo)
        {
            DroneController droneController = drone.GetComponent<DroneController>();
            if (droneController != null)
            {
                BasePlayer player = droneController.Controller;
                if (player != null)
                    player.ChatMessage(Message("Notification.Destroyed", player.userID));

                droneController._killOnDestroy = true;

                UnityEngine.Object.Destroy(droneController);
            }
        }

        private object CanPickupEntity(BasePlayer player, Drone drone)
        {
            DroneController droneController = drone.GetComponent<DroneController>();
            if (droneController != null && droneController.Controller != null)
                return false;
            return null;
        }

        private object CanSpectateTarget(BasePlayer player, string name)
        {
            DroneController droneController = player.GetMounted()?.GetComponentInParent<DroneController>();
            if (droneController)            
                return false;            
            return null;
        }

        private void Unload()
        {
            for (int i = DroneController._allDroneControllers.Count - 1; i >= 0; i--)            
                UnityEngine.Object.Destroy(DroneController._allDroneControllers[i]);            

            Configuration = null;
            GetMessage = null;
        }
        #endregion

        #region Functions
        private static void MoveInventoryTo(PlayerInventory from, PlayerInventory to)
        {
            for (int i = from.containerBelt.itemList.Count - 1; i >= 0; i--)
            {
                Item item = from.containerBelt.itemList[i];
                item.MoveToContainer(to.containerBelt, item.position, false);
            }

            for (int i = from.containerMain.itemList.Count - 1; i >= 0; i--)
            {
                Item item = from.containerMain.itemList[i];
                item.MoveToContainer(to.containerMain, item.position, false);
            }

            for (int i = from.containerWear.itemList.Count - 1; i >= 0; i--)
            {
                Item item = from.containerWear.itemList[i];
                item.MoveToContainer(to.containerWear, item.position, false);
            }
        }
        #endregion

        #region Controller
        private class DroneController : MonoBehaviour
        {
            public static List<DroneController> _allDroneControllers = new List<DroneController>();


            public Drone Drone { get; private set; }

            public BasePlayer Controller { get; private set; }

            public BasePlayer Dummy { get; private set; }

            public bool IsBeingControlled => Controller != null;


            private DroneInputState _currentInput;

            private float _lastInputTime;

            private double _lastCollision = -1000.0;

            private bool _isGrounded;

            private Transform _tr;

            private Rigidbody _rb;

            private BaseMountable _mountPoint;


            private float _avgTerrainHeight;

            internal bool _killOnDestroy = false;

            private void Awake()
            {
                Drone = GetComponent<Drone>();
                Drone.enabled = false;

                _tr = Drone.transform;
                _rb = Drone.GetComponent<Rigidbody>();

                _allDroneControllers.Add(this);
            }

            private void OnDestroy()
            {
                if (Controller != null)
                {
                    StopControllingDrone();

                    RestorePlayer();                    
                }

                if (_mountPoint != null && !_mountPoint.IsDestroyed)
                    _mountPoint.Kill(BaseNetworkable.DestroyMode.None);

                _allDroneControllers.Remove(this);

                if (_killOnDestroy && Drone != null && !Drone.IsDead())
                    Drone.Kill();
            }

            internal void StartPiloting(BasePlayer player)
            {
                CreateMountPoint();

                Controller = player;
                CreateDummyPlayer();

                Controller.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
                Controller.MountObject(_mountPoint);

                if (Net.sv.write.Start())
                {
                    Net.sv.write.PacketID(Network.Message.Type.EntityDestroy);
                    Net.sv.write.EntityID(player.net.ID);
                    Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
                    Net.sv.write.Send(new SendInfo(player.net.group.subscribers.Where(x => x.userid != player.userID).ToList()));
                }

                Controller.limitNetworking = true;
                Controller.syncPosition = false;
            }

            internal void OnPlayerDeath(HitInfo hitInfo)
            {
                StopControllingDrone();

                Controller.Die(hitInfo);

                Controller = null;

                Destroy(this);
            }

            private void StopControllingDrone()
            {
                Controller.PauseFlyHackDetection(3f);

                Controller.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
                Controller.limitNetworking = false;
                Controller.syncPosition = true;

                Controller.DismountObject();
                Controller.EnsureDismounted();
            }

            private void RestorePlayer()
            {
                Controller.transform.position = Dummy.transform.position;

                if (Dummy != null && !Dummy.IsDead())
                {
                    MoveInventoryTo(Dummy.inventory, Controller.inventory);
                    Dummy.Kill(BaseNetworkable.DestroyMode.None);
                }

                Controller = null;
            }

            private void CreateDummyPlayer()
            {
                Dummy = (BasePlayer)GameManager.server.CreateEntity(Controller.PrefabName, Controller.transform.position, Controller.transform.rotation);
                Dummy.enableSaving = false;
                Dummy.Spawn();

                Dummy.displayName = Controller.displayName;
                Dummy.SetFlag(BaseEntity.Flags.Reserved8, true);

                Dummy.inventory.Strip();

                MoveInventoryTo(Controller.inventory, Dummy.inventory);                
            }

            private void CreateMountPoint()
            {
                const string CHAIR_PREFAB = "assets/prefabs/deployable/chair/chair.deployed.prefab";

                _mountPoint = GameManager.server.CreateEntity(CHAIR_PREFAB, _tr.position) as BaseMountable;
                _mountPoint.enableSaving = false;
                _mountPoint.skinID = (ulong)1169930802;
                _mountPoint.isMobile = true;
                _mountPoint.Spawn();

                Destroy(_mountPoint.GetComponent<DestroyOnGroundMissing>());
                Destroy(_mountPoint.GetComponent<GroundWatch>());
                Destroy(_mountPoint.GetComponent<MeshCollider>());

                _mountPoint.SetParent(Drone);
                _mountPoint.transform.localPosition = new Vector3(0f, -0.5f, -1f);
            }

            private void UserInput(InputState inputState)
            {
                _currentInput.Reset();

                float forward = (inputState.IsDown(BUTTON.FORWARD) ? 1 : 0) + (inputState.IsDown(BUTTON.BACKWARD) ? -1 : 0);
                float sideways = (inputState.IsDown(BUTTON.RIGHT) ? 1 : 0) + (inputState.IsDown(BUTTON.LEFT) ? -1 : 0);

                _currentInput.movement = new Vector3(sideways, 0f, forward).normalized;
                _currentInput.throttle = ((inputState.IsDown(BUTTON.SPRINT) ? 1 : 0) + (inputState.IsDown(BUTTON.DUCK) ? -1 : 0));

                _lastInputTime = UnityEngine.Time.time;
            }

            private void FixedUpdate()
            {
                if (Drone.IsDead() || !IsBeingControlled)                
                    return;

                float num = Drone.WaterFactor();
                if (Drone.killInWater && num > 0f)
                {
                    if (num > 0.99f)
                    {
                        Controller.ChatMessage(GetMessage("Notification.WaterDamage", Controller.userID));

                        _killOnDestroy = true;
                        Destroy(this);
                    }
                    return;
                }

                if (Controller.serverInput.WasJustPressed(BUTTON.JUMP))
                {
                    Destroy(this);
                    return;
                }

                UserInput(Controller.serverInput);
                
                bool isCollisionDisabled = _lastCollision > 0.0 && TimeEx.currentTimestamp - _lastCollision < (double)Configuration.CollisionDisableTime;

                RaycastHit raycastHit;
                _isGrounded = (Drone.enableGrounding && _rb.SweepTest(-_tr.up, out raycastHit, Drone.groundTraceDist));

                Vector3 localToWorld = _tr.TransformDirection(_currentInput.movement);
                Vector3 direction;
                float magnitude;
                _rb.velocity.WithY(0f).ToDirectionAndMagnitude(out direction, out magnitude);

                float leanMagnitude = Mathf.Clamp01(magnitude / Configuration.LeanMaxVelocity);
                Vector3 worldLeanVelocity = Mathf.Approximately(localToWorld.sqrMagnitude, 0f) ? (-leanMagnitude * direction) : localToWorld;
                Vector3 normalizedLean = (Vector3.up + worldLeanVelocity * Configuration.LeanWeight * leanMagnitude).normalized;

                float leanForce = Mathf.Max(Vector3.Dot(normalizedLean, _tr.up), 0f);

                _avgTerrainHeight = Mathf.Lerp(_avgTerrainHeight, TerrainMeta.HeightMap.GetHeight(_tr.position), Time.fixedDeltaTime * 20f);

                if (!isCollisionDisabled || _isGrounded)
                {
                    Vector3 descendVector = (_isGrounded && _currentInput.throttle <= 0f) ? Vector3.zero : (-1f * _tr.up * Physics.gravity.y);
                    Vector3 movementVector = _isGrounded ? Vector3.zero : (localToWorld * Configuration.MovementAcceleration);
                    Vector3 ascendVector = _tr.up * _currentInput.throttle * Configuration.AltitudeAcceleration;
                    Vector3 force = descendVector + movementVector + ascendVector;

                    _rb.AddForce(force * leanForce, ForceMode.Acceleration);
                }

                if (!isCollisionDisabled && !_isGrounded)
                {
                    Vector3 worldYaw = _tr.TransformVector(0f, _currentInput.movement.x * Configuration.YawSpeed, 0f);
                    Vector3 lean = Vector3.Cross(Quaternion.Euler(_rb.angularVelocity * Drone.uprightPrediction) * _tr.up, normalizedLean) * Configuration.UprightSpeed;
                    float leanMultiplier = (leanForce < Drone.uprightDot) ? 0f : leanForce;

                    Vector3 force = worldYaw * leanForce + lean * leanMultiplier;
                                       
                    _rb.AddTorque(force * leanForce, ForceMode.Acceleration);

                    if (_currentInput.throttle == 0 && _currentInput.movement == Vector3.zero)
                        _rb.AddForce((Physics.gravity * 2f) * Time.fixedDeltaTime, ForceMode.Acceleration);
                }

                if (_tr.position.y > _avgTerrainHeight + Configuration.MaximumCruiseHeight)
                {
                    if (_rb.velocity.y > 0)
                    {
                        Vector3 velocity = _rb.velocity;
                        velocity.y = 0;
                        _rb.velocity = velocity;
                    }
                }


                UpdateNetworkGroup();
            }

            private void OnCollisionEnter(Collision collision)
            {
                _lastCollision = TimeEx.currentTimestamp;
                float magnitude = collision.relativeVelocity.magnitude;
                if (magnitude > Configuration.HurtVelocityThreshold)
                {
                    float damageAmount = (magnitude / Configuration.HurtVelocityThreshold) * Configuration.HurtDamagePower;

                    float healthFraction = (Drone.Health() - damageAmount) / Drone.MaxHealth();
                    if (healthFraction < 0)
                    {
                        Controller.ChatMessage(GetMessage("Notification.Destroyed", Controller.userID));

                        _killOnDestroy = true;

                        Destroy(this);
                        return;
                    }
                    else if (healthFraction < DRONE_DISABLE_HEALTH_FRACTION)
                    {
                        Controller.ChatMessage(GetMessage("Notification.TooMuchDamage", Controller.userID));
                        Destroy(this);
                        return;
                    }
                    else Drone.Hurt(damageAmount, Rust.DamageType.Collision);
                }

                _rb.velocity = Vector3.Reflect(_rb.velocity, collision.contacts[0].normal);
            }

            private void OnCollisionStay()
            {
                _lastCollision = TimeEx.currentTimestamp;
            }

            private void UpdateNetworkGroup()
            {
                Network.Visibility.Group group = Net.sv.visibility.GetGroup(_tr.position);
                if (_mountPoint.net.group != group)
                    _mountPoint.net.SwitchGroup(group);

                if (Controller.net.group != group)
                    Controller.net.SwitchGroup(group);
            }

            private struct DroneInputState
            {
                public Vector3 movement;

                public float throttle;

                public void Reset()
                {
                    movement = Vector3.zero;                    
                }
            }
        }
        #endregion

        #region Chat Commands
        [ChatCommand("drone")]
        private void GiveDrone(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, CREATE_PERMISSION))
            {
                player.ChatMessage(Message("Error.NoPermission", player.userID));
                return;
            }

            const string DRONE_ITEM = "drone";

            player.GiveItem(ItemManager.CreateByName(DRONE_ITEM), BaseEntity.GiveItemReason.PickedUp);
        }
        #endregion

        #region Config        
        private static ConfigData Configuration;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Vertical acceleration speed")]
            public float AltitudeAcceleration { get; set; }

            [JsonProperty(PropertyName = "Horizontal acceleration speed")]
            public float MovementAcceleration { get; set; }

            [JsonProperty(PropertyName = "Yaw speed")]
            public float YawSpeed { get; set; }

            [JsonProperty(PropertyName = "The speed that the drone will return to a levelled rotation after leaning")]
            public float UprightSpeed { get; set; }

            [JsonProperty(PropertyName = "Lean weight (how much the drone leans in to movement) (0.0 -> 1.0)")]
            public float LeanWeight { get; set; }

            [JsonProperty(PropertyName = "Lean max velocity (the maximum velocity for full lean)")]
            public float LeanMaxVelocity { get; set; }

            [JsonProperty(PropertyName = "The impact speed before damage is applied")]
            public float HurtVelocityThreshold { get; set; }

            [JsonProperty(PropertyName = "The amount of damage to apply from impacts (scales depending on speed)")]
            public float HurtDamagePower { get; set; }

            [JsonProperty(PropertyName = "The amount of time to disable the drone controls after a collision (seconds)")]
            public float CollisionDisableTime { get; set; }

            [JsonProperty(PropertyName = "Maximum cruising height above terrain")]
            public float MaximumCruiseHeight { get; set; }

            public Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = Config.ReadObject<ConfigData>();

            if (Configuration.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(Configuration, true);
        }

        protected override void LoadDefaultConfig() => Configuration = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                AltitudeAcceleration = 20f,
                MovementAcceleration = 15f,
                YawSpeed = 6f,
                UprightSpeed = 2f,
                LeanWeight = 0.35f,
                LeanMaxVelocity = 6f,
                HurtVelocityThreshold = 3f,
                HurtDamagePower = 3f,
                CollisionDisableTime = 0.25f,
                MaximumCruiseHeight = 30f,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(Configuration, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            Configuration.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Localization
        public string Message(string key, ulong playerId = 0U) => lang.GetMessage(key, this, playerId != 0U ? playerId.ToString() : null);

        private static Func<string, ulong, string> GetMessage;

        private readonly Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["Notification.TooMuchDamage"] = "This drone is too damaged to fly. Repair it to continue use",
            ["Notification.WaterDamage"] = "Drones can't swim... n00b",
            ["Notification.Destroyed"] = "Your drone has been destroyed",
            ["Notification.Controls"] = "<size=18><color=#ffa500>COBALT DRONE</color></size><size=14> - <color=#ce422b>Controls</color></size>\nForward/BackwardLeft/Right - Movement\nSprint/Duck - Ascend/Descend\nJump - Exit controls",
            ["Error.NoFlyPermissions"] = "You do not have permission to fly drones",
            ["Error.NoPermission"] = "You do not have permission to use this command",
        };
        #endregion
    }
}
