using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Admin Chat", "LaserHydra", "2.0.4")]
    [Description("Allows admins to write in an admin-only chatroom")]
    internal class AdminChat : CovalencePlugin
    {
        private const string Permission = "adminchat.use";

        private Configuration _config;
        private List<string> _enabledUserIds = new List<string>();

        #region Hooks

        private object OnUserChat(IPlayer player, string message)
        {
            if (!player.HasPermission(Permission))
                return null;

            if (message.StartsWith(_config.Prefix))
            {
                SendAdminMessage(player, message.Substring(_config.Prefix.Length));
                return true;
            }

            if (HasAdminChatEnabled(player))
            {
                SendAdminMessage(player, message);
                return true;
            }

            return null;
        }

        private void OnBetterChat(Dictionary<string, object> data)
        {
            var player = data["Player"] as IPlayer;
            var message = data["Message"] as string;

            if (!player.HasPermission(Permission))
                return;

            if (message.StartsWith(_config.Prefix) || HasAdminChatEnabled(player))
            {
                data["CancelOption"] = 1;
            }
        }

        #endregion

        #region Command

        [Command("adminchat"), Permission(Permission)]
        private void AdminChatTogggleCommand(IPlayer player, string command, string[] args)
        {
            if (_enabledUserIds.Contains(player.Id))
            {
                _enabledUserIds.Remove(player.Id);
                player.Reply(lang.GetMessage("Disabled Admin Chat", this, player.Id));
            }
            else
            {
                _enabledUserIds.Add(player.Id);
                player.Reply(lang.GetMessage("Enabled Admin Chat", this, player.Id));
            }
        }

        #endregion

        #region Helper

        private void SendAdminMessage(IPlayer sender, string message)
        {
            Puts($"{sender.Name}: {message}");

            string formattedMessage = _config.Format
                .Replace("{name}", sender.Name)
                .Replace("{message}", message.Trim());

            foreach (var player in players.Connected.Where(p => p.HasPermission(Permission)))
            {
#if RUST
                (player.Object as BasePlayer).SendConsoleCommand("chat.add", new object[] { (int) ConVar.Chat.ChatChannel.Global, sender.Id, covalence.FormatText(formattedMessage) });
#else
                player.Message(formattedMessage);
#endif
            }

            Interface.Call("OnAdminChat", sender, message, formattedMessage);
        }

        private bool HasAdminChatEnabled(IPlayer player) => _enabledUserIds.Contains(player.Id);

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Enabled Admin Chat"] = "You are now talking in admin chat.",
                ["Disabled Admin Chat"] = "You are no longer talking in admin chat."
            }, this);
        }

        #endregion

        #region Configuration

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        private class Configuration
        {
            [JsonProperty("Prefix (admin chat is used when message starts with this)")]
            public string Prefix { get; private set; } = "@";

            [JsonProperty("Format (how the message is formatted in chat)")]
            public string Format { get; private set; } = "[#red]Admin Chat[/#] [#grey]{name}[/#]: {message}";
        }

        #endregion
    }
}