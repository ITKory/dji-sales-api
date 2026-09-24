import { useState, useEffect } from 'react';
import { BarChart3, Moon, Sun, Download, Bell, Search, LayoutDashboard, GitCompare } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PeriodSelector } from '@/components/period-selector';
import { KpiCards } from '@/components/dashboard/kpi-cards';
import { SalesDynamicsChart } from '@/components/dashboard/sales-dynamics-chart';
import { CategoryProfitChart } from '@/components/dashboard/category-profit-chart';
import { TopProducts } from '@/components/dashboard/top-products';
import { ManagersRanking } from '@/components/dashboard/managers-ranking';
import { RecentSales } from '@/components/dashboard/recent-sales';
import { ManagerComparison } from '@/components/dashboard/manager-comparison';
import {
  type PeriodKey,
  type DateRange,
  type RankingMode,
  type KpiData,
  type ManagerRanking,
  type TimeSeriesPoint,
  type CategoryProfit,
  type TopProduct,
  type SalesPage,
  type ReportPeriod,
  periodQuery,
  getReport,
} from '@/lib/analytics-api';

interface DashboardData {
  period: ReportPeriod;
  kpis: KpiData;
  ranking: ManagerRanking[];
  dynamics: TimeSeriesPoint[];
  categories: CategoryProfit[];
  products: TopProduct[];
  sales: SalesPage;
}

function useTheme() {
  const [theme, setTheme] = useState<'light' | 'dark'>(() => {
    if (globalThis.window !== undefined && globalThis.matchMedia('(prefers-color-scheme: dark)').matches) {
      return 'dark';
    }
    return 'light';
  });

  useEffect(() => {
    document.documentElement.classList.toggle('dark', theme === 'dark');
  }, [theme]);

  return { theme, toggle: () => setTheme((t) => (t === 'dark' ? 'light' : 'dark')) };
}

export default function App() {
  const { theme, toggle } = useTheme();
  const [period, setPeriod] = useState<PeriodKey>('last30');
  const [customRange, setCustomRange] = useState<DateRange | null>(null);
  const [view, setView] = useState<'dashboard' | 'comparison'>('dashboard');

  const [sort, setSort] = useState<RankingMode>('grossProfit');
  const [page, setPage] = useState(1);
  const [retry, setRetry] = useState(0);

  const [result, setResult] = useState<{ key: string; data: DashboardData } | null>(null);
  const [error, setError] = useState<{ key: string; message: string } | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const query = periodQuery(period, customRange);
  const requestKey = `${query}&sortBy=${sort}&page=${page}&retry=${retry}`;

  const data = result?.data ?? null;
  const isStale = result !== null && result.key !== requestKey;
  const errorMessage = error?.key === requestKey ? error.message : null;

  const changePeriod = (next: PeriodKey) => {
    setPeriod(next);
    setPage(1);
  };

  const changeRange = (next: DateRange) => {
    setCustomRange(next);
    setPage(1);
  };

  useEffect(() => {
    const controller = new AbortController();
    const signal = controller.signal;

    async function load() {
      setIsLoading(true);
      setError(null);

      try {
        const [kpi, ranking, dynamics, categories, products, sales] = await Promise.all([
          getReport<KpiData>('kpi', query, signal),
          getReport<ManagerRanking[]>(
            'managers/ranking',
            `${query}&sortBy=${sort === 'avgCheck' ? 'AverageCheck' : 'GrossProfit'}`,
            signal,
          ),
          getReport<TimeSeriesPoint[]>('dynamics', query, signal),
          getReport<CategoryProfit[]>('categories', query, signal),
          getReport<TopProduct[]>('products/top', `${query}&limit=8`, signal),
          getReport<SalesPage>('sales/recent', `${query}&page=${page}&pageSize=8`, signal),
        ]);

        if (signal.aborted) return;

        setResult({
          key: requestKey,
          data: {
            period: kpi.period,
            kpis: kpi.data,
            ranking: ranking.data,
            dynamics: dynamics.data,
            categories: categories.data.map((category, index) => ({
              ...category,
              fill: `hsl(var(--chart-${(index % 5) + 1}))`,
            })),
            products: products.data,
            sales: {
              ...sales.data,
              items: sales.data.items.map((sale) => ({
                ...sale,
                date: new Intl.DateTimeFormat('en-US', {
                  timeZone: sales.period.timeZone,
                  year: 'numeric',
                  month: 'short',
                  day: 'numeric',
                }).format(new Date(sale.date)),
              })),
            },
          },
        });
      } catch (reason) {
        if (!signal.aborted) {
          setError({
            key: requestKey,
            message: reason instanceof Error ? reason.message : 'Unable to load analytics.',
          });
        }
      } finally {
        if (!signal.aborted) {
          setIsLoading(false);
        }
      }
    }

    void load();
    return () => controller.abort();
  }, [query, sort, page, retry, requestKey]);

  return (
    <div className="min-h-screen bg-background">
      <header className="sticky top-0 z-40 border-b bg-card/80 backdrop-blur-md">
        <div className="mx-auto flex h-16 max-w-[1440px] items-center gap-6 px-6">
          <div className="flex items-center gap-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary text-primary-foreground shadow-sm">
              <BarChart3 className="h-5 w-5" />
            </div>
            <div className="flex flex-col leading-none">
              <span className="text-base font-bold tracking-tight">Sales Performance</span>
              <span className="text-xs text-muted-foreground">Analytics Dashboard</span>
            </div>
          </div>

          <Separator orientation="vertical" className="h-8" />

          <div className="relative flex-1 max-w-xs">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Search deals, reps..."
              className="h-9 pl-9 bg-muted/50 border-transparent focus-visible:bg-card focus-visible:border-border"
            />
          </div>

          <div className="flex-1" />

          <PeriodSelector
            period={period}
            onPeriodChange={changePeriod}
            customRange={customRange}
            onCustomRangeChange={changeRange}
          />

          <div className="flex items-center gap-1.5">
            <Button variant="ghost" size="icon" className="h-9 w-9 text-muted-foreground hover:text-foreground">
              <Bell className="h-4 w-4" />
            </Button>
            <Button variant="outline" size="sm" className="h-9 gap-2">
              <Download className="h-4 w-4" />
              Export
            </Button>
            <Button variant="ghost" size="icon" className="h-9 w-9" onClick={toggle}>
              {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
            </Button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-[1440px] px-6 py-6">
        <div className="mb-6 flex items-end justify-between">
          <div>
            <h1 className="text-2xl font-bold tracking-tight">
              {view === 'dashboard' ? 'Dashboard' : 'Manager Comparison'}
            </h1>
            <p className="mt-1 text-sm text-muted-foreground">
              {view === 'dashboard'
                ? 'Monitor your sales performance and pipeline health'
                : 'Compare individual manager performance side-by-side'}
            </p>
          </div>
          <Tabs value={view} onValueChange={(v) => setView(v as 'dashboard' | 'comparison')}>
            <TabsList className="h-9">
              <TabsTrigger value="dashboard" className="gap-1.5 text-xs">
                <LayoutDashboard className="h-3.5 w-3.5" />
                Dashboard
              </TabsTrigger>
              <TabsTrigger value="comparison" className="gap-1.5 text-xs">
                <GitCompare className="h-3.5 w-3.5" />
                Comparison
              </TabsTrigger>
            </TabsList>
          </Tabs>
        </div>

        {errorMessage ? (
          <div role="alert" className="rounded-lg border border-destructive p-6">
            <p>{errorMessage}</p>
            <Button className="mt-3" onClick={() => setRetry((v) => v + 1)}>
              Retry
            </Button>
          </div>
        ) : !data && isLoading ? (
          <p role="status" className="py-20 text-center text-muted-foreground">
            Loading analytics…
          </p>
        ) : view === 'comparison' ? (
          <div className="animate-fade-in-up">
            <ManagerComparison managers={data!.ranking} />
          </div>
        ) : (
          <>
            <div className="animate-fade-in-up">
              <p className="mb-3 text-xs text-muted-foreground">
                {data!.period.from} → {data!.period.to} · {data!.period.timeZone} · Previous:{' '}
                {data!.period.previousFrom} → {data!.period.previousTo}
              </p>
              <KpiCards kpis={data!.kpis} sparklines={data!.kpis.sparklines} />
            </div>

            <div className="mt-6 grid grid-cols-2 gap-4 animate-fade-in-up animate-fade-in-up-delay-1">
              <ManagersRanking
                managers={data!.ranking}
                mode={sort}
                onModeChange={setSort}
              />
              <SalesDynamicsChart data={data!.dynamics} totals={data!.kpis} />
            </div>

            <div className="mt-4 grid grid-cols-2 gap-4 animate-fade-in-up animate-fade-in-up-delay-2">
              <CategoryProfitChart
                data={data!.categories}
                totalRevenue={data!.kpis.revenue}
              />
              <TopProducts products={data!.products} />
            </div>

            <div className="mt-4 animate-fade-in-up animate-fade-in-up-delay-3">
              <RecentSales
                sales={data!.sales.items}
                page={data!.sales.pageNumber}
                pageSize={data!.sales.pageSize}
                totalCount={data!.sales.totalCount}
                totalPages={data!.sales.totalPages}
                onPageChange={setPage}
                loading={isStale}
              />
            </div>
          </>
        )}
      </main>
    </div>
  );
}
