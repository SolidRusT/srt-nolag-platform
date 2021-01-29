using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust.Modular;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text;

namespace Oxide.Plugins
{
    [Info("Spawn Modular Car", "WhiteThunder", "4.0.2")]
    [Description("Allows players to spawn modular cars.")]
    internal class SpawnModularCar : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin CarCodeLocks, VehicleDeployedLocks;

        private static SpawnModularCar pluginInstance;

        private PluginData pluginData;
        private PluginConfig pluginConfig;

        private const string DefaultPresetName = "default";
        private const int PresetMaxLength = 30;

        private const string PermissionSpawnSockets2 = "spawnmodularcar.spawn.2";
        private const string PermissionSpawnSockets3 = "spawnmodularcar.spawn.3";
        private const string PermissionSpawnSockets4 = "spawnmodularcar.spawn.4";
        
        private const string PermissionEnginePartsTier1 = "spawnmodularcar.engineparts.tier1";
        private const string PermissionEnginePartsTier2 = "spawnmodularcar.engineparts.tier2";
        private const string PermissionEnginePartsTier3 = "spawnmodularcar.engineparts.tier3";

        private const string PermissionFix = "spawnmodularcar.fix";
        private const string PermissionFetch = "spawnmodularcar.fetch";
        private const string PermissionDespawn = "spawnmodularcar.despawn";
        private const string PermissionAutoFuel = "spawnmodularcar.autofuel";
        private const string PermissionAutoCodeLock = "spawnmodularcar.autocodelock";
        private const string PermissionAutoKeyLock = "spawnmodularcar.autokeylock";
        private const string PermissionAutoStartEngine = "spawnmodularcar.autostartengine";
        private const string PermissionAutoFillTankers = "spawnmodularcar.autofilltankers";
        private const string PermissionGiveCar = "spawnmodularcar.givecar";

        private const string PermissionPresets = "spawnmodularcar.presets";
        private const string PermissionPresetLoad = "spawnmodularcar.presets.load";
        private const string PermissionCommonPresets = "spawnmodularcar.presets.common";
        private const string PermissionManageCommonPresets = "spawnmodularcar.presets.common.manage";

        private const string PrefabSockets2 = "assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab";
        private const string PrefabSockets3 = "assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab";
        private const string PrefabSockets4 = "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab";

        private const string ItemDropPrefab = "assets/prefabs/misc/item drop/item_drop.prefab";

        private const string RepairEffectPrefab = "assets/bundled/prefabs/fx/build/promote_toptier.prefab";
        private const string TankerFilledEffectPrefab = "assets/prefabs/food/water jug/effects/water-jug-fill-container.prefab";

        private readonly Dictionary<string, PlayerConfig> PlayerConfigsMap = new Dictionary<string, PlayerConfig>();

        private CooldownManager SpawnCarCooldowns;
        private CooldownManager FixCarCooldowns;
        private CooldownManager FetchCarCooldowns;
        private CooldownManager LoadPresetCooldowns;

        #endregion

        #region Hooks

        private void Init()
        {
            pluginInstance = this;

            pluginData = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            pluginConfig = Config.ReadObject<PluginConfig>();

            permission.RegisterPermission(PermissionSpawnSockets2, this);
            permission.RegisterPermission(PermissionSpawnSockets3, this);
            permission.RegisterPermission(PermissionSpawnSockets4, this);

            permission.RegisterPermission(PermissionEnginePartsTier1, this);
            permission.RegisterPermission(PermissionEnginePartsTier2, this);
            permission.RegisterPermission(PermissionEnginePartsTier3, this);

            permission.RegisterPermission(PermissionFix, this);
            permission.RegisterPermission(PermissionFetch, this);
            permission.RegisterPermission(PermissionDespawn, this);
            permission.RegisterPermission(PermissionAutoFuel, this);
            permission.RegisterPermission(PermissionAutoCodeLock, this);
            permission.RegisterPermission(PermissionAutoKeyLock, this);
            permission.RegisterPermission(PermissionAutoStartEngine, this);
            permission.RegisterPermission(PermissionAutoFillTankers, this);
            permission.RegisterPermission(PermissionGiveCar, this);

            permission.RegisterPermission(PermissionPresets, this);
            permission.RegisterPermission(PermissionPresetLoad, this);
            permission.RegisterPermission(PermissionCommonPresets, this);
            permission.RegisterPermission(PermissionManageCommonPresets, this);

            SpawnCarCooldowns = new CooldownManager(pluginConfig.Cooldowns.SpawnSeconds);
            FixCarCooldowns = new CooldownManager(pluginConfig.Cooldowns.FixSeconds);
            FetchCarCooldowns = new CooldownManager(pluginConfig.Cooldowns.FetchSeconds);
            LoadPresetCooldowns = new CooldownManager(pluginConfig.Cooldowns.LoadPresetSeconds);
        }

        private void Unload()
        {
            pluginInstance = null;
        }

        private void OnNewSave(string filename)
        {
            pluginData.playerCars.Clear();
            pluginData.SaveData();
        }

        private void OnEntityKill(ModularCar car)
        {
            if (!IsPlayerCar(car)) return;
            
            string userID = pluginData.playerCars.FirstOrDefault(x => x.Value == car.net.ID).Key;
            BasePlayer player = BasePlayer.Find(userID);

            if (player != null)
                ChatMessage(player, "Generic.Info.CarDestroyed");

            pluginData.UnregisterCar(userID);
        }

        private object OnEngineStart(ModularCar car)
        {
            if (car == null ||
                car.OwnerID == 0 ||
                !pluginData.playerCars.ContainsValue(car.net.ID) ||
                !permission.UserHasPermission(car.OwnerID.ToString(), PermissionAutoStartEngine))
                return null;

            car.FinishStartingEngine();
            return false;
        }

        #endregion

        #region API

        private ModularCar API_SpawnPresetCar(BasePlayer player, Dictionary<string, object> options, Action<ModularCar> onReady = null)
        {
            var apiOptions = new APIOptions(options);
            if (apiOptions.ModuleIDs == null)
            {
                LogError("[API_SpawnPresetCar] '{0}' field is missing or unrecognizable.", APIOptions.ModulesField);
                return null;
            }

            if (apiOptions.ModuleIDs.Length < 2 || apiOptions.ModuleIDs.Length > 4)
            {
                LogError("[API_SpawnPresetCar] Requested a car with {0} sockets, but only 2-4 sockets is supported.", apiOptions.ModuleIDs.Length);
                return null;
            }

            if (SpawnWasBlocked(player)) return null;

            var presetOptions = apiOptions.ToPresetOptions();
            return SpawnCarForPlayer(player, presetOptions, false, onReady);
        }

        internal class APIOptions
        {
            public static string CodeLockField = "CodeLock";
            public static string KeyLockField = "KeyLock";
            public static string EnginePartsTierField = "EnginePartsTier";
            public static string FreshWaterAmountField = "FreshWaterAmount";
            public static string FuelAmountField = "FuelAmount";
            public static string ModulesField = "Modules";

            public readonly bool CodeLock;
            public readonly bool KeyLock;
            public readonly int EnginePartsTier;
            public readonly int FreshWaterAmount;
            public readonly int FuelAmount;
            public readonly int[] ModuleIDs;

            public APIOptions(Dictionary<string, object> options)
            {
                CodeLock = BoolOption(options, CodeLockField);
                KeyLock = BoolOption(options, KeyLockField);
                EnginePartsTier = IntOption(options, EnginePartsTierField);
                FreshWaterAmount = IntOption(options, FreshWaterAmountField);
                FuelAmount = IntOption(options, FuelAmountField);
                ModuleIDs = ParseModulesOption(options);
            }

            private bool BoolOption(Dictionary<string, object> options, string name) =>
                options.ContainsKey(name) && options[name] is bool ? (bool)options[name] : false;

            private int IntOption(Dictionary<string, object> options, string name) =>
                options.ContainsKey(name) && options[name] is int ? (int)options[name] : 0;

            public int[] ParseModulesOption(Dictionary<string, object> options)
            {
                if (!options.ContainsKey(ModulesField)) return null;

                var moduleArray = options[ModulesField] as object[];
                if (moduleArray == null) return null;

                return pluginInstance.ParseModules(moduleArray);
            }

            public PresetCarOptions ToPresetOptions()
            {
                return new PresetCarOptions
                {
                    CodeLock = CodeLock,
                    KeyLock = KeyLock,
                    EnginePartsTier = EnginePartsTier,
                    FreshWaterAmount = FreshWaterAmount,
                    FuelAmount = FuelAmount,
                    ModuleIDs = ModuleIDs
                };
            }
        }

        #endregion

        #region Commands

        [Command("givecar")]
        private void SpawnCarServerCommand(IPlayer player, string cmd, string[] args)
        {
            if (!player.IsServer && !VerifyPermissionAny(player, PermissionGiveCar)) return;

            if (args.Length < 2)
            {
                ReplyToPlayer(player, "Command.Give.Error.Syntax");
                return;
            }

            var playerNameOrIdArg = args[0];
            var presetNameArg = args[1];

            var targetPlayer = BasePlayer.Find(playerNameOrIdArg);
            if (targetPlayer == null)
            {
                ReplyToPlayer(player, "Command.Give.Error.PlayerNotFound", playerNameOrIdArg);
                return;
            }

            foreach (var preset in pluginConfig.Presets)
            {
                if (preset.Name.ToLower() == presetNameArg.ToLower())
                {
                    var carOptions = preset.Options;
                    if (carOptions.Length < 2)
                    {
                        ReplyToPlayer(player, "Command.Give.Error.PresetTooFewModules", preset.Name, carOptions.Length);
                        return;
                    }
                    if (carOptions.Length > 4)
                    {
                        ReplyToPlayer(player, "Command.Give.Error.PresetTooManyModules", preset.Name, carOptions.Length);
                        return;
                    }
                    SpawnCarForPlayer(targetPlayer, carOptions, shouldTrackCar: false, onReady: car =>
                    {
                        ReplyToPlayer(player, "Command.Give.Success", targetPlayer.displayName, preset.Name);
                    });
                    return;
                }
            }

            ReplyToPlayer(player, "Generic.Error.PresetNotFound", presetNameArg);
        }

        [Command("mycar")]
        private void MyCarCommand(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer) return;

            if (args.Length == 0)
            {
                SubCommand_SpawnCar(player, args);
                return;
            }

            switch (args[0].ToLower())
            {
                case "help":
                    SubCommand_Help(player, args.Skip(1).ToArray());
                    return;

                case "list":
                    SubCommand_ListPresets(player, args.Skip(1).ToArray());
                    return;

                case "save":
                    SubCommand_SavePreset(player, args.Skip(1).ToArray());
                    return;

                case "update":
                    SubCommand_UpdatePreset(player, args.Skip(1).ToArray());
                    return;

                case "load":
                    SubCommand_LoadPreset(player, args.Skip(1).ToArray());
                    return;

                case "rename":
                    SubCommand_RenamePreset(player, args.Skip(1).ToArray());
                    return;

                case "delete":
                    SubCommand_DeletePreset(player, args.Skip(1).ToArray());
                    return;

                case "fix":
                    SubCommand_FixCar(player, args.Skip(1).ToArray());
                    return;

                case "fetch":
                    SubCommand_FetchCar(player, args.Skip(1).ToArray());
                    return;

                case "destroy":
                    SubCommand_DestroyCar(player, args.Skip(1).ToArray());
                    return;

                case "autocodelock":
                    SubCommand_ToggleAutoCodeLock(player, args.Skip(1).ToArray());
                    return;

                case "autokeylock":
                    SubCommand_ToggleAutoKeyLock(player, args.Skip(1).ToArray());
                    return;

                case "autofilltankers":
                    SubCommand_ToggleAutoFillTankers(player, args.Skip(1).ToArray());
                    return;

                case "common":
                    SubCommand_CommonPreset(player, args.Skip(1).ToArray());
                    return;

                default:
                    SubCommand_SpawnCar(player, args);
                    return;
            }
        }

        private void SubCommand_CommonPreset(IPlayer player, string[] args)
        {
            if (args.Length == 0)
            {
                ReplyToPlayer(player, "Command.Common.Error.Syntax");
                return;
            }

            switch (args[0].ToLower())
            {
                case "list":
                    SubCommand_Common_ListPresets(player, args.Skip(1).ToArray());
                    return;

                case "load":
                    SubCommand_Common_LoadPreset(player, args.Skip(1).ToArray());
                    return;

                case "save":
                    SubCommand_Common_SavePreset(player, args.Skip(1).ToArray());
                    return;

                case "update":
                    SubCommand_Common_UpdatePreset(player, args.Skip(1).ToArray());
                    return;

                case "rename":
                    SubCommand_Common_RenamePreset(player, args.Skip(1).ToArray());
                    return;

                case "delete":
                    SubCommand_Common_DeletePreset(player, args.Skip(1).ToArray());
                    return;

                default:
                    SubCommand_Common_SpawnCar(player, args);
                    return;
            }
        }

        private void SubCommand_Help(IPlayer player, string[] args)
        {
            ushort maxAllowedSockets = GetPlayerMaxAllowedCarSockets(player.Id);
            if (maxAllowedSockets == 0)
            {
                ReplyToPlayer(player, "Generic.Error.NoPermission");
                return;
            }

            var canUsePresets = permission.UserHasPermission(player.Id, PermissionPresets);
            var canLoadPresets = permission.UserHasPermission(player.Id, PermissionPresetLoad);

            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, "Command.Help"));

            if (canUsePresets)
                sb.AppendLine(GetMessage(player, "Command.Help.Spawn.Basic.PresetsAllowed"));
            else
                sb.AppendLine(GetMessage(player, "Command.Help.Spawn.Basic"));

            sb.AppendLine(GetMessage(player, "Command.Help.Spawn.Sockets"));

            if (permission.UserHasPermission(player.Id, PermissionFix))
                sb.AppendLine(GetMessage(player, "Command.Help.Fix"));

            if (permission.UserHasPermission(player.Id, PermissionFetch))
                sb.AppendLine(GetMessage(player, "Command.Help.Fetch"));

            if (permission.UserHasPermission(player.Id, PermissionDespawn))
                sb.AppendLine(GetMessage(player, "Command.Help.Destroy"));

            if (canUsePresets)
            {
                sb.AppendLine(GetMessage(player, "Command.Help.Section.PersonalPresets"));
                sb.AppendLine(GetMessage(player, "Command.Help.ListPresets"));
                sb.AppendLine(GetMessage(player, "Command.Help.Spawn.Preset"));

                if (canLoadPresets)
                    sb.AppendLine(GetMessage(player, "Command.Help.LoadPreset"));

                sb.AppendLine(GetMessage(player, "Command.Help.SavePreset"));
                sb.AppendLine(GetMessage(player, "Command.Help.UpdatePreset"));
                sb.AppendLine(GetMessage(player, "Command.Help.RenamePreset"));
                sb.AppendLine(GetMessage(player, "Command.Help.DeletePreset"));
            }

            if (permission.UserHasPermission(player.Id, PermissionCommonPresets))
            {
                sb.AppendLine(GetMessage(player, "Command.Help.Section.CommonPresets"));
                sb.AppendLine(GetMessage(player, "Command.Help.Common.ListPresets"));
                sb.AppendLine(GetMessage(player, "Command.Help.Common.Spawn"));

                if (canLoadPresets)
                    sb.AppendLine(GetMessage(player, "Command.Help.Common.LoadPreset"));

                if (permission.UserHasPermission(player.Id, PermissionManageCommonPresets))
                {
                    sb.AppendLine(GetMessage(player, "Command.Help.Common.SavePreset"));
                    sb.AppendLine(GetMessage(player, "Command.Help.Common.UpdatePreset"));
                    sb.AppendLine(GetMessage(player, "Command.Help.Common.RenamePreset"));
                    sb.AppendLine(GetMessage(player, "Command.Help.Common.DeletePreset"));
                }
            }

            var canCodeLock = GetLockPlugin() != null && permission.UserHasPermission(player.Id, PermissionAutoCodeLock);
            var canKeyLock = permission.UserHasPermission(player.Id, PermissionAutoKeyLock);
            var canFillTankers = permission.UserHasPermission(player.Id, PermissionAutoFillTankers);

            if (canCodeLock || canKeyLock || canFillTankers)
                sb.AppendLine(GetMessage(player, "Command.Help.Section.PersonalSettings"));

            if (canCodeLock)
                sb.AppendLine(GetMessage(player, "Command.Help.ToggleAutoCodeLock", BooleanToLocalizedString(player, GetPlayerConfig(player).Settings.AutoCodeLock)));

            if (canKeyLock)
                sb.AppendLine(GetMessage(player, "Command.Help.ToggleAutoKeyLock", BooleanToLocalizedString(player, GetPlayerConfig(player).Settings.AutoKeyLock)));

            if (canFillTankers)
                sb.AppendLine(GetMessage(player, "Command.Help.ToggleAutoFillTankers", BooleanToLocalizedString(player, GetPlayerConfig(player).Settings.AutoFillTankers)));

            if (permission.UserHasPermission(player.Id, PermissionGiveCar))
            {
                sb.AppendLine("Command.Help.Section.OtherCommands");
                sb.AppendLine(GetMessage(player, "Command.Help.Give"));
            }

            player.Reply(sb.ToString());
        }

        private void SubCommand_SpawnCar(IPlayer player, string[] args)
        {
            ushort maxAllowedSockets = GetPlayerMaxAllowedCarSockets(player.Id);
            if (maxAllowedSockets == 0)
            {
                ReplyToPlayer(player, "Generic.Error.NoPermission");
                return;
            }

            if (!VerifyHasNoCar(player)) return;
            if (!VerifyOffCooldown(SpawnCarCooldowns, player)) return;
            if (!VerifyLocationNotRestricted(player)) return;
            if (!pluginConfig.CanSpawnBuildingBlocked && !VerifyNotBuildingBlocked(player)) return;

            // Key binds automatically pass the "True" argument
            var wasPassedArgument = args.Length > 0 && args[0] != "True";

            if (wasPassedArgument)
            {
                int desiredSockets;
                if (int.TryParse(args[0], out desiredSockets))
                {
                    if (desiredSockets < 2 || desiredSockets > 4)
                    {
                        ReplyToPlayer(player, "Command.Spawn.Error.SocketSyntax");
                        return;
                    }

                    if (desiredSockets > maxAllowedSockets)
                    {
                        ReplyToPlayer(player, "Generic.Error.NoPermission");
                        return;
                    }

                    SpawnRandomCarForPlayer(player, desiredSockets);
                    return;
                }

                if (!VerifyPermissionAny(player, PermissionPresets)) return;

                var presetNameArg = args[0];

                SimplePreset preset;
                if (!VerifyOnlyOneMatchingPreset(player, GetPlayerConfig(player), presetNameArg, out preset)) return;

                SpawnPresetCarForPlayer(player, preset);
            }
            else
            {
                if (permission.UserHasPermission(player.Id, PermissionPresets))
                {
                    var preset = GetPlayerConfig(player).FindPreset(DefaultPresetName);
                    if (preset != null)
                    {
                        SpawnPresetCarForPlayer(player, preset);
                        return;
                    }
                }

                SpawnRandomCarForPlayer(player, maxAllowedSockets);
            }
        }

        private void SubCommand_Common_SpawnCar(IPlayer player, string[] args)
        {
            ushort maxAllowedSockets = GetPlayerMaxAllowedCarSockets(player.Id);
            if (maxAllowedSockets == 0)
            {
                ReplyToPlayer(player, "Generic.Error.NoPermission");
                return;
            }

            if (!VerifyPermissionAny(player, PermissionCommonPresets)) return;
            if (!VerifyHasNoCar(player)) return;
            if (!VerifyOffCooldown(SpawnCarCooldowns, player)) return;
            if (!VerifyLocationNotRestricted(player)) return;
            if (!pluginConfig.CanSpawnBuildingBlocked && !VerifyNotBuildingBlocked(player)) return;

            var presetNameArg = args[0];

            SimplePreset preset;
            if (!VerifyOnlyOneMatchingPreset(player, pluginData, presetNameArg, out preset)) return;

            SpawnPresetCarForPlayer(player, preset);
        }

        private void SubCommand_FixCar(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionFix)) return;

            ModularCar car;
            if (!VerifyHasCar(player, out car) ||
                !VerifyCarIsNotDead(player, car) ||
                !VerifyOffCooldown(FixCarCooldowns, player) ||
                FixMyCarWasBlocked(player.Object as BasePlayer, car)) return;

            FixCar(car, GetPlayerAllowedFuel(player.Id), GetPlayerEnginePartsTier(player.Id));
            MaybeFillTankerModules(car, GetPlayerAllowedFreshWater(player.Id));
            FixCarCooldowns.UpdateLastUsedForPlayer(player.Id);

            MaybePlayCarRepairEffects(car);
            ReplyToPlayer(player, "Command.Fix.Success");
        }

        private void SubCommand_FetchCar(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionFetch)) return;

            var basePlayer = player.Object as BasePlayer;
            ModularCar car;

            if (!VerifyHasCar(player, out car) ||
                !pluginConfig.CanFetchOccupied && !VerifyCarNotOccupied(player, car) ||
                !VerifyOffCooldown(FetchCarCooldowns, player) ||
                !VerifyLocationNotRestricted(player) ||
                !pluginConfig.CanFetchBuildingBlocked && !VerifyNotBuildingBlocked(player) ||
                FetchMyCarWasBlocked(basePlayer, car)) return;

            // This is a hacky way to determine that the car is on a lift
            if (car.rigidBody.isKinematic && !TryReleaseCarFromLift(car))
            {
                var messages = new List<string> { GetMessage(player, "Command.Fetch.Error.StuckOnLift") };
                if (permission.UserHasPermission(player.Id, PermissionDespawn))
                    messages.Add(GetMessage(player, "Command.Fetch.Error.StuckOnLift.Help"));

                player.Reply(string.Join(" ", messages));
                return;
            }

            if (pluginConfig.DismountPlayersOnFetch)
                DismountAllPlayersFromCar(car);

            // Temporarily clear max angular velocity to prevent the car from unexpectedly spinning when teleporting really far
            var maxAngularVelocity = car.rigidBody.maxAngularVelocity;
            car.rigidBody.maxAngularVelocity = 0;

            car.transform.SetPositionAndRotation(GetIdealCarPosition(basePlayer), GetIdealCarRotation(basePlayer));
            car.SetVelocity(Vector3.zero);
            car.SetAngularVelocity(Vector3.zero);
            car.UpdateNetworkGroup();
            car.SendNetworkUpdateImmediate();
            timer.Once(1f, () =>
            {
                if (car != null)
                    car.rigidBody.maxAngularVelocity = maxAngularVelocity;
            });

            FetchCarCooldowns.UpdateLastUsedForPlayer(player.Id);
            ReplyToPlayer(player, "Command.Fetch.Success");
        }

        private void SubCommand_DestroyCar(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionDespawn)) return;

            var basePlayer = player.Object as BasePlayer;
            ModularCar car;

            if (!VerifyHasCar(player, out car) ||
                !pluginConfig.CanDespawnOccupied && !VerifyCarNotOccupied(player, car) ||
                DestroyMyCarWasBlocked(basePlayer, car))
                return;

            MaybeRemoveMatchingKeysFromPlayer(basePlayer, car);

            var extractedEngineParts = ExtractEnginePartsAboveTierAndDeleteRest(car, GetPlayerEnginePartsTier(player.Id));
            
            car.Kill();

            if (extractedEngineParts.Count > 0)
            {
                GiveItemsToPlayerOrDrop(basePlayer, extractedEngineParts);
                ReplyToPlayer(player, "Generic.Info.PartsRecovered");
            }
        }

        private void SubCommand_ListPresets(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionPresets)) return;

            PlayerConfig config = GetPlayerConfig(player);
            if (config.Presets.Count == 0)
            {
                ReplyToPlayer(player, "Generic.Error.NoPresets");
                return;
            }

            var presetList = config.Presets.Select(p => p).ToList();
            presetList.Sort(SortPresetNames);

            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, "Command.List"));

            foreach (var preset in presetList)
                sb.AppendLine(GetMessage(player, "Command.List.Item", preset.Name, preset.NumSockets));

            player.Reply(sb.ToString());
        }

        private void SubCommand_Common_ListPresets(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionCommonPresets)) return;

            if (pluginData.Presets.Count == 0)
            {
                ReplyToPlayer(player, "Generic.Error.NoCommonPresets");
                return;
            }

            ushort maxAllowedSockets = GetPlayerMaxAllowedCarSockets(player.Id);

            var presetList = pluginData.Presets.Where(p => p.NumSockets <= maxAllowedSockets).ToList();
            presetList.Sort(SortPresetNames);

            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, "Command.Common.List"));

            foreach (var preset in presetList)
                sb.AppendLine(GetMessage(player, "Command.List.Item", preset.Name, preset.NumSockets));

            player.Reply(sb.ToString());
        }

        private void SubCommand_SavePreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionPresets)) return;

            ModularCar car;
            if (!VerifyHasCar(player, out car)) return;

            var presetNameArg = args.Length == 0 ? DefaultPresetName : args[0];

            var presetManager = GetPlayerConfig(player);
            if (!VerifyNoMatchingPreset(player, presetManager, presetNameArg)) return;
            
            if (presetManager.Presets.Count >= pluginConfig.MaxPresetsPerPlayer)
            {
                ReplyToPlayer(player, "Command.SavePreset.Error.TooManyPresets", pluginConfig.MaxPresetsPerPlayer);
                return;
            }

            SavePreset(player, presetManager, presetNameArg, car);
        }

        private void SubCommand_Common_SavePreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionManageCommonPresets)) return;

            if (args.Length == 0)
            {
                ReplyToPlayer(player, "Command.Common.SavePreset.Error.Syntax");
                return;
            }

            var presetNameArg = args[0];

            ModularCar car;
            if (!VerifyHasCar(player, out car)) return;
            if (!VerifyNoMatchingPreset(player, pluginData, presetNameArg)) return;
            
            SavePreset(player, pluginData, presetNameArg, car);
        }

        private void SavePreset(IPlayer player, SimplePresetManager presetManager, string presetNameArg, ModularCar car)
        {
            if (presetNameArg.Length > PresetMaxLength)
            {
                ReplyToPlayer(player, "Generic.Error.PresetNameLength", PresetMaxLength);
                return;
            }

            presetManager.SavePreset(SimplePreset.FromCar(car, presetNameArg));
            ReplyToPlayer(player, "Command.SavePreset.Success", presetNameArg);
        }

        private void SubCommand_UpdatePreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionPresets)) return;
            var presetNameArg = args.Length == 0 ? DefaultPresetName : args[0];
            UpdatePreset(player, GetPlayerConfig(player), presetNameArg);
        }

        private void SubCommand_Common_UpdatePreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionManageCommonPresets)) return;

            if (args.Length == 0)
            {
                ReplyToPlayer(player, "Command.Common.UpdatePreset.Error.Syntax");
                return;
            }

            UpdatePreset(player, pluginData, args[0]);
        }

        private void UpdatePreset(IPlayer player, SimplePresetManager presetManager, string presetNameArg)
        {
            ModularCar car;
            if (!VerifyHasCar(player, out car)) return;

            SimplePreset preset;
            if (!VerifyHasPreset(player, presetManager, presetNameArg, out preset)) return;

            presetManager.UpdatePreset(SimplePreset.FromCar(car, preset.Name));
            ReplyToPlayer(player, "Command.UpdatePreset.Success", preset.Name);
        }

        private void SubCommand_LoadPreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionPresetLoad)) return;
            var presetNameArg = args.Length == 0 ? DefaultPresetName : args[0];
            LoadPreset(player, GetPlayerConfig(player.Id), presetNameArg);
        }

        private void SubCommand_Common_LoadPreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionPresetLoad) || !VerifyPermissionAny(player, PermissionCommonPresets)) return;

            if (args.Length == 0)
            {
                ReplyToPlayer(player, "Command.Common.LoadPreset.Error.Syntax");
                return;
            }

            var presetNameArg = args[0];
            LoadPreset(player, pluginData, presetNameArg);
        }

        private void LoadPreset(IPlayer player, SimplePresetManager presetManager, string presetNameArg)
        {
            var basePlayer = player.Object as BasePlayer;
            ModularCar car;

            if (!VerifyHasCar(player, out car) ||
                !VerifyCarIsNotDead(player, car) ||
                !VerifyCarNotOccupied(player, car) ||
                !VerifyOffCooldown(LoadPresetCooldowns, player) ||
                LoadMyCarPresetWasBlocked(basePlayer, car)) return;

            SimplePreset preset;
            if (!VerifyOnlyOneMatchingPreset(player, presetManager, presetNameArg, out preset)) return;

            var presetNumSockets = preset.NumSockets;
            if (presetNumSockets > GetPlayerMaxAllowedCarSockets(player.Id))
            {
                ReplyToPlayer(player, "Generic.Error.NoPermissionToPresetSocketCount", preset.Name, preset.NumSockets);
                return;
            }

            if (presetNumSockets != car.TotalSockets)
            {
                ReplyToPlayer(player, "Command.LoadPreset.Error.SocketCount", preset.Name, presetNumSockets, car.TotalSockets);
                return;
            }

            var wasEngineOn = car.IsOn();
            var enginePartsTier = GetPlayerEnginePartsTier(player.Id);
            var extractedEngineParts = ExtractEnginePartsAboveTierAndDeleteRest(car, enginePartsTier);
            UpdateCarModules(car, preset.ModuleIDs);
            LoadPresetCooldowns.UpdateLastUsedForPlayer(player.Id);

            NextTick(() => {
                var wereExtraParts = false;

                if (extractedEngineParts.Count > 0)
                {
                    var remainingEngineParts = AddEngineItemsAndReturnRemaining(car, extractedEngineParts);
                    if (remainingEngineParts.Count > 0)
                    {
                        wereExtraParts = true;
                        GiveItemsToPlayerOrDrop(basePlayer, remainingEngineParts);
                    }
                }

                FixCar(car, GetPlayerAllowedFuel(player.Id), enginePartsTier);

                // Restart the engine if it turned off during the brief moment it had no engine or no parts
                if (wasEngineOn && !car.IsOn() && car.CanRunEngines()) car.FinishStartingEngine();

                MaybeFillTankerModules(car, GetPlayerAllowedFreshWater(player.Id));
                
                if (car.carLock.HasALock && !car.carLock.CanHaveALock())
                {
                    MaybeRemoveMatchingKeysFromPlayer(basePlayer, car);
                    car.RemoveLock();
                }

                MaybePlayCarRepairEffects(car);

                var chatMessages = new List<string>() { GetMessage(player, "Command.LoadPreset.Success", preset.Name) };
                if (wereExtraParts)
                    chatMessages.Add(GetMessage(player, "Generic.Info.PartsRecovered"));

                player.Reply(string.Join(" ", chatMessages));
            });
        }

        private void SubCommand_RenamePreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionPresets)) return;

            if (args.Length < 2)
            {
                ReplyToPlayer(player, "Command.RenamePreset.Error.Syntax");
                return;
            }

            RenamePreset(player, GetPlayerConfig(player), args[0], args[1]);
        }

        private void SubCommand_Common_RenamePreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionManageCommonPresets)) return;

            if (args.Length < 2)
            {
                ReplyToPlayer(player, "Command.Common.RenamePreset.Error.Syntax");
                return;
            }

            RenamePreset(player, pluginData, args[0], args[1]);
        }

        private void RenamePreset(IPlayer player, SimplePresetManager presetManager, string oldName, string newName)
        {
            SimplePreset preset;
            if (!VerifyHasPreset(player, presetManager, oldName, out preset)) return;

            // Cache actual old preset name since matching is case-insensitive
            var actualOldPresetName = preset.Name;

            SimplePreset existingPresetWithNewName = presetManager.FindPreset(newName);

            if (newName.Length > PresetMaxLength)
            {
                ReplyToPlayer(player, "Generic.Error.PresetNameLength", PresetMaxLength);
                return;
            }

            // Allow renaming if just changing case
            if (existingPresetWithNewName != null && preset != existingPresetWithNewName)
            {
                ReplyToPlayer(player, "Generic.Error.PresetAlreadyTaken", existingPresetWithNewName.Name);
                return;
            }

            presetManager.RenamePreset(preset, newName);
            ReplyToPlayer(player, "Command.RenamePreset.Success", actualOldPresetName, newName);
        }

        private void SubCommand_DeletePreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionPresets)) return;
            var presetNameArg = args.Length == 0 ? DefaultPresetName : args[0];
            DeletePreset(player, GetPlayerConfig(player), presetNameArg);
        }

        private void SubCommand_Common_DeletePreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionManageCommonPresets)) return;

            if (args.Length == 0)
            {
                ReplyToPlayer(player, "Command.Common.DeletePreset.Error.Syntax");
                return;
            }

            DeletePreset(player, pluginData, args[0]);
        }

        private void DeletePreset(IPlayer player, SimplePresetManager presetManager, string presetNameArg)
        {
            SimplePreset preset;
            if (!VerifyHasPreset(player, presetManager, presetNameArg, out preset)) return;

            presetManager.DeletePreset(preset);
            ReplyToPlayer(player, "Command.DeletePreset.Success", preset.Name);
        }

        private void SubCommand_ToggleAutoCodeLock(IPlayer player, string[] args)
        {
            if (GetLockPlugin() == null || !VerifyPermissionAny(player, PermissionAutoCodeLock)) return;

            var config = GetPlayerConfig(player);
            config.Settings.AutoCodeLock = !config.Settings.AutoCodeLock;
            config.SaveData();
            ReplyToPlayer(player, "Command.ToggleAutoCodeLock.Success", BooleanToLocalizedString(player, config.Settings.AutoCodeLock));
        }

        private void SubCommand_ToggleAutoKeyLock(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionAutoKeyLock)) return;

            var config = GetPlayerConfig(player);
            config.Settings.AutoKeyLock = !config.Settings.AutoKeyLock;
            config.SaveData();
            ReplyToPlayer(player, "Command.ToggleAutoKeyLock.Success", BooleanToLocalizedString(player, config.Settings.AutoKeyLock));
        }

        private void SubCommand_ToggleAutoFillTankers(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionAutoFillTankers)) return;

            var config = GetPlayerConfig(player);
            config.Settings.AutoFillTankers = !config.Settings.AutoFillTankers;
            config.SaveData();
            ReplyToPlayer(player, "Command.ToggleAutoFillTankers.Success", BooleanToLocalizedString(player, config.Settings.AutoFillTankers));
        }

        #endregion

        #region Helper Methods - Command Checks

        private bool SpawnWasBlocked(BasePlayer player)
        {
            object hookResult = Interface.CallHook("CanSpawnModularCar", player);
            return (hookResult is bool && (bool)hookResult == false);
        }

        private bool SpawnMyCarWasBlocked(BasePlayer player)
        {
            if (SpawnWasBlocked(player)) return true;

            object hookResult = Interface.CallHook("CanSpawnMyCar", player);
            return (hookResult is bool && (bool)hookResult == false);
        }

        private bool FetchMyCarWasBlocked(BasePlayer player, ModularCar car)
        {
            object hookResult = Interface.CallHook("CanFetchMyCar", player, car);
            return (hookResult is bool && (bool)hookResult == false);
        }

        private bool FixMyCarWasBlocked(BasePlayer player, ModularCar car)
        {
            object hookResult = Interface.CallHook("CanFixMyCar", player, car);
            return (hookResult is bool && (bool)hookResult == false);
        }

        private bool LoadMyCarPresetWasBlocked(BasePlayer player, ModularCar car)
        {
            object hookResult = Interface.CallHook("CanLoadMyCarPreset", player, car);
            return (hookResult is bool && (bool)hookResult == false);
        }

        private bool DestroyMyCarWasBlocked(BasePlayer player, ModularCar car)
        {
            object hookResult = Interface.CallHook("CanDestroyMyCar", player, car);
            return (hookResult is bool && (bool)hookResult == false);
        }

        private bool VerifyPermissionAny(IPlayer player, params string[] permissionNames)
        {
            foreach (var perm in permissionNames)
            {
                if (!permission.UserHasPermission(player.Id, perm))
                {
                    ReplyToPlayer(player, "Generic.Error.NoPermission");
                    return false;
                }
            }
            return true;
        }

        private bool VerifyLocationNotRestricted(IPlayer player)
        {
            if ((player.Object as BasePlayer).GetComponentInParent<CargoShip>() != null)
            {
                ReplyToPlayer(player, "Generic.Error.LocationRestricted");
                return false;
            }
            return true;
        }

        private bool VerifyNotBuildingBlocked(IPlayer player)
        {
            if ((player.Object as BasePlayer).IsBuildingBlocked())
            {
                ReplyToPlayer(player, "Generic.Error.BuildingBlocked");
                return false;
            }
            return true;
        }

        private bool VerifyHasPreset(IPlayer player, SimplePresetManager presetManager, string presetName, out SimplePreset preset)
        {
            preset = presetManager.FindPreset(presetName);
            if (preset == null)
            {
                ReplyToPlayer(player, "Generic.Error.PresetNotFound", presetName);
                return false;
            }
            return true;
        }

        private bool VerifyNoMatchingPreset(IPlayer player, SimplePresetManager presetManager, string presetName)
        {
            var existingPreset = presetManager.FindPreset(presetName);
            if (existingPreset != null)
            {
                ReplyToPlayer(player, "Command.SavePreset.Error.PresetAlreadyExists", existingPreset.Name);
                return false;
            }
            return true;
        }

        private bool VerifyHasCar(IPlayer player, out ModularCar car)
        {
            car = FindPlayerCar(player);
            if (car == null)
            {
                ReplyToPlayer(player, "Generic.Error.CarNotFound");
                return false;
            }
            return true;
        }

        private bool VerifyHasNoCar(IPlayer player)
        {
            if (FindPlayerCar(player) == null) return true;

            var messages = new List<string> { GetMessage(player, "Command.Spawn.Error.CarAlreadyExists") };
            if (permission.UserHasPermission(player.Id, PermissionFetch))
                messages.Add(GetMessage(player, "Command.Spawn.Error.CarAlreadyExists.Help"));

            player.Reply(string.Join(" ", messages));
            return false;
        }

        private bool VerifyCarNotOccupied(IPlayer player, ModularCar car)
        {
            // Players can either be mounted in seats, or standing on flatbed modules
            if (car.AnyMounted() || car.AttachedModuleEntities.Any(module => module.children.Any(child => child is BasePlayer)))
            {
                ReplyToPlayer(player, "Generic.Error.CarOccupied");
                return false;
            }
            return true;
        }

        private bool VerifyCarIsNotDead(IPlayer player, ModularCar car)
        {
            if (car.IsDead())
            {
                ReplyToPlayer(player, "Generic.Error.CarDead");
                return false;
            }
            return true;
        }

        private bool VerifyOffCooldown(CooldownManager cooldownManager, IPlayer player)
        {
            var secondsRemaining = cooldownManager.GetSecondsRemaining(player.Id);
            if (secondsRemaining > 0)
            {
                ReplyToPlayer(player, "Generic.Error.Cooldown", Math.Ceiling(secondsRemaining));
                return false;
            }
            return true;
        }

        private bool VerifyOnlyOneMatchingPreset(IPlayer player, SimplePresetManager presetManager, string presetName, out SimplePreset preset)
        {
            preset = presetManager.FindPreset(presetName);
            if (preset != null) return true;

            var matchingPresets = presetManager.FindMatchingPresets(presetName);
            var matchCount = matchingPresets.Count;

            if (matchCount == 0)
            {
                ReplyToPlayer(player, "Generic.Error.PresetNotFound", presetName);
                return false;
            }
            else if (matchCount > 1)
            {
                ReplyToPlayer(player, "Generic.Error.PresetMultipleMatches", presetName);
                return false;
            }

            preset = matchingPresets.First();
            return true;
        }

        #endregion

        #region Helper Methods - Cars

        private bool IsPlayerCar(ModularCar car) =>
            pluginData.playerCars.ContainsValue(car.net.ID);

        private int SortPresetNames(SimplePreset a, SimplePreset b) =>
            a.Name.ToLower() == DefaultPresetName ? -1 :
            b.Name.ToLower() == DefaultPresetName ? 1 :
            a.Name.CompareTo(b.Name);

        private ModularCar FindPlayerCar(IPlayer player)
        {
            if (!pluginData.playerCars.ContainsKey(player.Id))
                return null;

            var car = BaseNetworkable.serverEntities.Find(pluginData.playerCars[player.Id]) as ModularCar;

            // Just in case the car was removed and that somehow wasn't detected sooner
            // This could happen if the data file somehow got out of sync for instance
            if (car == null)
                pluginData.UnregisterCar(player.Id);

            return car;
        }

        private int[] GetCarModuleIDs(ModularCar car)
        {
            var moduleIDs = new List<int>();

            for (int socketIndex = 0; socketIndex < car.TotalSockets; socketIndex++)
            {
                BaseVehicleModule module;
                if (car.TryGetModuleAt(socketIndex, out module) && module.FirstSocketIndex == socketIndex)
                    moduleIDs.Add(module.AssociatedItemDef.itemid);
                else
                    // Empty socket, use 0 to represent this
                    moduleIDs.Add(0);
            }

            return moduleIDs.ToArray();
        }

        private Vector3 GetIdealCarPosition(BasePlayer player)
        {
            Vector3 forward = player.GetNetworkRotation() * Vector3.forward;
            forward.y = 0;
            Vector3 position = player.transform.position + forward.normalized * 3f;
            position.y = player.transform.position.y + 1f;
            return position;
        }

        private Quaternion GetIdealCarRotation(BasePlayer player) =>
            Quaternion.Euler(0, player.GetNetworkRotation().eulerAngles.y - 90, 0);

        private int GetPlayerAllowedFreshWater(string userID) =>
            permission.UserHasPermission(userID, PermissionAutoFillTankers) && GetPlayerConfig(userID).Settings.AutoFillTankers ? pluginConfig.FreshWaterAmount : 0;

        private int GetPlayerAllowedFuel(string userID) =>
            permission.UserHasPermission(userID, PermissionAutoFuel) ? pluginConfig.FuelAmount : 0;

        private int GetPlayerEnginePartsTier(string userID)
        {
            if (permission.UserHasPermission(userID, PermissionEnginePartsTier3))
                return 3;
            else if (permission.UserHasPermission(userID, PermissionEnginePartsTier2))
                return 2;
            else if (permission.UserHasPermission(userID, PermissionEnginePartsTier1))
                return 1;
            else
                return 0;
        }

        private ushort GetPlayerMaxAllowedCarSockets(string userID)
        {
            if (permission.UserHasPermission(userID, PermissionSpawnSockets4))
                return 4;
            else if (permission.UserHasPermission(userID, PermissionSpawnSockets3))
                return 3;
            else if (permission.UserHasPermission(userID, PermissionSpawnSockets2))
                return 2;
            else
                return 0;
        }

        private void SpawnRandomCarForPlayer(IPlayer player, int desiredSockets)
        {
            var basePlayer = player.Object as BasePlayer;
            if (SpawnMyCarWasBlocked(basePlayer)) return;

            var carOptions = new RandomCarOptions(player.Id, desiredSockets);
            SpawnCarForPlayer(basePlayer, carOptions, shouldTrackCar: true, onReady: car =>
            {
                var chatMessages = new List<string> { GetMessage(player, "Command.Spawn.Success") };

                if (car.carLock.HasALock)
                    chatMessages.Add(GetMessage(player, "Command.Spawn.Success.Locked"));

                player.Reply(string.Join(" ", chatMessages));
            });
        }

        private void SpawnPresetCarForPlayer(IPlayer player, SimplePreset preset)
        {
            if (preset.NumSockets > GetPlayerMaxAllowedCarSockets(player.Id))
            {
                ReplyToPlayer(player, "Generic.Error.NoPermissionToPresetSocketCount", preset.Name, preset.NumSockets);
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            if (SpawnMyCarWasBlocked(basePlayer)) return;

            var carOptions = new PresetCarOptions(player.Id, preset.ModuleIDs);
            SpawnCarForPlayer(basePlayer, carOptions, shouldTrackCar: true, onReady: car =>
            {
                var chatMessages = new List<string> { GetMessage(player, "Command.Spawn.Success.Preset", preset.Name) };

                if (car.carLock.HasALock)
                    chatMessages.Add(GetMessage(player, "Command.Spawn.Success.Locked"));

                ReplyToPlayer(player, string.Join(" ", chatMessages));

                if (preset != null)
                    MaybePlayCarRepairEffects(car);
            });
        }

        private ModularCar SpawnCarForPlayer(BasePlayer player, BaseCarOptions options, bool shouldTrackCar = false, Action<ModularCar> onReady = null)
        {
            var numSockets = options.Length;

            string prefabName;
            if (numSockets == 4) prefabName = PrefabSockets4;
            else if (numSockets == 3) prefabName = PrefabSockets3;
            else if (numSockets == 2) prefabName = PrefabSockets2;
            else return null;

            var position = GetIdealCarPosition(player);
            if (position == null) return null;

            ModularCar car = (ModularCar)GameManager.server.CreateEntity(prefabName, position, GetIdealCarRotation(player));
            if (car == null) return null;

            var presetOptions = options as PresetCarOptions;
            if (presetOptions != null)
                car.spawnSettings.useSpawnSettings = false;

            car.OwnerID = player.userID;
            car.Spawn();

            if (presetOptions != null)
                AddInitialModules(car, presetOptions.ModuleIDs);

            if (shouldTrackCar)
            {
                pluginData.RegisterCar(player.UserIDString, car);
                SpawnCarCooldowns.UpdateLastUsedForPlayer(player.UserIDString);
            }

            NextTick(() =>
            {
                FixCar(car, options.FuelAmount, options.EnginePartsTier);
                MaybeFillTankerModules(car, options.FreshWaterAmount);

                if (options.CodeLock)
                {
                    var lockPlugin = GetLockPlugin();
                    if (lockPlugin != null)
                    {
                        // both VehicleDeployedLocks and CarCodeLocks have the same API signature
                        lockPlugin.Call("API_DeployCodeLock", car, player);
                    }
                }

                if (options.KeyLock) TryAddKeyLockForPlayer(car, player);

                onReady?.Invoke(car);
            });

            return car;
        }

        private void AddInitialModules(ModularCar car, int[] ModuleIDs)
        {
            for (int socketIndex = 0; socketIndex < car.TotalSockets; socketIndex++)
            {
                var desiredItemID = ModuleIDs[socketIndex];

                // We are using 0 to represent an empty socket which we skip
                if (desiredItemID != 0)
                {
                    var moduleItem = ItemManager.CreateByItemID(desiredItemID);
                    if (moduleItem != null)
                        car.TryAddModule(moduleItem, socketIndex);
                }
            }
        }

        private void UpdateCarModules(ModularCar car, int[] moduleIDs)
        {
            // Phase 1: Remove all modules that don't match the desired preset
            // This is done first since some modules take up two sockets
            for (int socketIndex = 0; socketIndex < car.TotalSockets; socketIndex++)
            {
                var desiredItemID = moduleIDs[socketIndex];
                var existingItem = car.Inventory.ModuleContainer.GetSlot(socketIndex);
                
                if (existingItem != null && existingItem.info.itemid != desiredItemID)
                {
                    existingItem.RemoveFromContainer();
                    existingItem.Remove();
                }
            }

            // Phase 2: Add the modules that are missing
            for (int socketIndex = 0; socketIndex < car.TotalSockets; socketIndex++)
            {
                var desiredItemID = moduleIDs[socketIndex];
                var existingItem = car.Inventory.ModuleContainer.GetSlot(socketIndex);

                // We are using 0 to represent an empty socket which we skip
                if (existingItem == null && desiredItemID != 0)
                {
                    var moduleItem = ItemManager.CreateByItemID(desiredItemID);
                    if (moduleItem != null)
                        car.TryAddModule(moduleItem, socketIndex);
                }
            }
        }

        private List<Item> AddEngineItemsAndReturnRemaining(ModularCar car, List<Item> engineItems)
        {
            var itemsByType = engineItems
                .GroupBy(item => item.info.GetComponent<ItemModEngineItem>().engineItemType)
                .ToDictionary(
                    grouping => grouping.Key,
                    grouping => grouping.OrderByDescending(item => item.info.GetComponent<ItemModEngineItem>().tier).ToList()
                );

            foreach (var module in car.AttachedModuleEntities)
            {
                var engineStorage = (module as VehicleModuleEngine)?.GetContainer() as EngineStorage;
                if (engineStorage == null) continue;

                for (var slotIndex = 0; slotIndex < engineStorage.inventory.capacity; slotIndex++)
                {
                    var engineItemType = engineStorage.slotTypes[slotIndex];
                    if (!itemsByType.ContainsKey(engineItemType)) continue;

                    var itemsOfType = itemsByType[engineItemType];
                    var existingItem = engineStorage.inventory.GetSlot(slotIndex);
                    if (existingItem != null || itemsOfType.Count == 0) continue;

                    itemsOfType[0].MoveToContainer(engineStorage.inventory, slotIndex, allowStack: false);
                    itemsOfType.RemoveAt(0);
                }
            }

            return itemsByType.Values.SelectMany(x => x).ToList();
        }

        private void AddUpgradeOrRepairEngineParts(EngineStorage engineStorage, int desiredTier)
        {
            if (engineStorage.inventory == null) return;
            
            var inventory = engineStorage.inventory;
            for (var i = 0; i < inventory.capacity; i++)
            {
                var item = inventory.GetSlot(i);
                if (item != null)
                {
                    var component = item.info.GetComponent<ItemModEngineItem>();
                    if (component != null && component.tier < desiredTier)
                    {
                        item.RemoveFromContainer();
                        item.Remove();
                        TryAddEngineItem(engineStorage, i, desiredTier);
                    }
                    else
                        item.condition = item.maxCondition;
                }
                else if (desiredTier > 0)
                    TryAddEngineItem(engineStorage, i, desiredTier);
            }
        }

        private bool TryAddEngineItem(EngineStorage engineStorage, int slot, int tier)
        {
            ItemModEngineItem output;
            if (!engineStorage.allEngineItems.TryGetItem(tier, engineStorage.slotTypes[slot], out output)) return false;

            var component = output.GetComponent<ItemDefinition>();
            var item = ItemManager.Create(component);
            if (item == null) return false;

            item.condition = component.condition.max;
            item.MoveToContainer(engineStorage.inventory, slot, allowStack: false);

            return true;
        }

        private List<Item> ExtractEnginePartsAboveTierAndDeleteRest(ModularCar car, int tier)
        {
            var extractedEngineParts = new List<Item>();

            foreach (var module in car.AttachedModuleEntities)
            {
                var engineStorage = (module as VehicleModuleEngine)?.GetContainer() as EngineStorage;
                if (engineStorage == null) continue;

                for (var i = 0; i < engineStorage.inventory.capacity; i++)
                {
                    var item = engineStorage.inventory.GetSlot(i);
                    if (item == null) continue;

                    var component = item.info.GetComponent<ItemModEngineItem>();
                    if (component == null) continue;

                    item.RemoveFromContainer();

                    if (component.tier > tier)
                        extractedEngineParts.Add(item);
                    else
                        item.Remove();
                }
            }

            return extractedEngineParts;
        }

        private void GiveItemsToPlayerOrDrop(BasePlayer player, List<Item> itemList)
        {
            var itemsToDrop = new List<Item>();

            foreach (var item in itemList)
                if (!player.inventory.GiveItem(item))
                    itemsToDrop.Add(item);

            if (itemsToDrop.Count > 0)
                DropEngineParts(player, itemsToDrop);
        }

        private void DropEngineParts(BasePlayer player, List<Item> itemList)
        {
            if (itemList.Count == 0) return;

            var position = player.GetDropPosition();
            if (itemList.Count == 1)
            {
                itemList[0].Drop(position, player.GetDropVelocity());
                return;
            }

            var container = GameManager.server.CreateEntity(ItemDropPrefab, position, player.GetNetworkRotation()) as DroppedItemContainer;
            if (container == null) return;

            container.playerName = $"{player.displayName}'s Engine Parts";

            // 4 large engines * 8 parts (each damaged) = 32 max engine parts
            // This fits within the standard max size of 36
            var capacity = Math.Min(itemList.Count, container.maxItemCount);

            container.inventory = new ItemContainer();
            container.inventory.ServerInitialize(null, capacity);
            container.inventory.GiveUID();
            container.inventory.entityOwner = container;
            container.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);

            foreach (var item in itemList)
                if (!item.MoveToContainer(container.inventory))
                    item.DropAndTossUpwards(position);

            container.ResetRemovalTime();
            container.SetVelocity(player.GetDropVelocity());
            container.Spawn();
        }

        private void FixCar(ModularCar car, int fuelAmount, int enginePartsTier)
        {
            car.SetHealth(car.MaxHealth());
            car.SendNetworkUpdate();
            AddOrRestoreFuel(car, fuelAmount);

            foreach (var module in car.AttachedModuleEntities)
            {
                module.SetHealth(module.MaxHealth());
                module.SendNetworkUpdate();

                var engineModule = module as VehicleModuleEngine;
                if (engineModule != null)
                {
                    var engineStorage = engineModule.GetContainer() as EngineStorage;
                    AddUpgradeOrRepairEngineParts(engineStorage, enginePartsTier);
                    engineModule.RefreshPerformanceStats(engineStorage);
                }
            }
        }

        private void AddOrRestoreFuel(ModularCar car, int specifiedFuelAmount)
        {
            var fuelContainer = car.fuelSystem.GetFuelContainer();
            var targetFuelAmount = specifiedFuelAmount == -1 ? fuelContainer.allowedItem.stackable : specifiedFuelAmount;
            if (targetFuelAmount == 0) return;

            var fuelItem = fuelContainer.inventory.FindItemByItemID(fuelContainer.allowedItem.itemid);
            if (fuelItem == null)
            {
                fuelContainer.inventory.AddItem(fuelContainer.allowedItem, targetFuelAmount);
            }
            else if (fuelItem.amount < targetFuelAmount)
            {
                fuelItem.amount = targetFuelAmount;
                fuelItem.MarkDirty();
            }
        }

        private bool TryReleaseCarFromLift(ModularCar car)
        {
            RaycastHit hitInfo;
            // This isn't perfect as it can hit other deployables such as rugs
            if (!Physics.SphereCast(car.transform.position + Vector3.up, 1f, Vector3.down, out hitInfo, 1f)) return false;

            var lift = RaycastHitEx.GetEntity(hitInfo) as ModularCarGarage;
            if (lift == null || lift.carOccupant != car) return false;

            // Disable the lift for a bit, to prevent it from grabbing the car back
            lift.enabled = false;
            lift.ReleaseOccupant();
            lift.Invoke(() => lift.enabled = true, 0.5f);

            return true;
        }

        private void DismountAllPlayersFromCar(ModularCar car)
        {
            // Dismount seated players
            if (car.AnyMounted())
                car.DismountAllPlayers();

            // Dismount players standing on flatbed modules
            foreach (var module in car.AttachedModuleEntities)
                foreach (var child in module.children.ToList())
                    if (child is BasePlayer)
                        (child as BasePlayer).SetParent(null, worldPositionStays: true);
        }

        private bool ShouldTryAddCodeLockForPlayer(string userID) =>
            permission.UserHasPermission(userID, PermissionAutoCodeLock) && GetPlayerConfig(userID).Settings.AutoCodeLock;

        private bool ShouldTryAddKeyLockForPlayer(string userID) =>
            permission.UserHasPermission(userID, PermissionAutoKeyLock) && GetPlayerConfig(userID).Settings.AutoKeyLock;

        private bool TryAddKeyLockForPlayer(ModularCar car, BasePlayer player)
        {
            if (car.carLock.HasALock || !car.carLock.CanHaveALock()) return false;
            car.carLock.AddALock();
            car.carLock.TryCraftAKey(player, free: true);
            return true;
        }

        private void MaybeRemoveMatchingKeysFromPlayer(BasePlayer player, ModularCar car)
        {
            if (pluginConfig.DeleteKeyOnDespawn && car.carLock.HasALock)
            {
                var matchingKeys = player.inventory.FindItemIDs(car.carKeyDefinition.itemid)
                    .Where(key => key.instanceData != null && key.instanceData.dataInt == car.carLock.LockID);

                foreach (var key in matchingKeys)
                    key.Remove();
            }
        }

        private void MaybeFillTankerModules(ModularCar car, int specifiedLiquidAmount)
        {
            if (specifiedLiquidAmount == 0) return;

            foreach (var module in car.AttachedModuleEntities)
            {
                var liquidContainer = (module as VehicleModuleStorage)?.GetContainer() as LiquidContainer;
                if (liquidContainer == null) continue;

                if (FillLiquidContainer(liquidContainer, specifiedLiquidAmount) && pluginConfig.EnableEffects)
                    Effect.server.Run(TankerFilledEffectPrefab, module.transform.position);
            }
        }

        private bool FillLiquidContainer(LiquidContainer liquidContainer, int specifiedAmount)
        {
            var targetAmount = specifiedAmount == -1 ? liquidContainer.maxStackSize : specifiedAmount;
            var defaultItem = liquidContainer.defaultLiquid;
            var existingItem = liquidContainer.GetLiquidItem();

            if (existingItem == null)
            {
                liquidContainer.inventory.AddItem(defaultItem, targetAmount);
                return true;
            }

            if (existingItem.info.itemid != defaultItem.itemid)
            {
                // Remove other liquid such as salt water
                existingItem.RemoveFromContainer();
                existingItem.Remove();
                liquidContainer.inventory.AddItem(defaultItem, targetAmount);
                return true;
            }
            
            if (existingItem.amount >= targetAmount)
                // Nothing added in this case
                return false;

            existingItem.amount = targetAmount;
            existingItem.MarkDirty();
            return true;
        }

        private void MaybePlayCarRepairEffects(ModularCar car)
        {
            if (!pluginConfig.EnableEffects) return;

            if (car.AttachedModuleEntities.Count > 0)
                foreach (var module in car.AttachedModuleEntities)
                    Effect.server.Run(RepairEffectPrefab, module.transform.position);
            else
                Effect.server.Run(RepairEffectPrefab, car.transform.position);
        }

        private int[] ParseModules(object[] moduleArray)
        {
            var moduleIDList = new List<int>();

            foreach (var module in moduleArray)
            {
                ItemDefinition itemDef;

                if (module is int || module is long)
                {
                    var moduleInt = module is long ? Convert.ToInt32((long)module) : (int)module;
                    if (moduleInt == 0)
                    {
                        moduleIDList.Add(0);
                        continue;
                    }
                    itemDef = ItemManager.FindItemDefinition(moduleInt);
                }
                else if (module is string)
                {
                    int parsedItemId;
                    if (int.TryParse(module as string, out parsedItemId))
                    {
                        if (parsedItemId == 0)
                        {
                            moduleIDList.Add(0);
                            continue;
                        }
                        itemDef = ItemManager.FindItemDefinition(parsedItemId);
                    }
                    else
                        itemDef = ItemManager.FindItemDefinition(module as string);
                }
                else
                {
                    pluginInstance.LogWarning("Unable to parse module id or name: '{0}'", module);
                    continue;
                }

                if (itemDef == null)
                {
                    pluginInstance.LogWarning("No item definition found for: '{0}'", module);
                    continue;
                }

                var vehicleModule = itemDef.GetComponent<ItemModVehicleModule>();
                if (vehicleModule == null)
                {
                    pluginInstance.LogWarning("No vehicle module found for item: '{0}'", module);
                    continue;
                }

                moduleIDList.Add(itemDef.itemid);

                // Normalize module IDs by adding 0s after the module if it takes multiple sockets
                for (var i = 0; i < vehicleModule.SocketsTaken - 1; i++)
                    moduleIDList.Add(0);
            }

            return moduleIDList.ToArray();
        }

        private int Clamp(int x, int min, int max) => Math.Min(max, Math.Max(min, x));

        #endregion

        #region Helper Methods - Misc

        private Plugin GetLockPlugin() => VehicleDeployedLocks ?? CarCodeLocks;

        #endregion

        #region Helper Classes

        internal class CooldownManager
        {
            private readonly Dictionary<string, float> CooldownMap = new Dictionary<string, float>();
            private readonly float CooldownDuration;

            public CooldownManager(float duration)
            {
                CooldownDuration = duration;
            }

            public void UpdateLastUsedForPlayer(string userID)
            {
                if (CooldownMap.ContainsKey(userID))
                    CooldownMap[userID] = Time.realtimeSinceStartup;
                else
                    CooldownMap.Add(userID, Time.realtimeSinceStartup);
            }

            public float GetSecondsRemaining(string userID)
            {
                if (!CooldownMap.ContainsKey(userID)) return 0;
                return CooldownMap[userID] + CooldownDuration - Time.realtimeSinceStartup;
            }
        }

        #endregion

        #region Data Management

        internal class PluginData : SimplePresetManager
        {
            [JsonProperty("playerCars")]
            public Dictionary<string, uint> playerCars = new Dictionary<string, uint>();

            public override void SaveData()
            {
                Interface.Oxide.DataFileSystem.WriteObject(pluginInstance.Name, this);
            }

            public void RegisterCar(string userID, ModularCar car)
            {
                playerCars.Add(userID, car.net.ID);
                SaveData();
            }

            public void UnregisterCar(string userID)
            {
                playerCars.Remove(userID);
                SaveData();
            }
        }

        private PlayerConfig GetPlayerConfig(IPlayer player) =>
            GetPlayerConfig(player.Id);

        private PlayerConfig GetPlayerConfig(string userID)
        {
            if (PlayerConfigsMap.ContainsKey(userID))
                return PlayerConfigsMap[userID];

            PlayerConfig config = PlayerConfig.Get(Name, userID);
            PlayerConfigsMap.Add(userID, config);
            return config;
        }

        internal abstract class SimplePresetManager
        {
            public static Func<SimplePreset, bool> MatchPresetName(string presetName) =>
                new Func<SimplePreset, bool>(preset => preset.Name.Equals(presetName, StringComparison.CurrentCultureIgnoreCase));

            [JsonProperty("Presets")]
            public List<SimplePreset> Presets = new List<SimplePreset>();

            public SimplePreset FindPreset(string presetName) =>
                Presets.FirstOrDefault(MatchPresetName(presetName));

            public List<SimplePreset> FindMatchingPresets(string presetName) =>
                Presets.Where(preset => preset.Name.IndexOf(presetName, StringComparison.CurrentCultureIgnoreCase) >= 0).ToList();

            public bool HasPreset(string presetName) =>
                Presets.Any(MatchPresetName(presetName));

            public void SavePreset(SimplePreset newPreset)
            {
                Presets.Add(newPreset);
                SaveData();
            }

            public void UpdatePreset(SimplePreset newPreset)
            {
                var presetIndex = Presets.FindIndex(new Predicate<SimplePreset>(MatchPresetName(newPreset.Name)));
                if (presetIndex == -1) return;
                Presets[presetIndex] = newPreset;
                SaveData();
            }

            public void RenamePreset(SimplePreset preset, string newName)
            {
                preset.Name = newName;
                SaveData();
            }

            public void DeletePreset(SimplePreset preset)
            {
                Presets.Remove(preset);
                SaveData();
            }

            public abstract void SaveData();
        }

        internal class PlayerConfig : SimplePresetManager
        {
            public static PlayerConfig Get(string dirPath, string ownerID)
            {
                var filepath = $"{dirPath}/{ownerID}";

                var config = Interface.Oxide.DataFileSystem.ExistsDatafile(filepath) ?
                    Interface.Oxide.DataFileSystem.ReadObject<PlayerConfig>(filepath) :
                    new PlayerConfig(ownerID);

                config.Filepath = filepath;
                return config;
            }

            [JsonIgnore]
            private string Filepath;

            [JsonProperty("OwnerID")]
            public string OwnerID { get; private set; }

            [JsonProperty("Settings")]
            public PlayerSettings Settings = new PlayerSettings();

            public PlayerConfig(string ownerID)
            {
                OwnerID = ownerID;
            }

            public override void SaveData() =>
                Interface.Oxide.DataFileSystem.WriteObject(Filepath, this);
        }

        internal class SimplePreset
        {
            public static SimplePreset FromCar(ModularCar car, string presetName)
            {
                return new SimplePreset
                {
                    Name = presetName,
                    ModuleIDs = pluginInstance.GetCarModuleIDs(car)
                };
            }

            [JsonProperty("Name")]
            public string Name;

            [JsonProperty("ModuleIDs")]
            public int[] ModuleIDs;

            [JsonIgnore]
            public int NumSockets
            {
                get { return ModuleIDs.Length; }
            }
        }

        internal class PlayerSettings
        {
            [JsonProperty("AutoCodeLock")]
            public bool AutoCodeLock = false;

            [JsonProperty("AutoKeyLock")]
            public bool AutoKeyLock = false;

            [JsonProperty("AutoFillTankers")]
            public bool AutoFillTankers = false;
        }

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig() => Config.WriteObject(GetDefaultConfig(), true);

        private PluginConfig GetDefaultConfig() => new PluginConfig();

        internal class PluginConfig
        {
            [JsonProperty("CanSpawnWhileBuildingBlocked")]
            public bool CanSpawnBuildingBlocked = false;

            [JsonProperty("CanFetchWhileBuildingBlocked")]
            public bool CanFetchBuildingBlocked = false;

            [JsonProperty("CanFetchWhileOccupied")]
            public bool CanFetchOccupied = false;

            [JsonProperty("CanDespawnWhileOccupied")]
            public bool CanDespawnOccupied = false;

            [JsonProperty("Presets")]
            public ServerPreset[] Presets = new ServerPreset[0];

            [JsonProperty("Cooldowns")]
            public CooldownConfig Cooldowns = new CooldownConfig();

            [JsonProperty("DeleteMatchingKeysFromPlayerInventoryOnDespawn")]
            public bool DeleteKeyOnDespawn = true;

            [JsonProperty("DismountPlayersOnFetch")]
            public bool DismountPlayersOnFetch = true;

            [JsonProperty("EnableEffects")]
            public bool EnableEffects = true;

            [JsonProperty("FreshWaterAmount")]
            public int FreshWaterAmount = -1;

            [JsonProperty("FuelAmount")]
            public int FuelAmount = -1;

            [JsonProperty("MaxPresetsPerPlayer")]
            public int MaxPresetsPerPlayer = 10;
        }

        internal class ServerPreset
        {
            [JsonProperty("Name")]
            public string Name;

            [JsonProperty("Options")]
            public ServerPresetOptions Options;
        }

        internal abstract class BaseCarOptions
        {
            private int _enginePartsTier = 0;

            [JsonProperty("CodeLock")]
            public bool CodeLock = false;

            [JsonProperty("EnginePartsTier")]
            public int EnginePartsTier
            {
                get { return _enginePartsTier; }
                set { _enginePartsTier = pluginInstance.Clamp(value, 0, 3); }
            }

            [JsonProperty("FreshWaterAmount")]
            public int FreshWaterAmount = 0;

            [JsonProperty("FuelAmount")]
            public int FuelAmount = 0;

            [JsonProperty("KeyLock")]
            public bool KeyLock = false;

            [JsonIgnore]
            public abstract int Length { get; }

            public BaseCarOptions() { }

            public BaseCarOptions(string userID)
            {
                CodeLock = pluginInstance.ShouldTryAddCodeLockForPlayer(userID);
                KeyLock = pluginInstance.ShouldTryAddKeyLockForPlayer(userID);
                EnginePartsTier = pluginInstance.GetPlayerEnginePartsTier(userID);
                FuelAmount = pluginInstance.GetPlayerAllowedFuel(userID);
                FreshWaterAmount = pluginInstance.GetPlayerAllowedFreshWater(userID);
            }
        }

        internal class PresetCarOptions : BaseCarOptions
        {
            [JsonProperty("ModuleIDs")]
            public virtual int[] ModuleIDs { get; set; } = new int[0];

            [JsonIgnore]
            public override int Length
            {
                get { return ModuleIDs.Length; }
            }

            // Empty constructor needed for deserialization
            public PresetCarOptions() { }

            public PresetCarOptions(string userID, int[] moduleIDs) : base(userID)
            {
                ModuleIDs = moduleIDs;
            }
        }

        internal class RandomCarOptions : BaseCarOptions
        {
            public int NumSockets;

            public override int Length
            {
                get { return NumSockets; }
            }

            public RandomCarOptions(string userID, int numSockets) : base(userID)
            {
                NumSockets = numSockets;
            }
        }

        internal class ServerPresetOptions : PresetCarOptions
        {
            private int[] _normalizedModuleIDs = new int[0];

            public override int[] ModuleIDs
            {
                get { return _normalizedModuleIDs; }
                // Legacy field
                set { _normalizedModuleIDs = NormalizeModuleIDs(value); }
            }

            [JsonProperty("Modules")]
            public object[] Modules
            {
                set { _normalizedModuleIDs = pluginInstance.ParseModules(value); }
            }

            private int[] NormalizeModuleIDs(int[] moduleIDs)
            {
                var moduleIDList = moduleIDs.ToList();

                for (var i = 0; i < moduleIDList.Count; i++)
                {
                    if (moduleIDList[i] != 0)
                    {
                        // Add a 0 after each module that takes 2 sockets
                        // This is more user-friendly than having people add the 0s themselves
                        var itemDefinition = ItemManager.FindItemDefinition(moduleIDList[i]);
                        var socketsTaken = itemDefinition.GetComponent<ItemModVehicleModule>()?.SocketsTaken ?? 1;
                        if (socketsTaken == 2)
                            moduleIDList.Insert(i + 1, 0);
                    }
                }

                return moduleIDList.ToArray();
            }
        }

        internal class CooldownConfig
        {
            [JsonProperty("SpawnCarSeconds")]
            public float SpawnSeconds = 10;

            [JsonProperty("FetchCarSeconds")]
            public float FetchSeconds = 10;

            [JsonProperty("LoadPresetSeconds")]
            public float LoadPresetSeconds = 10;

            [JsonProperty("FixCarSeconds")]
            public float FixSeconds = 60;
        }

        #endregion

        #region Localization

        private string BooleanToLocalizedString(IPlayer player, bool value) =>
            value ? GetMessage(player, "Generic.Setting.On") : GetMessage(player, "Generic.Setting.Off");

        private void ReplyToPlayer(IPlayer player, string messageName, params object[] args) =>
            player.Reply(string.Format(GetMessage(player, messageName), args));

        private void ChatMessage(BasePlayer player, string messageName, params object[] args) =>
            player.ChatMessage(string.Format(GetMessage(player.IPlayer, messageName), args));

        private string GetMessage(IPlayer player, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, player.Id);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Generic.Setting.On"] = "<color=yellow>ON</color>",
                ["Generic.Setting.Off"] = "<color=#bbb>OFF</color>",

                ["Generic.Error.NoPermission"] = "You don't have permission to use this command.",
                ["Generic.Error.LocationRestricted"] = "Error: Cannot do that here.",
                ["Generic.Error.BuildingBlocked"] = "Error: Cannot do that while building blocked.",
                ["Generic.Error.NoPresets"] = "You don't have any saved presets.",
                ["Generic.Error.NoCommonPresets"] = "There are no common presets.",
                ["Generic.Error.CarNotFound"] = "Error: You need a car to do that.",
                ["Generic.Error.CarOccupied"] = "Error: Cannot do that while your car is occupied.",
                ["Generic.Error.CarDead"] = "Error: Your car is dead.",
                ["Generic.Error.Cooldown"] = "Please wait <color=yellow>{0}s</color> and try again.",
                ["Generic.Error.NoPermissionToPresetSocketCount"] = "Error: You don't have permission to use preset <color=yellow>{0}</color> because it requires <color=yellow>{1}</color> sockets.",
                ["Generic.Error.PresetNotFound"] = "Error: Preset <color=yellow>{0}</color> not found.",
                ["Generic.Error.PresetMultipleMatches"] = "Error: Multiple presets found matching <color=yellow>{0}</color>. Use <color=yellow>mycar list</color> to view your presets.",
                ["Generic.Error.PresetAlreadyTaken"] = "Error: Preset <color=yellow>{0}</color> is already taken.",
                ["Generic.Error.PresetNameLength"] = "Error: Preset name may not be longer than {0} characters.",

                ["Generic.Info.CarDestroyed"] = "Your modular car was destroyed.",
                ["Generic.Info.PartsRecovered"] = "Recovered engine components were added to your inventory or dropped in front of you.",

                ["Command.Spawn.Error.SocketSyntax"] = "Syntax: <color=yellow>mycar <2|3|4></color>",
                ["Command.Spawn.Error.CarAlreadyExists"] = "Error: You already have a car.",
                ["Command.Spawn.Error.CarAlreadyExists.Help"] = "Try <color=yellow>mycar fetch</color> or <color=yellow>mycar help</color>.",
                ["Command.Spawn.Success"] = "Here is your modular car.",
                ["Command.Spawn.Success.Locked"] = "A matching key was added to your inventory.",
                ["Command.Spawn.Success.Preset"] = "Here is your modular car from preset <color=yellow>{0}</color>.",

                ["Command.Fix.Success"] = "Your car was fixed.",
                ["Command.Fetch.Error.StuckOnLift"] = "Error: Unable to fetch your car from its lift.",
                ["Command.Fetch.Error.StuckOnLift.Help"] = "You can use <color=yellow>mycar destroy</color> to destroy it.",
                ["Command.Fetch.Success"] = "Here is your modular car.",

                ["Command.SavePreset.Error.TooManyPresets"] = "Error: You may not have more than <color=yellow>{0}</color> presets. You may delete another preset and try again. See <color=yellow>mycar help</color>.",
                ["Command.SavePreset.Error.PresetAlreadyExists"] = "Error: Preset <color=yellow>{0}</color> already exists. Use <color=yellow>mycar update {0}</color> to update it.",
                ["Command.SavePreset.Success"] = "Saved car as <color=yellow>{0}</color> preset.",
                ["Command.UpdatePreset.Success"] = "Updated <color=yellow>{0}</color> preset with current module configuration.",
                ["Command.LoadPreset.Error.SocketCount"] = "Error: Unable to load <color=yellow>{0}</color> preset (<color=yellow>{1}</color> sockets) because your car has <color=yellow>{2}</color> sockets.",
                ["Command.LoadPreset.Success"] = "Loaded <color=yellow>{0}</color> preset onto your car.",
                ["Command.DeletePreset.Success"] = "Deleted <color=yellow>{0}</color> preset.",
                ["Command.RenamePreset.Error.Syntax"] = "Syntax: <color=yellow>mycar rename <name> <new_name></color>",
                ["Command.RenamePreset.Success"] = "Renamed <color=yellow>{0}</color> preset to <color=yellow>{1}</color>",
                ["Command.List"] = "Your saved modular car presets:",
                ["Command.List.Item"] = "<color=yellow>{0}</color> ({1} sockets)",

                ["Command.Common.List"] = "Common modular car presets:",
                ["Command.Common.Error.Syntax"] = "Try <color=yellow>mycar help</color>",
                ["Command.Common.LoadPreset.Error.Syntax"] = "Syntax: <color=yellow>mycar common load <name></color>",
                ["Command.Common.SavePreset.Error.Syntax"] = "Syntax: <color=yellow>mycar common save <name></color>",
                ["Command.Common.SavePreset.Error.PresetAlreadyExists"] = "Error: Common preset <color=yellow>{0}</color> already exists. Use <color=yellow>mycar common update {0}</color> to update it.",
                ["Command.Common.UpdatePreset.Error.Syntax"] = "Syntax: <color=yellow>mycar common update <name></color>",
                ["Command.Common.RenamePreset.Error.Syntax"] = "Syntax: <color=yellow>mycar common rename <name> <new_name></color>",
                ["Command.Common.DeletePreset.Error.Syntax"] = "Syntax: <color=yellow>mycar common delete <name></color>",

                ["Command.ToggleAutoCodeLock.Success"] = "<color=yellow>AutoCodeLock</color> set to {0}",
                ["Command.ToggleAutoKeyLock.Success"] = "<color=yellow>AutoKeyLock</color> set to {0}",
                ["Command.ToggleAutoFillTankers.Success"] = "<color=yellow>AutoFillTankers</color> set to {0}",

                ["Command.Give.Error.Syntax"] = "Syntax: <color=yellow>givecar <player> <preset></color>",
                ["Command.Give.Error.PlayerNotFound"] = "Error: Player <color=yellow>{0}</color> not found.",
                ["Command.Give.Error.PresetTooFewModules"] = "Error: Preset <color=yellow>{0}</color> has too few modules ({1}).",
                ["Command.Give.Error.PresetTooManyModules"] = "Error: Preset <color=yellow>{0}</color> has too many modules ({1}).",
                ["Command.Give.Success"] = "Modular car given to <color=yellow>{0}</color> from preset <color=yellow>{1}</color>.",

                ["Command.Help"] = "<color=orange>SpawnModularCar Command Usages</color>",
                ["Command.Help.Spawn.Basic"] = "<color=yellow>mycar</color> - Spawn a random car with max allowed sockets",
                ["Command.Help.Spawn.Basic.PresetsAllowed"] = "<color=yellow>mycar</color> - Spawn a car using your <color=yellow>default</color> preset if saved, else spawn a random car with max allowed sockets",
                ["Command.Help.Spawn.Sockets"] = "<color=yellow>mycar <2|3|4></color> - Spawn a random car of desired length",
                ["Command.Help.Fetch"] = "<color=yellow>mycar fetch</color> - Fetch your car",
                ["Command.Help.Fix"] = "<color=yellow>mycar fix</color> - Fix your car",
                ["Command.Help.Destroy"] = "<color=yellow>mycar destroy</color> - Destroy your car",

                ["Command.Help.Section.PersonalPresets"] = "<color=orange>--- Personal presets ---</color>",
                ["Command.Help.ListPresets"] = "<color=yellow>mycar list</color> - List your saved presets",
                ["Command.Help.Spawn.Preset"] = "<color=yellow>mycar <name></color> - Spawn a car from a saved preset",
                ["Command.Help.LoadPreset"] = "<color=yellow>mycar load <name></color> - Load a preset onto your car",
                ["Command.Help.SavePreset"] = "<color=yellow>mycar save <name></color> - Save your car as a preset",
                ["Command.Help.UpdatePreset"] = "<color=yellow>mycar update <name></color> - Overwrite a preset",
                ["Command.Help.RenamePreset"] = "<color=yellow>mycar rename <name> <new_name></color> - Rename a preset",
                ["Command.Help.DeletePreset"] = "<color=yellow>mycar delete <name></color> - Delete a preset",

                ["Command.Help.Section.CommonPresets"] = "<color=orange>--- Common presets ---</color>",
                ["Command.Help.Common.ListPresets"] = "<color=yellow>mycar common list</color> - List common presets",
                ["Command.Help.Common.Spawn"] = "<color=yellow>mycar common <name></color> - Spawn a car from a common preset",
                ["Command.Help.Common.LoadPreset"] = "<color=yellow>mycar common load <name></color> - Load a common preset onto your car",
                ["Command.Help.Common.SavePreset"] = "<color=yellow>mycar common save <name></color> - Save your car as a common preset",
                ["Command.Help.Common.UpdatePreset"] = "<color=yellow>mycar common update <name></color> - Overwrite a common preset",
                ["Command.Help.Common.RenamePreset"] = "<color=yellow>mycar common rename <name> <new_name></color> - Rename a common preset",
                ["Command.Help.Common.DeletePreset"] = "<color=yellow>mycar common delete <name></color> - Delete a common preset",

                ["Command.Help.Section.PersonalSettings"] = "<color=orange>--- Personal settings ---</color>",
                ["Command.Help.ToggleAutoCodeLock"] = "<color=yellow>mycar autocodelock</color> - Toggle AutoCodeLock: {0}",
                ["Command.Help.ToggleAutoKeyLock"] = "<color=yellow>mycar autokeylock</color> - Toggle AutoKeyLock: {0}",
                ["Command.Help.ToggleAutoFillTankers"] = "<color=yellow>mycar autofilltankers</color> - Toggle automatic filling of tankers with fresh water: {0}",

                ["Command.Help.Section.OtherCommands"] = "<color=orange>--- Other commands ---</color>",
                ["Command.Help.Give"] = "<color=yellow>givecar <player> <preset></color> - Spawn a car for the target player from the specified server preset",
            }, this);
        }

        #endregion
    }
}
