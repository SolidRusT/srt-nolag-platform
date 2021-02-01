using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Modular Car Code Locks", "WhiteThunder", "1.2.1")]
    [Description("Allows players to deploy code locks to Modular Cars.")]
    internal class CarCodeLocks : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin Clans, Friends, VehicleDeployedLocks;

        private static CarCodeLocks PluginInstance;

        private CarCodeLocksConfig PluginConfig;

        private const string PermissionUse = "carcodelocks.use";
        private const string PermissionUI = "carcodelocks.ui";
        private const string PermissionFreeLock = "carcodelocks.free";

        private const string CodeLockPrefab = "assets/prefabs/locks/keypad/lock.code.prefab";
        private const string CodeLockDeployedEffectPrefab = "assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab";

        private const int CodeLockItemId = 1159991980;

        private readonly Vector3 CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f);
        private readonly CarLiftTracker LiftTracker = new CarLiftTracker();
        private CodeLockUIManager UIManager;
        private CooldownManager CraftCooldowns;

        #endregion

        #region Hooks

        private void Init()
        {
            PluginInstance = this;
            PluginConfig = Config.ReadObject<CarCodeLocksConfig>();
            UIManager = new CodeLockUIManager(PluginConfig.UISettings);

            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionUI, this);
            permission.RegisterPermission(PermissionFreeLock, this);

            CraftCooldowns = new CooldownManager(PluginConfig.CooldownSeconds);
        }

        private void OnServerInitialized()
        {
            if (VehicleDeployedLocks == null)
                LogWarning("This plugin is deprecated. Please migrate to Vehicle Deployed Locks and optionally Car Lock UI from https://umod.org/.");
            else
                LogWarning("This plugin is deprecated. You have already installed Vehicle Deployed Locks so please uninstall this plugin to avoid conflicts.");
        }

        private void Unload()
        {
            UIManager.DestroyAllUIs();
            PluginInstance = null;
        }

        object CanMountEntity(BasePlayer player, BaseVehicleMountPoint entity)
        {
            var car = entity?.GetVehicleParent() as ModularCar;
            return CanPlayerInteractWithCar(player, car);
        }

        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            var parent = container.GetParentEntity();
            var car = parent as ModularCar ?? (parent as VehicleModuleStorage)?.Vehicle as ModularCar;
            return CanPlayerInteractWithCar(player, car);
        }

        object CanLootEntity(BasePlayer player, LiquidContainer container)
        {
            var car = (container.GetParentEntity() as VehicleModuleStorage)?.Vehicle as ModularCar;
            return CanPlayerInteractWithCar(player, car);
        }

        object CanLootEntity(BasePlayer player, ModularCarGarage carLift)
        {
            if (PluginConfig.AllowEditingWhileLockedOut || !carLift.PlatformIsOccupied) return null;
            return CanPlayerInteractWithCar(player, carLift.carOccupant);
        }

        private object CanPlayerInteractWithCar(BasePlayer player, ModularCar car)
        {
            if (car == null) return null;

            var codeLock = GetCarCodeLock(car);
            if (codeLock == null) return null;

            if (!CanPlayerBypassLock(player, codeLock))
            {
                Effect.server.Run(codeLock.effectDenied.resourcePath, codeLock, 0, Vector3.zero, Vector3.forward);
                player.ChatMessage(GetMessage(player.IPlayer, "Error.CarLocked"));
                return false;
            }

            Effect.server.Run(codeLock.effectUnlocked.resourcePath, codeLock, 0, Vector3.zero, Vector3.forward);
            return null;
        }

        void OnLootEntity(BasePlayer player, ModularCarGarage carLift)
        {
            LiftTracker.HandlePlayerLootCarLift(player, carLift);

            var car = carLift?.carOccupant;
            if (car == null) return;
            UIManager.UpdateCarUI(car);
        }

        void OnPlayerLootEnd(PlayerLoot inventory)
        {
            var player = inventory.GetComponent<BasePlayer>();
            if (player == null) return;
            LiftTracker.HandlePlayerLootEnd(player);
            UIManager.DestroyPlayerUI(player);
        }

        // Handle the case where a cockpit is added while a player is editing the car
        void OnEntitySpawned(VehicleModuleSeating seatingModule)
        {
            if (seatingModule == null || !seatingModule.HasADriverSeat()) return;
            NextTick(() =>
            {
                var car = seatingModule.Vehicle as ModularCar;
                if (car == null) return;
                UIManager.UpdateCarUI(car);
            });
        }

        // Handle the case where a cockpit is removed but the car remains
        // If a lock is present, either move the lock to another cockpit or destroy it
        private void OnEntityKill(VehicleModuleSeating seatingModule)
        {
            if (seatingModule == null || !seatingModule.HasADriverSeat()) return;

            var car = seatingModule.Vehicle as ModularCar;
            if (car == null) return;

            var codeLock = seatingModule.GetComponentInChildren<CodeLock>();
            if (codeLock == null)
            {
                NextTick(() =>
                {
                    if (car != null)
                        UIManager.UpdateCarUI(car);
                });
                return;
            }

            codeLock.SetParent(null);
            NextTick(() =>
            {
                if (car == null) return;

                var driverModule = FindFirstDriverModule(car);
                if (driverModule != null)
                    codeLock.SetParent(driverModule);
                else
                {
                    codeLock.Kill();
                    UIManager.UpdateCarUI(car);
                }
            });
        }

        // Handle the case where the code lock is removed but the car and cockpit remain
        private void OnEntityKill(CodeLock codeLock)
        {
            if (codeLock == null) return;

            var seatingModule = codeLock.GetParentEntity() as VehicleModuleSeating;
            if (seatingModule == null) return;

            var car = seatingModule.Vehicle as ModularCar;
            NextTick(() =>
            {
                if (car == null) return;
                UIManager.UpdateCarUI(car);
            });
        }

        #endregion

        #region API

        private CodeLock API_DeployCodeLock(ModularCar car, BasePlayer player)
        {
            if (car == null || car.IsDead() || DeployWasBlocked(car, player) || GetCarCodeLock(car) != null) return null;

            if (player != null)
                return DeployCodeLockForPlayer(car, player, isFree: true);

            var driverModule = FindFirstDriverModule(car);
            if (driverModule == null) return null;

            return DeployCodeLock(car, driverModule);
        }

        #endregion

        #region Commands

        [Command("carcodelock", "ccl")]
        private void CarCodeLockCommand(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer) return;

            var basePlayer = player.Object as BasePlayer;
            ModularCar car;
            bool mustCraft;

            if (!VerifyPermissionAny(player, PermissionUse) ||
                !VerifyNotBuildingBlocked(player) ||
                !VerifyCarFound(player, out car) ||
                !VerifyCarIsNotDead(player, car) ||
                !VerifyCarHasNoLock(player, car) ||
                !VerifyCarCanHaveALock(player, car) ||
                !VerifyPlayerCanDeployLock(player, out mustCraft) ||
                DeployWasBlocked(car, basePlayer))
                return;

            var codeLock = DeployCodeLockForPlayer(car, basePlayer, isFree: player.HasPermission(PermissionFreeLock));
            if (codeLock == null) return;

            if (mustCraft)
                CraftCooldowns.UpdateLastUsedForPlayer(player.Id);
        }

        [Command("carcodelock.ui.deploy")]
        private void UICommandDeploy(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer) return;
            if (!player.HasPermission(PermissionUI)) return;

            var basePlayer = player.Object as BasePlayer;
            var car = LiftTracker.GetCarPlayerIsLooting(basePlayer);
            if (car == null) return;

            bool mustCraft;

            if (!VerifyCarIsNotDead(player, car) ||
                !VerifyCarHasNoLock(player, car) ||
                !VerifyCarCanHaveALock(player, car) ||
                !VerifyPlayerCanDeployLock(player, out mustCraft) ||
                DeployWasBlocked(car, basePlayer))
                return;

            var codeLock = DeployCodeLockForPlayer(car, basePlayer, isFree: player.HasPermission(PermissionFreeLock));
            if (codeLock == null) return;

            if (mustCraft)
                CraftCooldowns.UpdateLastUsedForPlayer(player.Id);
        }

        [Command("carcodelock.ui.remove")]
        private void UICommandRemove(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer) return;
            if (!player.HasPermission(PermissionUI)) return;

            var basePlayer = player.Object as BasePlayer;
            var car = LiftTracker.GetCarPlayerIsLooting(basePlayer);
            if (car == null) return;

            var codeLock = GetCarCodeLock(car);
            if (codeLock == null) return;

            codeLock.Kill();

            if (!player.HasPermission(PermissionFreeLock))
            {
                var codeLockItem = ItemManager.CreateByItemID(CodeLockItemId);
                if (codeLockItem == null) return;
                basePlayer.GiveItem(codeLockItem);
            }
        }

        #endregion

        #region Helper Methods

        private bool DeployWasBlocked(ModularCar car, BasePlayer player)
        {
            object hookResult = Interface.CallHook("CanDeployCarCodeLock", car, player);
            return (hookResult is bool && (bool)hookResult == false);
        }

        private bool VerifyPermissionAny(IPlayer player, params string[] permissionNames)
        {
            foreach (var perm in permissionNames)
            {
                if (!permission.UserHasPermission(player.Id, perm))
                {
                    ReplyToPlayer(player, "Error.NoPermission");
                    return false;
                }
            }
            return true;
        }

        private bool VerifyNotBuildingBlocked(IPlayer player)
        {
            if (!(player.Object as BasePlayer).IsBuildingBlocked()) return true;
            ReplyToPlayer(player, "Error.BuildingBlocked");
            return false;
        }

        private bool VerifyCarFound(IPlayer player, out ModularCar car)
        {
            var basePlayer = player.Object as BasePlayer;
            var entity = GetLookEntity(basePlayer);

            if (PluginConfig.AllowDeployOffLift)
            {
                car = entity as ModularCar;
                if (car != null) return true;

                // Check for a lift as well since sometimes it blocks the ray
                car = (entity as ModularCarGarage)?.carOccupant;
                if (car != null) return true;

                ReplyToPlayer(player, "Error.NoCarFound");
                return false;
            }

            car = (entity as ModularCarGarage)?.carOccupant;
            if (car != null) return true;

            var carEnt = entity as ModularCar;
            if (carEnt == null)
            {
                ReplyToPlayer(player, "Error.NoCarFound");
                return false;
            }

            if (!IsCarOnLift(carEnt))
            {
                ReplyToPlayer(player, "Error.NotOnLift");
                return false;
            }

            car = carEnt;
            return true;
        }

        private bool VerifyCarIsNotDead(IPlayer player, ModularCar car)
        {
            if (!car.IsDead()) return true;
            ReplyToPlayer(player, "Error.CarDead");
            return false;
        }

        private bool VerifyCarHasNoLock(IPlayer player, ModularCar car)
        {
            if (GetCarCodeLock(car) == null) return true;
            ReplyToPlayer(player, "Error.HasLock");
            return false;
        }

        private bool VerifyCarCanHaveALock(IPlayer player, ModularCar car)
        {
            if (CanCarHaveCodeLock(car)) return true;
            ReplyToPlayer(player, "Error.NoCockpit");
            return false;
        }

        private bool VerifyPlayerCanDeployLock(IPlayer player, out bool mustCraft)
        {
            mustCraft = false;
            if (player.HasPermission(PermissionFreeLock) || DoesPlayerHaveLock(player)) return true;

            mustCraft = true;
            return VerifyPlayerCanCraftLock(player) && VerifyOffCooldown(player);
        }

        private bool VerifyPlayerCanCraftLock(IPlayer player)
        {
            if (CanPlayerCraftLock(player)) return true;

            var itemCost = PluginConfig.CodeLockCost;
            var itemDefinition = itemCost.GetItemDefinition();
            ReplyToPlayer(player, "Error.InsufficientResources", itemCost.Amount, itemDefinition.displayName.translated);
            return false;
        }

        private bool VerifyOffCooldown(IPlayer player)
        {
            var secondsRemaining = CraftCooldowns.GetSecondsRemaining(player.Id);
            if (secondsRemaining <= 0) return true;
            ReplyToPlayer(player, "Error.Cooldown", Math.Ceiling(secondsRemaining));
            return false;
        }

        private bool CanPlayerDeployLock(IPlayer player) =>
            player.HasPermission(PermissionFreeLock) ||
            DoesPlayerHaveLock(player) ||
            CanPlayerCraftLock(player);

        private bool DoesPlayerHaveLock(IPlayer player) =>
            (player.Object as BasePlayer).inventory.FindItemID(CodeLockItemId) != null;

        private bool CanPlayerCraftLock(IPlayer player)
        {
            var itemCost = PluginConfig.CodeLockCost;
            var itemDefinition = itemCost.GetItemDefinition();
            var playerInventory = (player.Object as BasePlayer).inventory;
            return playerInventory.GetAmount(itemCost.GetItemDefinition().itemid) >= itemCost.Amount;
        }

        private bool CanCarHaveCodeLock(ModularCar car) =>
            FindFirstDriverModule(car) != null;

        private BaseEntity GetLookEntity(BasePlayer player)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 3)) return null;
            return hit.GetEntity();
        }

        private bool IsCarOnLift(ModularCar car)
        {
            RaycastHit hitInfo;
            // This isn't perfect as it can hit other deployables such as rugs
            if (!Physics.SphereCast(car.transform.position + Vector3.up, 1f, Vector3.down, out hitInfo, 1f)) return false;

            var lift = RaycastHitEx.GetEntity(hitInfo) as ModularCarGarage;
            return lift != null && lift.carOccupant == car;
        }

        private CodeLock DeployCodeLockForPlayer(ModularCar car, BasePlayer player, bool isFree = true)
        {
            var driverModule = FindFirstDriverModule(car);
            if (driverModule == null) return null;

            var codeLockItem = player.inventory.FindItemID(CodeLockItemId);
            if (codeLockItem == null && !isFree)
            {
                var itemCost = PluginConfig.CodeLockCost;
                if (itemCost.Amount > 0)
                    player.inventory.Take(null, itemCost.GetItemID(), itemCost.Amount);
            }

            var codeLock = DeployCodeLock(car, driverModule, player.userID);
            if (codeLock == null) return null;

            // Allow other plugins to detect the lock being deployed (e.g., auto lock)
            if (codeLockItem != null)
            {
                Interface.CallHook("OnItemDeployed", codeLockItem.GetHeldEntity(), car);
                if (!isFree)
                    player.inventory.Take(null, CodeLockItemId, 1);
            }
            else
            {
                // Temporarily increase the player inventory capacity to ensure there is enough space
                player.inventory.containerMain.capacity++;
                var temporaryLockItem = ItemManager.CreateByItemID(CodeLockItemId);
                if (player.inventory.GiveItem(temporaryLockItem))
                {
                    Interface.CallHook("OnItemDeployed", temporaryLockItem.GetHeldEntity(), car);
                    temporaryLockItem.RemoveFromContainer();
                }
                temporaryLockItem.Remove();
                player.inventory.containerMain.capacity--;
            }

            return codeLock;
        }

        private CodeLock DeployCodeLock(ModularCar car, VehicleModuleSeating driverModule, ulong ownerID = 0)
        {
            var codeLock = GameManager.server.CreateEntity(CodeLockPrefab, CodeLockPosition, Quaternion.identity) as CodeLock;
            if (codeLock == null) return null;

            if (ownerID != 0)
                codeLock.OwnerID = ownerID;

            codeLock.SetParent(driverModule);
            codeLock.Spawn();
            car.SetSlot(BaseEntity.Slot.Lock, codeLock);

            Effect.server.Run(CodeLockDeployedEffectPrefab, codeLock.transform.position);

            UIManager.UpdateCarUI(car);

            return codeLock;
        }

        private CodeLock GetCarCodeLock(ModularCar car) =>
            car.GetSlot(BaseEntity.Slot.Lock) as CodeLock;

        private bool CanPlayerBypassLock(BasePlayer player, CodeLock codeLock)
        {
            if (!codeLock.IsLocked()) return true;

            object hookResult = Interface.CallHook("CanUseLockedEntity", player, codeLock);
            if (hookResult is bool) return (bool)hookResult;

            return IsPlayerAuthorizedToCodeLock(player.userID, codeLock) || IsCodeLockSharedWithPlayer(player, codeLock);
        }

        private bool IsPlayerAuthorizedToCodeLock(ulong userID, CodeLock codeLock) =>
            codeLock.whitelistPlayers.Contains(userID) || codeLock.guestPlayers.Contains(userID);

        private bool IsCodeLockSharedWithPlayer(BasePlayer player, CodeLock codeLock)
        {
            var ownerID = codeLock.OwnerID;
            if (ownerID == 0 || ownerID == player.userID) return false;

            // In case the owner was locked out for some reason
            if (!IsPlayerAuthorizedToCodeLock(ownerID, codeLock)) return false;

            var sharingSettings = PluginConfig.SharingSettings;

            if (sharingSettings.Team && player.currentTeam != 0)
            {
                var team = RelationshipManager.Instance.FindTeam(player.currentTeam);
                if (team != null && team.members.Contains(ownerID)) return true;
            }

            if (sharingSettings.Friends && Friends != null)
            {
                var friendsResult = Friends.Call("HasFriend", codeLock.OwnerID, player.userID);
                if (friendsResult is bool && (bool)friendsResult) return true;
            }

            if ((sharingSettings.Clan || sharingSettings.ClanOrAlly) && Clans != null)
            {
                var clanMethodName = sharingSettings.ClanOrAlly ? "IsMemberOrAlly" : "IsClanMember";
                var clanResult = Clans.Call(clanMethodName, ownerID.ToString(), player.UserIDString);
                if (clanResult is bool && (bool)clanResult) return true;
            }

            return false;
        }

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

        internal class CarLiftTracker
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
                if (car == null) return;

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
                if (!LootingCar.ContainsKey(player)) return;

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

        internal class CodeLockUIManager
        {
            private enum UIState { AddLock, RemoveLock }

            private const string CodeLockUIName = "CarCodeLocks.AddRemoveLockUI";

            private readonly UISettings Settings;
            private readonly Dictionary<BasePlayer, UIState> PlayerUIStates = new Dictionary<BasePlayer, UIState>();

            public CodeLockUIManager(UISettings settings)
            {
                Settings = settings;
            }

            public void DestroyAllUIs()
            {
                var keys = PlayerUIStates.Keys;
                if (keys.Count == 0) return;

                var playerList = new BasePlayer[keys.Count];
                keys.CopyTo(playerList, 0);

                foreach (var player in playerList)
                    DestroyPlayerUI(player);
            }

            public void UpdateCarUI(ModularCar car)
            {
                var looters = PluginInstance.LiftTracker.GetPlayersLootingCar(car);

                if (!PluginInstance.CanCarHaveCodeLock(car))
                {
                    foreach (var player in looters)
                        DestroyPlayerUI(player);
                    return;
                }

                var uiState = PluginInstance.GetCarCodeLock(car) == null ? UIState.AddLock : UIState.RemoveLock;
                foreach (var player in looters)
                    UpdatePlayerCarUI(player, uiState);
            }

            private void UpdatePlayerCarUI(BasePlayer player, UIState uiState)
            {
                if (!player.IPlayer.HasPermission(PermissionUI)) return;

                if (PlayerUIStates.ContainsKey(player))
                {
                    if (PlayerUIStates[player] == uiState) return;
                    DestroyPlayerUI(player);
                }

                if (uiState == UIState.AddLock && !PluginInstance.CanPlayerDeployLock(player.IPlayer))
                    return;

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
                                Text = PluginInstance.GetMessage(player.IPlayer, uiState == UIState.AddLock ? "UI.AddCodeLock" : "UI.RemoveCodeLock"),
                                Color = Settings.ButtonTextColor,
                                Align = TextAnchor.MiddleCenter,
                                FadeIn = 0.25f
                            },
                            Button =
                            {
                                Color = uiState == UIState.AddLock ? Settings.AddButtonColor : Settings.RemoveButtonColor,
                                Command = uiState == UIState.AddLock ? "carcodelock.ui.deploy" : "carcodelock.ui.remove"
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

        #region Configuration

        internal class CarCodeLocksConfig
        {
            [JsonProperty("AllowDeployOffLift")]
            public bool AllowDeployOffLift = false;

            [JsonProperty("AllowEditingWhileLockedOut")]
            public bool AllowEditingWhileLockedOut = true;

            [JsonProperty("CooldownSeconds")]
            public float CooldownSeconds = 10;

            [JsonProperty("CodeLockCost")]
            public ItemCost CodeLockCost = new ItemCost
            {
                ItemShortName = "metal.fragments",
                Amount = 100,
            };

            [JsonProperty("SharingSettings")]
            public SharingSettings SharingSettings = new SharingSettings();

            [JsonProperty("UISettings")]
            public UISettings UISettings = new UISettings();
        }

        internal class SharingSettings
        {
            [JsonProperty("Clan")]
            public bool Clan = false;

            [JsonProperty("ClanOrAlly")]
            public bool ClanOrAlly = false;

            [JsonProperty("Friends")]
            public bool Friends = false;

            [JsonProperty("Team")]
            public bool Team = false;
        }

        internal class ItemCost
        {
            [JsonProperty("ItemShortName")]
            public string ItemShortName;

            [JsonProperty("Amount")]
            public int Amount;

            public ItemDefinition GetItemDefinition() =>
                ItemManager.FindItemDefinition(ItemShortName);

            public int GetItemID() =>
                GetItemDefinition().itemid;
        }

        internal class UISettings
        {
            [JsonProperty("AnchorMin")]
            public string AnchorMin = "1 0";

            [JsonProperty("AnchorMax")]
            public string AnchorMax = "1 0";

            [JsonProperty("OffsetMin")]
            public string OffsetMin = "-255 349";

            [JsonProperty("OffsetMax")]
            public string OffsetMax = "-68 377";

            [JsonProperty("AddButtonColor")]
            public string AddButtonColor = "0.44 0.54 0.26 1";

            [JsonProperty("RemoveButtonColor")]
            public string RemoveButtonColor = "0.7 0.3 0 1";

            [JsonProperty("ButtonTextColor")]
            public string ButtonTextColor = "0.97 0.92 0.88 1";
        }

        private CarCodeLocksConfig GetDefaultConfig() =>
            new CarCodeLocksConfig();

        protected override void LoadDefaultConfig() =>
            Config.WriteObject(GetDefaultConfig(), true);

        #endregion

        #region Localization

        private void ReplyToPlayer(IPlayer player, string messageName, params object[] args) =>
            player.Reply(string.Format(GetMessage(player, messageName), args));

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
                ["Error.NoPermission"] = "You don't have permission to use this command.",
                ["Error.BuildingBlocked"] = "Error: Cannot do that while building blocked.",
                ["Error.NoCarFound"] = "Error: No car found.",
                ["Error.CarDead"] = "Error: That car is dead.",
                ["Error.NotOnLift"] = "Error: That car must be on a lift to receive a lock.",
                ["Error.HasLock"] = "Error: That car already has a lock.",
                ["Error.NoCockpit"] = "Error: That car needs a cockpit module to receive a lock.",
                ["Error.InsufficientResources"] = "Error: You need <color=red>{0} {1}</color> to craft a lock.",
                ["Error.Cooldown"] = "Please wait <color=red>{0}s</color> and try again.",
                ["Error.CarLocked"] = "That vehicle is locked.",
            }, this, "en");
        }

        #endregion
    }
}
