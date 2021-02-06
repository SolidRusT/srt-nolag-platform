#!/bin/bash
## Configuration
# example crontab
#echo "* *    * * *   modded  /home/modded/solidrust.net/permissions_sync.sh" | sudo tee -a /etc/crontab
# root of where the game server is installed
export GAME_ROOT="/home/modded"
# local RCON CLI config 
export RCON_CFG="${GAME_ROOT}/solidrust.net/servers/rcon.yaml" 

# Update global group permissions
## TODO: make this a separate cron
${GAME_ROOT}/rcon -c ${RCON_CFG} "o.load *"
sleep 15
${GAME_ROOT}/rcon -c ${RCON_CFG} "o.reload PermissionGroupSync"

# TODO: Figure out global economics
#(M) Economics.json
#(M) ServerRewards/*
#Backpack
#Bank

