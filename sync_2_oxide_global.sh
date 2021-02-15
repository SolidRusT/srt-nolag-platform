#!/bin/bash
## crontab example:
#        M H    D ? Y
#echo "*/3 *    * * *   ${USER}  ${HOME}/solidrust.net/permissions_sync.sh" | sudo tee -a /etc/crontab

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
rsync -r --delete "${SERVER_GLOBAL}/oxide/plugins/" "${GAME_ROOT}/oxide/plugins"

# TODO: Figure out inventory sync
#(M) Backpacks/*