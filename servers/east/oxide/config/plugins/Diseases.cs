using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Diseases", "mr01sam", "1.0.4")]
    [Description("Players can be inflicted with disease that can be spread to others")]
    public class Diseases : RustPlugin
    {

        private const string PermissionInfo = "diseases.info";
        private const string PermissionAdmin = "diseases.admin";

        List<Disease> diseases = new List<Disease>();

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

        #region Message
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
                ["Invalid command"] = "Invalid command",
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

        private string Color(string text, string colorCode)
        {
            return "<color=" + colorCode + ">" + text + "</color>";
        }
        #endregion

        #region Chat Commands
        [ChatCommand("disease")]
        private void CommandHandler(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (ArgsIs(args, 1, "list") && HasPerm(player, "diseases.info"))
                {
                    CommandPrintList(player);
                    return;
                }
                if (ArgsIs(args, 2, "stats") && HasPerm(player, "diseases.info"))
                {
                    string diseaseName = args[1];
                    CommandPrintStats(player, diseaseName);
                    return;
                }
                if (ArgsIs(args, 2, "outbreak") && HasPerm(player, "diseases.admin"))
                {
                    int numOutbreaks = 1;
                    
                    if (args.Count() >= 3)
                        numOutbreaks = int.Parse(args[2]);
                    string diseaseName = args[1];
                    Disease disease = diseases.Find(x => x.name.ToUpper() == diseaseName.ToUpper());
                    
                    if (disease == null)
                        SendMessage(player, 0, lang.GetMessage("Invalid disease", this, player.UserIDString));

                    int numInfected = 0;
                    for (int i = 0; i < numOutbreaks; i++)
                        if (AttemptOutbreak(disease, true) != null)
                            numInfected++;
                    
                    if (numInfected == 0)
                        SendMessage(player, 0, lang.GetMessage("Outbreak false", this, player.UserIDString));
                    else
                        SendMessage(player, 0, string.Format(lang.GetMessage("Outbreak true", this, player.UserIDString), numInfected));
                    return;
                }
                if (ArgsIs(args, 3, "infect") && HasPerm(player, "diseases.admin"))
                {
                    string playerName = args[1];
                    string diseaseName = args[2];
                    BasePlayer target = BasePlayer.Find(playerName);
                    Disease disease = diseases.Find(x => x.name.ToUpper() == diseaseName.ToUpper());
                    if (target == null)
                    {
                        SendMessage(player, 0, lang.GetMessage("Invalid player", this, player.UserIDString));
                        return;
                    }
                    if (disease == null)
                    {
                        SendMessage(player, 0, lang.GetMessage("Invalid disease", this, player.UserIDString));
                        return;
                    }
                    if (disease.Infect(target, true))
                    {
                        SendMessage(player, 0, string.Format(lang.GetMessage("Infected", this, player.UserIDString), player.displayName, disease.name));
                        return;
                    }
                    return;
                }
                if (ArgsIs(args, 3, "cure") && HasPerm(player, "diseases.admin"))
                {
                    string playerName = args[1];
                    string diseaseName = args[2];
                    BasePlayer target = BasePlayer.Find(playerName);
                    Disease disease = diseases.Find(x => x.name.ToUpper() == diseaseName.ToUpper());
                    if (target == null)
                    {
                        SendMessage(player, 0, lang.GetMessage("Invalid player", this, player.UserIDString));
                        return;
                    }
                    if (disease == null)
                    {
                        SendMessage(player, 0, lang.GetMessage("Invalid disease", this, player.UserIDString));
                        return;
                    }
                    if (disease.Recover(target))
                    {
                        SendMessage(player, 0, string.Format(lang.GetMessage("Cured", this, player.UserIDString), player.displayName, disease.name));
                        return;
                    }
                    return;
                }
            } catch (Exception) {
                SendMessage(player, 0, lang.GetMessage("Invalid command", this, player.UserIDString));
            }; /* Print usage for any exception */
            CommandPrintUsage(player);
        }

        private bool ArgsIs(string[] args, int minLength, string value = null)
        {
            if (args.Length < minLength)
                return false;
            if (value != null && args[0] != value)
                return false;
            return true;
        }

        private void ShowIndicatorCUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "IndicatorIMG");
            var filledTemplate = TEMPLATE.Replace("{imageUrl}", GetConfigString("General Settings", "Indicator image"));
            CuiHelper.AddUi(player, filledTemplate);
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

        private void CommandPrintUsage(BasePlayer player)
        {

            string message = lang.GetMessage("Commands", this, player.UserIDString) + "\n";
            message += "/disease list\n";
            message += "/disease stats <disease name>\n";
            message += "/disease outbreak <disease name>\n";
            message += "/disease outbreak <disease name> <number of outbreaks>\n";
            message += "/disease infect <player name> <disease name>\n";
            message += "/disease cure <player name> <disease name>\n";
            
            SendMessage(player, 0, message);
        }

        private void CommandPrintList(BasePlayer player)
        {
            string message = lang.GetMessage("List", this, player.UserIDString);
            foreach (Disease disease in diseases)
                message += disease.name + ",";
            message = message.Substring(0, message.Length - 1);
            SendMessage(player, 0, message);
        }

        private void CommandPrintStats(BasePlayer player, string diseaseName)
        {
            Disease data = null;
            foreach (Disease disease in diseases)
                if (disease.name.ToUpper() == diseaseName.ToUpper())
                {
                    data = disease;
                    break;
                }
            if (data != null)
            {
                string message = diseaseName.ToUpper() + "\n";
                message += lang.GetMessage("Stat infected current", this, player.UserIDString) + data.infected.Count + "\n";
                message += lang.GetMessage("Stat infected", this, player.UserIDString) + data.totalInfectedCount + "\n";
                message += lang.GetMessage("Stat deaths", this, player.UserIDString) + data.totalDeathCount + "\n";
                message += lang.GetMessage("Stat outbreaks", this, player.UserIDString) + data.totalOutbreakCount + "\n";
                SendMessage(player, 0, message);
            }
        }
        #endregion

        #region Hooks
        void Init()
        {
            /* Register Permissions */
            permission.RegisterPermission(PermissionInfo, this);
            permission.RegisterPermission(PermissionAdmin, this);

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
            Puts(diseases.Count + " diseases loaded");
        }

        void OnTick()
        {
            if (diseases.Count > 0)
            {
                try
                {
                    long now = DateTimeOffset.Now.ToUnixTimeSeconds();
                    foreach (Disease disease in diseases)
                    {
                        /* Outbreak */
                        if (now >= disease.nextOutbreakTick && disease.randomOutbreaksOccur)
                        {
                            BaseEntity inflicted = AttemptOutbreak(disease);
                            disease.nextOutbreakTick = now + disease.randomOutbreakTickInterval;
                        }
                        /* Disease Tick */
                        if (now >= disease.nextDiseaseTick && disease.infected != null && disease.infected.Count > 0)
                        {
                            foreach (InfectedEntity ie in disease.infected.Values)
                            {
                                try
                                {
                                    if (ie != null && (!ie.isPlayer || (!ie.player.IsSleeping())))
                                    {
                                        /* Spread */
                                        if (ie.canSpread && now >= ie.symptomTime)
                                        {
                                            List<BaseEntity> inflictedEntities = AttemptSpread(disease, ie);
                                            if (inflictedEntities != null)
                                                disease.SetNextSymptomTime(ie);
                                        }
                                        /* Recover */
                                        if (ie.cureTime == 0 && now >= ie.recoveryTime)
                                            disease.Recover(ie.entity);

                                        /* Cure */
                                        if (ie.cureTime != 0 && now >= ie.cureTime)
                                            disease.Cure(ie.entity);
                                    }
                                } catch (NullReferenceException)
                                {}
                            }
                            disease.nextDiseaseTick = now + 1;
                        }
                    }
                }
                catch (InvalidOperationException) { };
            }
        }

        void OnPlayerSleepEnded(BasePlayer player)
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
        }

        void OnItemUse(Item item, int amountToUse)
        {
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
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            foreach (Disease disease in diseases)
            {
                if (disease.WasCauseOfDeath(entity, info))
                {
                    disease.totalDeathCount += 1;
                    if (entity is BasePlayer && GetConfigBool("General Settings", "Show Death Message (true/false)") == true)
                    {
                        BasePlayer player = (BasePlayer)entity;
                        string color = GetConfigString("General Settings", "Disease Name Color");
                        SendMessage(player, 0, String.Format(lang.GetMessage("Death message", this, player.UserIDString), player.displayName, Color(disease.name.TitleCase(), color)));
                    }
                }
                
                if (disease.infected.ContainsKey(entity.net.ID))
                {
                    disease.infected.Remove(entity.net.ID);
                    if (entity is BasePlayer)
                        CuiHelper.DestroyUi((BasePlayer) entity, "IndicatorIMG");
                }
                    
            }
        }

        #endregion

        #region Helper

        private List<BaseEntity> AttemptSpread(Disease disease, InfectedEntity spreader)
        {
            if (spreader != null && spreader.entity is BaseCombatEntity && spreader.hasSymptoms)
            {
                foreach (string effect in disease.symptomEffects)
                    PlayEffect(spreader.entity, effect);
                if (spreader.isPlayer)
                {
                    spreader.player.metabolism.calories.value -= Math.Min(disease.damageCalories, spreader.player.metabolism.calories.value);
                    spreader.player.metabolism.hydration.value -= Math.Min(disease.damageHydration, spreader.player.metabolism.hydration.value);
                }
                try
                {
                    ((BaseCombatEntity)spreader.entity).Hurt(disease.damageHealth, Rust.DamageType.Poison);
                } catch (NullReferenceException)
                {
                    ((BaseCombatEntity)spreader.entity).health -= disease.damageHealth;
                }

                BaseEntity origin = spreader.entity;
                Vector3 position = new Vector3(origin.transform.position.x, origin.transform.position.y, origin.transform.position.z);
                float distance = disease.infectionSpreadDistance;
                List<BaseEntity> infected = new List<BaseEntity>();
                List<BaseEntity> nearby = new List<BaseEntity>();
                Vis.Entities<BaseEntity>(position, distance, nearby); foreach (BaseEntity entity in nearby)
                {
                    if (disease.CanInfect(entity))
                    {
                        int chance = disease.infectionChanceUncovered;
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
            return new List<BaseEntity>() { };
        }

        private BaseEntity AttemptOutbreak(Disease disease, bool force = false)
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

        #region Disease Class
        class Disease
        {
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

            public string name;
            public int minInfectionTime;
            public int maxInfectionTime;
            public int minSpreadTime;
            public int maxSpreadTime;
            public int minImmunityTime;
            public int maxImmunityTime;
            public int infectionChanceCovered;
            public int infectionChanceUncovered;
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
                this.damageHealth = 10;
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
                this.randomOutbreakTickInterval = 300;
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
                    "assets/bundled/prefabs/fx/water/midair_splash.prefab"
                };
                this.totalInfectedCount = 0;
                this.totalDeathCount = 0;
                this.totalOutbreakCount = 0;
                this.nextDiseaseTick = DateTimeOffset.Now.ToUnixTimeSeconds() + 1;
            }

            public bool HasInfected(BaseEntity entity)
            {
                if (infected != null && infected.Count > 0)
                    return infected.ContainsKey(entity.net.ID);
                return false;
            }

            public bool CanInfect(BaseEntity entity) {
                if (infected != null && infectableEntities != null)
                {
                    if (entity is BasePlayer && ((BasePlayer)entity).IsSleeping())
                        return false;
                    return !infected.ContainsKey(entity.net.ID) && infectableEntities.Contains(entity.ShortPrefabName);
                }
                return false;
            }

            public bool Infect(BaseEntity entity, bool force=false)
            {
                if (force && infected != null && infected.ContainsKey(entity.net.ID))
                    infected.Remove(entity.net.ID);
                if (CanInfect(entity))
                {
                    infected.Add(entity.net.ID, new InfectedEntity(this, entity));
                    InfectedEntity ie = infected[entity.net.ID];
                    SetNextSymptomTime(ie);
                    SetNextRecoverTime(ie);
                    if (plugin.GetConfigBool("General Settings", "Show Infected Message (true/false)") && ie.isPlayer)
                        ShowStatusMessage(entity, plugin.lang.GetMessage("Infected message", plugin, ie.player.UserIDString));
                    totalInfectedCount += 1;
                    if (ie.isPlayer)
                        plugin.ShowIndicatorCUI(ie.player);
                    return true;
                }
                return false;
            }

            public bool Recover(BaseEntity entity)
            {
                if (infected != null && infected.ContainsKey(entity.net.ID))
                {
                    InfectedEntity ie = infected[entity.net.ID];
                    if (ie.canSpread == true || ie.hasSymptoms == true)
                    {
                        ie.hasSymptoms = false;
                        ie.canSpread = false;
                        SetNextCureTime(ie);
                        if (plugin.GetConfigBool("General Settings", "Show Recovery Message (true/false)") && ie.isPlayer)
                            ShowStatusMessage(entity, plugin.lang.GetMessage("Recovering message", plugin, ie.player.UserIDString));
                        if (ie.isPlayer)
                            CuiHelper.DestroyUi(ie.player, "IndicatorIMG");
                        return true;
                    }
                }
                return false;
            }

            public bool Cure(BaseEntity entity)
            {
                if (entity != null && entity.net != null && infected.ContainsKey(entity.net.ID))
                {
                    InfectedEntity ie = infected[entity.net.ID];
                    infected.Remove(entity.net.ID);
                    if (plugin.GetConfigBool("General Settings", "Show Cured Message (true/false)") && ie.isPlayer)
                        ShowStatusMessage(entity, plugin.lang.GetMessage("Cured message", plugin, ie.player.UserIDString));
                    return true;
                    if (ie.isPlayer)
                        CuiHelper.DestroyUi(ie.player, "IndicatorIMG");
                }
                return false;
            }

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

            public void SetNextSymptomTime(InfectedEntity ie)
            {
                long now = DateTimeOffset.Now.ToUnixTimeSeconds();
                ie.symptomTime = now + UnityEngine.Random.Range(this.minSpreadTime, this.maxSpreadTime);
            }

            public void SetNextRecoverTime(InfectedEntity ie)
            {
                long now = DateTimeOffset.Now.ToUnixTimeSeconds();
                ie.recoveryTime = now + UnityEngine.Random.Range(this.minInfectionTime, this.maxInfectionTime);
            }

            public void SetNextCureTime(InfectedEntity ie)
            {
                long now = DateTimeOffset.Now.ToUnixTimeSeconds();
                ie.cureTime = now + UnityEngine.Random.Range(this.minImmunityTime, this.maxImmunityTime);
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
        }
        #endregion

        #region InfectedEntity
        class InfectedEntity
        {
            public Disease disease;
            public BaseEntity entity;
            public BasePlayer player;
            public bool isPlayer;
            public bool canSpread;
            public bool hasSymptoms;
            public long symptomTime;
            public long recoveryTime;
            public long cureTime;

            public InfectedEntity(Disease disease, BaseEntity entity)
            {
                this.disease = disease;
                this.entity = entity;
                this.isPlayer = (entity is BasePlayer);
                if (isPlayer)
                    this.player = (BasePlayer)entity;
                this.canSpread = true;
                this.hasSymptoms = true;
            }
        }
        #endregion

    }
}
