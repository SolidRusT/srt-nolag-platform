using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Team Ping", "Gonzi", "2.0.2")]
    [Description("Creates a Ping with name of the player who sent the ping and distance to it for all team members.")]
    public class TeamPing : RustPlugin
    {
        #region Fields

        private float mapSize;
        private string permName = "teamping.use";
        private readonly Hash<string, float> cooldowns = new Hash<string, float>();

        #endregion Fields

        #region Configuration

        private Configuration config;

        public class Configuration
        {
            // need permission or not
            public bool requiresPermission;

            // cooldown too wait until next ping (in seconds)
            public int pingCooldown;

            // time to show (in seconds)
            public int timeToShow;

            // max distance for ping (in meters)
            public int maxDistance;

            public string pluginChatPrefix;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    requiresPermission = false,
                    pingCooldown = 15,
                    timeToShow = 10,
                    maxDistance = 250,
                    pluginChatPrefix = "<color=#5af>[TEAM PING]</color>"
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch (Exception e)
            {
                Puts("{0} Exception caught.", e);
                PrintError("The configuration file is corrupted, creating a new one...");
                LoadDefaultConfig();
            }

            SaveConfig();
            return;
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Hooks

        private void OnServerInitialized()
        {
            mapSize = TerrainMeta.Size.x / 2;
        }

        private void Init()
        {
            if (config.requiresPermission) permission.RegisterPermission(permName, this);
        }

        protected override void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["cooldown"] = "Not so fast! (Cooldown aktive)"
            }, this);

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["cooldown"] = "Nicht so schnell! (Cooldown noch aktiv)"
            }, this, "de");
        }

        #endregion Hooks

        #region Commands

        [ConsoleCommand("teamping")]
        private void Ping(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player.Team == null || player.Team.members.Count == 1) return; // if player does not have any team members no ping allowed
            if (config.requiresPermission && !permission.UserHasPermission(player.IPlayer.Id, permName)) return;
            {
                RaycastHit hit;

                if (!Physics.Raycast(DetermineHeadRay(player), out hit, config.maxDistance)) return;

                // check if cooldown is active, if not new cooldown starts to prevent spamming
                if (!cooldowns.ContainsKey(player.UserIDString)) cooldowns.Add(player.UserIDString, 0f);
                if (cooldowns[player.UserIDString] + config.pingCooldown > Interface.Oxide.Now)
                {
                    player.IPlayer.Message(config.pluginChatPrefix + " " + Lang("cooldown", player.UserIDString));
                    return;
                }

                cooldowns[player.UserIDString] = Interface.Oxide.Now;

                foreach (BasePlayer p in BasePlayer.activePlayerList)
                {
                    // check if is same team, if yes draw the ping for all members
                    if (CheckSameTeam(player.userID, p.userID))
                    {
                        double distance = Math.Round(Vector3.Distance(p.transform.position, new Vector3(hit.point.x, hit.point.y, hit.point.z)), 2);
                        string text = "<size=20><color=#ff0000>Ping from </color><color=#0000ff>" + player.displayName + "</color>\n" + distance + " Meters \n<color=#ff0000>▼</color></size>";
                        if (player.IsAdmin)
                        {
                            p.SendConsoleCommand("ddraw.text", config.timeToShow, Color.yellow, new Vector3(hit.point.x, hit.point.y + 0.3f, hit.point.z), text);
                        }
                        else
                        {
                            // player doenst have permissions for ddraw.text so give and revoke it.
                            SetAdminFlag(p, true);
                            p.SendConsoleCommand("ddraw.text", config.timeToShow, Color.yellow, new Vector3(hit.point.x, hit.point.y + 0.3f, hit.point.z), text);
                            SetAdminFlag(p, false);
                        }
                    }
                }
            }
        }

        #endregion Commands

        #region Util

        private bool CheckSameTeam(ulong ply1Id, ulong ply2Id)
        {
            RelationshipManager.PlayerTeam t1;
            RelationshipManager.PlayerTeam t2;
            if (!RelationshipManager.ServerInstance.playerToTeam.TryGetValue(ply1Id, out t1)) return false;
            if (!RelationshipManager.ServerInstance.playerToTeam.TryGetValue(ply2Id, out t2)) return false;
            return t1.teamID == t2.teamID;
        }

        private void SetAdminFlag(BasePlayer player, bool state)
        {
            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, state);
            player.SendNetworkUpdateImmediate();
        }

        private Ray DetermineHeadRay(BasePlayer player)
        {
            var computerStation = player.GetMounted() as ComputerStation;
            if (computerStation != null)
            {
                var controlledEntity = computerStation.currentlyControllingEnt.Get(serverside: true);
                var drone = controlledEntity as Drone;
                if (drone != null)
                    return new Ray(drone.transform.position, drone.transform.forward);

                var cctv = controlledEntity as CCTV_RC;
                if (cctv != null)
                {
                    var direction = Quaternion.Euler(0, cctv.yaw.transform.localEulerAngles.y, -cctv.pitch.transform.localEulerAngles.x) * cctv.transform.forward;
                    return new Ray(cctv.transform.position + direction, direction);
                }
            }

            return player.eyes.HeadRay();
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion Util
    }
}