#!/bin/bash
#echo "7 *    * * *   modded  /home/modded/solidrust.net/backup.sh" | sudo tee -a /etc/crontab
# Configuration
export MYNAME=$(hostname)
export INSTALL_DIR="/home/modded"
export S3_BUCKET="s3://solidrust.net-backups/${MYNAME}"
export REPO_HOME="${INSTALL_DIR}/solidrust.net/${MYNAME}"
export RCON_CFG="${INSTALL_DIR}/solidrust.net/rcon.yaml"

# Update the app repo
cd ${INSTALL_DIR}/solidrust.net && git pull

# Save server state
${INSTALL_DIR}/rcon -c ${RCON_CFG} "server.save"
${INSTALL_DIR}/rcon -c ${RCON_CFG} "server.writecfg"
${INSTALL_DIR}/rcon -c ${RCON_CFG} "server.backup"

# Backup to S3
aws s3 sync --quiet --delete ${INSTALL_DIR}/backup ${S3_BUCKET}/backup
aws s3 sync --quiet --delete ${INSTALL_DIR}/oxide ${S3_BUCKET}/oxide

# Update plugins
rsync -avr --delete  ${INSTALL_DIR}/solidrust.net/oxide/plugins ${INSTALL_DIR}/oxide/

# update config from github repo
rsync -avr ${REPO_HOME}/server/solidrust/cfg          ${INSTALL_DIR}/server/solidrust/
rsync -avr ${REPO_HOME}/oxide/config                  ${INSTALL_DIR}/oxide/
rsync -avr ${INSTALL_DIR}/solidrust.net/oxide/config  ${INSTALL_DIR}/oxide/
rsync -avr ${INSTALL_DIR}/solidrust.net/oxide/data    ${INSTALL_DIR}/oxide/
aws s3 sync --quiet --delete \
    ${INSTALL_DIR}/server/solidrust/cfg ${S3_BUCKET}/server/solidrust/cfg
aws s3 sync --quiet --delete \
    ${INSTALL_DIR}/oxide/config         ${S3_BUCKET}/oxide/config


# Additional RCON commands
${INSTALL_DIR}/rcon -c ${RCON_CFG} "oxide.reload ConsoleFilter"
${INSTALL_DIR}/rcon -c ${RCON_CFG} "oxide.reload ImageLibrary"
sleep 5
${INSTALL_DIR}/rcon -c ${RCON_CFG} "oxide.reload Kits"
${INSTALL_DIR}/rcon -c ${RCON_CFG} "oxide.reload ItemSkinRandomizer"
${INSTALL_DIR}/rcon -c ${RCON_CFG} "oxide.reload NightZombies"
sleep 10
#${INSTALL_DIR}/rcon -c ${RCON_CFG} "oxide.grant group default realistictorch.use"
#sleep 5

# TODO:
#(M) Economics.json
#(M) ServerRewards/*
#(M) Backpacks/*

# Push any newly created configs and data back into GitHub
rsync -r ${INSTALL_DIR}/server/solidrust/cfg    ${REPO_HOME}/server/solidrust/
rsync -r ${INSTALL_DIR}/oxide/config            ${REPO_HOME}/oxide/
rsync -r ${INSTALL_DIR}/oxide/data              ${REPO_HOME}/oxide/
git add .
git commit -m "${MYNAME} autocommit"
git push
