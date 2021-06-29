using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Car Setup", "misticos", "1.0.0")]
    [Description("Manage car options easily")]
    class CarSetup : CovalencePlugin
    {
        #region Variables

        private static CarSetup _ins = null;
        private Dictionary<uint, BaseController> _controllers = new Dictionary<uint, BaseController>();

        #endregion

        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Sedan")]
            public SedanConfiguration BasicCar = new SedanConfiguration();

            [JsonProperty(PropertyName = "Modular Cars", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ModularCarConfiguration> ModularCars = new List<ModularCarConfiguration>
                {new ModularCarConfiguration()};

            public class ModularCarConfiguration : BaseConfiguration
            {
                [JsonProperty(PropertyName = "Selector")]
                public ModularCarSelector Selector = new ModularCarSelector();

                public class ModularCarSelector
                {
                    [JsonProperty(PropertyName = "Modules Available",
                        ObjectCreationHandling = ObjectCreationHandling.Replace)]
                    public HashSet<int> Modules = new HashSet<int> {1, 2, 3, 4};

                    public bool Fits(ModularCar car)
                    {
                        return Modules.Contains(car.TotalSockets);
                    }
                }
            }

            public class SedanConfiguration : BaseConfiguration
            {
                [JsonProperty(PropertyName = "Steering Angle")]
                public float Steering = 60f;
            }

            public class BaseConfiguration
            {
                [JsonProperty(PropertyName = "Mass Multiplier")]
                public float MassMultiplier = 1f;
                
                [JsonProperty(PropertyName = "Wheel Colliders")]
                public WheelColliderData WheelColliders = new WheelColliderData();

                [JsonProperty(PropertyName = "Movement")]
                public MovementData Movement = new MovementData();

                [JsonProperty(PropertyName = "Flipping")]
                public FlipData Flip = new FlipData();

                public class FlipData
                {
                    [JsonProperty(PropertyName = "Minimum Rotation")]
                    public float MinimumRotation = 5f;

                    [JsonProperty(PropertyName = "Torque Applied")]
                    public float Torque = 120f;
                }

                public class MovementData
                {
                    [JsonProperty(PropertyName = "Forward Force")]
                    public float ForceForward = 200f;

                    [JsonProperty(PropertyName = "Backward Force")]
                    public float ForceBackward = 150f;
                }

                public class WheelColliderData
                {
                    [JsonProperty(PropertyName = "Mass Multiplier")]
                    public float WheelMass = 1f;

                    [JsonProperty(PropertyName = "Damping Rate Multiplier")]
                    public float DampingRate = 1f;

                    [JsonProperty(PropertyName = "Motor Torque")]
                    public float TorqueMotor = 400f;

                    [JsonProperty(PropertyName = "Brake Torque")]
                    public float TorqueBrake = 10000f;

                    [JsonProperty(PropertyName = "Suspension")]
                    public SuspensionData Suspension = new SuspensionData();

                    public class SuspensionData
                    {
                        [JsonProperty(PropertyName = "Extension Distance Multiplier")]
                        public float ExtensionDistance = 1f;

                        [JsonProperty(PropertyName = "Damper Force Multiplier")]
                        public float DamperForce = 1f;

                        [JsonProperty(PropertyName = "Sprint Force Multiplier")]
                        public float SpringForce = 1f;
                    }

                    public void Apply(WheelCollider wheel)
                    {
                        wheel.mass *= WheelMass;
                        wheel.suspensionDistance *= Suspension.ExtensionDistance;
                        wheel.wheelDampingRate *= DampingRate;

                        var suspensionSpring = wheel.suspensionSpring;
                        suspensionSpring.damper *= Suspension.DamperForce;
                        suspensionSpring.spring *= Suspension.SpringForce;
                        wheel.suspensionSpring = suspensionSpring;
                    }

                    public void Undo(WheelCollider wheel)
                    {
                        wheel.mass /= WheelMass;
                        wheel.suspensionDistance /= Suspension.ExtensionDistance;
                        wheel.wheelDampingRate /= DampingRate;

                        var suspensionSpring = wheel.suspensionSpring;
                        suspensionSpring.damper /= Suspension.DamperForce;
                        suspensionSpring.spring /= Suspension.SpringForce;
                        wheel.suspensionSpring = suspensionSpring;
                    }
                }
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion

        #region Hooks

        private void Init()
        {
            _ins = this;
        }

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var basicCar = entity as BasicCar;
                if (!ReferenceEquals(basicCar, null))
                    OnEntitySpawned(basicCar);

                var car = entity as ModularCar;
                if (!ReferenceEquals(car, null))
                    OnEntitySpawned(car);
            }
        }

        private void OnEntitySpawned(BasicCar vehicle)
        {
            vehicle.gameObject.AddComponent<BasicCarController>();
        }

        private void OnEntitySpawned(ModularCar vehicle)
        {
            vehicle.gameObject.AddComponent<ModularCarController>();
        }

        private void Unload()
        {
            foreach (var vehicle in UnityEngine.Object.FindObjectsOfType<BasicCarController>())
                UnityEngine.Object.DestroyImmediate(vehicle);

            foreach (var vehicle in UnityEngine.Object.FindObjectsOfType<ModularCarController>())
                UnityEngine.Object.DestroyImmediate(vehicle);

            _ins = null;
        }

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            var uid = mountable.parentEntity.uid;
            if (uid == 0)
                return;
            
            BaseController controller;
            if (!_controllers.TryGetValue(uid, out controller))
                return;

            controller.Driver = player;
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            var uid = mountable.parentEntity.uid;
            if (uid == 0)
                return;
            
            BaseController controller;
            if (!_controllers.TryGetValue(uid, out controller))
                return;

            controller.Driver = null;
        }

        #endregion

        #region Controller

        [DefaultExecutionOrder(10000)] // Ensure we apply our changes after Facepunch do
        private abstract class BaseController : FacepunchBehaviour
        {
            protected WheelCollider[] Wheels;
            protected Rigidbody Rigidbody;

            public BasePlayer Driver;
        }

        private abstract class BaseController<TVehicle, TConfig> : BaseController where TVehicle : BaseVehicle
            where TConfig : Configuration.BaseConfiguration
        {
            protected TVehicle Car;
            protected TConfig Base;

            private uint _netId;

            protected virtual void Awake()
            {
                Car = GetComponent<TVehicle>();
                Rigidbody = Car.rigidBody;

                _ins._controllers[_netId = Car.net.ID] = this;
            }

            protected virtual void Start()
            {
                Rigidbody.mass *= Base.MassMultiplier;
                if (Wheels == null || Wheels.Length == 0)
                {
                    Debug.LogWarning($"There were no wheels found in {nameof(BaseController<TVehicle, TConfig>)}/{nameof(Start)}.");
                    return;
                }

                foreach (var wheel in Wheels)
                    Base.WheelColliders.Apply(wheel);
            }

            protected void OnDestroy()
            {
                _ins?._controllers?.Remove(_netId);
                
                if (Base == null)
                    return;

                Rigidbody.mass /= Base.MassMultiplier;
                if (Wheels == null || Wheels.Length == 0)
                    return;

                foreach (var wheel in Wheels)
                    Base.WheelColliders.Undo(wheel);
            }

            protected virtual void FixedUpdate()
            {
                ApplyFlip();

                ApplyWheelTorque();
                ApplyMovement();
            }

            protected void ApplyFlip()
            {
                foreach (var wheel in Wheels)
                {
                    if (Math.Abs(wheel.steerAngle) < Base.Flip.MinimumRotation || !wheel.isGrounded)
                        continue;

                    Rigidbody.AddRelativeTorque(
                        new Vector3(0f, 0f, Base.Flip.Torque * Math.Sign(wheel.steerAngle) * -1f), ForceMode.Force);
                }
            }

            protected virtual void ApplyWheelTorque()
            {
                foreach (var wheel in Wheels)
                {
                    ApplyWheelTorque(wheel);
                }
            }

            protected abstract void ApplyWheelTorque(WheelCollider wheel);

            protected void ApplyMovement()
            {
                // This is before wheels check because it is faster
                if (ReferenceEquals(Driver, null))
                    return;

                // Prevent it from pushing you in the sky
                foreach (var wheel in Wheels)
                {
                    if (wheel.isGrounded)
                        continue;

                    return;
                }

                if (Driver.serverInput.IsDown(BUTTON.FORWARD))
                {
                    Rigidbody.AddRelativeForce(Vector3.forward * Base.Movement.ForceForward, ForceMode.Impulse);
                }
                else if (Driver.serverInput.IsDown(BUTTON.BACKWARD))
                {
                    Rigidbody.AddRelativeForce(Vector3.back * Base.Movement.ForceBackward, ForceMode.Impulse);
                }
            }
        }

        private class ModularCarController : BaseController<ModularCar, Configuration.ModularCarConfiguration>
        {
            private ModularCar.Wheel[] _modularWheels;

            protected override void Awake()
            {
                base.Awake();
                foreach (var option in _ins._config.ModularCars)
                {
                    if (!option.Selector.Fits(Car))
                        continue;

                    Base = option;
                }

                if (Base == null)
                    DestroyImmediate(this);
            }

            protected override void Start()
            {
                _modularWheels = new[] {Car.wheelRR, Car.wheelFL, Car.wheelFR, Car.wheelRL};

                Wheels = new WheelCollider[_modularWheels.Length];

                for (var i = 0; i < Wheels.Length; i++)
                    Wheels[i] = _modularWheels[i].wheelCollider;

                base.Start();
            }

            protected override void ApplyWheelTorque(WheelCollider wheel)
            {
                if (Rigidbody.IsSleeping())
                    return;
                
                wheel.motorTorque = ReferenceEquals(Driver, null) && Car.timeSinceLastPush > 2f ? 0f : Base.WheelColliders.TorqueMotor;
                wheel.brakeTorque = ReferenceEquals(Driver, null) && Rigidbody.velocity.magnitude < 2.5f && Car.timeSinceLastPush > 2f
                    ? Base.WheelColliders.TorqueBrake
                    : 0f;
            }
        }

        private class BasicCarController : BaseController<BasicCar, Configuration.SedanConfiguration>
        {
            protected override void Awake()
            {
                base.Awake();
                Base = _ins._config.BasicCar;
            }

            protected override void Start()
            {
                Wheels = new WheelCollider[Car.wheels.Length];

                for (var i = 0; i < Car.wheels.Length; i++)
                {
                    Wheels[i] = Car.wheels[i].wheelCollider;
                }

                base.Start();
            }

            protected override void FixedUpdate()
            {
                base.FixedUpdate();

                if (ReferenceEquals(Driver, null))
                    return;

                if (Driver.serverInput.IsDown(BUTTON.LEFT))
                {
                    Car.steering = Base.Steering;
                }

                if (Driver.serverInput.IsDown(BUTTON.RIGHT))
                {
                    Car.steering = -Base.Steering;
                }
            }

            protected override void ApplyWheelTorque(WheelCollider wheel)
            {
                if (ReferenceEquals(Driver, null))
                    return;
                
                wheel.motorTorque = Car.gasPedal * Base.WheelColliders.TorqueMotor;
                wheel.brakeTorque = Car.brakePedal * Base.WheelColliders.TorqueBrake;
            }
        }

        #endregion
    }
}