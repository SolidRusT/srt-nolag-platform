#!/bin/bash
## Global default environments
export SRT_TYPE="game"
# root of where the game server is installed
export GAME_ROOT="/game"
# construct server logging endpoint
export SERVER_LOGS="${GAME_ROOT}/RustDedicated.log"
# Map stuff
export WORLD_SIZE="2700"
# toggle custom maps
export CUSTOM_MAP="enabled" # enabled / disabled
export CUSTOM_MAP_URL="https://solidrust.net/maps/Booty_Island_ver2.map" #  only if CUSTOM_MAP is "enabled"
# Current Map seed
export SEED=$(cat ${GAME_ROOT}/server.seed)
# Discord Settings
export WEBHOOK=https://discord.com/api/webhooks/954609421185593365/fyYcxae7shMAlrlet2mapqVRGXMxCW_8GzBrcZmS77GpJHu6z2uVC0Ay1hchQrtLJeGs
export CORDNAME="ca-100x Watchdog"
