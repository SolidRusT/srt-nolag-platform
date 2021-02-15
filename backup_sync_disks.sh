#!/bin/bash


sudo chmod ugo+rwx -R /game
rsync -ar --exclude={'Bundles','solidrust.net'} /game/${USER}  /home/

sudo chown -R ${USER}:${USER} /home/${USER}

${HOME}/solidrust.net/backup.sh

${HOME}/solidrust.net/permissions_sync.sh

rsync -ar --exclude={'Bundles','backup','*.log'} ${HOME}  /game/

${HOME}/solidrust.net/permissions_sync.sh
