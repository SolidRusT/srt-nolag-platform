#!/bin/bash
## Install on:
# - Game Server
#

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

# Game Node: if game service is still running
${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.save"
${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "server.writecfg"
${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "quit"

# refresh OS packages
echo "===> Buffing-up Debian Distribution..." | tee -a ${LOGS}
sudo apt update | tee -a ${LOGS}
sudo apt -y dist-upgrade | tee -a ${LOGS}
# TODO: output a message to reboot if kernel or initrd was updated

# Refresh Steam installation
echo "===> Validating installed Steam components..." | tee -a ${LOGS}
steamcmd +login anonymous +force_install_dir ${GAME_ROOT}/ +app_update 258550 validate +quit | tee -a ${LOGS}

# Update uMod platform
echo "===> Updating uMod..." | tee -a ${LOGS}
cd ${GAME_ROOT}
wget https://umod.org/games/rust/download/develop -O \
    Oxide.Rust.zip && \
    unzip -o Oxide.Rust.zip && \
    rm Oxide.Rust.zip | tee -a ${LOGS}

# Integrate discord binary
echo "===> Downloading discord binary..." | tee -a ${LOGS}
wget https://umod.org/extensions/discord/download -O \
    ${GAME_ROOT}/RustDedicated_Data/Managed/Oxide.Ext.Discord.dll | tee -a ${LOGS}

# Integrate RustEdit binary
echo "===> Downloading RustEdit.io binary..." | tee -a ${LOGS}
wget https://github.com/k1lly0u/Oxide.Ext.RustEdit/raw/master/Oxide.Ext.RustEdit.dll -O \
    ${GAME_ROOT}/RustDedicated_Data/Managed/Oxide.Ext.RustEdit.dll | tee -a ${LOGS}

# Integrate Rust:IO binary
echo "===> Downloading Rust:IO binary..." | tee -a ${LOGS}
wget http://playrust.io/latest -O \
    ${GAME_ROOT}/RustDedicated_Data/Managed/Oxide.Ext.RustIO.dll | tee -a ${LOGS}

# Update custom maps
echo "===> Downloading custom maps..." | tee -a ${LOGS}
aws s3 sync ${S3_WEB}/maps ${GAME_ROOT}/server/solidrust | tee -a ${LOGS}
