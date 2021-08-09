#!/bin/bash
export BOT_HOME="${HOME}/solidrust.net/defaults/web/linkbot"
cd ${BOT_HOME}
rm -rf node_modules 
npm install

while :
do
    echo "Starting Discord linkbot"
    nodejs LinkBot.js >> run.log
    echo "Stopped"
	echo "Restarting [ hit CTRL+C to stop]" | tee -a run.log
done