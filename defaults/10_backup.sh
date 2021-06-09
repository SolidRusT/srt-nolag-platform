#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "9,39 *    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/9-39_backup_oxide.sh" | sudo tee -a /etc/crontab

# Load global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
# Load SRT functions
source ${HOME}/solidrust.net/defaults/funct_common.sh
source ${HOME}/solidrust.net/defaults/funct_update.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

update_repo
backup_s3

echo "Finished ${me}"   | tee -a ${LOGS}
