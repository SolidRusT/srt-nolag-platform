#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "9 *    * * *   ${USER}  ${HOME}/solidrust.net/backup_1_oxide.sh" | sudo tee -a /etc/crontab

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

aws s3 sync --quiet --delete s3://solidrust.net-backups/repo ${GITHUB_ROOT}
