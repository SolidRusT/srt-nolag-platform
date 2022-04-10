#!/bin/bash
sudo sed -i 's/main/main contrib non-free/g' /etc/apt/sources.list
# sudo add-apt-repository universe
# sudo add-apt-repository multiverse
# sudo add-apt-repository restricted
sudo dpkg --add-architecture i386
sudo apt update
sudo apt -y dist-upgrade

NEW_NAME="nine"
echo ${NEW_NAME} | sudo tee /etc/hostname
echo "127.0.0.1    ${NEW_NAME} ${NEW_NAME}.solidrust.net" | sudo tee -a /etc/hosts /etc/cloud/templates/hosts.debian.tmpl
sudo hostnamectl set-hostname ${NEW_NAME}

echo "en_US.UTF-8 UTF-8" | sudo tee -a /etc/locale.gen
sudo locale-gen

sudo dpkg-reconfigure tzdata
# America/Los_Angeles or America/New_York or America/Vancouver

sudo dd if=/dev/zero of=/swapfile bs=128M count=128
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
sudo swapon -s
echo "/swapfile swap swap defaults 0 0" | sudo tee -a /etc/fstab # TODO move this to NVMe drive

# freshen-up the Debian repo
sudo apt -y install -f
sudo apt -y autoremove
sudo apt clean
sudo apt autoclean
# reboot # Recommended

# Installed required dependencies
## Buster required lib32gcc1 instead of lib32gcc-s1
## Bullseye required lib32gcc-s1 instead of lib32gcc1
## Works perfect with Ubuntu 20.04
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
    lib32gcc-s1 \
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
    build-essential \
    xfsprogs \
    steamcmd

sudo reboot