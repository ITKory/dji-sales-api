# REST API и интеграция frontend

Статус: реализовано. Этот документ описывает фактические маршруты, query-параметры и JSON текущей версии. Старые предложения `/api/dashboard/*`, `/api/managers/*`, `/api/sales` заменены шестью маршрутами `/api/analytics/*`; старые маршруты не являются aliases и возвращают 404.

## Общий контракт

GET без body; успешный ответ — `application/json`, JSON camelCase. День — `YYYY-MM-DD`, timestamp — UTC ISO 8601 с `Z`. День отчёта определяется сервером в `Reporting__TimeZone` (default Europe/Belgrade), независимо от браузера и timezone сессии PostgreSQL. Валюта USD, суммы/проценты — JSON number, не форматированные строки. Nullable поля сохраняются как null.

Все аналитические ответы имеют оболочку:

```ts
type PeriodKey = 'today' | 'last7' | 'last30' | 'thisMonth' | 'lastMonth' | 'custom';
interface ReportPeriod {
  key: PeriodKey;
  from: string; to: string;
  previousFrom: string; previousTo: string;
  days: number; timeZone: string;
}
interface Report<T> { period: ReportPeriod; currency: 'USD'; data: T }
```

Пустая выборка — 200: нулевые KPI, пустые рейтинги/категории/продукты, нулевая динамика по каждому дню, пустой items у страницы. Это не 404 и не 204. Отдельные вызовы не гарантируют общий snapshot при одновременном изменении продаж.

## Период: общий query для всех шести endpoints

| Параметр | Допустимое значение / default |
|---|---|
| preset | today, last7, last30, thisMonth, lastMonth, custom; default last30, если даты тоже отсутствуют |
| period | Совместимый alias preset; нельзя передавать оба |
| from | ISO date-only, включительно; требует to |
| to | ISO date-only, включительно; требует from |

Режимы запроса:

- `?preset=last7` — preset разрешается сервером.
- `?from=2026-09-01&to=2026-09-07` — автоматически custom.
- `?preset=custom&from=2026-09-01&to=2026-09-07` — явный custom.
- `?period=custom&from=2026-09-01&to=2026-09-07` — совместимый alias.
- Без date query — last30.

Имена параметров регистрозависимы, значения preset/sortBy регистронезависимы. Числовые значения enum не принимаются. Неизвестные параметры, повторение скалярного параметра, пустое значение — 400. from/to вместе с любым preset, кроме custom, — 400. Начало не раньше 1900-01-01, конец не позже today сервера, from <= to, не больше 366 календарных дней. Timestamp вместо date-only, неполная дата, невозможный день — 400.

| Preset | Включительный диапазон |
|---|---|
| today | today…today |
| last7 | today−6…today |
| last30 | today−29…today |
| thisMonth | первое число текущего месяца…today (MTD) |
| lastMonth | весь предыдущий календарный месяц |
| custom | from…to |

Previous Period: N=число включительных календарных дней; previousTo=from−1 день, previousFrom=from−N дней. Например, 1–7 сентября → 25–31 августа. UTC-фильтр полуоткрытый `[local from midnight, local day after to midnight)`; дни DST могут иметь 23/25 часов. Даты previous могут быть раньше минимальной входной даты.

## Реализованные endpoints

| Метод и путь | Дополнительные параметры | Report.data |
|---|---|---|
| GET /api/analytics/kpi | нет | KpiData |
| GET /api/analytics/managers/ranking | sortBy=GrossProfit\|AverageCheck; default GrossProfit | ManagerRanking[] |
| GET /api/analytics/dynamics | нет | TimeSeriesPoint[] |
| GET /api/analytics/categories | нет | CategoryProfit[] |
| GET /api/analytics/products/top | limit: integer 1…100, default 8 | TopProduct[] |
| GET /api/analytics/sales/recent | page: integer 1…2147483647, default 1; pageSize: integer 1…100, default 8 | SalesPage |

sortBy также принимает `grossProfit`, `averageCheck`, `avgCheck`. Целочисленные параметры содержат только цифры: дроби, знак, пробелы, переполнение — 400. Дополнительные параметры разрешены только у соответствующего маршрута; например limit у kpi отклоняется.

### KPI

```ts
interface KpiData {
  revenue: number; revenuePrev: number; revenueDelta: number | null;
  grossProfit: number; grossProfitPrev: number; grossProfitDelta: number | null;
  margin: number; marginPrev: number; marginDelta: number;
  salesCount: number; salesCountPrev: number; salesCountDelta: number | null;
  avgCheck: number; avgCheckPrev: number; avgCheckDelta: number | null;
  bestManagerId: string | null; bestManager: string | null;
  bestManagerValue: number; bestManagerPrev: number; bestManagerDelta: number | null;
  sparklines: Record<'revenue'|'grossProfit'|'margin'|'salesCount'|'avgCheck'|'bestManager', number[]>;
}
```

Revenue = сумма quantity×unitSalePrice; Cost = сумма quantity×unitCost; GrossProfit = Revenue−Cost. Только Paid. SalesCount — число чеков, не число позиций/единиц. Margin = 100×GP/Revenue, при Revenue=0 → 0. Average Check = Revenue/SalesCount, при отсутствии Paid → 0. Cancelled/Refunded исключены полностью, включая Cost. Отрицательная GP допустима.

Delta = (current−previous)/previous×100 при previous>0; 0→0 даёт 0; 0→ненулевое и previous<0 дают null. marginDelta — разность margin в процентных пунктах. Отношения округлены до двух знаков AwayFromZero только после вычисления delta на точных значениях.

Лучший менеджер: GP DESC, затем точный Average Check DESC, затем UUID ASC. Только менеджеры с текущими Paid, включая неактивных. BestManagerPrev и sparkline относятся к тому же текущему лидеру. Без лидера id/name/delta=null, value/prev=0. Каждый sparkline содержит ровно period.days значений, по одному на день, включая нули.

### Рейтинг менеджеров

```ts
interface ManagerRanking {
  id: string; name: string; initials: string;
  salesCount: number; revenue: number; grossProfit: number;
  avgCheck: number; margin: number;
  grossProfitChange: number | null; avgCheckChange: number | null;
  timeSeries: TimeSeriesPoint[];
}
```

По одной строке на менеджера с Paid в текущем периоде; без продаж в этом периоде в рейтинг не включается. GrossProfit: GP DESC → точный AvgCheck DESC → UUID ASC. AverageCheck: точный AvgCheck DESC → GP DESC → UUID ASC. Сортировка выполняется до округления. Change сравнивает того же менеджера с предыдущим периодом; для отсутствующей предыдущей выборки база равна 0.

`timeSeries` — серверная дневная динамика этого менеджера, включая нули. Поле добавлено для подключения существующей вкладки Comparison без mock-генераторов и дополнительных запросов на менеджера. В этой версии сравнение доступно для менеджеров текущего рейтинга; отдельные lookup/comparison endpoints и выбор менеджеров без текущих Paid не реализованы.

### Dynamics

```ts
interface TimeSeriesPoint {
  date: string; // YYYY-MM-DD в reporting timezone
  revenue: number; grossProfit: number; salesCount: number;
}
```

По точке на каждый календарный день, date ASC. Суммы дневных метрик равны KPI при одинаковом состоянии данных. Backend возвращает все три ряда; переключатель frontend выбирает только отображаемую метрику.

### Категории

```ts
interface CategoryProfit {
  id: string; name: string;
  revenue: number; grossProfit: number;
  revenueShare: number; grossProfitShare: number | null;
}
```

Группировка Paid-позиций по CategoryIdAtSale, а не текущей категории продукта. Вывод категорий с подходящими позициями: SortOrder ASC → UUID ASC. Доли считаются сервером в процентах: при нулевой общей сумме доля=0; при отрицательной общей GP grossProfitShare=null. При положительной общей GP убыточная категория может иметь отрицательную долю, а прибыльная — больше 100%. Округлённые доли не обязаны суммироваться ровно в 100. CSS/fill в API нет.

### Лучшие продукты

```ts
interface TopProduct {
  id: string; // productId:categoryId
  productId: string; categoryId: string;
  name: string; category: string;
  revenue: number; grossProfit: number; margin: number;
}
```

Paid-позиции группируются по ProductId + CategoryIdAtSale. Перемещённый между категориями товар может занимать две строки. Имя берётся из последней подходящей исторической позиции (SaleDate DESC, SaleId DESC, LineNumber DESC); текущее каталоговое имя не переписывает историю. Category — имя связанной исторической категории. Сортировка Revenue DESC → GP DESC → ProductId ASC → CategoryId ASC; limit применяется в SQL после агрегации. Margin рассчитывается сервером. Сумма ограниченного top не обязана равняться KPI.

### Последние продажи

```ts
type SaleStatus = 'Paid' | 'Cancelled' | 'Refunded';
interface RecentSale {
  id: string; date: string; // UTC timestamp
  managerName: string; managerInitials: string;
  customerName: string; customerCompany: string;
  productSummary: string; productCount: number;
  status: SaleStatus; amount: number; grossProfit: number;
}
interface SalesPage {
  items: RecentSale[];
  pageNumber: number; pageSize: number;
  totalCount: number; totalPages: number;
}
```

Период применяется к SaleDate. Все статусы, включая Cancelled/Refunded. amount сохраняет исходную сумму чека при любом статусе; grossProfit=0 для неоплаченных/возвращённых. productSummary — ProductNameAtSale всех позиций в порядке LineNumber через запятую; productCount — число позиций, не количество единиц. Порядок SaleDate DESC → UUID DESC. Page за пределами результата: пустой items, исходный totalCount и запрошенный pageNumber. При totalCount=0 totalPages=0. Offset вычисляется в long без переполнения int.

Count и page читаются в одной Repeatable Read транзакции; агрегация позиций выполняется только для ограниченной страницы. Это два SELECT, независимо от числа чеков/позиций; N+1 нет. При конкурентных изменениях между разными запросами страниц offset pagination может сдвигаться.

## Ошибки

`application/problem+json`, стандартные ProblemDetails; при 400 — errors с именами query-полей. Возвращается первая обнаруженная ошибка. SQL, stack trace, connection string не возвращаются.

```json
{
  "type": "about:blank",
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid query parameters.",
  "instance": "/api/analytics/kpi",
  "errors": { "to": ["Must be on or after from."] },
  "code": "validation_error",
  "traceId": "request-trace-id"
}
```

| HTTP | code | Значение |
|---|---|---|
| 400 | validation_error | Некорректные query/date/range/sort/limit/page |
| 404 | route_not_found | Неизвестный маршрут |
| 405 | method_not_allowed | Метод не поддерживается; сохранён заголовок Allow |
| 500 | internal_error | Непредвиденная внутренняя ошибка |
| 503 | database_unavailable | Ошибка PostgreSQL/подключения либо timeout; можно повторить |

RequestAborted передаётся в async DB-вызовы. Отмена клиентом не превращается в HTTP 500. HTTP query проверяет AnalyticsQuery, доменные границы дополнительно проверяет PeriodResolver. Swagger `/swagger` в Development содержит все шесть маршрутов, query, DTO и ошибки.

## Примеры

```sh
curl 'http://localhost:5026/api/analytics/kpi?preset=last30'
curl 'http://localhost:5026/api/analytics/managers/ranking?from=2026-09-01&to=2026-09-23&sortBy=AverageCheck'
curl 'http://localhost:5026/api/analytics/dynamics?preset=thisMonth'
curl 'http://localhost:5026/api/analytics/categories?preset=lastMonth'
curl 'http://localhost:5026/api/analytics/products/top?from=2025-09-24&to=2026-09-23&limit=8'
curl 'http://localhost:5026/api/analytics/sales/recent?preset=last7&page=2&pageSize=8'
```

Воспроизводимый seed фиксирован на 2025-09-24…2026-09-23; при просмотре позже этого периода выбирайте custom. Автоматического смещения истории к today нет.

## Frontend: фактическое подключение

Frontend находится отдельно от backend-репозитория: `/Users/aleksandrkorakin/Downloads/project`. Изменены `src/App.tsx`, добавлен `src/lib/analytics-api.ts`, адаптированы period-selector и семь dashboard-компонентов, настроен `vite.config.ts`.

- App запрашивает шесть маршрутов через fetch; sortBy меняется по переключателю рейтинга, page/pageSize — по пагинации. Смена периода сбрасывает page=1.
- У активного dashboard/comparison нет runtime-импортов mockData. Старый файл оставлен для неиспользуемых CRM-компонентов.
- KPI, проценты изменений, category shares, рейтинг, дневные ряды и pagination totals приходят с сервера. Frontend только форматирует деньги/даты, выбирает цвета/масштаб графика и отображаемую метрику.
- Period preset передаётся серверу без локального вычисления дат. Custom передаётся как локальные строки yyyy-MM-dd без UTC-сдвига JS Date. В UI показаны фактические Report.period и previous dates; recent timestamps форматируются в reporting timezone.
- Состояния загрузки, API/network error и Retry явные. Ошибка не маскируется нулями. AbortController отменяет старые запросы, проверка request key не позволяет старому ответу подменить новый диапазон.
- null delta/leader отображается как «—», Margin change имеет единицу pp; одноточечный sparkline не делит на ноль. Локальная сортировка рейтинга, финансовые агрегаты и slice всей истории удалены.
- Comparison использует timeSeries из рейтинга, серверные UUID вместо mock sc/mr/pp; пользователь выбирает 2–3 доступных менеджеров.
- Страница/сортировка перезагружают согласованный по фильтру комплект из шести ответов; общий DB snapshot между ними не гарантируется.

Локальный запуск backend: `docker compose up --build -d`. В папке frontend: `npm install` (если зависимости отсутствуют), затем `npm run dev`. Vite dev/preview проксирует `/api` на `http://127.0.0.1:5026`, CORS не нужен. При изменении API_PORT обновите target в vite.config.ts. `npm run build` создаёт frontend dist; для production раздачи нужен same-origin reverse proxy `/api` на backend. Compose пока поднимает PostgreSQL/init/API; frontend запускается отдельно.

Health: GET `/health/live` — процесс, GET `/health/ready` — БД и актуальная схема (200 либо 503). Они не имеют Report envelope и не являются аналитическими маршрутами.

## Проверка

WebApplicationFactory + настоящий PostgreSQL: шесть маршрутов, все presets, custom/aliases/default, полная сверка seed между разрезами, сортировка/nullable changes, page boundaries/статусы, query validation, 404/405/500/503 и Swagger. Дополнительный SQL interceptor подтверждает один SELECT для рейтинга/категорий/продуктов и два для recent. Историческая категория/имя, убыточный Paid и неактивный менеджер проверяются отдельным сценарием. Принципы HTTP тестирования — [ASP.NET Core integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-8.0); proxy — [Vite server.proxy](https://vite.dev/config/server-options#server-proxy).
