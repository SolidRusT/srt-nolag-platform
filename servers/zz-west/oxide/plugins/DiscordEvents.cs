using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Time = UnityEngine.Time;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Discord Events", "MON@H", "1.2.0")]
    [Description("Displays events to a discord channel")]
    class DiscordEvents : CovalencePlugin
    {
        #region Class Fields

        [PluginReference] private Plugin BetterChatMute, PersonalHeli;

        private Dictionary<DiscordMessage, string> _queueMessages = new Dictionary<DiscordMessage, string>();
        private Dictionary<DiscordMessage, string> _queueProcessed = new Dictionary<DiscordMessage, string>();
        private Dictionary<uint, float> _lastEntities = new Dictionary<uint, float>();

        private readonly List<Regex> _regexTags = new List<Regex>
        {
            new Regex("<color=.+?>", RegexOptions.Compiled),
            new Regex("<size=.+?>", RegexOptions.Compiled)
        };

        private readonly List<string> _tags = new List<string>
        {
            "</color>",
            "</size>",
            "<i>",
            "</i>",
            "<b>",
            "</b>"
        };

        private enum EventType
        {
            Bradley,
            CargoPlane,
            CargoShip,
            Chat,
            ChatTeam,
            Chinook,
            Christmas,
            DangerousTreasures,
            Death,
            DeathNotes,
            Duel,
            Easter,
            Halloween,
            Helicopter,
            LockedCrate,
            None,
            PlayerConnected,
            PlayerConnectedInfo,
            PlayerDisconnected,
            RaidableBases,
            SantaSleigh,
            SupplyDrop,
            SupplySignal
        }

        #endregion Class Fields

        #region Initialization

        private void Init()
        {
            UnsubscribeDisabled();
        }

        private void OnServerInitialized(bool isStartup)
        {
            if (isStartup && _configData.ServerStateSettings.Enabled)
            {
                if (_configData.GlobalSettings.LoggingEnabled)
                {
                    Puts("Server is online again!");
                }

                SendMessage(Lang("Initialized"), _configData.ServerStateSettings.WebhookURL);
            }
        }

        private void OnServerShutdown()
        {
            if (_configData.ServerStateSettings.Enabled)
            {
                if (_configData.GlobalSettings.LoggingEnabled)
                {
                    Puts("Server is shutting down!");
                }

                DiscordMessage discordMessage = new DiscordMessage(Lang("Shutdown"));
                SendDiscordMessage(_configData.ServerStateSettings.WebhookURL, discordMessage);
            }
        }

        #endregion Initialization

        #region Configuration

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Global settings")]
            public GlobalSettings GlobalSettings = new GlobalSettings();

            [JsonProperty(PropertyName = "Bradley settings")]
            public EventSettings BradleySettings = new EventSettings();

            [JsonProperty(PropertyName = "Cargo Ship settings")]
            public EventSettings CargoShipSettings = new EventSettings();

            [JsonProperty(PropertyName = "Cargo Plane settings")]
            public EventSettings CargoPlaneSettings = new EventSettings();

            [JsonProperty(PropertyName = "Chat settings")]
            public EventSettings ChatSettings = new EventSettings();

            [JsonProperty(PropertyName = "Chat (Team) settings")]
            public EventSettings ChatTeamSettings = new EventSettings();

            [JsonProperty(PropertyName = "CH47 Helicopter settings")]
            public EventSettings ChinookSettings = new EventSettings();

            [JsonProperty(PropertyName = "Christmas settings")]
            public EventSettings ChristmasSettings = new EventSettings();

            [JsonProperty(PropertyName = "Dangerous Treasures settings")]
            public EventSettings DangerousTreasuresSettings = new EventSettings();

            [JsonProperty(PropertyName = "Duel settings")]
            public EventSettings DuelSettings = new EventSettings();

            [JsonProperty(PropertyName = "Easter settings")]
            public EventSettings EasterSettings = new EventSettings();

            [JsonProperty(PropertyName = "Hackable Locked Crate settings")]
            public EventSettings LockedCrateSettings = new EventSettings();

            [JsonProperty(PropertyName = "Halloween settings")]
            public EventSettings HalloweenSettings = new EventSettings();

            [JsonProperty(PropertyName = "Helicopter settings")]
            public EventSettings HelicopterSettings = new EventSettings();

            [JsonProperty(PropertyName = "Player death settings")]
            public EventSettings PlayerDeathSettings = new EventSettings();

            [JsonProperty(PropertyName = "Player DeathNotes settings")]
            public EventSettings PlayerDeathNotesSettings = new EventSettings();

            [JsonProperty(PropertyName = "Player connect advanced info settings")]
            public EventSettings PlayerConnectedInfoSettings = new EventSettings();

            [JsonProperty(PropertyName = "Player connect settings")]
            public EventSettings PlayerConnectedSettings = new EventSettings();

            [JsonProperty(PropertyName = "Player disconnect settings")]
            public EventSettings PlayerDisconnectedSettings = new EventSettings();

            [JsonProperty(PropertyName = "Raidable Bases settings")]
            public EventSettings RaidableBasesSettings = new EventSettings();

            [JsonProperty(PropertyName = "SantaSleigh settings")]
            public EventSettings SantaSleighSettings = new EventSettings();

            [JsonProperty(PropertyName = "Server state settings")]
            public EventSettings ServerStateSettings = new EventSettings();

            [JsonProperty(PropertyName = "Supply Drop settings")]
            public EventSettings SupplyDropSettings = new EventSettings();
        }

        private class GlobalSettings
        {
            [JsonProperty(PropertyName = "Log to console?")]
            public bool LoggingEnabled = false;
        }

        private class EventSettings
        {
            [JsonProperty(PropertyName = "WebhookURL")]
            public string WebhookURL = "";

            [JsonProperty(PropertyName = "Enabled?")]
            public bool Enabled = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(_configData);

        #endregion Configuration

        #region Localization

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Bradley"] = ":dagger: Bradley spawned `{0}`",
                ["CargoPlane"] = ":airplane: Cargo Plane incoming `{0}`",
                ["CargoShip"] = ":ship: Cargo Ship incoming `{0}`",
                ["Chat"] = ":speech_left: **{0}**: {1}",
                ["ChatTeam"] = ":busts_in_silhouette: **{0}**: {1}",
                ["Chinook"] = ":helicopter: Chinook 47 incoming `{0}`",
                ["Christmas"] = ":christmas_tree: Christmas event started",
                ["DangerousTreasuresEnded"] = ":pirate_flag: Dangerous Treasures event at `{0}` is ended",
                ["DangerousTreasuresStarted"] = ":pirate_flag: Dangerous Treasures started at `{0}`",
                ["Death"] = ":skull: `{0}` died",
                ["DeathNotes"] = ":skull_crossbones: {0}",
                ["Duel"] = ":crossed_swords: `{0}` has defeated `{1}` in a duel",
                ["Easter"] = ":egg: Easter event started",
                ["EasterWinner"] = ":egg: Easter event ended. The winner is `{0}`",
                ["Halloween"] = ":jack_o_lantern: Halloween event started",
                ["HalloweenWinner"] = ":jack_o_lantern: Halloween event ended. The winner is `{0}`",
                ["Helicopter"] = ":dagger: Helicopter incoming `{0}`",
                ["Initialized"] = ":ballot_box_with_check: Server is online again!",
                ["LockedCrate"] = ":package: Codelocked crate is here `{0}`",
                ["PersonalHelicopter"] = ":dagger: Personal Helicopter incoming `{0}`",
                ["PlayerConnected"] = ":white_check_mark: {0} connected",
                ["PlayerConnectedInfo"] = ":detective: {0} connected. SteamID: `{1}` IP: `{2}`",
                ["PlayerDisconnected"] = ":x: {0} disconnected ({1})",
                ["RaidableBaseEnded"] = ":homes: {1} Raidable Base at `{0}` is ended",
                ["RaidableBaseStarted"] = ":homes: {1} Raidable Base spawned at `{0}`",
                ["SantaSleigh"] = ":santa: SantaSleigh Event started",
                ["Shutdown"] = ":stop_sign: Server is shutting down!",
                ["SupplyDrop"] = ":parachute: SupplyDrop incoming at `{0}`",
                ["SupplyDropLanded"] = ":gift: SupplyDrop landed at `{0}`",
                ["SupplySignal"] = ":firecracker: SupplySignal was thrown by `{0}` at `{1}`",

                ["Easy"] = "Easy",
                ["Medium"] = "Medium",
                ["Hard"] = "Hard",
                ["Expert"] = "Expert",
                ["Nightmare"] = "Nightmare"
            }, this);
        }

        #endregion Localization

        #region Events Hooks

        private void OnEntitySpawned(BaseHelicopter entity)
        {
            NextTick(() => {
                HandleEntity(entity);
            });
        }

        private void OnEntitySpawned(BradleyAPC entity)
        {
            HandleEntity(entity);
        }

        private void OnEntitySpawned(CargoPlane entity)
        {
            HandleEntity(entity);
        }

        private void OnEntitySpawned(CargoShip entity)
        {
            HandleEntity(entity);
        }

        private void OnEntitySpawned(CH47HelicopterAIController entity)
        {
            HandleEntity(entity);
        }

        private void OnEntitySpawned(EggHuntEvent entity)
        {
            HandleEntity(entity);
        }

        private void OnEntitySpawned(HackableLockedCrate entity)
        {
            HandleEntity(entity);
        }

        private void OnEntitySpawned(SantaSleigh entity)
        {
            HandleEntity(entity);
        }
        private void OnEntitySpawned(SupplyDrop entity)
        {
            HandleEntity(entity);
        }

        private void OnEntitySpawned(XMasRefill entity)
        {
            HandleEntity(entity);
        }

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null)
            {
                return;
            }

            if (_configData.PlayerDeathSettings.Enabled)
            {
                if (_configData.GlobalSettings.LoggingEnabled)
                {
                    Puts($"{player.displayName.Replace("*", "＊")} died.");
                }

                SendMessage(Lang("Death", null, player.displayName.Replace("*", "＊")), _configData.PlayerDeathSettings.WebhookURL);
            }
        }

        private void OnDeathNotice(Dictionary<string, object> data, string message)
        {
            if (_configData.PlayerDeathNotesSettings.Enabled)
            {
                SendMessage(Lang("DeathNotes", null, StripRustTags(Formatter.ToPlaintext(message))), _configData.PlayerDeathNotesSettings.WebhookURL);
            }
        }

        private void OnEntityKill(EggHuntEvent entity)
        {
            if (entity == null)
            {
                return;
            }

            var winners = entity.GetTopHunters();
            string winner;
            if (winners.Count > 0)
            {
                winner = winners[0].displayName.Replace("*", "＊");
            }
            else
            {
                winner = "No winner";
            }

            bool isHalloween = entity is HalloweenHunt;
            if (isHalloween)
            {
                if (_configData.HalloweenSettings.Enabled)
                {
                    if (_configData.GlobalSettings.LoggingEnabled)
                    {
                        Puts("Halloween Hunt Event has ended. The winner is " + winner);
                    }

                    SendMessage(Lang("HalloweenWinner", null, winner), _configData.HalloweenSettings.WebhookURL);
                }
            }
            else
            {
                if (_configData.EasterSettings.Enabled)
                {
                    if (_configData.GlobalSettings.LoggingEnabled)
                    {
                        Puts("Egg Hunt Event has ended. The winner is " + winner);
                    }

                    SendMessage(Lang("EasterWinner", null, winner), _configData.EasterSettings.WebhookURL);
                }
            }
        }

        private void OnExplosiveThrown(BasePlayer player, SupplySignal entity)
        {
            HandleSupplySignal(player, entity);
        }

        private void OnExplosiveDropped(BasePlayer player, SupplySignal entity)
        {
            HandleSupplySignal(player, entity);
        }

        private void OnSupplyDropLanded(SupplyDrop entity)
        {
            if (entity == null || IsEntityInList(entity.net.ID))
            {
                return;
            }

            if (_configData.SupplyDropSettings.Enabled)
            {
                if (_configData.GlobalSettings.LoggingEnabled)
                {
                    Puts("SupplyDrop landed at " + GetGridPosition(entity.transform.position));
                }

                SendMessage(Lang("SupplyDropLanded", null, GetGridPosition(entity.transform.position)), _configData.SupplyDropSettings.WebhookURL);
                _lastEntities.Add(entity.net.ID, Time.realtimeSinceStartup + 60);
            }
        }

        private void OnDuelistDefeated(BasePlayer attacker, BasePlayer victim)
        {
            if (attacker == null || victim == null)
            {
                return;
            }

            if (_configData.DuelSettings.Enabled)
            {
                if (_configData.GlobalSettings.LoggingEnabled)
                {
                    Puts($"{attacker.displayName.Replace("*", "＊")} has defeated {victim.displayName.Replace("*", "＊")} in a duel");
                }

                SendMessage(Lang("Duel", null, attacker.displayName.Replace("*", "＊"), victim.displayName.Replace("*", "＊")), _configData.DuelSettings.WebhookURL);
            }
        }

        private void OnRaidableBaseStarted(Vector3 raidPos, int difficulty)
        {
            HandleRaidableBase(raidPos, difficulty, "RaidableBaseStarted");
        }
        private void OnRaidableBaseEnded(Vector3 raidPos, int difficulty)
        {
            HandleRaidableBase(raidPos, difficulty, "RaidableBaseEnded");
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (_configData.PlayerConnectedSettings.Enabled)
            {
                if (_configData.GlobalSettings.LoggingEnabled)
                {
                    Puts($"Player {player.displayName.Replace("*", "＊")} connected.");
                }

                SendMessage(Lang("PlayerConnected", null, player.displayName.Replace("*", "＊")), _configData.PlayerConnectedSettings.WebhookURL);
            }

            if (_configData.PlayerConnectedInfoSettings.Enabled)
            {
                SendMessage(Lang("PlayerConnectedInfo", null, player.displayName.Replace("*", "＊"), player.UserIDString, player.net.connection.ipaddress.Split(':')[0]), _configData.PlayerConnectedInfoSettings.WebhookURL);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null)
            {
                return;
            }

            if (_configData.PlayerDisconnectedSettings.Enabled)
            {
                if (_configData.GlobalSettings.LoggingEnabled)
                {
                    Puts($"Player {player.displayName.Replace("*", "＊")} disconnected ({reason}).");
                }

                SendMessage(Lang("PlayerDisconnected", null, player.displayName.Replace("*", "＊"), reason), _configData.PlayerConnectedSettings.WebhookURL);
            }
        }

        private void OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)
        {
            if (player == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (BetterChatMute != null && BetterChatMute.IsLoaded)
            {
                if (BetterChatMute.Call<bool>("API_IsMuted", player.IPlayer))
                {
                    return;
                }
            }

            message = message.Replace("@here", "here").Replace("@everyone", "everyone").Replace("*", "＊");

            if (channel == ConVar.Chat.ChatChannel.Global && _configData.ChatSettings.Enabled)
            {
                SendMessage(Lang("Chat", null, player.displayName.Replace("*", "＊"), message), _configData.ChatSettings.WebhookURL);
            }

            if (channel == ConVar.Chat.ChatChannel.Team && _configData.ChatTeamSettings.Enabled)
            {
                SendMessage(Lang("ChatTeam", null, player.displayName.Replace("*", "＊"), message), _configData.ChatTeamSettings.WebhookURL);
            }
        }

        private void OnDangerousEventStarted(Vector3 containerPos)
        {
            HandleDangerousTreasures(containerPos, "DangerousTreasuresStarted");
        }
        private void OnDangerousEventEnded(Vector3 containerPos)
        {
            HandleDangerousTreasures(containerPos, "DangerousTreasuresEnded");
        }

        #endregion Events Hooks

        #region Methods

        private void SendMessage(string message, string webhookUrl)
        {

            if (string.IsNullOrWhiteSpace(message))
            {
                PrintError("SendMessage: message is null or empty!");
                return;
            }

            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                PrintError("SendMessage: webhookUrl is null or empty!");
                return;
            }

            DiscordMessage discordMessage = new DiscordMessage(message);

            _queueMessages.Add(discordMessage, webhookUrl);

            HandleQueue();
        }

        private void HandleQueue()
        {

            if (_queueProcessed.Count > 0)
            {
                return;
            }

            _queueProcessed = _queueMessages;
            _queueMessages = new Dictionary<DiscordMessage, string> ();

            timer.Repeat(1f, _queueProcessed.Count, () =>
            {

                foreach(KeyValuePair<DiscordMessage, string> message in _queueProcessed)
                {
                    SendDiscordMessage(message.Value, message.Key);
                    _queueProcessed.Remove(message.Key);
                    break;
                }

                if (_queueMessages.Count > 0 && _queueProcessed.Count == 0)
                {
                    HandleQueue();
                }
            });
        }

        private void HandleEntity(BaseEntity baseEntity)
        {
            if (baseEntity == null)
            {
                return;
            }

            EventType eventType = GetEventTypeFromEntity(baseEntity);
            if (eventType == EventType.None)
            {
                PrintError("HandleEntity: eventType == EventType.None ->" + baseEntity.ShortPrefabName);
                return;
            }

            var eventSettengs = GetEventSettings(eventType);
            if (eventSettengs == null)
            {
                PrintError("HandleEntity: eventSettengs == null");
                return;
            }

            if (eventSettengs.Enabled)
            {
                if (_configData.GlobalSettings.LoggingEnabled)
                {
                    Puts(eventType.ToString());
                }

                if (eventType == EventType.Helicopter)
                {
                    if (PersonalHeli != null && PersonalHeli.IsLoaded)
                    {
                        if (PersonalHeli.Call<bool>("IsPersonal", baseEntity))
                        {
                            if (_configData.GlobalSettings.LoggingEnabled)
                            {
                                Puts("Personal Helicopter spawned at " + GetGridPosition(baseEntity.transform.position));
                            }

                            SendMessage(Lang("PersonalHelicopter", null, GetGridPosition(baseEntity.transform.position)), eventSettengs.WebhookURL);
                            return;
                        }
                    }
                }

                SendMessage(Lang(eventType.ToString(), null, GetGridPosition(baseEntity.transform.position)), eventSettengs.WebhookURL);
            }
        }

        private void HandleSupplySignal(BasePlayer player, SupplySignal entity)
        {
            if (_configData.SupplyDropSettings.Enabled)
            {
                NextTick(() =>
                {
                    if (player != null && entity != null)
                    {
                        if (_configData.GlobalSettings.LoggingEnabled)
                        {
                            Puts($"SupplySignal was thrown by {player.displayName.Replace("*", "＊")} at {GetGridPosition(entity.transform.position)}");
                        }

                        SendMessage(Lang("SupplySignal", null, player.displayName.Replace("*", "＊"), GetGridPosition(entity.transform.position)), _configData.SupplyDropSettings.WebhookURL);
                    }
                });
            }
        }

        private void HandleRaidableBase(Vector3 raidPos, int difficulty, string langKey)
        {
            if (raidPos == null)
            {
                PrintError($"{langKey}: raidPos == null");
                return;
            }

            if (_configData.RaidableBasesSettings.Enabled)
            {
                string difficultyString;
                switch (difficulty)
                {
                    case 0:
                        difficultyString = "Easy";
                        break;
                    case 1:
                        difficultyString = "Medium";
                        break;
                    case 2:
                        difficultyString = "Hard";
                        break;
                    case 3:
                        difficultyString = "Expert";
                        break;
                    case 4:
                        difficultyString = "Nightmare";
                        break;
                    default:
                        PrintError($"{langKey}: Unknown difficulty: {difficulty}");
                        return;
                }

                if (_configData.GlobalSettings.LoggingEnabled)
                {
                    Puts(difficultyString + " Raidable Base at " + GetGridPosition(raidPos) + " is " + (langKey == "RaidableBaseStarted" ? "spawned" : "ended"));
                }

                SendMessage(Lang(langKey, null, GetGridPosition(raidPos), Lang(difficultyString)), _configData.RaidableBasesSettings.WebhookURL);
            }
        }

        private void HandleDangerousTreasures(Vector3 containerPos, string langKey)
        {
            if (containerPos == null)
            {
                PrintError($"{langKey}: containerPos == null");
                return;
            }

            if (_configData.DangerousTreasuresSettings.Enabled)
            {
                if (_configData.GlobalSettings.LoggingEnabled)
                {
                    Puts("Dangerous Treasures at " + GetGridPosition(containerPos) + " is " + (langKey == "DangerousTreasuresStarted" ? "spawned" : "ended"));
                }

                SendMessage(Lang(langKey, null, GetGridPosition(containerPos)), _configData.DangerousTreasuresSettings.WebhookURL);
            }
        }

        #endregion Methods

        #region Helpers

        private void UnsubscribeDisabled()
        {
            if (!_configData.DangerousTreasuresSettings.Enabled)
            {
                Unsubscribe(nameof(OnDangerousEventEnded));
                Unsubscribe(nameof(OnDangerousEventStarted));
            }

            if (!_configData.PlayerDeathNotesSettings.Enabled)
            {
                Unsubscribe(nameof(OnDeathNotice));
            }

            if (!_configData.PlayerDeathSettings.Enabled)
            {
                Unsubscribe(nameof(OnEntityDeath));
            }

            if (!_configData.EasterSettings.Enabled &&
                !_configData.HalloweenSettings.Enabled)
            {
                Unsubscribe(nameof(OnEntityKill));
            }

            if (!_configData.BradleySettings.Enabled &&
                !_configData.CargoPlaneSettings.Enabled &&
                !_configData.CargoShipSettings.Enabled &&
                !_configData.ChinookSettings.Enabled &&
                !_configData.ChristmasSettings.Enabled &&
                !_configData.EasterSettings.Enabled &&
                !_configData.HalloweenSettings.Enabled &&
                !_configData.HelicopterSettings.Enabled &&
                !_configData.LockedCrateSettings.Enabled &&
                !_configData.SantaSleighSettings.Enabled &&
                !_configData.SupplyDropSettings.Enabled)
            {
                Unsubscribe(nameof(OnEntitySpawned));
            }

            if (!_configData.SupplyDropSettings.Enabled)
            {
                Unsubscribe(nameof(OnExplosiveDropped));
                Unsubscribe(nameof(OnExplosiveThrown));
                Unsubscribe(nameof(OnSupplyDropLanded));
            }

            if (!_configData.PlayerConnectedSettings.Enabled &&
                !_configData.PlayerConnectedInfoSettings.Enabled)
            {
                Unsubscribe(nameof(OnPlayerConnected));
            }

            if (!_configData.ChatSettings.Enabled &&
                !_configData.ChatTeamSettings.Enabled)
            {
                Unsubscribe(nameof(OnPlayerChat));
            }

            if (!_configData.PlayerDisconnectedSettings.Enabled)
            {
                Unsubscribe(nameof(OnPlayerDisconnected));
            }

            if (!_configData.RaidableBasesSettings.Enabled)
            {
                Unsubscribe(nameof(OnRaidableBaseEnded));
                Unsubscribe(nameof(OnRaidableBaseStarted));
            }
        }

        private static EventType GetEventTypeFromEntity(BaseEntity baseEntity)
        {
            if (baseEntity is BaseHelicopter) return EventType.Helicopter;
            if (baseEntity is BradleyAPC) return EventType.Bradley;
            if (baseEntity is CargoPlane) return EventType.CargoPlane;
            if (baseEntity is CargoShip) return EventType.CargoShip;
            if (baseEntity is HackableLockedCrate) return EventType.LockedCrate;
            if (baseEntity is SupplyDrop) return EventType.SupplyDrop;
            if (baseEntity is SupplySignal) return EventType.SupplyDrop;
            if (baseEntity is CH47HelicopterAIController) return EventType.Chinook;
            if (baseEntity is SantaSleigh) return EventType.SantaSleigh;
            if (baseEntity is HalloweenHunt) return EventType.Halloween;
            if (baseEntity is EggHuntEvent) return EventType.Easter;
            if (baseEntity is XMasRefill) return EventType.Christmas;

            return EventType.None;
        }

        private EventSettings GetEventSettings(EventType eventType)
        {
            switch (eventType)
            {
                case EventType.Bradley: return _configData.BradleySettings;
                case EventType.CargoPlane: return _configData.CargoPlaneSettings;
                case EventType.CargoShip: return _configData.CargoShipSettings;
                case EventType.Chinook: return _configData.ChinookSettings;
                case EventType.Christmas: return _configData.ChristmasSettings;
                case EventType.DangerousTreasures: return _configData.DangerousTreasuresSettings;
                case EventType.Death: return _configData.PlayerDeathSettings;
                case EventType.DeathNotes: return _configData.PlayerDeathNotesSettings;
                case EventType.Easter: return _configData.EasterSettings;
                case EventType.Halloween: return _configData.HalloweenSettings;
                case EventType.Helicopter: return _configData.HelicopterSettings;
                case EventType.LockedCrate: return _configData.LockedCrateSettings;
                case EventType.PlayerConnected: return _configData.PlayerConnectedSettings;
                case EventType.PlayerConnectedInfo: return _configData.PlayerConnectedInfoSettings;
                case EventType.PlayerDisconnected: return _configData.PlayerDisconnectedSettings;
                case EventType.RaidableBases: return _configData.RaidableBasesSettings;
                case EventType.SantaSleigh: return _configData.SantaSleighSettings;
                case EventType.SupplyDrop: return _configData.SupplyDropSettings;
                default:
                    PrintError($"GetEventSettings: Unknown EventType: {eventType}");
                    return null;
            }
        }

        private bool IsEntityInList(uint networkId)
        {
            if (_lastEntities != null)
            {
                Dictionary<uint, float> actualized = new Dictionary<uint, float> ();

                foreach (var entity in _lastEntities)
                {
                    if (entity.Value > Time.realtimeSinceStartup)
                    {
                        actualized.Add(entity.Key, entity.Value);
                    }
                }

                _lastEntities = actualized;

                if (_lastEntities.ContainsKey(networkId))
                {
                    return true;
                }
            }

            return false;
        }

        private string StripRustTags(string original)
        {
            if (string.IsNullOrEmpty(original))
            {
                return string.Empty;
            }

            foreach (string tag in _tags)
            {
                original = original.Replace(tag, "`");
            }

            foreach (Regex regexTag in _regexTags)
            {
                original = regexTag.Replace(original, "`");
            }

            return original;
        }

        private string GetGridPosition(Vector3 pos)
        {
            const float gridCellSize = 146.3f;

            int maxGridSize = Mathf.FloorToInt(World.Size / gridCellSize) - 1;
            float halfWorldSize = World.Size / 2f;
            int xGrid = Mathf.Clamp(Mathf.FloorToInt((pos.x + halfWorldSize) / gridCellSize),0, maxGridSize);
            int zGrid = Mathf.Clamp(maxGridSize - Mathf.FloorToInt((pos.z + halfWorldSize) / gridCellSize),0, maxGridSize);

            string extraA = string.Empty;
            if (xGrid > 26)
            {
                extraA = $"{(char) ('A' + (xGrid / 26 - 1))}";
            }

            return $"{extraA}{(char) ('A' + xGrid % 26)}{zGrid.ToString()}";
        }

        #endregion Helpers

        #region Discord Embed

        #region Send Embed Methods
        /// <summary>
        /// Headers when sending an embeded message
        /// </summary>
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>()
        {
            {"Content-Type", "application/json"}
        };

        /// <summary>
        /// Sends the DiscordMessage to the specified webhook url
        /// </summary>
        /// <param name="url">Webhook url</param>
        /// <param name="message">Message being sent</param>
        private void SendDiscordMessage(string url, DiscordMessage message)
        {
            string json = message.ToJson();
            webrequest.Enqueue(url, json, SendDiscordMessageCallback, this, RequestMethod.POST, _headers);
        }

        /// <summary>
        /// Callback when sending the embed if any errors occured
        /// </summary>
        /// <param name="code">HTTP response code</param>
        /// <param name="message">Response message</param>
        private void SendDiscordMessageCallback(int code, string message)
        {
            if (code != 204)
            {
                PrintError(message);
            }
        }
        #endregion Send Embed Methods

        #region Embed Classes

        private class DiscordMessage
        {
            /// <summary>
            /// The name of the user sending the message changing this will change the webhook bots name
            /// </summary>
            [JsonProperty("username")]
            private string Username { get; set; }

            /// <summary>
            /// The avatar url of the user sending the message changing this will change the webhook bots avatar
            /// </summary>
            [JsonProperty("avatar_url")]
            private string AvatarUrl { get; set; }

            /// <summary>
            /// String only content to be sent
            /// </summary>
            [JsonProperty("content")]
            private string Content { get; set; }

            public DiscordMessage(string content, string username = null, string avatarUrl = null)
            {
                Content = content;
                Username = username;
                AvatarUrl = avatarUrl;
            }

            /// <summary>
            /// Adds string content to the message
            /// </summary>
            /// <param name="content"></param>
            /// <returns></returns>
            public DiscordMessage AddContent(string content)
            {
                Content = content;
                return this;
            }

            /// <summary>
            /// Changes the username and avatar image for the bot sending the message
            /// </summary>
            /// <param name="username">username to change</param>
            /// <param name="avatarUrl">avatar img url to change</param>
            /// <returns>This</returns>
            public DiscordMessage AddSender(string username, string avatarUrl)
            {
                Username = username;
                AvatarUrl = avatarUrl;
                return this;
            }

            /// <summary>
            /// Returns message as JSON to be sent in the web request
            /// </summary>
            /// <returns></returns>
            public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None,
                new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore});
        }
        #endregion Embed Classes

        #endregion Discord Embed
    }
}