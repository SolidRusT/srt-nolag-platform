## Install
### Bootstrap Ubuntu 20.04

setup your ssh keys, then start installing and configuring the cluster

```bash
sudo apt install -y net-tools python3 certbot python3-certbot-dns-cloudflare
```

```bash
sudo apt install -y \
    ca-certificates \
    curl \
    gnupg \
    lsb-release \
    software-properties-common \
    apt-transport-https
```

### Update distribution

```bash
sudo apt update && sudo apt dist-upgrade -y
```

### Remove swap

```bash
sudo nano /etc/fstab
sudo swapoff -a
```
may need to add `noauto` option to the swap entry in fstab to disable automount

### Enable network routing

```bash
sudo nano /etc/sysctl.conf
net.ipv4.ip_forward=1
vm.swappiness = 0
```

### Install Docker backend

```bash
sudo apt-get install -y apt-transport-https ca-certificates curl gnupg2 software-properties-common
curl -fsSL https://download.docker.com/linux/debian/gpg | sudo gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list
# apt-key adv --recv-keys --keyserver keyserver.ubuntu.com 7EA0A9C3F273FCD8
sudo apt update -y
sudo apt install docker-ce docker-ce-cli containerd.io -y
curl -s https://packages.cloud.google.com/apt/doc/apt-key.gpg | sudo apt-key add
sudo apt-add-repository "deb http://apt.kubernetes.io/ kubernetes-xenial main"
sudo apt update -y
sudo apt install kubelet kubeadm kubectl -y
```

add user to docker group

```bash
sudo usermod -aG docker ${USER}
```

```bash
sudo apt install unzip
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip
sudo ./aws/install
aws configure
```

.bashrc:
```bash
export KUBE_EDITOR="nano"

# SRT Shell commands
source ${HOME}/solidrust.net/defaults/funct_common.sh
source ${HOME}/solidrust.net/defaults/funct_update.sh
initialize_srt
```

```bash
export S3_REPO="s3://solidrust.net-repository"
aws s3 sync --delete ${S3_REPO} ${HOME}/solidrust.net | grep -v ".git" 
```

logout + login then run `update_repo`

```bash
update_repo
```

logout + login again

Your machine is now bootstrapped.
