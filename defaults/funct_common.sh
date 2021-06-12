function initialize_srt () {
    source ${HOME}/solidrust.net/defaults/env_vars.sh
    source ${HOME}/solidrust.net/servers/${HOSTNAME}/env_vars.sh
    me=$(basename -- "$0")
    echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}
}

function backup_s3 () {
    if [ -f "${GAME_ROOT}/rcon" ]; then
        echo "rcon binary found, saving world..." # no need to log this
        ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.writecfg"
        sleep 1
        ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.save"
        sleep 5
        ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.backup"
        sleep 10
    else
        echo "No rcon binary found here, unable to save world data" | tee -a ${LOGS}
    fi
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
}

function start_rust () {
    echo "Start RustDedicated game service" | tee -a ${LOGS}
    chmod +x ${HOME}/solidrust.net/defaults/solidrust.sh
    /bin/sh -c ${HOME}/solidrust.net/defaults/solidrust.sh &
    echo "Delaying for about 8mins while service loads" | tee -a ${LOGS}
    sleep 500
    echo "Should be ready for action" | tee -a ${LOGS}
}

function stop_rust () {
    echo "Stop RustDedicated game service" | tee -a ${LOGS}
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "restart 30"
}

function stop_rust_now () {
    echo "Stop RustDedicated game service" | tee -a ${LOGS}
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "restart 1"
}

function show_logs () {
    tail -n 20 -F "${HOME}/SolidRusT.log" "${GAME_ROOT}/RustDedicated.log" "${GAME_ROOT}/rcon-default.log"
}

echo "SRT Common Functions initialized"
