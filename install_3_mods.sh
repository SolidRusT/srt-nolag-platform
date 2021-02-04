#!/bin/bash
cd $HOME

wget https://umod.org/games/rust/download/develop -O Oxide.Rust.zip
unzip -o Oxide.Rust.zip
rm Oxide.Rust.zip

wget https://umod.org/extensions/discord/download -O ~/RustDedicated_Data/Managed/Oxide.Ext.Discord.dll


mkdir -p oxide/plugins && cd oxide/plugins

wget https://umod.org/plugins/PermissionGroupSync.cs
https://umod.org/plugins/DiscordWelcomer.cs


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