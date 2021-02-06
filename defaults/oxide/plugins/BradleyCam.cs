using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Bradley Cam", "bearr", "1.0.2")]
	[Description("Attaches a CCTV camera onto BradleyAPC.")]
	class BradleyCam : CovalencePlugin
	{
		#region Init Config
		private GameConfig config;
		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				config = Config.ReadObject<GameConfig>();
				if (config == null)
					Puts("Couldn't read config.");

				Config.WriteObject(config);
			}
			catch
			{
				LoadDefaultConfig();
			}
		}
		#endregion

		#region Vars
		string cctvPrefab = "assets/prefabs/deployable/cctvcamera/cctv.static.prefab";
		#endregion

		#region Messages
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["CCTVNull"] = "CCTV_RC object returned null.",
				["CCTVSpawn"] = "CCTV has been successfully spawned at {0} with cam name {1}!"
			}, this);
		}
		#endregion

		#region Hooks
		private void OnEntitySpawned(BradleyAPC bradley)
		{
			CreateBaseCamera(bradley);
		}
		#endregion

		#region Core
		private void CreateBaseCamera(BradleyAPC bradley)
		{
			CCTV_RC cctv = GameManager.server.CreateEntity(cctvPrefab, bradley.transform.position, bradley.transform.rotation) as CCTV_RC;
			if (cctv != null && bradley.gameObject != null)
			{
				cctv.SetParent(bradley);

				cctv.isStatic = false;
				cctv.transform.position = new Vector3(bradley.transform.position.x + config.camXOffset, bradley.transform.position.y + config.camYOffset, bradley.transform.position.z + config.camZOffset);	
				cctv.Spawn();
				cctv.UpdateHasPower(config.powerToSend, 1);
				cctv.UpdateIdentifier(config.camName);
				cctv.SendNetworkUpdate();

				Puts(lang.GetMessage("CCTVSpawn", this), cctv.transform.position.ToString(), config.camName);
			}
			else
			{
				PrintError(lang.GetMessage("CCTVNull", this));
			}
		}
		#endregion

		#region Config
		private class GameConfig
		{
			[JsonProperty("Cam X Offset")] public float camXOffset { get; set; }
			[JsonProperty("Cam Y Offset")] public float camYOffset { get; set; }
			[JsonProperty("Cam Z Offset")] public float camZOffset { get; set; }
			[JsonProperty("Camera Name")] public string camName { get; set; }
			[JsonProperty("Power To Send (anything below 5 will not work)")] public int powerToSend { get; set; }
		}

		private GameConfig GetDefaultConfig()
		{
			return new GameConfig
			{
				camXOffset = 0.0f,
				camYOffset = 3.0f,
				camZOffset = 0.0f,
				camName = "BRADLEYCAM",
				powerToSend = 5
			};
		}

		protected override void LoadDefaultConfig()
		{
			Config.WriteObject(GetDefaultConfig(), true);
		}
		#endregion
	}
}
