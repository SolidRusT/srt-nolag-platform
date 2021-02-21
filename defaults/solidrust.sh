#!/bin/bash
## Start SolidRusT

# Example launch:
# screen -dmS game /bin/bash "${HOME}/solidrust.net/defaults/solidrust.sh"

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

# construct server logging endpoint
export SERVER_LOGS="${GAME_ROOT}/RustDedicated.log"

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
    +server.itemdespawn 60 \
    +fps.limit 60 \
    +app.port 28082 \
    +rcon.web 1 \
    +rcon.password "NOFAGS" \
    +server.identity "solidrust" \
    +

# Stamp log with quit time
echo "I'm done! (finished): ${SERVER_LOGS}" | tee -a ${LOGS}

# exit cleanly
exit 0
