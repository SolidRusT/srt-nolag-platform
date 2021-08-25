#!/bin/bash
# Default Debian 10 net-install ISO (from debian.org)
# - No Desktop environment
# - No Desktop Managers
# - Yes Web server
# - Yes print server
# - Yes SSH server
# - Yes Standard system utilities
## Or latest Buster 10 Marketplace AMI (from Debian, not AWS)
echo "This is not fully automated yet"
exit 1

# Update system
sudo apt update && sudo apt dist-upgrade -y
sudo apt install -y net-tools apache2 php7.3 libapache2-mod-php7.3 php7.3-common php7.3-mbstring php7.3-xmlrpc php7.3-soap php7.3-gd php7.3-xml php7.3-intl php7.3-mysql php7.3-cli php7.3-ldap php7.3-zip php7.3-curl curl software-properties-common unzip imagemagick php-imagick snapd htop python3-pip
mkdir -p ${HOME}/solidrust.net
mkdir -p ${HOME}/solidrust.net/server

NEW_NAME="radio"
echo ${NEW_NAME} | sudo tee /etc/hostname
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/hosts
echo "10.2.1.26 stream stream.solidrust.net" | sudo tee -a /etc/hosts
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/cloud/templates/hosts.debian.tmpl
echo "10.2.1.26 stream stream.solidrust.net" | sudo tee -a /etc/cloud/templates/hosts.debian.tmpl
sudo hostnamectl set-hostname ${NEW_NAME}

export S3_BACKUPS="s3://solidrust.net-backups"
export LOGS="${HOME}/SolidRusT.log"

## If not on AWS, configure your CLI maually
# aws configure  # add Key, secret, region (us-west-2), and output (text)
# aws s3 ls  # test permissions to s3
aws s3 sync --delete ${S3_BACKUPS}/servers/web/web ${HOME}/solidrust.net | tee -a ${LOGS}
aws s3 sync --delete ${S3_BACKUPS}/servers/web/apache2 ${HOME}/solidrust.net/server/apache2 | tee -a ${LOGS}
aws s3 sync --delete ${S3_BACKUPS}/servers/web/php ${HOME}/solidrust.net/server/php | tee -a ${LOGS}
sudo rm -rf /etc/apache2/*
sudo cp -R ${HOME}/solidrust.net/server/apache2 /etc/
sudo rm -rf /etc/php/*
sudo cp -R ${HOME}/solidrust.net/server/php /etc/
sudo mkdir -p /var/www/html
sudo ln -s ${HOME}/solidrust.net /var/www/html/




## Get a basic HTTP site working (not HTTPS)
# Delete unused apache config # sudo rm /etc/apache2/sites-enabled/000-default.conf
# customize /etc/apache2/sites-enabled/solidrust.conf (linked from /etc/apache2/sites-available)
#  - /etc/apache2/sites-enabled
#  - /etc/apache2/sites-available
sudo rm /etc/apache2/sites-enabled/*-le-ssl.conf
sudo rm /etc/apache2/sites-enabled/wordpress*

sudo mv solidrust.conf radio.conf


sudo systemctl restart apache2 # test and make sure the site is working on HTTP-only
sudo snap install --classic certbot
sudo ln -s /snap/bin/certbot /usr/bin/certbot
sudo snap install core; sudo snap refresh core
sudo certbot --apache

## ONLY ON DEV environments - QA/Stage/Prod use separate DB instance
# Install and configure mysql
sudo apt install default-mysql-server
sudo mysql_secure_installation
sudo mysql -u root -p
sudo apt install vsftpd
sudo cp /etc/vsftpd.conf /etc/vsftpd.conf.orig

# sudo nano /etc/vsftpd.conf
# - write_enable=YES
# - chroot_local_user=YES
# - user_sub_token=$USER
# - local_root=/home/$USER
# - pasv_min_port=40000
# - pasv_max_port=50000
# - userlist_enable=YES
# - userlist_file=/etc/vsftpd.userlist
# - userlist_deny=NO
# - allow_writeable_chroot=YES
# - local_umask=022

echo "${USER}" | sudo tee -a /etc/vsftpd.userlist
sudo systemctl restart vsftpd

# Instal RCON CLI
# From: https://github.com/gorcon/rcon-cli/releases/latest
wget https://github.com/gorcon/rcon-cli/releases/download/v0.9.1/rcon-0.9.1-amd64_linux.tar.gz
    tar xzvf rcon-0.9.1-amd64_linux.tar.gz
    mv rcon-0.9.1-amd64_linux/rcon ${HOME}/rcon
    rm -rf rcon-0.9.1-amd64_linux*

sudo apt install npm nodejs
