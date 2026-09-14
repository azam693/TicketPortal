# Docker и Docker Compose — учебник на примере Catalog

Разбор с нуля: как устроен Docker, что означает каждое поле в нашем
`docker-compose.yml`, какими командами всё это жить.

---

## Часть 1. Модель Docker

### 1.1. Проблема, которую решает Docker

«У меня на машине работает, у тебя — нет». Причина — разное окружение: версия
Postgres, версия .NET, переменные, системные библиотеки. Docker упаковывает
приложение **вместе с окружением** в изолированный, воспроизводимый пакет.

В отличие от виртуальной машины, контейнер не тащит с собой гостевую ОС — он
использует ядро хоста и изолируется средствами ядра Linux (namespaces — что процесс
видит; cgroups — сколько ресурсов ему можно). Поэтому контейнер стартует за
доли секунды и весит десятки МБ, а не гигабайты.

### 1.2. Три главных понятия

| Понятие | Что это | Аналогия в коде |
|---|---|---|
| **Image (образ)** | неизменяемый шаблон: файловая система + метаданные (какую команду запускать, какие порты, какие переменные) | класс |
| **Container (контейнер)** | запущенный экземпляр образа со своим слоем изменяемых данных | объект (`new`) |
| **Registry (реестр)** | хранилище образов. По умолчанию — Docker Hub | NuGet / npm registry |

Из одного образа `postgres:17-alpine` можно поднять сколько угодно контейнеров.

### 1.3. Образы состоят из слоёв

Образ — это стопка слоёв (layers), каждый слой = разница в файловой системе.
`postgres:17-alpine` — это слои: `alpine` → пакеты → бинарники Postgres →
скрипты инициализации. Слои кешируются и переиспользуются: если два образа
основаны на `alpine`, этот слой на диске один.

Тег (`17-alpine`, `7-alpine`, `latest`) — это указатель на конкретную версию
образа. `latest` в проде — плохо (непредсказуемо, что подтянется), поэтому мы
пиним мажорные версии: `postgres:17-alpine`, `redis:7-alpine`.

Суффикс `-alpine` — образ на базе дистрибутива Alpine Linux (~5 МБ вместо ~120 МБ
у Debian). Меньше размер, меньше поверхность атаки; иногда есть нюансы с glibc,
но для Postgres/Redis официальные alpine-образы стабильны.

### 1.4. Жизненный цикл контейнера

```
docker pull   → скачать образ из реестра
docker create → создать контейнер из образа (ещё не запущен)
docker start  → запустить
docker stop   → послать SIGTERM, подождать, потом SIGKILL
docker rm     → удалить остановленный контейнер
```

`docker run` = `pull` (если нужно) + `create` + `start` за один раз.

Важно: когда контейнер удаляют, **исчезает его изменяемый слой** — всё, что
процесс записал в файловую систему контейнера. Чтобы данные жили дольше
контейнера, их выносят в **volume** (см. часть 3).

---

## Часть 2. Зачем Docker Compose

Одиночные контейнеры запускают так:

```bash
docker run -d --name tp-catalog-postgres \
  -e POSTGRES_USER=catalog -e POSTGRES_PASSWORD=catalog -e POSTGRES_DB=catalog \
  -p 5432:5432 \
  -v catalog-postgres-data:/var/lib/postgresql/data \
  postgres:17-alpine
```

…и так для каждого сервиса, руками, каждый раз. **Docker Compose** — это
декларативное описание всего набора сервисов в одном YAML-файле. Ты описываешь
*желаемое состояние*, а `docker compose up` его достигает.

Плюсы:

- одна команда поднимает весь стек (БД + кэш + позже Elastic + Jaeger + RabbitMQ);
- конфигурация в git, ревьюится, воспроизводится у всех одинаково;
- Compose сам создаёт общую сеть, куда сервисы видят друг друга по имени;
- удобное управление зависимостями и порядком запуска.

`docker compose` (с пробелом) — современная версия, плагин Docker CLI.
`docker-compose` (через дефис) — старая на Python, встречается в гайдах, но
считается legacy.

---

## Часть 3. Построчный разбор нашего `docker-compose.yml`

```yaml
name: ticketportal
```

**`name`** — имя проекта Compose. Из него складываются префиксы имён сети и
volume'ов: `ticketportal_default`, `ticketportal_catalog-postgres-data`. Без
этого поля Compose берёт имя папки (`deploy`). Явное имя = предсказуемые
названия ресурсов.

```yaml
services:
```

**`services`** — корневой раздел. Каждый вложенный ключ — один сервис (=
описание одного или нескольких контейнеров). У нас два: `catalog-postgres` и
`catalog-redis`. Имя сервиса — это ещё и DNS-имя внутри Compose-сети: другой
контейнер достучится до БД по хосту `catalog-postgres:5432`.

### 3.1. Сервис `catalog-postgres`

```yaml
  catalog-postgres:
    image: postgres:17-alpine
```

**`image`** — какой образ использовать. `postgres` — репозиторий на Docker Hub,
`17-alpine` — тег. Если образа нет локально, Compose скачает его при первом
`up`. Альтернатива `image` — `build:` (собрать свой образ из Dockerfile); нам
для готовых Postgres/Redis сборка не нужна.

```yaml
    container_name: tp-catalog-postgres
```

**`container_name`** — фиксированное имя контейнера. Без него Compose назовёт
его `ticketportal-catalog-postgres-1` (с числовым суффиксом для масштабирования).
Фиксированное имя удобно для `docker exec -it tp-catalog-postgres ...`, но
мешает запустить несколько копий — для локальной разработки это ок.

```yaml
    environment:
      POSTGRES_USER: ${CATALOG_DB_USER:-catalog}
      POSTGRES_PASSWORD: ${CATALOG_DB_PASSWORD:-catalog}
      POSTGRES_DB: ${CATALOG_DB_NAME:-catalog}
```

**`environment`** — переменные окружения внутри контейнера. Образ `postgres`
читает их при первом старте (когда каталог данных пуст):

- `POSTGRES_USER` / `POSTGRES_PASSWORD` — создаётся суперпользователь с этими
  кредами;
- `POSTGRES_DB` — создаётся стартовая база с этим именем.

Это контракт **конкретного образа**, а не Docker: у `redis` таких переменных
нет, у `mysql` они называются иначе (`MYSQL_ROOT_PASSWORD`). Всегда смотри
раздел «Environment Variables» на странице образа в Docker Hub.

Синтаксис `${VAR:-default}` — подстановка из окружения/`.env` со значением по
умолчанию:

| Запись | Поведение |
|---|---|
| `${VAR}` | пусто, если не задана |
| `${VAR:-foo}` | `foo`, если не задана или пустая |
| `${VAR:?msg}` | ошибка с текстом `msg`, если не задана |

Важно: **`POSTGRES_*` применяются только при инициализации пустого volume**.
Поменял пароль в compose, а volume уже есть — пароль в БД не изменится. Нужен
`docker compose down -v` (снести данные) или `ALTER USER` внутри БД.

```yaml
    ports:
      - "5432:5432"
```

**`ports`** — проброс портов «хост:контейнер». Формат `HOST:CONTAINER`.
`"5432:5432"` = порт 5432 на твоей машине → порт 5432 внутри контейнера.
Именно это позволяет `dotnet run` на Windows подключиться к
`localhost:5432`.

- Если 5432 на хосте занят (локально стоит Postgres), поставь `"5433:5432"` —
  тогда снаружи подключаешься к 5433, а строка подключения внутри Compose-сети
  всё равно использует 5432.
- Без `ports` сервис доступен **только** другим контейнерам внутри
  Compose-сети, но не с хоста. Для чистой архитектуры БД часто вообще не
  пробрасывают наружу — но тогда и мигратор должен ехать в контейнере.
- Кавычки нужны, потому что YAML может понять `60:1` как число в шестидесятеричной
  форме времени. Привычка — всегда брать порты в кавычки.

```yaml
    volumes:
      - catalog-postgres-data:/var/lib/postgresql/data
```

**`volumes`** — монтирование хранилища. Формат `ИСТОЧНИК:ЦЕЛЬ`. Здесь
источник — **именованный volume** `catalog-postgres-data` (Docker сам управляет
его расположением на диске), цель — `/var/lib/postgresql/data`, каталог, где
Postgres держит данные.

Смысл: контейнер снесли/пересоздали/обновили образ → данные остались в volume,
новый контейнер их подхватил.

Три вида монтирования:

| Вид | Синтаксис | Когда |
|---|---|---|
| Named volume | `mydata:/path` | БД, данные, которыми управляет Docker |
| Bind mount | `./local/dir:/path` или `C:\...:/path` | твой исходный код в dev, конфиги |
| Anonymous | `/path` | редко, временное |

`/var/lib/postgresql/data` внутри — это не то же самое, что `-alpine`-нюансы:
именно этот путь у официального образа объявлен как `VOLUME`, туда пишется
кластер БД.

```yaml
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${CATALOG_DB_USER:-catalog} -d ${CATALOG_DB_NAME:-catalog}"]
      interval: 10s
      timeout: 5s
      retries: 5
```

**`healthcheck`** — как Docker проверяет, что сервис не просто «запущен», а
реально готов принимать соединения. Контейнер получает статус
`starting → healthy → unhealthy`.

- **`test`** — команда проверки. `["CMD-SHELL", "..."]` = выполнить строку
  через `/bin/sh -c`. `["CMD", "bin", "arg"]` = выполнить бинарник напрямую без
  шелла. Код возврата 0 → здоров.
- `pg_isready` — штатная утилита Postgres: «сервер отвечает на этом сокете?».
- **`interval`** — как часто проверять (каждые 10 с).
- **`timeout`** — сколько ждать ответа команды, прежде чем счесть проверку
  провалившейся (5 с).
- **`retries`** — сколько провалов подряд до статуса `unhealthy` (5).
- есть ещё `start_period` — «льготное время» на старте, когда провалы не
  считаются (полезно для тяжёлых сервисов вроде Elasticsearch).

Зачем это нужно на практике: другой сервис может через
`depends_on: { condition: service_healthy }` дождаться, пока БД реально
поднимется, а не стартовать раньше неё.

### 3.2. Сервис `catalog-redis`

```yaml
  catalog-redis:
    image: redis:7-alpine
    container_name: tp-catalog-redis
    command: ["redis-server", "--save", "60", "1", "--loglevel", "warning"]
```

**`command`** — переопределяет команду по умолчанию, зашитую в образ (в
терминах Dockerfile — `CMD`). У `redis` дефолт — просто `redis-server`. Мы
добавляем аргументы:

- `--save 60 1` — сбрасывать снапшот на диск (в volume `/data`), если за 60 с
  было ≥1 изменение. Так кэш переживёт перезапуск. Без этого Redis чисто
  in-memory.
- `--loglevel warning` — меньше шума в логах.

`command` во Compose — это argv; альтернативно можно строкой:
`command: redis-server --save 60 1`.

Полей `ports`, `volumes`, `healthcheck` для Redis смысл тот же, что у Postgres.
`redis-cli ping` возвращает `PONG` при живом сервере — на этом healthcheck.

### 3.3. Раздел `volumes` верхнего уровня

```yaml
volumes:
  catalog-postgres-data:
  catalog-redis-data:
```

Именованные volume'ы нужно **объявить** здесь, прежде чем ссылаться на них в
сервисах. Пустое значение (`catalog-postgres-data:`) = «создай с настройками по
умолчанию, драйвер local». Полное имя на диске — `ticketportal_catalog-postgres-data`
(префикс из `name`).

### 3.4. Сеть, которой нет в файле

Мы не описали `networks` — Compose автоматически создаёт сеть
`ticketportal_default` типа `bridge` и подключает к ней оба сервиса. Внутри неё:

- контейнеры видят друг друга по имени сервиса (`catalog-postgres`,
  `catalog-redis`) — встроенный DNS Compose;
- порты между контейнерами открыты все, `ports:` для внутреннего общения не
  нужен;
- изоляция от других Compose-проектов.

Когда появится API Gateway в контейнере, в его строке подключения будет
`Host=catalog-postgres` (имя сервиса), а не `localhost`.

---

## Часть 4. Команды: полный цикл работы

Все команды — из папки `deploy/` (где лежит `docker-compose.yml`). Либо
добавляй `-f path/to/docker-compose.yml` из любого места.

### Запуск и остановка

```bash
docker compose up -d          # создать сеть/volumes, поднять всё в фоне (-d = detached)
docker compose up             # то же, но логи текут в терминал; Ctrl+C останавливает
docker compose stop           # остановить контейнеры, НЕ удалять (данные и контейнеры на месте)
docker compose start          # запустить ранее остановленные
docker compose restart catalog-redis   # перезапустить один сервис
docker compose down           # остановить И удалить контейнеры + сеть (volumes остаются!)
docker compose down -v        # + удалить volumes = полный сброс данных
docker compose down --rmi all # + удалить скачанные образы
```

Разница `stop` vs `down`: `stop` — «пауза на ночь», `down` — «убрать всё, кроме
данных». `down -v` — «начать с чистого листа».

### Наблюдение

```bash
docker compose ps             # список сервисов проекта, статусы, health, порты
docker compose logs           # логи всех сервисов
docker compose logs -f catalog-postgres   # -f = следить в реальном времени
docker compose logs --tail=50 catalog-redis
docker compose top            # процессы внутри контейнеров
docker stats                  # live CPU/RAM/сеть по всем контейнерам
```

### Заглянуть внутрь

```bash
# выполнить команду в работающем контейнере
docker compose exec catalog-postgres psql -U catalog -d catalog
docker compose exec catalog-redis redis-cli

# то же через docker (по container_name)
docker exec -it tp-catalog-postgres bash      # интерактивный шелл; -it = терминал
docker exec -it tp-catalog-redis redis-cli keys '*'

# разовый контейнер из образа (не трогая рабочий)
docker run --rm -it postgres:17-alpine psql --version   # --rm = удалить после выхода
```

`exec` заходит в **уже запущенный** контейнер, `run` создаёт **новый**.

### Применение изменений в compose-файле

```bash
docker compose up -d          # Compose сам сравнит желаемое и текущее,
                              # пересоздаст только изменившиеся сервисы
docker compose up -d --force-recreate catalog-redis   # пересоздать принудительно
docker compose pull           # подтянуть свежие версии образов по тегам
docker compose up -d --build  # пересобрать образы из Dockerfile (если есть build:)
```

### Диагностика ресурсов Docker

```bash
docker ps -a                  # все контейнеры, включая остановленные
docker images                 # локальные образы
docker volume ls              # volumes
docker network ls             # сети
docker system df              # сколько места занято
docker system prune           # удалить всё неиспользуемое (остановленные контейнеры,
                              # висячие образы, неиспользуемые сети). -a — агрессивнее,
                              # --volumes — и volumes тоже. Осторожно.
docker inspect tp-catalog-postgres   # полная JSON-конфигурация контейнера
```

---

## Часть 5. Переменные окружения и `.env`

Compose автоматически читает файл `.env` **из той же папки, что и
docker-compose.yml**. Переменные оттуда подставляются в `${...}` в YAML.

```
deploy/
  docker-compose.yml
  .env            ← реальные значения, в .gitignore
  .env.example    ← шаблон, в git
```

Наш `.env`:

```
CATALOG_DB_USER=catalog
CATALOG_DB_PASSWORD=catalog
CATALOG_DB_NAME=catalog
```

Приоритет (кто победит, если переменная задана в нескольких местах):

1. переменная в шелле (`CATALOG_DB_PASSWORD=secret docker compose up`);
2. `.env` файл;
3. значение по умолчанию в `${VAR:-default}`.

Не путай два разных «environment»:

- `${...}` в YAML — подстановка **на этапе чтения файла Compose** (на хосте);
- раздел `environment:` внутри сервиса — переменные **внутри контейнера** в
  рантайме.

Файл с секретами (`.env`) в git не коммитят — только `.env.example`. Проверь,
что в `.gitignore` есть строка `deploy/.env` (или `.env`).

---

## Часть 6. Dockerfile — кратко (понадобится для самого сервиса Catalog)

Готовые образы (Postgres, Redis) берём как есть. Но свой сервис .NET нужно
*собрать* в образ — это делает `Dockerfile`:

```dockerfile
# этап сборки
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/services/Catalog/Catalog.csproj", "src/services/Catalog/"]
COPY ["src/shared/Contracts/Contracts.csproj", "src/shared/Contracts/"]
RUN dotnet restore "src/services/Catalog/Catalog.csproj"
COPY . .
RUN dotnet publish "src/services/Catalog/Catalog.csproj" -c Release -o /app

# этап рантайма — только ASP.NET runtime, без SDK
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Catalog.dll"]
```

Ключевые инструкции:

| Инструкция | Смысл |
|---|---|
| `FROM` | базовый образ (и начало нового этапа при multi-stage) |
| `WORKDIR` | рабочий каталог для последующих команд |
| `COPY` | скопировать файлы с хоста в образ |
| `RUN` | выполнить команду **во время сборки**, результат становится слоем |
| `EXPOSE` | документирует порт (не публикует его — это делает `ports:`) |
| `ENTRYPOINT` / `CMD` | что запустить при старте контейнера |

**Multi-stage** (два `FROM`): собираем с тяжёлым SDK, а в финальный образ кладём
только бинарники + лёгкий runtime. Итоговый образ в разы меньше.

Порядок `COPY` важен для кеша: сначала копируем `.csproj` и делаем `restore`
(этот слой переиспользуется, пока не менялись зависимости), потом уже весь код.

В Compose такой сервис описывается через `build:` вместо `image:`:

```yaml
  catalog-api:
    build:
      context: ../          # корень репозитория
      dockerfile: src/services/Catalog/Dockerfile
    ports:
      - "5175:8080"
    environment:
      ConnectionStrings__DefaultConnection: "Host=catalog-postgres;Port=5432;Database=catalog;Username=catalog;Password=catalog"
      ConnectionStrings__Redis: "catalog-redis:6379"
    depends_on:
      catalog-postgres:
        condition: service_healthy
      catalog-redis:
        condition: service_healthy
```

Обрати внимание: внутри Compose-сети хост — `catalog-postgres`, а `__` в имени
переменной .NET превращает в `:` (`ConnectionStrings:DefaultConnection`).

---

## Часть 7. Как это ложится на WSL2

- Docker Engine (демон) работает в Linux — либо в дистрибутиве WSL, либо в
  служебной ВМ Docker Desktop (`docker-desktop`).
- Образы, контейнеры, volumes физически лежат **в файловой системе Linux**
  внутри WSL, не на диске C:. Поэтому I/O быстрый.
- `docker` CLI на Windows — тонкий клиент, шлёт команды демону в WSL.
- Проброс `ports: "5432:5432"` слушает на Linux-стороне, а WSL2
  автоматически форвардит `localhost` Windows → `localhost` WSL. Отсюда работает
  `localhost:5432` из `dotnet run` на Windows.
- Bind mount кода: держи репозиторий **внутри WSL** (`~/projects/...`), а не в
  `/mnt/d/...` — монтирование Windows-диска в контейнер медленное. Сейчас проект
  на `D:\`, для запуска только БД/Redis это неважно; станет важно, когда сам
  сервис поедет в контейнер с hot reload.

---

## Часть 8. Мини-шпаргалка

```bash
cd deploy
cp .env.example .env
docker compose up -d            # поднять
docker compose ps               # проверить статусы
docker compose logs -f          # смотреть логи
docker compose exec catalog-postgres psql -U catalog -d catalog   # зайти в БД
docker compose stop             # остановить на время
docker compose down             # убрать контейнеры (данные целы)
docker compose down -v          # снести всё с данными
```

Порядок изучения дальше:

1. поиграть с `up` / `stop` / `down` / `down -v`, посмотреть `docker volume ls`
   до и после;
2. руками сломать healthcheck (поменять `pg_isready` на `false`), увидеть
   `unhealthy` в `docker compose ps`;
3. написать `Dockerfile` для Catalog и добавить сервис `catalog-api` в compose;
4. добавить `elasticsearch` + `kibana`, потом `jaeger` — по одному сервису.
