using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Text;

namespace Oxide.Plugins
{
    [Info("CapsNoCaps", "LaserHydra", "2.0.0")]
    class CapsNoCaps : CovalencePlugin
    {
        private Configuration _config;

        private const string IgnorePermission = "capsnocaps.ignore";

        private void Init()
        {
            permission.RegisterPermission(IgnorePermission, this);
        }

        private void OnBetterChat(Dictionary<string, object> data)
        {
            var player = data["Player"] as IPlayer;
            var message = data["Message"] as string;

            if (CanWriteCaps(player))
                return;

            data["Message"] = ReplaceCapitalWords(message, _config.CapitalLetterPercentageThreshold, _config.WordLengthThreshold);
        }

        private static string ReplaceCapitalWords(string text, float captialLetterPercentageThreshold = 0.4f, int lengthThreshold = 1)
        {
            StringBuilder stringBuilder = new StringBuilder();

            int wordBeginning = 0,
                upperCaseLetters = 0,
                lowerCaseLetters = 0;

            for (int i = 0; i < text.Length; i++)
            {
                bool isLastChar = i == text.Length - 1;

                char c = text[i];

                if (char.IsUpper(c))
                    upperCaseLetters++;
                else if (char.IsLower(c))
                    lowerCaseLetters++;

                if (char.IsWhiteSpace(c) || isLastChar)
                {
                    int length = i - wordBeginning + 1; // +1 to include current character

                    int lengthWithoutWhitespace = isLastChar // In case this is NOT the last character, the current character is a whitespace which we subtract.
                        ? length
                        : length - 1;

                    if (lengthWithoutWhitespace > lengthThreshold && (float) upperCaseLetters / (lowerCaseLetters + upperCaseLetters) > captialLetterPercentageThreshold)
                        stringBuilder.Append(text.Substring(wordBeginning, length).ToLower());
                    else
                        stringBuilder.Append(text.Substring(wordBeginning, length));

                    wordBeginning = i + 1;
                    upperCaseLetters = 0;
                    lowerCaseLetters = 0;
                }
            }

            return stringBuilder.ToString();
        }

        private bool CanWriteCaps(IPlayer player) => player.HasPermission(IgnorePermission);

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
            [JsonProperty("Word Length Threshold")]
            public int WordLengthThreshold { get; private set; } = 1;

            [JsonProperty("Capital Letter Percentage Threshold (between 0 and 1)")]
            public float CapitalLetterPercentageThreshold { get; private set; } = 0.4f;
        }

        #endregion
    }
}
