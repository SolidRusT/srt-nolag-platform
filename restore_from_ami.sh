## THIS IS NOT A SCRIPT
# these are just shitty notes

# Set the relevant hostname and logout for the changes to properly affect your console
NEW_NAME="data"
echo ${NEW_NAME} | sudo tee /etc/hostname
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/hosts
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/cloud/templates/hosts.debian.tmpl
sudo hostnamectl set-hostname ${NEW_NAME}

# Data Node: Check that everything is already working, easy-mode here
sudo systemctl status mariadb
cat /etc/hosts
netstat -an | grep 3306

# Console Node:



# Game Node: if game service is still running
./rcon -c solidrust.net/rcon.yaml "server.save"
./rcon -c solidrust.net/rcon.yaml "server.writecfg"
./rcon -c solidrust.net/rcon.yaml "quit"

# Game Node: Apply steam upates
steamcmd +login anonymous +force_install_dir ~/ +app_update 258550 +quit
steamcmd +login anonymous +force_install_dir ~/ +app_update 258550 validate +quit
wget https://umod.org/games/rust/download/develop -O Oxide.Rust.zip
unzip -o Oxide.Rust.zip
rm Oxide.Rust.zip
wget https://umod.org/extensions/discord/download -O ~/RustDedicated_Data/Managed/Oxide.Ext.Discord.dll



# Game Node: Bootstrap
sudo su - ${STEAMUSER}
export MYNAME=$(hostname)
export DEST_S3="s3://solidrust.net-backups/${MYNAME}"
export INSTALL_DIR=${HOME}
cd ${INSTALL_DIR}/solidrust.net && git pull
mkdir -p ${INSTALL_DIR}/oxide/plugins
mkdir -p ${INSTALL_DIR}/oxide/data
mkdir -p ${INSTALL_DIR}/oxide/config

mkdir -p ${INSTALL_DIR}/solidrust.net/${MYNAME}/server/solidrust/cfg
rsync -r ${INSTALL_DIR}/server/solidrust/cfg ${INSTALL_DIR}/solidrust.net/${MYNAME}/server/solidrust/cfg
mkdir -p ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/config
rsync -r ${INSTALL_DIR}/oxide/config/ ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/config
mkdir -p ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/data

rsync -r ${INSTALL_DIR}/oxide/data/ ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/data
rsync -r ${INSTALL_DIR}/server/solidrust/cfg/ ${INSTALL_DIR}/solidrust.net/${MYNAME}/server/solidrust/cfg
rsync -r ${INSTALL_DIR}/oxide/config/ ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/config


# TODO: Figure out inventory sync
#(M) Backpacks/*

