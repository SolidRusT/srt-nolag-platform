#!/bin/bash
export PULL_FROM="nine"
export S3_BACKUPS="s3://solidrust.net-backups"

mount /dev/nvme0n1 ${GAME_DIR}

/usr/games/steamcmd +login anonymous +force_install_dir ${GAME_DIR} +app_update 258550 +quit

aws s3 sync --quiet --delete ${S3_BACKUPS}/repo $HOME/solidrust.net
chmod +x $HOME/solidrust.net/defaults/*.sh
/bin/sh -c ${HOME}/solidrust.net/defaults/update_rust_service.sh

aws s3 sync --quiet --delete ${S3_BACKUPS}/servers/${PULL_FROM}/oxide ${GAME_DIR}/oxide
aws s3 sync --quiet --delete ${S3_BACKUPS}/servers/${PULL_FROM}/server ${GAME_DIR}/server
mkdir -p ${GAME_DIR}/backup

nano ${GAME_DIR}/server/solidrust/cfg/server.cfg

app.publicip
export WAN_IP=$(wget http://ipecho.net/plain -O - -q ; echo)

server.hostname
server.description

server.seed
export SEED=$(shuf -i 1-2147483648 -n 1)

/bin/sh -c ${HOME}/solidrust.net/defaults/solidrust.sh &

enable crontab