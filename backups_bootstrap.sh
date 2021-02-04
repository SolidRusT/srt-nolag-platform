NEW_NAME="two"
echo ${NEW_NAME} | sudo tee /etc/hostname
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/hosts
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/cloud/templates/hosts.debian.tmpl
sudo hostnamectl set-hostname ${NEW_NAME}


# Bootstrap
export MYNAME=$(hostname)
export DEST_S3="s3://solidrust.net-backups/${MYNAME}"
export INSTALL_DIR="/home/modded"
mkdir -p ${INSTALL_DIR}/solidrust.net/${MYNAME}/server/solidrust/cfg
rsync -r ${INSTALL_DIR}/server/solidrust/cfg ${INSTALL_DIR}/solidrust.net/${MYNAME}/server/solidrust/cfg
mkdir -p ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/config
rsync -r ${INSTALL_DIR}/oxide/config/ ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/config
mkdir -p ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/data

rsync -r ${INSTALL_DIR}/oxide/data/ ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/data
rsync -r ${INSTALL_DIR}/server/solidrust/cfg/ ${INSTALL_DIR}/solidrust.net/${MYNAME}/server/solidrust/cfg
rsync -r ${INSTALL_DIR}/oxide/config/ ${INSTALL_DIR}/solidrust.net/${MYNAME}/oxide/config
