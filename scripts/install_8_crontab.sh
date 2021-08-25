source ${HOME}/solidrust.net/defaults/env_vars.sh
echo "3 *    * * *   ${USER} \
    rm -rf ${GITHUB_ROOT}; \
    mkdir -p ${GITHUB_ROOT}; \
    aws s3 sync --only-show-errors --delete ${S3_BACKUPS}/repo ${GITHUB_ROOT}; \
    chmod +x ${SERVER_GLOBAL}/*.sh" \
    | sudo tee -a /etc/crontab