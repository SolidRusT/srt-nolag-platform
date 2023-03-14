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
S3_REPO="s3://your-srt-no-lag-platform-config-repository"
LOG_FILE="SolidRusT.log"
LOGS="/root/SolidRusT.log"
```

If you just changed that file, source it to initialize the new variables.

```bash
source /etc/environment
```

