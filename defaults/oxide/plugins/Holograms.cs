using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Holograms", "birthdates", "1.4.3")]
    [Description("Provide floating text with information to players")]
    public class Holograms : RustPlugin
    {
        #region Variables

        /// <summary>
        ///     Layer mask with players (saves performances)
        /// </summary>
        private int PlayerMask { get; } = LayerMask.GetMask("Player (Server)");

        /// <summary>
        ///     The obstruction layer mask from <see cref="ConfigFile.ObstructionMask" />
        /// </summary>
        private LayerMask ObstructionMask { get; set; }

        /// <summary>
        ///     Our instance
        /// </summary>
        private static Holograms Instance { get; set; }

        /// <summary>
        ///     A map of placeholders: id -> instance
        /// </summary>
        private readonly IDictionary<string, Placeholder> _placeholders = new Dictionary<string, Placeholder>();

        /// <summary>
        ///     A global stopwatch
        /// </summary>
        private readonly Stopwatch _stopwatch = new Stopwatch();

        /// <summary>
        ///     Placeholder regex (%id%)
        /// </summary>
        private readonly Regex _placeholderRegex = new Regex("%.+?%");

        /// <summary>
        ///     Main permission
        /// </summary>
        private const string UsePermission = "holograms.use";

        #endregion

        #region Hooks

        private void Init()
        {
            Instance = this;
            permission.RegisterPermission(UsePermission, this);
            _data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);
            timer.Every(_config.PlaceholderInterval, UpdatePlaceholders);
            cmd.AddChatCommand("h", this, nameof(HologramPlayerCommand));
            cmd.AddConsoleCommand("h", this, nameof(HologramConsoleCommand));
            if (_config.ObstructionMask != null) ObstructionMask = LayerMask.GetMask(_config.ObstructionMask);
        }

        private void Unload()
        {
            SaveData();
            Instance = null;
        }

        private void OnServerInitialized()
        {
            timer.Every(_config.TickInterval, TickHolograms);
            RegisterPlaceholder("players", () => $"{BasePlayer.activePlayerList.Count:n0}");
            RegisterPlaceholder("sleepers", () => $"{BasePlayer.sleepingPlayerList.Count:n0}");
            RegisterPlaceholder("queued", () => $"{ServerMgr.Instance.connectionQueue.Queued:n0}");
            RegisterPlaceholder("joining", () => $"{ServerMgr.Instance.connectionQueue.Joining:n0}");
            RegisterPlaceholder("entities", () => $"{BaseNetworkable.serverEntities.Count:n0}");
            RegisterPlaceholder("fps", 1f, () => $"{Performance.report.frameRate:n0}");
        }

        #endregion

        #region Core Logic

        /// <summary>
        ///     Update all our placeholders
        /// </summary>
        private void UpdatePlaceholders()
        {
            foreach (var placeholder in _placeholders.Values) placeholder.TryUpdate();
        }

        /// <summary>
        ///     Get a placeholder's value/data
        /// </summary>
        /// <param name="id">Placeholder's ID</param>
        /// <returns>A <see cref="string" /> if a value/data was found. <see langword="null" /> otherwise.</returns>
        private string GetPlaceholderValue(string id)
        {
            Placeholder placeholder;
            return !_placeholders.TryGetValue(id, out placeholder) ? null : placeholder.Value;
        }

        /// <summary>
        ///     Register a new placeholder
        /// </summary>
        /// <param name="id">Placeholder's ID</param>
        /// <param name="updateTime">Placeholder's update interval in seconds</param>
        /// <param name="callback">Placeholder's callback with data</param>
        [HookMethod("RegisterPlaceholder")]
        private void RegisterPlaceholder(string id, float updateTime, Func<string> callback)
        {
            _placeholders[id] = new Placeholder(id, updateTime, callback);
        }

        /// <summary>
        ///     Register a new placeholder at a 10 second update interval
        /// </summary>
        /// <param name="id">Placeholder's ID</param>
        /// <param name="callback">Placeholder's callback with data</param>
        private void RegisterPlaceholder(string id, Func<string> callback)
        {
            RegisterPlaceholder(id, 10f, callback);
        }

        /// <summary>
        ///     Tick all holograms
        /// </summary>
        private void TickHolograms()
        {
            foreach (var hologram in _data.Holograms.Values) hologram.Tick();
        }

        /// <summary>
        ///     Try to create a hologram
        /// </summary>
        /// <param name="id">Hologram's ID</param>
        /// <param name="spawnPos">Hologram's position</param>
        /// <returns>
        ///     <see langword="true" />, if the hologram was created. <see langword="false" /> if there is currently a
        ///     hologram with that name.
        /// </returns>
        [HookMethod("CreateHologram")]
        private Hologram CreateHologram(string id, Vector3 spawnPos = default(Vector3))
        {
            if (_data.Holograms.ContainsKey(id)) return null;
            return _data.Holograms[id] = new Hologram
            {
                Id = id, Position = spawnPos,
                Lines = new List<Line>
                    {new Line {Text = $"<size=25>Hologram with id <color=green>\"{id}\"</color></size>"}}
            };
        }

        /// <summary>
        ///     Try to find a hologram by it's ID
        /// </summary>
        /// <param name="id">Target ID</param>
        /// <returns>A <see cref="Hologram" /> if found. <see langword="null" /> otherwise.</returns>
        [HookMethod("FindHologram")]
        private Hologram FindHologram(string id)
        {
            Hologram hologram;
            return !_data.Holograms.TryGetValue(id, out hologram) ? null : hologram;
        }

        /// <summary>
        ///     Try to delete a hologram by ID
        /// </summary>
        /// <param name="id">Hologram's ID</param>
        /// <returns><see langword="true" />, if the hologram was removed. <see langword="false" /> otherwise.</returns>
        [HookMethod("DeleteHologram")]
        private bool DeleteHologram(string id)
        {
            return _data.Holograms.Remove(id);
        }

        #endregion

        #region Commands

        [ChatCommand("hologram")]
        private void HologramPlayerCommand(BasePlayer player, string label, string[] args)
        {
            HologramCommand(new PlayerExecutor(player), label, args);
        }

        [ConsoleCommand("hologram")]
        private void HologramConsoleCommand(ConsoleSystem.Arg arg)
        {
            HologramCommand(new ConsoleExecutor(arg), arg.cmd.Name, arg.Args);
        }

        private void HologramCommand(IExecutor executor, string label, string[] args)
        {
            if (!executor.HasPermission(UsePermission))
            {
                executor.Reply(lang.GetMessage("NoPermission", this, executor.GetId()));
                return;
            }

            if (args.Length < 1)
            {
                SendHelp(executor, label);
                return;
            }

            var arg = args[0].ToLowerInvariant();
            if (arg.Equals("list"))
            {
                var str = _data.Holograms.Count > 0
                    ? string.Join(", ", _data.Holograms.Select(hologram => hologram.Value.Id).ToArray())
                    : "N/A";
                executor.Reply(string.Format(lang.GetMessage("List", this, executor.GetId()), str));
                return;
            }

            // Hologram Commands
            if (args.Length > 1)
            {
                var name = args[1].ToLowerInvariant().EscapeRichText();
                switch (arg)
                {
                    case "create":
                        var created = CreateHologram(name, executor.GetPosition()) != null;
                        if (created) SaveData();
                        executor.Reply(lang.GetMessage(
                            !created ? "HologramExists" : "HologramCreated", this,
                            executor.GetId()));
                        return;
                    case "delete":
                        var removed = DeleteHologram(name);
                        if (removed) SaveData();
                        executor.Reply(lang.GetMessage(removed ? "HologramDeleted" : "InvalidHologram",
                            this, executor.GetId()));
                        return;
                }

                var hologram = FindHologram(name);
                if (hologram == null)
                {
                    executor.Reply(lang.GetMessage("InvalidHologram", this, executor.GetId()));
                    return;
                }

                if (arg.Equals("teleport"))
                {
                    if (!executor.Teleport(hologram.Position)) return;
                    executor.Reply(string.Format(lang.GetMessage("Teleported", this, executor.GetId()), hologram.Id));
                    return;
                }
                if (arg.Equals("tphere"))
                {
                    hologram.Position = executor.GetPosition();
                    executor.Reply(string.Format(lang.GetMessage("TeleportedHere", this, executor.GetId()), hologram.Id));
                    return;
                }

                if (args.Length > 2)
                {
                    var thirdArg = args[2].ToLowerInvariant();
                    switch (arg)
                    {
                        case "spacing":
                            float spacing;
                            if (!float.TryParse(thirdArg, out spacing))
                            {
                                executor.Reply(lang.GetMessage("InvalidNumber", this, executor.GetId()));
                                return;
                            }

                            hologram.LineSpacing = spacing;
                            executor.Reply(string.Format(lang.GetMessage("LineSpacing", this, executor.GetId()),
                                hologram.Id, spacing));
                            return;
                        case "radius":
                            float radius;
                            if (!float.TryParse(thirdArg, out radius))
                            {
                                executor.Reply(lang.GetMessage("InvalidNumber", this, executor.GetId()));
                                return;
                            }

                            hologram.ViewRadius = radius;
                            executor.Reply(string.Format(lang.GetMessage("ViewRadius", this, executor.GetId()), radius,
                                hologram.Id));
                            return;
                        case "update":
                            float update;
                            if (!float.TryParse(thirdArg, out update))
                            {
                                executor.Reply(lang.GetMessage("InvalidNumber", this, executor.GetId()));
                                return;
                            }

                            hologram.UpdateInterval = update;
                            executor.Reply(string.Format(lang.GetMessage("UpdateInterval", this, executor.GetId()),
                                update, hologram.Id));
                            return;
                        case "permission":
                            hologram.Permission = thirdArg.Equals("null") ? null : thirdArg;
                            executor.Reply(string.Format(lang.GetMessage("Permission", this, executor.GetId()),
                                $"holograms.{thirdArg}", hologram.Id));
                            return;
                        case "rename":
                            if (FindHologram(thirdArg) != null)
                            {
                                executor.Reply(lang.GetMessage("HologramExists", this, executor.GetId()));
                                return;
                            }

                            var oldName = hologram.Id;
                            _data.Holograms.Remove(hologram.Id);
                            hologram.Id = thirdArg;
                            _data.Holograms[hologram.Id] = hologram;
                            executor.Reply(string.Format(lang.GetMessage("Renamed", this, executor.GetId()), oldName,
                                hologram.Id));
                            SaveData();
                            return;
                        case "addline":
                            var lines = string.Join(" ", args, 2, args.Length - 2);
                            hologram.AddLine(lines);
                            executor.Reply(string.Format(lang.GetMessage("AddLine", this, executor.GetId()),
                                hologram.Id));
                            return;
                        case "setline":
                            int line;
                            if (!int.TryParse(thirdArg, out line))
                            {
                                executor.Reply(lang.GetMessage("InvalidNumber", this, executor.GetId()));
                                return;
                            }

                            if (line > hologram.Lines.Count || line < 1)
                            {
                                executor.Reply(lang.GetMessage("InvalidIndex", this, executor.GetId()));
                                return;
                            }

                            var text = string.Join(" ", args, 3, args.Length - 3);
                            hologram.SetLine(line - 1, text);
                            executor.Reply(string.Format(lang.GetMessage("SetLine", this, executor.GetId()), line,
                                hologram.Id));
                            return;
                        case "remline":
                            int remLine;
                            if (!int.TryParse(thirdArg, out remLine))
                            {
                                executor.Reply(lang.GetMessage("InvalidNumber", this, executor.GetId()));
                                return;
                            }

                            if (remLine > hologram.Lines.Count || remLine < 1)
                            {
                                executor.Reply(lang.GetMessage("InvalidIndex", this, executor.GetId()));
                                return;
                            }

                            hologram.RemoveLine(remLine - 1);
                            executor.Reply(string.Format(lang.GetMessage("RemovedLine", this, executor.GetId()),
                                remLine, hologram.Id));
                            return;
                    }
                }
            }

            SendHelp(executor, label);
        }

        /// <summary>
        ///     Send the help locale message to an executor
        /// </summary>
        /// <param name="executor">Target executor</param>
        /// <param name="label">Command label</param>
        private void SendHelp(IExecutor executor, string label)
        {
            executor.Reply(string.Format(lang.GetMessage("Help", this, executor.GetId()), label));
        }

        #endregion

        #region Classes

        /// <summary>
        ///     Base placeholder implementation
        /// </summary>
        private class Placeholder
        {
            /// <summary>
            ///     This placeholder's ID
            /// </summary>
            private string Id { get; }

            /// <summary>
            ///     The next update
            /// </summary>
            private DateTime _nextUpdate;

            /// <summary>
            ///     Update interval in seconds
            /// </summary>
            private float UpdateTime { get; }

            /// <summary>
            ///     Placeholder value callback
            /// </summary>
            private Func<string> Callback { get; }

            /// <summary>
            ///     Current placeholder value
            /// </summary>
            public string Value { get; private set; }

            public Placeholder(string id, float updateTime, Func<string> callback)
            {
                Id = id;
                UpdateTime = updateTime;
                Callback = callback;
                _nextUpdate = DateTime.UtcNow;
                Value = null;
            }

            /// <summary>
            ///     Try to update <see cref="Value" />
            /// </summary>
            public void TryUpdate()
            {
                if (_nextUpdate > DateTime.UtcNow) return;
                _nextUpdate = DateTime.UtcNow.AddSeconds(UpdateTime);
                Instance._stopwatch.Start();
                Value = Callback.Invoke();
                Instance._stopwatch.Stop();
                Instance._stopwatch.Reset();
                if (Instance._stopwatch.ElapsedMilliseconds >= 100)
                    Instance.PrintWarning(
                        $"The placeholder with id \"{Id}\" took {Instance._stopwatch.ElapsedMilliseconds}ms to execute!");
            }
        }

        /// <summary>
        ///     Simple line class for placeholders
        /// </summary>
        private class Line
        {
            /// <summary>
            ///     Stored text
            /// </summary>
            public string Text { get; set; }

            /// <summary>
            ///     Placeholder mapped text
            /// </summary>
            [JsonIgnore]
            public string PlaceholderText { get; set; }
        }

        /// <summary>
        ///     Base hologram implementation
        /// </summary>
        private class Hologram
        {
            /// <summary>
            ///     Do we have placeholders?
            /// </summary>
            private bool _hasPlaceholders;

            /// <summary>
            ///     Have we checked?
            /// </summary>
            private bool _hasChecked;

            /// <summary>
            ///     Our next update time
            /// </summary>
            private DateTime _nextUpdate = DateTime.UtcNow;

            /// <summary>
            ///     This hologram's ID
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            ///     A list of lines this hologram holds
            /// </summary>
            public IList<Line> Lines { get; set; }

            /// <summary>
            ///     The position of this hologram
            /// </summary>
            public Vector3 Position { get; set; }

            /// <summary>
            ///     The spacing between lines in metres
            /// </summary>
            public float LineSpacing { get; set; } = Instance._config.DefaultLineSpacing;

            /// <summary>
            ///     The time between updates in seconds
            /// </summary>
            public float UpdateInterval { get; set; } = -1f;

            /// <summary>
            ///     The view radius in metres
            /// </summary>
            public float ViewRadius { get; set; } = 15f;

            /// <summary>
            ///     The permission to view this hologram
            /// </summary>
            public string Permission { get; set; }

            /// <summary>
            ///     Add a new line
            /// </summary>
            /// <param name="text">Line text</param>
            public void AddLine(string text)
            {
                DoesHavePlaceholders(text);
                Lines.Add(new Line {Text = text});
                UpdateForNearby();
            }

            /// <summary>
            ///     Set a line at a certain index
            /// </summary>
            /// <param name="index">Target index</param>
            /// <param name="text">New line</param>
            public void SetLine(int index, string text)
            {
                DoesHavePlaceholders(text);
                Lines[index] = new Line {Text = text};
                UpdateForNearby();
            }

            /// <summary>
            ///     Remove a line at a certain index
            /// </summary>
            /// <param name="index">Target index</param>
            public void RemoveLine(int index)
            {
                Lines.RemoveAt(index);
                UpdateForNearby();
                CheckForPlaceholders();
            }

            /// <summary>
            ///     Check for placeholders in all lines
            /// </summary>
            private void CheckForPlaceholders()
            {
                if (_hasPlaceholders) return;
                _hasChecked = true;
                foreach (var line in Lines)
                {
                    DoesHavePlaceholders(line.Text);
                    if (_hasPlaceholders) break;
                }
            }

            /// <summary>
            ///     Update if we have placeholders
            /// </summary>
            /// <param name="text">A line from <see cref="Lines" /></param>
            private void DoesHavePlaceholders(string text)
            {
                if (!_hasPlaceholders) _hasPlaceholders = Instance._placeholderRegex.IsMatch(text);
                _hasChecked = true;
            }

            /// <summary>
            ///     Tick this hologram
            /// </summary>
            public void Tick()
            {
                UpdateForNearby();
                if (_nextUpdate > DateTime.UtcNow) return;
                Update();
                _nextUpdate = DateTime.UtcNow.AddSeconds(UpdateInterval);
            }

            /// <summary>
            ///     Update our lines and replace placeholders
            /// </summary>
            private void Update()
            {
                if (!_hasChecked) CheckForPlaceholders();
                if (!_hasPlaceholders) return;
                foreach (var line in Lines)
                {
                    var text = line.Text;
                    var matches = Instance._placeholderRegex.Matches(text);
                    foreach (Match match in matches)
                    {
                        var id = match.Value.Substring(1, match.Value.Length - 2);
                        var value = Instance.GetPlaceholderValue(id);
                        if (string.IsNullOrEmpty(value)) continue;
                        text = Regex.Replace(text, match.Value, value);
                    }

                    line.PlaceholderText = text;
                }
            }

            /// <summary>
            ///     Send updates to nearby players
            /// </summary>
            private void UpdateForNearby()
            {
                var players = new List<BasePlayer>();
                Vis.Entities(Position, ViewRadius, players, Instance.PlayerMask, QueryTriggerInteraction.Ignore);
                var hasPermission = !string.IsNullOrEmpty(Permission);
                foreach (var player in players.Where(player =>
                    !player.IsNpc && player.IPlayer != null &&
                    (!hasPermission || player.IPlayer.HasPermission($"holograms.{Permission}"))))
                {
                    if (Instance._config.ObstructionMask != null && Instance._config.ObstructionMask.Length > 0 &&
                        Physics.Raycast(player.eyes.HeadRay(),
                            player.Distance(Position), Instance.ObstructionMask, QueryTriggerInteraction.Ignore))
                        continue;
                    var position = new Vector3(Position.x, Position.y, Position.z);
                    foreach (var line in Lines)
                    {
                        var wasAdmin = player.IsAdmin;
                        if (!wasAdmin)
                        {
                            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                            player.SendNetworkUpdateImmediate();
                        }

                        player.SendConsoleCommand("ddraw.text", Instance._config.TickInterval, Color.white, position,
                            _hasPlaceholders ? line.PlaceholderText : line.Text);
                        if (!wasAdmin)
                        {
                            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                            player.SendNetworkUpdateImmediate();
                        }

                        position -= new Vector3(0, LineSpacing, 0);
                    }
                }
            }
        }

        private struct ConsoleExecutor : IExecutor
        {
            /// <summary>
            ///     Our arg
            /// </summary>
            private readonly ConsoleSystem.Arg _arg;

            public ConsoleExecutor(ConsoleSystem.Arg arg)
            {
                _arg = arg;
            }

            /// <inheritdoc />
            public void Reply(string message)
            {
                _arg.ReplyWith(message);
            }

            /// <inheritdoc />
            public bool HasPermission(string permission)
            {
                return _arg.Player()?.IPlayer.HasPermission(permission) ?? true;
            }

            /// <inheritdoc />
            public string GetId()
            {
                return _arg.Player()?.UserIDString;
            }

            /// <inheritdoc />
            public Vector3 GetPosition()
            {
                return _arg.Player()?.transform.position ?? default(Vector3);
            }

            /// <inheritdoc />
            public bool Teleport(Vector3 position)
            {
                var player = _arg.Player();
                if (player == null)
                {
                    Reply("You must be a player to do this.");
                    return false;
                }

                player.Teleport(position);
                return true;
            }
        }

        /// <inheritdoc />
        private struct PlayerExecutor : IExecutor
        {
            /// <summary>
            ///     Our player
            /// </summary>
            private readonly BasePlayer _player;

            public PlayerExecutor(BasePlayer player)
            {
                _player = player;
            }

            /// <inheritdoc />
            public void Reply(string message)
            {
                _player.ChatMessage(message);
            }

            /// <inheritdoc />
            public bool HasPermission(string permission)
            {
                return _player.IPlayer.HasPermission(permission);
            }

            /// <inheritdoc />
            public string GetId()
            {
                return _player.UserIDString;
            }

            /// <inheritdoc />
            public Vector3 GetPosition()
            {
                return _player.transform.position;
            }

            /// <inheritdoc />
            public bool Teleport(Vector3 position)
            {
                _player.Teleport(position);
                return true;
            }
        }

        /// <summary>
        ///     Interface executor
        /// </summary>
        private interface IExecutor
        {
            /// <summary>
            ///     Reply to this executor
            /// </summary>
            /// <param name="message">Target message</param>
            void Reply(string message);

            /// <summary>
            ///     Does this executor have a permission?
            /// </summary>
            /// <param name="permission">Target permission</param>
            /// <returns><see langword="true" /> if this executor has the permission. <see langword="false" /> otherwise. </returns>
            bool HasPermission(string permission);

            /// <summary>
            ///     Get this executor's ID
            /// </summary>
            /// <returns>A <see cref="string" /> ID</returns>
            string GetId();

            /// <summary>
            ///     Get this executor's position
            /// </summary>
            /// <returns>A <see cref="Vector3" /></returns>
            Vector3 GetPosition();

            /// <summary>
            ///     Teleport this executor to a position
            /// </summary>
            /// <param name="position">Target position</param>
            /// <returns><see langword="true" /> if successful. <see langword="false" /> otherwise.</returns>
            bool Teleport(Vector3 position);
        }

        #endregion

        #region Configuration, Localization & Data

        private Data _data;
        private ConfigFile _config;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"List", "Active Holograms:\n- {0}\nYou can teleport to them with /hologram teleport <hologram>"},
                {"Teleported", "You have teleported to the hologram \"{0}\"."},
                {"HologramCreated", "You have created a hologram with that name at your position."},
                {"InvalidHologram", "A hologram with that name couldn't be found."},
                {"HologramExists", "A hologram with that name already exists."},
                {"InvalidNumber", "That is not a number."},
                {"InvalidIndex", "This hologram does not have that many/little lines."},
                {"RemovedLine", "You have removed the line #{0} from the hologram \"{1}\""},
                {"AddLine", "You have added that line to the hologram \"{0}\""},
                {"SetLine", "You have re-set the line #{0} from the hologram \"{1}\""},
                {"Renamed", "You have renamed the hologram \"{0}\" to \"{1}\""},
                {"LineSpacing", "You have set the line spacing of the hologram \"{0}\" to {1}m"},
                {"HologramDeleted", "You have deleted that hologram."},
                {"ViewRadius", "You have set the view radius of the hologram \"{0}\" to {1}m"},
                {"NoPermission", "You do not have permission to execute this command."},
                {"Permission", "You have set the viewing permission of the hologram \"{0}\" to \"{1}\""},
                {"UpdateInterval", "You have set the update interval of the hologram \"{0}\" to {1}s"},
                {"TeleportedHere", "You have teleported the hologram \"{0}\" to your position."},
                {
                    "Help", "<color=#4287f5>Holograms Help</color>\n" +
                            "/{0} create <name> - Create a hologram with that name\n" +
                            "/{0} delete <name> - Delete a hologram with that name\n" +
                            "/{0} list - List all of the active holograms\n" +
                            "/{0} teleport <hologram> - Teleport to that hologram\n" +
                            "/{0} tphere <hologram> - Teleport to that hologram to your location\n" +
                            "/{0} rename <hologram> <new name> - Rename a hologram\n\n" +
                            "/{0} spacing <hologram> <spacing> - Change the spacing between lines (metres) of a hologram\n" +
                            "/{0} update <hologram> <interval> - Change the placeholder update delay (seconds) of a hologram\n" +
                            "/{0} radius <hologram> <radius> - Change the view radius (metres) of a hologram\n" +
                            "/{0} permission <hologram> <permission> - Change the viewing permission (null to disable)\n\n" +
                            "/{0} addline <hologram> <text> - Add a line to the hologram\n" +
                            "/{0} setline <hologram> <line> <text> - Set an existing line of a hologram\n" +
                            "/{0} remline <hologram> <line> - Remove a line from a hologram"
                }
            }, this);
        }

        public class ConfigFile
        {
            /// <summary>
            ///     The default line spacing when creating a hologram
            /// </summary>
            [JsonProperty("Default Line Spacing")]
            public float DefaultLineSpacing { get; set; }

            /// <summary>
            ///     The time between hologram ticks (seconds)
            /// </summary>
            [JsonProperty("Hologram Tick Interval (seconds)")]
            public float TickInterval { get; set; }

            /// <summary>
            ///     The time between placeholder ticks (seconds)
            /// </summary>
            [JsonProperty("Placeholder Tick Interval (seconds)")]
            public float PlaceholderInterval { get; set; }

            /// <summary>
            ///     The obstruction layer masks
            /// </summary>
            [JsonProperty("Obstruction Mask")]
            public string[] ObstructionMask { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    ObstructionMask = new[] {"Construction", "Deployed", "World", "Terrain"},
                    DefaultLineSpacing = 0.1f,
                    TickInterval = 1f,
                    PlaceholderInterval = 0.5f
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null) LoadDefaultConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        private class Data
        {
            public IDictionary<string, Hologram> Holograms { get; } = new Dictionary<string, Hologram>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
        }

        #endregion
    }
}