
using System;
using System.Collections.Generic;
using Network;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Mushroom Effects", "supreme", "1.1.0")]
    [Description("Make mushroom eating fun and add effects")]
    public class MushroomEffects : CovalencePlugin
    {
        private Dictionary<string, int> amountUsed = new Dictionary<string, int>();

        private Dictionary<ulong, List<Timer>> playerTimers = new Dictionary<ulong, List<Timer>>();

        private System.Random random = new System.Random();

        const string MushroomEffectsBlur = "Blur";
        const string Layer = "Colors";
        const string shake_prefab = "assets/bundled/prefabs/fx/screen_land.prefab";
        const string shake1_prefab = "assets/bundled/prefabs/fx/takedamage_generic.prefab";
        const string vomit_prefab = "assets/bundled/prefabs/fx/gestures/drink_vomit.prefab";
        const string lick_prefab = "assets/bundled/prefabs/fx/gestures/lick.prefab"; 
        const string breathe_prefab = "assets/prefabs/npc/bear/sound/breathe.prefab";
        
        const string permUse = "mushroomeffects.use";

        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Minimal Amount Of Mushrooms")]
            public int minUsed = 1;
            
            [JsonProperty(PropertyName = "Maximal Amount Of Mushrooms")]
            public int maxUsed = 5;
            
            [JsonProperty(PropertyName = "Opacity of the colors")]
            public double colorOpacity = 0.3;
            
            [JsonProperty(PropertyName = "Repeat interval of the Colors")]
            public float repeatInterval = 0.25f;
            
            [JsonProperty(PropertyName = "Repeat amount of the Colors")]
            public int repeatAmount = 58;

            [JsonProperty(PropertyName = "Vomit")] 
            public bool vomitEnabled = true;
            
            [JsonProperty(PropertyName = "Lick")]
            public bool lickEnabled = true;
            
            [JsonProperty(PropertyName = "Breath")] 
            public bool breathEnabled = true;
            
            [JsonProperty(PropertyName = "Shake")] 
            public bool shakeEnabled = true;
            
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion

        void Init()
        {
            permission.RegisterPermission(permUse, this);
        }

        void Unload()
        {
            DestroyBlur();
        }
        
        void OnItemUse(Item item, int amount)
        {
            if (item.info.shortname != "mushroom") return;

            var player = item.parent?.playerOwner;
            
            List<Timer> timers = new List<Timer>();
            
            playerTimers = new Dictionary<ulong, List<Timer>>();
            
            if (player == null) return;

            if (amountUsed.ContainsKey(player.UserIDString))
                amountUsed[player.UserIDString] += amount;
            else
                amountUsed[player.UserIDString] = amount;

            int rnd = random.Next(_config.minUsed, _config.maxUsed);

            if (player.IPlayer.HasPermission(permUse) && amountUsed[player.UserIDString] >= rnd)
            {
                timer.In(15f, () => CuiHelper.DestroyUi(player, MushroomEffectsBlur));
                timer.In(15f, () => CuiHelper.DestroyUi(player, Layer));
                if (_config.shakeEnabled) timers.Add(timer.Repeat(0.25f, 58, () =>
                {
                    SendEffectTo(shake_prefab, player);
                    SendEffectTo(shake1_prefab, player);
                }));
                
                if (_config.vomitEnabled) timers.Add(timer.Repeat(3f, 4, () => SendEffect(vomit_prefab, player)));

                if (_config.lickEnabled) timers.Add(timer.Repeat(4f, 4, () => SendEffect(lick_prefab, player)));
                
                if (_config.breathEnabled) timers.Add(timer.Repeat(1f, 14, () => SendEffect(breathe_prefab, player)));
                
                InitializeUI(player);
                timer.Repeat(_config.repeatInterval, _config.repeatAmount, () =>InitializeUI2(player));
                amountUsed[player.UserIDString] = 0;
                playerTimers.Add(player.userID, timers);
            }
        }
        private void SendEffectTo(string effect, BasePlayer player)
        {
            if (player == null) return;
            var EffectInstance = new Effect();
            EffectInstance.Init(Effect.Type.Generic, player, 0, Vector3.up, Vector3.zero);
            EffectInstance.pooledstringid = StringPool.Get(effect);
            Network.Net.sv.write.Start();
            Network.Net.sv.write.PacketID(Message.Type.Effect);
            EffectInstance.WriteToStream(Network.Net.sv.write);
            Network.Net.sv.write.Send(new SendInfo(player.net.connection));
            EffectInstance.Clear();
        }
        private void SendEffect(string name, BasePlayer player, Vector3 position = new Vector3(), Vector3 offset = new Vector3())
        {
            if (player == null) return;
            Effect.server.Run(name, player, 0, offset, position);
        }
        void InitializeUI(BasePlayer player)
        {
            if (player == null) return;
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur.mat" },
                }, "Overlay", MushroomEffectsBlur);
            CuiHelper.DestroyUi(player, MushroomEffectsBlur);
            CuiHelper.AddUi(player, container);
        }
        void InitializeUI2(BasePlayer player)
        {
            if (player == null) return;
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Image = { Color = $"{RandomColor()}" }
                }, "Overlay", Layer);
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }
        
        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, MushroomEffectsBlur);
            playerTimers.Remove(player.userID);
        }
        
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, MushroomEffectsBlur);
            playerTimers.Remove(player.userID);
        }
        
        void DestroyBlur()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, MushroomEffectsBlur);
            }
        }

        string RandomColor()
        {
            var random = new System.Random();
            return $"{random.NextDouble()} {random.NextDouble()} {random.NextDouble()} {_config.colorOpacity}";
        }
    }
}