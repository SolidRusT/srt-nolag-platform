using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Death History", "MadKingCraig", "1.0.3")]
    [Description("Get the grid locations of previous deaths.")]
    class DeathHistory : RustPlugin
	{
        #region Fields

        private const string CanUsePermission = "deathhistory.use";

        private PluginConfiguration _configuration;
        private DynamicConfigFile _data;

        DeathHistoryData dhData;

        private Dictionary<string, List<List<float>>> _deaths = new Dictionary<string, List<List<float>>>();

        private float _worldSize = (ConVar.Server.worldsize);

        #endregion

        #region Commands

        [ChatCommand("deaths")]
        private void DeathsCommand(BasePlayer player, string command, string[] args)
        {
            string PlayerID = player.UserIDString;

            if (!permission.UserHasPermission(PlayerID, CanUsePermission))
            {
                SendReply(player, lang.GetMessage("NoPermission", this, PlayerID));
                return;
            }

            if (_deaths.ContainsKey(player.UserIDString))
            {
                List<string> deathLocations = SendCorpseLocations(player);
                if (deathLocations.Count > _configuration.MaxNumberOfDeaths)
                    deathLocations.RemoveRange(0, (deathLocations.Count - _configuration.MaxNumberOfDeaths));
                string message = string.Join(", ", deathLocations);
                SendReply(player, String.Format(lang.GetMessage("DeathLocation", this, player.UserIDString), message));
            }
            else
                SendReply(player, lang.GetMessage("UnknownLocation", this, PlayerID));
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(CanUsePermission, this);
        }

        private void Loaded()
        {
            _data = Interface.Oxide.DataFileSystem.GetFile("death_history_data");
        }

        private void OnServerInitialized()
        {
            _configuration = Config.ReadObject<PluginConfiguration>();
            Config.WriteObject(_configuration);

            LoadData();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["DeathLocation"] = "Death Locations (oldest to most recent)\n{0}",
                ["UnknownLocation"] = "Your last death location is unknown.",
                ["NoPermission"] = "You do not have permission to use this command."
            }, this);
        }

        private void OnNewSave(string filename)
        {
            NewData();
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            if (entity.gameObject == null) return;

            var player = entity as BasePlayer;

            if (player == null || entity.IsNpc) return;

            string userID = player.UserIDString;
            Vector3 deathPosition = entity.transform.position;
            List<float> shortDeathPosition = new List<float> { deathPosition.x, deathPosition.y, deathPosition.z };

            if (!_deaths.ContainsKey(userID))
                _deaths.Add(userID, new List<List<float>>());

            if (_deaths[userID].Count >= _configuration.MaxNumberOfDeaths)
                _deaths[userID].RemoveRange(0, (_deaths[userID].Count - _configuration.MaxNumberOfDeaths) + 1);

            _deaths[userID].Add(shortDeathPosition);

            SaveData();
        }

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig() => _configuration = new PluginConfiguration();

        private sealed class PluginConfiguration
        {
            [JsonProperty(PropertyName = "Number of Deaths to Keep")]
            public int MaxNumberOfDeaths = 5;
        }

        #endregion

        #region Functions

        private string CalculateGridPosition(Vector3 position)
        {
            int maxGridSize = Mathf.FloorToInt(World.Size / 146.3f) - 1;
            int xGrid = Mathf.Clamp(Mathf.FloorToInt((position.x + (World.Size / 2f)) / 146.3f), 0, maxGridSize);
            string extraA = string.Empty;
            if (xGrid > 26) extraA = $"{(char)('A' + (xGrid / 26 - 1))}";
            return $"{extraA}{(char)('A' + xGrid % 26)}{Mathf.Clamp(maxGridSize - Mathf.FloorToInt((position.z + (World.Size / 2f)) / 146.3f), 0, maxGridSize).ToString()}";
        }

        private List<string> GetGrids(List<Vector3> deathLocations)
        {
            List<string> deathGrids = new List<string>();
            
            foreach (var location in deathLocations)
            {
                deathGrids.Add(CalculateGridPosition(location));
            }
            
            return deathGrids;
        }

        private List<string> SendCorpseLocations(BasePlayer player)
        {
            List<List<float>> shortDeathLocations = _deaths[player.UserIDString];
            List<Vector3> allDeathLocations = new List<Vector3>();
            foreach (var location in shortDeathLocations)
            {
                allDeathLocations.Add(new Vector3(location[0], location[1], location[2]));
            }

            return GetGrids(allDeathLocations);
        }

        #endregion

        #region Data Management

        private void SaveData()
        {
            dhData.Deaths = _deaths;
            _data.WriteObject(dhData);
        }

        private void LoadData()
        {
            try
            {
                dhData = _data.ReadObject<DeathHistoryData>();
                _deaths = dhData.Deaths;
            }
            catch
            {
                dhData = new DeathHistoryData();
            }
        }

        private void NewData()
        {
            _deaths = new Dictionary<string, List<List<float>>>();
            SaveData();
        }

        public class DeathHistoryData
        {
            public Dictionary<string, List<List<float>>> Deaths = new Dictionary<string, List<List<float>>>();
        }

        #endregion
    }
}
