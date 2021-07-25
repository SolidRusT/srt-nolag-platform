using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Turret Loadouts", "WhiteThunder", "1.0.2")]
    [Description("Automatically fills turrets with weapons, attachments and ammo, using configurable loadouts.")]
    internal class TurretLoadouts : CovalencePlugin
    {
        #region Fields

        private static TurretLoadouts pluginInstance;

        private const int LoadoutNameMaxLength = 20;

        private const string Permission_AutoAuth = "turretloadouts.autoauth";
        private const string Permission_AutoToggle = "turretloadouts.autotoggle";
        private const string Permission_Manage = "turretloadouts.manage";
        private const string Permission_ManageCustom = "turretloadouts.manage.custom";

        private const string Permission_DefaultLoadoutPrefix = "turretloadouts.default";
        private const string Permission_RulesetPrefix = "turretloadouts.ruleset";

        private readonly Dictionary<string, PlayerData> playerDataCache = new Dictionary<string, PlayerData>();

        private Configuration pluginConfig;

        #endregion

        #region Hooks

        private void Init()
        {
            pluginInstance = this;

            permission.RegisterPermission(Permission_AutoAuth, this);
            permission.RegisterPermission(Permission_AutoToggle, this);
            permission.RegisterPermission(Permission_Manage, this);
            permission.RegisterPermission(Permission_ManageCustom, this);

            foreach (var defaultLoadout in pluginConfig.defaultLoadouts)
                permission.RegisterPermission(GetDefaultLoadoutPermission(defaultLoadout.name), this);

            foreach (var permissionConfig in pluginConfig.loadoutRulesets)
                permission.RegisterPermission(GetLoadoutRulesetPermission(permissionConfig.name), this);

            if (!pluginConfig.lockAutoFilledTurrets)
            {
                Unsubscribe(nameof(OnTurretToggle));
                Unsubscribe(nameof(CanMoveItem));
                Unsubscribe(nameof(OnDropContainerEntity));
            }
        }

        private void OnServerInitialized(bool initialBoot)
        {
            if (initialBoot)
            {
                // Update locked turrets so they can be picked up and don't drop loot
                // This is done even if the config option for locked turrets is off
                // because there could be locked turrets lingering from a previous configuration
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    var turret = entity as AutoTurret;
                    if (turret != null && IsTurretLocked(turret))
                    {
                        turret.dropChance = 0;
                        turret.pickup.requireEmptyInv = false;
                    }
                }
            }
        }

        private void Unload()
        {
            pluginInstance = null;
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            BasePlayer ownerPlayer = plan?.GetOwnerPlayer();
            if (ownerPlayer == null)
                return;

            BaseEntity entity = go?.ToBaseEntity();
            if (entity == null)
                return;

            AutoTurret turret = entity as AutoTurret;
            if (turret == null)
                return;

            MaybeAuthTurret(turret, ownerPlayer);

            var loadout = GetPlayerActiveLoadout(ownerPlayer.UserIDString);
            if (loadout != null)
            {
                if (loadout.peacekeeper)
                    turret.SetPeacekeepermode(true);

                var heldItem = AddHeldEntity(turret, ownerPlayer, loadout);
                if (heldItem != null)
                {
                    AddReserveAmmo(turret, loadout, ownerPlayer);
                    turret.UpdateTotalAmmo();
                    turret.EnsureReloaded();

                    var isInstrument = (heldItem.GetHeldEntity() as HeldEntity)?.IsInstrument() ?? false;
                    if ((isInstrument || GetTotalAmmo(turret) > 0) && HasPermissionAny(ownerPlayer, Permission_AutoToggle))
                    {
                        turret.SetOnline();
                        var turretSwitch = turret.GetComponentInChildren<ElectricSwitch>();
                        if (turretSwitch != null)
                            turretSwitch.SetSwitch(true);
                    }

                    if (pluginConfig.lockAutoFilledTurrets)
                    {
                        heldItem.contents.SetLocked(true);
                        turret.inventory.SetLocked(true);
                        turret.dropChance = 0;
                        turret.pickup.requireEmptyInv = false;
                    }

                    if (HasPermissionAny(ownerPlayer, Permission_Manage, Permission_ManageCustom))
                        ChatMessage(ownerPlayer, "Generic.FilledFromLoadout", loadout.GetDisplayName(ownerPlayer.UserIDString));
                }
            }

            turret.SendNetworkUpdate();
        }

        private void OnTurretToggle(AutoTurret turret)
        {
            // Remove items if powering down while locked and out of ammo
            // Otherwise, the turret would be unusable other than picking it up
            if (turret != null && turret.IsOnline() && IsTurretLocked(turret) && GetTotalAmmo(turret) == 0)
            {
                turret.inventory.Clear();
                turret.inventory.SetLocked(false);
            }
        }

        private object CanMoveItem(Item item)
        {
            if (item.parent == null)
                return null;

            // Fix issue where right-clicking an item in a locked turret inventory allows moving it
            if (item.parent.entityOwner is AutoTurret && item.parent.IsLocked())
                return false;

            return null;
        }

        // Compatibility with plugin: Remover Tool (RemoverTool)
        private object OnDropContainerEntity(AutoTurret turret)
        {
            // Prevent Remover Tool from explicitly dropping the turret inventory
            if (IsTurretLocked(turret))
                return false;

            return null;
        }

        #endregion

        #region Commands

        [Command("tl")]
        private void MainCommand(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer)
                return;

            if (args.Length == 0)
            {
                SubCommandDefault(player);
                return;
            }

            var playerData = GetPlayerData(player);

            switch (args[0].ToLower())
            {
                case "help":
                    SubCommandHelp(player);
                    return;

                case "list":
                    SubCommandList(player);
                    return;

                case "save":
                    SubCommandSave(player, args.Skip(1).ToArray());
                    return;

                case "update":
                    SubCommandUpdate(player, args.Skip(1).ToArray());
                    return;

                case "rename":
                    SubCommandRename(player, args.Skip(1).ToArray());
                    return;

                case "delete":
                    SubCommandDelete(player, args.Skip(1).ToArray());
                    return;

                default:
                    SubCommandActivate(player, args);
                    return;
            }
        }

        private void SubCommandDefault(IPlayer player)
        {
            if (!VerifyPermissionAny(player, Permission_Manage, Permission_ManageCustom))
                return;

            var sb = new StringBuilder();

            var loadout = GetPlayerActiveLoadout(player.Id);
            if (loadout == null)
                sb.AppendLine(GetMessage(player, "Command.Default.NoActive"));
            else
            {
                sb.AppendLine(GetMessage(player, "Command.Default.Active", loadout.GetDisplayName(player.Id)));
                sb.Append(PrintLoadoutDetails(player, loadout));
                sb.AppendLine();
            }

            sb.AppendLine(GetMessage(player, "Command.Default.HelpHint"));
            player.Reply(sb.ToString());
        }

        private void SubCommandHelp(IPlayer player)
        {
            if (!VerifyPermissionAny(player, Permission_Manage, Permission_ManageCustom))
                return;

            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, "Generic.Header"));
            sb.AppendLine(GetMessage(player, "Command.Help.Details"));
            sb.AppendLine(GetMessage(player, "Command.Help.List"));
            sb.AppendLine(GetMessage(player, "Command.Help.Activate"));

            if (player.HasPermission(Permission_ManageCustom))
            {
                sb.AppendLine(GetMessage(player, "Command.Help.Save"));
                sb.AppendLine(GetMessage(player, "Command.Help.Update"));
                sb.AppendLine(GetMessage(player, "Command.Help.Rename"));
                sb.AppendLine(GetMessage(player, "Command.Help.Delete"));
            }

            player.Reply(sb.ToString());
        }

        private void SubCommandList(IPlayer player)
        {
            if (!VerifyPermissionAny(player, Permission_Manage, Permission_ManageCustom))
                return;

            var playerData = GetPlayerData(player);

            // Prune loadouts that are no longer valid
            // For example, if the player no longer has permission to the weapon type
            playerData.RestrictAndPruneLoadouts(GetPlayerLoadoutRules(player));

            var defaultLoadout = GetPlayerDefaultLoadout(player.Id);
            if (playerData.loadouts.Count == 0 && defaultLoadout == null)
            {
                ReplyToPlayer(player, "Command.List.NoLoadouts");
                return;
            }

            var loadouts = playerData.loadouts.ToArray();
            Array.Sort(loadouts, SortLoadoutNames);

            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, "Generic.Header"));

            if (defaultLoadout != null)
                AddListItem(sb, player, defaultLoadout, playerData.activeLoadout);

            foreach (var loadout in loadouts)
                AddListItem(sb, player, loadout, playerData.activeLoadout);

            sb.AppendLine();
            sb.AppendLine(GetMessage(player, "Command.List.ToggleHint"));

            player.Reply(sb.ToString());
        }

        private void AddListItem(StringBuilder sb, IPlayer player, TurretLoadout loadout, string activeLoadout)
        {
            var weaponDefinition = ItemManager.itemDictionaryByName[loadout.weapon];
            var activeString = loadout.IsDefault && activeLoadout == null || activeLoadout == loadout.name ? GetMessage(player, "Command.List.Item.Active") : string.Empty;

            var attachmentAbbreviations = AbbreviateAttachments(player, loadout);
            var attachmentsString = attachmentAbbreviations == null ? string.Empty : string.Format(" ({0})", string.Join(", ", attachmentAbbreviations));

            sb.AppendLine(GetMessage(player, "Command.List.Item", activeString, loadout.GetDisplayName(player.Id), weaponDefinition.displayName.translated, attachmentsString));
        }

        private IEnumerable<string> AbbreviateAttachments(IPlayer player, TurretLoadout loadout)
        {
            if (loadout.attachments == null || loadout.attachments.Count == 0)
                return null;

            return loadout.attachments.Select(attachmentName =>
            {
                var langKey = string.Format("Abbreviation.{0}", attachmentName);
                var abbreviated = GetMessage(player, langKey);
                return abbreviated == langKey ? attachmentName : abbreviated;
            });
        }

        private void SubCommandSave(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, Permission_ManageCustom))
                return;

            if (args.Length == 0)
            {
                ReplyToPlayer(player, "Command.Save.Error.Syntax");
                return;
            }

            var loadoutName = args[0];
            if (!VerifyLoadoutNameLength(player, loadoutName))
                return;

            if (MatchesDefaultLoadoutName(player, loadoutName))
            {
                ReplyToPlayer(player, "Generic.Error.DefaultLoadout");
                return;
            }

            var playerData = GetPlayerData(player);
            var loadoutRuleset = GetPlayerLoadoutRules(player);
            playerData.RestrictAndPruneLoadouts(loadoutRuleset);

            if (playerData.HasLoadout(loadoutName))
            {
                ReplyToPlayer(player, "Command.Save.Error.LoadoutExists", loadoutName);
                return;
            }

            if (playerData.loadouts.Count >= pluginConfig.maxLoadoutsPerPlayer)
            {
                ReplyToPlayer(player, "Command.Save.Error.TooManyLoadouts", pluginConfig.maxLoadoutsPerPlayer);
                return;
            }

            var basePlayer = player.Object as BasePlayer;

            AutoTurret turret;
            TurretLoadout loadout;
            if (!VerifyTurretFound(basePlayer, out turret) ||
                !VerifyTurretLoadoutValid(player, turret, out loadout))
                return;

            var disallowedItems = new Dictionary<string, int>();
            if (playerData.ValidateAndPossiblyReduceLoadout(loadout, loadoutRuleset, disallowedItems) == LoadoutManager.ValidationResult.DisallowedWeapon)
            {
                var itemDefinition = ItemManager.itemDictionaryByName[loadout.weapon];
                ReplyToPlayer(player, "Generic.Error.WeaponNotAllowed", itemDefinition.displayName.translated);
                return;
            }

            loadout.name = loadoutName;
            playerData.SaveLoadout(loadout);

            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, "Command.Save.Success", loadoutName));
            sb.Append(PrintLoadoutDetails(player, loadout));
            if (disallowedItems != null && !disallowedItems.IsEmpty())
                sb.Append(PrintDisallowedItems(player, disallowedItems));
            player.Reply(sb.ToString());
        }

        private void SubCommandUpdate(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, Permission_ManageCustom))
                return;

            if (args.Length < 1)
            {
                ReplyToPlayer(player, "Command.Update.Error.Syntax");
                return;
            }

            var loadoutName = args[0];
            if (MatchesDefaultLoadoutName(player, loadoutName))
            {
                ReplyToPlayer(player, "Generic.Error.DefaultLoadout");
                return;
            }

            var basePlayer = player.Object as BasePlayer;

            AutoTurret turret;
            TurretLoadout newLoadout;
            if (!VerifyTurretFound(basePlayer, out turret) ||
                !VerifyTurretLoadoutValid(player, turret, out newLoadout))
                return;

            var playerData = GetPlayerData(player);
            var loadoutPermission = GetPlayerLoadoutRules(player);
            playerData.RestrictAndPruneLoadouts(loadoutPermission);

            TurretLoadout existingLoadout;
            if (!VerifyHasLoadout(player, loadoutName, out existingLoadout))
                return;

            var disallowedItems = new Dictionary<string, int>();
            if (playerData.ValidateAndPossiblyReduceLoadout(newLoadout, loadoutPermission, disallowedItems) == LoadoutManager.ValidationResult.DisallowedWeapon)
            {
                var itemDefinition = ItemManager.itemDictionaryByName[newLoadout.weapon];
                ReplyToPlayer(player, "Generic.Error.WeaponNotAllowed", itemDefinition.displayName.translated);
                return;
            }

            newLoadout.name = loadoutName;
            GetPlayerData(player).TryUpdateLoadout(newLoadout);

            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, "Command.Update.Success", loadoutName));
            sb.Append(PrintLoadoutDetails(player, newLoadout));
            if (disallowedItems != null && !disallowedItems.IsEmpty())
                sb.Append(PrintDisallowedItems(player, disallowedItems));
            player.Reply(sb.ToString());
        }

        private string PrintDisallowedItems(IPlayer player, Dictionary<string, int> disallowedItems)
        {
            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, "Generic.RestrictedItems"));
            foreach (var entry in disallowedItems)
                sb.AppendLine(string.Format("  {0} {1}", entry.Value, GetItemDisplayName(entry.Key)));
            return sb.ToString();
        }

        private string PrintLoadoutDetails(IPlayer player, TurretLoadout loadout)
        {
            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, loadout.peacekeeper ? "Command.Default.Mode.Peacekeeper" : "Command.Default.Mode.AttackAll"));

            var ammoString = loadout.ammo != null && loadout.ammo.amount > 0
                ? string.Format(" ({0} {1})", loadout.ammo.amount, GetItemDisplayName(loadout.ammo.name))
                : string.Empty;

            sb.AppendLine(GetMessage(player, "Command.Default.Weapon", GetItemDisplayName(loadout.weapon), ammoString));

            if (loadout.attachments != null && loadout.attachments.Count > 0)
            {
                sb.AppendLine(GetMessage(player, "Command.Default.Attachments"));
                foreach (var attachmentName in loadout.attachments)
                    sb.AppendLine(string.Format("  {0}", GetItemDisplayName(attachmentName)));
            }

            if (loadout.reserveAmmo != null && !loadout.reserveAmmo.IsEmpty())
            {
                sb.AppendLine(GetMessage(player, "Command.Default.ReserveAmmo"));
                foreach (var ammo in loadout.reserveAmmo)
                {
                    if (ammo.amount > 0)
                        sb.AppendLine(string.Format("  {0} {1}", ammo.amount, GetItemDisplayName(ammo.name)));
                }
            }

            return sb.ToString();
        }

        private void SubCommandRename(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, Permission_ManageCustom))
                return;

            if (args.Length < 2)
            {
                ReplyToPlayer(player, "Command.Rename.Error.Syntax");
                return;
            }

            var oldName = args[0];
            var newName = args[1];

            if (MatchesDefaultLoadoutName(player, oldName) || MatchesDefaultLoadoutName(player, newName))
            {
                ReplyToPlayer(player, "Generic.Error.DefaultLoadout");
                return;
            }

            TurretLoadout loadout;
            if (!VerifyHasLoadout(player, oldName, out loadout) ||
                !VerifyLoadoutNameLength(player, newName))
                return;

            var playerData = GetPlayerData(player);
            var existingLoadoutWithNewName = playerData.FindByName(newName);

            // Allow renaming if just changing case
            if (existingLoadoutWithNewName != null && loadout != existingLoadoutWithNewName)
            {
                ReplyToPlayer(player, "Command.Rename.Error.LoadoutNameTaken", existingLoadoutWithNewName.name);
                return;
            }

            var actualOldLoadoutName = loadout.name;
            playerData.RenameLoadout(loadout, newName);

            if (playerData.activeLoadout == actualOldLoadoutName)
                playerData.activeLoadout = newName;

            ReplyToPlayer(player, "Command.Rename.Success", actualOldLoadoutName, newName);
        }

        private void SubCommandDelete(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, Permission_ManageCustom))
                return;

            if (args.Length < 1)
            {
                ReplyToPlayer(player, "Command.Delete.Error.Syntax");
                return;
            }

            var loadoutName = args[0];

            TurretLoadout loadout;
            if (!VerifyHasLoadout(player, loadoutName, out loadout))
                return;

            if (loadout.IsDefault)
            {
                ReplyToPlayer(player, "Generic.Error.DefaultLoadout");
                return;
            }

            GetPlayerData(player).DeleteLoadout(loadout);
            ReplyToPlayer(player, "Command.Delete.Success", loadout.name);
        }

        private void SubCommandActivate(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, Permission_Manage, Permission_ManageCustom))
                return;

            if (args.Length < 1)
            {
                ReplyToPlayer(player, "Command.Activate.Error.Syntax");
                return;
            }

            var loadoutName = args[0];

            TurretLoadout loadout;
            if (!VerifyHasLoadout(player, loadoutName, out loadout, matchPartial: true))
                return;

            var playerData = GetPlayerData(player);

            if (loadout.IsDefault)
                playerData.activeLoadout = playerData.activeLoadout == null ? string.Empty : null;
            else
                playerData.activeLoadout = playerData.activeLoadout == loadout.name ? string.Empty : loadout.name;

            playerData.SaveData();

            if (playerData.activeLoadout == string.Empty)
                ReplyToPlayer(player, "Command.Activate.Success.Deactivated", loadout.GetDisplayName(player.Id));
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine(GetMessage(player, "Command.Default.Active", loadout.GetDisplayName(player.Id)));
                sb.Append(PrintLoadoutDetails(player, loadout));
                ReplyToPlayer(player, sb.ToString());
            }
        }

        #endregion

        #region Helper Methods - Command Checks

        private bool VerifyPermissionAny(IPlayer player, params string[] permissionNames)
        {
            if (HasPermissionAny(player, permissionNames))
                return true;

            ReplyToPlayer(player, "Generic.Error.NoPermission");
            return false;
        }

        private bool VerifyTurretFound(BasePlayer player, out AutoTurret turret)
        {
            turret = GetLookEntity(player, 3) as AutoTurret;
            if (turret != null)
                return true;

            ReplyToPlayer(player.IPlayer, "Command.Save.Error.NoTurretFound");
            return false;
        }

        private bool VerifyTurretLoadoutValid(IPlayer player, AutoTurret turret, out TurretLoadout loadout)
        {
            loadout = CreateLoadout(turret);
            if (loadout != null)
                return true;

            ReplyToPlayer(player, "Generic.Error.NoTurretWeapon");
            return false;
        }

        private bool VerifyHasLoadout(IPlayer player, string loadoutName, out TurretLoadout loadout, bool matchPartial = false)
        {
            if (MatchesDefaultLoadoutName(player, loadoutName, matchPartial))
                loadout = GetPlayerDefaultLoadout(player.Id);
            else
                loadout = GetPlayerData(player).FindByName(loadoutName, matchPartial);

            if (loadout != null)
                return true;

            ReplyToPlayer(player, "Generic.Error.LoadoutNotFound", loadoutName);
            return false;
        }

        private bool VerifyLoadoutNameLength(IPlayer player, string loadoutName)
        {
            if (loadoutName.Length <= LoadoutNameMaxLength)
                return true;

            ReplyToPlayer(player, "Generic.Error.LoadoutNameLength", LoadoutNameMaxLength);
            return false;
        }

        #endregion

        #region Helper Methods - Turrets

        private bool IsTurretLocked(AutoTurret turret) =>
            turret.inventory != null && turret.inventory.IsLocked();

        private int GetTotalAmmo(AutoTurret turret)
        {
            if (turret == null || turret.inventory == null)
                return 0;

            var weapon = turret.GetAttachedWeapon();
            if (weapon == null)
                return 0;

            // AutoTurret.GetTotalAmmo() only includes the reserve ammo, not the loaded ammo
            return weapon.primaryMagazine.contents + turret.GetTotalAmmo();
        }

        private void MaybeAuthTurret(AutoTurret turret, BasePlayer ownerPlayer)
        {
            if (HasPermissionAny(ownerPlayer, Permission_AutoAuth))
            {
                if (!turret.authorizedPlayers.Any(player => player != null && player.userid == ownerPlayer.userID))
                {
                    turret.authorizedPlayers.Add(new PlayerNameID
                    {
                        userid = ownerPlayer.userID,
                        username = ownerPlayer.UserIDString
                    });
                }
            }
        }

        private TurretLoadout CreateLoadout(AutoTurret turret)
        {
            var heldEntity = turret.AttachedWeapon;
            if (heldEntity == null)
                return null;

            var weaponItem = turret.inventory.GetSlot(0);
            if (weaponItem == null)
                return null;

            var loadout = new TurretLoadout()
            {
                weapon = weaponItem.info.shortname,
                skin = weaponItem.skin,
                peacekeeper = turret.PeacekeeperMode()
            };

            if (weaponItem.contents != null)
            {
                var attachments = new List<string>();
                for (var slot = 0; slot < weaponItem.contents.capacity; slot++)
                {
                    var attachmentItem = weaponItem.contents.GetSlot(slot);
                    if (attachmentItem != null)
                        attachments.Add(attachmentItem.info.shortname);
                }

                if (attachments.Count > 0)
                    loadout.attachments = attachments;
            }

            var weapon = heldEntity as BaseProjectile;
            if (weapon != null && weapon.primaryMagazine.contents > 0)
            {
                loadout.ammo = new AmmoAmount
                {
                    name = weapon.primaryMagazine.ammoType.shortname,
                    amount = weapon.primaryMagazine.contents,
                };
            }

            var reserveAmmo = new List<AmmoAmount>();
            for (var slot = 1; slot <= 6; slot++)
            {
                var ammoItem = turret.inventory.GetSlot(slot);
                if (ammoItem == null || ammoItem.amount <= 0)
                    continue;

                reserveAmmo.Add(new AmmoAmount
                {
                    name = ammoItem.info.shortname,
                    amount = ammoItem.amount
                });
            }

            if (reserveAmmo.Count > 0)
                loadout.reserveAmmo = reserveAmmo;

            return loadout;
        }

        private Item AddHeldEntity(AutoTurret turret, BasePlayer ownerPlayer, TurretLoadout loadout)
        {
            var heldItem = ItemManager.CreateByName(loadout.weapon, 1, loadout.skin);
            if (heldItem == null)
            {
                LogError($"Weapon '{loadout.weapon}' is not a valid item. Unable to add weapon to turret for player {ownerPlayer.userID}.");
                return null;
            }

            if (loadout.attachments != null)
            {
                foreach (var attachmentName in loadout.attachments)
                {
                    var attachmentItem = ItemManager.CreateByName(attachmentName);
                    if (attachmentItem == null)
                    {
                        LogError($"Attachment '{attachmentName}' is not a valid item. Unable to add to turret weapon for player {ownerPlayer.userID}.");
                    }
                    else if (!attachmentItem.MoveToContainer(heldItem.contents))
                    {
                        LogError($"Unable to move attachment item '{attachmentName}' to weapon for player {ownerPlayer.userID}.");
                        attachmentItem.Remove();
                    }
                }
            }

            if (!heldItem.MoveToContainer(turret.inventory, 0))
            {
                LogError($"Unable to move weapon {heldItem.info.shortname} to turret inventory for player {ownerPlayer.userID}.");
                heldItem.Remove();
                return null;
            }

            var weapon = heldItem.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                // Must unload the weapon first or the turret will unload it and the ammo will temporarily take up inventory space
                weapon.primaryMagazine.contents = 0;
                turret.UpdateAttachedWeapon();
                turret.CancelInvoke(turret.UpdateAttachedWeapon);

                if (loadout.ammo != null)
                {
                    ItemDefinition loadedAmmoItemDefinition;
                    if (!ItemManager.itemDictionaryByName.TryGetValue(loadout.ammo.name, out loadedAmmoItemDefinition))
                    {
                        LogError($"Ammo type '{loadout.ammo.name}' is not a valid item. Unable to add ammo to turret for player {ownerPlayer.userID}.");
                        return heldItem;
                    }

                    weapon.primaryMagazine.ammoType = loadedAmmoItemDefinition;
                    weapon.primaryMagazine.contents = Math.Min(weapon.primaryMagazine.capacity, loadout.ammo.amount);
                }
            }

            return heldItem;
        }

        private void AddReserveAmmo(AutoTurret turret, TurretLoadout loadout, BasePlayer ownerPlayer)
        {
            if (loadout.reserveAmmo == null)
                return;

            var slot = 1;
            var maxSlot = 6;

            foreach (var ammo in loadout.reserveAmmo)
            {
                if (slot > maxSlot)
                    break;

                if (ammo.amount <= 0)
                    continue;

                var itemDefinition = ItemManager.itemDictionaryByName[ammo.name];
                if (itemDefinition == null)
                {
                    LogError($"Ammo type '{loadout.ammo.name}' is not a valid item. Unable to add ammo to turret for player {ownerPlayer.userID}.");
                    continue;
                }

                // Allow default loadouts to bypass max stack size
                var amountToAdd = loadout.IsDefault ? ammo.amount : Math.Min(ammo.amount, itemDefinition.stackable);
                var ammoItem = ItemManager.Create(itemDefinition, amountToAdd);
                if (!ammoItem.MoveToContainer(turret.inventory, slot))
                {
                    LogError($"Unable to add ammo {ammoItem.amount} '{itemDefinition.shortname}' to turret inventory slot {slot} for player {ownerPlayer.userID}.");
                    ammoItem.Remove();
                }

                slot++;
            }
        }

        #endregion

        #region Helper Methods - Misc

        private bool HasPermissionAny(BasePlayer basePlayer, params string[] permissionNames) =>
            HasPermissionAny(basePlayer.UserIDString, permissionNames);

        private bool HasPermissionAny(IPlayer player, params string[] permissionNames) =>
            HasPermissionAny(player.Id, permissionNames);

        private bool HasPermissionAny(string userId, params string[] permissionNames)
        {
            foreach (var perm in permissionNames)
                if (permission.UserHasPermission(userId, perm))
                    return true;

            return false;
        }

        private bool MatchesDefaultLoadoutName(IPlayer player, string loadoutName, bool matchPartial = false)
        {
            var defaultLoadoutName = GetDefaultLoadoutName(player.Id);

            return matchPartial
                ? defaultLoadoutName.IndexOf(loadoutName, StringComparison.CurrentCultureIgnoreCase) >= 0
                : defaultLoadoutName.Equals(loadoutName, StringComparison.CurrentCultureIgnoreCase);
        }

        private string GetDefaultLoadoutName(string userIdString) =>
            GetMessage(userIdString, "Generic.DefaultLoadoutName");

        private int SortLoadoutNames(TurretLoadout a, TurretLoadout b) =>
            a.name.ToLower().CompareTo(b.name.ToLower());

        private string GetItemDisplayName(string shortname)
        {
            var itemDefinition = ItemManager.itemDictionaryByName[shortname];
            return itemDefinition == null ? shortname : itemDefinition.displayName.translated;
        }

        private BaseEntity GetLookEntity(BasePlayer basePlayer, int maxDistance)
        {
            RaycastHit hit;
            return !Physics.Raycast(basePlayer.eyes.HeadRay(), out hit, maxDistance) ? null : hit.GetEntity();
        }

        private string GetDefaultLoadoutPermission(string permissionName) => $"{Permission_DefaultLoadoutPrefix}.{permissionName}";

        private string GetLoadoutRulesetPermission(string permissionName) => $"{Permission_RulesetPrefix}.{permissionName}";

        private static void AddToDictKey(Dictionary<string, int> dict, string key, int amount)
        {
            if (dict.ContainsKey(key))
                dict[key] += amount;
            else
                dict[key] = amount;
        }

        #endregion

        #region Data Management

        private PlayerData GetPlayerData(IPlayer player) =>
            GetPlayerData(player.Id);

        private PlayerData GetPlayerData(string userIdString)
        {
            PlayerData data;
            if (playerDataCache.TryGetValue(userIdString, out data))
                return data;

            data = PlayerData.Get(userIdString);
            playerDataCache[userIdString] = data;
            return data;
        }

        internal class PlayerData : LoadoutManager
        {
            public static PlayerData Get(string ownerId)
            {
                var filepath = GetFilepath(ownerId);

                var data = Interface.Oxide.DataFileSystem.ExistsDatafile(filepath) ?
                    Interface.Oxide.DataFileSystem.ReadObject<PlayerData>(filepath) :
                    new PlayerData(ownerId);

                return data;
            }

            private static string GetFilepath(string ownerId) => $"{pluginInstance.Name}/{ownerId}";

            [JsonProperty("OwnerId")]
            public string ownerId { get; private set; }

            [JsonProperty("ActiveLoadout", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string activeLoadout;

            public PlayerData(string ownerId)
            {
                this.ownerId = ownerId;
            }

            public override void SaveData() =>
                Interface.Oxide.DataFileSystem.WriteObject(GetFilepath(ownerId), this);

            // Remove loadouts where the player no longer has permission to the weapon type
            // Update other loadouts to remove disallowed items
            public void RestrictAndPruneLoadouts(LoadoutRuleset ruleset)
            {
                var changed = false;

                for (var i = 0; i < loadouts.Count; i++)
                {
                    var loadout = loadouts[i];
                    var validationResult = ValidateAndPossiblyReduceLoadout(loadout, ruleset);

                    if (validationResult == ValidationResult.InvalidWeapon)
                        pluginInstance.LogWarning($"Removed turret loadout '{loadout.name}' for player '{ownerId}' because weapon '{loadout.weapon}' is not a valid item.");
                    else if (validationResult == ValidationResult.DisallowedWeapon)
                        pluginInstance.LogWarning($"Removed turret loadout '{loadout.name}' for player '{ownerId}' because they are no longer allowed to use loadouts with weapon '{loadout.weapon}'.");

                    if (validationResult == ValidationResult.InvalidWeapon || validationResult == ValidationResult.DisallowedWeapon)
                    {
                        loadouts.RemoveAt(i);
                        i--;
                    }

                    if (validationResult != ValidationResult.Valid)
                        changed = true;
                }

                if (changed)
                    SaveData();
            }
        }

        internal abstract class LoadoutManager
        {
            public enum ValidationResult { Valid, Changed, InvalidWeapon, DisallowedWeapon }

            private static bool MatchesLoadout(TurretLoadout loadout, string name, bool matchPartial = false) =>
                matchPartial
                    ? loadout.name.IndexOf(name, StringComparison.CurrentCultureIgnoreCase) >= 0
                    : loadout.name.Equals(name, StringComparison.CurrentCultureIgnoreCase);

            [JsonProperty("Loadouts")]
            public List<TurretLoadout> loadouts = new List<TurretLoadout>();

            public TurretLoadout FindByName(string loadoutName, bool matchPartial = false)
            {
                if (loadouts == null)
                    return null;

                foreach (var currentLoadout in loadouts)
                {
                    if (MatchesLoadout(currentLoadout, loadoutName))
                        return currentLoadout;
                }

                if (matchPartial)
                {
                    // Perform partial matching in a second pass so the first pass can get a closer match
                    foreach (var currentLoadout in loadouts)
                    {
                        if (MatchesLoadout(currentLoadout, loadoutName, matchPartial: true))
                            return currentLoadout;
                    }
                }

                return null;
            }

            public bool HasLoadout(string loadoutName) => FindByName(loadoutName) != null;

            public void SaveLoadout(TurretLoadout loadout)
            {
                loadouts.Add(loadout);
                SaveData();
            }

            public bool TryUpdateLoadout(TurretLoadout newLoadout)
            {
                var existingLoadout = FindByName(newLoadout.name);
                if (existingLoadout == null)
                    return false;

                loadouts[loadouts.IndexOf(existingLoadout)] = newLoadout;
                SaveData();
                return true;
            }

            public void RenameLoadout(TurretLoadout loadout, string newName)
            {
                loadout.name = newName;
                SaveData();
            }

            public void DeleteLoadout(TurretLoadout loadout)
            {
                loadouts.Remove(loadout);
                SaveData();
            }

            // Removes items from the loadout not currently allowed by the ruleset
            // The loadout may no longer be valid due to containing an invalid or disallowed weapon
            // This can be determined by checking the validation result
            public ValidationResult ValidateAndPossiblyReduceLoadout(TurretLoadout loadout, LoadoutRuleset loadoutRuleset, Dictionary<string, int> disallowedItems = null)
            {
                if (disallowedItems == null)
                    disallowedItems = new Dictionary<string, int>();

                var weaponDefinition = ItemManager.itemDictionaryByName[loadout.weapon];
                if (weaponDefinition == null)
                    return ValidationResult.InvalidWeapon;

                if (!loadoutRuleset.IsWeaponAllowed(loadout.weapon))
                {
                    disallowedItems[loadout.weapon] = 1;
                    return ValidationResult.DisallowedWeapon;
                }

                if (loadout.attachments != null)
                {
                    for (var i = 0; i < loadout.attachments.Count; i++)
                    {
                        var attachmentName = loadout.attachments[i];
                        if (!loadoutRuleset.IsAttachmentAllowed(attachmentName))
                        {
                            disallowedItems[attachmentName] = 1;
                            loadout.attachments.RemoveAt(i);
                            i--;
                        }
                    }
                }

                var allowedAmmo = loadoutRuleset.allowedAmmo;
                var countedAmmo = new Dictionary<string, int>();

                // Don't impose ammo limits if allowed ammo is null
                if (loadout.ammo != null && allowedAmmo != null)
                {
                    var ammo = loadout.ammo;

                    // Make sure ammo name exists
                    if (ItemManager.itemDictionaryByName[ammo.name] == null)
                    {
                        pluginInstance.LogWarning($"Ammo type '{ammo.name}' is not a valid item. Removing from loadout.");
                        ammo.amount = 0;
                    }
                    else if (allowedAmmo.ContainsKey(ammo.name))
                    {
                        var allowedAmount = allowedAmmo[ammo.name];

                        // Don't impose a limit if the allowed amount is negative
                        if (allowedAmount >= 0 && ammo.amount > allowedAmount)
                        {
                            // Reduce ammo to the allowed amount
                            AddToDictKey(disallowedItems, ammo.name, ammo.amount - allowedAmount);
                            ammo.amount = allowedAmount;
                        }
                    }
                    else
                    {
                        // Ammo not allowed
                        AddToDictKey(disallowedItems, ammo.name, ammo.amount);
                        ammo.amount = 0;
                    }

                    if (ammo.amount <= 0)
                        loadout.ammo = null;
                    else
                        AddToDictKey(countedAmmo, ammo.name, ammo.amount);
                }

                // Don't impose ammo limits if allowed ammo is null
                if (loadout.reserveAmmo != null && allowedAmmo != null)
                {
                    for (var i = 0; i < loadout.reserveAmmo.Count; i++)
                    {
                        var ammo = loadout.reserveAmmo[i];

                        // Make sure ammo name exists
                        if (ItemManager.itemDictionaryByName[ammo.name] == null)
                        {
                            pluginInstance.LogWarning($"Ammo type '{ammo.name}' is not a valid item. Removing from loadout.");
                            ammo.amount = 0;
                        }
                        else if (allowedAmmo.ContainsKey(ammo.name))
                        {
                            // Don't impose a limit if the allowed amount is negative
                            if (allowedAmmo[ammo.name] >= 0)
                            {
                                var countedAmount = countedAmmo.ContainsKey(ammo.name) ? countedAmmo[ammo.name] : 0;
                                var remainingAllowedAmount = allowedAmmo[ammo.name] - countedAmount;

                                if (ammo.amount > remainingAllowedAmount)
                                {
                                    // Reduce ammo to the allowed amount
                                    AddToDictKey(disallowedItems, ammo.name, ammo.amount - remainingAllowedAmount);
                                    ammo.amount = remainingAllowedAmount;
                                }
                            }
                        }
                        else
                        {
                            // Ammo not allowed
                            AddToDictKey(disallowedItems, ammo.name, ammo.amount);
                            ammo.amount = 0;
                        }

                        if (ammo.amount <= 0)
                        {
                            loadout.reserveAmmo.RemoveAt(i);
                            i--;
                        }
                        else
                            AddToDictKey(countedAmmo, ammo.name, ammo.amount);
                    }
                }

                return disallowedItems.IsEmpty() ? ValidationResult.Valid : ValidationResult.Changed;
            }

            public abstract void SaveData();
        }

        #endregion

        #region Configuration

        private TurretLoadout GetPlayerActiveLoadout(string userIdString)
        {
            if (HasPermissionAny(userIdString, Permission_Manage, Permission_ManageCustom))
            {
                var playerData = GetPlayerData(userIdString);
                if (playerData.activeLoadout == string.Empty)
                {
                    // Player has explicitly set no active loadout
                    return null;
                }

                if (playerData.activeLoadout != null)
                {
                    var loadout = playerData.FindByName(playerData.activeLoadout);
                    if (loadout == null)
                        return null;

                    var validationResult = playerData.ValidateAndPossiblyReduceLoadout(loadout, GetPlayerLoadoutRules(userIdString));
                    if (validationResult == LoadoutManager.ValidationResult.InvalidWeapon ||
                        validationResult == LoadoutManager.ValidationResult.DisallowedWeapon)
                        return null;

                    return loadout;
                }
            }

            // Player doesn't have permission to use custom loadouts, or they have not set an active one
            return GetPlayerDefaultLoadout(userIdString);
        }

        // Returns the last default loadout the player has permission to
        private TurretLoadout GetPlayerDefaultLoadout(string userIdString)
        {
            if (pluginConfig.defaultLoadouts == null || pluginConfig.defaultLoadouts.Length == 0)
                return null;

            for (var i = pluginConfig.defaultLoadouts.Length - 1; i >= 0; i--)
            {
                var loadout = pluginConfig.defaultLoadouts[i];
                if (!string.IsNullOrWhiteSpace(loadout.name) &&
                    permission.UserHasPermission(userIdString, GetDefaultLoadoutPermission(loadout.name)))
                {
                    return loadout;
                }
            }

            return null;
        }

        private LoadoutRuleset GetPlayerLoadoutRules(IPlayer player) => GetPlayerLoadoutRules(player.Id);

        // Returns the last loadout ruleset the player has permission to
        private LoadoutRuleset GetPlayerLoadoutRules(string userIdString)
        {
            if (pluginConfig.loadoutRulesets == null || pluginConfig.loadoutRulesets.Length == 0)
                return pluginConfig.emptyLoadoutRuleset;

            for (var i = pluginConfig.loadoutRulesets.Length - 1; i >= 0; i--)
            {
                var loadoutRuleset = pluginConfig.loadoutRulesets[i];
                if (!string.IsNullOrWhiteSpace(loadoutRuleset.name) &&
                    permission.UserHasPermission(userIdString, GetLoadoutRulesetPermission(loadoutRuleset.name)))
                {
                    return loadoutRuleset;
                }
            }

            return pluginConfig.emptyLoadoutRuleset;
        }

        internal class Configuration : SerializableConfiguration
        {
            [JsonIgnore]
            public LoadoutRuleset emptyLoadoutRuleset = new LoadoutRuleset()
            {
                // Nothing allowed
                allowedWeapons = new string[0],
                allowedAttachments = new string[0],
                allowedAmmo = new Dictionary<string, int>(),
            };

            [JsonProperty("LockAutoFilledTurrets")]
            public bool lockAutoFilledTurrets = false;

            [JsonProperty("MaxLoadoutsPerPlayer")]
            public int maxLoadoutsPerPlayer = 10;

            [JsonProperty("DefaultLoadouts")]
            public DefaultLoadout[] defaultLoadouts = new DefaultLoadout[]
            {
                new DefaultLoadout()
                {
                    name = "ak47",
                    weapon = "rifle.ak",
                    skin = 885146172,
                    ammo = new AmmoAmount()
                    {
                        name = "ammo.rifle",
                        amount = 30,
                    },
                    reserveAmmo = new List<AmmoAmount>()
                    {
                        new AmmoAmount()
                        {
                            name = "ammo.rifle",
                            amount = 128
                        },
                        new AmmoAmount()
                        {
                            name = "ammo.rifle",
                            amount = 128
                        }
                    },
                },
                new DefaultLoadout()
                {
                    name = "m249",
                    weapon = "lmg.m249",
                    skin = 1831294069,
                    attachments = new List<string>()
                    {
                        "weapon.mod.lasersight",
                        "weapon.mod.silencer"
                    },
                    ammo = new AmmoAmount()
                    {
                        name = "ammo.rifle.explosive",
                        amount = 100
                    },
                    reserveAmmo = new List<AmmoAmount>()
                    {
                        new AmmoAmount()
                        {
                            name = "ammo.rifle.incendiary",
                            amount = 128,
                        },
                        new AmmoAmount()
                        {
                            name = "ammo.rifle.hv",
                            amount = 128
                        }
                    },
                }
            };

            [JsonProperty("LoadoutRulesets")]
            public LoadoutRuleset[] loadoutRulesets = new LoadoutRuleset[]
            {
                new LoadoutRuleset()
                {
                    name = "onlypistols",
                    allowedWeapons = new string[]
                    {
                        "pistol.eoka",
                        "pistol.m92",
                        "pistol.nailgun",
                        "pistol.python",
                        "pistol.revolver",
                        "pistol.semiauto",
                        "pistol.water",
                    },
                    allowedAmmo = new Dictionary<string, int>()
                    {
                        ["ammo.pistol"] = 600,
                        ["ammo.pistol.hv"] = 400,
                        ["ammo.pistol.fire"] = 200,
                    },
                },
                new LoadoutRuleset()
                {
                    name = "norifles",
                    disallowedWeapons = new string[]
                    {
                        "rifle.ak",
                        "rifle.bolt",
                        "rifle.l96",
                        "rifle.lr300",
                        "rifle.m39",
                        "rifle.semiauto",
                        "lmg.m249",
                    }
                },
                new LoadoutRuleset()
                {
                    name = "unlimited"
                }
            };
        }

        internal class LoadoutRuleset
        {
            [JsonProperty("Name")]
            public string name;

            [JsonProperty("AllowedWeapons", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string[] allowedWeapons;

            [JsonProperty("DisallowedWeapons", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string[] disallowedWeapons;

            [JsonProperty("AllowedAttachments", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string[] allowedAttachments;

            [JsonProperty("DisallowedAttachments", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string[] disallowedAttachments;

            [JsonProperty("AllowedAmmo", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public Dictionary<string, int> allowedAmmo;

            public bool IsWeaponAllowed(string weaponName)
            {
                if (allowedWeapons != null)
                    return allowedWeapons.Contains(weaponName);
                else if (disallowedWeapons != null)
                    return !disallowedWeapons.Contains(weaponName);

                return true;
            }

            public bool IsAttachmentAllowed(string attachmentName)
            {
                if (allowedAttachments != null)
                    return allowedAttachments.Contains(attachmentName);
                else if (disallowedAttachments != null)
                    return !disallowedAttachments.Contains(attachmentName);

                return true;
            }
        }

        internal class TurretLoadout
        {
            [JsonProperty("Name")]
            public string name;

            [JsonProperty("Weapon")]
            public string weapon;

            [JsonProperty("Skin", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public ulong skin = 0;

            [JsonProperty("Peacekeeper", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool peacekeeper = false;

            [JsonProperty("Attachments", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public List<string> attachments;

            [JsonProperty("Ammo", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public AmmoAmount ammo;

            [JsonProperty("ReserveAmmo", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public List<AmmoAmount> reserveAmmo;

            [JsonIgnore]
            public virtual bool IsDefault => false;

            public string GetDisplayName(string userIdString) =>
                IsDefault ? pluginInstance.GetDefaultLoadoutName(userIdString) : name;
        }

        internal class DefaultLoadout : TurretLoadout
        {
            public override bool IsDefault => true;
        }

        internal class AmmoAmount
        {
            [JsonProperty("Name")]
            public string name;

            [JsonProperty("Amount")]
            public int amount;
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

        private bool MaybeUpdateConfig(Configuration config)
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

        #region Localization

        private void ReplyToPlayer(IPlayer player, string messageName, params object[] args) =>
            player.Reply(string.Format(GetMessage(player, messageName), args));

        private void ChatMessage(BasePlayer basePlayer, string messageName, params object[] args) =>
            basePlayer.ChatMessage(string.Format(GetMessage(basePlayer, messageName), args));

        private string GetMessage(BasePlayer basePlayer, string messageName, params object[] args) =>
            GetMessage(basePlayer.UserIDString, messageName, args);

        private string GetMessage(IPlayer player, string messageName, params object[] args) =>
            GetMessage(player.Id, messageName, args);

        private string GetMessage(string userIdString, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, userIdString);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Generic.RestrictedItems"] = "<color=#f44>Restricted items not saved:</color>",
                ["Generic.DefaultLoadoutName"] = "Default",
                ["Generic.Error.NoPermission"] = "You don't have permission to use this command.",
                ["Generic.Error.NoTurretWeapon"] = "Error: That auto turret has no weapon.",
                ["Generic.Error.WeaponNotAllowed"] = "Error: Weapon not allowed: <color=#f44>{0}</color>.",
                ["Generic.Error.LoadoutNotFound"] = "Error: Loadout <color=#fe4>{0}</color> not found.",
                ["Generic.Error.LoadoutNameLength"] = "Error: Loadout name may not be longer than <color=#fe4>{0}</color> characters.",
                ["Generic.Error.DefaultLoadout"] = "Error: You cannot edit the default loadout.",
                ["Generic.Header"] = "<size=16><color=#fa5>Turret Loadouts</color></size>",
                ["Generic.FilledFromLoadout"] = "Filled turret with loadout: <color=#fe4>{0}</color>. Type <color=#fe4>/tl help</color> for more options.",

                ["Command.Activate.Error.Syntax"] = "Syntax: <color=#fe4>tl <loadout name></color>",
                ["Command.Activate.Success.Deactivated"] = "Deactivated <color=#fe4>{0}</color> loadout.",

                ["Command.Default.HelpHint"] = "Use <color=#fe4>tl help</color> for more options.",
                ["Command.Default.NoActive"] = "No active turret loadout.",
                ["Command.Default.Active"] = "<size=16><color=#fa5>Active Turret Loadout</color>: {0}</size>",
                ["Command.Default.Mode.AttackAll"] = "<color=#fe4>Mode</color>: <color=#f44>Attack All</color>",
                ["Command.Default.Mode.Peacekeeper"] = "<color=#fe4>Mode</color>: <color=#6e6>Peacekeeper</color>",
                ["Command.Default.Weapon"] = "<color=#fe4>Weapon</color>: {0}{1}",
                ["Command.Default.Attachments"] = "<color=#fe4>Attachments</color>:",
                ["Command.Default.ReserveAmmo"] = "<color=#fe4>Reserve ammo</color>:",

                ["Command.List.NoLoadouts"] = "You don't have any turret loadouts.",
                ["Command.List.ToggleHint"] = "Use <color=#fe4>tl <loadout name></color> to activate or deactivate a loadout.",
                ["Command.List.Item"] = "<color=#fe4>{1}</color>{0} - {2}{3}",
                ["Command.List.Item.Active"] = " <color=#5bf>[ACTIVE]</color>",

                ["Command.Save.Error.Syntax"] = "Syntax: <color=#fe4>tl save <name></color>",
                ["Command.Save.Error.NoTurretFound"] = "Error: No auto turret found.",
                ["Command.Save.Error.LoadoutExists"] = "Error: Loadout <color=#fe4>{0}</color> already exists. Use <color=#fe4>tl update {0}</color> to update it.",
                ["Command.Save.Error.TooManyLoadouts"] = "Error: You may not have more than <color=#fe4>{0}</color> loadouts. You may delete another loadout and try again.",
                ["Command.Save.Success"] = "Turret loadout saved as <color=#fe4>{0}</color>. Activate it with <color=#fe4>tl {0}</color>.",

                ["Command.Update.Error.Syntax"] = "Syntax: <color=#fe4>tl update <name></color>",
                ["Command.Update.Success"] = "Updated <color=#fe4>{0}</color> loadout.",

                ["Command.Rename.Error.Syntax"] = "Syntax: <color=#fe4>tl rename <name> <new name></color>",
                ["Command.Rename.Error.LoadoutNameTaken"] = "Error: Loadout name <color=#fe4>{0}</color> is already taken.",
                ["Command.Rename.Success"] = "Renamed <color=#fe4>{0}</color> loadout to <color=#fe4>{1}</color>.",

                ["Command.Delete.Error.Syntax"] = "Syntax: <color=#fe4>tl delete <name></color>",
                ["Command.Delete.Success"] = "Deleted <color=#fe4>{0}</color> loadout.",

                ["Command.Help.Details"] = "<color=#fe4>tl</color> - Show your active loadout details",
                ["Command.Help.List"] = "<color=#fe4>tl list</color> - List turret loadouts",
                ["Command.Help.Activate"] = "<color=#fe4>tl <loadout name></color> - Toggle whether a loadout is active",
                ["Command.Help.Save"] = "<color=#fe4>tl save <name></color> - Save a loadout with the turret you are aiming at",
                ["Command.Help.Update"] = "<color=#fe4>tl update <name></color> - Overwrite an existing loadout with the turret you are aiming at",
                ["Command.Help.Rename"] = "<color=#fe4>tl rename <name> <new name></color> - Rename a loadout",
                ["Command.Help.Delete"] = "<color=#fe4>tl delete <name></color> - Delete a loadout",

                ["Abbreviation.weapon.mod.8x.scope"] = "16x",
                ["Abbreviation.weapon.mod.flashlight"] = "FL",
                ["Abbreviation.weapon.mod.holosight"] = "HS",
                ["Abbreviation.weapon.mod.lasersight"] = "LS",
                ["Abbreviation.weapon.mod.muzzleboost"] = "MBS",
                ["Abbreviation.weapon.mod.muzzlebrake"] = "MBR",
                ["Abbreviation.weapon.mod.silencer"] = "SL",
                ["Abbreviation.weapon.mod.simplesight"] = "SS",
                ["Abbreviation.weapon.mod.small.scope"] = "8x",
            }, this, "en");
        }

        #endregion
    }
}
