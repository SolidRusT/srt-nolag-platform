#!/bin/bash
## Configuration
# example crontab
#echo "* *    * * *   modded  /home/modded/solidrust.net/permissions_sync.sh" | sudo tee -a /etc/crontab
# Say my name
export MYNAME=$(hostname)
# root of where the game server is installed
export GAME_ROOT=${HOME}
# Amazon s3 destination for backups
export S3_BUCKET="s3://solidrust.net-backups/defaults"
# Github source for configs
export GITHUB_ROOT="${GAME_ROOT}/solidrust.net/servers/${MYNAME}"
# Default configs
export GLOBAL_CONFIG="${GAME_ROOT}/solidrust.net/defaults"
# local RCON CLI config
export RCON_CFG="${GAME_ROOT}/solidrust.net/servers/rcon.yaml"

# Update the app repo
cd ${GAME_ROOT}/solidrust.net && git pull
sudo chown -R ${$USER}:${$USER} ${GAME_ROOT}/oxide/data
sudo chown -R ${$USER}:${$USER} ${GAME_ROOT}/oxide/config
sudo chown -R ${$USER}:${$USER} ${GAME_ROOT}/server/solidrust/cfg

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