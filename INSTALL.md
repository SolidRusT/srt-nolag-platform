# Install rust server

Assuming you will only be running this one game on the server
install to your home `~/`.

```bash
# As the game server user
steamcmd +login anonymous +force_install_dir ~/ +app_update 258550 +quit
steamcmd +login anonymous +force_install_dir ~/ +app_update 258550 validate +quit
```

### OPTIONAL - Modded Install *ONLY* (Oxide)

By doing this part, your server will be listed as `Modded` instead of `community`.

In this example, RustDedicated is installed in `~/`, so we will put Oxide files there too.

```bash
# As the game server user
cd ~/
wget https://umod.org/games/rust/download/develop -O Oxide.Rust.zip
unzip -o Oxide.Rust.zip
rm Oxide.Rust.zip
aws s3 sync --quiet s3://suparious.com/oxide oxide
#aws s3 sync --quiet oxide s3://suparious.com/oxide

wget https://umod.org/extensions/discord/download -O ~/RustDedicated_Data/Managed/Oxide.Ext.Discord.dll
```

## Start the server

Starting the server for the first time helps generate the server data so we can validate our plugins. Once the server is fully started and working, use `ctrl + c` and wait for it to shutdown gracefully.

```bash
chmod +x start.sh
./start.sh
```

## Optional Customizations and final notes

### Kits

```
/kit add autokit
/kit items authlevel 2 hide true

/kit add builder
/kit items max 3 cooldown 86400

/kit add scuba
/kit items max 5 cooldown 3600

/kit add vip1
/kit items max 2 permission vip

o.reload Kits
```

### User and Group Permissions

```
ownerid 76561198024774727 "Suparious"

ownerid 76561198206550912 "ThePastaMasta"
server.writecfg

oxide.show groups
oxide.show perms

oxide.group add GM
oxide.group add dev
oxide.group add discord
oxide.group add vip

oxide.group parent default ""
oxide.group parent discord default
oxide.group parent dev default
oxide.group parent vip default
oxide.group parent GM default
oxide.group parent admin default

#######

server.writecfg
```

default perms

```
"removertool.normal", "instantcraft.use", "quicksmelt.use", "recyclerspeed.use", "nteleportation.home", "nteleportation.deletehome", "nteleportation.homehomes", "nteleportation.radiushome", "nteleportation.tpb", "nteleportation.tpr", "nteleportation.tphome", "nteleportation.tptown", "nteleportation.tpoutpost", "nteleportation.tpbandit", "nteleportation.tpn", "nteleportation.tpl", "nteleportation.tpremove", "nteleportation.tpsave", "nteleportation.wipehomes", "fuelgauge.allow", "pets.wolf", "pets.stag", "pets.horse", "pets.chicken", "pets.boar", "pets.bear", "skins.use", "trade.use", "trade.accept", "bank.use", "whoknocks.message", "whoknocks.knock", "signartist.url", "signartist.text", "signartist.restore", "bgrade.all", "signartist.raw", "signartist.restoreall", "backpacks.use", "backpacks.keeponwipe", "tcgui.use", "backpacks.gui", "randomrespawner.use", "respawnprotection.use", "itemskinrandomizer.use", "itemskinrandomizer.reskin", "workshopskinviewer.use", "vehicledeployedlocks.codelock.allvehicles", "vehicledeployedlocks.keylock.allvehicles", "discordmessages.report", "discordmessages.message", "carlockui.use.codelock", "carturrets.allmodules", "carturrets.deploy.inventory", "carturrets.deploy.command", "carturrets.limit.4", "storagemonitorcontrol.owner.all", "carcodelocks.ui", "infomenu.view", "warcopter.spawn", "warcopter.drone", "warcopter.fighter", "discordrewards.use", "heal.player", "heal.self", "airstrike.signal.strike", "airstrike.signal.squad", "airstrike.purchase.strike", "airstrike.purchase.squad", "airstrike.chat.strike", "airstrike.chat.squad", "diseases.info", "privatemessages.allow", "claimvehicle.claim.allvehicles", "automaticauthorization.use", "baserepair.use", "autolock.use", "cctvutilities.help", "cctvutilities.status.me", "cctvutilities.status.server", "cctvutilities.add.me", "cctvutilities.autoname", "cctvutilities.autoadd", "cctvutilities.autoadd.on", "cctvutilities.autoadd.off", "cctvutilities.autoadd.toggle", "cctvutilities.autoadd.me", "cctvutilities.status.custom", "cctvutilities.add.all", "cctvutilities.autoadd.server", "carcodelocks.use", "claimvehicle.unclaim", "fastloot.use", "largercarstorage.size.7", "craftingcontroller.setskins", "eventrandomizer.check", "discordcore.use", "signmap.use", "discordcore.plugins", "barrelpoints.default", "lottery.canuse", "nightlantern.lanterns", "nightlantern.tunalight", "nightlantern.searchlight", "treeplanter.use", "autocode.use", "furnacesplitter.use", "globalmail.use", "kits.defaultspawn", "framebox.use", "surveygather.use", "gesturewheel.use", "roadfinder.use", "nteleportation.importhomes", "playtimesupplysignal.bonus"
```

# oxide.grant group <group> <permission>
oxide.grant group default nteleportation.tpbandit

aws s3 sync --quiet --delete rust-west.zip s3://suparious.com/

UserManagement

```
Users and groups
DiscordSpawn add 76561198886543733 (SmokeQc)
DiscordSpawn add 76561199135759930 (Ratchet)
DiscordSpawn add 76561198421090963 (Hannaht56)
DiscordSpawn add 76561198852895608 (WeirdAl)
DiscordSpawn add 76561198024774727 (Suparious)

VipSpawn add 76561198852895608 (WeirdAl)
VipSpawn add 76561198421090963 (Hannaht56)
VipSpawn add 76561199135759930 (Ratchet)

DiscordSpawn add 76561199016007366 (1F-01 | J. Kilo)
DiscordSpawn add 76561199078529202 (thatlegitgamer310)
DiscordSpawn add 76561199051464652 (76561199051464652)


VipSpawn add 76561199016007366 (1F-01 | J. Kilo)
VipSpawn add 76561199078529202 (thatlegitgamer310)
VipSpawn add 76561199051464652 (76561199051464652)

DevSpawn add 76561198886543733 (SmokeQc)

GMSpawn add 76561198206550912 (ThePastaMasta)

AdminSpawn add 76561198024774727 (Suparious)


oxide.usergroup add Suparious default
```

```

oxide.usergroup add TheBoxiestCat default
oxide.usergroup add parkourgriffin default
oxide.usergroup add Ratchet default

oxide.usergroup add nick default
oxide.usergroup add miles96690 default
oxide.usergroup add Zombie60 default
oxide.usergroup add Hannaht56 default
oxide.usergroup add WeirdAl default
oxide.usergroup add ogjoed default


```

#### Give items

```
inventory.give “short name” “amount”
inventory.giveto “player name” “short name” “amount”
```

#### References

* https://www.corrosionhour.com/umod-permissions-guide/
* https://www.gameserverkings.com/knowledge-base/rust/oxide-permissions-101/
* https://www.corrosionhour.com/rust-item-list/
* https://rust.fandom.com/wiki/Server_Commands
* http://oxidemod.org/threads/updating-adding-without-restarting-server.28250/
* https://www.corrosionhour.com/rust-give-command/


### Maps

```
size: 6000
seed: 7880972
```


### TODO

```
https://umod.org/plugins/donation-claim
https://umod.org/plugins/gpay
https://umod.org/plugins/player-administration
https://umod.org/plugins/steam-groups
https://umod.org/community/rust-kits/2313-changing-gui-background-image?page=2#post-23

https://umod.org/plugins/server-info

https://umod.org/plugins/pop-up-manager
https://umod.org/plugins/offline-mail
https://umod.org/plugins/kings-lottery
https://umod.org/plugins/vote-for-money


https://umod.org/plugins/spawn-logger
```


sudo dpkg --add-architecture i386; sudo apt-get update;sudo apt-get install mailutils postfix curl wget file bzip2 gzip unzip bsdmainutils python util-linux ca-certificates binutils bc tmux lib32gcc1 libstdc++6 libstdc++6:i386 lib32z1