#!/bin/bash
## Install on:
# - Admin Console
#

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

cd /mnt/c/Users/Shaun/repos/solidrust.net/defaults/oxide/plugins

plugins=$(ls -1 *.cs)

for plugin in ${plugins[@]}; do
    echo "Attempting to replace $plugin from umod" | tee -a ${LOGS}
    wget -N "https://umod.org/plugins/$plugin" | tee -a ${LOGS}
    sleep 3 | tee -a ${LOGS}
done

echo "Finished ${me}"   | tee -a ${LOGS}