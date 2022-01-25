using System.Linq; 
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    //  Fixed
    //  Various npc types not working after December update
    //  Health for latest AI fixed. 

    [Info("NPCKits", "Steenamaroo", "1.1.3", ResourceId = 29)]    
    [Description("Give custom Kits to all default Rust npc types.")]

    public class NPCKits : RustPlugin
    {
        #region Declarations
        bool loaded;
        [PluginReference] Plugin Kits; 
        public System.Random random = new System.Random();  
        public Dictionary<ulong, Settings> LiveNpcs = new Dictionary<ulong, Settings>();
        public Dictionary<ulong, Inv> botInventories = new Dictionary<ulong, Inv>();

        public class Inv
        {
            public List<InvContents>[] inventory = { new List<InvContents>(), new List<InvContents>(), new List<InvContents>() };   
        }

        public class InvContents 
        {
            public int ID;
            public int amount;
            public ulong skinID;  
        }  
        #endregion

        #region Hooks 
        void OnServerInitialized() 
        {
            if (!LoadConfigVariables())
            {
                Puts("Config file issue detected. Please delete file, or check syntax and fix.");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            loaded = true;
            foreach (BasePlayer player in BaseNetworkable.serverEntities.OfType<BasePlayer>())
                if (player is NPCPlayer || player is global::HumanNPC)
                    AddToBotList(player);
        }
        #endregion

        #region BotHandling
        void OnEntitySpawned(BaseEntity entity)
        {
            if (!loaded || entity == null) return;
            timer.Once(0.3f, () =>
            {
                if (entity != null && (entity is NPCPlayer)) 
                    AddToBotList(entity.GetComponent<BasePlayer>()); 
            });

            var corpse = entity as LootableCorpse;
            if (corpse != null && LiveNpcs.ContainsKey(corpse.playerSteamID))
            {
                var pos = corpse.transform.position;
                Inv botInv = new Inv();
                ulong id = corpse.playerSteamID;
                Settings record;
                record = LiveNpcs[id]; 
                timer.Once(0.3f, () =>
                {
                    if (corpse == null || !botInventories.ContainsKey(id))
                        return;
                    botInv = botInventories[id]; 

                    var ARL = record.Default_Rust_Loot_Percent >= random.Next(1, 101);
                    var WM = record.Wipe_Main_Inventory_Percent >= random.Next(1, 101);
                    var WC = record.Wipe_Clothing_Percent >= random.Next(1, 101);
                    var WB = record.Wipe_Belt_Percent >= random.Next(1, 101);
                    if (!ARL)
                        corpse.containers[0].Clear();

                    for (int i = 0; i < botInv.inventory.Length; i++)
                    {
                        if (i == 0 && WM || i == 1 && WC || i == 2 && WB)
                            continue;

                        foreach (var item in botInv.inventory[i])
                        {
                            var giveItem = ItemManager.CreateByItemID(item.ID, item.amount, item.skinID);
                            giveItem.MoveToContainer(corpse.containers[i], -1, true);
                        }
                    }
                    LiveNpcs.Remove(corpse.playerSteamID);
                    botInventories.Remove(id);
                });
            }
        }

        float GetPercent(int min, int max, float weapMax)
        {
            min = Mathf.Max(1, min);
            max = Mathf.Max(1, max);
            if (min >= max)
                return weapMax / 100f * max;
            return weapMax / 100f * random.Next(min, max);
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info) => OnEntityKill(player); 
        void OnEntityKill(BaseNetworkable entity)
        {
            BasePlayer npc = entity as BasePlayer;
            if (npc?.userID != null && LiveNpcs.ContainsKey(npc.userID) && !botInventories.ContainsKey(npc.userID))
            {
                Item activeItem = npc.GetActiveItem();
                var record = LiveNpcs[npc.userID];
                int chance = random.Next(1, 101);
                if (record.Weapon_Drop_Percent >= chance && activeItem != null)
                {

                    activeItem.condition = GetPercent(record.Min_Weapon_Drop_Condition_Percent, record.Max_Weapon_Drop_Condition_Percent, activeItem.info.condition.max);

                    var held = activeItem.GetHeldEntity();
                    if (held != null)
                    {
                        BaseProjectile gun = held as BaseProjectile;
                        if (gun != null)
                        {
                            bool wipeAmmo = record.Dropped_Weapon_Has_Ammo_Percent_Chance < random.Next(1, 101);  
                            gun.primaryMagazine.contents = wipeAmmo ? 0 : gun.primaryMagazine.capacity;
                            gun.SendNetworkUpdateImmediate();
                            activeItem.Drop(npc.eyes.position, new Vector3(), new Quaternion()); 
                            npc.svActiveItemID = 0;
                            npc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                        }
                    }
                }

                ItemContainer[] source = { npc.inventory.containerMain, npc.inventory.containerWear, npc.inventory.containerBelt };
                Inv botInv = new Inv();
                botInventories.Add(npc.userID, botInv);
                for (int i = 0; i < source.Length; i++) 
                {
                    foreach (var item in source[i].itemList)
                    {
                        botInv.inventory[i].Add(new InvContents
                        {
                            ID = item.info.itemid,
                            amount = item.amount,
                            skinID = item.skin,
                        });  
                    }
                }
            }
        }
         
        void AddToBotList(BasePlayer player)  
        {
            if (player == null)
                return;
            if (Interface.CallHook("OnNpcKits", player.userID) != null)
                return;
            if (Interface.CallHook("OnNpcKits", player) != null)
                return;

            foreach (var entry in names)
                if (player.ShortPrefabName.Contains(entry.Key))
                { 
                    ProcessNpc(player, entry.Value);
                    return;  
                }

            if (player is NPCPlayer) 
            { 
                var instance = player?.GetComponent<SpawnPointInstance>()?.parentSpawnPoint;
                if (instance != null)
                {
                    var name = instance?.GetComponentInParent<PrefabParameters>()?.ToString();
                    if (name != null) 
                    {
                        foreach (var n in names)
                            if (name.Contains(n.Key))
                            {
                                ProcessNpc(player, n.Value);
                                return;
                            }
                    }
                }
            }
            //Puts($"Prefab name {player.PrefabName}");
            //foreach (var p in BasePlayer.activePlayerList)
            //    p.SendConsoleCommand("ddraw.line", 30f, Color.blue, player.transform.position, player.transform.position + new Vector3(0, 100, 0));
        }

        Dictionary<string, string> names = new Dictionary<string, string>()
        {
            {"oilrig", "OilRig"},
            {"excavator", "Excavator"},
            {"peacekeeper", "CompoundScientist"},
            {"bandit_guard", "BanditTown"},
            {"_ch47_gunner", "MountedScientist"},
            {"junkpile", "JunkPileScientist"},
            {"scarecrow", "ScareCrow"},
            {"military_tunnel", "MilitaryTunnelScientist" },
            {"scientist_full", "MilitaryTunnelScientist"},
            {"scientist_turret", "CargoShip"},
            {"scientist_astar", "CargoShip"}, 
            {"scientistnpc_cargo", "CargoShip"},
            {"_heavy", "HeavyScientist"},
            {"tunneldweller" , "TunnelDweller"},
            {"underwaterdweller" , "UnderwaterDweller"},
            {"trainyard" , "Trainyard"},
            {"airfield" , "Airfield"},
            {"missionprovider_fishing", "MissionProviderOutpost" },
            {"missionprovider_outpost", "MissionProviderFishing" },
            {"missionprovider_stables", "MissionProviderStables" },
            {"missionprovider_bandit", "MissionProviderBandit" },
            {"boat_shopkeeper", "BoatShopkeeper" },
            {"bandit_shopkeeper", "BanditShopkeeper" },
            {"scientistnpc_roamtethered", "DesertScientist" }
        };

        object GetKitInfo(string name) => Kits?.Call("GetKitInfo", name, true);
        object GiveKit(BasePlayer npc, string name) => Kits?.Call($"GiveKit", npc, name, true);

        void ProcessNpc(BasePlayer player, string NPCType) 
        {
            var record = configData.CorpseTypes[NPCType]; 
            if (record == null || !record.enabled)
                return; 
            if (LiveNpcs.ContainsKey(player.userID)) 
                return;

            LiveNpcs.Add(player.userID, record);

            if (record.Health > 0)
            {
                player._maxHealth = record.Health;
                player.startHealth = player._maxHealth;
                player.InitializeHealth(record.Health, record.Health);
            }

            if (record.Kits != null && record.Kits.Count != 0)
            {
                int kitRnd = random.Next(record.Kits.Count); 
                if (record.Kits[kitRnd] != null)
                {
                    object checkKit = GetKitInfo(record.Kits[kitRnd]); 
                    if (checkKit == null)
                        PrintWarning($"Kit {record.Kits[kitRnd]} does not exist.");  
                    else
                    {
                        if (record.Wipe_Default_Clothing)
                        {
                            player.inventory.containerWear.Clear();
                            ItemManager.DoRemoves();
                        }

                        if (record.Wipe_Default_Weapons)
                        {
                            player.inventory.containerBelt.Clear();
                            ItemManager.DoRemoves();
                        }

                        GiveKit(player, record.Kits[kitRnd]);

                        if (player is global::HumanNPC)
                            (player as global::HumanNPC).EquipWeapon();
                        if (player is NPCPlayer)
                            ((NPCPlayer)player).EquipWeapon();
                    }
                }
            }
        }
        #endregion

        #region Config 
        private ConfigData configData;

        class ConfigData
        {
            public static Settings Settings = new Settings();
            public Dictionary<string, Settings> CorpseTypes = new Dictionary<string, Settings>
            {
                {"MilitaryTunnelScientist", Settings},
                {"JunkPileScientist", Settings},
                {"MountedScientist", Settings},
                {"CompoundScientist", Settings},
                {"BanditTown", Settings},
                {"ScareCrow", Settings},
                {"CargoShip", Settings},
                {"OilRig", Settings},
                {"Excavator", Settings},
                {"Scientist", Settings},
                {"HeavyScientist", Settings},
                {"TunnelDweller", Settings },
                {"Trainyard", Settings },
                {"UnderwaterDweller", Settings },
                {"Airfield", Settings },
                {"MissionProviderOutpost", Settings },
                {"MissionProviderFishing", Settings },
                {"MissionProviderStables", Settings },
                {"MissionProviderBandit", Settings },
                {"BoatShopkeeper", Settings },
                {"BanditShopkeeper", Settings },
                {"DesertScientist", Settings }
            };
        }

        public class Settings
        {
            [JsonProperty(Order = 1)] 
            public bool enabled = false;
            [JsonProperty(Order = 2)]
            public List<string> Kits = new List<string>(); 
            [JsonProperty(Order = 3)]
            public int Health = 100;
            [JsonProperty(Order = 4)]
            public int Weapon_Drop_Percent = 100;
            [JsonProperty(Order = 5)]
            public int Min_Weapon_Drop_Condition_Percent = 100;
            [JsonProperty(Order = 6)]
            public int Max_Weapon_Drop_Condition_Percent = 100;
            [JsonProperty(Order = 7)]
            public int Dropped_Weapon_Has_Ammo_Percent_Chance = 100;
            [JsonProperty(Order = 8)]
            public bool Wipe_Default_Clothing = true;
            [JsonProperty(Order = 9)]
            public bool Wipe_Default_Weapons = true;
            [JsonProperty(Order = 10)]
            public int Wipe_Main_Inventory_Percent = 100;
            [JsonProperty(Order = 11)]
            public int Wipe_Clothing_Percent = 100;
            [JsonProperty(Order = 12)]
            public int Wipe_Belt_Percent = 100;
            [JsonProperty(Order = 13)]
            public int Default_Rust_Loot_Percent = 100; 
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
            SaveConfig(configData);
            return true;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
        }

        void SaveConfig(ConfigData config) 
        {
            config.CorpseTypes = config.CorpseTypes.OrderBy(x => x.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
            Config.WriteObject(config, true);
        }
        #endregion     
    }
}
