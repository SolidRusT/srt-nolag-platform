## HA PostgreSQL Install

kubectl apply -f configmap.yaml
kubectl apply -f operator-service-account-rbac.yaml
kubectl apply -f postgres-operator.yaml
kubectl apply -f api-service.yaml
