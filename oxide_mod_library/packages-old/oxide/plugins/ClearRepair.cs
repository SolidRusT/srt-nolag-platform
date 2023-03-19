using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Clear Repair", "Clearshot", "1.3.0")]
    [Description("Display insufficient resources required to repair with hammer or toolgun")]
    class ClearRepair : CovalencePlugin
    {
        private PluginConfig _config;
        private Game.Rust.Libraries.Player _rustPlayer = Interface.Oxide.GetLibrary<Game.Rust.Libraries.Player>("Player");
        private StringBuilder _sb = new StringBuilder();
        private readonly Dictionary<string, string> _shortPrefabNameToBuilding = new Dictionary<string, string>();

        private void SendChatMsg(BasePlayer pl, string msg, string prefix = null) =>
            _rustPlayer.Message(pl, msg, prefix != null ? prefix : lang.GetMessage("ChatPrefix", this, pl.UserIDString), Convert.ToUInt64(_config.chatIconID), Array.Empty<object>());

        private void Init()
        {
            permission.RegisterPermission("clearrepair.use", this);
        }

        private void OnServerInitialized()
        {
            foreach (var entityPath in GameManifest.Current.entities)
            {
                var construction = PrefabAttribute.server.Find<Construction>(StringPool.Get(entityPath));
                if (construction != null && construction.deployable == null && !string.IsNullOrEmpty(construction.info.name.english))
                {
                    var shortname = construction.fullName.Substring(construction.fullName.LastIndexOf('/') + 1).Replace(".prefab", "");
                    if (!_shortPrefabNameToBuilding.ContainsKey(shortname))
                    {
                        _shortPrefabNameToBuilding.Add(shortname, construction.info.name.english);
                    }
                }
            }
        }

        private object OnStructureRepair(BaseCombatEntity ent, BasePlayer pl)
        {
            if (ent == null || pl == null) return null;
            if (_config.usePermission && !permission.UserHasPermission(pl.UserIDString, "clearrepair.use")) return null;

            float num = 30f;
            if (ent.SecondsSinceAttacked <= num)
            {
                return null;
            }
            float num2 = ent.MaxHealth() - ent.health;
            float num3 = num2 / ent.MaxHealth();
            if (num2 <= 0f || num3 <= 0f)
            {
                return null;
            }
            List<ItemAmount> list = ent.RepairCost(num3);
            if (list == null)
            {
                return null;
            }

            float num4 = list.Sum((ItemAmount x) => x.amount);
            if (num4 > 0f)
            {
                float num5 = list.Min((ItemAmount x) => UnityEngine.Mathf.Clamp01(pl.inventory.GetAmount(x.itemid) / x.amount));
                num5 = UnityEngine.Mathf.Min(num5, 50f / num2);
                if (num5 <= 0f)
                {
                    if (_config.defaultChatNotification)
                    {
                        ent.OnRepairFailed(pl, lang.GetMessage("DefaultChatNotification", this, pl.UserIDString));
                    }

                    _sb.Clear();
                    _sb.AppendLine(string.Format(lang.GetMessage("RepairItemName", this, pl.UserIDString), GetEntityItemName(ent, pl.UserIDString)));
                    _sb.AppendLine(lang.GetMessage("InsufficientRes", this, pl.UserIDString));
                    foreach (ItemAmount itemAmount in list)
                    {
                        string color = pl.inventory.GetAmount(itemAmount.itemid) >= itemAmount.amount ? _config.itemFoundColor : _config.itemNotFoundColor;
                        _sb.AppendLine(string.Format(lang.GetMessage("ItemAmount", this, pl.UserIDString), GetItemName(itemAmount.itemDef, pl.UserIDString), color, itemAmount.amount));
                    }

                    SendChatMsg(pl, _sb.ToString());
                    return false;
                }
            }

            return null;
        }

        private string GetEntityItemName(BaseCombatEntity ent, string UserID)
        {
            string itemName;
            string msg = lang.GetMessage(ent.ShortPrefabName, this, UserID);
            if (msg != null && msg != ent.ShortPrefabName)
            {
                itemName = msg;
            }
            else if (ent?.repair.itemTarget != null)
            {
                return GetItemName(ent.repair.itemTarget, UserID);
            }
            else if (_shortPrefabNameToBuilding.ContainsKey(ent.ShortPrefabName))
            {
                itemName = _shortPrefabNameToBuilding[ent.ShortPrefabName];
            }
            else
            {
                itemName = ent.ShortPrefabName;
            }
            return _config.showShortname ? $"{itemName} ({ent.ShortPrefabName})" : itemName;
        }

        private string GetItemName(ItemDefinition item, string UserID)
        {
            string itemName;
            string msg = lang.GetMessage(item.shortname, this, UserID);
            if (msg != null && msg != item.shortname)
            {
                itemName = msg;
            }
            else if (!string.IsNullOrEmpty(item.displayName.english))
            {
                itemName = item.displayName.english;
            }
            else
            {
                itemName = item.shortname;
            }
            return _config.showShortname ? $"{itemName} ({item.shortname})": itemName;
        }

        [Command("clearrepair.generate_lang")]
        private void GenerateLangCommand(IPlayer player, string command, string[] args)
        {
            if (player == null || !player.IsServer) return;

            Dictionary<string, string> sortedList = new Dictionary<string, string>();

            _sb.Clear();
            _sb.AppendLine("\n*** Buildings ***\n");
            foreach (var building in _shortPrefabNameToBuilding.OrderBy(x => x.Key))
            {
                _sb.AppendLine($"{building.Key} - {building.Value}");
                sortedList.Add(building.Key, building.Value);
            }

            _sb.AppendLine("\n*** Items ***\n");
            foreach (var item in ItemManager.itemList.OrderBy(x => x.shortname))
            {
                _sb.AppendLine($"{item.shortname} - {item.displayName.english}");
                sortedList.Add(item.shortname, item.displayName.english);
            }

            Interface.Oxide.DataFileSystem.WriteObject($"{Name}\\GeneratedLang", sortedList);
            _sb.AppendLine($"\nSaved to /oxide/data/{Name}/GeneratedLang.json\n");
            Puts(_sb.ToString());
        }

        [Command("clearrepair.check_lang")]
        private void CheckLangCommand(IPlayer player, string command, string[] args)
        {
            if (player == null || !player.IsServer) return;

            if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            {
                _sb.Clear();
                _sb.AppendLine();
                foreach (var l in lang.GetLanguages(this))
                {
                    if (l.ToLower() == args[0].ToLower())
                    {
                        foreach(var msg in lang.GetMessages(l, this))
                        {
                            _sb.AppendLine($"{msg.Key} - {msg.Value}");
                        }
                    }
                }
                Puts(_sb.ToString());
            }
        }

        #region Config

        protected override void LoadDefaultMessages()
        {
            string[] langs = lang.GetLanguages(this);
            foreach(var l in langs)
            {
                Puts($"registered language: {l} ({lang.GetMessages(l, this).Count})");
            }

            if (!langs.Contains("en"))
            {
                lang.RegisterMessages(new Dictionary<string, string>
                {
                    ["ChatPrefix"] = $"<color=#00a7fe>[{Title}]</color>",
                    ["RepairItemName"] = "<line-height=20>{0}",
                    ["InsufficientRes"] = "<size=12>Unable to repair: Insufficient resources.</size><line-indent=5>",
                    ["ItemAmount"] = "<size=12>{0}: <color={1}>{2}</color></size>",
                    ["DefaultChatNotification"] = "Unable to repair: Insufficient resources.",
                    ["minicopter.entity"] = "Minicopter",
                    ["rowboat"] = "Row Boat",
                    ["rhib"] = "RHIB",
                    ["scraptransporthelicopter"] = "Scrap Transport Helicopter"
                }, this);
            } else {
                Dictionary<string, string> msgs = lang.GetMessages("en", this);
                if (msgs.Count < 6 || !msgs.ContainsKey("minicopter.entity"))
                {
                    Puts($"Outdated english language file detected! Delete /oxide/lang/en/{Name}.json and reload the plugin to generate an updated english language file.");
                }
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        private class PluginConfig
        {
            public bool usePermission = false;
            public bool defaultChatNotification = false;
            public bool showShortname = false;
            public string chatIconID = "0";
            public string itemFoundColor = "#87b33a";
            public string itemNotFoundColor = "#cb3f2a";
        }

        #endregion
    }
}
