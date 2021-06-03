#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "*/10 *    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/web/44_sync_server_config.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

# update repo
echo "Downloading repo from s3" | tee -a ${LOGS}
aws s3 sync --only-show-errors --delete ${S3_BACKUPS}/repo ${HOME}/solidrust.net | tee -a ${LOGS}
chmod +x ${HOME}/solidrust.net/defaults/*.sh
chmod +x ${HOME}/solidrust.net/defaults/web/*.sh

# Update custom maps
echo "Downloading custom maps from s3" | tee -a ${LOGS}
aws s3 sync ${S3_WEB}/maps ${HOME}/solidrust.net/web/maps | tee -a ${LOGS}

echo "Finished ${me}"   | tee -a ${LOGS}