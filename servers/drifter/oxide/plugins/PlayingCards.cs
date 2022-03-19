using Newtonsoft.Json;
using System.Collections.Generic;
using System;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("PlayingCards", "k1lly0u", "0.1.2")]
    [Description("Casino image management system")]
    class PlayingCards : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin ImageLibrary;

        private const string URI = "https://www.rustedit.io/images/casino/";

        public static bool IsReady { get; private set; }

        public static PlayingCards Instance { get; private set; }

        public static event Action OnImagesReady;

        public static HashSet<string> CardGames;
        #endregion

        #region Oxide Hooks
        private void OnServerInitialized()
        {
            Instance = this;
            IsReady = false;
            CardGames = new HashSet<string>();
            timer.In(5f, () => ImportCardImages());
        }
        
        private void Unload()
        {
            IsReady = false;
            CardGames = null;
            Instance = null;
        }
        #endregion

        #region Import Images
        private void ImportCardImages(int attempts = 0)
        {
            if (!ImageLibrary)
            {
                if (attempts > 3)
                {
                    PrintError("This plugin requires ImageLibrary to manage the card images. Please install ImageLibrary to continue...");
                    return;
                }

                timer.In(10, () => ImportCardImages(attempts++));
                return;
            }

            Dictionary<string, Dictionary<ulong, string>> loadOrder = new Dictionary<string, Dictionary<ulong, string>>();

            string[] values = new string[] { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
            string[] suits = new string[] { "C", "D", "H", "S" };
            int[] chips = new int[] { 1, 10, 50, 100, 500 };

            for (int i = 0; i < suits.Length; i++)
            {
                for (int j = 0; j < values.Length; j++)
                {
                    loadOrder.Add($"{values[j]}{suits[i]}", new Dictionary<ulong, string>() { [0U] = $"{URI}{values[j]}{suits[i]}.png" });
                }
            }
            loadOrder.Add("blue_back", new Dictionary<ulong, string>() { [0U] = $"{URI}blue_back.png" });
            loadOrder.Add("gray_back", new Dictionary<ulong, string>() { [0U] = $"{URI}gray_back.png" });
            loadOrder.Add("green_back", new Dictionary<ulong, string>() { [0U] = $"{URI}green_back.png" });
            loadOrder.Add("purple_back", new Dictionary<ulong, string>() { [0U] = $"{URI}purple_back.png" });
            loadOrder.Add("red_back", new Dictionary<ulong, string>() { [0U] = $"{URI}red_back.png" });
            loadOrder.Add("yellow_back", new Dictionary<ulong, string>() { [0U] = $"{URI}yellow_back.png" });

            foreach(string cardgame in CardGames)                
                loadOrder.Add($"board_{cardgame}", new Dictionary<ulong, string>() { [0U] = $"{URI}board_{cardgame}.png" });
            
            loadOrder.Add("betting_stack", new Dictionary<ulong, string>() { [0U] = $"{URI}betting_stack.png" });

            for (int i = 0; i < chips.Length; i++)
            {
                loadOrder.Add($"chip_{chips[i]}", new Dictionary<ulong, string>() { [0U] = $"{URI}chip_{chips[i]}.png" });
            }

            ImageLibrary.Call("ImportItemList", "Casino - Playing card imagery", loadOrder, configData.ForceUpdate, (Action)OnImagesLoaded);  
            
            if (configData.ForceUpdate)
            {
                configData.ForceUpdate = false;
                SaveConfig();
            }
        }

        private void OnImagesLoaded()
        {
            IsReady = true;
            OnImagesReady?.Invoke();
        }

        public static void AddImage(string imageName, string fileName) => Instance.ImageLibrary.Call("AddImage", fileName, imageName, 0U);

        public static string GetCardImage(string value, string suit) => (string)Instance.ImageLibrary?.Call("GetImage", $"{value}{suit}");

        public static string GetChipImage(int value) => (string)Instance.ImageLibrary?.Call("GetImage", $"chip_{value}");

        public static string GetChipStackImage() => (string)Instance.ImageLibrary?.Call("GetImage", "betting_stack");

        public static string GetBoardImage(string gameType) => (string)Instance.ImageLibrary?.Call("GetImage", $"board_{gameType}");

        public static string GetCardBackground(string color) => (string)Instance.ImageLibrary?.Call("GetImage", $"{color}_back");
        #endregion

        #region Config        
        private ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Force image update on load")]
            public bool ForceUpdate { get; set; }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                ForceUpdate = false,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion
    }
}
