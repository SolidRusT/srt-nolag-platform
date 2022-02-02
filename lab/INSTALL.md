```bash
apt install sudo net-tools
usermod -aG sudo shaun
```


```bash
sudo apt update && sudo apt dist-upgrade -y
```

unfuck your /etc/fstab - remove swap

```bash
sudo nano /etc/fstab
sudo swapoff -a
```

unfuck your sysctl

```bash
sudo nano /etc/sysctl.conf
net.ipv4.ip_forward=1
vm.swappiness = 0
```

install docker

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

sudo kubeadm init --apiserver-advertise-address=100.0.0.1 --pod-network-cidr=10.244.0.0/16


references
 - [Debian 11 Kubernetes Install](https://snapshooter.com/learn/linux/install-kubernetes)
 - [Bare Metal LoadBalancer](https://metallb.universe.tf/installation/)
 - [Bare Metal ingress-nginx](https://kubernetes.github.io/ingress-nginx/deploy/#bare-metal-clusters)
 - [Ingress TLS Termination](https://kubernetes.github.io/ingress-nginx/examples/tls-termination/)
