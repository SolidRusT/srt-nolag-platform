using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    using ClansEx;

    [Info("Clans", "k1lly0u", "0.2.5")]
    class Clans : CovalencePlugin
    {
        #region Fields        
        private bool isInitialized = false;

        private Regex hexFilter = new Regex("^([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$");

        public static Clans Instance { get; private set; }

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);

        private static readonly double MaxUnixSeconds = (DateTime.MaxValue - Epoch).TotalSeconds;

        private const string COLORED_LABEL = "[{0}]{1}[/#]";
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            Instance = this;
            LoadData();
        }

        protected override void LoadDefaultMessages() => lang.RegisterMessages(Messages, this);

        private void OnServerInitialized()
        {
            if (!configData.Tags.Enabled)
                Unsubscribe(nameof(OnPluginLoaded));

            InitializeClans();
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (configData.Tags.Enabled && plugin?.Title == "Better Chat")
                Interface.CallHook("API_RegisterThirdPartyTitle", this, new Func<IPlayer, string>(BetterChat_FormattedClanTag));
        }

        private void OnUserConnected(IPlayer player)
        {            
            Clan clan = storedData?.FindClanByID(player.Id);
            if (clan != null)
            {
                clan.OnPlayerConnected(player);
            }
            else
            {
                List<string> invites;
                if (storedData.playerInvites.TryGetValue(player.Id, out invites))
                {
                    player.Reply(string.Format(Message("Notification.PendingInvites", player.Id), invites.ToSentence(), "clan"));
                }
            }
        }

        private void OnUserDisconnected(IPlayer player) => storedData?.FindClanByID(player.Id)?.OnPlayerDisconnected(player);

        private void Unload()
        {
            SaveData();

            Instance = null;
        }
        #endregion

        #region Functions
        private void InitializeClans()
        {
            Puts("Initializing Clans...");

            List<string> purgedClans = ListPool.Get<string>();

            foreach (KeyValuePair<string, Clan> kvp in storedData.clans)
            {
                Clan clan = kvp.Value;

                if (clan.ClanMembers.Count == 0 || (configData.Purge.Enabled && UnixTimeStampUTC() - clan.LastOnlineTime > (configData.Purge.OlderThanDays * 86400)))
                {
                    purgedClans.Add(kvp.Key);
                    continue;
                }

                if (configData.Clans.Alliance.Enabled)
                {
                    for (int i = clan.AllianceInvites.Count - 1; i >= 0; i--)
                    {
                        KeyValuePair<string, double> allianceInvite = clan.AllianceInvites.ElementAt(i);

                        if (!storedData.clans.ContainsKey(allianceInvite.Key) || (UnixTimeStampUTC() - allianceInvite.Value > configData.Clans.Invites.AllianceInviteExpireTime))
                            clan.AllianceInvites.Remove(allianceInvite.Key);
                    }

                    for (int i = clan.Alliances.Count - 1; i >= 0; i--)
                    {
                        string allyTag = clan.Alliances.ElementAt(i);

                        if (!storedData.clans.ContainsKey(allyTag))
                            clan.Alliances.Remove(allyTag);
                    }
                }

                for (int i = clan.MemberInvites.Count - 1; i >= 0; i--)
                {
                    KeyValuePair<string, Clan.MemberInvite> memberInvite = clan.MemberInvites.ElementAt(i);

                    if (UnixTimeStampUTC() - memberInvite.Value.ExpiryTime > configData.Clans.Invites.MemberInviteExpireTime)
                        clan.MemberInvites.Remove(memberInvite.Key);
                }

                foreach (KeyValuePair<string, Clan.Member> member in clan.ClanMembers)
                    storedData.RegisterPlayer(member.Key, clan.Tag);
            }

            if (purgedClans.Count > 0)
            {
                Puts($"Purging {purgedClans.Count} expired or invalid clans");

                StringBuilder str = new StringBuilder();

                for (int i = 0; i < purgedClans.Count; i++)
                {
                    string tag = purgedClans[i];
                    Clan clan = storedData.clans[tag];
                    if (clan == null)
                        continue;

                    str.Append($"{(i > 0 ? "\n" : "")}Purged - [{tag}] | {clan.Description} | Owner: {clan.OwnerID} | Last Online: {UnixTimeStampToDateTime(clan.LastOnlineTime)}");

                    storedData.clans.Remove(tag);
                }

                if (configData.Purge.ListPurgedClans)
                {
                    Puts(str.ToString());

                    if (configData.Options.LogChanges)
                        LogToFile(Title, str.ToString(), this);
                }
            }

            Puts($"Loaded {storedData.clans.Count} clans!");

            ListPool.Free(ref purgedClans);

            if (configData.Tags.Enabled)
                Interface.CallHook("API_RegisterThirdPartyTitle", this, new Func<Oxide.Core.Libraries.Covalence.IPlayer, string>(BetterChat_FormattedClanTag));

            isInitialized = true;

            foreach (IPlayer player in players.Connected)
                OnUserConnected(player);

            TimedSaveData();
        }

        private bool ClanTagExists(string tag)
        {
            ICollection<string> collection = storedData.clans.Keys;
            for (int i = 0; i < collection.Count; i++)
            {
                if (collection.ElementAt(i).Equals(tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds((float)time);
            int days = dateDifference.Days;
            int hours = dateDifference.Hours + (days * 24);

            int mins = dateDifference.Minutes;
            int secs = dateDifference.Seconds;

            if (hours > 0)
                return string.Format("{0:00}h:{1:00}m:{2:00}s", hours, mins, secs);
            else if (mins > 0)
                return string.Format("{0:00}m:{1:00}s", mins, secs);
            else return string.Format("{0:00}s", secs);
        }

        private string BetterChat_FormattedClanTag(IPlayer player)
        {            
            Clan clan = storedData.FindClanByID(player.Id);
            if (clan == null)
                return string.Empty;

            return $"[#{(string.IsNullOrEmpty(clan.TagColor) || !configData.Tags.CustomColors ? configData.Tags.TagColor.Replace("#", "") : clan.TagColor.Replace("#", ""))}][+{configData.Tags.TagSize}]{configData.Tags.TagOpen}{clan.Tag}{configData.Tags.TagClose}[/+][/#]";
        }

        private static int UnixTimeStampUTC() => (int)DateTime.UtcNow.Subtract(Epoch).TotalSeconds;

        private static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            return unixTimeStamp > MaxUnixSeconds
                ? Epoch.AddMilliseconds(unixTimeStamp)
                : Epoch.AddSeconds(unixTimeStamp);
        }
        #endregion

        #region Clan Management        
        internal void CreateClan(IPlayer player, string tag, string description)
        {
            if (player == null)
                return;

            if (storedData.FindClanByID(player.Id) != null)
            {
                player.Reply(Message("Notification.Create.InExistingClan", player.Id));
                return;
            }

            if (tag.Length < configData.Tags.TagLength.Minimum || tag.Length > configData.Tags.TagLength.Maximum)
            {
                player.Reply(string.Format(Message("Notification.Create.InvalidTagLength", player.Id), configData.Tags.TagLength.Minimum, configData.Tags.TagLength.Maximum));
                return;
            }

            if (ClanTagExists(tag))
            {
                player.Reply(Message("Notification.Create.ClanExists", player.Id));
                return;
            }

            storedData.clans[tag] = new Clan(player, tag, description);
            storedData.RegisterPlayer(player.Id, tag);

            player.Reply(string.Format(Message("Notification.Create.Success", player.Id), tag));

            Interface.CallHook("OnClanCreate", tag);

            if (configData.Options.LogChanges)
                LogToFile(Title, $"{player.Name} created the clan [{tag}]", this);
        }

        internal bool InvitePlayer(IPlayer inviter, string targetId)
        {
            IPlayer invitee = covalence.Players.FindPlayerById(targetId) ?? null;
            if (invitee == null)
            {
                inviter.Reply(string.Format(Message("Notification.Generic.UnableToFindPlayer", inviter.Id), targetId));
                return false;
            }

            return InvitePlayer(inviter, invitee);
        }

        internal bool InvitePlayer(IPlayer inviter, IPlayer invitee)
        {
            if (inviter == null || invitee == null)
                return false;

            Clan clan = storedData.FindClanByID(inviter.Id);
            if (clan == null)
            {
                inviter.Reply(Message("Notification.Generic.NoClan", inviter.Id));
                return false;
            }

            Clan other = storedData.FindClanByID(invitee.Id);
            if (other != null)
            {
                inviter.Reply(string.Format(Message("Notification.Invite.InClan", inviter.Id), invitee.Name));
                return false;
            }

            return clan.InvitePlayer(inviter, invitee);
        }

        internal bool WithdrawInvite(IPlayer player, string partialNameOrID)
        {
            if (player == null)
                return false;

            Clan clan = storedData.FindClanByID(player.Id);
            if (clan == null)
            {
                player.Reply(Message("Notification.Generic.NoClan", player.Id));
                return false;
            }

            if (!clan.IsOwner(player.Id) && !clan.IsModerator(player.Id))
            {
                player.Reply(Message("Notification.WithdrawInvite.NoPermissions", player.Id));
                return false;
            }

            foreach (KeyValuePair<string, Clan.MemberInvite> invite in clan.MemberInvites)
            {
                if (partialNameOrID.Equals(invite.Key) || invite.Value.DisplayName.Contains(partialNameOrID))
                {
                    storedData.RevokePlayerInvite(partialNameOrID, clan.Tag);

                    clan.MemberInvites.Remove(invite.Key);
                    clan.Broadcast("Notification.WithdrawInvite.Success", player.Name, invite.Value.DisplayName);
                    return true;
                }
            }

            player.Reply(string.Format(Message("Notification.WithdrawInvite.UnableToFind", player.Id), partialNameOrID));
            return false;
        }

        internal bool RejectInvite(IPlayer player, string tag)
        {
            if (player == null)
                return false;

            Clan clan = storedData.FindClan(tag);
            if (clan == null)
            {
                player.Reply(string.Format(Message("Notification.Generic.InvalidClan", player.Id), tag));
                return false;
            }

            if (!clan.MemberInvites.ContainsKey(player.Id))
            {
                player.Reply(string.Format(Message("Notification.RejectInvite.InvalidInvite", player.Id), tag));
                return false;
            }

            clan.MemberInvites.Remove(player.Id);

            storedData.OnInviteRejected(player.Id, clan.Tag);

            clan.Broadcast("Notification.RejectInvite.Reply", player.Name);
            player.Reply(string.Format(Message("Notification.RejectInvite.PlayerMessage", player.Id), tag));

            if (configData.Options.LogChanges)
                Instance.LogToFile(Instance.Title, $"{player.Name} rejected their invite to [{tag}]", Instance);

            return true;
        }

        internal bool JoinClan(IPlayer player, string tag)
        {
            if (player == null || string.IsNullOrEmpty(tag))
                return false;

            Clan clan = storedData.FindClanByID(player.Id);
            if (clan != null)
            {
                player.Reply(Message("Notification.Join.InExistingClan", player.Id));
                return false;
            }

            clan = storedData.FindClan(tag);
            if (clan == null)
                return false;

            return clan.JoinClan(player);
        }

        internal bool LeaveClan(IPlayer player)
        {
            if (player == null)
                return false;

            Clan clan = storedData.FindClanByID(player.Id);
            if (clan == null)
            {
                player.Reply(Message("Notification.Generic.NoClan", player.Id));
                return false;
            }

            return clan.LeaveClan(player);
        }

        internal bool KickPlayer(IPlayer player, string playerId)
        {
            if (player == null)
                return false;

            Clan clan = storedData.FindClanByID(player.Id);
            if (clan == null)
            {
                player.Reply(Message("Notification.Generic.NoClan", player.Id));
                return false;
            }

            return clan.KickMember(player, playerId);
        }

        internal bool PromotePlayer(IPlayer promoter, string targetId)
        {
            if (promoter == null)
                return false;

            Clan clan = storedData.FindClanByID(promoter.Id);
            if (clan == null)
            {
                promoter.Reply(Message("Notification.Generic.NoClan", promoter.Id));
                return false;
            }

            Clan other = storedData.FindClanByID(targetId);
            if (other == null || !clan.Tag.Equals(other.Tag))
            {
                string Name = covalence.Players.FindPlayer(targetId)?.Name ?? targetId;

                promoter.Reply(string.Format(Message("Notification.Promotion.TargetNoClan", promoter.Id), Name));
                return false;
            }

            return clan.PromotePlayer(promoter, targetId);
        }

        internal bool DemotePlayer(IPlayer demoter, string targetId)
        {
            if (demoter == null)
                return false;

            Clan clan = storedData.FindClanByID(demoter.Id);
            if (clan == null)
            {
                demoter.Reply(Message("Notification.Generic.NoClan", demoter.Id));
                return false;
            }

            Clan other = storedData.FindClanByID(targetId);
            if (other == null || !clan.Tag.Equals(other.Tag))
            {
                string Name = covalence.Players.FindPlayer(targetId)?.Name ?? targetId;

                demoter.Reply(string.Format(Message("Notification.Promotion.TargetNoClan", demoter.Id), Name));
                return false;
            }

            return clan.DemotePlayer(demoter, targetId);
        }

        internal bool DisbandClan(IPlayer player)
        {
            Clan clan = storedData.FindClanByID(player.Id);

            if (clan == null)
            {
                player.Reply(Message("Notification.Generic.NoClan", player.Id));
                return false;
            }

            if (!clan.IsOwner(player.Id))
            {
                player.Reply(Message("Notification.Disband.NotOwner", player.Id));
                return false;
            }

            string tag = clan.Tag;

            clan.Broadcast("Notification.Disband.Reply", Array.Empty<object>());
            clan.DisbandClan();

            player.Reply(string.Format(Message("Notification.Disband.Success", player.Id), tag));

            return true;
        }
        #endregion

        #region Alliance Management
        internal bool OfferAlliance(IPlayer player, string tag)
        {
            if (!configData.Clans.Alliance.Enabled)
                return false;

            Clan clan = storedData.FindClanByID(player.Id);
            if (clan == null)
            {
                player.Reply(Message("Notification.Generic.NoClan", player.Id));
                return false;
            }

            Clan alliedClan = storedData.FindClan(tag);
            if (alliedClan == null)
            {
                player.Reply(string.Format(Message("Notification.Generic.InvalidClan", player.Id), tag));
                return false;
            }

            if (!clan.IsOwner(player.Id))
            {
                player.Reply(Message("Notification.Alliance.NoPermissions", player.Id));
                return false;
            }

            if (clan.AllianceInvites.ContainsKey(tag) && (UnixTimeStampUTC() - clan.AllianceInvites[tag] < configData.Clans.Invites.AllianceInviteExpireTime))
            {
                player.Reply(string.Format(Message("Notification.Alliance.PendingInvite", player.Id), tag));
                return false;
            }

            if (clan.AllianceInviteCount >= configData.Clans.Invites.AllianceInviteLimit)
            {
                player.Reply(Message("Notification.Alliance.MaximumInvites", player.Id));
                return false;
            }

            if (clan.AllianceCount >= configData.Clans.Alliance.AllianceLimit)
            {
                player.Reply(Message("Notification.Alliance.MaximumAlliances", player.Id));
                return false;
            }

            clan.AllianceInvites[tag] = UnixTimeStampUTC();
            alliedClan.IncomingAlliances.Add(clan.Tag);

            player.Reply(string.Format(Message("Notification.Alliance.InviteSent", player.Id), tag, FormatTime(configData.Clans.Invites.AllianceInviteExpireTime)));

            alliedClan.Broadcast("Notification.Alliance.InviteReceived", clan.Tag, FormatTime(configData.Clans.Invites.AllianceInviteExpireTime), "ally");

            return true;
        }

        internal bool WithdrawAlliance(IPlayer player, string tag)
        {
            if (!configData.Clans.Alliance.Enabled)
                return false;

            Clan clan = storedData.FindClanByID(player.Id);
            if (clan == null)
            {
                player.Reply(Message("Notification.Generic.NoClan", player.Id));
                return false;
            }

            Clan alliedClan = storedData.FindClan(tag);
            if (alliedClan == null)
            {
                player.Reply(string.Format(Message("Notification.Generic.InvalidClan", player.Id), tag));
                return false;
            }

            if (!clan.IsOwner(player.Id))
            {
                player.Reply(Message("Notification.Alliance.NoPermissions", player.Id));
                return false;
            }

            if (!clan.AllianceInvites.ContainsKey(tag))
            {
                player.Reply(string.Format(Message("Notification.Alliance.NoActiveInvite", player.Id), tag));
                return false;
            }

            clan.AllianceInvites.Remove(tag);
            alliedClan.IncomingAlliances.Remove(clan.Tag);

            clan.Broadcast("Notification.Alliance.WithdrawnClan", player.Name, tag);
            alliedClan.Broadcast("Notification.Alliance.WithdrawnTarget", clan.Tag);

            clan.MarkDirty();

            return true;
        }

        internal bool AcceptAlliance(IPlayer player, string tag)
        {
            if (!configData.Clans.Alliance.Enabled)
                return false;

            Clan clan = storedData.FindClanByID(player.Id);
            if (clan == null)
            {
                player.Reply(Message("Notification.Generic.NoClan", player.Id));
                return false;
            }

            Clan alliedClan = storedData.FindClan(tag);
            if (alliedClan == null)
            {
                player.Reply(string.Format(Message("Notification.Generic.InvalidClan", player.Id), tag));
                return false;
            }

            if (!clan.IsOwner(player.Id))
            {
                player.Reply(Message("Notification.Alliance.NoPermissions", player.Id));
                return false;
            }

            bool noActiveInvite = false;
            if (!alliedClan.AllianceInvites.ContainsKey(clan.Tag))
                noActiveInvite = true;

            if ((UnixTimeStampUTC() - alliedClan.AllianceInvites[clan.Tag] > configData.Clans.Invites.AllianceInviteExpireTime))
            {
                alliedClan.AllianceInvites.Remove(clan.Tag);
                noActiveInvite = true;
            }

            if (noActiveInvite)
            {
                player.Reply(string.Format(Message("Notification.Alliance.NoActiveInviteFrom", player.Id), tag));
                return false;
            }

            if (alliedClan.AllianceCount >= configData.Clans.Alliance.AllianceLimit)
            {
                player.Reply(string.Format(Message("Notification.Alliance.AtLimitTarget", player.Id), tag));
                return false;
            }

            if (clan.AllianceCount >= configData.Clans.Alliance.AllianceLimit)
            {
                player.Reply(string.Format(Message("Notification.Alliance.AtLimitSelf", player.Id), tag));
                return false;
            }

            clan.Alliances.Add(tag);
            clan.IncomingAlliances.Remove(tag);

            alliedClan.Alliances.Add(clan.Tag);
            alliedClan.AllianceInvites.Remove(clan.Tag);

            clan.MarkDirty();
            alliedClan.MarkDirty();

            clan.Broadcast("Notification.Alliance.Formed", clan.Tag, alliedClan.Tag);
            alliedClan.Broadcast("Notification.Alliance.Formed", clan.Tag, alliedClan.Tag);

            Interface.Oxide.CallHook("OnClanUpdate", clan.Tag);
            Interface.Oxide.CallHook("OnClanUpdate", alliedClan.Tag);

            return true;
        }

        internal bool RejectAlliance(IPlayer player, string tag)
        {
            if (!configData.Clans.Alliance.Enabled)
                return false;

            Clan clan = storedData.FindClanByID(player.Id);
            if (clan == null)
            {
                player.Reply(string.Format(Message("Notification.Generic.InvalidClan", player.Id), tag));
                return false;
            }

            Clan alliedClan = storedData.FindClan(tag);
            if (alliedClan == null)
            {
                player.Reply(Message("Notification.Generic.InvalidClan", player.Id));
                return false;
            }

            if (!clan.IsOwner(player.Id))
            {
                player.Reply(Message("Notification.Alliance.NoPermissions", player.Id));
                return false;
            }

            if (!alliedClan.AllianceInvites.ContainsKey(clan.Tag) || (UnixTimeStampUTC() - alliedClan.AllianceInvites[clan.Tag] > configData.Clans.Invites.AllianceInviteExpireTime))
            {
                player.Reply(string.Format(Message("Notification.Alliance.NoActiveInvite", player.Id), tag));
                return false;
            }

            clan.IncomingAlliances.Remove(tag);

            alliedClan.AllianceInvites.Remove(clan.Tag);
            alliedClan.MarkDirty();

            clan.Broadcast("Notification.Alliance.Rejected", clan.Tag, alliedClan.Tag);
            alliedClan.Broadcast("Notification.Alliance.Rejected", clan.Tag, alliedClan.Tag);

            return true;
        }

        internal bool RevokeAlliance(IPlayer player, string tag)
        {
            if (!configData.Clans.Alliance.Enabled)
                return false;

            Clan clan = storedData.FindClanByID(player.Id);
            if (clan == null)
            {
                player.Reply(string.Format(Message("Notification.Generic.InvalidClan", player.Id), tag));
                return false;
            }

            Clan alliedClan = storedData.FindClan(tag);
            if (alliedClan == null)
            {
                player.Reply(Message("Notification.Generic.InvalidClan", player.Id));
                return false;
            }

            if (!clan.IsOwner(player.Id))
            {
                player.Reply(Message("Notification.Alliance.NoPermissions", player.Id));
                return false;
            }

            if (!clan.Alliances.Contains(alliedClan.Tag))
            {
                player.Reply(string.Format(Message("Notification.Alliance.NoActiveAlliance", player.Id), alliedClan.Tag));
                return false;
            }

            alliedClan.Alliances.Remove(clan.Tag);
            clan.Alliances.Remove(alliedClan.Tag);

            alliedClan.MarkDirty();
            clan.MarkDirty();

            clan.Broadcast("Notification.Alliance.Revoked", clan.Tag, alliedClan.Tag);
            alliedClan.Broadcast("Notification.Alliance.Revoked", clan.Tag, alliedClan.Tag);

            return true;
        }
        #endregion

        #region Chat
        private void ClanChat(IPlayer player, string message)
        {
            if (player == null)
                return;

            Clan clan = storedData.FindClanByID(player.Id);
            if (clan == null)
                return;

            string str = string.Format(Message("Chat.Alliance.Format"), clan.Tag, clan.GetRoleColor(player.Id), player.Name, message);

            clan.Broadcast(string.Format(Message("Chat.Clan.Prefix"), str));

            Interface.CallHook("OnClanChat", player, message, clan.Tag);
        }

        private void AllianceChat(IPlayer player, string message)
        {
            Clan clan = storedData.FindClanByID(player.Id);
            if (clan == null)
                return;

            string str = string.Format(Message("Chat.Alliance.Format"), clan.Tag, clan.GetRoleColor(player.Id), player.Name, message);

            clan.Broadcast(string.Format(Message("Chat.Alliance.Prefix"), str));

            for (int i = 0; i < clan.AllianceCount; i++)
            {
                Clan alliedClan = storedData.FindClan(clan.Alliances.ElementAt(i));
                if (alliedClan != null)
                {
                    alliedClan.Broadcast(string.Format(Message("Chat.Alliance.Prefix"), str));
                }
            }

            Interface.CallHook("OnAllianceChat", player, message, clan.Tag);
        }
        #endregion

        #region Chat Commands
        [Command("a")]
        private void cmdAllianceChat(IPlayer player, string command, string[] args)
        {
            if (!configData.Clans.Alliance.Enabled || args.Length == 0)
                return;

            AllianceChat(player, string.Join(" ", args));
        }

        [Command("c")]
        private void cmdClanChat(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0)
                return;

            ClanChat(player, string.Join(" ", args));
        }

        [Command("cinfo")]
        private void cmdChatClanInfo(IPlayer player, string command, string[] args)
        {            
            if (args.Length == 0)
            {
                player.Reply(Message("Notification.Generic.SpecifyClanTag", player.Id));
                return;
            }

            Clan clan = storedData.FindClan(args[0]);
            if (clan == null)
            {
                player.Reply(string.Format(Message("Notification.Generic.InvalidClan", player.Id), args[0]));
                return;
            }

            clan.PrintClanInfo(player);
        }

        [Command("clanhelp")]
        private void cmdChatClanHelp(IPlayer player, string command, string[] args)
        {
            StringBuilder sb = new StringBuilder();

            Clan clan = storedData.FindClanByID(player.Id);
            if (clan == null)
            {
                sb.Append(Message("Notification.ClanInfo.Title", player.Id));
                sb.Append(string.Format(Message("Notification.ClanHelp.NoClan", player.Id), "clan"));
                player.Reply(sb.ToString());
                return;
            }

            sb.Append(Message("Notification.ClanInfo.Title", player.Id));
            sb.Append(string.Format(Message("Notification.ClanHelp.Basic", player.Id), "clan", "c"));

            if (clan.IsModerator(player.Id) || clan.OwnerID.Equals(player.Id))
            {
                if (configData.Clans.Alliance.Enabled && clan.OwnerID.Equals(player.Id))
                    sb.Append(string.Format(Message("Notification.ClanHelp.Alliance", player.Id), "ally"));

                sb.Append(string.Format(Message("Notification.ClanHelp.Moderator", player.Id), "clan"));
            }

            if (clan.OwnerID.Equals(player.Id))
            {
                sb.Append(string.Format(Message("Notification.ClanHelp.Owner", player.Id), "clan"));

                if (configData.Tags.CustomColors)
                    sb.Append(string.Format(Message("Notification.ClanHelp.TagColor", player.Id), "clan"));
            }

            player.Reply(sb.ToString());

        }

        [Command("ally")]
        private void cmdChatClanAlly(IPlayer player, string command, string[] args)
        {
            if (!configData.Clans.Alliance.Enabled)
                return;

            if (args.Length < 2)
            {
                player.Reply(string.Format(Message("Notification.ClanHelp.Alliance", player.Id), "ally"));
                return;
            }

            string tag = args[1];

            switch (args[0].ToLower())
            {
                case "invite":
                    OfferAlliance(player, tag);
                    return;
                case "withdraw":
                    WithdrawAlliance(player, tag);
                    return;
                case "accept":
                    AcceptAlliance(player, tag);
                    return;
                case "reject":
                    RejectAlliance(player, tag);
                    return;
                case "revoke":
                    RevokeAlliance(player, tag);
                    return;
                default:
                    player.Reply(string.Format(Message("Notification.ClanHelp.Alliance", player.Id), "ally"));
                    return;
            }
        }

        [Command("clan")]
        private void cmdChatClan(IPlayer player, string command, string[] args)
        {
            Clan clan = storedData.FindClanByID(player.Id);

            if (args.Length == 0)
            {
                StringBuilder sb = new StringBuilder();
                if (clan == null)
                {
                    sb.Append(Message("Notification.ClanInfo.Title", player.Id));
                    sb.Append(Message("Notification.Clan.NotInAClan", player.Id));
                    sb.Append(string.Format(Message("Notification.Clan.Help", player.Id), "clanhelp"));
                    player.Reply(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(Message("Notification.ClanInfo.Title", player.Id));
                    sb.Append(string.Format(Message((clan.IsOwner(player.Id) ? "Notification.Clan.OwnerOf" : clan.IsModerator(player.Id) ? "Notification.Clan.ModeratorOf" : "Notification.Clan.MemberOf"), player.Id), clan.Tag, clan.OnlineCount, clan.MemberCount));
                    sb.Append(string.Format(Message("Notification.Clan.MembersOnline", player.Id), clan.GetMembersOnline()));

                    sb.Append(string.Format(Message("Notification.Clan.Help", player.Id), "clanhelp"));
                    player.Reply(sb.ToString());
                    sb.Clear();
                }
                return;
            }

            string tag = clan?.Tag ?? string.Empty;

            switch (args[0].ToLower())
            {
                case "create":
                    if (args.Length < 2)
                    {
                        player.Reply(string.Format(Message("Notification.Clan.CreateSyntax", player.Id), "clan"));
                        return;
                    }

                    CreateClan(player, args[1], args.Length > 2 ? string.Join(" ", args.Skip(2)) : string.Empty);
                    return;

                case "leave":
                    LeaveClan(player);
                    return;

                case "invite":
                    if (args.Length < 2)
                    {
                        player.Reply(string.Format(Message("Notification.Clan.InviteSyntax", player.Id), "clan"));
                        return;
                    }

                    IPlayer invitee = players.FindPlayer(args[1]);
                    if (invitee == null)
                    {
                        player.Reply(string.Format(Message("Notification.Generic.UnableToFindPlayer", player.Id), args[1]));
                        return;
                    }

                    if (invitee == player)
                    {
                        player.Reply(Message("Notification.Generic.CommandSelf", player.Id));
                        return;
                    }

                    InvitePlayer(player, invitee);
                    return;

                case "withdraw":
                    if (args.Length < 2)
                    {
                        player.Reply(string.Format(Message("Notification.Clan.WithdrawSyntax", player.Id), "clan"));
                        return;
                    }

                    WithdrawInvite(player, args[1]);
                    return;

                case "accept":
                    if (args.Length < 2)
                    {
                        player.Reply(string.Format(Message("Notification.Clan.AcceptSyntax", player.Id), "clan"));
                        return;
                    }

                    JoinClan(player, args[1]);
                    return;

                case "reject":
                    if (args.Length < 2)
                    {
                        player.Reply(string.Format(Message("Notification.Clan.RejectSyntax", player.Id), "clan"));
                        return;
                    }

                    RejectInvite(player, args[1]);
                    return;

                case "kick":
                    if (args.Length < 2)
                    {
                        player.Reply(string.Format(Message("Notification.Clan.KickSyntax", player.Id), "clan"));
                        return;
                    }

                    string target = clan.FindPlayer(args[1]);
                    if (string.IsNullOrEmpty(target))
                    {
                        player.Reply(Message("Notification.Kick.NoPlayerFound", player.Id));
                        return;
                    }

                    if (target == player.Id)
                    {
                        player.Reply(Message("Notification.Generic.CommandSelf", player.Id));
                        return;
                    }

                    KickPlayer(player, target);
                    return;

                case "promote":
                    if (args.Length < 2)
                    {
                        player.Reply(string.Format(Message("Notification.Clan.PromoteSyntax", player.Id), "clan"));
                        return;
                    }

                    string promotee = clan.FindPlayer(args[1]);
                    if (string.IsNullOrEmpty(promotee))
                    {
                        player.Reply(string.Format(Message("Notification.Generic.UnableToFindPlayer", player.Id), args[1]));
                        return;
                    }

                    if (promotee == player.Id)
                    {
                        player.Reply(Message("Notification.Generic.CommandSelf", player.Id));
                        return;
                    }

                    PromotePlayer(player, promotee);
                    return;

                case "demote":
                    if (args.Length < 2)
                    {
                        player.Reply(string.Format(Message("Notification.Clan.DemoteSyntax", player.Id), "clan"));
                        return;
                    }

                    string demotee = clan.FindPlayer(args[1]);
                    if (string.IsNullOrEmpty(demotee))
                    {
                        player.Reply(string.Format(Message("Notification.Generic.UnableToFindPlayer", player.Id), args[1]));
                        return;
                    }

                    if (demotee == player.Id)
                    {
                        player.Reply(Message("Notification.Generic.CommandSelf", player.Id));
                        return;
                    }

                    DemotePlayer(player, demotee);
                    return;

                case "disband":
                    if (args.Length < 2 || !args[1].Equals("forever", StringComparison.OrdinalIgnoreCase))
                    {
                        player.Reply(string.Format(Message("Notification.Clan.DisbandSyntax", player.Id), "clan"));
                        return;
                    }

                    if (clan == null)
                    {
                        player.Reply(Message("Notification.Generic.NoClan", player.Id));
                        return;
                    }

                    if (!clan.IsOwner(player.Id))
                    {
                        player.Reply(Message("Notification.Disband.NotOwner", player.Id));
                        return;
                    }

                    clan.Broadcast("Notification.Disband.Reply", Array.Empty<object>());
                    clan.DisbandClan();

                    player.Reply(string.Format(Message("Notification.Disband.Success", player.Id), tag));
                    return;

                case "tagcolor":
                    if (!configData.Tags.CustomColors)
                    {
                        player.Reply(Message("Notification.Clan.TagColorDisabled", player.Id));
                        return;
                    }

                    if (args.Length < 2)
                    {
                        player.Reply(string.Format(Message("Notification.Clan.TagColorSyntax", player.Id), "clan"));
                        return;
                    }

                    if (!clan.IsOwner(player.Id))
                    {
                        player.Reply(Message("Notification.Disband.NotOwner", player.Id));
                        return;
                    }

                    string hexColor = args[1].ToUpper();

                    if (hexColor.Equals("RESET"))
                    {
                        clan.TagColor = string.Empty;
                        player.Reply(Message("Notification.Clan.TagColorReset", player.Id));
                        return;
                    }

                    if (hexColor.Length < 6 || hexColor.Length > 6 || !hexFilter.IsMatch(hexColor))
                    {
                        player.Reply(Message("Notification.Clan.TagColorFormat", player.Id));
                        return;
                    }

                    clan.TagColor = hexColor;
                    player.Reply(string.Format(Message("Notification.Clan.TagColorSet", player.Id), clan.TagColor));
                    return;

                default:
                    player.Reply(string.Format(Message("Notification.Clan.Help", player.Id), "clanhelp"));
                    return;
            }
        }

        #endregion

        #region API       
        private JObject GetClan(string tag)
        {
            if (!string.IsNullOrEmpty(tag))
                return storedData.FindClan(tag)?.ToJObject();

            return null;
        }

        private JArray GetAllClans() => new JArray(storedData.clans.Keys);
        
        private string GetClanOf(string playerId) => storedData.FindClanByID(playerId)?.Tag ?? null;

        private string GetClanOf(ulong playerId) => GetClanOf(playerId.ToString());

        private string GetClanOf(IPlayer player) => GetClanOf(player?.Id ?? string.Empty);

        #if RUST
        private string GetClanOf(BasePlayer player) => GetClanOf(player?.UserIDString ?? string.Empty);
        #endif

        #if HURTWORLD
        private string GetClanOf(PlayerSession session) => GetClanOf(session?.SteamId ?? string.Empty);
        #endif

        private List<string> GetClanMembers(string playerId) => storedData.FindClanByID(playerId)?.ClanMembers.Keys.ToList() ?? new List<string>();
               
        private object HasFriend(string ownerId, string playerId)
        {
            Clan clanOwner = storedData.FindClanByID(ownerId);
            if (clanOwner == null)
                return null;

            Clan clanFriend = storedData.FindClanByID(playerId);
            if (clanFriend == null)
                return null;

            return clanOwner.Tag.Equals(clanFriend.Tag);
        }

        private bool IsClanMember(string playerId, string otherId)
        {
            Clan clanPlayer = storedData.FindClanByID(playerId);
            if (clanPlayer == null)
                return false;

            Clan clanOther = storedData.FindClanByID(otherId);
            if (clanOther == null)
                return false;

            return clanPlayer.Tag.Equals(clanOther.Tag);
        }

        private bool IsMemberOrAlly(string playerId, string otherId)
        {
            Clan playerClan = storedData.FindClanByID(playerId);
            if (playerClan == null)
                return false;

            Clan otherClan = storedData.FindClanByID(otherId);
            if (otherClan == null)
                return false;

            if ((playerClan.Tag.Equals(otherClan.Tag)) || playerClan.Alliances.Contains(otherClan.Tag))
                return true;

            return false;
        }
        
        private bool IsAllyPlayer(string playerId, string otherId)
        {
            Clan playerClan = storedData.FindClanByID(playerId);
            if (playerClan == null)
                return false;

            Clan otherClan = storedData.FindClanByID(otherId);
            if (otherClan == null)
                return false;

            if (playerClan.Alliances.Contains(otherClan.Tag))
                return true;

            return false;
        }

        private List<string> GetClanAlliances(string playerId)
        {
            Clan clan = storedData.FindClanByID(playerId);
            if (clan == null)
                return new List<string>();

            return new List<string>(clan.Alliances);
        }
#endregion

        #region Clan
        [Serializable]
        public class Clan
        {
            public string Tag { get; set; }

            public string Description { get; set; }

            public string OwnerID { get; set; }

            public double CreationTime { get; set; }

            public double LastOnlineTime { get; set; }

            public Hash<string, Member> ClanMembers { get; internal set; } = new Hash<string, Member>();

            public Hash<string, MemberInvite> MemberInvites { get; internal set; } = new Hash<string, MemberInvite>();

            public HashSet<string> Alliances { get; internal set; } = new HashSet<string>();

            public Hash<string, double> AllianceInvites { get; internal set; } = new Hash<string, double>();

            public HashSet<string> IncomingAlliances { get; internal set; } = new HashSet<string>();

            public string TagColor { get; internal set; } = string.Empty;

            [JsonIgnore]
            internal int OnlineCount { get; private set; }

            [JsonIgnore]
            internal int ModeratorCount => ClanMembers.Where(x => x.Value.Role == Member.MemberRole.Moderator).Count();

            [JsonIgnore]
            internal int MemberCount => ClanMembers.Count;

            [JsonIgnore]
            internal int MemberInviteCount => MemberInvites.Count;

            [JsonIgnore]
            internal int AllianceCount => Alliances.Count;

            [JsonIgnore]
            internal int AllianceInviteCount => AllianceInvites.Count;

            public Clan() { }

            public Clan(IPlayer player, string tag, string description)
            {
                this.Tag = tag;
                this.Description = description;
                CreationTime = LastOnlineTime = UnixTimeStampUTC();
                OwnerID = player.Id;
                ClanMembers.Add(player.Id, new Member(Member.MemberRole.Owner, player.Name));
                OnPlayerConnected(player);
            }

#region Connection
            internal void OnPlayerConnected(IPlayer player)
            {
                if (player == null)
                    return;

                Member member;
                if (ClanMembers.TryGetValue(player.Id, out member))
                {
                    member.Player = player;                    
                    LastOnlineTime = UnixTimeStampUTC();
                    OnlineCount++;
                }

                MarkDirty();
            }

            internal void OnPlayerDisconnected(IPlayer player)
            {
                if (player == null)
                    return;

                Member member;
                if (ClanMembers.TryGetValue(player.Id, out member))
                {                    
                    member.Player = null;
                    LastOnlineTime = UnixTimeStampUTC();
                    OnlineCount--;
                }

                MarkDirty();
            }
#endregion

#region Clan Management
            internal bool InvitePlayer(IPlayer inviter, IPlayer invitee)
            {
                if (!IsOwner(inviter.Id) && !IsModerator(inviter.Id))
                {
                    inviter.Reply(Message("Notification.Invite.NoPermissions", inviter.Id));
                    return false;
                }

                if (ClanMembers.ContainsKey(invitee.Id))
                {
                    inviter.Reply(string.Format(Message("Notification.Invite.IsMember", inviter.Id), invitee.Name));
                    return false;
                }

                if (MemberInvites.ContainsKey(invitee.Id))
                {
                    inviter.Reply(string.Format(Message("Notification.Invite.HasPending", inviter.Id), invitee.Name));
                    return false;
                }

                if (MemberCount >= configData.Clans.MemberLimit)
                {
                    inviter.Reply(Message("Notification.Generic.ClanFull", inviter.Id));
                    return false;
                }

                if (MemberInviteCount >= configData.Clans.Invites.MemberInviteLimit)
                {
                    inviter.Reply(Message("Notification.Invite.InviteLimit", inviter.Id));
                    return false;
                }

                MemberInvites[invitee.Id] = new MemberInvite(invitee);

                Instance.storedData.AddPlayerInvite(invitee.Id, Tag);

                invitee.Reply(string.Format(Message("Notification.Invite.SuccesTarget", invitee.Id), Tag, Description, "clan"));
                Broadcast("Notification.Invite.SuccessClan", inviter.Name, invitee.Name);

                if (configData.Options.LogChanges)
                    Instance.LogToFile(Instance.Title, $"{inviter.Name} invited {invitee.Name} to [{Tag}]", Instance);

                return true;
            }

            internal bool JoinClan(IPlayer player)
            {
                if (!MemberInvites.ContainsKey(player.Id))
                    return false;

                if ((UnixTimeStampUTC() - MemberInvites[player.Id].ExpiryTime > configData.Clans.Invites.AllianceInviteExpireTime))
                {
                    MemberInvites.Remove(player.Id);
                    player.Reply(string.Format(Message("Notification.Join.ExpiredInvite", player.Id), Tag));
                    return false;
                }

                if (MemberCount >= configData.Clans.MemberLimit)
                {
                    player.Reply(Message("Notification.Generic.ClanFull", player.Id));
                    return false;
                }

                Instance.storedData.OnInviteAccepted(player.Id, Tag);

                MemberInvites.Remove(player.Id);
                List<string> currentMembers = ClanMembers.Keys.ToList();

                ClanMembers.Add(player.Id, new Member(Member.MemberRole.Member, player.Name));

                Instance.storedData.RegisterPlayer(player.Id, Tag);

                OnPlayerConnected(player);

                Broadcast("Notification.Join.Reply", player.Name);

                Interface.Oxide.CallHook("OnClanMemberJoined", player.Id, Tag);
                Interface.Oxide.CallHook("OnClanMemberJoined", player.Id, currentMembers);

                Interface.Oxide.CallHook("OnClanUpdate", Tag);

                if (configData.Options.LogChanges)
                    Instance.LogToFile(Instance.Title, $"{player.Name} joined [{Tag}]", Instance);

                return true;
            }

            internal bool LeaveClan(IPlayer player)
            {
                if (!ClanMembers.ContainsKey(player.Id))
                    return false;

                OnPlayerDisconnected(player);

                ClanMembers.Remove(player.Id);
                Instance.storedData.UnregisterPlayer(player.Id);

                player.Reply(string.Format(Message("Notification.Leave.PlayerMessage", player.Id), Tag));
                Broadcast("Notification.Leave.Reply", player.Name);

                MarkDirty();

                if (ClanMembers.Count == 0)
                {
                    Interface.Oxide.CallHook("OnClanMemberGone", player.Id, Tag);
                    Interface.Oxide.CallHook("OnClanMemberGone", player.Id, ClanMembers.Keys.ToList());

                    if (configData.Options.LogChanges)
                        Instance.LogToFile(Instance.Title, $"{player.Name} has left [{Tag}]", Instance);

                    DisbandClan();
                    return true;
                }

                if (OwnerID == player.Id)
                {
                    KeyValuePair<string, Member> newOwner = ClanMembers.OrderBy(x => x.Value.Role).First();

                    OwnerID = newOwner.Key;
                    ClanMembers[OwnerID].Role = Member.MemberRole.Owner;

                    Broadcast("Notification.Leave.NewOwner", ClanMembers[OwnerID].DisplayName);
                }

                Interface.Oxide.CallHook("OnClanMemberGone", player.Id, ClanMembers.Keys.ToList());
                Interface.Oxide.CallHook("OnClanMemberGone", player.Id, Tag);
                Interface.Oxide.CallHook("OnClanUpdate", Tag);

                if (configData.Options.LogChanges)
                    Instance.LogToFile(Instance.Title, $"{player.Name} has left [{Tag}]", Instance);
                
                return true;
            }

            internal bool KickMember(IPlayer player, string targetId)
            {
                if (!ClanMembers.ContainsKey(targetId))
                {
                    player.Reply(Message("Notification.Kick.NotClanmember", player.Id));
                    return false;
                }

                if (IsOwner(targetId))
                {
                    player.Reply(Message("Notification.Kick.IsOwner", player.Id));
                    return false;
                }

                if (!IsOwner(player.Id) && !IsModerator(player.Id))
                {
                    player.Reply(Message("Notification.Kick.NoPermissions", player.Id));
                    return false;
                }

                if ((IsOwner(targetId) || IsModerator(targetId)) && OwnerID != player.Id)
                {
                    player.Reply(Message("Notification.Kick.NotEnoughRank", player.Id));
                    return false;
                }

                Member member = ClanMembers[targetId];

                if (member.IsConnected && member.Player != null)
                {
                    member.Player.Reply(string.Format(Message("Notification.Kick.PlayerMessage", member.Player.Id), player.Name));

                    OnPlayerDisconnected(member.Player);
                }

                ClanMembers.Remove(targetId);
                Instance.storedData.UnregisterPlayer(targetId);

                Broadcast("Notification.Kick.Reply", player.Name, member.DisplayName);

                Interface.Oxide.CallHook("OnClanMemberGone", targetId, ClanMembers.Keys.ToList());
                Interface.Oxide.CallHook("OnClanMemberGone", targetId, Tag);
                Interface.Oxide.CallHook("OnClanUpdate", Tag);

                if (configData.Options.LogChanges)
                    Instance.LogToFile(Instance.Title, $"{member.DisplayName} was kicked from [{Tag}] by {player.Name}", Instance);

                return true;
            }

            internal bool PromotePlayer(IPlayer promoter, string targetId)
            {
                if (!IsOwner(promoter.Id))
                {
                    promoter.Reply(Message("Notification.Promotion.NoPermissions", promoter.Id));
                    return false;
                }

                if (IsOwner(targetId))
                {
                    promoter.Reply(Message("Notification.Promotion.IsOwner", promoter.Id));
                    return false;
                }

                if (IsModerator(targetId))
                {
                    promoter.Reply(Message("Notification.Promotion.IsModerator", promoter.Id));
                    return false;
                }

                if (IsMember(targetId) && ModeratorCount >= configData.Clans.ModeratorLimit)
                {
                    promoter.Reply(Message("Notification.Promotion.ModeratorLimit", promoter.Id));
                    return false;
                }

                Member member = ClanMembers[targetId];
                member.Role = (Member.MemberRole)(Math.Min((int)member.Role - 1, (int)Member.MemberRole.Member));

                MarkDirty();

                Broadcast("Notification.Promotion.Reply", member.DisplayName, string.Format(COLORED_LABEL, GetRoleColor(member.Role), member.Role), string.Format(COLORED_LABEL, GetRoleColor(promoter.Id), promoter.Name));
                Interface.Oxide.CallHook("OnClanUpdate", Tag);

                if (configData.Options.LogChanges)
                    Instance.LogToFile(Instance.Title, $"{member.DisplayName} was promototed to {member.Role} by {promoter.Name}", Instance);

                return true;
            }

            internal bool DemotePlayer(IPlayer demoter, string targetId)
            {
                if (!IsOwner(demoter.Id))
                {
                    demoter.Reply(Message("Notification.Demotion.NoPermissions", demoter.Id));
                    return false;
                }

                Member member = ClanMembers[targetId];
                if (IsMember(targetId))
                {
                    demoter.Reply(string.Format(Message("Notification.Demotion.IsMember", demoter.Id), member.DisplayName));
                    return false;
                }

                member.Role = member.Role + 1;

                MarkDirty();

                Broadcast("Notification.Demotion.Reply", member.DisplayName, string.Format(COLORED_LABEL, GetRoleColor(member.Role), member.Role), string.Format(COLORED_LABEL, GetRoleColor(demoter.Id), demoter.Name));

                Interface.Oxide.CallHook("OnClanUpdate", Tag);

                if (configData.Options.LogChanges)
                    Instance.LogToFile(Instance.Title, $"{member.DisplayName} was demoted to {member.Role} by {demoter.Name}", Instance);
                return true;
            }

            internal void DisbandClan()
            {
                List<string> clanMembers = ClanMembers.Keys.ToList();

                OnUnload();

                Instance.storedData.clans.Remove(Tag);

                foreach (KeyValuePair<string, Clan> kvp in Instance.storedData.clans)
                    kvp.Value.OnClanDisbanded(Tag);

                if (configData.Options.LogChanges)
                    Instance.LogToFile(Instance.Title, $"The clan [{Tag}] was disbanded", Instance);

                Interface.CallHook("OnClanDisbanded", clanMembers);
                Interface.CallHook("OnClanDisbanded", Tag);
            }

            internal void OnClanDisbanded(string tag)
            {
                Alliances.Remove(tag);
                AllianceInvites.Remove(tag);
                IncomingAlliances.Remove(tag);
            }

            internal void OnUnload()
            {
                foreach (KeyValuePair<string, Member> kvp in ClanMembers)
                {
                    Instance.storedData.UnregisterPlayer(kvp.Key);

                    if (kvp.Value.Player != null)
                        OnPlayerDisconnected(kvp.Value.Player);
                }
            }

            internal bool IsAlliedClan(string otherClan) => Alliances.Contains(otherClan);

            internal void MarkDirty()
            {
                cachedClanInfo = string.Empty;
                membersOnline = string.Empty;
                serializedClanObject = null;
            }
            #endregion

            #region Clan Chat
                        internal void Broadcast(string message)
                        {
                            foreach (Member member in ClanMembers.Values)
                                member.Player?.Reply(message);
                        }

                        internal void Broadcast(string key, params object[] args)
                        {
                            foreach (Member member in ClanMembers.Values)
                                member.Player?.Reply(string.Format(Message(key, member.Player.Id), args));
                        }
            #endregion

            #region Clan Info
            [JsonIgnore]
            private string cachedClanInfo = string.Empty;

            [JsonIgnore]
            private string membersOnline = string.Empty;

            #if RUST
            internal void PrintClanInfo(BasePlayer player) => PrintClanInfo(player.IPlayer);
            #endif
            internal void PrintClanInfo(IPlayer player)
            {
                if (string.IsNullOrEmpty(cachedClanInfo))
                {
                    StringBuilder str = new StringBuilder();
                    str.Append(Message("Notification.ClanInfo.Title"));
                    str.Append(string.Format(Message("Notification.ClanInfo.Tag"), Tag));

                    if (!string.IsNullOrEmpty(Description))
                        str.Append(string.Format(Message("Notification.ClanInfo.Description"), Description));

                    List<string> online = ListPool.Get<string>();
                    List<string> offline = ListPool.Get<string>();

                    foreach (KeyValuePair<string, Member> kvp in ClanMembers)
                    {
                        string member = string.Format(COLORED_LABEL, GetRoleColor(kvp.Key), kvp.Value.DisplayName);

                        if (kvp.Value.IsConnected)
                            online.Add(member);
                        else offline.Add(member);
                    }

                    if (online.Count > 0)
                        str.Append(string.Format(Message("Notification.ClanInfo.Online"), online.ToSentence()));

                    if (offline.Count > 0)
                        str.Append(string.Format(Message("Notification.ClanInfo.Offline"), offline.ToSentence()));

                    ListPool.Free(ref online);
                    ListPool.Free(ref offline);

                    str.Append(string.Format(Message("Notification.ClanInfo.Established"), UnixTimeStampToDateTime(CreationTime)));
                    str.Append(string.Format(Message("Notification.ClanInfo.LastOnline"), UnixTimeStampToDateTime(LastOnlineTime)));

                    if (configData.Clans.Alliance.Enabled)
                        str.Append(string.Format(Message("Notification.ClanInfo.Alliances"), Alliances.Count > 0 ? Alliances.ToSentence() : Message("Notification.ClanInfo.Alliances.None")));

                    cachedClanInfo = str.ToString();
                }

                player.Reply(cachedClanInfo);
            }

            internal string GetMembersOnline()
            {
                if (string.IsNullOrEmpty(membersOnline))
                {
                    List<string> list = ListPool.Get<string>();

                    foreach (KeyValuePair<string, Member> kvp in ClanMembers)
                    {
                        if (kvp.Value.IsConnected)
                        {
                            string member = string.Format(COLORED_LABEL, GetRoleColor(kvp.Key), kvp.Value.DisplayName);
                            list.Add(member);
                        }
                    }

                    membersOnline = list.ToSentence();

                    ListPool.Free(ref list);
                }
                return membersOnline;
            }
            #endregion

            #region Roles
            internal bool IsOwner(ulong playerId) => IsOwner(playerId.ToString());

            internal bool IsOwner(string playerId) => ClanMembers[playerId].Role == Member.MemberRole.Owner;

            internal bool IsModerator(ulong playerId) => IsModerator(playerId.ToString());

            internal bool IsModerator(string playerId) => ClanMembers[playerId].Role == Member.MemberRole.Moderator;

            internal bool IsCouncil(ulong playerId) => false;

            internal bool IsMember(ulong playerId) => IsMember(playerId.ToString());

            internal bool IsMember(string playerId) => ClanMembers[playerId].Role == Member.MemberRole.Member;

            internal Member GetOwner() => ClanMembers[OwnerID];

            internal string GetRoleColor(string Id) => GetRoleColor(ClanMembers[Id].Role);

            internal string GetRoleColor(Member.MemberRole role)
            {
                if (role == Member.MemberRole.Owner)
                    return configData.Colors.Owner;

                if (role == Member.MemberRole.Moderator)
                    return configData.Colors.Moderator;

                return configData.Colors.Member;
            }
            #endregion

            [Serializable]
            public class Member
            {
                [JsonIgnore]
                public IPlayer Player { get; set; }

                [JsonProperty("Name")]
                public string DisplayName { get; set; } = string.Empty;

                public MemberRole Role { get; set; }

                [JsonIgnore]
                internal bool IsConnected => Player != null ? Player.IsConnected : false;

                [JsonIgnore]
                public bool MemberFFEnabled { get; set; } = false;

                [JsonIgnore]
                public bool AllyFFEnabled { get; set; } = false;

                public Member() { }

                public Member(MemberRole role, string name)
                {
                    this.Role = role;
                    this.DisplayName = name;
                }

                public enum MemberRole { Owner, Moderator, Member }
            }

            [Serializable]
            public class MemberInvite
            {
                [JsonProperty("Name")]
                public string DisplayName { get; set; }

                public double ExpiryTime { get; set; }

                public MemberInvite() { }

                public MemberInvite(IPlayer player)
                {
                    DisplayName = player.Name;
                    ExpiryTime = UnixTimeStampUTC();
                }

                public MemberInvite(string name)
                {
                    DisplayName = name;
                    ExpiryTime = UnixTimeStampUTC();
                }
            }

            [JsonIgnore]
            private JObject serializedClanObject;

            internal JObject ToJObject()
            {
                if (serializedClanObject != null)
                    return serializedClanObject;

                serializedClanObject = new JObject();
                serializedClanObject["tag"] = Tag;
                serializedClanObject["description"] = Description;
                serializedClanObject["owner"] = OwnerID;

                JArray jmoderators = new JArray();
                JArray jmembers = new JArray();

                foreach (KeyValuePair<string, Member> kvp in ClanMembers)
                {
                    if (kvp.Value.Role == Member.MemberRole.Moderator)
                        jmoderators.Add(kvp.Key);

                    jmembers.Add(kvp.Key);
                }

                serializedClanObject["moderators"] = jmoderators;
                serializedClanObject["members"] = jmembers;

                JArray jallies = new JArray();

                foreach (string ally in Alliances)
                    jallies.Add(ally);

                serializedClanObject["allies"] = jallies;

                JArray jinvallies = new JArray();

                foreach (KeyValuePair<string, double> ally in AllianceInvites)
                    jinvallies.Add(ally.Key);

                serializedClanObject["invitedallies"] = jinvallies;

                return serializedClanObject;
            }

            internal string FindPlayer(string partialNameOrID)
            {
                foreach (KeyValuePair<string, Member> kvp in ClanMembers)
                {
                    if (kvp.Key.Equals(partialNameOrID))
                        return kvp.Key;

                    if (kvp.Value.DisplayName.Contains(partialNameOrID, CompareOptions.OrdinalIgnoreCase))
                        return kvp.Key;
                }

                return string.Empty;
            }
        }
#endregion

#region Config        
        internal static ConfigData configData;

        internal class ConfigData
        {
            [JsonProperty(PropertyName = "Clan Options")]
            public ClanOptions Clans { get; set; }

            [JsonProperty(PropertyName = "Role Colors")]
            public ColorOptions Colors { get; set; }

            [JsonProperty(PropertyName = "Clan Tag Options")]
            public TagOptions Tags { get; set; }

            [JsonProperty(PropertyName = "Purge Options")]
            public PurgeOptions Purge { get; set; }

            [JsonProperty(PropertyName = "Settings")]
            public OtherOptions Options { get; set; }

            public class ClanOptions
            {
                [JsonProperty(PropertyName = "Member limit")]
                public int MemberLimit { get; set; }

                [JsonProperty(PropertyName = "Moderator limit")]
                public int ModeratorLimit { get; set; }

                [JsonProperty(PropertyName = "Alliance Options")]
                public AllianceOptions Alliance { get; set; }

                [JsonProperty(PropertyName = "Invite Options")]
                public InviteOptions Invites { get; set; }

                [JsonIgnore]
                public bool MemberFF => false;

                [JsonIgnore]
                public bool OwnerFF => false;

                public class AllianceOptions
                {
                    [JsonProperty(PropertyName = "Enable clan alliances")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Alliance limit")]
                    public int AllianceLimit { get; set; }

                    [JsonIgnore]
                    public bool AllyFF => false;

                    [JsonIgnore]
                    public bool OwnerFF => false;

                }

                public class InviteOptions
                {
                    [JsonProperty(PropertyName = "Maximum allowed member invites at any given time")]
                    public int MemberInviteLimit { get; set; }

                    [JsonProperty(PropertyName = "Member invite expiry time (seconds)")]
                    public int MemberInviteExpireTime { get; set; }

                    [JsonProperty(PropertyName = "Maximum allowed alliance invites at any given time")]
                    public int AllianceInviteLimit { get; set; }

                    [JsonProperty(PropertyName = "Alliance invite expiry time (seconds)")]
                    public int AllianceInviteExpireTime { get; set; }
                }
            }

            public class ColorOptions
            {
                [JsonProperty(PropertyName = "Clan owner color (hex)")]
                public string Owner { get; set; }

                [JsonProperty(PropertyName = "Clan moderator color (hex)")]
                public string Moderator { get; set; }

                [JsonProperty(PropertyName = "Clan member color (hex)")]
                public string Member { get; set; }

                [JsonProperty(PropertyName = "General text color (hex)")]
                public string TextColor { get; set; }
            }

            public class TagOptions
            {
                [JsonProperty(PropertyName = "Enable clan tags (requires BetterChat)")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Tag opening character")]
                public string TagOpen { get; set; }

                [JsonProperty(PropertyName = "Tag closing character")]
                public string TagClose { get; set; }

                [JsonProperty(PropertyName = "Tag color (hex)")]
                public string TagColor { get; set; }

                [JsonProperty(PropertyName = "Allow clan leaders to set custom tag colors (BetterChat only)")]
                public bool CustomColors { get; set; }

                [JsonProperty(PropertyName = "Tag size")]
                public int TagSize { get; set; }

                [JsonProperty(PropertyName = "Tag character limits")]
                public Range TagLength { get; set; }
            }

            public class PurgeOptions
            {
                [JsonProperty(PropertyName = "Enable clan purging")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Purge clans that havent been online for x amount of day")]
                public int OlderThanDays { get; set; }

                [JsonProperty(PropertyName = "List purged clans in console when purging")]
                public bool ListPurgedClans { get; set; }
            }

            public class OtherOptions
            {
                [JsonProperty(PropertyName = "Log clan and member changes")]
                public bool LogChanges { get; set; }

                [JsonProperty(PropertyName = "Data save interval (seconds)")]
                public int SaveInterval { get; set; }
            }

            public class Range
            {
                public int Minimum { get; set; }
                public int Maximum { get; set; }

                public Range() { }

                public Range(int minimum, int maximum)
                {
                    this.Minimum = minimum;
                    this.Maximum = maximum;
                }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Clans = new ConfigData.ClanOptions
                {
                    Alliance = new ConfigData.ClanOptions.AllianceOptions
                    {
                        AllianceLimit = 2,                        
                        Enabled = true
                    },
                    Invites = new ConfigData.ClanOptions.InviteOptions
                    {
                        AllianceInviteExpireTime = 86400,
                        AllianceInviteLimit = 2,
                        MemberInviteExpireTime = 86400,
                        MemberInviteLimit = 8
                    },                   
                    MemberLimit = 8,
                    ModeratorLimit = 2,                    
                },
                Colors = new ConfigData.ColorOptions
                {
                    Member = "#fcf5cb",
                    Moderator = "#74c6ff",
                    Owner = "#a1ff46",
                    TextColor = "#e0e0e0"
                },                
                Options = new ConfigData.OtherOptions
                {
                    LogChanges = false,
                    SaveInterval = 900,
                },               
                Purge = new ConfigData.PurgeOptions
                {
                    Enabled = true,
                    ListPurgedClans = true,
                    OlderThanDays = 14,
                },
                Tags = new ConfigData.TagOptions
                {                    
                    CustomColors = false,
                    Enabled = true,
                    TagClose = "]",
                    TagColor = "#aaff55",
                    TagLength = new ConfigData.Range(2, 5),
                    TagOpen = "[",
                    TagSize = 15,
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(0, 2, 0))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
#endregion

#region Data Management
        internal StoredData storedData;

        private DynamicConfigFile data;

        private void TimedSaveData()
        {
            timer.In(configData.Options.SaveInterval, () =>
            {
                SaveData();
                TimedSaveData();
            });
        }

        private void SaveData() => data.WriteObject(storedData);

        private void LoadData()
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("clan_data") && Interface.Oxide.DataFileSystem.ExistsDatafile("clans_data"))
            {
                DynamicConfigFile oldData = Interface.Oxide.DataFileSystem.GetFile("clans_data");

                Dictionary<string, OldClan> clanData = oldData.ReadObject<Dictionary<string, OldClan>>();
                if (clanData != null && clanData.Count > 0)
                    RestoreClanData(clanData);
            }
            else
            {
                data = Interface.Oxide.DataFileSystem.GetFile("clan_data");
                storedData = data.ReadObject<StoredData>();
                if (storedData == null)
                    storedData = new StoredData();
            }
        }

        private void RestoreClanData(Dictionary<string, OldClan> clanData)
        {
            data = Interface.Oxide.DataFileSystem.GetFile("clan_data");
            storedData = new StoredData();

            foreach (KeyValuePair<string, OldClan> kvp in clanData)
            {
                Clan clan = storedData.clans[kvp.Key] = new Clan();

                clan.Tag = kvp.Key;
                clan.OwnerID = kvp.Value.ownerID;
                clan.CreationTime = clan.LastOnlineTime = UnixTimeStampUTC();

                foreach (KeyValuePair<string, string> memberKVP in kvp.Value.members)
                {
                    Clan.Member.MemberRole role = kvp.Value.ownerID == memberKVP.Key ? Clan.Member.MemberRole.Owner :
                                                  kvp.Value.moderators.Contains(memberKVP.Key) ? Clan.Member.MemberRole.Moderator :
                                                  Clan.Member.MemberRole.Member;

                    clan.ClanMembers[memberKVP.Key] = new Clan.Member(role, memberKVP.Value);
                }

                foreach(string alliance in kvp.Value.clanAlliances)
                    clan.Alliances.Add(alliance);

                foreach (string allianceInvite in kvp.Value.invitedAllies)
                    clan.AllianceInvites[allianceInvite] = UnixTimeStampUTC();

                foreach (KeyValuePair<string, string> memberInvite in kvp.Value.invitedPlayers)
                {
                    clan.MemberInvites[memberInvite.Key] = new Clan.MemberInvite(memberInvite.Value);
                    storedData.AddPlayerInvite(memberInvite.Key, clan.Tag);
                }

                foreach (string incomingAlliance in kvp.Value.pendingInvites)
                    clan.IncomingAlliances.Add(incomingAlliance);
            }

            SaveData();
        }

        [Serializable]
        internal class StoredData
        {
            public Hash<string, Clan> clans = new Hash<string, Clan>();

            public Hash<string, List<string>> playerInvites = new Hash<string, List<string>>();

            [JsonIgnore]
            private Hash<string, string> playerLookup = new Hash<string, string>();

            internal Clan FindClan(string tag)
            {
                Clan clan;
                if (clans.TryGetValue(tag, out clan))
                    return clan;

                string lower = tag.ToLower();

                foreach (KeyValuePair<string, Clan> kvp in clans)
                {
                    if (kvp.Key.ToLower().Equals(lower))
                        return kvp.Value;
                }

                return null;
            }

            internal Clan FindClanByID(ulong playerId) => FindClanByID(playerId.ToString());

            internal Clan FindClanByID(string playerId)
            {
                string tag;
                if (!playerLookup.TryGetValue(playerId, out tag))
                    return null;

                return FindClan(tag);
            }

            internal Clan.Member FindMemberByID(ulong playerId) => FindMemberByID(playerId.ToString());

            internal Clan.Member FindMemberByID(string playerId)
            {
                Clan.Member member = null;
                FindClanByID(playerId)?.ClanMembers.TryGetValue(playerId, out member);
                return member;
            }

            internal void RegisterPlayer(string playerId, string tag) => playerLookup[playerId] = tag;

            internal void UnregisterPlayer(string playerId) => playerLookup.Remove(playerId);

            internal void AddPlayerInvite(string target, string tag)
            {
                List<string> invites;
                if (!playerInvites.TryGetValue(target, out invites))
                    invites = playerInvites[target] = new List<string>();

                if (!invites.Contains(tag))
                    invites.Add(tag);
            }

            internal void RevokePlayerInvite(string target, string tag)
            {
                List<string> invites;
                if (!playerInvites.TryGetValue(target, out invites))
                    return;

                invites.Remove(tag);

                if (invites.Count == 0)
                    playerInvites.Remove(target);
            }

            internal void OnInviteAccepted(string target, string tag)
            {
                List<string> invites;
                if (!playerInvites.TryGetValue(target, out invites))
                    return;

                for (int i = invites.Count - 1; i >= 0; i--)
                {
                    string t = invites[i];

                    if (!t.Equals(tag))
                        FindClan(t)?.MemberInvites.Remove(target);

                    invites.RemoveAt(i);
                }

                if (invites.Count == 0)
                    playerInvites.Remove(target);
            }

            internal void OnInviteRejected(string target, string tag)
            {
                List<string> invites;
                if (!playerInvites.TryGetValue(target, out invites))
                    return;

                invites.Remove(tag);

                if (invites.Count == 0)
                    playerInvites.Remove(target);
            }
        }
#endregion

#region Data Conversion
        public class OldClan
        {
            public string clanTag = string.Empty;
            public string ownerID = string.Empty;

            public List<string> moderators = new List<string>();
            public Dictionary<string, string> members = new Dictionary<string, string>();
            public List<string> clanAlliances = new List<string>();

            public Dictionary<string, string> invitedPlayers = new Dictionary<string, string>();
            public List<string> invitedAllies = new List<string>();
            public List<string> pendingInvites = new List<string>();
        }
#endregion

#region Localization
        private static string Message(string key, string playerId = null) => string.Format(COLORED_LABEL, configData.Colors.TextColor, Instance.lang.GetMessage(key, Instance, playerId));

        private readonly Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["Notification.ClanInfo.Title"] = "[#ffa500]Clans[/#]",
            ["Notification.ClanInfo.Tag"] = "\nClanTag: [#b2eece]{0}[/#]",
            ["Notification.ClanInfo.Description"] = "\nDescription: [#b2eece]{0}[/#]",
            ["Notification.ClanInfo.Online"] = "\nMembers Online: {0}",
            ["Notification.ClanInfo.Offline"] = "\nMembers Offline: {0}",
            ["Notification.ClanInfo.Established"] = "\nEstablished: [#b2eece]{0}[/#]",
            ["Notification.ClanInfo.LastOnline"] = "\nLast Online: [#b2eece]{0}[/#]",
            ["Notification.ClanInfo.Alliances"] = "\nAlliances: [#b2eece]{0}[/#]",
            ["Notification.ClanInfo.Alliances.None"] = "None",

            ["Notification.Create.InExistingClan"] = "You are already a member of a clan",
            ["Notification.Create.NoPermission"] = "You do not have permission to create a clan",
            ["Notification.Create.InvalidTagLength"] = "The tag you have chosen is invalid. It must be between {0} and {1} characters long",
            ["Notification.Create.ClanExists"] = "A clan with that tag already exists",
            ["Notification.Create.Success"] = "You have formed the clan [#aaff55][{0}][/#]",

            ["Notification.Kick.IsOwner"] = "You can not kick the clan owner",
            ["Notification.Kick.NoPermissions"] = "You do not have sufficient permission to kick clan members",
            ["Notification.Kick.NotClanmember"] = "The target is not a member of your clan",
            ["Notification.Kick.Self"] = "You can not kick yourself",
            ["Notification.Kick.NotEnoughRank"] = "Only the clan owner can kick another ranking member",
            ["Notification.Kick.NoPlayerFound"] = "Unable to find a player with the specified name of ID",
            ["Notification.Kick.Reply"] = "{0} kicked {1} from the clan!",
            ["Notification.Kick.PlayerMessage"] = "{0} kicked you from the clan!",
            ["Notification.Kick.NoPermission"] = "You do not have permission to kick clan members",

            ["Notification.Leave.Reply"] = "{0} has left the clan!",
            ["Notification.Leave.PlayerMessage"] = "You have left the clan [#aaff55][{0}][/#]!",
            ["Notification.Leave.NewOwner"] = "{0} is now the clan leader!",
            ["Notification.Leave.NoPermission"] = "You do not have permission to leave this clan",

            ["Notification.Join.NoPermission"] = "You do not have permission to join a clan",
            ["Notification.Join.ExpiredInvite"] = "Your invite to {0} has expired!",
            ["Notification.Join.InExistingClan"] = "You are already a member of another clan",
            ["Notification.Join.Reply"] = "{0} has joined the clan!",

            ["Notification.Invite.NoPermissions"] = "You do not have sufficient permissions to invite other players",
            ["Notification.Invite.InviteLimit"] = "You already have the maximum number of invites allowed",
            ["Notification.Invite.HasPending"] = "{0} all ready has a pending clan invite",
            ["Notification.Invite.IsMember"] = "{0} is already a clan member",
            ["Notification.Invite.InClan"] = "{0} is already a member of another clan",
            ["Notification.Invite.NoPermission"] = "{0} does not have the required permission to join a clan",
            ["Notification.Invite.SuccesTarget"] = "You have been invited to join the clan: [#aaff55][{0}][/#] '{1}'\nTo join, type: [#ffd479]/{2} accept {0}[/#]",
            ["Notification.Invite.SuccessClan"] = "{0} has invited {1} to join the clan",
            ["Notification.PendingInvites"] = "You have pending clan invites from: {0}\nYou can join a clan type: [#ffd479]/{1} accept <tag>[/#]",

            ["Notification.WithdrawInvite.NoPermissions"] = "You do not have sufficient permissions to withdraw member invites",
            ["Notification.WithdrawInvite.UnableToFind"] = "Unable to find a invite for the player with {0}",
            ["Notification.WithdrawInvite.Success"] = "{0} revoked the member invitation for {0}",

            ["Notification.RejectInvite.InvalidInvite"] = "You do not have a invite to join [#aaff55][{0}][/#]",
            ["Notification.RejectInvite.Reply"] = "{0} has rejected their invition to join your clan",
            ["Notification.RejectInvite.PlayerMessage"] = "You have rejected the invitation to join [#aaff55][{0}][/#]",

            ["Notification.Promotion.NoPermissions"] = "You do not have sufficient permissions to promote other players",
            ["Notification.Promotion.TargetNoClan"] = "{0} is not a member of your clan",
            ["Notification.Promotion.IsOwner"] = "You can not promote the clan leader",          
            ["Notification.Promotion.ModeratorLimit"] = "You already have the maximum amount of moderators",
            ["Notification.Promotion.IsModerator"] = "You can not promote higher than the rank of moderator",
            ["Notification.Promotion.Reply"] = "{0} was promoted to rank of {1} by {2}",

            ["Notification.Demotion.NoPermissions"] = "You do not have sufficient permissions to demote other players",
            ["Notification.Demotion.IsOwner"] = "You can not demote the clan leader",
            ["Notification.Demotion.IsMember"] = "{0} is already at the lowest rank",
            ["Notification.Demotion.Reply"] = "{0} was demoted to rank of {1} by {2}",

            ["Notification.Alliance.NoPermissions"] = "You do not have sufficient permissions to manage alliances",
            ["Notification.Alliance.PendingInvite"] = "[#aaff55][{0}][/#] already has a pending alliance invite",
            ["Notification.Alliance.MaximumInvites"] = "You already have the maximum amount of alliance invites allowed",
            ["Notification.Alliance.MaximumAlliances"] = "You already have the maximum amount of alliances formed",
            ["Notification.Alliance.InviteSent"] = "You have sent a clan alliance invitation to [#aaff55][{0}][/#]\nThe invitation will expire in: {1}",
            ["Notification.Alliance.InviteReceived"] = "You have received a clan alliance invitation from [#aaff55][{0}][/#]\nTo accept, type: [#ffd479]/{2} accept {0}[/#]\nThe invitation will expire in: {1}",
            ["Notification.Alliance.NoActiveInvite"] = "You do not have an active alliance invitation for [#aaff55][{0}][/#]",
            ["Notification.Alliance.NoActiveInviteFrom"] = "You do not have an active alliance invitation from [#aaff55][{0}][/#]",
            ["Notification.Alliance.WithdrawnClan"] = "{0} has withdrawn an alliance invitation to [#aaff55][{1}][/#]",
            ["Notification.Alliance.WithdrawnTarget"] = "[#aaff55][{0}][/#] has withdrawn their alliance invitation",
            ["Notification.Alliance.AtLimitTarget"] = "[#aaff55][{0}][/#] currently has the maximum amount of alliances allowed",
            ["Notification.Alliance.AtLimitSelf"] = "Your clan currently has the maximum amount of alliances allowed",
            ["Notification.Alliance.Formed"] = "[#aaff55][{0}][/#] has formed an alliance with [#aaff55][{1}][/#]",
            ["Notification.Alliance.Rejected"] = "[#aaff55][{0}][/#] has rejected calls to form an alliance with [#aaff55][{1}][/#]",
            ["Notification.Alliance.Revoked"] = "[#aaff55][{0}][/#] has revoked their alliance with [#aaff55][{1}][/#]",
            ["Notification.Alliance.NoActiveAlliance"] = "You do not currently have an alliance with [#aaff55][{0}][/#]",

            ["Notification.ClanHelp.NoClan"] = "\nAvailable Commands:\n[#ffd479]/{0} create <tag> \"description\"[/#] - Create a new clan\n[#ffd479]/{0} accept <tag>[/#] - Join a clan by invitation\n[#ffd479]/{0} reject <tag>[/#] - Reject a clan invitation",
            ["Notification.ClanHelp.Basic"] = "\nAvailable Commands:\n[#ffd479]/{0}[/#] - Display your clan information\n[#ffd479]/{1} <message>[/#] - Send a message via clan chat\n[#ffd479]/{0} leave[/#] - Leave your current clan",
            ["Notification.ClanHelp.Alliance"] = "\n\n[#45b6fe]<size=14>Alliance Commands:</size>[/#]\n[#ffd479]/{0} invite <tag>[/#] - Invite a clan to become allies\n[#ffd479]/{0} withdraw <tag>[/#] - Withdraw an alliance invitation\n[#ffd479]/{0} accept <tag>[/#] - Accept an alliance invitation\n[#ffd479]/{0} reject <tag>[/#] - Reject an alliance invitation\n[#ffd479]/{0} revoke <tag>[/#] - Revoke an alliance",
            ["Notification.ClanHelp.Moderator"] = "\n\n[#b573ff]<size=14>Moderator Commands:</size>[/#]\n[#ffd479]/{0} invite <name or ID>[/#] - Invite a player to your clan\n[#ffd479]/{0} withdraw <name or ID>[/#] - Revoke a invitation\n[#ffd479]/{0} kick <name or ID>[/#] - Kick a member from your clan",
            ["Notification.ClanHelp.Owner"] = "\n\n[#a1ff46]<size=14>Owner Commands:</size>[/#]\n[#ffd479]/{0} promote <name or ID>[/#] - Promote a clan member\n[#ffd479]/{0} demote <name or ID>[/#] - Demote a clan member\n[#ffd479]/{0} disband forever[/#] - Disband your clan",

            ["Notification.Clan.NotInAClan"] = "\nYou are currently not a member of a clan",
            ["Notification.Clan.Help"] = "\nTo see available commands type: [#ffd479]/{0}[/#]",
            ["Notification.Clan.OwnerOf"] = "\nYou are the owner of: [#aaff55]{0}[/#] ({1}/{2})",
            ["Notification.Clan.ModeratorOf"] = "\nYou are a moderator of: [#aaff55]{0}[/#] ({1}/{2})",
            ["Notification.Clan.MemberOf"] = "\nYou are a member of: [#aaff55]{0}[/#] ({1}/{2})",
            ["Notification.Clan.MembersOnline"] = "\nMembers Online: {0}",

            ["Notification.Clan.CreateSyntax"] = "[#ffd479]/{0} create <tag> \"description\"[/#] - Create a new clan",
            ["Notification.Clan.InviteSyntax"] = "[#ffd479]/{0} invite <partialNameOrID>[/#] - Invite a player to your clan",
            ["Notification.Clan.WithdrawSyntax"] = "[#ffd479]/{0} withdraw <partialNameOrID>[/#] - Revoke a member invitation",
            ["Notification.Clan.AcceptSyntax"] = "[#ffd479]/{0} accept <tag>[/#] - Join a clan by invitation",
            ["Notification.Clan.RejectSyntax"] = "[#ffd479]/{0} reject <tag>[/#] - Reject a clan invitation",
            ["Notification.Clan.PromoteSyntax"] = "[#ffd479]/{0} promote <partialNameOrID>[/#] - Promote a clanFreb member to the next rank",
            ["Notification.Clan.DemoteSyntax"] = "[#ffd479]/{0} demote <partialNameOrID>[/#] - Demote a clan member to the next lowest rank",
            ["Notification.Clan.DisbandSyntax"] = "[#ffd479]/{0} disband forever[/#] - Disband your clan (this can not be undone)",
            ["Notification.Clan.KickSyntax"] = "[#ffd479]/{0} kick <partialNameOrID>[/#] - Kick a member from your clan",

            ["Notification.Clan.TagColorSyntax"] = "<color=#ffd479>/{0} tagcolor <hex (XXXXXX)></color> - Set a custom clan tag color",
            ["Notification.Clan.TagColorFormat"] = "<color=#ffd479>The hex string must be 6 characters long, and be a valid hex color</color>",
            ["Notification.Clan.TagColorReset"] = "<color=#ffd479>You have reset your clan's tag color</color>",
            ["Notification.Clan.TagColorSet"] = "<color=#ffd479>You have set your clan's tag color to</color> <color=#{0}>{0}</color>",
            ["Notification.Clan.TagColorDisabled"] = "<color=#ffd479>Custom tag colors are disabled on this server</color>",

            ["Notification.Disband.NotOwner"] = "You must be the clan owner to use this command",
            ["Notification.Disband.Success"] = "You have disbanded the clan [#aaff55][{0}][/#]",
            ["Notification.Disband.Reply"] = "The clan has been disbanded",
            ["Notification.Disband.NoPermission"] = "You do not have permission to disband this clan",

            ["Notification.Generic.ClanFull"] = "The clan is already at maximum capacity",
            ["Notification.Generic.NoClan"] = "You are not a member of a clan",
            ["Notification.Generic.InvalidClan"] = "The clan [#aaff55][{0}][/#] does not exist!",
            ["Notification.Generic.NoPermissions"] = "You have insufficient permission to use that command",
            ["Notification.Generic.SpecifyClanTag"] = "Please specify a clan tag",
            ["Notification.Generic.UnableToFindPlayer"] = "Unable to find a player with the name or ID {0}",
            ["Notification.Generic.CommandSelf"] = "You can not use this command on yourself",

            ["Chat.Alliance.Prefix"] = "[#a1ff46][ALLY CHAT][/#]: {0}",
            ["Chat.Clan.Prefix"] = "[#a1ff46][CLAN CHAT][/#]: {0}",
            ["Chat.Alliance.Format"] = "[{0}] [{1}]{2}[/#]: {3}",
        };
        #endregion

        #region ClansUI  
        #if RUST
        public bool HasFFEnabled(ulong playerID) => false;

        public void ToggleFF(ulong playerID) { }

        internal bool PromotePlayer(BasePlayer promoter, ulong targetId) => PromotePlayer(promoter.IPlayer, targetId.ToString());

        internal bool DemotePlayer(BasePlayer demoter, ulong targetId) => DemotePlayer(demoter.IPlayer, targetId.ToString());

        internal bool KickPlayer(BasePlayer player, ulong targetId) => KickPlayer(player.IPlayer, targetId.ToString());

        internal bool InvitePlayer(BasePlayer player, ulong targetId) => InvitePlayer(player.IPlayer, targetId.ToString());

        internal bool WithdrawInvite(BasePlayer player, string partialNameOrID) => WithdrawInvite(player.IPlayer, partialNameOrID);

        internal bool OfferAlliance(BasePlayer player, string tag) => OfferAlliance(player.IPlayer, tag);

        internal bool RevokeAlliance(BasePlayer player, string tag) => RevokeAlliance(player.IPlayer, tag);

        internal bool WithdrawAlliance(BasePlayer player, string tag) => WithdrawAlliance(player.IPlayer, tag);

        internal bool RejectAlliance(BasePlayer player, string tag) => RejectAlliance(player.IPlayer, tag);

        internal bool AcceptAlliance(BasePlayer player, string tag) => AcceptAlliance(player.IPlayer, tag);

        internal bool LeaveClan(BasePlayer player) => LeaveClan(player.IPlayer);

        internal bool DisbandClan(BasePlayer player) => DisbandClan(player.IPlayer);
        #endif
        #endregion
    }

    namespace ClansEx
    {
        public static class StringExtensions
        {
            public static bool Contains(this string haystack, string needle, CompareOptions options)
            {
                return CultureInfo.InvariantCulture.CompareInfo.IndexOf(haystack, needle, options) >= 0;
            }
        }

        public static class ListPool
        {
            public static Dictionary<Type, object> directory = new Dictionary<Type, object>();

            public static void CreateCollection<T>(int capacity)
            {
                if (directory.ContainsKey(typeof(T)))
                    return;

                object obj = new ListCollection<T>(capacity);
                directory.Add(typeof(T), obj);
            }

            public static ListCollection<T> FindCollection<T>()
            {
                object obj;
                if (!directory.TryGetValue(typeof(T), out obj))
                {
                    obj = new ListCollection<T>();
                    directory.Add(typeof(T), obj);
                }

                return (ListCollection<T>)obj;
            }

            public static List<T> Get<T>() => GetList<List<T>>();
            
            public static List<T> Get<T>(int capacity)
            {
                List<T> list = GetList<List<T>>();
                list.Capacity = capacity;
                return list;
            }

            private static T GetList<T>() where T : class, new()
            {
                ListCollection<T> poolCollection = FindCollection<T>();
                if (poolCollection != null)
                {
                    if (poolCollection.stack.Count > 0)
                        return poolCollection.stack.Pop();
                }
                return Activator.CreateInstance<T>();
            }

            public static void Free<T>(ref List<T> list)
            {
                if (list == null)
                    return;

                list.Clear();

                FreeList<List<T>>(ref list);
            }

            private static void FreeList<T>(ref T t) where T : class
            {
                if (t == null)
                    return;

                ListCollection<T> poolCollection = FindCollection<T>();
                if (poolCollection != null && poolCollection.HasSpace)
                {
                    poolCollection.stack.Push(t);
                    t = default(T);
                }
                else
                {
                    t = null;
                }
            }

            public static void ClearPool()
            {
                directory.Clear();
            }

            public class ListCollection<T>
            {
                public Stack<T> stack;

                private readonly int maximumSize;

                public bool HasSpace { get { return stack.Count < maximumSize; } }

                public ListCollection(int maximumSize = 7)
                {
                    this.maximumSize = maximumSize;
                    stack = new Stack<T>(maximumSize + 1);
                }
            }
        }
    }
}
