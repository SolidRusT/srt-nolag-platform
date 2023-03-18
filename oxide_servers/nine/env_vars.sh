#!/bin/bash
## Global default environments
export SRT_TYPE="game"
# root of where the game server is installed
export GAME_ROOT="/game"
# construct server logging endpoint
export SERVER_LOGS="${GAME_ROOT}/RustDedicated.log"
# Map stuff
export WORLD_SIZE="3700"
# toggle custom maps
export CUSTOM_MAP="disabled" # enabled / disabled
export CUSTOM_MAP_URL="" #  only if CUSTOM_MAP is "enabled"
# Current Map seed
export SEED=$(cat ${GAME_ROOT}/server.seed)
# Discord Settings
export WEBHOOK=https://discordapp.com/api/webhooks/869389187667877898/7ufiQxBLWqs2FTXJT2y3AcGAqstL129Lg6WPbuRBB3WRrPHJwbIdxYyHqosSgT-NNqtp
export CORDNAME="Nine Watchdog"