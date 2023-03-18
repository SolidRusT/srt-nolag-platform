using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Item Cleaner", "Lorddy", "0.0.6")]
    [Description("Remove specified item from all containers and players")]
    public class ItemCleaner : RustPlugin
    {
        #region Fields
        private const string PERMISSION_USE = "itemcleaner.use";
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CmdHelp1"] = "<color=orange>/itemcleaner remove <Item Display Name| Item Shortname | Item ID></color> <color=yellow>[optional: Entity Owner Name] [optional:\"priv\" To look in current privilege] </color> - Clear all containers and players of the specified item",
                ["CmdHelp2"] = "<color=orange>/itemcleaner find  <Item Display Name| Item Shortname | Item ID></color> <color=yellow>[optional: Entity Owner Name] [optional:\"priv\" To look in current privilege] </color>- Find all containers and players of the specified item",
                ["ItemNotFound"] = "Item Definition Not Found",
                //["PlayerNotFound"] = "Player Not Found",
                ["NoPerm"] = "You don't have permission to use this command",
                ["RemovedFromStorage"] = "Removed {0} {1} item from {2} Storage Container",
                ["RemovedFromPlayer"] = "Removed {0} {1} item from Player {2}.",
                ["FoundFromStorage"] = "Found {0} {1} item from {2} Storage Container",
                ["FoundFromPlayer"] = "Found {0} {1} item from Player {2}.",
                ["TimeTaken"] = "Item Cleaner Command Took {0} ms",
                ["FoundStorage"] = "Checking {0} Storage Containers",
                ["StorageSearchFinish"] = "Storage Container Search Finished",
                ["PlayerSearchFinish"] = "Player Inventory Search Finished",
            }, this);
        }
        #endregion

        #region Hooks
        private void Loaded()
        {
            permission.RegisterPermission(PERMISSION_USE, this);
        }
        #endregion

        #region Commands

        [ChatCommand("itemcleaner")]
        private void ItemCleanerCommand(BasePlayer player, string command, string[] args)
        {
            if (!(permission.UserHasPermission(player.UserIDString,PERMISSION_USE)))
            {
                PrintToChat(player, lang.GetMessage("NoPerm", this, player.UserIDString));
                return;
            }
            if (args.Length < 2)
            {
                PrintToChat(player, lang.GetMessage("CmdHelp1", this, player.UserIDString));
                PrintToChat(player, lang.GetMessage("CmdHelp2", this, player.UserIDString));
                return;
            }
            bool remove = false;
            switch (args[0].ToLower())
            {
                case "remove": remove = true; break;
                case "find": remove = false; break;
                default:
                    PrintToChat(player, lang.GetMessage("CmdHelp1", this, player.UserIDString));
                    PrintToChat(player, lang.GetMessage("CmdHelp2", this, player.UserIDString));
                    return;
            }

            bool priv = args.Contains("priv");

            var itemname = args[1].ToLower();
            var itemId = -1;
            int.TryParse(args[1].ToLower(), out itemId);
            ItemDefinition itemDefinition = null;
            
            IPlayer entityOwner = null;
            if (args.Length >= 3)
                entityOwner = covalence.Players.FindPlayer(args[2].ToLower());

            for (int i = 0; i < ItemManager.itemList.Count; i++)
            {
                if (ItemManager.itemList[i].displayName.english.ToLower() == itemname.ToLower() ||
                    ItemManager.itemList[i].shortname == itemname.ToLower() ||
                    (itemId != -1 && ItemManager.itemList[i].itemid == itemId))
                {
                    itemDefinition = ItemManager.itemList[i];
                    break;
                }
            }
            if (itemDefinition == null)
            {
                PrintToChat(player, lang.GetMessage("ItemNotFound", this, player.UserIDString));
                return;
            }
            RemoveItem(player, itemDefinition, remove, priv ? null : entityOwner, priv);
        }
        #endregion

        #region Methods
        private void RemoveItem(BasePlayer player, ItemDefinition itemDefinition, bool remove = true, IPlayer entityOwner = null, bool priv = false)
        {
            DateTime startTime = DateTime.Now;

            //var scs = Resources.FindObjectsOfTypeAll<StorageContainer>();
            var playerPriv = player?.GetBuildingPrivilege();

            var scs = BaseNetworkable.serverEntities.OfType<StorageContainer>();

            int i = 0;
            foreach(var sc in scs)
            {
                if (sc is LootContainer || !sc.OwnerID.IsSteamId())
                {
                    continue;
                }

                timer.In(i * 0.01f, () => RemoveItem(sc, player, itemDefinition, playerPriv, remove, entityOwner, priv));
                i++; 
            }
            timer.In((i + 1) * 0.01f, () => PrintToChat(player, string.Format(lang.GetMessage("StorageSearchFinish", this, player.UserIDString))));
            string storageFound = string.Format(lang.GetMessage("FoundStorage", this, player.UserIDString), i);
            //Puts(storageFound);
            PrintToChat(player, storageFound);

            int j = 0;
            foreach(var p in BasePlayer.allPlayerList)
            {
                timer.In(j * 0.01f, () => RemoveItem(p, player, itemDefinition, playerPriv, remove, entityOwner, priv));
                j++;
            }

            timer.In((j + 1) * 0.01f, () => PrintToChat(player, string.Format(lang.GetMessage("PlayerSearchFinish", this, player.UserIDString))));

            string timeTaken = string.Format(lang.GetMessage("TimeTaken",this,player.UserIDString), DateTime.Now.Subtract(startTime).TotalMilliseconds);
            //Puts(timeTaken);
            PrintToChat(player, timeTaken);
        }

        void RemoveItem(StorageContainer sc, BasePlayer player, ItemDefinition itemDefinition, BuildingPrivlidge playerPriv, bool remove = true, IPlayer entityOwner = null, bool priv = false)
        {
            if (entityOwner != null && sc.OwnerID != ulong.Parse(entityOwner.Id)) return;

            if (priv == false || (priv && sc?.GetBuildingPrivilege()?.net.ID == playerPriv?.net.ID))
            {
                RemoveItem(player, itemDefinition, sc, remove);
            }
                
        }

        void RemoveItem(BasePlayer bp, BasePlayer player, ItemDefinition itemDefinition, BuildingPrivlidge playerPriv, bool remove = true, IPlayer entityOwner = null, bool priv = false)
        {
            if (entityOwner != null && bp.UserIDString != entityOwner.Id) return;
            if (priv == false || (priv && bp?.GetBuildingPrivilege()?.net.ID == playerPriv?.net.ID))
            {
                RemoveItem(player, itemDefinition, bp, remove);
            }
                
        }


        private void RemoveItem(BasePlayer player, ItemDefinition itemDefinition, BasePlayer target, bool remove)
        {
            if (target == null) return;
            PlayerInventory inventory = target.inventory;
            if (inventory == null) return;
            List<Item> list = inventory.containerMain.itemList.FindAll((Item x) => x.info.itemid == itemDefinition.itemid);
            list.AddRange(inventory.containerBelt.itemList.Where((Item x) => x.info.itemid == itemDefinition.itemid));
            list.AddRange(inventory.containerWear.itemList.Where((Item x) => x.info.itemid == itemDefinition.itemid));
            if (list.Count > 0)
            {
                string msg = string.Format(lang.GetMessage(remove ? "RemovedFromPlayer" : "FoundFromPlayer", this, player.UserIDString), list.Count, itemDefinition.displayName.english, target.displayName); ;
                //Puts(msg);
                PrintToChat(player, msg);

                if (remove)
                    foreach (var item in list)
                    {
                        item.Remove();
                    }
            }
        }
        private void RemoveItem(BasePlayer player, ItemDefinition itemDefinition, StorageContainer sc, bool remove)
        {
            ItemContainer inventory = sc.inventory;
            if (inventory == null) return;
            List<Item> list = inventory.FindItemsByItemID(itemDefinition.itemid);
            //Puts($"{sc.OwnerID}'s Container Item List: [{string.Join(", ", sc.inventory.itemList.Select(x => x.info.displayName.english))}]");
            if (list.Count > 0)
            {
                string msg = string.Format(lang.GetMessage(remove ? "RemovedFromStorage" : "FoundFromStorage", this, player.UserIDString), list.Count, itemDefinition.displayName.english, sc.net.ID);
                //Puts(msg);
                PrintToChat(player, msg);
                if (remove)
                    foreach (var item in list)
                    {
                        item.Remove();
                    }
            }
        }
        #endregion
    }
}

