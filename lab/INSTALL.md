## Install
### Bootstrap Debian 11 (root)

```bash
apt install sudo net-tools python3 certbot python3-certbot-dns-cloudflare
usermod -aG sudo shaun
```

### Update distribution (user)

```bash
sudo apt update && sudo apt dist-upgrade -y
```

### Remove swap

```bash
sudo nano /etc/fstab
sudo swapoff -a
```
may need to add `noauto` to the swap entry in fstab, because automount

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
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/debian $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list
sudo apt update -y
sudo apt install docker-ce docker-ce-cli containerd.io -y
curl -s https://packages.cloud.google.com/apt/doc/apt-key.gpg | sudo apt-key add
sudo apt-add-repository "deb http://apt.kubernetes.io/ kubernetes-xenial main"
sudo apt update -y
sudo apt install kubelet kubeadm kubectl -y
```

### Create k8s cluster

```bash
sudo kubeadm init --apiserver-advertise-address=10.42.69.124 --pod-network-cidr=10.142.0.0/16
```

```bash
rm -rf $HOME/.kube
mkdir -p $HOME/.kube
sudo cp -i /etc/kubernetes/admin.conf $HOME/.kube/config
sudo chown $(id -u):$(id -g) $HOME/.kube/config
```

### Install Flannel network

```bash
kubectl apply -f https://raw.githubusercontent.com/flannel-io/flannel/master/Documentation/kube-flannel.yml
```

### Install MetalLB

```bash
kubectl apply -f https://raw.githubusercontent.com/metallb/metallb/v0.11.0/manifests/namespace.yaml
kubectl apply -f https://raw.githubusercontent.com/metallb/metallb/v0.11.0/manifests/metallb.yaml
```

### Configure MetalLB

```bash
kubectl apply -f metallb-config.yaml -n metallb-system
# kubectl edit configmap -n kube-system kube-proxy
kubectl get configmap kube-proxy -n kube-system -o yaml | \
sed -e "s/strictARP: false/strictARP: true/" | \
kubectl apply -f - -n kube-system
```

### Generate SSL certs for ingress-nginx

```bash
kubectl apply -f https://github.com/jetstack/cert-manager/releases/download/v1.7.0/cert-manager.yaml
kubectl apply -f cloudflare-token.yaml -n cert-manager
kubectl apply -f cert-manager-config.yaml -n cert-manager
kubectl apply -f ingress-tls-fix.yaml -n ingress-nginx
```

### install ingress-nginx

```bash
# wget https://kubernetes.github.io/ingress-nginx/examples/multi-tls/multi-tls.yaml
# wget https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.1.1/deploy/static/provider/baremetal/deploy.yaml

276c276
<   type: NodePort
---
>   type: LoadBalancer
298c298
< kind: Deployment
---
> kind: DaemonSet
```

```bash
kubectl apply -f ingress-nginx.yaml -n ingress-nginx
#kubectl apply -f multi-tls.yaml -n default
```

## Uninstall k8s cluster (on all servers)

```bash
sudo kubeadm reset
sudo reboot
```

## Docker Repo setup
### Enable NFS file share
#### Master
```bash
sudo apt install -y nfs-common nfs-kernel-server
sudo mkdir -p /opt/certs /opt/registry
sudo chown nobody:nogroup /opt/registry
sudo chown nobody:nogroup /opt/certs
sudo chmod 755 /opt/registry
sudo chmod 755 /opt/certs
sudo usermod -aG docker shaun
```

/etc/exports:
```
/opt/certs              10.42.69.0/24(rw,sync,no_subtree_check)
/opt/registry           10.42.69.0/24(rw,sync,no_subtree_check)
```

```bash
sudo systemctl restart nfs-kernel-server
kubectl apply -f private-registry.yaml
```

#### Slaves
`sudo mkdir /opt/certs /opt/registry`

/etc/fstab:
```
10.42.69.124:/opt/certs	    /opt/certs	nfs4	defaults,user,exec	0 0
10.42.69.124:/opt/registry	/opt/registry	nfs4	defaults,user,exec	0 0
```

`mount -a`

### Prepare SSL certificate for Docker Repository server

```bash
#sudo openssl req -newkey rsa:4096 -nodes -sha256 -keyout  /opt/certs/registry.key -x509 -days 365 -out /opt/certs/registry.crt
cp ${HOME}/letsencrypt-wild/config/live/lab.hq.solidrust.net/fullchain.pem /opt/certs
cp ${HOME}/letsencrypt-wild/config/live/lab.hq.solidrust.net/privkey.pem /opt/certs
openssl x509 -outform der -in ${HOME}/letsencrypt-wild/config/live/lab.hq.solidrust.net/fullchain.pem \
  -out /opt/certs/fullchain.crt
sudo chmod -R ugo+r /opt/certs
sudo cp /opt/certs/fullchain.crt /usr/local/share/ca-certificates
sudo update-ca-certificates
sudo systemctl restart docker
```

repeat this on slave nodes
```bash
sudo cp /opt/certs/fullchain.crt /usr/local/share/ca-certificates
sudo update-ca-certificates
sudo systemctl restart docker
```

 `get svc private-repository-k8s` and put external IP into DNS

```bash
docker pull nginx
docker tag nginx:latest srt-lab-repo:5000/nginx:latest
docker tag nginx:latest repo.lab.hq.solidrust.net:5000/nginx:latest
# docker push srt-lab-repo:5000/nginx:latest
docker push repo.lab.hq.solidrust.net:5000/nginx:latest
docker image rm repo.lab.hq.solidrust.net:5000/nginx:latest
```

### Cert Manager
```bash


```




## References
 - [Debian 11 Kubernetes Install](https://snapshooter.com/learn/linux/install-kubernetes)
 - [Bare Metal LoadBalancer](https://metallb.universe.tf/installation/)
 - [Bare Metal ingress-nginx](https://kubernetes.github.io/ingress-nginx/deploy/#bare-metal-clusters)
 - [Ingress TLS Termination](https://kubernetes.github.io/ingress-nginx/examples/tls-termination/)
 - [k8s Dashboard](https://upcloud.com/community/tutorials/deploy-kubernetes-dashboard/)
 - [Private Docker Repo](https://www.linuxtechi.com/setup-private-docker-registry-kubernetes/)





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

export S3_REPO="s3://solidrust.net-repository"
aws s3 sync --delete ${S3_REPO} ${HOME}/solidrust.net | grep -v ".git" 