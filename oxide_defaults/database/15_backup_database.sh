#!/bin/bash
## Install on:
# - Game Server
#
## crontab example:
#      M H    D ? Y
#echo "15 *    * * *   ${USER}  ${HOME}/solidrust.net/defaults/database/15_backup_database.sh" | sudo tee -a /etc/crontab

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

echo "MySQL is taking a dump" | tee -a ${LOGS}
# mysqldump -u [username] –p[password] [database_name] > [dump_file.sql]
# - RustPlayers
# - solidrust_lcy

SQL_GRANTS_OUT="/dev/shm/MySQLGrants.sql"
SQL_DATA_OUT="/dev/shm/MySQLData.sql"

mysql --skip-column-names -A \
    -e"SELECT CONCAT('SHOW GRANTS FOR ''',user,'''@''',host,''';') FROM mysql.user WHERE user<>''" | \
    mysql --skip-column-names -A | \
    sed 's/$/;/g' > ${SQL_GRANTS_OUT}

mysqldump --all-databases --routines --triggers > ${SQL_DATA_OUT}

echo "Pushing MySQL dump to s3" | tee -a ${LOGS}
aws s3 cp --quiet ${SQL_GRANTS_OUT} ${S3_BACKUPS}/servers/${HOSTNAME}/ | tee -a ${LOGS}
aws s3 cp --quiet ${SQL_DATA_OUT} ${S3_BACKUPS}/servers/${HOSTNAME}/ | tee -a ${LOGS}

echo "Finished ${me}"   | tee -a ${LOGS}