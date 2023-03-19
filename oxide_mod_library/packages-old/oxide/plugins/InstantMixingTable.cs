using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Instant Mixing Table", "MJSU", "1.0.0")]
    [Description("Allows players to instantly mix teas")]
    internal class InstantMixingTable : RustPlugin
    {
        #region Class Fields
        private PluginConfig _pluginConfig; //Plugin Config

        private const string PermBase = "instantmixingtable.";
        private const string UsePermission = PermBase + "use";
        #endregion

        #region Setup & Loading
        private void Init()
        {
            foreach (string perm in _pluginConfig.PlayerDurationMultiplier.Keys)
            {
                if (!perm.StartsWith(PermBase))
                {
                    PrintWarning($"Permissions {perm} was skipped. Permissions must start with ${PermBase}");
                    continue;
                }
                
                permission.RegisterPermission(perm, this);
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            config.PlayerDurationMultiplier = config.PlayerDurationMultiplier ?? new Hash<string, float>
            {
                [UsePermission] = 0f,
            };
            return config;
        }
        #endregion

        #region Hooks

        private void OnMixingTableToggle(MixingTable table, BasePlayer player)
        {
            if (table.IsOn())
            {
                return;
            }

            NextTick(() =>
            {
                foreach (KeyValuePair<string,float> permSpeed in _pluginConfig.PlayerDurationMultiplier.OrderBy(p => p.Value))
                {
                    if (HasPermission(player, permSpeed.Key))
                    {
                        table.RemainingMixTime *= permSpeed.Value;
                        table.TotalMixTime *= permSpeed.Value;
                        table.SendNetworkUpdateImmediate();
                        
                        if (table.RemainingMixTime < 1f)
                        {
                            table.CancelInvoke(table.TickMix);
                            table.Invoke(table.TickMix, table.RemainingMixTime);
                        }
                        
                        return;
                    }
                }
            });
        }
        #endregion

        #region Helper Methods
        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);
        #endregion

        #region Classes
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Player Duration Multiplier (0 = instant, 1 = normal speed)")]
            public Hash<string, float> PlayerDurationMultiplier { get; set; }
        }
        #endregion
    }
}
