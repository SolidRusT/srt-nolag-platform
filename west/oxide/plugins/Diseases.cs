using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Diseases", "mr01sam", "1.0.5")]
    [Description("Players can be inflicted with disease that can be spread to others")]
    public class Diseases : CovalencePlugin
    {

        private const string PermissionInfo = "diseases.info";
        private const string PermissionAdmin = "diseases.admin";

        List<Disease> diseases = new List<Disease>();

        private bool showWarnings = false;

        #region JSON
        private static string TEMPLATE = @"[
          {
            ""name"": ""IndicatorIMG"",
            ""parent"": ""Overlay"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.RawImage"",
                ""color"": ""1 1 1 1"",
                ""url"": ""{imageUrl}""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0.9685 0.905"",
                ""anchormax"": ""0.9885 0.940""
              }
            ]
          }]";
        #endregion

        #region Configuration
        protected override void LoadDefaultConfig()
        {
            Config["General Settings"] = new Dictionary<string, object>
            {
                { "Face Covering Items (Short Name)", new List<object> {
                    "mask.bandana",
                    "burlap.headwrap",
                    "clatter.helmet",
                    "coffeecan.helmet",
                    "heavy.plate.helmet",
                    "hazmatsuit",
                    "hazmatsuit.spacesuit",
                    "metal.facemask",
                    "halloween.surgeonsuit"
                    }
                },
                { "Show Prefix (true/false)", false },
                { "Show Infected Message (true/false)", true },
                { "Show Recovery Message (true/false)", true },
                { "Show Cured Message (true/false)", false },
                { "Show Death Message (true/false)", false },
                { "Message Color", "white" },
                { "Prefix Color", "green" },
                { "Disease Name Color", "orange" },
                { "Generate Default Disease (true/false)", true },
                { "Indicator image", "https://i.imgur.com/BODiaSy.png" }
            };
        }

        public int GetConfigInt(string menu, string key)
        {
            var data = Config[menu] as Dictionary<string, object>;
            return (int) data[key];
        }

        public object GetConfigValue(string menu, string key)
        {
            var data = Config[menu] as Dictionary<string, object>;
            return data[key];
        }

        public List<object> GetConfigList(string menu, string key)
        {
            var data = Config[menu] as Dictionary<string, object>;
            return (List<object>)data[key];
        }


        public string GetConfigString(string menu, string key)
        {
            var data = Config[menu] as Dictionary<string, object>;
            return data[key].ToString();
        }

        public bool GetConfigBool(string menu, string key)
        {
            var data = Config[menu] as Dictionary<string, object>;
            return bool.Parse(data[key].ToString());
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Infected message"] = "You are starting to feel sick from {0}",
                ["Recovering message"] = "You are recovering from {0}",
                ["Cured message"] = "You are no longer immune to {0}",
                ["Perm message"] = "You do not have permission to use this command",
                ["Prefix"] = "[Diseases]",
                ["List"] = "Diseases: ",
                ["Stat infected current"] = "Infected (current): ",
                ["Stat infected"] = "Infected (total): ",
                ["Stat deaths"] = "Deaths: ",
                ["Stat outbreaks"] = "Outbreaks: ",
                ["Stat outbreaks time"] = "Last Outbreak: ",
                ["Outbreak true"] = "Outbreak affected {0} entities",
                ["Outbreak false"] = "There are no valid entities for an outbreak",
                ["Invalid disease"] = "There is no loaded disease with that name",
                ["Invalid player"] = "There is no active player with that name",
                ["Infected"] = "Infected {0} with {1}",
                ["Cured"] = "Cured {0} of {1}",
                ["Invalid command"] = "Failed to execute command: {0}",
                ["Commands"] = "Commands:",
                ["Death message"] = "{0} has died from {1}"
            }, this);
        }

        private void SendMessage(BasePlayer player, int chatChannel, string message, string title = null, string color = null)
        {
            if (title != null)
            {
                player.SendConsoleCommand("chat.add2", chatChannel, "", message, title, color);
            }
            else
            {
                player.SendConsoleCommand("chat.add", chatChannel, "", message);
            }

        }

        private void BroadcastMessage(int chatChannel, string message, string title = null, string color = null)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                SendMessage(player, chatChannel, message, title, color);
            }
        }

        private string Color(string text, string colorCode)
        {
            return "<color=" + colorCode + ">" + text + "</color>";
        }
        #endregion

        #region Commands
        [Command("diseases"), Permission(PermissionInfo)]
        private void cmd_diseases(IPlayer player, string command, string[] args)
        {
            cmd_diseases_help(player, command, args);
        }

        [Command("diseases.help"), Permission(PermissionInfo)]
        private void cmd_diseases_help(IPlayer player, string command, string[] args)
        {
            try
            {
                string message = lang.GetMessage("Commands", this, player.Id) + "\n";
                message += "/diseases.help\n";
                message += "/diseases.list\n";
                message += "/diseases.stats <disease name>\n";
                message += "/diseases.outbreak <disease name> <number of outbreaks>\n";
                message += "/diseases.infect <player name> <disease name>\n";
                message += "/diseases.cure <player name> <disease name>\n";

                player.Reply(message);
            }
            catch (Exception) { player.Reply(string.Format(lang.GetMessage("Invalid command", this, player.Id), command)); };
        }

        [Command("diseases.list"), Permission(PermissionInfo)]
        private void cmd_diseases_list(IPlayer player, string command, string[] args)
        {
            try
            {
                string message = lang.GetMessage("List", this, player.Id);
                foreach (Disease disease in diseases)
                    message += disease.name + ",";
                message = message.Substring(0, message.Length - 1);
                player.Reply(message);
            }
            catch (Exception) { player.Reply(string.Format(lang.GetMessage("Invalid command", this, player.Id), command)); };
        }

        [Command("diseases.stats"), Permission(PermissionInfo)]
        private void cmd_diseases_stats(IPlayer player, string command, string[] args)
        {
            try
            {
                string diseaseName = args[0];
                Disease disease = diseases.Find(x => x.name.ToUpper() == diseaseName.ToUpper());
                if (disease != null)
                {
                    string message = diseaseName.ToUpper() + "\n";
                    message += lang.GetMessage("Stat infected current", this, player.Id) + disease.infected.Count + "\n";
                    message += lang.GetMessage("Stat infected", this, player.Id) + disease.totalInfectedCount + "\n";
                    message += lang.GetMessage("Stat deaths", this, player.Id) + disease.totalDeathCount + "\n";
                    message += lang.GetMessage("Stat outbreaks", this, player.Id) + disease.totalOutbreakCount + "\n";
                    player.Reply(message);
                }
            }
            catch (Exception) { player.Reply(string.Format(lang.GetMessage("Invalid command", this, player.Id), command)); };
        }

        [Command("diseases.infect"), Permission(PermissionAdmin)]
        private void cmd_diseases_infect(IPlayer player, string command, string[] args)
        {
            try
            {
                long now = DateTimeOffset.Now.ToUnixTimeSeconds();
                if (ArgsIs(args, 2))
                {
                    string playerName = args[0];
                    string diseaseName = args[1];
                    BasePlayer target = BasePlayer.Find(playerName);
                    Disease disease = diseases.Find(x => x.name.ToUpper() == diseaseName.ToUpper());
                    if (disease.Infect(target, true))
                        player.Reply("Infected " + target.displayName + " with " + disease.name);
                }
            } catch (Exception) { player.Reply(string.Format(lang.GetMessage("Invalid command", this, player.Id), command)); };
        }

        [Command("diseases.cure"), Permission(PermissionAdmin)]
        private void cmd_diseases_cure(IPlayer player, string command, string[] args)
        {
            try
            {
                long now = DateTimeOffset.Now.ToUnixTimeSeconds();
                if (ArgsIs(args, 2))
                {
                    string playerName = args[0];
                    string diseaseName = args[1];
                    BasePlayer target = BasePlayer.Find(playerName);
                    Disease disease = diseases.Find(x => x.name.ToUpper() == diseaseName.ToUpper());
                    if (disease.Recover(target))
                        player.Reply("Cured " + target.displayName + " of " + disease.name);
                }
            }
            catch (Exception) { player.Reply(string.Format(lang.GetMessage("Invalid command", this, player.Id), command)); };
        }

        [Command("diseases.outbreak"), Permission(PermissionAdmin)]
        private void cmd_diseases_outbreak(IPlayer player, string command, string[] args)
        {
            try
            {
                long now = DateTimeOffset.Now.ToUnixTimeSeconds();
                if (ArgsIs(args, 2))
                {
                    string diseaseName = args[0];
                    int numOutbreaks = int.Parse(args[1]);
                    int numInfected = 0;
                    Disease disease = diseases.Find(x => x.name.ToUpper() == diseaseName.ToUpper());
                    for (int i = 0; i < numOutbreaks; i++)
                        if (AttemptOutbreak(now, disease, true) != null)
                            numInfected++;
                    player.Reply("Outbreak affected " + numInfected + " entities");
                }
            }
            catch (Exception) { player.Reply(string.Format(lang.GetMessage("Invalid command", this, player.Id), command)); };
        }

        private bool ArgsIs(string[] args, int minLength, string value = null)
        {
            if (args.Length < minLength)
                return false;
            if (value != null && args[0] != value)
                return false;
            return true;
        }

        private bool HasPerm(BasePlayer player, string permString)
        {
            if (!permission.UserHasPermission(player.UserIDString, permString))
            {
                SendMessage(player, 0, lang.GetMessage("Perm message", this, player.UserIDString));
                return false;
            }
            return true;
        }

        #endregion

        #region Hooks
        void Init()
        {
            List<string> files = new List<string>();
            try
            {
                files = Interface.Oxide.DataFileSystem.GetFiles("Diseases/").ToList();
            } catch (Exception) { };

            
            /* Generate Default Disease */
            if (GetConfigBool("General Settings", "Generate Default Disease (true/false)") && files.Count == 0)
            {
                Disease disease = new Disease(this);
                diseases.Add(disease);
                Disease.SaveToFile(disease);
            }
            /* Load Saved Diseases */
            foreach (string filename in files)
            {
                string[] split = filename.Split('/');
                string diseaseName = split[split.Length - 1].Replace(".json", "");
                Disease loadedDisease = Disease.LoadFromFile(diseaseName, this);
                if (!diseases.Contains(loadedDisease))
                {
                    diseases.Add(loadedDisease);
                }
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "IndicatorIMG");
            }
            Puts(diseases.Count + " diseases loaded");
        }

        void OnTick()
        {
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            if (diseases.Count() > 0)
            {
                foreach (Disease disease in diseases)
                {
                    /* Outbreak tick */
                    if (disease.ShouldOutbreak(now))
                        AttemptOutbreak(now, disease);
                    
                    /* Spread tick */
                    foreach (uint entityId in disease.infected.Keys)
                    {
                        try
                        {
                            InfectedEntity ie = disease.infected[entityId];
                            if (ie.ShouldSpread(now))
                                AttemptSpread(now, disease, ie);
                            if (ie.ShouldRecover(now))
                                disease.Recover(ie.entity);
                            if (ie.ShouldCure(now))
                                disease.Cure(ie.entity);
                        } catch (NullReferenceException)
                        {
                            disease.RemoveInfected(entityId);
                        }
                    }
                    /* Cleanup tick */
                    disease.UpdateInfectedList(now);
                }
            }
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            try
            {
                bool isInfected = false;
                foreach (Disease disease in diseases)
                {
                    if (disease.HasInfected(player))
                    {
                        isInfected = true;
                        break;
                    }
                }
                if (isInfected)
                    ShowIndicatorCUI(player);
            } catch (NullReferenceException)
            {
                if (showWarnings)
                    Puts("Warning: Failed OnPlayerSleepEnded");
            }
        }

        void OnItemUse(Item item, int amountToUse)
        {
            try
            {
                long now = DateTimeOffset.Now.ToUnixTimeSeconds();
                if (item != null && item.info != null)
                {
                    foreach (Disease disease in diseases)
                    {
                        /* Try Cure */
                        if (disease.itemsThatCureOnConsumption.ContainsKey(item.info.shortname))
                        {
                            BasePlayer player = item.GetOwnerPlayer();
                            if (UnityEngine.Random.Range(0, 100) <= disease.itemsThatCureOnConsumption[item.info.shortname])
                                disease.Recover(player);
                        }

                        /* Try Outbreak */
                        if (disease.itemsThatCauseOutbreaksOnConsumption.ContainsKey(item.info.shortname))
                        {
                            BasePlayer player = item.GetOwnerPlayer();
                            if (UnityEngine.Random.Range(0, 100) <= disease.itemsThatCauseOutbreaksOnConsumption[item.info.shortname])
                                disease.Infect(player);
                        }
                    }
                }
            } catch (NullReferenceException)
            {
                if (showWarnings)
                    Puts("Warning: Failed OnItemUse");
            }

        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                foreach (Disease disease in diseases)
                {
                    if (disease.WasCauseOfDeath(entity, info))
                    {
                        disease.totalDeathCount += 1;
                        if (entity is BasePlayer && !((BasePlayer) entity).IsNpc && GetConfigBool("General Settings", "Show Death Message (true/false)") == true)
                        {
                            BasePlayer player = (BasePlayer)entity;
                            string color = GetConfigString("General Settings", "Disease Name Color");
                            BroadcastMessage(0, String.Format(lang.GetMessage("Death message", this, player.UserIDString), player.displayName, Color(disease.name.TitleCase(), color)));
                        }
                    }

                    if (disease.infected.ContainsKey(entity.net.ID))
                    {
                        disease.RemoveInfected(entity.net.ID);
                        if (entity is BasePlayer)
                            CuiHelper.DestroyUi((BasePlayer)entity, "IndicatorIMG");
                    }
                }
            } catch (NullReferenceException)
            {
                if (showWarnings)
                    Puts("Warning: Failed OnEntityDeath, entity=" + entity.ShortPrefabName);
            }

        }

        #endregion

        #region Helpers
        private void ShowIndicatorCUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "IndicatorIMG");
            var filledTemplate = TEMPLATE.Replace("{imageUrl}", GetConfigString("General Settings", "Indicator image"));
            CuiHelper.AddUi(player, filledTemplate);
        }

        private List<BaseEntity> AttemptSpread(long now, Disease disease, InfectedEntity spreader)
        {
            if (spreader != null && spreader.entity is BaseCombatEntity && spreader.hasSymptoms)
            {
                foreach (string effect in disease.symptomEffects)
                    PlayEffect(spreader.entity, effect);
                if (spreader.isPlayer && spreader.player.metabolism != null)
                {
                    spreader.player.metabolism.calories.value -= Math.Min(disease.damageCalories, spreader.player.metabolism.calories.value);
                    spreader.player.metabolism.hydration.value -= Math.Min(disease.damageHydration, spreader.player.metabolism.hydration.value);
                }
                ((BaseCombatEntity)spreader.entity).Hurt(disease.damageHealth, Rust.DamageType.Poison);

                BaseEntity origin = spreader.entity;
                Vector3 position = new Vector3(origin.transform.position.x, origin.transform.position.y, origin.transform.position.z);
                float distance = disease.infectionSpreadDistance;
                List<BaseEntity> infected = new List<BaseEntity>();
                List<BaseEntity> nearby = new List<BaseEntity>();
                Vis.Entities<BaseEntity>(position, distance, nearby); foreach (BaseEntity entity in nearby)
                {
                    if (disease.CanInfect(entity))
                    {
                        double chance = disease.infectionChanceUncovered;
                        if (HasFaceCovering(entity))
                            chance = disease.infectionChanceCovered;
                        if (UnityEngine.Random.Range(0, 100) <= chance)
                        {
                            disease.Infect(entity);
                            infected.Add(entity);
                        }
                    }
                }
                if (infected != null && infected.Count > 0)
                    return infected;
            }
            spreader.SetSymptomTime(now, disease.RandomValue(disease.minSpreadTime, disease.maxSpreadTime));
            return new List<BaseEntity>() { };
        }

        private BaseEntity AttemptOutbreak(long now, Disease disease, bool force = false)
        {
            List<BaseNpc> allNpc = BaseNetworkable.serverEntities.OfType<BaseNpc>().ToList();
            List<BasePlayer> allPlayers = BaseNetworkable.serverEntities.OfType<BasePlayer>().ToList();
            List<BaseEntity> validEntities = new List<BaseEntity>();

            if (allPlayers != null)
                foreach (BasePlayer entity in allPlayers)
                    if (disease.CanInfect(entity))
                        validEntities.Add((BaseEntity)entity);
            if (allNpc != null)
            foreach (BaseNpc entity in allNpc)
                if (disease.CanInfect(entity))
                    validEntities.Add((BaseEntity)entity);

            if (validEntities != null && validEntities.Count > 0)
            {
                BaseEntity randomEntity = validEntities[UnityEngine.Random.Range(0, validEntities.Count - 1)];
                if (force == true || UnityEngine.Random.Range(0, 100) < disease.randomOutbreakInfectionChance)
                {
                    disease.Infect(randomEntity);
                    disease.totalOutbreakCount += 1;

                    return randomEntity;
                }
            }
            disease.nextOutbreakTick = now + disease.randomOutbreakTickInterval;
            return null;
        }

        private bool HasFaceCovering(BaseEntity entity)
        {
            if (entity is BasePlayer)
            {
                foreach (Item item in ((BasePlayer)entity).inventory.containerWear.itemList)
                    if (GetConfigList("General Settings", "Face Covering Items (Short Name)").Contains(item.info.shortname))
                        return true;
            }
            return false;
        }


        private void PlayEffect(BaseEntity entity, string effectString)
        {
            var effect = new Effect(effectString, entity, 0, Vector3.zero, Vector3.forward);
            EffectNetwork.Send(effect);
        }
        #endregion

        #region Classes
        class Disease
        {
            #region Fields
            [NonSerialized]
            public Dictionary<uint, InfectedEntity> infected;
            [NonSerialized]
            public Diseases plugin;
            [NonSerialized]
            public long nextOutbreakTick; 
            [NonSerialized]
            public long nextDiseaseTick;
            [NonSerialized]
            public int totalInfectedCount;
            [NonSerialized]
            public int totalDeathCount;
            [NonSerialized]
            public int totalOutbreakCount;
            [NonSerialized]
            private List<uint> entitiesToAdd;
            [NonSerialized]
            private List<uint> entitiesToRemove;

            public string name;
            public int minInfectionTime;
            public int maxInfectionTime;
            public int minSpreadTime;
            public int maxSpreadTime;
            public int minImmunityTime;
            public int maxImmunityTime;
            public double infectionChanceCovered;
            public double infectionChanceUncovered;
            public float damageHealth;
            public float damageCalories;
            public float damageHydration;
            public bool randomOutbreaksOccur;
            public int randomOutbreakTickInterval;
            public int randomOutbreakInfectionChance;
            public float infectionSpreadDistance;
            public Dictionary<string, int> itemsThatCauseOutbreaksOnConsumption;
            public Dictionary<string, int> itemsThatCureOnConsumption;
            public List<object> infectableEntities;
            public List<object> symptomEffects;
            
            #endregion

            #region Constructors
            public Disease(Diseases plugin)
            {
                this.plugin = plugin;
                this.name = "Norovirus";
                this.minInfectionTime = 180;
                this.maxInfectionTime = 1200;
                this.minSpreadTime = 5;
                this.maxSpreadTime = 30;
                this.minImmunityTime = 120;
                this.maxImmunityTime = 180;
                this.infectionChanceUncovered = 90;
                this.infectionChanceCovered = 5;
                this.damageHealth = 5;
                this.damageCalories = 125;
                this.damageHydration = 85;
                this.infectableEntities = new List<object> {
                    "player",
                    "bandit_conversationalist",
                    "bandit_guard",
                    "bandit_shopkeeper",
                    "heavyscientist",
                    "scientist",
                    "scientist_astar_full_any",
                    "scientist_full_any",
                    "scientist_full_lr300",
                    "scientist_full_mp5",
                    "scientist_full_pistol",
                    "scientist_full_shotgun",
                    "scientist_gunner",
                    "scientistnpc",
                    "scientistpeacekeeper",
                    "zombie",
                    "murderer",
                    "bear",
                    "boar",
                    "stag",
                    "wolf",
                    "horse",
                    "chicken"
                };
                this.randomOutbreaksOccur = true;
                this.randomOutbreakTickInterval = 500;
                this.randomOutbreakInfectionChance = 20;
                this.infectionSpreadDistance = 2f;
                this.itemsThatCauseOutbreaksOnConsumption = new Dictionary<string, int>
                {
                    { "chicken.raw", 10 },
                    { "humanmeat.raw", 10 },
                    { "humanmeat.spoiled", 75 }
                };
                this.itemsThatCureOnConsumption = new Dictionary<string, int>
                {
                    { "antiradpills", 100 }
                };
                this.infected = new Dictionary<uint, InfectedEntity>();
                this.nextOutbreakTick = DateTimeOffset.Now.ToUnixTimeSeconds() + this.randomOutbreakTickInterval;
                this.symptomEffects = new List<object> {
                    "assets/bundled/prefabs/fx/gestures/drink_vomit.prefab",
                    "assets/bundled/prefabs/fx/water/midair_splash.prefab",
                    "assets/prefabs/tools/jackhammer/effects/strike_screenshake.prefab"
                };
                this.totalInfectedCount = 0;
                this.totalDeathCount = 0;
                this.totalOutbreakCount = 0;
                this.nextDiseaseTick = DateTimeOffset.Now.ToUnixTimeSeconds() + 1;
                this.entitiesToAdd = new List<uint>();
                this.entitiesToRemove = new List<uint>();
            }
            #endregion

            #region Private Methods
            private void ShowStatusMessage(BaseEntity entity, string message)
            {
                if (entity is BasePlayer)
                {
                    try
                    {
                        message = plugin.Color(String.Format(message, plugin.Color(name, plugin.GetConfigString("General Settings", "Disease Name Color"))), plugin.GetConfigString("General Settings", "Message Color"));
                    }
                    catch (Exception) { }
                    if (plugin.GetConfigBool("General Settings", "Show Prefix (true/false)"))
                        plugin.SendMessage((BasePlayer)entity, 0, message, plugin.lang.GetMessage("Prefix", plugin, ((BasePlayer)entity).UserIDString), plugin.GetConfigString("General Settings", "Prefix Color"));
                    else
                        plugin.SendMessage((BasePlayer)entity, 0, message);
                }
            }
            #endregion

            #region Public Methods
            public int RandomValue(int min, int max)
            {
                return UnityEngine.Random.Range(min, max);
            }

            public bool WasCauseOfDeath(BaseEntity entity, HitInfo info)
            {
                if (!HasInfected(entity))
                    return false;
                Rust.DamageType cause = info.damageTypes.GetMajorityDamageType();
                if (damageCalories > 0 && cause.Equals(Rust.DamageType.Hunger))
                    return true;
                if (damageHydration > 0 && cause.Equals(Rust.DamageType.Thirst))
                    return true;
                if (damageHealth > 0 && cause.Equals(Rust.DamageType.Poison))
                    return true;

                return false;
            }

            public bool HasInfected(BaseEntity entity)
            {
                if (infected != null && infected.Count > 0 && entity != null)
                    return infected.ContainsKey(entity.net.ID);
                return false;
            }

            public bool CanInfect(BaseEntity entity)
            {
                if (infected != null && infectableEntities != null && entity != null)
                {
                    if (entity is BasePlayer && ((BasePlayer)entity).IsSleeping())
                        return false;
                    return !infected.ContainsKey(entity.net.ID) && infectableEntities.Contains(entity.ShortPrefabName);
                }
                return false;
            }

            public bool ShouldOutbreak(long now)
            {
                return now >= nextOutbreakTick;
            }

            public void AddInfected(uint id)
            {
                if (!entitiesToAdd.Contains(id))
                    entitiesToAdd.Add(id);
            }

            public void RemoveInfected(uint id)
            {
                if (!entitiesToRemove.Contains(id))
                    entitiesToRemove.Add(id);
            }

            public void UpdateInfectedList(long now)
            {
                if (entitiesToRemove.Count() > 0)
                {
                    foreach (uint id in entitiesToRemove)
                    {
                        infected.Remove(id);
                    }
                    entitiesToRemove = new List<uint>();
                }

                if (entitiesToAdd.Count() > 0)
                {
                    foreach (uint id in entitiesToAdd)
                    {
                        BaseEntity entity = (BaseEntity)BaseNetworkable.serverEntities.Find(id);
                        if (entity != null)
                        {
                            InfectedEntity ie = new InfectedEntity(this, entity);
                            infected.Add(id, ie);
                            ie.SetSymptomTime(now, RandomValue(minSpreadTime, maxSpreadTime));
                            ie.SetRecoveryTime(now, RandomValue(minInfectionTime, maxInfectionTime));
                            ie.isInfected = true;
                            if (plugin.GetConfigBool("General Settings", "Show Infected Message (true/false)") && ie.isPlayer)
                                ShowStatusMessage(entity, plugin.lang.GetMessage("Infected message", plugin, ie.player.UserIDString));
                            if (ie.isPlayer)
                                plugin.ShowIndicatorCUI(ie.player);
                        }
                    }
                    entitiesToAdd = new List<uint>();
                }
            }

            public bool Infect(BaseEntity victim, bool force = false)
            {
                if (force && infected != null && victim != null && infected.ContainsKey(victim.net.ID))
                    infected.Remove(victim.net.ID);
                if (CanInfect(victim))
                {
                    AddInfected(victim.net.ID);
                    totalInfectedCount += 1;
                    return true;
                }
                return false;
            }

            public bool Recover(BaseEntity victim)
            {
                if (infected != null && victim != null && infected.ContainsKey(victim.net.ID))
                {
                    InfectedEntity ie = infected[victim.net.ID];
                    if (ie.isInfected && !ie.isRecovering)
                    {
                        long now = DateTimeOffset.Now.ToUnixTimeSeconds();
                        ie.hasSymptoms = false;
                        ie.isRecovering = true;
                        ie.SetCureTime(now, RandomValue(minImmunityTime, maxImmunityTime));
                        if (plugin.GetConfigBool("General Settings", "Show Recovery Message (true/false)") && ie.isPlayer)
                            ShowStatusMessage(victim, plugin.lang.GetMessage("Recovering message", plugin, ie.player.UserIDString));
                        if (ie.isPlayer)
                            CuiHelper.DestroyUi(ie.player, "IndicatorIMG");
                        return true;
                    }
                }
                return false;
            }

            public bool Cure(BaseEntity victim)
            {
                if (victim != null && victim.net != null && infected.ContainsKey(victim.net.ID))
                {
                    InfectedEntity ie = infected[victim.net.ID];
                    if (plugin.GetConfigBool("General Settings", "Show Cured Message (true/false)") && ie.isPlayer)
                        ShowStatusMessage(victim, plugin.lang.GetMessage("Cured message", plugin, ie.player.UserIDString));
                    if (ie.isPlayer)
                        CuiHelper.DestroyUi(ie.player, "IndicatorIMG");
                    RemoveInfected(victim.net.ID);
                    return true;
                }
                return false;
            }

            #endregion

            #region File IO
            public static void SaveToFile(Disease disease)
            {
                Interface.Oxide.DataFileSystem.WriteObject("Diseases/" + disease.name, disease);
            }

            public static Disease LoadFromFile(string diseaseName, Diseases plugin)
            {
                Disease disease = Interface.Oxide.DataFileSystem.ReadObject<Disease>("Diseases/" + diseaseName);
                disease.plugin = plugin;
                disease.infected = new Dictionary<uint, InfectedEntity>();
                disease.totalInfectedCount = 0;
                disease.totalDeathCount = 0;
                disease.totalOutbreakCount = 0;
                disease.nextDiseaseTick = DateTimeOffset.Now.ToUnixTimeSeconds() + 1;
                disease.entitiesToAdd = new List<uint>();
                disease.entitiesToRemove = new List<uint>();
                return disease;
            }

            public override bool Equals(System.Object obj)
            {
                //Check for null and compare run-time types.
                if ((obj == null) || !this.GetType().Equals(obj.GetType()))
                {
                    return false;
                }
                else
                {
                    return ((Disease)obj).name == name;
                }
            }
            #endregion
        }

        class InfectedEntity
        {
            public Disease disease;
            public BaseEntity entity;
            public BasePlayer player;
            public bool isPlayer;
            public bool isInfected;
            public bool isRecovering;
            public bool hasSymptoms;
            public long symptomTime;
            public long recoveryTime;
            public long cureTime;

            public InfectedEntity(Disease disease, BaseEntity entity)
            {
                this.disease = disease;
                this.entity = entity;
                this.isInfected = false;
                this.isRecovering = false;
                this.isPlayer = (entity is BasePlayer);
                if (isPlayer)
                    this.player = (BasePlayer)entity;
                this.hasSymptoms = true;
            }

            private bool CanSpread()
            {
                return hasSymptoms && (!isPlayer || !player.IsSleeping());
            }

            public void SetSymptomTime(long now, int seconds)
            {
                this.symptomTime = now + seconds;
            }

            public void SetRecoveryTime(long now, int seconds)
            {
                this.recoveryTime = now + seconds;
            }

            public void SetCureTime(long now, int seconds)
            {
                this.cureTime = now + seconds;
            }

            public bool CanBeInfected()
            {
                return !isInfected && (!isPlayer || !player.IsSleeping());
            }

            public bool ShouldSpread(long now)
            {
                return now >= symptomTime && CanSpread();
            }

            public bool ShouldRecover(long now)
            {
                return isInfected && now >= recoveryTime;
            }

            public bool ShouldCure(long now)
            {
                return isRecovering && now >= cureTime;
            }
        }
    }
    #endregion
}
