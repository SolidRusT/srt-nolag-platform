# Instal RCON CLI
# From: https://github.com/gorcon/rcon-cli/releases/latest
wget https://github.com/gorcon/rcon-cli/releases/download/v0.9.0/rcon-0.9.0-amd64_linux.tar.gz
tar xzvf rcon-0.9.0-amd64_linux.tar.gz
mv rcon-0.9.0-amd64_linux/rcon* .
rm -rf rcon-0.9.0-amd64_linux* rcon.yaml
ssh-keygen -b 4096 && \
PUB_KEY=$(cat ${HOME}/.ssh/id_rsa.pub)
echo ${PUB_KEY}

git config --global user.email "smoke@solidrust.net"
git config --global user.name "SmokeQc"

git clone git@github.com:suparious/solidrust.net.git

echo "50 *    * * *   ${USER}  /usr/sbin/logrotate -f ${HOME}/solidrust.net/defaults/console/logrotate.conf --state ${HOME}/logrotate-state" | sudo tee -a /etc/crontab
