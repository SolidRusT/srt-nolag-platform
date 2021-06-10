export STEAMUSER="root"
export GAME_DIR="/game"
echo "export STEAMUSER=\"${STEAMUSER}\"" >> ~/.bashrc
echo "export GAME_DIR=\"${GAME_DIR}\"" >> ~/.bashrc

sudo mkdir ${GAME_DIR}
sudo mkfs -t xfs /dev/nvme0n1
sudo mount /dev/nvme0n1 ${GAME_DIR}

# Create the game service user

# sudo adduser modded --disabled-password --quiet
#sudo adduser ${STEAMUSER} --disabled-password --quiet --home ${GAME_DIR}
#sudo chown -R ${USER}:${USER} ${HOME}

# login as the game user
sudo su - ${STEAMUSER}

# Create the game service user
export STEAMUSER="root"
export GAME_DIR="/game"
echo "export STEAMUSER=\"${STEAMUSER}\"" >> ~/.bashrc
echo "export GAME_DIR=\"${GAME_DIR}\"" >> ~/.bashrc

/usr/games/steamcmd +login anonymous +force_install_dir ${GAME_DIR} +app_update 258550 +quit
/usr/games/steamcmd +login anonymous +force_install_dir ${GAME_DIR} +app_update 258550 validate +quit

# VBox on Hyper-V only
#/usr/games/steamcmd +login anonymous +app_update 258550 +quit
#/usr/games/steamcmd +login anonymous +app_update 258550 validate +quit
# OR, 