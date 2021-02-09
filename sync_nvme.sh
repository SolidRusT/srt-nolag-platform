#!/bin/bash

sudo chown -R ${USER}:${USER} /game/${USER}/oxide/data
sudo chown -R ${USER}:${USER} /game/${USER}/oxide/config
sudo chown -R ${USER}:${USER} /game/${USER}/server/solidrust/cfg

rsync -ar --exclude={'Bundles','solidrust.net'} /game/${USER}  /home/

${HOME}/solidrust.net/backup.sh

${HOME}/solidrust.net/permissions_sync.sh

rsync -ar --exclude={'Bundles','backup','*.log'} ${HOME}  /game/

${HOME}/solidrust.net/permissions_sync.sh
