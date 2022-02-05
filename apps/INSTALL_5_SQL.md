## Stolon PostgreSQL
 - [Maintainers](https://github.com/sorintlab/stolon)
 - [Stolon on Kubernetes](https://github.com/sorintlab/stolon/blob/master/examples/kubernetes/README.md)

### Apply
```bash
kubectl run -i -t stolonctl --image=sorintlab/stolon:master-pg10 --restart=Never --rm -- /usr/local/bin/stolonctl --cluster-name=kube-stolon --store-backend=kubernetes --kube-resource-kind=configmap init
kubectl apply -f postgres-stolon-sentinel.yaml
kubectl apply -f postgres-stolon-secret.yaml
kubectl apply -f postgres-stolon-keeper.yaml
kubectl apply -f postgres-stolon-proxy.yaml
kubectl apply -f postgres-stolon-proxy-service.yaml
```

## Binami (depricated/broken/shit)
### Apply
```bash
helm repo add bitnami https://charts.bitnami.com/bitnami
RELEASE_NAME="hapostgres"
RELEASE_NAMESPACE="${RELEASE_NAME}"
helm install ${RELEASE_NAME} bitnami/postgresql-ha --create-namespace --namespace=${RELEASE_NAMESPACE}
kubectl get all -n ${RELEASE_NAMESPACE}
```

### Delete
```bash
RELEASE_NAME="hapostgres"
RELEASE_NAMESPACE="${RELEASE_NAME}"
helm delete ${RELEASE_NAME} --namespace=${RELEASE_NAMESPACE}
kubectl delete pvc --all -n ${RELEASE_NAMESPACE}
kubectl delete namespace ${RELEASE_NAMESPACE}
```


### host-level Access

```bash
kubectl port-forward --namespace hapostgres svc/hapostgres-postgresql-ha-pgpool 5432:5432
psql -h 127.0.0.1 -p 5432 -U postgres -d postgres

# psql hapostgres-postgresql-ha-pgpool.hapostgres.svc.cluster.local -p 5432
```

### Pod-level access

```bash
hapostgres-postgresql-ha-pgpool.hapostgres.svc.cluster.local
```

### Notes

** Please be patient while the chart is being deployed **
PostgreSQL can be accessed through Pgpool via port 5432 on the following DNS name from within your cluster:

    hapostgres-postgresql-ha-pgpool.hapostgres.svc.cluster.local

Pgpool acts as a load balancer for PostgreSQL and forward read/write connections to the primary node while read-only connections are forwarded to standby nodes.

To get the password for "postgres" run:

    export POSTGRES_PASSWORD=$(kubectl get secret --namespace hapostgres hapostgres-postgresql-ha-postgresql -o jsonpath="{.data.postgresql-password}" | base64 --decode)

To get the password for "repmgr" run:

    export REPMGR_PASSWORD=$(kubectl get secret --namespace hapostgres hapostgres-postgresql-ha-postgresql -o jsonpath="{.data.repmgr-password}" | base64 --decode)

To connect to your database run the following command:

    kubectl run hapostgres-postgresql-ha-client --rm --tty -i --restart='Never' --namespace hapostgres --image docker.io/bitnami/postgresql-repmgr:11.14.0-debian-10-r78 --env="PGPASSWORD=$POSTGRES_PASSWORD"  \
        --command -- psql -h hapostgres-postgresql-ha-pgpool -p 5432 -U postgres -d postgres

To connect to your database from outside the cluster execute the following commands:

    kubectl port-forward --namespace hapostgres svc/hapostgres-postgresql-ha-pgpool 5432:5432 &
    psql -h 127.0.0.1 -p 5432 -U postgres -d postgres
