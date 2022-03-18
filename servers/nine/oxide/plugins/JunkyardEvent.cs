using System;
using System.Collections;
using System.Collections.Generic;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Plugins.JunkyardEventExtensionMethods;

namespace Oxide.Plugins
{
    [Info("JunkyardEvent", "KpucTaJl", "2.0.1")]
    internal class JunkyardEvent : RustPlugin
    {
        #region Config
        private const bool En = true;

        private PluginConfig _config;

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a default config...");
            _config = PluginConfig.DefaultConfig();
            _config.PluginVersion = Version;
            SaveConfig();
            Puts("Creation of the default config completed!");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            if (_config.PluginVersion < Version) UpdateConfigValues();
        }

        private void UpdateConfigValues()
        {
            Puts("Config update detected! Updating config values...");
            _config.PluginVersion = Version;
            Puts("Config update completed!");
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        public class ItemConfig
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty(En ? "Minimum" : "Минимальное кол-во")] public int MinAmount { get; set; }
            [JsonProperty(En ? "Maximum" : "Максимальное кол-во")] public int MaxAmount { get; set; }
            [JsonProperty(En ? "Chance [0.0-100.0]" : "Шанс выпадения предмета [0.0-100.0]")] public float Chance { get; set; }
            [JsonProperty(En ? "Is this a blueprint? [true/false]" : "Это чертеж? [true/false]")] public bool IsBluePrint { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
            [JsonProperty(En ? "Name (empty - default)" : "Название (empty - default)")] public string Name { get; set; }
        }

        public class LootTableConfig
        {
            [JsonProperty(En ? "Minimum numbers of items" : "Минимальное кол-во элементов")] public int Min { get; set; }
            [JsonProperty(En ? "Maximum numbers of items" : "Максимальное кол-во элементов")] public int Max { get; set; }
            [JsonProperty(En ? "Use minimum and maximum values? [true/false]" : "Использовать минимальное и максимальное значение? [true/false]")] public bool UseCount { get; set; }
            [JsonProperty(En ? "List of items" : "Список предметов")] public List<ItemConfig> Items { get; set; }
        }

        public class PrefabConfig
        {
            [JsonProperty(En ? "Chance [0.0-100.0]" : "Шанс выпадения [0.0-100.0]")] public float Chance { get; set; }
            [JsonProperty(En ? "The path to the prefab" : "Путь к prefab-у")] public string PrefabDefinition { get; set; }
        }

        public class PrefabLootTableConfig
        {
            [JsonProperty(En ? "Minimum numbers of prefabs" : "Минимальное кол-во prefab-ов")] public int Min { get; set; }
            [JsonProperty(En ? "Maximum numbers of prefabs" : "Максимальное кол-во prefab-ов")] public int Max { get; set; }
            [JsonProperty(En ? "Use minimum and maximum values? [true/false]" : "Использовать минимальное и максимальное значение? [true/false]")] public bool UseCount { get; set; }
            [JsonProperty(En ? "List of prefabs" : "Список prefab-ов")] public List<PrefabConfig> Prefabs { get; set; }
        }

        public class CrateConfig
        {
            [JsonProperty(En ? "Number of crates" : "Кол-во ящиков")] public int Count { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу лута необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
        }

        public class HackCrateConfig
        {
            [JsonProperty(En ? "Number of crates" : "Кол-во ящиков")] public int Count { get; set; }
            [JsonProperty(En ? "Time to unlock the Crates [sec.]" : "Время разблокировки ящиков [sec.]")] public float UnlockTime { get; set; }
            [JsonProperty(En ? "Increase the event time if it's not enough to unlock the locked crate? [true/false]" : "Увеличивать время ивента, если недостаточно чтобы разблокировать заблокированный ящик? [true/false]")] public bool IncreaseEventTime { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу лута необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
        }

        public class ColorConfig
        {
            [JsonProperty("r")] public float R { get; set; }
            [JsonProperty("g")] public float G { get; set; }
            [JsonProperty("b")] public float B { get; set; }
        }

        public class MarkerConfig
        {
            [JsonProperty(En ? "Name" : "Название")] public string Name { get; set; }
            [JsonProperty(En ? "Radius" : "Радиус")] public float Radius { get; set; }
            [JsonProperty(En ? "Alpha" : "Прозрачность")] public float Alpha { get; set; }
            [JsonProperty(En ? "Marker color" : "Цвет маркера")] public ColorConfig Color { get; set; }
        }

        public class GuiConfig
        {
            [JsonProperty(En ? "Do you use the countdown GUI? [true/false]" : "Использовать ли GUI обратного отсчета? [true/false]")] public bool IsGui { get; set; }
            [JsonProperty("AnchorMin")] public string AnchorMin { get; set; }
            [JsonProperty("AnchorMax")] public string AnchorMax { get; set; }
        }

        public class GuiAnnouncementsConfig
        {
            [JsonProperty(En ? "Do you use the GUI Announcements? [true/false]" : "Использовать ли GUI Announcements? [true/false]")] public bool IsGuiAnnouncements { get; set; }
            [JsonProperty(En ? "Banner color" : "Цвет баннера")] public string BannerColor { get; set; }
            [JsonProperty(En ? "Text color" : "Цвет текста")] public string TextColor { get; set; }
            [JsonProperty(En ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float ApiAdjustVPosition { get; set; }
        }

        public class NotifyConfig
        {
            [JsonProperty(En ? "Do you use the Notify? [true/false]" : "Использовать ли Notify? [true/false]")] public bool IsNotify { get; set; }
            [JsonProperty(En ? "Type" : "Тип")] public string Type { get; set; }
        }

        public class DiscordConfig
        {
            [JsonProperty(En ? "Do you use the Discord? [true/false]" : "Использовать ли Discord? [true/false]")] public bool IsDiscord { get; set; }
            [JsonProperty("Webhook URL")] public string WebhookUrl { get; set; }
            [JsonProperty(En ? "Embed Color (DECIMAL)" : "Цвет полосы (DECIMAL)")] public int EmbedColor { get; set; }
            [JsonProperty(En ? "Keys of required messages" : "Ключи необходимых сообщений")] public HashSet<string> Keys { get; set; }
        }

        public class ScaleDamageConfig
        {
            [JsonProperty(En ? "Type of target" : "Тип цели")] public string Type { get; set; }
            [JsonProperty(En ? "Damage Multiplier" : "Множитель урона")] public float Scale { get; set; }
        }

        public class PveModeConfig
        {
            [JsonProperty(En ? "Use the PVE mode of the plugin? [true/false]" : "Использовать PVE режим работы плагина? [true/false]")] public bool Pve { get; set; }
            [JsonProperty(En ? "The amount of damage that the player has to do to become the Event Owner" : "Кол-во урона, которое должен нанести игрок, чтобы стать владельцем ивента")] public float Damage { get; set; }
            [JsonProperty(En ? "Damage coefficients for calculate to become the Event Owner" : "Коэффициенты урона для подсчета, чтобы стать владельцем события")] public HashSet<ScaleDamageConfig> ScaleDamage { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event loot the crates? [true/false]" : "Может ли не владелец ивента грабить ящики? [true/false]")] public bool LootCrate { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event hack locked crates? [true/false]" : "Может ли не владелец ивента взламывать заблокированные ящики? [true/false]")] public bool HackCrate { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event loot NPC corpses? [true/false]" : "Может ли не владелец ивента грабить трупы NPC? [true/false]")] public bool LootNpc { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event deal damage to the NPC? [true/false]" : "Может ли не владелец ивента наносить урон по NPC? [true/false]")] public bool DamageNpc { get; set; }
            [JsonProperty(En ? "Can an Npc attack a non-owner of the event? [true/false]" : "Может ли Npc атаковать не владельца ивента? [true/false]")] public bool TargetNpc { get; set; }
            [JsonProperty(En ? "Allow the non-owner of the event to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента не владельцу ивента? [true/false]")] public bool CanEnter { get; set; }
            [JsonProperty(En ? "Allow a player who has an active cooldown of the Event Owner to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента игроку, у которого активен кулдаун на получение статуса владельца ивента? [true/false]")] public bool CanEnterCooldownPlayer { get; set; }
            [JsonProperty(En ? "The time that the Event Owner may not be inside the event zone [sec.]" : "Время, которое владелец ивента может не находиться внутри зоны ивента [сек.]")] public int TimeExitOwner { get; set; }
            [JsonProperty(En ? "The time until the end of Event Owner status when it is necessary to warn the player [sec.]" : "Время таймера до окончания действия статуса владельца ивента, когда необходимо предупредить игрока [сек.]")] public int AlertTime { get; set; }
            [JsonProperty(En ? "Prevent the actions of the RestoreUponDeath plugin in the event zone? [true/false]" : "Запрещать работу плагина RestoreUponDeath в зоне действия ивента? [true/false]")] public bool RestoreUponDeath { get; set; }
            [JsonProperty(En ? "The time that the player can`t become the Event Owner, after the end of the event and the player was its owner [sec.]" : "Время, которое игрок не сможет стать владельцем ивента, после того как ивент окончен и игрок был его владельцем [sec.]")] public double CooldownOwner { get; set; }
            [JsonProperty(En ? "Darkening the dome (0 - disables the dome)" : "Затемнение купола (0 - отключает купол)")] public int Darkening { get; set; }
        }

        public class NpcBelt
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty(En ? "Amount" : "Кол-во")] public int Amount { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
            [JsonProperty(En ? "Mods" : "Модификации на оружие")] public HashSet<string> Mods { get; set; }
        }

        public class NpcWear
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
        }

        public class PresetConfig
        {
            [JsonProperty(En ? "Minimum" : "Минимальное кол-во")] public int Min { get; set; }
            [JsonProperty(En ? "Maximum" : "Максимальное кол-во")] public int Max { get; set; }
            [JsonProperty(En ? "List of locations" : "Список расположений")] public List<string> Positions { get; set; }
            [JsonProperty(En ? "NPCs setting" : "Настройки NPC")] public NpcConfig Config { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу предметов необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
        }

        public class NpcConfig
        {
            [JsonProperty(En ? "Name" : "Название")] public string Name { get; set; }
            [JsonProperty(En ? "Health" : "Кол-во ХП")] public float Health { get; set; }
            [JsonProperty(En ? "Roam Range" : "Дальность патрулирования местности")] public float RoamRange { get; set; }
            [JsonProperty(En ? "Chase Range" : "Дальность погони за целью")] public float ChaseRange { get; set; }
            [JsonProperty(En ? "Attack Range Multiplier" : "Множитель радиуса атаки")] public float AttackRangeMultiplier { get; set; }
            [JsonProperty(En ? "Sense Range" : "Радиус обнаружения цели")] public float SenseRange { get; set; }
            [JsonProperty(En ? "Target Memory Duration [sec.]" : "Длительность памяти цели [sec.]")] public float MemoryDuration { get; set; }
            [JsonProperty(En ? "Scale damage" : "Множитель урона")] public float DamageScale { get; set; }
            [JsonProperty(En ? "Aim Cone Scale" : "Множитель разброса")] public float AimConeScale { get; set; }
            [JsonProperty(En ? "Detect the target only in the NPC's viewing vision cone? [true/false]" : "Обнаруживать цель только в углу обзора NPC? [true/false]")] public bool CheckVisionCone { get; set; }
            [JsonProperty(En ? "Vision Cone" : "Угол обзора")] public float VisionCone { get; set; }
            [JsonProperty(En ? "Speed" : "Скорость")] public float Speed { get; set; }
            [JsonProperty(En ? "Disable radio effects? [true/false]" : "Отключать эффекты рации? [true/false]")] public bool DisableRadio { get; set; }
            [JsonProperty(En ? "Is this a stationary NPC? [true/false]" : "Это стационарный NPC? [true/false]")] public bool Stationary { get; set; }
            [JsonProperty(En ? "Remove a corpse after death? (it is recommended to use the true value to improve performance) [true/false]" : "Удалять труп после смерти? (рекомендуется использовать значение true для повышения производительности) [true/false]")] public bool IsRemoveCorpse { get; set; }
            [JsonProperty(En ? "Wear items" : "Одежда")] public HashSet<NpcWear> WearItems { get; set; }
            [JsonProperty(En ? "Belt items" : "Быстрые слоты")] public HashSet<NpcBelt> BeltItems { get; set; }
            [JsonProperty(En ? "Kit (it is recommended to use the previous 2 settings to improve performance)" : "Kit (рекомендуется использовать предыдущие 2 пункта настройки для повышения производительности)")] public string Kit { get; set; }
        }

        public class EconomyConfig
        {
            [JsonProperty(En ? "Which economy plugins do you want to use? (Economics, Server Rewards, IQEconomic)" : "Какие плагины экономики вы хотите использовать? (Economics, Server Rewards, IQEconomic)")] public HashSet<string> Plugins { get; set; }
            [JsonProperty(En ? "The minimum value that a player must collect to get points for the economy" : "Минимальное значение, которое игрок должен заработать, чтобы получить баллы за экономику")] public double Min { get; set; }
            [JsonProperty(En ? "Looting of crates" : "Ограбление ящиков")] public Dictionary<string, double> Crates { get; set; }
            [JsonProperty(En ? "Killing an NPC" : "Убийство NPC")] public double Npc { get; set; }
            [JsonProperty(En ? "Hacking a locked crate" : "Взлом заблокированного ящика")] public double LockedCrate { get; set; }
            [JsonProperty(En ? "Recycling car in a shredder" : "Переработка машины в шредере")] public double ShredderCar { get; set; }
            [JsonProperty(En ? "Recycling truck in a shredder" : "Переработка грузовика в шредере")] public double ShredderTruck { get; set; }
            [JsonProperty(En ? "List of commands that are executed in the console at the end of the event ({steamid} - the player who collected the highest number of points)" : "Список команд, которые выполняются в консоли по окончанию ивента ({steamid} - игрок, который набрал наибольшее кол-во баллов)")] public HashSet<string> Commands { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(En ? "Minimum time between events [sec.]" : "Минимальное время между ивентами [sec.]")] public float MinStartTime { get; set; }
            [JsonProperty(En ? "Maximum time between events [sec.]" : "Максимальное время между ивентами [sec.]")] public float MaxStartTime { get; set; }
            [JsonProperty(En ? "Is active the timer on to start the event? [true/false]" : "Активен ли таймер для запуска ивента? [true/false]")] public bool EnabledTimer { get; set; }
            [JsonProperty(En ? "Duration of the event [sec.]" : "Время проведения ивента [sec.]")] public int FinishTime { get; set; }
            [JsonProperty(En ? "Time before the starting of the event after receiving a chat message [sec.]" : "Время до начала ивента после сообщения в чате [sec.]")] public float PreStartTime { get; set; }
            [JsonProperty(En ? "Notification time until the end of the event [sec.]" : "Время оповещения до окончания ивента [sec.]")] public int PreFinishTime { get; set; }
            [JsonProperty(En ? "Bradley Crates setting" : "Настройка ящиков Bradley")] public CrateConfig BradleyCrates { get; set; }
            [JsonProperty(En ? "Helicopter Crates setting" : "Настройка ящиков вертолета")] public CrateConfig HeliCrates { get; set; }
            [JsonProperty(En ? "Locked Crates setting" : "Настройка заблокированных ящиков")] public HackCrateConfig HackCrates { get; set; }
            [JsonProperty(En ? "Marker configuration on the map" : "Настройка маркера на карте")] public MarkerConfig Marker { get; set; }
            [JsonProperty(En ? "Prefix of chat messages" : "Префикс сообщений в чате")] public string Prefix { get; set; }
            [JsonProperty(En ? "Do you use the chat? [true/false]" : "Использовать ли чат? [true/false]")] public bool IsChat { get; set; }
            [JsonProperty(En ? "GUI setting" : "Настройки GUI")] public GuiConfig Gui { get; set; }
            [JsonProperty(En ? "GUI Announcements setting" : "Настройка GUI Announcements")] public GuiAnnouncementsConfig GuiAnnouncements { get; set; }
            [JsonProperty(En ? "Notify setting" : "Настройка Notify")] public NotifyConfig Notify { get; set; }
            [JsonProperty(En ? "Discord setting (only for users DiscordMessages plugin)" : "Настройка оповещений в Discord (только для тех, кто использует плагин DiscordMessages)")] public DiscordConfig Discord { get; set; }
            [JsonProperty(En ? "Do you create a PVP zone in the event area? (only for users TruePVE plugin) [true/false]" : "Создавать зону PVP в зоне проведения ивента? (только для тех, кто использует плагин TruePVE) [true/false]")] public bool IsCreateZonePvp { get; set; }
            [JsonProperty(En ? "PVE Mode Setting (only for users PveMode plugin)" : "Настройка PVE режима работы плагина (только для тех, кто использует плагин PveMode)")] public PveModeConfig PveMode { get; set; }
            [JsonProperty(En ? "Interrupt the teleport in the event area? (only for users NTeleportation plugin) [true/false]" : "Запрещать телепорт в зоне проведения ивента? (только для тех, кто использует плагин NTeleportation) [true/false]")] public bool NTeleportationInterrupt { get; set; }
            [JsonProperty(En ? "Disable NPCs from the BetterNpc plugin on the monument while the event is on? [true/false]" : "Отключать NPC из плагина BetterNpc на монументе пока проходит ивент? [true/false]")] public bool RemoveBetterNpc { get; set; }
            [JsonProperty(En ? "Settings of all NPC presets when start the event" : "Настройки всех пресетов NPC при запуске ивента")] public HashSet<PresetConfig> PresetsNpc { get; set; }
            [JsonProperty(En ? "NPC settings that guard the truck" : "Настройки NPC, которые охраняют грузовик")] public PresetConfig NpcTruck { get; set; }
            [JsonProperty(En ? "Economy setting (total values will be added up and rewarded at the end of the event)" : "Настройка экономики (конечное значение суммируется и будет выдано игрокам по окончанию ивента)")] public EconomyConfig Economy { get; set; }
            [JsonProperty(En ? "Can SAM Site turrets appear in the event zone? [true/false]" : "Должны ли появляться Sam Site турели в зоне ивента? [true/false]")] public bool IsSamSites { get; set; }
            [JsonProperty(En ? "The number of broken cars in the junkyard when the event starts (no more than 15)" : "Кол-во сломанных машин на свалке, когда начинается ивент (не более 15)")] public int CountBrokenCars { get; set; }
            [JsonProperty(En ? "Plane flight speed multiplier" : "Множитель скорости полета самолета")] public float ScaleSpeedPlane { get; set; }
            [JsonProperty(En ? "Should an additional crane spawn? [true/false]" : "Должен ли появляться дополнительный кран? [true/false]")] public bool AdditionalCrane { get; set; }
            [JsonProperty(En ? "Configuration version" : "Версия конфигурации")] public VersionNumber PluginVersion { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    MinStartTime = 10800f,
                    MaxStartTime = 10800f,
                    EnabledTimer = true,
                    FinishTime = 3600,
                    PreStartTime = 300f,
                    PreFinishTime = 300,
                    BradleyCrates = new CrateConfig
                    {
                        Count = 2,
                        TypeLootTable = 0,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1, Max = 1, UseCount = true,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 50.0f, PrefabDefinition = "assets/prefabs/npc/m2bradley/bradley_crate.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1, Max = 1, UseCount = true,
                            Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 100, MaxAmount = 200, Chance = 50.0f, IsBluePrint = false, SkinID = 0, Name = "" } }
                        }
                    },
                    HeliCrates = new CrateConfig
                    {
                        Count = 2,
                        TypeLootTable = 0,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1, Max = 1, UseCount = true,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 50.0f, PrefabDefinition = "assets/prefabs/npc/patrol helicopter/heli_crate.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1, Max = 1, UseCount = true,
                            Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 100, MaxAmount = 200, Chance = 50.0f, IsBluePrint = false, SkinID = 0, Name = "" } }
                        }
                    },
                    HackCrates = new HackCrateConfig
                    {
                        Count = 1,
                        UnlockTime = 600f,
                        IncreaseEventTime = true,
                        TypeLootTable = 0,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1, Max = 1, UseCount = true,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 50.0f, PrefabDefinition = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1, Max = 1, UseCount = true,
                            Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 100, MaxAmount = 200, Chance = 50.0f, IsBluePrint = false, SkinID = 0, Name = "" } }
                        }
                    },
                    Marker = new MarkerConfig
                    {
                        Name = "JunkyardEvent ({time} sec.)",
                        Radius = 0.4f,
                        Alpha = 0.6f,
                        Color = new ColorConfig { R = 0.81f, G = 0.25f, B = 0.15f }
                    },
                    Prefix = "[JunkyardEvent]",
                    IsChat = true,
                    Gui = new GuiConfig
                    {
                        IsGui = true,
                        AnchorMin = "0 0.9",
                        AnchorMax = "1 0.95"
                    },
                    GuiAnnouncements = new GuiAnnouncementsConfig
                    {
                        IsGuiAnnouncements = false,
                        BannerColor = "Orange",
                        TextColor = "White",
                        ApiAdjustVPosition = 0.03f
                    },
                    Notify = new NotifyConfig
                    {
                        IsNotify = false,
                        Type = "0"
                    },
                    Discord = new DiscordConfig
                    {
                        IsDiscord = false,
                        WebhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                        EmbedColor = 13516583,
                        Keys = new HashSet<string>
                        {
                            "PreStart",
                            "Start",
                            "PreFinish",
                            "Finish",
                            "KillBrokenCar",
                            "KillTruck",
                            "TruckArrived"
                        }
                    },
                    IsCreateZonePvp = false,
                    PveMode = new PveModeConfig
                    {
                        Pve = false,
                        Damage = 500f,
                        ScaleDamage = new HashSet<ScaleDamageConfig>
                        {
                            new ScaleDamageConfig { Type = "NPC", Scale = 1f }
                        },
                        LootCrate = false,
                        HackCrate = false,
                        LootNpc = false,
                        DamageNpc = false,
                        TargetNpc = false,
                        CanEnter = false,
                        CanEnterCooldownPlayer = true,
                        TimeExitOwner = 300,
                        AlertTime = 60,
                        RestoreUponDeath = true,
                        CooldownOwner = 86400,
                        Darkening = 12
                    },
                    NTeleportationInterrupt = false,
                    RemoveBetterNpc = true,
                    PresetsNpc = new HashSet<PresetConfig>
                    {
                        new PresetConfig
                        {
                            Min = 3,
                            Max = 4,
                            Positions = new List<string>
                            {
                                "(50.8, 0.1, 26.4)",
                                "(72.1, 0.1, -27.6)",
                                "(22.2, 0.0, -56.9))",
                                "(-1.0, 0.1, -28.0)",
                                "(22.0, 0.1, -5.4)",
                                "(-10.5, 0.1, 8.0)",
                                "(-42.5, 11.3, 0.6)",
                                "(-31.7, 16.0, -29.2)",
                                "(-45.7, 0.1, 83.2)",
                                "(-64.6, 0.1, 8.8)",
                                "(32.9, 0.1, 71.4)",
                                "(-18.7, 15.0, 36.7)"
                            },
                            Config = new NpcConfig
                            {
                                Name = "Outcast",
                                Health = 100f,
                                RoamRange = 10f,
                                ChaseRange = 50f,
                                AttackRangeMultiplier = 2f,
                                SenseRange = 20f,
                                MemoryDuration = 30f,
                                DamageScale = 0.5f,
                                AimConeScale = 1.8f,
                                CheckVisionCone = false,
                                VisionCone = 135f,
                                Speed = 7.5f,
                                DisableRadio = true,
                                Stationary = false,
                                IsRemoveCorpse = true,
                                WearItems = new HashSet<NpcWear>
                                {
                                    new NpcWear { ShortName = "mask.bandana", SkinID = 1780166642 },
                                    new NpcWear { ShortName = "hoodie", SkinID = 1780158056 },
                                    new NpcWear { ShortName = "burlap.gloves", SkinID = 1362361447 },
                                    new NpcWear { ShortName = "pants", SkinID = 1780161166 }
                                },
                                BeltItems = new HashSet<NpcBelt>
                                {
                                    new NpcBelt { ShortName = "pistol.python", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight" } },
                                    new NpcBelt { ShortName = "syringe.medical", Amount = 10, SkinID = 0, Mods = new HashSet<string>() }
                                },
                                Kit = ""
                            },
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            }
                        },
                        new PresetConfig
                        {
                            Min = 2,
                            Max = 2,
                            Positions = new List<string>
                            {
                                "(50.8, 0.1, 26.4)",
                                "(72.1, 0.1, -27.6)",
                                "(22.2, 0.0, -56.9))",
                                "(-1.0, 0.1, -28.0)",
                                "(22.0, 0.1, -5.4)",
                                "(-10.5, 0.1, 8.0)",
                                "(-42.5, 11.3, 0.6)",
                                "(-31.7, 16.0, -29.2)",
                                "(-45.7, 0.1, 83.2)",
                                "(-64.6, 0.1, 8.8)",
                                "(32.9, 0.1, 71.4)",
                                "(-18.7, 15.0, 36.7)"
                            },
                            Config = new NpcConfig
                            {
                                Name = "Scavenger",
                                Health = 130f,
                                RoamRange = 10f,
                                ChaseRange = 100f,
                                AttackRangeMultiplier = 0.5f,
                                SenseRange = 50f,
                                MemoryDuration = 30f,
                                DamageScale = 5f,
                                AimConeScale = 1f,
                                CheckVisionCone = false,
                                VisionCone = 135f,
                                Speed = 8.5f,
                                DisableRadio = true,
                                Stationary = false,
                                IsRemoveCorpse = true,
                                WearItems = new HashSet<NpcWear>
                                {
                                    new NpcWear { ShortName = "mask.bandana", SkinID = 1780166642 },
                                    new NpcWear { ShortName = "clatter.helmet", SkinID = 0 },
                                    new NpcWear { ShortName = "hoodie", SkinID = 1780158056 },
                                    new NpcWear { ShortName = "burlap.gloves", SkinID = 1362361447 },
                                    new NpcWear { ShortName = "pants", SkinID = 1780161166 }
                                },
                                BeltItems = new HashSet<NpcBelt>
                                {
                                    new NpcBelt { ShortName = "shotgun.pump", Amount = 1, SkinID = 630162685, Mods = new HashSet<string> { "weapon.mod.flashlight" } },
                                    new NpcBelt { ShortName = "syringe.medical", Amount = 10, SkinID = 0, Mods = new HashSet<string>() }
                                },
                                Kit = ""
                            },
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            }
                        },
                        new PresetConfig
                        {
                            Min = 3,
                            Max = 4,
                            Positions = new List<string>
                            {
                                "(50.8, 0.1, 26.4)",
                                "(72.1, 0.1, -27.6)",
                                "(22.2, 0.0, -56.9))",
                                "(-1.0, 0.1, -28.0)",
                                "(22.0, 0.1, -5.4)",
                                "(-10.5, 0.1, 8.0)",
                                "(-42.5, 11.3, 0.6)",
                                "(-31.7, 16.0, -29.2)",
                                "(-45.7, 0.1, 83.2)",
                                "(-64.6, 0.1, 8.8)",
                                "(32.9, 0.1, 71.4)",
                                "(-18.7, 15.0, 36.7)"
                            },
                            Config = new NpcConfig
                            {
                                Name = "Nine Toes",
                                Health = 100f,
                                RoamRange = 10f,
                                ChaseRange = 50f,
                                AttackRangeMultiplier = 2f,
                                SenseRange = 20f,
                                MemoryDuration = 30f,
                                DamageScale = 1f,
                                AimConeScale = 2f,
                                CheckVisionCone = false,
                                VisionCone = 135f,
                                Speed = 7.5f,
                                DisableRadio = true,
                                Stationary = false,
                                IsRemoveCorpse = true,
                                WearItems = new HashSet<NpcWear>
                                {
                                    new NpcWear { ShortName = "mask.bandana", SkinID = 1780166642 },
                                    new NpcWear { ShortName = "hoodie", SkinID = 1780158056 },
                                    new NpcWear { ShortName = "burlap.gloves", SkinID = 1362361447 },
                                    new NpcWear { ShortName = "pants", SkinID = 1780161166 }
                                },
                                BeltItems = new HashSet<NpcBelt>
                                {
                                    new NpcBelt { ShortName = "pistol.semiauto", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight" } },
                                    new NpcBelt { ShortName = "syringe.medical", Amount = 10, SkinID = 0, Mods = new HashSet<string>() }
                                },
                                Kit = ""
                            },
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            }
                        }
                    },
                    NpcTruck = new PresetConfig
                    {
                        Min = 6,
                        Max = 6,
                        Positions = new List<string>
                        {
                            "(3.5, 0.2, -15.9)",
                            "(-2.1, 0.1, -22.8)",
                            "(-7.8, 0.6, -17.2)",
                            "(-11.3, 0.1, -9.7)",
                            "(-4.2, 0.3, -6.4)",
                            "(4.6, 0.1, -7.8)"
                        },
                        Config = new NpcConfig
                        {
                            Name = "Defenders of the faith",
                            Health = 350f,
                            RoamRange = 10f,
                            ChaseRange = 50f,
                            AttackRangeMultiplier = 2f,
                            SenseRange = 50f,
                            MemoryDuration = 30f,
                            DamageScale = 0.5f,
                            AimConeScale = 1f,
                            CheckVisionCone = false,
                            VisionCone = 135f,
                            Speed = 7.5f,
                            DisableRadio = true,
                            Stationary = false,
                            IsRemoveCorpse = true,
                            WearItems = new HashSet<NpcWear>
                            {
                                new NpcWear { ShortName = "mask.bandana", SkinID = 2017569333 },
                                new NpcWear { ShortName = "metal.facemask", SkinID = 1547235630 },
                                new NpcWear { ShortName = "burlap.shirt", SkinID = 2017554105 },
                                new NpcWear { ShortName = "burlap.gloves", SkinID = 0 },
                                new NpcWear { ShortName = "shoes.boots", SkinID = 0 },
                                new NpcWear { ShortName = "burlap.trousers", SkinID = 2017556850 }
                            },
                            BeltItems = new HashSet<NpcBelt>
                            {
                                new NpcBelt { ShortName = "smg.thompson", Amount = 1, SkinID = 2370519330, Mods = new HashSet<string> { "weapon.mod.flashlight" } },
                                new NpcBelt { ShortName = "syringe.medical", Amount = 10, SkinID = 0, Mods = new HashSet<string>() }
                            },
                            Kit = ""
                        },
                        TypeLootTable = 5,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Items = new List<ItemConfig>
                            {
                                new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                            }
                        }
                    },
                    Economy = new EconomyConfig
                    {
                        Plugins = new HashSet<string> { "Economics", "Server Rewards", "IQEconomic" },
                        Min = 0,
                        Crates = new Dictionary<string, double>
                        {
                            ["bradley_crate"] = 0.4,
                            ["heli_crate"] = 0.4
                        },
                        Npc = 0.3,
                        LockedCrate = 0.5,
                        ShredderCar = 0.3,
                        ShredderTruck = 0.5,
                        Commands = new HashSet<string>()
                    },
                    IsSamSites = true,
                    CountBrokenCars = 7,
                    ScaleSpeedPlane = 4f,
                    AdditionalCrane = false,
                    PluginVersion = new VersionNumber()
                };
            }
        }
        #endregion Config

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PreStart"] = "{0} The Rubbish Men <color=#738d43>will arrive</color> at the <color=#55aaff>Junkyard</color> location in <color=#55aaff>{1} sec.</color>!",
                ["Start"] = "{0} The Rubbish Men <color=#ce3f27>have hidden</color> a <color=#55aaff>supply signal grenade</color> inside one of the cars in The <color=#55aaff>Junkyard</color>. We need to <color=#738d43>destroy</color> the <color=#55aaff>cars</color> in The Junkyard shredder to <color=#738d43>locate</color> the <color=#55aaff>supplysignal grenade</color> and activate it!",
                ["PreFinish"] = "{0} Junkyard Event <color=#ce3f27>will end</color> in <color=#55aaff>{1} sec.</color>!",
                ["Finish"] = "{0} The Junkyard Event <color=#ce3f27>has concluded</color>!",
                ["KillBrokenCar"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>has destroyed</color> a <color=#55aaff>car</color> in the shredder with a <color=#55aaff>supply signal grenade</color> inside. A <color=#55aaff>truck</color> with loot <color=#738d43>will arrive</color> in a short time. Be careful! There are <color=#55aaff>mercenaries</color> inside the truck. They <color=#ce3f27>will try to stop</color> you from getting the loot!",
                ["PlayerKillBrokenCar"] = "{0} You <color=#738d43>have destroyed</color> a <color=#55aaff>car</color> in the shredder, but there was <color=#ce3f27>no</color> <color=#55aaff>supply signal grenade</color> in this car. Continue looking for the right car...",
                ["TruckArrived"] = "{0} <color=#55aaff>Loot Truck</color> <color=#738d43>has arrived</color> in The <color=#55aaff>Junkyard</color>. It is guarded by mercenaries. But the <color=#55aaff>loot truck</color> <color=#ce3f27>is locked</color>! If you want to get access to the crates you need to <color=#738d43>destroy</color> the <color=#55aaff>loot truck</color> using The Junkyard shredder",
                ["KillTruck"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>destroyed</color> the <color=#55aaff>loot truck</color> in The Junkyard shredder. Access to the loot <color=#738d43>is all unlocked</color>!",
                ["EventActive"] = "{0} This event is active now. To finish this event (<color=#55aaff>/jstop</color>), then (<color=#55aaff>/jstart</color>) to start the next one!",
                ["GUI"] = "The Junkyard Event will finish in {0} sec.",
                ["EnterPVP"] = "{0} You <color=#ce3f27>have entered</color> the PVP zone, now other players <color=#ce3f27>can damage</color> you!",
                ["ExitPVP"] = "{0} You <color=#738d43>have left</color> the PVP zone, now other players <color=#738d43>cannot damage</color> you!",
                ["NTeleportation"] = "{0} You <color=#ce3f27>cannot</color> teleport into the event zone!",
                ["SendEconomy"] = "{0} You <color=#738d43>have earned</color> <color=#55aaff>{1}</color> points in economics for participating in the event"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PreStart"] = "{0} Через <color=#55aaff>{1} сек.</color> мусорщики <color=#738d43>прибудут</color> в локацию <color=#55aaff>Свалка</color>!",
                ["Start"] = "{0} Мусорщики <color=#ce3f27>спрятали</color> на <color=#55aaff>Свалке</color> в одной из сломанных машин <color=#55aaff>сигнальную гранату</color> для вызова грузовика с припасами. Вам необходимо <color=#738d43>уничтожить</color> <color=#55aaff>машину</color> в шредере, чтобы <color=#738d43>найти</color> <color=#55aaff>сигнальную гранату</color> и привести её в исполнение",
                ["PreFinish"] = "{0} Ивент на свалке <color=#ce3f27>закончится</color> через <color=#55aaff>{1} сек.</color>!",
                ["Finish"] = "{0} Ивент на свалке <color=#ce3f27>закончен</color>!",
                ["KillBrokenCar"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>уничтожил</color> в шредере <color=#55aaff>машину</color>, внутри которой находилась <color=#55aaff>сигнальная граната</color>. В ближайшее время <color=#738d43>будет доставлен</color> <color=#55aaff>грузовик</color> с припасами. Будьте осторожны, внутри грузовика находятся <color=#55aaff>наемники</color>, которые <color=#ce3f27>помешают</color> вам заполучить припасы",
                ["PlayerKillBrokenCar"] = "{0} Вы <color=#738d43>уничтожили</color> <color=#55aaff>машину</color> в шредере, но в данной машине <color=#ce3f27>не было</color> <color=#55aaff>сигнальной гранаты</color>. Продолжайте искать необходимую машину...",
                ["TruckArrived"] = "{0} <color=#55aaff>Грузовик</color> <color=#738d43>доставлен</color> в локацию <color=#55aaff>Свалка</color>, его охраняют наемники. Но <color=#55aaff>грузовик</color> <color=#ce3f27>закрыт</color>, чтобы получить доступ к ящикам вам необходимо <color=#738d43>уничтожить</color> <color=#55aaff>грузовик</color> через шредер",
                ["KillTruck"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>уничтожил</color> в шредере <color=#55aaff>грузовик</color>. Доступ к припасам <color=#738d43>открыт</color>!",
                ["EventActive"] = "{0} Ивент в данный момент активен, сначала завершите текущий ивент (<color=#55aaff>/jstop</color>), чтобы начать следующий!",
                ["GUI"] = "Ивент на свалке закончится через {0} сек.",
                ["EnterPVP"] = "{0} Вы <color=#ce3f27>вошли</color> в PVP зону, теперь другие игроки <color=#ce3f27>могут</color> наносить вам урон!",
                ["ExitPVP"] = "{0} Вы <color=#738d43>вышли</color> из PVP зоны, теперь другие игроки <color=#738d43>не могут</color> наносить вам урон!",
                ["NTeleportation"] = "{0} Вы <color=#ce3f27>не можете</color> телепортироваться в зоне ивента!",
                ["SendEconomy"] = "{0} Вы <color=#738d43>получили</color> <color=#55aaff>{1}</color> баллов в экономику за прохождение ивента"
            }, this, "ru");
        }

        private string GetMessage(string langKey, string userID) => lang.GetMessage(langKey, _ins, userID);

        private string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
        #endregion Lang

        #region Oxide Hooks
        private static JunkyardEvent _ins;

        private void Init()
        {
            _ins = this;
            Unsubscribes();
        }

        private void OnServerInitialized()
        {
            LoadDefaultMessages();
            CheckAllLootTables();
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments) if (monument.displayPhrase.english == "Junkyard") StartLocations.Add(new Location { pos = monument.transform.position, rot = monument.transform.rotation.eulerAngles });
            if (StartLocations.Count == 0)
            {
                PrintError("The Junkyard location is missing on the map. The plugin cannot be loaded!");
                NextTick(() => Server.Command($"o.unload {Name}"));
                return;
            }
            if (_config.EnabledTimer)
            {
                timer.In(UnityEngine.Random.Range(_config.MinStartTime, _config.MaxStartTime), () =>
                {
                    if (!_active) Start();
                    else Puts("This event is active now. To finish this event (jstop), then to start the next one");
                });
            }
        }

        private void Unload()
        {
            if (_active) Finish();
            _ins = null;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            if (entity == _controller.Truck || entity == _controller.Module) return true;
            return null;
        }

        private object CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            if (player == null || entity == null) return null;
            BaseEntity parentEntity = entity.GetParentEntity();
            if (parentEntity == null) return null;
            if (parentEntity == _controller.Module) return true;
            if (parentEntity is MagnetCrane && _controller.Players.Contains(player))
            {
                if (_config.PveMode.Pve && plugins.Exists("PveMode") && PveMode.Call("CanActionEvent", Name, player) != null) return true;
                if (!_controller.PlayersInCrane.Contains(player)) _controller.AddPlayerInCrane(player);
            }
            return null;
        }

        private object CanDismountEntity(BasePlayer player, BaseMountable entity)
        {
            if (player == null || entity == null) return null;
            MagnetCrane crane = entity.GetParentEntity() as MagnetCrane;
            if (crane != null && _controller.PlayersInCrane.Contains(player)) _controller.RemovePlayerInCrane(player);
            return null;
        }

        private void OnEntitySpawned(MagnetCrane crane)
        {
            if (crane == null || _controller == null) return;
            if (Vector3.Distance(crane.transform.position, _controller.transform.position) < Radius)
            {
                int count = _controller.Cranes.Count;
                if (count == 2) NextTick(() => crane.Kill());
                else if (count == 1)
                {
                    if (_config.AdditionalCrane) _controller.Cranes.Add(crane);
                    else NextTick(() => crane.Kill());
                }
                else if (count == 0) _controller.Cranes.Add(crane);
            }
        }

        private void OnEntityKill(MagnetCrane crane)
        {
            if (crane == null || _controller == null) return;
            if (_controller.Cranes.Contains(crane))
            {
                _controller.Cranes.Remove(crane);
                timer.In(1f, () => _controller.KillCrane());
            }
        }

        private void OnEntityEnter(LargeShredderTrigger trigger, BaseCombatEntity entity)
        {
            if (trigger == null || entity == null) return;
            if (trigger == _controller.Shredder.trigger) _controller.CheckCar(entity.net.ID);
        }

        private void OnCargoPlaneSignaled(CargoPlane cargoPlane, SupplySignal supplySignal)
        {
            if (cargoPlane == null || supplySignal == null) return;
            if (supplySignal == _controller.Supply)
            {
                _controller.Plane = cargoPlane;
                cargoPlane.secondsToTake *= 1f / _config.ScaleSpeedPlane;
                Unsubscribe("OnCargoPlaneSignaled");
            }
        }

        private void OnSupplyDropDropped(SupplyDrop supplyDrop, CargoPlane cargoPlane)
        {
            if (supplyDrop == null || cargoPlane == null) return;
            if (cargoPlane == _controller.Plane)
            {
                _controller.SpawnTruck(supplyDrop.transform.position);
                if (supplyDrop.IsExists()) supplyDrop.Kill();
                Unsubscribe("OnSupplyDropDropped");
            }
        }

        private object OnVehiclePush(ModularCar vehicle, BasePlayer player)
        {
            if (vehicle != null && vehicle == _controller.Truck) return true;
            else return null;
        }

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (player != null && _controller.Players.Contains(player))
            {
                _controller.Players.Remove(player);
                if (_controller.PlayersInCrane.Contains(player)) _controller.PlayersInCrane.Remove(player);
                if (_config.Gui.IsGui) CuiHelper.DestroyUi(player, "TextMain");
            }
        }

        private void OnEntityDeath(ScientistNPC npc, HitInfo info)
        {
            if (npc == null || info == null) return;
            BasePlayer attacker = info.InitiatorPlayer;
            if (_controller.Scientists.Contains(npc) && attacker.IsPlayer()) ActionEconomy(attacker.userID, "Npc");
        }

        private readonly Dictionary<uint, ulong> _startHackCrates = new Dictionary<uint, ulong>();

        private object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (player == null || crate == null) return null;
            if (_controller.HackCrates.Contains(crate))
            {
                if (_startHackCrates.ContainsKey(crate.net.ID)) _startHackCrates[crate.net.ID] = player.userID;
                else _startHackCrates.Add(crate.net.ID, player.userID);
            }
            return null;
        }

        private void OnCrateHack(HackableLockedCrate crate)
        {
            if (crate == null) return;
            uint crateId = crate.net.ID;
            ulong playerId;
            if (_startHackCrates.TryGetValue(crateId, out playerId))
            {
                _startHackCrates.Remove(crateId);
                if (_config.HackCrates.IncreaseEventTime && _controller.TimeToFinish < (int)_config.HackCrates.UnlockTime) _controller.TimeToFinish += (int)_config.HackCrates.UnlockTime;
                ActionEconomy(playerId, "LockedCrate");
            }
        }

        private readonly HashSet<uint> _lootableCrates = new HashSet<uint>();

        private void OnLootEntity(BasePlayer player, LootContainer container)
        {
            if (player == null || container == null || _lootableCrates.Contains(container.net.ID)) return;
            if (_controller.HeliCrates.Contains(container) || _controller.BradleyCrates.Contains(container))
            {
                _lootableCrates.Add(container.net.ID);
                ActionEconomy(player.userID, "Crates", container.ShortPrefabName);
            }
        }
        #endregion Oxide Hooks

        #region Controller
        private ControllerJunkyardEvent _controller;
        private bool _active = false;

        private void Start()
        {
            if (!plugins.Exists("NpcSpawn"))
            {
                PrintError("NpcSpawn plugin doesn`t exist! Please read the file ReadMe.txt");
                Server.Command($"o.unload {Name}");
                return;
            }
            _active = true;
            AlertToAllPlayers("PreStart", _config.Prefix, _config.PreStartTime);
            timer.In(_config.PreStartTime, () =>
            {
                Interface.Oxide.CallHook("OnJunkyardEventStart");
                Subscribes();
                _controller = new GameObject().AddComponent<ControllerJunkyardEvent>();
                if (_config.PveMode.Pve && plugins.Exists("PveMode"))
                {
                    JObject config = new JObject
                    {
                        ["Damage"] = _config.PveMode.Damage,
                        ["ScaleDamage"] = new JArray { _config.PveMode.ScaleDamage.Select(x => new JObject { ["Type"] = x.Type, ["Scale"] = x.Scale }) },
                        ["LootCrate"] = _config.PveMode.LootCrate,
                        ["HackCrate"] = _config.PveMode.HackCrate,
                        ["LootNpc"] = _config.PveMode.LootNpc,
                        ["DamageNpc"] = _config.PveMode.DamageNpc,
                        ["DamageTank"] = false,
                        ["TargetNpc"] = _config.PveMode.TargetNpc,
                        ["TargetTank"] = false,
                        ["CanEnter"] = _config.PveMode.CanEnter,
                        ["CanEnterCooldownPlayer"] = _ins._config.PveMode.CanEnterCooldownPlayer,
                        ["TimeExitOwner"] = _config.PveMode.TimeExitOwner,
                        ["AlertTime"] = _config.PveMode.AlertTime,
                        ["RestoreUponDeath"] = _config.PveMode.RestoreUponDeath,
                        ["CooldownOwner"] = _config.PveMode.CooldownOwner,
                        ["Darkening"] = _config.PveMode.Darkening
                    };
                    PveMode.Call("EventAddPveMode", Name, config, _controller.transform.position, Radius, new HashSet<uint>(), _controller.Scientists.Select(x => x.net.ID), new HashSet<uint>(), new HashSet<ulong>(), null);
                }
                if (_config.RemoveBetterNpc && plugins.Exists("BetterNpc")) BetterNpc.Call("DestroyController", "Junkyard");
                AlertToAllPlayers("Start", _config.Prefix);
            });
        }

        private void Finish()
        {
            Unsubscribes();
            if (_config.RemoveBetterNpc && plugins.Exists("BetterNpc")) BetterNpc.Call("CreateController", "Junkyard");
            if (_config.PveMode.Pve && plugins.Exists("PveMode")) PveMode.Call("EventRemovePveMode", Name, true);
            if (_controller != null) UnityEngine.Object.Destroy(_controller.gameObject);
            _active = false;
            SendBalance();
            AlertToAllPlayers("Finish", _config.Prefix);
            Interface.Oxide.CallHook("OnJunkyardEventEnd");
            NextTick(() => Server.Command($"o.reload {Name}"));
        }

        internal HashSet<string> TrashList = new HashSet<string>
        {
            "minicopter.entity",
            "scraptransporthelicopter",
            "hotairballoon",
            "rowboat",
            "rhib",
            "submarinesolo.entity",
            "submarineduo.entity",
            "sled.deployed",
            "magnetcrane.entity",
            "sedantest.entity",
            "2module_car_spawned.entity",
            "3module_car_spawned.entity",
            "4module_car_spawned.entity",
            "wolf",
            "chicken",
            "boar",
            "stag",
            "bear",
            "testridablehorse",
            "servergibs_bradley",
            "servergibs_patrolhelicopter"
        };

        internal class ControllerJunkyardEvent : FacepunchBehaviour
        {
            private MapMarkerGenericRadius _mapmarker;
            private VendingMachineMapMarker _vendingMarker;
            private SphereCollider _sphereCollider;

            internal LargeShredder Shredder;

            private Vector3 _landingTruckPos;
            internal SupplySignal Supply;
            internal CargoPlane Plane;
            internal ModularCar Truck;
            internal VehicleModuleCamper Module;
            private BaseEntity _parachute;

            internal HashSet<MagnetCrane> Cranes = new HashSet<MagnetCrane>();
            internal Vector3 _mainCranePos;
            internal Vector3 _mainCraneRot;
            internal Vector3 _addCranePos;
            internal Vector3 _addCraneRot;

            private Coroutine _spawnBrokenCarsCoroutine = null;
            private readonly List<Vector3> _carPositionsLocal = new List<Vector3>
            {
                new Vector3(17.056f, 0.125f, 17.901f),
                new Vector3(35.747f, 0.059f, 14.970f),
                new Vector3(55.908f, 0.125f, 11.471f),
                new Vector3(71.476f, 0.125f, -27.498f),
                new Vector3(59.288f, 0.104f, -40.456f),
                new Vector3(31.557f, 0.050f, -62.765f),
                new Vector3(-2.163f, 0.125f, -52.034f),
                new Vector3(31.694f, 0.117f, 57.948f),
                new Vector3(5.459f, 0.109f, 75.459f),
                new Vector3(-18.921f, 0.108f, 79.526f),
                new Vector3(-48.276f, 0.136f, 79.298f),
                new Vector3(-68.626f, 0.077f, 35.397f),
                new Vector3(-69.282f, 0.125f, 8.979f),
                new Vector3(-73.574f, 0.111f, -12.416f),
                new Vector3(-20.111f, 0.088f, 1.391f)
            };
            private List<Vector3> _carPositionsGlobal = new List<Vector3>();
            internal List<BaseCombatEntity> BrokenCars = new List<BaseCombatEntity>();
            internal uint BrokenCarId;
            private uint _truckId;
            private HashSet<uint> _checkedCars = new HashSet<uint>();

            private readonly List<PointAnimationTransform> _railPointsLocal = new List<PointAnimationTransform>
            {
                new PointAnimationTransform { Time = 0.25f, Pos = new Vector3(-3.429f, 1.473f, 0f), Rot = new Vector3(0f, 270f, 0f) },
                new PointAnimationTransform { Time = 0.25f, Pos = new Vector3(-4.650f, 1.473f, 0f), Rot = new Vector3(0f, 270f, 0f) },
                new PointAnimationTransform { Time = 0.25f, Pos = new Vector3(-5.867f, 1.473f, 0f), Rot = new Vector3(0f, 270f, 0f) },
                new PointAnimationTransform { Time = 0.25f, Pos = new Vector3(-6.906f, 1.571f, 0f), Rot = new Vector3(352.399f, 270f, 0f) },
                new PointAnimationTransform { Time = 0.25f, Pos = new Vector3(-7.860f, 1.871f, 0f), Rot = new Vector3(335.144f, 270f, 0f) },
                new PointAnimationTransform { Time = 0.25f, Pos = new Vector3(-8.931f, 2.421f, 0f), Rot = new Vector3(330.605f, 270f, 0f) },
                new PointAnimationTransform { Time = 0.25f, Pos = new Vector3(-9.912f, 2.973f, 0f), Rot = new Vector3(330.605f, 270f, 0f) },
                new PointAnimationTransform { Time = 0.25f, Pos = new Vector3(-10.891f, 3.525f, 0f), Rot = new Vector3(330.605f, 270f, 0f) },
                new PointAnimationTransform { Time = 0.25f, Pos = new Vector3(-11.864f, 4.063f, 0f), Rot = new Vector3(334.411f, 270f, 0f) },
                new PointAnimationTransform { Time = 0.25f, Pos = new Vector3(-12.989f, 4.318f, 0f), Rot = new Vector3(355.833f, 270f, 0f) },
                new PointAnimationTransform { Time = 0.25f, Pos = new Vector3(-14.047f, 4.351f, 0f), Rot = new Vector3(0f, 270f, 0f) },
                new PointAnimationTransform { Time = 0.25f, Pos = new Vector3(-15.199f, 4.329f, 0f), Rot = new Vector3(2.748f, 270f, 0f) },
                new PointAnimationTransform { Time = 0.25f, Pos = new Vector3(-16.323f, 4.231f, 0f), Rot = new Vector3(6.429f, 270f, 0f) },
                new PointAnimationTransform { Time = 0.5f, Pos = new Vector3(-19.302f, 3.895f, 0f), Rot = new Vector3(6.429f, 270f, 0f) }
            };
            private List<PointAnimationTransform> _railPointsGlobal = new List<PointAnimationTransform>();

            private Coroutine _spawnCratesCoroutine = null;
            private HashSet<DroppedItemContainer> _backpackCrates = new HashSet<DroppedItemContainer>();
            internal HashSet<LootContainer> HeliCrates = new HashSet<LootContainer>();
            internal HashSet<LootContainer> BradleyCrates = new HashSet<LootContainer>();
            internal HashSet<HackableLockedCrate> HackCrates = new HashSet<HackableLockedCrate>();

            internal int TimeToFinish;
            internal HashSet<BasePlayer> Players = new HashSet<BasePlayer>();
            internal List<BasePlayer> PlayersInCrane = new List<BasePlayer>();

            private Dictionary<Vector3, Vector3> SamSitePositions = new Dictionary<Vector3, Vector3>
            {
                [new Vector3(-24.295f, 18.782f, -28.644f)] = new Vector3(0f, 90f, 0f),
                [new Vector3(-41.871f, 17.303f, 9.515f)] = new Vector3(0f, 284.807f, 0f),
                [new Vector3(-20.451f, 20.874f, 38.877f)] = new Vector3(0f, 0f, 0f)
            };
            private HashSet<SamSite> SamSites = new HashSet<SamSite>();

            internal HashSet<ScientistNPC> Scientists = new HashSet<ScientistNPC>();

            private void Awake()
            {
                Location location = _ins.StartLocations.GetRandom();
                transform.position = location.pos;
                transform.rotation = Quaternion.Euler(location.rot);

                SpawnMapMarker();

                gameObject.layer = 3;
                _sphereCollider = gameObject.AddComponent<SphereCollider>();
                _sphereCollider.isTrigger = true;
                _sphereCollider.radius = _ins.Radius;

                Vector3 pos; Quaternion rot;
                GetGlobal(transform, new Vector3(1.482f, 0.118f, 13.213f), new Vector3(0f, 71.964f, 0f), out pos, out rot);
                _mainCranePos = pos;
                _mainCraneRot = rot.eulerAngles;
                GetGlobal(transform, new Vector3(8.627f, 0.118f, -8.731f), new Vector3(0f, 71.964f, 0f), out pos, out rot);
                _addCranePos = pos;
                _addCraneRot = rot.eulerAngles;

                Cranes = GetEntities<MagnetCrane>(transform.position, _ins.Radius, -1);
                foreach (MagnetCrane crane in Cranes) if (crane.IsExists()) crane.Kill();
                Cranes.Clear();
                SpawnCrane(_mainCranePos, _mainCraneRot);
                if (_ins._config.AdditionalCrane) SpawnCrane(_addCranePos, _addCraneRot);

                FindShredder();
                _railPointsGlobal = _railPointsLocal.Select(s => new PointAnimationTransform
                {
                    Time = s.Time,
                    Pos = Shredder.transform.TransformPoint(s.Pos),
                    Rot = (Shredder.transform.rotation * Quaternion.Euler(s.Rot)).eulerAngles
                });

                _carPositionsGlobal = _carPositionsLocal.Select(GetGlobalPosition);
                FindAllBrokenCars();

                _landingTruckPos = GetGlobalPosition(new Vector3(-2.897f, 0.125f, -13.176f));

                if (_ins._config.IsSamSites) SpawnSamSites();

                foreach (PresetConfig preset in _ins._config.PresetsNpc) SpawnPreset(preset);

                TimeToFinish = _ins._config.FinishTime;
                InvokeRepeating(ChangeToFinishTime, 1f, 1f);
            }

            private void OnDestroy()
            {
                CancelInvoke(UpdateMarkerForPlayerInCrane);
                foreach (MagnetCrane crane in Cranes) if (crane.IsExists()) crane.Kill();

                CancelInvoke(UpdateMapMarker);
                if (_mapmarker.IsExists()) _mapmarker.Kill();
                if (_vendingMarker.IsExists()) _vendingMarker.Kill();

                if (_spawnBrokenCarsCoroutine != null) ServerMgr.Instance.StopCoroutine(_spawnBrokenCarsCoroutine);
                foreach (BaseCombatEntity car in BrokenCars) if (car.IsExists()) car.Kill();
                
                if (Truck.IsExists()) Truck.Kill();
                if (_parachute.IsExists()) _parachute.Kill();
                CancelInvoke(UpdateTruck);

                if (_spawnCratesCoroutine != null) ServerMgr.Instance.StopCoroutine(_spawnCratesCoroutine);
                foreach (DroppedItemContainer backpack in _backpackCrates) if (backpack.IsExists()) backpack.Kill();
                foreach (LootContainer crate in HeliCrates) if (crate.IsExists()) crate.Kill();
                foreach (LootContainer crate in BradleyCrates) if (crate.IsExists()) crate.Kill();
                foreach (HackableLockedCrate crate in HackCrates) if (crate.IsExists()) crate.Kill();

                foreach (SamSite samSite in SamSites) if (samSite.IsExists()) samSite.Kill();

                foreach (ScientistNPC npc in Scientists) if (npc.IsExists()) npc.Kill();

                CancelInvoke(ChangeToFinishTime);
                foreach (BasePlayer player in Players) CuiHelper.DestroyUi(player, "TextMain");
            }

            private void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player.IsPlayer())
                {
                    Players.Add(player);
                    if (_ins._config.Gui.IsGui) _ins.MessageGUI(player, _ins.GetMessage("GUI", player.UserIDString, TimeToFinish));
                    if (_ins._config.IsCreateZonePvp) _ins.AlertToPlayer(player, _ins.GetMessage("EnterPVP", player.UserIDString, _ins._config.Prefix));
                }
            }

            private void OnTriggerExit(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player.IsPlayer())
                {
                    Players.Remove(player);
                    if (_ins._config.Gui.IsGui) CuiHelper.DestroyUi(player, "TextMain");
                    if (_ins._config.IsCreateZonePvp) _ins.AlertToPlayer(player, _ins.GetMessage("ExitPVP", player.UserIDString, _ins._config.Prefix));
                    return;
                }
                BaseCombatEntity entity = other.GetComponentInParent<BaseCombatEntity>();
                if (entity.IsExists() && entity.ShortPrefabName == "shreddable_pickuptruck" && BrokenCars.Contains(entity) && Vector3.Distance(transform.position, entity.transform.position) > 25f)
                {
                    BrokenCars.Remove(entity);
                    BaseCombatEntity car = GameManager.server.CreateEntity("assets/content/vehicles/crane_magnet/shreddable_pickuptruck.prefab", GetSpawnPosBrokenCar()) as BaseCombatEntity;
                    car.enableSaving = false;
                    car.Spawn();
                    BrokenCars.Add(car);
                    if (entity.net.ID == BrokenCarId) BrokenCarId = BrokenCars.GetRandom().net.ID;
                }
            }

            private void ChangeToFinishTime()
            {
                TimeToFinish--;
                if (_ins._config.Gui.IsGui) foreach (BasePlayer player in Players) _ins.MessageGUI(player, _ins.GetMessage("GUI", player.UserIDString, TimeToFinish));
                if (TimeToFinish == _ins._config.PreFinishTime) _ins.AlertToAllPlayers("PreFinish", _ins._config.Prefix, _ins._config.PreFinishTime);
                else if (TimeToFinish == 0)
                {
                    CancelInvoke(ChangeToFinishTime);
                    _ins.Finish();
                }
            }

            private void SpawnMapMarker()
            {
                _mapmarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", transform.position) as MapMarkerGenericRadius;
                _mapmarker.Spawn();
                _mapmarker.radius = _ins._config.Marker.Radius;
                _mapmarker.alpha = _ins._config.Marker.Alpha;
                _mapmarker.color1 = new Color(_ins._config.Marker.Color.R, _ins._config.Marker.Color.G, _ins._config.Marker.Color.B);

                _vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", transform.position) as VendingMachineMapMarker;
                _vendingMarker.markerShopName = _ins._config.Marker.Name.Replace("{time}", $"{TimeToFinish}");
                _vendingMarker.Spawn();

                InvokeRepeating(UpdateMapMarker, 0, 1f);
            }

            private void UpdateMapMarker()
            {
                _mapmarker.SendUpdate();
                _vendingMarker.markerShopName = _ins._config.Marker.Name.Replace("{time}", $"{TimeToFinish}");
                _vendingMarker.SendNetworkUpdate();
            }

            private static void GetGlobal(Transform Transform, Vector3 localPosition, Vector3 localRotation, out Vector3 globalPosition, out Quaternion globalRotation)
            {
                globalPosition = Transform.TransformPoint(localPosition);
                globalRotation = Transform.rotation * Quaternion.Euler(localRotation);
            }

            private Vector3 GetGlobalPosition(Vector3 localPosition) => transform.TransformPoint(localPosition);

            private void FindShredder()
            {
                foreach (Collider collider in Physics.OverlapSphere(transform.position, 25f))
                {
                    Shredder = collider.ToBaseEntity() as LargeShredder;
                    if (Shredder.IsExists()) return;
                }
                Shredder = null;
            }

            private void FindAllBrokenCars()
            {
                foreach (BaseCombatEntity entity in GetEntities<BaseCombatEntity>(transform.position, _ins.Radius, 1 << 15)) if (entity.ShortPrefabName == "shreddable_pickuptruck") BrokenCars.Add(entity);
                int count = _ins._config.CountBrokenCars - BrokenCars.Count;
                if (count < 0)
                {
                    while (BrokenCars.Count > _ins._config.CountBrokenCars)
                    {
                        BaseCombatEntity car = BrokenCars.GetRandom();
                        BrokenCars.Remove(car);
                        if (car.IsExists()) car.Kill();
                    }
                    BrokenCarId = BrokenCars.GetRandom().net.ID;
                }
                else if (count == 0) BrokenCarId = BrokenCars.GetRandom().net.ID;
                else _spawnBrokenCarsCoroutine = ServerMgr.Instance.StartCoroutine(SpawnBrokenCars(count));
            }

            internal IEnumerator SpawnBrokenCars(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    BaseCombatEntity car = GameManager.server.CreateEntity("assets/content/vehicles/crane_magnet/shreddable_pickuptruck.prefab", GetSpawnPosBrokenCar()) as BaseCombatEntity;
                    car.enableSaving = false;
                    car.Spawn();
                    BrokenCars.Add(car);
                    yield return CoroutineEx.waitForSeconds(0.5f);
                }
                BrokenCarId = BrokenCars.GetRandom().net.ID;
            }

            private Vector3 GetSpawnPosBrokenCar()
            {
                foreach (Vector3 pos in _carPositionsGlobal) if (IsValidPlaceSpawnBrokenCar(pos)) return pos;
                return Vector3.zero;
            }

            private bool IsValidPlaceSpawnBrokenCar(Vector3 pos)
            {
                foreach (BaseEntity entity in GetEntities<BaseEntity>(pos, 4f, 1 << 15 | 1 << 17)) if (entity.ShortPrefabName == "shreddable_pickuptruck" || entity is BasePlayer) return false;
                return true;
            }

            internal void CheckCar(uint id)
            {
                if (_checkedCars.Contains(id)) return;
                else _checkedCars.Add(id);
                BasePlayer player = GetPlayerCrane();
                if (id == BrokenCarId)
                {
                    if (player != null) _ins.ActionEconomy(player.userID, "ShredderCar");
                    _ins.AlertToAllPlayers("KillBrokenCar", _ins._config.Prefix, player != null ? player.displayName : "Player");
                    BrokenCarId = 0;
                    BrokenCars.Clear();
                    SpawnEntityInConveyor("assets/prefabs/tools/supply signal/grenade.supplysignal.deployed.prefab");
                }
                else if (id == _truckId)
                {
                    if (player != null) _ins.ActionEconomy(player.userID, "ShredderTruck");
                    _ins.AlertToAllPlayers("KillTruck", _ins._config.Prefix, player != null ? player.displayName : "Player");
                    _truckId = 0;
                    _spawnCratesCoroutine = ServerMgr.Instance.StartCoroutine(SpawnCrates());
                    TimeToFinish = _ins._config.PreFinishTime;
                }
                else if (BrokenCarId != 0 && player != null) _ins.AlertToPlayer(player, _ins.GetMessage("PlayerKillBrokenCar", player.UserIDString, _ins._config.Prefix));
            }

            private BasePlayer GetPlayerCrane()
            {
                if (PlayersInCrane.Count == 0) return null;
                if (PlayersInCrane.Count == 1) return PlayersInCrane.First();
                if (PlayersInCrane.Count == 2) return PlayersInCrane.Min(x => Vector3.Distance(x.transform.position, Shredder.transform.position));
                return null;
            }

            private void SpawnEntityInConveyor(string name)
            {
                BaseEntity entity = GameManager.server.CreateEntity(name, _railPointsGlobal[0].Pos, Quaternion.Euler(_railPointsGlobal[0].Rot));
                entity.enableSaving = false;
                entity.Spawn();

                if (entity is SupplySignal) Supply = entity as SupplySignal;
                else if (entity is HackableLockedCrate)
                {
                    HackableLockedCrate hackCrate = entity as HackableLockedCrate;
                    hackCrate.hackSeconds = HackableLockedCrate.requiredHackSeconds - _ins._config.HackCrates.UnlockTime;
                    HackCrates.Add(hackCrate);
                    HackCrateConfig config = _ins._config.HackCrates;
                    if (config.TypeLootTable == 1 || config.TypeLootTable == 4 || config.TypeLootTable == 5)
                    {
                        _ins.NextTick(() =>
                        {
                            hackCrate.inventory.ClearItemsContainer();
                            if (config.TypeLootTable == 4 || config.TypeLootTable == 5) _ins.AddToContainerPrefab(hackCrate.inventory, config.PrefabLootTable);
                            if (config.TypeLootTable == 1 || config.TypeLootTable == 5) _ins.AddToContainerItem(hackCrate.inventory, config.OwnLootTable);
                        });
                    }
                }
                else if (entity is LockedByEntCrate)
                {
                    if (entity.ShortPrefabName == "heli_crate") HeliCrates.Add(entity as LootContainer);
                    else BradleyCrates.Add(entity as LootContainer);
                    entity.SetFlag(BaseEntity.Flags.Locked, true);
                }

                AnimationTransform animation = entity.gameObject.AddComponent<AnimationTransform>();
                animation.AddPath(_railPointsGlobal.Skip(1));
            }

            private IEnumerator SpawnCrates()
            {
                for (int i = 0; i < _ins._config.HeliCrates.Count; i++)
                {
                    SpawnEntityInConveyor("assets/prefabs/npc/patrol helicopter/heli_crate.prefab");
                    yield return CoroutineEx.waitForSeconds(1f);
                }
                for (int i = 0; i < _ins._config.BradleyCrates.Count; i++)
                {
                    SpawnEntityInConveyor("assets/prefabs/npc/m2bradley/bradley_crate.prefab");
                    yield return CoroutineEx.waitForSeconds(1f);
                }
                for (int i = 0; i < _ins._config.HackCrates.Count; i++)
                {
                    SpawnEntityInConveyor("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab");
                    yield return CoroutineEx.waitForSeconds(1f);
                }
                yield return CoroutineEx.waitForSeconds(4f);
                if (_ins._config.PveMode.Pve && _ins.plugins.Exists("PveMode"))
                {
                    HashSet<uint> crates = new HashSet<uint>();
                    foreach (LootContainer crate in HeliCrates) crates.Add(crate.net.ID);
                    foreach (LootContainer crate in BradleyCrates) crates.Add(crate.net.ID);
                    foreach (HackableLockedCrate crate in HackCrates) crates.Add(crate.net.ID);
                    _ins.PveMode.Call("EventAddCrates", _ins.Name, crates);
                }
            }

            internal void AddRigidBody(LockedByEntCrate ent)
            {
                DroppedItemContainer backpack = GameManager.server.CreateEntity("assets/prefabs/misc/item drop/item_drop_backpack.prefab", ent.transform.position, ent.transform.rotation) as DroppedItemContainer;
                backpack.enableSaving = false;
                backpack.Spawn();
                backpack.CancelInvoke(backpack.RemoveMe);
                _backpackCrates.Add(backpack);

                LockedByEntCrate entity = GameManager.server.CreateEntity(ent.PrefabName) as LockedByEntCrate;
                entity.enableSaving = false;
                entity.SetParent(backpack);

                if (ent.IsExists())
                {
                    if (ent.ShortPrefabName == "heli_crate") HeliCrates.Remove(ent);
                    else BradleyCrates.Remove(ent);
                    ent.Kill();
                }

                entity.Spawn();
                if (entity.ShortPrefabName == "heli_crate") HeliCrates.Add(entity);
                else BradleyCrates.Add(entity);
                entity.SetFlag(BaseEntity.Flags.Locked, true);

                backpack.GetComponent<Rigidbody>().AddForce(backpack.transform.forward * 100f, ForceMode.Force);

                Invoke(() =>
                {
                    if (!backpack.IsExists() || !entity.IsExists()) return;

                    LockedByEntCrate crate = GameManager.server.CreateEntity(entity.PrefabName, entity.transform.position, entity.transform.rotation) as LockedByEntCrate;
                    crate.enableSaving = false;
                    crate.Spawn();

                    if (entity.ShortPrefabName == "heli_crate") HeliCrates.Remove(entity);
                    else BradleyCrates.Remove(entity);
                    _backpackCrates.Remove(backpack);
                    backpack.Kill();

                    if (crate.ShortPrefabName == "heli_crate")
                    {
                        HeliCrates.Add(crate);
                        CrateConfig config = _ins._config.HeliCrates;
                        if (config.TypeLootTable == 1 || config.TypeLootTable == 4 || config.TypeLootTable == 5)
                        {
                            _ins.NextTick(() =>
                            {
                                crate.inventory.ClearItemsContainer();
                                if (config.TypeLootTable == 4 || config.TypeLootTable == 5) _ins.AddToContainerPrefab(crate.inventory, config.PrefabLootTable);
                                if (config.TypeLootTable == 1 || config.TypeLootTable == 5) _ins.AddToContainerItem(crate.inventory, config.OwnLootTable);
                            });
                        }
                    }
                    else
                    {
                        BradleyCrates.Add(crate);
                        CrateConfig config = _ins._config.BradleyCrates;
                        if (config.TypeLootTable == 1 || config.TypeLootTable == 4 || config.TypeLootTable == 5)
                        {
                            _ins.NextTick(() =>
                            {
                                crate.inventory.ClearItemsContainer();
                                if (config.TypeLootTable == 4 || config.TypeLootTable == 5) _ins.AddToContainerPrefab(crate.inventory, config.PrefabLootTable);
                                if (config.TypeLootTable == 1 || config.TypeLootTable == 5) _ins.AddToContainerItem(crate.inventory, config.OwnLootTable);
                            });
                        }
                    }
                }, 5f);
            }

            internal void AddPlayerInCrane(BasePlayer player)
            {
                PlayersInCrane.Add(player);
                if (PlayersInCrane.Count == 1) InvokeRepeating(UpdateMarkerForPlayerInCrane, 0f, 1f);
            }

            internal void RemovePlayerInCrane(BasePlayer player)
            {
                PlayersInCrane.Remove(player);
                if (PlayersInCrane.Count == 0) CancelInvoke(UpdateMarkerForPlayerInCrane);
            }

            private void UpdateMarkerForPlayerInCrane()
            {
                foreach (BasePlayer player in PlayersInCrane)
                {
                    foreach (BaseCombatEntity car in BrokenCars)
                    {
                        if (!car.IsExists()) continue;
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                        player.SendEntityUpdate();
                        player.SendConsoleCommand("ddraw.text", 1f, Color.white, car.transform.position, "<size=40><color=#e2c97ed9>◈</color></size>");
                        if (player.Connection.authLevel < 2) player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    }
                }
            }

            private void SpawnCrane(Vector3 pos, Vector3 rot)
            {
                ChechTrash(pos, 5f);
                MagnetCrane crane = GameManager.server.CreateEntity("assets/content/vehicles/crane_magnet/magnetcrane.entity.prefab", pos, Quaternion.Euler(rot)) as MagnetCrane;
                crane.enableSaving = false;
                crane.Spawn();
                if (!Cranes.Contains(crane)) Cranes.Add(crane);
            }

            internal void KillCrane()
            {
                if (IsValidPlaceSpawnCrane(_mainCranePos)) SpawnCrane(_mainCranePos, _mainCraneRot);
                else SpawnCrane(_addCranePos, _addCraneRot);
            }

            private bool IsValidPlaceSpawnCrane(Vector3 pos)
            {
                foreach (BaseEntity entity in GetEntities<BaseEntity>(pos, 5f, -1))
                    if (entity is MagnetCrane || entity is BasePlayer)
                        return false;
                return true;
            }

            internal void SpawnTruck(Vector3 pos)
            {
                Truck = GameManager.server.CreateEntity("assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab", pos, transform.rotation) as ModularCar;
                Truck.enableSaving = false;
                Truck.spawnSettings.useSpawnSettings = false;
                Truck.Spawn();

                Truck.GetFuelSystem().GetFuelContainer().inventory.capacity = 0;

                _truckId = Truck.net.ID;

                Truck.transform.position = new Vector3(_landingTruckPos.x, Truck.transform.position.y, _landingTruckPos.z);
                Truck.transform.rotation = transform.rotation;

                Item moduleItem = ItemManager.CreateByName("vehicle.2mod.camper");
                if (!Truck.TryAddModule(moduleItem)) moduleItem.Remove();
                _ins.NextTick(() => Module = Truck.AttachedModuleEntities.First() as VehicleModuleCamper);

                Truck.rigidBody.useGravity = false;
                Truck.rigidBody.detectCollisions = false;

                _parachute = GameManager.server.CreateEntity("assets/prefabs/misc/parachute/parachute.prefab");
                _parachute.enableSaving = false;
                _parachute.SetParent(Truck);
                _parachute.transform.localPosition = new Vector3(0f, 2f, 0f);
                _parachute.Spawn();

                ChechTrash(_landingTruckPos, 2f);

                Truck.rigidBody.AddForce(Vector3.down * 10000f, ForceMode.Force);
                InvokeRepeating(UpdateTruck, 0, 0.5f);
            }

            private void UpdateTruck()
            {
                if (Truck.transform.position.y - _landingTruckPos.y > 100f) Truck.rigidBody.AddForce(Vector3.down * 40000f, ForceMode.Force);
                else Truck.rigidBody.AddForce(Vector3.down * 20000f, ForceMode.Force);
                if (Truck.transform.position.y - _landingTruckPos.y < 1f)
                {
                    Truck.transform.position = _landingTruckPos;
                    Truck.transform.rotation = transform.rotation;
                    Truck.rigidBody.useGravity = true;
                    Truck.rigidBody.detectCollisions = true;
                    if (_parachute.IsExists()) _parachute.Kill();
                    SpawnPreset(_ins._config.NpcTruck);
                    if (_ins._config.PveMode.Pve && _ins.plugins.Exists("PveMode")) _ins.PveMode.Call("EventAddScientists", _ins.Name, Scientists.Select(x => x.net.ID));
                    _ins.AlertToAllPlayers("TruckArrived", _ins._config.Prefix);
                    CancelInvoke(UpdateTruck);
                }
            }

            private HashSet<T> GetEntities<T>(Vector3 position, float radius, int layerMask) where T : BaseEntity
            {
                HashSet<T> result = new HashSet<T>();
                foreach (Collider collider in Physics.OverlapSphere(position, radius, layerMask))
                {
                    BaseEntity entity = collider.ToBaseEntity();
                    if (entity.IsExists() && entity is T) result.Add(entity as T);
                }
                return result;
            }

            private void ChechTrash(Vector3 pos, float radius) { foreach (BaseEntity entity in GetEntities<BaseEntity>(pos, radius, -1)) if (_ins.TrashList.Contains(entity.ShortPrefabName) && entity.IsExists()) entity.Kill(); }

            private void SpawnSamSites()
            {
                Vector3 pos; Quaternion rot;
                foreach (KeyValuePair<Vector3, Vector3> dic in SamSitePositions)
                {
                    GetGlobal(transform, dic.Key, dic.Value, out pos, out rot);
                    SamSite samSite = GameManager.server.CreateEntity("assets/prefabs/npc/sam_site_turret/sam_static.prefab", pos, rot) as SamSite;
                    samSite.enableSaving = false;
                    samSite.Spawn();
                    SamSites.Add(samSite);
                }
            }

            private void SpawnPreset(PresetConfig preset)
            {
                int count = UnityEngine.Random.Range(preset.Min, preset.Max + 1);
                List<Vector3> positions = preset.Positions.Select(x => transform.TransformPoint(x.ToVector3()));
                JObject config = GetObjectConfig(preset.Config);
                for (int i = 0; i < count; i++)
                {
                    Vector3 pos = positions.GetRandom();
                    positions.Remove(pos);
                    ScientistNPC npc = (ScientistNPC)_ins.NpcSpawn.Call("SpawnNpc", pos, config);
                    Scientists.Add(npc);
                }
                Pool.Free(ref positions);
            }

            private JObject GetObjectConfig(NpcConfig config)
            {
                return new JObject
                {
                    ["Name"] = config.Name,
                    ["WearItems"] = new JArray { config.WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                    ["BeltItems"] = new JArray { config.BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods.Select(y => y) } }) },
                    ["Kit"] = config.Kit,
                    ["Health"] = config.Health,
                    ["RoamRange"] = config.RoamRange,
                    ["ChaseRange"] = config.ChaseRange,
                    ["DamageScale"] = config.DamageScale,
                    ["AimConeScale"] = config.AimConeScale,
                    ["DisableRadio"] = config.DisableRadio,
                    ["Stationary"] = config.Stationary,
                    ["CanUseWeaponMounted"] = true,
                    ["CanRunAwayWater"] = true,
                    ["Speed"] = config.Speed,
                    ["Sensory"] = new JObject
                    {
                        ["AttackRangeMultiplier"] = config.AttackRangeMultiplier,
                        ["SenseRange"] = config.SenseRange,
                        ["MemoryDuration"] = config.MemoryDuration,
                        ["CheckVisionCone"] = config.CheckVisionCone,
                        ["VisionCone"] = config.VisionCone
                    }
                };
            }
        }
        #endregion Controller

        #region Spawn Loot
        #region NPC
        private void OnCorpsePopulate(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null) return;
            if (_controller.Scientists.Contains(entity))
            {
                _controller.Scientists.Remove(entity);
                PresetConfig preset = _config.PresetsNpc.FirstOrDefault(x => x.Config.Name == entity.displayName);
                if (preset == null) preset = _config.NpcTruck;
                NextTick(() =>
                {
                    if (corpse == null) return;
                    ItemContainer container = corpse.containers[0];
                    if (preset.TypeLootTable == 0)
                    {
                        for (int i = container.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = container.itemList[i];
                            if (preset.Config.WearItems.Any(x => x.ShortName == item.info.shortname))
                            {
                                item.RemoveFromContainer();
                                item.Remove();
                            }
                        }
                        return;
                    }
                    if (preset.TypeLootTable == 2 || preset.TypeLootTable == 3)
                    {
                        if (preset.Config.IsRemoveCorpse && !corpse.IsDestroyed) corpse.Kill();
                        return;
                    }
                    container.ClearItemsContainer();
                    if (preset.TypeLootTable == 4 || preset.TypeLootTable == 5) AddToContainerPrefab(container, preset.PrefabLootTable);
                    if (preset.TypeLootTable == 1 || preset.TypeLootTable == 5) AddToContainerItem(container, preset.OwnLootTable);
                    if (preset.Config.IsRemoveCorpse && !corpse.IsDestroyed) corpse.Kill();
                });
            }
        }

        private object CanPopulateLoot(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || corpse == null || _controller == null) return null;
            if (_controller.Scientists.Contains(entity))
            {
                PresetConfig preset = _config.PresetsNpc.FirstOrDefault(x => x.Config.Name == entity.displayName);
                if (preset == null) preset = _config.NpcTruck;
                if (preset.TypeLootTable == 2) return null;
                else return true;
            }
            return null;
        }

        private object OnCustomLootNPC(uint netID)
        {
            if (_controller == null) return null;
            ScientistNPC entity = _controller.Scientists.FirstOrDefault(x => x.IsExists() && x.net.ID == netID);
            if (entity != null)
            {
                PresetConfig preset = _config.PresetsNpc.FirstOrDefault(x => x.Config.Name == entity.displayName);
                if (preset == null) preset = _config.NpcTruck;
                if (preset.TypeLootTable == 3) return null;
                else return true;
            }
            return null;
        }
        #endregion NPC

        #region Crates
        private object CanPopulateLoot(LootContainer container)
        {
            if (container == null || _controller == null) return null;
            if (_controller.HeliCrates.Contains(container))
            {
                if (_config.HeliCrates.TypeLootTable == 2) return null;
                else return true;
            }
            else if (_controller.BradleyCrates.Contains(container))
            {
                if (_config.BradleyCrates.TypeLootTable == 2) return null;
                else return true;
            }
            else if (container is HackableLockedCrate && _controller.HackCrates.Contains(container as HackableLockedCrate))
            {
                if (_config.HackCrates.TypeLootTable == 2) return null;
                else return true;
            }
            else return null;
        }

        private object OnCustomLootContainer(uint netID)
        {
            if (_controller == null) return null;
            if (_controller.HeliCrates.Any(x => x.IsExists() && x.net.ID == netID))
            {
                if (_config.HeliCrates.TypeLootTable == 3) return null;
                else return true;
            }
            else if (_controller.BradleyCrates.Any(x => x.IsExists() && x.net.ID == netID))
            {
                if (_config.BradleyCrates.TypeLootTable == 3) return null;
                else return true;
            }
            else if (_controller.HackCrates.Any(x => x.IsExists() && x.net.ID == netID))
            {
                if (_config.HackCrates.TypeLootTable == 3) return null;
                else return true;
            }
            return null;
        }
        #endregion Crates

        private void AddToContainerPrefab(ItemContainer container, PrefabLootTableConfig lootTable)
        {
            if (lootTable.UseCount)
            {
                int count = UnityEngine.Random.Range(lootTable.Min, lootTable.Max + 1);
                HashSet<string> prefabsInContainer = new HashSet<string>();
                while (prefabsInContainer.Count < count)
                {
                    foreach (PrefabConfig prefab in lootTable.Prefabs)
                    {
                        if (prefabsInContainer.Contains(prefab.PrefabDefinition)) continue;
                        if (UnityEngine.Random.Range(0.0f, 100.0f) <= prefab.Chance)
                        {
                            if (_allLootSpawnSlots.ContainsKey(prefab.PrefabDefinition))
                            {
                                LootContainer.LootSpawnSlot[] lootSpawnSlots = _allLootSpawnSlots[prefab.PrefabDefinition];
                                foreach (LootContainer.LootSpawnSlot lootSpawnSlot in lootSpawnSlots)
                                    for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
                                        if (UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.probability)
                                            lootSpawnSlot.definition.SpawnIntoContainer(container);
                            }
                            else _allLootSpawn[prefab.PrefabDefinition].SpawnIntoContainer(container);
                            prefabsInContainer.Add(prefab.PrefabDefinition);
                            if (prefabsInContainer.Count == count) return;
                        }
                    }
                }
            }
            else
            {
                HashSet<string> prefabsInContainer = new HashSet<string>();
                foreach (PrefabConfig prefab in lootTable.Prefabs)
                {
                    if (prefabsInContainer.Contains(prefab.PrefabDefinition)) continue;
                    if (UnityEngine.Random.Range(0.0f, 100.0f) <= prefab.Chance)
                    {
                        if (_allLootSpawnSlots.ContainsKey(prefab.PrefabDefinition))
                        {
                            LootContainer.LootSpawnSlot[] lootSpawnSlots = _allLootSpawnSlots[prefab.PrefabDefinition];
                            foreach (LootContainer.LootSpawnSlot lootSpawnSlot in lootSpawnSlots)
                                for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
                                    if (UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.probability)
                                        lootSpawnSlot.definition.SpawnIntoContainer(container);
                        }
                        else _allLootSpawn[prefab.PrefabDefinition].SpawnIntoContainer(container);
                        prefabsInContainer.Add(prefab.PrefabDefinition);
                    }
                }
            }
        }

        private void AddToContainerItem(ItemContainer container, LootTableConfig lootTable)
        {
            if (lootTable.UseCount)
            {
                int count = UnityEngine.Random.Range(lootTable.Min, lootTable.Max + 1);
                HashSet<int> indexMove = new HashSet<int>();
                while (indexMove.Count < count)
                {
                    foreach (ItemConfig item in lootTable.Items)
                    {
                        if (indexMove.Contains(lootTable.Items.IndexOf(item))) continue;
                        if (UnityEngine.Random.Range(0.0f, 100.0f) <= item.Chance)
                        {
                            Item newItem = item.IsBluePrint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.ShortName, UnityEngine.Random.Range(item.MinAmount, item.MaxAmount + 1), item.SkinID);
                            if (newItem == null)
                            {
                                PrintWarning($"Failed to create item! ({item.ShortName})");
                                continue;
                            }
                            if (item.IsBluePrint) newItem.blueprintTarget = ItemManager.FindItemDefinition(item.ShortName).itemid;
                            if (!string.IsNullOrEmpty(item.Name)) newItem.name = item.Name;
                            if (container.capacity < container.itemList.Count + 1) container.capacity++;
                            if (!newItem.MoveToContainer(container)) newItem.Remove();
                            else
                            {
                                indexMove.Add(lootTable.Items.IndexOf(item));
                                if (indexMove.Count == count) return;
                            }
                        }
                    }
                }
            }
            else
            {
                HashSet<int> indexMove = new HashSet<int>();
                foreach (ItemConfig item in lootTable.Items)
                {
                    if (indexMove.Contains(lootTable.Items.IndexOf(item))) continue;
                    if (UnityEngine.Random.Range(0.0f, 100.0f) <= item.Chance)
                    {
                        Item newItem = item.IsBluePrint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.ShortName, UnityEngine.Random.Range(item.MinAmount, item.MaxAmount + 1), item.SkinID);
                        if (newItem == null)
                        {
                            PrintWarning($"Failed to create item! ({item.ShortName})");
                            continue;
                        }
                        if (item.IsBluePrint) newItem.blueprintTarget = ItemManager.FindItemDefinition(item.ShortName).itemid;
                        if (!string.IsNullOrEmpty(item.Name)) newItem.name = item.Name;
                        if (container.capacity < container.itemList.Count + 1) container.capacity++;
                        if (!newItem.MoveToContainer(container)) newItem.Remove();
                        else indexMove.Add(lootTable.Items.IndexOf(item));
                    }
                }
            }
        }

        private void CheckAllLootTables()
        {
            CheckLootTable(_config.HeliCrates.OwnLootTable);
            CheckPrefabLootTable(_config.HeliCrates.PrefabLootTable);

            CheckLootTable(_config.BradleyCrates.OwnLootTable);
            CheckPrefabLootTable(_config.BradleyCrates.PrefabLootTable);

            CheckLootTable(_config.HackCrates.OwnLootTable);
            CheckPrefabLootTable(_config.HackCrates.PrefabLootTable);

            foreach (PresetConfig preset in _config.PresetsNpc)
            {
                CheckLootTable(preset.OwnLootTable);
                CheckPrefabLootTable(preset.PrefabLootTable);
            }

            CheckLootTable(_config.NpcTruck.OwnLootTable);
            CheckPrefabLootTable(_config.NpcTruck.PrefabLootTable);

            SaveConfig();
        }

        private void CheckLootTable(LootTableConfig lootTable)
        {
            lootTable.Items = lootTable.Items.OrderBy(x => x.Chance);
            if (lootTable.Max > lootTable.Items.Count) lootTable.Max = lootTable.Items.Count;
            if (lootTable.Min > lootTable.Max) lootTable.Min = lootTable.Max;
        }

        private void CheckPrefabLootTable(PrefabLootTableConfig lootTable)
        {
            HashSet<PrefabConfig> prefabs = new HashSet<PrefabConfig>();
            foreach (PrefabConfig prefabConfig in lootTable.Prefabs)
            {
                if (prefabs.Any(x => x.PrefabDefinition == prefabConfig.PrefabDefinition)) PrintWarning($"Duplicate prefab removed from loot table! ({prefabConfig.PrefabDefinition})");
                else
                {
                    GameObject gameObject = GameManager.server.FindPrefab(prefabConfig.PrefabDefinition);
                    global::HumanNPC humanNpc = gameObject.GetComponent<global::HumanNPC>();
                    ScarecrowNPC scarecrowNPC = gameObject.GetComponent<ScarecrowNPC>();
                    LootContainer lootContainer = gameObject.GetComponent<LootContainer>();
                    if (humanNpc != null && humanNpc.LootSpawnSlots.Length != 0)
                    {
                        if (!_allLootSpawnSlots.ContainsKey(prefabConfig.PrefabDefinition)) _allLootSpawnSlots.Add(prefabConfig.PrefabDefinition, humanNpc.LootSpawnSlots);
                        prefabs.Add(prefabConfig);
                    }
                    else if (scarecrowNPC != null && scarecrowNPC.LootSpawnSlots.Length != 0)
                    {
                        if (!_allLootSpawnSlots.ContainsKey(prefabConfig.PrefabDefinition)) _allLootSpawnSlots.Add(prefabConfig.PrefabDefinition, scarecrowNPC.LootSpawnSlots);
                        prefabs.Add(prefabConfig);
                    }
                    else if (lootContainer != null && lootContainer.LootSpawnSlots.Length != 0)
                    {
                        if (!_allLootSpawnSlots.ContainsKey(prefabConfig.PrefabDefinition)) _allLootSpawnSlots.Add(prefabConfig.PrefabDefinition, lootContainer.LootSpawnSlots);
                        prefabs.Add(prefabConfig);
                    }
                    else if (lootContainer != null && lootContainer.lootDefinition != null)
                    {
                        if (!_allLootSpawn.ContainsKey(prefabConfig.PrefabDefinition)) _allLootSpawn.Add(prefabConfig.PrefabDefinition, lootContainer.lootDefinition);
                        prefabs.Add(prefabConfig);
                    }
                    else PrintWarning($"Unknown prefab removed! ({prefabConfig.PrefabDefinition})");
                }
            }
            lootTable.Prefabs = prefabs.OrderBy(x => x.Chance);
            if (lootTable.Max > lootTable.Prefabs.Count) lootTable.Max = lootTable.Prefabs.Count;
            if (lootTable.Min > lootTable.Max) lootTable.Min = lootTable.Max;
        }

        private readonly Dictionary<string, LootSpawn> _allLootSpawn = new Dictionary<string, LootSpawn>();

        private readonly Dictionary<string, LootContainer.LootSpawnSlot[]> _allLootSpawnSlots = new Dictionary<string, LootContainer.LootSpawnSlot[]>();
        #endregion Spawn Loot

        #region Animation
        internal class PointAnimationTransform { public float Time; public Vector3 Pos; public Vector3 Rot; }

        internal class AnimationTransform : FacepunchBehaviour
        {
            private BaseEntity _entity;
            private List<PointAnimationTransform> _path = new List<PointAnimationTransform>();

            private Rigidbody _rigidbody;

            private float _secondsTaken;
            private float _secondsToTake;
            private float _waypointDone;

            private Vector3 _startPos;
            private Vector3 _endPos;

            private Vector3 _startRot;
            private Vector3 _endRot;

            private void Awake()
            {
                _entity = GetComponent<BaseEntity>();
                _rigidbody = GetComponent<Rigidbody>();
                enabled = false;
            }

            internal void AddPath(List<PointAnimationTransform> path)
            {
                _path = path;
                if (_rigidbody != null) _rigidbody.isKinematic = true;
                enabled = true;
            }

            private void FixedUpdate()
            {
                if (_secondsTaken == 0f)
                {
                    if (_path.Count == 0)
                    {
                        _startPos = _endPos = Vector3.zero;
                        _startRot = _endRot = Vector3.zero;
                        _secondsToTake = 0f;
                        _secondsTaken = 0f;
                        _waypointDone = 0f;
                        enabled = false;
                        if (_rigidbody != null)
                        {
                            _rigidbody.isKinematic = false;
                            float mass = _rigidbody.mass;
                            _rigidbody.mass = 1f;
                            _rigidbody.AddForce(_entity.transform.forward * 100f, ForceMode.Force);
                            Invoke(() => _rigidbody.mass = mass, 5f);
                        }
                        if (_entity is LockedByEntCrate) _ins._controller.AddRigidBody(_entity as LockedByEntCrate);
                        return;
                    }
                    _startPos = transform.position;
                    _startRot = transform.rotation.eulerAngles;
                    if (_path[0].Pos != _startPos || _path[0].Rot != _startRot)
                    {
                        _endPos = _path[0].Pos != _startPos ? _path[0].Pos : _startPos;
                        _endRot = _path[0].Rot != _startRot ? _path[0].Rot : _startRot;
                        _secondsToTake = _path[0].Time;
                        _secondsTaken = 0f;
                        _waypointDone = 0f;
                    }
                    _path.RemoveAt(0);
                }
                if (_startPos != _endPos || _startRot != _endRot)
                {
                    _secondsTaken += Time.deltaTime;
                    _waypointDone = Mathf.InverseLerp(0f, _secondsToTake, _secondsTaken);
                    if (_startPos != _endPos) transform.position = Vector3.Lerp(_startPos, _endPos, _waypointDone);
                    if (_startRot != _endRot) transform.rotation = Quaternion.Lerp(Quaternion.Euler(_startRot), Quaternion.Euler(_endRot), _waypointDone);
                    _entity.TransformChanged();
                    _entity.SendNetworkUpdate();
                    if (_waypointDone >= 1f) _secondsTaken = 0f;
                }
            }
        }
        #endregion Animation

        #region TruePVE
        private object CanEntityTakeDamage(BasePlayer victim, HitInfo hitinfo)
        {
            if (!_config.IsCreateZonePvp || victim == null || hitinfo == null || _controller == null) return null;
            BasePlayer attacker = hitinfo.InitiatorPlayer;
            if (_controller.Players.Contains(victim) && (attacker == null || _controller.Players.Contains(attacker))) return true;
            else return null;
        }
        #endregion TruePVE

        #region NTeleportation
        private object CanTeleport(BasePlayer player, Vector3 to)
        {
            if (_config.NTeleportationInterrupt && _controller != null && (_controller.Players.Contains(player) || Vector3.Distance(_controller.transform.position, to) < Radius)) return GetMessage("NTeleportation", player.UserIDString, _config.Prefix);
            else return null;
        }
        #endregion NTeleportation

        #region Economy
        [PluginReference] private readonly Plugin Economics, ServerRewards, IQEconomic;

        private readonly Dictionary<ulong, double> _playersBalance = new Dictionary<ulong, double>();

        private void ActionEconomy(ulong playerId, string type, string arg = "")
        {
            switch (type)
            {
                case "Crates":
                    if (_config.Economy.Crates.ContainsKey(arg)) AddBalance(playerId, _config.Economy.Crates[arg]);
                    break;
                case "Npc":
                    AddBalance(playerId, _config.Economy.Npc);
                    break;
                case "LockedCrate":
                    AddBalance(playerId, _config.Economy.LockedCrate);
                    break;
                case "ShredderCar":
                    AddBalance(playerId, _config.Economy.ShredderCar);
                    break;
                case "ShredderTruck":
                    AddBalance(playerId, _config.Economy.ShredderTruck);
                    break;
            }
        }

        private void AddBalance(ulong playerId, double balance)
        {
            if (balance == 0) return;
            if (_playersBalance.ContainsKey(playerId)) _playersBalance[playerId] += balance;
            else _playersBalance.Add(playerId, balance);
        }

        private void SendBalance()
        {
            if (_playersBalance.Count == 0) return;
            foreach (KeyValuePair<ulong, double> dic in _playersBalance)
            {
                if (dic.Value < _config.Economy.Min) continue;
                int intCount = Convert.ToInt32(dic.Value);
                if (_config.Economy.Plugins.Contains("Economics") && plugins.Exists("Economics") && dic.Value > 0) Economics.Call("Deposit", dic.Key.ToString(), dic.Value);
                if (_config.Economy.Plugins.Contains("Server Rewards") && plugins.Exists("ServerRewards") && intCount > 0) ServerRewards.Call("AddPoints", dic.Key, intCount);
                if (_config.Economy.Plugins.Contains("IQEconomic") && plugins.Exists("IQEconomic") && intCount > 0) IQEconomic.Call("API_SET_BALANCE", dic.Key, intCount);
                BasePlayer player = BasePlayer.FindByID(dic.Key);
                if (player != null) AlertToPlayer(player, GetMessage("SendEconomy", player.UserIDString, _config.Prefix, dic.Value));
            }
            ulong winnerId = _playersBalance.Max(x => x.Value).Key;
            foreach (string command in _config.Economy.Commands) Server.Command(command.Replace("{steamid}", $"{winnerId}"));
            _playersBalance.Clear();
        }
        #endregion Economy

        #region Alerts
        [PluginReference] private readonly Plugin GUIAnnouncements, DiscordMessages;

        private string ClearColorAndSize(string message)
        {
            message = message.Replace("</color>", string.Empty);
            message = message.Replace("</size>", string.Empty);
            while (message.Contains("<color="))
            {
                int index = message.IndexOf("<color=", StringComparison.Ordinal);
                message = message.Remove(index, message.IndexOf(">", index, StringComparison.Ordinal) - index + 1);
            }
            while (message.Contains("<size="))
            {
                int index = message.IndexOf("<size=", StringComparison.Ordinal);
                message = message.Remove(index, message.IndexOf(">", index, StringComparison.Ordinal) - index + 1);
            }
            message = message.Replace(_config.Prefix + " ", string.Empty);
            return message;
        }

        private bool CanSendDiscordMessage() => _config.Discord.IsDiscord && !string.IsNullOrEmpty(_config.Discord.WebhookUrl) && _config.Discord.WebhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

        private void AlertToAllPlayers(string langKey, params object[] args)
        {
            if (CanSendDiscordMessage() && _config.Discord.Keys.Contains(langKey))
            {
                object fields = new[] { new { name = Title, value = ClearColorAndSize(GetMessage(langKey, null, args)), inline = false } };
                DiscordMessages?.Call("API_SendFancyMessage", _config.Discord.WebhookUrl, "", _config.Discord.EmbedColor, JsonConvert.SerializeObject(fields), null, this);
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList) AlertToPlayer(player, GetMessage(langKey, player.UserIDString, args));
        }

        private void AlertToPlayer(BasePlayer player, string message)
        {
            if (_config.IsChat) PrintToChat(player, message);
            if (_config.GuiAnnouncements.IsGuiAnnouncements) GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(message), _config.GuiAnnouncements.BannerColor, _config.GuiAnnouncements.TextColor, player, _config.GuiAnnouncements.ApiAdjustVPosition);
            if (_config.Notify.IsNotify) player.SendConsoleCommand($"notify.show {_config.Notify.Type} {ClearColorAndSize(message)}");
        }
        #endregion Alerts

        #region GUI
        private void MessageGUI(BasePlayer player, string text)
        {
            CuiHelper.DestroyUi(player, "TextMain");

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = _config.Gui.AnchorMin, AnchorMax = _config.Gui.AnchorMax },
                CursorEnabled = false,
            }, "Hud", "TextMain");
            container.Add(new CuiElement
            {
                Parent = "TextMain",
                Components =
                {
                    new CuiTextComponent() { Color = "1 1 1 1", FadeIn = 0f, Text = text, FontSize = 24, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
                    new CuiOutlineComponent { Distance = "1 1", Color = "0 0 0 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            CuiHelper.AddUi(player, container);
        }
        #endregion GUI

        #region Helpers
        [PluginReference] private readonly Plugin NpcSpawn, BetterNpc, PveMode;

        internal float Radius = 100f;

        private readonly HashSet<string> _hooks = new HashSet<string>
        {
            "OnEntityTakeDamage",
            "CanMountEntity",
            "CanDismountEntity",
            "OnEntitySpawned",
            "OnEntityKill",
            "OnEntityEnter",
            "OnCargoPlaneSignaled",
            "OnSupplyDropDropped",
            "OnVehiclePush",
            "OnEntityDeath",
            "CanHackCrate",
            "OnCrateHack",
            "OnLootEntity",
            "OnCorpsePopulate",
            "CanPopulateLoot",
            "OnCustomLootNPC",
            "OnCustomLootContainer",
            "CanEntityTakeDamage",
            "CanTeleport"
        };

        private void Unsubscribes() { foreach (string hook in _hooks) Unsubscribe(hook); }

        private void Subscribes()
        {
            foreach (string hook in _hooks)
            {
                if (hook == "CanEntityTakeDamage" && !_config.IsCreateZonePvp) continue;
                if (hook == "CanTeleport" && !_config.NTeleportationInterrupt) continue;
                Subscribe(hook);
            }
        }

        internal class Location { public Vector3 pos; public Vector3 rot; }

        internal List<Location> StartLocations = new List<Location>();
        #endregion Helpers

        #region Commands
        [ChatCommand("jstart")]
        private void ChatStartEvent(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if (!_active) Start();
                else PrintToChat(player, GetMessage("EventActive", player.UserIDString, _config.Prefix));
            }
        }

        [ChatCommand("jstop")]
        private void ChatStopEvent(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if (_controller != null) Finish();
                else Server.Command($"o.reload {Name}");
            }
        }

        [ChatCommand("jpos")]
        private void ChatCommandPos(BasePlayer player)
        {
            if (!player.IsAdmin || _controller == null) return;
            Vector3 pos = _controller.transform.InverseTransformPoint(player.transform.position);
            Vector3 rot = player.viewAngles - _controller.transform.rotation.eulerAngles;
            Puts($"Position: {pos}. Rotation: {rot}");
            PrintToChat(player, $"Position: {pos}\nRotation: {rot}");
        }

        [ConsoleCommand("jstart")]
        private void ConsoleStartEvent(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                if (!_active) Start();
                else Puts("This event is active now. To finish this event (jstop), then to start the next one");
            }
        }

        [ConsoleCommand("jstop")]
        private void ConsoleStopEvent(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                if (_controller != null) Finish();
                else Server.Command($"o.reload {Name}");
            }
        }
        #endregion Commands
    }
}

namespace Oxide.Plugins.JunkyardEventExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
        }

        public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return enumerator.Current;
            return default(TSource);
        }

        #region Select
        public static HashSet<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> predicate)
        {
            HashSet<TResult> result = new HashSet<TResult>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(predicate(enumerator.Current));
            return result;
        }

        public static List<TResult> Select<TSource, TResult>(this IList<TSource> source, Func<TSource, TResult> predicate)
        {
            List<TResult> result = new List<TResult>();
            for (int i = 0; i < source.Count; i++)
            {
                TSource element = source[i];
                result.Add(predicate(element));
            }
            return result;
        }
        #endregion Select

        public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source)
        {
            List<TSource> result = new List<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static TSource First<TSource>(this IList<TSource> source) => source[0];

        public static TSource Min<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue < resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static TSource Max<TSource>(this IEnumerable<TSource> source, Func<TSource, double> predicate)
        {
            TSource result = source.ElementAt(0);
            double resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    double elementValue = predicate(element);
                    if (elementValue > resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static TSource ElementAt<TSource>(this IEnumerable<TSource> source, int index)
        {
            int movements = 0;
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (movements == index) return enumerator.Current;
                    movements++;
                }
            }
            return default(TSource);
        }

        public static List<TSource> OrderBy<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            List<TSource> result = source.ToList();
            for (int i = 0; i < result.Count; i++)
            {
                for (int j = 0; j < result.Count - 1; j++)
                {
                    if (predicate(result[j]) > predicate(result[j + 1]))
                    {
                        TSource z = result[j];
                        result[j] = result[j + 1];
                        result[j + 1] = z;
                    }
                }
            }
            return result;
        }

        public static List<TSource> Skip<TSource>(this IList<TSource> source, int count)
        {
            List<TSource> result = new List<TSource>();
            for (int i = 0; i < source.Count; i++)
            {
                if (i < count) continue;
                result.Add(source[i]);
            }
            return result;
        }

        public static bool IsPlayer(this BasePlayer player) => player != null && player.userID.IsSteamId();

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;

        public static void ClearItemsContainer(this ItemContainer container)
        {
            for (int i = container.itemList.Count - 1; i >= 0; i--)
            {
                Item item = container.itemList[i];
                item.RemoveFromContainer();
                item.Remove();
            }
        }
    }
}