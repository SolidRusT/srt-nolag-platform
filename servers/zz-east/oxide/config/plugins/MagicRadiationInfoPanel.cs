using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Magic Radiation Info Panel", "MJSU", "1.0.3")]
    [Description("Displays how much radiation protection the player has verse how much they need")]
    public class MagicRadiationInfoPanel : RustPlugin
    {
        #region Class Fields
        [PluginReference] private Plugin MagicPanel;

        private PluginConfig _pluginConfig; //Plugin Config

        private string _textFormat;

        private readonly Hash<ulong, PlayerRadiationData> _playerRadiation = new Hash<ulong, PlayerRadiationData>();

        private Coroutine _updateRoutine;

        private enum UpdateEnum { All = 1, Panel = 2, Image = 3, Text = 4 }
        #endregion

        #region Setup & Loading
        private void Init()
        {
            _textFormat = _pluginConfig.Panel.Text.Text;
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
                    Width = config.Panel?.Image?.Width ?? 0.28f,
                    Url = config.Panel?.Image?.Url ?? "https://i.imgur.com/hnNhgFj.png",
                    Padding = config.Panel?.Image?.Padding ?? new TypePadding(0.01f, 0.00f, 0.1f, 0.1f)
                },
                Text = new PanelText
                {
                    Enabled = config.Panel?.Text?.Enabled ?? true,
                    Color = config.Panel?.Text?.Color ?? "#FFFFFFFF",  
                    Order = config.Panel?.Text?.Order ?? 1,
                    Width = config.Panel?.Text?.Width ?? .72f,
                    FontSize = config.Panel?.Text?.FontSize ?? 14,
                    Padding = config.Panel?.Text?.Padding ?? new TypePadding(0.01f, 0.01f, 0.05f, 0.05f),
                    TextAnchor = config.Panel?.Text?.TextAnchor ?? TextAnchor.MiddleCenter,
                    Text = config.Panel?.Text?.Text ?? "{0:0}/{1:0}"
                }
            };
            config.PanelSettings = new PanelRegistration
            {
                BackgroundColor = config.PanelSettings?.BackgroundColor ?? "#FFF2DF08",
                Dock = config.PanelSettings?.Dock ?? "centerupper",
                Order = config.PanelSettings?.Order ?? 12,
                Width = config.PanelSettings?.Width ?? 0.0525f
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
            InvokeHandler.Instance.InvokeRepeating(UpdatePlayerRadiation, 1f, _pluginConfig.UpdateRate);
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            _playerRadiation[player.userID] = GetPlayerData(player);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            _playerRadiation.Remove(player.userID);
        }

        private void Unload()
        {
            InvokeHandler.Instance.CancelInvoke(UpdatePlayerRadiation);
            if (_updateRoutine != null)
            {
                InvokeHandler.Instance.StopCoroutine(_updateRoutine);
            }
        }
        
        private void UnsubscribeAll()
        {
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnPlayerDisconnected));
        }
        #endregion
        
        #region Player Coords Update
        private void UpdatePlayerRadiation()
        {
            _updateRoutine = InvokeHandler.Instance.StartCoroutine(HandleUpdatePlayerRadiation());
        }

        private IEnumerator HandleUpdatePlayerRadiation()
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                PlayerRadiationData data = GetPlayerData(player);
                float prevRad = data.RadAmount;
                float prevProc = data.ProtectionAmount;
                UpdatePlayerRadiation(player, data);
                
                yield return null;
                if (prevRad == data.RadAmount && prevProc == data.ProtectionAmount)
                {
                    continue;
                }
                
                MagicPanel?.Call("UpdatePanel", player, Name, (int)UpdateEnum.Text);
            }
        }
        #endregion

        #region MagicPanel Hook
        private Hash<string, object> GetPanel(BasePlayer player)
        {
            Panel panel = _pluginConfig.Panel;
            PanelText text = panel.Text;
            if (text != null)
            {
                PlayerRadiationData data = GetPlayerData(player);
                float radAmount = data.RadAmount;
                float radProtection = data.ProtectionAmount;

                text.Text = string.Format(_textFormat, radProtection, radAmount);
            }

            return panel.ToHash();
        }
        #endregion

        #region Helper Methods

        private PlayerRadiationData GetPlayerData(BasePlayer player)
        {
            PlayerRadiationData data = _playerRadiation[player.userID];
            if (data == null)
            {
                data = new PlayerRadiationData();
                _playerRadiation[player.userID] = data;
            }

            return data;
        }
        
        
        private void UpdatePlayerRadiation(BasePlayer player, PlayerRadiationData data)
        {
            float radAmount = 0f;
            if (player.triggers != null)
            {
                foreach (TriggerBase trigger in player.triggers)
                {
                    TriggerRadiation radiation = trigger as TriggerRadiation;
                    if (radiation != null)
                    {
                        radAmount = Mathf.Max(radAmount, radiation.GetRadiation(player.transform.position, 0));
                    }
                }
            }

            float radProtection = player.RadiationProtection();

            data.RadAmount = radAmount;
            data.ProtectionAmount = radProtection;
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

        private class PlayerRadiationData
        {
            public float RadAmount { get; set; }
            public float ProtectionAmount { get; set; }
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
