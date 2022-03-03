using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Airdrop Without Parachute", "Enforcer", "1.0.0")]
    [Description("Removes the parachute on Airdrops")]
    public class AirdropWithoutParachute : RustPlugin
    {
        #region Init

        private void Init()
        {
            LoadConfig();
        }

        private void Unload()
        {
            config = null;
        }

        #endregion

        #region Hooks

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity == null)
                return;

            if (entity is SupplyDrop)
            {
                SupplyDrop supplyDrop = entity as SupplyDrop;
                var drop = supplyDrop.GetComponent<Rigidbody>();

                supplyDrop.RemoveParachute();
                drop.drag = config.airdropDrag;

            }
        }

        #endregion

        #region Configuration

        ConfigData config = new ConfigData();
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Airdrop drag")]
            public float airdropDrag { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                for (var i = 0; i < 3; i++) PrintError($"{Name}.json is corrupted! Recreating a new configuration");
                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData()
            {
                airdropDrag = 5f,
            };
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion
    }
}