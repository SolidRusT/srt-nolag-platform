#!/bin/bash
## Install on:
# - Admin Console
#
## crontab example:
#      M H    D ? Y
#echo "5 *    * * *   ${USER}  ${HOME}/solidrust.net/sync_5_web_content.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh

# publish web contents
aws s3 sync --delete --acl public-read ${GITHUB_ROOT}/web ${S3_WEB} --exclude maps*

# publish custom maps
aws s3 sync --delete --acl public-read ${S3_BACKUPS}/maps ${S3_WEB}/maps
