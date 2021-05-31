#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "*/15 *    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/web/15_web_backups.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

aws s3 sync --quiet --delete /etc/apache2 ${S3_BACKUPS}/servers/${HOSTNAME}/apache2 | tee -a ${LOGS}
aws s3 sync --quiet --delete /etc/php ${S3_BACKUPS}/servers/${HOSTNAME}/php | tee -a ${LOGS}
aws s3 sync --quiet --delete /etc/mysql ${S3_BACKUPS}/servers/${HOSTNAME}/mysql | tee -a ${LOGS}
aws s3 sync --quiet --delete /var/log/apache2 ${S3_BACKUPS}/servers/${HOSTNAME}/apache2-logs
aws s3 cp /etc/crontab ${S3_BACKUPS}/servers/${HOSTNAME}/crontab
aws s3 cp / ${S3_BACKUPS}/servers/${HOSTNAME}/crontab
aws s3 sync --quiet --delete /var/www/html/solidrust ${S3_BACKUPS}/servers/${HOSTNAME}/web

echo "Finished ${me}"   | tee -a ${LOGS}