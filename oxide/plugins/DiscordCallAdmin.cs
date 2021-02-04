using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord;
using Oxide.Ext.Discord.DiscordObjects;
using ConVar;


namespace Oxide.Plugins
{
	[Info("Discord Call Admin", "evlad", "0.3.1")]
	[Description("Creates a live chat between a specific player and Admins through Discord")]

	internal class DiscordCallAdmin : CovalencePlugin
	{
		#region Variables

		[PluginReference]
		private Plugin DiscordCore;

		private DiscordClient _discordClient;
		private Guild _discordGuild;
		private User _discordBot;

		#endregion

		#region Config

		PluginConfig _config;

		private class PluginConfig
		{
			[JsonProperty(PropertyName = "CategoryID")]
			public string CategoryID = "";

			[JsonProperty(PropertyName = "ReplyCommand")]
			public string ReplyCommand = "r";

			[JsonProperty(PropertyName = "SteamProfileIcon")]
			public string SteamProfileIcon = "";

			[JsonProperty(PropertyName = "ShowAdminUsername")]
			public Boolean ShowAdminUsername = false;
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<PluginConfig>();
				if (_config == null) throw new Exception();
				SaveConfig();
			}
			catch
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
			}
		}

		protected override void LoadDefaultConfig() => _config = new PluginConfig();
		protected override void SaveConfig() => Config.WriteObject(_config);

		#endregion

		#region Localization
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["CallAdminNotAvailable"] = "/calladmin is not available yet.",
				["CallAdminSuccess"] = "[#00C851]Admins have been notified, they'll get in touch with you as fast as possible.[/#]",
				["CallAdminAlreadyCalled"] = "[#ff4444]You've already notified the admins, please wait until an admin responds.[/#]",
				["CallAdminMessageLayout"] = "[#9c0000]Admin Live Chat[/#] - <size=11>[#dadada]Reply by typing:[/#] [#bd8f8f]/{1} [message][/#]</size>\n{0}",
				["ReplyNotAvailable"] = "/{0} is not available yet.",
				["ReplyCommandUsage"] = "Usage: /{0} [message]",
				["ReplyNoLiveChatInProgress"] = "You have no live chat in progress.",
				["ReplyWaitForAdminResponse"] = "[#ff4444]Wait until an admin responds.[/#]",
				["ReplyMessageSent"] = "[#00C851]Your message has been sent to the admins[/#]\n{0}: {1}",
				["ChatClosed"] = "[#55aaff]An admin closed the live chat.[/#]",
				["NoPermission"] = "You do not have permission to use /calladmin."
			}, this);
		}

		private string GetTranslation(string key, string id = null, params object[] args) => covalence.FormatText(string.Format(lang.GetMessage(key, this, id), args));

		#endregion

		#region Initialization & Setup

		// private void OnServerInitialized()
		// {
		// 	DiscordCore?.Call("RegisterPluginForExtensionHooks", this);
		// }

		private void Init()
		{
			permission.RegisterPermission("discordcalladmin.use", this);

			if (_config.ReplyCommand.Length > 0) {
				AddCovalenceCommand(_config.ReplyCommand, "ReplyCommand");
			}
		}

		private void Loaded()
		{
			if (!IsDiscordReady())
				return;
			Setup();
		}

		private void OnDiscordCoreReady()
		{
			Setup();
		}

		private void Setup()
		{
			DiscordCore?.Call("RegisterPluginForExtensionHooks", this);
			_discordClient = DiscordCore?.Call<DiscordClient>("GetClient");
			_discordGuild = _discordClient.DiscordServer;
			_discordBot = DiscordCore?.Call<User>("GetBot");

			List<Channel> channels = (List<Channel>)DiscordCore?.Call("GetAllChannels");
			bool categoryExists = false;

			foreach (var channel in channels) {
				if (channel.id == _config.CategoryID && channel.type == ChannelType.GUILD_CATEGORY)
					categoryExists = true;
				if (channel.parent_id == _config.CategoryID)
					SubscribeToChannel(channel);
			}
			if (!categoryExists)
				throw new Exception("Category with ID: \"" + _config.CategoryID + "\" doesn't exist!");
		}

		#endregion

		#region Helpers

		[HookMethod("StartLiveChat")]
		public bool StartLiveChat(string playerID)
		{
			BasePlayer player = GetPlayerByID(playerID);
			if (!player) {
				PrintError("Player with ID \"" + playerID + "\" wasn't found!");
				return false;
			}

			String channelName = playerID + "_" + _discordBot.id;

			Channel newChannel = new Channel{
				name = channelName,
				type = ChannelType.GUILD_TEXT,
				parent_id = _config.CategoryID
			};
			Channel existingChannel = _discordGuild.channels.Find(c => c.name == channelName);
			if (existingChannel != null) {
				PrintError("Player \"" + playerID + "\" already has an opened chat!");
				return false;
			}

			_discordGuild.CreateGuildChannel(_discordClient, newChannel, (Channel channel) => {
				channel.CreateMessage(_discordClient, $"@here New chat opened!\nYou are now talking to `{player.displayName}`");
			});

			return true;
		}


		[HookMethod("StopLiveChat")]
		public void StopLiveChat(string playerID, string reason = null)
		{
			Channel channel = DiscordCore.Call<Channel>("GetChannel", playerID + "_" + _discordBot.id);
			if (channel == null)
				return;

			DeleteChannel(channel, reason);
		}

		private void DeleteChannel(Channel channel, string reason = null)
		{
			DiscordCore.Call("SendMessageToChannel", channel.id, $"Closing the chat in 5 seconds..." + (reason != null ? "\nReason: " + reason : ""));
			timer.Once(5f, () =>
			{
				channel.DeleteChannel(_discordClient);
			});
		}

		private void SubscribeToChannel(Channel channel)
		{
			string[] channelName = channel.name.Split('_');
			if (channelName.Length != 2 || channelName[1] != _discordBot.id)
				return;

			DiscordCore?.Call("SubscribeChannel", channel.id, this, new Func<Message, object>((message) => {
				// JObject userMessage = DiscordCore?.Call<JObject>("GetUserDiscordInfo", message.author.id);
				// if (userMessage == null) {
				// 	message.CreateReaction(_discordClient, "❌");
				// 	return null;
				// }

				if (message.content == "!close") {
					DeleteChannel(channel);
					return null;
				}

				string messageContent = "";
				if (_config.ShowAdminUsername) {
					messageContent += "[#c9c9c9]" + message.author.username + ": [/#]";
				}
				messageContent += message.content;
				if (!SendMessageToPlayerID(channelName[0], GetTranslation("CallAdminMessageLayout", channelName[0], messageContent, _config.ReplyCommand))) {
					DeleteChannel(channel, "User is not connected");
				}

				message.CreateReaction(_discordClient, "✅");

				return null;
			}));
		}

		private BasePlayer GetPlayerByID(string ID)
		{
			try {
				return BasePlayer.FindByID(Convert.ToUInt64(ID));
			} catch {
				return null;
			}
		}

		private bool SendMessageToPlayerID(string playerID, string message)
		{
			BasePlayer player = GetPlayerByID(playerID);
			if (player == null)
				return false;
			
			player.Command("chat.add", Chat.ChatChannel.Server, _config.SteamProfileIcon, message);
			return true;
		}

		private bool IsDiscordReady()
		{
			return DiscordCore?.Call<bool>("IsReady") ?? false;
		}

		#endregion

		#region Events

		private void Discord_ChannelCreate(Channel channel)
		{
			if (channel.parent_id == _config.CategoryID)
				SubscribeToChannel(channel);
		}

		private void Discord_ChannelDelete(Channel channel)
		{
			if (channel.parent_id == _config.CategoryID) {
				string[] channelName = channel.name.Split('_');
				if (channelName.Length != 1 && channelName[1] != _discordBot.id)
					return;

				SendMessageToPlayerID(channelName[0], GetTranslation("ChatClosed", channelName[0]));
			}
		}

		private void OnUserDisconnected(IPlayer player)
		{
			StopLiveChat(player.Id, "Player disconnected.");
		}

		#endregion

		#region Commands

		[Command("calladmin")]
		private void CallAdminCommand(IPlayer player, string command, string[] args)
		{
			if (!player.HasPermission("discordcalladmin.use"))
			{
				player.Reply(GetTranslation("NoPermission", player.Id));
				return;
			}
			if (!IsDiscordReady()) {
				player.Reply(GetTranslation("CallAdminNotAvailable", player.Id));
				return;
			}
			SendMessageToPlayerID(
				player.Id,
				GetTranslation(
					StartLiveChat(player.Id) ?
						"CallAdminSuccess" :
						"CallAdminAlreadyCalled", player.Id
				)
			);
		}

		private void ReplyCommand(IPlayer player, string command, string[] args)
		{
			if (!IsDiscordReady()) {
				player.Reply(GetTranslation("ReplyNotAvailable", player.Id, _config.ReplyCommand));
				return;
			}
			if (args.Length < 1) {
				player.Reply(GetTranslation("ReplyCommandUsage", player.Id, _config.ReplyCommand));
				return;
			}
			Channel replyChannel = DiscordCore?.Call<Channel>("GetChannel", player.Id + "_" + _discordBot.id);
			string sentMessage = string.Join(" ", args);

			if (replyChannel == null) {
				SendMessageToPlayerID(player.Id, GetTranslation("ReplyNoLiveChatInProgress", player.Id));
				return;
			}

			replyChannel.GetChannelMessages(_discordClient, messages =>
			{
				if (messages.Count < 2) {
					SendMessageToPlayerID(player.Id, GetTranslation("ReplyWaitForAdminResponse", player.Id));
					return;
				}

				DateTime now = DateTime.Now;
				DiscordCore?.Call("SendMessageToChannel", replyChannel.id, $"({now.Hour.ToString() + ":" + now.Minute.ToString()}) {player.Name}: {sentMessage}");
				SendMessageToPlayerID(player.Id, GetTranslation("ReplyMessageSent", player.Id, player.Name, sentMessage));
			});
		}

		#endregion
	}
}