#!/bin/bash
## Global default environments
# automation lock
export SOLID_LCK="${HOME}/SolidRusT.lock"
# construct default server logging endpoint
export SERVER_LOGS="${GAME_ROOT}/RustDedicated.log"
# Amazon s3 destination for backups
export S3_BACKUPS="${S3_REPO}/backups"
# Github source for configs
export GITHUB_ROOT="${STEAMUSER}/srt-nolag-platform"
# Default configs
export SERVER_GLOBAL="${GITHUB_ROOT}/defaults"
# Customized config
export SERVER_CUSTOM="${GITHUB_ROOT}/servers/${HOSTNAME}"
# local RCON CLI config
export RCON_CFG="${GITHUB_ROOT}/defaults/rcon.yaml"
# logging format
export LOG_DATE=$(date +"%Y_%m_%d_%I_%M_%p")
# Build root
export BUILD_ROOT="${HOME}/build-solidrust"
# Current server external IP
export SERVER_IP=$(curl -s http://whatismyip.akamai.com/)
# MySQL connection
export SQL_HOST=""
export SQL_USER=""
export SQL_PASS=""
# end of env_vars
echo "++++++= Initialized SolidRusT =++++++" | tee -a ${LOGS}