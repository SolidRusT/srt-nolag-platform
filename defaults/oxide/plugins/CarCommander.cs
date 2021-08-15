//Reference: UnityEngine.VehiclesModule
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Rust;
using System.Linq;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("CarCommander", "k1lly0u", "0.2.68")]
    [Description("A custom car controller with many options including persistence")]
    class CarCommander : RustPlugin
    {
        #region Fields
        [PluginReference]
        Plugin Clans, Friends, Spawns, RandomSpawns;

        RestoreData storedData;
        private Dictionary<ulong, double> userCooldowns = new Dictionary<ulong, double>();
        private DynamicConfigFile data, cooldowns;

        private static CarCommander ins;

        private Dictionary<string, string> itemNames = new Dictionary<string, string>();
        private Dictionary<ulong, HotwireManager> isHotwiring = new Dictionary<ulong, HotwireManager>();
        private Dictionary<CommandType, BUTTON> controlButtons;

        private List<CarController> temporaryCars = new List<CarController>();
        private List<CarController> saveableCars = new List<CarController>();

        private bool initialized;
        private bool wipeData = false;

        private int fuelType;        
        private int repairType;
        private string fuelTypeName;
        private string repairTypeName;

        const string carPrefab = "assets/content/vehicles/sedan_a/sedantest.entity.prefab";
        const string boxPrefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
        const string explosionPrefab = "assets/prefabs/npc/m2bradley/effects/bradley_explosion.prefab";

        const string uiHealth = "CCUI_Health";
        const string uiFuel = "CCUI_Fuel";
        #endregion
        
        #region Oxide Hooks
        private void Loaded()
        {
            permission.RegisterPermission("carcommander.admin", this);
            permission.RegisterPermission("carcommander.use", this);
            permission.RegisterPermission("carcommander.canspawn", this);
            permission.RegisterPermission("carcommander.canbuild", this);
            permission.RegisterPermission("carcommander.ignorecooldown", this);

            lang.RegisterMessages(Messages, this);
            data = Interface.Oxide.DataFileSystem.GetFile("carcommander_data");
            cooldowns = Interface.Oxide.DataFileSystem.GetFile("carcommander_cooldowns");
        }

        private void OnServerInitialized()
        {
            ins = this;
            LoadData();

            ConvertControlButtons();

            fuelType = ItemManager.itemList.Find(x => x.shortname == configData.Fuel.FuelType)?.itemid ?? 0;
            repairType = ItemManager.itemList.Find(x => x.shortname == configData.Repair.Shortname)?.itemid ?? 0;
            fuelTypeName = ItemManager.itemList.Find(x => x.shortname == configData.Fuel.FuelType)?.displayName.english ?? "Invalid fuel shortname set in config!";
            repairTypeName = ItemManager.itemList.Find(x => x.shortname == configData.Repair.Shortname)?.displayName.english ?? "Invalid repair item shortname set in config!";
                       
            initialized = true;

            if (wipeData)
            {
                PrintWarning("Map wipe detected! Wiping previous car data");
                storedData.restoreData.Clear();
                SaveData();
            }

            timer.In(3, RestoreVehicleInventories);
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;

            if (entity.GetComponent<CarController>())   
                entity.GetComponent<CarController>().ManageDamage(info); 
            else if (entity.GetComponent<StorageContainer>())
            {
                if (entity.GetParentEntity()?.GetComponent<CarController>())
                    NullifyDamage(info);
            }
        }
        
        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || info.HitEntity == null || !configData.Repair.Enabled)
                return;

            CarController controller = info.HitEntity.GetComponent<CarController>();
            if (controller != null && controller.entity != null)
            {
                if (controller.entity.health < controller.entity.MaxHealth())
                {
                    if (player.inventory.GetAmount(repairType) >= configData.Repair.Amount)
                    {
                        player.inventory.Take(null, repairType, configData.Repair.Amount);
                        controller.entity.Heal(configData.Repair.Damage);
                        player.Command("note.inv", new object[] { repairType, configData.Repair.Amount * -1 });
                    }
                    else SendReply(player, string.Format(msg("noresources", player.UserIDString), configData.Repair.Amount, repairTypeName));
                }
                else SendReply(player, msg("fullhealth", player.UserIDString));
            }
        }

        private void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            if (!configData.Security.Trunk.RequireKey)
                return;

            BasePlayer player = deployer.GetOwnerPlayer();

            if (player == null || entity == null)
                return;

            CarController controller = entity.GetComponentInParent<CarController>();
            if (controller != null)
            {
                Deployable deployable = deployer.GetDeployable();
                if (deployable != null)
                {
                    if (deployable.prefabID == 3518824735 || deployable.prefabID == 2106860026)
                    {
                        if (controller.HasVehicleKey(player)) 
                            return;

                        entity.GetSlot(deployable.slot)?.Kill();

                        if (deployable.prefabID == 3518824735)
                            player.GiveItem(ItemManager.CreateByItemID(1159991980, 1, 0)); // Codelock
                        else player.GiveItem(ItemManager.CreateByItemID(-850982208, 1, 0));

                        SendReply(player, msg("no_key_deploy", player.UserIDString));
                    }
                }
            }            
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!initialized || player == null) return;

            if (input.WasJustPressed(BUTTON.USE))
            {
                RaycastHit hit;
                if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 3f))
                {
                    CarController controller = hit.GetEntity()?.GetComponent<CarController>();
                    if (controller != null && controller.HasCommander() && !controller.occupants.Contains(player))
                        CanMountEntity(player, controller.entity);
                }
                return;
            }
            
            if (configData.Fuel.Enabled && input.WasJustPressed(controlButtons[CommandType.FuelTank]))
            {
                RaycastHit hit;
                if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 3f))
                {
                    CarController controller = hit.GetEntity()?.GetComponent<CarController>();
                    if (controller != null && !controller.HasCommander() && !controller.occupants.Contains(player))
                        OpenInventory(player, controller, controller.fuelTank);
                }
                return;
            }
        }

        private void OnEntityKill(BaseNetworkable networkable)
        {
            BasicCar BasicCar = networkable.GetComponent<BasicCar>();
            if (BasicCar != null)
            {
                if (storedData.HasRestoreData(BasicCar.net.ID))
                    storedData.RemoveData(BasicCar.net.ID);


                CarController controller = BasicCar.GetComponent<CarController>();
                if (controller != null)
                {
                    saveableCars.Remove(controller);
                    temporaryCars.Remove(controller);
                }
            }
        }

        private void OnNewSave(string filename) => wipeData = true;

        private void OnServerSave()
        {
            for (int i = saveableCars.Count - 1; i >= 0; i--)
            {
                CarController controller = saveableCars[i];
                if (controller == null || controller.entity == null || !controller.entity.IsValid() || controller.entity.IsDestroyed)
                {
                    saveableCars.RemoveAt(i);
                    continue;
                }
                storedData.AddData(controller);
            }
            SaveData();
        }

        private void Unload()
        {

            CarController[] objects = saveableCars.Union(temporaryCars).ToArray();
            for (int i = 0; i < objects.Length; i++)            
                UnityEngine.Object.Destroy(objects[i]);            

            objects = UnityEngine.Object.FindObjectsOfType<CarController>();
            if (objects != null)
            {
                foreach (CarController obj in objects)
                    UnityEngine.Object.Destroy(obj);
            }

            ins = null;
        }

        private object CanPickupEntity(BaseCombatEntity entity, BasePlayer player)
        {
            if (entity.GetComponentInParent<CarController>())
                return false;            
            return null;
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (isHotwiring.ContainsKey(player.userID))            
                isHotwiring[player.userID].CancelHotwire();

            if (configData.Decay.InstantDecay)
                ServerMgr.Instance.StartCoroutine(DelayedDestroy(player.userID));
        }

        private IEnumerator DelayedDestroy(ulong playerId)
        {
            List<CarController> controllers = Facepunch.Pool.GetList<CarController>();
            controllers.AddRange(saveableCars);
            controllers.AddRange(temporaryCars);

            for (int i = controllers.Count - 1; i >= 0; i--)
            {
                CarController controller = controllers[i];
                if (controller?.ownerId == playerId)
                {
                    BasicCar BasicCar = controller?.entity;
                    UnityEngine.Object.Destroy(controller);

                    if (BasicCar != null && !BasicCar.IsDestroyed)
                        BasicCar.Kill();
                }

                yield return null;
            }

            Facepunch.Pool.FreeList(ref controllers);
        }

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            CarController controller = mountable.VehicleParent()?.GetComponent<CarController>();
            if (controller != null)
                controller.OnEntityMounted(player, mountable == controller.entity.mountPoints[0].mountable);
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            CarController controller = mountable.VehicleParent()?.GetComponent<CarController>();
            if (controller != null)
            {
                if (controller.Commander?.userID == player.userID)
                {
                    if (isHotwiring.ContainsKey(player.userID))
                    {
                        isHotwiring[player.userID].CancelHotwire();
                        SendReply(player, ins.msg("hotwire_fail_left", player.UserIDString));
                    }
                }
                controller.OnEntityDismounted(player, mountable);
            }            
        }

        private object CanMountEntity(BasePlayer player, BaseMountable mountable)
        {
            CarController controller = mountable.VehicleParent()?.GetComponent<CarController>();
            if (controller != null)
            {
                if (player.isMounted)
                    return false;

                if (controller.externallyManaged)
                    return false;

                if (controller.isDieing)
                    return false;

                if (controller.IsFlipped)
                    return false;

                if (!permission.UserHasPermission(player.UserIDString, "carcommander.use"))
                {
                    SendReply(player, msg("nopermission", player.UserIDString));
                    return false;
                }

                if (mountable == controller.entity.mountPoints[0].mountable)                
                    return null;
                else
                {
                    if (configData.Passengers.Enabled)
                    {
                        BasePlayer commander = controller.Commander;

                        if (commander == null)                        
                            return null;                        
                        else
                        {
                            if (!configData.Passengers.UseFriends && !configData.Passengers.UseClans)                            
                                return null;
                            if (configData.Passengers.UseFriends && AreFriends(commander.userID, player.userID))                            
                                return null;
                            if (configData.Passengers.UseClans && IsClanmate(commander.userID, player.userID))                            
                                return null;

                            SendReply(player, msg("not_friend", player.UserIDString));
                            return false;
                        }
                    }
                    else
                    {
                        SendReply(player, msg("not_enabled", player.UserIDString));
                        return false;
                    }
                }
            }            
            return null;
        }

        private object CanDismountEntity(BasePlayer player, BaseMountable mountable)
        {
            CarController controller = mountable.VehicleParent()?.GetComponent<CarController>();
            if (controller != null)
            {
                if (controller.externallyManaged)
                    return null;

                // Temporary solution for failed dismount
                controller.DismountPlayer(player, mountable);
                return false;
            }           
            return null;
        }

         
        #endregion

        #region Functions
        private T ParseType<T>(string type)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), type, true);
            }
            catch
            {
                PrintError($"INVALID CONFIG OPTION DETECTED! The value \"{type}\" is an incorrect selection.\nAvailable options are: {Enum.GetNames(typeof(T)).ToSentence()}");
                return default(T);
            }
        }
        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm) || permission.UserHasPermission(player.UserIDString, "Carcommander.admin");

        private void ConvertControlButtons()
        {
            controlButtons = new Dictionary<CommandType, BUTTON>
            {
                [CommandType.Accelerate] = ParseType<BUTTON>(configData.Buttons.Accelerate),
                [CommandType.Brake] = ParseType<BUTTON>(configData.Buttons.Brake),
                [CommandType.Left] = ParseType<BUTTON>(configData.Buttons.Left),
                [CommandType.Right] = ParseType<BUTTON>(configData.Buttons.Right),
                [CommandType.Handbrake] = ParseType<BUTTON>(configData.Buttons.HBrake),
                [CommandType.Lights] = ParseType<BUTTON>(configData.Buttons.Lights),
                [CommandType.FuelTank] = ParseType<BUTTON>(configData.Buttons.FuelTank)
            };
        }

        private void NullifyDamage(HitInfo info)
        {
            info.damageTypes = new DamageTypeList();
            info.HitEntity = null;
            info.HitMaterial = 0;
            info.PointStart = Vector3.zero;
        }

        private void OpenInventory(BasePlayer player, CarController controller, ItemContainer container)
        {
            player.inventory.loot.Clear();
            player.inventory.loot.entitySource = controller.entity;
            player.inventory.loot.itemSource = null;
            player.inventory.loot.AddContainer(container);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "generic");
            player.SendNetworkUpdate();
        }
        
        private void RestoreVehicleInventories()
        {
            if (storedData.restoreData.Count > 0)
            {
                BaseEntity[] objects = BaseEntity.saveList.Where(x => x is BasicCar).ToArray();
                if (objects != null)
                {
                    foreach (var obj in objects)
                    {
                        if (obj == null || !obj.IsValid() || obj.IsDestroyed)
                            continue;

                        if (storedData.HasRestoreData(obj.net.ID))
                        {
                            obj.gameObject.AddComponent<CarController>();
                        }
                    }
                }
            }
            CheckForSpawns();
        }

        private void CheckForSpawns()
        {
            if (configData.Spawnable.Enabled)
            {
                if (saveableCars.Count < configData.Spawnable.Max)
                {
                    object position = null;
                    if (configData.Spawnable.RandomSpawns)
                    {
                        if (!RandomSpawns)
                        {                            
                            PrintError("RandomSpawns can not be found! Unable to autospawn cars");
                            return;
                        }

                        object success = RandomSpawns.Call("GetSpawnPoint");
                        if (success != null)
                            position = (Vector3)success;
                        else PrintError("Unable to find a valid spawnpoint from RandomSpawns");
                    }
                    else
                    {
                        if (!Spawns)
                        {
                            PrintError("Spawns Database can not be found! Unable to autospawn cars");
                            return;
                        }

                        object success = Spawns.Call("GetSpawnsCount", configData.Spawnable.Spawnfile);
                        if (success is string)
                        {
                            PrintError("An invalid spawnfile has been set in the config. Unable to autospawn cars : " + (string)success);
                            return;
                        }

                        success = Spawns.Call("GetRandomSpawn", configData.Spawnable.Spawnfile);
                        if (success is string)
                        {
                            PrintError((string)success);
                            return;
                        }
                        else position = (Vector3)success;
                    }

                    if (position != null)
                    {
                        List<BasicCar> entities = Facepunch.Pool.GetList<BasicCar>();
                        Vis.Entities((Vector3)position, 5f, entities);
                        if (entities.Count > 0)
                        {
                            timer.In(10, CheckForSpawns);
                            return;
                        }
                        else SpawnAtLocation((Vector3)position, new Quaternion(), true);                            
                        
                        Facepunch.Pool.FreeList(ref entities);
                    }
                }
                timer.In(configData.Spawnable.Time, CheckForSpawns);
            }
        }

        private int CountPlayerVehicles(ulong playerId)
        {
            int count = 0;
            for (int i = 0; i < saveableCars.Count; i++)
            {
                if (saveableCars[i].ownerId == playerId)
                    count++;
            }

            for (int i = 0; i < temporaryCars.Count; i++)
            {
                if (temporaryCars[i].ownerId == playerId)
                    count++;
            }
            return count;
        }

        private double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds((float)time);
            var days = dateDifference.Days;
            var hours = dateDifference.Hours;
            hours += (days * 24);
            var mins = dateDifference.Minutes;
            var secs = dateDifference.Seconds;
            return string.Format("{0:00}:{1:00}:{2:00}", hours, mins, secs);
        }
        #endregion

        #region API
        private BaseEntity SpawnAtLocation(Vector3 position, Quaternion rotation = default(Quaternion), bool enableSaving = false, bool isExternallyManaged = false, bool repairEnabled = true, bool disableFuel = false, bool disableSecurity = false, bool disableCollision = false)
        {
            BaseEntity entity = GameManager.server.CreateEntity(carPrefab, position + Vector3.up, rotation);
            entity.enableSaving = enableSaving;
            entity.Spawn();

            CarController controller = entity.gameObject.AddComponent<CarController>();
            
            if (enableSaving)
            {
                saveableCars.Add(controller);
                storedData.AddData(controller);
                SaveData();
            }
            else temporaryCars.Add(controller);

            if (isExternallyManaged)
                controller.SetExternallyManaged();
            else controller.SetFeatures(repairEnabled, disableFuel, disableSecurity, disableCollision);            

            return entity;
        }

        private void ToggleController(BasicCar BasicCar, bool enabled)
        {
            if (BasicCar == null)
                return;

            CarController controller = BasicCar.GetComponent<CarController>();
            if (controller != null)
            {
                controller.enabled = enabled;

                if (!enabled)
                {
                    foreach (var wheel in controller.entity.wheels)
                        wheel.wheelCollider.brakeTorque = 1f;
                }
            }
        }

        private void MountPlayerTo(BasePlayer player, BasicCar BasicCar)
        {
            if (player == null)
                return;

            CarController controller = BasicCar.GetComponent<CarController>();
            if (controller != null)
            {
                BaseMountable mountable = controller.entity.mountPoints[0].mountable;
                if (mountable != null && mountable.GetMounted() == null)
                {
                    player.EnsureDismounted();
                    mountable._mounted = player;
                    player.MountObject(mountable, 0);
                    player.MovePosition(mountable.mountAnchor.transform.position);
                    player.transform.rotation = mountable.mountAnchor.transform.rotation;
                    player.ServerRotation = mountable.mountAnchor.transform.rotation;
                    player.OverrideViewAngles(mountable.mountAnchor.transform.rotation.eulerAngles);
                    player.eyes.NetworkUpdate(mountable.mountAnchor.transform.rotation);
                    player.ClientRPCPlayer<Vector3>(null, player, "ForcePositionTo", player.transform.position);
                    mountable.SetFlag(BaseEntity.Flags.Busy, true, false);
                    controller.OnEntityMounted(player, true);
                }
            }
        }

        private void EjectAllPlayers(BasicCar BasicCar)
        {
            CarController controller = BasicCar.GetComponent<CarController>();
            if (controller != null)
                controller.EjectAllPlayers();
        }

        private ItemContainer GetVehicleInventory(BasicCar BasicCar)
        {
            CarController controller = BasicCar.GetComponent<CarController>();
            if (controller == null)
                return null;

            return controller.container.inventory;
        }

        private ItemContainer GetVehicleFuelTank(BasicCar BasicCar)
        {
            CarController controller = BasicCar.GetComponent<CarController>();
            if (controller == null)
                return null;

            return controller.fuelTank;
        }
        #endregion

        #region Component
        enum CommandType { Accelerate, Brake, Left, Right, Handbrake, FuelTank, Lights }

        public class CarController : MonoBehaviour
        {
            #region Variables
            public BasicCar entity;

            public StorageContainer container;
            public ItemContainer fuelTank;
            private Rigidbody rb;

            private Dictionary<CommandType, BUTTON> cb;
            private ConfigData.SecurityOptions security;
            private ConfigData.CollisionOptions collision;
            private ConfigData.DecaySettings decay;

            public List<BasePlayer> occupants = new List<BasePlayer>();

            WheelCollider[] allWheels = new WheelCollider[4];
            WheelFrictionCurve sidewaysFriction = new WheelFrictionCurve();
            WheelFrictionCurve sidewaysFrictionHB = new WheelFrictionCurve();

            public ulong ownerId = 0UL;

            private float engineTorque;
            private float brakeTorque;
            private float reverseTorque;

            private float speed;
            private float maxSpeed;

            private bool useDefaultHandling;
            private float maxSteeringAngleSpeed;
            private float maxSteeringAngle;
            private bool applyCounterSteer;
            private float driftAngle;
            private bool isDrifting;

            public float antiRollFrontHorizontal = 5000f; 
            public float antiRollRearHorizontal = 5000f;
            public float antiRollVertical = 500f;

            private float accelInput = 0f;
            private float brakeInput = 0f;
            private float steerInput = 0f;

            private bool repairEnabled;

            private bool fuelEnabled;
            private float consumptionRate;
            private int fuelId;
            private float pendingFuel;
            private float nextFuelCheckTime;
            private bool hasFuel = false;

            private bool isFlipped;
            private float upsideDownTime;

            private int ignitionCode;
            private bool hasBeenHotwired;
            private bool ignitionOn;

            private bool eBrake;
            private bool driftFriction;

            public bool externallyManaged;
            public bool isDieing;
            public bool lightsOn;

            // Temporary solution for dismounting players
            private Vector3[] dismountLocals = new Vector3[]
            {
                new Vector3(-1.4f, 0.1f, 0.5f),
                new Vector3(1.4f, 0.1f, 0.5f),
                new Vector3(-1.4f, 0.1f, -0.5f),
                new Vector3(1.4f, 0.1f, -0.5f)
            };

            public BasePlayer Commander
            {
                get
                {
                    return entity.mountPoints[0].mountable.GetMounted();
                }                
            }

            public bool HasBeenHotwired
            {
                get
                {
                    return hasBeenHotwired;
                }
                set
                {
                    hasBeenHotwired = value;
                }
            }

            public int IgnitionCode
            {
                get
                {
                    return ignitionCode;
                }
                set
                {
                    ignitionCode = value;
                }
            }

            public bool IsFlipped
            {
                get
                {
                    return isFlipped;
                }
            }
            #endregion

            #region Initialization            
            private void Awake()
            {
                entity = GetComponent<BasicCar>();
                entity.enabled = false;

                //if (entity.IsMounted())
                // entity.DismountAllPlayers();
                EjectAllPlayers();

                SetWheelColliders(); 
                InitializeSettings();
                InitializeFuel();
                InitializeInventory();
                InitializeDecay();               
            }

            private void SetWheelColliders()
            {
                if (!ins.configData.Movement.CustomHandling)
                {
                    useDefaultHandling = true;
                    return;
                }

                allWheels = new WheelCollider[] { entity.wheels[0].wheelCollider, entity.wheels[1].wheelCollider, entity.wheels[2].wheelCollider, entity.wheels[3].wheelCollider };

                WheelFrictionCurve forwardFriction = new WheelFrictionCurve();
                forwardFriction.extremumSlip = 0.2f;
                forwardFriction.extremumValue = 1;
                forwardFriction.asymptoteSlip = 0.8f;
                forwardFriction.asymptoteValue = 0.75f;
                forwardFriction.stiffness = 1.5f;

                sidewaysFriction.extremumSlip = 0.2f;
                sidewaysFriction.extremumValue = 0.8f;
                sidewaysFriction.asymptoteSlip = 0.4f;
                sidewaysFriction.asymptoteValue = 0.6f;
                sidewaysFriction.stiffness = 1.0f;

                sidewaysFrictionHB.extremumSlip = 0.15f;
                sidewaysFrictionHB.extremumValue = 0.7f; 
                sidewaysFrictionHB.asymptoteSlip = 0.2f;
                sidewaysFrictionHB.asymptoteValue = 0.5f;
                sidewaysFrictionHB.stiffness = 0.75f;

                var movement = ins.configData.Movement;

                foreach (WheelCollider wc in allWheels)
                {                    
                    JointSpring spring = wc.suspensionSpring;

                    spring.spring = movement.Spring;
                    spring.damper = movement.Damper;
                    spring.targetPosition = movement.Target;

                    wc.suspensionSpring = spring;
                    wc.suspensionDistance = movement.Distance;
                    wc.forceAppPointDistance = 0.1f;
                    wc.mass = 40f;
                    wc.wheelDampingRate = 1f;
                    
                    wc.sidewaysFriction = sidewaysFriction;
                    wc.forwardFriction = forwardFriction;
                }                
            }
           
            private void InitializeSettings()
            {
                rb = entity.GetComponent<Rigidbody>();
                cb = ins.controlButtons;

                security = ins.configData.Security;
                collision = ins.configData.Collision;

                var movement = ins.configData.Movement;
                engineTorque = movement.Acceleration;
                brakeTorque = movement.Brakes;
                reverseTorque = movement.Reverse;

                maxSteeringAngle = movement.Steer;
                maxSteeringAngleSpeed = movement.SteerSpeed;
                applyCounterSteer = movement.CounterSteer;

                antiRollFrontHorizontal = movement.AntiRollFH;
                antiRollRearHorizontal = movement.AntiRollRH;
                antiRollVertical = movement.AntiRollV;

                maxSpeed = movement.Speed;

                repairEnabled = ins.configData.Repair.Enabled;

                entity.mountPoints[0].mountable.canWieldItems = !ins.configData.ActiveItems.DisableDriver;

                for (int i = 1; i < entity.mountPoints.Count; i++)                
                    entity.mountPoints[i].mountable.canWieldItems = !ins.configData.ActiveItems.DisablePassengers;                
            }

            private void InitializeFuel()
            {
                var fuel = ins.configData.Fuel;
                fuelId = ins.fuelType;
                fuelEnabled = fuel.Enabled;
                consumptionRate = fuel.Consumption;

                if (fuelEnabled)
                {
                    fuelTank = new ItemContainer();
                    fuelTank.ServerInitialize(null, 1);
                    if ((int)fuelTank.uid == 0)
                        fuelTank.GiveUID();
                    fuelTank.onlyAllowedItems = new ItemDefinition[] { ItemManager.itemList.Find(x => x.itemid == fuelId) };

                    if (fuel.GiveFuel && !ins.storedData.HasRestoreData(entity.net.ID))
                    {
                        Item fuelItem = ItemManager.CreateByItemID(fuelId, UnityEngine.Random.Range(fuel.FuelAmountMin, fuel.FuelAmountMax));
                        fuelItem.MoveToContainer(fuelTank);
                    }
                }

                hasFuel = fuelEnabled ? GetFuelAmount() < 1 : false;
            }

            private void InitializeInventory()
            {
                if (ins.configData.Inventory.Enabled)
                {
                    container = GameManager.server.CreateEntity(boxPrefab, entity.transform.position, entity.transform.rotation) as StorageContainer;
                    container.enableSaving = false;                    
                    container.skinID = (ulong)1195832261;
                    container.Spawn();

                    Destroy(container.GetComponent<DestroyOnGroundMissing>());
                    Destroy(container.GetComponent<GroundWatch>());
                    Destroy(container.GetComponent<BoxCollider>());

                    container.SetParent(entity);
                    container.transform.localPosition = new Vector3(0, 0.475f, -2.15f);
                    container.transform.localRotation = Quaternion.Euler(-10, 180, 0);

                    
                    container.panelName = "generic";
                    container.inventorySlots = ins.configData.Inventory.Size;
                    container.isLockable = ins.configData.Security.Trunk.CanLock;
                    container.pickup.enabled = false;

                    container.inventory = new ItemContainer();
                    container.inventory.ServerInitialize(null, ins.configData.Inventory.Size);
                    if ((int)container.inventory.uid == 0)
                        container.inventory.GiveUID();
                }

                if (ins.storedData.HasRestoreData(entity.net.ID))
                {
                    ins.storedData.RestoreVehicle(this);
                    ins.saveableCars.Add(this);
                }
            }    
            
            private void InitializeDecay()
            {
                decay = ins.configData.Decay;
                StartDecayTimer();
            }
            #endregion
            
            #region Input
            private void FixedUpdate()
            {               
                if (WaterLevel.Factor(entity.WorldSpaceBounds().ToBounds()) > 0.7f)
                {
                    isDieing = true;
                    enabled = false;

                    if (externallyManaged)
                        Interface.CallHook("OnVehicleUnderwater", entity);
                    else StopToDie();
                    return;
                }

                CheckUpsideDown();
                hasFuel = fuelEnabled ? HasFuel(false) : true;

                BasePlayer player = Commander;
                                
                if (useDefaultHandling)
                {
                    if (player == null || !hasFuel || !ignitionOn)                    
                        entity.NoDriverInput();

                    DoSteering();
                    ApplyForceAtWheels();

                    if (player?.serverInput.WasJustPressed(cb[CommandType.Lights]) ?? false)
                        ToggleLights();

                    entity.SetFlag(BaseEntity.Flags.Reserved1, entity.IsMounted() && hasFuel && ignitionOn, false);
                    entity.SetFlag(BaseEntity.Flags.Reserved2, (!entity.IsMounted() ? false : lightsOn), false);
                                        
                    if (fuelEnabled && ignitionOn)
                        UseFuel(Time.deltaTime * (entity.gasPedal == 0 ? 0.0333f : 1f));
                }
                else
                {
                    if (player == null || !ignitionOn)
                    {
                        accelInput = 0f;
                        brakeInput = 0.5f;
                        eBrake = true;
                    }
                    else
                    {                        
                        rb.drag = rb.velocity.magnitude / 250;

                        if (player.serverInput.WasJustPressed(cb[CommandType.Lights]))
                            ToggleLights();

                        eBrake = player.serverInput.IsDown(cb[CommandType.Handbrake]);

                        if (player.serverInput.IsDown(cb[CommandType.Accelerate]))
                        {
                            accelInput = 1f;
                            brakeInput = 0f;
                        }
                        else if (player.serverInput.IsDown(cb[CommandType.Brake]))
                        {
                            brakeInput = 1f;
                            accelInput = 0f;
                        }
                        else
                        {
                            brakeInput = 0f;
                            accelInput = 0f;
                        }

                        steerInput = player.serverInput.IsDown(cb[CommandType.Left]) ? -1f : player.serverInput.IsDown(cb[CommandType.Right]) ? 1f : 0f;

                        if (fuelEnabled && ignitionOn)
                            UseFuel(Time.deltaTime * (accelInput == 0 ? 0.0333f : 1f));
                    }

                    speed = rb.velocity.magnitude * 3.6f;

                    ApplyAcceleration();
                    AntiRollBars();
                    CheckForDrift();
                    AdjustSteering();

                    entity.SetFlag(BaseEntity.Flags.Reserved1, player != null && hasFuel && ignitionOn, false);
                }
            }
            #endregion

            #region Movement
            #region Default Handling
            private void ApplyForceAtWheels()
            {
                if (entity.rigidBody == null)                
                    return;
                
                Vector3 vector3 = entity.rigidBody.velocity;
                float single = vector3.magnitude * Vector3.Dot(vector3.normalized, base.transform.forward);
                float single1 = entity.brakePedal;
                float single2 = entity.gasPedal;
                if (single > 0f && single2 < 0f)
                {
                    single1 = 100f;
                }
                else if (single < 0f && single2 > 0f)
                {
                    single1 = 100f;
                }
                BasicCar.VehicleWheel[] vehicleWheelArray = entity.wheels;
                for (int i = 0; i < vehicleWheelArray.Length; i++)
                {
                    BasicCar.VehicleWheel vehicleWheel = vehicleWheelArray[i];
                    if (vehicleWheel.wheelCollider.isGrounded)
                    {
                        if (vehicleWheel.powerWheel)
                        {
                            vehicleWheel.wheelCollider.motorTorque = single2 * entity.motorForceConstant;
                        }
                        if (vehicleWheel.brakeWheel)
                        {
                            vehicleWheel.wheelCollider.brakeTorque = single1 * entity.brakeForceConstant;
                        }
                    }
                }
                entity.SetFlag(BaseEntity.Flags.Reserved3, (single1 < 100f ? false : entity.IsMounted()), false);
            }
            
            private void DoSteering()
            {
                BasicCar.VehicleWheel[] vehicleWheelArray = entity.wheels;
                for (int i = 0; i < vehicleWheelArray.Length; i++)
                {
                    BasicCar.VehicleWheel vehicleWheel = vehicleWheelArray[i];
                    if (vehicleWheel.steerWheel)
                    {
                        vehicleWheel.wheelCollider.steerAngle = entity.steering;
                    }
                }
                entity.SetFlag(BaseEntity.Flags.Reserved4, entity.steering < -2f, false);
                entity.SetFlag(BaseEntity.Flags.Reserved5, entity.steering > 2f, false);
            }
            #endregion

            private void ApplyAcceleration()
            {
                if (accelInput > 0 && speed > maxSpeed)
                    accelInput = 0;

                float velocity = rb.velocity.magnitude * Vector3.Dot(rb.velocity.normalized, entity.transform.forward);

                float _motorTorque = hasFuel ? engineTorque * accelInput : 0;
                float _reverseTorque = hasFuel ? -reverseTorque : 0;
                float _brakeTorque = brakeTorque * brakeInput;

                if (velocity < 0.01f && brakeInput > 0.5f)
                {
                    for (int i = 0; i < allWheels.Length; i++)
                    {
                        WheelCollider wc = allWheels[i];
                        wc.brakeTorque = 0;
                        if (i < 2)
                            wc.motorTorque = _reverseTorque * 0.8f;
                        else wc.motorTorque = _reverseTorque;
                    } 
                }
                else
                {
                    if (eBrake)
                    {
                        for (int i = 2; i < allWheels.Length; i++)
                        {
                            WheelCollider wc = allWheels[i];
                            wc.motorTorque = 0;
                            wc.brakeTorque = brakeTorque;

                            if (steerInput != 0 && allWheels[i].isGrounded)
                            {
                                rb.angularVelocity = new Vector3(rb.angularVelocity.x, rb.angularVelocity.y + (steerInput / 60f), rb.angularVelocity.z);
                                wc.sidewaysFriction = sidewaysFrictionHB;
                                driftFriction = true;
                            }
                        }                       
                    }
                    else
                    {
                        for (int i = 0; i < allWheels.Length; i++)
                        {
                            WheelCollider wc = allWheels[i];
                            if (i > 1 && driftFriction)
                            {
                                wc.sidewaysFriction = sidewaysFriction;
                                driftFriction = false;
                            }
                            wc.motorTorque = _motorTorque;
                            wc.brakeTorque = _brakeTorque;                            
                        }
                    }

                    entity.SetFlag(BaseEntity.Flags.Reserved3, Commander == null ? false : (velocity > 0f && accelInput < 0f) || (velocity < 0f && brakeInput > 0f), false);
                }
            }

            private void AdjustSteering()
            {               
                var steerAngle = Mathf.Lerp(maxSteeringAngle, maxSteeringAngleSpeed, (speed / maxSpeed)) * steerInput;

                if (applyCounterSteer)
                    steerAngle = Mathf.Clamp((steerAngle * (steerInput + driftAngle)), -steerAngle, steerAngle);

                allWheels[0].steerAngle = steerAngle;
                allWheels[1].steerAngle = steerAngle;

                entity.SetFlag(BaseEntity.Flags.Reserved4, steerInput == -1f, false);
                entity.SetFlag(BaseEntity.Flags.Reserved5, steerInput == 1f, false);
            }

            private void AntiRollBars()
            {                
                WheelHit FrontWheelHit;

                float travelFL = 1.0f;
                float travelFR = 1.0f;

                bool groundedFL = allWheels[0].GetGroundHit(out FrontWheelHit);

                if (groundedFL)
                    travelFL = (-allWheels[0].transform.InverseTransformPoint(FrontWheelHit.point).y - allWheels[0].radius) / allWheels[0].suspensionDistance;

                bool groundedFR = allWheels[1].GetGroundHit(out FrontWheelHit);

                if (groundedFR)
                    travelFR = (-allWheels[1].transform.InverseTransformPoint(FrontWheelHit.point).y - allWheels[1].radius) / allWheels[1].suspensionDistance;

                float antiRollForceFrontHorizontal = (travelFL - travelFR) * antiRollFrontHorizontal;

                if (groundedFL)
                    rb.AddForceAtPosition(allWheels[0].transform.up * -antiRollForceFrontHorizontal, allWheels[0].transform.position);
                if (groundedFR)
                    rb.AddForceAtPosition(allWheels[1].transform.up * antiRollForceFrontHorizontal, allWheels[1].transform.position);

                WheelHit RearWheelHit;

                float travelRL = 1.0f;
                float travelRR = 1.0f;

                bool groundedRL = allWheels[2].GetGroundHit(out RearWheelHit);

                if (groundedRL)
                    travelRL = (-allWheels[2].transform.InverseTransformPoint(RearWheelHit.point).y - allWheels[2].radius) / allWheels[2].suspensionDistance;

                bool groundedRR = allWheels[3].GetGroundHit(out RearWheelHit);

                if (groundedRR)
                    travelRR = (-allWheels[3].transform.InverseTransformPoint(RearWheelHit.point).y - allWheels[3].radius) / allWheels[3].suspensionDistance;

                float antiRollForceRearHorizontal = (travelRL - travelRR) * antiRollRearHorizontal;

                if (groundedRL)
                    rb.AddForceAtPosition(allWheels[2].transform.up * -antiRollForceRearHorizontal, allWheels[2].transform.position);
                if (groundedRR)
                    rb.AddForceAtPosition(allWheels[3].transform.up * antiRollForceRearHorizontal, allWheels[3].transform.position);
                                
                float antiRollForceFrontVertical = (travelFL - travelRL) * antiRollVertical;

                if (groundedFL)
                    rb.AddForceAtPosition(allWheels[0].transform.up * -antiRollForceFrontVertical, allWheels[0].transform.position);
                if (groundedRL)
                    rb.AddForceAtPosition(allWheels[2].transform.up * antiRollForceFrontVertical, allWheels[2].transform.position);

                float antiRollForceRearVertical = (travelFR - travelRR) * antiRollVertical;

                if (groundedFR)
                    rb.AddForceAtPosition(allWheels[1].transform.up * -antiRollForceRearVertical, allWheels[1].transform.position);
                if (groundedRR)
                    rb.AddForceAtPosition(allWheels[3].transform.up * antiRollForceRearVertical, allWheels[3].transform.position);                                
            }

            private void CheckForDrift()
            {
                WheelHit hit;
                allWheels[3].GetGroundHit(out hit);

                if (speed > 1f && isDrifting && !eBrake)
                    driftAngle = hit.sidewaysSlip * 1f;
                else
                    driftAngle = 0f;

                if (Mathf.Abs(hit.sidewaysSlip) > .25f)
                    isDrifting = true;
                else
                    isDrifting = false;

            }

            private void CheckUpsideDown()
            {
                if (externallyManaged)
                    return;

                if (Vector3.Dot(entity.transform.up, Vector3.down) > 0)
                {
                    if (!isFlipped)
                    {
                        upsideDownTime += Time.deltaTime;

                        if (upsideDownTime > 3f)
                            OnCarFlipped();
                    }
                }
                else
                {
                    upsideDownTime = 0;
                    if (isFlipped)
                        isFlipped = false;
                }
            }

            private void OnCarFlipped()
            {
                isFlipped = true;
                BasePlayer player = Commander;
                if (ins.configData.Repair.CanFlip && player != null)
                    player.ChatMessage(ins.msg("carFlipped", player.UserIDString));
                EjectAllPlayers();
            }

            public void ResetCar()
            {
                entity.GetComponent<Rigidbody>().velocity = Vector3.zero;
                entity.transform.rotation = Quaternion.Euler(0, entity.transform.eulerAngles.y, 0);
                entity.SendNetworkUpdate();
                isFlipped = false;
            }
            #endregion

            #region Collision           
            private void OnCollisionEnter(Collision col)
            {
                if (!collision.Enabled)
                    return;
                
                bool damage = false;
                foreach(var cp in col.contacts)
                {
                    float local = cp.point.y - entity.transform.position.y;
                    if (local > 0.4f)
                    {
                        damage = true;
                        break;
                    }                    
                }

                if (damage)
                {
                    float force = col.relativeVelocity.magnitude;
                    if (force > 2)
                        entity.Hurt(new HitInfo(null, entity, DamageType.Explosion, force * collision.Multiplier));
                }
            }
            #endregion

            #region Decay
            private void StartDecayTimer()
            {
                if (decay.Enabled && occupants.Count == 0)
                    InvokeHandler.Invoke(this, DealDecayDamage, decay.Time);
            }

            private void DealDecayDamage()
            {
                if (entity.IsOutside())
                {
                    float decayDamage = entity.MaxHealth() * (decay.Amount / 100);

                    if (decayDamage >= entity.health)
                    {
                        entity.Die();
                        return;
                    }
                    else entity.Hurt(decayDamage, DamageType.Decay, null, false);                                     
                }
                InvokeHandler.Invoke(this, DealDecayDamage, decay.Time);
            }
            #endregion

            #region Functions
            private void ToggleLights()
            {
                lightsOn = !lightsOn;
                entity.SetFlag(BaseEntity.Flags.Reserved2, lightsOn, false);
            }

            private int GetFuelAmount() => fuelTank.GetAmount(fuelId, true);

            private bool HasFuel(bool forceCheck = false)
            {
                if (Time.time > nextFuelCheckTime || forceCheck)
                {
                    hasFuel = (float)GetFuelAmount() > 0f;
                    nextFuelCheckTime = Time.time + UnityEngine.Random.Range(1f, 2f);

                    if (Commander != null)
                        ins.CreateFuelUI(Commander, this);
                }
                return hasFuel;
            }

            private void UseFuel(float seconds)
            {                
                Item slot = fuelTank.GetSlot(0);
                if (slot == null || slot.amount < 1)                
                    return;
                
                pendingFuel = pendingFuel + seconds * consumptionRate;
                if (pendingFuel >= 1f)
                {
                    int num = Mathf.FloorToInt(pendingFuel);
                    slot.UseItem(num);
                    pendingFuel -= (float)num;
                }
                return;
            }

            public bool HasCommander() => Commander != null;
           
            public void OnDriverMounted(BasePlayer player)
            {
                string message = useDefaultHandling ? string.Format(ins.msg("controls2", player.UserIDString), cb[CommandType.Accelerate], cb[CommandType.Brake], cb[CommandType.Left], cb[CommandType.Right], cb[CommandType.Lights]) : string.Format(ins.msg("controls1", player.UserIDString), cb[CommandType.Accelerate], cb[CommandType.Brake], cb[CommandType.Left], cb[CommandType.Right], cb[CommandType.Handbrake], cb[CommandType.Lights]);

                if (container != null)
                    message += $"\n{ins.msg("access_inventory1", player.UserIDString)}";

                if (fuelEnabled)
                {
                    message += $"\n{string.Format(ins.msg("access_fuel", player.UserIDString), cb[CommandType.FuelTank])}";
                    message += $"\n{string.Format(ins.msg("fuel_type", player.UserIDString), ins.fuelTypeName)}";
                    ins.CreateFuelUI(player, this);
                }
                if (repairEnabled)
                    message += $"\n{string.Format(ins.msg("repairhelp", player.UserIDString), ins.configData.Repair.Amount, ins.repairTypeName)}";

                player.ChatMessage(message);
                CheckIgnitionSystems();
            }

            public void OnPassengerMounted(BasePlayer player)
            {
                string message = string.Empty;

                if (container != null)
                    message += ins.msg("access_inventory1", player.UserIDString);

                if (fuelEnabled)
                {
                    message += $"\n{string.Format(ins.msg("access_fuel", player.UserIDString), cb[CommandType.FuelTank])}";
                    message += $"\n{string.Format(ins.msg("fuel_type", player.UserIDString), ins.fuelTypeName)}";
                }
                if (repairEnabled)
                    message += $"\n{string.Format(ins.msg("repairhelp", player.UserIDString), ins.configData.Repair.Amount, ins.repairTypeName)}";

                if (!string.IsNullOrEmpty(message))
                    player.ChatMessage(message);
            }
            #endregion

            #region Security
            private void CheckIgnitionSystems()
            {
                if (security.Enabled && !hasBeenHotwired)
                {
                    BasePlayer player = Commander;                   
                    if (ignitionCode == 0)
                    {
                        ignitionCode = UnityEngine.Random.Range(100, int.MaxValue);

                        if (security.Ignition.KeyOnEnter && (security.Ignition.KeyChance == 1 || UnityEngine.Random.Range(1, security.Ignition.KeyChance) == 1))
                        {
                            CreateVehicleKey(player);

                            if (ins.configData.Security.Owners)
                                ownerId = player.userID;

                            player.ChatMessage(ins.msg("key_created", player.UserIDString));

                            if (security.Ignition.CanCopy)
                                player.ChatMessage(ins.msg("key_copy", player.UserIDString));
                            ignitionOn = true;
                        }
                        else CheckIgnitionSystems();
                    }
                    else
                    {
                        if (!HasVehicleKey(player))
                        {
                            if (security.Hotwire.Enabled)
                            {
                                player.ChatMessage(ins.msg("no_key_hotwire", player.UserIDString));
                                ignitionOn = false;
                            }
                            else
                            {
                                player.ChatMessage(ins.msg("no_key", player.UserIDString));
                                ignitionOn = false;
                            }
                        }
                        else ignitionOn = true;
                    }
                }
                else ignitionOn = true;
            }

            public void CreateVehicleKey(BasePlayer player)
            {                
                Item keyItem = ItemManager.CreateByItemID(946662961, 1, 0);
                keyItem.instanceData = new ProtoBuf.Item.InstanceData
                {
                    dataInt = ignitionCode,
                    ShouldPool = false
                };

                player.GiveItem(keyItem, BaseEntity.GiveItemReason.Crafted);                
            }

            public bool HasVehicleKey(BasePlayer keyHolder)
            {
                if (ins.configData.Security.Owners && keyHolder.userID == ownerId)
                    return true;

                List<Item> items = keyHolder.inventory.FindItemIDs(946662961);
                foreach (Item item in items)
                {
                    if (!IsCorrectKey(item))
                        continue;
                    return true;
                }
                return false;
            }

            private bool IsCorrectKey(Item key)
            {
                if (key.instanceData == null || key.instanceData.dataInt != ignitionCode)                
                    return false;               
                return true;
            }

            public void OnVehicleHotwired()
            {
                ownerId = 0UL;
                hasBeenHotwired = true;
                ignitionOn = true;
            }
            #endregion

            #region Damage and Destruction
            private void OnDestroy()
            {
                EjectAllPlayers();

                if (container != null)
                {
                    container.inventory?.Clear();

                    if (!container.IsDestroyed)
                        container.Kill();
                }

                if (entity != null && !entity.IsDestroyed && !entity.enableSaving)
                    entity.Kill();
            }

            public void EjectAllPlayers()
            {                
                ignitionOn = false;

                for (int i = occupants.Count - 1; i >= 0; i--)
                {
                    BasePlayer occupant = occupants.ElementAt(i);
                    BaseMountable mountable = occupant.GetMounted();

                    DismountPlayer(occupant, mountable);

                    if(occupant != null)
                        ins.DestroyAllUI(occupant);
                }
            }           
          
            public void ManageDamage(HitInfo info)
            {
                if (isDieing)
                {
                    ins.NullifyDamage(info);
                    return;
                }

                if (info.damageTypes.GetMajorityDamageType() == DamageType.Bullet)
                    info.damageTypes.ScaleAll(200);

                if (info.damageTypes.Total() >= entity.health)
                {
                    isDieing = true;
                    ins.NullifyDamage(info);
                    OnDeath();
                    return;
                }
              
                foreach (var occupant in occupants)
                    ins.CreateHealthUI(occupant, this);
            }

            public void StopToDie()
            {
                enabled = false;
                isDieing = true;

                if (entity != null)
                {
                    entity.SetFlag(BaseEntity.Flags.Reserved1, false, false);

                    foreach (var wheel in entity.wheels)
                    {
                        wheel.wheelCollider.motorTorque = 0;
                        wheel.wheelCollider.brakeTorque = float.MaxValue;
                    }

                    rb.velocity = Vector3.zero;
                } 

                EjectAllPlayers();
                InvokeHandler.Invoke(this, OnDeath, 5f);
            }
           
            private void OnDeath()
            {
                enabled = false;

                EjectAllPlayers();

                if (ins.configData.Death.Enabled && ins.configData.Death.Amount > 0)                
                    RadiusDamage(ins.configData.Death.Amount, ins.configData.Death.Radius, transform.position);                

                Effect.server.Run(explosionPrefab, transform.position);                

                if (ins.configData.Inventory.DropInv)
                {
                    if (container != null)
                        container.inventory.Drop("assets/prefabs/misc/item drop/item_drop.prefab", transform.position + Vector3.up + (UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(2f, 3f)), new Quaternion());
                    if (fuelTank != null)
                        fuelTank.Drop("assets/prefabs/misc/item drop/item_drop.prefab", transform.position + Vector3.up + (UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(2f, 3f)), new Quaternion());
                }

                ins.NextTick(() =>
                {
                    if (entity != null && !entity.IsDestroyed)
                        entity.DieInstantly();
                    Destroy(this);
                });
            }

            private void RadiusDamage(float amount, float radius, Vector3 position)
            {
                List<BaseCombatEntity> entities = Facepunch.Pool.GetList<BaseCombatEntity>();
                Vis.Entities(position, radius, entities);

                if (entities.Count > 0)
                {
                    for (int i = entities.Count - 1; i >= 0; i--)
                    {
                        BaseCombatEntity baseCombatEntity = entities[i];                     
                        if (baseCombatEntity == null || baseCombatEntity == entity || (baseCombatEntity.GetParentEntity() != null && baseCombatEntity.GetParentEntity() == entity))
                            continue;

                        float distance = Vector3.Distance(position, baseCombatEntity.transform.position);
                        baseCombatEntity.Hurt(amount * (1 - (distance / radius)), DamageType.Explosion, null);
                    }
                }

                Facepunch.Pool.FreeList(ref entities);
            }
            #endregion

            #region External Toggles
            public void SetExternallyManaged()
            {
                externallyManaged = true;
                repairEnabled = false;
                DisableFuelConsumption();
                DisableSecuritySettings();
                DisableCollisionSettings();
            }

            public void SetFeatures(bool repairEnabled, bool disableFuel, bool disableSecurity, bool disableCollision)
            {
                this.repairEnabled = repairEnabled;
                if (disableFuel)
                    DisableFuelConsumption();

                if (disableSecurity)
                    DisableSecuritySettings();

                if (disableCollision)
                    DisableCollisionSettings();
            }            

            public void DisableFuelConsumption()
            {
                if (fuelEnabled)
                {
                    fuelEnabled = false;
                    hasFuel = true;

                    foreach (var occupant in occupants)
                        ins.DestroyUI(occupant, uiFuel);
                }
            }

            public void DisableSecuritySettings()
            {                
                security.Enabled = false;
                security.Hotwire.Enabled = false;
                security.Trunk.CanLock = false;
            }

            public void DisableCollisionSettings()
            {
                collision.Enabled = false;
            }
            #endregion

            #region Mounting
            public void OnEntityMounted(BasePlayer player, bool isDriver)
            {
                occupants.Add(player);

                ins.CreateHealthUI(player, this);

                if (isDriver)                
                    OnDriverMounted(player);                
                else OnPassengerMounted(player);                

                if (InvokeHandler.IsInvoking(this, DealDecayDamage))
                    InvokeHandler.CancelInvoke(this, DealDecayDamage);
            }

            // Temporary solution for dismounting players until death on fail is fixed
            public void DismountPlayer(BasePlayer player, BaseMountable mountable)
            {
                if (player == null)
                    return;

                Vector3 dismountPosition = GetDismountPosition(mountable);

                player.DismountObject();

                player.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                player.MovePosition(dismountPosition);
                player.SendNetworkUpdateImmediate(false);
                player.ClientRPCPlayer(null, player, "ForcePositionTo", dismountPosition);
                mountable._mounted = null;
                entity.SetFlag(BaseEntity.Flags.Busy, false, false);
                player.EnsureDismounted();

                ins.OnEntityDismounted(mountable, player);
                //OnEntityDismounted(player, mountable);
            }

            private Vector3 GetDismountPosition(BaseMountable mountable)
            {
                int index = entity.mountPoints.Select(x => x.mountable).ToList().IndexOf(mountable);
                Vector3 dismountLocal = dismountLocals[index];

                Vector3 dismountPosition = mountable.transform.position + (mountable.transform.right * dismountLocal.x) + (mountable.transform.up * 0.1f) + (mountable.transform.forward * dismountLocal.z);

                if (TerrainMeta.HeightMap.GetHeight(dismountPosition) > dismountPosition.y)
                    dismountPosition.y = TerrainMeta.HeightMap.GetHeight(dismountPosition) + 0.5f;

                if (!Physics.CheckCapsule(dismountPosition + new Vector3(0f, 0.41f, 0f), dismountPosition + new Vector3(0f, 1.39f, 0f), 0.5f, LayerMask.GetMask("Construction", "Default", "Vehicle_Movement", "World")))
                {
                    Vector3 vector3 = dismountPosition + new Vector3(0f, 0.9f, 0f);
                    if (mountable.IsVisible(vector3) && !Physics.Linecast(mountable.transform.position + new Vector3(0f, 1f, 0f), vector3, 1075904513))
                        return dismountPosition;
                }
                return mountable.transform.position + new Vector3(0f, 1.5f, 0f);
            }
            // End of temporary solution

            public void OnEntityDismounted(BasePlayer player, BaseMountable mountable)
            {
                occupants.Remove(player);
                ins.DestroyAllUI(player);

                if (mountable != null && entity.mountPoints[0].mountable == mountable)                
                    ignitionOn = false;                

                if (occupants.Count == 0)
                    StartDecayTimer();
            }      
            #endregion
        }

        class HotwireManager
        {
            public BasePlayer player;
            public CarController controller;
            public Timer hwTimer;

            public HotwireManager() { }

            public HotwireManager(BasePlayer player, CarController controller)
            {
                this.player = player;
                this.controller = controller;
                BeginHotwire();
            }

            private void BeginHotwire()
            {
                int chance = ins.configData.Security.Hotwire.Chance;
                int time = ins.configData.Security.Hotwire.Time;

                hwTimer = ins.timer.In(ins.configData.Security.Hotwire.Time, () =>
                {
                    if (player == null || controller == null)
                        return;

                    if (controller.Commander != player)                    
                        player.ChatMessage(ins.msg("hotwire_fail_left", player.UserIDString));
                    else
                    {
                        if (UnityEngine.Random.Range(1, ins.configData.Security.Hotwire.Chance) == 1)
                        {
                            controller.OnVehicleHotwired();
                            player.ChatMessage(ins.msg("hotwire_success", player.UserIDString));
                        }
                        else
                        {
                            if (ins.configData.Security.Hotwire.DealDamage)
                            {
                                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", player.transform.position, new Vector3(), null, true);
                                player.Hurt(15f, DamageType.ElectricShock, controller.entity, false);
                            }
                            player.ChatMessage(ins.msg("hotwire_fail", player.UserIDString));
                        }
                    }
                    ins.isHotwiring.Remove(player.userID);
                });
            }

            public void CancelHotwire()
            {
                if (hwTimer != null)
                    hwTimer.Destroy();

                ins.isHotwiring.Remove(player.userID);
            }
        }
        #endregion

        #region UI
        #region UI Elements
        public static class UI
        {
            static public CuiElementContainer ElementContainer(string panelName, string color, UI4 dimensions, bool useCursor = false, string parent = "Overlay")
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return NewElement;
            }
            static public void Panel(ref CuiElementContainer container, string panel, string color, UI4 dimensions, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void Label(ref CuiElementContainer container, string panel, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text, Font = "droidsansmono.ttf" },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                },
                panel);

            }
            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        public class UI4
        {
            public float xMin, yMin, xMax, yMax;
            public UI4(float xMin, float yMin, float xMax, float yMax)
            {
                this.xMin = xMin;
                this.yMin = yMin;
                this.xMax = xMax;
                this.yMax = yMax;
            }
            public string GetMin() => $"{xMin} {yMin}";
            public string GetMax() => $"{xMax} {yMax}";
        }
        #endregion

        #region UI Creation        
        private void CreateHealthUI(BasePlayer player, CarController controller)
        {
            var opt = configData.UI.Health;
            if (!opt.Enabled)
                return;

            var container = UI.ElementContainer(uiHealth, UI.Color(opt.Color1, opt.Color1A), new UI4(opt.Xmin, opt.YMin, opt.XMax, opt.YMax));
            UI.Label(ref container, uiHealth, msg("health", player.UserIDString), 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft);
            var percentHealth = System.Convert.ToDouble((float)controller.entity.health / (float)controller.entity.MaxHealth());
            float yMaxHealth = 0.25f + (0.73f * (float)percentHealth);
            UI.Panel(ref container, uiHealth, UI.Color(opt.Color2, opt.Color2A), new UI4(0.25f, 0.1f, yMaxHealth, 0.9f));
            DestroyUI(player, uiHealth);
            CuiHelper.AddUi(player, container);
        }

        private void CreateFuelUI(BasePlayer player, CarController controller)
        {
            if (configData.Fuel.Enabled)
            {
                var opt = configData.UI.Fuel;
                if (!opt.Enabled)
                    return;

                int fuelAmount = controller.fuelTank.GetAmount(fuelType, false);
                var container = UI.ElementContainer(uiFuel, UI.Color(opt.Color1, opt.Color1A), new UI4(opt.Xmin, opt.YMin, opt.XMax, opt.YMax));
                UI.Label(ref container, uiFuel, string.Format(msg("fuel", player.UserIDString), $"<color={opt.Color2}>{(fuelAmount == 0 ? msg("fuel_empty", player.UserIDString) : fuelAmount.ToString())}</color>"), 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft);
                DestroyUI(player, uiFuel);
                CuiHelper.AddUi(player, container);
            }
        }

        private void DestroyUI(BasePlayer player, string panel) => CuiHelper.DestroyUi(player, panel);

        private void DestroyAllUI(BasePlayer player)
        {
            DestroyUI(player, uiHealth);
            DestroyUI(player, uiFuel);
        }
        #endregion
        #endregion

        #region Feature Commands
        [ChatCommand("copykey")]
        void cmdCopyKey(BasePlayer player, string command, string[] args)
        {
            if (!configData.Security.Enabled || !configData.Security.Ignition.CanCopy)
                return;

            if (player.isMounted)
            {
                BaseMountable mountable = player.GetMounted();
                if (mountable != null)
                {
                    CarController controller = mountable.VehicleParent()?.GetComponent<CarController>();
                    if (controller != null && controller.Commander == player)
                    {
                        if (!controller.HasVehicleKey(player))
                            SendReply(player, msg("not_has_key", player.UserIDString));
                        else
                        {
                            controller.CreateVehicleKey(player);
                            SendReply(player, msg("key_copied", player.UserIDString));
                        }
                        return;
                    }
                }
            }
            SendReply(player, msg("not_commander", player.UserIDString));
        }

        [ChatCommand("hotwire")]
        void cmdHotwire(BasePlayer player, string command, string[] args)
        {
            if (!configData.Security.Enabled || !configData.Security.Hotwire.Enabled)
                return;

            if (player.isMounted)
            {
                BaseMountable mountable = player.GetMounted();
                if (mountable != null)
                {
                    CarController controller = mountable.VehicleParent()?.GetComponent<CarController>();
                    if (controller != null && controller.Commander == player)
                    {                      
                        if (controller.HasBeenHotwired)
                        {
                            SendReply(player, msg("already_hotwired", player.UserIDString));
                            return;
                        }

                        if (controller.HasVehicleKey(player))
                        {
                            SendReply(player, msg("has_key", player.UserIDString));
                            return;
                        }

                        if (isHotwiring.ContainsKey(player.userID))
                        {
                            SendReply(player, msg("already_hotwiring", player.UserIDString));
                            return;
                        }

                        isHotwiring.Add(player.userID, new HotwireManager(player, controller));
                        SendReply(player, string.Format(msg("begun_hotwiring", player.UserIDString), configData.Security.Hotwire.Time, configData.Security.Hotwire.Chance));
                        return;
                    }
                }
            }
            SendReply(player, msg("not_commander", player.UserIDString));
        }
        #endregion

        #region Commands        
        [ChatCommand("spawncar")]
        private void cmdSpawnCar(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "carcommander.canspawn")) return;            

            RaycastHit hit;
            if (Physics.SphereCast(player.eyes.position, 0.1f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 20f))
            {
                BaseEntity entity = hit.GetEntity();
                if (entity != null && entity is BasicCar)
                {
                    SendReply(player, msg("noStacking", player.UserIDString));
                    return;
                }
            }

            if (configData.Spawnable.ApplyOwnershipOnSpawn && configData.Spawnable.MaxPerPlayer > 0)
            {
                if (CountPlayerVehicles(player.userID) >= configData.Spawnable.MaxPerPlayer)
                {
                    SendReply(player, msg("maxActiveVehicles", player.UserIDString));
                    return;
                }
            }

            if (configData.Spawnable.DisableBuildingBlock && player.IsBuildingBlocked())
            {
                SendReply(player, msg("buildingBlocked", player.UserIDString));
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, "carcommander.ignorecooldown"))
            {
                double time = GrabCurrentTime();
                if (!userCooldowns.ContainsKey(player.userID))
                    userCooldowns.Add(player.userID, time + configData.Spawnable.Cooldown);
                else
                {
                    double nextUseTime = userCooldowns[player.userID];
                    if (nextUseTime > time)
                    {
                        SendReply(player, string.Format(msg("onCooldown", player.UserIDString), FormatTime(nextUseTime - time)));
                        return;
                    }
                    else userCooldowns[player.userID] = time + configData.Spawnable.Cooldown;
                }
            }

            Vector3 position = player.eyes.position + (player.eyes.MovementForward() * 5f);

            float y = TerrainMeta.HeightMap.GetHeight(position);
            if (y > position.y)
                position.y = y;

            BaseEntity e = SpawnAtLocation(position, Quaternion.Euler(0, player.eyes.rotation.eulerAngles.y - 90f, 0), true);

            if (configData.Spawnable.ApplyOwnershipOnSpawn && configData.Security.Owners)
            {
                CarController controller = e.GetComponent<CarController>();
                controller.ownerId = player.userID;
            }

        }

        [ChatCommand("admincar")]
        private void cmdAdminCar(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "carcommander.admin")) return;
                   
            RaycastHit hit;
            if (Physics.SphereCast(player.eyes.position, 0.1f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 20f))
            {
                BaseEntity entity = hit.GetEntity();
                if (entity != null && entity is BasicCar)
                {
                    SendReply(player, msg("noStacking", player.UserIDString));
                    return;
                }
            }

            Vector3 position = player.eyes.position + (player.eyes.MovementForward() * 5f);

            float y = TerrainMeta.HeightMap.GetHeight(position);
            if (y > position.y)
                position.y = y;

            SpawnAtLocation(position, Quaternion.Euler(0, player.eyes.rotation.eulerAngles.y - 90f, 0), (args.Length == 1 && args[0].ToLower() == "save"));
        }

        [ChatCommand("clearcars")]
        private void cmdClearCars(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "carcommander.admin")) return;

            for (int i = saveableCars.Count - 1; i >= 0; i--)
            {
                var car = saveableCars[i];
                if (car != null && car.entity != null && !car.entity.IsDestroyed)
                    car.StopToDie();
            }

            for (int i = temporaryCars.Count - 1; i >= 0; i--)
            {
                var car = temporaryCars[i];
                if (car != null && car.entity != null && !car.entity.IsDestroyed)
                    car.StopToDie();
            }

            saveableCars.Clear();
            temporaryCars.Clear();
            SaveData();
        }

        [ChatCommand("removecar")]
        private void cmdRemoveCars(BasePlayer player, string command, string[] args)
        {
            ServerMgr.Instance.StartCoroutine(DelayedDestroy(player.userID));
            SendReply(player, msg("removingCars", player.UserIDString));
        }

        [ChatCommand("flipcar")]
        private void cmdFlipCar(BasePlayer player, string command, string[] args)
        {
            if (!configData.Repair.CanFlip)
                return;

            RaycastHit hit;
            if (Physics.SphereCast(player.eyes.position, 0.1f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 3))
            {
                BaseEntity entity = hit.GetEntity();
                if (entity != null)
                {
                    CarController controller = entity.GetComponent<CarController>();
                    if (controller != null && controller.IsFlipped)
                    {
                        controller.ResetCar();
                    }
                }
            }                       
        }

        [ChatCommand("buildcar")]
        private void cmdBuildHeli(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "carcommander.canbuild"))
            {
                SendReply(player, msg("nopermissionbuild", player.UserIDString));
                return;
            }

            if (!configData.Build.Enabled)
                return;

            if (configData.Spawnable.ApplyOwnershipOnSpawn && configData.Spawnable.MaxPerPlayer > 0)
            {
                if (CountPlayerVehicles(player.userID) >= configData.Spawnable.MaxPerPlayer)
                {
                    SendReply(player, msg("maxActiveVehicles", player.UserIDString));
                    return;
                }
            }

            if (configData.Spawnable.DisableBuildingBlock && player.IsBuildingBlocked())
            {
                SendReply(player, msg("buildingBlocked", player.UserIDString));
                return;
            }

            if (args.Length > 0)
            {
                if (args[0].ToLower() == "costs")
                {
                    string str = msg("costs", player.UserIDString);
                    foreach (ConfigData.BuildOptions.BuildOption cost in configData.Build.Costs)
                    {
                        ItemDefinition itemDefinition = ItemManager.FindItemDefinition(cost.Shortname);
                        if (itemDefinition == null)
                            continue;

                        str += $"\n- {cost.Amount} x {itemDefinition.displayName.translated}";
                    }
                    SendReply(player, str);                   
                }
                return;
            }
            
            List<ItemCost> requiredItems = new List<ItemCost>();

            foreach (ConfigData.BuildOptions.BuildOption cost in configData.Build.Costs)
            {
                ItemDefinition itemDefinition = ItemManager.FindItemDefinition(cost.Shortname);
                if (itemDefinition == null)
                    continue;

                if (player.inventory.GetAmount(itemDefinition.itemid) < cost.Amount)
                {
                    SendReply(player, string.Format(msg("notenoughres", player.UserIDString), cost.Amount, itemDefinition.displayName.translated));
                    return;
                }

                requiredItems.Add(new ItemCost(itemDefinition.itemid, cost.Amount));
            }

            if (configData.Build.Cooldown)
            {
                if (!permission.UserHasPermission(player.UserIDString, "carcommander.ignorecooldown"))
                {
                    double time = GrabCurrentTime();
                    if (!userCooldowns.ContainsKey(player.userID))
                        userCooldowns.Add(player.userID, time + configData.Spawnable.Cooldown);
                    else
                    {
                        double nextUseTime = userCooldowns[player.userID];
                        if (nextUseTime > time)
                        {
                            SendReply(player, string.Format(msg("onCooldown", player.UserIDString), FormatTime(nextUseTime - time)));
                            return;
                        }
                        else userCooldowns[player.userID] = time + configData.Spawnable.Cooldown;
                    }
                }
            }

            foreach (ItemCost cost in requiredItems)
                player.inventory.Take(null, cost.itemId, cost.amount);

            Vector3 position = player.eyes.position + (player.eyes.MovementForward() * 5f);

            float y = TerrainMeta.HeightMap.GetHeight(position);
            if (y > position.y)
                position.y = y;

            BaseEntity e = SpawnAtLocation(position, Quaternion.Euler(0, player.eyes.rotation.eulerAngles.y - 90f, 0), true);

            if (configData.Spawnable.ApplyOwnershipOnSpawn && configData.Security.Owners)
            {
                CarController controller = e.GetComponent<CarController>();
                controller.ownerId = player.userID;
            }
        }
        #endregion

        #region Console Commands
        [ConsoleCommand("clearcars")]
        private void ccmdClearCars(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null || arg.Args == null)
                return;

            for (int i = saveableCars.Count - 1; i >= 0; i--)
            {
                var car = saveableCars[i];
                if (car != null && car.entity != null && !car.entity.IsDestroyed)
                    car.StopToDie();
            }

            for (int i = temporaryCars.Count - 1; i >= 0; i--)
            {
                var car = temporaryCars[i];
                if (car != null && car.entity != null && !car.entity.IsDestroyed)
                    car.StopToDie();
            }

            saveableCars.Clear();
            temporaryCars.Clear();
            SaveData();
        }

        [ConsoleCommand("spawncar")]
        private void ccmdSpawnCar(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null || arg.Args == null)
                return;

            if (arg.Args.Length == 1 || arg.Args.Length == 2)
            {
                BasePlayer player = covalence.Players.Connected.FirstOrDefault(x => x.Id == arg.GetString(0))?.Object as BasePlayer;
                if (player != null)
                {
                    Vector3 position = player.eyes.position + (player.eyes.MovementForward() * 5f);

                    float y = TerrainMeta.HeightMap.GetHeight(position);
                    if (y > position.y)
                        position.y = y;

                    SpawnAtLocation(position, Quaternion.Euler(0, player.eyes.rotation.eulerAngles.y - 90f, 0), (arg.Args.Length == 2 && arg.Args[1].ToLower() == "save"));
                }
                return;
            }
            if (arg.Args.Length > 2)
            {
                float x;
                float y;
                float z;

                float rotation = 0;

                if (float.TryParse(arg.GetString(0), out x))
                {
                    if (float.TryParse(arg.GetString(1), out y))
                    {
                        if (float.TryParse(arg.GetString(2), out z))
                        {
                            if (arg.Args.Length > 4)
                                float.TryParse(arg.GetString(4), out rotation);

                            SpawnAtLocation(new Vector3(x, y, z), rotation == 0 ? new Quaternion() : Quaternion.Euler(0, rotation, 0), (arg.Args.Length >= 4 && arg.Args[3].ToLower() == "save"));
                            return;
                        }
                    }
                }
                PrintError($"Invalid arguments supplied to spawn a car at position : (x = {arg.GetString(0)}, y = {arg.GetString(1)}, z = {arg.GetString(2)})");
            }
        }
        #endregion

        #region Friends
        private bool AreFriends(ulong playerId, ulong friendId)
        {
            if (Friends && configData.Passengers.UseFriends)
                return (bool)Friends?.Call("AreFriends", playerId.ToString(), friendId.ToString());
            return true;
        }

        private bool IsClanmate(ulong playerId, ulong friendId)
        {
            if (Clans && configData.Passengers.UseClans)
            {
                object playerTag = Clans?.Call("GetClanOf", playerId);
                object friendTag = Clans?.Call("GetClanOf", friendId);
                if (playerTag is string && friendTag is string)
                {
                    if (!string.IsNullOrEmpty((string)playerTag) && !string.IsNullOrEmpty((string)friendTag) && (playerTag == friendTag))
                        return true;
                }
                return false;
            }
            return true;
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Movement Settings")]
            public MovementSettings Movement { get; set; }

            [JsonProperty(PropertyName = "Button Configuration")]
            public ButtonConfiguration Buttons { get; set; }

            [JsonProperty(PropertyName = "Passenger Options")]
            public PassengerOptions Passengers { get; set; }

            [JsonProperty(PropertyName = "Inventory Options")]
            public InventoryOptions Inventory { get; set; }

            [JsonProperty(PropertyName = "Spawnable Options")]
            public SpawnableOptions Spawnable { get; set; }

            [JsonProperty(PropertyName = "Fuel Options")]
            public FuelOptions Fuel { get; set; }

            [JsonProperty(PropertyName = "Repair Options")]
            public RepairSettings Repair { get; set; }

            [JsonProperty(PropertyName = "Death Options")]
            public DeathOptions Death { get; set; }

            [JsonProperty(PropertyName = "Active Item Options")]
            public ActiveItemOptions ActiveItems { get; set; }

            [JsonProperty(PropertyName = "Security Options")]
            public SecurityOptions Security { get; set; }

            [JsonProperty(PropertyName = "Collision Options")]
            public CollisionOptions Collision { get; set; }

            [JsonProperty(PropertyName = "Decay Options")]
            public DecaySettings Decay { get; set; }

            [JsonProperty(PropertyName = "UI Options")]
            public UIOptions UI { get; set; }

            [JsonProperty(PropertyName = "Build Options")]
            public BuildOptions Build { get; set; }

            public class BuildOptions
            {
                [JsonProperty(PropertyName = "Allow users to build a car")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Use cooldown timers")]
                public bool Cooldown { get; set; }

                [JsonProperty(PropertyName = "Build Costs")]
                public List<BuildOption> Costs { get; set; }

                public class BuildOption
                {
                    [JsonProperty(PropertyName = "Item Shortname")]
                    public string Shortname { get; set; }

                    [JsonProperty(PropertyName = "Amount")]
                    public int Amount { get; set; }
                }
            }

            public class ButtonConfiguration
            {
                [JsonProperty(PropertyName = "Open inventory")]
                public string Inventory { get; set; }

                public string Accelerate { get; set; }

                [JsonProperty(PropertyName = "Brake / Reverse")]
                public string Brake { get; set; }

                [JsonProperty(PropertyName = "Turn Left")]
                public string Left { get; set; }

                [JsonProperty(PropertyName = "Turn Right")]
                public string Right { get; set; }

                [JsonProperty(PropertyName = "Hand Brake")]
                public string HBrake { get; set; }

                [JsonProperty(PropertyName = "Open fuel tank")]
                public string FuelTank { get; set; }

                [JsonProperty(PropertyName = "Toggle lights")]
                public string Lights { get; set; }
            }

            public class DecaySettings
            {
                [JsonProperty(PropertyName = "Enable decay system")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Amount of decay per decay tick (percentage of maximum health)")]
                public float Amount { get; set; }

                [JsonProperty(PropertyName = "Time between decay ticks (seconds)")]
                public int Time { get; set; }

                [JsonProperty(PropertyName = "Instantly decay when vehicle owner leaves the server")]
                public bool InstantDecay { get; set; }
            }

            public class RepairSettings
            {
                [JsonProperty(PropertyName = "Repair system enabled")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Shortname of item required to repair")]
                public string Shortname { get; set; }

                [JsonProperty(PropertyName = "Amount of item required to repair")]
                public int Amount { get; set; }

                [JsonProperty(PropertyName = "Amount of damage repaired per hit")]
                public int Damage { get; set; }

                [JsonProperty(PropertyName = "Allow players to flip cars that are on their roof")]
                public bool CanFlip { get; set; }
            }

            public class MovementSettings
            {
                [JsonProperty(PropertyName = "Use custom handling options")]
                public bool CustomHandling { get; set; }

                [JsonProperty(PropertyName = "Engine - Acceleration torque")]
                public float Acceleration { get; set; }

                [JsonProperty(PropertyName = "Engine - Brake  torque")]
                public float Brakes { get; set; }

                [JsonProperty(PropertyName = "Engine - Reverse  torque")]
                public float Reverse { get; set; }

                [JsonProperty(PropertyName = "Engine - Maximum speed")]
                public float Speed { get; set; }

                [JsonProperty(PropertyName = "Steering - Max angle")]
                public float Steer { get; set; }

                [JsonProperty(PropertyName = "Steering - Max angle at speed")]
                public float SteerSpeed { get; set; }

                [JsonProperty(PropertyName = "Steering - Automatically counter steer")]
                public bool CounterSteer { get; set; }                

                [JsonProperty(PropertyName = "Suspension - Force")]
                public float Spring { get; set; }

                [JsonProperty(PropertyName = "Suspension - Damper")]
                public float Damper { get; set; }

                [JsonProperty(PropertyName = "Suspension - Target position (min 0, max 1)")]                
                public float Target { get; set; }

                [JsonProperty(PropertyName = "Suspension - Distance")]
                public float Distance { get; set; }


                [JsonProperty(PropertyName = "Anti Roll - Front horizontal force")]
                public float AntiRollFH { get; set; }

                [JsonProperty(PropertyName = "Anti Roll - Rear horizontal force")]
                public float AntiRollRH { get; set; }

                [JsonProperty(PropertyName = "Anti Roll - Vertical force")]
                public float AntiRollV { get; set; }                             
            }  
            
            public class PassengerOptions
            {
                [JsonProperty(PropertyName = "Allow passengers")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Require passenger to be a friend (FriendsAPI)")]
                public bool UseFriends { get; set; }

                [JsonProperty(PropertyName = "Require passenger to be a clan mate (Clans)")]
                public bool UseClans { get; set; }
            }

            public class CollisionOptions
            {
                [JsonProperty(PropertyName = "Enable collision damage system")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Collision damage multiplier")]
                public float Multiplier { get; set; }
            }

            public class DeathOptions
            {
                [JsonProperty(PropertyName = "Enable explosion damage on death")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Damage radius")]
                public float Radius { get; set; }

                [JsonProperty(PropertyName = "Damage Amount")]
                public float Amount { get; set; }
            }

            public class SecurityOptions
            {             
                [JsonProperty(PropertyName = "Enable ignition systems")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Set first player with a key as vehicle owner (doesn't require a key to drive)")]
                public bool Owners { get; set; }

                [JsonProperty(PropertyName = "Ignition options")]
                public IgnitionOptions Ignition { get; set; }

                [JsonProperty(PropertyName = "Hotwire options")]
                public HotwireOptions Hotwire { get; set; }

                [JsonProperty(PropertyName = "Trunk lock options")]
                public TrunkOptions Trunk { get; set; }

                public class TrunkOptions
                {
                    [JsonProperty(PropertyName = "Allow locks to be placed on the trunk")]
                    public bool CanLock { get; set; }

                    [JsonProperty(PropertyName = "Only allow locks to be placed if the player has the ignition key")]
                    public bool RequireKey { get; set; }
                }

                public class IgnitionOptions
                {
                    [JsonProperty(PropertyName = "Allow players to copy a ignition key")]
                    public bool CanCopy { get; set; }

                    [JsonProperty(PropertyName = "Give the first player to enter the vehicle a key")]
                    public bool KeyOnEnter { get; set; }   
                    
                    [JsonProperty(PropertyName = "Chance of getting a key on entrance (1 in X)")]
                    public int KeyChance { get; set; }
                }

                public class HotwireOptions
                {
                    [JsonProperty(PropertyName = "Allow players to hotwire vehicles")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Deal shock damage on failed hotwire attempts")]
                    public bool DealDamage { get; set; }

                    [JsonProperty(PropertyName = "Amount of time it takes per hotwire attempt (seconds)")]
                    public int Time { get; set; }

                    [JsonProperty(PropertyName = "Chance of successfully hotwiring a vehicle (1 in X chance)")]
                    public int Chance { get; set; }
                }
            }

            public class InventoryOptions
            {
                [JsonProperty(PropertyName = "Enable inventory system")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Drop inventory on death")]
                public bool DropInv { get; set; }    
                
                [JsonProperty(PropertyName = "Inventory size (max 36)")]
                public int Size { get; set; }
            }

            public class SpawnableOptions
            {
                [JsonProperty(PropertyName = "Enable automatic vehicle spawning")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Use RandomSpawns for spawn locations")]
                public bool RandomSpawns { get; set; }

                [JsonProperty(PropertyName = "Spawnfile name")]
                public string Spawnfile { get; set; }

                [JsonProperty(PropertyName = "Maximum spawned vehicles at any time")]
                public int Max { get; set; }

                [JsonProperty(PropertyName = "Time between autospawns (seconds)")]
                public int Time { get; set; }

                [JsonProperty(PropertyName = "Cooldown time for player spawned vehicles via chat command (seconds)")]
                public int Cooldown { get; set; }

                [JsonProperty(PropertyName = "Prevent command spawning if player is building blocked")]
                public bool DisableBuildingBlock { get; set; }

                [JsonProperty(PropertyName = "Set vehicle owner to the person who spawned it")]
                public bool ApplyOwnershipOnSpawn { get; set; }

                [JsonProperty(PropertyName = "Max vehicles allowed per player (requires 'Set vehicle owner' enabled, 0 = unlimited)")]
                public int MaxPerPlayer { get; set; }                
            }

            public class FuelOptions
            {
                [JsonProperty(PropertyName = "Requires fuel")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Fuel type (item shortname)")]
                public string FuelType { get; set; }

                [JsonProperty(PropertyName = "Fuel consumption rate (litres per second)")]
                public float Consumption { get; set; }

                [JsonProperty(PropertyName = "Spawn vehicles with fuel")]
                public bool GiveFuel { get; set; }

                [JsonProperty(PropertyName = "Amount of fuel to give spawned vehicles (minimum)")]
                public int FuelAmountMin { get; set; }

                [JsonProperty(PropertyName = "Amount of fuel to give spawned vehicles (maximum)")]
                public int FuelAmountMax { get; set; }
            }

            public class ActiveItemOptions
            {
                [JsonProperty(PropertyName = "Driver - Disable all held items")]
                public bool DisableDriver { get; set; }

                [JsonProperty(PropertyName = "Passenger - Disable all held items")]
                public bool DisablePassengers { get; set; }               
                
            }

            public class UIOptions
            {
                [JsonProperty(PropertyName = "Health settings")]
                public UICounter Health { get; set; }

                [JsonProperty(PropertyName = "Fuel settings")]
                public UICounter Fuel { get; set; }

                public class UICounter
                {
                    [JsonProperty(PropertyName = "Display to player")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Position - X minimum")]
                    public float Xmin { get; set; }

                    [JsonProperty(PropertyName = "Position - X maximum")]
                    public float XMax { get; set; }

                    [JsonProperty(PropertyName = "Position - Y minimum")]
                    public float YMin { get; set; }

                    [JsonProperty(PropertyName = "Position - Y maximum")]
                    public float YMax { get; set; }

                    [JsonProperty(PropertyName = "Background color (hex)")]
                    public string Color1 { get; set; }

                    [JsonProperty(PropertyName = "Background alpha")]
                    public float Color1A { get; set; }

                    [JsonProperty(PropertyName = "Status color (hex)")]
                    public string Color2 { get; set; }

                    [JsonProperty(PropertyName = "Status alpha")]
                    public float Color2A { get; set; }
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

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                ActiveItems = new ConfigData.ActiveItemOptions
                {
                    DisableDriver = true,
                    DisablePassengers = false
                },
                Buttons = new ConfigData.ButtonConfiguration
                {
                    Inventory = "RELOAD",
                    Accelerate = "FORWARD",
                    Brake = "BACKWARD",
                    Left = "LEFT",
                    Right = "RIGHT",
                    HBrake = "SPRINT",
                    FuelTank = "FIRE_THIRD",
                    Lights = "RELOAD"
                },
                Build = new ConfigData.BuildOptions
                {
                    Enabled = false,
                    Cooldown = true,
                    Costs = new List<ConfigData.BuildOptions.BuildOption>
                        {
                            new ConfigData.BuildOptions.BuildOption
                            {
                                Amount = 500,
                                Shortname = "metal.refined"
                            },
                            new ConfigData.BuildOptions.BuildOption
                            {
                                Amount = 100,
                                Shortname = "techparts"
                            }
                        }
                },
                Collision = new ConfigData.CollisionOptions
                {
                    Enabled = true,
                    Multiplier = 1.0f
                },
                Death = new ConfigData.DeathOptions
                {
                    Amount = 75,
                    Enabled = true,
                    Radius = 6
                },
                Decay = new ConfigData.DecaySettings
                {
                    Amount = 5,
                    Time = 3600,
                    Enabled = true,
                    InstantDecay = false
                },
                Inventory = new ConfigData.InventoryOptions
                {
                    DropInv = true,
                    Enabled = true,
                    Size = 36
                },
                Movement = new ConfigData.MovementSettings
                {
                    Acceleration = 600f,
                    Brakes = 800f,
                    Reverse = 500f,
                    Steer = 60f,
                    SteerSpeed = 20f,
                    CounterSteer = false,
                    Speed = 90f,
                    Damper = 2000f,
                    Distance = 0.2f,
                    Spring = 40000f,
                    Target = 0.4f,
                    AntiRollFH = 3500f,
                    AntiRollRH = 3500f,
                    AntiRollV = 500f,
                    CustomHandling = true
                },
                Passengers = new ConfigData.PassengerOptions
                {
                    Enabled = true,
                    UseClans = true,
                    UseFriends = true
                },
                Security = new ConfigData.SecurityOptions
                {
                    Enabled = true,
                    Ignition = new ConfigData.SecurityOptions.IgnitionOptions
                    {
                        CanCopy = true,
                        KeyChance = 1,
                        KeyOnEnter = true
                    },
                    Hotwire = new ConfigData.SecurityOptions.HotwireOptions
                    {
                        Enabled = true,
                        Chance = 5,
                        Time = 45,
                        DealDamage = true
                    },
                    Trunk = new ConfigData.SecurityOptions.TrunkOptions
                    {
                        CanLock = false,
                        RequireKey = true
                    }
                },
                Spawnable = new ConfigData.SpawnableOptions
                {
                    Enabled = true,
                    Max = 5,
                    Time = 1800,
                    Spawnfile = "",
                    RandomSpawns = false,
                    Cooldown = 86400,
                    DisableBuildingBlock = true,
                    ApplyOwnershipOnSpawn = false,
                    MaxPerPlayer = -1
                },
                Fuel = new ConfigData.FuelOptions
                {
                    Enabled = true,
                    Consumption = 0.5f,
                    FuelType = "lowgradefuel",
                    FuelAmountMin = 10,
                    FuelAmountMax = 50,
                    GiveFuel = true                    
                },
                Repair = new ConfigData.RepairSettings
                {
                    Amount = 10,
                    CanFlip = true,
                    Damage = 30,
                    Enabled = true,
                    Shortname = "scrap"
                },
                UI = new ConfigData.UIOptions
                {
                    Fuel = new ConfigData.UIOptions.UICounter
                    {
                        Color1 = "#F2F2F2",
                        Color1A = 0.05f,
                        Color2 = "#ce422b",
                        Color2A = 1,
                        Enabled = true,
                        Xmin = 0.69f,
                        XMax = 0.83f,
                        YMin = 0.06f,
                        YMax = 0.096f
                    },
                    Health = new ConfigData.UIOptions.UICounter
                    {
                        Color1 = "#F2F2F2",
                        Color1A = 0.05f,
                        Color2 = "#ce422b",
                        Color2A = 0.6f,
                        Enabled = true,
                        Xmin = 0.69f,
                        XMax = 0.83f,
                        YMin = 0.1f,
                        YMax = 0.135f
                    }
                },
                Version = Version
            };
        }

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();
            if (configData.Version < new VersionNumber(0, 2, 20))            
                configData.Decay = baseConfig.Decay;

            if (configData.Version < new VersionNumber(0, 2, 25))
            {
                configData.Spawnable.Cooldown = baseConfig.Spawnable.Cooldown;
                configData.Movement.CustomHandling = baseConfig.Movement.CustomHandling;
            }

            if (configData.Version < new VersionNumber(0, 2, 35))
            {
                configData.Repair.CanFlip = baseConfig.Repair.CanFlip;
            }

            if (configData.Version < new VersionNumber(0, 2, 37))
            {
                configData.Death = baseConfig.Death;
            }

            if (configData.Version < new VersionNumber(0, 2, 54))
                configData.Fuel.Consumption = baseConfig.Fuel.Consumption;

            if (configData.Version < new VersionNumber(0, 2, 58))
                configData.Build = baseConfig.Build;

            if (configData.Version < new VersionNumber(0, 2, 62))
            {
                configData.Spawnable.DisableBuildingBlock = true;
                configData.Decay.InstantDecay = false;
            }

            configData.Version = Version;

            PrintWarning("Config update completed!");
        }
        #endregion

        #region Data Management
        private void SaveData()
        {
            data.WriteObject(storedData);
            cooldowns.WriteObject(userCooldowns);
        }

        private void LoadData()
        {
            try
            {
                storedData = data.ReadObject<RestoreData>();

                if (storedData == null || storedData.restoreData == null)
                    storedData = new RestoreData();
            }
            catch
            {
                storedData = new RestoreData();
            }
            try
            {
                userCooldowns = cooldowns.ReadObject<Dictionary<ulong, double>>();
            }
            catch
            {
                userCooldowns = new Dictionary<ulong, double>();
            }
        }

        public class RestoreData
        {
            public Hash<uint, InventoryData> restoreData = new Hash<uint, InventoryData>();
            public Hash<uint, SecurityData> securityData = new Hash<uint, SecurityData>();

            public void AddData(CarController controller)
            {
                restoreData[controller.entity.net.ID] = new InventoryData(controller);
                securityData[controller.entity.net.ID] = new SecurityData(controller);
            }

            public void RemoveData(uint netId)
            {
                if (HasRestoreData(netId))
                    restoreData.Remove(netId);

                if (HasSecurityData(netId))
                    securityData.Remove(netId);
            }

            public bool HasRestoreData(uint netId) => restoreData.ContainsKey(netId);

            public bool HasSecurityData(uint netId) => securityData.ContainsKey(netId);

            public void RestoreVehicle(CarController controller)
            {
                if (controller == null)
                    return;

                InventoryData inventoryData;
                if (restoreData.TryGetValue(controller.entity.net.ID, out inventoryData))
                {
                    if (controller.container != null && controller.container.inventory != null)
                        RestoreAllItems(controller, inventoryData);
                }

                SecurityData securityDat;
                if (securityData.TryGetValue(controller.entity.net.ID, out securityDat))
                {
                    securityDat.RestoreVehicleSecurity(controller);
                }
            }

            private void RestoreAllItems(CarController controller, InventoryData inventoryData)
            {
                if (controller == null)
                    return;

                RestoreItems(controller, inventoryData.vehicleContainer, true);
                RestoreItems(controller, inventoryData.fuelContainer, false);
            }

            private bool RestoreItems(CarController controller, ItemData[] itemData, bool isInventory)
            {
                if ((!isInventory && !ins.configData.Fuel.Enabled) || (isInventory && !ins.configData.Inventory.Enabled) || itemData == null || itemData.Length == 0)
                    return true;

                for (int i = 0; i < itemData.Length; i++)
                {
                    Item item = CreateItem(itemData[i]);
                    item.MoveToContainer(isInventory ? controller.container.inventory : controller.fuelTank, itemData[i].position, true);
                }
                return true;
            }

            private Item CreateItem(ItemData itemData)
            {
                Item item = ItemManager.CreateByItemID(itemData.itemid, itemData.amount, itemData.skin);
                item.condition = itemData.condition;
                if (itemData.instanceData != null)
                    itemData.instanceData.Restore(item);

                item.blueprintTarget = itemData.blueprintTarget;

                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    if (!string.IsNullOrEmpty(itemData.ammotype))
                        weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemData.ammotype);
                    weapon.primaryMagazine.contents = itemData.ammo;
                }
                if (itemData.contents != null)
                {
                    foreach (var contentData in itemData.contents)
                    {
                        Item newContent = ItemManager.CreateByItemID(contentData.itemid, contentData.amount);
                        if (newContent != null)
                        {
                            newContent.condition = contentData.condition;
                            newContent.MoveToContainer(item.contents);
                        }
                    }
                }
                return item;
            }

            public class InventoryData
            {
                public ItemData[] vehicleContainer = new ItemData[0];
                public ItemData[] fuelContainer = new ItemData[0];

                public InventoryData() { }

                public InventoryData(CarController controller)
                {
                    if (ins.configData.Inventory.Enabled && controller.container != null)
                        vehicleContainer = GetItems(controller.container.inventory).ToArray();
                    if (ins.configData.Fuel.Enabled && controller.fuelTank != null)
                        fuelContainer = GetItems(controller.fuelTank).ToArray();
                }

                private IEnumerable<ItemData> GetItems(ItemContainer container)
                {
                    return container.itemList.Select(item => new ItemData
                    {
                        itemid = item.info.itemid,
                        amount = item.amount,
                        ammo = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.contents ?? 0,
                        ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null,
                        position = item.position,
                        skin = item.skin,
                        condition = item.condition,
                        instanceData = new ItemData.InstanceData(item),
                        blueprintTarget = item.blueprintTarget,
                        contents = item.contents?.itemList.Select(item1 => new ItemData
                        {
                            itemid = item1.info.itemid,
                            amount = item1.amount,
                            condition = item1.condition
                        }).ToArray()
                    });
                }
            }

            public class ItemData
            {
                public int itemid;
                public ulong skin;
                public int amount;
                public float condition;
                public int ammo;
                public string ammotype;
                public int position;
                public int blueprintTarget;
                public InstanceData instanceData;
                public ItemData[] contents;

                public class InstanceData
                {                    
                    public int dataInt;
                    public int blueprintTarget;
                    public int blueprintAmount;

                    public InstanceData() { }
                    public InstanceData(Item item)
                    {
                        if (item.instanceData == null)
                            return;

                        dataInt = item.instanceData.dataInt;
                        blueprintAmount = item.instanceData.blueprintAmount;
                        blueprintTarget = item.instanceData.blueprintTarget;
                    }

                    public void Restore(Item item)
                    {
                        item.instanceData = new ProtoBuf.Item.InstanceData();
                        item.instanceData.blueprintAmount = blueprintAmount;
                        item.instanceData.blueprintTarget = blueprintTarget;
                        item.instanceData.dataInt = dataInt;
                    }
                }
            }

            public class SecurityData
            {
                public int ignitionCode;
                public float health;
                public bool hasBeenHotwired;

                public bool hasCodeLock;
                public bool hasKeyLock;
                public bool isLocked;

                public string lockCode;
                public string guestCode;

                public ulong ownerId;

                public List<ulong> whiteListPlayers = new List<ulong>();
                public List<ulong> guestPlayers = new List<ulong>();

                public SecurityData() { }
                public SecurityData(CarController controller)
                {
                    this.ignitionCode = controller.IgnitionCode;
                    this.hasBeenHotwired = controller.HasBeenHotwired;
                    this.ownerId = controller.ownerId;
                    this.health = controller.entity.health;

                    if (controller.container != null)
                    {                       
                        BaseEntity lockEntity = controller.container.GetSlot(BaseEntity.Slot.Lock);
                        if (lockEntity != null)
                        {
                            if (lockEntity.prefabID == 3518824735)
                            {
                                hasCodeLock = true;
                                lockCode = (lockEntity as CodeLock).code;
                                guestCode = (lockEntity as CodeLock).guestCode;

                                whiteListPlayers = (lockEntity as CodeLock).whitelistPlayers;
                                guestPlayers = (lockEntity as CodeLock).guestPlayers;                                
                            }
                            else if (lockEntity.prefabID == 2106860026)
                            {
                                hasKeyLock = true;
                                lockCode = (lockEntity as KeyLock).keyCode.ToString();
                            }
                            isLocked = lockEntity.HasFlag(BaseEntity.Flags.Locked);
                        }
                    }
                }

                public void RestoreVehicleSecurity(CarController controller)
                {
                    controller.HasBeenHotwired = hasBeenHotwired;
                    controller.ownerId = ownerId;
                    controller.IgnitionCode = ignitionCode;
                    controller.entity.SetHealth(health);

                    if (controller.container == null)
                        return;

                    if (hasCodeLock)
                    {
                        BaseEntity lockEntity = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab", new Vector3(), new Quaternion(), true);
                        if (lockEntity != null)
                        {
                            lockEntity.SetParent(controller.container, controller.container.GetSlotAnchorName(BaseEntity.Slot.Lock));
                            lockEntity.OnDeployed(controller.container, null);
                            lockEntity.Spawn();
                            controller.container.SetSlot(BaseEntity.Slot.Lock, lockEntity);

                            (lockEntity as CodeLock).whitelistPlayers = whiteListPlayers;
                            (lockEntity as CodeLock).guestPlayers = guestPlayers;
                            (lockEntity as CodeLock).code = lockCode;
                            (lockEntity as CodeLock).guestCode = guestCode;
                            (lockEntity as CodeLock).SetFlag(BaseEntity.Flags.Locked, isLocked, false);
                        }
                    }
                    else if (hasKeyLock)
                    {
                        BaseEntity lockEntity = GameManager.server.CreateEntity("assets/prefabs/locks/keylock/lock.key.prefab", new Vector3(), new Quaternion(), true);
                        if (lockEntity != null)
                        {
                            lockEntity.SetParent(controller.container, controller.container.GetSlotAnchorName(BaseEntity.Slot.Lock));
                            lockEntity.OnDeployed(controller.container, null);
                            lockEntity.Spawn();
                            controller.container.SetSlot(BaseEntity.Slot.Lock, lockEntity);

                            (lockEntity as KeyLock).keyCode = int.Parse(lockCode);
                            (lockEntity as KeyLock).SetFlag(BaseEntity.Flags.Locked, isLocked, false);
                        }
                    }
                }
            }
        }

        public struct ItemCost
        {
            public int itemId;
            public int amount;

            public ItemCost(int itemId, int amount)
            {
                this.itemId = itemId;
                this.amount = amount;
            }
        }
        #endregion

        #region Localization
        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["not_friend"] = "<color=#D3D3D3>You must be a friend or clanmate with the operator</color>",
            ["controls1"] = "<color=#ce422b>Car Controls:</color>\n<color=#D3D3D3>Accelerate:</color> <color=#ce422b>{0}</color>\n<color=#D3D3D3>Brake/Reverse:</color> <color=#ce422b>{1}</color>\n<color=#D3D3D3>Turn Left:</color> <color=#ce422b>{2}</color>\n<color=#D3D3D3>Turn Right:</color> <color=#ce422b>{3}</color>\n<color=#D3D3D3>Hand Brake:</color> <color=#ce422b>{4}</color>\n<color=#D3D3D3>Toggle Lights:</color> <color=#ce422b>{5}</color>",
            ["controls2"] = "<color=#ce422b>Car Controls:</color>\n<color=#D3D3D3>Accelerate:</color> <color=#ce422b>{0}</color>\n<color=#D3D3D3>Brake/Reverse:</color> <color=#ce422b>{1}</color>\n<color=#D3D3D3>Turn Left:</color> <color=#ce422b>{2}</color>\n<color=#D3D3D3>Turn Right:</color> <color=#ce422b>{3}</color>\n<color=#D3D3D3>Toggle Lights:</color> <color=#ce422b>{4}</color>",
            ["access_inventory1"] = "<color=#D3D3D3>You can access the inventory from the trunk of the vehicle</color>",
            ["access_fuel"] = "<color=#D3D3D3>Access Fuel Tank (from outside of the vehicle) </color><color=#ce422b>{0}</color>",
            ["fuel_type"] = "<color=#D3D3D3>This vehicle requires </color><color=#ce422b>{0}</color> <color=#D3D3D3>to run!</color>",
            ["not_enabled"] = "<color=#D3D3D3>Passengers is not enabled</color>",
            ["nopermission"] = "<color=#D3D3D3>You do not have permission to drive this car</color>",
            ["nopermissionbuild"] = "<color=#D3D3D3>You do not have permission to build a car</color>",
            ["health"] = "HLTH: ",
            ["fuel"] = "FUEL: {0} L",
            ["fullhealth"] = "<color=#D3D3D3>This vehicle is already at full health</color>",
            ["noresources"] = "<color=#D3D3D3>You need atleast </color><color=#ce422b>{0}x {1}</color> <color=#D3D3D3>to make repairs</color>",
            ["repairhelp"] = "<color=#D3D3D3>You can make repairs to this vehicle using a hammer which costs </color><color=#ce422b>{0}x {1}</color> <color=#D3D3D3>per hit</color>",
            ["itemnotallowed"] = "<color=#D3D3D3>You can not use that item whilst you are in a car</color>",
            ["key_created"] = "<color=#ce422b>This vehicle requires a key to start.</color><color=#D3D3D3> Lucky for you it was in the ignition! You must have this key in your </color><color=#ce422b>inventory</color><color=#D3D3D3> to start the car</color>",
            ["key_copy"] = "<color=#D3D3D3>You can make copies of this key by typing </color><color=#ce422b>/copykey</color><color=#D3D3D3> whilst sitting in the drivers seat</color>",
            ["no_key"] = "<color=#D3D3D3>You do not have the </color><color=#ce422b>correct key</color> <color=#D3D3D3>to start this vehicle</color>",
            ["no_key_hotwire"] = "<color=#D3D3D3>You can attempt to hotwire this vehicle by typing </color><color=#ce422b>/hotwire</color><color=#D3D3D3> whilst sitting in the drivers seat</color>",
            ["hotwire_success"] = "<color=#D3D3D3>You have successfully hotwired this vehicle!</color>",
            ["hotwire_fail"] = "<color=#D3D3D3>You have failed to hotwire this vehicle!</color>",
            ["hotwire_fail_left"] = "<color=#D3D3D3>Hotwiring has been cancelled because you left the vehicle</color>",
            ["not_commander"] = "<color=#D3D3D3>You must be the driver of a vehicle to use that command</color>",
            ["no_key_deploy"] = "<color=#D3D3D3>You must have the vehicle key to place a lock on the trunk</color>",
            ["already_hotwired"] = "<color=#D3D3D3>This vehicle has already been hotwired</color>",
            ["has_key"] = "<color=#D3D3D3>You have the key for this vehicle in your possession</color>",
            ["not_has_key"] = "<color=#D3D3D3>You do not have the key for this vehicle in your possession</color>",
            ["already_hotwiring"] = "<color=#D3D3D3>You are already hotwiring a vehicle</color>",
            ["begun_hotwiring"] = "<color=#D3D3D3>You have begun to hotwire this vehicle. It will take </color><color=#ce422b>{0} seconds</color><color=#D3D3D3> with a</color><color=#ce422b> 1 in {1}</color> <color=#D3D3D3>chance of success</color>",
            ["key_copied"] = "<color=#D3D3D3>You have made a copy of the ignition key</color>",
            ["fuel_empty"] = "EMPTY",
            ["onCooldown"] = "<color=#D3D3D3>You must wait another </color><color=#ce422b>{0}</color><color=#D3D3D3> before you can spawn another car</color>",
            ["noStacking"] = "<color=#D3D3D3>That space is occupied by another vehicle</color>",
            ["carFlipped"] = "<color=#D3D3D3>You have flipped the vehicle! You can unflip it by looking at it and typing <color=#ce422b>/flipcar</color></color>",
            ["costs"] = "<color=#ce422b>Cost to build:</color>",
            ["notenoughres"] = "<color=#D3D3D3>You do not have enough resources to build! Type <color=#ce422b>/buildcar costs</color> to see what you need</color>",
            ["buildingBlocked"] = "<color=#D3D3D3>You can not spawn a car when you are <color=#ce422b>building blocked</color></color>",
            ["maxActiveVehicles"] = "<color=#D3D3D3>You already have the maximum amount of spawned vehicles allowed</color>",
            ["removingCars"] = "<color=#D3D3D3>Removing all cars owned by you...</color>",
        };
        #endregion
    }
}
