function notification () {
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
}

function wipe_map () {
    echo "Wipe out old Procedural maps and related data" | tee -a ${LOGS}
    rm -rf ${GAME_ROOT}/server/solidrust/proceduralmap.*
    echo "Wipe out custom maps and related data" | tee -a ${LOGS}
    rm -rf ${GAME_ROOT}/server/solidrust/*.map
}

function change_seed () {
    export SEED=$(shuf -i 1-2147483648 -n 1)
    echo "New Map Seed generated: ${SEED}" | tee -a ${LOGS}
    cp ${GAME_ROOT}/server.seed ${GAME_ROOT}/server.seed.old
    echo ${SEED} > ${GAME_ROOT}/server.seed
    echo "Installed new map seed to ${GAME_ROOT}/server.seed" | tee -a ${LOGS}
}

echo "SRT Wipe Functions initialized" | tee -a ${LOGS}
