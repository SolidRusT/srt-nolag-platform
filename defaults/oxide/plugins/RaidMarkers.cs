/*
 ########### README ####################################################
                                                                             
  !!! DON'T EDIT THIS FILE !!!
                                                                     
 ########### CHANGES ###################################################

 1.0.0
    - Plugin release
 1.0.1
    - Added Localization
 1.0.2
    - Added permissions to see markers
    - Added hook CanNetworkTo

 #######################################################################
*/

using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using System;
using Rust;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Raid Markers", "Paulsimik", "1.0.2")]
    [Description("Raid Markers on the map")]
    class RaidMarkers : RustPlugin
    {
        #region [Fields]

        private const string permAllow = "raidmarkers.allow";
        private Configuration config;
        private HashSet<MapMarkerGenericRadius> raidMarkers = new HashSet<MapMarkerGenericRadius>();

        #endregion

        #region [Oxide Hooks]

        private void Init() => permission.RegisterPermission(permAllow, this);

        private void Unload() => ClearRaidMarkers();

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null)
                return;

            var attacker = info.InitiatorPlayer;
            if (attacker == null)
                return;

            if (entity.lastDamage != DamageType.Explosion)
                return;

            if (config.blacklistedPrefab.Contains(entity.ShortPrefabName))
                return;

            if (entity.name.Contains("building") || config.additionalPrefab.Contains(entity.ShortPrefabName))
            {
                if (IsFar(entity.ServerPosition))
                    CreateRaidMarker(entity.ServerPosition);
            }
        }

        private bool CanNetworkTo(MapMarkerGenericRadius marker, BasePlayer player)
        {
            if (marker == null || player == null)
                return true;

            if (!raidMarkers.Contains(marker))
                return true;

            if (!permission.UserHasPermission(player.UserIDString, permAllow))
                return false;

            return true;
        }

        #endregion

        #region [Hooks]   

        private void CreateRaidMarker(Vector3 position)
        {
            MapMarkerGenericRadius marker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;
            if (marker == null)
                return;

            raidMarkers.Add(marker);
            marker.alpha = config.markerConfiguration.markerAlpha;
            marker.radius = config.markerConfiguration.markerRadius;
            marker.color1 = ParseColor(config.markerConfiguration.markerColor1);
            marker.color2 = ParseColor(config.markerConfiguration.markerColor2);
            marker.Spawn();
            marker.SendUpdate();

            timer.In(config.markerConfiguration.markerDuration, () =>
            {
                marker.Kill();
                marker.SendUpdate();
                raidMarkers.Remove(marker);
            });
        }

        private void ClearRaidMarkers()
        {
            foreach (var marker in raidMarkers)
            {
                if (marker != null)
                {
                    marker.Kill();
                    marker.SendUpdate();
                }
            }

            raidMarkers.Clear();
        }

        private bool IsFar(Vector3 position)
        {
            bool isFar = true;
            foreach (var marker in raidMarkers)
            {
                if (GetDistance(marker.ServerPosition, position) < config.markerDistance)
                {
                    isFar = false;
                    break;
                }
            }

            return isFar;
        }

        private double GetDistance(Vector3 pos1, Vector3 pos2)
        {
            return Math.Round(Vector3.Distance(pos1, pos2), 0);
        }

        private Color ParseColor(string hexColor)
        {
            if (!hexColor.StartsWith("#"))
                hexColor = $"#{hexColor}";

            Color color;
            if (ColorUtility.TryParseHtmlString(hexColor, out color))
                return color;

            return Color.white;
        }

        #endregion 

        #region [Chat Commands]

        [ChatCommand("rmtest")]
        private void cmdRaidMarker(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, GetLang("NoPerm", player.UserIDString));
                return;
            }

            CreateRaidMarker(player.ServerPosition);
            SendReply(player, GetLang("TestRaidMarker", player.UserIDString));
        }

        #endregion

        #region [Classes]

        private class Configuration
        {
            [JsonProperty(PropertyName = "Blaclisted prefabs")]
            public List<string> blacklistedPrefab = new List<string>();

            [JsonProperty(PropertyName = "Additional prefabs")]
            public List<string> additionalPrefab = new List<string>();

            [JsonProperty(PropertyName = "Distance when place new marker from another marker")]
            public int markerDistance;

            [JsonProperty(PropertyName = "Marker configuration")]
            public MarkerConfiguration markerConfiguration;

            public VersionNumber version;
        }

        private class MarkerConfiguration
        {
            [JsonProperty(PropertyName = "Alpha")]
            public float markerAlpha;

            [JsonProperty(PropertyName = "Radius")]
            public float markerRadius;

            [JsonProperty(PropertyName = "Color1")]
            public string markerColor1;

            [JsonProperty(PropertyName = "Color2")]
            public string markerColor2;

            [JsonProperty(PropertyName = "Duration")]
            public float markerDuration;
        }

        #endregion

        #region [Config]

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                blacklistedPrefab = new List<string>
                {
                    "wall.external.high.wood",
                    "wall.external.high.stone"
                },
                additionalPrefab = new List<string>
                {
                    "cupboard.tool.deployed"
                },
                markerDistance = 100,
                markerConfiguration = new MarkerConfiguration
                {
                    markerAlpha = 0.6f,
                    markerRadius = 0.7f,
                    markerDuration = 90f,
                    markerColor1 = "#000000",
                    markerColor2 = "#FF0000",
                },
                version = Version
            };
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
            Puts("Generating new configuration file........");
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("######### Configuration file is not valid! #########");
                return;
            }

            SaveConfig();
        }

        #endregion

        #region [Localization]

        private string GetLang(string key, string playerID) => lang.GetMessage(key, this, playerID);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NoPerm", "You don't have permissions" },
                { "TestRaidMarker", "Test Raid Marker created on your position" }

            }, this);
        }

        #endregion
    }
}