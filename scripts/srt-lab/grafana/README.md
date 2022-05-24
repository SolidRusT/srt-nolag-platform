### Configure persistent storage

configure your own NFS mounts in `volume.yaml`, for persistent storage.

for example:

```bash
sudo mkdir /opt/grafana
```

then add the above new folders to your `/etc/exports`

/etc/exports:
```
/opt/grafana            10.0.0.0/8(rw,sync,no_subtree_check)
```

restart NFS server

```bash
sudo systemctl restart nfs-kernel-server
```
