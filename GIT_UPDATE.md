sudo apt update
sudo apt install git python3 python3-pip
pip3 install requests boto3 configparser


example

```python
import requests
import boto3
import zipfile
import os

# Read configuration file
config = configparser.ConfigParser()
config.read('config.ini')
github_token = config['GITHUB']['token']
s3_bucket_name = config['S3']['bucket_name']
s3_access_key = config['S3']['access_key']
s3_secret_key = config['S3']['secret_key']

# Clone repository
repo_url = 'https://github.com/USERNAME/REPO_NAME.git'
repo_dir = '/path/to/repo'
os.system(f'git clone {repo_url} {repo_dir}')

# Get latest release
api_url = 'https://api.github.com/repos/USERNAME/REPO_NAME/releases/latest'
headers = {'Authorization': f'token {github_token}'}
response = requests.get(api_url, headers=headers)
release = response.json()

# Package release
release_dir = '/path/to/release'
zip_file = f'{release_dir}.zip'
with zipfile.ZipFile(zip_file, 'w') as zf:
    for root, dirs, files in os.walk(release_dir):
        for file in files:
            zf.write(os.path.join(root, file))

# Upload to S3
s3 = boto3.resource('s3', aws_access_key_id=s3_access_key, aws_secret_access_key=s3_secret_key)
s3.Bucket(s3_bucket_name).upload_file(zip_file, 'RELEASE_NAME.zip')

```