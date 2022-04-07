```bash



kubectl delete secret eks.solidrust.net-tls star.eks.solidrust.net-tls -n default
kubectl delete secret eks.solidrust.net-tls star.eks.solidrust.net-tls -n ingress-nginx
kubectl delete secret eks.solidrust.net-tls star.eks.solidrust.net-tls -n srt-lab-repo


kubectl create secret tls star.eks.solidrust.net-tls --cert=${CERTWILD_DIR}/config/live/eks.solidrust.net/fullchain.pem --key=${CERTWILD_DIR}/config/live/eks.solidrust.net/privkey.pem -n cert-manager
kubectl create secret tls star.eks.solidrust.net-tls --cert=${CERTWILD_DIR}/config/live/eks.solidrust.net/fullchain.pem --key=${CERTWILD_DIR}/config/live/eks.solidrust.net/privkey.pem -n srt-lab-repo

kubectl create secret tls eks.solidrust.net-tls --cert=${CERT_DIR}/config/live/eks.solidrust.net/fullchain.pem --key=${CERT_DIR}/config/live/eks.solidrust.net/privkey.pem -n ingress-nginx
kubectl create secret tls star.eks.solidrust.net-tls --cert=${CERTWILD_DIR}/config/live/eks.solidrust.net/fullchain.pem --key=${CERTWILD_DIR}/config/live/eks.solidrust.net/privkey.pem -n ingress-nginx
```


### TLS test app
```bash
kubectl apply -f solidrust.net/apps/hello-world.yaml
kubectl apply -f solidrust.net/apps/hello-world-ingress.yaml
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

### Certificate manager
```bash
helm repo add jetstack https://charts.jetstack.io
helm repo update
helm install \
  cert-manager jetstack/cert-manager \
  --namespace cert-manager \
  --create-namespace \
  --version v1.7.0 \
  --set installCRDs=true
```

#### CLI interface

```bash
wget https://github.com/cert-manager/cert-manager/releases/download/v1.7.0/cmctl-linux-amd64.tar.gz
tar xzvf cmctl-linux-amd64.tar.gz
sudo mv cmctl /usr/local/bin/
chmod +x /usr/local/bin/cmctl
rm cmctl-linux-amd64.tar.gz LICENSES
cmctl version
```

#### Deploy TLS configuration

```bash
kubectl apply -f solidrust.net/apps/cert-manager-cloudflare.yaml
kubectl apply -f solidrust.net/apps/cert-manager-issuer.yaml
kubectl apply -f solidrust.net/apps/cert-manager-certificate.yaml
```