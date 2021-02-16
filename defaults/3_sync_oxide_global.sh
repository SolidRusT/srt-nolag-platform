#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "3 *    * * *   ${USER}  ${HOME}/solidrust.net/defaults/3_sync_oxide_global.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh | tee -a ${LOGS}

OXIDE=(
    oxide/data
    oxide/config
)

for folder in ${OXIDE[@]}; do
    echo "sync ${SERVER_GLOBAL}/$folder/ to ${GAME_ROOT}/$folder" | tee -a ${LOGS}
    mkdir -p "${GAME_ROOT}/$folder" | tee -a ${LOGS}
    rsync -r "${SERVER_GLOBAL}/$folder/" "${GAME_ROOT}/$folder" | tee -a ${LOGS}
done

mkdir -p "${GAME_ROOT}/oxide/plugins" | tee -a ${LOGS}
rsync -ra --delete "${SERVER_GLOBAL}/oxide/plugins/" "${GAME_ROOT}/oxide/plugins" | tee -a ${LOGS}

${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.load *"
