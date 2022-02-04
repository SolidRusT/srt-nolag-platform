```bash
docker build -t srt-link-bot:1.0.0 .
docker tag srt-link-bot:1.0.0 repo.lab.hq.solidrust.net:5000/srt-link-bot:1.0.0
docker push repo.lab.hq.solidrust.net:5000/srt-link-bot:1.0.0
```

image is now stored in: `repo.lab.hq.solidrust.net:5000/srt-link-bot:1.0.0`