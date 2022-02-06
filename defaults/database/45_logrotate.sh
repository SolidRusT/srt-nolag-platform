#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "0 *    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/database/45_logrotate.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

/usr/sbin/logrotate -f ${SERVER_GLOBAL}/database/logrotate.conf --state ${HOME}/logrotate-state | tee -a ${LOGS}

echo "Finished ${me}"   | tee -a ${LOGS}