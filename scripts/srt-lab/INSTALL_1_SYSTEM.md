## Install
### Bootstrap Debian 11 Bullseye

```bash
sudo apt update && \
sudo apt-get install -y \
    net-tools \
    python3 \
    certbot \
    python3-certbot-dns-cloudflare \
    ca-certificates \
    curl \
    gnupg \
    lsb-release \
    software-properties-common \
    apt-transport-https \
    unzip \
    nfs-common \
    nfs-kernel-server \
    xfsprogs \
    lvm2 && \
sudo apt dist-upgrade -y
```

### Enable network routing

```bash
echo "
net.ipv4.ip_forward=1
vm.swappiness = 0" | sudo tee -a /etc/sysctl.conf
```

### Set hostname
```bash
NEW_NAME="demo"
echo ${NEW_NAME} | sudo tee /etc/hostname
echo "127.0.0.1    ${NEW_NAME} ${NEW_NAME}.solidrust.net" | sudo tee -a /etc/hosts /etc/cloud/templates/hosts.debian.tmpl
sudo hostnamectl set-hostname ${NEW_NAME}
sudo reboot
```
### Install Docker backend

```bash
echo "127.0.0.1 $(hostname)" | sudo tee -a /etc/hosts
```

### Add Docker repo
```bash
curl -fsSL https://download.docker.com/linux/debian/gpg | sudo gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/debian $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list
```

#### Add Kubernetes repo
```bash
curl -s https://packages.cloud.google.com/apt/doc/apt-key.gpg | sudo apt-key add
sudo apt-add-repository "deb http://apt.kubernetes.io/ kubernetes-xenial main"
```

#### Install Docker and Kubernetes CLI
```bash
sudo apt update -y && \
sudo apt install -y docker-ce docker-ce-cli containerd.io kubelet kubeadm kubectl
```

### Install AWS CLI v2

```bash
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip
sudo ./aws/install
aws configure
```

### Update shell profile

.bashrc:
```bash
export KUBE_EDITOR="nano"

# SRT Shell commands
source ${HOME}/solidrust.net/defaults/funct_common.sh
source ${HOME}/solidrust.net/defaults/funct_update.sh
initialize_srt
```

### Import SolidRusT defaults

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
