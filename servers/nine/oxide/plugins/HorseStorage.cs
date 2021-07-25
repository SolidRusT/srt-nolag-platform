using UnityEngine;
using Oxide.Core.Configuration; 

namespace Oxide.Plugins
{
    [Info("Horse Storage", "Bazz3l", "1.0.2")]
    [Description("Gives horses the ability to carry items")]
    public class HorseStorage : RustPlugin
    {
        #region Config
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                EnableStorage = true
            };
        }

        private class PluginConfig
        {
            public bool EnableStorage;
        }
        #endregion

        #region Oxide
        private void Init() => config = Config.ReadObject<PluginConfig>();

        private void OnEntitySpawned(RidableHorse entity)
        {
            if (entity == null || !config.EnableStorage)
            {
                return;
            }

            NextTick(() => {
                foreach (StorageContainer child in entity.GetComponentsInChildren<StorageContainer>(true)) {
                    if (child.name == "assets/prefabs/deployable/small stash/small_stash_deployed.prefab") 
                    {
                        return;
                    }
                }

                entity.gameObject.AddComponent<AddStorageBox>();
            });
        }

        private void OnEntityDeath(RidableHorse entity, HitInfo info)
        {
            if (entity == null || !config.EnableStorage)
            {
                return;
            }

            var box = entity.GetComponent<AddStorageBox>()?.box as StorageContainer;
            if (box != null)
            {
                box.DropItems();
            }
        }

        public class AddStorageBox : MonoBehaviour
        {
             public RidableHorse entity;
             public StorageContainer box;

             void Awake()
             {
                 entity = GetComponent<RidableHorse>();
                 if (entity == null)
                 {
                     Destroy(this);
                     return;
                 }
                 
                 box = GameManager.server.CreateEntity("assets/prefabs/deployable/small stash/small_stash_deployed.prefab", entity.transform.position) as StorageContainer;
                 if (box == null)
                 {
                     return;
                 }
                 
                 box.Spawn();
                 box.SetParent(entity);
                 box.transform.localPosition = new Vector3(0.4f, 1f, -0.4f);
                 box.transform.Rotate(new Vector3(90.0f, 90.0f, 0.0f));
                 box.SendNetworkUpdateImmediate(true);
             }
        }
        #endregion
    }
}
