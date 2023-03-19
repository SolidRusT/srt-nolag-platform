- The Flask web application (package_browser.py) queries the MySQL database and displays the package repository contents.
- NGINX is configured to serve the Flask web application.

## to install

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
create a new `config.ini` file using the provided example, and modify it to match your environment.
```bash
cp config.ini.example config.ini
nano config.ini
```
create an isolated userspace python environment, so the app doesn't depend on system installed package versions
```bash
python -m venv venv
source venv/bin/activate
pip install --upgrade pip
pip install -r requirements.txt
deactivate
```

## to run

```bash
source venv/bin/activate
python package_browser.py &
deactivate
```

## to uninstall

```bash
rm -rf venv
rm -rf config.ini
```

### using NGINX

an example of how to setup `/etc/nginx/sites-available/package_repo`:
```conf
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

Symlink your config to enable it, and restart NGINX. This is the way.
```bash
sudo ln -s /etc/nginx/sites-available/package_repo /etc/nginx/sites-enabled/
sudo systemctl restart nginx
```