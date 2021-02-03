#!/bin/bash
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
