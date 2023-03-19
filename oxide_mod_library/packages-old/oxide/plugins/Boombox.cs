using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Boombox", "Reheight/RyanJD", "1.0.0")]
    [Description("Allows players to easily set the url of a boombox.")]
    public class Boombox : RustPlugin
    {
        private const string UsePerm = "boombox.customurluse";
        private const string StationUsePerm = "boombox.stationsuse";
        private const string AdminUsePerm = "boombox.admin";

        private PropertyInfo _serverIpinfo = typeof(BoomBox).GetProperty("CurrentRadioIp");
        private PluginConfig _config;

        Regex limitedURLS;
        string presetStationsList;

        Dictionary<int, string> stationsNumbered = new Dictionary<int, string>();
        Dictionary<int, string> stationsNumberedName = new Dictionary<int, string>();

        private void SetBoomBoxServerIp(BoomBox box, string ip)
        {
            _serverIpinfo.SetValue(box, ip);
        }
        private void SetBoomBoxServerIp(DeployableBoomBox box, string ip)
        {
            SetBoomBoxServerIp(box.BoxController, ip);
        }

        private void Init()
        {
            permission.RegisterPermission(UsePerm, this);
            permission.RegisterPermission(StationUsePerm, this);
            permission.RegisterPermission(AdminUsePerm, this);
            AddCovalenceCommand("boombox", nameof(boomboxCMD));
            AddCovalenceCommand("stations", nameof(stationsCMD));
            AddCovalenceCommand("station", nameof(stationCMD));
            AddCovalenceCommand("addstation", nameof(addStationCMD));
            AddCovalenceCommand("removestation", nameof(removeStationCMD));

            _config = Config.ReadObject<PluginConfig>();
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new JsonException();
                }

                if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning($"PluginConfig file {Name}.json updated.");

                    SaveConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();

                PrintError("Config file contains an error and has been replaced with the default file.");
            }

            BuildVars(false);
        }

        private void BuildVars(bool renewing)
        {
            limitedURLS = new Regex($@"^(http||https):\/\/({ String.Join("|", _config.WhitelistedDomains) }).*", RegexOptions.Compiled);

            if (renewing)
            {
                stationsNumbered.Clear();
                stationsNumberedName.Clear();
            }

            StringBuilder stationsBuilder = new StringBuilder("The following stations we have are below!\n\n");

            int index = 1;
            foreach (var item in _config.PresetStations)
            {
                stationsBuilder.Append(index.ToString()).Append(". ").AppendLine(item.Key);

                stationsNumbered[index] = item.Value;
                stationsNumberedName[index] = item.Key;
                index++;
            }

            stationsBuilder.AppendLine().Append("Type /station <number> to play a station while holding or looking at a boombox!");

            presetStationsList = stationsBuilder.ToString();
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Whitelist", Order = 0)]
            public bool Whitelist { get; set; }

            [JsonProperty(PropertyName = "Whitelisted Domains", Order = 1)]
            public List<string> WhitelistedDomains { get; set; }

            [JsonProperty(PropertyName = "Boombox Deployed Require Power", Order = 2)]
            public bool BoomboxDeployedReqPower { get; set; }

            [JsonProperty(PropertyName = "Preset Stations", Order = 3)]
            public Dictionary<string, string> PresetStations { get; set; }

            [JsonProperty(PropertyName = "Deployed Boombox Never Decays", Order = 4)]
            public bool DeployedBoomboxImmortal { get; set; }

            [JsonProperty(PropertyName = "Handheld Boombox Never Breaks", Order = 5)]
            public bool HandheldBoomboxImmortal { get; set; }

            [JsonProperty(PropertyName = "Microphone Stand Never Breaks", Order = 6)]
            public bool MicrophoneStandImmortal { get; set; }

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                Whitelist = true,
                WhitelistedDomains = new List<string>()
                {
                    "stream.zeno.fm"
                },
                BoomboxDeployedReqPower = true,
                PresetStations = new Dictionary<string, string>()
                {
                    { "Country Hits", "http://crystalout.surfernetwork.com:8001/KXBZ_MP3" },
                    { "Todays Hits", "https://rfcmedia.streamguys1.com/MusicPulsePremium.mp3" },
                    { "Pop Hits", "https://rfcmedia.streamguys1.com/newpophitspremium.mp3" }
                },
                DeployedBoomboxImmortal = false,
                HandheldBoomboxImmortal = false,
                MicrophoneStandImmortal = false
            };
        }

        private void stationsCMD(IPlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, StationUsePerm) && !IsAdministrator(player.Object as BasePlayer))
            {
                player.Reply("You do not have permission to use this command!");
                return;
            }

            player.Reply(presetStationsList);
        }

        private void addStationCMD(IPlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, AdminUsePerm) && !IsAdministrator(player.Object as BasePlayer))
            {
                player.Reply("You do not have permission to use this command!");
                return;
            }

            if (args.Length < 2)
            {
                player.Reply("You must provide a valid radio station you'd like to add!" + "\n\n" + "<color=#ffcc00>Example:</color> /addstation \"Station Name\" \"URL\"");
                return;
            }

            if (_config.PresetStations.ContainsKey(args[1]))
            {
                player.Reply($"There is already a station by the name of: <color=#ffcc00>{args[0]}</color>");
                return;
            }

            _config.PresetStations.Add(args[0], args[1]);
            SaveConfig();
            BuildVars(true);

            player.Reply($"You have successfully added <color=#ffcc00>{args[0]}</color>!");
        }

        private void removeStationCMD(IPlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, AdminUsePerm) && !IsAdministrator(player.Object as BasePlayer))
            {
                player.Reply("You do not have permission to use this command!");
                return;
            }

            if (args.Length <= 0)
            {
                player.Reply("You must provide a valid radio station you'd like to remove!" + "\n\n" + "<color=#ffcc00>Example:</color> /addstation <ID>");
                return;
            }

            int index;
            string stationURL;

            if (args.Length <= 0 || !int.TryParse(args[0], out index) || !stationsNumbered.TryGetValue(index, out stationURL))
            {
                player.Reply("You must input a number that correlated to a station!");
                return;
            }

            string stationName = stationsNumberedName[index];

            _config.PresetStations.Remove(stationName);
            SaveConfig();
            BuildVars(true);

            player.Reply($"You have successfully removed station <color=#ffcc00>#{args[0]} ({stationName})</color>!");
        }

        private void stationCMD(IPlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, StationUsePerm) && !IsAdministrator(player.Object as BasePlayer))
            {
                player.Reply("You do not have permission to use this command!");
                return;
            }

            int index;
            string stationURL;

            if (args.Length <= 0 || !int.TryParse(args[0], out index) || !stationsNumbered.TryGetValue(index, out stationURL))
            {
                player.Reply("You must input a number that correlated to a station!");
                return;
            }

            bool stationSwtich = switchStation(player.Object as BasePlayer, stationURL);

            if (stationSwtich)
                player.Reply($"You are listening to station [#ffcc00]#{index} ({stationsNumberedName[index]})[/#]!");
        }

        private void boomboxCMD(IPlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, UsePerm) && !IsAdministrator(player.Object as BasePlayer))
            {
                player.Reply("You do not have permission to use this command!");
                return;
            }

            if (args.Length <= 0)
            {
                player.Reply("You must provide a URL to a audio stream!");
                return;
            }

            if (!args[0].StartsWith("http"))
                args[0] = $"https://{args[0]}";

            if (_config.Whitelist && !limitedURLS.IsMatch(args[0]) && !IsAdministrator(player.Object as BasePlayer))
            {
                player.Reply("You must use an accepted URL/Domain");
                return;
            }

            bool stationSwitch = switchStation(player.Object as BasePlayer, args[0]);

            if (stationSwitch)
                player.Reply($"You are now streaming audio from URL:\n[#ffcc00]{args[0]}[/#]");
        }

        private bool switchStation(BasePlayer player, string station)
        {
            Item heldItem = player.GetActiveItem();

            if (heldItem == null || heldItem.info.shortname != "fun.boomboxportable")
            {
                DeployableBoomBox boombox;
                if (!IsLookingAtBoomBox(player, out boombox))
                {
                    player.ChatMessage("You must be holding or looking at a boombox!");
                    return false;
                }

                if (!player.IsBuildingAuthed() && !IsAdministrator(player))
                {
                    player.ChatMessage("You must have building priviledge to change this boombox station!");
                    return false;
                }

                boombox.BoxController.ServerTogglePlay(false);
                SetBoomBoxServerIp(boombox, station);

                if (_config.BoomboxDeployedReqPower)
                {
                    if (boombox.ToEntity().currentEnergy >= boombox.PowerUsageWhilePlaying)
                    {
                        NextTick(() =>
                        {
                            boombox.BoxController.ServerTogglePlay(true);
                        });
                    }
                }
                else
                    boombox.BoxController.ServerTogglePlay(true);
            }
            else
            {
                BoomBox heldBoombox = heldItem.GetHeldEntity().GetComponent<BoomBox>();

                heldBoombox.ServerTogglePlay(false);
                SetBoomBoxServerIp(heldBoombox, station);
                heldBoombox.ServerTogglePlay(true);
            }

            return true;
        }

        void OnLoseCondition(Item item, ref float amount)
        {
            if (item == null)
                return;

            if (item.info.shortname == "fun.boomboxportable" && _config.HandheldBoomboxImmortal)
            {
                amount = 0f;
            }
        }

        void OnEntitySpawned(DeployableBoomBox boombox)
        {
            if (!_config.BoomboxDeployedReqPower)
            {
                NextTick(() =>
                {
                    boombox.SetFlag(IOEntity.Flag_HasPower, true);
                });
            }
        }

        object OnEntityTakeDamage(DeployableBoomBox boomBox, HitInfo info)
        {
            if (!_config.DeployedBoomboxImmortal)
                return null;

            if (info.damageTypes.Has(Rust.DamageType.Decay))
            {
                return true;
            }

            return null;
        }

        object OnEntityTakeDamage(MicrophoneStand microphoneStand, HitInfo info)
        {
            if (!_config.MicrophoneStandImmortal)
                return null;

            if (info.damageTypes.Has(Rust.DamageType.Decay))
            {
                return true;
            }

            return null;
        }

        private bool IsAdministrator(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminUsePerm) && !player.IsAdmin)
                return false;

            return true;
        }

        private bool IsLookingAtBoomBox(BasePlayer player, out DeployableBoomBox boombox)
        {
            RaycastHit hit;
            boombox = null;

            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 5))
            {
                BaseEntity entity = hit.GetEntity();

                if (entity is DeployableBoomBox)
                    boombox = entity as DeployableBoomBox;
            }

            return boombox != null;
        }
    }
}
