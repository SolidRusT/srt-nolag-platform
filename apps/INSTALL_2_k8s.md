### Create k8s cluster

```bash
#sudo kubeadm init --apiserver-advertise-address=10.42.69.124 --pod-network-cidr=10.142.0.0/16
sudo kubeadm init --pod-network-cidr=10.142.0.0/16
```

```bash
rm -rf $HOME/.kube
mkdir -p $HOME/.kube
sudo cp -i /etc/kubernetes/admin.conf $HOME/.kube/config
sudo chown $(id -u):$(id -g) $HOME/.kube/config
```

## Configure virtual networks

```bash
#sudo iptables -P INPUT ACCEPT
#sudo iptables -P FORWARD ACCEPT
#sudo iptables -P OUTPUT ACCEPT
#sudo iptables -F

helm repo add cilium https://helm.cilium.io/
helm repo update
helm install cilium cilium/cilium \
  --namespace kube-system
kubectl get pods --all-namespaces -o custom-columns=NAMESPACE:.metadata.namespace,NAME:.metadata.name,HOSTNETWORK:.spec.hostNetwork --no-headers=true | grep '<none>' | awk '{print "-n "$1" "$2}' | xargs -L 1 -r kubectl delete pod

curl -L --remote-name-all https://github.com/cilium/cilium-cli/releases/latest/download/cilium-linux-amd64.tar.gz{,.sha256sum}
sha256sum --check cilium-linux-amd64.tar.gz.sha256sum
sudo tar xzvfC cilium-linux-amd64.tar.gz /usr/local/bin
rm cilium-linux-amd64.tar.gz{,.sha256sum}
```

### Connect Nodes

```bash
kubeadm token create --print-join-command
```

### Test connectivity

```bash
cilium status --wait
cilium connectivity test
#cilium hubble enable
#export HUBBLE_VERSION=$(curl -s https://raw.githubusercontent.com/cilium/hubble/master/stable.txt)
#curl -L --remote-name-all https://github.com/cilium/hubble/releases/download/$HUBBLE_VERSION/hubble-linux-amd64.tar.gz{,.sha256sum}
#sha256sum --check hubble-linux-amd64.tar.gz.sha256sum
#sudo tar xzvfC hubble-linux-amd64.tar.gz /usr/local/bin
#rm hubble-linux-amd64.tar.gz{,.sha256sum}
```

### Install MetalLB

```bash
curl https://baltocdn.com/helm/signing.asc | sudo apt-key add -
echo "deb https://baltocdn.com/helm/stable/debian/ all main" | sudo tee /etc/apt/sources.list.d/helm-stable-debian.list
sudo apt-get update
sudo apt-get install helm
helm repo add metallb https://metallb.github.io/metallb
helm install metallb metallb/metallb -f solidrust.net/apps/metallb-values.yaml --create-namespace --namespace metallb
```

### Install Ingress controller

```bash
kubectl apply -n ingress-nginx -f ${HOME}/solidrust.net/apps/ingress-nginx.yaml
#helm install ingress-nginx ingress-nginx \
#--repo https://kubernetes.github.io/ingress-nginx \
#--namespace ingress-nginx --create-namespace
```

## Uninstall k8s cluster (on all servers)

```bash
sudo kubeadm reset
sudo reboot
```