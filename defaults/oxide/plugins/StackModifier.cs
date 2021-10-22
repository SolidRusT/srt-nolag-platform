using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;

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
*/

namespace Oxide.Plugins
{
    [Info("Stack Modifier", "Khan", "1.2.0")]
    [Description("Modify item stack sizes")]
    public class StackModifier : CovalencePlugin
    {
        #region Fields
        
        private Dictionary<uint, List<ProtoBuf.VendingMachine.SellOrder>> _sellOrders = new Dictionary<uint, List<ProtoBuf.VendingMachine.SellOrder>>();

        private const string ByPass = "stackmodifier.bypass";

        private Dictionary<string, int> defaults = new Dictionary<string, int>
        {
            {"hat.wolf", 1 },
            {"diving.fins", 1 },
            {"diving.mask", 1 },
            {"diving.tank", 1 },
            {"diving.wetsuit", 1 },
            {"boots.frog", 1 },
            {"barrelcostume", 1 },
            {"cratecostume", 1 },
            {"hat.gas.mask", 1 },
            {"burlap.gloves.new", 1 },
            {"burlap.gloves", 1 },
            {"roadsign.gloves", 1 },
            {"tactical.gloves", 1 },
            {"ghostsheet", 1 },
            {"halloween.mummysuit", 1 },
            {"scarecrow.suit", 1 },
            {"scarecrowhead", 1 },
            {"attire.hide.helterneck", 1 },
            {"hat.beenie", 1 },
            {"hat.boonie", 1 },
            {"bucket.helmet", 1 },
            {"burlap.headwrap", 1 },
            {"hat.candle", 1 },
            {"hat.cap", 1 },
            {"clatter.helmet", 1 },
            {"coffeecan.helmet", 1 },
            {"deer.skull.mask", 1 },
            {"heavy.plate.helmet", 1 },
            {"hat.miner", 1 },
            {"partyhat", 1 },
            {"riot.helmet", 1 },
            {"wood.armor.helmet", 1 },
            {"hoodie", 1 },
            {"bone.armor.suit", 1 },
            {"heavy.plate.jacket", 1 },
            {"jacket.snow", 1 },
            {"jacket", 1 },
            {"wood.armor.jacket", 1 },
            {"mask.balaclava", 1 },
            {"mask.bandana", 1 },
            {"metal.facemask", 1 },
            {"nightvisiongoggles", 1 },
            {"attire.ninja.suit", 1 },
            {"burlap.trousers", 1 },
            {"heavy.plate.pants", 1 },
            {"attire.hide.pants", 1 },
            {"roadsign.kilt", 1 },
            {"pants.shorts", 1 },
            {"wood.armor.pants", 1 },
            {"pants", 1 },
            {"attire.hide.poncho", 1 },
            {"burlap.shirt", 1 },
            {"shirt.collared", 1 },
            {"attire.hide.vest", 1 },
            {"shirt.tanktop", 1 },
            {"shoes.boots", 1 },
            {"burlap.shoes", 1 },
            {"attire.hide.boots", 1 },
            {"attire.hide.skirt", 1 },
            {"attire.banditguard", 1 },
            {"hazmatsuit", 1 },
            {"hazmatsuit_scientist", 1 },
            {"hazmatsuit_scientist_peacekeeper", 1 },
            {"hazmatsuit.spacesuit", 1 },
            {"scientistsuit_heavy", 1 },
            {"jumpsuit.suit.blue", 1 },
            {"jumpsuit.suit", 1 },
            {"tshirt.long", 1 },
            {"tshirt", 1 },
            {"metal.plate.torso", 1 },
            {"roadsign.jacket", 1 },
            {"hat.dragonmask", 1 },
            {"hat.oxmask", 1 },
            {"hat.ratmask", 1 },
            {"attire.bunnyears", 1 },
            {"attire.bunny.onesie", 1 },
            {"hat.bunnyhat", 1 },
            {"attire.nesthat", 1 },
            {"halloween.surgeonsuit", 1 },
            {"movembermoustachecard", 1 },
            {"movembermoustache", 1 },
            {"sunglasses2black", 1 },
            {"sunglasses2camo", 1 },
            {"sunglasses2red", 1 },
            {"sunglasses3black", 1 },
            {"sunglasses3chrome", 1 },
            {"sunglasses3gold", 1 },
            {"sunglasses", 1 },
            {"twitchsunglasses", 1 },
            {"twitch.headset", 1 },
            {"attire.reindeer.headband", 1 },
            {"santabeard", 1 },
            {"santahat", 1 },
            {"gloweyes", 1 },
            {"horse.armor.roadsign", 1 },
            {"horse.armor.wood", 1 },
            {"horse.saddle", 1 },
            {"horse.saddlebag", 1 },
            {"horse.shoes.advanced", 1 },
            {"horse.shoes.basic", 1 },
            { "fogmachine", 1 },
            {"strobelight", 1 },
            {"minihelicopter.repair", 1 },
            {"scraptransportheli.repair", 1 },
            {"submarineduo", 1 },
            {"submarinesolo", 1 },
            {"workcart", 1 },
            {"habrepair", 1 },
            {"car.key", 1 },
            {"door.key", 1 },
            {"blueprintbase", 1 },
            {"easterbasket", 1 },
            {"rustige_egg_a", 1 },
            {"rustige_egg_b", 1 },
            {"rustige_egg_c", 1 },
            {"rustige_egg_d", 1 },
            {"rustige_egg_e", 1 },
            {"easter.bronzeegg", 1 },
            {"easter.goldegg", 1 },
            {"easter.paintedeggs", 1 },
            {"easter.silveregg", 1 },
            {"halloween.candy", 1 },
            {"largecandles", 1 },
            {"smallcandles", 1 },
            {"coffin.storage", 1 },
            {"cursedcauldron", 1 },
            {"gravestone", 1 },
            {"woodcross", 1 },
            {"wall.graveyard.fence", 1 },
            {"halloween.lootbag.large", 1 },
            {"halloween.lootbag.medium", 1 },
            {"halloween.lootbag.small", 1 },
            {"pumpkinbasket", 1 },
            {"spiderweb", 1 },
            {"spookyspeaker", 1 },
            {"note", 1 },
            {"photo", 1 },
            {"giantcandycanedecor", 1 },
            {"giantlollipops", 1 },
            {"xmas.present.large", 1 },
            {"xmas.present.medium", 5},
            {"xmas.present.small", 1 },
            {"snowmachine", 1 },
            {"xmas.decoration.baubels", 1 },
            {"xmas.decoration.candycanes", 1 },
            {"xmas.decoration.gingerbreadmen", 1 },
            {"xmas.decoration.lights", 1 },
            {"xmas.decoration.pinecone", 1 },
            {"xmas.decoration.star", 1 },
            {"xmas.decoration.tinsel", 1 },
            {"keycard_blue", 1 },
            {"keycard_green", 1 },
            {"keycard_red", 1 },
            {"sickle", 1 },
            { "kayak", 1 },
            {"bbq", 1 },
            {"bed", 1 },
            {"campfire", 1 },
            {"chair", 5},
            {"composter", 1 },
            {"drone", 1 },
            {"dropbox", 5},
            {"fireplace.stone", 1 },
            {"fridge", 1 },
            {"furnace.large", 1 },
            {"furnace", 1 },
            {"hitchtroughcombo", 1 },
            {"jackolantern.angry", 1 },
            {"jackolantern.happy", 1 },
            {"lantern", 1 },
            {"box.wooden.large", 1 },
            {"water.barrel", 1 },
            {"locker", 1 },
            {"mailbox", 1 },
            {"mixingtable", 1 },
            {"small.oil.refinery", 1 },
            {"planter.large", 1 },
            {"planter.small", 1 },
            {"box.repair.bench", 1 },
            {"research.table", 1 },
            {"rug.bear", 1 },
            {"rug", 1 },
            {"secretlabchair", 5},
            {"shelves", 1 },
            {"sign.hanging.banner.large", 1 },
            {"sign.hanging", 1 },
            {"sign.hanging.ornate", 1 },
            {"sign.pictureframe.landscape", 1 },
            {"sign.pictureframe.portrait", 1 },
            {"sign.pictureframe.tall", 1 },
            {"sign.pictureframe.xl", 1 },
            {"sign.pictureframe.xxl", 1 },
            {"sign.pole.banner.large", 1 },
            {"sign.post.double", 1 },
            {"sign.post.single", 1 },
            {"sign.post.town", 1 },
            {"sign.post.town.roof", 1 },
            {"sign.wooden.huge", 1 },
            {"sign.wooden.large", 1 },
            {"sign.wooden.medium", 1 },
            {"sign.wooden.small", 1 },
            {"sleepingbag", 1 },
            {"stash.small", 5},
            {"sofa", 2},
            {"spinner.wheel", 1 },
            {"fishtrap.small", 5},
            {"table", 1 },
            {"workbench1", 1 },
            {"workbench2", 1 },
            {"workbench3", 1 },
            {"tunalight", 1 },
            {"vending.machine", 1 },
            {"water.purifier", 1 },
            {"box.wooden", 1 },
            {"botabag", 1 },
            {"chineselantern", 1 },
            {"dragondoorknocker", 1 },
            {"arcade.machine.chippy", 1 },
            {"easterdoorwreath", 1 },
            {"scarecrow", 5},
            {"skulldoorknocker", 1 },
            {"skull_fire_pit", 1 },
            {"photoframe.landscape", 1 },
            {"photoframe.large", 1 },
            {"photoframe.portrait", 1 },
            {"hobobarrel", 1 },
            {"xmas.lightstring", 2},
            {"xmas.door.garland", 1 },
            {"pookie.bear", 1 },
            {"snowman", 5},
            {"stocking.large", 1 },
            {"stocking.small", 5},
            {"xmas.window.garland", 1 },
            {"xmasdoorwreath", 1 },
            {"xmas.tree", 1 },
            {"map", 1 },
            { "ammo.grenadelauncher.buckshot", 24},
            {"ammo.grenadelauncher.he", 1 },
            {"ammo.grenadelauncher.smoke", 1 },
            {"arrow.hv", 64},
            {"arrow.wooden", 64},
            {"arrow.bone", 64},
            {"arrow.fire", 64},
            {"ammo.handmade.shell", 64},
            {"ammo.nailgun.nails", 64},
            {"ammo.pistol", 1 },
            {"ammo.pistol.fire", 1 },
            {"ammo.pistol.hv", 1 },
            {"ammo.rifle", 1 },
            {"ammo.rifle.explosive", 1 },
            {"ammo.rifle.incendiary", 1 },
            {"ammo.rifle.hv", 1 },
            {"ammo.rocket.basic", 3},
            {"ammo.rocket.fire", 3},
            {"ammo.rocket.hv", 3},
            {"ammo.rocket.smoke", 3},
            {"ammo.shotgun", 64},
            {"ammo.shotgun.fire", 64},
            {"ammo.shotgun.slug", 32},
            {"speargun.spear", 64},
            {"submarine.torpedo.straight", 1 },
            {"ammo.snowballgun", 1 },
            {"ammo.rocket.sam", 1 },
            { "door.double.hinged.metal", 1 },
            {"door.double.hinged.toptier", 1 },
            {"door.double.hinged.wood", 1 },
            {"door.hinged.metal", 1 },
            {"door.hinged.toptier", 1 },
            {"door.hinged.wood", 1 },
            {"floor.grill", 1 },
            {"floor.ladder.hatch", 1 },
            {"floor.triangle.grill", 1 },
            {"floor.triangle.ladder.hatch", 1 },
            {"gates.external.high.stone", 1 },
            {"gates.external.high.wood", 1 },
            {"ladder.wooden.wall", 5},
            {"wall.external.high.stone", 1 },
            {"wall.external.high", 1 },
            {"wall.frame.cell.gate", 1 },
            {"wall.frame.cell", 1 },
            {"wall.frame.fence.gate", 1 },
            {"wall.frame.fence", 1 },
            {"wall.frame.garagedoor", 1 },
            {"wall.frame.netting", 5},
            {"wall.frame.shopfront", 1 },
            {"wall.frame.shopfront.metal", 1 },
            {"wall.window.bars.metal", 1 },
            {"wall.window.bars.toptier", 1 },
            {"wall.window.bars.wood", 1 },
            {"shutter.metal.embrasure.a", 2},
            {"shutter.metal.embrasure.b", 2},
            {"wall.window.glass.reinforced", 1 },
            {"shutter.wood.a", 2},
            {"watchtower.wood", 5},
            {"barricade.concrete", 1 },
            {"barricade.wood.cover", 1 },
            {"barricade.metal", 1 },
            {"barricade.sandbags", 1 },
            {"barricade.stone", 1 },
            {"barricade.wood", 1 },
            {"barricade.woodwire", 1 },
            {"mining.pumpjack", 1 },
            {"mining.quarry", 1 },
            {"cupboard.tool", 1 },
            {"water.catcher.large", 1 },
            {"water.catcher.small", 1 },
            {"lock.key", 1 },
            {"lock.code", 1 },
            {"door.closer", 1 },
            {"door.hinged.industrial.a", 1 },
            {"wall.ice.wall", 1 },
            {"wall.external.high.ice", 1 },
            {"building.planner", 1 },
            { "bleach", 2},
            {"ducttape", 2},
            {"carburetor1", 5},
            {"carburetor2", 5},
            {"carburetor3", 5},
            {"crankshaft1", 5},
            {"crankshaft2", 5},
            {"crankshaft3", 5},
            {"piston1", 1 },
            {"piston2", 1 },
            {"piston3", 1 },
            {"sparkplug1", 2},
            {"sparkplug2", 2},
            {"sparkplug3", 2},
            {"valve1", 1 },
            {"valve2", 1 },
            {"valve3", 1 },
            {"fuse", 1 },
            {"gears", 2},
            {"glue", 1 },
            {"metalblade", 2},
            {"metalpipe", 2},
            {"propanetank", 5},
            {"roadsigns", 2},
            {"rope", 5},
            {"sewingkit", 2},
            {"sheetmetal", 2},
            {"metalspring", 2},
            {"sticks", 1 },
            {"tarp", 2},
            {"techparts", 5},
            {"riflebody", 1 },
            {"semibody", 1 },
            {"smgbody", 1 },
            {"vehicle.chassis.2mod", 1 },
            {"vehicle.chassis.3mod", 1 },
            {"vehicle.chassis.4mod", 1 },
            {"vehicle.chassis", 1 },
            {"vehicle.1mod.cockpit", 1 },
            {"vehicle.1mod.cockpit.armored", 1 },
            {"vehicle.1mod.cockpit.with.engine", 1 },
            {"vehicle.1mod.engine", 1 },
            {"vehicle.1mod.flatbed", 1 },
            {"vehicle.1mod.passengers.armored", 1 },
            {"vehicle.1mod.rear.seats", 1 },
            {"vehicle.1mod.storage", 1 },
            {"vehicle.1mod.taxi", 1 },
            {"vehicle.2mod.flatbed", 1 },
            {"vehicle.2mod.fuel.tank", 1 },
            {"vehicle.2mod.passengers", 1 },
            {"vehicle.module", 1 },
            { "trap.bear", 3},
            {"spikes.floor", 1 },
            {"trap.landmine", 5},
            {"guntrap", 1 },
            {"flameturret", 1 },
            {"samsite", 1 },
            { "ceilinglight", 1 },
            {"computerstation", 1 },
            {"elevator", 5},
            {"modularcarlift", 1 },
            {"electric.audioalarm", 5},
            {"smart.alarm", 5},
            {"smart.switch", 5},
            {"storage.monitor", 1 },
            {"electric.battery.rechargable.large", 1 },
            {"electric.battery.rechargable.medium", 1 },
            {"electric.battery.rechargable.small", 1 },
            {"electric.button", 5},
            {"electric.counter", 5},
            {"electric.hbhfsensor", 1 },
            {"electric.laserdetector", 5},
            {"electric.pressurepad", 1 },
            {"electric.doorcontroller", 5},
            {"electric.heater", 5},
            {"fluid.combiner", 5},
            {"fluid.splitter", 5},
            {"fluid.switch", 1 },
            {"electric.andswitch", 5},
            {"electric.blocker", 5},
            {"electrical.branch", 5},
            {"electrical.combiner", 5},
            {"electrical.memorycell", 5},
            {"electric.orswitch", 5},
            {"electric.random.switch", 5},
            {"electric.rf.broadcaster", 1 },
            {"electric.rf.receiver", 1 },
            {"electric.xorswitch", 5},
            {"electric.fuelgenerator.small", 1 },
            {"electric.generator.small", 1 },
            {"electric.solarpanel.large", 3},
            {"electric.igniter", 3},
            {"electric.flasherlight", 5},
            {"electric.simplelight", 1 },
            {"electric.sirenlight", 5},
            {"powered.water.purifier", 1 },
            {"electric.switch", 5},
            {"electric.splitter", 5},
            {"electric.sprinkler", 1 },
            {"electric.teslacoil", 3 },
            {"electric.timer", 5},
            {"electric.cabletunnel", 1 },
            {"waterpump", 1 },
            {"target.reactive", 1 },
            {"searchlight", 1 },
            {"generator.wind.scrap", 1 },
            {"sign.neon.125x125", 1 },
            {"sign.neon.125x215.animated", 1 },
            {"sign.neon.125x215", 1 },
            {"sign.neon.xl.animated", 1 },
            {"sign.neon.xl", 1 },
            {"xmas.lightstring.advanced", 1 },
            {"autoturret", 1 },
            {"hosetool", 1 },
            {"rf_pager", 1 },
            {"wiretool", 1 },
            { "firework.boomer.blue", 2},
            {"firework.boomer.champagne", 2},
            {"firework.boomer.green", 2},
            {"firework.boomer.orange", 2},
            {"firework.boomer.red", 2},
            {"firework.boomer.violet", 2},
            {"firework.romancandle.blue", 2},
            {"firework.romancandle.green", 2},
            {"firework.romancandle.red", 2},
            {"firework.romancandle.violet", 2},
            {"firework.volcano", 2},
            {"firework.volcano.red", 2},
            {"firework.volcano.violet", 2},
            {"fun.bass", 1 },
            {"fun.cowbell", 1 },
            {"drumkit", 1 },
            {"fun.flute", 1 },
            {"fun.guitar", 1 },
            {"fun.jerrycanguitar", 1 },
            {"piano", 1 },
            {"fun.tambourine", 1 },
            {"fun.trumpet", 1 },
            {"fun.tuba", 1 },
            {"xylophone", 1 },
            {"newyeargong", 1 },
            {"lunar.firecrackers", 5},
            {"skullspikes.candles", 1 },
            {"skullspikes.pumpkin", 1 },
            {"skullspikes", 1 },
            {"skull.trophy.jar", 1 },
            {"skull.trophy.jar2", 1 },
            {"skull.trophy.table", 1 },
            {"skull.trophy", 1 },
            {"abovegroundpool", 1 },
            {"beachchair", 1 },
            {"beachparasol", 1 },
            {"beachtable", 1 },
            {"beachtowel", 1 },
            {"boogieboard", 1 },
            {"innertube", 1 },
            {"innertube.horse", 1 },
            {"innertube.unicorn", 1 },
            {"paddlingpool", 1 },
            {"sled.xmas", 1 },
            {"sled", 1 },
            {"wrappedgift", 1 },
            {"wrappingpaper", 1 },
            {"boombox", 1 },
            {"fun.boomboxportable", 1 },
            {"cassette", 1 },
            {"cassette.medium", 1 },
            {"cassette.short", 1 },
            {"fun.casetterecorder", 1 },
            {"discoball", 5},
            {"discofloor", 5},
            {"discofloor.largetiles", 5},
            {"connected.speaker", 5},
            {"laserlight", 5},
            {"megaphone", 1 },
            {"microphonestand", 5},
            {"mobilephone", 1 },
            {"soundlight", 5},
            {"telephone", 1 },
            { "apple", 1 },
            {"apple.spoiled", 1 },
            {"black.raspberries", 1 },
            {"blueberries", 2},
            {"grub", 2},
            {"worm", 2},
            {"cactusflesh", 1 },
            {"can.beans", 1 },
            {"can.tuna", 1 },
            {"chocholate", 1 },
            {"fish.anchovy", 1 },
            {"fish.catfish", 5},
            {"fish.cooked", 2},
            {"fish.raw", 2},
            {"fish.herring", 1 },
            {"fish.minnows", 1 },
            {"fish.orangeroughy", 5},
            {"fish.salmon", 1 },
            {"fish.sardine", 1 },
            {"fish.smallshark", 5},
            {"fish.troutsmall", 1 },
            {"fish.yellowperch", 1 },
            {"granolabar", 1 },
            {"chicken.burned", 2},
            {"chicken.cooked", 2},
            {"chicken.raw", 2},
            {"chicken.spoiled", 2},
            {"deermeat.burned", 2},
            {"deermeat.cooked", 2},
            {"deermeat.raw", 2},
            {"horsemeat.burned", 2},
            {"horsemeat.cooked", 2},
            {"horsemeat.raw", 2},
            {"humanmeat.burned", 2},
            {"humanmeat.cooked", 2},
            {"humanmeat.raw", 2},
            {"humanmeat.spoiled", 2},
            {"bearmeat.burned", 2},
            {"bearmeat.cooked", 2},
            {"bearmeat", 2},
            {"wolfmeat.burned", 2},
            {"wolfmeat.cooked", 2},
            {"wolfmeat.raw", 2},
            {"wolfmeat.spoiled", 2},
            {"meat.pork.burned", 2},
            {"meat.pork.cooked", 2},
            {"meat.boar", 2},
            {"mushroom", 1 },
            {"jar.pickle", 1 },
            {"smallwaterbottle", 1 },
            {"waterjug", 1 },
            {"candycane", 1 },
            {"black.berry", 2},
            {"clone.black.berry", 5},
            {"seed.black.berry", 5},
            {"blue.berry", 2},
            {"clone.blue.berry", 5},
            {"seed.blue.berry", 5},
            {"green.berry", 2},
            {"clone.green.berry", 5},
            {"seed.green.berry", 5},
            {"red.berry", 2},
            {"clone.red.berry", 5},
            {"seed.red.berry", 5},
            {"white.berry", 2},
            {"clone.white.berry", 5},
            {"seed.white.berry", 5},
            {"yellow.berry", 2},
            {"clone.yellow.berry", 5},
            {"seed.yellow.berry", 5},
            {"corn", 2},
            {"clone.corn", 5},
            {"seed.corn", 5},
            {"clone.hemp", 5},
            {"seed.hemp", 5},
            {"potato", 2},
            {"clone.potato", 5},
            {"seed.potato", 5},
            {"pumpkin", 2},
            {"clone.pumpkin", 5},
            {"seed.pumpkin", 5},
            {"healingtea.advanced", 1 },
            {"healingtea", 1 },
            {"healingtea.pure", 1 },
            {"maxhealthtea.advanced", 1 },
            {"maxhealthtea", 1 },
            {"maxhealthtea.pure", 1 },
            {"oretea.advanced", 1 },
            {"oretea", 1 },
            {"oretea.pure", 1 },
            {"radiationremovetea.advanced", 1 },
            {"radiationremovetea", 1 },
            {"radiationremovetea.pure", 1 },
            {"radiationresisttea.advanced", 1 },
            {"radiationresisttea", 1 },
            {"radiationresisttea.pure", 1 },
            {"scraptea.advanced", 1 },
            {"scraptea", 1 },
            {"scraptea.pure", 1 },
            {"woodtea.advanced", 1 },
            {"woodtea", 1 },
            {"woodtea.pure", 1 },
            { "skull.human", 1 },
            {"coal", 1 },
            {"fat.animal", 1 },
            {"battery.small", 1 },
            {"bone.fragments", 1 },
            {"cctv.camera", 64 },
            {"charcoal", 1 },
            {"cloth", 1 },
            {"crude.oil", 5},
            {"diesel_barrel", 2},
            {"can.beans.empty", 1 },
            {"can.tuna.empty", 1 },
            {"explosives", 1 },
            {"fertilizer", 1 },
            {"gunpowder", 1 },
            {"horsedung", 5},
            {"hq.metal.ore", 1 },
            {"metal.refined", 1 },
            {"leather", 1 },
            {"lowgradefuel", 5},
            {"metal.fragments", 1 },
            {"metal.ore", 1 },
            {"paper", 1 },
            {"plantfiber", 1 },
            {"researchpaper", 1 },
            {"scrap", 1 },
            {"stones", 1 },
            {"sulfur.ore", 1 },
            {"sulfur", 1 },
            {"targeting.computer", 64 },
            {"skull.wolf", 1 },
            {"wood", 1 },
            {"tool.instant_camera", 1 },
            {"tool.binoculars", 1 },
            {"explosive.timed", 1 },
            {"tool.camera", 1 },
            {"rf.detonator", 1 },
            {"fishingrod.handmade", 1 },
            {"flare", 5},
            {"flashlight.held", 1 },
            {"geiger.counter", 1 },
            {"jackhammer", 1 },
            {"grenade.smoke", 3 },
            {"supply.signal", 1 },
            {"surveycharge", 1 },
            {"cakefiveyear", 1 },
            {"chainsaw", 1 },
            {"hammer", 1 },
            {"hatchet", 1 },
            {"pickaxe", 1 },
            {"rock", 1 },
            {"axe.salvaged", 1 },
            {"hammer.salvaged", 1 },
            {"icepick.salvaged", 1 },
            {"explosive.satchel", 1 },
            {"stonehatchet", 1 },
            {"stone.pickaxe", 1 },
            {"toolgun", 1 },
            {"torch", 1 },
            {"bucket.water", 1 },
            { "gun.water", 1 },
            {"pistol.water", 1 },
            {"candycaneclub", 1 },
            {"snowball", 1 },
            {"snowballgun", 1 },
            {"weapon.mod.8x.scope", 1 },
            {"weapon.mod.flashlight", 1 },
            {"weapon.mod.holosight", 1 },
            {"weapon.mod.lasersight", 1 },
            {"weapon.mod.muzzleboost", 1 },
            {"weapon.mod.muzzlebrake", 1 },
            {"weapon.mod.simplesight", 1 },
            {"weapon.mod.silencer", 1 },
            {"weapon.mod.small.scope", 1 },
            {"rifle.ak", 1 },
            {"grenade.beancan", 5},
            {"rifle.bolt", 1 },
            {"bone.club", 1 },
            {"knife.bone", 1 },
            {"bow.hunting", 1 },
            {"salvaged.cleaver", 1 },
            {"bow.compound", 1 },
            {"crossbow", 1 },
            {"shotgun.double", 1 },
            {"pistol.eoka", 1 },
            {"grenade.f1", 5},
            {"flamethrower", 1 },
            {"multiplegrenadelauncher", 1 },
            {"knife.butcher", 1 },
            {"pitchfork", 1 },
            {"knife.combat", 1 },
            {"rifle.l96", 1 },
            {"rifle.lr3", 1 },
            {"lmg.m249", 1 },
            {"rifle.m39", 1 },
            {"pistol.m92", 1 },
            {"mace", 1 },
            {"machete", 1 },
            {"smg.mp5", 1 },
            {"pistol.nailgun", 1 },
            {"paddle", 1 },
            {"shotgun.waterpipe", 1 },
            {"pistol.python", 1 },
            {"pistol.revolver", 1 },
            {"rocket.launcher", 1 },
            {"shotgun.pump", 1 },
            {"pistol.semiauto", 1 },
            {"rifle.semiauto", 1 },
            {"smg.2", 1 },
            {"shotgun.spas12", 1 },
            {"speargun", 1 },
            {"spear.stone", 1 },
            {"longsword", 1 },
            {"salvaged.sword", 1 },
            {"smg.thompson", 1 },
            {"spear.wooden", 1 },
            { "blood", 1 },
            {"antiradpills", 1 },
            {"largemedkit", 1 },
            {"syringe.medical", 2},
            {"bandage", 3},
        };

        private readonly HashSet<string> _exclude = new HashSet<string>
        {
            "water",
            "water.salt",
            "cardtable",
        };

        #endregion
        
        #region Config

        private PluginConfig _configData;
        
        protected override void LoadDefaultConfig() => _configData = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _configData = Config.ReadObject<PluginConfig>();

                if (_configData == null)
                {
                    PrintWarning($"Generating Config File for GUIShop");
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_configData))
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
        protected override void SaveConfig() => Config.WriteObject(_configData, true);
        private void CheckConfig()
        {
            foreach (ItemDefinition item in ItemManager.itemList)
            {
                string categoryName = item.category.ToString();

                Dictionary<string, _Items> stackCategory;
                
                if (!_configData.StackCategoryMultipliers.ContainsKey(categoryName))
                {
                    _configData.StackCategoryMultipliers[categoryName] = 1;
                }

                if (!_configData.StackCategories.TryGetValue(categoryName, out stackCategory))
                {
                    _configData.StackCategories[categoryName] = stackCategory = new Dictionary<string, _Items>();
                }

                if (_exclude.Contains(item.shortname)) continue;

                if (!stackCategory.ContainsKey(item.shortname))
                {
                    stackCategory.Add(item.shortname, new _Items
                    {
                        DisplayName = item.displayName.english,
                        Modified = item.stackable,
                    });
                }

                if (_configData.StackCategoryMultipliers[categoryName] >= 1)
                {
                    item.stackable = _configData.StackCategoryMultipliers[categoryName];
                }

                foreach (var i in defaults)
                {
                    if (_configData.StackCategories[categoryName][item.shortname].Modified >= i.Value && _configData.StackCategories[categoryName][item.shortname].Modified >= _configData.StackCategoryMultipliers[categoryName])
                    {
                        item.stackable = _configData.StackCategories[categoryName][item.shortname].Modified;
                    }
                }

            }
            SaveConfig();
        }
        internal class PluginConfig : SerializableConfiguration
        {
            [JsonProperty("Ignore Admins")] 
            public bool IsAdmin = true;
            
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
            public Dictionary<string, Dictionary<string, _Items>> StackCategories = new Dictionary<string, Dictionary<string, _Items>>();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());

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
            public string DisplayName;
            public int Modified;
        }
        
        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>().ToDictionary(prop => prop.Name, prop => ToObject(prop.Value));
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

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
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

        #region Oxide

        private void Unload()
        {
            if (_configData.Reset)
            {
                ResetStacks();
            }
        }

        private void Init()
        {
            Unsubscribe(nameof(OnItemAddedToContainer));
        }

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is NPCVendingMachine && !(entity is InvisibleVendingMachine))
                {
                    _sellOrders[entity.net.ID] = (entity as NPCVendingMachine).sellOrders.sellOrders;
                }
            }
            permission.RegisterPermission(ByPass, this);
            CheckConfig();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is NPCVendingMachine && !(entity is InvisibleVendingMachine))
                {
                    var e = entity as NPCVendingMachine;
                    e.ClearSellOrders();
                    e.ClearPendingOrder();
                    e.inventory.Clear();
                    e.sellOrders.sellOrders = _sellOrders[entity.net.ID];
                    e.InstallFromVendingOrders();
                }
                var vending = entity as InvisibleVendingMachine;
                if (entity is InvisibleVendingMachine)
                {
                    if (vending != null && !vending.IsDestroyed)
                    {
                        foreach (var s in vending.vendingOrders.orders)
                        {
                            if (defaults.ContainsKey(s.sellItem.shortname))
                            {
                                s.sellItemAmount = defaults[s.sellItem.shortname];
                            }
                        }
                    }
                }
            }
            
            Subscribe(nameof(OnItemAddedToContainer));
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

            if (_configData.Blocked.Contains(item.info.shortname) && item.GetOwnerPlayer() != null && !permission.UserHasPermission(item.GetOwnerPlayer().UserIDString, ByPass))
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
                    item.parent.playerOwner.GiveItem(ItemManager.CreateByItemID(containedItem.info.itemid, containedItem.amount));
                }
            }

            if (_configData.DisableFix)
            {
                return null;
            }
                
            BaseProjectile.Magazine itemMag = targetItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
            
            if (itemMag != null)
            {
                if (itemMag.contents > 0)
                {
                    item.GetOwnerPlayer().GiveItem(ItemManager.CreateByItemID(itemMag.ammoType.itemid,itemMag.contents));

                    itemMag.contents = 0;
                }
            }
            
            if (targetItem.GetHeldEntity() is FlameThrower)
            {
                FlameThrower flameThrower = targetItem.GetHeldEntity().GetComponent<FlameThrower>();

                if (flameThrower.ammo > 0)
                {
                    item.GetOwnerPlayer().GiveItem(ItemManager.CreateByItemID(flameThrower.fuelType.itemid,flameThrower.ammo));

                    flameThrower.ammo = 0;
                }
            }
            
            if (targetItem.GetHeldEntity() is Chainsaw)
            {
                Chainsaw chainsaw = targetItem.GetHeldEntity().GetComponent<Chainsaw>();

                if (chainsaw.ammo > 0)
                {
                    item.GetOwnerPlayer().GiveItem(ItemManager.CreateByItemID(chainsaw.fuelType.itemid,chainsaw.ammo));

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
            if (item.skinID != targetItem.skinID || item.item.name != targetItem.item.name)
            {
                return false;
            }

            if (item.item.contents != null || targetItem.item.contents != null)
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

                if (water != null) LiquidContainer.contents.AddItem(ItemManager.FindItemDefinition(-1779180711), water.amount);

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
                
                return x;
            }

            Item newItem = ItemManager.CreateByItemID(item.info.itemid);

            BaseProjectile.Magazine newItemMag = newItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;

            if (newItem.contents?.itemList.Count == 0 && (_configData.DisableFix || newItem.contents?.itemList.Count == 0 && newItemMag?.contents == 0))
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
            
            if (item.info.amountType == ItemDefinition.AmountType.Genetics && item.instanceData != null && item.instanceData.dataInt != 0)
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

            if (_configData.VendingMachineAmmoFix && item.GetRootContainer()?.entityOwner is VendingMachine)
            {
                return newItem;
            }

            if (_configData.DisableFix)
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
            
            BaseProjectile.Magazine itemMagDefault = newItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
            if (itemMagDefault != null && itemMagDefault.contents > 0)
            {
                itemMagDefault.contents = 0;
            }

            return newItem;
        }
        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            BasePlayer player = container.GetOwnerPlayer();

            if (player == null || _configData.IsAdmin && player.IsAdmin) return;
            
            if ((player.inventory.containerMain.uid == container.uid || player.inventory.containerBelt.uid == container.uid) && item.amount > item.MaxStackable())
            {
                int division = item.amount / item.MaxStackable();

                for (int i = 0; i < division; i++)
                {
                    Item y = item.SplitItem(item.MaxStackable());
                    if (y != null && !y.MoveToContainer(player.inventory.containerMain, -1, false) && (item.parent == null || !y.MoveToContainer(item.parent)))
                    {
                        y.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                    }
                }
            }

            if (player.inventory.containerWear.uid != container.uid) return;
            if (item.amount <= 1) return;
            int amount = item.amount -= 1;
            player.inventory.containerWear.Take(null, item.info.itemid, amount - 1);
            Item x = ItemManager.CreateByItemID(item.info.itemid, item.amount, item.skin);
            x.name = item.name;
            x.skin = item.skin;
            x.amount = amount;
            x._condition = item._condition;
            x._maxCondition = item._maxCondition;
            //x.MarkDirty();
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
                if (x != null && !x.MoveToContainer(player.inventory.containerMain, -1, false) && (item.parent == null || !x.MoveToContainer(item.parent)))
                {
                    x.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                }
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
                if (!_configData.StackCategories.ContainsKey(itemDefinition.category.ToString())) continue;
                Dictionary<string, _Items> stackCategory = _configData.StackCategories[itemDefinition.category.ToString()];
                if (!stackCategory.ContainsKey(itemDefinition.shortname)) continue;
                if (!defaults.ContainsKey(itemDefinition.shortname)) continue;
                int a = defaults[itemDefinition.shortname];
                itemDefinition.stackable = a;
            }
        }

        #endregion

        #region ResetVendors
        private void destroyvending()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var vending = entity as InvisibleVendingMachine;
                if (entity is InvisibleVendingMachine)
                {
                    if (vending != null && !vending.IsDestroyed)
                    {
                        vending.InstallFromVendingOrders();
                        foreach (var s in vending.vendingOrders.orders)
                        {
                            if (defaults.ContainsKey(s.sellItem.shortname))
                            {
                                s.sellItemAmount = defaults[s.sellItem.shortname];
                            }
                        }
                        vending.SendNetworkUpdate();
                    }
                }
            }
        }

        [Command("resetvenders")]
        private void cmddvend(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            destroyvending();
            player.Reply("All Venders Restored");
        }

        #endregion
    }
}