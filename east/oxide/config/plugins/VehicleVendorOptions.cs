using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Vehicle Vendor Options", "WhiteThunder", "1.2.0")]
    [Description("Allows vehicles spawned at vendors to have configurable fuel, properly assigned ownership, and to be free with permissions.")]
    internal class VehicleVendorOptions : CovalencePlugin
    {
        #region Fields

        private const string Permission_Ownership_All = "vehiclevendoroptions.ownership.allvehicles";
        private const string Permission_Ownership_MiniCopter = "vehiclevendoroptions.ownership.minicopter";
        private const string Permission_Ownership_ScrapHeli = "vehiclevendoroptions.ownership.scraptransport";
        private const string Permission_Ownership_Rowboat = "vehiclevendoroptions.ownership.rowboat";
        private const string Permission_Ownership_RHIB = "vehiclevendoroptions.ownership.rhib";
        private const string Permission_Ownership_RidableHorse = "vehiclevendoroptions.ownership.ridablehorse";

        private const string Permission_Free_All = "vehiclevendoroptions.free.allvehicles";
        private const string Permission_Free_RidableHorse = "vehiclevendoroptions.free.ridablehorse";

        private readonly DialogResponseConfig[] dialogResponseConfigs = new DialogResponseConfig[]
        {
            new DialogResponseConfig()
            {
                freePermission = "vehiclevendoroptions.free.minicopter",
                matchSpeechNode = "minicopterbuy",
                responseAction = "buyminicopter",
                successSpeechNode = "success"
            },
            new DialogResponseConfig()
            {
                freePermission = "vehiclevendoroptions.free.scraptransport",
                matchSpeechNode = "transportbuy",
                responseAction = "buytransport",
                successSpeechNode = "success"
            },
            new DialogResponseConfig()
            {
                freePermission = "vehiclevendoroptions.free.rowboat",
                matchSpeechNode = "pay_rowboat",
                responseAction = "buyboat",
                successSpeechNode = "buysuccess"
            },
            new DialogResponseConfig()
            {
                freePermission = "vehiclevendoroptions.free.rhib",
                matchSpeechNode = "pay_rhib",
                responseAction = "buyrhib",
                successSpeechNode = "buysuccess"
            },
        };

        internal class DialogResponseConfig
        {
            public string freePermission;
            public string matchSpeechNode;
            public string responseAction;
            public string successSpeechNode;
        }

        private Configuration pluginConfig;

        #endregion

        #region Hooks

        private void Init()
        {
            pluginConfig = Config.ReadObject<Configuration>();

            permission.RegisterPermission(Permission_Ownership_All, this);
            permission.RegisterPermission(Permission_Ownership_MiniCopter, this);
            permission.RegisterPermission(Permission_Ownership_ScrapHeli, this);
            permission.RegisterPermission(Permission_Ownership_Rowboat, this);
            permission.RegisterPermission(Permission_Ownership_RHIB, this);
            permission.RegisterPermission(Permission_Ownership_RidableHorse, this);

            permission.RegisterPermission(Permission_Free_All, this);
            permission.RegisterPermission(Permission_Free_RidableHorse, this);

            foreach (var responseConfig in dialogResponseConfigs)
                permission.RegisterPermission(responseConfig.freePermission, this);
        }

        private void OnEntitySpawned(MiniCopter vehicle) => HandleSpawn(vehicle);

        private void OnEntitySpawned(MotorRowboat vehicle) => HandleSpawn(vehicle);

        private object OnRidableAnimalClaim(RidableHorse horse, BasePlayer player)
        {
            if (!horse.IsForSale() || !HasPermissionAny(player.UserIDString, Permission_Free_All, Permission_Free_RidableHorse))
                return null;

            horse.SetFlag(BaseEntity.Flags.Reserved2, false);
            horse.AttemptMount(player, doMountChecks: false);
            Interface.CallHook("OnRidableAnimalClaimed", horse, player);
            return false;
        }

        private void OnRidableAnimalClaimed(RidableHorse horse, BasePlayer player) =>
            SetOwnerIfPermission(horse, player);

        private object OnNpcConversationRespond(NPCTalking npcTalking, BasePlayer player, ConversationData conversationData, ConversationData.ResponseNode responseNode)
        {
            if (!(npcTalking is VehicleVendor))
                return null;

            foreach (var responseConfig in dialogResponseConfigs)
            {
                if (responseNode.resultingSpeechNode != responseConfig.matchSpeechNode)
                    continue;

                if (!HasPermissionAny(player.UserIDString, Permission_Free_All, responseConfig.freePermission))
                    return null;

                if (!TryConversationAction(npcTalking, player, responseConfig.responseAction))
                    return null;

                AdvanceOrEndConveration(npcTalking, player, conversationData, responseConfig.successSpeechNode);
                return false;
            }

            return null;
        }

        #endregion

        #region Helper Methods

        private void HandleSpawn(BaseVehicle vehicle)
        {
            if (Rust.Application.isLoadingSave) return;

            NextTick(() =>
            {
                if (vehicle.creatorEntity == null) return;

                var vehicleConfig = GetVehicleConfig(vehicle);
                if (vehicleConfig == null) return;

                AdjustFuel(vehicle, vehicleConfig.FuelAmount);
                MaybeSetOwner(vehicle);
            });
        }

        private VehicleConfig GetVehicleConfig(BaseVehicle vehicle)
        {
            // Must go before MiniCopter
            if (vehicle is ScrapTransportHelicopter)
                return pluginConfig.Vehicles.ScrapTransport;

            if (vehicle is MiniCopter)
                return pluginConfig.Vehicles.Minicopter;

            // Must go before MotorRowboat
            if (vehicle is RHIB)
                return pluginConfig.Vehicles.RHIB;

            if (vehicle is MotorRowboat)
                return pluginConfig.Vehicles.Rowboat;

            return null;
        }

        private void AdjustFuel(BaseVehicle vehicle, int desiredFuelAmount)
        {
            var fuelSystem = vehicle.GetFuelSystem();
            if (fuelSystem == null) return;

            var fuelAmount = desiredFuelAmount < 0
                ? fuelSystem.GetFuelContainer().allowedItem.stackable
                : desiredFuelAmount;

            var fuelItem = fuelSystem.GetFuelItem();
            if (fuelItem != null && fuelItem.amount != fuelAmount)
            {
                fuelItem.amount = fuelAmount;
                fuelItem.MarkDirty();
            }
        }

        private void MaybeSetOwner(BaseVehicle vehicle)
        {
            var basePlayer = vehicle.creatorEntity as BasePlayer;
            if (basePlayer == null) return;

            SetOwnerIfPermission(vehicle, basePlayer);
        }

        private void SetOwnerIfPermission(BaseVehicle vehicle, BasePlayer basePlayer)
        {
            if (HasPermissionAny(basePlayer.IPlayer, Permission_Ownership_All, GetOwnershipPermission(vehicle)))
                vehicle.OwnerID = basePlayer.userID;
        }

        private bool HasPermissionAny(IPlayer player, params string[] permissionNames) =>
            HasPermissionAny(player.Id, permissionNames);

        private bool HasPermissionAny(string userIdString, params string[] permissionNames)
        {
            foreach (var perm in permissionNames)
                if (perm != null && permission.UserHasPermission(userIdString, perm))
                    return true;

            return false;
        }

        private string GetOwnershipPermission(BaseVehicle vehicle)
        {
            // Must go before MiniCopter
            if (vehicle is ScrapTransportHelicopter)
                return Permission_Ownership_ScrapHeli;

            if (vehicle is MiniCopter)
                return Permission_Ownership_MiniCopter;

            // Must go before MotorRowboat
            if (vehicle is RHIB)
                return Permission_Ownership_RHIB;

            if (vehicle is MotorRowboat)
                return Permission_Ownership_Rowboat;

            if (vehicle is RidableHorse)
                return Permission_Ownership_RidableHorse;

            return null;
        }

        private bool TryConversationAction(NPCTalking npcTalking, BasePlayer player, string action)
        {
            var resultAction = npcTalking.conversationResultActions.FirstOrDefault(result => result.action == action);
            if (resultAction == null)
                return false;

            // This re-implements game logic to kick out other conversing players when a vehicle spawns
            npcTalking.CleanupConversingPlayers();
            foreach (BasePlayer conversingPlayer in npcTalking.conversingPlayers)
            {
                if (conversingPlayer != player && conversingPlayer != null)
                {
                    int speechNodeIndex = npcTalking.GetConversationFor(player).GetSpeechNodeIndex("startbusy");
                    npcTalking.ForceSpeechNode(conversingPlayer, speechNodeIndex);
                }
            }

            // Spawn the vehicle
            npcTalking.lastActionPlayer = player;
            npcTalking.BroadcastEntityMessage(resultAction.broadcastMessage, resultAction.broadcastRange);
            npcTalking.lastActionPlayer = null;

            return true;
        }

        private void AdvanceOrEndConveration(NPCTalking npcTalking, BasePlayer player, ConversationData conversationData, string targetSpeechName)
        {
            var speechNodeIndex = conversationData.GetSpeechNodeIndex(targetSpeechName);
            if (speechNodeIndex == -1)
            {
                npcTalking.ForceEndConversation(player);
            }
            else
            {
                var speechNode = conversationData.speeches[speechNodeIndex];
                npcTalking.ForceSpeechNode(player, speechNodeIndex);
            }
        }

        #endregion

        #region Configuration

        internal class Configuration : SerializableConfiguration
        {
            [JsonProperty("Vehicles")]
            public VehicleConfigMap Vehicles = new VehicleConfigMap();
        }

        internal class VehicleConfigMap
        {
            [JsonProperty("Minicopter")]
            public VehicleConfig Minicopter = new VehicleConfig()
            {
                FuelAmount = 100
            };

            [JsonProperty("ScrapTransport")]
            public VehicleConfig ScrapTransport = new VehicleConfig()
            {
                FuelAmount = 100
            };

            [JsonProperty("Rowboat")]
            public VehicleConfig Rowboat = new VehicleConfig()
            {
                FuelAmount = 50
            };

            [JsonProperty("RHIB")]
            public VehicleConfig RHIB = new VehicleConfig()
            {
                FuelAmount = 50
            };
        }

        internal class VehicleConfig
        {
            [JsonProperty("FuelAmount")]
            public int FuelAmount = 100;
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #endregion

        #region Configuration Boilerplate

        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        internal static class JsonHelper
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

        protected override void LoadDefaultConfig() => pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                pluginConfig = Config.ReadObject<Configuration>();
                if (pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(pluginConfig))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(pluginConfig, true);
        }

        #endregion
    }
}
