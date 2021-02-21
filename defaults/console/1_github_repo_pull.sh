#!/bin/bash
## Install on:
# - Admin Console
#
## crontab example:
#      M H    D ? Y
#echo "1,30 *    * * *   ${USER}  ${HOME}/solidrust.net/defaults/console/1_github_repo_pull.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

if [ -f "${SOLID_LCK}" ]; then
    echo "Repo is locked, aborting..." | tee -a ${LOGS}
else
    # Delete and refresh SolidRusT repo
    echo "Refreshing from GitHub" | tee -a ${LOGS}
    rm -rf ${GITHUB_ROOT}
    cd ${HOME} && \
    git clone git@github.com:suparious/solidrust.net.git | tee -a ${LOGS}

    # Push repo updates to s3
    echo "Pushing repo to s3" | tee -a ${LOGS}
    aws s3 sync --quiet --delete ${GITHUB_ROOT} ${S3_BACKUPS}/repo  | tee -a ${LOGS}
fi
