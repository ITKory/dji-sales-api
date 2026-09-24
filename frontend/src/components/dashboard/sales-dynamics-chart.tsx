import { useState } from 'react';
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { LineChart, TrendingUp, Hash } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { formatCurrency, formatCompactNumber } from '@/lib/format';
import { EmptyState } from '@/components/dashboard/empty-state';
import type { TimeSeriesPoint, TimeSeriesMetric } from '@/lib/analytics-api';

interface SalesDynamicsChartProps {
  data: TimeSeriesPoint[];
  totals: { revenue: number; grossProfit: number; salesCount: number };
  loading?: boolean;
}

const METRIC_CONFIG: Record<
  TimeSeriesMetric,
  { label: string; key: string; color: string; format: (v: number) => string; icon: React.ReactNode }
> = {
  revenue: {
    label: 'Revenue',
    key: 'revenue',
    color: 'hsl(var(--chart-1))',
    format: (v) => formatCurrency(v, true),
    icon: <LineChart className="h-3.5 w-3.5" />,
  },
  grossProfit: {
    label: 'Gross Profit',
    key: 'grossProfit',
    color: 'hsl(var(--chart-2))',
    format: (v) => formatCurrency(v, true),
    icon: <TrendingUp className="h-3.5 w-3.5" />,
  },
  salesCount: {
    label: 'Number of Sales',
    key: 'salesCount',
    color: 'hsl(var(--chart-3))',
    format: (v) => formatCompactNumber(v),
    icon: <Hash className="h-3.5 w-3.5" />,
  },
};

function ChartTooltip({ active, payload, label, metric }: any) {
  if (!active || !payload?.length) return null;
  const config = METRIC_CONFIG[metric as TimeSeriesMetric];
  const value = payload[0]?.value ?? 0;
  return (
    <div className="rounded-lg border bg-popover p-3 shadow-lg">
      <p className="mb-1.5 text-xs font-semibold text-foreground">{label}</p>
      <div className="flex items-center justify-between gap-6">
        <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
          <span className="h-2 w-2 rounded-full" style={{ background: config.color }} />
          {config.label}
        </span>
        <span className="text-xs font-semibold">{config.format(value)}</span>
      </div>
    </div>
  );
}

function ChartSkeleton() {
  return (
    <div className="space-y-3 p-6">
      <div className="flex justify-between">
        <div className="space-y-2">
          <Skeleton className="h-4 w-32" />
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-5 w-20 mt-1" />
        </div>
        <Skeleton className="h-9 w-64 rounded-lg" />
      </div>
      <div className="relative h-[260px] w-full overflow-hidden rounded-lg border border-border/50">
        <div className="absolute inset-0 flex items-end gap-1 px-4 pb-8">
          {Array.from({ length: 20 }).map((_, i) => (
            <Skeleton
              key={i}
              className="flex-1 rounded-t-sm shimmer-pulse"
              style={{ height: `${20 + Math.sin(i * 0.5) * 30 + Math.cos(i * 0.3) * 20}%`, animationDelay: `${i * 40}ms` }}
            />
          ))}
        </div>
        <Skeleton className="absolute bottom-0 left-0 h-px w-full" />
        <Skeleton className="absolute left-0 top-0 h-full w-px" />
      </div>
    </div>
  );
}



export function SalesDynamicsChart({ data, totals, loading = false }: SalesDynamicsChartProps) {
  const [metric, setMetric] = useState<TimeSeriesMetric>('revenue');
  const config = METRIC_CONFIG[metric];
  const total = totals[metric];

  if (loading) {
    return (
      <Card className="shadow-sm">
        <ChartSkeleton />
      </Card>
    );
  }

  return (
    <Card className="shadow-sm">
      <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-4">
        <div>
          <CardTitle className="text-base font-semibold">Sales Dynamics</CardTitle>
          <CardDescription className="mt-1">
            {config.label} over time
          </CardDescription>
          <p className="mt-2 text-lg font-bold tracking-tight">
            {config.format(total)}
          </p>
        </div>
        <Tabs value={metric} onValueChange={(v) => setMetric(v as TimeSeriesMetric)}>
          <TabsList className="h-9">
            <TabsTrigger value="revenue" className="text-xs gap-1.5">
              <LineChart className="h-3.5 w-3.5" />
              Revenue
            </TabsTrigger>
            <TabsTrigger value="grossProfit" className="text-xs gap-1.5">
              <TrendingUp className="h-3.5 w-3.5" />
              Profit
            </TabsTrigger>
            <TabsTrigger value="salesCount" className="text-xs gap-1.5">
              <Hash className="h-3.5 w-3.5" />
              Sales
            </TabsTrigger>
          </TabsList>
        </Tabs>
      </CardHeader>
      <CardContent className="pl-2 pr-4 pb-4">
        {data.length === 0 ? (
          <div className="flex h-[260px] items-center justify-center">
            <EmptyState
              icon={<LineChart className="h-7 w-7 text-muted-foreground" />}
              title="No data for this period"
              description="Try selecting a different date range to see sales dynamics."
            />
          </div>
        ) : (
          <ResponsiveContainer width="100%" height={260}>
            <AreaChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id="dynGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor={config.color} stopOpacity={0.28} />
                  <stop offset="100%" stopColor={config.color} stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" vertical={false} />
              <XAxis
                dataKey="date"
                tick={{ fontSize: 11, fill: 'hsl(var(--muted-foreground))' }}
                tickLine={false}
                axisLine={false}
                interval="preserveStartEnd"
                minTickGap={24}
              />
              <YAxis
                tick={{ fontSize: 11, fill: 'hsl(var(--muted-foreground))' }}
                tickLine={false}
                axisLine={false}
                tickFormatter={(v) => config.format(v)}
                width={56}
              />
              <Tooltip content={<ChartTooltip metric={metric} />} />
              <Area
                key={metric}
                type="monotone"
                dataKey={config.key}
                stroke={config.color}
                strokeWidth={2.5}
                fill="url(#dynGrad)"
                dot={false}
                activeDot={{ r: 4, fill: config.color, strokeWidth: 2, stroke: 'hsl(var(--card))' }}
                animationDuration={400}
              />
            </AreaChart>
          </ResponsiveContainer>
        )}
      </CardContent>
    </Card>
  );
}
