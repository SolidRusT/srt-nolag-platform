using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Research Unlocked", "MJSU", "1.0.1")]
    [Description("Displays a ui if you have already unlocked the item placed in a research table")]
    public class ResearchUnlocked : RustPlugin
    {
        #region Class Fields
        private PluginConfig _pluginConfig; //Plugin Config

        private const string UsePermission = "researchunlocked.use";

        private readonly Hash<ResearchTable, List<BasePlayer>> _lootingPlayers = new Hash<ResearchTable, List<BasePlayer>>();

        private bool _init;
        private string _notLearnedColor;
        private string _learnedColor;
        #endregion

        #region Setup & Loading

        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.AlreadyLearned] = "Already Learned",
                [LangKeys.NotLearned] = "Not Learned"
            }, this);
        }
        
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            config.Pos = config.Pos ?? new UiPosition(0.86525f, 0.8025f, 0.86525f + 0.08f, 0.8025f + 0.03f);
            return config;
        }

        private void OnServerInitialized()
        {
            _init = true;
            _notLearnedColor = Ui.Color(_pluginConfig.NotLearnedColor);
            _learnedColor = Ui.Color(_pluginConfig.LearnedColor);
            
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                ResearchTable table = player.inventory.loot.entitySource as ResearchTable;
                if (table != null)
                {
                    OnLootEntity(player, table);
                }
            }
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyAllUi(player);
            }
        }
        #endregion

        #region uMod Hooks
        private void OnLootEntity(BasePlayer player, ResearchTable table)
        {
            if (!HasPermission(player, UsePermission) && !player.IsAdmin)
            {
                return;
            }
            
            if (!_lootingPlayers.ContainsKey(table))
            {
                _lootingPlayers[table] = new List<BasePlayer>();
            }

            _lootingPlayers[table].Add(player);

            Item item = table.GetTargetItem();
            if (item == null)
            {
                return;
            }

            if (!table.IsItemResearchable(item))
            {
                return;
            }

            CreateUnlockedUi(player, player.blueprints.HasUnlocked(item.info));
        }

        private void OnLootEntityEnd(BasePlayer player, ResearchTable table)
        {
            if (!_lootingPlayers.ContainsKey(table))
            {
                return;
            }

            _lootingPlayers[table].RemoveAll(p => p.userID == player.userID);

            if (_lootingPlayers[table].Count == 0)
            {
                _lootingPlayers.Remove(table);
            }

            DestroyAllUi(player);
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (!_init)
            {
                return;
            }

            NextTick(() =>
            {
                ResearchTable table = container.entityOwner as ResearchTable;
                if (table == null)
                {
                    return;
                }

                if (!_lootingPlayers.ContainsKey(table))
                {
                    return;
                }

                if (!table.IsItemResearchable(item))
                {
                    foreach (BasePlayer player in _lootingPlayers[table])
                    {
                        DestroyAllUi(player);
                    }

                    return;
                }

                foreach (BasePlayer player in _lootingPlayers[table])
                {
                    CreateUnlockedUi(player, player.blueprints.HasUnlocked(item.info));
                }
            });
        }

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            NextTick(() =>
            {
                ResearchTable table = container.entityOwner as ResearchTable;
                if (table == null)
                {
                    return;
                }
                
                if (!_lootingPlayers.ContainsKey(table))
                {
                    return;
                }

                if (!table.IsItemResearchable(item))
                {
                    return;
                }

                foreach (BasePlayer player in _lootingPlayers[table])
                {
                    DestroyAllUi(player);
                }
            });
        }

        #endregion

        #region Helpers
        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        private string Lang(string key, BasePlayer player = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, player?.UserIDString), args);
            }
            catch(Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception\n:{ex}");
                throw;
            }
        }

        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue("##00CC00FF")]
            [JsonProperty(PropertyName = "Learned Color")]
            public string LearnedColor { get; set; }
        
            [DefaultValue("#CC0000FF")]
            [JsonProperty(PropertyName = "Not Learned Color")]
            public string NotLearnedColor { get; set; }
            
            [JsonProperty(PropertyName = "Ui Position")]
            public UiPosition Pos { get; set; }
        }
        
        private class LangKeys
        {
            public const string AlreadyLearned = "AlreadyLearned";
            public const string NotLearned = "NotLearned";
        }
        #endregion

        #region UI
        private const string UiPanelName = "ResearchUnlocked_UI";

        private static class Ui
        {
            private static string UiPanel { get; set; }

            public static CuiElementContainer Container(string color, UiPosition pos, bool useCursor, string panel, string parent = "Hud.Menu")
            {
                UiPanel = panel;
                return new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax()},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panel
                    }
                };
            }

            public static void Panel(ref CuiElementContainer container, string color, UiPosition pos, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() },
                    CursorEnabled = cursor
                },
                    UiPanel);
            }

            public static void Label(ref CuiElementContainer container, string text, int size, UiPosition pos, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() }

                },
                    UiPanel);
            }

            public static string Color(string hexColor)
            {
                hexColor = hexColor.TrimStart('#');
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                int alpha = 255;
                if (hexColor.Length == 8)
                {
                    alpha = int.Parse(hexColor.Substring(6, 2), NumberStyles.AllowHexSpecifier);
                }
                return $"{red / 255.0} {green / 255.0} {blue / 255.0} {alpha / 255}";
            }
        }

        private class UiPosition
        {
            public float XMin { get; set; }
            public float YMin { get; set; }
            public float XMax { get; set; }
            public float YMax { get; set; }

            public UiPosition(float xMin, float yMin, float xMax, float yMax)
            {
                XMin = xMin;
                YMin = yMin;
                XMax = xMax;
                YMax = yMax;
            }

            public string GetMin() => $"{XMin} {YMin}";
            public string GetMax() => $"{XMax} {YMax}";

            public override string ToString()
            {
                return $"{XMin} {YMin} {XMax} {YMax}";
            }
        }

        private void DestroyAllUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UiPanelName);
        }
        #endregion

        #region UI Variables
        private readonly string _clear = Ui.Color("#00000000");
        
        private readonly UiPosition _fullArea = new UiPosition(0, 0, 1, 1);
        #endregion

        #region UI Creation
        private void CreateUnlockedUi(BasePlayer player, bool unlocked)
        {
            CuiElementContainer container = Ui.Container(_clear, _pluginConfig.Pos, false, UiPanelName, "Overlay");

            string key = unlocked ? LangKeys.AlreadyLearned : LangKeys.NotLearned;
            
            Ui.Panel(ref container, unlocked ? _notLearnedColor : _learnedColor, _fullArea);
            Ui.Label(ref container, Lang(key, player), 14, _fullArea);

            CuiHelper.DestroyUi(player, UiPanelName);
            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}
