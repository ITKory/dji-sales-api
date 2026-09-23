# Docker: PostgreSQL, миграции и seed

Статус: **реализовано и проверено**. Для локальной backend/demo среды достаточно Docker с Compose; .NET SDK на хосте и ручной запуск миграций/SQL не нужны.

## Запуск

Из корня репозитория:

```sh
docker compose up --build
```

Либо фоновый режим `docker compose up --build -d`. `.env` необязателен: работают локальные значения по умолчанию. `.env.example` содержит доступные overrides.

Последовательность:

1. `postgres` (postgres:16.15-alpine) создаёт sales_performance и ожидается healthy через pg_isready.
2. `database-init` запускает `dotnet sales-performance-api.dll --initialize-database`, применяет EF миграции и загружает sales-demo-v1, затем завершается с кодом 0.
3. `sales-performance-api` запускается только после `service_completed_successfully` для database-init, readiness проверяет доступность БД и отсутствие pending migrations.

`database-init Exited (0)` — нормальное состояние выполненного job. Ошибка даёт ненулевой exit code, API при первом запуске не стартует. Compose conditions поддерживают ожидание healthy service и завершённого job ([документация Docker](https://docs.docker.com/compose/how-tos/startup-order/)). API и init используют один образ; миграции встроены в сборку, runtime не требует EF CLI. Инициализация не привязана к `/docker-entrypoint-initdb.d`, поэтому новые миграции применяются и на существующем volume.

## Адреса и сервисы

| Service | Доступ |
|---|---|
| postgres | postgres:5432 внутри Compose; наружу порт БД не опубликован |
| database-init | Одноразовый job без HTTP-порта |
| sales-performance-api | http://localhost:5026, привязан к 127.0.0.1 |

- `GET /health/live` → 200 `{"status":"healthy"}` без проверки БД.
- `GET /health/ready` → 200 при доступной актуальной схеме, иначе 503 ProblemDetails.
- Swagger в Development: http://localhost:5026/swagger.
- Dashboard endpoints пока не реализованы; шаблонный WeatherForecast сохранён.

Проверка:

```sh
docker compose ps -a
docker compose logs database-init
curl --fail http://localhost:5026/health/ready
docker compose exec postgres psql -U sales_performance -d sales_performance -c "SELECT status, count(*) FROM sales GROUP BY status;"
```

Ожидается Paid=3054, Cancelled=341, Refunded=205; всего 3600 продаж, 7990 позиций. Данные покрывают 2025-09-24 — 2026-09-23, а не скользящее окно от сегодняшнего дня. Полный состав и распределения — [02-data.md](02-data.md).

## Настройки

| Переменная Compose / .env | Default | Назначение |
|---|---|---|
| POSTGRES_DB | sales_performance | Имя БД |
| POSTGRES_USER | sales_performance | Пользователь локальной БД |
| POSTGRES_PASSWORD | sales_demo_local_only | Публичный локальный demo-пароль, заменить при ином развёртывании |
| API_PORT | 5026 | Host HTTP-порт |
| SEED_ENABLED | true | Запуск demo seed; false оставляет только миграции |

Пример: `SEED_ENABLED=false docker compose up --build` создаёт пустую схему. Позднее включение seed заполнит её, если доменные таблицы пусты. Непустая БД без маркера seed отклоняется; для реальных данных использовать false. Изменение POSTGRES_* не меняет автоматически пользователей/пароли уже существующего PostgreSQL volume.

App configuration: `ConnectionStrings__SalesDatabase` передаётся контейнерам через env; `Seed__Enabled` разрешает seed только в initializer. В appsettings по умолчанию false, Compose явно включает по текущему требованию. Connection string обязателен для обычного запуска приложения. Локальный HTTP не перенаправляется на отсутствующий HTTPS endpoint; `Http__UseHttpsRedirection` false в Compose. Production TLS/auth/отдельная deployment identity остаются вне demo scope.

Dockerfile: SDK 8.0 restore/publish → aspnet 8.0 runtime; итоговый процесс non-root, curl установлен для healthcheck. `.dockerignore` исключает bin/obj/.env; `.gitignore` исключает .env и локальные варианты, но сохраняет .env.example. Образы SDK/runtime закреплены по major tag, PostgreSQL — по patch tag; при production release можно закрепить digest отдельно.

## Повторный запуск и хранение

Named volume `pgdata` сохраняет БД между остановками и пересборками. При `docker compose up --build` на той же БД init применяет только отсутствующие миграции; маркер sales-demo-v1 предотвращает повторную загрузку и сохраняет пользовательские правки. Session/transaction advisory locks защищают параллельные initializer/seed processes; данные и marker seed записываются одной транзакцией.

```sh
docker compose down
docker compose up --build -d
```

Эти команды сохраняют данные. **`docker compose down -v` удаляет БД**; использовать только для намеренного сброса локального демо. Следующий up создаст тот же business dataset, но новое operational applied_at. Удаление/редактирование отдельных demo-строк не исправляется автоматическим reseed.

Для повторного выполнения только initializer: `docker compose run --rm database-init` при работающем PostgreSQL. Не запускать параллельно ручной EF CLI database update: он не использует application advisory lock.

## Разработка вне контейнера

```sh
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project sales-performance-api
```

Задать `ConnectionStrings__SalesDatabase` на доступный PostgreSQL; дефолтный Compose не публикует БД наружу. Затем `dotnet run --project sales-performance-api --no-launch-profile -- --initialize-database` применит миграции; добавить `Seed__Enabled=true` в окружение для demo. Для обычного HTTP старта убрать CLI-флаг.

## Frontend

Frontend остаётся отдельным Vite-проектом в Downloads; этот этап поднимает PostgreSQL и backend. Будущая контейнеризация frontend: Node build → nginx, `/api` proxy к sales-performance-api:8080. Она и подключение mock UI к API не входят в текущую реализацию; нет зависимостей Compose от абсолютного Downloads path.

## Подключённый frontend

После `docker compose up --build -d` API обслуживает /api/analytics на localhost:5026. В `/Users/aleksandrkorakin/Downloads/project` выполнить `npm install` при необходимости и `npm run dev`: Vite /api proxy обращается к API. `npm run build` и `npm run preview` проверяют production bundle с таким же proxy. При переопределении API_PORT скорректировать target в vite.config.ts. Frontend-контейнер этим изменением не добавлен. Реальные контракты и примеры curl — 03-api.md.
