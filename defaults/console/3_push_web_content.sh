#!/bin/bash
## Install on:
# - Admin Console
#
## crontab example:
#      M H    D ? Y
#echo "3 *    * * *   ${USER}  ${HOME}/solidrust.net/defaults/console/3_push_web_content.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}


# publish web contents
aws s3 sync --delete --acl public-read ${GITHUB_ROOT}/web ${S3_WEB} --exclude maps* | tee -a ${LOGS}

# publish custom maps
aws s3 sync --delete --acl public-read ${S3_BACKUPS}/maps ${S3_WEB}/maps | tee -a ${LOGS}
