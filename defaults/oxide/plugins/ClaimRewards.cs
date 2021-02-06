using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins

{
    [Info("Claim Rewards", "DutchKingCobra", "2.0.8")]
    [Description("Claim rewards after certain amount of kills.")]
    // caught Nullreference expcetion before givereward is called
    // added permission check to onentitydeath no need to count for those who lack permission
    // code cleanup added permission bool
    class ClaimRewards : RustPlugin
    {

        private PluginConfig config;
        private Dictionary<ulong, Dictionary<string, string>> PlayerData;
        Dictionary<string, string> PData;
        readonly Dictionary<string, string> items = new Dictionary<string, string>();
        #region Hooks
        private void Init()
        {
            Registerpermissions();

            config = Config.ReadObject<PluginConfig>();
            if (config == null)
            {
                LoadDefaultConfig();
            }

            if (Config["ChatCommand", "Rewards"] == null) //for ppl already using this plugin just add this ection to config!!!!
            {
                Config["ChatCommand", "Rewards"] = "rw";
                Config["ChatCommand", "Claim"] = "gimme";
                Config["ChatCommand", "Discard"] = "discard";
                Config["ChatCommand", "Nextreward"] = "nextreward";
                SaveConfig();
            }

            cmd.AddChatCommand(config.chatcommands.chatcmdreward, this, nameof(Cmdrw));
            cmd.AddChatCommand(config.chatcommands.chatcmdclaim, this, nameof(Cmdclaim));
            cmd.AddChatCommand(config.chatcommands.chatcmddiscard, this, nameof(Cmddiscard));
            cmd.AddChatCommand(config.chatcommands.chatcmdnextreward, this, nameof(Cmdnextreward));

            PlayerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, string>>>("ClaimRewardsPlayerData");
        }

        void OnServerInitialized()
        {
            foreach (ItemDefinition definition in ItemManager.itemList)
            {
                items.Add(definition.shortname, definition.displayName.english);
                // Interface.Oxide.DataFileSystem.WriteObject("items.dump", items);
            }
        }
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || entity == null) return;
            if (info.HitEntity == null || info.HitEntity.tag != "Player") return;

            var player = info.InitiatorPlayer;
            if (player == null) { return; }

            if (!permission.UserHasPermission(player.UserIDString, this.Name.ToLower() + ".use")) { return; } //dont count for players without perm.

            if (player.UserIDString.Length < 10) { return; } //don't count kills for bots we dont want the server giving them stuff :P

            IncKillsAndCheckIfReward(player);
        }
        object OnPlayerDeath(BasePlayer player)
        {
            if (config.conf.oneLife == true)
            {
                if (PlayerData.TryGetValue(player.userID, out PData))
                {
                    PData["mykills"] = "0";
                }
            }
            return null;
        }

        void OnServerSave()
        {
            SaveData();
        }
        private void Unload()
        {
            SaveData();
        }
        #endregion
        #region Permissions
        public void Registerpermissions()
        {
            permission.RegisterPermission(this.Name.ToLower() + ".use", this);
        }
        public bool HasPerm(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, this.Name.ToLower() + ".use"))
            {
                player.ChatMessage(msg("noperm", player.UserIDString));
                return false;
            }
            return true;
        }

        #endregion
        #region Subs
        private void IncKillsAndCheckIfReward(BasePlayer player)
        {
            int kills;
            if (!PlayerData.TryGetValue(player.userID, out PData))
            {
                PlayerData[player.userID] = PData = new Dictionary<string, string>();
                PData.Add("mykills", "0");
            }

            if (PlayerData.TryGetValue(player.userID, out PData))
            {

                if (int.TryParse(PData["mykills"], out kills))
                {
                    if (kills < config.conf.maxKills - 1)
                    {
                        kills += 1;
                        PData["mykills"] = kills.ToString();
                    }
                    else
                    {
                        PData["mykills"] = "1";
                        kills = 1;
                    }

                    //Check if Reward
                    if (Config["Rewards", PData["mykills"]] == null) { return; }

                    var reward = Config["Rewards", PData["mykills"]].ToString();

                    var rw = Getitemdetails(reward);

                    if (rw == null)
                    {
                        return;
                    }

                    if (config.conf.autoGiveRewards == true)
                    {
                        GiveReward(player, reward, kills.ToString());
                        return;
                    }

                    AddReward(player.userID, kills.ToString(), reward);

                    player.ChatMessage(msg("rewardadded", player.UserIDString, kills.ToString(), rw.Item3, rw.Item2));
                    player.ChatMessage(msg("crrewards", player.UserIDString));

                }
            }


        }
        private void AddReward(ulong userID, string atKill, string TheReward)
        {
            if (PlayerData.TryGetValue(userID, out PData))
            {
                if (!PData.ContainsKey(atKill))
                {
                    PData.Add(atKill, TheReward);
                }
            }
        }

        private void RemoveReward(ulong userID, string atKill)
        {
            if (PlayerData.TryGetValue(userID, out PData))
            {
                PData.Remove(atKill);
            }
        }
        private void Listrewards(BasePlayer player)
        {
            if (!PlayerData.TryGetValue(player.userID, out PData))
            {
                PlayerData[player.userID] = PData = new Dictionary<string, string>();
                PData.Add("mykills", "0");
            }

            if (PlayerData.TryGetValue(player.userID, out PData))
            {
                if (PData["mykills"] == "0" || PData.Count == 1)
                {
                    player.ChatMessage(msg("norewards", player.UserIDString));
                    return;
                }
                var count = 1;
                foreach (KeyValuePair<string, string> item in PData)
                {
                    if (item.Key != "mykills" && count < 9)
                    {
                        var rw = Getitemdetails(item.Value);
                        if (rw == null)
                        {
                            return;
                        }

                        player.ChatMessage(msg("rwlistitem", player.UserIDString, item.Key, rw.Item3, rw.Item2));
                        count += 1;
                    }
                }
                player.ChatMessage(msg("rwlistsuccess", player.UserIDString));
            }
        }
        public void GiveReward(BasePlayer player, string reward, string str)
        {
            if (player.inventory.containerMain.IsFull())
            {
                player.ChatMessage(msg("invfull", player.UserIDString));

                AddReward(player.userID, str, reward);

                var rw = Getitemdetails(reward);

                if (rw == null)
                {
                    return;
                }

                player.ChatMessage(msg("rewardadded", player.UserIDString, str, rw.Item3, rw.Item2));
                player.ChatMessage(msg("crrewards", player.UserIDString));
                return;
            }
            else
            {
                GiveClaimedReward(player, reward, str);
            }

        }

        public void GiveClaimedReward(BasePlayer player, string reward, string kills)
        {
            if (player.inventory.containerMain.IsFull())
            {
                player.ChatMessage(msg("invfull", player.UserIDString));
                return;
            }

            try
            {
                if (reward == "recycler.give")
                {
                    Server.Command(reward + " " + player.userID);
                }
                else
                {

                    var rw = Getitemdetails(reward);

                    if (rw == null)
                    {
                        return;
                    }

                    int amountx;
                    int.TryParse(rw.Item2, out amountx);
                    player.inventory.GiveItem(ItemManager.CreateByName(rw.Item1, amountx));
                    player.ChatMessage(msg("rwreceived", player.UserIDString, rw.Item3, amountx));
                }
                RemoveReward(player.userID, kills);
                return;
            }
            catch (Exception ex)
            {
                Puts(ex.Message);
                throw;
            }

        }

        public Tuple<string, string, string> Getitemdetails(string sname)
        {
            string fname;
            string amount;
            if (sname != "recycler.give")
            {
                if (sname.IndexOf(' ') == -1)
                {
                    PrintError("bad format in ClaimRewards.json at string: " + sname + " Use item.shortname and quantity EX: 'metal.facemask 1' ");
                    return null;
                }
                else
                {
                    int stind = sname.IndexOf(' ');
                    amount = sname.Substring(stind, sname.Length - stind);
                    sname = sname.Substring(0, stind);

                    if (!items.ContainsKey(sname))
                    {
                        PrintError("Bad shortname in ClaimRewards.json at string: " + sname);
                        return null;
                    }
                    else
                    {
                        items.TryGetValue(sname, out fname);
                    }
                }
            }
            else
            {
                amount = "1";
                fname = "Recycler";
            }

            return new Tuple<string, string, string>(sname, amount, fname);
        }

        #endregion
        #region Config rewards
        protected override void LoadDefaultConfig()
        {
            Config["Config", "OneLife"] = false;
            Config["Config", "AutoGiveRewardsUnlessFullInventory"] = true;
            Config["Config", "MaxKillsBeforeReset"] = 35;

            Config["ChatCommand", "Rewards"] = "rw";
            Config["ChatCommand", "Claim"] = "gimme";
            Config["ChatCommand", "Discard"] = "discard";
            Config["ChatCommand", "Nextreward"] = "nextreward";

            Config["Rewards", "10"] = "bow.hunting 1";
            Config["Rewards", "20"] = "wood 250";
            Config["Rewards", "30"] = "easter.goldegg 1";
        }
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Config")]
            public ConfigSettings conf = new ConfigSettings();

            [JsonProperty(PropertyName = "ChatCommand")]
            public chatSettings chatcommands = new chatSettings();

            public class ConfigSettings
            {
                [JsonProperty(PropertyName = "OneLife")]
                public bool oneLife = false;

                [JsonProperty(PropertyName = "AutoGiveRewardsUnlessFullInventory")]
                public bool autoGiveRewards = true;

                [JsonProperty(PropertyName = "MaxKillsBeforeReset")]
                public int maxKills = 35;
            }
            public class chatSettings
            {
                [JsonProperty(PropertyName = "Rewards")]
                public string chatcmdreward = "rw";

                [JsonProperty(PropertyName = "Claim")]
                public string chatcmdclaim = "gimme";

                [JsonProperty(PropertyName = "Discard")]
                public string chatcmddiscard = "discard";

                [JsonProperty(PropertyName = "Nextreward")]
                public string chatcmdnextreward = "nextreward";

            }
        }

        #endregion
        #region Save Data
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ClaimRewardsPlayerData", PlayerData);
        }
        #endregion
        #region Language
        private string msg(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["rewardadded"] = "Reward ({0}) {1} x {2} added to claim list.",
                ["crrewards"] = "Use /rw or /rw help.",
                ["norewards"] = "No Rewards to claim.",
                ["rwlistitem"] = "Reward ({0}) {1} x {2}",
                ["rwlistsuccess"] = "Showing max 8 results. Use /gimme <nr> or /discard <nr>",
                ["rwreceived"] = "You received {0} x {1}",
                ["invfull"] = "Unable to give inventory full.",
                ["noperm"] = "You don't have permission to use this command!",
                ["rwhelpcr"] = "Use /rw to see what u can claim.",
                ["rwhelpclaimcr"] = "Use /gimme <nr> to claim a reward.",
                ["rwhelpdiscardcr"] = "Use /discard <nr> to dispose a reward.",
                ["rwhelpnextcr"] = "Use /nextreward to see your kills and next reward.",
                ["noclaims"] = "No rewards to claim, you have 0 kills.",
                ["crclaim"] = "Use /gimme <nr>",
                ["badrwnumber"] = "No reward with nr:{0}",
                ["norwdiscard"] = "No rewards to discard, you have 0 kills.",
                ["discard"] = "Use /discard <nr>",
                ["discarded"] = "Discarded {0}",
                ["nodiscard"] = "Unable to discard:{0} not in list!",
                ["nextrwinfo"] = "You have {0} kills next reward in {1} kills {2} x {3}",
                ["nonext"] = "You have {0}, you need {1} kills to reset and start again."
            }, this);
        }

        #endregion
        #region Chat Commands
        private void Cmdrw(BasePlayer player, string command, string[] args)
        {

            if (!HasPerm(player)) { return; }

            if (args.Length == 1 && args[0] == "help")
            {
                player.ChatMessage(msg("rwhelpcr", player.UserIDString));
                player.ChatMessage(msg("rwhelpclaimcr", player.UserIDString));
                player.ChatMessage(msg("rwhelpdiscardcr", player.UserIDString));
                player.ChatMessage(msg("rwhelpnextcr", player.UserIDString));
                return;
            }
            else
            {
                Listrewards(player);
            }
        }


        private void Cmdclaim(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player)) { return; }

            if (!PlayerData.TryGetValue(player.userID, out PData))
            {
                PlayerData[player.userID] = PData = new Dictionary<string, string>();
                PData.Add("mykills", "0");
                player.ChatMessage(msg("noclaims", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage(msg("crclaim", player.UserIDString));
                return;
            }

            if (args.Length == 1)
            {
                var atKill = args[0];
                int value;
                if (int.TryParse(atKill, out value))
                {

                    if (PlayerData.TryGetValue(player.userID, out PData))
                    {

                        if (PData.ContainsKey(atKill))
                        {
                            var reward = PData[atKill];
                            GiveClaimedReward(player, reward, atKill);
                            return;
                        }
                        player.ChatMessage(msg("badrwnumber", player.UserIDString, atKill));
                        return;
                    }

                }
                player.ChatMessage(msg("crclaim", player.UserIDString));
            }

        }


        private void Cmddiscard(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player)) { return; }

            if (!PlayerData.TryGetValue(player.userID, out PData))
            {
                PlayerData[player.userID] = PData = new Dictionary<string, string>();
                PData.Add("mykills", "0");
                player.ChatMessage(msg("norwdiscard", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage(msg("discard", player.UserIDString));
                return;
            }

            if (args.Length == 1)
            {
                var str = args[0];
                int value;
                if (int.TryParse(str, out value))
                {

                    if (PlayerData.TryGetValue(player.userID, out PData))
                    {

                        if (PData.ContainsKey(str))
                        {
                            var rw = Getitemdetails(PData[str]);

                            if (rw == null)
                            {
                                return;
                            }
                            player.ChatMessage(msg("discarded", player.UserIDString, rw.Item3));
                            PData.Remove(str);
                            Listrewards(player);
                            return;
                        }

                    }
                    player.ChatMessage(msg("nodiscard", player.UserIDString, str));
                    return;
                }
                player.ChatMessage(msg("discard", player.UserIDString));
            }
        }


        private void Cmdnextreward(BasePlayer player)
        {

            if (!HasPerm(player)) { return; }

            if (!PlayerData.TryGetValue(player.userID, out PData))
            {
                PlayerData[player.userID] = PData = new Dictionary<string, string>();
                PData.Add("mykills", "0");
            }


            var it = 0;
            var nrofkills = 0;
            if (PlayerData.TryGetValue(player.userID, out PData))
            {

                if (int.TryParse(PData["mykills"], out nrofkills))
                {

                    nrofkills += 1;

                    for (int ic = nrofkills; ic < config.conf.maxKills; ic++)
                    {
                        it += 1;

                        if (Config["Rewards", ic.ToString()] != null)
                        {
                            var reward = (string)Config["Rewards", ic.ToString()];
                            var numkills = nrofkills - 1;

                            var rw = Getitemdetails(reward);

                            if (rw == null)
                            {
                                return;
                            }

                            player.ChatMessage(msg("nextrwinfo", player.UserIDString, numkills.ToString(), it.ToString(), rw.Item3, rw.Item2));
                            return;
                        }
                    }

                }

            }

            player.ChatMessage(msg("nonext", player.UserIDString, nrofkills, config.conf.maxKills));
            //return;
        }
        #endregion
        #region ConsoleCommands
        [ConsoleCommand("vcr")]
        void vcrConsole(ConsoleSystem.Arg arg)
        {
            var errs = 0;
            for (int ivcr = 1; ivcr < config.conf.maxKills; ivcr++)
            {

                if (Config["Rewards", ivcr.ToString()] == null)
                {

                }
                else
                {
                    var chkstr = Config["Rewards", ivcr.ToString()].ToString();
                    string amount;
                    if (chkstr != "recycler.give")
                    {
                        if (chkstr.IndexOf(' ') == -1)
                        {
                            PrintError("bad format in ClaimRewards.json at string: " + chkstr + " Usage EX: 'metal.facemask 1' ");
                            errs += 1;
                        }
                        else
                        {
                            int stind = chkstr.IndexOf(' ');
                            amount = chkstr.Substring(stind, chkstr.Length - stind);
                            chkstr = chkstr.Substring(0, stind);

                            if (!items.ContainsKey(chkstr))
                            {
                                PrintError("Bad shortname in ClaimRewards.json at string: " + chkstr);
                                errs += 1;
                            }
                        }
                    }
                }
            }

            if (errs == 0)
            {
                PrintWarning("Validation done!");
                return;
            }
            PrintWarning("Found " + errs + " bad item(s) in config.");
        }
        #endregion

    }
}