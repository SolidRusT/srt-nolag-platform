The Python script (package_watcher.py) watches a folder for new C# packages, parses the required information, stores it in the MySQL database, and uploads the files to Amazon S3.

## to install
Ubuntu or Debian:
```bash
sudo apt-get update
sudo apt-get install -y python3-pip python3-venv
```
Arch Linux or SolidRusT-OS
```bash
sudo pacman -Syu
sudo pacman -S python-pip python-venv
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
python package_watcher.py &
deactivate
```

## to uninstall

```bash
rm -rf venv
rm -rf config.ini
```