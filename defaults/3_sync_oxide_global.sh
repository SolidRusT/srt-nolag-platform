#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "3 *    * * *   ${USER}  ${HOME}/solidrust.net/defaults/3_sync_oxide_global.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

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
mkdir -p "${BUILD_ROOT}/oxide/plugins"

rsync -ra "${SERVER_GLOBAL}/oxide/plugins/" "${BUILD_ROOT}/oxide/plugins" | tee -a ${LOGS}
rsync -ra "${SERVER_CUSTOM}/oxide/plugins/" "${BUILD_ROOT}/oxide/plugins" | tee -a ${LOGS}
rsync -ra --delete "${BUILD_ROOT}/oxide/plugins/" "${GAME_ROOT}/oxide/plugins" | tee -a ${LOGS}

# Check for RCON
if [ -f "${GAME_ROOT}/rcon" ]; then
    echo "rcon binary found" # no need to log this
else
    echo "No rcon found here, downloading it..." | tee -a ${LOGS}
    wget https://github.com/gorcon/rcon-cli/releases/download/v0.9.0/rcon-0.9.0-amd64_linux.tar.gz
    tar xzvf rcon-0.9.0-amd64_linux.tar.gz
    mv rcon-0.9.0-amd64_linux/rcon ${GAME_ROOT}/rcon
    rm -rf rcon-0.9.0-amd64_linux*
fi

${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.load *"
