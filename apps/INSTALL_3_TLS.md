```bash
CERT_DIR="${HOME}/letsencrypt"
CERTWILD_DIR="${HOME}/letsencrypt-wild"
rm -rf ${CERTWILD_DIR} && mkdir -p ${CERTWILD_DIR}
rm -rf ${CERT_DIR} && mkdir -p ${CERT_DIR}

certbot certonly -d lab.hq.solidrust.net --dns-cloudflare --logs-dir ${CERT_DIR}/log/ --config-dir ${CERT_DIR}/config/ --work-dir ${CERT_DIR}/work/ -m shaun@solidrust.net --agree-tos --server https://acme-v02.api.letsencrypt.org/directory

certbot certonly -d *.lab.hq.solidrust.net --dns-cloudflare --logs-dir ${CERTWILD_DIR}/log/ --config-dir ${CERTWILD_DIR}/config/ --work-dir ${CERTWILD_DIR}/work/ -m shaun@solidrust.net --agree-tos --server https://acme-v02.api.letsencrypt.org/directory

kubectl delete secret lab.hq.solidrust.net-tls star.lab.hq.solidrust.net-tls -n default
kubectl delete secret lab.hq.solidrust.net-tls star.lab.hq.solidrust.net-tls -n ingress-nginx
kubectl delete secret lab.hq.solidrust.net-tls star.lab.hq.solidrust.net-tls -n srt-lab-repo

kubectl create secret tls lab.hq.solidrust.net-tls --cert=${CERT_DIR}/config/live/lab.hq.solidrust.net/fullchain.pem --key=${CERT_DIR}/config/live/lab.hq.solidrust.net/privkey.pem -n default
kubectl create secret tls lab.hq.solidrust.net-tls --cert=${CERT_DIR}/config/live/lab.hq.solidrust.net/fullchain.pem --key=${CERT_DIR}/config/live/lab.hq.solidrust.net/privkey.pem -n cert-manager

kubectl create secret tls star.lab.hq.solidrust.net-tls --cert=${CERTWILD_DIR}/config/live/lab.hq.solidrust.net/fullchain.pem --key=${CERTWILD_DIR}/config/live/lab.hq.solidrust.net/privkey.pem -n default
kubectl create secret tls star.lab.hq.solidrust.net-tls --cert=${CERTWILD_DIR}/config/live/lab.hq.solidrust.net/fullchain.pem --key=${CERTWILD_DIR}/config/live/lab.hq.solidrust.net/privkey.pem -n cert-manager
kubectl create secret tls star.lab.hq.solidrust.net-tls --cert=${CERTWILD_DIR}/config/live/lab.hq.solidrust.net/fullchain.pem --key=${CERTWILD_DIR}/config/live/lab.hq.solidrust.net/privkey.pem -n srt-lab-repo

kubectl create secret tls lab.hq.solidrust.net-tls --cert=${CERT_DIR}/config/live/lab.hq.solidrust.net/fullchain.pem --key=${CERT_DIR}/config/live/lab.hq.solidrust.net/privkey.pem -n ingress-nginx
kubectl create secret tls star.lab.hq.solidrust.net-tls --cert=${CERTWILD_DIR}/config/live/lab.hq.solidrust.net/fullchain.pem --key=${CERTWILD_DIR}/config/live/lab.hq.solidrust.net/privkey.pem -n ingress-nginx
```


### TLS test app
```bash
kubectl apply -f solidrust.net/apps/hello-world.yaml
kubectl apply -f solidrust.net/apps/hello-world-ingress.yaml
```

#### Example Ingress
```yaml
#An example Ingress that makes use of the controller:
  apiVersion: networking.k8s.io/v1
  kind: Ingress
  metadata:
    name: example
    namespace: foo
  spec:
    ingressClassName: nginx
    rules:
      - host: www.example.com
        http:
          paths:
            - backend:
                service:
                  name: exampleService
                  port:
                    number: 80
              path: /
    # This section is only required if TLS is to be enabled for the Ingress
    tls:
      - hosts:
        - www.example.com
        secretName: example-tls

#If TLS is enabled for the Ingress, a Secret containing the certificate and key must also be provided:

  apiVersion: v1
  kind: Secret
  metadata:
    name: example-tls
    namespace: foo
  data:
    tls.crt: <base64 encoded cert>
    tls.key: <base64 encoded key>
  type: kubernetes.io/tls
```