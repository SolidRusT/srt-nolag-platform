using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust;
using Rust.Modular;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text;

namespace Oxide.Plugins
{
    [Info("Spawn Modular Car", "WhiteThunder", "5.0.1")]
    [Description("Allows players to spawn modular cars.")]
    internal class SpawnModularCar : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin CarCodeLocks, VehicleDeployedLocks;

        private static SpawnModularCar _pluginInstance;
        private static Configuration _pluginConfig;

        private PluginData _pluginData;
        private CommonPresets _commonPresets;

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

        // These layers are used to preventing spawning inside walls or players.
        private const int BoxcastLayers = Layers.Mask.Default
            + Layers.Mask.Deployed
            + Layers.Mask.Player_Server
            + Layers.Mask.AI
            + Layers.Mask.Vehicle_Detailed
            + Layers.Mask.Vehicle_World
            + Layers.Mask.World
            + Layers.Mask.Construction
            + Layers.Mask.Tree;

        // These layers are used to find a surface to spawn on.
        private const int RaycastLayers = Layers.Mask.Default
            + Layers.Mask.Terrain
            + Layers.World
            + Layers.Mask.Construction;

        private static readonly Vector3 ShortCarExtents = new Vector3(1, 1.1f, 1.5f);
        private static readonly Vector3 MediumCarExtents = new Vector3(1, 1.1f, 2.3f);
        private static readonly Vector3 LongCarExtents = new Vector3(1, 1.1f, 3);

        private static readonly Vector3 ShortCarFrontLeft = new Vector3(ShortCarExtents.x, 0, ShortCarExtents.z);
        private static readonly Vector3 ShortCarFrontRight = new Vector3(-ShortCarExtents.x, 0, ShortCarExtents.z);
        private static readonly Vector3 ShortCarBackLeft = new Vector3(ShortCarExtents.x, 0, -ShortCarExtents.z);
        private static readonly Vector3 ShortCarBackRight = new Vector3(-ShortCarExtents.x, 0, -ShortCarExtents.z);

        private static readonly Vector3 MediumCarFrontLeft = new Vector3(MediumCarExtents.x, 0, MediumCarExtents.z);
        private static readonly Vector3 MediumCarFrontRight = new Vector3(-MediumCarExtents.x, 0, MediumCarExtents.z);
        private static readonly Vector3 MediumCarBackLeft = new Vector3(MediumCarExtents.x, 0, -MediumCarExtents.z);
        private static readonly Vector3 MediumCarBackRight = new Vector3(-MediumCarExtents.x, 0, -MediumCarExtents.z);

        private static readonly Vector3 LongCarFrontLeft = new Vector3(LongCarExtents.x, 0, LongCarExtents.z);
        private static readonly Vector3 LongCarFrontRight = new Vector3(-LongCarExtents.x, 0, LongCarExtents.z);
        private static readonly Vector3 LongCarBackLeft = new Vector3(LongCarExtents.x, 0, -LongCarExtents.z);
        private static readonly Vector3 LongCarBackRight = new Vector3(-LongCarExtents.x, 0, -LongCarExtents.z);

        private static readonly float ForwardRaycastDistance = 1.5f + ShortCarExtents.x;
        private const float DownwardRaycastDistance = 4;

        private readonly RaycastHit[] _raycastBuffer = new RaycastHit[1];

        private readonly Dictionary<string, PlayerConfig> _playerConfigsMap = new Dictionary<string, PlayerConfig>();

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginInstance = this;

            _pluginData = PluginData.LoadData();
            _commonPresets = CommonPresets.LoadData(_pluginData);

            MigrateConfig();

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
        }

        private void Unload()
        {
            _pluginInstance = null;
            _pluginConfig = null;
        }

        private void OnNewSave(string filename)
        {
            _pluginData.PlayerCars.Clear();
            _pluginData.Cooldowns.ClearAll();
            _pluginData.SaveData();
        }

        private void OnEntityKill(ModularCar car)
        {
            if (!IsPlayerCar(car))
                return;

            string userId = _pluginData.PlayerCars.FirstOrDefault(x => x.Value == car.net.ID).Key;
            BasePlayer player = BasePlayer.Find(userId);

            if (player != null)
                ChatMessage(player, "Generic.Info.CarDestroyed");

            _pluginData.UnregisterCar(userId);
        }

        private object OnEngineStart(ModularCar car)
        {
            if (car == null
                || car.OwnerID == 0
                || !_pluginData.PlayerCars.ContainsValue(car.net.ID)
                || !permission.UserHasPermission(car.OwnerID.ToString(), PermissionAutoStartEngine))
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

            if (SpawnWasBlocked(player))
                return null;

            var presetOptions = apiOptions.ToPresetOptions();

            Vector3 spawnPosition;
            Quaternion rotation;
            if (!TryGetIdealCarPositionAndRotation(player, presetOptions.Length, out spawnPosition, out rotation))
            {
                spawnPosition = GetFixedCarPosition(player);
                rotation = GetRelativeCarRotation(player);
            }

            var car = SpawnCarForPlayer(player, presetOptions, spawnPosition, rotation, shouldTrackCar: false);
            if (car != null)
            {
                // Note: Consumers no longer need to use this callback since this plugin now forces synchronous module registration.
                onReady?.Invoke(car);
            }

            return car;
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
                if (!options.ContainsKey(ModulesField))
                    return null;

                var moduleArray = options[ModulesField] as object[];
                if (moduleArray == null)
                    return null;

                return _pluginInstance.ValidateModules(moduleArray);
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
                    NormalizedModuleIDs = ModuleIDs
                };
            }
        }

        #endregion

        #region Commands

        [Command("givecar")]
        private void SpawnCarServerCommand(IPlayer player, string cmd, string[] args)
        {
            if (!player.IsServer && !VerifyPermissionAny(player, PermissionGiveCar))
                return;

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

            foreach (var preset in _pluginConfig.Presets)
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

                    Vector3 spawnPosition;
                    Quaternion rotation;
                    if (!TryGetIdealCarPositionAndRotation(targetPlayer, preset.Options.Length, out spawnPosition, out rotation))
                    {
                        spawnPosition = GetFixedCarPosition(targetPlayer);
                        rotation = GetRelativeCarRotation(targetPlayer);
                    }

                    var car = SpawnCarForPlayer(targetPlayer, carOptions, spawnPosition, rotation, shouldTrackCar: false);
                    if (car != null)
                        ReplyToPlayer(player, "Command.Give.Success", targetPlayer.displayName, preset.Name);

                    return;
                }
            }

            ReplyToPlayer(player, "Generic.Error.PresetNotFound", presetNameArg);
        }

        [Command("mycar")]
        private void MyCarCommand(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer)
                return;

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

            var canCodeLock = VehicleDeployedLocks != null && permission.UserHasPermission(player.Id, PermissionAutoCodeLock);
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

            if (!VerifyHasNoCar(player)
                || !VerifyOffCooldown(player, CooldownType.Spawn)
                || !VerifyLocationNotRestricted(player)
                || !_pluginConfig.CanSpawnBuildingBlocked && !VerifyNotBuildingBlocked(player))
                return;

            // Key binds automatically pass the "True" argument.
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

                if (!VerifyPermissionAny(player, PermissionPresets))
                    return;

                var presetNameArg = args[0];

                SimplePreset preset;
                if (!VerifyOnlyOneMatchingPreset(player, GetPlayerConfig(player), presetNameArg, out preset))
                    return;

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

            if (!VerifyPermissionAny(player, PermissionCommonPresets)
                || !VerifyHasNoCar(player)
                || !VerifyOffCooldown(player, CooldownType.Spawn)
                || !VerifyLocationNotRestricted(player)
                || !_pluginConfig.CanSpawnBuildingBlocked && !VerifyNotBuildingBlocked(player))
                return;

            var presetNameArg = args[0];

            SimplePreset preset;
            if (!VerifyOnlyOneMatchingPreset(player, _commonPresets, presetNameArg, out preset))
                return;

            SpawnPresetCarForPlayer(player, preset);
        }

        private void SubCommand_FixCar(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionFix))
                return;

            ModularCar car;
            if (!VerifyHasCar(player, out car)
                || !VerifyOffCooldown(player, CooldownType.Fix)
                || FixMyCarWasBlocked(player.Object as BasePlayer, car))
                return;

            if (car.IsDead())
                ReviveCar(car);

            FixCar(car, GetPlayerAllowedFuel(player.Id), GetPlayerEnginePartsTier(player.Id));
            MaybeFillTankerModules(car, GetPlayerAllowedFreshWater(player.Id));
            _pluginData.StartCooldown(player.Id, CooldownType.Fix);

            MaybePlayCarRepairEffects(car);
            ReplyToPlayer(player, "Command.Fix.Success");
        }

        private void SubCommand_FetchCar(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionFetch))
                return;

            var basePlayer = player.Object as BasePlayer;
            ModularCar car;
            Vector3 fetchPosition;
            Quaternion fetchRotation;

            if (!VerifyHasCar(player, out car)
                || !_pluginConfig.CanFetchOccupied && !VerifyCarNotOccupied(player, car)
                || !VerifyOffCooldown(player, CooldownType.Fetch)
                || !VerifyLocationNotRestricted(player)
                || !_pluginConfig.CanFetchBuildingBlocked && !VerifyNotBuildingBlocked(player)
                || !VerifySufficientSpace(player, car.TotalSockets, out fetchPosition, out fetchRotation)
                || FetchMyCarWasBlocked(basePlayer, car))
                return;

            // This is a hacky way to determine that the car is on a lift.
            if (car.rigidBody.isKinematic && !TryReleaseCarFromLift(car))
            {
                var messages = new List<string> { GetMessage(player, "Command.Fetch.Error.StuckOnLift") };
                if (permission.UserHasPermission(player.Id, PermissionDespawn))
                    messages.Add(GetMessage(player, "Command.Fetch.Error.StuckOnLift.Help"));

                player.Reply(string.Join(" ", messages));
                return;
            }

            if (_pluginConfig.DismountPlayersOnFetch)
                DismountAllPlayersFromCar(car);

            // Temporarily clear max angular velocity to prevent the car from unexpectedly spinning when teleporting really far.
            var maxAngularVelocity = car.rigidBody.maxAngularVelocity;
            car.rigidBody.maxAngularVelocity = 0;

            car.transform.SetPositionAndRotation(fetchPosition, fetchRotation);
            car.SetVelocity(Vector3.zero);
            car.SetAngularVelocity(Vector3.zero);
            car.UpdateNetworkGroup();
            car.SendNetworkUpdateImmediate();
            timer.Once(1f, () =>
            {
                if (car != null)
                    car.rigidBody.maxAngularVelocity = maxAngularVelocity;
            });

            _pluginData.StartCooldown(player.Id, CooldownType.Fetch);
            ReplyToPlayer(player, "Command.Fetch.Success");
        }

        private void SubCommand_DestroyCar(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionDespawn))
                return;

            var basePlayer = player.Object as BasePlayer;
            ModularCar car;

            if (!VerifyHasCar(player, out car)
                || !_pluginConfig.CanDespawnOccupied && !VerifyCarNotOccupied(player, car)
                || DestroyMyCarWasBlocked(basePlayer, car))
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
            if (!VerifyPermissionAny(player, PermissionPresets))
                return;

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
            if (!VerifyPermissionAny(player, PermissionCommonPresets))
                return;

            if (_commonPresets.Presets.Count == 0)
            {
                ReplyToPlayer(player, "Generic.Error.NoCommonPresets");
                return;
            }

            ushort maxAllowedSockets = GetPlayerMaxAllowedCarSockets(player.Id);

            var presetList = _commonPresets.Presets.Where(p => p.NumSockets <= maxAllowedSockets).ToList();
            presetList.Sort(SortPresetNames);

            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, "Command.Common.List"));

            foreach (var preset in presetList)
                sb.AppendLine(GetMessage(player, "Command.List.Item", preset.Name, preset.NumSockets));

            player.Reply(sb.ToString());
        }

        private void SubCommand_SavePreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionPresets))
                return;

            ModularCar car;
            if (!VerifyHasCar(player, out car))
                return;

            var presetNameArg = args.Length == 0 ? DefaultPresetName : args[0];

            var presetManager = GetPlayerConfig(player);
            if (!VerifyNoMatchingPreset(player, presetManager, presetNameArg))
                return;

            if (presetManager.Presets.Count >= _pluginConfig.MaxPresetsPerPlayer)
            {
                ReplyToPlayer(player, "Command.SavePreset.Error.TooManyPresets", _pluginConfig.MaxPresetsPerPlayer);
                return;
            }

            SavePreset(player, presetManager, presetNameArg, car);
        }

        private void SubCommand_Common_SavePreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionManageCommonPresets))
                return;

            if (args.Length == 0)
            {
                ReplyToPlayer(player, "Command.Common.SavePreset.Error.Syntax");
                return;
            }

            var presetNameArg = args[0];

            ModularCar car;
            if (!VerifyHasCar(player, out car)
                || !VerifyNoMatchingPreset(player, _commonPresets, presetNameArg))
                return;

            SavePreset(player, _commonPresets, presetNameArg, car);
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
            if (!VerifyPermissionAny(player, PermissionPresets))
                return;

            var presetNameArg = args.Length == 0 ? DefaultPresetName : args[0];
            UpdatePreset(player, GetPlayerConfig(player), presetNameArg);
        }

        private void SubCommand_Common_UpdatePreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionManageCommonPresets))
                return;

            if (args.Length == 0)
            {
                ReplyToPlayer(player, "Command.Common.UpdatePreset.Error.Syntax");
                return;
            }

            UpdatePreset(player, _commonPresets, args[0]);
        }

        private void UpdatePreset(IPlayer player, SimplePresetManager presetManager, string presetNameArg)
        {
            ModularCar car;
            if (!VerifyHasCar(player, out car))
                return;

            SimplePreset preset;
            if (!VerifyHasPreset(player, presetManager, presetNameArg, out preset))
                return;

            presetManager.UpdatePreset(SimplePreset.FromCar(car, preset.Name));
            ReplyToPlayer(player, "Command.UpdatePreset.Success", preset.Name);
        }

        private void SubCommand_LoadPreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionPresetLoad))
                return;

            var presetNameArg = args.Length == 0 ? DefaultPresetName : args[0];
            LoadPreset(player, GetPlayerConfig(player.Id), presetNameArg);
        }

        private void SubCommand_Common_LoadPreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionPresetLoad)
                || !VerifyPermissionAny(player, PermissionCommonPresets))
                return;

            if (args.Length == 0)
            {
                ReplyToPlayer(player, "Command.Common.LoadPreset.Error.Syntax");
                return;
            }

            var presetNameArg = args[0];
            LoadPreset(player, _commonPresets, presetNameArg);
        }

        private void LoadPreset(IPlayer player, SimplePresetManager presetManager, string presetNameArg)
        {
            var basePlayer = player.Object as BasePlayer;
            ModularCar car;

            if (!VerifyHasCar(player, out car)
                || !VerifyCarNotOccupied(player, car)
                || !VerifyOffCooldown(player, CooldownType.Load)
                || LoadMyCarPresetWasBlocked(basePlayer, car))
                return;

            SimplePreset preset;
            if (!VerifyOnlyOneMatchingPreset(player, presetManager, presetNameArg, out preset))
                return;

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

            if (car.IsDead())
                ReviveCar(car);

            var wasEngineOn = car.IsOn();
            var enginePartsTier = GetPlayerEnginePartsTier(player.Id);
            var extractedEngineParts = ExtractEnginePartsAboveTierAndDeleteRest(car, enginePartsTier);
            UpdateCarModules(car, preset.ModuleIDs);
            _pluginData.StartCooldown(player.Id, CooldownType.Load);

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

                // Restart the engine if it turned off during the brief moment it had no engine or no parts.
                if (wasEngineOn && !car.IsOn() && car.CanRunEngines())
                    car.FinishStartingEngine();

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
            if (!VerifyPermissionAny(player, PermissionPresets))
                return;

            if (args.Length < 2)
            {
                ReplyToPlayer(player, "Command.RenamePreset.Error.Syntax");
                return;
            }

            RenamePreset(player, GetPlayerConfig(player), args[0], args[1]);
        }

        private void SubCommand_Common_RenamePreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionManageCommonPresets))
                return;

            if (args.Length < 2)
            {
                ReplyToPlayer(player, "Command.Common.RenamePreset.Error.Syntax");
                return;
            }

            RenamePreset(player, _commonPresets, args[0], args[1]);
        }

        private void RenamePreset(IPlayer player, SimplePresetManager presetManager, string oldName, string newName)
        {
            SimplePreset preset;
            if (!VerifyHasPreset(player, presetManager, oldName, out preset))
                return;

            // Cache actual old preset name since matching is case-insensitive.
            var actualOldPresetName = preset.Name;

            SimplePreset existingPresetWithNewName = presetManager.FindPreset(newName);

            if (newName.Length > PresetMaxLength)
            {
                ReplyToPlayer(player, "Generic.Error.PresetNameLength", PresetMaxLength);
                return;
            }

            // Allow renaming if just changing case.
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
            if (!VerifyPermissionAny(player, PermissionPresets))
                return;

            var presetNameArg = args.Length == 0 ? DefaultPresetName : args[0];
            DeletePreset(player, GetPlayerConfig(player), presetNameArg);
        }

        private void SubCommand_Common_DeletePreset(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionManageCommonPresets))
                return;

            if (args.Length == 0)
            {
                ReplyToPlayer(player, "Command.Common.DeletePreset.Error.Syntax");
                return;
            }

            DeletePreset(player, _commonPresets, args[0]);
        }

        private void DeletePreset(IPlayer player, SimplePresetManager presetManager, string presetNameArg)
        {
            SimplePreset preset;
            if (!VerifyHasPreset(player, presetManager, presetNameArg, out preset))
                return;

            presetManager.DeletePreset(preset);
            ReplyToPlayer(player, "Command.DeletePreset.Success", preset.Name);
        }

        private void SubCommand_ToggleAutoCodeLock(IPlayer player, string[] args)
        {
            if (VehicleDeployedLocks == null
                || !VerifyPermissionAny(player, PermissionAutoCodeLock))
                return;

            var config = GetPlayerConfig(player);
            config.Settings.AutoCodeLock = !config.Settings.AutoCodeLock;
            config.SaveData();
            ReplyToPlayer(player, "Command.ToggleAutoCodeLock.Success", BooleanToLocalizedString(player, config.Settings.AutoCodeLock));
        }

        private void SubCommand_ToggleAutoKeyLock(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionAutoKeyLock))
                return;

            var config = GetPlayerConfig(player);
            config.Settings.AutoKeyLock = !config.Settings.AutoKeyLock;
            config.SaveData();
            ReplyToPlayer(player, "Command.ToggleAutoKeyLock.Success", BooleanToLocalizedString(player, config.Settings.AutoKeyLock));
        }

        private void SubCommand_ToggleAutoFillTankers(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, PermissionAutoFillTankers))
                return;

            var config = GetPlayerConfig(player);
            config.Settings.AutoFillTankers = !config.Settings.AutoFillTankers;
            config.SaveData();
            ReplyToPlayer(player, "Command.ToggleAutoFillTankers.Success", BooleanToLocalizedString(player, config.Settings.AutoFillTankers));
        }

        #endregion

        #region Helper Methods - Command Checks

        private static bool SpawnWasBlocked(BasePlayer player)
        {
            object hookResult = Interface.CallHook("CanSpawnModularCar", player);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool SpawnMyCarWasBlocked(BasePlayer player)
        {
            if (SpawnWasBlocked(player))
                return true;

            object hookResult = Interface.CallHook("CanSpawnMyCar", player);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool FetchMyCarWasBlocked(BasePlayer player, ModularCar car)
        {
            object hookResult = Interface.CallHook("CanFetchMyCar", player, car);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool FixMyCarWasBlocked(BasePlayer player, ModularCar car)
        {
            object hookResult = Interface.CallHook("CanFixMyCar", player, car);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool LoadMyCarPresetWasBlocked(BasePlayer player, ModularCar car)
        {
            object hookResult = Interface.CallHook("CanLoadMyCarPreset", player, car);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool DestroyMyCarWasBlocked(BasePlayer player, ModularCar car)
        {
            object hookResult = Interface.CallHook("CanDestroyMyCar", player, car);
            return hookResult is bool && (bool)hookResult == false;
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

        private bool VerifySufficientSpace(IPlayer player, int numSockets, out Vector3 determinedPosition, out Quaternion determinedRotation)
        {
            var basePlayer = player.Object as BasePlayer;

            if (!TryGetIdealCarPositionAndRotation(basePlayer, numSockets, out determinedPosition, out determinedRotation)
                || !HasSufficientSpace(basePlayer, numSockets, determinedPosition, determinedRotation))
            {
                ReplyToPlayer(player, "Generic.Error.InsufficientSpace");
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
            if (FindPlayerCar(player) == null)
                return true;

            var messages = new List<string> { GetMessage(player, "Command.Spawn.Error.CarAlreadyExists") };
            if (permission.UserHasPermission(player.Id, PermissionFetch))
                messages.Add(GetMessage(player, "Command.Spawn.Error.CarAlreadyExists.Help"));

            player.Reply(string.Join(" ", messages));
            return false;
        }

        private bool VerifyCarNotOccupied(IPlayer player, ModularCar car)
        {
            // Players can either be mounted in seats, or standing on flatbed modules.
            if (car.AnyMounted()
                || car.AttachedModuleEntities.Any(module => module.children.Any(child => child is BasePlayer)))
            {
                ReplyToPlayer(player, "Generic.Error.CarOccupied");
                return false;
            }
            return true;
        }

        private bool VerifyOffCooldown(IPlayer player, CooldownType cooldownType)
        {
            var secondsRemaining = _pluginData.GetRemainingCooldownSeconds(player.Id, cooldownType);
            if (secondsRemaining > 0)
            {
                ReplyToPlayer(player, "Generic.Error.Cooldown", secondsRemaining);
                return false;
            }
            return true;
        }

        private bool VerifyOnlyOneMatchingPreset(IPlayer player, SimplePresetManager presetManager, string presetName, out SimplePreset preset)
        {
            preset = presetManager.FindPreset(presetName);
            if (preset != null)
                return true;

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

        private static int SortPresetNames(SimplePreset a, SimplePreset b) =>
            a.Name.ToLower() == DefaultPresetName ? -1 :
            b.Name.ToLower() == DefaultPresetName ? 1 :
            a.Name.CompareTo(b.Name);

        private static Vector3 GetCarExtents(int numSockets)
        {
            switch (numSockets)
            {
                case 2:
                    return ShortCarExtents;
                case 3:
                    return MediumCarExtents;
                default:
                    return LongCarExtents;
            }
        }

        private static void GetCarFrontBack(int numSockets, out Vector3 frontLeft, out Vector3 frontRight, out Vector3 backLeft, out Vector3 backRight)
        {
            switch (numSockets)
            {
                case 2:
                    frontLeft = ShortCarFrontLeft;
                    frontRight = ShortCarFrontRight;
                    backLeft = ShortCarBackLeft;
                    backRight = ShortCarBackRight;
                    return;
                case 3:
                    frontLeft = MediumCarFrontLeft;
                    frontRight = MediumCarFrontRight;
                    backLeft = MediumCarBackLeft;
                    backRight = MediumCarBackRight;
                    return;
                default:
                    frontLeft = LongCarFrontLeft;
                    frontRight = LongCarFrontRight;
                    backLeft = LongCarBackLeft;
                    backRight = LongCarBackRight;
                    return;
            }
        }

        private static int[] GetCarModuleIDs(ModularCar car)
        {
            var moduleIDs = new List<int>();

            for (int socketIndex = 0; socketIndex < car.TotalSockets; socketIndex++)
            {
                BaseVehicleModule module;
                if (car.TryGetModuleAt(socketIndex, out module) && module.FirstSocketIndex == socketIndex)
                    moduleIDs.Add(module.AssociatedItemDef.itemid);
                else
                    // Use 0 to represent an empty socket.
                    moduleIDs.Add(0);
            }

            return moduleIDs.ToArray();
        }

        private static Vector3 GetPlayerForwardPosition(BasePlayer player)
        {
            Vector3 forward = player.GetNetworkRotation() * Vector3.forward;
            forward.y = 0;
            return forward.normalized;
        }

        // Directly in front of the player.
        private static Vector3 GetFixedCarPosition(BasePlayer player)
        {
            Vector3 forward = GetPlayerForwardPosition(player);
            Vector3 position = player.transform.position + forward * 3f;
            position.y = player.transform.position.y + 1f;
            return position;
        }

        // On surface in front of player.
        private static bool TryGetIdealCarPositionAndRotation(BasePlayer player, int numSockets, out Vector3 position, out Quaternion rotation)
        {
            var carMiddle = player.eyes.position + GetPlayerForwardPosition(player) * ForwardRaycastDistance;

            Vector3 carFrontLeft, carFrontRight, carBackLeft, carBackRight;
            GetCarFrontBack(numSockets, out carFrontLeft, out carFrontRight, out carBackLeft, out carBackRight);

            var initialRotation = GetRelativeCarRotation(player);

            RaycastHit frontLeftHit, frontRightHit, backLeftHit, backRightHit;
            if (!Physics.Raycast(carMiddle + initialRotation * carFrontLeft, Vector3.down, out frontLeftHit, DownwardRaycastDistance, RaycastLayers, QueryTriggerInteraction.Ignore)
                || !Physics.Raycast(carMiddle + initialRotation * carFrontRight, Vector3.down, out frontRightHit, DownwardRaycastDistance, RaycastLayers, QueryTriggerInteraction.Ignore)
                || !Physics.Raycast(carMiddle + initialRotation * carBackLeft, Vector3.down, out backLeftHit, DownwardRaycastDistance, RaycastLayers, QueryTriggerInteraction.Ignore)
                || !Physics.Raycast(carMiddle + initialRotation * carBackRight, Vector3.down, out backRightHit, DownwardRaycastDistance, RaycastLayers, QueryTriggerInteraction.Ignore))
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return false;
            }

            // Rotate the car relative to the hit positions.
            rotation = Quaternion.LookRotation((frontLeftHit.point - backLeftHit.point), Vector3.up)
                * Quaternion.Euler(0, 0, (frontLeftHit.point - frontRightHit.point).y * 30);

            // Spawn in the midpoint between the front and back hits.
            position = Vector3.Lerp(frontLeftHit.point, backRightHit.point, 0.5f);

            return true;
        }

        private static Quaternion GetRelativeCarRotation(BasePlayer player) =>
            Quaternion.Euler(0, player.GetNetworkRotation().eulerAngles.y - 90, 0);

        private static void AddInitialModules(ModularCar car, int[] ModuleIDs)
        {
            for (int socketIndex = 0; socketIndex < car.TotalSockets; socketIndex++)
            {
                var desiredItemID = ModuleIDs[socketIndex];

                // We are using 0 to represent an empty socket which we skip.
                if (desiredItemID != 0)
                {
                    var moduleItem = ItemManager.CreateByItemID(desiredItemID);
                    if (moduleItem != null)
                        car.TryAddModule(moduleItem, socketIndex);
                }
            }
        }

        private static void UpdateCarModules(ModularCar car, int[] moduleIDs)
        {
            // Phase 1: Remove all modules that don't match the desired preset.
            // This is done first since some modules take up two sockets.
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

            // Phase 2: Add the modules that are missing.
            for (int socketIndex = 0; socketIndex < car.TotalSockets; socketIndex++)
            {
                var desiredItemID = moduleIDs[socketIndex];
                var existingItem = car.Inventory.ModuleContainer.GetSlot(socketIndex);

                // We are using 0 to represent an empty socket which we skip.
                if (existingItem == null && desiredItemID != 0)
                {
                    var moduleItem = ItemManager.CreateByItemID(desiredItemID);
                    if (moduleItem != null)
                        car.TryAddModule(moduleItem, socketIndex);
                }
            }
        }

        private static List<Item> AddEngineItemsAndReturnRemaining(ModularCar car, List<Item> engineItems)
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
                if (engineStorage == null)
                    continue;

                for (var slotIndex = 0; slotIndex < engineStorage.inventory.capacity; slotIndex++)
                {
                    var engineItemType = engineStorage.slotTypes[slotIndex];
                    if (!itemsByType.ContainsKey(engineItemType))
                        continue;

                    var itemsOfType = itemsByType[engineItemType];
                    var existingItem = engineStorage.inventory.GetSlot(slotIndex);
                    if (existingItem != null || itemsOfType.Count == 0)
                        continue;

                    itemsOfType[0].MoveToContainer(engineStorage.inventory, slotIndex, allowStack: false);
                    itemsOfType.RemoveAt(0);
                }
            }

            return itemsByType.Values.SelectMany(x => x).ToList();
        }

        private static void AddUpgradeOrRepairEngineParts(EngineStorage engineStorage, int desiredTier)
        {
            var inventory = engineStorage.inventory;
            if (inventory == null)
                return;

            // Ignore if the engine storage is locked, since it must be controlled by another plugin.
            if (inventory.IsLocked())
                return;

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

        private static bool TryAddEngineItem(EngineStorage engineStorage, int slot, int tier)
        {
            ItemModEngineItem output;
            if (!engineStorage.allEngineItems.TryGetItem(tier, engineStorage.slotTypes[slot], out output))
                return false;

            var component = output.GetComponent<ItemDefinition>();
            var item = ItemManager.Create(component);
            if (item == null)
                return false;

            item.condition = component.condition.max;
            item.MoveToContainer(engineStorage.inventory, slot, allowStack: false);

            return true;
        }

        private static List<Item> ExtractEnginePartsAboveTierAndDeleteRest(ModularCar car, int tier)
        {
            var extractedEngineParts = new List<Item>();

            foreach (var module in car.AttachedModuleEntities)
            {
                var engineStorage = (module as VehicleModuleEngine)?.GetContainer() as EngineStorage;
                if (engineStorage == null)
                    continue;

                var inventory = engineStorage.inventory;

                // Ignore if the engine storage is locked, since it must be controlled by another plugin.
                if (inventory.IsLocked())
                    continue;

                for (var i = 0; i < inventory.capacity; i++)
                {
                    var item = inventory.GetSlot(i);
                    if (item == null)
                        continue;

                    var component = item.info.GetComponent<ItemModEngineItem>();
                    if (component == null)
                        continue;

                    item.RemoveFromContainer();

                    if (component.tier > tier)
                        extractedEngineParts.Add(item);
                    else
                        item.Remove();
                }
            }

            return extractedEngineParts;
        }

        private static void GiveItemsToPlayerOrDrop(BasePlayer player, List<Item> itemList)
        {
            var itemsToDrop = new List<Item>();

            foreach (var item in itemList)
                if (!player.inventory.GiveItem(item))
                    itemsToDrop.Add(item);

            if (itemsToDrop.Count > 0)
                DropEngineParts(player, itemsToDrop);
        }

        private static void DropEngineParts(BasePlayer player, List<Item> itemList)
        {
            if (itemList.Count == 0)
                return;

            var position = player.GetDropPosition();
            if (itemList.Count == 1)
            {
                itemList[0].Drop(position, player.GetDropVelocity());
                return;
            }

            var container = GameManager.server.CreateEntity(ItemDropPrefab, position, player.GetNetworkRotation()) as DroppedItemContainer;
            if (container == null)
                return;

            container.playerName = $"{player.displayName}'s Engine Parts";

            // 4 large engines * 8 parts (each damaged) = 32 max engine parts.
            // This fits within the standard max size of 36.
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

        private static void FixCar(ModularCar car, int fuelAmount, int enginePartsTier)
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

        private static void ReviveCar(ModularCar car)
        {
            car.lifestate = BaseCombatEntity.LifeState.Alive;
            car.repair.enabled = true;

            foreach (var module in car.AttachedModuleEntities)
                module.repair.enabled = true;
        }

        private static void AddOrRestoreFuel(ModularCar car, int specifiedFuelAmount)
        {
            var fuelContainer = car.fuelSystem.GetFuelContainer();
            var targetFuelAmount = specifiedFuelAmount == -1 ? fuelContainer.allowedItem.stackable : specifiedFuelAmount;
            if (targetFuelAmount == 0)
                return;

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
            ModularCarGarage lift;
            if (!TryFindCarLift(car, out lift))
                return false;

            // Disable the lift for a bit, to prevent it from grabbing the car back.
            lift.enabled = false;
            lift.ReleaseOccupant();
            lift.Invoke(() => lift.enabled = true, 0.5f);

            return true;
        }

        private bool TryFindCarLift(ModularCar car, out ModularCarGarage lift)
        {
            if (Physics.RaycastNonAlloc(car.transform.position, car.transform.right, _raycastBuffer, 2, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) > 0)
            {
                lift = _raycastBuffer[0].GetEntity() as ModularCarGarage;
                if (lift != null && lift.carOccupant == car)
                    return true;
            }

            if (Physics.RaycastNonAlloc(car.transform.position, car.transform.right * -1, _raycastBuffer, 2, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) > 0)
            {
                lift = _raycastBuffer[0].GetEntity() as ModularCarGarage;
                if (lift != null && lift.carOccupant == car)
                    return true;
            }

            lift = null;
            return false;
        }

        private static void DismountAllPlayersFromCar(ModularCar car)
        {
            // Dismount seated players.
            if (car.AnyMounted())
                car.DismountAllPlayers();

            // Dismount players standing on flatbed modules.
            foreach (var module in car.AttachedModuleEntities)
                foreach (var child in module.children.ToList())
                    if (child is BasePlayer)
                        (child as BasePlayer).SetParent(null, worldPositionStays: true);
        }

        private static bool TryAddKeyLockForPlayer(ModularCar car, BasePlayer player)
        {
            if (car.carLock.HasALock || !car.carLock.CanHaveALock())
                return false;

            car.carLock.AddALock();
            car.carLock.TryCraftAKey(player, free: true);
            return true;
        }

        private static void MaybeRemoveMatchingKeysFromPlayer(BasePlayer player, ModularCar car)
        {
            if (_pluginConfig.DeleteKeyOnDespawn && car.carLock.HasALock)
            {
                var matchingKeys = player.inventory.FindItemIDs(car.carKeyDefinition.itemid)
                    .Where(key => key.instanceData != null && key.instanceData.dataInt == car.carLock.LockID);

                foreach (var key in matchingKeys)
                    key.Remove();
            }
        }

        private static void MaybeFillTankerModules(ModularCar car, int specifiedLiquidAmount)
        {
            if (specifiedLiquidAmount == 0)
                return;

            foreach (var module in car.AttachedModuleEntities)
            {
                var liquidContainer = (module as VehicleModuleStorage)?.GetContainer() as LiquidContainer;
                if (liquidContainer == null)
                    continue;

                if (FillLiquidContainer(liquidContainer, specifiedLiquidAmount) && _pluginConfig.EnableEffects)
                    Effect.server.Run(TankerFilledEffectPrefab, module.transform.position);
            }
        }

        private static bool FillLiquidContainer(LiquidContainer liquidContainer, int specifiedAmount)
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
                // Remove other liquid such as salt water.
                existingItem.RemoveFromContainer();
                existingItem.Remove();
                liquidContainer.inventory.AddItem(defaultItem, targetAmount);
                return true;
            }

            if (existingItem.amount >= targetAmount)
                // Nothing added in this case.
                return false;

            existingItem.amount = targetAmount;
            existingItem.MarkDirty();
            return true;
        }

        private static void MaybePlayCarRepairEffects(ModularCar car)
        {
            if (!_pluginConfig.EnableEffects)
                return;

            if (car.AttachedModuleEntities.Count > 0)
                foreach (var module in car.AttachedModuleEntities)
                    Effect.server.Run(RepairEffectPrefab, module.transform.position);
            else
                Effect.server.Run(RepairEffectPrefab, car.transform.position);
        }

        private static int Clamp(int x, int min, int max) => Math.Min(max, Math.Max(min, x));

        private bool IsPlayerCar(ModularCar car) =>
            _pluginData.PlayerCars.ContainsValue(car.net.ID);

        private ModularCar FindPlayerCar(IPlayer player)
        {
            if (!_pluginData.PlayerCars.ContainsKey(player.Id))
                return null;

            var car = BaseNetworkable.serverEntities.Find(_pluginData.PlayerCars[player.Id]) as ModularCar;

            // Just in case the car was removed and that somehow wasn't detected sooner.
            // This could happen if the data file somehow got out of sync for instance.
            if (car == null)
                _pluginData.UnregisterCar(player.Id);

            return car;
        }

        private bool HasSufficientSpace(BasePlayer player, int numSockets, Vector3 desiredPosition, Quaternion rotation)
        {
            var carExtents = GetCarExtents(numSockets);
            var carCenterPoint = desiredPosition + rotation * new Vector3(0, carExtents.y);

            // Need some extra height for the boxcast to allow spawning on a lift since lifts are construction.
            // Cars can't be spawned on sleepers.
            // Cars can still be spawned below ceiling lights.
            carCenterPoint.y += 0.3f;

            return Physics.BoxCastNonAlloc(carCenterPoint, carExtents, rotation * Vector3.forward, _raycastBuffer, rotation, 0.1f, BoxcastLayers, QueryTriggerInteraction.Ignore) == 0;
        }

        private int GetPlayerAllowedFreshWater(string userId) =>
            permission.UserHasPermission(userId, PermissionAutoFillTankers) && GetPlayerConfig(userId).Settings.AutoFillTankers ? _pluginConfig.FreshWaterAmount : 0;

        private int GetPlayerAllowedFuel(string userId) =>
            permission.UserHasPermission(userId, PermissionAutoFuel) ? _pluginConfig.FuelAmount : 0;

        private int GetPlayerEnginePartsTier(string userId)
        {
            if (permission.UserHasPermission(userId, PermissionEnginePartsTier3))
                return 3;
            else if (permission.UserHasPermission(userId, PermissionEnginePartsTier2))
                return 2;
            else if (permission.UserHasPermission(userId, PermissionEnginePartsTier1))
                return 1;
            else
                return 0;
        }

        private ushort GetPlayerMaxAllowedCarSockets(string userId)
        {
            if (permission.UserHasPermission(userId, PermissionSpawnSockets4))
                return 4;
            else if (permission.UserHasPermission(userId, PermissionSpawnSockets3))
                return 3;
            else if (permission.UserHasPermission(userId, PermissionSpawnSockets2))
                return 2;
            else
                return 0;
        }

        private void SpawnRandomCarForPlayer(IPlayer player, int desiredSockets)
        {
            var basePlayer = player.Object as BasePlayer;
            Vector3 spawnPosition;
            Quaternion rotation;
            if (!VerifySufficientSpace(player, desiredSockets, out spawnPosition, out rotation)
                || SpawnMyCarWasBlocked(basePlayer))
                return;

            var carOptions = new RandomCarOptions(player.Id, desiredSockets);
            var car = SpawnCarForPlayer(basePlayer, carOptions, spawnPosition, rotation, shouldTrackCar: true);
            if (car == null)
                return;

            var chatMessages = new List<string> { GetMessage(player, "Command.Spawn.Success") };
            if (car.carLock.HasALock)
                chatMessages.Add(GetMessage(player, "Command.Spawn.Success.Locked"));

            player.Reply(string.Join(" ", chatMessages));
        }

        private void SpawnPresetCarForPlayer(IPlayer player, SimplePreset preset)
        {
            if (preset.NumSockets > GetPlayerMaxAllowedCarSockets(player.Id))
            {
                ReplyToPlayer(player, "Generic.Error.NoPermissionToPresetSocketCount", preset.Name, preset.NumSockets);
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            Vector3 spawnPosition;
            Quaternion rotation;
            if (!VerifySufficientSpace(player, preset.NumSockets, out spawnPosition, out rotation)
                || SpawnMyCarWasBlocked(basePlayer))
                return;

            var carOptions = new PresetCarOptions(player.Id, preset.ModuleIDs);
            var car = SpawnCarForPlayer(basePlayer, carOptions, spawnPosition, rotation, shouldTrackCar: true);
            if (car == null)
                return;

            var chatMessages = new List<string> { GetMessage(player, "Command.Spawn.Success.Preset", preset.Name) };
            if (car.carLock.HasALock)
                chatMessages.Add(GetMessage(player, "Command.Spawn.Success.Locked"));

            ReplyToPlayer(player, string.Join(" ", chatMessages));

            if (preset != null)
                MaybePlayCarRepairEffects(car);
        }

        private ModularCar SpawnCarForPlayer(BasePlayer player, BaseCarOptions options, Vector3 position, Quaternion rotation, bool shouldTrackCar = false)
        {
            var numSockets = options.Length;

            string prefabName;
            if (numSockets == 4)
                prefabName = PrefabSockets4;
            else if (numSockets == 3)
                prefabName = PrefabSockets3;
            else if (numSockets == 2)
                prefabName = PrefabSockets2;
            else
                return null;

            var car = GameManager.server.CreateEntity(prefabName, position, rotation) as ModularCar;
            if (car == null)
                return null;

            var presetOptions = options as PresetCarOptions;
            if (presetOptions != null)
                car.spawnSettings.useSpawnSettings = false;

            car.OwnerID = player.userID;
            car.Spawn();

            if (presetOptions != null)
                AddInitialModules(car, presetOptions.NormalizedModuleIDs);

            if (shouldTrackCar)
            {
                _pluginData.StartCooldown(player.UserIDString, CooldownType.Spawn, save: false);
                _pluginData.RegisterCar(player.UserIDString, car);
            }

            // Force all modules to be processed and registered in AttachedModuleEntities.
            // This allows plugins to easily interact with the module entities such as to add engine parts.
            // Tested the performance cost of this, and it was negligible compared to creating entities above.
            foreach (KeyValuePair<BaseVehicleModule, Action> entry in car.moduleAddActions.ToList())
            {
                entry.Key.CancelInvoke(entry.Value);
                entry.Value.Invoke();
            }

            FixCar(car, options.FuelAmount, options.EnginePartsTier);
            MaybeFillTankerModules(car, options.FreshWaterAmount);

            if (options.CodeLock && VehicleDeployedLocks != null)
                VehicleDeployedLocks.Call("API_DeployCodeLock", car, player);

            if (options.KeyLock)
                TryAddKeyLockForPlayer(car, player);

            return car;
        }

        private bool ShouldTryAddCodeLockForPlayer(string userId) =>
            permission.UserHasPermission(userId, PermissionAutoCodeLock) && GetPlayerConfig(userId).Settings.AutoCodeLock;

        private bool ShouldTryAddKeyLockForPlayer(string userId) =>
            permission.UserHasPermission(userId, PermissionAutoKeyLock) && GetPlayerConfig(userId).Settings.AutoKeyLock;

        private int[] ValidateModules(object[] moduleArray)
        {
            ItemManager.Initialize();

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
                    LogWarning("Unable to parse module id or name: '{0}'", module);
                    continue;
                }

                if (itemDef == null)
                {
                    LogWarning("No item definition found for: '{0}'", module);
                    continue;
                }

                var vehicleModule = itemDef.GetComponent<ItemModVehicleModule>();
                if (vehicleModule == null)
                {
                    LogWarning("No vehicle module found for item: '{0}'", module);
                    continue;
                }

                moduleIDList.Add(itemDef.itemid);

                // Normalize module IDs by adding 0s after the module if it takes multiple sockets.
                for (var i = 0; i < vehicleModule.SocketsTaken - 1; i++)
                    moduleIDList.Add(0);
            }

            return moduleIDList.ToArray();
        }

        #endregion

        #region Data Management

        internal class PluginData : SimplePresetManager
        {
            [JsonProperty("playerCars")]
            public Dictionary<string, uint> PlayerCars = new Dictionary<string, uint>();

            [JsonProperty("Cooldowns")]
            public CooldownManager Cooldowns = new CooldownManager();

            public override List<SimplePreset> Presets { get; set; }
            public bool ShouldSerializePresets() => false;

            public static PluginData LoadData() =>
                Interface.Oxide.DataFileSystem.ReadObject<PluginData>(_pluginInstance.Name);

            public override void SaveData()
            {
                Interface.Oxide.DataFileSystem.WriteObject(_pluginInstance.Name, this);
            }

            public void RegisterCar(string userId, ModularCar car)
            {
                PlayerCars.Add(userId, car.net.ID);
                SaveData();
            }

            public void UnregisterCar(string userId)
            {
                PlayerCars.Remove(userId);
                SaveData();
            }

            public long GetRemainingCooldownSeconds(string userId, CooldownType cooldownType)
            {
                long cooldownStart;
                if (!Cooldowns.GetCooldownMap(cooldownType).TryGetValue(userId, out cooldownStart))
                    return 0;

                var cooldownSeconds = _pluginConfig.Cooldowns.GetSeconds(cooldownType);
                return cooldownStart + cooldownSeconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            public void StartCooldown(string userId, CooldownType cooldownType, bool save = true)
            {
                if (_pluginConfig.Cooldowns.GetSeconds(cooldownType) <= 0)
                    return;

                Cooldowns.GetCooldownMap(cooldownType)[userId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                if (save)
                    SaveData();
            }
        }

        internal class CommonPresets : SimplePresetManager
        {
            private static string Filename =>
                $"{_pluginInstance.Name}_CommonPresets";

            public static CommonPresets LoadData(PluginData pluginData)
            {
                var data = Interface.Oxide.DataFileSystem.ReadObject<CommonPresets>(Filename);

                if (pluginData.Presets != null)
                {
                    if (data.Presets == null || data.Presets.Count == 0)
                    {
                        _pluginInstance.LogWarning($"Migrating common presets to separate data file: {Filename}.json.");
                        data.Presets = pluginData.Presets.ToList();
                        data.SaveData();
                    }
                    else
                    {
                        _pluginInstance.LogWarning($"Deleting common presets from main data file since they appear to have already been migrated to a separate data file: {Filename}.json.");
                    }
                    pluginData.Presets.Clear();
                    pluginData.SaveData();
                }

                return data;
            }

            public override void SaveData() =>
                Interface.Oxide.DataFileSystem.WriteObject(Filename, this);
        }

        private PlayerConfig GetPlayerConfig(IPlayer player) =>
            GetPlayerConfig(player.Id);

        private PlayerConfig GetPlayerConfig(string userId)
        {
            if (_playerConfigsMap.ContainsKey(userId))
                return _playerConfigsMap[userId];

            PlayerConfig config = PlayerConfig.Get(Name, userId);
            _playerConfigsMap.Add(userId, config);
            return config;
        }

        internal enum CooldownType { Spawn, Fetch, Load, Fix }

        internal class CooldownManager
        {
            [JsonProperty("Spawn")]
            private Dictionary<string, long> Spawn = new Dictionary<string, long>();

            [JsonProperty("Fetch")]
            private Dictionary<string, long> Fetch = new Dictionary<string, long>();

            [JsonProperty("LoadPreset")]
            private Dictionary<string, long> LoadPreset = new Dictionary<string, long>();

            [JsonProperty("Fix")]
            private Dictionary<string, long> Fix = new Dictionary<string, long>();

            public Dictionary<string, long> GetCooldownMap(CooldownType cooldownType)
            {
                switch (cooldownType)
                {
                    case CooldownType.Spawn:
                        return Spawn;
                    case CooldownType.Fetch:
                        return Fetch;
                    case CooldownType.Load:
                        return LoadPreset;
                    case CooldownType.Fix:
                        return Fix;
                    default:
                        _pluginInstance.LogWarning($"Cooldown not implemented for {cooldownType}");
                        return null;
                }
            }

            public void ClearAll()
            {
                Spawn.Clear();
                Fetch.Clear();
                LoadPreset.Clear();
                Fix.Clear();
            }
        }

        internal abstract class SimplePresetManager
        {
            public static Func<SimplePreset, bool> MatchPresetName(string presetName) =>
                new Func<SimplePreset, bool>(preset => preset.Name.Equals(presetName, StringComparison.CurrentCultureIgnoreCase));

            [JsonProperty("Presets")]
            public virtual List<SimplePreset> Presets { get; set; } = new List<SimplePreset>();

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
                if (presetIndex == -1)
                    return;

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
                    ModuleIDs = GetCarModuleIDs(car)
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

        private void MigrateConfig()
        {
            if (_pluginConfig.ValidateServerPresets())
            {
                LogWarning("Performing automatic config migration.");
                SaveConfig();
            }
        }

        internal class Configuration : SerializableConfiguration
        {
            [JsonProperty("CanSpawnWhileBuildingBlocked")]
            public bool CanSpawnBuildingBlocked = false;

            [JsonProperty("CanFetchWhileBuildingBlocked")]
            public bool CanFetchBuildingBlocked = false;

            [JsonProperty("CanFetchWhileOccupied")]
            public bool CanFetchOccupied = false;

            [JsonProperty("CanDespawnWhileOccupied")]
            public bool CanDespawnOccupied = false;

            [JsonProperty("DismountPlayersOnFetch")]
            public bool DismountPlayersOnFetch = true;

            [JsonProperty("DeleteMatchingKeysFromPlayerInventoryOnDespawn")]
            public bool DeleteKeyOnDespawn = true;

            [JsonProperty("FuelAmount")]
            public int FuelAmount = 500;

            [JsonProperty("FreshWaterAmount")]
            public int FreshWaterAmount = -1;

            [JsonProperty("MaxPresetsPerPlayer")]
            public int MaxPresetsPerPlayer = 10;

            [JsonProperty("EnableEffects")]
            public bool EnableEffects = true;

            [JsonProperty("Cooldowns")]
            public CooldownConfig Cooldowns = new CooldownConfig();

            [JsonProperty("Presets")]
            public ServerPreset[] Presets = new ServerPreset[0];

            public bool ValidateServerPresets()
            {
                var changed = false;

                foreach (var preset in Presets)
                    if (preset.Options.ValidateModules())
                        changed = true;

                return changed;
            }
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

            [JsonProperty("CodeLock", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool CodeLock = false;

            [JsonProperty("EnginePartsTier", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int EnginePartsTier
            {
                get { return _enginePartsTier; }
                set { _enginePartsTier = Clamp(value, 0, 3); }
            }

            [JsonProperty("FreshWaterAmount", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int FreshWaterAmount = 0;

            [JsonProperty("FuelAmount", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int FuelAmount = 0;

            [JsonProperty("KeyLock", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool KeyLock = false;

            [JsonIgnore]
            public abstract int Length { get; }

            public BaseCarOptions() { }

            public BaseCarOptions(string userId)
            {
                CodeLock = _pluginInstance.ShouldTryAddCodeLockForPlayer(userId);
                KeyLock = _pluginInstance.ShouldTryAddKeyLockForPlayer(userId);
                EnginePartsTier = _pluginInstance.GetPlayerEnginePartsTier(userId);
                FuelAmount = _pluginInstance.GetPlayerAllowedFuel(userId);
                FreshWaterAmount = _pluginInstance.GetPlayerAllowedFreshWater(userId);
            }
        }

        internal class PresetCarOptions : BaseCarOptions
        {
            [JsonProperty("ModuleIDs")]
            public virtual int[] NormalizedModuleIDs { get; set; } = new int[0];

            [JsonIgnore]
            public override int Length
            {
                get { return NormalizedModuleIDs?.Length ?? 0; }
            }

            // Empty constructor needed for deserialization.
            public PresetCarOptions() { }

            public PresetCarOptions(string userId, int[] moduleIDs) : base(userId)
            {
                NormalizedModuleIDs = moduleIDs;
            }
        }

        internal class RandomCarOptions : BaseCarOptions
        {
            public int NumSockets;

            public override int Length
            {
                get { return NumSockets; }
            }

            public RandomCarOptions(string userId, int numSockets) : base(userId)
            {
                NumSockets = numSockets;
            }
        }

        internal class ServerPresetOptions : PresetCarOptions
        {
            // Override so we can avoid serializing it.
            public override int[] NormalizedModuleIDs { get; set; }

            // Hidden from config.
            public bool ShouldSerializeNormalizedModuleIDs() => false;

            [JsonProperty("Modules")]
            public object[] Modules;

            // Return value indicates whether the config was changed.
            public bool ValidateModules()
            {
                // Give precedence to "Modules".
                if (Modules != null)
                {
                    NormalizedModuleIDs = _pluginInstance.ValidateModules(Modules);
                }
                else if (NormalizedModuleIDs != null)
                {
                    // Resave the config with the field renamed to Modules.
                    // Must do this before normalizing so that no extra 0's are added.
                    Modules = NormalizedModuleIDs.Cast<object>().ToArray();
                    NormalizedModuleIDs = NormalizeModuleIDs(NormalizedModuleIDs);
                    return true;
                }

                return false;
            }

            private int[] NormalizeModuleIDs(int[] moduleIDs)
            {
                ItemManager.Initialize();

                var moduleIDList = moduleIDs.ToList();

                for (var i = 0; i < moduleIDList.Count; i++)
                {
                    if (moduleIDList[i] != 0)
                    {
                        // Add a 0 after each module that takes 2 sockets.
                        // This is more user-friendly than requiring people to add the 0s themselves.
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
            public long SpawnSeconds = 3600;

            [JsonProperty("FetchCarSeconds")]
            public long FetchSeconds = 600;

            [JsonProperty("LoadPresetSeconds")]
            public long LoadPresetSeconds = 3600;

            [JsonProperty("FixCarSeconds")]
            public long FixSeconds = 3600;

            public long GetSeconds(CooldownType cooldownType)
            {
                switch (cooldownType)
                {
                    case CooldownType.Spawn:
                        return SpawnSeconds;
                    case CooldownType.Fetch:
                        return FetchSeconds;
                    case CooldownType.Load:
                        return LoadPresetSeconds;
                    case CooldownType.Fix:
                        return FixSeconds;
                    default:
                        _pluginInstance.LogWarning($"Cooldown not implemented for {cooldownType}");
                        return 0;
                }
            }
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
            catch
            {
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
                ["Generic.Error.Cooldown"] = "Please wait <color=yellow>{0}s</color> and try again.",
                ["Generic.Error.NoPermissionToPresetSocketCount"] = "Error: You don't have permission to use preset <color=yellow>{0}</color> because it requires <color=yellow>{1}</color> sockets.",
                ["Generic.Error.PresetNotFound"] = "Error: Preset <color=yellow>{0}</color> not found.",
                ["Generic.Error.PresetMultipleMatches"] = "Error: Multiple presets found matching <color=yellow>{0}</color>. Use <color=yellow>mycar list</color> to view your presets.",
                ["Generic.Error.PresetAlreadyTaken"] = "Error: Preset <color=yellow>{0}</color> is already taken.",
                ["Generic.Error.PresetNameLength"] = "Error: Preset name may not be longer than {0} characters.",
                ["Generic.Error.InsufficientSpace"] = "Error: Not enough space.",

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
