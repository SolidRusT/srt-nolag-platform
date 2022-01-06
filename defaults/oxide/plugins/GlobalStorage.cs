using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Global Storage", "imthenewguy", "1.0.2")]
	[Description("Create global storage chests in safezone monuments and by placing the item.")]
	class GlobalStorage : RustPlugin
	{
        #region Config

        private Configuration config;
        public class Configuration
        {
            [JsonProperty("Storage prefab to use")]
            public string storage_prefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";

            [JsonProperty("Amount of slots that the players can access? Max value is the maximum number of slots for the container type.")]
            public int slot_count = 30;

            [JsonProperty("Box skin")]
            public ulong box_skin = 1499104921;

            [JsonProperty("Display floating text above the containers indicating that it is a storage unit?")]
            public bool draw_text = true;

            [JsonProperty("If floating text is enabled, how often should it update?")]
            public float draw_update = 5f;

            [JsonProperty("Maximum distance away from the box that the player can see the text?")]
            public float draw_distance = 30f;

            [JsonProperty("Display floating text above manually deployed containers?")]
            public bool draw_text_non_monument = true;

            [JsonProperty("Make player deployed global storage chests invulnerable?")]
            public bool deployed_chests_invulnerable = true;

            [JsonProperty("A list of item shortnames that cannot be placed into the chest")]
            public List<string> black_list = new List<string>();

            [JsonProperty("Monument modifiers")]
            public List<Configuration.monumentInfo> monuments = new List<Configuration.monumentInfo>();

            [JsonProperty("Actual box prefab to spawn")]
            public string spawn_prefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";

            public class monumentInfo
            {
                public string name;
                public bool enabled;
                public Vector3 pos;
                public Vector3 rot;
                public monumentInfo(string monument, bool enabled, Vector3 pos, Vector3 rot)
                {
                    this.enabled = enabled;
                    this.pos = pos;
                    this.rot = rot;
                    this.name = monument;
                }
            }

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            config.monuments = DefaultMonuments;
            config.black_list = new List<string>() { "cassette", "cassette.medium", "cassette.short", "boombox", "fun.boomboxportable", "fun.casetterecorder" };
        }

        private List<Configuration.monumentInfo> DefaultMonuments
        {
            get
            {
                return new List<Configuration.monumentInfo>
                {
                    new Configuration.monumentInfo("assets/bundled/prefabs/autospawn/monument/fishing_village/fishing_village_b.prefab", true, new Vector3(-10.1f, 2f, 20.4f), new Vector3(0, 270f, 0)),
                    new Configuration.monumentInfo("assets/bundled/prefabs/autospawn/monument/medium/bandit_town.prefab", true, new Vector3(9.0f, 2.8f, 0.7f), new Vector3(0, -90f, 0)),
                    new Configuration.monumentInfo("assets/bundled/prefabs/autospawn/monument/medium/compound.prefab", true, new Vector3(-24.1f, 0.2f, 13.1f), new Vector3(0, 90f, 0)),
                    new Configuration.monumentInfo("assets/bundled/prefabs/autospawn/monument/fishing_village/fishing_village_a.prefab", true, new Vector3(19.0f, 2.0f, -3.8f), new Vector3(0, 0, 0)),
                    new Configuration.monumentInfo("assets/bundled/prefabs/autospawn/monument/fishing_village/fishing_village_c.prefab", true, new Vector3(-5.3f, 2.0f, -2.0f), new Vector3(0, 180, 0))
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
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintToConsole("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintToConsole($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            PrintToConsole($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion

        #region Data

        PlayerEntity pcdData;
        private DynamicConfigFile PCDDATA;

        const string perms_admin = "globalstorage.admin";
        const string perms_chat = "globalstorage.chat";
        const string perms_access = "globalstorage.access";

        void Init()
        {
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile(this.Name);
            permission.RegisterPermission(perms_admin, this);
            permission.RegisterPermission(perms_chat, this);
            permission.RegisterPermission(perms_access, this);
            LoadData();
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.EndLooting();
            }

            foreach (var container in boxes.ToList())
            {
                if (pcdData.monuments.ContainsKey(container.Key) && pcdData.monuments[container.Key].monument == "deployed") continue;
                pcdData.monuments.Remove(container.Key);
                container.Value.KillMessage();
            }
            SaveData();
        }

        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

        void LoadData()
        {
            try
            {
                pcdData = Interface.Oxide.DataFileSystem.ReadObject<PlayerEntity>(this.Name);
            }
            catch
            {
                Puts("Couldn't load player data, creating new Playerfile");
                pcdData = new PlayerEntity();
            }
        }

        class StorageBox
        {
            public List<StorageInfo> _storage = new List<StorageInfo>();
        }

        public class StorageInfo
        {
            public string shortname;
            public string displayName;
            public ulong skin;
            public int amount;
            public int slot;
            public float condition;
            public float maxCondition;
            public int ammo;
            public string ammotype;
            public StorageInfo[] contents;
            public InstancedInfo instanceData;
            public class InstancedInfo
            {
                public bool ShouldPool;
                public int dataInt;
                public int blueprintTarget;
                public int blueprintAmount;
                public uint subEntity;
            }
            public string text;
        }

        class PlayerEntity
        {
            public Dictionary<ulong, StorageBox> storage = new Dictionary<ulong, StorageBox>();
            public Dictionary<uint, monumentInfo> monuments = new Dictionary<uint, monumentInfo>();
        }

        public class monumentInfo
        {
            public string monument;
            public bool enabled;
            public Vector3 pos;
            public Vector3 rot;
            public monumentInfo(string monument, bool enabled, Vector3 pos, Vector3 rot)
            {
                this.monument = monument;
                this.enabled = enabled;
                this.pos = pos;
                this.rot = rot;
            }
        }


        #endregion;

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["WarnDelayTime"] = "Please wait a moment before attempting to open the box again.",
                ["FloatingText"] = "<size=26>Global Storage Chest</size>",
                ["MorePlayersFound"] = "More than one player found: {0}",
                ["NoMatch"] = "No player was found that matched: {0}",
                ["GaveBoxes"] = "Gave {0} {1}x Global Storage Boxes",
                ["ReceiveBoxes"] = "You received {0}x Global Storage Boxes",
                ["AlreadySetup"] = "This box is already setup as a Global Storage Chest.",
                ["SetupBox"] = "Set {0} up as a Global Storage container. OwnerID: {1}",
                ["NoAccessPerms"] = "You do not have permissions to access global storage.",
                ["NoLock"] = "You cannot deploy a lock on a global storage chest.",
                ["BlackList"] = "This item has been black listed from global storage.",
                ["ValidUsage"] = "Valid usage: /giveglobalbox <name/id> <quantity>",
                ["ConsoleGave"] = "Gave {0} {1}x Global Storage Boxes"
            }, this);
        }

        #endregion

        #region Storage

        List<StorageContainer> containers = new List<StorageContainer>();

        Dictionary<ulong, float> bagCooldownTimer = new Dictionary<ulong, float>();

        StorageBox storageData;

        private void OpenStorage(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, perms_access))
            {
                PrintToChat(player, lang.GetMessage("NoAccessPerms", this, player.UserIDString));
                return;
            }
            if (bagCooldownTimer.ContainsKey(player.userID))
            {
                if (bagCooldownTimer[player.userID] > Time.time)
                {
                    PrintToChat(player, lang.GetMessage("WarnDelayTime", this, player.UserIDString));
                    return;
                }
                bagCooldownTimer.Remove(player.userID);
            }
            if (!bagCooldownTimer.ContainsKey(player.userID))
            {
                bagCooldownTimer.Add(player.userID, Time.time + 2f);                
            }
            player.EndLooting();

            object hookResult = Interface.CallHook("CanAccessGlobalStorage", player);
            if (hookResult is string && hookResult != null) return;

            var pos = new Vector3(player.transform.position.x, player.transform.position.y - 1000, player.transform.position.z);
            var storage = GameManager.server.CreateEntity(config.storage_prefab, pos) as StorageContainer;            
            storage.Spawn();
            UnityEngine.Object.DestroyImmediate(storage.GetComponent<GroundWatch>());
            UnityEngine.Object.DestroyImmediate(storage.GetComponent<DestroyOnGroundMissing>());
            storage.OwnerID = player.userID;
            storage.inventory.capacity = config.slot_count;

            if (pcdData.storage.TryGetValue(player.userID, out storageData) && storageData._storage.Count > 0)
            {
                foreach (var itemDef in storageData._storage)
                {
                    var item = ItemManager.CreateByName(itemDef.shortname, itemDef.amount, itemDef.skin);
                    item.name = itemDef.displayName;
                    item.condition = itemDef.condition;
                    item.maxCondition = itemDef.maxCondition;
                    if (itemDef.contents != null)
                    {
                        foreach (StorageInfo contentData in itemDef.contents)
                        {
                            Item newContent = ItemManager.CreateByName(contentData.shortname, contentData.amount);
                            if (newContent != null)
                            {
                                newContent.condition = contentData.condition;
                                newContent.MoveToContainer(item.contents);
                            }
                        }
                    }

                    if (itemDef.instanceData != null)
                    {
                        item.instanceData = new ProtoBuf.Item.InstanceData();
                        item.instanceData.ShouldPool = itemDef.instanceData.ShouldPool;
                        item.instanceData.dataInt = itemDef.instanceData.dataInt;
                        item.instanceData.blueprintTarget = itemDef.instanceData.blueprintTarget;
                        item.instanceData.blueprintAmount = itemDef.instanceData.blueprintAmount;
                        item.instanceData.subEntity = itemDef.instanceData.subEntity;
                    }

                    if (itemDef.text != null) item.text = itemDef.text;

                    item.MoveToContainer(storage.inventory, itemDef.slot, true, true);
                }
            }

            containers.Add(storage);

            timer.Once(0.1f, () =>
            {
                if (storage != null) storage.PlayerOpenLoot(player, "", false);
            });
        }

        void StoreContainerLoot(BasePlayer player, StorageContainer container)
        {
            if (!pcdData.storage.TryGetValue(player.userID, out storageData))
            {
                pcdData.storage.Add(player.userID, new StorageBox());
                storageData = pcdData.storage[player.userID];
            }
            if (storageData._storage.Count != 0) storageData._storage.Clear();
            List<StorageInfo> items = new List<StorageInfo>();
            foreach (var item in container.inventory.itemList)
            {
                var displayName = item.info.displayName.english;
                if (item.name != null) displayName = item.name;
                StorageInfo itemData;
                if (item.name != null) displayName = item.name;

                storageData._storage.Add(itemData = new StorageInfo()
                {
                    shortname = item.info.shortname,
                    skin = item.skin,
                    slot = item.position,
                    displayName = displayName,
                    amount = item.amount,
                    condition = item.condition,
                    maxCondition = item.maxCondition,
                    contents = item.contents?.itemList.Select(item1 => new StorageInfo
                    {
                        shortname = item1.info.shortname,
                        amount = item1.amount,
                        condition = item1.condition
                    }).ToArray()
                });

                if (item.instanceData != null)
                {
                    itemData.instanceData = new StorageInfo.InstancedInfo()
                    {
                        ShouldPool = item.instanceData.ShouldPool,
                        dataInt = item.instanceData.dataInt,
                        blueprintTarget = item.instanceData.blueprintTarget,
                        blueprintAmount = item.instanceData.blueprintAmount,
                        subEntity = item.instanceData.subEntity
                    };
                }

                if (item.text != null) itemData.text = item.text;

                items.Add(itemData);
            }
            SaveData();
            containers.Remove(container);
            container.Invoke(container.KillMessage, 0.01f);
        }

        void OnLootEntityEnd(BasePlayer player, StorageContainer entity)
        {            
            if (containers.Contains(entity))
            {
                StoreContainerLoot(player, entity);
            }
        }

        #endregion

        #region Monument handling

        StorageContainer CreateBox(MonumentInfo monument, Vector3 pos_settings, Vector3 rot_settings, ulong ownerID = 0)
        {
            Vector3 pos = monument.transform.localToWorldMatrix.MultiplyPoint3x4(pos_settings);
            Quaternion rot = monument.transform.localToWorldMatrix.rotation * Quaternion.Euler(rot_settings);
            BaseEntity box = GameManager.server.CreateEntity(config.spawn_prefab, pos, rot);
            if (box == null)
            {
                Puts("Asset path is invalid. Please update the config with the correct path.");
                return null;
            }
            box.skinID = config.box_skin;
            box.Spawn();
            box.OwnerID = ownerID;
            return box as StorageContainer;
        }
        Dictionary<uint, StorageContainer> boxes = new Dictionary<uint, StorageContainer>();

        #endregion

        #region Hooks

        void OnServerInitialized(bool initial)
        {
            if (config.black_list.Count == 0) Unsubscribe("CanMoveItem");
            var delay = 0.0f;
            foreach (var config_monument in config.monuments)
            {
                var found = false;
                var key = 0u;
                foreach (KeyValuePair<uint, monumentInfo> kvp in pcdData.monuments)
                {
                    if (config_monument.name == kvp.Value.monument)
                    {
                        found = true;
                        key = kvp.Key;
                        break;
                    }
                }
                if (found)
                {
                    BaseNetworkable box = BaseNetworkable.serverEntities.Find(key);

                    if (box == null)
                    {
                        if (!config_monument.enabled)
                        {
                            pcdData.monuments.Remove(key);
                            continue;
                        }
                        delay += 0.1f;
                        pcdData.monuments.Remove(key);

                        MonumentInfo Monument = TerrainMeta.Path.Monuments.Where(x => x.name == config_monument.name).FirstOrDefault();
                        if (Monument == null)
                        {
                            Puts($"Could not find {config_monument.name}");
                            continue;
                        }

                        timer.Once(delay, () =>
                        {
                            var newBox = CreateBox(Monument, config_monument.pos, config_monument.rot);
                            pcdData.monuments.Add(newBox.net.ID, new monumentInfo(Monument.name, true, config_monument.pos, config_monument.rot));
                            boxes.Add(newBox.net.ID, newBox);
                            SaveData();
                        });                        
                    }
                    else
                    {
                        if (!config_monument.enabled)
                        {
                            box.KillMessage();
                            pcdData.monuments.Remove(key);
                            continue;
                        }
                        var container = box as StorageContainer;
                        if (container.skinID != config.box_skin)
                        {
                            container.skinID = config.box_skin;
                            container.SendNetworkUpdateImmediate();
                        }
                        boxes.Add(container.net.ID, container);
                    }
                }
                else
                {
                    if (!config_monument.enabled) continue;
                    MonumentInfo Monument = TerrainMeta.Path.Monuments.Where(x => x.name == config_monument.name).FirstOrDefault();
                    if (Monument == null)
                    {
                        Puts($"Could not find {config_monument.name}");
                        continue;
                    }

                    delay += 0.1f;

                    timer.Once(delay, () =>
                    {
                        var newBox = CreateBox(Monument, config_monument.pos, config_monument.rot);
                        pcdData.monuments.Add(newBox.net.ID, new monumentInfo(Monument.name, true, config_monument.pos, config_monument.rot));
                       
                        boxes.Add(newBox.net.ID, newBox);
                        SaveData();
                    });                    
                }
            }

            foreach (KeyValuePair<uint, monumentInfo> kvp in pcdData.monuments.ToList())
            {
                if (kvp.Value.monument == "deployed")
                {
                    var entity = BaseNetworkable.serverEntities.Find(kvp.Key);
                    if (entity == null) pcdData.monuments.Remove(kvp.Key);
                    else boxes.Add(kvp.Key, entity as StorageContainer);
                }
            }
            
            if (config.draw_text)
            {
                timer.Every(config.draw_update, () =>
                {
                    foreach (var box in boxes)
                    {
                        if (box.Value == null) continue;
                        if (!config.draw_text_non_monument && box.Value.OwnerID > 0) continue;
                        var pos = box.Value.transform.position;
                        pos.y += 1f;
                        foreach (var player in BasePlayer.activePlayerList)
                        {
                            if (Vector3.Distance(player.transform.position, box.Value.transform.position) < config.draw_distance)
                            {
                                if (player.Connection.authLevel == 0)
                                {
                                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                                    player.SendNetworkUpdateImmediate();
                                }
                                
                                player.SendConsoleCommand("ddraw.text", config.draw_update, Color.cyan, pos, lang.GetMessage("FloatingText", this, player.UserIDString));

                                if (player.Connection.authLevel == 0)
                                {
                                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                                    player.SendNetworkUpdateImmediate();
                                }
                            }
                        }
                    }
                });
            }
        }

        object OnEntityTakeDamage(StorageContainer entity, HitInfo info)
        {
            if (entity == null) return null;
            if (boxes.ContainsKey(entity.net.ID))
            {
                if (!config.deployed_chests_invulnerable && pcdData.monuments.ContainsKey(entity.net.ID) && pcdData.monuments[entity.net.ID].monument == "deployed") return null;
                return true;
            }               
            return null;
        }

        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container != null && boxes.ContainsKey(container.net.ID))
            {
                OpenStorage(player);
                return true;
            }
            return null;
        }

        void OnEntityKill(StorageContainer entity)
        {
            if (entity == null) return;
            if (boxes.ContainsKey(entity.net.ID))
            {
                pcdData.monuments.Remove(entity.net.ID);
                boxes.Remove(entity.net.ID);
                SaveData();
            }
        }

        object CanPickupEntity(BasePlayer player, StorageContainer entity)
        {
            if (entity != null && boxes.ContainsKey(entity.net.ID))
            {
                if (entity.OwnerID == player.userID)
                {
                    pcdData.monuments.Remove(entity.net.ID);
                    boxes.Remove(entity.net.ID);
                    SaveData();
                    return null;
                }
                return false;
            }
            return null;
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var entity = go?.ToBaseEntity();
            if (entity == null || entity.skinID != config.box_skin) return;
            var player = plan?.GetOwnerPlayer();
            if (player == null) return;
            entity.OwnerID = player.userID;
            pcdData.monuments.Add(entity.net.ID, new monumentInfo("deployed", true, entity.transform.position, new Vector3()));
            boxes.Add(entity.net.ID, entity as StorageContainer);
            SaveData();
        }

        object CanDeployItem(BasePlayer player, Deployer deployer, uint entityId)
        {
            if (boxes.ContainsKey(entityId))
            {
                PrintToChat(player, lang.GetMessage("NoLock", this, player.UserIDString));
                return true;
            }
            return null;
        }

        object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot, int amount)
        {
            if (config.black_list.Count > 0 && config.black_list.Contains(item.info.shortname))
            {
                foreach (var container in containers)
                {
                    if (container.inventory.uid == targetContainer)
                    {
                        var player = item.GetOwnerPlayer();
                        PrintToChat(player, lang.GetMessage("BlackList", this, player.UserIDString));
                        return true;
                    }
                }
            }            
            return null;
        }

        #endregion

        #region Helpers

        void GiveBoxItem(BasePlayer player, int quantity = 1)
        {
            var item = ItemManager.CreateByName("box.wooden.large", quantity, config.box_skin);
            item.name = "global storage box";
            player.GiveItem(item);
        }

        private BasePlayer FindPlayerByName(string Playername, BasePlayer SearchingPlayer = null)
        {
            var lowered = Playername.ToLower();
            var targetList = BasePlayer.allPlayerList.Where(x => x.displayName.ToLower().Contains(lowered)).OrderBy(x => x.displayName.Length);
            if (targetList.Count() == 1)
            {
                return targetList.First();
            }
            if (targetList.Count() > 1)
            {
                if (targetList.First().displayName.Equals(Playername, StringComparison.OrdinalIgnoreCase))
                {
                    return targetList.First();
                }
                if (SearchingPlayer != null)
                {
                    PrintToChat(SearchingPlayer, string.Format(lang.GetMessage("MorePlayersFound", this, SearchingPlayer.UserIDString), String.Join(",", targetList.Select(x => x.displayName))));
                }
                else Puts(string.Format(lang.GetMessage("MorePlayersFound", this), String.Join(",", targetList.Select(x => x.displayName))));
                return null;
            }
            if (targetList.Count() == 0)
            {
                if (SearchingPlayer != null)
                {
                    PrintToChat(SearchingPlayer, string.Format(lang.GetMessage("NoMatch", this, SearchingPlayer.UserIDString), Playername));
                }
                else Puts(string.Format(lang.GetMessage("NoMatch", this), Playername));
                return null;
            }
            return null;
        }

        private const int LAYER_TARGET = ~(1 << 2 | 1 << 3 | 1 << 4 | 1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);
        private BaseEntity GetTargetEntity(BasePlayer player)
        {
            RaycastHit raycastHit;
            bool flag = Physics.Raycast(player.eyes.HeadRay(), out raycastHit, 5, LAYER_TARGET);
            var targetEntity = flag ? raycastHit.GetEntity() : null;
            return targetEntity;
        }

        #endregion

        #region Chat commands

        [ChatCommand("giveglobalbox")]
        void GiveBox(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, perms_admin)) return;
            var amount = 1;
            if (args.Length == 0)
            {
                GiveBoxItem(player, amount);
                PrintToChat(player, string.Format(lang.GetMessage("GaveBoxes", this, player.UserIDString), player.displayName, amount));
                return;
            }
            if (args.Length > 0)
            {
                var target = FindPlayerByName(args[0], player);
                if (target == null) return;                
                if (args.Length == 2 && args[1].IsNumeric()) amount = Convert.ToInt32(args[1]);
                GiveBoxItem(target, amount);
                PrintToChat(player, string.Format(lang.GetMessage("GaveBoxes", this, player.UserIDString), target.displayName, amount));
                PrintToChat(player, string.Format(lang.GetMessage("ReceiveBoxes", this, target.UserIDString), amount));
            }
        }

        [ConsoleCommand("giveglobalbox")]
        void GiveBoxConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, perms_admin)) return;
            if (arg.Args.Length == 0)
            {
                if (player != null) arg.ReplyWith(lang.GetMessage("ValidUsage", this, player.UserIDString));
                else arg.ReplyWith(lang.GetMessage("ValidUsage", this));
                return;
            }
            var target = FindPlayerByName(arg.Args[0], player ?? null);
            if (target == null) return;
            var amount = 1;
            if (arg.Args.Length == 2 && arg.Args[1].IsNumeric()) amount = Convert.ToInt32(arg.Args[1]);
            arg.ReplyWith(string.Format(lang.GetMessage("ConsoleGave", this), target.displayName, amount));
            GiveBoxItem(target, amount);
            PrintToChat(target, string.Format(lang.GetMessage("ReceiveBoxes", this, target.UserIDString), amount));
        }

        [ChatCommand("gstorage")]
        void GlobalStorageCMD(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, perms_chat)) return;
            OpenStorage(player);
        }

        [ChatCommand("addglobalstorage")]
        void AddGlobalStorageCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, perms_admin)) return;
            var entity = GetTargetEntity(player);
            if (entity == null || !(entity is StorageContainer) || entity.PrefabName != config.storage_prefab) return;
            var container = entity as StorageContainer;
            if (boxes.ContainsKey(container.net.ID))
            {
                PrintToChat(player, lang.GetMessage("AlreadySetup", this, player.UserIDString));
                return;
            }
            var ownerID = 0ul;
            if (args.Length > 0)
            {
                var target = FindPlayerByName(args[0], player);
                if (target == null) return;
                ownerID = target.userID;
            }
            container.OwnerID = ownerID;
            container.skinID = config.box_skin;
            container.SendNetworkUpdateImmediate();
            if (!pcdData.monuments.ContainsKey(container.net.ID)) pcdData.monuments.Add(container.net.ID, new monumentInfo("deployed", true, container.transform.position, new Vector3()));
            boxes.Add(container.net.ID, container);
            PrintToChat(player, string.Format(lang.GetMessage("SetupBox", this, player.UserIDString), container.net.ID, ownerID));
            SaveData();
        }

        #endregion

    }
}
