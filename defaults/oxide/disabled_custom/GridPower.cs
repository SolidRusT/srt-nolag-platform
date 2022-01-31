using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("GridPower", "Nikedemos", VERSION)]
    [Description("Allows players to take over, maintain and upgrade powerline poles to draw electric power")]
    public class GridPower : RustPlugin
    {
        #region CONST & STATIC
        private const string VERSION = "1.0.2";

        public const string FX_PLUG = "assets/prefabs/tools/wire/effects/plugeffect.prefab";
        public const string FX_TECHTRASH = "assets/prefabs/deployable/dropbox/effects/submit_items.prefab";
        public const string FX_COMBINER = "assets/prefabs/deployable/dropbox/effects/dropbox-deploy.prefab";

        public const string ITEM_LIGHTS = "xmas.lightstring.advanced";
        public const string ITEM_WIRETOOL = "wiretool";
        public const string ITEM_HOSETOOL = "hosetool";
        public const string ITEM_FUSE = "fuse";
        public const string ITEM_TECHTRASH = "techparts";
        public const string ITEM_ROOTCOMBINER = "electrical.combiner";
        public const string ITEM_LADDER = "ladder.wooden.wall";

        public const string ITEM_HAZMAT_SUIT = "hazmatsuit";
        public const string ITEM_NOMAD_SUIT = "hazmatsuit.nomadsuit";
        public const string ITEM_SPACESUIT = "hazmatsuit.spacesuit";

        public const string ITEM_SCIENTIST_SUIT = "hazmatsuit_scientist";
        public const string ITEM_SCIENTIST_SUIT_HEAVY = "scientistsuit_heavy";
        public const string ITEM_SCIENTIST_SUIT_PEACEKEEPER = "hazmatsuit_scientist_peacekeeper";

        public const string ITEM_FROGBOOTS = "boots.frog";

        public const string ITEM_HOODIE = "hoodie";
        public const string ITEM_PANTS = "pants";

        public const ulong SKIN_HOODIE_COBALT_ELECTRIC = 1581890527;
        public const ulong SKIN_HOODIE_COBALT_ELECTRIC_NOGLOW = 1582492745;

        public const ulong SKIN_PANTS_COBALT_ELECTRIC = 1581896222;

        public const string PREFAB_POWERLINE_POLE_LIGHTS_YES = "assets/bundled/prefabs/autospawn/decor/powerline-small/powerline_pole_a.prefab";
        public const string PREFAB_POWERLINE_POLE_LIGHTS_NO = "assets/content/props/powerline_poles/powerline_pole_a.prefab";

        public const string PREFAB_LADDER = "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab";
        public const string PREFAB_FUSEBOX = "assets/prefabs/io/electric/switches/fusebox/fusebox.prefab";
        public const string PREFAB_BUTTON = "assets/prefabs/deployable/playerioents/button/button.prefab";
        public const string PREFAB_ROOT_COMBINER = "assets/prefabs/deployable/playerioents/gates/combiner/electrical.combiner.deployed.prefab";
        public const string PREFAB_TESLA = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab";
        public const string PREFAB_SPHERE = "assets/prefabs/visualization/sphere.prefab";

        public const string PREFAB_GENERATOR_NEW = "assets/prefabs/deployable/playerioents/generators/generator.small.prefab";

        public const string PREFAB_GENERATOR_OLD = "assets/prefabs/io/electric/generators/generator.static.prefab";

        public const string PREFAB_SPOTLIGHT = "assets/bundled/prefabs/modding/cinematic/cinelights/cinelight_spot_warm.prefab";

        public const string PREFAB_POINTLIGHT = "assets/bundled/prefabs/modding/cinematic/cinelights/cinelight_point_warm.prefab";

        public const string COLLIDER_NAME_STREETLAMP_RIGHT = "powerline_pole_light (2)";
        public const string COLLIDER_NAME_STREETLAMP_LEFT = "powerline_pole_light (3)";

        //leave this alone or else you might break vanilla fuses
        public const float NORMAL_FUSE_DURATION_SECONDS = 200F;//

        public const string CMD_GP_CFG = "gp_cfg";

        public const string CMD_GP_EMERGENCY_CLEANUP = "gp_emergency_cleanup";

        public static BaseEntity.Flags FLAG_BAKED = BaseEntity.Flags.Reserved11;
        public static BaseEntity.Flags FLAG_INTERNAL = BaseEntity.Flags.Reserved10;
        public static BaseEntity.Flags FLAG_INDESTRUCTIBLE = BaseEntity.Flags.Reserved9;
        public static BaseEntity.Flags FLAG_HIDE_MESH = BaseEntity.Flags.Reserved1;

        private static GridPower Instance;
        public static Color[] XmasColorSequence;
        private static bool Unloading = false;
        private static bool NewSave = false;
        private static bool EmergencyCleanup = false;

        public static ListHashSet<string> ValidTools;

        public static ItemDefinition[] FuseboxAllowedItems;

        #endregion

        #region PERMISSIONS
        public const string PERM_ADMIN = "gridpower.admin";

        public const string PERM_VIP1 = "gridpower.vip1";
        public const string PERM_VIP2 = "gridpower.vip2";
        public const string PERM_VIP3 = "gridpower.vip3";
        public const string PERM_VIP4 = "gridpower.vip4";
        public const string PERM_VIP5 = "gridpower.vip5";
        #endregion

        #region CONFIG
        private ConfigData Configuration;

        public class ConfigData
        {
            public string Version = VERSION;

            public Dictionary<string, PermissionProfile> PermissionProfiles = new Dictionary<string, PermissionProfile>();

            public Dictionary<string, List<ulong>> ProtectiveClothing = new Dictionary<string, List<ulong>>();

            public float GeneratorChancePowerlineFunctional = 0.33F;

            public int GeneratorInitialOutletsMin = 0;
            public int GeneratorInitialOutletsMax = 4;

            public int GeneratorInitialLevelMin = 1;
            public int GeneratorInitialLevelMax = 5;

            public float GridProductionStartAtHour = 8F;
            public float GridProductionEndAtHour = 20F;

            public float StreetlightsTurnOnAtHour = 20F;
            public float StreetlightsTurnOffAtHour = 8F;

            public float StreetlightsReliability = 0.75F;

            public float StreetlightsFlickerLengthMin = 0.1F;
            public float StreetlightsFlickerLengthMax = 1F;

            public bool GridConstantPower = false;
            public bool StreetlightsConstantPower = false;

            public int PowerlinePowerPerTechTrash = 5;

            public float PowerlineFuseDurationSeconds = 12000F;

            public int PowerlineMaxTechTrashLevel = 50;

            public bool FuseRequired = true;

            public bool BuildingBlockPreventsButtonPress = true;

        }

        protected override void LoadDefaultConfig()
        {
            RestoreDefaultConfig();
        }

        private void ProcessConfigData()
        {
            bool needsSave = false;

            if (Configuration.PermissionProfiles?.Count == 0)
            {
                Instance.PrintWarning("No permission profiles (yet?), generating default...");
                Configuration.PermissionProfiles = new Dictionary<string, PermissionProfile>
                {
                    [PERM_ADMIN] = AdminPermissionProfile(),

                    ["default"] = DefaultPermissionProfile(),

                    [PERM_VIP1] = VIPPermissionProfile(),

                };
                needsSave = true;
            }

            if (Configuration.ProtectiveClothing?.Count == 0)
            {
                Configuration.ProtectiveClothing = new Dictionary<string, List<ulong>>
                {
                    //hazzie
                    [ITEM_HAZMAT_SUIT] = null,
                    [ITEM_SCIENTIST_SUIT_HEAVY] = null,
                    [ITEM_SCIENTIST_SUIT] = null,
                    [ITEM_SCIENTIST_SUIT_PEACEKEEPER] = null,
                    [ITEM_SPACESUIT] = null,
                    [ITEM_FROGBOOTS] = null,
                    [ITEM_NOMAD_SUIT] = null,
                    [ITEM_PANTS] = new List<ulong>
                    {
                        SKIN_PANTS_COBALT_ELECTRIC
                    },
                    [ITEM_HOODIE] = new List<ulong>
                    {
                        SKIN_HOODIE_COBALT_ELECTRIC,
                        SKIN_HOODIE_COBALT_ELECTRIC_NOGLOW
                    }
                };

                needsSave = true;
            }

            if (Configuration.Version != VERSION)
            {
                var oldVersion = Configuration.Version;

                if (oldVersion == "1.0.0")
                {
                    Instance.PrintWarning($"Looks like you're updating from 1.0.0 to {VERSION}. The new version replaces dummy generator prefabs from static to deployed. The plugin will now locate all static generators on the map that have been marked for internal use by PowerGrid and replace them with the deployed ones...");

                    var count = 0;

                    foreach (var generator in BaseNetworkable.serverEntities.OfType<ElectricGenerator>())
                    {
                        if (generator.OwnerID != 0)
                        {
                            continue;
                        }

                        if (generator.PrefabName != PREFAB_GENERATOR_OLD)
                        {
                            continue;
                        }

                        if (!IsMarkedInternal(generator))
                        {
                            continue;
                        }

                        var pos = generator.transform.position;
                        var rot = generator.transform.eulerAngles;

                        generator.Kill(BaseNetworkable.DestroyMode.None);

                        var newGenerator = SpawnSpecial<ElectricGenerator>(PREFAB_GENERATOR_NEW, pos, rot, true);

                        count++;

                    }
                    if (count == 0)
                    {
                        Instance.PrintWarning("No static generators found that met the criteria, nothing replaced.");
                    }
                    else
                    {
                        Instance.PrintWarning($"Replaced {count} static generators with deployed ones.");
                    }

                    Configuration.Version = VERSION;
                    needsSave = true;
                }

            }

            if (needsSave)
            {
                SaveConfigData();
            }
        }
        private void RestoreDefaultConfig()
        {
            PrintWarning("Generating default config...");
            Configuration = new ConfigData();

            SaveConfigData();
        }

        private void LoadConfigData()
        {
            PrintWarning("Loading configuration file...");
            try
            {
                Configuration = Config.ReadObject<ConfigData>();
                PrintWarning("Success.");
            }
            catch (Exception e)
            {
                Configuration = new ConfigData();
                PrintWarning($"Loading failed: \n{e.Message}\n{e.StackTrace}\nGenerating new config file...");

            }
            SaveConfigData();

        }
        private void SaveConfigData()
        {
            PrintWarning("Saving config...");
            Config.WriteObject(Configuration, true);
        }

        public class InteractiveConfigValue
        {
            private Func<object> getter;
            private Action<object> setter;

            private Func<object, bool> validator;
            public Func<object, string> formatter;
            private Func<object, object> parser;

            public Type valueType;

            private double lowerLimit;
            private double upperLimit;

            //name also identifies it in the dic
            public string name;
            public string description;

            public InteractiveConfigValue(string name, string description, Func<object> getter, Action<object> setter, Type validatorType = null, double lowerLimit = double.NegativeInfinity, double upperLimit = double.PositiveInfinity)
            {
                this.getter = getter;
                this.setter = setter;
                this.name = name;
                this.description = description;
                this.lowerLimit = lowerLimit;
                this.upperLimit = upperLimit;

                formatter = FormatDefault;

                if (validatorType == null || validatorType == typeof(bool))
                {
                    validator = ValidateBool;
                    parser = ParseBool;
                    formatter = FormatBool;
                }
                else if (validatorType == typeof(float))
                {
                    validator = ValidateFloat;
                    parser = ParseFloat;
                }
                else if (validatorType == typeof(int))
                {
                    validator = ValidateInt;
                    parser = ParseInt;
                }
                else if (validatorType == typeof(ulong))
                {
                    validator = ValidateUlong;
                    parser = ParseUlong;
                }
                else if (validatorType == typeof(string))
                {
                    validator = ValidateString;
                    parser = ParseString;
                    formatter = FormatString;
                }

                valueType = validatorType;
            }

            public object GetSet
            {
                get
                {
                    return getter();
                }
                set
                {
                    //validate first
                    if (validator(value))
                    {
                        ReusableObject = parser(value);
                        setter(ReusableObject);
                        //tell the players

                        TellMessage(null, MSG(MSG_VALGP_HAS_BEEN_SET, null, name, formatter(ReusableObject)), true);
                        Instance.SaveConfigData();

                    }
                    else
                    {
                        ReusableString = upperLimit != double.PositiveInfinity || lowerLimit != double.NegativeInfinity ? $"The value for {name} is either too low or too high. Try {FormatAcceptable()}" : $"Incorrect value for {name}. You need to enter {FormatAcceptable()}";

                        TellMessage(null, $"{ReusableString} \nThe value remains as {formatter(getter())}.");
                    }

                }
            }

            public string FormatAcceptable()
            {
                return valueType == typeof(bool) ? "logical values (<color=green>true</color> or <color=red>false</color>)" : valueType == typeof(string) ? FormatStrings() : FormatNumericLimits();
            }

            private string FormatNumericLimits()
            {
                ReusableString2 = valueType == typeof(float) ? "<color=green>fractions (like 1.2345)</color>" : "<color=purple>integers (like 12345)</color>";

                return $"{ReusableString2} between <color=red>{lowerLimit.ToString("0.00")}</color> and <color=blgp>{upperLimit.ToString("0.00")}";
            }

            private string FormatStrings()
            {
                return "hexadecimal numbers WITHOUT preceeding # (like 3db4b3)";
                //return "strings (like ThisIsAString) - if they have a space, in quotes (like \"This Is A String\")";
            }

            private string FormatString(object value)
            {
                return $"<color=yellow>{value?.ToString() ?? "NULL"}</color>";
            }

            private string FormatEnumCargo(object value)
            {
                return $"<color=red>{value.ToString() ?? "NULL"}</color>";
            }

            private string FormatBool(object value)
            {
                return value.Equals(true) ? "<color=green>true</color>" : "<color=red>false</color>";
            }

            private object ParseBool(object value)
            {
                ReusableString2 = value.ToString().ToLower();

                ReusableBool2 = !(ReusableString2.Contains("f") || ReusableString2.Contains("0") || ReusableString2.Contains("no"));

                return ReusableBool2;
            }
            
            private bool ValidateBool(object value)
            {
                //whatever it is, it can be always treated as bool
                return true;
            }

            private object ParseFloat(object value)
            {
                //it's already in ReusableFloat! neat, huh.
                //since we're using it, it must've been validated, so it's still there eh.
                return ReusableFloat2;
            }

            private object ParseString(object value)
            {
                return ReusableString;
            }

            private object ParseInt(object value)
            {
                return ReusableInt2;
            }

            private object ParseUlong(object value)
            {
                return ReusableUlong2;
            }

            private bool ValidateFloat(object value)
            {
                if (float.TryParse(value.ToString(), out ReusableFloat2))
                {
                    return (ReusableFloat2 >= lowerLimit && ReusableFloat2 <= upperLimit);
                }
                else return false;
            }

            private bool ValidateInt(object value)
            {
                if (int.TryParse(value.ToString(), out ReusableInt2))
                {
                    return (ReusableInt2 >= lowerLimit && ReusableInt2 <= upperLimit);
                }
                else return false;
            }

            private bool ValidateUlong(object value)
            {
                if (ulong.TryParse(value.ToString(), out ReusableUlong2))
                {
                    return (ReusableUlong2 >= lowerLimit && ReusableUlong2 <= upperLimit);
                }
                else return false;
            }

            private bool ValidateString(object value)
            {
                ReusableString = value.ToString();

                if (ReusableString.ToLower() == "null")
                {
                    ReusableString = null;
                }
                return true;
            }

            private bool ValidateEnumCargo(object value)
            {
                ReusableString = value.ToString().ToLower();

                if (ReusableString == "" || ReusableString == string.Empty)
                {
                    return false;
                }

                if (!(ReusableString.Contains("warn") || ReusableString.Contains("prevent") || ReusableString.Contains("none")))
                {
                    return false;
                }

                return true;

            }

            private string FormatDefault(object value)
            {
                return $"<color=#00FFFF>{value.ToString()}</color>";
            }

        }

        private static Dictionary<string, InteractiveConfigValue> ConfigValues = null;

        private static void AddInteractiveConfigValue(string name, string description, Func<object> getter, Action<object> setter, Type validator = null, double lowerLimit = double.NegativeInfinity, double upperLimit = double.PositiveInfinity)
        {
            ConfigValues.Add(name, new InteractiveConfigValue(name, description, getter, setter, validator, lowerLimit, upperLimit));
        }

        private static void GenerateAllInteractiveConfigValues()
        {
            ConfigValues = new Dictionary<string, InteractiveConfigValue>();

            AddInteractiveConfigValue("GridConstantPower", $"If set to true, the Grid will always produce electricity at its peak (100%), 24 hours a day.", () => Instance.Configuration.GridConstantPower, val => { Instance.Configuration.GridConstantPower = (bool)val; }, typeof(bool));

            AddInteractiveConfigValue("GridProductionStartAtHour", $"The hour of the day when the power production starts climbing up from 0", () => Instance.Configuration.GridProductionStartAtHour, val => { Instance.Configuration.GridProductionStartAtHour = (float)val; }, typeof(float), 0, 24);

            AddInteractiveConfigValue("GridProductionEndAtHour", $"The hour of the day when the power production settles back at 0", () => Instance.Configuration.GridProductionEndAtHour, val => { Instance.Configuration.GridProductionEndAtHour = (float)val; }, typeof(float), 0, 24);

            AddInteractiveConfigValue("StreetlightsConstantPower", $"If set to true, the Streetlights will be on 24 hours a day.", () => Instance.Configuration.StreetlightsConstantPower, val => { Instance.Configuration.StreetlightsConstantPower = (bool)val; }, typeof(bool));

            AddInteractiveConfigValue("StreetlightsTurnOnAtHour", $"The hour of the day when the street lights turn on", () => Instance.Configuration.StreetlightsTurnOnAtHour, val => { Instance.Configuration.StreetlightsTurnOnAtHour = (float)val; }, typeof(float), 0, 24);

            AddInteractiveConfigValue("StreetlightsTurnOffAtHour", $"The hour of the day when the street lights turn off", () => Instance.Configuration.StreetlightsTurnOffAtHour, val => { Instance.Configuration.StreetlightsTurnOffAtHour = (float)val; }, typeof(float), 0, 24);

            AddInteractiveConfigValue("StreetlightsReliability", $"The reliability of the streetlight. The less it is, the more often it will flicker. At 1, which represents 100%, it never flickers.", () => Instance.Configuration.StreetlightsReliability, val => { Instance.Configuration.StreetlightsReliability = (float)val; }, typeof(float), 0F, 1F);

            AddInteractiveConfigValue("StreetlightsFlickerLengthMin", $"The minimum random length of the flicker, in seconds", () => Instance.Configuration.StreetlightsFlickerLengthMin, val => { Instance.Configuration.StreetlightsFlickerLengthMin = (float)val; }, typeof(float), 0F, 10F);

            AddInteractiveConfigValue("StreetlightsFlickerLengthMax", $"The maximum random length of the flicker, in seconds", () => Instance.Configuration.StreetlightsFlickerLengthMax, val => { Instance.Configuration.StreetlightsFlickerLengthMax = (float)val; }, typeof(float), 0F, 10F);

            AddInteractiveConfigValue("GeneratorChancePowerlineFunctional", $"The chance that a valid Power Line Pole will be made functional during generation. 0 represents 0%, 0.5 represents 50%, 1 represents 100%", () => Instance.Configuration.GeneratorChancePowerlineFunctional, val => { Instance.Configuration.GeneratorChancePowerlineFunctional = (float)val; }, typeof(float), 0, 1);

            AddInteractiveConfigValue("FuseRequired", $"If set to false, the Transformers won't need Fuses to produce power, just the right time of the day (if power is not 24/7)", () => Instance.Configuration.FuseRequired, val => { Instance.Configuration.FuseRequired = (bool)val; }, typeof(bool));

            AddInteractiveConfigValue("BuildingBlockPreventsButtonPress", $"If set to to true, if there's any Tool Cupboards in the range of the Transformer, you need to be authorised on all of them to open the Transformer GUI", () => Instance.Configuration.BuildingBlockPreventsButtonPress, val => { Instance.Configuration.BuildingBlockPreventsButtonPress = (bool)val; }, typeof(bool));

            AddInteractiveConfigValue("GeneratorInitialLevelMin", $"The lower limit for the random Tech Trash level of valid Power Line Poles during generation", () => Instance.Configuration.GeneratorInitialLevelMin, val => { Instance.Configuration.GeneratorInitialLevelMin = (int)val; }, typeof(int), 0, 1000);

            AddInteractiveConfigValue("GeneratorInitialLevelMax", $"The upper limit for the random Tech Trash level of valid Power Line Poles during generation", () => Instance.Configuration.GeneratorInitialLevelMax, val => { Instance.Configuration.GeneratorInitialLevelMax = (int)val; }, typeof(int), 0, 1000);

            AddInteractiveConfigValue("GeneratorInitialOutletsMin", $"The lower limit for the random number of Outlets of valid Power Line Poles during generation", () => Instance.Configuration.GeneratorInitialOutletsMin, val => { Instance.Configuration.GeneratorInitialOutletsMin = (int)val; }, typeof(int), 0, 4);

            AddInteractiveConfigValue("GeneratorInitialOutletsMax", $"The upper limit for the random number of Outlets of valid Power Line Poles during generation", () => Instance.Configuration.GeneratorInitialOutletsMax, val => { Instance.Configuration.GeneratorInitialOutletsMax = (int)val; }, typeof(int), 0, 4);

            AddInteractiveConfigValue("PowerlinePowerPerTechTrash", $"How much RWs at peak hours are provided per 1 Tech Trash Level upgrade", () => Instance.Configuration.PowerlinePowerPerTechTrash, val => { Instance.Configuration.PowerlinePowerPerTechTrash = (int)val; }, typeof(int), 0, 1000);

            AddInteractiveConfigValue("PowerlineMaxTechTrashLevel", $"The maximum level that a Transformer can be upgraded to", () => Instance.Configuration.PowerlineMaxTechTrashLevel, val => { Instance.Configuration.PowerlineMaxTechTrashLevel = (int)val; }, typeof(int), 0, 1000);

            AddInteractiveConfigValue("PowerlineFuseDurationSeconds", $"How long (in seconds) a brand new Fuse inserted in the Transformer Fusebox will last", () => Instance.Configuration.PowerlineFuseDurationSeconds, val => { Instance.Configuration.PowerlineFuseDurationSeconds = (float)val; }, typeof(float), 0, float.PositiveInfinity);

        }
        #endregion

        #region CONFIG PERMISSION PROFILES
        public class PermissionProfile
        {
            public string PermissionRequired = "";

            public bool GridCanDeployLadder = true;
            public bool GridCanDeployRootCombiners = true;
            public bool GridCanConnectDisconnect = true;
            public bool GridCanPressButton = true;
            public bool GridCanUpgrade = true;

            public float GridDangerousWireElectricutionChance = 0.1F;

            public bool HangingXmasLights = true;
            public bool HangingWiresAndHoses = true;

            public int SubdivisionsPreview = 10;
            public int SubdivisionsFinal = 50;
            public float SlackMax = 5F;

        }

        private PermissionProfile AdminPermissionProfile()
        {
            return new PermissionProfile
            {
                PermissionRequired = PERM_ADMIN,
                HangingWiresAndHoses = true,
                HangingXmasLights = true,
                SlackMax = 20F,
                GridDangerousWireElectricutionChance = 0F,
            };
        }

        private PermissionProfile DefaultPermissionProfile()
        {
            return new PermissionProfile
            {
                PermissionRequired = "default",
                HangingWiresAndHoses = false,
                HangingXmasLights = false,
                SlackMax = 5F,
            };
        }

        private PermissionProfile VIPPermissionProfile()
        {
            return new PermissionProfile
            {
                PermissionRequired = PERM_VIP1,
                HangingWiresAndHoses = true,
                HangingXmasLights = true,
                SlackMax = 10F,
            };
        }
        private static PermissionProfile GetPermissionProfile(BasePlayer player)
        {
            foreach (var prof in Instance.Configuration.PermissionProfiles)
            {
                if (prof.Key == "default") continue;

                if (prof.Key == PERM_ADMIN)
                {
                    if (HasAdminPermission(player))
                    {
                        return prof.Value;
                    };
                }

                if (Instance.permission.UserHasPermission(player.UserIDString, prof.Key))
                {
                    return prof.Value;
                }
            }

            //we got here so that means nothing found.

            if (!Instance.Configuration.PermissionProfiles.ContainsKey("default"))
            {
                //oops, data must be fucked.
                Instance.Configuration.PermissionProfiles.Add("default", Instance.DefaultPermissionProfile());
                Instance.SaveConfigData();
            }

            return Instance.Configuration.PermissionProfiles["default"];
        }
        #endregion

        #region REUSABLES
        private static object ReusableObject;
        private static bool ReusableBool;
        private static string ReusableString;
        private static float ReusableFloat;

        private static bool ReusableBool2;

        private static string ReusableString2;
        private static float ReusableFloat2;
        private static int ReusableInt2;
        private static ulong ReusableUlong2;
        #endregion

        #region DATA
        public StoredData Data;

        public void ProcessData()
        {
            bool needsSave = false;

            if (NewSave || Data.PowerlineData == null || Data.PowerlineData?.Count == 0)
            {
                Instance.PrintError($"Fresh wipe or empty data detected, clearing old data, locating powerline poles and creating data entries for them...");

                Data.PowerlineData = new Hash<uint, SerializablePowerlinePole>();
                Data.PlayerWireConfigs = new Hash<ulong, MultiToolConfig>();

                var removed = TryEmergencyCleanup();

                if (removed > 0)
                {
                    Instance.PrintWarning($"Found and removed {removed} leftover entities");
                } 

                //iterate over everything to get positions

                //take chance of occurence into account

                string prefabName;
                GameObject prefabGameObject;

                uint validPolesFound = 0;
                uint totalPolesTurned = 0;

                SerializablePowerlinePole currentDataPowerPole;

                foreach (ProtoBuf.PrefabData entry in World.Serialization.world.prefabs)
                {
                    prefabName = StringPool.Get(entry.id);

                    if (prefabName != PREFAB_POWERLINE_POLE_LIGHTS_YES && prefabName != PREFAB_POWERLINE_POLE_LIGHTS_NO)
                    {
                        continue;
                    }

                    prefabGameObject = GameManager.server.FindPrefab(prefabName);

                    if (prefabGameObject == null)
                    {
                        continue;
                    }

                    if (IsPlacementBlockedAt(entry.position))
                    {
                        continue;
                    }

                    validPolesFound++;

                    //maybe skip it - what does the config chance say about it?

                    if (UnityEngine.Random.Range(0F, 1F) > Configuration.GeneratorChancePowerlineFunctional)
                    {
                        continue;
                    }

                    int outlets = Mathf.Clamp(UnityEngine.Random.Range(Configuration.GeneratorInitialOutletsMin, Configuration.GeneratorInitialOutletsMax), 0, 4);

                    currentDataPowerPole = new SerializablePowerlinePole
                    {
                        Uid = validPolesFound,
                        TechTrashInside = 1, //use config value
                        OutletStates = new bool[4]
                        {
                            outlets > 0,
                            outlets > 1,
                            outlets > 2,
                            outlets > 3,
                        },
                        Transform = new SerializableTransform
                        {
                            Position = new SerializableVector3(entry.position),
                            Rotation = new SerializableVector3(entry.rotation)
                        }
                    };

                    Data.PowerlineData.Add(validPolesFound, currentDataPowerPole);
                    totalPolesTurned++;

                }

                if (validPolesFound == 0)
                {
                    Instance.PrintError("ERROR: Your map doesn't seem to contain any valid, non - placement blocked Power Line Poles. Please review your map.");
                    Data.PowerlineData = null;
                }
                else
                {
                    if (totalPolesTurned == 0)
                    {
                        Instance.PrintWarning($"WARNING: Although {validPolesFound} valid Powerline Poles have been found on your map, none of them were lucky enough to be turned into functional ones. Please review your Generation chance settings in the config and maybe crank it up. OR, reload the plugin and hope for better random results next time.");
                        Data.PowerlineData = null;
                    }
                    else
                    {
                        Instance.PrintWarning($"Found {validPolesFound} valid (non-placement blocked) Powerline Poles and saved {totalPolesTurned} of them to data as functional.");
                    }

                }

                needsSave = true;
            }
            //powerline pole helpers...

            int spawnedHelpers = 0;

            foreach (var dataEntry in Data.PowerlineData)
            {
                PowerlinePoleHelper.AddHelperPowerlinePole(dataEntry.Value);
                spawnedHelpers++;
            }

            if (spawnedHelpers > 0)
            {
                Instance.PrintWarning($"Successfully spawned {spawnedHelpers} functional powerlines and their entities");
            }

            if (needsSave)
            {
                SaveData();
            }

        }

        public void LoadData()
        {
            PrintWarning("Loading data...");

            try
            {
                Data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                PrintWarning("Corrupt data, generating default one.");
                NewData();
            }

            if (Data == null)
            {
                PrintWarning("Null data, generating default one.");
                NewData();
            }
        }
        public void SaveData()
        {
            PrintWarning("Saving data...");

            Interface.Oxide.DataFileSystem.WriteObject(Name, Data);

            Data.Dirty = false;
        }

        public void NewData()
        {
            Data = new StoredData();
            SaveData();
        }

        public class StoredData
        {
            public Hash<uint, SerializablePowerlinePole> PowerlineData = new Hash<uint, SerializablePowerlinePole>();
            public Hash<ulong, MultiToolConfig> PlayerWireConfigs = new Hash<ulong, MultiToolConfig>();

            [JsonIgnore]
            public bool Dirty = false;
        }

        public class SerializableVector3
        {
            public float PosX;
            public float PosY;
            public float PosZ;

            [JsonConstructor]
            public SerializableVector3()
            {
                PosX = 0F;
                PosY = 0F;
                PosY = 0F;
            }

            public SerializableVector3(Vector3 vector3)
            {
                PosX = vector3.x;
                PosY = vector3.y;
                PosZ = vector3.z;
            }

            public SerializableVector3(float x, float y, float z)
            {
                PosX = x;
                PosY = y;
                PosZ = z;
            }

            public static implicit operator Vector3(SerializableVector3 vector3)
            {
                return new Vector3(vector3.PosX, vector3.PosY, vector3.PosZ);
            }

        }
        public class SerializableTransform
        {
            public SerializableVector3 Position;
            public SerializableVector3 Rotation;
        }

        public class SerializablePole
        {
            public uint Uid;
            public SerializableTransform Transform;
        }

        public class SerializablePowerlinePole : SerializablePole
        {
            //we will identify powerline poles by their order

            public bool[] OutletStates;

            public int TechTrashInside = 0;

            [JsonIgnore]
            public bool Dirty
            {
                set
                {
                    Instance.Data.Dirty = true;
                }
            }
        }

        public class MultiToolConfig
        {
            public float Slack = 0.5F;

            [JsonIgnore]
            public bool Dirty
            {
                set
                {
                    Instance.Data.Dirty = true;
                }
            }
        }

        #endregion

        #region LANG
        public const string MSG_CHAT_PREFIX = "MSG_CHAT_PREFIX";

        public const string MSG_VALGP_HAS_BEEN_SET = "MSG_VALGP_HAS_BEEN_SET";
        public const string MSG_CFG_RUNDOWN_FORMAT = "MSG_CFG_RUNDOWN_FORMAT";
        public const string MSG_CFG_DETAILS_FORMAT = "MSG_CFG_DETAILS_FORMAT";
        public const string MSG_CFG_NO_SETTING_FOUND = "MSG_CFG_NO_SETTING_FOUND";
        public const string MSG_CFG_DEFAULT = "MSG_CFG_DEFAULT";
        
        public const string MSG_POWERLINE_GUI_TITLE = "MSG_POWERLINE_GUI_TITLE";
        
        public const string MSG_POWERLINE_STATUS_NO_OUTLETS = "MSG_POWERLINE_STATUS_NO_OUTLETS";
        public const string MSG_POWERLINE_STATUS_NO_TECHTRASH = "MSG_POWERLINE_STATUS_NO_TECHTRASH";
        public const string MSG_POWERLINE_STATUS_NO_FUSE = "MSG_POWERLINE_STATUS_NO_FUSE";

        public const string MSG_POWERLINE_POWER_CONSTANT = "MSG_POWERLINE_POWER_CONSTANT";
        public const string MSG_POWERLINE_POWER_BETWEEN = "MSG_POWERLINE_POWER_BETWEEN";
        public const string MSG_POWERLINE_POWER_RECEIVING = "MSG_POWERLINE_POWER_RECEIVING";

        public const string MSG_POWERLINE_FUSE_WILL_LAST_FOR = "MSG_POWERLINE_FUSE_WILL_LAST_FOR";

        public const string MSG_POWERLINE_TIME_FORMAT_HOUR_MINUTE = "MSG_POWERLINE_TIME_FORMAT_OCLOCK";
        public const string MSG_POWERLINE_TIME_FORMAT_HOUR_MINUTE_SECOND = "MSG_POWERLINE_TIME_FORMAT_FULL";

        public const string MSG_POWERLINE_BODY_OUTLET_CURRENTLY_ROUTING = "MSG_POWERLINE_BODY_OUTLET_CURRENTLY_ROUTING";
        public const string MSG_POWERLINE_BODY_OUTLET_INSERT = "MSG_POWERLINE_BODY_OUTLET_INSERT";
        public const string MSG_POWERLINE_BODY_OUTLET_ALL_THERE = "MSG_POWERLINE_BODY_OUTLET_ALL_THERE";
        public const string MSG_POWERLINE_BODY_TECHTRASH = "MSG_POWERLINE_BODY_TECHTRASH";
        public const string MSG_POWERLINE_BODY_FUSE_TIME_LEFT = "MSG_POWERLINE_BODY_FUSE";
        public const string MSG_POWERLINE_BODY_FUSE_WILL_LAST = "MSG_POWERLINE_BODY_FUSE_WILL_LAST";
        public const string MSG_CANT_LOOT_FUSEBOX = "MSG_CANT_LOOT_FUSEBOX";

        public const string MSG_CANT_PRESS_TRANSFORMER_BUTTON = "MSG_CANT_PRESS_TRANSFORMER_BUTTON";
        public const string MSG_SLOT_NAME_INTERNAL = "MSG_SLOT_NAME_INTERNAL";
        public const string MSG_SLOT_NAME_GRID = "MSG_SLOT_NAME_GRID";
        public const string MSG_CANT_CONNECT_DISCONNECT_GRID = "MSG_CANT_CONNECT_DISCONNECT_GRID";
        public const string MSG_CANT_DEPLOY_LADDER = "MSG_CANT_DEPLOY_LADDER";
        public const string MSG_CANT_UPGRADE = "MSG_CANT_UPGRADE";
        public const string MSG_POWERLINE_BODY_FUSE_NOT_REQUIRED = "MSG_POWERLINE_BODY_FUSE_NOT_REQUIRED";
        public const string MSG_CANT_PRESS_BUILDING_BLOCK = "MSG_CANT_PRESS_BUILDING_BLOCK";
        public const string MSG_PLACEHOLDER_29 = "MSG_PLACEHOLDER_29";
        public const string MSG_PLACEHOLDER_30 = "MSG_PLACEHOLDER_30";

        private static Dictionary<string, string> LangMessages = new Dictionary<string, string>
        {
            [MSG_CHAT_PREFIX] = $"<color=#57ff77>[Grid Power]</color>",
            [MSG_VALGP_HAS_BEEN_SET] = "Config value <color=yellow>{0}</color> has been set to {1} by an admin.",
            [MSG_CFG_DEFAULT] = "Here's the current settings. Type <color=yellow>/gp_cfg settingName</color> with no parameters to see the description, the current value and what the accepted arguments are. Type <color=yellow>/gp_cfg settingName acceptedValue</color> to change the setting.",
            [MSG_CFG_RUNDOWN_FORMAT] = "/gp_cfg <color=yellow>{0}</color> (currently: {1})",
            [MSG_CFG_DETAILS_FORMAT] = "<color=green>{0}</color>:\n{1} ({2})\nThis value is currently set to: {3}\n",
            [MSG_CFG_NO_SETTING_FOUND] = "No setting with that name has been found. Type /gp_cfg to get a rundown.",

            [MSG_POWERLINE_GUI_TITLE] = "GRID TRANSFORMER",

            [MSG_POWERLINE_POWER_BETWEEN] = "Grid emits power from {0} to {1} (peak at {2})",
            [MSG_POWERLINE_POWER_RECEIVING] = "Receiving power from Grid at {0}% efficiency",
            [MSG_POWERLINE_POWER_CONSTANT] = "Grid emits power 24/7 at a 100% efficiency",

            [MSG_POWERLINE_FUSE_WILL_LAST_FOR] = "Each fuse inserted in the Fuse Box will last for approx. {0}",

            [MSG_POWERLINE_TIME_FORMAT_HOUR_MINUTE] = "{0}:{1}",

            [MSG_POWERLINE_TIME_FORMAT_HOUR_MINUTE_SECOND] = "{0}:{1}:{2}",

            [MSG_POWERLINE_STATUS_NO_TECHTRASH] = "No Tech Trash applied, insert some in the Fuse Box below",
            [MSG_POWERLINE_STATUS_NO_OUTLETS] = "No Root Combiners, insert some in the Fuse Box below",
            [MSG_POWERLINE_STATUS_NO_FUSE] = "No Fuse inside, insert one in the Fuse Box below",

            [MSG_POWERLINE_BODY_OUTLET_CURRENTLY_ROUTING] = "Currently routing {0} RW from Grid over {1} Outlets",

            [MSG_POWERLINE_BODY_OUTLET_INSERT] = "{0} out of 4 outlets present, insert Root Combiners to add more",
            [MSG_POWERLINE_BODY_OUTLET_ALL_THERE] = "4 out of 4 outlets present",


            [MSG_POWERLINE_BODY_TECHTRASH] = "Tech Trash level: {0} ({1} max level)\nPeak output: {2} RW ({3} RW at max level)\nEach Tech Trash inserted permanently increases peak output by {4} RW",
            [MSG_POWERLINE_BODY_FUSE_TIME_LEFT] = "Fuse condition: {0}%\nEstimated time left: {1}",
            [MSG_POWERLINE_BODY_FUSE_WILL_LAST] = "Each fresh fuse inside the transformer will last for approximately {0}",

            [MSG_CANT_LOOT_FUSEBOX] = "Can't loot the Fusebox. Is another plugin preventing it?",


            [MSG_SLOT_NAME_INTERNAL] = "INTERNAL SLOT",
            [MSG_SLOT_NAME_GRID] = "Grid Out {0}",

            [MSG_CANT_PRESS_TRANSFORMER_BUTTON] = "You don't have permission to open the Grid Transformer.",
            [MSG_CANT_CONNECT_DISCONNECT_GRID] = "You don't have permission to connect/disconnect to/from the Grid Transformer.",
            [MSG_CANT_DEPLOY_LADDER] = "You don't have permission to deploy ladders on Powerline poles.",
            [MSG_CANT_UPGRADE] = "You don't have permission to upgrade Grid Transformers with Tech Trash.",
            [MSG_POWERLINE_BODY_FUSE_NOT_REQUIRED] = "Fuses are not required on this server.",
            [MSG_CANT_PRESS_BUILDING_BLOCK] = "Cannot access the Transformer, you're building blocked",
            [MSG_PLACEHOLDER_29] = "Placeholder",
            [MSG_PLACEHOLDER_30] = "Placeholder",

        };

        private static string MSG(string msg, string userID = null, params object[] args)
        {
            if (args == null)
            {
                return Instance.lang.GetMessage(msg, Instance, userID);
            }
            else
            {
                return string.Format(Instance.lang.GetMessage(msg, Instance, userID), args);
            }

        }
        #endregion

        #region MONO
        //attach to empty game object, destroy at unload
        public class PowerGrid : CommonBehaviour
        {
            public static PowerGrid TheOnlyInstance;

            public float GlobalEfficiency = 0F;

            public bool GridProducing = false;

            public bool StreetlightsOn = false;

            public void Prepare()
            {
                UpdateRate = 1F;
                UpdateRateRandomization = 0.015F;
                UpdatePowerProduction();

                TheOnlyInstance = this;
            }

            public void UpdatePowerProduction()
            {
                if (Instance.Configuration.GridConstantPower)
                {
                    GridProducing = true;
                    GlobalEfficiency = 1F;
                    return;
                }

                GlobalEfficiency = GetGlobalPowerProduction(GetCurrentHour());
                GridProducing = GlobalEfficiency > 0F;

                StreetlightsOn = IsItTime(GetCurrentHour(), Instance.Configuration.StreetlightsTurnOnAtHour, Instance.Configuration.StreetlightsTurnOffAtHour);

            }

            public override void PerformUpdate()
            {
                UpdatePowerProduction();
            }

            void OnDestroy()
            {
                TheOnlyInstance = null;
            }
        }

        public class PowerlinePoleOutlet
        {
            public PowerlinePoleHelper PoleHelper;
            public int Index;
            public ElectricGenerator Generator;
            public ElectricalCombiner RootCombiner;

            public uint RootCombinerNetID;
            public uint GeneratorNetID;

            public bool Destroying = false;

            public void Prepare(PowerlinePoleHelper helper, int index, Item insertedItem = null)
            {
                Index = index;
                PoleHelper = helper;

                //summon battery and root combiner

                if (insertedItem != null)
                {
                    Generator = SpawnSpecialRelativeTo<ElectricGenerator>(PoleHelper.transform, PREFAB_GENERATOR_NEW, PowerlinePoleHelper.PowerlineSocketPositions[Index] + Vector3.down * 15F, Vector3.zero, true);

                    RootCombiner = SpawnSpecialRelativeTo<ElectricalCombiner>(PoleHelper.transform, PREFAB_ROOT_COMBINER, PowerlinePoleHelper.PowerlineSocketPositions[Index], PowerlinePoleHelper.PowerlineSocketRotations[Index], false);

                    RootCombiner.SetHealth(insertedItem.conditionNormalized * RootCombiner.MaxHealth());
                    RootCombiner.skinID = insertedItem.skin;

                    insertedItem.UseItem();
                    PlayCombinerEffect(RootCombiner.transform.position);
                }
                else
                {
                    Generator = SummonSpecialRelativeTo<ElectricGenerator>(PoleHelper.transform, ref PowerlinePoleHelper.TemporaryVisibilityList, PREFAB_GENERATOR_NEW, PowerlinePoleHelper.PowerlineSocketPositions[Index] + Vector3.down * 15F, Vector3.zero, true);
                    RootCombiner = SummonSpecialRelativeTo<ElectricalCombiner>(PoleHelper.transform, ref PowerlinePoleHelper.TemporaryVisibilityList, PREFAB_ROOT_COMBINER, PowerlinePoleHelper.PowerlineSocketPositions[Index], PowerlinePoleHelper.PowerlineSocketRotations[Index], false);

                }

                GeneratorNetID = Generator.net.ID;
                RootCombinerNetID = RootCombiner.net.ID;

                Generator.pickup.enabled = false;

                //RootCombiner.pickup.enabled = false;

                MarkIOEntitySlotSpecial(RootCombiner.outputs[0], RootCombiner, false, true, MSG(MSG_SLOT_NAME_GRID, null, Index + 1));// $"Grid Out {Index + 1}");

                MarkIOEntitySlotSpecial(RootCombiner.inputs[0], RootCombiner, true, true);
                MarkIOEntitySlotSpecial(RootCombiner.inputs[1], RootCombiner, true, true);

                PoleHelper.UpdateAllOutputs();

                EngageIO(RootCombiner, Generator, 0, 0, null, WireTool.WireColour.Default, true, false, true, true);


                //network out the battery cause sure why not
                /*
                Battery.limitNetworking = true;
                Battery.OnNetworkSubscribersLeave(Network.Net.sv.connections);
                */

                PowerlinePoleHelper.AllIOEntitiesToPowerlinePole.Add(RootCombinerNetID, PoleHelper);
                PowerlinePoleHelper.AllIOEntitiesToPowerlinePole.Add(GeneratorNetID, PoleHelper);
            }

            public void UpdateOutputPower(int newPower)
            {
                if (Generator == null)
                {
                    return;
                }

                if (Generator.electricAmount == newPower)
                {
                    return;
                }    

                Generator.electricAmount = newPower;// PoleHelper.Data.TechTrashInside*10;
                Generator.MarkDirtyForceUpdateOutputs();
                Generator.SendIONetworkUpdate();
                Generator.SendNetworkUpdateImmediate();
                Generator.UpdateNetworkGroup();

                Generator.SendChangedToRoot(true);
            }

            public void WhenOutletKilled()
            {
                //kill the batteries or at the very least network them back in?

                if (!Destroying)
                {
                    if (Generator != null)
                    {
                        if (!Generator.IsDestroyed)
                        {
                            Generator.Kill(BaseNetworkable.DestroyMode.None);
                            Destroying = true;
                        }
                    }

                    PowerlinePoleHelper.AllIOEntitiesToPowerlinePole.Remove(RootCombinerNetID);
                    PowerlinePoleHelper.AllIOEntitiesToPowerlinePole.Remove(GeneratorNetID);
                }
               /*
               if (!Destroying)
               {
                    if (RootCombiner != null)
                    {
                        if (!RootCombiner.IsDestroyed)
                        {
                            RootCombiner.Kill(BaseNetworkable.DestroyMode.None);
                            Destroying = true;
                        }
                    }
               }
               */
            }

            public void WhenBatteryKilled()
            {
                if (!Destroying)
                {
                    if (RootCombiner != null)
                    {
                        if (!RootCombiner.IsDestroyed)
                        {
                            RootCombiner.Kill(BaseNetworkable.DestroyMode.None);
                            Destroying = true;
                        }
                    }
                }
            }
        }

        public class PowerlinePoleGuiTransformerState
        {
            //decide on the final form of the shit displayed
            //put the encapsulators in lang and the replacements right here


            //what is taken from config?
            //hours of operation (put that in status when the grid is off)
            //peak hour
            //OR
            //constant power
            //max level
            //how much more RW per update
            //how long a fuse lasts

            //what do we wanna know per powerline?

            //current level
            //current max output
            //is the grid currently emitting?
            //health of current slots/or missing
            //output of current slots if not missing
            //is the fuse inside?
            //how much time left on the fuse

            public string TopStatusCurrent = "";
            public string TopStatusPrevious = "";

            public string OutletStatusCurrent = "";
            public string OutletStatusPrevious = "";

            public string TechTrashStatusCurrent = "";
            public string TechTrashStatusPrevious = "";

            public string FuseStatusCurrent = "";
            public string FuseStatusPrevious = "";

            public int WorkingOutletCountCurrent = 0;
            public int WorkingOutletCountPrevious = 0;

            public bool HasFuseInsideCurrent = false;
            public bool HasFuseInsidePrevious = true;

            public float FuseHealthCurrent = 0F;
            public float FuseHealthPrevious = 0F;

            public int TechTrashInsideCurrent = 0;
            public int TechTrashInsidePrevious = 0;

            public int PowerOutputMaxCurrent = 0;
            public int PowerOutputMaxPrevious = 0;


            public string StatusColor;

            public PowerlinePoleHelper Helper;

            public PowerlinePoleGuiTransformerState(PowerlinePoleHelper helper)
            {
                Helper = helper;
                StatusColor = ColorPalette.RustyGrey.unityColorString + " 0.5";
            }

            public bool UpdateTopStatus()
            {
                TopStatusPrevious = TopStatusCurrent;

                WorkingOutletCountPrevious = WorkingOutletCountCurrent;
                WorkingOutletCountCurrent = Helper.GetWorkingOutletCount();

                TechTrashInsidePrevious = TechTrashInsideCurrent;
                TechTrashInsideCurrent = Helper.Data.TechTrashInside;

                HasFuseInsidePrevious = HasFuseInsideCurrent;
                HasFuseInsideCurrent = Helper.HasFuseInside();

                if (WorkingOutletCountCurrent <= 0)
                {
                    TopStatusCurrent = $"{MSG(MSG_POWERLINE_STATUS_NO_OUTLETS)}";
                    StatusColor = ColorPalette.RustyRed.unityColorString + " 0.5";
                    return TopStatusCurrent != TopStatusPrevious;
                }

                if (TechTrashInsideCurrent <= 0)
                {
                    TopStatusCurrent = $"{MSG(MSG_POWERLINE_STATUS_NO_TECHTRASH)}";
                    StatusColor = ColorPalette.RustyRed.unityColorString + " 0.5";
                    return TopStatusCurrent != TopStatusPrevious;
                }

                if (!HasFuseInsideCurrent)
                {
                    TopStatusCurrent = $"{MSG(MSG_POWERLINE_STATUS_NO_FUSE)}";
                    StatusColor = ColorPalette.RustyRed.unityColorString + " 0.5";
                    return TopStatusCurrent != TopStatusPrevious;
                }                

                if (Instance.Configuration.GridConstantPower)
                {
                    TopStatusCurrent = $"{MSG(MSG_POWERLINE_POWER_CONSTANT)}";
                    StatusColor = ColorPalette.RustyBlgp.unityColorString + " 0.5";
                    return TopStatusCurrent != TopStatusPrevious;
                }

                if (!PowerGrid.TheOnlyInstance.GridProducing)
                {
                    TopStatusCurrent = $"{MSG(MSG_POWERLINE_POWER_BETWEEN, null, GetFormattedHour(Instance.Configuration.GridProductionStartAtHour), GetFormattedHour(Instance.Configuration.GridProductionEndAtHour), GetFormattedHour((Instance.Configuration.GridProductionStartAtHour + Instance.Configuration.GridProductionEndAtHour)/2F))}";
                    StatusColor = ColorPalette.RustyOrange.unityColorString + " 0.5";
                    return TopStatusCurrent != TopStatusPrevious;
                }

                StatusColor = ColorPalette.RustyGreen.unityColorString + " 0.5";
                TopStatusCurrent = $"{MSG(MSG_POWERLINE_POWER_RECEIVING, null, (PowerGrid.TheOnlyInstance.GlobalEfficiency*100F).ToString("0.00"))}";
                return TopStatusCurrent != TopStatusPrevious;
            }

            public bool UpdateOutletBody()
            {
                OutletStatusPrevious = OutletStatusCurrent;
                OutletStatusCurrent = $"{(WorkingOutletCountCurrent == 4 ? MSG(MSG_POWERLINE_BODY_OUTLET_ALL_THERE) : MSG(MSG_POWERLINE_BODY_OUTLET_INSERT, null, WorkingOutletCountCurrent))}\n{MSG(MSG_POWERLINE_BODY_OUTLET_CURRENTLY_ROUTING, null, Helper.LocalPowerProductionCurrent, WorkingOutletCountCurrent)}";// MSG(MSG_POWERLINE_BODY_OUTLET_INSERT, null, );

                return OutletStatusPrevious != OutletStatusCurrent;
            }

            public bool UpdateTechTrashBody()
            {

                /*
                if (TechTrashInsideCurrent == TechTrashInsidePrevious)
                {
                    return false;
                }
                */

                PowerOutputMaxPrevious = PowerOutputMaxCurrent;
                PowerOutputMaxCurrent = TechTrashInsideCurrent * Instance.Configuration.PowerlinePowerPerTechTrash;

                TechTrashStatusPrevious = TechTrashStatusCurrent;

                var fabryluk = Instance.Configuration.PowerlinePowerPerTechTrash * Instance.Configuration.PowerlineMaxTechTrashLevel;

                TechTrashStatusCurrent = MSG(MSG_POWERLINE_BODY_TECHTRASH, null, TechTrashInsideCurrent, Instance.Configuration.PowerlineMaxTechTrashLevel, PowerOutputMaxCurrent, fabryluk, Instance.Configuration.PowerlinePowerPerTechTrash);




                return TechTrashStatusPrevious != TechTrashStatusCurrent;
            }

            public bool UpdateFuseBody()
            {
                /*
                if (HasFuseInsideCurrent == HasFuseInsidePrevious && FuseHealthCurrent == FuseHealthPrevious)
                {
                    return false;
                }
                */

                FuseStatusPrevious = FuseStatusCurrent;

                if (HasFuseInsideCurrent)
                {
                    FuseHealthPrevious = FuseHealthCurrent;

                    if (Instance.Configuration.FuseRequired)
                    {
                        FuseHealthCurrent = Helper.Fusebox.inventory.GetSlot(0)?.conditionNormalized ?? 0;
                        FuseStatusCurrent = MSG(MSG_POWERLINE_BODY_FUSE_TIME_LEFT, null, (FuseHealthCurrent * 100F).ToString("0.00"), GetFormattedTimeLeft(GetFuseTimeLeftSeconds(FuseHealthCurrent)));
                    }
                    else
                    {
                        FuseHealthCurrent = 1F;
                        FuseStatusCurrent = MSG(MSG_POWERLINE_BODY_FUSE_NOT_REQUIRED, null);
                    }


                }
                else
                {
                    FuseStatusPrevious = FuseStatusCurrent;
                    FuseStatusCurrent = MSG(MSG_POWERLINE_BODY_FUSE_WILL_LAST, null, GetFormattedTimeLeft(Instance.Configuration.PowerlineFuseDurationSeconds));
                }



                return FuseStatusPrevious != FuseStatusCurrent;
            }
        }

        //attach to an empty gameobject at position/rotation of a powerpole

        public class StreetLampHelper : CommonBehaviour
        {
            public static ListHashSet<StreetLampHelper> StreetLampHelperCache;

            public static Vector3 SPOTLIGHT_POSITION_RIGHT = new Vector3(2.438F, 11.436F, 1.415F);
            public static Vector3 POINTLIGHT_POSITION_RIGHT = new Vector3(2.438F, 11.136F, 1.415F);
            public static Vector3 SPOTLIGHT_ROTATION_RIGHT = new Vector3(90F, 60F, 0F);

            public static Vector3 SPOTLIGHT_POSITION_LEFT = new Vector3(-2.438F, 11.436F, 1.415F);
            public static Vector3 POINTLIGHT_POSITION_LEFT = new Vector3(-2.438F, 11.136F, 1.415F);
            public static Vector3 SPOTLIGHT_ROTATION_LEFT = new Vector3(90F, 300F, 0F);

            public CinematicEntity SpotlightRight = null;
            public CinematicEntity SpotlightLeft = null;

            public CinematicEntity PointlightRight = null;
            public CinematicEntity PointlightLeft = null;

            public bool IsOnCurrentRight = false;
            public bool IsOnPreviousRight = false;

            public bool IsOnCurrentLeft= false;
            public bool IsOnPreviousLeft = false;

            public bool IsFlickeringLeft = false;
            public bool IsFlickeringRight = false;

            public void Prepare()
            {
                UpdateRate = 1F;
                UpdateRateRandomization = 1F;
                StreetLampHelperCache.Add(this);

                SpotlightSpawnRight();
                SpotlightSpawnLeft();
                PointlightSpawnLeft();
                PointlightSpawnRight();

                TurnOffLeft();
                TurnOffRight();
            }

            public void TurnOnLeft()
            {
                if (Instance == null)
                {
                    return;
                }

                if (SpotlightLeft == null)
                {
                    return;
                }

                if (PointlightLeft == null)
                {
                    return;
                }

                if (PowerGrid.TheOnlyInstance.StreetlightsOn)
                {
                    TurnOn(SpotlightLeft);
                    TurnOn(PointlightLeft);
                }

                IsFlickeringLeft = false;
            }

            public void TurnOnRight()
            {
                if (Instance == null)
                {
                    return;
                }

                if (SpotlightRight == null)
                {
                    return;
                }

                if (PointlightRight == null)
                {
                    return;
                }

                if (PowerGrid.TheOnlyInstance.StreetlightsOn)
                {
                    TurnOn(SpotlightRight);
                    TurnOn(PointlightRight);
                }

                IsFlickeringRight = false;
            }

            public void TurnOffLeft()
            {
                TurnOff(SpotlightLeft);
                TurnOff(PointlightLeft);
            }

            public void TurnOffRight()
            {
                TurnOff(SpotlightRight);
                TurnOff(PointlightRight);
            }

            public void TurnOn(CinematicEntity light)
            {
                light.limitNetworking = false;
                light.OnNetworkSubscribersEnter(Network.Net.sv.connections);
                light.SendChildrenNetworkUpdateImmediate();
            }

            public void TurnOff(CinematicEntity light)
            {
                light.limitNetworking = true;
                light._limitedNetworking = true;
                light.OnNetworkSubscribersLeave(Network.Net.sv.connections);
                light.SendChildrenNetworkUpdateImmediate();
            }

            public override void PerformUpdate()
            {
                IsOnPreviousRight = IsOnCurrentRight;
                IsOnPreviousLeft = IsOnCurrentLeft;

                if (SpotlightLeft == null)
                {
                    SpotlightSpawnLeft();
                }

                if (SpotlightRight == null)
                {
                    SpotlightSpawnRight();
                }

                if (PointlightLeft == null)
                {
                    PointlightSpawnLeft();
                }

                if (PointlightRight == null)
                {
                    PointlightSpawnRight();
                }


                if (!IsFlickeringRight)
                {
                    if (PowerGrid.TheOnlyInstance.StreetlightsOn)
                    {
                        if (Instance.Configuration.StreetlightsReliability < 1F)
                        {
                            if (UnityEngine.Random.Range(0F, 1F) > Instance.Configuration.StreetlightsReliability)
                            {
                                FlickerRight();
                            }
                        }
                    }

                    if (!IsFlickeringRight)
                    {
                        if (Instance.Configuration.StreetlightsConstantPower)
                        {
                            IsOnCurrentRight = true;
                        }
                        else
                        {
                            IsOnCurrentRight = PowerGrid.TheOnlyInstance.StreetlightsOn;
                        }
                    }
                }

                if (!IsFlickeringLeft)
                {
                    if (PowerGrid.TheOnlyInstance.StreetlightsOn)
                    {
                        if (Instance.Configuration.StreetlightsReliability < 1F)
                        {
                            if (UnityEngine.Random.Range(0F, 1F) > Instance.Configuration.StreetlightsReliability)
                            {
                                FlickerLeft();
                            }
                        }
                    }

                    if (!IsFlickeringLeft)
                    {
                        if (Instance.Configuration.StreetlightsConstantPower)
                        {
                            IsOnCurrentLeft = true;
                        }
                        else
                        {
                            IsOnCurrentLeft = PowerGrid.TheOnlyInstance.StreetlightsOn;
                        }
                    }
                }

                if (IsOnCurrentRight != IsOnPreviousRight)
                {
                    if (IsOnCurrentRight)
                    {
                        TurnOnRight();
                        //turn on!
                    }
                    else
                    {
                        TurnOffRight();
                        //turn off!
                    }
                }

                if (IsOnCurrentLeft != IsOnPreviousLeft)
                {
                    if (IsOnCurrentLeft)
                    {
                        TurnOnLeft();
                        //turn on!
                    }
                    else
                    {
                        TurnOffLeft();
                        //turn off!
                    }
                }
            }

            public void FlickerLeft()
            {
                IsFlickeringLeft = true;
                TurnOffLeft();
                Invoke(nameof(TurnOnLeft), UnityEngine.Random.Range(Instance.Configuration.StreetlightsFlickerLengthMin, Instance.Configuration.StreetlightsFlickerLengthMax));
            }

            public void FlickerRight()
            {
                IsFlickeringRight = true;
                TurnOffRight();
                Invoke(nameof(TurnOnRight), UnityEngine.Random.Range(Instance.Configuration.StreetlightsFlickerLengthMin, Instance.Configuration.StreetlightsFlickerLengthMax));
            }

            private void PointlightSpawnRight()
            {
                PointlightRight = SpawnSpecialRelativeTo<CinematicEntity>(transform, PREFAB_POINTLIGHT, POINTLIGHT_POSITION_RIGHT, SPOTLIGHT_ROTATION_RIGHT, true);
                PointlightRight.enableSaving = false;
                IsFlickeringRight = false;
            }

            private void PointlightKillRight()
            {
                if (PointlightRight != null)
                {
                    if (!PointlightRight.IsDestroyed)
                    {
                        PointlightRight.Kill();
                        PointlightRight = null;
                    }
                }
            }

            private void PointlightSpawnLeft()
            {
                PointlightLeft = SpawnSpecialRelativeTo<CinematicEntity>(transform, PREFAB_POINTLIGHT, POINTLIGHT_POSITION_LEFT, SPOTLIGHT_ROTATION_LEFT, true);
                PointlightLeft.enableSaving = false;
                IsFlickeringLeft = false;
            }

            private void PointlightKillLeft()
            {
                if (PointlightLeft != null)
                {
                    if (!PointlightLeft.IsDestroyed)
                    {
                        PointlightLeft.Kill();
                        PointlightLeft = null;
                    }
                }
            }

            private void SpotlightSpawnRight()
            {
                SpotlightRight = SpawnSpecialRelativeTo<CinematicEntity>(transform, PREFAB_SPOTLIGHT, SPOTLIGHT_POSITION_RIGHT, SPOTLIGHT_ROTATION_RIGHT, true);
                SpotlightRight.SetFlag(FLAG_HIDE_MESH, true, false, true);
                SpotlightRight.enableSaving = false;
                IsFlickeringRight = false;
            }

            private void SpotlightKillRight()
            {
                if (SpotlightRight != null)
                {
                    if (!SpotlightRight.IsDestroyed)
                    {
                        SpotlightRight.Kill();
                        SpotlightRight = null;
                    }
                }
            }

            private void SpotlightSpawnLeft()
            {
                SpotlightLeft = SpawnSpecialRelativeTo<CinematicEntity>(transform, PREFAB_SPOTLIGHT, SPOTLIGHT_POSITION_LEFT, SPOTLIGHT_ROTATION_LEFT, true);
                SpotlightLeft.SetFlag(FLAG_HIDE_MESH, true, false, true);
                SpotlightLeft.enableSaving = false;
                IsFlickeringLeft = false;
            }

            private void SpotlightKillLeft()
            {
                if (SpotlightLeft != null)
                {
                    if (!SpotlightLeft.IsDestroyed)
                    {
                        SpotlightLeft.Kill();
                        SpotlightLeft = null;
                    }
                }
            }

            void OnDestroy()
            {
                PointlightKillLeft();
                PointlightKillRight();

                SpotlightKillRight();
                SpotlightKillLeft();
            }
        }

        //completely ephemeridal
        //saving enables for entities
        //all get forcibly killed on destroy (unload)

        public class PowerlinePoleHelper : CommonBehaviour
        {
            public static Hash<uint, PowerlinePoleHelper> PowerlinePoleHelperCache;

            public static Hash<uint, PowerlinePoleHelper> AllIOEntitiesToPowerlinePole;

            public static Hash<IOEntity.IOSlot, uint> HardwiredSlotToEntityNetID;

            public static Hash<IOEntity.IOSlot, uint> DangerousSlotToEntityNetID;

            public static Vector3 LADDER_BOTTOM_POSITION = new Vector3(0F, 4.333F, 0.15F);
            public static Vector3 LADDER_BOTTOM_ROTATION = Vector3.zero;

            public static Vector3 LADDER_TOP_POSITION = new Vector3(0F, 9F, 2F);
            public static Vector3 LADDER_TOP_ROTATION = new Vector3(3F, 0F, 0F);

            public static Vector3 BUTTON_POSITION = new Vector3(0F, 10.45F, 0.166F);
            public static Vector3 BUTTON_ROTATION = Vector3.zero;

            public static Vector3 FUSEBOX_POSITION = new Vector3(0F, -9F, 0F);
            public static Vector3 FUSEBOX_ROTATION = Vector3.zero;

            public static List<Vector3> PowerlineSocketPositions;
            public static List<Vector3> PowerlineSocketRotations;

            public static Hash<uint, PowerlinePoleOutlet> CombinerToPowerlinePoleOutlet;
            public static Hash<uint, PowerlinePoleHelper> ButtonToPowerlinePole;
            public static Hash<uint, PowerlinePoleHelper> FuseboxToPowerlinePole;

            //use the data one
            public SerializablePowerlinePole Data;

            public PressButton FuseboxButton = null;
            public ItemBasedFlowRestrictor Fusebox = null;

            public TeslaCoil Electricutor;
            public SphereEntity ElectricutorParent;
            public Vector3 ElectricutorParentOriginalPos;
            public ElectricGenerator ElectricutorGenerator;
            public Timer ElectricutorTimer;

            public uint FuseboxNetID;
            public uint ButtonNetID;
            public uint ElectricutorParentNetID;
            public uint ElectricutorGeneratorNetID;
            public uint ElectricutorNetID;

            public static List<BaseEntity> TemporaryVisibilityList;

            public PowerlinePoleOutlet[] Outlets = Enumerable.Repeat<PowerlinePoleOutlet>(null, 4).ToArray();

            public int LocalPowerProductionCurrent = 0;
            public int LocalPowerProductionPrevious = 0;

            public Hash<ulong, BasePlayer> GuiSubscribers = new Hash<ulong, BasePlayer>();

            public PowerlinePoleGuiTransformerState GuiState;

            public bool NeedsAnyUpdate = false;
            public bool NeedsStatusUpdate = false;
            public bool NeedsOutletBodyUpdate = false;
            public bool NeedsTechTrashBodyUpdate = false;
            public bool NeedsFuseBodyUpdate = false;

            public void Prepare(SerializablePowerlinePole data)
            {
                UpdateRate = 1F;
                UpdateRateRandomization = 0.05F;

                GuiState = new PowerlinePoleGuiTransformerState(this);

                Data = data;

                //tell the sphere to die in like a second
                                
                PowerlinePoleHelperCache.Add(Data.Uid, this);

                SummonEntities();
            }

            public override void PerformUpdate()
            {
                LocalPowerProductionPrevious = LocalPowerProductionCurrent;

                if (PowerGrid.TheOnlyInstance.GlobalEfficiency == 0F)
                {
                    LocalPowerProductionCurrent = 0;
                }
                else
                {
                    if (HasFuseInside())
                    {
                        LocalPowerProductionCurrent = Mathf.RoundToInt(Instance.Configuration.PowerlinePowerPerTechTrash * Data.TechTrashInside * Mathf.Clamp01(PowerGrid.TheOnlyInstance.GlobalEfficiency));
                    }
                    else
                    {
                        LocalPowerProductionCurrent = 0;
                    }
                }

                if (LocalPowerProductionPrevious != LocalPowerProductionCurrent)
                {
                    UpdateAllOutputs();
                }

                if (GuiSubscribers.Count == 0)
                {
                    return;
                }

                NeedsStatusUpdate = GuiState.UpdateTopStatus();

                NeedsTechTrashBodyUpdate = GuiState.UpdateTechTrashBody();

                NeedsOutletBodyUpdate = GuiState.UpdateOutletBody();

                NeedsFuseBodyUpdate = GuiState.UpdateFuseBody();


                NeedsAnyUpdate = NeedsStatusUpdate || NeedsOutletBodyUpdate || NeedsTechTrashBodyUpdate || NeedsFuseBodyUpdate;

                if (!NeedsAnyUpdate)
                {
                    return;
                }

                foreach (var smashedThatLikeButton in GuiSubscribers)
                {
                    if (NeedsStatusUpdate)
                    {
                        PowerlineTransformerGUI.ShowContainerStatus(smashedThatLikeButton.Value, this);
                    }

                    if (NeedsOutletBodyUpdate)
                    {
                        PowerlineTransformerGUI.ShowContainerBody1(smashedThatLikeButton.Value, this);
                    }

                    if (NeedsTechTrashBodyUpdate)
                    {
                        PowerlineTransformerGUI.ShowContainerBody2(smashedThatLikeButton.Value, this);
                    }

                    if (NeedsFuseBodyUpdate)
                    {
                        PowerlineTransformerGUI.ShowContainerBody3(smashedThatLikeButton.Value, this);
                    }
                }

            }

            public void UpdateAllOutputs()
            {
                for (var p = 0; p < Outlets.Length; p++)
                {
                    if (Outlets[p] == null)
                    {
                        continue;
                    }

                    Outlets[p].UpdateOutputPower(LocalPowerProductionCurrent);
                }
            }

            public void SummonEntities()
            {
                Vis.Entities(transform.position + Vector3.up * 5F, 20F, TemporaryVisibilityList, int.MaxValue);

                SummonElectricutor();
                SummonButton();
                SummonFusebox();

                for (var o = 0; o < Data.OutletStates.Length; o++)
                {
                    if (Data.OutletStates[o])
                    {
                    SummonOutlet(o);
                    }
                }
            }

            public void ElectricutePlayerToDeath(BasePlayer player, float duration = 3F)
            {
                //cancel current timer...
                if (ElectricutorTimer != null)
                {
                    ElectricutorTimer.Destroy();
                    ElectricutorTimer = null;
                }

                ElectricutorParent.transform.position = player.transform.position + Vector3.up * 1.5F;
                ElectricutorParent.SendNetworkUpdateImmediate();
                
                EngageIO(Electricutor, ElectricutorGenerator, 0, 0, null, WireTool.WireColour.Default, false, false, true, true);

                //player.Hurt(350F, Rust.DamageType.ElectricShock, player, false);

                ElectricutorTimer = Instance.timer.Once(duration, () =>
                {
                    if (ElectricutorParent == null)
                    {
                        return;
                    }
                    DisengageIO(Electricutor, ElectricutorGenerator, 0, 0, true);


                    ElectricutorParent.transform.position = ElectricutorParentOriginalPos;
                    ElectricutorParent.SendNetworkUpdateImmediate();

                    ElectricutorTimer = null;
                });

            }

            public void SummonElectricutor()
            {
                ElectricutorParent = SpawnSpecialRelativeTo<SphereEntity>(transform, PREFAB_SPHERE, Vector3.down * 12F, Vector3.zero, true);
                ElectricutorParentNetID = ElectricutorParent.net.ID;

                ElectricutorGenerator = SummonSpecialRelativeTo<ElectricGenerator>(transform, ref TemporaryVisibilityList, PREFAB_GENERATOR_NEW, new Vector3(0F, PowerlinePoleHelper.PowerlineSocketPositions[0].y - 15F, 0F), Vector3.zero, true);

                MarkIOEntitySlotSpecial(ElectricutorGenerator.outputs[0], ElectricutorGenerator, true, true);

                ElectricutorGeneratorNetID = ElectricutorGenerator.net.ID;

                Electricutor = SpawnSpecialParentedTo<TeslaCoil>(ElectricutorParent, PREFAB_TESLA, Vector3.zero, Vector3.zero, true);

                ElectricutorNetID = Electricutor.net.ID;

                MarkIOEntitySlotSpecial(Electricutor.inputs[0], Electricutor, true, true);

                MarkIOEntitySlotSpecial(ElectricutorGenerator.outputs[0], ElectricutorGenerator, true, true);


                Electricutor.pickup.enabled = false;
                Electricutor.targetTrigger.losEyes = null;
                ElectricutorParentOriginalPos = ElectricutorParent.transform.position;

                //that's just for the player nearby, the player electricuted directly gets damage instantly
                Electricutor.maxDamageOutput = 350F;
                Electricutor.powerToDamageRatio = 20F;

                Electricutor.SendNetworkUpdate();

                ElectricutorParent.LerpRadiusTo(0.05F, 300F);

                AllIOEntitiesToPowerlinePole.Add(ElectricutorParentNetID, this);
                AllIOEntitiesToPowerlinePole.Add(ElectricutorNetID, this);
                AllIOEntitiesToPowerlinePole.Add(ElectricutorGeneratorNetID, this);
            }

            public void SummonButton()
            {
                FuseboxButton = SummonSpecialRelativeTo<PressButton>(transform, ref TemporaryVisibilityList, PREFAB_BUTTON, BUTTON_POSITION, BUTTON_ROTATION, true);
                FuseboxButton.pickup.enabled = false;
                FuseboxButton.pressDuration = 1F;

                MarkIOEntitySlotSpecial(FuseboxButton.outputs[0], FuseboxButton, true, true);
                MarkIOEntitySlotSpecial(FuseboxButton.inputs[0], FuseboxButton, true, true);

                ButtonNetID = FuseboxButton.net.ID;

                ButtonToPowerlinePole.Add(ButtonNetID, this);
                AllIOEntitiesToPowerlinePole.Add(ButtonNetID, this);
            }

            public void SummonFusebox()
            {
                Fusebox = SummonSpecialRelativeTo<ItemBasedFlowRestrictor>(transform, ref TemporaryVisibilityList, PREFAB_FUSEBOX, FUSEBOX_POSITION, FUSEBOX_ROTATION, true);
                Fusebox.UpdateHasPower(10, 0);
                Fusebox.SetFlag(BaseEntity.Flags.On, true, false, true);

                MarkIOEntitySlotSpecial(Fusebox.outputs[0], FuseboxButton, true, true);
                MarkIOEntitySlotSpecial(Fusebox.inputs[0], FuseboxButton, true, true);

                Fusebox.inventory.SetOnlyAllowedItems(FuseboxAllowedItems);

                Fusebox.SendNetworkUpdateImmediate();

                //change the inventory's only allowed items etc.

                FuseboxNetID = Fusebox.net.ID;

                FuseboxToPowerlinePole.Add(FuseboxNetID, this);
                AllIOEntitiesToPowerlinePole.Add(FuseboxNetID, this);
            }

            public bool SummonOutlet(int index, Item fromItem = null)
            {
                //first remove existing
                if (Outlets[index] != null)
                {
                    return false;
                }

                Outlets[index] = new PowerlinePoleOutlet();
                Outlets[index].Prepare(this, index, fromItem);
                CombinerToPowerlinePoleOutlet.Add(Outlets[index].RootCombinerNetID, Outlets[index]);

                Data.OutletStates[index] = true;
                Data.Dirty = true;

                return true;
            }

            public void PlayerOpenedFusebox(BasePlayer player)
            {
                if (!GuiSubscribers.ContainsKey(player.userID))
                {
                    GuiSubscribers.Add(player.userID, player);
                }

                PowerlineTransformerGUI.PlayerOpenedFusebox(player, this);
            }

            public void PlayerClosedFusebox(BasePlayer player)
            {
                if (GuiSubscribers.ContainsKey(player.userID))
                {
                    GuiSubscribers.Remove(player.userID);
                }

                PowerlineTransformerGUI.PlayerClosedFusebox(player);
            }

            public int SomebodyTriedDeployingOutlet(Item fromItem)
            {
                int firstFreeFound = -1;
                var player = GetOwnerThing(fromItem.GetRootContainer()) as BasePlayer;

                //check if there's any more space to deploy. deploy on the first null found.


                for (var i=0; i< Outlets.Length; i++)
                {
                    if (Outlets[i] == null)
                    {
                        firstFreeFound = i;
                        break;
                    }
                }

                if (firstFreeFound == -1)
                {
                    return firstFreeFound;
                }

                if (player != null)
                {
                    if (!GetPermissionProfile(player).GridCanDeployRootCombiners)
                    {
                        TellMessage(player, MSG_CANT_UPGRADE);
                        return -1;
                    }
                }

                SummonOutlet(firstFreeFound, fromItem);

                return firstFreeFound;
            }

            public bool SomebodyTriedInsertingTechTrash(Item fromItem)
            {
                var player = GetOwnerThing(fromItem.GetRootContainer()) as BasePlayer;

                if (player != null)
                {
                    if (!GetPermissionProfile(player).GridCanUpgrade)
                    {
                        TellMessage(player, MSG_CANT_UPGRADE);
                        return false;
                    }
                }

                //check if we haven't exceeded max upgrade level?
                if (Data.TechTrashInside >= Instance.Configuration.PowerlineMaxTechTrashLevel)
                {
                    return false;
                }

                PlayTechTrashEffect(FuseboxButton.transform.position);

                fromItem.UseItem(1);

                Data.TechTrashInside += 1;
                Data.Dirty = true;

                return true;
            }

            public bool HasFuseInside()
            {
                if (!Instance.Configuration.FuseRequired)
                {
                    return true;
                }

                if (Fusebox == null)
                {
                    return false;
                }

                return Fusebox.HasPassthroughItem();
            }

            public int GetWorkingOutletCount()
            {
                int count = 0;
                for (var b=0; b<Data.OutletStates.Length; b++)
                {
                    count += Data.OutletStates[b] ? 1 : 0;
                }

                return count;
            }

            public ItemContainer.CanAcceptResult? CanItemBeAddedToFusebox(Item item)
            {
                if (item.info.shortname == ITEM_ROOTCOMBINER)
                {
                    if (!item.isBroken)
                    {
                        SomebodyTriedDeployingOutlet(item);
                    }
                    else
                    {
                        //electricute item container owner if it's a player?
                    }


                    return ItemContainer.CanAcceptResult.CannotAccept;
                }

                if (item.info.shortname == ITEM_TECHTRASH)
                {
                    SomebodyTriedInsertingTechTrash(item);
                    return ItemContainer.CanAcceptResult.CannotAccept;
                }

                return null;
            }

            public void WhenFuseInsideLosesCondition(Item fuseItem, ref float amount)
            {
                if (LocalPowerProductionCurrent == 0F)
                {
                    //don't consume
                    amount = 0F;
                }
                else
                {
                    amount *= NORMAL_FUSE_DURATION_SECONDS/Instance.Configuration.PowerlineFuseDurationSeconds;
                }
            }

            public void WhenButtonPressed(BasePlayer player)
            {
                if (Fusebox != null)
                {
                    if (!player.CanInteract())
                    {
                        return;
                    }

                    if (Instance.Configuration.BuildingBlockPreventsButtonPress)
                    {
                        if (player.IsBuildingBlocked())
                        {
                            TellMessage(player, MSG_CANT_PRESS_BUILDING_BLOCK);
                            return;
                        }
                    }

                    if (!GetPermissionProfile(player).GridCanPressButton)
                    {
                        TellMessage(player, MSG(MSG_CANT_PRESS_TRANSFORMER_BUTTON, player.UserIDString));
                        return;
                    }

                    if (!player.inventory.loot.StartLootingEntity(Fusebox, false))
                    {
                        TellMessage(player, MSG(MSG_CANT_LOOT_FUSEBOX));
                        return;
                    }
                        
                    //Fusebox.SetFlag(BaseEntity.Flags.Open, true);
                    player.inventory.loot.AddContainer(Fusebox.inventory);
                    player.inventory.loot.SendImmediate();
                    player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", Fusebox.lootPanelName);
                    Fusebox.SendNetworkUpdate();                    
                }
            }

            public bool WhenButtonKilled()
            {
                DestroyImmediate(this);

                return true;
            }

            public bool WhenOutletKilledByNetID(uint netID)
            {
                if (!CombinerToPowerlinePoleOutlet.ContainsKey(netID)) 
                {
                    return false;
                }

                return WhenOutletKilled(CombinerToPowerlinePoleOutlet[netID].Index);
            }

            public bool WhenOutletKilled(int index)
            {

                if (Outlets[index] == null)
                {
                    return false;
                }

                Outlets[index].WhenOutletKilled();

                CombinerToPowerlinePoleOutlet.Remove(Outlets[index].RootCombinerNetID);

                Outlets[index] = null;

                Data.OutletStates[index] = false;
                Data.Dirty = true;

                return true;

            }

            private void OnDestroy()
            {
                foreach (var player in GuiSubscribers)
                {
                    if (player.Value == null)
                    {
                        continue;
                    }

                    PlayerClosedFusebox(player.Value);
                }

                if (ElectricutorParent != null)
                {
                    if (!ElectricutorParent.IsDestroyed)
                    {
                        ElectricutorParent.Kill();
                    }
                }

                if (Electricutor != null)
                {
                    if (!Electricutor.IsDestroyed)
                    {
                        Electricutor.Kill();
                    }
                }


                if (Unloading)
                {
                    return;
                }

                if (EmergencyCleanup)
                {
                    return;
                }

                FuseboxToPowerlinePole.Remove(FuseboxNetID);
                ButtonToPowerlinePole.Remove(ButtonNetID);

                AllIOEntitiesToPowerlinePole.Remove(ButtonNetID);
                AllIOEntitiesToPowerlinePole.Remove(FuseboxNetID);
                AllIOEntitiesToPowerlinePole.Remove(ElectricutorNetID);
                AllIOEntitiesToPowerlinePole.Remove(ElectricutorParentNetID);
                AllIOEntitiesToPowerlinePole.Remove(ElectricutorGeneratorNetID);


                if (FuseboxButton != null)
                {
                    if (!FuseboxButton.IsDestroyed)
                    {
                        FuseboxButton.Kill();
                    }
                }

                if (Fusebox != null)
                {
                    if (!Fusebox.IsDestroyed)
                    {
                        Fusebox.Kill();
                    }
                }

                for (var o=0; o<Outlets.Length; o++)
                {
                    if (Outlets[o] == null)
                    {
                        continue;
                    }

                    Outlets[o].RootCombiner.Kill();
                }

                PowerlinePoleHelperCache.Remove(Data.Uid);
                Instance.Data.PowerlineData.Remove(Data.Uid);
                Instance.Data.Dirty = true;

            }

            public static PowerlinePoleHelper AddHelperPowerlinePole(SerializablePowerlinePole data)
            {

                var newGameObject = new GameObject("GridPowerPowerlinePole");
                newGameObject.layer = (int)Rust.Layer.Reserved1;
                newGameObject.SetActive(true);
                newGameObject.transform.SetPositionAndRotation(data.Transform.Position, Quaternion.Euler(data.Transform.Rotation));

                PowerlinePoleHelper newHelper = newGameObject.AddComponent<PowerlinePoleHelper>();

                newHelper.Prepare(data);

                return newHelper;
            }
        }


        public class CommonPlayerBehaviour : CommonBehaviour
        {
            public BasePlayer Player = null;
            public MultiToolConfig PlayerConfig;
            public PermissionProfile PlayerPermissionProfile;

            public virtual object WhatToDoWhenPlayerMissing()
            {
                return null;
            }

            private void FixedUpdate()
            {
                if (UnityEngine.Time.time <= LastUpdateAt + UpdateRate)
                {
                    return;
                }

                LastUpdateAt = UnityEngine.Time.time;

                if (IsPlayerMissing(Player))
                {
                    if (WhatToDoWhenPlayerMissing() != null)
                    {
                        return;
                    }
                }

                PerformUpdate();
            }
        }


        public class CommonBehaviour : MonoBehaviour
        {
            public float UpdateRateRandomization = 0F;

            public float UpdateRate = 0.2F;

            public float LastUpdateAt = float.MinValue;

            private void FixedUpdate()
            {
                if (UnityEngine.Time.time <= LastUpdateAt + UpdateRate)
                {
                    return;
                }

                LastUpdateAt = UnityEngine.Time.time;

                PerformUpdate();

                if (UpdateRateRandomization != 0F)
                {
                    UpdateRate = UnityEngine.Random.Range(UpdateRate - UpdateRateRandomization, UpdateRate + UpdateRateRandomization);
                }
            }

            private void OnDestroy()
            {
                Cleanup();
            }

            public virtual void Cleanup()
            {

            }

            public virtual void PerformUpdate()
            {

            }



        }

        public class PlayerHelper : CommonPlayerBehaviour
        {
            public static Hash<ulong, PlayerHelper> Cache;

            public static CuiRectTransformComponent AnchorSlack;
            public static CuiElementContainer ContainerSlack;
            public static string ContainerSlackJSON;

            public static CuiRectTransformComponent AnchorCrosshair;
            public static CuiElementContainer ContainerCrosshair;
            public static string ContainerCrosshairJSON;

            public static CuiRectTransformComponent AnchorMultiToolEditor;
            public static CuiElementContainer ContainerMultiToolEditor;
            public static string ContainerMultiToolEditorJSON;

            public const string GUI_SLACK = "gp.slack";
            public const string GUI_CROSSHAIR = "gp.crosshair";
            public const string GUI_EDITOR = "gp.editor";


            public static void GuiPrepareStatic()
            {
                AnchorCrosshair = GetAnchorFromScreenBox(0F, SCREEN_HEIGHT_IN_PIXELS, SCREEN_WIDTH_IN_PIXELS, 0F);
                ContainerCrosshair = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Parent = "Hud",
                        Name = GUI_CROSSHAIR,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = "•",
                                FontSize = 28,
                                Color = ColorPalette.RustyDirtyPink.unityColorString,
                                Align = TextAnchor.MiddleCenter,
                            },
                            AnchorCrosshair,
                        }
                    }
                };

                ContainerCrosshairJSON = ContainerCrosshair.ToJson();

                AnchorSlack = GetAnchorFromScreenBox(633, 186, 1263, 111);

                ContainerSlack = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Parent = "Hud",
                        Name = GUI_SLACK,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = "Wire Slack: [SLA]\n<size=12>Hold SPRINT to decrease, DUCK to increase</size>",
                                Color = ColorPalette.RustyDirtyPink.unityColorString,
                                FontSize = 18,
                                Align = TextAnchor.MiddleCenter,
                            },
                            new CuiOutlineComponent
                            {
                                Color = ColorPalette.RustyBlack.unityColorString,
                                Distance = "1 1",
                                UseGraphicAlpha = true
                            },
                            AnchorSlack,
                        }
                    }
                };

                ContainerSlackJSON = ContainerSlack.ToJson();

            }

            public static void GuiUnloadStatic()
            {
                AnchorSlack = null;
                ContainerSlack = null;
                ContainerSlackJSON = null;

                AnchorCrosshair = null;
                ContainerCrosshair = null;
                ContainerCrosshairJSON = null;

                AnchorMultiToolEditor = null;
                ContainerMultiToolEditor = null;
                ContainerMultiToolEditorJSON = null;
            }

            public ulong UserID;

            public bool ButtonSprintIsDownCurrent = false;
            public bool ButtonSprintIsDownPrevious = false;

            public bool ButtonDuckIsDownCurrent = false;
            public bool ButtonDuckIsDownPrevious = false;

            public bool ButtonReloadIsDownCurrent = false;
            public bool ButtonReloadIsDownPrevious = false;

            public uint ActiveItemIDCurrent = 0;
            public uint ActiveItemIDPrevious = 0;

            public bool ActiveItemChanged = false;

            public Item CurrentlyHeldItem = null;

            public bool ToolIsValidCurrent = false;
            public bool ToolIsValidPrevious = false;

            public int Dragging = 0; //0 - not dragging, 1 - add slack (duck), -1 - take slack (shift)

            public float LastDelta = 0F;

            public bool SlackDirty;

            public InputState PlayerServerInput;

            public bool PlayerMovedMouseCurrent = false;
            public bool PlayerMovedMousePrevious = false;

            public void Prepare(BasePlayer player)
            {
                Player = player;
                PlayerServerInput = player.serverInput;
                UserID = player.userID;
                PlayerConfig = GetPlayerConfig(player);
                PlayerPermissionProfile = GetPermissionProfile(player);

                UpdateRate = 1F / Mathf.Clamp(20F, 0.1F, 60F);

                Cache.Add(UserID, this);
            }


            public void SlackShow()
            {
                if (Player == null)
                {
                    return;
                }
                SlackHide();

                CuiHelper.AddUi(Player, ContainerSlackJSON.Replace("[SLA]", PlayerConfig.Slack.ToString("0.00")));

                if (Player.IsInvoking(SlackHide))
                {
                    Player.CancelInvoke(SlackHide);
                }

                Player.Invoke(SlackHide, 5F);
            }

            public void SlackHide()
            {
                if (Player == null)
                {
                    return;
                }
                CuiHelper.DestroyUi(Player, GUI_SLACK);
            }



            //the ioentityslothelpers are also gonna ask that
            //Not Displaying also means not raycasting over that type!

            public HeldEntity PlayerHeldEntity;
            public Planner PlayerHeldPlanner;
            public Item PlayerHeldItem;

            public Vector3 LastPlayerInputPositionCurrent = Vector3.zero;
            public Vector3 LastPlayerInputPositionPrevious = Vector3.zero;

            public Vector3 LastPlayerInputAnglesCurrent = Vector3.zero;
            public Vector3 LastPlayerInputAnglesPrevious = Vector3.zero;

            public void WhenPlayerInput(InputState state)
            {
                bool leftPressed = state.WasJustPressed(BUTTON.FIRE_PRIMARY);

                if (!leftPressed)
                {
                    return;
                }

                //ladder?

                PlayerHeldItem = Player.GetActiveItem();

                if (PlayerHeldItem == null)
                {
                    return;
                }


                if (PlayerHeldItem.info.shortname != ITEM_LADDER)
                {
                    return;
                }

                if (!PlayerPermissionProfile.GridCanDeployLadder)
                {
                    TellMessage(Player, MSG_CANT_DEPLOY_LADDER);
                    return;
                }

                PlayerHeldEntity = Player.GetHeldEntity();

                if (PlayerHeldEntity == null)
                {
                    return;
                }

                PlayerHeldPlanner = PlayerHeldEntity as Planner;

                if (PlayerHeldPlanner == null)
                {
                    return;
                }

                RaycastHit hit;
                var ray = Player.eyes.BodyRay();

                if (!Physics.Raycast(ray, out hit, 4F, LayerMask.GetMask("World", "Deployed"), QueryTriggerInteraction.Ignore))
                {
                    return;
                }

                if (hit.collider.name != "powerline_pole_a (1)")
                {
                    return;
                }

                var guideGameObject = new GameObject();
                guideGameObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                string lastPlacementError;

                Construction.Target target;

                if (UpdateGuide(guideGameObject, Player, LadderConstruction, out lastPlacementError, out target, Vector3.zero))
                {

                    DoBuild(PlayerHeldPlanner, Player, PlayerHeldItem, target, LadderConstruction);
                }
                else
                {
                    Instance.PrintToChat($"ERROR: {lastPlacementError}");
                }

                DestroyImmediate(guideGameObject);

            }

            public override void PerformUpdate()
            {
                base.PerformUpdate();

                ActiveItemIDPrevious = ActiveItemIDCurrent;
                ActiveItemIDCurrent = Player.svActiveItemID;

                ActiveItemChanged = (ActiveItemIDCurrent != ActiveItemIDPrevious);

                ToolIsValidPrevious = ToolIsValidCurrent;

                if (ActiveItemChanged)
                {
                    ToolIsValidCurrent = false;

                    if (ActiveItemIDCurrent == 0)
                    {
                        //changed to nothing
                        CurrentlyHeldItem = null;
                    }
                    else
                    {
                        //changed to something
                        CurrentlyHeldItem = Player.GetActiveItem();

                        if (CurrentlyHeldItem != null)
                        {
                            //check the short prefab name: wire tool, hose tool or advanced christmas lights, or something else?
                            ToolIsValidCurrent = ValidTools.Contains(CurrentlyHeldItem.info.shortname);
                        }
                    }
                }

                bool shouldShowSlack = false;

                //check if switched from non-valid tool to a valid one (for slack, which is universal)
                if (ToolIsValidCurrent != ToolIsValidPrevious)
                {

                    //something changed
                    if (ToolIsValidCurrent)
                    {
                        //wasn't valid before but is now, show gui
                        shouldShowSlack = true;

                    }
                    else
                    {
                        //was valid before but isn't now, hide gui
                        SlackHide();
                    }
                }

                PlayerMovedMousePrevious = PlayerMovedMouseCurrent;
                //assigning will be done OnPlayerInput

                //the player can't be missing here
                ButtonSprintIsDownPrevious = ButtonSprintIsDownCurrent;
                ButtonSprintIsDownCurrent = PlayerServerInput.IsDown(BUTTON.SPRINT);

                ButtonDuckIsDownPrevious = ButtonDuckIsDownCurrent;
                ButtonDuckIsDownCurrent = PlayerServerInput.IsDown(BUTTON.DUCK);

                ButtonReloadIsDownPrevious = ButtonReloadIsDownCurrent;
                ButtonReloadIsDownCurrent = PlayerServerInput.IsDown(BUTTON.RELOAD);

                //wires and hoses

                if (ToolIsValidCurrent)
                {
                    switch (Dragging)
                    {
                        case 0:
                            {
                                //not dragging. listen if you should start
                                if (ButtonSprintIsDownCurrent && !ButtonSprintIsDownPrevious)
                                {
                                    Dragging = -1;
                                }
                                else if (ButtonDuckIsDownCurrent && !ButtonDuckIsDownPrevious)
                                {
                                    Dragging = 1;
                                }
                            }
                            break;
                        case -1:
                            {
                                //currently reducing slack with sprint, check if you should stop
                                if (!ButtonSprintIsDownCurrent && ButtonSprintIsDownPrevious)
                                {
                                    Dragging = 0;
                                    LastDelta = 0F;
                                }
                                else
                                {
                                    //keep reducing
                                    LastDelta -= 0.01F;
                                    if (LastDelta < -1F)
                                    {
                                        LastDelta = -1F;
                                    }
                                }
                            }
                            break;
                        case 1:
                            {
                                //currently increasing slack with duck, check if you should stop
                                if (!ButtonDuckIsDownCurrent && ButtonDuckIsDownPrevious)
                                {
                                    Dragging = 0;
                                    LastDelta = 0F;
                                }
                                else
                                {
                                    //keep increasing
                                    LastDelta += 0.01F;
                                    if (LastDelta > 1F)
                                    {
                                        LastDelta = 1F;
                                    }
                                }
                            }
                            break;
                    }

                }
                else
                {
                    Dragging = 0;
                    LastDelta = 0F;
                }

                if (Dragging != 0)
                {
                    if (LastDelta != 0F)
                    {
                        var newSlack = Mathf.Clamp(PlayerConfig.Slack + LastDelta, 0F, PlayerPermissionProfile.SlackMax);

                        SlackDirty = false;

                        if (newSlack != PlayerConfig.Slack)
                        {
                            PlayerConfig.Slack = newSlack;
                            SlackDirty = true;
                            PlayerConfig.Dirty = true;
                            shouldShowSlack = true;
                        }
                    }
                }

                if (shouldShowSlack)
                {
                    SlackShow();
                }                

            }

            public override void Cleanup()
            {
                SlackHide();

                if (Unloading)
                {
                    return;
                }

                Cache.Remove(UserID);
            }

            public override object WhatToDoWhenPlayerMissing()
            {
                DestroyImmediate(this);
                return false;
            }

        }

        //attach to xmas lights helper. those expire.
        public class VanillaXmasLightsHelper : CommonPlayerBehaviour
        {
            public static Hash<uint, VanillaXmasLightsHelper> Cache;

            public const float MAX_LIFETIME_SECONDS = 180F;

            public List<Vector3> Points;

            public AdvancedChristmasLights Lights;
            public uint LightsNetID;

            public float LastDrawnPreviewAt = float.MinValue;

            public float DestroyAt = UnityEngine.Time.time + MAX_LIFETIME_SECONDS;

            public Vector3[] PreviewCatenary = null;

            int HowManyMoreAdded = 0;

            //for slack preview
            public AdvancedChristmasLights.pointEntry PointStartCurrent;
            public AdvancedChristmasLights.pointEntry PointEndCurrent;
            public float SlackCurrent;

            public int PhysicalPointCountPrevious = 0;
            public int PhysicalPointCountCurrent = 0;
            public bool ReadyForBake = false;
            public bool ShouldBake = false;

            //for baking
            public AdvancedChristmasLights.pointEntry PointStartPrevious;
            public AdvancedChristmasLights.pointEntry PointEndPrevious;
            public float SlackPrevious;

            public int AddedPointCountCurrent = 0;
            public int AddedPointCountPrevious = 0;
            public bool ReadyForPreview = false;
            public bool ShouldPreview = false;

            public int NewPointsDectected = 0;

            //use this to get catenaries for Point Start/End Current/Previous
            public List<AdvancedChristmasLights.pointEntry> AddedPoints = new List<AdvancedChristmasLights.pointEntry>();


            //use these for counting how many points actually exist on the light,
            //to detect if the player has added some more
            //change those amounts after baking

            public void Prepare(BasePlayer player, AdvancedChristmasLights lights, Vector3 normal = default(Vector3))
            {
                Player = player;
                PlayerConfig = GetPlayerConfig(player);
                PlayerPermissionProfile = GetPermissionProfile(player);

                Lights = lights;
                LightsNetID = lights.net.ID;

                SlackCurrent = PlayerConfig.Slack;
                SlackPrevious = SlackCurrent;

                UpdateRate = GetUpdateRateFromFPS(20F);

                Cache.Add(LightsNetID, this);
            }

            public override void PerformUpdate()
            {
                if (LastUpdateAt >= DestroyAt)
                {
                    DestroyImmediate(this);
                    return;
                }

                if (Lights.IsFinalized())
                {
                    DestroyImmediate(this);
                    return;
                }

                ShouldPreview = false;
                ShouldBake = false;

                NewPointsDectected = 0;

                PhysicalPointCountPrevious = PhysicalPointCountCurrent;
                PhysicalPointCountCurrent = Lights.points?.Count ?? 0;

                //are we adding any points?

                if (PhysicalPointCountPrevious != PhysicalPointCountCurrent)
                {
                    //note how many...
                    NewPointsDectected = PhysicalPointCountCurrent - PhysicalPointCountPrevious;

                    //before adding, take the last two...
                    //careful with the indices.

                    //let's make sure we have at least 2 points BEFORE adding
                    if (AddedPointCountCurrent >= 2)
                    {
                        PointStartPrevious = AddedPoints[AddedPoints.Count() - 2];
                        PointEndPrevious = AddedPoints[AddedPoints.Count() - 1];
                    }

                    int startAtIndex = PhysicalPointCountPrevious - 0; //so if we had 30 points, the max index was 29, now start at 30
                    int endAtIndex = PhysicalPointCountCurrent - 1;

                    //inclusive, so <= instead of <
                    for (var index = startAtIndex; index <= endAtIndex; index++)
                    {
                        //add the points
                        AddedPoints.Add(Lights.points[index]);
                    }

                    //let's make sure we have at least 2 points now...

                    if (AddedPointCountCurrent + NewPointsDectected > 1)
                    {
                        PointStartCurrent = AddedPoints[AddedPoints.Count() - 2];
                        PointEndCurrent = AddedPoints[AddedPoints.Count() - 1];

                        SlackPrevious = SlackCurrent;
                        SlackCurrent = SlackCurrent = PlayerConfig.Slack;
                    }


                    ShouldBake = true;
                    ShouldPreview = true;
                }

                AddedPointCountPrevious = AddedPointCountCurrent;
                AddedPointCountCurrent = AddedPoints.Count;
                
                ReadyForPreview = AddedPointCountCurrent > 1;
                ReadyForBake = AddedPointCountCurrent > 2;

                if (PlayerConfig.Slack != SlackCurrent)
                {

                    SlackCurrent = PlayerConfig.Slack;
                    ShouldPreview = true;
                }

                //last chance to override: 

                if (!ReadyForBake)
                {
                    ShouldBake = false;
                }

                if (!ReadyForPreview)
                {
                    ShouldPreview = false;
                }

                if (ShouldPreview)
                {
                    GenerateAndDrawPreview(PointStartCurrent, PointEndCurrent, SlackCurrent, PlayerPermissionProfile.SubdivisionsPreview);
                }

                if (ShouldBake)
                {
                    GenerateAndBakePrevious(PointStartPrevious, PointEndPrevious, SlackPrevious);
                }
            }

            public void GenerateAndDrawPreview(AdvancedChristmasLights.pointEntry start, AdvancedChristmasLights.pointEntry end, float slack, float duration)
            {
                PreviewCatenary = GetCatenaryArrayVector3(start.point, end.point, slack, PlayerPermissionProfile.SubdivisionsPreview);
                DrawSpline(Player, duration, PreviewCatenary, XmasColorSequence);
            }

            public void GenerateAndBakePrevious(AdvancedChristmasLights.pointEntry start, AdvancedChristmasLights.pointEntry end, float slack)
            {
                //this is where the magic happens
                //we need to remove the pre-penultimate and penultimate points from the physical points
                //(start and end)
                //and instead, add a catenary based on those in that spot.
                //after the catenary, the latest point.

                Action<List<AdvancedChristmasLights.pointEntry>> executeAfterwards = (generatedPoints) =>
                {
                    //and now of course, set the physical point count current to whatever it is right now!
                    //so it doesn't freak out with 50+ new added points.
                    if (Lights == null)
                    {
                        return;
                    }

                    PhysicalPointCountCurrent = Lights.points.Count;
                };

                ModifyHangingXmasLightsSegments(Lights, new List<AdvancedChristmasLights.pointEntry> { start, end }, Enumerable.Repeat(slack, 3).ToList(), PlayerPermissionProfile.SubdivisionsFinal, executeAfterwards);
            }

            public override object WhatToDoWhenPlayerMissing()
            {
                DestroyImmediate(this);
                return false;
            }

            private void OnDestroy()
            {
                Cleanup();
            }

            public override void Cleanup()
            {
                base.Cleanup();

                //mark as baked, because clearly we're either done
                //or the time is up
                if (Lights != null)
                {
                    if (!Lights.IsDestroyed)
                    {
                        Lights.SetFlag(FLAG_BAKED, true, false, true);
                    }
                }

                //don't remove from cache if Unloading,
                //that means there's most likely some iterating going on

                if (Unloading)
                {
                    return;
                }

                Cache.Remove(LightsNetID);
            }
        }
        #endregion

        #region GUI

        public static class PowerlineTransformerGUI
        {
            #region HELPERS

            #endregion


            //container names
            public const string GUI_TITLE = "gp.title";

            public const string GUI_STATUS_BACKDROP = "gp.status.backdrop";
            public const string GUI_STATUS_TEXT = "gp.status.text";
            public const string GUI_OVERLAY = "Overlay";

            public const string GUI_ICON1_BACKDROP = "gp.icon.1.bg";
            public const string GUI_ICON2_BACKDROP = "gp.icon.2.bg";
            public const string GUI_ICON3_BACKDROP = "gp.icon.3.bg";

            public const string GUI_ICON1_IMAGE = "gp.icon.1.img";
            public const string GUI_ICON2_IMAGE = "gp.icon.2.img";
            public const string GUI_ICON3_IMAGE = "gp.icon.3.img";

            public const string GUI_BODY1_BACKDROP = "gp.body.1.bg";
            public const string GUI_BODY2_BACKDROP = "gp.body.2.bg";
            public const string GUI_BODY3_BACKDROP = "gp.body.3.bg";

            public const string GUI_BODY1_TEXT = "gp.body.1.txt";
            public const string GUI_BODY2_TEXT = "gp.body.2.txt";
            public const string GUI_BODY3_TEXT = "gp.body.3.txt";

            public const string ICON_URL_TECHTRASH = "https://i.imgur.com/wOOwzns.png";
            public const string ICON_URL_COMBINER = "https://i.imgur.com/jb70lgk.png";
            public const string ICON_URL_FUSE = "https://i.imgur.com/TPDgyh2.png";

            public const float PADDING = 11;

            public const float ANCHOR_TITLE_AND_STATUS_LEFT = 1249;
            public const float ANCHOR_TITLE_AND_STATUS_RIGHT = 1818;

            public const float ANCHOR_TITLE_BOTTOM = 315;
            public const float ANCHOR_TITLE_TOP = 274;

            public const float ANCHOR_STATUS_BOTTOM = 359;
            public const float ANCHOR_STATUS_TOP = 328;

            public const float ANCHOR_COMMON_RIGHT = 1800;
            public const float ANCHOR_COMMON_RIGHT_PADDED = ANCHOR_COMMON_RIGHT - PADDING;

            public const float ANCHOR_COMMON_LEFT = 1379;
            public const float ANCHOR_COMMON_LEFT_PADDED = ANCHOR_COMMON_LEFT + PADDING;

            public const float ANCHOR_COMMON_BOTTOM1 = 480;
            public const float ANCHOR_COMMON_BOTTOM_PADDED1 = ANCHOR_COMMON_BOTTOM1 - PADDING;

            public const float ANCHOR_COMMON_BOTTOM2 = 592;
            public const float ANCHOR_COMMON_BOTTOM_PADDED2 = ANCHOR_COMMON_BOTTOM2 - PADDING;

            public const float ANCHOR_COMMON_BOTTOM3 = 704;
            public const float ANCHOR_COMMON_BOTTOM_PADDED3 = ANCHOR_COMMON_BOTTOM3 - PADDING;

            public const float ANCHOR_COMMON_TOP1 = 375;
            public const float ANCHOR_COMMON_TOP1_PADDED = ANCHOR_COMMON_TOP1 + PADDING;

            public const float ANCHOR_COMMON_TOP2 = 487;
            public const float ANCHOR_COMMON_TOP2_PADDED = ANCHOR_COMMON_TOP2 + PADDING;

            public const float ANCHOR_COMMON_TOP3 = 599;
            public const float ANCHOR_COMMON_TOP3_PADDED = ANCHOR_COMMON_TOP3 + PADDING;

            //other consts
            public const int GUI_FONT_SIZE_TITLE = 20;
            public const int GUI_FONT_SIZE_REGULAR = 12;

            public const float GUI_FADE = 0.125F;

            //anchors are always shared
            public static CuiRectTransformComponent AnchorTitle;

            public static CuiRectTransformComponent AnchorStatus;

            public static CuiRectTransformComponent AnchorIcon1Backdrop;
            public static CuiRectTransformComponent AnchorIcon2Backdrop;
            public static CuiRectTransformComponent AnchorIcon3Backdrop;

            public static CuiRectTransformComponent AnchorIcon1Image;
            public static CuiRectTransformComponent AnchorIcon2Image;
            public static CuiRectTransformComponent AnchorIcon3Image;

            public static CuiRectTransformComponent AnchorBody1Backdrop;
            public static CuiRectTransformComponent AnchorBody2Backdrop;
            public static CuiRectTransformComponent AnchorBody3Backdrop;

            public static CuiRectTransformComponent AnchorBody1Text;
            public static CuiRectTransformComponent AnchorBody2Text;
            public static CuiRectTransformComponent AnchorBody3Text;

            //title, button 1 and button 2 containers are also shared...
            public static CuiElementContainer ContainerTitle;
            public static CuiElementContainer ContainerStatus;

            public static CuiElementContainer ContainerIcon1;
            public static CuiElementContainer ContainerIcon2;
            public static CuiElementContainer ContainerIcon3;

            public static CuiElementContainer ContainerBody1;
            public static CuiElementContainer ContainerBody2;
            public static CuiElementContainer ContainerBody3;

            //and their serialised versions...
            public static string ContainerTitleJSON;
            public static string ContainerStatusJSON;

            public static string ContainerIcon1JSON;
            public static string ContainerIcon2JSON;
            public static string ContainerIcon3JSON;

            public static string ContainerBody1JSON;
            public static string ContainerBody2JSON;
            public static string ContainerBody3JSON;

            public static string SerializeContainer(CuiElementContainer container)
            {
                return container.ToJson();
            }
            public static void InitializeStatic()
            {
                AnchorTitle = GetAnchorFromScreenBox(ANCHOR_TITLE_AND_STATUS_LEFT, ANCHOR_TITLE_BOTTOM, ANCHOR_TITLE_AND_STATUS_RIGHT, ANCHOR_TITLE_TOP);

                AnchorStatus = GetAnchorFromScreenBox(ANCHOR_TITLE_AND_STATUS_LEFT, ANCHOR_STATUS_BOTTOM, ANCHOR_TITLE_AND_STATUS_RIGHT, ANCHOR_STATUS_TOP);

                AnchorIcon1Backdrop = GetAnchorFromScreenBox(1267, 480, 1372, 375);

                AnchorIcon2Backdrop = GetAnchorFromScreenBox(1267, 592, 1372, 487);

                AnchorIcon3Backdrop = GetAnchorFromScreenBox(1267, 704, 1372, 599);

                AnchorIcon1Image = GetAnchorFromScreenBox(1278, 469, 1361, 386);

                AnchorIcon2Image = GetAnchorFromScreenBox(1278, 581, 1361, 498);

                AnchorIcon3Image = GetAnchorFromScreenBox(1278, 693, 1361, 610);

                AnchorBody1Backdrop = GetAnchorFromScreenBox(ANCHOR_COMMON_LEFT, ANCHOR_COMMON_BOTTOM1, ANCHOR_COMMON_RIGHT, ANCHOR_COMMON_TOP1);

                AnchorBody2Backdrop = GetAnchorFromScreenBox(ANCHOR_COMMON_LEFT, ANCHOR_COMMON_BOTTOM2, ANCHOR_COMMON_RIGHT, ANCHOR_COMMON_TOP2);

                AnchorBody3Backdrop = GetAnchorFromScreenBox(ANCHOR_COMMON_LEFT, ANCHOR_COMMON_BOTTOM3, ANCHOR_COMMON_RIGHT, ANCHOR_COMMON_TOP3);

                AnchorBody1Text = GetAnchorFromScreenBox(ANCHOR_COMMON_LEFT_PADDED, ANCHOR_COMMON_BOTTOM_PADDED1, ANCHOR_COMMON_RIGHT_PADDED, ANCHOR_COMMON_TOP1_PADDED);

                AnchorBody2Text = GetAnchorFromScreenBox(ANCHOR_COMMON_LEFT_PADDED, ANCHOR_COMMON_BOTTOM_PADDED2, ANCHOR_COMMON_RIGHT_PADDED, ANCHOR_COMMON_TOP2_PADDED);

                AnchorBody3Text = GetAnchorFromScreenBox(ANCHOR_COMMON_LEFT_PADDED, ANCHOR_COMMON_BOTTOM_PADDED3, ANCHOR_COMMON_RIGHT_PADDED, ANCHOR_COMMON_TOP3_PADDED);

                ContainerTitle = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = GUI_TITLE,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                FontSize = GUI_FONT_SIZE_TITLE,
                                Align = TextAnchor.LowerLeft,
                                Color = ColorPalette.RustyDirtyPink.unityColorString,
                                Text = MSG(MSG_POWERLINE_GUI_TITLE),
                                FadeIn = GUI_FADE
                            },
                            AnchorTitle,
                        }
                    }
                };

                ContainerTitleJSON = SerializeContainer(ContainerTitle);

                ContainerStatus = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = GUI_STATUS_BACKDROP,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "[STC]",
                                FadeIn = GUI_FADE
                            },
                            AnchorStatus
                        }
                    },
                    new CuiElement
                    {
                        Name = GUI_STATUS_TEXT,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                FontSize = GUI_FONT_SIZE_REGULAR,
                                Align = TextAnchor.MiddleCenter,
                                Color = ColorPalette.RustyDirtyPink.unityColorString,
                                Text = "[STA]",
                                FadeIn = GUI_FADE
                            },
                            AnchorStatus
                        }
                    }
                };
                ContainerStatusJSON = SerializeContainer(ContainerStatus);

                ContainerIcon1 = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = GUI_ICON1_BACKDROP,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = ColorPalette.RustyGrey.unityColorString+" 0.5",
                                FadeIn = GUI_FADE
                            },
                            AnchorIcon1Backdrop
                        }
                    },
                    new CuiElement
                    {
                        Name = GUI_ICON1_IMAGE,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Url = ICON_URL_COMBINER,
                                FadeIn = GUI_FADE,
                            },
                            AnchorIcon1Image
                        }
                    }
                };

                ContainerIcon1JSON = SerializeContainer(ContainerIcon1);

                ContainerIcon2 = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = GUI_ICON2_BACKDROP,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = ColorPalette.RustyGrey.unityColorString+" 0.5",
                                FadeIn = GUI_FADE
                            },
                            AnchorIcon2Backdrop
                        }
                    },
                    new CuiElement
                    {
                        Name = GUI_ICON2_IMAGE,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Url = ICON_URL_TECHTRASH,
                                FadeIn = GUI_FADE,
                            },
                            AnchorIcon2Image
                        }
                    }
                };

                ContainerIcon2JSON = SerializeContainer(ContainerIcon2);

                ContainerIcon3 = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = GUI_ICON3_BACKDROP,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = ColorPalette.RustyGrey.unityColorString+" 0.5",
                                FadeIn = GUI_FADE
                            },
                            AnchorIcon3Backdrop
                        }
                    },
                    new CuiElement
                    {
                        Name = GUI_ICON3_IMAGE,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Url = ICON_URL_FUSE,
                                FadeIn = GUI_FADE,
                            },
                            AnchorIcon3Image
                        }
                    }
                };

                ContainerIcon3JSON = SerializeContainer(ContainerIcon3);

                ContainerBody1 = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = GUI_BODY1_BACKDROP,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = ColorPalette.RustyGrey.unityColorString+" 0.5",
                                FadeIn = GUI_FADE
                            },
                            AnchorBody1Backdrop
                        }
                    },
                    new CuiElement
                    {
                        Name = GUI_BODY1_TEXT,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                FontSize = GUI_FONT_SIZE_REGULAR,
                                Align = TextAnchor.UpperLeft,
                                Color = ColorPalette.RustyDirtyPink.unityColorString,
                                Text = "[BD1]",
                                FadeIn = GUI_FADE
                            },
                            AnchorBody1Text,
                        }
                    }
                };
                ContainerBody1JSON = SerializeContainer(ContainerBody1);

                ContainerBody2 = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = GUI_BODY2_BACKDROP,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = ColorPalette.RustyGrey.unityColorString+" 0.5",
                                FadeIn = GUI_FADE
                            },
                            AnchorBody2Backdrop
                        }
                    },
                    new CuiElement
                    {
                        Name = GUI_BODY2_TEXT,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                FontSize = GUI_FONT_SIZE_REGULAR,
                                Align = TextAnchor.UpperLeft,
                                Color = ColorPalette.RustyDirtyPink.unityColorString,
                                Text = "[BD2]",
                                FadeIn = GUI_FADE
                            },
                            AnchorBody2Text,
                        }
                    }
                };
                ContainerBody2JSON = SerializeContainer(ContainerBody2);

                ContainerBody3 = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = GUI_BODY3_BACKDROP,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = ColorPalette.RustyGrey.unityColorString+" 0.5",
                                FadeIn = GUI_FADE
                            },
                            AnchorBody3Backdrop
                        }
                    },
                    new CuiElement
                    {
                        Name = GUI_BODY3_TEXT,
                        FadeOut = GUI_FADE,
                        Parent = GUI_OVERLAY,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                FontSize = GUI_FONT_SIZE_REGULAR,
                                Align = TextAnchor.UpperLeft,
                                Color = ColorPalette.RustyDirtyPink.unityColorString,
                                Text = "[BD3]",
                                FadeIn = GUI_FADE
                            },
                            AnchorBody3Text,
                        }
                    }
                };
                ContainerBody3JSON = SerializeContainer(ContainerBody3);
            }
            public static void UnloadStatic()
            {
                AnchorTitle = null;
                AnchorStatus = null;

                AnchorIcon1Backdrop = null;
                AnchorIcon2Backdrop = null;
                AnchorIcon3Backdrop = null;

                AnchorIcon1Image = null;
                AnchorIcon2Image = null;
                AnchorIcon3Image = null;

                AnchorBody1Backdrop = null;
                AnchorBody2Backdrop = null;
                AnchorBody3Backdrop = null;

                AnchorBody1Text = null;
                AnchorBody2Text = null;
                AnchorBody3Text = null;

                ContainerTitle = null;

                ContainerIcon1 = null;
                ContainerIcon2 = null;
                ContainerIcon3 = null;

                ContainerBody1 = null;
                ContainerBody2 = null;
                ContainerBody3 = null;

                ContainerStatus = null;

                //and their serialised versions...
                ContainerTitleJSON = null;

                ContainerIcon1JSON = null;
                ContainerIcon2JSON = null;
                ContainerIcon3JSON = null;

                ContainerBody1JSON = null;
                ContainerBody2JSON = null;
                ContainerBody3JSON = null;

                ContainerStatusJSON = null;
            }
            public static void ShowContainerTitle(BasePlayer player, PowerlinePoleHelper powerlineHelper)
            {
                //no replacements
                ShowContainer(player, ContainerTitleJSON, null, GUI_TITLE);
            }
            public static void HideContainerTitle(BasePlayer player)
            {
                HideContainers(player, GUI_TITLE);
            }

            public static void ShowContainerStatus(BasePlayer player, PowerlinePoleHelper powerlineHelper)
            {
                ShowContainer(player, ContainerStatusJSON, new Dictionary<string, string>
                {
                    ["[STA]"] = powerlineHelper.GuiState.TopStatusCurrent,
                    ["[STC]"] = powerlineHelper.GuiState.StatusColor
                }, GUI_STATUS_BACKDROP, GUI_STATUS_TEXT);
            }

            public static void HideContainerStatus(BasePlayer player)
            {
                HideContainers(player, GUI_STATUS_BACKDROP, GUI_STATUS_TEXT);
            }

            public static void ShowContainerIcon1(BasePlayer player, PowerlinePoleHelper powerlineHelper)
            {
                ShowContainer(player, ContainerIcon1JSON, null, GUI_ICON1_BACKDROP, GUI_ICON1_IMAGE);
            }

            public static void HideContainerIcon1(BasePlayer player)
            {
                HideContainers(player, GUI_ICON1_BACKDROP, GUI_ICON1_IMAGE);
            }

            public static void ShowContainerIcon2(BasePlayer player, PowerlinePoleHelper powerlineHelper)
            {
                ShowContainer(player, ContainerIcon2JSON, null, GUI_ICON2_BACKDROP, GUI_ICON2_IMAGE);
            }

            public static void HideContainerIcon2(BasePlayer player)
            {
                HideContainers(player, GUI_ICON2_BACKDROP, GUI_ICON2_IMAGE);
            }
            public static void ShowContainerIcon3(BasePlayer player, PowerlinePoleHelper powerlineHelper)
            {
                ShowContainer(player, ContainerIcon3JSON, null, GUI_ICON3_BACKDROP, GUI_ICON3_IMAGE);
            }

            public static void HideContainerIcon3(BasePlayer player)
            {
                HideContainers(player, GUI_ICON3_BACKDROP, GUI_ICON3_IMAGE);
            }

            public static void ShowContainerBody1(BasePlayer player, PowerlinePoleHelper powerlineHelper)
            {
                ShowContainer(player, ContainerBody1JSON, new Dictionary<string, string>
                {
                    ["[BD1]"] = powerlineHelper.GuiState.OutletStatusCurrent
                }, GUI_BODY1_BACKDROP, GUI_BODY1_TEXT);
            }

            public static void HideContainerBody1(BasePlayer player)
            {
                HideContainers(player, GUI_BODY1_BACKDROP, GUI_BODY1_TEXT);
            }

            public static void ShowContainerBody2(BasePlayer player, PowerlinePoleHelper powerlineHelper)
            {
                ShowContainer(player, ContainerBody2JSON, new Dictionary<string, string>
                {
                    ["[BD2]"] = powerlineHelper.GuiState.TechTrashStatusCurrent
                }, GUI_BODY2_BACKDROP, GUI_BODY2_TEXT);
            }

            public static void HideContainerBody2(BasePlayer player)
            {
                HideContainers(player, GUI_BODY2_BACKDROP, GUI_BODY2_TEXT);
            }
            public static void ShowContainerBody3(BasePlayer player, PowerlinePoleHelper powerlineHelper)
            {
                ShowContainer(player, ContainerBody3JSON, new Dictionary<string, string>
                {
                    ["[BD3]"] = powerlineHelper.GuiState.FuseStatusCurrent
                }, GUI_BODY3_BACKDROP, GUI_BODY3_TEXT);
            }

            public static void HideContainerBody3(BasePlayer player)
            {
                HideContainers(player, GUI_BODY3_BACKDROP, GUI_BODY3_TEXT);
            }

            public static void ShowContainer(BasePlayer player, string containerJSON, Dictionary<string, string> replacements = null, params string[] containersToHide)
            {
                HideContainers(player, containersToHide);

                if (replacements != null)
                {
                    containerJSON = replacements.Aggregate(containerJSON, (current, value) =>
     current.Replace(value.Key, value.Value));
                }

                CuiHelper.AddUi(player, containerJSON);
            }

            public static void HideContainers(BasePlayer player, params string[] containers)
            {
                for (var p = 0; p< containers.Length; p++)
                {
                    CuiHelper.DestroyUi(player, containers[p]);
                }
            }

            public static void ShowAllContainers(BasePlayer player, PowerlinePoleHelper powerlineHelper)
            {
                ShowContainerTitle(player, powerlineHelper);
                ShowContainerStatus(player, powerlineHelper);

                ShowContainerIcon1(player, powerlineHelper);
                ShowContainerIcon2(player, powerlineHelper);
                ShowContainerIcon3(player, powerlineHelper);

                ShowContainerBody1(player, powerlineHelper);
                ShowContainerBody2(player, powerlineHelper);
                ShowContainerBody3(player, powerlineHelper);
            }

            public static void HideAllContainers(BasePlayer player)
            {
                HideContainerTitle(player);
                HideContainerStatus(player);

                HideContainerIcon1(player);
                HideContainerIcon2(player);
                HideContainerIcon3(player);

                HideContainerBody1(player);
                HideContainerBody2(player);
                HideContainerBody3(player);
            }

            //top level shit
            //we need to change that so it's per-helper, rather
            //also keep player ulongs as key, not the baseplayer itself, what that goes null

            public static void PlayerOpenedFusebox(BasePlayer player, PowerlinePoleHelper powerlineHelper)
            {
                ShowAllContainers(player, powerlineHelper);
            }

            public static void PlayerClosedFusebox(BasePlayer player)
            {
                HideAllContainers(player);
            }
        }

        #endregion

        #region DEBUG
        [ChatCommand("raytest")]
        private void cmdChatDraw(BasePlayer player, string command, string[] args)
        {
            RaycastHit hit;
            var ray = player.eyes.BodyRay();

            if (!Physics.Raycast(ray, out hit, 4F, LayerMask.GetMask("World", "Deployed"), QueryTriggerInteraction.Ignore))
            {
                player.ChatMessage("RAY FAIL");
                return;
            }

            player.ChatMessage($"{hit.collider.name} is hit collider name. {hit.collider.gameObject.name} is game object name of that collider.");
            Collider AAA = hit.collider;

            Instance.PrintToChat("BREAKIE POINTIE");
        }
        #endregion

        #region COMMANDS
        private static BasePlayer commandPlayer;
        #region CMD
        [Command(CMD_GP_EMERGENCY_CLEANUP), Permission(PERM_ADMIN)]
        private void CommandEmergencyCleanup(IPlayer iplayer, string command, string[] args)
        {
            if (!HasAdminPermission(iplayer)) return;

            int removed = TryEmergencyCleanup();

            TellMessageI(iplayer, $"Emergency cleanup done, removed {removed} entities");
        }

        #endregion

        [Command(CMD_GP_CFG), Permission(PERM_ADMIN)]
        private void CommandGPConfig(IPlayer iplayer, string command, string[] args)
        {
            if (!HasAdminPermission(iplayer)) return;
            
            if (args.Length == 0)
            {
                //display all possible config keys and their values
                ReusableString = $"{MSG(MSG_CFG_DEFAULT, iplayer.Id)}\n";
                foreach (var entry in ConfigValues)
                {
                    ReusableString += $"{MSG(MSG_CFG_RUNDOWN_FORMAT, iplayer.Id, entry.Key, entry.Value.formatter(entry.Value.GetSet))}\n";
                }

                TellMessageI(iplayer, ReusableString);
            }
            else
            {

                if (ConfigValues.ContainsKey(args[0]))
                {
                    //has argument[1] been provided? if not, display the full description.
                    if (args.Length > 1)
                    {
                        ConfigValues[args[0]].GetSet = args[1];
                    }
                    else
                    {
                        TellMessageI(iplayer, $"{MSG(MSG_CFG_DETAILS_FORMAT, null, ConfigValues[args[0]].name, ConfigValues[args[0]].description, ConfigValues[args[0]].valueType, ConfigValues[args[0]].formatter(ConfigValues[args[0]].GetSet))}. Accepted values are {ConfigValues[args[0]].FormatAcceptable()}");
                    }

                }
                else
                {
                    TellMessageI(iplayer, MSG(MSG_CFG_NO_SETTING_FOUND, iplayer.Id));
                }
            }

        }
        #endregion

        public const uint LADDER_PREFAB_ID = 2150203378;
        public static Construction LadderConstruction;

        public static GameObject ThePowerGridGameObject;

        #region API
        [HookMethod(nameof(GridIsProducing))]
        public bool GridIsProducing()
        {
            if (PowerGrid.TheOnlyInstance == null)
            {
                return false;
            }

            return PowerGrid.TheOnlyInstance.GridProducing;
        }

        [HookMethod(nameof(GridGetEfficiency))]
        public float GridGetEfficiency()
        {
            if (PowerGrid.TheOnlyInstance == null)
            {
                return 0F;
            }

            return PowerGrid.TheOnlyInstance.GlobalEfficiency;
        }

        [HookMethod(nameof(GridPowerIsConstant))]
        public bool GridPowerIsConstant()
        {
            if (PowerGrid.TheOnlyInstance == null)
            {
                return false;
            }

            return Instance.Configuration.GridConstantPower;
        }

        [HookMethod(nameof(GridGetProductionHourStart))]
        public float GridGetProductionHourStart()
        {
            if (PowerGrid.TheOnlyInstance == null)
            {
                return -1F;
            }

            return Instance.Configuration.GridProductionStartAtHour;
        }

        [HookMethod(nameof(GridGetProductionHourEnd))]
        public float GridGetProductionHourEnd()
        {
            if (PowerGrid.TheOnlyInstance == null)
            {
                return -1F;
            }

            return Instance.Configuration.GridProductionEndAtHour;
        }

        [HookMethod(nameof(StreetlightsAreOn))]
        public bool StreetlightsAreOn()
        {
            if (PowerGrid.TheOnlyInstance == null)
            {
                return false;
            }

            return PowerGrid.TheOnlyInstance.StreetlightsOn;
        }

        [HookMethod(nameof(StreetlightsGetTurnOnHour))]
        public float StreetlightsGetTurnOnHour()
        {
            if (PowerGrid.TheOnlyInstance == null)
            {
                return -1F;
            }

            return Instance.Configuration.StreetlightsTurnOnAtHour;
        }

        [HookMethod(nameof(StreetlightsGetTurnOffHour))]
        public float StreetlightsGetTurnOffHour()
        {
            if (PowerGrid.TheOnlyInstance == null)
            {
                return -1F;
            }

            return Instance.Configuration.StreetlightsTurnOffAtHour;
        }

        [HookMethod(nameof(StreetlightsPowerIsConstant))]
        public bool StreetlightsPowerIsConstant()
        {
            if (PowerGrid.TheOnlyInstance == null)
            {
                return false;
            }

            return Instance.Configuration.StreetlightsConstantPower;
        }

        #endregion

        #region HOOKS
        private void OnServerInitialized()
        {
            Unloading = false;
            Instance = this;

            lang.RegisterMessages(LangMessages, this);

            LadderConstruction = PrefabAttribute.server.Find<Construction>(LADDER_PREFAB_ID);

            FuseboxAllowedItems = new ItemDefinition[]
            {
                ItemManager.itemDictionaryByName[ITEM_FUSE],
                ItemManager.itemDictionaryByName[ITEM_TECHTRASH],
                ItemManager.itemDictionaryByName[ITEM_ROOTCOMBINER],
            };

            PowerlinePoleHelper.PowerlineSocketPositions = new List<Vector3>
            {
                new Vector3(1.350F, 10.778F, 0.120F),
                new Vector3(0.746F, 10.768F, 0.120F),

                new Vector3(-0.748F, 10.742F, 0.120F),
                new Vector3(-1.355F, 10.731F, 0.120F),
            };

            PowerlinePoleHelper.PowerlineSocketRotations = new List<Vector3>
            {
                Vector3.forward,
                Vector3.forward,

                Vector3.forward,
                Vector3.forward,
            };

            PowerlineTransformerGUI.InitializeStatic();

            XmasColorSequence = new Color[]
            {
                Color.red,
                Color.green,
                Color.blue,
                Color.magenta,
                Color.yellow
            };


            ValidTools = new ListHashSet<string>
            {
                ITEM_LIGHTS,
                ITEM_WIRETOOL,
                ITEM_HOSETOOL,
            };

            ThePowerGridGameObject = new GameObject("ThePowerGrid");
            ThePowerGridGameObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            StreetLampHelper.StreetLampHelperCache = new ListHashSet<StreetLampHelper>();
            PowerlinePoleHelper.PowerlinePoleHelperCache = new Hash<uint, PowerlinePoleHelper>();
            PowerlinePoleHelper.AllIOEntitiesToPowerlinePole = new Hash<uint, PowerlinePoleHelper>();
            PowerlinePoleHelper.TemporaryVisibilityList = new List<BaseEntity>();
            PowerlinePoleHelper.CombinerToPowerlinePoleOutlet = new Hash<uint, PowerlinePoleOutlet>();
            PowerlinePoleHelper.ButtonToPowerlinePole = new Hash<uint, PowerlinePoleHelper>();
            PowerlinePoleHelper.FuseboxToPowerlinePole = new Hash<uint, PowerlinePoleHelper>();
            PowerlinePoleHelper.HardwiredSlotToEntityNetID = new Hash<IOEntity.IOSlot, uint>();
            PowerlinePoleHelper.DangerousSlotToEntityNetID = new Hash<IOEntity.IOSlot, uint>();

            VanillaXmasLightsHelper.Cache = new Hash<uint, VanillaXmasLightsHelper>();

            PlayerHelper.Cache = new Hash<ulong, PlayerHelper>();

            LoadConfigData();
            ProcessConfigData();

            ThePowerGridGameObject.AddComponent<PowerGrid>().Prepare();

            GenerateAllInteractiveConfigValues();

            LoadData();
            ProcessData();

            Instance.PrintWarning("Looking for streetlights...");

            var iterateOver = UnityEngine.Object.FindObjectsOfType<MeshCollider>();

            foreach (var collider in iterateOver)
            {
                if (!(collider.name == COLLIDER_NAME_STREETLAMP_LEFT || collider.name == COLLIDER_NAME_STREETLAMP_RIGHT))
                {
                    continue;
                }

                Instance.PrintWarning($"Found a street lamp at {collider.transform.position}, activating helper if it doesn't have one...");

                if (collider.transform.parent.gameObject.HasComponent<StreetLampHelper>())
                {
                    Instance.PrintWarning($"It DOES have one already. NEXT...");
                    continue;
                }

                Instance.PrintWarning($"ADDING COMPO TO PARENT...");
                collider.transform.parent.gameObject.AddComponent<StreetLampHelper>().Prepare();
            }

            permission.RegisterPermission(PERM_ADMIN, this);
            permission.RegisterPermission(PERM_VIP1, this);
            permission.RegisterPermission(PERM_VIP2, this);
            permission.RegisterPermission(PERM_VIP3, this);
            permission.RegisterPermission(PERM_VIP4, this);
            permission.RegisterPermission(PERM_VIP5, this);



            AddCovalenceCommand(CMD_GP_CFG, nameof(CommandGPConfig));
            AddCovalenceCommand(CMD_GP_EMERGENCY_CLEANUP, nameof(CommandEmergencyCleanup));

            PlayerHelper.GuiPrepareStatic();

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsSleeping()) return;
                if (player.IsDead()) return;

                OnPlayerSleepEnded(player);
            }

            //street lamps
        }

        object CanPickupEntity(BasePlayer player, ElectricalCombiner combiner)
        {
            if (Instance == null) return null;

            if (combiner == null) return null;

            if (combiner.net == null) return null;

            if (!IsMarkedInternal(combiner))
            {
                return null;
            }

            if (!PowerlinePoleHelper.CombinerToPowerlinePoleOutlet.ContainsKey(combiner.net.ID))
            {
                return null;
            }

            if (!ShouldPlayerDieFromHighVoltage(player, PowerlinePoleHelper.CombinerToPowerlinePoleOutlet[combiner.net.ID].PoleHelper))
            {
                return null;
            }

            PowerlinePoleHelper.CombinerToPowerlinePoleOutlet[combiner.net.ID].PoleHelper.ElectricutePlayerToDeath(player);

            return null;
        }

        ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (Instance == null)
            {
                return null;
            }

            if (item == null)
            {
                return null;
            }

            if (container == null)
            {
                return null;
            }

            BaseEntity ownerThing = GetOwnerThing(container);

            if (ownerThing == null)
            {
                return null;
            }

            if (!IsMarkedInternal(ownerThing))
            {
                return null;
            }

            if (ownerThing.net == null)
            {
                return null;
            }

            if (!PowerlinePoleHelper.FuseboxToPowerlinePole.ContainsKey(ownerThing.net.ID))
            {
                return null;
            }

            return PowerlinePoleHelper.FuseboxToPowerlinePole[ownerThing.net.ID].CanItemBeAddedToFusebox(item);
        }
        void OnLootEntity(BasePlayer player, ItemBasedFlowRestrictor fusebox)
        {
            if (Instance == null) return;

            if (fusebox == null) return;

            if (!IsMarkedInternal(fusebox))
            {
                return;
            }

            if (!PowerlinePoleHelper.FuseboxToPowerlinePole.ContainsKey(fusebox.net.ID))
            {
                return;
            }

            PowerlinePoleHelper.FuseboxToPowerlinePole[fusebox.net.ID].PlayerOpenedFusebox(player);

            PowerlineTransformerGUI.PlayerOpenedFusebox(player, PowerlinePoleHelper.FuseboxToPowerlinePole[fusebox.net.ID]);            
        }

        void OnLootEntityEnd(BasePlayer player, ItemBasedFlowRestrictor fusebox)
        {
            if (Instance == null) return;

            if (fusebox == null) return;


            if (!IsMarkedInternal(fusebox))
            {
                //PowerlineTransformerGUI.PlayerClosedFusebox(player);
                return;
            }


            if (fusebox.net == null) return;

            if (PowerlinePoleHelper.FuseboxToPowerlinePole.ContainsKey(fusebox.net.ID))
            {
                //this will also clear the gui
                PowerlinePoleHelper.FuseboxToPowerlinePole[fusebox.net.ID].PlayerClosedFusebox(player);
            }
            else
            {
                //just clear the gui
                PowerlineTransformerGUI.PlayerClosedFusebox(player);
            }
        }

        void OnLoseCondition(Item item, ref float amount)
        {
            if (Instance == null)
            {
                return;
            }

            if (item == null)
            {
                return;
            }

            if (item.info.shortname != ITEM_FUSE)
            {
                return;
            }

            ItemContainer container = item.GetRootContainer();

            if (container == null)
            {
                return;
            }

            BaseEntity ownerThing = GetOwnerThing(container);

            if (ownerThing == null)
            {
                return;
            }

            if (!IsMarkedInternal(ownerThing))
            {
                return;
            }

            if (!PowerlinePoleHelper.FuseboxToPowerlinePole.ContainsKey(ownerThing.net.ID))
            {
                return;
            }

            PowerlinePoleHelper.FuseboxToPowerlinePole[ownerThing.net.ID].WhenFuseInsideLosesCondition(item, ref amount);

        }
        object OnButtonPress(PressButton button, BasePlayer player)
        {
            if (Instance == null)
            {
                return null;
            }

            if (button.PrefabName != PREFAB_BUTTON)
            {
                return null;
            }

            if (!PowerlinePoleHelper.ButtonToPowerlinePole.ContainsKey(button.net.ID))
            {
                return null;
            }

            PowerlinePoleHelper.ButtonToPowerlinePole[button.net.ID].WhenButtonPressed(player);

            return null;
        }

        void OnEntityKill(PressButton button)
        {
            if (Instance == null)
            {
                return;
            }

            if (!IsMarkedInternal(button))
            {
                return;
            }

            if (!PowerlinePoleHelper.ButtonToPowerlinePole.ContainsKey(button.net.ID))
            {
                return;
            }

            PowerlinePoleHelper.ButtonToPowerlinePole[button.net.ID].WhenButtonKilled();

        }

        void OnEntityKill(ElectricalCombiner combiner)
        {
            if (Instance == null)
            {
                return;
            }

            if (!IsMarkedInternal(combiner))
            {
                return;
            }

            if (!PowerlinePoleHelper.CombinerToPowerlinePoleOutlet.ContainsKey(combiner.net.ID))
            {
                return;
            }

            PowerlinePoleHelper.CombinerToPowerlinePoleOutlet[combiner.net.ID].PoleHelper.WhenOutletKilledByNetID(combiner.net.ID);

        }

        object OnWireCommon(BasePlayer player, IOEntity.IOSlot slot1, IOEntity.IOSlot slot2)
        {
            if (Instance == null)
            {
                return null;
            }

            object result = (object)null;

            bool slot1isHardwired = false;
            bool slot1isDangerous = false;

            bool slot2isHardwired = false;
            bool slot2isDangerous = false;

            uint entNetID1 = 0;
            uint entNetID2 = 0;

            bool atLeastOneCase = false;

            //if hardwired: don't let them make change
            //if dangerous: don't let them make change and also electricute, both per random chance for each entity involved

            if (slot1 != null)
            {
                if (IsIOEntitySlotMarkedHardwired(slot1))
                {
                    result = true;
                    slot1isHardwired = true;
                    entNetID1 = PowerlinePoleHelper.HardwiredSlotToEntityNetID[slot1];
                }

                if (IsIOEntitySlotMarkedDangerous(slot1))
                {
                    result = true;
                    slot1isDangerous = true;
                    entNetID1 = PowerlinePoleHelper.DangerousSlotToEntityNetID[slot1];
                }
            }

            if (slot2 != null)
            {
                if (IsIOEntitySlotMarkedHardwired(slot2))
                {
                    result = true;
                    slot2isHardwired = true;
                    entNetID2 = PowerlinePoleHelper.HardwiredSlotToEntityNetID[slot2];
                }

                if (IsIOEntitySlotMarkedDangerous(slot2))
                {
                    result = true;
                    slot2isDangerous = true;
                    entNetID2 = PowerlinePoleHelper.DangerousSlotToEntityNetID[slot2];
                }
            }

            PowerlinePoleHelper helperFound = null;

            if (result != null)
            {
                //INTERNAL or DANGEROUS, so it means one of them must belong to the grid

                if (!GetPermissionProfile(player).GridCanConnectDisconnect)
                {
                    TellMessage(player, MSG(MSG_CANT_CONNECT_DISCONNECT_GRID, player.UserIDString));

                    //return non null
                    return true;
                }


                if (slot1isDangerous || slot2isDangerous)
                {
                    if (entNetID1 != 0)
                    {
                        helperFound = PowerlinePoleHelper.AllIOEntitiesToPowerlinePole[entNetID1];

                        if (helperFound != null)
                        {
                            if (ShouldPlayerDieFromHighVoltage(player, helperFound))
                            {
                                atLeastOneCase = true;
                            }
                        }
                    }
                    
                    if (atLeastOneCase == false)
                    {
                        if (entNetID2 != 0)
                        {
                            helperFound = PowerlinePoleHelper.AllIOEntitiesToPowerlinePole[entNetID2];

                            if (helperFound != null)
                            {
                                if (ShouldPlayerDieFromHighVoltage(player, helperFound))
                                {
                                    atLeastOneCase = true;
                                }
                            }
                        }
                    }
                }

                if (atLeastOneCase)
                {
                    helperFound.ElectricutePlayerToDeath(player);
                    return true;
                }
                else
                {
                    //just marked hardwired?
                    if (slot1isHardwired || slot2isHardwired)
                    {
                        return true;
                    }
                    else
                    {
                        //not hardwired or dangerous, let it through.
                        return null;
                    }
                }
            }

            return null;
        }

        object OnWireConnect(BasePlayer player, IOEntity entity1, int inputs, IOEntity entity2, int outputs)
        {
            if (Instance == null)
            {
                return null;
            }

            var slot1 = entity1.inputs[inputs];
            var slot2 = entity2.outputs[outputs];

            object result = OnWireCommon(player, slot1, slot2);

            if (result != null)
            {
                return result;
            }

            //unmark outputEntity as baked...
            //BUT WHY?
            if (IsMarkedBaked(entity2))
            {
                entity2.SetFlag(FLAG_BAKED, false, false, true);
            }

            if (!GetPermissionProfile(player).HangingWiresAndHoses)
            {
                return null;
            }

            //dont do this for real tiny slacks.
            if (GetPlayerConfig(player).Slack < 0.01F)
            {
                return null;
            }
            //check if player has it enabled right now according to the profile?

            //get slack and colour from player's profile
            //subdivisions from the config

            MakeHangingWireConnection(entity1, entity2, inputs, outputs, GetPlayerConfig(player).Slack, GetPermissionProfile(player).SubdivisionsFinal);

            return null;
        }

        //flag true means inputs, flag false means outputs
        object OnWireClear(BasePlayer player, IOEntity entity1, int connecteds, IOEntity entity2, bool flag)
        {
            if (Instance == null)
            {
                return null;
            }

            IOEntity.IOSlot slot1 = null;
            IOEntity.IOSlot slot2 = null;

            if (flag)
            {
                if (entity1 != null)
                {
                    slot1 = entity1.inputs[connecteds];
                }

                if (entity2 != null)
                {
                    slot2 = entity2.outputs[slot1.connectedToSlot];
                }

            }
            else
            {
                if (entity1 != null)
                {
                    slot1 = entity1.outputs[connecteds];
                }

                if (entity2 != null)
                {
                    slot2 = entity2.inputs[slot1.connectedToSlot];
                }
            }

            return OnWireCommon(player, slot1, slot2);
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (Instance == null)
            {
                return;
            }

            if (!PlayerHelper.Cache.ContainsKey(player.userID))
            {
                return;
            }

            PlayerHelper.Cache[player.userID].WhenPlayerInput(input);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (Instance == null)
            {
                return;
            }

            if (IsPlayerNPC(player))
            {
                return;
            }

            if (!PlayerHelper.Cache.ContainsKey(player.userID))
            {
                player.gameObject.AddComponent<PlayerHelper>().Prepare(player);
            }
        }

        private object OnPlayerDeath(BasePlayer player, HitInfo info) => OnPlayerMissingCommon(player);

        private void OnPlayerDisconnected(BasePlayer player, string reason) => OnPlayerMissingCommon(player);

        //not actually a hook
        private object OnPlayerMissingCommon(BasePlayer player)
        {
            if (Instance == null)
            {
                return null;
            }

            if (IsPlayerNPC(player))
            {
                return null;
            }

            if (PlayerHelper.Cache.ContainsKey(player.userID))
            {
                UnityEngine.Object.Destroy(PlayerHelper.Cache[player.userID]);
            }

            return null;
        }

        private void Unload()
        {
            Unloading = true;

            foreach (var helper in PowerlinePoleHelper.PowerlinePoleHelperCache.ToArray())
            {
                UnityEngine.Object.DestroyImmediate(helper.Value.gameObject);
            }

            UnityEngine.Object.DestroyImmediate(ThePowerGridGameObject);


            foreach (var helper in StreetLampHelper.StreetLampHelperCache.ToArray())
            {
                UnityEngine.Object.DestroyImmediate(helper);
            }



            StreetLampHelper.StreetLampHelperCache = null;
            PowerlinePoleHelper.PowerlinePoleHelperCache = null;
            PowerlinePoleHelper.AllIOEntitiesToPowerlinePole = null;
            PowerlinePoleHelper.TemporaryVisibilityList = null;
            PowerlinePoleHelper.PowerlineSocketPositions = null;
            PowerlinePoleHelper.PowerlineSocketRotations = null;
            PowerlinePoleHelper.CombinerToPowerlinePoleOutlet = null;
            PowerlinePoleHelper.ButtonToPowerlinePole = null;
            PowerlinePoleHelper.FuseboxToPowerlinePole = null;
            PowerlinePoleHelper.HardwiredSlotToEntityNetID = null;
            PowerlinePoleHelper.DangerousSlotToEntityNetID = null;

            //remove all monos
            foreach (var helper in VanillaXmasLightsHelper.Cache)
            {
                UnityEngine.Object.DestroyImmediate(helper.Value);
            }
            VanillaXmasLightsHelper.Cache = null;

            foreach (var helper in PlayerHelper.Cache)
            {
                UnityEngine.Object.DestroyImmediate(helper.Value);
            }
            PlayerHelper.Cache = null;

            PowerlineTransformerGUI.UnloadStatic();

            //null out static

            PlayerHelper.GuiUnloadStatic();

            ValidTools = null;

            if (Data.Dirty)
            {
                SaveData();
            }

            Unloading = false;

            ConfigValues = null;

            XmasColorSequence = null;

            Instance = null;
            NewSave = false;

            FuseboxAllowedItems = null;

            LadderConstruction = null;


            ThePowerGridGameObject = null;
        }
        void OnNewSave(string strFilename)
        {
            NewSave = true;
        }

        void OnServerSave()
        {
            //might be a chonky file, let's do it on a timer

            timer.Once(UnityEngine.Random.Range(0F, 30F), () =>
            {
                if (Instance == null)
                {
                    return;
                }

                if (Data == null)
                {
                    return;
                }

                if (Data.Dirty)
                {
                    SaveData();
                }
            });

        }

        public static BasePlayer TryToGetPlayerDeployingLights(AdvancedChristmasLights lights)
        {
            BasePlayer foundPlayer = null;

            List<BaseEntity> visibleEntities = Facepunch.Pool.GetList<BaseEntity>();

            Vis.Entities(lights.transform.position, 5F, visibleEntities, 1076005121, QueryTriggerInteraction.Ignore);

            if (visibleEntities.Count == 0)
            {
                return null;
            }

            BasePlayer currentPlayer;

            PoweredLightsDeployer maybePoweredLights;

            for (var i = 0; i < visibleEntities.Count; i++)
            {
                currentPlayer = visibleEntities[i] as BasePlayer;

                if (currentPlayer == null)
                {
                    continue;
                }

                if (currentPlayer.IsSleeping())
                {
                    continue;
                }

                maybePoweredLights = currentPlayer.GetHeldEntity() as PoweredLightsDeployer;

                if (maybePoweredLights == null)
                {
                    continue;
                }
                //so far so good, check it?

                if (maybePoweredLights.activeLights.Get(true)?.EqualNetID(lights) ?? false)
                {
                    continue;
                }

                //well we found it.
                //that was a soddin hacky way around it...
                //IF ONLY THERE WAS OWNER ID.
                foundPlayer = currentPlayer;
                break;
            }
            Facepunch.Pool.FreeList(ref visibleEntities);

            return foundPlayer;
        }
        private object OnEntityTakeDamage(BaseLadder ladder, HitInfo info) => OnEntityTakeDamageGenericPrevent(ladder, info);
        private object OnEntityTakeDamage(ElectricalCombiner combiner, HitInfo info) => OnEntityTakeDamageGenericPrevent(combiner, info);
        private object OnEntityTakeDamage(ElectricGenerator generator, HitInfo info) => OnEntityTakeDamageGenericPrevent(generator, info);
        private object OnEntityTakeDamage(TeslaCoil coil, HitInfo info) => OnEntityTakeDamageGenericPrevent(coil, info);

        private object OnEntityTakeDamageGenericPrevent(BaseEntity entity, HitInfo info)
        {
            if (entity == null)
            {
                return null;
            }

            if (info == null)
            {
                return null;
            }

            if (IsMarkedInternal(entity))
            {

                //if it's not indestructible, don't go further if it's NOT decay (apply normal damage)
                if (!IsMarkedIndestructible(entity))
                {
                    if (info.damageTypes.GetMajorityDamageType() != Rust.DamageType.Decay)
                    {
                        return null;
                    }
                }
            }
            else
            {
                //ignore non-internal
                return null;
            }

            info.damageTypes.ScaleAll(0F);

            return null;
        }

        private void OnEntitySpawned(AdvancedChristmasLights lights)
        {
            if (Instance == null)
            {
                return;
            }

            if (lights == null)
            {
                return;
            }

            if (lights.net == null)
            {
                return;
            }


            //means it's already done
            if (IsMarkedBaked(lights))
            {
                return;
            }

            //check for multi-tool ones...
            BasePlayer foundPlayer;

            foundPlayer = TryToGetPlayerDeployingLights(lights);

            if (foundPlayer == null)
            {
                return;
            }

            //is the player currently holding a multi tool?
            if (!PlayerHelper.Cache.ContainsKey(foundPlayer.userID))
            {
                return;
            }

            //this is vanilla+ editor

            if (!GetPermissionProfile(foundPlayer).HangingXmasLights)
            {
                return;
            }

            if (VanillaXmasLightsHelper.Cache.ContainsKey(lights.net.ID))
            {
                return;
            }

            lights.gameObject.AddComponent<VanillaXmasLightsHelper>().Prepare(foundPlayer, lights);
        }

        #endregion

        #region COLORS
        public class ColorCode
        {
            public string hexValue;
            public UnityEngine.Color unityColorValue;
            public string unityColorString;
            public ColorCode(string hex)
            {
                hex = hex.ToUpper();

                hexValue = "#" + hex;

                //extract the R, G, B
                var r = (float)short.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255;
                var g = (float)short.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255;
                var b = (float)short.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255;

                unityColorValue = new UnityEngine.Color(r, g, b);
                unityColorString = $"{r} {g} {b}";
            }
        }

        public static class ColorPalette
        {
            public static ColorCode ElectricOutputEngaged = new ColorCode("cf802b");
            public static ColorCode ElectricOutputFree = new ColorCode("cfa77c");
            public static ColorCode ElectricInputEngaged = new ColorCode("cfb62b");
            public static ColorCode ElectricInputFree = new ColorCode("cfc27c");

            public static ColorCode FluidicOutputEngaged = new ColorCode("2badce");
            public static ColorCode FluidicOutputFree = new ColorCode("7cbecf");

            public static ColorCode FluidicInputEngaged = new ColorCode("2bcfb9");
            public static ColorCode FluidicInputFree = new ColorCode("7ccfc4");

            public static ColorCode KineticOutputEngaged = new ColorCode("2bcf2e");
            public static ColorCode KineticOutputFree = new ColorCode("7ccf7d");
            public static ColorCode KineticInputEngaged = new ColorCode("2bcf65");
            public static ColorCode KineticInputFree = new ColorCode("7ccf99");

            public static ColorCode GenericOutputEngaged = new ColorCode("cf2b5c");
            public static ColorCode GenericOutputFree = new ColorCode("cf7c95");
            public static ColorCode GenericInputEngaged = new ColorCode("ce422b");
            public static ColorCode GenericInputFree = new ColorCode("cf877c");

            public static ColorCode RustyDirtyPink = new ColorCode("e8dcd3");
            public static ColorCode RustyGrey = new ColorCode("938c84");
            public static ColorCode RustyBlack = new ColorCode("1e2020");

            public static ColorCode RustyRed = new ColorCode("ce422b");
            public static ColorCode RustyOrange = new ColorCode("ce722b");
            public static ColorCode RustyYellow = new ColorCode("cfb62b");
            public static ColorCode RustyGreen = new ColorCode("93cf2b");
            public static ColorCode RustyBlgp = new ColorCode("2b88cf");
        }

        #endregion

        #region API

        #endregion

        #region VARIOUS STATIC HELPERS

        public const int SCREEN_WIDTH_IN_PIXELS = 1920;
        public const int SCREEN_HEIGHT_IN_PIXELS = 1080;

        public static CuiRectTransformComponent GetAnchorFromScreenBox(float leftmostX, float bottommostY, float rightmostX, float topmostY)
        {
            return new CuiRectTransformComponent { AnchorMin = $"{ScreenToRustX(leftmostX).ToString()} {ScreenToRustY(bottommostY).ToString()}", AnchorMax = $"{ScreenToRustX(rightmostX).ToString()} {ScreenToRustY(topmostY).ToString()}" };
        }
        public static CuiRectTransformComponent GetAnchorResizedRelative(CuiRectTransformComponent originalAnchor, float xLeft, float yBottom, float xRight, float yTop, float paddingInPixels = 0)
        {
            var paddingH = ScreenToRustX(paddingInPixels);
            var paddingV = 1F - ScreenToRustY(paddingInPixels);

            //let's split parse some strings, shall we.
            var anchorMinSplit = originalAnchor.AnchorMin.Split(' ');
            var anchorMaxSplit = originalAnchor.AnchorMax.Split(' ');

            float anchorMinX = float.Parse(anchorMinSplit[0]);
            float anchorMinY = float.Parse(anchorMinSplit[1]);

            float anchorMaxX = float.Parse(anchorMaxSplit[0]);
            float anchorMaxY = float.Parse(anchorMaxSplit[1]);

            float width = anchorMaxX - anchorMinX;
            float height = anchorMaxY - anchorMinY;

            float xLeftNew = anchorMinX + xLeft * width;
            float yBottomNew = anchorMinY + yBottom * height;

            float xRightNew = anchorMaxX - (1 - xRight) * width;
            float yTopNew = anchorMaxY - (1 - yTop) * height;

            return new CuiRectTransformComponent { AnchorMin = $"{xLeftNew + paddingH} {yBottomNew + paddingV}", AnchorMax = $"{xRightNew - paddingH} {yTopNew - paddingV}" };
        }
        public static float ScreenToRustX(float x)
        {
            return WidthToRustX(x, SCREEN_WIDTH_IN_PIXELS);
        }
        public static float ScreenToRustY(float y)
        {
            return HeightToRustY(y, SCREEN_HEIGHT_IN_PIXELS);
        }
        public static float WidthToRustX(float x, float width)
        {
            return (float)x / width;
        }
        public static float HeightToRustY(float y, float height)
        {
            return 1 - ((float)y / height);
        }

        public static bool IsPlacementBlockedAt(Vector3 worldPos)
        {
            return TerrainMeta.PlacementMap.GetBlocked(worldPos);
        }

        private static GroundWatch _reusableGroundWatch;
        private static DestroyOnGroundMissing _reusableDestroyOnGroundMissing;
        private static Vector3 _reusablePos1;
        private static Vector3 _reusablePos2;
        private static Vector3 _reusableRot1;
        private static Vector3 _reusableRot2;

        #region SERVER SIDED RE-IMPLEMENTATIONS
        public static float GetFuseTimeLeftSeconds(float fuseConditionNormalized)
        {
            return fuseConditionNormalized * Instance.Configuration.PowerlineFuseDurationSeconds;
        }

        public static string GetFormattedTimeLeft(float totalSeconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(totalSeconds);
            return time.ToString(@"hh\:mm\:ss");
        }

        public static string GetFormattedHour(float fractionalHours)
        {
            int wholeHours = Mathf.FloorToInt(fractionalHours);
            int wholeMinutes = Mathf.FloorToInt((fractionalHours - wholeHours)*60F);

            return MSG(MSG_POWERLINE_TIME_FORMAT_HOUR_MINUTE, null, wholeHours.ToString("00"), wholeMinutes.ToString("00"));
        }

        public static void PayForPlacement(Planner planner, Item ownerItem, ItemModDeployable deployable, BasePlayer player, Construction component)
        {
            if (Interface.CallHook("OnPayForPlacement", player, planner, component) != null)
            {
                return;
            }
            if (deployable != null)
            {
                ownerItem.UseItem();
                return;
            }
            List<Item> list = new List<Item>();
            foreach (ItemAmount item in component.defaultGrade.costToBuild)
            {
                player.inventory.Take(list, item.itemDef.itemid, (int)item.amount);
                player.Command("note.inv", item.itemDef.itemid, item.amount * -1f);
            }
            foreach (Item item2 in list)
            {
                item2.Remove();
            }
        }


        public static void DoBuild(Planner planner, BasePlayer ownerPlayer, Item ownerItem, Construction.Target target, Construction component)
        {
            if (!ownerPlayer || RayEx.IsNaNOrInfinity(target.ray) || target.position.IsNaNOrInfinity() || target.normal.IsNaNOrInfinity())
            {
                return;
            }
            if (target.socket != null)
            {
                if (!target.socket.female)
                {
                    ownerPlayer.ChatMessage("Target socket is not female. (" + target.socket.socketName + ")");
                    return;
                }
                if (target.entity != null && target.entity.IsOccupied(target.socket))
                {
                    ownerPlayer.ChatMessage("Target socket is occupied. (" + target.socket.socketName + ")");
                    return;
                }
                if (target.onTerrain)
                {
                    ownerPlayer.ChatMessage("Target on terrain is not allowed when attaching to socket. (" + target.socket.socketName + ")");
                    return;
                }
            }
            if (ConVar.AntiHack.eye_protection >= 2)
            {
                Vector3 center = ownerPlayer.eyes.center;
                Vector3 position = ownerPlayer.eyes.position;
                Vector3 origin = target.ray.origin;
                Vector3 p = ((target.entity != null && target.socket != null) ? target.GetWorldPosition() : target.position);
                if (target.entity != null)
                {
                    DeployShell deployShell = PrefabAttribute.server.Find<DeployShell>(target.entity.prefabID);
                    if (deployShell != null)
                    {
                        p += target.normal.normalized * deployShell.LineOfSightPadding();
                    }
                }
                int num = 2097152;
                int num2 = (ConVar.AntiHack.build_terraincheck ? 10551296 : 2162688);
                if (!GamePhysics.LineOfSight(padding: (target.socket != null) ? 0.5f : 0.01f, layerMask: (target.socket != null) ? num : num2, p0: center, p1: position, p2: origin, p3: p))
                {
                    ownerPlayer.ChatMessage("Line of sight blocked.");
                    return;
                }
            }
            Construction.lastPlacementError = "No Error";

            GameObject gameObject = DoPlacement(planner, ownerItem, ownerPlayer, target, component, out Construction.lastPlacementError);
            if (gameObject == null)
            {
                ownerPlayer.ChatMessage("Can't place: " + Construction.lastPlacementError);
            }
            if (!(gameObject != null))
            {
                return;
            }
            Interface.CallHook("OnEntityBuilt", planner, gameObject);
            Deployable deployable = planner.GetDeployable();
            if (deployable != null)
            {
                BaseEntity baseEntity = GameObjectEx.ToBaseEntity(gameObject);
                if (deployable.setSocketParent && target.entity != null && target.entity.SupportsChildDeployables() && (bool)baseEntity)
                {
                    baseEntity.SetParent(target.entity, true);
                }
                if (deployable.wantsInstanceData && ownerItem.instanceData != null)
                {
                    (baseEntity as IInstanceDataReceiver).ReceiveInstanceData(ownerItem.instanceData);
                }
                if (deployable.copyInventoryFromItem)
                {
                    StorageContainer component2 = baseEntity.GetComponent<StorageContainer>();
                    if ((bool)component2)
                    {
                        component2.ReceiveInventoryFromItem(ownerItem);
                    }
                }
                ItemModDeployable modDeployable = planner.GetModDeployable();
                if (modDeployable != null)
                {
                    OnDeployed(modDeployable, baseEntity, ownerPlayer);
                }
                baseEntity.OnDeployed(baseEntity.GetParentEntity(), ownerPlayer, ownerItem);
                if (deployable.placeEffect.isValid)
                {
                    if ((bool)target.entity && target.socket != null)
                    {
                        Effect.server.Run(deployable.placeEffect.resourcePath, target.entity.transform.TransformPoint(target.socket.worldPosition), target.entity.transform.up);
                    }
                    else
                    {
                        Effect.server.Run(deployable.placeEffect.resourcePath, target.position, target.normal);
                    }
                }
            }
            PayForPlacement(planner, ownerItem, planner.GetModDeployable(), ownerPlayer, component);
        }

        public static GameObject DoPlacement(Planner planner, Item ownerItem, BasePlayer ownerPlayer, Construction.Target placement, Construction component, out string lastPlacementError)
        {
            lastPlacementError = "No error";

            if (!ownerPlayer)
            {
                return null;
            }
            BaseEntity baseEntity = CreateConstruction(component, placement, out lastPlacementError, true);
            if (!baseEntity)
            {
                return null;
            }
            float num = 1f;
            float num2 = 0f;

            if (ownerItem != null)
            {
                baseEntity.skinID = ownerItem.skin;
                if (ownerItem.hasCondition)
                {
                    num = ownerItem.conditionNormalized;
                }
            }
            PoolableEx.AwakeFromInstantiate(baseEntity.gameObject);
            BuildingBlock buildingBlock = baseEntity as BuildingBlock;
            if ((bool)buildingBlock)
            {
                buildingBlock.blockDefinition = PrefabAttribute.server.Find<Construction>(buildingBlock.prefabID);
                if (!buildingBlock.blockDefinition)
                {
                    Debug.LogError("Placing a building block that has no block definition!");
                    return null;
                }
                buildingBlock.SetGrade(buildingBlock.blockDefinition.defaultGrade.gradeBase.type);
                num2 = buildingBlock.currentGrade.maxHealth;
            }
            BaseCombatEntity baseCombatEntity = baseEntity as BaseCombatEntity;
            if ((bool)baseCombatEntity)
            {
                num2 = ((buildingBlock != null) ? buildingBlock.currentGrade.maxHealth : baseCombatEntity.startHealth);
                baseCombatEntity.ResetLifeStateOnSpawn = false;
                baseCombatEntity.InitializeHealth(num2 * num, num2);
            }
            if (Interface.CallHook("OnConstructionPlace", baseEntity, component, placement, ownerPlayer) != null)
            {
                if (BaseEntityEx.IsValid(baseEntity))
                {
                    baseEntity.KillMessage();
                }
                else
                {
                    GameManager.Destroy(baseEntity);
                }
                return null;
            }
            baseEntity.gameObject.SendMessage("SetDeployedBy", ownerPlayer, SendMessageOptions.DontRequireReceiver);
            baseEntity.OwnerID = ownerPlayer.userID;
            baseEntity.Spawn();
            if ((bool)buildingBlock)
            {
                Effect.server.Run("assets/bundled/prefabs/fx/build/frame_place.prefab", baseEntity, 0u, Vector3.zero, Vector3.zero);
            }
            StabilityEntity stabilityEntity = baseEntity as StabilityEntity;
            if ((bool)stabilityEntity)
            {
                stabilityEntity.UpdateSurroundingEntities();
            }
            return baseEntity.gameObject;
        }

        public static BaseEntity CreateConstruction(Construction construction, Construction.Target target, out string lastPlacementError, bool bNeedsValidPlacement = false)
        {
            GameObject gameObject = GameManager.server.CreatePrefab(construction.fullName, Vector3.zero, Quaternion.identity, false);
            bool flag = UpdatePlacement(gameObject.transform, construction, ref target, out lastPlacementError);
            BaseEntity baseEntity = GameObjectEx.ToBaseEntity(gameObject);
            if (bNeedsValidPlacement && !flag)
            {
                if (BaseEntityEx.IsValid(baseEntity))
                {
                    baseEntity.Kill();
                }
                else
                {
                    GameManager.Destroy(gameObject);
                }
                return null;
            }
            DecayEntity decayEntity = baseEntity as DecayEntity;
            if ((bool)decayEntity)
            {
                decayEntity.AttachToBuilding(target.entity as DecayEntity);
            }
            return baseEntity;
        }




        public static void OnDeployed(ItemModDeployable deployable, BaseEntity ent, BasePlayer player)
        {
            BuildingPrivlidge buildingPrivlidge;
            if ((object)(buildingPrivlidge = ent as BuildingPrivlidge) != null && Interface.CallHook("OnCupboardAuthorize", buildingPrivlidge, player) == null)
            {
                buildingPrivlidge.AddPlayer(player);
            }
        }



        private static bool UpdateGuide(GameObject guideObject, BasePlayer ownerPlayer, Construction currentConstruction, out string lastPlacementError, out Construction.Target finalPlacement, Vector3 rotationOffset = default(Vector3))
        {
            lastPlacementError = "No Error";

            if (rotationOffset == default(Vector3))
            {
                rotationOffset = Vector3.zero;
            }

            Construction.Target placement = default(Construction.Target);
            placement.ray = ownerPlayer.eyes.BodyRay();
            placement.player = ownerPlayer;
            FillPlacement(ref placement, currentConstruction, rotationOffset);

            finalPlacement = placement;

            return UpdateGuidePartTwo(guideObject, ref placement, currentConstruction, out lastPlacementError);
        }

        public static bool UpdateGuidePartTwo(GameObject guideObject, ref Construction.Target placement, Construction currentComponent, out string lastPlacementError)
        {

            //lastPlacementError = "No Error";
            //Vector3 position = guideObject.transform.position;
            //Quaternion rotation = guideObject.transform.rotation;
            Vector3 direction = placement.ray.direction;
            direction.y = 0f;
            direction.Normalize();
            guideObject.transform.position = placement.ray.origin + placement.ray.direction * currentComponent.maxplaceDistance;
            guideObject.transform.rotation = Quaternion.Euler(placement.rotation) * Quaternion.LookRotation(direction);

            bool num = UpdatePlacement(guideObject.transform, currentComponent, ref placement, out lastPlacementError);


            //bool flag = WaterLevel.Test(guideObject.transform.position + new Vector3(0f, currentComponent.bounds.min.y, 0f));

            //UpdateGuideTransparency(!flag);

            if ((bool)MainCamera.mainCamera)
            {
                guideObject.transform.position = guideObject.transform.position + (MainCamera.position - guideObject.transform.position).normalized * 0.05f;
            }
            /*
            if (num)
            {

                
                if (placement.inBuildingPrivilege)
                {
                    if (!placement.valid || !lastPlacement.inBuildingPrivilege)
                    {
                        BecomeNeutral();
                    }
                }
                else if (!lastPlacement.valid || lastPlacement.inBuildingPrivilege)
                {
                    BecomeValid();
                }
                lastPlacement = placement;
            }
            else if (lastPlacement.valid)
            {
                if (Vector3.Distance(position, guideObject.transform.position) < 0.25f && currentComponent.UpdatePlacement(guideObject.transform, component, ref lastPlacement))
                {
                    guideObject.transform.position = position;
                    guideObject.transform.rotation = rotation;
                }
                else
                {
                    lastPlacement.valid = false;
                    BecomeInvalid();
                }
            }*/
            return num;
        }

        public static bool TestPlacingThroughRock(Construction construction, ref Construction.Placement placement, Construction.Target target)
        {
            OBB oBB = new OBB(placement.position, Vector3.one, placement.rotation, construction.bounds);
            Vector3 center = target.player.GetCenter(true);
            Vector3 origin = target.ray.origin;
            if (Physics.Linecast(center, origin, 65536, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            RaycastHit hit;
            Vector3 end = (oBB.Trace(target.ray, out hit) ? hit.point : oBB.ClosestPoint(origin));
            if (Physics.Linecast(origin, end, 65536, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            return true;
        }

        public static bool TestPlacingThroughWall(ref Construction.Placement placement, Transform transform, Construction common, Construction.Target target)
        {
            Vector3 vector = placement.position - target.ray.origin;
            RaycastHit hitInfo;

            if (!Physics.Raycast(target.ray.origin, vector.normalized, out hitInfo, vector.magnitude, 2097152))
            {
                return true;
            }
            StabilityEntity stabilityEntity = RaycastHitEx.GetEntity(hitInfo) as StabilityEntity;
            if (stabilityEntity != null && target.entity == stabilityEntity)
            {
                return true;
            }
            if (vector.magnitude - hitInfo.distance < 0.2f)
            {
                return true;
            }

            transform.SetPositionAndRotation(hitInfo.point, placement.rotation);
            return false;
        }


        public static bool UpdatePlacement(Transform transform, Construction common, ref Construction.Target target, out string lastPlacementError)
        {
            lastPlacementError = "No error";

            if (!target.valid)
            {
                lastPlacementError = "Target not valid";
                return false;
            }
            if (!common.canBypassBuildingPermission && !target.player.CanBuild())
            {
                lastPlacementError = "Player doesn't have permission";
                return false;
            }

            List<Socket_Base> obj = Facepunch.Pool.GetList<Socket_Base>();
            common.FindMaleSockets(target, obj);


            foreach (Socket_Base item in obj)
            {
                Construction.Placement placement = null;
                if (target.entity != null && target.socket != null && target.entity.IsOccupied(target.socket))
                {
                    continue;
                }
                if (placement == null)
                {
                    placement = item.DoPlacement(target);
                }
                if (placement == null)
                {
                    continue;
                }
                if (!item.CheckSocketMods(placement))
                {
                    transform.position = placement.position;
                    transform.rotation = placement.rotation;
                    continue;
                }

                
                if (TestPlacingThroughRock(common, ref placement, target))
                {
                    transform.position = placement.position;
                    transform.rotation = placement.rotation;
                    lastPlacementError = "Placing through rock";
                    continue;
                }

                if (!TestPlacingThroughWall(ref placement, transform, common, target))
                {
                    transform.position = placement.position;
                    transform.rotation = placement.rotation;
                    lastPlacementError = "Placing through wall";
                    continue;
                }

                if (Vector3.Distance(placement.position, target.player.eyes.position) > common.maxplaceDistance + 1f)
                {
                    transform.position = placement.position;
                    transform.rotation = placement.rotation;
                    lastPlacementError = "Too far away";
                    continue;
                }

                DeployVolume[] volumes = PrefabAttribute.server.FindAll<DeployVolume>(common.prefabID);
                if (DeployVolume.Check(placement.position, placement.rotation, volumes))
                {
                    transform.position = placement.position;
                    transform.rotation = placement.rotation;
                    lastPlacementError = "Not enough space";
                    continue;
                }
                if (BuildingProximity.Check(target.player, common, placement.position, placement.rotation))
                {
                    transform.position = placement.position;
                    transform.rotation = placement.rotation;
                    continue;
                }
                if (common.isBuildingPrivilege && !target.player.CanPlaceBuildingPrivilege(placement.position, placement.rotation, common.bounds))
                {
                    transform.position = placement.position;
                    transform.rotation = placement.rotation;
                    lastPlacementError = "Cannot stack building privileges";
                    continue;
                }
                bool flag = target.player.IsBuildingBlocked(placement.position, placement.rotation, common.bounds);
                if (!common.canBypassBuildingPermission && flag)
                {
                    transform.position = placement.position;
                    transform.rotation = placement.rotation;
                    lastPlacementError = "Building privilege";
                    continue;
                }

                target.inBuildingPrivilege = flag;
                transform.position = placement.position;
                transform.rotation = placement.rotation;
                Facepunch.Pool.FreeList(ref obj);
                return true;
            }

            Facepunch.Pool.FreeList(ref obj);
            return true;
        }



        public static void AdjustTarget(SocketHandle handle, ref Construction.Target target, float maxplaceDistance)
        {
            Vector3 worldPosition = handle.worldPosition;
            Vector3 a = target.ray.origin + target.ray.direction * maxplaceDistance - worldPosition;
            target.ray.direction = (a - target.ray.origin).normalized;
        }

        public static void FillPlacement(ref Construction.Target target, Construction component, Vector3 rotationOffset)
        {


            if ((bool)component.socketHandle)
            {
                AdjustTarget(component.socketHandle, ref target, component.maxplaceDistance);
            }
            FindAppropriateHandle(ref target, component, rotationOffset);
            if (!target.valid)
            {
                FindTerrainPlacement(ref target, component, rotationOffset);
                //_ = target.valid;
            }
        }

        public static bool FindAppropriateHandle(ref Construction.Target target, Construction component, Vector3 rotationOffset)
        {
            List<BaseEntity> obj = Facepunch.Pool.GetList<BaseEntity>();
            BasePlayer player = target.player;
            Ray ray = target.ray;
            float num = float.MaxValue;
            target.valid = false;
            Vis.Entities(ray.origin, component.maxplaceDistance * 2f, obj, 18874625);
            foreach (BaseEntity item in obj)
            {
                if (item.isServer)
                {
                    continue;
                }

                Construction construction = PrefabAttribute.server.Find<Construction>(item.prefabID);
                if (construction == null)
                {
                    continue;
                }
                Socket_Base[] allSockets = construction.allSockets;

                RaycastHit hit;

                foreach (Socket_Base socket_Base in allSockets)
                {
                    if (socket_Base.female && !socket_Base.femaleDummy && socket_Base.GetSelectBounds(item.transform.position, item.transform.rotation).Trace(ray, out hit) && !(hit.distance < 1f) && !(hit.distance > num) && !item.IsOccupied(socket_Base))
                    {
                        Construction.Target target2 = default(Construction.Target);
                        target2.socket = socket_Base;
                        target2.entity = item;
                        target2.ray = ray;
                        target2.valid = true;
                        target2.player = player;
                        target2.rotation = rotationOffset;
                        if (component.HasMaleSockets(target2))
                        {
                            target = target2;
                            num = hit.distance;
                        }
                    }
                }
            }
            Facepunch.Pool.FreeList(ref obj);
            return target.valid;
        }


        public static void FindTerrainPlacement(ref Construction.Target target, Construction component, Vector3 rotationOffset)
        {
            RaycastHit hitInfo;

            if (GamePhysics.Trace(target.ray, 0f, out hitInfo, component.maxplaceDistance + 0f, 161546496, QueryTriggerInteraction.Ignore))
            {
                target.position = target.ray.origin + target.ray.direction * hitInfo.distance;
                target.normal = hitInfo.normal;
                target.rotation = rotationOffset;
                target.onTerrain = true;
                target.valid = true;
                if ((bool)hitInfo.collider)
                {
                    target.entity = hitInfo.collider.gameObject.ToBaseEntity();
                }
            }
            else
            {
                target.position = target.ray.origin + target.ray.direction * component.maxplaceDistance;
                target.normal = Vector3.up;
                target.rotation = rotationOffset;
                target.onTerrain = component.canPlaceAtMaxDistance;
                target.valid = component.canPlaceAtMaxDistance;
            }
        }

        public static BaseEntity GetOwnerThing(ItemContainer container)
        {
            if (container == null)
            {
                return null;
            }

            if (container.entityOwner != null)
            {
                return container.entityOwner;
            }
            else
                return container.playerOwner != null ? container.playerOwner : null;
        }
        #endregion

        public static bool IsIOEntitySlotMarkedHardwired(IOEntity.IOSlot slot)
        {
            return PowerlinePoleHelper.HardwiredSlotToEntityNetID.ContainsKey(slot);
        }
        public static bool IsIOEntitySlotMarkedDangerous(IOEntity.IOSlot slot)
        {
            return PowerlinePoleHelper.DangerousSlotToEntityNetID.ContainsKey(slot);
        }


        public static void MarkIOEntitySlotHardwired(IOEntity.IOSlot slot, IOEntity entity, string niceName = null)
        {
            if (IsIOEntitySlotMarkedHardwired(slot))
            {
                return;
            }

            if (niceName == null)
            {
                niceName = MSG(MSG_SLOT_NAME_INTERNAL, null);
            }

            PowerlinePoleHelper.HardwiredSlotToEntityNetID.Add(slot, entity.net.ID);
            slot.niceName = niceName;
            entity.SendNetworkUpdate();
        }

        public static void MarkIOEntitySlotDangerous(IOEntity.IOSlot slot, IOEntity entity, string niceName = null)
        {
            if (IsIOEntitySlotMarkedDangerous(slot))
            {
                return;
            }

            if (niceName == null)
            {
                niceName = MSG(MSG_SLOT_NAME_INTERNAL, null);
            }

            PowerlinePoleHelper.DangerousSlotToEntityNetID.Add(slot, entity.net.ID);
            slot.niceName = niceName;
            entity.SendNetworkUpdate();
        }

        public static void MarkIOEntitySlotSpecial(IOEntity.IOSlot slot, IOEntity entity, bool hardwired, bool dangerous, string niceName = null)
        {
            if (hardwired)
            {
                MarkIOEntitySlotHardwired(slot, entity, niceName);
            }

            if (dangerous)
            {
                MarkIOEntitySlotDangerous(slot, entity, niceName);
            }
        }

        public int TryEmergencyCleanup()
        {
            EmergencyCleanup = true;

            foreach (var helper in PowerlinePoleHelper.PowerlinePoleHelperCache)
            {
                UnityEngine.Object.DestroyImmediate(helper.Value.gameObject);
            }

            foreach (var helper in StreetLampHelper.StreetLampHelperCache)
            {
                UnityEngine.Object.DestroyImmediate(helper);
            }

            StreetLampHelper.StreetLampHelperCache.Clear();

            PowerlinePoleHelper.PowerlinePoleHelperCache.Clear();

            int removed = 0;

            var allEntities = BaseNetworkable.serverEntities.OfType<BaseEntity>().ToArray();
            if (allEntities.Length == 0)
            {
                return 0;
            }

            for (var i = 0; i < allEntities.Length; i++)
            {
                if (!IsMarkedInternal(allEntities[i]))
                {
                    continue;
                }

                allEntities[i].Kill(BaseNetworkable.DestroyMode.None);
                removed++;
            }

            EmergencyCleanup = false;

            return removed;
        }

        public static bool IsMarkedIndestructible(BaseEntity entity) => entity.HasFlag(FLAG_INDESTRUCTIBLE);
        public static bool IsMarkedInternal(BaseEntity entity) => entity.HasFlag(FLAG_INTERNAL);
        public static bool IsMarkedBaked(BaseEntity entity) => entity.HasFlag(FLAG_BAKED);

        public static bool ShouldPlayerDieFromHighVoltage(BasePlayer player, PowerlinePoleHelper helper)
        {
            if (!helper.HasFuseInside())
            {
                return false;
            }

            bool foundProtectiveClothing = false;
            Item currentItem;

            for (var i= 0; i<player.inventory.containerWear.capacity; i++)
            {
                currentItem = player.inventory.containerWear.GetSlot(i);
                if (currentItem == null)
                {
                    continue;
                }

                if (!Instance.Configuration.ProtectiveClothing.ContainsKey(currentItem.info.shortname))
                {
                    continue;
                }

                int hasSkinsDefined = Instance.Configuration.ProtectiveClothing[currentItem.info.shortname]?.Count ?? 0;

                if (hasSkinsDefined == 0)
                {
                    foundProtectiveClothing = true;
                    break;
                }
                else
                {
                    if (Instance.Configuration.ProtectiveClothing[currentItem.info.shortname].Contains(currentItem.skin))
                    {
                        foundProtectiveClothing = true;
                        break;
                    }
                }
            }

            //insta death. gotta wear clothing.
            if (!foundProtectiveClothing)
            {
                return true;
            }

            float dyingChance = GetPermissionProfile(player).GridDangerousWireElectricutionChance;

            if (dyingChance > 0F)
            {
                if (UnityEngine.Random.Range(0F, 1F) < dyingChance)
                {
                    return true;
                }
            }

            return false;
        }

        public static float GetGlobalPowerProduction(float hour)
        {
            if (!IsItTime(hour, Instance.Configuration.GridProductionStartAtHour, Instance.Configuration.GridProductionEndAtHour))
            {
                return 0F;
            }

            return (-Mathf.Cos(Mathf.InverseLerp(Instance.Configuration.GridProductionStartAtHour, Instance.Configuration.GridProductionEndAtHour, hour) * 2F * Mathf.PI) + 1F) / 2F;
        }

        public static bool IsItTime(float hour, float startAt, float endAt)
        {
            if (startAt > endAt)
            {
                return (hour > startAt && hour < 24F) || (hour < endAt);
            }
            else
            {
                return (hour > startAt && hour < endAt);
            }
        }

        public static float GetCurrentHour() => TOD_Sky.Instance.Cycle.Hour;
        public static float GetCurrentDay() => TOD_Sky.Instance.Cycle.Day;
        public static float GetCurrentMonth() => TOD_Sky.Instance.Cycle.Month;
        public static float GetCurrentYear() => TOD_Sky.Instance.Cycle.Year;

        public static void TranslateLinePointsFromWorldToLocal(Transform transform, Vector3[] linePoints)
        {
            for (var i = 0; i< linePoints.Length; i++)
            {
                linePoints[i] = transform.InverseTransformPoint(linePoints[i]);
            }
        }
        public static T SummonSpecialRelativeTo<T>(Transform transform, ref List<BaseEntity> visibilityList, string prefabName, Vector3 localPos, Vector3 localRot, bool indestructible) where T : BaseEntity
        {
            _reusablePos2 = transform.TransformPoint(localPos);
            _reusableRot2 = transform.eulerAngles + localRot;//transform.TransformDirection(localRot);

            return SummonSpecial<T>(ref visibilityList, prefabName, _reusablePos2, _reusableRot2, indestructible);
        }

        public static T SummonSpecial<T>(ref List<BaseEntity> visibilityList, string prefabName, Vector3 worldPos, Vector3 worldRot, bool indestructible) where T : BaseEntity
        {
            //summoning: try to find at that world position
            //if not exist, re-spawn it
            T found = null;

            foreach (var candidate in visibilityList)//list)
            {
                if (candidate.PrefabName != prefabName)
                {
                    continue;
                }

                if (Vector3.Distance(worldPos, candidate.transform.position) > 0.01F)
                {
                    continue;
                }

                found = candidate as T;
                break;
            }

            if (found == null)
            {
                found = SpawnSpecial<T>(prefabName, worldPos, worldRot, indestructible);
            }

            if (visibilityList.Contains(found))
            {
                visibilityList.Remove(found);
            }

            return found;
        }
        public static T SpawnSpecialRelativeTo<T>(Transform transform, string prefabName, Vector3 localPos, Vector3 localRot, bool indestructible) where T : BaseEntity
        {
            _reusablePos1 = transform.TransformPoint(localPos);

            _reusableRot1 = transform.eulerAngles + localRot;

            return SpawnSpecial<T>(prefabName, _reusablePos1, _reusableRot1, indestructible);
        }
        public static T SpawnSpecialParentedTo<T>(BaseEntity parent, string prefabName, Vector3 localPos, Vector3 localRot, bool indestructible) where T : BaseEntity
        {
            T newEntity = SpawnSpecialRelativeTo<T>(parent.transform, prefabName, localPos, localRot, indestructible);

            if (newEntity == null)
            {
                return null;
            }

            newEntity.SetParent(parent, true, true);

            newEntity.OwnerID = parent.OwnerID;

            return newEntity;
        }


        public static T SpawnSpecial<T>(string prefabName, Vector3 worldPos, Vector3 worldRot, bool indestructible) where T : BaseEntity
        {
            T newEntity = GameManager.server.CreateEntity(prefabName, worldPos, Quaternion.Euler(worldRot.x, worldRot.y, worldRot.z)) as T;

            if (newEntity == null)
            {
                return null;
            }

            newEntity.Spawn();

            //make stable
            _reusableGroundWatch = newEntity.gameObject.GetComponent<GroundWatch>();

            if (_reusableGroundWatch != null)
            {
                UnityEngine.Object.DestroyImmediate(_reusableGroundWatch);
            }

            _reusableDestroyOnGroundMissing = newEntity.gameObject.GetComponent<DestroyOnGroundMissing>();

            if (_reusableDestroyOnGroundMissing != null)
            {
                UnityEngine.Object.DestroyImmediate(_reusableDestroyOnGroundMissing);
            }

            newEntity.SetFlag(FLAG_INTERNAL, true, false, true);

            if (indestructible)
            {
                newEntity.SetFlag(FLAG_INDESTRUCTIBLE, true, false, true);
            }

            return newEntity;
        }
        public static void PlayPlugEffect(Vector3 positionLookingFrom, Vector3 positionWorld)
        {
            Effect.server.Run(FX_PLUG, positionWorld, (positionWorld - positionLookingFrom).normalized);
        }

        public static void PlayCombinerEffect(Vector3 position)
        {
            Effect.server.Run(FX_COMBINER, position, Vector3.up);
        }

        public static void PlayTechTrashEffect(Vector3 position)
        {
            Effect.server.Run(FX_TECHTRASH, position, Vector3.up);
        }

        public static float GetUpdateRateFromFPS(float updateRate)
        {
            return 1F / Mathf.Clamp(updateRate, 0.1F, 60F);
        }

        public static void GetRidOfHeldEntity(Item item)
        {
            if (item == null)
            {
                return;
            }

            var oldHeldEntity = item.GetHeldEntity();

            if (oldHeldEntity == null)
            {
                return;
            }

            item.SetHeldEntity(null);

            oldHeldEntity.Kill();
        }

        private static void TellMessageI(IPlayer player, string message)
        {
            string result = $"{MSG(MSG_CHAT_PREFIX)} {message}";

            if (player.IsServer)
            {
                result = StripTags(result);
            }

            player.Message(result);
        }
        private static void TellMessage(BasePlayer player, string message, bool alsoPrintWarning = false)
        {
            string result = $"{MSG(MSG_CHAT_PREFIX)} {message}";

            if (player == null)
            {
                Instance.PrintToChat(result);
                if (alsoPrintWarning)
                {
                    Instance.PrintWarning(StripTags(result));
                }
            }
            else
            {
                player.ChatMessage(result);
                if (alsoPrintWarning)
                {
                    Instance.PrintWarning(StripTags($"{player.displayName} has been told: {result}"));
                }
            }
        }
        public static string StripTags(string inputString)
        {
            return System.Text.RegularExpressions.Regex.Replace(inputString, "<[^>]+>|</[^>]+>", string.Empty);
        }

        public static bool IsPlayerMissing(BasePlayer player)
        {
            if (player == null)
            {
                return true;
            }

            if (player.IsSleeping())
            {
                return true;
            }

            if (player.IsDead())
            {
                return true;
            }

            return false;
        }

        public static bool PrePlayerDraw(BasePlayer player)
        {
            if (!player.HasPlayerFlag(BasePlayer.PlayerFlags.IsAdmin))
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();

                return true;
            }

            return false;
        }

        public static void PostPlayerDraw(BasePlayer player, bool setAdmin)
        {
            if (setAdmin)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                player.SendNetworkUpdateImmediate();
            }
        }

        public static void DrawLineCommon(BasePlayer player, float duration, Color color, Vector3 point1, Vector3 point2)
        {
            if (color.Equals(Color.clear))
            {
                return;
            }
            player.SendConsoleCommand("ddraw.line", duration, color, point1, point2);
        }

        public static void DrawSpline(BasePlayer player, float duration, Vector3[] linePoints, Color[] colorSequence = null)
        {
            if (IsPlayerMissing(player))
            {
                return;
            }

            if (colorSequence == null)
            {
                colorSequence = new Color[1]
                {
                    Color.white
                };
            }

            bool setAdmin = PrePlayerDraw(player);

            Color currentColor;

            //finish at the penultimate element, since we need pairs of points to make them lines
            for (var d = 0; d < linePoints.Length - 1; d++)
            {
                //DON'T inverse transform. As it turns out, xmas lights use world positions.
                //Thanks, Obama.
                currentColor = colorSequence[d % colorSequence.Length];
                if (currentColor.Equals(Color.clear))
                {
                    return;
                }

                DrawLineCommon(player, duration, currentColor, linePoints[d], linePoints[d + 1]);
            }

            PostPlayerDraw(player, setAdmin);

        }

        public static bool IsPlayerNPC(BasePlayer player)
        {
            if (player.IsNpc)
            {
                return true;
            }

            return !(player.userID >= 76560000000000000L || player.userID <= 0L);
        }

        public static MultiToolConfig GetPlayerConfig(BasePlayer player)
        {
            if (!Instance.Data.PlayerWireConfigs.ContainsKey(player.userID))
            {
                Instance.Data.PlayerWireConfigs.Add(player.userID, new MultiToolConfig
                {
                    Slack = 0.25F
                });

                Instance.Data.PlayerWireConfigs[player.userID].Dirty = true;
            }

            return Instance.Data.PlayerWireConfigs[player.userID];
        }

        public static void ModifyHangingXmasLightsSegments(AdvancedChristmasLights lights, List<AdvancedChristmasLights.pointEntry> affectedPoints, List<float> slacks, int subdivisions, Action<List<AdvancedChristmasLights.pointEntry>> executeAfterDoneExtra = null)
        {
            List<AdvancedChristmasLights.pointEntry> resultingPoints = new List<AdvancedChristmasLights.pointEntry>();

            Action<List<AdvancedChristmasLights.pointEntry>> executeAfterDone = (generatedPoints) =>
            {
                if (lights == null)
                {
                    return;
                }

                //should we be using added points here or what...
                //are they even needed?

                var lastPoint = lights.points[lights.points.Count - 1];

                //ignore last 3 elements...
                var newPoints = lights.points.Take(lights.points.Count - 3).ToList();

                //add generated points...

                newPoints.AddRange(generatedPoints);

                //and add last point.
                newPoints.Add(lastPoint);

                lights.points = newPoints;


                //it will have to be picked up to be unmarked
                lights.SetFlag(FLAG_BAKED, true, false, true);

                lights.SendNetworkUpdateImmediate();

                if (executeAfterDoneExtra != null)
                {
                    executeAfterDoneExtra(generatedPoints);
                }
            };

            ServerMgr.Instance.StartCoroutine(CrunchXmasLightsCoroutine(affectedPoints, resultingPoints, slacks.ToArray(), subdivisions, executeAfterDone, 0));
        }

        public static void MakeHangingWireConnection(IOEntity inputEntity, IOEntity outputEntity, int inputSlot, int outputSlot, float slack, int subdivisions, Action<Vector3[]> executeAfterDoneExtra = null)
        {
            if (inputEntity == null)
            {
                return;
            }

            if (outputEntity == null)
            {
                return;
            }

            if (IsMarkedBaked(outputEntity))
            {
                return;
            }

            Vector3[] resultingPoints = new Vector3[1];

            Action<Vector3[]> executeAfterDone = (generatedPoints) =>
            {
                if (!AreIOEntitiesEngangedAtSlots(inputEntity, outputEntity, inputSlot, outputSlot))
                {
                    return;
                }

                //we know it's currently engaged, so disengage current true
                EngageIO(inputEntity, outputEntity, inputSlot, outputSlot, generatedPoints, WireTool.WireColour.Default, true);
                //mark as baked so we know it's already hanging

                //clearing or making a new connection on this line unmarks it
                //is this why?

                outputEntity.SetFlag(FLAG_BAKED, true, false, true);

                if (executeAfterDoneExtra != null)
                {
                    executeAfterDoneExtra(generatedPoints);
                }
            };

            ServerMgr.Instance.StartCoroutine(CrunchWireCoroutine(outputEntity.transform, outputEntity.outputs[outputSlot].linePoints, resultingPoints, slack, subdivisions, executeAfterDone));
        }

        public static void EngageIO(IOEntity inputEntity, IOEntity outputEntity, int inputSlot, int outputSlot, Vector3[] linePoints, WireTool.WireColour wireColour = WireTool.WireColour.Default, bool disengageCurrentFirst = true, bool forceProvidedWireColour = false, bool alsoMarkHardwired = false, bool alsoMarkDangerous = false)
        {
            if (disengageCurrentFirst)
            {
                //if they're engaged to one another, disengage them that way...
                if (AreIOEntitiesEngangedAtSlots(inputEntity, outputEntity, inputSlot, outputSlot))
                {
                    if (!forceProvidedWireColour)
                    {
                        wireColour = outputEntity.outputs[outputSlot].wireColour;
                    }

                    DisengageIO(inputEntity, outputEntity, inputSlot, outputSlot, true);

                }
                //but otherwise find out what to disengage
                else
                {
                    var maybeSomethingAtInputSlot = inputEntity.inputs[inputSlot].connectedTo?.Get();
                    var maybeSomethingAtOutputSlot = outputEntity.outputs[outputSlot].connectedTo?.Get();

                    if (maybeSomethingAtInputSlot != null)
                    {
                        DisengageIO(inputEntity, maybeSomethingAtInputSlot, inputSlot, inputEntity.inputs[inputSlot].connectedToSlot, true);
                    }

                    if (maybeSomethingAtOutputSlot != null)
                    {
                        DisengageIO(maybeSomethingAtOutputSlot, outputEntity, outputEntity.outputs[outputSlot].connectedToSlot, outputSlot, true);
                    }
                }
            }

            if (alsoMarkHardwired)
            {
                MarkIOEntitySlotHardwired(inputEntity.inputs[inputSlot], inputEntity);
                MarkIOEntitySlotHardwired(outputEntity.outputs[outputSlot], outputEntity);
            }

            if (alsoMarkDangerous)
            {
                MarkIOEntitySlotDangerous(inputEntity.inputs[inputSlot], inputEntity);
                MarkIOEntitySlotDangerous(outputEntity.outputs[outputSlot], outputEntity);
            }

            outputEntity.outputs[outputSlot].linePoints = linePoints;

            inputEntity.inputs[inputSlot].connectedTo.Set(outputEntity);
            inputEntity.inputs[inputSlot].connectedToSlot = outputSlot;
            inputEntity.inputs[inputSlot].wireColour = wireColour;
            inputEntity.inputs[inputSlot].connectedTo.Init();

            outputEntity.outputs[outputSlot].connectedTo.Set(inputEntity);
            outputEntity.outputs[outputSlot].connectedToSlot = inputSlot;
            outputEntity.outputs[outputSlot].wireColour = wireColour;
            outputEntity.outputs[outputSlot].connectedTo.Init();
            outputEntity.MarkDirtyForceUpdateOutputs();
            outputEntity.SendNetworkUpdateImmediate();
            inputEntity.SendNetworkUpdate();
            outputEntity.SendChangedToRoot(true);

        }

        public static bool AreIOEntitiesEngangedAtSlots(IOEntity inputEntity, IOEntity outputEntity, int inputSlot, int outputSlot)
        {
            if (inputEntity == null)
            {
                return false;
            }

            if (outputEntity == null)
            {
                return false;
            }

            if (inputEntity.inputs[inputSlot].connectedTo.Get() != outputEntity)
            {
                return false;
            }

            if (inputEntity.inputs[inputSlot].connectedToSlot != outputSlot)
            {
                return false;
            }

            return true;
        }

        public static void DisengageIO(IOEntity input, IOEntity output, int inputSlot, int outputSlot, bool sendUpdate = false)
        {
            input.inputs[inputSlot].connectedTo.entityRef.uid = 0;
            input.inputs[inputSlot].connectedTo.ioEnt = null;
            input.inputs[inputSlot].connectedToSlot = 0;

            output.outputs[outputSlot].linePoints = null;

            output.outputs[outputSlot].connectedTo.entityRef.uid = 0;
            output.outputs[outputSlot].connectedTo.ioEnt = null;
            output.outputs[outputSlot].connectedToSlot = 0;

            if (!sendUpdate)
            {
                return;
            }

            input.UpdateFromInput(0, 0);

            input.MarkDirtyForceUpdateOutputs();
            input.SendIONetworkUpdate();
            input.SendNetworkUpdateImmediate();
            input.UpdateNetworkGroup();

            output.MarkDirtyForceUpdateOutputs();
            output.SendIONetworkUpdate();
            output.SendNetworkUpdateImmediate();
            output.UpdateNetworkGroup();
        }

        public static bool HasAdminPermission(IPlayer player)
        {
            if (player.IsAdmin)
            {
                return true;
            }

            return Instance.permission.UserHasPermission(player.Id, PERM_ADMIN);

        }

        public static bool HasAdminPermission(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                return true;
            }

            if (player.IsDeveloper)
            {
                return true;
            }

            return Instance.permission.UserHasPermission(player.UserIDString, PERM_ADMIN);
        }
        #endregion

        #region SCARY MATHS, PHYSICS AND COROUTINES
        public static IEnumerator CrunchXmasLightsCoroutine(List<AdvancedChristmasLights.pointEntry> originalLinePoints, List<AdvancedChristmasLights.pointEntry> resultingPoints, float[] slacks = null, int subdivisions = 100, Action<List<AdvancedChristmasLights.pointEntry>> executeAfterCrunching = null, int slackIndexShift = 0)
        {
            if (executeAfterCrunching == null)
            {
                Instance.PrintError("ERROR: Trying to crunch the xmas lights coroutine, but the executeAfterCrunching Action is null! Why you bully me!");
                yield break;
            }

            if (slacks == null)
            {
                slacks = Enumerable.Repeat(0.25F, originalLinePoints.Count+1).ToArray();
            }

            var waitFor = new WaitForEndOfFrame();

            AdvancedChristmasLights.pointEntry first;
            AdvancedChristmasLights.pointEntry second;

            int currentIndex = 0;

            int maxFirstIndex = originalLinePoints.Count - 2;

            AdvancedChristmasLights.pointEntry[] currentSubArray;

            bool allDone = false;

            while (true)
            {
                if (allDone)
                {
                    executeAfterCrunching(resultingPoints);

                    yield break;
                }

                first = originalLinePoints[currentIndex];
                second = originalLinePoints[currentIndex + 1];

                currentSubArray = GetCatenaryArrayPointEntry(first, second, slacks[currentIndex+1+slackIndexShift], subdivisions);

                resultingPoints.AddRange(currentSubArray);

                if (currentIndex >= maxFirstIndex)
                {
                    allDone = true;
                }
                else
                {
                    currentIndex++;
                }

                yield return waitFor;
            }
        }

        public static IEnumerator CrunchWireCoroutine(Transform transform, Vector3[] localLinePoints, Vector3[] resultingPoints, float slack,  int subdivisions, Action<Vector3[]> executeAfterCrunching)
        {
            var waitFor = new WaitForEndOfFrame();

            Vector3 first;
            Vector3 second;

            int currentIndex = 0;

            int maxFirstIndex = localLinePoints.Length - 2;

            Vector3[] currentSubArray;

            var workingList = Facepunch.Pool.GetList<Vector3>();

            bool allDone = false;

            while (true)
            {
                if (allDone)
                {
                    resultingPoints = new Vector3[workingList.Count];//  workingList.ToArray();

                    var player = BasePlayer.activePlayerList.FirstOrDefault();

                    for (var i = 0; i<workingList.Count; i++)
                    {
                        resultingPoints[i] = transform.InverseTransformPoint(workingList[i]);
                    }

                    if (player != null)
                    {
                        DrawSpline(player, 10F, resultingPoints, XmasColorSequence);
                    }

                    executeAfterCrunching(resultingPoints);

                    Facepunch.Pool.FreeList(ref workingList);

                    yield break;
                }

                first = transform.TransformPoint(localLinePoints[currentIndex]);
                second = transform.TransformPoint(localLinePoints[currentIndex+1]);

                currentSubArray = GetCatenaryArrayVector3(first, second, slack, subdivisions);

                workingList.AddRange(currentSubArray);

                if (currentIndex >= maxFirstIndex)
                {
                    allDone = true;
                }
                else
                {
                    currentIndex++;
                }

                yield return waitFor;
            }
        }

        //Catenary equations based on https://gist.github.com/Farfarer/a765cd07920d48a8713a0c1924db6d70
        //Yes apparently there's a difference between a catenary and a parabole, thanks, Galileo

        public static AdvancedChristmasLights.pointEntry[] ArrayPointEntryFromArrayVector3(Vector3[] arrayVector3, Vector3 normal = default(Vector3))
        {
            if (arrayVector3 == null)
            {
                return null;
            }

            bool funkyNormal = normal == default(Vector3);

            if (funkyNormal)
            {
                normal = Vector3.down;/* (Vector3.down + (Vector3.right * XMAS_NORMAL_DEVIATION) + (UnityEngine.Random.Range(0F, XMAS_NORMAL_DEVIATION * 2F) * Vector3.left) + (Vector3.forward * XMAS_NORMAL_DEVIATION) + (UnityEngine.Random.Range(0F, XMAS_NORMAL_DEVIATION * 2F) * Vector3.back)); */
        }

            AdvancedChristmasLights.pointEntry[] result = new AdvancedChristmasLights.pointEntry[arrayVector3.Length];

            for (var i = 0; i< arrayVector3.Length; i++)
            {


                result[i] = new AdvancedChristmasLights.pointEntry
                {
                    point = arrayVector3[i],
                    normal = normal
                };
            }

            return result;
        }

        public static AdvancedChristmasLights.pointEntry[] GetCatenaryArrayPointEntry(AdvancedChristmasLights.pointEntry posStart, AdvancedChristmasLights.pointEntry posEnd, float slack, int steps) => ArrayPointEntryFromArrayVector3(GetCatenaryArrayVector3(posStart.point, posEnd.point, slack, steps));
        
        public static Vector3[] GetCatenaryArrayVector3(Vector3 posStart, Vector3 posEnd, float slack, int steps)
        {
            if (steps < 2)
            {
                return new Vector3[]
                {
                    posStart,
                    posEnd
                };
            }

            if (slack == 0F || slack == -0F)
            {
                return new Vector3[]
                {
                    posStart,
                    posEnd
                };
            }

            Vector3[] points;

            float lineDist = Vector3.Distance(posEnd, posStart);
            float lineDistH = Vector3.Distance(new Vector3(posEnd.x, posStart.y, posEnd.z), posStart);
            float l = lineDist + Mathf.Max(0.0001f, slack);
            float r = 0.0f;
            float s = posStart.y;
            float u = lineDistH;
            float v = posEnd.y;

            if ((u - r) == 0.0f)
            {
                return new Vector3[]
                {
                    posStart,
                    posEnd
                };
            }

            float ztarget = Mathf.Sqrt(Mathf.Pow(l, 2.0f) - Mathf.Pow(v - s, 2.0f)) / (u - r);

            int loops = 30;
            int iterationCount = 0;
            int maxIterations = loops * 10; // For safety.
            bool found = false;

            float z = 0.0f;
            float ztest;
            float zstep = 100.0f;
            float ztesttarget;
            for (int i = 0; i < loops; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    iterationCount++;
                    ztest = z + zstep;
                    ztesttarget = (float)Math.Sinh(ztest) / ztest;

                    if (float.IsInfinity(ztesttarget))
                        continue;

                    if (ztesttarget == ztarget)
                    {
                        found = true;
                        z = ztest;
                        break;
                    }
                    else if (ztesttarget > ztarget)
                    {
                        break;
                    }
                    else
                    {
                        z = ztest;
                    }

                    if (iterationCount > maxIterations)
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                    break;

                zstep *= 0.1f;
            }

            float a = (u - r) / 2.0f / z;
            float p = (r + u - a * Mathf.Log((l + v - s) / (l - v + s))) / 2.0f;
            float q = (v + s - l * (float)Math.Cosh(z) / (float)Math.Sinh(z)) / 2.0f;

            points = new Vector3[steps];
            float stepsf = steps - 1;
            float stepf;
            for (int i = 0; i < steps; i++)
            {
                stepf = i / stepsf;
                Vector3 pos = Vector3.zero;
                pos.x = Mathf.Lerp(posStart.x, posEnd.x, stepf);
                pos.z = Mathf.Lerp(posStart.z, posEnd.z, stepf);
                pos.y = a * (float)Math.Cosh(((stepf * lineDistH) - p) / a) + q;
                points[i] = pos;
            }

            return points;
        }
        #endregion

        #region GUI

        #endregion
    }
}
