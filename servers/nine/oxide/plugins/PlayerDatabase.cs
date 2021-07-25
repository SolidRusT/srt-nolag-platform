using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.SQLite.Libraries;
using Oxide.Core.MySql.Libraries;
using Newtonsoft.Json.Linq;
using Oxide.Core.Database;
using Newtonsoft.Json;
using System.Linq;

/*
 *  CHANGELOG
 *  
 *  [2020-06-25 by FAKENINJA] v1.5.9 - Optimization: Removed unnessecary data serialization for SQLite and MySQL
 *  
 *  [2020-06-26 by FAKENINJA] v1.6.0 - Bugfixes: 
 *                                     - Resolved issue with serialization not working on stored list objects in databases.
 *                                     
 *                                     Addition:
 *                                     - Added option to rename MySQL/SQLite database table in config (MySQL/SQLite - Database Table Name)
 * 
 *  [2020-06-28 by FAKENINJA] v1.6.1 - Bugfixes: 
 *                                     - Resolved issue with serialization not working on stored dictionary objects in databases.
 *                                     
 */

namespace Oxide.Plugins
{
    [Info("Player Database", "Reneb / Maintained by FakeNinja", "1.6.2")]
    class PlayerDatabase : CovalencePlugin
    {
        List<string> changedPlayersData = new List<string>();

        DataType dataType = DataType.Files;

        enum DataType
        {
            Files,
            SQLite,
            MySql
        }

        ////////////////////////////////////////////////////////////
        // Configs
        ////////////////////////////////////////////////////////////

        static int dataTypeCfg = 1;

        static string sqlitename = "playerdatabase.db";

        static string sql_host = "localhost";
        static int sql_port = 3306;
        static string sql_db = "rust";
        static string sql_table = "PlayerDatabase";
        static string sql_user = "root";
        static string sql_pass = "toor";


        protected override void LoadDefaultConfig() { }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        void Init()
        {
            CheckCfg<int>("Data Type : 0 (Files) or 1 (SQLite) or 2 (MySQL)", ref dataTypeCfg);
            CheckCfg<string>("SQLite - Database Name", ref sqlitename);
            CheckCfg<string>("MySQL - Host", ref sql_host);
            CheckCfg<int>("MySQL - Port", ref sql_port);
            CheckCfg<string>("MySQL - Database Name", ref sql_db);
            CheckCfg<string>("MySQL/SQLite - Database Table Name", ref sql_table);
            CheckCfg<string>("MySQL - Username", ref sql_user);
            CheckCfg<string>("MySQL - Password", ref sql_pass);
            dataType = (DataType)dataTypeCfg;
            SaveConfig();
            SetupDatabase();
        }

        ////////////////////////////////////////////////////////////
        // General Methods
        ////////////////////////////////////////////////////////////

        void FatalError(string msg)
        {
            Interface.Oxide.LogError(msg);
            if (dataType == DataType.MySql) Sql_conn.Con.Close();
            timer.Once(0.01f, () => Interface.Oxide.UnloadPlugin("PlayerDatabase"));
        }

        string GetMsg(string key, object steamid = null) => lang.GetMessage(key, this, steamid == null ? null : steamid.ToString());

        List<string> KnownPlayers() => dataType == DataType.SQLite ? sqliteData.Keys.ToList() : dataType == DataType.MySql ? sqlData.Keys.ToList() : storedData.knownPlayers.ToList();

        bool isKnownPlayer(string userid) => dataType == DataType.SQLite ? sqliteData.ContainsKey(userid) : dataType == DataType.MySql ? sqlData.ContainsKey(userid) : storedData.knownPlayers.Contains(userid);

        List<string> GetAllKnownPlayers() => KnownPlayers();

        object FindPlayer(string arg)
        {
            ulong steamid = 0L;
            ulong.TryParse(arg, out steamid);
            string lowerarg = arg.ToLower();

            if (steamid != 0L && arg.Length == 17)
            {
                if (!isKnownPlayer(arg)) return GetMsg("No players found matching this steamid.", null);
                else return arg;
            }
            Dictionary<string, string> foundPlayers = new Dictionary<string, string>();
            foreach (var userid in KnownPlayers())
            {
                var d = GetPlayerData(userid, "name");
                if (d != null)
                {
                    var name = (string)d;
                    string lowname = name.ToLower();
                    if (lowname.Contains(lowerarg))
                        if (!foundPlayers.ContainsKey(userid))
                            foundPlayers.Add(userid, name.ToString());

                }
            }
            if (foundPlayers.Count > 1)
            {
                string msg = string.Empty;
                foreach (KeyValuePair<string, string> pair in foundPlayers) { msg += string.Format("{0} {1}\n", pair.Key, pair.Value); }
                return msg;
            }
            foreach (string key in foundPlayers.Keys)
            {
                return key;
            }
            return GetMsg("No players found matching this name.", null);
        }

        ////////////////////////////////////////////////////////////
        // Oxide Hooks
        ////////////////////////////////////////////////////////////

        void OnServerSave()
        {
            SavePlayerDatabase();

            if (dataType == DataType.Files) SaveKnownPlayers();
        }

        void Unload()
        {
            OnServerSave();
        }

        void SetupDatabase()
        {
            LoadData();

            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "No players found matching this steamid.",  "No players found matching this steamid."},
                { "No players found matching this name.","No players found matching this name." }
            }, this);
        }

        void OnUserConnected(IPlayer player) { OnPlayerJoined(player.Id, player.Name, player.Address); }

        void OnPlayerJoined(string steamid, string name, string ip)
        {
            if (!isKnownPlayer(steamid)) { LoadPlayer(steamid); }
            SetPlayerData(steamid, "name", name);
            SetPlayerData(steamid, "ip", ip);
            SetPlayerData(steamid, "steamid", steamid);
        }
            ////////////////////////////////////////////////////////////
            // Save/Load
            ////////////////////////////////////////////////////////////

            void LoadData()
        {
            switch (dataType)
            {
                case DataType.SQLite:
                    LoadSQLite();
                    break;
                case DataType.MySql:
                    LoadMySQL();
                    break;
                default:
                    LoadFiles();
                    break;
            }
        }

        void LoadPlayers()
        {
            foreach (string userid in KnownPlayers())
            {
                try
                {
                    LoadPlayer(userid);
                }
                catch
                {
                    Interface.Oxide.LogWarning("Couldn't load " + userid);
                }
            }
        }

        void LoadPlayer(string userid)
        {
            try
            {
                if (dataType == DataType.SQLite)
                {
                    LoadPlayerSQLite(userid);
                }
                else if (dataType == DataType.MySql)
                {
                    LoadPlayerSQL(userid);
                }
                else
                {
                    LoadPlayerData(userid);
                }
            }
            catch (Exception e)
            {
                LogError(string.Format("Loading {0} got this error: {1}", userid, e.Message));
            }
        }

        void SavePlayerDatabase()
        {
            foreach (string userid in changedPlayersData)
            {
                try
                {
                    if (dataType == DataType.SQLite)
                    {
                        SavePlayerSQLite(userid);
                    }
                    else if (dataType == DataType.MySql)
                    {
                        SavePlayerSQL(userid);
                    }
                    else
                    {
                        SavePlayerData(userid);
                    }
                }
                catch (Exception e)
                {
                    Interface.Oxide.LogWarning(e.Message);
                }
            }
            changedPlayersData.Clear();
        }

        ////////////////////////////////////////////////////////////
        // Set / Get PlayerData
        ////////////////////////////////////////////////////////////

        void SetPlayerData(string userid, string key, object data, bool serializeData = false)
        {
            if (!isKnownPlayer(userid)) LoadPlayer(userid);


            if (data is List<string> || data is Dictionary<object, object> || serializeData == true)
            {
                data = JsonConvert.SerializeObject(data);
            }

            if (dataType == DataType.SQLite)
            {
                if (!isValidColumn(key))
                {
                    CreateNewColumn(key);
                }
                sqliteData[userid][key] = data.ToString();

            }
            else if (dataType == DataType.MySql)
            {
                if (!isValidColumn2(key))
                {
                    CreateNewColumn2(key);
                }
                sqlData[userid][key] = data.ToString();
            }
            else
            {
                var profile = playersData[userid];

                profile[key] = JsonConvert.SerializeObject(data);
                playersData[userid] = profile;
            }

            if (!changedPlayersData.Contains(userid))
                changedPlayersData.Add(userid);
        }

        object GetPlayerDataRaw(string userid, string key)
        {
            if (!isKnownPlayer(userid)) return null;

            if (dataType == DataType.SQLite)
            {
                if (!isValidColumn(key)) return null;
                if (sqliteData[userid] == null) return null;
                if (sqliteData[userid][key] == null) return null;
                return (string)sqliteData[userid][key];
            }
            else if (dataType == DataType.MySql)
            {
                if (!isValidColumn2(key)) return null;
                if (sqlData[userid] == null) return null;
                if (sqlData[userid][key] == null) return null;
                return (string)sqlData[userid][key];
            }
            else
            {
                var profile = playersData[userid];
                if (profile[key] == null) return null;
                return (string)profile[key];
            }
        }
        object GetPlayerData(string userid, string key)
        {
            if (!isKnownPlayer(userid)) return null;

            if (dataType == DataType.SQLite)
            {
                if (!isValidColumn(key)) return null;
                if (sqliteData[userid] == null) return null;
                if (sqliteData[userid][key] == null) return null;
                return sqliteData[userid][key];
            }
            else if (dataType == DataType.MySql)
            {
                if (!isValidColumn2(key)) return null;
                if (sqlData[userid] == null) return null;
                if (sqlData[userid][key] == null) return null;
                return sqlData[userid][key];
            }
            else
            {
                var profile = playersData[userid];
                if (profile[key] == null) return null;
                return JsonConvert.DeserializeObject((string)profile[key]);
            }
        }


        ////////////////////////////////////////////////////////////
        // Files
        ////////////////////////////////////////////////////////////

        public static DataFileSystem datafile = Interface.GetMod().DataFileSystem;

        string subDirectory = "playerdatabase/";

        Hash<string, DynamicConfigFile> playersData = new Hash<string, DynamicConfigFile>();

        StoredData storedData;

        class StoredData
        {
            public HashSet<string> knownPlayers = new HashSet<string>();

            public StoredData() { }
        }

        void LoadFiles()
        {
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("PlayerDatabase");
            }
            catch
            {
                storedData = new StoredData();
            }
            LoadPlayers();
        }

        void LoadPlayerData(string userid)
        {
            if (!storedData.knownPlayers.Contains(userid))
                storedData.knownPlayers.Add(userid);

            string path = subDirectory + userid;
            if (datafile.ExistsDatafile(path)) { }

            DynamicConfigFile profile = Interface.GetMod().DataFileSystem.GetDatafile(path);

            playersData[userid] = profile;
        }

        void SavePlayerData(string userid)
        {
            string path = subDirectory + userid;
            Interface.GetMod().DataFileSystem.SaveDatafile(path);
        }

        void SaveKnownPlayers()
        {
            Interface.GetMod().DataFileSystem.WriteObject("PlayerDatabase", storedData);
        }

        ////////////////////////////////////////////////////////////
        // SQLite
        ////////////////////////////////////////////////////////////

        Core.SQLite.Libraries.SQLite Sqlite = Interface.GetMod().GetLibrary<Core.SQLite.Libraries.SQLite>();
        Connection Sqlite_conn;

        List<string> sqliteColumns = new List<string>();

        Dictionary<string, Hash<string, string>> sqliteData = new Dictionary<string, Hash<string, string>>();

        bool isValidColumn(string column) => sqliteColumns.Contains(column);

        void CreateNewColumn(string column)
        {
            Sqlite.Insert(Core.Database.Sql.Builder.Append($"ALTER TABLE {sql_table} ADD COLUMN '{column}' TEXT"), Sqlite_conn);
            sqliteColumns.Add(column);
        }

        void LoadSQLite()
        {
            try
            {
                Sqlite_conn = Sqlite.OpenDb(sqlitename, this);
                if (Sqlite_conn == null)
                {
                    FatalError("Couldn't open the SQLite PlayerDatabase. ");
                    return;
                }
                Sqlite.Insert(Core.Database.Sql.Builder.Append($"CREATE TABLE IF NOT EXISTS {sql_table} ( id INTEGER NOT NULL PRIMARY KEY UNIQUE, userid TEXT );"), Sqlite_conn);
                Sqlite.Query(Core.Database.Sql.Builder.Append($"PRAGMA table_info({sql_table});"), Sqlite_conn, list =>
                {
                    if (list == null)
                    {
                        FatalError("Couldn't get columns. Database might be corrupted.");
                        return;
                    }
                    foreach (var entry in list)
                    {
                        sqliteColumns.Add((string)entry["name"]);
                    }

                });
                Sqlite.Query(Core.Database.Sql.Builder.Append($"SELECT userid from {sql_table}"), Sqlite_conn, list =>
                {
                    if (list == null) return;
                    foreach (var entry in list)
                    {
                        string steamid = (string)entry["userid"];
                        if (steamid != "0")
                        {
                            sqliteData.Add(steamid, new Hash<string, string>());
                        }
                    }
                    LoadPlayers();
                });
            }
            catch (Exception e)
            {
                FatalError(e.Message);
            }
        }

        void LoadPlayerSQLite(string userid)
        {
            if (!sqliteData.ContainsKey(userid)) { sqliteData.Add(userid, new Hash<string, string>()); }
            bool newplayer = true;
            Sqlite.Query(Core.Database.Sql.Builder.Append($"SELECT * from {sql_table} WHERE userid == {userid}"), Sqlite_conn, list =>
            {
                if (list != null)
                {
                    foreach (var entry in list)
                    {
                        foreach (var p in entry)
                        {
                            if (p.Value is string)
                            {
                                sqliteData[userid][p.Key] = (string)p.Value;
                            }
                        }
                        newplayer = false;
                    }
                }
                if (newplayer)
                {
                    sqliteData[userid]["userid"] = userid;
                    Sqlite.Insert(Core.Database.Sql.Builder.Append($"INSERT OR REPLACE INTO {sql_table} ( userid ) VALUES ( {userid} )"), Sqlite_conn);

                    changedPlayersData.Add(userid);
                }
            });
        }

        void SavePlayerSQLite(string userid)
        {
            var values = sqliteData[userid];
            var i = values.Count;
            string arg = string.Empty;
            var parms = new List<object>();
            foreach (var c in values)
            {
                arg += string.Format("{0}`{1}` = @{2}", arg == string.Empty ? string.Empty : ",", c.Key, parms.Count.ToString());
                parms.Add(c.Value);
            }
            Sqlite.Insert(Core.Database.Sql.Builder.Append($"UPDATE {sql_table} SET {arg} WHERE userid = {userid}", parms.ToArray()), Sqlite_conn);
        }


        ////////////////////////////////////////////////////////////
        // MySQL
        ////////////////////////////////////////////////////////////

        Core.MySql.Libraries.MySql Sql = Interface.GetMod().GetLibrary<Core.MySql.Libraries.MySql>();
        Connection Sql_conn;

        List<string> sqlColumns = new List<string>();

        Dictionary<string, Hash<string, string>> sqlData = new Dictionary<string, Hash<string, string>>();

        bool isValidColumn2(string column) => sqlColumns.Contains(column);

        void CreateNewColumn2(string column)
        {
            Sql.Insert(Core.Database.Sql.Builder.Append($"ALTER TABLE `{sql_table}` ADD `{column}` LONGTEXT"), Sql_conn);
            sqlColumns.Add(column);
        }

        void LoadMySQL()
        {
            try
            {
                Sql_conn = Sql.OpenDb(sql_host, sql_port, sql_db, sql_user, sql_pass, this);
                if (Sql_conn == null || Sql_conn.Con == null)
                {
                    FatalError("Couldn't open the SQLite PlayerDatabase: " + Sql_conn.Con.State.ToString());
                    return;
                }
                Sql.Insert(Core.Database.Sql.Builder.Append("SET NAMES utf8mb4"), Sql_conn);
                Sql.Insert(Core.Database.Sql.Builder.Append($"CREATE TABLE IF NOT EXISTS {sql_table} ( `id` int(11) NOT NULL, `userid` VARCHAR(17) NOT NULL );"), Sql_conn);
                Sql.Query(Core.Database.Sql.Builder.Append($"desc {sql_table};"), Sql_conn, list =>
                {
                    if (list == null)
                    {
                        FatalError("Couldn't get columns. Database might be corrupted.");
                        return;
                    }
                    foreach (var entry in list)
                    {
                        sqlColumns.Add((string)entry["Field"]);
                    }

                });
                Sql.Query(Core.Database.Sql.Builder.Append($"SELECT userid from {sql_table}"), Sql_conn, list =>
                {
                    if (list == null) return;
                    foreach (var entry in list)
                    {
                        string steamid = (string)entry["userid"];
                        if (steamid != "0")
                            sqlData.Add(steamid, new Hash<string, string>());
                    }
                    LoadPlayers();
                });
            }
            catch (Exception e)
            {
                FatalError(e.Message);
            }
        }

        void LoadPlayerSQL(string userid)
        {
            if (!sqlData.ContainsKey(userid)) sqlData.Add(userid, new Hash<string, string>());
            bool newplayer = true;
            Sql.Query(Core.Database.Sql.Builder.Append($"SELECT * from {sql_table} WHERE `userid` = '{userid}'"), Sql_conn, list =>
            {
                if (list != null)
                {
                    foreach (var entry in list)
                    {
                        foreach (var p in entry)
                        {
                            if (p.Value is string)
                            {
                                sqlData[userid][p.Key] = (string)p.Value;
                            }
                        }
                        newplayer = false;
                    }
                }
                if (newplayer)
                {
                    sqlData[userid]["userid"] = userid;
                    Sql.Insert(Core.Database.Sql.Builder.Append($"INSERT IGNORE INTO {sql_table} ( userid ) VALUES ( {userid} )"), Sql_conn);

                    changedPlayersData.Add(userid);
                }
            });
        }

        void SavePlayerSQL(string userid)
        {
            var values = sqlData[userid];

            string arg = string.Empty;
            var parms = new List<object>();
            foreach (var c in values)
            {
                arg += string.Format("{0}`{1}` = @{2}", arg == string.Empty ? string.Empty : ",", c.Key, parms.Count.ToString());
                parms.Add(c.Value);
            }

            Sql.Insert(Core.Database.Sql.Builder.Append($"UPDATE {sql_table} SET {arg} WHERE userid = {userid}", parms.ToArray()), Sql_conn);
        }
    }
}