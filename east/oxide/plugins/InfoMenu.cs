using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Info Menu", "Iv Misticos", "1.0.3")]
    [Description("Show server info and help with the ability to run popular commands")]
    class InfoMenu : RustPlugin
    {
        #region Variables

        private const float InitialLoadDelay = 10f;

        private bool _firstCached = false;
        private bool _firstCaching = false;

        private const string PermissionRecacheUI = "infomenu.recacheui";

        private const string CommandRecacheUI = "infomenu.recacheui";

        private static InfoMenu _ins;

        [PluginReference]
        // ReSharper disable once InconsistentNaming
        private Plugin ImageLibrary = null;

        [PluginReference]
        // ReSharper disable once InconsistentNaming
        private Plugin PlaceholderAPI = null;

        #endregion

        #region Configuration

        private static Configuration _config;

        internal class Configuration
        {
            [JsonProperty(PropertyName = "Tabs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Tab> Tabs = new List<Tab> {new Tab()};

            [JsonProperty(PropertyName = "Static Elements", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Button> Buttons = new List<Button> {new Button()};

            [JsonProperty(PropertyName = "Default Tab")]
            public string DefaultTab = "tab1";

            [JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Commands = new List<string> {"menu", "info", "help"};

            [JsonProperty(PropertyName = "Menu UI")]
            public UISettings UI = new UISettings();

            // ReSharper disable MemberCanBePrivate.Global
            public class UISettings
            {
                [JsonProperty(PropertyName = "Menu Position")]
                public UIData.Position MenuPosition = new UIData.Position
                {
                    Anchors = new UIData.Anchors
                    {
                        AnchorMinX = 0.5f,
                        AnchorMinY = 0.5f,
                        AnchorMaxX = 0.5f,
                        AnchorMaxY = 0.5f
                    },
                    Offsets = new UIData.Offsets
                    {
                        OffsetMinX = -400,
                        OffsetMinY = -300,
                        OffsetMaxX = 400,
                        OffsetMaxY = 300
                    }
                };

                [JsonProperty(PropertyName = "Menu Background Color")]
                public UIData.Colors MenuBackgroundColor = new UIData.Colors();

                [JsonProperty(PropertyName = "Menu Color")]
                public UIData.Colors MenuColor = new UIData.Colors();

                [JsonProperty(PropertyName = "Menu Title Background Position")]
                public UIData.Position MenuTitleBackgroundPosition = new UIData.Position();

                [JsonProperty(PropertyName = "Menu Title Background Color")]
                public UIData.Colors MenuTitleBackgroundColor = new UIData.Colors();

                [JsonProperty(PropertyName = "Menu Title Text")]
                public string MenuTitleText = "<size=3em>Info Menu</size>";

                [JsonProperty(PropertyName = "Menu Title Text Anchor")]
                [JsonConverter(typeof(StringEnumConverter))]
                public TextAnchor MenuTitleTextAnchor = TextAnchor.MiddleCenter;

                [JsonProperty(PropertyName = "Menu Title Placeholder API")]
                public bool MenuTitlePlaceholder = false;

                [JsonIgnore]
                public CuiElement ParsedMenuBackground;

                [JsonIgnore]
                public CuiElement ParsedMenuBackgroundButton;

                [JsonIgnore]
                public CuiElement ParsedMenu;

                [JsonIgnore]
                public CuiElement ParsedMenuTitleBackground;

                [JsonIgnore]
                public CuiElement ParsedMenuTitle;

                [JsonIgnore]
                public readonly string MenuName = "InfoMenu";

                [JsonIgnore]
                public readonly string MenuBackgroundName = "InfoMenu.Background";

                [JsonIgnore]
                public readonly string MenuBackgroundButtonName = "InfoMenu.Background.Button";

                [JsonIgnore]
                public readonly string MenuTitleBackgroundName = "InfoMenu.Title.Background";

                [JsonIgnore]
                public readonly string MenuTitleName = "InfoMenu.Title";

                #region Generation

                public CuiElement GetMenuBackground()
                {
                    var imageComponent = new CuiRawImageComponent();
                    if (MenuBackgroundColor.IsLink)
                    {
                        imageComponent.Png = ImageLibraryGet(MenuBackgroundName);
                    }
                    else
                    {
                        imageComponent.Color = MenuBackgroundColor.GetColor;
                        imageComponent.Sprite = MenuBackgroundColor.Sprite;
                        imageComponent.Material = MenuBackgroundColor.Material;
                    }

                    return new CuiElement
                    {
                        Name = MenuBackgroundName,
                        Parent = "Overlay",
                        Components =
                        {
                            imageComponent,
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.0 0.0",
                                AnchorMax = "1.0 1.0"
                            },
                            new CuiNeedsCursorComponent()
                        }
                    };
                }

                public CuiElement GetMenuBackgroundButton()
                {
                    return new CuiElement
                    {
                        Name = MenuBackgroundButtonName,
                        Parent = MenuBackgroundName,
                        Components =
                        {
                            new CuiButtonComponent
                            {
                                Color = "0.0 0.0 0.0 0.0",
                                Close = MenuBackgroundName
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.0 0.0",
                                AnchorMax = "1.0 1.0"
                            }
                        }
                    };
                }

                public CuiElement GetMenu()
                {
                    var imageComponent = new CuiRawImageComponent();
                    if (MenuColor.IsLink)
                    {
                        imageComponent.Png = ImageLibraryGet(MenuName);
                    }
                    else
                    {
                        imageComponent.Color = MenuColor.GetColor;
                        imageComponent.Sprite = MenuColor.Sprite;
                        imageComponent.Material = MenuColor.Material;
                    }

                    return new CuiElement
                    {
                        Name = MenuName,
                        Parent = MenuBackgroundButtonName,
                        Components =
                        {
                            imageComponent,
                            new CuiRectTransformComponent
                            {
                                AnchorMin = MenuPosition.Anchors.AnchorsMin,
                                AnchorMax = MenuPosition.Anchors.AnchorsMax,
                                OffsetMin = MenuPosition.Offsets.OffsetsMin,
                                OffsetMax = MenuPosition.Offsets.OffsetsMax
                            },
                            new CuiNeedsCursorComponent()
                        }
                    };
                }

                public CuiElement GetMenuTitleBackground()
                {
                    var imageComponent = new CuiRawImageComponent();
                    if (MenuTitleBackgroundColor.IsLink)
                    {
                        imageComponent.Png = ImageLibraryGet(MenuTitleBackgroundName);
                    }
                    else
                    {
                        imageComponent.Color =
                            MenuTitleBackgroundColor.GetColor;
                        imageComponent.Sprite = MenuTitleBackgroundColor.Sprite;
                        imageComponent.Material = MenuTitleBackgroundColor.Material;
                    }

                    return new CuiElement
                    {
                        Name = MenuTitleBackgroundName,
                        Parent = MenuName,
                        Components =
                        {
                            imageComponent,
                            new CuiRectTransformComponent
                            {
                                AnchorMin = _config.UI.MenuTitleBackgroundPosition.Anchors.AnchorsMin,
                                AnchorMax = _config.UI.MenuTitleBackgroundPosition.Anchors.AnchorsMax,
                                OffsetMin = _config.UI.MenuTitleBackgroundPosition.Offsets.OffsetsMin,
                                OffsetMax = _config.UI.MenuTitleBackgroundPosition.Offsets.OffsetsMax
                            },
                            new CuiNeedsCursorComponent()
                        }
                    };
                }

                public CuiElement GetMenuTitle(IPlayer player = null)
                {
                    return new CuiElement
                    {
                        Name = MenuTitleName,
                        Parent = MenuTitleBackgroundName,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Align = MenuTitleTextAnchor,
                                Text = MenuTitlePlaceholder ? ProcessPlaceholders(player, MenuTitleText) : MenuTitleText
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.0 0.0",
                                AnchorMax = "1.0 1.0"
                            }
                        }
                    };
                }

                #endregion
            }
            // ReSharper restore MemberCanBePrivate.Global

            public class Button
            {
                [JsonProperty(PropertyName = "Permission")]
                public string Permission = "infomenu.view";

                [JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<CommandData> Commands = new List<CommandData>
                {
                    new CommandData
                    {
                        Command = "menu",
                        Arguments = new object[] {"open", "tab1"}
                    }
                };

                [JsonProperty(PropertyName = "UI")]
                public ButtonUI UI = new ButtonUI();

                // ReSharper disable MemberCanBePrivate.Global
                public class ButtonUI
                {
                    [JsonProperty(PropertyName = "Background Color")]
                    public UIData.Colors Color = new UIData.Colors();

                    [JsonProperty(PropertyName = "Background Position")]
                    public UIData.Position Position = new UIData.Position();

                    [JsonProperty(PropertyName = "Text")]
                    public string Text = "Text";

                    [JsonProperty(PropertyName = "Text Anchor")]
                    [JsonConverter(typeof(StringEnumConverter))]
                    public TextAnchor TextAnchor = TextAnchor.MiddleCenter;

                    [JsonProperty(PropertyName = "Text Placeholder API")]
                    public bool TextPlaceholder = false;

                    [JsonIgnore]
                    public CuiElement ParsedButtonBackground;

                    [JsonIgnore]
                    public CuiElement ParsedButton;

                    [JsonIgnore]
                    public CuiElement ParsedButtonText;

                    [JsonIgnore]
                    public string CommandName = "infomenu." + Guid.NewGuid();

                    [JsonIgnore]
                    public string ButtonBackgroundName = "InfoMenu.Button.Background." + Guid.NewGuid();

                    [JsonIgnore]
                    public string ButtonName = "InfoMenu.Button." + Guid.NewGuid();

                    [JsonIgnore]
                    public string ButtonTextName = "InfoMenu.Button.Text." + Guid.NewGuid();

                    #region Generation

                    public CuiElement GetButtonBackground()
                    {
                        var imageComponent = new CuiRawImageComponent();
                        if (Color.IsLink)
                        {
                            imageComponent.Png = ImageLibraryGet(ButtonBackgroundName);
                        }
                        else
                        {
                            imageComponent.Color = Color.GetColor;
                            imageComponent.Sprite = Color.Sprite;
                            imageComponent.Material = Color.Material;
                        }

                        return new CuiElement
                        {
                            Name = ButtonBackgroundName,
                            Parent = _config.UI.MenuName,
                            Components =
                            {
                                imageComponent,
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = Position.Anchors.AnchorsMin,
                                    AnchorMax = Position.Anchors.AnchorsMax,
                                    OffsetMin = Position.Offsets.OffsetsMin,
                                    OffsetMax = Position.Offsets.OffsetsMax
                                },
                                new CuiNeedsCursorComponent()
                            }
                        };
                    }

                    public CuiElement GetButton()
                    {
                        return new CuiElement
                        {
                            Name = ButtonName,
                            Parent = ButtonBackgroundName,
                            Components =
                            {
                                new CuiButtonComponent
                                {
                                    Color = "0.0 0.0 0.0 0.0",
                                    Command = CommandName
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.0 0.0",
                                    AnchorMax = "1.0 1.0"
                                }
                            }
                        };
                    }

                    public CuiElement GetButtonText(IPlayer player = null)
                    {
                        return new CuiElement
                        {
                            Name = ButtonTextName,
                            Parent = ButtonName,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Align = TextAnchor,
                                    Text = TextPlaceholder ? ProcessPlaceholders(player, Text) : Text
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.0 0.0",
                                    AnchorMax = "1.0 1.0"
                                }
                            }
                        };
                    }

                    #endregion
                }
                // ReSharper restore MemberCanBePrivate.Global
            }

            public class Tab
            {
                [JsonProperty(PropertyName = "Technical Name")]
                public string Name = "tab1";

                [JsonProperty(PropertyName = "Permission")]
                public string Permission = "infomenu.view";
                
                [JsonProperty(PropertyName = "Elements", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<Button> Buttons = new List<Button> {new Button()};
            }

            public class CommandData
            {
                [JsonProperty(PropertyName = "Command")]
                public string Command = string.Empty;

                [JsonProperty(PropertyName = "Arguments", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public object[] Arguments = {"argument"};
            }
        }

        // ReSharper disable MemberCanBePrivate.Global
        internal class UIData
        {
            public class Colors
            {
                [JsonProperty(PropertyName = "Transparency")]
                public float Transparency = 1.0f;

                [JsonProperty(PropertyName = "Color")]
                public string Color = "#aaaaaa";

                [JsonProperty(PropertyName = "Link")]
                public string Link = string.Empty;

                [JsonProperty(PropertyName = "Sprite")]
                public string Sprite = "Assets/Content/UI/UI.Background.Tile.psd";

                [JsonProperty(PropertyName = "Material")]
                public string Material = "Assets/Icons/IconMaterial.mat";

                [JsonIgnore]
                public string GetColor => GetColor(Color, Transparency);

                [JsonIgnore]
                public bool IsLink => !string.IsNullOrEmpty(Link);
            }

            public class Position
            {
                [JsonProperty(PropertyName = "Anchors")]
                public Anchors Anchors = new Anchors();

                [JsonProperty(PropertyName = "Offsets")]
                public Offsets Offsets = new Offsets();
            }

            public class Anchors
            {
                [JsonProperty(PropertyName = "Anchor Min X")]
                public float AnchorMinX = 0.0f;

                [JsonProperty(PropertyName = "Anchor Min Y")]
                public float AnchorMinY = 0.0f;

                [JsonProperty(PropertyName = "Anchor Max X")]
                public float AnchorMaxX = 1.0f;

                [JsonProperty(PropertyName = "Anchor Max Y")]
                public float AnchorMaxY = 1.0f;

                [JsonIgnore]
                public string AnchorsMin => $"{AnchorMinX} {AnchorMinY}";

                [JsonIgnore]
                public string AnchorsMax => $"{AnchorMaxX} {AnchorMaxY}";
            }

            public class Offsets
            {
                [JsonProperty(PropertyName = "Offset Min X")]
                public int OffsetMinX = -50;

                [JsonProperty(PropertyName = "Offset Min Y")]
                public int OffsetMinY = -50;

                [JsonProperty(PropertyName = "Offset Max X")]
                public int OffsetMaxX = 50;

                [JsonProperty(PropertyName = "Offset Max Y")]
                public int OffsetMaxY = 50;

                [JsonIgnore]
                public string OffsetsMin => $"{OffsetMinX} {OffsetMinY}";

                [JsonIgnore]
                public string OffsetsMax => $"{OffsetMaxX} {OffsetMaxY}";
            }
        }
        // ReSharper restore MemberCanBePrivate.Global

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

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion

        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {
                    "Invalid Syntax", "Invalid command syntax. Usage:\n" +
                                      "open <Tab> - Open a tab\n" +
                                      "close - Close UI"
                },
                {"Only Players", "This command is available only for players."},
                {"No Tab", "Sorry, we couldn't find this tab (or you don't have enough permissions)."},
                {"No Recache Permission", "You don't have enough permissions (infomenu.recacheui)."},
                {"Recached", "All UI was successfully recached."},
                {"Initial Caching", "Please, wait. UI is caching. It can take up to 10 seconds and it will be opened automatically."}
            }, this);
        }

        private void Init()
        {
            _ins = this;
            
            permission.RegisterPermission(PermissionRecacheUI, this);

            foreach (var command in _config.Commands)
            {
                AddCovalenceCommand(command, nameof(CommandInfoMenu));
            }
            
            AddCovalenceCommand(CommandRecacheUI, nameof(CommandInfoMenuRecacheUI));
        }

        private void OnServerInitialized()
        {
            if (_config.UI.MenuBackgroundColor.IsLink)
            {
                ImageLibraryLoad(_config.UI.MenuBackgroundName, _config.UI.MenuBackgroundColor.Link);
            }

            if (_config.UI.MenuColor.IsLink)
            {
                ImageLibraryLoad(_config.UI.MenuName, _config.UI.MenuColor.Link);
            }

            if (_config.UI.MenuTitleBackgroundColor.IsLink)
            {
                ImageLibraryLoad(_config.UI.MenuTitleBackgroundName, _config.UI.MenuTitleBackgroundColor.Link);
            }

            foreach (var button in _config.Buttons)
            {
                SetupButton(button);
            }

            foreach (var tab in _config.Tabs)
            {
                // Check for existance to prevent load spam if same permissions used
                if (!string.IsNullOrEmpty(tab.Permission) && !permission.PermissionExists(tab.Permission))
                    permission.RegisterPermission(tab.Permission, this);

                foreach (var button in tab.Buttons)
                {
                    SetupButton(button);
                }
            }
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                InterfaceClose(player);
            }

            _ins = null;
            _config = null;
        }

        #endregion

        #region Commands

        private void CommandInfoMenuRecacheUI(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PermissionRecacheUI))
            {
                player.Reply(GetMsg("No Recache Permission", player.Id));
                return;
            }
            
            CacheUI();
            player.Reply(GetMsg("Recached", player.Id));
        }

        private void CommandInfoMenu(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
            {
                player.Reply(GetMsg("Only Players", player.Id));
                return;
            }

            if (args == null || args.Length == 0)
                args = new[] {"open"};

            switch (args[0].ToLower())
            {
                case "open":
                {
                    var tab = args.Length < 2 ? _config.DefaultTab : args[1];

                    InterfaceClose(basePlayer);
                    InterfaceShow(player, tab);
                    return;
                }

                case "close":
                {
                    InterfaceClose(basePlayer);
                    return;
                }

                default:
                {
                    goto invalidSyntax;
                }
            }

            invalidSyntax:
            player.Reply(GetMsg("Invalid Syntax", player.Id));
        }

        #endregion

        #region UI

        private void CacheUI()
        {
            _config.UI.ParsedMenuBackground = _config.UI.GetMenuBackground();
            _config.UI.ParsedMenuBackgroundButton = _config.UI.GetMenuBackgroundButton();
            
            _config.UI.ParsedMenu = _config.UI.GetMenu();
            _config.UI.ParsedMenuTitleBackground = _config.UI.GetMenuTitleBackground();

            if (!_config.UI.MenuTitlePlaceholder)
            {
                _config.UI.ParsedMenuTitle = _config.UI.GetMenuTitle();
            }

            foreach (var button in _config.Buttons)
            {
                CacheButton(button);
            }

            foreach (var tab in _config.Tabs)
            {
                foreach (var button in tab.Buttons)
                {
                    CacheButton(button);
                }
            }
            
            _firstCached = true;
        }

        private void InterfaceShow(IPlayer player, string tabName)
        {
            if (!_firstCached)
            {
                player.Reply(GetMsg("Initial Caching", player.Id));
                
                if (_firstCaching)
                    return;

                _firstCaching = true;
                
                timer.Once(InitialLoadDelay, () =>
                {
                    CacheUI();
                    InterfaceShow(player, tabName);
                });

                return;
            }

            Configuration.Tab selectedTab = null;
            foreach (var tab in _config.Tabs)
            {
                if (tab.Name != tabName || !string.IsNullOrEmpty(tab.Permission) &&
                    !player.HasPermission(tab.Permission))
                    continue;

                selectedTab = tab;
                break;
            }

            if (selectedTab == null)
            {
                player.Reply(GetMsg("No Tab", player.Id));
                return;
            }

            var container = new CuiElementContainer
            {
                // Menu background
                _config.UI.ParsedMenuBackground,

                // Menu background button
                _config.UI.ParsedMenuBackgroundButton,

                // Menu itself
                _config.UI.ParsedMenu,

                // Title background
                _config.UI.ParsedMenuTitleBackground,

                // Title text
                _config.UI.MenuTitlePlaceholder
                    ? _config.UI.GetMenuTitle(player)
                    : _config.UI.ParsedMenuTitle
            };

            foreach (var button in _config.Buttons)
            {
                InterfaceAddButton(player, container, button);
            }

            foreach (var button in selectedTab.Buttons)
            {
                InterfaceAddButton(player, container, button);
            }

            CuiHelper.AddUi(player.Object as BasePlayer, container);
        }

        private void InterfaceAddButton(IPlayer player, CuiElementContainer container, Configuration.Button button)
        {
            if (!string.IsNullOrEmpty(button.Permission) && !player.HasPermission(button.Permission))
                return;

            container.Add(button.UI.ParsedButtonBackground);
            container.Add(button.UI.ParsedButton);
            container.Add(button.UI.TextPlaceholder ? button.UI.GetButtonText(player) : button.UI.ParsedButtonText);
        }

        private void InterfaceClose(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, _config.UI.MenuBackgroundName);
        }

        private void SetupButton(Configuration.Button button)
        {
            // Check for existance to prevent load spam if same permissions used
            if (!string.IsNullOrEmpty(button.Permission) && !permission.PermissionExists(button.Permission))
                permission.RegisterPermission(button.Permission, this);

            cmd.AddConsoleCommand(button.UI.CommandName, this, arg =>
            {
                var basePlayer = arg.Player();
                if (basePlayer == null)
                    return false;

                foreach (var commandData in button.Commands)
                {
                    basePlayer.SendConsoleCommand(commandData.Command, commandData.Arguments);
                }

                return false;
            });
            
            if (button.UI.Color.IsLink)
            {
                ImageLibraryLoad(button.UI.ButtonBackgroundName, button.UI.Color.Link);
            }
        }

        private void CacheButton(Configuration.Button button)
        {
            button.UI.ParsedButtonBackground = button.UI.GetButtonBackground();
            button.UI.ParsedButton = button.UI.GetButton();

            if (!button.UI.TextPlaceholder)
            {
                button.UI.ParsedButtonText = button.UI.GetButtonText();
            }
        }

        #endregion

        #region Helpers

        private static string ProcessPlaceholders(IPlayer player, string text)
        {
            if (!_ins.PlaceholderAPILoaded())
            {
                Interface.Oxide.LogWarning("Info Menu requires Image Library for links support.");
                return text;
            }
            
            var builder = new StringBuilder(text);
            _ins.PlaceholderAPI?.CallHook("ProcessPlaceholders", player, builder);

            return builder.ToString();
        }

        private static string ImageLibraryGet(string name)
        {
            if (_ins.ImageLibraryLoaded())
            {
                return _ins.ImageLibrary.Call<string>("GetImage", name);
            }

            Interface.Oxide.LogWarning("Unable to get link for menu. Please, check whether Image Library is loaded.");
            return string.Empty;

        }

        private static void ImageLibraryLoad(string name, string link)
        {
            if (!_ins.ImageLibraryLoaded())
            {
                Interface.Oxide.LogWarning("Info Menu requires Image Library for links support.");
                return;
            }

            _ins.ImageLibrary.Call("AddImage", link, name, 0UL);
        }

        private bool ImageLibraryLoaded() => _ins.ImageLibrary != null && _ins.ImageLibrary.IsLoaded;
        
        private bool PlaceholderAPILoaded() => _ins.PlaceholderAPI != null && _ins.PlaceholderAPI.IsLoaded;

        private static string GetColor(string hex, float alpha)
        {
            var color = ColorTranslator.FromHtml(hex);
            var r = Convert.ToInt16(color.R) / 255f;
            var g = Convert.ToInt16(color.G) / 255f;
            var b = Convert.ToInt16(color.B) / 255f;

            return $"{r} {g} {b} {alpha}";
        }

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}