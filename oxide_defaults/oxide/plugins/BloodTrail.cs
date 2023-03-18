using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Blood Trail", "hoppel", "2.0.0")]
    [Description("Leaves a trail of blood behind players while bleeding")]
    public class BloodTrail : CovalencePlugin
    {
        #region Configuration

        private static Configuration config;

        public class Configuration
        {
            [JsonProperty("Blood trail refresh time")]
            public float RefreshTime = 0.2f;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Initialization

        private static BloodTrail instance;

        private const string permAllow = "bloodtrail.allow";
        private const string permBypass = "bloodtrail.bypass";

        private void Init()
        {
            instance = this;

            permission.RegisterPermission(permAllow, this);
            permission.RegisterPermission(permBypass, this);
        }

        private void OnServerInitialized()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }

        private void Unload()
        {
            Object[] objects = UnityEngine.Object.FindObjectsOfType(typeof(Blood));
            if (objects != null)
            {
                foreach (Object gameObj in objects)
                {
                    UnityEngine.Object.Destroy(gameObj);
                }
            }

            config = null;
            instance = null;
        }

        #endregion Initialization

        #region Blood Trail

        private void OnPlayerConnected(BasePlayer player)
        {
            if (HasPermission(player) && !player.gameObject.GetComponent<Blood>())
            {
                player.gameObject.AddComponent<Blood>();
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player.gameObject.GetComponent<Blood>())
            {
                UnityEngine.Object.Destroy(player.gameObject.GetComponent<Blood>());
            }
        }

        public class Blood : MonoBehaviour
        {
            private BasePlayer player;
            private Vector3 position;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                position = player.transform.position;
                InvokeRepeating("Track", 0.2f, config.RefreshTime);
            }

            private void Track()
            {
                if (player == null || !instance.HasPermission(player))
                {
                    return;
                }

                {
                    if (position == player.transform.position)
                    {
                        return;
                    }

                    position = player.transform.position;

                    if (!player || !player.IsConnected)
                    {
                        Destroy(this);
                        return;
                    }

                    if (player.metabolism.bleeding.value > 0)
                    {
                        Effect.server.Run("assets/bundled/prefabs/fx/player/beartrap_blood.prefab", player.transform.position, Vector3.up, null, true);
                    }
                }
            }

            private void OnDestroy()
            {
                CancelInvoke("Track");
                Destroy(this);
            }
        }

        #endregion Blood Trail

        #region Helpers

        private bool HasPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, permAllow) && !permission.UserHasPermission(player.UserIDString, permBypass);
        }

        #endregion Helpers
    }
}
