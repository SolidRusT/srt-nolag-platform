using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Inbound", "Substrata", "0.5.2")]
    [Description("Broadcasts notifications when patrol helicopters, supply drops, cargo ships, etc. are inbound")]

    class Inbound : RustPlugin
    {
        [PluginReference]
        Plugin DiscordMessages, PopupNotifications;

        bool hasSRig;
        bool hasLRig;
        Vector3 posSRig;
        Vector3 posLRig;
        ulong iconID;

        private void OnServerInitialized()
        {
            if (configData.notifications.popup && !PopupNotifications)
                PrintWarning("You have popup notifications enabled, but the 'Popup Notifications' plugin could not be found.");

            if (configData.discordMsg.enabled)
            {
                if (!DiscordMessages)
                    PrintWarning("You have Discord notifications enabled, but the 'Discord Messages' plugin could not be found.");
                else if (!configData.discordMsg.webhookURL.Contains("/api/webhooks/"))
                    PrintWarning("You have Discord notifications enabled, but the Webhook URL is missing or incorrect.");
            }

            if (configData.chatIcon.steamID.IsSteamId())
                iconID = configData.chatIcon.steamID;
            else if (configData.chatIcon.steamID != 0)
                PrintWarning("Chat icon is not set to a valid Steam ID. The default icon will be used instead.");

            foreach (MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (monument.name == "OilrigAI")
                {
                    hasSRig = true;
                    posSRig = monument.transform.position;
                }
                if (monument.name == "OilrigAI2")
                {
                    hasLRig = true;
                    posLRig = monument.transform.position;
                }
            }
        }

        #region Hooks
        void OnBradleyApcInitialize(BradleyAPC apc)
        {
            if (!configData.alerts.bradleyAPC) return;

            NextTick(() =>
            {
                if (apc == null) return;

                SendMsg(Lang("BradleyAPC", null, GetLocation(apc.transform.position, null, null)));
            });
        }

        void OnEntitySpawned(CargoPlane plane)
        {
            if (!configData.alerts.cargoPlane) return;

            NextTick(() =>
            {
                if (plane == null) return;

                SendMsg(Lang("CargoPlane", null, GetLocation(plane.startPos, null, null), GetLocationDest(plane.dropPosition)));
            });
        }

        void OnEntitySpawned(CargoShip ship)
        {
            if (!configData.alerts.cargoShip) return;

            NextTick(() =>
            {
                if (ship == null) return;

                SendMsg(Lang("CargoShip", null, GetLocation(ship.transform.position, null, null)));
            });
        }

        void OnEntitySpawned(CH47HelicopterAIController ch47)
        {
            if (!configData.alerts.ch47) return;

            timer.Once(1.5f, () =>
            {
                if (ch47 == null) return;
                if (configData.misc.hideRigCrates && ch47.ShouldLand()) return;

                SendMsg(Lang("CH47", null, GetLocation(ch47.transform.position, null, null), GetLocationDest(ch47.GetMoveTarget())));
            });
        }

        void OnExcavatorMiningToggled(ExcavatorArm arm)
        {
            if (!configData.alerts.excavator) return;
            if (arm == null || !arm.IsOn()) return;

            SendMsg(Lang("Excavator", null, GetLocation(arm.transform.position, null, null)));
        }

        void OnEntitySpawned(HackableLockedCrate crate)
        {
            if (!configData.alerts.hackableCrate) return;

            NextTick(() =>
            {
                if (crate == null) return;

                var pos = crate.transform.position;

                if (configData.misc.hideCargoCrates && AtCargoShip(null, crate)) return;
                if (configData.misc.hideRigCrates && (AtLargeRig(pos) || AtSmallRig(pos))) return;

                SendMsg(Lang("HackableCrate", null, GetLocation(pos, null, crate)));
            });
        }

        void OnEntitySpawned(BaseHelicopter heli)
        {
            if (!configData.alerts.patrolHeli) return;

            NextTick(() =>
            {
                if (heli == null) return;

                SendMsg(Lang("PatrolHeli", null, GetLocation(heli.transform.position, null, null), GetLocationDest(heli.GetComponentInParent<PatrolHelicopterAI>().destination)));
            });
        }

        void CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (!configData.alerts.hackingCrate) return;
            if (player == null || crate == null) return;

            var pos = crate.transform.position;

            if (configData.misc.hideCargoCrates && AtCargoShip(null, crate)) return;
            if (configData.misc.hideRigCrates && (AtLargeRig(pos) || AtSmallRig(pos))) return;

            SendMsg(Lang("HackingCrate", player.UserIDString, player.displayName, GetLocation(pos, null, crate)));
        }

        void OnExplosiveThrown(BasePlayer player, SupplySignal signal)
        {
            if (!configData.alerts.supplySignal) return;
            if (player == null || signal == null) return;

            SendMsg(Lang("SupplySignal", player.UserIDString, player.displayName, GetLocation(player.transform.position, player, null)));
        }

        void OnExplosiveDropped(BasePlayer player, SupplySignal signal) => OnExplosiveThrown(player, signal);

        void OnEntitySpawned(SupplyDrop drop)
        {
            if (!configData.alerts.supplyDrop) return;
            if (drop == null) return;

            SendMsg(Lang("SupplyDrop", null, GetLocation(drop.transform.position, null, null)));
        }

        private HashSet<uint> landedDrops = new HashSet<uint>();
        void OnSupplyDropLanded(SupplyDrop drop)
        {
            if (!configData.alerts.supplyDropLand) return;
            if (drop == null) return;

            if (!landedDrops.Contains(drop.net.ID))
            {
                SendMsg(Lang("SupplyDropLanded", null, GetLocation(drop.transform.position, null, null)));
                landedDrops.Add(drop.net.ID);
            }
        }
        #endregion

        #region Helpers
        string GetLocation(Vector3 pos, BasePlayer player, BaseEntity entity)
        {
            if (configData.grid.showGrid && configData.coordinates.showCoords)
            {
                string Grid = GetGrid(pos, player, entity)+" ";
                string Coords = pos.ToString();
                string posStr = Grid+Coords;
                if (Grid == null || !Regex.IsMatch(Grid, "^[A-Z]")) posStr = Coords.Replace("(", string.Empty).Replace(")", string.Empty);
                return Lang("Location", null, posStr);
            }

            if (configData.grid.showGrid)
            {
                string Grid = GetGrid(pos, player, entity);
                if (Grid == null || !Regex.IsMatch(Grid, "^[A-Z]")) return string.Empty;
                return Lang("Location", null, Grid);
            }

            if (configData.coordinates.showCoords)
            {
                string Coords = pos.ToString().Replace("(", string.Empty).Replace(")", string.Empty);
                return Lang("Location", null, Coords);
            }
            return string.Empty;
        }

        string GetLocationDest(Vector3 pos)
        {
            if (configData.grid.showGrid && configData.coordinates.showCoords)
            {
                if (configData.grid.showDestination && configData.coordinates.showDestination)
                {
                    string Grid = GetGrid(pos, null, null)+" ";
                    string Coords = pos.ToString();
                    string posStr = Grid+Coords;
                    if (Grid == null || !Regex.IsMatch(Grid, "^[A-Z]")) posStr = Coords.Replace("(", string.Empty).Replace(")", string.Empty);
                    return Lang("LocationDestination", null, posStr);
                }

                if (configData.grid.showDestination)
                {
                    string Grid = GetGrid(pos, null, null);
                    if (Grid == null || !Regex.IsMatch(Grid, "^[A-Z]")) return string.Empty;
                    return Lang("LocationDestination", null, Grid);
                }

                if (configData.coordinates.showDestination)
                {
                    string Coords = pos.ToString().Replace("(", string.Empty).Replace(")", string.Empty);
                    return Lang("LocationDestination", null, Coords);
                }
            }

            if (configData.grid.showGrid && configData.grid.showDestination)
            {
                string Grid = GetGrid(pos, null, null);
                if (Grid == null || !Regex.IsMatch(Grid, "^[A-Z]")) return string.Empty;
                return Lang("LocationDestination", null, Grid);
            }

            if (configData.coordinates.showCoords && configData.coordinates.showDestination)
            {
                string Coords = pos.ToString().Replace("(", string.Empty).Replace(")", string.Empty);
                return Lang("LocationDestination", null, Coords);
            }
            return string.Empty;
        }

        string GetGrid(Vector3 pos, BasePlayer player, BaseEntity entity)
        {
			var x = Mathf.Floor((pos.x+(ConVar.Server.worldsize/2)) / 146.3f);
			var z = (Mathf.Floor(ConVar.Server.worldsize/146.3f)-1)-Mathf.Floor((pos.z+(ConVar.Server.worldsize/2)) / 146.3f); // Credit: yetzt

            string Grid = $"{GetGridLetter((int)(x))}{z}";

            if (configData.grid.showRigCargo)
            {
                if (AtCargoShip(player, entity)) return "Cargo Ship";
                if (AtLargeRig(pos)) return "Large Oil Rig";
                if (AtSmallRig(pos)) return "Oil Rig";
            }

			return Grid;
		}

		public static string GetGridLetter(int num) // Credit: Jake_Rich
		{
			int num2 = Mathf.FloorToInt((float)(num / 26));
			int num3 = num % 26;
			string text = string.Empty;
			if (num2 > 0)
			{
				for (int i = 0; i < num2; i++)
				{
					text += Convert.ToChar(65 + i);
				}
			}
			return text + Convert.ToChar(65 + num3).ToString();
		}

        bool AtCargoShip(BasePlayer player, BaseEntity entity)
        {
            if (player?.GetComponentInParent<CargoShip>() || entity?.GetComponentInParent<CargoShip>()) return true;
            else return false;
        }

        bool AtLargeRig(Vector3 pos)
        {
            if (hasLRig)
            {
                float xDist = Mathf.Abs(posLRig.x - pos.x);
                float zDist = Mathf.Abs(posLRig.z - pos.z);
                if (xDist <= 75f && zDist <= 75f) return true;
                else return false;
            }
            return false;
        }

        bool AtSmallRig(Vector3 pos)
        {
            if (hasSRig)
            {
                float xDist = Mathf.Abs(posSRig.x - pos.x);
                float zDist = Mathf.Abs(posSRig.z - pos.z);
                if (xDist <= 60f && zDist <= 60f) return true;
                else return false;
            }
            return false;
        }

        string filterTags = @"<\/?(align|alpha|cspace|indent|line-height|line-indent|margin|mark|mspace|pos|size|space|voffset).*?>|<\/?(b|i|lowercase|uppercase|smallcaps|s|u|sup|sub)>";
        void SendMsg(string msg)
        {
            var msgFiltered_p = Regex.Replace(msg, filterTags, String.Empty);
            var msgFiltered = Regex.Replace(msgFiltered_p, @"<\/?color.*?>", String.Empty);

            if (configData.notifications.chat) Server.Broadcast(msg, null, iconID);
            if (configData.notifications.popup) PopupNotifications?.Call("CreatePopupNotification", msgFiltered_p);
            if (configData.discordMsg.enabled && configData.discordMsg.webhookURL.Contains("/api/webhooks/")) DiscordMessages?.Call("API_SendTextMessage", configData.discordMsg.webhookURL, Lang("DiscordMessage", null, msgFiltered));
            if (configData.misc.logToConsole) Puts(msgFiltered);
            if (configData.misc.logToFile) LogToFile("log", $"[{DateTime.Now.ToString("HH:mm:ss")}] {msgFiltered}", this);
        }
        #endregion

        #region Config
        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Notifications (true/false)")]
            public Notifications notifications = new Notifications();
            [JsonProperty(PropertyName = "Discord Messages")]
            public DiscordMsg discordMsg = new DiscordMsg();
            [JsonProperty(PropertyName = "Chat Icon")]
            public ChatIcon chatIcon = new ChatIcon();
            [JsonProperty(PropertyName = "Alerts (true/false)")]
            public Alerts alerts = new Alerts();
            [JsonProperty(PropertyName = "Grid (true/false)")]
            public Grid grid = new Grid();
            [JsonProperty(PropertyName = "Coordinates (true/false)")]
            public Coordinates coordinates = new Coordinates();
            [JsonProperty(PropertyName = "Misc (true/false)")]
            public Misc misc = new Misc();
        }

        class Notifications
        {
            [JsonProperty(PropertyName = "Chat Notifications")]
            public bool chat = true;
            [JsonProperty(PropertyName = "Popup Notifications")]
            public bool popup = false;
        }

        class DiscordMsg
        {
            [JsonProperty(PropertyName = "Enabled (true/false)")]
            public bool enabled = false;
            [JsonProperty(PropertyName = "Webhook URL")]
            public string webhookURL = "";
        }

        class ChatIcon
        {
            [JsonProperty(PropertyName = "Steam ID")]
            public ulong steamID = 0;
        }

        class Alerts
        {
            [JsonProperty(PropertyName = "Bradley APC Alerts")]
            public bool bradleyAPC = true;
            [JsonProperty(PropertyName = "Cargo Plane Alerts")]
            public bool cargoPlane = true;
            [JsonProperty(PropertyName = "Cargo Ship Alerts")]
            public bool cargoShip = true;
            [JsonProperty(PropertyName = "CH47 Chinook Alerts")]
            public bool ch47 = true;
            [JsonProperty(PropertyName = "Excavator Alerts")]
            public bool excavator = true;
            [JsonProperty(PropertyName = "Hackable Crate Alerts")]
            public bool hackableCrate = true;
            [JsonProperty(PropertyName = "Patrol Helicopter Alerts")]
            public bool patrolHeli = true;
            [JsonProperty(PropertyName = "Player Hacking Crate Alerts")]
            public bool hackingCrate = true;
            [JsonProperty(PropertyName = "Player Supply Signal Alerts")]
            public bool supplySignal = true;
            [JsonProperty(PropertyName = "Supply Drop Alerts")]
            public bool supplyDrop = true;
            [JsonProperty(PropertyName = "Supply Drop Landed Alerts")]
            public bool supplyDropLand = true;
        }

        class Grid
        {
            [JsonProperty(PropertyName = "Show Grid")]
            public bool showGrid = true;
            [JsonProperty(PropertyName = "Show Grid - Destination")]
            public bool showDestination = true;
            [JsonProperty(PropertyName = "Show Oil Rig / Cargo Ship Labels")]
            public bool showRigCargo = true;
        }

        class Coordinates
        {
            [JsonProperty(PropertyName = "Show Coordinates")]
            public bool showCoords = false;
            [JsonProperty(PropertyName = "Show Coordinates - Destination")]
            public bool showDestination = false;
        }

        class Misc
        {
            [JsonProperty(PropertyName = "Hide Cargo Ship Crate Messages")]
            public bool hideCargoCrates = false;
            [JsonProperty(PropertyName = "Hide Oil Rig Crate Messages")]
            public bool hideRigCrates = false;
            [JsonProperty(PropertyName = "Log To Console")]
            public bool logToConsole = false;
            [JsonProperty(PropertyName = "Log To File")]
            public bool logToFile = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => configData = new ConfigData();
        protected override void SaveConfig() => Config.WriteObject(configData);
        #endregion

        #region Localization
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"BradleyAPC", "Bradley APC inbound{0}"},
				{"CargoPlane", "Cargo Plane inbound{0}{1}"},
                {"CargoShip", "Cargo Ship inbound{0}"},
                {"CH47", "Chinook inbound{0}{1}"},
                {"Excavator", "The Excavator has been activated{0}"},
                {"HackableCrate", "Hackable Crate has spawned{0}"},
                {"PatrolHeli", "Patrol Helicopter inbound{0}{1}"},
                {"HackingCrate", "{0} is hacking a locked crate{1}"},
                {"SupplySignal", "{0} has deployed a supply signal{1}"},
                {"SupplyDrop", "Supply Drop has dropped{0}"},
                {"SupplyDropLanded", "Supply Drop has landed{0}"},
                {"Location", " at {0}"},
                {"LocationDestination", " and headed to {0}"},
                {"DiscordMessage", ":arrow_lower_right:  **{0}**"}
			}, this);
		}

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion
    }
}