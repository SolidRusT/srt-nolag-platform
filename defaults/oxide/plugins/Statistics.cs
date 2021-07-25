using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using WebSocketSharp;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
	[Info("Statistics", "Mevent", "1.0.9")]
	public class Statistics : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin ImageLibrary, PlayerDatabase;

		private const string Layer = "UI.Statistics";

		private const string UsePermission = "statistics.use";

		private const string HidePermission = "statistics.hide";

		#endregion

		#region Config

		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Statistics Commands",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] StatisticsCommands = {"stats", "statistics"};

			[JsonProperty(PropertyName = "Leaderboard Commands",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] LeaderboardCommands = {"leaders", "leaderboard"};

			[JsonProperty(PropertyName = "Automatic wipe on wipe")]
			public bool AutoWipe;

			[JsonProperty(PropertyName = "Access by permission (statistics.use)")]
			public bool AccessByPerm;

			[JsonProperty(PropertyName = "Weapons",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Weapons = new List<string>
			{
				"rifle.ak", "rifle.bolt", "rifle.l96", "rifle.lr300", "rifle.m39", "rifle.semiauto", "pistol.eoka",
				"pistol.m92", "pistol.nailgun", "pistol.python", "pistol.revolver", "pistol.semiauto"
			};

			[JsonProperty(PropertyName = "Resources",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Resources = new List<string>
			{
				"stones", "sulfur.ore", "metal.ore", "hq.metal.ore", "wood"
			};

			[JsonProperty(PropertyName = "Score Table (shortname - score)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, float> ScoreTable = new Dictionary<string, float>
			{
				["kills"] = 1,
				["deaths"] = -1,
				["stone-ore"] = 0.1f,
				["supply_drop"] = 3f,
				["crate_normal"] = 0.3f,
				["crate_elite"] = 0.5f,
				["bradley_crate"] = 5f,
				["heli_crate"] = 5f,
				["bradley"] = 10f,
				["helicopter"] = 15f,
				["barrel"] = 0.1f,
				["scientistnpc"] = 0.5f,
				["heavyscientist"] = 2f,
				["sulfur.ore"] = 0.5f,
				["metal.ore"] = 0.5f,
				["hq.metal.ore"] = 0.5f,
				["stones"] = 0.5f,
				["cupboard.tool.deployed"] = 1f
			};

			[JsonProperty(PropertyName = "PlayerDatabase")]
			public PlayerDatabaseConf PlayerDatabase = new PlayerDatabaseConf(false, "Statistics");
		}

		private class PlayerDatabaseConf
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Table")] public string Field;

			public PlayerDatabaseConf(bool enabled, string field)
			{
				Enabled = enabled;
				Field = field;
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

		private void SaveData(bool check = true)
		{
			Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

			if (check && _config.PlayerDatabase.Enabled)
				foreach (var player in BasePlayer.activePlayerList)
					UpdateStats(player);
		}

		private void LoadData()
		{
			try
			{
				_data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (_data == null) _data = new PluginData();
		}

		private class PluginData
		{
			[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
		}

		private class PlayerData
		{
			[JsonProperty(PropertyName = "Display Name")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Stats", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, float> Stats = new Dictionary<string, float>();

			#region Stats

			[JsonIgnore]
			public float Kills
			{
				get
				{
					float kills;
					Stats.TryGetValue("kills", out kills);
					return float.IsNaN(kills) || float.IsInfinity(kills) ? 0 : kills;
				}
			}

			[JsonIgnore]
			public float Deaths
			{
				get
				{
					float deaths;
					Stats.TryGetValue("deaths", out deaths);
					return float.IsNaN(deaths) || float.IsInfinity(deaths) ? 0 : deaths;
				}
			}

			[JsonIgnore]
			public float KD
			{
				get
				{
					var kd = Kills / Deaths;
					return float.IsNaN(kd) || float.IsInfinity(kd) ? 0 : kd;
				}
			}

			[JsonIgnore]
			public float Resources
			{
				get
				{
					var resources = Stats.Where(x => _config.Resources.Contains(x.Key)).Sum(x => x.Value);
					return float.IsNaN(resources) || float.IsInfinity(resources) ? 0 : resources;
				}
			}

			[JsonIgnore]
			public float Score
			{
				get
				{
					return (float) Math.Round(Stats
						.Where(x => _config.ScoreTable.ContainsKey(x.Key))
						.Sum(x => x.Value * _config.ScoreTable[x.Key]));
				}
			}

			#endregion
		}

		private PlayerData GetPlayerData(BasePlayer player)
		{
			return GetPlayerData(player.userID);
		}

		private PlayerData GetPlayerData(ulong member)
		{
			if (!_data.Players.ContainsKey(member))
				_data.Players.Add(member, new PlayerData());


			return _data.Players[member];
		}

		#region Stats

		private void AddToStats(ulong member, string shortName, int amount = 1)
		{
			if (!member.IsSteamId()) return;

			var data = GetPlayerData(member);
			if (data == null) return;

			if (data.Stats.ContainsKey(shortName))
				data.Stats[shortName] += amount;
			else
				data.Stats.Add(shortName, amount);
		}

		private float GetStatsValue(ulong member, string shortname)
		{
			var data = GetPlayerData(member);
			if (data == null) return 0;

			switch (shortname)
			{
				case "total":
				{
					return data.Score;
				}
				case "kd":
				{
					return data.KD;
				}
				case "resources":
				{
					return data.Resources;
				}
				default:
				{
					float result;
					return data.Stats.TryGetValue(shortname, out result) ? result : 0;
				}
			}
		}

		private int GetTop(ulong member, int mode = 0)
		{
			var i = 1;

			foreach (var player in _data.Players.OrderByDescending(x =>
			{
				switch (mode)
				{
					case 2:
						return x.Value.KD;
					case 1:
						return x.Value.Resources;
					default:
						return x.Value.Score;
				}
			}))
			{
				if (player.Key == member)
					return i;

				i++;
			}

			return i;
		}

		private string GetFavoriteWeapon(ulong member)
		{
			var data = GetPlayerData(member);
			if (data == null) return string.Empty;

			var weapon = data.Stats.Where(x => _config.Weapons.Contains(x.Key))
				.OrderByDescending(x => x.Value).FirstOrDefault().Key;

			return weapon.IsNullOrEmpty() ? "NONE" : ItemManager.FindItemDefinition(weapon)?.displayName?.translated;
		}

		#endregion

		#endregion

		#region Hooks

		private void Init()
		{
			LoadData();
		}

		private void OnServerInitialized()
		{
			LoadImages();

			if (_config.PlayerDatabase.Enabled && !PlayerDatabase)
				PrintWarning("PlayerDatabase NOT INSTALLED!!!");

			foreach (var player in BasePlayer.activePlayerList)
				OnPlayerConnected(player);

			if (!permission.PermissionExists(UsePermission))
				permission.RegisterPermission(UsePermission, this);

			if (!permission.PermissionExists(HidePermission))
				permission.RegisterPermission(HidePermission, this);

			AddCovalenceCommand(_config.LeaderboardCommands, nameof(CmdStats));
			AddCovalenceCommand(_config.StatisticsCommands, nameof(CmdStats));

			timer.Every(1, TimeHandle);
		}

		private void OnServerSave()
		{
			timer.In(Random.Range(2f, 7f), () => SaveData());
		}

		private void Unload()
		{
			SaveData();

			foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);

			_config = null;
		}

		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null || !player.userID.IsSteamId()) return;

			GetAvatar(player.userID,
				avatar => ImageLibrary?.Call("AddImage", avatar, $"avatar_{player.UserIDString}"));

			var data = GetPlayerData(player);
			if (data == null || player.displayName.IsNullOrEmpty()) return;

			data.DisplayName = player.displayName;

			if (_config.PlayerDatabase.Enabled) LoadStats(player);
		}

		private void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			UpdateStats(player);
		}

		private void OnNewSave(string filename)
		{
			if (!_config.AutoWipe) return;

			_data.Players.Clear();
			SaveData(false);
		}

		#region Stats

		#region Kills

		private void OnPlayerDeath(BasePlayer player, HitInfo info)
		{
			if (player == null || info == null ||
			    player.ShortPrefabName == "player" && !player.userID.IsSteamId()) return;

			var attacker = info.InitiatorPlayer;
			if (attacker == null || !attacker.userID.IsSteamId()
			                     || IsTeammates(player.userID, attacker.userID)) return;

			if (player.userID.IsSteamId())
			{
				AddToStats(attacker.userID, "kills");
				AddToStats(player.userID, "deaths");
			}
			else
			{
				AddToStats(attacker.userID, player.ShortPrefabName);
			}

			var activeItem = attacker.GetActiveItem();
			if (activeItem == null || !_config.Weapons.Contains(activeItem.info.shortname)) return;
			AddToStats(attacker.userID, activeItem.info.shortname);
		}

		#endregion

		#region Gather

		private void OnCollectiblePickup(Item item, BasePlayer player)
		{
			OnGather(player, item.info.shortname, item.amount);
		}

		private void OnCropGather(GrowableEntity plant, Item item, BasePlayer player)
		{
			OnGather(player, item.info.shortname, item.amount);
		}

		private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
		{
			OnGather(player, item.info.shortname, item.amount);
		}

		private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
		{
			OnGather(player, item.info.shortname, item.amount);
		}

		private void OnGather(BasePlayer player, string shortname, int amount)
		{
			if (player == null || shortname.IsNullOrEmpty() || amount <= 0) return;

			AddToStats(player.userID, shortname, amount);
		}

		#endregion

		#region Loot

		private readonly Dictionary<ulong, List<uint>> _loots = new Dictionary<ulong, List<uint>>();

		private void OnLootEntity(BasePlayer player, BaseEntity entity)
		{
			if (player == null || entity == null || entity.net == null || _loots.ContainsKey(player.userID) &&
				_loots[player.userID].Contains(entity.net.ID)) return;

			AddToStats(player.userID, entity.ShortPrefabName);

			if (_loots.ContainsKey(player.userID))
				_loots[player.userID].Add(entity.net.ID);
			else
				_loots.Add(player.userID, new List<uint> {entity.net.ID});
		}

		#endregion

		#region Entity Death

		private readonly Dictionary<uint, BasePlayer> _lastHeli = new Dictionary<uint, BasePlayer>();

		private void OnEntityTakeDamage(BaseHelicopter helicopter, HitInfo info)
		{
			if (helicopter != null && helicopter.net != null && info.InitiatorPlayer != null)
				_lastHeli[helicopter.net.ID] = info.InitiatorPlayer;
		}

		private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
		{
			if (entity == null || info == null) return;

			if (entity is BaseHelicopter)
			{
				if (_lastHeli.ContainsKey(entity.net.ID))
				{
					var basePlayer = _lastHeli[entity.net.ID];
					if (basePlayer != null)
						AddToStats(basePlayer.userID, "helicopter");
				}

				return;
			}

			var player = info.InitiatorPlayer;
			if (player == null) return;

			if (entity is BradleyAPC)
				AddToStats(player.userID, "bradley");
			else if (entity.name.Contains("barrel"))
				AddToStats(player.userID, "barrel");
			else if (_config.ScoreTable.ContainsKey(entity.ShortPrefabName))
				AddToStats(player.userID, entity.ShortPrefabName);
		}

		#endregion

		#endregion

		#endregion

		#region Commands

		private void CmdStats(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (_config.AccessByPerm && !permission.UserHasPermission(player.UserIDString, UsePermission))
			{
				SendReply(player, Msg(NoPermission, player.UserIDString));
				return;
			}

			if (_config.StatisticsCommands.Contains(command)) ProfileUi(player, player.userID, true);

			if (_config.LeaderboardCommands.Contains(command)) LeaderboardUi(player, first: true);
		}

		[ConsoleCommand("UI_Stats")]
		private void CmdConsoleStats(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null || !arg.HasArgs()) return;

			switch (arg.Args[0])
			{
				case "show":
				{
					ulong target;
					if (!arg.HasArgs(2) || !ulong.TryParse(arg.Args[1], out target)) return;

					ProfileUi(player, target);
					break;
				}
				case "leadboard":
				{
					var page = 0;
					if (arg.HasArgs(2))
						int.TryParse(arg.Args[1], out page);

					var mode = 0;
					if (arg.HasArgs(3))
						int.TryParse(arg.Args[2], out mode);

					LeaderboardUi(player, page, mode);
					break;
				}
			}
		}

		#endregion

		#region Interface

		private void ProfileUi(BasePlayer player, ulong targetId, bool first = false)
		{
			var container = new CuiElementContainer();

			var targetData = GetPlayerData(targetId);

			#region Background

			if (first)
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
						Close = Layer
					}
				}, Layer);
			}

			#endregion

			#region Main

			var hasResources = _config.Resources.Count > 0;

			var height = hasResources ? 180 : 92.5f;

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = $"-320 -{height}",
					OffsetMax = $"320 {height + 10}"
				},
				Image =
				{
					Color = HexToCuiColor("#0E0E10")
				}
			}, Layer, Layer + ".Main");

			#region Avatar

			container.Add(new CuiElement
			{
				Parent = Layer + ".Main",
				Components =
				{
					new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", $"avatar_{targetId}")},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "25 -180", OffsetMax = "180 -25"
					}
				}
			});

			#endregion

			#region Display Name

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "195 -40",
					OffsetMax = "300 -25"
				},
				Text =
				{
					Text = Msg(Username, player.UserIDString),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#818181")
				}
			}, Layer + ".Main");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "195 -65",
					OffsetMax = "350 -47.5"
				},
				Text =
				{
					Text = $"{targetData?.DisplayName}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".Main");

			#endregion

			#region Time

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "195 -95",
					OffsetMax = "420 -80"
				},
				Text =
				{
					Text = Msg(GameTimeOnServer, player.UserIDString),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#818181")
				}
			}, Layer + ".Main");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "195 -120",
					OffsetMax = "350 -102.5"
				},
				Text =
				{
					Text = $"{FormatTime(GetStatsValue(targetId, "time"))}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".Main");

			#endregion

			#region Line

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "327 -120",
					OffsetMax = "328 -25"
				},
				Image = {Color = HexToCuiColor("#161617")}
			}, Layer + ".Main");

			#endregion

			#region Scores

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "350 -40",
					OffsetMax = "480 -25"
				},
				Text =
				{
					Text = Msg(ScoreTitle, player.UserIDString),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#818181")
				}
			}, Layer + ".Main");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "350 -65",
					OffsetMax = "480 -47.5"
				},
				Text =
				{
					Text = $"{GetStatsValue(targetId, "total")}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".Main");

			#endregion

			#region Favorite Weapon

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "350 -95",
					OffsetMax = "480 -80"
				},
				Text =
				{
					Text = Msg(FavoriteWeapon, player.UserIDString),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#818181")
				}
			}, Layer + ".Main");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "350 -120",
					OffsetMax = "480 -102.5"
				},
				Text =
				{
					Text = $"{GetFavoriteWeapon(targetId)}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".Main");

			#endregion

			#region Line

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "490 -120",
					OffsetMax = "491 -25"
				},
				Image = {Color = HexToCuiColor("#161617")}
			}, Layer + ".Main");

			#endregion

			#region Top

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "505 -55",
					OffsetMax = "615 -40"
				},
				Text =
				{
					Text = Msg(Top, player.UserIDString),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#818181")
				}
			}, Layer + ".Main");

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "505 -90",
					OffsetMax = "615 -62.5"
				},
				Image =
				{
					Color = HexToCuiColor("#161617")
				}
			}, Layer + ".Main", Layer + ".Top");

			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = $"{GetTop(targetId)}",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".Top");

			#endregion

			#region Kills

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "195 -150",
					OffsetMax = "280 -135"
				},
				Text =
				{
					Text = Msg(KillsTitle, player.UserIDString),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#818181")
				}
			}, Layer + ".Main");

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "195 -180",
					OffsetMax = "280 -157.5"
				},
				Image =
				{
					Color = HexToCuiColor("#161617")
				}
			}, Layer + ".Main", Layer + ".Kills");

			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = $"{GetStatsValue(targetId, "kills")}",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".Kills");

			#endregion

			#region Deaths

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "290 -150",
					OffsetMax = "375 -135"
				},
				Text =
				{
					Text = Msg(DeathsTitle, player.UserIDString),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#818181")
				}
			}, Layer + ".Main");

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "290 -180",
					OffsetMax = "375 -157.5"
				},
				Image =
				{
					Color = HexToCuiColor("#161617")
				}
			}, Layer + ".Main", Layer + ".Deaths");

			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = $"{GetStatsValue(targetId, "deaths")}",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".Deaths");

			#endregion

			#region Line

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "384.5 -180",
					OffsetMax = "385.5 -157.5"
				},
				Image = {Color = HexToCuiColor("#161617")}
			}, Layer + ".Main");

			#endregion

			#region Back

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "395 -180",
					OffsetMax = "502.5 -157.5"
				},
				Text =
				{
					Text = Msg(Back, player.UserIDString),
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#FFFFFF"),
					Align = TextAnchor.MiddleCenter
				},
				Button =
				{
					Command = "leaderboard",
					Color = HexToCuiColor("#4B68FF", 33)
				}
			}, Layer + ".Main");

			#endregion

			#region Close

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "507.5 -180",
					OffsetMax = "615 -157.5"
				},
				Text =
				{
					Text = Msg(Close, player.UserIDString),
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#FFFFFF"),
					Align = TextAnchor.MiddleCenter
				},
				Button =
				{
					Color = HexToCuiColor("#4B68FF"),
					Close = Layer
				}
			}, Layer + ".Main");

			#endregion

			#region Line

			if (hasResources)
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "25 -206",
						OffsetMax = "-25 -205"
					},
					Image = {Color = HexToCuiColor("#161617")}
				}, Layer + ".Main");

			#endregion

			#region Resources

			if (hasResources)
			{
				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "25 -230",
						OffsetMax = "125 -215"
					},
					Text =
					{
						Text = Msg(Mining, player.UserIDString),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = HexToCuiColor("#818181")
					}
				}, Layer + ".Main");

				var Width = 95f;

				var Margin = (590f - _config.Resources.Count * Width) / (_config.Resources.Count - 1);

				var xSwitch = -(_config.Resources.Count * Width + (_config.Resources.Count - 1) * Margin) / 2f;

				foreach (var resource in _config.Resources)
				{
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{xSwitch} -350",
							OffsetMax = $"{xSwitch + Width} -235"
						},
						Image = {Color = HexToCuiColor("#161617")}
					}, Layer + ".Main", Layer + $".Resources.{resource}");

					container.Add(new CuiElement
					{
						Parent = Layer + $".Resources.{resource}",
						Components =
						{
							new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", $"{resource}")},
							new CuiRectTransformComponent
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = "-30 -65", OffsetMax = "30 -5"
							}
						}
					});

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = "0 -85",
							OffsetMax = "0 -70"
						},
						Text =
						{
							Text = $"{Msg(resource, player.UserIDString)}",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 10,
							Color = HexToCuiColor("#818181")
						}
					}, Layer + $".Resources.{resource}");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0",
							OffsetMax = "0 -85"
						},
						Text =
						{
							Text = lang.GetMessage(ResourcesAmount, this, player.UserIDString)
								.Replace("{0}",
									GetStatsValue(targetId, resource)
										.ToString(
											"F0")), // Msg(ResourcesAmount, player.UserIDString, GetStatsValue(targetId, resource)),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 14,
							Color = HexToCuiColor("#FFFFFF")
						}
					}, Layer + $".Resources.{resource}");

					xSwitch += Width + Margin;
				}
			}

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		private void LeaderboardUi(BasePlayer player, int page = 0, int mode = 0, bool first = false)
		{
			var ySwitch = -145f;
			var Margin = 12.5f;
			var amountOnPage = 6;

			var container = new CuiElementContainer();

			#region Background

			if (first)
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
						Close = Layer
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
					OffsetMin = "-317.5 -210",
					OffsetMax = "317.5 310"
				},
				Image =
				{
					Color = HexToCuiColor("#0E0E10")
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
				Image =
				{
					Color = HexToCuiColor("#161617")
				}
			}, Layer + ".Main", Layer + ".Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "30 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(Leaderboard, player.UserIDString),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-110 -37.5",
					OffsetMax = "-85 -12.5"
				},
				Text =
				{
					Text = "◀",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Color = HexToCuiColor("#4B68FF"),
					Command = page != 0 ? $"UI_Stats leadboard {page - 1} {mode}" : ""
				}
			}, Layer + ".Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-80 -37.5",
					OffsetMax = "-55 -12.5"
				},
				Text =
				{
					Text = "▶",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Color = HexToCuiColor("#4B68FF"),
					Command = _data.Players.Count > (page + 1) * amountOnPage
						? $"UI_Stats leadboard {page + 1} {mode}"
						: ""
				}
			}, Layer + ".Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-50 -37.5",
					OffsetMax = "-25 -12.5"
				},
				Text =
				{
					Text = "✕",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Close = Layer,
					Color = HexToCuiColor("#4B68FF")
				}
			}, Layer + ".Header");

			#endregion

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "10 -75",
					OffsetMax = "60 -55"
				},
				Text =
				{
					Text = "#",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#BFBFBF")
				}
			}, Layer + ".Main");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "75 -75",
					OffsetMax = "225 -55"
				},
				Text =
				{
					Text = Msg(Username, player.UserIDString),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#BFBFBF")
				}
			}, Layer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "225 -75",
					OffsetMax = "350 -55"
				},
				Text =
				{
					Text = Msg(KDAmount, player.UserIDString),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#BFBFBF")
				},
				Button =
				{
					Color = "0 0 0 0",
					Command = "UI_Stats leadboard 0 2"
				}
			}, Layer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "410 -75",
					OffsetMax = "510 -55"
				},
				Text =
				{
					Text = Msg(Mining, player.UserIDString),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#BFBFBF")
				},
				Button =
				{
					Color = "0 0 0 0",
					Command = "UI_Stats leadboard 0 1"
				}
			}, Layer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "550 -75",
					OffsetMax = "590 -55"
				},
				Text =
				{
					Text = Msg(ScoreTitle, player.UserIDString),
					Align = TextAnchor.MiddleRight,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#BFBFBF")
				},
				Button =
				{
					Color = "0 0 0 0",
					Command = "UI_Stats leadboard 0 0"
				}
			}, Layer + ".Main");

			#endregion

			#region My Profile

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "10 -130",
					OffsetMax = "-10 -80"
				},
				Image = {Color = "0 0 0 0"}
			}, Layer + ".Main", Layer + ".My.Profile");

			CreateOutLine(ref container, Layer + ".My.Profile", HexToCuiColor("#161617"), 1);

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "10 -40",
					OffsetMax = "40 -10"
				},
				Image = {Color = HexToCuiColor("#161617")}
			}, Layer + ".My.Profile", Layer + ".My.Profile.Top");

			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = $"{GetTop(player.userID)}",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 16,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".My.Profile.Top");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "0 1",
					OffsetMin = "65 0",
					OffsetMax = "225 0"
				},
				Text =
				{
					Text = $"{player.displayName}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".My.Profile");

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0.5", AnchorMax = "0 0.5",
					OffsetMin = "225 -15",
					OffsetMax = "275 15"
				},
				Image =
				{
					Color = HexToCuiColor("#161617")
				}
			}, Layer + ".My.Profile", Layer + ".My.Profile.Kills");

			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = $"{GetStatsValue(player.userID, "kills")}",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".My.Profile.Kills");

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0.5", AnchorMax = "0 0.5",
					OffsetMin = "280 -15",
					OffsetMax = "330 15"
				},
				Image =
				{
					Color = HexToCuiColor("#161617")
				}
			}, Layer + ".My.Profile", Layer + ".My.Profile.Deaths");

			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = $"{GetStatsValue(player.userID, "deaths")}",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".My.Profile.Deaths");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "0 1",
					OffsetMin = "375 0",
					OffsetMax = "515 0"
				},
				Text =
				{
					Text = lang.GetMessage(ResourcesAmount, this, player.UserIDString).Replace("{0}",
						GetStatsValue(player.userID, "resources")
							.ToString(
								"F0")), //Msg(ResourcesAmount, player.UserIDString, GetStatsValue(player.userID, "resources")),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".My.Profile");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "0 1",
					OffsetMin = "515 0",
					OffsetMax = "575 0"
				},
				Text =
				{
					Text = $"{GetStatsValue(player.userID, "total")}",
					Align = TextAnchor.MiddleRight,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".My.Profile");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "1 1"
				},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0",
					Command = $"UI_Stats show {player.userID}"
				}
			}, Layer + ".My.Profile");

			#endregion

			#region Line

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "10 -138",
					OffsetMax = "-10 -137"
				},
				Image = {Color = HexToCuiColor("#161617")}
			}, Layer + ".Main", Layer + ".My.Profile");

			#endregion

			#region Table

			var list = _data.Players
				.Where(x => !permission.UserHasPermission(x.Key.ToString(), HidePermission))
				.OrderByDescending(x =>
				{
					switch (mode)
					{
						case 2:
							return x.Value.KD;
						case 1:
							return x.Value.Resources;
						default:
							return x.Value.Score;
					}
				})
				.Skip(page * amountOnPage)
				.Take(amountOnPage)
				.ToList();

			for (var i = 0; i < list.Count; i++)
			{
				var check = list[i];

				var top = page * amountOnPage + i + 1;

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"10 {ySwitch - 50f}",
						OffsetMax = $"-10 {ySwitch}"
					},
					Image =
					{
						Color =
							top == 1 ? HexToCuiColor("#4B68FF") :
							top == 2 ? HexToCuiColor("#4B68FF", 53) :
							top == 3 ? HexToCuiColor("#4B68FF", 23) :
							"0 0 0 0"
					}
				}, Layer + ".Main", Layer + $".Profile.{i}");

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "10 -40",
						OffsetMax = "40 -10"
					},
					Image = {Color = HexToCuiColor("#161617")}
				}, Layer + $".Profile.{i}", Layer + $".Profile.{i}.Top");

				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = $"{top}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 16,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, Layer + $".Profile.{i}.Top");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = "65 0",
						OffsetMax = "225 0"
					},
					Text =
					{
						Text = $"{check.Value.DisplayName}",
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, Layer + $".Profile.{i}");

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "0 0.5",
						OffsetMin = "225 -15",
						OffsetMax = "275 15"
					},
					Image =
					{
						Color = HexToCuiColor("#161617")
					}
				}, Layer + $".Profile.{i}", Layer + $".Profile.{i}.Kills");

				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = $"{check.Value.Kills}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, Layer + $".Profile.{i}.Kills");

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "0 0.5",
						OffsetMin = "280 -15",
						OffsetMax = "330 15"
					},
					Image =
					{
						Color = HexToCuiColor("#161617")
					}
				}, Layer + $".Profile.{i}", Layer + $".Profile.{i}.Deaths");

				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = $"{check.Value.Deaths}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, Layer + $".Profile.{i}.Deaths");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = "375 0",
						OffsetMax = "515 0"
					},
					Text =
					{
						Text = lang.GetMessage(ResourcesAmount, this, player.UserIDString)
							.Replace("{0}",
								check.Value.Resources
									.ToString(
										"F0")), //Msg(ResourcesAmount, player.UserIDString, check.Value.Resources),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, Layer + $".Profile.{i}");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = "515 0",
						OffsetMax = "575 0"
					},
					Text =
					{
						Text = $"{check.Value.Score}",
						Align = TextAnchor.MiddleRight,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, Layer + $".Profile.{i}");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 0",
						AnchorMax = "1 1"
					},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command = $"UI_Stats show {check.Key}"
					}
				}, Layer + $".Profile.{i}");

				if (top > 3 && i != list.Count - 1)
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"10 {ySwitch - 50f - Margin / 2f - 0.5f}",
							OffsetMax = $"-10 {ySwitch - 50f - Margin / 2f + 0.5f}"
						},
						Image = {Color = HexToCuiColor("#161617")}
					}, Layer + ".Main");

				ySwitch = ySwitch - Margin - 50f;
			}

			#endregion

			#endregion


			CuiHelper.DestroyUi(player, Layer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region Utils

		#region Avatar

		private readonly Regex Regex = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");

		private void GetAvatar(ulong userId, Action<string> callback)
		{
			if (callback == null) return;

			webrequest.Enqueue($"http://steamcommunity.com/profiles/{userId}?xml=1", null, (code, response) =>
			{
				if (code != 200 || response == null)
					return;

				var avatar = Regex.Match(response).Groups[1].ToString();
				if (string.IsNullOrEmpty(avatar))
					return;

				callback.Invoke(avatar);
			}, this);
		}

		#endregion

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

		private static bool IsTeammates(ulong player, ulong friend)
		{
			return player == friend ||
			       RelationshipManager.ServerInstance.FindPlayersTeam(player)?.members?.Contains(friend) == true;
		}

		private static void CreateOutLine(ref CuiElementContainer container, string parent, string color, int size = 2)
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

		private static string FormatTime(float seconds)
		{
			var time = TimeSpan.FromSeconds(seconds);

			var result =
				$"{(time.Duration().Days > 0 ? $"{time.Days:0} Day{(time.Days == 1 ? string.Empty : "s")} " : string.Empty)}{(time.Duration().Hours > 0 ? $"{time.Hours:0} Hour{(time.Hours == 1 ? string.Empty : "s")} " : string.Empty)}{(time.Duration().Minutes > 0 ? $"{time.Minutes:0} Min " : string.Empty)}{(time.Duration().Seconds > 0 ? $"{time.Seconds:0} Sec" : string.Empty)}";

			if (result.EndsWith(", ")) result = result.Substring(0, result.Length - 2);

			if (string.IsNullOrEmpty(result)) result = "0 Seconds";

			return result;
		}

		private void TimeHandle()
		{
			foreach (var player in BasePlayer.activePlayerList) AddToStats(player.userID, "time");
		}

		private void LoadImages()
		{
			if (_config.Resources.Count > 0)
				timer.In(5f, () =>
				{
					if (!ImageLibrary)
					{
						PrintWarning("IMAGE LIBRARY IS NOT INSTALLED");
					}
					else
					{
						var itemIcons = new List<KeyValuePair<string, ulong>>();

						_config.Resources.ForEach(item => itemIcons.Add(new KeyValuePair<string, ulong>(item, 0)));

						if (itemIcons.Count > 0)
							ImageLibrary?.Call("LoadImageList", Title, itemIcons, null);
					}
				});
		}

		#endregion

		#region PlayerDatabase

		private void UpdateStats(BasePlayer player)
		{
			if (player == null) return;

			var data = GetPlayerData(player);
			if (data == null) return;

			var stats = JsonConvert.SerializeObject(data.Stats);
			if (stats == null) return;

			PlayerDatabase?.Call("SetPlayerData", player.UserIDString, _config.PlayerDatabase.Field, stats);
		}

		private void LoadStats(BasePlayer player)
		{
			if (player == null) return;

			var data = GetPlayerData(player);
			if (data == null) return;

			var success =
				PlayerDatabase?.Call<string>("GetPlayerDataRaw", player.UserIDString, _config.PlayerDatabase.Field);
			if (string.IsNullOrEmpty(success)) return;

			var stats = JsonConvert.DeserializeObject<Dictionary<string, float>>(success);
			if (stats == null) return;

			data.Stats = stats;
		}

		#endregion

		#region Lang

		private const string
			Back = "Back",
			Close = "Close",
			Mining = "Mining",
			ResourcesAmount = "ResourcesAmount",
			DeathsTitle = "Deaths",
			KillsTitle = "Kills",
			Top = "Top",
			FavoriteWeapon = "FavoriteWeapon",
			ScoreTitle = "Score",
			GameTimeOnServer = "GameTimeOnServer",
			Username = "Username",
			KDAmount = "KDAmount",
			Leaderboard = "Leaderboard",
			NoPermission = "NoPermission";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["stones"] = "Stones",
				["sulfur.ore"] = "Sulfur",
				["metal.ore"] = "Metal",
				["hq.metal.ore"] = "HQM",
				["wood"] = "Wood",
				[Back] = "Back",
				[Close] = "Close",
				[Mining] = "Mining",
				[ResourcesAmount] = "{0} pcs",
				[DeathsTitle] = "Deaths",
				[KillsTitle] = "Kills",
				[Top] = "Top",
				[FavoriteWeapon] = "Favorite weapon",
				[ScoreTitle] = "Score",
				[GameTimeOnServer] = "Game time on server",
				[Username] = "Username",
				[KDAmount] = "Amount of kills / deaths",
				[Leaderboard] = "Leaderboard",
				[NoPermission] = "You don't have permission to use this command!"
			}, this);
		}

		private string Msg(string key, string userid = null, params object[] obj)
		{
			return string.Format(lang.GetMessage(key, this, userid), obj);
		}

		#endregion
	}
}