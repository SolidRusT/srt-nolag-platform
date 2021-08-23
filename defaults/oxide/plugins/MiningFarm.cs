using System;
using System.Collections.Generic;
 using System.Globalization;
 using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MiningFarm", "CASHR#6906", "2.1.0")]
    internal class MiningFarm : RustPlugin
    {
        #region Static
        
        [PluginReference] private Plugin Econom;
        private static MiningFarm _ins;
        private static UIContoller _uiContoller;
        private Configuration _config;
        private const string perm = "miningfarm.use";
        private string Layer = "UI_MININGFARM_MAINLAYER";

        private class ComponentSettings
        {
            [JsonProperty("Количество добавляемое к прогрессу || The amount added to the progress")]
            public float OnTick;

            [JsonProperty("Количество добавляемой температуры || The amount of added temperature")]
            public float TemperaturePlus;
            [JsonProperty("Количество отнимаемой прочности у предмета || The amount of strength taken away from the item")]
            public float MinusCondition;

            [JsonProperty("Максимальное количество прочности предмета || Maximum amount of item strength")]
            public float MaxCondition;

            [JsonProperty("ShortName предмета || Item ShortName")] public string ShortName;
        }

        private class CoolerSettings
        {
            [JsonProperty("Количество отнимаемой температуры || The amount of temperature taken away")]
            public float TemperaturePlus;
            [JsonProperty("Количество отнимаемой прочности у предмета || The amount of strength taken away from the item")]
            public float MinusCondition;

            [JsonProperty("Максимальное количество прочности предмета || Maximum amount of item strength")]
            public float MaxCondition;

            [JsonProperty("ShortName предмета || Item ShortName")] public string ShortName;
        }
        private class ItemSettings
        {
            [JsonProperty("Количество предмета || Item Amount")] public int amount;
            [JsonProperty("Шанс спавна предмета || Item Spawn chance")] public int Chance;
            [JsonProperty("Имя предмета || Item Display Name")] public string DisplayName;
            [JsonProperty("ShortName предмета || Item ShortName")] public string shortname;
            [JsonProperty("SkinID предмета || Item SkinID")] public ulong SkinID;
            [JsonProperty("Максимальное количество прочности предмета || Maximum amount of item strength")]
            public float MaxCondition;

        }
        #endregion

        #region Config

        private class Configuration
        {
            [JsonProperty("Включить если есть проблема со стакими || Enable if there are problems with the stack plugin")] public bool skinEnabled = false;
            [JsonProperty("Разрешить работу фермы если игрока нет в сети? || Allow the farm to work if the player is not online?")]
            public bool OfflineWork = false;
            [JsonProperty(PropertyName = "Настройка слотов для привилегий || Configuring Privilege Slots",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> PermissionList = new Dictionary<string, int>()
            {
                ["miningfarm.use"] = 16,
                ["miningfarm.vip"] = 32
            };

        [JsonProperty("Настройки выдачи награды || Reward Issue Settings")]
            public RewardSettings Reward = new RewardSettings()
            {
                Amount = 1000,
                cmd = false,
                command = "",
                DisplayName = "МОНЕТА",
                economics = false,
                PluginAmount = 0,
                PluginHook = "AddBalance",
                PluginName = "RustShop",
                ShortName = "glue",
                SkinID = 1984567801
            };


            [JsonProperty(PropertyName = "Настройка спавна предметов(шортнейм ящика - предметы) || Setting up the spawn of items(shortname of the box-items)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string,List<ItemSettings>> ItemList = new Dictionary<string,List<ItemSettings>>()
            {
                ["crate_elite"] = new List<ItemSettings>()
                {
                    new ItemSettings()
                    {
                        shortname = "fuse",
                        SkinID = 2319470516,
                        DisplayName = "Cooler",
                        amount = 1,
                        MaxCondition = 100,
                        Chance = 20
                    },
                    new ItemSettings()
                    {
                        shortname = "fuse",
                        SkinID = 2319472024,
                        DisplayName = "VIDEO CARD RTX 3090",
                        amount = 1,
                        MaxCondition = 100,
                        Chance = 20
                    },
                    new ItemSettings()
                    {
                        shortname = "fuse",
                        SkinID = 2366175126,
                        DisplayName = "RAM MEMORY",
                        amount = 1,
                        MaxCondition = 100,
                        Chance = 20
                    },
                    new ItemSettings()
                    {
                        shortname = "fuse",
                        SkinID = 2319473211,
                        DisplayName = "VIDEO CARD RTX 3070",
                        amount = 1,
                        MaxCondition = 100,
                        Chance = 20
                    }
                }
            };

            [JsonProperty("Значения температуры до взрыва компьютера || Temperature values before the computer explodes")]
            public float CriticatlTemperature = 150;
            [JsonProperty("Количество температуры добавляемое 1 тиком || The amount of temperature added by 1 tick")]
            public float TemperaturePlus = 2;
            [JsonProperty("Значение для получения награды || Value for the award")]
            public float Capacity = 1000;

            [JsonProperty("Сколько добавлять к прогрессу за 1 тик || How much to add to the progress for 1 tick")]
            public float OnTick = 1;

            [JsonProperty("Раз в сколько секунд проходит тик || Once in how many seconds does a tick pass")]
            public float TickCooldown = 10;

            [JsonProperty("Количество подаваемой энергии на лампу для майнинга || The amount of energy supplied per lamp for mining")]
            public float CurrentEnergy = 10;

            [JsonProperty(PropertyName = "Настройки компонентов для фермы || Farm Component Settings",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, ComponentSettings> ComponentList = new Dictionary<ulong, ComponentSettings>()
            {
                [2319472024] = new ComponentSettings()
                {
                    OnTick = 2,
                    MinusCondition = 2,
                    TemperaturePlus = 1,
                    MaxCondition = 100,
                    ShortName = "fuse"
                },
                [2319470516] = new ComponentSettings()
                {
                    OnTick = 2,
                    MinusCondition = 2,
                    MaxCondition = 100,
                    TemperaturePlus = 1,
                    ShortName = "fuse"
                },
                [2319473211] = new ComponentSettings()
                {
                    OnTick = 2,
                    MinusCondition = 2,
                    TemperaturePlus = 1,
                    MaxCondition = 100,
                    ShortName = "fuse"
                }
            };
            [JsonProperty(PropertyName = "Настройки охлаждающих компонентов для фермы || Farm Cooling Component Settings",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, CoolerSettings> CoollerList = new Dictionary<ulong, CoolerSettings>()
            {
                [2319470516] = new CoolerSettings()
                {
                    MinusCondition = 2,
                    MaxCondition = 100,
                    ShortName = "fuse"
                }
            };
            public class RewardSettings
            {
                [JsonProperty("Количество предмета || Item amount")] public int Amount;

                [JsonProperty("Это команда?(true - команда, false - предмет) || This is item?(true - yes)")]
                public bool cmd;

                [JsonProperty("Команда, которая должна выполняться(Пример: givemoney %STEAMID% кол-во) || The command to be executed" )]
                public string command;

                [JsonProperty("Имя получаемого предмета || Name of the item to be received")]
                public string DisplayName;

                [JsonProperty("Использовать плагин экономики? || Use the economy plugin?")]
                public bool economics;

                [JsonProperty("Количество пополняемого баланса || The amount of the balance to be replenished")]
                public int PluginAmount;

                [JsonProperty("Хук отвечающий за пополнение баланса || The hook responsible for adding funds to the balance")]
                public string PluginHook;

                [JsonProperty("Полное имя плагина с Экономикой || Full name of the plugin with Economy")]
                public string PluginName;

                [JsonProperty("ShortName получаемого предмета || Item shortname")]
                public string ShortName;

                [JsonProperty("SkinID предмета || Item SkinID")] public ulong SkinID;
            }
        }


        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        #endregion

        #region OxideHooks

        private object CanHideStash(BasePlayer player, StashContainer stash)
        {
            return stash != null && stash.skinID == 132321 ? (object) false : null;
        }

        private object OnEntityTakeDamage(StashContainer entity, HitInfo info)
        {
            return entity == null || entity.skinID != 132321 ? (object) null : false;
        }

        [ChatCommand("getfarm")]
        private void cmdChatgetfarm(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            foreach (var list in _config.ItemList)
            {
                foreach (var check in list.Value)
                {
                    var item = ItemManager.CreateByName(check.shortname, check.amount, check.SkinID);
                    if (!string.IsNullOrEmpty(check.DisplayName))
                        item.name = check.DisplayName;

                    item.maxCondition = check.MaxCondition;
                    player.GiveItem(item);
                }
            }
        }

        private void OnLootEntity(BasePlayer player, LootContainer container)
        {
            if (player == null || container == null || container.OwnerID != 0) return;
            if (!_config.ItemList.ContainsKey(container.ShortPrefabName)) return;
            var lootlist = _config.ItemList[container.ShortPrefabName];
            foreach (var check in lootlist)
            {
                var random = Core.Random.Range(0, 100);
                if (random <= check.Chance)
                {
                    var item = ItemManager.CreateByName(check.shortname, check.amount, check.SkinID);
                    if (!string.IsNullOrEmpty(check.DisplayName))
                        item.name = check.DisplayName;

                    item.maxCondition = check.MaxCondition;
                    item.MoveToContainer(container.inventory);
                }
            }
            container.OwnerID = player.userID;
        }

        private object OnEntityKill(ComputerStation entity)
        {
            if (entity == null || entity.GetComponent<Computer>() == null) return null;
            var comp = entity.GetComponent<Computer>();
            comp.Box.DropItems();
            return null;
        }

        private void OnServerInitialized()
        {
            _ins = this;
            LoadData();
            permission.RegisterPermission(perm, this);
            foreach (var check in _config.PermissionList)
            {
                permission.RegisterPermission(check.Key, this);
            }

            PrintWarning("" + "\n=====================" + "\n=====================Author: CASHR" +
                         "\n=====================VK: vk.com/cashrdev" +
                         "\n=====================Discord: !CASHR#6906" +
                         "\n=====================Email: pipnik99@gmail.com" +
                         "\n=====================If you want to order a plugin from me, I am waiting for you in discord." +
                         "\n=====================");
            
            if(_config.skinEnabled)
                Unsubscribe("OnItemSplit");
            NextTick(() =>
            {
                RestoreFarm();
                _uiContoller = new GameObject().AddComponent<UIContoller>();
                if (!_config.Reward.economics) return;
                Econom = _ins.plugins.Find(_config.Reward.PluginName);
                if (Econom != null) return;
                _ins.PrintError($"Plugin '{_config.Reward.PluginName} ' Not found. Plugin UNLOAD!!!! ");
                Interface.Oxide.UnloadPlugin(Name);
            });
        }

        private void Unload()
        {
            var farm = UnityEngine.Object.FindObjectsOfType<Computer>();
            foreach (var go in farm)
            {
                go.Kill();
                UnityEngine.Object.Destroy(go);
            }
            var obj = UnityEngine.Object.FindObjectsOfType<UIContoller>();
            foreach (var go in obj)
            {
                go.Kill();
                UnityEngine.Object.Destroy(go);
            }
            SaveData();
            _ins = null;
        }

        private Item OnItemSplit(Item item, int amount)
        {
            if (item.skin == 0) return null;
            if (_config.ComponentList.ContainsKey(item.skin) || _config.CoollerList.ContainsKey(item.skin))
            {
                var x = ItemManager.CreateByName(item.info.shortname, amount, item.skin);
                x.name = item.name;
                x.maxCondition = item.maxCondition;
                x.condition = item.condition;
                item.amount -= amount;
                return x;
            }
            return null;
        }
       

        private void OnEntityBuilt(Planner plan, GameObject obj)
        {
            var ent = obj.ToBaseEntity();
            if (ent.GetComponent<ComputerStation>() == null) return;
            var player = plan.GetOwnerPlayer();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, perm))
            {
                player.ChatMessage(GetMsg(player, "CANT_BUILD", new []{""}));
                return;
            }

            var box = GameManager.server.CreateEntity(
                "assets/prefabs/deployable/small stash/small_stash_deployed.prefab") as StorageContainer;
            box.SetParent(ent);
            box.skinID = 132321;
            box.transform.localPosition = new Vector3(-0.55f, 0.85f, 0.25f);
            box.transform.localRotation = Quaternion.Euler(new Vector3(0f, 90f, 0f));
            box.panelName = "genericlarge";
            box.Spawn();
            box.OwnerID = player.userID;
            var slots = GetMaxSlots(player.UserIDString);
            box.inventory.capacity = slots;
            box.inventorySlots = slots;
            box.SendNetworkUpdateImmediate();
            var siren = GameManager.server.CreateEntity(
                    "assets/prefabs/deployable/playerioents/lights/sirenlight/electric.sirenlight.deployed.prefab") as
                SirenLight;
            siren.SetParent(ent);
            siren.transform.localPosition = new Vector3(0f, 0.77f, -0.25f);
            siren.transform.localRotation = Quaternion.Euler(new Vector3(180f, 90f, 180f));
            siren.Spawn();
            siren.speed = 10f;
            NextTick(() =>
            {
                box.GetComponent<StashContainer>().CancelInvoke("DoOccludedCheck");
                UnityEngine.Object.Destroy(siren.GetComponent<GroundWatch>());
                UnityEngine.Object.Destroy(box.GetComponent<GroundWatch>());
                UnityEngine.Object.Destroy(ent.GetComponent<GroundWatch>());
                UnityEngine.Object.Destroy(ent.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.Destroy(siren.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.Destroy(box.GetComponent<DestroyOnGroundMissing>());
            });
            siren.SendNetworkUpdate();
            siren.SendNetworkUpdateImmediate();
            _data.Add(new Data()
            {
                OwnerID = player.UserIDString,
                ComputerID = ent.net.ID,
                Active = false,
                Progress = 0,
                SirenID = siren.net.ID
            });
            ent.gameObject.AddComponent<Computer>();
            var comp = ent.GetComponent<Computer>();
            comp.Active = false;
            comp.Box = box;
            comp.Siren = siren;
            comp.Progress = 0;
            comp.StartFarm();
            ClearAllComponents(obj, typeof(DestroyOnGroundMissing), typeof(GroundWatch));
        }
        public static void ClearAllComponents(GameObject go, params Type[] types)
        {
            var lst = Pool.GetList<Component>();
            foreach (var type in types) lst.AddRange(go.GetComponentsInChildren(type) ?? new Component[0]);
            foreach (var component in lst) UnityEngine.Object.DestroyImmediate(component);
            Pool.FreeList(ref lst);
        }
        private void CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            if (!(entity is ComputerStation)) return;

            var farm = entity.GetComponent<Computer>();
            if (farm == null) return;


            CuiHelper.DestroyUi(player, "UI_CAINTAG_MAINLAYER");
            var container = new CuiElementContainer();
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "65 -82", OffsetMax = "250 -42"},
                Button = {Color = "1 1 1 0.5", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat", Close = Layer + "BUTTON", Command = $"UI_MININGFARM"},
                Text =
                {
                    Text = GetMsg(player, "UI_OPENINFOPANEL", new []{""}), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf",
                    FontSize = 16
                }
            }, "Overlay", Layer + "BUTTON");
            CuiHelper.DestroyUi(player, Layer + "BUTTON");
            CuiHelper.AddUi(player, container);
            _uiContoller.playerList.Add(player);
        }


        [ConsoleCommand("UI_MININGFARM")]
        private void cmdChatUI_MININGFARM(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var computer = player.GetMounted().GetComponent<Computer>();
            if (arg?.Args != null && arg.Args[0] == "SWITCH")
            {
                computer.Active = !computer.Active;
            }
            ShowControlUI(player, computer);
        }

        #endregion

        #region Data

        private List<Data> _data;

        private class Data
        {
            public string OwnerID;
            public uint ComputerID;
            public uint SirenID;
            public float Progress;
            public float Temperature;
            public bool Active;
        }

        private void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/FarmList"))
                _data = Interface.Oxide.DataFileSystem.ReadObject<List<Data>>(
                    $"{Name}/FarmList");
            else _data = new List<Data>();
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/FarmList", _data);
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void SaveData()
        {
            if (_data != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/FarmList", _data);
        }

        #endregion

        #region Function

        private void RestoreFarm()
        {
            foreach (var check in _data.ToArray())
            {
                var computer = BaseNetworkable.serverEntities.Find(check.ComputerID);
                if (computer == null)
                {
                    _data.Remove(check);
                    continue;
                }
                var box = computer.GetComponentInChildren<StorageContainer>();
                var siren = BaseNetworkable.serverEntities.Find(check.SirenID) as SirenLight;

                if (box == null || siren == null)
                {
                     _data.Remove(check);
                     PrintError("ОБЪЕКТ НЕ НАЙДЕН");
                     continue;
                }
				  NextTick(() =>
            {
                box.GetComponent<StashContainer>().CancelInvoke(nameof(StashContainer.DoOccludedCheck));
                UnityEngine.Object.Destroy(siren.GetComponent<GroundWatch>());
                UnityEngine.Object.Destroy(box.GetComponent<GroundWatch>());
                UnityEngine.Object.Destroy(computer.GetComponent<GroundWatch>());
                UnityEngine.Object.Destroy(computer.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.Destroy(siren.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.Destroy(box.GetComponent<DestroyOnGroundMissing>());
            });
                var slots = GetMaxSlots(check.OwnerID);
                box.panelName = "genericlarge";
                box.inventory.capacity = slots;
                box.inventorySlots = slots;
                box.SendNetworkUpdateImmediate();
                computer.gameObject.AddComponent<Computer>();
                var comp = computer.GetComponent<Computer>();
                comp.Active = check.Active;
                comp.Box = box;
                comp.Siren = siren;
                comp.Progress = check.Progress;
                comp.Temperature = check.Temperature;
            }
        }

        #endregion

        private void ShowControlUI(BasePlayer player, Computer _computer)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform =
                    {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image = {Color = "1 1 1 0"}
            }, "Overlay", Layer);

		    container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",OffsetMin = "-600 -340", OffsetMax = "600 340"},
                Button = {Color = "0 0 0 0.2", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"},
                Text =
                {
                    Text = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 18
                }
            }, Layer, Layer + ".WORK");
			 container.Add(new CuiElement
            {
                Parent = Layer + ".WORK",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player, "UI_CONTROLMENU", new []{""}), FontSize = 45, Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0.85", AnchorMax = "1 1"},
                    new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.7 -0.7"}
                }
            });
			container.Add(new CuiElement
            {
                Parent = Layer + ".WORK",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player, "UI_LASTWARNING", new []{_computer.Warning}), Color = "1 0 0 1",FontSize = 30, Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0.7", AnchorMax = "1 1"},
                    new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.7 -0.7"}
                }
            });
             var text = _computer.Active ? GetMsg(player, "ON", new []{""}) : GetMsg(player, "OFF", new []{""});
             var color = _computer.Active ? "0.4 0.7 0.4 0.95" : "0.44 0.29 0.29 1.00";
             container.Add(new CuiButton
             {
                 RectTransform = {AnchorMin = "0.035 0.905", AnchorMax = "0.15 0.962"},//908
                 Button = {Color = color, Command = "UI_MININGFARM SWITCH"},
                 Text =
                 {
                     Text = text, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 18
                 }
             }, Layer + ".WORK");
			container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.1 0.45", AnchorMax = "0.9 0.55"},
                Button = {Color = "1 1 1 0.4", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"},
                Text =
                {
                    Text = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18
                }
            },Layer + ".WORK", Layer + "PROGRESSBAR");

            var procent = _computer.Progress  * 100 / _config.Capacity;
            var TemperatureProcent = _computer.Temperature  * 100 / _config.CriticatlTemperature;
			var pos = 0.01 * procent;
			var TemperaturePos = 0.01 * TemperatureProcent;
            var colorTemp = TemperatureProcent > 70 ? "0.98 0.00 0.00 1.00" : TemperatureProcent > 40 ? "0.50 0.00 0.32 1.00" : "0.19 0.00 0.70 1.00";
			var info = _computer.GetTick(true) * 100 / _config.Capacity;

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.125 0.17", AnchorMax = "0.175 0.43"},
                Button = {Color = "1 1 1 0.4", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"},
                Text =
                {
                    Text = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 18
                }
            }, Layer, Layer + "TEMPBAR");
			container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = $"0.99 {TemperaturePos}"},
                Button = {Color = colorTemp},
                Text =
                {
                    Text = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 10
                }
            },Layer + "TEMPBAR");
			container.Add(new CuiElement
            {
                Parent = Layer + "TEMPBAR",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{_computer.Temperature:0.00}°C", FontSize = 30, Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"},
                    new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.7 -0.7"}
                }
            });
			container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = $"{pos} 0.98"},
                Button = {Color = "0.4 0.7 0.4 0.95"},
                Text =
                {
                    Text = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18
                }
            },Layer + "PROGRESSBAR");
            container.Add(new CuiElement
            {
                Parent = Layer + ".WORK",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player, "UI_MAXTEMPERATURE", new []{$"{_computer.GetTemperature():0.00}"}), FontSize = 30, Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {AnchorMin = "0.5 0.55", AnchorMax = "0.9 0.8"},
                    new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.7 -0.7"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".WORK",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player, "UI_COLLER", new []{$"{_computer.GetCooller(true):0.00}"}), FontSize = 30, Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {AnchorMin = "0.1 0.55", AnchorMax = "0.4 0.8"},
                    new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.7 -0.7"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + "PROGRESSBAR",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player, "UI_PROGRESS", new []{procent.ToString(CultureInfo.CurrentCulture)}), FontSize = 45, Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"},
                    new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.7 -0.7"}
                }
            });
			 container.Add(new CuiElement
            {
                Parent = Layer + ".WORK",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player, "UI_POWER", new []{info.ToString(CultureInfo.CurrentCulture), _config.TickCooldown.ToString(CultureInfo.InvariantCulture)}), FontSize = 45, Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 0.2"},
                    new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.7 -0.7"}
                }
            });
             container.Add(new CuiButton
             {
                 RectTransform = {AnchorMin = "0.45 0.2", AnchorMax = "0.55 0.35"},
                 Button = {Color = "1 1 1 1",Sprite = "assets/icons/refresh.png", Command = "UI_MININGFARM"},
                 Text =
                 {
                     Text = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 18
                 }
             }, Layer + ".WORK");
			 container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.905 0.905", AnchorMax = "0.964 0.962"},
                Button = {Color = "0 0 0 1", Close = Layer, Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"},
                Text =
                {
                    Text = GetMsg(player, "UI_EXIT", new []{""}), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18
                }
            },Layer + ".WORK");
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private int GetMaxSlots(string userid)
        {
            var rate =  0;
            foreach (var privilege in _config.PermissionList)
            {
                if (permission.UserHasPermission(userid, privilege.Key))
                    rate =  Mathf.Max(rate, privilege.Value);
            }
            return rate;
        }

        private class UIContoller : FacepunchBehaviour
        {
            public List<BasePlayer> playerList;

            private void Awake()
            {
                playerList = new List<BasePlayer>();
                InvokeRepeating(UpdateList, 0.3f,0.3f);
            }

            private void UpdateList()
            {
                if (playerList.Count == 0) return;
                var list = playerList.ToArray();
                for (var index = 0; index < list.Length; index++)
                {
                    var check = list[index];
                    if (check.isMounted) continue;
                    CuiHelper.DestroyUi(check, _ins.Layer);
                    CuiHelper.DestroyUi(check, _ins.Layer + "BUTTON");
                    playerList.Remove(check);
                }
            }

            public void Kill()
            {
                CancelInvoke(UpdateList);
                Destroy(this);
            }
        }

        #region Class Computer
        private class Computer : FacepunchBehaviour
        {
            private BaseEntity computer;
            public StorageContainer Box;
            public SirenLight Siren;
            public float Progress;
            public float Temperature;
            public bool Active;
            public string Warning = "";
            public StashContainer smallstash;
            private void Awake()
            {
                computer = GetComponent<BaseEntity>();
                Box = GetComponent<StorageContainer>();
                Siren = GetComponent<SirenLight>();
                StartFarm();
                Invoke(() =>
                {
                    smallstash = Box.GetComponent<StashContainer>();
                }, 1);
            }

            private void FixedUpdate()
            {
                if (smallstash == null) return;
                if (smallstash.IsInvoking(smallstash.DoOccludedCheck))
                {
                    InvokeHandler.CancelInvoke(smallstash.GetComponent<MonoBehaviour>(), smallstash.DoOccludedCheck);
                }
            }

            public void StartFarm()
            {
                InvokeRepeating(UpdateFarm, _ins._config.TickCooldown, _ins._config.TickCooldown);
            }

            private void UpdateFarm()
            {
                if (!Active && Temperature <= 0) return;
                if (Active)
                {
                    if (!_ins._config.OfflineWork && BasePlayer.Find(computer.OwnerID.ToString()) == null)
                    {
                        Active = false;
                        return;
                    }
                    if (Siren.currentEnergy < _ins._config.CurrentEnergy)
                    {
                        Effect.server.Run("assets/bundled/prefabs/fx/invite_notice.prefab",
                            computer.transform.position);
                        Warning = _ins.GetMsg(BasePlayer.activePlayerList.FirstOrDefault(), "NOT_ENOUGH_ELECTR", new []{""});
                        return;
                    }

                    Active = true;
                    Warning = "";
                    var tick = GetTick(false);
                    Progress += tick;
                    Temperature += 2;
                    var cooller = GetCooller(false);
                    Temperature += GetTemperature();
                    Temperature -= cooller;
                    Temperature = Temperature < -20 ? -20 : Temperature;
                    if ((Temperature * 100 / _ins._config.CriticatlTemperature) > 50)
                    {
                        Effect.server.Run("assets/bundled/prefabs/fx/invite_notice.prefab",
                            computer.transform.position);
                        Warning = _ins.GetMsg(BasePlayer.activePlayerList.FirstOrDefault(), "CRITICAL_TEMPERATURE", new []{""});
                    }

                    if (Temperature >= _ins._config.CriticatlTemperature)
                    {
                        Effect.server.Run("assets/bundled/prefabs/fx/weapons/landmine/landmine_explosion.prefab",
                            computer.transform.position);
                        computer.Kill();
                        Kill();
                        return;
                    }

                    if (!(Progress >= _ins._config.Capacity)) return;
                    Progress = 0;
                    GiveReward();
                }
                else
                {
                    Temperature -= 2;
                    Temperature = Temperature <= 0 ? 0 : Temperature;
                }
            }


            private void GiveReward()
            {
                var userid = computer.OwnerID;
                var config = _ins._config.Reward;
                if (config.economics)
                {
                    _ins.Econom?.Call(config.PluginHook, userid, config.PluginAmount);
                    _ins.LogToFile("MiningFarm", $"Игрок {userid} пополнил свой счет на {config.PluginAmount} рублей.",
                        _ins);
                    var player = BasePlayer.FindByID(userid);
                    if (player != null && player.IsConnected) player.ChatMessage(_ins.GetMsg(player, "FARM_BONUS", new []{""}));
                }

                if (config.cmd)
                {
                    var command = config.command.Replace("%STEAMID%", userid.ToString());
                    _ins.Server.Command(command);
                    return;
                }
                var container = Box.inventory;
                var item = ItemManager.CreateByName(config.ShortName, config.Amount, config.SkinID);
                if (item == null) return;
                item.name = config.DisplayName;
                if (!item.MoveToContainer(container)) item.Remove();
                ItemManager.DoRemoves();
            }

            public float GetTick(bool info)
            {
                if (Box == null)
                {
                    Box = computer.GetComponentInChildren<StorageContainer>();

                }

                var tick = _ins._config.OnTick;
                for (var index = 0; index < Box.inventory.itemList.ToArray().Length; index++)
                {
                    var check = Box.inventory.itemList[index];
                    if (_ins._config.ComponentList.ContainsKey(check.skin))
                    {
                        tick += _ins._config.ComponentList[check.skin].OnTick * check.amount;
                        if (!info)
                        {
                            check.condition -= _ins._config.ComponentList[check.skin].MinusCondition;
                            if (check.condition <= 0)
                                check.DoRemove();
                        }
                    }
                }
                return tick;
            }

            public float GetTemperature()
            {
                var temp = _ins._config.TemperaturePlus;
                for (var index = 0; index < Box.inventory.itemList.ToArray().Length; index++)
                {
                    var check = Box.inventory.itemList[index];
                    if (_ins._config.ComponentList.ContainsKey(check.skin))
                    {
                        temp += _ins._config.ComponentList[check.skin].TemperaturePlus * check.amount;
                    }
                }
                return temp;
            }

            public float GetCooller(bool info)
            {
                var amount = 0f;
                for (var index = 0; index < Box.inventory.itemList.Count; index++)
                {
                    var check = Box.inventory.itemList[index];
                    if (_ins._config.CoollerList.ContainsKey(check.skin))
                    {
                        amount += _ins._config.CoollerList[check.skin].TemperaturePlus * check.amount;
                        if (!info)
                        {
                            check.condition -= _ins._config.CoollerList[check.skin].MinusCondition;
                            if(check.condition<= 0)
                                check.DoRemove();
                        }
                    }
                }
                return amount;
            }
            public void Kill()
            {
                CancelInvoke(UpdateFarm);
                var first = _ins._data.First(p => p.ComputerID == computer.net.ID);
                first.Progress = Progress;
				first.Temperature = Temperature;
				first.Active = Active;
                _ins.SaveData();
                Destroy(this);
            }
        }
        #endregion

        #region Language

        private string GetMsg(BasePlayer player, string msg, string[] args) => string.Format(lang.GetMessage(msg, this, player.UserIDString), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["FARM_BONUS"] = "Your farm has made you a profit",
                ["CANT_BUILD"] = "You can't set a mining farm",
                ["CRITICAL_TEMPERATURE"] = "CRITICAL TEMPERATURE OF THE COMPUTER",
                ["NOT_ENOUGH_ELECTR"] = "NOT ENOUGH ELECTRICITY TO RUN YOUR FARM",
                ["UI_OPENINFOPANEL"] = "OPEN FARM MANAGEMENT MENU",
                ["UI_CONTROLMENU"] = "YOUR FARM MANAGEMENT MENU",
                ["UI_LASTWARNING"] = " LAST ERROR: {0}",
                ["ON"] = "ENABLED",
                ["OFF"] = "OFF",
                ["UI_MAXTEMPERATURE"] = "MAXIMUM COMPONENT TEMPERATURE: {0}°C",
                ["UI_COLLER"] = "ACTIVE COOLING: -{0}°C",
                ["UI_PROGRESS"] = " PROGRESS: {0}%",
                ["UI_POWER"] = "POWER OF YOUR FARM: {0} % IN {1} SECOND",
                ["UI_EXIT"] = "EXIT"
            }, this);
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["FARM_BONUS"] = "Ваша ферма принесла вам прибыль",
                ["CANT_BUILD"] = "Вы не можете устанавливать майнинговую ферму",
                ["CRITICAL_TEMPERATURE"] = "КРИТИЧЕСКАЯ ТЕМПЕРАТУРА КОМПЬЮЕТРА",
                ["NOT_ENOUGH_ELECTR"] = "НЕ ДОСТАТОЧНО ЭЛЕКТРОЭНЕРГИИ ДЛЯ РАБОТЫ ВАШЕЙ ФЕРМЫ",
                ["UI_OPENINFOPANEL"] = "ОТКРЫТЬ МЕНЮ УПРАВЛЕНИЯ ФЕРМОЙ",
                ["UI_CONTROLMENU"] = "МЕНЮ УПРАВЛЕНИЯ ВАШЕЙ ФЕРМОЙ",
                ["UI_LASTWARNING"] = "ПОСЛЕДНЯЯ ОШИБКА: {0}",
                ["ON"] = "ВКЛЮЧЕНО",
                ["OFF"] = "ВЫКЛЮЧЕНО",
                ["UI_MAXTEMPERATURE"] = "МАКСИМАЛЬНАЯ ТЕМПЕРАТУРА КОМПЛЕКТУЮЩИХ: {0}°C",
                ["UI_COLLER"] = "АКТИВНОЕ ОХЛАЖДЕНИЕ: -{0}°C",
                ["UI_PROGRESS"] = "ПРОГРЕСС: {0}%",
                ["UI_POWER"] = "МОЩНОСТЬ ВАШЕЙ ФЕРМЫ: {0} % В {1} СЕКУНДУ",
                ["UI_EXIT"] = "ВЫХОД"

            }, this, "ru");

        }

        #endregion
    }
}
