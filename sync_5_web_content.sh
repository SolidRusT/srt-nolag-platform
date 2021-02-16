#!/bin/bash
## Install on:
# - Admin Console
#
## crontab example:
#      M H    D ? Y
#echo "5 *    * * *   ${USER}  ${HOME}/solidrust.net/sync_5_web_content.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh

aws s3 sync --delete --acl public-read ${GITHUB_ROOT}/web/ s3://solidrust.net --exclude maps*

#aws s3 cp --acl public-read ${GAME_ROOT}/server/solidrust/Stellarium4.map s3://solidrust.net/maps/Stellarium4.map