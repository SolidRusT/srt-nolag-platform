#!/bin/bash

echo "pile of hot garbage, stoner notes"

## Shit notes
## WIP




## In the db: 
#create database solidrust_players;
## and then somethign like: (but cooler)
#GRANT ALL PRIVILEGES ON *.* TO 'modded'@'%' 
#    IDENTIFIED BY 'Admin408' 
#    WITH GRANT OPTION;
#FLUSH PRIVILEGES;

#cat oxide/config/PermissionGroupSync.json

#
# {
#  "ServerId": "west",
#  "PollIntervalSeconds": 120,
#  "DatabaseConfiguration": {
#    "Host": "data.solidrust.net",
#    "Port": 3306,
#    "Username": "modded",
#    "Password": "Admin408",
#    "Database": "solidrust_lcy"
#  },
#  "GroupPermissions": [
#    {
#      "CommandName": "usermod.verified",
#      "GroupName": "verified",
#      "ExtendedPermissionHandling": true,
#      "PermissionUse": true,
#      "ProtectedGroup": true,
#      "OverrideServerIdCheck": true,
#      "GroupsRemove": [
#        "admin",
#        "dev",
#        "GM",
#        "vip"
#      ],
#      "PermissionsOxide": [],
#      "AdditionalCommands": []
#    },
#    {
#      "CommandName": "usermod.vip",
#      "GroupName": "vip",
#      "ExtendedPermissionHandling": true,
#      "PermissionUse": true,
#      "ProtectedGroup": true,
#      "OverrideServerIdCheck": true,
#      "GroupsRemove": [
#        "admin",
#        "dev",
#        "moderator",
#        "vip"
#      ],
#      "PermissionsOxide": [],
#      "AdditionalCommands": []
#    },
#    {
#      "CommandName": "usermod.dev",
#      "GroupName": "dev",
#      "ExtendedPermissionHandling": true,
#      "PermissionUse": true,
#      "ProtectedGroup": false,
#      "OverrideServerIdCheck": true,
#      "GroupsRemove": [
#        "admin",
#        "gm"
#      ],
#      "PermissionsOxide": [],
#      "AdditionalCommands": []
#    },
#    {
#      "CommandName": "usermod.gm",
#      "GroupName": "gm",
#      "ExtendedPermissionHandling": true,
#      "PermissionUse": true,
#      "ProtectedGroup": false,
#      "OverrideServerIdCheck": true,
#      "GroupsRemove": [
#        "admin",
#        "dev"
#      ],
#      "PermissionsRust": 1,
#      "PermissionsOxide": [],
#      "AdditionalCommands": []
#    },
#    {
#      "CommandName": "usermod.admin",
#      "GroupName": "admin",
#      "ExtendedPermissionHandling": true,
#      "PermissionUse": true,
#      "ProtectedGroup": false,
#      "OverrideServerIdCheck": true,
#      "GroupsRemove": [
#        "gm",
#        "dev"
#      ],
#      "PermissionsRust": 2,
#      "PermissionsOxide": [],
#      "AdditionalCommands": []
#    }
#  ]
#}



Group nesting
oxide.group parent verified default
oxide.group parent dev default
oxide.group parent vip default
oxide.group parent gm default
oxide.group parent admin default


Users and groups
usermod.verified add 76561198886543733 (SmokeQc)
usermod.verified add 76561199135759930 (Ratchet)
usermod.verified add 76561198421090963 (Hannaht56)
usermod.verified add 76561198852895608 (WeirdAl)
usermod.verified add 76561198024774727 (Suparious)

usermod.vip add 76561198852895608 (WeirdAl)
usermod.vip add 76561198421090963 (Hannaht56)
usermod.vip add 76561198024774727 (Suparious)
#usermod.vip add 76561198886543733 (SmokeQc)
#usermod.vip add 76561199135759930 (Ratchet)

usermod.dev add 76561198886543733 (SmokeQc)

usermod.gm 76561198206550912 (ThePastaMasta)

usermod.admin add 76561198024774727 (Suparious)

ownerid "76561198024774727" "Suparious" “Pays the bills”



ssh-keygen -b 4096
cat ${HOME}/.ssh/id_rsa.pub

git config --global user.email "smoke@solidrust.net"
git config --global user.name "SmokeQc"

git clone git@github.com:suparious/solidrust.net.git
echo "11 *    * * *   ${STEAMUSER}  ${HOME}/solidrust.net/backup.sh" | sudo tee -a /etc/crontab












#https://just-wiped.net/rust-maps/procedural-map-3500-973546

rsync -ar --exclude 'Bundles' ${HOME}  /game/

steamcmd +login anonymous +force_install_dir /game/${USER} +app_update 258550 validate +quit

sudo chown -R ${USER}:${USER} /game
sudo chown -R ${USER}:${USER} ${HOME}

exit


# Login as the game service user
sudo su - ${STEAMUSER}

# Instal RCON CLI
# From: https://github.com/gorcon/rcon-cli/releases/latest
wget https://github.com/gorcon/rcon-cli/releases/download/v0.9.0/rcon-0.9.0-amd64_linux.tar.gz
tar xzvf rcon-0.9.0-amd64_linux.tar.gz
mv rcon-0.9.0-amd64_linux/rcon* .
rm -rf rcon-0.9.0-amd64_linux* rcon.yaml

