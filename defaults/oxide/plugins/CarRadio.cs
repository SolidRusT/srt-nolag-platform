using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Rust.Instruments;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static InstrumentKeyController;

namespace Oxide.Plugins
{
    [Info("Car Radio", "TCM420G", "1.0.3")]
    [Description("Allows players to attach radios to vehicles")]
    class CarRadio : CovalencePlugin
    {
        #region variables
        private const string PERMISSION_ATTACHRADIO = "carradio.attachcarradio";
        private const string PERMISSION_DETACHRADIO = "carradio.detachcarradio";
        private const string PERMISSION_ATTACHRADIO_GLOBAL = "carradio.attachallcarradio";
        private const string PERMISSION_DETACHRADIO_GLOBAL = "carradio.detachallcarradio";

        private const string I18N_MISSING_SIREN = "NoRadioForName";
        private const string I18N_COULD_NOT_ATTACH = "CouldNotAttach";
        private const string I18N_NOT_SUPPORTED = "NotSupported";
        private const string I18N_ATTACHED = "Attached";
        private const string I18N_ATTACHED_GLOBAL = "AttachedGlobal";
        private const string I18N_DETACHED = "Detached";
        private const string I18N_DETACHED_GLOBAL = "DetachedGlobal";
        private const string I18N_NOT_A_VEHICLE = "NotAVehicle";
        private const string I18N_RADIO = "Radios";
        private const string I18N_PLAYERS_ONLY = "PlayersOnly";

        // Initial prefabs
        private const string PREFAB_COCKPIT = "assets/content/vehicles/modularcar/module_entities/1module_cockpit.prefab";
        private const string PREFAB_COCKPIT_ARMORED = "assets/content/vehicles/modularcar/module_entities/1module_cockpit_armored.prefab";
        private const string PREFAB_COCKPIT_WITH_ENGINE = "assets/content/vehicles/modularcar/module_entities/1module_cockpit_with_engine.prefab";
        private const string PREFAB_BUTTON = "assets/prefabs/deployable/playerioents/button/button.prefab";
        private const string PREFAB_FLASHERLIGHT = null;
        private const string PREFAB_SIRENLIGHT = null;
        private const string PREFAB_RADIO = "assets/prefabs/voiceaudio/boombox/boombox.static.prefab";

        private const string PREFAB_SEDAN = "assets/content/vehicles/sedan_a/sedantest.entity.prefab";
        private const string PREFAB_MINICOPTER = "assets/content/vehicles/minicopter/minicopter.entity.prefab";
        private const string PREFAB_TRANSPORTHELI = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab";
        private const string PREFAB_RHIB = "assets/content/vehicles/boats/rhib/rhib.prefab";
        private const string PREFAB_ROWBOAT = "assets/content/vehicles/boats/rowboat/rowboat.prefab";
        private const string PREFAB_WORKCART = "assets/content/vehicles/workcart/workcart.entity.prefab";
        private const string PREFAB_MAGNETCRANE = "assets/content/vehicles/crane_magnet/magnetcrane.entity.prefab";
        private const string PREFAB_HORSE = "assets/rust.ai/nextai/testridablehorse.prefab";

        private const string KEY_MODULAR_CAR = "MODULAR_CAR";

        private const string DATAPATH_RADIO = "carradio/";

        // Preconfigured carradio
        private static readonly Radio SIREN_DEFAULT = new Radio("Car-Radio",
            new Dictionary<string, Attachment[]>
            {
                [PREFAB_COCKPIT] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(0.05f, 1.7f, 0.78f), new Vector3(210f, 0f, 0f)),
                    new Attachment(PREFAB_RADIO, new Vector3(-0.00f, 1.0f, -0.9f), new Vector3(180f, 180f, 180f))
                },
                [PREFAB_COCKPIT_ARMORED] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(0.05f, 1.7f, 0.78f), new Vector3(210f, 0f, 0f)),
                    new Attachment(PREFAB_RADIO, new Vector3(-0.00f, 1.0f, -0.9f), new Vector3(180f, 180f, 180f))
                },
                [PREFAB_COCKPIT_WITH_ENGINE] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(0.05f, 1.7f, 0.78f), new Vector3(210f, 0f, 0f)),
                    new Attachment(PREFAB_RADIO, new Vector3(-0.00f, 1.0f, -0.9f), new Vector3(180f, 180f, 180f))
                }
            },
           new Dictionary<string, Attachment[]>
            {
                [PREFAB_SEDAN] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(0.0f, 2.05f, 1.9f), new Vector3(210f, 0f, 0f)),
					    new Attachment(PREFAB_RADIO, new Vector3(0.0f, 0.55f, 1.7f), new Vector3(0f, 180f, 0f))
                },
                [PREFAB_MINICOPTER] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(-0.1f, 2.0f, 1.0f), new Vector3(180f, 0f, 0f)),
                    new Attachment(PREFAB_RADIO, new Vector3(0.0f, 0.700f, -0.1f), new Vector3(0f, 0f, 0f))
                },
                [PREFAB_TRANSPORTHELI] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(-0.1f, 2.68f, 3.865f), new Vector3(205f, 0f, 0f)),
                    new Attachment(PREFAB_RADIO, new Vector3(0.0f, 0.55f, 3.6f), new Vector3(0f, 180f, 0f))
                },
                [PREFAB_RHIB] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(-0.0f, 3.4f, 0.60f), new Vector3(180f, 0f, 0f)),
                    new Attachment(PREFAB_RADIO, new Vector3(0.0f, 2.83f, 0.62f), new Vector3(0f, 180f, 0f))
                },
                [PREFAB_ROWBOAT] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(-1.7f, 0.5f, -1.8f), new Vector3(270f, 270f, 0f)),
                    new Attachment(PREFAB_RADIO, new Vector3(0.0f, 0.8f, 2.18f), new Vector3(0f, 180f, 0f))
                },
                [PREFAB_WORKCART] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(0.19f, 3.13f, 4.95f), new Vector3(235f, 0f, 0f))
                },
                [PREFAB_MAGNETCRANE] = new Attachment[] {
                    new Attachment(PREFAB_FLASHERLIGHT, new Vector3(-0.95f, 4.25f, 0.5f))
                }
            }, new Tone(Notes.A, NoteType.Regular, 4, 1f), new Tone(Notes.D, NoteType.Regular, 5, 1f));
        private static readonly Radio SIREN_SILENT = new Radio("test-radio",
            new Dictionary<string, Attachment[]>
            {
                [PREFAB_COCKPIT] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(0.05f, 1.7f, 0.78f), new Vector3(210f, 0f, 0f)),
                    new Attachment(PREFAB_RADIO, new Vector3(-0.00f, 1.0f, -0.9f), new Vector3(180f, 180f, 180f))
                },
                [PREFAB_COCKPIT_ARMORED] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(0.05f, 1.7f, 0.78f), new Vector3(210f, 0f, 0f)),
                    new Attachment(PREFAB_RADIO, new Vector3(-0.00f, 1.0f, -0.9f), new Vector3(180f, 180f, 180f))
                },
                [PREFAB_COCKPIT_WITH_ENGINE] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(0.05f, 1.7f, 0.78f), new Vector3(210f, 0f, 0f)),
                    new Attachment(PREFAB_RADIO, new Vector3(-0.00f, 1.0f, -0.9f), new Vector3(180f, 180f, 180f))
                }
            },
            new Dictionary<string, Attachment[]>
          {
                [PREFAB_SEDAN] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(0.0f, 2.05f, 1.9f), new Vector3(210f, 0f, 0f)),
					    new Attachment(PREFAB_RADIO, new Vector3(0.0f, 0.55f, 3.6f), new Vector3(0f, 180f, 0f))
                },
                [PREFAB_MINICOPTER] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(-0.1f, 2.0f, 1.0f), new Vector3(180f, 0f, 0f)),
                    new Attachment(PREFAB_RADIO, new Vector3(0.0f, 0.700f, -0.1f), new Vector3(0f, 0f, 0f))
                },
                [PREFAB_TRANSPORTHELI] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(-0.1f, 2.68f, 3.865f), new Vector3(205f, 0f, 0f)),
                    new Attachment(PREFAB_RADIO, new Vector3(0.0f, 0.55f, 3.6f), new Vector3(0f, 180f, 0f))
                },
                [PREFAB_RHIB] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(-0.0f, 3.4f, 0.60f), new Vector3(180f, 0f, 0f)),
                    new Attachment(PREFAB_RADIO, new Vector3(0.0f, 2.83f, 0.62f), new Vector3(0f, 180f, 0f))
                },
                [PREFAB_ROWBOAT] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(-1.7f, 0.5f, -1.8f), new Vector3(270f, 270f, 0f)),
                    new Attachment(PREFAB_RADIO, new Vector3(0.0f, 0.8f, 2.18f), new Vector3(0f, 180f, 0f))
                },
                [PREFAB_WORKCART] = new Attachment[] {
                    new Attachment(PREFAB_BUTTON, new Vector3(0.19f, 3.13f, 4.95f), new Vector3(235f, 0f, 0f))
                },
                [PREFAB_MAGNETCRANE] = new Attachment[] {
                    new Attachment(PREFAB_FLASHERLIGHT, new Vector3(-0.95f, 4.25f, 0.5f))
                },
                [PREFAB_HORSE] = new Attachment[] {
                    new Attachment(PREFAB_RADIO, new Vector3(0.0f, 1.7f, 1.2f), new Vector3(25f, 0f, 0f), "head")
                }
            });
        #endregion variables

        #region data
        private class DataContainer
        {
            // Map BaseVehicle.net.ID -> RadioInfos
            public Dictionary<uint, VehicleContainer> VehicleRadioMap = new Dictionary<uint, VehicleContainer>();
        }

        private class VehicleContainer
        {
            public string RadioName = SIREN_DEFAULT.Name;
            public RadioController.States State = RadioController.States.OFF;
            public HashSet<uint> NetIDs = new HashSet<uint>();

            public VehicleContainer()
            {
            }

            public VehicleContainer(string aRadioName, RadioController.States aState, IEnumerable<uint> someNetIDs)
            {
                RadioName = aRadioName;
                State = aState;
                NetIDs.UnionWith(someNetIDs);
            }
        }
        #endregion data

        #region configuration

        private Configuration config;
        private IDictionary<string, Radio> RadioDictionary { get; } = new Dictionary<string, Radio>();

        private class Configuration
        {
            [JsonProperty("MountNeeded")]
            public bool MountNeeded = true;

            [JsonProperty("SoundEnabled")]
            public bool SoundEnabled = true;

            [JsonProperty("RadioSpawnProbability")]
            public Dictionary<string, float> RadioSpawnProbability = new Dictionary<string, float>
            {
                [KEY_MODULAR_CAR] = 0f,
                [PREFAB_HORSE] = 0f,
                [PREFAB_MINICOPTER] = 0f,
                [PREFAB_SEDAN] = 0f,
                [PREFAB_TRANSPORTHELI] = 0f
            };

            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty("DefaultState")]
            public RadioController.States DefaultState = RadioController.States.OFF;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        private class Tone
        {
            public Tone(Notes aNote = Notes.A, NoteType aNoteType = NoteType.Regular, int anOctave = 4, float aDuration = 1f)
            {
                Note = aNote;
                NoteType = aNoteType;
                Octave = anOctave;
                Duration = aDuration;
            }

            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty("Note")]
            public Notes Note;

            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty("NoteType")]
            public NoteType NoteType;

            [JsonProperty("Octave")]
            public int Octave;

            [JsonProperty("Duration")]
            public float Duration;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        private class Radio
        {
            public Radio(string aName, Dictionary<string, Attachment[]> someModules, Dictionary<string, Attachment[]> someVehicles, params Tone[] someTones)
            {
                Name = aName;
                Modules = someModules;
                Vehicles = someVehicles;
                Tones = someTones;
            }

            [JsonProperty("Name")]
            public string Name;

            [JsonProperty("Tones")]
            public Tone[] Tones;

            [JsonProperty("Modules")]
            public Dictionary<string, Attachment[]> Modules;

            [JsonProperty("Vehicles")]
            public Dictionary<string, Attachment[]> Vehicles;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        private class Attachment
        {
            public Attachment(string aPrefab, Vector3 aPosition, Vector3 anAngle = new Vector3(), string aBone = null)
            {
                Prefab = aPrefab;
                Position = aPosition;
                Angle = anAngle;
                Bone = aBone;
            }

            [JsonProperty("Prefab")]
            public string Prefab;

            [JsonProperty("Position")]
            public Vector3 Position;

            [JsonProperty("Angle")]
            public Vector3 Angle;

            [JsonProperty("Bone")]
            public string Bone;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            RadioDictionary.Clear();
            RadioDictionary.Add(SIREN_DEFAULT.Name, SIREN_DEFAULT);
            RadioDictionary.Add(SIREN_SILENT.Name, SIREN_SILENT);
        }

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

                try
                {
                    foreach (string eachRadioFile in Interface.Oxide.DataFileSystem.GetFiles(DATAPATH_RADIO, "*.json"))
                    {
                        string theFilename = eachRadioFile.Basename(".json");
                        try
                        {
                            Radio theRadio = Interface.Oxide.DataFileSystem.ReadObject<Radio>(DATAPATH_RADIO + theFilename);
                            RadioDictionary.Add(theRadio.Name, theRadio);
                        }
                        catch
                        {
                            PrintWarning($"Radio file {theFilename}.json is invalid; ignoring");
                        }
                    }
                }
                catch
                {

                }
                Puts("Loaded carradio: " + string.Join(", ", RadioDictionary.Keys));

                if (RadioDictionary.IsEmpty())
                {
                    PrintWarning("Configuration appears to be missing carradio; using defaults");
                    RadioDictionary.Add(SIREN_DEFAULT.Name, SIREN_DEFAULT);
                    RadioDictionary.Add(SIREN_SILENT.Name, SIREN_SILENT);
                    SaveConfig();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }

            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            PrintWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);

            foreach (Radio eachRadio in RadioDictionary.Values)
            {
                Interface.Oxide.DataFileSystem.WriteObject(DATAPATH_RADIO + eachRadio.Name, eachRadio);
            }
        }
        #endregion configuration

        #region localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [I18N_MISSING_SIREN] = "No radio was found for the given name (using {0} instead)",
                [I18N_COULD_NOT_ATTACH] = "Could not attach '{0}'",
                [I18N_ATTACHED] = "Attached radio '{0}'",
                [I18N_ATTACHED_GLOBAL] = "Attached radio '{0}' to all existing cars",
                [I18N_DETACHED] = "Detached radio",
                [I18N_DETACHED_GLOBAL] = "Detached all existing carradio",
                [I18N_NOT_A_VEHICLE] = "This entity is not a (supported) vehicle",
                [I18N_RADIO] = "Available carradio: {0}",
                [I18N_PLAYERS_ONLY] = "Command '{0}' can only be used by a player",
                [I18N_NOT_SUPPORTED] = "The radio '{0}' has no configuration for '{1}'"
            }, this);
        }
        #endregion localization

        #region commands
        [Command("attachradio"), Permission(PERMISSION_ATTACHRADIO)]
        private void AttachCarRadios(IPlayer aPlayer, string aCommand, string[] someArgs)
        {
            if (aPlayer.IsServer)
            {
                Message(aPlayer, I18N_PLAYERS_ONLY, aCommand);
                return;
            }

            BaseVehicle theVehicle = RaycastVehicle(aPlayer);
            if (theVehicle)
            {
                Radio theRadio = someArgs.Length > 0 ? FindRadioForName(someArgs[0], aPlayer) : RadioDictionary.Values.First();
                AttachRadios(theVehicle, theRadio, config.DefaultState, aPlayer);
                Message(aPlayer, I18N_ATTACHED, theRadio.Name);
            }
        }

        [Command("removeradio"), Permission(PERMISSION_DETACHRADIO)]
        private void DetachCarRadios(IPlayer aPlayer, string aCommand, string[] someArgs)
        {
            if (aPlayer.IsServer)
            {
                Message(aPlayer, I18N_PLAYERS_ONLY, aCommand);
                return;
            }

            BaseVehicle theVehicle = RaycastVehicle(aPlayer);
            if (theVehicle && DetachRadios(theVehicle))
            {
                Message(aPlayer, I18N_DETACHED);
            }
        }

        [Command("attachallcarradio"), Permission(PERMISSION_ATTACHRADIO_GLOBAL)]
        private void AttachAllCarRadios(IPlayer aPlayer, string aCommand, string[] someArgs)
        {
            Radio theRadio = someArgs.Length > 0 ? FindRadioForName(someArgs[0], aPlayer) : RadioDictionary.Values.First();
            foreach (BaseVehicle eachVehicle in BaseNetworkable.serverEntities.OfType<BaseVehicle>())
            {
                AttachRadios(eachVehicle, theRadio, config.DefaultState, aPlayer);
            }
            Message(aPlayer, I18N_ATTACHED_GLOBAL, theRadio.Name);
        }

        [Command("detachallcarradio"), Permission(PERMISSION_DETACHRADIO_GLOBAL)]
        private void DetachAllCarRadios(IPlayer aPlayer, string aCommand, string[] someArgs)
        {
            foreach (BaseVehicle eachVehicle in BaseNetworkable.serverEntities.OfType<BaseVehicle>())
            {
                DetachRadios(eachVehicle);
            }
            Message(aPlayer, I18N_DETACHED_GLOBAL);
        }

        

        [Command("togglecarradio")]
        private void ToggleRadios(IPlayer aPlayer, string aCommand, string[] someArgs)
        {
            if (aPlayer.IsServer)
            {
                Message(aPlayer, I18N_PLAYERS_ONLY, aCommand);
                return;
            }

            BasePlayer thePlayer = aPlayer.Object as BasePlayer;
            BaseVehicle theVehicle = thePlayer?.GetMountedVehicle();
            if (theVehicle)
            {
                theVehicle.GetComponent<RadioController>()?.ChangeState();
            }
            else if (!config.MountNeeded)
            {
                RaycastVehicle(aPlayer)?.GetComponent<RadioController>()?.ChangeState(); ;
            }
        }
        #endregion commands

        #region hooks
        private void Unload()
        {
            OnServerSave();

            foreach (BaseVehicle eachVehicle in BaseNetworkable.serverEntities.OfType<BaseVehicle>())
            {
                DetachRadios(eachVehicle);
            }
        }

        private void OnServerSave()
        {
            DataContainer thePersistentData = new DataContainer();
            foreach (BaseVehicle eachCar in BaseNetworkable.serverEntities.OfType<BaseVehicle>())
            {
                RadioController theController = eachCar.GetComponent<RadioController>();
                thePersistentData.VehicleRadioMap.Add(eachCar.net.ID, theController ? new VehicleContainer(theController.Radio.Name, theController.State, theController.NetIDs) : null);
            }
            Interface.Oxide.DataFileSystem.WriteObject(Name, thePersistentData);
        }

        private void OnServerInitialized(bool anInitialFlag)
        {
            bool theSpawnRandomlyFlag = config.RadioSpawnProbability.Any(entry => entry.Value > 0f);
            if (!theSpawnRandomlyFlag)
            {
                Unsubscribe("OnEntitySpawned");
            }

            // Reattach on server restart
            DataContainer thePersistentData = Interface.Oxide.DataFileSystem.ReadObject<DataContainer>(Name);
            foreach (BaseVehicle eachVehicle in BaseNetworkable.serverEntities.OfType<BaseVehicle>())
            {
                VehicleContainer theContainer;
                if (thePersistentData.VehicleRadioMap.TryGetValue(eachVehicle.net.ID, out theContainer))
                {
                    if (theContainer != null)
                    {
                        Radio theRadio;
                        if (RadioDictionary.TryGetValue(theContainer.RadioName, out theRadio))
                        {
                            CreateRadioController(eachVehicle, theRadio, theContainer.NetIDs);
                            AttachRadios(eachVehicle, theRadio, theContainer.State);
                        }
                        else
                        {
                            CreateRadioController(eachVehicle, null, theContainer.NetIDs);
                            DetachRadios(eachVehicle);
                            PrintWarning($"Missing radio for name \"{theContainer.RadioName}\". Ignoring...");
                        }
                    }
                }
                else if (theSpawnRandomlyFlag)
                {
                    RadioController theController = eachVehicle.GetComponent<RadioController>();
                    if (!theController)
                    {
                        float theProbability;
                        if (config.RadioSpawnProbability.TryGetValue(eachVehicle is ModularCar ? KEY_MODULAR_CAR : eachVehicle.PrefabName, out theProbability) && Core.Random.Range(0f, 1f) < theProbability)
                        {
                            AttachRadios(eachVehicle, RadioDictionary.Values.First(), config.DefaultState);
                        }
                    }
                }
            }
        }

        private object OnButtonPress(PressButton aButton, BasePlayer aPlayer)
        {
            BaseVehicle theVehicle = aButton.GetComponentInParent<BaseVehicle>()?.VehicleParent();
            theVehicle = theVehicle ? theVehicle : aButton.GetComponentInParent<BaseVehicle>();
            if (theVehicle)
            {
                RadioController theController = theVehicle.GetComponent<RadioController>();
                if (theController)
                {
                    if ((config.MountNeeded && aPlayer.GetMountedVehicle() != theVehicle) || !theController.NetIDs.Contains(aButton.net.ID))
                    {
                        return false;
                    }
                    theController.ChangeState();
                }
            }
            return null;
        }

        private void OnEntitySpawned(BaseVehicle aVehicle)
        {
            RadioController theController = aVehicle.GetComponent<RadioController>();
            if (!theController)
            {
                float theProbability;
                if (config.RadioSpawnProbability.TryGetValue(aVehicle is ModularCar ? KEY_MODULAR_CAR : aVehicle.PrefabName, out theProbability) && Core.Random.Range(0f, 1f) < theProbability)
                {
                    AttachRadios(aVehicle, RadioDictionary.Values.First(), config.DefaultState);
                }
            }
        }
        #endregion hooks

        #region methods
        /// <summary>
        /// Tries to attach the given radio to the vehicle, replacing any existing radio.
        /// </summary>
        /// <param name="aVehicle">The vehicle.</param>
        /// <param name="aRadio">The radio.</param>
        /// <param name="anInitialState">The initial radio state.</param>
        /// <param name="aPlayer">The calling player.</param>
        private void AttachRadios(BaseVehicle aVehicle, Radio aRadio, RadioController.States anInitialState, IPlayer aPlayer = null)
        {
            DetachRadios(aVehicle);
            RadioController theController = CreateRadioController(aVehicle, aRadio);
            if (aVehicle as ModularCar)
            {
                if (aRadio.Modules == null)
                {
                    Message(aPlayer, I18N_NOT_SUPPORTED, aRadio.Name, KEY_MODULAR_CAR);
                    DetachRadios(aVehicle);
                    return;
                }
                foreach (BaseVehicleModule eachModule in aVehicle.GetComponentsInChildren<BaseVehicleModule>())
                {
                    SpawnAttachments(aRadio.Modules, aPlayer, theController, eachModule);
                }
            }
            else if (!SpawnAttachments(aRadio.Vehicles, aPlayer, theController, aVehicle))
            {
                Message(aPlayer, I18N_NOT_SUPPORTED, aRadio.Name, aVehicle.PrefabName);
                DetachRadios(aVehicle);
                return;
            }
            theController.SetState(anInitialState);
        }

        /// <summary>
        /// Spawns the attachments for the given dictionary for the given parent entity.
        /// </summary>
        /// <param name="someAttachments">The dictionary.</param>
        /// <param name="aPlayer">The calling player.</param>
        /// <param name="theController">The RadioController of the Parent.</param>
        /// <param name="aParent">The Parent.</param>
        /// <returns>True, if the parent has an entry in the dictionary with at least one Attachment.</returns>
        private bool SpawnAttachments(IDictionary<string, Attachment[]> someAttachments, IPlayer aPlayer, RadioController theController, BaseEntity aParent)
        {
            if (someAttachments == null)
            {
                return false;
            }

            Attachment[] theAttachments;
            if (someAttachments.TryGetValue(aParent.PrefabName, out theAttachments))
            {
                foreach (Attachment eachAttachment in theAttachments)
                {
                    BaseEntity theNewEntity = AttachEntity(aParent, eachAttachment.Prefab, eachAttachment.Position, eachAttachment.Angle, eachAttachment.Bone);
                    if (theNewEntity)
                    {
                        theController.NetIDs.Add(theNewEntity.net.ID);
                    }
                    else if (aPlayer != null)
                    {
                        Message(aPlayer, I18N_COULD_NOT_ATTACH, eachAttachment.Prefab);
                    }
                }
                return !theAttachments.IsEmpty();
            }
            return false;
        }

        /// <summary>
        /// Creates or replaces the RadioController of the given vehicle.
        /// </summary>
        /// <param name="aVehicle">The vehicle.</param>
        /// <param name="aRadio">The Radio.</param>
        /// <param name="someNetIDs">Already existing radio entities.</param>
        /// <returns>The newly created RadioController.</returns>
        private RadioController CreateRadioController(BaseVehicle aVehicle, Radio aRadio, IEnumerable<uint> someNetIDs = null)
        {
            RadioController theController = aVehicle.GetComponent<RadioController>();
            if (theController)
            {
                UnityEngine.Object.DestroyImmediate(theController);
            }
            theController = aVehicle.gameObject.AddComponent<RadioController>();
            theController.Config = config;
            theController.Radio = aRadio;
            if (someNetIDs != null)
            {
                theController.NetIDs.UnionWith(someNetIDs);
            }
            return theController;
        }

        /// <summary>
        /// Detaches the radio from a vehicle and removes all corresponding entities.
        /// </summary>
        /// <param name="aVehicle"> The vehicle.</param>
        /// <returns>True, if a radio was removed.</returns>
        private bool DetachRadios(BaseVehicle aVehicle)
        {
            RadioController theController = aVehicle.GetComponent<RadioController>();
            if (theController)
            {
                foreach (BaseEntity eachEntity in aVehicle.GetComponentsInChildren<BaseEntity>())
                {
                    if (theController.NetIDs.Contains(eachEntity.net.ID))
                    {
                        Destroy(eachEntity);
                    }
                }
                UnityEngine.Object.DestroyImmediate(theController);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Destroys the entity.
        /// </summary>
        /// <param name="anEntity">The entity.</param>
        private static void Destroy(BaseEntity anEntity)
        {
            if (!anEntity.IsDestroyed)
            {
                anEntity.Kill();
            }
        }

        /// <summary>
        /// Attaches the prefab entity at the given local position and angles to the parent.
        /// </summary>
        /// <param name="aParent">The parent.</param>
        /// <param name="aPrefab">The prefab for the new entity.</param>
        /// <param name="aPosition">The local position.</param>
        /// <param name="anAngle">The local angles.</param>
        /// <returns></returns>
        private BaseEntity AttachEntity(BaseEntity aParent, string aPrefab, Vector3 aPosition, Vector3 anAngle = new Vector3(), string aBone = null)
        {
            BaseEntity theNewEntity = GameManager.server.CreateEntity(aPrefab, aParent.transform.position);
            if (!theNewEntity)
            {
                return null;
            }

            theNewEntity.Spawn();
            Transform theBone = aParent.FindBone(aBone);
            if (theBone == null && aBone != null)
            {
                PrintWarning($"No bone found for name '{aBone}'");
                PrintWarning("Valid bone names: " + string.Join(", ", aParent.GetBones().Select(eachBone => eachBone.name)));
            }

            if (theBone != null && theBone != aParent.transform)
            {
                theNewEntity.SetParent(aParent, theBone.name);
                theNewEntity.transform.localPosition = theBone.InverseTransformPoint(aParent.transform.TransformPoint(aPosition));
                theNewEntity.transform.localRotation = Quaternion.Inverse(theBone.rotation) * (aParent.transform.rotation * Quaternion.Euler(anAngle));
            }
            else
            {
                theNewEntity.transform.localPosition = aPosition;
                theNewEntity.transform.localEulerAngles = anAngle;
                theNewEntity.SetParent(aParent);
            }
            //Puts(theNewEntity.ShortPrefabName + ": (" + theNewEntity.GetComponents<Component>().Length + ") " + string.Join(", ", theNewEntity.GetComponents<Component>().Select(eachComp => eachComp.GetType().Name)));
            UnityEngine.Object.DestroyImmediate(theNewEntity.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(theNewEntity.GetComponent<GroundWatch>());
            UnityEngine.Object.DestroyImmediate(theNewEntity.GetComponent<BoxCollider>());
            UnityEngine.Object.DestroyImmediate(theNewEntity.GetComponent<InstrumentKeyController>());
            theNewEntity.OwnerID = 0;
            BaseCombatEntity theCombatEntity = theNewEntity as BaseCombatEntity;
            if (theCombatEntity)
            {
                theCombatEntity.pickup.enabled = false;
            }
            PressButton theButton = theNewEntity as PressButton;
            if (theButton)
            {
                theButton.pressDuration = 0.2f;
            }

            theNewEntity.EnableSaving(true);
            theNewEntity.SendNetworkUpdateImmediate();
            return theNewEntity;
        }

        /// <summary>
        /// Toggles the IOEntity.
        /// </summary>
        /// <param name="anIOEntity">The IOEntity.</param>
        /// <param name="theEnabledFlag">The new state.</param>
        private static void ToogleRadios(IOEntity anIOEntity, bool theEnabledFlag)
        {
            anIOEntity.UpdateHasPower(theEnabledFlag ? anIOEntity.ConsumptionAmount() : 0, 0);
            anIOEntity.SetFlag(BaseEntity.Flags.On, theEnabledFlag);
        }
        #endregion methods

        #region helpers
        private BaseVehicle RaycastVehicle(IPlayer aPlayer)
        {
            RaycastHit theHit;
            if (!Physics.Raycast((aPlayer.Object as BasePlayer).eyes.HeadRay(), out theHit, 5f))
            {
                return null;
            }

            BaseVehicle theVehicle = theHit.GetEntity()?.GetComponentInParent<BaseVehicle>();
            if (!theVehicle)
            {
                Message(aPlayer, I18N_NOT_A_VEHICLE);
            }
            return theVehicle;
        }

        private Radio FindRadioForName(string aName, IPlayer aPlayer)
        {
            Radio theRadio;
            if (!RadioDictionary.TryGetValue(aName, out theRadio))
            {
                theRadio = RadioDictionary.Values.First();
                Message(aPlayer, I18N_MISSING_SIREN, theRadio.Name);
            }
            return theRadio;
        }

        private string GetText(string aKey, string aPlayerId = null, params object[] someArgs) => string.Format(lang.GetMessage(aKey, this, aPlayerId), someArgs);

        private void Message(IPlayer aPlayer, string anI18nKey, params object[] someArgs)
        {
            if (aPlayer.IsConnected)
            {
                string theText = GetText(anI18nKey, aPlayer.Id, someArgs);
                aPlayer.Reply(theText != anI18nKey ? theText : anI18nKey);
            }
        }

        private void Message(BasePlayer aPlayer, string anI18nKey, params object[] someArgs)
        {
            if (aPlayer.IsConnected)
            {
                string theText = GetText(anI18nKey, aPlayer.UserIDString, someArgs);
                aPlayer.ChatMessage(theText != anI18nKey ? theText : anI18nKey);
            }
        }
        #endregion helpers

        #region controllers
        private class RadioController : FacepunchBehaviour
        {
            public enum States
            {
                OFF,
                ON,
                LIGHTS_ONLY
            }

            private BaseVehicle vehicle;
            private InstrumentTool trumpet;
            public Configuration Config { get; set; }
            public States State { get; private set; }
            public Radio Radio { get; set; }
            public ISet<uint> NetIDs { get; } = new HashSet<uint>();

            public States ChangeState()
            {
                SetState(State >= States.LIGHTS_ONLY ? States.OFF : State + 1);
                return State;
            }

            public void SetState(States aState)
            {
                State = aState;
                if ((!Config.SoundEnabled || Radio?.Tones?.Length < 1 || !GetTrumpet()) && State == States.ON)
                {
                    State++;
                }
                RefreshRadioState();
            }

            public void RefreshRadioState()
            {
                if (State == States.ON)
                {
                    PlayTone(0);
                }
                bool theLightsOnFlag = State > States.OFF;
                foreach (IOEntity eachEntity in GetVehicle().GetComponentsInChildren<IOEntity>())
                {
                    if (NetIDs.Contains(eachEntity.net.ID) && !(eachEntity is PressButton))
                    {
                        ToogleRadios(eachEntity, theLightsOnFlag);
                    }
                }
            }

            private InstrumentTool GetTrumpet()
            {
                if (trumpet == null || trumpet.IsDestroyed)
                {
                    trumpet = GetVehicle().GetComponentInChildren<InstrumentTool>();
                }
                return trumpet;
            }

            private BaseVehicle GetVehicle()
            {
                if (vehicle == null)
                {
                    vehicle = GetComponentInParent<BaseVehicle>();
                }
                return vehicle;
            }

            private void PlayTone(int anIndex)
            {
                if (State != States.ON || !GetTrumpet())
                {
                    return;
                }
                if (anIndex >= Radio.Tones.Length)
                {
                    anIndex = 0;
                }
                Tone theTone = Radio.Tones[anIndex];
                GetTrumpet().ClientRPC(null, "Client_PlayNote", (int)theTone.Note, (int)theTone.NoteType, theTone.Octave, 1f);
                Invoke(() => GetTrumpet().ClientRPC(null, "Client_StopNote", (int)theTone.Note, (int)theTone.NoteType, theTone.Octave), theTone.Duration);
                Invoke(() => PlayTone(++anIndex), theTone.Duration);
            }
        }
        #endregion controllers
    }
}
