#!/bin/bash

# Initialize
export BOT_HOME="${HOME}/solidrust.net/defaults/web/linkbot"
cd ${BOT_HOME}
rm -rf node_modules 
npm install
npm update

# clean some shit
SHIT=$(ps -ef | grep LinkBot.js | awk -F " " {' print $2 '})
for shit in ${SHIT[@]}; do kill $shit; done

# 
while :
do
    echo "Starting Discord linkbot"
    nodejs LinkBot.js >> run.log
    echo "Stopped" | tee -a run.log
	echo "Restarting [ hit Ctrl+C to stop ]" | tee -a run.log
done