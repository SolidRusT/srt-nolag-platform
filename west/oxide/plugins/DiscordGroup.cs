using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord.DiscordObjects;

namespace Oxide.Plugins
{
    [Info("Discord Group", "MJSU", "1.0.4")]
    [Description("Adds players to the discord role after linking their rust and discord accounts")]
    internal class DiscordGroup : CovalencePlugin
    {
        #region Class Fields
        [PluginReference] private Plugin DiscordCore;

        private PluginConfig _pluginConfig;
        private Role _role;
        #endregion

        #region Setup & Loading
        private void OnServerInitialized()
        {
            if (DiscordCore == null)
            {
                PrintError("Missing plugin dependency DiscordCore: https://umod.org/plugins/discord-core. Unloading plugin");
                return;
            }
        
            OnDiscordCoreReady();
        }

        private void OnDiscordCoreReady()
        {
            if (!(DiscordCore?.Call<bool>("IsReady") ?? false))
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pluginConfig.DiscordRole))
            {
                _role = DiscordCore.Call<Role>("GetRole", _pluginConfig.DiscordRole);
                if (_role == null)
                {
                    PrintWarning($"Discord Role '{_pluginConfig.DiscordRole}' does not exist. Please set the role name or id in the config.");
                    return;
                }
            }

            if (!string.IsNullOrEmpty(_pluginConfig.OxideGroup) && !permission.GroupExists(_pluginConfig.OxideGroup))
            {
                PrintWarning($"Oxide group '{_pluginConfig.OxideGroup}' does not exist. Please add the oxide group or set the correct group in the config.");
                return;
            }

            int index = 0;
            foreach (string userId in DiscordCore.Call<List<string>>("GetAllUsers"))
            {
                timer.In(index, () =>
                {
                    IPlayer player = covalence.Players.FindPlayerById(userId);
                    AddToGroup(player);
                });
            }
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
            return config;
        }
        #endregion

        #region Discord Core Hooks

        private void OnDiscordCoreJoin(IPlayer player)
        {
            AddToGroup(player);
        }

        private void OnDiscordCoreLeave(IPlayer player)
        {
            RemoveFromGroup(player);
        }
        #endregion

        #region Helpers

        private void AddToGroup(IPlayer player)
        {
            if (!string.IsNullOrEmpty(_pluginConfig.OxideGroup))
            {
                AddToOxideGroup(player);
            }

            if (!string.IsNullOrEmpty(_pluginConfig.DiscordRole))
            {
                AddToDiscordGroup(player);
            }
        }

        private void AddToOxideGroup(IPlayer player)
        {
            permission.AddUserGroup(player.Id, _pluginConfig.OxideGroup);
        }

        private void AddToDiscordGroup(IPlayer player)
        {
            GuildMember member = DiscordCore.Call("GetGuildMember", player.Id) as GuildMember;
            if (member == null)
            {
                return;
            }

            object hasRole = DiscordCore.Call("UserHasRole", member.user.id, _role.id);
            if (hasRole is bool && (bool)hasRole)
            {
                return;
            }

            DiscordCore.Call("AddRoleToUser", member.user.id, _role.id);
        }

        private void RemoveFromGroup(IPlayer player)
        {
            if (!string.IsNullOrEmpty(_pluginConfig.OxideGroup))
            {
                RemoveFromOxide(player);
            }

            if (!string.IsNullOrEmpty(_pluginConfig.DiscordRole))
            {
                RemoveFromDiscord(player);
            }
        }

        private void RemoveFromOxide(IPlayer player)
        {
            permission.RemoveUserGroup(player.Id, _pluginConfig.OxideGroup);
        }

        private void RemoveFromDiscord(IPlayer player)
        {
            GuildMember member = DiscordCore.Call("GetGuildMember", player.Id) as GuildMember;
            if (member == null)
            {
                return;
            }

            object hasRole = DiscordCore.Call("UserHasRole", member.user.id, _role.id);
            if (hasRole is bool && !(bool)hasRole)
            {
                return;
            }

            DiscordCore.Call("RemoveRoleFromUser", member.user.id, _role.id);
        }
        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue("")]
            [JsonProperty("Discord role name or id")]
            public string DiscordRole { get; set; }
            
            [DefaultValue("")]
            [JsonProperty("Oxide group name")]
            public string OxideGroup { get; set; }
        }
        #endregion

    }
}
