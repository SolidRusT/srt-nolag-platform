using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Instant H2O", "Lincoln", "1.0.3")]
    [Description("Instantly fills water catchers upon placement")]

    public class InstantH2O : RustPlugin
    {
        List<ulong> playerList = new List<ulong>();
        private const string permUse = "InstantH2O.use";

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Enabled"] = "<color=#ffc34d>InstantH2O</color>: Instantly fill water catchers <color=#b0fa66>Enabled</color>.",
                ["Disabled"] = "<color=#ffc34d>InstantH2O</color>: Instantly fill water catchers <color=#ff6666>Disabled</color>.",
                ["noPerm"] = "<color=#ffc34d>InstantH2O</color>: You do not have permissions to use this.",
                ["Reload"] = "<color=#ffc34d>InstantH2O</color>: Instantly fill water catchers <color=#ff6666>disabled</color> due to a plugin reload.",
            }, this);
        }
        #endregion
        private void OnServerInitialized()
        {
            permission.RegisterPermission(permUse, this);
        }
        private bool HasPermission(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permUse)) return true;

            else return false;
        }
        [ChatCommand("h2o")]
        private void H2OCMD(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!HasPermission(player))
            {
                player.ChatMessage(lang.GetMessage("noPerm", this, player.UserIDString));
                return;
            }
            if (playerList.Contains(player.userID))
            {
                playerList.Remove(player.userID);
                player.ChatMessage(lang.GetMessage("Disabled", this, player.UserIDString));
            }
            else
            {
                playerList.Add(player.userID);
                player.ChatMessage(lang.GetMessage("Enabled", this, player.UserIDString));

            }
            return;
        }
        private void OnEntitySpawned(WaterCatcher entity)
        {
            var catcher = UnityEngine.Object.FindObjectOfType<LiquidContainer>();
            if (entity == null) return;
            var player = entity.OwnerID;
            if (player == 0) return;
            if (playerList.Contains(player))
            {
                catcher.maxOutputFlow = 100;
                Timer myTimer = null;
                myTimer = timer.Every(1.0f, () =>
                {
                    if (entity == null) myTimer.Destroy();
                    else entity.AddResource(100000000);
                });

            }
            //Puts($"Did not find {player} in the list"); ONLY FOR DEBUGGING
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (playerList.Contains(player.userID))
                {
                    playerList.Clear();
                    player.ChatMessage(lang.GetMessage("Reload", this, player.UserIDString));
                }
            }
        }
    }
}