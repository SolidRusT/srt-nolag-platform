#!/bin/bash
#echo "7 *    * * *   modded  /home/modded/solidrust.net/backup.sh" | sudo tee -a /etc/crontab
# push to s3
export MYNAME=$(hostname)
export DEST_S3="s3://solidrust.net-backups/${MYNAME}"
export INSTALL_DIR="/home/modded"

# config
${INSTALL_DIR}/rcon -c ${INSTALL_DIR}/solidrust.net/rcon.yaml "server.save"
${INSTALL_DIR}/rcon -c ${INSTALL_DIR}/solidrust.net/rcon.yaml "server.writecfg"
aws s3 sync --quiet --delete ${INSTALL_DIR}/server/rust/cfg ${DEST_S3}/server/rust/cfg
# data
${INSTALL_DIR}/rcon -c ${INSTALL_DIR}/solidrust.net/rcon.yaml "server.backup"
aws s3 sync --quiet --delete ${INSTALL_DIR}/backup ${DEST_S3}/backup
# plugins
aws s3 sync --quiet --delete ${INSTALL_DIR}/oxide ${DEST_S3}/oxide

#(M) Economics.json
#(M) ServerRewards/*
#(M) Backpacks/*