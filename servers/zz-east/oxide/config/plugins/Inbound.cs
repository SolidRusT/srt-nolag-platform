using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Inbound", "Substrata", "0.5.1")]
    [Description("Broadcasts notifications when patrol helicopters, supply drops, cargo ships, etc. are inbound")]

    class Inbound : RustPlugin
    {
        [PluginReference]
        Plugin PopupNotifications;

        bool hasSRig;
        bool hasLRig;
        Vector3 posSRig;
        Vector3 posLRig;

        private void OnServerInitialized()
        {
            if (configData.notifications.popup && !PopupNotifications)
                PrintWarning("You have popup notifications enabled, but the 'Popup Notifications' plugin could not be found.");

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

                var pos = apc.transform.position;
                string msg = Lang("BradleyAPC", null, GetLocation(pos, null, null));

                if (configData.notifications.chat) Server.Broadcast(msg);
                if (configData.notifications.popup) PopupNotifications.Call("CreatePopupNotification", msg);

                if (configData.misc.logToConsole) Puts(msg);
                if (configData.misc.logToFile) LogToFile("log", $"[{DateTime.Now.ToString("HH:mm:ss")}] {msg}", this);
            });
        }

        void OnEntitySpawned(CargoPlane plane)
        {
            if (!configData.alerts.cargoPlane) return;

            NextTick(() =>
            {
                if (plane == null) return;

                var srcPos = plane.startPos;
                var destPos = plane.dropPosition;
                string msg = Lang("CargoPlane", null, GetLocation(srcPos, null, null), GetLocationDest(destPos));

                if (configData.notifications.chat) Server.Broadcast(msg);
                if (configData.notifications.popup) PopupNotifications.Call("CreatePopupNotification", msg);

                if (configData.misc.logToConsole) Puts(msg);
                if (configData.misc.logToFile) LogToFile("log", $"[{DateTime.Now.ToString("HH:mm:ss")}] {msg}", this);
            });
        }

        void OnEntitySpawned(CargoShip ship)
        {
            if (!configData.alerts.cargoShip) return;

            NextTick(() =>
            {
                if (ship == null) return;

                var pos = ship.transform.position;
                string msg = Lang("CargoShip", null, GetLocation(pos, null, null));

                if (configData.notifications.chat) Server.Broadcast(msg);
                if (configData.notifications.popup) PopupNotifications.Call("CreatePopupNotification", msg);

                if (configData.misc.logToConsole) Puts(msg);
                if (configData.misc.logToFile) LogToFile("log", $"[{DateTime.Now.ToString("HH:mm:ss")}] {msg}", this);
            });
        }

        void OnEntitySpawned(CH47HelicopterAIController ch47)
        {
            if (!configData.alerts.ch47) return;

            timer.Once(1.5f, () =>
            {
                if (ch47 == null) return;
                if (configData.misc.hideRigCrates && ch47.ShouldLand()) return;

                var srcPos = ch47.transform.position;
                var destPos = ch47.GetMoveTarget();
                string msg = Lang("CH47", null, GetLocation(srcPos, null, null), GetLocationDest(destPos));

                if (configData.notifications.chat) Server.Broadcast(msg);
                if (configData.notifications.popup) PopupNotifications.Call("CreatePopupNotification", msg);

                if (configData.misc.logToConsole) Puts(msg);
                if (configData.misc.logToFile) LogToFile("log", $"[{DateTime.Now.ToString("HH:mm:ss")}] {msg}", this);
            });
        }

        void OnExcavatorMiningToggled(ExcavatorArm arm)
        {
            if (!configData.alerts.excavator) return;
            if (arm == null) return;
            if (!arm.IsOn()) return;

            var pos = arm.transform.position;
            string msg = Lang("Excavator", null, GetLocation(pos, null, null));

            if (configData.notifications.chat) Server.Broadcast(msg);
            if (configData.notifications.popup) PopupNotifications.Call("CreatePopupNotification", msg);

            if (configData.misc.logToConsole) Puts(msg);
            if (configData.misc.logToFile) LogToFile("log", $"[{DateTime.Now.ToString("HH:mm:ss")}] {msg}", this);
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

                string msg = Lang("HackableCrate", null, GetLocation(pos, null, crate));

                if (configData.notifications.chat) Server.Broadcast(msg);
                if (configData.notifications.popup) PopupNotifications.Call("CreatePopupNotification", msg);

                if (configData.misc.logToConsole) Puts(msg);
                if (configData.misc.logToFile) LogToFile("log", $"[{DateTime.Now.ToString("HH:mm:ss")}] {msg}", this);
            });
        }

        void OnEntitySpawned(BaseHelicopter heli)
        {
            if (!configData.alerts.patrolHeli) return;

            NextTick(() =>
            {
                if (heli == null) return;

                var srcPos = heli.transform.position;
                var destPos = heli.GetComponentInParent<PatrolHelicopterAI>().destination;
                string msg = Lang("PatrolHeli", null, GetLocation(srcPos, null, null), GetLocationDest(destPos));

                if (configData.notifications.chat) Server.Broadcast(msg);
                if (configData.notifications.popup) PopupNotifications.Call("CreatePopupNotification", msg);

                if (configData.misc.logToConsole) Puts(msg);
                if (configData.misc.logToFile) LogToFile("log", $"[{DateTime.Now.ToString("HH:mm:ss")}] {msg}", this);
            });
        }

        void CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (!configData.alerts.hackingCrate) return;
            if (player == null || crate == null) return;

            var pos = crate.transform.position;

            if (configData.misc.hideCargoCrates && AtCargoShip(null, crate)) return;
            if (configData.misc.hideRigCrates && (AtLargeRig(pos) || AtSmallRig(pos))) return;

            string msg = Lang("HackingCrate", player.UserIDString, player.displayName, GetLocation(pos, null, crate));

            if (configData.notifications.chat) Server.Broadcast(msg);
            if (configData.notifications.popup) PopupNotifications.Call("CreatePopupNotification", msg);

            if (configData.misc.logToConsole) Puts(msg);
            if (configData.misc.logToFile) LogToFile("log", $"[{DateTime.Now.ToString("HH:mm:ss")}] {msg}", this);
        }

        void OnExplosiveThrown(BasePlayer player, SupplySignal signal)
        {
            if (!configData.alerts.supplySignal) return;
            if (player == null || signal == null) return;

            var pos = player.transform.position;
            string msg = Lang("SupplySignal", player.UserIDString, player.displayName, GetLocation(pos, player, null));

            if (configData.notifications.chat) Server.Broadcast(msg);
            if (configData.notifications.popup) PopupNotifications.Call("CreatePopupNotification", msg);

            if (configData.misc.logToConsole) Puts(msg);
            if (configData.misc.logToFile) LogToFile("log", $"[{DateTime.Now.ToString("HH:mm:ss")}] {msg}", this);
        }

        void OnExplosiveDropped(BasePlayer player, SupplySignal signal)
        {
            if (!configData.alerts.supplySignal) return;
            if (player == null || signal == null) return;

            var pos = player.transform.position;
            string msg = Lang("SupplySignal", player.UserIDString, player.displayName, GetLocation(pos, player, null));

            if (configData.notifications.chat) Server.Broadcast(msg);
            if (configData.notifications.popup) PopupNotifications.Call("CreatePopupNotification", msg);

            if (configData.misc.logToConsole) Puts(msg);
            if (configData.misc.logToFile) LogToFile("log", $"[{DateTime.Now.ToString("HH:mm:ss")}] {msg}", this);
        }

        void OnEntitySpawned(SupplyDrop drop)
        {
            if (!configData.alerts.supplyDrop) return;
            if (drop == null) return;

            var pos = drop.transform.position;
            string msg = Lang("SupplyDrop", null, GetLocation(pos, null, null));

            if (configData.notifications.chat) Server.Broadcast(msg);
            if (configData.notifications.popup) PopupNotifications.Call("CreatePopupNotification", msg);

            if (configData.misc.logToConsole) Puts(msg);
            if (configData.misc.logToFile) LogToFile("log", $"[{DateTime.Now.ToString("HH:mm:ss")}] {msg}", this);
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
            if ((player != null && player.GetComponentInParent<CargoShip>()) || (entity != null && entity.GetComponentInParent<CargoShip>())) return true;
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
        #endregion

        #region Config
        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Notifications (true/false)")]
            public Notifications notifications = new Notifications();
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
                {"Location", " at {0}"},
                {"LocationDestination", " and headed to {0}"}
			}, this);
		}

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion
    }
}