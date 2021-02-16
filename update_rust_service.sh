#!/bin/bash
## Install on:
# - Game Server
#
# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh

# Game Node: if game service is still running
${GAME_ROOT}/rcon -c ${RCON_CFG} "server.save"
${GAME_ROOT}/rcon -c ${RCON_CFG} "server.writecfg"
${GAME_ROOT}/rcon -c ${RCON_CFG} "quit"

# refresh OS packages
echo "===> Buffing-up Debian Distribution..."
sudo apt update
sudo apt -y dist-upgrade
# TODO: output a message to reboot if kernel or initrd was updated

# Refresh Steam installation
echo "===> Validating installed Steam components..."
steamcmd +login anonymous +force_install_dir ${GAME_ROOT}/ +app_update 258550 validate +quit

# Update uMod platform
echo "===> Updating uMod..."
cd ${GAME_ROOT}
wget https://umod.org/games/rust/download/develop -O \
    Oxide.Rust.zip && \
    unzip -o Oxide.Rust.zip && \
    rm Oxide.Rust.zip

# Integrate discord binary
echo "===> Downloading discord binary..."
wget https://umod.org/extensions/discord/download -O \
    ${GAME_ROOT}/RustDedicated_Data/Managed/Oxide.Ext.Discord.dll

# Integrate RustEdit binary
echo "===> Downloading RustEdit.io binary..."
wget https://github.com/k1lly0u/Oxide.Ext.RustEdit/raw/master/Oxide.Ext.RustEdit.dll -O \
    ${GAME_ROOT}/RustDedicated_Data/Managed/Oxide.Ext.RustEdit.dll

# Integrate Rust:IO binary
wget http://playrust.io/latest -O \
    ${GAME_ROOT}/RustDedicated_Data/Managed/Oxide.Ext.RustIO.dll

# Update custom maps
aws s3 sync ${S3_WEB}/maps ${GAME_ROOT}/server/solidrust









#### GARBAGE
# Secondary Servers
export SOURCE_S3="s3://suparious.com/backup/west"
export INSTALL_DIR=/home/modded

cd && mkdir shit
SHITS=(
oxide/config/AutoDemoRecordLite.json
oxide/config/DiscordCore.json
oxide/config/DiscordEvents.json
oxide/config/DiscordMessages.json
oxide/config/DiscordRewards.json
oxide/config/DiscordServerStats.json
oxide/config/DiscordWelcomer.json
oxide/config/DiscordWipe.json
oxide/config/PlayerDatabase.json
oxide/config/PluginUpdateNotifications.json
)

for shit in ${SHITS[@]}; do
    cp $shit shit/
done

aws s3 sync --delete --quiet ${SOURCE_S3}/oxide/config /home/modded/oxide/config
aws s3 cp --quiet ${SOURCE_S3}/oxide/data/Kits.json /home/modded/oxide/data/Kits.json
aws s3 cp --quiet ${SOURCE_S3}/oxide/data/GuardedCrate.json /home/modded/oxide/data/GuardedCrate.json
aws s3 cp --quiet ${SOURCE_S3}/oxide/data/BetterChat.json /home/modded/oxide/data/BetterChat.json

cp /home/modded/shit/* /home/modded/oxide/config

rm -rf shit

aws s3 sync --delete ${SOURCE_S3}/oxide/plugins /home/modded/oxide/plugins

/home/modded/rcon -c /home/modded/rcon.yaml "o.reload *"


#(M) Economics.json
#(M) ServerRewards/*



# Create update
aws s3 sync --quiet s3://suparious.com/oxide oxide

# Download updates
aws s3 sync --quiet s3://suparious.com/oxide/plugins oxide/plugins
aws s3 sync --quiet s3://suparious.com/oxide/config oxide/config

