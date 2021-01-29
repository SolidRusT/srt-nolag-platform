using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Rust;

namespace Oxide.Plugins
{
    [Info("Respawn Protection", "Ryz0r", "2.0.2")]
    [Description("Allows players to have protection when they respawn.")]
    public class RespawnProtection : RustPlugin
    {
        private const string ProtectionPerm = "respawnprotection.use";
        private Dictionary<ulong, DateTime> _protectedPlayers = new Dictionary<ulong, DateTime>();
        private Dictionary<ulong, Timer> timerList = new Dictionary<ulong, Timer>();

        [PluginReference] Plugin NoEscape;
        
        #region Config/Locale
        private Configuration _config;
        protected override void SaveConfig() => Config.WriteObject(_config);
        protected override void LoadDefaultConfig() => _config = new Configuration();

        private class Configuration
        {
            [JsonProperty(PropertyName = "Respawn Protection Timer")]
            public float RespawnProtectionTime = 30f;

            [JsonProperty(PropertyName = "Disable Protection If Raid Blocked")]
            public bool DisableProtectionIfRaidBlock = false;
            
            [JsonProperty(PropertyName = "Protect From PVE")]
            public bool ProtectFromPVE = true;
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
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["RespawnProtected"] = "You are protected from damage for the next {0} seconds.",
                ["NotProtected"] = "Your damage protection has expired.",
                ["CantDamage"] = "This player is protected and you can't damage them for {0} seconds.",
                ["RaidBlocked"] = "You are raid blocked, so your respawn protection has been disabled."
            }, this); 
        }
        
        private void Init()
        {
            permission.RegisterPermission(ProtectionPerm, this);
        }

        private void Unload()
        {
            NoEscape = null;
        }
        #endregion

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (IsRaidBlocked(player))
            {
                player.ChatMessage(lang.GetMessage("RaidBlocked", this, player.UserIDString));
                return;
            }

            player.ChatMessage(string.Format(lang.GetMessage("RespawnProtected", this, player.UserIDString), _config.RespawnProtectionTime));

            Timer checkTimer;
            if (timerList.TryGetValue(player.userID, out checkTimer))
            {
                checkTimer?.Destroy();
                timerList.Remove(player.userID);
            }

            timerList[player.userID] = timer.Once(_config.RespawnProtectionTime, () =>
            {
                if (player == null) return; 
                
                timerList.Remove(player.userID);
               player.ChatMessage(lang.GetMessage("NotProtected", this, player.UserIDString));
            });
        }

        private void OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (info.Initiator is BaseNpc && _config.ProtectFromPVE && _protectedPlayers.ContainsKey(player.userID) && permission.UserHasPermission(player.UserIDString, ProtectionPerm))
            {
                var npc = info.Initiator as BaseNpc;
                if (npc == null) return;
                
                info.damageTypes.ScaleAll(0);
                return;
            }
            
            
            var attacker = info.Initiator as BasePlayer;
            if (player == null || info.Initiator == null || attacker == null) return;
            if (!_protectedPlayers.ContainsKey(player.userID) || player.IsSleeping()) return;

            info.damageTypes.ScaleAll(0);
            
            if (_protectedPlayers.ContainsKey(attacker.userID))
            {
                _protectedPlayers.Remove(attacker.userID);
                attacker.ChatMessage(lang.GetMessage("NotProtected", this, attacker.UserIDString));
                return;
            }

            attacker.ChatMessage(string.Format(lang.GetMessage("CantDamage", this, player.UserIDString), Math.Round(_config.RespawnProtectionTime - (DateTime.Now - _protectedPlayers[player.userID]).TotalSeconds)));
        }
        
        private bool IsRaidBlocked(BasePlayer target) => _config.DisableProtectionIfRaidBlock && NoEscape != null && NoEscape.IsLoaded && (bool)(NoEscape.Call("IsRaidBlocked", target) ?? false);
    }
}