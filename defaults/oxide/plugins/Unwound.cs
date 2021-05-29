using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Oxide.Core.Plugins;

/*
 * This update 1.0.12
 * Fixes economic issues
 * Finished old feature integration for PopUpNotifications plugin
 * Added new config option for setting custom health level
 * Updated auto updater to include new config variables.
 *
 * This update 1.0.20
 * Added ServerRewards Support (Updated variables/logic)
 * Updated Lang
 * Updated To OnPlayerWounded to save on performance rather than using OnEntityTakeDamage.
 */

namespace Oxide.Plugins
{
    [Info("Unwound", "mk_sky/Khan", "1.0.20")]
    [Description("The sky presents the newest technology in calling the MEDIC!")]

    class Unwound : RustPlugin
    {
        #region vars
        ListDictionary<string, string> localization;

        List<ulong> called = new List<ulong>();

        ListDictionary<uint, uint> ecoSettings;

        List<ulong> inUnwoundZone = new List<ulong>();

        float startHealth = 50;

        uint waitTillMedic = 10;

        bool popupsEnabled = false;

        uint chanceTheMedicSavesYou = 100;

        bool canCallMedicOncePerWounded = true;

        bool enableServerRewards = false;

        bool enableEconomics = true;
        
        bool enableCurrency = false;

        bool forceCurrency = false;

        [PluginReference]
        Plugin Economics, ZoneManager, PopupNotifications, ServerRewards;

        #endregion

        void OnServerInitialized()
        {
            ConfigLoader();
            permission.RegisterPermission("unwound.canuse", this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();

            Config["Version"] = Version.ToString();

            #region localization
            Config["Localization", "PermissionMissing"] = "You have no permission to use this command, if you're wounded right now it means you're probably screwed!";

            Config["Localization", "TheMedicIsComing"] = "The medic is coming for you ... that means if you can survive another {0} seconds.";

            Config["Localization", "NotWounded"] = "You're not wounded, get your extra shots somewhere else!";

            Config["Localization", "Survived"] = "The claws of death failed to claim you this time!";

            Config["Localization", "DontTrollTheMedic"] = "How dare you call the medic and then don't wait for him before staying up again!";

            Config["Localization", "AboutToDie"] = "You are about to die, use /aid to call for a medic.\n{0}";

            Config["Localization", "MedicToLate"] = "Seems like your medic found some free beer on the way and won't come in time now ... I think we have to cut his salary!";

            Config["Localization", "MedicIncompetent"] = "This incompetent troll of a medic is just to stupid to get you back up, we will get rid of him!";

            Config["Localization", "MedicAlreadyCalled"] = "You already called for a medic, just wait for him.";

            Config["Localization", "NotEnoughMoney"] = "You don't have enough money, how horrible ... You have {0} and you would need {1} so just wait the full {2} seconds for the medic.";

            Config["Localization", "NotEnoughMoney_ForcedEco"] = "You don't have enough money, how horrible ... You have {0} and you would need {1}, maybe I'll come to your funeral then.";
            #endregion
            #region settings
            Config["Settings", "StartHealth"] = 50f;
            
            Config["Settings", "WaitTillMedic"] = 10;

            Config["Settings", "ChanceTheMedicSavesYou"] = 100;

            Config["Settings", "CanCallMedicOncePerWounded"] = true;

            Config["Settings", "EnablePopups"] = false;

            Config["Settings", "EnableServerRewards"] = false;
            
            Config["Settings", "EnableEconomics"] = true;

            Config["Settings", "EnableCurrency"] = false;

            Config["Settings", "ForceCurrency"] = false;

            Config["EcoSettings", "10"] = 10;

            Config["EcoSettings", "100"] = 1;
            #endregion

            SaveConfig();

            PrintWarning("Unwound created a new config.");
        }

        void ConfigLoader()
        {
            base.LoadConfig();

            if (Config.Exists() &&
                Config["Version"].ToString() != Version.ToString())
                ConfigUpdater();

            #region localization
            localization = new ListDictionary<string, string>();

            localization.Add("PermissionMissing", Config["Localization", "PermissionMissing"].ToString());

            localization.Add("TheMedicIsComing", Config["Localization", "TheMedicIsComing"].ToString());

            localization.Add("NotWounded", Config["Localization", "NotWounded"].ToString());

            localization.Add("Survived", Config["Localization", "Survived"].ToString());

            localization.Add("DontTrollTheMedic", Config["Localization", "DontTrollTheMedic"].ToString());

            localization.Add("AboutToDie", Config["Localization", "AboutToDie"].ToString());

            localization.Add("MedicToLate", Config["Localization", "MedicToLate"].ToString());

            localization.Add("MedicIncompetent", Config["Localization", "MedicIncompetent"].ToString());

            localization.Add("MedicAlreadyCalled", Config["Localization", "MedicAlreadyCalled"].ToString());

            localization.Add("NotEnoughMoney", Config["Localization", "NotEnoughMoney"].ToString());

            localization.Add("NotEnoughMoney_ForcedEco", Config["Localization", "NotEnoughMoney_ForcedEco"].ToString());
            #endregion
            #region settings

            startHealth = Convert.ToSingle(Config["Settings", "StartHealth"])> 100 ? 100 : Convert.ToUInt32(Config["Settings", "StartHealth"]);

            if (startHealth == 0)
            {
                PrintError("The startHealth can't be 0, Plugin will run it with 1."); //still almost 0 but not 0

                startHealth = 1;
            }

            waitTillMedic = Convert.ToUInt32(Config["Settings", "WaitTillMedic"]);

            chanceTheMedicSavesYou = Convert.ToUInt32(Config["Settings", "ChanceTheMedicSavesYou"]) > 100 ? 100 : Convert.ToUInt32(Config["Settings", "ChanceTheMedicSavesYou"]);

            if (chanceTheMedicSavesYou == 0)
            {
                PrintError("The ChanceTheMedicSavesYou can't be 0, Plugin will run it with 1."); //still almost 0 but not 0

                chanceTheMedicSavesYou = 1;
            }

            canCallMedicOncePerWounded = Convert.ToBoolean(Config["Settings", "CanCallMedicOncePerWounded"]);

            if (PopupNotifications == null &&
                Convert.ToBoolean(Config["Settings", "EnablePopups"]))
                PrintError("PopupNotifications-Plugin missing, can't enable pop-ups. Get the plugin first: https://umod.org/plugins/popup-notifications/");
            else if (PopupNotifications != null &&
                     Convert.ToBoolean(Config["Settings", "EnablePopups"]))
                popupsEnabled = true;
            
            enableEconomics = Convert.ToBoolean(Config["Settings", "EnableEconomics"]);
                
            enableServerRewards = Convert.ToBoolean(Config["Settings", "EnableServerRewards"]);

            ecoSettings = new ListDictionary<uint, uint>();
            
            if (Convert.ToBoolean(Config["Settings", "EnableCurrency"]))
            {
                Puts("settings EnableCurrency is true");
                enableCurrency = true;

                forceCurrency = Convert.ToBoolean(Config["Settings", "ForceCurrency"]);

                Dictionary<string, string> temp = Config.Get<Dictionary<string, string>>("EcoSettings");

                foreach (KeyValuePair<string, string> s in temp)
                    if (Convert.ToUInt32(s.Value) >= 0)
                        ecoSettings.Add(Convert.ToUInt32(s.Key), Convert.ToUInt32(s.Value));

                Puts($"{ecoSettings.Count}");

                ecoSettings.Keys.Reverse();
            }
            if (Convert.ToBoolean(Config["Settings", "EnableEconomics"]) && Economics == null)
            {
                PrintError("Economics-Plugin missing, can't enable economics. Get the plugin first: https://umod.org/plugins/economics");
            }
            if (Convert.ToBoolean(Config["Settings", "EnableServerRewards"]) && ServerRewards == null)
            {
                PrintError("ServerRewards-Plugin missing, can't enable server rewards. Get the plugin first: https://umod.org/plugins/server-rewards");
            }
                
            #endregion
        }

        void ConfigUpdater()
        {
            PrintWarning(String.Format("Config updated from v{0} to v{1}.", Config["Version"].ToString(), this.Version.ToString()));

            while (Config["Version"].ToString() != this.Version.ToString())
                switch (Config["Version"].ToString())
                {
                    #region 1.0.0 => 1.0.1
                    case "1.0.0":
                        Config["Localization", "AboutToDie"] = "You are about to die, use /aid to call for a medic.";

                        Config["Localization", "MedicToLate"] = "Seems like your medic found some free beer on the way and won't come in time now ... I think we have to cut his salary!";

                        Config["Localization", "MedicIncompetent"] = "This incompetent troll of a medic is just to stupid to get you back up, we will get rid of him!";

                        Config["Localization", "MedicAlreadyCalled"] = "You already called for a medic, just wait for him.";

                        Config["Localization", "NotEnoughMoney"] = "You don't have enough money, how horrible ... You have {0} and you would need {1} so just wait the full {2} seconds for the medic.";

                        Config["Settings", "ChanceTheMedicSavesYou"] = 100;

                        Config["Settings", "CanCallMedicOncePerWounded"] = true;

                        Config["Settings", "EnablePopups"] = false;

                        Config["Settings", "EnableEconomics"] = false;

                        Config["EcoSettings", "500"] = 0;

                        Config["EcoSettings", "250"] = 5;

                        Config["Version"] = "1.0.1";
                        break;
                    #endregion
                    #region 1.0.1 || 1.0.2 || 1.0.3 || 1.0.4 || 1.0.5 || 1.0.6 || 1.0.7 => 1.0.8
                    case "1.0.1":
                    case "1.0.2":
                    case "1.0.3":
                    case "1.0.4":
                    case "1.0.5":
                    case "1.0.6":
                    case "1.0.7":
                        Config["Version"] = "1.0.8";
                        break;
                    #endregion
                    #region 1.0.8 => 1.0.9
                    case "1.0.8":
                        if (permission.PermissionExists("canuseunwound"))
                        {
                            string[] playersWithPermission = permission.GetPermissionUsers("canuseunwound");

                            foreach (string s in playersWithPermission)
                                if (permission.UserHasPermission(s.Substring(0, s.IndexOf('(')), "canuseunwound"))
                                {
                                    permission.RevokeUserPermission(s.Substring(0, s.IndexOf('(')), "canuseunwound");

                                    permission.GrantUserPermission(s.Substring(0, s.IndexOf('(')), "unwound.canuse", this);
                                }

                            string[] groupsWithPermission = permission.GetPermissionGroups("canuseunwound");

                            foreach (string s in groupsWithPermission)
                                if (permission.GroupHasPermission(s, "canuseunwound"))
                                {
                                    permission.RevokeGroupPermission(s, "canuseunwound");

                                    permission.GrantGroupPermission(s, "unwound.canuse", this);
                                }

                            permission.RemoveGroup("canuseunwound");
                        }

                        Config["Localization", "NotEnoughMoney_ForcedEco"] = "You don't have enough money, how horrible ... You have {0} and you would need {1}, maybe I'll come to your funeral then.";

                        Config["Settings", "EnablePopups"] = null;

                        Config["Settings", "ForceEconomics"] = false;

                        Config["Version"] = "1.0.9";
                        break;
                        #endregion
                    #region 1.0.11 => 1.0.12
                    case "1.0.11":

                        Config["Settings", "EnablePopups"] = false;
                        
                        Config["Settings", "StartHealth"] = 50;
                        
                        Config["Version"] = "1.0.12";
                        break;
                    #endregion
                    #region 1.0.12 => 1.0.20
                    case "1.0.12":

                        Config["Settings", "EnableServerRewards"] = false;
        
                        Config["Localization", "AboutToDie"] = "You are about to die, use /aid to call for a medic.\n{0}";

                        Config["Settings", "EnableCurrency"] = false;

                        Config["Settings", "ForceCurrency"] = false;
                            
                        Config["Version"] = "1.0.20";
                        break;
                    #endregion
                }

            SaveConfig();
        }

        [ConsoleCommand("unwound.recreate")]
        void ConsoleCommandConfigRecreate()
        {
            LoadDefaultConfig();

            ConfigLoader();
        }

        [ConsoleCommand("unwound.load")]
        void ConsoleCommandConfigLoad()
        {
            ConfigLoader();
        }

        [ConsoleCommand("unwound.set")]
        void ConsoleCommandConfigSet(ConsoleSystem.Arg arg)
        {
            if (IsUInt(arg.GetString(2)))
                Config[arg.GetString(0), arg.GetString(1)] = arg.GetUInt(2);
            else if (arg.GetString(2) == "true" ||
                     arg.GetString(2) == "false")
                Config[arg.GetString(0), arg.GetString(1)] = arg.GetBool(2);
            else
                Config[arg.GetString(0), arg.GetString(1)] = arg.GetString(2);

            SaveConfig();

            ConfigLoader();
        }

        [ChatCommand("aid")]
        void ChatCommandAid(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "unwound.canuse"))
            {
                if (!popupsEnabled)
                    SendReply(player, localization["PermissionMissing"]);
                else
                    PopupNotifications.Call("CreatePopupNotification", localization["PermissionMissing"], player);

                return;
            }
            if (!player.IsWounded())
            {
                if (!popupsEnabled)
                    SendReply(player, localization["NotWounded"]);
                else
                    PopupNotifications.Call("CreatePopupNotification", localization["NotWounded"], player);

                return;
            }

            if (canCallMedicOncePerWounded && waitTillMedic > 0 && !CheckCanCall(player))
                return;


            if (args.Length > 0 && args[0] == "0" || args.Length > 0 && IsUInt(args[0]) && !ecoSettings.Contains(Convert.ToUInt32(args[0])) && !enableCurrency && args[0] != "?")
            {
                args = new string[0];
            }

            double playerMoney = GetCurrency(player); // Economics.Call<double>("Balance", player.userID);

            if (args.Length == 0 && enableCurrency && forceCurrency || args.Length == 1 && args[0] == "?" && enableCurrency)
            {
                playerMoney = GetCurrency(player); // Economics.Call<double>("Balance", player.userID);
                uint test = 0;
                
                for (int i = 0; i < ecoSettings.Count; i++)
                    if (playerMoney >= ecoSettings.GetByIndex(i).Key && test < ecoSettings.GetByIndex(i).Key)
                    {
                        test = ecoSettings.GetByIndex(i).Key;
                    }

                    else if (i + 1 == ecoSettings.Count && test == 0 && forceCurrency)
                    {
                        test = ecoSettings.GetByIndex(0).Key;
                    }

                if (forceCurrency || test != 0)
                {
                    args = new string[1] { test.ToString() };
                }
                else if (args.Length == 1 && args[0] == "?")
                {
                    args = new string[0];
                }
            }

            if (waitTillMedic > 0 || enableCurrency && forceCurrency)
            {
                if (args.Length >= 1 && IsUInt(args[0]))
                {
                    if (playerMoney >= Convert.ToDouble(args[0]))
                    {
                        uint waittime = waitTillMedic;
                        if (ecoSettings.Contains(Convert.ToUInt32(args[0])))
                        {
                            Withdraw(player, Convert.ToDouble(args[0]));
                            
                            waittime = ecoSettings[Convert.ToUInt32(args[0])];
                        }
                        else if (!ecoSettings.Contains(Convert.ToUInt32(args[0])))
                        {
                            SendReply(player, $"Invalid aid amount, valid options are\n{string.Join("\n", ecoSettings.Select(x => $"Cost {x.Key}, Seconds until revived {x.Value}").ToArray())}");
                        }
                        //Economics.Call("Withdraw", player.userID, Convert.ToDouble(args[0]));

                        if (waittime > 0)
                        {
                            if (!popupsEnabled)
                                SendReply(player, String.Format(localization["TheMedicIsComing"], waittime.ToString()));
                            else
                                PopupNotifications.Call("CreatePopupNotification", String.Format(localization["TheMedicIsComing"], waittime.ToString()), player);

                            Action timed = new Action(() => TimedMedic(player.userID));

                            timer.In(waittime, timed);
                        }
                        else
                        {
                            called.Remove(player.userID);

                            if (!MedicGetsYouUp(player))
                            {
                                switch (Oxide.Core.Random.Range(0, 1))
                                {
                                    case 0:
                                        if (!popupsEnabled)
                                            SendReply(player, localization["MedicToLate"]);
                                        else
                                            PopupNotifications.Call("CreatePopupNotification", localization["MedicToLate"], player);
                                        break;
                                    case 1:
                                        if (!popupsEnabled)
                                            SendReply(player, localization["MedicIncompetent"]);
                                        else
                                            PopupNotifications.Call("CreatePopupNotification", localization["MedicIncompetent"], player);
                                        break;
                                }

                                return;
                            }

                            player.StopWounded();

                            player.health = startHealth;

                            player.metabolism.bleeding.value = 0f;

                            if (!popupsEnabled)
                                SendReply(player, localization["Survived"]);
                            else
                                PopupNotifications.Call("CreatePopupNotification", localization["Survived"], player);
                        }
                    }
                    else
                    {
                        if (!popupsEnabled)
                            SendReply(player, String.Format((forceCurrency ? localization["NotEnoughMoney_ForcedEco"] : localization["NotEnoughMoney"]), playerMoney.ToString(""), args[0], waitTillMedic.ToString()));
                        else
                            PopupNotifications.Call("CreatePopupNotification", String.Format((forceCurrency ? localization["NotEnoughMoney_ForcedEco"] : localization["NotEnoughMoney"]), playerMoney.ToString(""), args[0], waitTillMedic.ToString()), player);
                        
                        if (!forceCurrency)
                        {
                            Action timed = new Action(() => TimedMedic(player.userID));

                            timer.In(waitTillMedic, timed);
                        }
                        else
                            called.Remove(player.userID);
                    }
                }
                else if (!forceCurrency)
                {
                    Puts("is called inside the !forceCurrency");
                    if (!popupsEnabled)
                        SendReply(player, String.Format(localization["TheMedicIsComing"], waitTillMedic.ToString()));
                    else
                        PopupNotifications.Call("CreatePopupNotification", String.Format(localization["TheMedicIsComing"], waitTillMedic.ToString()), player);

                    Action timed = new Action(() => TimedMedic(player.userID));

                    timer.In(waitTillMedic, timed);
                }
            }
            else
            {
                if (!MedicGetsYouUp(player))
                {
                    switch (Oxide.Core.Random.Range(0, 1))
                    {
                        case 0:
                            if (!popupsEnabled)
                                SendReply(player, localization["MedicToLate"]);
                            else
                                PopupNotifications.Call("CreatePopupNotification", localization["MedicToLate"], player);
                            break;
                        case 1:
                            if (!popupsEnabled)
                                SendReply(player, localization["MedicIncompetent"]);
                            else
                                PopupNotifications.Call("CreatePopupNotification", localization["MedicIncompetent"], player);
                            break;
                    }

                    return;
                }

                player.StopWounded();
                
                player.health = startHealth;
                
                player.metabolism.bleeding.value = 0f;

                if (!popupsEnabled)
                    SendReply(player, localization["Survived"]);
                else
                    PopupNotifications.Call("CreatePopupNotification", localization["Survived"], player);
            }
        }

        void TimedMedic(ulong playerID)
        {
            BasePlayer player = BasePlayer.FindByID(playerID);

            if (player == null)
            {
                PrintWarning(String.Format("Unwound reports that the medic has arrived, but player \"{0}\" does not exist ...", playerID.ToString()));
                SendReply(player, String.Format("Unwound reports that the medic has arrived, but player \"{0}\" does not exist ...", playerID.ToString()));

                return;
            }
            else if (!player.IsConnected)
                return;
            else if (player.IsDead())
            {
                //TODO: code to enqueue a message for the player
                return;
            }
            else if (!player.IsWounded())
            {
                if (!popupsEnabled)
                    SendReply(player, localization["DontTrollTheMedic"]);
                else
                    PopupNotifications.Call("CreatePopupNotification", localization["DontTrollTheMedic"], player);

                return;
            }
            else if (!MedicGetsYouUp(player))
            {
                called.Remove(player.userID);

                switch (Oxide.Core.Random.Range(0, 1))
                {
                    case 0:
                        if (!popupsEnabled)
                            SendReply(player, localization["MedicToLate"]);
                        else
                           PopupNotifications.Call("CreatePopupNotification", localization["MedicToLate"], player);
                        break;
                    case 1:
                        if (!popupsEnabled)
                            SendReply(player, localization["MedicIncompetent"]);
                        else
                            PopupNotifications.Call("CreatePopupNotification", localization["MedicIncompetent"], player);
                        break;
                }

                return;
            }

            called.Remove(player.userID);

            player.StopWounded();

            player.health = startHealth;
            
            player.metabolism.bleeding.value = 0f;

            if (!popupsEnabled)
                SendReply(player, localization["Survived"]);
            else
                PopupNotifications.Call("CreatePopupNotification", localization["Survived"], player);
        }

        void OnPlayerWound(BasePlayer player, HitInfo hitInfo)
        {
            if (ZoneManager != null)
            {
                object canBeWounded = ZoneManager.Call("CanBeWounded", player, hitInfo);

                if (canBeWounded == null)
                    return;
            }
            
            if (!popupsEnabled)
                SendReply(player, string.Format(localization["AboutToDie"],string.Join("\n", ecoSettings.Select(x => $"Cost {x.Key}, Seconds until revived {x.Value}").ToArray())));
            else
                PopupNotifications.Call("CreatePopupNotification", string.Format(localization["AboutToDie"],string.Join("\n", ecoSettings.Select(x => $"Cost {x.Key}, Seconds until revived {x.Value}").ToArray())), player);
        }

        bool CheckCanCall(BasePlayer player)
        {
            if (called.Contains(player.userID))
            {
                if (!popupsEnabled)
                    SendReply(player, localization["MedicAlreadyCalled"]);
                else
                    PopupNotifications.Call("CreatePopupNotification", localization["MedicAlreadyCalled"], player);

                return false;
            }
            else
            {
                called.Add(player.userID);

                return true;
            }
        }

        bool MedicGetsYouUp(BasePlayer player)
        {
            if (chanceTheMedicSavesYou == 100)
                return true;

            uint success = 0;

            //with 100k-test-rounds this seemed pretty accurate ... well 99% chance is as failsafe as 100%, so not perfect but good enough

            for (int i = 0; i <= 100; i++)
                success += (uint)Oxide.Core.Random.Range(1, 100);

            success = success % 100;

            return success <= chanceTheMedicSavesYou;
        }

        static double ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);

            TimeSpan diff = date.ToUniversalTime() - origin;

            return Math.Floor(diff.TotalSeconds);
        }

        static bool IsUInt(string s)
        {
            Regex _uint = new Regex("^\\d*$");

            return _uint.Match(s).Success;
        }

        #region Currency System

        double GetCurrency(BasePlayer player)
        {
            if (enableEconomics && !enableServerRewards && Economics != null)
            {
                return Economics.Call<double>("Balance", player.UserIDString);
            }

            if (enableServerRewards && !enableEconomics && ServerRewards != null)
            {
                return ServerRewards.Call<int>("CheckPoints", player.UserIDString);
            }

            PrintWarning("Default currency not selected");
            return 0;
        }

        bool Withdraw(BasePlayer player, double amount)
        {
            if (enableEconomics && !enableServerRewards && Economics != null)
            {
                Puts("Economics called all good.");
                return Economics.Call<bool>("Withdraw", player.userID, amount);
            }

            if (enableServerRewards && !enableEconomics && ServerRewards != null)
            {
                Puts("Yo called fucking Server Rewards bitch");
                return ServerRewards.Call<object>("TakePoints", player.userID, (int)amount) != null;
            }

            Puts("Both failed.");
            return false;
        }

        #endregion
    }
}