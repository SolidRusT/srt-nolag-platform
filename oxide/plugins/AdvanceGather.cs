using System.Collections.Generic;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Advance Gather", "Default", "1.0.7")]
    [Description("Custom gathering with some action's and extension drop")]
    class AdvanceGather : RustPlugin
    {
        #region Variable
        private int appleChance;
        private int rAppleChance;
        private int berryChance;
        private int berryChancePlanted;
        private int berryAmountMax;
        private int berryAmountMin;
        private string berryItem;

        private int LowgradeChanceCorn;
        private int LowgradeChancePlantedCorn;
        private int LowgradeAmountMaxCorn;
        private int LowgradeAmountMinCorn;
        private string LowgradeItemCorn;

        private int LowgradeChancePumpkin;
        private int LowgradeChancePlantedPumpkin;
        private int LowgradeAmountMaxPumpkin;
        private int LowgradeAmountMinPumpkin;
        private string LowgradeItemPumpkin;

        private bool enableBroadcast;
        #endregion

        #region Function
        object GetVariable(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
            }
            return value;
        }
        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
        #endregion

        #region Hooks
        protected override void LoadDefaultConfig()
        {
            enableBroadcast = Convert.ToBoolean(GetVariable("Option", "Enable broadcast", true));
            rAppleChance = Convert.ToInt32(GetVariable("Get apple from Tree", "Chance to drop rotten apple (Chance depend on chance)", 30));
            appleChance = Convert.ToInt32(GetVariable("Get apple from Tree", "Chance to drop any apples per hit", 3));

            berryItem = Convert.ToString(GetVariable("Get blueberries from Hemp", "berry Item", "blueberries"));
            berryChance = Convert.ToInt32(GetVariable("Get blueberries from Hemp", "Chance to get berry from hemp", 10));
            berryChancePlanted = Convert.ToInt32(GetVariable("Get blueberries from Hemp", "Chance to get berry from planted hemp", 80));
            berryAmountMax = Convert.ToInt32(GetVariable("Get blueberries from Hemp", "Max amount", 2));
            berryAmountMin = Convert.ToInt32(GetVariable("Get blueberries from Hemp", "Min amount", 1));

            LowgradeItemCorn = Convert.ToString(GetVariable("Get Lowgrades from Corn", "Lowgrade Item", "lowgradefuel"));
            LowgradeChanceCorn = Convert.ToInt32(GetVariable("Get Lowgrades from Corn", "Chance to get Lowgrade from corn", 8));
            LowgradeChancePlantedCorn = Convert.ToInt32(GetVariable("Get Lowgrades from Corn", "Chance to get Lowgrade from planted corn", 65));
            LowgradeAmountMaxCorn = Convert.ToInt32(GetVariable("Get Lowgrades from Corn", "Max amount", 3));
            LowgradeAmountMinCorn = Convert.ToInt32(GetVariable("Get Lowgrades from Corn", "Min amount", 2));

            LowgradeItemPumpkin = Convert.ToString(GetVariable("Get Lowgrades from Pumpkin", "Lowgrade Item", "lowgradefuel"));
            LowgradeChancePumpkin = Convert.ToInt32(GetVariable("Get Lowgrades from Pumpkin", "Chance to get Lowgrade from pumpkin", 5));
            LowgradeChancePlantedPumpkin = Convert.ToInt32(GetVariable("Get Lowgrades from Pumpkin", "Chance to get Lowgrade from planted pumpkin", 50));
            LowgradeAmountMaxPumpkin = Convert.ToInt32(GetVariable("Get Lowgrades from Pumpkin", "Max amount", 8));
            LowgradeAmountMinPumpkin = Convert.ToInt32(GetVariable("Get Lowgrades from Pumpkin", "Min amount", 4));

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            ItemManager.FindItemDefinition(berryItem).stackable = 1000000;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Apple"] = "<color=#66FF33>Congratulations!</color> You got <color=#66FF33>apple</color> from tree !",
                ["Berry"] = "<color=#66FF33>Congratulations!</color> You got <color=#66FF33>berry</color> from hemp !",
                ["LowgradeCorn"] = "<color=#66FF33>Congratulations!</color> You got <color=#66FF33>Lowgrade</color> from corn !",
                ["LowgradePumpkin"] = "<color=#66FF33>Congratulations!</color> You got <color=#66FF33>Lowgrade</color> from corn !"
            }, this);
        }

	    //Get apple from Tree
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (dispenser.GetComponent<BaseEntity>() is TreeEntity)
            {
                if (Oxide.Core.Random.Range(0, 100) < appleChance)
                {
                    if (Oxide.Core.Random.Range(0, 100) < rAppleChance)
                        ItemManager.CreateByName("apple.spoiled", 1).Drop(new Vector3(entity.transform.position.x, entity.transform.position.y + 20f, entity.transform.position.z), Vector3.zero);
                    else
                        ItemManager.CreateByName("apple", 1).Drop(new Vector3(entity.transform.position.x, entity.transform.position.y + 20f, entity.transform.position.z), Vector3.zero);
                    if (enableBroadcast)
                        SendReply(entity as BasePlayer, String.Format(msg("Apple")));
                }
            }
        }

	    //Natural plants
        void OnCollectiblePickup(Item item, BasePlayer player)
        {
  	        //Get berry from hemp
            if (item.info.displayName.english.Contains("Cloth"))
            {
                if (Oxide.Core.Random.Range(0, 100) < berryChance)
                {
                    player.inventory.GiveItem(ItemManager.CreateByName(berryItem, Oxide.Core.Random.Range(berryAmountMin, berryAmountMax + 1)));
                    if (enableBroadcast)
                        SendReply(player, String.Format(msg("Berry")));
                }
            }

	          //Get Lowgrade from corn
            if (item.info.displayName.english.Contains("Corn") && !item.info.displayName.english.Contains("Corn Seed"))
            {
                if (Oxide.Core.Random.Range(0, 100) < LowgradeChanceCorn)
                {
                    player.inventory.GiveItem(ItemManager.CreateByName(LowgradeItemCorn, Oxide.Core.Random.Range(LowgradeAmountMinCorn, LowgradeAmountMaxCorn + 1)));
                    if (enableBroadcast)
                        SendReply(player, String.Format(msg("LowgradeCorn")));
                }
            }

	      //Get Lowgrade from pumpkin
            if (item.info.displayName.english.Contains("Pumpkin") && !item.info.displayName.english.Contains("Pumpkin Seed"))
            {
                if (Oxide.Core.Random.Range(0, 100) < LowgradeChancePumpkin)
                {
                    player.inventory.GiveItem(ItemManager.CreateByName(LowgradeItemPumpkin, Oxide.Core.Random.Range(LowgradeAmountMinPumpkin, LowgradeAmountMaxPumpkin + 1)));
                    if (enableBroadcast)
                        SendReply(player, String.Format(msg("LowgradePumpkin")));
                }
            }
        }

	    //Planted plants
        void OnGrowableGathered(GrowableEntity growable, Item item, BasePlayer player)
        {
	        //Get berry from planted hemp
            if (item.info.displayName.english.Contains("Cloth"))
            {
                if (Oxide.Core.Random.Range(0, 100) < berryChancePlanted)
                {
                    player.inventory.GiveItem(ItemManager.CreateByName(berryItem, Oxide.Core.Random.Range(berryAmountMin, berryAmountMax + 1)));
                    if (enableBroadcast)
                        SendReply(player, String.Format(msg("Berry")));
                }
            }

	        //Get Lowgrade from planted corn
            if (item.info.displayName.english.Contains("Corn") && !item.info.displayName.english.Contains("Corn Seed"))
            {
                if (Oxide.Core.Random.Range(0, 100) < LowgradeChancePlantedCorn)
                {
                    player.inventory.GiveItem(ItemManager.CreateByName(LowgradeItemCorn, Oxide.Core.Random.Range(LowgradeAmountMinCorn, LowgradeAmountMaxCorn + 1)));
                    if (enableBroadcast)
                        SendReply(player, String.Format(msg("LowgradeCorn")));
                }
            }

	        //Get Lowgrade from planted pumpkin
            if (item.info.displayName.english.Contains("Pumpkin") && !item.info.displayName.english.Contains("Pumpkin Seed"))
            {
                if (Oxide.Core.Random.Range(0, 100) < LowgradeChancePlantedPumpkin)
                {
                    player.inventory.GiveItem(ItemManager.CreateByName(LowgradeItemPumpkin, Oxide.Core.Random.Range(LowgradeAmountMinPumpkin, LowgradeAmountMaxPumpkin + 1)));
                    if (enableBroadcast)
                        SendReply(player, String.Format(msg("LowgradePumpkin")));
                }
            }
        }

        #endregion
    }
}
