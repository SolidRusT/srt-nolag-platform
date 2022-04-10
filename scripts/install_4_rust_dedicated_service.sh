export STEAMUSER="root"
export GAME_ROOT="/game"
echo "export STEAMUSER=\"${STEAMUSER}\"" >>~/.bashrc
echo "export GAME_ROOT=\"${GAME_ROOT}\"" >>~/.bashrc

sudo mkdir -p ${GAME_ROOT}
sudo mkfs -t xfs /dev/nvme1n1
sudo mount /dev/nvme1n1 ${GAME_ROOT}

# Using AWS quickstart AMI
#sudo mkdir -p ${GAME_ROOT}
#sudo mkfs -t xfs /dev/nvme1n1
#sudo mount /dev/nvme1n1 ${GAME_ROOT}

#curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
#unzip awscliv2.zip
#sudo ./aws/install

sudo su - ${STEAMUSER}
export GAME_ROOT="/game"
export S3_REPO="s3://solidrust.net-repository"
export LOG_FILE="SolidRusT.log"
export LOGS="${HOME}/${LOG_FILE}"
touch ${LOGS}

echo "Sync repo for game server"
export S3_REPO="s3://solidrust.net-repository"
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
#TODO add database to /etc/hosts for local access
exit

sudo su - ${STEAMUSER}

update_repo game
# logout/login
update_repo game
update_server
update_mods
#update bashrc with gameroot mount
#pull default SRT crontab from repo
reboot
#configure discord hooks
#configure mapsize and seed/custom
update_repo game
update_mods
update_configs
#start_rust
update_permissions