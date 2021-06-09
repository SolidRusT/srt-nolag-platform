#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "4 *    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/40_sync_oxide_mods.sh" | sudo tee -a /etc/crontab

# Load global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
# Load SRT functions
source ${HOME}/solidrust.net/defaults/funct_update.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

update_repo
update_mods
update_map_api

echo "Finished ${me}"   | tee -a ${LOGS}
