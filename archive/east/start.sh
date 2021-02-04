#!/bin/bash
GAME_DIR=$HOME   # same as "~/" but better for scripting
jizz=true
cd ${GAME_DIR}
export LD_LIBRARY_PATH=${LD_LIBRARY_PATH}:${GAME_DIR}/RustDedicated_Data/Plugins/x86_64
while ${jizz} = true; do
echo "Starting up RustDedicated in: ${GAME_DIR}"
LOG_DATE=$(date +"%Y_%m_%d_%I_%M_%p")
./RustDedicated -batchmode -nographics -silent-crashes \
    -server.ip 0.0.0.0 \
    -server.port 28015 \
    -rcon.ip 0.0.0.0 \
    -rcon.port 28016 \
    -rcon.web 1 \
    -rcon.password "NOFAGS" \
    -app.port 28082 \
    -server.level "Procedural Map" \
    -server.identity "rust" \
    -server.worldsize 6000 \
    -server.seed 7880972 \
    -spawn.min_rate 1.7 ^ \
    -spawn.max_rate 2 \
    -spawn.min_density 1.7 ^ \
    -spawn.max_density 2 \
    -gather.rate dispenser * 10 \
    -gather.rate pickup * 10 \
    -gather.rate quarry * 10 \
    -gather.rate survey * 10 \
    -dispenser.scale tree 10 \
    -dispenser.scale ore 10 \
    -dispenser.scale corpse 10 \
    -logfile 2>&1 "RustDedicated-${LOG_DATE}.log"
echo "Restarting server, latest logs are in: ${GAME_DIR}/RustDedicated-${LOG_DATE}.log"
done
cd