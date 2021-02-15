#!/bin/bash
GAME_DIR="/game"
# ${HOSTNAME}
# ${USER}
# ${HOME}

cd ${HOME}
rm -rf solidrust.net
git clone git@github.com:suparious/solidrust.net.git


# do the oxide/config and oxide/data customized for this ${HOSTNAME}
#mkdir -p ${GAME_DIR}/server/solidrust/cfg
#cp solidrust.net/servers/${HOSTNAME}/server/solidrust/cfg/server.cfg ${GAME_DIR}/server/solidrust/cfg/server.cfg

# Game Node: Bootstrap
sudo su - ${STEAMUSER}
export MYNAME=$(hostname)
export DEST_S3="s3://solidrust.net-backups/${MYNAME}"
export INSTALL_DIR=${HOME}
cd ${INSTALL_DIR}/solidrust.net && git pull
mkdir -p ${INSTALL_DIR}/oxide/plugins
mkdir -p ${INSTALL_DIR}/oxide/data
mkdir -p ${INSTALL_DIR}/oxide/config

mkdir -p ${INSTALL_DIR}/solidrust.net/${MYNAME}/server/solidrust/cfg
rsync -r ${INSTALL_DIR}/server/solidrust/cfg ${INSTALL_DIR}/solidrust.net/${MYNAME}/server/solidrust/cfg
mkdir -p ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/config
rsync -r ${INSTALL_DIR}/oxide/config/ ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/config
mkdir -p ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/data

rsync -r ${INSTALL_DIR}/oxide/data/ ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/data
rsync -r ${INSTALL_DIR}/server/solidrust/cfg/ ${INSTALL_DIR}/solidrust.net/${MYNAME}/server/solidrust/cfg
rsync -r ${INSTALL_DIR}/oxide/config/ ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/config


# TODO: Figure out inventory sync
#(M) Backpacks/*