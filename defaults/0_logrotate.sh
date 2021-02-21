#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "0 *    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/0_logrotate.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

/usr/sbin/logrotate -f ${SERVER_GLOBAL}/logrotate.conf --state ${HOME}/logrotate-state | tee -a ${LOGS}