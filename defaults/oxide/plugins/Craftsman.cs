using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using System.Linq;
using Rust;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
	[Info("Craftsman", "mr01sam", "1.0.1")]
	[Description("Adds leveling skills to crafting and quality tiers to items")]
	public class Craftsman : CovalencePlugin
	{
		public static Craftsman PLUGIN;

		#region Permissions
		private const string PermissionLevelingMeleeWeapons = "craftsman.leveling.melee";
		private const string PermissionLevelingRangedWeapons = "craftsman.leveling.ranged";
		private const string PermissionLevelingClothing = "craftsman.leveling.clothing";
		private const string PermissionAdmin= "craftsman.admin";
		#endregion

		#region Global Data

		private Dictionary<uint, int> ItemQualities = new Dictionary<uint, int>();

		private Dictionary<uint, float> ClothingItemProtectionModifiers = new Dictionary<uint, float>();

		private Dictionary<ulong, Dictionary<CraftingCategory, float>> PlayerCraftingExperienceToNextLevel = new Dictionary<ulong, Dictionary<CraftingCategory, float>>();

		private Dictionary<ulong, Dictionary<CraftingCategory, float>> PlayerCraftingExperience = new Dictionary<ulong, Dictionary<CraftingCategory, float>>();

		private Dictionary<ulong, Dictionary<CraftingCategory, int>> PlayerCraftingLevel = new Dictionary<ulong, Dictionary<CraftingCategory, int>>();

		private Dictionary<uint, ulong> ItemCreatorIds = new Dictionary<uint, ulong>();

		#endregion

		#region Oxide Hooks
		void Init()
		{
			PLUGIN = this;

			/* Unsubscribe */
			Unsubscribe(nameof(OnItemCraftFinished));
			Unsubscribe(nameof(OnEntityTakeDamage));
		}

		void Unload()
		{
			PLUGIN = null;
		}

		void OnServerSave()
        {
			SaveAll();
        }

		void OnServerInitialized( bool initial )
		{
			/* Register Perms */
			permission.RegisterPermission(PermissionLevelingRangedWeapons, this);
			permission.RegisterPermission(PermissionLevelingMeleeWeapons, this);
			permission.RegisterPermission(PermissionLevelingClothing, this);

			/* Load Existing */
			LoadAll();

			/* Subscribe */
			Subscribe(nameof(OnItemCraftFinished));
			Subscribe(nameof(OnEntityTakeDamage));
		}

		void OnItemCraftFinished( ItemCraftTask task, Item item )
		{
			BaseEntity heldEntity = item.GetHeldEntity();
			BasePlayer basePlayer = task.owner;
			if (basePlayer.IsValid())
			{
				CraftingCategory category = GetCraftingCategory(heldEntity, item);
				if (category != CraftingCategory.None && HasPermForCategory(basePlayer, category))
				{

					int quality = GetCraftingQuality(basePlayer, category);
					int wbLevel = GetWorkbenchLevel(item);
					AdvancePlayerCraftingLevel(basePlayer, category, wbLevel);
					SetItemQuality(item, quality);
					ItemCreatorIds.Add(item.uid, basePlayer.userID);
				}
			}
		}

		object OnEntityTakeDamage( BasePlayer entity, HitInfo info )
		{
			foreach (Item item in entity.inventory.containerWear.itemList)
			{
				/* Protection increase by 1.5 */
				/* Scale by 1.5 - 1 = 0.5 */
				float modifier = ClothingItemProtectionModifiers.ContainsKey(item.uid) ? ClothingItemProtectionModifiers[item.uid] : 1f;
				info.damageTypes.ScaleAll(1f/modifier);
			}
			return null;
		}

		#endregion

		#region Enums

		private enum CraftingCategory
		{
			None = 0,
			ProjectileWeapon = 1,
			MeleeWeapon = 2,
			Clothing = 3
		}

		#endregion

		#region Main Helpers
		private void CraftClothing( float modifier, Item item )
		{
			ClothingItemProtectionModifiers.Add(item.uid, modifier);
		}

		private void CraftProjectileWeapon( float modifier, BaseProjectile itemEntity )
		{
			itemEntity.damageScale *= modifier;
		}

		private void CraftMeleeWeapon( float modifier, BaseMelee itemEntity )
		{
			itemEntity.gathering.Flesh.gatherDamage *= modifier;
			itemEntity.gathering.Ore.gatherDamage *= modifier;
			itemEntity.gathering.Tree.gatherDamage *= modifier;
			foreach (DamageTypeEntry entry in itemEntity.damageTypes) {
				entry.amount *= modifier;
			}
		}
		#endregion

		#region Helper Functions

		private void SetItemQuality(Item item, int quality)
		{
			BaseEntity heldEntity = item.GetHeldEntity();
			CraftingCategory category = GetCraftingCategory(heldEntity, item);
			if (quality > 0)
			{
				ItemQualities.Add(item.uid, quality);
				float modifier = GetCraftingStatModifier(quality);
				item._maxCondition *= modifier;
				item._condition = item._maxCondition;
				item.name = item.info.displayName.translated + " (" + GetQualityName(quality) + ")";
				switch (category)
				{
					case CraftingCategory.Clothing:
						CraftClothing(modifier, item);
						break;
					case CraftingCategory.ProjectileWeapon:
						CraftProjectileWeapon(modifier, (BaseProjectile)heldEntity);
						break;
					case CraftingCategory.MeleeWeapon:
						CraftMeleeWeapon(modifier, (BaseMelee)heldEntity);
						break;
				}
			}
		}

		private CraftingCategory GetCraftingCategory( BaseEntity heldEntity, Item item )
		{
			if (heldEntity == null && item.info.isWearable)
				return CraftingCategory.Clothing;
			if (heldEntity == null)
				return CraftingCategory.None;
			if (heldEntity is BaseProjectile || heldEntity.GetType().IsSubclassOf(typeof(BaseProjectile)))
				return CraftingCategory.ProjectileWeapon;
			if (heldEntity is BaseMelee || heldEntity.GetType().IsSubclassOf(typeof(BaseMelee)))
				return CraftingCategory.MeleeWeapon;

			return CraftingCategory.None;
		}

		private string GetQualityName( int quality )
		{
			return (quality <= 0) ? null : config.QualityTiers.Keys.ElementAt(quality-1);
		}

		private int GetCraftingQuality(BasePlayer basePlayer, CraftingCategory category)
		{
			InitPlayerCrafting(basePlayer);
			int craftLevel = PlayerCraftingLevel[basePlayer.userID][category];
			int numTiers = config.QualityTiers.Count;
			int bracketSize = (int) Math.Ceiling(100f / numTiers);
			int chanceUpper = craftLevel % bracketSize * ((int) Math.Round(100f / bracketSize));
			int lowerTier = (int) Math.Floor((float) craftLevel / bracketSize);
			int randRoll = UnityEngine.Random.Range(1, 100);
			return (chanceUpper >= randRoll) ? lowerTier + 1 : lowerTier;
		}

		private float GetCraftingStatModifier(int quality)
		{
			if (quality == 0)
				return 1f;
			return config.QualityTiers.Values.Count > (quality - 1) ? config.QualityTiers.Values.ElementAt(quality-1) : 1f;
		}

		private int GetWorkbenchLevel( Item item ) {
			return ItemManager.bpList.Where(x => x.targetItem.shortname == item.info.shortname).FirstOrDefault().workbenchLevelRequired;

		}

		private string GetCraftingCategoryName( CraftingCategory category ) {
			switch (category)
			{
				case CraftingCategory.ProjectileWeapon:
					return config.CraftingCategoryNames.Ranged;
				case CraftingCategory.MeleeWeapon:
					return config.CraftingCategoryNames.Melee;
				case CraftingCategory.Clothing:
					return config.CraftingCategoryNames.Clothing;
			}
			return "";
		}

		private CraftingCategory GetCategoryFromName( string inputStr ) {
			foreach (CraftingCategory category in Enum.GetValues(typeof(CraftingCategory)))
			{
				if (category != CraftingCategory.None)
				{
					string categoryName = GetCraftingCategoryName(category);
					if (inputStr.ToLower() == categoryName.ToLower()) {
						return category;
					}
				}
			}
			return CraftingCategory.None;
		}

		private float GetWbLevelWeight( int wbLevel )
		{
			switch (wbLevel)
			{
				case 1:
					return config.WbLevelWeights.Level1Weight;
				case 2:
					return config.WbLevelWeights.Level2Weight;
				case 3:
					return config.WbLevelWeights.Level3Weight;
				default:
					return config.WbLevelWeights.Level0Weight;
			}
		}

		private float CalculateExperienceToNextLevel(int currentLevel) {
			return (float)(1 * Math.Pow((config.XpGrowthRate + 1), currentLevel));
		}

		private bool HasPermForCategory( BasePlayer player, CraftingCategory category ) {
			switch (category)
			{
				case CraftingCategory.ProjectileWeapon:
					return PLUGIN.permission.UserHasPermission(player.UserIDString, PermissionLevelingRangedWeapons);
				case CraftingCategory.MeleeWeapon:
					return PLUGIN.permission.UserHasPermission(player.UserIDString, PermissionLevelingMeleeWeapons);
				case CraftingCategory.Clothing:
					return PLUGIN.permission.UserHasPermission(player.UserIDString, PermissionLevelingClothing);
				default:
					return false;
			}
		}

		private void AdvancePlayerCraftingLevel( BasePlayer basePlayer, CraftingCategory category, int wbLevel )
		{
			InitPlayerCrafting(basePlayer);
			if (PlayerCraftingLevel[basePlayer.userID][category] < 100)
			{
				PlayerCraftingExperience[basePlayer.userID][category] += GetWbLevelWeight(wbLevel);
				int previousLevel = PlayerCraftingLevel[basePlayer.userID][category];
				int currentLevel = previousLevel;
				while (PlayerCraftingExperience[basePlayer.userID][category] >= PlayerCraftingExperienceToNextLevel[basePlayer.userID][category])
				{
					// Levelup
					PlayerCraftingLevel[basePlayer.userID][category] += 1;
					currentLevel = PlayerCraftingLevel[basePlayer.userID][category];
					PlayerCraftingExperience[basePlayer.userID][category] -= PlayerCraftingExperienceToNextLevel[basePlayer.userID][category];
					PlayerCraftingExperienceToNextLevel[basePlayer.userID][category] = CalculateExperienceToNextLevel(currentLevel);
				}
				if (currentLevel > previousLevel)
				{
					basePlayer.ChatMessage(Lang("Levelup", basePlayer.UserIDString, GetCraftingCategoryName(category), previousLevel, currentLevel));
				}
			}
		}

		private void InitPlayerCrafting( BasePlayer basePlayer )
		{
			if (!PlayerCraftingExperience.ContainsKey(basePlayer.userID))
			{
				PlayerCraftingExperience.Add(basePlayer.userID, new Dictionary<CraftingCategory, float>());
				foreach (CraftingCategory cc in Enum.GetValues(typeof(CraftingCategory)))
				{
					PlayerCraftingExperience[basePlayer.userID].Add(cc, 0);
				}
			}
			if (!PlayerCraftingExperienceToNextLevel.ContainsKey(basePlayer.userID))
			{
				PlayerCraftingExperienceToNextLevel.Add(basePlayer.userID, new Dictionary<CraftingCategory, float>());
				foreach (CraftingCategory cc in Enum.GetValues(typeof(CraftingCategory)))
				{
					PlayerCraftingExperienceToNextLevel[basePlayer.userID].Add(cc, config.WbLevelWeights.Level0Weight);
				}
			}
			if (!PlayerCraftingLevel.ContainsKey(basePlayer.userID))
			{
				PlayerCraftingLevel.Add(basePlayer.userID, new Dictionary<CraftingCategory, int>());
				foreach (CraftingCategory cc in Enum.GetValues(typeof(CraftingCategory)))
				{
					PlayerCraftingLevel[basePlayer.userID].Add(cc, 0);
				}
			}
		}

		Item FindItemByUid( uint uid )
		{
			var dropped = GameObject.FindObjectsOfType<DroppedItem>();
			var storage = GameObject.FindObjectsOfType<StorageContainer>();
			for (int i = 0; i < dropped.Length; i++)
			{
				var item = dropped[i]?.item ?? null;
				if (item != null && item.uid == uid)
				{
					return item;
				}
			}
			for (int i = 0; i < storage.Length; i++)
			{
				var container = storage[i]?.inventory?.itemList ?? null;
				for (int j = 0; j < container.Count; j++)
				{
					if (container[j].uid == uid)
					{
						return container[j];
					}
				}
			}
			for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
			{
				var player = BasePlayer.activePlayerList[i];
				var items = player?.inventory?.AllItems() ?? null;
				if (items != null && items.Length > 0)
				{
					for (int j = 0; j < items.Length; j++)
					{
						if (items[j].uid == uid)
						{
							return items[j];
						}
					}
				}
			}
			for (int i = 0; i < BasePlayer.sleepingPlayerList.Count; i++)
			{
				var player = BasePlayer.sleepingPlayerList[i];
				var items = player?.inventory?.AllItems() ?? null;
				if (items != null && items.Length > 0)
				{
					for (int j = 0; j < items.Length; j++)
					{
						if (items[j].uid == uid)
						{
							return items[j];
						}
					}
				}
			}
			return null;
		}

		private void SaveAll()
        {
			Interface.Oxide.DataFileSystem.WriteObject("ItemQualities", ItemQualities);
			Interface.Oxide.DataFileSystem.WriteObject("ClothingItemProtectionModifiers", ClothingItemProtectionModifiers);
			Interface.Oxide.DataFileSystem.WriteObject("PlayerCraftingExperienceToNextLevel", PlayerCraftingExperienceToNextLevel);
			Interface.Oxide.DataFileSystem.WriteObject("PlayerCraftingExperience", PlayerCraftingExperience);
			Interface.Oxide.DataFileSystem.WriteObject("PlayerCraftingLevel", PlayerCraftingLevel);
			Interface.Oxide.DataFileSystem.WriteObject("ItemCreatorIds", ItemCreatorIds);
		}

		private void LoadAll()
        {
			ItemQualities = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<uint, int>>("ItemQualities");
			ClothingItemProtectionModifiers = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<uint, float>>("ClothingItemProtectionModifiers");
			PlayerCraftingExperienceToNextLevel = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<CraftingCategory, float>>>("PlayerCraftingExperienceToNextLevel");
			PlayerCraftingExperience = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<CraftingCategory, float>>>("PlayerCraftingExperience");
			PlayerCraftingLevel = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<CraftingCategory, int>>>("PlayerCraftingLevel");
			ItemCreatorIds = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<uint, ulong>>("ItemCreatorIds");
		}
		#endregion

		#region API Methods
		private float GetClothingItemProtectionModifiers( uint itemUid )
		{
			return ClothingItemProtectionModifiers.ContainsKey(itemUid) ? ClothingItemProtectionModifiers[itemUid] : 1f;
		}

		private bool HasCraftingCategory( BaseEntity heldItem, Item item )
		{
			return GetCraftingCategory(heldItem, item) != CraftingCategory.None;
		}

		private int GetCraftingQualityOfItem( uint itemUid )
		{
			return ItemQualities.ContainsKey(itemUid) ? ItemQualities[itemUid] : 0;
		}

		private int GetMaxCraftingQuality()
		{
			return config.QualityTiers.Count;
		}

		private ulong GetItemCreatorId( uint itemUid )
		{
			return ItemCreatorIds.ContainsKey(itemUid) ? ItemCreatorIds[itemUid] : 0;
		}
		#endregion

		#region Configuration
		private Configuration config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Quality tiers")]
			public Dictionary<string, float> QualityTiers = new Dictionary<string, float>()
			{
				{ "+", 1.1f },
				{ "++", 1.2f },
				{ "+++", 1.3f }
			};

			[JsonProperty(PropertyName = "XP Growth Rate")]
			public float XpGrowthRate = 0.06f;

			[JsonProperty(PropertyName = "Workbench Level XP Weights")]
			public LevelWeights WbLevelWeights = new LevelWeights { Level0Weight = 0.5f, Level1Weight = 1f, Level2Weight = 5f, Level3Weight = 9f };

			[JsonProperty(PropertyName = "Crafting Category Names")]
			public CraftingCategoryNames CraftingCategoryNames = new CraftingCategoryNames { Melee = "Melee Weapons", Ranged = "Ranged Weapons", Clothing = "Clothing" };
		}

		private class LevelWeights
		{
			[JsonProperty(PropertyName = "Level 0 Weight")]
			public float Level0Weight;
			[JsonProperty(PropertyName = "Level 1 Weight")]
			public float Level1Weight;
			[JsonProperty(PropertyName = "Level 2 Weight")]
			public float Level2Weight;
			[JsonProperty(PropertyName = "Level 3 Weight")]
			public float Level3Weight;
		}

		private class CraftingCategoryNames
		{
			[JsonProperty(PropertyName = "Melee Weapons")]
			public string Melee;
			[JsonProperty(PropertyName = "Ranged Weapons")]
			public string Ranged;
			[JsonProperty(PropertyName = "Clothing")]
			public string Clothing;
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();

			try
			{
				config = Config.ReadObject<Configuration>();
				if (config == null) throw new Exception();
			}
			catch
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
			}

			SaveConfig();
		}

		protected override void SaveConfig() => Config.WriteObject(config);
		protected override void LoadDefaultConfig() => config = new Configuration();
		#endregion

		#region Command Helpers
		private void PrintAllSkills( IPlayer player )
		{
			BasePlayer basePlayer = BasePlayer.FindByID(ulong.Parse(player.Id));
			InitPlayerCrafting(basePlayer);
			string message = $"<size=16><color=yellow>{Lang("YourCraftingSkills", player.Id)}:\n</color></size>";
			bool hasOneSkill = false;
			foreach (CraftingCategory category in Enum.GetValues(typeof(CraftingCategory)))
			{
				if (HasPermForCategory(basePlayer, category))
				{
					message += $"<color=orange>{GetCraftingCategoryName(category)}:</color> {PlayerCraftingLevel[basePlayer.userID][category]} / 100\n";
					hasOneSkill = true;
				}
			}
			if (hasOneSkill)
			{
				player.Reply(message);
			}
			else
			{
				player.Reply(Lang("NoPerm", player.Id));
			}
		}

		private void PrintSkill( IPlayer player, string inputStr )
		{
			CraftingCategory category = GetCategoryFromName(inputStr);
			BasePlayer basePlayer = BasePlayer.FindByID(ulong.Parse(player.Id));
			if (category == CraftingCategory.None || !HasPermForCategory(basePlayer, category))
			{
				player.Reply(Lang("NoSkill", player.Id));
				return;
			}
			else
			{
				string categoryName = GetCraftingCategoryName(category);
				InitPlayerCrafting(basePlayer);
				string message = $"<size=16><color=yellow>{Lang("CraftingSkill", player.Id)} - {categoryName}:\n</color></size>";
				message += $"<color=orange>{Lang("Level", player.Id)}: </color>{PlayerCraftingLevel[basePlayer.userID][category]} / 100\n";
				message += $"<color=orange>{Lang("XP", player.Id)}: </color>{Math.Round(PlayerCraftingExperience[basePlayer.userID][category] * 1000)} / {Math.Round(PlayerCraftingExperienceToNextLevel[basePlayer.userID][category] * 1000)}\n";
				player.Reply(message);
			}
		}
		#endregion

		#region Commands
		[Command("skills")]
		private void cmd_skills( IPlayer player, string command, string[] args )
		{
			if (args.Length == 0)
			{
				PrintAllSkills(player);
			}
			else
			{
				string inputStr = string.Join(" ", args);
				PrintSkill(player, inputStr);
			}
		}

		[Command("craftsman.help")]
		private void cmd_help( IPlayer player, string command, string[] args )
		{
			string message = $"<size=16><color=yellow>Craftsman {Lang("Commands", player.Id)}:\n</color></size>";
			message += $"<color=orange>/skills</color>\n";
			message += $"<color=orange>/skills</color> <skill_name></color>\n";
			if (permission.UserHasPermission(player.Id, PermissionAdmin))
			{
				message += $"<color=orange>/craftsman.quality.set</color> <item_uid> <quality_level>\n";
				message += $"<color=orange>/craftsman.skill.set</color> <user_id> <skill_level> <skill_name>\n";
			}
			player.Reply(message); 
		}

		[Command("craftsman.quality.set"), Permission(PermissionAdmin)]
		private void cmd_quality_set( IPlayer player, string command, string[] args )
		{
			uint uid = uint.Parse(args[0]);
			int quality = int.Parse(args[1]);
			if (quality < 0 || quality > config.QualityTiers.Count)
			{
				player.Reply(Lang("InvalidQuality", player.Id));
				return;
			}
			Item item = FindItemByUid(uid);
			if (item == null)
			{
				player.Reply(Lang("NoItem", player.Id));
				return;
			}
			CraftingCategory category = GetCraftingCategory(item.GetHeldEntity(), item);
			if (category == CraftingCategory.None)
			{
				player.Reply(Lang("InvalidSkillName", player.Id));
				return;
			}
			SetItemQuality(item, quality);
			player.Reply(Lang("SetQuality", player.Id, item.name, quality));
		}

		[Command("craftsman.skill.set"), Permission(PermissionAdmin)]
		private void cmd_skill_set( IPlayer player, string command, string[] args )
		{
			ulong userId = ulong.Parse(args[0]);
			int level = int.Parse(args[1]);
			string skill = string.Join(" ", args.Skip(2));
			if (level < 0 || level > 100)
			{
				player.Reply(Lang("InvalidLevel", player.Id));
				return;
			}
			CraftingCategory category = GetCategoryFromName(skill);
			if (category == CraftingCategory.None)
			{
				player.Reply(Lang("InvalidSkillName", player.Id));
				return;
			}
			BasePlayer basePlayer = BasePlayer.FindByID(userId);
			if (!basePlayer.IsValid())
			{
				player.Reply(Lang("InvalidUserId", player.Id));
				return;
			}
			InitPlayerCrafting(basePlayer);
			PlayerCraftingLevel[userId][category] = level;
			PlayerCraftingExperience[userId][category] = 0;
			PlayerCraftingExperienceToNextLevel[userId][category] = CalculateExperienceToNextLevel(level+1);
			player.Reply(Lang("SetLevel", player.Id, GetCraftingCategoryName(category), basePlayer.displayName, level));
		}
		#endregion

		#region Localization
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["NoPerm"] = "You don't have permission for this command",
				["NoSkill"] = "There is no skill with that name",
				["NoItem"] = "Could not find item",
				["CraftingSkill"] = "Crafting Skill",
				["Level"] = "Level",
				["XP"] = "XP",
				["Commands"] = "Commands",
				["InvalidQuality"] = "Invalid quality",
				["InvalidLevel"] = "Invalid level",
				["InvalidItem"] = "Invalid item",
				["InvalidSkillName"] = "Invalid skill name",
				["InvalidUserId"] = "Invalid user id",
				["SetQuality"] = "Set {0} to quality {1}",
				["SetLevel"] = "Set {0} level for {1} to {2}",
				["Levelup"] = "Your skill in crafting {0} increased from {1} to {2}",
				["YourCraftingSkills"] = "Your Crafting Skills"
			}, this);
		}

		private string Lang( string key, string id = null, params object[] args ) => string.Format(lang.GetMessage(key, this, id), args);
		#endregion
	}

}
