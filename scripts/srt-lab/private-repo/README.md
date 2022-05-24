## Docker Repo setup
### Enable NFS file share
#### Master k8s node(s)
```bash
sudo mkdir -p /opt/certs /opt/registry
sudo chown nobody:nogroup /opt/registry
sudo chown nobody:nogroup /opt/certs
sudo chmod 755 /opt/registry
sudo chmod 755 /opt/certs
```

/etc/exports:
```
/opt/certs              10.0.0.0/8(rw,sync,no_subtree_check)
/opt/registry           10.0.0.0/8(rw,sync,no_subtree_check)
```

```bash
sudo systemctl restart nfs-kernel-server
```

#### Slaves k8s nodes
`sudo mkdir /opt/certs /opt/registry`

/etc/fstab:
```
10.42.69.124:/opt/certs	    /opt/certs	nfs4	defaults,user,exec	0 0
10.42.69.124:/opt/registry	/opt/registry	nfs4	defaults,user,exec	0 0
```

mount the filesystems

`sudo mount -a`

### Install this app using ArgoCD

### Test push to your repository

```bash
docker pull nginx
docker tag nginx:latest repo.eks.solidrust.net:5000/nginx:latest
# docker push srt-lab-repo:5000/nginx:latest
docker push repo.eks.solidrust.net:5000/nginx:latest
docker image rm repo.eks.solidrust.net:5000/nginx:latest
```



#### Depricated

```bash
kubectl create namespace private-repo
kubectl apply -f ${HOME}/solidrust.net/apps/private-registry.yaml -n srt-repo
kubectl apply -f ${HOME}/solidrust.net/apps/private-registry-svc.yaml -n srt-repo
kubectl -n srt-repo get svc
kubectl get svc private-repository-k8s -n private-repo
```