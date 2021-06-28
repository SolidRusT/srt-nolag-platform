using Oxide.Game.Rust.Cui;
using UnityEngine;
using System;
using Rust;
using System.Collections.Generic;
using Oxide.Core;
using Newtonsoft.Json;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Chill Night Vision", "Thisha", "0.0.1")]
    [Description("Visual support for night goggles")]

    public class ChillNightVision : RustPlugin
    {
        private const string usePermission = "chillnightvision.use";
        private const string modifyPermission = "chillnightvision.modify";
        
        private const string inviteNoticeMsg = "assets/bundled/prefabs/fx/invite_notice.prefab";
        private const string gogglesShortName = "nightvisiongoggles";

        private const string defaultPictureURL = "https://cdn.discordapp.com/attachments/504359168312868874/808277427498254356/RustachillNightVision.png";
        private float pictureOffset = 0.012f; //0.022f;
        
        public DateTime lastCleanUp;

        public enum ShowValues { All, Bar, Value };

        public enum AllColors
        {
            AliceBlue = 28,
            AntiqueWhite = 29,
            Aqua = 30,
            Aquamarine = 31,
            Azure = 32,
            Beige = 33,
            Bisque = 34,
            Black = 35,
            BlanchedAlmond = 36,
            Blue = 37,
            BlueViolet = 38,
            Brown = 39,
            BurlyWood = 40,
            CadetBlue = 41,
            Chartreuse = 42,
            Chocolate = 43,
            Coral = 44,
            CornflowerBlue = 45,
            Cornsilk = 46,
            Crimson = 47,
            Cyan = 48,
            DarkBlue = 49,
            DarkCyan = 50,
            DarkGoldenrod = 51,
            DarkGray = 52,
            DarkGreen = 53,
            DarkKhaki = 54,
            DarkMagenta = 55,
            DarkOliveGreen = 56,
            DarkOrange = 57,
            DarkOrchid = 58,
            DarkRed = 59,
            DarkSalmon = 60,
            DarkSeaGreen = 61,
            DarkSlateBlue = 62,
            DarkSlateGray = 63,
            DarkTurquoise = 64,
            DarkViolet = 65,
            DeepPink = 66,
            DeepSkyBlue = 67,
            DimGray = 68,
            DodgerBlue = 69,
            Firebrick = 70,
            FloralWhite = 71,
            ForestGreen = 72,
            Fuchsia = 73,
            Gainsboro = 74,
            GhostWhite = 75,
            Gold = 76,
            Goldenrod = 77,
            Gray = 78,
            Green = 79,
            GreenYellow = 80,
            Honeydew = 81,
            HotPink = 82,
            IndianRed = 83,
            Indigo = 84,
            Ivory = 85,
            Khaki = 86,
            Lavender = 87,
            LavenderBlush = 88,
            LawnGreen = 89,
            LemonChiffon = 90,
            LightBlue = 91,
            LightCoral = 92,
            LightCyan = 93,
            LightGoldenrodYellow = 94,
            LightGray = 95,
            LightGreen = 96,
            LightPink = 97,
            LightSalmon = 98,
            LightSeaGreen = 99,
            LightSkyBlue = 100,
            LightSlateGray = 101,
            LightSteelBlue = 102,
            LightYellow = 103,
            Lime = 104,
            LimeGreen = 105,
            Linen = 106,
            Magenta = 107,
            Maroon = 108,
            MediumAquamarine = 109,
            MediumBlue = 110,
            MediumOrchid = 111,
            MediumPurple = 112,
            MediumSeaGreen = 113,
            MediumSlateBlue = 114,
            MediumSpringGreen = 115,
            MediumTurquoise = 116,
            MediumVioletRed = 117,
            MidnightBlue = 118,
            MintCream = 119,
            MistyRose = 120,
            Moccasin = 121,
            NavajoWhite = 122,
            Navy = 123,
            OldLace = 124,
            Olive = 125,
            OliveDrab = 126,
            Orange = 127,
            OrangeRed = 128,
            Orchid = 129,
            PaleGoldenrod = 130,
            PaleGreen = 131,
            PaleTurquoise = 132,
            PaleVioletRed = 133,
            PapayaWhip = 134,
            PeachPuff = 135,
            Peru = 136,
            Pink = 137,
            Plum = 138,
            PowderBlue = 139,
            Purple = 140,
            Red = 141,
            RosyBrown = 142,
            RoyalBlue = 143,
            SaddleBrown = 144,
            Salmon = 145,
            SandyBrown = 146,
            SeaGreen = 147,
            SeaShell = 148,
            Sienna = 149,
            Silver = 150,
            SkyBlue = 151,
            SlateBlue = 152,
            SlateGray = 153,
            Snow = 154,
            SpringGreen = 155,
            SteelBlue = 156,
            Tan = 157,
            Teal = 158,
            Thistle = 159,
            Tomato = 160,
            Turquoise = 161,
            Violet = 162,
            Wheat = 163,
            White = 164,
            WhiteSmoke = 165,
            Yellow = 166,
            YellowGreen = 167,
        }

        #region localization
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["HelpInfo"] = "Use \"/cnv <on|off>\" to show hide the night vision panel.",
                ["HelpShow"] = "Use \"/cnv show <alerts|ranges>\" to see your active alerts/colors.",
                ["HelpView"] = "Use \"/cnv view <bar|value|all>\" to switch view.",
                ["HelpColor"] = "Use \"/cnv colors help\" to show more info about color usage.",
                ["HelpColorList"] = "Use \"/cnv colors list\" to see a list of the available color names.",
                ["HelpAlertAdd"] = "Use \"/cnv add alert <number>\" to add or update an alert",
                ["HelpRangeAdd"] = "Use \"/cnv add range <number> <barcolor> <fontcolor>\" to add or update a color range.",
                ["HelpDelete"] = "Use \"/cnv remove <alert|range> <number>\" to delete a certain alert or range",
                ["HelpReset"] = "Use \"/cnv reset\" to reset your settings to defaults.",
                ["NoUsePermission"] = "You do not have permission to use the night vision information.",
                ["NoModifyPermission"] = "You do not have permission to modify night vision settings.",
                ["InvalidAlert"] = "Invalid alert value.",
                ["InvalidColor"] = "Invalid color value.",
                ["InvalidRGB"] = "Invalid RGB value.",
                ["InvalidNumberValue"] = "Invalid number value",
                ["NoAlerts"] = "You don't have any active alerts.",
                ["AlertAdded"] = "The alert has been added.",
                ["AlertUpdated"] = "The alert has been updated.",
                ["AlertRemoved"] = "The alert has been removed.",
                ["AlertDoesNotExists"] = "The alert does not exist.",
                ["NoRanges"] = "You don't have any color ranges.",
                ["RangeAdded"] = "The color range has been added.",
                ["RangeUpdated"] = "The color range has been updated.",
                ["RangeRemoved"] = "The color range has been removed.",
                ["RangeDoesNotExists"] = "The color range does not exist.",
                ["MaxAlerts"] = "You have reached the maximum number of alerts.",
                ["DataReset"] = "Your data has been reset to defaults.",
                ["ColorUsage"] = "Colors can be defined by a hexadecimal value, for example #FC03F0.\nTransparancy from FF (none) to 00 (full) can be added, for example FC03F0FF.\nColors can be defined by name too (case-sensitive), use \"/cnv colors list\" to view them.\nWhen using color names, the transparancy will be default.",
                ["ServerDefaults"] = "Server defaults",
                ["NoServerAlerts"] = "No alerts",
            }, this);
        }
        #endregion localization

        #region data
        private Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();

        private class PlayerData
        {
            public bool Enabled;
            public string ShowInfo = string.Empty;
            public DateTime LastChange = DateTime.Today;
            public DateTime LastOnline = DateTime.Today;

            public Dictionary<uint, SimpleRangeData> ColorRanges = new Dictionary<uint, SimpleRangeData>();
            public List<uint> Alerts = new List<uint>();

            public PlayerData()
            {

            }
        }
        #endregion data

        #region config
        private ConfigData config;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Lower Range Color Value")]
            public SimpleRangeData LowerRange = new SimpleRangeData
            {
                BarColorValue = "#bce8df",
                FontColorValue = "#FFFFFFFF"
            };

            [JsonProperty(PropertyName = "Middle Range")]
            public FullRangeData MiddleRange = new FullRangeData
            {
                BarColorValue = "#80e8d3",
                StartValue = 300,
                FontColorValue = "#FFFFFFFF"
            };

            [JsonProperty(PropertyName = "Higher Range")]
            public FullRangeData HigherRange = new FullRangeData
            {
                BarColorValue = "#34ebc6",
                StartValue = 600,
                FontColorValue = "#FFFFFFFF"
            };

            [JsonProperty(PropertyName = "Alert 1")]
            public uint Alert1 = 0;

            [JsonProperty(PropertyName = "Show picture")]
            public bool ShowPicture = true;

            [JsonProperty(PropertyName = "Picture URL")]
            public string PictureURL = "https://i.imgur.com/eZdRSQU.png";

            [JsonProperty(PropertyName = "Show info (All, Bar, Value)")]
            public string ShowInfo = ShowValues.All.ToString();

            [JsonProperty(PropertyName = "Postition")]
            public AnchorPosition Position = new AnchorPosition
            {
                XAxis = 0.035f,
                YAxis = 0.007f
            };

            [JsonProperty(PropertyName = "Maximum Player Alerts")]
            public uint MaxAlerts = 6;

            [JsonProperty(PropertyName = "Remove after offline days")]
            public uint OffDays = 0;
        }

        private class FullRangeData
        {
            [JsonProperty(PropertyName = "Starting Value")]
            public uint StartValue = 0;

            [JsonProperty(PropertyName = "Bar Color")]
            public string BarColorValue = "#00000000";

            [JsonProperty(PropertyName = "Font Color")]
            public string FontColorValue = "#00000000";
        }

        private class AnchorPosition
        {
            [JsonProperty(PropertyName = "X-axis")]
            public float XAxis = 0;

            [JsonProperty(PropertyName = "Y-axis")]
            public float YAxis = 0;
        }

        private class SimpleRangeData
        {
            [JsonProperty(PropertyName = "Bar Color")]
            public string BarColorValue = "#00000000";

            [JsonProperty(PropertyName = "Font Color")]
            public string FontColorValue = "#00000000";

            public SimpleRangeData()
            {

            }

            public SimpleRangeData(string barcolor, string fontcolor)
            {
                BarColorValue = barcolor;
                FontColorValue = fontcolor;
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                    throw new Exception();

                SaveConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }

        }
        #endregion config

        #region commands
        [ChatCommand("cnv")]
        void HandleChatcommand(BasePlayer player, string command, string[] args)
        {
            bool doUpdate = false;

            if (!permission.UserHasPermission(player.UserIDString, usePermission))
            {
                PrintToChat(player, Lang("NoUsePermission", player.UserIDString));
                return;
            }

            switch (args.Length)
            {
                case 1:
                    {
                        switch (args[0].ToLower())
                        {
                            case "on":
                                {
                                    ShowOxygen(player);
                                    break;
                                }

                            case "off":
                                {
                                    HideOxygen(player);
                                    break;
                                }

                            case "reset":
                                {
                                    if (!permission.UserHasPermission(player.UserIDString, modifyPermission))
                                    {
                                        PrintToChat(player, Lang("NoModifyPermission", player.UserIDString));
                                        return;
                                    }
                                    InitPlayer(player.userID, true);
                                    player.ChatMessage(Lang("DataReset", player.UserIDString));

                                    Item item;
                                    if (IsWearingGoggles(player, out item))
                                        UpdatePanels(player, item.condition, item.maxCondition, true);

                                    break;
                                }

                            default:
                                {
                                    ShowCommandHelp(player);
                                    break;
                                }
                        }
                        break;
                    }

                case 2:
                    {
                        switch (args[0].ToLower())
                        {
                            case "show":
                                {
                                    switch (args[1].ToLower())
                                    {
                                        case "alerts":
                                            { }
                                            player.ChatMessage(GetAlerts(player.userID));
                                            break;

                                        case "ranges":
                                            {
                                                player.ChatMessage(GetRanges(player.userID));
                                                break;
                                            }

                                        default:
                                            {
                                                PrintToChat(player, Lang("HelpShow", player.UserIDString));
                                                break;
                                            }
                                    }
                                    break;
                                }

                            case "view":
                                {
                                    if (!permission.UserHasPermission(player.UserIDString, modifyPermission))
                                    {
                                        PrintToChat(player, Lang("NoModifyPermission", player.UserIDString));
                                        return;
                                    }

                                    switch (args[1].ToLower())
                                    {
                                        case "bar":
                                            {
                                                ChangeView(player.userID, args[1], out doUpdate);
                                                break;
                                            }

                                        case "value":
                                            {
                                                ChangeView(player.userID, args[1], out doUpdate);
                                                break;
                                            }

                                        case "all":
                                            {
                                                ChangeView(player.userID, args[1], out doUpdate);
                                                break;
                                            }

                                        default:
                                            {
                                                PrintToChat(player, Lang("HelpView", player.UserIDString));
                                                break;
                                            }
                                    }
                                    break;
                                }

                            case "colors":
                                {
                                    switch (args[1].ToLower())
                                    {
                                        case "help":
                                            {
                                                PrintToChat(player, Lang("ColorUsage", player.UserIDString));
                                                break;
                                            }

                                        case "list":
                                            {
                                                PrintToChat(player, ColorList());
                                                break;
                                            }

                                        default:
                                            {
                                                PrintToChat(player, Lang("HelpColor", player.UserIDString));
                                                break;
                                            }
                                    }
                                    break;
                                }


                            default:
                                ShowCommandHelp(player);
                                break;
                        }
                        break;
                    }

                case 3:
                    {
                        if (!permission.UserHasPermission(player.UserIDString, modifyPermission))
                        {
                            PrintToChat(player, Lang("NoModifyPermission", player.UserIDString));
                            return;
                        }

                        switch (args[0].ToLower())
                        {
                            case "add":
                                {
                                    switch (args[1].ToLower())
                                    {
                                        case "alert":
                                            {
                                                player.ChatMessage(Lang(AddAlert(player.userID, args[2])));
                                                break;
                                            }

                                        default:
                                            {
                                                ShowCommandHelp(player);
                                                break;
                                            }
                                    }
                                    break;
                                }

                            case "remove":
                                {
                                    switch (args[1].ToLower())
                                    {
                                        case "alert":
                                            {
                                                player.ChatMessage(Lang(RemoveAlert(player.userID, args[2])));
                                                break;
                                            }

                                        case "range":
                                            {
                                                player.ChatMessage(Lang(RemoveRange(player.userID, args[2], out doUpdate)));
                                                break;
                                            }

                                        default:
                                            {
                                                ShowCommandHelp(player);
                                                break;
                                            }
                                    }
                                    break;
                                }

                            default:
                                ShowCommandHelp(player);
                                break;
                        }
                        break;
                    }

                case 5:
                    {
                        if (!permission.UserHasPermission(player.UserIDString, modifyPermission))
                        {
                            PrintToChat(player, Lang("NoModifyPermission", player.UserIDString));
                            return;
                        }

                        switch (args[0].ToLower())
                        {
                            case "add":
                                {
                                    switch (args[1].ToLower())
                                    {
                                        case "range":
                                            {
                                                string[] pars = new string[3] { args[2], args[3], args[4] };
                                                player.ChatMessage(Lang(AddRange(player.userID, pars, out doUpdate)));
                                                break;
                                            }

                                        default:
                                            {
                                                ShowCommandHelp(player);
                                                break;
                                            }
                                    }
                                    break;
                                }

                            default:
                                {
                                    ShowCommandHelp(player);
                                    break;
                                }

                        }
                        break;
                    }

                default:
                    {
                        ShowCommandHelp(player);
                        break;
                    }
            };

            if (doUpdate)
            {
                Item item;
                if (PlayerSignedUp(player))
                    if (IsWearingGoggles(player, out item))
                        UpdatePanels(player, item.condition, item.maxCondition, true);
            }

        }
        #endregion commands

        #region hooks
        private void Init()
        {
            permission.RegisterPermission(usePermission, this);
            permission.RegisterPermission(modifyPermission, this);
            LoadData();
        }

        void OnServerInitialized()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (!PlayerSignedUp(player))
                    continue;

                if (!player.IsAlive())
                    continue;

                Item item;
                if (IsWearingGoggles(player, out item))
                {
                    UpdatePanels(player, item.condition, item.maxCondition, true);
                }
            }
        }

        void OnServerSave()
        {
            RemoveOldData();
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (PlayerSignedUp(player))
                    DestroyUI(player, true);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (playerData.ContainsKey(player.userID))
            {
                playerData[player.userID].LastOnline = DateTime.Today;
                SaveData();
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!PlayerSignedUp(player))
                return;

            Item item;
            if (IsWearingGoggles(player, out item))
                UpdatePanels(player, item.condition, item.maxCondition, true);
        }

        void OnPlayerDeath(BasePlayer player, ref HitInfo info)
        {
            if (!PlayerSignedUp(player))
                return;

            Item item2;
            if (IsWearingGoggles(player, out item2))
                DestroyUI(player, true);
        }

        void OnItemAddedToContainer(ItemContainer cont, Item item)
        {
            if (ShouldHandleUI(cont, item))
                UpdatePanels(cont.playerOwner, item.condition, item.maxCondition, true);
        }

        void OnItemRemovedFromContainer(ItemContainer cont, Item item)
        {
            if (ShouldHandleUI(cont, item))
                DestroyUI(cont.GetOwnerPlayer(), true);
        }

        void OnEntityTakeDamage(BasePlayer player, ref HitInfo info)
        {
            if (info.damageTypes.Get(DamageType.Drowned) <= 0f)
                return;

            if (!PlayerSignedUp(player))
                return;

            if (!player.IsAlive())
                return;

            Item item;
            if (IsWearingGoggles(player, out item))
            {
                UpdatePanels(player, item.condition, item.maxCondition, true);
            }
        }

        void OnLoseCondition(Item item, ref float amount)
        {
            if (!item.info.shortname.Equals(gogglesShortName))
                return;

            BasePlayer player = item.GetOwnerPlayer();

            if (!PlayerSignedUp(player))
                return;

            UpdatePanels(player, item.condition - 3f, item.maxCondition, false);
        }

        void OnItemAction(Item item, string action, BasePlayer player)
        {
            if (!action.ToUpper().Contains("REFILL"))
                return;

            Item item2;
            if ((item.info.shortname.Equals(gogglesShortName)) && (IsWearingGoggles(player, out item2)))
            {
                float maxLossFraction = 0.05f;
                float tempMax = 0;
                float single = 1f - item.condition / item.maxCondition;
                maxLossFraction = Mathf.Clamp(maxLossFraction, 0f, item.maxCondition);
                tempMax = (float)Math.Floor(item.maxCondition * (1f - maxLossFraction * single));

                UpdatePanels(player, tempMax, tempMax, false);
            }
        }

        #endregion hooks

        #region functions
        #region general
        void RemoveOldData()
        {
            if (lastCleanUp >= DateTime.Today)
                return;

            if (config.OffDays <= 0)
                return;

            List<ulong> ToDelete = new List<ulong>();

            for (int i = 0; i < playerData.Count; i++)
            {
                if ((DateTime.Today - playerData.ElementAt(i).Value.LastOnline).TotalDays > config.OffDays)
                {
                    ToDelete.Add(playerData.ElementAt(i).Key);
                }
            }

            if (ToDelete.Count > 0)
            {
                for (int i = 0; i < ToDelete.Count; i++)
                    playerData.Remove(ToDelete[i]);

            }

            SaveData(); //catch save of new player data lastonline

            lastCleanUp = DateTime.Today;
        }

        void InitPlayer(ulong userID, bool reset)
        {
            if (reset)
            {
                playerData.Remove(userID);
                SaveData();
                return;
            }

            PlayerData data = new PlayerData();
            data.Enabled = true;
            data.ShowInfo = config.ShowInfo;

            data.ColorRanges.Add(0, config.LowerRange);
            data.ColorRanges.Add(config.MiddleRange.StartValue, new SimpleRangeData(config.MiddleRange.BarColorValue, config.MiddleRange.FontColorValue));
            data.ColorRanges.Add(config.HigherRange.StartValue, new SimpleRangeData(config.HigherRange.BarColorValue, config.HigherRange.FontColorValue));

            if (config.Alert1 > 0)
                data.Alerts.Add(config.Alert1);

            playerData[userID] = data;
            SaveData();
        }

        void HideOxygen(BasePlayer player)
        {
            if (!playerData.ContainsKey(player.userID))
                InitPlayer(player.userID, false);

            playerData[player.userID].Enabled = false;
            playerData[player.userID].LastChange = DateTime.Today;
            SaveData();

            DestroyUI(player, true);
        }

        void ShowOxygen(BasePlayer player)
        {
            PlayerData info;
            if (!playerData.TryGetValue(player.userID, out info))
            {
                InitPlayer(player.userID, false);
            }
            else
            {
                info.Enabled = true;
                playerData[player.userID].LastChange = DateTime.Today;
                SaveData();

                Item item;
                if (IsWearingGoggles(player, out item))
                    UpdatePanels(player, item.condition, item.maxCondition, true);
            }
        }
        #endregion general

        #region alerts
        string GetAlerts(ulong userID)
        {
            string alerts = string.Empty;

            if (playerData.ContainsKey(userID))
            {
                if (playerData[userID].Alerts.Count > 0)
                {
                    playerData[userID].Alerts.Sort();
                    foreach (var alert in playerData[userID].Alerts)
                    {
                        if (alerts.Length > 0)
                            alerts = alerts + ", " + alert.ToString();
                        else
                            alerts = alert.ToString();
                    }
                }

                if (alerts.Length == 0)
                {
                    alerts = Lang("NoAlerts", userID.ToString());
                }
            }
            else
            {
                alerts = Lang("NoAlerts", userID.ToString()) + '\n' + GetDefaultAlerts();
            }

            return alerts;
        }

        string GetDefaultAlerts()
        {
            string alerts = string.Empty;

            if (config.Alert1 > 0)
                alerts = (alerts + ", " + config.Alert1.ToString()).TrimStart(' ', ',');

            if (alerts.Length > 0)
                return (Lang("ServerDefaults") + ": " + alerts);
            else
                return (Lang("ServerDefaults") + ": " + Lang("NoServerAlerts"));
        }

        string AddAlert(ulong userID, string args)
        {
            string result = String.Empty;

            bool continuous = args.Substring(args.Length - 1) == "*";

            if (continuous)
            {
                args = args.Remove(args.Length - 1);
            }

            uint condition;
            if (uint.TryParse(args, out condition))
            {
                if (!playerData.ContainsKey(userID))
                    InitPlayer(userID, false);

                if (!playerData[userID].Alerts.Contains(condition))
                {
                    if ((playerData[userID].Alerts.Count < config.MaxAlerts) || (config.MaxAlerts == 0))
                    {
                        playerData[userID].LastChange = DateTime.Today;
                        SaveData();
                        result = "AlertAdded";
                    }
                    else
                    {
                        result = "MaxAlerts";
                    }
                }
                else
                {
                    playerData[userID].LastChange = DateTime.Today;
                    SaveData();
                    result = "AlertUpdated";
                }
            }
            else
            {
                result = "InvalidAlert";
            }

            return (result);
        }

        string RemoveAlert(ulong userID, string args)
        {
            string result = string.Empty;
            uint condition;

            if (uint.TryParse(args, out condition))
            {
                if (playerData[userID].Alerts.Remove(condition))
                {
                    playerData[userID].LastChange = DateTime.Today;
                    SaveData();
                    result = "AlertRemoved";
                }
                else
                {
                    result = "AlertDoesNotExists";
                }
            }
            else
            {
                result = "InvalidAlert";
            };

            return result;
        }

        bool MustDoEffect(float condition, ulong userID)
        {
            uint alert = 0;

            if (playerData.ContainsKey(userID))
            {
                alert = playerData[userID].Alerts.Where(x => x >= condition).OrderBy(x => x).FirstOrDefault();

                if (alert > 0)
                {
                    return (condition == alert);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return (config.Alert1 > 0) && (condition == config.Alert1);
            }

        }
        #endregion alerts

        #region ranges
        string GetRanges(ulong userID)
        {
            string ranges = string.Empty;

            if (playerData.ContainsKey(userID))
            {
                if (playerData[userID].ColorRanges.Count > 0)
                {
                    foreach (var range in playerData[userID].ColorRanges.OrderBy(x => x.Key))
                    {
                        if (ranges.Length > 0)
                            ranges = ranges + "\n" + range.Key.ToString() + " B: " + range.Value.BarColorValue + " F: " + range.Value.FontColorValue;
                        else
                            ranges = range.Key.ToString() + " B: " + range.Value.BarColorValue + " F: " + range.Value.FontColorValue;
                    }
                }
            }

            if (ranges.Length == 0)
            {
                ranges = Lang("NoRanges", userID.ToString()) + '\n' + Lang("ServerDefaults") + ": " + '\n' + GetDefaultRanges();
            }


            return ranges;
        }

        string GetDefaultRanges()
        {
            return ("0 " + "B: " + config.LowerRange.BarColorValue + " F: " + config.LowerRange.FontColorValue +
                '\n' + config.MiddleRange.StartValue.ToString() + " B: " + config.MiddleRange.BarColorValue + " F: " + config.LowerRange.FontColorValue +
                '\n' + config.HigherRange.StartValue.ToString() + " B: " + config.HigherRange.BarColorValue + " F: " + config.HigherRange.FontColorValue);
        }

        string AddRange(ulong userID, string[] args, out bool doUpdate)
        {
            string result = string.Empty;
            string barRGB = string.Empty;
            string fontRGB = string.Empty;
            uint startCond = 0;
            doUpdate = false;

            if (IsValidColor(args[1], out barRGB))
            {
                if (IsValidColor(args[2], out fontRGB))
                {
                    if (uint.TryParse(args[0], out startCond))
                    {
                        if (!playerData.ContainsKey(userID))
                            InitPlayer(userID, false);

                        SimpleRangeData range;
                        if (!playerData[userID].ColorRanges.TryGetValue(startCond, out range))
                        {
                            playerData[userID].ColorRanges.Add(startCond, new SimpleRangeData(args[1], args[2]));
                            playerData[userID].LastChange = DateTime.Today;
                            SaveData();
                            result = "RangeAdded";
                        }
                        else
                        {
                            playerData[userID].ColorRanges[startCond].BarColorValue = args[1];
                            playerData[userID].ColorRanges[startCond].FontColorValue = args[2];
                            playerData[userID].LastChange = DateTime.Today;
                            SaveData();
                            result = "RangeUpdated";
                        }
                        doUpdate = true;
                    }
                    else
                        result = "InvalidNumberValue";
                }
                else
                    result = "InvalidColor";
            }
            else
                result = "InvalidColor";

            return result;
        }

        string RemoveRange(ulong userID, string args, out bool doUpdate)
        {
            string result = string.Empty;
            uint condition;
            doUpdate = false;

            if (uint.TryParse(args, out condition))
            {
                if (playerData[userID].ColorRanges.Remove(condition))
                {
                    playerData[userID].LastChange = DateTime.Today;
                    SaveData();
                    result = "RangeRemoved";
                    doUpdate = true;
                }
                else
                {
                    result = "RangeDoesNotExists";
                }
            }
            else
            {
                result = "InvalidNumberValue";
            };

            return result;
        }

        void GetRGBValues(ulong userID, uint condition, out string barRGB, out string fontRGB)
        {
            barRGB = string.Empty;
            fontRGB = string.Empty;

            bool getFromconfig = true;
            uint startCond = 0;
            SimpleRangeData range;

            if (playerData.ContainsKey(userID))
            {
                startCond = playerData[userID].ColorRanges.Where(x => x.Key <= condition).OrderBy(x => x.Key).LastOrDefault().Key;

                if (playerData[userID].ColorRanges.TryGetValue(startCond, out range))
                {
                    barRGB = range.BarColorValue;
                    fontRGB = range.FontColorValue;
                    getFromconfig = false;
                }
            }

            if (getFromconfig)
            {
                if (condition < config.MiddleRange.StartValue)
                {
                    barRGB = config.LowerRange.BarColorValue;
                    fontRGB = config.LowerRange.FontColorValue;
                }
                else if ((condition >= config.MiddleRange.StartValue) && (condition < config.HigherRange.StartValue))
                {
                    barRGB = config.MiddleRange.BarColorValue;
                    fontRGB = config.MiddleRange.FontColorValue;
                }
                else
                {
                    barRGB = config.HigherRange.BarColorValue;
                    fontRGB = config.HigherRange.FontColorValue;
                }
            }

            if (barRGB[0] != '#')
                barRGB = ColorToRGB(barRGB);
            if (fontRGB[0] != '#')
                fontRGB = ColorToRGB(fontRGB);
        }
        #endregion ranges

        #region UI
        void ChangeView(ulong userID, string show, out bool doUpdate)
        {
            doUpdate = false;

            if (!playerData.ContainsKey(userID))
                InitPlayer(userID, false);

            if (playerData[userID].ShowInfo != show)
            {
                playerData[userID].ShowInfo = show;
                playerData[userID].LastChange = DateTime.Today;
                SaveData();
                doUpdate = true;
            }
        }

        bool ShouldHandleUI(ItemContainer cont, Item item)
        {
            if (!item.info.shortname.Equals(gogglesShortName))
                return false;

            if (cont.playerOwner == null)
                return false;

            if (!cont.playerOwner.IsAlive())
                return false;

            if (!PlayerSignedUp(cont.playerOwner))
                return false;

            if (IsWearablesContainer(cont))
                return true;

            return false;
        }

        void UpdatePanels(BasePlayer player, float condition, float maxCondition, bool doPicture)
        {
            if (!permission.UserHasPermission(player.UserIDString, usePermission))
                return;

            string barColor;
            string fontColor;
            string valueText;
            string barText;

            if (condition < 0)
                condition = 0;

            GetRGBValues(player.userID, (uint)Math.Floor(condition), out barColor, out fontColor);

            double lifepct = condition / maxCondition * 1000;

            if (MustDoEffect(condition, player.userID))
                Effect.server.Run(inviteNoticeMsg, player.transform.position, Vector3.zero, null, false);

            int counter = 0;
            int numberofchars = (int)Math.Round(lifepct);

            counter = Convert.ToInt32(config.Position.XAxis * 10000);

            if (condition != 0)
                counter = counter + numberofchars;

            if (MustShowValue(player.userID))
                counter = counter + 150;

            if (config.ShowPicture)
                counter = counter + 120;

            barText = "0." + counter.ToString("".PadLeft(4, '0')) + " " + (config.Position.YAxis + 0.023f).ToString();
            valueText = ((int)Math.Round(condition, 0)).ToString();

            DestroyUI(player, doPicture);
            DrawUI(player, ColorFromHex(barColor, 228), valueText, barText, ColorFromHex(fontColor, 255), doPicture);
        }

        void DestroyUI(BasePlayer player, bool updatePicture)
        {
            if (player == null)
                return;

            CuiHelper.DestroyUi(player, "cnvPanel");
            CuiHelper.DestroyUi(player, "cnvBar");

            if (updatePicture)
                CuiHelper.DestroyUi(player, "cnvPicture");
        }

        void DrawUI(BasePlayer player, string color, string valueText, string barText, string fontColor, bool updatePicture)
        {
            CuiHelper.AddUi(player, Generate_Menu(player, color, valueText, barText, fontColor, updatePicture));
        }

        private bool MustShowBar(ulong userID)
        {
            if (playerData.ContainsKey(userID))
                return (playerData[userID].ShowInfo.ToLower() == ShowValues.All.ToString().ToLower()) || (playerData[userID].ShowInfo.ToLower() == ShowValues.Bar.ToString().ToLower());
            else
                return (config.ShowInfo.ToLower() == ShowValues.All.ToString().ToLower()) || (config.ShowInfo.ToLower() == ShowValues.Bar.ToString().ToLower());
        }

        private bool MustShowValue(ulong userID)
        {
            if (playerData.ContainsKey(userID))
                return (playerData[userID].ShowInfo.ToLower() == ShowValues.All.ToString().ToLower()) || (playerData[userID].ShowInfo.ToLower() == ShowValues.Value.ToString().ToLower());
            else
                return (config.ShowInfo.ToLower() == ShowValues.All.ToString().ToLower()) || (config.ShowInfo.ToLower() == ShowValues.Value.ToString().ToLower());
        }

        private string GetAnchorMax(ulong userID)
        {
            float X = config.Position.XAxis;
            if (config.ShowPicture)
                X = X + pictureOffset;

            float Y = config.Position.YAxis;

            if (MustShowBar(userID))
                X = X + 0.116f;
            else
                X = X + 0.013f;

            if (!MustShowValue(userID))
                X = X - 0.015f;

            Y = Y + 0.025f;

            return X.ToString() + " " + Y.ToString();
        }

        CuiElementContainer Generate_Menu(BasePlayer player, string barColor, string valueText, string barText, string fontColor, bool updatePicture)
        {
            var elements = new CuiElementContainer();

            string contMin = config.Position.XAxis.ToString() + " " + config.Position.YAxis.ToString();
            if (config.ShowPicture)
                contMin = (config.Position.XAxis + pictureOffset).ToString() + " " + config.Position.YAxis.ToString();

            var panel = elements.Add(new CuiPanel
            {
                Image = {
                    Color = "0.60 0.60 0.60 0.27"
                },

                RectTransform = {
                    AnchorMin = contMin,
                    AnchorMax = GetAnchorMax(player.userID)
                },

                CursorEnabled = false
            }, "Hud", "cnvPanel");

            if (updatePicture)
            {
                if (config.ShowPicture)
                {
                    var logo = new CuiElementContainer();
                    logo.Add(new CuiElement
                    {
                        Name = "cnvPicture",
                        Components =
                    {
                        new CuiRawImageComponent
                        {
                            Color = "1 1 1 1",
                            Url = (config.PictureURL != "") ? config.PictureURL : "https://cdn.discordapp.com/attachments/504359168312868874/808374016821166151/toinvert.png"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = config.Position.XAxis.ToString() + " " + (config.Position.YAxis + 0.002f).ToString(),
                            AnchorMax = (config.Position.XAxis + 0.010f).ToString() + " " + (config.Position.YAxis + 0.025f).ToString()
                        }
                    }
                    });

                    CuiHelper.AddUi(player, logo);
                }
            }

            if (MustShowValue(player.userID))
            {
                var message01 = elements.Add(new CuiLabel
                {
                    Text =
                    {
                    Text = valueText,
                    Color = fontColor,
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft
                    },

                    RectTransform = {
                    AnchorMin = "0.02 0",
                    AnchorMax = "0.8 1"
                    },
                }, panel);
            }

            if ((MustShowBar(player.userID)) && (valueText != "0"))
            {
                float anchorMin = config.Position.XAxis;
                if (config.ShowPicture)
                    anchorMin = anchorMin + pictureOffset;

                if (MustShowValue(player.userID))
                    anchorMin = anchorMin + 0.015f;

                var panel2 = elements.Add(new CuiPanel
                {
                    Image = {
                    Color = barColor
                },

                    RectTransform = {
                    AnchorMin = anchorMin.ToString() + " " + (config.Position.YAxis + 0.002f).ToString(),
                    AnchorMax = barText
                },

                    CursorEnabled = false
                }, "Hud", "cnvBar");
            }

            return elements;
        }
        #endregion UI

        void LoadData()
        {
            try
            {
                playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>(Name);
            }
            catch
            {
                playerData = new Dictionary<ulong, PlayerData>();
            }
        }

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, playerData);

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion functions    

        #region helpers
        private static string ColorFromHex(string hexColor, int alpha)
        {
            hexColor = hexColor.TrimStart('#');
            if (hexColor.Length != 6 && hexColor.Length != 8)
            {
                hexColor = "000000";
            }
            int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);

            if (hexColor.Length == 8)
            {
                alpha = int.Parse(hexColor.Substring(6, 2), NumberStyles.AllowHexSpecifier);
            }

            return $"{red / 255.0} {green / 255.0} {blue / 255.0} {alpha / 255.0}";
        }

        private string Lang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);

        private bool IsWearablesContainer(ItemContainer cont)
        {
            return (cont.HasFlag(ItemContainer.Flag.Clothing) && (cont.HasFlag(ItemContainer.Flag.IsPlayer)));
        }

        private bool IsWearingGoggles(BasePlayer player, out Item item)
        {
            foreach (Item contItem in player.inventory.containerWear.itemList)
            {
                if (contItem.info.shortname.Equals(gogglesShortName))
                {
                    item = contItem;
                    return true;
                }
            }

            item = null;
            return false;
        }

        private bool PlayerSignedUp(BasePlayer player)
        {
            PlayerData info;
            if (playerData.TryGetValue(player.userID, out info))
                return info.Enabled;
            else
                return true;
        }

        private bool IsValidColor(string value, out string RGB)
        {
            RGB = string.Empty;

            if (value[0] == '#')
            {
                if (IsValidRGBValue(value))
                {
                    RGB = value;
                    return true;
                }
                else
                    return false;
            }
            else
            {
                if (Enum.IsDefined(typeof(AllColors), value))
                {
                    RGB = ColorToRGB(value);
                    return true;
                }
                else
                    return false;

            }
        }

        private bool IsValidRGBValue(string value)
        {
            if (value.Length <= 7)
                return Regex.IsMatch(value, "^#([A-Fa-f0-9]{6})$");
            else
                return Regex.IsMatch(value, "^#([A-Fa-f0-9]{8})$");
        }

        void ShowCommandHelp(BasePlayer player)
        {
            player.ChatMessage(Lang("HelpInfo", player.UserIDString) + "\n\n" + Lang("HelpView", player.userID.ToString()) + "\n" + Lang("HelpShow", player.userID.ToString()) +
                "\n\n" + Lang("HelpAlertAdd", player.userID.ToString()) + "\n" + Lang("HelpRangeAdd", player.userID.ToString()) + "\n" + Lang("HelpDelete", player.userID.ToString()) +
                "\n\n" + Lang("HelpView", player.userID.ToString()) + "\n\n" + Lang("HelpColor", player.userID.ToString()));
        }

        string ColorList()
        {
            string colors = string.Empty;
            foreach (string value in Enum.GetNames(typeof(AllColors)))
            {
                colors = colors + ", " + value;
            }

            colors = colors.TrimStart(',', ' ');
            return (colors);
        }

        string ColorToRGB(string namedColor)
        {
            System.Drawing.Color color = System.Drawing.Color.FromName(namedColor);
            return ("#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2"));
        }

        #endregion helpers
    }
}