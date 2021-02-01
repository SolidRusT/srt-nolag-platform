#region License (GPL v3)
/*
    DESCRIPTION
    Copyright (c) 2020 RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion License Information (GPL v3)
//#define DEBUG
using System.Collections.Generic;
using System.Globalization;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Tool Cupboard GUI", "RFC1920", "1.0.12")]
    [Description("Manage TC and Turret auth")]
    class TCGui : RustPlugin
    {
        #region vars
        [PluginReference]
        readonly Plugin HumanNPC, Friends, Clans;

        private ConfigData configData;
        private const string permTCGuiUse = "tcgui.use";
        const string TCGUI = "tcgui.editor";
        const string TCGUP = "tcgui.players";
        const string TCGUB = "tcgui.button";

        private readonly Dictionary<string, string> onlinePlayers = new Dictionary<string, string>();
        private readonly Dictionary<string, string> offlinePlayers = new Dictionary<string, string>();
        // Dict of TC net ID and player id for currently opened cupboards
        private Dictionary<uint, ulong> cuploot = new Dictionary<uint, ulong>();
        private readonly string tcurl = "http://vignette2.wikia.nocookie.net/play-rust/images/5/57/Tool_Cupboard_icon.png/revision/latest/scale-to-width-down/{0}";
        private readonly string trurl = "http://vignette2.wikia.nocookie.net/play-rust/images/f/f9/Auto_Turret_icon.png/revision/latest/scale-to-width-down/{0}";
        #endregion

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));
        #endregion

        #region init
        private void Init()
        {
            AddCovalenceCommand("tc", "CmdTCGUI");
            permission.RegisterPermission(permTCGuiUse, this);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["tcgui"] = "Tool Cupboard GUI",
                ["tcguisel"] = "Tool Cupboard GUI - Player Select",
                ["helptext1"] = "Tool Cupboard GUI instructions:",
                ["notauthorized"] = "You are not authorized for this command!",
                ["helptext2"] = "  type /tc to do stuff",
                ["close"] = "Close",
                ["me"] = "Me",
                ["manage"] = "Manage",
                ["foundtc"] = "Found a TC.  Authorized players:",
                ["none"] = "None found!",
                ["cupboard"] = "Cupboard",
                ["turret"] = "Turret",
                ["turrets"] = "Turrets",
                ["select"] = "Select",
                ["add"] = "Add",
                ["authall"] = "AuthAll",
                ["online"] = "Online",
                ["offline"] = "Offline",
                ["deauthall"] = "DeAuthAll",
                ["remove"] = "Remove"
            }, this);
        }

        private void Loaded() => LoadConfigValues();

        private void Unload()
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, TCGUI);
                CuiHelper.DestroyUi(player, TCGUP);
                CuiHelper.DestroyUi(player, TCGUB);
            }
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if(!player.IPlayer.HasPermission(permTCGuiUse)) return null;
            var privs = container.GetComponentInParent<BuildingPrivlidge>() ?? null;
            if(privs == null) return null;

            if(cuploot.ContainsKey(privs.net.ID)) return null;

            cuploot.Add(privs.net.ID, player.userID);
            TcButtonGUI(player, privs);

            return null;
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if(!player.IPlayer.HasPermission(permTCGuiUse)) return;
            if(!cuploot.ContainsKey(entity.net.ID)) return;
            if(entity == null) return;

            if(cuploot[entity.net.ID] == player.userID)
            {
                CuiHelper.DestroyUi(player, TCGUI);
                CuiHelper.DestroyUi(player, TCGUB);
                CuiHelper.DestroyUi(player, TCGUP);
                cuploot.Remove(entity.net.ID);
            }
        }
        #endregion

        #region Main
        [Command("tc")]
        private void CmdTCGUI(IPlayer iplayer, string command, string[] args)
        {
            if (!iplayer.HasPermission(permTCGuiUse)) { Message(iplayer, "notauthorized"); return; }
#if DEBUG
            string debug = string.Join(",", args); Puts($"{debug}");
#endif
            var player = iplayer.Object as BasePlayer;
            if(args.Length > 0)
            {
                if(args[0] == "guiclose")
                {
                    CuiHelper.DestroyUi(player, TCGUI);
                }
            }

            List<BuildingPrivlidge> cupboards = new List<BuildingPrivlidge>();
            Vis.Entities(player.transform.position, configData.Settings.cupboardRange, cupboards);
            foreach(var ent in cupboards)
            {
                if(ent.IsAuthed(player))
                {
                    if(args.Length > 0)
                    {
                        if(args[0] == "gui")
                        {
                            TcGUI(player, ent);
                        }
                        else if(args[0] == "guibtn")
                        {
                            TcGUI(player, ent);
                        }
                        else if(args[0] == "guisel")
                        {
                            if(args.Length > 2)
                            {
                                //      0     1      2      3    4
                                // tc guisel MODE TURRETID page NUM
                                // tc guisel cupboard 0    page 1
                                if (args.Length > 3)
                                {
                                    if (args[3] == "page" && args.Length > 4)
                                    {
#if DEBUG
                                        Puts($"Mode: {args[1]}, turretid = {args[2]}, page = {args[4]}");
#endif
                                        PlayerSelectGUI(player, args[1], uint.Parse(args[2]), uint.Parse(args[4]));
                                    }
                                }
                                else
                                {
                                    // tc guisel turret turretid
#if DEBUG
                                    Puts($"Mode: turret, turretid = {args[2].ToString()}");
#endif
                                    PlayerSelectGUI(player, "turret", uint.Parse(args[2]));
                                }
                            }
                            else
                            {
                                PlayerSelectGUI(player);
                            }
                        }
                        else if(args[0] == "guiselclose")
                        {
                            CuiHelper.DestroyUi(player, TCGUP);
                        }
                        else if(args[0] == "remove" && args.Length > 1)
                        {
#if DEBUG
                            Puts($"Removing player ({args[1]}) from TC");
#endif
                            CuiHelper.DestroyUi(player, TCGUP);
                            // tc remove 7656XXXXXXXXXXXX

                            foreach (var p in ent.authorizedPlayers.ToArray())
                            {
                                if (p.userid == ulong.Parse(args[1]))
                                {
                                    ent.authorizedPlayers.Remove(p);
                                    ent.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                                }
                            }
                            TcGUI(player, ent);
                        }
                        else if(args[0] == "add" && args.Length > 2)
                        {
#if DEBUG
                            Puts($"Adding player ({args[1]}/{args[2]}) to TC");
#endif
                            CuiHelper.DestroyUi(player, TCGUP);
                            // tc add 7656XXXXXXXXXXXX RFC1920
                            bool exists = false;
                            foreach (var p in ent.authorizedPlayers.ToArray())
                            {
                                if (p.userid == ulong.Parse(args[1]))
                                {
                                    exists = true;
                                }
                            }
                            if (!exists)
                            {
                                ent.authorizedPlayers.Add(new ProtoBuf.PlayerNameID()
                                {
                                    userid = ulong.Parse(args[1]),
                                    username = args[2]
                                });
                                ent.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                            }
                            TcGUI(player, ent);
                        }
                        else if(args[0] == "tremove" && args.Length > 2)
                        {
                            CuiHelper.DestroyUi(player, TCGUP);
                            // tc tremove 7656XXXXXXXXXXXX TURRETID
                            var turret = BaseNetworkable.serverEntities.Find(uint.Parse(args[2])) as AutoTurret;

                            if (turret != null)
                            {
                                foreach (var p in turret.authorizedPlayers.ToArray())
                                {
                                    if (p.userid == ulong.Parse(args[1]))
                                    {
                                        turret.authorizedPlayers.Remove(p);
                                        turret.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                                    }
                                }
                            }
                            TcGUI(player, ent);
                        }
                        else if(args[0] == "tadd" && args.Length > 3)
                        {
                            CuiHelper.DestroyUi(player, TCGUP);
                            // tc tadd 7656XXXXXXXXXXXX NAME TURRETID
                            var turret = BaseNetworkable.serverEntities.Find(uint.Parse(args[3])) as AutoTurret;

                            if (turret != null)
                            {
                                turret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID()
                                {
                                    userid = ulong.Parse(args[1]),
                                    username = args[2]
                                });
                                turret.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                            }
                            TcGUI(player, ent);
                        }
                    }
                    else
                    {
                        Message(iplayer, "foundtc");
                        foreach(var auth in ent.authorizedPlayers.Select(x => x.userid).ToArray())
                        {
                            var theplayer = BasePlayer.Find(auth.ToString());
                            if (theplayer == null) continue;
                            Message(iplayer, theplayer.displayName);
                        }
                    }
                }
                break;
            }
        }

        private List<AutoTurret> GetTurrets(Vector3 location, float range = 30f)
        {
            List<AutoTurret> turrets = new List<AutoTurret>();
            Vis.Entities(location, range, turrets);
            return turrets;
        }

        private bool IsFriend(ulong playerid, ulong ownerid)
        {
            if (configData.Settings.useFriends && Friends != null)
            {
                var fr = Friends?.CallHook("AreFriends", playerid, ownerid);
                if (fr != null && (bool)fr)
                {
                    return true;
                }
            }
            if (configData.Settings.useClans && Clans != null)
            {
                string playerclan = (string)Clans?.CallHook("GetClanOf", playerid);
                string ownerclan = (string)Clans?.CallHook("GetClanOf", ownerid);
                if (playerclan != null && ownerclan != null)
                {
                    if (playerclan == ownerclan)
                    {
                        return true;
                    }
                }
            }
            if (configData.Settings.useTeams)
            {
                BasePlayer player = BasePlayer.FindByID(playerid);
                if (player != null)
                {
                    if (player.currentTeam != 0)
                    {
                        RelationshipManager.PlayerTeam playerTeam = RelationshipManager.Instance.FindTeam(player.currentTeam);
                        if (playerTeam != null)
                        {
                            if (playerTeam.members.Contains(ownerid))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private void TcGUI(BasePlayer player, BuildingPrivlidge privs)
        {
            if (player == null) return;
            if (privs == null) return;
            CuiHelper.DestroyUi(player, TCGUI);

            CuiElementContainer container = UI.Container(TCGUI, UI.Color("2b2b2b", 1f), "0.15 0.1", "0.85 0.9", true, "Overlay");
            UI.Button(ref container, TCGUI, UI.Color("#d85540", 1f), Lang("close"), 12, "0.92 0.93", "0.985 0.98", $"tc guiclose");
            UI.Label(ref container, TCGUI, UI.Color("#ffffff", 1f), Lang("tcgui"), 18, "0.23 0.92", "0.7 1");

            UI.Icon(ref container, TCGUI, UI.Color("#ffffff", 1f), string.Format(tcurl, 140), "0.1 0.83", "0.15 0.9");
            UI.Label(ref container, TCGUI, UI.Color("#ffffff", 1f), Lang("cupboard"), 14, "0.15 0.83", "0.28 0.9");

            UI.Icon(ref container, TCGUI, UI.Color("#ffffff", 1f), string.Format(trurl, 140), "0.49 0.83", "0.54 0.9");
            UI.Label(ref container, TCGUI, UI.Color("#ffffff", 1f), Lang("turrets"), 14, "0.5 0.83", "0.7 0.9");

            int nc = 0;
            float[] n = GetButtonPosition(nc, 1);
            float[] b = GetButtonPosition(nc, 2);
            UI.Label(ref container, TCGUI, UI.Color("#ffffff", 1f), Lang("me"), 12, $"{n[0]} {n[1]}", $"{n[0] + ((n[2] - n[0]) / 2)} {n[3]}", TextAnchor.MiddleLeft);
            bool authed = false;

            foreach(var auth in privs.authorizedPlayers.Select(x => x.userid).ToArray())
            {
                var findme = BasePlayer.Find(auth.ToString());
                if(findme == null) continue;
                if(findme.userID == player.userID) authed = true;
                break;
            }

            if(authed)
            {
                UI.Button(ref container, TCGUI, UI.Color("#ff0000", 1f), Lang("remove"), 12, $"{b[0]} {b[1]}", $"{b[0] + ((b[2] - b[0]) / 2)} {b[3]}", $"tc remove {player.userID}");
            }
            else
            {
                UI.Button(ref container, TCGUI, UI.Color("#cccccc", 1f), Lang("add"), 12, $"{b[0]} {b[1]}", $"{b[0] + ((b[2] - b[0]) / 2)} {b[3]}", $"tc add {player.userID} {player.displayName}");
            }

            foreach(var auth in privs.authorizedPlayers.Select(x => x.userid).ToArray())
            {
                BasePlayer theplayer = BasePlayer.FindByID(auth);
                if(theplayer == null) continue;
#if DEBUG
                Puts($"Found authorized player ({theplayer.userID}/{theplayer.displayName})");
#endif
                if(theplayer.userID == player.userID) continue;
                nc++;

                float[] posn = GetButtonPosition(nc, 1);
                float[] posb = GetButtonPosition(nc, 2);

                UI.Label(ref container, TCGUI, UI.Color("#ffffff", 1f), theplayer.displayName, 12, $"{posn[0]} {posn[1]}", $"{posn[0] + ((posn[2] - posn[0]) / 2)} {posn[3]}", TextAnchor.MiddleLeft);
                UI.Button(ref container, TCGUI, UI.Color("#d85540", 1f), Lang("remove"), 12, $"{posb[0]} {posb[1]}", $"{posb[0] + ((posb[2] - posb[0]) / 2)} {posb[3]}", $"tc remove {theplayer.userID}");

            }
            nc++;

            float[] poss = GetButtonPosition(nc, 2);
            UI.Button(ref container, TCGUI, UI.Color("115540", 1f), Lang("select"), 12, $"{poss[0]} {poss[1]}", $"{poss[0] + ((poss[2] - poss[0]) / 2)} {poss[3]}", $"tc guisel");

            List<AutoTurret> turrets = GetTurrets(player.transform.position, configData.Settings.turretRange);
            List<ulong> foundturrets = new List<ulong>();

            nc = -1;
            foreach(var turret in turrets)
            {
                if(foundturrets.Contains(turret.net.ID)) continue;
                foundturrets.Add(turret.net.ID);

                nc++;
                float[] posn = GetButtonPosition(nc, 4);
                UI.Label(ref container, TCGUI, UI.Color("#ffffff", 1f), turret.net.ID.ToString(), 12, $"{posn[0]} {posn[1]}", $"{posn[0] + ((posn[2] - posn[0]) / 2)} {posn[3]}", TextAnchor.MiddleLeft);

                n = GetButtonPosition(nc, 5);
                b = GetButtonPosition(nc, 6);
                UI.Label(ref container, TCGUI, UI.Color("#ffffff", 1f), Lang("me"), 12, $"{n[0]} {n[1]}", $"{n[0] + ((n[2] - n[0]) / 2)} {n[3]}", TextAnchor.MiddleLeft);

                authed = false;
                foreach(var auth in turret.authorizedPlayers.Select(x => x.userid).ToArray())
                {
                    var findme = BasePlayer.Find(auth.ToString());
                    if (findme == null) continue;
                    if (findme.userID == player.userID) authed = true;
                }

                if(authed)
                {
                    UI.Button(ref container, TCGUI, UI.Color("#d85540", 1f), Lang("remove"), 12, $"{b[0]} {b[1]}", $"{b[0] + ((b[2] - b[0]) / 2)} {b[3]}", $"tc tremove {player.userID} {turret.net.ID.ToString()}");
                }
                else
                {
                    UI.Button(ref container, TCGUI, UI.Color("#cccccc", 1f), Lang("add"), 12, $"{b[0]} {b[1]}", $"{b[0] + ((b[2] - b[0]) / 2)} {b[3]}", $"tc tadd {player.userID} {player.displayName} {turret.net.ID.ToString()}");
                }

                foreach(var auth in turret.authorizedPlayers.Select(x => x.userid).ToArray())
                {
                    BasePlayer theplayer = BasePlayer.FindByID(auth);
                    if (theplayer == null) continue;
                    if (theplayer.userID == player.userID) continue;
                    nc++;

                    posn = GetButtonPosition(nc, 5);
                    float[] posb = GetButtonPosition(nc, 6);
                    UI.Label(ref container, TCGUI, UI.Color("#ffffff", 1f), theplayer.displayName, 12, $"{posn[0]} {posn[1]}", $"{posn[0] + ((posn[2] - posn[0]) / 2)} {posn[3]}", TextAnchor.MiddleLeft);
                    UI.Button(ref container, TCGUI, UI.Color("#d85540", 1f), Lang("remove"), 12, $"{posb[0]} {posb[1]}", $"{posb[0] + ((posb[2] - posb[0]) / 2)} {posb[3]}", $"tc tremove {theplayer.userID} {turret.net.ID.ToString()}");
                }
                nc++;
                poss = GetButtonPosition(nc, 6);
                UI.Button(ref container, TCGUI, UI.Color("#115540", 1f), Lang("select"), 12, $"{poss[0]} {poss[1]}", $"{poss[0] + ((poss[2] - poss[0]) / 2)} {poss[3]}", $"tc guisel turret {turret.net.ID.ToString()}");
            }

            CuiHelper.AddUi(player, container);
        }

        private void PlayerSelectGUI(BasePlayer player, string mode = "cupboard", uint turretid=0, uint page=0)
        {
            CuiHelper.DestroyUi(player, TCGUP);

            int total = BasePlayer.activePlayerList.Count + BasePlayer.sleepingPlayerList.Count;
#if DEBUG
            total += 300;
#endif
            string description = Lang("tcguisel") + ": " + Lang(mode);
            if(mode == "turret") description += $" {turretid.ToString()}";
            CuiElementContainer container = UI.Container(TCGUP, UI.Color("242424", 1f), "0.1 0.1", "0.9 0.9", true, "Overlay");
            UI.Label(ref container, TCGUP, UI.Color("#ffffff", 1f), description, 18, "0.23 0.92", "0.7 1");
            UI.Label(ref container, TCGUP, UI.Color("#d85540", 1f), Lang("online"), 12, "0.72 0.92", "0.77 1");
            UI.Label(ref container, TCGUP, UI.Color("#555500", 1f), Lang("offline"), 12, "0.79 0.92", "0.86 1");
            UI.Button(ref container, TCGUP, UI.Color("#d85540", 1f), Lang("close"), 12, "0.92 0.93", "0.985 0.98", $"tc guiselclose");
            // Pagination
            if(page > 0)
            {
                UI.Button(ref container, TCGUP, UI.Color("#d85540", 1f), "<<", 12, "0.01 0.001", "0.04 0.03", $"tc guisel {mode} {turretid} page {page-1}");
            }
            if (total > 98 && (page+1) * 98 < total)
            {
                UI.Button(ref container, TCGUP, UI.Color("#d85540", 1f), ">>", 12, "0.96 0.001", "0.99 0.03", $"tc guisel {mode} {turretid} page {page + 1}");
            }
            int col = 0;
            int row = 0;
            bool found = false;

            int iter = 0;
#if DEBUG
            // Show fake players
            for(int f=1; f < 301; f++)
            {
                iter++;
                found = true;
                // Pagination
                if (page > 0)
                {
                    if (iter <= page * 98) continue; 
                    if (iter > (page+1) * 98) continue;
                }
                else
                {
                    if (iter > 98)
                    {
                        continue;
                    }
                }
                if(row > 13)
                {
                    row = 0;
                    col++;
                }
                if(col > 7)
                {
                    col = 0; row = 0;
                    continue;
                }
                //Puts($"ITER: iter={iter.ToString()}, row={row.ToString()}, col={col.ToString()}, page={page}");

                float[] posb = GetButtonPositionP(row, col);
                var hName = "FAKE" + f.ToString();
                if(mode == "turret" && turretid > 0)
                {
                    UI.Button(ref container, TCGUP, UI.Color("#d85540", 1f), hName, 12, $"{posb[0]} {posb[1]}", $"{posb[0] + ((posb[2] - posb[0]) / 2)} {posb[3]}", $"tc tadd {f.ToString()} {turretid.ToString()}");
                }
                else
                {
                    UI.Button(ref container, TCGUP, UI.Color("#d85540", 1f), hName, 12, $"{posb[0]} {posb[1]}", $"{posb[0] + ((posb[2] - posb[0]) / 2)} {posb[3]}", $"tc add {f.ToString()} {hName}");
                }
                row++;
            }
#endif
            foreach(BasePlayer user in BasePlayer.activePlayerList.OrderBy(o => o.displayName).ToList())
            {
                iter++;
                found = true;
                if(user.userID == player.userID) continue;
                if (configData.Settings.limitToFriends)
                {
                    if (!IsFriend(user.userID, player.userID)) continue;
                }

                // Pagination
                if (page > 0)
                {
                    if (iter <= page * 98) continue; 
                    if (iter > (page+1) * 98) continue;
                }
                else
                {
                    if (iter > 98)
                    {
                        continue;
                    }
                }
                if(row > 13)
                {
                    row = 0;
                    col++;
                }
                if(col > 7)
                {
                    col = 0; row = 0;
                    continue;
                }

                float[] posb = GetButtonPositionP(row, col);
                if(mode == "turret" && turretid > 0)
                {
                    UI.Button(ref container, TCGUP, UI.Color("#d85540", 1f), user.displayName, 12, $"{posb[0]} {posb[1]}", $"{posb[0] + ((posb[2] - posb[0]) / 2)} {posb[3]}", $"tc tadd {user.userID} {user.UserIDString} {turretid.ToString()}");
                }
                else
                {
                    UI.Button(ref container, TCGUP, UI.Color("#d85540", 1f), user.displayName, 12, $"{posb[0]} {posb[1]}", $"{posb[0] + ((posb[2] - posb[0]) / 2)} {posb[3]}", $"tc add {user.userID} {user.UserIDString}");
                }
                row++;
            }

            foreach(BasePlayer user in BasePlayer.sleepingPlayerList.OrderBy(o => o.displayName).ToList())
            {
                iter++;
                found = true;
                if (configData.Settings.limitToFriends)
                {
                    if (!IsFriend(user.userID, player.userID)) continue;
                }

                // Pagination
                if (page > 0)
                {
                    if (iter <= page * 98) continue; 
                    if (iter > (page+1) * 98) continue;
                }
                else
                {
                    if (iter > 98)
                    {
                        continue;
                    }
                }
                if(row > 13)
                {
                    row = 0;
                    col++;
                }
                if(col > 7)
                {
                    col = 0; row = 0;
                    continue;
                }


                float[] posb = GetButtonPositionP(row, col);
                if(mode == "turret" && turretid > 0)
                {
                    //UI.Button(ref container, TCGUP, UI.Color("#555500", 1f), user.displayName, 12, $"{posb[0]} {posb[1]}", $"{posb[0] + ((posb[2] - posb[0]) / 2)} {posb[3]}", $"tc tadd {user.userID} {user.UserIDString} {turretid.ToString()}");
                }
                else
                {
                    UI.Button(ref container, TCGUP, UI.Color("#555500", 1f), user.displayName, 12, $"{posb[0]} {posb[1]}", $"{posb[0] + ((posb[2] - posb[0]) / 2)} {posb[3]}", $"tc add {user.userID} {user.UserIDString}");
                }
                row++;
            }
            if(!found)
            {
                UI.Label(ref container, TCGUP, UI.Color("#ffffff", 1f), Lang("none"), 12, "0.2 0.4", "0.7 1");
            }

            CuiHelper.AddUi(player, container);
        }

        private void TcButtonGUI(BasePlayer player, BaseEntity entity)
        {
            CuiHelper.DestroyUi(player, TCGUB);

            CuiElementContainer container = UI.Container(TCGUB, UI.Color("FFF5E0", 0.16f), "0.9 0.812", "0.946 0.835", true, "Overlay");
            UI.Button(ref container, TCGUB, UI.Color("#424242", 1f), Lang("manage"), 12, "0 0", "1 1", $"tc guibtn", TextAnchor.MiddleCenter, "CCCCCC");

            CuiHelper.AddUi(player, container);
        }

        private int RowNumber(int max, int count) => Mathf.FloorToInt(count / max);
        private float[] GetButtonPosition(int rowNumber, int columnNumber)
        {
            float offsetX = 0.05f + (0.096f * columnNumber);
            float offsetY = (0.80f - (rowNumber * 0.064f));

            return new float[] { offsetX, offsetY, offsetX + 0.196f, offsetY + 0.03f };
        }
        private float[] GetButtonPositionP(int rowNumber, int columnNumber)
        {
            float offsetX = 0.05f + (0.126f * columnNumber);
            float offsetY = (0.87f - (rowNumber * 0.064f));

            return new float[] { offsetX, offsetY, offsetX + 0.226f, offsetY + 0.03f };
        }
#endregion

#region Classes
        public static class UI
        {
            public static CuiElementContainer Container(string panel, string color, string min, string max, bool useCursor = false, string parent = "Overlay")
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = color },
                            RectTransform = {AnchorMin = min, AnchorMax = max},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panel
                    }
                };
                return container;
            }
            public static void Panel(ref CuiElementContainer container, string panel, string color, string min, string max, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    CursorEnabled = cursor
                },
                panel);
            }
            public static void Label(ref CuiElementContainer container, string panel, string color, string text, int size, string min, string max, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = min, AnchorMax = max }
                },
                panel);

            }
            public static void Button(ref CuiElementContainer container, string panel, string color, string text, int size, string min, string max, string command, TextAnchor align = TextAnchor.MiddleCenter, string tcolor="FFFFFF")
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    Text = { Text = text, FontSize = size, Align = align, Color = Color(tcolor, 1f) }
                },
                panel);
            }
            public static void Input(ref CuiElementContainer container, string panel, string color, string text, int size, string command, string min, string max)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Align = TextAnchor.MiddleLeft,
                            CharsLimit = 30,
                            Color = color,
                            Command = command + text,
                            FontSize = size,
                            IsPassword = false,
                            Text = text
                        },
                        new CuiRectTransformComponent { AnchorMin = min, AnchorMax = max },
                        new CuiNeedsCursorComponent()
                    }
                });
            }
            public static void Icon(ref CuiElementContainer container, string panel, string color, string imageurl, string min, string max)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = imageurl,
                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                            Color = color
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = min,
                            AnchorMax = max
                        }
                    }
                });
            }
            public static string Color(string hexColor, float alpha)
            {
                if(hexColor.StartsWith("#"))
                {
                    hexColor = hexColor.Substring(1);
                }
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
#endregion

#region config
        private class ConfigData
        {
            public Settings Settings = new Settings();
            public VersionNumber Version;
        }

        private class Settings
        {
            public float cupboardRange = 3f;
            public float turretRange = 30f;
            public bool limitToFriends = false;
            public bool useFriends = false;
            public bool useClans = false;
            public bool useTeams = false;
        }

        protected override void LoadDefaultConfig() => Puts("New configuration file created.");

        private void LoadConfigValues()
        {
            configData = Config.ReadObject<ConfigData>();
            configData.Version = Version;

            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
#endregion
    }
}
