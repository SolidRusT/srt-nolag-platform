using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
	[Info("Kits", "Mevent", "1.0.20")]
	public class Kits : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin ImageLibrary, CopyPaste, Notify;

		private static Kits _instance;

		private const string Layer = "UI.Kits";

		private const string InfoLayer = "UI.Kits.Info";

		private const string EditingLayer = "UI.Kits.Editing";

		private const string ModalLayer = "UI.Kits.Modal";

		private readonly Dictionary<BasePlayer, List<Kit>> openGUI = new Dictionary<BasePlayer, List<Kit>>();

		private readonly Dictionary<BasePlayer, Dictionary<string, object>> kitEditing =
			new Dictionary<BasePlayer, Dictionary<string, object>>();

		private readonly Dictionary<BasePlayer, Dictionary<string, object>> itemEditing =
			new Dictionary<BasePlayer, Dictionary<string, object>>();

		private readonly Dictionary<string, List<string>> ItemsCategories =
			new Dictionary<string, List<string>>();

		private const string PERM_ADMIN = "Kits.admin";

		private int LastKitID;

		private string ColorOne;
		private string ColorTwo;
		private string ColorWhite;
		private string ColorThree;
		private string ColorFour;
		private string ColorRed;

		#endregion

		#region Config

		private Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Automatic wipe on wipe")]
			public bool AutoWipe;

			[JsonProperty(PropertyName = "Default Kit Color")]
			public string KitColor = "#A0A935";

			[JsonProperty(PropertyName = "Work with Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands = {"kit", "kits"};

			[JsonProperty(PropertyName = "Rarity Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<RarityColor> RarityColors = new List<RarityColor>
			{
				new RarityColor(40, "#A0A935")
			};

			[JsonProperty(PropertyName = "Auto Kits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> AutoKits = new List<string>
			{
				"autokit", "autokit_vip", "autokit_premium"
			};

			[JsonProperty(PropertyName = "Getting an auto kit 1 time?")]
			public bool OnceAutoKit;

			[JsonProperty(PropertyName = "Logs")] public LogInfo Logs = new LogInfo
			{
				Console = true,
				File = true
			};

			[JsonProperty(PropertyName = "Color 1")]
			public string ColorOne = "#161617";

			[JsonProperty(PropertyName = "Color 2")]
			public string ColorTwo = "#0E0E10";

			[JsonProperty(PropertyName = "Color 3")]
			public string ColorThree = "#4B68FF";

			[JsonProperty(PropertyName = "Color 4")]
			public string ColorFour = "#303030";

			[JsonProperty(PropertyName = "Color Red")]
			public string ColorRed = "#FF4B4B";

			[JsonProperty(PropertyName = "Color White")]
			public string ColorWhite = "#FFFFFF";

			[JsonProperty(PropertyName = "Show Number?")]
			public bool ShowNumber = true;

			[JsonProperty(PropertyName = "Show No Permission Description?")]
			public bool ShowNoPermDescription = true;

			[JsonProperty(PropertyName = "Show All Kits?")]
			public bool ShowAllKits;

			[JsonProperty(PropertyName = "CopyPaste Parameters",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> CopyPasteParameters = new List<string>
			{
				"deployables", "true", "inventories", "true"
			};

			[JsonProperty(PropertyName = "Block in Building Block?")]
			public bool BlockBuilding;

			[JsonProperty(PropertyName = "NPC Kits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, NpcKitsData> NpcKits = new Dictionary<string, NpcKitsData>
			{
				["1234567"] = new NpcKitsData
				{
					Description = "Free Kits",
					Kits = new List<string>
					{
						"kit_one",
						"kit_two"
					}
				},
				["7654321"] = new NpcKitsData
				{
					Description = "VIPs Kits",
					Kits = new List<string>
					{
						"kit_three",
						"kit_four"
					}
				}
			};

			[JsonProperty(PropertyName = "Description")]
			public MenuDescription Description = new MenuDescription
			{
				AnchorMin = "0 0", AnchorMax = "1 0",
				OffsetMin = "0 -55", OffsetMax = "0 -5",
				Enabled = true,
				Color = new IColor("#0E0E10", 100),
				FontSize = 18,
				Font = "robotocondensed-bold.ttf",
				Align = TextAnchor.MiddleCenter,
				TextColor = new IColor("#FFFFFF", 100),
				Description = string.Empty
			};

			[JsonProperty(PropertyName = "Info Kit Description")]
			public DescriptionSettings InfoKitDescription = new DescriptionSettings
			{
				AnchorMin = "0.5 1", AnchorMax = "0.5 1",
				OffsetMin = "-125 -55", OffsetMax = "125 -5",
				Enabled = true,
				Color = new IColor("#0E0E10", 100),
				FontSize = 18,
				Font = "robotocondensed-bold.ttf",
				Align = TextAnchor.MiddleCenter,
				TextColor = new IColor("#FFFFFF", 100)
			};

			[JsonProperty(PropertyName = "Interface")]
			public UserInterface UI = new UserInterface
			{
				Height = 455,
				Width = 640,
				KitHeight = 165,
				KitWidth = 135f,
				Margin = 10f,
				KitsOnString = 4,
				Strings = 2,
				YIndent = -100f,
				DisplayName = new DisplayNameSettings
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-45 -75",
					OffsetMax = "45 0",
					Enabled = true
				},
				Image = new ImageSettings
				{
					Enabled = false,
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-32 -75", OffsetMax = "32 -11"
				},
				KitAvailable = new InterfacePosition
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -100", OffsetMax = "0 -75"
				},
				KitAmount = new KitAmountSettings
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-125",
					OffsetMax = "-120",
					Width = 115
				},
				KitCooldown = new InterfacePosition
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-32.5 -125", OffsetMax = "32.5 -105"
				},
				KitAmountCooldown = new InterfacePosition
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -120",
					OffsetMax = "0 -95"
				},
				NoPermission = new InterfacePosition
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -100", OffsetMax = "0 -75"
				}
			};
		}

		private class UserInterface
		{
			[JsonProperty(PropertyName = "Height")]
			public float Height;

			[JsonProperty(PropertyName = "Width")] public float Width;

			[JsonProperty(PropertyName = "Kit Height")]
			public float KitHeight;

			[JsonProperty(PropertyName = "Kit Width")]
			public float KitWidth;

			[JsonProperty(PropertyName = "Margin")]
			public float Margin;

			[JsonProperty(PropertyName = "Kits On String")]
			public int KitsOnString;

			[JsonProperty(PropertyName = "Strings")]
			public int Strings;

			[JsonProperty(PropertyName = "Y Indent")]
			public float YIndent;

			[JsonProperty(PropertyName = "Display Name Settings")]
			public DisplayNameSettings DisplayName;

			[JsonProperty(PropertyName = "Image Settings")]
			public ImageSettings Image;

			[JsonProperty(PropertyName = "Kit Available Settings")]
			public InterfacePosition KitAvailable;

			[JsonProperty(PropertyName = "Kit Amount Settings")]
			public KitAmountSettings KitAmount;

			[JsonProperty(PropertyName = "Kit Cooldown Settings")]
			public InterfacePosition KitCooldown;

			[JsonProperty(PropertyName = "Kit Cooldown Settings (with amount)")]
			public InterfacePosition KitAmountCooldown;

			[JsonProperty(PropertyName = "No Permission Settings")]
			public InterfacePosition NoPermission;
		}

		private class DisplayNameSettings : InterfacePosition
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;
		}

		private class KitAmountSettings : InterfacePosition
		{
			[JsonProperty(PropertyName = "Width")] public float Width;
		}

		private class NpcKitsData
		{
			[JsonProperty(PropertyName = "Description")]
			public string Description;

			[JsonProperty(PropertyName = "Kits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Kits;
		}

		private class InterfacePosition
		{
			public string AnchorMin;

			public string AnchorMax;

			public string OffsetMin;

			public string OffsetMax;
		}

		private class ImageSettings : InterfacePosition
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;
		}

		private class DescriptionSettings : ImageSettings
		{
			[JsonProperty(PropertyName = "Background Color")]
			public IColor Color;

			[JsonProperty(PropertyName = "Font Size")]
			public int FontSize;

			[JsonProperty(PropertyName = "Font")] public string Font;

			[JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
			public TextAnchor Align;

			[JsonProperty(PropertyName = "Text Color")]
			public IColor TextColor;

			public void Get(ref CuiElementContainer container, string parent, string name = null,
				string description = null)
			{
				if (!Enabled || string.IsNullOrEmpty(description)) return;

				if (string.IsNullOrEmpty(name))
					name = CuiHelper.GetGuid();

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = AnchorMin, AnchorMax = AnchorMax,
						OffsetMin = OffsetMin, OffsetMax = OffsetMax
					},
					Image = {Color = Color.Get()}
				}, parent, name);

				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = $"{description}",
						Align = Align,
						Font = Font,
						FontSize = FontSize,
						Color = TextColor.Get()
					}
				}, name);
			}
		}

		private class MenuDescription : DescriptionSettings
		{
			[JsonProperty(PropertyName = "Description")]
			public string Description;
		}

		private class IColor
		{
			[JsonProperty(PropertyName = "HEX")] public string HEX;

			[JsonProperty(PropertyName = "Opacity (0 - 100)")]
			public float Alpha;

			public string Get()
			{
				if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

				var str = HEX.Trim('#');
				if (str.Length != 6) throw new Exception(HEX);
				var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
				var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
				var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

				return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
			}

			public IColor(string hex, float alpha)
			{
				HEX = hex;
				Alpha = alpha;
			}
		}

		private class LogInfo
		{
			[JsonProperty(PropertyName = "To Console")]
			public bool Console;

			[JsonProperty(PropertyName = "To File")]
			public bool File;
		}

		private class RarityColor
		{
			[JsonProperty(PropertyName = "Chance")]
			public int Chance;

			[JsonProperty(PropertyName = "Color")] public string Color;

			public RarityColor(int chance, string color)
			{
				Chance = chance;
				Color = color;
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

		#region Data

		private PluginData _data;
		private Dictionary<ulong, Dictionary<string, KitData>> PlayerData;

		private void SaveData()
		{
			SaveKits();

			SaveUsers();
		}

		private void SaveKits()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Kits", _data);
		}

		private void SaveUsers()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Data", PlayerData);
		}

		private void LoadData()
		{
			try
			{
				_data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>($"{Name}/Kits");

				PlayerData =
					Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, KitData>>>(
						$"{Name}/Data");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (_data == null) _data = new PluginData();
			if (PlayerData == null) PlayerData = new Dictionary<ulong, Dictionary<string, KitData>>();
		}


		private class PluginData
		{
			[JsonProperty(PropertyName = "Kits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<Kit> Kits = new List<Kit>();
		}

		private KitData GetPlayerData(ulong userID, string name)
		{
			if (!PlayerData.ContainsKey(userID))
				PlayerData[userID] = new Dictionary<string, KitData>();

			if (!PlayerData[userID].ContainsKey(name))
				PlayerData[userID][name] = new KitData();

			return PlayerData[userID][name];
		}

		private class Kit
		{
			[JsonIgnore] public int ID;

			[JsonProperty(PropertyName = "Name")] public string Name;

			[JsonProperty(PropertyName = "Display Name")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Color")] public string Color;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = "Description")]
			public string Description;

			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Hide")] public bool Hide;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount;

			[JsonProperty(PropertyName = "Cooldown")]
			public double Cooldown;

			[JsonProperty(PropertyName = "Wipe Block")]
			public double CooldownAfterWipe;

			[JsonProperty(PropertyName = "Building")]
			public string Building;

			[JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<KitItem> Items;

			public void Get(BasePlayer player)
			{
				Items?.ForEach(item => item?.Get(player));
			}
		}

		private enum KitItemType
		{
			Item,
			Command
		}

		private class KitItem
		{
			[JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
			public KitItemType Type;

			[JsonProperty(PropertyName = "Command")]
			public string Command;

			[JsonProperty(PropertyName = "ShortName")]
			public string ShortName;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount;

			[JsonProperty(PropertyName = "Blueprint")]
			public int Blueprint;

			[JsonProperty(PropertyName = "SkinID")]
			public ulong SkinID;

			[JsonProperty(PropertyName = "Container")]
			public string Container;

			[JsonProperty(PropertyName = "Condition")]
			public float Condition;

			[JsonProperty(PropertyName = "Chance")]
			public int Chance;

			[JsonProperty(PropertyName = "Position", DefaultValueHandling = DefaultValueHandling.Populate)]
			[DefaultValue(-1)]
			public int Position;

			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Weapon")]
			public Weapon Weapon;

			[JsonProperty(PropertyName = "Content", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ItemContent> Content;

			public void Get(BasePlayer player)
			{
				if (Chance < 100 && Random.Range(0, 100) > Chance) return;

				switch (Type)
				{
					case KitItemType.Item:
					{
						GiveItem(player, BuildItem(),
							Container == "belt" ? player.inventory.containerBelt :
							Container == "wear" ? player.inventory.containerWear : player.inventory.containerMain);
						break;
					}
					case KitItemType.Command:
					{
						ToCommand(player);
						break;
					}
				}
			}

			private void ToCommand(BasePlayer player)
			{
				var command = Command.Replace("\n", "|")
					.Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace(
						"%username%",
						player.displayName, StringComparison.OrdinalIgnoreCase);

				foreach (var check in command.Split('|')) _instance?.Server.Command(check);
			}

			private static void GiveItem(BasePlayer player, Item item, ItemContainer cont = null)
			{
				if (item == null) return;
				var inv = player.inventory;

				var moved = item.MoveToContainer(cont, item.position) || item.MoveToContainer(cont) ||
				            item.MoveToContainer(inv.containerMain);
				if (!moved)
				{
					if (cont == inv.containerBelt)
						moved = item.MoveToContainer(inv.containerWear);
					if (cont == inv.containerWear)
						moved = item.MoveToContainer(inv.containerBelt);
				}

				if (!moved)
					item.Drop(player.GetCenter(), player.GetDropVelocity());
			}

			private Item BuildItem()
			{
				var item = ItemManager.CreateByName(ShortName, Amount > 1 ? Amount : 1, SkinID);
				item.condition = Condition;

				item.position = Position;

				if (Blueprint != 0)
					item.blueprintTarget = Blueprint;

				if (Weapon != null)
				{
					var baseProjectile = item.GetHeldEntity() as BaseProjectile;
					if (baseProjectile != null && !string.IsNullOrEmpty(Weapon.ammoType))
					{
						baseProjectile.primaryMagazine.contents = Weapon.ammoAmount;
						baseProjectile.primaryMagazine.ammoType =
							ItemManager.FindItemDefinition(Weapon.ammoType);
					}
				}

				Content?.ForEach(cont =>
				{
					var new_cont = ItemManager.CreateByName(cont.ShortName, cont.Amount);
					new_cont.condition = cont.Condition;
					new_cont.MoveToContainer(item.contents);
				});

				return item;
			}

			public static KitItem FromOld(ItemData item, string container)
			{
				var newItem = new KitItem
				{
					Content =
						item.Contents?.Select(x =>
								new ItemContent {ShortName = x.Shortname, Condition = x.Condition, Amount = x.Amount})
							.ToList() ?? new List<ItemContent>(),
					Weapon = new Weapon {ammoAmount = item.Ammo, ammoType = item.Ammotype},
					Container = container,
					SkinID = item.Skin,
					Command = string.Empty,
					Chance = 100,
					Blueprint = string.IsNullOrEmpty(item.BlueprintShortname) ? 0 : 1,
					Condition = item.Condition,
					Amount = item.Amount,
					ShortName = item.Shortname,
					Type = KitItemType.Item,
					Position = item.Position
				};

				return newItem;
			}
		}

		private class Weapon
		{
			public string ammoType;

			public int ammoAmount;
		}

		private class ItemContent
		{
			public string ShortName;

			public float Condition;

			public int Amount;
		}

		private class KitData
		{
			public int Amount;

			public double Cooldown;
		}

		#endregion

		#region Hooks

		private void Init()
		{
			_instance = this;

			LoadData();
		}

		private void OnServerInitialized()
		{
			LoadImages();

			#region Colors

			ColorOne = HexToCuiColor(_config.ColorOne);
			ColorTwo = HexToCuiColor(_config.ColorTwo);
			ColorWhite = HexToCuiColor(_config.ColorWhite);
			ColorThree = HexToCuiColor(_config.ColorThree);
			ColorFour = HexToCuiColor(_config.ColorFour);
			ColorRed = HexToCuiColor(_config.ColorRed);

			#endregion

			#region Set IDs

			_data.Kits.ForEach(kit =>
			{
				kit.ID = LastKitID;
				++LastKitID;

				if (!string.IsNullOrEmpty(kit.Permission) && !permission.PermissionExists(kit.Permission))
					permission.RegisterPermission(kit.Permission, this);
			});

			#endregion

			if (!permission.PermissionExists(PERM_ADMIN))
				permission.RegisterPermission(PERM_ADMIN, this);

			FixItemsPositions();

			FillCategories();

			AddCovalenceCommand(_config.Commands, nameof(CmdOpenKits));

			timer.Every(1, HandleUi);
		}

		private void OnServerSave()
		{
			timer.In(Random.Range(2, 7), SaveData);
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, Layer);
				CuiHelper.DestroyUi(player, InfoLayer);
				CuiHelper.DestroyUi(player, EditingLayer);
				CuiHelper.DestroyUi(player, ModalLayer);

				if (kitEditing.ContainsKey(player) || itemEditing.ContainsKey(player))
					player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
			}

			SaveUsers();

			_instance = null;
		}

		private void OnNewSave(string filename)
		{
			if (!_config.AutoWipe) return;

			LoadData();

			PlayerData.Clear();

			SaveUsers();
		}

		private void OnPlayerRespawned(BasePlayer player)
		{
			if (player == null) return;

			var kits = GetAutoKits(player);
			if (kits.Count == 0)
				return;

			player.inventory.Strip();

			if (_config.OnceAutoKit)
				kits.LastOrDefault()?.Get(player);
			else
				kits.ForEach(kit => kit.Get(player));
		}

		private void OnPlayerDisconnected(BasePlayer player)
		{
			openGUI.Remove(player);
			kitEditing.Remove(player);
			itemEditing.Remove(player);
		}

		private object CanSpectateTarget(BasePlayer player, string filter)
		{
			return player != null && (itemEditing.ContainsKey(player) || kitEditing.ContainsKey(player))
				? (object) true
				: null;
		}

		private void OnUseNPC(BasePlayer npc, BasePlayer player)
		{
			if (npc == null || player == null || !_config.NpcKits.ContainsKey(npc.UserIDString)) return;

			MainUi(player, npc.userID, First: true);
		}

		#endregion

		#region Commands

		private void CmdOpenKits(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (args.Length == 0)
			{
				MainUi(player, First: true);
				return;
			}

			switch (args[0])
			{
				case "help":
				{
					Reply(player, KitsHelp, command);
					break;
				}
				case "list":
				{
					Reply(player, KitsList,
						string.Join(", ", GetAvailableKits(player).Select(x => $"'{x.DisplayName}'")));
					break;
				}
				case "remove":
				{
					if (!IsAdmin(player)) return;

					var name = string.Join(" ", args.Skip(1));
					if (string.IsNullOrEmpty(name))
					{
						SendNotify(player, KitNotFound, 1, name);
						return;
					}

					var kit = GetAvailableKits(player)?.Find(x => x.DisplayName == name);
					if (kit == null)
					{
						SendNotify(player, KitNotFound, 1, name);
						return;
					}

					_data.Kits.Remove(kit);
					SaveKits();

					SendNotify(player, KitRemoved, 0, name);
					break;
				}
				default:
				{
					var name = string.Join(" ", args);
					if (string.IsNullOrEmpty(name))
					{
						SendNotify(player, KitNotFound, 1, name);
						return;
					}

					var kit = GetAvailableKits(player, checkAmount: false)
						.Find(x => string.Equals(x.Name, name, StringComparison.InvariantCultureIgnoreCase) ||
						           string.Equals(x.DisplayName, name, StringComparison.InvariantCultureIgnoreCase));
					if (kit == null)
					{
						SendNotify(player, KitNotFound, 1, name);
						return;
					}

					GiveKit(player, kit);
					break;
				}
			}
		}

		[ConsoleCommand("UI_Kits")]
		private void CmdKitsConsole(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null || !arg.HasArgs()) return;

			switch (arg.Args[0])
			{
				case "close":
				{
					openGUI.Remove(player);
					itemEditing.Remove(player);
					kitEditing.Remove(player);
					player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);

					if (itemEditing.Count == 0 && kitEditing.Count == 0)
						Unsubscribe(nameof(CanSpectateTarget));
					break;
				}

				case "stopedit":
				{
					itemEditing.Remove(player);
					kitEditing.Remove(player);
					player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);

					if (itemEditing.Count == 0 && kitEditing.Count == 0)
						Unsubscribe(nameof(CanSpectateTarget));
					break;
				}

				case "main":
				{
					var targetId = 0UL;
					if (arg.HasArgs(2))
						ulong.TryParse(arg.Args[1], out targetId);

					var page = 0;
					if (arg.HasArgs(3))
						int.TryParse(arg.Args[2], out page);

					var showAll = false;
					if (arg.HasArgs(4))
						bool.TryParse(arg.Args[3], out showAll);

					MainUi(player, targetId, page, showAll);
					break;
				}

				case "infokit":
				{
					int kitId;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out kitId)) return;

					var kit = _data.Kits.Find(x => x.ID == kitId);
					if (kit == null) return;

					player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);

					kitEditing.Remove(player);
					itemEditing.Remove(player);

					if (itemEditing.Count == 0 && kitEditing.Count == 0)
						Unsubscribe(nameof(CanSpectateTarget));

					InfoKitUi(player, kit);
					break;
				}

				case "givekit":
				{
					int kitId;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out kitId)) return;

					var kit = _data.Kits.Find(x => x.ID == kitId);
					if (kit == null) return;

					GiveKit(player, kit);
					break;
				}

				case "editkit":
				{
					if (!IsAdmin(player)) return;

					bool creating;
					if (!arg.HasArgs(2) || !bool.TryParse(arg.Args[1], out creating)) return;

					var kitId = -1;
					if (arg.HasArgs(3))
						int.TryParse(arg.Args[2], out kitId);

					if (arg.HasArgs(4) && (!arg.HasArgs(5) || string.IsNullOrEmpty(arg.Args[4])))
						return;

					if (arg.HasArgs(5))
					{
						var key = arg.Args[3];
						var value = arg.Args[4];

						if (kitEditing.ContainsKey(player) && kitEditing[player].ContainsKey(key))
						{
							object newValue = null;

							switch (key)
							{
								case "Name":
									newValue = value;
									break;
								case "DisplayName":
									newValue = string.Join(" ", arg.Args.Skip(4));
									break;
								case "Color":
									newValue = value;
									break;
								case "Permission":
									newValue = value;
									break;
								case "Hide":
									bool hide;
									bool.TryParse(value, out hide);

									newValue = hide;
									break;
								case "AutoKit":
									bool autoKit;
									bool.TryParse(value, out autoKit);

									newValue = autoKit;
									break;
								case "Amount":
									int amount;
									int.TryParse(value, out amount);

									newValue = amount;
									break;
								case "Cooldown":
									double cooldown;
									double.TryParse(value, out cooldown);

									newValue = cooldown;
									break;
							}

							kitEditing[player][key] = newValue;
						}
					}

					EditingKitUi(player, creating, kitId);
					break;
				}

				case "takeitem":
				{
					if (!IsAdmin(player)) return;

					int kitId, slot;
					if (!arg.HasArgs(5) ||
					    !itemEditing.ContainsKey(player) ||
					    !int.TryParse(arg.Args[2], out kitId) ||
					    !int.TryParse(arg.Args[3], out slot))
						return;

					var container = arg.Args[1];

					itemEditing[player]["ShortName"] = arg.Args[4];

					EditingItemUi(player, kitId, slot, container);
					break;
				}

				case "selectitem":
				{
					if (!IsAdmin(player)) return;

					int kitId, slot;
					if (!arg.HasArgs(4) ||
					    !itemEditing.ContainsKey(player) ||
					    !int.TryParse(arg.Args[2], out kitId) ||
					    !int.TryParse(arg.Args[3], out slot))
						return;

					var container = arg.Args[1];

					var selectedCategory = string.Empty;
					if (arg.HasArgs(5))
						selectedCategory = arg.Args[4];

					var page = 0;
					if (arg.HasArgs(6))
						int.TryParse(arg.Args[5], out page);

					var input = string.Empty;
					if (arg.HasArgs(7))
						input = string.Join(" ", arg.Args.Skip(6));

					SelectItem(player, kitId, slot, container, selectedCategory, page, input);
					break;
				}

				case "startedititem":
				{
					if (!IsAdmin(player)) return;

					int kitId, slot;
					if (!arg.HasArgs(4) ||
					    !int.TryParse(arg.Args[2], out kitId) ||
					    !int.TryParse(arg.Args[3], out slot)) return;

					var container = arg.Args[1];

					EditingItemUi(player, kitId, slot, container, true);
					break;
				}

				case "edititem":
				{
					if (!IsAdmin(player)) return;

					int kitId, slot;
					if (!arg.HasArgs(6) ||
					    !int.TryParse(arg.Args[2], out kitId) ||
					    !int.TryParse(arg.Args[3], out slot)) return;

					var container = arg.Args[1];

					var key = arg.Args[4];
					var value = arg.Args[5];

					if (itemEditing.ContainsKey(player) && itemEditing[player].ContainsKey(key))
					{
						object newValue = null;

						switch (key)
						{
							case "Type":
							{
								KitItemType type;
								if (Enum.TryParse(value, out type))
									newValue = type;
								break;
							}
							case "Command":
							{
								newValue = string.Join(" ", arg.Args.Skip(5));
								break;
							}
							case "ShortName":
							{
								newValue = value;
								break;
							}
							case "Amount":
							{
								int Value;

								if (int.TryParse(value, out Value))
									newValue = Value;
								break;
							}
							case "Blueprint":
							{
								int Value;

								if (int.TryParse(value, out Value))
									newValue = Value;
								break;
							}
							case "SkinID":
							{
								ulong Value;

								if (ulong.TryParse(value, out Value))
									newValue = Value;
								break;
							}
							case "Chance":
							{
								int Value;

								if (int.TryParse(value, out Value))
									newValue = Value;
								break;
							}
						}

						itemEditing[player][key] = newValue;
					}

					EditingItemUi(player, kitId, slot, container);
					break;
				}

				case "saveitem":
				{
					if (!IsAdmin(player)) return;

					int kitId, slot;
					if (!arg.HasArgs(4) ||
					    !int.TryParse(arg.Args[1], out kitId) ||
					    !int.TryParse(arg.Args[2], out slot)) return;

					var container = arg.Args[3];
					if (string.IsNullOrEmpty(container)) return;

					var editing = itemEditing[player];
					if (editing == null) return;

					var kit = _data.Kits.Find(x => x.ID == kitId);
					if (kit == null) return;

					var item = kit.Items.Find(x => x.Container == container && x.Position == slot);
					var hasItem = item != null;

					if (item == null)
						item = new KitItem();

					item.Type = (KitItemType) editing["Type"];
					item.Command = editing["Command"].ToString();
					item.Container = editing["Container"].ToString();
					item.ShortName = editing["ShortName"].ToString();
					item.Amount = (int) editing["Amount"];
					item.Blueprint = (int) editing["Blueprint"];
					item.Chance = (int) editing["Chance"];
					item.SkinID = (ulong) editing["SkinID"];
					item.Position = (int) editing["Position"];

					if (!hasItem)
					{
						var info = ItemManager.FindItemDefinition(item.ShortName);
						if (info != null)
							item.Condition = info.condition.max;

						kit.Items.Add(item);
					}

					itemEditing.Remove(player);

					SaveKits();

					InfoKitUi(player, kit);
					break;
				}

				case "removeitem":
				{
					if (!IsAdmin(player)) return;

					int kitId, slot;
					if (!arg.HasArgs(4) ||
					    !int.TryParse(arg.Args[1], out kitId) ||
					    !int.TryParse(arg.Args[2], out slot)) return;

					var editing = itemEditing[player];
					if (editing == null) return;

					var kit = _data.Kits.Find(x => x.ID == kitId);
					if (kit == null) return;

					var item = kit.Items.Find(x => x.Container == arg.Args[3] && x.Position == slot);
					if (item != null)
						kit.Items.Remove(item);

					itemEditing.Remove(player);

					SaveKits();

					InfoKitUi(player, kit);

					break;
				}

				case "savekit":
				{
					if (!IsAdmin(player)) return;

					bool creating;
					int kitId;
					if (!arg.HasArgs(3) || !bool.TryParse(arg.Args[1], out creating) ||
					    !int.TryParse(arg.Args[2], out kitId)) return;

					var editing = kitEditing[player];
					if (editing == null) return;

					Kit kit;
					if (creating)
					{
						kit = new Kit
						{
							ID = ++LastKitID,
							Name = (string) editing["Name"],
							DisplayName = (string) editing["DisplayName"],
							Color = (string) editing["Color"],
							Permission = (string) editing["Permission"],
							Hide = editing["Hide"] as bool? ?? true,
							Amount = (int) editing["Amount"],
							Cooldown = (double) editing["Cooldown"],
							Items = new List<KitItem>()
						};
						_data.Kits.Add(kit);
					}
					else
					{
						kit = _data.Kits.Find(x => x.ID == kitId);
						if (kit == null) return;

						kit.Name = (string) editing["Name"];
						kit.DisplayName = (string) editing["DisplayName"];
						kit.Color = (string) editing["Color"];
						kit.Permission = (string) editing["Permission"];
						kit.Hide = editing["Hide"] as bool? ?? true;
						kit.Amount = (int) editing["Amount"];
						kit.Cooldown = (double) editing["Cooldown"];
					}

					var autoKit = editing["AutoKit"] as bool? ?? false;
					if (autoKit)
					{
						if (!_config.AutoKits.Contains(kit.Name))
						{
							_config.AutoKits.Add(kit.Name);
							SaveConfig();
						}
					}
					else
					{
						_config.AutoKits.Remove(kit.Name);
						SaveConfig();
					}

					kitEditing.Remove(player);

					if (!string.IsNullOrEmpty(kit.Permission) && !permission.PermissionExists(kit.Permission))
						permission.RegisterPermission(kit.Permission, this);

					SaveKits();

					MainUi(player);
					break;
				}

				case "removekit":
				{
					if (!IsAdmin(player)) return;

					int kitId;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out kitId)) return;

					_data.Kits.RemoveAll(x => x.ID == kitId);

					SaveKits();

					MainUi(player);
					break;
				}

				case "frominv":
				{
					if (!IsAdmin(player)) return;

					int kitId;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out kitId)) return;

					var kit = _data.Kits.Find(x => x.ID == kitId);
					if (kit == null) return;

					var kitItems = GetPlayerItems(player);
					if (kitItems == null) return;

					kit.Items = kitItems;

					SaveKits();

					InfoKitUi(player, kit);
					break;
				}
			}
		}

		[ConsoleCommand("kits.resetkits")]
		private void CmdKitsReset(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			_data.Kits.Clear();
			PlayerData.Clear();

			SaveData();

			SendReply(arg, "Plugin successfully reset");
		}

		[ConsoleCommand("kits.resetdata")]
		private void CmdKitsResetPlayers(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			PlayerData.Clear();

			SaveUsers();

			SendReply(arg, "Players successfully reset");
		}

		[ConsoleCommand("kits.give")]
		private void CmdKitsGive(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			if (!arg.HasArgs(2))
			{
				SendReply(arg, $"Error syntax! Use: {arg.cmd.FullName} [name/steamid] [kitname]");
				return;
			}

			var target = BasePlayer.Find(arg.Args[0]);

			if (target == null)
			{
				SendReply(arg, $"Player '{arg.Args[0]}' not found!");
				return;
			}

			var kit = _data.Kits.Find(x => x.Name == arg.Args[1]);
			if (kit == null)
			{
				SendReply(arg, $"Kit '{arg.Args[1]}' not found!");
				return;
			}

			kit.Items.ForEach(item => item.Get(target));

			SendReply(arg, $"Player '{arg.Args[0]}' successfully received a kit '{arg.Args[1]}'");

			Interface.CallHook("OnKitRedeemed", target, kit.Name);
			Log(target, kit.Name);
		}

		#endregion

		#region Interface

		private void MainUi(BasePlayer player, ulong targetId = 0, int page = 0, bool showAll = false,
			bool First = false)
		{
			#region Fields

			var totalAmount = _config.UI.KitsOnString * _config.UI.Strings;

			var constSwitch = -(_config.UI.KitsOnString * _config.UI.KitWidth +
			                    (_config.UI.KitsOnString - 1) * _config.UI.Margin) / 2f;

			var xSwicth = constSwitch;
			var ySwitch = _config.UI.YIndent;

			var allKits = GetAvailableKits(player, targetId.ToString(), showAll);
			var kitsList = allKits.Skip(page * totalAmount).Take(totalAmount).ToList();

			openGUI[player] = kitsList;

			#endregion

			var container = new CuiElementContainer();

			#region Background

			if (First)
			{
				CuiHelper.DestroyUi(player, Layer);

				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image =
					{
						Color = "0 0 0 0.9",
						Material = "assets/content/ui/uibackgroundblur.mat"
					},
					CursorEnabled = true
				}, "Overlay", Layer);

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Close = Layer,
						Command = "UI_Kits close"
					}
				}, Layer);
			}

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = $"-{_config.UI.Width / 2f} -{_config.UI.Height / 2f}",
					OffsetMax = $"{_config.UI.Width / 2f} {_config.UI.Height / 2f}"
				},
				Image =
				{
					Color = ColorTwo
				}
			}, Layer, Layer + ".Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = ColorOne}
			}, Layer + ".Main", Layer + ".Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "10 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, MainTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = ColorWhite
				}
			}, Layer + ".Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-35 -37.5",
					OffsetMax = "-10 -12.5"
				},
				Text =
				{
					Text = Msg(player, Close),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = ColorWhite
				},
				Button =
				{
					Close = Layer,
					Color = ColorThree,
					Command = "UI_Kits close"
				}
			}, Layer + ".Header");

			if (IsAdmin(player))
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = "-140 -37.5",
						OffsetMax = "-45 -12.5"
					},
					Text =
					{
						Text = Msg(player, CreateKit),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = ColorWhite
					},
					Button =
					{
						Color = ColorTwo,
						Command = "UI_Kits editkit True"
					}
				}, Layer + ".Header");

			#endregion

			#region Second Header

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "10 -85",
					OffsetMax = "110 -60"
				},
				Text =
				{
					Text = Msg(player, ListKits),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = ColorWhite
				}
			}, Layer + ".Main");

			#region Checkbox

			if (IsAdmin(player))
				CheckBoxUi(ref container,
					Layer + ".Main",
					Layer + ".ShowAll",
					"0 1", "0 1",
					"90 -77.5",
					"100 -67.5",
					showAll,
					$"UI_Kits main {targetId} 0 {!showAll}",
					Msg(player, ShowAll)
				);

			#endregion

			#region Pages

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-132.5 -82.5",
					OffsetMax = "-72.5 -60"
				},
				Text =
				{
					Text = Msg(player, Back),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = ColorWhite
				},
				Button =
				{
					Color = ColorOne,
					Command = page != 0 ? $"UI_Kits main {targetId} {page - 1} {showAll}" : ""
				}
			}, Layer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-70 -82.5",
					OffsetMax = "-10 -60"
				},
				Text =
				{
					Text = Msg(player, Next),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = ColorWhite
				},
				Button =
				{
					Color = ColorThree,
					Command = allKits.Count > (page + 1) * totalAmount
						? $"UI_Kits main {targetId} {page + 1} {showAll}"
						: ""
				}
			}, Layer + ".Main");

			#endregion

			#endregion

			#region Kits

			if (allKits.Count == 0)
				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 25", OffsetMax = "0 -85"
					},
					Text =
					{
						Text = Msg(player, NotAvailableKits),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 20,
						Color = "1 1 1 0.45"
					}
				}, Layer + ".Main");
			else
				for (var i = 0; i < kitsList.Count; i++)
				{
					var kit = kitsList[i];

					var number = page * totalAmount + i + 1;

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{xSwicth} {ySwitch - _config.UI.KitHeight}",
							OffsetMax = $"{xSwicth + _config.UI.KitWidth} {ySwitch}"
						},
						Image =
						{
							Color = "0 0 0 0"
						}
					}, Layer + ".Main", Layer + $".Kit.{kit.ID}.Main");

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 30", OffsetMax = "0 0"
						},
						Image =
						{
							Color = ColorOne
						}
					}, Layer + $".Kit.{kit.ID}.Main", Layer + $".Kit.{kit.ID}.Main.Background");

					#region Name

					if (_config.ShowNumber)
						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = "-45 -75",
								OffsetMax = "45 0"
							},
							Text =
							{
								Text = $"#{number}",
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 60,
								Color = ColorFour
							}
						}, Layer + $".Kit.{kit.ID}.Main");

					if (_config.UI.DisplayName.Enabled)
						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = _config.UI.DisplayName.AnchorMin,
								AnchorMax = _config.UI.DisplayName.AnchorMax,
								OffsetMin = _config.UI.DisplayName.OffsetMin,
								OffsetMax = _config.UI.DisplayName.OffsetMax
							},
							Text =
							{
								Text = $"{kit.DisplayName}",
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 16,
								Color = "1 1 1 1"
							}
						}, Layer + $".Kit.{kit.ID}.Main");

					#endregion

					#region Image

					if (_config.UI.Image.Enabled && !string.IsNullOrEmpty(kit.Image))
						container.Add(new CuiElement
						{
							Parent = Layer + $".Kit.{kit.ID}.Main",
							Components =
							{
								new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", kit.Image)},
								new CuiRectTransformComponent
								{
									AnchorMin = _config.UI.Image.AnchorMin,
									AnchorMax = _config.UI.Image.AnchorMax,
									OffsetMin = _config.UI.Image.OffsetMin,
									OffsetMax = _config.UI.Image.OffsetMax
								}
							}
						});

					#endregion

					#region Line

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "0 0", OffsetMax = "0 2"
						},
						Image = {Color = HexToCuiColor(kit.Color)}
					}, Layer + $".Kit.{kit.ID}.Main.Background");

					#endregion

					#region Give Kit

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = $"0 -{_config.UI.KitHeight}",
							OffsetMax = $"{_config.UI.KitWidth - 30} -{_config.UI.KitHeight - 25}"
						},
						Text =
						{
							Text = Msg(player, KitTake),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 10,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = ColorOne,
							Command = $"UI_Kits givekit {kit.ID}"
						}
					}, Layer + $".Kit.{kit.ID}.Main");

					#endregion

					#region Info

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = $"{_config.UI.KitWidth - 25} -{_config.UI.KitHeight}",
							OffsetMax = $"{_config.UI.KitWidth} -{_config.UI.KitHeight - 25}"
						},
						Text =
						{
							Text = Msg(player, KitInfo),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 18,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = ColorOne,
							Command = $"UI_Kits infokit {kit.ID}"
						}
					}, Layer + $".Kit.{kit.ID}.Main");

					#endregion

					RefreshKitUi(ref container, player, kit);

					if ((i + 1) % _config.UI.KitsOnString == 0)
					{
						xSwicth = constSwitch;
						ySwitch = ySwitch - _config.UI.KitHeight - _config.UI.Margin;
					}
					else
					{
						xSwicth += _config.UI.Margin + _config.UI.KitWidth;
					}
				}

			#endregion

			#region Description

			NpcKitsData npcKit;
			var description = targetId == 0
				? _config.Description.Description
				: _config.NpcKits.TryGetValue(targetId.ToString(), out npcKit)
					? npcKit.Description
					: string.Empty;

			_config.Description.Get(ref container, Layer + ".Main", null, description);

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		private void InfoKitUi(BasePlayer player, Kit kit)
		{
			var container = new CuiElementContainer();

			#region Fields

			var Size = 70f;
			var Margin = 5f;

			var ySwitch = -125f;
			var amountOnString = 6;
			var constSwitch = -(amountOnString * Size + (amountOnString - 1) * Margin) / 2f;

			var total = 0;

			#endregion

			#region Background

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = HexToCuiColor(_config.ColorTwo, 98)
				}
			}, "Overlay", InfoLayer);

			#endregion

			#region Header

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "112.5 -140", OffsetMax = "222.5 -115"
				},
				Text =
				{
					Text = Msg(player, ComeBack),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = ColorThree,
					Command = "UI_Kits stopedit",
					Close = InfoLayer
				}
			}, InfoLayer);

			#region Change Button

			if (IsAdmin(player))
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-12.5 -140", OffsetMax = "102.5 -115"
					},
					Image = {Color = "0 0 0 0"}
				}, InfoLayer, InfoLayer + ".Btn.Change");

				CreateOutLine(ref container, InfoLayer + ".Btn.Change", ColorThree, 1);

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = Msg(player, Edit),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = "0 0 0 0",
						Command = $"UI_Kits editkit {false} {kit.ID}",
						Close = InfoLayer
					}
				}, InfoLayer + ".Btn.Change");
			}

			#endregion

			#endregion

			#region Main

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = $"{constSwitch} {ySwitch - 15f}", OffsetMax = $"0 {ySwitch}"
				},
				Text =
				{
					Text = Msg(player, ContainerMain),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 1"
				}
			}, InfoLayer);

			ySwitch -= 20f;

			var xSwitch = constSwitch;

			var kitItems = kit.Items.FindAll(item => item.Container == "main");

			kitItems.Sort((x, y) => y.Chance.CompareTo(x.Chance));

			for (var slot = 0; slot < amountOnString * 4; slot++)
			{
				var kitItem = kitItems.Find(x => x.Position == slot); //kitItems.Count > slot ? kitItems[slot] : null;

				InfoItemUi(ref container, player,
					slot,
					$"{xSwitch} {ySwitch - Size}",
					$"{xSwitch + Size} {ySwitch}",
					kit,
					kitItem,
					total,
					"main");

				if ((slot + 1) % amountOnString == 0)
				{
					xSwitch = constSwitch;
					ySwitch = ySwitch - Size - Margin;
				}
				else
				{
					xSwitch += Size + Margin;
				}

				total++;
			}

			#endregion

			#region Wear

			ySwitch -= 5f;

			amountOnString = 7;

			constSwitch = -(amountOnString * Size + (amountOnString - 1) * Margin) / 2f;

			xSwitch = constSwitch;

			kitItems = kit.Items.FindAll(item => item.Container == "wear");

			kitItems.Sort((x, y) => y.Chance.CompareTo(x.Chance));

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = $"{constSwitch} {ySwitch - 15f}", OffsetMax = $"0 {ySwitch}"
				},
				Text =
				{
					Text = Msg(player, ContaineWear),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 1"
				}
			}, InfoLayer);

			ySwitch -= 20f;

			for (var slot = 0; slot < amountOnString; slot++)
			{
				var kitItem = kitItems.Find(x => x.Position == slot); //kitItems.Count > slot ? kitItems[slot] : null;

				InfoItemUi(ref container, player,
					slot,
					$"{xSwitch} {ySwitch - Size}",
					$"{xSwitch + Size} {ySwitch}",
					kit,
					kitItem,
					total,
					"wear");

				if ((slot + 1) % amountOnString == 0)
				{
					xSwitch = constSwitch;
					ySwitch = ySwitch - Size - Margin;
				}
				else
				{
					xSwitch += Size + Margin;
				}

				total++;
			}

			#endregion

			#region Belt

			ySwitch -= 5f;

			amountOnString = 6;

			constSwitch = -(amountOnString * Size + (amountOnString - 1) * Margin) / 2f;

			xSwitch = constSwitch;

			kitItems = kit.Items.FindAll(item => item.Container == "belt");

			kitItems.Sort((x, y) => y.Chance.CompareTo(x.Chance));

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = $"{constSwitch} {ySwitch - 15f}", OffsetMax = $"0 {ySwitch}"
				},
				Text =
				{
					Text = Msg(player, ContainerBelt),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 1"
				}
			}, InfoLayer);

			ySwitch -= 20f;

			for (var slot = 0; slot < amountOnString; slot++)
			{
				var kitItem = kitItems.Find(x => x.Position == slot); //kitItems.Count > slot ? kitItems[slot] : null;

				InfoItemUi(ref container, player,
					slot,
					$"{xSwitch} {ySwitch - Size}",
					$"{xSwitch + Size} {ySwitch}",
					kit,
					kitItem,
					total,
					"belt");

				if ((slot + 1) % amountOnString == 0)
				{
					xSwitch = constSwitch;
					ySwitch = ySwitch - Size - Margin;
				}
				else
				{
					xSwitch += Size + Margin;
				}

				total++;
			}

			#endregion

			#region Description

			_config.InfoKitDescription.Get(ref container, InfoLayer, null, kit.Description);

			#endregion

			CuiHelper.DestroyUi(player, InfoLayer);
			CuiHelper.AddUi(player, container);
		}

		private void EditingKitUi(BasePlayer player, bool creating, int kitId = -1)
		{
			#region Dictionary

			if (!kitEditing.ContainsKey(player))
			{
				if (kitId != -1)
				{
					var kit = _data.Kits.Find(x => x.ID == kitId);
					if (kit == null) return;

					kitEditing.Add(player, new Dictionary<string, object>
					{
						["Name"] = kit.Name,
						["DisplayName"] = kit.DisplayName,
						["Color"] = kit.Color,
						["Permission"] = kit.Permission,
						["Hide"] = kit.Hide,
						["Amount"] = kit.Amount,
						["Cooldown"] = kit.Cooldown,
						["AutoKit"] = _config.AutoKits.Contains(kit.Name)
					});
				}
				else
				{
					kitEditing.Add(player, new Dictionary<string, object>
					{
						["Name"] = CuiHelper.GetGuid(),
						["DisplayName"] = "My Kit",
						["Color"] = _config.KitColor,
						["Permission"] = $"{Name}.default",
						["Hide"] = true,
						["Amount"] = 0,
						["Cooldown"] = 0.0,
						["AutoKit"] = false
					});
				}

				player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);

				Subscribe(nameof(CanSpectateTarget));
			}

			#endregion

			var container = new CuiElementContainer();

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = HexToCuiColor(_config.ColorTwo, 98)
				}
			}, "Overlay", EditingLayer);

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-260 -150",
					OffsetMax = "260 185"
				},
				Image =
				{
					Color = ColorTwo
				}
			}, EditingLayer, EditingLayer + ".Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = ColorOne}
			}, EditingLayer + ".Main", EditingLayer + ".Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "10 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, CreateOrEditKit),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = ColorWhite
				}
			}, EditingLayer + ".Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-35 -37.5",
					OffsetMax = "-10 -12.5"
				},
				Text =
				{
					Text = Msg(player, Close),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = ColorWhite
				},
				Button =
				{
					Close = EditingLayer,
					Color = ColorThree,
					Command = "UI_Kits close"
				}
			}, EditingLayer + ".Header");

			if (IsAdmin(player))
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = "-140 -37.5",
						OffsetMax = "-45 -12.5"
					},
					Text =
					{
						Text = Msg(player, MainMenu),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = ColorTwo,
						Command = $"{_config.Commands.GetRandom()}",
						Close = EditingLayer
					}
				}, EditingLayer + ".Header");

			#endregion

			#region Fields

			var ySwitch = -60f;
			var Height = 60f;
			var Width = 225f;
			var xMargin = 35f;
			var yMargin = 10f;


			var i = 1;
			foreach (var obj in kitEditing[player].Where(x => x.Key != "Hide" && x.Key != "AutoKit"))
			{
				var xSwitch = i % 2 == 0 ? xMargin / 2f : -Width - xMargin / 2f;

				EditFieldUi(ref container, EditingLayer + ".Main", EditingLayer + $".Editing.{i}",
					$"{xSwitch} {ySwitch - Height}",
					$"{xSwitch + Width} {ySwitch}",
					$"UI_Kits editkit {creating} {kitId} {obj.Key} ",
					obj);

				if (i % 2 == 0) ySwitch = ySwitch - Height - yMargin;

				i++;
			}

			#region Hide

			var hide = !(kitEditing[player]["Hide"] is bool && (bool) kitEditing[player]["Hide"]);

			CheckBoxUi(ref container, EditingLayer + ".Main", EditingLayer + ".Editing.Hide", "0.5 1", "0.5 1",
				$"{-Width - xMargin / 2f} {ySwitch - 10}",
				$"{-Width - xMargin / 2f + 10} {ySwitch}",
				hide,
				$"UI_Kits editkit {creating} {kitId} Hide {hide}",
				Msg(player, EnableKit)
			);

			#endregion

			#region Auto Kit

			var autoKit = kitEditing[player]["AutoKit"] is bool && (bool) kitEditing[player]["AutoKit"];

			CheckBoxUi(ref container, EditingLayer + ".Main", EditingLayer + ".Editing.AutoKit", "0.5 1", "0.5 1",
				$"{-Width - xMargin / 2f + 80} {ySwitch - 10}",
				$"{-Width - xMargin / 2f + 90} {ySwitch}",
				autoKit,
				$"UI_Kits editkit {creating} {kitId} AutoKit {!autoKit}",
				Msg(player, AutoKit)
			);

			#endregion

			#endregion

			ySwitch -= 35f;

			#region Buttons

			#region Save Kit

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = $"15 {ySwitch - 25}",
					OffsetMax = $"115 {ySwitch}"
				},
				Text =
				{
					Text = Msg(player, SaveKit),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = ColorThree,
					Command = $"UI_Kits savekit {creating} {kitId}",
					Close = EditingLayer
				}
			}, EditingLayer + ".Main");

			#endregion

			#region Add From Inventory

			if (!creating)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"-100 {ySwitch - 25}",
						OffsetMax = $"100 {ySwitch}"
					},
					Text =
					{
						Text = Msg(player, CopyItems),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = HexToCuiColor("#50965F"),
						Command = $"UI_Kits frominv {kitId}",
						Close = EditingLayer
					}
				}, EditingLayer + ".Main");

			#endregion

			#region Remove Kit

			if (!creating)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = $"-115 {ySwitch - 25}",
						OffsetMax = $"-15 {ySwitch}"
					},
					Text =
					{
						Text = Msg(player, RemoveKit),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = ColorRed,
						Command = $"UI_Kits removekit {kitId}",
						Close = EditingLayer
					}
				}, EditingLayer + ".Main");

			#endregion

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, EditingLayer);
			CuiHelper.AddUi(player, container);
		}

		private void EditingItemUi(BasePlayer player, int kitId, int slot, string itemContainer, bool First = false)
		{
			var container = new CuiElementContainer();

			#region Dictionary

			if (!itemEditing.ContainsKey(player))
			{
				var kit = _data.Kits.Find(x => x.ID == kitId);
				if (kit == null) return;

				var item = kit.Items.Find(x => x.Container == itemContainer && x.Position == slot);
				if (item != null)
					itemEditing.Add(player, new Dictionary<string, object>
					{
						["Type"] = item.Type,
						["Command"] = item.Command,
						["Container"] = item.Container,
						["ShortName"] = item.ShortName,
						["Amount"] = item.Amount,
						["Blueprint"] = item.Blueprint,
						["SkinID"] = item.SkinID,
						["Chance"] = item.Chance,
						["Position"] = item.Position
					});
				else
					itemEditing.Add(player, new Dictionary<string, object>
					{
						["Type"] = KitItemType.Item,
						["Container"] = itemContainer,
						["Command"] = string.Empty,
						["ShortName"] = string.Empty,
						["Amount"] = 1,
						["Blueprint"] = 0,
						["SkinID"] = 0UL,
						["Chance"] = 100,
						["Position"] = slot
					});

				player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);

				Subscribe(nameof(CanSpectateTarget));
			}

			#endregion

			var edit = itemEditing[player];

			#region Background

			if (First)
			{
				CuiHelper.DestroyUi(player, EditingLayer);

				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = HexToCuiColor(_config.ColorOne, 80)},
					CursorEnabled = true
				}, "Overlay", EditingLayer);
			}

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-260 -240",
					OffsetMax = "260 250"
				},
				Image =
				{
					Color = ColorTwo
				}
			}, EditingLayer, EditingLayer + ".Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = ColorOne}
			}, EditingLayer + ".Main", EditingLayer + ".Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "10 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, EditingTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = ColorWhite
				}
			}, EditingLayer + ".Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-35 -37.5",
					OffsetMax = "-10 -12.5"
				},
				Text =
				{
					Text = Msg(player, Close),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = ColorWhite
				},
				Button =
				{
					Close = EditingLayer,
					Color = ColorThree,
					Command = $"UI_Kits infokit {kitId}"
				}
			}, EditingLayer + ".Header");

			#endregion

			#region Type

			var type = edit["Type"] as KitItemType? ?? KitItemType.Item;

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "10 -110",
					OffsetMax = "115 -80"
				},
				Text =
				{
					Text = Msg(player, ItemName),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = HexToCuiColor(_config.ColorThree, type == KitItemType.Item ? 100 : 50),
					Command = $"UI_Kits edititem {itemContainer} {kitId} {slot} Type {KitItemType.Item}"
				}
			}, EditingLayer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "135 -110",
					OffsetMax = "240 -80"
				},
				Text =
				{
					Text = Msg(player, CmdName),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = HexToCuiColor(_config.ColorThree, type == KitItemType.Command ? 100 : 50),
					Command = $"UI_Kits edititem {itemContainer} {kitId} {slot} Type {KitItemType.Command}"
				}
			}, EditingLayer + ".Main");

			#endregion

			#region Command

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-240 -110",
				"0 -60",
				$"UI_Kits edititem {itemContainer} {kitId} {slot} Command ",
				new KeyValuePair<string, object>("Command", edit["Command"]));

			#endregion

			#region Item

			var shortName = (string) edit["ShortName"];

			#region Image

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-240 -265", OffsetMax = "-105 -130"
				},
				Image = {Color = ColorOne}
			}, EditingLayer + ".Main", EditingLayer + ".Image");

			if (!string.IsNullOrEmpty(shortName) && ImageLibrary)
				container.Add(new CuiElement
				{
					Parent = EditingLayer + ".Image",
					Components =
					{
						new CuiRawImageComponent
							{Png = ImageLibrary.Call<string>("GetImage", shortName, edit["SkinID"])},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "10 10", OffsetMax = "-10 -10"
						}
					}
				});

			#endregion

			#region ShortName

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-85 -190",
				"140 -130",
				$"UI_Kits edititem {itemContainer} {kitId} {slot} ShortName ",
				new KeyValuePair<string, object>("ShortName", edit["ShortName"]));

			#endregion

			#region Select Item

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-85 -265",
					OffsetMax = "55 -235"
				},
				Text =
				{
					Text = Msg(player, BtnSelect),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = ColorThree,
					Command = $"UI_Kits selectitem {itemContainer} {kitId} {slot}"
				}
			}, EditingLayer + ".Main");

			#endregion

			#region Blueprint

			var bp = edit["Blueprint"] as int? ?? 0;
			CheckBoxUi(ref container,
				EditingLayer + ".Main",
				CuiHelper.GetGuid(),
				"0.5 1", "0.5 1",
				"65 -255",
				"75 -245",
				bp == 1,
				$"UI_Kits edititem {itemContainer} {kitId} {slot} Blueprint {(bp == 0 ? 1 : 0)}",
				Msg(player, BluePrint)
			);

			#endregion

			#region Amount

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-240 -345",
				"-7.5 -285",
				$"UI_Kits edititem {itemContainer} {kitId} {slot} Amount ",
				new KeyValuePair<string, object>("Amount", edit["Amount"]));

			#endregion

			#region Chance

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"7.5 -345",
				"240 -285",
				$"UI_Kits edititem {itemContainer} {kitId} {slot} Chance ",
				new KeyValuePair<string, object>("Chance", edit["Chance"]));

			#endregion

			#region Skin

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-240 -425",
				"240 -365",
				$"UI_Kits edititem {itemContainer} {kitId} {slot} SkinID ",
				new KeyValuePair<string, object>("SkinID", edit["SkinID"]));

			#endregion

			#endregion

			#region Save Button

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "-90 10",
					OffsetMax = $"{(slot == -1 ? 90 : 55)} 40"
				},
				Text =
				{
					Text = Msg(player, BtnSave),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = ColorThree,
					Command = $"UI_Kits saveitem {kitId} {slot} {itemContainer}",
					Close = EditingLayer
				}
			}, EditingLayer + ".Main");

			#endregion

			#region Save Button

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "60 10",
					OffsetMax = "90 40"
				},
				Text =
				{
					Text = Msg(player, RemoveItem),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = ColorRed,
					Command = $"UI_Kits removeitem {kitId} {slot} {itemContainer}",
					Close = EditingLayer
				}
			}, EditingLayer + ".Main");

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, EditingLayer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		private void SelectItem(BasePlayer player, int kitId, int slot, string itemContainer,
			string selectedCategory = "", int page = 0, string input = "")
		{
			if (string.IsNullOrEmpty(selectedCategory)) selectedCategory = ItemsCategories.FirstOrDefault().Key;

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Close = ModalLayer,
					Color = HexToCuiColor(_config.ColorOne, 80)
				}
			}, "Overlay", ModalLayer);

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-260 -270",
					OffsetMax = "260 280"
				},
				Image =
				{
					Color = ColorTwo
				}
			}, ModalLayer, ModalLayer + ".Main");

			#region Categories

			var amountOnString = 4;
			var Width = 120f;
			var Height = 25f;
			var xMargin = 5f;
			var yMargin = 5f;

			var constSwitch = -(amountOnString * Width + (amountOnString - 1) * xMargin) / 2f;
			var xSwitch = constSwitch;
			var ySwitch = -15f;

			var i = 1;
			foreach (var category in ItemsCategories)
			{
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} {ySwitch - Height}",
						OffsetMax = $"{xSwitch + Width} {ySwitch}"
					},
					Text =
					{
						Text = $"{category.Key}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = selectedCategory == category.Key
							? ColorThree
							: ColorOne,
						Command = $"UI_Kits selectitem {itemContainer} {kitId} {slot}  {category.Key}"
					}
				}, ModalLayer + ".Main");

				if (i % amountOnString == 0)
				{
					ySwitch = ySwitch - Height - yMargin;
					xSwitch = constSwitch;
				}
				else
				{
					xSwitch += xMargin + Width;
				}

				i++;
			}

			#endregion

			#region Items

			amountOnString = 5;

			var strings = 4;
			var totalAmount = amountOnString * strings;

			ySwitch = ySwitch - yMargin - Height - 10f;

			Width = 85f;
			Height = 85f;
			xMargin = 15f;
			yMargin = 5f;

			constSwitch = -(amountOnString * Width + (amountOnString - 1) * xMargin) / 2f;
			xSwitch = constSwitch;

			i = 1;

			var canSearch = !string.IsNullOrEmpty(input) && input.Length > 2;

			var temp = canSearch
				? ItemsCategories
					.SelectMany(x => x.Value)
					.Where(x => x.StartsWith(input) || x.Contains(input) || x.EndsWith(input)).ToList()
				: ItemsCategories[selectedCategory];

			var itemsAmount = temp.Count;
			var Items = temp.Skip(page * totalAmount).Take(totalAmount).ToList();

			Items.ForEach(item =>
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} {ySwitch - Height}",
						OffsetMax = $"{xSwitch + Width} {ySwitch}"
					},
					Image = {Color = ColorOne}
				}, ModalLayer + ".Main", ModalLayer + $".Item.{item}");

				if (ImageLibrary)
					container.Add(new CuiElement
					{
						Parent = ModalLayer + $".Item.{item}",
						Components =
						{
							new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", item)},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "5 5", OffsetMax = "-5 -5"
							}
						}
					});

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command = $"UI_Kits takeitem {itemContainer} {kitId} {slot} {item}",
						Close = ModalLayer
					}
				}, ModalLayer + $".Item.{item}");

				if (i % amountOnString == 0)
				{
					xSwitch = constSwitch;
					ySwitch = ySwitch - yMargin - Height;
				}
				else
				{
					xSwitch += xMargin + Width;
				}

				i++;
			});

			#endregion

			#region Search

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "-90 10", OffsetMax = "90 35"
				},
				Image = {Color = ColorThree}
			}, ModalLayer + ".Main", ModalLayer + ".Search");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "10 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = canSearch ? $"{input}" : Msg(player, ItemSearch),
					Align = canSearch ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = canSearch ? "1 1 1 0.8" : "1 1 1 1"
				}
			}, ModalLayer + ".Search");

			container.Add(new CuiElement
			{
				Parent = ModalLayer + ".Search",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 10,
						Align = TextAnchor.MiddleLeft,
						Command = $"UI_Kits selectitem {itemContainer} {kitId} {slot} {selectedCategory} 0 ",
						Color = "1 1 1 0.95",
						CharsLimit = 150
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "0 0"
					}
				}
			});

			#endregion

			#region Pages

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "0 0",
					OffsetMin = "10 10",
					OffsetMax = "80 35"
				},
				Text =
				{
					Text = Msg(player, Back),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = ColorOne,
					Command = page != 0
						? $"UI_Kits selectitem {itemContainer} {kitId} {slot} {selectedCategory} {page - 1} {input}"
						: ""
				}
			}, ModalLayer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 0", AnchorMax = "1 0",
					OffsetMin = "-80 10",
					OffsetMax = "-10 35"
				},
				Text =
				{
					Text = Msg(player, Next),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = ColorThree,
					Command = itemsAmount > (page + 1) * totalAmount
						? $"UI_Kits selectitem {itemContainer} {kitId} {slot} {selectedCategory} {page + 1} {input}"
						: ""
				}
			}, ModalLayer + ".Main");

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, ModalLayer);
			CuiHelper.AddUi(player, container);
		}

		private void ErrorUi(BasePlayer player, string msg)
		{
			var container = new CuiElementContainer
			{
				{
					new CuiPanel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Image = {Color = HexToCuiColor(_config.ColorTwo, 98)},
						CursorEnabled = true
					},
					"Overlay", ModalLayer
				},
				{
					new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5",
							AnchorMax = "0.5 0.5",
							OffsetMin = "-127.5 -75",
							OffsetMax = "127.5 140"
						},
						Image = {Color = ColorRed}
					},
					ModalLayer, ModalLayer + ".Main"
				},
				{
					new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -165", OffsetMax = "0 0"
						},
						Text =
						{
							Text = "XXX",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 120,
							Color = ColorWhite
						}
					},
					ModalLayer + ".Main"
				},
				{
					new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -175", OffsetMax = "0 -155"
						},
						Text =
						{
							Text = $"{msg}",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = ColorWhite
						}
					},
					ModalLayer + ".Main"
				},
				{
					new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 30"
						},
						Text =
						{
							Text = Msg(player, BtnClose),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = ColorWhite
						},
						Button = {Color = HexToCuiColor("#CD3838"), Close = ModalLayer}
					},
					ModalLayer + ".Main"
				}
			};

			CuiHelper.DestroyUi(player, ModalLayer);
			CuiHelper.AddUi(player, container);
		}

		private void EditFieldUi(ref CuiElementContainer container,
			string parent,
			string name,
			string oMin,
			string oMax,
			string command,
			KeyValuePair<string, object> obj)
		{
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = $"{oMin}",
					OffsetMax = $"{oMax}"
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, parent, name);

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -20", OffsetMax = "0 0"
				},
				Text =
				{
					Text = $"{obj.Key}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				}
			}, name);

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "0 0", OffsetMax = "0 -20"
				},
				Image = {Color = "0 0 0 0"}
			}, name, $"{name}.Value");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "10 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = $"{obj.Value}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 0.15"
				}
			}, $"{name}.Value");

			CreateOutLine(ref container, $"{name}.Value", ColorOne);

			container.Add(new CuiElement
			{
				Parent = $"{name}.Value",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 12,
						Align = TextAnchor.MiddleLeft,
						Command = $"{command}",
						Color = "1 1 1 0.99",
						CharsLimit = 150
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "0 0"
					}
				}
			});
		}

		private void CheckBoxUi(ref CuiElementContainer container, string parent, string name, string aMin, string aMax,
			string oMin, string oMax, bool enabled,
			string command, string text)
		{
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = aMin, AnchorMax = aMax,
					OffsetMin = oMin,
					OffsetMax = oMax
				},
				Image = {Color = "0 0 0 0"}
			}, parent, name);

			CreateOutLine(ref container, name, ColorThree, 1);

			if (enabled)
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Image = {Color = ColorThree}
				}, name);


			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0",
					Command = $"{command}"
				}
			}, name);

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "1 0.5", AnchorMax = "1 0.5",
					OffsetMin = "5 -10",
					OffsetMax = "100 10"
				},
				Text =
				{
					Text = $"{text}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = ColorWhite
				}
			}, name);
		}

		private void InfoItemUi(ref CuiElementContainer container, BasePlayer player, int slot, string oMin,
			string oMax, Kit kit,
			KitItem kitItem, int total, string itemContainer)
		{
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = $"{oMin}",
					OffsetMax = $"{oMax}"
				},
				Image =
				{
					Color = ColorOne
				}
			}, InfoLayer, InfoLayer + $".Item.{total}");

			if (kitItem != null)
			{
				if (ImageLibrary)
					container.Add(new CuiElement
					{
						Parent = InfoLayer + $".Item.{total}",
						Components =
						{
							new CuiRawImageComponent
							{
								Png = !string.IsNullOrEmpty(kitItem.Image)
									? ImageLibrary.Call<string>("GetImage", kitItem.Image)
									: ImageLibrary.Call<string>("GetImage", kitItem.ShortName, kitItem.SkinID)
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "10 10", OffsetMax = "-10 -10"
							}
						}
					});

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "2.5 3.5", OffsetMax = "-2.5 -2.5"
					},
					Text =
					{
						Text = $"x{kitItem.Amount}",
						Align = TextAnchor.LowerRight,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					}
				}, InfoLayer + $".Item.{total}");

				var color = _config.RarityColors.Find(x => x.Chance == kitItem.Chance);
				if (color != null)
				{
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "0 0", OffsetMax = "0 2"
						},
						Image =
						{
							Color = HexToCuiColor(color.Color)
						}
					}, InfoLayer + $".Item.{total}");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "2.5 2.5", OffsetMax = "-2.5 -2.5"
						},
						Text =
						{
							Text = $"{kitItem.Chance}%",
							Align = TextAnchor.UpperLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 10,
							Color = "1 1 1 1"
						}
					}, InfoLayer + $".Item.{total}");
				}
			}

			if (IsAdmin(player))
				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command =
							$"UI_Kits startedititem {itemContainer} {kit.ID} {slot}",
						Close = InfoLayer
					}
				}, InfoLayer + $".Item.{total}");
		}

		private void RefreshKitUi(ref CuiElementContainer container, BasePlayer player, Kit kit)
		{
			var playerData = GetPlayerData(player.userID, kit.Name);
			if (playerData == null) return;

			CuiHelper.DestroyUi(player, Layer + $".Kit.{kit.ID}");

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1"},
				Image = {Color = "0 0 0 0"}
			}, Layer + $".Kit.{kit.ID}.Main", Layer + $".Kit.{kit.ID}");

			if (_config.ShowAllKits && _config.ShowNoPermDescription && !string.IsNullOrEmpty(kit.Permission) &&
			    !permission.UserHasPermission(player.UserIDString, kit.Permission))
			{
				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.NoPermission.AnchorMin, AnchorMax = _config.UI.NoPermission.AnchorMax,
						OffsetMin = _config.UI.NoPermission.OffsetMin, OffsetMax = _config.UI.NoPermission.OffsetMax
					},
					Text =
					{
						Text = Msg(player, NoPermissionDescription),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = ColorFour
					}
				}, Layer + $".Kit.{kit.ID}");
				return;
			}

			if (kit.Cooldown > 0 && playerData.Cooldown - 1 < GetCurrentTime() || kit.Cooldown == 0)
			{
				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.KitAvailable.AnchorMin, AnchorMax = _config.UI.KitAvailable.AnchorMax,
						OffsetMin = _config.UI.KitAvailable.OffsetMin, OffsetMax = _config.UI.KitAvailable.OffsetMax
					},
					Text =
					{
						Text = Msg(player, KitAvailableTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = ColorFour
					}
				}, Layer + $".Kit.{kit.ID}");
			}
			else
			{
				var time = TimeSpan.FromSeconds(playerData.Cooldown - GetCurrentTime());

				if (kit.Amount > 0)
				{
					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = _config.UI.KitAmountCooldown.AnchorMin,
							AnchorMax = _config.UI.KitAmountCooldown.AnchorMax,
							OffsetMin = _config.UI.KitAmountCooldown.OffsetMin,
							OffsetMax = _config.UI.KitAmountCooldown.OffsetMax
						},
						Text =
						{
							Text = $"{FormatShortTime(time)}",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						}
					}, Layer + $".Kit.{kit.ID}", Layer + $".Kit.{kit.ID}.Cooldown");
				}
				else
				{
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = _config.UI.KitCooldown.AnchorMin, AnchorMax = _config.UI.KitCooldown.AnchorMax,
							OffsetMin = _config.UI.KitCooldown.OffsetMin, OffsetMax = _config.UI.KitCooldown.OffsetMax
						},
						Image = {Color = HexToCuiColor(kit.Color)}
					}, Layer + $".Kit.{kit.ID}", Layer + $".Kit.{kit.ID}.Cooldown");

					container.Add(new CuiLabel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text =
						{
							Text = $"{FormatShortTime(time)}",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						}
					}, Layer + $".Kit.{kit.ID}.Cooldown");
				}
			}

			if (kit.Amount > 0)
			{
				var width = kit.Amount == 1
					? _config.UI.KitAmount.Width
					: _config.UI.KitAmount.Width / kit.Amount * 0.9f;

				var margin = (_config.UI.KitAmount.Width - width * kit.Amount) / (kit.Amount - 1);

				var xSwitch = -(_config.UI.KitAmount.Width / 2f);

				for (var i = 0; i < kit.Amount; i++)
				{
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = _config.UI.KitAmount.AnchorMin, AnchorMax = _config.UI.KitAmount.AnchorMax,
							OffsetMin = $"{xSwitch} {_config.UI.KitAmount.OffsetMin}",
							OffsetMax = $"{xSwitch + width} {_config.UI.KitAmount.OffsetMax}"
						},
						Image =
						{
							Color = i < playerData.Amount ? HexToCuiColor(kit.Color) : ColorTwo
						}
					}, Layer + $".Kit.{kit.ID}");

					xSwitch += width + margin;
				}
			}
		}

		#endregion

		#region Kit Helpers

		private void GiveKit(BasePlayer player, Kit kit, bool force = false)
		{
			if (player == null || kit == null) return;

			if (Interface.Oxide.CallHook("canRedeemKit", player) != null)
				return;

			if (!force && !string.IsNullOrEmpty(kit.Permission) &&
			    !permission.UserHasPermission(player.UserIDString, kit.Permission))
			{
				ErrorUi(player, Msg(player, NoPermission));
				return;
			}

			if (!force && _config.BlockBuilding && !player.CanBuild())
			{
				ErrorUi(player, Msg(player, BBlocked));
				return;
			}

			var currentTime = GetCurrentTime();

			if (!force && kit.CooldownAfterWipe > 0)
			{
				var leftTime = UnBlockTime(kit.CooldownAfterWipe) - currentTime;
				if (leftTime > 0)
				{
					ErrorUi(player,
						Msg(player, KitCooldown,
							FormatShortTime(TimeSpan.FromSeconds(leftTime))));

					return;
				}
			}

			var playerData = GetPlayerData(player.userID, kit.Name);

			if (!force && kit.Amount > 0 && playerData.Amount >= kit.Amount)
			{
				ErrorUi(player, Msg(player, KitLimit));
				return;
			}

			if (!force && kit.Cooldown > 0)
				if (playerData.Cooldown > currentTime)
				{
					ErrorUi(player,
						Msg(player, KitCooldown,
							FormatShortTime(TimeSpan.FromSeconds(playerData.Cooldown - currentTime))));
					return;
				}

			if (CopyPaste && !string.IsNullOrEmpty(kit.Building))
			{
				var success = CopyPaste?.Call("TryPasteFromSteamId", player.userID, kit.Building,
					_config.CopyPasteParameters.ToArray());
				if (success is string)
				{
					SendNotify(player, BuildError, 1);
					return;
				}
			}

			var beltcount = kit.Items.Count(i => i.Container == "belt");
			var wearcount = kit.Items.Count(i => i.Container == "wear");
			var maincount = kit.Items.Count(i => i.Container == "main");
			var totalcount = beltcount + wearcount + maincount;
			if (player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count < beltcount ||
			    player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count < wearcount ||
			    player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count < maincount)
				if (totalcount > player.inventory.containerMain.capacity -
					player.inventory.containerMain.itemList.Count)
				{
					ErrorUi(player, Msg(player, NotEnoughtSpace));
					return;
				}

			kit.Items.ForEach(item => item?.Get(player));

			if (!force && kit.Amount > 0) playerData.Amount += 1;

			if (!force && kit.Cooldown > 0)
				playerData.Cooldown = GetCurrentTime() + GetCooldown(kit.Cooldown, player);

			SendNotify(player, KitClaimed, 0, kit.DisplayName);

			openGUI.Remove(player);

			CuiHelper.DestroyUi(player, Layer);

			Interface.CallHook("OnKitRedeemed", player, kit.Name);

			Log(player, kit.Name);
		}

		private double GetCooldown(double cooldown, BasePlayer player)
		{
			var cd = Interface.Oxide.CallHook("OnKitCooldown", player, cooldown) as double?;
			if (cd != null) return (double) cd;

			return cooldown;
		}

		private List<KitItem> GetPlayerItems(BasePlayer player)
		{
			var kititems = new List<KitItem>();

			player.inventory.containerWear.itemList.ForEach(item =>
			{
				if (item == null) return;
				kititems.Add(ItemToKit(item, "wear"));
			});

			player.inventory.containerMain.itemList.ForEach(item =>
			{
				if (item == null) return;
				kititems.Add(ItemToKit(item, "main"));
			});

			player.inventory.containerBelt.itemList.ForEach(item =>
			{
				if (item == null) return;
				kititems.Add(ItemToKit(item, "belt"));
			});

			return kititems;
		}

		private KitItem ItemToKit(Item item, string container)
		{
			var kitem = new KitItem
			{
				Amount = item.amount,
				Container = container,
				SkinID = item.skin,
				Blueprint = item.blueprintTarget,
				ShortName = item.info.shortname,
				Condition = item.condition,
				Weapon = null,
				Content = null,
				Chance = 100,
				Command = string.Empty,
				Position = item.position
			};

			if (item.info.category == ItemCategory.Weapon)
			{
				var weapon = item.GetHeldEntity() as BaseProjectile;
				if (weapon != null)
					kitem.Weapon = new Weapon
					{
						ammoType = weapon.primaryMagazine.ammoType.shortname,
						ammoAmount = weapon.primaryMagazine.contents
					};
			}

			if (item.contents != null)
				kitem.Content = item.contents.itemList.Select(cont => new ItemContent
				{
					Amount = cont.amount,
					Condition = cont.condition,
					ShortName = cont.info.shortname
				}).ToList();

			return kitem;
		}

		#endregion

		#region Utils

		private void FillCategories()
		{
			ItemManager.itemList.ForEach(item =>
			{
				var itemCategory = item.category.ToString();

				if (ItemsCategories.ContainsKey(itemCategory))
				{
					if (!ItemsCategories[itemCategory].Contains(item.shortname))
						ItemsCategories[itemCategory].Add(item.shortname);
				}
				else
				{
					ItemsCategories.Add(itemCategory, new List<string> {item.shortname});
				}
			});
		}

		private void HandleUi()
		{
			var toRemove = Pool.GetList<BasePlayer>();

			foreach (var check in openGUI)
			{
				var player = check.Key;
				if (player == null || !player.IsConnected)
				{
					toRemove.Add(player);
					continue;
				}

				var container = new CuiElementContainer();

				check.Value.ForEach(kit => RefreshKitUi(ref container, player, kit));

				CuiHelper.AddUi(player, container);
			}

			toRemove.ForEach(x => openGUI.Remove(x));
			Pool.FreeList(ref toRemove);
		}

		private void FixItemsPositions()
		{
			_data.Kits.ForEach(kit =>
			{
				var Positions = new Dictionary<string, int>
				{
					["belt"] = 0,
					["main"] = 0,
					["wear"] = 0
				};

				kit.Items.ForEach(item =>
				{
					if (Positions.ContainsKey(item.Container) && item.Position == -1)
					{
						item.Position = Positions[item.Container];

						Positions[item.Container] += 1;
					}
				});
			});

			SaveKits();
		}

		private void LoadImages()
		{
			if (!ImageLibrary)
			{
				PrintError("IMAGE LIBRARY IS NOT INSTALLED!");
			}
			else
			{
				var imagesList = new Dictionary<string, string>();

				var itemIcons = new List<KeyValuePair<string, ulong>>();

				_data.Kits.ForEach(kit =>
				{
					if (_config.UI.Image.Enabled && !string.IsNullOrEmpty(kit.Image)
					                             && !imagesList.ContainsKey(kit.Image))
						imagesList.Add(kit.Image, kit.Image);

					kit.Items.ForEach(item =>
					{
						if (!string.IsNullOrEmpty(item.Image) && !imagesList.ContainsKey(item.Image))
							imagesList.Add(item.Image, item.Image);

						itemIcons.Add(new KeyValuePair<string, ulong>(item.ShortName, item.SkinID));
					});
				});

				if (itemIcons.Count > 0) ImageLibrary?.Call("LoadImageList", Title, itemIcons, null);

				ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
			}
		}

		private static string HexToCuiColor(string HEX, float Alpha = 100)
		{
			if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

			var str = HEX.Trim('#');
			if (str.Length != 6) throw new Exception(HEX);
			var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
			var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
			var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

			return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100f}";
		}

		private static string FormatShortTime(TimeSpan time)
		{
			return time.ToShortString();
		}

		private static void CreateOutLine(ref CuiElementContainer container, string parent, string color,
			float size = 2)
		{
			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0",
						AnchorMax = "1 0",
						OffsetMin = $"{size} 0",
						OffsetMax = $"-{size} {size}"
					},
					Image = {Color = color}
				},
				parent);
			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = $"{size} -{size}",
						OffsetMax = $"-{size} 0"
					},
					Image = {Color = color}
				},
				parent);
			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = "0 0",
						OffsetMax = $"{size} 0"
					},
					Image = {Color = color}
				},
				parent);
			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "1 0",
						AnchorMax = "1 1",
						OffsetMin = $"-{size} 0",
						OffsetMax = "0 0"
					},
					Image = {Color = color}
				},
				parent);
		}

		private List<Kit> GetAvailableKits(BasePlayer player, string targetId = "0", bool showAll = false,
			bool checkAmount = true)
		{
			return IsAdmin(player) && showAll
				? _data.Kits
				: _data.Kits.FindAll(x =>
					!x.Hide &&
					(targetId == "0" || _config.NpcKits.ContainsKey(targetId) &&
						_config.NpcKits[targetId].Kits.Contains(x.Name)) &&
					(!checkAmount || x.Amount == 0 || x.Amount > 0 &&
						GetPlayerData(player.userID, x.Name).Amount < x.Amount) &&
					(_config.ShowAllKits || string.IsNullOrEmpty(x.Permission) ||
					 permission.UserHasPermission(player.UserIDString, x.Permission)));
		}

		private List<Kit> GetAutoKits(BasePlayer player)
		{
			return _data.Kits
				.FindAll(kit => kit.Name == "autokit" || _config.AutoKits.Contains(kit.Name) &&
					(string.IsNullOrEmpty(kit.Permission) ||
					 permission.UserHasPermission(
						 player.UserIDString, kit.Permission)));
		}

		private double UnBlockTime(double amount)
		{
			return TimeSpan.FromTicks(SaveRestore.SaveCreatedTime.ToUniversalTime().Ticks).TotalSeconds + amount;
		}

		private static double GetCurrentTime()
		{
			return TimeSpan.FromTicks(DateTime.UtcNow.Ticks).TotalSeconds;
		}

		private bool IsAdmin(BasePlayer player)
		{
			return player != null && (player.IsAdmin || permission.UserHasPermission(player.UserIDString, PERM_ADMIN));
		}

		#endregion

		#region Log

		private void Log(BasePlayer player, string kitname)
		{
			if (player == null) return;

			var text = $"{player.displayName}[{player.UserIDString}] - Received Kit: {kitname}";

			if (_config.Logs.Console)
				Puts(text);

			if (_config.Logs.Console)
				LogToFile(Name, $"[{DateTime.Now}] {text}", this);
		}

		#endregion

		#region Lang

		private const string
			KitExist = "KitExist",
			KitNotExist = "KitNotExist",
			KitRemoved = "KitRemoved",
			AccessDenied = "AccessDenied",
			KitLimit = "KitLimit",
			KitCooldown = "KitCooldown",
			KitCreate = "KitCreate",
			KitClaimed = "KitClaimed",
			NotEnoughtSpace = "NotEnoughtSpace",
			NotifyTitle = "NotifyTitle",
			Close = "Close",
			MainTitle = "MainTitle",
			Back = "Back",
			Next = "Next",
			NotAvailableKits = "NoAvailabeKits",
			CreateKit = "CreateKit",
			ListKits = "ListKits",
			ShowAll = "ShowAll",
			KitInfo = "KitInfo",
			KitTake = "KitGet",
			ComeBack = "ComeBack",
			Edit = "Edit",
			ContainerMain = "ContainerMain",
			ContaineWear = "ContaineWear",
			ContainerBelt = "ContainerBelt",
			CreateOrEditKit = "CreateOrEditKit",
			MainMenu = "MainMenu",
			EnableKit = "EnableKit",
			AutoKit = "AutoKit",
			SaveKit = "SaveKit",
			CopyItems = "CopyItems",
			RemoveKit = "RemoveKit",
			EditingTitle = "EditingTitle",
			ItemName = "ItemName",
			CmdName = "CmdName",
			BtnSelect = "BtnSelect",
			BluePrint = "BluePrint",
			BtnSave = "BtnSave",
			ItemSearch = "ItemSearch",
			BtnClose = "BtnClose",
			KitAvailableTitle = "KitAvailable",
			KitsList = "KitsList",
			KitsHelp = "KitsHelp",
			KitNotFound = "KitNotFound",
			RemoveItem = "RemoveItem",
			NoPermission = "NoPermission",
			BuildError = "BuildError",
			BBlocked = "BuildingBlocked",
			NoPermissionDescription = "NoPermissionDescription";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[KitExist] = "Kit with the same name already exist",
				[KitCreate] = "You have created a new kit - {0}",
				[KitNotExist] = "This kit doesn't exist",
				[KitRemoved] = "Kit {0} was removed",
				[AccessDenied] = "Access denied",
				[KitLimit] = "Usage limite reached",
				[KitCooldown] = "You will be able to use this kit after: {0}",
				[NotEnoughtSpace] = "Can't redeem kit. Not enought space",
				[KitClaimed] = "You have claimed kit - {0}",
				[NotifyTitle] = "KITS",
				[Close] = "✕",
				[MainTitle] = "Kits",
				[Back] = "Back",
				[Next] = "Next",
				[NotAvailableKits] = "NO KITS AVAILABLE FOR YOU :(",
				[CreateKit] = "Create Kit",
				[ListKits] = "List of kits",
				[ShowAll] = "Show all",
				[KitInfo] = "i",
				[KitTake] = "Take",
				[ComeBack] = "Come back",
				[Edit] = "Edit",
				[ContainerMain] = "Main",
				[ContaineWear] = "Wear",
				[ContainerBelt] = "Belt",
				[CreateOrEditKit] = "Create/Edit Kit",
				[MainMenu] = "Main menu",
				[EnableKit] = "Enable kit",
				[AutoKit] = "Auto kit",
				[SaveKit] = "Save kit",
				[CopyItems] = "Copy items from inventory",
				[RemoveKit] = "Remove kit",
				[EditingTitle] = "Item editing",
				[ItemName] = "Item",
				[CmdName] = "Command",
				[BtnSelect] = "Select",
				[BluePrint] = "Blueprint",
				[BtnSave] = "Save",
				[ItemSearch] = "Item search",
				[BtnClose] = "CLOSE",
				[KitAvailableTitle] = "KIT AVAILABLE\nTO RECEIVE",
				[KitsList] = "List of kits: {0}",
				[KitsHelp] =
					"KITS HELP\n- /{0} help - get help with kits\n- /{0} list - get a list of available kits\n- /{0} [name] - get the kit",
				[KitNotFound] = "Kit '{0}' not found",
				[RemoveItem] = "✕",
				[NoPermission] = "You don't have permission to get this kit",
				[BuildError] = "Can't place the building here",
				[BBlocked] = "Cannot do that while building blocked.",
				[NoPermissionDescription] = "PURCHASE THIS KIT AT\nSERVERNAME.GG"
			}, this);
		}

		private string Msg(string key, string userid = null, params object[] obj)
		{
			return string.Format(lang.GetMessage(key, this, userid), obj);
		}

		private string Msg(BasePlayer player, string key, params object[] obj)
		{
			return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
		}

		private void Reply(BasePlayer player, string key, params object[] obj)
		{
			SendReply(player, Msg(key, player.UserIDString, obj));
		}

		private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
		{
			if (Notify && _config.UseNotify)
				Notify?.Call("SendNotify", player, type, Msg(player, key, obj));
			else
				Reply(player, key, obj);
		}

		#endregion

		#region API

		private string[] GetAllKits()
		{
			return _data.Kits.Select(kit => kit.Name).ToArray();
		}

		private object GetKitInfo(string kitname)
		{
			var kit = _data.Kits.Find(x => x.Name == kitname);
			if (kit == null) return null;

			var obj = new JObject
			{
				["name"] = kit.Name,
				["displayname"] = kit.DisplayName,
				["color"] = kit.Color,
				["permission"] = kit.Permission,
				["image"] = kit.Image,
				["hide"] = kit.Hide,
				["amount"] = kit.Amount,
				["cooldown"] = kit.Cooldown
			};

			var items = new JArray();
			foreach (var item in kit.Items.Select(itemEntry => new JObject
			{
				["type"] = itemEntry.Type.ToString(),
				["command"] = itemEntry.Command,
				["shortname"] = itemEntry.ShortName,
				["amount"] = itemEntry.Amount,
				["blueprint"] = itemEntry.Blueprint,
				["skinid"] = itemEntry.SkinID,
				["container"] = itemEntry.Container,
				["condition"] = itemEntry.Condition,
				["chance"] = itemEntry.Chance
			}))
				items.Add(item);

			obj["items"] = items;
			return obj;
		}

		private string[] GetKitContents(string kitname)
		{
			var kit = _data.Kits.Find(x => x.Name == kitname);
			if (kit == null) return null;

			var items = new List<string>();
			foreach (var item in kit.Items)
			{
				var itemstring = $"{item.ShortName}_{item.Amount}";
				if (item.Content.Count > 0)
					itemstring = item.Content.Aggregate(itemstring, (current, mod) => current + $"_{mod.ShortName}");

				items.Add(itemstring);
			}

			return items.ToArray();
		}

		private double GetKitCooldown(string kitname)
		{
			return _data.Kits.Find(x => x.Name == kitname)?.Cooldown ?? 0;
		}

		private double PlayerKitCooldown(ulong ID, string kitname)
		{
			return GetPlayerData(ID, kitname).Cooldown;
		}

		private int KitMax(string kitname)
		{
			return _data.Kits.Find(x => x.Name == kitname)?.Amount ?? 0;
		}

		private double PlayerKitMax(ulong ID, string kitname)
		{
			return GetPlayerData(ID, kitname)?.Amount ?? 0;
		}

		private string KitImage(string kitname)
		{
			return _data.Kits.Find(x => x.Name == kitname)?.Image ?? string.Empty;
		}

		private void GiveKit(BasePlayer player, string kitname)
		{
			GiveKit(player, _data.Kits.Find(x => x.Name == kitname), true);
		}

		private bool isKit(string kitname)
		{
			return IsKit(kitname);
		}

		private bool IsKit(string kitname)
		{
			return _data.Kits.Exists(x => x.Name == kitname);
		}

		#endregion

		#region Convert

		[ConsoleCommand("kits.convert")]
		private void OldKitsConvert(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			OldData oldKits = null;

			try
			{
				oldKits = Interface.Oxide.DataFileSystem.ReadObject<OldData>("Kits/kits_data");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			var amount = 0;

			oldKits?._kits.ToList().ForEach(oldKit =>
			{
				var kit = new Kit
				{
					ID = ++LastKitID,
					Name = oldKit.Value.Name,
					DisplayName = oldKit.Value.Name,
					Permission = oldKit.Value.RequiredPermission,
					Amount = oldKit.Value.MaximumUses,
					Cooldown = oldKit.Value.Cooldown,
					Description = oldKit.Value.Description,
					Hide = oldKit.Value.IsHidden,
					Building = oldKit.Value.CopyPasteFile,
					Image = oldKit.Value.KitImage,
					Color = _config.KitColor,
					Items = new List<KitItem>()
				};

				foreach (var item in oldKit.Value.MainItems)
					kit.Items.Add(KitItem.FromOld(item, "main"));

				foreach (var item in oldKit.Value.WearItems)
					kit.Items.Add(KitItem.FromOld(item, "wear"));

				foreach (var item in oldKit.Value.BeltItems)
					kit.Items.Add(KitItem.FromOld(item, "belt"));

				_data.Kits.Add(kit);

				amount++;
			});

			Puts($"{amount} kits was converted!");

			SaveKits();
		}

		private class OldData
		{
			[JsonProperty] public Dictionary<string, OldKitsData> _kits =
				new Dictionary<string, OldKitsData>(StringComparer.OrdinalIgnoreCase);
		}

		private class OldKitsData
		{
			public string Name;
			public string Description;
			public string RequiredPermission;

			public int MaximumUses;
			public int RequiredAuth;
			public int Cooldown;
			public int Cost;

			public bool IsHidden;

			public string CopyPasteFile;
			public string KitImage;

			public ItemData[] MainItems;
			public ItemData[] WearItems;
			public ItemData[] BeltItems;
		}

		private class ItemData
		{
			public string Shortname;

			public ulong Skin;

			public int Amount;

			public float Condition;

			public float MaxCondition;

			public int Ammo;

			public string Ammotype;

			public int Position;

			public int Frequency;

			public string BlueprintShortname;

			public ItemData[] Contents;
		}

		#endregion
	}
}