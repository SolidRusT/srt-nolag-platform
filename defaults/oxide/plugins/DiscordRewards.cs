using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord;
using Oxide.Ext.Discord.Attributes;
using Oxide.Ext.Discord.DiscordObjects;
using Oxide.Ext.Discord.Exceptions;

namespace Oxide.Plugins
{
    [Info("Discord Rewards", "birthdates", "1.3.1")]
    [Description("Get rewards for joining a discord!")]
    public class DiscordRewards : CovalencePlugin
    {
        #region Variables
        [DiscordClient] private DiscordClient Client;
        private Role role;
        private const string Perm = "discordrewards.use";
        private Data data;
        #endregion

        #region Hooks
        private void Init()
        {
            LoadConfig();
            data = Interface.Oxide.DataFileSystem.ReadObject<Data>("Discord Rewards");
            permission.RegisterPermission(Perm, this);
            AddCovalenceCommand(_config.command, "ChatCMD");
            if (!_config.wipeData)
            {
                Unsubscribe(nameof(OnNewSave));
            }
        }

        private void OnServerInitialized()
        {
            try
            {
                Discord.CreateClient(this, _config.botKey);
            }
            catch (LimitedClientException)
            {
                PrintError("Too many bots open!");
            }
        }
        
        private void OnNewSave()
        {
            data = new Data();
            SaveData();
            PrintWarning("Wiped all verification data");
        }

        private void Discord_Ready()
        {
            timer.In(0.3f, () =>
            {
                role = GetRoleByName(_config.role);
                if (role == null)
                {
                    PrintError($"ERROR: The \"{_config.role}\" role couldn't be found (try role ID instead)! Expect further errors!");
                    return;
                }
                foreach (var id in data.verified2)
                {
                    foreach (var discordServer in Client.DiscordServers)
                    {
                        var member = discordServer.members.FirstOrDefault(m => m.user.id == id);
                        if (member == null || member.roles.Contains(role.id))
                        {
                            continue;
                        }
                        discordServer.AddGuildMemberRole(Client, member.user, role);
                    }
                }
            });
        }

        private void ChatCMD(IPlayer player)
        {
            if (!permission.UserHasPermission(player.Id, Perm))
            {
                player.Message(lang.GetMessage("NoPermission", this, player.Id));
                return;
            }

            if (data.verified.Contains(player.Id))
            {
                player.Message(lang.GetMessage("AlreadyVerified", this, player.Id));
                return;
            }
            if (data.codes.ContainsValue(player.Id))
            {
                player.Message(string.Format(lang.GetMessage("YouAlreadyHaveACodeOut", this, player.Id), data.codes.First(x => x.Value == player.Id).Key));
                return;
            }
            var code = RandomString(_config.codeLength);
            data.codes.Add(code, player.Id);
            player.Message(string.Format(lang.GetMessage("Verify", this, player.Id), code));
        }
        private readonly System.Random random = new System.Random();

        private string RandomString(int length)
        {
            return new string(Enumerable.Repeat(_config.codeChars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }


        private void DiscordSocket_Initialized()
        {
            if (Client == null)
            {
                PrintError("Discord bot not connected correcty!");
                Discord.CreateClient(this, _config.botKey);
                return;
            }
            PrintWarning("Discord bot connected!");
        }

        private void Discord_MessageCreate(Message message)
        {
            if (message.author.bot == true) return;
            Channel.GetChannel(Client, message.channel_id, c =>
            {
                if (c.type != ChannelType.DM)
                    return;
                if (data.verified2.Contains(message.author.id))
                {
                    message.Reply(Client, lang.GetMessage("AlreadyVerified", this));
                    return;
                }
                if (!data.codes.ContainsKey(message.content))
                {
                    message.Reply(Client, lang.GetMessage("NotAValidCode", this));
                    return;
                }
                var p = players.FindPlayer(data.codes[message.content]);
                data.verified.Add(p.Id);
                data.verified2.Add(message.author.id);
                foreach (var s in _config.commands)
                {
                    server.Command(string.Format(s, p.Id));
                }
                message.Reply(Client, lang.GetMessage("Success", this));
                data.codes.Remove(message.content);
                p.Message(lang.GetMessage("VerifiedInGame", this, p.Id));
                SaveData();
                if(role != null) Client.DiscordServer.AddGuildMemberRole(Client, message.author, role);
            });
            
        }
        
        private Role GetRoleByName(string id)
        {
            return Client?.DiscordServer?.roles?.FirstOrDefault(r => r != null && (string.Equals(r.name, id, StringComparison.CurrentCultureIgnoreCase) || r.id == id));
        }

        private void Unload()
        {
            Discord.CloseClient(Client);
            SaveData();
        }

        #endregion

        #region API

        [HookMethod("IsAuthorized")]
        private bool IsAuthorized(string ID)
        {
            return data.verified.Contains(ID) || 
                   data.verified2.Contains(ID);
        }

        [HookMethod("Deauthorize")]
        private bool Deauthorize(string ID)
        {
            return data.verified.Remove(ID) ||
                data.verified2.Remove(ID);
        }

        #endregion

        #region Configuration & Language
        private ConfigFile _config;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NotAValidCode", "That is not a valid code!"},
                {"Success", "Success you are now verified, check for you rewards in game!"},
                {"AlreadyVerified", "You are already verified."},
                {"NoPermission", "You dont have permission to do this!"},
                {"YouAlreadyHaveACodeOut", "You already have a code out, it is {0}"},
                {"Verify", "Please message the bot on our discord with {0}"},
                {"VerifiedInGame", "Thank you for supporting the server, here are your rewards!"}
            }, this);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Discord Rewards", data);
        }

        private class Data
        {
            public List<string> verified2 = new List<string>();
            public List<string> verified = new List<string>();
            public Dictionary<string, string> codes = new Dictionary<string, string>();
        }

        public class ConfigFile
        {
            [JsonProperty("Command")]
            public string command;
            [JsonProperty("Discord bot key (Look at documentation for how to get this)")]
            public string botKey;
            [JsonProperty("Verification Role (role given when verified)")]
            public string role;
            [JsonProperty("Commands to execute when player is verified (use {0} for the player's steamid)")]
            public List<string> commands;
            [JsonProperty("Amount of characters in the code")]
            public int codeLength;
            [JsonProperty("Erase all verification data on wipe (new map save)?")]
            public bool wipeData;
            [JsonProperty("Characters used in the verification code")]
            public string codeChars;
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    command = "verify",
                    botKey = "INSERT_BOT_KEY_HERE",
                    role = "enter_role_here",
                    commands = new List<string>
                    {
                        "inventory.giveto {0} stones 1000"
                    },
                    codeLength = 6,
                    wipeData = false,
                    codeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz"
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        #endregion
    }
}
//Generated with birthdates' Plugin Maker
