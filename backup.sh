#!/bin/bash
#echo "7 *    * * *   modded  /home/modded/solidrust.net/backup.sh" | sudo tee -a /etc/crontab
# push to s3
export MYNAME=$(hostname)
export DEST_S3="s3://solidrust.net-backups/${MYNAME}"
export INSTALL_DIR="/home/modded"

# config
/home/modded/rcon -c /home/modded/solidrust.net/rcon.yaml "server.save"
/home/modded/rcon -c /home/modded/solidrust.net/rcon.yaml "server.writecfg"
aws s3 sync --quiet --delete /home/modded/server/rust/cfg ${DEST_S3}/server/rust/cfg
# data
/home/modded/rcon -c /home/modded/solidrust.net/rcon.yaml "server.backup"
aws s3 sync --quiet --delete /home/modded/backup ${DEST_S3}/backup
# plugins
aws s3 sync --quiet --delete /home/modded/oxide ${DEST_S3}/oxide

#(M) Economics.json
#(M) ServerRewards/*
#(M) Backpacks/*