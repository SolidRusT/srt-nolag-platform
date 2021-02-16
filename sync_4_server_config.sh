#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "4 *    * * *   ${USER}  ${HOME}/solidrust.net/sync_4_server_config.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh

# Update Rust server config
mkdir -p ${GAME_ROOT}/server/solidrust/cfg
rsync -a ${SERVER_CUSTOM}/server/solidrust/cfg/server.cfg ${GAME_ROOT}/server/solidrust/cfg/server.cfg
rsync -a ${SERVER_GLOBAL}/cfg/server.cfg ${GAME_ROOT}/server/solidrust/cfg/users.cfg

# Update custom maps
aws s3 sync s3://solidrust.net/maps ${GAME_DIR}/server/solidrust
