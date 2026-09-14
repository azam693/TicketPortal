# Локальная инфраструктура Catalog (WSL + Docker)

Базовый уровень: PostgreSQL (write-модель EF Core) + Redis (кэш). ElasticSearch,
логи и трейсы добавим позже отдельными сервисами в этот же compose.

## 0. Предпосылки

- Windows 11 + WSL2 (`wsl --status` должен показать версию 2).
- Docker в WSL. Любой вариант:
  - **Docker Desktop** с включённой интеграцией WSL (Settings → Resources → WSL integration).
  - **Нативный Docker Engine** внутри дистрибутива WSL (`sudo apt install docker.io docker-compose-plugin`,
    затем `sudo usermod -aG docker $USER` и перелогин).
- Проверка: в терминале WSL `docker version` и `docker compose version` работают без sudo.

Приложение Catalog запускается на Windows (`dotnet run`), а контейнеры — в WSL.
WSL2 пробрасывает порты на `localhost`, поэтому из Windows БД видна как `localhost:5432`,
Redis — `localhost:6379`. Отдельная настройка не нужна.

## 1. Поднять контейнеры

Из терминала WSL, в каталоге репозитория:

```bash
cd deploy
cp .env.example .env          # при желании поменяй пароль
docker compose up -d
docker compose ps             # оба сервиса healthy
```

Что поднялось:

| Сервис            | Порт | Логин/пароль/БД            |
|-------------------|------|---------------------------|
| catalog-postgres  | 5432 | catalog / catalog / catalog |
| catalog-redis     | 6379 | без пароля                 |

Данные лежат в именованных volume'ах (`ticketportal_catalog-postgres-data`,
`ticketportal_catalog-redis-data`) и переживают перезапуск контейнеров.

## 2. Проверить подключение

```bash
docker exec -it tp-catalog-postgres psql -U catalog -d catalog -c "\l"
docker exec -it tp-catalog-redis redis-cli ping           # -> PONG
```

## 3. Строки подключения в приложении

Уже прописаны в `src/services/Catalog/appsettings.Development.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=catalog;Username=catalog;Password=catalog",
  "Redis": "localhost:6379"
}
```

Секреты в реальном проекте держат в user-secrets / переменных окружения, но для
локального обучения этого достаточно (файл `.Development` не уходит в прод).

## 4. Миграции EF Core

Один раз поставить инструмент:

```bash
dotnet tool install --global dotnet-ef
```

Создать и применить первую миграцию (из папки сервиса):

```bash
cd src/services/Catalog
dotnet ef migrations add InitialCreate -o Infrastructure/Migrations
dotnet ef database update
```

Проверка, что таблицы появились:

```bash
docker exec -it tp-catalog-postgres psql -U catalog -d catalog -c "\dt"
```

При каждом изменении сущностей: `dotnet ef migrations add <Name>` → `dotnet ef database update`.

## 5. Запустить сервис и проверить сквозняк

```bash
cd src/services/Catalog
dotnet run
```

- `POST /api/catalog/admin/events` создаёт событие → строка в Postgres.
- `GET /api/catalog/events` читает из БД.
- Кэш Redis подключён через `AddStackExchangeRedisCache`; ключи появятся, когда в
  хендлерах будет `IDistributedCache` (cache-aside). Смотреть ключи:
  `docker exec -it tp-catalog-redis redis-cli keys '*'`.

## 6. Повседневные команды

```bash
docker compose stop            # остановить, данные сохранить
docker compose start           # поднять обратно
docker compose down            # удалить контейнеры (volume'ы остаются)
docker compose down -v         # снести вместе с данными (чистый старт)
docker compose logs -f catalog-postgres
```

## 7. Что дальше (следующие итерации)

- **ElasticSearch + Kibana** — добавить сервисы `elasticsearch` и `kibana` в этот
  compose, раскомментировать `ElasticsearchClient` в `Program.cs`, добавить
  `Elasticsearch:Url` в конфиг.
- **Логи/трейсы** — OpenTelemetry Collector + Jaeger (или Grafana Tempo/Loki) как
  сервисы compose; в приложении — пакеты `OpenTelemetry.Extensions.Hosting` и
  экспортёр OTLP.
- **RabbitMQ** — для MassTransit, когда дойдёт до саги Order.

Все они кладутся в `deploy/docker-compose.yml` рядом с текущими сервисами, чтобы
вся локальная среда поднималась одной командой.
