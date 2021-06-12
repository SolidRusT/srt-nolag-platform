#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "9,39 *    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/9-39_backup_oxide.sh" | sudo tee -a /etc/crontab

# Load SRT functions
source ${HOME}/solidrust.net/defaults/funct_common.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

initialize_srt
backup_s3

echo "Finished ${me}"   | tee -a ${LOGS}
