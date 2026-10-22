#!/usr/bin/env bash
# Полный прогон контрольной точки: build -> run -> ps -> запросы к API -> logs.
# Все выводы сохраняются в docs/*.txt — их можно приложить к сдаче вместо скриншотов.
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p docs

IMAGE=warehouse-api
CONTAINER=warehouse
URL=http://localhost:8080

docker rm -f "$CONTAINER" >/dev/null 2>&1 || true

echo "===== docker build ====="
docker build -t "$IMAGE" . 2>&1 | tee docs/docker_build.txt

echo
echo "===== docker run ====="
docker run -d --name "$CONTAINER" -p 8080:8080 "$IMAGE"
sleep 3

echo
echo "===== docker ps ====="
docker ps --filter "name=$CONTAINER" | tee docs/docker_ps.txt

echo
echo "===== запросы к API ====="
{
  echo "\$ curl $URL/health";                     curl -s "$URL/health"; echo; echo
  echo "\$ curl $URL/products";                   curl -s "$URL/products"; echo; echo
  echo "\$ curl $URL/products/low-stock";         curl -s "$URL/products/low-stock"; echo; echo
  echo "\$ curl $URL/products/2";                 curl -s "$URL/products/2"; echo; echo
  echo "\$ curl -X POST $URL/products/2/receive -H 'Content-Type: application/json' -d '{\"amount\":400}'"
  curl -s -X POST "$URL/products/2/receive" -H 'Content-Type: application/json' -d '{"amount":400}'; echo; echo
  echo "\$ curl -X POST $URL/products/4/issue -H 'Content-Type: application/json' -d '{\"amount\":5}'"
  curl -s -X POST "$URL/products/4/issue" -H 'Content-Type: application/json' -d '{"amount":5}'; echo; echo
  echo "\$ curl $URL/products/999";               curl -s "$URL/products/999"; echo
} | tee docs/api_requests.txt

echo
echo "===== docker logs ====="
docker logs "$CONTAINER" 2>&1 | tee docs/docker_logs.txt

echo
echo "Готово. Выводы сохранены в docs/. Контейнер '$CONTAINER' оставлен запущенным."
