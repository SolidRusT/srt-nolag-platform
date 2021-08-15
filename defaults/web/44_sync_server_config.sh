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
mkdir -p ${HOME}/solidrust.net/web ${HOME}/solidrust.net/defaults
aws s3 sync --size-only --delete ${S3_BACKUPS}/repo/web ${HOME}/solidrust.net/web --exclude 'web/maps/*' | tee -a ${LOGS}
aws s3 sync --size-only --delete ${S3_BACKUPS}/repo/defaults ${HOME}/solidrust.net/defaults | tee -a ${LOGS}
aws s3 cp ${S3_BACKUPS}/repo/build.txt ${HOME}/solidrust.net/web/
cat ${HOME}/solidrust.net/web/build.txt | head -n 2
chmod +x ${HOME}/solidrust.net/defaults/*.sh ${HOME}/solidrust.net/defaults/web/*.sh

# Update custom maps
echo "Downloading custom maps from s3" | tee -a ${LOGS}
aws s3 sync --size-only --delete ${S3_WEB}/maps ${HOME}/solidrust.net/web/maps | tee -a ${LOGS}

# Update SRT Radio
echo "" | tee -a ${LOGS}
aws s3 sync --size-only --delete ${S3_RADIO} /var/www/radio | tee -a ${LOGS}

echo "Finished ${me}"   | tee -a ${LOGS}
