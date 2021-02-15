#!/bin/bash
GAME_DIR="/game/${USER}"
cd ${GAME_DIR}
LOG_DATE=$(date +"%Y_%m_%d_%I_%M_%p")
LOG_FILE="RustDedicated-${LOG_DATE}.log"

# Launch game server
echo "===> Touching my peepee..."
sudo ./RustDedicated -batchmode -nographics -silent-crashes \
    -server.ip 0.0.0.0 \
    -rcon.ip 0.0.0.0 \
    -server.port 28015 \
    -rcon.port 28016 \
    -app.port 28082 \
    -rcon.web 1 \
    -rcon.password "NOFAGS" \
    -server.identity "solidrust" \
    -logfile 2>&1 ${LOG_FILE}

echo "I'm done! (finished): ${LOG_FILE}"

exit 0