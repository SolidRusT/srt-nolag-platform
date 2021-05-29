using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using Physics = UnityEngine.Physics;

namespace Oxide.Plugins
{
    [Info("Item Skin Randomizer", "Orange", "1.5.3")]
    [Description("Simple plugin that will select a random skin for an item when crafting")]
    public class ItemSkinRandomizer : RustPlugin
    {
        #region Vars

        private const string permUse = "itemskinrandomizer.use";
        private const string permReSkin = "itemskinrandomizer.reskin";
        private Dictionary<string, List<ulong>> skins = new Dictionary<string, List<ulong>>();

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permReSkin, this);
            
            foreach (var command in config.commands)
            {
                cmd.AddChatCommand(command, this, nameof(cmdControlChat));
                cmd.AddConsoleCommand(command, this, nameof(cmdControlConsole));
            }
        }
		
		private void OnServerInitialized() 
		{          
            GenerateSkins();
		}

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            if (task.skinID != 0)
            {
                return;
            }

            if (permission.UserHasPermission(task.owner.UserIDString, permUse) == false)
            {
                return;
            }

            if (config.blockedItems.Contains(item.info.shortname))
            {
                return;
            }

            SetRandomSkin(null, item);
        }

        #endregion

        #region Commands

        private void cmdControlConsole(ConsoleSystem.Arg arg)
        {
            cmdControlChat(arg.Player());
        }

        private void cmdControlChat(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }
            
            if (permission.UserHasPermission(player.UserIDString, permReSkin) == false)
            {
                Message(player, "Permission");
                return;
            }

            var item = player.GetActiveItem();
            if (item != null)
            {
                SetRandomSkin(player, item);
                return;
            }

            var entity = GetLookEntity<BaseEntity>(player);
            if (entity != null)
            {
                SetRandomSkin(player, entity);
                return;
            }
            
            Message(player, "No Object");
        }

        #endregion

        #region Core

        private void GenerateSkins()
        {
            List<ulong> list;
            
            foreach (var pair in Rust.Workshop.Approved.All)
            {
                if (pair.Value == null || pair.Value.Skinnable == null)
                {
                    continue;
                }
                
                var skinId = pair.Value.WorkshopdId;
                if (config.blockedSkins.Contains(skinId) == true || skinId == 0)
                {
                    continue;
                }

                var key = pair.Value.Skinnable.ItemName;
                if (key.Contains("lr300"))
                {
                    key = "rifle.lr300";
                }
                
                if (skins.TryGetValue(key, out list) == false)
                {
                    list = new List<ulong>();
                    skins.Add(key, list);
                }
                
                list.Add(skinId);
            }
        }

        private void SetRandomSkin(BasePlayer player, Item item)
        {
            var skin = GetRandomSkin(item.info.shortname);
            if (skin == 0)
            {
                return;
            }

            item.skin = skin;
            item.MarkDirty();

            var held = item.GetHeldEntity();
            if (held != null)
            {
                held.skinID = skin;
                held.SendNetworkUpdate();
            }
            
            Message(player, "Changed To", skin);
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
            }

            var def = ItemManager.FindItemDefinition(shortname);
            if (def != null)
            {
                var skin = GetRandomSkin(def.shortname);
                if (skin == 0)
                {
                    return;
                }
                
                entity.skinID = skin;
                entity.SendNetworkUpdate();
                Message(player, "Changed To", skin);
            }
        }

        private ulong GetRandomSkin(string key)
        {
            List<ulong> list;

            if (skins.TryGetValue(key, out list) == false)
            {
                return 0;
            }

            return list.GetRandom();
        }
        
        private static T GetLookEntity<T>(BasePlayer player)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit) == false)
            {
                return default(T);
            }

            var entity = hit.GetEntity();
            if (entity == null)
            {
                return default(T);
            }

            return entity.GetComponent<T>();
        }

        #endregion

        #region Configuration | 09.2020

        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty("Commands")]
            public string[] commands =
            {
                "reskin",
                "rskin",
                "randomskin"
            };

            [JsonProperty("Blocked skin id's")]
            public ulong[] blockedSkins =
            {
                12,
                345,
                6789,
            };

            [JsonProperty("Blocked items")]
            public string[] blockedItems =
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
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                for (var i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }
                
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
                config = new ConfigData();
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Localization 1.1.1

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Permission", "You don't have permission to use that!"},
                {"No Object", "You need to hold item or look on object!"},
                {"Changed To", "Skin was changed to {0}"}
            }, this);
        }

        private void Message(BasePlayer player, string messageKey, params object[] args)
        {
            if (player == null)
            {
                return;
            }

            var message = GetMessage(messageKey, player.UserIDString, args);
            player.ChatMessage(message);
        }

        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
        }

        #endregion
    }
}