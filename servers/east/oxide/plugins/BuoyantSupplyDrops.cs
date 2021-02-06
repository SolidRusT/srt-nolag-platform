using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Buoyant Supply Drops", "Bazz3l", "1.0.2")]
    [Description("Allows supply drops to float on water")]
    class BuoyantSupplyDrops : RustPlugin
    {
        #region Config
        public PluginConfig _config;

        public PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                DetectionRate = 1,
            };
        }

        public class PluginConfig
        {
            public int DetectionRate;
        }
        #endregion

        #region Oxide
        protected override void LoadDefaultConfig() => Config.WriteObject(GetDefaultConfig(), true);

        void Init()
        {
            _config = Config.ReadObject<PluginConfig>();
        }

        void OnEntitySpawned(SupplyDrop supply)
        {
            if (supply == null) return;
            MakeBuoyant sfloat   = supply.gameObject.AddComponent<MakeBuoyant>();
            sfloat.buoyancyScale = 1f;
            sfloat.detectionRate = _config.DetectionRate;
        }
        #endregion

        #region Classes
        class MakeBuoyant : MonoBehaviour
        {
            public float buoyancyScale;
            public int detectionRate;
            private SupplyDrop _supplyDrop;

            void Awake()
            {
                _supplyDrop = GetComponent<SupplyDrop>();
                if(_supplyDrop == null) Destroy(this);
            }

            void FixedUpdate()
            {
                if(_supplyDrop == null)
                {
                    Destroy(this);
                    return;
                }
                
                if (UnityEngine.Time.frameCount % detectionRate == 0 && WaterLevel.Factor(_supplyDrop.WorldSpaceBounds().ToBounds()) > 0.65f)
                {
                    _supplyDrop.RemoveParachute();
                    _supplyDrop.MakeLootable();
                    BuoyancyComponent();
                    Destroy(this);
                }
            }

            void BuoyancyComponent()
            {
                Buoyancy buoyancy                  = gameObject.AddComponent<Buoyancy>();
                buoyancy.buoyancyScale             = buoyancyScale;
                buoyancy.rigidBody                 = gameObject.GetComponent<Rigidbody>();
                buoyancy.rigidBody.velocity        = Vector3.zero;
                buoyancy.rigidBody.angularVelocity = Vector3.zero;
                buoyancy.rigidBody.constraints     = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotationX;
            }
        }
        #endregion
    }
}
