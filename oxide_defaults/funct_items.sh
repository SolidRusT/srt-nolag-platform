function give_item() {
    export user=$1
    export item=$2
    export qty=$3
    echo "Giving $user ${qty}x ${item}(s)" | tee -a ${LOGS}
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "inventory.giveto $user $item $qty" | tee -a ${LOGS}
    sleep 1
}

function give_kit() {
    export user=$1
    export kit=$2
    case "${kit}" in
    ak)
        echo "Assault Rifle, laser sight and 8x scope"
        give_item $user rifle.ak 1
        give_item $user weapon.mod.lasersight 1
        give_item $user weapon.mod.small.scope 1
        ;;
    builder)
        echo "Building Materials"
        give_item $user wood 5000
        give_item $user stones 10000
        give_item $user metal.fragments 5000
        give_item $user metal.refined 500
        give_item $user building.planner 1
        give_item $user hammer 1        
        ;;
    *)
        echo "Please specify a valid kit."
        ;;
    esac
}

echo "SRT Item Functions initialized" | tee -a ${LOGS}
