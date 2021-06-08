#!/bin/bash
## Install on:
# - Game Server
#

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}


# Update custom maps
echo "===> Downloading custom maps..." | tee -a ${LOGS}
aws s3 sync ${S3_WEB}/maps ${GAME_ROOT}/server/solidrust | tee -a ${LOGS}

aws s3 sync --quiet ${S3_BACKUPS}/servers/${HOSTNAME}/server ${GAME_ROOT}/server
aws s3 sync --quiet ${S3_BACKUPS}/servers/${HOSTNAME}/backup ${GAME_ROOT}/backup
aws s3 sync --quiet ${S3_BACKUPS}/servers/${HOSTNAME}/oxide ${GAME_ROOT}/oxide

echo "Finished ${me}"   | tee -a ${LOGS}