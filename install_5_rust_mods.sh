#!/bin/bash
#export STEAMUSER="rusty"
#export GAME_DIR="/game"

cd ${GAME_DIR}
wget https://umod.org/games/rust/download/develop -O \
    Oxide.Rust.zip
unzip -o Oxide.Rust.zip
rm Oxide.Rust.zip

wget https://umod.org/extensions/discord/download -O \
    ${GAME_DIR}/RustDedicated_Data/Managed/Oxide.Ext.Discord.dll

wget http://playrust.io/latest -O \
    ${GAME_DIR}/RustDedicated_Data/Managed/Oxide.Ext.RustIO.dll

wget https://github.com/k1lly0u/Oxide.Ext.RustEdit/raw/master/Oxide.Ext.RustEdit.dll -O \
    ${GAME_DIR}/RustDedicated_Data/Managed/Oxide.Ext.RustEdit.dll

mkdir -p oxide/plugins && cd oxide/plugins
wget https://umod.org/plugins/PermissionGroupSync.cs
