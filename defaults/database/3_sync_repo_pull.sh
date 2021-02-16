#!/bin/bash
## Install on:
# - Game server
#
## crontab example:
#      M H    D ? Y
#echo "3 *    * * *   ${USER}  ${HOME}/solidrust.net/defaults/database/3_sync_repo_pull.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh | tee -a ${LOGS}

# pull repo updates from s3
mkdir -p ${GITHUB_ROOT}
aws s3 sync --quiet --delete ${S3_BACKUPS}/repo ${GITHUB_ROOT} | tee -a ${LOGS}