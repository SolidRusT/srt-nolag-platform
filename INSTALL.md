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