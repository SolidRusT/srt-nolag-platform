using System.Collections.Generic;

using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Free Build", "0x89A", "2.0.1")]
    [Description("Allows building, upgrading and placing deployables for free")]
    class FreeBuild : CovalencePlugin
    {
        private const string usePerm = "freebuild.allow";

        private HashSet<BasePlayer> activePlayers = new HashSet<BasePlayer>();

        bool freeDeployables = false;
        bool requireChat = false;
        string chatCommand = string.Empty;

        void Init()
        {
            permission.RegisterPermission(usePerm, this);

            if (requireChat) AddCovalenceCommand(chatCommand, nameof(Command), usePerm);
        }

        object CanAffordToPlace(BasePlayer player, Planner planner, Construction construction)
        {
            if (IsAllowed(player)) return true;
            else return null;
        }

        object OnPayForPlacement(BasePlayer player, Planner planner, Construction construction)
        {
            if (IsAllowed(player) && DeployableCheck(construction.deployable)) return true;
            else return null;
        }

        object OnPayForUpgrade(BasePlayer player, BuildingBlock block, ConstructionGrade gradeTarget)
        {
            if (IsAllowed(player)) return true;
            else return null;
        }

        private void Command(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (activePlayers.Contains(player))
            {
                activePlayers.Remove(player);
                player.ChatMessage(lang.GetMessage("Disabled", this, player.UserIDString));
            }
            else
            {
                activePlayers.Add(player);
                player.ChatMessage(lang.GetMessage("Enabled", this, player.UserIDString));
            }
        }

        private bool IsAllowed(BasePlayer player)
        {
            return requireChat ? activePlayers.Contains(player) : permission.UserHasPermission(player.UserIDString, usePerm);
        }

        private bool DeployableCheck(Deployable deployable)
        {
            return !(deployable != null && !freeDeployables && deployable.fullName.Contains("deployed"));
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Enabled"] = "Free build <color=green>enabled</color>",
                ["Disabled"] = "Free build <color=red>disabled</color>",
            }, this);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                if (!(Config["Require Chat Command"] is bool) || !(Config["Chat Command"] is string) || !(Config["Deployables Are Free"] is bool)) 
                    throw new System.Exception();

                requireChat = (bool)Config["Require Chat Command"];
                freeDeployables = (bool)Config["Deployables Are Free"];
                chatCommand = (string)Config["Chat Command"];

                Config.Save();
            }
            catch
            {
                PrintWarning("Configuration is corrupt, generating new configuration");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config["Chat Command"] = "freebuild";
            Config["Require Chat Command"] = true;
            Config["Deployables Are Free"] = true;
            Config.Save();
        }
    }
}
