using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Dance", "senyaa", "1.0.4")]
    [Description("Plugin that allows players to use dance_01 gesture")]
    class Dance : RustPlugin
    {
        #region Configuration
        private class PluginConfig
        {
            public uint gestureID;
        }
        PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                gestureID = 834887525,
            };
        }

        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["notAllowed"] = "You are not allowed to use this command"
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["notAllowed"] = "У вас нет доступа к этой команде"
            }, this, "ru");
        }
        #endregion

        private void Init()
        {
            permission.RegisterPermission("dance.use", this);
            config = Config.ReadObject<PluginConfig>();
        }
        private Nullable<bool> CanUseGesture(BasePlayer player, GestureConfig gesture)
        {
            if (gesture.gestureId == config.gestureID && player.IPlayer.HasPermission("dance.use"))
                return true;
            return null;
        }

        [ChatCommand("dance")]
        private void DanceCommand(BasePlayer player)
        {
            if (!player.IPlayer.HasPermission("dance.use"))
            {
                player.IPlayer.Reply(lang.GetMessage("notAllowed", this, player.IPlayer.Id));
                return;
            }
            foreach (var gesture in player.gestureList.AllGestures)
            {
                if (gesture.gestureId == config.gestureID)
                {
                    player.Server_StartGesture(gesture);
                }
            }
        }
    }
}