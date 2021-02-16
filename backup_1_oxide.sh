#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "9 *    * * *   ${USER}  ${HOME}/solidrust.net/backup_1_oxide.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh

OXIDE=(
    oxide/data
    oxide/config
    oxide/data
)

for folder in ${OXIDE[@]}; do
    echo "sync ${GAME_ROOT}/$folder/ to ${S3_BACKUPS}/${HOSTNAME}/$folder"
    aws s3 sync --quiet --delete ${GAME_ROOT}/$folder ${S3_BACKUPS}/${HOSTNAME}/$folder
    sleep 1
done
