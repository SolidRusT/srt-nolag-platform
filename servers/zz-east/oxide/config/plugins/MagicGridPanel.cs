using System;
using System.Collections;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Magic Grid Panel", "MJSU", "1.0.6")]
    [Description("Displays players grid position in magic panel")]
    public class MagicGridPanel : RustPlugin
    {
        #region Class Fields

        [PluginReference] private Plugin MagicPanel;

        private PluginConfig _pluginConfig; //Plugin Config

        private string _gridText;

        private readonly Hash<ulong, string> _playersGrid = new Hash<ulong, string>();

        private Coroutine _updateRoutine;

        private enum UpdateEnum
        {
            All = 1,
            Panel = 2,
            Image = 3,
            Text = 4
        }

        #endregion

        #region Setup & Loading

        private void Init()
        {
            _gridText = _pluginConfig.Panel.Text.Text;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            string path = $"{Manager.ConfigPath}/MagicPanel/{Name}.json";
            DynamicConfigFile newConfig = new DynamicConfigFile(path);
            if (!newConfig.Exists())
            {
                LoadDefaultConfig();
                newConfig.Save();
            }

            try
            {
                newConfig.Load();
            }
            catch (Exception ex)
            {
                RaiseError("Failed to load config file (is the config file corrupt?) (" + ex.Message + ")");
                return;
            }

            newConfig.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(newConfig.ReadObject<PluginConfig>());
            newConfig.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            config.Panel = new Panel
            {
                Image = new PanelImage
                {
                    Enabled = config.Panel?.Image?.Enabled ?? true,
                    Color = config.Panel?.Image?.Color ?? "#FFFFFFFF",
                    Order = config.Panel?.Image?.Order ?? 0,
                    Width = config.Panel?.Image?.Width ?? 0.2f,
                    Url = config.Panel?.Image?.Url ?? "https://i.imgur.com/ENHIFZl.png",
                    Padding = config.Panel?.Image?.Padding ?? new TypePadding(0.01f, 0.00f, 0.1f, 0.1f)
                },
                Text = new PanelText
                {
                    Enabled = config.Panel?.Text?.Enabled ?? true,
                    Color = config.Panel?.Text?.Color ?? "#FFFFFFFF",
                    Order = config.Panel?.Text?.Order ?? 1,
                    Width = config.Panel?.Text?.Width ?? .8f,
                    FontSize = config.Panel?.Text?.FontSize ?? 14,
                    Padding = config.Panel?.Text?.Padding ?? new TypePadding(0.01f, 0.01f, 0.05f, 0.05f),
                    TextAnchor = config.Panel?.Text?.TextAnchor ?? TextAnchor.MiddleCenter,
                    Text = config.Panel?.Text?.Text ?? "{0}"
                }
            };
            config.PanelSettings = new PanelRegistration
            {
                BackgroundColor = config.PanelSettings?.BackgroundColor ?? "#FFF2DF08",
                Dock = config.PanelSettings?.Dock ?? "leftbottom",
                Order = config.PanelSettings?.Order ?? 3,
                Width = config.PanelSettings?.Width ?? 0.05f
            };
            return config;
        }

        private void OnServerInitialized()
        {
            MagicPanelRegisterPanels();
        }

        private void MagicPanelRegisterPanels()
        {
            if (MagicPanel == null)
            {
                PrintError("Missing plugin dependency MagicPanel: https://umod.org/plugins/magic-panel");
                UnsubscribeAll();
                return;
            }

            MagicPanel?.Call("RegisterPlayerPanel", this, Name, JsonConvert.SerializeObject(_pluginConfig.PanelSettings), nameof(GetPanel));
            InvokeHandler.Instance.InvokeRepeating(UpdatePlayerCoords, Random.Range(0, _pluginConfig.UpdateRate), _pluginConfig.UpdateRate);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            _playersGrid.Remove(player.userID);
        }

        private void Unload()
        {
            InvokeHandler.Instance.CancelInvoke(UpdatePlayerCoords);
            if (_updateRoutine != null)
            {
                InvokeHandler.Instance.StopCoroutine(_updateRoutine);
            }
        }

        private void UnsubscribeAll()
        {
            Unsubscribe(nameof(OnPlayerDisconnected));
        }

        #endregion

        #region Player Coords Update

        private void UpdatePlayerCoords()
        {
            _updateRoutine = InvokeHandler.Instance.StartCoroutine(HandleUpdatePlayerCoords());
        }

        private IEnumerator HandleUpdatePlayerCoords()
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                string grid = GetPlayerGrid(player);
                string previous = _playersGrid[player.userID];

                yield return null;
                if (grid == previous)
                {
                    continue;
                }

                _playersGrid[player.userID] = grid;
                MagicPanel?.Call("UpdatePanel", player, Name, (int) UpdateEnum.Text);
            }
        }

        private string GetPlayerGrid(BasePlayer player)
        {
            if (player == null)
            {
                return string.Empty;
            }

            return PositionToGridCoord(player.transform.position);
        }
        
        private string PositionToGridCoord(Vector3 position)
        {
            char chr;
            Vector2 vector2 = new Vector2(TerrainMeta.NormalizeX(position.x), TerrainMeta.NormalizeZ(position.z));
            float size = TerrainMeta.Size.x / 1024f;
            int num = 7;
            Vector2 vector21 = (vector2 * size) * (float)num;
            float single = Mathf.Floor(vector21.x) + 1f;
            float single1 = Mathf.Floor(size * (float)num - vector21.y);
            string empty = string.Empty;
            float single2 = single / 26f;
            float single3 = single % 26f;
            if (single3 == 0f)
            {
                single3 = 26f;
            }
            if (single2 > 1f)
            {
                chr = Convert.ToChar(64 + (int)single2);
                empty = string.Concat(empty, chr.ToString());
            }
            chr = Convert.ToChar(64 + (int)single3);
            empty = string.Concat(empty, chr.ToString());
            return string.Format("{0}{1}", empty, single1);
        }
        #endregion

        #region MagicPanel Hook

        private Hash<string, object> GetPanel(BasePlayer player)
        {
            Panel panel = _pluginConfig.Panel;
            PanelText text = panel.Text;
            if (text != null)
            {
                text.Text = string.Format(_gridText, _playersGrid[player.userID]);
            }

            return panel.ToHash();
        }

        #endregion

        #region Classes

        private class PluginConfig
        {
            [DefaultValue(5f)]
            [JsonProperty(PropertyName = "Update Rate (Seconds)")]
            public float UpdateRate { get; set; }

            [JsonProperty(PropertyName = "Panel Settings")]
            public PanelRegistration PanelSettings { get; set; }

            [JsonProperty(PropertyName = "Panel Layout")]
            public Panel Panel { get; set; }
        }

        private class PanelRegistration
        {
            public string Dock { get; set; }
            public float Width { get; set; }
            public int Order { get; set; }
            public string BackgroundColor { get; set; }
        }

        private class Panel
        {
            public PanelImage Image { get; set; }
            public PanelText Text { get; set; }

            public Hash<string, object> ToHash()
            {
                return new Hash<string, object>
                {
                    [nameof(Image)] = Image.ToHash(),
                    [nameof(Text)] = Text.ToHash()
                };
            }
        }

        private abstract class PanelType
        {
            public bool Enabled { get; set; }
            public string Color { get; set; }
            public int Order { get; set; }
            public float Width { get; set; }
            public TypePadding Padding { get; set; }

            public virtual Hash<string, object> ToHash()
            {
                return new Hash<string, object>
                {
                    [nameof(Enabled)] = Enabled,
                    [nameof(Color)] = Color,
                    [nameof(Order)] = Order,
                    [nameof(Width)] = Width,
                    [nameof(Padding)] = Padding.ToHash(),
                };
            }
        }

        private class PanelImage : PanelType
        {
            public string Url { get; set; }

            public override Hash<string, object> ToHash()
            {
                Hash<string, object> hash = base.ToHash();
                hash[nameof(Url)] = Url;
                return hash;
            }
        }

        private class PanelText : PanelType
        {
            public string Text { get; set; }
            public int FontSize { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public TextAnchor TextAnchor { get; set; }

            public override Hash<string, object> ToHash()
            {
                Hash<string, object> hash = base.ToHash();
                hash[nameof(Text)] = Text;
                hash[nameof(FontSize)] = FontSize;
                hash[nameof(TextAnchor)] = TextAnchor;
                return hash;
            }
        }

        private class TypePadding
        {
            public float Left { get; set; }
            public float Right { get; set; }
            public float Top { get; set; }
            public float Bottom { get; set; }

            public TypePadding(float left, float right, float top, float bottom)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
            }

            public Hash<string, object> ToHash()
            {
                return new Hash<string, object>
                {
                    [nameof(Left)] = Left,
                    [nameof(Right)] = Right,
                    [nameof(Top)] = Top,
                    [nameof(Bottom)] = Bottom
                };
            }
        }

        #endregion
    }
}