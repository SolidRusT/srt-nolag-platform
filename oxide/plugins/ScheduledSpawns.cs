using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using System.Globalization;
using Newtonsoft.Json;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("Scheduled Spawns", "1AK1", "1.0.2")]
    [Description("Spawn any item or prefab on a schedule")]
    internal class ScheduledSpawns : CovalencePlugin
    {
        #region Vars

        private Dictionary<string, Timer> prefabTimers = new Dictionary<string, Timer>();
        private Dictionary<string, Timer> itemTimers = new Dictionary<string, Timer>();
        private const string permUse = "scheduledspawns.use";

        #endregion

        #region Config       

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Default timer (s)")]
            public float DefaultTimer { get; set; }

            [JsonProperty(PropertyName = "Check radius")]
            public float CheckRadius { get; set; }

        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                DefaultTimer = 300f,
                CheckRadius = 1f
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        #endregion Config

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayersOnly"] = "Command '{0}' can only be used by players",
                ["UsageScheduledSpawns"] = "Usage {0} <item|prefab> <add|remove|list|start|stop>",
                ["UsageItemAddCommand"] = "Usage {0} item add <itemshortname> <timer> <name>",
                ["UsagePrefabAddCommand"] = "Usage {0} prefab add <prefabpath> <timer> <name>",
                ["UsageItemRemoveCommand"] = "Usage {0} item remove <name>",
                ["UsagePrefabRemoveCommand"] = "Usage {0} prefab remove <name>",
                ["AlreadyExist"] = "{0} already exist",
                ["NameDoesNotExist"] = "{0} does not exist",
                ["NameAdded"] = "{0} added successfully",
                ["NameRemoved"] = "{0} removed successfully",
                ["ItemSpawnStart"] = "Item spawn started",
                ["ItemSpawnStop"] = "Item spawn stopped",
                ["PrefabSpawnStart"] = "Prefab spawn started",
                ["PrefabSpawnStop"] = "Prefab spawn stopped",
                ["UnknownCommand"] = "Unknown command",
            }, this);
        }

        #endregion Localization

        #region Oxide Hooks

        private void Init()
        {
            AddCovalenceCommand("ss", "SpawnCommand");
            permission.RegisterPermission(permUse, this);
        }

        private void OnNewSave(string file)
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(filename))
            {
                Interface.Oxide.DataFileSystem.GetFile(filename).Clear();
                Interface.Oxide.DataFileSystem.GetFile(filename).Save();
            }
        }

        private void OnServerInitialized(bool initial)
        {
            LoadData();
            SpawnPrefabsInData();
            SpawnItemsInData();
        }

        #endregion

        #region Core

        private void SpawnPrefabsInData()
        {
            foreach (var data in _data._prefabContainer)
            {
                Vector3 position = StringToVector3(data.Value["Location"]);

                float time = configData.DefaultTimer;

                try
                {
                    time = float.Parse(data.Value["Timer"], CultureInfo.InvariantCulture.NumberFormat);
                }
                catch (FormatException)
                {
                    Puts("'{0}' is invalid using {1}.", data.Value["Timer"], configData.DefaultTimer);
                }
            
                string name = data.Key;

                if (data.Value["Prefab"].IsNullOrEmpty())
                {
                    continue;
                }

                if (!prefabTimers.ContainsKey(name))
                {
                    prefabTimers[name] = timer.Every(time, () =>
                    {
                        var entities = FindEntities<BaseEntity>(position, configData.CheckRadius);

                        if (entities.Count == 0)
                        {
                            SpawnPrefab(data.Value["Prefab"], position);
                        }
                    });
                }
            }
        }

        private void SpawnItemsInData()
        {
            foreach (var data in _data._itemContainer)
            {
                Vector3 position = StringToVector3(data.Value["Location"]);
                position.y = position.y + 1;

                float time = configData.DefaultTimer;

                try
                {
                    time = float.Parse(data.Value["Timer"], CultureInfo.InvariantCulture.NumberFormat);
                }
                catch (FormatException)
                {
                    Puts("'{0}' is invalid using {1}.", data.Value["Timer"], configData.DefaultTimer);
                }

                string name = data.Key;

                if (data.Value["ItemShortName"].IsNullOrEmpty())
                {
                    continue;
                }

                if (!itemTimers.ContainsKey(name))
                {
                    itemTimers[name] = timer.Every(time, () =>
                    {
                        var entities = FindEntities<BaseEntity>(position, configData.CheckRadius);

                        if (entities.Count == 0)
                        {
                            SpawnItem(data.Value["ItemShortName"], position);
                        }                      
                    });
                }
            }
        }

        private void SpawnPrefab(string prefabname, Vector3 position)
        {
            var entity = GameManager.server.CreateEntity(prefabname, position) as BaseEntity;

            if (entity == null)
            {
                return;
            }
            else
            {
                entity.Spawn();
            }
        }

        private void SpawnItem(string itemname, Vector3 position)
        {
            Item item = ItemManager.CreateByName(itemname, 1);

            if (item == null)
            {
                return;
            }
            else
            {
                item.CreateWorldObject(position);
            }
        }

        #endregion

        #region Commands

        private void SpawnCommand(IPlayer player, string command, string[] args)
        {

            if (player.IsServer)
            {
                Message(player, "PlayersOnly", command);
                return;
            }

            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args == null || args.Length <= 0)
            {
                Message(player, "UsageScheduledSpawns", command);
                return;
            }

            var bplayer = (BasePlayer)player.Object;
            var position = bplayer.transform.position;
            var newPos = GetGroundPosition(position);


            switch (args[0].ToLower())
            {
                case "item":
                    switch (args[1].ToLower())
                    {
                        case "add":
                            if (args.Length != 5)
                            {
                                Message(player, "UsageItemAddCommand", command);
                                
                            }
                            else
                            {
                                if (_data._itemContainer.ContainsKey(args[4]))
                                {
                                    Message(player, "AlreadyExist", args[4]);
                                }
                                else
                                {
                                    Dictionary<string, string> prefabsContainer2 = new Dictionary<string, string>();

                                    prefabsContainer2["ItemShortName"] = args[2];
                                    prefabsContainer2["Location"] = newPos.ToString();
                                    prefabsContainer2["Timer"] = args[3];

                                    _data._itemContainer.Add(args[4], prefabsContainer2);
                                    SaveData();
                                    Message(player, "NameAdded", args[4]);
                                }
                            }
                            break;
                        case "remove":
                            if (args.Length != 3)
                            {
                                Message(player, "UsageItemRemoveCommand", command);
                            }
                            else
                            {
                                if (!_data._itemContainer.ContainsKey(args[2]))
                                {
                                    Message(player, "NameDoesNotExist", args[2]);
                                }
                                else
                                {
                                    _data._itemContainer.Remove(args[2]);
                                    SaveData();
                                    if (itemTimers.ContainsKey(args[2]))
                                    {
                                        itemTimers[args[2]].Destroy();
                                    }
                                    Message(player, "NameRemoved", args[2]);
                                }
                            }
                            break;
                        case "list":
                            foreach (var entry in _data._itemContainer)
                            {
                                player.Message(entry.Key);
                            }
                            break;
                        case "start":
                            SpawnItemsInData();
                            Message(player, "ItemSpawnStart");
                            break;
                        case "stop":
                            foreach (var entry in itemTimers)
                            {
                                entry.Value.Destroy();
                            }
                            itemTimers.Clear();
                            Message(player, "ItemSpawnStop");
                            break;
                        default:
                            Message(player, "UnknownCommand");
                            break;
                    }
                    break;
                case "prefab":
                    switch (args[1].ToLower())
                    {
                        case "add":
                            if (args.Length != 5)
                            {
                                Message(player, "UsagePrefabAddCommand", command);
                            }
                            else
                            {
                                if (_data._prefabContainer.ContainsKey(args[4]))
                                {
                                    Message(player, "AlreadyExist", args[4]);
                                }
                                else
                                {
                                    Dictionary<string, string> prefabsContainer2 = new Dictionary<string, string>();

                                    prefabsContainer2["Prefab"] = args[2];
                                    prefabsContainer2["Location"] = newPos.ToString();
                                    prefabsContainer2["Timer"] = args[3];

                                    _data._prefabContainer.Add(args[4], prefabsContainer2);
                                    SaveData();
                                    Message(player, "NameAdded", args[4]);
                                }
                            }
                            break;
                        case "remove":
                            if (args.Length != 3)
                            {
                                Message(player, "UsagePrefabRemoveCommand", command);
                            }
                            else
                            {
                                if (!_data._prefabContainer.ContainsKey(args[2]))
                                {
                                    Message(player, "NameDoesNotExist", args[2]);
                                }
                                else
                                {
                                    _data._prefabContainer.Remove(args[2]);
                                    SaveData();
                                    if (prefabTimers.ContainsKey(args[2]))
                                    {
                                        prefabTimers[args[2]].Destroy();
                                    }

                                    Message(player, "NameRemoved", args[2]);
                                }
                            }
                            break;
                        case "list":
                            foreach (var entry in _data._prefabContainer)
                            {
                                player.Message(entry.Key);
                            }
                            break;
                        case "start":
                            SpawnPrefabsInData();
                            Message(player, "PrefabSpawnStart");
                            break;
                        case "stop":
                            foreach (var entry in prefabTimers)
                            {
                                entry.Value.Destroy();
                            }
                            prefabTimers.Clear();
                            Message(player, "PrefabSpawnStop");
                            break;
                        default:
                            Message(player, "UnknownCommand");
                            break;
                    }
                    break;
                default:
                    Message(player, "UnknownCommand");
                    break;
            }
        }

        #endregion

        #region Helpers

        List<T> FindEntities<T>(Vector3 position, float distance) where T : BaseEntity
        {
            LayerMask layers = LayerMask.GetMask("Deployed");
            var list = Facepunch.Pool.GetList<T>();
            Vis.Entities(position, distance, list);
            return list;
        }

        private static LayerMask GROUND_MASKS = LayerMask.GetMask("Terrain", "World", "Construction");
        //Credit: Wulf
        static Vector3 GetGroundPosition(Vector3 sourcePos)
        {
            RaycastHit hitInfo;

            if (UnityEngine.Physics.Raycast(sourcePos, Vector3.down, out hitInfo, GROUND_MASKS))
            {
                sourcePos.y = hitInfo.point.y;
            }
            sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));
            return sourcePos;
        }

        public static Vector3 StringToVector3(string sVector)
        {
            if (sVector.StartsWith("(") && sVector.EndsWith(")"))
            {
                sVector = sVector.Substring(1, sVector.Length - 2);
            }
            string[] sArray = sVector.Split(',');

            Vector3 result = new Vector3(
                float.Parse(sArray[0]),
                float.Parse(sArray[1]),
                float.Parse(sArray[2]));

            return result;
        }

        private void Message(IPlayer player, string key, params object[] args)
        {
            if (player.IsConnected)
            {
                player.Message(Lang(key, player.Id, args));
            }
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        #endregion

        #region Data

        private const string filename = "ScheduledSpawns/ScheduledSpawnsData";
        private PluginData _data;

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(filename, _data);
        }

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(filename);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null)
            {
                _data = new PluginData();
            }
        }

        private class PluginData
        {
            public Dictionary<string, Dictionary<string, string>> _prefabContainer = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, Dictionary<string, string>> _itemContainer = new Dictionary<string, Dictionary<string, string>>();
        }

        #endregion
    }

}