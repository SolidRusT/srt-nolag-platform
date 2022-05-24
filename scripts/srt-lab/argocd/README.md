## Install ArgoCD
### Installing from Helm Charts
```bash
kubectl create namespace argocd
#kubectl apply -f deploy-argocd-ha.yaml
kubectl apply -f deploy-argocd-ha.yaml -n argocd
```

### Verify deployment
```bash
kubectl get svc -n argocd
kubectl get pods -n argocd
kubectl get deployments -n argocd
```

### Configure ingress

```bash
ns="ingress-nginx"
CA=$(kubectl -n $ns get secret ingress-nginx-admission -ojsonpath='{.data.ca}')
kubectl patch validatingwebhookconfigurations ingress-nginx-admission -n $ns --type='json' -p='[{"op": "add", "path": "/webhooks/0/clientConfig/caBundle", "value":"'$CA'"}]'
```

```bash
kubectl apply -f prod-https-ingress.yaml
kubectl get ingress -n argocd
kubectl describe ingress argocd-server-http-ingress
kubectl get ingress argocd-server-http-ingress -o yaml
```

### Retreive default admin password
```bash
kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath="{.data.password}" | base64 -d
```

### Log into argocd and change admin password
```bash
argocd login <ARGOCD_SERVER>
argocd account update-password
```










## Update Argo CD configurations
```bash
helm dep update charts/argo-cd/
helm upgrade argo-cd charts/argo-cd/ -n argocd
```

## Uninstall ArgoCD from the cluster
```bash
helm uninstall argo-cd charts/argo-cd/ -n argocd
```

## Troubleshooting
```bash
kubectl get svc argo-cd-argocd-server -n argocd
```

## Reset admin password
```bash
kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath="{.data.password}" | base64 -d
kubectl patch secret argocd-secret -n argocd -p '{"data": {"admin.password": null, "admin.passwordMtime": null}}'
kubectl delete secret argocd-initial-admin-secret -n argocd
kubectl rollout restart deployment argocd-server -n argocd
kubectl get pods -n argocd | grep argocd-server
```

#### References
[Install Argo using Helm](https://www.arthurkoziel.com/setting-up-argocd-with-helm/)
