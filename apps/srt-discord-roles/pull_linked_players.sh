#!/bin/bash
# sudo apt update && sudo apt install -y jsonlint
DATE_STAMP=$(date +%Y%m%d%H%M)
SQL_USER="srt_sl_lcy"
SQL_PASS="lcy_402"
SQL_HOST="data.solidrust.net"
SQL_DB="srt_web_auth"
SQL_QUERY="SELECT discord_id, steam_id FROM users"
SQL_OUTPUT="/dev/shm/sql-out-${DATE_STAMP}"
JSON_OUT="DiscordRoles.json"
# Fetch records from database
mysql -N -u${SQL_USER} -p${SQL_PASS} -h${SQL_HOST} -D${SQL_DB} <<< ${SQL_QUERY} | awk -F " " {' print $2","$1 '} | sort | uniq > $SQL_OUTPUT
# Apply JSON header to the output
echo "{
  \"PlayerDiscordInfo\": {" > ${JSON_OUT}
# Output linked players
while IFS="," read -r steam_id discord_id; do
  echo "    \"$steam_id\": {
      \"DiscordId\": \"$discord_id\",
      \"PlayerId\": \"$steam_id\"
    },"
done < $SQL_OUTPUT >> ${JSON_OUT}
# apply JSON footer
echo "    \"99999999999999999\": {
      \"DiscordId\": \"999999999999999999\",
      \"PlayerId\": \"99999999999999999\"
    }
  },
  \"LeftPlayerInfo\": {},
  \"MessageData\": null
}" >> ${JSON_OUT}
# Check output for errors
jsonlint-php ${JSON_OUT}
if [ $? -eq 0 ];then
  echo "Passed JSON syntax check"
  # Move formatted player link data to oxide dir
  mv ${JSON_OUT} ${GAME_ROOT}/oxide/data/
  # RCON reload DiscordRoles
  ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.reload DiscordRoles"
else
  echo "failed JSON syntax check, not pushing results"
fi
# Clean temp files
rm ${SQL_OUTPUT}