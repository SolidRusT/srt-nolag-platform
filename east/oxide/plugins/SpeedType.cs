using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Oxide.Core.Configuration;
using Oxide.Core;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
	[Info("Speed Type", "TMafono", "1.1.5")]
    [Description("Quickly type randomly generated words to win a prize")]
    class SpeedType : RustPlugin
    {
		#region Variables
		private const string SpeedTypeAdmin = "speedtype.admin";
		
		private bool EventActive = false;
		private string RandomWord = String.Empty;
		
		private bool StartTier2Event = false;
		private bool StartTier3Event = false;
		
		Timer EndEventTimer;
        Timer EventAutoTimer;
		
		private List<string> EventWords = new List<string> {"A","B","C","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z","0","1","2","3","4","5","6","7","8","9"};
		
		private readonly DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("SpeedType");

        private Dictionary<string, int> TierStates = new Dictionary<string, int>();
		#endregion Variables
		
		#region Configuration
		private static Configuration config;

		private class Configuration
        {
			[JsonProperty(PropertyName = "Enable Automatic Events")]
            public bool AutoEventEnabled = true;
			
			[JsonProperty(PropertyName = "Event Frequency (Run Event Every X Seconds)")]
            public float EventFrequency = 300f;
			
			[JsonProperty(PropertyName = "Event Length (Ends After X Seconds)")]
            public float EventLength = 60f;
			
			[JsonProperty(PropertyName = "Minimum number of players to start a event")]
			public int StartEventMinPlayers = 10;
			
			[JsonProperty(PropertyName = "Chat Icon (SteamID64)")]
            public ulong ChatIcon = 0;
			
			[JsonProperty(PropertyName = "Tier 1 Letters/Numbers count")]
            public int Tier1LetterCount = 6;
			
			[JsonProperty(PropertyName = "Enable Tier 2 Events")]
            public bool Tier2EventStatus = false;
			
			[JsonProperty(PropertyName = "Tier 2 Event Frequency (Every X events it will be a tier 2 event)")]
            public int Tier2Frequency = 10;
			
			[JsonProperty(PropertyName = "Tier 2 Letters/Numbers count")]
            public int Tier2LetterCount = 10;
			
			[JsonProperty(PropertyName = "Enable Tier 3 Events")]
            public bool Tier3EventStatus = false;
			
			[JsonProperty(PropertyName = "Tier 3 Event Frequency (Every X events it will be a tier 3 event)")]
            public int Tier3Frequency = 100;
			
			[JsonProperty(PropertyName = "Tier 3 Letters/Numbers count")]
            public int Tier3LetterCount = 14;
			
			[JsonProperty(PropertyName = "Tier 1 Loot (Item Shortname | Item Ammount)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Dictionary<string, int>> EventT1LootTable = new List<Dictionary<string, int>>
            {
				new Dictionary<string, int>
				{
					{"stones", 100},
					{"wood", 100}
				},
				new Dictionary<string, int>
				{
					{"bandage", 100}
				}
            };
			
			[JsonProperty(PropertyName = "Tier 2 Loot (Item Shortname | Item Ammount)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<Dictionary<string, int>> EventT2LootTable = new List<Dictionary<string, int>>
            {
				new Dictionary<string, int>
				{
					{"metal.fragments", 50},
					{"metal.refined", 20}
				},
				new Dictionary<string, int>
				{
					{"leather", 60},
					{"cloth", 40}
				}
            };

			[JsonProperty(PropertyName = "Tier 3 Loot (Item Shortname | Item Ammount)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<Dictionary<string, int>> EventT3LootTable = new List<Dictionary<string, int>>
            {
				new Dictionary<string, int>
				{
					{"explosive.timed", 1}
				},
				new Dictionary<string, int>
				{
					{"rifle.ak", 1},
					{"ammo.rifle", 50}
				}
            };
			
			[JsonProperty(PropertyName = "Log Events to console")]
            public bool LogEvents = false;
        }
		
		protected override void LoadConfig()
        {
            base.LoadConfig();
            try {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            } catch {
                PrintError("Could not load a valid configuration file. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);
		#endregion Configuration
		
		#region Localization
        protected override void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EventStart"] = "<size=20><color=#1e90ff>Speed Type</color></size>\n<size=16><color=#{0}>Tier {1} Event</color></size>\n\nThe first person to type:\n<color=#33ccff>/guess {2}</color>\nWill win a prize!",
				["EventEnd"] = "<size=20><color=#1e90ff>Speed Type</color></size>\n<size=16><color=#ffa500>Event Over!</color></size>\n\nNo Winners",
				["EventEndWinner"] = "<size=20><color=#1e90ff>Speed Type</color></size>\n<size=16><color=#ffa500>Event Over!</color></size>\n\nThe Winner is:\n<color=#1e90ff>{0}</color>\nReward:{1}",
				["EventNotStarted"] = "<size=20><color=#1e90ff>Speed Type</color></size>\n\n<size=16><color=#ffa500>No Active Events!</color></size>",
				["EventStarted"] = "<size=20><color=#1e90ff>Speed Type</color></size>\n\n<size=16><color=#ffa500>Event already started</color></size>",
				["LogEventStart"] = "Speed Type Tier {0} Event Started",
				["LogEventEnd"] = "Speed Type Event Ended",
				["LogEventEndWinner"] = "Speed Type Event Winner: {0} | User Won: {1} x{2}",
				["WrongCode"] = "<size=20><color=#1e90ff>Speed Type</color></size>\n\n<size=16><color=#ffa500>Wrong Code!</color></size>",
				["WrongSyntax"] = "<size=20><color=#1e90ff>Speed Type</color></size>\n\n<size=16><color=#ffa500>Wrong Command Syntax</color></size>",
				["WrongPerm"] = "<size=20><color=#1e90ff>Speed Type</color></size>\n\n<size=16><color=#ffa500>No Permission!</color></size>",
				["RewardFormat"] = "\n<color=#FFD700>{0}</color> x{1}",
            }, this);
        }
        #endregion Localization
		
		#region Initialization
		private void Init()
        {
            permission.RegisterPermission(SpeedTypeAdmin, this);
			
			if(config.Tier2EventStatus || config.Tier3EventStatus) {
				TierStates = dataFile.ReadObject<Dictionary<string, int>>();
			
				if(TierStates.Count == 0) {
					TierStates = new Dictionary<string, int>
					{
						{"t2efrequency", config.Tier2Frequency},
						{"t3efrequency", config.Tier3Frequency}
					};
					dataFile.WriteObject(TierStates);
				}
			}
		}
		#endregion Initialization
		
		#region Hooks
		private void OnServerInitialized()
        {
			if (config.AutoEventEnabled) {
				EventAutoTimer = timer.Repeat(config.EventFrequency, 0, () =>
                {
                    StartSpeedTypeEvent();
                });
			}
		}
		#endregion Hooks
		
		void StartSpeedTypeEvent(bool consolecmd = false)
        {
            if (EventActive)
				return;
			
			if(BasePlayer.activePlayerList.Count <= config.StartEventMinPlayers && !consolecmd)
				return;
			
			if(!consolecmd)
				CheckTierStatus();
			
			EventActive = true;
			
			if(StartTier3Event) {
				RandomWord = SpeedEventWordGenerator(config.Tier3LetterCount);
				Broadcast(Lang("EventStart",null,"ff4500","3",RandomWord));
				if(config.LogEvents)
					Puts(Lang("LogEventStart",null,"3"));
			} else if (StartTier2Event) {
				RandomWord = SpeedEventWordGenerator(config.Tier2LetterCount);
				Broadcast(Lang("EventStart",null,"ffa500","2",RandomWord));
				if(config.LogEvents)
					Puts(Lang("LogEventStart",null,"2"));
			} else {
				RandomWord = SpeedEventWordGenerator(config.Tier1LetterCount);
				Broadcast(Lang("EventStart",null,"ffff00","1",RandomWord));
				if(config.LogEvents)
					Puts(Lang("LogEventStart",null,"1"));
			}
            
            EndEventTimer = timer.Once(config.EventLength, () =>
            {
				EndSpeedTypeEvent();
            });
        }
		
		private void EndSpeedTypeEvent(BasePlayer winner = null)
        {	
			EventActive = false;
			EndEventTimer.Destroy();
			
			if(winner != null){
				if(StartTier3Event) {
					var RandomList = RandomGen(config.EventT3LootTable.Count);
					GiveItem(winner,config.EventT3LootTable[RandomList]);
				} else if (StartTier2Event) {
					var RandomList = RandomGen(config.EventT2LootTable.Count);
					GiveItem(winner,config.EventT2LootTable[RandomList]);
				} else {
					var RandomList = RandomGen(config.EventT1LootTable.Count);
					GiveItem(winner,config.EventT1LootTable[RandomList]);
				}
				if(config.LogEvents)
					Puts(Lang("LogEventEnd"));
			} else {
				Broadcast(Lang("EventEnd"));
				if(config.LogEvents)
					Puts(Lang("LogEventEnd"));
			}
			
			StartTier2Event = false;
			StartTier3Event = false;
        }
		
		[ChatCommand("guess")]
        private void SpeedTypeCommand(BasePlayer player, string cmd, string[] args)
        {
			if (args.Length == 1) {
				if(args[0].ToUpper() == "END") {
					if(HasPermission(player)) {
						if (EventActive){
							EndSpeedTypeEvent();
						}
					}
				} else {
					if (!EventActive) {
						Message(player,Lang("EventNotStarted"));
						return;
					}
					
					if(args[0].ToUpper() == RandomWord) {
						EndSpeedTypeEvent(player);
					} else {
						Message(player,Lang("WrongCode"));
					}
				}
			} else if (args.Length == 2) {
				if(args[0].ToUpper() == "START") {
					if(HasPermission(player)) {
						if(args[1].ToUpper() == "T1") {
							if (EventActive){
								Message(player,Lang("EventStarted"));
								return;
							}
							
							StartSpeedTypeEvent(true);
						} else if(args[1].ToUpper() == "T2") {
							if (EventActive){
								Message(player,Lang("EventStarted"));
								return;
							}
							
							StartTier2Event = true;
							StartSpeedTypeEvent(true);
						} else if(args[1].ToUpper() == "T3") {
							if (EventActive){
								Message(player,Lang("EventStarted"));
								return;
							}
							
							StartTier3Event = true;
							StartSpeedTypeEvent(true);
						} else {
							Message(player,Lang("WrongSyntax"));
						}
					} else {
						Message(player,Lang("WrongPerm"));
					}
				}
			} else {
				Message(player,Lang("WrongSyntax"));
			}
		}
		
		#region Helpers
		private int RandomGen(int tableSize)
        {
            return Convert.ToInt32(Math.Round(Convert.ToDouble(Random.Range(Convert.ToSingle(0), Convert.ToSingle(tableSize-1)))));
        }
		
		
		private void GiveItem(BasePlayer player, Dictionary<string, int> selectedList)
        {
			string ItemReward = String.Empty;
			
			foreach(var items in selectedList)
			{
				Item item = ItemManager.Create(FindItem(items.Key));
				if (item == null) {
					return;
				}

				item.amount = items.Value;

				ItemContainer itemContainer = player.inventory.containerMain;

				if (!player.inventory.GiveItem(item, itemContainer)) {
					item.Remove();
					return;
				}

				var itemName = item.info.displayName.english;
				player.Command("note.inv", item.info.itemid, items.Value);
				ItemReward += Lang("RewardFormat",null,itemName,items.Value);
				
				if(config.LogEvents)
					Puts(Lang("LogEventEndWinner",null,player.displayName,itemName,items.Value));
			}
			
			Broadcast(Lang("EventEndWinner",null,player.displayName,ItemReward));
		}
		
		private void CheckTierStatus()
        {
			if(TierStates.Count != 0) {
				if(config.Tier2EventStatus) {
					if(TierStates["t2efrequency"] == 0){
						StartTier2Event = true;
						TierStates["t2efrequency"] = config.Tier2Frequency;
					} else {
						TierStates["t2efrequency"]--;
					}
				}
				
				if(config.Tier3EventStatus) {
					if(TierStates["t3efrequency"] == 0){
						StartTier2Event = false;
						StartTier3Event = true;
						TierStates["t3efrequency"] = config.Tier3Frequency;
					} else {
						TierStates["t3efrequency"]--;
					}
				}
				
				if(config.Tier2EventStatus || config.Tier3EventStatus) {
					dataFile.WriteObject(TierStates);
				}
			}
		}
		
		private ItemDefinition FindItem(string itemName)
        {
            ItemDefinition itemDef = ItemManager.FindItemDefinition(itemName.ToLower());
            return itemDef;
        }
		
		private string SpeedEventWordGenerator(int wordcount)
		{
			var RandomGeneratedWord = String.Empty;
			
			for (var i = 0; i < wordcount; i++) {
				var randomletter = Convert.ToInt32(Math.Round(Convert.ToDouble(Random.Range(Convert.ToSingle(0), Convert.ToSingle(EventWords.Count-1)))));
				RandomGeneratedWord = RandomGeneratedWord + EventWords[randomletter];
            }
			
			return RandomGeneratedWord;
			
		}
        private void Broadcast(string message)
        {
            Server.Broadcast(message, config.ChatIcon);
        }

        private void Message(BasePlayer player, string message)
        {
            Player.Message(player, message, config.ChatIcon);
        }

        private bool HasPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, SpeedTypeAdmin);
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }
		#endregion Helpers
	}
}