#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "0 0    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/99_wipe_server.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

echo "Notifying players with 1 hour warning" | tee -a ${LOGS}
${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "restart 3600 \"Scheduled map wipe is about to begin.\""
sleep 3590
echo "Backing-up server to local disk" | tee -a ${LOGS}
${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.writecfg"
sleep 1
${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.save"
sleep 5
${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.backup"
sleep 4

echo "Uploading backup to s3" | tee -a ${LOGS}
CONTENTS=(
    oxide
    server
    backup
)

for folder in ${CONTENTS[@]}; do
    echo "sync ${GAME_ROOT}/$folder to ${S3_BACKUPS}/servers/${HOSTNAME}/$folder" | tee -a ${LOGS}
    aws s3 sync --quiet --delete ${GAME_ROOT}/$folder ${S3_BACKUPS}/servers/${HOSTNAME}/$folder | tee -a ${LOGS}
    sleep 1
done

echo "Wipe out old maps and map data" | tee -a ${LOGS}
rm -rf ${GAME_ROOT}/server/solidrust/proceduralmap.*
echo "Generate and install new map seed" | tee -a ${LOGS}
export SEED=$(shuf -i 1-2147483648 -n 1)
echo "New Map Seed generated: ${SEED}" | tee -a ${LOGS}
sed -i "/server.seed/d" ${GAME_ROOT}/server/solidrust/cfg/server.cfg
echo "server.seed \"${SEED}\"" >> ${GAME_ROOT}/server/solidrust/cfg/server.cfg

echo "Update game service and integrations" | tee -a ${LOGS}
/bin/sh -c ${HOME}/solidrust.net/defaults/update_rust_service.sh

echo "Start RustDedicated game service" | tee -a ${LOGS}
/bin/sh -c ${HOME}/solidrust.net/defaults/solidrust.sh &

echo "Finished ${me}"   | tee -a ${LOGS}
exit 0