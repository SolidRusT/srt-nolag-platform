using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core;
using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Simple Time", "MadKingCraig", "1.1.2")]
    [Description("Provides a chat command for the current game time")]
    public class SimpleTime : RustPlugin
    {
        private bool _initialized;
        private int _componentSearchAttempts;

        private const string CanUsePermission = "simpletime.use";

        #region Oxide Hooks
        private void Init()
        {
            permission.RegisterPermission(CanUsePermission, this);
        }

        private void Loaded()
        {
            _initialized = false;
        }

        private void OnServerInitialized()
        {
            if (TOD_Sky.Instance == null)
            {
                _componentSearchAttempts++;
                if (_componentSearchAttempts < 10)
                    timer.Once(1, OnServerInitialized);
                else
                    PrintWarning("Could not find required component after 10 attempts. Plugin disabled");
                return;
            }
            if (TOD_Sky.Instance.Components.Time == null)
            {
                PrintWarning("Could not fetch time component. Plugin disabled");
                return;
            }

            _initialized = true;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Time"] = "Current Time: {0}",
                ["NoPermission"] = "You do not have permission to use this command."
            }, this);
        }
        #endregion

        #region Hooks
        [HookMethod("GetSimpleTime")]
        public string GetSimpleTime()
        {
            return TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm");
        }
        #endregion

        #region Commands
        [ChatCommand("time")]
		private void TimeCommand(BasePlayer player, string command, string[] args)
		{
            if (!_initialized)
                return;

            string PlayerID = player.UserIDString;

            if (!permission.UserHasPermission(PlayerID, CanUsePermission))
            {
                SendReply(player, lang.GetMessage("NoPermission", this, PlayerID));
                return;
            }

            string currentTime = TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm");
            SendReply(player, string.Format(lang.GetMessage("Time", this, PlayerID), currentTime));
        }
        #endregion
    }
}
