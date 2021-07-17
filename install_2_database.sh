# Configure Debian package manager
sudo sed -i 's/main/main contrib non-free/g' /etc/apt/sources.list
sudo dpkg --add-architecture i386
sudo apt update
sudo apt -y dist-upgrade

# Configure server hostname
NEW_NAME="rust-data"
echo ${NEW_NAME} | sudo tee /etc/hostname
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/hosts
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/cloud/templates/hosts.debian.tmpl
sudo hostnamectl set-hostname ${NEW_NAME}

# Install base environment
sudo apt-get -y install \
    git \
    apt-transport-https \
    ca-certificates \
    curl \
    wget \
    htop \
    gnupg-agent \
    gnupg2 \
    software-properties-common \
    screen \
    unzip \
    rsync \
    linux-headers-$(uname -r) \
    build-essential
# OPTIONAL - to help the server realize the updates and hostname change
sudo apt install -f
sudo apt autoremove
sudo apt clean
sudo apt autoclean
sudo reboot


sudo apt install mariadb-server mariadb-client
sudo mysql

# import Schema and permissions from defaults/database/*.sql
#mysql < defaults/database/*.sql -- or some shit

# Tweak the config a bit, then restart
sudo nano /etc/mysql/mariadb.conf.d/50-server.cnf
sudo systemctl restart mariadb



echo "50 *    * * *   ${USER}  /usr/sbin/logrotate ${HOME}/solidrust.net/defaults/database/logrotate.conf --state ${HOME}/logrotate-state" | sudo tee -a /etc/crontab

source ${HOME}/solidrust.net/defaults/env_vars.sh
echo "3 *    * * *   ${USER} \
    rm -rf ${GITHUB_ROOT}; \
    mkdir -p ${GITHUB_ROOT}; \
    aws s3 sync --only-show-errors --delete ${S3_BACKUPS}/repo ${GITHUB_ROOT}; \
    chmod +x ${SERVER_GLOBAL}/*.sh" \
    | sudo tee -a /etc/crontab


#sudo su -
#mysql
#create database srt_web_auth;
#GRANT ALL PRIVILEGES ON *.* TO 'srt_sl_lcy'@'%' 
#    IDENTIFIED BY 'lcy_402' 
#    WITH GRANT OPTION;
#FLUSH PRIVILEGES;
### Import verification.sql now