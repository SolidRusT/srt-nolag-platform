using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Stack Multiplier", "Ryz0r", "1.0.3")]
    [Description("Allows you to multiply all items stack size by a multiplier, except those in the blocked config list.")]
    public class StackMultiplier : CovalencePlugin
    {
        private Configuration _config;
        private readonly Dictionary<string, int> _defaultSizes = new Dictionary<string, int>();
        private int _multiplier = 1;
        private const string UsePerm = "stackmultiplier.use";
        
        #region Configuration
        private class Configuration
        {
            [JsonProperty(PropertyName = "BlockedList", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> BlockedList = new List<string>() { "hammer" };
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

        protected override void LoadDefaultConfig()
        {
            PrintWarning("A new configuration file is being generated.");
            _config = new Configuration();
        }
		
		
        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion
        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CurrStack"] = "The current stack size is {0}x.",
                ["ArgOver"] = "You have provided too many arguments. Try: stackmultiplier.multiply 2 for 2x stacks.",
                ["ArgOverReset"] = "You have provided too many arguments.",
                ["NotInt"] =  "That is not a valid integer.",
                ["BlockedItems"] = "Stack size set was blocked for items in config: {0}.",
                ["StackReset"] = "All stacks have been reset.",
                ["NoPerms"] = "You lack the permissions to use this command."
                
            }, this);
        }
        #endregion
        #region Initialization Stuff
        private void Init()
        {
            permission.RegisterPermission(UsePerm, this);
        }

        private void OnServerInitialized()
        {
            foreach (var gameitem in ItemManager.itemList)
            {
                _defaultSizes.Add(gameitem.shortname, gameitem.stackable);
            }
        }

        void Unload()
        {
            ResetStacks();
            
        }
        #endregion
        
        [Command("stackmultiplier.multiply")]
        private void MultiplyCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(UsePerm))
            {
                player.Reply(lang.GetMessage("NoPerms", this, player.Id));
                return;
            }
            
            if (args.Length == 0)
            {
                player.Reply(String.Format(lang.GetMessage("CurrStack", this, player.Id), _multiplier.ToString()));
                return;
            }

            if (args.Length > 2)
            {
                player.Reply(lang.GetMessage("ArgOver", this, player.Id));
                return;
            }

            int localMultiplier;
            if (int.TryParse(args[0], out localMultiplier))
            {
                _multiplier = localMultiplier;
                foreach(var gameitem in ItemManager.itemList)
                {
                    if (!_config.BlockedList.Contains(gameitem.shortname))
                    {
                        ChangeSize(gameitem, _multiplier);
                    }
                }
                
                player.Reply(String.Format(lang.GetMessage("BlockedItems", this, player.Id), String.Join(", ", _config.BlockedList)));
                player.Reply(String.Format(lang.GetMessage("CurrStack", this, player.Id), _multiplier.ToString()));
            }
            else
            {
                player.Reply(lang.GetMessage("NotInt", this, player.Id));
            }
        }
        
        [Command("stackmultiplier.reset")]
        private void ResetCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(UsePerm))
            {
                player.Reply(lang.GetMessage("NoPerms", this, player.Id));
                return;
            }
            
            if (args.Length > 0)
            {
                player.Reply(lang.GetMessage("ArgOverReset", this, player.Id));
                return;
            }

            ResetStacks();
            player.Reply(lang.GetMessage("StackReset", this));
        }

        private void ChangeSize(ItemDefinition gameitem, int multiplier)
        {
            gameitem.stackable = _defaultSizes[gameitem.shortname] * _multiplier;
        }

        private void ResetStacks()
        {
            foreach (var gameitem in ItemManager.itemList)
            {
                gameitem.stackable = _defaultSizes[gameitem.shortname];
            }
            
            _multiplier = 1;
        }
    }
}