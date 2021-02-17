#!/bin/bash
## Install on:
# - Admin Console
#
## crontab example:
#      M H    D ? Y
#echo "45 *    * * *   ${USER}  ${HOME}/solidrust.net/defaults/console/45_sync_logs.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

SERVERS=(nine)

for server in ${SERVERS[@]}; do
    mkdir -p $HOME/logs/$server
    scp $server.solidrust.net:~/*.log* $HOME/logs/$server/
    scp $server.solidrust.net:/game/*.log* $HOME/logs/$server/
done