#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "5 *    * * *   ${USER}  ${HOME}/solidrust.net/sync_3_oxide_custom.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh

OXIDE=(
    oxide/data
    oxide/config
    oxide/plugins
)

for folder in ${OXIDE[@]}; do
    echo "sync ${SERVER_CUSTOM}/$folder/ to ${GAME_ROOT}/$folder"
    mkdir -p "${GAME_ROOT}/$folder"
    rsync -r "${SERVER_CUSTOM}/$folder/" "${GAME_ROOT}/$folder"
done

${GAME_ROOT}/rcon -c ${RCON_CFG} "o.load *"
