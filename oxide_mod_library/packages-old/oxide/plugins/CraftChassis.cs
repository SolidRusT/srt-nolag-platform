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
    [Info("Craft Car Chassis", "WhiteThunder", "1.2.2")]
    [Description("Allows players to craft a modular car chassis at a car lift using a UI.")]
    internal class CraftChassis : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin Economics, ServerRewards;

        private static CraftChassis _pluginInstance;

        private Configuration _pluginConfig;

        private const string PermissionCraft2 = "craftchassis.2";
        private const string PermissionCraft3 = "craftchassis.3";
        private const string PermissionCraft4 = "craftchassis.4";
        private const string PermissionFree = "craftchassis.free";
        private const string PermissionFuel = "craftchassis.fuel";

        private const string ChassisPrefab2 = "assets/content/vehicles/modularcar/car_chassis_2module.entity.prefab";
        private const string ChassisPrefab3 = "assets/content/vehicles/modularcar/car_chassis_3module.entity.prefab";
        private const string ChassisPrefab4 = "assets/content/vehicles/modularcar/car_chassis_4module.entity.prefab";
        private const string SpawnEffect = "assets/bundled/prefabs/fx/build/promote_toptier.prefab";

        private readonly Dictionary<BasePlayer, ModularCarGarage> playerLifts = new Dictionary<BasePlayer, ModularCarGarage>();
        private readonly ChassisUIManager uiManager = new ChassisUIManager();

        private enum CurrencyType { Items, Economics, ServerRewards }

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginInstance = this;
            _pluginConfig = Config.ReadObject<Configuration>();

            permission.RegisterPermission(PermissionCraft2, this);
            permission.RegisterPermission(PermissionCraft3, this);
            permission.RegisterPermission(PermissionCraft4, this);
            permission.RegisterPermission(PermissionFree, this);
            permission.RegisterPermission(PermissionFuel, this);
        }

        private void Unload()
        {
            uiManager.DestroyAllUIs();
            _pluginInstance = null;
        }

        private void OnLootEntity(BasePlayer player, ModularCarGarage carLift)
        {
            if (carLift == null)
                return;

            if (carLift.carOccupant == null)
            {
                playerLifts.Add(player, carLift);
                uiManager.MaybeSendPlayerUI(player);
            }
            else
            {
                uiManager.DestroyPlayerUI(player);
            }
        }

        private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            var player = inventory.baseEntity;
            if (player == null)
                return;

            playerLifts.Remove(player);
            uiManager.DestroyPlayerUI(player);
        }

        #endregion

        #region Commands

        [Command("craftchassis.ui")]
        private void CraftChassisUICommand(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer || args.Length < 1)
                return;

            int numSockets;
            if (!int.TryParse(args[0], out numSockets))
                return;

            var maxAllowedSockets = GetMaxAllowedSockets(player);
            if (numSockets < 2 || numSockets > maxAllowedSockets)
                return;

            ChassisCost chassisCost;
            if (!CanPlayerCreateChassis(player, numSockets, out chassisCost))
                return;

            var basePlayer = player.Object as BasePlayer;
            ModularCarGarage carLift;
            if (!playerLifts.TryGetValue(basePlayer, out carLift) || carLift.carOccupant != null)
                return;

            var car = SpawnChassis(carLift, numSockets, basePlayer);
            if (car == null)
                return;

            if (_pluginConfig.EnableEffects)
                Effect.server.Run(SpawnEffect, car.transform.position);

            if (chassisCost != null)
                ChargePlayer(basePlayer, chassisCost);
        }

        #endregion

        #region Helper Methods

        private ModularCar SpawnChassis(ModularCarGarage carLift, int numSockets, BasePlayer player)
        {
            var prefab = GetChassisPrefab(numSockets);

            var position = carLift.GetNetworkPosition() + Vector3.up * 0.7f;
            var rotation = Quaternion.Euler(0, carLift.GetNetworkRotation().eulerAngles.y - 90, 0);

            var car = GameManager.server.CreateEntity(prefab, position, rotation) as ModularCar;
            if (car == null)
                return null;

            if (_pluginConfig.SetOwner)
                car.OwnerID = player.userID;

            car.Spawn();
            AddOrRestoreFuel(car, player);

            return car;
        }

        private void AddOrRestoreFuel(ModularCar car, BasePlayer player)
        {
            var desiredFuelAmount = _pluginConfig.FuelAmount;
            if (desiredFuelAmount == 0 || !permission.UserHasPermission(player.UserIDString, PermissionFuel))
                return;

            var fuelContainer = car.GetFuelSystem().GetFuelContainer();
            if (desiredFuelAmount < 0)
                desiredFuelAmount = fuelContainer.allowedItem.stackable;

            var fuelItem = fuelContainer.inventory.FindItemByItemID(fuelContainer.allowedItem.itemid);
            if (fuelItem == null)
            {
                fuelContainer.inventory.AddItem(fuelContainer.allowedItem, desiredFuelAmount);
            }
            else if (fuelItem.amount < desiredFuelAmount)
            {
                fuelItem.amount = desiredFuelAmount;
                fuelItem.MarkDirty();
            }
        }

        private string GetChassisPrefab(int numSockets)
        {
            if (numSockets == 4)
                return ChassisPrefab4;
            else if (numSockets == 3)
                return ChassisPrefab3;
            else
                return ChassisPrefab2;
        }

        private int GetMaxAllowedSockets(IPlayer player)
        {
            if (player.HasPermission(PermissionCraft4))
                return 4;
            else if (player.HasPermission(PermissionCraft3))
                return 3;
            else if (player.HasPermission(PermissionCraft2))
                return 2;
            else
                return 0;
        }

        private bool CanPlayerCreateChassis(IPlayer player, int numSockets, out ChassisCost chassisCost)
        {
            chassisCost = null;
            if (player.HasPermission(PermissionFree))
                return true;

            chassisCost = GetCostForSockets(numSockets);
            return CanPlayerAffordCost(player.Object as BasePlayer, chassisCost);
        }

        private bool CanPlayerAffordSockets(BasePlayer basePlayer, int sockets) =>
            CanPlayerAffordCost(basePlayer, GetCostForSockets(sockets));

        private bool CanPlayerAffordCost(BasePlayer basePlayer, ChassisCost chassisCost)
        {
            CurrencyType currencyType;
            return chassisCost.amount == 0 || GetPlayerCurrencyAmount(basePlayer, chassisCost, out currencyType) >= chassisCost.amount;
        }

        private void ChargePlayer(BasePlayer basePlayer, ChassisCost chassisCost)
        {
            if (chassisCost.amount == 0)
                return;

            if (chassisCost.useEconomics && Economics != null)
            {
                Economics.Call("Withdraw", basePlayer.userID, Convert.ToDouble(chassisCost.amount));
                return;
            }

            if (chassisCost.useServerRewards && ServerRewards != null)
            {
                ServerRewards.Call("TakePoints", basePlayer.userID, chassisCost.amount);
                return;
            }

            var itemid = ItemManager.itemDictionaryByName[chassisCost.itemShortName].itemid;
            basePlayer.inventory.Take(null, itemid, chassisCost.amount);
            basePlayer.Command("note.inv", itemid, -chassisCost.amount);
        }

        private double GetPlayerCurrencyAmount(BasePlayer basePlayer, ChassisCost chassisCost, out CurrencyType currencyType)
        {
            if (chassisCost.useEconomics && Economics != null)
            {
                var balance = Economics.Call("Balance", basePlayer.userID);
                currencyType = CurrencyType.Economics;
                return balance is double ? (double)balance : 0;
            }

            if (chassisCost.useServerRewards && ServerRewards != null)
            {
                var points = ServerRewards.Call("CheckPoints", basePlayer.userID);
                currencyType = CurrencyType.ServerRewards;
                return points is int ? (int)points : 0;
            }

            currencyType = CurrencyType.Items;
            return basePlayer.inventory.GetAmount(ItemManager.itemDictionaryByName[chassisCost.itemShortName].itemid);
        }

        private ChassisCost GetCostForSockets(int numSockets)
        {
            if (numSockets == 4)
                return _pluginConfig.ChassisCostMap.ChassisCost4;
            if (numSockets == 3)
                return _pluginConfig.ChassisCostMap.ChassisCost3;
            else
                return _pluginConfig.ChassisCostMap.ChassisCost2;
        }

        #endregion

        #region UI

        internal class ChassisUIManager
        {
            private const string PanelBackgroundColor = "1 0.96 0.88 0.15";
            private const string TextColor = "0.97 0.92 0.88 1";
            private const string DisabledLabelTextColor = "0.75 0.42 0.14 1";
            private const string ButtonColor = "0.44 0.54 0.26 1";
            private const string DisabledButtonColor = "0.25 0.32 0.19 0.7";

            private const string CraftChassisUIName = "CraftChassis";
            private const string CraftChassisUIHeaderName = "CraftChassis.Header";

            private readonly List<BasePlayer> PlayersWithUIs = new List<BasePlayer>();

            public void DestroyAllUIs()
            {
                var playerList = new BasePlayer[PlayersWithUIs.Count];
                PlayersWithUIs.CopyTo(playerList, 0);

                foreach (var player in playerList)
                    DestroyPlayerUI(player);
            }

            public void DestroyPlayerUI(BasePlayer player)
            {
                if (PlayersWithUIs.Contains(player))
                {
                    CuiHelper.DestroyUi(player, CraftChassisUIName);
                    PlayersWithUIs.Remove(player);
                }
            }

            private CuiLabel CreateCostLabel(BasePlayer player, bool freeCrafting, int maxAllowedSockets, int numSockets)
            {
                var freeLabel = _pluginInstance.GetMessage(player.IPlayer, "UI.CostLabel.Free");

                string text = freeLabel;
                string color = TextColor;

                if (numSockets > maxAllowedSockets)
                {
                    text = _pluginInstance.GetMessage(player.IPlayer, "UI.CostLabel.NoPermission");
                    color = DisabledLabelTextColor;
                }
                else if (!freeCrafting)
                {
                    var chassisCost = _pluginInstance.GetCostForSockets(numSockets);
                    if (chassisCost.amount > 0)
                    {
                        CurrencyType currencyType;
                        var playerCurrencyAmount = _pluginInstance.GetPlayerCurrencyAmount(player, chassisCost, out currencyType);

                        switch (currencyType)
                        {
                            case CurrencyType.Economics:
                                text = _pluginInstance.GetMessage(player.IPlayer, "UI.CostLabel.Economics", chassisCost.amount);
                                break;
                            case CurrencyType.ServerRewards:
                                text = _pluginInstance.GetMessage(player.IPlayer, "UI.CostLabel.ServerRewards", chassisCost.amount);
                                break;
                            default:
                                var itemDefinition = ItemManager.itemDictionaryByName[chassisCost.itemShortName];
                                text = $"{chassisCost.amount} {itemDefinition.displayName.translated}";
                                break;
                        }

                        if (playerCurrencyAmount < chassisCost.amount)
                            color = DisabledLabelTextColor;
                    }
                }

                int offsetMinX = 8 + (numSockets - 2) * 124;
                int offsetMaxX = 124 + (numSockets - 2) * 124;
                int offsetMinY = 43;
                int offsetMaxY = 58;

                return new CuiLabel
                {
                    Text =
                    {
                        Text = text,
                        Color = color,
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 11,
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = $"{offsetMinX} {offsetMinY}",
                        OffsetMax = $"{offsetMaxX} {offsetMaxY}"
                    }
                };
            }

            private CuiButton CreateCraftButton(BasePlayer player, bool freeCrafting, int maxAllowedSockets, int numSockets)
            {
                var color = ButtonColor;

                if (numSockets > maxAllowedSockets || !freeCrafting && !_pluginInstance.CanPlayerAffordSockets(player, numSockets))
                    color = DisabledButtonColor;

                int offsetMinX = 8 + (numSockets - 2) * 124;
                int offsetMaxX = 124 + (numSockets - 2) * 124;
                int offsetMinY = 8;
                int offsetMaxY = 40;

                return new CuiButton
                {
                    Text = {
                        Text = _pluginInstance.GetMessage(player.IPlayer, $"UI.ButtonText.Sockets.{numSockets}"),
                        Color = TextColor,
                        Align = TextAnchor.MiddleCenter
                    },
                    Button =
                    {
                        Color = color,
                        Command = $"craftchassis.ui {numSockets}"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = $"{offsetMinX} {offsetMinY}",
                        OffsetMax = $"{offsetMaxX} {offsetMaxY}"
                    }
                };
            }

            public void MaybeSendPlayerUI(BasePlayer player)
            {
                if (PlayersWithUIs.Contains(player))
                    return;

                var maxAllowedSockets = _pluginInstance.GetMaxAllowedSockets(player.IPlayer);
                if (maxAllowedSockets == 0)
                    return;

                var freeCrafting = player.IPlayer.HasPermission(PermissionFree);

                var cuiElements = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image = new CuiImageComponent { Color = PanelBackgroundColor },
                            RectTransform =
                            {
                                AnchorMin = "0.5 0",
                                AnchorMax = "0.5 0",
                                OffsetMin = "192.5 431",
                                OffsetMax = "572.5 495",
                            }
                        },
                        "Hud.Menu",
                        CraftChassisUIName
                    },
                    {
                        new CuiPanel
                        {
                            Image = new CuiImageComponent { Color = PanelBackgroundColor },
                            RectTransform =
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = "0 3",
                                OffsetMax = "380 24"
                            }
                        },
                        CraftChassisUIName,
                        CraftChassisUIHeaderName
                    },
                    {
                        new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "10 0",
                                OffsetMax = "0 0"
                            },
                            Text =
                            {
                                Text = _pluginInstance.GetMessage(player.IPlayer, "UI.Header").ToUpperInvariant(),
                                Align = TextAnchor.MiddleLeft,
                                FontSize = 13
                            }
                        },
                        CraftChassisUIHeaderName
                    },
                    { CreateCostLabel(player, freeCrafting, maxAllowedSockets, 2), CraftChassisUIName },
                    { CreateCraftButton(player, freeCrafting, maxAllowedSockets, 2), CraftChassisUIName },
                    { CreateCostLabel(player, freeCrafting, maxAllowedSockets, 3), CraftChassisUIName },
                    { CreateCraftButton(player, freeCrafting, maxAllowedSockets, 3), CraftChassisUIName },
                    { CreateCostLabel(player, freeCrafting, maxAllowedSockets, 4), CraftChassisUIName },
                    { CreateCraftButton(player, freeCrafting, maxAllowedSockets, 4), CraftChassisUIName },
                };

                CuiHelper.AddUi(player, cuiElements);
                PlayersWithUIs.Add(player);
            }
        }

        #endregion

        #region Configuration

        private Configuration GetDefaultConfig() => new Configuration();

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("ChassisCost")]
            public ChassisCostMap ChassisCostMap = new ChassisCostMap();

            [JsonProperty("FuelAmount")]
            public int FuelAmount = 0;

            [JsonProperty("EnableEffects")]
            public bool EnableEffects = true;

            [JsonProperty("SetOwner")]
            public bool SetOwner = false;
        }

        private class ChassisCostMap
        {
            [JsonProperty("2sockets")]
            public ChassisCost ChassisCost2 = new ChassisCost
            {
                itemShortName = "metal.fragments",
                amount = 200,
            };

            [JsonProperty("3sockets")]
            public ChassisCost ChassisCost3 = new ChassisCost
            {
                itemShortName = "metal.fragments",
                amount = 300,
            };

            [JsonProperty("4sockets")]
            public ChassisCost ChassisCost4 = new ChassisCost
            {
                itemShortName = "metal.fragments",
                amount = 400,
            };
        }

        private class ChassisCost
        {
            [JsonProperty("Amount")]
            public int amount;

            [JsonProperty("ItemShortName")]
            public string itemShortName;

            [JsonProperty("UseEconomics", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool useEconomics = false;

            [JsonProperty("UseServerRewards", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool useServerRewards = false;
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
                ["UI.Header"] = "Craft a chassis",
                ["UI.CostLabel.Free"] = "Free",
                ["UI.CostLabel.NoPermission"] = "No Permission",
                ["UI.CostLabel.Economics"] = "{0:C}",
                ["UI.CostLabel.ServerRewards"] = "{0} reward points",
                ["UI.ButtonText.Sockets.2"] = "2 sockets",
                ["UI.ButtonText.Sockets.3"] = "3 sockets",
                ["UI.ButtonText.Sockets.4"] = "4 sockets",
            }, this, "en");
        }

        #endregion
    }
}
