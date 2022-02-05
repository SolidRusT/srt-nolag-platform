

## Docker Repo setup
### Enable NFS file share
#### Master
```bash
sudo apt install -y nfs-common nfs-kernel-server
sudo mkdir -p /opt/certs /opt/registry
sudo chown nobody:nogroup /opt/registry
sudo chown nobody:nogroup /opt/certs
sudo chmod 755 /opt/registry
sudo chmod 755 /opt/certs
sudo usermod -aG docker shaun
```

/etc/exports:
```
/opt/certs              10.42.69.0/24(rw,sync,no_subtree_check)
/opt/registry           10.42.69.0/24(rw,sync,no_subtree_check)
```

```bash
sudo systemctl restart nfs-kernel-server
kubectl create namespace srt-lab-repo
kubectl apply -f private-registry.yaml
kubectl apply -f private-registry-svc.yaml
kubectl -n srt-lab-repo get svc
```

#### Slaves
`sudo mkdir /opt/certs /opt/registry`

/etc/fstab:
```
10.42.69.124:/opt/certs	    /opt/certs	nfs4	defaults,user,exec	0 0
10.42.69.124:/opt/registry	/opt/registry	nfs4	defaults,user,exec	0 0
```

`mount -a`

### Prepare SSL certificate for Docker Repository server

```bash
#sudo openssl req -newkey rsa:4096 -nodes -sha256 -keyout  /opt/certs/registry.key -x509 -days 365 -out /opt/certs/registry.crt
cp ${HOME}/letsencrypt-wild/config/live/lab.hq.solidrust.net/fullchain.pem /opt/certs
cp ${HOME}/letsencrypt-wild/config/live/lab.hq.solidrust.net/privkey.pem /opt/certs
openssl x509 -outform der -in ${HOME}/letsencrypt-wild/config/live/lab.hq.solidrust.net/fullchain.pem \
  -out /opt/certs/fullchain.crt
sudo chmod -R ugo+r /opt/certs
sudo cp /opt/certs/fullchain.crt /usr/local/share/ca-certificates
sudo update-ca-certificates
sudo systemctl restart docker
```

repeat this on slave nodes
```bash
sudo cp /opt/certs/fullchain.crt /usr/local/share/ca-certificates
sudo update-ca-certificates
sudo systemctl restart docker
```

 `get svc private-repository-k8s` and put external IP into DNS

```bash
docker pull nginx
docker tag nginx:latest srt-lab-repo:5000/nginx:latest
docker tag nginx:latest repo.lab.hq.solidrust.net:5000/nginx:latest
# docker push srt-lab-repo:5000/nginx:latest
docker push repo.lab.hq.solidrust.net:5000/nginx:latest
docker image rm repo.lab.hq.solidrust.net:5000/nginx:latest
```