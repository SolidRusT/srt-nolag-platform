
# Backups and Updates

Create some backup and sync jobs

```bash
# in you .bashrc profile: REGION="east"
# echo "" | sudo tee -a /etc/crontab
echo "# solidrust config sync " | sudo tee -a /etc/crontab
echo "27 *    * * *   modded  cd /home/modded && rsync -qr /etc/crontab solidrust.net/${REGION}" | sudo tee -a /etc/crontab
echo "28 *    * * *   modded  cd /home/modded && rsync -qr server/rust/cfg solidrust.net/${REGION}" | sudo tee -a /etc/crontab
echo "29 *    * * *   modded  cd /home/modded && rsync -qr oxide/ solidrust.net/${REGION}/oxide" | sudo tee -a /etc/crontab
echo "# solidrust backups " | sudo tee -a /etc/crontab
echo "52 *    * * *   modded  cd /home/modded && zip -q9ru /tmp/solidrust.zip solidrust.net" | sudo tee -a /etc/crontab
echo "59 *    * * *   root    aws s3 cp /tmp/solidrust.zip s3://suparious.com/solidrust-${REGION}-config.zip" | sudo tee -a /etc/crontab
echo "59 *    * * *   root    aws s3 sync --quiet --delete  backup s3://suparious.com/solidrust-${REGION}" | sudo tee -a /etc/crontab
```

### Primary server push out updates

```bash
echo "# Push Oxide to s3" | sudo tee -a /etc/crontab
echo "*/5 *    * * *   modded  aws s3 sync --quiet --delete oxide s3://suparious.com/oxide" | sudo tee -a /etc/crontab
```

### Secondary server(s)

```bash
echo "# Pull Oxide from s3" | sudo tee -a /etc/crontab
echo "*/10 *    * * *   modded  aws s3 sync --quiet --delete s3://suparious.com/oxide/plugins oxide/plugins" | sudo tee -a /etc/crontab
echo "*/30 *    * * *   modded  aws s3 cp --quiet s3://suparious.com/oxide/data/Kits.json oxide/data/" | sudo tee -a /etc/crontab
echo "*/22 *    * * *   modded  aws s3 cp --quiet s3://suparious.com/oxide/data/BetterChat.json oxide/data/" | sudo tee -a /etc/crontab
echo "*/11 *    * * *   modded  aws s3 cp --quiet s3://suparious.com/oxide/data/murdersPoint.json oxide/data/" | sudo tee -a /etc/crontab
echo "*/13 *    * * *   modded  aws s3 cp --quiet s3://suparious.com/oxide/data/CompoundOptions.json oxide/data/" | sudo tee -a /etc/crontab
echo "*/12 *    * * *   modded  aws s3 cp --quiet s3://suparious.com/oxide/data/StackSizeController.json oxide/data/" | sudo tee -a /etc/crontab
echo "*/15 *    * * *   modded  aws s3 cp --quiet s3://suparious.com/oxide/data/FancyDrop.json oxide/data/" | sudo tee -a /etc/crontab
# actual
echo "*/15 *    * * *   modded  aws s3 cp --quiet s3://suparious.com/oxide/config/PermissionGroupSync.json oxide/config/PermissionGroupSync.json" | sudo tee -a /etc/crontab
echo "*/15 *    * * *   modded  aws s3 cp --quiet s3://suparious.com/oxide/config/Skins.json oxide/config/Skins.json" | sudo tee -a /etc/crontab
echo "*/30 *    * * *   modded  aws s3 cp --quiet s3://suparious.com/oxide/data/Kits.json oxide/data/Kits.json" | sudo tee -a /etc/crontab


#(M) Economics.json
#(M) ServerRewards/*
```

## Updates - First Tuseday of each month

from the console

```
server.writecfg
server.backup
server.save
server.stop( string "Getting FacePunched monthy" )
```

Emergency restarting

```
#restart <seconds> <message>
restart 120 “Don’t wander too far off!”
```

Patching

```bash
cd ~/
steamcmd +login anonymous +force_install_dir ~/ +app_update 258550 validate +quit
wget https://umod.org/games/rust/download/develop -O Oxide.Rust.zip
unzip -o Oxide.Rust.zip
rm Oxide.Rust.zip
wget https://umod.org/extensions/discord/download -O ~/RustDedicated_Data/Managed/Oxide.Ext.Discord.dll
```


ssh-keygen -b 4096
git config user.email "smoke@techfusion.ca"
git config user.name "SmokeQc"

# Create update
aws s3 sync --quiet s3://suparious.com/oxide oxide

# Download updates
aws s3 sync --quiet s3://suparious.com/oxide/plugins oxide/plugins
aws s3 sync --quiet s3://suparious.com/oxide/config oxide/config