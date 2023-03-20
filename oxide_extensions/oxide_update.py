import os
import tarfile
import zipfile
from configparser import ConfigParser
import boto3
import requests

# Read config.ini
config = ConfigParser()
config.read("config.ini")
GAME_ROOT = config.get("DEFAULT", "game_root")
S3_REPO = config.get("DEFAULT", "s3_repo")

# Configure S3 client
s3 = boto3.client("s3")

def download_and_extract_zip(s3_object_key, local_path):
    local_zip_path = os.path.join(GAME_ROOT, os.path.basename(s3_object_key))
    s3.download_file(S3_REPO, s3_object_key, local_zip_path)

    with zipfile.ZipFile(local_zip_path, "r") as zip_ref:
        zip_ref.extractall(GAME_ROOT)

    os.remove(local_zip_path)

def download_and_move_dll(url, local_path):
    response = requests.get(url)
    with open(local_path, "wb") as f:
        f.write(response.content)

def get_latest_rcon_version():
    response = requests.get("https://github.com/gorcon/rcon-cli/releases")
    return response.text.split("/releases/tag/v")[1].split('"')[0]

def download_and_extract_rcon(latest_rcon):
    rcon_file = f"rcon-{latest_rcon}-amd64_linux.tar.gz"
    rcon_url = f"https://github.com/gorcon/rcon-cli/releases/download/v{latest_rcon}/{rcon_file}"
    response = requests.get(rcon_url)

    with open(rcon_file, "wb") as f:
        f.write(response.content)

    with tarfile.open(rcon_file, "r:gz") as tar_ref:
        tar_ref.extractall()

    os.rename(f"rcon-{latest_rcon}-amd64_linux/rcon", os.path.join(GAME_ROOT, "rcon"))
    os.remove(rcon_file)
    os.rmdir(f"rcon-{latest_rcon}-amd64_linux")

# Main script execution
download_and_extract_zip("Oxide.Rust-linux.zip", GAME_ROOT)

managed_dir = os.path.join(GAME_ROOT, "RustDedicated_Data/Managed")
download_and_move_dll(f"{S3_REPO}/Oxide.Ext.Discord.dll", os.path.join(managed_dir, "Oxide.Ext.Discord.dll"))
download_and_move_dll("https://github.com/k1lly0u/Oxide.Ext.RustEdit/raw/master/Oxide.Ext.RustEdit.dll", os.path.join(managed_dir, "Oxide.Ext.RustEdit.dll"))

latest_rcon = get_latest_rcon_version()
download_and_extract_rcon(latest_rcon)
