using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Monument Lifts", "Mevent", "1.0.3")]
	[Description("Adds lifts to monuments")]
	public class MonumentLifts : RustPlugin
	{
		#region Fields

		private readonly List<ModularCarGarage> _lifts = new List<ModularCarGarage>();

		#endregion

		#region Config

		private Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Prefab")]
			public string Prefab =
				"assets/prefabs/deployable/modular car lift/electrical.modularcarlift.deployed.prefab";

			[JsonProperty(PropertyName = "Spawn Setting (name - pos correction)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, Vector3> Monuments = new Dictionary<string, Vector3>
			{
				["gas_station_1"] = new Vector3(4.2f, 0f, -0.5f),
				["supermarket_1"] = new Vector3(0.2f, 0f, 17.5f)
			};
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

		#region Hooks

		private void OnServerInitialized()
		{
			TerrainMeta.Path.Monuments.ForEach(monument =>
			{
				if (monument == null) return;

				var correct = _config.Monuments.FirstOrDefault(x => monument.name.Contains(x.Key)).Value;
				if (correct == Vector3.zero) return;

				var transform = monument.transform;
				var rot = transform.rotation;
				var pos = transform.position + rot * correct;
				pos.y = TerrainMeta.HeightMap.GetHeight(pos);

				SpawnEntity(pos, rot);
			});
		}

		private void Unload()
		{
			_lifts
				.ToList()
				.ForEach(lift =>
				{
					if (lift == null || lift.IsDestroyed) return;

					lift.Kill();
				});
		}

		private void OnEntityTakeDamage(ModularCarGarage entity, HitInfo info)
		{
			if (entity == null || info == null || !_lifts.Contains(entity)) return;
			info.damageTypes.ScaleAll(0);
		}

		#endregion

		#region Utils

		private void SpawnEntity(Vector3 pos, Quaternion rot)
		{
			var entity = GameManager.server.CreateEntity(_config.Prefab, pos, rot) as ModularCarGarage;
			if (entity == null) return;

			entity.enableSaving = false;

			entity.Spawn();
			entity.needsElectricity = false;
			entity.SetFlag(BaseEntity.Flags.On, true);

			_lifts.Add(entity);
		}

		#endregion
	}
}