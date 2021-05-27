#!/bin/bash
echo "restoring to: \"${GAME_DIR}\"."
export NEW_NAME="eleven"

sed -i "/nine/d" /etc/hosts /etc/cloud/templates/hosts.debian.tmpl

echo ${NEW_NAME} | tee /etc/hostname
echo "127.0.0.1    ${NEW_NAME}" | tee -a /etc/hosts /etc/cloud/templates/hosts.debian.tmpl
echo "127.0.0.1    ${NEW_NAME}" | tee -a /etc/cloud/templates/hosts.debian.tmpl
hostnamectl set-hostname ${NEW_NAME}

sudo dpkg-reconfigure tzdata
#America/Los_Angeles or America/New_York

reboot

export PULL_FROM="nine"
export S3_BACKUPS="s3://solidrust.net-backups"

mkdir -p ${GAME_DIR}
mkfs -t xfs /dev/nvme0n1
mount /dev/nvme0n1 ${GAME_DIR}

/usr/games/steamcmd +login anonymous +force_install_dir ${GAME_DIR} +app_update 258550 +quit

aws s3 sync --quiet --delete ${S3_BACKUPS}/repo $HOME/solidrust.net
chmod +x $HOME/solidrust.net/defaults/*.sh
/bin/sh -c ${HOME}/solidrust.net/defaults/update_rust_service.sh

aws s3 sync --quiet --delete s3://${S3_BACKUPS}/servers/${PULL_FROM}/oxide ${GAME_DIR}/oxide
aws s3 sync --quiet --delete s3://${S3_BACKUPS}/servers/${PULL_FROM}/server ${GAME_DIR}/server
mkdir -p ${GAME_DIR}/backup

nano ${GAME_DIR}/server/solidrust/cfg/server.cfg

export SEED=$(shuf -i 1-2147483648 -n 1)
export WAN_IP=$(wget http://ipecho.net/plain -O - -q ; echo)

app.publicip
server.hostname
server.description
server.seed

/bin/sh -c ${HOME}/solidrust.net/defaults/solidrust.sh &

enable crontab