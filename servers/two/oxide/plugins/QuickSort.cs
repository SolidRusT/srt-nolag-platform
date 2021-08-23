using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Quick Sort", "MON@H", "1.6.0")]
    [Description("Adds a GUI that allows players to quickly sort items into containers")]
    public class QuickSort : RustPlugin
    {
        #region Variables

        private const string PermissionAutoLootAll = "quicksort.autolootall";
        private const string PermissionLootAll = "quicksort.lootall";
        private const string PermissionUse = "quicksort.use";
        private const string GUIPanelName = "QuickSortUI";

        #endregion Variables

        #region Initialization

        private void Init()
        {
            Unsubscribe(nameof(OnBackpackClosed));
            Unsubscribe(nameof(OnBackpackOpened));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnLootEntity));
            Unsubscribe(nameof(OnLootPlayer));
            Unsubscribe(nameof(OnPlayerLootEnd));
            Unsubscribe(nameof(OnPlayerSleep));

            LoadData();

            permission.RegisterPermission(PermissionAutoLootAll, this);
            permission.RegisterPermission(PermissionLootAll, this);
            permission.RegisterPermission(PermissionUse, this);

            if (_configData.GlobalSettings.Commands.Length == 0)
            {
                _configData.GlobalSettings.Commands = new[] { "qs" };
                SaveConfig();
            }

            foreach (string command in _configData.GlobalSettings.Commands)
            {
                cmd.AddChatCommand(command, this, nameof(CmdQuickSort));
            }
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnBackpackClosed));
            Subscribe(nameof(OnBackpackOpened));
            Subscribe(nameof(OnEntityDeath));
            Subscribe(nameof(OnEntityKill));
            Subscribe(nameof(OnLootEntity));
            Subscribe(nameof(OnLootPlayer));
            Subscribe(nameof(OnPlayerLootEnd));
            Subscribe(nameof(OnPlayerSleep));
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyGUI(player);
            }
        }

        #endregion Initialization

        #region Configuration

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Global settings")]
            public GlobalConfiguration GlobalSettings = new GlobalConfiguration();

            [JsonProperty(PropertyName = "Custom UI Settings")]
            public UiConfiguration CustomUISettings = new UiConfiguration();

            public class GlobalConfiguration
            {
                [JsonProperty(PropertyName = "Default enabled")]
                public bool DefaultEnabled = true;

                [JsonProperty(PropertyName = "Default UI style (center, lite, right, custom)")]
                public string DefaultUiStyle = "right";

                [JsonProperty(PropertyName = "Enable Backpacks plugin support")]
                public bool BackpacksEnabled = true;

                [JsonProperty(PropertyName = "Loot all delay in seconds (0 to disable)")]
                public int LootAllDelay = 0;

                [JsonProperty(PropertyName = "Enable loot all on the sleepers")]
                public bool LootSleepers = false;

                [JsonProperty(PropertyName = "Auto loot all enabled by default")]
                public bool AutoLootAll = false;

                [JsonProperty(PropertyName = "Default enabled container types")]
                public PlayerContainers Containers = new PlayerContainers();

                [JsonProperty(PropertyName = "Chat steamID icon")]
                public ulong SteamIDIcon = 0;

                [JsonProperty(PropertyName = "Chat command")]
                public string[] Commands = new[] { "qs", "quicksort" };

                [JsonProperty(PropertyName = "Excluded containers")]
                public string[] ContainersExcluded = new[]
                {
                    "autoturret_deployed",
                    "dropbox.deployed",
                    "flameturret.deployed",
                    "guntrap.deployed",
                    "sam_site_turret_deployed",
                    "sam_static",
                    "scientist_turret_any",
                    "scientist_turret_lr300",
                    "wall.frame.shopfront",
                    "wall.frame.shopfront.metal",
                    "wall.frame.shopfront.metal.static",
                };
            }

            public class UiConfiguration
            {
                public string AnchorsMin = "0.5 1.0";
                public string AnchorsMax = "0.5 1.0";
                public string OffsetsMin = "192 -137";
                public string OffsetsMax = "573 0";
                public string Color = "0.5 0.5 0.5 0.33";
                public string ButtonsColor = "0.75 0.43 0.18 0.8";
                public string LootAllColor = "0.41 0.50 0.25 0.8";
                public string TextColor = "0.77 0.92 0.67 0.8";
                public int TextSize = 16;
                public int CategoriesTextSize = 14;
            }
        }

        public class PlayerContainers
        {
            public bool Belt = false;
            public bool Main = true;
            public bool Wear = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(_configData);

        #endregion Configuration

        #region DataFile

        private StoredData _storedData;

        private class StoredData
        {
            public readonly Hash<ulong, PlayerData> PlayerData = new Hash<ulong, PlayerData>();
        }

        public class PlayerData
        {
            public bool Enabled;
            public bool AutoLootAll;
            public string UiStyle;
            public PlayerContainers Containers;
        }

        private PlayerData GetPlayerData(ulong playerID)
        {
            PlayerData playerData;
            if (!_storedData.PlayerData.TryGetValue(playerID, out playerData))
            {
                return null;
            }

            return playerData;
        }

        private void LoadData()
        {
            try
            {
                _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                ClearData();
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);

        private void ClearData()
        {
            _storedData = new StoredData();
            SaveData();
        }

        #endregion DataFile

        #region Localization

        private string Lang(string key, string userIDString = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, userIDString), args);
            }
            catch (Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception:\n{ex}");
                throw;
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Deposit"] = "Deposit",
                ["DepositAll"] = "All",
                ["DepositAmmo"] = "Ammo",
                ["DepositAttire"] = "Attire",
                ["DepositConstruction"] = "Construction",
                ["DepositElectrical"] = "Electrical",
                ["DepositExisting"] = "Existing",
                ["DepositFood"] = "Food",
                ["DepositItems"] = "Deployables",
                ["DepositMedical"] = "Medical",
                ["DepositResources"] = "Resources",
                ["DepositTools"] = "Tools",
                ["DepositTraps"] = "Traps",
                ["DepositWeapons"] = "Weapons",
                ["DepositComponents"] = "Components",
                ["DepositMisc"] = "Misc",
                ["LootAll"] = "Loot All",

                ["NotAllowed"] = "You do not have permission to use this command",
                ["Enabled"] = "<color=#228B22>Enabled</color>",
                ["Disabled"] = "<color=#B22222>Disabled</color>",
                ["QuickSort"] = "Quick Sort GUI is now {0}",
                ["Style"] = "Quick Sort GUI style is now {0}",
                ["AutoLootAll"] = "Automated looting is now {0}",
                ["ContainerType"] = "Quick Sort for container type {0} is now {1}",
                ["Prefix"] = "<color=#00FF00>[Quick Sort]</color>: ",

                ["SyntaxError"] = "List Commands:\n" +
                "<color=#FFFF00>/{0} on</color> - Enable GUI\n" +
                "<color=#FFFF00>/{0} off</color> - Disable GUI\n" +
                "<color=#FFFF00>/{0} auto</color> - Enable/Disable automated looting\n" +
                "<color=#FFFF00>/{0} <s | style> <center | lite | right | custom></color> - change GUI style\n" +
                "<color=#FFFF00>/{0} <c | conatiner> <main | wear | belt></color> - add/remove container type from the sort",
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Deposit"] = "Положить",
                ["DepositAll"] = "Всё",
                ["DepositAmmo"] = "Патроны",
                ["DepositAttire"] = "Одежда",
                ["DepositConstruction"] = "Конструкции",
                ["DepositElectrical"] = "Электричество",
                ["DepositExisting"] = "Существующие",
                ["DepositFood"] = "Еда",
                ["DepositItems"] = "Развертываемые",
                ["DepositMedical"] = "Медикаменты",
                ["DepositResources"] = "Ресурсы",
                ["DepositTools"] = "Инструменты",
                ["DepositTraps"] = "Ловушки",
                ["DepositWeapons"] = "Оружие",
                ["DepositComponents"] = "Компоненты",
                ["DepositMisc"] = "Разное",
                ["LootAll"] = "Забрать всё",

                ["NotAllowed"] = "У вас нет разрешения на использование этой команды",
                ["Enabled"] = "<color=#228B22>Включена</color>",
                ["Disabled"] = "<color=#B22222>Отключена</color>",
                ["QuickSort"] = "GUI быстрой сортировки теперь {0}",
                ["Style"] = "Стиль GUI быстрой сортировки теперь {0}",
                ["AutoLootAll"] = "Забирать всё автоматически теперь {0}",
                ["ContainerType"] = "Быстрая сортировка для типа контейнера {0} теперь {1}",
                ["Prefix"] = "<color=#00FF00>[Быстрая сортировка]</color>: ",

                ["SyntaxError"] = "Список команд:\n" +
                "<color=#FFFF00>/{0} on</color> - Включить GUI\n" +
                "<color=#FFFF00>/{0} off</color> - Отключить GUI\n" +
                "<color=#FFFF00>/{0} auto</color> - Включить/Отключить забирать всё автоматически.\n" +
                "<color=#FFFF00>/{0} <s | style> <center | lite | right | custom></color> - изменить стиль GUI быстрой сортировки.\n" +
                "<color=#FFFF00>/{0} <c | conatiner> <main | wear | belt></color> - добавить/удалить тип контейнера для сортировки.",
            }, this, "ru");
        }

        #endregion Localization

        #region Oxide Hooks

        private void OnLootPlayer(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                UserInterface(player);
            }
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            DestroyOnGroundMissing check = entity.GetComponent<DestroyOnGroundMissing>();

            if (check != null && check.enabled == false)
            {
                return;
            }

            if (IsContainerExcluded(player, entity))
            {
                return;
            }

            if (permission.UserHasPermission(player.UserIDString, PermissionAutoLootAll))
            {
                PlayerData playerData = GetPlayerData(player.userID);

                bool autoLootAll = _configData.GlobalSettings.AutoLootAll;

                if (playerData != null)
                {
                    autoLootAll = playerData.AutoLootAll;
                }

                if (autoLootAll)
                {
                    timer.Once(_configData.GlobalSettings.LootAllDelay, () =>
                    {
                        List<ItemContainer> containers = GetLootedInventory(player);

                        if (containers != null)
                        {
                            foreach (ItemContainer itemContainer in containers)
                            {
                                if (itemContainer.HasFlag(ItemContainer.Flag.NoItemInput))
                                {
                                    AutoLoot(player);
                                    if (itemContainer.IsEmpty())
                                    {
                                        player.EndLooting();
                                        return;
                                    }
                                }
                            }
                        }
                    });
                }
            }

            if (permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                UserInterface(player);
            }
        }

        private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            BasePlayer player = inventory.GetComponent<BasePlayer>();

            if (player != null)
            {
                DestroyGUI(player);
            }
        }

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (player != null && !player.IsNpc && player.userID.IsSteamId())
            {
                DestroyGUI(player);
            }
        }

        private void OnEntityKill(BasePlayer player)
        {
            if (player != null && !player.IsNpc && player.userID.IsSteamId())
            {
                DestroyGUI(player);
            }
        }

        private void OnPlayerSleep(BasePlayer player) => DestroyGUI(player);

        private void OnBackpackOpened(BasePlayer player, ulong backpackOwnerID, ItemContainer backpackContainer)
        {
            if (permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                UserInterface(player);
            }
        }

        private void OnBackpackClosed(BasePlayer player, ulong backpackOwnerID, ItemContainer backpackContainer)
        {
            if (player != null)
            {
                DestroyGUI(player);
            }
        }

        #endregion Oxide Hooks

        #region Commands

        private void CmdQuickSort(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                PlayerSendMessage(player, Lang("NotAllowed", player.UserIDString));
                return;
            }

            PlayerData playerData = GetPlayerData(player.userID);
            if (playerData == null)
            {
                playerData = new PlayerData
                {
                    Enabled = _configData.GlobalSettings.DefaultEnabled,
                    AutoLootAll = _configData.GlobalSettings.AutoLootAll,
                    UiStyle = _configData.GlobalSettings.DefaultUiStyle,
                    Containers = _configData.GlobalSettings.Containers,
                };

                _storedData.PlayerData.Add(player.userID, playerData);
                SaveData();
            }

            if (args == null || args.Length == 0)
            {
                PlayerSendMessage(player, Lang("SyntaxError", player.UserIDString, _configData.GlobalSettings.Commands[0]));
                return;
            }

            switch (args[0].ToLower())
            {
                case "on":
                    if (!playerData.Enabled)
                    {
                        playerData.Enabled = true;
                        SaveData();
                    }
                    PlayerSendMessage(player, Lang("QuickSort", player.UserIDString, Lang("Enabled", player.UserIDString)));
                    return;
                case "off":
                    if (playerData.Enabled)
                    {
                        playerData.Enabled = false;
                        SaveData();
                    }
                    PlayerSendMessage(player, Lang("QuickSort", player.UserIDString, Lang("Disabled", player.UserIDString)));
                    return;
                case "auto":
                    playerData.AutoLootAll = !playerData.AutoLootAll;
                    SaveData();
                    PlayerSendMessage(player, Lang("AutoLootAll", player.UserIDString, playerData.AutoLootAll ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                    return;
                case "s":
                case "style":
                    {
                        if (args.Length > 1)
                        {
                            switch (args[1].ToLower())
                            {
                                case "center":
                                case "lite":
                                case "right":
                                case "custom":
                                    {
                                        playerData.UiStyle = args[1].ToLower();
                                        SaveData();
                                        PlayerSendMessage(player, Lang("Style", player.UserIDString, args[1].ToLower()));
                                        return;
                                    }
                            }
                        }
                        break;
                    }
                case "c":
                case "container":
                    {
                        if (args.Length > 1)
                        {
                            switch (args[1].ToLower())
                            {
                                case "main":
                                    {
                                        bool flag = false;
                                        if (args.Length > 2 && bool.TryParse(args[2], out flag))
                                        {
                                            playerData.Containers.Main = flag;
                                        }
                                        else
                                        {
                                            playerData.Containers.Main = !playerData.Containers.Main;
                                        }
                                        SaveData();

                                        PlayerSendMessage(player, Lang("ContainerType", player.UserIDString, "main", playerData.Containers.Main ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                                        return;
                                    }
                                case "wear":
                                    {
                                        bool flag = false;
                                        if (args.Length > 2 && bool.TryParse(args[2], out flag))
                                        {
                                            playerData.Containers.Wear = flag;
                                        }
                                        else
                                        {
                                            playerData.Containers.Wear = !playerData.Containers.Wear;
                                        }
                                        SaveData();

                                        PlayerSendMessage(player, Lang("ContainerType", player.UserIDString, "wear", playerData.Containers.Wear ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                                        return;
                                    }
                                case "belt":
                                    {
                                        bool flag = false;
                                        if (args.Length > 2 && bool.TryParse(args[2], out flag))
                                        {
                                            playerData.Containers.Belt = flag;
                                        }
                                        else
                                        {
                                            playerData.Containers.Belt = !playerData.Containers.Belt;
                                        }
                                        SaveData();

                                        PlayerSendMessage(player, Lang("ContainerType", player.UserIDString, "belt", playerData.Containers.Belt ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                                        return;
                                    }
                            }
                        }
                        break;
                    }
            }

            PlayerSendMessage(player, Lang("SyntaxError", player.UserIDString, _configData.GlobalSettings.Commands[0]));
        }

        [ConsoleCommand("quicksortgui")]
        private void SortCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player != null && permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                try
                {
                    SortItems(player, arg.Args);
                }
                catch { }
            }
        }

        [ConsoleCommand("quicksortgui.lootall")]
        private void LootAllCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player != null && permission.UserHasPermission(player.UserIDString, PermissionLootAll))
            {
                timer.Once(_configData.GlobalSettings.LootAllDelay, () => AutoLoot(player));
            }
        }

        [ConsoleCommand("quicksortgui.lootdelay")]
        private void LootDelayCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player != null && player.IsAdmin)
            {
                int x;
                if (int.TryParse(arg.Args[0], out x))
                {
                    _configData.GlobalSettings.LootAllDelay = x;
                    SaveConfig();
                }
            }
        }

        #endregion Commands

        #region Loot Handling

        private void AutoLoot(BasePlayer player)
        {
            List<ItemContainer> containers = GetLootedInventory(player);

            ItemContainer playerMain = player.inventory.containerMain;

            if (containers != null && playerMain != null && (containers[0].playerOwner == null || _configData.GlobalSettings.LootSleepers))
            {
                List<Item> itemsSelected = Pool.GetList<Item>();

                foreach (ItemContainer c in containers)
                {
                    itemsSelected.AddRange(CloneItemList(c.itemList));
                }

                itemsSelected.Sort((item1, item2) => item2.info.itemid.CompareTo(item1.info.itemid));
                MoveItems(itemsSelected, playerMain);
                Pool.FreeList(ref itemsSelected);
            }
        }

        private void SortItems(BasePlayer player, string[] args)
        {
            if (player == null)
            {
                return;
            }

            PlayerContainers type = GetPlayerData(player.userID)?.Containers;
            ItemContainer container = GetLootedInventory(player)[0];
            ItemContainer playerMain = player.inventory?.containerMain;
            ItemContainer playerWear = player.inventory?.containerWear;
            ItemContainer playerBelt = player.inventory?.containerBelt;

            if (container != null && playerMain != null && !container.HasFlag(ItemContainer.Flag.NoItemInput))
            {
                List<Item> itemsSelected = Pool.GetList<Item>();

                if (args == null)
                {
                    if (_configData.GlobalSettings.Containers.Main && (type == null || type.Main))
                    {
                        itemsSelected.AddRange(CloneItemList(playerMain.itemList));
                    }

                    if (playerWear != null && _configData.GlobalSettings.Containers.Wear && type != null && type.Wear)
                    {
                        itemsSelected.AddRange(CloneItemList(playerWear.itemList));
                    }

                    if (playerBelt != null && _configData.GlobalSettings.Containers.Belt && type != null && type.Belt)
                    {
                        itemsSelected.AddRange(CloneItemList(playerBelt.itemList));
                    }
                }
                else
                {
                    if (args[0].Equals("existing"))
                    {
                        if (_configData.GlobalSettings.Containers.Main && (type == null || type.Main))
                        {
                            itemsSelected.AddRange(GetExistingItems(playerMain, container));
                        }

                        if (playerWear != null && _configData.GlobalSettings.Containers.Wear && type != null && type.Wear)
                        {
                            itemsSelected.AddRange(GetExistingItems(playerWear, container));
                        }

                        if (playerBelt != null && _configData.GlobalSettings.Containers.Belt && type != null && type.Belt)
                        {
                            itemsSelected.AddRange(GetExistingItems(playerBelt, container));
                        }
                    }
                    else
                    {
                        ItemCategory category = StringToItemCategory(args[0]);
                        if (_configData.GlobalSettings.Containers.Main && (type == null || type.Main))
                        {
                            itemsSelected.AddRange(GetItemsOfType(playerMain, category));
                        }

                        if (playerWear != null && _configData.GlobalSettings.Containers.Wear && type != null && type.Wear)
                        {
                            itemsSelected.AddRange(GetItemsOfType(playerWear, category));
                        }

                        if (playerBelt != null && _configData.GlobalSettings.Containers.Belt && type != null && type.Belt)
                        {
                            itemsSelected.AddRange(GetItemsOfType(playerBelt, category));
                        }
                    }
                }

                IEnumerable<Item> uselessItems = GetUselessItems(itemsSelected, container);

                foreach (Item item in uselessItems)
                {
                    itemsSelected.Remove(item);
                }

                itemsSelected.Sort((item1, item2) => item2.info.itemid.CompareTo(item1.info.itemid));

                MoveItems(itemsSelected, container);

                Pool.FreeList(ref itemsSelected);
            }
        }

        #endregion Loot Handling

        #region Helpers

        private IEnumerable<Item> GetUselessItems(IEnumerable<Item> items, ItemContainer container)
        {
            BaseOven furnace = container.entityOwner?.GetComponent<BaseOven>();

            List<Item> uselessItems = new List<Item>();

            if (furnace != null)
            {
                foreach (Item item in items)
                {
                    ItemModCookable cookable = item.info.GetComponent<ItemModCookable>();

                    if (cookable == null || cookable.lowTemp > furnace.cookingTemperature || cookable.highTemp < furnace.cookingTemperature)
                    {
                        uselessItems.Add(item);
                    }
                }
            }

            return uselessItems;
        }

        private List<Item> CloneItemList(IEnumerable<Item> list)
        {
            List<Item> clone = new List<Item>();

            foreach (Item item in list)
            {
                clone.Add(item);
            }

            return clone;
        }

        private List<Item> GetExistingItems(ItemContainer primary, ItemContainer secondary)
        {
            List<Item> existingItems = new List<Item>();

            if (primary != null && secondary != null)
            {
                foreach (Item t in primary.itemList)
                {
                    foreach (Item t1 in secondary.itemList)
                    {
                        if (t.info.itemid != t1.info.itemid)
                        {
                            continue;
                        }

                        existingItems.Add(t);
                        break;
                    }
                }
            }

            return existingItems;
        }

        private List<Item> GetItemsOfType(ItemContainer container, ItemCategory category)
        {
            List<Item> items = new List<Item>();

            foreach (Item item in container.itemList)
            {
                if (item.info.category == category)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        private List<ItemContainer> GetLootedInventory(BasePlayer player)
        {
            PlayerLoot playerLoot = player.inventory.loot;

            if (playerLoot != null && playerLoot.IsLooting())
            {
                List<ItemContainer> containers = playerLoot.containers;

                foreach (ItemContainer container in containers)
                {
                    BaseEntity entity = container.entityOwner;

                    if (entity != null && IsContainerExcluded(player, entity))
                    {
                        return null;
                    }
                }

                return containers;
            }

            return null;
        }

        private void MoveItems(IEnumerable<Item> items, ItemContainer to)
        {
            foreach (Item item in items)
            {
                item.MoveToContainer(to);
            }
        }

        private ItemCategory StringToItemCategory(string categoryName)
        {
            string[] categoryNames = Enum.GetNames(typeof(ItemCategory));

            for (int i = 0; i < categoryNames.Length; i++)
            {
                if (categoryName.ToLower().Equals(categoryNames[i].ToLower()))
                {
                    return (ItemCategory)i;
                }
            }

            return (ItemCategory)categoryNames.Length;
        }

        private bool IsContainerExcluded(BasePlayer player, BaseEntity entity)
        {
            if (entity != null)
            {
                VendingMachine vendingMachine = entity as VendingMachine;
                if (vendingMachine != null && !vendingMachine.PlayerBehind(player))
                {
                    return true;
                }

                if (entity is ShopFront || entity is BigWheelBettingTerminal)
                {
                    return true;
                }

                if (!_configData.GlobalSettings.ContainersExcluded.IsNullOrEmpty() && _configData.GlobalSettings.ContainersExcluded.Contains(entity.ShortPrefabName))
                {
                    return true;
                }
            }

            return false;
        }

        private void PlayerSendMessage(BasePlayer player, string message)
        {
            player.SendConsoleCommand("chat.add", 2, _configData.GlobalSettings.SteamIDIcon, string.IsNullOrEmpty(Lang("Prefix", player.UserIDString)) ? message : Lang("Prefix", player.UserIDString) + message);
        }

        #endregion Helpers

        #region User Interface

        private void UserInterface(BasePlayer player)
        {
            PlayerData playerData = GetPlayerData(player.userID);

            bool enabled = _configData.GlobalSettings.DefaultEnabled;

            if (playerData != null)
            {
                enabled = playerData.Enabled;
            }

            if (!enabled)
            {
                return;
            }

            DestroyGUI(player);

            string uiStyle = _configData.GlobalSettings.DefaultUiStyle;

            if (playerData != null)
            {
                uiStyle = playerData.UiStyle;
            }

            switch (uiStyle)
            {
                case "center":
                    UiCenter(player);
                    break;
                case "lite":
                    UiLite(player);
                    break;
                case "right":
                    UiRight(player);
                    break;
                case "custom":
                    UiCustom(player);
                    break;
            }
        }

        #region UI Custom

        private void UiCustom(BasePlayer player)
        {
            CuiElementContainer elements = new CuiElementContainer();
            ConfigData.UiConfiguration customUISettings = _configData.CustomUISettings;

            string panel = elements.Add(new CuiPanel
            {
                Image = { Color = customUISettings.Color },
                RectTransform = {
                    AnchorMin = customUISettings.AnchorsMin,
                    AnchorMax = customUISettings.AnchorsMax,
                    OffsetMin = customUISettings.OffsetsMin,
                    OffsetMax = customUISettings.OffsetsMax
                }
            }, "Hud.Menu", GUIPanelName);

            //left
            elements.Add(new CuiLabel
            {
                Text = { Text = Lang("Deposit", player.UserIDString), FontSize = customUISettings.TextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor },
                RectTransform = { AnchorMin = "0.02 0.8", AnchorMax = "0.35 1" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui existing", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.02 0.6", AnchorMax = "0.35 0.8" },
                Text = { Text = Lang("DepositExisting", player.UserIDString), FontSize = customUISettings.TextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.02 0.35", AnchorMax = "0.35 0.55" },
                Text = { Text = Lang("DepositAll", player.UserIDString), FontSize = customUISettings.TextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            if (permission.UserHasPermission(player.UserIDString, PermissionLootAll))
            {
                elements.Add(new CuiButton
                {
                    Button = { Command = "quicksortgui.lootall", Color = customUISettings.LootAllColor },
                    RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.35 0.3" },
                    Text = { Text = Lang("LootAll", player.UserIDString), FontSize = customUISettings.TextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
                }, panel);
            }

            //center
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui weapon", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.37 0.818", AnchorMax = "0.65 0.949" },
                Text = { Text = Lang("DepositWeapons", player.UserIDString), FontSize = customUISettings.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui ammunition", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.37 0.664", AnchorMax = "0.65 0.796" },
                Text = { Text = Lang("DepositAmmo", player.UserIDString), FontSize = customUISettings.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui medical", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.37 0.511", AnchorMax = "0.65 0.642" },
                Text = { Text = Lang("DepositMedical", player.UserIDString), FontSize = customUISettings.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui attire", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.37 0.358", AnchorMax = "0.65 0.489" },
                Text = { Text = Lang("DepositAttire", player.UserIDString), FontSize = customUISettings.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui resources", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.37 0.204", AnchorMax = "0.65 0.336" },
                Text = { Text = Lang("DepositResources", player.UserIDString), FontSize = customUISettings.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui component", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.37 0.051", AnchorMax = "0.65 0.182" },
                Text = { Text = Lang("DepositComponents", player.UserIDString), FontSize = customUISettings.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            //right
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui construction", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.818", AnchorMax = "0.98 0.949" },
                Text = { Text = Lang("DepositConstruction", player.UserIDString), FontSize = customUISettings.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui items", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.664", AnchorMax = "0.98 0.796" },
                Text = { Text = Lang("DepositItems", player.UserIDString), FontSize = customUISettings.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui tool", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.511", AnchorMax = "0.98 0.642" },
                Text = { Text = Lang("DepositTools", player.UserIDString), FontSize = customUISettings.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui food", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.358", AnchorMax = "0.82 0.489" },
                Text = { Text = Lang("DepositFood", player.UserIDString), FontSize = customUISettings.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui misc", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.83 0.358", AnchorMax = "0.98 0.489" },
                Text = { Text = Lang("DepositMisc", player.UserIDString), FontSize = customUISettings.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui traps", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.204", AnchorMax = "0.98 0.336" },
                Text = { Text = Lang("DepositTraps", player.UserIDString), FontSize = customUISettings.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui electrical", Color = customUISettings.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.051", AnchorMax = "0.98 0.182" },
                Text = { Text = Lang("DepositElectrical", player.UserIDString), FontSize = customUISettings.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = customUISettings.TextColor }
            }, panel);

            CuiHelper.AddUi(player, elements);
        }

        #endregion UI Custom

        #region UI Center

        private void UiCenter(BasePlayer player)
        {
            CuiElementContainer elements = new CuiElementContainer();

            string panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.5 0.5 0.5 0.33" },
                RectTransform = {
                    AnchorMin = "0.5 0.0",
                    AnchorMax = "0.5 0.0",
                    OffsetMin = "-198 472",
                    OffsetMax = "182 626"
                }
            }, "Hud.Menu", GUIPanelName);

            //left
            elements.Add(new CuiLabel
            {
                Text = { Text = Lang("Deposit", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"},
                RectTransform = { AnchorMin = "0.02 0.8", AnchorMax = "0.35 1" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui existing", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.02 0.6", AnchorMax = "0.35 0.8" },
                Text = { Text = Lang("DepositExisting", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.02 0.35", AnchorMax = "0.35 0.55" },
                Text = { Text = Lang("DepositAll", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);

            if (permission.UserHasPermission(player.UserIDString, PermissionLootAll))
            {
                elements.Add(new CuiButton
                {
                    Button = { Command = "quicksortgui.lootall", Color = "0.41 0.50 0.25 0.8" },
                    RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.35 0.3" },
                    Text = { Text = Lang("LootAll", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
                }, panel);
            }

            //center
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui weapon", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.37 0.818", AnchorMax = "0.65 0.949" },
                Text = { Text = Lang("DepositWeapons", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui ammunition", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.37 0.664", AnchorMax = "0.65 0.796" },
                Text = { Text = Lang("DepositAmmo", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui medical", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.37 0.511", AnchorMax = "0.65 0.642" },
                Text = { Text = Lang("DepositMedical", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui attire", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.37 0.358", AnchorMax = "0.65 0.489" },
                Text = { Text = Lang("DepositAttire", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui resources", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.37 0.204", AnchorMax = "0.65 0.336" },
                Text = { Text = Lang("DepositResources", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui component", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.37 0.051", AnchorMax = "0.65 0.182" },
                Text = { Text = Lang("DepositComponents", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);

            //right
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui construction", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.818", AnchorMax = "0.98 0.949" },
                Text = { Text = Lang("DepositConstruction", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui items", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.664", AnchorMax = "0.98 0.796" },
                Text = { Text = Lang("DepositItems", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui tool", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.511", AnchorMax = "0.98 0.642" },
                Text = { Text = Lang("DepositTools", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui food", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.358", AnchorMax = "0.82 0.489" },
                Text = { Text = Lang("DepositFood", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui misc", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.83 0.358", AnchorMax = "0.98 0.489" },
                Text = { Text = Lang("DepositMisc", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui traps", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.204", AnchorMax = "0.98 0.336" },
                Text = { Text = Lang("DepositTraps", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui electrical", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.051", AnchorMax = "0.98 0.182" },
                Text = { Text = Lang("DepositElectrical", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            CuiHelper.AddUi(player, elements);
        }

        #endregion UI Center

        #region UI Lite

        private void UiLite(BasePlayer player)
        {
            CuiElementContainer elements = new CuiElementContainer();

            string panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = {
                    AnchorMin = "0.5 0.0",
                    AnchorMax = "0.5 0.0",
                    OffsetMin = "-56 340",
                    OffsetMax = "179 359"
                }
            }, "Hud.Menu", GUIPanelName);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui existing", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.44 1" },
                Text = { Text = Lang("DepositExisting", player.UserIDString), FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.46 0", AnchorMax = "0.60 1" },
                Text = { Text = Lang("DepositAll", player.UserIDString), FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            if (permission.UserHasPermission(player.UserIDString, PermissionLootAll))
            {
                elements.Add(new CuiButton
                {
                    Button = { Command = "quicksortgui.lootall", Color = "0.41 0.50 0.25 0.8" },
                    RectTransform = { AnchorMin = "0.62 0", AnchorMax = "1 1" },
                    Text = { Text = Lang("LootAll", player.UserIDString), FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
                }, panel);
            }

            CuiHelper.AddUi(player, elements);
        }

        #endregion UI Lite

        #region UI Right

        private void UiRight(BasePlayer player)
        {
            CuiElementContainer elements = new CuiElementContainer();

            string panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.5 0.5 0.5 0.33" },
                RectTransform = {                    
                    AnchorMin = "0.5 1.0",
                    AnchorMax = "0.5 1.0",
                    OffsetMin = "192 -137",
                    OffsetMax = "573 0"
                }
            }, "Hud.Menu", GUIPanelName);

            //left
            elements.Add(new CuiLabel
            {
                Text = { Text = Lang("Deposit", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" },
                RectTransform = { AnchorMin = "0.02 0.8", AnchorMax = "0.35 1" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui existing", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.02 0.6", AnchorMax = "0.35 0.8" },
                Text = { Text = Lang("DepositExisting", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.02 0.35", AnchorMax = "0.35 0.55" },
                Text = { Text = Lang("DepositAll", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            if (permission.UserHasPermission(player.UserIDString, PermissionLootAll))
            {
                elements.Add(new CuiButton
                {
                    Button = { Command = "quicksortgui.lootall", Color = "0.41 0.50 0.25 0.8" },
                    RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.35 0.3" },
                    Text = { Text = Lang("LootAll", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
                }, panel);
            }

            //center
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui weapon", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.37 0.818", AnchorMax = "0.65 0.949" },
                Text = { Text = Lang("DepositWeapons", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui ammunition", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.37 0.664", AnchorMax = "0.65 0.796" },
                Text = { Text = Lang("DepositAmmo", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui medical", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.37 0.511", AnchorMax = "0.65 0.642" },
                Text = { Text = Lang("DepositMedical", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui attire", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.37 0.358", AnchorMax = "0.65 0.489" },
                Text = { Text = Lang("DepositAttire", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui resources", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.37 0.204", AnchorMax = "0.65 0.336" },
                Text = { Text = Lang("DepositResources", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui component", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.37 0.051", AnchorMax = "0.65 0.182" },
                Text = { Text = Lang("DepositComponents", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);
            //right
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui construction", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.818", AnchorMax = "0.98 0.949" },
                Text = { Text = Lang("DepositConstruction", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui items", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.664", AnchorMax = "0.98 0.796" },
                Text = { Text = Lang("DepositItems", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui tool", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.511", AnchorMax = "0.98 0.642" },
                Text = { Text = Lang("DepositTools", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui food", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.358", AnchorMax = "0.82 0.489" },
                Text = { Text = Lang("DepositFood", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui misc", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.83 0.358", AnchorMax = "0.98 0.489" },
                Text = { Text = Lang("DepositMisc", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui traps", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.204", AnchorMax = "0.98 0.336" },
                Text = { Text = Lang("DepositTraps", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui electrical", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.051", AnchorMax = "0.98 0.182" },
                Text = { Text = Lang("DepositElectrical", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            CuiHelper.AddUi(player, elements);
        }

        #endregion UI Right

        private void DestroyGUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, GUIPanelName);
        }

        #endregion User Interface
    }
}