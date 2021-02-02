using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Oxide.Core.Database;

namespace Oxide.Plugins {

    [Info("Permission Group Sync", "OldSpice", "1.1.0")]
    [Description("Synchronizes sets of Groups and Permissions with customizable Commands across multiple Servers")]

    public class PermissionGroupSync : CovalencePlugin {

        #region Globals

        private const string PermissionGroupSyncPerm = "permissiongroupsync.admin";
        private readonly Core.MySql.Libraries.MySql _mySql = new Core.MySql.Libraries.MySql();
		private Connection _mySqlConnection = null;
        private string sql_table = "permissiongroupsync";
        private bool PluginLoaded = false;

        #endregion Globals

        #region Config

        Configuration _cfg;

        private void InitializeConfig() {
            try {
                _cfg = Config.ReadObject<Configuration>();
            }
            catch {
                try {
                    LoadDefaultConfig();
                    SaveConfig();
                } catch {
                    PrintError(Lang("ErrorConfig"));
                }
            }
        }
        
        protected override void LoadDefaultConfig() => _cfg = new Configuration();
        protected override void SaveConfig() => Config.WriteObject(_cfg);

        #endregion Config

        #region Lang

        private new void LoadDefaultMessages() {
            lang.RegisterMessages(new Dictionary<string, string> {
                ["ErrorConfig"] = "Error: Could not read config values or invalid / empty options specified.",
                ["ErrorDatabaseConnect"] = "Error: Could not connect to Database {0}. Check Connection and Service.",
                ["ErrorDatabase"] = "Error: Could not read or write from Database {0}: {1}",
                ["ErrorCommand"] = "Error: Could not modify Group Permissions for Player {0}.",
                ["ConnectedDatabase"] = "Connected to Database {0}.",
                ["NoPermissions"] = "You don't have Permissions to use this Command.",
                ["InvalidSyntax"] = "Invalid Syntax. Use /{0} add <steamId> or /{0} remove <steamId>.",
                ["InvalidSteamId"] = "No valid SteamId entered.",
                ["GroupAdded"] = "Added {0} to Group {1}.",
                ["GroupRemoved"] = "Removed {0} from Group {1}.",
                ["NoAction"] = "No action needed because {0} is already in Group or removed.",
                ["NoLongerInDB"] = "Removing {0} from {1} because no longer in Database.",
                ["NotAddedGroup"] = "Permissions could not be added to Group. Retrying.",
            }, this);
        }

        #endregion Lang

        #region Init

        private void Init() {
            InitializeConfig();

			permission.RegisterPermission(PermissionGroupSyncPerm, this);

            foreach (GroupPermission entry in _cfg.GroupPermissions)
                AddCovalenceCommand(entry.CommandName, nameof(Command_PermissionGroupSync));
        }

        void OnServerInitialized() {
            InitializeDatabase();

            if (PluginLoaded) {
                foreach(GroupPermission entry in _cfg.GroupPermissions)
                    InitializeGroup(entry.GroupName, entry.Permissions);
                
                RunTimer();
            }
        }

		private void InitializeDatabase() {
			if (_mySqlConnection == null)
			{
                if (_cfg.DatabaseConfiguration.Host == "") {
                    PrintWarning(Lang("The configuration has not been set up yet. Plugin not laoded."));
                    return;
                }

                try {
				    _mySqlConnection = _mySql.OpenDb(_cfg.DatabaseConfiguration.Host, _cfg.DatabaseConfiguration.Port, _cfg.DatabaseConfiguration.Database, _cfg.DatabaseConfiguration.Username, _cfg.DatabaseConfiguration.Password, this);
                    PluginLoaded = true;

				    Puts(string.Format(Lang("ConnectedDatabase"), _cfg.DatabaseConfiguration.Database));
                } catch {
                    PrintError(string.Format(Lang("ErrorDatabaseConnect"), _cfg.DatabaseConfiguration.Database));
                    return;
                }

                try {
                    var sql = Sql.Builder.Append($"SHOW TABLES LIKE '{sql_table}'");
                    _mySql.Query(sql, _mySqlConnection, list => {
                        if ((list == null) || ((list != null) && (list.Count == 0)))
                        {
                            _mySql.Insert(Sql.Builder.Append("SET SQL_MODE = 'NO_AUTO_VALUE_ON_ZERO'"), _mySqlConnection);
                            _mySql.Insert(Sql.Builder.Append($"CREATE TABLE IF NOT EXISTS {sql_table} ( `id` int(32) NOT NULL, `steamid` varchar(17) DEFAULT NULL, `groupname` varchar(255) DEFAULT NULL ) ENGINE=InnoDB DEFAULT CHARSET=latin1;"), _mySqlConnection);
                            _mySql.Insert(Sql.Builder.Append($"ALTER TABLE {sql_table} ADD PRIMARY KEY (`id`);"), _mySqlConnection);
                            _mySql.Insert(Sql.Builder.Append($"ALTER TABLE {sql_table} MODIFY `id` int(32) NOT NULL AUTO_INCREMENT;"), _mySqlConnection);
                        }
                    });
                } catch (Exception ex) {
                    PrintError(string.Format(Lang("ErrorDatabase"), _cfg.DatabaseConfiguration.Database, ex.Message));
                }
			}
		}

        private void RunTimer() {
			timer.Every(_cfg.PollIntervalSeconds, SyncPermissions);
		}

        #endregion Init

		#region Chat Command
		
        [Command("pgsync")]
        private void Command_PermissionGroupSync(IPlayer player, string command, string[] args) {
            string groupName = null;

            foreach (GroupPermission entry in _cfg.GroupPermissions) {
                if (entry.CommandName.ToLower() == command.ToLower()) {
                    groupName = entry.GroupName;
                    break;
                }
            }

            if (groupName == null) {
                PrintError(Lang("ErrorConfig"));
                return;
            }

			if (!permission.UserHasPermission(player.Id, PermissionGroupSyncPerm))
            {
                player.Reply(Lang("NoPermissions"));
                return;
			}
			if (args == null || args.Length != 2)
            {
                player.Reply(Lang("InvalidSyntax"));
                return;
            }
            else
            {
                List<string> userGroupsCurrent = GetCurrentUserIds(groupName);
                ulong steamId = 0;
                string mode = args[0].ToLower();
                string commandExec, queryExec, response = "";

                try {
                    if (args[1].Length != 17)
                        throw new System.InvalidOperationException("Error");

                    steamId = Convert.ToUInt64(args[1]);
                } catch {
                    player.Reply(Lang("InvalidSteamId"));
                    return;
                }

                switch (mode) {
                    case "add":
                        if (userGroupsCurrent.Contains(steamId.ToString())) {
                            player.Reply(String.Format(Lang("NoAction"), steamId.ToString()));
                            return;
                        }

                        commandExec = String.Format("oxide.usergroup add {0} {1}", steamId.ToString(), groupName);
                        queryExec = $"INSERT INTO {sql_table} (steamid, groupname) SELECT * FROM (SELECT @0, @1) AS tmp WHERE NOT EXISTS (SELECT steamid FROM {sql_table} WHERE steamid = @0) LIMIT 1";
                        response = Lang("GroupAdded");
                        break;

                    case "remove":
                        if (!userGroupsCurrent.Contains(steamId.ToString())) {
                            player.Reply(String.Format(Lang("NoAction"), steamId.ToString()));
                            return;
                        }

                        commandExec = String.Format("oxide.usergroup remove {0} {1}", steamId.ToString(), groupName);
                        queryExec = $"DELETE FROM {sql_table} WHERE steamid=@0";
                        response = Lang("GroupRemoved");
                        break;

                    default:
                        player.Reply(Lang("InvalidSyntax"));
                        return;
                }

                try {
                    server.Command(commandExec);
                    executeQuery(queryExec, steamId.ToString(), groupName);

                    player.Reply(String.Format(response, steamId.ToString(), groupName));
                } catch {
                    player.Reply(String.Format(Lang("ErrorCommand"), steamId.ToString()));
                }
            }
        }

        #endregion Chat Command

        #region Helpers

        private string Lang(string key, string userId = null) => lang.GetMessage(key, this, userId);

		public void SyncPermissions() {
            foreach(GroupPermission groupPermission in _cfg.GroupPermissions) {
                string sqlText = $"SELECT steamid FROM {sql_table} ORDER BY id DESC";

                var sql = Sql.Builder.Append(sqlText);
                _mySql.Query(sql, _mySqlConnection, list => {
                    List<string> userGroupsChecked = new List<string>();
                    List<string> userGroupsCurrent = GetCurrentUserIds(groupPermission.GroupName);

                    foreach (var entry in list) {
                        string playerSteamId = entry["steamid"].ToString();
                        userGroupsChecked.Add(playerSteamId);

                        if ((userGroupsCurrent == null) || (!userGroupsCurrent.Contains(playerSteamId))) {
                            permission.AddUserGroup(playerSteamId, groupPermission.GroupName);
                        }
                    }

                    foreach (string userGroup in userGroupsCurrent) {
                        if (!userGroupsChecked.Contains(userGroup)) {
                            permission.RemoveUserGroup(userGroup, groupPermission.GroupName);
                            PrintWarning(String.Format(Lang("NoLongerInDB"), userGroup, groupPermission.GroupName));
                        }
                    }
                });
            }
        }

        public List<string> GetCurrentUserIds(string groupNameSearch) {
            List<string> userGroupsCurrentTemp = new List<string>();

            foreach (string userGroup in permission.GetUsersInGroup(groupNameSearch)) {
                if (userGroup.Length >= 17)
                    userGroupsCurrentTemp.Add(userGroup.Substring(0, 17));
            }

            return userGroupsCurrentTemp;
        }

        public void InitializeGroup(string groupName, List<string> groupPermissions) {
            if (!permission.GroupExists(groupName)) {
                PrintWarning(String.Format("Permission Group {0} didn't existed. Creating it.", groupName));
                permission.CreateGroup(groupName, "", 0);
            }

            timer.Every(5, () =>
            {
                foreach (string perm in groupPermissions) {
                    permission.GrantGroupPermission(groupName, perm, null);
                }

                string[] perms = permission.GetGroupPermissions(groupName);

                if (perms.Length > 0)
                    return;

                PrintWarning(Lang("NotAddedGroup"));
            });
        }

		public void executeQuery(string query, params object[] data) {
            try {
				var sql = Sql.Builder.Append(query, data);
				_mySql.Update(sql, _mySqlConnection);
			}
			catch (Exception ex)
			{
                PrintError(string.Format(Lang("ErrorDatabase"), _cfg.DatabaseConfiguration.Database, ex.Message));
			}
		}

        #endregion Helpers

        #region Classes

        private class Configuration
        {
            [JsonProperty(PropertyName = "PollIntervalSeconds")]
            public int PollIntervalSeconds { get; set; } = 300;

            [JsonProperty(PropertyName = "DatabaseConfiguration")]
            public DatabaseConfiguration DatabaseConfiguration { get; set; } = new DatabaseConfiguration
            {
                Host = "",
                Port = 3306,
                Username = "username",
                Password = "password",
                Database = "PermissionGroupSync"
            };

            [JsonProperty("GroupPermissions", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<GroupPermission> GroupPermissions { get; set; } = new List<GroupPermission>
            {
                new GroupPermission
                {
                    CommandName = "yourCommand1",
                    GroupName = "PermissionGroupSync",
                    Permissions = new List<string>
                    {
                        "plugin.permission1",
                        "plugin.permission2"
                    }
                },
                new GroupPermission
                {
                    CommandName = "yourCommand2",
                    GroupName = "PermissionGroupSync2",
                    Permissions = new List<string>
                    {
                        "plugin.permission1",
                        "plugin.permission2"
                    }
                }
            };
        }

        public class DatabaseConfiguration
        {
            [JsonProperty(PropertyName = "Host")]
            public string Host { get; set; }

            [JsonProperty(PropertyName = "Port")]
            public int Port { get; set; }

            [JsonProperty(PropertyName = "Username")]
            public string Username { get; set; }

            [JsonProperty(PropertyName = "Password")]
            public string Password { get; set; }

            [JsonProperty(PropertyName = "Database")]
            public string Database { get; set; }
        }

        public class GroupPermission
        {
            [JsonProperty(PropertyName = "CommandName")]
            public string CommandName { get; set; }

            [JsonProperty(PropertyName = "GroupName")]
            public string GroupName { get; set; }

            [JsonProperty(PropertyName = "Permissions")]
            public List<string> Permissions { get; set; } = new List<string>();
        }

        #endregion Classes
    }
}