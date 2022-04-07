sudo mkdir -p /storage
sudo mkfs -t xfs /dev/nvme1n1
sudo mount /dev/nvme1n1 /storage
sudo blkid
sudo nano /etc/fstab


helm repo add rook-release https://charts.rook.io/release
helm install --create-namespace --namespace rook-ceph rook-ceph rook-release/rook-ceph
kubectl --namespace rook-ceph get pods -l "app=rook-ceph-operator"

kubectl apply -f rook-ceph.yaml



kubectl patch storageclass default-local-storage -p '{"metadata": {"annotations":{"storageclass.kubernetes.io/is-default-class":"true"}}}'


kubectl get sc -A



helm delete --namespace rook-ceph rook-ceph