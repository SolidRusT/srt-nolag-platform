using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
	[Info("Compound Teleport", "DezLife", "1.0.9")]
	[Description("Teleport through the death screen to the NPC town and bandit camp")]
	class CompoundTeleport : RustPlugin
	{
		#region Variables
		private int Layer = ~(1 << 8 | 1 << 10 | 1 << 18 | 1 << 21 | 1 << 24 | 1 << 28 | 1 << 29);
		private Dictionary<string, Vector3> positions = new Dictionary<string, Vector3>();
		private Dictionary<BasePlayer, SleepingBag[]> bags = new Dictionary<BasePlayer, SleepingBag[]>();
		private Queue<SleepingBag> bagsPool = new Queue<SleepingBag>();
		List<Vector3> PositionsOutPost = new List<Vector3>();
		List<Vector3> PositionsBandit = new List<Vector3>();

		#endregion

		#region Config

		private static Configuration config = new Configuration();
		private class Configuration
		{
			[JsonProperty("Sleeping bag Cooldown")]
			public int cooldown;
			[JsonProperty("The opportunity respawn in a outpost")]
			public bool outPostRespawn;
			[JsonProperty("Name bag outpost")]
			public string bagNameOutPost;
			[JsonProperty("The opportunity respawn in a Bandit Town")]
			public bool banditRespawn;
			[JsonProperty("Name bag Bandit Town")]
			public string bagNameBandit;
			public static Configuration GetNewConfiguration()
			{
				return new Configuration
				{
					cooldown = 150,
					outPostRespawn = true,
					bagNameOutPost = "OUTPOST",
					banditRespawn = true,
					bagNameBandit = "BANDIT TOWN"

				};
			}
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				config = Config.ReadObject<Configuration>();
			}
			catch
			{
				PrintWarning("Error reading configuration 'oxide/config/{Name}' Check, please");
				Unsubscribe("OnPlayerRespawn");
				Unsubscribe("OnServerCommand");
				Unsubscribe("OnPlayerConnected");
				Unsubscribe("OnPlayerDisconnected");
			}
			NextTick(SaveConfig);
		}
		protected override void LoadDefaultConfig()
		{
			config = new Configuration();
			SaveConfig();
		}
		protected override void SaveConfig() => Config.WriteObject(config);


		#endregion

		#region OxideHooks
		void Unload()
		{
			foreach(SleepingBag[] bagsToRemove in bags.Values)
                for (int i = 0; i < bagsToRemove.Length; i++)
					bagsToRemove[i]?.Kill();
			foreach (SleepingBag bagsToRemove in bagsPool)
				bagsToRemove?.Kill();
		}	

		void OnPlayerRespawn(BasePlayer p, SleepingBag bag)
		{
			foreach(SleepingBag findBagPlayer in bags[p])
            {
				if(findBagPlayer.net.ID == bag.net.ID)
                {
					var pos = bag.niceName == config.bagNameBandit ? PositionsBandit : PositionsOutPost;
					bag.transform.position = pos.GetRandom();
				}
			}
		}

		object OnServerCommand(ConsoleSystem.Arg arg)
		{
			BasePlayer basePlayer = arg.Player();
			if (basePlayer == null)
				return null;
			uint netId = arg.GetUInt(0, 0);
			if (arg.cmd.Name.ToLower() == "respawn_sleepingbag_remove" && netId != 0)
			{				
				foreach(SleepingBag noRemoveBags in bags[basePlayer])    
					if (noRemoveBags.net.ID == netId)
						return false;       
			}
			return null;
		}
		private void OnServerInitialized()
		{
			if (ConVar.Server.level == "HapisIsland" && config.outPostRespawn)
            {
				SpawnPointGeneration(new Vector3(-211f, 105f, -432f), 40, PositionsOutPost, "carpark", "concrete_slabs", "road", "train_track", "pavement", "platform");
				positions.Add(config.bagNameOutPost, new Vector3(-211f, 105f, -432f));
			}
			else
            {
				foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
				{
					if (monument.name.ToLower() == "assets/bundled/prefabs/autospawn/monument/medium/compound.prefab" && config.outPostRespawn)
					{
						SpawnPointGeneration(monument.transform.position, 40, PositionsOutPost, "carpark", "concrete_slabs", "road", "train_track", "pavement", "platform");
						positions.Add(config.bagNameOutPost, monument.transform.position);
					}
					else if (monument.name.ToLower() == "assets/bundled/prefabs/autospawn/monument/medium/bandit_town.prefab" && config.banditRespawn)
					{
						SpawnPointGeneration(monument.transform.position, 75, PositionsBandit, "helipad", "walkway", "rope", "floating");
						positions.Add(config.bagNameBandit, monument.transform.position);
					}
				}
			}
				
			foreach (BasePlayer player in BasePlayer.activePlayerList)
				OnPlayerConnected(player);
		}
		private void OnPlayerConnected(BasePlayer d)
		{
			int count = positions.Count, idx = -1;

			if (!bags.ContainsKey(d))
				bags.Add(d, new SleepingBag[count]);

			foreach (var positionKvp in positions)
			{
				SleepingBag bag = FromPool(d);
				if (bag == null) continue;
				bag.niceName = positionKvp.Key;
				bag.transform.position = positionKvp.Value;

				bags[d][++idx] = bag;

				SleepingBag.sleepingBags.Add(bag);
			}
		}

		private void OnPlayerDisconnected(BasePlayer d)
		{
			if (!bags.ContainsKey(d))
				return;

			foreach (SleepingBag bag in bags[d])
			{
				SleepingBag.sleepingBags.Remove(bag);
				ResetToPool(bag);
			}
		}

		#endregion

		#region Metods generate spawn point

		public void SpawnPointGeneration(Vector3 pos, float radius, List<Vector3> targetPosList, params string[] coliderName)
		{
			for (int i = 0; i < 150; i++)
			{
				RaycastHit rayHit;
				Vector3 resultPositions = pos + (Random.insideUnitSphere * radius);
				resultPositions.y = pos.y + 100f;
				if (Physics.Raycast(resultPositions, Vector3.down, out rayHit, 100, Layer, QueryTriggerInteraction.Ignore))
				{
					if (rayHit.collider is TerrainCollider)
						targetPosList.Add(rayHit.point);
					else
					{
						if (coliderName != null)
							for (int a = 0; a < coliderName.Length; a++)
							{
								if (rayHit.collider.name.Contains(coliderName[a]))
									targetPosList.Add(rayHit.point);
							}
					}
				}
			}
		}

		#endregion

		#region Helpers
		private SleepingBag FromPool(BasePlayer d)
		{
			SleepingBag bag;

			if (bagsPool.Count > 0)
			{
				bag = bagsPool.Dequeue();
				bag.OwnerID = d.userID;

				return bag;
			}

			GameObject go = new GameObject();
			bag = go.AddComponent<SleepingBag>();

			bag.deployerUserID = d.userID;
			bag.net = Network.Net.sv.CreateNetworkable();
			bag.niceName = string.Empty;
			bag.secondsBetweenReuses = config.cooldown;
			bag.transform.position = Vector3.one;
			bag.RespawnType = ProtoBuf.RespawnInformation.SpawnOptions.RespawnType.Bed;
			bag.unlockTime = 0;

			return bag;
		}

		private void ResetToPool(SleepingBag bag)
		{
			bagsPool.Enqueue(bag);
		}
		#endregion
	}
}
