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

aws s3 sync --delete --acl public-read ${GITHUB_ROOT}/web/ s3://solidrust.net --exclude maps*

#aws s3 cp --acl public-read ${GAME_ROOT}/server/solidrust/Stellarium4.map s3://solidrust.net/maps/Stellarium4.map