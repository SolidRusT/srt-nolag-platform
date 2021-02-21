#!/bin/bash
## Install on:
# - Game server
#
## crontab example:
#      M H    D ? Y
#echo "3 *    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/3_sync_repo_pull.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}


echo "nothing todo"   | tee -a ${LOGS}

## MOVED TO:
#source ${HOME}/solidrust.net/defaults/env_vars.sh
#echo "3 *    * * *   ${USER} \
#    rm -rf ${GITHUB_ROOT} | tee -a ${LOGS}; \
#    mkdir -p ${GITHUB_ROOT}; | tee -a ${LOGS}; \
#    aws s3 sync --only-show-errors --delete ${S3_BACKUPS}/repo ${GITHUB_ROOT} | tee -a ${LOGS}; \
#    chmod +x ${SERVER_GLOBAL}/*.sh | tee -a ${LOGS}" \
#    | sudo tee -a /etc/crontab




echo "Finished ${me}"   | tee -a ${LOGS}