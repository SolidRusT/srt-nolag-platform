#!/bin/bash
# Once server is finished loading and running

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

cd ${GAME_DIR}

# for perm in ${DEFAULT_PERMS[@]}; do ./rcon -c ~/solidrust.net/defaults/rcon.yaml "o.grant group default $perm"; done
DEFAULT_PERMS=(
skins.use  \
craftchassis.2  \
removertool.normal  \
baserepair.use  \
autolock.use  \
backpacks.gui  \
backpacks.use  \
kits.defaultspawn  \
bank.use  \
buildinggrades.cangrade  \
buildinggrades.down  \
bgrade.all  \
vehicledeployedlocks.codelock.allvehicles  \
vehicledeployedlocks.keylock.allvehicles  \
carlockui.use.codelock  \
carturrets.limit.2  \
carturrets.allmodules  \
trade.use  \
trade.accept  \
recycle.use  \
discordcalladmin.use  \
carturrets.deploy.command  \
carturrets.deploy.inventory  \
nteleportation.home  \
nteleportation.deletehome  \
nteleportation.homehomes  \
nteleportation.importhomes  \
nteleportation.radiushome  \
nteleportation.tpr  \
nteleportation.tpb  \
nteleportation.tphome  \
nteleportation.tptown  \
nteleportation.tpoutpost  \
nteleportation.tpbandit  \
nteleportation.wipehomes  \
securitylights.use  \
vehiclevendoroptions.ownership.allvehicles  \
autodoors.use  \
automaticauthorization.use  \
largercarstorage.size.4  \
barrelpoints.default  \
discordcore.use  \
itemskinrandomizer.use  \
itemskinrandomizer.reskin  \
instantcraft.use  \
randomrespawner.use  \
furnacesplitter.use  \
tcmapmarkers.use  \
realistictorch.use  \
fastloot.use  \
boxsorterlite.use  \
raidalarm.use  \
clearrepair.use  \
mushroomeffects.use  \
treeplanter.use  \
bounty.use  \
dronepilot.use  \
farmtools.clone  \
farmtools.clone.all  \
farmtools.harvest.all  \
turretloadouts.autoauth  \
turretloadouts.autotoggle  \
turretloadouts.manage  \
turretloadouts.manage.custom  \
claimrewards.use  \
heal.self  \
heal.player  \
blueprintshare.toggle  \
blueprintshare.share  \
recyclerspeed.use  \
discordrewards.use \
dance.use \
securitycameras.use \
simpletime.use \
whoknocks.message \
whoknocks.knock \
eventrandomizer.check \
cctvutilities.help \
cctvutilities.status.me \
cctvutilities.status.server \
cctvutilities.clear \
cctvutilities.rename \
cctvutilities.add.me \
cctvutilities.add.server \
cctvutilities.add.clear \
cctvutilities.autoname \
cctvutilities.autoadd \
cctvutilities.autoadd.on \
cctvutilities.autoadd.off \
cctvutilities.autoadd.toggle \
cctvutilities.autoadd.me \
cctvutilities.autoadd.server \
chute.allowed \
buildinggrades.use \
buildinggrades.up.all \
buildinggrades.down.all \
hazmattoscientistsuit.use \
hazmattoscientistsuit.craft \
teamping.use
)

./rcon -c ${HOME}/solidrust.net/defaults/rcon.yaml "o.load *"

for perm in ${DEFAULT_PERMS[@]}; do
    echo "./rcon -c ${HOME}/solidrust.net/defaults/rcon.yaml \"o.grant default $perm\""
    ./rcon -c ${HOME}/solidrust.net/defaults/rcon.yaml "o.grant group default $perm"
    sleep 2
done

./rcon -c ${HOME}/solidrust.net/defaults/rcon.yaml "o.reload PermissionGroupSync"

