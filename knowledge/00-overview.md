# Sales Performance Dashboard: обзор

Дата первичного анализа: 2026-09-23. Ниже сохранено исходное состояние на момент анализа.

Обновление: реализованы доменные сущности, EF Core конфигурации и миграция InitialCreate, воспроизводимый sales-demo-v1 и автоматический запуск PostgreSQL → database-init → API через `docker compose up --build`. Seed содержит 20 менеджеров, 80 клиентов, 6 категорий, 48 товаров и 3600 продаж за 2025-09-24 — 2026-09-23. Реализованы шесть GET /api/analytics endpoints, серверные расчёты и интеграция frontend. Проверены 40 unit + 68 PostgreSQL/HTTP тестов, Compose и browser smoke. Актуальные данные — [02-data.md](02-data.md), запуск — [06-docker.md](06-docker.md), Refunded — [04-business-rules.md](04-business-rules.md).

## Источники и исходное состояние на момент первичного анализа

- Backend: корень этого репозитория, solution `sales-performance-api.sln`, единственный проект `sales-performance-api/sales-performance-api.csproj` (`net8.0`, nullable, namespace `sales_performance_api`).
- `Program.cs`: controllers, Swagger в Development, HTTPS redirection, authorization middleware без настроенной аутентификации. Единственный маршрут — шаблонный `GET /WeatherForecast`.
- EF Core, Npgsql, DbContext, миграций, доменных сущностей и тестов пока нет. Единственный PackageReference — Swashbuckle.AspNetCore 6.6.2.
- `compose.yaml` содержит только сборку API, без опубликованных портов, БД и frontend. Dockerfile — многоэтапная сборка .NET 8, EXPOSE 8080/8081.
- `global.json`: SDK 8.0.0, rollForward latestMinor; локально установлен SDK 8.0.407 (также 6.0.428 и 9.0.301).
- Указанный `/downloads/project` отсутствует. Фактический frontend: `/Users/aleksandrkorakin/Downloads/project`, отдельный каталог вне backend-репозитория.
- Оба `AI_NOTES.md` пустые; backend `AI_PROMPTS.md` также пуст. Дополнительных AGENTS.md в проверенных деревьях и родительских каталогах не найдено.
- В csproj уже есть виртуальная папка `knowledge/`; документы намеренно размещены в **корне репозитория** `knowledge/`, как общая документация системы.

Frontend: React 18, TypeScript, Vite 5, Tailwind, Radix/shadcn, Recharts, date-fns. `src/main.tsx` монтирует `App.tsx`; переключение dashboard/comparison — локальное состояние, клиентского роутера нет. Все данные берутся из `src/lib/mockData.ts`. HTTP API client, fetch/axios, URL backend, query strings, обработка сетевых ошибок, Vite proxy отсутствуют. Supabase указан зависимостью, но не используется в исходниках.

Проверены исходники `src/`: App, все dashboard-компоненты, period-selector, lib, hooks, UI-компоненты, стили и конфигурация сборки. `node_modules`, скомпилированный `dist`, IDE metadata не являются исходными контрактами. Сетевой поиск по всему `src` и vite.config не выявил API-вызовов. `npm run typecheck` проходит; это не проверка бизнес-корректности.

## Назначение и scope

Система показывает результаты продаж за выбранный календарный период и помогает сравнивать менеджеров, категории и продукты. Backend отвечает за хранение исходных продаж, единые вычисления и REST read API. Источник загрузки реальных продаж пока не определён; первая реализация предполагает миграции и демонстрационный seed.

Сценарии:

1. Выбрать today / last7 / last30 / thisMonth / lastMonth / custom (по умолчанию last30).
2. Получить Revenue, Gross Profit, Margin, число Paid-продаж, Average Check и лучшего менеджера, сравнить с предыдущим периодом той же длины.
3. Переключить рейтинг Gross Profit / Average Check и график Revenue / Gross Profit / Sales Count.
4. Посмотреть выручку и прибыль категорий, топ продуктов, последние продажи всех трёх статусов с пагинацией.
5. Сравнить 2–3 менеджеров по итогам и дневной динамике; вкладка обнаружена в готовом frontend и включена в проект API.

Не входят в этот этап: реализация кода, CRUD/импорт продаж, аутентификация/роли, экспорт, уведомления, полнотекстовый поиск, CRM pipeline и планы продаж. У Export, Bell и Search нет работающих обработчиков. Существующие `funnel-chart`, `recent-deals`, `top-reps`, `revenue-chart`, `category-breakdown` не подключены к App; их модели не требуют endpoints в MVP.

## Навигация

- [Домен](01-domain.md)
- [PostgreSQL, миграции и seed](02-data.md)
- [Реализованный REST API и frontend](03-api.md)
- [Расчёты и проверочные примеры](04-business-rules.md)
- [Архитектура](05-architecture.md)
- [Docker](06-docker.md)
- [ADR, допущения и несовпадения](07-decisions.md)
- [История](changelog.md)
