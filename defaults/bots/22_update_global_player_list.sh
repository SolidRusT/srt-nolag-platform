#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "*/10 *    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/web/44_sync_server_config.sh" | sudo tee -a /etc/crontab

# Load SRT functions
source ${HOME}/solidrust.net/defaults/funct_common.sh
source ${HOME}/solidrust.net/defaults/funct_stats.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

initialize_srt
global_online

echo "Finished ${me}"   | tee -a ${LOGS}
