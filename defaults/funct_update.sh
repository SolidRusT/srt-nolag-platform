function update_repo () {
    echo "Downloading repo from s3" | tee -a ${LOGS}
    aws s3 sync --only-show-errors --delete ${S3_BACKUPS}/repo ${HOME}/solidrust.net | tee -a ${LOGS}
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

    # Plugin merge + sync
    rm -rf ${BUILD_ROOT}
    mkdir -p ${BUILD_ROOT}/oxide/plugins
    echo "Updating Oxide plugins" | tee -a ${LOGS}
    rsync -ra --delete "${SERVER_GLOBAL}/oxide/plugins/" "${BUILD_ROOT}/oxide/plugins" | tee -a ${LOGS}
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
    echo "Download fresh plugins from uMod"
    cd ${GAME_DIR}/oxide/plugins

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

echo "SRT Update Functions initialized"
