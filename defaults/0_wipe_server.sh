#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "0 0    * * *   ${USER}  /bin/sh -c ${HOME}/solidrust.net/defaults/99_wipe_server.sh" | sudo tee -a /etc/crontab
export type="$1"

source ${HOME}/solidrust.net/defaults/funct_common.sh
source ${HOME}/solidrust.net/defaults/funct_wipe.sh
source ${HOME}/solidrust.net/defaults/funct_update.sh

if [ -z $type ]; then
    echo "is empty"
    if [ "$(date +\%d)" -le 7 ]; then
        echo "forcewipe"
        export type="forcewipe"
    else
        echo "standard wipe"
        export type="standard"
    fi
else
    echo "running manually"
fi

case "$type" in
now | fast | quick)
    initialize_srt
    echo "performing a Quick Wipe" | tee -a ${LOGS}
    notification_restart 1
    wipe_map
    change_seed
    start_rust
    update_map_api
    ;;

force | forcewipe | facepunch)
    initialize_srt
    echo "performing a Facepunch Force-wipe" | tee -a ${LOGS}
    #sleep 3600
    sleep 2900
    notification_restart 3600
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
    notification_restart 3600
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
