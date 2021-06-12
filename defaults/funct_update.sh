function update_repo () {
    echo "Downloading repo from s3" | tee -a ${LOGS}
    aws s3 sync --only-show-errors --delete ${S3_BACKUPS}/repo ${HOME}/solidrust.net | tee -a ${LOGS}
    echo "Setting execution bits" | tee -a ${LOGS}
    chmod +x ${HOME}/solidrust.net/defaults/*.sh
    chmod +x ${HOME}/solidrust.net/defaults/database/*.sh
    chmod +x ${HOME}/solidrust.net/defaults/web/*.sh
}

function update_mods () {
    OXIDE=(
        oxide/data
        oxide/config
    )
    for folder in ${OXIDE[@]}; do
        # Sync global Oxide defaults
        echo "sync ${SERVER_GLOBAL}/$folder/ to ${GAME_ROOT}/$folder" | tee -a ${LOGS}
        mkdir -p "${GAME_ROOT}/$folder" | tee -a ${LOGS}
        rsync -r "${SERVER_GLOBAL}/$folder/" "${GAME_ROOT}/$folder" | tee -a ${LOGS}
        # Sync custom Oxide overrides
        echo "sync ${SERVER_CUSTOM}/$folder/ to ${GAME_ROOT}/$folder" | tee -a ${LOGS}
        mkdir -p "${GAME_ROOT}/$folder" | tee -a ${LOGS}
        rsync -r "${SERVER_CUSTOM}/$folder/" "${GAME_ROOT}/$folder" | tee -a ${LOGS}
    done
    rm -rf ${BUILD_ROOT}
    mkdir -p ${BUILD_ROOT}/oxide/plugins
    echo "Updating Oxide plugins" | tee -a ${LOGS}
    rsync -ra --delete "${SERVER_GLOBAL}/oxide/plugins/" "${BUILD_ROOT}/oxide/plugins" | tee -a ${LOGS}
    rsync -ra "${SERVER_GLOBAL}/oxide/custom/" "${BUILD_ROOT}/oxide/plugins" | tee -a ${LOGS}
    rsync -ra "${SERVER_CUSTOM}/oxide/plugins/" "${BUILD_ROOT}/oxide/plugins" | tee -a ${LOGS}
    rsync -ra --delete "${BUILD_ROOT}/oxide/plugins/" "${GAME_ROOT}/oxide/plugins" | tee -a ${LOGS}
    rm -rf ${BUILD_ROOT}
    echo "loading dormant plugins" | tee -a ${LOGS}
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.load *" | tee -a ${LOGS}
}

function update_maps () {
    echo "Update custom maps" | tee -a ${LOGS}
    aws s3 sync ${S3_WEB}/maps ${GAME_ROOT}/server/solidrust | tee -a ${LOGS}
}

function update_configs () {
    echo "Update Rust Server configs" | tee -a ${LOGS}
    mkdir -p ${GAME_ROOT}/server/solidrust/cfg | tee -a ${LOGS}
    rsync -a ${SERVER_CUSTOM}/server/solidrust/cfg/server.cfg ${GAME_ROOT}/server/solidrust/cfg/server.cfg | tee -a ${LOGS}
    rsync -a ${SERVER_GLOBAL}/cfg/users.cfg ${GAME_ROOT}/server/solidrust/cfg/users.cfg | tee -a ${LOGS}
    rsync -a ${SERVER_GLOBAL}/cfg/bans.cfg ${GAME_ROOT}/server/solidrust/cfg/bans.cfg | tee -a ${LOGS}
}

function update_server () {
    echo "updating ${GAME_ROOT}" | tee -a ${LOGS}
    echo "No rcon found here, downloading it..." | tee -a ${LOGS}
    wget https://github.com/gorcon/rcon-cli/releases/download/v0.9.1/rcon-0.9.1-amd64_linux.tar.gz
    tar xzvf rcon-0.9.1-amd64_linux.tar.gz
    mv rcon-0.9.1-amd64_linux/rcon ${GAME_ROOT}/rcon
    rm -rf rcon-0.9.1-amd64_linux*
    echo "===> Buffing-up Debian Distribution..." | tee -a ${LOGS}
    sudo apt update | tee -a ${LOGS}
    sudo apt -y dist-upgrade | tee -a ${LOGS}
    # TODO: output a message to reboot if kernel or initrd was updated
    echo "===> Validating installed Steam components..." | tee -a ${LOGS}
    /usr/games/steamcmd +login anonymous +force_install_dir ${GAME_ROOT}/ +app_update 258550 validate +quit | tee -a ${LOGS}
    echo "===> Updating uMod..." | tee -a ${LOGS}
    cd ${GAME_ROOT}
    wget https://umod.org/games/rust/download/develop -O \
        Oxide.Rust.zip && \
        unzip -o Oxide.Rust.zip && \
        rm Oxide.Rust.zip | tee -a ${LOGS}
    echo "===> Downloading discord binary..." | tee -a ${LOGS}
    wget https://umod.org/extensions/discord/download -O \
        ${GAME_ROOT}/RustDedicated_Data/Managed/Oxide.Ext.Discord.dll | tee -a ${LOGS}
    echo "===> Downloading RustEdit.io binary..." | tee -a ${LOGS}
    wget https://github.com/k1lly0u/Oxide.Ext.RustEdit/raw/master/Oxide.Ext.RustEdit.dll -O \
        ${GAME_ROOT}/RustDedicated_Data/Managed/Oxide.Ext.RustEdit.dll | tee -a ${LOGS}
    echo "===> Downloading Rust:IO binary..." | tee -a ${LOGS}
    wget http://playrust.io/latest -O \
        ${GAME_ROOT}/RustDedicated_Data/Managed/Oxide.Ext.RustIO.dll | tee -a ${LOGS}
}

function update_umod () {
    echo "Download fresh plugins from uMod" | tee -a ${LOGS}
    cd ${GAME_ROOT}/oxide/plugins
    plugins=$(ls -1 *.cs)
    for plugin in ${plugins[@]}; do
        echo "Attempting to replace $plugin from umod" | tee -a ${LOGS}
        wget "https://umod.org/plugins/$plugin" -O $plugin | tee -a ${LOGS}
        sleep 3 | tee -a ${LOGS}
    done
}

function update_ip () {
    export IP=$(curl -s http://whatismyip.akamai.com/)
    echo ${IP} > ${GAME_ROOT}/app.publicip
    sed -i "/app.publicip/d" ${GAME_ROOT}/server/solidrust/cfg/server.cfg
    echo "Updating public IP to: \"${IP}\" " | tee -a ${LOGS}
    echo "app.publicip \"${IP}\"" >> ${GAME_ROOT}/server/solidrust/cfg/server.cfg
}

function update_seed () {
    export SEED=$(cat ${GAME_ROOT}/server.seed)
    echo "Using current server seed: ${SEED}" | tee -a ${LOGS}
    sed -i "/server.seed/d" ${GAME_ROOT}/server/solidrust/cfg/server.cfg
    echo "server.seed \"${SEED}\"" >> ${GAME_ROOT}/server/solidrust/cfg/server.cfg
    echo "Installed \"${SEED}\" map seed to ${GAME_ROOT}/server/solidrust/cfg/server.cfg" | tee -a ${LOGS}
}

function update_permissions () {
    echo "Updating Map API data" | tee -a ${LOGS}
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.load *" | tee -a ${LOGS}
    sleep 3
    for perm in ${DEFAULT_PERMS[@]}; do
        echo "./rcon -c ${HOME}/solidrust.net/defaults/rcon.yaml \"o.grant default $perm\""
        ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.grant group default $perm" | tee -a ${LOGS}
        sleep 2
    done
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.reload PermissionGroupSync"| tee -a ${LOGS}
}

function update_map_api () {
    echo "Updating Map API data" | tee -a ${LOGS}
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "rma_regenerate" | tee -a ${LOGS}
    sleep 10
    echo "Uploading Map to Imgur" | tee -a ${LOGS}
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "rma_upload default 2000 1 1" | tee -a ${LOGS}
    sleep 10
    IMGUR_URL=$(tail -n 1000 ${GAME_ROOT}/RustDedicated.log | grep "imgur.com" | tail -n 1 | awk '{print $4}')
    echo "Successfully uploaded: ${IMGUR_URL}" | tee -a ${LOGS}
    echo "Uploading to S3"
    wget ${IMGUR_URL} -O ${GAME_ROOT}/${HOSTNAME}.jpg
    aws s3 cp ${GAME_ROOT}/${HOSTNAME}.jpg ${S3_WEB}/maps/
}

function staging_push () {
    echo "plugin is broken, and therfore currently disabled" | tee -a ${LOGS}
    #SOURCE=$1
    ## Pull global env vars
    #source ${HOME}/solidrust.net/defaults/env_vars.sh
    #me=$(basename -- "$0")
    #echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}
    #export BASE_BACKUPS_PATH="${S3_BACKUPS}/servers/${SOURCE}"
    #
    ## save Rust Server configs
    #echo "===> Uploading Rust Server Configs configs to: ${HOME}/solidrust.net/defaults/cfg/users.cfg" | tee -a ${LOGS}
    #aws s3 cp --quiet ${BASE_BACKUPS_PATH}/staged/server/users.cfg ${HOME}/solidrust.net/defaults/cfg/users.cfg | tee -a ${LOGS}
    ## bans.cfg
    #
    ## save Rust Oxide plugin configs
    #echo "===> Uploading Rust Oxide plugin configs to: ${HOME}/solidrust.net/defaults/oxide/config" | tee -a ${LOGS}
    #aws s3 sync --quiet --delete ${BASE_BACKUPS_PATH}/staged/config ${HOME}/solidrust.net/defaults/oxide/config | tee -a ${LOGS}
    #
    ## save Rust Oxide plugin data
    #echo "===> Uploading Rust Oxide plugin configs to: ${HOME}/solidrust.net/defaults/oxide/data" | tee -a ${LOGS}
    #
    #PLUGS=(
    #    EventManager \
    #    Kits \
    #    ZoneManager \
    #    BetterChat \
    #    CompoundOptions \
    #    GuardedCrate \
    #    KillStreak-Zones.json \
    #    Kits_Data \
    #    NTeleportationDisabledCommands \
    #    StackSizeController \
    #    killstreak_data \
    #    death \
    #    hit
    #)
    #
    #for plug in ${PLUGS[@]}; do
    #    aws s3 sync --quiet --delete ${BASE_BACKUPS_PATH}/staged/data/$plug ${HOME}/solidrust.net/defaults/oxide/data/$plug | tee -a ${LOGS}
    #done
    #
    ## save Rust Oxide installed plugins
    #echo "===> Uploading Rust Oxide installed plugins to: ${HOME}/solidrust.net/defaults/oxide/plugins" | tee -a ${LOGS}
    #aws s3 sync --quiet --delete ${BASE_BACKUPS_PATH}/staged/plugins ${HOME}/solidrust.net/defaults/oxide/plugins | tee -a ${LOGS}
    #
    ## nuke staging area
    #echo "===> Nuking the stagin area: ${BASE_BACKUPS_PATH}/staged" | tee -a ${LOGS}
    #aws s3 rm --quiet --recursive ${BASE_BACKUPS_PATH}/staged | tee -a ${LOGS}
}

function staging_link () {
    echo "Mapping running mods to the SRT github repo" | tee -a ${LOGS}
    chmod +x ${HOME}/solidrust.net/defaults/solidrust.sh
    rm -rf ${GAME_ROOT}/oxide
    ln -s ${SERVER_GLOBAL}/oxide ${GAME_ROOT}/oxide
}

# Data
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
advancedgather.use
)

echo "SRT Update Functions initialized" | tee -a ${LOGS}
