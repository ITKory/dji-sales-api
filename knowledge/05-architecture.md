# Архитектура backend

Статус: архитектурный план, частично реализованный. Domain, Persistence, migrations, seed, runtime DI и health endpoints уже работают; Application-сервис KPI и динамики реализован; AnalyticsController реализует шесть маршрутов /api/analytics, frontend подключён к ним. EF Core 8.x и Npgsql provider совместимой основной версии, patch-версии закрепить при реализации. Подключение PostgreSQL через Npgsql EF provider соответствует [официальной документации Npgsql](https://www.npgsql.org/efcore/).

## Структура

```text
sales-performance-api.sln
knowledge/
sales-performance-api/
  Program.cs
  Controllers/
    DashboardController.cs
    ManagersController.cs
    SalesController.cs
  Contracts/
    Common/             # Report, ReportPeriod, Page, query DTO
    Dashboard/          # KPI, dynamics, category, top product DTO
    Managers/           # lookup, ranking, comparison DTO
    Sales/              # recent sale DTO
  Domain/
    Entities/           # Manager, Customer, Category, Product, Sale, SaleItem
    Enums/SaleStatus.cs
  Application/
    Dashboard/          # query service interfaces, report composition
    Managers/           # ranking/comparison use cases
    Sales/              # paginated recent sales query
    Reporting/          # PeriodResolver, MetricCalculator, PeriodRequest, Report, options
  Infrastructure/
    Persistence/
      SalesDbContext.cs
      Configurations/   # Fluent API, precision, FK, CHECK, indexes
      Migrations/
      Seed/             # deterministic dev seed, version marker
    Queries/            # EF implementations of application query interfaces
  Configuration/        # validated database/reporting/seed options
  ErrorHandling/        # ProblemDetails, exception handler, validation mapping
  Health/               # readiness database/schema check
  Dockerfile
  appsettings.json
  appsettings.Development.json
tests/
  SalesPerformance.UnitTests/
  SalesPerformance.IntegrationTests/
deploy/
  frontend.Dockerfile    # future frontend build from external context
  nginx.conf
compose.yaml
.env.example            # future local configuration template
.config/dotnet-tools.json
```

Один production csproj, логические слои папками; не требуются микросервисы, MediatR, AutoMapper, generic repository и отдельные проекты для каждой сущности. В будущем выделить сборки, если нужны жёсткие границы зависимостей. Существующий namespace можно оставить; переименование не нужно для функциональности.

## Ответственность и зависимости

Controllers валидируют transport query и вызывают application use cases. Application разрешает период, применяет единые правила и собирает ответ. Infrastructure реализует query interfaces посредством EF Core и SQL-агрегаций. Domain не зависит от HTTP/EF; Contracts не являются EF entities. Регистрация зависимостей — в Program/DI extensions. DTO projection явная.

PeriodResolver принимает TimeProvider и валидированную ReportingOptions.TimeZone; один now на запрос. ResolvedPeriod внутри приложения содержит local DateOnly, UTC bounds и previous bounds. MetricCalculator централизует zero guards, delta и итоговое округление; не переводит все строки БД в LINQ-to-Objects.

Query services используют AsNoTracking, Select/GroupBy и агрегаты в БД, передают CancellationToken из RequestAborted в ToListAsync/CountAsync и транзакции. Не использовать .Result/.Wait или Task.Run для database IO. Один DbContext scoped на запрос; его операции выполнять последовательно: EF Core не поддерживает одновременные операции одного контекста ([документация Microsoft](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/#avoiding-dbcontext-threading-issues)).

KPI current/previous/sparklines читать в одной read-only repeatable-read транзакции либо одним согласованным SQL-запросом. Аналогично comparison и items+count страницы. Между HTTP-запросами общий снимок не обещается. Сначала без кэша; иначе нужна единая инвалидизация при возврате и запись timezone/range в ключ.

## HTTP и эксплуатация

- Controllers возвращают явные response DTO; System.Text.Json camelCase, строки enums с точным регистром, null сохраняется.
- Единые ProblemDetails, validation и status code handling; query allowlist и проверка дубликатов, чтобы спецификация 03-api не зависела от permissive model binder.
- OpenAPI документирует query, defaults, limits, nullable delta, enum strings, 200/400/404/405/500/503; Swagger UI оставить для Development.
- Логи с traceId, длительностью запроса и статусом; без клиентских имён, SQL параметров с PII и secrets в обычном логировании.
- Health live без БД; ready проверяет доступность БД и ожидаемую версию миграций. Startup options validation до обработки запросов.
- Конфигурация через appsettings + env; ConnectionStrings__SalesDatabase, Reporting__TimeZone, Seed__Enabled. Пароли не в git. CORS — только конкретный dev origin при прямом обращении; при proxy same-origin не нужен.
- Auth отсутствует в требованиях/UI. MVP рассматривается как локальная/internal read-only система; внешний production доступ и его роли требуют отдельного scope, не имитировать защиту наличием UseAuthorization.

## Исходный план после анализа (история этапов)

1. Удалить WeatherForecast при появлении реальных контроллеров; настроить DI/options/error handling.
2. Создать сущности, конфигурации EF, миграцию и идемпотентный dev seed.
3. Реализовать period resolver и правила метрик с unit tests.
4. Реализовать SQL query services и endpoints по 03-api, проверить на PostgreSQL.
5. Добавить Compose postgres/migrate/seed/api/frontend и smoke checks.
6. Подключить слой данных frontend с локальными изменениями props/форматирования, без redesign; backend сам по себе не заменит import mockData.

## Проверка будущей реализации

Unit tests: календарные диапазоны, Previous Period, zero/negative delta, rounding и best manager. Integration tests на настоящем PostgreSQL (контейнер): фильтрация статусов, timestamptz/DST, SQL translation, JOIN без дублирования чеков, топ/сортировки, server pagination, migrations/seed. InMemory provider не доказывает PostgreSQL-семантику.

Contract tests: casing/null/enums, все query errors, ProblemDetails, lookup и comparison, нулевые данные; сверка KPI с daily/category/manager aggregates. UI smoke: переключение каждого периода, обе сортировки, 2–3 менеджера, страницы и ошибки сети. Compose smoke: cold start, readiness, migration failure, повторный старт с volume. На текущем этапе пройдены 40 unit-тестов и 68 интеграционных тестов PostgreSQL/HTTP: периоды/метрики, аналитика, сохранение модели, migrations, воспроизводимость/идемпотентность/rollback/concurrency seed; ранее отдельно проверен Compose. HTTP тесты шести endpoints включают валидацию, 404/405/500/503 и Swagger; browser smoke проверяет frontend через proxy.

## Реализованный bootstrap БД

`Program.cs` регистрирует SalesDbContext, DemoSeeder и DatabaseInitializer. При CLI-флаге `--initialize-database` HTTP сервер не запускается: scoped initializer под PostgreSQL session lock применяет MigrateAsync, затем выполняет seed в отдельной транзакции, после чего процесс завершается. Compose database-init использует этот путь; обычный API migration/seed не выполняет. Init и API используют один runtime image с встроенной миграцией; EF Design/dotnet-ef нужны только для разработки новых миграций.

`SeedHistory` — техническая модель Infrastructure, не седьмая сущность бизнес-домена. `DemoDataGenerator` отделён от persistence и не зависит от часов/БД; DemoSeeder отвечает за транзакцию, версию, блокировку и защиту существующих данных. Seed разрешён явно конфигурацией: false в appsettings, true в локальном Compose согласно текущей задаче. Даты набора фиксированы, не смещаются автоматически.

Health `/health/live` проверяет процесс, `/health/ready` — доступность и актуальность схемы. Для внутреннего Compose HTTP выключен HTTPS redirect. Подробности эксплуатации — 06-docker.md, решение об отклонении от первоначального bundle/dev-profile плана — ADR-011.

## Реализованный сервис аналитики

`Application/Dashboard/IDashboardQueryService` → `DashboardQueryService` зависит только от `IPeriodResolver` и `IAnalyticsReader`. `Application/Reporting/PeriodResolver` использует TimeProvider и ReportingOptions; MetricCalculator хранит правила отношений, delta и округления. DTO KpisResponse/TimeSeriesPointDto и Report находятся рядом с use cases; AnalyticsController возвращает их напрямую. Черновики из Infrastructure/Persistence/Reporting и Infrastructure/Services/DashboardQueryService перенесены в Application.

`Infrastructure/Queries/PostgresAnalyticsReader` реализует IAnalyticsReader через EF Core 8 Database.SqlQuery с параметризованным SQL. Первый CTE агрегирует позиции одного Paid-чека, второй — менеджера/локальную дату; затем присоединяется имя менеджера. Application получает только ManagerDailyAggregate (revenue, cost, salesCount), считает итоговые отношения, выбирает лидера и дополняет пропущенные дни. Объём результата ограничен числом менеджеров × числом дней (до 732 дней для current+previous), а не числом продаж/позиций. Отдельные сущности и navigation collections не материализуются, change tracker пуст.

Каждый вызов KPI выполняет один SQL для обоих периодов, лидера и sparklines. Dynamics выполняет один SQL для текущего периода. Один statement обеспечивает согласованность внутри отчёта; отдельная repeatable-read транзакция не требуется. Разные вызовы могут видеть разные состояния БД. CancellationToken передаётся до ToListAsync; отменённый запрос и невалидный период не запускают SQL.

В Program зарегистрированы singleton TimeProvider/IPeriodResolver и scoped IAnalyticsReader/IDashboardQueryService. ReportingOptions валидируются при запуске, timezone задан в appsettings и может переопределяться Reporting__TimeZone. Application не зависит от EF/HTTP. HTTP error mapping (ApiErrors), AnalyticsQuery, Swagger operation filter и frontend adapter реализованы. Схема EF не изменилась, дополнительная миграция не нужна.

## REST API и frontend (реализовано)

`Controllers/AnalyticsController` → Application DashboardQueryService/AnalyticsDetailsService → IAnalyticsReader/IAnalyticsDetailsReader → PostgresAnalyticsReader/PostgresAnalyticsDetailsReader. `Api/AnalyticsQuery` разбирает query, `ApiErrors` централизует ProblemDetails, `AnalyticsOperationFilter` документирует параметры Swagger. Сервисы/reader scoped; DTO не EF entities.

Рейтинг читает менеджер/день одним SQL для двух периодов и включает дневные ряды для Comparison. Категории и продукты — по одному агрегатному SQL, historical category/name сохраняются. Recent — два SELECT под Repeatable Read, LIMIT/OFFSET до JOIN позиций. Новых схем/миграций нет; SeedHistoryConfiguration не изменена.

Frontend отдельный `/Users/aleksandrkorakin/Downloads/project`: analytics-api.ts задаёт типы и fetch, App управляет запросами/отменой/ошибками, компоненты отображают серверные метрики. Vite dev/preview proxy обеспечивает same-origin /api без CORS. Контракт и запуск полностью описаны в 03-api.md. Lookup менеджеров без текущих продаж и отдельный comparison endpoint пока отсутствуют; текущая Comparison использует рейтинг и его timeSeries.
