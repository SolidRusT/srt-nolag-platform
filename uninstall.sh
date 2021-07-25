#!/bin/bash
# This is SUPERBAD, please don't run this
GAME_ROOT="/game"

# delete all non-player data

# nuke folders
rm -rf ${GAME_ROOT}/steam*
rm -rf ${GAME_ROOT}/.steam
rm -rf ${GAME_ROOT}/RustDedicated*
rm -rf ${GAME_ROOT}/HarmonyMods
rm -rf ${GAME_ROOT}/Bundles
rm -rf ${GAME_ROOT}/.mono

# nuke files
rm ${GAME_ROOT}/LinuxPlayer_s.debug
rm ${GAME_ROOT}/server/solidrust/*.map
rm ${GAME_ROOT}/server/solidrust/*.sav*
rm ${GAME_ROOT}/Compiler.x86_x64
rm ${GAME_ROOT}/runds.sh
rm ${GAME_ROOT}/libsteam_api.so
rm ${GAME_ROOT}/Unity*
