// #define DEBUG

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;
using Time = Oxide.Core.Libraries.Time;

namespace Oxide.Plugins
{
    [Info("Auto Purge", "misticos", "2.1.1")]
    [Description("Remove entities if the owner becomes inactive")]
    public class AutoPurge : CovalencePlugin
    {
        #region Variables

        private static AutoPurge _ins;

        private Dictionary<string, bool> _canPurgeCache = new Dictionary<string, bool>();
        private HashSet<string> _logCache = new HashSet<string>();
        
        private HashSet<string> _deployables = new HashSet<string>();

        [PluginReference("PlaceholderAPI")]
        private Plugin _placeholderAPI = null;

        private Time _time = GetLibrary<Time>();

        private Timer _timerPurge;

        private const string CommandRunPurgeName = "autopurge.run";
        private const string PermissionRunPurge = "autopurge.run";

        #endregion

        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Purge Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<PurgeSettings> Purges = new List<PurgeSettings> {new PurgeSettings()};

            [JsonProperty(PropertyName = "Purge Timer Frequency")]
            public float PurgeFrequency = 900f;

            [JsonProperty(PropertyName = "Purge On Startup")]
            public bool PurgeOnStartup = false;

            [JsonProperty(PropertyName = "Entities Per Step")]
            public int EntitiesPerStep = 512;

            [JsonProperty(PropertyName = "Purge Building Blocks")]
            public bool PurgeBuildingBlocks = true;

            [JsonProperty(PropertyName = "Purge Deployables")]
            public bool PurgeDeployables = true;

            [JsonProperty(PropertyName = "Purge Sleepers")]
            public bool PurgeSleepers = true;

            [JsonProperty(PropertyName = "Use Logs")]
            public bool UseLogs = true;

            public class PurgeSettings
            {
                [JsonProperty(PropertyName = "Permission")]
                public string Permission = "";

                [JsonProperty(PropertyName = "Lifetime")]
                public string LifetimeRaw = "none";

                [JsonIgnore]
                public uint Lifetime = 0;

                [JsonIgnore]
                public bool NoPurge = false;

                public static PurgeSettings Find(string playerId)
                {
                    PurgeSettings best = null;

                    for (var i = 0; i < _ins._config.Purges.Count; i++)
                    {
                        var purge = _ins._config.Purges[i];
                        if (!string.IsNullOrEmpty(purge.Permission) &&
                            !_ins.permission.UserHasPermission(playerId, purge.Permission))
                            continue;

                        if (purge.NoPurge)
                            return purge;

                        if (best == null || best.Lifetime < purge.Lifetime)
                            best = purge;
                    }

                    return best;
                }
            }
        }

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

        #region Work with Data

        private PluginData _data;

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }

        // ReSharper disable MemberCanBePrivate.Local
        private class PluginData
        {
            [JsonProperty("Users", NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore)]
#pragma warning disable 414
            public List<UserData> Users = null;

            // ReSharper disable once ClassNeverInstantiated.Local
            public class UserData
            {
                public string Id = "";

                public uint LastSeen = 0;
            }
#pragma warning restore 414

            public Dictionary<string, uint> LastSeen = new Dictionary<string, uint>();
        }
        // ReSharper restore MemberCanBePrivate.Local

        #endregion

        #region Commands

        private void CommandRunPurge(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PermissionRunPurge))
            {
                player.Reply(GetMsg("No Permission", player.Id));
                return;
            }

            RunPurge(player);
        }

        #endregion

        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"No Permission", "You don't have enough permissions"},
                {"Purge: Started", "The purge has been started."},
                {"Purge: Ended", "The purge has been completed. Purged entities: {purged}."}
            }, this);
        }

        private void Init()
        {
            _ins = this;
            LoadData();

            const string noPurgeLifetime = "none";

            for (var i = 0; i < _config.Purges.Count; i++)
            {
                var purge = _config.Purges[i];
                if (!string.IsNullOrEmpty(purge.Permission))
                    permission.RegisterPermission(purge.Permission, this);

                var isNoPurge = purge.LifetimeRaw.Equals(noPurgeLifetime, StringComparison.CurrentCultureIgnoreCase);
                if (!isNoPurge && !ConvertToSeconds(purge.LifetimeRaw, out purge.Lifetime))
                {
                    PrintWarning(
                        $"Unable to parse {purge.LifetimeRaw} value as Lifetime. Disabling purge for this purge setup");
                    purge.NoPurge = true;
                }

                if (isNoPurge)
                    purge.NoPurge = true;
            }

            if (_data.Users != null)
            {
                foreach (var user in _data.Users)
                    _data.LastSeen[user.Id] = user.LastSeen;

                _data.Users = null;
                SaveData();
            }

            permission.RegisterPermission(PermissionRunPurge, this);

            AddCovalenceCommand(CommandRunPurgeName, nameof(CommandRunPurge));
        }

        private void OnServerInitialized()
        {
            UpdateLastSeen(_time.GetUnixTimestamp());

            foreach (var item in ItemManager.itemList)
            {
                var deployable = item.GetComponent<ItemModDeployable>();
                if (deployable == null)
                    continue;

                _deployables.Add(deployable.entityPrefab.resourcePath);
            }

            if (_config.PurgeOnStartup)
                RunPurge();

            if (_config.PurgeFrequency > 0f)
                _timerPurge = timer.Every(_config.PurgeFrequency, () => RunPurge());
        }

        private void Unload()
        {
            _timerPurge?.Destroy();

            SaveData();

            _ins = null;
        }

        private void OnUserConnected(IPlayer player) => _data.LastSeen[player.Id] = _time.GetUnixTimestamp();

        private void OnUserDisconnected(IPlayer player) => _data.LastSeen[player.Id] = _time.GetUnixTimestamp();

        private void OnPlaceholderAPIReady()
        {
            _placeholderAPI?.CallHook("AddPlaceholder", this, "autopurge.lastseen",
                new Func<IPlayer, string, object>((p, o) =>
                {
                    uint lastSeen;
                    return !string.IsNullOrEmpty(p?.Id) && _data.LastSeen.TryGetValue(p.Id, out lastSeen)
                        ? _time.GetDateTimeFromUnix(lastSeen)
                        : (object) null;
                }),
                $"Date the player was last seen within {Title}. Options: \"local\" to use local time offset, UTC (default)");

            _placeholderAPI?.CallHook("AddPlaceholder", this, "autopurge.inactivefor",
                new Func<IPlayer, string, object>((p, o) =>
                {
                    uint lastSeen;
                    return !string.IsNullOrEmpty(p?.Id) && _data.LastSeen.TryGetValue(p.Id, out lastSeen)
                        ? TimeSpan.FromSeconds(_time.GetUnixTimestamp() - lastSeen)
                        : (object) null;
                }), $"Time since the player was last seen within {Title}");

            _placeholderAPI?.CallHook("AddPlaceholder", this, "autopurge.next",
                new Func<IPlayer, string, object>((p, o) =>
                {
                    uint lastSeen;
                    if (string.IsNullOrEmpty(p?.Id) || !_data.LastSeen.TryGetValue(p.Id, out lastSeen))
                        return null;

                    var settings = Configuration.PurgeSettings.Find(p.Id);
                    if (settings.NoPurge)
                        return null;

                    return _time.GetDateTimeFromUnix(lastSeen + settings.Lifetime);
                }), "Next purge for the specified player", 15d);
        }

        #endregion

        #region Last Seen

        private void UpdateLastSeen(uint timestamp)
        {
            foreach (var player in players.Connected)
            {
                _data.LastSeen[player.Id] = timestamp;
            }

            SaveData();
        }

        #endregion

        #region Purge

        private void RunPurge(IPlayer caller = null)
        {
            // Run a coroutine so that nothing lags
            InvokeHandler.Instance.StartCoroutine(RunPurgeEnumerator(caller));
        }

        private static readonly WaitForFixedUpdate WaitForFixedUpdate = new WaitForFixedUpdate();

        private IEnumerator RunPurgeEnumerator(IPlayer caller)
        {
            if ((caller?.IsConnected ?? false) && !caller.IsServer)
                caller.Reply(GetMsg("Purge: Started", caller.Id));
            Puts(GetMsg("Purge: Started", string.Empty));

            var purged = 0;
            var timestamp = _time.GetUnixTimestamp();

            UpdateLastSeen(timestamp);

#if DEBUG
            var entitiesCount = BaseNetworkable.serverEntities.entityList.Values.Count;
            var stopwatch = Stopwatch.StartNew();
#endif

            for (var i = BaseNetworkable.serverEntities.entityList.Values.Count - 1; i >= 0; i--)
            {
                // Has to be here, what if the plugin unloads when it's still purging?
                if (!IsLoaded)
                    break;

#if DEBUG
                stopwatch.Stop();
#endif

                if (i % _config.EntitiesPerStep == 0)
                    yield return WaitForFixedUpdate;

#if DEBUG
                stopwatch.Start();
#endif

                var entity = BaseNetworkable.serverEntities.entityList.Values[i] as BaseCombatEntity;

                // Skipping invalid entities
                if (entity == null || entity.IsDestroyed || entity.IsNpc)
                    continue;

                if (_config.PurgeSleepers && entity is BasePlayer)
                {
                    var player = entity as BasePlayer; // player should never be an NPC
                    if (!player.IsConnected && CanPurgeUser(player.UserIDString, timestamp))
                    {
                        player.Die();
                        purged++;
                    }
                }

                if (_config.PurgeDeployables && _deployables.Contains(entity.PrefabName) ||
                    _config.PurgeBuildingBlocks && entity is BuildingBlock)
                {
                    var decayEntity = entity as DecayEntity;
                    if (CanPurge(entity.OwnerID, decayEntity == null
                        ? entity.GetBuildingPrivilege() // Use the fastest way if possible
                        : decayEntity.GetBuilding()?.GetDominatingBuildingPrivilege(), timestamp))
                    {
                        entity.Die();
                        purged++;
                    }
                }
            }

#if DEBUG
            stopwatch.Stop();
            Interface.Oxide.LogDebug(
                $"Total: {stopwatch.Elapsed.TotalMilliseconds}ms / Per Entity: {stopwatch.Elapsed.TotalMilliseconds / entitiesCount}ms / Per Purged: {stopwatch.Elapsed.TotalMilliseconds / purged}ms");
#endif

            if (_logCache.Count > 0)
            {
                LogToFile("purged", string.Join(Environment.NewLine, _logCache), this);
                _logCache.Clear();
            }

            _canPurgeCache.Clear();

            if ((caller?.IsConnected ?? false) && !caller.IsServer)
                caller.Reply(GetMsg("Purge: Ended", caller.Id).Replace("{purged}", purged.ToString()));
            Puts(GetMsg("Purge: Ended", string.Empty).Replace("{purged}", purged.ToString()));
        }

        private bool CanPurge(ulong ownerId, BuildingPrivlidge privilege, uint timestamp)
        {
            if (!CanPurgeUser(ownerId.ToString(), timestamp))
                return false; // We CANNOT purge owner, no need to check further
            
            if (privilege == null)
                return true; // We CAN purge owner and there is NO privilege

            // TODO: Results cache per privilege
            for (var i = 0; i < privilege.authorizedPlayers.Count; i++)
            {
                if (!CanPurgeUser(privilege.authorizedPlayers[i].userid.ToString(), timestamp))
                    return false; // We CANNOT purge an authorized player
            }

            // We CAN purge owner and there are NO users authorized who we CANNOT purge
            return true;
        }

        private bool CanPurgeUser(string id, uint timestamp)
        {
            if (string.IsNullOrEmpty(id) || !id.IsSteamId())
                return false;

            bool result;
            if (_canPurgeCache.TryGetValue(id, out result))
                return result;

            if ((_canPurgeCache[id] = result = CanPurgeUserInternal(id, timestamp)) && _config.UseLogs)
            {
                _logCache.Add(
                    $"[{DateTime.UtcNow:T}] Marked {id} as suitable for purge (Last seen: {_time.GetDateTimeFromUnix(_data.LastSeen[id]):g})");
            }

            return result;
        }

        private bool CanPurgeUserInternal(string id, uint timestamp)
        {
            var purge = Configuration.PurgeSettings.Find(id);
            if (purge == null || purge.NoPurge)
                return false;

            uint lastSeen;
            if (!_data.LastSeen.TryGetValue(id, out lastSeen))
            {
                _data.LastSeen[id] = timestamp;
                return false;
            }

            return timestamp > lastSeen + purge.Lifetime;
        }

        #endregion

        #region Parsers

        private static readonly Regex RegexStringTime = new Regex(@"(\d+)([dhms])", RegexOptions.Compiled);

        private static bool ConvertToSeconds(string time, out uint seconds)
        {
            seconds = 0;
            if (time == "0" || string.IsNullOrEmpty(time)) return true;
            var matches = RegexStringTime.Matches(time);
            if (matches.Count == 0) return false;
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (match.Groups[2].Value)
                {
                    case "d":
                    {
                        seconds += uint.Parse(match.Groups[1].Value) * 24 * 60 * 60;
                        break;
                    }
                    case "h":
                    {
                        seconds += uint.Parse(match.Groups[1].Value) * 60 * 60;
                        break;
                    }
                    case "m":
                    {
                        seconds += uint.Parse(match.Groups[1].Value) * 60;
                        break;
                    }
                    case "s":
                    {
                        seconds += uint.Parse(match.Groups[1].Value);
                        break;
                    }
                }
            }

            return true;
        }

        #endregion

        #region Helpers

        private string GetMsg(string key, string userId) => lang.GetMessage(key, this, userId);

        #endregion
    }
}