using System.Collections.Generic;
using Oxide.Core.Configuration;
using Newtonsoft.Json;
using System;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Text.RegularExpressions;
using VLB;
using static Oxide.Plugins.MonumentPlusEx.MonumentPlusEx;
using Rust.Ai;
using System.Collections;

namespace Oxide.Plugins
{
    [Info("Monument Plus", "Ts3Hosting", "1.0.27", ResourceId = 2)]
    [Description("Add some Puzzles to Monuments")]
    public class MonumentPlus : RustPlugin
    {
		public static MonumentPlus _instance;
		[PluginReference]
		Plugin CopyPaste, ZoneManager;
		public string useAllow = "monumentplus.admin";
		npcEntity npcData;
        private DynamicConfigFile NPCDATA;
		npcEntity1 npcData1;
        private DynamicConfigFile NPCDATA1;
		private bool isPasting;
		public Dictionary<uint, string> vendOrders = new Dictionary<uint, string>();
		public Dictionary<IOEntity, DoorManipulator> newPuzzles = new Dictionary<IOEntity, DoorManipulator>();
		public Dictionary<uint, string> LootBoxesPos = new Dictionary<uint, string>();
		public Dictionary<IOEntity, IOEntity> fuseBoxs = new Dictionary<IOEntity, IOEntity>();
		public List<uint> puzzleEntitys = new List<uint>();
		public List<uint> buildingEntitys = new List<uint>();
		
		public List<uint> nonSwitchLights = new List<uint>();	
		public List<uint> lightEntitys = new List<uint>();
		
		public List<uint> WiredLightSupermarket = new List<uint>();
		public Dictionary<uint, uint> WiredSwitchSupermarket= new Dictionary<uint, uint>();
		public Dictionary<uint, uint> WiredFlashLightSupermarket= new Dictionary<uint, uint>();

		public List<uint> WiredLightGasStation = new List<uint>();
		public Dictionary<uint, uint> WiredSwitchGasStation = new Dictionary<uint, uint>();
		public Dictionary<uint, uint> WiredFlashLightGasStation = new Dictionary<uint, uint>();

        public List<uint> WiredLightMiningOutpost = new List<uint>();
		public Dictionary<uint, uint> WiredSwitchMiningOutpost = new Dictionary<uint, uint>();
		public Dictionary<uint, uint> WiredFlashLightMiningOutpost = new Dictionary<uint, uint>();

        public List<uint> WiredLightAirfield = new List<uint>();
		public Dictionary<uint, uint> WiredSwitchAirfield = new Dictionary<uint, uint>();
		public Dictionary<uint, uint> WiredFlashLightAirfield = new Dictionary<uint, uint>();
		
		private TimeSpan sTime;
		private TimeSpan eTime;
		private bool lightsOnOff;
		
		public class FindLightsNow
		{
			public List<BaseEntity> Lights = new List<BaseEntity>();
			public List<BaseEntity> Switches = new List<BaseEntity>();
		}
		
		
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPerm"] = "<color=#ce422b>You can not use this command</color>",
            }, this);
        }

        private void CheckDependencies()
        {
            if (CopyPaste == null)
			{
                PrintWarning($"CopyPaste could not be found! Disabling paste feature");

			}			
        }
		
		void Init()
        {
			_instance = this;
			RegisterPermissions();
            NPCDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/ShopKeeperLoot");
			NPCDATA1 = Interface.Oxide.DataFileSystem.GetFile(Name + "/ShopKeeper");
            LoadData();
        }
		
		private void RegisterPermissions()
        {
            permission.RegisterPermission(useAllow, this);
        }
		
        void LoadData()
        {
            try
            {
                npcData = Interface.Oxide.DataFileSystem.ReadObject<npcEntity>(Name + "/ShopKeeperLoot");
            }
            catch
            {
                PrintWarning("Couldn't load ShopKeeperLoot data, creating new ShopKeeperLoot file");
                npcData = new npcEntity();
            }
			try
            {
                npcData1 = Interface.Oxide.DataFileSystem.ReadObject<npcEntity1>(Name + "/ShopKeeper");
            }
            catch
            {
                PrintWarning("Couldn't load ShopKeeper data, creating new ShopKeeper file");
                npcData1 = new npcEntity1();
            }
			SaveData1();
        }

        class npcEntity
        {
            public Dictionary<string, NPCInfo> SellItemList = new Dictionary<string, NPCInfo>();
        }
		
		class npcEntity1
        {
            public Dictionary<string, NPCInfo1> ShopKeeper = new Dictionary<string, NPCInfo1>();
        }

		public class Order
        {
            public string _comment;
            public int sellId;
            public int sellAmount;
            public bool sellAsBP;
            public int currencyId;
            public int currencyAmount;
			public bool currencyAsBP;
			public ulong costskinID;
			public ulong sellskinID;
			public string sellItemName;			
        }
		
        class NPCInfo
        {
			public List<Order> itemsList = new List<Order>();
        }

		class NPCInfo1
        {
			public Vector3 location;
			public Vector3 rotation;
			public string lootFile;
			public uint currentID;
			public bool monumentSpawn;
			public string monumentName;
			public float MonumentRotation;
        }
		
        void SaveData()
        {
            NPCDATA.WriteObject(npcData);
        }
		
		void SaveData1()
        {
            NPCDATA1.WriteObject(npcData1);
        }

        #region Config 
	
        private ConfigData configData;
        class ConfigData
        {
			[JsonProperty(PropertyName = "Spawn Lights At")]
            public SettingsL settingsL { get; set; }

            [JsonProperty(PropertyName = "Spawn Puzzle At")]
            public Settings settings { get; set; }
			
			[JsonProperty(PropertyName = "Spawn BigWheel at")]
            public BigWheel bigWheel { get; set; }
			
			[JsonProperty(PropertyName = "Spawn Addons At")]
            public Lifts lifts { get; set; }

			[JsonProperty(PropertyName = "Spawn Building At")]
            public Buildingsz buildingsz { get; set; }
			
			public class SettingsL
            {
				public bool GasStation  { get; set; }
				public bool Supermarket  { get; set; }
				public bool MiningOutpost { get; set; }
				public bool Airfield { get; set; }
				public bool UseLightSwitch { get; set; }
				public string StartTime  { get; set; }
				public string EndTime  { get; set; }
			}
			
            public class Settings
            {
				public bool Junkyard  { get; set; }
				public bool Supermarket  { get; set; }
				public bool Dome  { get; set; }
			}			
			
			public class BigWheel
            {
				public bool Outpost  { get; set; }
			}
			
			public class Lifts
            {
				public bool SpawnAddons  { get; set; }
				public Dictionary<string, itemSpawning> itemAddon { get; set; }
			}
			
			public class Buildingsz
            {
				public bool SpawnBuildings { get; set; }
				public Dictionary<string, buildingInfo> building { get; set; }
			}
			
            public Oxide.Core.VersionNumber Version { get; set; }
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
				settingsL = new ConfigData.SettingsL
                {
					GasStation = true,
					Supermarket = true,
					MiningOutpost = false,
					Airfield = false,
					UseLightSwitch = false,
					StartTime = "20:00",
					EndTime = "08:00"
				},
				
                settings = new ConfigData.Settings
                {
					Junkyard = false,
					Supermarket = false,
					Dome = false
				},
				
				bigWheel = new ConfigData.BigWheel
                {
					Outpost = false
				},
				
				lifts = new ConfigData.Lifts
                {
					SpawnAddons = false,
					itemAddon = new Dictionary<string, itemSpawning>()

				},
				
				buildingsz = new ConfigData.Buildingsz
                {
					SpawnBuildings = false,
					building = new Dictionary<string, buildingInfo>()

				},
										
					Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(1, 0, 1))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion		

		class buildingInfo
		{
			public string monumentName;
			public string pasteFile;
			public Vector3 position;
			public float rotation;
			public List<string> options;
		}
		class vectorPos
        {
            public Vector3 pos;
			public string prefab;
			public float rot;
			public float moveRotate;
        }
		
		class itemSpawning
        {
			public string MonumentName;
			public string prefab;
            public Vector3 pos;
			public float rotate;
        }

		private void OnServerInitialized()
		{
			CheckDependencies();
			SpawnPuzzles(false);
			if (configData.lifts.itemAddon.Count() <= 0)
			{
				configData.lifts.itemAddon.Add("supermarket", new itemSpawning());
				configData.lifts.itemAddon["supermarket"].MonumentName = "supermarket";
				configData.lifts.itemAddon["supermarket"].prefab = "assets/bundled/prefabs/static/modularcarlift.static.prefab";
				configData.lifts.itemAddon["supermarket"].pos = new Vector3(0.2f, 0f, 17.5f);
				configData.lifts.itemAddon["supermarket"].rotate = 0.0f;
				
				configData.lifts.itemAddon.Add("gasstation", new itemSpawning());
				configData.lifts.itemAddon["gasstation"].MonumentName = "gas_station";
				configData.lifts.itemAddon["gasstation"].prefab = "assets/bundled/prefabs/static/modularcarlift.static.prefab";
				configData.lifts.itemAddon["gasstation"].pos = new Vector3(4.2f, 3.0f, -0.5f);
				configData.lifts.itemAddon["gasstation"].rotate = 0.0f;
				
				Config.WriteObject(configData, true);
			}
			
			if (configData.lifts.SpawnAddons)
				spawningAddons(false);
			
			if (!npcData.SellItemList.ContainsKey("default"))
			{
				npcData.SellItemList.Add("default", new NPCInfo());
				npcData.SellItemList["default"].itemsList = vendorItems;
				SaveData();
			}
			List<string> sendNpc = new List<string>();
			foreach (var key in npcData1.ShopKeeper.ToList())	
			{
				sendNpc.Add(key.Key);
			}
			if (sendNpc.Count > 0)
			timer.Once(5, () => 
			{		
				QueuedRoutine = ServerMgr.Instance.StartCoroutine(StartslowSpawn(sendNpc));			
			});
			sTime = TimeSpan.Parse(configData.settingsL.StartTime);
			eTime = TimeSpan.Parse(configData.settingsL.EndTime);
			if (configData.settingsL.GasStation || configData.settingsL.Supermarket || configData.settingsL.MiningOutpost || configData.settingsL.Airfield)
				timer.Every(15f, () => { CheckLightTime(); });

			if (configData.buildingsz == null) 
			{
				configData.buildingsz = new ConfigData.Buildingsz();
				configData.buildingsz.building = new Dictionary<string, buildingInfo>();
				configData.buildingsz.building.Add("Example1", new buildingInfo());
				configData.buildingsz.building["Example1"].monumentName = "fishing_village_b";
			    configData.buildingsz.building["Example1"].pasteFile = "T1B1";
			    configData.buildingsz.building["Example1"].position = new Vector3(1f, 1f, 1f);
			    configData.buildingsz.building["Example1"].rotation = 0.1f;
			    configData.buildingsz.building["Example1"].options = new List<string>{"stability", "true"};
				SaveConfig();
			}

			if (configData.buildingsz.SpawnBuildings && CopyPaste != null && configData.buildingsz.building.Count > 0)
			{
				
				List<string> BuildingName = new List<string>();
				
				foreach (var key in configData.buildingsz.building.ToList())
				{
					BuildingName.Add(key.Key);
				}
				
				PasteRoutine = ServerMgr.Instance.StartCoroutine(SpawnBuilding(BuildingName));	
			}
		}
		
		private Coroutine QueuedRoutine { get; set; }
		private IEnumerator StartslowSpawn(List<string> sendNpc)
        {
			if (sendNpc == null)
				if (_instance.QueuedRoutine != null)
				{
                    ServerMgr.Instance.StopCoroutine(_instance.QueuedRoutine);
				    _instance.QueuedRoutine = null;
				}

            while (sendNpc.Count > 0)
            {
				if (npcData1.ShopKeeper.ContainsKey(sendNpc[0]))
				{
					if (npcData1.ShopKeeper[sendNpc[0]].monumentSpawn)
					{
						string name = "";
						int count = -1;
						foreach ( var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
						{
							if (GetMonumentName(monument) == null) continue;
							if (npcData1.ShopKeeper[sendNpc[0]].monumentName == GetMonumentName(monument))
							{
								count++;
								if (count == 0) name = sendNpc[0];
								else
								{
								   name = sendNpc[0] + "_" + count;
								}
								Vector3 loc = monument.transform.TransformPoint( npcData1.ShopKeeper[sendNpc[0]].location );								
								float rotation = (monument.transform.rotation.eulerAngles * Mathf.Deg2Rad).y - 2.17f + npcData1.ShopKeeper[sendNpc[0]].MonumentRotation;
								SpawnNpcVendors(name, loc, monument.transform.localEulerAngles, npcData1.ShopKeeper[sendNpc[0]].lootFile, npcData1.ShopKeeper[sendNpc[0]].MonumentRotation + 100f);	
								yield return CoroutineEx.waitForSeconds(2f);
							} //monument.transform.rotation.eulerAngles
						}
					}
					else SpawnNpcVendors(sendNpc[0], npcData1.ShopKeeper[sendNpc[0]].location, npcData1.ShopKeeper[sendNpc[0]].rotation, npcData1.ShopKeeper[sendNpc[0]].lootFile);			
				}
				sendNpc.RemoveAt(0);
                yield return CoroutineEx.waitForSeconds(5f);
            }
				if (_instance.QueuedRoutine != null)
				{
                    ServerMgr.Instance.StopCoroutine(_instance.QueuedRoutine);
					_instance.QueuedRoutine = null;				
                }
        }
		
		void Unload()
        {
			if (QueuedRoutine != null)
			{
                ServerMgr.Instance.StopCoroutine(QueuedRoutine);				
            }
			if (PasteRoutine != null)
			{
                ServerMgr.Instance.StopCoroutine(PasteRoutine);				
            }
				
            foreach (uint entity in puzzleEntitys.ToList())
            {
				var networkable = BaseNetworkable.serverEntities.Find(entity);
				if (networkable != null) networkable?.Kill();
            }
			foreach (var entity in LootBoxesPos.ToList())
            {
				var networkable = BaseNetworkable.serverEntities.Find(entity.Key);
				if (networkable != null) networkable?.Kill();
            }
			foreach (uint entityL in lightEntitys.ToList())
            {
				var networkable = BaseNetworkable.serverEntities.Find(entityL);
				if (networkable != null) networkable?.Kill();
            }
			foreach (uint entityL in buildingEntitys.ToList())
            {
				var networkable = BaseNetworkable.serverEntities.Find(entityL);
				if (networkable != null) networkable?.Kill();
            }
			
			SpawnPuzzles(true);			
        }

		void OnSwitchToggle(ElectricSwitch entity, BasePlayer player)
		{	
			if (!lightEntitys.Contains(entity.net.ID)) return;
			
			bool setOn = false;
			if (!entity.IsOn()) setOn = true;
			
			if (WiredSwitchGasStation.ContainsKey(entity.net.ID))
			{
				foreach (var lightEntity in WiredFlashLightGasStation.ToList())
				{
					ProjectileWeaponMod networkable = BaseNetworkable.serverEntities.Find(lightEntity.Key) as ProjectileWeaponMod;
					if (networkable == null) continue;
					else networkable.SetFlag(BaseEntity.Flags.On, setOn);
				}
				foreach (var lightswitch in WiredSwitchGasStation.ToList())
				{
				    ElectricSwitch networkable = BaseNetworkable.serverEntities.Find(lightswitch.Key) as ElectricSwitch;
					if (networkable == null || networkable == entity) continue;
					else networkable.SetSwitch(setOn);	
				}
			}
					
			else if (WiredSwitchSupermarket.ContainsKey(entity.net.ID))
			{
				foreach (var lightEntity in WiredFlashLightSupermarket.ToList())
				{
					ProjectileWeaponMod networkable = BaseNetworkable.serverEntities.Find(lightEntity.Key) as ProjectileWeaponMod;
					if (networkable == null) continue;
					else networkable.SetFlag(BaseEntity.Flags.On, setOn);
				}
				foreach (var lightswitch in WiredSwitchSupermarket.ToList())
				{
				    ElectricSwitch networkable = BaseNetworkable.serverEntities.Find(lightswitch.Key) as ElectricSwitch;
					if (networkable == null || networkable == entity) continue;
					else networkable.SetSwitch(setOn);	
				}
			}
			else if (WiredSwitchMiningOutpost.ContainsKey(entity.net.ID))
			{
				foreach (var lightEntity in WiredFlashLightMiningOutpost.ToList())
				{
					ProjectileWeaponMod networkable = BaseNetworkable.serverEntities.Find(lightEntity.Key) as ProjectileWeaponMod;
					if (networkable == null) continue;
					else networkable.SetFlag(BaseEntity.Flags.On, setOn);
				}
				foreach (var lightswitch in WiredSwitchMiningOutpost.ToList())
				{
				    ElectricSwitch networkable = BaseNetworkable.serverEntities.Find(lightswitch.Key) as ElectricSwitch;
					if (networkable == null || networkable == entity) continue;
					else networkable.SetSwitch(setOn);	
				}
			}
			else if (WiredSwitchAirfield.ContainsKey(entity.net.ID))
			{
				foreach (var lightEntity in WiredFlashLightAirfield.ToList())
				{
					ProjectileWeaponMod networkable = BaseNetworkable.serverEntities.Find(lightEntity.Key) as ProjectileWeaponMod;
					if (networkable == null) continue;
					else networkable.SetFlag(BaseEntity.Flags.On, setOn);
				}
				foreach (var lightswitch in WiredSwitchAirfield.ToList())
				{
				    ElectricSwitch networkable = BaseNetworkable.serverEntities.Find(lightswitch.Key) as ElectricSwitch;
					if (networkable == null || networkable == entity) continue;
					else networkable.SetSwitch(setOn);	
				}
			}
		}

		public static bool IsBetween(DateTime now, TimeSpan start, TimeSpan end)
		{
			var time = now.TimeOfDay;
			// If the start time and the end time is in the same day.
			if (start <= end)
				return time >= start && time <= end;
			// The start time and end time is on different days.
			return time >= start || time <= end;
		}
		
		private void CheckLightTime()
		{
			if (IsBetween(TOD_Sky.Instance.Cycle.DateTime, sTime, eTime))
			{
				if (lightsOnOff) return;
				lightsOnOff = true;
				toggleLights(true);
			}
			else if (lightsOnOff)
			{
				lightsOnOff = false;
				toggleLights(false);
			}
		}
		
		private void toggleLights(bool toggle = false)
		{
			int power = 0;
			if (toggle) power = 250;
			foreach (uint lightEntity in lightEntitys.ToList())
			{
				BaseOven networkable = BaseNetworkable.serverEntities.Find(lightEntity) as BaseOven;
				ProjectileWeaponMod networkable1 = BaseNetworkable.serverEntities.Find(lightEntity) as ProjectileWeaponMod;
				if (networkable != null) networkable.SetFlag(BaseEntity.Flags.On, toggle);
				if (networkable1 != null && !configData.settingsL.UseLightSwitch) networkable1.SetFlag(BaseEntity.Flags.On, toggle);
				if (nonSwitchLights.Contains(lightEntity))
				{
					IOEntity networkable2 = BaseNetworkable.serverEntities.Find(lightEntity) as IOEntity;
					if (networkable2 != null) networkable2.UpdateFromInput(power, 0);
				}
			}
			if (!configData.settingsL.UseLightSwitch)
			{
				if (WiredLightGasStation.Count > 0)
				{
					foreach (uint light in WiredLightGasStation.ToList())
					{
						IOEntity networkable = BaseNetworkable.serverEntities.Find(light) as IOEntity;
						if (networkable != null)
						{
							networkable.UpdateFromInput(power, 0);
						}
					}
				}
				if (WiredLightSupermarket.Count > 0)
				{
					foreach (uint light in WiredLightSupermarket.ToList())
					{
						IOEntity networkable = BaseNetworkable.serverEntities.Find(light) as IOEntity;
						if (networkable != null)
						{
							networkable.UpdateFromInput(power, 0);
						}
					}
				}
				if (WiredLightMiningOutpost.Count > 0)
				{
					foreach (uint light in WiredLightMiningOutpost.ToList())
					{
						IOEntity networkable = BaseNetworkable.serverEntities.Find(light) as IOEntity;
						if (networkable != null)
						{
							networkable.UpdateFromInput(power, 0);
						}
					}
				}
				if (WiredLightAirfield.Count > 0)
				{
					foreach (uint light in WiredLightAirfield.ToList())
					{
						IOEntity networkable = BaseNetworkable.serverEntities.Find(light) as IOEntity;
						if (networkable != null)
						{
							networkable.UpdateFromInput(power, 0);
						}
					}
				}
			}
		}
		
		private void RemoveGroundWatch(BaseEntity entity)
        {
			if (entity == null) return;
			if (entity.GetComponent<GroundWatch>() != null)
				UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());
			if (entity.GetComponent<DestroyOnGroundMissing>() != null)
				UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
        }
		
		private void spawningAddons(bool unLoading)
		{
			if (unLoading) return;
			foreach (var key in configData.lifts.itemAddon.ToList())
			{
				
				foreach (var monument in TerrainMeta.Path.Monuments)
				{
					Vector3 itemsVector = Vector3.zero;
					if (monument == null) continue;
					if (!monument.name.ToLower().Contains(key.Value.MonumentName.ToLower())) continue;
					itemsVector = monument.transform.TransformPoint(key.Value.pos);
					if (itemsVector == null || itemsVector == Vector3.zero) continue;
					
					var itemsEntity = GameManager.server.CreateEntity(key.Value.prefab, itemsVector, Quaternion.Euler(monument.transform.localEulerAngles.x, monument.transform.localEulerAngles.y + key.Value.rotate, monument.transform.localEulerAngles.z));
					
					if (itemsEntity != null)
					{
						itemsEntity.enableSaving = false;
						RemoveGroundWatch(itemsEntity);
						itemsEntity.Spawn();
						RemoveGroundWatch(itemsEntity);
						puzzleEntitys.Add(itemsEntity.net.ID);
					}						
				}
			}
		}
		
		#region GasstationLocations

		private static List<Order> vendorItems
        {
            get
            {
                return new List<Order>
                {
					new Order { _comment = "Sell High Quality Pistons x 1 for Scrap x 75", sellId = 1883981800, sellAmount = 75, sellAsBP = false, currencyId = -932201673, currencyAmount = 1, currencyAsBP = false, sellskinID = 0, costskinID = 0 },
					new Order { _comment = "Sell High Quality Crankshaft x 1 for Scrap x 120", sellId = 1158340332, sellAmount = 120, sellAsBP = false, currencyId = -932201673, currencyAmount = 1, currencyAsBP = false, sellskinID = 0, costskinID = 0 },
					new Order { _comment = "Sell High Quality Valves x 1 for Scrap x 75", sellId = -1802083073, sellAmount = 75, sellAsBP = false, currencyId = -932201673, currencyAmount = 1, currencyAsBP = false, sellskinID = 0, costskinID = 0 },
					new Order { _comment = "Sell High Quality Spark Plugs x 1 for Scrap x 75", sellId = 1072924620, sellAmount = 75, sellAsBP = false, currencyId = -932201673, currencyAmount = 1, currencyAsBP = false, sellskinID = 0, costskinID = 0 },
					new Order { _comment = "Sell High Quality Carburetor x 1 for Scrap x 120", sellId = 656371026, sellAmount = 120, sellAsBP = false, currencyId = -932201673, currencyAmount = 1, currencyAsBP = false, sellskinID = 0, costskinID = 0 },
				};
            }
        }
		
		private static List<vectorPos> placemntGasStation
        {
            get
            {
                return new List<vectorPos>
                {
                    new vectorPos { pos = new Vector3(1.5f, 7.5f, 4.0f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f},
                    new vectorPos { pos = new Vector3(7.5f, 7.5f, -5.0f), rot = 0f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f },
                    new vectorPos { pos = new Vector3(13.5f, 7.5f, 19.5f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(1.8f, 7.8f, 21.55f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },				
					new vectorPos { pos = new Vector3(1.8f, 7.8f, 21.55f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(-1.2f, 7.8f, 21.55f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(-4.2f, 7.8f, 21.55f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(-7.2f, 7.8f, 21.55f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(4.94f, 7.6f, 29.28f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(-1.58f, 7.6f, 28.92f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(-6.3f, 7.6f, 28.92f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(-9.95f, 7.6f, 28.92f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(1.74f, 6.6f, 21.23f), rot = 0f, prefab = "assets/prefabs/weapon mods/flashlight/flashlight.entity.prefab", moveRotate = 85f },
					new vectorPos { pos = new Vector3(-1.26f, 6.6f, 21.23f), rot = 0f, prefab = "assets/prefabs/weapon mods/flashlight/flashlight.entity.prefab", moveRotate = 85f },
					new vectorPos { pos = new Vector3(-4.26f, 6.6f, 21.23f), rot = 0f, prefab = "assets/prefabs/weapon mods/flashlight/flashlight.entity.prefab", moveRotate = 85f },
					new vectorPos { pos = new Vector3(-7.26f, 6.6f, 21.23f), rot = 0f, prefab = "assets/prefabs/weapon mods/flashlight/flashlight.entity.prefab", moveRotate = 85f },
					new vectorPos { pos = new Vector3(4.86f, 6.2f, 28.98f), rot = 0f, prefab = "assets/prefabs/weapon mods/flashlight/flashlight.entity.prefab", moveRotate = 85f },
					new vectorPos { pos = new Vector3(-1.65f, 6.45f, 28.61f), rot = 0f, prefab = "assets/prefabs/weapon mods/flashlight/flashlight.entity.prefab", moveRotate = 85f },
					new vectorPos { pos = new Vector3(-6.40f, 6.45f, 28.61f), rot = 0f, prefab = "assets/prefabs/weapon mods/flashlight/flashlight.entity.prefab", moveRotate = 85f },
					new vectorPos { pos = new Vector3(-10.01f, 6.45f, 28.61f), rot = 0f, prefab = "assets/prefabs/weapon mods/flashlight/flashlight.entity.prefab", moveRotate = 85f },
				    new vectorPos { pos = new Vector3(8.745f, 3.75f, 27.95f), rot = -90f, prefab = "assets/prefabs/io/electric/switches/simpleswitch/simpleswitch.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-2.145f, 3.75f, 31.25f), rot = 180f, prefab = "assets/prefabs/io/electric/switches/simpleswitch/simpleswitch.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(0.145f, 3.75f, 25.88f), rot = 180f, prefab = "assets/prefabs/io/electric/switches/simpleswitch/simpleswitch.prefab", moveRotate = 0f }
                };
            }
        }
		
		#endregion
		
		#region SupermarketLocations
		
        private static List<vectorPos> SupermarketDoor
        {
            get
            {
                return new List<vectorPos>
                {
					new vectorPos { pos = new Vector3(1.5f, -0.05f, 5.40f), rot = 90f, prefab = "assets/bundled/prefabs/static/door.hinged.security.green.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(2.25f, 0.9f, 5.23f), rot = 270f, prefab = "assets/prefabs/io/electric/switches/doormanipulator.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(2.38f, 0.0f, 5.25f), rot = 180f, prefab = "assets/prefabs/io/electric/switches/cardreader.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(2.38f, 0.0f, 5.50f), rot = 0f, prefab = "assets/prefabs/io/electric/switches/pressbutton/pressbutton.prefab", moveRotate = 0f }
				};
            }
        }
		
		private static List<vectorPos> SupermarketDoorTwo
        {
            get
            {
                return new List<vectorPos>
                {
					new vectorPos { pos = new Vector3(7.9f, -0.05f, 9.005f), rot = 180f, prefab = "assets/bundled/prefabs/static/door.hinged.security.green.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(7.75f, 0.9f, 8.23f), rot = 0f, prefab = "assets/prefabs/io/electric/switches/doormanipulator.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(8.00f, 0.0f, 8.10f), rot = 90f, prefab = "assets/prefabs/io/electric/switches/cardreader.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(7.743f, 0.0f, 8.10f), rot = 270f, prefab = "assets/prefabs/io/electric/switches/pressbutton/pressbutton.prefab", moveRotate = 0f }
				};
            }
        }
		
		private static List<vectorPos> SupermarketBoxLoc
        {
            get
            {
                return new List<vectorPos>
                {
					new vectorPos { pos = new Vector3(7.1f, -0.05f, 7.205f), rot = 0f, prefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(4.9f, -0.05f, 7.005f), rot = 0f, prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab", moveRotate = 0f }
				};
            }
        }
		
	    private static List<vectorPos> SupermarketDoorReplace
        {
            get
            {
                return new List<vectorPos>
                {
					new vectorPos { pos = new Vector3(1.5f, -0.05f, 5.40f), rot = 90f, prefab = "assets/bundled/prefabs/static/door.hinged.industrial_a_b.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(7.9f, -0.05f, 9.005f), rot = 180f, prefab = "assets/bundled/prefabs/static/door.hinged.industrial_a_b.prefab", moveRotate = 0f }
				};
            }
        }
		
		private static List<vectorPos> placemntSupermarket
        {
            get
            {
                return new List<vectorPos>
                {
                    new vectorPos { pos = new Vector3(-1.68f, 4.4f, 1.25f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(4.14f, 4.4f, 1.25f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(9.94f, 4.4f, 1.25f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
                    new vectorPos { pos = new Vector3(-1.68f, 4.4f, -4.55f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(4.14f, 4.4f, -4.55f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(9.94f, 4.4f, -4.55f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(-3.57f, 4.26f, 7.35f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(6.15f, 4.26f, 7.35f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(-5.85f, 0.0f, 6.25f), rot = 180f, prefab = "assets/bundled/prefabs/static/hobobarrel_static.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-2.85f, 0.0f, -12.25f), rot = 180f, prefab = "assets/bundled/prefabs/static/hobobarrel_static.prefab", moveRotate = 0f },
                    new vectorPos { pos = new Vector3(0.55f, 0.5f, 5.25f), rot = 180f, prefab = "assets/prefabs/io/electric/switches/simpleswitch/simpleswitch.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(7.75f, 0.6f, 8.10f), rot = -90f, prefab = "assets/prefabs/io/electric/switches/simpleswitch/simpleswitch.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-4.99f, 0.5f, 8.41f), rot = 90f, prefab = "assets/prefabs/io/electric/switches/simpleswitch/simpleswitch.prefab", moveRotate = 0f }

				};
            }
        }

		
		#endregion
		
		#region DomeLocations
		
        private static List<vectorPos> DomeDoor
        {
            get
            {
                return new List<vectorPos>
                {
					new vectorPos { pos = new Vector3(-3.1873f, 0.2397f, 9.2959f), rot = 131f, prefab = "assets/bundled/prefabs/static/door.hinged.security.green.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-2.4640f, 1.2232f, 8.6518f), rot = 125f, prefab = "assets/prefabs/io/electric/switches/doormanipulator.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-2.4640f, 0.1532f, 8.6518f), rot = 220f, prefab = "assets/prefabs/io/electric/switches/cardreader.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-2.3640f, 0.1532f, 8.6518f), rot = 42f, prefab = "assets/prefabs/io/electric/switches/pressbutton/pressbutton.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-26.384f, 43.2403f, -9.4194f), rot = -108f, prefab = "assets/prefabs/io/electric/switches/fusebox/fusebox.prefab", moveRotate = -6f }
				};
            }
        }
		
		private static List<vectorPos> DomeDoorReplace
        {
            get
            {
                return new List<vectorPos>
                {
					new vectorPos { pos = new Vector3(-3.1873f, 0.2397f, 9.2959f), rot = 131f, prefab = "assets/bundled/prefabs/static/door.hinged.industrial_a_f.prefab", moveRotate = 0f }
				};
            }
        }
		
		private static List<vectorPos> DomeBoxLoc
        {
            get
            {
                return new List<vectorPos>
                {
					new vectorPos { pos = new Vector3(-1.0047f, 0.2227f, 10.5113f), rot = 0f, prefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-1.8745f, 0.2167f, 11.9956f), rot = 0f, prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab", moveRotate = 0f }
				};
            }
        }
		
		#endregion

		#region JunkyardLocations
		
        private static List<vectorPos> JunkyardDoor
        {
            get
            {
                return new List<vectorPos>
                {
					new vectorPos { pos = new Vector3(-42.7512f, 11.5245f, 4.9712f), rot = 56.5f, prefab = "assets/bundled/prefabs/static/door.hinged.security.green.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-42.0775f, 12.5891f, 5.4739f), rot = 56.5f, prefab = "assets/prefabs/io/electric/switches/doormanipulator.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-41.9275f, 11.2891f, 5.4739f), rot = 145.5f, prefab = "assets/prefabs/io/electric/switches/cardreader.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-42.0775f, 11.2891f, 5.4739f), rot = -35.5f, prefab = "assets/prefabs/io/electric/switches/pressbutton/pressbutton.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-26.0719f, 16.4779f, -29.6761f), rot = 60.5f, prefab = "assets/prefabs/io/electric/switches/fusebox/fusebox.prefab", moveRotate = 0f }
				};
            }
        }
		
		
		private static List<vectorPos> JunkyardBoxLoc
        {
            get
            {
                return new List<vectorPos>
                {
					new vectorPos { pos = new Vector3(-42.1944f, 11.5124f, 6.2973f), rot = 55f, prefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-43.5566f, 11.5124f, 5.5696f), rot = -35f, prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab", moveRotate = 0f }
				};
            }
        }
		
		#endregion
		
		#region OutpostLocations
		
        private static List<vectorPos> OutpostStuff
        {
            get
            {
                return new List<vectorPos>
                {
					new vectorPos { pos = new Vector3(-28.89f, 3.28f, 31.615f), rot = 0f, prefab = "assets/prefabs/misc/casino/bigwheel/big_wheel.prefab", moveRotate = 90f },
					new vectorPos { pos = new Vector3(-26.59f, 0.28f, 36.215f), rot = 210f, prefab = "assets/bundled/prefabs/static/chair.static.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-26.09f, 0.28f, 35.815f), rot = 295f, prefab = "assets/prefabs/misc/casino/bigwheel/bigwheelbettingterminal.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-28.09f, 0.28f, 36.615f), rot = 180f, prefab = "assets/bundled/prefabs/static/chair.static.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-27.49f, 0.28f, 36.415f), rot = 270f, prefab = "assets/prefabs/misc/casino/bigwheel/bigwheelbettingterminal.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-29.59f, 0.28f, 36.615f), rot = 170f, prefab = "assets/bundled/prefabs/static/chair.static.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-28.99f, 0.28f, 36.465f), rot = 260f, prefab = "assets/prefabs/misc/casino/bigwheel/bigwheelbettingterminal.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-31.09f, 0.28f, 36.215f), rot = 150f, prefab = "assets/bundled/prefabs/static/chair.static.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-30.49f, 0.28f, 36.25f), rot = 240f, prefab = "assets/prefabs/misc/casino/bigwheel/bigwheelbettingterminal.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-29.19f, 0.22f, 33.515f), rot = 180f, prefab = "assets/bundled/prefabs/static/campfire_static.prefab", moveRotate = 0f }
				};
            }
        }
		#endregion

		#region MiningOutpostLocations

		private static List<vectorPos> placemntMiningOutpost
        {
            get
            {
                return new List<vectorPos>
                {
					new vectorPos { pos = new Vector3(12.3301f, 5.4395f, -7.8474f), rot = 0f, prefab = "assets/prefabs/weapon mods/flashlight/flashlight.entity.prefab", moveRotate = 85f },
					new vectorPos { pos = new Vector3(6.4050f, 5.4395f, -7.8392f), rot = 0f, prefab = "assets/prefabs/weapon mods/flashlight/flashlight.entity.prefab", moveRotate = 85f },
                    new vectorPos { pos = new Vector3(0.4906f, 5.4395f, -7.8620f), rot = 0f, prefab = "assets/prefabs/weapon mods/flashlight/flashlight.entity.prefab", moveRotate = 85f },
					new vectorPos { pos = new Vector3(-5.4604f, 5.4395f, -7.8590f), rot = 0f, prefab = "assets/prefabs/weapon mods/flashlight/flashlight.entity.prefab", moveRotate = 85f },
					new vectorPos { pos = new Vector3(-11.3301f, 5.4395f, -7.8793f), rot = 0f, prefab = "assets/prefabs/weapon mods/flashlight/flashlight.entity.prefab", moveRotate = 85f },
					new vectorPos { pos = new Vector3(-3.5541f, 3.4072f, -0.6241f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-3.8208f, 3.4072f, -15.1696f), rot = 0f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(2.7459f, 3.4072f, -15.1696f), rot = 0f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(9.7896f, 3.4072f, -15.1696f), rot = 0f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(3.0029f, 3.4072f, -0.6241f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(10.4103f, 3.4072f, -0.6241f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f },
                    new vectorPos { pos = new Vector3(-10.5113f, 0.5728f, -15.3765f), rot = 0f, prefab = "assets/prefabs/io/electric/switches/simpleswitch/simpleswitch.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(11.5830f, 0.5728f, -0.3741f), rot = 180f, prefab = "assets/prefabs/io/electric/switches/simpleswitch/simpleswitch.prefab", moveRotate = 0f }

				};
            }
        }
		
		#endregion
		
		#region AirfieldLocations
        //assets/bundled/prefabs/autospawn/monument/large/airfield_1.prefab
		
		private static List<vectorPos> placemntAirfieldHangers
        {
            get
            {
                return new List<vectorPos>
                {
					//Hanger1
					new vectorPos { pos = new Vector3(-32.85f, 7.0456f, 26.069f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(-32.85f, 7.0456f, 38.0699f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(-32.85f, 7.0456f, 50.0540f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(-26.9909f, 7.0456f, 26.069f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(-26.9909f, 7.0456f, 38.0699f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(-26.9909f, 7.0456f, 50.0540f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					//Hanger2
					new vectorPos { pos = new Vector3(6.1511f, 7.0456f, 26.069f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(6.1511f, 7.0456f, 38.0699f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(6.1511f, 7.0456f, 50.0540f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(12.0256f, 7.0456f, 26.069f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(12.0256f, 7.0456f, 38.0699f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(12.0256f, 7.0456f, 50.0540f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					//Hanger3
					new vectorPos { pos = new Vector3(46.5043f, 7.0456f, 26.069f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(46.5043f, 7.0456f, 38.0699f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(46.5043f, 7.0456f, 50.0540f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(52.4056f, 7.0456f, 26.069f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(52.4056f, 7.0456f, 38.0699f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					new vectorPos { pos = new Vector3(52.4056f, 7.0456f, 50.0540f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 60f },
					//Recycler
					new vectorPos { pos = new Vector3(-4.3921f, 7.5715f, -104.7120f), rot = 0f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-9.5517f, 7.5715f, -104.7120f), rot = 0f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-15.4176f, 7.5715f, -104.7120f), rot = 0f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f },

					new vectorPos { pos = new Vector3(-4.3921f, 7.5715f, -92.8641f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-9.5517f, 7.5715f, -92.8641f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-15.4176f, 7.5715f, -92.8641f), rot = 180f, prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab", moveRotate = 0f }


				};
            }
        }

		private static List<vectorPos> placemntAirfieldBuilding
        {
            get
            {
                return new List<vectorPos>
                {
					//Main Building
					new vectorPos { pos = new Vector3(-9.3545f, 6.2629f, -83.8042f), rot = 0f, prefab = "assets/prefabs/deployable/ceiling light/ceilinglight.deployed.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-9.3545f, 6.2629f, -89.7927f), rot = 0f, prefab = "assets/prefabs/deployable/ceiling light/ceilinglight.deployed.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-16.0038f, 6.2629f, -91.30f), rot = 0f, prefab = "assets/prefabs/deployable/ceiling light/ceilinglight.deployed.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-22.02f, 6.2629f, -91.30f), rot = 0f, prefab = "assets/prefabs/deployable/ceiling light/ceilinglight.deployed.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-29.0264f, 6.2629f, -89.7991f), rot = 0f, prefab = "assets/prefabs/deployable/ceiling light/ceilinglight.deployed.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-4.1210f, 6.2629f, -85.3235f), rot = 0f, prefab = "assets/prefabs/deployable/ceiling light/ceilinglight.deployed.prefab", moveRotate = 0f },
					//floor 2
					new vectorPos { pos = new Vector3(-28.0137f, 9.1888f, -83.7479f), rot = 0f, prefab = "assets/prefabs/deployable/ceiling light/ceilinglight.deployed.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-28.0137f, 9.1888f, -91.2838f), rot = 0f, prefab = "assets/prefabs/deployable/ceiling light/ceilinglight.deployed.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-15.8247f, 9.1888f, -91.2838f), rot = 0f, prefab = "assets/prefabs/deployable/ceiling light/ceilinglight.deployed.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-13.1794f, 9.1888f, -83.8369f), rot = 0f, prefab = "assets/prefabs/deployable/ceiling light/ceilinglight.deployed.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-10.0362f, 9.1888f, -89.2494f), rot = 0f, prefab = "assets/prefabs/deployable/ceiling light/ceilinglight.deployed.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-3.9973f, 9.1888f, -85.3235f), rot = 0f, prefab = "assets/prefabs/deployable/ceiling light/ceilinglight.deployed.prefab", moveRotate = 0f },
					//switches
                    new vectorPos { pos = new Vector3(-10.0517f, 3.7508f, -80.93f), rot = 180f, prefab = "assets/prefabs/io/electric/switches/simpleswitch/simpleswitch.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-25.0047f, 3.7508f, -92.668f), rot = 0f, prefab = "assets/prefabs/io/electric/switches/simpleswitch/simpleswitch.prefab", moveRotate = 0f },
					new vectorPos { pos = new Vector3(-10.2042f, 3.7508f, -92.668f), rot = 0f, prefab = "assets/prefabs/io/electric/switches/simpleswitch/simpleswitch.prefab", moveRotate = 0f }

				};
            }
        }
		
		
		#endregion
		
		[ChatCommand("npc_loot")]
        private void npcAddLoot(BasePlayer player, string command, string[] args)
        {
			if (!permission.UserHasPermission(player.UserIDString, useAllow))
            {
                SendReply(player, lang.GetMessage("NoPerm", this));
                return;
            }
			string colorCode = "#FFFF00";
			switch (args[0].ToLower())
            {
                case "add":
					if (args.Length < 2)
					{
						SendReply(player, "Usage:\n\n" + $"<color={colorCode}>/npc_loot add <LootFile></color> - Adds loot file.\n" + $"<color={colorCode}>/npc_loot remove <LootFile> </color> - Remove loot file.\n");
						return;
					}
					editLootTable(player, args[1].ToLower());
                    break;
					case "remove":
					if (args.Length < 2)
					{
						SendReply(player, "Usage:\n\n" + $"<color={colorCode}>/npc_loot add <LootFile></color> - Adds loot file.\n" + $"<color={colorCode}>/npc_loot remove <LootFile> </color> - Remove loot file.\n");
						return;
					}
					clearTheLootTable(player, args[1].ToLower());
                    break;
                default:
					SendReply(player, "Usage:\n\n" + $"<color={colorCode}>/npc_loot add <LootFile></color> - Adds loot file.\n" + $"<color={colorCode}>/npc_loot remove <LootFile> </color> - Remove loot file.\n");
                    break;
            }
		}

		private void clearTheLootTable(BasePlayer player, string theName)
		{
			if (npcData.SellItemList.ContainsKey(theName))
			{
				npcData.SellItemList.Remove(theName);
				SaveData();
				SendReply(player, "The LootTable named " + theName + " was removed.");
			}
			else
			{
				SendReply(player, "No LootTable named " + theName);
			}
		}
		
		private void editLootTable(BasePlayer player, string theName, string newLootFile = "")
		{				
			int placement = 0;
			int placement1 = 1;
			List<Order> vendorItems = new List<Order>();
			List<int> blocks = new List<int>() {1,3,5,7,9,11,13,15,17,18,19,20,21,22,23,24,25,26};
			Dictionary<int, Item> orgnize = new Dictionary<int, Item>();
			foreach (Item item in player.inventory.containerMain.itemList.ToList())
			{	
				orgnize.Add(item.position, item);
			}
			foreach (var item in orgnize.ToList())
			{
				if (orgnize.ContainsKey(placement) && orgnize.ContainsKey(placement1))
				{
					Order theAdd = new Order { _comment = orgnize[placement].info.shortname, sellItemName = orgnize[placement].name, sellId = orgnize[placement].info.itemid, sellAmount = orgnize[placement1].amount, sellAsBP = false, currencyId = orgnize[placement1].info.itemid, currencyAmount = orgnize[placement].amount, currencyAsBP = false, sellskinID = orgnize[placement].skin, costskinID = orgnize[placement1].skin };
					vendorItems.Add(theAdd);
				}
				placement = placement + 2;
				placement1 = placement1 + 2;
			}
			
			if (npcData.SellItemList.ContainsKey(theName))
			{
				npcData.SellItemList[theName].itemsList.Clear();
				SendReply(player, "The LootTable named " + theName + " existed and was cleared and new items added.");
			}
			else
			{
				npcData.SellItemList.Add(theName, new NPCInfo());
			}

				npcData.SellItemList[theName].itemsList = vendorItems;
				SaveData();
				SendReply(player, "Loot table added " + theName);
		}
		
		[ChatCommand("npc_vendor")]
        private void cmdChatNPCAddVendor(BasePlayer player, string command, string[] args)
        {
			string colorCode = "#FFFF00";
            if (!permission.UserHasPermission(player.UserIDString, useAllow))
            {
                SendReply(player, lang.GetMessage("NoPerm", this));
                return;
            }
            if (args.Length == 0)
            {
                SendReply(player, "Usage:\n\n" + $"<color={colorCode}>/npc_vendor add <Name> <LootFile></color> - Adds vendor to location.\n" + $"<color={colorCode}>/npc_vendor edit <Name> <LootFile> </color> - Change vendor loot file.\n" + $"<color={colorCode}>/npc_vendor remove <Name></color> - Removes a vendor\n");
                return;
            }

            switch (args[0].ToLower())
            {
                case "add":
					if (args.Length < 4)
					{
						SendReply(player, "Usage:\n\n" + $"<color={colorCode}>/npc_vendor add <Name> <LootFile> <AtMonument true/false></color> - Adds vendor to location.\n" + $"<color={colorCode}>/npc_vendor edit <Name> <LootFile> </color> - Change vendor loot file.\n" + $"<color={colorCode}>/npc_vendor remove <Name></color> - Removes a vendor\n");
						return;
					}
					if (!npcData.SellItemList.ContainsKey(args[2].ToLower()))
					{
						SendReply(player, "There no LootTable named " + args[2].ToLower() + ". You can use default if you have no tables.");
						return;
					}
					Quaternion currentRot;
					if(!TryGetPlayerView(player, out currentRot))
					{
						SendReply(player, "Couldn't get player rotation");
						return;
					}
					Vector3 localPos = Vector3.zero;
					string name = "Unknown";
					float rotation = 0;
					Vector3 rotNEW = Vector3.zero;
					MonumentInfo closest = null;
					if (args[3].ToLower() == "true")
					{
						float lowestDist = float.MaxValue;
	
						foreach ( var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
						{
							if (GetMonumentName(monument) == null || GetMonumentName(monument).Contains("substation")) continue;
							float dist = Vector3.Distance( player.transform.position, monument.transform.position );
							if ( dist < lowestDist )
							{
								lowestDist = dist;
								closest = monument;
								name = GetMonumentName(monument);
							}							
						}
						localPos = closest.transform.InverseTransformPoint( player.transform.position );

					}
			
					if (!npcData1.ShopKeeper.ContainsKey(args[1].ToLower()))
					{						
						npcData1.ShopKeeper.Add(args[1].ToLower(), new NPCInfo1());
						
						if (args[3].ToLower() == "true")
						{
							float rotat = (closest.transform.rotation.eulerAngles * Mathf.Deg2Rad).y - 2.17f + currentRot.eulerAngles.y;
							npcData1.ShopKeeper[args[1].ToLower()].lootFile = args[2].ToLower();
							npcData1.ShopKeeper[args[1].ToLower()].monumentName = name;
						    npcData1.ShopKeeper[args[1].ToLower()].monumentSpawn = true;
							npcData1.ShopKeeper[args[1].ToLower()].location = localPos;
							npcData1.ShopKeeper[args[1].ToLower()].rotation = currentRot.eulerAngles;
							npcData1.ShopKeeper[args[1].ToLower()].MonumentRotation = rotat;
							
							SaveData1();
							SpawnNpcVendors(args[1].ToLower(), player.transform.position, currentRot.eulerAngles, args[2].ToLower());
					    	SendReply(player, "Spawned vendor " + args[1].ToLower() + " with Loot table " + args[2].ToLower() + " At monument " + name);
						}					
						else
						{
							npcData1.ShopKeeper[args[1].ToLower()].lootFile = args[2].ToLower();
							npcData1.ShopKeeper[args[1].ToLower()].location = player.transform.position;
							npcData1.ShopKeeper[args[1].ToLower()].rotation = currentRot.eulerAngles;
							npcData1.ShopKeeper[args[1].ToLower()].lootFile = args[2].ToLower();
							npcData1.ShopKeeper[args[1].ToLower()].MonumentRotation = currentRot.eulerAngles.y;
							npcData1.ShopKeeper[args[1].ToLower()].monumentSpawn = false;
							SaveData1();
							SpawnNpcVendors(args[1].ToLower(), player.transform.position, npcData1.ShopKeeper[args[1].ToLower()].rotation, args[2].ToLower());
							SendReply(player, "Spawned vendor " + args[1].ToLower() + " with Loot table " + args[2].ToLower());
						}
					}
					else
					{
						SendReply(player, "There is a config named " + args[1].ToLower() + " already.");
						return;
					}
                    break;

				case "edit":
				if (args.Length < 3)
					{
						SendReply(player, "Usage:\n\n" + $"<color={colorCode}>/npc_vendor add <Name> <LootFile></color> - Adds vendor to location.\n" + $"<color={colorCode}>/npc_vendor edit <Name> <LootFile> </color> - Change vendor loot file.\n" + $"<color={colorCode}>/npc_vendor remove <Name></color> - Removes a vendor\n");
						return;
					}
					editNpc(player, args[1].ToLower(), args[2].ToLower());
                    break;
					
                case "remove":
				if (args.Length < 1)
					{
						SendReply(player, "Usage:\n\n" + $"<color={colorCode}>/npc_vendor add <Name> <LootFile></color> - Adds vendor to location.\n" + $"<color={colorCode}>/npc_vendor edit <Name> <LootFile> </color> - Change vendor loot file.\n" + $"<color={colorCode}>/npc_vendor remove <Name></color> - Removes a vendor\n");
						return;
					}
					string theName = "";
					if (args.Length == 2)
						theName = args[1].ToLower();
					removeNpc(player, theName);
                    break;

                default:
					SendReply(player, "Usage:\n\n" + $"<color={colorCode}>/npc_vendor add <Name> <LootFile></color> - Adds vendor to location.\n" + $"<color={colorCode}>/npc_vendor edit <Name> <LootFile> </color> - Change vendor loot file.\n" + $"<color={colorCode}>/npc_vendor remove <Name></color> - Removes a vendor\n");
                    break;
            }
		}
		
		 [ChatCommand("mylocation")]
        private void GetLocations(BasePlayer player, string command, string[] args)
        {
			if (!permission.UserHasPermission(player.UserIDString, useAllow))
            {
                SendReply(player, lang.GetMessage("NoPerm", this));
                return;
            }
			 float lowestDist = float.MaxValue;
             MonumentInfo closest = null;
			 string name = "Unknown";
                foreach ( var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
                {
					if (GetMonumentName(monument) == null || GetMonumentName(monument).Contains("substation")) continue;
                    float dist = Vector3.Distance( player.transform.position, monument.transform.position );
                    if ( dist < lowestDist )
                    {
                        lowestDist = dist;
                        closest = monument;
						name = GetMonumentName(monument);
						if (name.Contains("monument_marker.prefab"))
							name = "\"" + monument.gameObject?.gameObject?.transform?.parent?.gameObject?.transform?.root?.name + "\"" ;
                    }
					
                }
				
                var localPos = closest.transform.InverseTransformPoint( player.transform.position );
                var rotation = player.transform.rotation;
                PrintToChat( $"MonumentName: {name} Pos: {localPos.ToString( "F4" )} Rotation: {rotation.ToString( "F4" )}" );
				Puts(name + " " + localPos.ToString( "F4" ));	
		}
		
		public string GetMonumentName(MonumentInfo monument)
        {
            var gameObject = monument.gameObject;

            while ( gameObject.name.StartsWith( "assets/" ) == false && gameObject.transform.parent != null )
            {
                gameObject = gameObject.transform.parent.gameObject;
            }

            return gameObject?.name;
        }
		
		private void editNpc(BasePlayer player, string theName, string newLootFile)
		{	
			if (!npcData.SellItemList.ContainsKey(newLootFile))
			{
				SendReply(player, "There no LootTable named " + newLootFile + ". You can use default if you have no tables.");
				return;
			}
			else if (!npcData1.ShopKeeper.ContainsKey(theName))
			{
				SendReply(player, "ShopKeeper " +  theName + " is not in the save list.");
				return;
			}
			else if (npcData1.ShopKeeper.ContainsKey(theName))
			{
				npcData1.ShopKeeper[theName].lootFile = newLootFile;
				SaveData1();
				SendReply(player, "You updated vendor " + theName + " with new loot table " + newLootFile);
			}
		}
		
		private void removeNpc(BasePlayer player, string theName = "")
		{	
			if (theName == "")
			{
				Quaternion currentRot;
				if (!TryGetPlayerView(player, out currentRot)) return;
				object closestEnt;
				Vector3 closestHitpoint;
				if (!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint)) return;
				NPCShopKeeper keeperShop = ((Collider)closestEnt).GetComponentInParent<NPCShopKeeper>();
				if (keeperShop == null)
				{
					SendReply(player, "This is not an ShopKeeper");
					return;
				}
				if (keeperShop.displayName == null || !npcData1.ShopKeeper.ContainsKey(keeperShop.displayName))
				{
					SendReply(player, "This ShopKeeper is not in the save list.");
					return;
				}
				else if (npcData1.ShopKeeper.ContainsKey(keeperShop.displayName))
				{
					npcData1.ShopKeeper.Remove(keeperShop.displayName);
					SaveData1();
					SendReply(player, "You removed vendor " + keeperShop.displayName);
					keeperShop?.machine?.Kill();
					keeperShop.Kill();
				}
			}
			else if (theName != "")
			{
				if (!npcData1.ShopKeeper.ContainsKey(theName))
				{
					SendReply(player, "This ShopKeeper " +  theName + " is not in the save list.");
					return;
				}
				else if (npcData1.ShopKeeper.ContainsKey(theName))
				{
					NPCShopKeeper networkable = BaseNetworkable.serverEntities.Find(npcData1.ShopKeeper[theName].currentID) as NPCShopKeeper;
					npcData1.ShopKeeper.Remove(theName);
					SaveData1();
					SendReply(player, "You removed vendor " + theName);
					networkable?.machine?.Kill();
					networkable?.Kill();
				}
			}
		}
		
		private void SpawnNpcVendors(string name, Vector3 position, Vector3 rotation, string theConfig = "default", float rot = 0f)
		{
			if (!npcData.SellItemList.ContainsKey(theConfig))
				theConfig = "default";
			if (!npcData.SellItemList.ContainsKey(theConfig))
				return;
			InvisibleVendingMachine newVedMachine = null;
			NPCVendingOrder.Entry[] orders = null;
			newVedMachine = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/npcvendingmachines/shopkeeper_vm_invis.prefab", position, Quaternion.Euler(rotation.x, rotation.y + rot, rotation.z)) as InvisibleVendingMachine;      
			NextTick(() =>
            {
				NPCShopKeeper newVednder = GameManager.server.CreateEntity("assets/prefabs/npc/bandit/shopkeepers/bandit_shopkeeper.prefab", position, Quaternion.Euler(0f, rotation.y + rot, 0f)) as NPCShopKeeper;
				newVednder.enableSaving = false;
				newVedMachine.enableSaving = false;

				if (newVednder != null && newVedMachine != null)
				{
					if (npcData.SellItemList.ContainsKey(theConfig))
						orders = GetOrdersForMachine(newVedMachine, theConfig);
					newVednder.enableSaving = false;
					newVedMachine.enableSaving = false;
					newVedMachine.vendingOrders = ScriptableObject.CreateInstance<NPCVendingOrder>();
					newVedMachine.vmoManifest = ScriptableObject.CreateInstance<NPCVendingOrderManifest>();
					if (orders != null && orders.Length > 0)
					{
						newVedMachine.vendingOrders.orders = orders;
						Array.Resize(ref newVedMachine.vmoManifest.orderList, 1);
						newVedMachine.vmoManifest.orderList[0] = newVedMachine.vendingOrders;
					}
					newVedMachine.Spawn();
					newVednder.machine = newVedMachine;
					newVednder.invisibleVendingMachineRef.Set((BaseEntity) newVedMachine);
					newVednder.displayName = name;
					newVednder.Spawn();
					newVednder.SendNetworkUpdateImmediate();	
					puzzleEntitys.Add(newVednder.net.ID);
					if (newVednder.machine == null) Puts("Default is null");
					newVednder.machine.shopName = name;

					newVedMachine.SendNetworkUpdateImmediate();
					puzzleEntitys.Add(newVedMachine.net.ID);
					newVedMachine.CancelInvoke(newVedMachine.Refill);
					if (!vendOrders.ContainsKey(newVedMachine.net.ID)) vendOrders.Add(newVedMachine.net.ID, theConfig);

					//NextTick(() => newVedMachine.CancelInvoke(newVedMachine.Refill) );
				
					if (npcData1.ShopKeeper.ContainsKey(name))
					{
						if (npcData1.ShopKeeper[name].currentID != null && npcData1.ShopKeeper[name].currentID != 0)
						{
							var networkable = BaseNetworkable.serverEntities.Find(npcData1.ShopKeeper[name].currentID);
							if (networkable != null) networkable?.Kill();
						}
							
						npcData1.ShopKeeper[name].currentID = newVednder.net.ID;
						SaveData1();
					}
					
					timer.Every(320f, () =>	{ if (newVedMachine != null) refill(newVedMachine, theConfig); });
					timer.Once(3, () => 
					{
						newVedMachine.CancelInvoke(newVedMachine.Refill);
						refill(newVedMachine, theConfig);
					});						
					/*
						if (orders != null && orders.Length >= 1)
						{
							if (orders != null && orders.Length > 0)
							{
								newVedMachine.ClearSellOrders();
								newVedMachine.inventory.Clear();
								ItemManager.DoRemoves();
								newVedMachine.inventory.capacity = 1000;
								newVedMachine.vendingOrders.orders = orders;
								newVedMachine.InstallFromVendingOrders();
								if (!vendOrders.ContainsKey(newVedMachine.net.ID)) vendOrders.Add(newVedMachine.net.ID, orders);
								timer.Every(320f, () =>	{ if (newVedMachine != null) refill(newVedMachine, theConfig); });
							}
						}
							refill(newVedMachine, theConfig); 
					} );
					
					timer.Once(30, () =>
					{
						if (orders != null && orders.Length >= 1)
						{
							if (orders != null && orders.Length > 0)
							{  
								newVednder.machine = newVedMachine; 
								newVedMachine.ClearSellOrders();
								newVedMachine.inventory.Clear();
								ItemManager.DoRemoves();
								newVedMachine.inventory.capacity = 1000;
								newVedMachine.vendingOrders.orders = orders;
								newVedMachine.InstallFromVendingOrders();
								if (!vendOrders.ContainsKey(newVedMachine.net.ID)) vendOrders.Add(newVedMachine.net.ID, orders);
								refill(newVedMachine, theConfig);							
								timer.Every(320f, () =>	{ if (newVedMachine != null) refill(newVedMachine, theConfig); });
							}
						}
					}); */
					
			   } 
			});		   
		}
		
		private void OnNpcGiveSoldItem(NPCVendingMachine machine, Item item, BasePlayer buyer)
		{
			if (vendOrders.ContainsKey(machine.net.ID))
			{
				if (vendOrders[machine.net.ID] != null)
				{
					//machine.Refill();
					refill(machine, vendOrders[machine.net.ID]);
				}
			}
		}
		
		private NPCVendingOrder.Entry[] GetOrdersForMachine(NPCVendingMachine vending, string theConfig)
        {
            List<NPCVendingOrder.Entry> temp = new List<NPCVendingOrder.Entry>();
			int count = 0;
            foreach (var order in npcData.SellItemList[theConfig].itemsList.ToList())
            {
				ItemDefinition itemdef = ItemManager.FindItemDefinition(order.currencyId);
				ItemDefinition itemdef2 =  ItemManager.FindItemDefinition(order.sellId);
				if (itemdef == null || itemdef2 == null) continue;

                temp.Add(new NPCVendingOrder.Entry
                {
                    currencyAmount = order.sellAmount,
                    currencyAsBP = order.currencyAsBP,
                    currencyItem = ItemManager.FindItemDefinition(order.currencyId),
                    sellItem = ItemManager.FindItemDefinition(order.sellId),
                    sellItemAmount = order.currencyAmount,
                    sellItemAsBP = order.sellAsBP,
					weight = 100,
                    refillAmount = 500,
                    refillDelay = 30.1f
                });
				count++;
				if(temp.Count == 7)
					break;
            }
			if (temp == null || temp.Count <= 0) return null;
            return temp.ToArray();
        }
		
		private void setSkin(NPCVendingMachine vending, string theConfig)
		{
			foreach (var order in npcData.SellItemList[theConfig].itemsList.ToList())
            {
				foreach (var item in vending.inventory.itemList)
				{
					if (item.info.itemid == order.sellId) item.skin = order.sellskinID;
				}
			}
		}
		
		private void refill(NPCVendingMachine newVedMachine, string theConfig)
		{
			if (newVedMachine == null || newVedMachine.transactionActive || !vendOrders.ContainsKey(newVedMachine.net.ID)) return;
			//newVedMachine.ClearSellOrders();
			newVedMachine.inventory.Clear();
			ItemManager.DoRemoves();
							
			newVedMachine.transactionActive = true;
			foreach (var order in npcData.SellItemList[theConfig].itemsList.ToList())
            {
			    if (order.sellAsBP)
				{
					var item = ItemManager.CreateByItemID(newVedMachine.blueprintBaseDef.itemid, order.sellAmount * 10);
					
					item.blueprintTarget = order.sellId;
				//	item.name = offer.SellItem.DisplayName;					
					newVedMachine.inventory.Insert(item);
				}
				else
				{
					var item = ItemManager.CreateByItemID(order.sellId, order.sellAmount * 10000, order.sellskinID);
					
					item.name = order.sellItemName;
					
					newVedMachine.inventory.Insert(item);
				}
			}
			newVedMachine.transactionActive = false;
		}
		
		private bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
        {
            viewAngle = new Quaternion(0f, 0f, 0f, 0f);
            if (player.serverInput?.current == null) return false;
            viewAngle = Quaternion.Euler(player.serverInput.current.aimAngles);
            return true;
        }
		
		private bool TryGetClosestRayPoint(Vector3 sourcePos, Quaternion sourceDir, out object closestEnt, out Vector3 closestHitpoint)
        {
            Vector3 sourceEye = sourcePos + new Vector3(0f, 1.5f, 0f);
            Ray ray = new Ray(sourceEye, sourceDir * Vector3.forward);

            var hits = Physics.RaycastAll(ray);
            float closestdist = 999999f;
            closestHitpoint = sourcePos;
            closestEnt = false;
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider.GetComponentInParent<TriggerBase>() == null && hit.distance < closestdist)
                {
                    closestdist = hit.distance;
                    closestEnt = hit.collider;
                    closestHitpoint = hit.point;
                }
            }

            if (closestEnt is bool) return false;
            return true;
        }
		
		private void RemoveOldDoor(Vector3 position, float size)
		{
			List<Door> nearby = new List<Door>();
            Vis.Entities<Door>(position, size, nearby);
            foreach (Door item in nearby.Distinct().ToList())
			{
                if (item is Door)
				{
                   // PrintWarning("Removing old door and placing new secerity door.");
                    item?.Kill();
                }
            }
        }
		
		void SpawnRefresh(BaseNetworkable entity1)
        {
            UnityEngine.Object.Destroy(entity1.GetComponent<Collider>());
        }

		void OnLootEntityEnd(BasePlayer player, LootContainer entity)
		{
			Vector3 Loc = Vector3.zero;
			Quaternion quad = Quaternion.Euler(Vector3.zero);
			LootContainer newBox = null;
			uint netID = 0;
			if (entity != null && LootBoxesPos.ContainsKey(entity.net.ID))
			{
				netID = entity.net.ID;
				Loc = entity.transform.position;
				quad = entity.transform.rotation;
				string prefab = LootBoxesPos[netID];
				var boxInfo = LootBoxesPos[netID];
				timer.Once(1200.0f, () =>
				{ 
					
					if (Loc != Vector3.zero)
					{
						newBox = GameManager.server.CreateEntity(prefab, Loc, quad) as LootContainer;
						newBox.enableSaving = false;
						newBox.Spawn();						
						LootBoxesPos.Add(newBox.net.ID, boxInfo);
						
					}
					if (entity != null) entity?.Kill();
				});
				LootBoxesPos.Remove(entity.net.ID);
			}
		}

		void OnCardSwipe(CardReader reader1, Keycard card1, BasePlayer player)
        {
			if (!newPuzzles.ContainsKey(reader1)) return;
			if (card1.accessLevel != reader1.accessLevel) return;
			else
			{
				if (newPuzzles[reader1] != null)
				newPuzzles[reader1].UpdateFromInput(25, 0);
				timer.Once(60.2f, () => { if (newPuzzles[reader1] != null) newPuzzles[reader1].UpdateFromInput(0, 0); });
			}
		}
		
		void OnButtonPress(PressButton button, BasePlayer player)
		{
			if (!newPuzzles.ContainsKey(button)) return;

			if (newPuzzles[button] != null)
			newPuzzles[button].UpdateFromInput(25, 0);
			timer.Once(60.2f, () => { if (newPuzzles[button] != null) newPuzzles[button].UpdateFromInput(0, 0); });
			
		}
	
		object CanPickupEntity(BasePlayer player, IOEntity entity)
        {
			if (entity == null) return null;
			uint entityID = entity.net.ID;
			if (entity != null && puzzleEntitys.Contains(entityID) || lightEntitys.Contains(entityID))
            {
                return false;
            }
				return null;
		}
		
		private void connectWires(IOEntity entity, IOEntity entity1)
		{
			IOEntity.IOSlot ioOutput = entity.outputs[0];			
			if (ioOutput != null)
            {
                ioOutput.connectedTo = new IOEntity.IORef();
                ioOutput.connectedTo.Set(entity1);
                ioOutput.connectedToSlot = 0;
                ioOutput.connectedTo.Init();

                entity1.inputs[0].connectedTo = new IOEntity.IORef();
                entity1.inputs[0].connectedTo.Set(entity);
                entity1.inputs[0].connectedToSlot = 0;
                entity1.inputs[0].connectedTo.Init();
				//entity.rustWattSeconds = live;							
			}
	}
	
		BaseEntity SpawnTheEntitys(Vector3 pos, float rot, float rotation, string prefab, MonumentInfo monument)
		{
	    	var itemsEntity = GameManager.server.CreateEntity(prefab, pos, Quaternion.Euler(monument.transform.localEulerAngles.x, monument.transform.localEulerAngles.y + rot, monument.transform.localEulerAngles.z));
			itemsEntity.enableSaving = false;
			if (rotation != 0f)
			{
				itemsEntity.transform.Rotate(rotation, 0, 0);
				itemsEntity.SendNetworkUpdateImmediate();		
			}
									
			itemsEntity.Spawn();
			puzzleEntitys.Add(itemsEntity.net.ID);
			SpawnRefresh(itemsEntity);
			if (itemsEntity.name.Contains("campfire"))
			{
				itemsEntity.SetFlag(BaseEntity.Flags.Locked, true, false, true);
				itemsEntity.SetFlag(BaseEntity.Flags.On, true, false, true);
			}
			return itemsEntity;					
		}
		
		LootContainer spawnInTheLoot(Vector3 pos, string prefab, float rot, float rotation, MonumentInfo monument)
		{
			LootContainer itemsEntity = GameManager.server.CreateEntity(prefab, pos, Quaternion.Euler(monument.transform.localEulerAngles.x, monument.transform.localEulerAngles.y + rot, monument.transform.localEulerAngles.z)) as LootContainer;
			if (rotation != 0f)
			{
				itemsEntity.transform.Rotate(rotation, 0, 0);
				itemsEntity.SendNetworkUpdateImmediate();		
			}
			itemsEntity.enableSaving = false;
			itemsEntity.Spawn();
			LootBoxesPos.Add(itemsEntity.net.ID, prefab);
			return itemsEntity;
		}
		
		private void CheckTheEntitys(IOEntity reader1, DoorManipulator controler, IOEntity button1, IOEntity theBox)
		{			
			if (reader1 != null && controler != null)
			newPuzzles.Add(reader1, controler);
			if (button1 != null || controler != null)
			newPuzzles.Add(button1, controler);
			if (theBox != null && reader1 != null)
			{
				fuseBoxs.Add(theBox, reader1);
				connectWires(theBox, reader1);
				theBox.UpdateFromInput(25, 0);
			}
		}
		
		public void SpawnPuzzles(bool unLoading)
		{		
			foreach (var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
			{
				if (!unLoading) spawnInLights(monument);
				Vector3 itemsVector = Vector3.zero;
                var monumentName = monument.GetMonumentName();
				switch (monumentName)
				{
                    default: continue;
					case MonumentName.Outpost:
					{
						if (!configData.bigWheel.Outpost || unLoading) continue;
						foreach (var key in OutpostStuff)
						{							
							itemsVector = monument.transform.TransformPoint(key.pos);
							if (itemsVector != Vector3.zero)
							{	
								BaseEntity itemsEntity = SpawnTheEntitys(itemsVector, key.rot, key.moveRotate, key.prefab, monument);
							}
						}
						break;
					}
					case MonumentName.Junkyard:
					{					
						if (!configData.settings.Junkyard || unLoading) continue;
						
							IOEntity reader1 = null;
							DoorManipulator controler = null;
							IOEntity button1 = null;
							IOEntity theBox = null;
						
							foreach (var key in JunkyardDoor)
							{							
								itemsVector = monument.transform.TransformPoint(key.pos);
								if (itemsVector != Vector3.zero)
								{
									if (key.prefab.Contains("door"))
										RemoveOldDoor(itemsVector, 0.0001f);
									
									BaseEntity itemsEntity = SpawnTheEntitys(itemsVector, key.rot, key.moveRotate, key.prefab, monument);
									if (itemsEntity == null) continue;
									
									if (itemsEntity is CardReader)
									{
										reader1 = itemsEntity as IOEntity;
									}
									else if (itemsEntity is Door)
										timer.Once(0.2f, () => { (itemsEntity as Door).CloseRequest(); });
									else if (itemsEntity is DoorManipulator)
										controler = itemsEntity as DoorManipulator;
									else if (itemsEntity is PressButton)
										button1 = itemsEntity as IOEntity;
									else if (itemsEntity is ItemBasedFlowRestrictor)
									{
										theBox = itemsEntity as IOEntity;
									}
								}
							}
								CheckTheEntitys(reader1, controler, button1, theBox);	
								
							foreach (var key in JunkyardBoxLoc)
							{
								itemsVector = monument.transform.TransformPoint(key.pos);
								if (itemsVector != Vector3.zero)
								{
									spawnInTheLoot(itemsVector, key.prefab, key.rot, key.moveRotate, monument);
								}
							}
							
                        break;
                    }					
                    case MonumentName.Dome:
					{					
						if (!configData.settings.Dome) continue;
						
						if (unLoading)
						{
							foreach (var key in DomeDoorReplace)
							{							
								itemsVector = monument.transform.TransformPoint(key.pos);
								if (itemsVector != Vector3.zero)
								{
									var itemsEntity = GameManager.server.CreateEntity(key.prefab, itemsVector, Quaternion.Euler(monument.transform.localEulerAngles.x, monument.transform.localEulerAngles.y + key.rot, monument.transform.localEulerAngles.z));
									if (key.moveRotate != 0f)
									{
										itemsEntity.transform.Rotate(key.moveRotate, 0, 0);
										itemsEntity.SendNetworkUpdateImmediate();		
									}
									
									itemsEntity.Spawn();
									SpawnRefresh(itemsEntity);
								}
							}
									continue;
						}
							IOEntity reader1 = null;
							DoorManipulator controler = null;
							IOEntity button1 = null;
							IOEntity theBox = null;
						
							foreach (var key in DomeDoor)
							{							
								itemsVector = monument.transform.TransformPoint(key.pos);
								if (itemsVector != Vector3.zero)
								{
									if (key.prefab.Contains("door"))
										RemoveOldDoor(itemsVector, 0.0001f);
									
									BaseEntity itemsEntity = SpawnTheEntitys(itemsVector, key.rot, key.moveRotate, key.prefab, monument);
									if (itemsEntity == null) continue;
									
									if (itemsEntity is CardReader)
									{
										reader1 = itemsEntity as IOEntity;
										//(itemsEntity as IOEntity).UpdateFromInput(25, 0);
									}
									else if (itemsEntity is Door)
										timer.Once(0.2f, () => { (itemsEntity as Door).CloseRequest(); });
									else if (itemsEntity is DoorManipulator)
										controler = itemsEntity as DoorManipulator;
									else if (itemsEntity is PressButton)
										button1 = itemsEntity as IOEntity;
									else if (itemsEntity is ItemBasedFlowRestrictor)
									{
										theBox = itemsEntity as IOEntity;
									}
								}
							}
								
							CheckTheEntitys(reader1, controler, button1, theBox);
							
							foreach (var key in DomeBoxLoc)
							{
								itemsVector = monument.transform.TransformPoint(key.pos);
								if (itemsVector != Vector3.zero)
								{
									spawnInTheLoot(itemsVector, key.prefab, key.rot, key.moveRotate, monument);
								}
							}
							
                        break;
                    }
                    case MonumentName.Supermarket:
					{  	
						if (!configData.settings.Supermarket) continue;
						
						if (unLoading)
						{
							foreach (var key in SupermarketDoorReplace)
							{							
								itemsVector = monument.transform.TransformPoint(key.pos);
								if (itemsVector != Vector3.zero)
								{
									var itemsEntity = GameManager.server.CreateEntity(key.prefab, itemsVector, Quaternion.Euler(monument.transform.localEulerAngles.x, monument.transform.localEulerAngles.y + key.rot, monument.transform.localEulerAngles.z));
									if (key.moveRotate != 0f)
									{
										itemsEntity.transform.Rotate(key.moveRotate, 0, 0);
										itemsEntity.SendNetworkUpdateImmediate();		
									}
									
									itemsEntity.Spawn();
									SpawnRefresh(itemsEntity);
								}
							}
									continue;
						}
							IOEntity reader1 = null;
							DoorManipulator controler = null;
							IOEntity button1 = null;
							IOEntity theBox = null;
							foreach (var key in SupermarketDoor)
							{							
								itemsVector = monument.transform.TransformPoint(key.pos);
								if (itemsVector != Vector3.zero)
								{
									if (key.prefab.Contains("door"))
										RemoveOldDoor(itemsVector, 0.0001f);
									
									BaseEntity itemsEntity = SpawnTheEntitys(itemsVector, key.rot, key.moveRotate, key.prefab, monument);
									if (itemsEntity == null) continue;
									
									if (itemsEntity is CardReader)
									{
										reader1 = itemsEntity as IOEntity;
										(itemsEntity as IOEntity).UpdateFromInput(25, 0);
									}
									else if (itemsEntity is Door)
										timer.Once(0.2f, () => { (itemsEntity as Door).CloseRequest(); });
									else if (itemsEntity is DoorManipulator)
										controler = itemsEntity as DoorManipulator;
									else if (itemsEntity is PressButton)
										button1 = itemsEntity as IOEntity;
								}
							}
									CheckTheEntitys(reader1, controler, button1, theBox);
										
							foreach (var key in SupermarketDoorTwo)
							{							
								itemsVector = monument.transform.TransformPoint(key.pos);
								if (itemsVector != Vector3.zero)
								{
									if (key.prefab.Contains("door"))
										RemoveOldDoor(itemsVector, 0.0001f);
									
									BaseEntity itemsEntity = SpawnTheEntitys(itemsVector, key.rot, key.moveRotate, key.prefab, monument);
									if (itemsEntity == null) continue;
									
									if (itemsEntity is CardReader)
									{
										reader1 = itemsEntity as IOEntity;
										(itemsEntity as IOEntity).UpdateFromInput(25, 0);
									}
									else if (itemsEntity is Door)
										timer.Once(0.2f, () => { (itemsEntity as Door).CloseRequest(); });
									else if (itemsEntity is DoorManipulator)
										controler = itemsEntity as DoorManipulator;
									else if (itemsEntity is PressButton)
										button1 = itemsEntity as IOEntity;
								}
							}
									CheckTheEntitys(reader1, controler, button1, theBox);
										
							foreach (var key in SupermarketBoxLoc)
							{
								itemsVector = monument.transform.TransformPoint(key.pos);
								if (itemsVector != Vector3.zero)
								{
									spawnInTheLoot(itemsVector, key.prefab, key.rot, key.moveRotate, monument);
								}
							}
						}	
                        break;
                    }
				}
		}

			private void spawnInLights(MonumentInfo monument)
			{
				Vector3 LightVector = Vector3.zero;
                var monumentName = monument.GetMonumentName();
				switch (monumentName)
				{
                    default: return;                                                             
                    case MonumentName.GasStation:
					{
						List<BaseEntity> savedLights = new List<BaseEntity>();
						List<IOEntity> savedSimpleLight = new List<IOEntity>();
						List<BaseEntity> savedSwitches = new List<BaseEntity>();
						bool powered = false;
						if (configData.settingsL.GasStation)
						{
							foreach (var key in placemntGasStation)
							{							
								LightVector = monument.transform.TransformPoint(key.pos);
								if (LightVector != Vector3.zero)
								{
									var lightEntity = GameManager.server.CreateEntity(key.prefab, LightVector, Quaternion.Euler(monument.transform.localEulerAngles.x, monument.transform.localEulerAngles.y + key.rot, monument.transform.localEulerAngles.z));
									lightEntity.enableSaving = false;
									if (key.moveRotate != 0f)
									{
										lightEntity.transform.Rotate(key.moveRotate, 0, 0);
										lightEntity.SendNetworkUpdateImmediate();	
									}

									lightEntity.Spawn();
									lightEntitys.Add(lightEntity.net.ID);
									ProjectileWeaponMod flashlight = lightEntity.gameObject.GetComponent<ProjectileWeaponMod>();
									if (flashlight != null)
									{				
										lightEntity.SetFlag(BaseEntity.Flags.Disabled, false, false, true);
										lightEntity.enableSaving = false;
										WiredFlashLightGasStation.Add(flashlight.net.ID, 0);
									}

									SpawnRefresh(lightEntity);
									savedLights.Add(lightEntity);
									
									if (lightEntity is SimpleLight)
									{
										WiredLightGasStation.Add(lightEntity.net.ID);
										savedSimpleLight.Add((lightEntity as IOEntity));
										if (savedSimpleLight.Count >= 2)
										{
											connectWires(savedSimpleLight[savedSimpleLight.Count - 2], (lightEntity as IOEntity));
										}
									}
									else if (lightEntity is ElectricSwitch)
									{
										if (configData.settingsL.UseLightSwitch)
										{
											if (!powered && savedSimpleLight.Count >= 1)
											{
												powered = true;
												connectWires((lightEntity as IOEntity), savedSimpleLight[0]);
											}
											WiredSwitchGasStation.Add(lightEntity.net.ID, 0);
											if (lightEntity is IOEntity) (lightEntity as IOEntity).UpdateFromInput(250, 0);
										}
										else lightEntity.Kill();
									}
								}
							}
						}							
						break;
                    }
                    case MonumentName.Supermarket:
					{  
						List<BaseEntity> savedLights = new List<BaseEntity>();
						List<IOEntity> savedSimpleLight = new List<IOEntity>();
						List<BaseEntity> savedSwitches = new List<BaseEntity>();
						bool powered = false;
						if (configData.settingsL.Supermarket)
						{
							foreach (var key in placemntSupermarket)
							{							
								LightVector = monument.transform.TransformPoint(key.pos);
								if (LightVector != Vector3.zero)
								{
									var lightEntity = GameManager.server.CreateEntity(key.prefab, LightVector, Quaternion.Euler(monument.transform.localEulerAngles.x, monument.transform.localEulerAngles.y + key.rot, monument.transform.localEulerAngles.z));
									lightEntity.enableSaving = false;
									if (key.moveRotate != 0f)
									{
										lightEntity.transform.Rotate(key.moveRotate, 0, 0);
										lightEntity.SendNetworkUpdateImmediate();	
									}
									
									lightEntity.Spawn();
									lightEntitys.Add(lightEntity.net.ID);
									ProjectileWeaponMod flashlight = lightEntity.gameObject.GetComponent<ProjectileWeaponMod>();
									if (flashlight != null)
									{				
										lightEntity.SetFlag(BaseEntity.Flags.Disabled, false, false, true);
										lightEntity.enableSaving = false;
										WiredFlashLightSupermarket.Add(flashlight.net.ID, 0);
									}
																				
									SpawnRefresh(lightEntity);
									savedLights.Add(lightEntity);
									
									if (lightEntity is SimpleLight)
									{
										WiredLightSupermarket.Add(lightEntity.net.ID);
										savedSimpleLight.Add((lightEntity as IOEntity));
										if (savedSimpleLight.Count >= 2)
										{
											connectWires(savedSimpleLight[savedSimpleLight.Count - 2], (lightEntity as IOEntity));
										}
									}
									else if (lightEntity is ElectricSwitch)
									{
										if (configData.settingsL.UseLightSwitch)
										{
											if (!powered && savedSimpleLight.Count >= 1)
											{
												powered = true;
												connectWires((lightEntity as IOEntity), savedSimpleLight[0]);
											}
											WiredSwitchSupermarket.Add(lightEntity.net.ID, 0);
											if (lightEntity is IOEntity) (lightEntity as IOEntity).UpdateFromInput(250, 0);
										}
										else lightEntity.Kill();
									}
								}
							}
						}							
						break;
					}
					
					case MonumentName.MiningOutpost:
					{ 
						List<BaseEntity> savedLights = new List<BaseEntity>();
						List<IOEntity> savedSimpleLight = new List<IOEntity>();
						List<BaseEntity> savedSwitches = new List<BaseEntity>();
						bool powered = false;
						if (configData.settingsL.MiningOutpost)
						{
							foreach (var key in placemntMiningOutpost)
							{							
								LightVector = monument.transform.TransformPoint(key.pos);
								if (LightVector != Vector3.zero)
								{
									var lightEntity = GameManager.server.CreateEntity(key.prefab, LightVector, Quaternion.Euler(monument.transform.localEulerAngles.x, monument.transform.localEulerAngles.y + key.rot, monument.transform.localEulerAngles.z));
									lightEntity.enableSaving = false;
									if (key.moveRotate != 0f)
									{
										lightEntity.transform.Rotate(key.moveRotate, 0, 0);
										lightEntity.SendNetworkUpdateImmediate();	
									}
									
									lightEntity.Spawn();
									lightEntitys.Add(lightEntity.net.ID); 
									ProjectileWeaponMod flashlight = lightEntity.gameObject.GetComponent<ProjectileWeaponMod>();
									if (flashlight != null)
									{				
										lightEntity.SetFlag(BaseEntity.Flags.Disabled, false, false, true);
										lightEntity.enableSaving = false;
										WiredFlashLightMiningOutpost.Add(flashlight.net.ID, 0);
									}
																					
									SpawnRefresh(lightEntity);
									savedLights.Add(lightEntity);
									
									if (lightEntity is SimpleLight)
									{
										WiredLightMiningOutpost.Add(lightEntity.net.ID);
										savedSimpleLight.Add((lightEntity as IOEntity));
										if (savedSimpleLight.Count >= 2)
										{
											connectWires(savedSimpleLight[savedSimpleLight.Count - 2], (lightEntity as IOEntity));
										}
									}
									else if (lightEntity is ElectricSwitch)
									{
										if (configData.settingsL.UseLightSwitch)
										{
											if (!powered && savedSimpleLight.Count >= 1)
											{
												powered = true;
												connectWires((lightEntity as IOEntity), savedSimpleLight[0]);
											}
											WiredSwitchMiningOutpost.Add(lightEntity.net.ID, 0);
											if (lightEntity is IOEntity) (lightEntity as IOEntity).UpdateFromInput(250, 0);
										}
										else lightEntity.Kill();
									}
								}
							}
						}													
						break;
                    }
					
					case MonumentName.Airfield:
					{ 
					    List<BaseEntity> savedLights = new List<BaseEntity>();
						List<IOEntity> savedSimpleLight = new List<IOEntity>();
						List<BaseEntity> savedSwitches = new List<BaseEntity>();
						bool powered = false;
						if (configData.settingsL.Airfield)
						{
							foreach (var key in placemntAirfieldHangers)
							{							
								LightVector = monument.transform.TransformPoint(key.pos);
								if (LightVector != Vector3.zero)
								{
									var lightEntity = GameManager.server.CreateEntity(key.prefab, LightVector, Quaternion.Euler(monument.transform.localEulerAngles.x, monument.transform.localEulerAngles.y + key.rot, monument.transform.localEulerAngles.z));
									lightEntity.enableSaving = false;
									if (key.moveRotate != 0f)
									{
										lightEntity.transform.Rotate(key.moveRotate, 0, 0);
										lightEntity.SendNetworkUpdateImmediate();	
									}									
									
									lightEntity.Spawn();
									lightEntitys.Add(lightEntity.net.ID);
									ProjectileWeaponMod flashlight = lightEntity.gameObject.GetComponent<ProjectileWeaponMod>();
									if (flashlight != null)
									{				
										lightEntity.SetFlag(BaseEntity.Flags.Disabled, false, false, true);
										lightEntity.enableSaving = false;
									}
									nonSwitchLights.Add(lightEntity.net.ID);
									SpawnRefresh(lightEntity);
								}
							}
                            foreach (var key in placemntAirfieldBuilding)
							{							
								LightVector = monument.transform.TransformPoint(key.pos);
								if (LightVector != Vector3.zero)
								{
									var lightEntity = GameManager.server.CreateEntity(key.prefab, LightVector, Quaternion.Euler(monument.transform.localEulerAngles.x, monument.transform.localEulerAngles.y + key.rot, monument.transform.localEulerAngles.z));
									lightEntity.enableSaving = false;
									if (key.moveRotate != 0f)
									{
										lightEntity.transform.Rotate(key.moveRotate, 0, 0);
										lightEntity.SendNetworkUpdateImmediate();	
									}

									lightEntity.Spawn();
									lightEntitys.Add(lightEntity.net.ID); 
									ProjectileWeaponMod flashlight = lightEntity.gameObject.GetComponent<ProjectileWeaponMod>();
									if (flashlight != null)
									{				
										lightEntity.SetFlag(BaseEntity.Flags.Disabled, false, false, true);
										lightEntity.enableSaving = false;
										WiredFlashLightAirfield.Add(flashlight.net.ID, 0);
									}
																					
									SpawnRefresh(lightEntity);
									savedLights.Add(lightEntity);
									
									if (lightEntity.name.Contains("ceilinglight") || lightEntity is SimpleLight)
									{
										WiredLightAirfield.Add(lightEntity.net.ID);
										savedSimpleLight.Add((lightEntity as IOEntity));
										if (savedSimpleLight.Count >= 2)
										{
											connectWires(savedSimpleLight[savedSimpleLight.Count - 2], (lightEntity as IOEntity));
										}
									}
									else if (lightEntity is ElectricSwitch)
									{
										if (configData.settingsL.UseLightSwitch)
										{
											if (!powered && savedSimpleLight.Count >= 1)
											{
												powered = true;
												connectWires((lightEntity as IOEntity), savedSimpleLight[0]);
											}
											WiredSwitchAirfield.Add(lightEntity.net.ID, 0);
											if (lightEntity is IOEntity) (lightEntity as IOEntity).UpdateFromInput(250, 0);
										}
										else lightEntity.Kill();
									}
								}
							}
						}							
						break;
                    }					
				}		
			}

		// CopyPaste stuff
		bool PasteBuilding(Vector3 pos, float rotationCorrection, string filename, List<string> options)
		{
			var success = CopyPaste.Call("TryPasteFromVector3", pos, rotationCorrection, filename, options.ToArray());

			if(success is string)
			{
				return false;
			}

			return true;
		}
		void OnPasteFinished(List<BaseEntity> pastedEntities)
		{
			if (!isPasting || pastedEntities == null) return;
				foreach (BaseEntity key in pastedEntities.ToList())
				{
					if (key != null)
					{
						key.enableSaving = false;
						buildingEntitys.Add(key.net.ID);
					}
				}
		}
		
		public bool HasSaveFile(string id)
        {
            return Interface.Oxide.DataFileSystem.ExistsDatafile($"copypaste/{id.ToString()}");
        }
		
		private Coroutine PasteRoutine { get; set; }
		private IEnumerator SpawnBuilding(List<string> BuildingName)
        {
			if (BuildingName == null || BuildingName.Count <=0)
				if (_instance.PasteRoutine != null)
				{
					_instance.isPasting = false; 
                    ServerMgr.Instance.StopCoroutine(_instance.PasteRoutine);
				    _instance.PasteRoutine = null;
				}

            while (BuildingName.Count > 0)
            {	
				string name = configData.buildingsz.building[BuildingName[0]].monumentName;
				Vector3 pos = configData.buildingsz.building[BuildingName[0]].position;
				string fileName = configData.buildingsz.building[BuildingName[0]].pasteFile; 
				float rot = configData.buildingsz.building[BuildingName[0]].rotation;
				List<string> options = configData.buildingsz.building[BuildingName[0]].options;
				if (!HasSaveFile(fileName)) PrintWarning($"CopyPaste could not find paste file " + fileName);
				if (HasSaveFile(fileName))
				foreach (var monument in TerrainMeta.Path.Monuments)
				{
					Vector3 posVector = Vector3.zero;
					if (monument == null) continue;
					if (!monument.name.ToLower().Contains(name.ToLower())) continue;
					posVector = monument.transform.TransformPoint(pos);
					float rotation = (monument.transform.rotation.eulerAngles * Mathf.Deg2Rad).y - 2.17f + rot;
					_instance.isPasting = true;
				  _instance.PasteBuilding(posVector, rotation, fileName, options);
				}

				  BuildingName.RemoveAt(0);

                yield return CoroutineEx.waitForSeconds(20f);
            }
				yield return new WaitForSeconds(120);
				_instance.isPasting = false; 
				if (_instance.QueuedRoutine != null)
				{
                    ServerMgr.Instance.StopCoroutine(_instance.PasteRoutine);
					_instance.PasteRoutine = null;				
                }
        }
		
	}	
		namespace MonumentPlusEx
		{
			public static class MonumentPlusEx
			{
				public enum MonumentName
				{
					Unknown = 0,
					GasStation,
					Supermarket,
					Dome,
					Junkyard,
					Outpost,
					MiningOutpost,
					Airfield
				}

				private static Dictionary<string, MonumentName> MonumentToName = new Dictionary<string, MonumentName>(){
					{"assets/bundled/prefabs/autospawn/monument/roadside/gas_station_1.prefab", MonumentName.GasStation},
					{"assets/bundled/prefabs/autospawn/monument/roadside/supermarket_1.prefab", MonumentName.Supermarket},
					{"assets/bundled/prefabs/autospawn/monument/small/sphere_tank.prefab", MonumentName.Dome},
					{"assets/bundled/prefabs/autospawn/monument/medium/junkyard_1.prefab", MonumentName.Junkyard},
					{"assets/bundled/prefabs/autospawn/monument/medium/compound.prefab", MonumentName.Outpost},
					{"assets/bundled/prefabs/autospawn/monument/roadside/warehouse.prefab", MonumentName.MiningOutpost},
					{"assets/bundled/prefabs/autospawn/monument/large/airfield_1.prefab", MonumentName.Airfield},
				};

				public static MonumentName GetMonumentName(this MonumentInfo monument)
				{
					MonumentName name;
					var gameObject = monument.gameObject;
					while (gameObject.name.StartsWith("assets/") == false && gameObject.transform.parent != null){
						gameObject = gameObject.transform.parent.gameObject;
					}
					MonumentToName.TryGetValue(gameObject.name, out name);
					return name;
				}
			}
		}
		
	}
    
