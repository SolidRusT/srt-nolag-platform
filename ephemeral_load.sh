## THIS IS NOT A SCRIPT (yet)

sudo apt install xfsprogs
reboot

sudo su - ${STEAMUSER}
sudo mkdir /game
sudo mkfs -t xfs /dev/nvme0n1
sudo mount /dev/nvme0n1 /game
sudo chown ${USER}:${USER} /game


rsync -ar ${HOME}  /game/

#sudo chown -R ${USER}:${USER} /game

sudo chown -R ${USER}:${USER} ${HOME}
rsync -ar /game/${USER}  /home/