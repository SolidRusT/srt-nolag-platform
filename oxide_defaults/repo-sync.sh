#!/bin/bash
echo "Pushing SRT Defaults"
aws s3 sync --delete /c/Users/shaun/repos/solidrust.net/defaults/ s3://solidrust.net-repository/defaults/
echo "Pushing SRT Custom Servers"
aws s3 sync --delete /c/Users/shaun/repos/solidrust.net/servers/ s3://solidrust.net-repository/servers/
echo "Pushing SRT Web"
aws s3 sync --delete /c/Users/shaun/repos/solidrust.net/web/ s3://solidrust.net-repository/web/
