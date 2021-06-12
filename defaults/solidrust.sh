#!/bin/bash
## Start SolidRusT

# Example launch:
# screen -dmS game /bin/bash "${HOME}/solidrust.net/defaults/solidrust.sh"
# tail -n 20 -F "${HOME}/SolidRusT.log" "${GAME_ROOT}/RustDedicated.log"

# Load SRT functions
source ${HOME}/solidrust.net/defaults/funct_common.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

initialize_srt

# enter game root
cd ${GAME_ROOT}

# Launch game server
echo "===> Touching my peepee..." | tee -a ${LOGS}
./RustDedicated -batchmode -nographics -silent-crashes -logfile 2>&1 ${SERVER_LOGS} \
    +server.ip 0.0.0.0 \
    +server.port 28015 \
    +rcon.ip 0.0.0.0 \
    +rcon.port 28016 \
    +server.tickrate 30 \
    +app.port 28082 \
    +rcon.web 1 \
    +rcon.password "NOFAGS" \
    +server.identity "solidrust"

# Stamp log with quit time
echo "I'm done! (finished): ${SERVER_LOGS}" | tee -a ${LOGS}

