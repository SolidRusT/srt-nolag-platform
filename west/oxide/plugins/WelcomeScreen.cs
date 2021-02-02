using System.Linq;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Welcome Screen", "Orange", "1.0.6")]
    [Description("Showing welcoming image on player joining")]
    public class WelcomeScreen : RustPlugin
    {
        #region Oxide Hooks

        private void OnPlayerConnected(BasePlayer player)
        {
            if (config.delay > 0)
            {
                timer.Once(config.delay, () => { CreateGUI(player); });
            }
            else
            {
                CreateGUI(player);
            }
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                CuiHelper.DestroyUi(player, elem);
            }
        }

        #endregion

        #region Commands

        [ChatCommand("welcomescreen")]
        private void Cmd(BasePlayer player)
        {
            OnPlayerConnected(player);
        }

        #endregion

        #region GUI

        private const string elem = "welcomescreen.main";

        private void CreateGUI(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = elem,
                FadeOut = config.fadeOut,
                Components =
                {
                    new CuiRawImageComponent {Color = $"1 1 1 {config.transparency}", FadeIn = config.fadeIn, Url = config.url},

                    config.anchorMin == "0 0" && config.anchorMax == "1 1" ? 
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-2000 -2000",
                        OffsetMax = "2000 2000",
                    } : 
                    new CuiRectTransformComponent
                    {
                        AnchorMin = config.anchorMin,
                        AnchorMax = config.anchorMax
                    },
                }
            });

            container.Add(new CuiElement
            {
                Parent = elem,
                Components =
                {
                    new CuiButtonComponent
                    {
                        Color = "0 0 0 0",
                        Close = elem
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    }
                }
            });

            CuiHelper.DestroyUi(player, elem);
            CuiHelper.AddUi(player, container);
            
            if (config.duration > 0)
            {
                timer.Once(config.duration, () => { CuiHelper.DestroyUi(player, elem); });
            }
        }

        #endregion

        #region Configuration | 24.05.2020

        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Image URL")]
            public string url = "https://i.imgur.com/RhMXzvF.jpg";
            
            [JsonProperty(PropertyName = "Fade-in duration")]
            public float fadeIn = 5f;
            
            [JsonProperty(PropertyName = "Fade-out duration")]
            public float fadeOut = 5f;
            
            [JsonProperty(PropertyName = "Delay after joining to create image")]
            public float delay = 10f;
            
            [JsonProperty(PropertyName = "Delay after creating image to start fade out")]
            public float duration = 20f;
            
            [JsonProperty(PropertyName = "Anchor min (left bottom coordinate)")]
            public string anchorMin = "0 0";
            
            [JsonProperty(PropertyName = "Anchor min (right top coordinate)")]
            public string anchorMax = "1 1";

            [JsonProperty(PropertyName = "Image transparency")]
            public float transparency = 1f;
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
                for (var i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }
                
                LoadDefaultConfig();
                return;
            }

            ValidateConfig();
            SaveConfig();
        }

        private static void ValidateConfig()
        {
            
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
    }
}