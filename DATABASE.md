# Suparious Rust Server

 - no lag gameplay
 - rust-vanilla-us-east-1
 - 

## Debian Buster 10 Overview

 - Configure and update Debian base server
 - Install steamcmd and steam login
 - Install Rust server and test
 - Install linuxGSM and add rust
 - Import config backup, or create new config
 - Run rust using service manager
 - Backup working config
 - Monitor running games

## Install Pre-Requisites

### Configure Debian package manager

```bash
sudo sed -i 's/main/main contrib non-free/g' /etc/apt/sources.list
sudo dpkg --add-architecture i386
sudo apt update
sudo apt -y dist-upgrade
```

### Configure server hostname

```bash
NEW_NAME="rust-data"
echo ${NEW_NAME} | sudo tee /etc/hostname
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/hosts
echo "127.0.0.1    ${NEW_NAME}" | sudo tee -a /etc/cloud/templates/hosts.debian.tmpl
sudo hostnamectl set-hostname ${NEW_NAME}
```

The next time you log in, you will see the new hostname.

### Install base environment

```bash
sudo apt-get -y install \
    git \
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
# OPTIONAL - to help the server realize the updates and hostname change
sudo apt install -f
sudo apt autoremove
sudo apt clean
sudo apt autoclean
sudo reboot
```

```bash
sudo apt install mariadb-server mariadb-client
sudo mysql
```

use `sudo mysql`

```sql
GRANT ALL ON *.* TO 'admin'@'localhost' IDENTIFIED BY 'Admin208' WITH GRANT OPTION;
FLUSH PRIVILEGES;
exit
```

login with this user you just made

```sql
create database PermissionGroupSync;
create database RustPlayers;
create database oxide;

GRANT ALL PRIVILEGES ON *.* TO 'modded'@'%' 
    IDENTIFIED BY 'Admin408' 
    WITH GRANT OPTION;
FLUSH PRIVILEGES;

CREATE TABLE IF NOT EXISTS oxide.ibn_table (
    `id` INT(11) NOT NULL AUTO_INCREMENT,
    `itransaction_id` VARCHAR(60) NOT NULL,
    `ipayerid` VARCHAR(60) NOT NULL,
    `iname` VARCHAR(60) NOT NULL,
    `iemail` VARCHAR(60) NOT NULL,
    `itransaction_date` DATETIME NOT NULL,
    `ipaymentstatus` VARCHAR(60) NOT NULL,
    `ieverything_else` TEXT NOT NULL,
    `item_name` VARCHAR(255) DEFAULT NULL,
    `claimed` INT(11) NOT NULL DEFAULT '0',
    `claim_date` DATETIME DEFAULT NULL,
    PRIMARY KEY (`id`)
)  ENGINE=MYISAM AUTO_INCREMENT=9;

CREATE DEFINER=`root`@`localhost` PROCEDURE oxide.claim_donation(IN email_address VARCHAR(255))
BEGIN

set email_address = REPLACE(email_address,'@@','@');

set @ID = (
select    IBN.id
from    oxide.ibn_table as IBN
where    IBN.iemail = email_address
        and IBN.claimed = 0
        and IBN.claim_date IS NULL
        and IBN.ipaymentstatus = "Completed"
ORDER BY IBN.itransaction_date DESC
LIMIT 1);

UPDATE oxide.ibn_table
SET    claimed = 1, claim_date = NOW()
WHERE id = @ID;

select    IBN.item_name
from    oxide.ibn_table as IBN
where    IBN.id = @ID;

END

/* GRANT ALL ON *.* TO 'modded'@'rust-east-mods' IDENTIFIED BY 'Admin408'; */
GRANT ALL ON *.* TO 'modded'@'18.233.110.188' IDENTIFIED BY 'Admin408';
/* GRANT ALL ON *.* TO 'modded'@'52.12.71.69' IDENTIFIED BY 'Admin408'; */
/* GRANT ALL ON *.* TO 'modded'@'10.11.0.28' IDENTIFIED BY 'Admin408'; */
/* GRANT ALL ON *.* TO 'modded'@'52.39.196.77' IDENTIFIED BY 'Admin408'; */
/* GRANT ALL ON *.* TO 'modded'@'ec2-52-39-196-77.us-west-2.compute.amazonaws.com' IDENTIFIED BY 'Admin408'; */


exit
```

admin@rust-data:~$ sudo nano /etc/mysql/mariadb.conf.d/50-server.cnf
admin@rust-data:~$ sudo systemctl restart mariadb
