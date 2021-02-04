using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Team Offline Doors", "Gargoyle", "0.8.4")]
    [Description("When the last team member goes offline, closes doors / hides stashes of the whole team.")]

    class TeamOfflineDoors : RustPlugin
    {
	#region Configuration

	private bool CloseDoors = true; // Set to false to disable Door-Closing feature!
	private bool HideStash  = true; // Set to false to disable Stash-Hiding feature!

	#endregion

        #region Hooks

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
	    if (player)
	    {
		if (player.currentTeam != 0UL)
		{
		    RelationshipManager.PlayerTeam theTeam = RelationshipManager.Instance.FindTeam(player.currentTeam);

		    int online = 0;                        
		    foreach (var member in theTeam.members)
		    {
			if (member == player.userID)
				continue;  // Do not check if the logging out player is online xD
			
			foreach (BasePlayer ingame in BasePlayer.activePlayerList)
			{
			    if (ingame.userID == member)
			    {
				online++;	// TeamMember online!
				break;		//  = Do nothing!
			    }
			}

			if ( online > 0 )
			   break;	// If someone was online, do nothing.
		    }

		    if( online == 0 )	// Only if no team members were online? 
		    {
			foreach (var member in theTeam.members)  // For each TeamMember...
			{
			   if (CloseDoors) DoClose( member );	// Close their doors?
			   if (HideStash) DoHide( member );	// Hide their stashes?
			}
		    }


		} //end of "if team", below same for solo players:
		else
		{
		   if (CloseDoors) DoClose( player.userID );	// Close their doors?
		   if (HideStash) DoHide( player.userID );	// Hide their stashes?
		}

	    } // (if player defined)
        }
        #endregion



        #region Commands

	private void DoClose(ulong playerid)
	{
	 // Step 1: Make List
	    List<Door> list = Resources.FindObjectsOfTypeAll<Door>().Where(x => x.OwnerID == playerid).ToList();
	    if (list.Count == 0) return;

	 // Step 2: Process List...
	    foreach (var item in list)
	    {
		// Step 3: Close all his/her open doors
		if (item.IsOpen()) item.CloseRequest();	
	    }
	}

        private void DoHide(ulong playerid)
        {
	 // Step 1: Make List
	    List<StashContainer> list = Resources.FindObjectsOfTypeAll<StashContainer>().Where(x => x.OwnerID == playerid).ToList();
	    if (list.Count == 0) return;

	 // Step 2: Process List...
	    foreach (var item in list)
	    {
		// Step 3: Hide all his/her visible Stashes
		if (!item.IsHidden()) item.SetHidden(true);
	    }
	}

        #endregion
    }

}
