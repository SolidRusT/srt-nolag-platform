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
CERTWILD_DIR="${HOME}/letsencrypt-wild"
kubectl create secret tls star.lab.hq.solidrust.net-tls --cert=${CERTWILD_DIR}/config/live/lab.hq.solidrust.net/fullchain.pem --key=${CERTWILD_DIR}/config/live/lab.hq.solidrust.net/privkey.pem -n default
kubectl apply -f postgres-ui/

kubectl get pod -l name=postgres-operator-ui

#kubectl port-forward svc/postgres-operator-ui 8081:80
```

### Deploy a PostgreSQL cluster

```bash
kubectl create namespace zalando-test
kubectl apply -n zalando-test -f postgres-cluster/minimal-postgres-manifest.yaml

kubectl -n zalando-test get postgresql
kubectl -n zalando-test get pods -l application=spilo -L spilo-role
kubectl -n zalando-test get svc -l application=spilo -L spilo-role
```