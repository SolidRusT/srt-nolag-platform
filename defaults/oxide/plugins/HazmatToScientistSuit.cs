using System;                   //config
using System.Collections.Generic;   //config

namespace Oxide.Plugins
{
	[Info("Hazmat To Scientist Suit", "Krungh Crow", "1.0.1")]
	[Description("Craft scientist blue or green peacekeeper suit instead of Hazmat for players with permission.")]

/*======================================================================================================================= 
*
*   Thx to BuzZ the original creator of this plugin
*
*=======================================================================================================================*/


	public class HazmatToScientistSuit : RustPlugin
	{

        string Prefix = "[Hazmat] ";                       // CHAT PLUGIN PREFIX
        string PrefixColor = "#555555";                 // CHAT PLUGIN PREFIX COLOR
        string ChatColor = "#999999";                   // CHAT MESSAGE COLOR
        ulong SteamIDIcon = 76561199090290915;          //  STEAMID created for this plugin 76561199090290915
        private bool ConfigChanged;
        string suitname = "Revised Blue HazmatSuit";
        string suitname2 = "Revised Green HazmatSuit";
        string suitname3 = "Revised Heavy HazmatSuit";
        string suitname4 = "Revised Space Suit";
        bool loaded = false;
        bool debug = false;

        const string HTSS_use = "hazmattoscientistsuit.use";//to be able to wear a crafted Revised suit
        const string HTSS_craft = "hazmattoscientistsuit.craft";//to be able to craft any Revised suit
        const string HTSS_craftS = "hazmattoscientistsuit.craftsuit";//to be able to craft blue scientistsuit
        const string HTSS_craftGS = "hazmattoscientistsuit.craftgreensuit";//to be able to craft green scientist suit
        const string HTSS_craftH = "hazmattoscientistsuit.craftheavy";//to be able to craft heavy scientist suit
        const string HTSS_craftSS = "hazmattoscientistsuit.craftspacesuit";//to be able to craft the spacesuit (does not change players skinned hazmat suits)

        protected override void LoadDefaultConfig()

        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {

            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[Hazmat] "));                       // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#47ff6f"));                // CHAT PLUGIN PREFIX COLOR
            ChatColor = Convert.ToString(GetConfig("Chat Settings", "ChatColor", "#a0ffb5"));                    // CHAT  COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", 76561199090290915));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / 76561198857725741(version <1.0.0) /
            suitname = Convert.ToString(GetConfig("Suit Name", "Blue scientist suit", "Revised Blue HazmatSuit"));
            suitname2 = Convert.ToString(GetConfig("Suit Name", "Green scientist suit", "Revised Green HazmatSuit"));
            suitname3 = Convert.ToString(GetConfig("Suit Name", "Heavy scientist suit", "Revised Heavy HazmatSuit"));
            suitname4 = Convert.ToString(GetConfig("Suit Name", "Space suit", "Revised Space Suit"));

            if (!ConfigChanged) return;
            SaveConfig();
            ConfigChanged = false;
        }

#region MESSAGES / LANG

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {

                {"TransMsg", "Your crafted Hazmat Suit received a transformation"},
                {"BackTransMsg", "It returned to a classic Hazmat Suit."},
                {"NoPermMsg", "You are not allowed to wear this."},

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {

                {"TransMsg", "Votre tenue anti radioactivité a subie une transformation"},
                {"BackTransMsg", "La tenue est revenue à son type d'origine."},
                {"NoPermMsg", "Vous n'êtes pas autorisé à porter."},

            }, this, "fr");
        }

#endregion

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                ConfigChanged = true;
            }
            return value;
        }

		private void Init()
        {
            LoadVariables();
            permission.RegisterPermission(HTSS_use, this);
            permission.RegisterPermission(HTSS_craft, this);
            permission.RegisterPermission(HTSS_craftS, this);
            permission.RegisterPermission(HTSS_craftGS, this);
            permission.RegisterPermission(HTSS_craftH, this);
            permission.RegisterPermission(HTSS_craftSS, this);
            loaded = true;
        }

        void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            if (loaded == false){return;}
            if (item == null){return;}
            BasePlayer owner = task.owner as BasePlayer;
            bool hasperm = permission.UserHasPermission(owner.UserIDString, HTSS_craft);//if permision to craft
            int color = -253079493;//blue scientist suit
            string suitName = suitname;

            if (hasperm)
            {
                if (item.info.shortname == "hazmatsuit")
                {
                    item.UseItem();
                    ulong unull = 0;
                    if (permission.UserHasPermission(owner.UserIDString, HTSS_craftGS) == true)
                    {
                        color = -1958316066;//green scientist suit
                        suitName = suitname2;
                    }
                    else if (permission.UserHasPermission(owner.UserIDString, HTSS_craftH) == true)
                    {
                        color = -1772746857;//heavy scientist suit
                        suitName = suitname3;

                    }
                    else if (permission.UserHasPermission(owner.UserIDString, HTSS_craftSS) == true)
                    {
                        color = -560304835;//space suit
                        suitName = suitname4;
                    }
                    Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(color).itemid, 1, unull);
                    itemtogive.name = suitName; 
                    if (itemtogive == null){return;}
                    if (owner == null){return;}
                    owner.GiveItem(itemtogive);    
                    Player.Message(owner, $"<color={ChatColor}>{lang.GetMessage("TransMsg", this, owner.UserIDString)}</color>",$"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                }
            }
        }

        void CanWearItem(PlayerInventory inventory, Item item, int targetPos)
        {
            if (loaded == false){return;}
            if (item == null){return;}
            if (inventory == null){return;}
            BasePlayer wannawear = inventory.GetComponent<BasePlayer>();
            if (wannawear.IsConnected)
            {
                if (debug) Puts($"item.name -- {item.name} - item.info.shortname ---- {item.info.shortname}");
                if (item.name == null) return;
                if (item.name.Contains($"{suitname}") == true)
                {
                    bool hasperm = permission.UserHasPermission(wannawear.UserIDString, HTSS_use);//if permission to use/wear the crafted suits
                    if (hasperm == false)
                    {
                        if (item.name.Contains($"{suitname}") == true)
                        {
                            item.Remove();
                            ulong unull = 0;
                            Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                            if (itemtogive == null){return;}
                            if (wannawear == null){return;}
                            wannawear.GiveItem(itemtogive);    
                            Player.Message(wannawear, $"<color={ChatColor}>{lang.GetMessage("NoPermMsg", this, wannawear.UserIDString)}</color>\n<color=yellow>{lang.GetMessage("BackTransMsg", this, wannawear.UserIDString)}</color>",$"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                        }
                    }
                } 
            }   
        }
    }
}