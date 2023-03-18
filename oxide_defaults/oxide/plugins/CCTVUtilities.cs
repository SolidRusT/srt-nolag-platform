using Oxide.Core.Libraries;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Oxide.Core.Configuration;
using Oxide.Core;

namespace Oxide.Plugins
{
    /*  Copyright 2020, GrumpyGordon
     * 
     *  This software is licensed & protected under the MIT Copyright License (1988)
     * 
     *  Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the
     *  Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software,
     *  and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
     * 
     *  The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
     * 
     *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
     *  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
     *  ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
     *  THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
     * 
     */

    [Info("CCTV Utilities", "GrumpyGordon","1.5.1")]
    [Description("Allows players to quickly add all their cameras to a computer station in Rust. With additional permissions they can add all the servers cameras to their CCTV setup.")]
    public class CCTVUtilities : RustPlugin
    {
        void Init()
        {
            permission.RegisterPermission("cctvutilities.help", this);
            permission.RegisterPermission("cctvutilities.rename", this);
            permission.RegisterPermission("cctvutilities.add.me", this);
            permission.RegisterPermission("cctvutilities.add.server", this);
            permission.RegisterPermission("cctvutilities.add.custom", this);
            permission.RegisterPermission("cctvutilities.add.all", this);
            permission.RegisterPermission("cctvutilities.clear", this);
            permission.RegisterPermission("cctvutilities.status.me", this);
            permission.RegisterPermission("cctvutilities.status.server", this);
            permission.RegisterPermission("cctvutilities.status.custom", this);
            permission.RegisterPermission("cctvutilities.status.all", this);
            permission.RegisterPermission("cctvutilities.autoname", this);
            permission.RegisterPermission("cctvutilities.autoadd", this);
            permission.RegisterPermission("cctvutilities.autoadd.on", this);
            permission.RegisterPermission("cctvutilities.autoadd.off", this);
            permission.RegisterPermission("cctvutilities.autoadd.toggle", this);
            permission.RegisterPermission("cctvutilities.autoadd.me", this);
            permission.RegisterPermission("cctvutilities.autoadd.server", this);
            permission.RegisterPermission("cctvutilities.autoadd.custom", this);
            permission.RegisterPermission("cctvutilities.autoadd.all", this);
            permission.RegisterPermission("cctvutilities.autopowered", this);
            EnsureCustomConfigExists();
            LoadDefaultMessages();
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPerm"] = "You don't have permission to perform this action",
                ["Error"] = "An error has occured! {0}",
                ["Incomplete"] = "This command is not yet complete!",
                ["ArgNotRecognised"] = "Argument is not recognised! Please try again with: {0} instead.",
                ["CheckConsole"] = "Hi {0}! Please check the console (F1) for command output!",
                //
                ["HelpAPI"] = "<color=#ff9a00ff>CCTV Utilities</color> /cctv help - Get help with CCTV Utilities",
                //
                ["Help1"] = "Help for <color=#ff9a00ff>CCTV Utilities</color> commands",
                ["Help2"] = "<color=#96ff94ff>/cctv help</color> - Sends help information.",
                ["Help3"] = "<color=#96ff94ff>/cctv status</color> <color=#ffd799ff><me | server | custom | all></color> - Prints a list of all cameras (in selected category)  to console.",
                ["Help4"] = "<color=#96ff94ff>/cctv add</color> <color=#ffd799ff><me | server | custom | all></color> - Adds cameras in selected category to a the computer station you're currently looking at.",
                ["Help5"] = "<color=#96ff94ff>/cctv clear</color> - Clears cameras from the computer station you're currently looking at.",
                ["Help6"] = "<color=#96ff94ff>/cctv rename</color> <color=#ffd799ff><Camera ID> <New Name></color> - Rename a camera identifier remotely.",
                ["Help7"] = "<color=#96ff94ff>/cctv about</color> - Provides plugin information.",
                ["Help8"] = "<color=#96ff94ff>/cctv autoname</color> <color=#ffd799ff><on | off | toggle></color> - Automatically names cameras as they are placed.",
                ["Help9"] = "<color=#96ff94ff>/cctv autoadd</color> <color=#ffd799ff><on | off | me | server | custom | all | toggle></color> - Automatically adds cameras to the computer station when placed.",
                ["Help10"] = "<color=#96ff94ff>/cctv autopowered</color> <color=#ffd799ff><on | off | toggle></color> - Automatically powers cameras when placed / server restarts.",
                //
                ["AddMe"] = "Successfully added all your cameras to this computer station!",
                ["AddServer"] = "Successfully added all Server cameras to this computer station!",
                ["AddCustom"] = "Successfully added all Custom cameras to this computer station!",
                ["AddAll"] = "Successfully added all cameras to this computer station!",
                ["WarnInactive"] = "Please bare in mind some may be inactive.",
                //
                ["Clear"] = "All cameras have been cleared from this computer station.",
                //
                ["IdentifierUpdated"] = "Camera name successfully changed from {0} to {1}",
                //
                ["DetailAll"] = "All Cameras",
                ["DetailAllHeader"] = "Camera ID: | <color=#00d619ff>Online</color> / <color=#ff0000ff>Offline</color> | Owner(OwnerSteamID)",
                ["DetailAllServer"] = "{0} | <color=#00d619ff>Online</color> | Server",
                ["DetailAllPlayerOnline"] = "{0} | <color=#00d619ff>Online</color> | {1}({2})",
                ["DetailAllPlayerOffline"] = "{0} | <color=#ff0000ff>Offline</color> | {1}({2})",
                //
                ["DetailServer"] = "All Server Owned Cameras",
                ["DetailCustom"] = "All Custom Server Owned Cameras",
                ["DetailPlayer"] = "All Player Owned Cameras",
                ["DetailUniversalHeader"] = "Camera ID: | <color=#00d619ff>Online</color> / <color=#ff0000ff>Offline</color>",
                ["DetailUniversalOnline"] = "{0} | <color=#00d619ff>Online</color>",
                ["DetailUniversalOffline"] = "{0} | <color=#00d619ff>Online</color>",
                //
                ["AboutHeader"] = "About <color=#ff9a00ff>CCTV Utilities</color>",
                ["AboutDescription"] = "Description: {0}",
                ["AboutAuthor"] = "Author: GrumpyGordon (Discord: GrumpyGordon#9728) (Web: grumpygordon.modded.org)",
                ["AboutVersion"] = "Version: {0}",
                ["AboutLicense"] = "License: This plugin is protected under the MIT License (1988)",
                //
                ["NoEntity"] = "This is not a valid entity! (Are you too far away?)",
                ["WrongEntity"] = "Please make sure you are looking at a {0}! (Are you too far away?)",
                //
                ["CamDown"] = "Camera powered down.",
                ["CamUp"] = "Camera powered up.",
                //
                ["CamNotOwner"] = "You do not own this camera!",
                ["CamNotOwnerConsole"] = "{0} tried to interact with other players CCTV: {1} owned by {2}({3})",
                ["CamNotFound"] = "We failed to find the camera you were looking for. Please ensure you have typed it correctly and try again.",
                //
                ["AutoNameOn"] = "Cameras will now be automatically named.",
                ["AutoNameOff"] = "Cameras will no longer be automatically named.",
                ["AutoNameToggleOn"] = "Camera auto naming toggled on.",
                ["AutoNameToggleOff"] = "Camera auto naming toggled off.",
                //
                ["AutoNameCamPlaced"] = "Camera placed and automatically named: {0}",
                ["AutoPoweredCamPlaced"] = "Camera placed and automatically powered",
                ["AutoNamePoweredCamPlaced"] = "Camera placed and automatically powered and named: {0}",
                //
                ["AutoAddOn"] = "Your cameras will be automatically added to computer stations when placed!",
                ["AutoAddOff"] = "Your cameras will no longer be automatically added to computer stations when placed!",
                ["AutoAddMe"] = "Your cameras will be automatically added to computer stations when placed!",
                ["AutoAddServer"] = "Server cameras will be automatically added to computer stations when placed!",
                ["AutoAddCustom"] = "Custom server cameras will be automatically added to computer stations when placed!",
                ["AutoAddAll"] = "All cameras will be automatically added to computer stations when placed!",
                ["AutoAddToggleOn"] = "Computer station auto adding toggled on.",
                ["AutoAddToggleOff"] = "Computer station auto adding toggled on.",
                //
                ["AutoPoweredOn"] = "Cameras will now be automatically powered on",
                ["AutoPoweredOff"] = "Cameras will no longer be automatically powered on",
                ["AutoPoweredToggleOn"] = "Camera auto powering toggled on.",
                ["AutoPoweredToggleOff"] = "Camera auto powering toggled off.",
                //
                ["DefaultMsg"] = "This message is here because commas."
            }, this);
        }

        void EnsureCustomConfigExists()
        {
            try
            {
                Interface.Oxide.DataFileSystem.ReadObject<string[]>("CCTVUtilities\\CustomCameras");
            }
            catch
            {
                CCTV_RC[] cams = GetServerCCTVs();
                string[] names = new string[cams.Length];
                for (int i = 0; i < cams.Length; i++)
                {
                    names[i] = cams[i].rcIdentifier;
                }
                Interface.Oxide.DataFileSystem.WriteObject("CCTVUtilities\\CustomCameras", names);
                Puts("Created Custom Cameras List using default server cameras.");
            }
        }

        //API to work with Help-Text Plugin (https://umod.org/plugins/help-text)
        private void SendHelpText(BasePlayer ply)
        {
            if (ply == null || ply.userID == 0)
            {
                return;
            }

            if (ply.IPlayer.HasPermission("cctvutilities.help"))
            {
                SendLangMsg(ply, "HelpAPI");
            }
        }

        void SendLangMsg(BasePlayer ply, string key)
        {
            ply.IPlayer.Reply(lang.GetMessage(key, this, ply.IPlayer.Id));
        }
        void SendLangMsg(BasePlayer ply, string key, string arg0)
        {
            ply.IPlayer.Reply(string.Format(lang.GetMessage(key, this, ply.IPlayer.Id), arg0));
        }
        void SendLangMsg(BasePlayer ply, string key, string arg0, string arg1)
        {
            ply.IPlayer.Reply(string.Format(lang.GetMessage(key, this, ply.IPlayer.Id), arg0, arg1));
        }
        void SendLangMsgConsole(BasePlayer ply, string key)
        {
            ply.IPlayer.Command("echo", lang.GetMessage(key, this, ply.IPlayer.Id));
        }
        void SendLangMsgConsole(BasePlayer ply, string key, string arg0)
        {
            ply.IPlayer.Command("echo", string.Format(lang.GetMessage(key, this, ply.IPlayer.Id), arg0));
        }
        void SendLangMsgConsole(BasePlayer ply, string key, string arg0, string arg1, string arg2)
        {
            ply.IPlayer.Command("echo", string.Format(lang.GetMessage(key, this, ply.IPlayer.Id), arg0, arg1, arg2));
        }

        public void SendHelpChat(BasePlayer ply)
        {
            if (ply.IPlayer.HasPermission("cctvutilities.help"))
            {
                SendLangMsg(ply, "Help1");
                SendLangMsg(ply, "Help2");
                if (ply.IPlayer.HasPermission("cctvutilities.status.me") || ply.IPlayer.HasPermission("cctvutilities.status.server") || ply.IPlayer.HasPermission("cctvutilities.status.custom") || ply.IPlayer.HasPermission("cctvutilities.status.all"))
                {
                    SendLangMsg(ply, "Help3");
                }
                if (ply.IPlayer.HasPermission("cctvutilities.add.me") || ply.IPlayer.HasPermission("cctvutilities.add.server") || ply.IPlayer.HasPermission("cctvutilities.add.custom") || ply.IPlayer.HasPermission("cctvutilities.add.all"))
                {
                    SendLangMsg(ply, "Help4");
                }
                if (ply.IPlayer.HasPermission("cctvutilities.clear"))
                {
                    SendLangMsg(ply, "Help5");
                }
                if (ply.IPlayer.HasPermission("cctvutilities.rename"))
                {
                    SendLangMsg(ply, "Help6");
                }
                SendLangMsg(ply, "Help7");
                if (ply.IPlayer.HasPermission("cctvutilities.autoname"))
                {
                    SendLangMsg(ply, "Help8");
                }
                if (ply.IPlayer.HasPermission("cctvutilities.autoadd.off") || ply.IPlayer.HasPermission("cctvutilities.autoadd.on") || ply.IPlayer.HasPermission("cctvutilities.autoadd.toggle") || ply.IPlayer.HasPermission("cctvutilities.autoadd.me") || ply.IPlayer.HasPermission("cctvutilities.autoadd.server") || ply.IPlayer.HasPermission("cctvutilities.autoadd.custom") || ply.IPlayer.HasPermission("cctvutilities.autoadd.all"))
                {
                    SendLangMsg(ply, "Help9");
                }
                if (ply.IPlayer.HasPermission("cctvutilities.autopowered"))
                {
                    SendLangMsg(ply, "Help10");
                }
            }
        }


        [ChatCommand("cctv")]
        void CCTVCommand(BasePlayer ply, string cctv, string[] args)
        {
            if (0 < args.Length)
            {
                args[0] = args[0].ToLower();
                switch (args[0])
                {
                    case "help":
                        if (ply.IPlayer.HasPermission("cctvutilities.help"))
                        {
                            SendHelpChat(ply);
                        }
                        else
                        {
                            SendLangMsg(ply, "NoPerm");
                        }
                        break;
                    case "rename":
                        if (ply.IPlayer.HasPermission("cctvutilities.rename"))
                        {
                            if(2 < args.Length)
                            {
                                if(GetCCTVAuth(args[1], ply) != null)
                                {
                                    CCTV_RC cam = GetCCTVAuth(args[1], ply);
                                    cam.UpdateIdentifier(args[2]);
                                    SendLangMsg(ply, "IdentifierUpdated", args[1], args[2]);
                                }                                
                            }
                            else
                            {
                                SendLangMsg(ply, "ArgNotRecognised", "<Camera ID> <New ID>");
                            }
                        }
                        else
                        {
                            SendLangMsg(ply, "NoPerm");
                        }
                        break;
                    case "add":
                        args[1] = args[1].ToLower();
                        if (1 < args.Length)
                        {
                            switch (args[1])
                            {
                                case "me":
                                    if (ply.IPlayer.HasPermission("cctvutilities.add.me"))
                                    {
                                        var cs = FindCCTVStationByRay(ply);
                                        if (cs != null)
                                        {
                                            foreach (var item in GetPlayerCCTVs(ply))
                                            {
                                                if (!cs.controlBookmarks.ContainsKey(item.rcIdentifier))
                                                {
                                                    cs.controlBookmarks.Add(item.rcIdentifier, item.net.ID);
                                                }
                                            }
                                        }
                                        SendLangMsg(ply, "AddMe");
                                        SendLangMsg(ply, "WarnInactive");
                                    }
                                    else
                                    {
                                        SendLangMsg(ply, "NoPerm");
                                    }
                                    break;
                                case "server":
                                    if (ply.IPlayer.HasPermission("cctvutilities.add.server"))
                                    {
                                        var cs = FindCCTVStationByRay(ply);
                                        if (cs != null)
                                        {
                                            foreach (var item in GetServerCCTVs())
                                            {
                                                if (!cs.controlBookmarks.ContainsKey(item.rcIdentifier))
                                                {
                                                    cs.controlBookmarks.Add(item.rcIdentifier, item.net.ID);
                                                }
                                            }
                                            SendLangMsg(ply, "AddServer");
                                        }
                                    }
                                    else
                                    {
                                        SendLangMsg(ply, "NoPerm");
                                    }
                                    break;
                                case "custom":
                                    if (ply.IPlayer.HasPermission("cctvutilities.add.custom"))
                                    {
                                        var cs = FindCCTVStationByRay(ply);
                                        if (cs != null)
                                        {
                                            foreach (var item in GetCustomCCTVs())
                                            {
                                                if (!cs.controlBookmarks.ContainsKey(item.rcIdentifier))
                                                {
                                                    cs.controlBookmarks.Add(item.rcIdentifier, item.net.ID);
                                                }
                                            }
                                            SendLangMsg(ply, "AddCustom");
                                        }
                                    }
                                    else
                                    {
                                        SendLangMsg(ply, "NoPerm");
                                    }
                                    break;
                                case "all":
                                    if (ply.IPlayer.HasPermission("cctvutilities.add.all"))
                                    {
                                        var cs = FindCCTVStationByRay(ply);
                                        if (cs != null)
                                        {
                                            foreach (var item in GetCCTVs())
                                            {
                                                if (!cs.controlBookmarks.ContainsKey(item.rcIdentifier))
                                                {
                                                    cs.controlBookmarks.Add(item.rcIdentifier, item.net.ID);
                                                }
                                            }
                                            SendLangMsg(ply, "AddAll");
                                            SendLangMsg(ply, "WarnInactive");
                                        }
                                    }
                                    else
                                    {
                                        SendLangMsg(ply, "NoPerm");
                                    }
                                    break;
                                default:
                                    SendLangMsg(ply, "ArgNotRecognised", "me | server | custom | all");
                                    break;
                            }
                        }
                        else
                        {
                            if (ply.IPlayer.HasPermission("cctvutilities.add.me"))
                            {
                                var cs = FindCCTVStationByRay(ply);
                                if (cs != null)
                                {
                                    foreach (var item in GetPlayerCCTVs(ply))
                                    {
                                        if (!cs.controlBookmarks.ContainsKey(item.rcIdentifier))
                                        {
                                            cs.controlBookmarks.Add(item.rcIdentifier, item.net.ID);
                                        }
                                    }
                                }
                                SendLangMsg(ply, "AddMe");
                                SendLangMsg(ply, "WarnInactive");
                            }
                            else
                            {
                                SendLangMsg(ply, "NoPerm");
                            }
                        }
                        break;
                    case "clear":
                        if (ply.IPlayer.HasPermission("cctvutilities.clear"))
                        {
                            var cs = FindCCTVStationByRay(ply);
                            if (cs != null)
                            {
                                cs.controlBookmarks.Clear();
                                SendLangMsg(ply, "Clear");
                            }
                        }
                        else
                        {
                            SendLangMsg(ply, "NoPerm");
                        }
                        break;
                    case "status":
                        args[1] = args[1].ToLower();
                        if (1 < args.Length)
                        {
                            switch (args[1])
                            {
                                case "me":
                                    if (ply.IPlayer.HasPermission("cctvutilities.status.me"))
                                    {
                                        SendLangMsgConsole(ply, "DetailPlayer");
                                        SendLangMsgConsole(ply, "DetailUniversalHeader");
                                        foreach (var cam in GetPlayerCCTVs(ply))
                                        {
                                            if (cam.IsPowered())
                                            {
                                                SendLangMsgConsole(ply, "DetailUniversalOnline", cam.rcIdentifier);
                                            }
                                            else
                                            {
                                                SendLangMsgConsole(ply, "DetailUniversalOffline", cam.rcIdentifier);
                                            }
                                        }
                                        SendLangMsg(ply, "CheckConsole", ply.displayName);
                                    }
                                    else
                                    {
                                        SendLangMsg(ply, "NoPerm");
                                    }
                                    break;
                                case "server":
                                    if (ply.IPlayer.HasPermission("cctvutilities.status.server"))
                                    {
                                        SendLangMsgConsole(ply, "DetailServer");
                                        SendLangMsgConsole(ply, "DetailUniversalHeader");
                                        foreach (var cam in GetServerCCTVs())
                                        {
                                            SendLangMsgConsole(ply, "DetailAllServer", cam.rcIdentifier);
                                        }
                                        SendLangMsg(ply, "CheckConsole", ply.displayName);
                                    }
                                    else
                                    {
                                        SendLangMsg(ply, "NoPerm");
                                    }
                                    break;
                                case "custom":
                                    if (ply.IPlayer.HasPermission("cctvutilities.status.custom"))
                                    {
                                        SendLangMsgConsole(ply, "DetailCustom");
                                        SendLangMsgConsole(ply, "DetailAllHeader");
                                        foreach (var cam in GetCustomCCTVs())
                                        {
                                            if (cam.OwnerID == 0)
                                            {
                                                SendLangMsgConsole(ply, "DetailAllServer", cam.rcIdentifier);
                                            }
                                            else
                                            {
                                                if (cam.IsPowered())
                                                {
                                                    SendLangMsgConsole(ply, "DetailAllPlayerOnline", cam.rcIdentifier, GetPlyName(cam.OwnerID), cam.OwnerID.ToString());
                                                }
                                                else
                                                {
                                                    SendLangMsgConsole(ply, "DetailAllPlayerOffline", cam.rcIdentifier, GetPlyName(cam.OwnerID), cam.OwnerID.ToString());
                                                }
                                            }
                                        }
                                        SendLangMsg(ply, "CheckConsole", ply.displayName);
                                    }
                                    else
                                    {
                                        SendLangMsg(ply, "NoPerm");
                                    }
                                    break;
                                case "all":
                                    if (ply.IPlayer.HasPermission("cctvutilities.status.all"))
                                    {
                                        SendLangMsgConsole(ply, "DetailAll");
                                        SendLangMsgConsole(ply, "DetailAllHeader");
                                        foreach (var cam in GetCCTVs())
                                        {
                                            if (cam.OwnerID == 0)
                                            {
                                                SendLangMsgConsole(ply, "DetailAllServer", cam.rcIdentifier);
                                            }
                                            else
                                            {
                                                if (cam.IsPowered())
                                                {
                                                    SendLangMsgConsole(ply, "DetailAllPlayerOnline", cam.rcIdentifier, GetPlyName(cam.OwnerID), cam.OwnerID.ToString());
                                                }
                                                else
                                                {
                                                    SendLangMsgConsole(ply, "DetailAllPlayerOffline", cam.rcIdentifier, GetPlyName(cam.OwnerID), cam.OwnerID.ToString());
                                                }
                                            }
                                        }
                                        SendLangMsg(ply, "CheckConsole", ply.displayName);
                                    }
                                    else
                                    {
                                        SendLangMsg(ply, "NoPerm");
                                    }
                                    break;
                                default:
                                    SendLangMsg(ply, "ArgNotRecognised", "me | server | all");
                                    break;
                            }
                        }
                        else
                        {
                            if (ply.IPlayer.HasPermission("cctvutilities.status.me"))
                            {
                                SendLangMsgConsole(ply, "DetailPlayer");
                                SendLangMsgConsole(ply, "DetailUniversalHeader");
                                foreach (var cam in GetPlayerCCTVs(ply))
                                {
                                    if (cam.IsPowered())
                                    {
                                        SendLangMsgConsole(ply, "DetailUniversalOnline", cam.rcIdentifier);
                                    }
                                    else
                                    {
                                        SendLangMsgConsole(ply, "DetailUniversalOffline", cam.rcIdentifier);
                                    }
                                }
                                SendLangMsg(ply, "CheckConsole", ply.displayName);
                            }
                            else
                            {
                                SendLangMsg(ply, "NoPerm");
                            }
                        }
                        break;
                    case "about":
                        SendLangMsg(ply, "CheckConsole", ply.displayName);
                        SendLangMsgConsole(ply, "AboutHeader");
                        SendLangMsgConsole(ply, "AboutDescription", this.Description);
                        SendLangMsgConsole(ply, "AboutAuthor");
                        SendLangMsgConsole(ply, "AboutVersion", this.Version.ToString());
                        SendLangMsgConsole(ply, "AboutLicense");
                        break;
                    case "autoname":
                        args[1] = args[1].ToLower();
                        if (ply.IPlayer.HasPermission("cctvutilities.autoname"))
                        {
                            if (1 < args.Length)
                            {
                                SetCCTVAutoname(ply, args[1]);
                            }
                            else
                            {
                                SetCCTVAutoname(ply, "toggle");
                            }
                        }
                        else
                        {
                            SendLangMsg(ply, "NoPerm");
                        }
                        break;
                    case "autoadd":
                        args[1] = args[1].ToLower();
                        if (ply.IPlayer.HasPermission("cctvutilities.autoadd"))
                        {
                            if (1 < args.Length)
                            {
                                SetCCTVAutoadd(ply, args[1]);
                            }
                            else
                            {
                                SetCCTVAutoadd(ply, "toggle");
                            }
                        }
                        else
                        {
                            SendLangMsg(ply, "NoPerm");
                        }
                        break;
                    case "autopowered":
                        args[1] = args[1].ToLower();
                        if (ply.IPlayer.HasPermission("cctvutilities.autopowered"))
                        {
                            if (1 < args.Length)
                            {
                                SetCCTVAutopowered(ply, args[1]);
                            }
                            else
                            {
                                SetCCTVAutopowered(ply, "toggle");
                            }
                        }
                        else
                        {
                            SendLangMsg(ply, "NoPerm");
                        }
                        break;
                    default:
                        SendLangMsg(ply, "ArgNotRecognised", "help | status | add | clear | rename | about | autoname | autoadd | autopowered");
                        break;
                        //Made By Grumpy Gordon :)
                }
            }
            else
            {
                if (ply.IPlayer.HasPermission("cctvutilities.help"))
                {
                    SendHelpChat(ply);
                }
                else
                {
                    SendLangMsg(ply, "NoPerm");
                }
            }
        }
        CCTV_RC FindCCTVByRay(BasePlayer ply)
        {
            RaycastHit ray;
            if (!Physics.Raycast(ply.eyes.HeadRay(), out ray, 5f))
            {
                SendLangMsg(ply, "NoEntity");
                return null;
            }
            CCTV_RC cctv = ray.GetEntity() as CCTV_RC;

            if(cctv == null)
            {
                SendLangMsg(ply, "WrongEntity", "Camera");
                return null;
            }
            else
            {
                return cctv;
            }
        }
        ComputerStation FindCCTVStationByRay(BasePlayer ply)
        {
            RaycastHit ray;

            if (!Physics.Raycast(ply.eyes.HeadRay(), out ray, 5f))
            {
                SendLangMsg(ply, "NoEntity");
                return null;
            }
            ComputerStation cctv = ray.GetEntity() as ComputerStation;

            if (cctv == null)
            {
                SendLangMsg(ply, "WrongEntity", "Computer Station");
                return null;
            }
            else
            {
                return cctv;
            }
        }
        CCTV_RC[] GetCCTVs()
        {
            return GameObject.FindObjectsOfType<CCTV_RC>();
        }
        CCTV_RC[] GetPlayerCCTVs(BasePlayer ply)
        {
            List<CCTV_RC> list = new List<CCTV_RC>(); 
            foreach (var item in GetCCTVs())
            {
                if(item.OwnerID == ply.userID)
                {
                    list.Add(item);
                }
            }
            return list.ToArray();
        }
        CCTV_RC[] GetServerCCTVs()
        {
            List<CCTV_RC> list = new List<CCTV_RC>();
            foreach (var item in GetCCTVs())
            {
                if (item.OwnerID == 0)
                {
                    list.Add(item);
                }
            }
            return list.ToArray();
        }
        CCTV_RC[] GetCustomCCTVs()
        {
            List<CCTV_RC> list = new List<CCTV_RC>();

            foreach (var cam in GetCCTVs())
            {
                if (GetCustomCCTVNames().Contains(cam.rcIdentifier))
                {
                    list.Add(cam);
                }
            }
            return list.ToArray();
        }
        string[] GetCustomCCTVNames()
        {
            return Interface.Oxide.DataFileSystem.ReadObject<string[]>("CCTVUtilities\\CustomCameras");
        }
        CCTV_RC FindCCTV(string id)
        {
            foreach (var item in GetCCTVs())
            {
                if(item.rcIdentifier == id)
                {
                    return item;
                }
            }
            return null;
        }
        CCTV_RC GetCCTVAuth(string id, BasePlayer ply)
        {
            var cam = FindCCTV(id);
            if (cam != null)
            {
                if (IsCCTVOwner(cam, ply))
                {
                    return cam;
                }
                else
                {
                    SendLangMsg(ply, "CamNotOwner");
                    Puts(string.Format(lang.GetMessage("CamNotOwnerConsole", this, ply.IPlayer.Id), ply.displayName, cam.rcIdentifier, GetPlyName(cam.OwnerID), cam.OwnerID));
                    return null;
                }
            }
            else
            {
                SendLangMsg(ply, "CamNotFound");
                return null;
            }
        }
        CCTV_RC GetCCTVAuth(CCTV_RC cam, BasePlayer ply)
        {
            if (cam != null)
            {
                if (IsCCTVOwner(cam, ply))
                {
                    return cam;
                }
                else
                {
                    SendLangMsg(ply, "CamNotOwner");
                    Puts(string.Format(lang.GetMessage("CamNotOwnerConsole", this, ply.IPlayer.Id), ply.displayName, cam.rcIdentifier, GetPlyName(cam.OwnerID), cam.OwnerID));
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
        bool IsCCTVOwner(CCTV_RC cctv, BasePlayer ply)
        {
            if(cctv.OwnerID == ply.userID)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        string GetPlyName(ulong id)
        {
            if (BasePlayer.FindByID(id) != null)
            {
                return BasePlayer.FindByID(id).displayName;
            }
            else
            {
                return "Server";
            }
        }

        void SetCCTVAutoname(BasePlayer ply, string arg)
        {
            DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetDatafile("CCTVUtilities\\PlayerOptions");
            switch (arg)
            {
                case "on":
                    dataFile[ply.userID + "_autoname"] = true;
                    dataFile.Save();
                    SendLangMsg(ply, "AutoNameOn");
                    break;
                case "off":
                    dataFile[ply.userID + "_autoname"] = false;
                    dataFile.Save();
                    SendLangMsg(ply, "AutoNameOff");
                    break;
                case "toggle":
                    if (dataFile[ply.userID + "_autoname"] == null)
                    {
                        dataFile[ply.userID + "_autoname"] = true;
                        SendLangMsg(ply, "AutoNameToggleOn");
                    }
                    else if (dataFile[ply.userID + "_autoname"].ToString().ToLower() == "true")
                    {
                        dataFile[ply.userID + "_autoname"] = false;
                        SendLangMsg(ply, "AutoNameToggleOff");
                    }
                    else if (dataFile[ply.userID + "_autoname"].ToString().ToLower() == "false")
                    {
                        dataFile[ply.userID + "_autoname"] = true;
                        SendLangMsg(ply, "AutoNameToggleOn");
                    }
                    else
                    {
                        dataFile[ply.userID + "_autoname"] = true;
                        SendLangMsg(ply, "AutoNameToggleOn");
                    }
                    dataFile.Save();
                    break;
                default:
                    SendLangMsg(ply, "ArgNotRecognised", "on | off | toggle");
                    break;
            }
        }
        void SetCCTVAutopowered(BasePlayer ply, string arg)
        {
            DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetDatafile("CCTVUtilities\\PlayerOptions");
            switch (arg)
            {
                case "on":
                    dataFile[ply.userID + "_autopowered"] = true;
                    dataFile.Save();
                    SendLangMsg(ply, "AutoPoweredOn");
                    break;
                case "off":
                    dataFile[ply.userID + "_autopowered"] = false;
                    dataFile.Save();
                    SendLangMsg(ply, "AutoPoweredOff");
                    break;
                case "toggle":
                    if (dataFile[ply.userID + "_autopowered"] == null)
                    {
                        dataFile[ply.userID + "_autopowered"] = true;
                        SendLangMsg(ply, "AutoPoweredToggleOn");
                    }
                    else if (dataFile[ply.userID + "_autopowered"].ToString().ToLower() == "true")
                    {
                        dataFile[ply.userID + "_autopowered"] = false;
                        SendLangMsg(ply, "AutoPoweredToggleOff");
                    }
                    else if (dataFile[ply.userID + "_autopowered"].ToString().ToLower() == "false")
                    {
                        dataFile[ply.userID + "_autopowered"] = true;
                        SendLangMsg(ply, "AutoPoweredToggleOn");
                    }
                    else
                    {
                        dataFile[ply.userID + "_autopowered"] = true;
                        SendLangMsg(ply, "AutoPoweredToggleOn");
                    }
                    dataFile.Save();
                    break;
                default:
                    SendLangMsg(ply, "ArgNotRecognised", "on | off | toggle");
                    break;
            }
        }
        void SetCCTVAutoadd(BasePlayer ply, string arg)
        {
            DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetDatafile("CCTVUtilities\\PlayerOptions");
            switch (arg)
            {
                case "on":
                    if (ply.IPlayer.HasPermission("cctvutilities.autoadd.on"))
                    {
                        dataFile[ply.userID + "_autoadd"] = "me";
                        dataFile.Save();
                        SendLangMsg(ply, "AutoAddOn");
                    }
                    else
                    {
                        SendLangMsg(ply, "NoPerm");
                    }
                    break;
                case "off":
                    if (ply.IPlayer.HasPermission("cctvutilities.autoadd.off"))
                    {
                        dataFile[ply.userID + "_autoadd"] = false;
                        dataFile.Save();
                        SendLangMsg(ply, "AutoAddOff");
                    }
                    else
                    {
                        SendLangMsg(ply, "NoPerm");
                    }
                    break;
                case "me":
                    if (ply.IPlayer.HasPermission("cctvutilities.autoadd.me"))
                    {
                        dataFile[ply.userID + "_autoadd"] = "me";
                        dataFile.Save();
                        SendLangMsg(ply, "AutoAddMe");
                    }
                    else
                    {
                        SendLangMsg(ply, "NoPerm");
                    }
                    break;
                case "server":
                    if (ply.IPlayer.HasPermission("cctvutilities.autoadd.server"))
                    {
                        dataFile[ply.userID + "_autoadd"] = "server";
                        dataFile.Save();
                        SendLangMsg(ply, "AutoAddServer");
                    }
                    else
                    {
                        SendLangMsg(ply, "NoPerm");
                    }
                    break;
                case "custom":
                    if (ply.IPlayer.HasPermission("cctvutilities.autoadd.custom"))
                    {
                        dataFile[ply.userID + "_autoadd"] = "custom";
                        dataFile.Save();
                        SendLangMsg(ply, "AutoAddCustom");
                    }
                    else
                    {
                        SendLangMsg(ply, "NoPerm");
                    }
                    break;
                case "all":
                    if (ply.IPlayer.HasPermission("cctvutilities.autoadd.all"))
                    {
                        dataFile[ply.userID + "_autoadd"] = "all";
                        dataFile.Save();
                        SendLangMsg(ply, "AutoAddAll");
                    }
                    else
                    {
                        SendLangMsg(ply, "NoPerm");
                    }
                    break;
                case "toggle":
                    if (ply.IPlayer.HasPermission("cctvutilities.autoadd.toggle"))
                    {
                        if (dataFile[ply.userID + "_autoadd"] == null)
                        {
                            dataFile[ply.userID + "_autoadd"] = "me";
                            SendLangMsg(ply, "AutoAddToggleOn");
                        }
                        else if (dataFile[ply.userID + "_autoadd"].ToString().ToLower() == "me")
                        {
                            dataFile[ply.userID + "_autoadd"] = false;
                            SendLangMsg(ply, "AutoAddToggleOff");
                        }
                        else if (dataFile[ply.userID + "_autoadd"].ToString().ToLower() == "false")
                        {
                            dataFile[ply.userID + "_autoadd"] = "me";
                            SendLangMsg(ply, "AutoAddToggleOn");
                        }
                        else
                        {
                            dataFile[ply.userID + "_autoadd"] = "me";
                            SendLangMsg(ply, "AutoAddToggleOn");
                        }
                    }
                    else
                    {
                        SendLangMsg(ply, "NoPerm");
                    }
                    dataFile.Save();
                    break;
                default:
                    SendLangMsg(ply, "ArgNotRecognised", "on | off | me | server | custom | all | toggle");
                    break;
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            BaseEntity be = go.ToBaseEntity();
            BasePlayer ply = BasePlayer.FindByID(be.OwnerID);
            if (be.name == "assets/prefabs/deployable/cctvcamera/cctv_deployed.prefab")
            {
                CCTV_RC cam = be as CCTV_RC;

                DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetDatafile("CCTVUtilities\\PlayerOptions");
                if (dataFile[ply.userID + "_autoname"] != null && dataFile[ply.userID + "_autopowered"] != null)
                {
                    if (dataFile[ply.userID + "_autoname"].ToString().ToLower() == "true" && dataFile[ply.userID + "_autopowered"].ToString().ToLower() == "true")
                    {
                        cam.UpdateIdentifier(RandomString(12));
                        cam.UpdateHasPower(25, 1);
                        SendLangMsg(ply, "AutoNamePoweredCamPlaced", cam.rcIdentifier);
                    }
                }
                else if (dataFile[ply.userID + "_autoname"] != null)
                {
                    if (dataFile[ply.userID + "_autoname"].ToString().ToLower() == "true")
                    {
                        cam.UpdateIdentifier(RandomString(12));
                        SendLangMsg(ply, "AutoNameCamPlaced", cam.rcIdentifier);
                    }
                }
                else if (dataFile[ply.userID + "_autopowered"] != null)
                {
                    if (dataFile[ply.userID + "_autopowered"].ToString().ToLower() == "true")
                    {
                        cam.UpdateHasPower(25, 1);
                        SendLangMsg(ply, "AutoPoweredCamPlaced");
                    }
                }
            }
            else if (be.name == "assets/prefabs/deployable/computerstation/computerstation.deployed.prefab")
            {
                ComputerStation cs = be as ComputerStation;
                
                DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetDatafile("CCTVUtilities\\PlayerOptions");
                if (dataFile[ply.userID + "_autoadd"] != null)
                {
                    string option = dataFile[ply.userID + "_autoadd"].ToString().ToLower();
                    switch (option)
                    {
                        case "me":
                            if (ply.IPlayer.HasPermission("cctvutilities.autoadd.me"))
                            {
                                if (cs != null)
                                {
                                    foreach (var item in GetPlayerCCTVs(ply))
                                    {
                                        if (!cs.controlBookmarks.ContainsKey(item.rcIdentifier))
                                        {
                                            cs.controlBookmarks.Add(item.rcIdentifier, item.net.ID);
                                        }
                                    }
                                }
                                SendLangMsg(ply, "AddMe");
                            }
                            else
                            {
                                SendLangMsg(ply, "NoPerm");
                            }
                            break;
                        case "server":
                            if (ply.IPlayer.HasPermission("cctvutilities.autoadd.server"))
                            {
                                if (cs != null)
                                {
                                    foreach (var item in GetServerCCTVs())
                                    {
                                        if (!cs.controlBookmarks.ContainsKey(item.rcIdentifier))
                                        {
                                            cs.controlBookmarks.Add(item.rcIdentifier, item.net.ID);
                                        }
                                    }
                                }
                                SendLangMsg(ply, "AddServer");
                            }
                            else
                            {
                                SendLangMsg(ply, "NoPerm");
                            }
                            break;
                        case "custom":
                            if (ply.IPlayer.HasPermission("cctvutilities.autoadd.custom"))
                            {
                                if (cs != null)
                                {
                                    foreach (var item in GetCustomCCTVs())
                                    {
                                        if (!cs.controlBookmarks.ContainsKey(item.rcIdentifier))
                                        {
                                            cs.controlBookmarks.Add(item.rcIdentifier, item.net.ID);
                                        }
                                    }
                                }
                                SendLangMsg(ply, "AddCustom");
                            }
                            else
                            {
                                SendLangMsg(ply, "NoPerm");
                            }
                            break;
                        case "all":
                            if (ply.IPlayer.HasPermission("cctvutilities.autoadd.all"))
                            {
                                if (cs != null)
                                {
                                    foreach (var item in GetCCTVs())
                                    {
                                        if (!cs.controlBookmarks.ContainsKey(item.rcIdentifier))
                                        {
                                            cs.controlBookmarks.Add(item.rcIdentifier, item.net.ID);
                                        }
                                    }
                                }
                                SendLangMsg(ply, "AddAll");
                            }
                            else
                            {
                                SendLangMsg(ply, "NoPerm");
                            }
                            break;
                        case "false":
                            break;
                        default:
                            if (ply.IPlayer.HasPermission("cctvutilities.autoadd.me"))
                            {
                                if (cs != null)
                                {
                                    foreach (var item in GetPlayerCCTVs(ply))
                                    {
                                        if (!cs.controlBookmarks.ContainsKey(item.rcIdentifier))
                                        {
                                            cs.controlBookmarks.Add(item.rcIdentifier, item.net.ID);
                                        }
                                    }
                                }
                                SendLangMsg(ply, "AddMe");
                            }
                            else
                            {
                                SendLangMsg(ply, "NoPerm");
                            }
                            break;
                    }
                }
            }
        }
        string RandomString(int length)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringChars = new char[length];
            var random = new System.Random();

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }
            return new String(stringChars);
        }
        void OnEntitySpawned(CCTV_RC cam)
        {
            if(cam.OwnerID != 0)
            {
                DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetDatafile("CCTVUtilities\\PlayerOptions");
                BasePlayer ply = BasePlayer.FindByID(cam.OwnerID);
                if (dataFile[ply.userID + "_autopowered"] != null)
                {
                    if (dataFile[ply.userID + "_autopowered"].ToString().ToLower() == "true")
                    {
                        cam.UpdateHasPower(25, 1);
                    }
                }
                cam.UpdateHasPower(25, 1);
            }
        }
    }
}
