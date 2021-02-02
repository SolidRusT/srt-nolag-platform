using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using System.Linq;
using Oxide.Ext.Discord.DiscordObjects;
using Oxide.Ext.Discord;
using Oxide.Ext.Discord.Attributes;

namespace Oxide.Plugins
{
    [Info("Discord Welcomer", "Trey", "1.0.0")]
    [Description("Welcomes players when they join your Discord server.")]
    public class DiscordWelcomer : RustPlugin
    {
        #region Fields

        [DiscordClient]
        private DiscordClient Client;

        #endregion

        #region Data

        Data _Data;
        public class Data
        {
            public List<string> ExistingData = new List<string>();
        }

        private void LoadData() => _Data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _Data);

        #endregion

        #region Configuration

        Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Discord Bot Token")]
            public string BotToken = string.Empty;

            [JsonProperty(PropertyName = "Your Discord ID (For Testing)")]
            public string TestID = string.Empty;

            [JsonProperty(PropertyName = "Discord Embed Title")]
            public string EmbedTitle = "Welcome!";

            [JsonProperty(PropertyName = "Discord Embed Color (No '#')")]
            public string EmbedColor = "66B2FF";

            [JsonProperty(PropertyName = "Discord Embed Author Name (Leave Blank if Unwanted)")]
            public string EmbedAuthorName = "Server Administration";

            [JsonProperty(PropertyName = "Discord Embed Author Icon URL (Leave Blank if Unwanted)")]
            public string EmbedAuthorURL = "https://steamuserimages-a.akamaihd.net/ugc/687094810512264399/04BA8A55B390D1ED0389E561E95775BCF33A9857/";

            [JsonProperty(PropertyName = "Discord Embed Thumbnail Link (Leave Blank if Unwanted)")]
            public string EmbedThumbnailURL = "https://leganerd.com/wp-content/uploads/2014/05/Rust-logo.png";

            [JsonProperty(PropertyName = "Discord Embed Full Image URL (Leave Blank if Unwanted)")]
            public string EmbedFullImageURL = "https://leganerd.com/wp-content/uploads/2014/05/Rust-logo.png";

            [JsonProperty(PropertyName = "Discord Embed Footer Text (Leave Blank if Unwanted)")]
            public string EmbedFooterText = "Thanks for playing with us!";

            [JsonProperty(PropertyName = "Discord Embed Footer Image URL (Leave Blank if Unwanted)")]
            public string EmbedFooterURL = "https://steamuserimages-a.akamaihd.net/ugc/687094810512264399/04BA8A55B390D1ED0389E561E95775BCF33A9857/";

            [JsonProperty(PropertyName = "Config Version")]
            public string ConfigVersion = "1.0.0";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Localization

        public class LangKeys
        {
            public const string Welcome_New = "Welcome_New";
            public const string Welcome_Existing = "Welcome_Existing";
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Welcome_New] = "Welcome to our Discord Server! Please read over our rules and regulations. We truly hope you enjoy your time with us!",
                [LangKeys.Welcome_Existing] = "Welcome back! Please read over our rules and regulations. We hope you stay with us this time!",
            }, this);
        }

        #endregion

        #region Core Methods
        private void OnServerInitialized()
        {
            LoadData();
            CheckConfigVersion(Version);

            if (config.BotToken != string.Empty)
            {
                Discord.CreateClient(this, config.BotToken);
            }
            else
            {
                PrintWarning($"{Name} cannot function while your Discord Bot Token is empty!");
            }
        }

        private void Unload()
        {
            Discord.CloseClient(Client);
            SaveData();
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void Discord_MemberAdded(GuildMember member)
        {
            if (member == null) return;
            if (Client == null) return;

            member.user.CreateDM(Client, dm => 
            {
            
                if (_Data.ExistingData.Contains(member.user.id))
                {
                    dm.CreateMessage(Client, CreateEmbed(Lang(LangKeys.Welcome_Existing, null)));
                }

                else
                {
                    dm.CreateMessage(Client, CreateEmbed(Lang(LangKeys.Welcome_New, null)));
                    _Data.ExistingData.Add(member.user.id);
                }
            
            });
        }
        #endregion

        #region Command
        [ConsoleCommand("testwelcomemessage")]
        private void TestWelcomeCommand(ConsoleSystem.Arg args)
        {
            if (args.Args == null)
            {
                if (config.TestID == string.Empty)
                {
                    PrintError("We couldn't send you a test message because your Test ID is empty in your config.");
                    return;
                }

                GuildMember member = Client.DiscordServer.members.FirstOrDefault(x => x.user.id == config.TestID);

                if (member == null) return;

                Discord_MemberAdded(member);
            }
        }
        #endregion

        #region Helpers

        private Embed CreateEmbed(string message)
        {
            Embed embed = new Embed
            {
                title = config.EmbedTitle,
                color = ConvertColorToDiscordColor(config.EmbedColor),
                author = new Embed.Author
                {
                    name = config.EmbedAuthorName,
                    icon_url = config.EmbedAuthorURL
                },
                thumbnail = new Embed.Thumbnail
                {
                    url = config.EmbedThumbnailURL
                },
                description = message,
                footer = new Embed.Footer
                {
                    text = config.EmbedFooterText,
                    icon_url = config.EmbedFooterURL
                },
                image = new Embed.Image
                {
                    url = config.EmbedFullImageURL
                },
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            };

            return embed;
        }

        private int ConvertColorToDiscordColor(string colorcode) => Convert.ToInt32($"0x{colorcode}", 16);

        string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void CheckConfigVersion(VersionNumber version)
        {
            if (config.ConfigVersion != version.ToString())
            {
                Config.WriteObject(config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonOld");
                PrintError("Your configuration file is out of date, generating up to date one.\nThe old configuration file was saved in the .jsonOld extension");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        #endregion
    }
}

