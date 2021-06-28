using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Optimal Burn", "Thisha", "0.9.4")]
    [Description("Splitter according to ultimate furnace guide")]
    public class OptimalBurn : RustPlugin
    {
        #region variables
        private const string permUse = "optimalburn.use";
        
        private const int sulfurOre = -1157596551;  //sulfur.ore
        private const int metalOre = -4031221; //metal.ore
        private const int hqOre = -1982036270; //HQ
        private const int wood = -151838493; //wood

        private const string largefurnace = "furnace.large";
        private const string smallfurnace = "furnace";

        private enum furnaceType {invalid, large, small };
        
        private const int sulfurLarge1K = 5106;
        private const int sulfurLarge2K = 7985;
        private const int sulfurLarge2HK = 8759;
        
        private const int metalLarge1K = 2804;
        private const int metalLarge2K = 4772;
        private const int metalLarge3K = 6087;
        private const int metalLarge4K = 6879;
        private const int metalLarge5K = 7314;
        private const int metalSmall = 600;
        private const int sulfurSmall = 900;

        private List<Item> items = new List<Item>();
        private readonly Dictionary<ulong, string> openUis = new Dictionary<ulong, string>();
        private Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();

        private class PlayerData
        {
            public int sulfurQtyFurnace;
            public int metalQtyFurnace;
            public int hqQtyFurnace;
            public int woodQtyFurnace;
            public int oreQtyFurnace;
            
            public int sulfurQtyPlayer;
            public int metalQtyPlayer;
            public int hqQtyPlayer;
            public int oreQtyPlayer;
            public int woodQQtyPlayer;

            public int ore;
            public int oreQty;
            public int woodQty;

            public int woodToAdd;
            public int oreToAdd;
            public bool divideit = false;
            public bool autoSplit = false;

            public furnaceType currFurnace = furnaceType.invalid;
        }
        #endregion variables

        #region localization
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Invalid source"] = "Invalid furnace",
                ["Multiple ores"] = "You have multiple ores."  + '\n' + "You must either add sulfur, metal or HQM to define what you want to burn",
                ["Nothing to burn"] = "There is nothing to burn",
                ["Invalid items"] = "Please remove invalid items first",
                ["No wood"] = "You don't have wood",
                ["Cannot optimize"] = "Cannot define an optimal burn",
                ["In use"] = "The furnace is in use"
            }, this);
        }
        #endregion localization

        #region hooks
        private void Init()
        {
            permission.RegisterPermission(permUse, this);
        }

        private void Unload()
        {
            foreach (var kv in openUis.ToDictionary(kv => kv.Key, kv => kv.Value))
            {
                BasePlayer player = BasePlayer.FindByID(kv.Key);
                DestroyUI(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            DestroyUI(player);
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            DestroyUI(player);
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            BaseOven oven = entity as BaseOven;

            if (oven == null || !HasPermission(player))
                return;

            if (playerData.ContainsKey(player.userID))
                playerData.Remove(player.userID);

            PlayerData data = new PlayerData();
            playerData[player.userID] = data;

            playerData[player.userID].currFurnace = furnaceType.invalid;

            if (entity.ShortPrefabName.Equals(largefurnace))
                playerData[player.userID].currFurnace = furnaceType.large;
            else
            {
                if (entity.ShortPrefabName.Equals(smallfurnace))
                    playerData[player.userID].currFurnace = furnaceType.small;
            }

            if (playerData[player.userID].currFurnace == furnaceType.invalid)
                return;

            CreateUi(player, oven);
        }
        
        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            BaseOven oven = entity as BaseOven;

            if (oven == null || ((!(entity.ShortPrefabName.Equals(largefurnace))) && (!(entity.ShortPrefabName.Equals(smallfurnace)))))
                return;

            DestroyUI(player);
        }
        
        private void OnEntityKill(BaseNetworkable networkable)
        {
            BaseOven oven = networkable as BaseOven;

            if (oven != null)
                DestroyOvenUI(oven);
        }

        object CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            if (!(entity.ShortPrefabName.Equals(largefurnace)) && !(entity.ShortPrefabName.Equals(smallfurnace)))
                return null;

            if (openUis.ContainsKey(player.userID))
            {
                player.ChatMessage(Lang("In use", player.UserIDString));
                return false;
            }

            return null; 
        }
        #endregion hooks

        #region commands
        [ConsoleCommand("optimalburn.split")]
        private void OptimizeLargefurnace(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (arg.Connection == null || player == null)
                return;

            BaseOven furnace = player.inventory.loot?.entitySource as BaseOven;

            if (!playerData.ContainsKey(player.userID))
            {
                return;
            }

            if (furnace == null || (playerData[player.userID].currFurnace == furnaceType.invalid))
            {
                player.ChatMessage(Lang("Invalid source", player.UserIDString));
                return;
            }

            Optimizefurnace(player, furnace);
        }
        #endregion commands

        #region methods
        bool HasInvalidItems(BaseOven furnace)
        {
            for (int i = 0; i <= 17; i++)
            {
                Item item = furnace.inventory.GetSlot(i);
                if (item != null)
                {
                    if ((item.info.itemid != metalOre) && (item.info.itemid != sulfurOre) && (item.info.itemid != hqOre) && (item.info.itemid != wood))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool BurnableDefined(BasePlayer player)
        {
            //no different ores allowed in furnace
            int types = 0;
            
            if (playerData[player.userID].sulfurQtyFurnace > 0)
                types += 1;
                
            if (playerData[player.userID].metalQtyFurnace > 0)
                types += 1;
            
            if (playerData[player.userID].hqQtyFurnace > 0)
                types += 1;
            
            if (types > 1)
            {
                player.ChatMessage(Lang("Multiple ores", player.UserIDString));
                return false;
            }

            if ((playerData[player.userID].sulfurQtyFurnace == 0) && (playerData[player.userID].metalQtyFurnace == 0) && (playerData[player.userID].hqQtyFurnace == 0))
            {
                //if only containing one of both, auto define
                if ((playerData[player.userID].sulfurQtyPlayer > 0) && (playerData[player.userID].metalQtyPlayer == 0) && (playerData[player.userID].hqQtyFurnace == 0))
                {
                    playerData[player.userID].ore = sulfurOre;
                }
                else
                {
                    if ((playerData[player.userID].sulfurQtyPlayer == 0) && (playerData[player.userID].metalQtyPlayer > 0) && (playerData[player.userID].hqQtyPlayer == 0))
                    {
                        playerData[player.userID].ore = metalOre;
                    }
                    else
                    {
                        if ((playerData[player.userID].sulfurQtyPlayer == 0) && (playerData[player.userID].metalQtyPlayer == 0) && (playerData[player.userID].hqQtyPlayer > 0))
                        {
                            playerData[player.userID].ore = hqOre;
                        }
                        else
                        {
                            if ((playerData[player.userID].sulfurQtyPlayer == 0) && (playerData[player.userID].metalQtyPlayer == 0) && (playerData[player.userID].hqQtyPlayer == 0))
                            {
                                player.ChatMessage(Lang("Nothing to burn", player.UserIDString));
                                return false;
                            }
                            else
                            {
                                player.ChatMessage(Lang("Multiple ores", player.UserIDString));
                                return false;
                            }
                        }
                    }
                }
            }
            else
            {
                if (playerData[player.userID].sulfurQtyFurnace > 0) 
                    playerData[player.userID].ore = sulfurOre;
                else
                {
                    if (playerData[player.userID].metalQtyFurnace > 0)
                        playerData[player.userID].ore = metalOre;
                    else
                        playerData[player.userID].ore = hqOre;
                }
                    
            }
            return true;
        }

        void DefineQuantitesToUseLarge(BasePlayer player)
        {
            playerData[player.userID].autoSplit = false;
            playerData[player.userID].divideit = false;
            playerData[player.userID].oreToAdd = 0;
            playerData[player.userID].woodToAdd = 0;

            if (playerData[player.userID].woodQtyFurnace > 0)
            {
                switch (playerData[player.userID].woodQtyFurnace)
                {
                    case 1000:
                        {
                            switch (playerData[player.userID].ore)
                            {
                                case sulfurOre:
                                    {
                                        if (playerData[player.userID].oreQty >= sulfurLarge1K)
                                        {
                                            playerData[player.userID].oreToAdd = sulfurLarge1K;
                                            playerData[player.userID].woodToAdd = 1000;
                                        }
                                        else
                                            playerData[player.userID].autoSplit = true;

                                        break;
                                    }

                                case metalOre:
                                    {
                                        if (playerData[player.userID].oreQty >= metalLarge1K)
                                        {
                                            playerData[player.userID].oreToAdd = metalLarge1K;
                                            playerData[player.userID].woodToAdd = 1000;
                                        }
                                        else
                                            playerData[player.userID].autoSplit = true;

                                        break;
                                    }

                                case hqOre:
                                    {
                                        playerData[player.userID].autoSplit = true;
                                        break;
                                    }

                                default:
                                    {
                                        return;
                                    }

                            }
                            break;
                        }

                    case 2000:
                        {
                            switch (playerData[player.userID].ore)
                            {
                                case (sulfurOre):
                                    {
                                        if (playerData[player.userID].oreQty >= sulfurLarge2K)
                                        {
                                            playerData[player.userID].oreToAdd = sulfurLarge2K;
                                            playerData[player.userID].woodToAdd = 2000;
                                        }
                                        else
                                            playerData[player.userID].autoSplit = true;

                                        break;
                                    }

                                case metalOre:
                                    {
                                        if (playerData[player.userID].oreQty >= metalLarge2K)
                                        {
                                            playerData[player.userID].oreToAdd = metalLarge2K;
                                            playerData[player.userID].woodToAdd = 2000;
                                        }

                                        break;
                                    }

                                case hqOre:
                                    {
                                        playerData[player.userID].autoSplit = true;
                                        break;
                                    }

                                default:
                                    {
                                        return;
                                    }

                            }
                            break;
                        }

                    case 2500:
                        {
                            if (playerData[player.userID].ore == sulfurOre)
                            {
                                if (playerData[player.userID].oreQty >= sulfurLarge2HK)
                                {
                                    playerData[player.userID].oreToAdd = sulfurLarge2HK;
                                    playerData[player.userID].woodToAdd = 2500;
                                }
                                else
                                    playerData[player.userID].autoSplit = true;
                            }
                            else
                            {
                                playerData[player.userID].autoSplit = true;
                            }
                            break;
                        }

                    case 3000:
                        {
                            if (playerData[player.userID].ore == metalOre)
                            {
                                if (playerData[player.userID].oreQty >= metalLarge3K)
                                {
                                    playerData[player.userID].oreToAdd = metalLarge3K;
                                    playerData[player.userID].woodToAdd = 3000;
                                }
                                else
                                    playerData[player.userID].autoSplit = true;
                            }
                            else
                            {
                                playerData[player.userID].autoSplit = true;
                            }
                            break;
                        }

                    case 4000:
                        {
                            if (playerData[player.userID].ore == metalOre)
                            {
                                if (playerData[player.userID].oreQty >= metalLarge4K)
                                {
                                    playerData[player.userID].oreToAdd = metalLarge4K;
                                    playerData[player.userID].woodToAdd = 4000;
                                }
                                else
                                    playerData[player.userID].autoSplit = true;
                            }
                            else
                            {
                                playerData[player.userID].autoSplit = true;
                            }

                            break;
                        }

                    case 5000:
                        {
                            if (playerData[player.userID].ore == metalOre)
                            {
                                if (playerData[player.userID].oreQty >= metalLarge5K)
                                {
                                    playerData[player.userID].oreToAdd = metalLarge5K;
                                    playerData[player.userID].autoSplit = true;
                                }
                                else
                                    playerData[player.userID].autoSplit = true;
                            }
                            else
                            {
                                playerData[player.userID].autoSplit = true;
                            }

                            break;
                        }

                    default:
                        {
                            playerData[player.userID].autoSplit = true;
                            break;
                        }
                        
                }
            }
            else
            {
                playerData[player.userID].autoSplit = true;
            }

            if (playerData[player.userID].autoSplit)
            {
                //if no wood was added or the ore amount is insufficient for it, define according to inventory
                switch (playerData[player.userID].ore)
                {
                    case (metalOre):
                        {
                            if ((playerData[player.userID].oreQty >= metalLarge5K) && (playerData[player.userID].woodQty >= 5000))
                            {
                                playerData[player.userID].woodToAdd = 5000;
                                playerData[player.userID].oreToAdd = metalLarge5K;
                            }
                            else
                            {
                                if ((playerData[player.userID].oreQty >= metalLarge4K) && (playerData[player.userID].woodQty >= 4000))
                                {
                                    playerData[player.userID].woodToAdd = 4000;
                                    playerData[player.userID].oreToAdd = metalLarge4K;
                                }
                                else
                                {
                                    if ((playerData[player.userID].oreQty >= metalLarge3K) && (playerData[player.userID].woodQty >= 3000))
                                    {
                                        playerData[player.userID].woodToAdd = 3000;
                                        playerData[player.userID].oreToAdd = metalLarge3K;
                                    }
                                    else
                                    {
                                        if ((playerData[player.userID].oreQty >= metalLarge2K) && (playerData[player.userID].woodQty >= 2000))
                                        {
                                            playerData[player.userID].woodToAdd = 2000;
                                            playerData[player.userID].oreToAdd = metalLarge2K;
                                        }
                                        else
                                        {
                                            if ((playerData[player.userID].oreQty >= metalLarge1K) && (playerData[player.userID].woodQty >= 1000))
                                            {
                                                playerData[player.userID].woodToAdd = 1000;
                                                playerData[player.userID].oreToAdd = metalLarge1K;
                                            }
                                            else
                                            {
                                                if ((playerData[player.userID].oreQty >= 990) && (playerData[player.userID].woodQty >= 330))
                                                {
                                                    playerData[player.userID].woodToAdd = 330;
                                                    playerData[player.userID].oreToAdd = 990;
                                                    playerData[player.userID].divideit = true;
                                                }
                                                else
                                                {
                                                    playerData[player.userID].divideit = true;

                                                    if (playerData[player.userID].oreQty >= 15)
                                                    {
                                                        int orePartToAdd = 66; //max
                                                        if (orePartToAdd * 15 > playerData[player.userID].oreQty)
                                                            orePartToAdd = (int)playerData[player.userID].oreQty / 15;

                                                        while (orePartToAdd * 5 > playerData[player.userID].woodQty)
                                                        {
                                                            orePartToAdd -= 1;
                                                        }

                                                        playerData[player.userID].oreToAdd = orePartToAdd * 15;
                                                        playerData[player.userID].woodToAdd = orePartToAdd * 5;
                                                    }
                                                    else
                                                        playerData[player.userID].oreToAdd = 0;
                                                    
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            break;
                        }

                    case (sulfurOre):
                        {
                            if ((playerData[player.userID].oreQty >= sulfurLarge2HK) && (playerData[player.userID].woodQty >= 2500))
                            {
                                playerData[player.userID].woodToAdd = 2500;
                                playerData[player.userID].oreToAdd = sulfurLarge2HK;
                            }
                            else
                            {
                                if ((playerData[player.userID].oreQty >= sulfurLarge2K) && (playerData[player.userID].woodQty >= 2000))
                                {
                                    playerData[player.userID].woodToAdd = 2000;
                                    playerData[player.userID].oreToAdd = sulfurLarge2K;
                                } 
                                else
                                {
                                    if ((playerData[player.userID].oreQty >= sulfurLarge1K) && (playerData[player.userID].woodQty >= 1000))
                                    {
                                        playerData[player.userID].woodToAdd = 1000;
                                        playerData[player.userID].oreToAdd = sulfurLarge1K;
                                    }
                                    else
                                    {
                                        if ((playerData[player.userID].oreQty >= 990) && (playerData[player.userID].woodQty >= 165))
                                        {
                                            playerData[player.userID].woodToAdd = 165;
                                            playerData[player.userID].oreToAdd = 990;
                                            playerData[player.userID].divideit = true;
                                        }
                                        else
                                        {
                                            playerData[player.userID].divideit = true;

                                            if (playerData[player.userID].oreQty >= 15)
                                            {
                                                int orePartToAdd = 66; //max
                                                if (orePartToAdd * 15 > playerData[player.userID].oreQty)
                                                    orePartToAdd = (int)playerData[player.userID].oreQty / 15;

                                                while ((int)Math.Ceiling(orePartToAdd * 2.5f) > playerData[player.userID].woodQty)
                                                {
                                                    orePartToAdd -= 1;
                                                }

                                                playerData[player.userID].oreToAdd = orePartToAdd * 15;
                                                playerData[player.userID].woodToAdd = (int)Math.Ceiling(orePartToAdd * 2.5f);
                                            }
                                            else
                                            {
                                                playerData[player.userID].oreToAdd = 0;
                                            }
                                        }
                                        
                                    }
                                }
                            }

                            break;
                        }

                    case (hqOre):
                        {
                            playerData[player.userID].divideit = true;

                            if (playerData[player.userID].oreQty >= 15) 
                            {
                                int orePartToAdd = 66; //max
                                if (orePartToAdd * 15 > playerData[player.userID].oreQty)
                                    orePartToAdd = (int)playerData[player.userID].oreQty / 15;

                                while (orePartToAdd * 10 > playerData[player.userID].woodQty)
                                {
                                    orePartToAdd -= 1;
                                }

                                playerData[player.userID].oreToAdd = orePartToAdd * 15;
                                playerData[player.userID].woodToAdd = orePartToAdd *10;
                            }
                            else
                                playerData[player.userID].oreToAdd = 0;

                            break;
                        }
                }
            }

        }

        void DefineQuantitesToUseSmall(BasePlayer player)
        {
            playerData[player.userID].autoSplit = false;
            playerData[player.userID].divideit = false;
            playerData[player.userID].oreToAdd = 0;
            playerData[player.userID].woodToAdd = 0;

            if (playerData[player.userID].woodQtyFurnace > 0)
            {
                switch (playerData[player.userID].woodQtyFurnace)
                {
                    case 1000:
                        {
                            switch (playerData[player.userID].ore)
                            {
                                case sulfurOre:
                                    {
                                        if (playerData[player.userID].oreQty >= 999)
                                        {
                                            playerData[player.userID].oreToAdd = 999;
                                            playerData[player.userID].woodToAdd = 833;
                                        }
                                        else
                                        {
                                            if (playerData[player.userID].oreQty >= sulfurSmall)
                                            {
                                                playerData[player.userID].oreToAdd = sulfurSmall;
                                                playerData[player.userID].woodToAdd = 750;
                                            }
                                            else
                                                playerData[player.userID].autoSplit = true;
                                        }

                                        break;
                                    }

                                case metalOre:
                                    {
                                        if (playerData[player.userID].oreQty >= metalSmall)
                                        {
                                            playerData[player.userID].oreToAdd = metalSmall;
                                            playerData[player.userID].woodToAdd = 1000;
                                        }
                                        else
                                            playerData[player.userID].autoSplit = true;

                                        break;
                                    }

                                case (hqOre):
                                    {
                                        playerData[player.userID].autoSplit = true;

                                        break;
                                    }

                                default:
                                    {
                                        return;
                                    }

                            }
                            break;
                        }

                    case 750:
                        {
                            switch (playerData[player.userID].ore)
                            {
                                case sulfurOre:
                                    {
                                        if (playerData[player.userID].oreQty >= 900)
                                        {
                                            playerData[player.userID].oreToAdd = 900;
                                            playerData[player.userID].woodToAdd = 750;
                                        }
                                        else
                                            playerData[player.userID].autoSplit = true;

                                        break;
                                    }

                                case metalOre:
                                    {
                                        if (playerData[player.userID].oreQty >= 150)
                                        {
                                            playerData[player.userID].oreToAdd = 450;
                                            playerData[player.userID].woodToAdd = 750;
                                        }
                                        else
                                            playerData[player.userID].autoSplit = true;

                                        break;
                                    }

                                case (hqOre):
                                    {
                                        playerData[player.userID].autoSplit = true;

                                        break;
                                    }

                                default:
                                    {
                                        return;
                                    }

                            }
                            break;
                        }

                    case 500:
                        {
                            switch (playerData[player.userID].ore)
                            {
                                case sulfurOre:
                                    {
                                        if (playerData[player.userID].oreQty >= 600)
                                        {
                                            playerData[player.userID].oreToAdd = 600;
                                            playerData[player.userID].woodToAdd = 500;
                                        }
                                        else
                                            playerData[player.userID].autoSplit = true;

                                        break;
                                    }

                                case metalOre:
                                    {
                                        if (playerData[player.userID].oreQty >= 100)
                                        {
                                            playerData[player.userID].oreToAdd = 300;
                                            playerData[player.userID].woodToAdd = 500;
                                        }
                                        else
                                            playerData[player.userID].autoSplit = true;

                                        break;
                                    }

                                case (hqOre):
                                    {
                                        playerData[player.userID].autoSplit = true;

                                        break;
                                    }

                                default:
                                    {
                                        return;
                                    }

                            }
                            break;
                        }

                    default:
                        {
                            playerData[player.userID].autoSplit = true;
                            break;
                        }

                }
            }
            else
            {
                playerData[player.userID].autoSplit = true;
            }

            if (playerData[player.userID].autoSplit)
            {
                //if no wood was added or the ore amount is insufficient for it, define according to inventory
                switch (playerData[player.userID].ore)
                {
                    case (metalOre):
                        {
                            if ((playerData[player.userID].oreQty >= metalSmall) && (playerData[player.userID].woodQty >= 1000)) //enough for the best
                            {
                                playerData[player.userID].woodToAdd = 1000;
                                playerData[player.userID].oreToAdd = metalSmall;
                            }
                            else
                            {
                                playerData[player.userID].divideit = true;

                                if (playerData[player.userID].oreQty >= 3)
                                {
                                    int orePartToAdd = 200; //max
                                    if (orePartToAdd * 3 > playerData[player.userID].oreQty)
                                        orePartToAdd = (int)playerData[player.userID].oreQty / 3;

                                    while (orePartToAdd * 5 > playerData[player.userID].woodQty)
                                    {
                                        orePartToAdd -= 1;
                                    }
                                    playerData[player.userID].woodToAdd = orePartToAdd * 5;
                                    playerData[player.userID].oreToAdd = orePartToAdd * 3;
                                }
                                else
                                    playerData[player.userID].oreToAdd = 0;
                            }

                            break;
                        }

                    case (sulfurOre):
                        {
                            if ((playerData[player.userID].oreQty >= 999) && (playerData[player.userID].woodQty >= 833)) 
                            {
                                playerData[player.userID].oreToAdd = 999;
                                playerData[player.userID].woodToAdd = 833;
                            } else
                            {
                                if ((playerData[player.userID].oreQty >= sulfurSmall) && (playerData[player.userID].woodQty >= 750))
                                {
                                    playerData[player.userID].woodToAdd = 750;
                                    playerData[player.userID].oreToAdd = sulfurSmall;
                                }
                                else
                                {
                                    playerData[player.userID].divideit = true;

                                    if (playerData[player.userID].oreQty >= 3)
                                    {
                                        int orePartToAdd = 333; //max
                                        if (orePartToAdd * 3 > playerData[player.userID].oreQty)
                                            orePartToAdd = (int)playerData[player.userID].oreQty / 3;

                                        while ((int)Math.Ceiling(orePartToAdd * 2.5f) > playerData[player.userID].woodQty)
                                        {
                                            orePartToAdd -= 1;
                                        }

                                        playerData[player.userID].woodToAdd = (int)Math.Ceiling(orePartToAdd * 2.5f);
                                        playerData[player.userID].oreToAdd = orePartToAdd * 3;
                                    }
                                    else
                                        playerData[player.userID].oreToAdd = 0;
                                }
                            }

                            break;
                        }

                    case (hqOre):
                        {
                            playerData[player.userID].divideit = true;

                            if (playerData[player.userID].oreQty >= 3)
                            {
                                
                                int orePartToAdd = 100; //max
                                if (orePartToAdd * 3 > playerData[player.userID].oreQty)
                                    orePartToAdd = (int)playerData[player.userID].oreQty / 3;
                                while (orePartToAdd * 10 > playerData[player.userID].woodQty)
                                {
                                    orePartToAdd -= 1;
                                }

                                playerData[player.userID].woodToAdd = orePartToAdd * 10;
                                playerData[player.userID].oreToAdd = orePartToAdd * 3;
                            }
                            else
                                playerData[player.userID].oreToAdd = 0;

                            break;
                        }
                            
                }
            }

        }

        void Optimizefurnace(BasePlayer player, BaseOven furnace)
        {
            //check for items other than wood, sulfur or metal
            if (HasInvalidItems(furnace))
            {
                player.ChatMessage(Lang("Invalid items", player.UserIDString));
                return;
            }

            //get available amounts of resources
            playerData[player.userID].sulfurQtyFurnace = furnace.inventory.GetAmount(sulfurOre, true);
            playerData[player.userID].metalQtyFurnace = furnace.inventory.GetAmount(metalOre, true);
            playerData[player.userID].hqQtyFurnace = furnace.inventory.GetAmount(hqOre, true);
            playerData[player.userID].woodQtyFurnace = furnace.inventory.GetAmount(wood, true);

            playerData[player.userID].sulfurQtyPlayer = player.inventory.GetAmount(sulfurOre);
            playerData[player.userID].metalQtyPlayer = player.inventory.GetAmount(metalOre);
            playerData[player.userID].hqQtyPlayer = player.inventory.GetAmount(hqOre);
            playerData[player.userID].woodQQtyPlayer = player.inventory.GetAmount(wood);

            playerData[player.userID].woodQty = playerData[player.userID].woodQtyFurnace + playerData[player.userID].woodQQtyPlayer;

            //check wood to burn
            if (playerData[player.userID].woodQty == 0)
            {
                player.ChatMessage(Lang("No wood", player.UserIDString));
                return;
            }

            //define what must be burnt
            if (!BurnableDefined(player))
                return;
            
            //get total amount of ore to burn
            switch (playerData[player.userID].ore)
            {
                case (metalOre):
                    {
                        playerData[player.userID].oreQty = playerData[player.userID].metalQtyFurnace + playerData[player.userID].metalQtyPlayer;
                        playerData[player.userID].oreQtyPlayer = playerData[player.userID].metalQtyPlayer;
                        playerData[player.userID].oreQtyFurnace = playerData[player.userID].metalQtyFurnace;

                        break;
                    }

                case (sulfurOre):
                    {
                        playerData[player.userID].oreQty = playerData[player.userID].sulfurQtyFurnace + playerData[player.userID].sulfurQtyPlayer;
                        playerData[player.userID].oreQtyPlayer = playerData[player.userID].sulfurQtyPlayer;
                        playerData[player.userID].oreQtyFurnace = playerData[player.userID].sulfurQtyFurnace;

                        break;
                    }

                case (hqOre):
                    {
                        playerData[player.userID].oreQty = playerData[player.userID].hqQtyFurnace + playerData[player.userID].hqQtyPlayer;
                        playerData[player.userID].oreQtyPlayer = playerData[player.userID].hqQtyPlayer;
                        playerData[player.userID].oreQtyFurnace = playerData[player.userID].hqQtyFurnace;

                        break;
                    }
            }
            
                
            //something to burn at all?
            if (playerData[player.userID].oreQty == 0)
            {
                player.ChatMessage(Lang("Nothing to burn", player.UserIDString));
                return;
            }

            //get the best solution
            if (playerData[player.userID].currFurnace == furnaceType.large)
                DefineQuantitesToUseLarge(player);
            else
                DefineQuantitesToUseSmall(player);

            //no success
            if ((playerData[player.userID].oreToAdd <= 0) || (playerData[player.userID].woodToAdd <= 0))
            {
                player.ChatMessage(Lang("Cannot optimize", player.UserIDString));
                return;
            }

            //split stuff
            if (playerData[player.userID].currFurnace == furnaceType.large)
                Filllargefurnace(furnace, player);
            else
                Fillsmallfurnace(furnace, player);

            //take from or give to player according what was used and what was already in furnace
            Handleremainings(player);
        }

        void Handleremainings(BasePlayer player)
        {
            //take from player what was used that wasn't in the furnace
            if (playerData[player.userID].woodQtyFurnace < playerData[player.userID].woodToAdd)
                player.inventory.Take(items, wood, playerData[player.userID].woodToAdd - playerData[player.userID].woodQtyFurnace);

            if (playerData[player.userID].oreQtyFurnace < playerData[player.userID].oreToAdd) 
            {
                switch(playerData[player.userID].ore)
                {
                    case (metalOre):
                        {
                            player.inventory.Take(items, metalOre, playerData[player.userID].oreToAdd - playerData[player.userID].metalQtyFurnace);
                            break;
                        }

                    case (sulfurOre):
                        {
                            player.inventory.Take(items, sulfurOre, playerData[player.userID].oreToAdd - playerData[player.userID].sulfurQtyFurnace);
                            break;
                        }

                    case (hqOre):
                        {
                            player.inventory.Take(items, hqOre, playerData[player.userID].oreToAdd - playerData[player.userID].hqQtyFurnace);
                            break;
                        }
                }
            }

            //give to player what was too much in the furnace
            if (playerData[player.userID].woodQtyFurnace > playerData[player.userID].woodToAdd)
                ReturnUnstackedToPlayer(player, wood, playerData[player.userID].woodQtyFurnace - playerData[player.userID].woodToAdd);

            switch (playerData[player.userID].ore)
            {
                case (metalOre):
                    {
                        if (playerData[player.userID].metalQtyFurnace > playerData[player.userID].oreToAdd)
                            ReturnUnstackedToPlayer(player, metalOre, playerData[player.userID].metalQtyFurnace - playerData[player.userID].oreToAdd);

                        break;
                    }

                case (sulfurOre):
                    {
                        if (playerData[player.userID].sulfurQtyFurnace > playerData[player.userID].oreToAdd)
                            ReturnUnstackedToPlayer(player, sulfurOre, playerData[player.userID].sulfurQtyFurnace - playerData[player.userID].oreToAdd);

                        break;
                    }

                case (hqOre):
                    {
                        if (playerData[player.userID].hqQtyFurnace > playerData[player.userID].oreToAdd)
                            ReturnUnstackedToPlayer(player, hqOre, playerData[player.userID].hqQtyFurnace - playerData[player.userID].oreToAdd);

                        break;
                    }
            }
        }

        void ReturnUnstackedToPlayer(BasePlayer player, int itemID, int Qty)
        {
            int remaining = Qty;

            while (remaining > 0)
            {
                if (remaining >= 1000)
                {
                    player.GiveItem(ItemManager.CreateByItemID(itemID, 1000), BaseEntity.GiveItemReason.PickedUp);
                    remaining -= 1000;
                }
                    
                else
                {
                    player.GiveItem(ItemManager.CreateByItemID(itemID, remaining), BaseEntity.GiveItemReason.PickedUp);
                    remaining = 0;
                }
            }
        }

        void Filllargefurnace(BaseOven furnace, BasePlayer player)
        {
            //clear the furnace for easy filling
            switch (playerData[player.userID].ore)
            {
                case (metalOre):
                    {
                        furnace.inventory.Take(items, metalOre, playerData[player.userID].metalQtyFurnace);
                        break;
                    }

                case (sulfurOre):
                    {
                        furnace.inventory.Take(items, sulfurOre, playerData[player.userID].sulfurQtyFurnace);
                        break;
                    }

                case (hqOre):
                    {
                        furnace.inventory.Take(items, hqOre, playerData[player.userID].hqQtyFurnace);
                        break;
                    }
            }

            furnace.inventory.Take(items, wood, playerData[player.userID].woodQtyFurnace);

            //make sure wood is in the right place
            if (playerData[player.userID].woodToAdd == 2500)
            {
                ItemManager.CreateByItemID(wood, 500).MoveToContainer(furnace.inventory, 0, false);
                ItemManager.CreateByItemID(wood, 1000).MoveToContainer(furnace.inventory, 1, false);
                ItemManager.CreateByItemID(wood, 1000).MoveToContainer(furnace.inventory, 2, false);
            }
            else
            {
                if (playerData[player.userID].woodToAdd >= 1000)
                {
                    for (int i = 1; i <= playerData[player.userID].woodToAdd / 1000; i++)
                        ItemManager.CreateByItemID(wood, 1000).MoveToContainer(furnace.inventory, i - 1, false);
                }
                else
                {
                    ItemManager.CreateByItemID(wood, playerData[player.userID].woodToAdd).MoveToContainer(furnace.inventory, 0, false);

                }
                    
            }

            //add the ore
            if (playerData[player.userID].divideit)
            {
                for (int i = 3; i <= 17; i++)
                    ItemManager.CreateByItemID(playerData[player.userID].ore, playerData[player.userID].oreToAdd / 15).MoveToContainer(furnace.inventory, i, false);
            }
            else
            {
                switch (playerData[player.userID].ore)
                {
                    case (metalOre):
                        {
                            switch (playerData[player.userID].woodToAdd)
                            {
                                case 1000:
                                    {
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 66).MoveToContainer(furnace.inventory, 3, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 138).MoveToContainer(furnace.inventory, 4, false);
                                        for (int i = 5; i <= 17; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 200).MoveToContainer(furnace.inventory, i, false);
                                        
                                        break;
                                    }

                                case 2000:
                                    {
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 71).MoveToContainer(furnace.inventory, 4, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 148).MoveToContainer(furnace.inventory, 5, false);
                                        for (int i = 6; i <= 10; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 400).MoveToContainer(furnace.inventory, i, false);
                                        
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 231).MoveToContainer(furnace.inventory, 11, false);

                                        for (int i = 12; i <= 16; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 400).MoveToContainer(furnace.inventory, i, false);
                                        
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 322).MoveToContainer(furnace.inventory, 17, false);

                                        break;
                                    }

                                case 3000:
                                    {
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 76).MoveToContainer(furnace.inventory, 5, false);
                                        for (int i = 6; i <= 8; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 600).MoveToContainer(furnace.inventory, i, false);
                                        
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 587).MoveToContainer(furnace.inventory, 9, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 462).MoveToContainer(furnace.inventory, 10, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 160).MoveToContainer(furnace.inventory, 11, false);

                                        for (int i = 12; i <= 15; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 600).MoveToContainer(furnace.inventory, i, false);
                                        
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 351).MoveToContainer(furnace.inventory, 16, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 251).MoveToContainer(furnace.inventory, 17, false);

                                        break;
                                    }

                                case 4000:
                                    {
                                        for (int i = 6; i <= 8; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 800).MoveToContainer(furnace.inventory, i, false);
                                        
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 510).MoveToContainer(furnace.inventory, 9, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 385).MoveToContainer(furnace.inventory, 10, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 83).MoveToContainer(furnace.inventory, 11, false);
                                        for (int i = 12; i <= 14; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 800).MoveToContainer(furnace.inventory, i, false);
                                        
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 653).MoveToContainer(furnace.inventory, 15, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 274).MoveToContainer(furnace.inventory, 16, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 174).MoveToContainer(furnace.inventory, 17, false);

                                        break;
                                    }

                                case 5000:
                                    {
                                        for (int i = 6; i <= 8; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 1000).MoveToContainer(furnace.inventory, i, false);
                                        
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 427).MoveToContainer(furnace.inventory, 9, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 302).MoveToContainer(furnace.inventory, 10, false);
                                        
                                        for (int i = 12; i <= 13; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 1000).MoveToContainer(furnace.inventory, i, false);
                                        
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 736).MoveToContainer(furnace.inventory, 14, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 569).MoveToContainer(furnace.inventory, 15, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 190).MoveToContainer(furnace.inventory, 16, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 90).MoveToContainer(furnace.inventory, 17, false);

                                        break;
                                    }
                            }
                            break;
                        }

                    case (sulfurOre):
                        {
                            switch (playerData[player.userID].woodToAdd)
                            {
                                case 1000:
                                    {
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 66).MoveToContainer(furnace.inventory, 3, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 138).MoveToContainer(furnace.inventory, 4, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 215).MoveToContainer(furnace.inventory, 5, false);

                                        for (int i = 6; i <= 10; i++)
                                        {
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 400).MoveToContainer(furnace.inventory, i, false);
                                        }

                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 298).MoveToContainer(furnace.inventory, 11, false);

                                        for (int i = 12; i <= 16; i++)
                                        {
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 400).MoveToContainer(furnace.inventory, i, false);
                                        }

                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 389).MoveToContainer(furnace.inventory, 17, false);

                                        break;
                                    }

                                case 2000:
                                    {
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 71).MoveToContainer(furnace.inventory, 4, false);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 148).MoveToContainer(furnace.inventory, 5, false);
                                        for (int i = 6; i <= 8; i++)
                                        {
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 800).MoveToContainer(furnace.inventory, i, false);
                                        }

                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 658).MoveToContainer(furnace.inventory, 9);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 533).MoveToContainer(furnace.inventory, 10);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 231).MoveToContainer(furnace.inventory, 11);

                                        for (int i = 12; i <= 15; i++)
                                        {
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 800).MoveToContainer(furnace.inventory, i);
                                        }

                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 422).MoveToContainer(furnace.inventory, 16);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 322).MoveToContainer(furnace.inventory, 17);

                                        break;
                                    }

                                case 2500:
                                    {
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 76).MoveToContainer(furnace.inventory, 5);
                                        for (int i = 6; i <= 8; i++)
                                        {
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 1000).MoveToContainer(furnace.inventory, i);
                                        }

                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 587).MoveToContainer(furnace.inventory, 9);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 462).MoveToContainer(furnace.inventory, 10);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 160).MoveToContainer(furnace.inventory, 11);

                                        for (int i = 12; i <= 14; i++)
                                        {
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 1000).MoveToContainer(furnace.inventory, i);
                                        }

                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 872).MoveToContainer(furnace.inventory, 15);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 351).MoveToContainer(furnace.inventory, 16);
                                        ItemManager.CreateByItemID(playerData[player.userID].ore, 251).MoveToContainer(furnace.inventory, 17);

                                        break;
                                    }
                            }
                            break;
                        }

                    default:
                        return;
                }
            }
        }

        void Fillsmallfurnace(BaseOven furnace, BasePlayer player)
        {
            //clear the furnace for easy filling
            switch (playerData[player.userID].ore)
            {
                case (metalOre):
                    {
                        furnace.inventory.Take(items, metalOre, playerData[player.userID].metalQtyFurnace);
                        break;
                    }

                case (sulfurOre):
                    {
                        furnace.inventory.Take(items, sulfurOre, playerData[player.userID].sulfurQtyFurnace);
                        break;
                    }

                case (hqOre):
                    {
                        furnace.inventory.Take(items, hqOre, playerData[player.userID].hqQtyFurnace);
                        break;
                    }
            }
  
            furnace.inventory.Take(items, wood, playerData[player.userID].woodQtyFurnace);

            //only one spot for wood
            ItemManager.CreateByItemID(wood, playerData[player.userID].woodToAdd).MoveToContainer(furnace.inventory, 0, false);
            
            //add the ore
            if (playerData[player.userID].divideit)
            {
                for (int i = 1; i <= 3; i++)
                    ItemManager.CreateByItemID(playerData[player.userID].ore, playerData[player.userID].oreToAdd / 3).MoveToContainer(furnace.inventory, i, false);
            }
            else
            {
                switch (playerData[player.userID].ore)
                {
                    case (metalOre):
                        {
                            switch (playerData[player.userID].woodToAdd)
                            {
                                case 1000:
                                    {
                                        for (int i = 1; i <= 3; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 200).MoveToContainer(furnace.inventory, i, false);

                                        break;
                                    }

                                case 750:
                                    {
                                        for (int i = 1; i <= 3; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 150).MoveToContainer(furnace.inventory, i, false);

                                        break;
                                    }

                                case 500:
                                    {
                                        for (int i = 1; i <= 3; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 100).MoveToContainer(furnace.inventory, i, false);

                                        break;
                                    }
                            }
                            break;
                        }

                    case (sulfurOre):
                        {
                            switch (playerData[player.userID].woodToAdd)
                            {
                                case 833:
                                    {
                                        for (int i = 1; i <= 3; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 333).MoveToContainer(furnace.inventory, i, false);

                                        break;
                                    }
                                case 750:
                                    {
                                        for (int i = 1; i <= 3; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 300).MoveToContainer(furnace.inventory, i, false);

                                        break;
                                    }

                                case 500:
                                    {
                                        for (int i = 1; i <= 3; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 200).MoveToContainer(furnace.inventory, i, false);

                                        break;
                                    }
                                case 250:
                                    {
                                        for (int i = 1; i <= 3; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, 100).MoveToContainer(furnace.inventory, i, false);

                                        break;
                                    }

                                default:
                                    {
                                        for (int i = 1; i <= 3; i++)
                                            ItemManager.CreateByItemID(playerData[player.userID].ore, playerData[player.userID].oreToAdd).MoveToContainer(furnace.inventory, i, false);

                                        break;
                                    }
                            }
                            break;
                        }

                    default:
                        return;
                }
            }
        }
        #endregion methods

        #region UI
        private CuiElementContainer CreateUi(BasePlayer player, BaseOven oven)
        {
            float uiScale = 1.0f;
            int contentSize = Convert.ToInt32(10 * uiScale);
            string toggleButtonColor = "0.415 0.5 0.258 0.4"; 
            string toggleButtonTextColor = "0.607 0.705 0.431"; 

            DestroyUI(player);

            Vector2 uiPosition = new Vector2(
                ((((0.6505f) - 0.5f) * uiScale) + 0.5f),
                (0.042f - 0.02f) + 0.02f * uiScale);
            
            Vector2 uiSize = new Vector2(0.1785f * uiScale, 0.111f * uiScale);

            CuiElementContainer result = new CuiElementContainer();
            string rootPanelName = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = uiPosition.x + " " + uiPosition.y,
                    AnchorMax = uiPosition.x + uiSize.x + " " + (uiPosition.y + uiSize.y)
                }
            }, "Hud.Menu");

            string contentPanel = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = "0.65 0.65 0.65 0.06"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 0.74"
                }
            }, rootPanelName);

            // Toggle button
            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.02 0.65",  
                    AnchorMax = "0.25 0.95"
                },
                Button =
                {
                    Command = "optimalburn.split",
                    Color = toggleButtonColor
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = "Optimize",
                    Color = toggleButtonTextColor,
                    FontSize = Convert.ToInt32(11 * uiScale)
                }
            }, contentPanel);

            openUis.Add(player.userID, rootPanelName);
            CuiHelper.AddUi(player, result);
            return result;
        }

        private void DestroyUI(BasePlayer player)
        {
            if (!openUis.ContainsKey(player.userID))
                return;

            string uiName = openUis[player.userID];

            if (openUis.Remove(player.userID))
                CuiHelper.DestroyUi(player, uiName);
        }

        private void DestroyOvenUI(BaseOven oven)
        {
            if (oven == null) throw new ArgumentNullException(nameof(oven));

            foreach (KeyValuePair<ulong, string> kv in openUis.ToDictionary(kv => kv.Key, kv => kv.Value))
            {
                BasePlayer player = BasePlayer.FindByID(kv.Key);

                BaseOven playerLootOven = player.inventory.loot?.entitySource as BaseOven;

                if (oven == playerLootOven)
                {
                    DestroyUI(player);
                }
            }
        }
        #endregion UI

        #region helpers
        private bool HasPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, permUse);
        }

        private string Lang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);
        #endregion helpers
    }
}