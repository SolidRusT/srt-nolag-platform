using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Rust.Workshop;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Item Skin Randomizer", "Mevent", "1.6.1")]
    [Description("Simple plugin that will select a random skin for an item when crafting")]
    public class ItemSkinRandomizer : RustPlugin
    {
        #region Fields

        private const string permUse = "itemskinrandomizer.use";

        private const string permUseEntities = "itemskinrandomizer.useentities";

        private const string permReSkin = "itemskinrandomizer.reskin";

        private Dictionary<string, List<ulong>> skins = new Dictionary<string, List<ulong>>();

        #endregion

        #region Config

        private static ConfigData _config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty("Commands")] public string[] Commands =
            {
                "reskin",
                "rskin",
                "randomskin"
            };

            [JsonProperty("Blocked skin id's")] public ulong[] BlockedSkins =
            {
                12,
                345,
                6789
            };

            [JsonProperty("Blocked items")] public string[] BlockedItems =
            {
                "grenade.f1",
                "explosive.satchel"
            };

            [JsonProperty("Set random skins for entities?")]
            public bool UseEntities = true;

            [JsonProperty("Blocked entities")] public string[] BlockedEntities =
            {
                "grenade.f1",
                "explosive.satchel"
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<ConfigData>();
                if (_config == null) LoadDefaultConfig();
            }
            catch
            {
                for (var i = 0; i < 3; i++)
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");

                LoadDefaultConfig();
                return;
            }

            ValidateConfig();
            SaveConfig();
        }

        private void ValidateConfig()
        {
            if (Interface.Oxide.CallHook("Debug_UseDefaultValues") != null)
            {
                PrintWarning("Using default configuration in debug mode");
                _config = new ConfigData();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(permUse, this);

            permission.RegisterPermission(permUseEntities, this);

            permission.RegisterPermission(permReSkin, this);

            AddCovalenceCommand(_config.Commands, nameof(CmdControl));

            if (!_config.UseEntities)
                Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            GenerateSkins();
        }

        private void Unload()
        {
            _config = null;
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            if (task.skinID != 0 || !permission.UserHasPermission(task.owner.UserIDString, permUse) ||
                _config.BlockedItems.Contains(item.info.shortname)) return;

            SetRandomSkin(null, item);
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity == null || entity.OwnerID == 0 || entity.skinID != 0 ||
                !permission.UserHasPermission(entity.OwnerID.ToString(), permUse) ||
                _config.BlockedEntities.Contains(entity.ShortPrefabName)) return;

            SetRandomSkin(null, entity);
        }

        #endregion

        #region Commands

        private void CmdControl(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null)
                return;

            if (!cov.HasPermission(permReSkin))
            {
                Message(player, Permission);
                return;
            }

            var item = player.GetActiveItem();
            if (item != null)
            {
                SetRandomSkin(player, item);
                return;
            }

            if (!player.CanBuild())
            {
                Message(player, CantBuild);
                return;
            }

            var entity = GetLookEntity<BaseEntity>(player);
            if (entity != null)
            {
                SetRandomSkin(player, entity);
                return;
            }

            Message(player, NoObject);
        }

        #endregion

        #region Utils

        private static string FixNames(string name)
        {
            switch (name)
            {
                case "wall.external.high.wood": return "wall.external.high";
                case "electric.windmill.small": return "generator.wind.scrap";
                case "graveyardfence": return "wall.graveyard.fence";
                case "coffinstorage": return "coffin.storage";
            }

            return name;
        }

        private void GenerateSkins()
        {
            foreach (var pair in Approved.All)
            {
                if (pair.Value == null || pair.Value.Skinnable == null)
                    continue;

                var skinId = pair.Value.WorkshopdId;
                if (skinId == 0 || _config.BlockedSkins.Contains(skinId))
                    continue;

                var key = pair.Value.Skinnable.ItemName;
                if (key.Contains("lr300"))
                    key = "rifle.lr300";

                List<ulong> list;
                if (skins.TryGetValue(key, out list))
                {
                    if (!list.Contains(skinId))
                        skins[key].Add(skinId);
                }
                else
                {
                    skins.Add(key, new List<ulong>());
                }
            }
        }

        private void SetRandomSkin(BasePlayer player, Item item)
        {
            var skin = GetRandomSkin(item.info.shortname);
            if (skin == 0) return;

            item.skin = skin;
            item.MarkDirty();

            var held = item.GetHeldEntity();
            if (held != null)
            {
                held.skinID = skin;
                held.SendNetworkUpdate();
            }

            Message(player, ChangedTo, skin);
        }

        private void SetRandomSkin(BasePlayer player, BaseEntity entity)
        {
            var shortname = entity.ShortPrefabName;

            switch (shortname)
            {
                case "sleepingbag_leather_deployed":
                    shortname = "sleepingbag";
                    break;

                case "vendingmachine.deployed":
                    shortname = "vending.machine";
                    break;

                case "woodbox_deployed":
                    shortname = "box.wooden";
                    break;

                case "reactivetarget_deployed":
                    shortname = "target.reactive";
                    break;

                default:
                {
                    shortname = entity.ShortPrefabName;
                    shortname = Regex.Replace(shortname, "\\.deployed|_deployed", "");
                    shortname = FixNames(shortname);
                    break;
                }
            }

            var def = ItemManager.FindItemDefinition(shortname);
            if (def == null) return;

            var skin = GetRandomSkin(def.shortname);
            if (skin == 0) return;

            entity.skinID = skin;
            entity.SendNetworkUpdate();

            Message(player, ChangedTo, skin);
        }

        private ulong GetRandomSkin(string key)
        {
            List<ulong> list;
            return skins.TryGetValue(key, out list) ? list.GetRandom() : 0;
        }

        private static T GetLookEntity<T>(BasePlayer player)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit)) return default(T);

            var entity = hit.GetEntity();
            return entity == null ? default(T) : entity.GetComponent<T>();
        }

        #endregion

        #region Lang

        private const string
            CantBuild = "Cant Build",
            ChangedTo = "Changed To",
            NoObject = "No Object",
            Permission = "Permission";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {Permission, "You don't have permission to use that!"},
                {NoObject, "You need to hold item or look on object!"},
                {ChangedTo, "Skin was changed to {0}"},
                {CantBuild, "You need building privilege to use that!"}
            }, this);
        }

        private void Message(BasePlayer player, string messageKey, params object[] args)
        {
            if (player == null) return;

            player.ChatMessage(GetMessage(messageKey, player.UserIDString, args));
        }

        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
        }

        #endregion
    }
}