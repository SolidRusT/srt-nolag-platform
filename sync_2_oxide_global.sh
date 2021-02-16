#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "3 *    * * *   ${USER}  ${HOME}/solidrust.net/sync_2_oxide_global.sh" | sudo tee -a /etc/crontab

## Configuration
# root of where the game server is installed
export GAME_ROOT="/game"
# Amazon s3 destination for backups
export S3_BUCKET="s3://solidrust.net-backups/defaults"
# Github source for configs
export GITHUB_ROOT="${HOME}/solidrust.net"
# Default configs
export SERVER_GLOBAL="${GITHUB_ROOT}/defaults"
# Customized config
export SERVER_CUSTOM="${GITHUB_ROOT}/servers/${HOSTNAME}"
# local RCON CLI config
export RCON_CFG="${GITHUB_ROOT}/servers/rcon.yaml"

# pull repo updates from s3
aws s3 sync --quiet --delete s3://solidrust.net-backups/repo ${GITHUB_ROOT}

OXIDE=(
    oxide/data
    oxide/config
)

for folder in ${OXIDE[@]}; do
    echo "sync ${SERVER_GLOBAL}/$folder/ to ${GAME_ROOT}/$folder"
    mkdir -p "${GAME_ROOT}/$folder"
    rsync -r "${SERVER_GLOBAL}/$folder/" "${GAME_ROOT}/$folder"
done

mkdir -p "${GAME_ROOT}/oxide/plugins"
rsync -ra --delete "${SERVER_GLOBAL}/oxide/plugins/" "${GAME_ROOT}/oxide/plugins"

${GAME_ROOT}/rcon -c ${RCON_CFG} "o.load *"
