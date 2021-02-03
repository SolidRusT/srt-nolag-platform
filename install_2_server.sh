#!/bin/bash
STEAMUSER="modded"

# Install Steam client
sudo apt -y install steamcmd

# Install statistics server
curl -sL https://deb.nodesource.com/setup_12.x | sudo -E bash -
sudo apt update && sudo apt install -y nodejs
sudo npm install gamedig -g

# Install AWS CLI for admin user
pip3 install awscli

# Create the game service user
echo "export STEAMUSER=\"${STEAMUSER}\"" >> ~/.bashrc
# sudo adduser modded --disabled-password --quiet
sudo adduser ${STEAMUSER} --disabled-password --quiet
sudo su - ${STEAMUSER}

# Install AWS CLI for modded user
pip3 install awscli
echo "PATH=\$PATH:$HOME/.local/bin" >> ".bashrc"

exit
sudo su - ${STEAMUSER}

# Instal RCON CLI
# From: https://github.com/gorcon/rcon-cli/releases/latest
wget https://github.com/gorcon/rcon-cli/releases/download/v0.9.0/rcon-0.9.0-amd64_linux.tar.gz
tar xzvf rcon-0.9.0-amd64_linux.tar.gz
mv rcon-0.9.0-amd64_linux/rcon* .
rm -rf rcon-0.9.0-amd64_linux* rcon.yaml

steamcmd +login anonymous +force_install_dir ~/ +app_update 258550 +quit
steamcmd +login anonymous +force_install_dir ~/ +app_update 258550 validate +quit

ssh-keygen -b 4096
cat /home/modded/.ssh/id_rsa.pub

git config --global user.email "smoke@solidrust.net"
git config --global user.name "SmokeQc"

git clone git@github.com:suparious/solidrust.net.git
echo "7 *    * * *   modded  /home/modded/solidrust.net/backup.sh" | sudo tee -a /etc/crontab

