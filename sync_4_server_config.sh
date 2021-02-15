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

# Update Rust server config
mkdir -p ${GAME_ROOT}/server/solidrust/cfg
rsync -a ${SERVER_CUSTOM}/server/solidrust/cfg/server.cfg ${GAME_ROOT}/server/solidrust/cfg/server.cfg

# Update custom maps
aws s3 sync s3://solidrust.net/maps ${GAME_DIR}/server/solidrust

