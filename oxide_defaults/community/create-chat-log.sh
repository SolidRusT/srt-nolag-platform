tail -n +1 -f /game/RustDedicated.log | grep CHAT | tee -a /game/chat-global.log &

#!/bin/bash
mypid="$!"
echo "Starting chatlog infinite loop on PID: ${mypid}"
fielpos="/game/chat-global.log"
while true; do
    declare -i lineno=0
    while read -r line; do
        send_discord "${line}"
        let ++lineno
        sed -i "1 d" "$fielpos"
        sleep 1
    done < "$fielpos"
    sleep 1
done