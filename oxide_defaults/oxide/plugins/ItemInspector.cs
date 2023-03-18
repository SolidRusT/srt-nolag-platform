using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Linq;
using System.Text;
using Rust;

namespace Oxide.Plugins
{
	[Info("Item Inspector", "mr01sam", "1.0.0")]
	[Description("Allows players to inspect items to see their true stats")]
	public class ItemInspector : CovalencePlugin
	{
		[PluginReference]
		private readonly Plugin Craftsman;

		#region Permissions
		private const string PermissionUse = "iteminspector.use";
		private const string PermissionAdmin = "iteminspector.admin";
		#endregion

		#region Oxide Hooks
		void OnServerInitialized( bool initial )
		{
			permission.RegisterPermission(PermissionUse, this);
			permission.RegisterPermission(PermissionAdmin, this);
		}
		#endregion

		#region API Methods
		private Dictionary<string, object> GetItemData( Item item )
		{
			ItemData data = new ItemData();
			BaseEntity heldEntity = item.GetHeldEntity();
			ItemCategory itemCategory = item.info.category;
			if (true)
			{
				data.AddMetadata("category", item.info.category);
				data.AddMetadata("uid", item.uid);
				data.AddMetadata("short_name", item.info.shortname);
				data.AddMetadata("id", item.info.itemid);
				data.AddInfo("display_name", item.info.displayName.translated);
			}
			if (heldEntity != null)
			{
				data.AddMetadata("object", heldEntity.GetType().ToString());
			}
			if (item._maxCondition > 0)
			{
				data.AddInfo("condition", item._condition);
				data.AddInfo("max_condition", item._maxCondition);
			}
			if (Craftsman && (bool)Craftsman.Call("HasCraftingCategory", heldEntity, item))
			{
				data.AddInfo("quality", (int)Craftsman.Call("GetCraftingQualityOfItem", item.uid));
				data.AddInfo("max_quality", (int)Craftsman.Call("GetMaxCraftingQuality"));
			}
			if (item.info.stackable > 1)
			{
				data.AddInfo("amount", item.amount);
				data.AddInfo("max_amount", item.info.stackable);
			}
			if (item.skin != 0)
			{
				data.AddMetadata("skin", item.skin);
			}
			if (new List<ItemCategory>() {
				ItemCategory.Tool,
				ItemCategory.Weapon
			}.Contains(itemCategory) && heldEntity != null && IsClassOf(heldEntity, typeof(BaseMelee)))
			{
				BaseMelee baseMelee = (BaseMelee)heldEntity;
				if (baseMelee.gathering.Tree.gatherDamage > 0 || baseMelee.gathering.Flesh.gatherDamage > 0 || baseMelee.gathering.Ore.gatherDamage > 0)
				{
					data.AddInfo("gather_rate", new Dictionary<string, float>() {
						{ "tree", baseMelee.gathering.Tree.gatherDamage },
						{ "ore", baseMelee.gathering.Ore.gatherDamage },
						{ "flesh", baseMelee.gathering.Flesh.gatherDamage }
					});
				}
				float totalDamage = 0;
				foreach (DamageTypeEntry dt in baseMelee.damageTypes)
				{
					totalDamage += dt.amount;
				}
				if (totalDamage > 0)
				{
					data.AddInfo("damage", totalDamage);
				}
			}
			if (new List<ItemCategory>() {
				ItemCategory.Weapon
			}.Contains(itemCategory) && heldEntity != null && IsClassOf(heldEntity, typeof(ProjectileWeaponMod)))
			{
				ProjectileWeaponMod projectileWeaponMod = (ProjectileWeaponMod)heldEntity;
				if (projectileWeaponMod.projectileDamage.scalar > 0 || projectileWeaponMod.projectileVelocity.scalar > 0 || projectileWeaponMod.projectileDistance.scalar > 0)
				{
					data.AddInfo("modifiers", new Dictionary<string, float>()
					{
						{ "damage", projectileWeaponMod.projectileDamage.scalar },
						{ "velocity", projectileWeaponMod.projectileVelocity.scalar },
						{ "distance", projectileWeaponMod.projectileDistance.scalar }
					});
				}
			}

			if (new List<ItemCategory>() {
				ItemCategory.Weapon,
				ItemCategory.Tool
			}.Contains(itemCategory) && heldEntity != null && IsClassOf(heldEntity, typeof(AttackEntity)))
			{

				List<string> mods = new List<string>();
				float scaleAmount = 1f;
				if (IsClassOf(heldEntity, typeof(BaseProjectile)))
				{
					BaseProjectile baseProjectile = (BaseProjectile)heldEntity;

					if (item.contents != null && item.contents.itemList != null && item.contents.itemList.Count > 0)
					{
						foreach (Item item2 in item.contents.itemList)
						{
							mods.Add(item2.info.displayName.translated);
						}
						data.AddInfo("mods", mods.ToArray());
					}
				}
				if (WeaponDamages.ContainsKey(item.info.shortname))
				{
					float damageBase = WeaponDamages[item.info.shortname];
					float damageScaled = damageBase * scaleAmount;
					if (item.contents != null && item.contents.itemList != null)
					{
						foreach (Item item2 in item.contents.itemList)
						{
							if (((ProjectileWeaponMod)item2.GetHeldEntity()).projectileDamage.scalar > 0)
							{
								damageScaled *= ((ProjectileWeaponMod)item2.GetHeldEntity()).projectileDamage.scalar;
							}
						}
					}
					data.AddInfo("damage", damageScaled);
				}
			}
			if (Craftsman)
			{
				ulong creatorId = (ulong)Craftsman.Call("GetItemCreatorId", item.uid);
				if (creatorId != 0)
				{
					BasePlayer basePlayer = BasePlayer.FindByID(creatorId);
					if (basePlayer != null)
					{
						data.AddInfo("creator", basePlayer.displayName);
					}
				}
			}
			if (new List<ItemCategory>() {
				ItemCategory.Attire,
			}.Contains(itemCategory) && ClothingProtection.ContainsKey(item.info.shortname))
			{
				ClothingResistances prot = ClothingProtection[item.info.shortname];
				data.AddInfo("protection", new Dictionary<string, float>() {
						{ "projectile", prot.Projectile },
						{ "melee", prot.Melee },
						{ "bite", prot.Bite },
						{ "cold", prot.Cold },
						{ "radiation", prot.Radiation },
						{ "explosion", prot.Explosion }
					});
			}

			return new Dictionary<string, object>()
			{
				{ "info", data.info },
				{ "metadata", data.metadata },
			};
		}
		#endregion

		#region Classes
		private class ItemData {
			public readonly Dictionary<string, object> metadata;
			public readonly Dictionary<string, object> info;

			public ItemData(  )
			{
				this.metadata = new Dictionary<string, object>();
				this.info = new Dictionary<string, object>();
			}

			public void AddInfo( string key, object value ) {
				this.info.Add(key, value);
			}

			public void AddMetadata( string key, object value )
			{
				this.metadata.Add(key, value);
			}
		}

		#endregion

		#region Helpers
		private bool IsClassOf(object entity, Type type) {
			return entity.GetType() == type || entity.GetType().IsSubclassOf(type);
		}

		private string Size( int size, string text )
		{
			return $"<size={size}>{text}</size>";
		}

		private string Color( string color, string text )
		{
			return $"<color={color}>{text}</color>";
		}

		private string Header( string titlestr, string color )
		{
			return Size(16, Color(color, titlestr + ":\n"));
		}

		private string Label( string labelstr, string color ) {
			return Color(color, labelstr + ": ");
		}

		private string Text( string statstr, string color) {
			return Color(color, statstr + "\n");
		}

		private string FormatList( string[] elements ) {
			return String.Join(" | ", elements.ToArray());
		}

		private string FormatDict( string[] keys, string[] values )
		{
			return String.Join(" | ", (from k in keys select $"{values[Array.IndexOf(keys, k)]} {k}").ToArray());
		}

		private string FormatString( string str ) {
			return str;
		}

		private string FormatRatio( string left, string right )
		{
			return $"{left}/{right}";
		}

		private string LabelText( string label, string text, string color ) {
			return $"{Label(label, config.Colors.Labels)}{Text(text, color)}";
		}

		private void PrintItemStats( IPlayer player, Dictionary<string, object> data )
		{
			StringBuilder message = new StringBuilder();
			
			if (data.ContainsKey("info") && (permission.UserHasPermission(player.Id, PermissionUse) || permission.UserHasPermission(player.Id, PermissionAdmin))) {
				message.Append($"{Header(Lang("inspect", player.Id), config.Colors.Title)}");
				foreach (KeyValuePair<string, object> entry in (Dictionary<string, object>) data["info"])
				{
					switch (entry.Key)
					{
						case "condition":
							message.Append(LabelText(
								Lang(entry.Key, player.Id),
								FormatRatio(entry.Value.ToString(), ((Dictionary<string, object>)data["info"])["max_condition"].ToString()),
								config.Colors.Ratios
							));
							break;
						case "quality":
							message.Append(LabelText(
								Lang(entry.Key, player.Id),
								FormatRatio(entry.Value.ToString(), ((Dictionary<string, object>)data["info"])["max_quality"].ToString()),
								config.Colors.Ratios
							));
							break;
						case "amount":
							message.Append(LabelText(
								Lang(entry.Key, player.Id),
								FormatRatio(entry.Value.ToString(), ((Dictionary<string, object>)data["info"])["max_amount"].ToString()),
								config.Colors.Ratios
							));
							break;
						case "gather_rate":
							message.Append(LabelText(
								Lang(entry.Key, player.Id),
								FormatDict(
									(from k in ((Dictionary<string, float>) entry.Value).Keys select Lang(k.ToString(), player.Id)).ToArray(),
									(from v in ((Dictionary<string, float>) entry.Value).Values select v.ToString()).ToArray()
								),
								config.Colors.Mappings
							));
							break;
						case "modifiers":
							message.Append(LabelText(
								Lang(entry.Key, player.Id),
								FormatDict(
									(from k in ((Dictionary<string, float>)entry.Value).Keys select Lang(k.ToString(), player.Id)).ToArray(),
									(from v in ((Dictionary<string, float>)entry.Value).Values select v.ToString()).ToArray()
								),
								config.Colors.Mappings
							));
							break;
						case "mods":
							message.Append(LabelText(
								Lang(entry.Key, player.Id),
								FormatList(
									(from v in (string[]) entry.Value select v.ToString()).ToArray()
								),
								config.Colors.Lists
							));
							break;
						case "protection":
							message.Append(LabelText(
								Lang(entry.Key, player.Id),
								FormatDict(
									(from k in ((Dictionary<string, float>)entry.Value).Keys select Lang(k.ToString(), player.Id)).ToArray(),
									(from v in ((Dictionary<string, float>)entry.Value).Values select v.ToString()).ToArray()
								),
								config.Colors.Mappings
							));
							break;
						default:
							if (!entry.Key.StartsWith("max_"))
							{
								message.Append(LabelText(
									Lang(entry.Key, player.Id),
									FormatString(entry.Value.ToString()),
									config.Colors.Info
								));
							}
							break;
					}
				}
			}
			if (data.ContainsKey("metadata") && permission.UserHasPermission(player.Id, PermissionAdmin))
			{
				foreach (KeyValuePair<string, object> entry in (Dictionary<string, object>)data["metadata"])
				{
					switch (entry.Key)
					{
						default:
							if (!entry.Key.StartsWith("max_"))
							{
								message.Append(LabelText(
									Lang(entry.Key, player.Id),
									FormatString(entry.Value.ToString()),
									config.Colors.Metadata
								));
							}
							break;
					}
				}
			}

			player.Reply(message.ToString());
		}
		#endregion

		#region Commands
		[Command("inspect")]
		private void cmd_inspect( IPlayer player, string command, string[] args )
		{
			if (permission.UserHasPermission(player.Id, PermissionUse) || permission.UserHasPermission(player.Id, PermissionAdmin))
			{
				BasePlayer basePlayer = BasePlayer.FindByID(ulong.Parse(player.Id));
				Item activeItem = basePlayer.GetActiveItem();
				if (activeItem == null)
				{
					player.Reply(Lang("no_active_item", player.Id));
					return;
				}
				PrintItemStats(player, GetItemData(activeItem));
			}
			else
			{
				player.Reply(Lang("no_perms", player.Id));
			}
		}
		#endregion

		#region Config
		private Configuration config;
		private class Configuration
		{
			[JsonProperty(PropertyName = "Colors")]
			public ColorsConfig Colors = new ColorsConfig();


			public class ColorsConfig
			{
				[JsonProperty(PropertyName = "Title")]
				public string Title { get; set; } = "#cf391f";

				[JsonProperty(PropertyName = "Labels")]
				public string Labels { get; set; } = "#CACACA";

				[JsonProperty(PropertyName = "Info")]
				public string Info { get; set; } = "#e6b265";

				[JsonProperty(PropertyName = "Metadata")]
				public string Metadata { get; set; } = "#d683a7";

				[JsonProperty(PropertyName = "Lists")]
				public string Lists { get; set; } = "#69c7e0";

				[JsonProperty(PropertyName = "Ratios")]
				public string Ratios { get; set; } = "#93ed96";

				[JsonProperty(PropertyName = "Mappings")]
				public string Mappings { get; set; } = "#d36dde";
			}
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

		#region Localization
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["inspect"] = "Inspected",
				["category"] = "Category",
				["uid"] = "UID",
				["short_name"] = "Short Name",
				["id"] = "ID",
				["display_name"] = "Name",
				["object"] = "Object",
				["condition"] = "Condition",
				["quality"] = "Quality",
				["amount"] = "Amount",
				["skin"] = "Skin",
				["gather_rate"] = "Gather Rate",
				["damage"] = "Damage",
				["modifiers"] = "Modifiers",
				["mods"] = "Mods",
				["creator"] = "Creator",
				["protection"] = "Protection",
				["no_active_item"] = "You must have an item selected in your hotbar to use this command",
				["no_perms"] = "You do not have permission to use this command",
				["tree"] = "Tree",
				["flesh"] = "Flesh",
				["ore"] = "Ore",
				["velocity"] = "Velocity",
				["distance"] = "Distance",
				["projectile"] = "Projectile",
				["melee"] = "Melee",
				["bite"] = "Bite",
				["cold"] = "Cold",
				["radiation"] = "Radiation",
				["explosion"] = "Explosion"
			}, this);
		}

		private string Lang( string key, string id = null, params object[] args ) => string.Format(lang.GetMessage(key, this, id), args);
		#endregion

		#region Data
		private Dictionary<string, float> WeaponDamages = new Dictionary<string, float>()
		{
			{ "rifle.ak", 50 },
			{ "rifle.bolt", 80 },
			{ "rifle.lr300", 40 },
			{ "rifle.l96", 80 },
			{ "rifle.m39", 50 },
			{ "rifle.semiauto", 40 },
			{ "pistol.m92", 45 },
			{ "pistol.eoka", 180 },
			{ "pistol.python", 55 },
			{ "pistol.revolver", 35 },
			{ "pistol.nailgun", 18 },
			{ "pistol.semiauto", 40 },
			{ "lmg.m249", 65 },
			{ "smg.mp5", 35 },
			{ "smg.thompson", 37 },
			{ "smg.2", 30 },
			{ "bow.compound", 100 },
			{ "bow.hunting", 40 },
			{ "crossbow", 60 },
			{ "shotgun.pump", 180 },
			{ "shotgun.spas12", 117 },
			{ "shotgun.double", 180 },
			{ "shotgun.waterpipe", 180 },
			{ "multiplegrenadelauncher", 90 },
			{ "rocket.launcher", 350 },
			{ "snowballgun", 25 },
			{ "grenade.f1", 225 },
			{ "grenade.beancan", 115 },
			{ "explosive.satchel", 475 },
			{ "explosive.timed", 550 },
			{ "surveycharge", 20 }
		};

		private class ClothingResistances
		{
			[JsonProperty(PropertyName = "Projectile")]
			public float Projectile = 0f;
			[JsonProperty(PropertyName = "Melee")]
			public float Melee = 0f;
			[JsonProperty(PropertyName = "Bite")]
			public float Bite = 0f;
			[JsonProperty(PropertyName = "Radiation")]
			public float Radiation = 0f;
			[JsonProperty(PropertyName = "Explosion")]
			public float Explosion = 0f;
			[JsonProperty(PropertyName = "Cold")]
			public float Cold = 0f;
		}

		private Dictionary<string, ClothingResistances> ClothingProtection = new Dictionary<string, ClothingResistances>
		{
			{ "metal.plate.torso", new ClothingResistances { Projectile = 25f, Melee = 20f, Bite = 3f, Cold = -8f } },
			{ "metal.facemask", new ClothingResistances { Projectile = 50f, Melee = 70f, Bite = 8f, Cold = -4f, Explosion = 8f } },
			{ "hazmatsuit", new ClothingResistances { Projectile = 30f, Melee = 30f, Bite = 8f, Radiation = 50f, Cold = 8, Explosion = 5f } },
			{ "mask.bandana", new ClothingResistances { Projectile = 5f, Melee = 10f, Bite = 3f, Radiation = 3f, Cold = 10} },
			{ "attire.banditguard", new ClothingResistances { Projectile = 30f, Melee = 30f, Bite = 15f, Radiation = 50f, Cold = 8, Explosion = 17f } },
			{ "hat.cap", new ClothingResistances { Projectile = 10f, Melee = 10f, Bite = 3f, Radiation = 1f, Cold = 7 } },
			{ "hat.beenie", new ClothingResistances { Projectile = 10f, Melee = 10f, Bite = 3f, Radiation = 1f, Cold = 7 } },
			{ "hat.boonie", new ClothingResistances { Projectile = 10f, Melee = 10f, Bite = 3f, Radiation = 1f, Cold = 7 } },
			{ "shoes.boots", new ClothingResistances { Projectile = 10f, Melee = 10f, Bite = 3f, Radiation = 3f, Cold = 8 } },
			{ "bone.armor.suit", new ClothingResistances { Projectile = 25f, Melee = 40f, Bite = 13f, Radiation = 4f, Explosion = 7f } },
			{ "deer.skull.mask", new ClothingResistances { Projectile = 25f, Melee = 40f, Bite = 13f, Radiation = 4f, Explosion = 7f } },
			{ "bucket.helmet", new ClothingResistances { Projectile = 20f, Melee = 50f, Bite = 8f, Radiation = 4f, Cold = 6f, Explosion = 8f } },
			{ "burlap.gloves.new", new ClothingResistances { Melee = 5f, Bite = 2f, Radiation = 3f, Cold = 4f } },
			{ "burlap.headwrap", new ClothingResistances { Projectile = 15f, Melee = 15f, Bite = 3f, Radiation = 2f, Cold = 7f } },
			{ "burlap.shirt", new ClothingResistances { Projectile = 10f, Melee = 10f, Bite = 3f, Radiation = 2f, Cold = 7f } },
			{ "burlap.shoes", new ClothingResistances { Projectile = 5f, Melee = 5f, Bite = 2f, Radiation = 2f, Cold = 3f } },
			{ "burlap.trousers", new ClothingResistances { Projectile = 10f, Melee = 10f, Bite = 3f, Radiation = 2f, Cold = 7f } },
			{ "movembermoustachecard", new ClothingResistances { Projectile = 5f, Melee = 10f, Bite = 3f, Radiation = 3f, Cold = 10f } },
			{ "clatter.helmet", new ClothingResistances { Projectile = 20f, Melee = 50f, Bite = 8f, Radiation = 4f, Cold = 6f, Explosion = 8f } },
			{ "coffeecan.helmet", new ClothingResistances { Projectile = 35f, Melee = 50f, Bite = 8f, Radiation = 5f, Explosion = 8f } },
			{ "hat.dragonmask", new ClothingResistances { Projectile = 30f, Melee = 60f, Bite = 10f, Radiation = 4f, Cold = 6f, Explosion = 13f } },
			{ "boots.frog", new ClothingResistances { Radiation = 5f, Cold = 8f } },
			{ "hat.gas.mask", new ClothingResistances { Projectile = 20f, Melee = 50f, Bite = 8f, Radiation = 4f, Cold = 6f, Explosion = 8f } },
			{ "ghostsheet", new ClothingResistances { Projectile = 20f, Melee = 15f, Bite = 6f, Radiation = 5f, Cold = 8f } },
			{ "twitch.headset", new ClothingResistances { Projectile = 20f, Melee = 25f, Bite = 7f, Radiation = 5f, Cold = 10f } },
			{ "heavy.plate.helmet", new ClothingResistances { Projectile = 90f, Melee = 80f, Bite = 13f, Radiation = 7f, Cold = -17f, Explosion = 17f } },
			{ "heavy.plate.jacket", new ClothingResistances { Projectile = 75f, Melee = 70f, Bite = 12f, Radiation = 7f, Cold = -17f, Explosion = 17f } },
			{ "heavy.plate.pants", new ClothingResistances { Projectile = 75f, Melee = 70f, Bite = 12f, Radiation = 7f, Cold = -17f, Explosion = 17f } },
			{ "scientistsuit_heavy", new ClothingResistances { Projectile = 30f, Melee = 30f, Bite = 15f, Radiation = 50f, Cold = 8f, Explosion = 17f } },
			{ "attire.hide.boots", new ClothingResistances { Projectile = 5f, Melee = 5f, Bite = 3f, Radiation = 2f, Cold = 5f } },
			{ "attire.hide.helterneck", new ClothingResistances { Projectile = 10f, Melee = 10f, Bite = 3f, Radiation = 2f, Cold = 7f } },
			{ "attire.hide.pants", new ClothingResistances { Projectile = 10f, Melee = 10f, Bite = 8f, Radiation = 2f, Cold = 4f } },
			{ "attire.hide.poncho", new ClothingResistances { Projectile = 10f, Melee = 40f, Bite = 5f, Radiation = 8f, Cold = 8f, Explosion = 5f } },
			{ "attire.hide.skirt", new ClothingResistances { Projectile = 10f, Melee = 10f, Bite = 3f, Radiation = 2f, Cold = 7f } },
			{ "attire.hide.vest", new ClothingResistances { Projectile = 15f, Melee = 15f, Bite = 8f, Radiation = 3f, Cold = 5f } },
			{ "horse.shoes.advanced", new ClothingResistances { Projectile = 5f, Melee = 5f, Bite = 3f, Radiation = 2f, Cold = 5f } },
			{ "hoodie", new ClothingResistances { Projectile = 20f, Melee = 15f, Bite = 6f, Radiation = 5f, Cold = 8f } },
			{ "mask.balaclava", new ClothingResistances { Projectile = 15f, Melee = 15f, Bite = 3f, Radiation = 3f, Cold = 13f, Explosion = 3f } },
			{ "jacket", new ClothingResistances { Projectile = 15f, Melee = 20f, Bite = 7f, Radiation = 5f, Cold = 10f } },
			{ "jumpsuit.suit", new ClothingResistances { Projectile = 25f, Melee = 40f, Bite = 13f, Radiation = 4f, Explosion = 7f } },
			{ "burlap.gloves", new ClothingResistances { Projectile = 5f, Melee = 5f, Bite = 2f, Radiation = 4f, Cold = 5f } },
			{ "tshirt.long", new ClothingResistances { Projectile = 15f, Melee = 15f, Bite = 6f, Radiation = 5f, Cold = 10f } },
			{ "hat.oxmask", new ClothingResistances { Projectile = 30f, Melee = 60f, Bite = 10f, Radiation = 4f, Cold = 6f, Explosion = 13f } },
			{ "movembermoustache", new ClothingResistances { Projectile = 5f, Melee = 10f, Bite = 3f, Radiation = 3f, Cold = 10f } },
			{ "halloween.mummysuit", new ClothingResistances { Melee = 10f, Bite = 5f, Radiation = 15f, Cold = 13f } },
			{ "attire.nesthat", new ClothingResistances { Projectile = 30f, Melee = 60f, Bite = 10f, Radiation = 4f, Cold = 6f, Explosion = 13f } },
			{ "nightvisiongoggles", new ClothingResistances { Projectile = 15f, Melee = 20f, Bite = 3f, Cold = 4f } },
			{ "attire.ninja.suit", new ClothingResistances { Projectile = 25f, Melee = 30f, Bite = 8f, Radiation = 15f, Cold = 8f, Explosion = 5f } },
			{ "pants", new ClothingResistances { Projectile = 15f, Melee = 15f, Bite = 3f, Radiation = 5f, Cold = 8f } },
			{ "roadsign.jacket", new ClothingResistances { Projectile = 20f, Melee = 25f, Bite = 10f, Cold = -8f } },
			{ "roadsign.kilt", new ClothingResistances { Projectile = 20f, Melee = 25f, Bite = 10f, Cold = -8f } },
			{ "hat.ratmask", new ClothingResistances { Projectile = 30f, Melee = 60f, Bite = 10f, Radiation = 4f, Cold = 6f, Explosion = 13f } },
			{ "attire.reindeer.headband", new ClothingResistances { Projectile = 15f, Melee = 15f, Bite = 3f, Radiation = 2f, Cold = 7f } },
			{ "riot.helmet", new ClothingResistances { Projectile = 25f, Melee = 80f, Bite = 13f, Radiation = 5f, Cold = 6f, Explosion = 8f } },
			{ "roadsign.gloves", new ClothingResistances { Projectile = 10f, Melee = 25f, Bite = 10f, Cold = -8f } },
			{ "santabeard", new ClothingResistances { Projectile = 5f, Melee = 10f, Bite = 3f, Radiation = 3f, Cold = 10f } },
			{ "santahat", new ClothingResistances { Projectile = 25f, Melee = 40f, Bite = 13f, Radiation = 4f, Explosion = 7f } },
			{ "scarecrow.suit", new ClothingResistances { Projectile = 10f, Bite = 5f, Radiation = 15f, Cold = 13f } },
			{ "scarecrowhead", new ClothingResistances { Projectile = 30f, Melee = 60f, Bite = 10f, Radiation = 4f, Cold = 6f, Explosion = 13f } },
			{ "hazmatsuit_scientist", new ClothingResistances { Projectile = 30f, Melee = 30f, Bite = 8f, Radiation = 50f, Cold = 8f, Explosion = 5f } },
			{ "hazmatsuit_scientist_peacekeeper", new ClothingResistances { Projectile = 30f, Melee = 30f, Bite = 8f, Radiation = 50f, Cold = 8f, Explosion = 5f } },
			{ "shirt.collared", new ClothingResistances { Projectile = 15f, Melee = 15f, Bite = 5f, Radiation = 3f, Cold = 6f } },
			{ "pants.shorts", new ClothingResistances { Projectile = 10f, Melee = 10f, Bite = 3f, Radiation = 2f, Cold = 7f } },
			{ "jacket.snow", new ClothingResistances { Projectile = 20f, Melee = 30f, Bite = 5f, Radiation = 20f, Cold = 17f } },
			{ "tactical.gloves", new ClothingResistances { Projectile = 10f, Melee = 5f, Bite = 2f, Radiation = 5f, Cold = 7f } },
			{ "halloween.surgeonsuit", new ClothingResistances { Projectile = 25f, Melee = 30f, Bite = 8f, Radiation = 15f, Cold = 8f, Explosion = 5f } },
			{ "shirt.tanktop", new ClothingResistances { Projectile = 10f, Melee = 10f, Bite = 3f, Radiation = 2f, Cold = 2f } },
			{ "tshirt", new ClothingResistances { Projectile = 15f, Melee = 15f, Bite = 5f, Radiation = 3f, Cold = 6f } },
			{ "diving.wetsuit", new ClothingResistances { Melee = 10f, Bite = 5f, Radiation =15f, Cold = 13f } },
			{ "hat.wolf", new ClothingResistances { Projectile = 30f, Melee = 60f, Bite = 10f, Radiation = 4f, Cold = 6f, Explosion = 13f } },
			{ "wood.armor.helmet", new ClothingResistances { Projectile = 15f, Melee = 25f, Bite = 3f, Radiation = 2f, Cold = 7f } },
			{ "wood.armor.pants", new ClothingResistances { Projectile = 10f, Melee = 40f, Bite = 5f, Radiation = 5f, Explosion = 5f } },
			{ "wood.armor.jacket", new ClothingResistances { Projectile = 10f, Melee = 40f, Bite = 5f, Radiation = 5f, Explosion = 5f } }
		};
		#endregion
	}
}
