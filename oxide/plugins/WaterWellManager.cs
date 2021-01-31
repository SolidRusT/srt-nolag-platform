using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("WaterWellManager", "Krungh Crow", "2.0.0")]
    [Description("Configure how the water wells work")]

    #region Changelogs and ToDo
    /*************************************************************
    * 
    * Thx to    : redBDGR the original creator of this plugin
    * 
    * 2.0.0     : Complete rewrite
    * 
    **************************************************************/
    #endregion

    class WaterWellManager : CovalencePlugin
    {
        #region Configuration
        void Init()
        {
            if (!LoadConfigVariables())
            {
            Puts("Config file issue detected. Please delete file, or check syntax and fix.");
            return;
            }
        }

        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public SettingsWaterWell WellChecks = new SettingsWaterWell();
        }

        class SettingsWaterWell
        {
            [JsonProperty(PropertyName = "Calories needed Per Pump")]
            public float Calories = 5f;
            [JsonProperty(PropertyName = "Pressure per pump")]
            public float Pressure = 0.2f;
            [JsonProperty(PropertyName = "Pressure needed to pump")]
            public float PressureNeeded = 1f;
            [JsonProperty(PropertyName = "Water output per pump")]
            public int Output = 100;
        }

        private bool LoadConfigVariables()
        {
            try
            {
            configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
            return false;
            }
            SaveConf();
            return true;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Fresh install detected Creating a new config file.");
            configData = new ConfigData();
            SaveConf();
        }

        void SaveConf() => Config.WriteObject(configData, true);
        #endregion

        #region Main
        private void OnServerInitialized()
        {
            UpdateRoutine();
        }

        void Unload()
        {
            VanillaRoutine();
        }

        private void UpdateRoutine()
        {
            foreach (WaterWell well in UnityEngine.Object.FindObjectsOfType<WaterWell>())

            if (well != null)
            {
                well.caloriesPerPump = configData.WellChecks.Calories;
                well.pressurePerPump = configData.WellChecks.Pressure;
                well.pressureForProduction = configData.WellChecks.PressureNeeded;
                well.waterPerPump = configData.WellChecks.Output;
                well.SendNetworkUpdateImmediate();
            }
        }

        private void VanillaRoutine()
        {
            foreach (WaterWell well in UnityEngine.Object.FindObjectsOfType<WaterWell>())

            if (well != null)
            {
                well.caloriesPerPump = 5f;
                well.pressurePerPump = 0.2f;
                well.pressureForProduction = 1f;
                well.waterPerPump = 50;
                well.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }
        }
        #endregion
    }
}