
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{

    [Info("Phones Plus", "mr01sam", "1.0.2")]
    [Description("Adds notifications, messages, caller ID and more features to phones.")]
    public class PhonesPlus : CovalencePlugin
    {
        private static PhonesPlus PLUGIN;

        private const string PermissionUse = "phonesplus.use";
        private const string PermissionAdmin = "phonesplus.admin";

        /* For internal use */
        private const string PermissionTemp = "phonesplus.temp";

        [PluginReference]
        Plugin ImageLibrary;

        PhoneManager phoneManager;


        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI title history"] = "HISTORY",
                ["UI title messages"] = "MESSAGES",
                ["UI title clear"] = "CLEAR",
                ["UI title track"] = "TRACK PHONE",
                ["UI title untrack"] = "UNTRACK PHONE",
                ["UI title prompt"] = "No answer? Leave a message:",
                ["HUD incoming"] = "Incoming call from {0}",
                ["History missed"] = "Missed call from {0}",
                ["History outgoing"] = "Outgoing call to {0}",
                ["History answered"] = "Answered call from {0}",
                ["Time minutes"] = "{0} minutes ago",
                ["Time hours"] = "{0} hours ago",
                ["Time days"] = "{0} days ago",
                ["Usage"] = "Usage: {0}",
                ["Notifications on"] = "You will receive incoming call notifications from tracked phones",
                ["Notifications off"] = "You will no longer receive incoming call notifications from tracked phones",
                ["Indicators on"] = "Indicators will be displayed on the HUD for tracked phones",
                ["Indicators off"] = "Indicators will no longer be displayed on the HUD for tracked phones",
                ["No permission"] = "You do not have permission to use that command",
                ["Tracking on"] = "Now tracking phone {0}",
                ["Tracking off"] = "Stopped tracking phone {0}",
                ["Reset ui"] = "Reset UI",
                ["Reset tracked"] = "Reset tracked phone records",
                ["No messages"] = "No recent messages",
                ["No history"] = "No recent calls",
                ["Clear history"] = "Cleared history for {0}",
                ["Clear trackers"] = "Cleared trackers for {0}",
                ["Clear messages"] = "Cleared messages for {0}"
            }, this);
        }
        #endregion

        #region Configuration

        private Configuration config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Enable incoming call notifications (true/false)")]
            public bool EnableIncomingCallNotifications { get; set; } = true;

            [JsonProperty(PropertyName = "Hourly message limit")]
            public int HourlyMessageLimit { get; set; } = 50;

            [JsonProperty(PropertyName = "HUD Notification")]
            public NotificationHUD HudNotification = new NotificationHUD
            {
                AnchorMin = "0.4 0.87",
                AnchorMax = "0.6 0.95",
                BackgroundColor = "0.4 0.4 0.4"
            };

            [JsonProperty(PropertyName = "HUD Call Indicator")]
            public IndicatorHUD HudCallIndicator = new IndicatorHUD
            {
                AnchorMin = "0.655 0.90",
                AnchorMax = "0.675 0.95",
                ImageColor = "0.5 1 0.5 0.5"
            };

            [JsonProperty(PropertyName = "HUD Message Indicator")]
            public IndicatorHUD HudMessageIndicator = new IndicatorHUD
            {
                AnchorMin = "0.68 0.90",
                AnchorMax = "0.70 0.95",
                ImageColor = "0.5 0.5 1 0.5"
            };

            public class NotificationHUD
            {
                [JsonProperty(PropertyName = "Anchor min")]
                public string AnchorMin;
                [JsonProperty(PropertyName = "Anchor max")]
                public string AnchorMax;
                [JsonProperty(PropertyName = "Background color")]
                public string BackgroundColor;
            }

            public class IndicatorHUD
            {
                [JsonProperty(PropertyName = "Anchor min")]
                public string AnchorMin;
                [JsonProperty(PropertyName = "Anchor max")]
                public string AnchorMax;
                [JsonProperty(PropertyName = "Image color")]
                public string ImageColor;
            }

            public class HistoryUI
            {
                [JsonProperty(PropertyName = "Entry background color")]
                public string EntryBackgroundColor = "0.9 0.9 0.9 0.9";
                [JsonProperty(PropertyName = "Font color")]
                public string FontColor = "1 1 1 1";
            }

            public class MessagesUI
            {
                [JsonProperty(PropertyName = "Entry background color")]
                public string EntryBackgroundColor = "0.9 0.9 0.9 0.9";
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig() => config = new Configuration();


        #endregion

        #region Commands

        private Dictionary<string, ChatCmd> commands = new Dictionary<string, ChatCmd>();

        void InitCommands()
        {
            commands.Add("help", new ChatCmd
            {
                perms = new List<string>() { PermissionUse },
                function = (IPlayer player, string command, string[] args) => { cmd_phone_help(player, command, args); }
            });
            commands.Add("set", new ChatCmd
            {
                usages = new List<string>() { "<notifications/indicators> <true/false>" },
                perms = new List<string>() { PermissionUse },
                function = (IPlayer player, string command, string[] args) => { cmd_phone_set(player, command, args); }
            });
            commands.Add("reset", new ChatCmd
            {
                usages = new List<string>() { "<ui/tracked>" },
                perms = new List<string>() { PermissionUse },
                function = (IPlayer player, string command, string[] args) => { cmd_phone_reset(player, command, args); }
            });
            commands.Add("clear", new ChatCmd
            {
                usages = new List<string>() { "<trackers/messages/history> <phonenumber>" },
                perms = new List<string>() { PermissionAdmin },
                function = (IPlayer player, string command, string[] args) => { cmd_phone_clear(player, command, args); }
            });
        }

        class ChatCmd
        {
            public string prefix = "";
            public List<string> usages = new List<string>();
            public List<string> perms = new List<string>();
            public Action<IPlayer, string, string[]> function;

            public bool HasPerms(string id, PhonesPlus plugin)
            {
                foreach (string perm in perms)
                    if (plugin.permission.UserHasPermission(id, perm))
                        return true;
                return false;
            }

            public string Usage(string prefix)
            {
                string message = "";
                if (usages.Count > 0)
                {
                    string last = usages.Last();
                    foreach (string usg in usages)
                    {
                        message += string.Format("{0} {1}", prefix, usg);
                        if (!usg.Equals(last))
                            message += ", ";
                    }
                }
                else
                {
                    message = prefix;
                }

                return message;
            }
        }

        [Command("phone")]
        private void cmd_phone(IPlayer player, string command, string[] args)
        {
            if (args.Length >= 1)
            {
                string prefix = args[0].ToLower();
                if (commands.ContainsKey(prefix))
                {
                    args = args.Skip(1).ToArray();

                    ChatCmd cmd = commands[prefix];
                    if (cmd.HasPerms(player.Id, this))
                    {
                        try
                        {
                            commands[prefix].function(player, command, args);
                        }
                        catch (Exception)
                        {
                            player.Reply(string.Format(lang.GetMessage("Usage", this, player.Id), "phone " + cmd.Usage(prefix)));
                        }
                        return;
                    }
                    else
                    {
                        /* No perms */
                        player.Reply(lang.GetMessage("No permission", this, player.Id));
                    }
                }
            }
            else
            {
                if (permission.UserHasPermission(player.Id, PermissionUse))
                {
                    cmd_phone_help(player, command, args);
                }
                else
                {
                    player.Reply(lang.GetMessage("No permission", this, player.Id));
                }
            }
        }

        #region User Commands

        [Command("phone.set"), Permission(PermissionUse)]
        private void cmd_phone_set(IPlayer player, string command, string[] args)
        {
            bool value = bool.Parse(args[1]);
            switch (args[0].ToLower())
            {
                case "notifications":
                    phoneManager.SetPreference(ulong.Parse(player.Id), "notifications", value);
                    if (value)
                        player.Reply(lang.GetMessage("Notifications on", this, player.Id));
                    else
                        player.Reply(lang.GetMessage("Notifications off", this, player.Id));
                    break;
                case "indicators":
                    phoneManager.SetPreference(ulong.Parse(player.Id), "indicators", value);
                    if (value)
                        player.Reply(lang.GetMessage("Indicators on", this, player.Id));
                    else
                        player.Reply(lang.GetMessage("Indicators off", this, player.Id));
                    ShowUnreadIndicator(BasePlayer.FindByID(ulong.Parse(player.Id)));
                    ShowCallIndicator(BasePlayer.FindByID(ulong.Parse(player.Id)));
                    break;
                default:
                    break;
            }
        }

        [Command("phone.reset"), Permission(PermissionUse)]
        private void cmd_phone_reset(IPlayer player, string command, string[] args)
        {
            BasePlayer entity = BasePlayer.FindByID(ulong.Parse(player.Id));
            switch (args[0].ToLower())
            {
                case "ui":
                    HideAllUI(entity);
                    ShowCallIndicator(entity);
                    ShowUnreadIndicator(entity);
                    player.Reply(lang.GetMessage("Reset ui", this, player.Id));
                    break;
                case "tracked":
                    foreach (uint id in phoneManager.GetTrackedPhones(entity.userID))
                    {
                        RegisteredPhone phone = phoneManager.FindPhoneByID(id);
                        if (phone != null)
                            phoneManager.UntrackPhone(entity, phone.GetNumber());
                    }

                    phoneManager.UpdatePhoneMessageCount(entity.userID, 0);
                    phoneManager.UpdatePhoneRecordsCount(entity.userID, 0);
                    player.Reply(lang.GetMessage("Reset tracked", this, player.Id));
                    ShowUnreadIndicator(entity);
                    ShowCallIndicator(entity);
                    break;
                default:
                    break;
            }
        }

        [Command("phone.help"), Permission(PermissionUse)]
        private void cmd_phone_help(IPlayer player, string command, string[] args)
        {
            string size = "16";
            string cmdList = FormatTxt("Phones", "ca2a13", size) + FormatTxt("Plus", "ca2a13", size) + "\n";
            foreach (string prefix in commands.Keys)
            {
                ChatCmd cmd = commands[prefix];
                if (cmd.HasPerms(player.Id, this))
                    cmdList += "phone " + cmd.Usage(prefix) + "\n";
            }
            player.Reply(cmdList);
        }

        #endregion

        #region UI Commands
        [Command("phone.message"), Permission(PermissionTemp)]
        private void cmd_phone_message(IPlayer player, string command, string[] args)
        {
            int senderNumber = int.Parse(args[0]);
            int targetNumber = int.Parse(args[1]);
            string text = "";
            if (args.Length > 2 && !phoneManager.ExceededMessageLimit(ulong.Parse(player.Id)))
            {
                for (int i = 2; i < args.Length; i++)
                {
                    if (i == 2)
                        args[i] = args[i].TitleCase();
                    text += args[i];
                    if (i < args.Length - 1)
                        text += " ";
                }
                CuiHelper.DestroyUi(BasePlayer.FindByID(ulong.Parse(player.Id)), "phonesInputOverlay");
                if (text.Length > 0)
                {
                    phoneManager.IncrementMessageUse(ulong.Parse(player.Id));
                    phoneManager.LeaveMessage(senderNumber, targetNumber, text);
                }
            }
            CuiHelper.DestroyUi(BasePlayer.FindByID(ulong.Parse(player.Id)), "phonesInputOverlay");
        }

        [Command("phone.track"), Permission(PermissionTemp)]
        private void cmd_phone_track(IPlayer player, string command, string[] args)
        {
            int phoneNumber = int.Parse(args[0]);

            RegisteredPhone phone = phoneManager.FindPhoneByNumber(phoneNumber);
            if (phone != null)
            {
                BasePlayer target = BasePlayer.FindByID(ulong.Parse(player.Id));
                if (phoneManager.TrackPhone(target, phoneNumber))
                    player.Reply(string.Format(lang.GetMessage("Tracking on", this, player.Id), phoneNumber));
                ShowPhoneUI(target, phone);
            }
        }

        [Command("phone.untrack"), Permission(PermissionTemp)]
        private void cmd_phone_untrack(IPlayer player, string command, string[] args)
        {
            int phoneNumber = int.Parse(args[0]);

            RegisteredPhone phone = phoneManager.FindPhoneByNumber(phoneNumber);
            if (phone != null)
            {
                BasePlayer target = BasePlayer.FindByID(ulong.Parse(player.Id));
                if (phoneManager.UntrackPhone(target, phoneNumber))
                    player.Reply(string.Format(lang.GetMessage("Tracking off", this, player.Id), phoneNumber));
                ShowPhoneUI(target, phone);
            }
        }

        [Command("phone.clear"), Permission(PermissionTemp)]
        private void cmd_phone_clear(IPlayer player, string command, string[] args)
        {
            int number = int.Parse(args[1]);
            RegisteredPhone phone = phoneManager.FindPhoneByNumber(number);
            BasePlayer entity = BasePlayer.FindByID(ulong.Parse(player.Id));
            switch (args[0].ToLower())
            {
                case "messages":
                    phoneManager.ClearMessages(phone);
                    if (!phoneManager.IsStateEqualTo(entity, 0))
                        ShowPhoneUI(entity, phone);
                    else
                        player.Reply(string.Format(lang.GetMessage("Clear messages", this, player.Id), phone.GetIdentity()));
                    break;
                case "history":
                    phoneManager.ClearHistory(phone);
                    if (!phoneManager.IsStateEqualTo(entity, 0))
                        ShowPhoneUI(entity, phone);
                    else
                        player.Reply(string.Format(lang.GetMessage("Clear history", this, player.Id), phone.GetIdentity()));
                    break;
                case "trackers":
                    phoneManager.ClearTrackers(phone);
                    if (!phoneManager.IsStateEqualTo(entity, 0))
                        ShowPhoneUI(entity, phone);
                    else
                        player.Reply(string.Format(lang.GetMessage("Clear trackers", this, player.Id), phone.GetIdentity()));
                    break;
                default:
                    break;
            }
        }

        [Command("phone.uiclose"), Permission(PermissionTemp)]
        private void cmd_uiclose(IPlayer player, string command, string[] args)
        {
            HideAllUI(BasePlayer.FindByID(ulong.Parse(player.Id)));
            string usage = string.Format(lang.GetMessage("Usage", this, player.Id), command);
            player.Reply(usage);
        }

        #endregion

        #region Admin Commands
        [Command("phone.history"), Permission(PermissionAdmin)]
        private void cmd_phone_history(IPlayer player, string command, string[] args)
        {
            try
            {
                int phoneNumber = int.Parse(args[0]);
                RegisteredPhone phone = phoneManager.FindPhoneByNumber(phoneNumber);
                if (phone != null)
                {
                    PhoneRecordList records = phoneManager.GetPhoneRecords(phoneNumber);
                    string recordListStr = "<" + phoneNumber + ">\n";
                    foreach (PhoneRecord record in records.RecentRecords(10))
                    {
                        recordListStr += string.Format("({0}, {1}, {2})", record.GetTimeElapsed(), record.status, record.Number(phoneManager)) + "\n";
                    }
                    player.Reply(recordListStr);
                }
            }
            catch (Exception)
            {
                string usage = string.Format(lang.GetMessage("Usage", this, player.Id), command) + " <phone_number>";
                player.Reply(usage);
            }
        }
        #endregion

        #endregion

        #region Custom Hooks
        void UsePhone(BasePlayer player, RegisteredPhone phone)
        {
            if (Interface.CallHook("OnPhoneUse", player, phone.phoneController) != null) return;
            if (player.IsValid() && phone != null && permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                if (phone.phoneController.activeCallTo == null)
                    ShowPhoneUI(player, phone);
                HideNotificationHUD(player);
                int changedMessages = phoneManager.GetPhoneMessages(phone.GetNumber()).MarkAllAsRead();
                int changedRecords = phoneManager.GetPhoneRecords(phone.GetNumber()).MarkAllAsRead();
                foreach (ulong userId in phoneManager.GetPhoneTrackers(phone))
                {
                    BasePlayer tracker = BasePlayer.FindByID(userId);
                    if (tracker.IsValid())
                    {
                        phoneManager.UpdatePhoneMessageCount(tracker.userID, phoneManager.GetPhoneMessageCount(tracker.userID) - changedMessages);
                        phoneManager.UpdatePhoneRecordsCount(tracker.userID, phoneManager.GetPhoneRecordsCount(tracker.userID) - changedRecords);
                        ShowUnreadIndicator(tracker);
                        ShowCallIndicator(tracker);
                    }
                }
            }
        }

        void UsePhoneEnd(BasePlayer player)
        {
            if (Interface.CallHook("OnPhoneUseEnd", player) != null) return;
            HideAllUI(player);
        }

        void ActiveCall(BasePlayer player, RegisteredPhone phone)
        {
            if (Interface.CallHook("OnActiveCall", player, phone.phoneController) != null) return;
            HideAllUI(player);
        }

        void ActiveCallEnd(BasePlayer player, RegisteredPhone phone)
        {
            if (Interface.CallHook("OnActiveCallEnd", player, phone.phoneController) != null) return;
            ShowPhoneUI(player, phone);
        }
        #endregion

        #region Hooks

        void Init()
        {
            #region Unsubscribe
            Unsubscribe(nameof(OnPhoneAnswered));
            Unsubscribe(nameof(OnPhoneDial));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(CanPickupEntity));
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnUserPermissionGranted));
            Unsubscribe(nameof(OnPhoneDialFail));
            Unsubscribe(nameof(OnServerSave));
            #endregion

            InitCommands();
        }

        void OnServerInitialized()
        {
            PLUGIN = this;
            try
            {
                ImageLibrary.Call("isLoaded", null);
            }
            catch (Exception)
            {
                PrintWarning($"The required dependency ImageLibrary is not installed, unloading {Name}.");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            #region Subscribe
            Subscribe(nameof(OnPhoneAnswered));
            Subscribe(nameof(OnPhoneDial));
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnEntityDeath));
            Subscribe(nameof(CanPickupEntity));
            Subscribe(nameof(OnPlayerConnected));
            Subscribe(nameof(OnUserPermissionGranted));
            Subscribe(nameof(OnPhoneDialFail));
            Subscribe(nameof(OnServerSave));
            #endregion

            phoneManager = new PhoneManager();

            LoadImages();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                HideAllUI(player);
                HideNotificationHUD(player);
                ShowUnreadIndicator(player);
                ShowCallIndicator(player);
                CheckIfUsingPhone(player);
            }
            ResetMessages();
        }

        void OnPhoneAnswered(PhoneController receiverPhone, PhoneController callerPhone)
        {
            phoneManager.RegisterPhone(callerPhone);
            phoneManager.RegisterPhone(receiverPhone);
            phoneManager.GetPhoneRecords(receiverPhone.PhoneNumber).RecordAnswered(receiverPhone, callerPhone, phoneManager);
            HideAllUI(receiverPhone.currentPlayer);
            HideAllUI(callerPhone.currentPlayer);
        }

        object OnPhoneDial(PhoneController callerPhone, PhoneController receiverPhone, BasePlayer player)
        {
            HideAllUI(player);
            phoneManager.RegisterPhone(callerPhone);
            phoneManager.RegisterPhone(receiverPhone);
            phoneManager.GetPhoneRecords(callerPhone.PhoneNumber).RecordOutgoing(callerPhone, receiverPhone, phoneManager);
            if (config.EnableIncomingCallNotifications)
                NotifyIncoming(receiverPhone.baseEntity.net.ID, callerPhone.baseEntity.net.ID);
            return null;
        }

        void OnEntitySpawned(Telephone entity)
        {
            phoneManager.RegisterPhone(entity.Controller);
        }

        void OnEntityDeath(Telephone entity, HitInfo info)
        {
            phoneManager.UnregisterPhone(entity);
        }

        bool CanPickupEntity(BasePlayer player, Telephone entity)
        {
            phoneManager.UnregisterPhone(entity);
            return true;
        }

        void OnPlayerConnected(BasePlayer player)
        {
            InitPlayer(player);
        }

        void OnUserPermissionGranted(string id, string permName)
        {
            if (permName == PermissionUse)
                InitPlayer(BasePlayer.FindByID(ulong.Parse(id)));
        }

        object OnPhoneDialFail(PhoneController callerPhone, Telephone.DialFailReason reason, BasePlayer player)
        {
            if (callerPhone == null) return null;
            PhoneController receiverPhone = callerPhone.activeCallTo;
            if (receiverPhone == null) return null;
            phoneManager.RegisterPhone(callerPhone);
            phoneManager.RegisterPhone(receiverPhone);

            if (reason == Telephone.DialFailReason.SelfHangUp || reason == Telephone.DialFailReason.TimedOut)
            {
                phoneManager.GetPhoneRecords(receiverPhone.PhoneNumber).RecordMissed(receiverPhone, callerPhone, phoneManager);
                if (player != null && callerPhone != null)
                {
                    ShowPhoneUI(player, phoneManager.FindPhoneByNumber(callerPhone.PhoneNumber));
                    timer.In(0.05f, () =>
                    {
                        if (receiverPhone != null)
                            receiverPhone.activeCallTo = null;
                        if (!phoneManager.ExceededMessageLimit(player.userID) && !receiverPhone.currentPlayer.IsValid())
                            ShowInputUI(player, callerPhone.PhoneNumber, receiverPhone.PhoneNumber);
                    });
                    

                }
            }

            return null;
        }

        void OnServerSave()
        {
            phoneManager.SavePhoneMessages();
            phoneManager.SavePhoneRecords();
            phoneManager.SavePreferences();
            phoneManager.SaveTrackedPhones();
            Puts("saved data");
        }

        #endregion

        #region Helpers
        private string FormatTxt(string text, string color = "white", string size = "14")
        {
            return $"<size={size}>[#{color}]{text}[/#]</size>";
        }

        private void LoadImages()
        {
            ImageLibrary.Call<bool>("AddImage", "https://i.imgur.com/P4yACUv.png", "PhonesPlusIcon", 0UL);
            ImageLibrary.Call<bool>("AddImage", "https://i.imgur.com/HlSs8oN.png", "CallIcon", 0UL);
            ImageLibrary.Call<bool>("AddImage", "https://i.imgur.com/wbEpjW2.png", "HistoryIcon", 0UL);
            ImageLibrary.Call<bool>("AddImage", "https://i.imgur.com/dpAt1K4.png", "MessageIcon", 0UL);
            ImageLibrary.Call<bool>("AddImage", "https://i.imgur.com/CXcaqw7.png", "CloseIcon", 0UL);
        }

        private void CheckIfUsingPhone(BasePlayer player)
        {
            if (player.IsValid() && player.IsConnected)
            {
                if (player.HasActiveTelephone && phoneManager.IsStateEqualTo(player, 0))
                {
                    RegisteredPhone phone = phoneManager.GetNearbyPhone(player);
                    if (phone != null)
                    {
                        phoneManager.SetState(player, 1);
                        UsePhone(player, phone);
                    }
                }
                else if (player.HasActiveTelephone && phoneManager.IsStateEqualTo(player, 1))
                {
                    RegisteredPhone phone = phoneManager.GetNearbyPhone(player);
                    if (phone != null)
                    {
                        if (phone.phoneController.activeCallTo != null)
                        {
                            phoneManager.SetState(player, 2);
                            ActiveCall(player, phone);
                        }
                    }
                }
                else if (player.HasActiveTelephone && phoneManager.IsStateEqualTo(player, 2))
                {
                    RegisteredPhone phone = phoneManager.GetNearbyPhone(player);
                    if (phone != null)
                    {
                        if (phone.phoneController.activeCallTo == null)
                        {
                            phoneManager.SetState(player, 1);
                            ActiveCallEnd(player, phone);
                        }
                    }
                }
                else if (!player.HasActiveTelephone && !phoneManager.IsStateEqualTo(player, 0))
                {
                    phoneManager.SetState(player, 0);
                    UsePhoneEnd(player);
                }

                timer.In(0.05f, () =>
                {
                    CheckIfUsingPhone(player);
                });

            }
        }

        private void ResetMessages()
        {
            timer.In(3600f, () =>
            {
                phoneManager.ResetHourlyMessageLimits();
                ResetMessages();
            });
        }

        private void InitPlayer(BasePlayer player)
        {
            if (player.IsValid())
            {
                HideAllUI(player);
                CheckIfUsingPhone(player);
                ShowUnreadIndicator(player);
                ShowCallIndicator(player);
            }
        }

        private string ElapsedTimeString(BasePlayer player, long seconds)
        {
            if (seconds < 3600)
                return string.Format(lang.GetMessage("Time minutes", this, player.UserIDString), Math.Max(1, Math.Round((double)seconds / 60)));
            if (seconds < 86400)
                return string.Format(lang.GetMessage("Time hours", this, player.UserIDString), Math.Max(1, Math.Round((double)seconds / 3600)));
            else
                return string.Format(lang.GetMessage("Time hours", this, player.UserIDString), Math.Max(1, Math.Round((double)seconds / 86400)));
        }

        void NotifyIncoming(uint receiverPhoneID, uint callerPhoneID)
        {
            RegisteredPhone receivingPhone = phoneManager.FindPhoneByID(receiverPhoneID);
            RegisteredPhone callingPhone = phoneManager.FindPhoneByID(callerPhoneID);
            List<ulong> trackers = phoneManager.GetPhoneTrackers(receivingPhone);
            foreach (ulong userId in trackers)
            {
                BasePlayer tracker = BasePlayer.FindByID(userId);
                if (tracker.IsValid())
                {
                    if (phoneManager.GetPreference<bool>(tracker.userID, "notifications") && phoneManager.IsStateEqualTo(tracker, 0))
                    {
                        string message = string.Format(lang.GetMessage("HUD incoming", this, tracker.UserIDString), callingPhone.GetIdentity());
                        ShowNotificationHUD(tracker, message, receivingPhone);
                    };
                }
            }
        }
        #endregion

        #region Classes

        class PhoneMessage
        {
            public readonly int senderNumber;
            public readonly int receiverNumber;
            public readonly string text;
            public bool unread;
            public long time;

            public PhoneMessage(int senderNumber, int receiverNumber, string text, long time = 0)
            {
                this.text = text;
                this.senderNumber = senderNumber;
                this.receiverNumber = receiverNumber;
                if (time == 0)
                    this.time = DateTimeOffset.Now.ToUnixTimeSeconds();
                else
                    this.time = time;
                this.unread = true;
            }

            public long GetTimeElapsed()
            {
                return DateTimeOffset.Now.ToUnixTimeSeconds() - time;
            }

            public string SenderID(PhoneManager manager)
            {
                RegisteredPhone phone = manager.FindPhoneByNumber(senderNumber);
                return phone != null ? phone.GetIdentity() : manager.UNREGISTERED;
            }

            public string ReceiverID(PhoneManager manager)
            {
                RegisteredPhone phone = manager.FindPhoneByNumber(receiverNumber);
                return phone != null ? phone.GetIdentity() : manager.UNREGISTERED;
            }
        }

        class PhoneMessageList
        {
            public List<PhoneMessage> messages;

            public PhoneMessageList()
            {
                messages = new List<PhoneMessage>();
            }

            public List<PhoneMessage> RecentMessages(int amount)
            {
                List<PhoneMessage> recent = new List<PhoneMessage>();
                for (int i = Math.Max(0, messages.Count - amount); i < messages.Count; ++i)
                {
                    recent.Add(messages[i]);
                }
                recent.Sort((x, y) => y.time.CompareTo(x.time));
                return recent;
            }

            public int MarkAllAsRead()
            {
                int changed = 0;
                foreach (PhoneMessage message in messages)
                {
                    if (message.unread)
                    {
                        changed++;
                        message.unread = false;
                    }
                }
                return changed;
            }
        }

        class PhoneRecord
        {
            public readonly int callerNumber;
            public readonly int receiverNumber;
            public bool unread;
            public readonly string status; /* answered, missed, outgoing */
            public long time;

            public PhoneRecord(string status, int callerNumber, int receiverNumber, long time = 0)
            {
                this.status = status;
                this.callerNumber = callerNumber;
                this.receiverNumber = receiverNumber;
                if (time == 0)
                    this.time = DateTimeOffset.Now.ToUnixTimeSeconds();
                else
                    this.time = time;
                this.unread = true;
            }

            public long GetTimeElapsed()
            {
                return DateTimeOffset.Now.ToUnixTimeSeconds() - time;
            }

            public string CallerID(PhoneManager manager)
            {
                RegisteredPhone phone = manager.FindPhoneByNumber(callerNumber);
                if (phone != null)
                    return phone.GetIdentity();
                return manager.UNREGISTERED;
            }

            public string ReceiverID(PhoneManager manager)
            {
                RegisteredPhone phone = manager.FindPhoneByNumber(receiverNumber);
                if (phone != null)
                    return phone.GetIdentity();
                return manager.UNREGISTERED;
            }

            public string ID(PhoneManager manager)
            {
                if (status == "outgoing")
                    return ReceiverID(manager);
                return CallerID(manager);
            }

            public int Number(PhoneManager manager)
            {
                if (status == "outgoing")
                    return receiverNumber;
                return callerNumber;
            }
        }

        class PhoneRecordList
        {
            public List<PhoneRecord> records;

            [NonSerialized]
            public PhoneController phoneItem;

            public PhoneRecordList(PhoneController phone)
            {
                records = new List<PhoneRecord>();
                phoneItem = phone;
            }

            public PhoneRecordList()
            {
                records = new List<PhoneRecord>();
            }

            public List<PhoneRecord> RecentRecords(int amount)
            {
                List<PhoneRecord> recent = new List<PhoneRecord>();
                for (int i = Math.Max(0, records.Count - amount); i < records.Count; ++i)
                {
                    recent.Add(records[i]);
                }
                recent.Sort((x, y) => y.time.CompareTo(x.time));
                return recent;
            }

            public void RecordAnswered(PhoneController thisPhone, PhoneController callerPhone, PhoneManager manager)
            {
                records.Add(new PhoneRecord("answered", callerPhone.PhoneNumber, thisPhone.PhoneNumber));
            }

            public void RecordMissed(PhoneController thisPhone, PhoneController callerPhone, PhoneManager manager)
            {
                RegisteredPhone phone = manager.FindPhoneByNumber(thisPhone.PhoneNumber);
                records.Add(new PhoneRecord("missed", callerPhone.PhoneNumber, thisPhone.PhoneNumber));
                foreach (ulong userId in manager.GetPhoneTrackers(phone))
                {
                    BasePlayer tracker = BasePlayer.FindByID(userId);
                    if (tracker.IsValid())
                    {
                        manager.UpdatePhoneRecordsCount(tracker.userID, manager.GetPhoneRecordsCount(tracker.userID) + 1);
                        PLUGIN.ShowCallIndicator(tracker);
                    }
                }
            }

            public void RecordOutgoing(PhoneController thisPhone, PhoneController receiverPhone, PhoneManager manager)
            {
                records.Add(new PhoneRecord("outgoing", thisPhone.PhoneNumber, receiverPhone.PhoneNumber));
            }

            public int MarkAllAsRead()
            {
                int changed = 0;
                foreach (PhoneRecord record in records)
                {
                    if (record.unread)
                    {
                        changed++;
                        record.unread = false;
                    }
                }
                return changed;
            }
        }

        class RegisteredPhone
        {
            public readonly BaseEntity baseEntity;

            public readonly PhoneController phoneController;

            public RegisteredPhone(PhoneController phoneController)
            {
                this.phoneController = phoneController;
                this.baseEntity = phoneController.baseEntity;
            }

            public uint GetID()
            {
                return baseEntity.net.ID;
            }

            public int GetNumber()
            {
                return phoneController.PhoneNumber;
            }

            public string GetName()
            {
                return phoneController.PhoneName;
            }

            public bool HasName()
            {
                return phoneController.PhoneName != null && phoneController.PhoneName != "";
            }

            public string GetIdentity()
            {
                if (HasName())
                    return GetName().Substring(0, Math.Min(16, GetName().Length));
                return GetNumber().ToString();
            }

            public bool UserHasAuth(BasePlayer player)
            {
                BuildingPrivlidge priv = baseEntity.GetBuildingPrivilege();
                if (priv != null)
                {
                    foreach (ProtoBuf.PlayerNameID playerID in priv.authorizedPlayers)
                    {
                        if (playerID.userid == player.userID)
                            return true;
                    }
                }
                return false;
            }
        }

        class PhoneManager
        {
            public List<uint> registeredPhoneIds;
            public List<RegisteredPhone> registeredPhones;
            private Dictionary<uint, RegisteredPhone> id2phone;
            private Dictionary<int, RegisteredPhone> number2phone;
            private Dictionary<ulong, List<uint>> userTrackedPhones;
            private Dictionary<uint, List<ulong>> trackersOfPhone;
            private Dictionary<ulong, Dictionary<string, object>> userPreferences;
            public Dictionary<int, PhoneRecordList> phoneRecords;
            private Dictionary<ulong, int> phoneRecordsCount;
            public Dictionary<int, PhoneMessageList> phoneMessages;
            private Dictionary<ulong, int> phoneMessageCount;
            private Dictionary<ulong, int> hourlyMessageCount;
            private Dictionary<ulong, int> callState;

            public readonly string UNREGISTERED = "???";

            public PhoneManager()
            {
                registeredPhones = new List<RegisteredPhone>();
                registeredPhoneIds = new List<uint>();
                id2phone = new Dictionary<uint, RegisteredPhone>();
                number2phone = new Dictionary<int, RegisteredPhone>();
                userTrackedPhones = new Dictionary<ulong, List<uint>>();
                trackersOfPhone = new Dictionary<uint, List<ulong>>();
                userPreferences = new Dictionary<ulong, Dictionary<string, object>>();
                phoneRecords = new Dictionary<int, PhoneRecordList>();
                phoneRecordsCount = new Dictionary<ulong, int>();
                phoneMessages = new Dictionary<int, PhoneMessageList>();
                phoneMessageCount = new Dictionary<ulong, int>();
                hourlyMessageCount = new Dictionary<ulong, int>();
                callState = new Dictionary<ulong, int>();
                RegisterExistingPhones();
                LoadTrackedPhones();
                LoadPhoneRecords();
                LoadPhoneMessages();
                LoadPreferences();
            }

            public void LeaveMessage(int senderNumber, int targetNumber, string text)
            {
                RegisteredPhone phone = FindPhoneByNumber(targetNumber);
                if (phone != null)
                {
                    if (!phoneMessages.ContainsKey(targetNumber))
                        phoneMessages.Add(targetNumber, new PhoneMessageList());
                    phoneMessages[targetNumber].messages.Add(new PhoneMessage(senderNumber, targetNumber, text));
                    foreach (ulong userId in GetPhoneTrackers(phone))
                    {
                        BasePlayer tracker = BasePlayer.FindByID(userId);
                        if (tracker.IsValid())
                        {
                            UpdatePhoneMessageCount(tracker.userID, GetPhoneMessageCount(tracker.userID) + 1);
                            PLUGIN.ShowUnreadIndicator(tracker);
                        }
                    }
                }
            }

            public void InitPreferences(ulong userID)
            {
                if (!userPreferences.ContainsKey(userID))
                {
                    userPreferences.Add(userID, new Dictionary<string, object>() {
                        { "notifications", true },
                        { "indicators", true }
                    });
                }
            }

            public void SetPreference(ulong userID, string key, object value)
            {
                InitPreferences(userID);
                userPreferences[userID][key] = value;
            }

            public T GetPreference<T>(ulong userID, string key)
            {
                InitPreferences(userID);
                return (T)userPreferences[userID][key];
            }

            public void RegisterPhone(PhoneController phone)
            {
                if (!registeredPhoneIds.Contains(phone.baseEntity.net.ID))
                {
                    RegisteredPhone newPhone = new RegisteredPhone(phone);
                    id2phone.Add(newPhone.GetID(), newPhone);
                    number2phone.Add(newPhone.GetNumber(), newPhone);
                    registeredPhones.Add(newPhone);
                    registeredPhoneIds.Add(newPhone.GetID());
                    phoneRecords.Add(newPhone.GetNumber(), new PhoneRecordList());
                    phoneMessages.Add(newPhone.GetNumber(), new PhoneMessageList());
                }
            }

            public void UnregisterPhone(Telephone baseEntity)
            {
                if (baseEntity.IsValid() && registeredPhoneIds.Contains(baseEntity.net.ID))
                {
                    RegisteredPhone phone = id2phone[baseEntity.net.ID];
                    id2phone.Remove(phone.GetID());
                    number2phone.Remove(phone.GetNumber());
                    foreach (ulong userId in GetPhoneTrackers(phone))
                    {
                        BasePlayer player = BasePlayer.FindByID(userId);
                        if (player.IsValid())
                        {
                            int changedMessages = GetPhoneMessages(phone.GetNumber()).MarkAllAsRead();
                            int changedRecords = GetPhoneRecords(phone.GetNumber()).MarkAllAsRead();
                            UpdatePhoneMessageCount(player.userID, GetPhoneMessageCount(player.userID) - changedMessages);
                            UpdatePhoneRecordsCount(player.userID, GetPhoneRecordsCount(player.userID) - changedRecords);
                            PLUGIN.ShowUnreadIndicator(player);
                            PLUGIN.ShowCallIndicator(player);
                            userTrackedPhones[player.userID].Remove(phone.GetID());
                        }

                    }

                    if (trackersOfPhone.ContainsKey(phone.GetID()))
                        trackersOfPhone.Remove(phone.GetID());
                    registeredPhones.Remove(phone);
                    registeredPhoneIds.Remove(phone.GetID());
                    phoneRecords.Remove(phone.GetNumber());
                    phoneMessages.Remove(phone.GetNumber());

                }
            }

            public bool TrackPhone(BasePlayer player, int phoneNumber)
            {
                RegisteredPhone phone = FindPhoneByNumber(phoneNumber);

                if (phone != null && player.IsValid())
                {
                    if (!userTrackedPhones.ContainsKey(player.userID))
                        userTrackedPhones.Add(player.userID, new List<uint>());
                    if (!trackersOfPhone.ContainsKey(phone.GetID()))
                        trackersOfPhone.Add(phone.GetID(), new List<ulong>());

                    userTrackedPhones[player.userID].Add(phone.GetID());
                    trackersOfPhone[phone.GetID()].Add(player.userID);

                    return true;
                }
                /* Phone not registered */
                return false;
            }

            public bool UntrackPhone(BasePlayer player, int phoneNumber)
            {
                RegisteredPhone phone = FindPhoneByNumber(phoneNumber);

                if (phone != null)
                {
                    if (this.userTrackedPhones.ContainsKey(player.userID) && this.userTrackedPhones[player.userID].Contains(phone.GetID()))
                    {
                        this.userTrackedPhones[player.userID].Remove(phone.GetID());
                        this.trackersOfPhone[phone.GetID()].Remove(player.userID);
                    }
                    return true;
                }
                return false;
            }

            public void ClearTrackers(RegisteredPhone phone)
            {
                if (phone != null && trackersOfPhone.ContainsKey(phone.GetID()))
                {
                    int changedMessages = GetPhoneMessages(phone.GetNumber()).MarkAllAsRead();
                    int changedRecords = GetPhoneRecords(phone.GetNumber()).MarkAllAsRead();
                    foreach (ulong userId in GetPhoneTrackers(phone))
                    {
                        BasePlayer tracker = BasePlayer.FindByID(userId);
                        if (tracker.IsValid())
                        {
                            if (userTrackedPhones.ContainsKey(phone.GetID()))
                                userTrackedPhones[tracker.userID].Remove(phone.GetID());
                            UpdatePhoneMessageCount(tracker.userID, GetPhoneMessageCount(tracker.userID) - changedMessages);
                            PLUGIN.ShowUnreadIndicator(tracker);
                            UpdatePhoneRecordsCount(tracker.userID, GetPhoneRecordsCount(tracker.userID) - changedRecords);
                            PLUGIN.ShowCallIndicator(tracker);
                        }
                    }
                    trackersOfPhone[phone.GetID()] = new List<ulong>();
                }
            }

            public void ClearMessages(RegisteredPhone phone)
            {
                if (phone != null && phoneMessages.ContainsKey(phone.GetNumber()))
                {
                    int changed = GetPhoneMessages(phone.GetNumber()).MarkAllAsRead();
                    phoneMessages[phone.GetNumber()].messages = new List<PhoneMessage>();
                    foreach (ulong userId in GetPhoneTrackers(phone))
                    {
                        BasePlayer tracker = BasePlayer.FindByID(userId);
                        if (tracker.IsValid())
                        {
                            UpdatePhoneMessageCount(tracker.userID, GetPhoneMessageCount(tracker.userID) - changed);
                            PLUGIN.ShowUnreadIndicator(tracker);
                        }
                    }
                }
            }

            public void ClearHistory(RegisteredPhone phone)
            {
                if (phone != null && phoneRecords.ContainsKey(phone.GetNumber()))
                {
                    int changed = GetPhoneRecords(phone.GetNumber()).MarkAllAsRead();
                    phoneRecords[phone.GetNumber()].records = new List<PhoneRecord>();
                    foreach (ulong userId in GetPhoneTrackers(phone))
                    {
                        BasePlayer tracker = BasePlayer.FindByID(userId);
                        if (tracker.IsValid())
                        {
                            UpdatePhoneRecordsCount(tracker.userID, GetPhoneRecordsCount(tracker.userID) - changed);
                            PLUGIN.ShowCallIndicator(tracker);
                        }
                    }
                }
            }

            public void ResetHourlyMessageLimits()
            {
                hourlyMessageCount = new Dictionary<ulong, int>();
            }

            public void IncrementMessageUse(ulong userId)
            {
                if (hourlyMessageCount.ContainsKey(userId))
                    hourlyMessageCount[userId] += 1;
                else
                    hourlyMessageCount.Add(userId, 0);
            }

            public bool ExceededMessageLimit(ulong userId)
            {
                if (hourlyMessageCount.ContainsKey(userId))
                    return hourlyMessageCount[userId] > PLUGIN.config.HourlyMessageLimit;
                return false;
            }

            public RegisteredPhone GetNearbyPhone(BasePlayer entity)
            {
                Vector3 startPos = entity.transform.position;
                double maxDist = 3.0;
                foreach (RegisteredPhone phone in registeredPhones)
                {
                    if (phone != null && phone.baseEntity.IsValid())
                    {
                        Vector3 endPos = phone.baseEntity.transform.position;
                        double dist = Math.Sqrt(Math.Pow(startPos.x - endPos.x, 2) + Math.Pow(startPos.z - endPos.z, 2));
                        if (dist <= maxDist && phone.phoneController.currentPlayer == entity)
                        {
                            return phone;
                        }
                    }
                }
                return null;
            }

            public RegisteredPhone FindPhoneByNumber(int phoneNumber)
            {
                if (number2phone.ContainsKey(phoneNumber))
                    return number2phone[phoneNumber];
                return null;
            }

            public RegisteredPhone FindPhoneByID(uint baseEntityId)
            {
                if (id2phone.ContainsKey(baseEntityId))
                    return id2phone[baseEntityId];
                return null;
            }

            public List<ulong> GetPhoneTrackers(RegisteredPhone phone)
            {

                if (trackersOfPhone.ContainsKey(phone.GetID()))
                {
                    return trackersOfPhone[phone.GetID()];
                }

                return new List<ulong>();
            }

            public List<uint> GetTrackedPhones(ulong basePlayerId)
            {
                if (userTrackedPhones.ContainsKey(basePlayerId))
                    return userTrackedPhones[basePlayerId];
                return new List<uint>();
            }

            public bool IsTrackedBy(RegisteredPhone phone, ulong userId)
            {
                return GetPhoneTrackers(phone).Contains(userId);
            }

            public PhoneRecordList GetPhoneRecords(int phoneNumber)
            {
                if (!phoneRecords.ContainsKey(phoneNumber))
                    phoneRecords.Add(phoneNumber, new PhoneRecordList());
                return phoneRecords[phoneNumber];
            }

            public int GetPhoneRecordsCount(ulong userId)
            {
                if (phoneRecordsCount.ContainsKey(userId))
                    return phoneRecordsCount[userId];
                return 0;
            }

            public void UpdatePhoneRecordsCount(ulong userId, int newValue)
            {
                if (phoneRecordsCount.ContainsKey(userId))
                    phoneRecordsCount[userId] = Math.Max(0, newValue);
                else
                    phoneRecordsCount.Add(userId, Math.Max(0, newValue));
            }

            public PhoneMessageList GetPhoneMessages(int phoneNumber)
            {
                if (!phoneMessages.ContainsKey(phoneNumber))
                    phoneMessages.Add(phoneNumber, new PhoneMessageList());
                return phoneMessages[phoneNumber];
            }

            public int GetPhoneMessageCount(ulong userId)
            {
                if (phoneMessageCount.ContainsKey(userId))
                    return phoneMessageCount[userId];
                return 0;
            }

            public void UpdatePhoneMessageCount(ulong userId, int newValue)
            {
                if (phoneMessageCount.ContainsKey(userId))
                    phoneMessageCount[userId] = Math.Max(0, newValue);
                else
                    phoneMessageCount.Add(userId, Math.Max(0, newValue));
            }

            public void SetState(BasePlayer player, int value)
            {
                if (callState.ContainsKey(player.userID))
                    callState[player.userID] = value;
                else
                    callState.Add(player.userID, value);
            }

            public bool IsStateEqualTo(BasePlayer player, int value)
            {
                if (callState.ContainsKey(player.userID))
                    return callState[player.userID] == value;
                else
                    callState.Add(player.userID, 0);
                return callState[player.userID] == value;
            }

            public void RegisterExistingPhones()
            {
                List<Telephone> allPhones = BaseNetworkable.serverEntities.OfType<Telephone>().ToList();
                foreach (Telephone phone in allPhones)
                    RegisterPhone(phone.Controller);
                PLUGIN.Puts(registeredPhones.Count() + " registered");
            }

            #region File IO
            public void SaveTrackedPhones()
            {
                SaveFile<Dictionary<ulong, List<uint>>>("tracked", userTrackedPhones);
            }

            public void LoadTrackedPhones()
            {
                try
                {
                    Dictionary<ulong, List<uint>> tracked = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<uint>>>("PhonesPlus/tracked");
                    Dictionary<uint, List<ulong>> temp = new Dictionary<uint, List<ulong>>();
                    foreach (ulong userId in tracked.Keys)
                    {
                        BasePlayer player = BasePlayer.FindByID(userId);
                        if (player.IsValid())
                        {
                            List<uint> phones = tracked[userId];
                            foreach (uint entityId in phones)
                            {
                                if (id2phone.ContainsKey(entityId))
                                {
                                    RegisteredPhone phone = id2phone[entityId];
                                    if (phone != null)
                                    {
                                        TrackPhone(player, phone.GetNumber());
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    PLUGIN.PrintWarning("Failed to load from data file 'tracked'");
                }
            }

            public void SavePhoneRecords()
            {
                SaveFile<Dictionary<int, PhoneRecordList>>("records", phoneRecords);
            }

            public void LoadPhoneRecords()
            {
                phoneRecords = LoadFromFile<Dictionary<int, PhoneRecordList>>("records", phoneRecords);
            }

            public void SavePhoneMessages()
            {
                SaveFile<Dictionary<int, PhoneMessageList>>("messages", phoneMessages);
            }

            public void LoadPhoneMessages()
            {
                phoneMessages = LoadFromFile<Dictionary<int, PhoneMessageList>>("messages", phoneMessages);
                foreach (int number in phoneMessages.Keys)
                {
                    RegisteredPhone phone = FindPhoneByNumber(number);
                    if (phone != null)
                    {
                        foreach (ulong userId in GetPhoneTrackers(phone))
                        {
                            BasePlayer player = BasePlayer.FindByID(userId);
                            if (player.IsValid())
                            {
                                PhoneMessageList messageList = phoneMessages[number];
                                foreach (PhoneMessage message in messageList.RecentMessages(6))
                                {
                                    if (message.unread)
                                    {
                                        UpdatePhoneMessageCount(userId, GetPhoneMessageCount(userId) + 1);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            public void SavePreferences()
            {
                SaveFile<Dictionary<ulong, Dictionary<string, object>>>("preferences", userPreferences);
            }

            public void LoadPreferences()
            {
                userPreferences = LoadFromFile<Dictionary<ulong, Dictionary<string, object>>>("preferences", userPreferences);
            }

            private void SaveFile<T>(string fileName, T data)
            {
                try
                {
                    Interface.Oxide.DataFileSystem.WriteObject("PhonesPlus/" + fileName, data);
                }
                catch (Exception)
                {
                    PLUGIN.PrintWarning("Failed to save data to file '" + fileName + "'");
                }
            }

            private T LoadFromFile<T>(string fileName, T defaultValue)
            {
                try
                {
                    T data = Interface.Oxide.DataFileSystem.ReadObject<T>("PhonesPlus/" + fileName);
                    return data;
                }
                catch (Exception)
                {
                    PLUGIN.PrintWarning("Failed to load from data file '" + fileName + "'");
                }
                return defaultValue;
            }
            #endregion
        }
        #endregion

        #region UI

        #region History Panel
        CuiElementContainer CreateHistoryPanel(BasePlayer player, RegisteredPhone phone)
        {
            CuiElementContainer container = new CuiElementContainer();
            if (player.IsValid() && phone != null)
            {
                container.Add(new CuiPanel
                {
                    Image =
                {
                    Color = "0 0 0 0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.685 0.1275",
                    AnchorMax = "0.900 0.76"
                }
                }, "Overlay", "phonesHistory");
                container.Add(new CuiPanel
                {
                    Image =
                {
                    Color = COLOR_RUST_BODY.RGBA()
                },
                    RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 0.92"
                }
                }, "phonesHistory", "phonesHistoryBody");
                container.Add(new CuiPanel
                {
                    Image =
                {
                    Color = COLOR_RUST_HEADER.RGBA()
                },
                    RectTransform =
                {
                    AnchorMin = "0 0.92",
                    AnchorMax = "1 1"
                }
                }, "phonesHistory", "phonesHistoryHeader");
                container.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = lang.GetMessage("UI title history", this, player.UserIDString),
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
                }, "phonesHistoryHeader", "phonesHistoryHeaderText");
                container.Add(new CuiElement
                {
                    Name = "phonesHistoryHeaderImage",
                    Parent = "phonesHistoryHeader",
                    Components =
                {
                    new CuiRawImageComponent {
                        Png = ImageLibrary?.Call<string>("GetImage", "HistoryIcon")
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.04 0.3",
                        AnchorMax = "0.10 0.7"
                    }
                },
                });

                List<PhoneRecord> records = phoneManager.GetPhoneRecords(phone.GetNumber()).RecentRecords(10);
                if (records.Count > 0)
                {
                    int index = 0;
                    double bottomS = 0.9;
                    double topS = 0.97;
                    double mod = 0.08;
                    foreach (PhoneRecord record in phoneManager.GetPhoneRecords(phone.GetNumber()).RecentRecords(10))
                    {
                        CreateHistoryEntry(container, player, record, index, mod, bottomS, topS);
                        index++;
                    }
                    container.Add(new CuiElement
                    {
                        Name = "phonesHistoryBodyButton",
                        Parent = "phonesHistoryBody",
                        Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = "phone.clear history " + phone.GetNumber(),
                            Color = COLOR_PP_WARN.RGBA()
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.38 0.05",
                            AnchorMax = "0.62 0.11"
                        }
                    }
                    });
                    container.Add(new CuiElement
                    {
                        Name = "phonesHistoryBodyButtonText",
                        Parent = "phonesHistoryBodyButton",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = lang.GetMessage("UI title clear", this, player.UserIDString),
                                FontSize = 10,
                                Color = "1 1 1 1",
                                Align = TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1"
                            }
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Name = "phonesHistoryBodyText",
                        Parent = "phonesHistoryBody",
                        Components =
                {
                    new CuiTextComponent {
                        Text = lang.GetMessage("No history", this, player.UserIDString),
                        Color = "1 1 1 0.5",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                },
                    });
                }
            }

            return container;
        }

        void CreateHistoryEntry(CuiElementContainer container, BasePlayer player, PhoneRecord record, int index, double mod, double bottomS, double topS)
        {
            float opacity = 0.9f;
            if (!record.unread)
                opacity = 0.3f;
            string bodyColor = COLOR_PP_ENTRY_BODY.RGBA(opacity);
            string titleColor = COLOR_PP_ENTRY_TITLE_FONT.RGBA(opacity);

            container.Add(new CuiElement
            {
                Name = "phonesHistoryEntry" + index,
                Parent = "phonesHistoryBody",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = bodyColor,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.05 " + (bottomS - (index * mod)),
                        AnchorMax = "0.95 " + (topS - (index * mod)),
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesHistoryEntryText" + index,
                Parent = "phonesHistoryEntry" + index,
                Components =
                {
                    new CuiTextComponent
                    {
                        Color = titleColor,
                        Text = string.Format(lang.GetMessage("History " + record.status, this, player.UserIDString), record.ID(phoneManager)),
                        Align = TextAnchor.MiddleLeft,
                        FontSize = 10
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.05 0",
                        AnchorMax = "0.65 1",
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesHistoryEntryTime" + index,
                Parent = "phonesHistoryEntry" + index,
                Components =
                {
                    new CuiTextComponent
                    {
                        Color = titleColor,
                        Text = ElapsedTimeString(player, record.GetTimeElapsed()),
                        Align = TextAnchor.MiddleRight,
                        FontSize = 10
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.70 0",
                        AnchorMax = "0.95 1",
                    }
                }
            });
        }

        CuiElementContainer CreateTrackButton(BasePlayer player, RegisteredPhone phone)
        {
            CuiElementContainer container = new CuiElementContainer();
            string buttonCmd = "phone.track " + phone.GetNumber();
            string buttonColor = COLOR_PP_SUBMIT.RGBA();
            string buttonText = lang.GetMessage("UI title track", this, player.UserIDString);
            if (phoneManager.IsTrackedBy(phone, player.userID))
            {
                buttonCmd = "phone.untrack " + phone.GetNumber();
                buttonColor = COLOR_PP_WARN.RGBA();
                buttonText = lang.GetMessage("UI title untrack", this, player.UserIDString);
            }
            container.Add(new CuiElement
            {
                Name = "phonesTrackButton",
                Parent = "Overlay",
                Components =
                {
                    new CuiButtonComponent
                    {
                        Command = buttonCmd,
                        Color = buttonColor,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.425 0.03",
                        AnchorMax = "0.575 0.09"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesTrackButtonText",
                Parent = "phonesTrackButton",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = buttonText,
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });
            return container;
        }

        #endregion

        #region Messages Panel
        CuiElementContainer CreateMessagesPanel(BasePlayer player, RegisteredPhone phone, bool centered = false)
        {
            CuiElementContainer container = new CuiElementContainer();
            float aMin1 = 0.098f;
            float aMin2 = 0.1275f;
            float aMax1 = 0.313f;
            float aMax2 = 0.76f;
            float opacity = 0.9f;
            if (centered)
            {
                float modX = 0.3f;
                aMin1 += modX;
                aMax1 += modX;
                opacity = 0.99f;
            }

            if (player.IsValid() && phone != null)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = centered,
                    Image =
                    {
                        Color = "0 0 0 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = aMin1.ToString() + " " + aMin2.ToString(),
                        AnchorMax = aMax1.ToString() + " " + aMax2.ToString(),
                    }
                }, "Overlay", "phonesMessages");
                container.Add(new CuiPanel
                {
                    Image =
                {
                    Color = COLOR_RUST_BODY.RGBA(opacity)
                },
                    RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 0.92"
                }
                }, "phonesMessages", "phonesMessagesBody");
                container.Add(new CuiPanel
                {
                    Image =
                {
                    Color = COLOR_RUST_HEADER.RGBA(opacity)
                },
                    RectTransform =
                {
                    AnchorMin = "0 0.92",
                    AnchorMax = "1 1"
                }
                }, "phonesMessages", "phonesMessagesHeader");
                container.Add(new CuiLabel
                {
                    Text =
                {
                    Text = lang.GetMessage("UI title messages", this, player.UserIDString),
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter
                },
                    RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
                }, "phonesMessagesHeader", "phonesMessagesHeaderText");
                container.Add(new CuiElement
                {
                    Name = "phonesMessagesHeaderImage",
                    Parent = "phonesMessagesHeader",
                    Components =
                {
                    new CuiRawImageComponent {
                        Png = ImageLibrary.Call<string>("GetImage", "MessageIcon"),
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.04 0.3",
                        AnchorMax = "0.10 0.7"
                    }
                },
                });


                List<PhoneMessage> messages = phoneManager.GetPhoneMessages(phone.GetNumber()).RecentMessages(6);
                if (messages.Count > 0)
                {
                    int index = 0;
                    double bottomS = 0.85;
                    double topS = 0.97;
                    double mod = 0.13;
                    foreach (PhoneMessage message in messages)
                    {
                        CreateMessageEntry(container, player, message, index, mod, bottomS, topS);
                        index++;
                    }
                    container.Add(new CuiElement
                    {
                        Name = "phonesMessagesBodyButton",
                        Parent = "phonesMessagesBody",
                        Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = "phone.clear messages " + phone.GetNumber(),
                            Color = COLOR_PP_WARN.RGBA()
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.38 0.05",
                            AnchorMax = "0.62 0.11"
                        }
                    }
                    });
                    container.Add(new CuiElement
                    {
                        Name = "phonesMessagesBodyButtonText",
                        Parent = "phonesMessagesBodyButton",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = lang.GetMessage("UI title clear", this, player.UserIDString),
                                FontSize = 10,
                                Color = "1 1 1 1",
                                Align = TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1"
                            }
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Name = "phonesMessagesBodyText",
                        Parent = "phonesMessagesBody",
                        Components =
                {
                    new CuiTextComponent {
                        Text = lang.GetMessage("No messages", this, player.UserIDString),
                        Color = "1 1 1 0.5",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                },
                    });
                }
            }

            return container;
        }

        void CreateMessageEntry(CuiElementContainer container, BasePlayer player, PhoneMessage message, int index, double mod, double bottomS, double topS)
        {
            float opacity = 0.9f;
            if (!message.unread)
                opacity = 0.3f;
            string headerColor = COLOR_PP_ENTRY_HEADER.RGBA(opacity);
            string bodyColor = COLOR_PP_ENTRY_BODY.RGBA(opacity);
            string titleColor = COLOR_PP_ENTRY_TITLE_FONT.RGBA(opacity);
            string textColor = COLOR_PP_ENTRY_BODY_FONT.RGBA(opacity);

            container.Add(new CuiElement
            {
                Name = "phonesMessagesEntry" + index,
                Parent = "phonesMessagesBody",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0.5"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.05 " + (bottomS - (index * mod)),
                        AnchorMax = "0.95 " + (topS - (index * mod)),
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesMessagesEntryHeader" + index,
                Parent = "phonesMessagesEntry" + index,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = headerColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.5",
                        AnchorMax = "1 1"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesMessagesEntryBody" + index,
                Parent = "phonesMessagesEntry" + index,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = bodyColor,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.49"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesMessagesEntryHeaderName" + index,
                Parent = "phonesMessagesEntryHeader" + index,
                Components =
                {
                    new CuiTextComponent
                    {
                        Color = titleColor,
                        Text = message.SenderID(phoneManager),
                        FontSize = 10,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.03 0",
                        AnchorMax = "0.50 1"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesMessagesEntryHeaderTime" + index,
                Parent = "phonesMessagesEntryHeader" + index,
                Components =
                {
                    new CuiTextComponent
                    {
                        Color = titleColor,
                        Text = ElapsedTimeString(player, message.GetTimeElapsed()),
                        FontSize = 10,
                        Align = TextAnchor.MiddleRight
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.97 1"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesMessagesEntryBodyText" + index,
                Parent = "phonesMessagesEntryBody" + index,
                Components =
                {
                    new CuiTextComponent
                    {
                        Color = textColor,
                        Text = message.text,
                        FontSize = 10,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.05 0",
                        AnchorMax = "0.95 1"
                    }
                }
            });
        }

        #endregion

        #region Input Panel
        CuiElementContainer CreateInputPanel(BasePlayer player, int callerNumber, int receiverNumber)
        {
            string panelColor = "0.36 0.34 0.3 0.99";
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.9"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
            }, "Overlay", "phonesInputOverlay");
            container.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0.35 0.45",
                    AnchorMax = "0.65 0.6"
                }
            }, "phonesInputOverlay", "phonesInput");
            container.Add(new CuiPanel
            {
                Image =
                {
                    Color = panelColor
                },
                RectTransform =
                {
                    AnchorMin = "0 0.7",
                    AnchorMax = "1 1"
                }
            }, "phonesInput", "phonesInputHeader");
            container.Add(new CuiPanel
            {
                Image =
                {
                    Color = panelColor
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 0.69"
                }
            }, "phonesInput", "phonesInputBody");
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = lang.GetMessage("UI title prompt", this, player.UserIDString),
                    Color = "1 1 1 0.9",
                    FontSize = 14,
                    Align = TextAnchor.MiddleLeft
                },
                RectTransform =
                {
                    AnchorMin = "0.05 0",
                    AnchorMax = "0.95 1"
                }
            }, "phonesInputHeader", "phonesInputHeaderText");
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = ""
                },
                Button =
                {
                    Command = "phone.message " + callerNumber + " " + receiverNumber + " ",
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0.92 0.25",
                    AnchorMax = "0.97 0.75"
                }
            }, "phonesInputHeader", "phonesInputHeaderButton");
            container.Add(new CuiElement
            {
                Name = "phonesInputHeaderButtonImage",
                Parent = "phonesInputHeaderButton",
                Components =
                {
                    new CuiRawImageComponent {
                        Png = ImageLibrary.Call<string>("GetImage", "CloseIcon"),
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.1 0.1",
                        AnchorMax = "0.9 0.9"
                    }
                },
            });
            container.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.5"
                },
                RectTransform =
                {
                    AnchorMin = "0.025 0.18",
                    AnchorMax = "0.975 0.82"
                }
            }, "phonesInputBody", "phonesInputBodyField");
            container.Add(new CuiElement
            {
                Name = "phonesInputBodyFieldInput",
                Parent = "phonesInputBodyField",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        CharsLimit = 40,
                        Command = "phone.message " + callerNumber + " " + receiverNumber,
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleLeft,
                        FontSize = 14
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.02 0",
                        AnchorMax = "0.98 1"
                    }
                },
            });
            return container;
        }
        #endregion

        #region Notification Panel
        CuiElementContainer CreateNotificationPanel(BasePlayer player, string message, RegisteredPhone phone)
        {
            CuiElementContainer container = new CuiElementContainer();
            string bodyOpacity = " 0.9";
            string titleOpacity = " 0.95";
            container.Add(new CuiElement
            {
                Name = "phonesNotify",
                Parent = "Hud",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = config.HudNotification.AnchorMin,
                        AnchorMax = config.HudNotification.AnchorMax
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesNotifyHeader",
                Parent = "phonesNotify",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = config.HudNotification.BackgroundColor + titleOpacity
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.82",
                        AnchorMax = "1 1"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesNotifyHeaderText",
                Parent = "phonesNotifyHeader",
                Components =
                {
                    new CuiTextComponent
                    {
                        FontSize = 9,
                        Color = "1 1 1 1",
                        Text = phone.GetIdentity(),
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesNotifyBody",
                Parent = "phonesNotify",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = config.HudNotification.BackgroundColor + bodyOpacity
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.25",
                        AnchorMax = "1 0.80"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesNotifyBodyImage",
                Parent = "phonesNotifyBody",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = ImageLibrary.Call<string>("GetImage", "CallIcon"),
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.01 0.08",
                        AnchorMax = "0.13 0.9"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesNotifyBodyText",
                Parent = "phonesNotifyBody",
                Components =
                {
                    new CuiTextComponent
                    {
                        FontSize = 12,
                        Color = "1 1 1 1",
                        Text = message,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.14 0.08",
                        AnchorMax = "0.86 0.9"
                    }
                }
            });
            return container;
        }
        #endregion

        #region Indicators
        CuiElementContainer CreateUnreadIndicator(BasePlayer player, int messages)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "phonesUnread",
                Parent = "Hud",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = config.HudMessageIndicator.AnchorMin,
                        AnchorMax = config.HudMessageIndicator.AnchorMax
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesUnreadImage",
                Parent = "phonesUnread",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = ImageLibrary.Call<string>("GetImage", "MessageIcon"),
                        Color = config.HudMessageIndicator.ImageColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.35",
                        AnchorMax = "1 1"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesUnreadText",
                Parent = "phonesUnread",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = messages.ToString(),
                        Color = "1 1 1 1",
                        Align = TextAnchor.LowerRight
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.1",
                        AnchorMax = "1 1"
                    }
                }
            });
            return container;
        }

        CuiElementContainer CreateCallIndicator(BasePlayer player, int calls)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "phonesCalls",
                Parent = "Hud",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = config.HudCallIndicator.AnchorMin,
                        AnchorMax = config.HudCallIndicator.AnchorMax
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesCallsImage",
                Parent = "phonesCalls",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = ImageLibrary.Call<string>("GetImage", "CallIcon"),
                        Color = config.HudCallIndicator.ImageColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.35",
                        AnchorMax = "1 1"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = "phonesCallsText",
                Parent = "phonesCalls",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = calls.ToString(),
                        Color = "1 1 1 1",
                        Align = TextAnchor.LowerRight
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.1",
                        AnchorMax = "1 1"
                    }
                }
            });
            return container;
        }
        #endregion

        #region Functions
        void ShowPhoneUI(BasePlayer player, RegisteredPhone phone)
        {
            HideAllUI(player);
            if (permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                permission.GrantUserPermission(player.UserIDString, PermissionTemp, this);
                CuiElementContainer container1 = CreateHistoryPanel(player, phone);
                CuiHelper.AddUi(player, container1);
                CuiElementContainer container2 = CreateMessagesPanel(player, phone);
                CuiHelper.AddUi(player, container2);
                CuiElementContainer container3 = CreateTrackButton(player, phone);
                CuiHelper.AddUi(player, container3);
            }
        }

        void ShowMessagesUI(BasePlayer player, RegisteredPhone phone)
        {
            HideAllUI(player);
            if (permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                CuiElementContainer container = CreateMessagesPanel(player, phone, true);
                CuiHelper.AddUi(player, container);
            }
        }

        void ShowNotificationHUD(BasePlayer player, string message, RegisteredPhone phone)
        {
            CuiHelper.DestroyUi(player, "phonesNotify");
            if (permission.UserHasPermission(player.UserIDString, PermissionUse) && phoneManager.GetPreference<bool>(player.userID, "notifications"))
            {
                CuiElementContainer container = CreateNotificationPanel(player, message, phone);
                CuiHelper.AddUi(player, container);
                timer.Once(12f, () =>
                {
                    CuiHelper.DestroyUi(player, "phonesNotify");
                });
            }
        }

        void HideNotificationHUD(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "phonesNotify");
        }

        void ShowInputUI(BasePlayer player, int senderNumber, int receiverNumber)
        {
            CuiHelper.DestroyUi(player, "phonesInputOverlay");
            if (permission.UserHasPermission(player.UserIDString, PermissionUse) && !phoneManager.ExceededMessageLimit(player.userID))
            {
                CuiElementContainer container2 = CreateInputPanel(player, senderNumber, receiverNumber);
                CuiHelper.AddUi(player, container2);
            }
        }

        void ShowUnreadIndicator(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "phonesUnread");
            if (permission.UserHasPermission(player.UserIDString, PermissionUse) && phoneManager.GetPreference<bool>(player.userID, "indicators"))
            {
                int count = phoneManager.GetPhoneMessageCount(player.userID);
                if (count > 0)
                {
                    CuiElementContainer container = CreateUnreadIndicator(player, count);
                    CuiHelper.AddUi(player, container);
                }
            }
        }

        void ShowCallIndicator(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "phonesCalls");
            if (permission.UserHasPermission(player.UserIDString, PermissionUse) && phoneManager.GetPreference<bool>(player.userID, "indicators"))
            {
                int count = phoneManager.GetPhoneRecordsCount(player.userID);
                if (count > 0)
                {
                    CuiElementContainer container = CreateCallIndicator(player, count);
                    CuiHelper.AddUi(player, container);
                }
            }
        }

        void HideAllUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "phonesTrackButton");
            CuiHelper.DestroyUi(player, "phonesInputOverlay");
            CuiHelper.DestroyUi(player, "phonesHistory");
            CuiHelper.DestroyUi(player, "phonesMessages");
            permission.RevokeUserPermission(player.UserIDString, PermissionTemp);
        }
        #endregion

        #region Constants
        private readonly ColorUI COLOR_RUST_BODY = new ColorUI(0.15f, 0.14f, 0.11f, 0.9f);
        private readonly ColorUI COLOR_RUST_HEADER = new ColorUI(0.36f, 0.34f, 0.3f, 0.9f);
        private readonly ColorUI COLOR_PP_UI = new ColorUI(0.8f, 0.5f, 0.5f, 0.9f);
        private readonly ColorUI COLOR_PP_WARN = new ColorUI(0.7f, 0.22f, 0.15f, 1f);
        private readonly ColorUI COLOR_PP_SUBMIT = new ColorUI(0.45f, 0.55f, 0.27f, 1f);
        private readonly ColorUI COLOR_PP_ENTRY_BODY = new ColorUI(0.05f, 0.05f, 0.05f, 0.9f);
        private readonly ColorUI COLOR_PP_ENTRY_HEADER = new ColorUI(0.1f, 0.1f, 0.1f, 0.9f);
        private readonly ColorUI COLOR_PP_ENTRY_TITLE_FONT = new ColorUI(0.8f, 0.8f, 0.8f, 1f);
        private readonly ColorUI COLOR_PP_ENTRY_BODY_FONT = new ColorUI(1f, 1f, 1f, 1f);

        class ColorUI
        {
            public float r;
            public float g;
            public float b;
            public float a;

            public ColorUI(float r, float g, float b, float a = 1f)
            {
                this.r = r;
                this.g = g;
                this.b = b;
                this.a = a;
            }

            public string RGBA()
            {
                return RGBA(a);
            }

            public string RGBA(float a)
            {
                return string.Format("{0} {1} {2} {3}", r, g, b, a);
            }
        }
        #endregion

        #endregion
    }

}
