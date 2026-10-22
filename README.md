# WarehouseApi — учёт складских товаров

Контрольная точка: простое API-приложение в Docker-контейнере.
Проектная тема — **учёт складских товаров**. API хранит список позиций на складе,
отдаёт остатки, показывает товары ниже минимального запаса и фиксирует приход/расход.

Стек: .NET 10, ASP.NET Core Minimal API, Docker (multi-stage build).

## Структура

```
WarehouseApi/
├── Program.cs            — endpoint'ы API
├── Models/Product.cs     — модель товара и тела запросов
├── WarehouseApi.csproj
├── Dockerfile            — написан вручную, с комментариями к каждой инструкции
├── .dockerignore
├── check.sh              — полный прогон build/run/ps/curl/logs с сохранением выводов
└── docs/                 — текстовые выводы docker ps, запросов к API, docker logs
```

## Endpoint'ы

| Метод | Путь | Что делает |
|---|---|---|
| GET | `/health` | проверка, что сервис жив |
| GET | `/products` | все товары на складе |
| GET | `/products/low-stock` | товары, у которых остаток ниже минимального |
| GET | `/products/{id}` | один товар |
| POST | `/products` | добавить позицию `{sku, name, quantity, minQuantity, price}` |
| POST | `/products/{id}/receive` | приход: `{"amount": N}` — остаток увеличивается |
| POST | `/products/{id}/issue` | расход: `{"amount": N}` — остаток уменьшается, в минус нельзя |

Данные хранятся в памяти, при перезапуске контейнера сбрасываются к стартовым (4 позиции).

## 1. Проверка локально без Docker

```bash
dotnet run --urls http://localhost:8080
curl http://localhost:8080/products
```

## 2. Сборка образа

```bash
docker build -t warehouse-api .
```

## 3. Запуск контейнера с пробросом порта

```bash
docker run -d --name warehouse -p 8080:8080 warehouse-api
```

`-p 8080:8080` — порт 8080 хоста пробрасывается на порт 8080 контейнера, где слушает Kestrel.

## 4. Проверка

Контейнер запущен:

```bash
docker ps
```

Запросы к API (curl, можно и в браузере — `http://localhost:8080/products`):

```bash
curl http://localhost:8080/health
curl http://localhost:8080/products
curl http://localhost:8080/products/low-stock
curl http://localhost:8080/products/2

# приход 400 шт. гаек M8
curl -X POST http://localhost:8080/products/2/receive \
  -H 'Content-Type: application/json' -d '{"amount":400}'

# расход со склада, где остаток 0 — вернёт 400 Bad Request
curl -X POST http://localhost:8080/products/4/issue \
  -H 'Content-Type: application/json' -d '{"amount":5}'

# новая позиция
curl -X POST http://localhost:8080/products \
  -H 'Content-Type: application/json' \
  -d '{"sku":"GLUE-1","name":"Клей ПВА 1 кг","quantity":40,"minQuantity":10,"price":210}'
```

Логи контейнера:

```bash
docker logs warehouse
```

## 5. Всё одной командой

```bash
chmod +x check.sh
./check.sh
```

Скрипт пересобирает образ, поднимает контейнер и сохраняет выводы в `docs/`:
`docker_build.txt`, `docker_ps.txt`, `api_requests.txt`, `docker_logs.txt`.

## Остановка

```bash
docker rm -f warehouse
```

## Что в Dockerfile и зачем

Multi-stage сборка из двух этапов:

1. **build** — образ `mcr.microsoft.com/dotnet/sdk:10.0` с компилятором.
   Сначала копируется только `.csproj` и делается `dotnet restore` — этот слой
   кэшируется, пока не меняются зависимости. Потом копируется весь код и
   выполняется `dotnet publish -c Release`.
2. **runtime** — образ `mcr.microsoft.com/dotnet/aspnet:10.0` без SDK.
   Из этапа build копируется только результат publish. `ENV ASPNETCORE_HTTP_PORTS=8080`
   задаёт порт Kestrel, `EXPOSE 8080` документирует его, `ENTRYPOINT ["dotnet", "WarehouseApi.dll"]`
   запускает приложение.

Итог: образ маленький (без SDK), собирается воспроизводимо, кэш ускоряет пересборку.
