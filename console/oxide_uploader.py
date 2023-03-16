import os
import tempfile
import requests
from configparser import ConfigParser
from github import Github
from boto3.session import Session

LAST_VERSION_FILE = "last_downloaded_version.txt"

def read_last_downloaded_version() -> str:
    if os.path.isfile(LAST_VERSION_FILE):
        with open(LAST_VERSION_FILE, "r") as f:
            return f.read().strip()
    return None

def write_last_downloaded_version(version: str):
    with open(LAST_VERSION_FILE, "w") as f:
        f.write(version)

def download_latest_release(repo_name: str, asset_name: str, access_token: str) -> (str, str):
    github = Github(access_token)
    repo = github.get_repo(repo_name)
    releases = repo.get_releases()

    for release in releases:
        for asset in release.get_assets():
            if asset_name in asset.name:
                response = requests.get(asset.browser_download_url, stream=True)
                with tempfile.NamedTemporaryFile(delete=False, suffix=".zip") as temp_file:
                    for chunk in response.iter_content(chunk_size=8192):
                        temp_file.write(chunk)
                return temp_file.name, release.tag_name
    return None, None

def upload_to_s3(file_path: str, bucket: str, object_key: str, aws_credentials):
    access_key, secret_key, region = aws_credentials
    session = Session(aws_access_key_id=access_key, aws_secret_access_key=secret_key, region_name=region)
    s3 = session.resource('s3')
    s3.Bucket(bucket).upload_file(file_path, object_key)

def main():
    # Load configuration
    config = ConfigParser()
    config.read("config.ini")

    repo_name = "OxideMod/Oxide.Rust"
    asset_name = "Oxide.Rust-linux.zip"
    s3_bucket = config.get("s3", "bucket")
    s3_object_key = config.get("s3", "object_key")
    github_access_token = config.get("github", "access_token")
    aws_credentials = (
        config.get("aws", "access_key_id"),
        config.get("aws", "secret_access_key"),
        config.get("aws", "region"),
    )

    last_downloaded_version = read_last_downloaded_version()

    # Download latest release
    downloaded_file, release_version = download_latest_release(repo_name, asset_name, github_access_token)
    if downloaded_file:
        if release_version != last_downloaded_version:
            # Upload to S3
            upload_to_s3(downloaded_file, s3_bucket, s3_object_key, aws_credentials)
            # Update last_downloaded_version
            write_last_downloaded_version(release_version)
            # Clean up
            os.remove(downloaded_file)
            print(f"Uploaded {asset_name} to S3 bucket {s3_bucket}")
        else:
            print(f"Latest version ({release_version}) is already downloaded.")
            os.remove(downloaded_file)
    else:
        print("Asset not found.")

if __name__ == "__main__":
    main()
