using ConVar;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("Auto Demo Record Lite", "Pho3niX90", "1.0.82")]
    [Description("Automatic recording based on conditions.")]
    internal class AutoDemoRecordLite : RustPlugin
    {
        private List<ARReport> _reports = new List<ARReport>();
        private Dictionary<ulong, Timer> _timers = new Dictionary<ulong, Timer>();
        private ARConfig config;
        int lastSavedCount = 0;

        [PluginReference]
        Plugin DiscordApi, DiscordMessages;

        private void Loaded() {
            LoadData();
        }

        private void Unload() {
            SaveData();

            _reports.Clear();
            _reports = null;

            foreach (var player in BasePlayer.activePlayerList) {
                if (player.Connection.IsRecording)
                    player.Connection.StopRecording();
            }

            foreach (Timer timer in _timers.Values) {
                timer.Destroy();
            }

            _timers.Clear();
            _timers = null;
        }

        protected override void LoadDefaultMessages() {
            lang.RegisterMessages(new Dictionary<string, string> {
                ["Recording Started"] = "Recording started for player {0}, eta is {1} mins",
                ["Recording Ended"] = "Recording finished for player {0}, player was recorded for {1} mins",
            }, this);
        }

        string GetMsg(string key) => lang.GetMessage(key, this);

        void OnPlayerReported(BasePlayer reporter, string targetName, string targetId, string subject, string message, string type) {
            ulong targetIdLong = 0;
            if (ulong.TryParse(targetId, out targetIdLong)) {
                var report = new ARReport(reporter.UserIDString, reporter.displayName, targetId, targetName, subject, message, type);
                _reports.Add(report);
                ProcessF7(targetIdLong, report);
            }
        }

        void OnPlayerCommand(BasePlayer player, string command, string[] args) {
            if (!command.ToLower().Equals("report") || args.Length < 2) return;

            var target = BasePlayer.Find(args[0]);
            if (target == null) return;

            List<string> reason = args.Skip(1).ToList();

            var report = new ARReport(player.UserIDString, player.displayName, target.UserIDString, target.displayName, "Report", string.Join(" ", reason), "Report");
            _reports.Add(report);
            ProcessF7(target.userID, report);
        }

        void ReportCommand(IPlayer reporter, IPlayer target, string reason) {
            var report = new ARReport(reporter.Id, reporter.Name, target.Id, target.Name, "DM Report", reason, "DM Report");
            _reports.Add(report);
            ProcessF7(ulong.Parse(target.Id), report);
        }

        private void OnDestroy() {
            foreach (Timer timer in _timers.Values) {
                timer.Destroy();
            }
            _timers.Clear();
            _reports.Clear();
        }

        void ProcessF7(ulong targetId, ARReport report = null) {
            BasePlayer accused = BasePlayer.FindByID(targetId);
            if (accused == null) return;
            if (accused.IsConnected) {
                // record player only if he has reaced the amount in the config. And only when there is no recording active. 
                if (CheckReports(accused) >= config.AR_Report) {
                    if (!_timers.ContainsKey(accused.userID)) {
                        StartRecording(accused, report);
                    }
                }
            }
        }

        void StartRecording(BasePlayer player, ARReport report = null) {
            var msg = string.Format(GetMsg("Recording Started"), player.UserIDString, config.AR_Report_Length);
            Puts(msg);

            if (config.AR_Discord_Notify_RecordStart) NotifyDiscord(player, msg, report, true);

            player.StartDemoRecording();
            if (config.AR_Report_Length > 0) {
                _timers[player.userID] = timer.Once(config.AR_Report_Length * 60, () => StopRecording(player, report));
            }
        }

        void StopRecording(BasePlayer player, ARReport report) {
            var msg = string.Format(GetMsg("Recording Ended"), player.UserIDString, config.AR_Report_Length);
            Puts(msg);
            if (config.AR_Discord_Notify_RecordStop) NotifyDiscord(player, msg, report, false);
            if (_timers.ContainsKey(player.userID)) {
                player.StopDemoRecording();
                if (config.AR_Clear_Counter) {
                    _reports.RemoveAll(x => x.targetId == player.UserIDString);
                }
                _timers.Remove(player.userID);
            }
        }

        int CheckReports(BasePlayer player) => config.AR_Report_Seconds > 0
                ? this._reports.Count(x => secondsAgo(x.created) <= config.AR_Report_Seconds && x.targetId == player.UserIDString)
                : this._reports.Count(x => x.targetId == player.UserIDString);


        int secondsAgo(DateTime timeFrom) => (int)Math.Round((DateTime.UtcNow - timeFrom).TotalSeconds);


        void NotifyDiscord(BasePlayer player, string msg, ARReport report, bool isStart) {
            if (!config.AR_Discord_Webhook.IsNullOrEmpty() && !config.Equals("https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks")
                && (config.AR_Discord_Notify_RecordStart || config.AR_Discord_Notify_RecordStop)) {
                List<EmbedFieldList> fields = new List<EmbedFieldList>();

                fields.Add(new EmbedFieldList() {
                    name = covalence.Server.Name,
                    value = $"[steam://connect/{covalence.Server.Address}:{covalence.Server.Port}](steam://connect/{covalence.Server.Address}:{covalence.Server.Port})",
                    inline = true
                });

                if (report != null &&
                    ((isStart && config.AR_Discord_Notify_RecordStartMsg) || (!isStart && config.AR_Discord_Notify_RecordStopMsg))) {
                    fields.Add(new EmbedFieldList() {
                        name = "Reporter",
                        value = $"{report.reporterName} ({report.reporterId})",
                        inline = false
                    });
                    fields.Add(new EmbedFieldList() {
                        name = "Report Subject",
                        value = $"{report.type}: {report.subject}",
                        inline = false
                    });
                    fields.Add(new EmbedFieldList() {
                        name = "Report Message",
                        value = report.message,
                        inline = false
                    });
                }

                fields.Add(new EmbedFieldList() {
                    name = player.displayName,
                    value = msg,
                    inline = false
                });

                string json = JsonConvert.SerializeObject(fields.ToArray());

                if (DiscordApi != null && DiscordApi.IsLoaded) {
                    DiscordApi?.Call("API_SendEmbeddedMessage", config.AR_Discord_Webhook, "Auto Demo Recorder", config.AR_Discord_Color, json);
                } else if (DiscordMessages != null && DiscordMessages.IsLoaded) {
                    DiscordMessages?.Call("API_SendFancyMessage", config.AR_Discord_Webhook, "Auto Demo Recorder", config.AR_Discord_Color, json);
                } else {
                    Puts("No discord API plugin loaded, will not publish to hook!");
                }
            }
        }

        #region Configuration
        private class ARConfig
        {
            // Config default vars
            public int AR_Report = 1;
            public int AR_Report_Length = 1;
            public bool AR_Clear_Counter = false;
            public string AR_Discord_Webhook = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
            public int AR_Discord_Color = 39423;
            public bool AR_Discord_Notify_RecordStart = false;
            public bool AR_Discord_Notify_RecordStop = false;
            public int AR_Report_Seconds = 0;
            public bool AR_Discord_Notify_RecordStartMsg = true;
            public bool AR_Discord_Notify_RecordStopMsg = false;
            public bool AR_Save_Reports = true;

            // Plugin reference
            private AutoDemoRecordLite plugin;
            public ARConfig(AutoDemoRecordLite plugin) {
                this.plugin = plugin;
                /**
                 * Load all saved config values
                 * */
                GetConfig(ref AR_Report, "Auto record after X reports");
                GetConfig(ref AR_Report_Length, "Auto record for X minutes");
                GetConfig(ref AR_Clear_Counter, "Clear report counter after recording?");
                GetConfig(ref AR_Discord_Webhook, "Discord Webhook");
                GetConfig(ref AR_Discord_Color, "Discord MSG Color");
                GetConfig(ref AR_Discord_Notify_RecordStart, "Discord: Notify if recording is started");
                GetConfig(ref AR_Discord_Notify_RecordStartMsg, "Discord: Include report with start message?");
                GetConfig(ref AR_Discord_Notify_RecordStop, "Discord: Notify if recording is stopped");
                GetConfig(ref AR_Discord_Notify_RecordStopMsg, "Discord: Include report with end message?");
                GetConfig(ref AR_Report_Seconds, "Only record when reports within X seconds");
                GetConfig(ref AR_Save_Reports, "Save/Load reports to datafile on reload");

                plugin.SaveConfig();
            }

            private void GetConfig<T>(ref T variable, params string[] path) {
                if (path.Length == 0) return;
                if (plugin.Config.Get(path) == null) {
                    SetConfig(ref variable, path);
                    plugin.PrintWarning($"Added new field to config: {string.Join("/", path)}");
                }
                variable = (T)Convert.ChangeType(plugin.Config.Get(path), typeof(T));
            }

            private void SetConfig<T>(ref T variable, params string[] path) => plugin.Config.Set(path.Concat(new object[] { variable }).ToArray());

        }
        protected override void LoadConfig() {
            base.LoadConfig();
            config = new ARConfig(this);
        }

        void LoadData() {
            if (!config.AR_Save_Reports) return;
            try {
                _reports = Interface.Oxide.DataFileSystem.ReadObject<List<ARReport>>(this.Name);
                lastSavedCount = _reports.Count();
            } catch (Exception e) {
                Puts(e.Message);
            }
        }

        void SaveData() {
            int recordsDiff = _reports.Count() - lastSavedCount;
            if (!config.AR_Save_Reports || recordsDiff == 0) return;
            try {
                Interface.Oxide.DataFileSystem.WriteObject(this.Name, _reports, true);
            } catch (Exception e) {
                Puts(e.Message);
            }
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file.");
        #endregion

        #region Classes 
        public class EmbedFieldList
        {
            public string name { get; set; }
            public string value { get; set; }
            public bool inline { get; set; }
        }

        public class ARReport
        {
            public string reporterName;
            public string reporterId;
            public string targetName;
            public string targetId;
            public string subject;
            public string message;
            public string type;
            public DateTime created;
            public ARReport() { }
            public ARReport(string reporterId, string reporterName, string targetId, string targetName, string subject, string message, string type) {
                this.reporterId = reporterId;
                this.reporterName = reporterName;
                this.targetId = targetId;
                this.targetName = targetName;
                this.subject = subject;
                this.message = message;
                this.type = type;
                this.created = DateTime.UtcNow;
            }
            public ARReport(string targetId, string targetName) {
                this.targetName = targetName;
                this.targetId = targetId;
            }
        }
        #endregion
    }
}
