using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Universal Link", "Ryz0r", "2.0.0")]
    [Description("Universal Linking System for Ryz0rs Verification Website system.")]
    public class UniversalLink : CovalencePlugin
    {
        #region Configuration

        private Configuration _config;
        protected override void SaveConfig() => Config.WriteObject(_config);
        protected override void LoadDefaultConfig() => _config = new Configuration();

        private Dictionary<string, float> _commandCooldown = new Dictionary<string, float>();
        private Queue<IPlayer> _pQueue = new Queue<IPlayer>();
        
        private PluginData _data;
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        public class Configuration
        {
            [JsonProperty(PropertyName = "API URL")]
            public string URL = "http://url.com/{id(s)}";
            
            [JsonProperty(PropertyName = "Should auto execute syncing on a timer? (If not, it will happen on join and when command used. May help with server performance.)")]
            public bool AutoExecuteSyncing = true;
            
            [JsonProperty(PropertyName = "Should auto execute syncing on player connect? (If not, it will happen when command used. May help with server performance.)")]
            public bool AutoExecuteSyncingOnConnect = true;

            [JsonProperty(PropertyName = "Run Command(s) On Link")]
            public bool RunCommandsOnLink = false;

            [JsonProperty(PropertyName = "Commands To Run On Link (Use {id} for Steam ID, {name} for Steam Name)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] CommandsToRunOnLink = {"say {name} has linked!", "say {id} has linked."};
            
            [JsonProperty(PropertyName = "Run Command(s) On UnLink")]
            public bool RunCommandsOnUnLink = false;

            [JsonProperty(PropertyName = "Commands To Run On UnLink (Use {id} for Steam ID, {name} for Steam Name)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] CommandsToRunOnUnLink = {"say {name} has unlinked!", "say {id} has unlinked."};

            [JsonProperty(PropertyName = "Disable Console Messages")]
            public bool DisableConsoleMessages = false;
            
            [JsonProperty(PropertyName = "Use Nitro Feature")]
            public bool UseNitroFeature = true;
            
            [JsonProperty(PropertyName = "Use Steam Rewards Feature")]
            public bool UseSteamFeature = true;
            
            [JsonProperty(PropertyName = "Allow Steam Rewards If Not Linked")]
            public bool AllowSteamRewardsIfNotLinked = false;

            [JsonProperty(PropertyName = "Sync Names To Discord")]
            public bool SyncNamesToDiscord = true;
            
            [JsonProperty(PropertyName = "Link Website")]
            public string LinkSite = "https://link.rustylegends.com/";
            
            [JsonProperty(PropertyName = "Steam Group ID")]
            public string SteamGroupID = "GroupID (Found on Edit Group Page)";
            
            [JsonProperty(PropertyName = "OxideRoleNameAndDiscordID", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, long[]> OxideRoleNameAndDiscordID = new Dictionary<string, long[]>
            {
                {"OxideRoleName", new[] {1234567891929, 9876543213939}},
                {"OxideRoleName1", new[] {293849303393, 3939384304493}}
            };
            
            [JsonProperty(PropertyName = "User Group")]
            public string UserGroup = "linked";
            
            [JsonProperty(PropertyName = "User Group Nitro")]
            public string UserGroupNitro = "nitrobooster";
            
            [JsonProperty(PropertyName = "User Group Steam Rewards")]
            public string UserGroupSteam = "steamrewards";

            [JsonProperty(PropertyName = "Valid Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Commands = {"link", "discord", "auth", "linker"};

            [JsonProperty(PropertyName = "Refresh Cooldown")]
            public float RefreshCooldown = 30f;
            
            [JsonProperty(PropertyName = "Command Cooldown (Seconds)")]
            public float CommandCooldownSeconds = 60f;
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
        #endregion
        #region Data
        private void OnNewSave()
        {
            _data = new PluginData();
            SaveData();
        }

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }

        private class PluginData
        {
            [JsonProperty(PropertyName = "Linked")]
            public HashSet<string> LinkVerified = new HashSet<string>();
            
            [JsonProperty(PropertyName = "Nitro")]
            public HashSet<string> NitroBoosted = new HashSet<string>();
            
            [JsonProperty(PropertyName = "Steam Group")]
            public HashSet<string> SteamGroup = new HashSet<string>();
            
            [JsonProperty(PropertyName = "Previous Role Synced")]
            public HashSet<string> PreviousRoleSynced = new HashSet<string>();
        }
        
        #endregion
        #region Setup
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ConnectionError"] = "There was a connection error. Please try again, or contact an admin if this issue persists.",
                ["RemovedMsg"] = "You have been removed from the authenticated group because you unlinked your accounts.\nContact an admin if you did not unlink.",
                ["RemovedNitroMsg"] = "You have been removed from the nitro group because your boost was removed.\nContact an admin if this is an issue.",
                ["VerifyMsg"] = "Hey, make sure you verify at {0} to receive some awesome perks.",
                ["ThanksMsg"] = "You have successfully verified and linked your accounts. Thanks!",
                ["SteamNotVerified"] = "You are in the Steam Group, but aren't verified. Please verify at our website.",
                ["ThanksNitroMsg"] = "You have successfully boosted our discord. Thanks!",
                ["RemovedSteamMsg"] = "You have been removed from the Steam Rewards group for leaving the Steam Group.",
                ["ThanksSteamMsg"] = "You have successfully joined our Steam Group. Thanks!",
                ["NotClient"] = "This command is meant to be run from a client, not the console.",
                ["OnCooldown"] = "You are currently on a cooldown for using this command.",
                ["NoUsers"] = "UniversalLink: The response received does not contain any users."
            }, this); 
        }

        private void OnServerSave()
        {
            Puts("Universal Link is now saving data...");
            SaveData();
        }

        private void Loaded()
        {
            AddCovalenceCommand(_config.Commands, nameof(LinkCommand));
            LoadData();

            if (!permission.GroupExists(_config.UserGroup))
            {
                permission.CreateGroup(_config.UserGroup, "", 0);
            }
            
            if (!permission.GroupExists(_config.UserGroupNitro))
            {
                permission.CreateGroup(_config.UserGroupNitro, "", 0);
            }
            
            if (!permission.GroupExists(_config.UserGroupSteam))
            {
                permission.CreateGroup(_config.UserGroupSteam, "", 0);
            }

            if (_config.AutoExecuteSyncing)
            {
                timer.Every(_config.RefreshCooldown, HandleQueue);

                timer.Every(2.5f, () =>
                {
                    foreach (var player in players.Connected)
                    {
                        if (_pQueue.Contains(player)) return;
                        _pQueue.Enqueue(player);
                    }
                });
            }
        }
        #endregion
        #region Functions

        private void HandleQueue()
        {
            if (_pQueue.Count < 1)
            {
                return;
            }
            
            var amountToTake = _pQueue.Count < 15 ? _pQueue.Count() : Math.Ceiling(_pQueue.Count() / 15.0);
            
            if (!_config.DisableConsoleMessages)
            {
                Puts($"UniversalLink: Now updating verification and nitro status for {amountToTake} / {_pQueue.Count}");
            }

            var users = string.Join(",", _pQueue.DequeueChunk(Convert.ToInt32(amountToTake)).Select(p => p.Id));

            UpdateVerify(users);
            if (_config.UseNitroFeature) UpdateNitro(users);
            if (_config.UseSteamFeature) UpdateSteam(users);
        }
        
        private void LinkCommand(IPlayer player, string command, string[] args)
        {
            if (player.Object as BasePlayer == null)
            {
                player.Reply(lang.GetMessage("NotClient", this, player.Id));
                return;
            }

            if (_commandCooldown.ContainsKey(player.Id))
            {
                player.Reply(lang.GetMessage("OnCooldown", this, player.Id));
                return;
            }

            if (_data.LinkVerified.Contains(player.Id))
            {
                player.Reply(lang.GetMessage("ThanksMsg", this, player.Id));
                UpdateVerify(player.Id);
                if (_config.UseSteamFeature) UpdateSteam(player.Id);
                if (_config.UseNitroFeature) UpdateNitro(player.Id);
            }
            else
            {
                player.Reply(string.Format(lang.GetMessage("VerifyMsg", this, player.Id), _config.LinkSite));
                UpdateVerify(player.Id);
                if (_config.UseSteamFeature) UpdateSteam(player.Id);
                if (_config.UseNitroFeature) UpdateNitro(player.Id);
            }
            
            if (_data.PreviousRoleSynced.Add(player.Id))
            {
                CheckRoles(player.Id);
            }
            
            _commandCooldown.Add(player.Id, Time.time);
            
            timer.Once(_config.CommandCooldownSeconds, () =>
            {
                _commandCooldown.Remove(player.Id);
            });
        }
        
        private void CheckRoles(string playerID)
        {
            foreach (var pGroup in _config.OxideRoleNameAndDiscordID)
            {
                if (permission.UserHasGroup(playerID, pGroup.Key))
                {
                    foreach (var discordAdd in pGroup.Value){
                        UpdateRoles(playerID, discordAdd, "add");
                    }
                } else {
                    foreach (var discordDel in pGroup.Value){
                        UpdateRoles(playerID, discordDel, "remove");
                    }
                }
            }
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (!_config.AutoExecuteSyncingOnConnect) return;
            
            UpdateVerify(player.UserIDString);
            if (_config.UseSteamFeature) UpdateSteam(player.UserIDString);
            if (_config.UseNitroFeature) UpdateNitro(player.UserIDString);
            
            if (_data.PreviousRoleSynced.Add(player.UserIDString))
            {
                CheckRoles(player.UserIDString);
            }

            if (_config.SyncNamesToDiscord && permission.UserHasGroup(player.UserIDString, _config.UserGroup))
            {
                var url = _config.URL.Replace("{id(s)}", player.UserIDString);
                url = url.Replace("?action=steamChecks", "?action=updateSteam&name=" + player.displayName);
            
                webrequest.Enqueue(url, string.Empty, UpdateNameResponse, this);
            }
        }

        private void UpdateNameResponse(int respCode, string response)
        {
            if (respCode != 200 || string.IsNullOrEmpty(response))
            {
                Puts($"UniversalLink: Error Code: {respCode}\n\n{response}");
            }
        }
        
        private void UpdateVerify(string users)
        {
            webrequest.Enqueue(_config.URL.Replace("{id(s)}", users), string.Empty, CheckVerify, this);
        }
        
        private void UpdateNitro(string users)
        {
            var url = _config.URL.Replace("{id(s)}", users);
            url = url.Replace("?action=steamChecks", "?action=nitroChecks");
            
            webrequest.Enqueue(url, string.Empty, CheckNitro, this);
        }
        
        private void UpdateSteam(string users)
        {
            var url = _config.URL.Replace("{id(s)}", users);
            url = url.Replace("?action=steamChecks", "?action=groupChecks&group=" + _config.SteamGroupID);

            webrequest.Enqueue(url, string.Empty, CheckGroups, this);
        }
        
        private void CheckGroups(int respCode, string response)
        {
            if (respCode != 200 || string.IsNullOrEmpty(response))
            {
                Puts($"UniversalLink: Error Code: {respCode}\n\n{response}");
                return;
            }
            

            var users = JsonConvert.DeserializeObject<Dictionary<string, bool>>(response);
            if (users == null)
            {
                Puts(lang.GetMessage("NoUsers", this));
                return;
            }

            foreach (var userResponse in users)
            {
                if (userResponse.Value)
                {
                    if (permission.UserHasGroup(userResponse.Key, _config.UserGroup))
                    {

                        if (!_data.SteamGroup.Add(userResponse.Key)) continue;

                        var player = players.FindPlayerById(userResponse.Key);
                        if (player == null) continue;

                        player.AddToGroup(_config.UserGroupSteam);
                        if (player.IsConnected) player.Message(lang.GetMessage("ThanksSteamMsg", this, player.Id));
                    }
                    else
                    {
                        if (_config.AllowSteamRewardsIfNotLinked)
                        {
                            if (!_data.SteamGroup.Add(userResponse.Key)) continue;

                            var player = players.FindPlayerById(userResponse.Key);
                            if (player == null) continue;

                            player.AddToGroup(_config.UserGroupSteam);
                            if (player.IsConnected) player.Message(lang.GetMessage("ThanksSteamMsg", this, player.Id));
                        }
                        else
                        {
                            if (!_data.SteamGroup.Remove(userResponse.Key)) continue;
                    
                            var player = players.FindPlayerById(userResponse.Key);
                            if (player == null)
                                continue;

                            player.RemoveFromGroup(_config.UserGroupSteam);
                            if (player.IsConnected) player.Message(lang.GetMessage("RemovedSteamMsg", this, player.Id));
                        }
                    }
                }
                else
                {
                    if (!_data.SteamGroup.Remove(userResponse.Key)) continue;
                    
                    var player = players.FindPlayerById(userResponse.Key);
                    if (player == null)
                        continue;

                    player.RemoveFromGroup(_config.UserGroupSteam);
                    if (player.IsConnected) player.Message(lang.GetMessage("RemovedSteamMsg", this, player.Id));
                }
            }

            SaveData();
        }
        
        private void CheckNitro(int respCode, string response)
        {
            if (respCode != 200 || string.IsNullOrEmpty(response))
            {
                Puts($"UniversalLink: Error Code: {respCode}\n\n{response}");
                return;
            }
            

            var users = JsonConvert.DeserializeObject<Dictionary<string, bool>>(response);
            if (users == null)
            {
                Puts(lang.GetMessage("NoUsers", this));
                return;
            }

            foreach (var userResponse in users)
            {
                if (userResponse.Value)
                {
                    if (!_data.NitroBoosted.Add(userResponse.Key)) continue;
                    
                    var player = players.FindPlayerById(userResponse.Key);
                    if (player == null) continue;
                        
                    player.AddToGroup(_config.UserGroupNitro);

                    if (player.IsConnected) player.Message(lang.GetMessage("ThanksNitroMsg", this, player.Id));
                }
                else
                {
                    if (!_data.NitroBoosted.Remove(userResponse.Key)) continue;
                    
                    var player = players.FindPlayerById(userResponse.Key);
                    if (player == null)
                        continue;

                    player.RemoveFromGroup(_config.UserGroupNitro);
                    if (player.IsConnected) player.Message(lang.GetMessage("RemovedNitroMsg", this, player.Id));
                }
            }

            SaveData();
        }
        
        private void CheckVerify(int respCode, string response)
        {
            if (respCode != 200 || string.IsNullOrEmpty(response))
            {
                Puts($"UniversalLink: Error Code: {respCode}\n\n{response}");
                return;
            }

            var users = JsonConvert.DeserializeObject<Dictionary<string, bool>>(response);
            if (users == null)
            {
                Puts(lang.GetMessage("NoUsers", this));
                return;
            }

            foreach (var userResponse in users)
            {
                if (userResponse.Value)
                {
                    if (!_data.LinkVerified.Add(userResponse.Key)) continue;
                    
                    var player = players.FindPlayerById(userResponse.Key);
                    if (player == null) continue;
                        
                    player.AddToGroup(_config.UserGroup);
                    
                    if (_config.RunCommandsOnLink)
                    {
                        foreach (var cmd in _config.CommandsToRunOnLink)
                        {
                            var nCmd = new StringBuilder();
                            nCmd.Append(cmd.Replace("{id}", player.Id).Replace("{name}", player.Name));
                            server.Command(nCmd.ToString());
                        }
                    }

                    if (_data.SteamGroup.Contains(player.Id))
                    {
                        player.AddToGroup(player.Id);
                    }

                    if (player.IsConnected)
                        player.Message(lang.GetMessage("ThanksMsg", this, player.Id));
                }
                else
                {
                    if (!_data.LinkVerified.Remove(userResponse.Key)) continue;
                    
                    var player = players.FindPlayerById(userResponse.Key);
                    if (player == null)
                        continue;

                    player.RemoveFromGroup(_config.UserGroup);

                    if (_config.RunCommandsOnUnLink)
                    {
                        foreach (var cmd in _config.CommandsToRunOnUnLink)
                        {
                            var nCmd = new StringBuilder();
                            nCmd.Append(cmd.Replace("{id}", player.Id).Replace("{name}", player.Name));
                            server.Command(nCmd.ToString());
                        }
                    }

                    if (permission.UserHasGroup(player.Id, _config.UserGroupSteam))
                    {
                        player.RemoveFromGroup(_config.UserGroupSteam);
                        _data.SteamGroup.Remove(userResponse.Key);
                    }

                    if (player.IsConnected) player.Message(lang.GetMessage("RemovedMsg", this, player.Id));
                }
            }

            SaveData();
        }

        private void OnUserGroupAdded(string id, string groupName)
        {
            if (!permission.UserHasGroup(id, _config.UserGroup)) return;
            if (!_config.OxideRoleNameAndDiscordID.ContainsKey(groupName)) return;
            
            foreach (var discordR in _config.OxideRoleNameAndDiscordID[groupName])
            {
                UpdateRoles(id, discordR, "add");
            }
        }

        private void OnUserGroupRemoved(string id, string groupName)
        {
            if (!permission.UserHasGroup(id, _config.UserGroup)) return;
            if (!_config.OxideRoleNameAndDiscordID.ContainsKey(groupName)) return;
            
            foreach (var discordR in _config.OxideRoleNameAndDiscordID[groupName])
            {
                UpdateRoles(id, discordR, "remove");
            }
        }
        
        private void UpdateRoles(string id, long discordGroup, string mode)
        {
            var url = _config.URL.Replace("{id(s)}", id);
            url = url.Replace("?action=steamChecks", $"?action=update&role={discordGroup}&mode={mode}");

            webrequest.Enqueue(url, string.Empty, RoleResponse, this);
        }

        private void RoleResponse(int respCode, string response)
        {
            if (respCode != 200 || string.IsNullOrEmpty(response) || response.ToLower().Contains("error"))
            {
                Puts($"UniversalLink: Error Code: {respCode}\n\n{response}");
                return;
            }
        }
        #endregion
    }
    
    public static class QueueExtensions
    {
        public static IEnumerable<T> DequeueChunk<T>(this Queue<T> queue, int chunkSize) 
        {
            for (int i = 0; i < chunkSize && queue.Count > 0; i++)
            {
                yield return queue.Dequeue();
            }
        }
    }
}
