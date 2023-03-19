using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Oxide.Core.Database;

namespace Oxide.Plugins {

    [Info("Permission Group Sync", "OldSpice", "2.0.3")]
    [Description("Synchronizes sets of Groups and Permissions with customizable Commands across multiple Servers")]

    public class PermissionGroupSync : CovalencePlugin {

        #region Globals

        private const string PermissionGroupSyncPerm = "permissiongroupsync";
        private const string PermissionGroupSyncPermAdmin = "permissiongroupsync.globaladmin";
        private const string sql_table = "permissiongroupsync";
        private const string ServerIdAll = "_ALL";

        private readonly Core.MySql.Libraries.MySql _mySql = new Core.MySql.Libraries.MySql();
		private Connection _mySqlConnection = null;

        private bool PluginLoaded = false;

        #endregion Globals

        #region Config

        Configuration _cfg;

        private void InitializeConfig() {
            try {
                _cfg = Config.ReadObject<Configuration>();
            } catch {
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
                ["InvalidSyntax"] = "Invalid Syntax. Use /{0} <add or remove> <steamId> <optional:serverId>.",
                ["InvalidSteamId"] = "No valid SteamId provided.",
                ["GroupAdded"] = "Added {0} to Group {1} for ServerId: {2}",
                ["GroupRemoved"] = "Removed {0} from Group {1}  for ServerId: {2}",
                ["NoAction"] = "No action needed because {0} is already in Group or removed.",
                ["PermissionChanged"] = "Your ingame Permissions have changed. Please relog so they can apply.",
                ["NoLongerInDB"] = "Removing {0} from {1} because no longer in Database.",
                ["NotRemovedProtected"] = "Could not remove {0} from Group {1} because Group is protected.",
                ["NotAddedGroup"] = "Permissions could not be added to Group. Retrying.",
                ["PermissionChangedLog"] = "Changed AuthLevel for User {0} to {1}.",
            }, this);
        }

        #endregion Lang

        #region Init

        private void Init() {
            InitializeConfig();

			permission.RegisterPermission(PermissionGroupSyncPermAdmin, this);

            foreach (GroupPermission entry in _cfg.GroupPermissions) {
                AddCovalenceCommand(entry.CommandName, nameof(Command_PermissionGroupSync));

                if (entry.PermissionUse)
                    permission.RegisterPermission($"{PermissionGroupSyncPerm}.{entry.CommandName}", this);
            }
        }

        void OnServerInitialized() {
            InitializeDatabase();

            if (PluginLoaded) {
                foreach(GroupPermission entry in _cfg.GroupPermissions)
                    InitializeGroup(entry.GroupName, entry.PermissionsOxide);
                
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
                            _mySql.Insert(Sql.Builder.Append($"CREATE TABLE IF NOT EXISTS {sql_table} ( `id` int(32) NOT NULL, `steamid` varchar(17) DEFAULT NULL, `groupname` varchar(255) DEFAULT NULL, `serverid` VARCHAR(255) NOT NULL DEFAULT '{ServerIdAll}' ) ENGINE=InnoDB DEFAULT CHARSET=latin1;"), _mySqlConnection);
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
		
        private void Command_PermissionGroupSync(IPlayer player, string command, string[] args) {
            GroupPermission groupPermission = null;

            foreach (GroupPermission entry in _cfg.GroupPermissions) {
                if (entry.CommandName.ToLower() == command.ToLower()) {
                    groupPermission = entry;
                    break;
                }
            }
            if (groupPermission == null) {
                PrintError(Lang("ErrorConfig"));
                return;
            }
			if ((!permission.UserHasPermission(player.Id, PermissionGroupSyncPermAdmin) || ((groupPermission.PermissionUse) && (!permission.UserHasPermission(player.Id, $"{PermissionGroupSyncPerm}.{groupPermission.GroupName}"))))) {
                player.Reply(Lang("NoPermissions"));
                return;
			}
            
			if (args == null || args.Length < 2 || args.Length > 3) {
                player.Reply(Lang("InvalidSyntax"));
                return;
            } else {
                
                HashSet<string> usersGroupsCurrent = GetCurrentUserIds(groupPermission.GroupName);

                string steamId = args[1];
                ulong steamIdNumber = CheckUserSteamId(steamId);
                string mode = args[0].ToLower();
                string commandExec, queryExec, response = "";
                string serverId = ServerIdAll;

                if (steamIdNumber == 0) {
                    player.Reply(Lang("InvalidSteamId"));
                    return;
                }

                if ((args.Length == 3) && (args[2] != null))
                    serverId = args[2];

                List<GroupPermissionDb> groupPermissionsDb = new List<GroupPermissionDb>(); 
                string sqlTextQuery = String.Format("SELECT steamid, groupname, serverid FROM {0} ORDER BY groupname ASC, serverid ASC", sql_table);

                var sqlQuery = Sql.Builder.Append(sqlTextQuery);
                _mySql.Query(sqlQuery, _mySqlConnection, list => {
                    if (list == null)
                        return;

                    foreach (var entry in list) {
                        GroupPermissionDb groupPermissionDb = new GroupPermissionDb
                        {
                            SteamId = CheckUserSteamId(entry["steamid"].ToString()),
                            GroupName = entry["groupname"].ToString(),
                            ServerId = entry["serverid"].ToString(),
                        };
                        groupPermissionsDb.Add(groupPermissionDb);
                    }

                    switch (mode) {
                        case "add":
                            if (groupPermissionsDb.Where(x => ((x.SteamId == steamIdNumber) && (x.GroupName == groupPermission.GroupName) && (x.ServerId == serverId))).Count() > 0) {
                                player.Reply(String.Format(Lang("NoAction"), steamId));
                                return;
                            }

                            queryExec = $"INSERT INTO {sql_table} (steamid, groupname, serverid) SELECT * FROM (SELECT @0, @1, @2) AS tmp WHERE NOT EXISTS (SELECT steamid FROM {sql_table} WHERE steamid=@0 AND groupname=@1 AND serverid=@2) LIMIT 1";
                            response = Lang("GroupAdded");
                            break;

                        case "remove":
                            if (groupPermissionsDb.Where(x => ((x.SteamId == steamIdNumber) && (x.GroupName == groupPermission.GroupName) && (x.ServerId == serverId))).Count() == 0) {
                                player.Reply(String.Format(Lang("NoAction"), steamId));
                                return;
                            }

                            queryExec = $"DELETE FROM {sql_table} WHERE steamid=@0 AND groupname=@1 AND serverid=@2";
                            response = Lang("GroupRemoved");
                            break;

                        default:
                            player.Reply(Lang("InvalidSyntax"));
                            return;
                    }

                    try {
                        executeQuery(queryExec, steamId.ToString(), groupPermission.GroupName, serverId);
                        player.Reply(String.Format(response, steamId.ToString(), groupPermission.GroupName, serverId));
                    } catch (Exception ex) {
                        player.Reply(String.Format(Lang("ErrorDatabase"), sql_table, ex.Message));
                    }

                });
            }
        }

        #endregion Chat Command

        #region Helpers

        private string Lang(string key, string userId = null) => lang.GetMessage(key, this, userId);

		public void SyncPermissions() {

            List<GroupPermissionDb> groupPermissionsDb = new List<GroupPermissionDb>(); 
            string sqlTextQuery = String.Format("SELECT steamid, groupname, serverid FROM {0} ORDER BY groupname ASC, serverid ASC", sql_table);

            var sqlQuery = Sql.Builder.Append(sqlTextQuery);
            _mySql.Query(sqlQuery, _mySqlConnection, list => {
                if (list == null)
                    return;

                foreach (var entry in list) {
                    GroupPermissionDb groupPermissionDb = new GroupPermissionDb
                    {
                        SteamId = CheckUserSteamId(entry["steamid"].ToString()),
                        GroupName = entry["groupname"].ToString(),
                        ServerId = entry["serverid"].ToString(),
                    };
                    groupPermissionsDb.Add(groupPermissionDb);
                }

                HashSet<string> queryList = new HashSet<string>();
                HashSet<string> commandList = new HashSet<string>();
                bool executeWriteCommand = false;

                foreach(GroupPermission groupPermission in _cfg.GroupPermissions) {

                    List<GroupPermissionDb> usersGroupChecked = new List<GroupPermissionDb>();
                    HashSet<string> usersGroupCurrent = GetCurrentUserIds(groupPermission.GroupName);

                    foreach (GroupPermissionDb groupPermissionDb in groupPermissionsDb.Where(x => ((x.GroupName == groupPermission.GroupName) && ((x.ServerId == ServerIdAll) || (x.ServerId.Contains(_cfg.ServerId)))))) {
                        string steamId = groupPermissionDb.SteamId.ToString();
                        ulong steamIdNumber = groupPermissionDb.SteamId;
                        bool skipPermission = false;
                        bool executeAdditionalCommands = false;

                        if (steamIdNumber == 0)
                            break;

                        if ((!groupPermission.OverrideServerIdCheck) && (groupPermission.ExtendedPermissionHandling)) {
                            if (groupPermissionDb.ServerId == ServerIdAll) {
                                foreach (GroupPermissionDb groupPermissionDbTmp in groupPermissionsDb.Where(x => ((x.SteamId == groupPermissionDb.SteamId) && (x.ServerId != ServerIdAll) && (x.ServerId == _cfg.ServerId)))) {
                                    if (_cfg.GroupPermissions.Where(x => ((x.GroupName == groupPermissionDbTmp.GroupName) && (x.ExtendedPermissionHandling))).Count() > 0) {
                                        skipPermission = true;
                                        break;
                                    }
                                }
                            }
                        }

                        if ((CheckGroupExclude(groupPermission, steamId)) || (skipPermission))
                            continue;

                        if ((!CheckUserPermissionRust(steamIdNumber, groupPermission.PermissionRust)) && (groupPermission.ExtendedPermissionHandling)) {
                            if(SetUserPermissionRust(steamIdNumber, groupPermission.PermissionRust)) {
                                executeWriteCommand = true;
                                executeAdditionalCommands = true;
                            }
                        }

                        usersGroupChecked.Add(groupPermissionDb);
                        if ((usersGroupCurrent == null) || (!usersGroupCurrent.Contains(steamId))) {
                            permission.AddUserGroup(steamId, groupPermission.GroupName);
                            executeAdditionalCommands = true;
                        }

                        if ((groupPermission.GroupsRemove.Count > 0) && (groupPermission.ExtendedPermissionHandling))
                            queryList = RemoveGroups(groupPermission, steamId);

                        if ((queryList.Count > 0) || (executeAdditionalCommands)) {
                            foreach (string additionalCommand in groupPermission.AdditionalCommands) {
                                string additionalCommandRun = String.Format(additionalCommand, steamId);
                                commandList.Add(additionalCommandRun);
                            }
                        }
                    }

                    foreach (string userGroup in usersGroupCurrent) {
                        if (CheckGroupExclude(groupPermission, userGroup))
                            continue;

                        ulong steamIdNumber = CheckUserSteamId(userGroup);

                        if (steamIdNumber == 0)
                            return;

                        if (usersGroupChecked.Where(x => ((x.SteamId == steamIdNumber) && (x.GroupName == groupPermission.GroupName) && ((x.ServerId == ServerIdAll) || (x.ServerId.Contains(_cfg.ServerId))))).Count() == 0) {
                            if (groupPermission.ExtendedPermissionHandling) {
                                if (!CheckUserPermissionRust(steamIdNumber, 0)) {
                                    bool _tmp = SetUserPermissionRust(steamIdNumber, 0);
                                }

                                permission.RemoveUserGroup(userGroup, groupPermission.GroupName);
                                PrintWarning(String.Format(Lang("NoLongerInDB"), userGroup, groupPermission.GroupName));
                            }
                        }
                    }
                }

                if (queryList.Count > 0) {
                    try {
                        executeQuery(String.Join("; ", queryList));
                    } catch (Exception ex) {
                        PrintError(String.Format(Lang("ErrorDatabase"), sql_table, ex.Message));
                    }
                }

                if (executeWriteCommand)
                    commandList.Add("writecfg");

                foreach (string command in commandList)
                    server.Command(command);
            });
        }

        public bool SetUserPermissionRust(ulong steamIdNumber, int permissionRust) {
            if ((steamIdNumber == null) || (steamIdNumber == 0) || (permissionRust == null))
                return false;
            
            IPlayer player = covalence.Players.FindPlayerById(steamIdNumber.ToString());

            if (player == null)
                return false;

            switch(permissionRust) {
                case 0:
                    ServerUsers.Set(steamIdNumber, ServerUsers.UserGroup.None, "", "");
                break;
                case 1:
                    ServerUsers.Set(steamIdNumber, ServerUsers.UserGroup.Moderator, "", "");
                break;
                case 2:
                    ServerUsers.Set(steamIdNumber, ServerUsers.UserGroup.Owner, "", "");
                break;
                default:
                    PrintError(Lang("ErrorConfig"));
                    return false;
            }

            foreach (BasePlayer playerRespond in BasePlayer.activePlayerList.Where(x => x.userID == steamIdNumber))
                playerRespond.ChatMessage(Lang("PermissionChanged"));

            Puts(String.Format(Lang("PermissionChangedLog"), steamIdNumber.ToString(), permissionRust.ToString()));

            return true;
        }

        public bool CheckGroupExclude(GroupPermission groupPermission, string steamId) {

            HashSet<string> userGroups = GetCurrentUserGroups(steamId);

            foreach (string userGroup in userGroups) {
                if (groupPermission.GroupsCheckExcludedSync.Contains(userGroup, StringComparer.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public bool CheckUserPermissionRust(ulong steamIdNumber, int permissionNumber) {
            try {
                var userObject = ServerUsers.Get(steamIdNumber);

                switch(permissionNumber) {
                    case 0:
                    if (userObject.group == ServerUsers.UserGroup.None)
                        return true;
                    break;
                    case 1:
                    if (userObject.group == ServerUsers.UserGroup.Moderator)
                        return true;
                    break;
                    case 2:
                    if (userObject.group == ServerUsers.UserGroup.Owner)
                        return true;
                    break;
                }

            } catch {
                if (permissionNumber == 0)
                    return true;
            }

            return false;
        }

        public ulong CheckUserSteamId(string steamId) {
            if ((steamId.Length == 17) && (steamId.StartsWith("7656119"))) {
                try { return Convert.ToUInt64(steamId); }
                catch { return 0; }
            }

            return 0;
        }

        public HashSet<string> GetCurrentUserIds(string groupNameSearch) {
            HashSet<string> usersGroupCurrentTemp = new HashSet<string>();

            foreach (string userGroup in permission.GetUsersInGroup(groupNameSearch)) {
                if (userGroup.Length >= 17)
                    usersGroupCurrentTemp.Add(userGroup.Substring(0, 17));
            }

            return usersGroupCurrentTemp;
        }

        public HashSet<string> GetCurrentUserGroups(string steamIdSearch) {
            HashSet<string> userGroupsCurrentTemp = new HashSet<string>(permission.GetUserGroups(steamIdSearch));
            return userGroupsCurrentTemp;
        }

        public void InitializeGroup(string groupName, HashSet<string> groupPermissions) {
            if (!permission.GroupExists(groupName)) {
                PrintWarning(String.Format("Permission Group {0} didn't existed. Creating it.", groupName));
                permission.CreateGroup(groupName, "", 0);
            }

            timer.Every(5, () =>
            {
                if (groupPermissions.Count == 0)
                    return;
                
                foreach (string perm in groupPermissions) {
                    permission.GrantGroupPermission(groupName, perm, null);
                }

                string[] perms = permission.GetGroupPermissions(groupName);

                if (perms.Length > 0)
                    return;

                PrintWarning(Lang("NotAddedGroup"));

            });
        }

        public HashSet<string> RemoveGroups(GroupPermission groupPermission, string steamId) {
            HashSet<string> queryList = new HashSet<string>();
            HashSet<string> currentUserGroups = GetCurrentUserGroups(steamId);

            bool isProtected = false;

            foreach (string groupRemove in groupPermission.GroupsRemove) {
                if (currentUserGroups.Contains(groupRemove, StringComparer.OrdinalIgnoreCase)) {
                    foreach(var checkProtectedGroup in _cfg.GroupPermissions.Where(x => x.GroupName.ToLower() == groupRemove.ToLower())) {
                        if (checkProtectedGroup.ProtectedGroup) {
                            isProtected = true;
                            break;
                        }
                    }

                    if (!isProtected) {
                        permission.RemoveUserGroup(steamId, groupRemove);
                        queryList.Add(String.Format("DELETE FROM {0} WHERE steamid={1} AND groupname='{2}' AND ( serverid='{3}' OR serverid LIKE '%{4}%' )", sql_table, steamId, groupRemove, ServerIdAll, _cfg.ServerId));
                    } else {
                        PrintWarning(String.Format(Lang("NotRemovedProtected"), steamId, groupRemove));
                        isProtected = false;
                    }
                }
            }

            return queryList;
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

		public void executeQuery(string query) {
            try {
				var sql = Sql.Builder.Append(query);
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
            [JsonProperty(PropertyName = "ServerId")]
            public string ServerId { get; set; } = "yourid";

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

            [JsonProperty(PropertyName = "GroupPermissions", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<GroupPermission> GroupPermissions { get; set; } = new List<GroupPermission>
            {
                new GroupPermission
                {
                    CommandName = "yourCommand1",
                    GroupName = "PermissionGroupSync",
                    ExtendedPermissionHandling = true,
                    PermissionUse = true,
                    ProtectedGroup = false,
                    OverrideServerIdCheck = false,
                    GroupsCheckExcludedSync = new HashSet<string>
                    {
                        "admin",
                        "testgroup"
                    },
                    GroupsRemove = new HashSet<string>
                    {
                        "moderator"
                    },
                    PermissionRust = 0,
                    PermissionsOxide = new HashSet<string>
                    {
                        "plugin.permission1",
                        "plugin.permission2"
                    },
                    AdditionalCommands = new HashSet<string>
                    {
                        "writefg"
                    }
                },
                new GroupPermission
                {
                    CommandName = "yourCommand2",
                    GroupName = "PermissionGroupSync2",
                    ExtendedPermissionHandling = false,
                    PermissionUse = false,
                    ProtectedGroup = false,
                    OverrideServerIdCheck = false,
                    GroupsCheckExcludedSync = new HashSet<string>
                    {
                        "admin",
                        "testgroup"
                    },
                    GroupsRemove = new HashSet<string>
                    {
                        "moderator"
                    },
                    PermissionRust = 0,
                    PermissionsOxide = new HashSet<string>
                    {
                        "plugin.permission1",
                        "plugin.permission2"
                    },
                    AdditionalCommands = new HashSet<string>
                    {
                        "writefg"
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

            [JsonProperty(PropertyName = "ExtendedPermissionHandling")]
            public bool ExtendedPermissionHandling { get; set; }

            [JsonProperty(PropertyName = "PermissionUse")]
            public bool PermissionUse { get; set; }

            [JsonProperty(PropertyName = "ProtectedGroup")]
            public bool ProtectedGroup { get; set; }

            [JsonProperty(PropertyName = "OverrideServerIdCheck")]
            public bool OverrideServerIdCheck { get; set; }

            [JsonProperty(PropertyName = "GroupsCheckExcludedSync")]
            public HashSet<string> GroupsCheckExcludedSync { get; set; } = new HashSet<string>();

            [JsonProperty(PropertyName = "GroupsRemove")]
            public HashSet<string> GroupsRemove { get; set; } = new HashSet<string>();

            [JsonProperty(PropertyName = "PermissionsRust")]
            public int PermissionRust { get; set; }

            [JsonProperty(PropertyName = "PermissionsOxide")]
            public HashSet<string> PermissionsOxide { get; set; } = new HashSet<string>();

            [JsonProperty(PropertyName = "AdditionalCommands")]
            public HashSet<string> AdditionalCommands { get; set; } = new HashSet<string>();
        }

        public class GroupPermissionDb
        {
            [JsonProperty(PropertyName = "SteamId")]
            public ulong SteamId { get; set; }

            [JsonProperty(PropertyName = "GroupName")]
            public string GroupName { get; set; }

            [JsonProperty(PropertyName = "ServerId")]
            public string ServerId { get; set; }
        }

        #endregion Classes
    }
}