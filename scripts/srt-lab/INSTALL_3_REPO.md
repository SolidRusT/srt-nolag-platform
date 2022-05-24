## Docker Repo setup
### Enable NFS file share
#### Master
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

#### Slaves
`sudo mkdir /opt/certs /opt/registry`

/etc/fstab:
```
10.42.69.124:/opt/certs	    /opt/certs	nfs4	defaults,user,exec	0 0
10.42.69.124:/opt/registry	/opt/registry	nfs4	defaults,user,exec	0 0
```

mount the filesystems

`sudo mount -a`


### Cloudflare integration

#### https://dash.cloudflare.com/profile/api-tokens
 mkdir certbot
 nano certbot/cloudflare.ini
 chmod go-rw certbot/cloudflare.ini


### Prepare SSL certificate for Docker Repository server

```bash
CERT_DIR="${HOME}/letsencrypt"
CERTWILD_DIR="${HOME}/letsencrypt-wild"
rm -rf ${CERTWILD_DIR} && mkdir -p ${CERTWILD_DIR}
rm -rf ${CERT_DIR} && mkdir -p ${CERT_DIR}
```

### Create SSL certificates

```bash
certbot certonly -d eks.solidrust.net --dns-cloudflare --dns-cloudflare-credentials ${HOME}/certbot/cloudflare.ini --logs-dir ${CERT_DIR}/log/ --config-dir ${CERT_DIR}/config/ --work-dir ${CERT_DIR}/work/ -m shaun@solidrust.net --agree-tos --server https://acme-v02.api.letsencrypt.org/directory
```

```bash
certbot certonly -d *.eks.solidrust.net --dns-cloudflare --dns-cloudflare-credentials ${HOME}/certbot/cloudflare.ini --logs-dir ${CERTWILD_DIR}/log/ --config-dir ${CERTWILD_DIR}/config/ --work-dir ${CERTWILD_DIR}/work/ -m shaun@solidrust.net --agree-tos --server https://acme-v02.api.letsencrypt.org/directory
```

```bash
sudo cp ${HOME}/letsencrypt-wild/config/live/eks.solidrust.net/fullchain.pem /opt/certs
sudo cp ${HOME}/letsencrypt-wild/config/live/eks.solidrust.net/privkey.pem /opt/certs
sudo openssl x509 -outform der -in ${HOME}/letsencrypt-wild/config/live/eks.solidrust.net/fullchain.pem \
  -out /opt/certs/fullchain.crt
sudo chmod -R ugo+r /opt/certs
sudo cp /opt/certs/fullchain.crt /usr/local/share/ca-certificates
sudo update-ca-certificates
sudo systemctl restart docker
```

```bash
kubectl create secret tls eks.solidrust.net-tls --cert=${CERT_DIR}/config/live/eks.solidrust.net/fullchain.pem --key=${CERT_DIR}/config/live/eks.solidrust.net/privkey.pem -n default

kubectl create secret tls star.eks.solidrust.net-tls --cert=${CERTWILD_DIR}/config/live/eks.solidrust.net/fullchain.pem --key=${CERTWILD_DIR}/config/live/eks.solidrust.net/privkey.pem -n default
```

repeat this on slave nodes
```bash
sudo ln -s /opt/certs/fullchain.crt /usr/local/share/ca-certificates
sudo update-ca-certificates
sudo systemctl restart docker
sudo mount -a
```

### Create Docker repo service

```bash
kubectl create namespace srt-repo
kubectl apply -f ${HOME}/solidrust.net/apps/private-registry.yaml -n srt-repo
kubectl apply -f ${HOME}/solidrust.net/apps/private-registry-svc.yaml -n srt-repo
kubectl -n srt-repo get svc
kubectl get svc private-repository-k8s -n srt-repo
```

### Test push to your repository

```bash
docker pull nginx
docker tag nginx:latest repo.eks.solidrust.net:5000/nginx:latest
# docker push srt-lab-repo:5000/nginx:latest
docker push repo.eks.solidrust.net:5000/nginx:latest
docker image rm repo.eks.solidrust.net:5000/nginx:latest
```