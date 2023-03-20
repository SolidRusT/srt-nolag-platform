import os
import zipfile
import tempfile
import requests
import mysql.connector
from configparser import ConfigParser
from github import Github
from boto3.session import Session

def get_mysql_connection(config):
    return mysql.connector.connect(
        host=config.get("mysql", "host"),
        user=config.get("mysql", "user"),
        password=config.get("mysql", "password"),
        database=config.get("mysql", "database"),
    )

def read_last_downloaded_version(conn) -> str:
    cursor = conn.cursor()
    cursor.execute("SELECT version FROM oxide_ext_discord LIMIT 1")
    result = cursor.fetchone()
    return result[0] if result else None

def write_last_downloaded_version(conn, version: str):
    cursor = conn.cursor()
    cursor.execute("UPDATE oxide_ext_discord SET version = %s", (version,))
    conn.commit()

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

    repo_name = "dassjosh/Oxide.Ext.Discord"
    asset_name = "Oxide.Ext.Discord-2.1.9.zip"
    s3_bucket = config.get("s3", "bucket")
    s3_object_key = config.get("s3", "object_key")
    github_access_token = config.get("github", "access_token")
    aws_credentials = (
        config.get("aws", "access_key_id"),
        config.get("aws", "secret_access_key"),
        config.get("aws", "region"),
    )

    # Connect to MySQL
    conn = get_mysql_connection(config)

    last_downloaded_version = read_last_downloaded_version(conn)

    # Download latest release
    downloaded_file, release_version = download_latest_release(repo_name, asset_name, github_access_token)
    if downloaded_file:
        if release_version != last_downloaded_version:
            # Upload to S3
            upload_to_s3(downloaded_file, s3_bucket, s3_object_key, aws_credentials)
            # Update last_downloaded_version in MySQL
            write_last_downloaded_version(conn, release_version)
            # Clean up
            os.remove(downloaded_file)
            print(f"Uploaded {asset_name} to S3 bucket {s3_bucket}")
        else:
            print(f"Latest version ({release_version}) is already downloaded.")
            os.remove(downloaded_file)
    else:
        print("Asset not found.")

    # Close MySQL connection
    conn.close()

if __name__ == "__main__":
    main()
