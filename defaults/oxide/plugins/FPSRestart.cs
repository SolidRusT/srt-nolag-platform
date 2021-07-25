using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("FPS Restart", "RustySpoon342", "1.2.0")]
    [Description("Restarts the server when FPS reaches a specific target")]
    public class FPSRestart : CovalencePlugin
    {
        private Timer timerAborted;
        private Timer timerFirstCheck;
        private Timer timerLastCheck;

        #region Configuration

        private ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "FPS To Trigger Restart")]
            public float FrameRate = 100;

            [JsonProperty(PropertyName = "How Long The Restart Should Be")]
            public float RestartTime = 300;

            [JsonProperty("Show Restart Message To Server")]
            public bool ShowMessage = true;
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

                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            timerFirstCheck = timer.Every(300, FramerateFirstCheck);
        }

        private void Unload()
        {
            config = null;
            timerFirstCheck.Destroy();
            timerLastCheck.Destroy();
            timerAborted.Destroy();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["RestartMessage"] = "The Server Has Detected Low FPS That May Cause Lag. A Restart Has Begun, stash yo' loot!",
            }, this);
        }
        #endregion

        #region Core

        private void FramerateFirstCheck()
        {
            if (Performance.report.frameRate > config.FrameRate)
            {
                return;
            }

            timerLastCheck = timer.Once(60, FramerateLastCheck);
        }

        private void FramerateLastCheck()
        {
           string msg = string.Format(lang.GetMessage("RestartMessage", this));

           float args = config.RestartTime;

           float args2 = config.RestartTime + 60;

            if (Performance.report.frameRate > config.FrameRate)
            {
                return;
            }
           
            if (config.ShowMessage)
            {
             server.Broadcast(msg);
            }
            
            LogWarning("The Server Has Detected Low FPS That May Cause Lag. A Restart Has Begun!");
            server.Command("restart", args);
            timerLastCheck.Destroy();
            timerAborted = timer.Once(args2, OnServerInitialized);
        }

        #endregion
    }
}


//  Copyright (C) <2021>  <RustySpoon342>
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses></https:>.
