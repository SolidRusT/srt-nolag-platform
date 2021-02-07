using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Frame Box", "MJSU", "1.0.1")]
    [Description("Deploys a frame on a box allowing the player to set the image")]
    internal class FrameBox : RustPlugin
    {
        #region Class Fields
        private StoredData _storedData; //Plugin Data
        private PluginConfig _pluginConfig; //Plugin Config

        private static FrameBox _ins;

        private const string UsePermission = "framebox.use";
        private const string NoCostPermission = "framebox.nocost";
        private const string AccentColor = "#de8732";

        private readonly Hash<uint, BoxData> _boxes = new Hash<uint, BoxData>();
        private readonly Hash<uint, FrameData> _frames = new Hash<uint, FrameData>();
        private readonly Hash<string, ContainerData> _containerPositions = new Hash<string, ContainerData>();
        #endregion

        #region Setup & Loading
        private void Init()
        {
            _ins = this;
            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

            permission.RegisterPermission(UsePermission, this);
            permission.RegisterPermission(NoCostPermission, this);

            cmd.AddChatCommand(_pluginConfig.ChatCommand, this, SignBoxChatCommand);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Chat] = $"<color=#bebebe>[<color={AccentColor}>{Title}</color>] {{0}}</color>",
                [LangKeys.NoPermission] = "You do not have permission to use this command",
                [LangKeys.MissingItem] = "{0}: {1}x",
                [LangKeys.MissingMessage] =
                    "You do not have enough items to add a frame to this box. You're missing:\n{0}",
                [LangKeys.CostPre] = "Cost to make:\n",
                [LangKeys.Cost] = "{0}: {1}x",
                [LangKeys.AddInvalidSyntax] =
                    $"Invalid Syntax: <color={AccentColor}>/{{0}} {{2}} front</color> - to add the frame to the front of the box" +
                    "\nAvailable frame positions for this container are {1}",
                [LangKeys.InvalidAddFramePosition] = "Invalid Frame Position. Available frame positions are:\n {0}",
                [LangKeys.NotLookingAt] = "You're not looking at a box or frame",
                [LangKeys.AddSlotIsTaken] = "The frame position {0} is already taken. Open positions are:\n{1}",
                [LangKeys.AddSuccess] = "You have successfully added a frame onto your box in position {0}. " +
                                        "In order to access the box using the frame you need to first lock the frame." +
                                        "The only way to unlock the frame is using the /fb unlock command",
                [LangKeys.RemoveSuccess] = "You have successfully removed the frame from position {0}",
                [LangKeys.UnlockBuildingBlocked] = "Cannot unlock in a building blocked zone",
                [LangKeys.UnlockSuccess] = "You have successfully unlocked the sign.",
                [LangKeys.NotLookingAtFrame] = "You're not looking at a frame.",
                [LangKeys.NotLookingAtFrameBoxFrame] = "You're not looking at a frame box frame",
                [LangKeys.NotLookingAtFrameBox] = "You're not looking at a frame box or frame box frame",
                [LangKeys.NoFrameInPosition] = "There is no frame in position {0}",
                [LangKeys.ContainerNotAllowed] = "This container is not allowed to have a frame on it.",
                [LangKeys.NoUnlockPermission] = "You do not have permission to unlock this sign.",
                [LangKeys.AddSubCommand] = "add",
                [LangKeys.RemoveSubCommand] = "remove",
                [LangKeys.CostSubCommand] = "cost",
                [LangKeys.UnlockSubCommand] = "unlock",
                [LangKeys.AllSubOption] = "all",
                [LangKeys.HelpText] =
                    "Allows placing photo frames on boxes. After settings the sign and locking it you can now access the box while looking at the sign.\n" +
                    $"<color={AccentColor}>/{{0}} {{1}}</color> to see the available positions to palace a sign\n" +
                    $"<color={AccentColor}>/{{0}} {{1}} right</color> to place a sign on the right\n" +
                    $"<color={AccentColor}>/{{0}} {{1}} frontleft</color> to place a sign on the front left\n" +
                    $"<color={AccentColor}>/{{0}} {{2}}</color> to remove a sign you're looking at\n" +
                    $"<color={AccentColor}>/{{0}} {{3}}</color> to get the cost of the sign\n" +
                    $"<color={AccentColor}>/{{0}} {{4}}</color> to unlock the sign to allow edits again\n" +
                    $"<color={AccentColor}>/{{0}}</color> to see this help text again\n"
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        //TODO: Fix config back to normal
        public PluginConfig AdditionalConfig(PluginConfig config)
        {
            config.Cost = config.Cost ?? new Hash<string, int>
            {
                ["wood"] = 150
            };

            config.SignPositions = new Hash<string, ContainerData>
            {
                ["box.wooden.large"] = new ContainerData
                {
                    ItemShortname = "photoframe.portrait",
                    PositionData = new Hash<string, PositionData>
                    {
                        ["Left"] = new PositionData
                        {
                            Offset = new Vector3(0.95f, 0.4f, 0),
                            Rotation = new Vector3(0, 90, 90),
                            IncludeAll = true
                        },
                        ["Right"] = new PositionData
                        {
                            Offset = new Vector3(-0.95f, 0.4f, 0),
                            Rotation = new Vector3(0, 270, 90),
                            IncludeAll = true
                        },
                        ["Front"] = new PositionData
                        {
                            Offset = new Vector3(0, 0.4f, 0.55f),
                            Rotation = new Vector3(0, 0, 90),
                            IncludeAll = false
                        },
                        ["FrontRight"] = new PositionData
                        {
                            Offset = new Vector3(-0.45f, 0.4f, 0.55f),
                            Rotation = new Vector3(0, 0, 90),
                            IncludeAll = true
                        },
                        ["FrontLeft"] = new PositionData
                        {
                            Offset = new Vector3(0.45f, 0.4f, 0.55f),
                            Rotation = new Vector3(0, 0, 90),
                            IncludeAll = true
                        },
                        ["Back"] = new PositionData
                        {
                            Offset = new Vector3(0, 0.4f, -0.55f),
                            Rotation = new Vector3(0, 180, 90),
                            IncludeAll = false
                        },
                        ["BackRight"] = new PositionData
                        {
                            Offset = new Vector3(-0.45f, 0.4f, -0.55f),
                            Rotation = new Vector3(0, 180, 90),
                            IncludeAll = true
                        },
                        ["BackLeft"] = new PositionData
                        {
                            Offset = new Vector3(0.45f, 0.4f, -0.55f),
                            Rotation = new Vector3(0, 180, 90),
                            IncludeAll = true
                        },
                        ["Top"] = new PositionData
                        {
                            Offset = new Vector3(0, 0.775f, 0),
                            Rotation = new Vector3(270, 0, 90),
                            IncludeAll = false
                        },
                        ["TopRight"] = new PositionData
                        {
                            Offset = new Vector3(-0.45f, 0.775f, 0),
                            Rotation = new Vector3(270, 0, 90),
                            IncludeAll = true
                        },
                        ["TopLeft"] = new PositionData
                        {
                            Offset = new Vector3(0.45f, 0.775f, 0),
                            Rotation = new Vector3(270, 0, 90),
                            IncludeAll = true
                        }
                    }
                },
                ["box.wooden"] = new ContainerData
                {
                    ItemShortname = "photoframe.portrait",
                    PositionData = new Hash<string, PositionData>
                    {
                        ["Left"] = new PositionData
                        {
                            Offset = new Vector3(0.49f, 0.35f, 0),
                            Rotation = new Vector3(0, 90, 90),
                            IncludeAll = true
                        },
                        ["Right"] = new PositionData
                        {
                            Offset = new Vector3(-0.49f, 0.35f, 0),
                            Rotation = new Vector3(0, 270, 90),
                            IncludeAll = true
                        },
                        ["Front"] = new PositionData
                        {
                            Offset = new Vector3(0, 0.35f, .39f),
                            Rotation = new Vector3(0, 0, 90),
                            IncludeAll = true
                        },
                        ["Back"] = new PositionData
                        {
                            Offset = new Vector3(0, 0.35f, -.39f),
                            Rotation = new Vector3(0, 180, 90),
                            IncludeAll = true
                        },
                        ["Top"] = new PositionData
                        {
                            Offset = new Vector3(0, 0.6f, 0),
                            Rotation = new Vector3(270, 0, 90),
                            IncludeAll = true
                        }
                    }
                }
            };
            return config;
        }

        private void OnServerInitialized()
        {
            foreach (KeyValuePair<string, ContainerData> entityPositions in _pluginConfig.SignPositions)
            {
                string containerShortname = GetShortPrefabName(entityPositions.Key);
                if (string.IsNullOrEmpty(containerShortname))
                {
                    continue;
                }

                string prefabName = GetPrefabName(entityPositions.Value.ItemShortname);
                if (string.IsNullOrEmpty(prefabName))
                {
                    continue;
                }

                Hash<string, PositionData> positions = new Hash<string, PositionData>();
                foreach (KeyValuePair<string, PositionData> position in entityPositions.Value.PositionData)
                {
                    PositionData data = position.Value;
                    data.DisplayName = position.Key;
                    positions[position.Key.ToLower()] = data;
                }

                ContainerData containerData = new ContainerData
                {
                    ItemShortname = prefabName,
                    PositionData = positions
                };

                _containerPositions[containerShortname] = containerData;
            }

            foreach (KeyValuePair<uint, Hash<string, uint>> data in _storedData.FrameData.ToList())
            {
                BoxStorage box = BaseNetworkable.serverEntities.Find(data.Key) as BoxStorage;
                if (box == null)
                {
                    _storedData.FrameData.Remove(data.Key);
                    continue;
                }

                BoxData boxData = new BoxData(box);

                foreach (KeyValuePair<string, uint> framePos in data.Value.ToList())
                {
                    PhotoFrame frame = BaseNetworkable.serverEntities.Find(framePos.Value) as PhotoFrame;
                    if (frame == null)
                    {
                        data.Value.Remove(framePos.Key);
                        continue;
                    }

                    if (!boxData.AddFrame(frame, framePos.Key))
                    {
                        data.Value.Remove(framePos.Key);
                        frame.Kill();
                    }
                }
            }

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }

        private void OnNewSave(string filename)
        {
            _storedData = new StoredData();
            SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            NextTick(() =>
            {
                if (player.IsAdmin || player.IsDeveloper)
                {
                    player.gameObject.AddComponent<AdminBoxBehavior>();
                }
            });
        }

        private void Unload()
        {
            foreach (AdminBoxBehavior adminBoxBehavior in GameObject.FindObjectsOfType<AdminBoxBehavior>())
            {
                adminBoxBehavior.DoDestroy();
            }

            SaveData();
            _ins = null;
        }
        #endregion

        #region Chat Command
        private void SignBoxChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!HasPermission(player, UsePermission))
            {
                Chat(player, LangKeys.NoPermission);
                return;
            }

            if (args.Length == 0)
            {
                HandleHelp(player);
                return;
            }

            string subCommand = args[0];
            if (subCommand.Equals(Lang(LangKeys.AddSubCommand, player), StringComparison.InvariantCultureIgnoreCase))
            {
                HandleAdd(player, args);
            }
            else if (subCommand.Equals(Lang(LangKeys.RemoveSubCommand, player), StringComparison.InvariantCultureIgnoreCase))
            {
                HandleRemove(player, args);
            }
            else if (subCommand.Equals(Lang(LangKeys.CostSubCommand, player), StringComparison.InvariantCultureIgnoreCase))
            {
                HandleCost(player);
            }
            else if (subCommand.Equals(Lang(LangKeys.UnlockSubCommand, player), StringComparison.InvariantCultureIgnoreCase))
            {
                HandleUnlock(player, args);
            }
            else
            {
               HandleHelp(player);
            }
        }

        public void HandleHelp(BasePlayer player)
        {
            Chat(player, LangKeys.HelpText, _pluginConfig.ChatCommand, 
                Lang(LangKeys.AddSubCommand, player), 
                Lang(LangKeys.RemoveSubCommand, player), 
                Lang(LangKeys.CostSubCommand, player), 
                Lang(LangKeys.UnlockSubCommand, player));
        }

        public void HandleAdd(BasePlayer player, string[] args)
        {
            BaseCombatEntity box = Raycast<IItemContainerEntity>(player) as BaseCombatEntity;
            if (box == null)
            {
                Chat(player, LangKeys.NotLookingAt);
                return;
            }

            if (box is PhotoFrame)
            {
                FrameData data = _frames[box.net.ID];
                if (data == null)
                {
                    Chat(player, LangKeys.NotLookingAt);
                    return;
                }

                box = data.BoxData.Box;
            }
            
            ContainerData containerData = _containerPositions[box.ShortPrefabName];
            if (containerData == null)
            {
                Chat(player, LangKeys.ContainerNotAllowed);
                return;
            }

            if (args.Length < 2)
            {
                Chat(player, LangKeys.AddInvalidSyntax, _pluginConfig.ChatCommand, string.Join(", ", containerData.PositionData.Values.Select(cd => cd.DisplayName).ToArray()), Lang(LangKeys.AddSubCommand, player));
                return;
            }

            string pos = args[1].ToLower();
            if (!containerData.PositionData.ContainsKey(pos) && !pos.Equals(Lang(LangKeys.AllSubOption, player), StringComparison.InvariantCultureIgnoreCase))
            {
                Chat(player, LangKeys.InvalidAddFramePosition, string.Join(", ", containerData.PositionData.Values.Select(cd => cd.DisplayName).ToArray()));
                return;
            }

            BoxData boxData = _boxes[box.net.ID] ?? new BoxData(box);
            if (boxData.IsSlotTaken(pos))
            {
                Chat(player, LangKeys.AddSlotIsTaken, pos, string.Join(", ", boxData.GetOpenPositions(false)));
                return;
            }

            Hash<string, uint> frameData = _storedData.FrameData[box.net.ID];
            if (frameData == null)
            {
                frameData = new Hash<string, uint>();
                _storedData.FrameData[box.net.ID] = frameData;
            }

            if (pos.Equals(Lang(LangKeys.AllSubOption, player), StringComparison.InvariantCultureIgnoreCase))
            {
                if (!HasPermission(player, NoCostPermission) && !CanAfford(player, _pluginConfig.Cost, boxData.GetOpenPositions(true).Length))
                {
                    return;
                }

                foreach (KeyValuePair<string, PositionData> positionData in containerData.PositionData)
                {
                    if (!positionData.Value.IncludeAll)
                    {
                        continue;
                    }

                    frameData[positionData.Key] = AddFrame(boxData, containerData.ItemShortname, positionData.Value, positionData.Key);
                }
            }
            else
            {
                if (!HasPermission(player, NoCostPermission) && !CanAfford(player, _pluginConfig.Cost, 1))
                {
                    return;
                }

                PositionData framePos = containerData.PositionData[pos];
                frameData[pos] = AddFrame(boxData, containerData.ItemShortname, framePos, pos);
            }

            Chat(player, LangKeys.AddSuccess, pos);
            SaveData();
        }

        public uint AddFrame(BoxData boxData, string prefabName, PositionData framePos, string pos)
        {
            PhotoFrame frame = GameManager.server.CreateEntity(prefabName, boxData.Box.transform.TransformPoint(framePos.Offset), boxData.Box.transform.rotation * Quaternion.Euler(framePos.Rotation)) as PhotoFrame;
            frame.Spawn();
            boxData.AddFrame(frame, pos);
            return frame.net.ID;
        }

        public void HandleRemove(BasePlayer player, string[] args)
        {
            BoxData boxData;
            FrameData frameData;

            if (!GetFrameBox(player, out boxData, out frameData))
            {
                return;
            }

            string[] framesToRemove = null;
            if (args.Length == 1)
            {
                if (frameData == null)
                {
                    Chat(player, LangKeys.NotLookingAtFrame);
                    return;
                }
                
                framesToRemove = new [] {frameData.Position};
            }
            else if(args.Length > 1)
            {
                framesToRemove = ParseFramePositions(player, boxData, args[1].ToLower());
            }

            if (framesToRemove == null)
            {
                return;
            }

            int removed = 0;
            foreach (string pos in framesToRemove)
            {
                if (boxData.RemoveFrame(pos, true))
                {
                    removed++;
                }
            }

            if (removed == 0)
            {
                Chat(player, LangKeys.NoFrameInPosition, args[1].ToLower());
                return;
            }
            
            if (_pluginConfig.RefundOnRemove)
            {
                foreach (KeyValuePair<string, int> cost in _pluginConfig.Cost)
                {
                    int amount = Mathf.FloorToInt(cost.Value * _pluginConfig.RefundPercentage * removed);
                    if (amount <= 0)
                    {
                        continue;
                    }

                    Item item = ItemManager.CreateByName(cost.Key, amount);
                    player.GiveItem(item);
                }
            }
            
            Chat(player, LangKeys.RemoveSuccess, args[1]);
            SaveData();
        }

        public void HandleCost(BasePlayer player)
        {
            StringBuilder sb = new StringBuilder(Lang(LangKeys.CostPre, player));

            foreach (KeyValuePair<string, int> item in _pluginConfig.Cost)
            {
                sb.AppendLine(Lang(LangKeys.Cost, player, ItemManager.FindItemDefinition(item.Key)?.displayName.translated, item.Value));
            }

            Chat(player, sb.ToString());
        }

        public void HandleUnlock(BasePlayer player, string[] args)
        {
            BuildingPrivlidge priv = player.GetBuildingPrivilege();
            if (priv != null && !priv.IsAuthed(player))
            {
                Chat(player, LangKeys.UnlockBuildingBlocked);
                return;
            }

            BoxData boxData;
            FrameData frameData;

            if (!GetFrameBox(player, out boxData, out frameData))
            {
                return;
            }

            string[] framesToUnlock = null;
            if (args.Length == 1)
            {
                if (frameData == null)
                {
                    Chat(player, LangKeys.NotLookingAtFrame);
                    return;
                }
                
                framesToUnlock = new [] {frameData.Position};
            }
            else if(args.Length > 1)
            {
                framesToUnlock = ParseFramePositions(player, boxData, args[1].ToLower());
            }

            if (framesToUnlock == null)
            {
                return;
            }

            int unlocked = 0;
            foreach (string frame in framesToUnlock)
            {
                if (boxData.GetFrame(frame)?.Unlock(player) ?? false)
                {
                    unlocked++;
                }
            }

            if (unlocked == 0)
            {
                Chat(player, LangKeys.NoUnlockPermission);
                return;
            }
            

            Chat(player, LangKeys.UnlockSuccess);
        }

        private bool GetFrameBox(BasePlayer player, out BoxData boxData, out FrameData frameData)
        {
            boxData = null;
            frameData = null;

            BaseCombatEntity box = Raycast<IItemContainerEntity>(player) as BaseCombatEntity;
            if (box == null)
            {
                Chat(player, LangKeys.NotLookingAt);
                return false;
            }

            if (box is PhotoFrame)
            {
                frameData = _frames[box.net.ID];
                if (frameData == null)
                {
                    Chat(player, LangKeys.NotLookingAtFrameBoxFrame);
                    return false;
                }

                boxData = frameData.BoxData;
            }
            else
            {
                boxData = _boxes[box.net.ID];
                if (boxData == null)
                {
                    Chat(player, LangKeys.NotLookingAtFrameBox);
                    return false;
                }
            }

            return true;
        }

        public string[] ParseFramePositions(BasePlayer player, BoxData box, string pos)
        {
            if (pos.Equals(Lang(LangKeys.AllSubOption, player)))
            {
                return box.GetTakenFramePositions();
            }
            
            FrameData frameData = box.GetFrame(pos);
            if (frameData == null)
            {
                Chat(player, LangKeys.NoFrameInPosition, pos);
                return null;
            }

            return new []{pos};
        }
        #endregion

        #region Hooks
        private object CanLootEntity(BasePlayer player, PhotoFrame frame)
        {
            if (frame == null || !frame.IsLocked())
            {
                return null;
            }

            IItemContainerEntity box = _frames[frame.net.ID]?.BoxData.Box as IItemContainerEntity;
            if (box == null)
            {
                return null;
            }

            box.PlayerOpenLoot(player);
            return true;
        }

        private void OnEntityKill(BoxStorage box)
        {
            _boxes[box.net.ID]?.OnKilled();
        }

        private void OnEntityKill(PhotoFrame frame)
        {
            _frames[frame.net.ID]?.OnKilled();
        }

        private void OnSignLocked(PhotoFrame frame, BasePlayer player)
        {
            FrameData frameData = _frames[frame.net.ID];
            if (frameData == null)
            {
                return;
            }

            frame.OwnerID = 0;
        }
        #endregion

        #region Sign Artist Hooks
        private object GetImageRotation(PhotoFrame frame)
        {
            if (!_frames.ContainsKey(frame.net.ID))
            {
                return null;
            }

            return RotateFlipType.Rotate270FlipNone;
        }
        #endregion

        #region Magic Remove Hooks
        private Hash<string, int> GetRemoveAdditionalRefund(BoxStorage entity, Hash<string, int> refund)
        {
            BoxData box = _boxes[entity.net.ID];
            if (box == null)
            {
                return null;
            }

            int frameCount = box.GetFrameCount();
            foreach (KeyValuePair<string, int> item in _pluginConfig.Cost)
            {
                int amount = Mathf.FloorToInt(item.Value * frameCount * _pluginConfig.RefundPercentage);
                if (amount == 0)
                {
                    continue;
                }

                refund[item.Key] += amount;
            }

            return refund;
        }
        #endregion

        #region Handle Paying
        public bool CanAfford(BasePlayer player, Hash<string, int> cost, int multiplier)
        {
            bool canAfford = true;
            StringBuilder missingItems = new StringBuilder();
            foreach (KeyValuePair<string, int> item in cost)
            {
                ItemDefinition def = ItemManager.FindItemDefinition(item.Key);
                int amount = player.inventory.GetAmount(def.itemid);
                if (amount < item.Value * multiplier)
                {
                    string itemName = ItemManager.FindItemDefinition(item.Key).displayName.english;
                    missingItems.AppendLine(Lang(LangKeys.MissingItem, player, itemName,
                        item.Value * multiplier - amount));
                    canAfford = false;
                }
            }

            if (!canAfford)
            {
                Chat(player, Lang(LangKeys.MissingMessage, player, missingItems));
                return false;
            }

            TakeCost(player, cost, multiplier);

            return true;
        }

        public void TakeCost(BasePlayer player, Hash<string, int> cost, int multiplier)
        {
            List<Item> items = Pool.GetList<Item>();
            foreach (KeyValuePair<string, int> item in cost)
            {
                ItemDefinition def = ItemManager.FindItemDefinition(item.Key);
                player.inventory.Take(items, def.itemid, item.Value * multiplier);
                player.Command("note.inv", def.itemid, -item.Value * multiplier);
            }

            foreach (Item item in items)
            {
                item.Remove();
            }

            Pool.FreeList(ref items);
        }
        #endregion

        #region Helper Methods
        public string GetShortPrefabName(string shortname)
        {
            ItemDefinition def = ItemManager.FindItemDefinition(shortname);
            if (def == null)
            {
                PrintWarning($"{shortname} is not a valid item shortname!");
                return null;
            }

            string prefabName = def.GetComponent<ItemModDeployable>()?.entityPrefab.Get().GetComponent<BaseEntity>()
                .ShortPrefabName;
            if (string.IsNullOrEmpty(prefabName))
            {
                PrintWarning($"{shortname} does not contain a deployable item and cannot be used");
                return null;
            }

            return prefabName;
        }

        public string GetPrefabName(string shortname)
        {
            ItemDefinition def = ItemManager.FindItemDefinition(shortname);
            if (def == null)
            {
                PrintWarning($"{shortname} is not a valid item shortname!");
                return null;
            }

            string prefabName = def.GetComponent<ItemModDeployable>()?.entityPrefab.Get().GetComponent<BaseEntity>()
                .PrefabName;
            if (string.IsNullOrEmpty(prefabName))
            {
                PrintWarning($"{shortname} does not contain a deployable item and cannot be used");
                return null;
            }

            return prefabName;
        }

        public T Raycast<T>(BasePlayer player) where T : class
        {
            RaycastHit[] hits = Physics.RaycastAll(player.eyes.HeadRay(), _pluginConfig.MaxDistance);
            GamePhysics.Sort(hits);
            foreach (RaycastHit hit in hits)
            {
                BaseEntity entity = hit.GetEntity();
                if (entity == null)
                {
                    continue;
                }

                T type = entity as T;
                if (type == null)
                {
                    continue;
                }

                if (Vector3.Distance(player.eyes.position, entity.transform.position) > _pluginConfig.MaxDistance + 3)
                {
                    continue;
                }

                return type;
            }

            return default(T);
        }

        public void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);

        public void Chat(BasePlayer player, string key, params object[] args) =>
            PrintToChat(player, Lang(LangKeys.Chat, player, Lang(key, player, args)));

        public string Lang(string key, BasePlayer player = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, player?.UserIDString), args);
            }
            catch (Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception:\n{ex}");
                throw;
            }
        }

        public bool HasPermission(BasePlayer player, string perm) =>
            permission.UserHasPermission(player.UserIDString, perm);
        #endregion

        #region Data Classes
        public class FrameData
        {
            public PhotoFrame Frame { get; }
            public BoxData BoxData { get; }
            public string Position { get; }

            public FrameData(PhotoFrame frame, BoxData boxData, string position)
            {
                Frame = frame;
                BoxData = boxData;
                Position = position;
                DestroyGroundWatch(Frame);
                _ins._frames[frame.net.ID] = this;
            }

            public bool Unlock(BasePlayer player)
            {
                if (Frame.OwnerID != 0 && !Frame.CanUnlockSign(player))
                {
                    return false;
                }

                Frame.SetFlag(BaseEntity.Flags.Locked, false);
                Frame.SendNetworkUpdate();
                return true;
            }

            private void DestroyGroundWatch(BaseEntity entity)
            {
                DestroyOnGroundMissing missing = entity.GetComponent<DestroyOnGroundMissing>();
                if (missing != null)
                {
                    GameObject.Destroy(missing);
                }

                GroundWatch watch = entity.GetComponent<GroundWatch>();
                if (watch != null)
                {
                    GameObject.Destroy(watch);
                }
            }

            public void OnKilled()
            {
                BoxData.RemoveFrame(Position, false);
            }

            public void RemoveFrame(bool kill)
            {
                _ins._frames.Remove(Frame.net.ID);
                _ins._storedData.FrameData.Remove(Frame.net.ID);
                if (kill)
                {
                    Frame.Kill();
                }
            }
        }

        public class BoxData
        {
            public BaseCombatEntity Box { get; }
            private Hash<string, FrameData> BoxFrames { get; }

            public BoxData(BaseCombatEntity box)
            {
                Box = box;
                BoxFrames = new Hash<string, FrameData>();
                _ins._boxes[Box.net.ID] = this;
            }

            public FrameData GetFrame(string position)
            {
                return BoxFrames[position];
            }

            public bool AddFrame(PhotoFrame frame, string position)
            {
                if (IsSlotTaken(position))
                {
                    return false;
                }

                BoxFrames[position] = new FrameData(frame, this, position);
                return true;
            }

            public bool RemoveFrame(string position, bool kill)
            {
                FrameData frame = BoxFrames[position];
                if (frame == null)
                {
                    return false;
                }

                frame.RemoveFrame(kill);
                BoxFrames.Remove(position);
                
                Hash<string, uint> data = _ins._storedData.FrameData[Box.net.ID];
                if (data != null)
                {
                    data.Remove(position);
                    if (data.Count == 0)
                    {
                        _ins._storedData.FrameData.Remove(Box.net.ID);
                        _ins._boxes.Remove(Box.net.ID);
                    }
                }
                
                return true;
            }

            public bool IsSlotTaken(string position)
            {
                return BoxFrames.ContainsKey(position);
            }

            public string[] GetOpenPositions(bool allOnly)
            {
                return _ins._containerPositions[Box.ShortPrefabName].PositionData
                    .Where(fp => !BoxFrames.ContainsKey(fp.Key) && (!allOnly || fp.Value.IncludeAll))
                    .Select(fp => fp.Value.DisplayName)
                    .ToArray();
            }

            public string[] GetTakenFramePositions()
            {
                return BoxFrames.Keys.ToArray();
            }

            public int GetFrameCount()
            {
                return BoxFrames.Count;
            }

            public void OnKilled()
            {
                foreach (string frame in BoxFrames.Keys.ToList())
                {
                    RemoveFrame(frame, true);
                }

                _ins._boxes.Remove(Box.net.ID);
                _ins._storedData.FrameData.Remove(Box.net.ID);
            }
        }
        #endregion

        #region Admin Behavior
        //Players with Admin or Developer flag can always edit signs. We use this to open box for them.
        public class AdminBoxBehavior : FacepunchBehaviour
        {
            private BasePlayer Player { get; set; }
            private bool IsDown { get; set; }

            private void Awake()
            {
                Player = GetComponent<BasePlayer>();
            }

            private void Update()
            {
                if (Player.inventory.loot.IsLooting())
                {
                    return;
                }

                bool isDown = Player.serverInput.IsDown(BUTTON.USE);
                if (isDown == IsDown)
                {
                    return;
                }

                IsDown = isDown;
                if (!IsDown)
                {
                    return;
                }

                PhotoFrame frame = _ins.Raycast<PhotoFrame>(Player);
                if (frame == null || !frame.IsLocked())
                {
                    return;
                }

                FrameData frameData = _ins._frames[frame.net.ID];
                if (frameData == null)
                {
                    return;
                }

                StartCoroutine(OpenBox(frame, frameData.BoxData.Box as IItemContainerEntity));
            }

            //Needed because we need to be sure the sign is destroyed on the client before we try to open the box
            private IEnumerator OpenBox(PhotoFrame frame, IItemContainerEntity box)
            {
                frame.DestroyOnClient(Player.Connection);
                yield return null;
                yield return null;

                box?.PlayerOpenLoot(Player);

                yield return new WaitForSeconds(0.05f);

                frame.SendNetworkUpdate();
            }

            public void DoDestroy()
            {
                Destroy(this);
            }
        }
        #endregion

        #region Classes
        public class PluginConfig
        {
            [DefaultValue("fb")]
            [JsonProperty(PropertyName = "Chat Command")]
            public string ChatCommand { get; set; }

            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Refund on remove")]
            public bool RefundOnRemove { get; set; }

            [DefaultValue(1f)]
            [JsonProperty(PropertyName = "Refund % (0 none - 1 max)")]
            public float RefundPercentage { get; set; }

            [DefaultValue(5f)]
            [JsonProperty(PropertyName = "Max distance (Meters)")]
            public float MaxDistance { get; set; }

            [JsonProperty(PropertyName = "Cost")]
            public Hash<string, int> Cost { get; set; }

            [JsonProperty(PropertyName = "Sign Positions")]
            public Hash<string, ContainerData> SignPositions { get; set; }
        }

        public class StoredData
        {
            public Hash<uint, Hash<string, uint>> FrameData = new Hash<uint, Hash<string, uint>>();
        }

        public class ContainerData
        {
            [JsonProperty(PropertyName = "Frame Item Shortname")]
            public string ItemShortname { get; set; }

            [JsonProperty(PropertyName = "Frame Positions")]
            public Hash<string, PositionData> PositionData { get; set; }
        }

        public class PositionData
        {
            [JsonConverter(typeof(Vector3Converter))]
            [JsonProperty(PropertyName = "Frame Position Offset")]
            public Vector3 Offset { get; set; }

            [JsonConverter(typeof(Vector3Converter))]
            [JsonProperty(PropertyName = "Frame Position Rotation")]
            public Vector3 Rotation { get; set; }

            [JsonProperty(PropertyName = "Include Position In All Option")]
            public bool IncludeAll { get; set; }

            [JsonIgnore]
            public string DisplayName { get; set; }
        }

        public class LangKeys
        {
            public const string Chat = nameof(Chat);
            public const string NoPermission = nameof(NoPermission);
            public const string HelpText = nameof(HelpText) + "_V1";
            public const string MissingItem = nameof(MissingItem);
            public const string MissingMessage = nameof(MissingMessage);
            public const string CostPre = nameof(CostPre) + "_V1";
            public const string Cost = nameof(Cost);
            public const string AddInvalidSyntax = nameof(AddInvalidSyntax) + "_V1";
            public const string InvalidAddFramePosition = nameof(InvalidAddFramePosition) + "_V1";
            public const string NotLookingAt = nameof(NotLookingAt);
            public const string AddSlotIsTaken = nameof(AddSlotIsTaken);
            public const string AddSuccess = nameof(AddSuccess);
            public const string RemoveSuccess = nameof(RemoveSuccess);
            public const string UnlockBuildingBlocked = nameof(UnlockBuildingBlocked);
            public const string UnlockSuccess = nameof(UnlockSuccess);
            public const string NotLookingAtFrame = nameof(NotLookingAtFrame);
            public const string NotLookingAtFrameBoxFrame = nameof(NotLookingAtFrameBoxFrame);
            public const string NotLookingAtFrameBox = nameof(NotLookingAtFrameBox);
            public const string NoFrameInPosition = nameof(NoFrameInPosition);
            public const string ContainerNotAllowed = nameof(ContainerNotAllowed) + "_V1";
            public const string NoUnlockPermission = nameof(NoUnlockPermission);
            public const string AddSubCommand = nameof(AddSubCommand);
            public const string RemoveSubCommand = nameof(RemoveSubCommand);
            public const string CostSubCommand = nameof(CostSubCommand);
            public const string UnlockSubCommand = nameof(UnlockSubCommand);
            public const string AllSubOption = nameof(AllSubOption);
        }
        #endregion

        #region JSON Converters
        public class Vector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                Vector3 pos = (Vector3) value;
                writer.WriteValue($"{pos.x} {pos.y} {pos.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
                JsonSerializer serializer)
            {
                string[] values = reader.Value.ToString().Trim().Split(' ');
                return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]),
                    Convert.ToSingle(values[2]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        }
        #endregion
    }
}