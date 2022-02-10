## HA PostgreSQL Install

kubectl apply -f postgres/configmap.yaml
kubectl apply -f postgres/operator-service-account-rbac.yaml
kubectl apply -f postgres/postgres-operator.yaml
kubectl apply -f postgres/api-service.yaml

kubectl get pod -l name=postgres-operator
kubectl logs "$(kubectl get pod -l name=postgres-operator --output='name')"


kubectl apply -f postgres-ui/
