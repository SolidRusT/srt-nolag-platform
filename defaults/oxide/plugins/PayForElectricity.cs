using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Pay for Electricity", "mr01sam", "1.0.2")]
	[Description("Electric generators cost money (or scrap) to provide power")]
	public class PayForElectricity : CovalencePlugin
	{
		private static PayForElectricity PLUGIN;

		/* Permissions */
		private const string PermissionUse = "payforelectricity.use";

		/* Dependencies */
		[PluginReference]
		private readonly Plugin Economics;

		private readonly string prefab = "assets/prefabs/io/electric/switches/fusebox/fusebox.prefab";

		private const int PAY_PERIOD = 2;

		#region Oxide Hooks
		void Init()
		{
			PLUGIN = this;

			/* Unsubscribe */
			Unsubscribe(nameof(OnEntityKill));
			Unsubscribe(nameof(OnEntitySpawned));
			Unsubscribe(nameof(OnLootEntity));
			Unsubscribe(nameof(CanAcceptItem));
		}

		void OnServerInitialized( bool initial )
		{
			/* Register perms */
			permission.RegisterPermission(PermissionUse, this);

			/* Check if using Economics */
			if (config.UseEconomics && !Economics)
			{
				PrintWarning($"You do not have the Economics plugin installed, setting 'Use economics balance' to false");
				config.UseEconomics = false;
			}

			/* Reset UI */
			foreach (BasePlayer player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, "pfePanel");
			}

			/* Load existing generators */
			try
			{
				PaidGenerator.LoadAll(Interface.Oxide.DataFileSystem.ReadObject<Dictionary<uint, PaidGenerator>>($"{Name}/generators"));
			}
			catch
			{
				PrintWarning("Failed to load existing generator data");
			}

			/* Start collection timer */
			timer.Every(PAY_PERIOD, () =>
			{
				foreach (PaidGenerator gen in PaidGenerator.Generators.Values.ToArray())
				{
					gen.CollectPayment(PAY_PERIOD);
				}
			});

			/* Subscribe */
			Subscribe(nameof(OnEntityKill));
			Subscribe(nameof(OnEntitySpawned));
			Subscribe(nameof(OnLootEntity));
			Subscribe(nameof(CanAcceptItem));
		}

		void OnServerSave()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/generators", PaidGenerator.Generators);
		}

		void Unload()
		{
			PaidGenerator.Generators.Clear();
			PLUGIN = null;
		}

		void OnEntitySpawned( ElectricGenerator entity )
		{
			if (entity.IsValid())
			{
				BasePlayer owner = BasePlayer.FindByID(entity.OwnerID);
				if (owner.IsValid() && !permission.UserHasPermission(owner.UserIDString, PermissionUse))
					return;
				var box = GameManager.server?.CreateEntity(prefab, entity.transform.position) as ItemBasedFlowRestrictor;
				if (box == null) return;
				box.SetParent(entity);
				box.transform.localPosition = new Vector3(0.15f, 0.75f, 1.7f);
				box.transform.Rotate(new Vector3(-90.0f, 0.0f, 0.0f));
				box.SendNetworkUpdateImmediate(true);
				timer.In(0.1f, () =>
				{
					PaidGenerator gen = PaidGenerator.Create(entity.net.ID, box.net.ID);
				});
			}
		}

		void OnEntityKill( ItemBasedFlowRestrictor entity )
		{
			if (entity.IsValid())
			{
				PaidGenerator.Destroy(entity.net.ID);
				BaseEntity parent = entity.GetParentEntity();
				if (parent != null)
				{
					foreach (BaseEntity child in parent.children.ToArray())
					{
						child.SetParent(null);
					}
					parent.Kill();
				}
			}
		}

		void OnLootEntity( BasePlayer player, ItemBasedFlowRestrictor entity )
		{
			BaseEntity parent = entity.GetParentEntity();
			if (parent != null && parent.ShortPrefabName == "generator.small")
			{
				PaidGenerator gen = PaidGenerator.FindById(entity.net.ID);
				if (gen == null)
				{
					gen = PaidGenerator.Create(parent.net.ID, entity.net.ID);
				}
				ShowUI(player, gen);
				CheckIfLooting(player);
			}
		}

		ItemContainer.CanAcceptResult? CanAcceptItem( ItemContainer container, Item item, int targetPos )
		{
			BaseEntity parent = container.entityOwner;
			if (parent != null && parent.name == prefab)
			{
				BaseEntity gen = parent.GetParentEntity();
				if (gen != null)
					return ItemContainer.CanAcceptResult.CannotAccept;
			}
			return null;
		}
		#endregion

		#region Helpers
		private void CheckIfLooting( BasePlayer player )
		{
			timer.In(0.1f, () =>
			{
				if (!player.inventory.loot.IsLooting())
				{
					CuiHelper.DestroyUi(player, "pfePanel");
				}
				else
				{
					CheckIfLooting(player);
				}
			});
		}

		private string FormatTime( BasePlayer player, double hours )
		{
			if (double.IsInfinity(hours))
			{
				return Lang("no power", player.UserIDString);
			}
			else if (double.IsNaN(hours) || hours <= 0)
			{
				return "---";
			}
			else if (hours < 1)
			{
				return Lang("minutes", player.UserIDString, Math.Round(hours * 60, 1));
			}
			else
			{
				return Lang("hours", player.UserIDString, Math.Round(hours, 1));
			}
		}

		private void RemoveScrap( BasePlayer player, int amount )
		{
			List<Item> stacks = player.inventory.FindItemIDs(-932201673);
			foreach (Item stack in stacks)
			{
				int size = stack.amount;
				if (size >= amount)
				{
					stack.UseItem(amount);
					break;
				}
				else
				{
					stack.UseItem(amount);
					amount -= size;
				}
			}
		}

		private double GetBalance( BasePlayer player )
		{
			if (config.UseEconomics)
			{
				return (double)Economics.Call("Balance", player.userID);
			}
			else
			{
				return player.inventory.GetAmount(-932201673);
			}
		}

		private void SubBalance( BasePlayer player, double amount )
		{
			if (config.UseEconomics)
			{
				Economics.Call("Withdraw", player.userID, amount);
			}
			else
			{
				RemoveScrap(player, (int)amount);
			}
		}


		private void PlayEffect( BaseEntity entity, string effectString )
		{
			var effect = new Effect(effectString, entity, 0, new Vector3(0.1f, 1, 0.4f), Vector3.forward);
			EffectNetwork.Send(effect);
		}

		#endregion

		#region Classes
		private class PaidGenerator
		{

			public static readonly Dictionary<uint, PaidGenerator> Generators = new Dictionary<uint, PaidGenerator>();

			public uint boxId { get; set; }
			public uint generatorId { get; set; }
			public double powerOutput { get; set; }
			public double costPerHour { get; set; }
			public double balance { get; set; }

			public PaidGenerator( uint generatorId, uint boxId )
			{
				this.generatorId = generatorId;
				this.boxId = boxId;
				this.balance = 0;
				SetPowerOutput(0);
			}

			public void SetPowerOutput( double output )
			{
				output = Math.Min(PLUGIN.config.MaxPower, Math.Max(PLUGIN.config.MinPower, output));
				this.powerOutput = output;
				this.costPerHour = PLUGIN.config.PricePerWatt * output;
				ElectricGenerator obj = GetElectricGenerator(generatorId);
				if (obj != null)
				{
					if (balance == 0)
						obj.electricAmount = 0;
					else
						obj.electricAmount = (float)output;
					obj.currentEnergy = (int)obj.electricAmount;
					obj.UpdateOutputs();
				}
			}

			public ElectricGenerator GetElectricGenerator( uint entityId )
			{
				return (ElectricGenerator)BaseNetworkable.serverEntities.Find(entityId);
			}

			public double AddBalance( double amount )
			{
				this.balance += amount;
				return balance;
			}

			public void CollectPayment( int seconds )
			{
				double oldBalance = this.balance;
				this.balance = Math.Max(0, this.balance - (costPerHour * ((float)seconds / 3600.0)));
				if (balance <= 0 && oldBalance > 0)
				{
					PlayShutdownEffect();
				}
				SetPowerOutput(powerOutput);
			}

			public double GetHoursRemaining()
			{
				return balance / costPerHour;
			}

			public double CostOfHours( int hours )
			{
				return hours * costPerHour;
			}

			private void PlayShutdownEffect()
			{
				ElectricGenerator entity = GetElectricGenerator(generatorId);
				EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", entity, 0, new Vector3(0.1f, 0.7f, 0.4f), Vector3.forward));
			}

			public static void LoadAll( Dictionary<uint, PaidGenerator> existing )
			{
				foreach (uint key in existing.Keys)
				{
					Generators.Add(key, existing[key]);
				}
			}

			public static void Destroy( uint boxId )
			{
				if (Generators.ContainsKey(boxId))
					Generators.Remove(boxId);
			}

			public static PaidGenerator Create( uint generatorId, uint boxId )
			{
				PaidGenerator generator = new PaidGenerator(generatorId, boxId);
				Generators.Add(boxId, generator);
				return generator;
			}

			public static PaidGenerator FindById( uint boxId )
			{
				if (Generators.ContainsKey(boxId))
					return Generators[boxId];
				return null;
			}
		}
		#endregion

		#region Commands
		[Command("setpower")]
		private void cmd_setpower( IPlayer player, string command, string[] args )
		{
			uint boxId = uint.Parse(args[0]);
			double newPowerAmount = double.Parse(args[1]);
			PaidGenerator gen = PaidGenerator.FindById(boxId);
			if (gen != null)
			{
				BasePlayer basePlayer = BasePlayer.FindByID(ulong.Parse(player.Id));
				gen.SetPowerOutput(newPowerAmount);
				ShowUI(basePlayer, gen);
			}
		}

		[Command("addhours")]
		private void cmd_addhours( IPlayer player, string command, string[] args )
		{
			uint boxId = uint.Parse(args[0]);
			int hours = int.Parse(args[1]);
			PaidGenerator gen = PaidGenerator.FindById(boxId);
			if (gen != null)
			{
				BasePlayer basePlayer = BasePlayer.FindByID(ulong.Parse(player.Id));
				double balance = GetBalance(basePlayer);
				double cost = gen.CostOfHours(hours);
				if (balance >= cost)
				{
					SubBalance(basePlayer, cost);
					gen.AddBalance(cost);
					ShowUI(basePlayer, gen);
				}
			}
		}
		#endregion

		#region Config

		private Configuration config;
		private class Configuration
		{
			[JsonProperty(PropertyName = "Price per watt (hourly)")]
			public float PricePerWatt = 0.1f;

			[JsonProperty(PropertyName = "Min power")]
			public float MinPower = 10;

			[JsonProperty(PropertyName = "Max power")]
			public float MaxPower = 1000;

			[JsonProperty(PropertyName = "Power increments")]
			public float PowerIncrements = 10;

			[JsonProperty(PropertyName = "Use economics balance (requires plugin)")]
			public bool UseEconomics = false;
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

		#region UI
		void ShowUI( BasePlayer player, PaidGenerator generator )
		{
			CuiHelper.DestroyUi(player, "pfePanel");
			CuiElementContainer container = new CuiElementContainer();
			string backgroundColor = "0.45 0.45 0.45 0";
			string btnColor = "0.475 0.54 0.32 0.8";
			string btnColorFaded = "0.6 0.6 0.6 0.4";
			string textColor = "0.9 0.9 0.9 1";
			int fontSize = 11;
			int btnSize = 10;

			container.Add(new CuiElement
			{
				Name = "pfePanel",
				Parent = "Overlay",
				Components = {
					new CuiImageComponent {
						Color = "0 0 0 0"
					},
					new CuiRectTransformComponent {
						AnchorMin = "0.65 0.151",
						AnchorMax = "0.947 0.266"
					}
				}
			});
			container.Add(new CuiElement
			{
				Name = "pfeTitle",
				Parent = "pfePanel",
				Components = {
					new CuiImageComponent {
						Color = "0 0 0 0"
					},
					new CuiRectTransformComponent {
						AnchorMin = "0 0.75",
						AnchorMax = "1 1"
					}
				}
			});
			container.Add(new CuiElement
			{
				Name = "pfeBody",
				Parent = "pfePanel",
				Components = {
					new CuiImageComponent {
						Color = "0.35 0.35 0.35 0.9"
					},
					new CuiRectTransformComponent {
						AnchorMin = "0 0",
						AnchorMax = "1 0.6677"
					}
				}
			});
			container.Add(new CuiElement
			{
				Name = "pfeBack1",
				Parent = "pfeBody",
				Components = {
					new CuiImageComponent {
						Color = "0 0 0 0"
					},
					new CuiRectTransformComponent {
						AnchorMin = "0 0.02",
						AnchorMax = "0.424 0.985"
					}
				}
			});
			container.Add(new CuiElement
			{
				Name = "pfeBack2",
				Parent = "pfeBody",
				Components = {
					new CuiImageComponent {
						Color = backgroundColor
					},
					new CuiRectTransformComponent {
						AnchorMin = "0.572 0.02",
						AnchorMax = "0.995 0.985"
					}
				}
			});
			container.Add(new CuiElement
			{
				Name = "pfeLeft",
				Parent = "pfeBody",
				Components = {
					new CuiImageComponent {
						Color = "0 0 0 0"
					},
					new CuiRectTransformComponent {
						AnchorMin = "0.02 0",
						AnchorMax = "0.5 1"
					}
				}
			});
			container.Add(new CuiElement
			{
				Name = "pfeRight",
				Parent = "pfeBody",
				Components = {
					new CuiImageComponent {
						Color = "0 0 0 0"
					},
					new CuiRectTransformComponent {
						AnchorMin = "0.4 0",
						AnchorMax = "0.98 1"
					}
				}
			});
			#region Title
			container.Add(new CuiElement
			{
				Name = "pfeTitleText",
				Parent = "pfeTitle",
				Components = {
					new CuiTextComponent {
						Text = Lang("ELECTRIC GENERATOR", player.UserIDString),
						Align = TextAnchor.MiddleLeft
					},
					new CuiRectTransformComponent {
						AnchorMin="0.02 0",
						AnchorMax="1 1"
					}
				}
			});
			#endregion
			#region Left
			container.Add(new CuiElement
			{
				Name = "pfeCostText",
				Parent = "pfeLeft",
				Components = {
					new CuiTextComponent {
						Text = Lang("cost", player.UserIDString, Math.Round(generator.costPerHour, 1)),
						Align = TextAnchor.MiddleLeft,
						FontSize = fontSize,
						Color = textColor
					},
					new CuiRectTransformComponent {
						AnchorMin="0 0.6",
						AnchorMax="1 1.0"
					}
				}
			});
			container.Add(new CuiButton
			{
				Button = {
					Command = $"setpower {generator.boxId} {generator.powerOutput - config.PowerIncrements}",
					Color = "1 1 1 0.5"
				},
				Text = {
					Text = "<",
					Align = TextAnchor.MiddleCenter,
					FontSize = btnSize,
				},
				RectTransform = {
					AnchorMin = "0 0.1",
					AnchorMax = "0.07 0.5"
				}
			}, "pfeLeft", "pfeButtonSub");
			container.Add(new CuiButton
			{
				Button = {
					Command = $"setpower {generator.boxId} {generator.powerOutput + config.PowerIncrements}",
					Color = "1 1 1 0.5"
				},
				Text = {
					Text = ">",
					Align = TextAnchor.MiddleCenter,
					FontSize = btnSize
				},
				RectTransform = {
					AnchorMin = "0.25 0.1",
					AnchorMax = "0.32 0.5"
				}
			}, "pfeLeft", "pfeButtonAdd");
			container.Add(new CuiElement
			{
				Name = "pfeInputText",
				Parent = "pfeLeft",
				Components = {
					new CuiTextComponent {
						Text = $"{(int) generator.powerOutput}",
						Align = TextAnchor.MiddleCenter,
						Color = textColor
					},
					new CuiRectTransformComponent {
						AnchorMin="0.07 0.1",
						AnchorMax="0.25 0.5"
					}
				}
			});
			container.Add(new CuiElement
			{
				Name = "pfeInputLabel",
				Parent = "pfeLeft",
				Components = {
					new CuiTextComponent {
						Text = "power",
						Align = TextAnchor.MiddleLeft,
						FontSize = fontSize,
						Color = textColor
					},
					new CuiRectTransformComponent {
						AnchorMin="0.35 0.1",
						AnchorMax="1.0 0.5"
					}
				}
			});
			#endregion
			#region Right
			container.Add(new CuiElement
			{
				Name = "pfeTimeText",
				Parent = "pfeRight",
				Components = {
					new CuiTextComponent {
						Text = Lang("time left", player.UserIDString, FormatTime(player, generator.GetHoursRemaining())),
						Align = TextAnchor.MiddleLeft,
						FontSize = fontSize,
						Color = textColor
					},
					new CuiRectTransformComponent {
						AnchorMin="0 0.6",
						AnchorMax="1 1.0"
					}
				}
			});
			container.Add(new CuiButton
			{
				Button = {
					Command = $"addhours {generator.boxId} 1",
					Color = GetBalance(player) >= Math.Round(generator.costPerHour, 0) ? btnColor : btnColorFaded
				},
				Text = {
					Text = Lang("pay1", player.UserIDString, Math.Round(generator.costPerHour, 0)),
					Align = TextAnchor.MiddleCenter,
					Color = GetBalance(player) >= Math.Round(generator.costPerHour, 0) ? "1 1 1 1" : "1 1 1 0.3",
					FontSize = btnSize
				},
				RectTransform = {
					AnchorMin = "0 0.1",
					AnchorMax = "0.4 0.5"
				}
			}, "pfeRight", "pfeButtonPay1");
			container.Add(new CuiButton
			{
				Button = {
					Command = $"addhours {generator.boxId} 24",
					Color = GetBalance(player) >= Math.Round(generator.costPerHour*24, 0) ? btnColor : btnColorFaded
				},
				Text = {
					Text = Lang("pay24", player.UserIDString, Math.Round(generator.costPerHour*24, 0)),
					Color = GetBalance(player) >= Math.Round(generator.costPerHour*24, 0) ? "1 1 1 1" : "1 1 1 0.3",
					Align = TextAnchor.MiddleCenter,
					FontSize = btnSize
				},
				RectTransform = {
					AnchorMin = "0.45 0.1",
					AnchorMax = "0.85 0.5"
				}
			}, "pfeRight", "pfeButtonPay24");
			#endregion
			CuiHelper.AddUi(player, container);
		}
		#endregion

		#region Localization
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["days"] = "{0} days",
				["hours"] = "{0} hrs",
				["minutes"] = "{0} min",
				["power"] = "power",
				["no power"] = "NO POWER",
				["cost"] = "Cost: {0}/hr",
				["time left"] = "Time left: {0}",
				["pay1"] = "1 Hour ({0})",
				["pay24"] = "24 Hours ({0})",
				["title"] = "ELECTRIC GENERATOR"
			}, this);
		}

		private string Lang( string key, string id = null, params object[] args ) => string.Format(lang.GetMessage(key, this, id), args);

		#endregion
	}
}
