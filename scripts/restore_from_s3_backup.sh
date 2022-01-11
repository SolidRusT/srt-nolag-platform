## Game server
#!/bin/bash
# setup bashrc and env-vars or export GAME_ROOT="/game"
export PULL_FROM="demo"
export S3_BACKUPS="s3://solidrust.net-backups"
export S3_REPO="s3://solidrust.net-repository"

mkdir -p ${GAME_ROOT}
#mkfs -t xfs /dev/nvme0n1
#mount /dev/nvme0n1 ${GAME_ROOT}

/usr/games/steamcmd +force_install_dir ${GAME_ROOT} +login anonymous +app_update 258550 +quit

# setup aws cli if using bare-metal
aws s3 sync --delete ${S3_BACKUPS}/servers/${PULL_FROM}/oxide ${GAME_ROOT}/oxide
aws s3 sync --delete ${S3_BACKUPS}/servers/${PULL_FROM}/server ${GAME_ROOT}/server
aws s3 sync --delete ${S3_REPO} ${HOME}/solidrust.net
mkdir -p ${GAME_ROOT}/backup

# logout and login to test the .bashrc SRT console initialization

# check seed
update_server
update_repo game && update_mods

# logout and login to reset the .bashrc SRT console initialization
# update server, wipe the map and set your seed if using procedural
rm -rf ${HOME}/solidrust.net
update_repo game && update_mods
update_server
update_repo game && update_mods
update_configs
wipe_map
    change_seed
    wipe_kits
    wipe_backpacks
    wipe_leaderboards
    #wipe_permissions
    start_rust
    update_map_api
    update_permissions

## Database server
#!/bin/bash
sudo su
export SQL_BACKUPS="/dev/shm"
export S3_BACKUPS="s3://solidrust.net-backups"
aws s3 sync --quiet --delete ${S3_BACKUPS}/servers/${HOSTNAME} ${SQL_BACKUPS}

mysql < /dev/shm/MySQLData.sql
mysql < /dev/shm/MySQLGrants.sql
