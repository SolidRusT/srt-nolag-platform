helm repo add rook-release https://charts.rook.io/release
helm install --create-namespace --namespace rook-ceph rook-ceph rook-release/rook-ceph
kubectl --namespace rook-ceph get pods -l "app=rook-ceph-operator"

helm delete --namespace rook-ceph rook-ceph