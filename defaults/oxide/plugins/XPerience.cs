using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using System.Text;
using System.Web;
using Facepunch.Extend;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Core.Database;
using Oxide.Plugins.XPerienceEx;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("XPerience", "MACHIN3", "1.2.0")]
    [Description("Player level system with xp, stats, and skills")]
    public class XPerience : RustPlugin
    {
        #region Update Log

        /*****************************************************
		【 𝓜𝓐𝓒𝓗𝓘𝓝𝓔 】
		12 / 20 / 2021
        Discord: discord.skilledsoldiers.net
        *****************************************************/

        #region version 1.2.0
        /*****************************************************
		----------------------
		✯ version 1.2.0
		----------------------
        ✯ Added reset page to admin panel for resetting config and all players
        ✯ Fixed crafting and repair issue in Crafting skill
        ✯ Changed all crafting adjustments from 0.1 to 0.01  in Crafting skill
        ✯ Added repair cost decrease to Crafter skill
        ✯ Fixed XP from going negative
        ✯ Fixed LiveUI not updating when FixPlayerData used
        ✯ Fixed SQL error on server save
        ✯ Resetting XPerience now deletes all SQL data as well
        ✯ Moved all other mod support to a seperate admin page "Other Mod Settings"

        ✯ Added Server Rewards support: (requires Server Rewards plugin)
        ✯ Option to enable level up reward and amount
        ✯ Option to enable level down reduction and amount
        
        ✯ Added Clans support: (requires Clans plugin)
        ✯ Option to enable shared xp gain and amount
        ✯ Option to enable shared xp reduction and amount

        *****************************************************/
        #endregion
        #region version 1.1.9
        /*****************************************************
		----------------------
		✯ version 1.1.9
		----------------------
        ✯ Fixed power tool detection not working to disable xp
        ✯ Changed apple chance adjustment from 0.1 to 0.01
        ✯ Fixed xp gain issue when upgrading buildings with Building Grades plugin

        ✯ Added Economics support: (requires Economics plugin)
        ✯ Option to enable level up reward and amount
        ✯ Option to enable level down reduction and amount
        ✯ Option to enable cost to reset stats and amount
        ✯ Option to enable cost to reset skills and amount
        *****************************************************/
        #endregion
        #region version 1.1.8
        /*****************************************************
		----------------------
		✯ version 1.1.8
		----------------------
        ✯ Added xp for reviving players
        ✯ Replaced Chemist Stat with Medic Skill
        ✯ Expanded UIs to fit new skill
        ✯ Changed LiveStats location label
        ✯ Added Fix My Data button to help page for players to fix their own data after server settings change
        ✯ Added timer on admin timer page for player fix data option
        ✯ Fixed level boost setting adjustment from 0.1 to 0.01
        ✯ Fixed Armor bar display issues not showing correct values
        ✯ Changed OnFishCaught to OnFishCatch so fishing skill works again
        ✯ Changed Critical and Block display to round to whole numbers
        ✯ Added option to show KillRecords button on Player Control Panel
        ✯ Adjusted cold/heat tolerance detection - more work needed
        ✯ Added option to disable XP when using chainsaw or jackhammer

        ✯ Added New Skill (Medic):
        ✯ Reduced crafting time (mixing table)
        ✯ Revive players with more health
        ✯ Recover from wounded with higher health
        ✯ Get more health from some medical tools

        ✯ Added UINotify support: (requires UINotify plugin and user permissions)
        ✯ Option to disable default chat messages
        ✯ Show XP gain/loss in UINotify
        ✯ Show Level up/down in UInotify
        ✯ Show Dodge/Block notification in UINotify
        ✯ Show Critical Hit in UINotify

        ATTENTION - IF YOU HAVEN'T ALREADY DONE SO WITH PREVIOUS UPDATES YOU MUST RUN CHAT COMMAND /xpupdate THEN USE REPAIR ALL PLAYER DATA IN ADMIN PANEL!
        *****************************************************/
        #endregion
        #region version 1.1.7
        /*****************************************************
		----------------------
		✯ version 1.1.7
		----------------------
        ✯ Fixed gunpowder multiply issue when above level 5 crafter

        ATTENTION - IF YOU HAVEN'T ALREADY DONE SO WITH PREVIOUS UPDATES YOU MUST RUN CHAT COMMAND /xpupdate THEN USE REPAIR ALL PLAYER DATA IN ADMIN PANEL!
          *****************************************************/
        #endregion
        #region version 1.1.6
        /*****************************************************      
        ----------------------
		✯ version 1.1.6
		----------------------
        ✯ Fixed building repair issues
		✯ Rewrote building repair to properly reduce costs and time
        ✯ Fixed item repair issues

        ATTENTION - IF YOU HAVEN'T ALREADY DONE SO WITH PREVIOUS UPDATES YOU MUST RUN CHAT COMMAND /xpupdate THEN USE REPAIR ALL PLAYER DATA IN ADMIN PANEL!
        *****************************************************/
        #endregion
        #region version 1.1.5
        /*****************************************************    
        ----------------------
		✯ version 1.1.5
		----------------------
		✯ Changed some lang text
        ✯ Reordered admin panel menu
        ✯ Setting Stats/Skills to max level 0 will now disable them
        ✯ Fixed level percentage not showing correct value
        ✯ Fixed admin FixPlayerData button not working for server admins

        ✯ Added Missions Support:
        ✯ Option to set eperience reward amount
        ✯ Option to enable failed xp reduction
        ✯ Option to set failed xp reduction amount

        ATTENTION - IF YOU HAVEN'T ALREADY DONE SO WITH LAST UPDATE YOU MUST RUN CHAT COMMAND /xpupdate THEN USE REPAIR ALL PLAYER DATA IN ADMIN PANEL!
        *****************************************************/
        #endregion
        #region version 1.1.4
        /*****************************************************        
        ----------------------
		✯ version 1.1.4
		----------------------
		✯ Added 5th location option for LiveUI stats
        ✯ Added Admin Control Panel for config changes in game
        ✯ Added Option to prevent players from changing LiveUI Location
        ✯ Added option to disable armor absorb chat messages
        ✯ Fixed tamer skill not giving permissions to use Pets mod
        ✯ Rewrote xp and level progression
        ✯ Added max level limit option (default 500)
        ✯ Added admin command to reset all player stats except experience
        ✯ Fixed OnPlayerHealthChange error

        ATTENTION - MUST RUN CHAT COMMAND /xpupdate THEN USE REPAIR ALL PLAYER DATA IN ADMIN PANEL!
        *****************************************************/
        #endregion
        #region version 1.1.3
        /*****************************************************
        ----------------------
		✯ version 1.1.3
		----------------------
		✯ Fixed level loss issue when admin removes more than 1 level of xp from a player
        *****************************************************/
        #endregion
        #region version 1.1.2
        /*****************************************************        
        ----------------------
		✯ version 1.1.2
		----------------------
		✯ Fixed error from c4 on bradley
        ✯ Fixed point cost multipler to go off next level instead of current
        ✯ Added new pages to help UI that shows server settings
        ✯ Added new pages to help UI that explains all stats and skills
        ✯ Added Admin command for giving expience to players
        ✯ Added Admin command for taking expience from players

        ✯ Ingame Admin Panel Coming Soon
        *****************************************************/
        #endregion
        #region version 1.1.1
        /*****************************************************	
		----------------------
		✯ version 1.1.1
		----------------------
		✯ Fixed OnEntityTake Damage Error
        *****************************************************/
        #endregion
        #region version 1.1.0
        /*****************************************************	
		----------------------
		✯ version 1.1.0
		----------------------
		✯ Fixed building errors
		✯ Fixed TC auth issues
		✯ Fixed crafting cost issues
		✯ Changed API hook - expects rarity cost as double
		✯ Fixed OnEntityDeath errors
		✯ Added VIP class for seperate reset timers
        *****************************************************/
        #endregion
        #region version 1.0.9
        /*****************************************************
		----------------------
		✯ version 1.0.9
		----------------------
		✯ LiveUI selection within player control panel
		✯ Added HELP button to control panel
		✯ created HELP UI that will explain XPerience in more detail to players
		✯ fixed OnPlayerDeath error
		✯ fixed OnEntityTakeDamage error
		✯ Fixed ResearchCostDetermain error
		✯ Added API support for other Research mods
		✯ Changed gather rate from multiple to addition
		✯ Added option to use vanilla gather rates
		✯ Fixed armor bar display when using tea
		✯ Added Bradley / Helicopter XP gain options
		✯ Added config list for randomchance items in forager
        *****************************************************/
        #endregion
        #region version 1.0.8
        /*****************************************************
		----------------------
		✯ version 1.0.8
		----------------------
		✯ Added armor damage reduction - dexterity
		✯ Changed Smithy skill to include % chance production increase
		✯ Added option to allow admin class to bypass reset timers
		✯ Added more items to random find in Forager
		✯ Ability to search other player stats
		✯ Top list names now link to player stats
        *****************************************************/
        #endregion
        #region version 1.0.7
        /*****************************************************
		----------------------
		✯ version 1.0.7
		----------------------
		✯ Fixed research cost issues
		✯ Added apple as random item on forager
        *****************************************************/
        #endregion
        #region version 1.0.6
        /*****************************************************
		----------------------
		✯ version 1.0.6
		----------------------
		✯ Added reset restriction and timer option
        *****************************************************/
        #endregion
        #region version 1.0.5
        /*****************************************************
		----------------------
		✯ version 1.0.5
		----------------------
		✯ fixed lang API spelling
		✯ Added admin reset command
		✯ fixed metabolism issues
        *****************************************************/
        #endregion
        #region version prereleases
        /*****************************************************
		----------------------
		✯ version 1.0.4
		----------------------
		✯ more lang API fixes

		----------------------
		✯ version 1.0.3
		----------------------
		✯ full lang api addition

		----------------------
		✯ version 1.0.2
		----------------------
		✯ fixed hooks and loading issues

		----------------------
		✯ version 1.0.1
		----------------------
		✯ more language definitions
		✯ fixed OnEntityTakeDamage error

        *****************************************************/
        #endregion
        /*****************************************************
		----------------------
		To-Do:
		✯ More pet abilities for tamer
		✯ Fix horse taming
		✯ Add chance for more grubs/worms
		*****************************************************/
        #endregion

        #region Refrences

        [PluginReference]
        Plugin KillRecords, Pets, UINotify, Economics, ServerRewards, Clans;

        #endregion

        #region Fields

        private XPData _xpData;
        private LootData _lootData;
        private DynamicConfigFile _XPerienceData;
        private DynamicConfigFile _LootContainData;
        private Dictionary<string, XPRecord> _xperienceCache;
        private Dictionary<uint, Loot> _lootCache;
        private Configuration config;
        private static readonly RNGCryptoServiceProvider _generator = new RNGCryptoServiceProvider();
        private const string Admin = "xperience.admin";
        private const string VIP = "xperience.vip";

        #endregion

        #region Config

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("Default Options")]
            public DefaultOptions defaultOptions = new DefaultOptions();

            [JsonProperty("UI Text Colors")]
            public UITextColor uitextColor = new UITextColor();

            [JsonProperty("UI Notify (requires UINotify plugin)")]
            public UINotifier UiNotifier = new UINotifier();

            [JsonProperty("XP - Level Config")]
            public XpLevel xpLevel = new XpLevel();

            [JsonProperty("XP - Night Bonus")]
            public NightBonus nightBonus = new NightBonus();

            [JsonProperty("XP - Gain Amounts")]
            public XpGain xpGain = new XpGain();

            [JsonProperty("XP - Gather Amounts")]
            public XpGather xpGather = new XpGather();

            [JsonProperty("XP - Building Amounts")]
            public XpBuilding xpBuilding = new XpBuilding();

            [JsonProperty("XP - Mission Amounts")]
            public XpMissions xpMissions = new XpMissions();

            [JsonProperty("XP - Reducer Amounts")]
            public XpReducer xpReducer = new XpReducer();

            [JsonProperty("BonusXP - Bonus Amounts (requires KillRecords plugin)")]
            public XpBonus xpBonus = new XpBonus();

            [JsonProperty("Economics Rewards (requires Economics plugin)")]
            public XpEcon xpEcon = new XpEcon();

            [JsonProperty("Server Rewards (requires ServerRewards plugin)")]
            public SRewards sRewards = new SRewards();

            [JsonProperty("Mentality Stat")]
            public Mentality mentality = new Mentality();

            [JsonProperty("Dexterity Stat")]
            public Dexterity dexterity = new Dexterity();

            [JsonProperty("Might Stat")]
            public Might might = new Might();

            [JsonProperty("Chemist Stat")]
            public Chemist chemist = new Chemist();

            [JsonProperty("WoodCutter Skill")]
            public Woodcutter woodcutter = new Woodcutter();

            [JsonProperty("Smithy Skill")]
            public Smithy smithy = new Smithy();

            [JsonProperty("Miner Skill")]
            public Miner miner = new Miner();

            [JsonProperty("Forager Skill")]
            public Forager forager = new Forager();

            [JsonProperty("Hunter Skill")]
            public Hunter hunter = new Hunter();

            [JsonProperty("Fisher Skill")]
            public Fisher fisher = new Fisher();

            [JsonProperty("Crafter Skill")]
            public Crafter crafter = new Crafter();

            [JsonProperty("Framer Skill")]
            public Framer framer = new Framer();

            [JsonProperty("Medic Skill")]
            public Medic medic = new Medic();

            [JsonProperty("Tamer Skill")]
            public Tamer tamer = new Tamer();

            [JsonProperty("Clans (requires Clans plugin)")]
            public XpClans xpclans = new XpClans();

            [JsonProperty("SQL Info")]
            public SQL sql = new SQL();

        }

        public class DefaultOptions
        {
            public int liveuistatslocation = 1;
            public bool liveuistatslocationmoveable = true;
            public bool showchatprofileonconnect = true;
            public bool restristresets = true;
            public int resetminsstats = 60;
            public int resetminsskills = 60;
            public bool bypassadminreset = true;
            public int vipresetminstats = 30;
            public int vipresetminsskills = 30;
            public int playerfixdatatimer = 60;
            public bool disablearmorchat = false;
            //public bool hardcorenoreset = false;
        }

        public class UITextColor
        {
            public string defaultcolor = "white";
            public string level = "green";
            public string experience = "green";
            public string nextlevel = "yellow";
            public string remainingxp = "cyan";
            public string statskilllevels = "yellow";
            public string perks = "green";
            public string unspentpoints = "green";
            public string spentpoints = "red";
            public string pets = "cyan";

        }

        public class UINotifier
        {
            public bool useuinotify = false;
            public bool disablechats = false;
            public bool xpgainloss = true;
            public int xpgainlosstype = 0;
            public bool levelupdown = true;
            public int levelupdowntype = 0;
            public bool dodgeblock = true;
            public int dodgeblocktype = 0;
            public bool criticalhit = true;
            public int criticalhittype = 0;
        }

        public class XpLevel
        {
            public double levelstart = 25;
            public double levelmultiplier = 50;
            public int maxlevel = 500;
            public double levelxpboost = 0.05;
            public int statpointsperlvl = 1;
            public int skillpointsperlvl = 2;
        }

        public class NightBonus
        {
            public bool Enable = true;
            public int StartTime = 19;
            public int EndTime = 5;
            public double Bonus = 0.10;
            public bool enableskillboosts = true;
        }

        public class XpGain
        {
            public double chickenxp = 5;
            public double fishxp = 5;
            public double boarxp = 10;
            public double stagxp = 15;
            public double wolfxp = 20;
            public double bearxp = 25;
            public double sharkxp = 30;
            public double horsexp = 20;
            public double scientistxp = 25;
            public double dwellerxp = 25;
            public double playerxp = 25;
            public double lootcontainerxp = 5;
            public double animalharvestxp = 5;
            public double corpseharvestxp = 5;
            public double underwaterlootcontainerxp = 10;
            public double lockedcratexp = 25;
            public double hackablecratexp = 50;
            public double craftingxp = 5;
            public double bradley = 25;
            public double patrolhelicopter = 30;
            public double playerrevive = 5;
        }

        public class XpGather
        {
            public double treexp = 5;
            public double orexp = 5;
            public double harvestxp = 5;
            public double plantxp = 5;
            public bool noxptools = true;
        }

        public class XpBuilding
        {
            public double woodstructure = 5;
            public double stonestructure = 10;
            public double metalstructure = 15;
            public double armoredstructure = 20;
        }

        public class XpMissions
        {
            public double missionsucceededxp = 50;
            public bool missionfailed = false;
            public double missionfailedxp = 10;
        }

        public class XpReducer
        {
            public bool suicidereduce = true;
            public double suicidereduceamount = 0.05;
            public bool deathreduce = true;
            public double deathreduceamount = 0.05;
        }

        public class XpBonus
        {
            public bool showkrbutton = false;
            public bool enablebonus = false;
            public int requiredkills = 10;
            public double bonusxp = 5;
            public int endbonus = 500;
            public bool multibonus = true;
            public string multibonustype = "fixed";
        }
         
        public class XpEcon
        {
            public bool econlevelup = false;
            public bool econleveldown = false;
            public bool econresetstats = false;
            public bool econresetskills = false;
            public double econlevelreward = 50;
            public double econlevelreduction = 25;
            public double econresetstatscost = 100;
            public double econresetskillscost = 100;
        }

        public class SRewards
        {
            public bool srewardlevelup = false;
            public bool srewardleveldown = false;
            public int srewardlevelupamt = 5;
            public int srewardleveldownamt = 5;
        }

        public class Mentality
        {
            public int maxlvl = 10;
            public int pointcoststart = 2;
            public int costmultiplier = 2;
            public double researchcost = 0.10;
            public double researchspeed = 0.10;
            public double criticalchance = 0.05;
            public bool useotherresearchmod = false;
        }

        public class Dexterity
        {
            public int maxlvl = 10;
            public int pointcoststart = 2;
            public int costmultiplier = 2;
            public double blockchance = 0.05;
            public double blockamount = 0.10;
            public double dodgechance = 0.05;
            public double reducearmordmg = 0.05;
        }

        public class Might
        {
            public int maxlvl = 10;
            public int pointcoststart = 2;
            public int costmultiplier = 2;
            public double armor = 0.10;
            public double meleedmg = 0.05;
            public double metabolism = 0.02;
            public double bleedreduction = 0.05;
            public double radreduction = 0.05;
            public double heattolerance = 0.05;
            public double coldtolerance = 0.05;
            //public bool enablestacking = false;
            //public int stackmultiplier = 2;
        }

        public class Chemist
        {
            public int maxlvl = 10;
            public int pointcoststart = 2;
            public int costmultiplier = 2;
            public double crafttime = 0.10;
            //public double craftcost = 0.05;
            //public double production = 0.10;
        }

        public class Woodcutter
        {
            public int maxlvl = 10;
            public int pointcoststart = 2;
            public int costmultiplier = 2;
            public double gatherrate = 0.5;
            public double bonusincrease = 0.10;
            public double applechance = 0.10;
        }

        public class Smithy
        {
            public int maxlvl = 10;
            public int pointcoststart = 2;
            public int costmultiplier = 2;
            public double productionrate = 0.10;
            public double fuelconsumption = 0.10;
        }

        public class Miner
        {
            public int maxlvl = 10;
            public int pointcoststart = 2;
            public int costmultiplier = 2;
            public double gatherrate = 0.5;
            public double bonusincrease = 0.10;
            public double fuelconsumption = 0.10;
        }

        public class Forager
        {
            public int maxlvl = 10;
            public int pointcoststart = 2;
            public int costmultiplier = 2;
            public double gatherrate = 0.3;
            public double chanceincrease = 0.10;
            //public double grubwormincrease = 0.10;
            public double randomchance = 0.05;
            public Dictionary<int, RandomChanceList> randomChanceList = new Dictionary<int, RandomChanceList>
            {
                [0] = new RandomChanceList
                {
                    shortname = "apple",
                    amount = 1
                },
                [1] = new RandomChanceList
                {
                    shortname = "bandage",
                    amount = 1
                },
                [2] = new RandomChanceList
                {
                    shortname = "scrap",
                    amount = 1
                },
                [3] = new RandomChanceList
                {
                    shortname = "bucket.water",
                    amount = 1
                },
                [4] = new RandomChanceList
                {
                    shortname = "metal.fragments",
                    amount = 1
                }
            };
        }

        public class RandomChanceList
        {
            public string shortname = "";
            public int amount = 1;
        }

        public class Hunter
        {
            public int maxlvl = 10;
            public int pointcoststart = 2;
            public int costmultiplier = 2;
            public double gatherrate = 0.3;
            public double bonusincrease = 0.10;
            public double damageincrease = 0.05;
            public double nightdmgincrease = 0.01;
        }

        public class Fisher
        {
            public int maxlvl = 10;
            public int pointcoststart = 2;
            public int costmultiplier = 2;
            public double fishamountincrease = 0.75;
            public double itemamountincrease = 0.25;
        }

        public class Crafter
        {
            public int maxlvl = 10;
            public int pointcoststart = 2;
            public int costmultiplier = 2;
            public double craftspeed = 0.10;
            public double craftcost = 0.05;
            public double repairincrease = 0.07;
            public double repaircost = 0.05;
            public double conditionchance = 0.07;
            public double conditionamount = 0.10;
        }

        public class Framer
        {
            public int maxlvl = 10;
            public int pointcoststart = 2;
            public int costmultiplier = 2;
            public double upgradecost = 0.05;
            public double repaircost = 0.05;
            public double repairtime = 0.10;
        }
        
        public class Medic
        {
            public int maxlvl = 10;
            public int pointcoststart = 2;
            public int costmultiplier = 2;
            public double revivehp = 5;
            public double recoverhp = 5;
            public double crafttime = 0.10;
            public double tools = 2;
        }

        public class Tamer
        {
            public bool enabletame = false;
            public int maxlvl = 5;
            public int pointcoststart = 2;
            public int costmultiplier = 2;
            public bool tamechicken = true;
            public bool tameboar = true;
            public bool tamestag = true;
            public bool tamewolf = true;
            public bool tamebear = true;
            //public bool rideablepets = false;
            //public double petdamage = 0.05;
            public int chickenlevel = 1;
            public int boarlevel = 2;
            public int staglevel = 3;
            public int wolflevel = 4;
            public int bearlevel = 5;
        }

        public class XpClans
        {
            public bool enableclanbonus = false;
            public bool enableclanreduction = false;
            public double clanbonusamount = 0.10;
            public double clanreductionamount = 0.02;
        }

        public class SQL
        {
            public bool enablesql = false;
            public string SQLhost = "localhost";
            public int SQLport = 3306;
            public string SQLdatabase = "databasename";
            public string SQLusername = "username";
            public string SQLpassword = "password";
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
                if (MaybeUpdateConfig(config))
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
        }

        #region UpdateChecker

        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>().ToDictionary(prop => prop.Name, prop => ToObject(prop.Value));
                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        #endregion

        public static class RandomNumber
        {
            private static readonly RNGCryptoServiceProvider _generator = new RNGCryptoServiceProvider();
            public static int Between(int minimumValue, int maximumValue)
            {
                byte[] randomNumber = new byte[1];
                _generator.GetBytes(randomNumber);
                double asciiValueOfRandomCharacter = Convert.ToDouble(randomNumber[0]);
                double multiplier = Math.Max(0, (asciiValueOfRandomCharacter / 255d) - 0.00000000001d);
                int range = maximumValue - minimumValue + 1;
                double randomValueInRange = Math.Floor(multiplier * range);
                return (int)(minimumValue + randomValueInRange);
            }
        }

        #endregion

        #region Storage

        private void SaveData()
        {
            if (_xpData != null)
            {
                _xpData.XPerience = _xperienceCache;
                _XPerienceData.WriteObject(_xpData);
            }
        }

        private void SaveLoot()
        {
            _LootContainData = Interface.Oxide.DataFileSystem.GetFile(nameof(XPerience) + "/XPLootData");
            _lootCache.Clear();
            if (_lootData != null)
            {
                _lootData.LootRecords = _lootCache;
                _LootContainData.WriteObject(_lootData);
            }
        }

        private void LoadData()
        {
            try
            {
                _xpData = _XPerienceData.ReadObject<XPData>();
                _xperienceCache = _xpData.XPerience;
            }
            catch
            {
                _xpData = new XPData();
            }
            try
            {
                _lootData = _LootContainData.ReadObject<LootData>();
                _lootCache = _lootData.LootRecords;
            }
            catch
            {
                _lootData = new LootData();
            }
        }

        private class XPData
        {
            public Dictionary<string, XPRecord> XPerience = new Dictionary<string, XPRecord>();
        }

        private class XPRecord
        {
            public double level;
            public double experience;
            public double requiredxp;
            public int statpoint;
            public int skillpoint;
            public int Mentality;
            public int MentalityP;
            public int Dexterity;
            public int DexterityP;
            public int Might;
            public int MightP;
            public int Chemist;
            public int ChemistP;
            public int WoodCutter;
            public int WoodCutterP;
            public int Smithy;
            public int SmithyP;
            public int Miner;
            public int MinerP;
            public int Forager;
            public int ForagerP;
            public int Hunter;
            public int HunterP;
            public int Fisher;
            public int FisherP;
            public int Crafter;
            public int CrafterP;
            public int Framer;
            public int FramerP;
            public int Medic;
            public int MedicP;
            public int Tamer;
            public int TamerP;
            public int UILocation;
            public DateTime resettimerstats;
            public DateTime resettimerskills;
            public DateTime playerfixdata;
            public string displayname;
            public string id;
        }

        private class LootData
        {
            public Dictionary<uint, Loot> LootRecords = new Dictionary<uint, Loot>();
        }

        private class Loot
        {
            public uint entity;
            public List<string> id;
        }

        #endregion

        #region SQL

        Core.MySql.Libraries.MySql sqlLibrary = Interface.Oxide.GetLibrary<Core.MySql.Libraries.MySql>();
        Connection sqlConnection;

        private void CreatSQLTable()
        {
            sqlLibrary.Insert(Sql.Builder.Append($"CREATE TABLE IF NOT EXISTS XPerience (" +
                $" `id` BIGINT(255) NOT NULL AUTO_INCREMENT," +
                $" `steamid` BIGINT(255) NOT NULL," +
                $" `displayname` VARCHAR(255) NOT NULL," +
                $" `level` BIGINT(255) NOT NULL," +
                $" `experience` BIGINT(255) NOT NULL," +
                $" `requiredxp` BIGINT(255) NOT NULL," +
                $" `statpoint` BIGINT(255) NOT NULL," +
                $" `skillpoint` BIGINT(255) NOT NULL," +
                $" `Mentality` BIGINT(255) NOT NULL," +
                $" `MentalityP` BIGINT(255) NOT NULL," +
                $" `Dexterity` BIGINT(255) NOT NULL," +
                $" `DexterityP` BIGINT(255) NOT NULL," +
                $" `Might` BIGINT(255) NOT NULL," +
                $" `MightP` BIGINT(255) NOT NULL," +
                $" `Chemist` BIGINT(255) NOT NULL," +
                $" `ChemistP` BIGINT(255) NOT NULL," +
                $" `WoodCutter` BIGINT(255) NOT NULL," +
                $" `WoodCutterP` BIGINT(255) NOT NULL," +
                $" `Smithy` BIGINT(255) NOT NULL," +
                $" `SmithyP` BIGINT(255) NOT NULL," +
                $" `Miner` BIGINT(255) NOT NULL," +
                $" `MinerP` BIGINT(255) NOT NULL," +
                $" `Forager` BIGINT(255) NOT NULL," +
                $" `ForagerP` BIGINT(255) NOT NULL," +
                $" `Hunter` BIGINT(255) NOT NULL," +
                $" `HunterP` BIGINT(255) NOT NULL," +
                $" `Fisher` BIGINT(255) NOT NULL," +
                $" `FisherP` BIGINT(255) NOT NULL," +
                $" `Crafter` BIGINT(255) NOT NULL," +
                $" `CrafterP` BIGINT(255) NOT NULL," +
                $" `Framer` BIGINT(255) NOT NULL," +
                $" `FramerP` BIGINT(255) NOT NULL," +
                $" `Medic` BIGINT(255) NOT NULL," +
                $" `MedicP` BIGINT(255) NOT NULL," +
                $" `Tamer` BIGINT(255) NOT NULL," +
                $" `TamerP` BIGINT(255) NOT NULL," +
                $"PRIMARY KEY (id)" +
                $" );"), sqlConnection);
        }

        private void UpdateSQLTable()
        {
            try
            {
                sqlLibrary.Query(Sql.Builder.Append($"SELECT * FROM XPerience"), sqlConnection, list =>
                {
                    foreach (var entry in list)
                    {
                        if (!entry.ContainsKey("Medic"))
                        {
                            sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE XPerience ADD COLUMN `Medic` BIGINT(255) NOT NULL DEFAULT '0' AFTER FramerP"), sqlConnection);
                        }
                        if (!entry.ContainsKey("MedicP"))
                        {
                            sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE XPerience ADD COLUMN `MedicP` BIGINT(255) NOT NULL DEFAULT '0' AFTER Medic"), sqlConnection);
                        }
                    }
                });
            }
            catch (MySqlException e)
            {
                PrintError("Failed to Update Table (" + e.Message + ")");
            }
        }

        private void CreatePlayerDataSQL(BasePlayer player)
        {
            XPRecord xprecord = GetXPRecord(player);
            // Remove special characters in names to prevent injection
            string removespecials = "(['^$.|?*+()&{}\\\\])";
            string replacename = "\\$1";
            Regex rgx = new Regex(removespecials);
            var playername = rgx.Replace(xprecord.displayname, replacename);
            sqlLibrary.Insert(Sql.Builder.Append($"INSERT XPerience (steamid, displayname, level, experience, requiredxp, statpoint, skillpoint, Mentality, MentalityP, Dexterity, DexterityP, Might, MightP, Chemist, ChemistP, WoodCutter, WoodCutterP, Smithy, SmithyP, Miner, MinerP, Forager, ForagerP, Hunter, HunterP, Fisher, FisherP, Crafter, CrafterP, Framer, FramerP, Medic, MedicP, Tamer, TamerP) " +
            $"VALUES ('" +
            $"{xprecord.id}', " +
            $"'{playername}', " +
            $"'{xprecord.level}', " +
            $"'{xprecord.experience}', " +
            $"'{xprecord.requiredxp}', " +
            $"'{xprecord.statpoint}', " +
            $"'{xprecord.skillpoint}', " +
            $"'{xprecord.Mentality}', " +
            $"'{xprecord.MentalityP}', " +
            $"'{xprecord.Dexterity}', " +
            $"'{xprecord.DexterityP}', " +
            $"'{xprecord.Might}', " +
            $"'{xprecord.MightP}', " +
            $"'{xprecord.Chemist}', " +
            $"'{xprecord.ChemistP}', " +
            $"'{xprecord.WoodCutter}', " +
            $"'{xprecord.WoodCutterP}', " +
            $"'{xprecord.Smithy}', " +
            $"'{xprecord.SmithyP}', " +
            $"'{xprecord.Miner}', " +
            $"'{xprecord.MinerP}', " +
            $"'{xprecord.Forager}', " +
            $"'{xprecord.ForagerP}', " +
            $"'{xprecord.Hunter}', " +
            $"'{xprecord.HunterP}', " +
            $"'{xprecord.Fisher}', " +
            $"'{xprecord.FisherP}', " +
            $"'{xprecord.Crafter}', " +
            $"'{xprecord.CrafterP}', " +
            $"'{xprecord.Framer}', " +
            $"'{xprecord.FramerP}', " +
            $"'{xprecord.Medic}', " +
            $"'{xprecord.MedicP}', " +
            $"'{xprecord.Tamer}', " +
            $"'{xprecord.TamerP}');"), sqlConnection);
        }

        private void UpdatePlayersDataSQL()
        {
            foreach (var r in _xperienceCache)
            {
                // Remove special characters in names to prevent injection
                string removespecials = "(['^$.|?*+()&{}\\\\])";
                string replacename = "\\$1";
                Regex rgx = new Regex(removespecials);
                var playername = rgx.Replace(r.Value.displayname, replacename);

                sqlLibrary.Update(Sql.Builder.Append($"UPDATE XPerience SET " +
                $"steamid='{r.Value.id}', " +
                $"displayname='{playername}', " +
                $"level='{r.Value.level}', " +
                $"experience='{r.Value.experience}', " +
                $"requiredxp='{r.Value.requiredxp}', " +
                $"statpoint='{r.Value.statpoint}', " +
                $"skillpoint='{r.Value.skillpoint}', " +
                $"Mentality='{r.Value.Mentality}', " +
                $"MentalityP='{r.Value.MentalityP}', " +
                $"Dexterity='{r.Value.Dexterity}', " +
                $"DexterityP='{r.Value.DexterityP}', " +
                $"Might='{r.Value.Might}', " +
                $"MightP='{r.Value.MightP}', " +
                $"Chemist='{r.Value.Chemist}', " +
                $"ChemistP='{r.Value.ChemistP}', " +
                $"WoodCutter='{r.Value.WoodCutter}', " +
                $"WoodCutterP='{r.Value.WoodCutterP}', " +
                $"Smithy='{r.Value.Smithy}', " +
                $"SmithyP='{r.Value.SmithyP}', " +
                $"Miner='{r.Value.Miner}', " +
                $"MinerP='{r.Value.MinerP}', " +
                $"Forager='{r.Value.Forager}', " +
                $"ForagerP='{r.Value.ForagerP}', " +
                $"Hunter='{r.Value.Hunter}', " +
                $"HunterP='{r.Value.HunterP}', " +
                $"Fisher='{r.Value.Fisher}', " +
                $"FisherP='{r.Value.FisherP}', " +
                $"Crafter='{r.Value.Crafter}', " +
                $"CrafterP='{r.Value.CrafterP}', " +
                $"Framer='{r.Value.Framer}', " +
                $"FramerP='{r.Value.FramerP}', " +
                $"Medic='{r.Value.Medic}', " +
                $"MedicP='{r.Value.MedicP}', " +
                $"Tamer='{r.Value.Tamer}', " +
                $"TamerP='{r.Value.TamerP}' " +
                $"WHERE steamid = '{r.Key}';"), sqlConnection);
            }
        }

        private void UpdatePlayerDataSQL(BasePlayer player)
        {
            XPRecord xprecord = GetXPRecord(player);
            // Remove special characters in names to prevent injection
            string removespecials = "(['^$.|?*+()&{}\\\\])";
            string replacename = "\\$1";
            Regex rgx = new Regex(removespecials);
            var playername = rgx.Replace(xprecord.displayname, replacename);

            sqlLibrary.Update(Sql.Builder.Append($"UPDATE XPerience SET " +
            $"steamid='{xprecord.id}', " +
            $"displayname='{playername}', " +
            $"level='{xprecord.level}', " +
            $"experience='{xprecord.experience}', " +
            $"requiredxp='{xprecord.requiredxp}', " +
            $"statpoint='{xprecord.statpoint}', " +
            $"skillpoint='{xprecord.skillpoint}', " +
            $"Mentality='{xprecord.Mentality}', " +
            $"MentalityP='{xprecord.MentalityP}', " +
            $"Dexterity='{xprecord.Dexterity}', " +
            $"DexterityP='{xprecord.DexterityP}', " +
            $"Might='{xprecord.Might}', " +
            $"MightP='{xprecord.MightP}', " +
            $"Chemist='{xprecord.Chemist}', " +
            $"ChemistP='{xprecord.ChemistP}', " +
            $"WoodCutter='{xprecord.WoodCutter}', " +
            $"WoodCutterP='{xprecord.WoodCutterP}', " +
            $"Smithy='{xprecord.Smithy}', " +
            $"SmithyP='{xprecord.SmithyP}', " +
            $"Miner='{xprecord.Miner}', " +
            $"MinerP='{xprecord.MinerP}', " +
            $"Forager='{xprecord.Forager}', " +
            $"ForagerP='{xprecord.ForagerP}', " +
            $"Hunter='{xprecord.Hunter}', " +
            $"HunterP='{xprecord.HunterP}', " +
            $"Fisher='{xprecord.Fisher}', " +
            $"FisherP='{xprecord.FisherP}', " +
            $"Crafter='{xprecord.Crafter}', " +
            $"CrafterP='{xprecord.CrafterP}', " +
            $"Framer='{xprecord.Framer}', " +
            $"FramerP='{xprecord.FramerP}', " +
            $"Medic='{xprecord.Medic}', " +
            $"MedicP='{xprecord.MedicP}', " +
            $"Tamer='{xprecord.Tamer}', " +
            $"TamerP='{xprecord.TamerP}' " +
            $"WHERE steamid = '{player.UserIDString}';"), sqlConnection);
        }

        private void CheckPlayerDataSQL(BasePlayer player)
        {
            bool newplayer = true;
            sqlLibrary.Query(Sql.Builder.Append($"SELECT steamid FROM XPerience"), sqlConnection, list =>
            {
                foreach (var entry in list)
                {
                    if (entry["steamid"].ToString() == player.UserIDString)
                    {
                        UpdatePlayerDataSQL(player);
                        newplayer = false;
                    }
                }
                if (newplayer)
                {
                    CreatePlayerDataSQL(player);
                }
            });

        }
        
        private void DeleteSQL()
        {
            sqlLibrary.Delete(Sql.Builder.Append($"DELETE FROM XPerience;"), sqlConnection);
        }

        #endregion

        #region Load/Save

        private void Init()
        {
            Unsubscribe(nameof(OnRunPlayerMetabolism));
            Unsubscribe(nameof(OnPlayerDeath));
            Unsubscribe(nameof(OnResearchCostDetermine));
            Unsubscribe(nameof(CanUnlockTechTreeNode));
            Unsubscribe(nameof(OnTechTreeNodeUnlock));
            Unsubscribe(nameof(OnItemResearch));
            _xperienceCache = new Dictionary<string, XPRecord>();
            _lootCache = new Dictionary<uint, Loot>();
        }

        private void OnServerInitialized()
        {
            _XPerienceData = Interface.Oxide.DataFileSystem.GetFile(nameof(XPerience) + "/XPerience");
            LoadData();
            SaveData();
            SaveLoot();
            if (config.xpReducer.deathreduce)
            {
                Subscribe(nameof(OnPlayerDeath));
            }
            permission.RegisterPermission(Admin, this);
            permission.RegisterPermission(VIP, this);
            Subscribe(nameof(OnRunPlayerMetabolism));
            foreach (var player in BasePlayer.activePlayerList)
            {
                GetXPRecord(player);
                PlayerArmor(player);
                MightAttributes(player);
                LiveStats(player);
            }
            if (config.sql.enablesql)
            {
                sqlConnection = sqlLibrary.OpenDb(config.sql.SQLhost, config.sql.SQLport, config.sql.SQLdatabase, config.sql.SQLusername, config.sql.SQLpassword, this);
                CreatSQLTable();
                UpdateSQLTable();
            }
            if (!config.mentality.useotherresearchmod)
            {
                Subscribe(nameof(OnResearchCostDetermine));
                Subscribe(nameof(CanUnlockTechTreeNode));
                Subscribe(nameof(OnTechTreeNodeUnlock));
                Subscribe(nameof(OnItemResearch));
            }
            Puts("ATTENTION - IF YOU HAVEN'T ALREADY DONE SO WITH 1.1.4 UPDATE YOU MUST RUN CHAT COMMAND /xpupdate THEN USE REPAIR ALL PLAYER DATA IN ADMIN PANEL!");
        }

        private void Unload()
        {
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUi(player, XPerienceLivePrimary);
                DestroyUi(player, XPeriencePlayerControlPrimary);
                DestroyUi(player, XPerienceTopMain);
                DestroyUi(player, XPerienceAdminPanelMain);
            }
            if (config.sql.enablesql)
            {
                UpdatePlayersDataSQL();
                sqlLibrary.CloseDb(sqlConnection);
            }
        }

        private void OnServerShutdown()
        {
            SaveData();
            if (config.sql.enablesql)
            {
                UpdatePlayersDataSQL();
                sqlLibrary.CloseDb(sqlConnection);
            }
        }

        private void OnServerSave()
        {
            SaveData();
            if (config.sql.enablesql)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    CheckPlayerDataSQL(player);
                }
                UpdatePlayersDataSQL();
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            GetXPRecord(player);
            PlayerArmor(player);
            MightAttributes(player);
            LiveStats(player);
            if (config.defaultOptions.showchatprofileonconnect)
            {
                PlayerStatsChat(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyUi(player, XPerienceLivePrimary);
            DestroyUi(player, XPeriencePlayerControlPrimary);
            DestroyUi(player, XPerienceTopMain);
            DestroyUi(player, XPerienceAdminPanelMain);
            if (config.sql.enablesql)
            {
                CheckPlayerDataSQL(player);
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            PlayerArmor(player);
            MightAttributes(player);
        }

        #endregion

        #region PlayerData

        private void UpdateData(BasePlayer player)
        {
            GetXPRecord(player);
            SaveData();
        }

        private XPRecord GetXPRecord(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return null;
            XPRecord xprecord;
            if (_xperienceCache.TryGetValue(player.UserIDString, out xprecord))
            {
                return xprecord;
            }
            if (!_xperienceCache.TryGetValue(player.UserIDString, out xprecord))
            {
                _xperienceCache[player.UserIDString] = xprecord = new XPRecord
                {
                    level = 0,
                    experience = 0,
                    requiredxp = config.xpLevel.levelstart,
                    statpoint = 0,
                    skillpoint = 0,
                    Mentality = 0,
                    MentalityP = 0,
                    Dexterity = 0,
                    DexterityP = 0,
                    Might = 0,
                    MightP = 0,
                    Chemist = 0,
                    ChemistP = 0,
                    WoodCutter = 0,
                    WoodCutterP = 0,
                    Smithy = 0,
                    SmithyP = 0,
                    Miner = 0,
                    MinerP = 0,
                    Forager = 0,
                    ForagerP = 0,
                    Hunter = 0,
                    HunterP = 0,
                    Fisher = 0,
                    FisherP = 0,
                    Crafter = 0,
                    CrafterP = 0,
                    Framer = 0,
                    FramerP = 0,
                    Medic = 0,
                    MedicP = 0,
                    Tamer = 0,
                    TamerP = 0,
                    UILocation = 1,
                    resettimerstats = DateTime.Now,
                    resettimerskills = DateTime.Now,
                    playerfixdata = DateTime.Now,
                };
                xprecord.id = player.UserIDString;
                xprecord.displayname = player.displayName;
            }
            return xprecord;
        }

        private XPRecord GetPlayerRecord(string player)
        {
            if (player == null) return null;
            XPRecord xprecord;
            if (_xperienceCache.TryGetValue(player, out xprecord))
            {
                return xprecord;
            }
            return xprecord;
        }

        private void AddLootData(BasePlayer player, BaseEntity entity)
        {
            Loot loot;
            if (!_lootCache.TryGetValue(entity.net.ID, out loot))
            {
                _lootCache.Add(entity.net.ID, loot = new Loot
                {
                    id = new List<string>(),
                });
            }

            if (!loot.id.Contains(player.UserIDString))
            {
                loot.id.Add(player.UserIDString);
            }
        }

        private static BasePlayer FindPlayer(string playerid)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == playerid)
                    return activePlayer;
            }
            foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.UserIDString == playerid)
                    return sleepingPlayer;
            }
            return null;
        }

        #endregion

        #region Level/XP/Stat/Skill Control

        private void GainExp(BasePlayer player, double e)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord.level >= config.xpLevel.maxlevel) return;
            if(xprecord.experience <= 0)
            {
                xprecord.experience = 0;
            }
            // Clans
            if (Clans != null && config.xpclans.enableclanbonus && e != 0)
            {
                XPClans(player, e, "addxp");
            }
            if (IsNight() && config.nightBonus.Enable)
            {
                double timebonus = e * config.nightBonus.Bonus;
                e = e + timebonus;
            }
            if (xprecord.level > 0)
            {
                e = e + ((config.xpLevel.levelxpboost * xprecord.level) * e);
            }
            xprecord.experience = (int)xprecord.experience + e;
            if (xprecord.experience >= xprecord.requiredxp)
            {
                LvlUp(player, 0, 0);
            }
            LiveStats(player, true);
            // UINotify
            if (UINotify != null && config.UiNotifier.useuinotify && config.UiNotifier.xpgainloss && e != 0)
            {
                UINotify.Call("SendNotify", player, config.UiNotifier.xpgainlosstype, XPLang("uinotify_xpgain", player.UserIDString, Math.Round(e)));
            }
        }

        private void GainExpAdmin(BasePlayer player, double e)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            XPRecord xprecord = GetXPRecord(player);
             xprecord.experience = (int)xprecord.experience + e;
            if (xprecord.experience >= xprecord.requiredxp)
            {
                LvlUp(player, 0, 0);
            }
            LiveStats(player, true);
        }

        private void LvlUp(BasePlayer player, int chatstatpoint, int chatskillpoint)
        {
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord.level >= config.xpLevel.maxlevel) return;
            xprecord.level = xprecord.level + 1;
            xprecord.statpoint = xprecord.statpoint + config.xpLevel.statpointsperlvl;
            xprecord.skillpoint = xprecord.skillpoint + config.xpLevel.skillpointsperlvl;
            xprecord.requiredxp = Math.Round(xprecord.requiredxp + (xprecord.level * config.xpLevel.levelmultiplier));
            chatstatpoint += config.xpLevel.statpointsperlvl;
            chatskillpoint += config.xpLevel.skillpointsperlvl;
            MightAttributes(player);
            if (xprecord.experience > xprecord.requiredxp)
            {
                LvlUp(player, chatstatpoint, chatskillpoint);
                return;
            }
            // UINotify
            if (UINotify != null && config.UiNotifier.useuinotify && config.UiNotifier.levelupdown)
            {
                UINotify.Call("SendNotify", player, config.UiNotifier.levelupdowntype, XPLang("levelup", player.UserIDString, xprecord.level, chatstatpoint, chatskillpoint));
            }
            // Normal Chat Notify
            if (!config.UiNotifier.disablechats)
            {
                player.ChatMessage(XPLang("levelup", player.UserIDString, xprecord.level, chatstatpoint, chatskillpoint));
            }
            // Econ
            if (Economics != null && config.xpEcon.econlevelup)
            {
                Economics.Call("Deposit", player.UserIDString, config.xpEcon.econlevelreward);
                player.ChatMessage(XPLang("econdeposit", player.UserIDString, config.xpEcon.econlevelreward));
            }
            // Server Rewards
            if (ServerRewards != null && config.sRewards.srewardlevelup)
            {
                ServerRewards?.Call("AddPoints", player.userID, config.sRewards.srewardlevelupamt);
                player.ChatMessage(XPLang("srewardsup", player.UserIDString, config.sRewards.srewardlevelupamt));
            }
        }

        private void LoseExp(BasePlayer player, double e)
        {
            XPRecord xprecord = GetXPRecord(player);
            if (e < 1)
            {
                e = 1;
            }
            // Clans
            if (Clans != null && config.xpclans.enableclanreduction && e != 0)
            {
                XPClans(player, e, "takexp");
            }
            double newxp = xprecord.experience - e;
            double nextlevel = xprecord.requiredxp;
            // Make sure XP does not go negative
            if (newxp <= 0)
            {
                newxp = 0;
            }
            xprecord.experience = (int)newxp;
            if (nextlevel == config.xpLevel.levelstart) return;
            double prevlevel = xprecord.requiredxp - (xprecord.level * config.xpLevel.levelmultiplier);
            if (xprecord.experience < prevlevel)
            {
                LvlDown(player, 0, 0);
            }
            LiveStats(player, true);
            // UINotify
            if (UINotify != null && config.UiNotifier.useuinotify && config.UiNotifier.xpgainloss)
            {
                UINotify.Call("SendNotify", player, config.UiNotifier.xpgainlosstype, XPLang("uinotify_xploss", player.UserIDString, Math.Round(e)));
            }
        }

        private void LvlDown(BasePlayer player, int chatstatpoint, int chatskillpoint)
        {
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord.level < 1) return;
            xprecord.level = xprecord.level - 1;
            xprecord.requiredxp = Math.Round(xprecord.requiredxp - (xprecord.level * config.xpLevel.levelmultiplier));
            chatstatpoint -= config.xpLevel.statpointsperlvl;
            chatskillpoint -= config.xpLevel.skillpointsperlvl;
            bool removestatlvl = false;
            bool removeskilllvl = false;
            // UINotify
            if (UINotify != null && config.UiNotifier.useuinotify && config.UiNotifier.levelupdown)
            {
                UINotify.Call("SendNotify", player, config.UiNotifier.levelupdowntype, XPLang("leveldown", player.UserIDString, xprecord.level));
            }
            // Normal Chat Notify
            if (!config.UiNotifier.disablechats)
            {
                player.ChatMessage(XPLang("leveldown", player.UserIDString, xprecord.level));
            }
            // Check if player has enough unspent stat points to take
            if (xprecord.statpoint >= config.xpLevel.statpointsperlvl)
            {
                xprecord.statpoint = xprecord.statpoint - config.xpLevel.statpointsperlvl;
                player.ChatMessage(XPLang("statdown", player.UserIDString, config.xpLevel.statpointsperlvl));
            }
            else
            {
                removestatlvl = true;
            }

            // Check if player has enough unspent skill points to take
            if (xprecord.skillpoint >= config.xpLevel.skillpointsperlvl)
            {
                xprecord.skillpoint = xprecord.skillpoint - config.xpLevel.skillpointsperlvl;
                player.ChatMessage(XPLang("skilldown", player.UserIDString, config.xpLevel.skillpointsperlvl));
            }
            else
            {
                removeskilllvl = true;
            }

            // If player does not have enough unspent stat points then get first available stat to level down and remove points
            if (removestatlvl == true)
            {
                int allstats = xprecord.Mentality + xprecord.Dexterity + xprecord.Might + xprecord.Chemist;
                if (allstats == 0)
                {
                    xprecord.statpoint = 0;
                    player.ChatMessage(XPLang("nostatpoints", player.UserIDString));
                    return;
                }

                var stat = "";
                int statpoints = 0;
                int pointadj = 0;
                bool dropmentality = false;
                bool dropdexterity = false;
                bool dropmight = false;
                bool dropchemist = false;

                // Check each stat for levels
                if (xprecord.Mentality > 0) { dropmentality = true; }
                else if (xprecord.Dexterity > 0) { dropdexterity = true; }
                else if (xprecord.Might > 0) { dropmight = true; }
                else if (xprecord.Chemist > 0) { dropchemist = true; }

                // Random stat chosen
                if (dropmentality == true)
                {
                    stat = "Mentality";
                    if (xprecord.Mentality == 1)
                    {
                        statpoints = config.mentality.pointcoststart;
                    }
                    else
                    {
                        statpoints = (xprecord.Mentality - 1) * config.mentality.costmultiplier;
                    }
                    pointadj = statpoints - config.xpLevel.statpointsperlvl;
                    xprecord.Mentality = xprecord.Mentality - 1;
                    xprecord.MentalityP = xprecord.MentalityP - statpoints;
                    xprecord.statpoint = pointadj;
                }
                else if (dropdexterity == true)
                {
                    stat = "Dexterity";
                    if (xprecord.Dexterity == 1)
                    {
                        statpoints = config.dexterity.pointcoststart;
                    }
                    else
                    {
                        statpoints = (xprecord.Dexterity - 1) * config.dexterity.costmultiplier;
                    }
                    pointadj = statpoints - config.xpLevel.statpointsperlvl;
                    xprecord.Dexterity = xprecord.Dexterity - 1;
                    xprecord.DexterityP = xprecord.DexterityP - statpoints;
                    xprecord.statpoint = pointadj;
                }
                else if (dropmight == true)
                {
                    stat = "Might";
                    if (xprecord.Might == 1)
                    {
                        statpoints = config.might.pointcoststart;
                    }
                    else
                    {
                        statpoints = (xprecord.Might - 1) * config.might.costmultiplier;
                    }
                    pointadj = statpoints - config.xpLevel.statpointsperlvl;
                    xprecord.Might = xprecord.Might - 1;
                    xprecord.MightP = xprecord.MightP - statpoints;
                    xprecord.statpoint = pointadj;
                    MightAttributes(player);
                }
                else if (dropchemist == true)
                {
                    stat = "Chemist";
                    if (xprecord.Chemist == 1)
                    {
                        statpoints = config.chemist.pointcoststart;
                    }
                    else
                    {
                        statpoints = (xprecord.Chemist - 1) * config.chemist.costmultiplier;
                    }
                    pointadj = statpoints - config.xpLevel.statpointsperlvl;
                    xprecord.Chemist = xprecord.Chemist - 1;
                    xprecord.ChemistP = xprecord.ChemistP - statpoints;
                    xprecord.statpoint = pointadj;
                }
                // Make sure points do not go negative
                if (xprecord.statpoint < 0)
                {
                    xprecord.statpoint = 0;
                }

                player.ChatMessage(XPLang("statdownextra", player.UserIDString, stat, config.xpLevel.statpointsperlvl, pointadj));
            }

            // If player does not have enough unspent skill points then get first available skill to level down and remove points
            if (removeskilllvl == true)
            {
                int allskills = xprecord.WoodCutter + xprecord.Smithy + xprecord.Miner + xprecord.Forager + xprecord.Hunter + xprecord.Fisher + xprecord.Crafter + xprecord.Framer + xprecord.Medic + xprecord.Tamer;
                if (allskills == 0)
                {
                    xprecord.skillpoint = 0;
                    player.ChatMessage(XPLang("noskillpoints", player.UserIDString));
                    return;
                }

                var skill = "";
                int skillpoints = 0;
                int pointadj = 0;
                bool dropwoodcutter = false;
                bool dropsmithy = false;
                bool dropminer = false;
                bool dropforager = false;
                bool drophunter = false;
                bool dropfisher = false;
                bool dropcrafter = false;
                bool dropframer = false;
                bool dropmedic = false;
                bool droptamer = false;

                // Check each skill for levels
                if (xprecord.WoodCutter > 0) { dropwoodcutter = true; }
                else if (xprecord.Smithy > 0) { dropsmithy = true; }
                else if (xprecord.Miner > 0) { dropminer = true; }
                else if (xprecord.Forager > 0) { dropforager = true; }
                else if (xprecord.Hunter > 0) { drophunter = true; }
                else if (xprecord.Fisher > 0) { dropfisher = true; }
                else if (xprecord.Crafter > 0) { dropcrafter = true; }
                else if (xprecord.Framer > 0) { dropframer = true; }
                else if (xprecord.Medic > 0) { dropmedic = true; }
                else if (xprecord.Tamer > 0) { droptamer = true; }

                // Random Skill Chosen
                if (dropwoodcutter == true)
                {
                    skill = "WoodCutter";
                    if (xprecord.WoodCutter == 1)
                    {
                        skillpoints = config.woodcutter.pointcoststart;
                    }
                    else
                    {
                        skillpoints = xprecord.WoodCutter * config.woodcutter.costmultiplier;
                    }
                    pointadj = skillpoints - config.xpLevel.skillpointsperlvl;
                    xprecord.WoodCutter = xprecord.WoodCutter - 1;
                    xprecord.WoodCutterP = xprecord.WoodCutterP - skillpoints;
                    xprecord.skillpoint = pointadj;
                }
                else if (dropsmithy == true)
                {
                    skill = "Smithy";
                    if (xprecord.Smithy == 1)
                    {
                        skillpoints = config.smithy.pointcoststart;
                    }
                    else
                    {
                        skillpoints = xprecord.Smithy * config.smithy.costmultiplier;
                    }
                    pointadj = skillpoints - config.xpLevel.skillpointsperlvl;
                    xprecord.Smithy = xprecord.Smithy - 1;
                    xprecord.SmithyP = xprecord.SmithyP - skillpoints;
                    xprecord.skillpoint = pointadj;
                }
                else if (dropminer == true)
                {
                    skill = "Miner";
                    if (xprecord.Miner == 1)
                    {
                        skillpoints = config.miner.pointcoststart;
                    }
                    else
                    {
                        skillpoints = xprecord.Miner * config.miner.costmultiplier;
                    }
                    pointadj = skillpoints - config.xpLevel.skillpointsperlvl;
                    xprecord.Miner = xprecord.Miner - 1;
                    xprecord.MinerP = xprecord.MinerP - skillpoints;
                    xprecord.skillpoint = pointadj;
                }
                else if (dropforager == true)
                {
                    skill = "Forager";
                    if (xprecord.Forager == 1)
                    {
                        skillpoints = config.forager.pointcoststart;
                    }
                    else
                    {
                        skillpoints = xprecord.Forager * config.forager.costmultiplier;
                    }
                    pointadj = skillpoints - config.xpLevel.skillpointsperlvl;
                    xprecord.Forager = xprecord.Forager - 1;
                    xprecord.ForagerP = xprecord.ForagerP - skillpoints;
                    xprecord.skillpoint = pointadj;
                }
                else if (drophunter == true)
                {
                    skill = "Hunter";
                    if (xprecord.Hunter == 1)
                    {
                        skillpoints = config.hunter.pointcoststart;
                    }
                    else
                    {
                        skillpoints = xprecord.Hunter * config.hunter.costmultiplier;
                    }
                    pointadj = skillpoints - config.xpLevel.skillpointsperlvl;
                    xprecord.Hunter = xprecord.Hunter - 1;
                    xprecord.HunterP = xprecord.HunterP - skillpoints;
                    xprecord.skillpoint = pointadj;
                }
                else if (dropfisher == true)
                {
                    skill = "Fisher";
                    if (xprecord.Fisher == 1)
                    {
                        skillpoints = config.fisher.pointcoststart;
                    }
                    else
                    {
                        skillpoints = xprecord.Fisher * config.fisher.costmultiplier;
                    }
                    pointadj = skillpoints - config.xpLevel.skillpointsperlvl;
                    xprecord.Fisher = xprecord.Fisher - 1;
                    xprecord.FisherP = xprecord.FisherP - skillpoints;
                    xprecord.skillpoint = pointadj;
                }
                else if (dropcrafter == true)
                {
                    skill = "Crafter";
                    if (xprecord.Crafter == 1)
                    {
                        skillpoints = config.crafter.pointcoststart;
                    }
                    else
                    {
                        skillpoints = xprecord.Crafter * config.crafter.costmultiplier;
                    }
                    pointadj = skillpoints - config.xpLevel.skillpointsperlvl;
                    xprecord.Crafter = xprecord.Crafter - 1;
                    xprecord.CrafterP = xprecord.CrafterP - skillpoints;
                    xprecord.skillpoint = pointadj;
                }
                else if (dropframer == true)
                {
                    skill = "Framer";
                    if (xprecord.Framer == 1)
                    {
                        skillpoints = config.framer.pointcoststart;
                    }
                    else
                    {
                        skillpoints = xprecord.Framer * config.framer.costmultiplier;
                    }
                    pointadj = skillpoints - config.xpLevel.skillpointsperlvl;
                    xprecord.Framer = xprecord.Framer - 1;
                    xprecord.FramerP = xprecord.FramerP - skillpoints;
                    xprecord.skillpoint = pointadj;
                }
                else if (dropmedic == true)
                {
                    skill = "Medic";
                    if (xprecord.Medic == 1)
                    {
                        skillpoints = config.medic.pointcoststart;
                    }
                    else
                    {
                        skillpoints = xprecord.Medic * config.medic.costmultiplier;
                    }
                    pointadj = skillpoints - config.xpLevel.skillpointsperlvl;
                    xprecord.Medic = xprecord.Framer - 1;
                    xprecord.MedicP = xprecord.MedicP - skillpoints;
                    xprecord.skillpoint = pointadj;
                }
                else if (droptamer == true)
                {
                    skill = "Tamer";
                    if (xprecord.Tamer == 1)
                    {
                        skillpoints = config.tamer.pointcoststart;
                    }
                    else
                    {
                        skillpoints = xprecord.Tamer * config.tamer.costmultiplier;
                    }
                    pointadj = skillpoints - config.xpLevel.skillpointsperlvl;
                    xprecord.Tamer = xprecord.Tamer - 1;
                    xprecord.TamerP = xprecord.TamerP - skillpoints;
                    xprecord.skillpoint = pointadj;
                    PetChecks(player, false, xprecord.Tamer);
                }
                // Make sure points do not go negative
                if (xprecord.skillpoint < 0)
                {
                    xprecord.skillpoint = 0;
                }

                player.ChatMessage(XPLang("skilldownextra", player.UserIDString, skill, config.xpLevel.skillpointsperlvl, pointadj));
            }
            // Econ
            if (Economics != null && config.xpEcon.econleveldown)
            {
                Economics.Call("Withdraw", player.UserIDString, config.xpEcon.econlevelreduction);
                player.ChatMessage(XPLang("econwidthdrawlevel", player.UserIDString, config.xpEcon.econlevelreduction));
            }
            // Server Rewards
            if (ServerRewards != null && config.sRewards.srewardleveldown)
            {
                ServerRewards?.Call("TakePoints", player.userID, config.sRewards.srewardleveldownamt);
                player.ChatMessage(XPLang("srewardsdown", player.UserIDString, config.sRewards.srewardleveldownamt));
            }
            double prevlevel = Math.Round(xprecord.requiredxp / config.xpLevel.levelmultiplier);
            if (prevlevel > xprecord.experience)
            {
                LvlDown(player, 0, 0);
            }
        }

        private void StatUp(BasePlayer player, string stat)
        {
            XPRecord xprecord = GetXPRecord(player);
            int nextlevel = 0;
            int statcost = 0;
            int pointsremaining = 0;
            int pointsinstat = 0;

            // Mentality
            if (stat == "mentality" && config.mentality.maxlvl != 0)
            {
                if (xprecord.Mentality == 0)
                {
                    nextlevel = 1;
                    statcost = config.mentality.pointcoststart;
                    pointsremaining = xprecord.statpoint - statcost;
                    pointsinstat = xprecord.MentalityP + statcost;
                }
                else
                {
                    nextlevel = xprecord.Mentality + 1;
                    statcost = nextlevel * config.mentality.costmultiplier;
                    pointsremaining = xprecord.statpoint - statcost;
                    pointsinstat = xprecord.MentalityP + statcost;
                }
                if (xprecord.statpoint < statcost)
                {
                    player.ChatMessage(XPLang("notenoughstatpoints", player.UserIDString, nextlevel, stat, statcost));
                    return;
                }
                xprecord.Mentality = nextlevel;
                xprecord.statpoint = pointsremaining;
                xprecord.MentalityP = pointsinstat;
            }
            // Dexterity
            if (stat == "dexterity" && config.dexterity.maxlvl != 0)
            {
                if (xprecord.Dexterity == 0)
                {
                    nextlevel = 1;
                    statcost = config.dexterity.pointcoststart;
                    pointsremaining = xprecord.statpoint - statcost;
                    pointsinstat = xprecord.DexterityP + statcost;
                }
                else
                {
                    nextlevel = xprecord.Dexterity + 1;
                    statcost = nextlevel * config.dexterity.costmultiplier;
                    pointsremaining = xprecord.statpoint - statcost;
                    pointsinstat = xprecord.DexterityP + statcost;
                }
                if (xprecord.statpoint < statcost)
                {
                    player.ChatMessage(XPLang("notenoughstatpoints", player.UserIDString, nextlevel, stat, statcost));
                    return;
                }
                xprecord.Dexterity = nextlevel;
                xprecord.statpoint = pointsremaining;
                xprecord.DexterityP = pointsinstat;
            }
            // Might
            if (stat == "might" && config.might.maxlvl != 0)
            {
                if (xprecord.Might == 0)
                {
                    nextlevel = 1;
                    statcost = config.might.pointcoststart;
                    pointsremaining = xprecord.statpoint - statcost;
                    pointsinstat = xprecord.MightP + statcost;
                }
                else
                {
                    nextlevel = xprecord.Might + 1;
                    statcost = nextlevel * config.might.costmultiplier;
                    pointsremaining = xprecord.statpoint - statcost;
                    pointsinstat = xprecord.MightP + statcost;
                }
                if (xprecord.statpoint < statcost)
                {
                    player.ChatMessage(XPLang("notenoughstatpoints", player.UserIDString, nextlevel, stat, statcost));
                    return;
                }
                xprecord.Might = nextlevel;
                xprecord.statpoint = pointsremaining;
                xprecord.MightP = pointsinstat;
                PlayerArmor(player);
                MightAttributes(player);
            }
            // Chemist
            if (stat == "chemist" && config.chemist.maxlvl != 0)
            {
                if (xprecord.Chemist == 0)
                {
                    nextlevel = 1;
                    statcost = config.chemist.pointcoststart;
                    pointsremaining = xprecord.statpoint - statcost;
                    pointsinstat = xprecord.ChemistP + statcost;
                }
                else
                {
                    nextlevel = xprecord.Chemist + 1;
                    statcost = nextlevel * config.chemist.costmultiplier;
                    pointsremaining = xprecord.statpoint - statcost;
                    pointsinstat = xprecord.ChemistP + statcost;
                }
                if (xprecord.statpoint < statcost)
                {
                    player.ChatMessage(XPLang("notenoughstatpoints", player.UserIDString, nextlevel, stat, statcost));
                    return;
                }
                xprecord.Chemist = nextlevel;
                xprecord.statpoint = pointsremaining;
                xprecord.ChemistP = pointsinstat;
            }

            player.ChatMessage(XPLang("statup", player.UserIDString, statcost, nextlevel, stat));
            LiveStats(player, true);
        }

        private void SkillUp(BasePlayer player, string skill)
        {
            XPRecord xprecord = GetXPRecord(player);
            int nextlevel = 0;
            int skillcost = 0;
            int pointsremaining = 0;
            int pointsinskill = 0;

            // WoodCutter
            if (skill == "woodcutter" && config.woodcutter.maxlvl != 0)
            {
                if (xprecord.WoodCutter == 0)
                {
                    nextlevel = 1;
                    skillcost = config.woodcutter.pointcoststart;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.WoodCutterP + skillcost;
                }
                else
                {
                    nextlevel = xprecord.WoodCutter + 1;
                    skillcost = nextlevel * config.woodcutter.costmultiplier;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.WoodCutterP + skillcost;
                }
                if (xprecord.skillpoint < skillcost)
                {
                    player.ChatMessage(XPLang("notenoughskillpoints", player.UserIDString, nextlevel, skill, skillcost));
                    return;
                }
                xprecord.WoodCutter = nextlevel;
                xprecord.skillpoint = pointsremaining;
                xprecord.WoodCutterP = pointsinskill;
            }
            // Smithy
            if (skill == "smithy" && config.smithy.maxlvl != 0)
            {
                if (xprecord.Smithy == 0)
                {
                    nextlevel = 1;
                    skillcost = config.smithy.pointcoststart;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.SmithyP + skillcost;
                }
                else
                {
                    nextlevel = xprecord.Smithy + 1;
                    skillcost = nextlevel * config.smithy.costmultiplier;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.SmithyP + skillcost;
                }
                if (xprecord.skillpoint < skillcost)
                {
                    player.ChatMessage(XPLang("notenoughskillpoints", player.UserIDString, nextlevel, skill, skillcost));
                    return;
                }
                xprecord.Smithy = nextlevel;
                xprecord.skillpoint = pointsremaining;
                xprecord.SmithyP = pointsinskill;
            }
            // Miner
            if (skill == "miner" && config.miner.maxlvl != 0)
            {
                if (xprecord.Miner == 0)
                {
                    nextlevel = 1;
                    skillcost = config.miner.pointcoststart;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.MinerP + skillcost;
                }
                else
                {
                    nextlevel = xprecord.Miner + 1;
                    skillcost = nextlevel * config.miner.costmultiplier;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.MinerP + skillcost;
                }
                if (xprecord.skillpoint < skillcost)
                {
                    player.ChatMessage(XPLang("notenoughskillpoints", player.UserIDString, nextlevel, skill, skillcost));
                    return;
                }
                xprecord.Miner = nextlevel;
                xprecord.skillpoint = pointsremaining;
                xprecord.MinerP = pointsinskill;
            }
            // Forager
            if (skill == "forager" && config.forager.maxlvl != 0)
            {
                if (xprecord.Forager == 0)
                {
                    nextlevel = 1;
                    skillcost = config.forager.pointcoststart;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.ForagerP + skillcost;
                }
                else
                {
                    nextlevel = xprecord.Forager + 1;
                    skillcost = nextlevel * config.forager.costmultiplier;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.ForagerP + skillcost;
                }
                if (xprecord.skillpoint < skillcost)
                {
                    player.ChatMessage(XPLang("notenoughskillpoints", player.UserIDString, nextlevel, skill, skillcost));
                    return;
                }
                xprecord.Forager = nextlevel;
                xprecord.skillpoint = pointsremaining;
                xprecord.ForagerP = pointsinskill;
            }
            // Hunter
            if (skill == "hunter" && config.hunter.maxlvl != 0)
            {
                if (xprecord.Hunter == 0)
                {
                    nextlevel = 1;
                    skillcost = config.hunter.pointcoststart;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.HunterP + skillcost;
                }
                else
                {
                    nextlevel = xprecord.Hunter + 1;
                    skillcost = nextlevel * config.hunter.costmultiplier;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.HunterP + skillcost;
                }
                if (xprecord.skillpoint < skillcost)
                {
                    player.ChatMessage(XPLang("notenoughskillpoints", player.UserIDString, nextlevel, skill, skillcost));
                    return;
                }
                xprecord.Hunter = nextlevel;
                xprecord.skillpoint = pointsremaining;
                xprecord.HunterP = pointsinskill;
            }
            // Fisher
            if (skill == "fisher" && config.fisher.maxlvl != 0)
            {
                if (xprecord.Fisher == 0)
                {
                    nextlevel = 1;
                    skillcost = config.fisher.pointcoststart;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.FisherP + skillcost;
                }
                else
                {
                    nextlevel = xprecord.Fisher + 1;
                    skillcost = nextlevel * config.fisher.costmultiplier;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.FisherP + skillcost;
                }
                if (xprecord.skillpoint < skillcost)
                {
                    player.ChatMessage(XPLang("notenoughskillpoints", player.UserIDString, nextlevel, skill, skillcost));
                    return;
                }
                xprecord.Fisher = nextlevel;
                xprecord.skillpoint = pointsremaining;
                xprecord.FisherP = pointsinskill;
            }
            // Crafter
            if (skill == "crafter" && config.crafter.maxlvl != 0)
            {
                if (xprecord.Crafter == 0)
                {
                    nextlevel = 1;
                    skillcost = config.crafter.pointcoststart;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.CrafterP + skillcost;
                }
                else
                {
                    nextlevel = xprecord.Crafter + 1;
                    skillcost = nextlevel * config.crafter.costmultiplier;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.CrafterP + skillcost;
                }
                if (xprecord.skillpoint < skillcost)
                {
                    player.ChatMessage(XPLang("notenoughskillpoints", player.UserIDString, nextlevel, skill, skillcost));
                    return;
                }
                xprecord.Crafter = nextlevel;
                xprecord.skillpoint = pointsremaining;
                xprecord.CrafterP = pointsinskill;
            }
            // Framer
            if (skill == "framer" && config.framer.maxlvl != 0)
            {
                if (xprecord.Framer == 0)
                {
                    nextlevel = 1;
                    skillcost = config.framer.pointcoststart;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.FramerP + skillcost;
                }
                else
                {
                    nextlevel = xprecord.Framer + 1;
                    skillcost = nextlevel * config.framer.costmultiplier;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.FramerP + skillcost;
                }
                if (xprecord.skillpoint < skillcost)
                {
                    player.ChatMessage(XPLang("notenoughskillpoints", player.UserIDString, nextlevel, skill, skillcost));
                    return;
                }
                xprecord.Framer = nextlevel;
                xprecord.skillpoint = pointsremaining;
                xprecord.FramerP = pointsinskill;
            }
            // Medic
            if (skill == "medic" && config.medic.maxlvl != 0)
            {
                if (xprecord.Medic == 0)
                {
                    nextlevel = 1;
                    skillcost = config.medic.pointcoststart;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.MedicP + skillcost;
                }
                else
                {
                    nextlevel = xprecord.Medic + 1;
                    skillcost = nextlevel * config.medic.costmultiplier;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.MedicP + skillcost;
                }
                if (xprecord.skillpoint < skillcost)
                {
                    player.ChatMessage(XPLang("notenoughskillpoints", player.UserIDString, nextlevel, skill, skillcost));
                    return;
                }
                xprecord.Medic = nextlevel;
                xprecord.skillpoint = pointsremaining;
                xprecord.MedicP = pointsinskill;
            }
            // Tamer
            if (skill == "tamer")
            {
                if (xprecord.Tamer == 0)
                {
                    nextlevel = 1;
                    skillcost = config.tamer.pointcoststart;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.TamerP + skillcost;
                }
                else
                {
                    nextlevel = xprecord.Tamer + 1;
                    skillcost = nextlevel * config.tamer.costmultiplier;
                    pointsremaining = xprecord.skillpoint - skillcost;
                    pointsinskill = xprecord.TamerP + skillcost;
                }
                if (xprecord.skillpoint < skillcost)
                {
                    player.ChatMessage(XPLang("notenoughskillpoints", player.UserIDString, nextlevel, skill, skillcost));
                    return;
                }
                xprecord.Tamer = nextlevel;
                xprecord.skillpoint = pointsremaining;
                xprecord.TamerP = pointsinskill;
                PetChecks(player, false, nextlevel);
            }

            player.ChatMessage(XPLang("skillup", player.UserIDString, skillcost, nextlevel, skill));
            LiveStats(player, true);
        }

        private void StatsReset(BasePlayer player)
        {
            XPRecord xprecord = GetXPRecord(player);
            int timer = 0;
            if (permission.UserHasPermission(player.UserIDString, XPerience.VIP))
            {
                DateTime resettimestats = xprecord.resettimerstats.AddMinutes(config.defaultOptions.vipresetminstats);
                TimeSpan interval = resettimestats - DateTime.Now;
                timer = (int)interval.TotalMinutes;
            }
            else
            {
                DateTime resettimestats = xprecord.resettimerstats.AddMinutes(config.defaultOptions.resetminsstats);
                TimeSpan interval = resettimestats - DateTime.Now;
                timer = (int)interval.TotalMinutes;
            }
            if (config.defaultOptions.bypassadminreset && player.IsAdmin && permission.UserHasPermission(player.UserIDString, XPerience.Admin))
            {
                timer = 0;
            }
            if (timer > 0 && config.defaultOptions.restristresets)
            {
                player.ChatMessage(XPLang("resettimerstats", player.UserIDString, timer));
                return;
            }
            // Econ
            if (Economics != null && config.xpEcon.econresetstats)
            {
                Economics.Call("Withdraw", player.UserIDString, config.xpEcon.econresetstatscost);
                player.ChatMessage(XPLang("econwidthdrawresetstat", player.UserIDString, config.xpEcon.econresetstatscost));
            }
            // Reset max health if needed before removing points
            if (player._maxHealth > 100 || player._health > 100)
            {    
                // Get Stats
                double armor = (xprecord.Might * config.might.armor) * 100;
                double currentmaxhealth = player._maxHealth;
                double currenthealth = Mathf.Ceil(player._health);
                // Remove Armor
                double newmaxhealth = currentmaxhealth - armor;
                double newhealth = currenthealth - armor;
                // Change Health
                if (newmaxhealth < 100)
                {
                    player._maxHealth = 100;
                    player._health = (float)newhealth;
                }
                else
                {
                    player._maxHealth = (float)newmaxhealth;
                    player._health = (float)newhealth;
                } 
                //player._health = 100;
            }
            // Add all spent points
            int statpoints = xprecord.statpoint + xprecord.MentalityP + xprecord.DexterityP + xprecord.MightP + xprecord.ChemistP;
            // Refund Points
            xprecord.statpoint = statpoints;
            // Reset Stat Levels
            xprecord.Mentality = 0;
            xprecord.Dexterity = 0;
            xprecord.Might = 0;
            xprecord.Chemist = 0;
            // Reset Stat Spent Points
            xprecord.MentalityP = 0;
            xprecord.DexterityP = 0;
            xprecord.MightP = 0;
            xprecord.ChemistP = 0;
            if (player.metabolism.calories.max > 500)
            {
                player.metabolism.calories.max = 500;
            }
            if (player.metabolism.hydration.max > 250)
            {
                player.metabolism.hydration.max = 250;
            }
            // New Reset Timer
            xprecord.resettimerstats = DateTime.Now;
            // Message Player with number of stat points returned
            player.ChatMessage(XPLang("resetstats", player.UserIDString, statpoints));
            // Update Live UI
            LiveStats(player, true);
        }

        private void SkillsReset(BasePlayer player)
        {
            XPRecord xprecord = GetXPRecord(player);
            int timer = 0;
            if (permission.UserHasPermission(player.UserIDString, XPerience.VIP))
            {
                DateTime resettimeskills = xprecord.resettimerstats.AddMinutes(config.defaultOptions.resetminsskills);
                TimeSpan interval = resettimeskills - DateTime.Now;
                timer = (int)interval.TotalMinutes;
            }
            else
            {
                DateTime resettimeskills = xprecord.resettimerstats.AddMinutes(config.defaultOptions.resetminsskills);
                TimeSpan interval = resettimeskills - DateTime.Now;
                timer = (int)interval.TotalMinutes;
            }
            if (config.defaultOptions.bypassadminreset && player.IsAdmin && permission.UserHasPermission(player.UserIDString, XPerience.Admin))
            {
                timer = 0;
            }
            if (timer > 0 && config.defaultOptions.restristresets)
            {
                player.ChatMessage(XPLang("resettimerskills", player.UserIDString, timer));
                return;
            }
            // Econ
            if (Economics != null && config.xpEcon.econresetskills)
            {
                Economics.Call("Withdraw", player.UserIDString, config.xpEcon.econresetskillscost);
                player.ChatMessage(XPLang("econwidthdrawresetskill", player.UserIDString, config.xpEcon.econresetskillscost));
            }
            // Add all spent points
            int skillpoints = xprecord.skillpoint + xprecord.WoodCutterP + xprecord.SmithyP + xprecord.MinerP + xprecord.ForagerP + xprecord.HunterP + xprecord.FisherP + xprecord.CrafterP + xprecord.FramerP + +xprecord.MedicP + xprecord.TamerP;
            // Reset Skill Levels
            xprecord.skillpoint = skillpoints;
            xprecord.WoodCutter = 0;
            xprecord.Smithy = 0;
            xprecord.Miner = 0;
            xprecord.Forager = 0;
            xprecord.Hunter = 0;
            xprecord.Fisher = 0;
            xprecord.Crafter = 0;
            xprecord.Framer = 0;
            xprecord.Medic = 0;
            xprecord.Tamer = 0;
            // Reset Skill Spents Points
            xprecord.WoodCutterP = 0;
            xprecord.SmithyP = 0;
            xprecord.MinerP = 0;
            xprecord.ForagerP = 0;
            xprecord.HunterP = 0;
            xprecord.FisherP = 0;
            xprecord.CrafterP = 0;
            xprecord.FramerP = 0;
            xprecord.MedicP = 0;
            xprecord.TamerP = 0;
            // Check/Reset Tamer permissions
            PetChecks(player, true);
            // New Reset Timer
            xprecord.resettimerskills = DateTime.Now;
            // Message Player with number of skill points returned
            player.ChatMessage(XPLang("resetskills", player.UserIDString, skillpoints));
            // Update Live UI
            LiveStats(player, true);
        }

        private void PlayerFixData(BasePlayer player)
        {
            if (player == null) return;
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord == null) return;
            int timer = 0;
            DateTime resettimedata = xprecord.playerfixdata.AddMinutes(config.defaultOptions.playerfixdatatimer);
            TimeSpan interval = resettimedata - DateTime.Now;
            timer = (int)interval.TotalMinutes;
            if (timer > 0)
            {
                player.ChatMessage(XPLang("resettimerdata", player.UserIDString, timer));
                return;
            }
            // Reset Level, Required XP & Stat/Skill Points
            xprecord.level = 0;
            xprecord.requiredxp = 25;
            xprecord.statpoint = 0;
            xprecord.skillpoint = 0;
            // Reset max health if needed before removing points
            if (player._maxHealth > 100 || player._health > 100)
            {
                // Get Stats
                double armor = (xprecord.Might * config.might.armor) * 100;
                double currentmaxhealth = player._maxHealth;
                double currenthealth = Mathf.Ceil(player._health);
                // Remove Armor
                double newmaxhealth = currentmaxhealth - armor;
                double newhealth = currenthealth - armor;
                // Change Health
                if (newmaxhealth < 100)
                {
                    player._maxHealth = 100;
                    player._health = (float)newhealth;
                }
                else
                {
                    player._maxHealth = (float)newmaxhealth;
                    player._health = (float)newhealth;
                }
                //player._health = 100;
            }
            // Reset Stat Levels
            xprecord.Mentality = 0;
            xprecord.Dexterity = 0;
            xprecord.Might = 0;
            xprecord.Chemist = 0;
            // Reset Stat Spent Points
            xprecord.MentalityP = 0;
            xprecord.DexterityP = 0;
            xprecord.MightP = 0;
            xprecord.ChemistP = 0;
            // Reset Skill Levels
            xprecord.WoodCutter = 0;
            xprecord.Smithy = 0;
            xprecord.Miner = 0;
            xprecord.Forager = 0;
            xprecord.Hunter = 0;
            xprecord.Fisher = 0;
            xprecord.Crafter = 0;
            xprecord.Framer = 0;
            xprecord.Medic = 0;
            xprecord.Tamer = 0;
            // Reset Skill Spents Points
            xprecord.WoodCutterP = 0;
            xprecord.SmithyP = 0;
            xprecord.MinerP = 0;
            xprecord.ForagerP = 0;
            xprecord.HunterP = 0;
            xprecord.FisherP = 0;
            xprecord.CrafterP = 0;
            xprecord.FramerP = 0;
            xprecord.MedicP = 0;
            xprecord.TamerP = 0;
            // Reset calories/hydration if needed
            if (player.metabolism.calories.max > 500)
            {
                player.metabolism.calories.max = 500;
            }
            if (player.metabolism.hydration.max > 250)
            {
                player.metabolism.hydration.max = 250;
            }
            // Check/Reset Tamer permissions
            PetChecks(player, true);
            // Set LiveUI Location to Default
            xprecord.UILocation = config.defaultOptions.liveuistatslocation;
            // Timer
            xprecord.playerfixdata = DateTime.Now;
            // Run Level Up to Recalculate Players Data
            LvlUp(player, 0, 0);
            // Update Live UI
            LiveStats(player, true);
            // Notify Players
            player.ChatMessage(XPLang("playerfixdata", player.UserIDString));
        }

        private bool IsNight()
        {
            var dateTime = TOD_Sky.Instance.Cycle.DateTime;
            return dateTime.Hour >= config.nightBonus.StartTime || dateTime.Hour <= config.nightBonus.EndTime;
        }

        private void OnPlayerHealthChange(BasePlayer player, float oldValue, float newValue)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            LiveStats(player, true);
        }

        private void KRBonus(BasePlayer player, string KillType, int reqkills, double bonus, int bonusend, bool enablemultibonus, object multibonustype)
        {
            var playerid = player.userID.ToString();
            XPRecord xprecord = GetXPRecord(player);
            int KillAmount = reqkills;
            int BonusEnd = bonusend;
            int GetKillRecord = KillRecords.Call<int>("GetKillRecord", playerid, KillType.ToLower());
            if (GetKillRecord == KillAmount)
            {
                GainExp(player, bonus);
                player.ChatMessage(XPLang("bonus", player.UserIDString, bonus, KillAmount, KillType));
            }
            else
            {
                if (enablemultibonus)
                {
                    int MultipleKA = KillAmount;
                    double Multibonus = bonus;
                    for (int k = 0; k < BonusEnd; ++k)
                    {
                        MultipleKA += reqkills + k / BonusEnd;
                        if (multibonustype == "increase")
                        {
                            Multibonus += bonus + k / BonusEnd;
                        }
                        if (MultipleKA >= BonusEnd) return;
                        if (GetKillRecord == MultipleKA)
                        {
                            GainExp(player, Multibonus);
                            player.ChatMessage(XPLang("bonus", player.UserIDString, Multibonus, MultipleKA, KillType));
                        }
                    }
                }
            }
        }

        private void XPClans(BasePlayer player, double e, string type)
        {
            foreach (var allplayer in BasePlayer.activePlayerList)
            {
                bool isinclan = Clans.Call<bool>("IsClanMember", player.UserIDString, allplayer.UserIDString);
                if (isinclan && (allplayer != player))
                {
                    Puts($"{allplayer.UserIDString} | {isinclan}");
                    XPRecord xprecord = GetXPRecord(allplayer);
                    if (type == "addxp")
                    {
                        double addxp = config.xpclans.clanbonusamount * e;
                        if (addxp < 1)
                        {
                            addxp = 1;
                        }
                        xprecord.experience = (int)xprecord.experience + addxp;
                        if (xprecord.experience >= xprecord.requiredxp)
                        {
                            LvlUp(allplayer, 0, 0);
                        }
                        LiveStats(allplayer, true);
                        Puts($"Player:{allplayer.UserIDString} | +XP:{addxp}");
                        if (UINotify != null && config.UiNotifier.useuinotify && config.UiNotifier.xpgainloss && addxp != 0)
                        {
                            UINotify.Call("SendNotify", allplayer, config.UiNotifier.xpgainlosstype, XPLang("uinotify_xpgain", allplayer.UserIDString, Math.Round(addxp)));
                        }
                    }
                    if (type == "takexp")
                    {
                        if (e < 1)
                        {
                            e = 1;
                        }
                        double takexp = config.xpclans.clanreductionamount * e;
                        if (takexp < 1)
                        {
                            takexp = 1;
                        }
                        double newxp = xprecord.experience - takexp;
                        double nextlevel = xprecord.requiredxp;
                        // Make sure XP does not go negative
                        if (newxp <= 0)
                        {
                            newxp = 0;
                        }
                        xprecord.experience = (int)newxp;
                        if (nextlevel == config.xpLevel.levelstart) return;
                        double prevlevel = xprecord.requiredxp - (xprecord.level * config.xpLevel.levelmultiplier);
                        if (xprecord.experience < prevlevel)
                        {
                            LvlDown(allplayer, 0, 0);
                        }
                        LiveStats(allplayer, true);
                        Puts($"Player:{allplayer.UserIDString} | -XP:{takexp}");
                        // UINotify
                        if (UINotify != null && config.UiNotifier.useuinotify && config.UiNotifier.xpgainloss)
                        {
                            UINotify.Call("SendNotify", allplayer, config.UiNotifier.xpgainlosstype, XPLang("uinotify_xploss", allplayer.UserIDString, Math.Round(takexp)));
                        }
                    }
                }
            }      
        }

        #endregion

        #region Pets

        public const string Tame = "cannpc";
        public const string TameChicken = "pets.chicken";
        public const string TameBoar = "pets.boar";
        public const string TameStag = "pets.stag";
        public const string TameWolf = "pets.wolf";
        public const string TameBear = "pets.bear";

        private void PetChecks(BasePlayer player, bool reset = false, int leveldown = 0)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            XPRecord xprecord = GetXPRecord(player);
            var skilllevel = xprecord.Tamer;

            if (!config.tamer.enabletame || Pets == null || !Pets.IsLoaded) return;

            if (reset)
            {
                permission.RevokeUserPermission(player.UserIDString, TameChicken);
                permission.RevokeUserPermission(player.UserIDString, TameBoar);
                permission.RevokeUserPermission(player.UserIDString, TameStag);
                permission.RevokeUserPermission(player.UserIDString, TameWolf);
                permission.RevokeUserPermission(player.UserIDString, TameBear);
                return;
            }

            if (skilllevel >= config.tamer.chickenlevel && config.tamer.tamechicken && !permission.UserHasPermission(player.UserIDString, TameChicken))
            {
                permission.GrantUserPermission(player.UserIDString, TameChicken, Pets);
            }
            if (leveldown < config.tamer.chickenlevel)
            {
                permission.RevokeUserPermission(player.UserIDString, TameChicken);
            }
            if (skilllevel >= config.tamer.boarlevel && config.tamer.tameboar && !permission.UserHasPermission(player.UserIDString, TameBoar))
            {
                permission.GrantUserPermission(player.UserIDString, TameBoar, Pets);
            }
            if (leveldown < config.tamer.boarlevel)
            {
                permission.RevokeUserPermission(player.UserIDString, TameBoar);
            }
            if (skilllevel >= config.tamer.staglevel && config.tamer.tamestag && !permission.UserHasPermission(player.UserIDString, TameStag))
            {
                permission.GrantUserPermission(player.UserIDString, TameStag, Pets);
            }
            if (leveldown < config.tamer.staglevel)
            {
                permission.RevokeUserPermission(player.UserIDString, TameStag);
            }
            if (skilllevel >= config.tamer.wolflevel && config.tamer.tamewolf && !permission.UserHasPermission(player.UserIDString, TameWolf))
            {
                permission.GrantUserPermission(player.UserIDString, TameWolf, Pets);
            }
            if (leveldown < config.tamer.wolflevel)
            {
                permission.RevokeUserPermission(player.UserIDString, TameWolf);
            }
            if (skilllevel >= config.tamer.bearlevel && config.tamer.tamebear && !permission.UserHasPermission(player.UserIDString, TameBear))
            {
                permission.GrantUserPermission(player.UserIDString, TameBear, Pets);
            }
            if (leveldown < config.tamer.bearlevel)
            {
                permission.RevokeUserPermission(player.UserIDString, TameBear);
            }

        }

        #endregion

        #region Kills/Deaths/Loot

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            // Check for null
            if (entity == null || hitInfo == null || hitInfo.Initiator == null) return;
            // Count Player Suicide Separately If Enabled
            if (entity == hitInfo.Initiator)
            {
                var suicider = entity as BasePlayer;
                if (suicider == null || !suicider.userID.IsSteamId()) return;
                var r = GetXPRecord(suicider);
                var reducexp = Math.Round(r.experience * config.xpReducer.suicidereduceamount);
                LoseExp(suicider, reducexp);
                suicider.ChatMessage(XPLang("suicide", suicider.UserIDString, reducexp));
                return;
            }
            // Get Killer Info
            var attacker = hitInfo.Initiator as BasePlayer;
            if (attacker == null || !attacker.userID.IsSteamId()) return;
            string KillType = entity?.GetType().Name.ToLower();
            XPRecord xprecord = GetXPRecord(attacker);
            if (xprecord == null) return;
            double addxp = 0;
            // Update DataCache On Kill
            switch (KillType)
            {
                case "chicken":
                    addxp = config.xpGain.chickenxp;
                    break;
                case "boar":
                    addxp = config.xpGain.boarxp;
                    break;
                case "stag":
                    addxp = config.xpGain.stagxp;
                    break;
                case "wolf":
                    addxp = config.xpGain.wolfxp;
                    break;
                case "bear":
                    addxp = config.xpGain.bearxp;
                    break;
                case "simpleshark":
                    addxp = config.xpGain.sharkxp;
                    break;
                case "horse":
                case "ridablehorse":
                    addxp = config.xpGain.horsexp;
                    break;
                case "scientistnpc":
                case "scientist":
                    addxp = config.xpGain.scientistxp;
                    break;
                case "tunneldweller":
                case "underwaterdweller":
                    addxp = config.xpGain.dwellerxp;
                    break;
                case "baseplayer":
                    addxp = config.xpGain.playerxp;
                    break;
                case "lootcontainer":
                    addxp = config.xpGain.lootcontainerxp;
                    break;
                case "basecorpse":
                    addxp = config.xpGain.animalharvestxp;
                    break;
                case "npcplayercorpse":
                    addxp = config.xpGain.corpseharvestxp;
                    break;
                case "bradleyapc":
                    addxp = config.xpGain.bradley;
                    break;
                case "patrolhelicopter":
                    addxp = config.xpGain.patrolhelicopter;
                    break;
            }

            if (KillRecords != null && config.xpBonus.enablebonus)
            {
                KRBonus(attacker, KillType, config.xpBonus.requiredkills, config.xpBonus.bonusxp, config.xpBonus.endbonus, config.xpBonus.multibonus, config.xpBonus.multibonustype);
            }
            GainExp(attacker, addxp);
        }

        private void OnPlayerDeath(BasePlayer victim, HitInfo hitInfo)
        {
            // Check for null or NPC
            if (victim == null || !victim.userID.IsSteamId()) return;

            BaseEntity attacker = hitInfo?.Initiator;
            if (attacker == null) return;

            // If Suicide Ingnore Death
            if (attacker == victim) return;

            // If Attack Type Not Detected Remove Error
            if (victim.lastDamage == null) return;


            // Update Player Data On deaths if enabled
            if(config.xpReducer.deathreduce)
            {            
                XPRecord xprecord = GetXPRecord(victim);
                var reducexp = Math.Round(xprecord.experience * config.xpReducer.deathreduceamount);
                LoseExp(victim, reducexp);
                victim.ChatMessage(XPLang("death", victim.UserIDString, reducexp));
            }

        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || !player.userID.IsSteamId() || !entity.IsValid()) return;
            XPRecord xprecord = GetXPRecord(player);
            var loot = entity.GetType().Name.ToLower();
            var lootid = entity.net.ID;
            double addxp = 0;

            if (_lootCache.ContainsKey(lootid) && _lootCache[lootid].id.Contains(player.UserIDString))
            {
                return;
            }
            AddLootData(player, entity);

            if (loot == "lootcontainer")
            {
                addxp = config.xpGain.lootcontainerxp;
            }
            else if (loot == "freeablelootcontainer")
            {
                addxp = config.xpGain.underwaterlootcontainerxp;
            }
            else if (loot == "lockedbyentcrate")
            {
                addxp = config.xpGain.lockedcratexp;
            }
            else if (loot == "hackablelockedcrate")
            {
                addxp = config.xpGain.hackablecratexp;
            }

            GainExp(player, addxp);
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null || hitInfo.Initiator == null) return;
            if (entity.GetType().Name.ToLower().Contains("corpse")) return;
            // Ignore if suicide
            if (entity == hitInfo.Initiator) return;
            double blockamount = 0.0;
            bool didblock = false;
            bool diddodge = false;
            var player = entity as BasePlayer;
            if (player != null && player.userID.IsSteamId())
            {
                XPRecord vxprecord = GetXPRecord(player);
                if (vxprecord != null)
                {
                    // If UI open then close
                    DestroyUi(player, XPeriencePlayerControlPrimary);
                    DestroyUi(player, XPerienceAdminPanelMain);
                    // Dexterity Armor Reduction
                    if (vxprecord.Dexterity > 0 && player._health > 100)
                    {
                        double defaultdmg = hitInfo.damageTypes.Total();
                        double armordmgreduction = (vxprecord.Dexterity * config.dexterity.reducearmordmg);
                        hitInfo.damageTypes?.ScaleAll(1 - (float)armordmgreduction);
                        double damgdiff = defaultdmg - hitInfo.damageTypes.Total();
                        if (config.defaultOptions.disablearmorchat)
                        {
                            player.ChatMessage(XPLang("victimarmordmg", player.UserIDString, damgdiff));
                        }
                    }
                    // Random chance to Block or Dodge
                    double dodgechance = (vxprecord.Dexterity * config.dexterity.dodgechance) * 100;
                    double blockchance = (vxprecord.Dexterity * config.dexterity.blockchance) * 100;
                    int fifty = RandomNumber.Between(0, 100);
                    int roll = RandomNumber.Between(0, 110);
                    // Block
                    if (fifty < 50)
                    {
                        if (vxprecord.Dexterity > 0 && config.dexterity.blockchance != 0 && roll <= blockchance)
                        {
                            didblock = true;
                            double blockdmg = (vxprecord.Dexterity * config.dexterity.blockamount);
                            blockamount = hitInfo.damageTypes.Total() * blockdmg;
                            hitInfo.damageTypes?.ScaleAll(1 - (float)blockdmg);
                            // UINotify
                            if (UINotify != null && config.UiNotifier.useuinotify && config.UiNotifier.dodgeblock)
                            {
                                UINotify.Call("SendNotify", player, config.UiNotifier.dodgeblocktype, XPLang("victimblock", player.UserIDString, Math.Round(blockamount)));
                            }
                            // Disable Chats
                            if (!config.UiNotifier.disablechats)
                            {
                                player.ChatMessage(XPLang("victimblock", player.UserIDString, Math.Round(blockamount)));
                            }
                        }
                    }
                    // Dodge
                    if (fifty > 50)
                    {
                        if (vxprecord.Dexterity > 0 && config.dexterity.dodgechance != 0 && roll <= dodgechance)
                        {
                            diddodge = true;
                            hitInfo.damageTypes?.ScaleAll(0);
                            // UINotify
                            if (UINotify != null && config.UiNotifier.useuinotify && config.UiNotifier.dodgeblock)
                            {
                                UINotify.Call("SendNotify", player, config.UiNotifier.dodgeblocktype, XPLang("victimdodge", player.UserIDString));
                            }
                            // Disable Chats
                            if (!config.UiNotifier.disablechats)
                            {
                                player.ChatMessage(XPLang("victimdodge", player.UserIDString));
                            }
                        }
                    }

                }
            }

            var attacker = hitInfo.Initiator as BasePlayer;
            if (attacker == null || !attacker.userID.IsSteamId()) return;
            var KillType = entity?.GetType().Name.ToLower();
            XPRecord xprecord = GetXPRecord(attacker);
            if (xprecord == null) return;

            if (diddodge && attacker)
            {
                // UINotify
                if (UINotify != null && config.UiNotifier.useuinotify && config.UiNotifier.dodgeblock)
                {
                    UINotify.Call("SendNotify", attacker, config.UiNotifier.dodgeblocktype, XPLang("attackerdodge", attacker.UserIDString));
                }
                // Disable Chats
                if (!config.UiNotifier.disablechats)
                {
                    attacker.ChatMessage(XPLang("attackerdodge", attacker.UserIDString));
                }
                return;
            }

            if (didblock && attacker)
            {
                // UINotify
                if (UINotify != null && config.UiNotifier.useuinotify && config.UiNotifier.dodgeblock)
                {
                    UINotify.Call("SendNotify", attacker, config.UiNotifier.dodgeblocktype, XPLang("attackerblock", attacker.UserIDString, Math.Round(blockamount)));
                }
                // Disable Chats
                if (!config.UiNotifier.disablechats)
                {
                    attacker.ChatMessage(XPLang("attackerblock", attacker.UserIDString, Math.Round(blockamount)));
                }
                return;
            }

            // Hunter Wildlife Increase
            if (KillType == "chicken" || KillType == "boar" || KillType == "stag" || KillType == "wolf" || KillType == "bear" || KillType == "horse" || KillType == "simpleshark")
            {
                double hunterdmg = xprecord.Hunter * config.hunter.damageincrease;
                hitInfo.damageTypes?.ScaleAll(1 + (float)hunterdmg);
            }
            // Hunter Night Wildlife Increase
            if (IsNight() && config.nightBonus.enableskillboosts)
            {
                double nightdmg = xprecord.Hunter * config.hunter.nightdmgincrease;
                hitInfo.damageTypes?.ScaleAll(1 + (float)nightdmg);
            }
            // Mentality Critical Chance
            if (xprecord.Mentality >= 1)
            {
                double critchance = (xprecord.Mentality * config.mentality.criticalchance) * 100;
                if (RandomNumber.Between(0, 100) <= critchance)
                {
                    hitInfo.damageTypes?.ScaleAll(1 + 0.10f);
                    double crithit = Math.Round((int)hitInfo.damageTypes.Total() * 0.10f);
                    // UINotify
                    if (UINotify != null && config.UiNotifier.useuinotify && config.UiNotifier.criticalhit)
                    {
                        UINotify.Call("SendNotify", attacker, config.UiNotifier.criticalhittype, XPLang("crithit", attacker.UserIDString, Math.Round(crithit)));
                    }
                    // Disable Chats
                    if (!config.UiNotifier.disablechats)
                    {
                        attacker.ChatMessage(XPLang("crithit", attacker.UserIDString, Math.Round(crithit)));
                    }
                }
            }
            // Might Melee Increase
            if (xprecord.Might > 0 && hitInfo?.Weapon != null)
            {
                if (hitInfo.Weapon.ShortPrefabName.Contains("knife") ||
                    hitInfo.Weapon.ShortPrefabName.Contains("hatchet") ||
                    hitInfo.Weapon.ShortPrefabName.Contains("club") ||
                    hitInfo.Weapon.ShortPrefabName.Contains("mace") ||
                    hitInfo.Weapon.ShortPrefabName.Contains("pickaxe") ||
                    hitInfo.Weapon.ShortPrefabName.Contains("machete"))
                {
                    double meleeincrease = (xprecord.Might * config.might.meleedmg);
                    hitInfo.damageTypes?.ScaleAll(1 + (float)meleeincrease);
                }
            }
        }

        #endregion

        #region Missions

        private void OnMissionSucceeded(BaseMission mission, BaseMission.MissionInstance missionInstance, BasePlayer assignee)
        {
            // Check for null or NPC
            if (assignee == null || !assignee.userID.IsSteamId()) return;
            if (mission == null) return;
            // Get Mission Player
            XPRecord xprecord = GetXPRecord(assignee);
            // Give XP
            double addxp = config.xpMissions.missionsucceededxp;
            GainExp(assignee, addxp);
        }

        private void OnMissionFailed(BaseMission mission, BaseMission.MissionInstance missionInstance, BasePlayer assignee)
        {
            // Check for null or NPC
            if (assignee == null || !assignee.userID.IsSteamId()) return;
            if (mission == null) return;
            // Get Mission Player
            XPRecord xprecord = GetXPRecord(assignee);
            // Take XP
            double reducexp = config.xpMissions.missionfailedxp;
            LoseExp(assignee, reducexp);
        }

        #endregion

        #region Crafting/Building

        public void CollectIngredient(int item, int amount, List<Item> collect, ItemCrafter itemCrafter, BasePlayer player)
        {
            XPRecord xprecord = GetXPRecord(player);
            int skilllevel = xprecord.Crafter;
            double craftcost = (config.crafter.craftcost * skilllevel) * amount;
            double newamount = Math.Round(amount - craftcost);
            if (((config.crafter.craftcost * skilllevel) * 100) > 45 && (item == -1938052175 || item == -1581843485))
            {
                craftcost = 0.45 * amount;
                newamount = Math.Round(amount - craftcost);
            }
            if (newamount < 1)
            {
                newamount = 1;
            }
            foreach (ItemContainer container in itemCrafter.containers)
            {
                amount -= container.Take(collect, item, (int)newamount);
                if (amount < 1)
                {
                    amount = 1;
                }
            }
        }

        bool? OnIngredientsCollect(ItemCrafter itemCrafter, ItemBlueprint blueprint, ItemCraftTask task, int amount, BasePlayer player)
        {
            //if (amount == 0 || amount == null) return;
            List<Item> collect = new List<Item>();
            foreach (ItemAmount ingredient in blueprint.ingredients)
                CollectIngredient(ingredient.itemid, (int)ingredient.amount * amount, collect, itemCrafter, player);
            task.potentialOwners = new List<ulong>();
            foreach (Item obj in collect)
            {
                obj.CollectedForCrafting(player);
                if (!task.potentialOwners.Contains(player.userID))
                    task.potentialOwners.Add(player.userID);
            }
            task.takenItems = collect;
            return true;
        }

        private void OnItemCraft(ItemCraftTask task, BasePlayer player, Item item)
        {
            if (task.cancelled) return;
            // Ignore keys
            if (task.blueprint.targetItem.shortname.Contains("key") || task.blueprint.name.Contains("(Clone)")) return;
            XPRecord xprecord = GetXPRecord(player);
            int skilllevel = xprecord.Crafter;
            if (skilllevel <= 0) return;
            var craftTime = task.blueprint.time;
            var itemlevel = task.blueprint.workbenchLevelRequired;
            float workbenchinuse = player.currentCraftLevel;
            // Items with no workbench requirement or same as workbench level
            if (itemlevel == workbenchinuse || workbenchinuse == 0)
            {
                double craftspeed = (config.crafter.craftspeed * skilllevel) * task.blueprint.time;
                craftTime = task.blueprint.time - (float)craftspeed;
            }
            // Items with no workbench requirement using level 1,2,3 workbench
            if (workbenchinuse == 1 && itemlevel == 0)
            {
                double craftspeed = (config.crafter.craftspeed * skilllevel) * (task.blueprint.time * 0.5);
                craftTime = task.blueprint.time - (float)craftspeed;
            }
            else if ((workbenchinuse == 2 || workbenchinuse == 3) && itemlevel == 0)
            {
                double craftspeed = (config.crafter.craftspeed * skilllevel) * (task.blueprint.time * 0.75);
                craftTime = task.blueprint.time - (float)craftspeed;
            }
            // Items with workbench requirement level 1
            if (workbenchinuse == 2 && itemlevel == 1)
            {
                double craftspeed = (config.crafter.craftspeed * skilllevel) * (task.blueprint.time * 0.5);
                craftTime = task.blueprint.time - (float)craftspeed;
            }
            else if (workbenchinuse == 3 && itemlevel == 1)
            {
                double craftspeed = (config.crafter.craftspeed * skilllevel) * (task.blueprint.time * 0.75);
                craftTime = task.blueprint.time - (float)craftspeed;
            }
            // Items with workbench requirement level 2
            if (workbenchinuse == 3 && itemlevel == 2)
            {
                double craftspeed = (config.crafter.craftspeed * skilllevel) * (task.blueprint.time * 0.5);
                craftTime = task.blueprint.time - (float)craftspeed;
            }
            task.blueprint = UnityEngine.Object.Instantiate(task.blueprint);
            task.blueprint.time = craftTime;
            return;
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            if (task == null || item == null) return;
            var player = task.owner.ToPlayer();
            if (player == null) return;
            XPRecord xprecord = GetXPRecord(player);

            GainExp(player, config.xpGain.craftingxp);

            int skilllevel = xprecord.Crafter;
            if (skilllevel <= 0) return;

            double conditionchance = (config.crafter.conditionchance * skilllevel) * 100;
            float tenpercent = (float)(item._maxCondition / (config.crafter.conditionamount * 100));  // Adding max condition on top of whatever the total condition level is for the item.

            if (Random.Range(0, 100) <= conditionchance)
            {
                if (item.GetHeldEntity() is BaseProjectile)
                {
                    BaseProjectile projectile = item?.GetHeldEntity() as BaseProjectile;
                    if (projectile == null) return;

                    item._maxCondition = item._maxCondition + tenpercent;
                    item.condition = item._condition + tenpercent;
                    projectile.SendNetworkUpdateImmediate();
                    player.ChatMessage(XPLang("weaponcon", player.UserIDString, item.condition));
                }
                else
                {
                    item._maxCondition = item._maxCondition + tenpercent;
                    item.condition = item._condition + tenpercent;
                    item.GetHeldEntity()?.SendNetworkUpdateImmediate();
                }
            }
        }

        private object OnItemUse(Item item, int amount)
        {
            if (item?.info.shortname != "lowgradefuel") return null;
            var shortName = item.parent?.parent?.info.shortname;
            if (shortName != "hat.candle" && shortName != "hat.miner") return null;
            var player = item.GetRootContainer()?.GetOwnerPlayer();
            XPRecord xprecord = GetXPRecord(player);
            var skilllevel = xprecord.Miner;
            double lessfueltotal = (config.miner.fuelconsumption * skilllevel) * 100;
            if (Random.Range(0, 110) <= lessfueltotal)
            {
                return 0;
            }
            return null;
        }

        private List<ItemAmount> RepairItems(BasePlayer player, Item item)
        {
            ItemDefinition info = item.info;
            ItemBlueprint component = info.GetComponent<ItemBlueprint>();
            List<ItemAmount> list = Facepunch.Pool.GetList<ItemAmount>();
            RepairBench.GetRepairCostList(component, list);
            return ApplyItemCostReduction(player, list, item);
        }

        private List<ItemAmount> ApplyItemCostReduction(BasePlayer player, List<ItemAmount> list, Item item)
        {
            List<ItemAmount> reducedlist = new List<ItemAmount>();
            var repairCostreduction = RepairBench.RepairCostFraction(item);
            XPRecord xprecord = GetXPRecord(player);
            int skilllevel = xprecord.Crafter;
            if (skilllevel <= 0) return null;
            foreach (ItemAmount itemAmount in list)
            {
                if (itemAmount.itemDef.category != ItemCategory.Component)
                {
                    itemAmount.amount = Mathf.CeilToInt(itemAmount.amount * repairCostreduction);
                    itemAmount.amount = Mathf.Round((float) (itemAmount.amount - (config.crafter.repaircost * xprecord.Crafter) * itemAmount.amount));
                    
                    if (itemAmount.amount < 1)
                    {
                        itemAmount.amount = 1;
                    }
                    reducedlist.Add(itemAmount);
                }
            }
            return reducedlist;
        }

        bool PlayerCanRepair(BasePlayer player, List<ItemAmount> list)
        {
            foreach (ItemAmount itemAmount in list)
            {
                int amount = player.inventory.GetAmount(itemAmount.itemDef.itemid);
                if (itemAmount.amount > amount)
                {
                    return false;
                }
            }
            return true;
        }

        private object OnItemRepair(BasePlayer player, Item item)
        {
            if (player == null || item == null) return null;
            XPRecord xprecord = GetXPRecord(player);
            int skilllevel = xprecord.Crafter;
            if (skilllevel <= 0) return null;
            double repairincrease = (config.crafter.repairincrease * skilllevel) * 100;
            var list = RepairItems(player, item);
            if (!PlayerCanRepair(player, list))
            {
                return false;
            }
            if (Random.Range(0, 100) <= repairincrease)
            {
                if (item.GetHeldEntity() is BaseProjectile)
                {
                    BaseProjectile projectile = item?.GetHeldEntity() as BaseProjectile;
                    if (projectile == null) return null;

                    item._maxCondition = item._maxCondition;
                    item.condition = item._maxCondition;
                    projectile.SendNetworkUpdateImmediate();
                }
                else
                {
                    item._maxCondition = item._maxCondition;
                    item.condition = item._maxCondition;
                    item.GetHeldEntity()?.SendNetworkUpdateImmediate();
                }
                
            }
            foreach (ItemAmount itemAmount in list)
            {
                player.inventory.Take((List<Item>)null, itemAmount.itemid, (int)itemAmount.amount);
            }
            return true;
        }

        private void OnEntityBuilt(Planner plan, GameObject gameObject)
        {
            var player = plan.GetOwnerPlayer();
            if (player == null) return;
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord == null) return;
            double addxp = config.xpBuilding.woodstructure;
            GainExp(player, addxp);
        }

        private void OnStructureUpgrade(BuildingBlock buildingBlock, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (buildingBlock == null || player == null) return;
            BuildingPrivlidge isauth = player.GetBuildingPrivilege();
            if (isauth == null || !isauth.IsAuthed(player)) return;
            XPRecord xprecord = GetXPRecord(player);
            double addxp = 0;
            if (buildingBlock.grade == BuildingGrade.Enum.Twigs)
            {
                addxp = config.xpBuilding.woodstructure;
            }
            if (buildingBlock.grade == BuildingGrade.Enum.Wood)
            {
                addxp = config.xpBuilding.stonestructure;
            }
            if (buildingBlock.grade == BuildingGrade.Enum.Stone)
            {
                addxp = config.xpBuilding.metalstructure;
            }
            if (buildingBlock.grade == BuildingGrade.Enum.Metal)
            {
                addxp = config.xpBuilding.armoredstructure;
            }
            if (buildingBlock.grade == BuildingGrade.Enum.TopTier)
            {
                addxp = config.xpBuilding.armoredstructure;
            }

            if (CanAffordUpgrade(buildingBlock, player, grade))
            {
                GainExp(player, addxp);
                RefundMaterials(buildingBlock, player, grade);
            }  
        }

        public bool CanAffordUpgrade(BuildingBlock buildingBlock, BasePlayer player, BuildingGrade.Enum grade)
        {
            object building = Interface.CallHook("CanAffordUpgrade", player, buildingBlock, grade);
            if (building is bool)
            {
                return (bool)building;
            }

            bool canupgrade = true;
            foreach (var item in buildingBlock.blockDefinition.grades[(int)grade].costToBuild)
            {
                var missingAmount = item.amount - player.inventory.GetAmount(item.itemid);
                if (missingAmount > 0f)
                {
                    canupgrade = false;
                }
            }
            return canupgrade;
        }

        private void RefundMaterials(BuildingBlock buildingBlock, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (buildingBlock == null || player == null) return;
            if (buildingBlock.OwnerID != player.userID) return;
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord.Framer == 0) return;
            var items = buildingBlock.blockDefinition.grades[(int)grade].costToBuild;
            foreach (var item in items)
            {
                double reducedcost = item.amount * (config.framer.upgradecost * xprecord.Framer);
                if (reducedcost < 1)
                {
                    reducedcost = 1;
                }
                player.GiveItem(ItemManager.CreateByItemID(item.itemid, (int)reducedcost));
            }
        }

        private void OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            if (entity == null || player == null) return;
            BuildingPrivlidge isauth = player.GetBuildingPrivilege();
            if (isauth == null || !isauth.IsAuthed(player)) return;
            XPRecord xprecord = GetXPRecord(player);
            int skilllevel = xprecord.Framer;
            if (skilllevel == 0) return;
            double repairtime = config.framer.repairtime;
            double repaircost = config.framer.repaircost;

            // Reduce Repair Time
            entity.lastAttackedTime = (float)(entity.lastAttackedTime - (repairtime * skilllevel));
            if (entity.SecondsSinceAttacked < 30) return;

            // Reduce Repair Cost
            float missingHealth = entity.MaxHealth() - entity.health;
            float healthPercentage = missingHealth / entity.MaxHealth();
            if (missingHealth <= 0f || healthPercentage <= 0f)
            {
                entity.OnRepairFailed(null, string.Empty);
                return;
            }

            List<ItemAmount> itemAmounts = entity.RepairCost(healthPercentage);
            if (itemAmounts.Sum(x => x.amount) <= 0f)
            {
                entity.health += missingHealth;
                entity.SendNetworkUpdate();
                entity.OnRepairFinished();
                return;
            }
            
            foreach (ItemAmount amount in itemAmounts)
            { 
                if (amount.amount > 40f) 
                {
                    amount.amount = 40f;
                }
                amount.amount = (float)(amount.amount * (repaircost * skilllevel));
            }

            if (itemAmounts.Any(ia => player.inventory.GetAmount(ia.itemid) < (int)ia.amount))
            {
                entity.OnRepairFailed(null, string.Empty);
                return;
            }

            foreach (ItemAmount amount in itemAmounts)
            {
                if (amount.amount < 1) return;
                player.GiveItem(ItemManager?.CreateByItemID(amount.itemid, (int)amount.amount));
            }
        }

        private void OnFuelConsume(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (oven == null || fuel == null) return;

            var player = FindPlayer(oven.OwnerID.ToString());
            if (player == null) return;

            if (player.UserIDString == oven.OwnerID.ToString())
            {
                XPRecord xprecord = GetXPRecord(player);
                var skilllevel = xprecord.Smithy;
                double lessfueltotal = (config.smithy.fuelconsumption * skilllevel) * 100;
                if (Random.Range(0, 100) < lessfueltotal)
                {
                    fuel.amount += 1;
                    return;
                }
            }

            XPRecord rec = GetXPRecord(player);
            double increasechance = (config.smithy.productionrate * rec.Smithy) * 100;
            var items = oven.inventory.itemList.ToArray();
            foreach (var item in items)
            {
                if (Random.Range(0, 100) < increasechance)
                {
                    double increaseamount = Math.Round((config.smithy.productionrate * rec.Smithy) * 5);
                    var itemModCookable = item.info.GetComponent<ItemModCookable>();
                    if (itemModCookable?.becomeOnCooked == null || item.temperature < itemModCookable.lowTemp || item.temperature > itemModCookable.highTemp || itemModCookable.cookTime < 0) continue;
                    if (oven.inventory.Take(null, item.info.itemid, 1) != 1) continue;
                    var itemToGive = ItemManager.Create(itemModCookable.becomeOnCooked, (1 + (int)increaseamount));
                    if (!itemToGive.MoveToContainer(oven.inventory))
                        itemToGive.Drop(oven.inventory.dropPosition, oven.inventory.dropVelocity);
                }
            }

        }

        #endregion

        #region Plants/Trees/Ores/Food

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity.ToPlayer();
            if (player == null || !player.userID.IsSteamId() || dispenser == null || entity == null || item == null) return;
            XPRecord xprecord = GetXPRecord(player);
            var gatherType = dispenser.gatherType;
            double addxp = 0;
            int skilllevel = 0;
            double gatherincrease = 0;
            double increaseamount = 0;
            var tool = player.GetActiveItem().ToString().ToLower();
            if (gatherType == ResourceDispenser.GatherType.Tree)
            {
                addxp = config.xpGather.treexp;
                if (config.xpGather.noxptools && tool.Contains("chainsaw"))
                {
                    addxp = 0;
                }
                gatherincrease = config.woodcutter.gatherrate;
                skilllevel = xprecord.WoodCutter;
                double chance = (config.woodcutter.applechance * skilllevel) * 100;
                if ((Random.Range(0, 100) <= chance) == true)
                {
                    var roll = Random.Range(1, 10);
                    if (roll < 5)
                    {
                        // Bad
                        player.Command("note.inv", 352130972, 1.ToString());
                        ItemManager.CreateByName("apple.spoiled", 1)?.DropAndTossUpwards(entity.GetDropPosition());
                        player.RunEffect("assets/bundled/prefabs/fx/notice/loot.drag.itemdrop.fx.prefab");
                    }
                    if (roll > 5)
                    {
                        // good
                        player.Command("note.inv", 1548091822, 1.ToString());
                        ItemManager.CreateByName("apple", 1)?.DropAndTossUpwards(entity.GetDropPosition());
                        player.RunEffect("assets/bundled/prefabs/fx/notice/loot.drag.itemdrop.fx.prefab");
                    }
                }
            }
            else if (gatherType == ResourceDispenser.GatherType.Ore)
            {
                addxp = config.xpGather.orexp;

                if (config.xpGather.noxptools && tool.Contains("jackhammer"))
                {
                    addxp = 0;
                }
                gatherincrease = config.miner.gatherrate;
                skilllevel = xprecord.Miner;
            }
            else if (gatherType == ResourceDispenser.GatherType.Flesh || item.info.shortname == "cactusflesh")
            {
                addxp = config.xpGather.harvestxp;
                gatherincrease = config.hunter.gatherrate;
                skilllevel = xprecord.Hunter;
            }
            //double results = item.amount * (gatherincrease * skilllevel);
            double results = item.amount + (item.amount * (gatherincrease * skilllevel));
            if (skilllevel >= 1)
            {
                item.amount = (int)results;
            }

            GainExp(player, addxp);
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null || !player.userID.IsSteamId() || dispenser == null || item == null) return;
            XPRecord xprecord = GetXPRecord(player);
            var gatherType = dispenser.gatherType;
            double addxp = 0;
            int skilllevel = 0;
            double bonus = 0;
            double increaseamount = 0;

            if (gatherType == ResourceDispenser.GatherType.Tree)
            {
                addxp = config.xpGather.treexp;
                bonus = config.woodcutter.bonusincrease;
                skilllevel = xprecord.WoodCutter;
            }
            else if (gatherType == ResourceDispenser.GatherType.Ore)
            {
                addxp = config.xpGather.orexp;
                bonus = config.miner.bonusincrease;
                skilllevel = xprecord.Miner;
            }
            else if (gatherType == ResourceDispenser.GatherType.Flesh || item.info.shortname == "cactusflesh")
            {
                addxp = config.xpGather.harvestxp;
                bonus = config.hunter.bonusincrease;
                skilllevel = xprecord.Hunter;
            }
            //increaseamount = item.amount * (bonus * skilllevel);
            increaseamount = item.amount + (item.amount * (bonus * skilllevel));
            if (skilllevel >= 1)
            {
                item.amount = (int)increaseamount;
            }
            GainExp(player, addxp);
        }

        private void OnCollectiblePickup(Item item, BasePlayer player, CollectibleEntity entity)
        {
            if (player == null || !player.userID.IsSteamId() || item == null) return;
            XPRecord xprecord = GetXPRecord(player);
            double addxp = 0;
            double gatherincrease = config.forager.gatherrate;
            int skilllevel = xprecord.Forager;
            var name = item.info.shortname;
            // Seeds
            if (name.StartsWith("seed"))
            {
                double chance = (config.forager.chanceincrease * skilllevel) * 100;
                if ((Random.Range(0, 100) <= chance) == true)
                {
                    double addseeds = (config.forager.chanceincrease * skilllevel) * 10;
                    if (addseeds <= 1)
                    {
                        addseeds = 1;
                    }
                    item.amount = item.amount + (int)addseeds;
                }
            }
            // Random Item
            double itemchance = (config.forager.randomchance * skilllevel) * 100;
            if ((Random.Range(0, 100) <= itemchance) == true)
            {
                int randomroll = Random.Range(1, config.forager.randomChanceList.Count);
                var selected = config.forager.randomChanceList[randomroll];
                ItemDefinition definition = ItemManager.FindItemDefinition(selected.shortname);
                if (definition == null)
                {
                    Puts($"invalid shortname in config for item number {selected}");
                }
                player.Command("note.inv", definition.itemid, selected.amount);
                ItemManager.CreateByName(selected.shortname, selected.amount)?.DropAndTossUpwards(player.GetDropPosition());
                player.RunEffect("assets/bundled/prefabs/fx/notice/loot.drag.itemdrop.fx.prefab");
            }
            // XP / Gather Rate
            if (name.Contains("wood"))
            {
                addxp = config.xpGather.treexp;
            }
            else if (name.Contains("ore") || name.Contains("stone"))
            {
                addxp = config.xpGather.orexp;
            }
            else if (name.Contains("berry") || name == "mushroom" || name == "cloth" || name == "pumpkin" || name == "corn" || name == "potato")
            {
                addxp = config.xpGather.plantxp;
            }
            //double results = item.amount * (gatherincrease * skilllevel);
            double results = item.amount + (item.amount * (gatherincrease * skilllevel));
            if (skilllevel >= 1)
            {
                if (results <= 1.5 && gatherincrease != 0)
                {
                    item.amount = 2;
                }
                else
                {
                    item.amount = (int)results;
                }
            }
            GainExp(player, addxp);
        }

        private void OnGrowableGathered(GrowableEntity growable, Item item, BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId() || growable == null || item == null) return;
            XPRecord xprecord = GetXPRecord(player);
            double addxp = 0;
            double gatherincrease = config.forager.gatherrate;
            int skilllevel = xprecord.Forager;

            var name = item.info.shortname;
            if (name.StartsWith("seed"))
            {
                double chance = (config.forager.chanceincrease * skilllevel) * 100;
                if ((Random.Range(0, 100) <= chance) == true)
                {
                    double addseeds = (config.forager.chanceincrease * skilllevel) * 2;
                    if (addseeds <= 1)
                    {
                        addseeds = 1;
                    }
                    item.amount = item.amount + (int)addseeds;
                }
                return;
            }

            if (name.Contains("wood"))
            {
                addxp = config.xpGather.treexp;
            }
            if (name.Contains("berry") || name.Contains("clone") || name == "mushroom" || name == "cloth" || name == "pumpkin" || name == "corn" || name == "potato")
            {
                addxp = config.xpGather.plantxp;
            }

            //double results = item.amount * (gatherincrease * skilllevel);
            double results = item.amount + (item.amount * (gatherincrease * skilllevel));
            if (skilllevel >= 1)
            {
                if (results <= 1.5 && gatherincrease != 0)
                {
                    item.amount = 2;
                }
                else
                {
                    item.amount = (int)results;
                }
            }

            GainExp(player, addxp);
        }

        private void OnFishCatch(Item fish, BaseFishingRod fishingRod, BasePlayer player)
        {
            if (player == null || fish == null) return;
            XPRecord xprecord = GetXPRecord(player);
            double addxp = config.xpGain.fishxp;
            GainExp(player, addxp);
            if (xprecord.Fisher > 0)
            {
                var fishname = fish.info.shortname;
                if (fishname.Contains("anchovy") || fishname.Contains("catfish") || fishname.Contains("herring") || fishname.Contains("minnow") || fishname.Contains("roughy") || fishname.Contains("salmon") || fishname.Contains("sardine") || fishname.Contains("shark") || fishname.Contains("trout") || fishname.Contains("Perch"))
                {
                    double results = Math.Round(fish.amount + (xprecord.Fisher * config.fisher.fishamountincrease));
                    fish.amount = (int)results;
                }
                else
                {
                    double results = Math.Round(fish.amount + (xprecord.Fisher * config.fisher.itemamountincrease));
                    fish.amount = (int)results;
                }

            }
        }
        
        #endregion

        #region Stat & Skill Hooks/Helpers

        // Mentality

        private Dictionary<Rarity, int> rarityValues = new Dictionary<Rarity, int>
        {
            {Rarity.None, 500},
            {Rarity.Common, 20},
            {Rarity.Uncommon, 75},
            {Rarity.Rare, 125},
            {Rarity.VeryRare, 500},
        };

        private object OnResearchCostDetermine(Item item, ResearchTable researchTable)
        {
            XPRecord xprecord = GetXPRecord(researchTable.user);
            if (xprecord.Mentality == 0) return null;
            int rarityvalue = rarityValues[item.info.rarity];
            double reducecost = (config.mentality.researchcost * xprecord.Mentality) * rarityvalue;
            double researchcost = rarityvalue - reducecost;
            return (int)researchcost;
        }

        [HookMethod("OnResearchCost")]
        private int OnResearchCost(int rarityvalue, BasePlayer player)
        {
            if (player == null)
            {
                return rarityvalue;
            }
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord.Mentality == 0) return rarityvalue;
            double reducecost = (config.mentality.researchcost * xprecord.Mentality) * rarityvalue;
            double researchcost = rarityvalue - reducecost;
            return (int)researchcost;
        }

        [HookMethod("OnItemResearchReduction")]
        private float OnItemResearchReduction(float value, BasePlayer player)
        {
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord == null || xprecord.Mentality == 0) return value;
            double researchspeed = (config.mentality.researchspeed * xprecord.Mentality) * value;
            return value - (float)researchspeed;
        }

        private bool CheckUnlockPath(BasePlayer player, TechTreeData.NodeInstance node, TechTreeData techTree)
        {
            if (node.inputs.Count == 0) return true;
            var unlockPath = false;

            foreach (int nodeId in node.inputs)
            {
                var selectNode = techTree.GetByID(nodeId);
                if (selectNode.itemDef == null) return true;

                if (!techTree.HasPlayerUnlocked(player, selectNode)) continue;

                if (CheckUnlockPath(player, selectNode, techTree))
                    unlockPath = true;
            }

            return unlockPath;
        }

        private object CanUnlockTechTreeNode(BasePlayer player, TechTreeData.NodeInstance node, TechTreeData techTree)
        {
            if (player == null) return null;
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord == null || xprecord.Mentality == 0) return null;
            int rarityvalue = rarityValues[node.itemDef.rarity];
            double reducecost = (config.mentality.researchcost * xprecord.Mentality) * rarityvalue;
            double researchcost = rarityvalue - reducecost;
            var cost = (int)researchcost;

            var itemdefinition = ItemManager.FindItemDefinition("scrap");
            techTree.GetEntryNode().costOverride = cost;

            if (player.inventory.GetAmount(itemdefinition.itemid) < cost)
            {
                player.ChatMessage(XPLang("techtreenode", player.UserIDString, cost, node.itemDef.displayName.english));
                return false;
            }

            return CheckUnlockPath(player, node, techTree);
        }

        private object OnTechTreeNodeUnlock(Workbench workbench, TechTreeData.NodeInstance node, BasePlayer player)
        {
            if (workbench == null || player == null) return null;
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord == null || xprecord.Mentality == 0) return null;
            int rarityvalue = rarityValues[node.itemDef.rarity];
            double reducecost = (config.mentality.researchcost * xprecord.Mentality) * rarityvalue;
            double researchcost = rarityvalue - reducecost;
            var cost = (int)researchcost;
            int itemid = ItemManager.FindItemDefinition("scrap").itemid;
            player.inventory.Take((List<Item>)null, itemid, cost);
            player.blueprints.Unlock(node.itemDef);
            return false;
        }

        private void OnItemResearch(ResearchTable table, Item targetItem, BasePlayer player)
        {
            if (player == null) return;
            XPRecord xprecord = GetXPRecord(player);
            double researchspeed = (config.mentality.researchspeed * xprecord.Mentality) * table.researchDuration;
            table.researchDuration = table.researchDuration - (float)researchspeed;
        }

        // Might

        private void PlayerArmor(BasePlayer player)
        {
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord == null) return;
            if (xprecord.Might < 0) return;
            var maxarmor = 100 + ((xprecord.Might * config.might.armor) * 100);
            player._maxHealth = (float)maxarmor;
        }

        private void MightAttributes(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            XPRecord xprecord = GetXPRecord(player);

            if (xprecord.Might > 0)
            {
                // Increase Hunger Max - Reset to default then calculate new max
                player.metabolism.calories.max = 500;
                double maxcalories = (config.might.metabolism * xprecord.Might) * player.metabolism.calories.max;
                player.metabolism.calories.max = player.metabolism.calories.max + (float)maxcalories;
                // Increase Thirst Max - Reset to default then calculate new max
                player.metabolism.hydration.max = 250;
                double maxhydration = (config.might.metabolism * xprecord.Might) * player.metabolism.hydration.max;
                player.metabolism.hydration.max = player.metabolism.hydration.max + (float)maxhydration;
            }
        }

        private void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BasePlayer player, float delta)
        {
            if (player == null || metabolism == null || !player.userID.IsSteamId()) return;
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord == null) return;
            if (xprecord.Might > 0)
            {
                // Reduce Bleeding
                if (metabolism.bleeding.value > 0)
                {
                    metabolism.bleeding.value = metabolism.bleeding.value - (((float)config.might.bleedreduction * xprecord.Might) * metabolism.bleeding.value);
                }
                // Reduce Radiation
                if (metabolism.radiation_level.value > 0)
                {
                    metabolism.radiation_level.value = metabolism.radiation_level.value - (((float)config.might.radreduction * xprecord.Might) * metabolism.radiation_level.value);
                }
                // Heat Reduction
                if (metabolism.temperature.value > PlayerMetabolism.HotThreshold)
                {
                    metabolism.temperature.value = metabolism.temperature.value - (((float)config.might.heattolerance * xprecord.Might) * 20);
                }
                // Cold Reduction
                if (metabolism.temperature.value < PlayerMetabolism.ColdThreshold)
                {
                    metabolism.temperature.value = metabolism.temperature.value + (((float)config.might.coldtolerance * xprecord.Might) * 20);
                }
                
            }
            
            //metabolism.temperature.value = 10f;
        }

        // Medic
        
        private void OnHealingItemUse(MedicalTool tool, BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId() || tool == null) return;
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord.Medic <= 0) return;
            double addhealth = xprecord.Medic * config.medic.tools;
            player._health = (float)(player._health + addhealth);
            var toolused = tool.GetType().Name;
            player.ChatMessage(XPLang("medictooluse", player.UserIDString, addhealth, toolused));
            return;
        }
        
        private void OnPlayerRevive(BasePlayer reviver, BasePlayer player)
        {
            if (reviver == null || !reviver.userID.IsSteamId() || player == null || !player.userID.IsSteamId()) return;
            XPRecord xprecord = GetXPRecord(reviver);
            double addxp = config.xpGain.playerrevive;
            GainExp(reviver, addxp);
            if (xprecord.Medic <= 0) return;
            double addhealth = xprecord.Medic * config.medic.revivehp;
            player._health = (float)(player._health + addhealth);
            player.ChatMessage(XPLang("medicreviveplayer", player.UserIDString, addhealth));
            reviver.ChatMessage(XPLang("medicrevivereviver", reviver.UserIDString, addhealth));
        }

        private void OnPlayerRecovered(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord.Medic <= 0) return;
            double addhealth = xprecord.Medic * config.medic.revivehp;
            player._health = (float)(player._health + addhealth);
            player.ChatMessage(XPLang("medicrecoverplayer", player.UserIDString, addhealth));
        }

        private void OnMixingTableToggle(MixingTable table, BasePlayer player)
        {
            if (table.IsOn()) return;
            if (player == null) return;
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord == null) return;
            if (xprecord.Medic > 0)
            {
                NextTick(() =>
                {
                    double reducetotal = (xprecord.Medic * config.medic.crafttime) * table.TotalMixTime;
                    double reduceremaining = (xprecord.Medic * config.medic.crafttime) * table.TotalMixTime;
                    table.TotalMixTime = table.TotalMixTime - (float)reducetotal;
                    table.RemainingMixTime = table.RemainingMixTime - (float)reduceremaining;
                    table.SendNetworkUpdateImmediate();
                });
            }
        }
        
        #endregion

        #region Chat Commands

        // Chat Commands
        [ChatCommand("xphelp")]
        private void cmdxphelp(BasePlayer player, string command, string[] args)
        {
            player.ChatMessage(XPLang("xphelp", player.UserIDString));
        }

        [ChatCommand("xpstats")]
        private void cmdopenxpcontrolpanelprimary(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                DestroyUi(player, XPeriencePlayerControlPrimary);
                PlayerControlPanel(player);
            }
            else
            {
                DestroyUi(player, XPeriencePlayerControlPrimary);
                var user = _xperienceCache.ToList().FirstOrDefault(x => x.Value.displayname.ToString().ToLower().Contains(args[0].ToLower()));
                if (user.Value == null) return;
                PlayerPanel(player, user.Value.id);
            }
        }

        [ChatCommand("xpstatschat")]
        private void cmdmystat(BasePlayer player, string command, string[] args)
        {
            PlayerStatsChat(player);
        }

        [ChatCommand("xptop")]
        private void cmstopxp(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                var info = "level";
                XperienceTop(player, info);
                return;
            }
            XperienceTop(player, args[0].ToLower());
        }

        [ChatCommand("xpaddstat")]
        private void cmdaddstat(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.ChatMessage(XPLang("xphelp", player.UserIDString));
                return;
            }
            StatUp(player, args[0].ToLower());
        }

        [ChatCommand("xpaddskill")]
        private void cmdaddskill(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.ChatMessage(XPLang("xphelp", player.UserIDString));
                return;
            }
            SkillUp(player, args[0].ToLower());
        }

        [ChatCommand("xpresetstats")]
        private void cmdresetstats(BasePlayer player, string command, string[] args)
        {
            StatsReset(player);
        }

        [ChatCommand("xpresetskills")]
        private void cmdresetskills(BasePlayer player, string command, string[] args)
        {
            SkillsReset(player);
        }

        [ChatCommand("xpliveui")]
        private void cmdxpliveui(BasePlayer player, string command, string[] args)
        {
            if (!config.defaultOptions.liveuistatslocationmoveable) return;
            if (args.Length == 0)
            {
                player.ChatMessage(XPLang("liveuilocation", player.UserIDString, _xperienceCache[player.UserIDString].UILocation));
                return;
            }
            var cmdArg = args[0].ToLower();

            switch (cmdArg)
            {
                case "0":
                    _xperienceCache[player.UserIDString].UILocation = 0;
                    player.ChatMessage(XPLang("liveuilocationoff", player.UserIDString, cmdArg));
                    LiveStats(player, true);
                    break;
                case "1":
                    _xperienceCache[player.UserIDString].UILocation = 1;
                    player.ChatMessage(XPLang("liveuilocation", player.UserIDString, cmdArg));
                    LiveStats(player, true);
                    break;
                case "2":
                    _xperienceCache[player.UserIDString].UILocation = 2;
                    player.ChatMessage(XPLang("liveuilocation", player.UserIDString, cmdArg));
                    LiveStats(player, true);
                    break;
                case "3":
                    _xperienceCache[player.UserIDString].UILocation = 3;
                    player.ChatMessage(XPLang("liveuilocation", player.UserIDString, cmdArg));
                    LiveStats(player, true);
                    break;
                case "4":
                    _xperienceCache[player.UserIDString].UILocation = 4;
                    player.ChatMessage(XPLang("liveuilocation", player.UserIDString, cmdArg));
                    LiveStats(player, true);
                    break;
                case "5":
                    _xperienceCache[player.UserIDString].UILocation = 5;
                    player.ChatMessage(XPLang("liveuilocation", player.UserIDString, cmdArg));
                    LiveStats(player, true);
                    break;
                default:
                    player.ChatMessage(XPLang("liveuilocationhelp", player.UserIDString, _xperienceCache[player.UserIDString].UILocation));
                    break;
            }
        }

        // Admin Commands
        [ChatCommand("xpadminhelp")]
        private void cmdadminxphelp(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
            player.ChatMessage(XPLang("xphelpadmin", player.UserIDString));
        }
        
        [ChatCommand("xpconfig")]
        private void cmdxpconfig(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
            DestroyUi(player, XPerienceAdminPanelMain);
            AdminControlPanel(player);
            AdminInfoPage(player);
        }

        [ChatCommand("resetxperience")]
        private void cmdxperiencereset(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
            _xperienceCache.Clear();
            _lootCache.Clear();
            _XPerienceData.Clear();
            _LootContainData.Clear();
            if (config.sql.enablesql)
            {
                DeleteSQL();
            }
            Interface.Oxide.ReloadPlugin("XPerience");
            player.ChatMessage(XPLang("resetxperience", player.UserIDString));
        }

        [ChatCommand("xpupdate")]
        private void cmdupdater(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
            config.xpLevel.levelmultiplier = 50;
            config.xpLevel.levelxpboost = 0.05;
            config.woodcutter.gatherrate = 0.5;
            config.woodcutter.bonusincrease = 0.10;
            config.miner.gatherrate = 0.5;
            config.miner.bonusincrease = 0.10;
            config.forager.gatherrate = 0.3;
            config.hunter.gatherrate = 0.3;
            config.hunter.bonusincrease = 0.10;
            SaveConfig();
            player.ChatMessage("updating config and reloading");
            Interface.Oxide.ReloadPlugin("XPerience");
        }
        
        [ChatCommand("xpgive")]
        private void cmdxpgive(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
            if (args.Length == 0)
            {
                player.ChatMessage(XPLang("xpgiveneedname", player.UserIDString));
                return;
            }
            var user = _xperienceCache.ToList().FirstOrDefault(x => x.Value.displayname.ToString().ToLower().Contains(args[0].ToLower()));
            if (user.Value == null)
            {
                player.ChatMessage(XPLang("xpgivenotfound", player.UserIDString));
                return;
            }
            if (args.Length == 1)
            {
                player.ChatMessage(XPLang("xpgiveneedamount", player.UserIDString));
                return;
            }
            double amount = Convert.ToDouble(args[1]);
            var selectplayer = BasePlayer.FindByID(Convert.ToUInt64(user.Value.id));
            XPRecord xprecord = GetXPRecord(selectplayer);
            GainExpAdmin(selectplayer, amount);
            player.ChatMessage(XPLang("xpgiveplayer", player.UserIDString, user.Value.displayname, amount, xprecord.experience));     
        }

        [ChatCommand("xptake")]
        private void cmdxptake(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
            if (args.Length == 0)
            {
                player.ChatMessage(XPLang("xptakeneedname", player.UserIDString));
                return;
            }
            var user = _xperienceCache.ToList().FirstOrDefault(x => x.Value.displayname.ToString().ToLower().Contains(args[0].ToLower()));
            if (user.Value == null)
            {
                player.ChatMessage(XPLang("xptakenotfound", player.UserIDString));
                return;
            }
            if (args.Length == 1)
            {
                player.ChatMessage(XPLang("xptakeneedamount", player.UserIDString));
                return;
            }
            double amount = Convert.ToDouble(args[1]);
            var selectplayer = BasePlayer.FindByID(Convert.ToUInt64(user.Value.id));
            XPRecord xprecord = GetXPRecord(selectplayer);
            LoseExp(selectplayer, amount);
            player.ChatMessage(XPLang("xptakeplayer", player.UserIDString, amount, user.Value.displayname, xprecord.experience));
        }

        [ChatCommand("xpfix")]
        private void UpdateAllPlayers(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
            foreach (var allPlayer in BasePlayer.allPlayerList)
            {
                if (!allPlayer.UserIDString.IsSteamId()) continue;
                XPRecord xprecord = GetXPRecord(allPlayer);
                // Reset Level, Required XP & Stat/Skill Points
                xprecord.level = 0;
                if(xprecord.experience <= 0)
                {
                    xprecord.experience = 0;
                }
                xprecord.requiredxp = 25;
                xprecord.statpoint = 0;
                xprecord.skillpoint = 0;
                // Reset max health if needed before removing points
                if (allPlayer._maxHealth > 100 || allPlayer._health > 100)
                {
                    // Get Stats
                    double armor = (xprecord.Might * config.might.armor) * 100;
                    double currentmaxhealth = allPlayer._maxHealth;
                    double currenthealth = Mathf.Ceil(allPlayer._health);
                    // Remove Armor
                    double newmaxhealth = currentmaxhealth - armor;
                    double newhealth = currenthealth - armor;
                    // Change Health
                    if (newmaxhealth < 100)
                    {
                        allPlayer._maxHealth = 100;
                        allPlayer._health = (float)newhealth;
                    }
                    else
                    {
                        allPlayer._maxHealth = (float)newmaxhealth;
                        allPlayer._health = (float)newhealth;
                    }
                    //allPlayer._health = 100;
                }
                // Reset Stat Levels
                xprecord.Mentality = 0;
                xprecord.Dexterity = 0;
                xprecord.Might = 0;
                xprecord.Chemist = 0;
                // Reset Stat Spent Points
                xprecord.MentalityP = 0;
                xprecord.DexterityP = 0;
                xprecord.MightP = 0;
                xprecord.ChemistP = 0;
                // Reset Skill Levels
                xprecord.WoodCutter = 0;
                xprecord.Smithy = 0;
                xprecord.Miner = 0;
                xprecord.Forager = 0;
                xprecord.Hunter = 0;
                xprecord.Fisher = 0;
                xprecord.Crafter = 0;
                xprecord.Framer = 0;
                xprecord.Medic = 0;
                xprecord.Tamer = 0;
                // Reset Skill Spents Points
                xprecord.WoodCutterP = 0;
                xprecord.SmithyP = 0;
                xprecord.MinerP = 0;
                xprecord.ForagerP = 0;
                xprecord.HunterP = 0;
                xprecord.FisherP = 0;
                xprecord.CrafterP = 0;
                xprecord.FramerP = 0;
                xprecord.MedicP = 0;
                xprecord.TamerP = 0;
                // Check/Reset Tamer permissions
                PetChecks(allPlayer, true);
                // Set LiveUI Location to Default
                xprecord.UILocation = config.defaultOptions.liveuistatslocation;
                // Run Level Up to Recalculate Players Data
                LvlUp(allPlayer, 0, 0);
                // Update Live UI
                LiveStats(allPlayer, true);
                // Notify Players
                player.ChatMessage(XPLang("adminfixplayers", player.UserIDString));
            }
        }

        #endregion

        #region UI Handlers / Controls

        [ConsoleCommand("xp.playercontrolpanel")]
        private void cmdcontrolpanel(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            DestroyUi(player, XPeriencePlayerControlPrimary);
            PlayerControlPanel(player);
        }

        [ConsoleCommand("xp.addstat")]
        private void cmdaddstat(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            StatUp(player, arg.GetString(0));
            DestroyUi(player, XPeriencePlayerControlPrimary);
            PlayerControlPanel(player);
        }

        [ConsoleCommand("xp.addskill")]
        private void cmdaddskill(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            SkillUp(player, arg.GetString(0));
            DestroyUi(player, XPeriencePlayerControlPrimary);
            PlayerControlPanel(player);
        }

        [ConsoleCommand("xp.resetstats")]
        private void cmdresetstats(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            StatsReset(player);
            DestroyUi(player, XPeriencePlayerControlPrimary);
            PlayerControlPanel(player);
        }

        [ConsoleCommand("xp.resetskills")]
        private void cmdresetskills(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            SkillsReset(player);
            DestroyUi(player, XPeriencePlayerControlPrimary);
            PlayerControlPanel(player);
        }

        [ConsoleCommand("xp.topxp")]
        private void cmdtopxpui(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var info = arg.GetString(0);
            DestroyUi(player, XPerienceTopMain);
            XperienceTop(player, info);
        }

        [ConsoleCommand("xp.player")]
        private void cmdplayerxpui(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var info = arg.GetString(0);
            DestroyUi(player, XPerienceTopMain);
            PlayerPanel(player, info);
        }

        [ConsoleCommand("xp.liveui")]
        private void cmdxpliveuisel(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var info = arg.GetString(0);

            if (info == "0")
            {
                _xperienceCache[player.UserIDString].UILocation = 0;
                player.ChatMessage(XPLang("liveuilocationoff", player.UserIDString, info));
                LiveStats(player, true);
                DestroyUi(player, XPeriencePlayerControlPrimary);
                PlayerControlPanel(player);
            }
            else if (info == "1")
            {
                _xperienceCache[player.UserIDString].UILocation = 1;
                player.ChatMessage(XPLang("liveuilocation", player.UserIDString, info));
                LiveStats(player, true);
                DestroyUi(player, XPeriencePlayerControlPrimary);
                PlayerControlPanel(player);
            }
            else if (info == "2")
            {
                _xperienceCache[player.UserIDString].UILocation = 2;
                player.ChatMessage(XPLang("liveuilocation", player.UserIDString, info));
                LiveStats(player, true);
                DestroyUi(player, XPeriencePlayerControlPrimary);
                PlayerControlPanel(player);
            }
            else if (info == "3")
            {
                _xperienceCache[player.UserIDString].UILocation = 3;
                player.ChatMessage(XPLang("liveuilocation", player.UserIDString, info));
                LiveStats(player, true);
                DestroyUi(player, XPeriencePlayerControlPrimary);
                PlayerControlPanel(player);
            }
            else if (info == "4")
            {
                _xperienceCache[player.UserIDString].UILocation = 4;
                player.ChatMessage(XPLang("liveuilocation", player.UserIDString, info));
                LiveStats(player, true);
                DestroyUi(player, XPeriencePlayerControlPrimary);
                PlayerControlPanel(player);
            }
            else if (info == "5")
            {
                _xperienceCache[player.UserIDString].UILocation = 5;
                player.ChatMessage(XPLang("liveuilocation", player.UserIDString, info));
                LiveStats(player, true);
                DestroyUi(player, XPeriencePlayerControlPrimary);
                PlayerControlPanel(player);
            }
        }

        [ConsoleCommand("xp.helpui")]
        private void cmdhelpxpui(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var page = 1;
            DestroyUi(player, XPeriencePlayerControlPrimary);
            HelpPanel(player, page);
        }

        [ConsoleCommand("xp.help")]
        private void cmdhelpxp(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var page = arg.GetInt(0);
            if (page == null || page <= 0)
            {
                page = 7;
            }
            if (page == null || page >= 8)
            {
                page = 1;
            }
            DestroyUi(player, XPeriencePlayerControlPrimary);
            HelpPanel(player, page);
        }

        [ConsoleCommand("xp.playerfixdata")]
        private void cmdplayerfixdatabutton(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            PlayerFixData(player);
            DestroyUi(player, XPeriencePlayerControlPrimary);
        }
        
        [ConsoleCommand("xp.killrecords")]
        private void cmdkrbutton(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var info = arg.GetString(0).ToLower();
            // UINotify
            if (KillRecords != null && config.xpBonus.showkrbutton)
            {
                KillRecords.Call("KRUIplayers", player, info);
            }
            DestroyUi(player, XPeriencePlayerControlPrimary);
        }
        
        // UI Helpers
        private void PlayerStatsChat(BasePlayer player)
        {
            XPRecord xprecord = GetXPRecord(player);
            player.ChatMessage(XPLang("playerprofilechat", player.UserIDString, xprecord.level, (int)xprecord.experience, (int)xprecord.requiredxp, xprecord.statpoint, xprecord.skillpoint, xprecord.Mentality, xprecord.Dexterity, xprecord.Might, xprecord.Chemist, xprecord.WoodCutter, xprecord.Smithy, xprecord.Miner, xprecord.Forager, xprecord.Hunter, xprecord.Fisher, xprecord.Crafter, xprecord.Framer, xprecord.Tamer));
        }

        private IEnumerable<XPRecord> GetTopXP(int page, int takeCount, string info)
        {
            IEnumerable<XPRecord> data = null;
            if (info == "level")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.level);
            }
            else if (info == "mentality")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.Mentality);
            }
            else if (info == "dexterity")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.Dexterity);
            }
            else if (info == "might")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.Might);
            }
            else if (info == "chemist")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.Chemist);
            }
            else if (info == "woodcutter")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.WoodCutter);
            }
            else if (info == "smithy")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.Smithy);
            }
            else if (info == "miner")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.Miner);
            }
            else if (info == "forager")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.Forager);
            }
            else if (info == "hunter")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.Hunter);
            }
            else if (info == "fisher")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.Fisher);
            }
            else if (info == "crafter")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.Crafter);
            }
            else if (info == "framer")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.Framer);
            }
            else if (info == "medic")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.Medic);
            }
            else if (info == "tamer")
            {
                data = _xperienceCache.Values.OrderByDescending(i => i.Tamer);
            }

            return data?
            .Skip((page - 1) * takeCount)
            .Take(takeCount);
        }

        private object ServerSettings(string option)
        {
            var value = "none";
            // Main
            if (option == "levelstart")
            {
                value = config.xpLevel.levelstart.ToString();
            }
            if (option == "levelmultiplier")
            {
                value = config.xpLevel.levelmultiplier.ToString();
            }
            if (option == "levelxpboost")
            {
                double boost = config.xpLevel.levelxpboost * 100;
                value = boost.ToString();
            }
            if (option == "statpointsperlvl")
            {
                value = config.xpLevel.statpointsperlvl.ToString();
            }
            if (option == "skillpointsperlvl")
            {
                value = config.xpLevel.skillpointsperlvl.ToString();
            }
            if (option == "resettimerenabled")
            {
                value = config.defaultOptions.restristresets.ToString();
            }
            if (option == "resettimerstats")
            {
                value = config.defaultOptions.resetminsstats.ToString();
            }
            if (option == "resettimerskills")
            {
                value = config.defaultOptions.resetminsskills.ToString();
            }
            if (option == "vipresettimerstats")
            {
                value = config.defaultOptions.vipresetminstats.ToString();
            }
            if (option == "vipresettimerskills")
            {
                value = config.defaultOptions.vipresetminsskills.ToString();
            }
            if (option == "nightbonusenable")
            {
                value = config.nightBonus.Enable.ToString();
            }
            if (option == "nightbonus")
            {
                double nightboost = config.nightBonus.Bonus * 100;
                value = nightboost.ToString();
            }
            if (option == "nightstart")
            {
                value = config.nightBonus.StartTime.ToString();
            }
            if (option == "nightend")
            {
                value = config.nightBonus.EndTime.ToString();
            }
            if (option == "nightskill")
            {
                value = config.nightBonus.enableskillboosts.ToString();
            }
            // Kills
            if (option == "chicken")
            {
                value = config.xpGain.chickenxp.ToString();
            }
            if (option == "fish")
            {
                value = config.xpGain.fishxp.ToString();
            }
            if (option == "boar")
            {
                value = config.xpGain.boarxp.ToString();
            }
            if (option == "stag")
            {
                value = config.xpGain.stagxp.ToString();
            }
            if (option == "wolf")
            {
                value = config.xpGain.wolfxp.ToString();
            }
            if (option == "bear")
            {
                value = config.xpGain.bearxp.ToString();
            }
            if (option == "shark")
            {
                value = config.xpGain.sharkxp.ToString();
            }
            if (option == "horse")
            {
                value = config.xpGain.horsexp.ToString();
            }
            if (option == "scientist")
            {
                value = config.xpGain.scientistxp.ToString();
            }
            if (option == "dweller")
            {
                value = config.xpGain.dwellerxp.ToString();
            }
            if (option == "player")
            {
                value = config.xpGain.playerxp.ToString();
            }
            if (option == "bradley")
            {
                value = config.xpGain.bradley.ToString();
            }
            if (option == "heli")
            {
                value = config.xpGain.patrolhelicopter.ToString();
            }
            if (option == "revive")
            {
                value = config.xpGain.playerrevive.ToString();
            }
            // Gathering / Loot
            if (option == "loot")
            {
                value = config.xpGain.lootcontainerxp.ToString();
            }
            if (option == "uloot")
            {
                value = config.xpGain.underwaterlootcontainerxp.ToString();
            }
            if (option == "lloot")
            {
                value = config.xpGain.lockedcratexp.ToString();
            }
            if (option == "hloot")
            {
                value = config.xpGain.hackablecratexp.ToString();
            }
            if (option == "aharvest")
            {
                value = config.xpGain.animalharvestxp.ToString();
            }
            if (option == "charvest")
            {
                value = config.xpGain.corpseharvestxp.ToString();
            }
            if (option == "tree")
            {
                value = config.xpGather.treexp.ToString();
            }
            if (option == "ore")
            {
                value = config.xpGather.orexp.ToString();
            }
            if (option == "gather")
            {
                value = config.xpGather.harvestxp.ToString();
            }
            if (option == "plant")
            {
                value = config.xpGather.plantxp.ToString();
            }
            // Crafting / Building
            if (option == "craft")
            {
                value = config.xpGain.craftingxp.ToString();
            }
            if (option == "wood")
            {
                value = config.xpBuilding.woodstructure.ToString();
            }
            if (option == "stone")
            {
                value = config.xpBuilding.stonestructure.ToString();
            }
            if (option == "metal")
            {
                value = config.xpBuilding.metalstructure.ToString();
            }
            if (option == "armored")
            {
                value = config.xpBuilding.armoredstructure.ToString();
            }
            // Missions
            if (option == "missionsucceed")
            {
                value = config.xpMissions.missionsucceededxp.ToString();
            }            
            if (option == "missionfailed")
            {
                value = config.xpMissions.missionfailed.ToString();
            }            
            if (option == "missionfailedxp")
            {
                value = config.xpMissions.missionsucceededxp.ToString();
            }
            // Reductions
            if (option == "death")
            {
                double death = config.xpReducer.deathreduceamount * 100;
                value = death.ToString();
            }
            if (option == "deathenable")
            {
                value = config.xpReducer.deathreduce.ToString();
            }
            if (option == "suicide")
            {
                double suicide = config.xpReducer.suicidereduceamount * 100;
                value = suicide.ToString();
            }
            if (option == "suicideenable")
            {
                value = config.xpReducer.suicidereduce.ToString();
            }
            // Mentality
            if (option == "mentalitylevel")
            {
                value = config.mentality.maxlvl.ToString();
            }
            if (option == "mentalitycost")
            {
                value = config.mentality.pointcoststart.ToString();
            }
            if (option == "mentalitymultiplier")
            {
                value = config.mentality.costmultiplier.ToString();
            }
            if (option == "mentalityresearchcost")
            {
                double mentcost = config.mentality.researchcost * 100;
                value = mentcost.ToString();
            }
            if (option == "mentalityresearchspeed")
            {
                double mentspeed = config.mentality.researchspeed * 100;
                value = mentspeed.ToString();
            }
            if (option == "mentalitycrit")
            {
                double mentcrit = config.mentality.criticalchance * 100;
                value = mentcrit.ToString();
            }
            // Dexterity
            if (option == "dexlevel")
            {
                value = config.dexterity.maxlvl.ToString();
            }
            if (option == "dexcost")
            {
                value = config.dexterity.pointcoststart.ToString();
            }
            if (option == "dexmultiplier")
            {
                value = config.dexterity.costmultiplier.ToString();
            }
            if (option == "dexblock")
            {
                double mentcost = config.dexterity.blockchance * 100;
                value = mentcost.ToString();
            }
            if (option == "dexblockamt")
            {
                double mentspeed = config.dexterity.blockamount * 100;
                value = mentspeed.ToString();
            }
            if (option == "dexdodge")
            {
                double mentcrit = config.dexterity.dodgechance * 100;
                value = mentcrit.ToString();
            }
            if (option == "dexarmor")
            {
                double mentcrit = config.dexterity.reducearmordmg * 100;
                value = mentcrit.ToString();
            }
            // Might
            if (option == "mightlevel")
            {
                value = config.might.maxlvl.ToString();
            }
            if (option == "mightcost")
            {
                value = config.might.pointcoststart.ToString();
            }
            if (option == "mightmultiplier")
            {
                value = config.might.costmultiplier.ToString();
            }
            if (option == "mightarmor")
            {
                double might = config.might.armor * 100;
                value = might.ToString();
            }
            if (option == "mightmelee")
            {
                double might = config.might.meleedmg * 100;
                value = might.ToString();
            }
            if (option == "mightmeta")
            {
                double might = config.might.metabolism * 100;
                value = might.ToString();
            }
            if (option == "mightbleed")
            {
                double might = config.might.bleedreduction * 100;
                value = might.ToString();
            }
            if (option == "mightrad")
            {
                double might = config.might.radreduction * 100;
                value = might.ToString();
            }
            if (option == "mightheat")
            {
                double might = config.might.heattolerance * 100;
                value = might.ToString();
            }
            if (option == "mightcold")
            {
                double might = config.might.coldtolerance * 100;
                value = might.ToString();
            }
            // Chemist
            if (option == "chemlevel")
            {
                value = config.chemist.maxlvl.ToString();
            }
            if (option == "chemcost")
            {
                value = config.chemist.pointcoststart.ToString();
            }
            if (option == "chemmultiplier")
            {
                value = config.chemist.costmultiplier.ToString();
            }
            if (option == "chemcraft")
            {
                double chemcraft = config.chemist.crafttime * 100;
                value = chemcraft.ToString();
            }
            // WoodCutter
            if (option == "woodcutterlev")
            {
                value = config.woodcutter.maxlvl.ToString();
            }
            if (option == "woodcuttercost")
            {
                value = config.woodcutter.pointcoststart.ToString();
            }
            if (option == "woodcuttermulti")
            {
                value = config.woodcutter.pointcoststart.ToString();
            }
            if (option == "woodcuttergather")
            {
                double woodgather = config.woodcutter.gatherrate * 100;
                value = woodgather.ToString();
            }
            if (option == "woodcutterbonus")
            {
                double woodbonus = config.woodcutter.bonusincrease * 100;
                value = woodbonus.ToString();
            }
            if (option == "woodcutterapple")
            {
                double woodapple = config.woodcutter.applechance * 100;
                value = woodapple.ToString();
            }
            // Smithy
            if (option == "smithylev")
            {
                value = config.smithy.maxlvl.ToString();
            }
            if (option == "smithycost")
            {
                value = config.smithy.pointcoststart.ToString();
            }
            if (option == "smithymulti")
            {
                value = config.smithy.pointcoststart.ToString();
            }
            if (option == "smithyproduction")
            {
                double smithyrate = config.smithy.productionrate * 100;
                value = smithyrate.ToString();
            }
            if (option == "smithyfuel")
            {
                double smithyfuel = config.smithy.fuelconsumption * 100;
                value = smithyfuel.ToString();
            }
            // Miner
            if (option == "minerlev")
            {
                value = config.forager.maxlvl.ToString();
            }
            if (option == "minercost")
            {
                value = config.forager.pointcoststart.ToString();
            }
            if (option == "minermulti")
            {
                value = config.forager.costmultiplier.ToString();
            }
            if (option == "minergather")
            {
                double minergather = config.miner.gatherrate * 100;
                value = minergather.ToString();
            }
            if (option == "minerbonus")
            {
                double minerbonus = config.miner.bonusincrease * 100;
                value = minerbonus.ToString();
            }
            if (option == "minerfuel")
            {
                double minerfuel = config.miner.fuelconsumption * 100;
                value = minerfuel.ToString();
            }
            // Forager
            if (option == "foragerlev")
            {
                value = config.forager.maxlvl.ToString();
            }
            if (option == "foragercost")
            {
                value = config.forager.pointcoststart.ToString();
            }
            if (option == "foragermulti")
            {
                value = config.forager.costmultiplier.ToString();
            }
            if (option == "foragergather")
            {
                double forgrate = config.forager.gatherrate * 100;
                value = forgrate.ToString();
            }
            if (option == "foragerseed")
            {
                double seedchance = config.forager.chanceincrease * 100;
                value = seedchance.ToString();
            }
            if (option == "foragerseedamt")
            {
                double seedamt = config.forager.chanceincrease * 10;
                value = seedamt.ToString();
            }
            if (option == "forageritem")
            {
                double forgitem = config.forager.randomchance * 100;
                value = forgitem.ToString();
            }
            // Hunter
            if (option == "hunterlev")
            {
                value = config.hunter.maxlvl.ToString();
            }
            if (option == "huntercost")
            {
                value = config.hunter.pointcoststart.ToString();
            }
            if (option == "huntermulti")
            {
                value = config.hunter.costmultiplier.ToString();
            }
            if (option == "huntergather")
            {
                double huntrate = config.hunter.gatherrate * 100;
                value = huntrate.ToString();
            }
            if (option == "hunterbonus")
            {
                double huntbonus = config.hunter.bonusincrease * 100;
                value = huntbonus.ToString();
            }
            if (option == "hunterdmg")
            {
                double huntdmg = config.hunter.damageincrease * 100;
                value = huntdmg.ToString();
            }
            if (option == "hunterdmgnight")
            {
                double huntdmgnight = config.hunter.nightdmgincrease * 100;
                value = huntdmgnight.ToString();
            }
            // Fisher
            if (option == "fisherlev")
            {
                value = config.fisher.maxlvl.ToString();
            }
            if (option == "fishercost")
            {
                value = config.fisher.pointcoststart.ToString();
            }
            if (option == "fishermulti")
            {
                value = config.fisher.costmultiplier.ToString();
            }
            if (option == "fisheramt")
            {
                double huntrate = Math.Round(config.fisher.fishamountincrease);
                value = huntrate.ToString();
            }
            if (option == "fisheramtitem")
            {
                double huntbonus = Math.Round(config.fisher.itemamountincrease);
                value = huntbonus.ToString();
            }
            // Crafter
            if (option == "craftlev")
            {
                value = config.crafter.maxlvl.ToString();
            }
            if (option == "craftcost")
            {
                value = config.crafter.pointcoststart.ToString();
            }
            if (option == "craftmulti")
            {
                value = config.crafter.costmultiplier.ToString();
            }
            if (option == "craftspeed")
            {
                double craftspeed = config.crafter.craftspeed * 100;
                value = craftspeed.ToString();
            }
            if (option == "craftcostitem")
            {
                double craftcost = config.crafter.craftcost * 100;
                value = craftcost.ToString();
            }
            if (option == "craftrepair")
            {
                double craftrepair = config.crafter.repairincrease * 100;
                value = craftrepair.ToString();
            }
            if (option == "craftcond")
            {
                double craftcond = config.crafter.conditionchance * 100;
                value = craftcond.ToString();
            }
            // Framer
            if (option == "framerlev")
            {
                value = config.framer.maxlvl.ToString();
            }
            if (option == "framercost")
            {
                value = config.framer.pointcoststart.ToString();
            }
            if (option == "framermulti")
            {
                value = config.framer.costmultiplier.ToString();
            }
            if (option == "framerupgrade")
            {
                double framerupgrade = config.framer.upgradecost * 100;
                value = framerupgrade.ToString();
            }
            if (option == "framerrepair")
            {
                double framerrepair = config.framer.repaircost * 100;
                value = framerrepair.ToString();
            }
            if (option == "framertime")
            {
                double framertime = config.framer.repairtime * 100;
                value = framertime.ToString();
            }
            // Medic
            if (option == "mediclev")
            {
                value = config.medic.maxlvl.ToString();
            }
            if (option == "mediccost")
            {
                value = config.medic.pointcoststart.ToString();
            }
            if (option == "medicmulti")
            {
                value = config.medic.costmultiplier.ToString();
            }
            if (option == "medichpp")
            {
                value = config.medic.revivehp.ToString();
            }
            if (option == "medichp")
            {
                value = config.medic.recoverhp.ToString();
            }
            if (option == "mediccraft")
            {
                double mediccraft = config.medic.crafttime * 100;
                value = mediccraft.ToString();
            }
            // Tamer
            if (option == "tamerenabled")
            {
                value = config.tamer.enabletame.ToString();
            }
            if (option == "tamerlev")
            {
                value = config.tamer.maxlvl.ToString();
            }
            if (option == "tamercost")
            {
                value = config.tamer.pointcoststart.ToString();
            }
            if (option == "tamermulti")
            {
                value = config.tamer.costmultiplier.ToString();
            }
            if (option == "tamerchicken")
            {
                value = config.tamer.tamechicken.ToString();
            }
            if (option == "tamerchickenlev")
            {
                value = config.tamer.chickenlevel.ToString();
            }
            if (option == "tamerboar")
            {
                value = config.tamer.tameboar.ToString();
            }
            if (option == "tamerboarlev")
            {
                value = config.tamer.boarlevel.ToString();
            }
            if (option == "tamerstag")
            {
                value = config.tamer.tamestag.ToString();
            }
            if (option == "tamerstaglev")
            {
                value = config.tamer.staglevel.ToString();
            }
            if (option == "tamerwolf")
            {
                value = config.tamer.tamewolf.ToString();
            }
            if (option == "tamerwolflev")
            {
                value = config.tamer.wolflevel.ToString();
            }
            if (option == "tamerbear")
            {
                value = config.tamer.tamebear.ToString();
            }
            if (option == "tamerbearlev")
            {
                value = config.tamer.bearlevel.ToString();
            }
            // Return Values
            return value;
        }

        private void DestroyUi(BasePlayer player, string name)
        {
            CuiHelper.DestroyUi(player, name);
        }

        #endregion

        #region UI Constants

        private const string XPerienceLivePrimary = "XPerienceLivePrimary";
        private const string XPerienceLiveArmorIcon = "XPerienceLiveArmorIcon";
        private const string XPerienceLiveArmorBar = "XPerienceLiveArmorBar";
        private const string XPerienceLiveLevelIcon = "XPerienceLiveLevelIcon";
        private const string XPerienceLiveLevelBar = "XPerienceLiveLevelBar";
        private const string XPeriencePlayerControlPrimary = "XPeriencePlayerControlPrimary";
        private const string XPeriencePlayerControlMain = "XPeriencePlayerControlMain";
        private const string XPeriencePlayerControlStats = "XPeriencePlayerControlStats";
        private const string XPeriencePlayerControlSkills = "XPeriencePlayerControlSkills";
        private const string XPeriencePlayerControlSkillsL = "XPeriencePlayerControlSkillsL";
        private const string XPeriencePlayerControlSkillsR = "XPeriencePlayerControlSkillsR";
        private const string XPerienceTopMain = "XPerienceTopMain";
        private const string XPerienceTopSelection = "XPerienceTopSelection";
        private const string XPerienceTopInner = "XPerienceTopInner";
        private const string XPerienceHelp = "XPerienceHelp";
        private const string XPerienceAdminPanelMain = "XPerienceAdminPanelMain";
        private const string XPerienceAdminPanelMenu = "XPerienceAdminPanelMenu";
        private const string XPerienceAdminPanelInfo = "XPerienceAdminPanelInfo";
        private const string XPerienceAdminPanelLevelXP = "XPerienceAdminPanelLevelXP";
        private const string XPerienceAdminPanelStats = "XPerienceAdminPanelStats";
        private const string XPerienceAdminPanelSkills = "XPerienceAdminPanelSkills";
        private const string XPerienceAdminPanelTimerColor = "XPerienceAdminPanelTimerColor";
        private const string XPerienceAdminPanelOtherMods = "XPerienceAdminPanelOtherMods";
        private const string XPerienceAdminPanelSQL = "XPerienceAdminPanelSQL";
        private const string XPerienceAdminPanelReset = "XPerienceAdminPanelReset";

        #endregion

        #region Colors

        private object TextColor(string type, int value)
        {
            var color = config.uitextColor.defaultcolor;
            if (type == "mainlevel" && value > 0)
            {
                color = config.uitextColor.level;
            }
            if (type == "experience" && value > 0)
            {
                color = config.uitextColor.experience;
            }
            if (type == "nextlevel" && value > 0)
            {
                color = config.uitextColor.nextlevel;
            }
            if (type == "remainingxp" && value > 0)
            {
                color = config.uitextColor.remainingxp;
            }
            if (type == "level" && value > 0)
            {
                color = config.uitextColor.statskilllevels;
            }
            if (type == "perk" && value > 0)
            {
                color = config.uitextColor.perks;
            }
            if (type == "unspent" && value > 0)
            {
                color = config.uitextColor.unspentpoints;
            }
            if (type == "spent" && value > 0)
            {
                color = config.uitextColor.spentpoints;
            }
            if (type == "pets" && value > 0)
            {
                color = config.uitextColor.pets;
            }
            // Live UI Color Selected
            if (type == "UI0" && value == 0)
            {
                color = "0.0 1.0 0.0 1.0";
            }
            if (type == "UI1" && value == 1)
            {
                color = "0.0 1.0 0.0 1.0";
            }
            if (type == "UI2" && value == 2)
            {
                color = "0.0 1.0 0.0 1.0";
            }
            if (type == "UI3" && value == 3)
            {
                color = "0.0 1.0 0.0 1.0";
            }
            if (type == "UI4" && value == 4)
            {
                color = "0.0 1.0 0.0 1.0";
            }
            if (type == "UI5" && value == 5)
            {
                color = "0.0 1.0 0.0 1.0";
            }
            // Live UI Color Unselected
            if (type == "UI0" && value != 0)
            {
                color = "1.0 0.0 0.0 1.0";
            }
            if (type == "UI1" && value != 1)
            {
                color = "1.0 0.0 0.0 1.0";
            }
            if (type == "UI2" && value != 2)
            {
                color = "1.0 0.0 0.0 1.0";
            }
            if (type == "UI3" && value != 3)
            {
                color = "1.0 0.0 0.0 1.0";
            }
            if (type == "UI4" && value != 4)
            {
                color = "1.0 0.0 0.0 1.0";
            }
            if (type == "UI5" && value != 5)
            {
                color = "1.0 0.0 0.0 1.0";
            }

            // Return Values
            return color;
        }

        #endregion

        #region UI Panels

        private CuiPanel XPUIPanel(string anchorMin, string anchorMax, string color = "0 0 0 0")
        {
            return new CuiPanel
            {
                Image =
                {
                    Color = color
                },
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                }
            };
        }

        private CuiLabel XPUILabel(string text, int i, float height, TextAnchor align = TextAnchor.MiddleLeft, int fontSize = 13, string xMin = "0", string xMax = "1", string color = "1.0 1.0 1.0 1.0")
        {
            return new CuiLabel
            {
                Text =
                {
                    Text = text,
                    FontSize = fontSize,
                    Align = align,
                    Color = color
                },
                RectTransform =
                {
                    AnchorMin = $"{xMin} {1 - height*i + i * .002f}",
                    AnchorMax = $"{xMax} {1 - height*(i-1) + i * .002f}"
                }
            };
        }

        private CuiButton XPUIButton(string command, int i, float rowHeight, int fontSize = 11, string color = "1.0 0.0 0.0 0.7", string content = "+", string xMin = "0", string xMax = "1", TextAnchor align = TextAnchor.MiddleLeft, string fcolor = "1.0 1.0 1.0 1.0")
        {
            return new CuiButton
            {
                Button =
                {
                    Command = command,
                    Color = $"{color}"
                },
                RectTransform =
                {
                    AnchorMin = $"{xMin} {1 - rowHeight*i + i * .002f}",
                    AnchorMax = $"{xMax} {1 - rowHeight*(i-1) + i * .002f}"
                },
                Text =
                {
                    Text = content,
                    FontSize = fontSize,
                    Align = align,
                    Color = fcolor,
                }
            };
        }

        private void LiveStats(BasePlayer player, bool update = false)
        {
            if (player == null) return;
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord == null || xprecord.UILocation == 0)
            {
                DestroyUi(player, XPerienceLivePrimary);
                return;
            }
            if (update)
            {
                DestroyUi(player, XPerienceLivePrimary);
            }
            // XP Bar defaults
            double lastlevel = 0;
            double nextlevel = 0;
            double currentxp = 0;
            double reqxpperc = 0;
            double remainingxp = 0;
            double levelpercent = 0;

            // Armor Bar Calculations
            double armorpoints = (xprecord.Might * config.might.armor) * 100;
            double maxhealth = player._maxHealth;
            //double maxhealth = player._maxHealth;
            double fullhealth = maxhealth - armorpoints;
            double currenthealth = Mathf.Ceil(player._health);
            double maxarmor = maxhealth - fullhealth;
            double currentarmor = 0;
            double teaadd = currenthealth - maxhealth;
            // Armor
            if (currenthealth > fullhealth)
            {
                currentarmor = ((float)currenthealth - (player._maxHealth - armorpoints));
            }
            // Detect Teas
            if (currenthealth > maxhealth)
            {
                maxarmor = maxarmor + teaadd;
            }
            var armorperc = currentarmor / maxarmor;

            // XP Bar Calulations
            if (xprecord.experience == 0 || xprecord.level == 0)
            {
                lastlevel = 0;
                nextlevel = config.xpLevel.levelstart;
                currentxp = xprecord.experience - lastlevel;
                reqxpperc = (xprecord.experience - lastlevel) / nextlevel;
                remainingxp = nextlevel - currentxp;
                levelpercent = reqxpperc * 100;
            }
            else
            {
                lastlevel = xprecord.requiredxp - (xprecord.level * config.xpLevel.levelmultiplier);
                nextlevel = xprecord.requiredxp - lastlevel;
                currentxp = xprecord.experience - lastlevel;
                reqxpperc = (xprecord.experience - lastlevel) / nextlevel;
                remainingxp = nextlevel - currentxp;
                levelpercent = reqxpperc * 100;
            }
            var LIVEelements = new CuiElementContainer();

            if (xprecord.UILocation == 1)
            {
                // Live UI Box
                LIVEelements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.0 0.0 0.0 0.0"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.66 0.025",
                        AnchorMax = "0.82 0.1075"
                    }
                }, "Hud", XPerienceLivePrimary);
                // Live UI Temp/Armor
                if (xprecord.Might > 0)
                {
                    // Armor
                    LIVEelements.Add(XPUIPanel("0.0 0.66", "0.10 1.0", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveArmorIcon);
                    LIVEelements.Add(XPUILabel("✪", 1, 1, TextAnchor.MiddleCenter, 17, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveArmorIcon);
                    LIVEelements.Add(XPUIPanel("0.1075 0.66", "1.0 1.0", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveArmorBar);
                    LIVEelements.Add(XPUIPanel("0.0 0.10", $"{armorperc} 0.99", "1.50 0.05 0.05 0.80"), XPerienceLiveArmorBar);
                    LIVEelements.Add(XPUILabel($"{currentarmor}   /   {maxarmor}", 1, 1, TextAnchor.MiddleCenter, 15, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveArmorBar);
                }
                // Live UI Level
                LIVEelements.Add(XPUIPanel("0.0 0.33", "1.0 0.65", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveLevelIcon);
                LIVEelements.Add(XPUILabel($"{XPLang("level", player.UserIDString)}: {xprecord.level} ({(int)levelpercent}%)", 1, 1.06f, TextAnchor.MiddleCenter, 15, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveLevelIcon);
                // Live UI Level Bar
                LIVEelements.Add(XPUIPanel("0.0 0.0", "1.0 0.32", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveLevelBar);
                LIVEelements.Add(XPUIPanel("0.0 0.08", $"{reqxpperc} 0.90", "0.05 1.05 0.05 0.80"), XPerienceLiveLevelBar);
                LIVEelements.Add(XPUILabel($"{(int)remainingxp}", 1, 1, TextAnchor.MiddleCenter, 15, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveLevelBar);
            }
            
            if (xprecord.UILocation == 2)
            {
                // Live UI Box
                LIVEelements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.0 0.0 0.0 0.0"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.00 0.025",
                        AnchorMax = "0.15 0.1075"
                    }
                }, "Hud", XPerienceLivePrimary);
                // Live UI Armor
                if (xprecord.Might > 0)
                {
                    LIVEelements.Add(XPUIPanel("0.0 0.66", "0.10 1.0", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveArmorIcon);
                    LIVEelements.Add(XPUILabel("✪", 1, 1, TextAnchor.MiddleCenter, 17, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveArmorIcon);
                    LIVEelements.Add(XPUIPanel("0.1075 0.66", "1.0 1.0", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveArmorBar);
                    LIVEelements.Add(XPUIPanel("0.0 0.10", $"{armorperc} 0.99", "1.50 0.05 0.05 0.80"), XPerienceLiveArmorBar);
                    LIVEelements.Add(XPUILabel($"{currentarmor}   /   {maxarmor}", 1, 1, TextAnchor.MiddleCenter, 15, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveArmorBar);
                }
                // Live UI Level
                LIVEelements.Add(XPUIPanel("0.0 0.33", "1.0 0.65", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveLevelIcon);
                LIVEelements.Add(XPUILabel($"{XPLang("level", player.UserIDString)}: {xprecord.level} ({(int)levelpercent}%)", 1, 1.06f, TextAnchor.MiddleCenter, 15, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveLevelIcon);
                // Live UI Level Bar
                LIVEelements.Add(XPUIPanel("0.0 0.0", "1.0 0.32", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveLevelBar);
                LIVEelements.Add(XPUIPanel("0.0 0.08", $"{reqxpperc} 0.90", "0.05 1.05 0.05 0.80"), XPerienceLiveLevelBar);
                LIVEelements.Add(XPUILabel($"{(int)remainingxp}", 1, 1, TextAnchor.MiddleCenter, 15, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveLevelBar);
            }

            if (xprecord.UILocation == 3)
            {
                // Live UI Box
                LIVEelements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.0 0.0 0.0 0.0"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.00 0.9025",
                        AnchorMax = "0.15 0.995"
                    }
                }, "Hud", XPerienceLivePrimary);
                // Live UI Armor
                if (xprecord.Might > 0)
                {
                    LIVEelements.Add(XPUIPanel("0.0 0.66", "0.10 1.0", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveArmorIcon);
                    LIVEelements.Add(XPUILabel("✪", 1, 1, TextAnchor.MiddleCenter, 17, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveArmorIcon);
                    LIVEelements.Add(XPUIPanel("0.1075 0.66", "1.0 1.0", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveArmorBar);
                    LIVEelements.Add(XPUIPanel("0.0 0.10", $"{armorperc} 0.99", "1.50 0.05 0.05 0.80"), XPerienceLiveArmorBar);
                    LIVEelements.Add(XPUILabel($"{currentarmor}   /   {maxarmor}", 1, 1, TextAnchor.MiddleCenter, 15, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveArmorBar);
                }
                // Live UI Level
                LIVEelements.Add(XPUIPanel("0.0 0.33", "1.0 0.65", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveLevelIcon);
                LIVEelements.Add(XPUILabel($"{XPLang("level", player.UserIDString)}: {xprecord.level} ({(int)levelpercent}%)", 1, 1.06f, TextAnchor.MiddleCenter, 15, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveLevelIcon);
                // Live UI Level Bar
                LIVEelements.Add(XPUIPanel("0.0 0.0", "1.0 0.315", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveLevelBar);
                LIVEelements.Add(XPUIPanel("0.0 0.08", $"{reqxpperc} 0.90", "0.05 1.05 0.05 0.80"), XPerienceLiveLevelBar);
                LIVEelements.Add(XPUILabel($"{(int)remainingxp}", 1, 1, TextAnchor.MiddleCenter, 15, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveLevelBar);
            }

            if (xprecord.UILocation == 4)
            {
                // Live UI Box
                LIVEelements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.0 0.0 0.0 0.0"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.84 0.9025",
                        AnchorMax = "0.995 0.995"
                    }
                }, "Hud", XPerienceLivePrimary);
                // Live UI Armor
                if (xprecord.Might > 0)
                {
                    LIVEelements.Add(XPUIPanel("0.0 0.66", "0.10 1.0", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveArmorIcon);
                    LIVEelements.Add(XPUILabel("✪", 1, 1, TextAnchor.MiddleCenter, 17, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveArmorIcon);
                    LIVEelements.Add(XPUIPanel("0.1075 0.66", "1.0 1.0", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveArmorBar);
                    LIVEelements.Add(XPUIPanel("0.0 0.10", $"{armorperc} 0.99", "1.50 0.05 0.05 0.80"), XPerienceLiveArmorBar);
                    LIVEelements.Add(XPUILabel($"{currentarmor}   /   {maxarmor}", 1, 1, TextAnchor.MiddleCenter, 15, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveArmorBar);
                }
                // Live UI Level
                LIVEelements.Add(XPUIPanel("0.0 0.33", "1.0 0.65", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveLevelIcon);
                LIVEelements.Add(XPUILabel($"{XPLang("level", player.UserIDString)}: {xprecord.level} ({(int)levelpercent}%)", 1, 1.06f, TextAnchor.MiddleCenter, 15, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveLevelIcon);
                // Live UI Level Bar
                LIVEelements.Add(XPUIPanel("0.0 0.0", "1.0 0.315", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveLevelBar);
                LIVEelements.Add(XPUIPanel("0.0 0.08", $"{reqxpperc} 0.90", "0.05 1.05 0.05 0.80"), XPerienceLiveLevelBar);
                LIVEelements.Add(XPUILabel($"{(int)remainingxp}", 1, 1, TextAnchor.MiddleCenter, 15, "0.0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveLevelBar);
            }

            if (xprecord.UILocation == 5)
            {
                // Live UI Box
                LIVEelements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.0 0.0 0.0 0.0"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.344 0.0",
                        AnchorMax = "0.64 0.13"
                    }
                }, "Hud", XPerienceLivePrimary);
                // Live UI Armor Bar
                if (xprecord.Might > 0)
                {
                    LIVEelements.Add(XPUIPanel("0.001 0.829", "0.999 0.999", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveArmorBar);
                    LIVEelements.Add(XPUIPanel("0 0", $"{armorperc} 0.99", "1.50 0.05 0.05 0.80"), XPerienceLiveArmorBar);
                    LIVEelements.Add(XPUILabel($"{currentarmor}   /   {maxarmor}", 1, 1, TextAnchor.MiddleCenter, 12, "0.001", "0.999", "1.0 1.0 1.0 0.70"), XPerienceLiveArmorBar);
                    LIVEelements.Add(XPUILabel($"✪{XPLang("armor", player.UserIDString)}", 1, 1, TextAnchor.MiddleCenter, 12, "0", "0.15", "1.0 1.0 1.0 0.9"), XPerienceLiveArmorBar);
                }
                // Live UI Level Bar
                LIVEelements.Add(XPUIPanel("0.001 0", "0.495 0.17", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveLevelIcon); //0.33 0.65
                LIVEelements.Add(XPUILabel($"{XPLang("level", player.UserIDString)}: {xprecord.level} ({(int)levelpercent}%)", 1, 1, TextAnchor.MiddleCenter, 12, "0", "1", "1.0 1.0 1.0 0.70"), XPerienceLiveLevelIcon);
                // Live UI XP Bar
                LIVEelements.Add(XPUIPanel("0.505 0", "0.999 0.17", "0.0 0.0 0.0 0.50"), XPerienceLivePrimary, XPerienceLiveLevelBar); // 2.28 4.20
                LIVEelements.Add(XPUIPanel("0 0", $"{reqxpperc} 0.9", "0.05 1.05 0.05 0.80"), XPerienceLiveLevelBar);
                LIVEelements.Add(XPUILabel($"{XPLang("xp", player.UserIDString)}: {(int)remainingxp}", 1, 1, TextAnchor.MiddleCenter, 12, "0", "0.9", "1.0 1.0 1.0 0.70"), XPerienceLiveLevelBar);          
            }
            
            CuiHelper.AddUi(player, LIVEelements);
            return;
        }

        private void PlayerControlPanel(BasePlayer player)
        {
            if (player == null) return;
            XPRecord xprecord = GetXPRecord(player);
            if (xprecord == null) return;
            float height = 0.036f;
            float buttonheight = 0.035f;
            var ControlPanelelements = new CuiElementContainer();
            // Main UI
            ControlPanelelements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.1 0.1 0.1 0.95"
                },
                RectTransform =
                {
                    AnchorMin = $"0.10 0.10",
                    AnchorMax = $"0.90 0.95"
                },
                CursorEnabled = true
            }, "Overlay", XPeriencePlayerControlPrimary);
            // Close Button
            ControlPanelelements.Add(new CuiButton
            {
                Button =
                {
                    Close = XPeriencePlayerControlPrimary,
                    Color = "0.0 0.0 0.0 0.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.95 0.94",
                    AnchorMax = "1.0 1.0"
                },
                Text =
                {
                    Text = "ⓧ",
                    FontSize = 20,
                    Color = "1.0 0.0 0.0 1.0",
                    Align = TextAnchor.MiddleCenter
                }
            }, XPeriencePlayerControlPrimary);
            // Help Button
            ControlPanelelements.Add(new CuiButton
            {
                Button =
                {
                    Command = "xp.helpui 1",
                    Color = "1.0 0.0 0.0 1.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.16 0.95",
                    AnchorMax = "0.25 0.99"
                },
                Text =
                {
                    Text = $"{XPLang("help", player.UserIDString)}",
                    FontSize = 12,
                    Color = "1.0 1.0 1.0 1.0",
                    Align = TextAnchor.MiddleCenter
                }
            }, XPeriencePlayerControlPrimary);
            // Reset Stats Button
            DateTime resettimestats = xprecord.resettimerstats.AddMinutes(config.defaultOptions.resetminsstats);
            TimeSpan statsinterval = resettimestats - DateTime.Now;
            int statstimer = (int)statsinterval.TotalMinutes;
            if (config.defaultOptions.bypassadminreset && player.IsAdmin && permission.UserHasPermission(player.UserIDString, XPerience.Admin))
            {
                statstimer = 0;
            }
            if (statstimer > 0 && config.defaultOptions.restristresets)
            {
                ControlPanelelements.Add(new CuiButton
                {
                    Button =
                            {
                                Command = $"xp.resetstats {player.UserIDString}",
                                Color = "1.0 0.0 0.0 1.0"
                            },
                    RectTransform =
                            {
                                    AnchorMin = "0.31 0.95",
                                    AnchorMax = "0.45 0.99"
                            },
                    Text =
                            {
                                Text = $"{XPLang("canresetstats", player.UserIDString, statstimer)}",
                                FontSize = 12,
                                Color = "1.0 1.0 1.0 1.0",
                                Align = TextAnchor.MiddleCenter
                            }
                }, XPeriencePlayerControlPrimary);
            }
            else
            {
                ControlPanelelements.Add(new CuiButton
                {
                    Button =
                            {
                                Command = $"xp.resetstats {player.UserIDString}",
                                Color = "1.0 0.0 0.0 1.0"
                            },
                    RectTransform =
                            {
                                    AnchorMin = "0.31 0.95",
                                    AnchorMax = "0.45 0.99"
                            },
                    Text =
                            {
                                Text = $"{XPLang("resetstatsbutton", player.UserIDString)}",
                                FontSize = 12,
                                Color = "1.0 1.0 1.0 1.0",
                                Align = TextAnchor.MiddleCenter
                            }
                }, XPeriencePlayerControlPrimary);
            }
            // Fix Data Button
            ControlPanelelements.Add(new CuiButton
            {
                Button =
                {
                    Command = "xp.help 7",
                    Color = "1.0 0.0 0.0 1.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.50 0.95",
                    AnchorMax = "0.60 0.99"
                },
                Text =
                {
                    Text = $"{XPLang("playerfixdatabutton", player.UserIDString)}",
                    FontSize = 12,
                    Color = "1.0 1.0 1.0 1.0",
                    Align = TextAnchor.MiddleCenter
                }
            }, XPeriencePlayerControlPrimary);
            // Reset Skills Button
            DateTime resettimeskills = xprecord.resettimerskills.AddMinutes(config.defaultOptions.resetminsskills);
            TimeSpan skillinterval = resettimeskills - DateTime.Now;
            int skilltimer = (int)skillinterval.TotalMinutes;
            if (config.defaultOptions.bypassadminreset && player.IsAdmin && permission.UserHasPermission(player.UserIDString, XPerience.Admin))
            {
                skilltimer = 0;
            }
            if (skilltimer > 0 && config.defaultOptions.restristresets)
            {
                ControlPanelelements.Add(new CuiButton
                {
                    Button =
                        {
                            Command = $"xp.resetskills {player.UserIDString}",
                            Color = "1.0 0.0 0.0 1.0"
                        },
                    RectTransform =
                        {
                                AnchorMin = "0.685 0.95",
                                AnchorMax = "0.815 0.99"
                        },
                    Text =
                        {
                            Text = $"{XPLang("canresetskills", player.UserIDString, skilltimer)}",
                            FontSize = 12,
                            Color = "1.0 1.0 1.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                }, XPeriencePlayerControlPrimary);
            }
            else
            {
                ControlPanelelements.Add(new CuiButton
                {
                    Button =
                            {
                                Command = $"xp.resetskills {player.UserIDString}",
                                Color = "1.0 0.0 0.0 1.0"
                            },
                    RectTransform =
                            {
                                    AnchorMin = "0.685 0.95",
                                    AnchorMax = "0.815 0.99"
                            },
                    Text =
                            {
                                Text = $"{XPLang("resetskillsbutton", player.UserIDString)}",
                                FontSize = 12,
                                Color = "1.0 1.0 1.0 1.0",
                                Align = TextAnchor.MiddleCenter
                            }
                }, XPeriencePlayerControlPrimary);
            }
            // Top Label
            ControlPanelelements.Add(XPUILabel("ⓍⓅerience", 1, 0.060f, TextAnchor.MiddleLeft, 20, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlPrimary);
            // Main - Player Info
            int statpoints = xprecord.MentalityP + xprecord.DexterityP + xprecord.MightP + xprecord.ChemistP;
            int skillpoints = xprecord.WoodCutterP + xprecord.SmithyP + xprecord.MinerP + xprecord.ForagerP + xprecord.HunterP + xprecord.FisherP + xprecord.CrafterP + xprecord.FramerP + xprecord.TamerP;
            // XP Bar Calulations
            double levelpercent = 0;
            if (xprecord.experience == 0 || xprecord.level == 0)
            {
                levelpercent = ((xprecord.experience - 0) / config.xpLevel.levelstart) * 100;
            }
            else
            {
                levelpercent = ((xprecord.experience - (xprecord.requiredxp - (xprecord.level * config.xpLevel.levelmultiplier))) / (xprecord.requiredxp - (xprecord.requiredxp - (xprecord.level * config.xpLevel.levelmultiplier)))) * 100;
            }

            ControlPanelelements.Add(XPUIPanel("0.0 0.0", "0.25 0.93", "0.0 0.0 0.0 0.95"), XPeriencePlayerControlPrimary, XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{player.displayName}", 1, height, TextAnchor.MiddleCenter, 15, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 2, height, TextAnchor.MiddleCenter, 9, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{XPLang("level", player.UserIDString)}: <color={TextColor("mainlevel", (int)xprecord.level)}>{xprecord.level} ({(int)levelpercent}%)</color>", 3, height, TextAnchor.MiddleLeft, 13, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{XPLang("experience", player.UserIDString)}: <color={TextColor("experience", (int)xprecord.experience)}>{(int)xprecord.experience}</color>", 4, height, TextAnchor.MiddleLeft, 13, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{XPLang("nextlevel", player.UserIDString)}: <color={TextColor("nextlevel", (int)xprecord.requiredxp)}>{(int)xprecord.requiredxp}</color> (<color={TextColor("remainingxp", (int)(xprecord.requiredxp - xprecord.experience))}>{(int)(xprecord.requiredxp - xprecord.experience)}</color>)", 5, height, TextAnchor.MiddleLeft, 13, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 6, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{XPLang("unusedstatpoints", player.UserIDString)}: <color={TextColor("unspent", xprecord.statpoint)}>{xprecord.statpoint}</color>", 7, height, TextAnchor.MiddleLeft, 13, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{XPLang("unusedskillpoints", player.UserIDString)}: <color={TextColor("unspent", xprecord.skillpoint)}>{xprecord.skillpoint}</color>", 8, height, TextAnchor.MiddleLeft, 13, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 9, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{XPLang("totalspent", player.UserIDString)}: <color={TextColor("spent", statpoints + skillpoints)}>{statpoints + skillpoints}</color>", 10, height, TextAnchor.MiddleLeft, 13, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            if (config.mentality.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("mentality", player.UserIDString)}: <color={TextColor("spent", xprecord.MentalityP)}>{xprecord.MentalityP}</color>", 11, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.dexterity.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("dexterity", player.UserIDString)}: <color={TextColor("spent", xprecord.DexterityP)}>{xprecord.DexterityP}</color>", 12, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.might.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("might", player.UserIDString)}: <color={TextColor("spent", xprecord.MightP)}>{xprecord.MightP}</color>", 13, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            /*
            if (config.chemist.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("chemist", player.UserIDString)}: <color={TextColor("spent", xprecord.ChemistP)}>{xprecord.ChemistP}</color>", 14, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            */
            if (config.woodcutter.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("woodcutter", player.UserIDString)}: <color={TextColor("spent", xprecord.WoodCutterP)}>{xprecord.WoodCutterP}</color>", 15, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.smithy.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("smithy", player.UserIDString)}: <color={TextColor("spent", xprecord.SmithyP)}>{xprecord.SmithyP}</color>", 16, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.miner.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("miner", player.UserIDString)}: <color={TextColor("spent", xprecord.MinerP)}>{xprecord.MinerP}</color>", 17, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.forager.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("forager", player.UserIDString)}: <color={TextColor("spent", xprecord.ForagerP)}>{xprecord.ForagerP}</color>", 18, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.hunter.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("hunter", player.UserIDString)}: <color={TextColor("spent", xprecord.HunterP)}>{xprecord.HunterP}</color>", 19, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.crafter.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("crafter", player.UserIDString)}: <color={TextColor("spent", xprecord.CrafterP)}>{xprecord.CrafterP}</color>", 20, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.framer.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("framer", player.UserIDString)}: <color={TextColor("spent", xprecord.FramerP)}>{xprecord.FramerP}</color>", 21, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.fisher.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("fisher", player.UserIDString)}: <color={TextColor("spent", xprecord.FisherP)}>{xprecord.FisherP}</color>", 22, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.medic.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("medic", player.UserIDString)}: <color={TextColor("spent", xprecord.MedicP)}>{xprecord.MedicP}</color>", 23, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.tamer.enabletame)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("tamer", player.UserIDString)}: <color={TextColor("spent", xprecord.TamerP)}>{xprecord.TamerP}</color>", 24, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            //Live UI Location Selection
            if (config.defaultOptions.liveuistatslocationmoveable)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("liveuiselection", player.UserIDString)}:", 26, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
                // UI Off
                ControlPanelelements.Add(new CuiButton
                {
                    Button =
                        {
                            Command = $"xp.liveui 0",
                            Color = $"{TextColor("UI0", xprecord.UILocation)}"
                        },
                    RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*27 + 27 * .002f}",
                    AnchorMax = $"0.182 {1 - height*(27-1) + 27 * .002f}"
                        },
                    Text =
                        {
                            Text = "Off",
                            FontSize = 11,
                            Color = "1.0 1.0 1.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                }, XPeriencePlayerControlMain);
                // UI 1
                ControlPanelelements.Add(new CuiButton
                {
                    Button =
                        {
                            Command = $"xp.liveui 1",
                            Color = $"{TextColor("UI1", xprecord.UILocation)}"
                        },
                    RectTransform =
                        {
                    AnchorMin = $"0.21 {1 - height*27 + 27 * .002f}",
                    AnchorMax = $"0.282 {1 - height*(27-1) + 27 * .002f}"
                        },
                    Text =
                        {
                            Text = "1",
                            FontSize = 11,
                            Color = "1.0 1.0 1.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                }, XPeriencePlayerControlMain);
                // UI 2
                ControlPanelelements.Add(new CuiButton
                {
                    Button =
                        {
                            Command = $"xp.liveui 2",
                            Color = $"{TextColor("UI2", xprecord.UILocation)}"
                        },
                    RectTransform =
                        {
                    AnchorMin = $"0.31 {1 - height*27 + 27 * .002f}",
                    AnchorMax = $"0.382 {1 - height*(27-1) + 27 * .002f}"
                        },
                    Text =
                        {
                            Text = "2",
                            FontSize = 11,
                            Color = "1.0 1.0 1.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                }, XPeriencePlayerControlMain);
                // UI 3
                ControlPanelelements.Add(new CuiButton
                {
                    Button =
                        {
                            Command = $"xp.liveui 3",
                            Color = $"{TextColor("UI3", xprecord.UILocation)}"
                        },
                    RectTransform =
                        {
                    AnchorMin = $"0.41 {1 - height*27 + 27 * .002f}",
                    AnchorMax = $"0.482 {1 - height*(27-1) + 27 * .002f}"
                        },
                    Text =
                        {
                            Text = "3",
                            FontSize = 11,
                            Color = "1.0 1.0 1.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                }, XPeriencePlayerControlMain);
                // UI 4
                ControlPanelelements.Add(new CuiButton
                {
                    Button =
                        {
                            Command = $"xp.liveui 4",
                            Color = $"{TextColor("UI4", xprecord.UILocation)}"
                        },
                    RectTransform =
                        {
                    AnchorMin = $"0.51 {1 - height*27 + 27 * .002f}",
                    AnchorMax = $"0.582 {1 - height*(27-1) + 27 * .002f}"
                        },
                    Text =
                        {
                            Text = "4",
                            FontSize = 11,
                            Color = "1.0 1.0 1.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                }, XPeriencePlayerControlMain);
                // UI 5
                ControlPanelelements.Add(new CuiButton
                {
                    Button =
                        {
                            Command = $"xp.liveui 5",
                            Color = $"{TextColor("UI5", xprecord.UILocation)}"
                        },
                    RectTransform =
                        {
                    AnchorMin = $"0.61 {1 - height*27 + 27 * .002f}",
                    AnchorMax = $"0.682 {1 - height*(27-1) + 27 * .002f}"
                        },
                    Text =
                        {
                            Text = "5",
                            FontSize = 11,
                            Color = "1.0 1.0 1.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                }, XPeriencePlayerControlMain);
            }
            // Primary - Stats
            ControlPanelelements.Add(XPUIPanel("0.26 0.0", "0.50 0.93", "0.0 0.0 0.0 0.95"), XPeriencePlayerControlPrimary, XPeriencePlayerControlStats);
            ControlPanelelements.Add(XPUILabel($"{XPLang("stats", player.UserIDString)}", 1, height, TextAnchor.MiddleCenter, 15, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 2, height, TextAnchor.MiddleCenter, 9, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            // Mentality
            if (config.mentality.maxlvl != 0)
            {
                if (xprecord.Mentality < config.mentality.maxlvl)
                {
                    ControlPanelelements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"xp.addstat mentality {player.UserIDString}",
                            Color = "0.3 0.3 0.3 0.0"
                        },
                        RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*3 + 3 * .002f}",
                    AnchorMax = $"0.072 {1 - height*(3-1) + 3 * .002f}"
                        },
                        Text =
                        {
                            Text = "+",
                            FontSize = 17,
                            Color = "0.0 1.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                    }, XPeriencePlayerControlStats);
                }
                ControlPanelelements.Add(XPUILabel($"{XPLang("mentality", player.UserIDString)}: <color={TextColor("level", xprecord.Mentality)}>{xprecord.Mentality}</color>", 3, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("researchcost", player.UserIDString)}: <color={TextColor("perk", xprecord.Mentality)}>-{(xprecord.Mentality * config.mentality.researchcost) * 100}%</color>", 4, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("researchspeed", player.UserIDString)}: <color={TextColor("perk", xprecord.Mentality)}>-{(xprecord.Mentality * config.mentality.researchspeed) * 100}%</color>", 5, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("critchance", player.UserIDString)}:  <color={TextColor("perk", xprecord.Mentality)}>{(xprecord.Mentality * config.mentality.criticalchance) * 100}%</color>", 6, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 7, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            // Dexterity
            if (config.dexterity.maxlvl != 0)
            {
                if (xprecord.Dexterity < config.dexterity.maxlvl)
                {
                    ControlPanelelements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"xp.addstat dexterity {player.UserIDString}",
                            Color = "0.3 0.3 0.3 0.0"
                        },
                        RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*8 + 8 * .002f}",
                    AnchorMax = $"0.072 {1 - height*(8-1) + 8 * .002f}"
                        },
                        Text =
                        {
                            Text = "+",
                            FontSize = 17,
                            Color = "0.0 1.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                    }, XPeriencePlayerControlStats);
                }
                ControlPanelelements.Add(XPUILabel($"{XPLang("dexterity", player.UserIDString)}: <color={TextColor("level", xprecord.Dexterity)}>{xprecord.Dexterity}</color>", 8, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("blockchance", player.UserIDString)}: <color={TextColor("perk", xprecord.Dexterity)}>{(xprecord.Dexterity * config.dexterity.blockchance) * 100}%</color> (Damage: <color={TextColor("perk", xprecord.Dexterity)}>-{(xprecord.Dexterity * config.dexterity.blockamount) * 100}%</color>)", 9, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("dodgechance", player.UserIDString)}: <color={TextColor("perk", xprecord.Dexterity)}>{(xprecord.Dexterity * config.dexterity.dodgechance) * 100}%</color>", 10, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("armordmgabsorb", player.UserIDString)}: <color={TextColor("perk", xprecord.Dexterity)}>{(xprecord.Dexterity * config.dexterity.reducearmordmg) * 100}%</color>", 11, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 12, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            // Might
            if (config.might.maxlvl != 0)
            {
                if (xprecord.Might < config.might.maxlvl)
                {
                    ControlPanelelements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"xp.addstat might {player.UserIDString}",
                            Color = "0.3 0.3 0.3 0.0"
                        },
                        RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*13 + 13 * .002f}",
                    AnchorMax = $"0.072 {1 - height*(13-1) + 13 * .002f}"
                        },
                        Text =
                        {
                            Text = "+",
                            FontSize = 17,
                            Color = "0.0 1.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                    }, XPeriencePlayerControlStats);
                }
                ControlPanelelements.Add(XPUILabel($"{XPLang("might", player.UserIDString)}: <color={TextColor("level", xprecord.Might)}>{xprecord.Might}</color>", 13, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("armor", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>{(xprecord.Might * config.might.armor) * 100}</color>", 14, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("melee", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>+{(xprecord.Might * config.might.meleedmg) * 100}%</color>", 15, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("calories", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>+{(int)((config.might.metabolism * xprecord.Might) * player.metabolism.calories.max)}</color>", 16, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("hydration", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>+{(int)((config.might.metabolism * xprecord.Might) * player.metabolism.hydration.max)}</color>", 17, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("bleed", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>-{(config.might.bleedreduction * xprecord.Might) * 100}%</color>", 18, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("radiation", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>-{(config.might.radreduction * xprecord.Might) * 100}%</color>", 19, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("heat", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>+{(config.might.heattolerance * xprecord.Might) * 100}%</color>", 20, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("cold", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>+{(config.might.coldtolerance * xprecord.Might) * 100}%</color>", 21, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                //ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("heat", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>(disabled)</color>", 20, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                //ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("cold", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>(disabled)</color>", 21, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 22, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            // Chemist
            /*
            if (config.chemist.maxlvl != 0)
            {
                if (xprecord.Chemist < config.chemist.maxlvl && config.chemist.maxlvl != 0)
                {
                    ControlPanelelements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"xp.addstat chemist {player.UserIDString}",
                            Color = "0.3 0.3 0.3 0.0"
                        },
                        RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*23 + 23 * .002f}",
                    AnchorMax = $"0.072 {1 - height*(23-1) + 23 * .002f}"
                        },
                        Text =
                        {
                            Text = "+",
                            FontSize = 17,
                            Color = "0.0 1.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                    }, XPeriencePlayerControlStats);
                }
                ControlPanelelements.Add(XPUILabel($"{XPLang("chemist", player.UserIDString)}: <color={TextColor("level", xprecord.Chemist)}>{xprecord.Chemist}</color>", 23, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫</color> {XPLang("craftspeed", player.UserIDString)}: <color={TextColor("perk", xprecord.Chemist)}>-{(xprecord.Chemist * config.chemist.crafttime) * 100}%</color>", 24, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            }
            */
            // Kill Records Button
            if(KillRecords != null && config.xpBonus.showkrbutton)
            {
                ControlPanelelements.Add(new CuiButton
                {
                    Button =
                        {
                            Command = $"xp.killrecords {player.UserIDString}",
                            Color = "0.2 0.2 0.2 1.0"
                        },
                    RectTransform =
                        {
                    AnchorMin = $"0.20 {1 - height*25 + 25 * .002f}",
                    AnchorMax = $"0.80 {1 - height*(25-1) + 25 * .002f}"
                        },
                    Text =
                        {
                            Text = "View Kill Records",
                            FontSize = 15,
                            Color = "1.0 0.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                }, XPeriencePlayerControlStats);
            }
            // Secondary - Skills
            ControlPanelelements.Add(XPUIPanel("0.51 0.0", "1.0 0.93", "0.0 0.0 0.0 0.95"), XPeriencePlayerControlPrimary, XPeriencePlayerControlSkills);
            ControlPanelelements.Add(XPUILabel($"{XPLang("skills", player.UserIDString)}", 1, height, TextAnchor.MiddleCenter, 15, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkills);
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 2, height, TextAnchor.MiddleCenter, 9, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkills);
            // Left Column
            ControlPanelelements.Add(XPUIPanel("0.0 0.0", "0.5 0.915", "0.0 0.0 0.0 0.0"), XPeriencePlayerControlSkills, XPeriencePlayerControlSkillsL);
            // WoodCutter
            if (config.woodcutter.maxlvl != 0)
            {
                if (xprecord.WoodCutter < config.woodcutter.maxlvl)
                {
                    ControlPanelelements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"xp.addskill woodcutter {player.UserIDString}",
                            Color = "0.3 0.3 0.3 0.0"
                        },
                        RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*1 + 1 * .002f}",
                    AnchorMax = $"0.072 {1 - height*(1-1) + 1 * .002f}"
                        },
                        Text =
                        {
                            Text = "+",
                            FontSize = 17,
                            Color = "0.0 1.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                    }, XPeriencePlayerControlSkillsL);
                }
                ControlPanelelements.Add(XPUILabel($"{XPLang("woodcutter", player.UserIDString)}: <color={TextColor("level", xprecord.WoodCutter)}>{xprecord.WoodCutter}</color>", 1, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("woodgather", player.UserIDString)}: <color={TextColor("perk", xprecord.WoodCutter)}>+{(xprecord.WoodCutter * config.woodcutter.gatherrate)}x</color>", 2, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("woodbonus", player.UserIDString)}: <color={TextColor("perk", xprecord.WoodCutter)}>+{(xprecord.WoodCutter * config.woodcutter.bonusincrease)}x</color>", 3, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("woodapple", player.UserIDString)}: <color={TextColor("perk", xprecord.WoodCutter)}>{(xprecord.WoodCutter * config.woodcutter.applechance) * 100}%</color>", 4, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 5, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            // Smithy
            if (config.smithy.maxlvl != 0)
            {
                if (xprecord.Smithy < config.smithy.maxlvl)
                {
                    ControlPanelelements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"xp.addskill smithy {player.UserIDString}",
                            Color = "0.3 0.3 0.3 0.0"
                        },
                        RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*6 + 6 * .002f}",
                    AnchorMax = $"0.072 {1 - height*(6-1) + 6 * .002f}"
                        },
                        Text =
                        {
                            Text = "+",
                            FontSize = 17,
                            Color = "0.0 1.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                    }, XPeriencePlayerControlSkillsL);
                }
                ControlPanelelements.Add(XPUILabel($"{XPLang("smithy", player.UserIDString)}: <color={TextColor("level", xprecord.Smithy)}>{xprecord.Smithy}</color>", 6, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("productionrate", player.UserIDString)}: <color={TextColor("perk", xprecord.Smithy)}>{(xprecord.Smithy * config.smithy.productionrate) * 100}%</color> ({XPLang("productionamount", player.UserIDString)}: <color={TextColor("perk", xprecord.Smithy)}>+{Math.Round((xprecord.Smithy * config.smithy.productionrate) * 5)}</color>)", 7, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("fuelconsumption", player.UserIDString)}: - <color={TextColor("perk", xprecord.Smithy)}>{(xprecord.Smithy * config.smithy.fuelconsumption) * 100}%</color>", 8, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 9, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            // Miner
            if (config.miner.maxlvl != 0)
            {
                if (xprecord.Miner < config.miner.maxlvl)
                {
                    ControlPanelelements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"xp.addskill miner {player.UserIDString}",
                            Color = "0.3 0.3 0.3 0.0"
                        },
                        RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*10 + 10 * .002f}",
                    AnchorMax = $"0.072 {1 - height*(10-1) + 10 * .002f}"
                        },
                        Text =
                        {
                            Text = "+",
                            FontSize = 17,
                            Color = "0.0 1.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                    }, XPeriencePlayerControlSkillsL);
                }
                ControlPanelelements.Add(XPUILabel($"{XPLang("miner", player.UserIDString)}: <color={TextColor("level", xprecord.Miner)}>{xprecord.Miner}</color>", 10, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("oregather", player.UserIDString)}: <color={TextColor("perk", xprecord.Miner)}>+{(xprecord.Miner * config.miner.gatherrate)}x</color>", 11, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("orebonus", player.UserIDString)}: <color={TextColor("perk", xprecord.Miner)}>+{(xprecord.Miner * config.miner.bonusincrease)}x</color>", 12, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("fuelconsumptionhats", player.UserIDString)}: <color={TextColor("perk", xprecord.Miner)}>-{(xprecord.Miner * config.miner.fuelconsumption) * 100}%</color>", 13, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 14, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            // Forager
            if (config.forager.maxlvl != 0)
            {
                if (xprecord.Forager < config.forager.maxlvl)
                {
                    ControlPanelelements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"xp.addskill forager {player.UserIDString}",
                            Color = "0.3 0.3 0.3 0.0"
                        },
                        RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*15 + 15 * .002f}",
                    AnchorMax = $"0.072 {1 - height*(15-1) + 15 * .002f}"
                        },
                        Text =
                        {
                            Text = "+",
                            FontSize = 17,
                            Color = "0.0 1.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                    }, XPeriencePlayerControlSkillsL);
                }
                ControlPanelelements.Add(XPUILabel($"{XPLang("forager", player.UserIDString)}: <color={TextColor("level", xprecord.Forager)}>{xprecord.Forager}</color>", 15, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("gather", player.UserIDString)}: <color={TextColor("perk", xprecord.Forager)}>+{(xprecord.Forager * config.forager.gatherrate)}x</color>", 16, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("seedbonus", player.UserIDString)}: <color={TextColor("perk", xprecord.Forager)}>{(config.forager.chanceincrease * xprecord.Forager) * 100}%</color> (Amount: <color={TextColor("perk", xprecord.Forager)}>{(config.forager.chanceincrease * xprecord.Forager) * 10}</color>)", 17, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("randomitem", player.UserIDString)}: <color={TextColor("perk", xprecord.Forager)}>{(xprecord.Forager * config.forager.randomchance) * 100}%</color>", 18, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 19, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            // Hunter
            if (config.hunter.maxlvl != 0)
            {
                if (xprecord.Hunter < config.hunter.maxlvl)
                {
                    ControlPanelelements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"xp.addskill hunter {player.UserIDString}",
                            Color = "0.3 0.3 0.3 0.0"
                        },
                        RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*20 + 20 * .002f}",
                    AnchorMax = $"0.072 {1 - height*(20-1) + 20 * .002f}"
                        },
                        Text =
                        {
                            Text = "+",
                            FontSize = 17,
                            Color = "0.0 1.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                    }, XPeriencePlayerControlSkillsL);
                }
                ControlPanelelements.Add(XPUILabel($"{XPLang("hunter", player.UserIDString)}: <color={TextColor("level", xprecord.Hunter)}>{xprecord.Hunter}</color>", 20, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("foodgather", player.UserIDString)}: <color={TextColor("perk", xprecord.Hunter)}>+{(xprecord.Hunter * config.hunter.gatherrate)}x</color>", 21, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("bonusgather", player.UserIDString)}: <color={TextColor("perk", xprecord.Hunter)}>+{(xprecord.Hunter * config.hunter.bonusincrease)}x</color>", 22, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("damagewildlife", player.UserIDString)}: <color={TextColor("perk", xprecord.Hunter)}>+{(xprecord.Hunter * config.hunter.damageincrease) * 100}%</color>", 23, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("nightdamage", player.UserIDString)}: <color={TextColor("perk", xprecord.Hunter)}>+{(xprecord.Hunter * config.hunter.nightdmgincrease) * 100}%</color>", 24, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            }
            // Right Column
            ControlPanelelements.Add(XPUIPanel("0.5 0.0", "1.0 0.915", "0.0 0.0 0.0 0.0"), XPeriencePlayerControlSkills, XPeriencePlayerControlSkillsR);
            // Crafter
            if (config.crafter.maxlvl != 0)
            {
                if (xprecord.Crafter < config.crafter.maxlvl)
                {
                    ControlPanelelements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"xp.addskill crafter {player.UserIDString}",
                            Color = "0.3 0.3 0.3 0.0"
                        },
                        RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*1 + 1 * .002f}",
                    AnchorMax = $"0.072 {1 - height*(1-1) + 1 * .002f}"
                        },
                        Text =
                        {
                            Text = "+",
                            FontSize = 17,
                            Color = "0.0 1.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                    }, XPeriencePlayerControlSkillsR);
                }
                ControlPanelelements.Add(XPUILabel($"{XPLang("crafter", player.UserIDString)}: <color={TextColor("level", xprecord.Crafter)}>{xprecord.Crafter}</color>", 1, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("craftspeed", player.UserIDString)}: <color={TextColor("perk", xprecord.Crafter)}>-{(config.crafter.craftspeed * xprecord.Crafter) * 100}%</color>", 2, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("costreduction", player.UserIDString)}: <color={TextColor("perk", xprecord.Crafter)}>-{(xprecord.Crafter * config.crafter.craftcost) * 100}%</color>", 3, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("fullrepair", player.UserIDString)}: <color={TextColor("perk", xprecord.Crafter)}>{(xprecord.Crafter * config.crafter.repairincrease) * 100}%</color>", 4, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("repaircost", player.UserIDString)}: <color={TextColor("perk", xprecord.Crafter)}>-{(xprecord.Crafter * config.crafter.repaircost) * 100}%</color>", 5, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("highcond", player.UserIDString)}: <color={TextColor("perk", xprecord.Crafter)}>{(config.crafter.conditionchance * xprecord.Crafter) * 100}%</color> (<color={TextColor("perk", xprecord.Crafter)}>+{config.crafter.conditionamount * 100}%</color> Condition)", 6, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 7, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            // Framer
            if (config.framer.maxlvl != 0)
            {
                if (xprecord.Framer < config.framer.maxlvl)
                {
                    ControlPanelelements.Add(new CuiButton
                    {
                        Button =
                            {
                                Command = $"xp.addskill framer {player.UserIDString}",
                            Color = "0.3 0.3 0.3 0.0"
                        },
                        RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*8 + 8 * .002f}",
                    AnchorMax = $"0.072 {1 - height*(8-1) + 8 * .002f}"
                        },
                        Text =
                        {
                            Text = "+",
                            FontSize = 17,
                            Color = "0.0 1.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                    }, XPeriencePlayerControlSkillsR);
                }
                ControlPanelelements.Add(XPUILabel($"{XPLang("framer", player.UserIDString)}: <color={TextColor("level", xprecord.Framer)}>{xprecord.Framer}</color>", 8, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("upgradecost", player.UserIDString)}: <color={TextColor("perk", xprecord.Framer)}>-{(config.framer.upgradecost * xprecord.Framer) * 100}%</color>", 9, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("repairtime", player.UserIDString)}: - <color={TextColor("perk", xprecord.Framer)}>{(xprecord.Framer * config.framer.repairtime) * 100}%</color>", 10, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("repaircost", player.UserIDString)}: - <color={TextColor("perk", xprecord.Framer)}>{(xprecord.Framer * config.framer.repaircost) * 100}%</color>", 11, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 12, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            // Fisher
            if (config.fisher.maxlvl != 0)
            {
                if (xprecord.Fisher < config.fisher.maxlvl)
                {
                    ControlPanelelements.Add(new CuiButton
                    {
                        Button =
                            {
                                Command = $"xp.addskill fisher {player.UserIDString}",
                            Color = "0.3 0.3 0.3 0.0"
                        },
                        RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*13 + 13 * .002f}",
                    AnchorMax = $"0.072 {1 - height*(13-1) + 13 * .002f}"
                        },
                        Text =
                        {
                            Text = "+",
                            FontSize = 17,
                            Color = "0.0 1.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                    }, XPeriencePlayerControlSkillsR);
                }
                ControlPanelelements.Add(XPUILabel($"{XPLang("fisher", player.UserIDString)}: <color={TextColor("level", xprecord.Fisher)}>{xprecord.Fisher}</color>", 13, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("fishamount", player.UserIDString)}: <color={TextColor("perk", xprecord.Fisher)}>+{Math.Round(xprecord.Fisher * config.fisher.fishamountincrease)}</color>", 14, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("fishitems", player.UserIDString)}: <color={TextColor("perk", xprecord.Fisher)}>+{Math.Round(xprecord.Fisher * config.fisher.itemamountincrease)}</color>", 15, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 16, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            // Medic
            if (config.medic.maxlvl != 0)
            {
                if (xprecord.Medic < config.medic.maxlvl)
                {
                    ControlPanelelements.Add(new CuiButton
                    {
                        Button =
                            {
                                Command = $"xp.addskill medic {player.UserIDString}",
                            Color = "0.3 0.3 0.3 0.0"
                        },
                        RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*17 + 17 * .002f}",
                    AnchorMax = $"0.072 {1 - height*(17-1) + 17 * .002f}"
                        },
                        Text =
                        {
                            Text = "+",
                            FontSize = 17,
                            Color = "0.0 1.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                    }, XPeriencePlayerControlSkillsR);
                }
                ControlPanelelements.Add(XPUILabel($"{XPLang("medic", player.UserIDString)}: <color={TextColor("level", xprecord.Medic)}>{xprecord.Medic}</color>", 17, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("medicrevive", player.UserIDString)}: <color={TextColor("perk", xprecord.Medic)}>+{Math.Round(xprecord.Medic * config.medic.recoverhp)}</color>", 18, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("medicrecover", player.UserIDString)}: <color={TextColor("perk", xprecord.Medic)}>+{Math.Round(xprecord.Medic * config.medic.revivehp)}</color>", 19, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("medictools", player.UserIDString)}: <color={TextColor("perk", xprecord.Medic)}>+{Math.Round(xprecord.Medic * config.medic.tools)}</color>", 20, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("mediccrafting", player.UserIDString)}: <color={TextColor("perk", xprecord.Medic)}>+{Math.Round((xprecord.Medic * config.medic.crafttime) * 100)}%</color>", 21, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 22, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            // Tamer
            if (config.tamer.enabletame)
            {
                if (xprecord.Tamer < config.tamer.maxlvl)
                {
                    ControlPanelelements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"xp.addskill tamer {player.UserIDString}",
                            Color = "0.3 0.3 0.3 0.0"
                        },
                        RectTransform =
                        {
                    AnchorMin = $"0.01 {1 - height*23 + 23 * .002f}",
                    AnchorMax = $"0.072 {1 - height*(23-1) + 23 * .002f}"
                        },
                        Text =
                        {
                            Text = "+",
                            FontSize = 17,
                            Color = "0.0 1.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                    }, XPeriencePlayerControlSkillsR);
                }
                ControlPanelelements.Add(XPUILabel($"{XPLang("tamer", player.UserIDString)}: <color={TextColor("level", xprecord.Tamer)}>{xprecord.Tamer}</color>", 23, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                if (xprecord.Tamer == 0)
                {
                    ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("tamerinc", player.UserIDString)}", 24, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                }
                if (xprecord.Tamer > 0)
                {
                    ControlPanelelements.Add(XPUILabel($"{XPLang("tamerpets", player.UserIDString)}:", 24, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                }
                if (xprecord.Tamer >= config.tamer.chickenlevel)
                {
                    ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> <color={TextColor("pets", xprecord.Tamer)}>{XPLang("chicken", player.UserIDString)}</color>", 25, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                }
                if (xprecord.Tamer >= config.tamer.boarlevel)
                {
                    ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> <color={TextColor("pets", xprecord.Tamer)}>{XPLang("boar", player.UserIDString)}</color>", 26, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                }
                if (xprecord.Tamer >= config.tamer.staglevel)
                {
                    ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> <color={TextColor("pets", xprecord.Tamer)}>{XPLang("stag", player.UserIDString)}</color>", 27, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                }
                if (xprecord.Tamer >= config.tamer.wolflevel)
                {
                    ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> <color={TextColor("pets", xprecord.Tamer)}>{XPLang("wolf", player.UserIDString)}</color>", 28, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                }
                if (xprecord.Tamer >= config.tamer.bearlevel)
                {
                    ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> <color={TextColor("pets", xprecord.Tamer)}>{XPLang("bear", player.UserIDString)}</color>", 29, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                }
            }
            // UI End
            CuiHelper.AddUi(player, ControlPanelelements);
            return;
        }

        private void XperienceTop(BasePlayer player, string info)
        {
            if (player == null) return;
            if (info == null)
            {
                info = "level";
            }
            var ControlPanelelements = new CuiElementContainer();
            var height = 0.084f;
            var selectionheight = 0.057f;
            var selectionspacer = -1f;
            var vals = GetTopXP(0, 10, info);
            if (vals == null) { return; }
            var index = 0;
            // Main UI
            ControlPanelelements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.0 0.0 0.0 0.9"
                },
                RectTransform =
                {
                    AnchorMin = "0.75 0.35",
                    AnchorMax = "0.995 0.85"
                },
                CursorEnabled = true
            }, "Overlay", XPerienceTopMain);
            // Close Button
            ControlPanelelements.Add(new CuiButton
            {
                Button =
                {
                    Close = XPerienceTopMain,
                    Color = "0.0 0.0 0.0 0.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.87 0.93",
                    AnchorMax = "1.0 1.002"
                },
                Text =
                {
                    Text = "ⓧ",
                    FontSize = 20,
                    Color = "1.0 0.0 0.0",
                    Align = TextAnchor.MiddleCenter
                }
            }, XPerienceTopMain);
            // Main UI Label
            ControlPanelelements.Add(XPUILabel($"ⓍⓅerience {XPLang("topplayers", player.UserIDString)}", 1, height, TextAnchor.MiddleCenter, 19, "0.03", "0.85", "1.0 1.0 1.0 1.0"), XPerienceTopMain);
            // Selections UI
            ControlPanelelements.Add(XPUIPanel("0.01 0.00", "0.35 0.9"), XPerienceTopMain, XPerienceTopSelection);
            var selected = "➤";
            var dcolor = "0.0 0.0 0.0 0.7";
            var scolor = "1.0 0.0 0.0 0.7";

            if (info == "level")
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp level", 1, selectionheight, 11, scolor, $"{selected} {XPLang("level", player.UserIDString)}"), XPerienceTopSelection);
            }
            else
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp level", 1, selectionheight, 11, dcolor, $" {XPLang("level", player.UserIDString)}"), XPerienceTopSelection);
            }
            if (info == "mentality")
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp mentality", 2, selectionheight, 11, scolor, $"{selected} {XPLang("mentality", player.UserIDString)}"), XPerienceTopSelection);
            }
            else
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp mentality", 2, selectionheight, 11, dcolor, $" {XPLang("mentality", player.UserIDString)}"), XPerienceTopSelection);
            }
            if (info == "dexterity")
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp dexterity", 3, selectionheight, 11, scolor, $"{selected} {XPLang("dexterity", player.UserIDString)}"), XPerienceTopSelection);
            }
            else
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp dexterity", 3, selectionheight, 11, dcolor, $" {XPLang("dexterity", player.UserIDString)}"), XPerienceTopSelection);
            }
            if (info == "might")
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp might", 4, selectionheight, 11, scolor, $"{selected} {XPLang("might", player.UserIDString)}"), XPerienceTopSelection);
            }
            else
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp might", 4, selectionheight, 11, dcolor, $" {XPLang("might", player.UserIDString)}"), XPerienceTopSelection);
            }
            if (info == "chemist")
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp chemist", 5, selectionheight, 11, scolor, $"{selected} {XPLang("chemist", player.UserIDString)}"), XPerienceTopSelection);
            }
            else
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp chemist", 5, selectionheight, 11, dcolor, $" {XPLang("chemist", player.UserIDString)}"), XPerienceTopSelection);
            }
            if (info == "woodcutter")
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp woodcutter", 6, selectionheight, 11, scolor, $"{selected} {XPLang("woodcutter", player.UserIDString)}"), XPerienceTopSelection);
            }
            else
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp woodcutter", 6, selectionheight, 11, dcolor, $" {XPLang("woodcutter", player.UserIDString)}"), XPerienceTopSelection);
            }
            if (info == "smithy")
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp smithy", 7, selectionheight, 11, scolor, $"{selected} {XPLang("smithy", player.UserIDString)}"), XPerienceTopSelection);
            }
            else
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp smithy", 7, selectionheight, 11, dcolor, $" {XPLang("smithy", player.UserIDString)}"), XPerienceTopSelection);
            }
            if (info == "miner")
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp miner", 8, selectionheight, 11, scolor, $"{selected} {XPLang("miner", player.UserIDString)}"), XPerienceTopSelection);
            }
            else
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp miner", 8, selectionheight, 11, dcolor, $" {XPLang("miner", player.UserIDString)}"), XPerienceTopSelection);
            }
            if (info == "forager")
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp forager", 9, selectionheight, 11, scolor, $"{selected} {XPLang("forager", player.UserIDString)}"), XPerienceTopSelection);
            }
            else
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp forager", 9, selectionheight, 11, dcolor, $" {XPLang("forager", player.UserIDString)}"), XPerienceTopSelection);
            }
            if (info == "hunter")
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp hunter", 10, selectionheight, 11, scolor, $"{selected} {XPLang("hunter", player.UserIDString)}"), XPerienceTopSelection);
            }
            else
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp hunter", 10, selectionheight, 11, dcolor, $" {XPLang("hunter", player.UserIDString)}"), XPerienceTopSelection);
            }
            if (info == "fisher")
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp fisher", 11, selectionheight, 11, scolor, $"{selected} {XPLang("fisher", player.UserIDString)}"), XPerienceTopSelection);
            }
            else
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp fisher", 11, selectionheight, 11, dcolor, $" {XPLang("fisher", player.UserIDString)}"), XPerienceTopSelection);
            }
            if (info == "crafter")
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp crafter", 12, selectionheight, 11, scolor, $"{selected} {XPLang("crafter", player.UserIDString)}"), XPerienceTopSelection);
            }
            else
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp crafter", 12, selectionheight, 11, dcolor, $" {XPLang("crafter", player.UserIDString)}"), XPerienceTopSelection);
            }
            if (info == "framer")
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp framer", 13, selectionheight, 11, scolor, $"{selected} {XPLang("framer", player.UserIDString)}"), XPerienceTopSelection);
            }
            else
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp framer", 13, selectionheight, 11, dcolor, $" {XPLang("framer", player.UserIDString)}"), XPerienceTopSelection);
            }
            if (info == "medic")
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp medic", 14, selectionheight, 11, scolor, $"{selected} {XPLang("medic", player.UserIDString)}"), XPerienceTopSelection);
            }
            else
            {
                ControlPanelelements.Add(XPUIButton("xp.topxp medic", 14, selectionheight, 11, dcolor, $" {XPLang("medic", player.UserIDString)}"), XPerienceTopSelection);
            }
            if (config.tamer.enabletame)
            {
                if (info == "tamer")
                {
                    ControlPanelelements.Add(XPUIButton("xp.topxp tamer", 15, selectionheight, 11, scolor, $"{selected} {XPLang("tamer", player.UserIDString)}"), XPerienceTopSelection);
                }
                else
                {
                    ControlPanelelements.Add(XPUIButton("xp.topxp tamer", 15, selectionheight, 11, dcolor, $" {XPLang("tamer", player.UserIDString)}"), XPerienceTopSelection);
                }
            }

            // List UI
            ControlPanelelements.Add(XPUIPanel("0.40 0.09", "0.98 0.9"), XPerienceTopMain, XPerienceTopInner);
            // Inner UI Labels
            ControlPanelelements.Add(XPUILabel($"〖 {XPLang($"{info}", player.UserIDString)} 〗", 1, height, TextAnchor.MiddleCenter, 16), XPerienceTopInner);
            ControlPanelelements.Add(XPUILabel(("﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌"), 2, height, TextAnchor.MiddleCenter), XPerienceTopInner);
            int n = 0;
            for (int i = 3; i < 13; i++)
            {
                n++;
                if (vals.ElementAtOrDefault(index) == null)
                {
                    continue;
                }
                var playerdata = vals.ElementAtOrDefault(index);
                if (playerdata == null) continue;

                if (info == "level" && playerdata.level != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.06", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.level}", "0.03", "1"), XPerienceTopInner);
                }
                else if (info == "mentality" && playerdata.Mentality != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.Mentality}", "0.03", "1"), XPerienceTopInner);
                }
                else if (info == "dexterity" && playerdata.Dexterity != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.Dexterity}", "0.03", "1"), XPerienceTopInner);
                }
                else if (info == "might" && playerdata.Might != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.Might}", "0.03", "1"), XPerienceTopInner);
                }
                else if (info == "chemist" && playerdata.Chemist != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.Chemist}", "0.03", "1"), XPerienceTopInner);
                }
                else if (info == "woodcutter" && playerdata.WoodCutter != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.WoodCutter}", "0.03", "1"), XPerienceTopInner);
                }
                else if (info == "smithy" && playerdata.Smithy != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.Smithy}", "0.03", "1"), XPerienceTopInner);
                }
                else if (info == "miner" && playerdata.Miner != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.Miner}", "0.03", "1"), XPerienceTopInner);
                }
                else if (info == "forager" && playerdata.Forager != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.Forager}", "0.03", "1"), XPerienceTopInner);
                }
                else if (info == "hunter" && playerdata.Hunter != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.Hunter}", "0.03", "1"), XPerienceTopInner);
                }
                else if (info == "fisher" && playerdata.Fisher != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.Fisher}", "0.03", "1"), XPerienceTopInner);
                }
                else if (info == "crafter" && playerdata.Crafter != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.Crafter}", "0.03", "1"), XPerienceTopInner);
                }
                else if (info == "framer" && playerdata.Framer != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.Framer}", "0.03", "1"), XPerienceTopInner);
                }
                else if (info == "medic" && playerdata.Medic != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.Medic}", "0.03", "1"), XPerienceTopInner);
                }
                else if (info == "tamer" && playerdata.Tamer != 0)
                {
                    if (playerdata.displayname == _xperienceCache[player.UserIDString].displayname)
                    {
                        ControlPanelelements.Add(XPUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), XPerienceTopInner);
                    }
                    ControlPanelelements.Add(XPUIButton($"xp.player {playerdata.id}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.Tamer}", "0.03", "1"), XPerienceTopInner);
                }
                index++;
            }
            CuiHelper.AddUi(player, ControlPanelelements);
        }

        private void PlayerPanel(BasePlayer player, string info)
        {
            if (player == null || info == null) return;
            XPRecord xprecord = GetPlayerRecord(info);
            if (xprecord == null) return;
            float height = 0.037f;
            float buttonheight = 0.035f;
            var ControlPanelelements = new CuiElementContainer();
            // Main UI
            ControlPanelelements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.1 0.1 0.1 0.95"
                },
                RectTransform =
                {
                    AnchorMin = $"0.10 0.17",
                    AnchorMax = $"0.90 0.95"
                },
                CursorEnabled = true
            }, "Overlay", XPeriencePlayerControlPrimary);
            // Close Button
            ControlPanelelements.Add(new CuiButton
            {
                Button =
                {
                    Close = XPeriencePlayerControlPrimary,
                    Color = "0.0 0.0 0.0 0.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.95 0.94",
                    AnchorMax = "1.0 1.0"
                },
                Text =
                {
                    Text = "ⓧ",
                    FontSize = 20,
                    Color = "1.0 0.0 0.0 1.0",
                    Align = TextAnchor.MiddleCenter
                }
            }, XPeriencePlayerControlPrimary);
            // Top Label
            ControlPanelelements.Add(XPUILabel("ⓍⓅerience", 1, 0.060f, TextAnchor.MiddleLeft, 20, "0.01", "0.25", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlPrimary);
            // Main - Player Info
            int statpoints = xprecord.MentalityP + xprecord.DexterityP + xprecord.MightP + xprecord.ChemistP;
            int skillpoints = xprecord.WoodCutterP + xprecord.SmithyP + xprecord.MinerP + xprecord.ForagerP + xprecord.HunterP + xprecord.FisherP + xprecord.CrafterP + xprecord.FramerP + xprecord.TamerP;
            // XP Bar Calulations
            double levelpercent = 0;
            if (xprecord.experience == 0 || xprecord.level == 0)
            {
                levelpercent = ((xprecord.experience - 0) / config.xpLevel.levelstart) * 100;
            }
            else
            {
                levelpercent = ((xprecord.experience - (xprecord.requiredxp / config.xpLevel.levelmultiplier)) / (xprecord.requiredxp - (xprecord.requiredxp / config.xpLevel.levelmultiplier))) * 100;
            }

            ControlPanelelements.Add(XPUIPanel("0.0 0.0", "0.25 0.93", "0.0 0.0 0.0 0.95"), XPeriencePlayerControlPrimary, XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{xprecord.displayname}", 1, height, TextAnchor.MiddleCenter, 15, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 2, height, TextAnchor.MiddleCenter, 9, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{XPLang("level", player.UserIDString)}: <color={TextColor("mainlevel", (int)xprecord.level)}>{xprecord.level} ({(int)levelpercent}%)</color>", 3, height, TextAnchor.MiddleLeft, 13, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{XPLang("experience", player.UserIDString)}: <color={TextColor("experience", (int)xprecord.experience)}>{(int)xprecord.experience}</color>", 4, height, TextAnchor.MiddleLeft, 13, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{XPLang("nextlevel", player.UserIDString)}: <color={TextColor("nextlevel", (int)xprecord.requiredxp)}>{(int)xprecord.requiredxp}</color> (<color={TextColor("remainingxp", (int)(xprecord.requiredxp - xprecord.experience))}>{(int)(xprecord.requiredxp - xprecord.experience)}</color>)", 5, height, TextAnchor.MiddleLeft, 13, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 6, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{XPLang("unusedstatpoints", player.UserIDString)}: <color={TextColor("unspent", xprecord.statpoint)}>{xprecord.statpoint}</color>", 7, height, TextAnchor.MiddleLeft, 13, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{XPLang("unusedskillpoints", player.UserIDString)}: <color={TextColor("unspent", xprecord.skillpoint)}>{xprecord.skillpoint}</color>", 8, height, TextAnchor.MiddleLeft, 13, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 9, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{XPLang("totalspent", player.UserIDString)}: <color={TextColor("spent", statpoints + skillpoints)}>{statpoints + skillpoints}</color>", 10, height, TextAnchor.MiddleLeft, 13, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            if (config.mentality.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("mentality", player.UserIDString)}: <color={TextColor("spent", xprecord.MentalityP)}>{xprecord.MentalityP}</color>", 11, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.dexterity.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("dexterity", player.UserIDString)}: <color={TextColor("spent", xprecord.DexterityP)}>{xprecord.DexterityP}</color>", 12, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.might.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("might", player.UserIDString)}: <color={TextColor("spent", xprecord.MightP)}>{xprecord.MightP}</color>", 13, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            /*
            if (config.chemist.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("chemist", player.UserIDString)}: <color={TextColor("spent", xprecord.ChemistP)}>{xprecord.ChemistP}</color>", 14, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            */
            if (config.woodcutter.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("woodcutter", player.UserIDString)}: <color={TextColor("spent", xprecord.WoodCutterP)}>{xprecord.WoodCutterP}</color>", 15, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.smithy.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("smithy", player.UserIDString)}: <color={TextColor("spent", xprecord.SmithyP)}>{xprecord.SmithyP}</color>", 16, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.miner.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("miner", player.UserIDString)}: <color={TextColor("spent", xprecord.MinerP)}>{xprecord.MinerP}</color>", 17, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.forager.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("forager", player.UserIDString)}: <color={TextColor("spent", xprecord.ForagerP)}>{xprecord.ForagerP}</color>", 18, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.hunter.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("hunter", player.UserIDString)}: <color={TextColor("spent", xprecord.HunterP)}>{xprecord.HunterP}</color>", 19, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.crafter.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("crafter", player.UserIDString)}: <color={TextColor("spent", xprecord.CrafterP)}>{xprecord.CrafterP}</color>", 20, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.framer.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("framer", player.UserIDString)}: <color={TextColor("spent", xprecord.FramerP)}>{xprecord.FramerP}</color>", 21, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.fisher.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("fisher", player.UserIDString)}: <color={TextColor("spent", xprecord.FisherP)}>{xprecord.FisherP}</color>", 22, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.medic.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("medic", player.UserIDString)}: <color={TextColor("spent", xprecord.MedicP)}>{xprecord.MedicP}</color>", 23, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }
            if (config.tamer.enabletame)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("tamer", player.UserIDString)}: <color={TextColor("spent", xprecord.TamerP)}>{xprecord.TamerP}</color>", 24, height, TextAnchor.MiddleLeft, 11, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            }            
            // Primary - Stats
            ControlPanelelements.Add(XPUIPanel("0.26 0.0", "0.50 0.93", "0.0 0.0 0.0 0.95"), XPeriencePlayerControlPrimary, XPeriencePlayerControlStats);
            ControlPanelelements.Add(XPUILabel($"{XPLang("stats", player.UserIDString)}", 1, height, TextAnchor.MiddleCenter, 15, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 2, height, TextAnchor.MiddleCenter, 9, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            // Mentality
            if (config.mentality.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("mentality", player.UserIDString)}: <color={TextColor("level", xprecord.Mentality)}>{xprecord.Mentality}</color>", 3, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("researchcost", player.UserIDString)}: <color={TextColor("perk", xprecord.Mentality)}>-{(xprecord.Mentality * config.mentality.researchcost) * 100}%</color>", 4, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("researchspeed", player.UserIDString)}: <color={TextColor("perk", xprecord.Mentality)}>-{(xprecord.Mentality * config.mentality.researchspeed) * 100}%</color>", 5, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("critchance", player.UserIDString)}:  <color={TextColor("perk", xprecord.Mentality)}>{(xprecord.Mentality * config.mentality.criticalchance) * 100}%</color>", 6, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 7, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            // Dexterity
            if (config.dexterity.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("dexterity", player.UserIDString)}: <color={TextColor("level", xprecord.Dexterity)}>{xprecord.Dexterity}</color>", 8, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("blockchance", player.UserIDString)}: <color={TextColor("perk", xprecord.Dexterity)}>{(xprecord.Dexterity * config.dexterity.blockchance) * 100}%</color> (Damage: <color={TextColor("perk", xprecord.Dexterity)}>-{(xprecord.Dexterity * config.dexterity.blockamount) * 100}%</color>)", 9, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("dodgechance", player.UserIDString)}: <color={TextColor("perk", xprecord.Dexterity)}>{(xprecord.Dexterity * config.dexterity.dodgechance) * 100}%</color>", 10, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("armordmgabsorb", player.UserIDString)}: <color={TextColor("perk", xprecord.Dexterity)}>{(xprecord.Dexterity * config.dexterity.reducearmordmg) * 100}%</color>", 11, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 12, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            // Might
            if (config.might.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("might", player.UserIDString)}: <color={TextColor("level", xprecord.Might)}>{xprecord.Might}</color>", 13, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("armor", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>{(xprecord.Might * config.might.armor) * 100}</color>", 14, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("melee", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>+{(xprecord.Might * config.might.meleedmg) * 100}%</color>", 15, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("calories", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>+{(int)((config.might.metabolism * xprecord.Might) * player.metabolism.calories.max)}</color>", 16, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("hydration", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>+{(int)((config.might.metabolism * xprecord.Might) * player.metabolism.hydration.max)}</color>", 17, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("bleed", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>-{(config.might.bleedreduction * xprecord.Might) * 100}%</color>", 18, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("radiation", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>-{(config.might.radreduction * xprecord.Might) * 100}%</color>", 19, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("heat", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>+{(config.might.heattolerance * xprecord.Might) * 100}%</color>", 20, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("cold", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>+{(config.might.coldtolerance * xprecord.Might) * 100}%</color>", 21, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                //ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("heat", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>+(disabled)</color>", 20, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                //ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("cold", player.UserIDString)}: <color={TextColor("perk", xprecord.Might)}>+(disabled)</color>", 21, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 22, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            // Chemist
            /*
            if (config.chemist.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("chemist", player.UserIDString)}: <color={TextColor("level", xprecord.Chemist)}>{xprecord.Chemist}</color>", 23, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫</color>{XPLang("craftspeed", player.UserIDString)}: <color={TextColor("perk", xprecord.Chemist)}>-{(xprecord.Chemist * config.chemist.crafttime) * 100}%</color>", 24, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlStats);
            }
            */
            // Kill Records Button
            if (KillRecords != null && config.xpBonus.showkrbutton)
            {
                ControlPanelelements.Add(new CuiButton
                {
                    Button =
                        {
                            Command = $"xp.killrecords {xprecord.displayname}",
                            Color = "0.2 0.2 0.2 1.0"
                        },
                    RectTransform =
                        {
                    AnchorMin = $"0.20 {1 - height*25 + 25 * .002f}",
                    AnchorMax = $"0.80 {1 - height*(25-1) + 25 * .002f}"
                        },
                    Text =
                        {
                            Text = "View Kill Records",
                            FontSize = 15,
                            Color = "1.0 0.0 0.0 1.0",
                            Align = TextAnchor.MiddleCenter
                        }
                }, XPeriencePlayerControlStats);
            }
            // Secondary - Skills
            ControlPanelelements.Add(XPUIPanel("0.51 0.0", "1.0 0.93", "0.0 0.0 0.0 0.95"), XPeriencePlayerControlPrimary, XPeriencePlayerControlSkills);
            ControlPanelelements.Add(XPUILabel($"{XPLang("skills", player.UserIDString)}", 1, height, TextAnchor.MiddleCenter, 15, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkills);
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 2, height, TextAnchor.MiddleCenter, 9, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkills);
            // Left Column
            ControlPanelelements.Add(XPUIPanel("0.0 0.0", "0.5 0.915", "0.0 0.0 0.0 0.0"), XPeriencePlayerControlSkills, XPeriencePlayerControlSkillsL);
            // WoodCutter
            if (config.woodcutter.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("woodcutter", player.UserIDString)}: <color={TextColor("level", xprecord.WoodCutter)}>{xprecord.WoodCutter}</color>", 1, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("woodgather", player.UserIDString)}: <color={TextColor("perk", xprecord.WoodCutter)}>+{(xprecord.WoodCutter * config.woodcutter.gatherrate)}x</color>", 2, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("woodbonus", player.UserIDString)}: <color={TextColor("perk", xprecord.WoodCutter)}>+{(xprecord.WoodCutter * config.woodcutter.bonusincrease)}x</color>", 3, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("woodapple", player.UserIDString)}: <color={TextColor("perk", xprecord.WoodCutter)}>{(xprecord.WoodCutter * config.woodcutter.applechance) * 100}%</color>", 4, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 5, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            // Smithy
            if (config.smithy.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("smithy", player.UserIDString)}: <color={TextColor("level", xprecord.Smithy)}>{xprecord.Smithy}</color>", 6, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("productionrate", player.UserIDString)}: <color={TextColor("perk", xprecord.Smithy)}>{(xprecord.Smithy * config.smithy.productionrate) * 100}%</color> ({XPLang("productionamount", player.UserIDString)}: <color={TextColor("perk", xprecord.Smithy)}>+{Math.Round((xprecord.Smithy * config.smithy.productionrate) * 5)}</color>)", 7, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("fuelconsumption", player.UserIDString)}: - <color={TextColor("perk", xprecord.Smithy)}>{(xprecord.Smithy * config.smithy.fuelconsumption) * 100}%</color>", 8, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 9, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            // Miner
            if (config.miner.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("miner", player.UserIDString)}: <color={TextColor("level", xprecord.Miner)}>{xprecord.Miner}</color>", 10, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("oregather", player.UserIDString)}: <color={TextColor("perk", xprecord.Miner)}>+{(xprecord.Miner * config.miner.gatherrate)}x</color>", 11, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("orebonus", player.UserIDString)}: <color={TextColor("perk", xprecord.Miner)}>+{(xprecord.Miner * config.miner.bonusincrease)}x</color>", 12, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("fuelconsumptionhats", player.UserIDString)}: <color={TextColor("perk", xprecord.Miner)}>-{(xprecord.Miner * config.miner.fuelconsumption) * 100}%</color>", 13, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 14, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            // Forager
            if (config.forager.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("forager", player.UserIDString)}: <color={TextColor("level", xprecord.Forager)}>{xprecord.Forager}</color>", 15, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("gather", player.UserIDString)}: <color={TextColor("perk", xprecord.Forager)}>+{(xprecord.Forager * config.forager.gatherrate)}x</color>", 16, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("seedbonus", player.UserIDString)}: <color={TextColor("perk", xprecord.Forager)}>{(config.forager.chanceincrease * xprecord.Forager) * 100}%</color> (Amount: <color={TextColor("perk", xprecord.Forager)}>{(config.forager.chanceincrease * xprecord.Forager) * 10}</color>)", 17, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("randomitem", player.UserIDString)}: <color={TextColor("perk", xprecord.Forager)}>{(xprecord.Forager * config.forager.randomchance) * 100}%</color>", 18, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 19, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            // Hunter
            if (config.hunter.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("hunter", player.UserIDString)}: <color={TextColor("level", xprecord.Hunter)}>{xprecord.Hunter}</color>", 20, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("foodgather", player.UserIDString)}: <color={TextColor("perk", xprecord.Hunter)}>+{(xprecord.Hunter * config.hunter.gatherrate)}x</color>", 21, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("bonusgather", player.UserIDString)}: <color={TextColor("perk", xprecord.Hunter)}>+{(xprecord.Hunter * config.hunter.bonusincrease)}x</color>", 22, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("damagewildlife", player.UserIDString)}: <color={TextColor("perk", xprecord.Hunter)}>+{(xprecord.Hunter * config.hunter.damageincrease) * 100}%</color>", 23, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("nightdamage", player.UserIDString)}: <color={TextColor("perk", xprecord.Hunter)}>+{(xprecord.Hunter * config.hunter.nightdmgincrease) * 100}%</color>", 24, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsL);
            }
            // Right Column
            ControlPanelelements.Add(XPUIPanel("0.5 0.0", "1.0 0.915", "0.0 0.0 0.0 0.0"), XPeriencePlayerControlSkills, XPeriencePlayerControlSkillsR);
            // Crafter
            if (config.crafter.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("crafter", player.UserIDString)}: <color={TextColor("level", xprecord.Crafter)}>{xprecord.Crafter}</color>", 1, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("craftspeed", player.UserIDString)}: <color={TextColor("perk", xprecord.Crafter)}>-{(config.crafter.craftspeed * xprecord.Crafter) * 100}%</color>", 2, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("costreduction", player.UserIDString)}: <color={TextColor("perk", xprecord.Crafter)}>-{(xprecord.Crafter * config.crafter.craftcost) * 100}%</color>", 3, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("fullrepair", player.UserIDString)}: <color={TextColor("perk", xprecord.Crafter)}>{(xprecord.Crafter * config.crafter.repairincrease) * 100}%</color>", 4, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("repaircost", player.UserIDString)}: <color={TextColor("perk", xprecord.Crafter)}>-{(xprecord.Crafter * config.crafter.repaircost) * 100}%</color>", 5, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("highcond", player.UserIDString)}: <color={TextColor("perk", xprecord.Crafter)}>{(config.crafter.conditionchance * xprecord.Crafter) * 100}%</color> (<color={TextColor("perk", xprecord.Crafter)}>+{config.crafter.conditionamount * 100}%</color> Condition)", 6, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 7, height, TextAnchor.MiddleLeft, 7, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            // Framer
            if (config.framer.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("framer", player.UserIDString)}: <color={TextColor("level", xprecord.Framer)}>{xprecord.Framer}</color>", 8, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("upgradecost", player.UserIDString)}: <color={TextColor("perk", xprecord.Framer)}>-{(config.framer.upgradecost * xprecord.Framer) * 100}%</color>", 9, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("repairtime", player.UserIDString)}: - <color={TextColor("perk", xprecord.Framer)}>{(xprecord.Framer * config.framer.repairtime) * 100}%</color>", 10, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("repaircost", player.UserIDString)}: - <color={TextColor("perk", xprecord.Framer)}>{(xprecord.Framer * config.framer.repaircost) * 100}%</color>", 11, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 12, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            // Fisher
            if (config.fisher.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("fisher", player.UserIDString)}: <color={TextColor("level", xprecord.Fisher)}>{xprecord.Fisher}</color>", 13, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("fishamount", player.UserIDString)}: <color={TextColor("perk", xprecord.Fisher)}>+{Math.Round(xprecord.Fisher * config.fisher.fishamountincrease)}</color>", 14, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("fishitems", player.UserIDString)}: <color={TextColor("perk", xprecord.Fisher)}>+{Math.Round(xprecord.Fisher * config.fisher.itemamountincrease)}</color>", 15, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 16, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            // Medic
            if (config.medic.maxlvl != 0)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("medic", player.UserIDString)}: <color={TextColor("level", xprecord.Medic)}>{xprecord.Medic}</color>", 17, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("medicrevive", player.UserIDString)}: <color={TextColor("perk", xprecord.Medic)}>+{Math.Round(xprecord.Medic * config.medic.recoverhp)}</color>", 18, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("medicrecover", player.UserIDString)}: <color={TextColor("perk", xprecord.Medic)}>+{Math.Round(xprecord.Medic * config.medic.revivehp)}</color>", 19, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("medictools", player.UserIDString)}: <color={TextColor("perk", xprecord.Medic)}>+{Math.Round(xprecord.Medic * config.medic.tools)}</color>", 20, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("mediccrafting", player.UserIDString)}: <color={TextColor("perk", xprecord.Medic)}>+{Math.Round((xprecord.Medic * config.medic.crafttime) * 100)}%</color>", 21, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            }
            // Spacer
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 22, height, TextAnchor.MiddleLeft, 5, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
            // Tamer
            if (config.tamer.enabletame)
            {
                ControlPanelelements.Add(XPUILabel($"{XPLang("tamer", player.UserIDString)}: <color={TextColor("level", xprecord.Tamer)}>{xprecord.Tamer}</color>", 23, height, TextAnchor.MiddleLeft, 12, "0.1", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                if (xprecord.Tamer == 0)
                {
                    ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> {XPLang("tamerinc", player.UserIDString)}", 24, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                }
                if (xprecord.Tamer > 0)
                {
                    ControlPanelelements.Add(XPUILabel($"{XPLang("tamerpets", player.UserIDString)}:", 24, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                }
                if (xprecord.Tamer >= config.tamer.chickenlevel)
                {
                    ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> <color={TextColor("pets", xprecord.Tamer)}>{XPLang("chicken", player.UserIDString)}</color>", 25, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                }
                if (xprecord.Tamer >= config.tamer.boarlevel)
                {
                    ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> <color={TextColor("pets", xprecord.Tamer)}>{XPLang("boar", player.UserIDString)}</color>", 26, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                }
                if (xprecord.Tamer >= config.tamer.staglevel)
                {
                    ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> <color={TextColor("pets", xprecord.Tamer)}>{XPLang("stag", player.UserIDString)}</color>", 27, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                }
                if (xprecord.Tamer >= config.tamer.wolflevel)
                {
                    ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> <color={TextColor("pets", xprecord.Tamer)}>{XPLang("wolf", player.UserIDString)}</color>", 28, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                }
                if (xprecord.Tamer >= config.tamer.bearlevel)
                {
                    ControlPanelelements.Add(XPUILabel($"<color=red>▫ </color> <color={TextColor("pets", xprecord.Tamer)}>{XPLang("bear", player.UserIDString)}</color>", 29, height, TextAnchor.MiddleLeft, 10, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlSkillsR);
                }
            }
            // UI End
            CuiHelper.AddUi(player, ControlPanelelements);
            return;
        }

        private void HelpPanel(BasePlayer player, int page)
        {
            if (player == null) return;
            float height = 0.043f;
            float buttonheight = 0.035f;
            //page = "1";
            var ControlPanelelements = new CuiElementContainer();
            // Main UI
            ControlPanelelements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.1 0.1 0.1 0.95"
                },
                RectTransform =
                {
                    AnchorMin = $"0.05 0.12",
                    AnchorMax = $"0.95 0.95"
                },
                CursorEnabled = true
            }, "Overlay", XPeriencePlayerControlPrimary);
            // Close Button
            ControlPanelelements.Add(new CuiButton
            {
                Button =
                {
                    Close = XPeriencePlayerControlPrimary,
                    Color = "0.0 0.0 0.0 0.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.95 0.94",
                    AnchorMax = "1.0 1.0"
                },
                Text =
                {
                    Text = "ⓧ",
                    FontSize = 20,
                    Color = "1.0 0.0 0.0 1.0",
                    Align = TextAnchor.MiddleCenter
                }
            }, XPeriencePlayerControlPrimary);
            // Profile Button
            ControlPanelelements.Add(new CuiButton
            {
                Button =
                {
                    Command = "xp.playercontrolpanel",
                    Color = "1.0 0.0 0.0 1.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.16 0.95",
                    AnchorMax = "0.30 0.99"
                },
                Text =
                {
                    Text = $"{XPLang("mystats", player.UserIDString)}",
                    FontSize = 12,
                    Color = "1.0 1.0 1.0 1.0",
                    Align = TextAnchor.MiddleCenter
                }
            }, XPeriencePlayerControlPrimary);
            // Previous Page Button
            ControlPanelelements.Add(new CuiButton
            {
                Button =
                {
                    Command = $"xp.help {page - 1}",
                    Color = "1.0 0.0 0.0 1.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.70 0.95",
                    AnchorMax = "0.77 0.99"
                },
                Text =
                {
                    Text = $"{XPLang("helpprev", player.UserIDString)}",
                    FontSize = 12,
                    Color = "1.0 1.0 1.0 1.0",
                    Align = TextAnchor.MiddleCenter
                }
            }, XPeriencePlayerControlPrimary);
            // Next Page Button
            ControlPanelelements.Add(new CuiButton
            {
                Button =
                {
                    Command = $"xp.help {page + 1}",
                    Color = "1.0 0.0 0.0 1.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.79 0.95",
                    AnchorMax = "0.86 0.99"
                },
                Text =
                {
                    Text = $"{XPLang("helpnext", player.UserIDString)}",
                    FontSize = 12,
                    Color = "1.0 1.0 1.0 1.0",
                    Align = TextAnchor.MiddleCenter
                }
            }, XPeriencePlayerControlPrimary);
            // Top Label
            ControlPanelelements.Add(XPUILabel("ⓍⓅerience", 1, 0.060f, TextAnchor.MiddleLeft, 20, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlPrimary);
            ControlPanelelements.Add(XPUILabel($"{XPLang("moddetails", player.UserIDString)} MACHIN3", 1, 0.060f, TextAnchor.MiddleCenter, 15, "0.3", "0.70", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlPrimary);
            // Command List
            ControlPanelelements.Add(XPUIPanel("0.0 0.0", "0.30 0.93", "0.0 0.0 0.0 0.95"), XPeriencePlayerControlPrimary, XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"{XPLang("helpcommands", player.UserIDString)}:", 1, height, TextAnchor.MiddleCenter, 15, "0.01", "0.90", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(XPUILabel($"----------------------------------------------------------------", 2, height, TextAnchor.MiddleCenter, 9, "0.0", "1.0", "1.0 1.0 1.0 1.0"), XPeriencePlayerControlMain);
            ControlPanelelements.Add(new CuiLabel
            {
                Text =
                {
                    Text = $"{XPLang("helpcommandslist", player.UserIDString)} \n{XPLang("bindkey", player.UserIDString)}",
                    FontSize = 11,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.02 0.0",
                    AnchorMax = "0.99 0.90"
                }
            }, XPeriencePlayerControlMain);
            // Main Details
            ControlPanelelements.Add(XPUIPanel("0.31 0.0", ".99 0.93", "0.0 0.0 0.0 0.95"), XPeriencePlayerControlPrimary, XPerienceHelp);
            if (page == 1)
            {
                ControlPanelelements.Add(XPUILabel("ⓍⓅerience", 3, 0.060f, TextAnchor.MiddleCenter, 25, "0.01", "0.99", "1.0 1.0 1.0 1.0"), XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutxperience", player.UserIDString)}",
                    FontSize = 18,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.0",
                    AnchorMax = "0.99 0.99"
                }
                }, XPerienceHelp);
            }
            if (page == 2)
            {
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("serversettings", player.UserIDString, ServerSettings("levelstart"), ServerSettings("levelmultiplier"), ServerSettings("levelxpboost"), ServerSettings("statpointsperlvl"), ServerSettings("skillpointsperlvl"), ServerSettings("resettimerenabled"), ServerSettings("resettimerstats"), ServerSettings("resettimerskills"), ServerSettings("vipresettimerstats"), ServerSettings("vipresettimerskills"), ServerSettings("nightbonusenable"), ServerSettings("nightbonus"), ServerSettings("nightstart"), ServerSettings("nightend"), ServerSettings("nightskill"))}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.65",
                    AnchorMax = "0.99 0.99"
                }
                }, XPerienceHelp);

                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("xpsettings", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.60",
                    AnchorMax = "0.99 0.64"
                }
                }, XPerienceHelp);

                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("xpsettingskills", player.UserIDString, ServerSettings("chicken"), ServerSettings("fish"), ServerSettings("boar"), ServerSettings("stag"), ServerSettings("wolf"), ServerSettings("bear"), ServerSettings("shark"), ServerSettings("horse"), ServerSettings("scientist"), ServerSettings("dweller"), ServerSettings("player"), ServerSettings("bradley"), ServerSettings("heli"), ServerSettings("revive"))}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.0",
                    AnchorMax = "0.15 0.59"
                }
                }, XPerienceHelp);

                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("xpsettingsloot", player.UserIDString, ServerSettings("loot"), ServerSettings("uloot"), ServerSettings("lloot"), ServerSettings("hloot"), ServerSettings("aharvest"), ServerSettings("charvest"), ServerSettings("tree"), ServerSettings("ore"), ServerSettings("gather"), ServerSettings("plant"))}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.16 0.0",
                    AnchorMax = "0.37 0.59"
                }
                }, XPerienceHelp);

                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("xpsettingscraft", player.UserIDString, ServerSettings("craft"), ServerSettings("wood"), ServerSettings("stone"), ServerSettings("metal"), ServerSettings("armored"))}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.38 0.0",
                    AnchorMax = "0.55 0.59"
                }
                }, XPerienceHelp);

                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("xpmissionsettings", player.UserIDString, ServerSettings("missionsucceed"), ServerSettings("missionfailed"), ServerSettings("missionfailedxp"))}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.56 0.0",
                    AnchorMax = "0.80 0.59"
                }
                }, XPerienceHelp);

                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("xpreductionsettings", player.UserIDString, ServerSettings("death"), ServerSettings("deathenable"), ServerSettings("suicide"), ServerSettings("suicideenable"))}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.81 0.0",
                    AnchorMax = "0.99 0.59"
                }
                }, XPerienceHelp);

                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("nextpagestats", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.0",
                    AnchorMax = "0.99 0.10"
                }
                }, XPerienceHelp);

            }
            if (page == 3)
            {
                // Main Details
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutstats", player.UserIDString)}\n-----------------------------------------------------------------------------------------------------------------------\n\n",
                    FontSize = 15,
                    Align = TextAnchor.UpperCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.83",
                    AnchorMax = "0.99 0.99"
                }
                }, XPerienceHelp);
                // Mentality
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"[{XPLang("mentality", player.UserIDString)}]",
                    FontSize = 15,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.79",
                    AnchorMax = "0.75 0.82"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutmentality", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.65",
                    AnchorMax = "0.75 0.79"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutmentalitysettings", player.UserIDString, ServerSettings("mentalitylevel"), ServerSettings("mentalitycost"), ServerSettings("mentalitymultiplier"), ServerSettings("mentalityresearchcost"), ServerSettings("mentalityresearchspeed"), ServerSettings("mentalitycrit"))}",
                    FontSize = 10,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.76 0.65",
                    AnchorMax = "0.99 0.82"
                }
                }, XPerienceHelp);
                // Dexterity
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"[{XPLang("dexterity", player.UserIDString)}]",
                    FontSize = 15,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.61",
                    AnchorMax = "0.75 0.64"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutdexterity", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.47",
                    AnchorMax = "0.75 0.60"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutdexteritysettings", player.UserIDString, ServerSettings("dexlevel"), ServerSettings("dexcost"), ServerSettings("dexmultiplier"), ServerSettings("dexblock"), ServerSettings("dexblockamt"), ServerSettings("dexdodge"), ServerSettings("dexarmor"))}",
                    FontSize = 10,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.76 0.47",
                    AnchorMax = "0.99 0.64"
                }
                }, XPerienceHelp);
                // Might
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"[{XPLang("might", player.UserIDString)}]",
                    FontSize = 15,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.43",
                    AnchorMax = "0.75 0.46"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutmight", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.20",
                    AnchorMax = "0.75 0.42"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutmightsettings", player.UserIDString, ServerSettings("mightlevel"), ServerSettings("mightcost"), ServerSettings("mightmultiplier"), ServerSettings("mightarmor"), ServerSettings("mightmelee"), ServerSettings("mightmeta"), ServerSettings("mightbleed"), ServerSettings("mightrad"), ServerSettings("mightheat"), ServerSettings("mightcold"))}",
                    FontSize = 10,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.76 0.20",
                    AnchorMax = "0.99 0.46"
                }
                }, XPerienceHelp);
                // Chemist
                /*
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"[{XPLang("chemist", player.UserIDString)}]",
                    FontSize = 15,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.17",
                    AnchorMax = "0.75 0.20"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutchemist", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.01",
                    AnchorMax = "0.75 0.17"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutchemistsettings", player.UserIDString, ServerSettings("chemlevel"), ServerSettings("chemcost"), ServerSettings("chemmultiplier"), ServerSettings("chemcraft"))}",
                    FontSize = 10,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.76 0.01",
                    AnchorMax = "0.99 0.20"
                }
                }, XPerienceHelp);
                */
            }
            if (page == 4)
            {
                // Main Details
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutskills", player.UserIDString)}\n-----------------------------------------------------------------------------------------------------------------------\n\n",
                    FontSize = 15,
                    Align = TextAnchor.UpperCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.83",
                    AnchorMax = "0.99 0.99"
                }
                }, XPerienceHelp);
                // WoodCutter
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"[{XPLang("woodcutter", player.UserIDString)}]",
                    FontSize = 15,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.79",
                    AnchorMax = "0.75 0.82"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutwoodcutter", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.65",
                    AnchorMax = "0.75 0.79"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutwoodcuttersettings", player.UserIDString, ServerSettings("woodcutterlev"), ServerSettings("woodcuttercost"), ServerSettings("woodcuttermulti"), ServerSettings("woodcuttergather"), ServerSettings("woodcutterbonus"), ServerSettings("woodcutterapple"))}",
                    FontSize = 10,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.76 0.65",
                    AnchorMax = "0.99 0.82"
                }
                }, XPerienceHelp);
                // Smithy
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"[{XPLang("smithy", player.UserIDString)}]",
                    FontSize = 15,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.62",
                    AnchorMax = "0.75 0.65"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutsmithy", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.48",
                    AnchorMax = "0.75 0.62"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutsmithysettings", player.UserIDString, ServerSettings("smithylev"), ServerSettings("smithycost"), ServerSettings("smithymulti"), ServerSettings("smithyproduction"), ServerSettings("smithyfuel"))}",
                    FontSize = 10,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.76 0.48",
                    AnchorMax = "0.99 0.65"
                }
                }, XPerienceHelp);
                // Miner
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"[{XPLang("miner", player.UserIDString)}]",
                    FontSize = 15,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.45",
                    AnchorMax = "0.75 0.48"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutminer", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.31",
                    AnchorMax = "0.75 0.45"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutminersettings", player.UserIDString, ServerSettings("minerlev"), ServerSettings("minercost"), ServerSettings("minermulti"), ServerSettings("minergather"), ServerSettings("minerbonus"), ServerSettings("minerfuel"))}",
                    FontSize = 10,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.76 0.31",
                    AnchorMax = "0.99 0.48"
                }
                }, XPerienceHelp);
                // Forager
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"[{XPLang("forager", player.UserIDString)}]",
                    FontSize = 15,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.25",
                    AnchorMax = "0.75 0.28"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutforager", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.0",
                    AnchorMax = "0.75 0.25"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutforagersettings", player.UserIDString, ServerSettings("foragerlev"), ServerSettings("foragercost"), ServerSettings("foragermulti"), ServerSettings("foragergather"), ServerSettings("foragerseed"), ServerSettings("foragerseedamt"), ServerSettings("forageritem"))}",
                    FontSize = 10,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.76 0.0",
                    AnchorMax = "0.99 0.28"
                }
                }, XPerienceHelp);
                // Page Break
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("nextpageskills", player.UserIDString)}",
                    FontSize = 10,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.0",
                    AnchorMax = "0.99 0.10"
                }
                }, XPerienceHelp);
            }
            if (page == 5)
            {
                // Main Details
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutskills", player.UserIDString)}\n-----------------------------------------------------------------------------------------------------------------------\n\n",
                    FontSize = 15,
                    Align = TextAnchor.UpperCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.83",
                    AnchorMax = "0.99 0.99"
                }
                }, XPerienceHelp);
                // Hunter
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"[{XPLang("hunter", player.UserIDString)}]",
                    FontSize = 15,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.79",
                    AnchorMax = "0.75 0.82"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("abouthunter", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.65",
                    AnchorMax = "0.75 0.79"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("abouthuntersettings", player.UserIDString, ServerSettings("hunterlev"), ServerSettings("huntercost"), ServerSettings("huntermulti"), ServerSettings("huntergather"), ServerSettings("hunterbonus"), ServerSettings("hunterdmg"), ServerSettings("hunterdmgnight"))}",
                    FontSize = 10,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.76 0.65",
                    AnchorMax = "0.99 0.82"
                }
                }, XPerienceHelp);
                // Fisher
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"[{XPLang("fisher", player.UserIDString)}]",
                    FontSize = 15,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.62",
                    AnchorMax = "0.75 0.65"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutfisher", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.48",
                    AnchorMax = "0.75 0.62"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutfishersettings", player.UserIDString, ServerSettings("fisherlev"), ServerSettings("fishercost"), ServerSettings("fishermulti"), ServerSettings("fisheramt"), ServerSettings("fisheramtitem"))}",
                    FontSize = 10,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.76 0.48",
                    AnchorMax = "0.99 0.65"
                }
                }, XPerienceHelp);
                // Crafter
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"[{XPLang("crafter", player.UserIDString)}]",
                    FontSize = 15,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.45",
                    AnchorMax = "0.75 0.48"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutcrafter", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.31",
                    AnchorMax = "0.75 0.45"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutcraftersettings", player.UserIDString, ServerSettings("craftlev"), ServerSettings("craftcost"), ServerSettings("craftmulti"), ServerSettings("craftspeed"), ServerSettings("craftcostitem"), ServerSettings("craftrepair"), ServerSettings("craftcond"))}",
                    FontSize = 10,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.76 0.31",
                    AnchorMax = "0.99 0.48"
                }
                }, XPerienceHelp);
                // Framer
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"[{XPLang("framer", player.UserIDString)}]",
                    FontSize = 15,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.25",
                    AnchorMax = "0.75 0.28"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutframer", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.0",
                    AnchorMax = "0.75 0.25"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutframersettings", player.UserIDString, ServerSettings("framerlev"), ServerSettings("framercost"), ServerSettings("framermulti"), ServerSettings("framerupgrade"), ServerSettings("framerrepair"), ServerSettings("framertime"))}",
                    FontSize = 10,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.76 0.0",
                    AnchorMax = "0.99 0.28"
                }
                }, XPerienceHelp);
                // Page Break
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("nextpageskills", player.UserIDString)}",
                    FontSize = 10,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.0",
                    AnchorMax = "0.99 0.10"
                }
                }, XPerienceHelp);
            }
            if (page == 6)
            {
                // Main Details
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutskills", player.UserIDString)}\n-----------------------------------------------------------------------------------------------------------------------\n\n",
                    FontSize = 15,
                    Align = TextAnchor.UpperCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.83",
                    AnchorMax = "0.99 0.99"
                }
                }, XPerienceHelp);
                // Medic
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"[{XPLang("medic", player.UserIDString)}]",
                    FontSize = 15,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.79",
                    AnchorMax = "0.75 0.82"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutmedic", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.50",
                    AnchorMax = "0.75 0.78"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("aboutmedicsettings", player.UserIDString, ServerSettings("mediclev"), ServerSettings("mediccost"), ServerSettings("medicmulti"), ServerSettings("medichpp"), ServerSettings("medichp"), ServerSettings("mediccraft"))}",
                    FontSize = 10,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.76 0.50",
                    AnchorMax = "0.99 0.82"
                }
                }, XPerienceHelp);
                // Tamer
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"[{XPLang("tamer", player.UserIDString)}]",
                    FontSize = 15,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.46",
                    AnchorMax = "0.75 0.49"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("abouttamer", player.UserIDString)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.01",
                    AnchorMax = "0.75 0.45"
                }
                }, XPerienceHelp);
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("abouttamersettings", player.UserIDString, ServerSettings("tamerenabled"), ServerSettings("tamerlev"), ServerSettings("tamercost"), ServerSettings("tamermulti"), ServerSettings("tamerchicken"), ServerSettings("tamerchickenlev"), ServerSettings("tamerboar"), ServerSettings("tamerboarlev"), ServerSettings("tamerstag"), ServerSettings("tamerstaglev"), ServerSettings("tamerwolf"), ServerSettings("tamerwolflev"), ServerSettings("tamerbear"), ServerSettings("tamerbearlev"))}",
                    FontSize = 10,
                    Align = TextAnchor.UpperLeft,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.76 0.01",
                    AnchorMax = "0.99 0.49"
                }
                }, XPerienceHelp);
            }
            if (page == 7)
            {
                // Fix Player Data Help
                ControlPanelelements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = $"{XPLang("playerfixdatahelp", player.UserIDString)}\n-----------------------------------------------------------------------------------------------------------------------\n\n",
                    FontSize = 15,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.02 0.50",
                    AnchorMax = "0.99 0.99"
                }
                }, XPerienceHelp);
                // Player Fix Data Button
                XPRecord xprecord = GetXPRecord(player);
                DateTime resettimedata = xprecord.playerfixdata.AddMinutes(config.defaultOptions.playerfixdatatimer);
                TimeSpan datainterval = resettimedata - DateTime.Now;
                int datatimer = (int)datainterval.TotalMinutes;
                var button = "";

                if (datatimer > 0)
                {
                    button = $"{XPLang("resettimerdata", player.UserIDString, datatimer)}";
                }
                else
                {
                    button = $"{XPLang("playerfixdatabutton", player.UserIDString)}";
                }

                ControlPanelelements.Add(new CuiButton
                {
                    Button =
                {
                    Command = "xp.playerfixdata",
                    Color = "1.0 0.0 0.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.45 0.45",
                    AnchorMax = "0.55 0.50"
                },
                    Text =
                {
                    Text = $"{button}",
                    FontSize = 12,
                    Color = "1.0 1.0 1.0 1.0",
                    Align = TextAnchor.MiddleCenter
                }
                }, XPerienceHelp);
            }
            // UI End
            CuiHelper.AddUi(player, ControlPanelelements);
            return;
        }
        
        #endregion

        #region Admin Control Panel

        [ConsoleCommand("xp.admin")]
        private void cmdadminxp(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
            string page = arg.GetString(0);
            switch (page)
            {
                case "main":
                    DestroyUi(player, XPerienceAdminPanelInfo);
                    DestroyUi(player, XPerienceAdminPanelLevelXP);
                    DestroyUi(player, XPerienceAdminPanelStats);
                    DestroyUi(player, XPerienceAdminPanelSkills);
                    DestroyUi(player, XPerienceAdminPanelTimerColor);
                    DestroyUi(player, XPerienceAdminPanelOtherMods);
                    DestroyUi(player, XPerienceAdminPanelSQL);
                    DestroyUi(player, XPerienceAdminPanelReset);
                    AdminInfoPage(player);
                    break;
                case "levelxp":
                    DestroyUi(player, XPerienceAdminPanelInfo);
                    DestroyUi(player, XPerienceAdminPanelLevelXP);
                    DestroyUi(player, XPerienceAdminPanelStats);
                    DestroyUi(player, XPerienceAdminPanelSkills);
                    DestroyUi(player, XPerienceAdminPanelTimerColor);
                    DestroyUi(player, XPerienceAdminPanelOtherMods);
                    DestroyUi(player, XPerienceAdminPanelSQL);
                    DestroyUi(player, XPerienceAdminPanelReset);
                    AdminLevelPage(player);
                    break;
                case "stats":
                    DestroyUi(player, XPerienceAdminPanelInfo);
                    DestroyUi(player, XPerienceAdminPanelLevelXP);
                    DestroyUi(player, XPerienceAdminPanelStats);
                    DestroyUi(player, XPerienceAdminPanelSkills);
                    DestroyUi(player, XPerienceAdminPanelTimerColor);
                    DestroyUi(player, XPerienceAdminPanelOtherMods);
                    DestroyUi(player, XPerienceAdminPanelSQL);
                    DestroyUi(player, XPerienceAdminPanelReset);
                    AdminStatsPage(player);
                    break;
                case "skills":
                    DestroyUi(player, XPerienceAdminPanelInfo);
                    DestroyUi(player, XPerienceAdminPanelLevelXP);
                    DestroyUi(player, XPerienceAdminPanelStats);
                    DestroyUi(player, XPerienceAdminPanelSkills);
                    DestroyUi(player, XPerienceAdminPanelTimerColor);
                    DestroyUi(player, XPerienceAdminPanelOtherMods);
                    DestroyUi(player, XPerienceAdminPanelSQL);
                    DestroyUi(player, XPerienceAdminPanelReset);
                    AdminSkillsPage(player);
                    break;
                case "timercolor":
                    DestroyUi(player, XPerienceAdminPanelInfo);
                    DestroyUi(player, XPerienceAdminPanelLevelXP);
                    DestroyUi(player, XPerienceAdminPanelStats);
                    DestroyUi(player, XPerienceAdminPanelSkills);
                    DestroyUi(player, XPerienceAdminPanelTimerColor);
                    DestroyUi(player, XPerienceAdminPanelOtherMods);
                    DestroyUi(player, XPerienceAdminPanelSQL);
                    DestroyUi(player, XPerienceAdminPanelReset);
                    AdminTimerColorPage(player);
                    break;
                case "othermods":
                    DestroyUi(player, XPerienceAdminPanelInfo);
                    DestroyUi(player, XPerienceAdminPanelLevelXP);
                    DestroyUi(player, XPerienceAdminPanelStats);
                    DestroyUi(player, XPerienceAdminPanelSkills);
                    DestroyUi(player, XPerienceAdminPanelTimerColor);
                    DestroyUi(player, XPerienceAdminPanelOtherMods);
                    DestroyUi(player, XPerienceAdminPanelSQL);
                    DestroyUi(player, XPerienceAdminPanelReset);
                    AdminOtherModsPage(player);
                    break;
                case "sql":
                    DestroyUi(player, XPerienceAdminPanelInfo);
                    DestroyUi(player, XPerienceAdminPanelLevelXP);
                    DestroyUi(player, XPerienceAdminPanelStats);
                    DestroyUi(player, XPerienceAdminPanelSkills);
                    DestroyUi(player, XPerienceAdminPanelTimerColor);
                    DestroyUi(player, XPerienceAdminPanelOtherMods);
                    DestroyUi(player, XPerienceAdminPanelSQL);
                    DestroyUi(player, XPerienceAdminPanelReset);
                    AdminSQLPage(player);
                    break;
                case "save":
                    player.ChatMessage(XPLang("saveconfig", player.UserIDString));
                    SaveConfig();
                    break;
                case "reload":
                    Interface.Oxide.ReloadPlugin("XPerience");
                    break;
                case "close":
                    DestroyUi(player, XPerienceAdminPanelMain);
                    break;
                case "reset":
                    DestroyUi(player, XPerienceAdminPanelInfo);
                    DestroyUi(player, XPerienceAdminPanelLevelXP);
                    DestroyUi(player, XPerienceAdminPanelStats);
                    DestroyUi(player, XPerienceAdminPanelSkills);
                    DestroyUi(player, XPerienceAdminPanelTimerColor);
                    DestroyUi(player, XPerienceAdminPanelOtherMods);
                    DestroyUi(player, XPerienceAdminPanelSQL);
                    DestroyUi(player, XPerienceAdminPanelReset);
                    AdminResetPage(player);
                    break;              
                case "fix":
                    foreach (var allPlayer in BasePlayer.allPlayerList)
                    {
                        if (!allPlayer.UserIDString.IsSteamId()) continue;
                        XPRecord xprecord = GetXPRecord(allPlayer);
                        // Reset Level, Required XP & Stat/Skill Points
                        xprecord.level = 0;
                        if (xprecord.experience <= 0)
                        {
                            xprecord.experience = 0;
                        }
                        xprecord.requiredxp = 25;
                        xprecord.statpoint = 0;
                        xprecord.skillpoint = 0;
                        // Reset max health if needed before removing points
                        if (allPlayer._maxHealth > 100 || allPlayer._health > 100)
                        {
                            // Get Stats
                            double armor = (xprecord.Might * config.might.armor) * 100;
                            double currentmaxhealth = allPlayer._maxHealth;
                            double currenthealth = Mathf.Ceil(allPlayer._health);
                            // Remove Armor
                            double newmaxhealth = currentmaxhealth - armor;
                            double newhealth = currenthealth - armor;
                            // Change Health
                            if (newmaxhealth < 100)
                            {
                                allPlayer._maxHealth = 100;
                                allPlayer._health = (float)newhealth;
                            }
                            else
                            {
                                allPlayer._maxHealth = (float)newmaxhealth;
                                allPlayer._health = (float)newhealth;
                            }
                            //allPlayer._health = 100;
                        }
                        // Reset Stat Levels
                        xprecord.Mentality = 0;
                        xprecord.Dexterity = 0;
                        xprecord.Might = 0;
                        xprecord.Chemist = 0;
                        // Reset Stat Spent Points
                        xprecord.MentalityP = 0;
                        xprecord.DexterityP = 0;
                        xprecord.MightP = 0;
                        xprecord.ChemistP = 0;
                        // Reset Skill Levels
                        xprecord.WoodCutter = 0;
                        xprecord.Smithy = 0;
                        xprecord.Miner = 0;
                        xprecord.Forager = 0;
                        xprecord.Hunter = 0;
                        xprecord.Fisher = 0;
                        xprecord.Crafter = 0;
                        xprecord.Framer = 0;
                        xprecord.Medic = 0;
                        xprecord.Tamer = 0;
                        // Reset Skill Spents Points
                        xprecord.WoodCutterP = 0;
                        xprecord.SmithyP = 0;
                        xprecord.MinerP = 0;
                        xprecord.ForagerP = 0;
                        xprecord.HunterP = 0;
                        xprecord.FisherP = 0;
                        xprecord.CrafterP = 0;
                        xprecord.FramerP = 0;
                        xprecord.MedicP = 0;
                        xprecord.TamerP = 0;
                        // Check/Reset Tamer permissions
                        PetChecks(allPlayer, true);
                        // Set LiveUI Location to Default
                        xprecord.UILocation = config.defaultOptions.liveuistatslocation;
                        // Run Level Up to Recalculate Players Data
                        LvlUp(allPlayer, 0, 0);
                        // Update Live UI
                        LiveStats(allPlayer, true);
                        // Notify Players
                        player.ChatMessage(XPLang("adminfixplayers", player.UserIDString));
                   }
                    break;              
            }
        }
                
        [ConsoleCommand("xp.config")]
        private void cmdadminxpconfig(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
            string page = arg.GetString(0);
            string option = arg.GetString(1);
            double value = Convert.ToDouble(arg.GetString(2));
            bool setting = Convert.ToBoolean(arg.GetString(3));
            string color = "white";
            switch (page)
            {
                case "main":
                    DestroyUi(player, XPerienceAdminPanelInfo);
                    AdminInfoPage(player);
                    break;
                case "levelxp":
                    switch (option)
                    {
                        // Main
                        case "levelstart":
                            config.xpLevel.levelstart = (double)value;
                            break;
                        case "levelmultiplier":
                            config.xpLevel.levelmultiplier = (double)value;
                            break;
                        case "levelmax":
                            config.xpLevel.maxlevel = (int)value;
                            break;
                        case "levelxpboost":
                            config.xpLevel.levelxpboost = (double)value;
                            break;
                        case "statpointsperlvl":
                            config.xpLevel.statpointsperlvl = (int)value;
                            break;
                        case "skillpointsperlvl":
                            config.xpLevel.skillpointsperlvl = (int)value;
                            break;
                        // Night
                        case "nightenable":
                            config.nightBonus.Enable = setting;
                            break;
                        case "nightstart":
                            config.nightBonus.StartTime = (int)value;
                            break;
                        case "nightend":
                            config.nightBonus.EndTime = (int)value;
                            break;
                        case "nightbonus":
                            config.nightBonus.Bonus = (double)value;
                            break;
                        case "nightskill":
                            config.nightBonus.enableskillboosts = setting;
                            break;
                        // XP Kills
                        case "chicken":
                            config.xpGain.chickenxp = (double)value;
                            break;
                        case "fish":
                            config.xpGain.fishxp = (double)value;
                            break;
                        case "boar":
                            config.xpGain.boarxp = (double)value;
                            break;
                        case "stag":
                            config.xpGain.stagxp = (double)value;
                            break;
                        case "wolf":
                            config.xpGain.wolfxp = (double)value;
                            break;
                        case "bear":
                            config.xpGain.bearxp = (double)value;
                            break;
                        case "shark":
                            config.xpGain.sharkxp = (double)value;
                            break;
                        case "horse":
                            config.xpGain.horsexp = (double)value;
                            break;
                        case "scientist":
                            config.xpGain.scientistxp = (double)value;
                            break;
                        case "dweller":
                            config.xpGain.dwellerxp = (double)value;
                            break;
                        case "player":
                            config.xpGain.playerxp = (double)value;
                            break;
                        case "bradley":
                            config.xpGain.bradley = (double)value;
                            break;
                        case "heli":
                            config.xpGain.patrolhelicopter = (double)value;
                            break;
                        // XP Gathering/Looting
                        case "loot":
                            config.xpGain.lootcontainerxp = (double)value;
                            break;
                        case "lootu":
                            config.xpGain.underwaterlootcontainerxp = (double)value;
                            break;
                        case "lootlocked":
                            config.xpGain.lockedcratexp = (double)value;
                            break;
                        case "loothacked":
                            config.xpGain.hackablecratexp = (double)value;
                            break;
                        case "aharvest":
                            config.xpGain.animalharvestxp = (double)value;
                            break;
                        case "charvest":
                            config.xpGain.corpseharvestxp = (double)value;
                            break;
                        case "tree":
                            config.xpGather.treexp = (double)value;
                            break;
                        case "ore":
                            config.xpGather.orexp = (double)value;
                            break;
                        case "harvest":
                            config.xpGather.harvestxp = (double)value;
                            break;
                        case "plant":
                            config.xpGather.plantxp = (double)value;
                            break;
                        case "noxptools":
                            config.xpGather.noxptools = setting;
                            break;
                        // Crafting/Building
                        case "crafting":
                            config.xpGain.craftingxp = (double)value;
                            break;
                        case "woodbuild":
                            config.xpBuilding.woodstructure = (double)value;
                            break;
                        case "stonebuild":
                            config.xpBuilding.stonestructure = (double)value;
                            break;
                        case "metalbuild":
                            config.xpBuilding.metalstructure = (double)value;
                            break;
                        case "armorbuild":
                            config.xpBuilding.armoredstructure = (double)value;
                            break;
                        // XP Reduce
                        case "suicide":
                            config.xpReducer.suicidereduce = setting;
                            break;
                        case "suicideamt":
                            config.xpReducer.suicidereduceamount = (double)value;
                            break;
                        case "death":
                            config.xpReducer.deathreduce = setting;
                            break;
                        case "deathamt":
                            config.xpReducer.deathreduceamount = (double)value;
                            break;
                        // Missions
                        case "missionsucceeded":
                            config.xpMissions.missionsucceededxp = (int)value;
                            break;
                        case "missionfailed":
                            config.xpMissions.missionfailed = setting;
                            break;
                        case "missionfailedxp":
                            config.xpMissions.missionfailedxp = (int)value;
                            break;
                    }
                    DestroyUi(player, XPerienceAdminPanelLevelXP);
                    AdminLevelPage(player);
                    break;
                case "stats":
                    switch (option)
                    {
                        // Mentality
                        case "mentalitymaxlevel":
                            config.mentality.maxlvl = (int)value;
                            break;
                        case "mentalitycost":
                            config.mentality.pointcoststart = (int)value;
                            break;
                        case "mentalitycostmultiplier":
                            config.mentality.costmultiplier = (int)value;
                            break;
                        case "mentalityresearchcost":
                            config.mentality.researchcost = (double)value;
                            break;
                        case "mentalityresearchspeed":
                            config.mentality.researchspeed = (double)value;
                            break;
                        case "mentalitycriticalchance":
                            config.mentality.criticalchance = (double)value;
                            break;
                        case "mentalityothermod":
                            config.mentality.useotherresearchmod = setting;
                            break;
                        // Dexterity
                        case "dexteritymaxlevel":
                            config.dexterity.maxlvl = (int)value;
                            break;
                        case "dexteritycost":
                            config.dexterity.pointcoststart = (int)value;
                            break;
                        case "dexteritycostmultiplier":
                            config.dexterity.costmultiplier = (int)value;
                            break;
                        case "dexterityblock":
                            config.dexterity.blockchance = (double)value;
                            break;
                        case "dexterityblockamt":
                            config.dexterity.blockamount = (double)value;
                            break;
                        case "dexteritydodge":
                            config.dexterity.dodgechance = (double)value;
                            break;
                        case "dexterityarmor":
                            config.dexterity.reducearmordmg = (double)value;
                            break;
                        // Chemist
                        case "chemistmaxlevel":
                            config.chemist.maxlvl = (int)value;
                            break;
                        case "chemistcost":
                            config.chemist.pointcoststart = (int)value;
                            break;
                        case "chemistcostmultiplier":
                            config.chemist.costmultiplier = (int)value;
                            break;
                        case "chemistspeed":
                            config.chemist.crafttime = (double)value;
                            break;
                        // Might
                        case "mightmaxlevel":
                            config.might.maxlvl = (int)value;
                            break;
                        case "mightcost":
                            config.might.pointcoststart = (int)value;
                            break;
                        case "mightcostmultiplier":
                            config.might.costmultiplier = (int)value;
                            break;
                        case "mightarmor":
                            config.might.armor = (double)value;
                            break;
                        case "mightmelee":
                            config.might.meleedmg = (double)value;
                            break;
                        case "mightmeta":
                            config.might.metabolism = (double)value;
                            break;
                        case "mightbleed":
                            config.might.bleedreduction = (double)value;
                            break;
                        case "mightrad":
                            config.might.radreduction = (double)value;
                            break;
                        case "mightheat":
                            config.might.heattolerance = (double)value;
                            break;
                        case "mightcold":
                            config.might.coldtolerance = (double)value;
                            break;
                    }
                    DestroyUi(player, XPerienceAdminPanelStats);
                    AdminStatsPage(player);
                    break;
                case "skills":
                    switch (option)
                    {
                        // WoodCutter
                        case "woodcuttermaxlevel":
                            config.woodcutter.maxlvl = (int)value;
                            break;
                        case "woodcuttercost":
                            config.woodcutter.pointcoststart = (int)value;
                            break;
                        case "woodcuttercostmultiplier":
                            config.woodcutter.costmultiplier = (int)value;
                            break;
                        case "woodcuttergatherrate":
                            config.woodcutter.gatherrate = (double)value;
                            break;
                        case "woodcutterbonus":
                            config.woodcutter.bonusincrease = (double)value;
                            break;
                        case "woodcutterapple":
                            config.woodcutter.applechance = (double)value;
                            break;
                        // Smithy
                        case "smithymaxlevel":
                            config.smithy.maxlvl = (int)value;
                            break;
                        case "smithycost":
                            config.smithy.pointcoststart = (int)value;
                            break;
                        case "smithycostmultiplier":
                            config.smithy.costmultiplier = (int)value;
                            break;
                        case "smithyprate":
                            config.smithy.productionrate = (double)value;
                            break;
                        case "smithyfuel":
                            config.smithy.fuelconsumption = (double)value;
                            break;
                        // Miner
                        case "minermaxlevel":
                            config.miner.maxlvl = (int)value;
                            break;
                        case "minercost":
                            config.miner.pointcoststart = (int)value;
                            break;
                        case "minercostmultiplier":
                            config.miner.costmultiplier = (int)value;
                            break;
                        case "minergatherrate":
                            config.miner.gatherrate = (double)value;
                            break;
                        case "minerbonus":
                            config.miner.bonusincrease = (double)value;
                            break;
                        case "minerfuel":
                            config.miner.fuelconsumption = (double)value;
                            break;
                        // Forager
                        case "foragermaxlevel":
                            config.forager.maxlvl = (int)value;
                            break;
                        case "foragercost":
                            config.forager.pointcoststart = (int)value;
                            break;
                        case "foragercostmultiplier":
                            config.forager.costmultiplier = (int)value;
                            break;
                        case "foragergatherrate":
                            config.forager.gatherrate = (double)value;
                            break;
                        case "foragerseed":
                            config.forager.chanceincrease = (double)value;
                            break;
                        case "foragerrandom":
                            config.forager.randomchance = (double)value;
                            break;
                        // Hunter
                        case "huntermaxlevel":
                            config.hunter.maxlvl = (int)value;
                            break;
                        case "huntercost":
                            config.hunter.pointcoststart = (int)value;
                            break;
                        case "huntercostmultiplier":
                            config.hunter.costmultiplier = (int)value;
                            break;
                        case "huntergatherrate":
                            config.hunter.gatherrate = (double)value;
                            break;
                        case "hunterbonus":
                            config.hunter.bonusincrease = (double)value;
                            break;
                        case "hunterdamage":
                            config.hunter.damageincrease = (double)value;
                            break;
                        case "hunterndamage":
                            config.hunter.nightdmgincrease = (double)value;
                            break;
                        // Fisher
                        case "fishermaxlevel":
                            config.fisher.maxlvl = (int)value;
                            break;
                        case "fishercost":
                            config.fisher.pointcoststart = (int)value;
                            break;
                        case "fishercostmultiplier":
                            config.fisher.costmultiplier = (int)value;
                            break;
                        case "fisheramount":
                            config.fisher.fishamountincrease = (double)value;
                            break;
                        case "fisheritem":
                            config.fisher.itemamountincrease = (double)value;
                            break;
                        // Crafter
                        case "craftermaxlevel":
                            config.crafter.maxlvl = (int)value;
                            break;
                        case "craftercost":
                            config.crafter.pointcoststart = (int)value;
                            break;
                        case "craftercostmultiplier":
                            config.crafter.costmultiplier = (int)value;
                            break;
                        case "crafterspeed":
                            config.crafter.craftspeed = (double)value;
                            break;
                        case "craftercosts":
                            config.crafter.craftcost = (double)value;
                            break;
                        case "crafterrepair":
                            config.crafter.repairincrease = (double)value;
                            break;
                        case "crafterrepaircost":
                            config.crafter.repaircost = (double)value;
                            break;
                        case "craftercondtition":
                            config.crafter.conditionchance = (double)value;
                            break;
                        case "craftercondtitionamt":
                            config.crafter.conditionamount = (double)value;
                            break;
                        // Framer
                        case "framermaxlevel":
                            config.framer.maxlvl = (int)value;
                            break;
                        case "framercost":
                            config.framer.pointcoststart = (int)value;
                            break;
                        case "framercostmultiplier":
                            config.framer.costmultiplier = (int)value;
                            break;
                        case "framerupgrade":
                            config.framer.upgradecost = (double)value;
                            break;
                        case "framerrepair":
                            config.framer.repaircost = (double)value;
                            break;
                        case "framertime":
                            config.framer.repairtime = (double)value;
                            break;
                        // Medic
                        case "medicmaxlevel":
                            config.medic.maxlvl = (int)value;
                            break;
                        case "mediccost":
                            config.medic.pointcoststart = (int)value;
                            break;
                        case "mediccostmultiplier":
                            config.medic.costmultiplier = (int)value;
                            break;
                        case "medicrevival":
                            config.medic.revivehp = (double)value;
                            break;
                        case "medicrecover":
                            config.medic.recoverhp = (double)value;
                            break;
                        case "mediccraft":
                            config.medic.crafttime = (double)value;
                            break;
                        case "medictools":
                            config.medic.tools = (double)value;
                            break;
                        // Tamer
                        case "tamerenable":
                            config.tamer.enabletame = setting;
                            break;
                        case "tamermaxlevel":
                            config.tamer.maxlvl = (int)value;
                            break;
                        case "tamercost":
                            config.tamer.pointcoststart = (int)value;
                            break;
                        case "tamercostmultiplier":
                            config.tamer.costmultiplier = (int)value;
                            break;
                        case "tamerchicken":
                            config.tamer.tamechicken = setting;
                            break;
                        case "tamerboar":
                            config.tamer.tameboar = setting;
                            break;
                        case "tamerstag":
                            config.tamer.tamestag = setting;
                            break;
                        case "tamerwolf":
                            config.tamer.tamewolf = setting;
                            break;
                        case "tamerbear":
                            config.tamer.tamebear = setting;
                            break;
                        case "tamerchickenlevel":
                            config.tamer.chickenlevel = (int)value;
                            break;
                        case "tamerboarlevel":
                            config.tamer.boarlevel = (int)value;
                            break;
                        case "tamerstaglevel":
                            config.tamer.staglevel = (int)value;
                            break;
                        case "tamerwolflevel":
                            config.tamer.wolflevel = (int)value;
                            break;
                        case "tamerbearlevel":
                            config.tamer.bearlevel = (int)value;
                            break;

                    }
                    DestroyUi(player, XPerienceAdminPanelSkills);
                    AdminSkillsPage(player);
                    break;
                case "timercolor":
                    switch (option)
                    {
                        case "defaultliveuimoveable":
                            config.defaultOptions.liveuistatslocationmoveable = setting;
                            break;
                        case "defaultliveui":
                            config.defaultOptions.liveuistatslocation = (int)value;
                            break;
                        case "showchatprofile":
                            config.defaultOptions.showchatprofileonconnect = setting;
                            break;
                        case "armorchat":
                            config.defaultOptions.disablearmorchat = setting;
                            break;
                        case "defaultrestristresets":
                            config.defaultOptions.restristresets = setting;
                            break;
                        case "defaultstattimer":
                            config.defaultOptions.resetminsstats = (int)value;
                            break;
                        case "defaultskilltimer":
                            config.defaultOptions.resetminsskills = (int)value;
                            break;
                        case "defaultvipstattimer":
                            config.defaultOptions.vipresetminstats = (int)value;
                            break;
                        case "defaultvipskilltimer":
                            config.defaultOptions.vipresetminsskills = (int)value;
                            break;
                        case "defaultplayerfixdata":
                            config.defaultOptions.playerfixdatatimer = (int)value;
                            break;
                        case "defaultadminbypass":
                            config.defaultOptions.bypassadminreset = setting;
                            break;
                        case "defaultuicolor":
                            switch ((int)value)
                            {
                                case 1:
                                    color = "blue";
                                    break;
                                case 2:
                                    color = "cyan";
                                    break;
                                case 3:
                                    color = "gray";
                                    break;
                                case 4:
                                    color = "green";
                                    break;
                                case 5:
                                    color = "magenta";
                                    break;
                                case 6:
                                    color = "red";
                                    break;
                                case 7:
                                    color = "white";
                                    break;
                                case 8:
                                    color = "yellow";
                                    break;
                            }
                            config.uitextColor.defaultcolor = color;
                            break;
                        case "leveluicolor":
                            switch ((int)value)
                            {
                                case 1:
                                    color = "blue";
                                    break;
                                case 2:
                                    color = "cyan";
                                    break;
                                case 3:
                                    color = "gray";
                                    break;
                                case 4:
                                    color = "green";
                                    break;
                                case 5:
                                    color = "magenta";
                                    break;
                                case 6:
                                    color = "red";
                                    break;
                                case 7:
                                    color = "white";
                                    break;
                                case 8:
                                    color = "yellow";
                                    break;
                            }
                            config.uitextColor.level = color;
                            break;
                        case "xpuicolor":
                            switch ((int)value)
                            {
                                case 1:
                                    color = "blue";
                                    break;
                                case 2:
                                    color = "cyan";
                                    break;
                                case 3:
                                    color = "gray";
                                    break;
                                case 4:
                                    color = "green";
                                    break;
                                case 5:
                                    color = "magenta";
                                    break;
                                case 6:
                                    color = "red";
                                    break;
                                case 7:
                                    color = "white";
                                    break;
                                case 8:
                                    color = "yellow";
                                    break;
                            }
                            config.uitextColor.experience = color;
                            break;
                        case "nextlvluicolor":
                            switch ((int)value)
                            {
                                case 1:
                                    color = "blue";
                                    break;
                                case 2:
                                    color = "cyan";
                                    break;
                                case 3:
                                    color = "gray";
                                    break;
                                case 4:
                                    color = "green";
                                    break;
                                case 5:
                                    color = "magenta";
                                    break;
                                case 6:
                                    color = "red";
                                    break;
                                case 7:
                                    color = "white";
                                    break;
                                case 8:
                                    color = "yellow";
                                    break;
                            }
                            config.uitextColor.nextlevel = color;
                            break;
                        case "remainuicolor":
                            switch ((int)value)
                            {
                                case 1:
                                    color = "blue";
                                    break;
                                case 2:
                                    color = "cyan";
                                    break;
                                case 3:
                                    color = "gray";
                                    break;
                                case 4:
                                    color = "green";
                                    break;
                                case 5:
                                    color = "magenta";
                                    break;
                                case 6:
                                    color = "red";
                                    break;
                                case 7:
                                    color = "white";
                                    break;
                                case 8:
                                    color = "yellow";
                                    break;
                            }
                            config.uitextColor.remainingxp = color;
                            break;
                        case "ssluicolor":
                            switch ((int)value)
                            {
                                case 1:
                                    color = "blue";
                                    break;
                                case 2:
                                    color = "cyan";
                                    break;
                                case 3:
                                    color = "gray";
                                    break;
                                case 4:
                                    color = "green";
                                    break;
                                case 5:
                                    color = "magenta";
                                    break;
                                case 6:
                                    color = "red";
                                    break;
                                case 7:
                                    color = "white";
                                    break;
                                case 8:
                                    color = "yellow";
                                    break;
                            }
                            config.uitextColor.statskilllevels = color;
                            break;
                        case "perksuicolor":
                            switch ((int)value)
                            {
                                case 1:
                                    color = "blue";
                                    break;
                                case 2:
                                    color = "cyan";
                                    break;
                                case 3:
                                    color = "gray";
                                    break;
                                case 4:
                                    color = "green";
                                    break;
                                case 5:
                                    color = "magenta";
                                    break;
                                case 6:
                                    color = "red";
                                    break;
                                case 7:
                                    color = "white";
                                    break;
                                case 8:
                                    color = "yellow";
                                    break;
                            }
                            config.uitextColor.perks = color;
                            break;
                        case "upointsuicolor":
                            switch ((int)value)
                            {
                                case 1:
                                    color = "blue";
                                    break;
                                case 2:
                                    color = "cyan";
                                    break;
                                case 3:
                                    color = "gray";
                                    break;
                                case 4:
                                    color = "green";
                                    break;
                                case 5:
                                    color = "magenta";
                                    break;
                                case 6:
                                    color = "red";
                                    break;
                                case 7:
                                    color = "white";
                                    break;
                                case 8:
                                    color = "yellow";
                                    break;
                            }
                            config.uitextColor.unspentpoints = color;
                            break;
                        case "spointsuicolor":
                            switch ((int)value)
                            {
                                case 1:
                                    color = "blue";
                                    break;
                                case 2:
                                    color = "cyan";
                                    break;
                                case 3:
                                    color = "gray";
                                    break;
                                case 4:
                                    color = "green";
                                    break;
                                case 5:
                                    color = "magenta";
                                    break;
                                case 6:
                                    color = "red";
                                    break;
                                case 7:
                                    color = "white";
                                    break;
                                case 8:
                                    color = "yellow";
                                    break;
                            }
                            config.uitextColor.spentpoints = color;
                            break;
                        case "petsuicolor":
                            switch ((int)value)
                            {
                                case 1:
                                    color = "blue";
                                    break;
                                case 2:
                                    color = "cyan";
                                    break;
                                case 3:
                                    color = "gray";
                                    break;
                                case 4:
                                    color = "green";
                                    break;
                                case 5:
                                    color = "magenta";
                                    break;
                                case 6:
                                    color = "red";
                                    break;
                                case 7:
                                    color = "white";
                                    break;
                                case 8:
                                    color = "yellow";
                                    break;
                            }
                            config.uitextColor.pets = color;
                            break;
                    }
                    DestroyUi(player, XPerienceAdminPanelTimerColor);
                    AdminTimerColorPage(player);
                    break;
                case "othermods":
                    switch (option)
                    {
                        // Kill Records
                        case "krshowbutton":
                            config.xpBonus.showkrbutton = setting;
                            break;
                        case "krenable":
                            config.xpBonus.enablebonus = setting;
                            break;
                        case "krrequiredkills":
                            config.xpBonus.requiredkills = (int)value;
                            break;
                        case "krbonusamount":
                            config.xpBonus.bonusxp = (double)value;
                            break;
                        case "krbonusend":
                            config.xpBonus.endbonus = (int)value;
                            break;
                        case "krenablemulti":
                            config.xpBonus.multibonus = setting;
                            break;
                        case "krmultitype":
                            if (setting)
                            {
                                config.xpBonus.multibonustype = "increase";
                            }
                            else
                            {
                                config.xpBonus.multibonustype = "fixed";
                            }
                            break;
                        // Economics
                        case "econlevelup":
                            config.xpEcon.econlevelup = setting;
                            break;
                        case "econleveldown":
                            config.xpEcon.econleveldown = setting;
                            break;
                        case "econresetstats":
                            config.xpEcon.econresetstats = setting;
                            break;
                        case "econresetskills":
                            config.xpEcon.econresetskills = setting;
                            break;
                        case "econlevelreward":
                            config.xpEcon.econlevelreward = (int)value;
                            break;
                        case "econlevelreduction":
                            config.xpEcon.econlevelreduction = (int)value;
                            break;
                        case "econresetstatscost":
                            config.xpEcon.econresetstatscost = (int)value;
                            break;
                        case "econresetskillscost":
                            config.xpEcon.econresetskillscost = (int)value;
                            break;
                        // Server Rewards
                        case "srewardlevelup":
                            config.sRewards.srewardlevelup = setting;
                            break;
                        case "srewardleveldown":
                            config.sRewards.srewardleveldown = setting;
                            break;
                        case "srewardlevelupamt":
                            config.sRewards.srewardlevelupamt = (int)value;
                            break;
                        case "srewardleveldownamt":
                            config.sRewards.srewardleveldownamt = (int)value;
                            break;
                        // Clans
                        case "enableclanbonus":
                            config.xpclans.enableclanbonus = setting;
                            break;
                        case "enableclanreduction":
                            config.xpclans.enableclanreduction = setting;
                            break;
                        case "clanbonusamt":
                            config.xpclans.clanbonusamount = (double)value;
                            break;
                        case "clanreductionamt":
                            config.xpclans.clanreductionamount = (double)value;
                            break;
                        // UI Notify
                        case "uinotifyenable":
                            config.UiNotifier.useuinotify = setting;
                            break;
                        case "uinotifydisablechats":
                            config.UiNotifier.disablechats = setting;
                            break;
                        case "uinotifyxpgainloss":
                            config.UiNotifier.xpgainloss = setting;
                            break;
                        case "uinotifyxpgainlosstype":
                            config.UiNotifier.xpgainlosstype = (int)value;
                            break;
                        case "uinotifylevelgainloss":
                            config.UiNotifier.levelupdown = setting;
                            break;
                        case "uinotifylevelgainlosstype":
                            config.UiNotifier.levelupdowntype = (int)value;
                            break;
                        case "uinotifydodgeblock":
                            config.UiNotifier.dodgeblock = setting;
                            break;
                        case "uinotifydodgeblocktype":
                            config.UiNotifier.dodgeblocktype = (int)value;
                            break;
                        case "uinotifycritical":
                            config.UiNotifier.criticalhit = setting;
                            break;
                        case "uinotifycriticaltype":
                            config.UiNotifier.criticalhittype = (int)value;
                            break;
                    }
                    DestroyUi(player, XPerienceAdminPanelOtherMods);
                    AdminOtherModsPage(player);
                    break;
                case "sql":
                    switch (option)
                    {
                        case "sqlenable":
                            config.sql.enablesql = setting;
                            break;
                        case "sqlhost":
                            config.sql.SQLhost = arg.GetString(2);
                            break;     
                        case "sqlport":
                            config.sql.SQLport = (int)value;
                            break;     
                    }
                    DestroyUi(player, XPerienceAdminPanelSQL);
                    AdminSQLPage(player);
                    break;
                case "reset":
                    switch (option)
                    {
                        case "resetconfig":
                            player.ChatMessage(XPLang("adminresetconfig", player.UserIDString));
                            LoadDefaultConfig();
                            SaveConfig();
                            DestroyUi(player, XPerienceAdminPanelReset);
                            AdminResetPage(player);
                            break;
                        case "resetall":
                            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
                            _xperienceCache.Clear();
                            _lootCache.Clear();
                            _XPerienceData.Clear();
                            _LootContainData.Clear();
                            if (config.sql.enablesql)
                            {
                                DeleteSQL();
                            }
                            Interface.Oxide.ReloadPlugin("XPerience");
                            player.ChatMessage(XPLang("resetxperience", player.UserIDString));
                            break;    
                    }
                    DestroyUi(player, XPerienceAdminPanelSQL);
                    AdminSQLPage(player);
                    break;
            }
        }

        private void AdminControlPanel(BasePlayer player)
        {
            if (player == null) return;
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
            var ControlPanelelements = new CuiElementContainer();
            var height = 0.050f;
            // Main Screen
            ControlPanelelements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.1 0.1 0.1 0.99"
                },
                RectTransform =
                {
                    AnchorMin = $"0.0 0.0",
                    AnchorMax = $"1.0 1.0"
                },
                CursorEnabled = true
            }, "Overlay", XPerienceAdminPanelMain);
            // Top Label
            ControlPanelelements.Add(XPUILabel($"ⓍⓅerience {XPLang("adminpanel", player.UserIDString)}", 1, 0.060f, TextAnchor.MiddleLeft, 20, "0.01", "0.18", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelMain);
            // Navigation Menu
            ControlPanelelements.Add(XPUIPanel("0.0 0.0", "0.17 0.85", "1.0 1.0 1.0 0.0"), XPerienceAdminPanelMain, XPerienceAdminPanelMenu);
            ControlPanelelements.Add(XPUIButton("xp.admin main", 1, height, 18, "0.0 0.0 0.0 0.7", $"{XPLang("adminmenu_001", player.UserIDString)}", "0.03", "1", TextAnchor.MiddleCenter), XPerienceAdminPanelMenu);
            ControlPanelelements.Add(XPUIButton("xp.admin levelxp", 2, height, 18, "0.0 0.0 0.0 0.7", $"{XPLang("adminmenu_002", player.UserIDString)}", "0.03", "1", TextAnchor.MiddleCenter), XPerienceAdminPanelMenu);
            ControlPanelelements.Add(XPUIButton("xp.admin stats", 3, height, 18, "0.0 0.0 0.0 0.7", $"{XPLang("adminmenu_003", player.UserIDString)}", "0.03", "1", TextAnchor.MiddleCenter), XPerienceAdminPanelMenu);
            ControlPanelelements.Add(XPUIButton("xp.admin skills", 4, height, 18, "0.0 0.0 0.0 0.7", $"{XPLang("adminmenu_004", player.UserIDString)}", "0.03", "1", TextAnchor.MiddleCenter), XPerienceAdminPanelMenu);
            ControlPanelelements.Add(XPUIButton("xp.admin timercolor", 5, height, 18, "0.0 0.0 0.0 0.7", $"{XPLang("adminmenu_005", player.UserIDString)}", "0.03", "1", TextAnchor.MiddleCenter), XPerienceAdminPanelMenu);
            ControlPanelelements.Add(XPUIButton("xp.admin othermods", 6, height, 18, "0.0 0.0 0.0 0.7", $"{XPLang("adminmenu_012", player.UserIDString)}", "0.03", "1", TextAnchor.MiddleCenter), XPerienceAdminPanelMenu);
            ControlPanelelements.Add(XPUIButton("xp.admin sql", 7, height, 18, "0.0 0.0 0.0 0.7", $"{XPLang("adminmenu_006", player.UserIDString)}", "0.03", "1", TextAnchor.MiddleCenter), XPerienceAdminPanelMenu);
            ControlPanelelements.Add(XPUIButton("xp.admin save", 9, height, 18, "0.0 0.0 0.0 0.7", $"{XPLang("adminmenu_007", player.UserIDString)}", "0.03", "1", TextAnchor.MiddleCenter), XPerienceAdminPanelMenu);
            ControlPanelelements.Add(XPUIButton("xp.admin reload", 10, height, 18, "0.0 0.0 0.0 0.7", $"{XPLang("adminmenu_008", player.UserIDString)}", "0.03", "1", TextAnchor.MiddleCenter), XPerienceAdminPanelMenu);
            ControlPanelelements.Add(XPUIButton("xp.admin fix", 12, height, 18, "0.0 0.0 0.0 0.7", $"{XPLang("adminmenu_011", player.UserIDString)}", "0.03", "1", TextAnchor.MiddleCenter), XPerienceAdminPanelMenu);
            ControlPanelelements.Add(XPUIButton("xp.admin close", 14, height, 18, "0.0 0.0 0.0 0.7", $"{XPLang("adminmenu_009", player.UserIDString)}", "0.03", "1", TextAnchor.MiddleCenter), XPerienceAdminPanelMenu);
            //ControlPanelelements.Add(XPUIButton("xp.admin reset", 16, height, 18, "0.0 0.0 0.0 0.7", $"{XPLang("adminmenu_010", player.UserIDString)}", "0.03", "1", TextAnchor.MiddleCenter), XPerienceAdminPanelMenu);
            ControlPanelelements.Add(XPUIButton("xp.admin reset", 16, height, 18, "0.0 0.0 0.0 0.7", $"{XPLang("adminmenu_013", player.UserIDString)}", "0.03", "1", TextAnchor.MiddleCenter), XPerienceAdminPanelMenu);
            // UI End
            CuiHelper.AddUi(player, ControlPanelelements);
            return;
        }
        
        private void AdminInfoPage(BasePlayer player)
        {
            var ControlPanelelements = new CuiElementContainer();
            ControlPanelelements.Add(XPUIPanel("0.18 0.0", "1.0 1.0", "0.0 0.0 0.0 0.7"), XPerienceAdminPanelMain, XPerienceAdminPanelInfo);
            ControlPanelelements.Add(new CuiLabel
            {
                Text =
                {
                    Text = $"{XPLang("adminpanelinfonew", player.UserIDString)}",
                    FontSize = 18,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.02 0.0",
                    AnchorMax = "0.98 0.99"
                }
            }, XPerienceAdminPanelInfo);
            CuiHelper.AddUi(player, ControlPanelelements);
            return;
        }

        private void AdminLevelPage(BasePlayer player)
        {
            var ControlPanelelements = new CuiElementContainer();
            var height = 0.030f;
            int row = 5;
            ControlPanelelements.Add(XPUIPanel("0.18 0.0", "1.0 1.0", "0.0 0.0 0.0 0.7"), XPerienceAdminPanelMain, XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"{XPLang("adminxp_001", player.UserIDString)}", 1, 0.090f, TextAnchor.MiddleLeft, 18, "0.01", "1", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            // Main Level Settings
            ControlPanelelements.Add(XPUILabel($"{XPLang("adminxp_002", player.UserIDString)}", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.30", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            row++;
            // Level Start
            ControlPanelelements.Add(XPUILabel($"{XPLang("adminxp_003", player.UserIDString)}", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpLevel.levelstart}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp levelstart {config.xpLevel.levelstart + 5} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp levelstart {config.xpLevel.levelstart - 5} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Level Multiplier
            row++;
            ControlPanelelements.Add(XPUILabel($"XP Requirement Increase:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpLevel.levelmultiplier}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp levelmultiplier {config.xpLevel.levelmultiplier + 5} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp levelmultiplier {config.xpLevel.levelmultiplier - 5} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Max Level
            row++;
            ControlPanelelements.Add(XPUILabel($"Max Level:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpLevel.maxlevel}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp levelmax {config.xpLevel.maxlevel + 10} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp levelmax {config.xpLevel.maxlevel - 10} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Level XP Boost
            row++;
            ControlPanelelements.Add(XPUILabel($"Level XP Boost:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpLevel.levelxpboost}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp levelxpboost {config.xpLevel.levelxpboost + 0.01} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp levelxpboost {config.xpLevel.levelxpboost - 0.01} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Stat Points Per LVL
            row++;
            ControlPanelelements.Add(XPUILabel($"Stat Points Per LVL:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpLevel.statpointsperlvl}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp statpointsperlvl {config.xpLevel.statpointsperlvl + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp statpointsperlvl {config.xpLevel.statpointsperlvl - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Skill Points Per LVL
            row++;
            ControlPanelelements.Add(XPUILabel($"Skill Points Per LVL:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpLevel.skillpointsperlvl}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp skillpointsperlvl {config.xpLevel.skillpointsperlvl + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp skillpointsperlvl {config.xpLevel.skillpointsperlvl - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Night Bonus Settings
            row++;
            row++;
            ControlPanelelements.Add(XPUILabel($"[Night Settings]", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.30", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            // Enable / Disable
            row++;
            ControlPanelelements.Add(XPUILabel($"Enable Night Bonus:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.nightBonus.Enable}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp nightenable 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp nightenable 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Start Time
            row++;
            ControlPanelelements.Add(XPUILabel($"Start Time:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.nightBonus.StartTime}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp nightstart {config.nightBonus.StartTime + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp nightstart {config.nightBonus.StartTime - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // End Time
            row++;
            ControlPanelelements.Add(XPUILabel($"End Time:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.nightBonus.EndTime}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp nightend {config.nightBonus.EndTime + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp nightend {config.nightBonus.EndTime - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Bonus Amount
            row++;
            ControlPanelelements.Add(XPUILabel($"Bonus Percent:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.nightBonus.Bonus}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp nightbonus {config.nightBonus.Bonus + 0.1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp nightbonus {config.nightBonus.Bonus - 0.1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Enable Skill Boost
            row++;
            ControlPanelelements.Add(XPUILabel($"Enable Night Skills:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.nightBonus.enableskillboosts}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp nightskill 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp nightskill 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // XP Kill Settings
            row++;
            row++;
            ControlPanelelements.Add(XPUILabel($"[XP Kill Settings]", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.30", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            // Chicken
            row++;
            ControlPanelelements.Add(XPUILabel($"Chicken:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.chickenxp}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp chicken {config.xpGain.chickenxp + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp chicken {config.xpGain.chickenxp - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Fish
            row++;
            ControlPanelelements.Add(XPUILabel($"Fish:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.fishxp}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp fish {config.xpGain.fishxp + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp fish {config.xpGain.fishxp - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Boar
            row++;
            ControlPanelelements.Add(XPUILabel($"Boar:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.boarxp}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp boar {config.xpGain.boarxp + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp boar {config.xpGain.boarxp - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Stag
            row++;
            ControlPanelelements.Add(XPUILabel($"Stag:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.stagxp}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp stag {config.xpGain.stagxp + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp stag {config.xpGain.stagxp - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Wolf
            row++;
            ControlPanelelements.Add(XPUILabel($"Wolf:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.wolfxp}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp wolf {config.xpGain.wolfxp + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp wolf {config.xpGain.wolfxp - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Bear
            row++;
            ControlPanelelements.Add(XPUILabel($"Bear:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.bearxp}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp bear {config.xpGain.bearxp + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp bear {config.xpGain.bearxp - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Shark
            row++;
            ControlPanelelements.Add(XPUILabel($"Shark:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.sharkxp}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp shark {config.xpGain.sharkxp + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp shark {config.xpGain.sharkxp - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Horse
            row++;
            ControlPanelelements.Add(XPUILabel($"Horse:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.horsexp}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp horse {config.xpGain.horsexp + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp horse {config.xpGain.horsexp - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Scientist
            row++;
            ControlPanelelements.Add(XPUILabel($"Scientist:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.scientistxp}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp scientist {config.xpGain.scientistxp + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp scientist {config.xpGain.scientistxp - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Dweller
            row++;
            ControlPanelelements.Add(XPUILabel($"Dweller:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.dwellerxp}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp dweller {config.xpGain.dwellerxp + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp dweller {config.xpGain.dwellerxp - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Player
            row++;
            ControlPanelelements.Add(XPUILabel($"Player:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.playerxp}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp player {config.xpGain.playerxp + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp player {config.xpGain.playerxp - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Bradley
            row++;
            ControlPanelelements.Add(XPUILabel($"Bradley:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.bradley}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp bradley {config.xpGain.bradley + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp bradley {config.xpGain.bradley - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Heli
            row++;
            ControlPanelelements.Add(XPUILabel($"Patrol Helicopter:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.patrolhelicopter}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp heli {config.xpGain.patrolhelicopter + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp heli {config.xpGain.patrolhelicopter - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // XP Gathering/Looting
            int rowtwo = 5;
            ControlPanelelements.Add(XPUILabel($"[XP Gathering/Looting Settings]", rowtwo, height, TextAnchor.MiddleLeft, 15, "0.33", "0.66", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            rowtwo++;
            // Loot Containers
            ControlPanelelements.Add(XPUILabel($"Loot Containers:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.lootcontainerxp}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp loot {config.xpGain.lootcontainerxp + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp loot {config.xpGain.lootcontainerxp - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Underwater Loot
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Underwater Loot Containers:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.underwaterlootcontainerxp}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp lootu {config.xpGain.underwaterlootcontainerxp + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp lootu {config.xpGain.underwaterlootcontainerxp - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Locked Crate
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Locked Crate:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.lockedcratexp}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp lootlocked {config.xpGain.lockedcratexp + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp lootlocked {config.xpGain.lockedcratexp - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Hackable Crate
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Hackable Crate:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.hackablecratexp}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp loothacked {config.xpGain.hackablecratexp + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp loothacked {config.xpGain.hackablecratexp - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Animal Harvest
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Animal Harvest:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.animalharvestxp}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp aharvest {config.xpGain.animalharvestxp + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp aharvest {config.xpGain.animalharvestxp - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Corpse Harvest
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Corpse Harvest:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.corpseharvestxp}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp charvest {config.xpGain.corpseharvestxp + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp charvest {config.xpGain.corpseharvestxp - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Tree
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Tree:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGather.treexp}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp tree {config.xpGather.treexp + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp tree {config.xpGather.treexp - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Ore
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Ore:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGather.orexp}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp ore {config.xpGather.orexp + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp ore {config.xpGather.orexp - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Harvest
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Gathering:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGather.harvestxp}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp harvest {config.xpGather.harvestxp + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp harvest {config.xpGather.harvestxp - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Plant
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Plants:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGather.plantxp}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp plant {config.xpGather.plantxp + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp plant {config.xpGather.plantxp - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Disable Tool XP
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Disable Power Tool XP:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGather.noxptools}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp noxptools 0 true", rowtwo, height, 12, "0.0 1.0 0.0 0", "T", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp noxptools 0 false", rowtwo, height, 12, "1.0 0.0 0.0 0", "F", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // XP Crafting/Building
            rowtwo++;
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"[XP Crafting/Building Settings]", rowtwo, height, TextAnchor.MiddleLeft, 15, "0.33", "0.66", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            // Crafting
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Crafting:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpGain.craftingxp}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp crafting {config.xpGain.craftingxp + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp crafting {config.xpGain.craftingxp - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Wood Building
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Wood Structure:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpBuilding.woodstructure}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp woodbuild {config.xpBuilding.woodstructure + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp woodbuild {config.xpBuilding.woodstructure - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Stone Building
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Stone Structure:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpBuilding.stonestructure}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp stonebuild {config.xpBuilding.stonestructure + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp stonebuild {config.xpBuilding.stonestructure - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Metal Building
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Metal Structure:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpBuilding.metalstructure}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp metalbuild {config.xpBuilding.metalstructure + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp metalbuild {config.xpBuilding.metalstructure - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Armor Building
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Armor Structure:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpBuilding.armoredstructure}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp armorbuild {config.xpBuilding.armoredstructure + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp armorbuild {config.xpBuilding.armoredstructure - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // XP Reduce Settings
            rowtwo++;
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"[XP Reducer Settings]", rowtwo, height, TextAnchor.MiddleLeft, 15, "0.33", "0.66", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            // Enable Suicide Reduction
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Enable Suicide Reduction:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpReducer.suicidereduce}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp suicide 0 true", rowtwo, height, 12, "0.0 1.0 0.0 0", "T", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp suicide 0 false", rowtwo, height, 12, "1.0 0.0 0.0 0", "F", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Suicide Reduction Amount
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Suicide Reduction Percent:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpReducer.suicidereduceamount}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp suicideamt {config.xpReducer.suicidereduceamount + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp suicideamt {config.xpReducer.suicidereduceamount - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Enable Death Reduction
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Enable Death Reduction:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpReducer.deathreduce}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp death 0 true", rowtwo, height, 12, "0.0 1.0 0.0 0", "T", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp death 0 false", rowtwo, height, 12, "1.0 0.0 0.0 0", "F", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Death Reduction Amount
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Death Reduction Percent:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpReducer.deathreduceamount}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp deathamt {config.xpReducer.deathreduceamount + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp deathamt {config.xpReducer.deathreduceamount - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Mission Settings
            int rowthree = 5;
            ControlPanelelements.Add(XPUILabel($"[Mission XP Settings]", rowthree, height, TextAnchor.MiddleLeft, 15, "0.66", "0.99", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            // Mission Succeeded
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Mission Succeeded:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpMissions.missionsucceededxp}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.88", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp missionsucceeded {config.xpMissions.missionsucceededxp + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.88", "0.89", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp missionsucceeded {config.xpMissions.missionsucceededxp - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.90", "0.91", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Enable Mission Failed
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Enable Failed Reduction:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpMissions.missionfailed}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.88", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp missionfailed 0 true", rowthree, height, 12, "0.0 1.0 0.0 0", "T", "0.88", "0.89", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp missionfailed 0 false", rowthree, height, 12, "1.0 0.0 0.0 0", "F", "0.90", "0.91", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // Mission Failed
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Failed Reduction Amount:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpMissions.missionfailedxp}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.88", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp missionfailedxp {config.xpMissions.missionfailedxp + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.88", "0.89", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            ControlPanelelements.Add(XPUIButton($"xp.config levelxp missionfailedxp {config.xpMissions.missionfailedxp - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.90", "0.91", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelLevelXP);
            // End
            CuiHelper.AddUi(player, ControlPanelelements);
            return;
        }

        private void AdminStatsPage(BasePlayer player)
        {
            var ControlPanelelements = new CuiElementContainer();
            var height = 0.030f;
            int row = 5;
            var mentmaxlevel = "off";
            var dexmaxlevel = "off";
            var mightmaxlevel = "off";
            var chemmaxlevel = "off";
            ControlPanelelements.Add(XPUIPanel("0.18 0.0", "1.0 1.0", "0.0 0.0 0.0 0.7"), XPerienceAdminPanelMain, XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"Stat Settings - Note that these settings are what players gain per level of each stat.", 1, 0.090f, TextAnchor.MiddleLeft, 18, "0.01", "1", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            // Mentality Settings
            ControlPanelelements.Add(XPUILabel($"[Mentality Settings]", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.30", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            row++;
            // Max Level
            if (config.mentality.maxlvl > 0)
            {
                mentmaxlevel = config.mentality.maxlvl.ToString();
            }
            ControlPanelelements.Add(XPUILabel($"Max Level:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {mentmaxlevel}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mentalitymaxlevel {config.mentality.maxlvl + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            if (config.mentality.maxlvl > 0)
            {
                ControlPanelelements.Add(XPUIButton($"xp.config stats mentalitymaxlevel {config.mentality.maxlvl - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            }
            // Cost to Start
            row++;
            ControlPanelelements.Add(XPUILabel($"Point Cost To Start:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.mentality.pointcoststart}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mentalitycost {config.mentality.pointcoststart + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mentalitycost {config.mentality.pointcoststart - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Cost Multiplier
            row++;
            ControlPanelelements.Add(XPUILabel($"Cost Multiplier:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.mentality.costmultiplier}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mentalitycostmultiplier {config.mentality.costmultiplier + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mentalitycostmultiplier {config.mentality.costmultiplier - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Research Cost
            row++;
            ControlPanelelements.Add(XPUILabel($"Research Cost Reduction:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.mentality.researchcost}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mentalityresearchcost {config.mentality.researchcost + 0.1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mentalityresearchcost {config.mentality.researchcost - 0.1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Research Speed
            row++;
            ControlPanelelements.Add(XPUILabel($"Research Speed Reduction:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.mentality.researchspeed}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mentalityresearchspeed {config.mentality.researchspeed + 0.1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mentalityresearchspeed {config.mentality.researchspeed - 0.1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Critical Chance
            row++;
            ControlPanelelements.Add(XPUILabel($"Critical Chance Percent:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.mentality.criticalchance}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mentalitycriticalchance {config.mentality.criticalchance + 0.01} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mentalitycriticalchance {config.mentality.criticalchance - 0.01} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Enable / Disable Research
            row++;
            ControlPanelelements.Add(XPUILabel($"Use Other Research Mod:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.mentality.useotherresearchmod}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mentalityothermod 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mentalityothermod 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Dexterity Settings
            row++;
            row++;
            ControlPanelelements.Add(XPUILabel($"[Dexterity Settings]", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.30", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            row++;
            // Max Level
            if (config.dexterity.maxlvl > 0)
            {
                dexmaxlevel = config.dexterity.maxlvl.ToString();
            }
            ControlPanelelements.Add(XPUILabel($"Max Level:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {dexmaxlevel}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats dexteritymaxlevel {config.dexterity.maxlvl + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            if(config.dexterity.maxlvl > 0)
            { 
                ControlPanelelements.Add(XPUIButton($"xp.config stats dexteritymaxlevel {config.dexterity.maxlvl - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            }
            // Cost to Start
            row++;
            ControlPanelelements.Add(XPUILabel($"Point Cost To Start:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.dexterity.pointcoststart}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats dexteritycost {config.dexterity.pointcoststart + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats dexteritycost {config.dexterity.pointcoststart - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Cost Multiplier
            row++;
            ControlPanelelements.Add(XPUILabel($"Cost Multiplier:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.dexterity.costmultiplier}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats dexteritycostmultiplier {config.dexterity.costmultiplier + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats dexteritycostmultiplier {config.dexterity.costmultiplier - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Block Chance
            row++;
            ControlPanelelements.Add(XPUILabel($"Block Chance Percent:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.dexterity.blockchance}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats dexterityblock {config.dexterity.blockchance + 0.01} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats dexterityblock {config.dexterity.blockchance - 0.01} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Block Amount
            row++;
            ControlPanelelements.Add(XPUILabel($"Block Amount Percent:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.dexterity.blockamount}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats dexterityblockamt {config.dexterity.blockamount + 0.1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats dexterityblockamt {config.dexterity.blockamount - 0.1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Dodge Chance
            row++;
            ControlPanelelements.Add(XPUILabel($"Dodge Chance Percent:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.dexterity.dodgechance}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats dexteritydodge {config.dexterity.dodgechance + 0.01} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats dexteritydodge {config.dexterity.dodgechance - 0.01} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Armor Damage
            row++;
            ControlPanelelements.Add(XPUILabel($"Armor Damage Reduction:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.dexterity.reducearmordmg}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats dexterityarmor {config.dexterity.reducearmordmg + 0.01} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats dexterityarmor {config.dexterity.reducearmordmg - 0.01} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Chemist Settings
            /*
            row++;
            row++;
            ControlPanelelements.Add(XPUILabel($"[Chemist Settings]", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.30", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            row++;
            // Max Level
            if (config.chemist.maxlvl > 0)
            {
                chemmaxlevel = config.chemist.maxlvl.ToString();
            }
            ControlPanelelements.Add(XPUILabel($"Max Level:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {chemmaxlevel}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats chemistmaxlevel {config.chemist.maxlvl + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            if (config.chemist.maxlvl > 0)
            {
                ControlPanelelements.Add(XPUIButton($"xp.config stats chemistmaxlevel {config.chemist.maxlvl - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            }
            // Cost to Start
            row++;
            ControlPanelelements.Add(XPUILabel($"Point Cost To Start:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.chemist.pointcoststart}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats chemistcost {config.chemist.pointcoststart + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats chemistcost {config.chemist.pointcoststart - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Cost Multiplier
            row++;
            ControlPanelelements.Add(XPUILabel($"Cost Multiplier:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.chemist.costmultiplier}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats chemistcostmultiplier {config.chemist.costmultiplier + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats chemistcostmultiplier {config.chemist.costmultiplier - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Craft Speed
            row++;
            ControlPanelelements.Add(XPUILabel($"Craft Speed Reduction:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.chemist.crafttime}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats chemistspeed {config.chemist.crafttime + 0.1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats chemistspeed {config.chemist.crafttime - 0.1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            */
            // Might
            int rowtwo = 5;
            ControlPanelelements.Add(XPUILabel($"[Might Settings]", rowtwo, height, TextAnchor.MiddleLeft, 15, "0.33", "0.66", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            // Max Level
            rowtwo++;
            if (config.might.maxlvl > 0)
            {
                mightmaxlevel = config.might.maxlvl.ToString();
            }
            ControlPanelelements.Add(XPUILabel($"Max Level:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {mightmaxlevel}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightmaxlevel {config.might.maxlvl + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            if (config.might.maxlvl > 0)
            {
                ControlPanelelements.Add(XPUIButton($"xp.config stats mightmaxlevel {config.might.maxlvl - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);   
            }
            // Max Cost to Start
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Point Cost To Start:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.might.pointcoststart}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightcost {config.might.pointcoststart + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightcost {config.might.pointcoststart - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Cost Multiplier
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Cost Multiplier:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.might.costmultiplier}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightcostmultiplier {config.might.costmultiplier + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightcostmultiplier {config.might.costmultiplier - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Armor
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Armor:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.might.armor}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightarmor {config.might.armor + 0.1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightarmor {config.might.armor - 0.1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Melee Dmg
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Melee Damage Increase:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.might.meleedmg}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightmelee {config.might.meleedmg + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightmelee {config.might.meleedmg - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Metabolism
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Metabolism (Hunger/Thirst):", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.might.metabolism}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightmeta {config.might.metabolism + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightmeta {config.might.metabolism - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Bleeding
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Bleed Reduction:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.might.bleedreduction}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightbleed {config.might.bleedreduction + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightbleed {config.might.bleedreduction - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Radiation
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Radiation Reduction:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.might.radreduction}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightrad {config.might.radreduction + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightrad {config.might.radreduction - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Heat
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Heat Reduction:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.might.heattolerance}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightheat {config.might.heattolerance + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightheat {config.might.heattolerance - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // Cold
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Cold Reduction:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUILabel($"|       {config.might.coldtolerance}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightcold {config.might.coldtolerance + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelStats);
            ControlPanelelements.Add(XPUIButton($"xp.config stats mightcold {config.might.coldtolerance - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelStats);
            // End
            CuiHelper.AddUi(player, ControlPanelelements);
            return;
        }

        private void AdminSkillsPage(BasePlayer player)
        {
            var ControlPanelelements = new CuiElementContainer();
            var height = 0.027f;
            int row = 5;
            var woodcuttermaxlevel = "off";
            var smithymaxlevel = "off";
            var minermaxlevel = "off";
            var fishermaxlevel = "off";
            var foragermaxlevel = "off";
            var huntermaxlevel = "off";
            var craftermaxlevel = "off";
            var framermaxlevel = "off";
            var medicmaxlevel = "off";
            ControlPanelelements.Add(XPUIPanel("0.18 0.0", "1.0 1.0", "0.0 0.0 0.0 0.7"), XPerienceAdminPanelMain, XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"Skills Settings - Note that these settings are what players gain per level of each skill.", 1, 0.090f, TextAnchor.MiddleLeft, 18, "0.01", "1", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            // WoodCutter Settings
            ControlPanelelements.Add(XPUILabel($"[WoodCutter Settings]", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.30", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            row++;
            // Max Level
            if (config.woodcutter.maxlvl > 0)
            {
                woodcuttermaxlevel = config.woodcutter.maxlvl.ToString();
            }
            ControlPanelelements.Add(XPUILabel($"Max Level:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {woodcuttermaxlevel}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills woodcuttermaxlevel {config.woodcutter.maxlvl + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            if (config.woodcutter.maxlvl > 0)
            {
                ControlPanelelements.Add(XPUIButton($"xp.config skills woodcuttermaxlevel {config.woodcutter.maxlvl - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            }
            // Cost to Start
            row++;
            ControlPanelelements.Add(XPUILabel($"Point Cost To Start:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.woodcutter.pointcoststart}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills woodcuttercost {config.woodcutter.pointcoststart + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills woodcuttercost {config.woodcutter.pointcoststart - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Cost Multiplier
            row++;
            ControlPanelelements.Add(XPUILabel($"Cost Multiplier:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.woodcutter.costmultiplier}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills woodcuttercostmultiplier {config.woodcutter.costmultiplier + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills woodcuttercostmultiplier {config.woodcutter.costmultiplier - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Gather Rate
            row++;
            ControlPanelelements.Add(XPUILabel($"Gather Rate:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.woodcutter.gatherrate}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills woodcuttergatherrate {config.woodcutter.gatherrate + 0.1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills woodcuttergatherrate {config.woodcutter.gatherrate - 0.1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Bonus Amount
            row++;
            ControlPanelelements.Add(XPUILabel($"Bonus Amount:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.woodcutter.bonusincrease}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills woodcutterbonus {config.woodcutter.bonusincrease + 0.1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills woodcutterbonus {config.woodcutter.bonusincrease - 0.1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Apple Chance
            row++;
            ControlPanelelements.Add(XPUILabel($"Apple Chance:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.woodcutter.applechance}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills woodcutterapple {config.woodcutter.applechance + 0.01} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills woodcutterapple {config.woodcutter.applechance - 0.01} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Smithy Settings
            row++;
            row++;
            ControlPanelelements.Add(XPUILabel($"[Smithy Settings]", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.30", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            // Max Level
            row++;
            if (config.smithy.maxlvl > 0)
            {
                smithymaxlevel = config.smithy.maxlvl.ToString();
            }
            ControlPanelelements.Add(XPUILabel($"Max Level:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {smithymaxlevel}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills smithymaxlevel {config.smithy.maxlvl + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            if (config.smithy.maxlvl > 0)
            {
                ControlPanelelements.Add(XPUIButton($"xp.config skills smithymaxlevel {config.smithy.maxlvl - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            }
            // Cost to Start
            row++;
            ControlPanelelements.Add(XPUILabel($"Point Cost To Start:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.smithy.pointcoststart}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills smithycost {config.smithy.pointcoststart + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills smithycost {config.smithy.pointcoststart - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Cost Multiplier
            row++;
            ControlPanelelements.Add(XPUILabel($"Cost Multiplier:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.smithy.costmultiplier}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills smithycostmultiplier {config.smithy.costmultiplier + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills smithycostmultiplier {config.smithy.costmultiplier - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Production Rate
            row++;
            ControlPanelelements.Add(XPUILabel($"Production Rate:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.smithy.productionrate}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills smithyprate {config.smithy.productionrate + 0.1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills smithyprate {config.smithy.productionrate - 0.1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Fuel Consumption
            row++;
            ControlPanelelements.Add(XPUILabel($"Fuel Consumption:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.smithy.fuelconsumption}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills smithyfuel {config.smithy.fuelconsumption + 0.1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills smithyfuel {config.smithy.fuelconsumption - 0.1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Miner Settings
            row++;
            row++;
            ControlPanelelements.Add(XPUILabel($"[Miner Settings]", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.30", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            // Max Level
            row++;
            if (config.miner.maxlvl > 0)
            {
                minermaxlevel = config.miner.maxlvl.ToString();
            }
            ControlPanelelements.Add(XPUILabel($"Max Level:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {minermaxlevel}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills minermaxlevel {config.miner.maxlvl + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            if (config.miner.maxlvl > 0)
            {
                ControlPanelelements.Add(XPUIButton($"xp.config skills minermaxlevel {config.miner.maxlvl - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            }
            // Cost to Start
            row++;
            ControlPanelelements.Add(XPUILabel($"Point Cost To Start:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.miner.pointcoststart}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills minercost {config.miner.pointcoststart + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills minercost {config.miner.pointcoststart - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Cost Multiplier
            row++;
            ControlPanelelements.Add(XPUILabel($"Cost Multiplier:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.miner.costmultiplier}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills minercostmultiplier {config.miner.costmultiplier + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills minercostmultiplier {config.miner.costmultiplier - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Gather Rate
            row++;
            ControlPanelelements.Add(XPUILabel($"Production Rate:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.miner.gatherrate}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills minergatherrate {config.miner.gatherrate + 0.1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills minergatherrate {config.miner.gatherrate - 0.1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Bonus Amount
            row++;
            ControlPanelelements.Add(XPUILabel($"Bonus Amount:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.miner.bonusincrease}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills minerbonus {config.miner.bonusincrease + 0.1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills minerbonus {config.miner.bonusincrease - 0.1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Fuel Consumption
            row++;
            ControlPanelelements.Add(XPUILabel($"Fuel Consumption:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.miner.fuelconsumption}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills minerfuel {config.miner.fuelconsumption + 0.1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills minerfuel {config.miner.fuelconsumption - 0.1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Fisher Settings
            row++;
            row++;
            ControlPanelelements.Add(XPUILabel($"[Fisher Settings]", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.30", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            // Max Level
            row++;
            if (config.fisher.maxlvl > 0)
            {
                fishermaxlevel = config.fisher.maxlvl.ToString();
            }
            ControlPanelelements.Add(XPUILabel($"Max Level:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {fishermaxlevel}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills fishermaxlevel {config.fisher.maxlvl + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            if (config.woodcutter.maxlvl > 0)
            {
                ControlPanelelements.Add(XPUIButton($"xp.config skills fishermaxlevel {config.fisher.maxlvl - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            }
            // Cost to Start
            row++;
            ControlPanelelements.Add(XPUILabel($"Point Cost To Start:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.fisher.pointcoststart}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills fishercost {config.fisher.pointcoststart + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills fishercost {config.fisher.pointcoststart - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Cost Multiplier
            row++;
            ControlPanelelements.Add(XPUILabel($"Cost Multiplier:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.fisher.costmultiplier}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills fishercostmultiplier {config.fisher.costmultiplier + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills fishercostmultiplier {config.fisher.costmultiplier - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Fish Amount
            row++;
            ControlPanelelements.Add(XPUILabel($"Fish Amount:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.fisher.fishamountincrease}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills fisheramount {config.fisher.fishamountincrease + 0.05} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills fisheramount {config.fisher.fishamountincrease - 0.05} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Item Amount
            row++;
            ControlPanelelements.Add(XPUILabel($"Item Amount:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.fisher.itemamountincrease}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills fisheritem {config.fisher.itemamountincrease + 0.05} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills fisheritem {config.fisher.itemamountincrease - 0.05} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Forager
            int rowtwo = 5;
            ControlPanelelements.Add(XPUILabel($"[Forager Settings]", rowtwo, height, TextAnchor.MiddleLeft, 15, "0.33", "0.66", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            // Max Level
            rowtwo++;
            if (config.forager.maxlvl > 0)
            {
                foragermaxlevel = config.forager.maxlvl.ToString();
            }
            ControlPanelelements.Add(XPUILabel($"Max Level:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {foragermaxlevel}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills foragermaxlevel {config.forager.maxlvl + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            if (config.forager.maxlvl > 0)
            {
               ControlPanelelements.Add(XPUIButton($"xp.config skills foragermaxlevel {config.forager.maxlvl - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            }
            // Max Cost to Start
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Point Cost To Start:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.forager.pointcoststart}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills foragercost {config.forager.pointcoststart + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills foragercost {config.forager.pointcoststart - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Cost Multiplier
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Cost Multiplier:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.forager.costmultiplier}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills foragercostmultiplier {config.forager.costmultiplier + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills foragercostmultiplier {config.forager.costmultiplier - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Gather Rate
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Gather Rate:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.forager.gatherrate}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills foragergatherrate {config.forager.gatherrate + 0.1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills foragergatherrate {config.forager.gatherrate - 0.1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Seed Chance
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Increase Seed Chance/Amount:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.forager.chanceincrease}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills foragerseed {config.forager.chanceincrease + 0.1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills foragerseed {config.forager.chanceincrease - 0.1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Seed Chance
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Random Item Chance:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.forager.randomchance}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills foragerrandom {config.forager.randomchance + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills foragerrandom {config.forager.randomchance - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Hunter
            rowtwo++;
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"[Hunter Settings]", rowtwo, height, TextAnchor.MiddleLeft, 15, "0.33", "0.66", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            // Max Level
            rowtwo++;
            if (config.hunter.maxlvl > 0)
            {
               huntermaxlevel = config.hunter.maxlvl.ToString();
            }
            ControlPanelelements.Add(XPUILabel($"Max Level:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {huntermaxlevel}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills huntermaxlevel {config.hunter.maxlvl + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            if (config.hunter.maxlvl > 0)
            {
                ControlPanelelements.Add(XPUIButton($"xp.config skills huntermaxlevel {config.hunter.maxlvl - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            }
            // Max Cost to Start
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Point Cost To Start:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.hunter.pointcoststart}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills huntercost {config.hunter.pointcoststart + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills huntercost {config.hunter.pointcoststart - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Cost Multiplier
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Cost Multiplier:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.hunter.costmultiplier}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills huntercostmultiplier {config.hunter.costmultiplier + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills huntercostmultiplier {config.hunter.costmultiplier - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Gather Rate
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Gather Rate:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.hunter.gatherrate}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills huntergatherrate {config.hunter.gatherrate + 0.1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills huntergatherrate {config.hunter.gatherrate - 0.1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Bonus Amount
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Bonus Amount:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.hunter.bonusincrease}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills hunterbonus {config.hunter.bonusincrease + 0.1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills hunterbonus {config.hunter.bonusincrease - 0.1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Damage Increase Wildlife
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Damage Increase (Wildlife):", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.hunter.damageincrease}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills hunterdamage {config.hunter.damageincrease + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills hunterdamage {config.hunter.damageincrease - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Damage Increase Night
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Night Damage Increase (Wildlife):", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.hunter.nightdmgincrease}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills hunterndamage {config.hunter.nightdmgincrease + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills hunterndamage {config.hunter.nightdmgincrease - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Crafter
            rowtwo++;
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"[Crafter Settings]", rowtwo, height, TextAnchor.MiddleLeft, 15, "0.33", "0.66", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            // Max Level
            rowtwo++;
            if (config.crafter.maxlvl > 0)
            {
                craftermaxlevel = config.crafter.maxlvl.ToString();
            }
            ControlPanelelements.Add(XPUILabel($"Max Level:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {craftermaxlevel}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills craftermaxlevel {config.crafter.maxlvl + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            if (config.crafter.maxlvl > 0)
            {
                ControlPanelelements.Add(XPUIButton($"xp.config skills craftermaxlevel {config.crafter.maxlvl - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            }
            // Max Cost to Start
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Point Cost To Start:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.crafter.pointcoststart}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills craftercost {config.crafter.pointcoststart + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills craftercost {config.crafter.pointcoststart - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Cost Multiplier
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Cost Multiplier:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.crafter.costmultiplier}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills craftercostmultiplier {config.crafter.costmultiplier + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills craftercostmultiplier {config.crafter.costmultiplier - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Craft Speed
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Crafting Speed:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.crafter.craftspeed}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills crafterspeed {config.crafter.craftspeed + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills crafterspeed {config.crafter.craftspeed - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Craft Cost
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Crafting Cost:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.crafter.craftcost}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills craftercosts {config.crafter.craftcost + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills craftercosts {config.crafter.craftcost - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Repair Increase
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Repair Increase:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.crafter.repairincrease}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills crafterrepair {config.crafter.repairincrease + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills crafterrepair {config.crafter.repairincrease - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Repair Cost
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Repair Cost:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.crafter.repaircost}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills crafterrepaircost {config.crafter.repaircost + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills crafterrepaircost {config.crafter.repaircost - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Condition Chance
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Condition Chance:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.crafter.conditionchance}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills craftercondtition {config.crafter.conditionchance + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills craftercondtition {config.crafter.conditionchance - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Condition Amount
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Condition Amount:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       +{config.crafter.conditionamount * 100}%", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills craftercondtitionamt {config.crafter.conditionamount + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills craftercondtitionamt {config.crafter.conditionamount - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Framer Settings
            rowtwo++;
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"[Framer Settings]", rowtwo, height, TextAnchor.MiddleLeft, 15, "0.33", "0.66", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            // Max Level
            rowtwo++;
            if (config.framer.maxlvl > 0)
            {
                framermaxlevel = config.framer.maxlvl.ToString();
            }
            ControlPanelelements.Add(XPUILabel($"Max Level:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {framermaxlevel}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills framermaxlevel {config.framer.maxlvl + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            if (config.framer.maxlvl > 0)
            {
                ControlPanelelements.Add(XPUIButton($"xp.config skills framermaxlevel {config.framer.maxlvl - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            }
            // Max Cost to Start
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Point Cost To Start:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.framer.pointcoststart}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills framercost {config.framer.pointcoststart + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills framercost {config.framer.pointcoststart - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Cost Multiplier
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Cost Multiplier:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.framer.costmultiplier}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills framercostmultiplier {config.framer.costmultiplier + 1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills framercostmultiplier {config.framer.costmultiplier - 1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Upgrade Cost
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Upgrade Cost:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.framer.upgradecost}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills framerupgrade {config.framer.upgradecost + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills framerupgrade {config.framer.upgradecost - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Repair Cost
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Repair Cost:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.framer.repaircost}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills framerrepair {config.framer.repaircost + 0.01} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills framerrepair {config.framer.repaircost - 0.01} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Repair Time
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Repair Time:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.33", "0.48", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.framer.repairtime}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.48", "0.53", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills framertime {config.framer.repairtime + 0.1} false", rowtwo, height, 18, "0.0 1.0 0.0 0", "⇧", "0.53", "0.54", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills framertime {config.framer.repairtime - 0.1} false", rowtwo, height, 18, "1.0 0.0 0.0 0", "⇩", "0.55", "0.56", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Medic Settings
            int rowthree = 5;
            ControlPanelelements.Add(XPUILabel($"[Medic Settings]", rowthree, height, TextAnchor.MiddleLeft, 15, "0.66", "0.99", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            // Max Level
            rowthree++;
            if (config.medic.maxlvl > 0)
            {
                medicmaxlevel = config.medic.maxlvl.ToString();
            }
            ControlPanelelements.Add(XPUILabel($"Max Level:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {medicmaxlevel}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills medicmaxlevel {config.medic.maxlvl + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            if (config.framer.maxlvl > 0)
            {
                ControlPanelelements.Add(XPUIButton($"xp.config skills medicmaxlevel {config.medic.maxlvl - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            }
            // Max Cost to Start
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Point Cost To Start:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.medic.pointcoststart}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills mediccost {config.medic.pointcoststart + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills mediccost {config.medic.pointcoststart - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Cost Multiplier
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Cost Multiplier:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.medic.costmultiplier}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills mediccostmultiplier {config.medic.costmultiplier + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills mediccostmultiplier {config.medic.costmultiplier - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Revive HP
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Revival Amount:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.medic.revivehp}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills medicrevival {config.medic.revivehp + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills medicrevival {config.medic.revivehp - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Recover HP
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Recover Amount:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.medic.recoverhp}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills medicrecover {config.medic.recoverhp + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills medicrecover {config.medic.recoverhp - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Medic Tools
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Medical Tools:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.medic.tools}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills medictools {config.medic.tools + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills medictools {config.medic.tools - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // RCraft Speed
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Mixing Table Speed:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.medic.crafttime}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills mediccraft {config.medic.crafttime + 0.1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills mediccraft {config.medic.crafttime - 0.1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Tamer Settings
            rowthree++;
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"[Tamer Settings (Requires Pets Mod)]", rowthree, height, TextAnchor.MiddleLeft, 15, "0.66", "0.99", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            // Enable / Disable
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Enable Pets:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.tamer.enabletame}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerenable 0 true", rowthree, height, 12, "0.0 1.0 0.0 0", "T", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerenable 0 false", rowthree, height, 12, "1.0 0.0 0.0 0", "F", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Max Level
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Max Level:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.tamer.maxlvl}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamermaxlevel {config.tamer.maxlvl + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamermaxlevel {config.tamer.maxlvl - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Max Cost to Start
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Point Cost To Start:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.tamer.pointcoststart}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamercost {config.tamer.pointcoststart + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamercost {config.tamer.pointcoststart - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Cost Multiplier
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Cost Multiplier:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.tamer.costmultiplier}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamercostmultiplier {config.tamer.costmultiplier + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamercostmultiplier {config.tamer.costmultiplier - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Enable Chicken
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Chicken:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.tamer.tamechicken}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerchicken 0 true", rowthree, height, 12, "0.0 1.0 0.0 0", "T", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerchicken 0 false", rowthree, height, 12, "1.0 0.0 0.0 0", "F", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Enable Boar
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Boar:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.tamer.tameboar}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerboar 0 true", rowthree, height, 12, "0.0 1.0 0.0 0", "T", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerboar 0 false", rowthree, height, 12, "1.0 0.0 0.0 0", "F", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Enable Stag
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Stag:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.tamer.tamestag}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerstag 0 true", rowthree, height, 12, "0.0 1.0 0.0 0", "T", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerstag 0 false", rowthree, height, 12, "1.0 0.0 0.0 0", "F", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Enable Wolf
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Wolf:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.tamer.tamewolf}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerwolf 0 true", rowthree, height, 12, "0.0 1.0 0.0 0", "T", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerwolf 0 false", rowthree, height, 12, "1.0 0.0 0.0 0", "F", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Enable Bear
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Bear:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.tamer.tamebear}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerbear 0 true", rowthree, height, 12, "0.0 1.0 0.0 0", "T", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerbear 0 false", rowthree, height, 12, "1.0 0.0 0.0 0", "F", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Chicken Level
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Chicken Level:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.tamer.chickenlevel}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerchickenlevel {config.tamer.chickenlevel + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerchickenlevel {config.tamer.chickenlevel - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Boar Level
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Boar Level:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.tamer.boarlevel}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerboarlevel {config.tamer.boarlevel + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerboarlevel {config.tamer.boarlevel - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Stag Level
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Stag Level:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.tamer.staglevel}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerstaglevel {config.tamer.staglevel + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerstaglevel {config.tamer.staglevel - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Wolf Level
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Wolf Level:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.tamer.wolflevel}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerwolflevel {config.tamer.wolflevel + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerwolflevel {config.tamer.wolflevel - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // Bear Level
            rowthree++;
            ControlPanelelements.Add(XPUILabel($"Bear Level:", rowthree, height, TextAnchor.MiddleLeft, 12, "0.66", "0.81", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUILabel($"|       {config.tamer.bearlevel}", rowthree, height, TextAnchor.MiddleLeft, 12, "0.81", "0.86", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerbearlevel {config.tamer.bearlevel + 1} false", rowthree, height, 18, "0.0 1.0 0.0 0", "⇧", "0.86", "0.87", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSkills);
            ControlPanelelements.Add(XPUIButton($"xp.config skills tamerbearlevel {config.tamer.bearlevel - 1} false", rowthree, height, 18, "1.0 0.0 0.0 0", "⇩", "0.88", "0.89", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSkills);
            // UI End
            CuiHelper.AddUi(player, ControlPanelelements);
            return;
        }

        private void AdminTimerColorPage(BasePlayer player)
        {
            var ControlPanelelements = new CuiElementContainer();
            var height = 0.030f;
            int row = 5;
            ControlPanelelements.Add(XPUIPanel("0.18 0.0", "1.0 1.0", "0.0 0.0 0.0 0.7"), XPerienceAdminPanelMain, XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"Timers, Chat, and UI Settings", 1, 0.090f, TextAnchor.MiddleLeft, 18, "0.01", "1", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            // Main Level Settings
            ControlPanelelements.Add(XPUILabel($"[Live Stats UI / Chat Settings]", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.30", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            // Allow LiveStats Location
            row++;
            ControlPanelelements.Add(XPUILabel($"Allow Players to Move UI:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.defaultOptions.liveuistatslocationmoveable}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultliveuimoveable 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultliveuimoveable 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            // Default Location
            row++;
            ControlPanelelements.Add(XPUILabel($"Default Location:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.defaultOptions.liveuistatslocation}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultliveui 1 false", row, height, 12, "0.0 1.0 0.0 0", "1", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultliveui 2 false", row, height, 12, "1.0 0.0 0.0 0", "2", "0.23", "0.24", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultliveui 3 false", row, height, 12, "0.0 1.0 0.0 0", "3", "0.25", "0.26", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultliveui 4 false", row, height, 12, "1.0 0.0 0.0 0", "4", "0.27", "0.28", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultliveui 5 false", row, height, 12, "1.0 0.0 0.0 0", "5", "0.29", "0.30", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            // Show Profile in Chat
            row++;
            ControlPanelelements.Add(XPUILabel($"Show Player Stats in Chat On Connect:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.defaultOptions.showchatprofileonconnect}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor showchatprofile 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor showchatprofile 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            // Show Armor Absorb Chat
            row++;
            ControlPanelelements.Add(XPUILabel($"Show Armor Absorb Chat Messages:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.defaultOptions.disablearmorchat}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor armorchat 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor armorchat 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            // Timer Settings
            row++;
            row++;
            ControlPanelelements.Add(XPUILabel($"[Stat / Skill Timer Settings]", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.30", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            // Restrict Resets
            row++;
            ControlPanelelements.Add(XPUILabel($"Restrict Resets:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.defaultOptions.restristresets}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultrestristresets 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultrestristresets 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            // Stat Reset Timer
            row++;
            ControlPanelelements.Add(XPUILabel($"Stat Reset Timer Mins:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.defaultOptions.resetminsstats}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultstattimer {config.defaultOptions.resetminsstats + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultstattimer {config.defaultOptions.resetminsstats - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            // Skill Reset Timer
            row++;
            ControlPanelelements.Add(XPUILabel($"Skill Reset Timer Mins:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.defaultOptions.resetminsskills}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultskilltimer {config.defaultOptions.resetminsskills + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultskilltimer {config.defaultOptions.resetminsskills - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            // VIP Stat Reset Timer
            row++;
            ControlPanelelements.Add(XPUILabel($"VIP Stat Reset Timer Mins:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.defaultOptions.vipresetminstats}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultvipstattimer {config.defaultOptions.vipresetminstats + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultvipstattimer {config.defaultOptions.vipresetminstats - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            // VIP Skill Reset Timer
            row++;
            ControlPanelelements.Add(XPUILabel($"VIP Skill Reset Timer Mins:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.defaultOptions.vipresetminsskills}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultvipskilltimer {config.defaultOptions.vipresetminsskills + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultvipskilltimer {config.defaultOptions.vipresetminsskills - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            // Fix Data Reset Timer
            row++;
            ControlPanelelements.Add(XPUILabel($"Player Fix Data Timer Mins:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.defaultOptions.playerfixdatatimer}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultplayerfixdata {config.defaultOptions.playerfixdatatimer + 1} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultplayerfixdata {config.defaultOptions.playerfixdatatimer - 1} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            // Admin Bypass Resets
            row++;
            ControlPanelelements.Add(XPUILabel($"Admins Bypass:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.defaultOptions.bypassadminreset}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultadminbypass 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultadminbypass 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            // Color Options
            row++;
            row++;
             ControlPanelelements.Add(XPUILabel($"[UI Text Color Settings]", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.30", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
           // Default
            row++;
            ControlPanelelements.Add(XPUILabel($"Default Color:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.uitextColor.defaultcolor}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.22", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultuicolor 1 false", row, height, 12, "0.0 1.0 0.0 0", "B", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultuicolor 2 false", row, height, 12, "1.0 0.0 0.0 0", "C", "0.23", "0.24", TextAnchor.MiddleCenter, "0.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultuicolor 3 false", row, height, 12, "0.0 1.0 0.0 0", "G", "0.25", "0.26", TextAnchor.MiddleCenter, "0.0 0.5 0.5 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultuicolor 4 false", row, height, 12, "1.0 0.0 0.0 0", "G", "0.27", "0.28", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultuicolor 5 false", row, height, 12, "1.0 0.0 0.0 0", "M", "0.29", "0.30", TextAnchor.MiddleCenter, "1.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultuicolor 6 false", row, height, 12, "0.0 1.0 0.0 0", "R", "0.31", "0.32", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultuicolor 7 false", row, height, 12, "1.0 0.0 0.0 0", "W", "0.33", "0.34", TextAnchor.MiddleCenter, "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor defaultuicolor 8 false", row, height, 12, "1.0 0.0 0.0 0", "Y", "0.35", "0.36", TextAnchor.MiddleCenter, "1.0 0.92 0.016 1.0"), XPerienceAdminPanelTimerColor);
            // Level
            row++;
            ControlPanelelements.Add(XPUILabel($"Level Color:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.uitextColor.level}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.22", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor leveluicolor 1 false", row, height, 12, "0.0 1.0 0.0 0", "B", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor leveluicolor 2 false", row, height, 12, "1.0 0.0 0.0 0", "C", "0.23", "0.24", TextAnchor.MiddleCenter, "0.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor leveluicolor 3 false", row, height, 12, "0.0 1.0 0.0 0", "G", "0.25", "0.26", TextAnchor.MiddleCenter, "0.0 0.5 0.5 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor leveluicolor 4 false", row, height, 12, "1.0 0.0 0.0 0", "G", "0.27", "0.28", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor leveluicolor 5 false", row, height, 12, "1.0 0.0 0.0 0", "M", "0.29", "0.30", TextAnchor.MiddleCenter, "1.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor leveluicolor 6 false", row, height, 12, "0.0 1.0 0.0 0", "R", "0.31", "0.32", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor leveluicolor 7 false", row, height, 12, "1.0 0.0 0.0 0", "W", "0.33", "0.34", TextAnchor.MiddleCenter, "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor leveluicolor 8 false", row, height, 12, "1.0 0.0 0.0 0", "Y", "0.35", "0.36", TextAnchor.MiddleCenter, "1.0 0.92 0.016 1.0"), XPerienceAdminPanelTimerColor);
            // Experience
            row++;
            ControlPanelelements.Add(XPUILabel($"Experience Color:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.uitextColor.experience}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.22", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor xpuicolor 1 false", row, height, 12, "0.0 1.0 0.0 0", "B", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor xpuicolor 2 false", row, height, 12, "1.0 0.0 0.0 0", "C", "0.23", "0.24", TextAnchor.MiddleCenter, "0.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor xpuicolor 3 false", row, height, 12, "0.0 1.0 0.0 0", "G", "0.25", "0.26", TextAnchor.MiddleCenter, "0.0 0.5 0.5 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor xpuicolor 4 false", row, height, 12, "1.0 0.0 0.0 0", "G", "0.27", "0.28", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor xpuicolor 5 false", row, height, 12, "1.0 0.0 0.0 0", "M", "0.29", "0.30", TextAnchor.MiddleCenter, "1.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor xpuicolor 6 false", row, height, 12, "0.0 1.0 0.0 0", "R", "0.31", "0.32", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor xpuicolor 7 false", row, height, 12, "1.0 0.0 0.0 0", "W", "0.33", "0.34", TextAnchor.MiddleCenter, "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor xpuicolor 8 false", row, height, 12, "1.0 0.0 0.0 0", "Y", "0.35", "0.36", TextAnchor.MiddleCenter, "1.0 0.92 0.016 1.0"), XPerienceAdminPanelTimerColor);
            // Next Level
            row++;
            ControlPanelelements.Add(XPUILabel($"Next Level Color:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.uitextColor.nextlevel}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.22", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor nextlvluicolor 1 false", row, height, 12, "0.0 1.0 0.0 0", "B", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor nextlvluicolor 2 false", row, height, 12, "1.0 0.0 0.0 0", "C", "0.23", "0.24", TextAnchor.MiddleCenter, "0.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor nextlvluicolor 3 false", row, height, 12, "0.0 1.0 0.0 0", "G", "0.25", "0.26", TextAnchor.MiddleCenter, "0.0 0.5 0.5 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor nextlvluicolor 4 false", row, height, 12, "1.0 0.0 0.0 0", "G", "0.27", "0.28", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor nextlvluicolor 5 false", row, height, 12, "1.0 0.0 0.0 0", "M", "0.29", "0.30", TextAnchor.MiddleCenter, "1.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor nextlvluicolor 6 false", row, height, 12, "0.0 1.0 0.0 0", "R", "0.31", "0.32", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor nextlvluicolor 7 false", row, height, 12, "1.0 0.0 0.0 0", "W", "0.33", "0.34", TextAnchor.MiddleCenter, "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor nextlvluicolor 8 false", row, height, 12, "1.0 0.0 0.0 0", "Y", "0.35", "0.36", TextAnchor.MiddleCenter, "1.0 0.92 0.016 1.0"), XPerienceAdminPanelTimerColor);
            // Remaining XP
            row++;
            ControlPanelelements.Add(XPUILabel($"Remaining XP Color:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.uitextColor.remainingxp}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.22", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor remainuicolor 1 false", row, height, 12, "0.0 1.0 0.0 0", "B", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor remainuicolor 2 false", row, height, 12, "1.0 0.0 0.0 0", "C", "0.23", "0.24", TextAnchor.MiddleCenter, "0.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor remainuicolor 3 false", row, height, 12, "0.0 1.0 0.0 0", "G", "0.25", "0.26", TextAnchor.MiddleCenter, "0.0 0.5 0.5 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor remainuicolor 4 false", row, height, 12, "1.0 0.0 0.0 0", "G", "0.27", "0.28", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor remainuicolor 5 false", row, height, 12, "1.0 0.0 0.0 0", "M", "0.29", "0.30", TextAnchor.MiddleCenter, "1.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor remainuicolor 6 false", row, height, 12, "0.0 1.0 0.0 0", "R", "0.31", "0.32", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor remainuicolor 7 false", row, height, 12, "1.0 0.0 0.0 0", "W", "0.33", "0.34", TextAnchor.MiddleCenter, "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor remainuicolor 8 false", row, height, 12, "1.0 0.0 0.0 0", "Y", "0.35", "0.36", TextAnchor.MiddleCenter, "1.0 0.92 0.016 1.0"), XPerienceAdminPanelTimerColor);
            // Stats / Skills / Levels
            row++;
            ControlPanelelements.Add(XPUILabel($"Stats/Skills/Levels Color:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.uitextColor.statskilllevels}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.22", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor ssluicolor 1 false", row, height, 12, "0.0 1.0 0.0 0", "B", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor ssluicolor 2 false", row, height, 12, "1.0 0.0 0.0 0", "C", "0.23", "0.24", TextAnchor.MiddleCenter, "0.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor ssluicolor 3 false", row, height, 12, "0.0 1.0 0.0 0", "G", "0.25", "0.26", TextAnchor.MiddleCenter, "0.0 0.5 0.5 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor ssluicolor 4 false", row, height, 12, "1.0 0.0 0.0 0", "G", "0.27", "0.28", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor ssluicolor 5 false", row, height, 12, "1.0 0.0 0.0 0", "M", "0.29", "0.30", TextAnchor.MiddleCenter, "1.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor ssluicolor 6 false", row, height, 12, "0.0 1.0 0.0 0", "R", "0.31", "0.32", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor ssluicolor 7 false", row, height, 12, "1.0 0.0 0.0 0", "W", "0.33", "0.34", TextAnchor.MiddleCenter, "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor ssluicolor 8 false", row, height, 12, "1.0 0.0 0.0 0", "Y", "0.35", "0.36", TextAnchor.MiddleCenter, "1.0 0.92 0.016 1.0"), XPerienceAdminPanelTimerColor);
            // Perks
            row++;
            ControlPanelelements.Add(XPUILabel($"Perks Color:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.uitextColor.perks}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.22", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor perksuicolor 1 false", row, height, 12, "0.0 1.0 0.0 0", "B", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor perksuicolor 2 false", row, height, 12, "1.0 0.0 0.0 0", "C", "0.23", "0.24", TextAnchor.MiddleCenter, "0.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor perksuicolor 3 false", row, height, 12, "0.0 1.0 0.0 0", "G", "0.25", "0.26", TextAnchor.MiddleCenter, "0.0 0.5 0.5 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor perksuicolor 4 false", row, height, 12, "1.0 0.0 0.0 0", "G", "0.27", "0.28", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor perksuicolor 5 false", row, height, 12, "1.0 0.0 0.0 0", "M", "0.29", "0.30", TextAnchor.MiddleCenter, "1.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor perksuicolor 6 false", row, height, 12, "0.0 1.0 0.0 0", "R", "0.31", "0.32", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor perksuicolor 7 false", row, height, 12, "1.0 0.0 0.0 0", "W", "0.33", "0.34", TextAnchor.MiddleCenter, "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor perksuicolor 8 false", row, height, 12, "1.0 0.0 0.0 0", "Y", "0.35", "0.36", TextAnchor.MiddleCenter, "1.0 0.92 0.016 1.0"), XPerienceAdminPanelTimerColor);
            // Unspent Points
            row++;
            ControlPanelelements.Add(XPUILabel($"Unspent Points Color:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.uitextColor.unspentpoints}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.22", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor upointsuicolor 1 false", row, height, 12, "0.0 1.0 0.0 0", "B", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor upointsuicolor 2 false", row, height, 12, "1.0 0.0 0.0 0", "C", "0.23", "0.24", TextAnchor.MiddleCenter, "0.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor upointsuicolor 3 false", row, height, 12, "0.0 1.0 0.0 0", "G", "0.25", "0.26", TextAnchor.MiddleCenter, "0.0 0.5 0.5 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor upointsuicolor 4 false", row, height, 12, "1.0 0.0 0.0 0", "G", "0.27", "0.28", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor upointsuicolor 5 false", row, height, 12, "1.0 0.0 0.0 0", "M", "0.29", "0.30", TextAnchor.MiddleCenter, "1.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor upointsuicolor 6 false", row, height, 12, "0.0 1.0 0.0 0", "R", "0.31", "0.32", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor upointsuicolor 7 false", row, height, 12, "1.0 0.0 0.0 0", "W", "0.33", "0.34", TextAnchor.MiddleCenter, "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor upointsuicolor 8 false", row, height, 12, "1.0 0.0 0.0 0", "Y", "0.35", "0.36", TextAnchor.MiddleCenter, "1.0 0.92 0.016 1.0"), XPerienceAdminPanelTimerColor);
            // Spent Points
            row++;
            ControlPanelelements.Add(XPUILabel($"Spent Points Color:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.uitextColor.spentpoints}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.22", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor spointsuicolor 1 false", row, height, 12, "0.0 1.0 0.0 0", "B", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor spointsuicolor 2 false", row, height, 12, "1.0 0.0 0.0 0", "C", "0.23", "0.24", TextAnchor.MiddleCenter, "0.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor spointsuicolor 3 false", row, height, 12, "0.0 1.0 0.0 0", "G", "0.25", "0.26", TextAnchor.MiddleCenter, "0.0 0.5 0.5 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor spointsuicolor 4 false", row, height, 12, "1.0 0.0 0.0 0", "G", "0.27", "0.28", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor spointsuicolor 5 false", row, height, 12, "1.0 0.0 0.0 0", "M", "0.29", "0.30", TextAnchor.MiddleCenter, "1.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor spointsuicolor 6 false", row, height, 12, "0.0 1.0 0.0 0", "R", "0.31", "0.32", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor spointsuicolor 7 false", row, height, 12, "1.0 0.0 0.0 0", "W", "0.33", "0.34", TextAnchor.MiddleCenter, "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor spointsuicolor 8 false", row, height, 12, "1.0 0.0 0.0 0", "Y", "0.35", "0.36", TextAnchor.MiddleCenter, "1.0 0.92 0.016 1.0"), XPerienceAdminPanelTimerColor);
            // Pets
            row++;
            ControlPanelelements.Add(XPUILabel($"Pets Color:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUILabel($"|       {config.uitextColor.pets}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.22", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor petsuicolor 1 false", row, height, 12, "0.0 1.0 0.0 0", "B", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor petsuicolor 2 false", row, height, 12, "1.0 0.0 0.0 0", "C", "0.23", "0.24", TextAnchor.MiddleCenter, "0.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor petsuicolor 3 false", row, height, 12, "0.0 1.0 0.0 0", "G", "0.25", "0.26", TextAnchor.MiddleCenter, "0.0 0.5 0.5 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor petsuicolor 4 false", row, height, 12, "1.0 0.0 0.0 0", "G", "0.27", "0.28", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor petsuicolor 5 false", row, height, 12, "1.0 0.0 0.0 0", "M", "0.29", "0.30", TextAnchor.MiddleCenter, "1.0 0.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor petsuicolor 6 false", row, height, 12, "0.0 1.0 0.0 0", "R", "0.31", "0.32", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor petsuicolor 7 false", row, height, 12, "1.0 0.0 0.0 0", "W", "0.33", "0.34", TextAnchor.MiddleCenter, "1.0 1.0 1.0 1.0"), XPerienceAdminPanelTimerColor);
            ControlPanelelements.Add(XPUIButton($"xp.config timercolor petsuicolor 8 false", row, height, 12, "1.0 0.0 0.0 0", "Y", "0.35", "0.36", TextAnchor.MiddleCenter, "1.0 0.92 0.016 1.0"), XPerienceAdminPanelTimerColor);
            // Column Two
            int rowtwo = 5;

            // End
            CuiHelper.AddUi(player, ControlPanelelements);
            return;
        }

        private void AdminOtherModsPage(BasePlayer player)
        {
            var ControlPanelelements = new CuiElementContainer();
            var height = 0.030f;
            int row = 5;
            ControlPanelelements.Add(XPUIPanel("0.18 0.0", "1.0 1.0", "0.0 0.0 0.0 0.7"), XPerienceAdminPanelMain, XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"Other Mod Support Settings", 1, 0.090f, TextAnchor.MiddleLeft, 18, "0.01", "1", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            // Kill Records Settings
            ControlPanelelements.Add(XPUILabel($"[Kill Records Settings] (Requires KillRecords plugin)", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.40", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            // KR Enable
            row++;
            ControlPanelelements.Add(XPUILabel($"Enable KillRecords Bonus:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpBonus.enablebonus}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods krenable 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods krenable 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // KR Button
            row++;
            ControlPanelelements.Add(XPUILabel($"Show KillRecords Button:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpBonus.showkrbutton}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods krshowbutton 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods krshowbutton 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // KR Required Kills
            row++;
            ControlPanelelements.Add(XPUILabel($"Required Kills:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpBonus.requiredkills}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods krrequiredkills {config.xpBonus.requiredkills + 5} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods krrequiredkills {config.xpBonus.requiredkills - 5} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // KR Bonus Amount
            row++;
            ControlPanelelements.Add(XPUILabel($"Bonus XP Amount:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpBonus.bonusxp}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods krbonusamount {config.xpBonus.bonusxp + 5} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods krbonusamount {config.xpBonus.bonusxp - 5} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // KR Bonus End
            row++;
            ControlPanelelements.Add(XPUILabel($"Bonus XP End:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpBonus.endbonus}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods krbonusend {config.xpBonus.endbonus + 10} false", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods krbonusend {config.xpBonus.endbonus - 10} false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // KR Enable MultiBonus
            row++;
            ControlPanelelements.Add(XPUILabel($"Multiple Bonus:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpBonus.multibonus}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods krenablemulti 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods krenablemulti 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // KR MultiBonus Type
            row++;
            ControlPanelelements.Add(XPUILabel($"Multiple Bonus Type:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpBonus.multibonustype}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.26", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods krmultitype 0 true", row, height, 18, "0.0 1.0 0.0 0", "⇧", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods krmultitype 0 false", row, height, 18, "1.0 0.0 0.0 0", "⇩", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Economics Settings
            row++;
            row++;
            ControlPanelelements.Add(XPUILabel($"[Economics Settings] (Requires Economics plugin)", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.40", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            // Enable Levelup Reward
            row++;
            ControlPanelelements.Add(XPUILabel($"Enable Level Up Reward:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpEcon.econlevelup}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econlevelup 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econlevelup 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Enable Leveldown Reduction
            row++;
            ControlPanelelements.Add(XPUILabel($"Enable Level Down Reduction:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpEcon.econleveldown}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econleveldown 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econleveldown 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Enable ResetStats Cost
            row++;
            ControlPanelelements.Add(XPUILabel($"Enable Reset Stats Cost:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpEcon.econresetstats}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econresetstats 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econresetstats 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Enable ResetStats Cost
            row++;
            ControlPanelelements.Add(XPUILabel($"Enable Reset Skills Cost:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpEcon.econresetskills}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econresetskills 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econresetskills 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Level Up Reward Amount
            row++;
            ControlPanelelements.Add(XPUILabel($"Level Up Reward Amount:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpEcon.econlevelreward}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econlevelreward {config.xpEcon.econlevelreward + 10} false", row, height, 12, "0.0 1.0 0.0 0", "⇧", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econlevelreward {config.xpEcon.econlevelreward - 10} false", row, height, 12, "1.0 0.0 0.0 0", "⇩", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Level Down Reduction
            row++;
            ControlPanelelements.Add(XPUILabel($"Level Loss Reduction:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpEcon.econlevelreduction}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econlevelreduction {config.xpEcon.econlevelreduction + 5} false", row, height, 12, "0.0 1.0 0.0 0", "⇧", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econlevelreduction {config.xpEcon.econlevelreduction - 5} false", row, height, 12, "1.0 0.0 0.0 0", "⇩", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Reset Stats Cost
            row++;
            ControlPanelelements.Add(XPUILabel($"Reset Stats Cost:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpEcon.econresetstatscost}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econresetstatscost {config.xpEcon.econresetstatscost + 5} false", row, height, 12, "0.0 1.0 0.0 0", "⇧", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econresetstatscost {config.xpEcon.econresetstatscost - 5} false", row, height, 12, "1.0 0.0 0.0 0", "⇩", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Reset Skills Cost
            row++;
            ControlPanelelements.Add(XPUILabel($"Reset Skills Cost:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpEcon.econresetskillscost}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econresetskillscost {config.xpEcon.econresetskillscost + 5} false", row, height, 12, "0.0 1.0 0.0 0", "⇧", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods econresetskillscost {config.xpEcon.econresetskillscost - 5} false", row, height, 12, "1.0 0.0 0.0 0", "⇩", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Server Rewards Settings
            row++;
            row++;
            ControlPanelelements.Add(XPUILabel($"[Server Rewards Settings] (Requires Server Rewards plugin)", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.40", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            // Enable Level Up Reward
            row++;
            ControlPanelelements.Add(XPUILabel($"Enable Level Up Reward:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.sRewards.srewardlevelup}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods srewardlevelup 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods srewardlevelup 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Enable Level Down Reduction
            row++;
            ControlPanelelements.Add(XPUILabel($"Enable Level Down Reduction:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.sRewards.srewardleveldown}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods srewardleveldown 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods srewardleveldown 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Level Up Amount
            row++;
            ControlPanelelements.Add(XPUILabel($"Level Up Amount:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.sRewards.srewardlevelupamt}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods srewardlevelupamt {config.sRewards.srewardlevelupamt + 1} false", row, height, 12, "0.0 1.0 0.0 0", "⇧", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods srewardlevelupamt {config.sRewards.srewardlevelupamt - 1} false", row, height, 12, "1.0 0.0 0.0 0", "⇩", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Level Down Amount
            row++;
            ControlPanelelements.Add(XPUILabel($"Level Down Amount:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.sRewards.srewardleveldownamt}", row, height, TextAnchor.MiddleLeft, 12, "0.20", "0.25", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods srewardleveldownamt {config.sRewards.srewardleveldownamt + 1} false", row, height, 12, "0.0 1.0 0.0 0", "⇧", "0.26", "0.27", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods srewardleveldownamt {config.sRewards.srewardleveldownamt - 1} false", row, height, 12, "1.0 0.0 0.0 0", "⇩", "0.28", "0.29", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Clans
            int rowtwo = 5;
            ControlPanelelements.Add(XPUILabel($"[Clans Settings] (Requires Clans plugin)", rowtwo, height, TextAnchor.MiddleLeft, 15, "0.40", "0.65", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            // Enable Clan Bonus
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Enable Clans Bonus:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.40", "0.55", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpclans.enableclanbonus}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.55", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods enableclanbonus 0 true", rowtwo, height, 12, "0.0 1.0 0.0 0", "T", "0.61", "0.62", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods enableclanbonus 0 false", rowtwo, height, 12, "1.0 0.0 0.0 0", "F", "0.63", "0.64", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Enable Clan Bonus
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Clans Bonus Amount:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.40", "0.55", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpclans.clanbonusamount * 100}%", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.55", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods clanbonusamt {config.xpclans.clanbonusamount + 0.01} false", rowtwo, height, 12, "0.0 1.0 0.0 0", "⇧", "0.61", "0.62", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods clanbonusamt {config.xpclans.clanbonusamount - 0.01} false", rowtwo, height, 12, "1.0 0.0 0.0 0", "⇩", "0.63", "0.64", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Enable Clan Reduction
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Enable Clans Reduction:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.40", "0.55", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpclans.enableclanreduction}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.55", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods enableclanreduction 0 true", rowtwo, height, 12, "0.0 1.0 0.0 0", "T", "0.61", "0.62", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods enableclanreduction 0 false", rowtwo, height, 12, "1.0 0.0 0.0 0", "F", "0.63", "0.64", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Enable Clan Bonus
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Clans Reduction Amount:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.40", "0.55", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.xpclans.clanreductionamount * 100}%", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.55", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods clanreductionamt {config.xpclans.clanreductionamount + 0.01} false", rowtwo, height, 12, "0.0 1.0 0.0 0", "⇧", "0.61", "0.62", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods clanreductionamt {config.xpclans.clanreductionamount - 0.01} false", rowtwo, height, 12, "1.0 0.0 0.0 0", "⇩", "0.63", "0.64", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // UI Notify
            rowtwo++;
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"[UI Notify] (Requires UINotify Plugin)", rowtwo, height, TextAnchor.MiddleLeft, 15, "0.40", "0.65", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            // Enable UINotify
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Enable UINotify:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.40", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.UiNotifier.useuinotify}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.55", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifyenable 0 true", rowtwo, height, 12, "0.0 1.0 0.0 0", "T", "0.61", "0.62", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifyenable 0 false", rowtwo, height, 12, "1.0 0.0 0.0 0", "F", "0.63", "0.64", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // Disable Chats
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Disable Default Chats:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.40", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.UiNotifier.disablechats}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.55", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifydisablechats 0 true", rowtwo, height, 12, "0.0 1.0 0.0 0", "T", "0.61", "0.62", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifydisablechats 0 false", rowtwo, height, 12, "1.0 0.0 0.0 0", "F", "0.63", "0.64", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // UI XP Gain/Loss
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Show XP Gain/Loss:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.40", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.UiNotifier.xpgainloss}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.55", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifyxpgainloss 0 true", rowtwo, height, 12, "0.0 1.0 0.0 0", "T", "0.61", "0.62", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifyxpgainloss 0 false", rowtwo, height, 12, "1.0 0.0 0.0 0", "F", "0.63", "0.64", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // UI XP Gain/Loss Type
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"XP Gain/Loss Message Type:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.40", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.UiNotifier.xpgainlosstype}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.55", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifyxpgainlosstype {config.UiNotifier.xpgainlosstype + 1} false", rowtwo, height, 12, "0.0 1.0 0.0 0", "⇧", "0.61", "0.62", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifyxpgainlosstype {config.UiNotifier.xpgainlosstype - 1} false", rowtwo, height, 12, "1.0 0.0 0.0 0", "⇩", "0.63", "0.64", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // UI Level Gain/Loss
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Show Level Gain/Loss:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.40", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.UiNotifier.levelupdown}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.55", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifylevelgainloss 0 true", rowtwo, height, 12, "0.0 1.0 0.0 0", "T", "0.61", "0.62", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifylevelgainloss 0 false", rowtwo, height, 12, "1.0 0.0 0.0 0", "F", "0.63", "0.64", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // UI Level Gain/Loss Type
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Level Gain/Loss Message Type:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.40", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.UiNotifier.levelupdowntype}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.55", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifylevelgainlosstype {config.UiNotifier.levelupdowntype + 1} false", rowtwo, height, 12, "0.0 1.0 0.0 0", "⇧", "0.61", "0.62", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifylevelgainlosstype {config.UiNotifier.levelupdowntype - 1} false", rowtwo, height, 12, "1.0 0.0 0.0 0", "⇩", "0.63", "0.64", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // UI Dodge/Block
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Show Dodge/Block:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.40", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.UiNotifier.dodgeblock}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.55", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifydodgeblock 0 true", rowtwo, height, 12, "0.0 1.0 0.0 0", "T", "0.61", "0.62", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifydodgeblock 0 false", rowtwo, height, 12, "1.0 0.0 0.0 0", "F", "0.63", "0.64", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // UI Dodge/Block Type
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Dodge/Block Message Type:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.40", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.UiNotifier.dodgeblocktype}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.55", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifydodgeblocktype {config.UiNotifier.dodgeblocktype + 1} false", rowtwo, height, 12, "0.0 1.0 0.0 0", "⇧", "0.61", "0.62", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifydodgeblocktype {config.UiNotifier.dodgeblocktype - 1} false", rowtwo, height, 12, "1.0 0.0 0.0 0", "⇩", "0.63", "0.64", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // UI Criticals
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Show Critical Hits:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.40", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.UiNotifier.criticalhit}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.55", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifycritical 0 true", rowtwo, height, 12, "0.0 1.0 0.0 0", "T", "0.61", "0.62", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifycritical 0 false", rowtwo, height, 12, "1.0 0.0 0.0 0", "F", "0.63", "0.64", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            // UI Dodge/Block Type
            rowtwo++;
            ControlPanelelements.Add(XPUILabel($"Critical Hit Message Type:", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.40", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUILabel($"|       {config.UiNotifier.criticalhittype}", rowtwo, height, TextAnchor.MiddleLeft, 12, "0.55", "0.60", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifycriticaltype {config.UiNotifier.criticalhittype + 1} false", rowtwo, height, 12, "0.0 1.0 0.0 0", "⇧", "0.61", "0.62", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelOtherMods);
            ControlPanelelements.Add(XPUIButton($"xp.config othermods uinotifycriticaltype {config.UiNotifier.criticalhittype - 1} false", rowtwo, height, 12, "1.0 0.0 0.0 0", "⇩", "0.63", "0.64", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelOtherMods);


            CuiHelper.AddUi(player, ControlPanelelements);
            return;
        }        
        
        private void AdminSQLPage(BasePlayer player)
        {
            var ControlPanelelements = new CuiElementContainer();
            var height = 0.030f;
            int row = 5;
            ControlPanelelements.Add(XPUIPanel("0.18 0.0", "1.0 1.0", "0.0 0.0 0.0 0.7"), XPerienceAdminPanelMain, XPerienceAdminPanelSQL);
            ControlPanelelements.Add(XPUILabel($"SQL Settings / Info - You can enable / disable SQL saving here, You must manually enter SQL info in the config file.", 1, 0.090f, TextAnchor.MiddleLeft, 18, "0.01", "1", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);
            // Main SQL Settings
            ControlPanelelements.Add(XPUILabel($"[SQL Settings]", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.30", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);
            // Enable SQL
            row++;
            ControlPanelelements.Add(XPUILabel($"Enable SQL Save:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);
            ControlPanelelements.Add(XPUILabel($"|       {config.sql.enablesql}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.20", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);
            ControlPanelelements.Add(XPUIButton($"xp.config sql sqlenable 0 true", row, height, 12, "0.0 1.0 0.0 0", "T", "0.21", "0.22", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), XPerienceAdminPanelSQL);
            ControlPanelelements.Add(XPUIButton($"xp.config sql sqlenable 0 false", row, height, 12, "1.0 0.0 0.0 0", "F", "0.23", "0.24", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelSQL);
            // Main SQL Settings
            row++;
            row++;
            ControlPanelelements.Add(XPUILabel($"[SQL Info] (change in config file)", row, height, TextAnchor.MiddleLeft, 15, "0.01", "0.50", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);
            // SQL Info
            row++;
            ControlPanelelements.Add(XPUILabel($"SQL Host IP:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);
            ControlPanelelements.Add(XPUILabel($"|       {config.sql.SQLhost}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.50", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);
            row++;
            ControlPanelelements.Add(XPUILabel($"SQL Host Port:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);
            ControlPanelelements.Add(XPUILabel($"|       {config.sql.SQLport}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.50", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);
            row++;
            ControlPanelelements.Add(XPUILabel($"SQL Host Database:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);
            ControlPanelelements.Add(XPUILabel($"|       {config.sql.SQLdatabase}", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.50", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);
            row++;
            ControlPanelelements.Add(XPUILabel($"SQL Host Username:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);
            ControlPanelelements.Add(XPUILabel($"|       (<i>hidden</i>)", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.50", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);
            row++;
            ControlPanelelements.Add(XPUILabel($"SQL Host Password:", row, height, TextAnchor.MiddleLeft, 12, "0.01", "0.15", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);
            ControlPanelelements.Add(XPUILabel($"|       (<i>hidden</i>)", row, height, TextAnchor.MiddleLeft, 12, "0.15", "0.50", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelSQL);

            CuiHelper.AddUi(player, ControlPanelelements);
            return;
        }        
        
        private void AdminResetPage(BasePlayer player)
        {
            var ControlPanelelements = new CuiElementContainer();
            var height = 0.030f;
            int row = 7;
            ControlPanelelements.Add(XPUIPanel("0.18 0.0", "1.0 1.0", "0.0 0.0 0.0 0.7"), XPerienceAdminPanelMain, XPerienceAdminPanelReset);
            ControlPanelelements.Add(XPUILabel($"Reset Config and Players.", 1, 0.090f, TextAnchor.MiddleLeft, 18, "0.01", "1", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelReset);
            // Reset Config
            ControlPanelelements.Add(XPUILabel($"[Reset XPerience to Default Config]", row, height, TextAnchor.MiddleCenter, 15, "0.25", "0.75", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelReset);
            row++;
            ControlPanelelements.Add(XPUIButton($"xp.config reset resetconfig 0 true", row, height, 12, "1.0 0.0 0.0 0", ">> Reset Config <<", "0.45", "0.55", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelReset);
            // MReset Players
            row++;
            row++;
            ControlPanelelements.Add(XPUILabel($"[Reset All Players Data] - Full Wipe of XPerience", row, height, TextAnchor.MiddleCenter, 15, "0.25", "0.75", "1.0 1.0 1.0 1.0"), XPerienceAdminPanelReset);
            row++;
            ControlPanelelements.Add(XPUIButton($"xp.config reset resetall 0 true", row, height, 12, "1.0 0.0 0.0 0", ">> Reset Players <<", "0.45", "0.55", TextAnchor.MiddleCenter, "1.0 0.0 0.0 1.0"), XPerienceAdminPanelReset);

            CuiHelper.AddUi(player, ControlPanelelements);
            return;
        }

        #endregion

        #region Lang

        private string XPLang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["adminpanel"] = "Admin Panel",
                ["adminmenu_001"] = "Main",
                ["adminmenu_002"] = "Level / XP",
                ["adminmenu_003"] = "Stats",
                ["adminmenu_004"] = "Skills",
                ["adminmenu_005"] = "UIs / Timers",
                ["adminmenu_006"] = "SQL",
                ["adminmenu_007"] = "SAVE SETTINGS",
                ["adminmenu_008"] = "Reload Mod",
                ["adminmenu_009"] = "CLOSE",
                ["adminmenu_010"] = "Reset Default Settings",
                ["adminmenu_011"] = "Fix Player Data",
                ["adminmenu_012"] = "Other Mod Settings",
                ["adminmenu_013"] = "Reset Options",
                ["adminfixplayers"] = "All player data has been reset except experience.\nLevels, points and requirements recalculated.",
                ["adminresetconfig"] = "Config has been reset to default values.",
                ["saveconfig"] = "New Config has been Saved",
                ["admininfoliveui"] = "Default Live UI Location",
                ["adminxp_001"] = "Level & XP Settings",
                ["adminxp_002"] = "[Level / Point Settings]",
                ["adminxp_003"] = "Level Start:",
                ["playerfixdata"] = "Your data has been fixed and your level recalculated. You will need to reapply your stat & skill points",
                ["playerfixdatabutton"] = "Fix My Data",
                ["resettimerdata"] = "Wait another {0} mins",
                ["xphelp"] = "XPerience Plugin by M@CHIN3 \n Commands: \n" +
                "/xpstats - brings up user control panel \n" +
                "/xpstatschat - shows your level, xp, stats, and skills in chat \n" +
                "/xptop - brings up top players UI \n" +
                "/xpaddstats (stat) - level up selected stat \n" +
                "/xpaddskill (skill) - level up selected skill \n" +
                "/xpresetstats - resets all stats and refunds points \n" +
                "/xpresetskills - resets all skills and refunds points \n" +
                "/xpliveui (0-4) - Live UI Location / 0 = off",
                ["xphelpadmin"] = "XPerience Plugin by M@CHIN3 \n Admin Commands: \n" +
                "/xpconfig - Opens admin control panel for mod setup" +
                "/resetxperience - resets entire mod and deletes all player data \n" +
                "/xpgive (playername) (amount) - gives x amount of experience to selected player \n" +
                "/xptake (playername) (amount) - takes x amount of experience from selected player \n",
                ["playerprofilechat"] = "My Stats: \n" +
                "---------------- \n" +
                "Level: {0} \n" +
                "Current XP: {1} \n" +
                "Next Level: {2} \n" +
                "Stat Points: {3} \n" +
                "Skill Points: {4} \n" +
                "---------------- \n" +
                "Mentality: {5} \n" +
                "Dexterity: {6} \n" +
                "Might: {7} \n" +
                "Chemist: {8} \n" +
                "---------------- \n" +
                "WoodCutter: {9} \n" +
                "Smithy: {10} \n" +
                "Miner: {11} \n" +
                "Forager: {12} \n" +
                "Hunter: {13} \n" +
                "Fisher: {14} \n" +
                "Crafter: {15} \n" +
                "Framer: {16} \n" +
                "Tamer: {17} \n",
                ["suicide"] = "You have lost {0} XP for commiting suicide",
                ["death"] = "Your XP has been reduced by {0} for death",
                ["levelup"] = "You are now Level {0}.  You have recieved {1} stat point and {2} skill points",
                ["leveldown"] = "You have lost a level! You are now Level {0}",
                ["statdown"] = "You have lost {0} stats points",
                ["skilldown"] = "You have lost {0} skill points",
                ["statdownextra"] = "You did not have enough unspent stat points to take, your ({0}) stat has been lowered and you have lost {1} stats points, {2} stat points returned to your unspent amount",
                ["skilldownextra"] = "You did not have enough unspent skill points to take, your ({0}) skill has been lowered and you have lost {1} skill points, {2} skill points returned to your unspent amount",
                ["bonus"] = "You get a bonus {0} XP for {1} {2}",
                ["notenoughpoints"] = "You do not have enough points",
                ["notenoughstatpoints"] = "You do not have enough points for level {0} {1}, requires {2} statpoints",
                ["notenoughskillpoints"] = "You do not have enough points for level {0} {1}, requires {2} skillpoints",
                ["pointsadded"] = "you now have {0} points applied in {1}",
                ["pointsremoved"] = "you have remove {0} points from {1}",
                ["statup"] = "You used {0} statpoints to reach Level {1} in {2}",
                ["skillup"] = "You used {0} skillpoints to reach Level {1} in {2}",
                ["nostatpoints"] = "You have lost all stat points",
                ["noskillpoints"] = "You have lost all skill points",
                ["resetstats"] = "You have reset your stats and have {0} stat points returned",
                ["resetskills"] = "You have reset your skills and have {0} skill points returned",
                ["attackerdodge"] = "Your victim dodged your attack",
                ["attackerblock"] = "Your victim blocked {0} damage from your attack",
                ["victimdodge"] = "You dodged last attack",
                ["victimblock"] = "You blocked {0} damage from last attack",
                ["crithit"] = "You preformed a critical hit for {0} extra damage",
                ["weaponcon"] = "New weapon condition is now {0}",
                ["medictools"] = "Medical Tools",
                ["medictooluse"] = "You recived an extra {0} health from {1}",
                ["medicrecoverplayer"] = "You have recovered with an extra {0} health.",
                ["medicreviveplayer"] = "You have been revived with an extra {0} health.",
                ["medicrevivereviver"] = "You have revived player with an extra {0} health.",
                ["level"] = "Level",
                ["experience"] = "Experience",
                ["xp"] = "XP",
                ["mentality"] = "Mentality",
                ["dexterity"] = "Dexterity",
                ["might"] = "Might",
                ["chemist"] = "Chemist",
                ["woodcutter"] = "WoodCutter",
                ["smithy"] = "Smithy",
                ["miner"] = "Miner",
                ["forager"] = "Forager",
                ["hunter"] = "Hunter",
                ["fisher"] = "Fisher",
                ["crafter"] = "Crafter",
                ["framer"] = "Framer",
                ["medic"] = "Medic",
                ["tamer"] = "Tamer",
                ["stats"] = "Stats",
                ["skills"] = "Skills",
                ["researchcost"] = "Research Cost",
                ["researchspeed"] = "Research Speed",
                ["critchance"] = "Critical Chance",
                ["blockchance"] = "Block Chance",
                ["dodgechance"] = "Dodge Chance",
                ["fishamount"] = "Fish Amount",
                ["fishitems"] = "Item Amount",
                ["armor"] = "Armor",
                ["melee"] = "Melee Damage",
                ["calories"] = "Max Calories",
                ["hydration"] = "Max Hydration",
                ["bleed"] = "Bleed Reduction",
                ["radiation"] = "Radiation Reduction",
                ["heat"] = "Heat Tolerance",
                ["cold"] = "Cold Tolerance",
                ["craftspeed"] = "Crafting Speed",
                ["woodgather"] = "Wood Gathering",
                ["woodbonus"] = "Bonus Gathering",
                ["woodapple"] = "Apple Chance",
                ["productionrate"] = "Production Chance",
                ["productionamount"] = "Amount",
                ["fuelconsumption"] = "Fuel Consumption",
                ["fuelconsumptionhats"] = "Fuel Consumption (hats)",
                ["oregather"] = "Ore Gathering",
                ["orebonus"] = "Bonus Gathering",
                ["gather"] = "Gathering",
                ["seedbonus"] = "Seed Bonus",
                ["randomitem"] = "Random Item",
                ["foodgather"] = "Food Gathering",
                ["bonusgather"] = "Bonus Gathering",
                ["damagewildlife"] = "Damage (wildlife)",
                ["nightdamage"] = "Night Damage (wildlife)",
                ["costreduction"] = "Cost Reduction",
                ["fullrepair"] = "Full Repair Chance",
                ["highcond"] = "Higher Condition Chance",
                ["upgradecost"] = "Upgrade Cost",
                ["repairtime"] = "Repair Time",
                ["repaircost"] = "Repair Cost",
                ["nextlevel"] = "Next Level",
                ["medicrevive"] = "Revival Health",
                ["medicrecover"] = "Recover Health",
                ["mediccrafting"] = "Mixing Table Speed",
                ["unusedstatpoints"] = "Unused Stat Points",
                ["unusedskillpoints"] = "Unused Skill Points",
                ["totalspent"] = "Total Used Points",
                ["liveuilocationoff"] = "Live XP UI Stats are off",
                ["liveuilocation"] = "Live XP UI location is {0}",
                ["liveuilocationhelp"] = "/xpliveui (0-4) - Live UI Location / 0 = off \n Current UI location is {0}",
                ["resetstatsbutton"] = "Reset Stat Points",
                ["resetskillsbutton"] = "Reset Skill Points",
                ["chicken"] = "Chicken",
                ["boar"] = "Boar",
                ["stag"] = "Stag",
                ["wolf"] = "Wolf",
                ["bear"] = "Bear",
                ["tamerinc"] = "Increase Tamer to tame animals",
                ["tamerpets"] = "Tameable Pets",
                ["topplayers"] = "Top Players",
                ["resetxperience"] = "All XPerience player data deleted",
                ["resettimerstats"] = "You have {0} mins left before you can reset your stats",
                ["resettimerskills"] = "You have {0} mins left before you can reset your skills",
                ["canresetstats"] = "Can Reset in {0} mins",
                ["canresetskills"] = "Can Reset in {0} mins",
                ["victimarmordmg"] = "Armor Absorbed {0} Damage",
                ["armordmgabsorb"] = "Armor Damage Reduction",
                ["liveuiselection"] = "Live Stats Location",
                ["mystats"] = "My Stats",
                ["help"] = "HELP",
                ["helpprev"] = "<< Prev Page",
                ["helpnext"] = "Next Page >>",
                ["helpcommands"] = "Chat Commands",
                ["helpcommandslist"] = "Note that many of these commands can be used within your control panel without using chat. \n\n" +
                "/xphelp - shows chat commands in chat \n\n" +
                "/xpstats - brings up your control panel \n\n" +
                "/xpstats (playername) = brings up another players full profile \n\n" +
                "/xpstatschat - shows your level, xp, stats, and skills in chat \n\n" +
                "/xptop - brings up top players panel \n\n" +
                "/xpaddstats (stat) - level up selected stat \n\n" +
                "/xpaddskill (skill) - level up selected skill \n\n" +
                "/xpresetstats - resets all stats and refunds points \n\n" +
                "/xpresetskills - resets all skills and refunds points \n\n" +
                "/xpliveui (0-4) - Live UI Location / 0 = off \n\n",
                ["moddetails"] = "About XPerience Created by:",
                ["bindkey"] = "You can bind any key to open your XPerience control panel. \n" +
                "Press F1 to open your console \n" +
                "Decide what key you want to bind the command to \n" +
                "Type bind 0 chat.say /xpstats \n" +
                "This will bind the 0 key to open the control panel. \n" +
                "Next execute the writecfg command in your console to save the config so it won't reset when you relaunch the game",
                ["aboutxperience"] = "\n\n XPerience is an extremely detailed RPG based mod that allows players to earn experience and levels by interacting with all aspects of the game. You can earn experience from just about " +
                "anything from cutting down trees, mining ore, hunting, killing, fishing, building, and more.. As you earn experience you will progress in levels that grant stat points and skill points you can spend in different traits " +
                "that will give you increased abilities. There are currently 3 major Stats and 10 secondary Skills each with their own special attributes, more may come in the future. Stats will grant you overall character strengths while Skills grant you increased abilities when " +
                "interacting with the world. For every level you increase these traits it will increase the strength of the abilities that each one gives you. The higher the level of each trait the more points it requires to reach the next level. " +
                "Server owners can configure and adjust every aspect of the XPerience mod including level requirements, level multiplier, xp gained from each source, points awarded per level, point cost per level, bonuses, stat and skill strengths per level, max level of stats " +
                "and skills, reset timers, and more. \n\n To see the current server settings or details about stats and skills click Next Page at the top. \n\n",
                ["serversettings"] = "Every server that uses XPerience can be setup differently to fit their preference. Below are some of the settings for this server: \n\n" +
                "[MAIN SETTINGS] Levels, Multipliers, Points, Timers, etc..\n" +
                "Level Start: {0} | Required XP to reach level 1 \n" +
                "XP Requirment: {1} | XP Requirement increase for next level ex. ({0} + {1} to reach level 2) \n" +
                "Level XP Boost: {2}% | XP increase per level \n" +
                "Stat Points Earned Per Level: {3} \n" +
                "Skill Points Earned Per Level: {4} \n" +
                "Reset Timers: Enabled:{5} Stats {6} / Skills {7} | Time in mins before you can reset your stats or skills \n" +
                "VIP Reset Timers: Stats {8} / Skills {9} | Time in mins before VIP players can reset stats and skills \n" +
                "Night Bonus XP: Enabled:{10} | {11}% | Bonus XP received between {12}:00 and {13}:00 hours game time if enabled \n" +
                "Night Skills Enabled: {14} | Skills that have bonuses at night between {12}:00 and {13}:00 hours game time \n\n",
                ["xpsettings"] = "[XP Settings]: Amount of XP earned for kills, gathering, building, crafting, etc.. \n",
                ["xpsettingskills"] = "[Kills & Revive] \n\n" +
                "Chickens: {0} \n" +
                "Fish: {1} \n" +
                "Boar: {2} \n" +
                "Stag: {3} \n" +
                "Wolf: {4} \n" +
                "Bear: {5} \n" +
                "Shark: {6} \n" +
                "Horse: {7} \n" +
                "Scientist: {8} \n" +
                "Dweller: {9} \n" +
                "Player: {10} \n" +
                "Bradley: {11} \n" +
                "Helicopter: {12}\n\n" +
                "Reviving: {13}",
                ["xpsettingsloot"] = "[Gathering / Looting] \n\n" +
                "Loot Container: {0} \n" +
                "Underwater Loot Container: {1} \n" +
                "Locked Crate: {2} \n" +
                "Hackable Crate: {3} \n" +
                "Animal Harvest: {4} \n" +
                "Corpse Harvest: {5} \n" +
                "Tree: {6} \n" +
                "Ore: {7} \n" +
                "Gathering: {8} \n" +
                "Plant: {9}",
                ["xpsettingscraft"] = "[Crafting / Building] \n\n" +
                "Crafting: {0} \n" +
                "Wood Structure: {1} \n" +
                "Stone Structure: {2} \n" +
                "Metal Structure: {3} \n" +
                "Armored Structure: {4} \n",
                ["xpmissionsettings"] = "[Missions] \n\n" +
                "Mission Succeeded: {0} \n" +
                "Failed Reduction Enabled: {1} \n" +
                "Failed Reduction Amount: {2} \n",
                ["xpreductionsettings"] = "[XP Reduction] \n\n" +
                "Death: {0}% Enabled: {1}\n" +
                "Suicide: {2}% Enabled: {3}\n",
                ["nextpagestats"] = "To view details about Stats and Skills click Next Page at the top.",
                ["aboutstats"] = "The 3 major Stats are Mentality, Dexterity & Might.\nThe current server settings are listed below and represent the strength of each Stat per level gained.",
                ["aboutmentality"] = "Grants you the ability to lower research costs such as the amount of scrap required to unlock new items, Reduces Research Speed that decreases the amount of time it takes to research items in the research station, and " +
                "gives you increased chance to attack with a critical hit and cause more damage to an enemy or animal.",
                ["aboutmentalitysettings"] = "[Current Mentality Settings] \nMax Level: {0} \nStarting Cost: {1} \nCost Multiplier: {2}x  Level \nResearch Cost Reduction: {3}% \nResearch Speed Reduction: {4}% \n" +
                "Critical Chance: {5}%",
                ["aboutdexterity"] = "Grants you increased chance to Block attacks and lower the amount of damage you recieve, increased the chance to Dodge an attack completely and take no damage, and decrease the damage you recieve when your Armor bar is " +
                "full (Armor requires Might)",
                ["aboutdexteritysettings"] = "[Current Dexterity Settings]\nMax Level: {0} \nStarting Cost: {1} \nCost Multiplier: {2}x Level \nBlock Chance: {3}% | Block Amount: {4} \nDodge Chance: {5}% \nReduced Armor Damage: {6}%",
                ["aboutmight"] = "This is one of the most beneficial stats in the system! It grants you the ability to reduce bleeding time, radiation taken, greater tolerance to heat and cold, higher max calories and hydration, increased max health (Armor) " +
                "as well as increases the damage you do with melee weapons.",
                ["aboutmightsettings"] = "[Current Might Settings] \nMax Level: {0} \nStarting Cost: {1} \nCost Multiplier: {2}x Level \nArmor: {3}% | Increased Max Health \nMelee Damage Increase: {4}% \n" +
                "Metabolism Increase: {5}% | Thirst/Hunger \nBleed Reduction: {6}% \nRadiation Reduction: {7}% \nIncreased Heat Tolerance: {8}% \nIncreased Cold Tolerance: {9}%",
                ["aboutchemist"] = "Grants reduced crafting speed when using the mixing table.",
                ["aboutchemistsettings"] = "[Current Chemist Settings]\nMax Level: {0} \nStarting Cost: {1} \nCost Multiplier: {2}x Level \nReduced Crafting Time: {3} | Mixing Table",
                ["aboutskills"] = "The 9 secondary skills are Woodcutter, Smithy, Miner, Forager, Hunter, Crafter, Framer, Fisher, and Tamer\n(taming requires pets mod and may not be available on certain servers).\nThe current server settings are listed below and represent the strength of each Skill per level gained.",
                ["aboutwoodcutter"] = "Increases the amount of wood you receive from cutting down trees, increases the bonus amount you get when a tree has been cut down, and gives you increased chances to have apples fall while cutting a tree.",
                ["aboutwoodcuttersettings"] = "[Current WoodCutter Settingss] \nMax Level: {0} \nStarting Cost: {1} \nCost Multiplier: {2}x Level \nGather Rate: +{3}% \nBonus: +{4}% \nApple Chance: {5}%",
                ["aboutsmithy"] = "Increases the chance of extra production from smelting or cooking in a furnace or grill and reduces the amount of fuel used in a furnace or grill so they burn longer with less fuel.",
                ["aboutsmithysettings"] = "[Current Smithy Settings]\nMax Level: {0} \nStarting Cost: {1} \nCost Multiplier: {2}x Level \nIncreased Production: {3}% \n Fuel Consumption: -{4}%",
                ["aboutminer"] ="Increases the amount of ore gathered from stone, metal, sulfur, etc.. and the amount of bonus material recieved when an ore has been fully collected. This skill also reduces the amount of fuel used when wearing a hat that consumes fuel like the mining hat, candle hat, etc..",
                ["aboutminersettings"] = "[Current Miner Settings]\nMax Level: {0}\nStarting Cost: {1}\nCost Multiplier: {2}\nGather Rate: +{3}%\nBonus: +{4}%\nFuel Consumption: -{5}%",
                ["aboutforager"] = "Increases the amount of resources you receive when collecting by hand from the ground such as wood, stone, metal, sulfer, berries, mushrooms, etc.. anything collected on the ground by hand. You also get an increased amount of seeds from berries, hemp, and other resources that provide seeds. This skill also gives you an increased chance to find random items when gathering by hand so make sure you keep an eye out around you for random item.",
                ["aboutforagersettings"] = "[Current Forager Settings]\nMax Level: {0}\nStarting Cost: {1}\nCost Multiplier: {2}\nGather Rate: +{3}%\nSeed Chance: +{4}% Amount: {5}\nRandom Item: {6}%",
                ["abouthunter"] = "Grants you the ability to get more food from animals when harvesting, increased bonus amount when fully harvested, increased damage to wildlife and even greater damage to wildlife when hunting at night.",
                ["abouthuntersettings"] = "[Current Hunter Settings]\nMax Level: {0}\nStarting Cost: {1}\nCost Multiplier: {2}\nGather Rate: +{3}%\nBonus: +{4}%\nWildlife Dmg Increase: +{5}%\nNight Dmg Increase: +{6}%",
                ["aboutcrafter"] = "Grants you increased crafting speed while reducing the amount of material cost when crafting. Gives you increased chance to fully repair items and increased chance to create items with up to 10% higher condition.",
                ["aboutcraftersettings"] = "[Current Crafter Settings]\nMax Level: {0}\nStarting Cost: {1}\nCost Multiplier: {2}\nCraft Speed: -{3}%\nCraft Cost: -{4}%\nRepair Speed: {5}%\nCondition Chance: {6}%\nCondition Increase: +10%",
                ["aboutframer"] = "Decreases the cost of materials needed to upgrade or repair buildings as well as reduces the repair time when a building has been damaged.",
                ["aboutframersettings"] = "[Current Framer Settings]\nMax Level: {0}\nStarting Cost: {1}\nCost Multiplier: {2}\n Upgrade Cost: {3}%\nRepair Cost: {4}%\nRepair Time: {5}%",
                ["aboutfisher"] = "Gives you the ability to catch more fish at one time or increases the items you collect when fishing if you don't catch a fish.",
                ["aboutfishersettings"] = "[Current Fisher Settings]\nMax Level: {0}\nStarting Cost: {1} \nCost Multiplier: {2} \nFish Increase: {3} \nItem Increase: {4}",
                ["aboutmedic"] = "Gives you the ability to revive yourself and other players with more health once revived as well as reduces the time it takes to craft teas or other items in the mixing table.",
                ["aboutmedicsettings"] = "[Current Medic Settings]\nMax Level: {0}\nStarting Cost: {1} \nCost Multiplier: {2} \nRevival Health: {3} \nRecover Health: {4} \nCrafting Time: {5}%",
                ["abouttamer"] = "If this skill is available then it will grant you the ability to tame animals as pets. Each level allows you to tame a bigger animal that can help you survive in the world. These pets can also carry items and even attack your enemies. Pets are currently controlled by a seperate mod with it's own settings and adjustments. More details about Pets can be found using the '/pet help' chat command",
                ["abouttamersettings"] = "[Current Tamer Settings]\nEnabled: {0} \nMax Level: {1} \nStarting Cost: {2} \nCost Multiplier: {3} \n\n[Tameable Pets]\nChicken: {4} | Level Req: {5} \nBoar: {6} | Level Req: {7} \nStag: {8} | Level Req:{9} \nWolf: {10} | Level Req: {11} \nBear: {12} | Level Req: {13}",
                ["nextpageskills"] = "Click Next Page to view more skill information",
                ["techtreenode"] = "You need {0} scrap to research {1}",
                ["xpgiveneedname"] = "Need to enter a player name /xpgive (playername) (amount)",
                ["xpgivenotfound"] = "Player not found",
                ["xpgiveneedamount"] = "Need to enter an amount /xpgive (playername) (amount)",
                ["xpgiveplayer"] = "You have given {0} {1} experience, they now have a total of {2} experience.",
                ["xptakeneedname"] = "Need to enter a player name /xptake (playername) (amount)",
                ["xptakenotfound"] = "Player not found",
                ["xptakeneedamount"] = "Need to enter an amount /xptake (playername) (amount)",
                ["xptakeplayer"] = "You have taken {0} experince from {1}, they now have a total of {2} experience.",
                ["adminpanelinfonew"] = "ⓍⓅerience Admin Control Panel\n\n Here you can adjust all the settings for this mod without having to open and edit the config file. On the menu to your left are several pages where you can adjust " +
                "everything from levels, experience, stats, skills, and more.. Once you have made any adjustments to these pages make sure your click SAVE on the menu and then Reload Mod so that these adjustments are writen to the config " +
                "and loaded. If you do not click save and reload any adjustment you made will be lost! Keep in mind this is an extremely detailed mod and even the slightest adjustment can make a huge difference on how this mod functions! It is " +
                "suggested that you make minor adjustments to see how the settings will effect your server and player's gaming experience. If you adjust the level start or xp requirement increase settings make sure you click Fix Player Data " +
                "on the menu AFTER you save and reload the mod so that the system can recalculate all players levels and requirements. Players will not loose any XP but they will have their profile reset and will have to reapply any points they have.\n\n" +
                "If you have any issues, questions, or suggestions you can join the mod developer's discord at \ndiscord.skilledsolders.net\nhttps://discord.gg/XMmgGwnXCZ" +
                "\n\nⓍⓅerience was created by MACHIN3",
                ["playerfixdatahelp"] = "You can use the Fix My Data button below to have your xperience data recalculated. This will reset all your stats except your experience. Your level, required xp, points, and info will be reset and recalculated based on the current server settings and your current experience. You will receive " +
                "however many points for stats and skills that your level should have and you will need to reapply them towards your stats and skills.\n\n Reasons you may need to do this:\n1. Server settings may have been changed since your last login.\n2.Map wipe didn't properly link your data.\n3. New features were added.\n4. Server was restored to an earlier date.",
                ["uinotify_xpgain"] = "+{0} XP",
                ["uinotify_xploss"] = "-{0} XP",
                ["econdeposit"] = "You received a deposit of {0} into your account for leveling up",
                ["econwidthdrawlevel"] = "You lost {0} from your account for level loss",
                ["econwidthdrawresetstat"] = "You spent {0} for resetting stats",
                ["econwidthdrawresetskill"] = "You spent {0} for resetting skills",
                ["srewardsup"] = "You recieved {0} points in server rewards for leveling up",
                ["srewardsdown"] = "You lost {0} points in server rewards for leveling down",

            }, this);
        }

        #endregion

        #region API

        private void GiveXP(BasePlayer player, double amount)
        {
            GainExp(player, amount);
        }

        private void TakeXP(BasePlayer player, double amount)
        {
            LoseExp(player, amount);
        }

        #endregion

    }

    #region Extension Methods

    namespace XPerienceEx
    {
        public static class PlayerEx
        {
            public static void RunEffect(this BasePlayer player, string prefab)
            {
                Effect effect = new Effect();
                effect.Init(Effect.Type.Generic, player.ServerPosition, Vector3.zero);
                effect.pooledString = prefab;
                EffectNetwork.Send(effect, player.Connection);
            }
        }
    }

    #endregion

}