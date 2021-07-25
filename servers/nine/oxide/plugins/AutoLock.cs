using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = Oxide.Core.Random;

namespace Oxide.Plugins
{
    [Info("Auto Lock", "birthdates", "2.4.1")]
    [Description("Automatically adds a codelock to a lockable entity with a set pin")]
    public class AutoLock : RustPlugin
    {
        #region Variables

        private const string PermissionUse = "autolock.use";
        private readonly Dictionary<BasePlayer, TimedCodeLock> _awaitingResponse = new Dictionary<BasePlayer, TimedCodeLock>();
        [UsedImplicitly] [PluginReference("NoEscape")] private Plugin _noEscape;

        private struct TimedCodeLock
        {
            public CodeLock CodeLock { get; set; }
            public DateTime Expiry { get; set; }
        }
        
        #endregion

        #region Hooks

        [UsedImplicitly]
        private void Init()
        {
            LoadConfig();
            permission.RegisterPermission(PermissionUse, this);
            _data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);

            cmd.AddChatCommand("autolock", this, ChatCommand);
            cmd.AddChatCommand("al", this, ChatCommand);
            if(_config.CodeLockExpiry <= 0f) Unsubscribe(nameof(OnServerInitialized));
        }

        [UsedImplicitly]
        private void OnServerInitialized()
        {
            timer.Every(3f, () =>
            {
                for (var i = _awaitingResponse.Count - 1; i > 0; i--)
                {
                    var timedLock = _awaitingResponse.ElementAt(i);
                    if(timedLock.Value.Expiry > DateTime.UtcNow) continue;
                    _awaitingResponse.Remove(timedLock.Key);
                }
            });
        }

        [UsedImplicitly]
        private void OnEntityBuilt(HeldEntity plan, GameObject go)
        {
            var player = plan.GetOwnerPlayer();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse)) return;
            var entity = go.ToBaseEntity() as DecayEntity;
            if (entity == null || _config.Disabled.Contains(entity.PrefabName)) return;
            var container = entity as StorageContainer;
            if (entity.IsLocked() || container != null && container.inventorySlots < 12 || !container && !(entity is AnimatedBuildingBlock)) return;
            if (_noEscape != null)
            {
                if (_config.NoEscapeSettings.BlockRaid && _noEscape.Call<bool>("IsRaidBlocked", player.UserIDString))
                {
                    player.ChatMessage(lang.GetMessage("RaidBlocked", this, player.UserIDString));
                    return;
                }

                if (_config.NoEscapeSettings.BlockCombat && _noEscape.Call<bool>("IsCombatBlocked", player.UserIDString))
                {
                    player.ChatMessage(lang.GetMessage("CombatBlocked", this, player.UserIDString));
                    return;
                }
            }

            
            var playerData = CreateDataIfAbsent(player.UserIDString);
            if (!playerData.Enabled || !HasCodeLock(player)) return;
            var code = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab") as CodeLock;
			if (code != null)
			{
			    code.gameObject.Identity();
			    code.SetParent(entity, entity.GetSlotAnchorName(BaseEntity.Slot.Lock));
			    code.Spawn();
			    code.code = playerData.Code;
			    code.hasCode = true;
			    entity.SetSlot(BaseEntity.Slot.Lock, code);
			    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab", code.transform.position);
			    code.whitelistPlayers.Add(player.userID);
			    code.SetFlag(BaseEntity.Flags.Locked, true);
			}
            TakeCodeLock(player);
            player.ChatMessage(string.Format(lang.GetMessage("CodeAdded", this, player.UserIDString),
                player.net.connection.info.GetBool("global.streamermode") ? "****" : playerData.Code));
        }

        private static string GetRandomCode()
        {
            return Random.Range(1000, 9999).ToString();
        }

        [UsedImplicitly]
        private void OnServerShutdown()
        {
            Unload();
        }

        private void Unload()
        {
            SaveData();
            foreach (var timedLock in _awaitingResponse.Values.Where(timedLock => !timedLock.CodeLock.IsDestroyed)) timedLock.CodeLock.Kill();
        }

        private PlayerData CreateDataIfAbsent(string id)
        {
            PlayerData playerData;
            if (_data.Codes.TryGetValue(id, out playerData)) return playerData;
            _data.Codes.Add(id, playerData = new PlayerData
            {
                Code = GetRandomCode(),
                Enabled = true
            });
            return playerData;
        }

        #endregion

        #region Command

        private void ChatCommand(BasePlayer player, string label, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                player.ChatMessage(lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                player.ChatMessage(string.Format(lang.GetMessage("InvalidArgs", this, player.UserIDString), label));
                return;
            }

            CreateDataIfAbsent(player.UserIDString);
            switch (args[0].ToLower())
            {
                case "code":
                    OpenCodeLockUI(player);
                    break;
                case "toggle":
                    player.ChatMessage(lang.GetMessage(Toggle(player) ? "Enabled" : "Disabled", this,
                        player.UserIDString));
                    break;
                default:
                    player.ChatMessage(string.Format(lang.GetMessage("InvalidArgs", this, player.UserIDString), label));
                    break;
            }
        }

        private static bool HasCodeLock(BasePlayer player)
        {
            return player.inventory.FindItemID(1159991980) != null;
        }

        private static void TakeCodeLock(BasePlayer player)
        {
            player.inventory.Take(null, 1159991980, 1);
        }

        private void OpenCodeLockUI(BasePlayer player)
        {
            var codeLock = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab",
                player.eyes.position + new Vector3(0, -3, 0)) as CodeLock;
            if (codeLock == null) return;
            codeLock.Spawn();
            codeLock.SetFlag(BaseEntity.Flags.Locked, true);
            codeLock.ClientRPCPlayer(null, player, "EnterUnlockCode");
            if (_awaitingResponse.ContainsKey(player)) _awaitingResponse.Remove(player);
            _awaitingResponse.Add(player, new TimedCodeLock(){CodeLock = codeLock, Expiry = DateTime.UtcNow.AddSeconds(_config.CodeLockExpiry)});
            if (_awaitingResponse.Count == 1) Subscribe("OnCodeEntered");

            timer.In(20f, () =>
            {
                if (!codeLock.IsDestroyed) codeLock.Kill();
            });
        }
        
        [UsedImplicitly]
        private void OnCodeEntered(Object codeLock, BasePlayer player, string code)
        {
            TimedCodeLock timedCodeLock;
            if (player == null || !_awaitingResponse.TryGetValue(player, out timedCodeLock)) return;
            var playerCodeLock = timedCodeLock.CodeLock;
            if (playerCodeLock != codeLock)
            {
                if (!playerCodeLock.IsDestroyed) playerCodeLock.Kill();
                _awaitingResponse.Remove(player);
                return;
            }

            var pData = _data.Codes[player.UserIDString];
            pData.Code = code;
            player.ChatMessage(string.Format(lang.GetMessage("CodeUpdated", this, player.UserIDString),
                player.net.connection.info.GetBool("global.streamermode") ? "****" : code));

            var prefab = playerCodeLock.effectCodeChanged;
            if (!playerCodeLock.IsDestroyed) playerCodeLock.Kill();
            _awaitingResponse.Remove(player);

            Effect.server.Run(prefab.resourcePath, player.transform.position);
            if (_awaitingResponse.Count < 1) Unsubscribe("OnCodeEntered");
        }

        private bool Toggle(BasePlayer player)
        {
            var data = _data.Codes[player.UserIDString];
            var newToggle = !data.Enabled;
            data.Enabled = newToggle;
            return newToggle;
        }

        #endregion

        #region Configuration & Language

        private ConfigFile _config;
        private Data _data;

        private class PlayerData
        {
            public string Code;
            public bool Enabled;
        }

        private class Data
        {
            public readonly Dictionary<string, PlayerData> Codes = new Dictionary<string, PlayerData>();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"CodeAdded", "Codelock placed with code {0}."},
                {"Disabled", "You have disabled auto locks."},
                {"Enabled", "You have enabled auto locks."},
                {"CodeUpdated", "Your new code is {0}."},
                {"NoPermission", "You don't have permission."},
                {"InvalidArgs", "/{0} code|toggle|hide"},
                {"RaidBlocked", "The codelock wasn't automatically locked due to you being raid blocked!"},
                {"CombatBlocked", "The codelock wasn't automatically locked due to you being combat blocked!"}
            }, this);
        }

        public class ConfigFile
        {
            [JsonProperty("Disabled Items (Prefabs)")]
            public List<string> Disabled;

            [JsonProperty("No Escape")] public NoEscapeSettings NoEscapeSettings;
            
            [JsonProperty("Code Lock Expiry Time (Seconds, put -1 if you want to disable)")]
            public float CodeLockExpiry;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    Disabled = new List<string>
                    {
                        "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab"
                    },
                    CodeLockExpiry = 10f,
                    NoEscapeSettings = new NoEscapeSettings
                    {
                        BlockCombat = true,
                        BlockRaid = true
                    }
                };
            }
        }

        public class NoEscapeSettings
        {
            [JsonProperty("Block Auto Lock whilst in Combat?")]
            public bool BlockCombat;

            [JsonProperty("Block Auto Lock whilst Raid Blocked?")]
            public bool BlockRaid;
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
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