#!/bin/bash
## crontab example:
#        M H    D ? Y
#echo "*/3 *    * * *   ${USER}  ${HOME}/solidrust.net/permissions_sync.sh" | sudo tee -a /etc/crontab

## Configuration
GAME_DIR="/game"
# ${HOSTNAME}
# ${USER}
# ${HOME}

# Delete and refresh SolidRusT repo
cd ${HOME}
rm -rf solidrust.net
git clone git@github.com:suparious/solidrust.net.git
