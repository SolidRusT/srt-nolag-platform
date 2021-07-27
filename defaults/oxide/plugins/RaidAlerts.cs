using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Raid Alerts", "Ryz0r", "1.0.1")]
    [Description("Allows players with permissions to receive alerts when explosives are thrown or fired.")]
    public class RaidAlerts : CovalencePlugin
    {
        private const string RaidAlertCommands = "raidalerts.use";
        private readonly Dictionary<string, float> _AlertedUsers = new Dictionary<string, float>();
        
        private static List<string> _EnabledPlayersList = new List<string>();
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _EnabledPlayersList);
        #region Configuration
        private Configuration config;
        private class Configuration
        {
            [JsonProperty(PropertyName = "UseWebhook")]
            public bool UseWebhook = false;

            [JsonProperty(PropertyName = "WebhookURL")]
            public string WebhookUrl = "";

            [JsonProperty(PropertyName = "OutputCooldown")]
            public float OutputCooldown = 5f;
        }
		
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<RaidAlerts.Configuration>();
                if (config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }
		
        protected override void LoadDefaultConfig()
        {
            PrintWarning("A new configuration file is being generated.");
            config = new RaidAlerts.Configuration();
        }
		
		
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion
        
         protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["IncorrectArgs"] = "You are using the command incorrectly. Try /alerts on or /alerts off.",
                ["AlreadyReceiving"] = "You are already receiving alerts. Try /alerts off.",
                ["NotReceiving"] = "You are not receiving alerts. Try /alerts on.",
                ["NowReceiving"] = "You will now receive alerts when a raid is happening.",
                ["NoLongerReceiving"] = "You will no longer receive alerts when a raid is happening.",
                ["ThrownAlert"] = "{0} has thrown a {1} at the location {2}.",
                ["FiredAlert"] = "{0} has fired a Rocket/HE Grenade at the location {1}.",
                ["GenRaidAlert"] = "{0} is using explosive ammo or fire ammo at the location {1}.",
                ["NoPerm"] = "You don't have the permissions to use this command."
            }, this);
        }
        
        private void OnNewSave(string filename)
        {
            if(Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
            {
                Interface.Oxide.DataFileSystem.GetFile(Name).Clear();
                Interface.Oxide.DataFileSystem.GetFile(Name).Save();

                Puts($"Wiped '{Name}.json'");
            }
        }

        private void Init()
        {
            permission.RegisterPermission(RaidAlertCommands, this);
        }

        [Command("alerts")]
        private void AlertsCommand(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(RaidAlertCommands))
            {
                if (args.Length == 0 || args.Length > 1)
                {

                    player.Reply(lang.GetMessage("IncorrectArgs", this, player.Id));
                    return;
                }

                switch (args[0])
                {
                    case "on":
                        if (_EnabledPlayersList.Contains(player.Id))
                        {
                            player.Reply(lang.GetMessage("AlreadyReceiving", this, player.Id));
                            return;
                        }

                        _EnabledPlayersList.Add(player.Id);
                        player.Reply(lang.GetMessage("NowReceiving", this, player.Id));
                        SaveData();
                        break;
                    case "off":
                        if (!_EnabledPlayersList.Contains(player.Id))
                        {
                            player.Reply(lang.GetMessage("NotReceiving", this, player.Id));
                            return;
                        }

                        _EnabledPlayersList.Remove(player.Id);
                        player.Reply(lang.GetMessage("NoLongerReceiving", this, player.Id));
                        SaveData();
                        break;
                }
            }
            else
            {
                player.Reply(lang.GetMessage("NoPerm", this, player.Id));
            }
        }
        
        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item)
        {
            var playerThrow = player.displayName;
            var entityLocation = entity.transform.position;
            var explosiveUsed = item.ShortPrefabName;
            
            switch (explosiveUsed)
            {
                case "explosive.timed.entity":
                    explosiveUsed = "C4";
                    break;
                case "explosive.satchel.entity":
                    explosiveUsed = "Satchel";
                    break;
                case "grenade.beancan.entity":
                    explosiveUsed = "Beancan";
                    break;
                case "grenade.f1.entity":
                    explosiveUsed = "Grenade";
                    break;
                case "survey_charge":
                    explosiveUsed = "Survey Charge";
                    break;
            }
            
            foreach(var user in BasePlayer.activePlayerList)
            {
                if (_EnabledPlayersList.Contains(user.UserIDString))
                {
                    user.ChatMessage(string.Format(lang.GetMessage("ThrownAlert", this, player.UserIDString), playerThrow, explosiveUsed, entityLocation));
                }
            }
            
            if (config.UseWebhook && config.WebhookUrl != null)
            {
                SendDiscordMessage(playerThrow, entityLocation, explosiveUsed);
            }
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if(info.damageTypes.Has(DamageType.Explosion) && info.Initiator != null && !_AlertedUsers.ContainsValue(entity.transform.position.x))
            {
                var attackerName = (info.Initiator as BasePlayer).displayName;
                
                timer.Once(config.OutputCooldown, () =>
                {
                    _AlertedUsers.Remove(attackerName);
                });
                
                _AlertedUsers.Add(attackerName, entity.transform.position.x);
                foreach (var user in BasePlayer.activePlayerList)
                {
                    if (_EnabledPlayersList.Contains(user.UserIDString))
                    {
                        user.ChatMessage(string.Format(lang.GetMessage("GenRaidAlert", this, user.UserIDString), attackerName, entity.transform.position));
                        return true;
                    }
                }
                
                if (config.UseWebhook && config.WebhookUrl != null)
                { 
                    SendDiscordMessage(attackerName, entity.transform.position, "Explo/Fire");
                }
            }
            return null;
        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            var playerThrow = player.displayName;
            var entityLocation = entity.transform.position;
            
            foreach(var user in BasePlayer.activePlayerList)
            {
                if (_EnabledPlayersList.Contains(user.UserIDString))
                {
                    user.ChatMessage(string.Format(lang.GetMessage("FiredAlert", this, player.UserIDString), playerThrow, entityLocation));
                }
            }

            if (config.UseWebhook && config.WebhookUrl != null)
            { 
                SendDiscordMessage(playerThrow, entityLocation, "Rocket/HE Grenade");
            }
        }
        
        private void SendDiscordMessage(string playerName, Vector3 entityLocation, string explosive)
        {

            var embed = new Embed()
                .AddField("Player Name:", playerName, true)
                .AddField("Explosive Used:", explosive, false)
                .AddField("Rocket Location:", entityLocation.ToString(), false);

            webrequest.Enqueue(config.WebhookUrl, new DiscordMessage("", embed).ToJson(), (code, response) => {
            }, this, RequestMethod.POST, new Dictionary<string, string>() {
                { "Content-Type", "application/json" }
            });
        }
        
        #region Discord Stuff
        private class DiscordMessage
        {
            public DiscordMessage(string content, params Embed[] embeds)
            {
                Content = content;
                Embeds  = embeds.ToList();
            }

            [JsonProperty("content")] public string Content { get; set; }
            [JsonProperty("embeds")] public List<Embed> Embeds { get; set; }

            public string ToJson() => JsonConvert.SerializeObject(this);
        }

        private class Embed
        {
            [JsonProperty("fields")] public List<Field> Fields { get; set; } = new List<Field>();

            public Embed AddField(string name, string value, bool inline)
            {
                Fields.Add(new Field(name, Regex.Replace(value, "<.*?>", string.Empty), inline));

                return this;
            }
        }

        private class Field
        {
            public Field(string name, string value, bool inline)
            {
                Name   = name;
                Value  = value;
                Inline = inline;
            }

            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("value")] public string Value { get; set; }
            [JsonProperty("inline")] public bool Inline { get; set; }
        }
        #endregion
    }
}