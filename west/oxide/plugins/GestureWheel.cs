// Requires: ImageLibrary
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using Oxide.Core;
using UnityEngine;
using Color = UnityEngine.Color;

namespace Oxide.Plugins
{
    [Info("Gesture Wheel", "Tricky & Mevent, RFC1920", "0.1.4")]
    [Description("Convenient wheel that provides the ability to use gestures")]

    public class GestureWheel : RustPlugin
    {
        #region Plugin References
        [PluginReference]
        private Plugin ImageLibrary;
        #endregion

        #region Config
        Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Command")]
            public string Command = "gestures";

            [JsonProperty(PropertyName = "Use Permission")]
            public bool UsePermission = false;

            [JsonProperty(PropertyName = "Button Radius")]
            public int ButtonRadius = 100;

            [JsonProperty(PropertyName = "Close Button Color")]
            public string CloseButtonColor = "#FFB6B3DE";

            [JsonProperty(PropertyName = "Gesture Button Color")]
            public string GestureButtonColor = "#FF6666DE";

            [JsonProperty(PropertyName = "Gesture Button Size")]
            public int GestureButtonSize = 50;

            [JsonProperty(PropertyName = "Gestures", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Gesture> Gestures = new List<Gesture>
            {
                new Gesture
                {
                    Name = "wave",
                    Image = "https://i.imgur.com/pB3iZer.png"
                },

                new Gesture
                {
                    Name = "victory",
                    Image = "https://i.imgur.com/PLbSgED.png"
                },

                new Gesture
                {
                    Name = "shrug",
                    Image = "https://i.imgur.com/A3hHcgV.png"
                },

                new Gesture
                {
                    Name = "thumbsup",
                    Image = "https://i.imgur.com/yWuhCMu.png"
                },

                new Gesture
                {
                    Name = "chicken",
                    Image = "https://i.imgur.com/Qxhjf6N.png"
                },

                new Gesture
                {
                    Name = "hurry",
                    Image = "https://i.imgur.com/vVKVeha.png"
                },

                new Gesture
                {
                    Name = "whoa",
                    Image = "https://i.imgur.com/AFeGOrK.png"
                }
            };

            public class Gesture
            {
                [JsonProperty(PropertyName = "Gesture Name")]
                public string Name;

                [JsonProperty(PropertyName = "Image")]
                public string Image;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Stored Data
        private readonly string Layer = "UI_Emotions";
        private readonly string Perm = "gesturewheel.use";
        List<ulong> Players = new List<ulong>();
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            permission.RegisterPermission(Perm, this);
            cmd.AddConsoleCommand(config.Command, this, nameof(GesturesCommand));
            cmd.AddConsoleCommand("gestures.run", this, nameof(GesturesRunCommand));
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
            }
        }

        private void OnServerInitialized()
        {
            if (ImageLibrary == null)
            {
                PrintError("Image Library is not loaded, get it at https://umod.org/plugins/image-library");
                return;
            }

            ImageLibrary.Call("AddImage", "https://i.imgur.com/D40FoBT.png", "CloseImage");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/2fjUdcJ.png", "EmotionImage");

            for (int i = 0; i < config.Gestures.Count; i++)
            {
                ImageLibrary.Call("AddImage", config.Gestures[i].Image, config.Gestures[i].Name);
            }
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;
            //if(input.current.buttons > 0)
            //    Puts($"OnPlayerInput: {input.current.buttons}");
            // Shift-RightClick
            if (input.current.buttons == 2176)
            {
                Players.Remove(player.userID);
                player.SendConsoleCommand("gestures");
            }
        }
        #endregion

        #region Commands
        private void GesturesCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (config.UsePermission && !player.IPlayer.HasPermission(Perm)) return;

            if (!Players.Contains(player.userID))
            {
                UI_DrawInterface(player);
                Players.Add(player.userID);
            }
            else
            {
                CuiHelper.DestroyUi(player, Layer);
                Players.Remove(player.userID);
            }
        }

        private void GesturesRunCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (arg.Args.Length > 0)
            {
                player.SendConsoleCommand("gesture ", arg.Args[0]);

                CuiHelper.DestroyUi(player, Layer);
                Players.Remove(player.userID);
            }
        }
        #endregion

        #region User Interface
        private void UI_DrawInterface(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5" }
            }, "Overlay", Layer);

            LoadImage(ref container, Layer, Layer + ".Img", "CloseImage", oMin: "-30 -30", oMax: "30 30", color: HexToRustFormat(config.CloseButtonColor));

            container.Add(new CuiButton
            {
                Button = { Command = "gestures.close", Color = "0 0 0 0", Close = Layer },
                Text = { Text = "" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, Layer + ".Img");

            for (int i = 0; i < config.Gestures.Count; i++)
            {
                var emotion = config.Gestures[i];
                int r = config.Gestures.Count * 10 + config.ButtonRadius;
                var c = (double) config.Gestures.Count / 2;
                var pos = i / c * Math.PI;
                var x = r * Math.Sin(pos);
                var y = r * Math.Cos(pos);

                LoadImage(ref container, Layer, $"EmoButton.{i}", "EmotionImage", aMin: $"{x - config.GestureButtonSize} {y - config.GestureButtonSize}", aMax: $"{x + config.GestureButtonSize} {y + config.GestureButtonSize}", color: HexToRustFormat(config.GestureButtonColor));
                LoadImage(ref container, $"EmoButton.{i}", $"EmoButton.{i}.Img", emotion.Name, aMin: "0.5 0.5", aMax: "0.5 0.5", oMin: "-30 -30", oMax: "30 30");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Color = "0 0 0 0", Command = $"gestures.run {emotion.Name}" },
                    Text = { Text = "" }
                }, $"EmoButton.{i}");
            }

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        public void LoadImage(ref CuiElementContainer container, string parent, string name, string image, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string color = "1 1 1 1")
        {
            container.Add(new CuiElement
            {
                Name = name,
                Parent = parent,
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", image), Color = color },
                    new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax },
                }
            });
        }
        #endregion

        #region Helpers
        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');
            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);

            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }
        #endregion
    }
}
