using System.Collections.Generic;   //dict
using System;   //String.
using Newtonsoft.Json;
using UnityEngine;


namespace Oxide.Plugins
{
    [Info("Who Knocks", "Hockeygel23", "0.0.7")]
    [Description("Get information messages on door knock")]
    class WhoKnocks : RustPlugin
    {
        #region Vars
        private const string AdminPermission = "WhoKnocks.admin";
        private const string KnockPermission = "WhoKnocks.knock";
        private const string MessagePermission = "WhoKnocks.message";
        private ConfigData config;
        private List<BasePlayer> Cooldowns = new List<BasePlayer>();
        private List<string> Colors = new List<string>() { "white", "yellow", "orange", "green", "red", "black", "blue", "blanc", "jaune", "vert", "rouge", "noir", "bleu" };
        const float CellSize = 146.3f;


        #endregion

        #region Init
        private void Init()
        {
            permission.RegisterPermission(KnockPermission, this);
            permission.RegisterPermission(AdminPermission, this);
            permission.RegisterPermission(MessagePermission, this);

        }
        #endregion

        #region Config

        private class ConfigData
        {
            [JsonProperty("Knocking Cooldown in seconds (0 = no cooldown)")]
            public float KnockCooldown;

            [JsonProperty("Display Door coordinates for owner")]
            public bool DoorCoordinates;

            [JsonProperty("Text color when the owner is offline")]
            public string OfflineColor;

            [JsonProperty("Text color when the owner is online")]
            public string OnlineColor;

            [JsonProperty("Display owner online/offline")]
            public bool OwnerDisplay;
        }

        private ConfigData GenerateConfig()
        {
            return new ConfigData
            {
                OfflineColor = "red",
                OnlineColor = "green",
                KnockCooldown = 0,
                DoorCoordinates =  true,
                OwnerDisplay = true,
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = GenerateConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        #region messages

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"KnocksMsg", "{0} is knocking your door at {1}."},
                {"KnocksMsgNoCoordinates", "{0} is knocking your door."},
                {"OwnerOnlineMsg", "Owner {0} is online and has been informed."},
                {"OwnerMsg", "Owner {0} has been informed."},
                {"OwnerDisplay", "You have set the knock display owner status to: {0}"},
                {"OwnerOfflineMsg", "Owner {0} is actually sleeping !"},
                {"TooFast", "{0}, You have a cooldown period of {1} seconds" },
                {"MissingArgs", "{0}, You are missing  arguments. Ex, /whoknocks cooldown 5" },
                {"NegativeCooldown", "{0}, The cooldown value has to be greater than 0" },
                {"WrongColor", "{0}, The color is incorrect. The options are: white, yellow, orange, green, red, black, and blue" },
                {"CorrectColor", "{0}, You have set {1} to {2}" },
                {"CorrectCooldown", "{0}, You have set {1} to {2} seconds" },
                {"IsAdmin", "{0}, The server admin, {1}, has knocked on your door!" },
                {"CoordOff", "{0}, door coordinates {1} be displayed!" }
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"KnocksMsg", "{0} est en train de tocquer à une de vos porte {1}."},
                {"KnocksMsgNoCoordinates", "{0} frappe à votre porte."},
                {"OwnerOnlineMsg", "L'habitant {0} est en ligne et a été informé."},
                {"OwnerMsg", "Le propriétaire {0} a été informé"},
                {"OwnerDisplay", "Vous avez défini le statut de propriétaire de l'affichage Knock sur: {0}" },
                {"OwnerOfflineMsg", "L'habitant {0} est en train de dormir !"},
                {"TooFast", "{0}, Vous avez un temps de recharge de {1} secondes" },
                {"MissingArgs", "{0}, Il vous manque des arguments. Ex, /whoknocks cooldown 5" },
                {"NegativeCooldown", "{0}, La valeur du temps de recharge doit être supérieure à 0" },
                {"WrongColor", "{0}, La couleur est incorrecte. Les options sont: blanc, jaune, orange, vert, rouge, noir et bleu" },
                {"CorrectColor", "{0}, Vous avez défini {1} à {2}" },
                {"CorrectCooldown", "{0}, Vous avez défini {1} à {2} secondes" },
                {"IsAdmin", "L'administrateur du serveur, {1}, a frappé à votre porte!" },
                {"CoordOff", "{0}, les coordonnées de la porte {1} affichées! " }
            }, this, "fr");
        }

        #endregion

        #region OxideHooks
        private void OnDoorKnocked(Door door, BasePlayer player)
        {
            if (door == null || player == null) return;
            BaseEntity DoorItem = door as BaseEntity;
            if (DoorItem == null) return;
            if (DoorItem.OwnerID == null || DoorItem.OwnerID.Equals( (object)null ) ) return;
            BasePlayer owner = null;
            foreach(var Player in BasePlayer.allPlayerList)
            {
                if (Player.userID != DoorItem.OwnerID) continue;
                owner = Player;
            }
            
            if (owner == null) return;
            if (player == owner) return;
            

            switch (permission.UserHasPermission(player.UserIDString, KnockPermission))
            {

                case true:
                    if (owner.IsConnected)
                    {
                        if (InCooldown(player))
                        {
                            //Puts(Cooldowns.Contains(player).ToString());
                            player.ChatMessage($"<color=red>{string.Format(lang.GetMessage("TooFast", this), player.displayName, config.KnockCooldown)}</color>");
                            return;
                        }
                        else
                        {
                            if (config.OwnerDisplay)
                            {
                                player.ChatMessage($"<color={config.OnlineColor}>{String.Format(lang.GetMessage("OwnerOnlineMsg", this), owner.displayName)}</color>");
                            }
                            else
                            {
                                player.ChatMessage($"<color={config.OnlineColor}>{String.Format(lang.GetMessage("OwnerMsg", this), owner.displayName)}</color>");
                            }
                            
                            Cooldown(player);
                        }
                    }
                    else
                    {
                        if (Cooldowns.Contains(player))
                        {
                            player.ChatMessage($"<color=red>{String.Format(lang.GetMessage("TooFast", this), player.displayName, config.KnockCooldown)}</color>");
                            return;
                        }
                        else
                        {
                            if (config.OwnerDisplay)
                            {
                                player.ChatMessage($"<color={config.OfflineColor}>{String.Format(lang.GetMessage("OwnerOfflineMsg", this), owner.displayName)}</color>");
                            }
                            else
                            {
                                player.ChatMessage($"<color={config.OnlineColor}>{String.Format(lang.GetMessage("OwnerMsg", this), owner.displayName)}</color>");
                            }
                            Cooldown(player);
                        }
                    }
                    break;    
            }

            switch (permission.UserHasPermission(owner.UserIDString, MessagePermission))
            {

                case true:
                    if (config.DoorCoordinates)
                    {
                        owner.ChatMessage($"<color={config.OnlineColor}> {String.Format(lang.GetMessage("KnocksMsg", this), player.displayName, GetDoorGridPosition(door.transform.position))} </color>");
                    }
                    else
                    {
                        owner.ChatMessage($"<color={config.OnlineColor}> {String.Format(lang.GetMessage("KnocksMsgNoCoordinates", this), player.displayName)} </color>");
                    }
                    break;
            }
        }
        #endregion

        #region Helpers

        private string GetDoorGridPosition(Vector3 position)
        {
            float GetMiddleWorldSize = World.Size / 2f;

            int BiggestGridSize = Mathf.FloorToInt(World.Size / CellSize) - 1;
            
            
            int GridZAxis = Mathf.Clamp(BiggestGridSize - Mathf.FloorToInt((position.z + GetMiddleWorldSize) / CellSize), 0, BiggestGridSize);

            int GridXAxis = Mathf.Clamp(Mathf.FloorToInt((position.x + GetMiddleWorldSize) / CellSize), 0, BiggestGridSize);

            string extraA = string.Empty;
            if (GridXAxis > 26)
            {
                extraA = $"{(char)('A' + (GridXAxis / 26 - 1))}";
            }

            return $"{extraA}{(char)('A' + GridXAxis % 26)}{GridZAxis.ToString()}";
        }

        private void Cooldown(BasePlayer player)
        {
            if(config.KnockCooldown == 0 || Cooldowns.Contains(player))
            {
                return;
            }
            
            Cooldowns.Add(player);
            TimerCooldown(player);
        }

        private bool InCooldown(BasePlayer player)
        {
            if (Cooldowns.Contains(player))
            {
                return true;
            }
            else return false;
        }
        private void TimerCooldown(BasePlayer player)
        {
            timer.Once(config.KnockCooldown, () =>
            {
                Cooldowns.Remove(player);
            });
        }
        #endregion

        #region ChatCommands
        [ChatCommand("whoknocks")]
        private void cmdChat(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                return;
            }

            if (args.IsEmpty())
            {
                player.ChatMessage($"<color=red>{String.Format(lang.GetMessage("MissingArgs", this), player.displayName)}</color>");
                return;
            }

            switch (args[0])
            {
                case "cooldown":
                    if (float.Parse(args[1]) < 0)
                    {
                        player.ChatMessage($"<color=red>{String.Format(lang.GetMessage("NegativeCooldown", this), player.displayName)}</color>");
                        return;
                    }
                    else
                    {
                        
                        config.KnockCooldown = float.Parse(args[1]);
                        player.ChatMessage($"<color=green>{String.Format(lang.GetMessage("CorrectCooldown", this), player.displayName, "Cooldown", config.KnockCooldown)}</color>");
                    }
                    break;
                case "offlinecolor":
                    if (!Colors.Contains(args[1]))
                    {
                        player.ChatMessage($"<color=red>{String.Format(lang.GetMessage("WrongColor", this), player.displayName)}</color>");
                        return;
                    }
                    switch (args[1])
                    {
                        case "black":
                        case "noir":
                            config.OfflineColor = "black";
                            break;

                        case "white":
                        case "blanc":
                            config.OfflineColor = "white";
                            break;

                        case "yellow":
                        case "jaune":
                            config.OfflineColor = "yellow";
                            break;

                        case "green":
                        case "vert":
                            config.OfflineColor = "green";
                            break;

                        case "blue":
                        case "bleu":
                            config.OfflineColor = "blue";
                            break;

                        case "orange":
                            config.OfflineColor = "orange";
                            break;


                        case "red":
                        case "rouge":
                            config.OfflineColor = "red";
                            break;
                    }
                    player.ChatMessage($"<color={config.OfflineColor}>{String.Format(lang.GetMessage("CorrectColor", this), player.displayName, "OfflineColor", config.OfflineColor)}</color>");
                    break;

                case "onlinecolor":
                    if (!Colors.Contains(args[1]))
                    {
                        player.ChatMessage($"<color=red>{String.Format(lang.GetMessage("WrongColor", this), player.displayName)}</color>");
                        return;
                    }
                    switch (args[1])
                    {
                        case "black":
                        case "noir":
                            config.OnlineColor = "black";
                            break;

                        case "white":
                        case "blanc":
                            config.OnlineColor = "white";
                            break;

                        case "yellow":
                        case "jaune":
                            config.OnlineColor = "yellow";
                            break;

                        case "green":
                        case "vert":
                            config.OnlineColor = "green";
                            break;

                        case "blue":
                        case "bleu":
                            config.OnlineColor = "blue";
                            break;

                        case "orange":
                            config.OnlineColor = "orange";
                            break;

                        case "red":
                        case "rouge":
                            config.OnlineColor = "red";
                            break;
                    }
                    player.ChatMessage($"<color={config.OnlineColor}>{String.Format(lang.GetMessage("CorrectColor", this), player.displayName, "OnlineColor", config.OnlineColor)}</color>");

                    break;
                case "coordinates":
                    switch (args[1])
                    {
                        case "on":
                            config.DoorCoordinates = true;
                            player.ChatMessage($"<color=green>{String.Format(lang.GetMessage("CoordOff", this), player.displayName, "will")}</color>");
                            break;

                        case "off":
                            config.DoorCoordinates = false;
                            player.ChatMessage($"<color=red>{String.Format(lang.GetMessage("CoordOff", this), player.displayName, "will not")}</color>");
                            break;
                    }
                    break;

                case "ownerstatus":
                    switch (args[1])
                    {
                        case "on":
                            config.OwnerDisplay = true;
                            player.ChatMessage($"<color=green>{String.Format(lang.GetMessage("OwnerDisplay", this), args[1])}</color>");
                            break;

                        case "off":
                            config.OwnerDisplay = false;
                            player.ChatMessage($"<color=green>{String.Format(lang.GetMessage("OwnerDisplay", this), args[1])}</color>");
                            break;
                    }
                    break;
            }
        }
        #endregion

        private void Unload()
        {
            SaveConfig();
        }
    }
}

