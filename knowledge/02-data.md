# PostgreSQL: схема, миграции, seed

Статус: реализованы модель, миграция `20260923201152_InitialCreate`, model snapshot, детерминированный seed `sales-demo-v1` и автоматическая Compose-инициализация. EF Core Design / Npgsql EF provider / локальный dotnet-ef закреплены на 8.0.11. PostgreSQL — 16.15-alpine. Рабочий запуск описан в [06-docker.md](06-docker.md).

## Таблицы

Все поля NOT NULL, если явно не указано иное. Id — uuid PK, генерируются приложением. Денежные поля numeric(18,2), C# decimal. Временные отметки timestamptz, приложение записывает UTC.

| Таблица | Колонки помимо id |
|---|---|
| managers | name varchar(200), initials varchar(8), is_active boolean default true |
| customers | name varchar(200), company varchar(200) |
| categories | name varchar(100), sort_order integer default 0 |
| products | category_id uuid FK, name varchar(200), is_active boolean default true |
| sales | sale_date timestamptz, manager_id uuid FK, customer_id uuid FK, status varchar(16), currency char(3) default 'USD' |
| sale_items | sale_id uuid FK, line_number integer, product_id uuid FK, category_id_at_sale uuid FK, product_name_at_sale varchar(200), quantity integer, unit_sale_price numeric(18,2), unit_cost numeric(18,2) |

Техническая таблица `seed_history`: version varchar(100) PRIMARY KEY, applied_at timestamptz NOT NULL; она не имеет uuid id и не является доменной сущностью. `__EFMigrationsHistory` управляется EF Core.

CHECK: status IN ('Paid','Cancelled','Refunded'), currency = 'USD', quantity > 0, line_number > 0, unit_sale_price >= 0, unit_cost >= 0. Дополнительно CHECK `isfinite(sale_date)` и `btrim(...) <> ''` для имён/названий, initials, company и product_name_at_sale; длины ограничены varchar. Входные значения будущий write service должен проверять до SQL, включая максимум два знака денег: numeric(18,2) округляет лишние знаки. CHECK не обеспечивает наличие хотя бы одной позиции: это инвариант агрегата и транзакции seed/будущего write service.

Все FK — RESTRICT на физическое удаление в MVP. Историческая категория позиции может отличаться от текущей категории продукта. Unique: categories(name), sale_items(sale_id,line_number); имена менеджеров, клиентов и продуктов не уникальны.

## Реализованные индексы

Все индексы B-tree; PK создают свои уникальные индексы.

| Индекс | Колонки | Назначение |
|---|---|---|
| ix_sales_status_sale_date | status, sale_date | Paid + диапазон дат для KPI/графиков/разрезов; также произвольный статус |
| ix_sales_manager_id_sale_date | manager_id, sale_date | Выбранный менеджер и диапазон дат, все статусы; покрывает FK manager_id |
| ix_sales_sale_date_id | sale_date DESC, id DESC | Последние продажи всех статусов; стабильная сортировка и будущая keyset pagination |
| ix_sales_customer_id | customer_id | Обратная связь с клиентом и проверка FK |
| ix_products_category_id | category_id | Каталог категории и FK |
| ix_sale_items_product_id | product_id | Продажи продукта и FK |
| ix_sale_items_category_id_at_sale | category_id_at_sale | Исторический категорийный разрез и FK |
| ux_sale_items_sale_id_line_number | sale_id, line_number, UNIQUE | Уникальность номера позиции в чеке, загрузка позиций в порядке строк, покрывает FK sale_id |
| ux_categories_name | name, UNIQUE | Уникальные названия категорий; регистр учитывается |

Порядок `(Status, SaleDate)` выбран для `Status = Paid AND SaleDate >= from AND SaleDate < to`: сначала равенство, затем диапазон. Обратный `(SaleDate, Status)` не добавлен, поскольку date-first доступ уже покрыт `(SaleDate, Id)`. Индекс одного Status малоизбирателен; отдельные ManagerId/SaleId дублировали бы левый префикс составных индексов.

Первоначальный план двух частичных Paid-индексов заменён полными составными: они поддерживают все статусы и не требуют от планировщика доказательства частичного предиката при параметризованном Status. Не добавлены INCLUDE и широкие тройные индексы без измерений. EXPLAIN ANALYZE на репрезентативных данных остаётся будущей проверкой производительности; тесты корректности схемы его не заменяют. Обоснование связей/индексов — ADR-010 в 07-decisions.

## План запросов

Вначале фильтровать sales по UTC-границам и Paid, затем агрегировать позиции до Sale.Id для KPI/менеджеров. Иначе JOIN позиций увеличит число чеков. Для категорий и продуктов агрегировать строки позиций. Не загружать все чеки в память. Дни группировать в отчётной timezone; недостающие даты дополнить нулями после SQL-агрегации. Recent sales: сначала страница продаж, затем проекция её позиций, без N+1. Count страницы и items читать в одной repeatable-read транзакции.

Денежные суммы вычислять без округления промежуточных групп; цены имеют два знака, quantity целое. Проверить переполнения/лимиты JSON number на предполагаемом объёме (см. ADR). При группировке по исторической категории один продукт может попасть в несколько категорий: top-products группировать по ProductId + CategoryIdAtSale, а DTO id делать составным непрозрачным ключом `productUuid:categoryUuid`.

## Миграции

`Infrastructure/Persistence/Migrations/20260923201152_InitialCreate.cs` создаёт шесть доменных таблиц, seed_history, все CHECK/FK/индексы. Designer и SalesDbContextModelSnapshot сохранены в репозитории. Down удаляет таблицы в порядке зависимостей и означает потерю данных — это не обычный перезапуск.

Приложение регистрирует SalesDbContext в DI через ConnectionStrings__SalesDatabase. Design-time factory позволяет генерировать миграции без работающей БД; для database update требуется реальное подключение через env либо --connection.

```sh
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project sales-performance-api
dotnet ef migrations add MigrationName --project sales-performance-api --output-dir Infrastructure/Persistence/Migrations
```

При запуске Compose одноразовый `database-init` выполняет опубликованное приложение с `--initialize-database`. DatabaseInitializer держит session advisory lock `78234001` на одном открытом соединении, затем вызывает MigrateAsync и, если разрешено, DemoSeeder. API сам миграции не запускает. Это уточнение исходного плана migration bundle: отдельный job сохранён, но использует тот же runtime image и не требует SDK/dotnet-ef в контейнере.

EF Core 8 не имеет встроенной блокировки миграций: все конкурентные автоматические инициализаторы должны использовать эту CLI-команду. Параллельный ручной `dotnet ef database update` этот application lock не соблюдает; не запускать его одновременно с job. Ошибка миграции/seed завершает job ненулевым кодом и блокирует первый запуск API.

## Воспроизводимый seed sales-demo-v1

Исходники: `Infrastructure/Persistence/Seed/DemoDataGenerator.cs`, `DemoSeeder.cs`, `DemoData.cs`, `SeedHistory.cs`. Генератор использует `Random(20260923)`, фиксированные UUID всех сущностей, фиксированные цены/распределения и даты. Никакие DateTime.Now/Today, локаль, часовой пояс машины или внешняя сеть не участвуют в бизнес-данных.

| Данные | Фактическое количество |
|---|---:|
| Менеджеры | 20: первые 5 сильные, следующие 10 средние, последние 5 слабые |
| Клиенты | 80 |
| Категории | 6: Drones, Cameras, Stabilizers, Batteries, Accessories, Services |
| Продукты | 48, по 8 в категории |
| Продажи | 3600 |
| Позиции | 7990 |
| Paid | 3054 |
| Cancelled | 341 |
| Refunded | 205 |

История: **2025-09-24 — 2026-09-23 включительно**, 365 дней (год, затрагивает 13 календарных месячных групп, первая/последняя неполные). Min timestamp 2025-09-24 08:48:02Z, max 2026-09-23 17:51:45Z. Даты сознательно не смещаются к дню запуска: через год новый seed всё равно создаст те же продажи. Для просмотра позднее выбирать custom range из этого периода; обновление demo window требует новой явно описанной версии набора, не автоматического переписывания старой БД.

Неравномерность обеспечивают:

- веса месяцев: пик ноября/декабря, спад летом; объём декабря 2025 — 616 продаж, июля 2026 — 155;
- пониженная вероятность выходных и вариация соседних дней;
- доли потока менеджеров по весам 8/3/1 на сильного/среднего/слабого;
- 1–4 / 1–3 / 1–2 позиции и разное количество единиц по группам менеджеров, что создаёт разные средние чеки;
- различные базовые цены по категориям/моделям, вариации фактической цены, разная маржа по категориям и менеджерам;
- разные вероятности отмен и возвратов по группам;
- глобальное окно **2026-02-10 — 2026-02-16** без единой продажи; у слабых менеджеров нет продаж в июле и августе;
- отдельные бесплатные/убыточные Paid-позиции для проверки нулевой выручки и отрицательной прибыли. Refunded не моделируется отрицательными ценами/quantity.

Итоговые показатели считаются из позиций, готовые KPI не записываются. Профиль strong/medium/weak управляет генератором, не добавляет новый enum/колонку домена.

## Повторный запуск, сбои и существующие данные

DemoSeeder открывает одну транзакцию и берёт transaction advisory lock `78234002`. Если seed_history содержит sales-demo-v1, данные и applied_at не изменяются. Если маркера нет, а любая доменная таблица либо seed_history уже непуста, загрузка завершается ошибкой вместо смешивания демо с реальными данными. Для такой БД отключить seed (`SEED_ENABLED=false` в Compose).

Все inserts и marker фиксируются одним commit. При ошибке/отмене транзакция откатывается, повторный процесс может загрузить seed с нуля. Успешно применённая миграция при ошибке seed остаётся; это допустимое промежуточное состояние пустой схемы. Маркер не является механизмом восстановления вручную удалённых demo-строк: после его появления повторный seed ничего не «ремонтирует» и не затирает пользовательские правки.

`seed_history.applied_at` — реальное UTC-время успешной загрузки, операционные метаданные; оно различается в двух свежих базах. Все шесть таблиц бизнес-данных воспроизводимы. Изменения генератора с изменением данных требуют новой версии и явного плана перехода, а не изменения sales-demo-v1 на месте.

## Проверки

`dotnet test sales-performance-api.sln --configuration Release` требует Docker. Все PostgreSQL fixtures теперь используют **MigrateAsync**, EnsureCreated больше не применяется. 28 тестов проверяют модель, реальную миграцию, отсутствие расхождения snapshot/model, воспроизводимость генератора при разных CultureInfo и полные fingerprints шести таблиц в двух новых БД, распределения/пустые периоды, конкурентный запуск, сохранение правок при повторе, запрет seed в немаркированную непустую БД и rollback после SQL inserts с успешным повтором.

Первый Compose-start создаёт БД/схему/seed, повторный не меняет бизнес-данные и marker. Нагрузочная оценка индексов через EXPLAIN ANALYZE остаётся отдельным этапом.
