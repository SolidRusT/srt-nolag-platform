#!/bin/bash
#echo "7 *    * * *   modded  /home/modded/solidrust.net/backup.sh" | sudo tee -a /etc/crontab
# Configuration
export MYNAME=$(hostname) # get my name
export INSTALL_DIR="/home/modded"  # root of where the game server is installed
export S3_BUCKET="s3://solidrust.net-backups/${MYNAME}"  # s3 destination for backups
export REPO_HOME="${INSTALL_DIR}/solidrust.net/${MYNAME}" # github source for configs
export RCON_CFG="${INSTALL_DIR}/solidrust.net/rcon.yaml" # local RCON CLI

# Update the app repo
cd ${INSTALL_DIR}/solidrust.net && git pull

# Make sure github paths exists (useful for new servers)
mkdir -p ${REPO_HOME}/server/solidrust/cfg
mkdir -p ${REPO_HOME}/solidrust.net/oxide/config
mkdir -p ${REPO_HOME}/solidrust.net/oxide/data
mkdir -p ${INSTALL_DIR}/backup
mkdir -p ${INSTALL_DIR}/oxide
mkdir -p ${INSTALL_DIR}/server/solidrust/cfg

# Save server state
${INSTALL_DIR}/rcon -c ${RCON_CFG} "server.save"
${INSTALL_DIR}/rcon -c ${RCON_CFG} "server.writecfg"
${INSTALL_DIR}/rcon -c ${RCON_CFG} "server.backup"

# Backup to S3
aws s3 sync --quiet --delete \
    ${INSTALL_DIR}/backup               ${S3_BUCKET}/backup
aws s3 sync --quiet --delete \
    ${INSTALL_DIR}/oxide                ${S3_BUCKET}/oxide
aws s3 sync --quiet --delete \
    ${INSTALL_DIR}/server/solidrust/cfg ${S3_BUCKET}/server/solidrust/cfg

# Update plugins
rsync -avr --delete  ${INSTALL_DIR}/solidrust.net/oxide/plugins ${INSTALL_DIR}/oxide/

# update config from github repo
rsync -avr ${REPO_HOME}/server/solidrust/cfg          ${INSTALL_DIR}/server/solidrust/
rsync -avr ${REPO_HOME}/oxide/config                  ${INSTALL_DIR}/oxide/
rsync -avr ${INSTALL_DIR}/solidrust.net/oxide/config  ${INSTALL_DIR}/oxide/
rsync -avr ${INSTALL_DIR}/solidrust.net/oxide/data    ${INSTALL_DIR}/oxide/

# push a copy to aws s3 just in case



# Additional RCON commands
${INSTALL_DIR}/rcon -c ${RCON_CFG} "o.load *"
sleep 15
#${INSTALL_DIR}/rcon -c ${RCON_CFG} "oxide.reload FastLoot"
sleep 10
#${INSTALL_DIR}/rcon -c ${RCON_CFG} "oxide.grant group default fastloot.use"
#sleep 5
${INSTALL_DIR}/rcon -c ${RCON_CFG} "oxide.grant group default boxsorterlite.use"
${INSTALL_DIR}/rcon -c ${RCON_CFG} "oxide.grant group default raidalarm.use"
${INSTALL_DIR}/rcon -c ${RCON_CFG} "oxide.grant group default clearrepair.use"
${INSTALL_DIR}/rcon -c ${RCON_CFG} "oxide.grant group default mushroomeffects.use"
#sleep 5




# TODO:
#(M) Economics.json
#(M) ServerRewards/*
#(M) Backpacks/*

# Push any newly created configs and data back into GitHub
rsync -r ${INSTALL_DIR}/server/solidrust/cfg    ${REPO_HOME}/server/solidrust/
rsync -r ${INSTALL_DIR}/oxide/config            ${REPO_HOME}/oxide/
rsync -r ${INSTALL_DIR}/oxide/data              ${REPO_HOME}/oxide/
cd ${REPO_HOME} && git add .
git commit -m "${MYNAME} autocommit"
git push
