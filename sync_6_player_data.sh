### BROKEN WIP
## DO NOT USE



#!/bin/bash
## crontab example:
#        M H    D ? Y
#echo "*/3 *    * * *   ${USER}  ${HOME}/solidrust.net/permissions_sync.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh

# TODO: Figure out inventory sync
#(M) Backpacks/*


# Update the app repo
cd ${GAME_ROOT}/solidrust.net && git pull
sudo chown -R ${USER}:${USER} ${GAME_ROOT}/oxide/data
sudo chown -R ${USER}:${USER} ${GAME_ROOT}/oxide/config
sudo chown -R ${USER}:${USER} ${GAME_ROOT}/server/solidrust/cfg

# update global config from github repo
rsync -ar ${GLOBAL_CONFIG}/oxide/config  ${GAME_ROOT}/oxide/
#rsync -ar ${GLOBAL_CONFIG}/RustDedicated_Data/Managed            ${GAME_ROOT}/RustDedicated_Data/

# update customized config for this server
rsync -ar ${GITHUB_ROOT}/oxide/config    ${GAME_ROOT}/oxide/

# push common data (configuration) for data sync process
aws s3 sync --quiet ${GLOBAL_CONFIG}/oxide/data             ${S3_BUCKET}/oxide/data

# apply customized data for this server
rsync -ar ${GITHUB_ROOT}/oxide/data      ${GAME_ROOT}/oxide/

# update customized server details
rsync -ar ${GITHUB_ROOT}/server/solidrust/cfg   ${GAME_ROOT}/server/solidrust/

# Update global plugins
rsync -ar --delete  ${GLOBAL_CONFIG}/oxide/plugins ${GAME_ROOT}/oxide/

# Update global group permissions
## TODO: make this a separate cron
${GAME_ROOT}/rcon -c ${RCON_CFG} "o.load *"
echo "Snoozing..."
sleep 10

echo "Starting s3 push-pull..."
# TODO: Figure out global economics
#(M) Economics.json
#(M) ServerRewards/*

PLAYER_DATA=(
    DeathNotes \
    Backpacks \
    banks \
    EventManager \
    Zonemanager
)

# Sync Push-Pull
echo "Sync player data folders..."
for data in ${PLAYER_DATA[@]}; do
    aws s3 sync --quiet  \
    ${S3_BUCKET}/oxide/data/$data ${GAME_ROOT}/oxide/data/$data
    aws s3 sync --quiet \
    ${GAME_ROOT}/oxide/data/$data  ${S3_BUCKET}/oxide/data/$data
done


PLAYER_JSON=(
    BetterChat.json \
    CompoundOptions.json \
    death.png \
    GuardedCrate.json \
    hit.png \
    killstreak_data.json \
    KillStreaks-Zones.json \
    Kits.json \
    NTeleportationDisabledCommands.json \
    StackSizeController.json
)

# Sync Push-Pull
echo "Sync player data files..."
for data in ${PLAYER_JSON[@]}; do
    aws s3 cp --quiet\
    ${S3_BUCKET}/oxide/data/$data ${GAME_ROOT}/oxide/data/$data
    aws s3 cp --quiet\
    ${GAME_ROOT}/oxide/data/$data  ${S3_BUCKET}/oxide/data/$data
done

${GAME_ROOT}/rcon -c ${RCON_CFG} "o.reload PermissionGroupSync"

