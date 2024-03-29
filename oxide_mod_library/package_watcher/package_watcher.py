import os
import re
import sys
import time
import boto3
import datetime
import configparser
import mysql.connector
from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler

# Load configuration values
config = configparser.ConfigParser()
config.read('config.ini')

db_config = {
    'host': config.get('database', 'host'),
    'port': int(config.get('database', 'port')),
    'user': config.get('database', 'user'),
    'password': config.get('database', 'password'),
    'database': config.get('database', 'database')
}

# Load AWS credentials from the config file
aws_access_key_id = config.get('aws', 'access_key_id')
aws_secret_access_key = config.get('aws', 'secret_access_key')

# Load bucket details
s3_bucket = config.get('s3', 'bucket')
s3_path = config.get('s3', 'path')

# Connect to MySQL and create a cursor
cnx = mysql.connector.connect(**db_config)
cursor = cnx.cursor()

# Configure Boto3 for interacting with S3
s3 = boto3.client(
    's3',
    aws_access_key_id=aws_access_key_id,
    aws_secret_access_key=aws_secret_access_key
)

# Define the event handler for new files
class PackageEventHandler(FileSystemEventHandler):
    def on_created(self, event):
        if event.src_path.endswith('.cs'):
            self.process_package(event.src_path)

    def process_package(self, path):
        with open(path, 'r') as file:
            content = file.read()

            # Parse Info and Description arrays
            info = re.search(r'\[Info\("(.*?)", "(.*?)", "(.*?)"\)\]', content)

            if not info:
                print(f"Invalid package format: {path}")
                return

            package_name, package_author, package_version = info.groups()

            # Store package info in MySQL
            query = """
            INSERT INTO packages (package_name, package_author, package_version, timestamp)
            VALUES (%s, %s, %s, %s)
            ON DUPLICATE KEY UPDATE
                package_author = VALUES(package_author),
                package_version = VALUES(package_version),
                timestamp = VALUES(timestamp)
            """
            cursor.execute(query, (package_name, package_author, package_version, datetime.datetime.utcnow()))
            cnx.commit()

            # Upload to S3
            s3_key_cs = f"{s3_path}/plugins/{package_name}.cs"
            s3.upload_file(path, s3_bucket, s3_key_cs)

            print(f"Package '{package_name}' processed and uploaded to S3")

# Start watching the folder
def main():
    if len(sys.argv) != 2:
        print("Usage: python package_watcher.py /path/to/folder")
        return

    watch_folder = sys.argv[1]

    event_handler = PackageEventHandler()
    observer = Observer()
    observer.schedule(event_handler, watch_folder, recursive=False)
    observer.start()

    print(f"Watching folder: {watch_folder}")

    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        observer.stop()

    observer.join()

if __name__ == "__main__":
    main()
