```bash
docker build -t srt-link-bot:1.0.0 .
docker tag srt-link-bot:1.0.0 repo.lab.hq.solidrust.net:5000/srt-link-bot:1.0.0
docker push repo.lab.hq.solidrust.net:5000/srt-link-bot:1.0.0
```

image is now stored in: `repo.lab.hq.solidrust.net:5000/srt-link-bot:1.0.0`

kubectl create namespace srt-link-bot
kubectl apply -f k8s-srt-link-bot.yaml -n srt-link-bot

## microk8s

sudo docker push localhost:32000/srt-link-bot:1.0.0
