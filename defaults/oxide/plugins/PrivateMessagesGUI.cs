using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Private Messages GUI", "Tricky", "1.0.2")]
    [Description("Provides a GUI output for Private Messages")]

    public class PrivateMessagesGUI : CovalencePlugin
    {
        #region Plugin References
        [PluginReference]
        private Plugin PrivateMessages;
        #endregion

        #region Hooks
        private void Loaded()
        {
            if (PrivateMessages == null)
            {
                PrintWarning("PrivateMessages could not be found! Get it at https://umod.org/plugins/private-messages");
            }
            else
            {
                if (PrivateMessages.Author != "MisterPixie")
                {
                    PrintWarning("This version of PrivateMessages isn't supported! Get it at https://umod.org/plugins/private-messages");
                }
                else if (PrivateMessages.Version < new VersionNumber(1, 0, 51))
                {
                    PrintWarning("Only Private Messages v1.0.51 or above is supported!");
                }
            }
        }

        private object OnPMProcessed(IPlayer sender, IPlayer target, string message)
        {
            UpdateGUI(sender, Lang("PMTo", sender.Id, target.Name, message));
            UpdateGUI(target, Lang("PMFrom", target.Id, sender.Name, message));

            PrivateMessages?.Call("AddPmHistory", sender.Id, target.Id);
            PrivateMessages?.Call("AddHistoryAndLogging", sender, target, message);
            return true;
        }

        private void UpdateGUI(IPlayer player, string text)
        {
            text = Formatter.ToPlaintext(text);

            player.Command("gametip.hidegametip");
            player.Command("gametip.showgametip", text);
            timer.In(5, () => player?.Command("gametip.hidegametip"));
        }
        #endregion

        #region Helpers
        private string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, PrivateMessages, id), args);
        #endregion
    }
}
