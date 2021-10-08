﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Facepunch;
using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using Rust.Modular;
using UnityEngine;
using UnityEngine.SceneManagement;
using static NPCPlayerApex;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("XDCobaltLaboratory", "DezLife", "1.9.83")]
    public class XDCobaltLaboratory : RustPlugin
    {
        #region Var
        [PluginReference] Plugin CopyPaste, IQChat, RustMap;
        private static XDCobaltLaboratory _;
        private List<uint> Light = new List<uint> { 3887352222, 1889323056, 630866573, 4027991414, 1392608348, 1748062128 };
        private HashSet<Vector3> busyPoints3D = new HashSet<Vector3>();
        private List<BaseEntity> HouseCobaltLab = new List<BaseEntity>();
        private List<NPCMonitor> nPCMonitors = new List<NPCMonitor>();
        private NpcZones npcZones = null;
        private List<UInt64> HideUIUser = new List<UInt64>();
        private string PosIvent;
        private HackableLockedCrate CrateEnt;
        private int maxTry = 250000;
        private const int MaxRadius = 5;
        private Timer SpawnHouseTime;
        private Timer RemoveHouseTime;
        private static DateTime TimeCreatedSave = SaveRestore.SaveCreatedTime.Date;
        private static DateTime RealTime = DateTime.Now.Date;
        private static int SaveCreated = RealTime.Subtract(TimeCreatedSave).Days;
        private Configuration.BuildingPasteSettings.PasteBuild labIndex = null;
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["XD_IVENT_START"] = "<color=#008000>[Cobalt Lab Event] </color>Ученые разбили на этом острове свою лабораторию под названием Кобальт, скорее всего там находятся ценные вещи, ведь она охраняется!\nКвадрат: <color=#00FF00>{0}</color>",
                ["XD_IVENT_STARTUI"] = "Ученые развернули свою Кобальтовую лабораторию!\nКвадрат : {0}",
                ["XD_IVENT_NOPLAYER"] = "<color=#008000>[Cobalt Lab Event] </color>Ученые закончили свой эксперимент и успешно покинули остров без происшествий",
                ["XD_IVENT_CRATEHACK"] = "<color=#008000>[Cobalt Lab Event] </color>В лаборатории кобальт <color=#FFD700>{0}</color> начал взлом секретного ящика в квадрате <color=#00FF00>{1}</color>",
                ["XD_IVENT_CRATEHACKHELP"] = "<color=#008000>[Cobalt Lab Event] </color>В лаборатории кобальт <color=#FFD700>{0}</color> начал взлом секретного ящика в квадрате <color=#00FF00>{1}</color>\nНа это место уже прибыла подмога! Будте осторожней",
                ["XD_IVENT_CRATEHACKEND"] = "<color=#008000>[Cobalt Lab Event] </color>В лаборатории кобальт был взломан секретный ящик, ученые начинают эвакуацию с острова, у вас осталось <color=#9ACD32>{0} минут</color>, чтобы забрать его!",
                ["XD_IVENT_CRATELOOTFOUND"] = " <color=#008000>[Cobalt Lab Event] </color>В лаборатории кобальт никто не успел залутать взломанный ящик, лаборатория была эвакуирована и постройка разрушена",
                ["XD_IVENT_CRATELOOTPLAYER"] = "<color=#008000>[Cobalt Lab Event] </color><color=#FFD700>{0}</color> успешно ограбил лабораторию кобальт и забрал ценные вещи с секретного ящика",
                ["XD_IVENT_HOUSECOBALT"] = "Cobalt laboratory",
                ["XD_IVENT_START_DISCORD"] = "Ученые разбили на этом острове свою лабораторию под названием Кобальт,скорее всего там находится ценные вещи, ведь он охраняется!\nКвадрат : {0}",
                ["XD_IVENT_NOPLAYER_DISCORD"] = "Ученые закончили свой эксперимент и успешно покинули остров без происшествий",
                ["XD_IVENT_CRATEHACK_DISCORD"] = "В лаборатории кобальт {0} начал взлом секретного ящика в квадрате {1}\nСоберитесь с силами и отбейте его",
                ["XD_IVENT_CRATEHACKHELP_DISCORD"] = "В лаборатории кобальт {0} начал взлом секретного ящика в квадрате {1}\nСоберитесь с силами и отбейте его\nНа это место уже прибыла подмога! Будте осторожней",
                ["XD_IVENT_CRATEHACKEND_DISCORD"] = "В лаборатории кобальт был взломан секретный ящик, ученые начинают эвакуацию с острова, у вас осталось {0} минут, чтобы забрать его!",
                ["XD_IVENT_CRATELOOTFOUND_DISCORD"] = "В лаборатории кобальт никто не успел залутать взломанный ящик, лаборатория была эвакуирована и постройка разрушена",
                ["XD_IVENT_CRATELOOTPLAYER_DISCORD"] = "{0}  успешно ограбил лабораторию кобальт и забрал ценные вещи с секретного ящика",
                ["XD_IVENT_CLCONTROLLER_HELI_HELP_DISCORD "] = "Внутри ящика оказалась ловушка с сигналом в службу охраны Cobalt.\nНа защиту вылетел боевой вертолёт\nВы должны сбить его или выжить в течении 5 минут.",
                ["XD_IVENT_CL_ITEM_SYNTAX"] = "Используйте:\n/cl.items add - добавить лут к существующему\n/cl.items reset - заменить старый лут на новый",
                ["XD_IVENT_CL_ITEM_ADDED"] = "Вы успешно добавили новые предметы для ящика.\nОбязательно настройте их в конфиге",
                ["XD_IVENT_CL_ITEM_REPLACED"] = "Вы успешно заменили все предметы на новые.\nОбязательно настройте их в конфиге",
                ["XD_IVENT_CLBOT_ITEM_SYNTAX"] = "Используйте:\n/cl.botitems add - добавить лут к существующему\n/cl.botitems reset - заменить старый лут на новый",
                ["XD_IVENT_CLBOT_ITEM_ADDED"] = "Вы успешно добавили новые предметы для npc.\nОбязательно настройте их в конфиге",
                ["XD_IVENT_CLBOT_ITEM_REPLACED"] = "Вы успешно заменили все предметы на новые.\nОбязательно настройте их в конфиге",
                ["XD_IVENT_CLCONTROLLER_SYNTAX"] = "Используйте:\n/cl start - Запуск ивента досрочно\n/cl stop - отменить ивент досрочно",
                ["XD_IVENT_CLCONTROLLER_ACTIVE"] = "Ивент уже активен!",
                ["XD_IVENT_CLCONTROLLER_STOPADMIN"] = "Ивент окончен досрочно администратором!",
                ["XD_IVENT_CLCONTROLLER_NOT_ACTIVE"] = "Нет активных ивентов",
                ["XD_IVENT_CLCONTROLLER_NOT_ENOUGH_PLAYERS"] = "Недостаточно игроков для запуска ивента!",
                ["XD_IVENT_CLCONTROLLER_BUILDING_ERROR"] = "Ошибка #1 \nПостройка не смогла заспавнится\nОбратитесь к разработчику\nDezLife#1480\nvk.com/dezlife",
                ["XD_IVENT_CLCONTROLLER_NOT_BUILDING_BOX"] = "Ошибка #3, В постройке не найден ящик",
                ["XD_IVENT_CLCONTROLLER_THE_POINTS_ARE_ENDED"] = "Все точки закончены!\nНачинаем генерировать новые...",
                ["XD_IVENT_CLCONTROLLER_LOADING_THE_CONSTRUCTION"] = "Файл постройки не найден!\nНачинаем импортировать...",
                ["XD_IVENT_CLCONTROLLER_CONSTRUCTION_LOADED"] = "Постройка успешно загружена!",
                ["XD_IVENT_CLCONTROLLER_ERROR_DOWNLOADING"] = "Ошибка при загрузке постройки!\nПробуем загрузить еще раз",
                ["XD_IVENT_CLCONTROLLER_RETAINED_UPLOAD_ERROR"] = "Повторная загрузка не была успешной\nОбратитесь к разработчику\nDezLife#1480\nvk.com/dezlife",
                ["XD_IVENT_CLCONTROLLER_HELI_HELP"] = "<color=#008000>[Cobalt Lab Event] </color>Внутри ящика оказалась ловушка с сигналом в службу охраны Cobalt.\nНа защиту вылетел боевой вертолёт\nВы должны сбить его или выжить в течении 5 минут.",
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["XD_IVENT_START"] = "<color=#008000>[Cobalt Lab Event] </color>Scientists have set up their laboratory on this island called Cobalt, most likely there are valuable things there, because it is protected!\nGrid: <color=#00FF00>{0}</color>",
                ["XD_IVENT_STARTUI"] = "Scientists have deployed their Cobalt Lab!\nGrid : {0}",
                ["XD_IVENT_NOPLAYER"] = "<color=#008000>[Cobalt Lab Event] </color>The scientists completed their experiment and successfully left the island without incident",
                ["XD_IVENT_CRATEHACK"] = "<color=#008000>[Cobalt Lab Event] </color>In the cobalt lab <color=#FFD700>{0}</color> started hacking a secret crate in the grid <color=#00FF00>{1}</color>",
                ["XD_IVENT_CRATEHACKHELP"] = "<color=#008000>[Cobalt Lab Event] </color>In the cobalt lab <color=#FFD700>{0}</color> started hacking a secret crate in the grid <color=#00FF00>{1}</color>\nHelp has already arrived at this place! Be careful",
                ["XD_IVENT_CRATEHACKEND"] = "<color=#008000>[Cobalt Lab Event] </color>A secret crate has been hacked in the cobalt lab, scientists are beginning to evacuate the island, you have <color=#9ACD32>{0} minutes</color> left to pick it up!",
                ["XD_IVENT_CRATELOOTFOUND"] = " <color=#008000>[Cobalt Lab Event] </color>In the cobalt laboratory, no one had time to hack the secret crate, the laboratory was evacuated and the building was destroyed",
                ["XD_IVENT_CRATELOOTPLAYER"] = "<color=#008000>[Cobalt Lab Event] </color><color=#FFD700>{0}</color> successfully robbed the cobalt lab and took valuables from a secret crate",
                ["XD_IVENT_HOUSECOBALT"] = "Cobalt laboratory",
                ["XD_IVENT_START_DISCORD"] = "Scientists have set up their laboratory on this island called Cobalt, most likely there are valuable things there, because it is guarded!\nКвадрат : {0}",
                ["XD_IVENT_NOPLAYER_DISCORD"] = "Scientists finished their experiment and successfully left the island without incident.",
                ["XD_IVENT_CRATEHACK_DISCORD"] = "In the laboratory, cobalt {0} started breaking into a secret box in square {1}\nGather your strength and fight it off",
                ["XD_IVENT_CRATEHACKHELP_DISCORD"] = "In the laboratory, cobalt {0} started breaking into a secret box in square {1}\nGather your strength and fight it off\nHelp has already arrived at this place! Be careful",
                ["XD_IVENT_CRATEHACKEND_DISCORD"] = "A secret box has been broken in the cobalt laboratory, scientists are starting to evacuate the island, you have {0} minutes left to pick it up!",
                ["XD_IVENT_CRATELOOTFOUND_DISCORD"] = " In the cobalt laboratory, no one had time to patch up the cracked box, the laboratory was evacuated and the building was destroyed",
                ["XD_IVENT_CRATELOOTPLAYER_DISCORD"] = "{0}  successfully robbed the cobalt laboratory and took valuable items from the secret box",
                ["XD_IVENT_CLCONTROLLER_HELI_HELP_DISCORD"] = "Inside the box was a trap with a signal to the Cobalt security service.\nA battle helicopter flew to protect the box\nYou need to shoot it down or survive for 5 minutes",
                ["XD_IVENT_CL_ITEM_SYNTAX"] = "Use:\n/cl.items add - add loot to the existing one\n/cl.items reset - replace old loot with a new one",
                ["XD_IVENT_CL_ITEM_ADDED"] = "You have successfully added new items to the crate.\nBe sure to configure them in the config",
                ["XD_IVENT_CL_ITEM_REPLACED"] = "You have successfully replaced all items with new ones.\nBe sure to configure them in the config",
                ["XD_IVENT_CLBOT_ITEM_SYNTAX"] = "Use:\n/cl.botitems add - add loot to the existing one\n/cl.botitems reset - replace old loot with a new one",
                ["XD_IVENT_CLBOT_ITEM_ADDED"] = "You have successfully added new items for the npc.\nBe sure to configure them in the config",
                ["XD_IVENT_CLBOT_ITEM_REPLACED"] = "You have successfully replaced all items with new ones.\nBe sure to configure them in the config",
                ["XD_IVENT_CLCONTROLLER_SYNTAX"] = "Use:\n/cl start - Launching the event ahead of schedule\n/cl stop - cancel the event ahead of schedule",
                ["XD_IVENT_CLCONTROLLER_ACTIVE"] = "The event is already active!",
                ["XD_IVENT_CLCONTROLLER_STOPADMIN"] = "Event ended ahead of schedule by the administrator!",
                ["XD_IVENT_CLCONTROLLER_NOT_ACTIVE"] = "No active events",
                ["XD_IVENT_CLCONTROLLER_NOT_ENOUGH_PLAYERS"] = "Not enough players to start the event!",
                ["XD_IVENT_CLCONTROLLER_BUILDING_ERROR"] = "Error #1 \nThe building could not spawn\nContact the developer\nDezLife#1480\nvk.com/dezlife",
                ["XD_IVENT_CLCONTROLLER_NOT_BUILDING_BOX"] = "Error #3,No crate found in the building",
                ["XD_IVENT_CLCONTROLLER_THE_POINTS_ARE_ENDED"] = "All points are completed!\nWe start generating new ...",
                ["XD_IVENT_CLCONTROLLER_LOADING_THE_CONSTRUCTION"] = "Building file not found!\nWe start to import...",
                ["XD_IVENT_CLCONTROLLER_CONSTRUCTION_LOADED"] = "The building has been loaded successfully!",
                ["XD_IVENT_CLCONTROLLER_ERROR_DOWNLOADING"] = "Error when loading a building!\nTrying to download again",
                ["XD_IVENT_CLCONTROLLER_RETAINED_UPLOAD_ERROR"] = "Reload was not successful\nContact the developer\nDezLife#1480\nvk.com/dezlife",
                ["XD_IVENT_CLCONTROLLER_HELI_HELP"] = "<color=#008000>[Cobalt Lab Event] </color>Inside the box was a trap with a signal to the Cobalt security service.\nA battle helicopter flew to protect the box\nYou need to shoot it down or survive for 5 minutes",
            }, this);
        }

        #endregion

        #region Data
        public void LoadDataCopyPaste(Boolean repeat = false)
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("copypaste/HouseCobalt"))
            {
                PrintWarning(GetLang("XD_IVENT_CLCONTROLLER_LOADING_THE_CONSTRUCTION"));
                webrequest.Enqueue("http://utilite.skyplugins.ru/XDCasinoOutPost/HouseCobaltNew234.json", null, (i, s) =>
                {
                    {
                        if (i == 200)
                        {
                            PasteData obj = JsonConvert.DeserializeObject<PasteData>(s);
                            Interface.Oxide.DataFileSystem.WriteObject("copypaste/HouseCobalt", obj);
                            PrintWarning(GetLang("XD_IVENT_CLCONTROLLER_CONSTRUCTION_LOADED"));
                        }
                        else
                        {
                            if (!repeat)
                            {
                                PrintError(GetLang("XD_IVENT_CLCONTROLLER_ERROR_DOWNLOADING"));
                                timer.Once(10f, () => LoadDataCopyPaste(true));
                            }
                            else
                            {
                                PrintError(GetLang("XD_IVENT_CLCONTROLLER_RETAINED_UPLOAD_ERROR"));
                            }

                            return;
                        }
                    }
                }, this, RequestMethod.GET);
            }
        }
        public class PasteData
        {
            public Dictionary<string, object> @default;
            public ICollection<Dictionary<string, object>> entities;
            public Dictionary<string, object> protocol;
        }
        #endregion

        #region Configuration
        public class LootNpcOrBox
        {
            [JsonProperty("ShortName")]
            public string Shortname;
            [JsonProperty("SkinID")]
            public ulong SkinID;
            [JsonProperty("Имя предмета | Item name")]
            public string DisplayName;
            [JsonProperty("Чертеж? | BluePrint?")]
            public bool BluePrint;
            [JsonProperty("Минимальное количество | Minimal amount")]
            public int MinimalAmount;
            [JsonProperty("Максимальное количество | Maximum amount")]
            public int MaximumAmount;
            [JsonProperty("Шанс выпадения предмета | Item drop chance")]
            public int DropChance;
            [JsonProperty("Умножать этот предмета на день вайпа ? | Multiply this item by wipe day?")]
            public bool wipeCheck;
        }

        private Configuration config;
        private class Configuration
        {
            [JsonProperty("Настройка постройки для ивента (CopyPaste) | Setting up a building for an event (CopyPaste)")]
            public BuildingPasteSettings pasteSettings = new BuildingPasteSettings();
            [JsonProperty("Настройка запуска и остановки ивента | Setting up and stopping an event")]
            public IventController iventController = new IventController();
            [JsonProperty("Настройка уведомлений | Configuring notifications")]
            public NotiferSettings notiferSettings = new NotiferSettings();
            [JsonProperty("Настройка радиации в зоне ивента | Setting up radiation in the event area")]
            public RadiationConroller radiationConroller = new RadiationConroller();
            [JsonProperty("Отображения ивента на картах | Event display on maps")]
            public MapMarkers mapMarkers = new MapMarkers();
            [JsonProperty("Настройка NPC | NPC setup")]
            public NpcController npcController = new NpcController();
            [JsonProperty("Настройка ящика | Customizing the box")]
            public BoxSetting boxSetting = new BoxSetting();

            internal class RadiationConroller
            {
                [JsonProperty("Включить радиацию ? | Turn on radiation?")]
                public bool radUse = true;
                [JsonProperty("Количество радиационных частиц | Number of radiation particles")]
                public int radCount = 20;
            }

            internal class MapMarkers
            {
                [JsonProperty("Отметить ивент на карте RustMap? | Mark event on RustMap?")]
                public bool rustMapUse = true;
                [JsonProperty("Иконка для карты RustMap | Icon for RustMap")]
                public string rustMapIcon = "https://i.imgur.com/bwg6de6.png";
                [JsonProperty("Текст для карты RustMap | RustMap text")]
                public string rustMapTxt = "Cobalt lab";
                [JsonProperty("Отметить ивент на карте G (Требуется https://umod.org/plugins/marker-manager) | Mark the event on the G card (Requires https://umod.org/plugins/marker-manager)")]
                public bool MapUse = false;
                [JsonProperty("Текст для карты G | Text for map G")]
                public string MapTxt = "Cobalt lab";
            }
            internal class IventController
            {
                [JsonProperty("Минимальное количество игроков для запуска ивента | The minimum number of players to start an event")]
                public int minPlayedPlayers = 0;
                [JsonProperty("Время до начала ивента (Минимальное в секундах) | Time before the start of the event (Minimum in seconds)")]
                public int minSpawnIvent = 3000;
                [JsonProperty("Время до начала ивента (Максимальное в секундах) | Time before the start of the event (Maximum in seconds)")]
                public int maxSpawnIvent = 7200;
                [JsonProperty("Время до удаления ивента если никто не откроет ящик (Секунды) | Time until the event is deleted if no one opens the box (Seconds)")]
                public int timeRemoveHouse = 900;
                [JsonProperty("Время до удаления ивента после того как разблокируется ящик | The time until the event is deleted after the box is unlocked")]
                public int timeRemoveHouse2 = 300;
            }
            internal class NpcController
            {           
                [JsonProperty("Рандомные ники нпс | Random nicknames npc")]
                public List<string> nameNPC = new List<string>();
                [JsonProperty("Одежда для NPC | NPC Clothes")]
                public List<ItemNpc> wearNpc = new List<ItemNpc>();
                [JsonProperty("Варианты оружия для NPC | Weapon options for NPCs")]
                public List<ItemNpc> beltNpc = new List<ItemNpc>();
                [JsonProperty("Использовать свой лут в нпс ? | Use your loot in NPCs?")]
                public bool useCustomLoot = true;
                [JsonProperty("Настройка лута в NPC (Если выключенно то будет стандартный) /cl.botitems | Setting up loot in NPC (if disabled, it will be standard) /cl.botitems")]
                public List<LootNpcOrBox> lootNpcs = new List<LootNpcOrBox>();

                internal class ItemNpc
                {
                    [JsonProperty("ShortName")]
                    public string Shortname;
                    [JsonProperty("SkinID")]
                    public ulong SkinID;
                }
            }

            internal class BuildingPasteSettings
            {
                [JsonProperty("Постройки для спавна. (Если более 1 то выбирается рандомная) | Spawn buildings. (If more than 1 then random is selected)")]
                public List<PasteBuild> pasteBuilds = new List<PasteBuild>();
                [JsonProperty("радиус для обнаружения построек игроков | radius for detecting player buildings")]
                public int radiusClear = 25;
                internal class PasteBuild
                {
                    [JsonProperty("Настройка высоты постройки (Требуется в настройке, если вы хотите ставить свою постройку) | Setting the height of the building (Required in the setting if you want to place your building)")]
                    public int heightBuilding;
                    [JsonProperty("файл в папке /oxide/data/copypaste с вашей постройкой(Если не указать загрузится стандартная) | file in the folder /oxide/data/ copypaste with your building (If you do not specify the standard will be loaded)")]
                    public string housepath;
                    [JsonProperty("Спавнить NPC вокруг дома ? | Spawn NPCs around the house?")]
                    public bool useSpawnNPC;
                    [JsonProperty("Колличевство NPC | Number of NPCs")]
                    public int countSpawnNpc;
                    [JsonProperty("ХП NPC | HP NPC")]
                    public int healthNPC;
                    [JsonProperty("Дистанция видимости | Visibility distance")]
                    public int DistanceRange;
                    [JsonProperty("Точность оружия ученого (1 - 100) | Scientist Weapon Accuracy (1 - 100)")]
                    public int Accuracy;
                    [JsonProperty("Спавнить ли подмогу после взлома ящика ? (НПС) | Should I spawn help after breaking the box? (NPC)")]
                    public bool helpBot;
                    [JsonProperty("Колличевство нпс (Подмога) | Number of NPCs (Help)")]
                    public int helpCount;
                    [JsonProperty("Возможность вылета вертолета в качевстве подмоги | The possibility of a helicopter departure as an aid")]
                    public Boolean heliHelp;
                    [JsonProperty("Шанс вылета вертолета(0% - 100%) | Helicopter departure chance (0% - 100%)")]
                    public int ChanceHeliHelp;
                    [JsonProperty("Шанс спавна коптера (Если присутствует в постройке) | Copter spawn chance (if present in a building)")]
                    public int copterChance;
                }
            }

            internal class BoxSetting
            {
                [JsonProperty("Настройка лута в ящике /cl.items | Loot setting in the box /cl.items")]
                public List<LootNpcOrBox> lootBoxes = new List<LootNpcOrBox>();

                [JsonProperty("Время разблокировки ящика (Сек) | Box unlocking time (Sec)")]
                public int unBlockTime = 900;
                [JsonProperty("Макcимальное количество предметов в ящике | Maximum number of items in a drawer")]
                public int maxItemCount = 10;
                [JsonProperty("умножать количество лута на количество дней с начала вайпа (на 3й день - лута будет в 3 раза больше) | multiply the amount of loot by the number of days since the start of the wipe (on the 3rd day - there will be 3 times more loot)")]
                public bool lootWipePlus = false;
                [JsonProperty("Включить сигнализацию ? | Turn on the alarm?")]
                public bool signaling = true;
                [JsonProperty("Использовать AlphaLoot для заполнения ящика ? | Use AlphaLoot to fill the box?")]
                public bool AlphaLootUse = false;
                [JsonProperty("Использовать EcoLootUI для заполнения ящика ? | Use EcoLootUI to fill the box?")]
                public bool EcoLootUIUse = false;
            }
            internal class NotiferSettings
            {
                [JsonProperty("ВебХук дискорда (Если не нужны уведомления в дискорд, оставьте поле пустым) | Discord WebHook (If you do not need discord notifications, leave the field blank)")]
                public string weebHook = string.Empty;
                [JsonProperty("Включить UI Уведомления ? | Enable UI Notifications?")]
                public bool useUiNotifi = true;
                [JsonProperty("Скрывать автоматически UI уведомления? | Auto hide UI notifications?")]
                public bool hideUiNotifi = true;
                [JsonProperty("Через сколько после показа будет скрываться? (сек) | How long after the show will it hide? (sec)")]
                public float hideUiNotifiTime = 15f;
                [JsonProperty("Цвет заднего фона окна UI | UI window background color")]
                public string colorBackground = "0.8 0.28 0.2 0.8";
                [JsonProperty("Цвет Кнопки закрытия UI | UI Close Button Color")]
                public string colorBtnCloseUi = "0.6784314 0.254902 0.1843137 0.8";
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                    throw new Exception();
                SaveConfig();
            }
            catch
            {
                for (var i = 0; i < 3; i++)
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                LoadDefaultConfig();
            }
            ValidateConfig();
            SaveConfig();
        }

        private void ValidateConfig()
        {
            if (config.npcController.nameNPC.Count == 0)
            {
                config.npcController.nameNPC = new List<string> { "Cobalt guard", "Cobalt defense" };
            }
            if (config.npcController.wearNpc.Count == 0)
            {
                config.npcController.wearNpc = new List<Configuration.NpcController.ItemNpc>
                {
                    new Configuration.NpcController.ItemNpc
                    {
                        Shortname = "roadsign.kilt",
                        SkinID = 1121447954
                    },
                    new Configuration.NpcController.ItemNpc
                    {
                        Shortname = "burlap.shirt",
                        SkinID = 2076298726
                    },
                    new Configuration.NpcController.ItemNpc
                    {
                        Shortname = "shoes.boots",
                        SkinID = 0
                    },
                    new Configuration.NpcController.ItemNpc
                    {
                        Shortname = "roadsign.gloves",
                        SkinID = 0
                    },
                    new Configuration.NpcController.ItemNpc
                    {
                        Shortname = "burlap.trousers",
                        SkinID = 2076292007
                    },
                    new Configuration.NpcController.ItemNpc
                    {
                        Shortname = "metal.facemask",
                        SkinID = 835028125
                    },
                };
            }
            if (config.npcController.beltNpc.Count == 0)
            {
                config.npcController.beltNpc = new List<Configuration.NpcController.ItemNpc>
                {
                    new Configuration.NpcController.ItemNpc
                    {
                        Shortname = "rifle.lr300",
                        SkinID = 1975712725
                    },
                    new Configuration.NpcController.ItemNpc
                    {
                        Shortname = "rifle.lr300",
                        SkinID = 1837473292
                    },
                    new Configuration.NpcController.ItemNpc
                    {
                        Shortname = "pistol.semiauto",
                        SkinID = 1557105240
                    },
                    new Configuration.NpcController.ItemNpc
                    {
                        Shortname = "rifle.semiauto",
                        SkinID = 1845735432
                    },
                    new Configuration.NpcController.ItemNpc
                    {
                        Shortname = "rifle.ak",
                        SkinID = 1352726257
                    },
                };
            }
            if (config.npcController.lootNpcs.Count == 0)
            {
                config.npcController.lootNpcs = new List<LootNpcOrBox>
                {
                    new LootNpcOrBox{Shortname = "halloween.surgeonsuit", SkinID = 0, DisplayName = "", BluePrint = false, MinimalAmount = 1, MaximumAmount = 1, DropChance = 70 },
                    new LootNpcOrBox{Shortname = "metal.facemask", SkinID = 1886184322, DisplayName = "", BluePrint = false, MinimalAmount = 1, MaximumAmount = 1, DropChance = 20 },
                    new LootNpcOrBox{Shortname = "door.double.hinged.metal", SkinID = 191100000, DisplayName = "", BluePrint = false, MinimalAmount = 1, MaximumAmount = 2, DropChance = 60 },
                    new LootNpcOrBox{Shortname = "rifle.bolt", SkinID = 0, DisplayName = "", BluePrint = true, MinimalAmount = 1, MaximumAmount = 1, DropChance = 10 },
                    new LootNpcOrBox{Shortname = "rifle.lr300", SkinID = 0, DisplayName = "", BluePrint = false, MinimalAmount = 1, MaximumAmount = 1, DropChance = 15 },
                    new LootNpcOrBox{Shortname = "pistol.revolver", SkinID = 0, DisplayName = "", BluePrint = false, MinimalAmount = 1, MaximumAmount = 3, DropChance = 60 },
                    new LootNpcOrBox{Shortname = "supply.signal", SkinID = 0, DisplayName = "", BluePrint = false, MinimalAmount = 1, MaximumAmount = 3, DropChance = 20 },
                    new LootNpcOrBox{Shortname = "explosive.satchel", SkinID = 0, DisplayName = "", BluePrint = false, MinimalAmount = 1, MaximumAmount = 3, DropChance = 5 },
                    new LootNpcOrBox{Shortname = "grenade.smoke", SkinID = 0, DisplayName = "", BluePrint = false, MinimalAmount = 1, MaximumAmount = 20, DropChance = 45 },
                    new LootNpcOrBox{Shortname = "ammo.rifle", SkinID = 0, DisplayName = "", BluePrint = false, MinimalAmount = 50, MaximumAmount = 120, DropChance = 35 },
                    new LootNpcOrBox{Shortname = "scrap", SkinID = 0, DisplayName = "", BluePrint = false, MinimalAmount = 100, MaximumAmount = 500, DropChance = 20 },
                    new LootNpcOrBox{Shortname = "giantcandycanedecor", SkinID = 0, DisplayName = "Новый год", BluePrint = false, MinimalAmount = 1, MaximumAmount = 5, DropChance = 70 },
                };
            }
            if (config.boxSetting.lootBoxes.Count == 0)
            {
                config.boxSetting.lootBoxes = new List<LootNpcOrBox>
                {
                    new LootNpcOrBox
                    {
                        Shortname = "pistol.python",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 1,
                        MaximumAmount = 1,
                        DropChance = 60,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "multiplegrenadelauncher",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 1,
                        MaximumAmount = 1,
                        DropChance = 15,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "sulfur",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 500,
                        MaximumAmount = 800,
                        DropChance = 40,
                        wipeCheck = true
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "gunpowder",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 300,
                        MaximumAmount = 400,
                        DropChance = 10,
                        wipeCheck = true
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "door.hinged.toptier",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = true,
                        MinimalAmount = 1,
                        MaximumAmount = 1,
                        DropChance = 15,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "wall.external.high.ice",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 1,
                        MaximumAmount = 5,
                        DropChance = 75,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "ammo.rocket.basic",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 1,
                        MaximumAmount = 3,
                        DropChance = 25,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "ammo.grenadelauncher.smoke",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 3,
                        MaximumAmount = 10,
                        DropChance = 70,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "ammo.grenadelauncher.he",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 2,
                        MaximumAmount = 5,
                        DropChance = 10,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "metal.facemask",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = true,
                        MinimalAmount = 1,
                        MaximumAmount = 1,
                        DropChance = 15,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "metal.plate.torso",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = true,
                        MinimalAmount = 1,
                        MaximumAmount = 1,
                        DropChance = 10,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "clatter.helmet",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 1,
                        MaximumAmount = 1,
                        DropChance = 70,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "carburetor3",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 1,
                        MaximumAmount = 1,
                        DropChance = 20,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "crankshaft3",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 1,
                        MaximumAmount = 1,
                        DropChance = 10,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "techparts",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 5,
                        MaximumAmount = 15,
                        DropChance = 35,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "xmas.lightstring.advanced",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 30,
                        MaximumAmount = 70,
                        DropChance = 45,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "largemedkit",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 3,
                        MaximumAmount = 5,
                        DropChance = 70,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "largemedkit",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = true,
                        MinimalAmount = 3,
                        MaximumAmount = 5,
                        DropChance = 70,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "metal.fragments",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 1000,
                        MaximumAmount = 2000,
                        DropChance = 70,
                        wipeCheck = true
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "explosives",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 10,
                        MaximumAmount = 50,
                        DropChance = 30,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "autoturret",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 1,
                        MaximumAmount = 1,
                        DropChance = 60,
                        wipeCheck = false
                    },
                    new LootNpcOrBox
                    {
                        Shortname = "explosive.timed",
                        SkinID = 0UL,
                        DisplayName = "",
                        BluePrint = false,
                        MinimalAmount = 1,
                        MaximumAmount = 1,
                        DropChance = 5,
                        wipeCheck = true
                    },
                };
            }
            if (config.pasteSettings.pasteBuilds.Count == 0)
            {
                config.pasteSettings.pasteBuilds = new List<Configuration.BuildingPasteSettings.PasteBuild>
                {
                    new Configuration.BuildingPasteSettings.PasteBuild
                    {
                        housepath = "",
                        heightBuilding = 0,
                        useSpawnNPC = true,
                        countSpawnNpc = 8,
                        healthNPC = 170,
                        DistanceRange = 140,
                        Accuracy = 40,
                        helpBot = true,
                        helpCount = 4,
                        heliHelp = true,
                        ChanceHeliHelp = 50,
                        copterChance = 50,
                    }
                };
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }
        #endregion

        #region SpawnPoint

        #region CheckFlat
        private List<Vector3>[] patternPositionsAboveWater = new List<Vector3>[MaxRadius];
        private List<Vector3>[] patternPositionsUnderWater = new List<Vector3>[MaxRadius];

        private readonly Quaternion[] directions =
        {
            Quaternion.Euler(90, 0, 0),
            Quaternion.Euler(0, 0, 90),
            Quaternion.Euler(0, 0, 180)
        };

        private void FillPatterns()
        {
            Vector3[] startPositions = { new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(-1, 0, -1), new Vector3(1, 0, -1) };

            patternPositionsAboveWater[0] = new List<Vector3> { new Vector3(0, -1.0f, 0) };
            for (int loop = 1; loop < MaxRadius; loop++)
            {
                patternPositionsAboveWater[loop] = new List<Vector3>();

                for (int step = 0; step < loop * 2; step++)
                {
                    for (int pos = 0; pos < 4; pos++)
                    {
                        Vector3 sPos = startPositions[pos] * step;
                        for (int rot = 0; rot < 3; rot++)
                        {
                            Vector3 rPos = directions[rot] * sPos;
                            rPos.y = -1.0f;
                            patternPositionsAboveWater[loop].Add(rPos);
                        }
                    }
                }
            }

            for (int i = 0; i < patternPositionsAboveWater.Length; i++)
            {
                patternPositionsUnderWater[i] = new List<Vector3>();
                foreach (var vPos in patternPositionsAboveWater[i])
                {
                    var rPos = new Vector3(vPos.x, 1.0f, vPos.z);
                    patternPositionsUnderWater[i].Add(rPos);
                }
            }
        }

        [ConsoleCommand("isflat")]
        private void CmdIsFlat(ConsoleSystem.Arg arg)
        {
            Vector3 pPos = new Vector3(arg.Player().transform.position.x, TerrainMeta.HeightMap.GetHeight(arg.Player().transform.position), arg.Player().transform.position.z);
            var b = IsFlat(ref pPos);
            arg.Player().Teleport(pPos);
        }

        public bool IsFlat(ref Vector3 position)
        {
            List<Vector3>[] AboveWater = new List<Vector3>[MaxRadius];

            Array.Copy(patternPositionsAboveWater, AboveWater, patternPositionsAboveWater.Length);

            for (int i = 0; i < AboveWater.Length; i++)
            {
                for (int j = 0; j < AboveWater[i].Count; j++)
                {
                    Vector3 pPos = AboveWater[i][j];
                    Vector3 resultAbovePos = new Vector3(pPos.x + position.x, position.y + 1.0f, pPos.z + position.z);
                    Vector3 resultUnderPos = new Vector3(pPos.x + position.x, position.y - 1.0f, pPos.z + position.z);

                    if (resultAbovePos.y >= TerrainMeta.HeightMap.GetHeight(resultAbovePos) && resultUnderPos.y <= TerrainMeta.HeightMap.GetHeight(resultUnderPos))
                    {
                    }
                    else
                        return false;
                }
            }

            return true;
        }
        #endregion

        #region GenerateSpawnPoint

        public bool IsDistancePoint(Vector3 point)
        {
            bool result = busyPoints3D.Count(x => Vector3.Distance(point, x) < 20f) == 0;
            return result;
        }
        private void GenerateSpawnPoints()
        {
            for (int i = 0; i < 100; i++)
            {
                maxTry -= 1;
                Vector3 point3D = new Vector3();
                Vector2 point2D = new Vector3(UnityEngine.Random.Range(-TerrainMeta.Size.x / 2, TerrainMeta.Size.x / 2), UnityEngine.Random.Range(-TerrainMeta.Size.z / 2, TerrainMeta.Size.z / 2));

                point3D.x = point2D.x;
                point3D.z = point2D.y;
                point3D.y = TerrainMeta.HeightMap.GetHeight(point3D);

                if (!IsFlat(ref point3D))
                    continue;

                if (!Is3DPointValid(ref point3D))
                    continue;

                if (!IsDistancePoint(point3D))
                    continue;

                if (point3D != Vector3.zero)
                {
                    AcceptValue(ref point3D);
                }
            }
            if (maxTry > 0)
            {
                NextTick(() =>
                {
                    GenerateSpawnPoints();
                });
            }
            else
            {
                PrintWarning($"{busyPoints3D.Count} POINTS FOUND!");
                maxTry = 250000;
            }
        }
        private bool Is3DPointValid(ref Vector3 point)
        {
            List<BuildingPrivlidge> cupboards = new List<BuildingPrivlidge>();
            Vis.Entities(point, config.pasteSettings.radiusClear, cupboards);
            if (Physics.CheckSphere(point, config.pasteSettings.radiusClear, LayerMask.GetMask("Construction", "Default", "Deployed", "World", "Trigger", "Prevent Building")) || cupboards.Count > 0 || point.y < ConVar.Env.oceanlevel + 4f)
            {
                return false;
            }
            return true;
        }

        private void AcceptValue(ref Vector3 point)
        {
            busyPoints3D.Add(point);
        }
        #endregion

        #region GetPosition
        private void ClearArea(ref Vector3 pos)
        {
            List<ResourceEntity> ResourceList = new List<ResourceEntity>();
            Vis.Entities(pos, 15, ResourceList);
            foreach (var item in ResourceList)
                item?.KillMessage();
        }
        private object GetSpawnPoints()
        {
            if (busyPoints3D.ToList().Count <= 3)
            {
                PrintWarning(GetLang("XD_IVENT_CLCONTROLLER_THE_POINTS_ARE_ENDED"));
                busyPoints3D.Clear();
                GenerateSpawnPoints();
                GenerateIvent();
                return Vector3.zero;
            }

            Vector3 targetPos = busyPoints3D.ToList().GetRandom();
            if (targetPos == Vector3.zero)
            {
                busyPoints3D.Remove(targetPos);
                return GetSpawnPoints();
            }

            bool valid = Is3DPointValid(ref targetPos);

            if (!valid)
            {
                busyPoints3D.Remove(targetPos);
                return GetSpawnPoints();
            }
            busyPoints3D.Remove(targetPos);
            ClearArea(ref targetPos);
            return targetPos;
        }
        #endregion

        #endregion

        #region Hooks
        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            if (HouseCobaltLab.Contains(entity?.GetParentEntity()))
                HouseCobaltLab.Remove(entity.GetParentEntity());
        }

        object CanUILootSpawn(LootContainer container)
        {
            if (container == CrateEnt)
                return false;
            return null;
        }
        object CanPopulateLoot(LootContainer container)
        {
            if (container == CrateEnt)
                return false;
            return null;
        }
        void Unload()
        {
            foreach (BaseEntity iventEnt in HouseCobaltLab)
            {
                if (!iventEnt.IsDestroyed)
                    iventEnt?.Kill();
            }
            HouseCobaltLab.Clear();
            if (SpawnHouseTime != null)
                SpawnHouseTime.Destroy();
            if (RemoveHouseTime != null)
                RemoveHouseTime.Destroy();
            DestroyZone();
            RemoveMapMarker();
            Cui.DestroyAllPlayer();
        }
        void Init() 
        {
            UnscribeHook();
            Unsubscribe("CanPopulateLoot");
            Unsubscribe("CanUILootSpawn");       
        }

        private void OnServerInitialized()
        {
            if (!CopyPaste)
            {
                NextTick(() => {
                    PrintError("Check if you have the 'Copy Paste'plugin installed");
                    Interface.Oxide.UnloadPlugin(Name);
                });
                return;
            }
            else if (CopyPaste.Version < new VersionNumber(4, 1, 27))
            {
                NextTick(() => {
                    PrintError("You have an old version of Copy Paste!\nplease update the plugin to the latest version (4.1.27 or higher) - https://umod.org/plugins/copy-paste");
                    Interface.Oxide.UnloadPlugin(Name);
                });
                return;
            }
            _ = this;
            FillPatterns();
            NextTick(() =>
            {
                GenerateSpawnPoints();
            });
            GenerateIvent();
            LoadDataCopyPaste();
            if(!config.boxSetting.AlphaLootUse)
                Subscribe("CanPopulateLoot");
            if (!config.boxSetting.EcoLootUIUse)
                Subscribe("CanUILootSpawn");
        }

        private void CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (crate == CrateEnt)
            {
                if (RemoveHouseTime != null)
                    RemoveHouseTime.Destroy();
                if (config.boxSetting.signaling)
                {
                    var Alarm = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/alarms/audioalarm.prefab", crate.transform.position, default(Quaternion), true);
                    Alarm.Spawn();
                    Alarm.SetFlag(BaseEntity.Flags.Reserved8, true);
                    Alarm.gameObject.Identity();
                    Alarm.SetParent(crate);

                    var Light = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/lights/sirenlight/electric.sirenlight.deployed.prefab", crate.transform.position, Quaternion.identity, false);
                    Light.enableSaving = true;
                    Light.Spawn();
                    Light.SetParent(crate);
                    Light.transform.localPosition = new Vector3(0.4f, 1.45f, -0.3f);
                    Light.transform.hasChanged = true;
                    Light.SendNetworkUpdate();

                    Light.SetFlag(BaseEntity.Flags.Reserved8, true);
                }
                SendChatAll(labIndex.helpBot ? "XD_IVENT_CRATEHACKHELP" : "XD_IVENT_CRATEHACK", player.displayName, PosIvent);
                if (labIndex.helpBot)
                    SpawnBots(crate, true);
                if (labIndex.heliHelp && random.Next(0, 100) >= (100 - labIndex.ChanceHeliHelp))
                    CallHeliForPlayer(player);

            }
        }
        void OnCrateHackEnd(HackableLockedCrate crate)
        {
            if (crate == CrateEnt)
            {
                if (RemoveHouseTime != null)
                    RemoveHouseTime.Destroy();
                SendChatAll("XD_IVENT_CRATEHACKEND", (config.iventController.timeRemoveHouse2 / 60));
                RemoveHouseTime = timer.Once(config.iventController.timeRemoveHouse2, () =>
                {
                    SendChatAll("XD_IVENT_CRATELOOTFOUND");
                    StopIvent();
                });
            }
        }
        void CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container is HackableLockedCrate && container == CrateEnt)
            {
                SendChatAll("XD_IVENT_CRATELOOTPLAYER", player.displayName);
                if (RemoveHouseTime != null)
                    RemoveHouseTime.Destroy();
                RemoveHouseTime = timer.Once(300, () =>
                {
                    StopIvent();
                });
                Unsubscribe("CanLootEntity");
            }
        }
        void OnCorpsePopulate(Scientist npc, NPCPlayerCorpse corpse)
        {
            if (npc?.GetComponent<NPCMonitor>() != null && corpse != null)
            {
                if (config.npcController.useCustomLoot && config.npcController.lootNpcs?.Count > 0 && corpse.containers[0]?.itemList != null)
                {
                    corpse.containers[0]?.itemList?.Clear();
                    for (int i = 0; i < config.npcController.lootNpcs.Count; i++)
                    {
                        var main = config.npcController.lootNpcs[i];
                        if (corpse.containers[0].IsFull())
                            break;
                        bool goodChance = random.Next(0, 100) >= (100 - main.DropChance);
                        if (goodChance)
                        {
                            if (main.BluePrint)
                            {
                                Item item = ItemManager.CreateByItemID(-996920608, 1, 0);
                                item.blueprintTarget = ItemManager.FindItemDefinition(main.Shortname).itemid;
                                if (!item.MoveToContainer(corpse.containers[0]))
                                    item.Remove();
                            }
                            else
                            {
                                Item item = ItemManager.CreateByName(main.Shortname, random.Next(main.MinimalAmount, main.MaximumAmount), main.SkinID);
                                if (!string.IsNullOrEmpty(main.DisplayName))
                                {
                                    item.name = main.DisplayName;
                                }
                                if (!item.MoveToContainer(corpse.containers[0]))
                                    item.Remove();
                            }
                        }
                    }
                    corpse.containers[0].capacity = corpse.containers[0].itemList.Count;
                    corpse.containers[1].capacity = 0;
                    corpse.containers[2].capacity = 0;
                    corpse.containers[0].MarkDirty();
                    corpse.SendNetworkUpdate();
                }
            }
        }


        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null)
                return;

            if (entity?.OwnerID == 342968945867)
            {
                hitInfo.damageTypes.ScaleAll(0);
            }

            var attacker = hitInfo.InitiatorPlayer;

            if (attacker.IsValid() && attacker is Scientist && (attacker as NPCPlayerApex).OwnerID == 3566257 && labIndex.Accuracy < UnityEngine.Random.Range(0f, 100f))
            {
                hitInfo.damageTypes = new DamageTypeList();
                hitInfo.DidHit = false;
                hitInfo.DoHitEffects = false;
                hitInfo.HitEntity = null;
                return;
            }
        }
        #endregion

        #region MetodsPasteBuild
        void GenerateBuilding(Configuration.BuildingPasteSettings.PasteBuild build)
        {
            string[] options = { "stability", "false", "deployables", "true", "autoheight", "false", "height", "1.5", "entityowner", "false" };
            Vector3 resultVector = (Vector3)GetSpawnPoints();
            if (resultVector == null || resultVector == Vector3.zero)
                return;
            var success = CopyPaste.Call("TryPasteFromVector3", new Vector3(resultVector.x, resultVector.y + build.heightBuilding, resultVector.z), 0f, !string.IsNullOrWhiteSpace(build.housepath) ? build.housepath : "HouseCobalt", options);

            if (success is string)
            {
                PrintWarning(GetLang("XD_IVENT_CLCONTROLLER_BUILDING_ERROR"));
                GenerateIvent();
                return;
            }
        }
        #region CarSetup
        private void MaybeAddFuel(ModularCar car)
        {
            var fuelAmount = 100;
            if (fuelAmount == 0)
                return;

            var fuelContainer = car.fuelSystem.GetFuelContainer();
            if (fuelAmount < 0)
                fuelAmount = fuelContainer.allowedItem.stackable;

            var fuelItem = fuelContainer.inventory.FindItemByItemID(fuelContainer.allowedItem.itemid);
            if (fuelItem == null)
                fuelContainer.inventory.AddItem(fuelContainer.allowedItem, fuelAmount);
        }
        private void MaybeAddEngineParts(ModularCar car)
        {

            foreach (var module in car.AttachedModuleEntities)
            {
                var engineModule = module as VehicleModuleEngine;
                if (engineModule != null)
                {
                    var engineStorage = engineModule.GetContainer() as EngineStorage;
                    if (engineStorage != null)
                    {
                        AddPartsToEngineStorage(engineStorage);
                        engineModule.RefreshPerformanceStats(engineStorage);
                    }
                }
            }
        }
        private void AddPartsToEngineStorage(EngineStorage engineStorage)
        {
            if (engineStorage.inventory == null)
                return;

            var inventory = engineStorage.inventory;
            for (var i = 0; i < inventory.capacity; i++)
            {
                var item = inventory.GetSlot(i);
                if (item != null)
                    continue;

                var tier = 2;
                if (tier > 0)
                    TryAddEngineItem(engineStorage, i, tier);
            }
        }

        private bool TryAddEngineItem(EngineStorage engineStorage, int slot, int tier)
        {
            ItemModEngineItem output;
            if (!engineStorage.allEngineItems.TryGetItem(tier, engineStorage.slotTypes[slot], out output))
                return false;

            var component = output.GetComponent<ItemDefinition>();
            var item = ItemManager.Create(component);
            if (item == null)
                return false;

            item.conditionNormalized = 1.0f;
            item.MoveToContainer(engineStorage.inventory, slot, allowStack: false);
            return true;
        }
        #endregion

        #region Helicopter

        private void CallHeliForPlayer(BasePlayer player)
        {
            #region RandomSpawnPosition
            float x = TerrainMeta.Size.x;
            float y = 70f;
            Vector3 val = Vector3Ex.Range(-1f, 1f);
            val.y = 0f;
            val.Normalize();
            val *= x * 1f;
            val.y = y;
            #endregion

            BaseHelicopter heli = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", val, new Quaternion(), true) as BaseHelicopter;
            if (!heli)
                return;
            heli.Spawn();
            var heliAi = heli.GetComponent<PatrolHelicopterAI>();
            heliAi._targetList.Add(new PatrolHelicopterAI.targetinfo((BaseEntity)player, player));
            heliAi.State_Move_Enter(player.transform.position + new Vector3(0.0f, 30f, 0.0f));
            heliAi.State_Strafe_Enter(player.transform.position);
            timer.Once(15f, () =>
            {
                SendChatAll("XD_IVENT_CLCONTROLLER_HELI_HELP");
            });

            timer.Once(360f, () => { 
                if(!heliAi.isDead)
                    heliAi.Retire();
            });         
        }

        #endregion

        void OnPasteFinished(List<BaseEntity> pastedEntities, string fileName)
        {
            if (fileName != "HouseCobalt" && !config.pasteSettings.pasteBuilds.Where(x => x.housepath == fileName).Any())
               return;

            HouseCobaltLab = pastedEntities;
            BaseEntity box = null;
            List<CCTV_RC> cam = new List<CCTV_RC>();
            ComputerStation comp = null;
            foreach (BaseEntity ent in HouseCobaltLab)
            {
                if (ent is BaseCombatEntity)
                {
                    (ent as BaseCombatEntity).pickup.enabled = false;
                }
                if (ent is ModularCar)
                {
                    var car = (ent as ModularCar);
                    foreach (var item in car.AttachedModuleEntities)
                    {
                        item.health = item.MaxHealth();
                    }
                    MaybeAddEngineParts(car);
                    MaybeAddFuel(car);
                    continue;
                }
                if (ent is MiniCopter)
                {
                    if (random.Next(0, 100) >= (100 - labIndex.copterChance))
                    {
                        MiniCopter copter = (ent as MiniCopter);
                        copter.fuelSystem.AddStartingFuel(50);
                        copter.transform.position = new Vector3(copter.transform.position.x, copter.transform.position.y, copter.transform.position.z);
                    }
                    else
                    {
                        NextTick(() =>
                        {
                            HouseCobaltLab.Remove(ent);
                            ent.Kill();
                        });
                    }
                        
                    continue;
                }
                ent.OwnerID = 342968945867;
                if (Light.Contains(ent.prefabID))
                {
                    ent.SetFlag(BaseEntity.Flags.On, true);
                }
                if (ent.skinID == 22848)
                {
                    var CrateEnt = GameManager.server.CreateEntity("assets/prefabs/io/electric/lights/sirenlightorange.prefab", ent.transform.position, ent.transform.rotation, true);
                    CrateEnt.Spawn();
                    if (CrateEnt is IOEntity)
                    {
                        CrateEnt.SetFlag(BaseEntity.Flags.Reserved8, true);
                        CrateEnt.SetFlag(BaseEntity.Flags.On, true);
                    }
                    NextTick(() =>
                    {
                        HouseCobaltLab.Remove(ent);
                        ent.Kill();
                        HouseCobaltLab.Add(CrateEnt);

                    });
                }
                if (ent is Signage)
                {
                    var ents = ent as Signage;
                    if (ents == null)
                        continue;
                    ents?.SetFlag(BaseEntity.Flags.Locked, true);
                    ents?.SetFlag(BaseEntity.Flags.Busy, true);
                    ents.SendNetworkUpdate(global::BasePlayer.NetworkQueue.Update);
                }              
                if (ent is Workbench || ent is ResearchTable || ent is MixingTable || ent is BaseArcadeMachine
                || ent is IOEntity || ent is ComputerStation || ent is CCTV_RC)
                {
                    if (ent is IOEntity)
                    {
                        ent.SetFlag(BaseEntity.Flags.Reserved8, true);
                        ent.SetFlag(BaseEntity.Flags.On, true);
                    }
                    if (ent is ComputerStation)
                        comp = ent as ComputerStation;
                    if (ent is CCTV_RC)
                    {
                        CCTV_RC cams = ent as CCTV_RC;
                        cams.UpdateIdentifier((cam.Count + 1).ToString());
                        cam.Add(cams);
                    }
                    continue;
                }
                if (ent is VendingMachine)
                {
                    var ents = ent as VendingMachine;
                    if (ents == null)
                        continue;
                    ents.SetFlag(BaseEntity.Flags.Reserved4, false);
                    ents.UpdateMapMarker();
                }
                if (ent is FogMachine)
                {
                    var ents = ent as FogMachine;
                    if (ents == null)
                        continue;
                    ents.SetFlag(BaseEntity.Flags.Reserved8, true);
                    ents.SetFlag(BaseEntity.Flags.Reserved7, false);
                    ents.SetFlag(BaseEntity.Flags.Reserved6, false);
                }
                if (ent.prefabID == 2206646561)
                {
                    box = CrateHackableLocked(ent);
                    NextTick(() =>
                    {
                        HouseCobaltLab.Remove(ent);
                        ent.Kill();
                        HouseCobaltLab.Add(box);

                    });
                    continue;
                }
                if (ent is Door)
                {
                    var ents = ent as Door;
                    if (ents == null)
                        continue;
                    ents.pickup.enabled = false;
                    ents.canTakeLock = false;
                    ents.canTakeCloser = false;
                    continue;
                }
                if (ent is ElectricGenerator)
                {
                    (ent as ElectricGenerator).electricAmount = 400;
                }
                if (ent as BuildingBlock)
                {
                    var build = ent as BuildingBlock;
                    build?.SetFlag(BaseEntity.Flags.Reserved1, false);
                    build?.SetFlag(BaseEntity.Flags.Reserved2, false);
                }
                if (ent is DecayEntity)
                {
                    (ent as DecayEntity).decay = null;
                    (ent as DecayEntity).upkeepTimer = float.MinValue;
                }
                ent?.SetFlag(BaseEntity.Flags.Busy, true);
                ent?.SetFlag(BaseEntity.Flags.Locked, true);
            }
            if (comp != null && cam.Count > 0)
            {
                foreach (CCTV_RC sd in cam)
                    comp.controlBookmarks.Add(sd.GetIdentifier(), sd.net.ID);
            }
            if (box == null)
            {
                PrintError(GetLang("XD_IVENT_CLCONTROLLER_NOT_BUILDING_BOX"));
                StopIvent();
                GenerateIvent();
                return;
            }
            npcZones = new GameObject().AddComponent<NpcZones>();
            npcZones.Activate(box, 15, config.radiationConroller.radUse);
            if (labIndex.useSpawnNPC)
                SpawnBots(box);
            PosIvent = GetGridString(box.transform.position);
            GenerateMapMarker(box.transform.position);
            SendChatAll("XD_IVENT_START", PosIvent);
            if (config.notiferSettings.useUiNotifi)
                Cui.CreateUIAllPlayer();

            RemoveHouseTime = timer.Once(config.iventController.timeRemoveHouse, () =>
            {
                SendChatAll("XD_IVENT_NOPLAYER");
                StopIvent();
            });
        }

        private void GenerateMapMarker(Vector3 pos)
        {
            if (config.mapMarkers.rustMapUse)
                RustMap?.Call("ApiAddPointUrl", config.mapMarkers.rustMapIcon, Title, pos, config.mapMarkers.rustMapTxt);

            if (config.mapMarkers.MapUse)
                Interface.CallHook("API_CreateMarker", pos, "xdcobaltlab", 0, 3f, 0.3f, config.mapMarkers.MapTxt, "f3ecad", "ff3535");
        }

        private void RemoveMapMarker()
        {
            if (config.mapMarkers.rustMapUse)
                RustMap?.Call("ApiRemovePointUrl", Title);
            if (config.mapMarkers.MapUse)
                Interface.CallHook("API_RemoveMarker", "xdcobaltlab");
        }
        #endregion

        #region MainMetods
        private void GenerateIvent()
        {
            if (RemoveHouseTime != null)
                RemoveHouseTime.Destroy();
            if (SpawnHouseTime != null)
                SpawnHouseTime.Destroy();
            SpawnHouseTime = timer.Once(GenerateSpawnIventTime(), () =>
            {
                StartIvent();
            });
        }
        private BaseEntity CrateHackableLocked(BaseEntity box)
        {
            CrateEnt = GameManager.server.CreateEntity("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", new Vector3(box.transform.position.x, box.transform.position.y + 1f, box.transform.position.z), box.transform.rotation, true) as HackableLockedCrate;
            CrateEnt.enableSaving = false;
            CrateEnt.Spawn();

            CrateEnt.inventory.itemList.Clear();
            for (int i = 0; i < config.boxSetting.lootBoxes.Count; i++)
            {
                if (CrateEnt.inventory.itemList.Count >= config.boxSetting.maxItemCount)
                    break;
                var cfg = config.boxSetting.lootBoxes[i];
                bool goodChance = UnityEngine.Random.Range(0, 100) >= (100 - cfg.DropChance);

                if (goodChance)
                {
                    if (cfg.BluePrint)
                    {
                        Item bp = ItemManager.CreateByItemID(-996920608, 1, 0);
                        bp.blueprintTarget = ItemManager.FindItemDefinition(cfg.Shortname).itemid;
                        if (!bp.MoveToContainer(CrateEnt.inventory))
                            bp.Remove();
                    }
                    else
                    {
                        int s = random.Next(cfg.MinimalAmount, cfg.MaximumAmount);
                        if (config.boxSetting.lootWipePlus && cfg.wipeCheck)
                            s = s * SaveCreated;

                        Item GiveItem = ItemManager.CreateByName(cfg.Shortname, s, cfg.SkinID);
                        if (!string.IsNullOrEmpty(cfg.DisplayName))
                        {
                            GiveItem.name = cfg.DisplayName;
                        }
                        if (!GiveItem.MoveToContainer(CrateEnt.inventory))
                            GiveItem.Remove();
                    }
                }
            }
            CrateEnt.inventory.capacity = CrateEnt.inventory.itemList.Count;
            CrateEnt.inventory.MarkDirty();
            CrateEnt.SendNetworkUpdate();
            CrateEnt.hackSeconds = HackableLockedCrate.requiredHackSeconds - config.boxSetting.unBlockTime;
            return CrateEnt;
        }

        private void StartIvent()
        {
            if (BasePlayer.activePlayerList.Count < config.iventController.minPlayedPlayers)
            {
                Puts(GetLang("XD_IVENT_CLCONTROLLER_NOT_ENOUGH_PLAYERS"));
                GenerateIvent();
                return;
            }
            if (RemoveHouseTime != null)
                RemoveHouseTime.Destroy();
            if (SpawnHouseTime != null)
                SpawnHouseTime.Destroy();
            SubscribeHook();
            labIndex = config.pasteSettings.pasteBuilds[0];
            if (config.pasteSettings.pasteBuilds.Count > 1)
            {
                labIndex = config.pasteSettings.pasteBuilds.GetRandom();
            }
            GenerateBuilding(labIndex);
        }

        private void StopIvent()
        {
            foreach (BaseEntity iventEnt in HouseCobaltLab)
                if (!iventEnt.IsDestroyed)
                    iventEnt?.Kill();
            if (config.notiferSettings.useUiNotifi)
                Cui.DestroyAllPlayer();
            HideUIUser.Clear();
            HouseCobaltLab.Clear();
            if (SpawnHouseTime != null)
                SpawnHouseTime.Destroy();
            if (RemoveHouseTime != null)
                RemoveHouseTime.Destroy();
            DestroyZone();
            UnscribeHook();
            RemoveMapMarker();
            GenerateIvent();
        }

        private void UnscribeHook()
        {
            Unsubscribe("OnEntityTakeDamage");
            Unsubscribe("OnCorpsePopulate");
            Unsubscribe("CanLootEntity");
            Unsubscribe("OnCrateHackEnd");
            Unsubscribe("CanHackCrate");
            Unsubscribe("OnEntityMounted");
        }
        private void SubscribeHook()
        {
            Subscribe("OnEntityTakeDamage");
            Subscribe("OnCorpsePopulate");
            Subscribe("CanLootEntity");
            Subscribe("OnCrateHackEnd");
            Subscribe("CanHackCrate");
            Subscribe("OnEntityMounted");
        }


        #region Method controller npc
        Vector3 RandomCircle(Vector3 center, float radius = 2)
        {
            float ang = UnityEngine.Random.value * 360;
            Vector3 pos;
            pos.x = center.x + radius * Mathf.Sin(ang * Mathf.Deg2Rad);
            pos.z = center.z + radius * Mathf.Cos(ang * Mathf.Deg2Rad);
            pos.y = center.y;
            pos.y = GetGroundPosition(pos);

            return pos;
        }

        static float GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            RaycastHit hit;

            if (Physics.Raycast(new Vector3(pos.x, pos.y + 200f, pos.z), Vector3.down, out hit, Mathf.Infinity, LayerMask.GetMask(new[] { "Terrain", "World", "Default", "Construction", "Deployed" })) && !hit.collider.name.Contains("rock_cliff"))
                return Mathf.Max(hit.point.y, y);
            return y;
        }

        private NPCPlayerApex InstantiateEntity(string type, Vector3 position)
        {
            position.y = GetGroundPosition(position);
            var gameObject = Instantiate.GameObject(GameManager.server.FindPrefab(type), position, new Quaternion());
            gameObject.name = type;
            SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);
            UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            NPCPlayerApex component = gameObject.GetComponent<NPCPlayerApex>();
            return component;
        }
        void SpawnBots(BaseEntity box, bool help = false)
        {
            int count = labIndex.countSpawnNpc;
            if (help && labIndex.helpBot)
                if (labIndex.helpCount > 0)
                    count = labIndex.helpCount;

            for (int i = 0; i < count; i++)
            {
                NPCPlayerApex entity = null;
                entity = InstantiateEntity("assets/prefabs/npc/scientist/scientist.prefab", RandomCircle(box.transform.position, 10));
                entity.enableSaving = false;
                entity.Spawn();
                entity.OwnerID = 3566257;
                entity.IsInvinsible = false;
                entity.startHealth = labIndex.healthNPC;
                entity.InitializeHealth(entity.startHealth, entity.startHealth);
                ControllerInventory(entity);
                entity.Stats.AggressionRange = entity.Stats.DeaggroRange = labIndex.DistanceRange;
                entity.CommunicationRadius = 0;
                entity.displayName = config.npcController.nameNPC.GetRandom();
                entity.GetComponent<Scientist>().LootPanelName = entity.displayName;
                entity.CancelInvoke(entity.EquipTest);
                Equip(entity);
                entity.Stats.MaxRoamRange = 75f;
                entity.NeverMove = true;

                NPCMonitor npcMonitor = entity.gameObject.AddComponent<NPCMonitor>();
                nPCMonitors.Add(npcMonitor);
                
                npcMonitor.Initialize(box);
            }
        }

        public HeldEntity GetFirstWeapon(BasePlayer player)
        {
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item.CanBeHeld() && (item.info.category == ItemCategory.Weapon))
                {
                    BaseProjectile projectile = item.GetHeldEntity() as BaseProjectile;
                    if (projectile != null)
                    {
                        global::Item items = projectile?.GetItem();
                        if (item != null && items.contents != null)
                        {
                            if (UnityEngine.Random.Range(0, 2) == 0)
                            {
                                global::Item item2 = global::ItemManager.CreateByName("weapon.mod.flashlight", 1, 0UL);
                                if (!item2.MoveToContainer(items.contents, -1, true))
                                    item2.Remove(0f);
                            }
                            else
                            {
                                global::Item item3 = global::ItemManager.CreateByName("weapon.mod.lasersight", 1, 0UL);
                                if (!item3.MoveToContainer(items.contents, -1, true))
                                    item3.Remove(0f);
                            }
                        }
                        projectile.primaryMagazine.contents = projectile.primaryMagazine.capacity;
                        projectile.SendNetworkUpdateImmediate();
                        return item.GetHeldEntity() as HeldEntity;
                    }
                }
            }
            return null;
        }
        private void Equip(BasePlayer player)
        {
            HeldEntity weapon = GetFirstWeapon(player);
            if (weapon != null)
            {
                weapon.SetHeld(true);
                weapon.SetLightsOn(false);
            }
        }

        private void ControllerInventory(BasePlayer player)
        {
            if (player == null)
                return;
            player.inventory.Strip();
            if (config?.npcController?.beltNpc?.Count > 0)
            {
                var index = random.Next(0, config.npcController.beltNpc.Count);
                ItemManager.CreateByName(config.npcController.beltNpc[index].Shortname, 1, config.npcController.beltNpc[index].SkinID).MoveToContainer(player.inventory.containerBelt);
            }
            if (config?.npcController?.wearNpc?.Count > 0)
            {
                for (int i = 0; i < config.npcController.wearNpc.Count; i++)
                {
                    if (player.inventory.containerWear.IsFull())
                        break;

                    var wear = config.npcController.wearNpc[i];
                    ItemManager.CreateByName(wear.Shortname, 1, wear.SkinID).MoveToContainer(player.inventory.containerWear);
                }
            }
        }
        public class NPCMonitor : FacepunchBehaviour
        {
            public NPCPlayerApex player
            {
                get; private set;
            }
            private List<Vector3> patrolPositions = new List<Vector3>();
            private Vector3 homePosition;
            private int lastPatrolIndex = 0;
            private void Awake()
            {
                player = GetComponent<NPCPlayerApex>();
                InvokeRepeating(UpdateDestination, 0f, 3.0f);
                checkNight();
                InvokeRandomized(new Action(checkNight), 0f, 30f, 5f);
            }
            private void checkNight()
            {
                HeldEntity heldEntity1 = player.GetActiveItem()?.GetHeldEntity() as HeldEntity;
                if (heldEntity1 != null)
                    heldEntity1.SetLightsOn(TOD_Sky.Instance.IsNight ? true : false);
            }

            public void Initialize(BaseEntity box)
            {
                this.homePosition = box.transform.position;
                GeneratePatrolPositions();
            }
            private void UpdateDestination()
            {
                if (player.AttackTarget == null)
                {
                    player.NeverMove = true;
                    float distance = (player.transform.position - homePosition).magnitude;
                    bool tooFar = distance > 10;

                    if (player.GetNavAgent == null || !player.GetNavAgent.isOnNavMesh)
                        player.finalDestination = patrolPositions[lastPatrolIndex];
                    else
                    {
                        if (Vector3.Distance(player.transform.position, patrolPositions[lastPatrolIndex]) < 5)
                            lastPatrolIndex++;
                        if (lastPatrolIndex >= patrolPositions.Count)
                            lastPatrolIndex = 0;
                        player.SetDestination(patrolPositions[lastPatrolIndex]);
                    }

                    player.SetDestination(patrolPositions[lastPatrolIndex]);
                    player.SetFact(NPCPlayerApex.Facts.Speed, tooFar ? (byte)NPCPlayerApex.SpeedEnum.Run : (byte)NPCPlayerApex.SpeedEnum.Walk, true, true);
                }
                else
                {
                    player.NeverMove = false;
                    player.IsStopped = false;

                    var attacker = player.AttackTarget as BasePlayer;
                    if (attacker == null)
                        return;

                    if (attacker.IsDead())
                        Forget();
                }
            }
            private void Forget()
            {
                player.lastDealtDamageTime = Time.time;
                player.AttackTarget = null;
                player.lastAttacker = null;
                player.lastAttackedTime = Time.time;
                player.LastAttackedDir = Vector3.zero;
                player.SetFact(Facts.HasEnemy, 0, true, true);
                player.SetFact(Facts.EnemyRange, 3, true, true);
                player.SetFact(Facts.AfraidRange, 1, true, true);
                player.SetFact(Facts.IsAggro, 0, true, true);
                player.SetFact(Facts.IsAfraid, 0, true, true);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
            }
            private void OnDestroy()
            {
                if (player != null && !player.IsDestroyed)
                    player.Kill();
                Destroy(gameObject);
            }
            private void GeneratePatrolPositions()
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector3 position = homePosition + (UnityEngine.Random.onUnitSphere * 20f);
                    position.y = TerrainMeta.HeightMap.GetHeight(position);
                    patrolPositions.Add(position);
                }
                enabled = true;
            }
        }
        #region NpcZonesOrRadiation
        public class NpcZones : MonoBehaviour
        {
            private Vector3 Position;
            private float Radius;
            private TriggerRadiation Radiation;

            private void Awake()
            {
                gameObject.layer = (int)Rust.Layer.Reserved1;
                gameObject.name = "NpcZonesOrRadiation";
            }
            public void Activate(BaseEntity box, float radius, bool rad)
            {
                Position = box.transform.position;
                Radius = radius;
                transform.position = Position;
                transform.rotation = new Quaternion();
                UpdateCollider();
                enabled = true;
                if (rad)
                {
                    Radiation = gameObject.AddComponent<TriggerRadiation>();
                    Radiation.RadiationAmountOverride = _.config.radiationConroller.radCount;
                    Radiation.interestLayers = LayerMask.GetMask("Player (Server)");
                    Radiation.enabled = true;
                }
                gameObject.SetActive(true);
                enabled = true;
            }

            private void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other.GetComponent<BasePlayer>();
                if (player != null && player.IsNpc == false && Radiation != null)
                {
                    if (Radiation.entityContents == null)
                        Radiation.entityContents = new HashSet<BaseEntity>();

                    Radiation.entityContents.Add(player);
                    player.EnterTrigger(Radiation);
                }
            }

            private void OnTriggerExit(Collider other)
            {
                BasePlayer player = other.GetComponent<BasePlayer>();
                if (player != null && player.IsNpc == false && Radiation != null)
                {
                    if (Radiation.entityContents != null)
                        Radiation.entityContents.Remove(player);

                    player.LeaveTrigger(Radiation);
                }
            }
            private void OnDestroy()
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    Radiation.entityContents.Remove(player);
                    player.LeaveTrigger(Radiation);
                }
                Destroy(gameObject);
            }

            private void UpdateCollider()
            {
                var sphereCollider = gameObject.GetComponent<SphereCollider>();
                {
                    if (sphereCollider == null)
                    {
                        sphereCollider = gameObject.AddComponent<SphereCollider>();
                        sphereCollider.isTrigger = true;
                    }
                    sphereCollider.radius = Radius;
                }
            }
        }
        #endregion
        #endregion

        #endregion

        #region HelpMetods

        #region Узнаем квадрат
        string GetGridString(Vector3 pos)
        {
            char[] alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

            pos.z = -pos.z;
            pos += new Vector3(TerrainMeta.Size.x, 0, TerrainMeta.Size.z) * .5f;

            var cubeSize = 146.14f;

            int xCube = (int)(pos.x / cubeSize);
            int zCube = (int)(pos.z / cubeSize);
           // int yNumber = skykey;
            int firstLetterIndex = (int)(xCube / alpha.Length) - 1;
            string firstLetter = "";
            if (firstLetterIndex >= 0)
                firstLetter = $"{alpha[firstLetterIndex]}";

            var xStr = $"{firstLetter}{alpha[xCube % 26]}";
            var zStr = $"{zCube}";

            return $"{xStr}{zStr}";
        }

        private string NumberToString(int number)
        {
            bool a = number > 25;
            Char c = (Char)(65 + (a ? number - 26 : number));
            return a ? "A" + c : c.ToString();
        }
        #endregion

        #region discord

        #region FancyDiscord
        public class FancyMessage
        {
            public string content
            {
                get; set;
            }
            public bool tts
            {
                get; set;
            }
            public Embeds[] embeds
            {
                get; set;
            }

            public class Embeds
            {
                public string title
                {
                    get; set;
                }
                public int color
                {
                    get; set;
                }
                public List<Fields> fields
                {
                    get; set;
                }

                public Embeds(string title, int color, List<Fields> fields)
                {
                    this.title = title;
                    this.color = color;
                    this.fields = fields;
                }
            }

            public FancyMessage(string content, bool tts, Embeds[] embeds)
            {
                this.content = content;
                this.tts = tts;
                this.embeds = embeds;
            }

            public string toJSON() => JsonConvert.SerializeObject(this);
        }

        public class Fields
        {
            public string name
            {
                get; set;
            }
            public string value
            {
                get; set;
            }
            public bool inline
            {
                get; set;
            }
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }

        private void Request(string url, string payload, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");
            webrequest.Enqueue(url, payload, (code, response) =>
            {
                if (code != 200 && code != 204)
                {
                    if (response != null)
                    {
                        try
                        {
                            JObject json = JObject.Parse(response);
                            if (code == 429)
                            {
                                float seconds = float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                            }
                            else
                            {
                                PrintWarning($" Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                            }
                        }
                        catch
                        {
                            PrintWarning($"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                        }
                    }
                    else
                    {
                        PrintWarning($"Discord didn't respond (down?) Code: {code}");
                    }
                }
                try
                {
                    callback?.Invoke(code);
                }
                catch (Exception ex) { }

            }, this, RequestMethod.POST, header);
        }
        #endregion

        void SendDiscordMsg(string msg)
        {
            List<Fields> fields = new List<Fields>
            {
                new Fields(lang.GetMessage("XD_IVENT_HOUSECOBALT", this), msg, true),
            };
            FancyMessage newMessage = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(null, 16775936, fields) });
            Request(config.notiferSettings.weebHook, newMessage.toJSON());
        }

        #endregion

        private static System.Random random = new System.Random();
        private int GenerateSpawnIventTime() => random.Next(config.iventController.minSpawnIvent, config.iventController.maxSpawnIvent);

        public static StringBuilder sb = new StringBuilder();
        public string GetLang(string LangKey, string userID = null, params object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }
        public void SendChatAll(string Message, params object[] args)
        {
            if (!String.IsNullOrEmpty(config.notiferSettings.weebHook))
            {
                string msg = GetLang(Message, null, args);
                SendDiscordMsg(GetLang(Message + "_DISCORD", null, args));
            }
            if (IQChat)
                IQChat?.Call("API_ALERT", GetLang(Message, null, args));
            else
                BasePlayer.activePlayerList.ToList().ForEach(p => p.SendConsoleCommand("chat.add", ConVar.Chat.ChatChannel.Global, 0, GetLang(Message, p.UserIDString, args)));
        }
        public void SendChatPlayer(string Message, BasePlayer player, ConVar.Chat.ChatChannel channel = ConVar.Chat.ChatChannel.Global)
        {
            if (IQChat)
                IQChat?.Call("API_ALERT_PLAYER", player, Message);
            else
                player.SendConsoleCommand("chat.add", channel, 0, Message);
        }
        private static string HexToRustFormat(string hex)
        {
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            sb.Clear();
            return sb.AppendFormat("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a).ToString();
        }
        private void DestroyZone()
        {
            UnityEngine.Object.Destroy(npcZones);

            foreach (NPCMonitor zones in nPCMonitors)
                    UnityEngine.Object.Destroy(zones);
        }
        #endregion

        #region Command
        [ConsoleCommand("HideUi")]
        void CMDHideUi(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (Player != null)
            {
                if (!HideUIUser.Contains(player.userID))
                {
                    HideUIUser.Add(player.userID);
                    CuiHelper.DestroyUi(player, "CobaltPanel");
                }
                else
                {
                    HideUIUser.Remove(player.userID);
                    Cui.MainUI(player);
                }
            }
        }
        [ChatCommand("cl")]
        void CLCommand(BasePlayer player, string cmd, string[] Args)
        {
            if (!player.IsAdmin)
                return;
            if (Args == null || Args.Length == 0)
            {
                SendChatPlayer(GetLang("XD_IVENT_CLCONTROLLER_SYNTAX", player.UserIDString), player);
                return;
            }
            switch (Args[0])
            {
                case "start":
                    {
                        if (SpawnHouseTime.Destroyed)
                        {
                            PrintToChat(player, GetLang("XD_IVENT_CLCONTROLLER_ACTIVE", player.UserIDString));
                        }
                        else
                        {
                            SpawnHouseTime.Destroy();
                            StartIvent();
                        }
                        break;
                    }
                case "stop":
                    {
                        if (SpawnHouseTime.Destroyed)
                        {
                            StopIvent();
                            SendChatAll(GetLang("XD_IVENT_CLCONTROLLER_STOPADMIN", player.UserIDString));
                        }
                        else
                        {

                            SendChatPlayer(GetLang("XD_IVENT_CLCONTROLLER_NOT_ACTIVE", player.UserIDString), player);
                        }
                        break;
                    }
            }

        }
        #endregion
       
        #region Command itemAddOrReset
        [ChatCommand("cl.items")]
        void BoxItemCommand(BasePlayer player, string cmd, string[] Args)
        {
            if (Args == null || Args.Length == 0)
            {
                SendChatPlayer(GetLang("XD_IVENT_CL_ITEM_SYNTAX", player.UserIDString), player);
                return;
            }
            switch (Args[0])
            {
                case "add":
                    {
                        foreach (var item in player.inventory.containerMain.itemList)
                        {
                            config.boxSetting.lootBoxes.Add(new LootNpcOrBox
                            {
                                BluePrint = item.IsBlueprint(),
                                Shortname = item.IsBlueprint() ? item.blueprintTargetDef.shortname : item.info.shortname,
                                SkinID = item.skin,
                                DisplayName = string.Empty,
                                DropChance = 30,
                                MinimalAmount = 1,
                                MaximumAmount = 1
                            });
                        }
                        SaveConfig();
                        SendChatPlayer(GetLang("XD_IVENT_CL_ITEM_ADDED", player.UserIDString), player);
                        break;
                    }
                case "reset":
                    {
                        config.boxSetting.lootBoxes.Clear();
                        foreach (var item in player.inventory.containerMain.itemList)
                        {
                            config.boxSetting.lootBoxes.Add(new LootNpcOrBox
                            {
                                BluePrint = item.IsBlueprint(),
                                Shortname = item.IsBlueprint() ? item.blueprintTargetDef.shortname : item.info.shortname,
                                SkinID = item.skin,
                                DisplayName = string.Empty,
                                DropChance = 30,
                                MinimalAmount = 1,
                                MaximumAmount = 1
                            });
                        }
                        SaveConfig();
                        SendChatPlayer(GetLang("XD_IVENT_CL_ITEM_REPLACED", player.UserIDString), player);
                        break;
                    }
            }
        }
        [ChatCommand("cl.botitems")]
        void NpcLootCommand(BasePlayer player, string cmd, string[] Args)
        {
            if (Args == null || Args.Length == 0)
            {
                SendChatPlayer(GetLang("XD_IVENT_CLBOT_ITEM_SYNTAX", player.UserIDString), player);
                return;
            }
            switch (Args[0])
            {
                case "add":
                    {
                        foreach (var item in player.inventory.containerMain.itemList)
                        {
                            config.npcController.lootNpcs.Add(new LootNpcOrBox
                            {
                                BluePrint = item.IsBlueprint(),
                                Shortname = item.IsBlueprint() ? item.blueprintTargetDef.shortname : item.info.shortname,
                                SkinID = item.skin,
                                DisplayName = string.Empty,
                                DropChance = 30,
                                MinimalAmount = 1,
                                MaximumAmount = 1
                            });
                        }
                        SaveConfig();
                        SendChatPlayer(GetLang("XD_IVENT_CLBOT_ITEM_ADDED", player.UserIDString), player);
                        break;
                    }
                case "reset":
                    {
                        config.npcController.lootNpcs.Clear();
                        foreach (var item in player.inventory.containerMain.itemList)
                        {
                            config.npcController.lootNpcs.Add(new LootNpcOrBox
                            {
                                BluePrint = item.IsBlueprint(),
                                Shortname = item.IsBlueprint() ? item.blueprintTargetDef.shortname : item.info.shortname,
                                SkinID = item.skin,
                                DisplayName = string.Empty,
                                DropChance = 30,
                                MinimalAmount = 1,
                                MaximumAmount = 1
                            });
                        }
                        SaveConfig();
                        SendChatPlayer(GetLang("XD_IVENT_CLBOT_ITEM_REPLACED", player.UserIDString), player);
                        break;
                    }
            }
        }
        #endregion

        #region ui
        public static class Cui
        {
            public static void CreateUIAllPlayer()
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    ButtonClose(player);
                    if (_.HideUIUser.Contains(player.userID))
                        continue;
                    MainUI(player);
                    if (_.config.notiferSettings.hideUiNotifi)
                    {
                        _.timer.Once(_.config.notiferSettings.hideUiNotifiTime, () => { player.SendConsoleCommand("HideUi"); });
                    }
                }       
            }

            public static void MainUI(BasePlayer player)
            {
                var container = new CuiElementContainer();
                container.Add(new CuiPanel
                {   
                    CursorEnabled = false,
                    Image = { Color = _.config.notiferSettings.colorBackground, FadeIn = 0.2f },
                    RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-342.195 -15.973", OffsetMax = "-13.805 59.667" }
                }, "Overlay", "CobaltPanel");

                container.Add(new CuiElement
                {
                    Name = "CobaltImg",
                    Parent = "CobaltPanel",
                    Components = {
                    new CuiRawImageComponent { Color = "0.9568628 0.7254902 0 1", Material = "assets/icons/iconmaterial.mat", Sprite = "assets/icons/radiation.png", FadeIn = 0.2f },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "6.5 -17.5", OffsetMax = "41.5 17.5" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "CobaltTitle",
                    Parent = "CobaltPanel",
                    Components = {
                    new CuiTextComponent { Text =  _.lang.GetMessage("XD_IVENT_HOUSECOBALT", _).ToUpper(), Font = "robotocondensed-bold.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FadeIn = 0.2f },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "0.5 0.5" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-103.801 -23.938", OffsetMax = "103.801 -2.861" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "CobaltInfo",
                    Parent = "CobaltPanel",
                    Components = {
                    new CuiTextComponent { Text = string.Format(_.lang.GetMessage("XD_IVENT_STARTUI", _), _.PosIvent), Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FadeIn = 0.2f },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-118.84 -33.077", OffsetMax = "151.44 13.881" }
                }
                });
                CuiHelper.AddUi(player, CuiHelper.ToJson(container));
            }

            public static void ButtonClose(BasePlayer player)
            {
                var container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = _.config.notiferSettings.colorBtnCloseUi },
                    RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-13.854 -15.973", OffsetMax = "0 59.667" }
                }, "Overlay", "CobaltClosePanel");

                container.Add(new CuiElement
                {
                    Name = "ButtonClodedUI",
                    Parent = "CobaltClosePanel",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Material = "assets/icons/iconmaterial.mat", Sprite = "assets/icons/chevron_right.png" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8.739 -11.998", OffsetMax = "8.74 11.998" }
                }
                });

                container.Add(new CuiButton
                {
                    Text = { Text = "" },
                    Button = { Command = "HideUi", Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8.739 -37.82", OffsetMax = "8.74 37.82" }
                }, "CobaltClosePanel", "Closed");

                CuiHelper.AddUi(player, CuiHelper.ToJson(container));
            }

            public static void DestroyAllPlayer()
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(Net.sv.connections), null, "DestroyUI", "CobaltClosePanel");
                CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(Net.sv.connections), null, "DestroyUI", "CobaltPanel");
            }
        }
        #endregion
    }
}