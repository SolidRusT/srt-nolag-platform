#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "9,39 *    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/9-39_backup_oxide.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

# Check for RCON
if [ -f "${GAME_ROOT}/rcon" ]; then
    echo "rcon binary found, saving world..." # no need to log this
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.writecfg"
    sleep 1
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.save"
    sleep 5
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.backup"
    sleep 10
else
    echo "No rcon binary found here, unable to save world data" | tee -a ${LOGS}
fi

CONTENTS=(
    oxide
    server
    backup
)

for folder in ${CONTENTS[@]}; do
    echo "sync ${GAME_ROOT}/$folder to ${S3_BACKUPS}/servers/${HOSTNAME}/$folder" | tee -a ${LOGS}
    aws s3 sync --quiet --delete ${GAME_ROOT}/$folder ${S3_BACKUPS}/servers/${HOSTNAME}/$folder | tee -a ${LOGS}
    sleep 1
done

echo "Finished ${me}"   | tee -a ${LOGS}
exit 0