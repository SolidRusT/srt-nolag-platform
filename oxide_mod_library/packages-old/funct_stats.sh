function global_online() {
  echo "Refresh the list of online players across all servers" | tee -a ${LOGS}
  SERVERS=(us-west-10x ca-west-100x us-east-1000x)
  GLOBAL_ONLINE="${GAME_ROOT}/global_online.out"
  touch ${GLOBAL_ONLINE}
  cat /dev/null > ${GLOBAL_ONLINE}
  for server in ${SERVERS[@]}; do
    ${GAME_ROOT}/rcon -a $server.solidrust.net:28016 -p NOFAGS -t web "players" | \
      sed '1d' > ${GLOBAL_ONLINE}.$server
    for player in $(cat ${GLOBAL_ONLINE}.$server | awk {' print $1 '}); do
      #player=$(grep $player global_online.out.us-east-1000x | {' print $2 '})
      #echo "$player_name $server" >> ${GLOBAL_ONLINE}
      echo "$player $server" >> ${GLOBAL_ONLINE}
    done
  done
  # Calculate total number of online players
  GLOBAL_ONLINE_PLAYER_COUNT=$(cat ${GLOBAL_ONLINE} | wc -l | tee ${GLOBAL_ONLINE}.count)
  echo "Total of: ${GLOBAL_ONLINE_PLAYER_COUNT} players online."
  # Update list of player roles
  echo " - Collecting player roles..."
  mysql -u ${SQL_USER} --password=${SQL_PASS} -h ${SQL_HOST} -D solidrust_lcy -ss -e  \
    'SELECT steamid, groupname FROM permissiongroupsync' > ${GLOBAL_ONLINE}.player_roles
  # XPerience -> XPerience
  echo " - Collecting player XP Stats..."
  mysql -u ${SQL_USER} --password=${SQL_PASS} -h ${SQL_HOST} -D XPerience -ss -e  \
    'SELECT steamid, level, experience, status  FROM XPerience' > ${GLOBAL_ONLINE}.player_xpstats
  # RustPlayers -> west
  echo " - Collecting player playtime statistics..."
  mysql -u ${SQL_USER} --password=${SQL_PASS} -h ${SQL_HOST} -D RustPlayers -ss -e  \
    'SELECT name, steamid, `Last Seen` FROM west' > ${GLOBAL_ONLINE}.player_playtime
  # srt_web_auth -> users
  echo " - Collecting player registration data..."
  mysql -u ${SQL_USER} --password=${SQL_PASS} -h ${SQL_HOST} -D srt_web_auth -ss -e  \
    'SELECT steam_id, nitro, timestamp FROM users' > ${GLOBAL_ONLINE}.player_registration
}

# epoch to human
# date -d @1643253457

echo "SRT Statistics Functions initialized" | tee -a ${LOGS}







