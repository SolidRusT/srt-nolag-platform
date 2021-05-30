#!/bin/bash
## Install on:
# - Game Server
#

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}
export BASE_BACKUPS_PATH="${S3_BACKUPS}/servers/${HOSTNAME}"

cd ${GAME_ROOT}/oxide/plugins/

plugins=$(ls -1 *.cs)

for plugin in ${plugins[@]}; do
    echo "Checking $plugin on umod against ${GAME_ROOT}/oxide/plugins/$plugin" | tee -a ${LOGS}
    wget -N "https://umod.org/plugins/$plugin" | tee -a ${LOGS}
    sleep 3 | tee -a ${LOGS}
done

echo "reloading failed plugins"
${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.load *" | tee -a ${LOGS}
${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.reload EventManager" | tee -a ${LOGS}
${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.load *" | tee -a ${LOGS}

# save Rust Oxide installed plugins
echo "===> Uploading Rust Oxide installed plugins to: ${BASE_BACKUPS_PATH}/staged/plugins" | tee -a ${LOGS}
aws s3 sync --quiet --delete ${GAME_ROOT}/oxide/plugins ${BASE_BACKUPS_PATH}/staged/plugins | tee -a ${LOGS}

echo "Finished ${me}"   | tee -a ${LOGS}
