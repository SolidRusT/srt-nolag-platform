#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "4 *    * * *   ${USER}  ${HOME}/solidrust.net/defaults/4_sync_server_config.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh

# Update Rust server config
mkdir -p ${GAME_ROOT}/server/solidrust/cfg | tee -a ${LOGS}
rsync -a ${SERVER_CUSTOM}/server/solidrust/cfg/server.cfg ${GAME_ROOT}/server/solidrust/cfg/server.cfg | tee -a ${LOGS}
rsync -a ${SERVER_GLOBAL}/cfg/server.cfg ${GAME_ROOT}/server/solidrust/cfg/users.cfg | tee -a ${LOGS}

# Update custom maps
aws s3 sync ${S3_WEB}/maps ${GAME_ROOT}/server/solidrust | tee -a ${LOGS}
