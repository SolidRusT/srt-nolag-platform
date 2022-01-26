#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "*/15 *    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/web/15_web_backups.sh" | sudo tee -a /etc/crontab

# Load SRT functions
source ${HOME}/solidrust.net/defaults/funct_common.sh
source ${HOME}/solidrust.net/defaults/funct_update.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

initialize_srt
backup_bots

echo "Finished ${me}"   | tee -a ${LOGS}