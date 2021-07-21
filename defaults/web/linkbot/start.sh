#!/bin/bash
export BOT_HOME="${HOME}/solidrust.net/defaults/web/linkbot"
cd ${BOT_HOME}
rm -rf node_modules
npm install

while :
do
    nodejs LinkBot.js >> run.log
	echo "Restarting [ hit CTRL+C to stop]" | tee -a run.log
done
