#!/bin/bash
GAME_DIR="/game"
# ${HOSTNAME}
# ${USER}
# ${HOME}

cd ${HOME}
rm -rf solidrust.net
git clone git@github.com:suparious/solidrust.net.git

mkdir -p ${GAME_DIR}/server/solidrust/cfg
cp solidrust.net/servers/${HOSTNAME}/server/solidrust/cfg/server.cfg ${GAME_DIR}/server/solidrust/cfg/server.cfg
