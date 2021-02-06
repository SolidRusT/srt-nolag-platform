#!/bin/bash
## Configuration
# example crontab
#echo "7 *    * * *   modded  /home/modded/solidrust.net/backup.sh" | sudo tee -a /etc/crontab
# Collect user input
export COMMAND="$1"
# Say my name
export MYNAME="eleven"
# root of where the game server is installed
export GAME_ROOT="/c/Users/shaun/Downloads/servers/1/serverfiles"
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

# Save server state
## TODO: check if the server is running, instead of this
# if no arguments are passed, assume we are running from crontab
if [ -z ${COMMAND} ]; then
    ${GAME_ROOT}/rcon -c ${RCON_CFG} "server.save"
    ${GAME_ROOT}/rcon -c ${RCON_CFG} "server.writecfg"
    ${GAME_ROOT}/rcon -c ${RCON_CFG} "server.writecfg"
fi

# Backup to S3
aws s3 sync --quiet --delete \
${GAME_ROOT}/oxide                ${S3_BUCKET}/oxide
aws s3 sync --quiet --delete \
${GAME_ROOT}/server/solidrust/cfg ${S3_BUCKET}/server/solidrust/cfg

# Update plugins
rsync -ar --delete  ${GLOBAL_CONFIG}/oxide/plugins ${GAME_ROOT}/oxide/

# update global config from github repo
rsync -ar --delete ${GLOBAL_CONFIG}/oxide/config  ${GAME_ROOT}/oxide/
rsync -ar ${GLOBAL_CONFIG}/RustDedicated_Data/Managed            ${GAME_ROOT}/RustDedicated_Data/

aws s3 sync --quiet ${GLOBAL_CONFIG}/oxide/data             ${S3_BUCKET}/oxide/data

# update customized config for this server
rsync -ar ${GITHUB_ROOT}/oxide/config    ${GAME_ROOT}/oxide/

# update customized data for this server
rsync -ar ${GITHUB_ROOT}/oxide/data      ${GAME_ROOT}/oxide/

# update server details
rsync -ar ${GITHUB_ROOT}/server/solidrust/cfg   ${GAME_ROOT}/server/solidrust/
