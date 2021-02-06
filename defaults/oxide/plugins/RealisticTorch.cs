using Rust;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Realistic Torch", "Synvy", "1.0.3")]
    [Description("Prevents cold damage to players holding a lit torch.")]
    public class RealisticTorch : RustPlugin
    {
        #region Permissions

        private const string permUse = "realistictorch.use";

        #endregion

        #region Configuration

        private ConfigData configData;

        private class ConfigData
        {
            public int MinimumPlayerTemperature;
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                MinimumPlayerTemperature = 21,
            };

            SaveConfig(config);
        }

        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

        #endregion

        #region Hooks

        private void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permUse, this);
        }

        void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity entity)
        {
            var player = entity as BasePlayer;

            if (!(entity is BasePlayer) || !permission.UserHasPermission(player.UserIDString, permUse))
                return;

            if (ActiveItemIsTorch(player) && IsTorchOn(player))
            {
                if (player.metabolism.temperature.value < configData.MinimumPlayerTemperature)
                    player.metabolism.temperature.value = (configData.MinimumPlayerTemperature + 5);
            } 
        }

        #endregion

        #region Item Functions

        private bool ActiveItemIsTorch(BasePlayer player)
        {
            var item = player.GetActiveItem()?.info.shortname ?? "null";
            return item == "torch";
        }

        private bool IsTorchOn(BasePlayer player)
        {
            HeldEntity torch = player.GetHeldEntity();

            if (torch == null)
                return false;

            return torch.HasFlag(BaseEntity.Flags.On);
        }

        #endregion
    }
}