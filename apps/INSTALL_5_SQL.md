## Stolon PostgreSQL
 - [Maintainers](https://github.com/sorintlab/stolon)
 - [Stolon on Kubernetes](https://github.com/sorintlab/stolon/blob/master/examples/kubernetes/README.md)

### Apply
```bash
kubectl create namespace postgres-stolon
kubectl apply -n postgres-stolon -f postgres-stolon-sentinel.yaml
kubectl apply -n postgres-stolon -f postgres-stolon-secret.yaml
kubectl apply -n postgres-stolon -f postgres-stolon-keeper.yaml
kubectl apply -n postgres-stolon -f postgres-stolon-proxy.yaml
kubectl apply -n postgres-stolon -f postgres-stolon-proxy-service.yaml
```

#### Initialize / Test cluster
```bash
kubectl run -n postgres-stolon -i -t stolonctl --image=sorintlab/stolon:master-pg14 --restart=Never --rm -- /usr/local/bin/stolonctl --cluster-name=kube-stolon --store-backend=kubernetes --kube-resource-kind=configmap init
```

### Create a Role for the Service

```bash
kubectl apply -n postgres-stolon -f postgres-stolon-role.yaml
```

### Bind the Role to a ServiceAccount

```bash
kubectl apply -n postgres-stolon -f postgres-stolon-rolebind.yaml
```