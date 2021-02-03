#!/bin/bash
GAME_DIR=$HOME
cd ${GAME_DIR}
LOG_DATE=$(date +"%Y_%m_%d_%I_%M_%p")

./RustDedicated -batchmode -nographics -silent-crashes \
    -server.ip 0.0.0.0 \
    -rcon.ip 0.0.0.0 \
    -server.port 28015 \
    -rcon.port 28016 \
    -app.port 28082 \
    -rcon.web 1 \
    -rcon.password "NOFAGS" \
    -server.level "Procedural Map" \
    -server.identity "solidrust" \
    -server.worldsize 3500 \
    -server.seed 973546 \
    -server.tickrate 30 \
    -server.saveinterval 900 \
    -server.maxplayers 300  \
    -server.globalchat true \
    -fps.limit 250 \
    -server.savebackupcount "2" \
    -logfile 2>&1 "RustDedicated-${LOG_DATE}.log"
