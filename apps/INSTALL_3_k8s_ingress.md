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