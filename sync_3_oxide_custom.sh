#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "5 *    * * *   ${USER}  ${HOME}/solidrust.net/sync_3_oxide_custom.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh

# pull repo updates from s3
aws s3 sync --quiet --delete s3://solidrust.net-backups/repo ${GITHUB_ROOT}

OXIDE=(
    oxide/data
    oxide/config
)

for folder in ${OXIDE[@]}; do
    echo "sync ${SERVER_CUSTOM}/$folder/ to ${GAME_ROOT}/$folder"
    rsync -r "${SERVER_CUSTOM}/$folder/" "${GAME_ROOT}/$folder"
done

# custom plugins are not yet available
#rsync -rv --delete "${SERVER_CUSTOM}/oxide/plugins/" "${GAME_ROOT}/oxide/plugins"
#
#${GAME_ROOT}/rcon -c ${RCON_CFG} "o.load *"
