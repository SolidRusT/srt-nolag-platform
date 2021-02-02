#region License (GPL v3)
/*
    Road Finder - Show and list roads, and use road points
    Copyright (c) 2021 RFC1920 <desolationoutpostpve@gmail.com>

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
#endregion License (GPL v3)

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;
using System;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("RoadFinder", "RFC1920", "1.0.5")]
    [Description("Allows admins to show or teleport to roads, and devs to use road points.")]

    class RoadFinder : CovalencePlugin
    {
        private ConfigData configData;
        const string permUse = "roadfinder.use";
        public static Dictionary<string, Road> roads = new Dictionary<string, Road>();

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));
        #endregion

        bool HasPerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, permUse));

        public class Road
        {
            public List<Vector3> points = new List<Vector3>();
            public float width;
            public float offset;
            public int topo;
        }

        void OnServerInitialized()
        {
            permission.RegisterPermission(permUse, this);
            LoadConfigVariables();
            FindRoads();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "You don't have permission to use this command." },
                { "IncorrectUsage", "Incorrect usage! /road [list/name]" },
                { "DoesntExist", "The road '{0}' doesn't exist. Use '/road list' for a list of roads." },
                { "RoadList", "<color=#00ff00>Roads:</color>\n{0}" },
                { "start", "Start" },
                { "end", "End" },
                { "TeleportingTo", "Teleporting to the {1} of : {0} in {2} seconds..." },
                { "TeleportedTo", "Teleported to the {1} of : {0}" }
            }, this);
        }

        [Command("road")]
        void roadCommand(IPlayer player, string command, string[] args)
        {
            if (!HasPerm(player))
            {
                Message(player, "NoPermission");
                return;
            }

            if (args.Length == 0)
            {
                Message(player, "IncorrectUsage");
                return;
            }

            if (args[0].ToLower() == "list")
            {
                foreach (KeyValuePair<string, Road> r in roads)
                {
                    (player.Object as BasePlayer).SendConsoleCommand("ddraw.text", configData.Options.ShowAllTextTime, Color.green, r.Value.points[0] + new Vector3(0, 1.5f, 0), $"<size=20>{r.Key} {Lang("start",null)}</size>");
                    (player.Object as BasePlayer).SendConsoleCommand("ddraw.text", configData.Options.ShowAllTextTime, Color.blue, r.Value.points.Last() + new Vector3(0, 1.5f, 0), $"<size=20>{r.Key} {Lang("end",null)}</size>");
                }
                string roadlist = "";
                foreach(KeyValuePair<string, Road> r in roads)
                {
                    roadlist += r.Key + ": " + r.Value.points[0].ToString() + "\n";
                }
                Message(player, "RoadList", roadlist);
                return;
            }

            int point = 0;
            string roadName = null;
            string send = "start";
            bool tp = true;

            if (args[0].ToLower() == "show" && args.Length > 1)
            {
                List<string> newargs = new List<string>(args);
                newargs.RemoveAt(0);
                roadName = string.Join(" ", newargs).ToLower().Titleize();
                roadName = FixRoadName(roadName);

                (player.Object as BasePlayer).SendConsoleCommand("ddraw.text", configData.Options.ShowOneTextTime, Color.green, roads[roadName].points[0] + new Vector3(0, 1.5f, 0), $"<size=20>{roadName} {Lang("start",null)}</size>");

                if (configData.Options.ShowOneAllPoints)
                {
                    int i = -1;
                    foreach (Vector3 pt in roads[roadName].points)
                    {
                        i++;
                        if (i == 0 || i == roads[roadName].points.Count - 1) continue;
                        string d = Math.Round(Vector3.Distance(roads[roadName].points[i], roads[roadName].points[i - 1]), 2).ToString();
                        (player.Object as BasePlayer).SendConsoleCommand("ddraw.text", 300, Color.yellow, pt + new Vector3(0, 1.5f, 0), $"<size=20>{i.ToString()} ({d})m</size>");
                    }
                }
                (player.Object as BasePlayer).SendConsoleCommand("ddraw.text", configData.Options.ShowOneTextTime, Color.blue, roads[roadName].points.Last() + new Vector3(0, 1.5f, 0), $"<size=20>{roadName} {Lang("end",null)}</size>");
                tp = false;
            }
            else if (args[0].ToLower() == "start")
            {
                List<string> newargs = new List<string>(args);
                newargs.RemoveAt(0);
                roadName = string.Join(" ", newargs).ToLower().Titleize();
                roadName = FixRoadName(roadName);
            }
            else if (args[0].ToLower() == "end")
            {
                List<string> newargs = new List<string>(args);
                newargs.RemoveAt(0);
                roadName = string.Join(" ", newargs).ToLower().Titleize();
                roadName = FixRoadName(roadName);
                point = roads[roadName].points.Count - 1;
                send = "end";
            }
            else
            {
                roadName = string.Join(" ", args).ToLower().Titleize();
                roadName = FixRoadName(roadName);
            }

            if (!roads.ContainsKey(roadName))
            {
                Message(player, "DoesntExist", roadName);
                return;
            }

            if (tp)
            {
                var pos = ToGeneric(roads[roadName].points[point]);
                Message(player, "TeleportingTo", roadName, send, "5");
                timer.Once(5f, () => { player.Teleport(pos.X, pos.Y, pos.Z); Message(player, "TeleportedTo", send, roadName); });
            }
        }

        public string FixRoadName(string roadName)
        {
            if (roadName.Length < 3)
            {
                roadName = "Road " + roadName;
            }
            return roadName;
        }
        GenericPosition ToGeneric(Vector3 vec) => new GenericPosition(vec.x, vec.y, vec.z);

        #region InboundHooks
        private List<string> GetRoadNames() => roads.Keys.ToList();
        private Dictionary<string, Road> GetRoads() => roads;
        private Road GetRoad(string name) => roads[name];
        private List<Vector3> GetRoadPoints(string name) => roads[name].points;
        #endregion

        private void FindRoads()
        {
            foreach (PathList x in TerrainMeta.Path.Roads)
            {
                if (roads.ContainsKey(x.Name)) continue;
                var roadname = x.Name;
                roads.Add(roadname, new Road()
                {
                    topo   = x.Topology,
                    width  = x.Width,
                    offset = x.TerrainOffset
                });

                foreach (var point in x.Path.Points)
                {
                    roads[roadname].points.Add(point);
                }
            }
        }

        #region config
        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            var config = new ConfigData
            {
                Version = Version
            };
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            configData.Version = Version;
            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        public class ConfigData
        {
            public Options Options = new Options();
            public VersionNumber Version;
        }

        public class Options
        {
            public float ShowAllTextTime = 30;
            public float ShowOneTextTime = 60;
            public bool ShowOneAllPoints = true;
        }
        #endregion
    }
}
