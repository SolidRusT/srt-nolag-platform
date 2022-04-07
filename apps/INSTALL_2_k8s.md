### Create k8s cluster

```bash
#sudo kubeadm init --apiserver-advertise-address=10.42.69.124 --pod-network-cidr=10.142.0.0/16
sudo kubeadm init --pod-network-cidr=10.140.0.0/16
```

```bash
rm -rf $HOME/.kube
mkdir -p $HOME/.kube
sudo cp -i /etc/kubernetes/admin.conf $HOME/.kube/config
sudo chown $(id -u):$(id -g) $HOME/.kube/config
```

## Configure virtual networks

/etc/default/kubelet:
  `KUBELET_EXTRA_ARGS="--cgroup-driver=cgroupfs"`

```bash
#sudo iptables -P INPUT ACCEPT
#sudo iptables -P FORWARD ACCEPT
#sudo iptables -P OUTPUT ACCEPT
#sudo iptables -F

### Connect Nodes

```bash
kubeadm token create --print-join-command
```

```bash
curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
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

### Apply Cilium cusomizations
```bash
kubectl apply -f ${HOME}solidrust.net/apps/cilium-config.yaml -n kube-system
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

### Install LoadBalancer

```bash
kubectl get configmap kube-proxy -n kube-system -o yaml | \
sed -e "s/strictARP: false/strictARP: true/" | \
kubectl apply -f - -n kube-system
helm repo add metallb https://metallb.github.io/metallb
helm install metallb metallb/metallb
helm install metallb metallb/metallb -f ${HOME}/solidrust.net/apps/metallb-values.yaml
```

### Restart all pods
```bash
kubectl get pods --all-namespaces -o custom-columns=NAMESPACE:.metadata.namespace,NAME:.metadata.name,HOSTNETWORK:.spec.hostNetwork --no-headers=true | grep '<none>' | awk '{print "-n "$1" "$2}' | xargs -L 1 -r kubectl delete pod
```

### Install Ingress controller
```bash
kubectl apply -n ingress-nginx -f ${HOME}/solidrust.net/apps/ingress-nginx.yaml
#helm install ingress-nginx ingress-nginx \
#--repo https://kubernetes.github.io/ingress-nginx \
#--namespace ingress-nginx --create-namespace
```