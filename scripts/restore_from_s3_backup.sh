## Game server
#!/bin/bash
export PULL_FROM="nine"
export S3_BACKUPS="s3://solidrust.net-backups"
export S3_REPO="s3://solidrust.net-repository"

mkdir -p ${GAME_ROOT}
mkfs -t xfs /dev/nvme0n1
mount /dev/nvme0n1 ${GAME_ROOT}

/usr/games/steamcmd +force_install_dir ${GAME_ROOT} +login anonymous +app_update 258550 +quit

aws s3 sync --delete ${S3_BACKUPS}/servers/${PULL_FROM}/oxide ${GAME_ROOT}/oxide
aws s3 sync --delete ${S3_BACKUPS}/servers/${PULL_FROM}/server ${GAME_ROOT}/server
mkdir -p ${GAME_ROOT}/backup

# check seed

update_server
update_repo game && update_mods


## Database server
#!/bin/bash
sudo su
export SQL_BACKUPS="/dev/shm"
export S3_BACKUPS="s3://solidrust.net-backups"
aws s3 sync --quiet --delete ${S3_BACKUPS}/servers/${HOSTNAME} ${SQL_BACKUPS}

mysql < /dev/shm/MySQLData.sql
mysql < /dev/shm/MySQLGrants.sql
