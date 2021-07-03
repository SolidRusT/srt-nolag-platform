using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("FPS Restart", "RustySpoon342", "1.1")]
    [Description("Restarts the server when FPS reaches a specific target")]
    public class FPSRestart : CovalencePlugin
    {
        private Timer timerObject;

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
            timerObject = timer.Every(60, FrameRate);
        }

        private void Unload()
        {
            config = null;
            timerObject.Destroy();
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

        private void FrameRate()
        {
            string msg = string.Format(lang.GetMessage("RestartMessage", this));

           float args = config.RestartTime;

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
            timerObject.Destroy();
        }

        #endregion
    }
}
