#!/bin/bash
## Configuration
# example crontab
#echo "7 *    * * *   modded  /home/modded/solidrust.net/backup.sh" | sudo tee -a /etc/crontab
# Collect user input
export COMMAND="$1"
# Say my name
export MYNAME=$(hostname)
# root of where the game server is installed
export GAME_ROOT="/home/modded"
# Amazon s3 destination for backups
export S3_BUCKET="s3://solidrust.net-backups/${MYNAME}"
# Github source for configs
export GITHUB_ROOT="${GAME_ROOT}/solidrust.net/servers/${MYNAME}"
# Default configs
export GLOBAL_CONFIG="${GAME_ROOT}/solidrust.net/defaults"
# local RCON CLI config
export RCON_CFG="${GAME_ROOT}/solidrust.net/servers/rcon.yaml"

# Update the app repo
cd ${GAME_ROOT}/solidrust.net && git pull

# Make sure path stubs exists (useful for new servers)
mkdir -p ${GAME_ROOT}/backup
mkdir -p ${GAME_ROOT}/oxide
mkdir -p ${GAME_ROOT}/server/solidrust/cfg

# Save server state
## TODO: check if the server is running, instead of this
# if no arguments are passed, assume we are running from crontab
if [ -z ${COMMAND} ]; then
    ${GAME_ROOT}/rcon -c ${RCON_CFG} "server.save"
    ${GAME_ROOT}/rcon -c ${RCON_CFG} "server.writecfg"
    ${GAME_ROOT}/rcon -c ${RCON_CFG} "server.backup"
fi

# Backup to S3
aws s3 sync --quiet --delete \
${GAME_ROOT}/backup               ${S3_BUCKET}/backup
aws s3 sync --quiet --delete \
${GAME_ROOT}/oxide                ${S3_BUCKET}/oxide
aws s3 sync --quiet --delete \
${GAME_ROOT}/server/solidrust/cfg ${S3_BUCKET}/server/solidrust/cfg

# Update plugins
rsync -avr --delete  ${GLOBAL_CONFIG}/oxide/plugins ${GAME_ROOT}/oxide/

# update global config from github repo
rsync -avr --delete ${GLOBAL_CONFIG}/oxide/config  ${GAME_ROOT}/oxide/
rsync -avr ${GLOBAL_CONFIG}/RustDedicated_Data/Managed            ${GAME_ROOT}/RustDedicated_Data/

aws s3 sync --quiet ${GLOBAL_CONFIG}/oxide/data             ${S3_BUCKET}/oxide/data

# update customized config for this server
rsync -avr ${GITHUB_ROOT}/oxide/config    ${GAME_ROOT}/oxide/

# update customized data for this server
rsync -avr ${GITHUB_ROOT}/oxide/data      ${GAME_ROOT}/oxide/

# update server details
rsync -avr ${GITHUB_ROOT}/server/solidrust/cfg   ${GAME_ROOT}/server/solidrust/
