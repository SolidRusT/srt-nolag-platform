Ubuntu or Debian:
```bash
sudo apt-get update
sudo apt-get install -y python3-pip nginx python3-venv
```
Arch Linux or SolidRusT-OS
```bash
sudo pacman -Syu
sudo pacman -S python-pip nginx python-venv
```
Clone this repo
```bash
# Optional, organize repos in a foler
cd ~ && mkdir repos && cd repos
# download from git
git clone https://github.com/SolidRusT/srt-nolag-platform.git
cd srt-nolag-platform/oxide_library
```


```bash
python -m venv venv
source venv/bin/activate
pip install --upgrade pip
pip install -r requirements.txt
```

Copy the example config to your own file called `config.ini`. Edit the placeholders with your real values.

```bash
cp config.ini.example config.ini
```


Make sure this SQl is on your database server

```sql
CREATE DATABASE package_repo;
USE package_repo;

CREATE TABLE packages (
    id INT AUTO_INCREMENT PRIMARY KEY,
    package_name VARCHAR(255),
    package_author VARCHAR(255),
    package_version VARCHAR(255),
    package_description TEXT,
    timestamp DATETIME
);
```

create a user for this app

```sql
CREATE USER 'package_repo'@'%' IDENTIFIED BY 'SomePassword123';
GRANT ALL PRIVILEGES ON *.* TO 'package_repo'@'%';
FLUSH PRIVILEGES;
```

/etc/nginx/sites-available/package_repo:
```
server {
    listen 80;
    server_name your_domain.com;

    location / {
        proxy_pass http://127.0.0.1:8000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}
```

```
sudo ln -s /etc/nginx/sites-available/package_repo /etc/nginx/sites-enabled/
sudo systemctl restart nginx
```

- The Python script (package_watcher.py) watches a folder for new C# packages, parses the required information, stores it in the MySQL database, and uploads the files to Amazon S3.
- The Flask web application (app.py) queries the MySQL database and displays the package repository contents.
- NGINX is configured to serve the Flask web application.

To get started, run the Python script and the Flask application on the server:

```bash
source venv/bin/activate
python3 package_watcher.py /path/to/watch/folder &
python3 app.py &
```

If you are on windows, then for the virtualenv part instead use:
```bash
venv\Scripts\activate
```

To exit the virtualenv

```bash
deactivate
```