## THIS IS NOT A SCRIPT (yet)

sudo apt install xfsprogs
reboot

sudo su - ${STEAMUSER}
sudo mkdir /game
sudo mkfs -t xfs /dev/nvme0n1
sudo mount /dev/nvme0n1 /game
sudo chown -R ${USER}:${USER} /game
sudo chown -R ${USER}:${USER} ${HOME}

rsync -ar --exclude 'Bundles' ${HOME}  /game/

steamcmd +login anonymous +force_install_dir /game/${USER} +app_update 258550 validate +quit

sudo chown -R ${USER}:${USER} /game
sudo chown -R ${USER}:${USER} ${HOME}

# cron to do this push-pull

