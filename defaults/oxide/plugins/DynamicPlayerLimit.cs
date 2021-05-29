using ConVar;
using Newtonsoft.Json;
using System;

namespace Oxide.Plugins
{
    [Info("Dynamic Player Limit", "Pho3niX90", "0.0.7")]
    [Description("Increases the player limit by demand")]
    class DynamicPlayerLimit : RustPlugin
    {
        int _originalLimit = 0;
        Timer updateTimer = null;
        void OnServerInitialized(bool serverInitialized) {
            _originalLimit = Admin.ServerInfo().MaxPlayers;

            int startPlayerLimit = Math.Max(Admin.ServerInfo().Players, config.startPlayerSlots);
            UpdatePlayerLimit(startPlayerLimit);
            updateTimer = timer.Every(config.incrementInterval * 60, () => AdjustPlayers());
        }

        void Unload() {
            UpdatePlayerLimit(Math.Max(BasePlayer.activePlayerList.Count, _originalLimit));
            if (updateTimer != null) updateTimer.Destroy();
        }

        #region Helpers
        private void AdjustPlayers() {
            int currentSlots = Admin.ServerInfo().MaxPlayers;

            int slotsOpen = (currentSlots - (Admin.ServerInfo().Players + Admin.ServerInfo().Joining));
            if (slotsOpen <= config.incrementSlotsOpen && currentSlots < config.maxPlayerSlots) {
                int newSlots = Math.Min(config.incrementPlayerSlots + currentSlots, config.maxPlayerSlots);

                if (Performance.report.frameRate > config.fpsLimit) {
                    Puts($"Incrementing player slots from `{currentSlots}` to `{newSlots}\n Queued players `{Admin.ServerInfo().Queued}`\n Joining players `{Admin.ServerInfo().Joining}`");
                    UpdatePlayerLimit(newSlots);
                } else {
                    Puts($"Server FPS too low {Performance.report.frameRate} to adjust");
                }

            } else if (config.doAutoDecrease && slotsOpen > config.incrementSlotsOpen && currentSlots > config.startPlayerSlots) {
                int newSlots = Math.Max(Math.Max(currentSlots - config.incrementPlayerSlots, config.startPlayerSlots), Admin.ServerInfo().Players);

                Puts($"Decreasing player slots from `{currentSlots}` to `{newSlots}\n Queued players `{Admin.ServerInfo().Queued}`\n Joining players `{Admin.ServerInfo().Joining}`");
                UpdatePlayerLimit(newSlots);
            }
        }

        private void UpdatePlayerLimit(int limit) => covalence.Server.MaxPlayers = limit;
        #endregion

        #region Configuration
        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Starting Player Slots")]
            public int startPlayerSlots = 75;

            [JsonProperty(PropertyName = "Maximum Player Slots")]
            public int maxPlayerSlots = 125;

            [JsonProperty(PropertyName = "Increment Player Slots")]
            public int incrementPlayerSlots = 2;

            [JsonProperty(PropertyName = "Increment Interval Minutes")]
            public int incrementInterval = 3;

            [JsonProperty(PropertyName = "Increment When Slots Available")]
            public int incrementSlotsOpen = 0;

            [JsonProperty(PropertyName = "Only increment when FPS above")]
            public int fpsLimit = 45;

            [JsonProperty(PropertyName = "Auto decrease again")]
            public bool doAutoDecrease = true;
        }

        protected override void LoadConfig() {
            base.LoadConfig();

            try {
                config = Config.ReadObject<ConfigData>();
                if (config == null) LoadDefaultConfig();
            } catch {
                PrintError("Your config seems to be corrupted. Will load defaults.");
                LoadDefaultConfig();
                return;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() {
            config = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion
    }
}
