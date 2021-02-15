#!/bin/bash
export WEB_CONTENT="/c/Users/shaun/Documents/repos/solidrust.net"
aws s3 sync --delete ${WEB_CONTENT}/web s3://solidrust.net --acl public-read
