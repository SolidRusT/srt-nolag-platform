
DEFAULT_PERMS=(
skins.use \
craftchassis.2 \
removertool.normal \
baserepair.use \
autolock.use \
backpacks.gui \
backpacks.use \
kits.defaultspawn \
bank.use \
buildinggrades.cangrade \
buildinggrades.down \
bgrade.all \
vehicledeployedlocks.codelock.allvehicles \
vehicledeployedlocks.keylock.allvehicles \
carlockui.use.codelock \
carturrets.limit.2 \
carturrets.allmodules \
trade.use \
trade.accept \
recycle.use \
discordcalladmin.use \
carturrets.deploy.command \
carturrets.deploy.inventory \
nteleportation.home \
nteleportation.deletehome \
nteleportation.homehomes \
nteleportation.importhomes \
nteleportation.radiushome \
nteleportation.tpr \
nteleportation.tpb \
nteleportation.tphome \
nteleportation.tptown \
nteleportation.tpoutpost \
nteleportation.tpbandit \
nteleportation.wipehomes \
securitylights.use \
vehiclevendoroptions.ownership.allvehicles \
autodoors.use \
automaticauthorization.use \
largercarstorage.size.4 \
barrelpoints.default \
discordcore.use \
itemskinrandomizer.use \
itemskinrandomizer.reskin \
instantcraft.use \
randomrespawner.use \
furnacesplitter.use \
tcmapmarkers.use \
realistictorch.use \
fastloot.use \
boxsorterlite.use \
raidalarm.use \
clearrepair.use \
mushroomeffects.use \
treeplanter.use \
bounty.use \
dronepilot.use \
farmtools.clone \
farmtools.clone.all \
farmtools.harvest.all \
turretloadouts.autoauth \
turretloadouts.autotoggle \
turretloadouts.manage \
turretloadouts.manage.custom \
claimrewards.use \
heal.self \
heal.player \
blueprintshare.toggle \
blueprintshare.share \
recyclerspeed.use
)

./rcon -c rcon.yaml "o.load *"

for perm in ${DEFAULT_PERMS[@]}; do
    echo "./rcon -c rcon.yaml \"o.grant default $perm\""
    ./rcon -c rcon.yaml "o.grant group default $perm"
    sleep 2
done

./rcon -c rcon.yaml "o.reload PermissionGroupSync"

