# TicketPortal — цели и архитектура

Этот документ — рабочая карта проекта: зачем он существует, из каких сервисов
должен состоять, и как каждый сервис примерно устроен внутри, исходя из уже
реализованного `Catalog`. Он не описывает то, что ещё не решено окончательно —
такие места помечены как "открытый вопрос" и должны обсуждаться перед
реализацией, а не копировать Catalog по инерции.

Обновляйте этот файл по мере продвижения по шагам (см. `README.md` и
`docs/adr/`) — он должен отражать текущее намерение, а не историю.

## 1. Зачем существует проект

TicketPortal — учебный, а не продуктовый проект. Цель — за 3-4 месяца пройти
путь от идеи до полноценной распределённой системы и на каждом шаге
осознанно применять паттерн **потому что того требует конкретная проблема**,
а не потому что "так принято". Ключевые вещи, которые проект должен
продемонстрировать на практике (и уметь объяснить на собеседовании):

- микросервисная архитектура с самого начала (не monolith-first);
- система под высокой конкурентной нагрузкой (flash-sale домен) —
  distributed locking, optimistic concurrency, idempotency;
- полная observability (метрики, трейсинг, логи);
- Cloud/Kubernetes развёртывание;
- осознанные архитектурные компромиссы (например, почему НЕ Event Sourcing
  везде, почему НЕ синхронный REST между Catalog и Booking — см.
  `docs/adr/0001-catalog-booking-integration-via-outbox.md`);
- нагрузочное тестирование и намеренный поиск perf-багов;
- DevOps: CI/CD, Helm, Terraform.

Домен: продажа билетов на события с фазой "flash-sale" — момент старта
продаж, когда тысячи пользователей одновременно пытаются забронировать одни
и те же места. Именно этот момент — центр архитектурной сложности проекта.

## 2. Общие принципы, применяемые ко всем сервисам

Эти решения зафиксированы в Step 1 и в ADR, они не пересматриваются при
запуске каждого нового сервиса:

- **Clean Architecture + tactical DDD на уровне сервиса**, но без лишней
  многослойности внутри одного небольшого сервиса — на практике это
  выражается как **Vertical Slice** организация (`Features/<UseCase>Handler.cs`),
  как в Catalog, а не отдельные Application/Domain/Infrastructure проекты.
- **Аггрегаты — обычные C#-классы с приватными сеттерами и валидацией в
  конструкторе/методах** (`Guard.IsNotNullOrWhiteSpace`, `DomainException` при
  нарушении инварианта). Никаких анемичных DTO вместо доменной модели.
- **Один сервис — своя база данных** (Database-per-service). Постгрес per
  сервис в `deploy/docker-compose.yml` (`catalog-postgres`, далее по аналогии
  `booking-postgres` и т.д.).
- **Интеграция между сервисами — только асинхронно через RabbitMQ**
  (MassTransit как клиент шины), никогда синхронным REST-запросом в момент
  пиковой нагрузки. Синхронный REST допустим только для read-путей, не
  завязанных на flash-sale (см. ADR 0001).
- **Transactional Outbox** для атомарной публикации интеграционных событий:
  запись в `outbox_messages` в той же транзакции, что и доменное изменение;
  отдельный `BackgroundService`-диспетчер вычитывает и публикует. Пока —
  реализовано руками (не встроенный `AddEntityFrameworkOutbox` от
  MassTransit), сознательно, чтобы понимать механику. Переход на встроенный
  outbox — отдельное будущее упражнение.
- **Inbox / идемпотентность потребителя** — обязательна на стороне любого
  сервиса, который потребляет события, т.к. доставка at-least-once.
  `EventEnvelope.MessageId` = `Id` записи в outbox издателя, используется как
  ключ дедупликации.
- **Общий контракт-пакет** `src/shared/Contracts` — сюда идут только вещи,
  которые обязаны быть одинаковыми на обеих сторонах шины: `EventEnvelope<T>`,
  сами интеграционные события (`Contracts/Events/*`), `Money`, доменные
  исключения и общие middleware (`DomainExceptionHandler`,
  `ProblemDetailsCustomizer`). Сюда НЕ должны попадать вещи, специфичные для
  одного сервиса.
- **Валидация и ошибки** — `ProblemDetails` через `DomainExceptionHandler` +
  `ProblemDetailsCustomizer`, коды ошибок как строковые `title`
  (`EVENT_NOT_FOUND`, и т.п.), не HTTP-статус как единственный сигнал.
- **Кэш** — Redis (`IDistributedCache`) на read-моделях, инвалидация точечным
  ключом при мутации (пример: `cache.RemoveAsync($"event:{id}")` в
  `PublishEventHandler`).
- **.NET 10, central package management, `TreatWarningsAsErrors`.**

## 3. Сервисы: назначение и структура

### 3.1 Catalog — реализован (Step 1)

**Назначение**: read-heavy справочник — площадки (`Venue`), события
(`Event`), места (`Seat`) и их статусы для UI/каталога. Источник состава мест
для Booking через событие `EventPublished`.

**Структура** (шаблон для остальных сервисов):

```
src/services/<Service>/
  Entities/            — доменные агрегаты и enum-ы статусов (Event, Seat, Venue, ...)
  Features/<Area>/     — vertical slice на каждый use case:
                          <UseCase>Handler.cs (обработчик + при необходимости DTO request/response тут же)
                          <Area>EndpointExtensions.cs (регистрация route'ов в MapXxxEndpoints)
  Dtos/                — DTO для ответов API, переиспользуемые между несколькими handler'ами
  Infrastructure/
    <Service>DbContext.cs
    Configurations/    — EF Core IEntityTypeConfiguration на каждый агрегат
    Migrations/
  BackgroundServices/  — OutboxDispatcherService и другие фоновые воркеры
  Program.cs           — DI, MassTransit/RabbitMQ, EF Core, Redis, ProblemDetails, endpoints
```

**Ключевые агрегаты**: `Venue` (площадка, состоит из `SeatMapSection`),
`Event` (жизненный цикл `Draft → Published → Cancelled`, инвариант
`SalesStartAt < StartsAt`), `Seat` (создаётся пакетно только при публикации
события — `PublishEventHandler` разворачивает секции venue в конкретные
места с ценой по категории).

**Публикуемое событие**: `EventPublished` (снимок всех мест события) — см.
ADR 0001. Публикуется через Outbox, не напрямую.

**Открытый вопрос**: обратный поток данных Catalog ← Booking
(`SeatReserved`/`SeatReleased`/`SeatSold` обновляют read-модель `Seat.Status`
в Catalog) — упомянут в ADR 0001 как перспектива, но обработчики этих
событий в Catalog ещё не реализованы.

### 3.2 Booking — Step 2, не реализован

**Назначение**: write-heavy сервис резервирования мест — самый нагруженный
узел системы, ради которого вообще выбран этот домен. Не читает Catalog
синхронно: строит и хранит собственную проекцию мест из `EventPublished`
(ADR 0001) и дальше живёт автономно, включая во время недоступности Catalog.

Единица работы — не отдельное место, а **бронь (`Reservation`)**: клиент за
один запрос может удерживать несколько мест события, и вся группа должна
захватываться/освобождаться атомарно (либо все запрошенные места, либо ни
одного). Класс агрегата назван `Reservation`, а не `Booking` — совпадение
имени класса с корневым неймспейсом сервиса (`Booking`) ловится компилятором
как коллизия (`CS0118: 'Booking' is a namespace but is used like a type`) в
любом файле, где рядом оказываются `using Booking.Entities;` и сам
неймспейс `Booking`; проверено сборкой. API-ресурс (`/api/bookings`) и
доменный термин "бронь" остаются прежними — расхождение только в имени
C#-класса.

**Жизненный цикл**:

```
Seat (в Booking):        Available → Held → Sold
                                    ↘ Released/Expired → Available

Reservation:  Held → Confirmed
                  ↘ Released (клиент отменил)
                  ↘ Expired  (не подтверждена вовремя)
```

**Ключевые агрегаты** (реализованы в `Entities/`):

- `Seat` — собственная проекция (не копия Catalog 1-в-1):
  `Id, EventId, SectionId, Row, Number, Price (Money), Category, Status,
  RowVersion (xmin)`. `Id` совпадает с `SeatId` из `EventPublished` — это
  внешняя идентичность, а не локально генерируемая.
- `Reservation` (агрегат-корень) — `Id, EventId, CustomerId?, Status,
  Seats: IReadOnlyList<ReservationSeat> { SeatId }, CreatedAt, ExpiresAt,
  ConfirmedAt?`. Инварианты в конструкторе/методах: нельзя создать пустую
  бронь; `Confirm()`/`Release()`/`Expire()` допустимы только из `Held`.
- `InboxMessage` — дедуп потребления `EventPublished` по
  `EventEnvelope.MessageId` (см. п.2).
- `OutboxMessage` — та же структура, что в Catalog, публикация через
  `OutboxDispatcherService`.
- `IdempotencyKey` — `Key` (из заголовка `Idempotency-Key`), `RequestHash`,
  `ResponseStatusCode`, `ResponseBody`, `ExpiresAt`. Это **бизнесовая**
  идемпотентность клиентского запроса "забронировать", отдельная от
  транспортной (`MessageId`) — повторный `POST /api/bookings` с тем же
  ключом возвращает прежний результат, а не создаёт вторую бронь.

**События**:

- Потребляет `EventPublished` (Catalog → Booking), сидирует таблицу `Seats`.
- Публикует (новые типы в `Contracts.Events`, через Outbox):
  `SeatsHeld(BookingId, EventId, SeatIds, ExpiresAt)`,
  `BookingConfirmed(BookingId, EventId, SeatIds)`,
  `BookingReleased(BookingId, EventId, SeatIds, Reason: UserCancelled | Expired)`.
  Потребители в перспективе — Order (Step 4) и Catalog (обновление read-модели
  `Seat.Status`, открытый вопрос из п.3.1).

**Эндпоинты**:

```
POST   /api/bookings                — удержать N мест события
       headers: Idempotency-Key (обязателен)
       body: { EventId, SeatIds[] }
       → 201 { BookingId, Status: Held, ExpiresAt }
       → 409 если хотя бы одно место уже не Available

POST   /api/bookings/{id}/confirm   — Held → Confirmed
POST   /api/bookings/{id}/release   — Held → Released (явная отмена клиентом)
GET    /api/bookings/{id}           — статус брони

GET    /api/events/{eventId}/seats  — живая карта мест (Available/Held/Sold)
                                       из собственной проекции Booking —
                                       во время flash-sale именно Booking,
                                       а не Catalog, источник правды
```

**Решение по concurrency-механизму** (закрывает прежний открытый вопрос —
Redis lock vs optimistic concurrency не взаимоисключающие, у них разные
роли):

- **Optimistic concurrency (`RowVersion`/`xmin`)** — основной механизм защиты
  от double-booking на уровне одной строки `Seat`:
  `UPDATE ... WHERE Id=@id AND Status='Available' AND xmin=@rowVersion`.
  Конфликт → retry с бэкоффом N раз → 409 "место уже занято".
- **Redis distributed lock** — нужен не для одного места, а чтобы набор мест
  внутри одной `Booking` захватывался атомарно: без распределённой
  транзакции чистый optimistic concurrency не гарантирует "либо все места,
  либо ни одного" при гонке параллельных запросов на пересекающийся набор
  мест. Лок берётся на уровне запроса (например `lock:event:{eventId}:seats`)
  только на время самой транзакции обновления, а не на весь TTL брони.
- **TTL экспирации** брони — источник истины в Postgres (`ExpiresAt`),
  проверяется лениво + подчищается фоновым
  `ReservationExpirationSweeper : BackgroundService` (сканирует
  `Held AND ExpiresAt < now()`, публикует `BookingReleased(Reason=Expired)`).
  Чистый Redis-TTL для этого не годится: истечение ключа в Redis — "тихое"
  событие, для надёжной публикации доменного события всё равно нужен опрос
  БД или keyspace-notifications, что сложнее и менее надёжно, чем polling
  собственной БД.

**Структура папок** (шаблон из п.3.1):

```
src/services/Booking/
  Entities/            Seat.cs, SeatStatuses.cs, Reservation.cs,
                        ReservationSeat.cs, ReservationStatuses.cs,
                        OutboxMessage.cs, InboxMessage.cs,
                        IdempotencyKey.cs
  Features/
    Bookings/          CreateBookingHandler.cs, ConfirmBookingHandler.cs,
                        ReleaseBookingHandler.cs, GetBookingByIdHandler.cs,
                        BookingEndpointExtensions.cs
    EventSeats/        GetEventSeatsHandler.cs, EventSeatsEndpointExtensions.cs
    Integration/       EventPublishedConsumer.cs
  Dtos/                BookingDto.cs, SeatDto.cs
  Infrastructure/
    BookingDbContext.cs
    Configurations/    SeatConfiguration.cs, ReservationConfiguration.cs, ...
    Migrations/
  BackgroundServices/  OutboxDispatcherService.cs, BookingExpirationSweeper.cs
  Program.cs
```

### 3.3 Contracts / messaging — Step 3, частично реализовано

Не отдельный сервис, а расширение инфраструктуры: транзакционный Outbox уже
есть в Catalog (шаблон для остальных сервисов), предстоит:
- вынести общий переиспользуемый кусок Outbox-механики (сущность
  `OutboxMessage`, диспетчер) в переиспользуемый код, если паттерн
  повторяется 1-в-1 в нескольких сервисах — **но только после того, как он
  реализован руками во втором сервисе и стало видно, что действительно
  дублируется** (преждевременная абстракция после одного применения не
  нужна).
- добавить Inbox-паттерн (таблица обработанных `MessageId` на стороне
  потребителя) — первый раз понадобится в Booking.

### 3.4 Order — Step 4, реализован (happy path + компенсация, Payment — мок)

**Назначение**: оркестрирует покупку как **сагу** (`MassTransitStateMachine`)
поверх уже существующей Held-брони, явно управляя компенсацией при отказе
(оплата не прошла → освободить место).

**Ключевое архитектурное решение**: сама фаза Hold (`POST /api/bookings`) в
сагу не входит и остаётся прямым синхронным REST-вызовом клиент → Booking,
как и раньше. Это горячий flash-sale путь с optimistic concurrency + Redis
lock, где клиенту критично мгновенно получить `409`, если место занято;
заворачивать его в сагу означало бы добавить лишний хоп через шину именно
там, где приложение обязано быть быстрым — то есть прямо против цели проекта
(п.1). Клиент-серверный REST не нарушает принцип "асинхронно между
сервисами" из п.2 — тот принцип про интеграцию сервис-сервис, а не про
клиентские вызовы. Order-сага начинается только после того, как у клиента
уже есть `Held`-бронь, и оркестрирует `Confirm → Payment → (Release при
отказе)`.

**Корреляция**: `OrderState.CorrelationId` (Id саги/заказа) намеренно равен
`ReservationId`, а не отдельно сгенерированному `OrderId` — одна Held-бронь
конвертируется ровно в один заказ, и это делает тривиальной корреляцию
входящих событий Booking (`BookingConfirmed`/`BookingReleased` несут
`ReservationId`, а не `OrderId`) без отдельной таблицы соответствий.

**Сага (`OrderStateMachine`)**:

```
(none) --SubmitOrder--> AwaitingConfirmation
AwaitingConfirmation --BookingConfirmed, оплата OK--> Completed
AwaitingConfirmation --BookingConfirmed, оплата отказ--> AwaitingCompensation
AwaitingConfirmation --BookingReleased (истёк холд)--> Failed
AwaitingCompensation --BookingReleased (компенсация подтверждена)--> Failed
```

`Completed`/`Failed` — обычные состояния саги, а не встроенный `Final`:
инстанс не удаляется после завершения (в отличие от типичного
`SetCompletedWhenFinalized()`), потому что `GET /api/orders/{id}` должен
продолжать отдавать финальный статус.

Все исходящие сообщения саги (`ConfirmReservation`/`ReleaseReservation` в
Booking, `OrderCompleted`/`OrderFailed` наружу) идут не напрямую через
`Publish`, а через тот же ручной Outbox, что и в Catalog/Booking:
`OrderDbContext.OutboxMessages`, дописываемый в ту же транзакцию, что
сохраняет состояние саги (EF saga-репозиторий MassTransit настроен на
`ExistingDbContext<OrderDbContext>`, поэтому это одна и та же транзакция) —
иначе переход состояния и отправка сообщения не были бы атомарны.

**Payment**: `IPaymentGateway` с реализацией `PaymentServiceGateway` —
request/response через шину (`MassTransit IRequestClient<ProcessPayment>`)
к отдельному сервису Payment (см. п.3.5), вызываемой синхронно внутри саги.
Сага (`OrderStateMachine`) не изменилась при переходе от прежней
`FakePaymentGateway` к реальному сервису — она видит только интерфейс.

**Новые контракты**: `Contracts.Commands.ConfirmReservation`,
`Contracts.Commands.ReleaseReservation`, `Contracts.Commands.SubmitOrder`,
`Contracts.Events.OrderCompleted`, `Contracts.Events.OrderFailed`. Booking
получил consumer'ы для `ConfirmReservation`/`ReleaseReservation` с тем же
Inbox-дедупом, что и `EventPublishedConsumer`; HTTP-эндпоинты
`/api/bookings/{id}/confirm|release` при этом не изменились — переиспользуют
ту же доменную логику (`ConfirmReservationHandler.ConfirmAsync` /
`ReleaseReservationHandler.ReleaseAsync`), просто без немедленного
`SaveChanges`, чтобы consumer мог сохранить результат одной транзакцией с
записью в Inbox.

**Эндпоинты**:

```
POST /api/orders            — { ReservationId, CustomerId?, Amount, Currency }
                               публикует SubmitOrder, саги ещё не существует
                               в момент ответа → 202 Accepted { OrderId }
GET  /api/orders/{id}       — статус саги (Id == ReservationId)
```

### 3.5 Payment — Step 4, реализован

**Назначение**: идемпотентный шлюз оплаты (в учебных целях — мок реального
провайдера с намеренной нестабильностью для отработки retry/idempotency).
Реальный провайдер (Stripe) пока не подключён — `ProcessPaymentConsumer`
сам случайно проваливает часть запросов (`PaymentGatewayOptions.FailureRate`,
как раньше `FakePaymentGateway` внутри Order).

**Ключевое архитектурное решение**: интеграция с Order — не
publish/subscribe интеграционного события, а MassTransit request/response
(`IRequestClient<ProcessPayment>` → `IConsumer<ProcessPayment>` →
`context.RespondAsync`). Сага должна получить результат оплаты синхронно в
рамках текущей обработки `BookingConfirmed`, а не продолжать работу по
отдельному входящему событию — поэтому здесь оправдан RPC-паттерн поверх
шины, а не Outbox/Inbox, как в остальных интеграциях сервис-сервис.

**Идемпотентность**: по бизнес-ключу платежа, а не по `MessageId` шины —
двух разных гарантий идемпотентности стоит коснуться явно, транспортная и
бизнесовая. `PaymentTransaction.Id` (класс назван не `Payment` — коллизия с
корневым неймспейсом сервиса, см. аналогичное решение для
`Reservation`/`Booking` в п.3.2) равен `OrderId` (= `ReservationId` саги).
Повторный `ProcessPayment` с тем же `OrderId` (redelivery запроса при
at-least-once доставке через шину) находит уже сохранённую запись и
возвращает её результат вместо повторного списания у провайдера — отдельный
Inbox по `MessageId` не нужен, бизнес-ключ уже накрывает этот случай.

**Эндпоинты**: `GET /api/payments/{orderId}` — статус платежа, для отладки.

**Структура папок** (шаблон из п.3.1, без Outbox/Inbox — они не нужны для
чистого request/response):

```
src/services/Payment/
  Entities/            PaymentTransaction.cs, PaymentStatuses.cs
  Features/
    Payments/          ProcessPaymentConsumer.cs, GetPaymentByOrderIdHandler.cs,
                        PaymentEndpointExtensions.cs
  Dtos/                PaymentDto.cs
  Infrastructure/
    PaymentDbContext.cs
    Configurations/    PaymentTransactionConfiguration.cs
    Migrations/
    PaymentGatewayOptions.cs
  Program.cs
```

### 3.6 API Gateway (YARP) — Step 5, не реализован

Единая точка входа, маршрутизация к сервисам, вероятно агрегация ответов для
read-путей UI.

### 3.7 Identity (Keycloak) — Step 5, не реализован

OAuth2/OIDC, сервисы принимают JWT, авторизация на уровне endpoint'ов.

## 4. Инфраструктура и наблюдаемость (Step 6-9, не реализовано)

- OpenTelemetry end-to-end трейсинг через шину и HTTP между сервисами.
- Prometheus/Grafana/Loki — метрики и логи; первая бизнес-метрика,
  предложенная ещё в ADR 0001 — задержка обработки Outbox / размер очереди
  необработанных сообщений в `OutboxDispatcherService`.
- Polly — политики устойчивости (retry/circuit breaker) на синхронных
  вызовах между сервисами и к внешним системам.
- Kubernetes (kind) + Helm umbrella chart + HPA — авто-масштабирование
  read-heavy Catalog отдельно от write-heavy Booking, чтобы демонстрировать
  разную нагрузочную профиль.
- Terraform (Azure) + GitHub Actions CD.
- Нагрузочное тестирование (NBomber/k6) сфокусировано на flash-sale сценарии
  в Booking; намеренные perf-баги вносятся и диагностируются как отдельное
  упражнение.

## 5. Как пользоваться этим документом при добавлении нового сервиса

1. Сначала явно обсудить и зафиксировать архитектурное решение (ADR), если
   оно нетривиально или расходится с шаблоном Catalog — не копировать слепо.
2. Взять структуру папок из раздела 3.1 как отправную точку.
3. Обновить этот файл: перевести секцию сервиса из "не реализован" в
   "реализован", описать реальные агрегаты и события по факту кода, а не по
   плану.
4. Дополнить README.md таблицей статусов по шагам, если она там есть.
