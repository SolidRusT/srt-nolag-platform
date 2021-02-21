#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "5 *    * * *   ${USER}  ${HOME}/solidrust.net/defaults/5_sync_oxide_custom.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

echo "Nothing to do" | tee -a ${LOGS}
