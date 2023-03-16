import os
import re
import sys
import boto3
import datetime
import mysql.connector
from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler

# Add your MySQL credentials and S3 bucket info here
db_config = {
    'host': '10.10.10.11',
    'port': 3306,
    'user': 'your_username',
    'password': 'your_password',
    'database': 'package_repo'
}

s3_bucket = 'srt-nolag-platform-live-repo'
s3_path = '/repo'

# Connect to MySQL and create a cursor
cnx = mysql.connector.connect(**db_config)
cursor = cnx.cursor()

# Configure Boto3 for interacting with S3
s3 = boto3.client('s3')

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
            desc = re.search(r'\[Description\("(.*?)"\)\]', content)

            if not info or not desc:
                print(f"Invalid package format: {path}")
                return

            package_name, package_author, package_version = info.groups()
            package_description, = desc.groups()

            # Store package info in MySQL
            query = "INSERT INTO packages (package_name, package_author, package_version, package_description, timestamp) VALUES (%s, %s, %s, %s, %s)"
            cursor.execute(query, (package_name, package_author, package_version, package_description, datetime.datetime.utcnow()))
            cnx.commit()

            # Upload to S3
            s3_key_cs = f"{s3_path}/plugins/{package_name}.cs"
            s3.upload_file(path, s3_bucket,s3_key_cs)

            # Upload JSON to S3
            json_path = os.path.splitext(path)[0] + '.json'
            if os.path.exists(json_path):
              s3_key_json = f"{s3_path}/configs/{package_name}.json"
              s3.upload_file(json_path, s3_bucket, s3_key_json)

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