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
now | fast | quick)
    initialize_srt
    echo "performing a Quick Wipe" | tee -a ${LOGS}
    stop_rust_now
    wipe_map
    change_seed
    start_rust
    update_map_api
    ;;

force | forcewipe | facepunch)
    initialize_srt
    echo "performing a Facepunch Force-wipe" | tee -a ${LOGS}
    notification
    update_repo
    update_server
    update_mods
    update_configs
    wipe_map
    change_seed
    wipe_kits
    wipe_backpacks
    wipe_banks
    wipe_leaderboards
    #wipe_permissions
    start_rust
    update_map_api
    #update_permissions
    ;;

*)
    initialize_srt
    echo "performing a Standard Wipe" | tee -a ${LOGS}
    notification
    update_repo
    update_server
    update_mods
    update_configs
    wipe_map
    change_seed
    wipe_kits
    start_rust
    update_map_api
    #update_permissions
    ;;
esac
