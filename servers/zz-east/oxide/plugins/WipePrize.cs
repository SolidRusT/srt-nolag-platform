using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Wipe Prize", "Mevent", "1.0.6")]
	[Description("Rewards the first N players after Wipe")]
	public class WipePrize : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin ImageLibrary;

		private const string Layer = "UI.WipePrize";

		private bool _wasWipe = false;

		private static WipePrize _instance;
		
		private List<ulong> ConnectedPlayers = new List<ulong>();

		private List<ulong> WasGive = new List<ulong>();
		
		private enum ItemType
		{
			Item,
			Command,
			Plugin
		}
		
		#endregion

		#region Config

		private Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Amount of Players")]
			public int MaxCount = 100;

			[JsonProperty(PropertyName = "Command")]
			public string Command = "giveaward";

			[JsonProperty(PropertyName = "Enable logging to the console?")]
			public bool LogToConsole = true;

			[JsonProperty(PropertyName = "Enable logging to the file?")]
			public bool LogToFile = true;

			[JsonProperty(PropertyName = "Awards", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ItemCase> Awards = new List<ItemCase>()
			{
				new ItemCase()
				{
					Type = ItemType.Item,
					DisplayName = string.Empty,
					Shortname = "wood",
					Skin = 0,
					Amount = 2000,
					Command = string.Empty,
					PluginAward = new PluginAward()
				},
				new ItemCase()
				{
					Type = ItemType.Item,
					DisplayName = string.Empty,
					Shortname = "stones",
					Skin = 0,
					Amount = 3000,
					Command = string.Empty,
					PluginAward = new PluginAward()
				},
				new ItemCase()
				{
					Type = ItemType.Plugin,
					DisplayName = string.Empty,
					Shortname = string.Empty,
					Skin = 0,
					Amount = 1,
					Command = string.Empty,
					PluginAward = new PluginAward()
				},
				new ItemCase()
				{
					Type = ItemType.Command,
					DisplayName = string.Empty,
					Shortname = string.Empty,
					Skin = 0,
					Amount = 1,
					Command = "addgroup {steamid} vip 7d",
					PluginAward = new PluginAward()
				}

			};

			[JsonProperty(PropertyName = "Background")]
			public IPanel Background = new IPanel
			{
				AnchorMin = "1 0.5",
				AnchorMax = "1 0.5",
				OffsetMin = "-205 -30",
				OffsetMax = "-2 30",
				Color = "1 1 1 0.2",
				Image = string.Empty,
				isRaw = false
			};

			[JsonProperty(PropertyName = "Text")]
			public IText Text = new IText
			{
				AnchorMin = "0 0",
				AnchorMax = "1 1",
				OffsetMin = "0 0",
				OffsetMax = "0 25",
				Align = TextAnchor.MiddleCenter,
				FontSize = 14,
				Font = "robotocondensed-regular.ttf",
				Color = "1 1 1 1"
			};

			[JsonProperty(PropertyName = "Button")]
			public IButton Button = new IButton
			{
				AnchorMin = "0.5 0",
				AnchorMax = "0.5 0",
				OffsetMin = "-85 2.5",
				OffsetMax = "85 22.5",
				Align = TextAnchor.MiddleCenter,
				FontSize = 14,
				Font = "robotocondensed-regular.ttf",
				TextColor = "1 1 1 1",
				Color = "0.63 0.93 0.63 0.5"
			};
		}
		
		private class ItemCase
        {
            [JsonProperty(PropertyName = "Item type")] 
            [JsonConverter(typeof(StringEnumConverter))]
            public ItemType Type;

            [JsonProperty(PropertyName =
                "Display name (for the item) (if empty - standard)")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Shortname")]
            public string Shortname;

            [JsonProperty(PropertyName = "Skin")]
            public ulong Skin;

            [JsonProperty(PropertyName = "Amount (for item)")]
            public int Amount;

            [JsonProperty(PropertyName = "Command")]
            public string Command;

            [JsonProperty(PropertyName = "Plugin")]
            public PluginAward PluginAward;

            private void ToItem(BasePlayer player)
            {
                var newItem = ItemManager.CreateByName(Shortname, Amount, Skin);

                if (newItem == null)
                {
                    _instance?.PrintError($"Error creating item with shortname '{Shortname}'");
                    return;
                }

                if (!string.IsNullOrEmpty(DisplayName)) newItem.name = DisplayName;

                player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
            }

            private void ToCommand(BasePlayer player)
            {
                var command = Command.Replace("\n", "|")
                    .Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace("%username%",
                        player.displayName, StringComparison.OrdinalIgnoreCase);

                foreach (var check in command.Split('|'))
                {
                    _instance?.Server.Command(check);
                }
            }

            public void GetItem(BasePlayer player)
            {
                if (player == null) return;

                switch (Type)
                {
                    case ItemType.Command:
                    {
                        ToCommand(player);
                        break;
                    }
                    case ItemType.Plugin:
                    {
                        PluginAward?.ToPluginAward(player);
                        break;
                    }
                    case ItemType.Item:
                    {
                        ToItem(player);
                        break;
                    }
                }
            }
        }
		
		private class PluginAward
        {
            [JsonProperty("Hook to call")]
            public string hook = "Withdraw";

            [JsonProperty("Plugin name")]
            public string plugin = "Economics";

            [JsonProperty("Amount")] 
            public int amount = 0;

            [JsonProperty("(GameStores) Store ID in the service")]
            public string ShopID = "UNDEFINED";

            [JsonProperty("(GameStores) Server ID in the service")]
            public string ServerID = "UNDEFINED";

            [JsonProperty("(GameStores) Secret key")]
            public string SecretKey = "UNDEFINED";

            public void ToPluginAward(BasePlayer player)
            {
                var plug = _instance?.plugins.Find(plugin);
                if (plug == null)
                {
                    _instance?.PrintError($"Economy plugin '{plugin}' not found !!! ");
                    return;
                }

                switch (plugin)
                {
                    case "RustStore":
                    {
                        plug.Call(hook, player.userID, amount, new Action<string>(result =>
                        {
                            if (result == "SUCCESS")
                            {
                                _instance?.Log(givemoney, givemoney, player.displayName, player.UserIDString,
                                    amount, plug);
                                return;
                            }

                            Interface.Oxide.LogDebug($"The balance was not changed, error: {result}");
                        }));
                        break;
                    }
                    case "GameStoresRUST":
                    {
                        _instance?.webrequest.Enqueue(
                            $"https://gamestores.ru/api/?shop_id={ShopID}&secret={SecretKey}&server={ServerID}&action=moneys&type=plus&steam_id={player.UserIDString}&amount={amount}",
                            "", (code, response) =>
                            {
                                switch (code)
                                {
                                    case 0:
                                    {
                                        _instance?.PrintError("Api does not responded to a request");
                                        break;
                                    }
                                    case 200:
                                    {
                                        _instance?.Log(givemoney, givemoney, player.displayName,
                                            player.UserIDString, amount, plug);
                                        break;
                                    }
                                    case 404:
                                    {
                                        _instance?.PrintError("Please check your configuration! [404]");
                                        break;
                                    }
                                }
                            }, _instance);
                        break;
                    }
                    case "Economics":
                    {
                        plug.Call(hook, player.userID, (double) amount);
                        break;
                    }
                    default:
                    {
                        plug.Call(hook, player.userID, amount);
                        break;
                    }
                }
            }
        }
		
		private abstract class InterfacePosition
		{
			public string AnchorMin;

			public string AnchorMax;

			public string OffsetMin;

			public string OffsetMax;
		}

		private class IText : InterfacePosition
        {
            [JsonProperty(PropertyName = "Font Size")]
            public int FontSize;

            [JsonProperty(PropertyName = "Font")]
            public string Font;

            [JsonProperty(PropertyName = "Align")] 
            [JsonConverter(typeof(StringEnumConverter))]
            public TextAnchor Align;

            [JsonProperty(PropertyName = "Text Color")]
            public string Color;

            public void Get(ref CuiElementContainer container, string parent = "Hud", string name = null, string text ="")
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                container.Add(new CuiLabel
                {
                    RectTransform =
                        {AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax},
                    Text = {Text = text, Align = Align, FontSize = FontSize, Color = Color,
                        Font = Font}
                }, parent, name);
            }
        }

        private class IButton : InterfacePosition
        {
            [JsonProperty(PropertyName = "Color")]
            public string Color;

            [JsonProperty(PropertyName = "Text Color")]
            public string TextColor;

            [JsonProperty(PropertyName = "Font Size")]
            public int FontSize;

            [JsonProperty(PropertyName = "Font")]
            public string Font;
            
            [JsonProperty(PropertyName = "Align")]
            [JsonConverter(typeof(StringEnumConverter))]
            public TextAnchor Align;
            
            public void Get(ref CuiElementContainer container, string parent = "Hud", string name = null,
                string close = "", string cmd = "", string text = "")
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                container.Add(new CuiButton
                {
	                RectTransform =
		                {AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax},
                    Text =
                    {
                        Text = text,
                        Align = Align, 
                        FontSize = FontSize,
                        Color = TextColor,
                        Font = Font
                    },
                    Button = {Command = cmd, Color = Color, Close = close}
                }, parent, name + ".BTN");
            }
        }

        private class IPanel : InterfacePosition
        {
            [JsonProperty(PropertyName = "Image")]
            public string Image;

            [JsonProperty(PropertyName = "Color")] public string Color;

            [JsonProperty(PropertyName = "Preserving the color of the image?")]
            public bool isRaw;

            public void Get(ref CuiElementContainer container, string parent = "Hud", string name = null)
            {
	            if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                if (isRaw)
                    container.Add(new CuiElement
                    {
                        Name = name,
                        Parent = parent,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = !string.IsNullOrEmpty(Image)
                                    ? _instance.ImageLibrary.Call<string>("GetImage", Image)
                                    : null,
                                Color = Color
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin,
                                OffsetMax = OffsetMax
                            }
                        }
                    });
                else
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax
                        },
                        Image =
                        {
                            Png = !string.IsNullOrEmpty(Image)
                                ? _instance.ImageLibrary.Call<string>("GetImage", Image)
                                : null,
                            Color = Color
                        }
                    }, parent, name);
            }
        }

        protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config == null) throw new Exception();
				SaveConfig();
			}
			catch
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
			}
		}

		protected override void SaveConfig() => Config.WriteObject(_config);

		protected override void LoadDefaultConfig() => _config = new Configuration();

		#endregion

		#region Data

		private void SaveData()
		{
			Interface.Oxide.DataFileSystem.WriteObject(Name, ConnectedPlayers);
			Interface.Oxide.DataFileSystem.WriteObject(Name + "_Give", WasGive);
		}

		private void LoadData()
		{
			try
			{
				ConnectedPlayers = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>(Name);
				WasGive = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>(Name + "_Give");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (ConnectedPlayers == null) ConnectedPlayers = new List<ulong>();
			if (WasGive == null) WasGive = new List<ulong>();
		}

		#endregion
		
		#region Hooks

		private void OnServerInitialized()
		{
			_instance = this;
			
			LoadData();

			AddCovalenceCommand(_config.Command, nameof(GiveAwardCmd));

			foreach (var player in BasePlayer.activePlayerList)
				if (ConnectedPlayers.Contains(player.userID) && !WasGive.Contains(player.userID))
					MainUi(player);
		}

		private void Unload()
		{
			_instance = null;
			
			foreach (var player in BasePlayer.activePlayerList) 
				CuiHelper.DestroyUi(player, Layer);
		}
		
		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null || !_wasWipe) return;

			if (!WasGive.Contains(player.userID) && ConnectedPlayers.Count < _config.MaxCount)
			{
				if (!ConnectedPlayers.Contains(player.userID)) 
					ConnectedPlayers.Add(player.userID);
				
				MainUi(player);
				SaveData();
			}
		}

		private void OnNewSave()
		{
			LoadData();
			ConnectedPlayers.Clear();
			_wasWipe = true;
		}
		
		#endregion

		#region Commands

		private void GiveAwardCmd(IPlayer cov, string cmd, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (WasGive.Contains(player.userID))
			{
				SendReply(player, Msg(RecieveAward, player.UserIDString));
				return;
			}
			
			_config?.Awards?.ForEach(award => award?.GetItem(player));
			SendReply(player, Msg(GiveAward, player.UserIDString));
			WasGive.Add(player.userID);
			SaveData();
		}

		#endregion
		
		#region Interface

		private void MainUi(BasePlayer player)
		{
			if (!ConnectedPlayers.Contains(player.userID)) return;
			
			var container = new CuiElementContainer();

			_config.Background.Get(ref container, "Overlay", Layer);
			
			_config.Text.Get(ref container, Layer, text: Msg(UITitle, player.UserIDString, ConnectedPlayers.IndexOf(player.userID) + 1, _config.MaxCount));
			
			_config.Button.Get(ref container, Layer, text:Msg(UIBTN, player.UserIDString),
				cmd:$"{_config.Command}", close:Layer);
			
			CuiHelper.DestroyUi(player, Layer);
			CuiHelper.AddUi(player, container);
		}

		#endregion
		
		#region Lang

		private const string
			givemoney = "givemoney",
			UITitle = "UITitle",
			UIBTN = "UIBTN",
			GiveAward = "GiveAward",
			RecieveAward = "RecieveAward";
		
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
	            [givemoney] = "Player {0} ({1}) received {2} to the balance in {3}",
	            [UITitle] = "You are {0} from {1}\nSo you get the reward.",
	            [UIBTN] = "Give award",
	            [GiveAward] = "Congratulations! You received an award!",
	            [RecieveAward] = "You've already received your reward!"
            }, this);
        }

        private string Msg(string key, string userid = null, params object[] obj) =>
            string.Format(lang.GetMessage(key, this, userid), obj);

        #endregion
		
		#region Log

		private void Log(string filename, string key, params object[] obj)
		{
			var text = Msg(key, null, obj);
			if (_config.LogToConsole)
			{
				Puts(text);
			}

			if (_config.LogToFile)
			{
				LogToFile(filename, $"[{DateTime.Now}] {text}", this);
			}
		}

		#endregion
	}
}