### Create k8s cluster

```bash
sudo kubeadm init --apiserver-advertise-address=10.42.69.124 --pod-network-cidr=10.42.0.0/16 --service-dns-domain lab-internal.hq.solidrust.net
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

### Connect Nodes

```bash
kubeadm token create --print-join-command
```

### Install MetalLB

```bash
curl https://baltocdn.com/helm/signing.asc | sudo apt-key add -
echo "deb https://baltocdn.com/helm/stable/debian/ all main" | sudo tee /etc/apt/sources.list.d/helm-stable-debian.list
sudo apt-get update
sudo apt-get install helm
helm repo add metallb https://metallb.github.io/metallb
helm install metallb metallb/metallb -f metallb-values.yaml --create-namespace --namespace metallb
```
### Install TLS secrets



### Install Ingress controller

```bash
helm install ingress-nginx ingress-nginx \
--repo https://kubernetes.github.io/ingress-nginx \
--namespace ingress-nginx --create-namespace
kubectl apply -f hello-world.yaml
kubectl apply -f hello-world-ingress.yaml
```

#### Example Ingress
```yaml
#An example Ingress that makes use of the controller:
  apiVersion: networking.k8s.io/v1
  kind: Ingress
  metadata:
    name: example
    namespace: foo
  spec:
    ingressClassName: nginx
    rules:
      - host: www.example.com
        http:
          paths:
            - backend:
                service:
                  name: exampleService
                  port:
                    number: 80
              path: /
    # This section is only required if TLS is to be enabled for the Ingress
    tls:
      - hosts:
        - www.example.com
        secretName: example-tls

#If TLS is enabled for the Ingress, a Secret containing the certificate and key must also be provided:

  apiVersion: v1
  kind: Secret
  metadata:
    name: example-tls
    namespace: foo
  data:
    tls.crt: <base64 encoded cert>
    tls.key: <base64 encoded key>
  type: kubernetes.io/tls
```

### Certicifate manager
```bash
helm repo add jetstack https://charts.jetstack.io
helm repo update
helm install \
  cert-manager jetstack/cert-manager \
  --namespace cert-manager \
  --create-namespace \
  --version v1.7.0 \
  --set installCRDs=true
wget https://github.com/cert-manager/cert-manager/releases/download/v1.7.0/cmctl-linux-amd64.tar.gz
tar xzvf cmctl-linux-amd64.tar.gz
sudo mv cmctl /usr/local/bin/
chmod +x /usr/local/bin/cmctl
rm cmctl-linux-amd64.tar.gz LICENSES
cmctl version
```

## Configure egress

```bash
helm repo add istio https://istio-release.storage.googleapis.com/charts
helm repo update
kubectl create namespace istio-system
helm install istio-base istio/base -n istio-system
helm install istiod istio/istiod -n istio-system --wait
helm status istiod -n istio-system
```

## Uninstall k8s cluster (on all servers)

```bash
sudo kubeadm reset
sudo reboot
```