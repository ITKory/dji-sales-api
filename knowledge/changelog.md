# Changelog

## 2026-09-23 — Миграции, воспроизводимый seed и автоматический Compose

- Добавлены EF Core Design 8.0.11 и локальный dotnet-ef 8.0.11, design-time factory, InitialCreate + Designer + model snapshot. Тестовые БД переведены с EnsureCreated на MigrateAsync.
- Реализован sales-demo-v1: Random(20260923), фиксированные UUID/даты, 20 менеджеров трёх групп, 80 клиентов, 6 категорий, 48 товаров, 3600 продаж и 7990 позиций за 2025-09-24—2026-09-23. Paid=3054, Cancelled=341, Refunded=205.
- Добавлены сезонность, различия среднего чека/маржи, отмены/возвраты, глобальная неделя без продаж, два пустых летних месяца у слабых менеджеров и отдельные бесплатные/убыточные Paid-позиции.
- Добавлены seed_history, транзакционный/идемпотентный DemoSeeder, DatabaseInitializer с advisory locks и CLI --initialize-database. Немаркированная непустая БД не перезаписывается.
- Compose теперь запускает PostgreSQL → migrations+seed job → API; named volume, health checks, локальный порт 5026, .env.example и настройки seed. Dockerfile публикует миграции вместе с приложением и содержит curl для readiness.
- Refunded окончательно зафиксирован в 04-business-rules.md как полный возврат без отрицательной проводки; обновлены актуальные страницы KB и ADR-011.
- Пройдены 28 интеграционных тестов, pending-model-changes отсутствуют. Проверены первый и повторный docker compose up --build -d; SQL counts, полный fingerprint шести бизнес-таблиц и marker подтверждают отсутствие изменений при повторе, health/ready=200.
- Frontend не изменён. PostgreSQL/API оставлены работающими после проверки; dashboard endpoints и frontend integration ещё не реализованы.

## 2026-09-23 — Доменные сущности и EF Core mapping

- Прочитана текущая Knowledge Base в корневой knowledge/: указанный /docs/knowledge-base/ отсутствует.
- Добавлены Manager, Customer, Category, Product, Sale, SaleItem и SaleStatus; дата продажи реализована как SaleDate (UTC) вместо проектного SoldAt.
- Добавлены Npgsql EF provider 8.0.11, SalesDbContext и шесть Fluent API конфигураций: uuid, numeric(18,2), timestamptz, string enum, NOT NULL, CHECK, FK с RESTRICT, уникальность строк чека.
- Реализованы аналитические индексы Status+SaleDate, ManagerId+SaleDate, SaleDate DESC+Id DESC, индексы FK и уникальность имени категории. Обоснование — ADR-010.
- Обновлены 01-domain.md, 02-data.md, 07-decisions.md; имя SaleDate синхронизировано в API/бизнес-правилах, обзор дополнен текущим статусом.
- Добавлен проект интеграционных тестов с Testcontainers: Release сборка и все 21 тест прошли на PostgreSQL 16.15-alpine; проверены фактические индексы/FK, ограничения, исторические снимки, UTC, decimal и агрегация Paid.
- Frontend и Compose не менялись. Миграции, seed, runtime DI/connection string и write-сценарии не входили в этот этап.

## 2026-09-23 — Первичный технический анализ и проектирование

- Изучены репозиторий, csproj/Program/controllers/config/Dockerfile/compose.yaml и указанный AI_NOTES.md (пуст).
- Frontend найден в /Users/aleksandrkorakin/Downloads/project вместо отсутствующего /downloads/project. Проверены приложение, dashboard и UI components, hooks, TypeScript-модели, mock data, форматирование, стили и конфигурация сборки.
- Установлено отсутствие HTTP API-вызовов и существующих транспортных контрактов. Отдельно зафиксированы реальные frontend types и предложенные REST endpoints/DTO/query/ProblemDetails.
- Учтены Dashboard и обнаруженная вкладка Manager Comparison. Неиспользуемые CRM-виджеты и неработающие кнопки отделены от scope.
- Созданы 00-overview.md, 01-domain.md, 02-data.md, 03-api.md, 04-business-rules.md, 05-architecture.md, 06-docker.md, 07-decisions.md и этот changelog в корневой knowledge/.
- Зафиксированы обязательные правила Paid/Cancelled/Refunded, формулы, нулевые значения, включительные даты и Previous Period. Предложены схема PostgreSQL, индексы, миграции, seed, слои Web API и Compose orchestration.
- Описаны ошибки mock daysInRange, несогласованные суммы, неоднозначность лучшего менеджера, formatting/null/pagination/UUID/timezone отличия, минимальные интеграционные изменения frontend.
- Проверка frontend: npm run typecheck завершилась успешно; npm выдал предупреждение о неизвестной env config min-release-age. Backend endpoints и Docker stack не реализованы и не запускались.
- Изменения этого этапа ограничены документацией knowledge/. Frontend, исходники backend, compose и существующие пользовательские изменения не изменялись. Исходное git-состояние уже включало добавленные AI_NOTES.md/AI_PROMPTS.md и изменение csproj с виртуальной папкой knowledge.

## 2026-09-23 — Application analytics

Реализованы PeriodResolver, MetricCalculator, DashboardQueryService, отчётные DTO и PostgreSQL analytics reader. KPI включают previous period, текущего лидера и ежедневные sparklines; dynamics заполняет пропуски нулями. Один параметризованный SQL на вызов, без N+1; Paid-only и полное исключение Refunded. Добавлены DI/options и тесты: 40 unit + 35 integration прошли. Обновлены 03-api, 04-business-rules, 05-architecture, ADR-012; HTTP dashboard endpoints остаются следующим этапом. Frontend не изменён.

## 2026-09-23 — REST API и frontend integration

Реализованы шесть GET /api/analytics endpoints, рейтинг GrossProfit/AverageCheck, категории/доли, top-products, recent pagination. Добавлены строгий query parser, ProblemDetails 400/404/405/500/503, Swagger query descriptions и async cancellation. Рейтинг содержит серверную динамику для сохранения Comparison. Frontend в Downloads/project подключён к API через Vite proxy, mock-генераторы отключены в активном dashboard, добавлены loading/error/retry/abort, nullable delta и server pagination.

03-api.md полностью переписан по фактическому контракту; дополнены 00/04/05/06 и ADR-013. Проверены 40 unit + 68 PostgreSQL/HTTP тестов, frontend production build, Compose rebuild/readiness и browser smoke (шесть маршрутов, сортировка, page, Comparison, Today, error/retry). Схема БД и SeedHistoryConfiguration не изменены.
