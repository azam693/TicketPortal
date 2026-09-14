# Dockerfile для Catalog — разбор до последней строчки

Отвечаю на все вопросы по порядку: что такое build context, куда и зачем
копируется `.csproj`, почему потом `COPY . .`, где окажется собранный проект,
как он запускается, и откуда берётся `/app`.

Разбираемый файл:

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

---

## 1. Базовая механика: что такое сборка образа

Когда ты запускаешь `docker build -f Dockerfile .`, происходит следующее:

1. **Точка `.` в конце — это build context.** Docker берёт содержимое этой
   папки (обычно корень репозитория) и передаёт демону Docker. Всё, что можно
   `COPY` внутрь образа, берётся **только из этого контекста** — Dockerfile не
   имеет доступа к произвольным путям на твоём диске.
2. Docker читает Dockerfile сверху вниз. **Каждая инструкция создаёт новый
   слой** — неизменяемый diff файловой системы поверх предыдущего слоя.
3. Результат — образ: стопка слоёв + метаданные (`WORKDIR`, `ENV`, `EXPOSE`,
   `ENTRYPOINT`).

Ключевой момент, вокруг которого построен весь этот Dockerfile — **кеш слоёв**.

### Как работает кеш

Перед выполнением каждой инструкции Docker проверяет: «а есть ли у меня уже
готовый слой для *этой же* инструкции поверх *того же самого* родительского
слоя?»

- Для `RUN` — сравнивается **текст команды**. `RUN dotnet restore ...` с тем же
  текстом → берётся кеш, команда не выполняется заново.
- Для `COPY` / `ADD` — сравнивается **содержимое копируемых файлов** (их
  контрольные суммы). Изменился хоть один байт в источнике → слой
  пересобирается.
- **Как только одна инструкция промахнулась мимо кеша — все последующие тоже
  выполняются заново.** Кеш «ломается» сверху вниз и не восстанавливается.

Отсюда золотое правило: **редко меняющееся — выше, часто меняющееся — ниже.**

---

## 2. Этап build, строка за строкой

### `FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build`

- Берём официальный образ Microsoft с полным **.NET SDK** (компилятор, `dotnet`
  CLI, MSBuild, NuGet). Он большой (~800 МБ), но нужен только чтобы собрать.
- `AS build` — даём этому этапу имя. Дальше по имени `build` мы вытащим из него
  результат. Это и есть **multi-stage build**: несколько `FROM` в одном файле,
  каждый — отдельный временный образ.

### `WORKDIR /src`

- Создаёт каталог `/src` внутри образа (если нет) и делает его текущим для всех
  последующих `COPY`, `RUN`, `CMD`. Аналог `mkdir -p /src && cd /src`.
- Дальше относительные пути в `COPY` отсчитываются от `/src`. То есть
  `COPY ["src/services/Catalog/Catalog.csproj", "src/services/Catalog/"]`
  положит файл в `/src/src/services/Catalog/Catalog.csproj` внутри образа.

  (Двойной `src/src` выглядит странно, но это норма: первый `src` — папка в
  репозитории, второй — каталог `WORKDIR`. Часто `WORKDIR` называют `/app` или
  `/build`, чтобы не было визуального дубля. Суть не меняется.)

### `COPY ["src/services/Catalog/Catalog.csproj", "src/services/Catalog/"]`

Разберём твой вопрос детально.

**Что копируется:** ровно один файл — `Catalog.csproj`. Это XML-манифест
проекта: список `PackageReference` (NuGet-пакеты и их версии) и
`ProjectReference` (ссылки на другие проекты решения). Никакого кода, только
описание зависимостей.

**Откуда:** из build context (корень репо на хосте), путь
`src/services/Catalog/Catalog.csproj`.

**Куда:** внутрь образа, в `/src/src/services/Catalog/`. Форма записи
`COPY ["источник", "назначение"]` (JSON-массив) — то же самое, что
`COPY источник назначение`, просто безопаснее с пробелами в путях. Если
назначение заканчивается на `/`, Docker понимает его как каталог и кладёт файл
туда под исходным именем.

**Зачем вообще копировать в образ:** потому что `dotnet restore` на следующем
шаге выполняется **внутри образа**, командой `RUN`. Ему нужны `.csproj`, чтобы
понять, какие пакеты качать. Файлов с хоста внутри образа нет по умолчанию —
надо явно положить.

**Зачем отдельно, а не весь проект сразу:** вот здесь и работает кеш. Идея:

```
COPY только .csproj      ← меняется редко (когда трогаешь зависимости)
RUN dotnet restore       ← тяжёлый: качает десятки пакетов из NuGet
COPY . .                 ← весь код, меняется на каждый чих
RUN dotnet publish       ← компиляция
```

Пока ты правишь только `.cs`-файлы, а `.csproj` не трогаешь:

- слой `COPY *.csproj` — **из кеша** (файлы не изменились);
- слой `RUN dotnet restore` — **из кеша** (родитель тот же, команда та же) →
  **пакеты не качаются повторно**, экономия минут на каждой сборке;
- ломается кеш только на `COPY . .`, дальше идёт `dotnet publish`.

Если бы было наоборот — `COPY . .` перед `restore` — то любая правка одной
буквы в коде инвалидировала бы слой копирования, а за ним и `restore`, и Docker
каждый раз заново тянул бы весь NuGet. Медленно.

### `COPY ["src/shared/Contracts/Catalog.csproj"...]` — почему ещё и Contracts

Потому что `Catalog.csproj` содержит
`<ProjectReference Include="..\..\shared\Contracts\Contracts.csproj" />`.
`dotnet restore` для Catalog пойдёт разворачивать зависимости и Contracts тоже.
Если `Contracts.csproj` не положить в образ — restore упадёт с «файл не найден».

Правило: **в образ до `restore` нужно положить `.csproj` всех проектов, которые
прямо или транзитивно указаны в `ProjectReference`.** В большом решении это
обычно делают одной командой (скопировать `*.sln` и все `.csproj` скриптом), но
для двух проектов проще перечислить руками.

Код Contracts (`.cs`-файлы) на этом шаге не нужен — `restore` работает только с
манифестами. Код приедет позже в `COPY . .`.

### `RUN dotnet restore "src/services/Catalog/Catalog.csproj"`

- Выполняется **внутри образа**, в каталоге `/src`.
- Читает `Catalog.csproj` (и через ProjectReference — `Contracts.csproj`),
  скачивает все NuGet-пакеты нужных версий в кеш NuGet внутри образа
  (`~/.nuget/packages`), генерирует `obj/project.assets.json` — «карту»
  разрешённых зависимостей.
- Результат фиксируется в слое. Пока `.csproj` не меняются — этот слой
  переиспользуется, интернет при сборке не нужен.

### `COPY . .`

**Твой вопрос: «зачем опять копировать?»**

Первый `COPY` положил только манифесты (`.csproj`), чтобы закешировать
`restore`. Но чтобы **скомпилировать** проект, нужен весь исходный код: `.cs`,
`appsettings.json`, `Program.cs`, `wwwroot`, миграции и т.д. Вот `COPY . .` их
и приносит.

- Первая `.` — источник: **весь build context** (корень репо на хосте).
- Вторая `.` — назначение: текущий `WORKDIR`, то есть `/src`.

То есть «скопировать всё содержимое репозитория в `/src` внутри образа».

Да, `.csproj`-файлы при этом копируются повторно — и это нормально: они просто
перезапишутся теми же байтами. Стоит это копейки. Важно, что `restore` уже
закеширован *выше* этой строки и повторно не выполнится.

**`.dockerignore`.** Рядом с Dockerfile обычно кладут файл `.dockerignore` —
он как `.gitignore`, но для build context. Туда пишут `bin/`, `obj/`,
`**/node_modules`, `.git/`, чтобы `COPY . .` не тащил в образ мусор сборки с
хоста (он не нужен и ломает кеш). Пример:

```
**/bin/
**/obj/
.git/
.vs/
*.user
```

### `RUN dotnet publish "src/services/Catalog/Catalog.csproj" -c Release -o /app`

**Твой вопрос: «данные опять копируются в образ?»** Нет. `publish` ничего не
копирует с хоста. Он берёт исходники, которые *уже лежат* в `/src` (после
`COPY . .`), **компилирует** их и складывает готовый результат.

- `-c Release` — конфигурация Release: оптимизации компилятора, без отладочных
  проверок.
- `-o /app` — **output directory**. Сюда `publish` кладёт итог. Эта папка
  `/app` создаётся прямо сейчас, этой командой, **внутри образа этапа build**.
- Что окажется в `/app`: `Catalog.dll` (твой скомпилированный код), `Contracts.dll`,
  все DLL зависимостей из NuGet, `Catalog.runtimeconfig.json`,
  `Catalog.deps.json`, `appsettings.json`, `appsettings.Development.json`,
  `web.config`, папка `wwwroot` если есть. Это самодостаточный набор для запуска
  — но всё ещё требует установленного .NET runtime (мы не делали
  self-contained-публикацию).

На этом этап build закончен. У нас есть временный образ `build` весом под
гигабайт, где в `/app` лежит готовое приложение. Тащить весь SDK в продакшн не
нужно — поэтому дальше второй этап.

---

## 3. Этап runtime, строка за строкой

### `FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime`

- **Новый `FROM` = новый чистый образ.** Всё, что было в этапе `build`
  (каталог `/src`, SDK, NuGet-кеш, исходники), сюда **не переносится
  автоматически**. Начинаем с нуля.
- База — `aspnet:10.0`: только **ASP.NET Core runtime** (~220 МБ), без
  компилятора и SDK. Запускать `.dll` он умеет, собирать — нет. Это и есть смысл
  multi-stage: финальный образ маленький и без лишнего инструментария.

### `WORKDIR /app`

**Твой вопрос: «откуда взялась папка app?»**

`WORKDIR /app` **сам создаёт** этот каталог, если его нет (как `mkdir -p`), и
делает текущим. В этом образе (`aspnet:10.0`) каталога `/app` изначально нет —
инструкция его создаёт здесь и сейчас.

Важно: этот `/app` — **другой** `/app`, не тот, что был в этапе build. Это
новый образ, новая файловая система. Совпадение имени — просто договорённость,
можно было назвать `/opt/catalog`.

### `COPY --from=build /app .`

Вот здесь два `/app` встречаются.

- `--from=build` — «источник не с хоста, а из другого этапа сборки по имени
  `build`».
- `/app` — путь **в образе этапа build** (тот самый, куда `dotnet publish -o /app`
  положил результат).
- `.` — назначение: текущий `WORKDIR`, то есть `/app` **в runtime-образе**.

Читается: «возьми содержимое `/app` из этапа build и положи в `/app` здесь».

Именно так готовое приложение переезжает из жирного build-образа в тонкий
runtime-образ. Всё остальное (SDK, исходники, NuGet-кеш) остаётся в build-этапе
и в финальный образ не попадает — его вообще выбрасывают после сборки.

После этой строки в runtime-образе по пути `/app` лежат: `Catalog.dll`,
`Contracts.dll`, зависимости, `appsettings*.json`.

### `EXPOSE 8080`

**Это только документация.** `EXPOSE` **не открывает и не публикует порт**. Он
лишь помечает в метаданных образа: «приложение внутри слушает 8080». Реальный
проброс наружу делается при запуске — `docker run -p 5175:8080` или `ports:` в
compose.

Почему именно 8080: начиная с .NET 8, официальные образы `aspnet` по умолчанию
выставляют `ASPNETCORE_HTTP_PORTS=8080` (раньше был 80, раньше через
`ASPNETCORE_URLS`). Kestrel читает эту переменную и слушает `0.0.0.0:8080`
внутри контейнера. Хочешь другой порт — задай `ASPNETCORE_HTTP_PORTS=5000` в
`environment`.

Обрати внимание: в `Program.cs` есть `app.UseHttpsRedirection()`. В контейнере
HTTPS-порт обычно не настраивают (TLS терминируется на gateway/реверс-прокси),
поэтому этот вызов может спамить предупреждениями или редиректить в никуда —
для контейнерного запуска его часто убирают или оборачивают в
`if (!IsDevelopment)`.

### `ENTRYPOINT ["dotnet", "Catalog.dll"]`

**Твой вопрос: «где билд и как Docker его слушает / за счёт какой команды?»**

- Билд лежит в `/app` (см. выше). `WORKDIR /app` активен, поэтому команда
  выполняется именно оттуда, и `Catalog.dll` находится без указания полного
  пути.
- `ENTRYPOINT` — **команда, которую контейнер запускает при старте**. Форма с
  массивом (`["dotnet", "Catalog.dll"]`) — это «exec form»: запускается напрямую
  процесс `dotnet` с аргументом `Catalog.dll`, **без шелла**. Процесс `dotnet`
  получает PID 1 в контейнере.
- **Docker сам ничего не «слушает».** Порт слушает процесс `dotnet` → внутри
  него ASP.NET Core → Kestrel открывает TCP-сокет на `0.0.0.0:8080`. Docker
  только связывает этот внутренний порт с портом хоста, когда ты передал `-p`.
- Пока процесс `dotnet` жив — контейнер в статусе `running`. Процесс упал или
  завершился — контейнер останавливается с тем же кодом возврата. Один контейнер
  = один основной процесс.

`ENTRYPOINT` vs `CMD`:

| | Назначение | Переопределение при `docker run` |
|---|---|---|
| `ENTRYPOINT` | фиксированное «что запускать» | сложно (нужен `--entrypoint`) |
| `CMD` | аргументы по умолчанию / команда по умолчанию | легко: `docker run img arg1 arg2` |

Часто пишут `ENTRYPOINT ["dotnet", "Catalog.dll"]` + `CMD []`, чтобы можно было
дописать аргументы приложению через `docker run`.

---

## 4. Полная картина: что где лежит

### Во время сборки (этап build, временный образ)

```
/src/                              ← WORKDIR, весь репозиторий (после COPY . .)
  src/services/Catalog/
    Catalog.csproj
    Program.cs, *.cs, appsettings*.json
    obj/  bin/                      ← создано dotnet restore / publish
  src/shared/Contracts/
    Contracts.csproj, *.cs
/app/                              ← результат dotnet publish -o /app
  Catalog.dll
  Contracts.dll
  *.dll (зависимости)
  Catalog.runtimeconfig.json
  Catalog.deps.json
  appsettings.json
  appsettings.Development.json
```

### В финальном образе (этап runtime, то, что реально поедет в реестр)

```
/app/                              ← WORKDIR, сюда COPY --from=build /app .
  Catalog.dll
  Contracts.dll
  *.dll
  Catalog.runtimeconfig.json
  Catalog.deps.json
  appsettings.json
  appsettings.Development.json
+ базовый ASP.NET Core runtime из образа aspnet:10.0
```

Ни `/src`, ни SDK, ни NuGet-кеша, ни исходников `.cs` — только собранные
бинарники. Итоговый размер — ~250 МБ вместо ~900 МБ.

---

## 5. Как это собрать и запустить

### Напрямую через docker

```bash
# из корня репозитория (точка в конце = build context)
docker build -f src/services/Catalog/Dockerfile -t catalog:local .

# запустить, пробросив порт и передав строку подключения
docker run --rm -p 5175:8080 \
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Port=5432;Database=catalog;Username=catalog;Password=catalog" \
  -e ConnectionStrings__Redis="host.docker.internal:6379" \
  catalog:local
```

`host.docker.internal` — специальное DNS-имя: «хост, на котором крутится
Docker». Нужно, когда контейнер приложения обращается к БД, которая
**не в этой же Compose-сети** (например, Postgres ты поднял отдельным
`docker compose` или локально).

`__` (два подчёркивания) в имени переменной .NET читает как `:` — то есть
`ConnectionStrings__DefaultConnection` = `ConnectionStrings:DefaultConnection`
из конфига.

### Через docker compose (правильный способ для этого проекта)

Добавить в `deploy/docker-compose.yml` сервис:

```yaml
  catalog-api:
    build:
      context: ..                                  # корень репозитория
      dockerfile: src/services/Catalog/Dockerfile
    container_name: tp-catalog-api
    ports:
      - "5175:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Host=catalog-postgres;Port=5432;Database=catalog;Username=catalog;Password=catalog"
      ConnectionStrings__Redis: "catalog-redis:6379"
    depends_on:
      catalog-postgres:
        condition: service_healthy
      catalog-redis:
        condition: service_healthy
```

Здесь хост БД — `catalog-postgres` (имя сервиса в Compose-сети), а не
`localhost` и не `host.docker.internal`: контейнеры одной Compose-сети видят
друг друга по именам сервисов.

Запуск:

```bash
cd deploy
docker compose up -d --build        # --build = пересобрать образ catalog-api
docker compose logs -f catalog-api
```

Проверка:

```bash
curl http://localhost:5175/api/catalog/events
```

---

## 6. Ответы на вопросы одним списком

- **Зачем копируется `.csproj` и куда?** В файловую систему образа (этап build,
  каталог под `WORKDIR /src`). Чтобы `dotnet restore`, который выполняется
  внутри образа, увидел список зависимостей.
- **Зачем именно в образ и зачем там сборка?** Docker собирает приложение в
  изолированном воспроизводимом окружении, без зависимости от того, что стоит
  на твоей машине. Всё, что нужно сборке, должно быть внутри образа.
- **Почему остальные файлы не копируются сразу?** Ради кеша слоёв: манифесты
  меняются редко → слой `restore` переиспользуется и не качает NuGet заново.
  Код меняется часто → его копируем отдельной строкой ниже.
- **Зачем `COPY . .` после restore?** Принести исходный код (`.cs` и прочее)
  для компиляции. Restore работает только с манифестами, publish — с кодом.
- **`dotnet publish` опять копирует данные?** Нет. Он компилирует уже
  скопированные исходники и кладёт результат в `/app`.
- **Где билд в образе?** В финальном образе — по пути `/app` (`Catalog.dll` и
  зависимости).
- **Как Docker его «слушает»?** Никак. `ENTRYPOINT ["dotnet","Catalog.dll"]`
  запускает процесс `dotnet`, внутри него Kestrel слушает `0.0.0.0:8080`. Docker
  лишь пробрасывает этот порт на хост при `-p`.
- **Откуда `/app` в этапе runtime?** Его создаёт `WORKDIR /app`. Затем
  `COPY --from=build /app .` наполняет его результатом из этапа build.
