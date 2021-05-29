using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Console Filter", "Wulf", "0.0.2")]
    [Description("Filters debug, test, and other undesired output in the server console")]
    class ConsoleFilter : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        class Configuration
        {
            // TODO: Add support for regex matching

            [JsonProperty("List of partial strings to filter", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Filter = new List<string>
            {
                "AngryAnt Behave version",
                "alphamapResolution is clamped to the range of",
                "api.facepunch.com/api/public/manifest/",
                "Checking for new Steam Item Definitions..",
                "Floating point textures aren't supported on this device",
                "HDR Render Texture not supported, disabling HDR on reflection probe",
                "Image Effects are not supported on this platform",
                "Loading Prefab Bundle",
                "Missing shader in",
                "Missing projectileID",
                "Motion vectors not supported on a platform that does not support",
                "SwitchParent Missed",
                "saddletest",
                "The image effect Main Camera",
                "The image effect effect -",
                "The referenced script",
                "Unsupported encoding: 'utf8'",
                "Warning, null renderer for ScaleRenderer!",
                "[AmplifyColor]",
                "[AmplifyOcclusion]",
                "[CoverageQueries] Disabled due to unsupported",
                "[CustomProbe]",
                "[Manifest] URI IS",
                "[SpawnHandler] populationCounts",
                ", disk("
            };

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
            LogWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Filtering

        private void Init()
        {
            UnityEngine.Application.logMessageReceived += HandleLog;
#if HURTWORLD
            UnityEngine.Application.logMessageReceived -= ConsoleManager.Instance.CaptureLog;
#elif RUST
            UnityEngine.Application.logMessageReceived -= Facepunch.Output.LogHandler;
#elif SEVENDAYSTODIE
            UnityEngine.Application.logMessageReceivedThreaded -= Logger.Main.UnityLogCallback;
#endif
        }

        private void Unload()
        {
#if HURTWORLD
            UnityEngine.Application.logMessageReceived += ConsoleManager.Instance.CaptureLog;
#elif RUST
            UnityEngine.Application.logMessageReceived += Facepunch.Output.LogHandler;
#elif SEVENDAYSTODIE
            UnityEngine.Application.logMessageReceivedThreaded += Logger.Main.UnityLogCallback;
#endif
            UnityEngine.Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string message, string stackTrace, UnityEngine.LogType type)
        {
            if (!string.IsNullOrEmpty(message) && !config.Filter.Any(message.Contains))
            {
#if HURTWORLD
                ConsoleManager.Instance.CaptureLog(message, stackTrace, type);
#elif RUST
                Facepunch.Output.LogHandler(message, stackTrace, type);
#elif SEVENDAYTODIE
                Logger.Main.SendToLogListeners(message, stackTrace, type);
#endif
            }
        }

        #endregion Filtering
    }
}
