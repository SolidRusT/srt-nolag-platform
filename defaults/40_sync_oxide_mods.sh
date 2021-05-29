#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "4 *    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/4_sync_oxide_mods.sh" | sudo tee -a /etc/crontab

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

${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.load *"

echo "Finished ${me}"   | tee -a ${LOGS}