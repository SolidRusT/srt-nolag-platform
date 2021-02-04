using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;

namespace Oxide.Plugins {

	[Info("Recycle", "5Dev24", "3.0.4")]
	[Description("Recycle items into their resources")]
	public class Recycle : RustPlugin {

		private const string RecyclePrefab = "assets/bundled/prefabs/static/recycler_static.prefab",
			BackpackPrefab = "assets/prefabs/misc/item drop/item_drop_backpack.prefab",
			AdminPermission = "recycle.admin", RecyclerPermission = "recycle.use",
			CooldownBypassPermission = "recycle.bypass";
		private Dictionary<uint, EntityAndPlayer> Recyclers = new Dictionary<uint, EntityAndPlayer>();
		private Dictionary<uint, EntityAndPlayer> DroppedBags = new Dictionary<uint, EntityAndPlayer>();
		private Dictionary<ulong, long> Cooldowns = new Dictionary<ulong, long>();
		public ConfigData Data;

		#region Hooks

		private void Init() => this.Unsubscribe(nameof(CanNetworkTo));

		private void Loaded() {
			this.LoadMessages();
			this.ValidateConfig();
			this.Data = Config.ReadObject<ConfigData>();

			this.AddCovalenceCommand(this.Data?.Settings?.RecycleCommand ?? "recycle", "RecycleCommand", null);

			permission.RegisterPermission(Recycle.AdminPermission, this);
			permission.RegisterPermission(Recycle.RecyclerPermission, this);
			permission.RegisterPermission(Recycle.CooldownBypassPermission, this);
		}

		private void Unload() {
			this.DestroyRecyclers();
			this.DestroyBags();
		}

		private void OnLootEntityEnd(BasePlayer p, BaseEntity e) {
			if (this.IsRecycleBox(e)) this.DestroyRecycler(e);
		}

		private void OnPlayerDisconnected(BasePlayer p, string reason) {
			BaseEntity result = this.RecyclerFromPlayer(p.userID);
			if (result != null) this.DestroyRecycler(result);

			EntityAndPlayer[] eaps = this.DroppedBags.Values.Where(e => e.Player.userID == p.userID).ToArray();
			foreach (EntityAndPlayer eap in eaps)
				eap.Entity.Kill();
		}

		private object CanMoveItem(Item item, PlayerInventory pLoot, uint targetCon, int targetSlot, int amount) {
			if (this.Data.Settings.ToInventory && targetSlot >= 6)
				foreach (ItemContainer con in pLoot.loot.containers.Where(c => c.uid == targetCon && c.entityOwner != null).ToArray()) {
					Recycler r = con.entityOwner as Recycler;
					if (this.IsRecycleBox(r)) return false;
				}
			return null;
		}

		private object CanAcceptItem(ItemContainer con, Item i, int target) {
			if (con.entityOwner is Recycler) {
				Recycler r = con.entityOwner as Recycler;
				if (this.IsRecycleBox(r)) {
					BasePlayer p = this.PlayerFromRecycler(r.net.ID);
					if (p == null) return null;

					if (target < 6) {
						if (!this.Data.Settings.RecyclableTypes.Contains(Enum.GetName(typeof(ItemCategory), i.info.category)) ||
							this.Data.Settings.Blacklist.Contains(i.info.shortname)) {
							if (p != null) this.PrintToChat(p, this.GetMessage("Recycle", "Invalid", p));
							return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
						} else
							NextFrame(() => {
								if (r == null || !r.HasRecyclable()) return;

								if (this.Data.Settings.InstantRecycling) {
									if (!r.IsOn()) {
										r.InvokeRepeating(new Action(r.RecycleThink), 0, 0);
										r.SetFlag(BaseEntity.Flags.On, true, false, true);
										r.SendNetworkUpdateImmediate();
									}
								} else r.StartRecycling();
							});
					} else if (this.Data.Settings.ToInventory)
						NextFrame(() => {
							if (p == null || p.inventory == null || p.inventory.containerMain == null ||
								p.inventory.containerBelt == null || i == null) return;

							bool flag = false;
							if (!p.inventory.containerMain.IsFull())
								flag = i.MoveToContainer(p.inventory.containerMain);
							if (!flag && !p.inventory.containerBelt.IsFull())
								i.MoveToContainer(p.inventory.containerBelt);
						});
				}
			}
			return null;
		}

		private object OnRecycleItem(Recycler r, Item i) {
			if (this.IsRecycleBox(r)) {
				BasePlayer p = this.PlayerFromRecycler(r.net.ID);
				if (p != null && r.transform.position != p.transform.position) {
					Vector3 old = r.transform.position;
					r.transform.position = p.transform.position;
					r.SendNetworkUpdateImmediate();
					this.NextFrame(() => {
						if (r == null) return;
						r.transform.position = old;
						r.SendNetworkUpdateImmediate();
					});
				}
			}

			return null;
		}

		private object CanLootEntity(BasePlayer p, DroppedItemContainer con) {
			if (this.DroppedBags.ContainsKey(con.net.ID) && this.DroppedBags[con.net.ID].Player.userID != p.userID) return false;
			return null;
		}

		private object CanNetworkTo(BaseNetworkable e, BasePlayer p) {
			if (e == null || p == null || p == e || p.IsAdmin) return null;

			if (this.IsRecycleBox(e))
				return (this.PlayerFromRecycler(e.net.ID)?.userID ?? 0) == p.userID;
			return null;
		}

		private void OnEntityKill(BaseNetworkable e) {
			if (e is DroppedItemContainer && this.DroppedBags.ContainsKey(e.net.ID))
				this.DroppedBags.Remove(e.net.ID);
		}

		private void LoadMessages() {
			Func<string, string> youCannot = (thing) => "You cannot recycle while " + thing;

			lang.RegisterMessages(new Dictionary<string, string> {
				{ "Recycle -> DestroyedAllBags", "All bags have been destroyed" },
				{ "Recycle -> DestroyedAll", "All recyclers have been destroyed" },
				{ "Recycle -> Dropped", "You left some items in the recycler!" },
				{ "Recycle -> Invalid", "You cannot recycle that!" },
				{ "Denied -> Permission", "You don't have permission to use that command" },
				{ "Denied -> Privilege", "You cannot recycle within someone's building privilege" },
				{ "Denied -> Swimming", youCannot("swimming") },
				{ "Denied -> Falling", youCannot("falling") },
				{ "Denied -> Mounted", youCannot("mounted") },
				{ "Denied -> Wounded", youCannot("wounded") },
				{ "Denied -> Irradiation", youCannot("irradiated") },
				{ "Denied -> Ship", youCannot("on a ship") },
				{ "Denied -> Elevator", youCannot("on an elevator") },
				{ "Denied -> Balloon", youCannot("on a balloon") },
				{ "Denied -> Safe Zone", youCannot("in a safe zone") },
				{ "Denied -> Hook Denied", "You can't recycle right now" },
				{ "Cooldown -> In", "You need to wait {0} before recycling" },
				{ "Timings -> second", "second" },
				{ "Timings -> seconds", "seconds" },
				{ "Timings -> minute", "minute" },
				{ "Timings -> minutes", "minutes" }
			}, this);
		}

		#endregion

		#region HumanNPC Support

		private void OnUseNPC(BasePlayer npc, BasePlayer p) {
			if (this.Data?.Settings?.NPCIds != null && !this.Data.Settings.NPCIds.Contains(npc.UserIDString)) return;
			this.OpenRecycler(p);
		}

		#endregion

		#region Commands

		private void RecycleCommand(IPlayer iP, string cmd, string[] args) {
			BasePlayer p = iP.Object as BasePlayer;
			if (p == null) return;
			if (!this.Data.Settings.NPCOnly && this.CanPlayerOpenRecycler(p)) {
				this.OpenRecycler(p);
				if (this.Data.Settings.Cooldown > 0) {
					if (this.Cooldowns.ContainsKey(p.userID))
						this.Cooldowns[p.userID] = DateTimeOffset.Now.ToUnixTimeSeconds();
					else
						this.Cooldowns.Add(p.userID, DateTimeOffset.Now.ToUnixTimeSeconds());
				}
			}
		}

		[ChatCommand("purgerecyclers")]
		private void PurgeRecyclersChatCommand(BasePlayer p, string cmd, string[] args) {
			if (this.CanManageRecyclers(p.userID)) {
				this.DestroyRecyclers();
				this.PrintToChat(p, this.GetMessage("Recycle", "DestroyedAll", p));
			} else this.PrintToChat(p, this.GetMessage("Denied", "Perrmission", p));
		}

		[ChatCommand("purgebags")]
		private void PurgeBagsChatCommand(BasePlayer p, string cmd, string[] args) {
			if (this.CanManageRecyclers(p.userID)) {
				this.DestroyBags();
				this.PrintToChat(p, this.GetMessage("Recycle", "DestroyedAllBags", p));
			} else this.PrintToChat(p, this.GetMessage("Denied", "Permission", p));
		}

		#endregion

		#region Structs

		public struct EntityAndPlayer {
			public BaseEntity Entity;
			public BasePlayer Player;
		}

		public class ConfigData {
			public class SettingsWrapper {
				[JsonProperty("Command To Open Recycler")]
				public string RecycleCommand = "recycle";
				[JsonProperty("Cooldown (in minutes)")]
				public float Cooldown = 5.0f;
				[JsonProperty("Maximum Radiation")]
				public float RadiationMax = 1f;
				[JsonProperty("Refund Ratio")]
				public float RefundRatio = 0.5f;
				[JsonProperty("NPCs Only")]
				public bool NPCOnly = false;
				[JsonProperty("Allowed In Safe Zones")]
				public bool AllowedInSafeZones = true;
				[JsonProperty("Instant Recycling")]
				public bool InstantRecycling = false;
				[JsonProperty("Send Recycled Items To Inventory")]
				public bool ToInventory = false;
				[JsonProperty("Send Items To Inventory Before Bag")]
				public bool InventoryBeforeBag = false;
				[JsonProperty("NPC Ids")]
				public List<object> NPCIds = new List<object>();
				[JsonProperty("Recyclable Types")]
				public List<object> RecyclableTypes = new List<object>();
				[JsonProperty("Blacklisted Items")]
				public List<object> Blacklist = new List<object>();
			}
			public SettingsWrapper Settings = new SettingsWrapper();
			public string VERSION = "3.0.4";
		}

		#endregion

		#region Configuration

		protected override void LoadDefaultConfig() {
			ConfigData tmp = new ConfigData();
			tmp.Settings.RecyclableTypes = new List<object>() {
				"Ammunition", "Attire", "Common", "Component", "Construction", "Electrical",
				"Fun", "Items", "Medical", "Misc", "Tool", "Traps", "Weapon" };
			Config.WriteObject(tmp, true);
			this.Data = tmp;
		}

		private T GetSetting<T>(string val, T defaultVal) {
			if (val == null) return default(T);
			object gotten = Config.Get("Settings", val);
			if (gotten == null) {
				Config.Set("Settings", val, defaultVal);
				return defaultVal;
			}
			return this.ConvertType(gotten, defaultVal);
		}

		private T ConvertType<T>(object val, T defaultVal) {
			if (val == null) return defaultVal;
			return (T) Convert.ChangeType(val, typeof(T));
		}

		private void ValidateConfig() {
			this.LoadConfig();
			try {
				object version = Config.Get("VERSION");
				if (version == null) this.LoadDefaultConfig();
				else if (version.Equals("2.1.10")) {
					this.Data = new ConfigData {
						Settings = new ConfigData.SettingsWrapper {
							Cooldown = this.GetSetting("cooldownMinutes", 5f),
							RefundRatio = this.GetSetting("refundRatio", 0.5f),
							RadiationMax = this.GetSetting("radiationMax", 1f),
							NPCOnly = this.GetSetting("NPCOnly", false),
							NPCIds = this.GetSetting("NPCIDs", new List<object>()),
							RecyclableTypes = this.GetSetting("recyclableTypes", new List<object>() {
								"Ammunition", "Attire", "Common", "Component", "Construction", "Electrical",
								"Fun", "Items", "Medical", "Misc", "Tool", "Traps", "Weapon" }),
							Blacklist = this.GetSetting("blacklist", new List<object>()),
							AllowedInSafeZones = this.GetSetting("allowSafeZone", true)
						}
					};
					this.UpdateAndSave();
				} else if (version.Equals("3.0.0") || version.Equals("3.0.1") || version.Equals("3.0.3")) {
					/* All of these versions should handle updating fine due to
					 * the ConfigData object having defaults
					 */
					this.Data = Config.ReadObject<ConfigData>();
					if (this.Data == null || this.Data.Settings == null) this.LoadDefaultConfig();
					else this.UpdateAndSave();
				}
			} catch (NullReferenceException) {}
		}

		private void UpdateAndSave() {
			this.Data.VERSION = Version.ToString();
			Config.Clear();
			Config.WriteObject(this.Data, true);
			Config.Save();
		}

		#endregion

		#region Helpers

		private void CreateRecycler(BasePlayer p) {
			Recycler r = GameManager.server.CreateEntity(Recycle.RecyclePrefab, p.transform.position + Vector3.up * 4000) as Recycler;
			r.recycleEfficiency = this.Data.Settings.RefundRatio;
			r.enableSaving = false;
			r.SetFlag(BaseEntity.Flags.Locked, true);
			r.UpdateNetworkGroup();
			r.Spawn();
			if (!r.isSpawned) return;
			r.gameObject.layer = 0;
			r.SendNetworkUpdateImmediate(true);
			this.Subscribe(nameof(CanNetworkTo));
			this.OpenContainer(p, r);
			this.Recyclers.Add(r.net.ID, new EntityAndPlayer { Entity = r, Player = p });
		}

		private void OpenContainer(BasePlayer p, StorageContainer con) {
			timer.In(.2f, () => {
				p.EndLooting();
				if (!p.inventory.loot.StartLootingEntity(con, false)) return;
				p.inventory.loot.AddContainer(con.inventory);
				p.inventory.loot.SendImmediate();
				p.ClientRPCPlayer(null, p, "RPC_OpenLootPanel", con.panelName);
				p.SendNetworkUpdate();
			});
		}

		private void DropRecyclerContents(BaseEntity e) {
			if (e is Recycler && this.IsRecycleBox(e)) {
				Recycler r = e as Recycler;
				if ((r?.inventory?.itemList?.Count ?? 0) == 0) return;

				BasePlayer p = this.PlayerFromRecycler(r.net.ID);
				if (p == null) return;
				this.PrintToChat(p, this.GetMessage("Recycle", "Dropped", p));

				List<Item> items = r.inventory.itemList.ToList();

				if (this.Data.Settings.InventoryBeforeBag) {
					for (int i = 0; i < items.Count; i++) {
						Item item = items[i];

						bool flag = false;
						if (!p.inventory.containerMain.IsFull())
							flag = item.MoveToContainer(p.inventory.containerMain);
						if (!flag && !p.inventory.containerBelt.IsFull())
							item.MoveToContainer(p.inventory.containerBelt);
						
						if (flag) {
							items.RemoveAt(i);
							i--;
						}
					}
				}

				if (items.Count == 0) return;

				DroppedItemContainer bag = GameManager.server.CreateEntity(Recycle.BackpackPrefab, p.transform.position + Vector3.up, Quaternion.identity) as DroppedItemContainer;
				bag.enableSaving = false;
				bag.TakeFrom(r.inventory);
				bag.Spawn();
				bag.lootPanelName = "smallwoodbox";
				bag.playerSteamID = p.userID;
				this.DroppedBags.Add(bag.net.ID, new EntityAndPlayer { Entity = bag, Player = p });
			}
		}

		private void DestroyRecycler(BaseEntity e) {
			if (this.IsRecycleBox(e)) {
				this.DropRecyclerContents(e);
				this.Recyclers.Remove(e.net.ID);
				e.Kill();
			}

			if (this.Recyclers.Count == 0)
				this.Unsubscribe(nameof(CanNetworkTo));
		}

		private void DestroyRecyclers() {
			while (this.Recyclers.Count > 0)
				this.DestroyRecycler(this.Recyclers.FirstOrDefault().Value.Entity);

			this.Unsubscribe(nameof(CanNetworkTo));
			this.Recyclers.Clear();
		}

		private void DestroyBags() { 
			while (this.DroppedBags.Count > 0) {
				KeyValuePair<uint, EntityAndPlayer> ueap = this.DroppedBags.FirstOrDefault();
				this.DroppedBags.Remove(ueap.Value.Entity.net.ID);
				ueap.Value.Entity.Kill();
			}

			this.DroppedBags.Clear();
		}

		private string GetMessage(string top, string bottom, BasePlayer p) =>
			lang.GetMessage(top + " -> " + bottom, this, p?.UserIDString);

		private int[] GetCooldown(ulong uid) {
			long time;
			if (!this.Cooldowns.TryGetValue(uid, out time)) return new int[] { 0, 0 };
			time += (long) this.Data.Settings.Cooldown * 60;
			long now = DateTimeOffset.Now.ToUnixTimeSeconds();
			if (now > time)	return new int[] { 0, 0 };
			TimeSpan diff = TimeSpan.FromSeconds(time - DateTimeOffset.Now.ToUnixTimeSeconds());
			return new int[] { diff.Minutes, diff.Seconds };
		}

		private string CooldownTimesToString(int[] times, BasePlayer p) {
			if (times == null || times.Length != 2) return "";
			int mins = times[0], secs = times[1];
			return (
				string.Format(mins == 0 ? "" : ("{0} " + this.GetMessage("Timings", mins == 1 ? "minute" : "minutes", p)), mins) +
				string.Format(" {0} " + this.GetMessage("Timings", secs == 1 ? "second" : "seconds", p), secs)
			).Trim();
		}

		#endregion

		#region API

		public bool IsRecycler(uint netID) => this.Recyclers.ContainsKey(netID);

		public BasePlayer PlayerFromRecycler(uint netID) {
			if (!this.IsRecycler(netID)) return null;
			return this.Recyclers[netID].Player;
		}

		public BaseEntity RecyclerFromPlayer(ulong uid) {
			foreach (EntityAndPlayer eap in this.Recyclers.Values)
				if (eap.Player.userID == uid) return eap.Entity;
			return null;
		}

		public bool IsOnCooldown(ulong uid) => !this.CanBypassCooldown(uid) && this.Cooldowns.ContainsKey(uid) &&
			this.Cooldowns[uid] + (int) (this.Data?.Settings?.Cooldown ?? 5) * 60 > DateTimeOffset.Now.ToUnixTimeSeconds();

		public bool CanUseRecycler(ulong uid) =>
			this.permission.UserHasPermission(uid + "", Recycle.RecyclerPermission);

		public bool CanManageRecyclers(ulong uid) =>
			this.permission.UserHasPermission(uid + "", Recycle.AdminPermission);

		public bool CanBypassCooldown(ulong uid) =>
			this.permission.UserHasPermission(uid + "", Recycle.CooldownBypassPermission);

			#region Friendly API

			// Backwards compatability support
			public bool IsRecycleBox(BaseNetworkable e) {
				if (e == null || e.net == null) return false;
				return this.IsRecycler(e.net.ID);
			}

			public bool CanPlayerOpenRecycler(BasePlayer p) {
				if (p == null || p.IsDead()) this.PrintToChat(p, this.GetMessage("Denied", "Hook Denied", p));
				else if (!this.CanUseRecycler(p.userID) && !this.CanManageRecyclers(p.userID)) this.PrintToChat(p, this.GetMessage("Denied", "Permission", p));
				else if (this.Data.Settings.Cooldown > 0 && this.IsOnCooldown(p.userID)) this.PrintToChat(p, this.GetMessage("Cooldown", "In", p), this.CooldownTimesToString(this.GetCooldown(p.userID), p));
				else if (p.IsWounded()) this.PrintToChat(p, this.GetMessage("Denied", "Wounded", p));
				else if (!p.CanBuild()) this.PrintToChat(p, this.GetMessage("Denied", "Privilege", p));
				else if (this.Data.Settings.RadiationMax > 0 && p.radiationLevel > this.Data.Settings.RadiationMax) this.PrintToChat(p, this.GetMessage("Denied", "Irradiation", p));
				else if (p.IsSwimming()) this.PrintToChat(p, this.GetMessage("Denied", "Swimming", p));
				else if (!p.IsOnGround() || p.IsFlying || p.isInAir) this.PrintToChat(p, this.GetMessage("Denied", "Falling", p));
				else if (p.isMounted) this.PrintToChat(p, this.GetMessage("Denied", "Mounted", p));
				else if (p.GetComponentInParent<CargoShip>()) this.PrintToChat(p, this.GetMessage("Denied", "Ship", p));
				else if (p.GetComponentInParent<HotAirBalloon>()) this.PrintToChat(p, this.GetMessage("Denied", "Balloon", p));
				else if (p.GetComponentInParent<Lift>()) this.PrintToChat(p, this.GetMessage("Denied", "Elevator", p));
				else if (!this.Data.Settings.AllowedInSafeZones && p.InSafeZone()) this.PrintToChat(p, this.GetMessage("Denied", "Safe Zone", p));
				else {
					object ret = Interface.Call("CanOpenRecycler", p);
					if (ret != null && ret is bool && !((bool) ret)) this.PrintToChat(p, this.GetMessage("Denied", "Hook Denied", p));
					else return true;
				}
				return false;
			}

			public bool IsOnCooldown(BasePlayer p) => this.IsOnCooldown(p.userID);

			public void OpenRecycler(BasePlayer p) {
				if (p == null) return;
				BaseEntity result = this.RecyclerFromPlayer(p.userID);
				if (result == null) this.CreateRecycler(p);
				else {
					this.DestroyRecycler(result);
					this.CreateRecycler(p);
				}
			}

			public void AddNPC(string id) {
				if (this.Data != null && this.Data.Settings != null && this.Data.Settings.NPCIds != null) {
					this.Data.Settings.NPCIds.Add(id);
					Config.WriteObject(this.Data, true);
				}
			}

			public void RemoveNPC(string id) {
				if (this.Data != null && this.Data.Settings != null && this.Data.Settings.NPCIds != null)
					if (this.Data.Settings.NPCIds.Remove(id))
						Config.WriteObject(this.Data, true);
			}

			#endregion

		#endregion

	}

}
