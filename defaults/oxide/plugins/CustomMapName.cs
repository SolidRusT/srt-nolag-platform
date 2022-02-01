using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Steamworks;

namespace Oxide.Plugins
{
    [Info("Custom Map Name", "Ryz0r", "1.0.1")]
    [Description("Allows you to edit the Custom Map Name field without using a custom map.")]
    public class CustomMapName : RustPlugin
    {
        private Configuration _config;
        protected override void SaveConfig() => Config.WriteObject(_config);
        protected override void LoadDefaultConfig() => _config = new Configuration();
        private string _cachedMapName;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Map Name Displayed")]
            public string MapNameDisplayed = "Your Map Name";

            [JsonProperty(PropertyName = "Cycle Map Names")]
            public bool CycleMapNames = false;

            [JsonProperty(PropertyName = "Map Names to Cycle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] MapNamesToCycle = {"Map Name 1", "Welcome To Our Server", "Map Name 2"};

            [JsonProperty(PropertyName = "Name Refresh Interval")]
            public float NameRefreshInterval = 5.0f;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        private void Init()
        {
            timer.Every(_config.NameRefreshInterval, () =>
            {
                var nameToSet = "";
                if (_config.CycleMapNames)
                {
                    if (NoNames())
                    {
                        PrintError("Cycle Map Names is turned on, but no map names to cycle");
                        return;
                    }
                    
                    var currName = SteamServer.MapName;
                    var newName = _config.MapNamesToCycle[new Random().Next(0, _config.MapNamesToCycle.Length)];
                    if (newName == currName) newName = _config.MapNamesToCycle[new Random().Next(0, _config.MapNamesToCycle.Length)];

                    nameToSet = newName;
                }
                else
                {
                    nameToSet = _config.MapNameDisplayed;
                }

                if (ContainsOfficial(nameToSet))
                {
                    PrintError("Using the word official in your map name can get you blacklisted. We can not allow this to happen.");
                    return;
                }

                if (TooShort(nameToSet))
                {
                    PrintError("Requested map name to be displayed should be at least 1 character long.");
                    return;
                }

                _cachedMapName = nameToSet;
            });
        }

        private void OnServerInformationUpdated()
        {
            SteamServer.MapName = _cachedMapName;
        }

        private static bool ContainsOfficial(string name) => name.ToLower().Contains("official");
        private static bool TooShort(string name) => name.Length < 1;
        private bool NoNames() => _config.MapNamesToCycle.Length < 1;
    }
}