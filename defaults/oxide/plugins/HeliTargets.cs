using Facepunch;
using Rust;
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("Heli Targets", "XavierB", "0.0.7")]
	[Description("Allows patrol helicopters to target scientists and murderers.")]
	
	public class HeliTargets : RustPlugin
    {

		static HeliTargets plugin;

		private bool ConfigChanged;
		
		
		// Confiig defaults
		int HeliLifeTimeMinutes = 15;
		float HeliBaseHealth = 10000.0f;
		float HeliSpeed = 30f;
		int NumRockets = 12;
		float ScanFrequencySeconds = 5;
		float TargetVisible = 100;
		float MaxTargetRange = 300;
		bool NotifyPlayers = true;
		bool TargetScientist = false;
		bool TargetMurderers = true;


		void LoadVariables()
		{
			HeliLifeTimeMinutes = Convert.ToInt32(GetConfig("Settings", "How long in minutes can PatrolHelicopter patrol?", "15"));
			HeliBaseHealth = Convert.ToSingle(GetConfig("Settings", "Max health when helicopter is spawned", "10000.0"));
			HeliSpeed = Convert.ToSingle(GetConfig("Settings", "Helicopter speed to fly at", "30"));
			ScanFrequencySeconds = Convert.ToSingle(GetConfig("Settings", "Interval in seconds at which to scan for targets", "5"));
			MaxTargetRange = Convert.ToSingle(GetConfig("Settings", "Max distance at which guns can shoot", "300"));
			TargetVisible = Convert.ToSingle(GetConfig("Settings", "Max distance at which victims become targets", "100"));
			TargetMurderers = Convert.ToBoolean(GetConfig("Settings","Can target murderers?", "true"));
			TargetScientist = Convert.ToBoolean(GetConfig("Settings","Can target scientists?", "false"));
			NumRockets = Convert.ToInt32(GetConfig("Settings", "Number of rockets available", "12"));
			NotifyPlayers = Convert.ToBoolean(GetConfig("Settings","Notify Players when Helicopter is inbound?", "true"));
			
			if (ConfigChanged)
			{
				PrintWarning(lang.GetMessage("configchange", this));
				SaveConfig();
			}
			else
			{
				ConfigChanged = false;
				return;
			}
		}
		
		#region Config Reader
		
		private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                ConfigChanged = true;
            }
            return value;
        }
		
		#endregion
		
		protected override void LoadDefaultConfig()
		{
			LoadVariables();
		}
		
		void Init()
		{
			LoadVariables();
			plugin = this;
		}

		void Unload()
        {
			foreach (var heli in UnityEngine.Object.FindObjectsOfType<BaseHelicopter>())
            {
				var helicopter = heli.GetComponent<HeliComponent>();
				if (helicopter)
				{
					helicopter.UnloadComponent();
				}
			}
			plugin = null;
		}
		
		void OnServerInitialized()
		{
			foreach (var heli in UnityEngine.Object.FindObjectsOfType<BaseHelicopter>())
            {
				heli.gameObject.AddComponent<HeliComponent>();
			}
		}

		#region Hooks
		private void OnEntitySpawned(BaseHelicopter heli)
		{
			heli.gameObject.AddComponent<HeliComponent>();
		}
		#endregion
		
		#region Behaviour
		
		class HeliComponent : FacepunchBehaviour
		{
			private BaseHelicopter heli;
			private PatrolHelicopterAI AI;
			private bool isFlying = true;
			private bool isRetiring = false;
			float timer;
			float timerAdd;
			
			void Awake()
			{
				heli = GetComponent<BaseHelicopter>();
				AI = heli.GetComponent<PatrolHelicopterAI>();
				heli.startHealth = plugin.HeliBaseHealth;
				AI.maxSpeed = Mathf.Clamp(plugin.HeliSpeed, 0.1f, 125);
				AI.numRocketsLeft = plugin.NumRockets;
				attachGuns(AI);
				timerAdd = (Time.realtimeSinceStartup + Convert.ToSingle(plugin.HeliLifeTimeMinutes * 60));
				InvokeRepeating("ScanForTargets", plugin.ScanFrequencySeconds, plugin.ScanFrequencySeconds);
			}
			
			void FixedUpdate()
			{
				timer = Time.realtimeSinceStartup;
				
				if (timer >= timerAdd && !isRetiring)
				{
					isRetiring = true;
				}
				if (isRetiring && isFlying)
				{
					CancelInvoke("ScanForTargets");
					isFlying = false;
					heliRetire(AI);
				}
			}
			
			internal void ScanForTargets()
			{
					List<NPCPlayerApex> nearby = new List<NPCPlayerApex>();
					Vis.Entities(transform.position, plugin.TargetVisible, nearby);
					
					foreach (var player in nearby)
					{
						if (player is Scientist && !plugin.TargetScientist) continue;
						if (player is NPCMurderer && !plugin.TargetMurderers) continue;
						UpdateTargets(player);
					}
			}
			
			void UpdateTargets(BasePlayer Player)
			{
				AI._targetList.Add(new PatrolHelicopterAI.targetinfo((BaseEntity) Player, Player));
			}
			
			internal void attachGuns(PatrolHelicopterAI helicopter)
			{
				if (helicopter == null) return;
				var guns = new List<HelicopterTurret>();
				guns.Add(helicopter.leftGun);
				guns.Add(helicopter.rightGun);
				for (int i = 0; i < guns.Count; i++)
				{
					// Leave these as hardcoded for now
					var turret = guns[i];
					turret.fireRate = 0.125f;
					turret.timeBetweenBursts = 3f;
					turret.burstLength = 3f;
					turret.maxTargetRange = plugin.MaxTargetRange;
				}
			}
			
			internal void heliRetire(PatrolHelicopterAI helicopter)
			{
				AI.Retire();
			}

			public void UnloadComponent()
			{
				Destroy(this);
			}
			
			void OnDestroy()
			{
				CancelInvoke("ScanForTargets");
			}
		}
		
		#endregion
		
		#region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				{"saving", "Saving..."},
				{"configchange", "Config has changed."},
				{"failedload", "Falied to load, creating new config."},
            }, this, "en");
        }
        #endregion
	}
	
}