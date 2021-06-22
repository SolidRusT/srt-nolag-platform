using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Suicide Vest", "birthdates", "1.1.1")]
    [Description("Allows players to have a suicide vest that blows up")]
    public class SuicideVest : RustPlugin
    {
        #region Variables

        private readonly List<BasePlayer> _armed = new List<BasePlayer>();
        private readonly Dictionary<BasePlayer, long> _cooldowns = new Dictionary<BasePlayer, long>();
        private readonly Dictionary<BasePlayer, Timer> _timers = new Dictionary<BasePlayer, Timer>();
        private Data _data;

        private const string PermissionGive = "suicidevest.give";
        private const string PermissionUse = "suicidevest.use";

        #endregion

        #region Hooks

        private void Init()
        {
            LoadConfig();
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionGive, this);
            cmd.AddChatCommand("givevest", this, ChatCmd);
            _data = Interface.Oxide.DataFileSystem.ReadObject<Data>("Suicide Vest");
            if (!_config.ExplodeWhenShot) Unsubscribe("OnEntityTakeDamage");
        }

        private void OnServerInitialized()
        {
            var prefabs = _config.ExplosionPrefab;
            prefabs.Add(_config.ArmedSoundPrefab);
            prefabs.Add(_config.UnarmedSoundPrefab);
            foreach (var pref in prefabs.Where(IsInvalidPrefab)) PrintError(pref + " is not a valid prefab!");
            SetupItems();
            Cleanup();
        }

        private static bool IsInvalidPrefab(string prefab)
        {
            return !StringPool.toString.Values.Contains(prefab);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Suicide Vest", _data);
            if (_data.Vests.Count < 1)
            {
                Unsubscribe("OnPlayerInput");
                Unsubscribe("OnPlayerDie");
                if (_config.ExplodeWhenShot) Unsubscribe("OnEntityTakeDamage");
            }
            else if (_data.Vests.Count == 1)
            {
                Subscribe("OnPlayerInput");
                Subscribe("OnPlayerDie");
                if (_config.ExplodeWhenShot) Subscribe("OnEntityTakeDamage");
            }
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse) ||
                !input.WasJustReleased(BUTTON.FIRE_THIRD) || !input.WasDown(BUTTON.SPRINT) || !HasVest(player)) return;

            if (_armed.Contains(player))
            {
                if (!_config.Unarm) return;

                long cooldown;
                if (!_cooldowns.TryGetValue(player, out cooldown))
                {
                    AddCooldown(player);
                }
                else
                {
                    if (cooldown > DateTime.Now.Ticks) return;
                    AddCooldown(player);
                }

                Unarm(player);
            }
            else
            {
                Arm(player);
            }
        }

        private void AddCooldown(BasePlayer player)
        {
            _cooldowns[player] = DateTime.Now.Ticks + TimeSpan.FromSeconds(_config.UnarmCooldown).Ticks;
        }

        private static Vector3 GetBody(BasePlayer player)
        {
            var pos = player.eyes.position;
            pos.y -= 1;
            return pos;
        }

        private void OnPlayerDie(BasePlayer player)
        {
            if (!_armed.Contains(player)) return;

            if (_config.ExplodeOnDeath)
            {
                Explode(player);
                _armed.Remove(player);
            }
            else
            {
                Unarm(player);
            }
        }

        private object CanMoveItem(Item item)
        {
            var player = item.GetOwnerPlayer();
            if (player == null) return null;

            if (!_armed.Contains(player) || !_data.Vests.Contains(item.uid)) return null;
            SendReply(player, lang.GetMessage("CannotMoveWhilstArmed", this, player.UserIDString));
            return false;
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity as BasePlayer;

            if (player == null || !_armed.Contains(player)) return;
            var p = info.Initiator as BasePlayer;
            if (p == null) return;
            if (info.boneName != "chest" || info.damageTypes.types[9] < 1f) return;
            Explode(player);
        }

        private static HashSet<Item> GetBoxItems()
        {
            var returnList = new HashSet<Item>();

            var boxes = Resources.FindObjectsOfTypeAll<StorageContainer>();
            foreach (var box in boxes)
            {
                if (box == null || box.inventory?.itemList == null) continue;

                foreach (var item in box.inventory.itemList) returnList.Add(item);
            }

            return returnList;
        }

        private static IEnumerable<Item> GetContainerItems(ItemContainer container)
        {
            var returnList = new HashSet<Item>();

            foreach (var item in container.itemList) returnList.Add(item);

            return returnList;
        }

        private IEnumerable<Item> GetEntityItems()
        {
            var returnList = new HashSet<Item>();
            var entities = BaseNetworkable.serverEntities.ToList();
            foreach (var a in entities.Cast<BaseEntity>()
                .Where(x => x != null && x.GetItem() != null && _data.Vests.Contains(x.GetItem().uid)))
                returnList.Add(a.GetItem());
            return returnList;
        }

        private static IEnumerable<Item> GetPlayerItems()
        {
            var returnList = new HashSet<Item>();

            var players = new HashSet<BasePlayer>(BasePlayer.activePlayerList);
            players.UnionWith(BasePlayer.sleepingPlayerList);

            foreach (var player in players)
            {
                returnList.UnionWith(GetContainerItems(player.inventory.containerMain));
                returnList.UnionWith(GetContainerItems(player.inventory.containerBelt));
                returnList.UnionWith(GetContainerItems(player.inventory.containerWear));
            }

            return returnList;
        }

        private void Arm(BasePlayer player)
        {
            SendReply(player, string.Format(lang.GetMessage("Armed", this, player.UserIDString), _config.Delay));
            _armed.Add(player);

            Effect.server.Run(_config.ArmedSoundPrefab, player.transform.position);
            if (_timers.ContainsKey(player))
            {
                _timers[player].Destroy();
                _timers.Remove(player);
            }

            _timers.Add(player, timer.In(_config.Delay, delegate
            {
                if (!_armed.Contains(player)) return;
                _armed.Remove(player);
                Explode(player);
            }));
        }

        private void Explode(BasePlayer player)
        {
            var vest = GetVest(player);
            if (vest != null)
            {
                vest.RemoveFromContainer();
                vest.RemoveFromWorld();
            }

            var pos = GetBody(player);
            foreach (var pref in _config.ExplosionPrefab) Effect.server.Run(pref, pos);

            if (_config.ExplosionDamage > 0)
            {
                var all = new List<BaseCombatEntity>();

                Vis.Entities(pos, _config.ExplosionRadius, all);

                foreach (var entity in all.ToList())
                {
                    if (entity == null || !(entity.health > 0)) continue;
                    var a = entity.health;
                    entity.Hurt(new HitInfo(player, entity, DamageType.Explosion, _config.ExplosionDamage));
                    var p = entity as BasePlayer;
                    if (p == null || Math.Abs(p.health - a) < 0.1) continue;
                    p.metabolism.bleeding.value += _config.BleedingAfterDamage;
                    Interface.CallHook("OnRunPlayerMetabolism", p.metabolism);
                }
            }

            if (vest != null) _data.Vests.Remove(vest.uid);
            SaveData();
        }

        private void Unarm(BasePlayer player)
        {
            SendReply(player, lang.GetMessage("UnArmed", this, player.UserIDString));
            _armed.Remove(player);
            Effect.server.Run(_config.UnarmedSoundPrefab, player.transform.position);
            Timer playerTimer;
            if (!_timers.TryGetValue(player, out playerTimer)) return;
            playerTimer.Destroy();
            _timers.Remove(player);
        }

        private bool HasVest(BasePlayer player)
        {
            return player.inventory.containerWear.itemList.Find(x => _data.Vests.Contains(x.uid)) != null;
        }

        private Item GetVest(BasePlayer player)
        {
            return player.inventory.containerWear.itemList.Find(x => _data.Vests.Contains(x.uid));
        }

        [ConsoleCommand("givevest")]
        private void ConsoleCmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;

            if (arg.Args.Length < 1)
            {
                arg.ReplyWith(lang.GetMessage("InvalidPlayer", this));
                return;
            }

            var player = BasePlayer.Find(arg.GetString(0));
            if (player == null)
            {
                arg.ReplyWith(lang.GetMessage("InvalidPlayer", this));
                return;
            }

            var vestItem = ItemManager.CreateByName(_config.Item, 1, _config.SkinID);
            if (vestItem == null)
            {
                PrintError("Vest item is NULL! Please fix your configuration.");
                return;
            }

            vestItem.name = _config.Name;
            player.GiveItem(vestItem);

            _data.Vests.Add(vestItem.uid);
            SaveData();
        }

        private void SetupItems()
        {
            var list = GetBoxItems();
            list.UnionWith(GetPlayerItems());
            list.UnionWith(GetEntityItems());
            foreach (var item in list.Where(x => _data.Vests.Contains(x.uid)))
            {
                item.name = _config.Name;
                item.skin = _config.SkinID;
                item.MarkDirty();
            }
        }

        private void Unload()
        {
            var list = GetBoxItems();
            list.UnionWith(GetPlayerItems());
            list.UnionWith(GetEntityItems());
            foreach (var item in list.Where(x => _data.Vests.Contains(x.uid)))
            {
                item.name = string.Empty;
                item.skin = 0;
                item.MarkDirty();
            }
        }

        private void OnServerSave()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            var list = GetBoxItems();
            list.UnionWith(GetPlayerItems());
            list.UnionWith(GetEntityItems());
            for (var z = 0; z < _data.Vests.Count; z++)
            {
                var vest = _data.Vests[z];
                if (list.FirstOrDefault(x => x.uid == vest) == null) _data.Vests.Remove(vest);
            }

            SaveData();
        }

        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (action != "drop" || !_data.Vests.Contains(item.uid) || !_armed.Contains(player)) return null;
            SendReply(player, lang.GetMessage("CannotMoveWhilstArmed", this, player.UserIDString));
            return false;
        }

        private void ChatCmd(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionGive))
            {
                SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
            }
            else
            {
                BasePlayer target;
                target = args.Length < 1 ? player : BasePlayer.Find(args[0]);

                if (player == null)
                {
                    SendReply(player, lang.GetMessage("InvalidPlayer", this, player.UserIDString));
                    return;
                }

                var vestItem = ItemManager.CreateByName(_config.Item, 1, _config.SkinID);
                if (vestItem == null)
                {
                    PrintError("Vest item is NULL! Please fix your configuration.");
                    return;
                }

                vestItem.name = _config.Name;
                target.GiveItem(vestItem);

                _data.Vests.Add(vestItem.uid);
                SaveData();

                SendReply(player,
                    string.Format(lang.GetMessage("VestGiveSuccess", this, player.UserIDString), target.displayName));
            }
        }

        #endregion

        #region Configuration & Language

        private ConfigFile _config;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Armed", "You have armed your vest and it will explode in {0} seconds."},
                {"UnArmed", "You have unarmed your vest."},
                {"CannotMoveWhilstArmed", "You cannot move your vest whilst it's armed!"},
                {"NoPermission", "No permission!"},
                {"InvalidPlayer", "Invalid player!"},
                {"VestGiveSuccess", "You have given {0} a suicide vest"}
            }, this);
        }

        private class Data
        {
            public List<uint> Vests { get; } = new List<uint>();
        }

        public class ConfigFile
        {
            [JsonProperty("Vest armed sound (prefab)")]
            public string ArmedSoundPrefab;

            [JsonProperty("Bleeding Damage")] public float BleedingAfterDamage;

            [JsonProperty("Count down after armed (seconds)")]
            public int Delay;

            [JsonProperty("Explode when a user dies with an armed vest?")]
            public bool ExplodeOnDeath;

            [JsonProperty("Explode when a user shoots the vest?")]
            public bool ExplodeWhenShot;

            [JsonProperty("Explosion Damage")] public float ExplosionDamage;

            [JsonProperty("Effect prefabs")] public List<string> ExplosionPrefab;

            [JsonProperty("Explosion Radius")] public float ExplosionRadius;

            [JsonProperty("Vest Item (Shortname)")]
            public string Item;

            [JsonProperty("Vest Name")] public string Name;

            [JsonProperty("Vest Skin ID")] public ulong SkinID;

            [JsonProperty("Ability to unarm")] public bool Unarm;

            [JsonProperty("Unarm Cooldown (seconds)")]
            public long UnarmCooldown;

            [JsonProperty("Vest unarmed sound (prefab)")]
            public string UnarmedSoundPrefab;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    Item = "metal.plate.torso",
                    SkinID = 0,
                    Name = "Suicide Vest",
                    Delay = 10,
                    Unarm = false,
                    ExplosionPrefab = new List<string>
                    {
                        "assets/bundled/prefabs/fx/gas_explosion_small.prefab",
                        "assets/bundled/prefabs/fx/explosions/explosion_03.prefab"
                    },
                    ArmedSoundPrefab = "assets/prefabs/locks/keypad/effects/lock.code.updated.prefab",
                    UnarmedSoundPrefab = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab",
                    ExplodeOnDeath = true,
                    UnarmCooldown = 3,
                    ExplosionRadius = 3f,
                    ExplosionDamage = 75f,
                    BleedingAfterDamage = 10f,
                    ExplodeWhenShot = true
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null) LoadDefaultConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion
    }
}
//Generated with birthdates' Plugin Maker