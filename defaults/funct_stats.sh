function global_online() {
  echo "Refresh the list of online players across all servers" | tee -a ${LOGS}
  SERVERS=(us-west-10x ca-west-100x us-east-1000x)
  GLOBAL_ONLINE="${GAME_ROOT}/global_online.out"
  touch ${GLOBAL_ONLINE}
  cat /dev/null > ${GLOBAL_ONLINE}
  for server in ${SERVERS[@]}; do
    ${GAME_ROOT}/rcon -a $server.solidrust.net:28016 -p NOFAGS -t web "players" | \
      sed '1d' > ${GLOBAL_ONLINE}.$server
    for player in $(cat ${GLOBAL_ONLINE}.$server); do
      echo "$server    $player" >> ${GLOBAL_ONLINE}
    done
  done
  cat ${GLOBAL_ONLINE}
}

echo "SRT Statistics Functions initialized" | tee -a ${LOGS}
