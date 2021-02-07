using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core.Plugins;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Turret Manager", "OrangeDoggo", "1.2.0")]
    [Description("Allows you to place down a turret fast & easy. (Auto fill up turret,Auto auth self (or/and friends)")]
    internal class TurretManager : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin Friends;

        private Configuration PluginConfig;

        private const string autoFillPerm = "turretmanager.autofill";
        private const string autoAuthPerm = "turretmanager.autoauth";
        private const string autoTogglePerm = "turretmanager.autotoggle";

        private ItemDefinition ammoToLoad;
        private ItemDefinition gunToLoad;

        #endregion

        #region Hooks

        private void Init()
        {
            if (!PluginConfig.LockAutoFilledTurrets)
            {
                Unsubscribe(nameof(OnEntityDeath));
                Unsubscribe(nameof(OnTurretToggle));
                Unsubscribe(nameof(CanMoveItem));
            }
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(autoFillPerm, this);
            permission.RegisterPermission(autoAuthPerm, this);
            permission.RegisterPermission(autoTogglePerm, this);

            gunToLoad = ItemManager.FindItemDefinition(PluginConfig.GunShortName);
            if (gunToLoad == null)
            {
                LogError("Invalid item for Gun");
                return;
            };

            if (gunToLoad.category != ItemCategory.Weapon)
                LogError($"Item '{gunToLoad.shortname}' isn't a valid weapon!");
            else
                ammoToLoad = (ItemManager.Create(gunToLoad).GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType;
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

            if (HasPermission(ownerPlayer, autoAuthPerm))
            {
                if (PluginConfig.AutoAuthFriends && Friends != null)
                {
                    string[] friendsList = Friends.Call<string[]>("GetFriendList", ownerPlayer.userID);
                    if (friendsList != null)
                    {
                        foreach (string friendUserId in friendsList)
                        {
                            BasePlayer friend = BasePlayer.Find(friendUserId);
                            if (friend == null) continue;

                            if (turret.authorizedPlayers.All(x => x.userid != friend.userID))
                            {
                                turret.authorizedPlayers.Add(new PlayerNameID
                                {
                                    userid = friend.userID,
                                    username = friend.UserIDString
                                });
                            }
                        }
                    }
                }

                if (turret.authorizedPlayers.All(player => player.userid != ownerPlayer.userID))
                {
                    turret.authorizedPlayers.Add(new PlayerNameID
                    {
                        userid = ownerPlayer.userID, 
                        username = ownerPlayer.UserIDString
                    });
                }
            }

            if (HasPermission(ownerPlayer, autoFillPerm))
            {
                if (ammoToLoad != null && gunToLoad != null)
                {
                    var weaponItem = ItemManager.Create(gunToLoad);
                    weaponItem.MoveToContainer(turret.inventory, 0);

                    var heldWeapon = weaponItem.GetHeldEntity() as BaseProjectile;
                    heldWeapon.primaryMagazine.contents = 0;

                    turret.UpdateAttachedWeapon();
                    turret.CancelInvoke(turret.UpdateAttachedWeapon);

                    var weapon = turret.GetAttachedWeapon();
                    weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;

                    for (var i = 0; i < PluginConfig.AmountOfStacks; i++)
                    {
                        ItemManager.Create(ammoToLoad, PluginConfig.AmmoStackSize).MoveToContainer(turret.inventory, i + 1);
                    }

                    if (PluginConfig.LockAutoFilledTurrets)
                        turret.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
                }
                else
                {
                    LogError("{0} definition is null. Please contact the plugin author if this happens again.", ammoToLoad == null ? "Weapon ammo" : gunToLoad == null ? "Weapon" : ammoToLoad == null && gunToLoad == null ? "Weapon and Ammo" : "");
                }
            }

            if (HasPermission(ownerPlayer, autoTogglePerm))
            {
                turret.SetOnline();
                var turretSwitch = turret.GetComponentInChildren<ElectricSwitch>();
                if (turretSwitch != null)
                    turretSwitch.SetSwitch(true);
            }

            turret.SendNetworkUpdateImmediate();
        }

        private void OnEntityDeath(AutoTurret turret)
        {
            if (turret != null && IsTurretLocked(turret))
                turret.inventory.Kill();
        }

        private void OnTurretToggle(AutoTurret turret)
        {
            // Remove items if powering down while locked and out of ammo
            // Otherwise, the turret would be unusable other than picking it up
            if (turret != null && turret.IsOnline() && IsTurretLocked(turret) && GetTotalAmmo(turret) == 0)
            {
                turret.inventory.Clear();
                turret.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
            }
        }

        private object CanMoveItem(Item item)
        {
            if (item.parent == null) return null;

            var turret = item.parent.entityOwner as AutoTurret;
            if (turret == null) return null;

            // Fix issue where right-clicking an item in a locked turret inventory allows moving it
            if (item.parent.IsLocked())
                return false;

            return null;
        }

        #endregion

        #region Helper Methods

        private bool HasPermission(BasePlayer player, string perm) =>
            permission.UserHasPermission(player.UserIDString, perm) || PluginConfig.AdminBypass && (permission.UserHasGroup(player.UserIDString, "admin"));

        private bool IsTurretLocked(AutoTurret turret) =>
            turret.inventory != null && turret.inventory.HasFlag(ItemContainer.Flag.IsLocked);

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

        #endregion

        #region Configuration

        internal class Configuration : SerializableConfiguration
        {
            [JsonProperty("Gun (Shortname)")]
            public string GunShortName = "rifle.ak";

            [JsonProperty("Ammo amount")]
            public int AmmoStackSize = 128;

            [JsonProperty("Stack amount")]
            public int AmountOfStacks = 6;

            [JsonProperty("Lock auto filled turrets (true/false)")]
            public bool LockAutoFilledTurrets = false;

            [JsonProperty("Auto Authorize Friends (Requires Friends API)")]
            public bool AutoAuthFriends = false;

            [JsonProperty("Admin Bypass")]
            public bool AdminBypass = false;
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

        protected override void LoadDefaultConfig() => PluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                PluginConfig = Config.ReadObject<Configuration>();
                if (PluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(PluginConfig))
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
            Config.WriteObject(PluginConfig, true);
        }

        #endregion
    }
}
