export STEAMUSER="root"
export GAME_ROOT="/game"
echo "export STEAMUSER=\"${STEAMUSER}\"" >> ~/.bashrc
echo "export GAME_ROOT=\"${GAME_ROOT}\"" >> ~/.bashrc

sudo mkdir ${GAME_ROOT}
sudo mkfs -t xfs /dev/nvme0n1
sudo mount /dev/nvme0n1 ${GAME_ROOT}

/usr/games/steamcmd +login anonymous +force_install_dir ${GAME_ROOT} +app_update 258550 +quit
/usr/games/steamcmd +login anonymous +force_install_dir ${GAME_ROOT} +app_update 258550 validate +quit

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