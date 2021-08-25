//#define DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("Translation API", "Wulf", "2.0.0")]
    [Description("Plugin API for translating messages using free or paid translation services")]
    public class TranslationAPI : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        private class Configuration
        {
            [JsonProperty("API key (if required)")]
            public string ApiKey = string.Empty;

            [JsonProperty("Translation service")]
            public string Service = "google";

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            LogWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Initialization

        private static readonly Regex GoogleRegex = new Regex(@"\[\[\[""((?:\s|.)+?)"",""(?:\s|.)+?""");
        private static readonly Regex MicrosoftRegex = new Regex("\"(.*)\"");

        private void Init()
        {
            if (string.IsNullOrEmpty(config.ApiKey) && config.Service.ToLower() != "google")
            {
                LogWarning("Invalid API key, please check that it is set and valid");
            }
        }

        #endregion Initialization

        #region Translation API

        private string lastOutput;

        /// <summary>
        /// Translates text from one language to another language
        /// </summary>
        /// <param name="text"></param>
        /// <param name="to"></param>
        /// <param name="from"></param>
        /// <param name="callback"></param>
        private void Translate(string text, string to, string from = "auto", Action<string> callback = null)
        {
            string apiKey = config.ApiKey;
            string service = config.Service.ToLower();
            to = to.Contains("-") ? to.Split('-')[0].ToLower() : to.ToLower();
            from = from.Contains("-") ? from.Split('-')[0].ToLower() : from.ToLower();

            if (string.IsNullOrEmpty(config.ApiKey) && service != "google")
            {
                LogOutput("Invalid API key, please check that it is set and valid");
                return;
            }

            switch (service)
            {
                case "google":
                    {
                        // Reference: https://cloud.google.com/translate/docs/basic/quickstart

                        string url = string.IsNullOrEmpty(apiKey)
                            ? $"https://translate.googleapis.com/translate_a/single?client=gtx&tl={to}&sl={from}&dt=t&q={Uri.EscapeUriString(text)}"
                            : $"https://www.googleapis.com/language/translate/v2?key={apiKey}&target={to}&source={from}&q={Uri.EscapeUriString(text)}";

                        // TODO: Update to support newer Google Translate API
                        // https://translation.googleapis.com/language/translate/v2
                        // -H "Authorization: Bearer "$(gcloud auth application-default print-access-token)
                        // -H "Content-Type: application/json; charset=utf-8"
                        /*
                        {
                          "data": {
                          "translations": [{
                            "translatedText": "text here"
                          }]
                          }
                        }
                        */

                        webrequest.Enqueue(url, null, (code, response) => // TODO: {} for POST necessary?
                        {
                            if (code != 200 || string.IsNullOrEmpty(response) || response.Equals("[null,null,\"\"]"))
                            {
                                LogOutput($"No valid response received from {service.Titleize()}, try again later");
                                callback?.Invoke(text);
                                return;
                            }

                            Callback(code, response, text, callback);
                        }, this);
                        break;
                    }

                case "bing":
                case "microsoft":
                    {
                        // Reference: https://www.microsoft.com/en-us/translator/getstarted.aspx
                        // Supported language codes: https://msdn.microsoft.com/en-us/library/hh456380.aspx
                        // TODO: Implement the new access token method for Bing/Microsoft

                        webrequest.Enqueue($"http://api.microsofttranslator.com/V2/Ajax.svc/Detect?appId={apiKey}&text={Uri.EscapeUriString(text)}", null, (c, r) =>
                        {
                            if (string.IsNullOrEmpty(r) || r.Contains("<html>"))
                            {
                                LogOutput($"No valid response received from {service.Titleize()}, try again later");
                                callback?.Invoke(text);
                                return;
                            }

                            if (r.Contains("ArgumentException: Invalid appId"))
                            {
                                LogOutput("Invalid API key, please check that it is valid and try again");
                                callback?.Invoke(text);
                                return;
                            }

                            if (r.Contains("ArgumentOutOfRangeException: 'to' must be a valid language"))
                            {
                                LogOutput($"Invalid language code, please check that it is valid and try again (to: {to}, from: {from})");
                                callback?.Invoke(text);
                                return;
                            }

                            string url = $"http://api.microsofttranslator.com/V2/Ajax.svc/Translate?appId={apiKey}&to={to}&from={r}&text={Uri.EscapeUriString(text)}";
                            webrequest.Enqueue(url, null, (code, response) =>
                            {
                                if (string.IsNullOrEmpty(response) || response.Contains("<html>"))
                                {
                                    LogOutput($"No valid response received from {service.Humanize()}, try again later");
                                    callback?.Invoke(text);
                                    return;
                                }

                                if (response.Contains("ArgumentOutOfRangeException: 'from' must be a valid language"))
                                {
                                    LogOutput($"Invalid language code, please check that it is valid and try again (to: {to}, from: {from})");
                                    callback?.Invoke(text);
                                    return;
                                }

                                Callback(code, response, text, callback);
                            }, this);
                        }, this, RequestMethod.POST);
                        break;
                    }

                case "yandex":
                    {
                        // Reference (old): https://tech.yandex.com/keys/get/?service=trnsl
                        // Reference (new): https://cloud.yandex.com/docs/translate/operations/translate

                        webrequest.Enqueue($"https://translate.yandex.net/api/v1.5/tr.json/detect?key={apiKey}&hint={from}&text={Uri.EscapeUriString(text)}", null, (c, r) =>
                        {
                            if (string.IsNullOrEmpty(r))
                            {
                                LogOutput($"No valid response received from {service.Humanize()}, try again later");
                                callback?.Invoke(text);
                                return;
                            }

                            if (c == 502 || r.Contains("Invalid parameter: hint"))
                            {
                                LogOutput($"Invalid language code, please check that it is valid and try again (to: {to}, from: {from})");
                                callback?.Invoke(text);
                                return;
                            }

                            from = (string)JObject.Parse(r).GetValue("lang");
                            string url = $"https://translate.yandex.net/api/v1.5/tr.json/translate?key={apiKey}&lang={from}-{to}&text={Uri.EscapeUriString(text)}";
                            webrequest.Enqueue(url, null, (code, response) =>
                            {
                                if (string.IsNullOrEmpty(response))
                                {
                                    LogOutput($"No valid response received from {service.Humanize()}, try again later");
                                    callback?.Invoke(text);
                                    return;
                                }

                                if (c == 501 || c == 502 || response.Contains("The specified translation direction is not supported") || r.Contains("Invalid parameter: lang"))
                                {
                                    LogOutput($"Invalid language code, please check that it is valid and try again (to: {to}, from: {from})");
                                    callback?.Invoke(text);
                                    return;
                                }

                                Callback(code, response, text, callback);
                            }, this, RequestMethod.POST);
                        }, this);
                        break;
                    }

                default:
                    LogOutput($"Translation service '{service}' is not a valid setting");
                    break;
            }
        }

        private void Callback(int code, string response, string text, Action<string> callback = null)
        {
            if (code != 200 || string.IsNullOrEmpty(response))
            {
                LogOutput($"Translation failed! {config.Service.Titleize()} responded with: {response} ({code})");
                return;
            }

            string translated = null;
            string service = config.Service.ToLower();

            if (service == "google" && string.IsNullOrEmpty(config.ApiKey))
            {
                translated = GoogleRegex.Match(response).Groups[1].ToString();
            }
            else if (service == "google" && !string.IsNullOrEmpty(config.ApiKey))
            {
                translated = (string)JObject.Parse(response)["data"]["translations"]["translatedText"];
            }
            else if (service == "microsoft" || service.ToLower() == "bing")
            {
                translated = MicrosoftRegex.Match(response).Groups[1].ToString();
            }
            else if (service == "yandex")
            {
                translated = (string)JObject.Parse(response).GetValue("text").First;
            }
#if DEBUG
            LogWarning($"Using {service.Titleize()} to translate");
            LogWarning("----------------------------------------");
            LogWarning($"Original: {text}");
            LogWarning($"Translated: {translated}");
            LogWarning("----------------------------------------");
            if (translated == text)
            {
                LogWarning("Translated text is the same as original text");
            }
#endif
            callback?.Invoke(string.IsNullOrEmpty(translated) ? text : Regex.Unescape(translated));
        }

        private void LogOutput(string text)
        {
            if (text != lastOutput)
            {
                LogWarning(text);
                lastOutput = text;
            }
        }

        #endregion Translation API
    }
}
