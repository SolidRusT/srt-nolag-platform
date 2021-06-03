using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Plugins;
using Rust;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins {
	[Info("Cargo Wetness Fix", "yetzt", "1.0.2")]
	[Description("Prevents players from getting wet on the Cargo Ship")]

	public class CargoWetnessFix : RustPlugin {

		private List<BasePlayer> onboard = new List<BasePlayer>();
		Timer checker;
		int numships = 0;
			
		void OnServerInitialized() {

			// get current amount of cargoships (sorry for the huge call, but runs only once)
			numships = GameObject.FindObjectsOfType<CargoShip>().Length;
			
			if (numships == 0) {
			
				// unsubscribe some hooks when no cargo is present
				Unsubscribe(nameof(OnRunPlayerMetabolism));
				Unsubscribe(nameof(OnEntityEnter));
				Unsubscribe(nameof(OnEntityLeave));
				Unsubscribe(nameof(OnPlayerDisconnected));
				Unsubscribe(nameof(OnPlayerSleepEnded));
			
			} else {
				
				// check all online players triggers for cargo
				foreach (BasePlayer player in BasePlayer.activePlayerList) {
					CheckPlayer(player);
				}

				// sometimes do sanity checks
				checker = timer.Every(60f, () => {
					RunChecker();
				});
				
			}

		}
		
		void Unload(){
			
			for (int i = onboard.Count - 1; i >= 0; i--) {
				Disembark(onboard[i]);
			}

			// destroy checking timer
			checker?.Destroy();
		}

		void OnEntitySpawned(CargoShip entity) {
			if (entity == null) return;

			numships++;
			
			if (numships == 1) {

				Subscribe(nameof(OnRunPlayerMetabolism));
				Subscribe(nameof(OnEntityEnter));
				Subscribe(nameof(OnEntityLeave));
				Subscribe(nameof(OnPlayerDisconnected));
				Subscribe(nameof(OnPlayerSleepEnded));
			
				// destroy old checker if present, run checker
				checker?.Destroy();
				checker = timer.Every(60f, () => {
					RunChecker();
				});
				
			}

		}
		
		private void OnEntityKill(CargoShip entity) {
			if (entity == null) return;
			
			numships--;
			
			// check if another cargoship is present
			// multiple cargoships might be possible
			if (numships == 0) {

				// unsubscribe to all the hooks when no cargo is present
				Unsubscribe(nameof(OnRunPlayerMetabolism));
				Unsubscribe(nameof(OnEntityEnter));
				Unsubscribe(nameof(OnEntityLeave));
				Unsubscribe(nameof(OnPlayerDisconnected));
				Unsubscribe(nameof(OnPlayerSleepEnded));
			
				// destroy checking timer
				checker?.Destroy();

				// JIC remove remaining players instantly
				for (int i = onboard.Count - 1; i >= 0; i--) {
					Disembark(onboard[i]);
				}

			}

		}
		
		private void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BasePlayer player, float delta) {
			if (!IsEmbarked(player)) return;
			// cap wetness at current wetness level
			// so player stays wet when they are wet, but you can't get wetter than they are
			player.metabolism.wetness.max = player.metabolism.wetness.value;
		}

		void OnEntityEnter(TriggerParent trigger, BasePlayer player) {
			if (trigger == null || player == null || player.userID < 76560000000000000L || trigger.GetComponentInParent<CargoShip>() == null) return;
			Embark(player);
		}

		void OnEntityLeave(TriggerParent trigger, BasePlayer player) {
			if (trigger == null || player == null || player.userID < 76560000000000000L || trigger.GetComponentInParent<CargoShip>() == null) return;
			Disembark(player);
		}
		
		void OnPlayerDisconnected(BasePlayer player, string reason) {
			Disembark(player);
		}
		
		void OnPlayerSleepEnded(BasePlayer player) {
			CheckPlayer(player);
		}
		
		void RunChecker() {
			for (int i = onboard.Count - 1; i >= 0; i--) {
				CheckPlayer(onboard[i]);
			}
		}
		
		void CheckPlayer(BasePlayer player) {
			if (player == null || player.triggers == null) return;
			foreach (TriggerBase trigger in player.triggers) {
				if (trigger != null && trigger.GetComponentInParent<CargoShip>() != null) {
					Embark(player);
					return;
				}
			}
			Disembark(player);
		}
		
		bool IsEmbarked(BasePlayer player) {
			return (player != null) ? onboard.Contains(player) : false;
		}
		
		void Embark(BasePlayer player) {
			if (IsEmbarked(player)) return;
			onboard.Add(player);
			player.metabolism.wetness.max = player.metabolism.wetness.value;
			player.metabolism.SendChangesToClient();
		}

		void Disembark(BasePlayer player) {
			if (!IsEmbarked(player)) return;
			onboard.Remove(player);
			player.metabolism.wetness.max = 1;
			player.metabolism.SendChangesToClient();
		}
		
	}
}