On the Database server:

```sql
CREATE USER 'some_user'@'%' IDENTIFIED BY 'SomePassword123';
GRANT ALL PRIVILEGES ON *.* TO 'some_user'@'%';
FLUSH PRIVILEGES;
```

test connections from the app servers

`mysql -u some_user -p -h some.database.server.xyz`

pull down the `srt-nolag-platform` to your local machine or admin console

```bash
sudo su - ${STEAMUSER}
cd ~
git clone https://github.com/SolidRusT/srt-nolag-platform.git
```

configure global environment variables (already done if you have deployed solidrust.net terraform), but check anyways.

It is recommended to do this on both the game server and your local console(s).

/etc/environment:
```
DEBIAN_FRONTEND=noninteractive
STEAMUSER="root"
GAME_ROOT="/game"
S3_REPO="s3://<your_own_nolag_platform_bucket>"
LOGS="/var/log/SolidRusT.log"
NOLAG_REPO="https://github.com/SolidRusT/srt-nolag-platform.git"
PLATFORM_ROOT="/root/srt-nolag-platform"
RUST_RCON_ADMIN="<your_rcon_password>"
RUST_IDENTITY="solidrust"
RUST_AVATAR="https://solidrust.net/images/SoldRust_Logo.png"
RUST_SERVER_PORT="28015"
RUST_RCON_PORT="28016"
RUST_APP_PORT="28082"
WORLD_SIZE="2700"
LEVEL="Procedural Map"  # ignored if using custom maps
CUSTOM_MAP="disabled"   # enabled / disabled
CUSTOM_MAP_URL=""       # only if CUSTOM_MAP is "enabled"
```

If you just changed that file, source it to initialize the new variables.

```bash
source /etc/environment
```

individual hosts vars

```
export SQL_HOST=""
export SQL_USER=""
export SQL_PASS=""
```


## CPU throttling disable
Only works on some instances see [Supported EC2 instance types](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/processor_state_control.html)

```bash
sudo apt install linux-cpupower cpufrequtils
```

edit the `/etc/default/cpufrequtils` and set `GOVERNOR="performance"`.

then restart the daemon

`sudo systemctl restart cpufrequtils`

check the processor status with this:

`cpufreq-info` or `sudo cpupower frequency-info`

## cleanup APT

sudo apt -y install -f
sudo apt -y autoremove
sudo apt clean
sudo apt autoclean

