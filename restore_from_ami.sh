## THIS IS NOT A SCRIPT
# these are just shitty notes
# If installed from AMI
NEW_NAME="nine"
echo ${NEW_NAME} | sudo tee /etc/hostname
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/hosts
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/cloud/templates/hosts.debian.tmpl
sudo hostnamectl set-hostname ${NEW_NAME}

## if running
./rcon -c solidrust.net/rcon.yaml "server.save"
./rcon -c solidrust.net/rcon.yaml "server.writecfg"
./rcon -c solidrust.net/rcon.yaml "quit"

# Apply steam upates
steamcmd +login anonymous +force_install_dir ~/ +app_update 258550 +quit
steamcmd +login anonymous +force_install_dir ~/ +app_update 258550 validate +quit
wget https://umod.org/games/rust/download/develop -O Oxide.Rust.zip
unzip -o Oxide.Rust.zip
rm Oxide.Rust.zip
wget https://umod.org/extensions/discord/download -O ~/RustDedicated_Data/Managed/Oxide.Ext.Discord.dll



# Bootstrap
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
