using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Fishing", "Colon Blow", "1.4.11")]
    class Fishing : RustPlugin
    {

        // tweaks for null checks
        // delcared access modifiers

        #region Loadup

        private Dictionary<ulong, string> GuiInfo = new Dictionary<ulong, string>();

        private void Loaded()
        {
            lang.RegisterMessages(messages, this);
            permission.RegisterPermission("fishing.allowed", this);
            permission.RegisterPermission("fishing.makepole", this);
        }

        #endregion

        #region Configuration

        private static PluginConfig config;

        private class PluginConfig
        {
            public GlobalSettings globalSettings { get; set; }
            public DefaultCatchSettings defaultCatchSettings { get; set; }
            public FishCatchSettings fishCatchSettings { get; set; }
            public LootCatchSettings lootCatchSettings { get; set; }

            public class GlobalSettings
            {
                [JsonProperty(PropertyName = "Global - Show Fish Catch Indicator")] public bool ShowFishCatchIcon { get; set; }
                [JsonProperty(PropertyName = "Global - Enable Random Item Condition Loss when Spear/Bow Fishing ? ")] public bool enableRandomLootBox { get; set; }
                [JsonProperty(PropertyName = "Global - Enable Timer when Spear Fishing Pole ? ")] public bool enableSpearTimer { get; set; }
                [JsonProperty(PropertyName = "Global - Enable Timer when Casting Fishing Pole ? ")] public bool enableCastTimer { get; set; }
                [JsonProperty(PropertyName = "Global - Enable Random Item Condition Loss when Spear/Bow Fishing ?")] public bool enableConditionLoss { get; set; }
                [JsonProperty(PropertyName = "Global - Random Item Conditon Loss Max percent : ")] public float conditionLossMax { get; set; }
                [JsonProperty(PropertyName = "Global - Random Item Conditon Loss Min percent : ")] public float conditionLossMin { get; set; }
                [JsonProperty(PropertyName = "Global - Allow Bonus from Weapons ? ")] public bool enableWeaponModifer { get; set; }
                [JsonProperty(PropertyName = "Global - Allow Bonus from Attire ? ")] public bool enableAttireModifer { get; set; }
                [JsonProperty(PropertyName = "Global - Allow Bonus from Items ? ")] public bool enableItemModifier { get; set; }
                [JsonProperty(PropertyName = "Global - Allow Bonus from Time of Day ? ")] public bool enableTimeModifier { get; set; }
                [JsonProperty(PropertyName = "Global - Seconds to Wait after Fishing Attempt with Casting Pole, if enabled (default 6 second) : ")] public float timeReCastPole { get; set; }
                [JsonProperty(PropertyName = "Global - Seconds to Wait after Fishing Attempt with Spear/Bow, if enabled (default 6 seconds) : ")] public float timeReAttack { get; set; }
                [JsonProperty(PropertyName = "Global - Random chests will despawn themselves after this amount of time (default 200 seconds) : ")] public float lootBoxDespawnTime { get; set; }
            }

            public class DefaultCatchSettings
            {
                [JsonProperty(PropertyName = "Catch Chance - Starting default chance to catch something (Percentage)")] public int chanceDefaultAMT { get; set; }
                [JsonProperty(PropertyName = "Catch Chance - Bonus - From Weapon (Percentage)")] public int chanceBonusWeaponAMT { get; set; }
                [JsonProperty(PropertyName = "Catch Chance - Bonus - From Attire (Percentage)")] public int chanceBonusAttireAMT { get; set; }
                [JsonProperty(PropertyName = "Catch Chance - Bonus - From Item (Percentage)")] public int chacneBonusItemAMT { get; set; }
                [JsonProperty(PropertyName = "Catch Chance - Bonus - From Time of Day (Percentage)")] public int chanceBonusTimeAMT { get; set; }

                [JsonProperty(PropertyName = "Attire Bonus - if player is wearing this, gets attire bonus (default Boonie Hat) ")] public int attireBonusID { get; set; }
                [JsonProperty(PropertyName = "Item Bonus - if player has in inventory this, get item bonus (default Pookie Bear ")] public int itemBonusID { get; set; }

                [JsonProperty(PropertyName = "Time Bonus 1 - if current time is after the hour of : ")] public int timeBonus1After { get; set; }
                [JsonProperty(PropertyName = "Time Bonus 1 - and current time is before the hour of : ")] public int timeBonus1Before { get; set; }
                [JsonProperty(PropertyName = "Time Bonus 2 - if current time is after the hour of : ")] public int timeBonus2After { get; set; }
                [JsonProperty(PropertyName = "Time Bonus 2 - and current time is before the hour of : ")] public int timeBonus2Before { get; set; }
            }

            public class FishCatchSettings
            {
                [JsonProperty(PropertyName = "Chance - When Something is caught, it will be a Common Fish 1 ")] public int common1catchchance { get; set; }
                [JsonProperty(PropertyName = "Chance - When Something is caught, it will be a Common Fish 2 ")] public int common2catchchance { get; set; }
                [JsonProperty(PropertyName = "Chance - When Something is caught, it will be a UnCommon Fish ")] public int uncommoncatchchance { get; set; }
                [JsonProperty(PropertyName = "Chance - When Something is caught, it will be a Rare Fish ")] public int rarecatchchance { get; set; }
                [JsonProperty(PropertyName = "Chance - When Something is caught, it will be a Loot Box ")] public int randomitemchance { get; set; }

                [JsonProperty(PropertyName = "Amount - Common Fish 1 - When this is caught, Amount of Catch ")] public int common1catchamount { get; set; }
                [JsonProperty(PropertyName = "Amount - Common Fish 2 - When this is caught, Amount of Catch ")] public int common2catchamount { get; set; }
                [JsonProperty(PropertyName = "Amount - Uncommon Fish - When this is caught, Amount of Catch ")] public int uncommoncatchamount { get; set; }
                [JsonProperty(PropertyName = "Amount - Rare Fish - When this is caught, Amount of Catch ")] public int rarecatchamount { get; set; }

                [JsonProperty(PropertyName = "ItemID - Common Fish 1 - Item ID of Catch (default is Minnows)")] public int common1catchitemid { get; set; }
                [JsonProperty(PropertyName = "ItemID - Common Fish 2 - Item ID of Catch (default is Small Trout)")] public int common2catchitemid { get; set; }
                [JsonProperty(PropertyName = "ItemID - Uncommon Fish - Item ID of Catch (default is Small Trout)")] public int uncommoncatchitemid { get; set; }
                [JsonProperty(PropertyName = "ItemID - Rare Fish - Item ID of Catch (default is Small Trout)")] public int rarecatchitemid { get; set; }

                [JsonProperty(PropertyName = "Icon - Common Fish 1 - Icon URL to show when this fish is caught (if enabled) ")] public string common1iconurl { get; set; }
                [JsonProperty(PropertyName = "Icon - Common Fish 2 - Icon URL to show when this fish is caught (if enabled) ")] public string common2iconurl { get; set; }
                [JsonProperty(PropertyName = "Icon - Uncommon Fish - Icon URL to show when this fish is caught (if enabled) ")] public string uncommoniconurl { get; set; }
                [JsonProperty(PropertyName = "Icon - Rare Fish - Icon URL to show when this fish is caught (if enabled) ")] public string rareiconurl { get; set; }
            }

            public class LootCatchSettings
            {
                [JsonProperty(PropertyName = "Chance - If player cathces a loot box, it will be Chest Type 1 ")] public int randomlootprefab1chance { get; set; }
                [JsonProperty(PropertyName = "Chance - If player cathces a loot box, it will be Chest Type 2 ")] public int randomlootprefab2chance { get; set; }
                [JsonProperty(PropertyName = "Chance - If player cathces a loot box, it will be Chest Type 3 ")] public int randomlootprefab3chance { get; set; }
                [JsonProperty(PropertyName = "Chance - If player cathces a loot box, it will be Chest Type 4 ")] public int randomlootprefab4chance { get; set; }
                [JsonProperty(PropertyName = "Chance - If player cathces a loot box, it will be Chest Type 5 ")] public int randomlootprefab5chance { get; set; }

                [JsonProperty(PropertyName = "Chest Type 1 - Prefab string ")] public string randomlootprefab1 { get; set; }
                [JsonProperty(PropertyName = "Chest Type 2 - Prefab string ")] public string randomlootprefab2 { get; set; }
                [JsonProperty(PropertyName = "Chest Type 3 - Prefab string ")] public string randomlootprefab3 { get; set; }
                [JsonProperty(PropertyName = "Chest Type 4 - Prefab string ")] public string randomlootprefab4 { get; set; }
                [JsonProperty(PropertyName = "Chest Type 5 - Prefab string ")] public string randomlootprefab5 { get; set; }

                [JsonProperty(PropertyName = "Icon - Icon URL to show when this fish is caught (if enabled)")] public string randomitemiconurl { get; set; }
            }

            public static PluginConfig DefaultConfig() => new PluginConfig()
            {
                globalSettings = new PluginConfig.GlobalSettings
                {
                    ShowFishCatchIcon = true,
                    enableRandomLootBox = true,
                    enableSpearTimer = true,
                    enableCastTimer = true,
                    enableConditionLoss = true,
                    conditionLossMax = 5f,
                    conditionLossMin = 1f,
                    enableWeaponModifer = true,
                    enableAttireModifer = true,
                    enableItemModifier = true,
                    enableTimeModifier = true,
                    timeReCastPole = 6f,
                    timeReAttack = 6f,
                    lootBoxDespawnTime = 200f,
                },
                defaultCatchSettings = new PluginConfig.DefaultCatchSettings
                {
                    chanceDefaultAMT = 10,
                    chanceBonusWeaponAMT = 10,
                    chanceBonusAttireAMT = 10,
                    chacneBonusItemAMT = 10,
                    chanceBonusTimeAMT = 10,

                    attireBonusID = -23994173,
                    itemBonusID = -1651220691,

                    timeBonus1After = 6,
                    timeBonus1Before = 8,

                    timeBonus2After = 16,
                    timeBonus2Before = 19,
                },
                fishCatchSettings = new PluginConfig.FishCatchSettings
                {
                    common1catchchance = 40,
                    common2catchchance = 30,
                    uncommoncatchchance = 20,
                    rarecatchchance = 8,
                    randomitemchance = 2,

                    common1catchamount = 5,
                    common2catchamount = 1,
                    uncommoncatchamount = 2,
                    rarecatchamount = 5,

                    common1catchitemid = -542577259,
                    common2catchitemid = -1878764039,
                    uncommoncatchitemid = -1878764039,
                    rarecatchitemid = -1878764039,

                    common1iconurl = "http://i.imgur.com/rBEmhpg.png",
                    common2iconurl = "http://i.imgur.com/HftxU00.png",
                    uncommoniconurl = "http://i.imgur.com/xReDQM1.png",
                    rareiconurl = "http://i.imgur.com/jMZxGf1.png",
                },
                lootCatchSettings = new PluginConfig.LootCatchSettings
                {
                    randomlootprefab1chance = 40,
                    randomlootprefab2chance = 5,
                    randomlootprefab3chance = 15,
                    randomlootprefab4chance = 20,
                    randomlootprefab5chance = 20,
                    randomlootprefab1 = "assets/bundled/prefabs/radtown/crate_basic.prefab",
                    randomlootprefab2 = "assets/bundled/prefabs/radtown/crate_elite.prefab",
                    randomlootprefab3 = "assets/bundled/prefabs/radtown/crate_mine.prefab",
                    randomlootprefab4 = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                    randomlootprefab5 = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                    randomitemiconurl = "http://i.imgur.com/y2scGmZ.png",
                }
            };
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created!!");
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            SaveConfig();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Localization

        private string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            ["missedfish"] = "You Missed the fish....",
            ["notlookingatwater"] = "You must be aiming at water !!!!",
            ["notstandinginwater"] = "You must be standing in water !!!!",
            ["alreadyfishing"] = "You are already fishing !!",
            ["toosoon"] = "Please wait to try that again !!",
            ["cantmove"] = "You must stay still while fishing !!!",
            ["wrongweapon"] = "You are not holding a fishing pole !!!",
            ["correctitem"] = "You must be holding a spear to make a fishing pole !! ",
            ["commonfish1"] = "You Got a Savis Island Swordfish",
            ["commonfish2"] = "You Got a Hapis Island RazorJaw",
            ["uncommonfish1"] = "You Got a Colon BlowFish",
            ["rarefish1"] = "You Got a Craggy Island Dorkfish",
            ["randomitem"] = "You found something in the water !!!",
            ["chancetext1"] = "Your chance to catch a fish is : ",
            ["chancetext2"] = "at Current time of : "
        };

        #endregion

        #region Commands

        [ChatCommand("castpole")]
        private void cmdChatcastfishingpole(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, "fishing.allowed")) return;
            if (ValidateCastFish(player)) ProcessCastFish(player);
        }

        [ConsoleCommand("castpole")]
        private void cmdConsoleCastFishingPole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!IsAllowed(player, "fishing.allowed")) return;
                if (ValidateCastFish(player)) ProcessCastFish(player);
            }
        }

        [ChatCommand("fishchance")]
        private void cmdChatfishchance(BasePlayer player, string command, string[] args)
        {
            int catchchance = CatchFishModifier(player);
            SendReply(player, lang.GetMessage("chancetext1", this) + catchchance + "%\n");
        }

        [ChatCommand("makepole")]
        private void cmdChatMakeFishingPole(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, "fishing.makepole")) return;
            MakeFishingPole(player);
        }

        [ConsoleCommand("makepole")]
        private void cmdConsoleMakeFishingPole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!IsAllowed(player, "fishing.makepole")) return;
                MakeFishingPole(player);
            }
        }

        #endregion

        #region Craft Fishing Pole

        private void MakeFishingPole(BasePlayer player)
        {
            Item activeItem = player.GetActiveItem();
            if (activeItem != null && (activeItem.info.shortname.Contains("spear") || activeItem.info.shortname.Contains("fishingrod")))
            {
                activeItem.Remove(0f);
                ulong skinid = 1393234529;
                if (activeItem.info.shortname == "spear.stone") skinid = 1393231089;
                Item pole = ItemManager.CreateByItemID(1569882109, 1, skinid);
                if (!player.inventory.GiveItem(pole, null))
                {
                    pole.Drop(player.eyes.position, Vector3.forward, new Quaternion());
                    SendInfoMessage(player, "No Room in Inventory, Dropped New Fishing Pole !!", 5f);
                    return;
                }
                SendInfoMessage(player, "New Fishing Pole Placed in your Inventory !!", 5f);
                return;
            }
            SendReply(player, msg("correctitem", player.UserIDString));
        }

        #endregion

        #region Cast Fishing Process

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            var isfishing = player.GetComponent<FishingControl>();
            if (isfishing)
            {
                if (input.WasJustPressed(BUTTON.FORWARD)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.BACKWARD)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.RIGHT)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.LEFT)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.JUMP)) isfishing.playermoved = true;
            }
            if (UsingCastRod(player) && input.WasJustPressed(BUTTON.FIRE_PRIMARY))
            {

                if (ValidateCastFish(player)) ProcessCastFish(player);
            }
        }

        private bool ValidateCastFish(BasePlayer player)
        {
            if (IsFishing(player)) { SendReply(player, msg("alreadyfishing", player.UserIDString)); return false; }
            if (HasFishingCooldown(player)) { SendReply(player, msg("toosoon", player.UserIDString)); return false; }
            if (!LookingAtWater(player)) { SendReply(player, msg("notlookingatwater", player.UserIDString)); return false; }
            return true;
        }

        private void ProcessCastFish(BasePlayer player)
        {
            Vector3 castPointSpawn = new Vector3();
            RaycastHit castPointHit;
            if (Physics.Raycast(player.eyes.HeadRay(), out castPointHit, 50f, UnityEngine.LayerMask.GetMask("Water"))) castPointSpawn = castPointHit.point;
            var addfishing = player.gameObject.AddComponent<FishingControl>();
            addfishing.SpawnBobber(castPointSpawn);
        }

        #endregion

        #region Cast Fishing Control

        private class FishingControl : MonoBehaviour
        {
            private BasePlayer player;
            public string anchormaxstr;
            private Fishing fishing;
            public float counter;
            private BaseEntity bobber;
            public bool playermoved;
            private Vector3 bobberpos;

            private void Awake()
            {
                fishing = new Fishing();
                player = base.GetComponentInParent<BasePlayer>();
                counter = config.globalSettings.timeReCastPole;
                if (!config.globalSettings.enableCastTimer || counter < 0.1f) counter = 0.1f;
                playermoved = false;
            }

            public void SpawnBobber(Vector3 pos)
            {
                float waterheight = TerrainMeta.WaterMap.GetHeight(pos);

                pos = new Vector3(pos.x, waterheight, pos.z);
                var createdPrefab = GameManager.server.CreateEntity("assets/prefabs/tools/fishing rod/bobber/bobber.prefab", pos, Quaternion.identity);
                bobber = createdPrefab?.GetComponent<BaseEntity>();
                bobber.enableSaving = false;
                bobber.transform.eulerAngles = new Vector3(270, 0, 0);
                bobber?.Spawn();
                bobberpos = bobber.transform.position;
            }

            private void FixedUpdate()
            {
                bobberpos = bobber.transform.position;
                if (counter <= 0f) { RollForFish(bobberpos); return; }
                if (playermoved) { PlayerMoved(); return; }
                counter = counter - 0.1f;
                fishingindicator(player, counter);
            }

            private void PlayerMoved()
            {
                if (bobber != null && !bobber.IsDestroyed) { bobber.Invoke("KillMessage", 0.1f); }
                fishing.SendReply(player, fishing.msg("cantmove", player.UserIDString));
                OnDestroy();
            }

            private void RollForFish(Vector3 pos)
            {
                if (player != null) fishing.FishChanceRoll(player, pos);
                if (bobber != null && !bobber.IsDestroyed) { bobber.Invoke("KillMessage", 0.1f); }
                OnDestroy();
            }

            private string GetGUIString(float counter)
            {
                string guistring = "0.60 0.145";
                var getrefreshtime = config.globalSettings.timeReCastPole;
                if (counter < getrefreshtime * 0.1) { guistring = "0.42 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.2) { guistring = "0.44 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.3) { guistring = "0.46 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.4) { guistring = "0.48 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.5) { guistring = "0.50 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.6) { guistring = "0.52 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.7) { guistring = "0.54 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.8) { guistring = "0.56 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.9) { guistring = "0.58 0.145"; return guistring; }
                return guistring;
            }

            public void fishingindicator(BasePlayer player, float counter)
            {
                DestroyCui(player);
                string anchormaxstr = GetGUIString(counter);
                var fishingindicator = new CuiElementContainer();

                fishingindicator.Add(new CuiButton
                {
                    Button = { Command = $"", Color = "0.0 0.0 1.0 0.6" },
                    RectTransform = { AnchorMin = "0.40 0.125", AnchorMax = anchormaxstr },
                    Text = { Text = (""), FontSize = 14, Color = "1.0 1.0 1.0 0.6", Align = TextAnchor.MiddleRight }
                }, "Overall", "FishingGui");
                CuiHelper.AddUi(player, fishingindicator);
            }

            private void DestroyCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "FishingGui");
            }

            public void OnDestroy()
            {
                DestroyCui(player);
                Destroy(this);
            }
        }

        #endregion

        #region Spear Fishing Process

        private void OnPlayerAttack(BasePlayer attacker, HitInfo hitInfo)
        {
            if (attacker == null || hitInfo == null) return;
            if (IsAllowed(attacker, "fishing.allowed") || IsAllowed(attacker, "fishing.unlimited"))
            {
                if (HasFishingCooldown(attacker)) { SendReply(attacker, msg("toosoon", attacker.UserIDString)); return; }
                if (UsingSpearRod(hitInfo))
                {
                    if (LookingAtWater(attacker) || attacker.IsHeadUnderwater())
                    {
                        ProcessSpearFish(attacker, hitInfo);
                    }
                }
            }
        }

        private void ProcessSpearFish(BasePlayer attacker, HitInfo hitInfo)
        {
            if (config.globalSettings.enableConditionLoss)
            {
                float maxloss = 1f - (config.globalSettings.conditionLossMax / 100f);
                float minloss = 1f - (config.globalSettings.conditionLossMin / 100f);
                Item activeItem = attacker.GetActiveItem();
                if (activeItem != null) activeItem.condition = activeItem.condition * UnityEngine.Random.Range(minloss, maxloss);
            }
            if (attacker != null && hitInfo.HitPositionWorld != null) FishChanceRoll(attacker, hitInfo.HitPositionWorld);
            if (config.globalSettings.enableSpearTimer) attacker.gameObject.AddComponent<SpearFishingControl>();
        }

        #endregion

        #region Spear Fishing Control

        private class SpearFishingControl : MonoBehaviour
        {
            private BasePlayer player;
            public string anchormaxstr;
            private Fishing fishing;
            public float counter;

            private void Awake()
            {
                fishing = new Fishing();
                player = base.GetComponentInParent<BasePlayer>();
                counter = config.globalSettings.timeReAttack;
                if (!config.globalSettings.enableSpearTimer || counter < 0.1f) counter = 0.1f;
            }

            private void FixedUpdate()
            {
                if (counter <= 0f) OnDestroy();
                counter = counter - 0.1f;
                fishingindicator(player, counter);
            }

            private string GetGUIString(float counter)
            {
                string guistring = "0.60 0.145";
                var getrefreshtime = config.globalSettings.timeReAttack;
                if (counter < getrefreshtime * 0.1) { guistring = "0.42 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.2) { guistring = "0.44 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.3) { guistring = "0.46 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.4) { guistring = "0.48 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.5) { guistring = "0.50 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.6) { guistring = "0.52 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.7) { guistring = "0.54 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.8) { guistring = "0.56 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.9) { guistring = "0.58 0.145"; return guistring; }
                return guistring;
            }

            public void fishingindicator(BasePlayer player, float counter)
            {
                DestroyCui(player);
                var getrefreshtime = config.globalSettings.timeReAttack;
                string anchormaxstr = GetGUIString(counter);
                var fishingindicator = new CuiElementContainer();

                fishingindicator.Add(new CuiButton
                {
                    Button = { Command = $"", Color = "1.0 0.0 0.0 0.6" },
                    RectTransform = { AnchorMin = "0.40 0.125", AnchorMax = anchormaxstr },
                    Text = { Text = (""), FontSize = 14, Color = "1.0 1.0 1.0 0.6", Align = TextAnchor.MiddleRight }
                }, "Overall", "FishingGui");
                CuiHelper.AddUi(player, fishingindicator);
            }

            private void DestroyCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "FishingGui");
            }

            public void OnDestroy()
            {
                DestroyCui(player);
                Destroy(this);
            }
        }

        #endregion

        #region Fish Catch Process

        private void FishChanceRoll(BasePlayer player, Vector3 hitloc)
        {
            int roll = IntUtil.Random(1, 101);
            int totatchance = CatchFishModifier(player);

            if (roll < totatchance)
            {
                FishTypeRoll(player, hitloc);
                return;
            }
            else
                SendReply(player, msg("missedfish", player.UserIDString));
            return;
        }

        private int CatchFishModifier(BasePlayer player)
        {
            int chances = new int();
            chances = config.defaultCatchSettings.chanceDefaultAMT;
            if (config.globalSettings.enableWeaponModifer)
            {
                Item activeItem = player.GetActiveItem();
                if (activeItem != null)
                {
                    if (activeItem.info.shortname == "spear.stone" || activeItem.skin == 1393231089 || activeItem.info.shortname == "crossbow" || activeItem.info.shortname == "bow.compound")
                    {
                        chances += config.defaultCatchSettings.chanceBonusWeaponAMT;
                    }
                }
            }
            if (config.globalSettings.enableAttireModifer)
            {
                int hasBoonieOn = player.inventory.containerWear.GetAmount(config.defaultCatchSettings.attireBonusID, true);
                if (hasBoonieOn > 0) chances += config.defaultCatchSettings.chanceBonusAttireAMT;
            }
            if (config.globalSettings.enableItemModifier)
            {
                int hasPookie = player.inventory.containerMain.GetAmount(config.defaultCatchSettings.itemBonusID, true);
                if (hasPookie > 0) chances += config.defaultCatchSettings.chacneBonusItemAMT;
            }
            if (config.globalSettings.enableTimeModifier)
            {
                var currenttime = TOD_Sky.Instance.Cycle.Hour;
                if ((currenttime < config.defaultCatchSettings.timeBonus1Before && currenttime > config.defaultCatchSettings.timeBonus1After) || (currenttime < config.defaultCatchSettings.timeBonus2Before && currenttime > config.defaultCatchSettings.timeBonus2After)) chances += config.defaultCatchSettings.chanceBonusTimeAMT;
            }
            int totalchance = chances;
            return totalchance;
        }

        private void FishTypeRoll(BasePlayer player, Vector3 hitloc)
        {
            int totalfishtypechance = config.fishCatchSettings.rarecatchchance + config.fishCatchSettings.uncommoncatchchance + config.fishCatchSettings.common1catchchance + config.fishCatchSettings.common2catchchance;
            var fishtyperoll = IntUtil.Random(1, totalfishtypechance + 1);
            if (config.globalSettings.enableRandomLootBox)
            {
                if (fishtyperoll < config.fishCatchSettings.randomitemchance)
                {
                    catchFishCui(player, config.lootCatchSettings.randomitemiconurl);
                    SendReply(player, msg("randomitem", player.UserIDString));
                    SpawnLootBox(player, hitloc);
                    return;
                }
            }
            if (fishtyperoll < config.fishCatchSettings.rarecatchchance)
            {
                catchFishCui(player, config.fishCatchSettings.rareiconurl);
                SendReply(player, msg("rarefish1", player.UserIDString));
                player.inventory.GiveItem(ItemManager.CreateByItemID(config.fishCatchSettings.rarecatchitemid, config.fishCatchSettings.rarecatchamount));
                player.Command("note.inv", config.fishCatchSettings.rarecatchitemid, config.fishCatchSettings.rarecatchamount);
                return;
            }
            if (fishtyperoll < config.fishCatchSettings.rarecatchchance + config.fishCatchSettings.uncommoncatchchance)
            {
                catchFishCui(player, config.fishCatchSettings.uncommoniconurl);
                SendReply(player, msg("uncommonfish1", player.UserIDString));
                player.inventory.GiveItem(ItemManager.CreateByItemID(config.fishCatchSettings.uncommoncatchitemid, config.fishCatchSettings.uncommoncatchamount));
                player.Command("note.inv", config.fishCatchSettings.uncommoncatchitemid, config.fishCatchSettings.uncommoncatchamount);
                return;
            }
            if (fishtyperoll < config.fishCatchSettings.rarecatchchance + config.fishCatchSettings.uncommoncatchchance + config.fishCatchSettings.common2catchchance)
            {
                catchFishCui(player, config.fishCatchSettings.common2iconurl);
                SendReply(player, msg("commonfish2", player.UserIDString));
                player.inventory.GiveItem(ItemManager.CreateByItemID(config.fishCatchSettings.common2catchitemid, config.fishCatchSettings.common2catchamount));
                player.Command("note.inv", config.fishCatchSettings.common2catchitemid, config.fishCatchSettings.common2catchamount);
                return;
            }
            if (fishtyperoll < config.fishCatchSettings.rarecatchchance + config.fishCatchSettings.uncommoncatchchance + config.fishCatchSettings.common2catchchance + config.fishCatchSettings.common1catchchance)
            {
                catchFishCui(player, config.fishCatchSettings.common1iconurl);
                SendReply(player, msg("commonfish1", player.UserIDString));
                player.inventory.GiveItem(ItemManager.CreateByItemID(config.fishCatchSettings.common1catchitemid, config.fishCatchSettings.common1catchamount));
                player.Command("note.inv", config.fishCatchSettings.common1catchitemid, config.fishCatchSettings.common1catchamount);
                return;
            }
        }

        #endregion

        #region Fish Loot Process

        private void SpawnLootBox(BasePlayer player, Vector3 hitloc)
        {
            var randomlootprefab = config.lootCatchSettings.randomlootprefab1;
            var rlroll = IntUtil.Random(1, 6);
            if (rlroll == 1) randomlootprefab = config.lootCatchSettings.randomlootprefab1;
            if (rlroll == 2) randomlootprefab = config.lootCatchSettings.randomlootprefab2;
            if (rlroll == 3) randomlootprefab = config.lootCatchSettings.randomlootprefab3;
            if (rlroll == 4) randomlootprefab = config.lootCatchSettings.randomlootprefab4;
            if (rlroll == 5) randomlootprefab = config.lootCatchSettings.randomlootprefab5;

            var createdPrefab = GameManager.server.CreateEntity(randomlootprefab, hitloc);
            BaseEntity treasurebox = createdPrefab?.GetComponent<BaseEntity>();
            treasurebox.enableSaving = false;
            treasurebox?.Spawn();
            timer.Once(config.globalSettings.lootBoxDespawnTime, () => CheckTreasureDespawn(treasurebox));
        }

        private void CheckTreasureDespawn(BaseEntity treasurebox)
        {
            if (treasurebox != null) treasurebox.Kill(BaseNetworkable.DestroyMode.None);
        }

        #endregion

        #region Helpers

        private bool IsFishing(BasePlayer baseplayer)
        {
            var isfishing = baseplayer.GetComponent<FishingControl>();
            if (isfishing) return true;
            return false;
        }

        private bool HasFishingCooldown(BasePlayer baseplayer)
        {
            var incooldown = baseplayer.GetComponent<SpearFishingControl>();
            if (incooldown) return true;
            return false;
        }

        private bool UsingCastRod(BasePlayer player)
        {
            Item activeItem = player.GetActiveItem();
            if (activeItem != null && activeItem.info.shortname.Contains("fishingrod")) return true;
            return false;
        }

        private bool UsingSpearRod(HitInfo hitInfo)
        {
            if (hitInfo.WeaponPrefab.ToString().Contains("spear") || hitInfo.WeaponPrefab.ToString().Contains("bow")) return true;
            return false;
        }

        private bool LookingAtWater(BasePlayer player)
        {
            float waterHitDistance = 0;
            float groundHitDistance = 100f;
            UnityEngine.Ray ray = new UnityEngine.Ray(player.eyes.position, player.eyes.HeadForward());

            var rayHitWaterLayer = UnityEngine.Physics.RaycastAll(ray, 25f, UnityEngine.LayerMask.GetMask("Water"));
            var rayHitGroundLayer = UnityEngine.Physics.RaycastAll(ray, 25f, UnityEngine.LayerMask.GetMask("Terrain", "World", "Construction"));

            foreach (var hit in rayHitWaterLayer)
            {
                waterHitDistance = hit.distance;
            }
            foreach (var hit in rayHitGroundLayer)
            {
                groundHitDistance = hit.distance;
            }
            if (waterHitDistance > 0 && groundHitDistance == null) return true;
            if (waterHitDistance < groundHitDistance && waterHitDistance > 0) return true;
            return false;
        }

        private void SendInfoMessage(BasePlayer player, string message, float time)
        {
            player?.SendConsoleCommand("gametip.showgametip", message);
            timer.Once(time, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        private bool IsAllowed(BasePlayer player, string perm)
        {
            if (permission.UserHasPermission(player.userID.ToString(), perm)) return true;
            return false;
        }

        private class IntUtil
        {
            private static System.Random random;

            private static void Init()
            {
                if (random == null) random = new System.Random();
            }
            public static int Random(int min, int max)
            {
                Init();
                return random.Next(min, max);
            }
        }

        #endregion

        #region Hooks

        private void OnPlayerRespawned(BasePlayer player)
        {
            var isfishing = player.GetComponent<FishingControl>();
            if (isfishing) isfishing.OnDestroy();
            var hascooldown = player.GetComponent<SpearFishingControl>();
            if (hascooldown) hascooldown.OnDestroy();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var isfishing = player.GetComponent<FishingControl>();
            if (isfishing) isfishing.OnDestroy();
            var hascooldown = player.GetComponent<SpearFishingControl>();
            if (hascooldown) hascooldown.OnDestroy();
        }

        private void Unload()
        {
            DestroyAll<FishingControl>();
            DestroyAll<SpearFishingControl>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                string guiInfo;
                if (GuiInfo.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);
            }
        }

        private void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

        #endregion

        #region GUI

        private void catchFishCui(BasePlayer player, string fishicon)
        {
            if (config.globalSettings.ShowFishCatchIcon) FishingGui(player, fishicon);
        }

        private void FishingGui(BasePlayer player, string fishicon)
        {
            DestroyCui(player);

            var elements = new CuiElementContainer();
            GuiInfo[player.userID] = CuiHelper.GetGuid();

            if (config.globalSettings.ShowFishCatchIcon)
            {
                elements.Add(new CuiElement
                {
                    Name = GuiInfo[player.userID],
                    Parent = "Overlay",
                    Components =
                    {
                        new CuiRawImageComponent { Color = "1 1 1 1", Url = fishicon, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent { AnchorMin = "0.220 0.03",  AnchorMax = "0.260 0.10" }
                    }
                });
            }

            CuiHelper.AddUi(player, elements);
            timer.Once(1f, () => DestroyCui(player));
        }


        private void DestroyCui(BasePlayer player)
        {
            string guiInfo;
            if (GuiInfo.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);
        }

        #endregion
    }
}