#!/bin/bash
# Secondary Servers
export SOURCE_S3="s3://suparious.com/backup/west"
export INSTALL_DIR=/home/modded


aws s3 sync --delete ${SOURCE_S3}/oxide/plugins /home/modded/oxide/plugins

/home/modded/rcon -c /home/modded/rcon.yaml "o.reload *"


#(M) Economics.json
#(M) ServerRewards/*
