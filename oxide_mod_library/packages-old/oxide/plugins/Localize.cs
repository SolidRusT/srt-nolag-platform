//#define DEBUG
// Requires: TranslationAPI

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Localize", "Wulf", "1.0.1")]
    [Description("Generates localization files for other languages using existing plugin localization")]
    public class Localize : CovalencePlugin
    {
        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandLocalize"] = "localize",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["LocalizationExists"] = "Localization already exists for '{0}'",
                ["PluginNotFound"] = "Plugin '{0}' could not be found or is not loaded",
                ["SameLanguage"] = "Cannot localize to and from the same language ('{0}' and '{1}')",
                ["StringsToTranslate"] = "Translating {0} strings from '{1}'",
                ["StringsTranslated"] = "Translated {0} strings to '{1}'",
                ["TranslationFailed"] = "Translation failed else nothing to translate. Please try again later",
                ["UsageLocalize"] = "Usage: {0} <plugin name> <language code>"
            }, this);
        }

        #endregion Localization

        #region Initialization

        [PluginReference]
        private Plugin TranslationAPI;

        private const string permUse = "localize.use";

        private void OnServerInitialized()
        {
            AddLocalizedCommand(nameof(CommandLocalize));

            permission.RegisterPermission(permUse, this);
            MigratePermission("langgen.use", permUse);

            /*foreach (string language in config.DefaultLanguages) // TODO: Add configuration check to enable/disable this
            {
                // TODO: Generate default languages from configuration
            }*/
        }

        #endregion Initialization

        #region Localization

        // TODO: Add command to output localization for specified language, and see locale list

        private void CommandLocalize(IPlayer player, string command, string[] args)
        {
            // TODO: Add optional arg to force overwriting existing

            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 2)
            {
                Message(player, "CommandUsage", command);
                return;
            }

            string pluginName = args[0];
            Plugin[] pluginList = plugins.GetAll().ToArray();
            Plugin plugin = pluginList.FirstOrDefault(x => x.Name.ToLower().Equals(pluginName.ToLower()));
            if (plugin == null)
            {
                Message(player, "PluginNotFound", pluginName);
                return;
            }

            string[] pluginLocales = lang.GetLanguages(plugin);
            string langTo = args[1];

            bool overwrite = false;
            if (args.Length >= 3)
            {
                bool.TryParse(args[2], out overwrite);
            }

            string langFrom = pluginLocales.Contains(lang.GetServerLanguage()) ? lang.GetServerLanguage() : pluginLocales.Contains("en") ? "en" : pluginLocales[0];
            Dictionary<string, string> fromLang = lang.GetMessages(langFrom, plugin);

            if (pluginLocales.Contains(langTo) && !overwrite) // TODO: Configuration check for overwrite
            {
                Dictionary<string, string> toLang = lang.GetMessages(langTo, plugin);
                if (fromLang.Count == toLang.Count && fromLang.Keys.SequenceEqual(toLang.Keys))
                {
                    Message(player, "LocalizationExists", langTo);
                    return;
                }
            }

            if (langFrom == langTo)
            {
                Message(player, "SameLanguage", langTo, langFrom);
                return;
            }

            Message(player, "StringsToTranslate", fromLang.Count, langFrom);

            int processed = 0;
            Dictionary<string, string> newLang = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> pair in fromLang)
            {
                string original = pair.Value.Replace("><", "> <").Replace("<", "< ").Replace(">", " >").Replace("#", "/=").Replace(".", "/_"); // TODO: Fix some languages not translating when > is by (
                Action<string> callback = translation =>
                {
                    processed++;

                    if (translation == original)
                    {
                        if (processed == fromLang.Count)
                        {
                            Message(player, "TranslationFailed");
                        }
                        return;
                    }

                    translation = translation.Replace("><", "> <").Replace("< ", "<").Replace(" >", ">").Replace("/=", "#") // TODO: Find a better way to do this
                        .Replace("/ =", "#").Replace("# ", "#").Replace("/_", ".").Replace("/ _", ".").Replace("</ ", "</")
                        .Replace("{ ", "{").Replace(" }", "}").Replace(" &gt;", ">");
#if DEBUG
                    player.Reply($"Original: {pair.Value}");
                    player.Reply($"Translated: {translation}");
#endif
                    newLang.Add(pair.Key, translation);

                    if (processed == fromLang.Count)
                    {
                        lang.RegisterMessages(newLang, plugin, langTo);

                        Message(player, "StringsTranslated", processed, langTo);
#if DEBUG
                        player.Reply($"Available languages: {string.Join(", ", lang.GetLanguages(plugin))}");
#endif
                    }
                };

                TranslationAPI.Call("Translate", original, langTo, langFrom, callback);
            }
        }

        #endregion Localization

        #region Helpers

        private void AddLocalizedCommand(string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages)
                {
                    if (message.Key.Equals(command))
                    {
                        if (!string.IsNullOrEmpty(message.Value))
                        {
                            AddCovalenceCommand(message.Value, command);
                        }
                    }
                }
            }
        }

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        private void Message(IPlayer player, string textOrLang, params object[] args)
        {
            if (player.IsConnected)
            {
                string message = GetLang(textOrLang, player.Id, args);
                player.Reply(message != textOrLang ? message : textOrLang);
            }
        }

        private void MigratePermission(string oldPerm, string newPerm)
        {
            foreach (string groupName in permission.GetPermissionGroups(oldPerm))
            {
                permission.GrantGroupPermission(groupName, newPerm, null);
                permission.RevokeGroupPermission(groupName, oldPerm);
            }

            foreach (string playerId in permission.GetPermissionUsers(oldPerm))
            {
                permission.GrantUserPermission(Regex.Replace(playerId, "[^0-9]", ""), newPerm, null);
                permission.RevokeUserPermission(Regex.Replace(playerId, "[^0-9]", ""), oldPerm);
            }
        }

        #endregion Helpers
    }
}
