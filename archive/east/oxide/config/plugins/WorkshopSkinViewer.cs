using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Workshop Skin Viewer", "Orange", "1.0.0")]
    [Description("Allows you to check item skins from workshop")]
    public class WorkshopSkinViewer : RustPlugin
    {
        #region Vars

        private const string permUse = "workshopskinviewer.use";

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            cmd.AddChatCommand(config.command, this, nameof(cmdGiveSkinnedItem));
            permission.RegisterPermission(permUse, this);
        }

        #endregion

        #region Commands

        private void cmdGiveSkinnedItem(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, permUse) == false)
            {
                Message(player, "Permission");
                return;
            }

            if (args == null || args?.Length < 2)
            {
                Message(player, "Usage");
                return;
            }

            GiveItem(player, args[0], args[1]);
        }

        #endregion

        #region Core

        private void GiveItem(BasePlayer player, string shortname, string skinIDString)
        {
            try
            {
                var skinID = 0UL;
                if (ulong.TryParse(skinIDString, out skinID) == false)
                {
                    Message(player, "Error");
                    return;
                }
                
                var item = ItemManager.CreateByName(shortname, 1, skinID);
                if (item == null)
                {
                    Message(player, "Error");
                    return;
                }

                var held = item.GetHeldEntity();
                if (held != null)
                {
                    held.skinID = skinID;
                    held.SendNetworkUpdate();
                }

                player.GiveItem(item);
                Message(player, "Received", item.info.displayName.english, skinID);
            }
            catch
            {
                Message(player, "Error");
            }
        }

        #endregion

        #region Configuration 1.1.0

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Command")]
            public string command;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                command = "wskin",
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
                PrintError("Configuration file is corrupt! Unloading plugin...");
                Interface.Oxide.RootPluginManager.RemovePlugin(this);
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
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
                {"Usage", "Usage:\n/wskin shortname skinID"},
                {"Permission", "You don't have permission to use that!"},
                {"Received", "You received {0} with skin #{1}"},
                {"Error", "Looks as you made something wrong! SkinID or item shortname!"}
            }, this);
        }
        
        private void Message(BasePlayer player, string messageKey, params object[] args)
        {
            if (player == null)
            {
                return;
            }

            var message = GetMessage(messageKey, player.UserIDString, args);
            player.SendConsoleCommand("chat.add", (object) 0, (object) message);
        }

        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
        }

        #endregion
    }
}
