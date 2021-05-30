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
echo "===> Uploading Rust Server Configs configs to: ${HOME}/solidrust.net/defaults/cfg/users.cfg" | tee -a ${LOGS}
aws s3 cp --quiet ${BASE_BACKUPS_PATH}/staged/server/users.cfg ${HOME}/solidrust.net/defaults/cfg/users.cfg | tee -a ${LOGS}
# bans.cfg

# save Rust Oxide plugin configs
echo "===> Uploading Rust Oxide plugin configs to: ${HOME}/solidrust.net/defaults/oxide/config" | tee -a ${LOGS}
aws s3 sync --quiet --delete ${BASE_BACKUPS_PATH}/staged/config ${HOME}/solidrust.net/defaults/oxide/config | tee -a ${LOGS}

# save Rust Oxide plugin data
echo "===> Uploading Rust Oxide plugin configs to: ${HOME}/solidrust.net/defaults/oxide/data" | tee -a ${LOGS}

PLUGS=(
    EventManager \
    Kits \
    ZoneManager \
    BetterChat \
    CompoundOptions \
    GuardedCrate \
    KillStreak-Zones.json \
    Kits_Data \
    NTeleportationDisabledCommands \
    StackSizeController \
    killstreak_data \
    death \
    hit
)

for plug in ${PLUGS[@]}; do
    aws s3 sync --quiet --delete ${BASE_BACKUPS_PATH}/staged/data/$plug ${HOME}/solidrust.net/defaults/oxide/data/$plug | tee -a ${LOGS}
done

# save Rust Oxide installed plugins
${BASE_BACKUPS_PATH}/staged/plugin

echo "===> Uploading Rust Oxide installed plugins to: ${BASE_BACKUPS_PATH}/staged/plugins" | tee -a ${LOGS}
aws s3 sync --quiet --delete ${GAME_ROOT}/oxide/plugins ${BASE_BACKUPS_PATH}/staged/plugins | tee -a ${LOGS}

echo "Finished ${me}"   | tee -a ${LOGS}
