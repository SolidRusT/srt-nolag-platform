#!/bin/bash
GAME_DIR=$HOME
cd ${GAME_DIR}
LOG_DATE=$(date +"%Y_%m_%d_%I_%M_%p")

steamcmd +login anonymous +force_install_dir ~/ +app_update 258550 +quit
steamcmd +login anonymous +force_install_dir ~/ +app_update 258550 validate +quit

wget https://umod.org/extensions/discord/download -O ~/RustDedicated_Data/Managed/Oxide.Ext.Discord.dll

wget https://umod.org/games/rust/download -O Oxide.Rust.zip
unzip -o Oxide.Rust.zip
rm Oxide.Rust.zip

./RustDedicated -batchmode -nographics -silent-crashes \
    -server.ip 0.0.0.0 \
    -rcon.ip 0.0.0.0 \
    -server.port 28015 \
    -rcon.port 28016 \
    -app.port 28082 \
    -rcon.web 1 \
    -rcon.password "NOFAGS" \
    -server.level "SolidRusT" \
    -server.identity "solidrust" \
    -levelurl https://www.solidrust.net/maps/Stellarium4.map
    -server.tickrate 30 \
    -server.saveinterval 900 \
    -server.maxplayers 300  \
    -server.globalchat true \
    -fps.limit 250 \
    -server.savebackupcount "2" \
    -logfile 2>&1 "RustDedicated-${LOG_DATE}.log"

