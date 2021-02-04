using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Barrelless", "Krungh Crow", "2.0.3")]
    [Description("Entities spawn after player destroys a barrel")]
    class Barrelless : RustPlugin
    {
        #region Variables
        /*Random object*/
        System.Random rnd = new System.Random();
        /*prefab strings*/
        const string bearString = "assets/rust.ai/agents/bear/bear.prefab";
        const string boarstring = "assets/rust.ai/agents/boar/boar.prefab";
        const string chickenString = "assets/rust.ai/agents/chicken/chicken.prefab";
        const string wolfString = "assets/rust.ai/agents/wolf/wolf.prefab";
        const string scientistString = "assets/prefabs/npc/scientist/scientist.prefab";
        const string peacekeeperString = "assets/prefabs/npc/scientist/scientistpeacekeeper.prefab";
        const string zombieString = "assets/prefabs/npc/murderer/murderer.prefab";
        const string scarecrowString = "assets/prefabs/npc/scarecrow/scarecrow.prefab";
        const string airdropString = "assets/prefabs/misc/supply drop/supply_drop.prefab";
        const string beancanString = "assets/prefabs/weapons/beancan grenade/grenade.beancan.deployed.prefab";
        const string fireString = "assets/prefabs/npc/m2bradley/oilfireball2.prefab";
        const string fire2String = "assets/bundled/prefabs/oilfireballsmall.prefab";
        const string fire3String = "assets/bundled/prefabs/fireball.prefab";
        //files
        const string file_main = "barrelless_players/";

        #endregion

        #region Configuration
        void Init()
        {
            if (!LoadConfigVariables())
            {
                Puts("Config file issue detected. Please delete file, or check syntax and fix.");
                return;
            }
            //permission.RegisterPermission(Tier2_Perm, this);
        }
        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Drop Settings")]
            public SettingsDrop DropData = new SettingsDrop();
            [JsonProperty(PropertyName = "Scientist Settings")]
            public SettingsScientist ScientistData = new SettingsScientist();
            [JsonProperty(PropertyName = "Peacekeeper Settings")]
            public SettingsPeacekeeper PeaceData = new SettingsPeacekeeper();
            [JsonProperty(PropertyName = "Scarecrow Settings")]
            public SettingsCrow CrowData = new SettingsCrow();
            [JsonProperty(PropertyName = "Zombie Settings")]
            public SettingsZomb ZombData = new SettingsZomb();
            [JsonProperty(PropertyName = "Animal Settings")]
            public SettingsAnimal AnimalData = new SettingsAnimal();
            [JsonProperty(PropertyName = "Fire Settings")]
            public SettingsFire FireData = new SettingsFire();
        }
        class SettingsScientist
        {
            [JsonProperty(PropertyName = "Scientist : Chance on spawn (1-00)")]
            public int ScientistRate { get; set; } = 0;
            [JsonProperty(PropertyName = "Scientist : Use Custom Name")]
            public bool ScientistCustom = false;
            [JsonProperty(PropertyName = "Scientist : Name")]
            public string ScientistName = "BarrelScientist";
            [JsonProperty(PropertyName = "Scientist : Health")]
            public int ScientistHealth { get; set; } = 200;
        }
        class SettingsPeacekeeper
        {
            [JsonProperty(PropertyName = "Peacekeeper : Chance on spawn (1-00)")]
            public int PeacekeeperRate { get; set; } = 0;
            [JsonProperty(PropertyName = "Peacekeeper : Use Custom Name")]
            public bool PeaceCustom = false;
            [JsonProperty(PropertyName = "Peacekeeper : Name")]
            public string PeaceName = "BarrelKeeper";
            [JsonProperty(PropertyName = "PeaceKeeper : Health")]
            public int PeaceHealth { get; set; } = 300;
        }
        class SettingsCrow
        {
            [JsonProperty(PropertyName = "Scarecrow : Chance on spawn (1-00)")]
            public int CrowRate { get; set; } = 0;
            [JsonProperty(PropertyName = "Scarecrow : Use Custom Name")]
            public bool CrowCustom = false;
            [JsonProperty(PropertyName = "Scarecrow : Name")]
            public string CrowName = "BarrelCrow";
            [JsonProperty(PropertyName = "Scarecrow : Health")]
            public int CrowHealth { get; set; } = 300;
        }
        class SettingsZomb
        {
            [JsonProperty(PropertyName = "Zombie : Chance on spawn (1-00)")]
            public int ZombRate { get; set; } = 0;
            [JsonProperty(PropertyName = "Zombie : Use clothes")]
            public bool UseClothes = false;
            [JsonProperty(PropertyName = "Zombie : Use Custom Name")]
            public bool ZombCustom = false;
            [JsonProperty(PropertyName = "Zombie : Name")]
            public string ZombName = "BarrelZombie";
            [JsonProperty(PropertyName = "Zombie : Health")]
            public int ZombHealth { get; set; } = 250;
        }
        class SettingsAnimal
        {
            [JsonProperty(PropertyName = "Bear : Chance on spawn (1-00)")]
            public int BearRate { get; set; } = 0;
            [JsonProperty(PropertyName = "Wolf : Chance on spawn (1-00)")]
            public int WolfRate { get; set; } = 0;
            [JsonProperty(PropertyName = "Chicken : Chance on spawn (1-00)")]
            public int ChickenRate { get; set; } = 0;
            [JsonProperty(PropertyName = "Boar : Chance on spawn (1-00)")]
            public int BoarRate { get; set; } = 0;
        }
        class SettingsDrop
        {
            [JsonProperty(PropertyName = "Drop : Count random per x barrels")]
            public int Barrelcountdrop { get; set; } = 1;
            [JsonProperty(PropertyName = "Drop : Spawn only 1 entity on trigger")]
            public bool Trigger = false;
            [JsonProperty(PropertyName = "Airdrop : Chance on spawn (1-00)")]
            public int AirdropRate { get; set; } = 0;
            [JsonProperty(PropertyName = "Airdrop : Drop height")]
            public int AirdropHeight { get; set; } = 20;
            [JsonProperty(PropertyName = "Beancan : Chance on spawn (1-00)")]
            public int BeancanRate { get; set; } = 0;
        }
        class SettingsFire
        {
            [JsonProperty(PropertyName = "Fire : Trigger only on oil/diesel barrels")]
            public bool FireOnly { get; set; } = false;
            [JsonProperty(PropertyName = "Fire (Large) : Chance on spawn (1-00)")]
            public int FireRate { get; set; } = 0;
            [JsonProperty(PropertyName = "Fire (Medium): Chance on spawn (1-00)")]
            public int Fire2Rate { get; set; } = 0;
            [JsonProperty(PropertyName = "Fire (Small): Chance on spawn (1-00)")]
            public int Fire3Rate { get; set; } = 0;
        }

        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConf();
            return true;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Fresh install detected Creating a new config file.");
            configData = new ConfigData();
            SaveConf();
        }

        void SaveConf() => Config.WriteObject(configData, true);
        #endregion

        #region LanguageApi
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Msg_Bearspawned"] = "<color=yellow>A wild Bear just appeared!</color>",
                ["Msg_Chickenspawned"] = "<color=yellow>tok...toktoktok!</color>",
                ["Msg_BoarSpawned"] = "<color=yellow>Oink oink! a wild Boar spawned</color>",
                ["Msg_Wolfspawned"] = "<color=yellow>A wild Wolf just appeared!</color>",
                ["Msg_Scienistspawned"] = "<color=yellow>A Scienist saw you destroying barrels!</color>",
                ["Msg_Peacekeeperspawned"] = "<color=yellow>A Heavy Scienist saw you destroying barrels!</color>",
                ["Msg_Airdropspawned"] = "<color=yellow>An Airdrop has been sent to this location!</color>",
                ["Msg_Scarecrowspawned"] = "<color=yellow>A Scarecrow just appeared!</color>",
                ["Msg_Zombiespawned"] = "<color=yellow>A Zombie just appeared!</color>",
                ["Msg_Beancanspawned"] = "<color=yellow>A small explosive fell out of the barrel!</color>",
                ["Msg_Firespawned"] = "<color=yellow>Oops its lit!</color>"

            }, this);
        }
        #endregion

        #region Hooks

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            //Checks if entity is a barrel.          
            if (!entity.ShortPrefabName.StartsWith("loot-barrel") && !entity.ShortPrefabName.StartsWith("loot_barrel") && entity.ShortPrefabName != "oil_barrel" && entity.ShortPrefabName != "diesel_barrel_world")
                return;

            if (CheckPlayer(info) == false)
            {
                return;
            }
            else
            {
                Playerinfo user = get_user(info.InitiatorPlayer);
                if ( user.barrelCount < configData.DropData.Barrelcountdrop)
                {
                    user.barrelCount += 1;
                    update_user(info.InitiatorPlayer,user);
                }
                else
                {
                    user.barrelCount = 0;
                    update_user(info.InitiatorPlayer, user);

                    if (entity.transform.position != null)
                    {
                        if (SpawnRate(configData.AnimalData.BearRate) == true)
                        {
                            SpawnBear(bearString, entity.transform.position);
                            SendMsg(info.InitiatorPlayer, "Msg_Bearspawned");
                            if (configData.DropData.Trigger)
                            return;
                        }
                        //else
                        if (SpawnRate(configData.AnimalData.ChickenRate) == true)
                        {
                            SpawnChicken(chickenString, entity.transform.position);
                            SendMsg(info.InitiatorPlayer, "Msg_Chickenspawned");
                            if (configData.DropData.Trigger)
                            return;
                        }
                        //else
                        if (SpawnRate(configData.AnimalData.BoarRate) == true)
                        {
                            SpawnBoar(boarstring, entity.transform.position);
                            SendMsg(info.InitiatorPlayer, "Msg_BoarSpawned");
                            if (configData.DropData.Trigger)
                            return;
                        }
                        //else
                        if (SpawnRate(configData.AnimalData.WolfRate) == true)
                        {
                            SpawnWolf(wolfString, entity.transform.position);
                            SendMsg(info.InitiatorPlayer, "Msg_Wolfspawned");
                            if (configData.DropData.Trigger)
                            return;
                        }
                        //else
                        if (SpawnRate(configData.ScientistData.ScientistRate) == true)
                        {
                            SpawnScientist(scientistString, entity.transform.position);
                            SendMsg(info.InitiatorPlayer, "Msg_Scienistspawned");
                            if (configData.DropData.Trigger)
                            return;
                        }
                        //else
                        if (SpawnRate(configData.PeaceData.PeacekeeperRate) == true)
                        {
                            SpawnPeacekeeper(peacekeeperString, entity.transform.position);
                            SendMsg(info.InitiatorPlayer, "Msg_Peacekeeperspawned");
                            if (configData.DropData.Trigger)
                            return;
                        }
                        //else
                        if (SpawnRate(configData.DropData.AirdropRate) == true)
                        {
                            SpawnSupplyCrate(airdropString, entity.transform.position);
                            SendMsg(info.InitiatorPlayer, "Msg_Airdropspawned");
                            if (configData.DropData.Trigger)
                            return;
                        }
                        //else
                        if (SpawnRate(configData.DropData.BeancanRate) == true)
                        {
                            SpawnBeancan(beancanString, entity.transform.position);
                            SendMsg(info.InitiatorPlayer, "Msg_Beancanspawned");
                            if (configData.DropData.Trigger)
                            return;
                        }
                        //else
                        if (SpawnRate(configData.ZombData.ZombRate) == true)
                        {
                            SpawnZombie(zombieString, entity.transform.position);
                            SendMsg(info.InitiatorPlayer, "Msg_Zombiespawned");
                            if (configData.DropData.Trigger)
                            return;
                        }
                        //else
                        if (SpawnRate(configData.CrowData.CrowRate) == true)
                        {
                            SpawnScarecrow(scarecrowString, entity.transform.position);
                            SendMsg(info.InitiatorPlayer, "Msg_Scarecrowspawned");
                            if (configData.DropData.Trigger)
                            return;
                        }
                        //else
                        if (SpawnRate(configData.FireData.FireRate) == true)
                        {
                            if (configData.FireData.FireOnly == false)
                            {
                                SpawnFire(fireString, entity.transform.position);
                                SendMsg(info.InitiatorPlayer, "Msg_Firespawned");
                                if (configData.DropData.Trigger)
                                    return;
                            }
                        }
                        if (SpawnRate(configData.FireData.FireRate) == true)
                        {
                            if (configData.FireData.FireOnly == true && !entity.ShortPrefabName.StartsWith("loot-barrel") && !entity.ShortPrefabName.StartsWith("loot_barrel"))
                            {
                                SpawnFire(fireString, entity.transform.position);
                                SendMsg(info.InitiatorPlayer, "Msg_Firespawned");
                                if (configData.DropData.Trigger)
                                    return;
                            }
                            else
                            {
                                return;
                            }
                        }
                        //else
                        if (SpawnRate(configData.FireData.Fire2Rate) == true)
                        {
                            if (configData.FireData.FireOnly == false)
                            {
                                Spawn2Fire(fire2String, entity.transform.position);
                                SendMsg(info.InitiatorPlayer, "Msg_Firespawned");
                                if (configData.DropData.Trigger)
                                    return;
                            }
                        }
                        if (SpawnRate(configData.FireData.Fire2Rate) == true)
                        {
                            if (configData.FireData.FireOnly == true && !entity.ShortPrefabName.StartsWith("loot-barrel") && !entity.ShortPrefabName.StartsWith("loot_barrel"))
                            {
                                Spawn2Fire(fire2String, entity.transform.position);
                                SendMsg(info.InitiatorPlayer, "Msg_Firespawned");
                                if (configData.DropData.Trigger)
                                    return;
                            }
                            else
                            {
                                return;
                            }
                        }
                        //else
                        if (SpawnRate(configData.FireData.Fire3Rate) == true)
                        {
                            if (configData.FireData.FireOnly == false)
                            {
                                Spawn3Fire(fire3String, entity.transform.position);
                                SendMsg(info.InitiatorPlayer, "Msg_Firespawned");
                                if (configData.DropData.Trigger)
                                    return;
                            }
                        }
                        if (SpawnRate(configData.FireData.Fire3Rate) == true)
                        {
                            if (configData.FireData.FireOnly == true && !entity.ShortPrefabName.StartsWith("loot-barrel") && !entity.ShortPrefabName.StartsWith("loot_barrel"))
                            {
                                Spawn3Fire(fire3String, entity.transform.position);
                                SendMsg(info.InitiatorPlayer, "Msg_Firespawned");
                                if (configData.DropData.Trigger)
                                    return;
                            }
                            else
                            {
                                return;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Methodes

            private bool SpawnRate(int npcRate)
        {
            if (rnd.Next(1, 101) <= npcRate)
            {
                return true;
            }
            return false;
        }

        //Checks if hitinfo is a Baseplayer
        private bool CheckPlayer(HitInfo info)
        {
            bool Checker = false;
            BasePlayer player = info.InitiatorPlayer;
            if (player != null)
            {
                Checker = true;
            }

            return Checker;
        }

        private void SpawnSupplyCrate(string prefab, Vector3 position)
        {
            Vector3 newPosition = position + new Vector3(0, configData.DropData.AirdropHeight, 0);
            BaseEntity SupplyCrateEntity = GameManager.server.CreateEntity(prefab, newPosition);
            if (SupplyCrateEntity != null)
            {
                SupplyDrop Drop = SupplyCrateEntity.GetComponent<SupplyDrop>();
                Drop.Spawn();
            }

        }

        private void SpawnBeancan(string prefab, Vector3 position)
        {
            BaseEntity Beancan = GameManager.server.CreateEntity(prefab, position);
            if (Beancan != null)
            {
                Beancan.Spawn();
            }
        }

        private void SpawnBear(string prefab, Vector3 position)
        {
            BaseEntity Bear = GameManager.server.CreateEntity(prefab, position);
            if (Bear != null)
            {
                Bear.Spawn();
            }
        }

        private void SpawnChicken(string prefab, Vector3 position)
        {
            BaseEntity Chicken = GameManager.server.CreateEntity(prefab, position);
            if (Chicken != null)
            {
                Chicken.Spawn();
            }
        }

        private void SpawnBoar(string prefab, Vector3 position)
        {
            BaseEntity Boar = GameManager.server.CreateEntity(prefab, position);
            if (Boar != null)
            {
                Boar.Spawn();
            }
        }

        private void SpawnWolf(string prefab, Vector3 position)
        {
            BaseEntity Wolf = GameManager.server.CreateEntity(prefab, position);
            if (Wolf != null)
            {
                Wolf.Spawn();
            }
        }

        private void SpawnScientist(string prefab, Vector3 position)
        {
            NPCPlayerApex scientist = (NPCPlayerApex)GameManager.server.CreateEntity(prefab, position, new Quaternion(), true);

            if (scientist != null)
            {
                scientist.Spawn();
                {
                    if (configData.ScientistData.ScientistCustom)
                    {
                    scientist.displayName = configData.ScientistData.ScientistName;
                        {
                            (scientist as Scientist).LootPanelName = configData.ScientistData.ScientistName;
                        }
                        scientist.startHealth = configData.ScientistData.ScientistHealth;
                        scientist.InitializeHealth(configData.ScientistData.ScientistHealth, configData.ScientistData.ScientistHealth);
                    }
                }
            }
        }

        private void SpawnPeacekeeper(string prefab, Vector3 position)
        {
            NPCPlayerApex peacekeeper = (NPCPlayerApex)GameManager.server.CreateEntity(prefab, position, new Quaternion(), true);

            if (peacekeeper != null)
            {
                peacekeeper.Spawn();
                {
                    if (configData.PeaceData.PeaceCustom)
                    {
                         peacekeeper.displayName = configData.PeaceData.PeaceName;
                        {
                            (peacekeeper as Scientist).LootPanelName = configData.PeaceData.PeaceName;
                        }
                        peacekeeper.startHealth = configData.PeaceData.PeaceHealth;
                        peacekeeper.InitializeHealth(configData.PeaceData.PeaceHealth, configData.PeaceData.PeaceHealth);
                    }
                }
            }
        }

        private void SpawnZombie(string prefab, Vector3 position)
        {
            NPCPlayerApex zombie = (NPCPlayerApex)GameManager.server.CreateEntity(prefab, position, new Quaternion(), true);
            if (zombie != null)
            {
                zombie.Spawn();
                {
                    if (configData.ZombData.ZombCustom)
                    {
                        zombie.displayName = configData.ZombData.ZombName;
                    }
                    zombie.startHealth = configData.ZombData.ZombHealth;
                    zombie.InitializeHealth(configData.ZombData.ZombHealth, configData.ZombData.ZombHealth);
                    if (configData.ZombData.UseClothes)
                    {
                        var inv_belt = zombie.inventory.containerBelt;
                        var inv_wear = zombie.inventory.containerWear;

                        Item outfit = ItemManager.CreateByName("halloween.surgeonsuit", 1, 0);
                        Item eyes = ItemManager.CreateByName("gloweyes", 1, 0);

                        inv_wear.Clear();
                        if (outfit != null) outfit.MoveToContainer(inv_wear);
                        if (eyes != null) eyes.MoveToContainer(inv_wear);
                    }
                }
            }
        }

        private void SpawnScarecrow(string prefab, Vector3 position)
        {
            HTNPlayer scarecrow = (HTNPlayer)GameManager.server.CreateEntity(prefab, position, new Quaternion(), true);
            if (scarecrow != null)
            {
                scarecrow.Spawn();
                {
                if (configData.CrowData.CrowCustom)
                    {
                        scarecrow.displayName = configData.CrowData.CrowName;
                        scarecrow.LootPanelName = configData.CrowData.CrowName;
                    }
                scarecrow.startHealth = configData.CrowData.CrowHealth;
                scarecrow.InitializeHealth(configData.CrowData.CrowHealth, configData.CrowData.CrowHealth);
                }
            }
        }

        private void SpawnFire(string prefab, Vector3 position)
        {
            BaseEntity fire = GameManager.server.CreateEntity(fireString, position);
            if (fire != null)
            {
                fire.Spawn();
            }
        }

        private void Spawn2Fire(string prefab, Vector3 position)
        {
            BaseEntity fire2 = GameManager.server.CreateEntity(fire2String, position);
            if (fire2 != null)
            {
                fire2.Spawn();
            }
        }

        private void Spawn3Fire(string prefab, Vector3 position)
        {
            BaseEntity fire3 = GameManager.server.CreateEntity(fire3String, position);
            if (fire3 != null)
            {
                fire3.Spawn();
            }
        }

        Playerinfo get_user(BasePlayer player)
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(file_main + player.UserIDString))
            {
                Playerinfo user = new Playerinfo();
                user.userName = player.displayName.ToString();
                user.barrelCount = 0;
                update_user(player, user);
                return user;
            }
            else
            {
                string raw_player_file = Interface.Oxide.DataFileSystem.ReadObject<string>(file_main + player.UserIDString);
                return JsonConvert.DeserializeObject<Playerinfo>(raw_player_file);
            }
        }

        void update_user(BasePlayer player, Playerinfo user)
        {
            Interface.Oxide.DataFileSystem.WriteObject<string>(file_main + player.UserIDString, JsonConvert.SerializeObject(user));
        }

        #endregion

        #region Helpers
        //Send message to a player by giving baseplayer and key of the dictionary.
        private void SendMsg(BasePlayer player, string key)
        {
            PrintToChat(player, lang.GetMessage(key, this, player.UserIDString));
        }
        #endregion

        #region Classes
        public class Playerinfo
        {
            private string _userName;
            private int _barrelCount;

            public Playerinfo()
            {

            }

            public int barrelCount
            {
                get { return _barrelCount; }
                set { _barrelCount = value; }
            }

            public string userName
            {
                get { return _userName; }
                set { _userName = value; }
            }

        }
        #endregion
    }
}