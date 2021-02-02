
# Backups and Updates

Create some backup and sync jobs

```bash
sudo apt -y install golang
wget https://github.com/gorcon/rcon-cli/releases/download/v0.9.0/rcon-0.9.0-amd64_linux.tar.gz
tar xzvf rcon-0.9.0-amd64_linux.tar.gz
mv rcon-0.9.0-amd64_linux/rcon* .
rm -rf rcon-0.9.0-amd64_linux*
/sbin/ifconfig
```

```bash
# in you .bashrc profile: REGION="east"
echo "# write from memory" | sudo tee -a /etc/crontab
echo "02 *    * * *   modded  cd /home/modded && ./rcon -c rcon.yaml server.writecfg" | sudo tee -a /etc/crontab
echo "# backup server data" | sudo tee -a /etc/crontab
echo "03 *    * * *   modded  cd /home/modded && ./rcon -c rcon.yaml server.backup" | sudo tee -a /etc/crontab
echo "04 *    * * *   modded  aws s3 sync --quiet --delete  /home/modded/backup s3://suparious.com/backup/${REGION}" | sudo tee -a /etc/crontab
echo "# save the map" | sudo tee -a /etc/crontab
echo "0/5 *    * * *   modded  cd /home/modded && ./rcon -c rcon.yaml server.save" | sudo tee -a /etc/crontab
echo "0/5 *    * * *   modded  cd /home/modded &&  ./rcon -c rcon.yaml \"o.reload WipeInfoApi\"" | sudo tee -a /etc/crontab 
echo "0/5 *    * * *   modded  cd /home/modded &&  ./rcon -c rcon.yaml \"o.reload MagicWipePanel\"" | sudo tee -a /etc/crontab
echo "# backup game mods" | sudo tee -a /etc/crontab
echo "*/30 *    * * *   modded  aws s3 sync --delete --quiet /home/modded/oxide s3://suparious.com/backup/${REGION}/oxide" | sudo tee -a /etc/crontab
echo "# solidrust config sync " | sudo tee -a /etc/crontab
echo "27 *    * * *   modded rsync -qr /etc/crontab /home/modded/solidrust.net/${REGION}" | sudo tee -a /etc/crontab
echo "28 *    * * *   modded rsync -qr /home/modded/server/rust/cfg /home/modded/solidrust.net/${REGION}" | sudo tee -a /etc/crontab
echo "29 *    * * *   modded rsync -qr /home/modded/oxide/ /home/modded/solidrust.net/${REGION}/oxide" | sudo tee -a /etc/crontab
echo "# solidrust backup export " | sudo tee -a /etc/crontab
echo "52 *    * * *   modded  cd /home/modded && zip -q9ru /tmp/solidrust.zip solidrust.net" | sudo tee -a /etc/crontab
echo "59 *    * * *   modded  aws s3 cp /tmp/solidrust.zip s3://suparious.com/backup/${REGION}/config.zip" | sudo tee -a /etc/crontab
```

### Secondary-only (east)

aws s3 sync --delete s3://suparious.com/backup/west/oxide/config/MagicPanel /home/modded/oxide/config/MagicPanel



```bash
SOURCE_S3="s3://suparious.com/backup/west"

CONFIGS=(
oxide/config/Backpacks.json
oxide/data/Kits.json
oxide/data/BetterChat.json
oxide/data/murdersPoint.json
oxide/data/CompoundOptions.json
oxide/data/StackSizeController.json
oxide/data/FancyDrop.json
oxide/config/Skins.json
oxide/config/PermissionGroupSync.json

)

for config in ${CONFIGS[@]}; do
    echo "aws s3 cp --quiet ${SOURCE_S3}/$config $config"
done



#(M) Economics.json
#(M) ServerRewards/*
```

oxide/config/DiscordServerStats.json

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