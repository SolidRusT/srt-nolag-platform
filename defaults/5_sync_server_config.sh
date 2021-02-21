#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "5 *    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/5_sync_server_config.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

# Update Rust server config
mkdir -p ${GAME_ROOT}/server/solidrust/cfg | tee -a ${LOGS}
rsync -a ${SERVER_CUSTOM}/server/solidrust/cfg/server.cfg ${GAME_ROOT}/server/solidrust/cfg/server.cfg | tee -a ${LOGS}
rsync -a ${SERVER_GLOBAL}/cfg/users.cfg ${GAME_ROOT}/server/solidrust/cfg/users.cfg | tee -a ${LOGS}
rsync -a ${SERVER_GLOBAL}/cfg/bans.cfg ${GAME_ROOT}/server/solidrust/cfg/bans.cfg | tee -a ${LOGS}

# Update custom maps
aws s3 sync ${S3_WEB}/maps ${GAME_ROOT}/server/solidrust | tee -a ${LOGS}
