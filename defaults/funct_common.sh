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
    # enter game root
    cd ${GAME_ROOT}

    if [ ${CUSTOM_MAP} = "enabled" ]; then
        echo "Custom Maps enabled: ${CUSTOM_MAP_URL}" | tee -a ${LOGS}
        sleep 2
        ./RustDedicated -batchmode -nographics -silent-crashes -logfile 2>&1 ${SERVER_LOGS} \
        +server.ip 0.0.0.0 \
        +server.port 28015 \
        +rcon.ip 0.0.0.0 \
        +rcon.port 28016 \
        +server.tickrate 30 \
        +app.publicip ${SERVER_IP} \
        +app.port 28082 \
        +rcon.web 1 \
        +rcon.password "NOFAGS" \
        +server.identity "solidrust" \
        +server.levelurl ${CUSTOM_MAP_URL} &
    else
        echo "Using ${WORLD_SIZE} Procedural map with seed: ${SEED} " | tee -a ${LOGS}
        sleep 2
        ./RustDedicated -batchmode -nographics -silent-crashes -logfile 2>&1 ${SERVER_LOGS} \
        +server.ip 0.0.0.0 \
        +server.port 28015 \
        +rcon.ip 0.0.0.0 \
        +rcon.port 28016 \
        +server.tickrate 30 \
        +app.publicip ${SERVER_IP} \
        +app.port 28082 \
        +rcon.web 1 \
        +rcon.password "NOFAGS" \
        +server.identity "solidrust" \
        +server.level "Procedural Map" \
        +server.seed ${SEED} \
        +server.worldsize ${WORLD_SIZE} &
    fi
    # Launch game server
    echo "===> Touching my peepee..." | tee -a ${LOGS}
    sleep 3
    tail -n 10 ${SERVER_LOGS}
    echo "Delaying for about 8mins while service loads" | tee -a ${LOGS}
    sleep 120
    tail -n 10 ${SERVER_LOGS}
    echo "Delaying for 6mins while service loads"  | tee -a ${LOGS}
    sleep 120
    tail -n 10 ${SERVER_LOGS}
    echo "Delaying for 4mins while service loads"  | tee -a ${LOGS}
    sleep 60
    tail -n 10 ${SERVER_LOGS}
    echo "Delaying for 3mins while service loads"  | tee -a ${LOGS}
    sleep 60
    tail -n 10 ${SERVER_LOGS}
    echo "Delaying for 2mins while service loads"  | tee -a ${LOGS}
    sleep 60
    tail -n 10 ${SERVER_LOGS}
    echo "Delaying for 1mins while service loads"  | tee -a ${LOGS}
    sleep 60
    tail -n 10 ${SERVER_LOGS}
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


echo "SRT Common Functions initialized" | tee -a ${LOGS}
