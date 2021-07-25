using UnityEngine;
using CompanionServer.Handlers;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("TC Map Markers", "1AK1", "1.1.2")]
    [Description("Shows custom map markers for all cupboards on the server.")]
    internal class TcMapMarkers : CovalencePlugin
    {
        [PluginReference] private Plugin Friends, Clans;

        #region Vars

        private int count;
        private const string permUse = "tcmapmarkers.use";
        public List<MapMarkerGenericRadius> TCMarkers = new List<MapMarkerGenericRadius>();

        #endregion Vars

        #region Config       

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Map Marker Options")]
            public MapMarkerOptions MapMarker { get; set; }

            [JsonProperty(PropertyName = "Owned Markers Options")]
            public OwnedMarkersOptions OwnedMarkers { get; set; }

            [JsonProperty(PropertyName = "Other Markers Options")]
            public OtherMarkersOptions OtherMarkers { get; set; }

            [JsonProperty(PropertyName = "Teams Markers Options")]
            public TeamsMarkersOptions TeamsMarkers { get; set; }

            [JsonProperty(PropertyName = "Friends Markers Options")]
            public FriendsMarkersOptions FriendsMarkers { get; set; }

            [JsonProperty(PropertyName = "Clans Markers Options")]
            public ClansMarkersOptions ClansMarkers { get; set; }

            public class MapMarkerOptions
            {
                [JsonProperty(PropertyName = "Prefab Path")]
                public string PREFAB_MARKER { get; set; }

                [JsonProperty(PropertyName = "Show to all? (true/false)")]
                public bool visibleToAll { get; set; }

                [JsonProperty(PropertyName = "Enable Teams? (true/false)")]
                public bool TeamsUse { get; set; }

                [JsonProperty(PropertyName = "Enable Friends? (true/false)")]
                public bool FriendsUse { get; set; }

                [JsonProperty(PropertyName = "Enable Clans? (true/false)")]
                public bool ClansUse { get; set; }

            }

            public class OwnedMarkersOptions
            {
                [JsonProperty(PropertyName = "Color1 (hex)")]
                public string OwnedColor1 { get; set; }

                [JsonProperty(PropertyName = "Color2 (hex)")]
                public string OwnedColor2 { get; set; }

                [JsonProperty(PropertyName = "Alpha")]
                public float OwnedAlpha { get; set; }

                [JsonProperty(PropertyName = "Radius")]
                public float OwnedRadius { get; set; }
            }

            public class OtherMarkersOptions
            {
                [JsonProperty(PropertyName = "Color1 (hex)")]
                public string OtherColor1 { get; set; }

                [JsonProperty(PropertyName = "Color2 (hex)")]
                public string OtherColor2 { get; set; }

                [JsonProperty(PropertyName = "Alpha")]
                public float OtherAlpha { get; set; }

                [JsonProperty(PropertyName = "Radius")]
                public float OtherRadius { get; set; }
            }

            public class TeamsMarkersOptions
            {
                [JsonProperty(PropertyName = "Color1 (hex)")]
                public string TeamsColor1 { get; set; }

                [JsonProperty(PropertyName = "Color2 (hex)")]
                public string TeamsColor2 { get; set; }

                [JsonProperty(PropertyName = "Alpha")]
                public float TeamsAlpha { get; set; }

                [JsonProperty(PropertyName = "Radius")]
                public float TeamsRadius { get; set; }
            }

            public class FriendsMarkersOptions
            {
                [JsonProperty(PropertyName = "Color1 (hex)")]
                public string FriendsColor1 { get; set; }

                [JsonProperty(PropertyName = "Color2 (hex)")]
                public string FriendsColor2 { get; set; }

                [JsonProperty(PropertyName = "Alpha")]
                public float FriendsAlpha { get; set; }

                [JsonProperty(PropertyName = "Radius")]
                public float FriendsRadius { get; set; }
            }

            public class ClansMarkersOptions
            {
                [JsonProperty(PropertyName = "Color1 (hex)")]
                public string ClansColor1 { get; set; }

                [JsonProperty(PropertyName = "Color2 (hex)")]
                public string ClansColor2 { get; set; }

                [JsonProperty(PropertyName = "Alpha")]
                public float ClansAlpha { get; set; }

                [JsonProperty(PropertyName = "Radius")]
                public float ClansRadius { get; set; }
            }

        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();
            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                MapMarker = new ConfigData.MapMarkerOptions
                {
                    PREFAB_MARKER = "assets/prefabs/tools/map/genericradiusmarker.prefab",
                    visibleToAll = true,
                    FriendsUse = true

                },
                OwnedMarkers = new ConfigData.OwnedMarkersOptions
                {
                    OwnedColor1 = "#00FF00",
                    OwnedColor2 = "#00FF00",
                    OwnedAlpha = 1f,
                    OwnedRadius = 0.08f
                },
                OtherMarkers = new ConfigData.OtherMarkersOptions
                {
                    OtherColor1 = "#FF0000",
                    OtherColor2 = "#FF0000",
                    OtherAlpha = 1f,
                    OtherRadius = 0.08f
                },
                TeamsMarkers = new ConfigData.TeamsMarkersOptions
                {
                    TeamsColor1 = "#0000FF",
                    TeamsColor2 = "#0000FF",
                    TeamsAlpha = 1f,
                    TeamsRadius = 0.08f
                },
                FriendsMarkers = new ConfigData.FriendsMarkersOptions
                {
                    FriendsColor1 = "#0000FF",
                    FriendsColor2 = "#0000FF",
                    FriendsAlpha = 1f,
                    FriendsRadius = 0.08f
                },
                ClansMarkers = new ConfigData.ClansMarkersOptions
                {
                    ClansColor1 = "#0000FF",
                    ClansColor2 = "#0000FF",
                    ClansAlpha = 1f,
                    ClansRadius = 0.08f
                }
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        #endregion Config

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayersOnly"] = "Command '{0}' can only be used by players",
                ["UsageTcMap"] = "Usage {0} <clear|update|showtoall|>",
                ["MarkersRemoved"] = "All map markers has been removed",
                ["MarkersSpawned"] = "Spawned {0} map markers",
                ["MarkersVisibleAdmin"] = "Map marker are now visible to ADMINS only",
                ["MarkersVisibleAll"] = "Map marker are now visible to ALL"
            }, this);
        }

        #endregion Localization

        #region Oxide Hooks

        private void Init()
        {
            AddCovalenceCommand("tcmap", "CommandTCMap");
            permission.RegisterPermission(permUse, this);
        }

        private void OnServerInitialized(bool initial)
        {
            LoadMapMarkers();
        }

        private void Unload()
        {
            RemoveMapMarkers();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                timer.Once(1f, () => OnPlayerConnected(player));
                return;
            }
            foreach (var marker in TCMarkers)
            {
                if(marker != null)
                {
                    marker.SendUpdate();
                }           
            }
            
        }

        object CanNetworkTo(MapMarkerGenericRadius marker, BasePlayer player)
        {
            if (!TCMarkers.Contains(marker)) return null;

            if (!configData.MapMarker.visibleToAll && !player.IPlayer.HasPermission(permUse)) return false;

            switch (marker.name)
            {
                case "owner":
                    if (marker.OwnerID == player.userID) return null;
                    else return false;
                case "team":
                    if(configData.MapMarker.TeamsUse && player.Team != null && player.Team.members.Contains(marker.OwnerID)) return null;
                    else return false;
                case "friend":
                    if(configData.MapMarker.FriendsUse && Friends != null && Friends.IsLoaded && Friends.Call<bool>("AreFriends", player.userID, marker.OwnerID)) return null;
                    else return false;
                case "clan":
                    if (configData.MapMarker.ClansUse && Clans != null && Clans.IsLoaded && Clans.Call<bool>("IsClanMember", player.UserIDString, marker.OwnerID.ToString())) return null;
                    else return false;
                default:
                    return null;
            }
        }
        
        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var cupboard = go.GetComponent<BuildingPrivlidge>();
            if (cupboard == null)
            {
                return;
            }

            CreateMapMarker(cupboard, "other", configData.OtherMarkers.OtherColor1, configData.OtherMarkers.OtherColor2, configData.OtherMarkers.OtherAlpha, configData.OtherMarkers.OtherRadius);

            if (configData.MapMarker.TeamsUse)
            {
                CreateMapMarker(cupboard, "team", configData.TeamsMarkers.TeamsColor1, configData.TeamsMarkers.TeamsColor2, configData.TeamsMarkers.TeamsAlpha, configData.TeamsMarkers.TeamsRadius);
            }

            if (configData.MapMarker.FriendsUse && Friends != null && Friends.IsLoaded)
            {
                CreateMapMarker(cupboard, "friend", configData.FriendsMarkers.FriendsColor1, configData.FriendsMarkers.FriendsColor2, configData.FriendsMarkers.FriendsAlpha, configData.FriendsMarkers.FriendsRadius);
            }

            if (configData.MapMarker.ClansUse && Clans != null && Clans.IsLoaded)
            {
                CreateMapMarker(cupboard, "clan", configData.ClansMarkers.ClansColor1, configData.ClansMarkers.ClansColor2, configData.ClansMarkers.ClansAlpha, configData.ClansMarkers.ClansRadius);
            }

            CreateMapMarker(cupboard, "owner", configData.OwnedMarkers.OwnedColor1, configData.OwnedMarkers.OwnedColor2, configData.OwnedMarkers.OwnedAlpha, configData.OwnedMarkers.OwnedRadius);
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            if (!(entity is BuildingPrivlidge)) return;
            var cupboard = (BuildingPrivlidge)entity;

            foreach (var marker in TCMarkers)
            {  
                if(marker == null)
                {
                    return;
                }

                if (marker.transform.position == cupboard.transform.position)
                {
                    marker.Kill();
                    marker.SendUpdate();
                }
            }
        }

        #endregion Oxide Hooks

        #region Core

        private void LoadMapMarkers()
        {
            
            count = 0;
            foreach (var entity in BaseNetworkable.serverEntities)
            {                
                if ((entity is BuildingPrivlidge))
                {
                    var cupboard = (BuildingPrivlidge)entity;
                    CreateMapMarker(cupboard, "other", configData.OtherMarkers.OtherColor1, configData.OtherMarkers.OtherColor2, configData.OtherMarkers.OtherAlpha, configData.OtherMarkers.OtherRadius);

                    if (configData.MapMarker.TeamsUse)
                    {
                        CreateMapMarker(cupboard, "team", configData.TeamsMarkers.TeamsColor1, configData.TeamsMarkers.TeamsColor2, configData.TeamsMarkers.TeamsAlpha, configData.TeamsMarkers.TeamsRadius);
                    }

                    if (configData.MapMarker.FriendsUse && Friends != null && Friends.IsLoaded)
                    {
                        CreateMapMarker(cupboard, "friend", configData.FriendsMarkers.FriendsColor1, configData.FriendsMarkers.FriendsColor2, configData.FriendsMarkers.FriendsAlpha, configData.FriendsMarkers.FriendsRadius);
                    }

                    if (configData.MapMarker.ClansUse && Clans != null && Clans.IsLoaded)
                    {
                        CreateMapMarker(cupboard, "clan", configData.ClansMarkers.ClansColor1, configData.ClansMarkers.ClansColor2, configData.ClansMarkers.ClansAlpha, configData.ClansMarkers.ClansRadius);
                    }

                    CreateMapMarker(cupboard, "owner", configData.OwnedMarkers.OwnedColor1, configData.OwnedMarkers.OwnedColor2, configData.OwnedMarkers.OwnedAlpha, configData.OwnedMarkers.OwnedRadius);
                    count++;
                }
            }      
            Puts(Lang("MarkersSpawned", null, count.ToString()));
        }

        private void RemoveMapMarkers()
        {
            foreach (var marker in TCMarkers)
            {
                if (marker != null)
                {
                    marker.Kill();
                    marker.SendUpdate();
                }
            }
            TCMarkers.Clear();
        }

        private void CreateMapMarker(BuildingPrivlidge cupboard, string type, string color1, string color2, float alpha, float radius)
        {
            MapMarkerGenericRadius mapMarker = GameManager.server.CreateEntity(configData.MapMarker.PREFAB_MARKER, cupboard.transform.position) as MapMarkerGenericRadius;

            if (mapMarker != null)
            {
                mapMarker.alpha = alpha;
                if (!ColorUtility.TryParseHtmlString(color1, out mapMarker.color1))
                {
                    mapMarker.color1 = Color.black;
                    PrintError($"Invalid map marker color1: {color1}");
                }

                if (!ColorUtility.TryParseHtmlString(color2, out mapMarker.color2))
                {
                    mapMarker.color2 = Color.white;
                    PrintError($"Invalid map marker color2: {color2}");
                }

                mapMarker.name = type;
                mapMarker.radius = radius;
                mapMarker.OwnerID = cupboard.OwnerID;
                TCMarkers.Add(mapMarker);
                mapMarker.Spawn();
                mapMarker.SendUpdate();

            }
        }

        #endregion Core

        #region Commands

        private void CommandTCMap(IPlayer player, string command, string[] args)
        {

            if (player.IsServer)
            {
                Message(player, "PlayersOnly", command);
                return;
            }

            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args == null || args.Length <= 0)
            {
                Message(player, "UsageTcMap", command);
                return;
            }

            switch (args[0].ToLower())
            {
                case "help":

                    Message(player, "UsageTcMap", command);
                    return;

                case "clear":

                    RemoveMapMarkers();
                    Message(player, "MarkersRemoved");
                    return;

                case "update":

                    RemoveMapMarkers();
                    LoadMapMarkers();
                    Message(player, "MarkersSpawned", count.ToString());
                    return;

                case "showtoall":

                    if (configData.MapMarker.visibleToAll)
                    {
                        configData.MapMarker.visibleToAll = false;
                        RemoveMapMarkers();
                        LoadMapMarkers();
                        Message(player, "MarkersVisibleAdmin");
                    }
                    else
                    {
                        configData.MapMarker.visibleToAll = true;
                        RemoveMapMarkers();
                        LoadMapMarkers();
                        Message(player, "MarkersVisibleAll");

                    }
                    return;
            }
        }

        #endregion Commands

        #region Helpers


        private void Message(IPlayer player, string key, params object[] args)
        {
            if (player.IsConnected)
            {
                player.Message(Lang(key, player.Id, args));
            }
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        #endregion Helpers

    }
}