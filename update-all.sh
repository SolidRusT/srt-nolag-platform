#!/bin/bash
CUSTOM_ROOT=$1
GAME_ROOT="/home/modded"

if [ -z "${CUSTOM_ROOT}" ]; then
    CUSTOM_ROOT="${GAME_ROOT}"
    echo "CUSTOM_ROOT: ${CUSTOM_ROOT}"
else
    echo "GAME_ROOT: ${CUSTOM_ROOT}"
fi

plugins=$(ls -1 "$CUSTOM_ROOT/oxide/plugins")

for plugin in ${plugins[@]}; do
    echo wget -N "https://umod.org/plugins/$plugin" -O  "$CUSTOM_ROOT/oxide/plugins/$plugin"
    sleep 3
done
