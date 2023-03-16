Requires python3 and virtualenv

I also like to use pyenv

```bash
pyenv install 3.11.2
pyenv local 3.11.2
git clone https://github.com/SolidRusT/srt-nolag-platform.git
cd srt-nolag-platform
cd console
python -m venv venv
source venv/bin/activate
pip install --upgrade pip
pip install -r requirements.txt
```

copy the `config.ini.example` to `config.ini` and edit all the values.

once ready, test the execution manually with:

```bash
python3 oxide_uploader.py
```

stuff this up your ${STEAMUSER} contab using `crontab -e`

`0 * * * * /usr/bin/python3 /path/to/your/oxide_uploader.py >> /path/to/your/log_file.log 2>&1`