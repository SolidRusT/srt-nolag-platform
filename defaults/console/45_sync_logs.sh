SERVERS=(nine)

for server in ${SERVERS[@]}; do
    mkdir -p $HOME/logs/$server
    scp $server.solidrust.net:~/*.log $HOME/logs/$server/
    scp $server.solidrust.net:/game/*.log $HOME/logs/$server/
done