using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

/*
 * Has WearContainer Anti Stacking Duplication features/bug fixes
 * Fixes Custom skin splitting issues + Custom Item Names. Making oranges skinsfix plugin not required/needed. 
 * Has vending machine no ammo patch toggle (so it won't affect default map vending machine not giving out stock ammo.
 * Doesn't have ammo duplication with repair bench by skin manipulation issue.
 * Doesn't have condition reset issues when re-stacking used weapons.
 * Has not being able to stack a used gun onto a new gun, only.
 * Doesn't have the weapon attachments issues
 *
 * Fixed config spelling errors
 * Fixed Visual bug on item splits ( where the players inventory UI wasn't updating properly )
 * Slight performance tweak
 * Added Updater methods.
 *
 * Updater code was derived from whitethunder's plugin.
 *
 * Fixed new NRE issues 6/8/2021
 *
 * Update 1.0.5 6/12/2021
 * Changed check config value to >= because fuck it
 *
 * updated 1.0.6 7/15/2021
 * Added feature to stop player abusing higher stack sizes when moving items from other storage containers to player inventory set from other plugins
 * Fixed Clone stack issues
 * Updated OnItemAddedToContainer code logic to fix StackItemStorage Bug Credits to Clearshot.
 *
 * Update 1.0.8
 * Fixed High hook time warnings significantly reduced
 * Fixed Condition loss comparison between float values
 * Added Ignore Admin check for F1 Spawns
 * Adjusted/fixed item moving issues from other plugins
 *
 * Update 1.0.9
 * Patched Skins auto merging into 1 stack bug
 *
 * Update 1.1.0
 * Added Liquid stacking support
 * Fixed On ItemAdded issue with stacks when using StackItemStorage
 *
 * Update 1.1.1
 * Added support for stacking Miner Hats with fuel + Candle Hats
 *
 * Update 1.1.2
 * Fixed Stacking issues with float values not matching due to unity float comparison bug
 *
 * Update 1.1.3
 * Fixed Vendor bug..
 *
 * Update 1.1.4
 * Added OnCardSwipe to fix stack loss when it hits broken stage.
 *
 * Update 1.1.5
 * Fixed High hook time hangs sometimes resulted in server crashes..
 *
 * Update 1.1.7
 * Fixes all vendor problems when booting/rebooting servers
 * Added Chat command to manually reset the vendors that you talk to only /resetvendors
 *
 * Update 1.1.8
 * Pulled due to false reports.
 * Reverted back to original patch of 1.1.7
 * 
 * Update 1.1.9
 * Fixes custom items that have different custom names applied with the same skinids from stacking.
 * Fixes resetting stacks to default if ( true ) in config.
 *
 * Update 1.2.0
 * Added Global Category Group Stack Setting options
 *
 * Update 1.3.0
 * Added Editor UI
 * Swapped back to Rust Plugin
 * Added Image Library Support
 * Added New Config Options
 * Added Search Bar + fade out.
 * Added additional checks/options made some performance improvements
 *
 * Update 1.3.2
 * Blocks player movements while using editor
 * Updated console output responses
 * Updated UI Systems and fixed a bug relating to first time opening
 * ( it was double opening )
 *
 * Update 1.3.21
 * Updated Input checks
 * Fixed spectating players while using the UI Editor..
 *
 * Update 1.3.22
 * Expanded the UI Editor Search parameters a bit
 *
 * Update 1.3.3
 * Fixed Missing Defaults
 * Fixed UI Not showing correctly between Multipliers and Modified items
 *
 * aiming.module.mlrs, MLRS Aiming Module
 * mlrs, MLRS
 * ammo.rocket.mlrs, MLRS Rocket
 * lumberjack hoodie, Lumberjack Hoodie
 * frankensteintable, Frankenstein Table
 * carvable.pumpkin, Carvable Pumpkin
 * frankensteins.monster.01.head, Light Frankenstein Head
 * frankensteins.monster.01.legs, Light Frankenstein Legs
 * frankensteins.monster.01.torso, Light Frankenstein Torso
 * frankensteins.monster.02.head, Medium Frankenstein Head
 * frankensteins.monster.02.legs, Medium Frankenstein Legs
 * frankensteins.monster.02.torso, Medium Frankenstein Torso
 * frankensteins.monster.03.head, Heavy Frankenstein Head
 * frankensteins.monster.03.legs, Heavy Frankenstein Legs
 * frankensteins.monster.03.torso, Heavy Frankenstein Torso
 * sunglasses02black, Sunglasses
 * sunglasses02camo, Sunglasses
 * sunglasses02red, Sunglasses
 * sunglasses03black, Sunglasses
 * sunglasses03chrome, Sunglasses
 * sunglasses03gold, Sunglasses
 * captainslog, Captain's Log
 * fishing.tackle, Fishing Tackle
 * bottle.vodka, Vodka Bottle
 * vehicle.2mod.camper, Camper Vehicle Module
 * skull, Skull
 * rifle.lr300, LR-300 Assault Rifle
 *
 * Update 1.3.4
 * Added new reset Command for the search bar
 * Added new set Command for the search bar
 * Updated Category Descriptions
 * ( Type reset in any category search bar and it will reset that whole category for you! )
 * ( Type set 8 in any category search bar and it will set that whole category for you to 8! )
 *
 * Update 1.3.5
 * Re-fixed vendor problem..
 *
 * Notice
 * I will not be providing any more updates for this, this month.
 *
 * Update 1.3.7
 * Fixes ( Stack problems with different stack sizes for different storages )
 * WARNING!
 * Potentially heavy update.. idk if this will crash 1000000mil x Servers or not!
 *
 * Update 1.3.71
 * Code Cleanup + improvements / back to permanent
 * Fixed Reloading problems
 * Added some missed checks for ImageLibrary to resolve
 * Removed Admin Toggle Config Option. ( it's hardcoded to ignore admins )
 *
 * Update 1.4.0
 * Partial UI Re-write/Optimizations!
 * More config options!
 * New command sluts! /stackmodifiercolor
 * Fixed Search bar commands! ( reset & set ) now work excellently!
 * Search parameters work 200% better than before!
 * Added Patch for Nivex
 * This update was brought to you with baz!
 *
 * Update 1.4.1
 * Fixed UI Constantly Re-Updating the Multiplier Descriptions ( when not needed ).
 * Another UI Performance update/Tweak
 * Fixed Sunglasses Display Names
 * Added Lang API Support from codefling https://codefling.com/plugins/lang-api?tab=details
 *
 * update 1.4.2
 * Re-fixed ignore admin check
 *
 * update 1.4.4
 * Optional Update
 * Re-designed UI System
 * Added All Category
 *
 * update 1.4.7
 * Updated for rust update
 * Added the following new items
 * rhib
 * rowboat
 * snowmobile
 * snowmobiletomaha
 * hazmatsuit.arcticsuit
 * hazmatsuit_scientist_arctic
 * spraycan
 * rifle.ak.ice
*/

namespace Oxide.Plugins
{
    [Info("Stack Modifier", "Khan", "1.4.7")]
    [Description("Modify item stack sizes, includes UI Editor")]
    public class StackModifier : RustPlugin
    {
        #region Fields

        [PluginReference] Plugin ImageLibrary, LangAPI;

        private bool _isRestartSM = true;
        private bool _isEditorReady;
        private List<string> _opensm = new List<string>();
        private Hash<ulong, int> _editorPageSM = new Hash<ulong, int>();
        private Dictionary<string, string> _stackModifierImageList;
        private List<KeyValuePair<string, ulong>> _stackModifierIcons;

        private const string ByPass = "stackmodifier.bypass";
        private const string Admin = "stackmodifier.admin";

        private readonly Dictionary<string, int> _vending = new Dictionary<string, int>
        {
            {"mixingtable", 1},
            {"diving.mask", 1},
            {"diving.tank", 1},
            {"diving.fins", 1},
            {"fishingrod.handmade", 1}
        };

        private readonly Dictionary<string, int> _defaults = new Dictionary<string, int>
        {
            {"hat.wolf", 1},
            {"fogmachine", 1},
            {"strobelight", 1},
            {"kayak", 1},
            {"minihelicopter.repair", 1},
            {"aiming.module.mlrs", 1},
            {"mlrs", 1},
            {"ammo.rocket.mlrs", 6},
            {"scraptransportheli.repair", 1},
            {"submarineduo", 1},
            {"submarinesolo", 1},
            {"workcart", 1},
            {"ammo.grenadelauncher.buckshot", 24},
            {"ammo.grenadelauncher.he", 12},
            {"ammo.grenadelauncher.smoke", 12},
            {"arrow.hv", 64},
            {"arrow.wooden", 64},
            {"arrow.bone", 64},
            {"arrow.fire", 64},
            {"ammo.handmade.shell", 64},
            {"ammo.nailgun.nails", 64},
            {"ammo.pistol", 128},
            {"ammo.pistol.fire", 128},
            {"ammo.pistol.hv", 128},
            {"ammo.rifle", 128},
            {"ammo.rifle.explosive", 128},
            {"ammo.rifle.incendiary", 128},
            {"ammo.rifle.hv", 128},
            {"ammo.rocket.basic", 3},
            {"ammo.rocket.fire", 3},
            {"ammo.rocket.hv", 3},
            {"ammo.rocket.smoke", 3},
            {"ammo.shotgun", 64},
            {"ammo.shotgun.fire", 64},
            {"ammo.shotgun.slug", 32},
            {"speargun.spear", 64},
            {"submarine.torpedo.straight", 100},
            {"door.double.hinged.metal", 1},
            {"door.double.hinged.toptier", 1},
            {"door.double.hinged.wood", 1},
            {"door.hinged.metal", 1},
            {"door.hinged.toptier", 1},
            {"door.hinged.wood", 1},
            {"floor.grill", 10},
            {"floor.ladder.hatch", 1},
            {"floor.triangle.grill", 10},
            {"floor.triangle.ladder.hatch", 1},
            {"gates.external.high.stone", 1},
            {"gates.external.high.wood", 1},
            {"ladder.wooden.wall", 5},
            {"wall.external.high.stone", 10},
            {"wall.external.high", 10},
            {"wall.frame.cell.gate", 1},
            {"wall.frame.cell", 10},
            {"wall.frame.fence.gate", 1},
            {"wall.frame.fence", 10},
            {"wall.frame.garagedoor", 1},
            {"wall.frame.netting", 5},
            {"wall.frame.shopfront", 1},
            {"wall.frame.shopfront.metal", 1},
            {"wall.window.bars.metal", 10},
            {"wall.window.bars.toptier", 10},
            {"wall.window.bars.wood", 10},
            {"shutter.metal.embrasure.a", 20},
            {"shutter.metal.embrasure.b", 20},
            {"wall.window.glass.reinforced", 10},
            {"shutter.wood.a", 20},
            {"watchtower.wood", 5},
            {"diving.fins", 1},
            {"diving.mask", 1},
            {"diving.tank", 1},
            {"diving.wetsuit", 1},
            {"boots.frog", 1},
            {"barrelcostume", 1},
            {"cratecostume", 1},
            {"hat.gas.mask", 1},
            {"burlap.gloves.new", 1},
            {"burlap.gloves", 1},
            {"roadsign.gloves", 1},
            {"tactical.gloves", 1},
            {"ghostsheet", 1},
            {"halloween.mummysuit", 1},
            {"scarecrow.suit", 1},
            {"scarecrowhead", 1},
            {"attire.hide.helterneck", 1},
            {"hat.beenie", 1},
            {"hat.boonie", 1},
            {"bucket.helmet", 1},
            {"burlap.headwrap", 1},
            {"hat.candle", 1},
            {"hat.cap", 1},
            {"clatter.helmet", 1},
            {"coffeecan.helmet", 1},
            {"deer.skull.mask", 1},
            {"heavy.plate.helmet", 1},
            {"hat.miner", 1},
            {"partyhat", 1},
            {"riot.helmet", 1},
            {"wood.armor.helmet", 1},
            {"hoodie", 1},
            {"bone.armor.suit", 1},
            {"heavy.plate.jacket", 1},
            {"jacket.snow", 1},
            {"jacket", 1},
            {"wood.armor.jacket", 1},
            {"lumberjack hoodie", 1},
            {"mask.balaclava", 1},
            {"mask.bandana", 1},
            {"metal.facemask", 1},
            {"nightvisiongoggles", 1},
            {"attire.ninja.suit", 1},
            {"burlap.trousers", 1},
            {"heavy.plate.pants", 1},
            {"attire.hide.pants", 1},
            {"roadsign.kilt", 1},
            {"pants.shorts", 1},
            {"wood.armor.pants", 1},
            {"pants", 1},
            {"attire.hide.poncho", 1},
            {"burlap.shirt", 1},
            {"shirt.collared", 1},
            {"attire.hide.vest", 1},
            {"shirt.tanktop", 1},
            {"shoes.boots", 1},
            {"burlap.shoes", 1},
            {"attire.hide.boots", 1},
            {"attire.hide.skirt", 1},
            {"attire.banditguard", 1},
            {"hazmatsuit", 1},
            {"hazmatsuit_scientist", 1},
            {"hazmatsuit_scientist_peacekeeper", 1},
            {"hazmatsuit.spacesuit", 1},
            {"scientistsuit_heavy", 1},
            {"jumpsuit.suit.blue", 1},
            {"jumpsuit.suit", 1},
            {"tshirt.long", 1},
            {"tshirt", 1},
            {"metal.plate.torso", 1},
            {"roadsign.jacket", 1},
            {"bleach", 20},
            {"ducttape", 20},
            {"carburetor1", 5},
            {"carburetor2", 5},
            {"carburetor3", 5},
            {"crankshaft1", 5},
            {"crankshaft2", 5},
            {"crankshaft3", 5},
            {"piston1", 10},
            {"piston2", 10},
            {"piston3", 10},
            {"sparkplug1", 20},
            {"sparkplug2", 20},
            {"sparkplug3", 20},
            {"valve1", 15},
            {"valve2", 15},
            {"valve3", 15},
            {"fuse", 10},
            {"gears", 20},
            {"glue", 10},
            {"metalblade", 20},
            {"metalpipe", 20},
            {"propanetank", 5},
            {"roadsigns", 20},
            {"rope", 50},
            {"sewingkit", 20},
            {"sheetmetal", 20},
            {"metalspring", 20},
            {"sticks", 100},
            {"tarp", 20},
            {"techparts", 50},
            {"riflebody", 10},
            {"semibody", 10},
            {"smgbody", 10},
            {"barricade.concrete", 10},
            {"barricade.wood.cover", 10},
            {"barricade.metal", 10},
            {"barricade.sandbags", 10},
            {"barricade.stone", 10},
            {"barricade.wood", 10},
            {"barricade.woodwire", 10},
            {"bbq", 1},
            {"trap.bear", 3},
            {"bed", 1},
            {"campfire", 1},
            {"ceilinglight", 10},
            {"chair", 5},
            {"composter", 1},
            {"computerstation", 1},
            {"drone", 1},
            {"dropbox", 5},
            {"elevator", 5},
            {"fireplace.stone", 1},
            {"firework.boomer.blue", 20},
            {"firework.boomer.champagne", 20},
            {"firework.boomer.green", 20},
            {"firework.boomer.orange", 20},
            {"firework.boomer.red", 20},
            {"firework.boomer.violet", 20},
            {"firework.romancandle.blue", 20},
            {"firework.romancandle.green", 20},
            {"firework.romancandle.red", 20},
            {"firework.romancandle.violet", 20},
            {"firework.volcano", 20},
            {"firework.volcano.red", 20},
            {"firework.volcano.violet", 20},
            {"spikes.floor", 10},
            {"frankensteintable", 1},
            {"fridge", 1},
            {"furnace.large", 1},
            {"furnace", 1},
            {"hitchtroughcombo", 1},
            {"habrepair", 1},
            {"jackolantern.angry", 1},
            {"jackolantern.happy", 1},
            {"trap.landmine", 5},
            {"lantern", 1},
            {"box.wooden.large", 1},
            {"water.barrel", 1},
            {"locker", 1},
            {"mailbox", 1},
            {"mixingtable", 1},
            {"modularcarlift", 1},
            {"mining.pumpjack", 1},
            {"small.oil.refinery", 1},
            {"planter.large", 10},
            {"planter.small", 10},
            {"electric.audioalarm", 5},
            {"smart.alarm", 5},
            {"smart.switch", 5},
            {"storage.monitor", 1},
            {"electric.battery.rechargable.large", 1},
            {"electric.battery.rechargable.medium", 1},
            {"electric.battery.rechargable.small", 1},
            {"electric.button", 5},
            {"electric.counter", 5},
            {"electric.hbhfsensor", 1},
            {"electric.laserdetector", 5},
            {"electric.pressurepad", 1},
            {"electric.doorcontroller", 5},
            {"electric.heater", 5},
            {"fluid.combiner", 5},
            {"fluid.splitter", 5},
            {"fluid.switch", 1},
            {"electric.andswitch", 5},
            {"electric.blocker", 5},
            {"electrical.branch", 5},
            {"electrical.combiner", 5},
            {"electrical.memorycell", 5},
            {"electric.orswitch", 5},
            {"electric.random.switch", 5},
            {"electric.rf.broadcaster", 1},
            {"electric.rf.receiver", 1},
            {"electric.xorswitch", 5},
            {"electric.fuelgenerator.small", 1},
            {"electric.generator.small", 1},
            {"electric.solarpanel.large", 3},
            {"electric.igniter", 3},
            {"electric.flasherlight", 5},
            {"electric.simplelight", 1},
            {"electric.sirenlight", 5},
            {"powered.water.purifier", 1},
            {"electric.switch", 5},
            {"electric.splitter", 5},
            {"electric.sprinkler", 10},
            {"electric.teslacoil", 3},
            {"electric.timer", 5},
            {"electric.cabletunnel", 1},
            {"waterpump", 1},
            {"mining.quarry", 1},
            {"target.reactive", 1},
            {"box.repair.bench", 1},
            {"research.table", 1},
            {"rug.bear", 1},
            {"rug", 1},
            {"searchlight", 1},
            {"secretlabchair", 5},
            {"shelves", 10},
            {"sign.hanging.banner.large", 5},
            {"sign.hanging", 5},
            {"sign.hanging.ornate", 5},
            {"sign.pictureframe.landscape", 5},
            {"sign.pictureframe.portrait", 5},
            {"sign.pictureframe.tall", 5},
            {"sign.pictureframe.xl", 5},
            {"sign.pictureframe.xxl", 5},
            {"sign.pole.banner.large", 5},
            {"sign.post.double", 5},
            {"sign.post.single", 5},
            {"sign.post.town", 5},
            {"sign.post.town.roof", 5},
            {"sign.wooden.huge", 5},
            {"sign.wooden.large", 5},
            {"sign.wooden.medium", 5},
            {"sign.wooden.small", 5},
            {"guntrap", 1},
            {"sleepingbag", 1},
            {"stash.small", 5},
            {"sofa", 2},
            {"spinner.wheel", 1},
            {"fishtrap.small", 5},
            {"table", 1},
            {"workbench1", 1},
            {"workbench2", 1},
            {"workbench3", 1},
            {"cupboard.tool", 1},
            {"tunalight", 1},
            {"vending.machine", 1},
            {"water.catcher.large", 1},
            {"water.catcher.small", 1},
            {"water.purifier", 1},
            {"generator.wind.scrap", 1},
            {"box.wooden", 1},
            {"apple", 10},
            {"apple.spoiled", 1},
            {"black.raspberries", 1},
            {"blueberries", 20},
            {"botabag", 1},
            {"grub", 20},
            {"worm", 20},
            {"cactusflesh", 10},
            {"can.beans", 10},
            {"can.tuna", 10},
            {"chocholate", 10},
            {"fish.anchovy", 10},
            {"fish.catfish", 5},
            {"fish.cooked", 20},
            {"fish.raw", 20},
            {"fish.herring", 10},
            {"fish.minnows", 10},
            {"fish.orangeroughy", 5},
            {"fish.salmon", 10},
            {"fish.sardine", 10},
            {"fish.smallshark", 5},
            {"fish.troutsmall", 10},
            {"fish.yellowperch", 10},
            {"granolabar", 10},
            {"chicken.burned", 20},
            {"chicken.cooked", 20},
            {"chicken.raw", 20},
            {"chicken.spoiled", 20},
            {"deermeat.burned", 20},
            {"deermeat.cooked", 20},
            {"deermeat.raw", 20},
            {"horsemeat.burned", 20},
            {"horsemeat.cooked", 20},
            {"horsemeat.raw", 20},
            {"humanmeat.burned", 20},
            {"humanmeat.cooked", 20},
            {"humanmeat.raw", 20},
            {"humanmeat.spoiled", 20},
            {"bearmeat.burned", 20},
            {"bearmeat.cooked", 20},
            {"bearmeat", 20},
            {"wolfmeat.burned", 20},
            {"wolfmeat.cooked", 20},
            {"wolfmeat.raw", 20},
            {"wolfmeat.spoiled", 20},
            {"meat.pork.burned", 20},
            {"meat.pork.cooked", 20},
            {"meat.boar", 20},
            {"mushroom", 10},
            {"jar.pickle", 10},
            {"smallwaterbottle", 1},
            {"waterjug", 1},
            {"fun.bass", 1},
            {"fun.cowbell", 1},
            {"drumkit", 1},
            {"fun.flute", 1},
            {"fun.guitar", 1},
            {"fun.jerrycanguitar", 1},
            {"piano", 1},
            {"fun.tambourine", 1},
            {"fun.trumpet", 1},
            {"fun.tuba", 1},
            {"xylophone", 1},
            {"car.key", 1},
            {"door.key", 1},
            {"lock.key", 10},
            {"lock.code", 10},
            {"blueprintbase", 1000},
            {"chineselantern", 1},
            {"dragondoorknocker", 1},
            {"hat.dragonmask", 1},
            {"newyeargong", 1},
            {"hat.oxmask", 1},
            {"hat.ratmask", 1},
            {"lunar.firecrackers", 5},
            {"arcade.machine.chippy", 1},
            {"door.closer", 1},
            {"attire.bunnyears", 1},
            {"attire.bunny.onesie", 1},
            {"hat.bunnyhat", 1},
            {"easterdoorwreath", 1},
            {"easterbasket", 1},
            {"rustige_egg_a", 1},
            {"rustige_egg_b", 1},
            {"rustige_egg_c", 1},
            {"rustige_egg_d", 1},
            {"rustige_egg_e", 1},
            {"attire.nesthat", 1},
            {"easter.bronzeegg", 10},
            {"easter.goldegg", 10},
            {"easter.paintedeggs", 1000},
            {"easter.silveregg", 10},
            {"halloween.candy", 1000},
            {"largecandles", 1},
            {"smallcandles", 1},
            {"carvable.pumpkin", 1},
            {"coffin.storage", 1},
            {"cursedcauldron", 1},
            {"gravestone", 1},
            {"woodcross", 1},
            {"frankensteins.monster.01.head", 1},
            {"frankensteins.monster.01.legs", 1},
            {"frankensteins.monster.01.torso", 1},
            {"frankensteins.monster.02.head", 1},
            {"frankensteins.monster.02.legs", 1},
            {"frankensteins.monster.02.torso", 1},
            {"frankensteins.monster.03.head", 1},
            {"frankensteins.monster.03.legs", 1},
            {"frankensteins.monster.03.torso", 1},
            {"wall.graveyard.fence", 10},
            {"halloween.lootbag.large", 10},
            {"halloween.lootbag.medium", 10},
            {"halloween.lootbag.small", 10},
            {"pumpkinbasket", 1},
            {"scarecrow", 5},
            {"skullspikes.candles", 1},
            {"skullspikes.pumpkin", 1},
            {"skullspikes", 1},
            {"skulldoorknocker", 1},
            {"skull_fire_pit", 1},
            {"spiderweb", 10},
            {"spookyspeaker", 1},
            {"halloween.surgeonsuit", 1},
            {"skull.trophy.jar", 1},
            {"skull.trophy.jar2", 1},
            {"skull.trophy.table", 1},
            {"skull.trophy", 1},
            {"movembermoustachecard", 1},
            {"movembermoustache", 1},
            {"note", 1},
            {"skull.human", 1},
            {"abovegroundpool", 1},
            {"beachchair", 1},
            {"beachparasol", 1},
            {"beachtable", 1},
            {"beachtowel", 1},
            {"boogieboard", 1},
            {"innertube", 1},
            {"innertube.horse", 1},
            {"innertube.unicorn", 1},
            {"tool.instant_camera", 1},
            {"paddlingpool", 1},
            {"photo", 1},
            {"photoframe.landscape", 1},
            {"photoframe.large", 1},
            {"photoframe.portrait", 1},
            {"sunglasses02black", 1},
            {"sunglasses02camo", 1},
            {"sunglasses02red", 1},
            {"sunglasses03black", 1},
            {"sunglasses03chrome", 1},
            {"sunglasses03gold", 1},
            {"sunglasses", 1},
            {"gun.water", 1},
            {"pistol.water", 1},
            {"twitchsunglasses", 1},
            {"twitch.headset", 1},
            {"hobobarrel", 1},
            {"door.hinged.industrial.a", 1},
            {"candycaneclub", 1},
            {"xmas.lightstring", 20},
            {"xmas.door.garland", 10},
            {"candycane", 1},
            {"giantcandycanedecor", 1},
            {"wall.ice.wall", 10},
            {"wall.external.high.ice", 10},
            {"giantlollipops", 1},
            {"sign.neon.125x125", 5},
            {"sign.neon.125x215.animated", 1},
            {"sign.neon.125x215", 5},
            {"sign.neon.xl.animated", 5},
            {"sign.neon.xl", 1},
            {"pookie.bear", 1},
            {"xmas.lightstring.advanced", 150},
            {"coal", 1},
            {"xmas.present.large", 1},
            {"xmas.present.medium", 5},
            {"xmas.present.small", 10},
            {"sled.xmas", 1},
            {"sled", 1},
            {"snowmachine", 1},
            {"snowball", 1},
            {"ammo.snowballgun", 128},
            {"snowballgun", 1},
            {"snowman", 5},
            {"stocking.large", 1},
            {"stocking.small", 5},
            {"attire.reindeer.headband", 1},
            {"santabeard", 1},
            {"santahat", 1},
            {"xmas.window.garland", 10},
            {"wrappedgift", 1},
            {"wrappingpaper", 1},
            {"xmasdoorwreath", 1},
            {"xmas.decoration.baubels", 1},
            {"xmas.decoration.candycanes", 1},
            {"xmas.decoration.gingerbreadmen", 1},
            {"xmas.decoration.lights", 1},
            {"xmas.decoration.pinecone", 1},
            {"xmas.decoration.star", 1},
            {"xmas.decoration.tinsel", 1},
            {"xmas.tree", 1},
            {"captainslog", 1},
            {"fishing.tackle", 1},
            {"bottle.vodka", 1},
            {"autoturret", 1},
            {"flameturret", 1},
            {"gloweyes", 1},
            {"ammo.rocket.sam", 1000},
            {"samsite", 1},
            {"black.berry", 20},
            {"clone.black.berry", 50},
            {"seed.black.berry", 50},
            {"blue.berry", 20},
            {"clone.blue.berry", 50},
            {"seed.blue.berry", 50},
            {"green.berry", 20},
            {"clone.green.berry", 50},
            {"seed.green.berry", 50},
            {"red.berry", 20},
            {"clone.red.berry", 50},
            {"seed.red.berry", 50},
            {"white.berry", 20},
            {"clone.white.berry", 50},
            {"seed.white.berry", 50},
            {"yellow.berry", 20},
            {"clone.yellow.berry", 50},
            {"seed.yellow.berry", 50},
            {"corn", 20},
            {"clone.corn", 50},
            {"seed.corn", 50},
            {"clone.hemp", 50},
            {"seed.hemp", 50},
            {"potato", 20},
            {"clone.potato", 50},
            {"seed.potato", 50},
            {"pumpkin", 20},
            {"clone.pumpkin", 50},
            {"seed.pumpkin", 50},
            {"fat.animal", 1000},
            {"battery.small", 1},
            {"blood", 1000},
            {"bone.fragments", 1000},
            {"cctv.camera", 64},
            {"charcoal", 1000},
            {"cloth", 1000},
            {"crude.oil", 500},
            {"diesel_barrel", 20},
            {"can.beans.empty", 10},
            {"can.tuna.empty", 10},
            {"explosives", 100},
            {"fertilizer", 1000},
            {"gunpowder", 1000},
            {"horsedung", 100},
            {"hq.metal.ore", 1000},
            {"metal.refined", 100},
            {"leather", 1000},
            {"lowgradefuel", 500},
            {"metal.fragments", 1000},
            {"metal.ore", 1000},
            {"paper", 1000},
            {"plantfiber", 1000},
            {"researchpaper", 1000},
            {"scrap", 1000},
            {"stones", 1000},
            {"sulfur.ore", 1000},
            {"sulfur", 1000},
            {"targeting.computer", 64},
            {"skull.wolf", 1},
            {"wood", 1000},
            {"healingtea.advanced", 10},
            {"healingtea", 10},
            {"healingtea.pure", 10},
            {"maxhealthtea.advanced", 10},
            {"maxhealthtea", 10},
            {"maxhealthtea.pure", 10},
            {"oretea.advanced", 10},
            {"oretea", 10},
            {"oretea.pure", 10},
            {"radiationremovetea.advanced", 10},
            {"radiationremovetea", 10},
            {"radiationremovetea.pure", 10},
            {"radiationresisttea.advanced", 10},
            {"radiationresisttea", 10},
            {"radiationresisttea.pure", 10},
            {"scraptea.advanced", 10},
            {"scraptea", 10},
            {"scraptea.pure", 10},
            {"woodtea.advanced", 10},
            {"woodtea", 10},
            {"woodtea.pure", 10},
            {"antiradpills", 10},
            {"tool.binoculars", 1},
            {"explosive.timed", 10},
            {"tool.camera", 1},
            {"rf.detonator", 1},
            {"fishingrod.handmade", 1},
            {"flare", 5},
            {"flashlight.held", 1},
            {"geiger.counter", 1},
            {"hosetool", 1},
            {"jackhammer", 1},
            {"keycard_blue", 1},
            {"keycard_green", 1},
            {"keycard_red", 1},
            {"largemedkit", 1},
            {"map", 1},
            {"syringe.medical", 2},
            {"rf_pager", 1},
            {"building.planner", 1},
            {"grenade.smoke", 3},
            {"supply.signal", 1},
            {"surveycharge", 10},
            {"wiretool", 1},
            {"vehicle.chassis.2mod", 1},
            {"vehicle.chassis.3mod", 1},
            {"vehicle.chassis.4mod", 1},
            {"vehicle.chassis", 1},
            {"vehicle.1mod.cockpit", 1},
            {"vehicle.1mod.cockpit.armored", 1},
            {"vehicle.1mod.cockpit.with.engine", 1},
            {"vehicle.1mod.engine", 1},
            {"vehicle.1mod.flatbed", 1},
            {"vehicle.1mod.passengers.armored", 1},
            {"vehicle.1mod.rear.seats", 1},
            {"vehicle.1mod.storage", 1},
            {"vehicle.1mod.taxi", 1},
            {"vehicle.2mod.camper", 1},
            {"vehicle.2mod.flatbed", 1},
            {"vehicle.2mod.fuel.tank", 1},
            {"vehicle.2mod.passengers", 1},
            {"vehicle.module", 1},
            {"boombox", 1},
            {"fun.boomboxportable", 1},
            {"cassette", 1},
            {"cassette.medium", 1},
            {"cassette.short", 1},
            {"fun.casetterecorder", 1},
            {"discoball", 5},
            {"discofloor", 5},
            {"discofloor.largetiles", 5},
            {"connected.speaker", 5},
            {"laserlight", 5},
            {"megaphone", 1},
            {"microphonestand", 5},
            {"mobilephone", 1},
            {"soundlight", 5},
            {"telephone", 1},
            {"weapon.mod.8x.scope", 1},
            {"weapon.mod.flashlight", 1},
            {"weapon.mod.holosight", 1},
            {"weapon.mod.lasersight", 1},
            {"weapon.mod.muzzleboost", 1},
            {"weapon.mod.muzzlebrake", 1},
            {"weapon.mod.simplesight", 1},
            {"weapon.mod.silencer", 1},
            {"weapon.mod.small.scope", 1},
            {"rifle.ak", 1},
            {"bandage", 3},
            {"grenade.beancan", 5},
            {"rifle.bolt", 1},
            {"bone.club", 1},
            {"knife.bone", 1},
            {"bow.hunting", 1},
            {"cakefiveyear", 1},
            {"chainsaw", 1},
            {"salvaged.cleaver", 1},
            {"bow.compound", 1},
            {"crossbow", 1},
            {"shotgun.double", 1},
            {"pistol.eoka", 1},
            {"grenade.f1", 5},
            {"flamethrower", 1},
            {"multiplegrenadelauncher", 1},
            {"knife.butcher", 1},
            {"pitchfork", 1},
            {"sickle", 1},
            {"skull", 1},
            {"hammer", 1},
            {"hatchet", 1},
            {"knife.combat", 1},
            {"rifle.l96", 1},
            {"rifle.lr300", 1},
            {"lmg.m249", 1},
            {"rifle.m39", 1},
            {"pistol.m92", 1},
            {"mace", 1},
            {"machete", 1},
            {"smg.mp5", 1},
            {"pistol.nailgun", 1},
            {"paddle", 1},
            {"pickaxe", 1},
            {"shotgun.waterpipe", 1},
            {"pistol.python", 1},
            {"pistol.revolver", 1},
            {"rock", 1},
            {"rocket.launcher", 1},
            {"axe.salvaged", 1},
            {"hammer.salvaged", 1},
            {"icepick.salvaged", 1},
            {"explosive.satchel", 10},
            {"shotgun.pump", 1},
            {"pistol.semiauto", 1},
            {"rifle.semiauto", 1},
            {"smg.2", 1},
            {"shotgun.spas12", 1},
            {"speargun", 1},
            {"stonehatchet", 1},
            {"stone.pickaxe", 1},
            {"spear.stone", 1},
            {"longsword", 1},
            {"salvaged.sword", 1},
            {"smg.thompson", 1},
            {"toolgun", 1},
            {"torch", 1},
            {"bucket.water", 1},
            {"spear.wooden", 1},
            {"horse.armor.roadsign", 1},
            {"horse.armor.wood", 1},
            {"horse.saddle", 1},
            {"horse.saddlebag", 1},
            {"horse.shoes.advanced", 1},
            {"horse.shoes.basic", 1},
            {"hazmatsuit.nomadsuit", 1},
            {"sofa.pattern", 2},
            {"factorydoor", 1},
            {"industrial.wall.light.green", 10},
            {"industrial.wall.light", 10},
            {"industrial.wall.light.red", 10},
            {"firework.boomer.pattern", 20},
            {"xmas.advent", 1},
            {"xmas.double.door.garland", 10},
            {"attire.snowman.helmet", 1},
            {"skylantern", 20},
            {"skylantern.skylantern.green", 20},
            {"skylantern.skylantern.orange", 20},
            {"skylantern.skylantern.purple", 20},
            {"skylantern.skylantern.red", 20},
            {"hat.tigermask", 1},
            {"rhib", 1},
            {"rowboat", 1},
            {"snowmobile", 1},
            {"snowmobiletomaha", 1},
            {"hazmatsuit.arcticsuit", 1},
            {"hazmatsuit_scientist_arctic", 1},
            {"spraycan", 1},
            {"rifle.ak.ice", 1},
        };

        private readonly HashSet<string> _exclude = new HashSet<string>
        {
            "water",
            "water.salt",
            "cardtable",
            "ammo.snowballgun"
        };

        private readonly Dictionary<string, string> _corrections = new Dictionary<string, string>
        {
            {"sunglasses02black", "Sunglasses Style 2"},
            {"sunglasses02camo", "Sunglasses Camo"},
            {"sunglasses02red", "Sunglasses Red"},
            {"sunglasses03black", "Sunglasses Style 3"},
            {"sunglasses03chrome", "Sunglasses Chrome"},
            {"sunglasses03gold", "Sunglasses Gold"},
            {"twitchsunglasses", "Sunglasses Purple"},
        };

        #endregion

        #region Config

        private PluginConfig _config;

        private Dictionary<string, string> _itemMap = new Dictionary<string, string>();

        private void CheckConfig()
        {
            _stackModifierIcons = new List<KeyValuePair<string, ulong>>();
            foreach (ItemDefinition item in ItemManager.itemList)
            {
                string categoryName = item.category.ToString();
                Dictionary<string, _Items> stackCategory;

                if (_exclude.Contains(item.shortname)) continue;

                if (!_itemMap.ContainsKey(item.shortname))
                    _itemMap.Add(item.shortname, categoryName);

                if (!_config.StackCategoryMultipliers.ContainsKey(categoryName))
                {
                    _config.StackCategoryMultipliers[categoryName] = 1;
                }

                if (!_config.StackCategories.TryGetValue(categoryName, out stackCategory))
                {
                    _config.StackCategories[categoryName] = stackCategory = new Dictionary<string, _Items>();
                }

                if (!stackCategory.ContainsKey(item.shortname))
                {
                    stackCategory.Add(item.shortname, new _Items
                    {
                        ShortName = item.shortname,
                        DisplayName = item.displayName.english,
                        Modified = item.stackable,
                    });
                }

                if (_corrections.ContainsKey(item.shortname))
                {
                    _config.StackCategories[categoryName][item.shortname].DisplayName = _corrections[item.shortname];
                }

                if (stackCategory.ContainsKey(item.shortname))
                {
                    _config.StackCategories[categoryName][item.shortname].ShortName = item.shortname;
                }

                if (!_defaults.ContainsKey(item.shortname))
                {
                    Puts($"Yo Tell Developer about missing defaults! {item.shortname}, {item.stackable}");
                    continue;
                }

                if (_config.StackCategoryMultipliers[categoryName] > 1 
                    && _config.StackCategories[categoryName][item.shortname].Modified == _defaults[item.shortname]
                )
                {
                    item.stackable = _config.StackCategoryMultipliers[categoryName];
                }
                else if (_config.StackCategories[categoryName][item.shortname].Modified != _defaults[item.shortname])
                {
                    item.stackable = _config.StackCategories[categoryName][item.shortname].Modified;
                }

                if (!_config.StackCategories[categoryName].ContainsKey(item.shortname) ||
                    item.shortname.Equals("vehicle.chassis") ||
                    item.shortname.Equals("vehicle.module")) continue;
                _stackModifierIcons.Add(new KeyValuePair<string, ulong>(item.shortname, 0));
            }

            SaveConfig();
        }

        internal class PluginConfig : SerializableConfiguration
        {
            [JsonProperty("Revert to Vanilla Stacks on unload (Recommended true if removing plugin)")]
            public bool Reset { get; set; }

            [JsonProperty("Disable Ammo/Fuel duplication fix (Recommended false)")]
            public bool DisableFix { get; set; }

            [JsonProperty("Enable VendingMachine Ammo Fix (Recommended)")]
            public bool VendingMachineAmmoFix { get; set; }

            [JsonProperty("Blocked Stackable Items", Order = 4)]
            public List<string> Blocked { get; set; }

            [JsonProperty("Category Stack Multipliers", Order = 5)]
            public Dictionary<string, int> StackCategoryMultipliers = new Dictionary<string, int>();

            [JsonProperty("Stack Categories", Order = 6)]
            public Dictionary<string, Dictionary<string, _Items>> StackCategories =
                new Dictionary<string, Dictionary<string, _Items>>();

            [JsonProperty("Enable UI Editor")]
            public bool EnableEditor = true;

            [JsonProperty("Sets editor command")] 
            public string modifycommand = "stackmodifier";

            [JsonProperty("Sets editor color command")]
            public string colorcommand = "stackmodifiercolor";

            [JsonProperty("Sets Reset Vendors command")]
            public string resetvenders = "resetvenders";

            [JsonProperty("Sets Default Category to open")]
            public string DefaultCat = "All";

            [JsonProperty("Stack Modifier UI Title")]
            public string EditorMsg = "Stack Modifier Editor ◝(⁰▿⁰)◜";

            [JsonProperty("UI - Stack Size Label")]
            public string StackLabel = "Default Stacks";

            [JsonProperty("UI - Set Stack Label")] 
            public string SetLabel = "Set Stacks";

            [JsonProperty("UI - Search Bar Label")]
            public string SearchLable = "Search";

            [JsonProperty("UI - Back Button Text")]
            public string BackButtonText = "◀";

            [JsonProperty("UI - Forward Button Text")]
            public string ForwardButtonText = "▶";

            [JsonProperty("UI - Close Label")] 
            public string CloseButtonlabel = "✖";

            [JsonProperty("UI - Background Image Url")]
            public string BackgroundUrlSm = "https://i.imgur.com/Jej3cwR.png";

            [JsonProperty("Sets any item to this image if image library does not have one for it.")]
            public string IconUrlSm = "https://imgur.com/BPM9UR4.png";

            public Colors Colors = new Colors();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() =>
                JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    Reset = false,
                    DisableFix = false,
                    VendingMachineAmmoFix = true,
                    Blocked = new List<string>
                    {
                        "shortname"
                    },
                };
            }
        }

        public class _Items
        {
            public string ShortName;
            public string DisplayName;
            public int Modified;
        }

        public class Colors
        {
            public Color InputPanel = new Color("#0E0E10", 0.98f);//"#0E0E10", 0.98f); "#FFFFFF", 0.2f);
            //public Color InputText = new Color("#FFE24B", 0.5f);
            public Color TextColor = new Color( "#FFFFFF");
            public Color Transparency = new Color("#", 0.95f);
            //public Color SearchLable = new Color("#FFFFFF", 0.05f);
            public Color DescriptionText = new Color("#FFFFFF", 0.5f);
            //public Color ButtonGreen = new Color("#556c31", 0.65f);
            public Color NewInputColor = new Color("#ffa805", 1f);
            public Color ButtonGreenText = new Color("#9ab36d", 0.431f);
            public Color ButtonGrey = new Color("#bfbfbf", 0.3f);
            public Color ButtonGreyText = new Color("#bfbfbf", 1f);
        }

        #region Updater

        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() =>
                JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                            .ToDictionary(prop => prop.Name, prop => ToObject(prop.Value));
                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue) token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults,
            Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        #endregion

        #endregion

        #region Oxide

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    PrintWarning($"Generating Config File for StackModifier");
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                PrintWarning("Failed to load config file (is the config file corrupt?) (" + ex.Message + ")");
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void Unload()
        {
            if (_config.Reset)
            {
                ResetStacks();
            }

            if (_config.EnableEditor)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (!player.IsSpectating() || !permission.UserHasPermission(player.UserIDString, Admin)) continue;
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
                    player.gameObject.SetLayerRecursive(17);
                    player.ChatMessage("Movement Restored");
                    DestroyUi(player, true);
                }

                _itemMap.Clear();
                _stackModifierImageList = null;
                _stackModifierIcons = null;
                _opensm = null;
                _editorPageSM = null;
            }
        }

        private void OnServerShutdown()
        {
            SaveConfig();

            if (_config.EnableEditor)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (!player.IsSpectating() || !permission.UserHasPermission(player.UserIDString, Admin)) continue;
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
                    player.gameObject.SetLayerRecursive(17);
                    player.ChatMessage("Movement Restored");
                    DestroyUi(player, true);
                }
            }
        }

        private void Init()
        {
            Unsubscribe(nameof(OnItemAddedToContainer));
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(ByPass, this);
            permission.RegisterPermission(Admin, this);
            CheckConfig();
            cmd.AddChatCommand(_config.modifycommand, this, CmdModify);
            cmd.AddChatCommand(_config.colorcommand, this, CmdColor);
            cmd.AddChatCommand(_config.resetvenders, this, cmddvend);
            LibraryCheck();
            Subscribe(nameof(OnItemAddedToContainer));
            foreach (var vendor in BaseNetworkable.serverEntities.OfType<InvisibleVendingMachine>())
            {
                foreach (var order in vendor.vendingOrders.orders)
                {
                    int amount;
                    if (_vending.TryGetValue(order.sellItem.shortname, out amount))
                    {
                        order.sellItemAmount = amount;
                    }
                }
            }
        }

        private void OnPluginLoaded(Plugin name)
        {
            if (ImageLibrary != null && name.Name == ImageLibrary.Name && !_isRestartSM)
            {
                _maxImageLibraryAttempts = 0;
                LibraryCheck();
            }
        }

        private object CanStackItem(Item item, Item targetItem)
        {
            if (item.GetOwnerPlayer().IsUnityNull() || targetItem.GetOwnerPlayer().IsUnityNull())
            {
                return null;
            }

            /*if (_configData.VendingMachineAmmoFix && item.GetRootContainer()?.entityOwner is VendingMachine)
            {
                return true;
            }*/

            if (_config.Blocked.Contains(item.info.shortname) && item.GetOwnerPlayer() != null && !permission.UserHasPermission(item.GetOwnerPlayer().UserIDString, ByPass))
            {
                return false;
            }

            if (item.info.itemid == targetItem.info.itemid && !CanWaterItemsStack(item, targetItem))
            {
                return false;
            }

            if (
                item.info.stackable <= 1 ||
                targetItem.info.stackable <= 1 ||
                item.info.itemid != targetItem.info.itemid ||
                !item.IsValid() ||
                item.IsBlueprint() && item.blueprintTarget != targetItem.blueprintTarget ||
                Math.Ceiling(targetItem.condition) != item.maxCondition ||
                item.skin != targetItem.skin ||
                item.name != targetItem.name
            )
            {
                return false;
            }

            if (item.info.amountType == ItemDefinition.AmountType.Genetics || targetItem.info.amountType == ItemDefinition.AmountType.Genetics)
            {
                if ((item.instanceData?.dataInt ?? -1) != (targetItem.instanceData?.dataInt ?? -1))
                {
                    return false;
                }
            }

            if (targetItem.contents?.itemList.Count > 0)
            {
                foreach (Item containedItem in targetItem.contents.itemList)
                {
                    item.parent.playerOwner.GiveItem(ItemManager.CreateByItemID(containedItem.info.itemid,
                        containedItem.amount));
                }
            }

            if (_config.DisableFix)
            {
                return null;
            }

            BaseProjectile.Magazine itemMag = targetItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;

            if (itemMag != null)
            {
                if (itemMag.contents > 0)
                {
                    item.GetOwnerPlayer()
                        .GiveItem(ItemManager.CreateByItemID(itemMag.ammoType.itemid, itemMag.contents));

                    itemMag.contents = 0;
                }
            }

            if (targetItem.GetHeldEntity() is FlameThrower)
            {
                FlameThrower flameThrower = targetItem.GetHeldEntity().GetComponent<FlameThrower>();

                if (flameThrower.ammo > 0)
                {
                    item.GetOwnerPlayer()
                        .GiveItem(ItemManager.CreateByItemID(flameThrower.fuelType.itemid, flameThrower.ammo));

                    flameThrower.ammo = 0;
                }
            }

            if (targetItem.GetHeldEntity() is Chainsaw)
            {
                Chainsaw chainsaw = targetItem.GetHeldEntity().GetComponent<Chainsaw>();

                if (chainsaw.ammo > 0)
                {
                    item.GetOwnerPlayer().GiveItem(ItemManager.CreateByItemID(chainsaw.fuelType.itemid, chainsaw.ammo));

                    chainsaw.ammo = 0;
                }
            }

            if (targetItem.info.shortname == "hat.miner" || targetItem.info.shortname == "hat.candle")
            {
                if (targetItem.contents != null && !targetItem.contents.IsEmpty())
                {
                    var content = targetItem.contents.itemList.First();
                    Item newItem = ItemManager.CreateByItemID(content.info.itemid, content.amount);
                    newItem.amount = content.amount;
                    newItem.amount = 0;
                    item.GetOwnerPlayer().GiveItem(newItem);
                }
            }

            return true;
        }

        private object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.skinID != targetItem.skinID ||
                item.item.name != targetItem.item.name ||
                item.item.contents != null ||
                targetItem.item.contents != null ||
                item.item.contents != null ||
                targetItem.item.contents != null)
            {
                return false;
            }

            if (Math.Abs(item.item._condition - targetItem.item._condition) > 0f)
            {
                return false;
            }

            if (Math.Abs(item.item._maxCondition - targetItem.item._maxCondition) > 0f)
            {
                return false;
            }

            return null;
        }

        private Item OnItemSplit(Item item, int amount)
        {
            if (item.GetHeldEntity()?.GetComponentInChildren<BaseLiquidVessel>() != null)
            {
                Item LiquidContainer = ItemManager.CreateByName(item.info.shortname);
                LiquidContainer.amount = amount;

                item.amount -= amount;
                item.MarkDirty();

                Item water = item.contents.FindItemByItemID(-1779180711);

                if (water != null)
                    LiquidContainer.contents.AddItem(ItemManager.FindItemDefinition(-1779180711), water.amount);

                return LiquidContainer;
            }

            if (item.skin != 0)
            {
                Item x = ItemManager.CreateByItemID(item.info.itemid);
                BaseProjectile.Magazine itemMag = x.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
                if (itemMag != null && itemMag.contents > 0)
                {
                    itemMag.contents = 0;
                }

                item.amount -= amount;
                x.name = item.name;
                x.skin = item.skin;
                x.amount = amount;
                x.MarkDirty();
                var heldEntity = x.GetHeldEntity();
                if (heldEntity != null)
                {
                    heldEntity.skinID = item.skin; 
                }

                return x;
            }

            Item newItem = ItemManager.CreateByItemID(item.info.itemid);

            BaseProjectile.Magazine newItemMag =
                newItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;

            if (newItem.contents?.itemList.Count == 0 &&
                (_config.DisableFix || newItem.contents?.itemList.Count == 0 && newItemMag?.contents == 0))
            {
                return null;
            }

            item.amount -= amount;
            newItem.name = item.name;
            newItem.amount = amount;

            item.MarkDirty();

            if (item.IsBlueprint())
            {
                newItem.blueprintTarget = item.blueprintTarget;
            }

            if (item.info.amountType == ItemDefinition.AmountType.Genetics && item.instanceData != null &&
                item.instanceData.dataInt != 0)
            {
                newItem.instanceData = new ProtoBuf.Item.InstanceData()
                {
                    dataInt = item.instanceData.dataInt,
                    ShouldPool = false
                };
            }

            if (newItem.contents?.itemList.Count > 0)
            {
                item.contents.Clear();
            }

            newItem.MarkDirty();

            if (_config.VendingMachineAmmoFix && item.GetRootContainer()?.entityOwner is VendingMachine)
            {
                return newItem;
            }

            if (_config.DisableFix)
            {
                return newItem;
            }

            if (newItem.GetHeldEntity() is FlameThrower)
            {
                newItem.GetHeldEntity().GetComponent<FlameThrower>().ammo = 0;
            }

            if (newItem.GetHeldEntity() is Chainsaw)
            {
                newItem.GetHeldEntity().GetComponent<Chainsaw>().ammo = 0;
            }

            BaseProjectile.Magazine itemMagDefault =
                newItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
            if (itemMagDefault != null && itemMagDefault.contents > 0)
            {
                itemMagDefault.contents = 0;
            }

            return newItem;
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            BasePlayer player = container.GetOwnerPlayer();
            if (player == null || player.IsNpc || player.IsAdmin) return;

            if ((player.inventory.containerMain.uid == container.uid || player.inventory.containerBelt.uid == container.uid) && item.amount > item.MaxStackable())
            {
                int division = item.amount / item.MaxStackable();

                for (int i = 0; i < division; i++)
                {
                    Item y = item.SplitItem(item.MaxStackable());
                    if (y != null && !y.MoveToContainer(player.inventory.containerMain, -1, false) && (item.parent == null || !y.MoveToContainer(item.parent)))
                    {
                        y.Drop(player.inventory.containerMain.dropPosition,
                            player.inventory.containerMain.dropVelocity);
                    }
                }
            }

            if (player.inventory.containerWear.uid != container.uid) return;
            if (item.amount <= 1) return;
            int amount2 = item.amount -= 1;
            player.inventory.containerWear.Take(null, item.info.itemid, amount2 - 1);
            Item x = ItemManager.CreateByItemID(item.info.itemid, item.amount, item.skin);
            x.name = item.name;
            x.skin = item.skin;
            x.amount = amount2;
            x._condition = item._condition;
            x._maxCondition = item._maxCondition;
            x.MarkDirty();
            x.MoveToContainer(player.inventory.containerMain);
        }

        private object OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            var item = card.GetItem();
            if (item.amount <= 1) return null;

            int division = item.amount / 1;

            for (int i = 0; i < division; i++)
            {
                Item x = item.SplitItem(1);
                if (x != null && !x.MoveToContainer(player.inventory.containerMain, -1, false) &&
                    (item.parent == null || !x.MoveToContainer(item.parent)))
                {
                    x.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                }
            }

            return null;
        }

        private object CanSpectateTarget(BasePlayer player, string filter)
        {
            if (permission.UserHasPermission(player.UserIDString, Admin) && _opensm.Contains(player.UserIDString))
            {
                return false;
            }

            return null;
        }

        #endregion

        #region Helpers

        private bool CanWaterItemsStack(Item item, Item targetItem)
        {
            if (item.GetHeldEntity()?.GetComponentInChildren<BaseLiquidVessel>() == null)
            {
                return true;
            }

            if (targetItem.contents.IsEmpty() || item.contents.IsEmpty())
                return (!targetItem.contents.IsEmpty() || !item.contents.IsFull()) && (!item.contents.IsEmpty() || !targetItem.contents.IsFull());

            var first = item.contents.itemList.First();
            var second = targetItem.contents.itemList.First();
            if (first.info.itemid != second.info.itemid || first.amount != second.amount) return false;

            return (!targetItem.contents.IsEmpty() || !item.contents.IsFull()) && (!item.contents.IsEmpty() || !targetItem.contents.IsFull());
        }

        private void ResetStacks()
        {
            foreach (ItemDefinition itemDefinition in ItemManager.GetItemDefinitions())
            {
                if (!_config.StackCategories.ContainsKey(itemDefinition.category.ToString())) continue;
                Dictionary<string, _Items> stackCategory = _config.StackCategories[itemDefinition.category.ToString()];
                if (!stackCategory.ContainsKey(itemDefinition.shortname)) continue;
                if (!_defaults.ContainsKey(itemDefinition.shortname)) continue;
                int a = _defaults[itemDefinition.shortname];
                itemDefinition.stackable = a;
            }
        }
        
        private int _maxImageLibraryAttempts = 0;
        private void LibraryCheck()
        {
            bool success = ImageLibrary != null && ImageLibrary.IsLoaded && ImageLibrary.Call<bool>("IsReady");

            if (!_config.EnableEditor) return;
            if (!success)
            {
                if (_maxImageLibraryAttempts >= 20)
                {
                    _isRestartSM = false;
                    PrintWarning("StackModifier was still unable to find ImageLibrary plugin. UI Editor will not be usable!");
                    return;
                }
                _maxImageLibraryAttempts++;
                PrintWarning("Unable to find ImageLibrary plugin. This may be caused by StackModifier loading before it. Will check again in 1 minute");
                timer.In(60, LibraryCheck);
            }
            else
            {
                _stackModifierImageList = new Dictionary<string, string>();
                LoadImages();
            }
        }

        private void LoadImages()
        {
            _isRestartSM = false;
            _stackModifierImageList.Add(StackModifierEditorBackgroundImage, _config.BackgroundUrlSm);
            _stackModifierImageList.Add(_config.IconUrlSm, _config.IconUrlSm);

            if (_stackModifierIcons.Count > 0)
            {
                ImageLibrary?.Call("LoadImageList", Name, _stackModifierIcons, null);
            }

            ImageLibrary?.Call("ImportImageList", Name, _stackModifierImageList, 0UL, true, new Action(Ready));
        }

        private void Ready()
        {
            _isEditorReady = true;
            _stackModifierImageList.Clear();
            _stackModifierIcons.Clear();
        }

        #endregion

        #region ResetVendors

        private void reset()
        {
            foreach (var vendor in BaseNetworkable.serverEntities.OfType<InvisibleVendingMachine>())
            {
                foreach (var order in vendor.vendingOrders.orders)
                {
                    int amount;
                    if (_vending.TryGetValue(order.sellItem.shortname, out amount))
                    {
                        order.sellItemAmount = amount;
                    }
                }
            }
        }

        private void cmddvend(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Admin) || !player.IsAdmin) return;
            reset();
            player.ChatMessage("Affected Venders Restored");
        }

        #endregion

        #region UI

        private const string StackModifierEditorOverlayName = "StackModifierEditorOverlay";
        private const string StackModifierEditorContentName = "StackModifierEditorContent";
        private const string StackModifierEditorDescOverlay = "StackModifierEditorDescOverlay";
        private const string StackModifierEditorBackgroundImage = "StackModifierBackground";

        private CuiElementContainer CreateEditorOverlay()
        {
            return new CuiElementContainer
            {
                {
                    new CuiPanel //background transparency
                    {
                        Image =
                        {
                            Color = _config.Colors.Transparency.Rgb
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 0", 
                            AnchorMax = "1 1"
                        },
                        CursorEnabled = true
                    },
                    "Overlay", StackModifierEditorOverlayName
                },
                    new CuiElement //Background image
                    {
                        Parent = StackModifierEditorOverlayName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = ImageLibrary?.Call<string>("GetImage", StackModifierEditorBackgroundImage)
                            },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                        }
                    },
                {
                    new CuiLabel //Welcome Msg
                    {
                        Text =
                        {
                            Text = GetText(_config.EditorMsg, "label"),
                            FontSize = 30,
                            Color = _config.Colors.TextColor.Rgb,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.25 0.85", 
                            AnchorMax = "0.7 0.95"
                        }
                    },
                    StackModifierEditorOverlayName
                },
                {
                    new CuiLabel // Set Label
                    {
                        Text =
                        {
                            Text = GetText(_config.SetLabel, "label"),
                            FontSize = 20,
                            Color = _config.Colors.TextColor.Rgb,
                            Align = TextAnchor.MiddleLeft
                        },
                        RectTransform = {AnchorMin = "0.57 0.6", AnchorMax = "0.7 0.65"}
                    },
                    StackModifierEditorOverlayName
                },
                {
                    new CuiLabel // Stack Label,
                    {
                        Text =
                        {
                            Text = GetText(_config.StackLabel, "label"),
                            FontSize = 20,
                            Color = _config.Colors.TextColor.Rgb,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform = {AnchorMin = "0.44 0.6", AnchorMax = "0.55 0.65"}
                    },
                    StackModifierEditorOverlayName
                },
                {
                    new CuiLabel // Search Label,
                    {
                        Text =
                        {
                            Text = GetText(_config.SearchLable, "label"),
                            FontSize = 20,
                            Color = _config.Colors.TextColor.Rgb,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.355 0.6", 
                            AnchorMax = "0.4 0.65"
                        }
                    },
                    StackModifierEditorOverlayName
                },
                {
                    new CuiLabel // Arrow Search Label,
                    {
                        Text =
                        {
                            Text = GetText("▶", "label"),
                            FontSize = 20,
                            Color = _config.Colors.TextColor.Rgb,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform = {AnchorMin = "0.32 0.6", AnchorMax = "0.35 0.65"}
                    },
                    StackModifierEditorOverlayName
                },
                {
                    new CuiPanel
                    {
                        Image = {Color = _config.Colors.InputPanel.Rgb},
                        RectTransform =
                        {
                            AnchorMin = "0.325 0.60", 
                            AnchorMax = "0.445 0.645"
                        },
                        CursorEnabled = true
                    },
                    StackModifierEditorOverlayName, "InputNameSearch"
                },
                {
                    new CuiButton //close button Label
                    {
                        Button =
                        {
                            Command = $"editorsm.close", 
                            Color = _config.Colors.ButtonGrey.Rgb
                        },
                        RectTransform = {AnchorMin = "0.444 0.11", AnchorMax = "0.54 0.16"},
                        Text =
                        {
                            Text = GetText(_config.CloseButtonlabel, "label"),
                            FontSize = 20,
                            Color = _config.Colors.ButtonGreyText.Rgb,
                            Align = TextAnchor.MiddleCenter
                        }
                    },
                    StackModifierEditorOverlayName, "close"
                }
            };
        }

        private readonly CuiLabel editorDescription = new CuiLabel
        {
            Text =
            {
                Text = "{editorDescription}",
                FontSize = 15,
                Align = TextAnchor.MiddleCenter
            },
            RectTransform =
            {
                AnchorMin = "0.16 0.66",
                AnchorMax = "0.8 0.71"
            }
        };

        private CuiElementContainer CreateEditorItemEntry(_Items dataItem, float ymax, float ymin, string catName, string text)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = $"{_defaults[dataItem.ShortName]}",
                        FontSize = 15,
                        Color = _config.Colors.TextColor.Rgb,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{0.4} {ymin}",
                        AnchorMax = $"{0.5} {ymax}"
                    }
                },
                StackModifierEditorContentName);

            int msg = dataItem.Modified != _defaults[dataItem.ShortName] ? 
                    dataItem.Modified : 
                    dataItem.Modified == _defaults[dataItem.ShortName] && _config.StackCategoryMultipliers[_itemMap[dataItem.ShortName]] == 1 ? 
                        dataItem.Modified : 
                         _config.StackCategoryMultipliers[_itemMap[dataItem.ShortName]];

            container.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = $"{msg}",
                        FontSize = 15,
                        Color = _config.Colors.TextColor.Rgb, //$"0.5 0.5 0.5 0.5",
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{(0.499) + 1 * 0.03 + 0.001} {ymin}",
                        AnchorMax = $"{(0.579) + 1 * 0.03 - 0.001} {ymax - 0.01}"
                    }
                },
                StackModifierEditorContentName);

            container.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = _config.Colors.InputPanel.Rgb
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{(0.499) + 1 * 0.03 + 0.001} {ymin}",
                        AnchorMax = $"{(0.579) + 1 * 0.03 - 0.001} {ymax - 0.01}"
                    },
                    CursorEnabled = true
                },
                StackModifierEditorContentName, "InputName");

            container.Add(new CuiElement
            {
                Parent = "InputName",
                FadeOut = 1f,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        //Text = $"{dataItem.Modified}",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        Color = _config.Colors.NewInputColor.Rgb,
                        IsPassword = false,
                        Command = $"{"editorsm.edit"} {catName} {dataItem.DisplayName.Replace(" ", "_")} {text}",
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                }
            });

            return container;
        }

        private void CreateEditorItemIcon(ref CuiElementContainer container, string shortname, string displayName, string userId, float ymax, float ymin)
        {
            var label = new CuiLabel
            {
                Text =
                {
                    Text = LangAPI?.Call<string>("GetItemDisplayName", shortname, displayName, userId) ?? displayName,
                    FontSize = 15,
                    Color = _config.Colors.TextColor.Rgb,
                    Align = TextAnchor.MiddleLeft
                },
                RectTransform =
                {
                    AnchorMin = $"0.3 {ymin}",
                    AnchorMax = $"0.4 {ymax}"
                }
            };

            var rawImage = new CuiRawImageComponent();
            
            if ((bool) (ImageLibrary?.Call("HasImage", shortname, 0UL) ?? false))
            {
                rawImage.Png = (string) ImageLibrary?.Call("GetImage", shortname, 0UL, false);
            }
            else
            {
                rawImage.Png = (string) ImageLibrary?.Call("GetImage", _config.IconUrlSm);
            }

            container.Add(label, StackModifierEditorContentName);
            container.Add(new CuiElement
            {
                Parent = StackModifierEditorContentName,
                Components =
                {
                    rawImage,
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0.26 {ymin}",
                        AnchorMax = $"0.29 {ymax}"
                    }
                }
            });
        }

        private void CreateEditorChangePage(ref CuiElementContainer container, string currentcat, int editorpageminus, int editorpageplus)
        {
            container.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"editorsm.show {currentcat} {editorpageminus}",
                        Color = _config.Colors.ButtonGrey.Rgb //"0 0 0 0.40"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.344 0.11",
                        AnchorMax = "0.44 0.16"
                    },
                    Text =
                    {
                        Text = GetText(_config.BackButtonText, "label"),
                        Color = _config.Colors.ButtonGreenText.Rgb,
                        FontSize = 30,
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf"
                    }
                },
                StackModifierEditorOverlayName,
                "ButtonBack");

            container.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"editorsm.show {currentcat} {editorpageplus}",
                        Color = _config.Colors.ButtonGrey.Rgb //"0 0 0 0.40"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.544 0.11",
                        AnchorMax = "0.64 0.16"
                    },
                    Text =
                    {
                        Text = GetText(_config.ForwardButtonText, "label"),
                        Color = _config.Colors.ButtonGreenText.Rgb,
                        FontSize = 30,
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf"
                    }
                },
                StackModifierEditorOverlayName,
                "ButtonForward");
        }

        private void CreateTab(ref CuiElementContainer container, string cat, int editorpageminus, int rowPos)
        {
            int numberPerRow = 5;

            float padding = 0.01f;
            float margin = (0.314f + padding);

            float width = ((0.334f - (padding * (numberPerRow + 1))) / numberPerRow);
            float height = (width * 0.65f);

            int row = (int) Math.Floor((float) rowPos / numberPerRow);
            int col = (rowPos - (row * numberPerRow));
            
            container.Add(new CuiButton
            {
                Button =
                {
                    Command = $"editorsm.show {cat} {editorpageminus}",
                    Color = "0.5 0.5 0.5 0.5"
                },
                RectTransform = // 0.050 <width  padding> 0.056
                {
                    AnchorMin = $"{margin + (width * col) + (padding * col)} {(0.85f - padding) - ((row + 1) * height) - (padding * row)}", // 0.11  0.334  // 0.78
                    AnchorMax = $"{margin + (width * (col + 1)) + (padding * col)} {(0.85f - padding) - (row * height) - (padding * row)}" // 0.16 0.384  // 0.82
                },
                Text =
                {
                    Text = $"{cat}", //StackModifierLang(cat, player.UserIDString),
                    Align = TextAnchor.MiddleCenter,
                    Color = _config.Colors.TextColor.Rgb,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12
                }
            }, StackModifierEditorOverlayName, cat);
        }

        private void DestroyUi(BasePlayer player, bool full = false)
        {
            CuiHelper.DestroyUi(player, StackModifierEditorContentName);
            CuiHelper.DestroyUi(player, "ButtonForward");
            CuiHelper.DestroyUi(player, "ButtonBack");
            if (!full) return;
            CuiHelper.DestroyUi(player, StackModifierEditorDescOverlay);
            CuiHelper.DestroyUi(player, StackModifierEditorOverlayName);
        }

        private void ShowEditor(BasePlayer player, string catid, int from = 0, bool fullPaint = true, bool refreshMultipler = false, bool filter = false, string input = "")
        {
            _editorPageSM[player.userID] = from;
            Dictionary<string, _Items> item = null;
            if (catid == "All")
            {
                item = new Dictionary<string, _Items>();
                foreach (var cat in _config.StackCategories.Values)
                {
                    foreach (var i in cat)
                    {
                        item.Add(i.Key, i.Value);
                    }
                }
            }
            else
            {
                item = _config.StackCategories[catid];
            }

            editorDescription.Text.Color = _config.Colors.DescriptionText.Rgb;
            if (catid == "All")
                editorDescription.Text.Text = $"Global Multiplier is disabled in All category";
            else if (_config.StackCategoryMultipliers[catid] != 1)
                editorDescription.Text.Text = $"{catid} Multiplier {_config.StackCategoryMultipliers[catid]}x & will re-apply on ( restart or reload )\nUnless the Modified value does not equal default";
            else
                editorDescription.Text.Text = $"{catid} Global Multiplier is disabled since it has not been modified";

            CuiElementContainer container;

            if (fullPaint)
            {
                CuiHelper.DestroyUi(player, StackModifierEditorOverlayName);

                container = CreateEditorOverlay();

                int rowPos = 1;

                CreateTab(ref container, "All", from, 0);

                foreach (var cat in _config.StackCategories)
                {
                    CreateTab(ref container, cat.Key, from, rowPos);
                    rowPos++;
                }

                if (!refreshMultipler)
                    container.Add(editorDescription, StackModifierEditorOverlayName, StackModifierEditorDescOverlay);
            }
            else
            {
                container = new CuiElementContainer();
            }

            CuiHelper.DestroyUi(player, StackModifierEditorDescOverlay);
            CuiHelper.DestroyUi(player, StackModifierEditorContentName);
            CuiHelper.DestroyUi(player, "Field");
            CuiHelper.DestroyUi(player, "ButtonForward");
            CuiHelper.DestroyUi(player, "ButtonBack");

            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0.0"},
                RectTransform = {AnchorMin = "0.08 0.2", AnchorMax = "1 0.6"}
            }, StackModifierEditorOverlayName, StackModifierEditorContentName);

            if (refreshMultipler)
            {
                CuiHelper.DestroyUi(player, StackModifierEditorDescOverlay);
                container.Add(editorDescription, StackModifierEditorOverlayName, StackModifierEditorDescOverlay);
            }

            int current = 0;

            List<_Items> items = Facepunch.Pool.GetList<_Items>();

            if (filter && !string.IsNullOrEmpty(input))
            {
                if (catid == "All")
                {
                    foreach (var cItem in item.Values)
                    {
                        if (cItem.DisplayName.Contains(input.Replace("_", " "), CompareOptions.OrdinalIgnoreCase))
                            items.Add(cItem);
                    }
                }
                else
                    foreach (var shortname in item.Keys)
                    {
                        if (!_config.StackCategories[catid].ContainsKey(shortname)) continue;

                        _Items cItem = _config.StackCategories[catid][shortname];
                        if (cItem.DisplayName.Contains(input.Replace("_", " "), CompareOptions.OrdinalIgnoreCase))
                            items.Add(cItem);
                    }
            }
            else
            {
                if (catid == "All")
                {
                    foreach (var cItem in item.Values)
                    {
                        items.Add(cItem);
                    }
                }
                else
                    foreach (var shortname in item.Keys)
                    {
                        if (!_config.StackCategories[catid].ContainsKey(shortname)) continue;

                        items.Add(_config.StackCategories[catid][shortname]);
                    }
            }

            input = string.Empty;

            container.Add(new CuiElement
            {
                Parent = "InputNameSearch",
                Name = "Field",
                FadeOut = 1f,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        //Text = input,
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        Color = _config.Colors.NewInputColor.Rgb, //$"{new Color("#FFE24B", 0.5f).Rgb}", //$"{new Color( "#FFFFFF", 0.05f).Rgb}",
                        IsPassword = false,
                        Command = $"editorsm.{("search")} {catid} {input.Replace(" ", "_")}",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                }
            });

            foreach (_Items data in items)
            {
                if (current >= from && current < from + 8)
                {
                    float pos = 0.85f - 0.125f * (current - from);

                    CreateEditorItemIcon(ref container, data.ShortName, data.DisplayName, player.UserIDString, pos + 0.125f, pos);

                    container.AddRange(CreateEditorItemEntry(data, pos + 0.125f, pos, catid, ""));
                }

                current++;
            }

            Facepunch.Pool.FreeList(ref items);

            int minfrom = from <= 8 ? 0 : from - 8;
            int maxfrom = from + 8 >= current ? from : from + 8;

            CreateEditorChangePage(ref container, catid, minfrom, maxfrom);

            CuiHelper.AddUi(player, container);
        }

        private void CmdModify(BasePlayer player, string command, string[] args)
        {
            if (!_config.EnableEditor || !permission.UserHasPermission(player.UserIDString, Admin))
                return;

            if (!_isEditorReady)
            {
                player.ChatMessage("Waiting On ImageLibrary to finish the load order");
                return;
            }
            
            ShowEditor(player, _config.DefaultCat);
            _opensm.Add(player.UserIDString);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
            player.CancelInvoke("ServerUpdate");
            /*player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
            player.gameObject.SetLayerRecursive(10);*/
            player.ChatMessage("Blocking Movement");
        }

        private void CmdColor(BasePlayer player, string command, string[] args)
        {
            if (!_config.EnableEditor || !permission.UserHasPermission(player.UserIDString, Admin))
                return;

            if (args.Length < 2)
            {
                player.ChatMessage($"Invalid Syntax, please type /{_config.colorcommand} <inputpanel|inputpanelnew|inputtext|text|transparent|description> <color> <alpha|ex, 0.98>");
                return;
            }

            UnityEngine.Color color;
            if (!ColorUtility.TryParseHtmlString(args[1], out color))
            {
                SendReply(player, "Not a valid hex color");
                return;
            }

            float alpha = 1f;
            if (args.Length == 3 && !float.TryParse(args[2], out alpha))
            {
                alpha = 1f;
            }

            switch (args[0].ToLower())
            {
                case "inputpanel":
                    _config.Colors.InputPanel = new Color(args[1], alpha);
                    SendReply(player, $"UI {args[0]} color was updated");
                    break;
                case "inputpanelnew":
                    _config.Colors.NewInputColor = new Color(args[1], alpha);
                    SendReply(player, $"UI {args[0]} color was updated");
                    break;
                case "inputtext":
                    _config.Colors.NewInputColor = new Color(args[1], alpha);
                    SendReply(player, $"UI {args[0]} color was updated");
                    break;
                case "text":
                    _config.Colors.TextColor = new Color(args[1], alpha);
                    SendReply(player, $"UI {args[0]} color was updated");
                    break;
                case "transparent":
                    _config.Colors.Transparency = new Color(args[1], alpha);
                    SendReply(player, $"UI {args[0]} color was updated");
                    break;
                case "description":
                    _config.Colors.DescriptionText = new Color(args[1], alpha);
                    SendReply(player, $"UI {args[0]} color was updated");
                    break;
                default:
                    player.ChatMessage($"Invalid Syntax, please type /{_config.colorcommand} <inputpanel|inputpanelnew|inputtext|text|transparent|description> <color> <alpha|ex, 0.98>");
                    break;
            }

            SaveConfig();
        }
 
        [ConsoleCommand("editorsm.show")]
        private void ConsoleEditorShow(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(2)) return;
            BasePlayer player = arg.Player();
            string catid = arg.GetString(0);

            if (catid.Equals("close"))
            {
                BasePlayer targetPlayer = arg.GetPlayer(1);
                DestroyUi(targetPlayer, true);
                return;
            }

            if (player == null || !permission.UserHasPermission(player.UserIDString, Admin))
                return;

            ShowEditor(player, catid, arg.GetInt(1), false, true);
        }

        [ConsoleCommand("editorsm.edit")]
        private void ConsoleEditSet(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(3)) return;
            BasePlayer player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, Admin)) return;
            string catName = arg.GetString(0).Replace("_", " ");
            string item = arg.GetString(1).Replace("_", " ");
            int amount = arg.GetInt(2);
            if (amount == 0 || string.IsNullOrEmpty(amount.ToString())) return;

            if (catName == "All")
            {
                foreach (var cats in _config.StackCategories.Values)
                {
                    foreach (var i in cats)
                    {
                        if (i.Value.DisplayName != item) continue;
                            i.Value.Modified = amount;
                    }
                }
                SaveConfig();
            }
            else
            {
                var cat = _config.StackCategories[catName];
                foreach (var shortname in cat.Keys)
                {
                    if (!_config.StackCategories[catName].ContainsKey(shortname)) continue;
                    _Items stackItem = _config.StackCategories[catName][shortname];
                    if (stackItem.DisplayName != item) continue;
                    stackItem.Modified = amount;
                }
                SaveConfig();
            }

            if (catName == "All")
            {
                foreach (ItemDefinition id in ItemManager.itemList)
                {
                    string categoryName = id.category.ToString();
                    if (!_config.StackCategories[categoryName].ContainsKey(id.shortname)) continue;
                    _Items stackItem = _config.StackCategories[categoryName][id.shortname];
                    if (stackItem.DisplayName == item)
                    {
                        id.stackable = _config.StackCategories[categoryName][id.shortname].Modified;
                    }
                }
                SaveConfig();
            }
            else
            {
                foreach (ItemDefinition id in ItemManager.itemList)
                {
                    string categoryName = id.category.ToString();
                    if (!_config.StackCategories[catName].ContainsKey(id.shortname)) continue;
                    _Items stackItem;
                    stackItem = _config.StackCategories[catName][id.shortname];
                    if (stackItem.DisplayName == item)
                    {
                        id.stackable = _config.StackCategories[categoryName][id.shortname].Modified;
                    }
                }
                SaveConfig();
            }

            ShowEditor(player, catName, _editorPageSM[player.userID], false);
        }

        [ConsoleCommand("editorsm.search")]
        private void ConsoleEditSearch(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, Admin)) return;
            string catid = arg.GetString(0);
            string input = arg.GetString(1) + arg.GetString(2) + arg.GetString(3) + arg.GetString(4) + arg.GetString(5);
            if (string.IsNullOrEmpty(input)) return;
            bool resetting = false;
            bool filter = true;
            bool refresh = false;

            if (arg.GetString(1).Equals("reset", StringComparison.OrdinalIgnoreCase) || arg.GetString(1).Equals("set", StringComparison.OrdinalIgnoreCase) && arg.GetInt(2) != 0)
            {
                resetting = true;
                filter = false;
                refresh = true;

                int set = arg.GetInt(2);
                bool command = arg.GetString(1).Equals("set", StringComparison.OrdinalIgnoreCase);

                if (command && catid == "All") return;
                if (!command && catid == "All")
                {
                    foreach (var cats in _config.StackCategories.Values)
                    {
                        foreach (var i in cats)
                        {
                            if (!_defaults.ContainsKey(i.Value.ShortName)) continue;
                            i.Value.Modified = _defaults[i.Value.ShortName];
                        }
                    }

                    var catss = _config.StackCategories.Keys;
                    foreach (var value in catss)
                    {
                        if (_config.StackCategoryMultipliers.ContainsKey(value))
                        {
                            _config.StackCategoryMultipliers[value] = 1;
                        }
                    }
                    SaveConfig();

                    foreach (ItemDefinition id in ItemManager.itemList)
                    {
                        if (!_defaults.ContainsKey(id.shortname)) continue;
                        id.stackable = _defaults[id.shortname];
                    }
                }

                if (catid != "All")
                {

                    if (command)
                    {
                        _config.StackCategoryMultipliers[catid] = set;
                    }
                    else
                    {
                        _config.StackCategoryMultipliers[catid] = 1;
                    }

                    var cat = _config.StackCategories[catid];

                    foreach (var shortname in cat.Keys)
                    {
                        if (!_config.StackCategories[catid].ContainsKey(shortname)) continue;
                        _Items stackItem = _config.StackCategories[catid][shortname];
                        if (stackItem.ShortName != shortname) continue;
                        if (!command)
                        {
                            stackItem.Modified = _defaults[shortname];
                        }
                    }
                    SaveConfig();

                    foreach (ItemDefinition id in ItemManager.itemList)
                    {
                        if (!_config.StackCategories[catid].ContainsKey(id.shortname)) continue;
                        if (command && _config.StackCategories[catid][id.shortname].Modified == _defaults[id.shortname])
                        {
                            id.stackable = set;
                        }
                        else
                        {
                            id.stackable = _defaults[id.shortname];
                        }
                    }
                }

                string output = command
                    ? $"{player.displayName} has set {catid} category to {set}"
                    : $"{player.displayName} Reset {catid} category to defaults";
              //  Puts($"{output}");
                string response =
                    command ? $"Setting {catid} category to {set}" : $"Resetting {catid} category & multiplier to defaults";
                player.ChatMessage($"{response}");
            }

            ShowEditor(player, catid, arg.GetInt(1), resetting, refresh, filter, input);
        }

        [ConsoleCommand("editorsm.close")]
        private void ConsoleEditClose(ConsoleSystem.Arg arg)
        {
            SaveConfig();
            BasePlayer player = arg.Player();
            DestroyUi(player, true);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
            /*if (!player.IsSpectating()) return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
            player.gameObject.SetLayerRecursive(17);*/
            player.ChatMessage("Movement Restored");
            _opensm.Remove(player.UserIDString);
        }

        #endregion

        #region UI Colors

        private string GetText(string text, string type)
        {
            switch (type)
            {
                case "label":
                    return text;
                case "image":
                    return "https://i.imgur.com/fL7N8Zf.png";
            }

            return "";
        }

        public class Color
        {
            [JsonIgnore]
            public int R;
            [JsonIgnore]
            public int G;
            [JsonIgnore]
            public int B;
            [JsonIgnore]
            public float A;
            public string Hex;
            public string Rgb;

            public Color(string hex, float alpha = 1f)
            {
                if (hex.StartsWith("#"))
                    hex = hex.Substring(1);

                if (hex.Length == 6)
                {
                    R = int.Parse(hex.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                    G = int.Parse(hex.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                    B = int.Parse(hex.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                }

                A = alpha;
                Hex = "#" + hex;
                Rgb = $"{(double)R / 255} {(double)G / 255} {(double)B / 255} {A}";
            }
        }

        #endregion
    }
}