using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Car Lock UI", "WhiteThunder", "1.0.2")]
    [Description("Adds a UI to add code locks to modular cars.")]
    internal class CarLockUI : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin VehicleDeployedLocks;

        private static CarLockUI _pluginInstance;
        private Configuration _pluginConfig;

        private const string PermissionUseCodeLock = "carlockui.use.codelock";

        private const int CodeLockItemId = 1159991980;

        private readonly CarLiftTracker _liftTracker = new CarLiftTracker();
        private CodeLockUIManager UIManager;

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginInstance = this;
            UIManager = new CodeLockUIManager(_pluginConfig.UISettings);

            permission.RegisterPermission(PermissionUseCodeLock, this);
        }

        private void OnServerInitialized()
        {
            if (VehicleDeployedLocks == null)
                LogError("VehicleDeployedLocks is not loaded, get it at https://umod.org");
        }

        private void Unload()
        {
            UIManager.DestroyAllUIs();
            _pluginInstance = null;
        }

        private void OnLootEntity(BasePlayer player, ModularCarGarage carLift)
        {
            _liftTracker.HandlePlayerLootCarLift(player, carLift);

            var car = carLift?.carOccupant;
            if (car == null)
                return;

            UIManager.UpdateCarUI(car);
        }

        private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            var player = inventory.GetComponent<BasePlayer>();
            if (player == null)
                return;

            _liftTracker.HandlePlayerLootEnd(player);
            UIManager.DestroyPlayerUI(player);
        }

        // Handle the case where a cockpit is added while a player is editing the car
        private void OnEntitySpawned(VehicleModuleSeating seatingModule)
        {
            if (seatingModule == null || !seatingModule.HasADriverSeat())
                return;

            NextTick(() =>
            {
                var car = seatingModule.Vehicle as ModularCar;
                if (car == null)
                    return;

                UIManager.UpdateCarUI(car);
            });
        }

        // Handle the case where a cockpit is removed but the car remains
        private void OnEntityKill(VehicleModuleSeating seatingModule)
        {
            if (seatingModule == null || !seatingModule.HasADriverSeat())
                return;

            var car = seatingModule.Vehicle as ModularCar;
            if (car == null)
                return;

            NextTick(() =>
            {
                if (car == null)
                    return;

                UIManager.UpdateCarUI(car);
            });
        }

        // Handle the case where the code lock is removed but the car and cockpit remain
        private void OnEntityKill(CodeLock codeLock)
        {
            if (codeLock == null)
                return;

            var seatingModule = codeLock.GetParentEntity() as VehicleModuleSeating;
            if (seatingModule == null)
                return;

            var car = seatingModule.Vehicle as ModularCar;
            NextTick(() =>
            {
                if (car == null)
                    return;

                UIManager.UpdateCarUI(car);
            });
        }

        private void OnVehicleLockDeployed(ModularCar car) =>
            UIManager.UpdateCarUI(car);

        #endregion

        #region Commands

        [Command("carlockui.deploy.codelock")]
        private void UICommandDeploy(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer || !player.HasPermission(PermissionUseCodeLock) || VehicleDeployedLocks == null)
                return;

            var basePlayer = player.Object as BasePlayer;
            var car = _liftTracker.GetCarPlayerIsLooting(basePlayer);
            if (car == null || !CanPlayerDeployCodeLock(basePlayer, car))
                return;

            VehicleDeployedLocks.Call("API_DeployCodeLock", car, basePlayer, false);
        }

        [Command("carlockui.remove.codelock")]
        private void UICommandRemove(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer || !player.HasPermission(PermissionUseCodeLock) || VehicleDeployedLocks == null)
                return;

            var basePlayer = player.Object as BasePlayer;
            var car = _liftTracker.GetCarPlayerIsLooting(basePlayer);
            if (car == null)
                return;

            var codeLock = GetCarLock(car) as CodeLock;
            if (codeLock == null)
                return;

            codeLock.Kill();

            var codeLockItem = ItemManager.CreateByItemID(CodeLockItemId);
            if (codeLockItem == null)
                return;

            basePlayer.GiveItem(codeLockItem);
        }

        #endregion

        #region Helper Methods

        private bool CanCarHaveLock(ModularCar car) =>
            FindFirstDriverModule(car) != null;

        private bool CanPlayerDeployCodeLock(BasePlayer player, ModularCar car) =>
            VehicleDeployedLocks != null && (bool)VehicleDeployedLocks.Call("API_CanPlayerDeployCodeLock", player, car);

        private BaseLock GetCarLock(ModularCar car) =>
            car.GetSlot(BaseEntity.Slot.Lock) as BaseLock;

        private VehicleModuleSeating FindFirstDriverModule(ModularCar car)
        {
            for (int socketIndex = 0; socketIndex < car.TotalSockets; socketIndex++)
            {
                BaseVehicleModule module;
                if (car.TryGetModuleAt(socketIndex, out module))
                {
                    var seatingModule = module as VehicleModuleSeating;
                    if (seatingModule != null && seatingModule.HasADriverSeat())
                        return seatingModule;
                }
            }
            return null;
        }

        #endregion

        #region Helper Classes

        private class CarLiftTracker
        {
            private readonly Dictionary<ModularCar, List<BasePlayer>> LootersOfCar = new Dictionary<ModularCar, List<BasePlayer>>();
            private readonly Dictionary<BasePlayer, ModularCar> LootingCar = new Dictionary<BasePlayer, ModularCar>();

            public ModularCar GetCarPlayerIsLooting(BasePlayer player) =>
                LootingCar.ContainsKey(player) ? LootingCar[player] : null;

            public List<BasePlayer> GetPlayersLootingCar(ModularCar car) =>
                LootersOfCar.ContainsKey(car) ? LootersOfCar[car] : new List<BasePlayer>();

            public void HandlePlayerLootCarLift(BasePlayer player, ModularCarGarage carLift)
            {
                var car = carLift?.carOccupant;
                if (car == null)
                    return;

                if (LootersOfCar.ContainsKey(car))
                    LootersOfCar[car].Add(player);
                else
                    LootersOfCar.Add(car, new List<BasePlayer> { player });

                if (LootingCar.ContainsKey(player))
                    LootingCar[player] = car;
                else
                    LootingCar.Add(player, car);
            }

            public void HandlePlayerLootEnd(BasePlayer player)
            {
                if (!LootingCar.ContainsKey(player))
                    return;

                var car = LootingCar[player];
                LootingCar.Remove(player);

                if (LootersOfCar.ContainsKey(car))
                {
                    LootersOfCar[car].Remove(player);
                    if (LootersOfCar[car].Count == 0)
                        LootersOfCar.Remove(car);
                }
            }
        }

        private class CodeLockUIManager
        {
            private enum UIState { AddLock, RemoveLock, None }

            private const string CodeLockUIName = "CarLockUI.AddRemoveLock";

            private readonly UISettings Settings;
            private readonly Dictionary<BasePlayer, UIState> PlayerUIStates = new Dictionary<BasePlayer, UIState>();

            public CodeLockUIManager(UISettings settings)
            {
                Settings = settings;
            }

            public void DestroyAllUIs()
            {
                var keys = PlayerUIStates.Keys;
                if (keys.Count == 0)
                    return;

                var playerList = new BasePlayer[keys.Count];
                keys.CopyTo(playerList, 0);

                foreach (var player in playerList)
                    DestroyPlayerUI(player);
            }

            public void UpdateCarUI(ModularCar car)
            {
                var looters = _pluginInstance._liftTracker.GetPlayersLootingCar(car);

                var currentLock = _pluginInstance.GetCarLock(car);
                if (!_pluginInstance.CanCarHaveLock(car) || currentLock is KeyLock)
                {
                    foreach (var player in looters)
                        DestroyPlayerUI(player);
                    return;
                }

                var uiState = currentLock == null ? UIState.AddLock : UIState.RemoveLock;
                foreach (var player in looters)
                    UpdatePlayerCarUI(player, car, uiState);
            }

            private void UpdatePlayerCarUI(BasePlayer player, ModularCar car, UIState desiredUIState)
            {
                UIState uiState = desiredUIState;

                if (!player.IPlayer.HasPermission(PermissionUseCodeLock) || player.IsBuildingBlocked())
                    uiState = UIState.None;

                if (uiState == UIState.AddLock && !_pluginInstance.CanPlayerDeployCodeLock(player, car))
                    uiState = UIState.None;

                UIState currentUIState;
                if (PlayerUIStates.TryGetValue(player, out currentUIState) && currentUIState == uiState)
                    return;

                DestroyPlayerUI(player);

                if (uiState != UIState.None)
                    SendPlayerUI(player, uiState);
            }

            public void DestroyPlayerUI(BasePlayer player)
            {
                if (PlayerUIStates.ContainsKey(player))
                {
                    CuiHelper.DestroyUi(player, CodeLockUIName);
                    PlayerUIStates.Remove(player);
                }
            }

            private void SendPlayerUI(BasePlayer player, UIState uiState)
            {
                var cuiElements = new CuiElementContainer
                {
                    {
                        new CuiButton
                        {
                            Text = {
                                Text = _pluginInstance.GetMessage(player.IPlayer, uiState == UIState.AddLock ? "UI.AddCodeLock" : "UI.RemoveCodeLock"),
                                Color = Settings.ButtonTextColor,
                                Align = TextAnchor.MiddleCenter,
                                FadeIn = 0.25f
                            },
                            Button =
                            {
                                Color = uiState == UIState.AddLock ? Settings.AddButtonColor : Settings.RemoveButtonColor,
                                Command = uiState == UIState.AddLock ? "carlockui.deploy.codelock" : "carlockui.remove.codelock"
                            },
                            RectTransform =
                            {
                                AnchorMin = Settings.AnchorMin,
                                AnchorMax = Settings.AnchorMax,
                                OffsetMin = Settings.OffsetMin,
                                OffsetMax = Settings.OffsetMax
                            }
                        },
                        "Hud.Menu",
                        CodeLockUIName
                    }
                };

                CuiHelper.AddUi(player, cuiElements);
                PlayerUIStates.Add(player, uiState);
            }
        }

        #endregion

        #region Configuration

        private Configuration GetDefaultConfig() => new Configuration();

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("UISettings")]
            public UISettings UISettings = new UISettings();
        }

        private class UISettings
        {
            [JsonProperty("AnchorMin")]
            public string AnchorMin = "0.5 0";

            [JsonProperty("AnchorMax")]
            public string AnchorMax = "0.5 0";

            [JsonProperty("OffsetMin")]
            public string OffsetMin = "385.5 349";

            [JsonProperty("OffsetMax")]
            public string OffsetMax = "572.5 377";

            [JsonProperty("AddButtonColor")]
            public string AddButtonColor = "0.44 0.54 0.26 1";

            [JsonProperty("RemoveButtonColor")]
            public string RemoveButtonColor = "0.7 0.3 0 1";

            [JsonProperty("ButtonTextColor")]
            public string ButtonTextColor = "0.97 0.92 0.88 1";
        }

        #endregion

        #region Configuration Boilerplate

        private class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _pluginConfig = Config.ReadObject<Configuration>();
                if (_pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_pluginConfig))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_pluginConfig, true);
        }

        #endregion

        #region Localization

        private string GetMessage(IPlayer player, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, player.Id);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI.AddCodeLock"] = "Add Code Lock",
                ["UI.RemoveCodeLock"] = "REMOVE Code Lock",
            }, this, "en");
        }

        #endregion
    }
}
