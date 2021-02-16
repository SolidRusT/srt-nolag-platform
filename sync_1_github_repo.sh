#!/bin/bash
## Install on:
# - Admin Console
#
## crontab example:
#      M H    D ? Y
#echo "1 *    * * *   ${USER}  ${HOME}/solidrust.net/sync_1_github_repo.sh" | sudo tee -a /etc/crontab

# Delete and refresh SolidRusT repo
cd ${HOME}
rm -rf solidrust.net
git clone git@github.com:suparious/solidrust.net.git

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh

# Push repo updates to s3
aws s3 sync --quiet --delete ${GITHUB_ROOT} ${S3_BACKUPS}/repo
