/*
RespawnBradley Copyright (c) 2021 by PinguinNordpol

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Rust;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Respawn Bradley", "PinguinNordpol", "0.3.0")]
    [Description("Adds the possibility to respawn Bradley via command")]
    class RespawnBradley : CovalencePlugin
    {
        [PluginReference]
        private Plugin ServerRewards, Economics, LootDefender;

        #region Fields
        private ConfigData config_data;
        private CooldownController cooldown_controller;
        #endregion

        #region Oxide Hooks
        void Init()
        {
            // Register our permissions
            permission.RegisterPermission("respawnbradley.use", this);
            permission.RegisterPermission("respawnbradley.nolock", this);
            permission.RegisterPermission("respawnbradley.nocosts", this);
            permission.RegisterPermission("respawnbradley.nocooldown", this);
        }

        void Loaded() => lang.RegisterMessages(Messages, this);

        void OnServerInitialized()
        {
            LoadConfig();

            if (this.config_data.Cooldowns.EnableCooldown)
            {
                // Initialize cooldown controller if requested to
                this.cooldown_controller = new CooldownController(this, this.config_data.Cooldowns.CooldownSecs, this.config_data.Cooldowns.CooldownPerPlayer);
            }
        }
        #endregion

        #region Cooldown control
        private class CooldownController
        {
            private DynamicConfigFile cooldown_datafile;
            private Dictionary<string, DateTime> cooldown_data;
            private RespawnBradley parent;
            private double cooldown_secs;
            private bool cooldown_per_player;

            /*
             * CooldownController
             *
             * Constructor
             */
            public CooldownController(RespawnBradley _parent, uint _cooldown_secs, bool _cooldown_per_player)
            {
                // Save settings
                this.parent = _parent;
                this.cooldown_secs = Convert.ToDouble(_cooldown_secs);
                this.cooldown_per_player = _cooldown_per_player;

                // Read and parse cooldown data file
                if (Interface.Oxide.DataFileSystem.ExistsDatafile("RespawnBradleyCooldowns"))
                {
                    this.cooldown_datafile = Interface.Oxide.DataFileSystem.GetFile("RespawnBradleyCooldowns");
                    this.cooldown_data = this.cooldown_datafile.ReadObject<Dictionary<string, DateTime>>();

                    // Do housekeeping on file content
                    if (this.cooldown_data.Count != 0)
                    {
                        this.parent.Puts("Removing expired cooldown data, this might take a moment.");
                        List<string> players_on_cooldown = new List<string>(this.cooldown_data.Keys);
                        foreach(string player_on_cooldown in players_on_cooldown)
                        {
                            if (this.cooldown_data[player_on_cooldown].AddSeconds(this.cooldown_secs) <= DateTime.UtcNow)
                            {
                                this.cooldown_data.Remove(player_on_cooldown);
                            }
                        }
                        this.cooldown_datafile.WriteObject(this.cooldown_data);
                        this.parent.Puts($"Removed {players_on_cooldown.Count-this.cooldown_data.Count} expired of {players_on_cooldown.Count} total cooldown(s).");
                    }
                }
                else
                {
                    // Create new data file
                    this.parent.Puts("Creating new cooldown data file!");
                    this.cooldown_datafile = Interface.Oxide.DataFileSystem.GetFile("RespawnBradleyCooldowns");
                    this.cooldown_data = new Dictionary<string, DateTime>();
                    this.cooldown_datafile.WriteObject(this.cooldown_data);
                }
            }

            /*
             * AddCooldown
             *
             * Add a cooldown for a player
             */
            public void AddCooldown(string player_id)
            {
                if (!this.cooldown_per_player)
                {
                    // In case we have a global cooldown, user player id 0, and hope this will never be a real player id
                    player_id = "0";
                }

                this.cooldown_data[player_id] = DateTime.UtcNow;
                this.cooldown_datafile.WriteObject(this.cooldown_data);
            }

            /*
             * HasCooldown
             *
             * Check if a player currently has a cooldown
             */
            public bool HasCooldown(string player_id) => HasCooldownHelper(player_id, DateTime.UtcNow);
            public bool HasCooldown(string player_id, out uint remaining_secs)
            {
                DateTime now = DateTime.UtcNow;

                if (HasCooldownHelper(player_id, now))
                {
                    // Player has a cooldown, return the actual cooldown time in remaining_secs
                    remaining_secs = Convert.ToUInt32(this.cooldown_secs - (now - this.cooldown_data[player_id]).TotalSeconds);
                    return true;
                }

                remaining_secs = 0;
                return false;
            }

            /*
             * HasCooldownHelper
             *
             * Helper function for HasCooldown functions
             */
            private bool HasCooldownHelper(string player_id, DateTime now)
            {
                if (!this.cooldown_per_player)
                {
                    // In case we have a global cooldown, user player id 0, and hope this will never be a real player id
                    player_id = "0";
                }

                if (!this.cooldown_data.ContainsKey(player_id))
                {
                    // Player currently has no cooldown
                    return false;
                }

                if (this.cooldown_data[player_id].AddSeconds(this.cooldown_secs) <= now)
                {
                    // Cooldown has expired, remove player from our cooldown list
                    this.cooldown_data.Remove(player_id);
                    this.cooldown_datafile.WriteObject(this.cooldown_data);
                    return false;
                }

                return true;
            }
        }
        #endregion

        #region Functions
        /*
         * IsBradleyAlive
         *
         * Check if Bradley is currently alive
         */
        bool IsBradleyAlive()
        {
            BradleySpawner singleton = BradleySpawner.singleton;
            if (singleton != null && (bool)singleton.spawned) return true;

            foreach (HelicopterDebris debris in BaseNetworkable.serverEntities.OfType<HelicopterDebris>())
            {
                string prefab_name = debris?.ShortPrefabName ?? string.Empty;
                if (prefab_name.Contains("bradley"))
                {
                    return true;
                }
            }

            foreach (LockedByEntCrate crate in BaseNetworkable.serverEntities.OfType<LockedByEntCrate>())
            {
                string prefab_name = crate?.ShortPrefabName ?? string.Empty;
                if (prefab_name.Contains("bradley"))
                {
                    return true;
                }
            }

            return false;
        }

        /*
         * DoRespawn
         *
         * Respawn Bradley
         */
        bool DoRespawn(IPlayer player)
        {
            BradleySpawner singleton = BradleySpawner.singleton;

            if (singleton == null)
            {
                Puts("No Bradley spawner found!");
                return false;
            }

            if ((bool)singleton.spawned)
            {
                singleton.spawned.Kill(BaseNetworkable.DestroyMode.None);
            }

            singleton.spawned = null;
            singleton.DoRespawn();

            if (this.config_data.Options.LockBradleyOnRespawn && !player.HasPermission("respawnbradley.nolock"))
            {
                if (LootDefender != null)
                {
                    // Telling LootDefender Bradley took max amount of damage, this should hopefully always lock it whatever Damage Lock Threshold has been configured to
                    HitInfo hit_info = new HitInfo(player.Object as BaseEntity, singleton.spawned as BaseEntity, DamageType.Generic, singleton.spawned.MaxHealth(), new Vector3());
                    LootDefender.Call("OnEntityTakeDamage", singleton.spawned, hit_info);
                }
                else
                {
                    Puts("Unable to lock Bradley without LootDefender plugin!");
                }
            }

            return true;
        }

        /*
         * ChargePlayer
         *
         * Charge RP from a player
         */
        bool ChargePlayer(IPlayer player, bool called_by_player)
        {
            object result = null;
            
            if (called_by_player && player.HasPermission("respawnbradley.nocosts")) return true;
            if (called_by_player && !this.config_data.Options.ChargeOnPlayerCommand) return true;
            if (!called_by_player && !this.config_data.Options.ChargeOnServerCommand) return true;

            if (this.config_data.Options.UseServerRewards && ServerRewards != null)
            {
                result = ServerRewards.Call("TakePoints", Convert.ToUInt64(player.Id), Convert.ToInt32(this.config_data.Options.RespawnCosts));
            }
            else if (this.config_data.Options.UseEconomics && Economics != null)
            {
                result = Economics.Call("Withdraw", player.Id, Convert.ToDouble(this.config_data.Options.RespawnCosts));
            }
            else
            {
                // No supported rewards plugin loaded or configured
                player.Reply(GetMSG("UnableToCharge", player.Id).Replace("{amount}", this.config_data.Options.RespawnCosts.ToString()).Replace("{currency}", this.config_data.Options.CurrencySymbol));
                return false;
            }

            if (result == null || (result is bool && (bool)result == false))
            {
                player.Reply(GetMSG("UnableToCharge", player.Id).Replace("{amount}", this.config_data.Options.RespawnCosts.ToString()).Replace("{currency}", this.config_data.Options.CurrencySymbol));
                return false;
            }

            player.Reply(GetMSG("PlayerCharged", player.Id).Replace("{amount}", this.config_data.Options.RespawnCosts.ToString()).Replace("{currency}", this.config_data.Options.CurrencySymbol));
            return true;
        }

        /*
         * RefundPlayer
         *
         * Refund RP to a player
         */
        void RefundPlayer(IPlayer player, bool called_by_player)
        {
            object result = null;

            if (called_by_player && player.HasPermission("respawnbradley.nocosts")) return;
            if (called_by_player && !this.config_data.Options.RefundOnPlayerCommand) return;
            if (!called_by_player && !this.config_data.Options.RefundOnServerCommand) return;

            if (this.config_data.Options.UseServerRewards && ServerRewards != null)
            {
                result = ServerRewards.Call("AddPoints", Convert.ToUInt64(player.Id), Convert.ToInt32(this.config_data.Options.RespawnCosts));
            }
            else if (this.config_data.Options.UseEconomics && Economics != null)
            {
                result = Economics.Call("Deposit", player.Id, Convert.ToDouble(this.config_data.Options.RespawnCosts));
            }
            else
            {
                // No supported rewards plugin loaded or configured
                player.Reply(GetMSG("UnableToRefund", player.Id).Replace("{amount}", this.config_data.Options.RespawnCosts.ToString()).Replace("{currency}", this.config_data.Options.CurrencySymbol));
                return;
            }

            if (result == null || (result is bool && (bool)result == false))
            {
                player.Reply(GetMSG("UnableToRefund", player.Id).Replace("{amount}", this.config_data.Options.RespawnCosts.ToString()).Replace("{currency}", this.config_data.Options.CurrencySymbol));
                return;
            }

            player.Reply(GetMSG("PlayerRefunded", player.Id).Replace("{amount}", this.config_data.Options.RespawnCosts.ToString()).Replace("{currency}", this.config_data.Options.CurrencySymbol));
        }
        #endregion

        #region Helpers
        /*
         * FindPlayer
         *
         * Find a player based on steam id
         */
        private IPlayer FindPlayer(string player_id)
        {
            return players.FindPlayerById(player_id);
        }

        /*
         * ColorizeText
         *
         * Replace color placeholders in messages
         */
        private string ColorizeText(string msg)
        {
            return msg.Replace("{MsgCol}", this.config_data.Messaging.MsgColor).Replace("{HilCol}", this.config_data.Messaging.HilColor).Replace("{ErrCol}", this.config_data.Messaging.ErrColor).Replace("{ColEnd}","</color>");
        }

        /*
         * FormatSecs
         *
         * Format seconds to human readable hours/minutes/seconds
         */
        private string FormatSecs(IPlayer player, uint secs)
        {
            string result = string.Empty;

            if (secs >= 3600)
            {
                result = $" {secs/3600} {GetMSG("Hours", player.Id)}";
                secs = secs % 3600;
            }
            if (secs >= 60)
            {
                result += $" {secs/60} {GetMSG("Minutes", player.Id)}";
                secs = secs % 60;
            }
            if (secs != 0)
            {
                result += $" {secs} {GetMSG("Seconds", player.Id)}";
            }

            return result;
        }
        #endregion

        #region Commands
        /*
         * cmdRespawnBradley
         *
         * Command to respawn Bradley
         */
        [Command("respawnbradley")]
        private void cmdRespawnBradley(IPlayer player, string command, string[] args)
        {
            IPlayer target_player = null;
            bool called_by_player = false;

            if (!player.IsServer && !player.HasPermission("respawnbradley.use"))
            {
                player.Reply(GetMSG("NoPermission", player.Id));
                return;
            }
            else if (!player.IsServer)
            {
                // Player has called command directly
                target_player = player;
                called_by_player = true;
            }
            else
            {
                // Command is called via a store, find target player
                if (args.Length != 1) {
                    // Called via shop, but not given target player id
                    Puts("Erronous invocation of respawnbradley command! Usage: respawnbradley <playerId>");
                    return;
                }

                target_player = this.FindPlayer(args[0]);
                if (target_player == null)
                {
                    // Called via shop, but no valid player id given
                    Puts($"Erronous invocation of respawnbradley command! Unknown player id '{args[0]}'");
                    return;
                }
            }

            // Check for cooldown
            if (this.config_data.Cooldowns.EnableCooldown && !target_player.HasPermission("respawnbradley.nocooldown"))
            {
                uint remaining_secs;
                if (this.cooldown_controller.HasCooldown(target_player.Id, out remaining_secs))
                {
                    // Command is still on cooldown
                    target_player.Reply(GetMSG("PlayerOnCooldown", player.Id).Replace("{time}", this.FormatSecs(target_player, remaining_secs)));
                    return;
                }
            }

            // Make sure Bradley is not already alive
            if(this.IsBradleyAlive())
            {
                if (!called_by_player)
                {
                    // If called via shop, player has already been charged, so need to refund here
                    this.RefundPlayer(target_player, called_by_player);
                }
                target_player.Reply(GetMSG("UnableToRespawnBradley", player.Id));
                return;
            }

            // Charge player for respawn
            if (!this.ChargePlayer(target_player, called_by_player))
            {
                return;
            }

            // Respawn Bradley
            if(!this.DoRespawn(target_player))
            {
                this.RefundPlayer(target_player, called_by_player);
                target_player.Reply(GetMSG("UnableToRespawnBradley", player.Id));
                return;
            }

            // Set cooldown
            if (this.config_data.Cooldowns.EnableCooldown && !target_player.HasPermission("respawnbradley.nocooldown"))
            {
                this.cooldown_controller.AddCooldown(target_player.Id);
            }

            target_player.Reply(GetMSG("BradleyHasBeenRespawned", player.Id));
        }
        #endregion

        #region Config
        class Messaging
        {
            public string MsgColor { get; set; }
            public string HilColor { get; set; }
            public string ErrColor { get; set; }
        }        
        class Options
        {
            public bool LockBradleyOnRespawn { get; set; }
            public bool UseServerRewards { get; set; }
            public bool UseEconomics { get; set; }
            public bool ChargeOnServerCommand { get; set; }
            public bool ChargeOnPlayerCommand { get; set; }
            public bool RefundOnServerCommand { get; set; }
            public bool RefundOnPlayerCommand { get; set; }
            public int RespawnCosts { get; set; }
            public string CurrencySymbol { get; set; }
        }
        class Cooldowns
        {
            public bool EnableCooldown { get; set; }
            public bool CooldownPerPlayer { get; set; }
            public uint CooldownSecs { get; set; }
        }
        class PluginVersion
        {
            public string CurrentVersion { get; set; }
        }
        class ConfigData
        {
            public Messaging Messaging { get; set; }
            public Options Options { get; set; }
            public Cooldowns Cooldowns { get; set; }
            public Oxide.Core.VersionNumber Version { get; set; }

        }
        private void LoadConfig()
        {
            ConfigData configdata = Config.ReadObject<ConfigData>();

            if (configdata.Version < Version)
            {
                this.config_data = this.UpdateConfig(configdata);
            }
            else
            {
                this.config_data = configdata;
            }
        }
        private ConfigData CreateNewConfig()
        {
            return new ConfigData
            {
                Messaging = new Messaging
                {
                    MsgColor = "<color=#939393>",
                    HilColor = "<color=orange>",
                    ErrColor = "<color=red>"
                },
                Options = new Options
                {
                    LockBradleyOnRespawn = false,
                    UseServerRewards = true,
                    UseEconomics = false,
                    ChargeOnServerCommand = false,
                    ChargeOnPlayerCommand = false,
                    RefundOnServerCommand = true,
                    RefundOnPlayerCommand = false,
                    RespawnCosts = 10000,
                    CurrencySymbol = "RP"
                },
                Cooldowns = new Cooldowns
                {
                    EnableCooldown = false,
                    CooldownPerPlayer = true,
                    CooldownSecs = 1200
                },
                Version = Version
            };
        }
        protected override void LoadDefaultConfig() => SaveConfig(this.CreateNewConfig());
        private ConfigData UpdateConfig(ConfigData old_config)
        {
            ConfigData new_config;
            bool config_changed = false;

            if (old_config.Version < new VersionNumber(0, 3, 0))
            {
                new_config = this.CreateNewConfig();
                new_config.Messaging.MsgColor = old_config.Messaging.MsgColor;
                new_config.Messaging.HilColor = old_config.Messaging.HilColor;
                new_config.Messaging.ErrColor = old_config.Messaging.ErrColor;
                new_config.Options.UseServerRewards = old_config.Options.UseServerRewards;
                new_config.Options.ChargeOnServerCommand = old_config.Options.ChargeOnServerCommand;
                new_config.Options.ChargeOnPlayerCommand = old_config.Options.ChargeOnPlayerCommand;
                new_config.Options.RefundOnServerCommand = old_config.Options.RefundOnServerCommand;
                new_config.Options.RefundOnPlayerCommand = old_config.Options.RefundOnPlayerCommand;
                new_config.Options.RespawnCosts = old_config.Options.RespawnCosts;
                new_config.Options.CurrencySymbol = old_config.Options.CurrencySymbol;
/*
                if (old_config.Version >= new VersionNumber(0, 3, 0))
                {
                    new_config.Cooldowns.EnableCooldown = old_config.Cooldowns.EnableCooldown;
                    new_config.Cooldowns.CooldownPerPlayer = old_config.Cooldowns.CooldownPerPlayer;
                    new_config.Cooldowns.CooldownSecs = old_config.Cooldowns.CooldownSecs;
                }
*/
                if (old_config.Version >= new VersionNumber(0, 2, 0))
                {
                    new_config.Options.LockBradleyOnRespawn = old_config.Options.LockBradleyOnRespawn;
                }
                config_changed = true;
            }
            else
            {
                new_config = old_config;
                new_config.Version = Version;
            }

            this.SaveConfig(new_config);
            if (config_changed) Puts("Configuration of RespawnBradley was updated. Please check configuration file for changes!");

            return new_config;
        }
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Messaging
        private string GetMSG(string key, string userid = null) => ColorizeText(lang.GetMessage(key, this, userid));
        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"NoPermission", "{ErrCol}You do not have permission to use this command!{ColEnd}"},
            {"UnableToCharge", "{ErrCol}We were unable to charge you {amount} {currency}! Please contact an admin{ColEnd}" },
            {"PlayerCharged", "{MsgCol}You have been charged {ColEnd}{HilCol}{amount} {currency}{ColEnd} {MsgCol}for respawning Bradley{ColEnd}" },
            {"UnableToRefund", "{ErrCol}Unable to refund you {amount} {currency}! Please contact an admin{ColEnd}" },
            {"PlayerRefunded", "{HilCol}You have been refunded {amount} {currency}{ColEnd}" },
            {"UnableToRespawnBradley", "{MsgCol}Unable to respawn Bradley as it's still alive or not all of its debris has been cleared{ColEnd}" },
            {"BradleyHasBeenRespawned", "{HilCol}Bradley has been respawned{ColEnd}" },
            {"Hours", "hour(s)" },
            {"Minutes", "minute(s)" },
            {"Seconds", "second(s)" },
            {"PlayerOnCooldown", "{MsgCol}This command is on cooldown for{time}{ColEnd}" }
        };
        #endregion
    }
}
