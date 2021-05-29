using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Team Info", "Lorddy", "0.1.5")]
    [Description("Get Team Info")]
    public class TeamInfo : RustPlugin
    {
        #region Fields
        private const string PERMISSION_USE = "teaminfo.use";
        #endregion
        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CmdHelp1"] = "<color=orange>/teaminfo <name|ID></color> - Show team info",
                ["PlayerNotFound"] = "Player Not Found",
                ["TeamNotFound"] = "Player {0} has no team",
                ["NoPerm"] = "You don't have permission to use this command",
                ["Result1"] = "<color=orange>TeamID:</color> {0}",
                ["Result2"] = "\n<color=orange>Leader:</color> {0}",
                ["Result3"] = "\n<color=orange>Name:</color> {0}",
                ["Result4"] = "\n<color=orange>Start Time:</color> {0}",
                ["Result5"] = "\n<color=orange>Life Time:</color> {0}",
                ["Result6"] = "\n<color=orange>Teammates:</color> {0}",
            }, this);
        }
        #endregion

        #region Hooks
        private void Loaded()
        {
            permission.RegisterPermission(PERMISSION_USE, this);
        }
        #endregion

        #region Commands

        [ChatCommand("teaminfo")]
        private void TeamInfoCommand(BasePlayer player, string command, string[] args)
        {
            if(!(permission.UserHasPermission(player.UserIDString,PERMISSION_USE)))
            {
                PrintToChat(player, lang.GetMessage("NoPerm", this, player.UserIDString));
                return;
            }
            if (args.Length == 0)
            {
                PrintToChat(player, lang.GetMessage("CmdHelp1", this, player.UserIDString));
                return;
            }

            IPlayer target = covalence.Players.FindPlayer(args[0]);
            if (target==null)
            {
                PrintToChat(player,lang.GetMessage("PlayerNotFound",this,player.UserIDString));
                return;
            }

            RelationshipManager.PlayerTeam targetTeam = null;
            if (RelationshipManager.Instance.playerToTeam.ContainsKey(ulong.Parse(target.Id)))
            {
                targetTeam = RelationshipManager.Instance.playerToTeam[ulong.Parse(target.Id)];
            }

            if (targetTeam == null)
            {
                PrintToChat(player, lang.GetMessage("TeamNotFound", this, player.UserIDString),target.Name);
                return;
            }
            
            string result = "";
            string teamMembers = string.Join(", ", targetTeam.members.Select(teamMember => covalence.Players.FindPlayerById(teamMember.ToString()).Name));
            result += string.Format(lang.GetMessage("Result1", this, player.UserIDString), targetTeam.teamID);
            result += string.Format(lang.GetMessage("Result2", this, player.UserIDString), covalence.Players.FindPlayerById(targetTeam.teamLeader.ToString()).Name);
            result += string.Format(lang.GetMessage("Result3", this, player.UserIDString), targetTeam.teamName);
            result += string.Format(lang.GetMessage("Result4", this, player.UserIDString), targetTeam.teamStartTime);
            result += string.Format(lang.GetMessage("Result5", this, player.UserIDString), targetTeam.teamLifetime);
            result += string.Format(lang.GetMessage("Result6", this, player.UserIDString), teamMembers);
            PrintToChat(player, result);
        }
        #endregion
    }
}

