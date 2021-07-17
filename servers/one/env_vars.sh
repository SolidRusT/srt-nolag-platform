#!/bin/bash
## Global default environments

# root of where the game server is installed
export GAME_ROOT="/root/.steamapps/common/rust_dedicated"
# construct server logging endpoint
export SERVER_LOGS="${GAME_ROOT}/RustDedicated.log"
# toggle custom maps
export CUSTOM_MAP="enabled" # enabled / disabled
export CUSTOM_MAP_URL="https://solidrust.net/maps/HDRPStellarium3.map" #  only if CUSTOM_MAP is "enabled"
export WORLD_SIZE="4500"
# Current Map seed
export SEED=$(cat ${GAME_ROOT}/server.seed)