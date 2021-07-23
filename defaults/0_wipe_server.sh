#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "0 0    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/99_wipe_server.sh" | sudo tee -a /etc/crontab

source ${HOME}/solidrust.net/defaults/funct_common.sh
source ${HOME}/solidrust.net/defaults/funct_wipe.sh
source ${HOME}/solidrust.net/defaults/funct_update.sh

case "$1" in
    now | fast | quick )
        echo "performing a Quick Wipe"
        initialize_srt
        stop_rust_now
        wipe_map
        change_seed
        start_rust
        update_map_api
        ;;
    *)
        echo "performing a Standard Wipe"
        initialize_srt
        update_repo
        initialize_srt
        notification
        update_server        
        update_mods
        update_configs
        wipe_map
        change_seed
        start_rust
        update_map_api
        #update_permissions
        ;;
esac