#!/bin/bash
## Install on:
# - Game Server
#

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}
export BASE_BACKUPS_PATH="${S3_BACKUPS}/servers/${HOSTNAME}"

# save Rust Server configs
echo "===> Uploading Rust Server Configs configs to: ${BASE_BACKUPS_PATH}/staged/server" | tee -a ${LOGS}
aws s3 sync --quiet --delete ${GAME_ROOT}/server/solidrust/cfg ${BASE_BACKUPS_PATH}/staged/server | tee -a ${LOGS}

# save Rust Oxide plugin configs
echo "===> Uploading Rust Oxide plugin configs to: ${BASE_BACKUPS_PATH}/staged/configs" | tee -a ${LOGS}
aws s3 sync --quiet --delete ${GAME_ROOT}/oxide/config ${BASE_BACKUPS_PATH}/staged/config | tee -a ${LOGS}

# save Rust Oxide plugin data
echo "===> Uploading Rust Oxide plugin configs to: ${BASE_BACKUPS_PATH}/staged/data" | tee -a ${LOGS}
aws s3 sync --quiet --delete ${GAME_ROOT}/oxide/data ${BASE_BACKUPS_PATH}/staged/data | tee -a ${LOGS}

# save Rust Oxide installed plugins
echo "===> Uploading Rust Oxide installed plugins to: ${BASE_BACKUPS_PATH}/staged/plugins" | tee -a ${LOGS}
aws s3 sync --quiet --delete ${GAME_ROOT}/oxide/plugins ${BASE_BACKUPS_PATH}/staged/plugins | tee -a ${LOGS}

echo "Finished ${me}"   | tee -a ${LOGS}
