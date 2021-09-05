export STEAMUSER="root"
export GAME_ROOT="/game"
echo "export STEAMUSER=\"${STEAMUSER}\"" >>~/.bashrc
echo "export GAME_ROOT=\"${GAME_ROOT}\"" >>~/.bashrc

sudo mkdir -p ${GAME_ROOT}
sudo mkfs -t xfs /dev/nvme0n1
sudo mount /dev/nvme0n1 ${GAME_ROOT}

# Using AWS quickstart AMI
#sudo mkdir -p ${GAME_ROOT}
#sudo mkfs -t xfs /dev/nvme1n1
#sudo mount /dev/nvme1n1 ${GAME_ROOT}

sudo su - ${STEAMUSER}
export GAME_ROOT="/game"
export S3_REPO="s3://solidrust.net-repository"
export LOG_FILE="SolidRusT.log"
export LOGS="${HOME}/${LOG_FILE}"
touch ${LOGS}

echo "Sync repo for game server"
mkdir -p ${HOME}/solidrust.net/servers ${HOME}/solidrust.net/defaults
aws s3 sync --delete \
  --exclude "web/*" \
  --exclude "defaults/web/*" \
  --exclude "defaults/database/*" \
  --exclude "servers/web/*" \
  --exclude "servers/data/*" \
  --exclude "servers/radio/*" \
  ${S3_REPO} ${HOME}/solidrust.net | tee -a ${LOGS}
echo "Setting execution bits" | tee -a ${LOGS}
chmod +x ${HOME}/solidrust.net/defaults/*.sh

cp ${HOME}/solidrust.net/defaults/bashrc ~/.bashrc
exit
sudo su - ${STEAMUSER}
update_repo game
update_server
update_mods

/usr/games/steamcmd +login anonymous +force_install_dir ${GAME_ROOT} +app_update 258550 +quit
/usr/games/steamcmd +login anonymous +force_install_dir ${GAME_ROOT} +app_update 258550 validate +quit

cp ${HOME}/solidrust.net/defaults/bashrc ~/.bashrc

# Create the game service user

# sudo adduser modded --disabled-password --quiet
#sudo adduser ${STEAMUSER} --disabled-password --quiet --home ${GAME_ROOT}
#sudo chown -R ${USER}:${USER} ${HOME}

# login as the game user
#sudo su - ${STEAMUSER}

## Create the game service user
#export STEAMUSER="root"
#export GAME_ROOT="/game"
#echo "export STEAMUSER=\"${STEAMUSER}\"" >> ~/.bashrc
#echo "export GAME_ROOT=\"${GAME_ROOT}\"" >> ~/.bashrc

# VBox on Hyper-V only
#/usr/games/steamcmd +login anonymous +app_update 258550 +quit
#/usr/games/steamcmd +login anonymous +app_update 258550 validate +quit
# OR,
