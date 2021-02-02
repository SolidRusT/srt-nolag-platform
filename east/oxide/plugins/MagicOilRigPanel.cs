using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Apex;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Magic Oil Rig Panel", "MJSU", "1.0.1")]
    [Description("Displays if the small of large oil rig crate is present")]
    public class MagicOilRigPanel : RustPlugin
    {
        #region Class Fields

        [PluginReference] private readonly Plugin MagicPanel;

        private PluginConfig _pluginConfig; //Plugin Config

        private string _largeName;
        private readonly List<HackableLockedCrate> _activeLargeOil = new List<HackableLockedCrate>();
        private Vector3 _largePos;
        
        private string _smallName;
        private readonly List<HackableLockedCrate> _activeSmallOil = new List<HackableLockedCrate>();
        private Vector3 _smallPos;

        private bool _init;

        private enum UpdateEnum { All = 1, Panel = 2, Image = 3, Text = 4 }
        #endregion

        #region Setup & Loading
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
            config.SmallOilRig = new PanelSetup
            {
                InactiveColor = config.SmallOilRig?.InactiveColor ?? "#FFFFFF1A",
                ActiveColor = config.SmallOilRig?.ActiveColor ?? "#FF4E49FF",
                HackingColor = config.SmallOilRig?.HackingColor ?? "#E5E500FF",
                FullyHackedColor = config.SmallOilRig?.FullyHackedColor ?? "#4EE44EFF",
                Panel = new Panel
                {
                    Image = new PanelImage
                    {
                        Enabled = config.SmallOilRig?.Panel?.Image?.Enabled ?? true,
                        Color = config.SmallOilRig?.Panel?.Image?.Color ?? "#FFFFFFFF",
                        Order = config.SmallOilRig?.Panel?.Image?.Order ?? 0,
                        Width = config.SmallOilRig?.Panel?.Image?.Width ?? 1f,
                        Url = config.SmallOilRig?.Panel?.Image?.Url ?? "https://i.imgur.com/xnT0SjR.png",
                        Padding = config.SmallOilRig?.Panel?.Image?.Padding ?? new TypePadding(0.1f, 0.1f, 0.1f, 0.1f)
                    }
                },
                PanelSettings = new PanelRegistration
                {
                    BackgroundColor = config.SmallOilRig?.PanelSettings?.BackgroundColor ?? "#FFF2DF08",
                    Dock = config.SmallOilRig?.PanelSettings?.Dock ?? "center",
                    Order = config.SmallOilRig?.PanelSettings?.Order ?? 12,
                    Width = config.SmallOilRig?.PanelSettings?.Width ?? 0.02f
                }
            };
            
            config.LargeOilRig = new PanelSetup
            {
                InactiveColor = config.LargeOilRig?.InactiveColor ?? "#FFFFFF1A",
                ActiveColor = config.LargeOilRig?.ActiveColor ?? "#FF4E49FF",
                HackingColor = config.LargeOilRig?.HackingColor ?? "#E5E500FF",
                FullyHackedColor = config.LargeOilRig?.FullyHackedColor ?? "#4EE44EFF",
                Panel = new Panel
                {
                    Image = new PanelImage
                    {
                        Enabled = config.LargeOilRig?.Panel?.Image?.Enabled ?? true,
                        Color = config.LargeOilRig?.Panel?.Image?.Color ?? "#FFFFFFFF",
                        Order = config.LargeOilRig?.Panel?.Image?.Order ?? 0,
                        Width = config.LargeOilRig?.Panel?.Image?.Width ?? 1f,
                        Url = config.LargeOilRig?.Panel?.Image?.Url ?? "https://i.imgur.com/xnT0SjR.png",
                        Padding = config.LargeOilRig?.Panel?.Image?.Padding ?? new TypePadding(0.05f, 0.05f, 0.05f, 0.05f)
                    }
                },
                PanelSettings = new PanelRegistration
                {
                    BackgroundColor = config.LargeOilRig?.PanelSettings?.BackgroundColor ?? "#FFF2DF08",
                    Dock = config.LargeOilRig?.PanelSettings?.Dock ?? "center",
                    Order = config.LargeOilRig?.PanelSettings?.Order ?? 13,
                    Width = config.LargeOilRig?.PanelSettings?.Width ?? 0.02f
                }
            };
            
            return config;
        }

        private void OnServerInitialized()
        {
            _init = true;
            _smallName = $"{Name}_Small";
            _largeName = $"{Name}_Large";
            
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                if (monument.name == "OilrigAI2")
                {
                    _largePos = monument.transform.position;
                }
                else if (monument.name == "OilrigAI")
                {
                    _smallPos = monument.transform.position;
                }
            }

            NextTick(() =>
            {
                foreach (HackableLockedCrate crate in UnityEngine.Object.FindObjectsOfType<HackableLockedCrate>())
                {
                    if (IsCrateAt(crate, _largePos))
                    {
                        _activeLargeOil.Add(crate);
                    }
                    else if (IsCrateAt(crate, _smallPos))
                    {
                        _activeSmallOil.Add(crate);
                    }
                }

                MagicPanelRegisterPanels();
            });
        }

        private void MagicPanelRegisterPanels()
        {
            if (MagicPanel == null)
            {
                PrintError("Missing plugin dependency MagicPanel: https://umod.org/plugins/magic-panel");
                UnsubscribeAll();
                return;
            }

            MagicPanel?.Call("RegisterGlobalPanel", this, _smallName, JsonConvert.SerializeObject(_pluginConfig.SmallOilRig.PanelSettings), nameof(GetPanel));
            MagicPanel?.Call("RegisterGlobalPanel", this, _largeName, JsonConvert.SerializeObject(_pluginConfig.LargeOilRig.PanelSettings), nameof(GetPanel));
        }

        private void CheckSmallEvent(bool force = false)
        {
            if (force || _activeSmallOil.Count == 0 || _activeSmallOil.Count == 1)
            {
                MagicPanel?.Call("UpdatePanel", _smallName, (int)UpdateEnum.Image);
            }
        }
        
        private void CheckLargeEvent(bool force = false)
        {
            if (force || _activeLargeOil.Count == 0 || _activeLargeOil.Count == 1)
            {
                MagicPanel?.Call("UpdatePanel", _largeName, (int)UpdateEnum.Image);
            }
        }
        
        private void UnsubscribeAll()
        {
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityKill));
        }
        #endregion

        #region uMod Hooks

        private void OnEntitySpawned(HackableLockedCrate crate)
        {
            if (!_init)
            {
                return;
            }
            
            NextTick(() =>
            {
                if (IsCrateAt(crate, _smallPos))
                {
                    _activeSmallOil.Add(crate);
                    CheckSmallEvent();
                }
                else if(IsCrateAt(crate, _largePos))
                {
                    _activeLargeOil.Add(crate);
                    CheckLargeEvent();
                }
            });
        }

        private void OnCrateHack(HackableLockedCrate crate)
        {
           NextTick(() =>
           {
               if (IsCrateAt(crate, _smallPos))
               {
                   CheckSmallEvent(true);
               }
               else if (IsCrateAt(crate, _largePos))
               {
                   CheckLargeEvent(true);
               }
           });
        }
        
        private void OnCrateHackEnd(HackableLockedCrate crate)
        {
           NextTick(() =>
           {
               if (IsCrateAt(crate, _smallPos))
               {
                   CheckSmallEvent(true);
               }
               else if (IsCrateAt(crate, _largePos))
               {
                   CheckLargeEvent(true);
               }
           });
        }
        
        private void OnEntityKill(HackableLockedCrate crate)
        {
            if (_activeSmallOil.Remove(crate))
            {
                CheckSmallEvent();
            }
            else if (_activeLargeOil.Remove(crate))
            {
                CheckLargeEvent();
            }
        }
        #endregion

        #region MagicPanel Hook
        private Hash<string, object> GetPanel(string name)
        {
            Panel panel;
            string color;
            if (name == _smallName)
            {
                panel = _pluginConfig.SmallOilRig.Panel;
                color = GetPanelColor(_activeSmallOil, _pluginConfig.SmallOilRig);
            }
            else if (name == _largeName)
            {
                panel = _pluginConfig.LargeOilRig.Panel;
                color = GetPanelColor(_activeLargeOil, _pluginConfig.LargeOilRig);
            }
            else
            {
                return null;
            }
            
            PanelImage image = panel.Image;
            if (image != null)
            {
                image.Color = color;
            }

            return panel.ToHash();
        }

        private string GetPanelColor(List<HackableLockedCrate> crates, PanelSetup setup)
        {
            if (crates.Count == 0)
            {
                return setup.InactiveColor;
            }

            if (crates.Any(h => h.IsFullyHacked()))
            {
                return setup.FullyHackedColor;
            }

            if (crates.Any(h => h.IsBeingHacked()))
            {
                return setup.HackingColor;
            }
            
            return setup.ActiveColor;
        }
        #endregion
        
        #region Helper Methods
        private bool IsCrateAt(HackableLockedCrate crate, Vector3 pos)
        {
            return Vector3.Distance(crate.transform.position, pos) <= 50;
        }
        #endregion

        #region Classes

        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Small Oil Rig")]
            public PanelSetup SmallOilRig { get; set; }
            
            [JsonProperty(PropertyName = "Large Oil Rig")]
            public PanelSetup LargeOilRig { get; set; }
        }

        private class PanelSetup
        {
            [JsonProperty(PropertyName = "No Crate Color")]
            public string InactiveColor { get; set; }
            
            [JsonProperty(PropertyName = "Untouched Crate Color")]
            public string ActiveColor { get; set; }
            
            [JsonProperty(PropertyName = "Hacking Crate Color")]
            public string HackingColor { get; set; }
            
            [JsonProperty(PropertyName = "Fully Hacked Crate Color")]
            public string FullyHackedColor { get; set; }

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
            
            public Hash<string, object> ToHash()
            {
                return new Hash<string, object>
                {
                    [nameof(Image)] = Image.ToHash(),
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
