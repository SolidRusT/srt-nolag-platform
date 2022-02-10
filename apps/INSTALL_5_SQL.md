## HA PostgreSQL Install

### Deploy the k8s Operator

```bash
kubectl apply -f postgres-operator/configmap.yaml
kubectl apply -f postgres-operator/operator-service-account-rbac.yaml
kubectl apply -f postgres-operator/postgres-operator.yaml
kubectl apply -f postgres-operator/api-service.yaml

kubectl get pod -l name=postgres-operator

kubectl logs "$(kubectl get pod -l name=postgres-operator --output='name')"
```

### Deploy the UI

```bash
kubectl apply -f postgres-ui/

kubectl get pod -l name=postgres-operator-ui

#kubectl port-forward svc/postgres-operator-ui 8081:80
```

### Deploy a PostgreSQL cluster

```bash
kubectl create namespace zalando-sandbox
kubectl apply -n zalando-sandbox -f postgres-cluster/minimal-postgres-manifest.yaml

```