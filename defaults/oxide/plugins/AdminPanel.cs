using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/*======================================================================================================================= 
*
*   
*   20th november 2018
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   1.3.0   20181120    New maintainer (BuzZ)   added GUI button for set new tp pos (config for color, and bool for on/off)
*   1.4.0   20190609    toggle system FIX
*********************************************
*   Original author :   DaBludger on versions <1.3.0
*   Maintainer(s)   :   BuzZ since 20181116 from v1.3.0
*********************************************   
*
*=======================================================================================================================*/

/*
 * 1.4.6:
 * Added Player Administration button
 * Added missing localization to language API
 * Added adminpanel.autotoggle.admin permission - toggles panel when added/removed from the admin group
 * Added command `adminpanel settp` - sets the custom teleport
 * Added command `adminpanel settp all` - sets the teleport location for all admins without a custom location
 * Added command `adminpanel removetp` - removes the custom teleport location
 * Removed ToggleMode requirement to use `adminpanel toggle` console command
 * 
 * 1.4.5:
 * UI updates panel when player toggles godmode/vanish/radar
 * Requires AdminRadar 5.0.8+
 * 
 * 1.4.4:
 * Added support for AdminRadar 5.0+
 * 
 * 1.4.3:
 * Fixed `adminpanel toggle`
 * 
 * 1.4.2:
 *  Fixed issue with GUI on server startup @atope
 *  
 * 1.4.1:
 *  Fixed vanish permission
 *  Fixed NullReferenceException in console command: adminpanel
 *  Renamed deprecated hook OnPlayerDie to OnPlayerDeath
 *  Added unsubcribing and subscribing of hooks
 *  Fixed `/adminpanel show` glitching the game when AdminPanelToggleMode is true
 *  Fixed `/adminpanel show` not showing the GUI
 */

// https://umod.org/community/admin-panel/28638-ability-to-create-custom-buttons

namespace Oxide.Plugins
{
    [Info("Admin Panel", "nivex", "1.4.6")]
    [Description("GUI admin panel with command buttons")]
    class AdminPanel : RustPlugin
    {
        [PluginReference]
        private Plugin AdminRadar, Godmode, Vanish, PlayerAdministration;

        private const string permAdminPanel = "adminpanel.allowed";
        private const string permAdminRadar = "adminradar.allowed";
        private const string permGodmode = "godmode.toggle";
        private const string permVanish = "vanish.allow";
        private const string permPlayerAdministration = "playeradministration.access.show";
        private const string permAutoToggle = "adminpanel.autotoggle.admin";

        public Dictionary<BasePlayer, string> playerCUI = new Dictionary<BasePlayer, string>();

        #region Integrations

        public class StoredData
        {
            public Dictionary<string, string> TP = new Dictionary<string, string>();
            public StoredData() { }
        }

        public StoredData data = new StoredData();

        #region Player Administration

        private List<string> _playerAdministration = new List<string>();

        private bool IsPlayerAdministration(string UserID)
        {
            return PlayerAdministration != null && _playerAdministration.Contains(UserID);
        }

        private void TogglePlayerAdministration(BasePlayer player)
        {            
            if (PlayerAdministration == null || !PlayerAdministration.IsLoaded) return;

            if (IsPlayerAdministration(player.UserIDString))
            {
                player.Command("playeradministration.closeui", player, "playeradministration.closeui", new string[0]);
            }
            else
            {
                player.Command("padmin", player, "padmin", new string[0]);
            }
        }

        private void OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (player.IsValid() && permission.UserHasPermission(player.UserIDString, permPlayerAdministration))
            {
                if (command.Equals("padmin", StringComparison.OrdinalIgnoreCase) && !_playerAdministration.Contains(player.UserIDString))
                {
                    _playerAdministration.Add(player.UserIDString);
                    AdminGui(player);
                }
                else if (command.Equals("playeradministration.closeui", StringComparison.OrdinalIgnoreCase) && _playerAdministration.Contains(player.UserIDString))
                {
                    _playerAdministration.Remove(player.UserIDString);
                    AdminGui(player);
                }
            }
        }

        private void OnServerCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (player.IsValid() && permission.UserHasPermission(player.UserIDString, permPlayerAdministration))
            {
                string command = arg.cmd.FullName.Replace("/", string.Empty);
                
                if (command.Equals("padmin", StringComparison.OrdinalIgnoreCase) && !_playerAdministration.Contains(player.UserIDString))
                {
                    _playerAdministration.Add(player.UserIDString);
                    AdminGui(player);
                }
                else if (command.Equals("playeradministration.closeui", StringComparison.OrdinalIgnoreCase) && _playerAdministration.Contains(player.UserIDString))
                {
                    _playerAdministration.Remove(player.UserIDString);
                    AdminGui(player);
                }
            }
        }

        #endregion Player Administration

        #region Godmode

        private bool IsGod(string UserID)
        {
            return Godmode != null && Convert.ToBoolean(Godmode?.Call("IsGod", UserID));
        }

        private void ToggleGodmode(BasePlayer player)
        {
            if (Godmode == null || !Godmode.IsLoaded) return;

            if (IsGod(player.UserIDString))
                Godmode.Call("DisableGodmode", player.IPlayer);
            else
                Godmode.Call("EnableGodmode", player.IPlayer);

            AdminGui(player);
        }

        private void OnGodmodeToggle(string playerId, bool state)
        {
            var player = RustCore.FindPlayerByIdString(playerId);

            if (player.IsValid() && player.IsConnected && IsAllowed(player, permAdminPanel))
            {
                AdminGui(player);
            }
        }

        #endregion Godmode

        #region Vanish

        private bool IsInvisible(BasePlayer player)
        {
            return Vanish != null && Convert.ToBoolean(Vanish?.Call("IsInvisible", player));
        }

        private void ToggleVanish(BasePlayer player)
        {
            if (Vanish == null || !Vanish.IsLoaded) return;

            if (!IsInvisible(player))
                Vanish.Call("Disappear", player);
            else
                Vanish.Call("Reappear", player);

            AdminGui(player);
        }

        private void OnVanishDisappear(BasePlayer player)
        {
            if (player.IsValid() && player.IsConnected && IsAllowed(player, permAdminPanel))
            {
                AdminGui(player);
            }
        }

        private void OnVanishReappear(BasePlayer player)
        {
            if (player.IsValid() && player.IsConnected && IsAllowed(player, permAdminPanel))
            {
                AdminGui(player);
            }
        }

        #endregion Vanish

        #region Admin Radar

        private bool IsRadar(string id)
        {
            return AdminRadar != null && Convert.ToBoolean(AdminRadar?.Call("IsRadar", id));
        }

        private void ToggleRadar(BasePlayer player)
        {
            if (AdminRadar == null || !AdminRadar.IsLoaded) return;

            if (AdminRadar.Version < new Core.VersionNumber(5, 0, 0)) AdminRadar.Call("cmdESP", player, "radar", new string[0]);
            else if (player.IPlayer != null) AdminRadar.Call("RadarCommand", player.IPlayer, "radar", new string[0]);
            AdminGui(player);
        }

        private void OnRadarActivated(BasePlayer player)
        {
            if (player.IsValid() && player.IsConnected && IsAllowed(player, permAdminPanel))
            {
                AdminGui(player);
            }
        }

        private void OnRadarDeactivated(BasePlayer player)
        {
            if (player.IsValid() && player.IsConnected && IsAllowed(player, permAdminPanel))
            {
                AdminGui(player);
            }
        }

        #endregion Admin Radar

        #endregion Integrations

        private void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permAdminPanel, this);
            permission.RegisterPermission(permAutoToggle, this);
            Unsubscribe(nameof(OnPlayerSleepEnded));
            Unsubscribe(nameof(OnPlayerDeath));
        }

        private void OnUserGroupRemoved(string id, string group)
        {
            if (group != "admin" || !permission.UserHasPermission(id, permAutoToggle))
            {
                return;
            }

            var player = BasePlayer.FindAwakeOrSleeping(id);

            if (player == null || !player.IsConnected)
            {
                return;
            }

            DestroyUI(player);
        }

        private void OnUserGroupAdded(string id, string group)
        {
            if (group != "admin" || !permission.UserHasPermission(id, permAutoToggle))
            {
                return;
            }

            var player = BasePlayer.FindAwakeOrSleeping(id);

            if (player == null || !player.IsConnected || !IsAllowed(player, permAdminPanel))
            {
                return;
            }

            AdminGui(player);
        }

        #region Configuration

        private bool ToggleMode;
        private bool newtp;
        private string PanelPosMax;
        private string PanelPosMin;
        private string adminZoneCords;
        private string btnActColor;
        private string btnInactColor;
        private string btnNewtpColor;

        protected override void LoadDefaultConfig()
        {
            Config["AdminPanelToggleMode"] = ToggleMode = GetConfig("AdminPanelToggleMode", false);
            Config["AdminPanelPosMax"] = PanelPosMax = GetConfig("AdminPanelPosMax", "0.991 0.67");
            Config["AdminPanelPosMin"] = PanelPosMin = GetConfig("AdminPanelPosMin", "0.9 0.5");
            Config["AdminZoneCoordinates"] = adminZoneCords = GetConfig("AdminZoneCoordinates", "0;0;0;");
            Config["PanelButtonActiveColor"] = btnActColor = GetConfig("PanelButtonActiveColor", "0 2.55 0 0.3");
            Config["PanelButtonInactiveColor"] = btnInactColor = GetConfig("PanelButtonInactiveColor", "2.55 0 0 0.3");
            Config["PanelButtonNewtp"] = newtp = GetConfig("PanelButtonNewtp", false);
            Config["PanelButtonNewtpColor"] = btnNewtpColor = GetConfig("PanelButtonNewtpColor", "1.0 0.65 0.85 0.3");

            SaveConfig();
        }

        #endregion Configuration

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminTP"] = "Teleport",
                ["Godmode"] = "God",
                ["Radar"] = "Radar",
                ["Vanish"] = "Vanish",
                ["NewTP"] = "NewTP",
                ["PA"] = "Player Administration",
                ["Syntax"] = "Invalid syntax: /{0} {1}",
                ["No Custom Location Set"] = "You do not have a custom location set.",
                ["Removed Custom Location"] = "Removed your custom TP coordinates. You will teleport to the admin location instead.",
                ["Set Custom TP Coordinates"] = "Your TP coordinates set to current position {0}",
                ["Set Admin TP Coordinates"] = "Admin zone coordinates set to current position {0}",
                ["Panel Shown"] = "Admin panel refreshed/shown",
                ["Panel Hidden"] = "Admin panel hidden",
                ["Usage"] = "Usage: /{0} show/hide/settp/removetp",
            }, this);

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminTP"] = "Teleport",
                ["Godmode"] = "Dios",
                ["Radar"] = "Radar",
                ["Vanish"] = "Desaparecer",
                ["NewTP"] = "NewTP",
            }, this, "es");

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminTP"] = "Teleport",
                ["Godmode"] = "Dieu",
                ["Radar"] = "Radar",
                ["Vanish"] = "Invisible",
                ["NewTP"] = "NewTP",
            }, this, "fr");
        }

        #endregion Localization

        #region Hooks

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (IsAllowed(player, permAdminPanel))
            {
                AdminGui(player);
            }
        }

        private void OnPlayerDeath(BasePlayer player)
        {
            DestroyUI(player);
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "AdminRadar" || plugin.Name == "Godmode" || plugin.Name == "Vanish" || plugin.Name == "PlayerAdministration")
            {
                RefreshAllUI();
            }
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "AdminRadar" || plugin.Name == "Godmode" || plugin.Name == "Vanish" || plugin.Name == "PlayerAdministration")
            {
                RefreshAllUI();
            }
        }

        private void OnServerInitialized()
        {
            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {

            }

            if (data == null)
            {
                data = new StoredData();
                SaveData();
            }

            Subscribe(nameof(OnPlayerDeath));

            if (!ToggleMode)
            {
                Subscribe(nameof(OnPlayerSleepEnded));
                RefreshAllUI();
            }
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, data, true);
        }

        private void RefreshAllUI()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (IsAllowed(player, permAdminPanel))
                {
                    AdminGui(player);
                }
            }
        }

        #endregion Hooks

        #region Command Structure

        [ConsoleCommand("adminpanel")]
        private void ccmdAdminPanel(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !IsAllowed(player, permAdminPanel) || !arg.HasArgs()) return;

            switch (arg.Args[0].ToLower())
            {
                case "action":
                    {
                        if (arg.Args.Length >= 2)
                        {
                            switch (arg.Args[1].ToLower())
                            {
                                case "vanish":
                                    ToggleVanish(player);
                                    break;
                                case "radar":
                                    ToggleRadar(player);
                                    break;
                                case "god":
                                    ToggleGodmode(player);
                                    break;
                                case "pa":
                                    TogglePlayerAdministration(player);
                                    break;
                                case "admintp":
                                    if (data.TP.ContainsKey(player.UserIDString))
                                    {
                                        var pos = data.TP[player.UserIDString].ToVector3();
                                        player.Teleport(pos);
                                    }
                                    else
                                    {
                                        var pos = adminZoneCords.Split(';');
                                        var loc = new Vector3(float.Parse(pos[0]), float.Parse(pos[1]), float.Parse(pos[2]));
                                        player.Teleport(loc);
                                    }
                                    break;
                                case "newtp":
                                    if (newtp)
                                    {
                                        string[] argu = new string[1];
                                        argu[0] = "settp";
                                        ccmdAdminPanel(player, null, argu);
                                    }
                                    break;
                                default:
                                    arg.ReplyWith(_("Syntax", player.UserIDString, "adminpanel", "action vanish/admintp/radar/god/newtp"));
                                    break;
                            }
                        }
                        else
                        {
                            arg.ReplyWith(_("Syntax", player.UserIDString, "adminpanel", "action vanish/admintp/radar/god/newtp"));
                        }

                        break;
                    }
                case "toggle":
                    {
                        if (IsAllowed(player, permAdminPanel))
                        {
                            if (playerCUI.ContainsKey(player))
                            {
                                DestroyUI(player);
                            }
                            else
                            {
                                AdminGui(player);
                            }
                        }
                        break;
                    }
                default:
                    {
                        arg.ReplyWith(_("Syntax", player.UserIDString, "adminpanel", "action/toggle"));
                        break;
                    }
            }
        }

        [ChatCommand("adminpanel")]
        private void ccmdAdminPanel(BasePlayer player, string command, string[] args) // TODO: Make universal command
        {
            if (!IsAllowed(player, permAdminPanel))
            {
                SendReply(player, $"Unknown command: {command}");
                return;
            }

            if (args.Length == 0)
            {
                Message(player, "Usage", command);
                return;
            }

            switch (args[0].ToLower())
            {
                case "hide":
                    DestroyUI(player);
                    Message(player, "Panel Hidden");
                    break;

                case "show":
                    AdminGui(player);                    
                    Message(player, "Panel Shown");
                    break;

                case "settp":
                    Vector3 coord = player.transform.position;
                    if (args.Any(arg => arg.ToLower() == "all"))
                    {
                        Config["AdminZoneCoordinates"] = adminZoneCords = $"{coord.x};{coord.y};{coord.z}";
                        Config.Save();                        
                        Message(player, "Set Admin TP Coordinates", coord);
                    }
                    else
                    {
                        data.TP[player.UserIDString] = coord.ToString();
                        SaveData();
                        Message(player, "Set Custom TP Coordinates", coord);
                    }
                    break;

                case "removetp":
                    if (data.TP.Remove(player.UserIDString))
                    {
                        Message(player, "Removed Custom Location");
                        SaveData();
                    }
                    else Message(player, "No Custom Location Set");
                    break;

                default:
                    Message(player, "Syntax", command, args[0]);
                    break;
            }
        }

        #endregion Command Structure

        #region GUI Panel

        private void AdminGui(BasePlayer player)
        {
            NextTick(() =>
            {
                // Destroy existing UI
                DestroyUI(player);

                var BTNColorVanish = btnInactColor;
                var BTNColorGod = btnInactColor;
                var BTNColorRadar = btnInactColor;
                var BTNColorNewTP = btnNewtpColor;
                var BTNColorPA = btnInactColor;

                if (AdminRadar != null && IsRadar(player.UserIDString)) BTNColorRadar = btnActColor;
                if (Godmode != null && IsGod(player.UserIDString)) BTNColorGod = btnActColor;
                if (Vanish != null && IsInvisible(player)) BTNColorVanish = btnActColor;
                if (_playerAdministration.Contains(player.UserIDString)) BTNColorPA = btnActColor;

                var GUIElement = new CuiElementContainer();

                var GUIPanel = GUIElement.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "1 1 1 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = PanelPosMin,
                        AnchorMax = PanelPosMax
                    },
                    CursorEnabled = false
                }, "Hud", Name);

                if (AdminRadar != null && permission.UserHasPermission(player.UserIDString, permAdminRadar))
                {
                    GUIElement.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "adminpanel action radar",
                            Color = BTNColorRadar
                        },
                        Text =
                        {
                            Text = _("Radar", player.UserIDString),
                            FontSize = 8,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.062 0.21",
                            AnchorMax = "0.51 0.37"
                        }
                    }, GUIPanel);
                }

                if (PlayerAdministration != null && permission.UserHasPermission(player.UserIDString, permPlayerAdministration))
                {
                    GUIElement.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "adminpanel action pa",
                            Color = BTNColorPA
                        },
                        Text =
                        {
                            Text = _("PA", player.UserIDString),
                            FontSize = 8,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.062 0.39",
                            AnchorMax = "0.51 0.555"
                        }
                    }, GUIPanel);
                }

                GUIElement.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "adminpanel action admintp",
                        Color = "1.28 0 1.28 0.3"
                    },
                    Text =
                    {
                        Text = _("AdminTP", player.UserIDString),
                        FontSize = 8,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.52 0.21",
                        AnchorMax = "0.95 0.37"
                    }
                }, GUIPanel);

                if (newtp)
                {
                    GUIElement.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "adminpanel action newtp",
                            Color = BTNColorNewTP
                        },
                        Text =
                        {
                            Text = _("newTP", player.UserIDString),
                            FontSize = 8,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.52 0.39",
                            AnchorMax = "0.95 0.47"
                        }
                    }, GUIPanel);
                }

                if (Godmode != null && permission.UserHasPermission(player.UserIDString, permGodmode))
                {
                    GUIElement.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "adminpanel action god",
                            Color = BTNColorGod
                        },
                        Text =
                        {
                            Text = _("Godmode", player.UserIDString),
                            FontSize = 8,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.52 0.02",
                            AnchorMax = "0.95 0.19"
                        }
                    }, GUIPanel);
                }

                if (Vanish != null && permission.UserHasPermission(player.UserIDString, permVanish))
                {
                    GUIElement.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "adminpanel action vanish",
                            Color = BTNColorVanish
                        },
                        Text =
                        {
                            Text = _("Vanish", player.UserIDString),
                            FontSize = 8,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.062 0.02",
                            AnchorMax = "0.51 0.19"
                        }
                    }, GUIPanel);
                }

                CuiHelper.AddUi(player, GUIElement);
                playerCUI.Add(player, GUIPanel);
            });
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            string cuiElement;
            if (playerCUI.TryGetValue(player, out cuiElement))
            {
                CuiHelper.DestroyUi(player, cuiElement);
                playerCUI.Remove(player);
            }
        }

        #endregion GUI Panel

        #region Helpers

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        private bool IsAllowed(BasePlayer player, string perm) => player != null && permission.UserHasPermission(player.UserIDString, perm);

        private string _(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(BasePlayer player, string key, params object[] args) => Player.Message(player, _(key, player.UserIDString, args));

        #endregion Helpers
    }
}
