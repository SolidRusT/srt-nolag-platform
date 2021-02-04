using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Fast Loot", "DezLife", "0.0.4")]
    [Description("Loot crates quickly")]
    class FastLoot : RustPlugin
    {
        #region Var
        private const string FastLootLayer = "UI_FastLootLayer";
        private const string perm = "fastloot.use";
        #endregion

        #region Config
        private Configuration config;

        private class Configuration
        {
            [JsonProperty("Color background")]
            public string colorBackground;
            [JsonProperty("Color font")]
            public string colorFont;
            [JsonProperty("Coordinates OffsetMin")]
            public string OffsetMin;
            [JsonProperty("Coordinates OffsetMax")]
            public string OffsetMax;
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                colorBackground = "0.968627453 0.921631568632 0.882352948 0.03529412",
                colorFont = "0.87 0.84 0.80 1.00",
                OffsetMin = "290 510",
                OffsetMax = "573 540"
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion  

        #region Hook
        void OnLootEntity(BasePlayer player, LootContainer loot)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm))
                return;
            UIFastLoot(player);
        }
        private void OnLootEntityEnd(BasePlayer player, LootContainer entity) => CuiHelper.DestroyUi(player, FastLootLayer);
        private void Init() => permission.RegisterPermission(perm, this);
        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, FastLootLayer);
        }

        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["FASTLOOT_TAKE"] = "Take all",

            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["FASTLOOT_TAKE"] = "Забрать все",

            }, this, "ru");
        }

        #endregion

        #region Command

        [ConsoleCommand("UI_FastLoot")]
        void FastLootCMD(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;
            if (!permission.UserHasPermission(player.UserIDString, perm))
                return;
            if (player.inventory?.loot?.entitySource is LootContainer)
            {
                StorageContainer loot = player.inventory?.loot?.entitySource as StorageContainer;
                if (loot != null)
                {
                    loot.MoveAllInventoryItems(loot.inventory, player.inventory.containerMain);
                }
            }
           
        }
        #endregion

        #region UI
        private void UIFastLoot(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, FastLootLayer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = config.OffsetMin, OffsetMax = config.OffsetMax },
                Image = { Color = config.colorBackground, Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
            }, "Overlay", FastLootLayer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0 0 0 0", Command = "UI_FastLoot" },
                Text = { Text = lang.GetMessage("FASTLOOT_TAKE", this, player.UserIDString),FontSize = 17, Align = TextAnchor.MiddleCenter, Color = config.colorFont }
            }, FastLootLayer);
            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}
