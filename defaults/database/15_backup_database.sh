#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "15 *    * * *   ${USER}  ${HOME}/solidrust.net/defaults/database/15_backup_database.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=`basename "$0"`
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

echo "Do some database shit here." | tee -a ${LOGS}
