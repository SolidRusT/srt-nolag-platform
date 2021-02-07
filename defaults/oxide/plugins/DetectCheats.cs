using CodeHatch.Common;
using CodeHatch.Engine.Networking;
using CodeHatch.TerrainAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Detect Cheats", "D-Kay & Troll Knight", "3.0.0")]
    [Description("Monitors players for the use of cheats.")]
    public class DetectCheats : ReignOfKingsPlugin
    {
        #region Data

        #region Objects

        public enum ActionType
        {
            Ignore,
            Kick,
            Ban
        }

        public abstract class ModuleBase
        {
            public delegate void EventHandler<in T>(T eventArgs);
            public event EventHandler<CheatDetectionEventArgs> CheatDetectionEvent;

            public virtual string Permission { get; } = "Module.Base.Admin";
            protected ModuleConfig Config { get; }
            protected PluginTimers Timer { get; }

            protected ModuleBase(ModuleConfig config, PluginTimers timer)
            {
                Config = config;
                Timer = timer;
            }

            public virtual void Initialize()
            {
                Timer.Every(Config.Interval, Elapsed);
            }

            protected abstract void Elapsed();

            protected void OnCheatDetected(CheatDetectionEventArgs eventArgs)
            {
                CheatDetectionEvent?.Invoke(eventArgs);
            }
        }

        public class CheatDetectionEventArgs : EventArgs
        {
            public Player Player { get; }
            public string Module { get; }
            public string Details { get; }
            public ActionType Action { get; set; }
            public bool LogEvent { get; set; }

            public CheatDetectionEventArgs(Player player, string module, string details, ActionType action = ActionType.Ignore, bool logEvent = true)
            {
                Player = player;
                Module = module;
                Details = details;
                Action = action;
                LogEvent = logEvent;
            }
        }

        public sealed class FlyModule : ModuleBase
        {
            public override string Permission { get; } = "Module.Fly.Admin";

            private readonly float _maxHeight;
            private readonly int _actionThreshold;

            private readonly Dictionary<ulong, Vector3> _lastPosition;
            private readonly Dictionary<ulong, int> _detections;

            public FlyModule(FlyConfig config, PluginTimers timer) : base(config, timer)
            {
                _maxHeight = config.MaxHeight;
                _actionThreshold = config.ActionThreshold;

                _lastPosition = new Dictionary<ulong, Vector3>();
                _detections = new Dictionary<ulong, int>();
            }

            protected override void Elapsed()
            {
                foreach (var player in Server.ClientPlayers)
                {
                    try
                    {
                        if (!player.Entity.IsInLoadedPage()) continue;
                        if (!player.CurrentCharacter.HasCompletedCreation) continue;
                        if (player.HasPermission(Permission)) continue;
                        CheckPlayer(player);
                    }
                    catch
                    {
                        //ignore and continue
                    }
                }
            }

            private void CheckPlayer(Player player)
            {
                if (!_lastPosition.ContainsKey(player.Id))
                    _lastPosition.Add(player.Id, player.Entity.Position);

                var oldPosition = _lastPosition[player.Id];
                var currentPosition = player.Entity.Position;
                _lastPosition[player.Id] = currentPosition;

                currentPosition.y -= 0.75f;
                if (TerrainAPIBase.WaterController.IsUnderWater(currentPosition))
                {
                    RemoveDetection(player);
                    return;
                }
                
                var collisions = Physics.OverlapSphere(currentPosition, 0.6f);
                if (collisions.Any(collision => collision.gameObject.layer != 15 && collision.gameObject.layer != 23))
                {
                    RemoveDetection(player);
                    return;
                }
                
                var distance = 1000f;
                var hits = Physics.SphereCastAll(currentPosition, 0.5f, Vector3.down);
                if (hits.Any())
                {
                    distance = (from raycastHit in hits
                                where raycastHit.transform.gameObject.layer != 15 && raycastHit.transform.gameObject.layer != 23
                                select Vector3.Distance(currentPosition, raycastHit.point)).Min();
                }
                if (distance <= _maxHeight)
                {
                    RemoveDetection(player);
                    return;
                }
                
                var velocity = (player.Entity.Position - oldPosition) / Config.Interval;
                if (velocity.y <= 0.5 && Mathf.Abs(velocity.x) < 10 && Mathf.Abs(velocity.z) < 10)
                {
                    RemoveDetection(player);
                    return;
                }

                AddDetection(player, distance, currentPosition, velocity);
            }

            private void AddDetection(Player player, float distance, Vector3 position, Vector3 velocity)
            {
                if (!_detections.ContainsKey(player.Id))
                    _detections.Add(player.Id, 0);

                var action = ActionType.Ignore;
                var currentDetections = _detections[player.Id] += 1;
                if (currentDetections >= _actionThreshold)
                {
                    action = Config.Action;
                    _detections.Remove(player.Id);
                }

                CheatDetectionEventArgs eventArgs;
                try
                {
                    eventArgs = new CheatDetectionEventArgs(player, "Fly", $"{player.Name} has been detected {distance} meter above ground. Position: {position}. Velocity: {velocity}. Concurrent detections: {currentDetections}", action, Config.Log);
                }
                catch (Exception e)
                {
                    eventArgs = new CheatDetectionEventArgs(player, "Fly", e.Message, action, Config.Log);
                }
                this.OnCheatDetected(eventArgs);
            }

            private void RemoveDetection(Player player)
            {
                if (!_detections.ContainsKey(player.Id)) return;

                var currentCount = _detections[player.Id] -= 1;
                if (currentCount <= 0) _detections.Remove(player.Id);
            }
        }

        #endregion

        #region Configuration

        private Configuration _config;
        private new Configuration Config => _config ?? (_config = new Configuration(base.Config));

        public class Configuration
        {
            private DynamicConfigFile Config { get; }

            public Configuration(DynamicConfigFile config)
            {
                this.Config = config;
            }

            public ConfigData Load()
            {
                var config = Config.ReadObject<ConfigData>();
                if (config == null) return LoadDefault();

                Save(config);
                return config;
            }

            public void Save(ConfigData data)
            {
                Config.WriteObject(data);
            }

            public ConfigData LoadDefault()
            {
                var config = new ConfigData
                {
                    FlyModule = new FlyConfig
                    {
                        Enabled = true,
                        Log = true,
                        Interval = 1f,
                        Action = ActionType.Ban,
                        MaxHeight = 6f,
                        ActionThreshold = 3
                    }
                };
                Save(config);
                return config;
            }
        }

        public class ConfigData
        {
            [JsonProperty(PropertyName = "Possible actions. Cannot be modified.", Order = 0)]
            public string[] Actions { get; }

            [JsonProperty(PropertyName = "Module: Fly detection", Order = 1)]
            public FlyConfig FlyModule { get; set; }

            public ConfigData()
            {
                Actions = Enum.GetNames(typeof(ActionType));
            }
        }

        public class ModuleConfig
        {
            [JsonProperty(PropertyName = "Enabled", Order = 0)]
            public bool Enabled { get; set; }

            [JsonProperty(PropertyName = "Log detections", Order = 1)]
            public bool Log { get; set; }

            [JsonProperty(PropertyName = "Time between checks", Order = 2)]
            public float Interval { get; set; }

            [JsonProperty(PropertyName = "Detection action", Order = 3)]
            [JsonConverter(typeof(StringEnumConverter))]
            public ActionType Action { get; set; }
        }

        public class FlyConfig : ModuleConfig
        {
            [JsonProperty(PropertyName = "Maximum allowed player height above objects", Order = 10)]
            public float MaxHeight { get; set; }

            [JsonProperty(PropertyName = "Number of detections before action is performed", Order = 11)]
            public int ActionThreshold { get; set; }
        }

        #endregion

        #region Fields

        private readonly List<ModuleBase> _modules = new List<ModuleBase>();

        #endregion

        #endregion

        #region Initialization

        protected override void LoadDefaultConfig() => Config.LoadDefault();

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Kick.Fly.Reason", "Was considered using fly hacks." },
                { "Ban.Fly.Reason", "Was detected using fly hacks. Please join the discord server if you wish to appeal for unban." },
            }, this);
        }

        private void Init()
        {
            var config = Config.Load();
            if (config.FlyModule.Enabled)
            {
                var module = new FlyModule(config.FlyModule, timer);
                module.CheatDetectionEvent += OnCheatDetectionEvent;
                RegisterPermissions(module.Permission);
                _modules.Add(module);
            }

            Config.Save(config);
        }

        private void Loaded()
        {
            _modules.Foreach(x => x.Initialize());
        }

        #endregion

        #region Event Handling

        private void OnCheatDetectionEvent(CheatDetectionEventArgs eventArgs)
        {
            if (eventArgs.LogEvent)
            {
                try
                {
                    Log(eventArgs.Details);
                }
                catch
                {
                    PrintWarning(eventArgs.Details);
                }
            }
            
            switch (eventArgs.Action)
            {
                case ActionType.Kick:
                    Server.Kick(eventArgs.Player, GetMessage($"Kick.{eventArgs.Module}.Reason", eventArgs.Player), true);
                    break;
                case ActionType.Ban:
                    Server.Ban(eventArgs.Player, GetMessage($"Ban.{eventArgs.Module}.Reason", eventArgs.Player));
                    break;
            }
        }

        #endregion

        #region Utility

        private void RegisterPermissions(params string[] permissions) => permissions.Foreach(p => permission.RegisterPermission($"{Name}.{p}", this));

        private string GetMessage(string key, Player player, params object[] args) => string.Format(lang.GetMessage(key, this, player?.Id.ToString()), args);

        private void Log(string msg) => LogToFile($"Detections {DateTime.Now:yyyy-MM-dd}", $"[{DateTime.Now:h:mm:ss tt}] {msg}", this, false);

        #endregion
    }
}