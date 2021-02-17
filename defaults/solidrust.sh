#!/bin/bash
## Start SolidRusT

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh

# construct server logging endpoint
export SERVER_LOGS="${GAME_ROOT}/RustDedicated.log"

# enter game root
cd ${GAME_ROOT}

# Launch game server
echo "===> Touching my peepee..." | tee -a ${LOGS}
./RustDedicated -batchmode -nographics -silent-crashes \
    -server.ip 0.0.0.0 \
    -rcon.ip 0.0.0.0 \
    -server.port 28015 \
    -rcon.port 28016 \
    -app.port 28082 \
    -rcon.web 1 \
    -rcon.password "NOFAGS" \
    -server.identity "solidrust" \
    -logfile 2>&1 ${SERVER_LOGS} | tee -a ${LOGS}

# Stamp log with quit time
echo "I'm done! (finished): ${SERVER_LOGS}" | tee -a ${LOGS}

# exit cleanly
exit 0
