#!/bin/bash
## Install on:
# - Admin Console
#
## crontab example:
#      M H    D ? Y
#echo "15 5    * * *   ${USER}  ${HOME}/solidrust.net/defaults/console/15_update_plugins.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=`basename "$0"`
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

# lock the repo from being updated while we complete this job
echo "=====> locking repo <=====" | tee -a ${LOGS}
touch ${SOLID_LCK}

# Delete and refresh SolidRusT repo
rm -rf ${GITHUB_ROOT}
cd ${HOME} && \
git clone git@github.com:suparious/solidrust.net.git | tee -a ${LOGS}

export PLUGIN_COUNT=$(ls -1 "${SERVER_GLOBAL}/oxide/plugins | wc -l")
echo "=====> Updating ${PLUGIN_COUNT} plugins! <=====" | tee -a ${LOGS}

plugins=$(ls -1 "${SERVER_GLOBAL}/oxide/plugins")

for plugin in ${plugins[@]}; do
    echo "==> updating $plugin <==" | tee -a ${LOGS}
    echo wget -N "https://umod.org/plugins/$plugin" -O  "${SERVER_GLOBAL}/oxide/plugins/$plugin" | tee -a ${LOGS}
    sleep 3
done

# commit any updates
cd ${GITHUB_ROOT}
git add . && \
git commit -m "auto plugin update" && \
git push  | tee -a ${LOGS}

# unlock the repo
echo "=====> unlocking repo <=====" | tee -a ${LOGS}
rm ${SOLID_LCK}
