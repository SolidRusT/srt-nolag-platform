sudo dd if=/dev/zero of=/dev/nvme1n1 bs=1M

git clone https://github.com/rook/rook.git
kubectl create -f crds.yaml -f common.yaml -f operator.yaml
kubectl create -f cluster.yaml
kubectl -n rook-ceph get pod


kubectl apply -f ${HOME}/solidrust.net/apps/rook-ceph.yaml
