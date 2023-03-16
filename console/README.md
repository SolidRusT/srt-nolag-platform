Requires `python3` and `virtualenv`. Some distributions you can install `python-is-python3` if there is still a legacy python 2 installed, otherwise you can replace the below `python` with `python3` if the command is not working. The same with `pip` for `pip3`.

I also like to use [pyenv](https://github.com/pyenv/pyenv) and `virtualenv`. These are not required, but are just better to use, so that we have more control over the python environment here.

```bash
git clone https://github.com/SolidRusT/srt-nolag-platform.git
cd srt-nolag-platform
cd console
pyenv install 3.11.2
pyenv local 3.11.2
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