using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Magic Direction Panel", "MJSU", "1.0.0")]
    [Description("Displays players direction in magic panel")]
    public class MagicDirectionPanel : RustPlugin
    {
        #region Class Fields
        [PluginReference] private Plugin MagicPanel;

        private PluginConfig _pluginConfig; //Plugin Config

        private string _directionText;

        private readonly Hash<ulong, string> _playerDirection = new Hash<ulong, string>();

        private Coroutine _updateRoutine;

        private enum UpdateEnum { All = 1, Panel = 2, Image = 3, Text = 4 }
        #endregion

        #region Setup & Loading
        private void Init()
        {
            _directionText = _pluginConfig.Panel.Text.Text;
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.North] = "North",
                [LangKeys.Northeast] = "Northeast",
                [LangKeys.East] = "East",
                [LangKeys.Southeast] = "Southeast",
                [LangKeys.South] = "South",
                [LangKeys.Southwest] = "Southwest",
                [LangKeys.West] = "West",
                [LangKeys.Northwest] = "Northwest",
            }, this);
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
                    Url = config.Panel?.Image?.Url ?? "https://i.imgur.com/6xCIe5a.png",
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
                Order = config.PanelSettings?.Order ?? 15,
                Width = config.PanelSettings?.Width ?? 0.11f
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
            _playerDirection.Remove(player.userID);
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

        #region Player Direction Update
        private void UpdatePlayerCoords()
        {
            _updateRoutine = InvokeHandler.Instance.StartCoroutine(HandleUpdatePlayerCoords());
        }

        private IEnumerator HandleUpdatePlayerCoords()
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                string direction = GetDirection(player);
                string previous = _playerDirection[player.userID];
               
                yield return null;
                if (direction == previous)
                {
                    continue;
                }
                
                _playerDirection[player.userID] = direction;
                MagicPanel?.Call("UpdatePanel", player, Name, (int)UpdateEnum.Text);
            }
        }

        private string GetDirection(BasePlayer player)
        {
            Vector3 direction = player.eyes.rotation.eulerAngles;
            if (_pluginConfig.ShowAngle)
            {
                return $"{direction.y:0.0}\u00B0";
            }

            if (direction.y > 337.5 || direction.y <= 22.5)
                return LangKeys.North;
            
            if (direction.y > 22.5 && direction.y <= 67.5)
                return LangKeys.Northeast;
            
            if (direction.y > 67.5 && direction.y <= 112.5)
                return LangKeys.East;
            
            if (direction.y > 112.5 && direction.y <= 157.5)
                return LangKeys.Southeast;
            
            if (direction.y > 157.5 && direction.y <= 202.5)
                return LangKeys.South;
            
            if (direction.y > 202.5 && direction.y <= 247.5)
                return LangKeys.Southwest;
            
            if (direction.y > 247.5 && direction.y <= 292.5)
                return LangKeys.West;
            
            return LangKeys.Northwest;
        }
        #endregion

        #region MagicPanel Hook
        private Hash<string, object> GetPanel(BasePlayer player)
        {
            Panel panel = _pluginConfig.Panel;
            PanelText text = panel.Text;
            if (text != null)
            {
                string direction =  _playerDirection[player.userID] ?? GetDirection(player);
                if (!_pluginConfig.ShowAngle)
                {
                    direction = Lang(direction, player);
                }
                text.Text = string.Format(_directionText, direction);
            }

            return panel.ToHash();
        }
        #endregion

        #region Helper Methods

        private string Lang(string key, BasePlayer player = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, player?.UserIDString), args);
            }
            catch (Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception:\n{ex}");
                throw;
            }
        }

        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue(5f)]
            [JsonProperty(PropertyName = "Update Rate (Seconds)")]
            public float UpdateRate { get; set; }
            
            [DefaultValue(false)]
            [JsonProperty(PropertyName = "Show Angle")]
            public bool ShowAngle { get; set; }

            [JsonProperty(PropertyName = "Panel Settings")]
            public PanelRegistration PanelSettings { get; set; }

            [JsonProperty(PropertyName = "Panel Layout")]
            public Panel Panel { get; set; }
        }
        
        private class LangKeys
        {
            public const string North = "North";
            public const string Northeast = "NorthEast";
            public const string East = "East";
            public const string Southeast = "SouthEast";
            public const string South = "South";
            public const string Southwest = "SouthWest";
            public const string West = "West";
            public const string Northwest = "NorthWest";
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
