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
  GLOBAL_ONLINE_PLAYER_COUNT=$(cat ${GLOBAL_ONLINE} | wc -l | tee ${GLOBAL_ONLINE}.count)
  echo "Total of: ${GLOBAL_ONLINE_PLAYER_COUNT} players online."
  cat ${GLOBAL_ONLINE}
}

echo "SRT Statistics Functions initialized" | tee -a ${LOGS}


# last update
# select * from west WHERE userid = "76561198024774727";
# date -d @1643253457

# The last time progress was made
# RustPlayers -> west
#SELECT name, steamid, `Last Seen` FROM west WHERE steamid = "76561198024774727";
# srt_web_auth -> users
#SELECT steam_id, nitro, timestamp FROM users WHERE steam_id = "76561198024774727";
# XPerience -> XPerience
#SELECT steamid, level, experience, status  FROM XPerience WHERE steamid = "76561198024774727";
# solidrust_lcy -> permissiongroupsync
#SELECT steamid, groupname FROM permissiongroupsync WHERE steamid = "76561198024774727";

# one-liner
#mysql -u srt_sl_lcy --password=lcy_402 -h data.solidrust.net -D solidrust_lcy -ss -e \
#  'SELECT steamid, groupname FROM permissiongroupsync WHERE steamid = "76561198024774727"'