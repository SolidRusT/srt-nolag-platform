#!/bin/bash
## Global default environments

# root of where the game server is installed
export GAME_ROOT="/game"
# construct server logging endpoint
export SERVER_LOGS="${GAME_ROOT}/RustDedicated.log"
# toggle custom maps
export CUSTOM_MAP="disable" # enabled / disabled
export CUSTOM_MAP_URL="" #  only if CUSTOM_MAP is "enabled"
export WORLD_SIZE="2700"
# Current Map seed
export SEED=$(cat ${GAME_ROOT}/server.seed)
# Discord Settings
export WEBHOOK=https://discordapp.com/api/webhooks/868649931374727268/dhbp5kB_lOHbPm9eCOIGpzQGSRaQFjyJIu59XzF9wDysUlZMHcm_TEggrCAYL1NputJC
export CORDNAME="Two Watchdog"