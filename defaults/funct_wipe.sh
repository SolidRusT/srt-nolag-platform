# ${GAME_ROOT}/server/solidrust/
#    player.blueprints.3.db            – This is the database which stores all players blueprints.
#    player.deaths.3.db                – This is supposed to store player deaths but is unused.
#    proceduralmap.<size>.<seed>.map   – Generated Map file for your selected size / seed.
#    proceduralmap.<size>.<seed>.sav   – Entities / Structures save file.
#    sv.files.0.db                     – All images / paintings stored on signs.

function notification_restart() {
  declare -i delay=$1
  if [ $delay -eq 0 ]; then
    declare -i delay=3600
    echo "using default notification delay of ${delay} seconds" | tee -a ${LOGS}
  else
    echo "notification delay manually set to ${delay} seconds" | tee -a ${LOGS}
  fi

  while [ $delay -gt 900 ]; do
    if [ $delay -gt 60 ]; then
      declare -i delay_amount=delay/60
      export delay_unit="minutes"
      export delay_amount=$delay_amount
    else
      declare -i delay_amount=$delay
      export delay_unit="seconds"
      export delay_amount=$delay_amount
    fi
    echo "Notify players that wipe will begin, in ${delay_amount} ${delay_unit}." | tee -a ${LOGS}
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "say \"Scheduled map wipe is about to begin, in ${delay_amount} ${delay_unit}.\"" | tee -a ${LOGS}

    if [ $delay -gt 900 ]; then
      declare -i delay=delay-900
      export delay=$delay
      sleep 900
    fi
  done

  ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "restart $delay \"Scheduled map wipe is about to begin.\""
  sleep $delay
  sleep 5
}

function wipe_map() {
  echo "Wipe out old maps and related data" | tee -a ${LOGS}
  rm -rf ${GAME_ROOT}/server/solidrust/*.map
  rm -rf ${GAME_ROOT}/server/solidrust/*.sav*
  rm -rf ${GAME_ROOT}/server/solidrust/sv.*
}

function wipe_kits() {
  echo "Wipe out all saved kits" | tee -a ${LOGS}
  echo "oxide/data/Kits/Data.json"
  rm -rf ${GAME_ROOT}/oxide/data/Kits/Data.json
}

function wipe_banks() {
  echo "Wipe out Banks and ATM related data" | tee -a ${LOGS}
  echo "oxide/data/BankSystem"
  ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "bank.wipe 0"
  echo "/game/oxide/data/Economics.json"
  ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "ecowipe"
}

function wipe_backpacks() {
  echo "Wipe out player-saved backpack items and related data" | tee -a ${LOGS}
  echo "oxide/data/Backpacks/*.json"
  rm -rf ${GAME_ROOT}/oxide/data/Backpacks/*.json
}

function wipe_leaderboards() {
  echo "Wipe out old Procedural maps and related data" | tee -a ${LOGS}
  rm -rf ${GAME_ROOT}/server/solidrust/player.*
}

function wipe_permissions() {
  echo "Wipe out old Procedural maps and related data" | tee -a ${LOGS}
  MOD_GROUPS=$(${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.show groups" | grep -v "Groups:" | sed -z 's/, /\n/g' | grep -v default)
  for group in ${MOD_GROUPS[@]}; do
    echo " - Removing: $group" | tee -a ${LOGS}
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.group remove $group" | tee -a ${LOGS}
  done
  echo "=> Reload permissions sync" | tee -a ${LOGS}
  ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.reload PermissionGroupSync" | tee -a ${LOGS}
}

function change_seed() {
  echo "Changing seed for world size: ${WORLD_SIZE}" | tee -a ${LOGS}
  if [ -f "${GAME_ROOT}/server.seed.new" ]; then
    export SEED=$(cat ${GAME_ROOT}/server.seed.new)
    echo "Staged Map Seed found: ${SEED}" | tee -a ${LOGS}
    cp ${GAME_ROOT}/server.seed ${GAME_ROOT}/server.seed.old
    echo ${SEED} >${GAME_ROOT}/server.seed
    rm -f ${GAME_ROOT}/server.seed.new
  else
    echo "Generating new ${WORLD_SIZE} seed." | tee -a ${LOGS}
    if [ -f "${HOME}/solidrust.net/defaults/procedural-maps/${WORLD_SIZE}-full.txt" ]; then
      echo "Custom SRT map list found" | tee -a ${LOGS}
      export SEED=$(shuf -n 1 ${HOME}/solidrust.net/defaults/procedural-maps/${WORLD_SIZE}-full.txt)
    else
      echo "using a truly random seed" | tee -a ${LOGS}
      export SEED=$(shuf -i 1-2147483648 -n 1)
    fi
    echo "New ${WORLD_SIZE} Map Seed generated: ${SEED}" | tee -a ${LOGS}
    cp ${GAME_ROOT}/server.seed ${GAME_ROOT}/server.seed.old
    echo ${SEED} >${GAME_ROOT}/server.seed
  fi
  echo "Installed new map seed to ${GAME_ROOT}/server.seed" | tee -a ${LOGS}
}

echo "SRT Wipe Functions initialized" | tee -a ${LOGS}
