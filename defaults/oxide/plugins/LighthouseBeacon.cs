using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Lighthouse Beacon", "S0N_0F_BISCUIT", "1.0.3")]
	[Description("Adds a beacon to the lighthouse monument")]
	class LighthouseBeacon : RustPlugin
	{
		#region Variables
		private List<SearchLight> beacons = new List<SearchLight>();

		class Beacon : MonoBehaviour
		{
			#region Variables
			private SearchLight light { get; set; } = null;
			#endregion

			#region Initialization
			/// <summary>
			/// Initialize beacon
			/// </summary>
			private void Awake()
			{
				light = GetComponent<SearchLight>();
				light.SetFlag(BaseEntity.Flags.On, true);
				light.SetFlag(BaseEntity.Flags.Busy, true);
				light.aimDir = Quaternion.AngleAxis(15, Vector3.right) * (light.aimDir + Vector3.forward);
				light.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
			}
			#endregion

			#region Functionality
			/// <summary>
			/// Rotate the beacon
			/// </summary>
			private void Update()
			{
				light.aimDir = Quaternion.Euler(0, -.25f, 0) * light.aimDir;
				light.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
			}
			/// <summary>
			/// Destroy the beacon with the searchlight
			/// </summary>
			public void OnDestroy()
			{
				Destroy(this);
			}
			#endregion
		}
		#endregion

		/// <summary>
		/// Create beacons when the plugin is initialized
		/// </summary>
		void OnServerInitialized()
		{
			// Delete leftover beacons
			foreach (var light in UnityEngine.Object.FindObjectsOfType<SearchLight>().Where(light => light.gameObject.HasComponent<Beacon>()))
            {
				light.Kill();
            }
			// Spawn at monuments
			foreach (MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>().Where(monument => monument.name.Contains("lighthouse")))
			{
				CreateBeacon(monument.transform.position + Vector3.up * 57);
			}
		}
		/// <summary>
		/// Destroy the beacons when the plugin is unloaded
		/// </summary>
		void Unload()
		{
			foreach (SearchLight beacon in beacons)
			{
				try
				{
					beacon.Kill();
				}
				catch { }
			}
		}
		/// <summary>
		/// Create a new lighthouse beacon
		/// </summary>
		/// <param name="position"></param>
		void CreateBeacon(Vector3 position)
		{
			var newLight = GameManager.server.CreateEntity("assets/prefabs/deployable/search light/searchlight.deployed.prefab", position, new Quaternion(), true);
			BaseEntity searchLight = newLight?.GetComponent<BaseEntity>();
			if (searchLight)
			{
				searchLight?.gameObject.AddComponent<Beacon>();
				searchLight?.Spawn();
				(searchLight as SearchLight).UpdateHasPower((searchLight as SearchLight).DesiredPower(), 0);
				(searchLight as SearchLight).SetFlag(BaseEntity.Flags.Reserved8, true);
				beacons.Add(searchLight as SearchLight);
			}
		}
	}
}