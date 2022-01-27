#!/bin/bash
# Default Debian 11 net-install ISO (from debian.org)
# - No Desktop environment
# - No Desktop Managers
# - Yes Web server
# - Yes print server
# - Yes SSH server
# - Yes Standard system utilities
## Or latest Buster 11 Marketplace AMI (from Debian, not AWS)
echo "This is not fully automated yet"
exit 1

# Update system
sudo apt update && sudo apt dist-upgrade -y
# Install nodejs services
sudo apt-get -y install \
    net-tools \
    imagemagick \
    python3-pip \
    npm \
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
# Configure server hostname
NEW_NAME="bots"
echo ${NEW_NAME} | sudo tee /etc/hostname
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/hosts
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/cloud/templates/hosts.debian.tmpl
sudo hostnamectl set-hostname ${NEW_NAME}
# reboot  (recommended)

## Install SRT Release manager
# pip3 install awscli
# export PATH="${PATH}:${HOME}/.local/bin"
mkdir -p ${HOME}/solidrust.net
mkdir -p ${HOME}/solidrust.net/server
export S3_REPO="s3://solidrust.net-repository"
export LOGS="${HOME}/SolidRusT.log"
## If not on AWS, configure your CLI maually
# aws configure  # add Key, secret, region (us-west-2), and output (text)
# aws s3 ls  # test permissions to s3
echo "Sync repo for bots server"
    mkdir -p ${HOME}/solidrust.net
    aws s3 sync --size-only --delete \
      --exclude "web/maps/*" \
      --exclude "defaults/oxide/*" \
      --exclude "defaults/cfg/*" \
      --exclude "defaults/console/*" \
      --exclude "defaults/radio/*" \
      --exclude "defaults/procedural-maps/*" \
      --exclude "defaults/database/*" \
      --exclude "servers/*" \
      --include "servers/bots/*" \
      ${S3_REPO} ${HOME}/solidrust.net | tee -a ${LOGS}

cp -R ${HOME}/solidrust.net/apps/srt-link-bot ${HOME}/run/srt-link-bot

# Instal RCON CLI
# From: https://github.com/gorcon/rcon-cli/releases/latest
wget https://github.com/gorcon/rcon-cli/releases/download/v0.9.1/rcon-0.9.1-amd64_linux.tar.gz
    tar xzvf rcon-0.9.1-amd64_linux.tar.gz
    mv rcon-0.9.1-amd64_linux/rcon ${HOME}/rcon
    rm -rf rcon-0.9.1-amd64_linux*

sudo apt install npm nodejs
