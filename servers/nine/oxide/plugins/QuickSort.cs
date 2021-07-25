using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Quick Sort", "MON@H", "1.5.2")]
    [Description("Adds a GUI that allows players to quickly sort items into containers")]
    public class QuickSort : CovalencePlugin
    {
        #region Initialization
        private static readonly Hash<ulong, string> _guiInfo = new Hash<ulong, string>();

        private const string PermissionAutoLootAll = "quicksort.autolootall";
        private const string PermissionLootAll = "quicksort.lootall";
        private const string PermissionUse = "quicksort.use";

        private void Init()
        {
            Unsubscribe(nameof(OnBackpackClosed));
            Unsubscribe(nameof(OnBackpackOpened));
            Unsubscribe(nameof(OnLootEntity));
            Unsubscribe(nameof(OnLootPlayer));
            Unsubscribe(nameof(OnPlayerLootEnd));
            Unsubscribe(nameof(OnPlayerTick));

            LoadData();

            permission.RegisterPermission(PermissionAutoLootAll, this);
            permission.RegisterPermission(PermissionLootAll, this);
            permission.RegisterPermission(PermissionUse, this);

            foreach (string command in _configData.ChatSettings.Commands)
            {
                AddCovalenceCommand(command, nameof(CmdQuickSort));
            }
        }

        private void OnServerInitialized()
        {
            UpdateConfig();

            if (_configData.GlobalSettings.BackpacksEnabled)
            {
                Subscribe(nameof(OnBackpackClosed));
                Subscribe(nameof(OnBackpackOpened));                
            }

            Subscribe(nameof(OnLootEntity));
            Subscribe(nameof(OnLootPlayer));
            Subscribe(nameof(OnPlayerLootEnd));
            Subscribe(nameof(OnPlayerTick));
        }

        private void UpdateConfig()
        {
            if (_configData.ChatSettings.Commands.Length == 0)
            {
                _configData.ChatSettings.Commands = new[] { "qs" };
                SaveConfig();
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

            [JsonProperty(PropertyName = "Chat settings")]
            public ChatConfiguration ChatSettings = new ChatConfiguration();

            public class GlobalConfiguration
            {
                [JsonProperty(PropertyName = "Use permissions")]
                public bool UsePermission = true;

                [JsonProperty(PropertyName = "Allows admins to use Quick Sort without permission")]
                public bool AdminsAllowed = true;

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
                public GlobalConfiguration.ContainerTypesEnabled Containers = new GlobalConfiguration.ContainerTypesEnabled();
                public class ContainerTypesEnabled
                {
                    public bool Belt = false;
                    public bool Main = true;
                    public bool Wear = false;

                }

                [JsonProperty(PropertyName = "Excluded containers")]
                public string[] ContainersExcluded = new[]
                {
                    "autoturret_deployed",
                    "bandit_shopkeeper",
                    "bigwheelbettingterminal",
                    "dropbox.deployed",
                    "flameturret.deployed",
                    "guntrap.deployed",
                    "npcvendingmachine",
                    "npcvendingmachine",
                    "npcvendingmachine_attire",
                    "npcvendingmachine_building",
                    "npcvendingmachine_building",
                    "npcvendingmachine_building_hapis",
                    "npcvendingmachine_buyres_hapis",
                    "npcvendingmachine_components",
                    "npcvendingmachine_food_hapis",
                    "npcvendingmachine_hapis_hapis",
                    "npcvendingmachine_resources",
                    "npcvendingmachine_tools",
                    "npcvendingmachine_weapons",
                    "npcvendingmachine_weapons_hapis",
                    "sam_site_turret_deployed",
                    "sam_static",
                    "scientist_turret_any",
                    "scientist_turret_lr300",
                    "shopkeeper_vm_invis",
                    "shopkeeper_vm_invis",
                    "vending_mapmarker",
                    "vendingmachine.deployed",
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

            public class ChatConfiguration
            {
                [JsonProperty(PropertyName = "Chat command")]
                public string[] Commands = new[] { "qs", "quicksort" };

                [JsonProperty(PropertyName = "Chat steamID icon")]
                public ulong SteamIDIcon = 0;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null)
                    LoadDefaultConfig();
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
            }
            SaveConfig();
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
            public readonly Hash<ulong, PlayerData> PlayerDataList = new Hash<ulong, PlayerData>();

            public class PlayerData
            {
                public bool Enabled;
                public bool AutoLootAll;
                public string UiStyle;
                public ConfigData.GlobalConfiguration.ContainerTypesEnabled Containers;
            }
        }

        private StoredData.PlayerData GetPlayerData(ulong playerID)
        {
            StoredData.PlayerData playerData;
            if (!_storedData.PlayerDataList.TryGetValue(playerID, out playerData))
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
                _storedData = null;
            }
            finally
            {
                if (_storedData == null)
                {
                    ClearData();
                }
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);

        private void ClearData()
        {
            _storedData = new StoredData();
            SaveData();
        }

        private void OnNewSave(string filename)
        {
            SaveData();
        }

        #endregion DataFile

        #region Localization

        private string Lang(string key, string userID = "", params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, userID), args);
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
                ["SyntaxError"] = "Syntax error, type '<color=#FFFF00>/{0} <help | h></color>' to view help",
                ["QuickSort"] = "Quick Sort GUI is now {0}",
                ["Style"] = "Quick Sort GUI style is now {0}",
                ["AutoLootAll"] = "Automated looting is now {0}",
                ["ContainerType"] = "Quick Sort for container type {0} is now {1}",
                ["Prefix"] = "<color=#00FFFF>[Quick Sort]</color>: ",
                ["Help"] = "List Commands:\n" +
                "<color=#FFFF00>/{0}</color> - Enable/Disable GUI.\n" +
                "<color=#FFFF00>/{0} auto - Enable/Disable automated looting.\n" +
                "<color=#FFFF00>/{0} style \"center/lite/right/custom\" - change GUI style.\n" +
                "<color=#FFFF00>/{0} conatiner \"main/wear/belt\" - add/remove container type from the sort.",
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Deposit"] = "Положить",
                ["DepositAll"] = "Всё",
                ["DepositAmmo"] = "Патроны",
                ["DepositAttire"] = "Одежда",
                ["DepositConstruction"] = "Конструкции",
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
                ["SyntaxError"] = "Синтаксическая ошибка, напишите '<color=#FFFF00>/{0} <help | h></color>' чтобы отобразить подсказки",
                ["QuickSort"] = "GUI быстрой сортировки теперь {0}",
                ["Style"] = "Стиль GUI быстрой сортировки теперь {0}",
                ["AutoLootAll"] = "Забирать всё автоматически теперь {0}",
                ["ContainerType"] = "Быстрая сортировка для типа контейнера {0} теперь {1}",
                ["Prefix"] = "<color=#00FFFF>[Quick Sort]</color>: ",
                ["Help"] = "Список команд:\n" +
                "<color=#FFFF00>/{0}</color> - Включить/Отключить GUI быстрой сортировки.\n" +
                "<color=#FFFF00>/{0} auto</color> - Включить/Отключить забирать всё автоматически.\n" +
                "<color=#FFFF00>/{0} style <center/lite/right/custom></color> - изменить стиль GUI быстрой сортировки.\n" +
                "<color=#FFFF00>/{0} conatiner <main/wear/belt></color> - добавить/удалить тип контейнера для сортировки.",
            }, this, "ru");
        }

        #endregion Localization

        #region Game Hooks

        private void OnLootPlayer(BasePlayer player)
        {
            if (UserHasPerm(player, PermissionUse))
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
            else if (IsContainerExcluded(entity))
            {
                return;
            }
            else if (UserHasPerm(player, PermissionAutoLootAll))
            {

                StoredData.PlayerData playerData = GetPlayerData(player.userID);
                bool autoLootAll = _configData.GlobalSettings.AutoLootAll;
                if (playerData != null) autoLootAll = playerData.AutoLootAll;
                if (autoLootAll)
                {
                    timer.Once(_configData.GlobalSettings.LootAllDelay, () =>
                    {
                        List<ItemContainer> containers = GetLootedInventory(player);

                        if (containers != null)
                        {
                            foreach (ItemContainer c in containers)
                            {
                                if (c.HasFlag(ItemContainer.Flag.NoItemInput))
                                {
                                    AutoLoot(player);
                                    if (c.IsEmpty())
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
            if (UserHasPerm(player, PermissionUse))
            {
                UserInterface(player);
            }
        }

        private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            BasePlayer player = inventory.GetComponent<BasePlayer>();

            if (player != null)
            {
                DestroyUi(player);
            }
        }

        private void OnPlayerTick(BasePlayer player)
        {
            if (player.IsConnected && player.IsSleeping() && _guiInfo.ContainsKey(player.userID))
            {
                DestroyUi(player);
            }
        }

        private void OnBackpackOpened(BasePlayer player, ulong backpackOwnerID, ItemContainer backpackContainer)
        {
            if (UserHasPerm(player, PermissionUse))
            {
                UserInterface(player);
            }
        }

        private void OnBackpackClosed(BasePlayer player, ulong backpackOwnerID, ItemContainer backpackContainer)
        {
            if (player != null)
            {
                DestroyUi(player);
            }
        }

        #endregion Game Hooks

        #region Commands

        private void CmdQuickSort(IPlayer player, string command, string[] args)
        {
            if (_configData.GlobalSettings.UsePermission && !permission.UserHasPermission(player.Id, PermissionUse))
            {
                if (!_configData.GlobalSettings.AdminsAllowed || !player.IsAdmin)
                {
                    Print(player, Lang("NotAllowed", player.Id));
                    return;
                }
            }

            StoredData.PlayerData playerData = GetPlayerData(ulong.Parse(player.Id));
            if (playerData == null)
            {
                playerData = new StoredData.PlayerData
                {
                    Enabled = _configData.GlobalSettings.DefaultEnabled,
                    AutoLootAll = _configData.GlobalSettings.AutoLootAll,
                    UiStyle = _configData.GlobalSettings.DefaultUiStyle,
                    Containers = _configData.GlobalSettings.Containers,
                };
                _storedData.PlayerDataList.Add(ulong.Parse(player.Id), playerData);
                SaveData();
            }

            if (args == null || args.Length == 0)
            {
                playerData.Enabled = !playerData.Enabled;
                SaveData();
                Print(player, Lang("QuickSort", player.Id, playerData.Enabled ? Lang("Enabled", player.Id) : Lang("Disabled", player.Id)));
                return;
            }

            switch (args[0].ToLower())
            {
                case "h":
                case "help":
                    string firstCmd = _configData.ChatSettings.Commands[0];
                    Print(player, Lang("Help", player.Id, firstCmd));
                    return;
                case "auto":
                    playerData.AutoLootAll = !playerData.AutoLootAll;
                    SaveData();
                    Print(player, Lang("AutoLootAll", player.Id, playerData.AutoLootAll ? Lang("Enabled", player.Id) : Lang("Disabled", player.Id)));
                    return;
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
                                        Print(player, Lang("Style", player.Id, args[1].ToLower()));
                                        return;
                                    }
                            }
                        }
                        break;
                    }
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

                                        Print(player, Lang("ContainerType", player.Id, "main", playerData.Containers.Main ? Lang("Enabled", player.Id) : Lang("Disabled", player.Id)));
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

                                        Print(player, Lang("ContainerType", player.Id, "wear", playerData.Containers.Wear ? Lang("Enabled", player.Id) : Lang("Disabled", player.Id)));
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

                                        Print(player, Lang("ContainerType", player.Id, "belt", playerData.Containers.Belt ? Lang("Enabled", player.Id) : Lang("Disabled", player.Id)));
                                        return;
                                    }
                            }
                        }
                        break;
                    }
            }
            Print(player, Lang("SyntaxError", player.Id, _configData.ChatSettings.Commands[0]));
        }

        [Command("quicksortgui")]
        private void SortCommand(IPlayer player, string command, string[] args)
        {
            if (UserHasPerm((player.Object as BasePlayer), PermissionUse))
            {
                try
                {
                    SortItems(player.Object as BasePlayer, args);
                }
                catch { }
            }
        }

        [Command("quicksortgui.lootall")]
        private void LootAllCommand(IPlayer player, string command, string[] args)
        {
            if (UserHasPerm((player.Object as BasePlayer), PermissionLootAll))
            {
                timer.Once(_configData.GlobalSettings.LootAllDelay, () => AutoLoot(player.Object as BasePlayer));
            }
        }

        [Command("quicksortgui.lootdelay")]
        private void LootDelayCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                int x;
                if (int.TryParse(args[0], out x))
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
                List<Item> itemsSelected = new List<Item>();
                foreach (ItemContainer c in containers)
                {
                    itemsSelected.AddRange(CloneItemList(c.itemList));
                }
                itemsSelected.Sort((item1, item2) => item2.info.itemid.CompareTo(item1.info.itemid));
                MoveItems(itemsSelected, playerMain);
            }
        }

        private void SortItems(BasePlayer player, string[] args)
        {
            if (player == null) return;
            ConfigData.GlobalConfiguration.ContainerTypesEnabled type = GetPlayerData(ulong.Parse(player.UserIDString))?.Containers;
            ItemContainer container = GetLootedInventory(player)[0];
            ItemContainer playerMain = player.inventory?.containerMain;
            ItemContainer playerWear = player.inventory?.containerWear;
            ItemContainer playerBelt = player.inventory?.containerBelt;

            if (container != null && playerMain != null && !container.HasFlag(ItemContainer.Flag.NoItemInput))
            {
                List<Item> itemsSelected = new List<Item>();

                if (args.Length == 1)
                {
                    if (string.IsNullOrEmpty(args[0])) return;
                    if (args[0].Equals("existing"))
                    {
                        if (_configData.GlobalSettings.Containers.Main && (type == null || type.Main))
                            itemsSelected.AddRange(GetExistingItems(playerMain, container));
                        if (playerWear != null && _configData.GlobalSettings.Containers.Wear && type != null && type.Wear)
                            itemsSelected.AddRange(GetExistingItems(playerWear, container));
                        if (playerBelt != null && _configData.GlobalSettings.Containers.Belt && type != null && type.Belt)
                            itemsSelected.AddRange(GetExistingItems(playerBelt, container));
                    }
                    else
                    {
                        ItemCategory category = StringToItemCategory(args[0]);
                        if (_configData.GlobalSettings.Containers.Main && (type == null || type.Main))
                            itemsSelected.AddRange(GetItemsOfType(playerMain, category));
                        if (playerWear != null && _configData.GlobalSettings.Containers.Wear && type != null && type.Wear)
                            itemsSelected.AddRange(GetItemsOfType(playerWear, category));
                        if (playerBelt != null && _configData.GlobalSettings.Containers.Belt && type != null && type.Belt)
                            itemsSelected.AddRange(GetItemsOfType(playerBelt, category));
                    }
                }
                else
                {
                    if (_configData.GlobalSettings.Containers.Main && (type == null || type.Main))
                        itemsSelected.AddRange(CloneItemList(playerMain.itemList));
                    if (playerWear != null && _configData.GlobalSettings.Containers.Wear && type != null && type.Wear)
                        itemsSelected.AddRange(CloneItemList(playerWear.itemList));
                    if (playerBelt != null && _configData.GlobalSettings.Containers.Belt && type != null && type.Belt)
                        itemsSelected.AddRange(CloneItemList(playerBelt.itemList));
                }

                IEnumerable<Item> uselessItems = GetUselessItems(itemsSelected, container);

                foreach (Item item in uselessItems)
                {
                    itemsSelected.Remove(item);
                }

                itemsSelected.Sort((item1, item2) => item2.info.itemid.CompareTo(item1.info.itemid));
                MoveItems(itemsSelected, container);
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
                    if (entity != null && IsContainerExcluded(entity))
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

        private bool UserHasPerm(BasePlayer player, string perm)
        {
            if (player != null)
            {
                if (!_configData.GlobalSettings.UsePermission)
                {
                    return true;
                }
                else if (_configData.GlobalSettings.UsePermission && permission.UserHasPermission(player.UserIDString, perm))
                {
                    return true;
                }
                else if (_configData.GlobalSettings.AdminsAllowed && player.IsAdmin)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsContainerExcluded(BaseEntity entity)
        {
            if (entity != null)
            {
                if ((entity is VendingMachine) || (entity is ShopFront) || (entity is BigWheelBettingTerminal))
                {
                    return true;
                }
                else if (!_configData.GlobalSettings.ContainersExcluded.IsNullOrEmpty() && _configData.GlobalSettings.ContainersExcluded.Contains(entity.ShortPrefabName))
                {
                    return true;
                }
            }

            return false;
        }

        private void Print(IPlayer player, string message)
        {
            string text = string.IsNullOrEmpty(Lang("Prefix", player.Id)) ? string.Empty : $"{Lang("Prefix", player.Id)}{message}";

            (player.Object as BasePlayer).SendConsoleCommand("chat.add", 2, _configData.ChatSettings.SteamIDIcon, text);
            return;
        }

        #endregion Helpers

        #region User Interface

        private void UserInterface(BasePlayer player)
        {
            StoredData.PlayerData playerData = GetPlayerData(player.userID);
            bool enabled = _configData.GlobalSettings.DefaultEnabled;
            if (playerData != null) enabled = playerData.Enabled;
            if (!enabled) return;

            DestroyUi(player);
            _guiInfo[player.userID] = CuiHelper.GetGuid();
            player.inventory.loot.gameObject.AddComponent<UIDestroyer>();

            string uiStyle = _configData.GlobalSettings.DefaultUiStyle;
            if (playerData != null) uiStyle = playerData.UiStyle;
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
            ConfigData.UiConfiguration cfg = _configData.CustomUISettings;

            string panel = elements.Add(new CuiPanel
            {
                Image = { Color = cfg.Color },
                RectTransform = {
                    AnchorMin = cfg.AnchorsMin,
                    AnchorMax = cfg.AnchorsMax,
                    OffsetMin = cfg.OffsetsMin,
                    OffsetMax = cfg.OffsetsMax
                }
            }, "Hud.Menu", _guiInfo[player.userID]);
            //left
            elements.Add(new CuiLabel
            {
                Text = { Text = Lang("Deposit", player.UserIDString), FontSize = cfg.TextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor },
                RectTransform = { AnchorMin = "0.02 0.8", AnchorMax = "0.35 1" }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui existing", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.02 0.6", AnchorMax = "0.35 0.8" },
                Text = { Text = Lang("DepositExisting", player.UserIDString), FontSize = cfg.TextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.02 0.35", AnchorMax = "0.35 0.55" },
                Text = { Text = Lang("DepositAll", player.UserIDString), FontSize = cfg.TextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            if (UserHasPerm(player, PermissionLootAll))
            {
                elements.Add(new CuiButton
                {
                    Button = { Command = "quicksortgui.lootall", Color = cfg.LootAllColor },
                    RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.35 0.3" },
                    Text = { Text = Lang("LootAll", player.UserIDString), FontSize = cfg.TextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
                }, panel);
            }
            //center
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui weapon", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.37 0.818", AnchorMax = "0.65 0.949" },
                Text = { Text = Lang("DepositWeapons", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui ammunition", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.37 0.664", AnchorMax = "0.65 0.796" },
                Text = { Text = Lang("DepositAmmo", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui medical", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.37 0.511", AnchorMax = "0.65 0.642" },
                Text = { Text = Lang("DepositMedical", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui attire", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.37 0.358", AnchorMax = "0.65 0.489" },
                Text = { Text = Lang("DepositAttire", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui resources", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.37 0.204", AnchorMax = "0.65 0.336" },
                Text = { Text = Lang("DepositResources", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui component", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.37 0.051", AnchorMax = "0.65 0.182" },
                Text = { Text = Lang("DepositComponents", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            //right
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui construction", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.818", AnchorMax = "0.98 0.949" },
                Text = { Text = Lang("DepositConstruction", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui items", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.664", AnchorMax = "0.98 0.796" },
                Text = { Text = Lang("DepositItems", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui tool", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.511", AnchorMax = "0.98 0.642" },
                Text = { Text = Lang("DepositTools", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui food", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.358", AnchorMax = "0.98 0.489" },
                Text = { Text = Lang("DepositFood", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui traps", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.204", AnchorMax = "0.98 0.336" },
                Text = { Text = Lang("DepositTraps", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui misc", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.051", AnchorMax = "0.98 0.182" },
                Text = { Text = Lang("DepositMisc", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
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
            }, "Hud.Menu", _guiInfo[player.userID]);
            //left
            elements.Add(new CuiLabel
            {
                Text = { Text = Lang("Deposit"), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"},
                RectTransform = { AnchorMin = "0.02 0.8", AnchorMax = "0.35 1" }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui existing", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.02 0.6", AnchorMax = "0.35 0.8" },
                Text = { Text = Lang("DepositExisting"), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.02 0.35", AnchorMax = "0.35 0.55" },
                Text = { Text = Lang("DepositAll"), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);
            if (UserHasPerm(player, PermissionLootAll))
            {
                elements.Add(new CuiButton
                {
                    Button = { Command = "quicksortgui.lootall", Color = "0.41 0.50 0.25 0.8" },
                    RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.35 0.3" },
                    Text = { Text = Lang("LootAll"), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
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
                RectTransform = { AnchorMin = "0.67 0.358", AnchorMax = "0.98 0.489" },
                Text = { Text = Lang("DepositFood", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui traps", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.204", AnchorMax = "0.98 0.336" },
                Text = { Text = Lang("DepositTraps", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui misc", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.051", AnchorMax = "0.98 0.182" },
                Text = { Text = Lang("DepositMisc", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8"}
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
            }, "Hud.Menu", _guiInfo[player.userID]);

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
            if (UserHasPerm(player, PermissionLootAll))
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
            }, "Hud.Menu", _guiInfo[player.userID]);
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
            if (UserHasPerm(player, PermissionLootAll))
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
                RectTransform = { AnchorMin = "0.67 0.358", AnchorMax = "0.98 0.489" },
                Text = { Text = Lang("DepositFood", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui traps", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.204", AnchorMax = "0.98 0.336" },
                Text = { Text = Lang("DepositTraps", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksortgui misc", Color = "0.75 0.43 0.18 0.8" },
                RectTransform = { AnchorMin = "0.67 0.051", AnchorMax = "0.98 0.182" },
                Text = { Text = Lang("DepositMisc", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.77 0.92 0.67 0.8" }
            }, panel);

            CuiHelper.AddUi(player, elements);
        }

        #endregion UI Right

        #region Cleanup

        private static void DestroyUi(BasePlayer player)
        {
            string gui;
            if (_guiInfo.TryGetValue(player.userID, out gui))
            {
                CuiHelper.DestroyUi(player, gui);
                _guiInfo.Remove(player.userID);
            }
        }

        private class UIDestroyer : MonoBehaviour
        {
            private void PlayerStoppedLooting(BasePlayer player)
            {
                DestroyUi(player);
                Destroy(this);
            }
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyUi(player);
            }
            SaveData();
        }

        #endregion Cleanup

        #endregion User Interface
    }
}