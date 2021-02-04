using Newtonsoft.Json;
using Oxide.Core;
using Rust.Modular;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Engine Parts Durability", "WhiteThunder", "1.2.0")]
    [Description("Alters engine part durability loss when modular cars are damaged.")]
    internal class EnginePartsDurability : CovalencePlugin
    {
        private DurabilityConfig _pluginConfig;

        #region Hooks

        private void Init()
        {
            _pluginConfig = Config.ReadObject<DurabilityConfig>();
        }

        private void OnServerInitialized(bool serverInitialized)
        {
            // The OnEntitySpawned hook already covers server init so this is just for late loading
            if (!serverInitialized)
            {
                foreach (var engineModule in BaseNetworkable.serverEntities.OfType<VehicleModuleEngine>())
                    SetInternalDamageMultiplier(engineModule);
            }
        }

        private void OnEntitySpawned(VehicleModuleEngine engineModule) =>
            SetInternalDamageMultiplier(engineModule);

        #endregion

        #region API

        private void API_RefreshMultiplier(EngineStorage engineStorage)
        {
            engineStorage.internalDamageMultiplier = _pluginConfig.DurabilityLossMultiplier;
        }

        #endregion

        #region Helpers

        private static bool MultiplierChangeWasBlocked(EngineStorage engineStorage, float desiredMultiplier)
        {
            object hookResult = Interface.CallHook("OnEngineDamageMultiplierChange", engineStorage, desiredMultiplier);
            return hookResult is bool && (bool)hookResult == false;
        }

        private void SetInternalDamageMultiplier(VehicleModuleEngine engineModule)
        {
            NextTick(() =>
            {
                if (engineModule == null)
                    return;

                var engineStorage = engineModule.GetContainer() as EngineStorage;
                if (engineStorage == null)
                    return;

                if (!MultiplierChangeWasBlocked(engineStorage, _pluginConfig.DurabilityLossMultiplier))
                    engineStorage.internalDamageMultiplier = _pluginConfig.DurabilityLossMultiplier;
            });
        }

        #endregion

        protected override void LoadDefaultConfig() => Config.WriteObject(new DurabilityConfig(), true);

        internal class DurabilityConfig
        {
            [JsonProperty("DurabilityLossMultiplier")]
            public float DurabilityLossMultiplier = 0;
        }
    }
}
