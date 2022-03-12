using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System.Linq;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Trashcan", "Smallo", "1.0.4")]
    [Description("Turn a Large Wood Box into a trashcan")]
    public class Trashcan : CovalencePlugin
    {
		#region Variables
        private ConfigData config;
		private int defaultMin = 225;
		private int defaultMax = 285;
		private int sizeIncrement = 62;
		private string breakEffect = "assets/bundled/prefabs/fx/entities/loot_barrel/gib.prefab";
		private const string trashCommandPermission = "trashcan.use";
        #endregion
		
		#region NoEscape
        [PluginReference] private Plugin NoEscape;
        private bool CurrentlyRaidBlocked(BasePlayer player)
        {
            return NoEscape?.Call<bool>("IsRaidBlocked", player) ?? false;
        }
        #endregion
		
		#region Startup		
		private void Loaded()
		{
			if (NoEscape == null || !NoEscape.IsLoaded)
				LogError("'No Escape' is not loaded, it is required for the raid block part of the script. If you intend to use the raid block config option please get 'No Escape' at https://umod.org/plugins/no-escape");
		}
		#endregion
		
		#region Configuration
        private class ConfigData
        {
            [JsonProperty(PropertyName = "SkinID")]
            public ulong TrashSkinID = 1595211850;

            [JsonProperty(PropertyName = "Trashcan Storage Slots")]
            public int TrashStorageSlots = 12;
			
            [JsonProperty("Enable Effects")]
            public bool EnableEffects = true;
			
            [JsonProperty("Lock Box While In Use (This prevents someone else from deleting your items)")]
            public bool LockBox = true;
			
            [JsonProperty("Disable During Raid Block (Requires No Escape plugin)")]
            public bool RaidBlock = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
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
            config = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion Configuration
		
		#region localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TrashHeadText"] = "TRASHCAN",
                ["TrashSubText"] = "<color=#FF4433>Warning:</color> When you close this menu, everything in the storage will be deleted!",
                ["TrashNoBoxText"] = "<color=#FFFF00>Trash Can:</color> You do not have a Large Wooden Box in your hand.",
                ["TrashNoItemText"] = "<color=#FFFF00>Trash Can:</color> You do not have anything in your hand.",
                ["TrashCreatedText"] = "<color=#FFFF00>Trash Can:</color> You have turned your currently held Large Wooden Box into a trashcan.",
                ["TrashAlreadyText"] = "<color=#FFFF00>Trash Can:</color> This Large Wooden Box is already a trashcan.",
                ["RaidBlockedText"] = "<color=#FFFF00>Trash Can:</color> You are currently raid blocked, your items will not be deleted until you reuse the box after the raid is over."
            }, this);
        }
        #endregion localization
		
		#region Hooks
		private void OnLootEntity(BasePlayer player, StorageContainer storageContainer)
		{
			var baseEntity = storageContainer as BaseEntity;
			if (baseEntity.skinID != config.TrashSkinID)
				return;
			
			var itemContainer = storageContainer.inventory as ItemContainer;
			itemContainer.capacity = config.TrashStorageSlots;
			int addAmount = (RoundUp(config.TrashStorageSlots, 6) / 6) - 1;
			
			if (config.LockBox)
				itemContainer.entityOwner.SetFlag(BaseEntity.Flags.Locked, true);
			
			CreateUI(addAmount, player);
		}
		
		private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            var player = inventory.baseEntity;
            if (player == null) return;
			
            foreach (ItemContainer container in inventory.containers)
			{
				if (container == null) continue;
				if (container.entityOwner == null) continue;
				
				if (container.entityOwner.skinID == config.TrashSkinID) {
					CuiHelper.DestroyUi(player, "TrashOverlay");
					int itemCount = container.itemList.Count;
					
					if (config.RaidBlock && CurrentlyRaidBlocked(player) && itemCount > 0) {
						if (config.LockBox)
							container.entityOwner.SetFlag(BaseEntity.Flags.Locked, false);
						
						player.ChatMessage(lang.GetMessage("RaidBlockedText",this, player.UserIDString));
						return;
					}
					
					if (!container.entityOwner.IsBusy()) {
						if (config.EnableEffects && itemCount > 0)
							Effect.server.Run(breakEffect, container.entityOwner.GetNetworkPosition());
						
						if (config.LockBox)
							container.entityOwner.SetFlag(BaseEntity.Flags.Locked, false);
						
						container.Clear();
					}
				}
			}
        }
		
		#endregion
		
		#region UI
		private CuiPanel TrashPanel(string anchorMin, string anchorMax, string color = "0 0 0 0", string offsetMin = "0", string offsetMax = "0")
        {
            return new CuiPanel
            {
                Image =
                {
                    Color = color
                },
                RectTransform =
                {
					OffsetMin = offsetMin,
					OffsetMax = offsetMax,
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                }
            };
        }
		
		private CuiLabel TrashLabel(string text, int i, float height, TextAnchor align = TextAnchor.MiddleLeft, int fontSize = 13, string xMin = "0", string xMax = "1", string color = "1.0 1.0 1.0 1.0")
        {
            return new CuiLabel
            {
                Text =
                {
                    Text = text,
                    FontSize = fontSize,
                    Align = align,
                    Color = color
                },
                RectTransform =
                {
                    AnchorMin = $"{xMin} {1 - height * i + i * 0.002f}",
                    AnchorMax = $"{xMax} {1 - height * (i - 1) + i * 0.002f}"
                }
            };
        }
		#endregion
		
		#region Functions
		private void CreateUI(int addAmount, BasePlayer player){
			CuiElementContainer TrashUI = new CuiElementContainer();
			TrashUI.Add(TrashPanel("0.5 0", "0.5 0", "1 0.96 0.88 0.15", $"192.5 {defaultMin + (sizeIncrement * addAmount)}", $"572.5 {defaultMax + (sizeIncrement * addAmount)}"), "Hud.Menu", "TrashOverlay");
			TrashUI.Add(TrashPanel("0 0.5", "1 1", "0.6 0.6 0.6 0.25"), "TrashOverlay", "TrashTextHeading");
			TrashUI.Add(TrashLabel(lang.GetMessage("TrashHeadText", this, player.UserIDString), 1, 1, TextAnchor.MiddleCenter, 18, "0", "1", "0.97 0.92 0.88 1"), "TrashTextHeading");
			TrashUI.Add(TrashPanel("0 0", "1 0.5", "0 0 0 0"), "TrashOverlay", "TrashTextSubtitle");
			TrashUI.Add(TrashLabel(lang.GetMessage("TrashSubText", this, player.UserIDString), 1, 1, TextAnchor.MiddleCenter, 11, "0", "1", "0.97 0.92 0.88 1"), "TrashTextSubtitle");
			CuiHelper.AddUi(player, TrashUI);
		}
		
		private int RoundUp(int numToRound, int multiple)
		{
			if (multiple == 0)
				return numToRound;

			int remainder = numToRound % multiple;
			if (remainder == 0)
				return numToRound;

			return numToRound + multiple - remainder;
		}
        #endregion
		
		#region Commands
		[Command("trash"), Permission(trashCommandPermission)]
		private void TrashCmd(IPlayer player, string command, string[] args)
        {
			BasePlayer bPlayer = player.Object as BasePlayer;
			
			var activeItem = bPlayer.GetActiveItem() as Item;
			string userString = bPlayer.UserIDString;
			if (activeItem == null) {
				bPlayer.ChatMessage(lang.GetMessage("TrashNoItemText", this, userString));
				return;
			}
			
			if (activeItem.info.shortname != "box.wooden.large") {
				bPlayer.ChatMessage(lang.GetMessage("TrashNoBoxText", this, userString));
				return;
			}
			
			if (activeItem.skin == config.TrashSkinID)
			{
				bPlayer.ChatMessage(lang.GetMessage("TrashAlreadyText", this, userString));
				return;
			}
			
			bPlayer.ChatMessage(lang.GetMessage("TrashCreatedText", this, userString));
			activeItem.skin = config.TrashSkinID;
            activeItem.MarkDirty();
		}
        #endregion
    }
}