using UnityEngine;
using System.Linq;

namespace Oxide.Plugins {

	[Info("Submarine Storage", "yetzt", "0.0.4")]
	[Description("Adds Storage Boxes to Submarines")]

	public class SubmarineStorage : RustPlugin {

		private string prefab = "assets/content/vehicles/boats/rhib/subents/rhib_storage.prefab";

		#region Oxide

		private void OnEntitySpawned(BaseSubmarine entity) {
			if (entity == null) return;

			// defer checking to ensure storage box is loaded (loads after parent, race condition there)
			timer.Once(0.1f, () => {
				if (entity == null) return;

				// check if there is already a box
				foreach (var child in entity.GetComponentsInChildren<StorageContainer>(true)) {
					if (child.name == prefab) return;
				}
						
				// adding box
				var box = GameManager.server?.CreateEntity(prefab, entity.transform.position) as StorageContainer;
				if (box == null) return;
				box.Spawn();
				box.SetParent(entity);
				if (entity.ShortPrefabName == "submarineduo.entity") {
					box.transform.localPosition = new Vector3(0f, 0.2f, -0.3f);
					box.transform.Rotate(new Vector3(0f, 90f, 0.0f));
				} else {
					box.transform.localPosition = new Vector3(0f, 0.4f, -0.6f);
					box.transform.Rotate(new Vector3(0.0f, 0.0f, 0.0f));
				}
				box.SendNetworkUpdateImmediate(true);

			});

		}

		// drop items when entity dies
		private void OnEntityDeath(BaseSubmarine entity, HitInfo info) {
			if (entity == null) return;
			foreach (var child in entity.GetComponentsInChildren<StorageContainer>(true)) {
				if (child.name == prefab) {
					child.DropItems();
				}
			}
		}
		
		// drop items when entity is killed (don't want salty tears)
		private void OnEntityKill(BaseSubmarine entity) {
			if (entity == null) return;
			foreach (var child in entity.GetComponentsInChildren<StorageContainer>(true)) {
				if (child.name == prefab) {
					child.DropItems();
				}
			}
		}
		
		#endregion
	}
}

