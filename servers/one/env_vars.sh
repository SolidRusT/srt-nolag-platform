#!/bin/bash
## Global default environments

# root of where the game server is installed
export GAME_ROOT="/root/.steamapps/common/rust_dedicated"
# construct server logging endpoint
export SERVER_LOGS="${GAME_ROOT}/RustDedicated.log"
# toggle custom maps
export CUSTOM_MAP="enabled" # enabled / disabled
export CUSTOM_MAP_URL="https://solidrust.net/maps/Stellarium6.map" #  only if CUSTOM_MAP is "enabled"