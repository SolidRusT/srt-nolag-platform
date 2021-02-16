#!/bin/bash
## crontab example:
#      M H    D ? Y
#echo "1 *    * * *   ${USER}  ${HOME}/solidrust.net/sync_1_github_repo.sh" | sudo tee -a /etc/crontab

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

# Delete and refresh SolidRusT repo
cd ${HOME}
rm -rf solidrust.net
git clone git@github.com:suparious/solidrust.net.git

# push repo updates to s3
aws s3 sync --quiet --delete ${GITHUB_ROOT} s3://solidrust.net-backups/repo