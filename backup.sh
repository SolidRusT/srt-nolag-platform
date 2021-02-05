#!/bin/bash
#echo "7 *    * * *   modded  /home/modded/solidrust.net/backup.sh" | sudo tee -a /etc/crontab
# push to s3
export MYNAME=$(hostname)
export S3_BUCKET="s3://solidrust.net-backups/${MYNAME}"
export INSTALL_DIR="/home/modded"

${INSTALL_DIR}/rcon -c ${INSTALL_DIR}/solidrust.net/rcon.yaml "server.save"
${INSTALL_DIR}/rcon -c ${INSTALL_DIR}/solidrust.net/rcon.yaml "server.writecfg"
${INSTALL_DIR}/rcon -c ${INSTALL_DIR}/solidrust.net/rcon.yaml "server.backup"

aws s3 sync --quiet --delete ${INSTALL_DIR}/backup ${S3_BUCKET}/backup
aws s3 sync --quiet --delete ${INSTALL_DIR}/oxide/data ${S3_BUCKET}/oxide/data

cd ${INSTALL_DIR}/solidrust.net && git pull

# regular sync
rsync -r ${INSTALL_DIR}/solidrust.net/${MYNAME}/server/solidrust/cfg/ ${INSTALL_DIR}/server/solidrust/cfg
aws s3 sync --quiet --delete ${INSTALL_DIR}/server/solidrust/cfg ${S3_BUCKET}/server/solidrust/cfg
rsync -r ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/config/ ${INSTALL_DIR}/oxide/config
aws s3 sync --quiet --delete ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/config ${S3_BUCKET}/oxide/config
rsync -r ${INSTALL_DIR}/solidrust.net/oxide/plugins/ ${INSTALL_DIR}/oxide/plugins

${INSTALL_DIR}/rcon -c ${INSTALL_DIR}/solidrust.net/rcon.yaml "oxide.grant group default furnacesplitter.use"

#(M) Economics.json
#(M) ServerRewards/*
#(M) Backpacks/*
