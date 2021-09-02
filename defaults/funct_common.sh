function initialize_srt() {
    source ${HOME}/solidrust.net/defaults/env_vars.sh
    source ${HOME}/solidrust.net/servers/${HOSTNAME}/env_vars.sh
    alias rcon="${GAME_ROOT}/rcon -c ${HOME}/solidrust.net/defaults/rcon.yaml"
    me=$(basename -- "$0")
    echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}
    source ${HOME}/solidrust.net/defaults/funct_common.sh
    source ${HOME}/solidrust.net/defaults/funct_wipe.sh
    source ${HOME}/solidrust.net/defaults/funct_update.sh
    source ${HOME}/solidrust.net/defaults/funct_items.sh
}

function save_players() {
    if [ -f "${GAME_ROOT}/rcon" ]; then
        echo "Saving game world..." | tee -a ${LOGS}
        ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.save"
    else
        echo "No rcon binary found here, unable to save world data" | tee -a ${LOGS}
    fi    
}

function backup_s3() {
    if [ -f "${GAME_ROOT}/rcon" ]; then
        sleep 5
        echo "Backing-up game world" | tee -a ${LOGS}
        ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.writecfg"
        sleep 1
        ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.backup"
        sleep 10
    else
        echo "No rcon binary found here, unable to save world data" | tee -a ${LOGS}
    fi
    # if modded server, then:
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

function save_ebs() {
    echo "save current game folder to instance EBS" | tee -a ${LOGS}
    mkdir -p ${HOME}/nvme_root
    rsync -ra --delete "${GAME_ROOT}/" "${HOME}/nvme_root" | tee -a ${LOGS}
}

function restore_ebs() {
    echo "save current game folder to instance EBS" | tee -a ${LOGS}
    rsync -ra --delete "${HOME}/nvme_root/" "${GAME_ROOT}"
}

function start_rust() {
    echo "Start RustDedicated game service" | tee -a ${LOGS}
    # enter game root
    cd ${GAME_ROOT}

    if [ ${CUSTOM_MAP} = "enabled" ]; then
        echo "Custom Maps enabled: ${CUSTOM_MAP_URL}" | tee -a ${LOGS}
        sleep 2
        ./RustDedicated -batchmode -nographics -silent-crashes -logfile ${SERVER_LOGS} \
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
            +server.levelurl ${CUSTOM_MAP_URL} \
            +server.logoimage "https://solidrust.net/images/SoldRust_Logo.png" 2>&1 \
            ;

    else

        echo "Using ${WORLD_SIZE} Procedural map with seed: ${SEED} " | tee -a ${LOGS}
        sleep 2
        ./RustDedicated -batchmode -nographics -silent-crashes -logfile ${SERVER_LOGS} \
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
            +server.worldsize ${WORLD_SIZE} \
            +server.logoimage "https://solidrust.net/images/SoldRust_Logo.png" &
    fi

    # Launch game server
    echo "===> Touching my peepee..." | tee -a ${LOGS}
    sleep 3
    export MY_PID=$(pidof RustDedicated)
    echo "Boosting affinity for RustDedicated PID: ${MY_PID}" | tee -a ${LOGS}
    renice -10 ${MY_PID} | tee -a ${LOGS}
    tail -n 10 ${SERVER_LOGS}
    echo "Delaying for about 8mins while service loads" | tee -a ${LOGS}
    sleep 120
    tail -n 10 ${SERVER_LOGS}
    echo "Delaying for 6mins while service loads" | tee -a ${LOGS}
    sleep 120
    tail -n 10 ${SERVER_LOGS}
    echo "Delaying for 4mins while service loads" | tee -a ${LOGS}
    sleep 60
    tail -n 10 ${SERVER_LOGS}
    echo "Delaying for 3mins while service loads" | tee -a ${LOGS}
    sleep 60
    tail -n 10 ${SERVER_LOGS}
    echo "Delaying for 2mins while service loads" | tee -a ${LOGS}
    sleep 60
    tail -n 10 ${SERVER_LOGS}
    echo "Delaying for 1mins while service loads" | tee -a ${LOGS}
    sleep 60
    tail -n 10 ${SERVER_LOGS}
    echo "Should be ready for action" | tee -a ${LOGS}
}

function stop_rust() {
    echo "Stop RustDedicated game service" | tee -a ${LOGS}
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "restart 30"
}

function stop_rust_now() {
    echo "Stop RustDedicated game service" | tee -a ${LOGS}
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "restart 1"
}

function show_logs() {
    #find . -type f -exec grep -l samplestring {} \;
    tail -n 20 -F "${HOME}/SolidRusT.log" "${GAME_ROOT}/RustDedicated.log" "${GAME_ROOT}/rcon-default.log"
}

function hot_plugs() {
    export REPORTS="${GAME_ROOT}/oxide/data/PerformanceMonitor/Reports"
    export LATEST_REPORT=$(ls -1tr ${REPORTS}/*/* | tail -n 1)
}

function send_discord() {
    MESSAGE="$1"

    curl -X POST \
        -F "content=${MESSAGE}" \
        -F "username=\"${HOSTNAME}-global\"" \
        "${WEBHOOK}"
}

function show_players() {
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "players"
}

function create_chat_log() {
    echo "parsing the serverlog for player chat" | tee -a ${LOGS}
    tail -n +1 -f ${GAME_ROOT}/RustDedicated.log | while read line; do echo "$line" | grep "CHAT" | tee -a ${GAME_ROOT}/chat-global.out ; done
}

function chat_to_discord() {
    echo "sending player chat to discord" | tee -a ${LOGS}
    fielpos="/game/chat-global.out"
    declare -i infinite=1
    while [ "$infinite" -eq 1 ]; do
        declare -i lineno=0
        while read -r line; do
            send_discord "${line}"
            let ++lineno
            sed -i "1 d" "$fielpos"
            sleep 2
        done <"$fielpos"
        sleep 3
    done 
}

echo "SRT Common Functions initialized" | tee -a ${LOGS}

#fuckyou