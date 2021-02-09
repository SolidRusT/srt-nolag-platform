#!/bin/bash

rsync -ar --exclude 'Bundles' /game/${USER}  /home/

${HOME}/solidrust.net/backup.sh

${USER}/solidrust.net/permissions_sync.sh

rsync -ar --exclude 'Bundles' ${HOME}  /game/

${USER}/solidrust.net/permissions_sync.sh
