#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "5 *    * * *   ${USER}  ${HOME}/solidrust.net/defaults/5_sync_oxide_custom.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh | tee -a ${LOGS}

OXIDE=(
    oxide/data
    oxide/config
    oxide/plugins
)

for folder in ${OXIDE[@]}; do
    echo "sync ${SERVER_CUSTOM}/$folder/ to ${GAME_ROOT}/$folder" | tee -a ${LOGS}
    mkdir -p "${GAME_ROOT}/$folder" | tee -a ${LOGS}
    rsync -r "${SERVER_CUSTOM}/$folder/" "${GAME_ROOT}/$folder" | tee -a ${LOGS}
done 

${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.load *"
