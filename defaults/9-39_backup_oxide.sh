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
    echo "rcon binary found" # no need to log this
else
    echo "No rcon found here, downloading it..." | tee -a ${LOGS}
    wget https://github.com/gorcon/rcon-cli/releases/download/v0.9.0/rcon-0.9.0-amd64_linux.tar.gz
    tar xzvf rcon-0.9.0-amd64_linux.tar.gz
    mv rcon-0.9.0-amd64_linux/rcon ${GAME_ROOT}/rcon
    rm -rf rcon-0.9.0-amd64_linux*
fi

${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.writecfg"
sleep 1
${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.save"
sleep 5
${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.backup"
sleep 10

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
