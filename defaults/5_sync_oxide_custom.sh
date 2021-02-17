#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "5 *    * * *   ${USER}  ${HOME}/solidrust.net/defaults/5_sync_oxide_custom.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

OXIDE=(
    oxide/data
    oxide/config
    oxide/plugins
)

for folder in ${OXIDE[@]}; do
    echo "sync ${SERVER_CUSTOM}/$folder/ to ${GAME_ROOT}/$folder" | tee -a ${LOGS}
    mkdir -p "${GAME_ROOT}/$folder" | tee -a ${LOGS}
    rsync -r "${SERVER_CUSTOM}/$folder/" "${GAME_ROOT}/$folder" | tee -a ${LOGS}
done 

if [ -f "${GAME_ROOT}/rcon" ]; then
    echo "rcon binary found" # no need to log this
else
    echo "No rcon found here, downloading it..." | tee -a ${LOGS}
    wget https://github.com/gorcon/rcon-cli/releases/download/v0.9.0/rcon-0.9.0-amd64_linux.tar.gz
    tar xzvf rcon-0.9.0-amd64_linux.tar.gz
    mv rcon-0.9.0-amd64_linux/rcon ${GAME_ROOT}/rcon
    rm -rf rcon-0.9.0-amd64_linux*
fi

${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.load *"
