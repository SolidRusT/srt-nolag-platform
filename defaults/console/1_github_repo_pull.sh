#!/bin/bash
## Install on:
# - Admin Console
#
## crontab example:
#      M H    D ? Y
#echo "1 *    * * *   ${USER}  ${HOME}/solidrust.net/defaults/console/1_github_repo_pull.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh

# Delete and refresh SolidRusT repo
rm -rf ${GITHUB_ROOT}
cd ${HOME} && \
git clone git@github.com:suparious/solidrust.net.git

cd ${GITHUB_ROOT}

plugins=$(ls -1 "${GITHUB_ROOT}/oxide/plugins")

for plugin in ${plugins[@]}; do
    echo wget -N "https://umod.org/plugins/$plugin" -O  "${GITHUB_ROOT}/oxide/plugins/$plugin"
    sleep 3
done

# commit any updates
git add . && \
git commit -m "auto plugin update" && \
git push

# Push repo updates to s3
aws s3 sync --quiet --delete ${GITHUB_ROOT} ${S3_BACKUPS}/repo
