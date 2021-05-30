#!/bin/bash
sudo sed -i 's/main/main contrib non-free/g' /etc/apt/sources.list
sudo dpkg --add-architecture i386
sudo apt update
sudo apt -y dist-upgrade

NEW_NAME="one"
echo ${NEW_NAME} | sudo tee /etc/hostname
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/hosts
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/cloud/templates/hosts.debian.tmpl
sudo hostnamectl set-hostname ${NEW_NAME}

echo "en_US.UTF-8 UTF-8" | sudo tee -a /etc/locale.gen
sudo locale-gen

sudo dpkg-reconfigure tzdata
#America/Los_Angeles or America/New_York

sudo dd if=/dev/zero of=/swapfile bs=128M count=128
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
sudo swapon -s
echo "/swapfile swap swap defaults 0 0" | sudo tee -a /etc/fstab

sudo apt-get -y install \
    git \
    apt-transport-https \
    ca-certificates \
    curl \
    wget \
    htop \
    gnupg-agent \
    gnupg2 \
    mailutils \
    software-properties-common \
    screen \
    lib32gcc1 \
    libsdl2-2.0-0:i386 \
    libsdl2* \
    lib32stdc++6 \
    libstdc++6:i386 \
    zip \
    unzip \
    bzip2 \
    file \
    binutils \
    rsync \
    bc \
    jq \
    tmux \
    netcat \
    lib32z1 \
    libgdiplus \
    mariadb-client \
    python \
    python3 \
    golang \
    python3-pip \
    util-linux \
    bsdmainutils \
    build-essential

echo "PATH=\$PATH:$HOME/.local/bin" >> ".bashrc"

# freshen-up the Debian repo
sudo apt -y install -f
sudo apt -y autoremove
sudo apt clean
sudo apt autoclean

#!/bin/bash
STEAMUSER="root"

# Install Steam client
sudo apt -y install steamcmd

# Install statistics server
curl -sL https://deb.nodesource.com/setup_12.x | sudo -E bash -
sudo apt update && sudo apt install -y nodejs
sudo npm install gamedig -g

# Install xfs for the ephemeral disk
sudo apt -y install xfsprogs

echo "0 *    * * *   ${USER}  /usr/sbin/logrotate -f ${HOME}/solidrust.net/defaults/logrotate.conf --state ${HOME}/logrotate-state" | sudo tee -a /etc/crontab


# Reboot to load the kernel storage module for XFS
sudo reboot
