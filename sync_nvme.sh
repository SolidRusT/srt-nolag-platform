#!/bin/bash

rsync -ar --exclude={'Bundles','solidrust.net'} /game/${USER}  /home/

${HOME}/solidrust.net/backup.sh

${USER}/solidrust.net/permissions_sync.sh

rsync -ar --exclude 'Bundles' ${HOME}  /game/

${USER}/solidrust.net/permissions_sync.sh
