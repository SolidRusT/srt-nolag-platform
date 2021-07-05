function notification () {
    echo "Notifying players with 1 hour warning" | tee -a ${LOGS}
    #3600
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "say \"Scheduled map wipe is about to begin, in 1 hour.\""
    sleep 900
    #2700
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "say \"Scheduled map wipe is about to begin, in 45 minutes.\""
    sleep 900
    #1800
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "say \"Scheduled map wipe is about to begin, in 30 minutes.\""
    sleep 900
    #900
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "restart 900 \"Scheduled map wipe is about to begin.\""
    sleep 874
    backup_s3
}

function wipe_map () {
    echo "Wipe out old Procedural maps and related data" | tee -a ${LOGS}
    rm -rf ${GAME_ROOT}/server/solidrust/proceduralmap.*
    echo "Wipe out custom maps and related data" | tee -a ${LOGS}
    rm -rf ${GAME_ROOT}/server/solidrust/*.map
}

function wipe_kits () {
    echo "Wipe out all saved kits" | tee -a ${LOGS}
    echo "oxide/data/Kits/kits_data.json"
}

function wipe_bank () {
    echo "Wipe out old Procedural maps and related data" | tee -a ${LOGS}
    echo "oxide/data/banks/*.json"
}

function wipe_backpacks () {
    echo "Wipe out old Procedural maps and related data" | tee -a ${LOGS}
    echo "oxide/data/Backpacks/*.json"
}

function wipe_leaderboards () {
    echo "Wipe out old Procedural maps and related data" | tee -a ${LOGS}
}

function wipe_permissions () {
    echo "Wipe out old Procedural maps and related data" | tee -a ${LOGS}
    MOD_GROUPS=$(${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.show groups" | grep -v "Groups:" | sed -z 's/, /\n/g' | grep -v default)
    for group in ${MOD_GROUPS[@]}; do 
        echo " - Removing: $group" | tee -a ${LOGS}
        ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.group remove $group" | tee -a ${LOGS}
    done
    echo "=> Reload permissions sync" | tee -a ${LOGS}
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.reload PermissionGroupSync"| tee -a ${LOGS}
}

function change_seed () {
    export SEED=$(shuf -i 1-2147483648 -n 1)
    echo "New Map Seed generated: ${SEED}" | tee -a ${LOGS}
    cp ${GAME_ROOT}/server.seed ${GAME_ROOT}/server.seed.old
    echo ${SEED} > ${GAME_ROOT}/server.seed
    echo "Installed new map seed to ${GAME_ROOT}/server.seed" | tee -a ${LOGS}
}

echo "SRT Wipe Functions initialized" | tee -a ${LOGS}
